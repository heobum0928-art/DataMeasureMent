using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
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

    public partial class MainView : UserControl {
        private const string ViewerMemoryImageLabel = "(memory)";
        private MainWindow mParentWindow;
        private DeviceHandler pDev;
        private SequenceHandler pSeq;
        private LightHandler pLight;
        private readonly object mDrawInterlock = new object();
        private Dictionary<string, SequenceContext> ContextList;
        private Task GrabTask;
        private readonly List<IMainView> CustomViewList = new List<IMainView>();
        private string _lastRenderedImagePath;
        private double _drawScale = 1.0;
        private InspectionViewModel _viewModel;

        public bool IsEditable { get; set; }
        public double DrawScale {
            get { return _drawScale; }
            set { _drawScale = value; }
        }

        public MainView() {
            InitializeComponent();
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

            DrawScale = pDev.Config.DrawScale;
            UpdatePointerLabel(0, 0, null);

            var recipeManager = SystemHandler.Handle.Sequences.RecipeManager;
            _viewModel = new InspectionViewModel(recipeManager);
            DataContext = _viewModel;
        }

        private void TreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e) {
            if (_viewModel == null) return;
            _viewModel.SelectedNode = e.NewValue;

            // Canvas update (per D-02)
            if (e.NewValue is ShotNodeViewModel shotVm) {
                DisplayShotImage(shotVm);
            } else if (e.NewValue is FAINodeViewModel faiVm) {
                // Find parent shot and display its image
                foreach (var shot in _viewModel.Shots) {
                    if (shot.FAIItems.Contains(faiVm)) {
                        DisplayShotImage(shot);
                        break;
                    }
                }
            } else {
                label_message.Content = "NO Image";
                label_message.Visibility = Visibility.Visible;
            }
        }

        /// <summary>Displays the image for the selected Shot node. LoadImage clones the HImage internally so disposal is safe.</summary>
        private void DisplayShotImage(ShotNodeViewModel shotVm) {
            if (shotVm.ShotConfig.HasImage) {
                HImage img = null;
                try {
                    img = shotVm.ShotConfig.GetImage();
                    if (img != null) {
                        halconViewer.LoadImage(img);
                        label_message.Visibility = Visibility.Collapsed;
                    } else {
                        label_message.Content = "이미지 로드 실패";
                        label_message.Visibility = Visibility.Visible;
                    }
                } finally {
                    img?.Dispose();
                }
            } else {
                label_message.Content = "NO Image";
                label_message.Visibility = Visibility.Visible;
            }
        }

        private void Btn_Add_Click(object sender, RoutedEventArgs e) {
            if (_viewModel == null) return;
            if (_viewModel.SelectedNode is ShotNodeViewModel || _viewModel.SelectedNode is FAINodeViewModel) {
                _viewModel.AddFAIToSelectedShot();
            } else {
                _viewModel.AddShot();
            }
        }

        private void Btn_Remove_Click(object sender, RoutedEventArgs e) {
            if (_viewModel == null) return;
            _viewModel.RemoveSelected();
        }

        private void Btn_Edit_Click(object sender, RoutedEventArgs e) {
            if (_viewModel == null) return;
            _viewModel.RenameSelected();
        }

        private void FAIResults_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            // Phase 2: ROI highlight will be implemented here once RoiDefinition teaching data exists
        }

        // Keep public methods called by MainWindow and InspectionListView

        public void AddCustomControl(string name, UserControl control) {
            // TabControl no longer present — custom views are not shown in Phase 1 UI.
            // Phase 2 will provide a dedicated panel for custom views.
            if (control is IMainView mainView) {
                CustomViewList.Add(mainView);
                if (ContextList != null) {
                    mainView.ContextList = ContextList;
                }
            }
        }

        public void ChangeTabPage(int index) {
            // TabControl removed in Phase 1 UI redesign. No-op.
        }

        public void SetParam(ESequence seqID, ParamBase param) {
            if (pSeq == null || pSeq[seqID] == null) return;
            string selectedSeq = pSeq[seqID].Name;
            if (ContextList != null && ContextList.ContainsKey(selectedSeq)) {
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
                        label_message.Visibility = Visibility.Visible;

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
                label_message.Visibility = Visibility.Visible;

                foreach (IMainView customView in CustomViewList) {
                    customView.Display(param.SequenceName, "Loaded Image", label_message.Foreground, param.ActionName);
                }
            }
            catch (Exception ex) {
                Logging.PrintErrLog((int)ELogType.Error, ex.Message);
                label_message.Foreground = Brushes.Red;
                label_message.Content = string.Format("{0}\nLoad Fail", param.DeviceName);
                label_message.Visibility = Visibility.Visible;
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
                    label_message.Visibility = Visibility.Visible;

                    foreach (IMainView customView in CustomViewList) {
                        customView.Display(param.Parent.Name, resultStr, label_message.Foreground, param.OwnerName);
                    }
                });
            }
        }

        public void DisplaySequenceContext(SequenceContext context) {
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
                    label_message.Visibility = Visibility.Visible;

                    foreach (IMainView customView in CustomViewList) {
                        if (context.ActionParam != null)
                            customView.Display(context.Source.Name, resultStr, label_message.Foreground, context.ActionParam.OwnerName);
                        else
                            customView.Display(context.Source.Name, resultStr, label_message.Foreground);
                    }
                });
            }
        }

        public void SetManualToolsEnabled(bool enabled) {
            halconViewer.SetManualToolsEnabled(enabled);
        }

        private void ExecuteOnUi(Action action) {
            if (Dispatcher.CheckAccess()) {
                action();
                return;
            }

            Dispatcher.Invoke(action);
        }

        private void HalconViewer_PointerInfoChanged(object sender, MainViewerPointerChangedEventArgs e) {
            UpdatePointerLabel(e.X, e.Y, e.GrayValue);
        }

        private void UpdatePointerLabel(double x, double y, double? grayValue) {
            if (label_pos == null) {
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

        private void MainView_Unloaded(object sender, RoutedEventArgs e) {
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
    }
}
