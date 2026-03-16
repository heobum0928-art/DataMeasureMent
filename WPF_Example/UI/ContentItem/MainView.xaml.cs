using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using HalconDotNet;
using Microsoft.Win32;
using ReringProject.Halcon.Models;
using ReringProject.Halcon.Services;
using ReringProject.Define;
using ReringProject.Device;
using ReringProject.Sequence;
using ReringProject.Setting;
using ReringProject.Utility;

namespace ReringProject.UI {

    public enum MainViewMode {
        All,
        Original,
        Result,
    }

    public class MainViewModel : INotifyPropertyChanged {
        private string _SelectedSeqName;
        public string SelectedSeqName {
            get { return _SelectedSeqName; }
            set { _SelectedSeqName = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("SelectedSeqName")); }
        }

        private int _SelectedSeqIndex;
        public int SelectedSeqIndex {
            get { return _SelectedSeqIndex; }
            set { _SelectedSeqIndex = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("SelectedSeqIndex")); }
        }

        private string _SelectedViewMode;
        public string SelectedViewMode {
            get { return _SelectedViewMode; }
            set { _SelectedViewMode = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("SelectedViewMode")); }
        }

        private int _SelectedViewIndex;
        public int SelectedViewIndex {
            get { return _SelectedViewIndex; }
            set { _SelectedViewIndex = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("SelectedViewIndex")); }
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }

    public partial class MainView : UserControl {
        private const string COMBOBOX_SEQUENCE_ALL = "All";
        private const string ViewerMemoryImageLabel = "(memory)";
        private MainWindow mParentWindow;
        private DeviceHandler pDev;
        private SequenceHandler pSeq;
        private LightHandler pLight;
        private readonly object mDrawInterlock = new object();
        private Dictionary<string, SequenceContext> ContextList;
        private Task GrabTask;
        private bool dragStarted;
        private MainViewModel Model;
        private readonly List<IMainView> CustomViewList = new List<IMainView>();
        private string _lastRenderedImagePath;
        private double _drawScale = 1.0;

        public bool IsEditable { get; set; }

        public double DrawScale {
            get { return _drawScale; }
            set {
                _drawScale = value;
                if (slider_scale != null) {
                    slider_scale.Value = Math.Max(slider_scale.Minimum, Math.Min(slider_scale.Maximum, value * 100));
                }
            }
        }

        public MainView() {
            InitializeComponent();
            Model = new MainViewModel();
            DataContext = Model;
            halconViewer.PointerInfoChanged += HalconViewer_PointerInfoChanged;
            Unloaded += MainView_Unloaded;
        }

        private void MainView_Loaded(object sender, RoutedEventArgs e) {
            halconViewer.PointerInfoChanged -= HalconViewer_PointerInfoChanged;
            halconViewer.PointerInfoChanged += HalconViewer_PointerInfoChanged;
            mParentWindow = (MainWindow)System.Windows.Window.GetWindow(this);
            pDev = SystemHandler.Handle.Devices;
            pSeq = SystemHandler.Handle.Sequences;
            pLight = SystemHandler.Handle.Lights;
            ContextList = pSeq.GetContextDictionary();

            foreach (IMainView customView in CustomViewList) {
                customView.ContextList = ContextList;
            }

            comboBox_viewMode.Items.Clear();
            foreach (var name in Enum.GetNames(typeof(MainViewMode))) {
                comboBox_viewMode.Items.Add(name.Replace("_", " "));
            }
            if (comboBox_viewMode.Items.Count > 0) comboBox_viewMode.SelectedIndex = 0;

            comboBox_sequence.Items.Clear();
            comboBox_sequence.Items.Add(COMBOBOX_SEQUENCE_ALL);
            for (int i = 0; i < pSeq.Count; i++) {
                comboBox_sequence.Items.Add(pSeq[i].Name);
            }
            if (comboBox_sequence.Items.Count > 0) comboBox_sequence.SelectedIndex = 0;

            DrawScale = pDev.Config.DrawScale;
            UpdatePointerLabel(0, 0, null);
        }

        public void AddCustomControl(string name, UserControl control) {
            var item = new TabItem { Header = name, Visibility = Visibility.Visible, Height = 42, Content = control };
            tabControl_view.Items.Add(item);
            if (control is IMainView mainView) {
                CustomViewList.Add(mainView);
                mainView.ContextList = ContextList;
            }
        }

        public void ChangeTabPage(int index) {
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Normal, new Action(() => { tabControl_view.SelectedIndex = index; }));
        }

        public void SetParam(ESequence seqID, ParamBase param) {
            if (pSeq[seqID] == null) return;
            string selectedSeq = pSeq[seqID].Name;
            Model.SelectedSeqIndex = comboBox_sequence.Items.IndexOf(COMBOBOX_SEQUENCE_ALL);
            if (Model.SelectedSeqName == COMBOBOX_SEQUENCE_ALL) {
                DisplayParam(ContextList[selectedSeq], param);
            }
        }

        public async void GrabAndDisplay(ICameraParam param, bool eventCall = false) {
            if (param == null || !pSeq.IsIdle || GrabTask != null) return;

            GrabTask = Task.Run(() => {
                lock (mDrawInterlock) {
                    pLight.ApplyLight(param);
                    HImage grabbedHalconImage = pDev.GrabHalconImage(param);
                    param.PutImage(grabbedHalconImage);

                    ExecuteOnUi(() => {
                        var resultStr = "Grab Fail";
                        var brush = Brushes.Red;
                        if (pDev[param.DeviceName] == null) {
                            resultStr = "Device Not Opened";
                        }
                        else if (DisplayToViewer(grabbedHalconImage, ConvertParamRects(param as ParamBase))) {
                            resultStr = pDev[param.DeviceName].IsGrabFromFile ? "Grab From File" : "Grab Success";
                            brush = Brushes.Lime;
                        }

                        label_message.Foreground = brush;
                        label_message.Content = string.Format(
                            "{0}\n{1} ({2:0.00}s)\n{3}",
                            param.DeviceName,
                            resultStr,
                            pDev[param.DeviceName].ElapsedTime.TotalMilliseconds / 1000.0,
                            BuildViewerStateSummary(
                                _lastRenderedImagePath,
                                ConvertParamRects(param as ParamBase),
                                null));

                        foreach (IMainView customView in CustomViewList) {
                            customView.Display(param.SequenceName, resultStr, brush, param.ActionName);
                        }
                    });
                }
            });

            await GrabTask;
            GrabTask.Dispose();
            GrabTask = null;
        }

        public void LoadAndDisplay(ICameraParam param) {
            if (param == null) return;

            var dialog = new OpenFileDialog {
                Filter = "Image Files (*.bmp;*.jpg;*.jpeg;*.png;*.tif;*.tiff)|*.bmp;*.jpg;*.jpeg;*.png;*.tif;*.tiff|All Files (*.*)|*.*"
            };
            if (dialog.ShowDialog() != true) {
                return;
            }

            try {
                halconViewer.LoadImage(dialog.FileName);
                halconViewer.UpdateDisplayState(ConvertParamRects(param as ParamBase), null, null);
                _lastRenderedImagePath = dialog.FileName;
                if (param is IOfflineImageParam offlineImageParam) {
                    offlineImageParam.SetLatestImagePath(dialog.FileName);
                }

                label_message.Foreground = Brushes.DeepSkyBlue;
                label_message.Content = string.Format(
                    "{0}\nLoaded Image\n{1}",
                    param.DeviceName,
                    BuildViewerStateSummary(
                        dialog.FileName,
                        ConvertParamRects(param as ParamBase),
                        null));

                foreach (IMainView customView in CustomViewList) {
                    customView.Display(param.SequenceName, "Loaded Image", label_message.Foreground, param.ActionName);
                }
            }
            catch (Exception ex) {
                Logging.PrintErrLog((int)ELogType.Error, ex.Message);
                label_message.Foreground = Brushes.Red;
                label_message.Content = string.Format("{0}\nLoad Fail", param.DeviceName);
            }
        }

        public void DisplayParam(SequenceContext context, ParamBase param) {
            lock (mDrawInterlock) {
                ExecuteOnUi(() => {
                    DisplayContextToViewer(context, ConvertParamRects(param));
                    var resultStr = string.Format("{0}\n{1} ({2:0.00}s)", param, context.ResultString, context.Timer.Elapsed.TotalMilliseconds / 1000.0);
                    label_message.Content = string.Format(
                        "{0}\n{1}",
                        resultStr,
                        BuildViewerStateSummary(context.ResultImagePath, ConvertParamRects(param), context.InspectionOverlays));
                    label_message.Foreground = GetResultBrush(context.Result);

                    foreach (IMainView customView in CustomViewList) {
                        customView.Display(param.Parent.Name, resultStr, label_message.Foreground, param.OwnerName);
                    }
                });
            }
        }

        public void DisplaySequenceContext(SequenceContext context) {
            string selectedItem = Model.SelectedSeqName;
            if ((selectedItem != COMBOBOX_SEQUENCE_ALL) && (selectedItem != context.Source.Name)) {
                return;
            }

            lock (mDrawInterlock) {
                ExecuteOnUi(() => {
                    DisplayContextToViewer(context, ConvertParamRects(context.ActionParam));
                    string name = context.ActionParam != null ? context.ActionParam.ToString() : context.Source.Name;
                    string resultStr = string.Format("{0}\n{1} ({2:0.00}s)", name, context.ResultString, context.Timer.Elapsed.TotalMilliseconds / 1000.0);
                    label_message.Content = string.Format(
                        "{0}\n{1}",
                        resultStr,
                        BuildViewerStateSummary(context.ResultImagePath, ConvertParamRects(context.ActionParam), context.InspectionOverlays));
                    label_message.Foreground = GetResultBrush(context.Result);

                    foreach (IMainView customView in CustomViewList) {
                        if (context.ActionParam != null)
                            customView.Display(context.Source.Name, resultStr, label_message.Foreground, context.ActionParam.OwnerName);
                        else
                            customView.Display(context.Source.Name, resultStr, label_message.Foreground);
                    }
                });
            }
        }

        public void SetManualToolsEnabled(bool enabled)
        {
            halconViewer.SetManualToolsEnabled(enabled);
        }

        private void ExecuteOnUi(Action action) {
            if (Dispatcher.CheckAccess()) {
                action();
                return;
            }

            Dispatcher.Invoke(action);
        }

        private void HalconViewer_PointerInfoChanged(object sender, MainViewerPointerChangedEventArgs e)
        {
            UpdatePointerLabel(e.X, e.Y, e.GrayValue);
        }

        private void UpdatePointerLabel(double x, double y, double? grayValue)
        {
            if (label_pos == null)
            {
                return;
            }

            label_pos.Content = string.Format(
                "X:{0:0.0}, Y:{1:0.0}, G:{2}",
                x,
                y,
                grayValue.HasValue ? grayValue.Value.ToString("0.0") : "-");
        }

        private bool DisplayToViewer(HImage img, IEnumerable<RoiDefinition> rois) {
            try {
                if (img == null) {
                    return false;
                }

                halconViewer.LoadImage(img);
                halconViewer.UpdateDisplayState(rois, null, null);
                _lastRenderedImagePath = ViewerMemoryImageLabel;
                return true;
            }
            catch (Exception ex) {
                Logging.PrintErrLog((int)ELogType.Error, ex.Message);
                return false;
            }
        }


        private bool DisplayContextToViewer(SequenceContext context, IEnumerable<RoiDefinition> rois) {
            if (context == null) {
                return false;
            }

            var roiList = rois == null ? new List<RoiDefinition>() : rois.ToList();

            if (context.ResultHalconImage != null) {
                try {
                    halconViewer.LoadImage(context.ResultHalconImage);
                    halconViewer.UpdateDisplayState(roiList, context.InspectionOverlays, null);
                    return true;
                }
                catch (Exception ex) {
                    Logging.PrintErrLog((int)ELogType.Error, ex.Message);
                }
            }

            if (!string.IsNullOrWhiteSpace(context.ResultImagePath) && File.Exists(context.ResultImagePath)) {
                try {
                    if (!string.Equals(halconViewer.CurrentImagePath, context.ResultImagePath, StringComparison.OrdinalIgnoreCase)) {
                        halconViewer.LoadImage(context.ResultImagePath);
                    }
                    halconViewer.UpdateDisplayState(roiList, context.InspectionOverlays, null);
                    return true;
                }
                catch (Exception ex) {
                    Logging.PrintErrLog((int)ELogType.Error, ex.Message);
                }
            }

            halconViewer.UpdateDisplayState(roiList, context.InspectionOverlays, null);
            return true;
        }
        private void MainView_Unloaded(object sender, RoutedEventArgs e)
        {
            halconViewer.PointerInfoChanged -= HalconViewer_PointerInfoChanged;
        }

        private static List<RoiDefinition> ConvertParamRects(ParamBase param) {
            var provider = param as IHalconTeachingProvider;
            if (provider != null) {
                var viewerRois = provider.GetViewerRois();
                if (viewerRois != null) {
                    var taught = viewerRois.Where(roi => roi != null).Select(roi => roi.Clone()).ToList();
                    if (taught.Count > 0) {
                        return taught;
                    }
                }
            }

            var rois = new List<RoiDefinition>();
            if (param == null) return rois;

            for (int i = 0; i < param.GetRectCount(); i++) {
                if (!param.GetRect(i, out System.Windows.Rect rect)) continue;
                param.GetRectName(i, out string name);
                rois.Add(new RoiDefinition {
                    Id = "Rect_" + i,
                    Name = string.IsNullOrWhiteSpace(name) ? "Rect " + i : name,
                    Row1 = rect.Top,
                    Column1 = rect.Left,
                    Row2 = rect.Bottom,
                    Column2 = rect.Right,
                    IsTaught = true
                });
            }

            return rois;
        }

        private static Brush GetResultBrush(EContextResult result) {
            switch (result) {
                case EContextResult.Pass:
                    return Brushes.Lime;
                case EContextResult.Fail:
                    return Brushes.Red;
                default:
                    return Brushes.Yellow;
            }
        }

        private static string BuildViewerStateSummary(string imagePath, IEnumerable<RoiDefinition> rois, IEnumerable<EdgeInspectionOverlay> overlays) {
            var roiCount = rois == null ? 0 : rois.Count();
            var overlayCount = overlays == null ? 0 : overlays.Count();
            return string.Format(
                "IMG:{0} | ROI:{1} | OVR:{2}",
                string.IsNullOrWhiteSpace(imagePath) ? "null" : Path.GetFileName(imagePath),
                roiCount,
                overlayCount);
        }

        private void ComboBox_sequence_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            if (Model.SelectedSeqIndex < 0) return;
            string selectedName = Model.SelectedSeqName;
            if (selectedName == COMBOBOX_SEQUENCE_ALL) return;
            DisplaySequenceContext(ContextList[selectedName]);
        }

        private void ComboBox_viewMode_SelectionChanged(object sender, SelectionChangedEventArgs e) {
        }

        private void Slider_DragCompleted(object sender, DragCompletedEventArgs e) {
            DrawScale = slider_scale.Value / 100.0;
            dragStarted = false;
        }

        private void Slider_DragStarted(object sender, DragStartedEventArgs e) {
            dragStarted = true;
        }

        private void Slider_scale_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) {
            if (!IsLoaded || dragStarted) return;
            DrawScale = slider_scale.Value / 100.0;
        }
    }
}












