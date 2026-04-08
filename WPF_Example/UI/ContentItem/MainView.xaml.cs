using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
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
        public bool IsEditable { get; set; }
        public double DrawScale {
            get { return _drawScale; }
            set { _drawScale = value; }
        }

        // Phase 2: Drawing mode state
        private enum ECanvasMode { None, RectRoi, PolygonRoi, Calibration }
        private ECanvasMode _canvasMode = ECanvasMode.None;
        private FAIConfig _editingFai;
        private readonly List<System.Windows.Point> _polygonPoints = new List<System.Windows.Point>();
        private readonly List<System.Windows.Point> _calibrationPoints = new List<System.Windows.Point>();

        // Track last known image coordinates from PointerInfoChanged (used for polygon/calibration clicks)
        private double _lastPointerRow, _lastPointerCol;

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
            PreviewKeyDown += MainView_PreviewKeyDown;
        }

        /// <summary>Displays the shot image associated with the selected FAIConfig. Per D-12.
        /// FAIConfig itself does not store an image; the parent ShotConfig holds it.</summary>
        public void DisplayFAIImage(FAIConfig fai) {
            if (fai == null) {
                label_message.Content = "NO Image";
                label_message.Visibility = Visibility.Visible;
                return;
            }
            // FAIConfig owner is the ShotConfig that was passed as owner at construction
            ShotConfig shot = fai.Owner as ShotConfig;
            DisplayShotImage(shot);
        }

        /// <summary>Displays the image stored in the given ShotConfig on the canvas.</summary>
        private void DisplayShotImage(ShotConfig shot) {
            if (shot != null && shot.HasImage) {
                HImage img = null;
                try {
                    img = shot.GetImage();
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

        /// <summary>Binds DataGrid to the InspectionViewModel's FAIResults collection.</summary>
        public void SetFAIResultSource(InspectionViewModel vm) {
            dataGrid_faiResults.SetBinding(
                System.Windows.Controls.DataGrid.ItemsSourceProperty,
                new System.Windows.Data.Binding("FAIResults") { Source = vm });
        }

        private void FAIResults_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            var selectedRow = dataGrid_faiResults.SelectedItem as FAIResultRow;
            if (selectedRow == null) {
                // No selection: show all ROIs without highlight
                var allRois = GetCurrentFAIRois();
                if (allRois.Count > 0)
                    halconViewer.UpdateDisplayState(allRois, null, null, null);
                return;
            }

            // Show all ROIs, highlight selected FAI's ROI (per D-01, D-03)
            var rois = GetCurrentFAIRois();
            string selectedRoiId = selectedRow.FAIName;
            halconViewer.UpdateDisplayState(rois, selectedRoiId, null, null);

            // If selected FAI has no ROI taught, show hint text
            var selectedFai = selectedRow.SourceFAI;
            if (selectedFai != null && (selectedFai.ROI_Length1 <= 0 || selectedFai.ROI_Length2 <= 0)) {
                label_message.Content = "ROI not set";
                label_message.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFAAAAAA"));
                label_message.Visibility = Visibility.Visible;
            }
        }

        /// <summary>Collects RoiDefinitions from all FAIs of the currently displayed shot.</summary>
        private List<RoiDefinition> GetCurrentFAIRois() {
            var result = new List<RoiDefinition>();
            // Iterate through all rows in the DataGrid (each row = one FAI)
            foreach (var item in dataGrid_faiResults.Items) {
                var row = item as FAIResultRow;
                if (row?.SourceFAI != null) {
                    var roi = row.SourceFAI.ToRoiDefinition();
                    if (roi.IsTaught)
                        result.Add(roi);
                }
            }
            return result;
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
                    var elapsed = context.Timer != null ? context.Timer.Elapsed.TotalMilliseconds / 1000.0 : 0; //260407 hbk Timer null 체크 추가
                    var resultStr = string.Format("{0}\n{1} ({2:0.00}s)", param, context.ResultString, elapsed);
                    label_message.Content = string.Format(
                        "{0}\n{1}",
                        resultStr,
                        BuildViewerStateSummary(context.ResultImagePath, ConvertParamRects(param), context.InspectionOverlays));
                    label_message.Foreground = GetResultBrush(context.Result);
                    label_message.Visibility = Visibility.Visible;

                    string seqName = param.Parent?.Name ?? context.Source?.Name ?? ""; //260407 hbk Parent null 안전 처리 (동적 Shot/FAI 대응)
                    foreach (IMainView customView in CustomViewList) {
                        customView.Display(seqName, resultStr, label_message.Foreground, param.OwnerName);
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
            // Phase 2: Track last known image coordinates for polygon/calibration click handlers
            _lastPointerRow = e.Y;
            _lastPointerCol = e.X;
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

        // Phase 2: Escape key cancels any active drawing mode
        private void MainView_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e) {
            if (e.Key == System.Windows.Input.Key.Escape && _canvasMode != ECanvasMode.None) {
                ExitCanvasMode();
                e.Handled = true;
            }
        }

        // Phase 2: Exit drawing mode and reset all state (rect + polygon; calibration cleanup added in Task 3)
        private void ExitCanvasMode() {
            // Unsubscribe polygon mouse handlers (safe to call even if not subscribed)
            halconViewer.MouseLeftButtonDown -= HalconViewer_PolygonMouseDown;
            halconViewer.MouseRightButtonDown -= HalconViewer_PolygonRightClick;

            _canvasMode = ECanvasMode.None;
            _editingFai = null;
            btn_rectRoi.IsChecked = false;
            btn_polygonRoi.IsChecked = false;
            label_drawHint.Visibility = Visibility.Collapsed;
            label_pointCount.Visibility = Visibility.Collapsed;
            halconViewer.ClearPolygonDraft();
            _polygonPoints.Clear();

            // Calibration cleanup (added in Task 3)
            halconViewer.MouseLeftButtonDown -= HalconViewer_CalibrationMouseDown;
            btn_calibrate.Content = "Calibrate";
            _calibrationPoints.Clear();
        }

        // Phase 2: Task 1 — Rect ROI drawing mode
        private void RectRoiButton_Click(object sender, RoutedEventArgs e) {
            if (btn_rectRoi.IsChecked == true) {
                ExitCanvasMode();
                _canvasMode = ECanvasMode.RectRoi;
                btn_rectRoi.IsChecked = true;

                var selectedRow = dataGrid_faiResults.SelectedItem as FAIResultRow;
                if (selectedRow?.SourceFAI == null) {
                    CustomMessageBox.Show("FAI를 먼저 선택하세요.", "Rect ROI");
                    ExitCanvasMode();
                    return;
                }
                _editingFai = selectedRow.SourceFAI;

                label_drawHint.Content = "드래그하여 ROI를 설정하세요";
                label_drawHint.Visibility = Visibility.Visible;
                halconViewer.StartRectangleDrawing();
            }
            else {
                CommitRectRoi();
            }
        }

        private void CommitRectRoi() {
            if (_canvasMode != ECanvasMode.RectRoi || _editingFai == null) {
                ExitCanvasMode();
                return;
            }

            var roi = halconViewer.CommitActiveRectangle();
            if (roi != null) {
                // Convert RoiDefinition bounding box back to center+half-lengths (per D-05: phi=0 for new ROI)
                _editingFai.ROI_Row = (roi.Row1 + roi.Row2) / 2.0;
                _editingFai.ROI_Col = (roi.Column1 + roi.Column2) / 2.0;
                _editingFai.ROI_Phi = 0.0;  // Always 0 for new ROI (per D-05: no Rectangle2)
                _editingFai.ROI_Length1 = (roi.Row2 - roi.Row1) / 2.0;
                _editingFai.ROI_Length2 = (roi.Column2 - roi.Column1) / 2.0;

                // Refresh canvas to show new ROI
                var rois = GetCurrentFAIRois();
                halconViewer.UpdateDisplayState(rois, _editingFai.FAIName, null, null);
            }
            ExitCanvasMode();
        }

        // Phase 2: Task 2 — Polygon ROI drawing mode
        private void PolygonRoiButton_Click(object sender, RoutedEventArgs e) {
            if (btn_polygonRoi.IsChecked == true) {
                ExitCanvasMode();
                _canvasMode = ECanvasMode.PolygonRoi;
                btn_polygonRoi.IsChecked = true;

                var selectedRow = dataGrid_faiResults.SelectedItem as FAIResultRow;
                if (selectedRow?.SourceFAI == null) {
                    CustomMessageBox.Show("FAI를 먼저 선택하세요.", "Polygon ROI");
                    ExitCanvasMode();
                    return;
                }
                _editingFai = selectedRow.SourceFAI;
                _polygonPoints.Clear();

                label_drawHint.Content = "점을 클릭, 우클릭으로 완성 (최소 3점)";
                label_drawHint.Visibility = Visibility.Visible;
                label_pointCount.Content = "0 / 20 pts";
                label_pointCount.Visibility = Visibility.Visible;

                halconViewer.MouseLeftButtonDown += HalconViewer_PolygonMouseDown;
                halconViewer.MouseRightButtonDown += HalconViewer_PolygonRightClick;
            }
            else {
                ExitCanvasMode();
            }
        }

        private void HalconViewer_PolygonMouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e) {
            if (_canvasMode != ECanvasMode.PolygonRoi) return;

            if (_polygonPoints.Count >= 20) {
                label_pointCount.Content = "20 / 20 pts MAX";
                return;
            }

            var imagePoint = new System.Windows.Point(_lastPointerCol, _lastPointerRow);
            _polygonPoints.Add(imagePoint);
            label_pointCount.Content = string.Format("{0} / 20 pts", _polygonPoints.Count);

            halconViewer.SetPolygonDraft(_polygonPoints, "red");
            e.Handled = true;
        }

        private void HalconViewer_PolygonRightClick(object sender, System.Windows.Input.MouseButtonEventArgs e) {
            if (_canvasMode != ECanvasMode.PolygonRoi) return;

            if (_polygonPoints.Count >= 3) {
                CompletePolygon();
            }
            e.Handled = true;
        }

        private void CompletePolygon() {
            if (_editingFai == null || _polygonPoints.Count < 3) return;

            var sb = new StringBuilder();
            for (int i = 0; i < _polygonPoints.Count; i++) {
                if (i > 0) sb.Append(";");
                sb.AppendFormat("{0:F1},{1:F1}", _polygonPoints[i].X, _polygonPoints[i].Y);
            }
            _editingFai.PolygonPoints = sb.ToString();

            halconViewer.SetPolygonDraft(_polygonPoints, "blue");

            var rois = GetCurrentFAIRois();
            halconViewer.UpdateDisplayState(rois, _editingFai.FAIName, null, null);

            ExitCanvasMode();
        }

        // Phase 2: Task 3 — 2-point calibration flow
        private void CalibrateButton_Click(object sender, RoutedEventArgs e) {
            ExitCanvasMode();
            _canvasMode = ECanvasMode.Calibration;
            _calibrationPoints.Clear();

            btn_calibrate.Content = "Pick Point 1";
            label_drawHint.Content = "캔버스에서 첫 번째 점을 클릭하세요";
            label_drawHint.Visibility = Visibility.Visible;

            halconViewer.MouseLeftButtonDown += HalconViewer_CalibrationMouseDown;
        }

        private void HalconViewer_CalibrationMouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e) {
            if (_canvasMode != ECanvasMode.Calibration) return;

            var pos = new System.Windows.Point(_lastPointerCol, _lastPointerRow);
            _calibrationPoints.Add(pos);
            e.Handled = true;

            if (_calibrationPoints.Count == 1) {
                btn_calibrate.Content = "Pick Point 2";
                label_drawHint.Content = "캔버스에서 두 번째 점을 클릭하세요";
            }
            else if (_calibrationPoints.Count == 2) {
                halconViewer.MouseLeftButtonDown -= HalconViewer_CalibrationMouseDown;
                FinishCalibration();
            }
        }

        private void FinishCalibration() {
            var p1 = _calibrationPoints[0];
            var p2 = _calibrationPoints[1];

            double dx = p2.X - p1.X;
            double dy = p2.Y - p1.Y;
            double pixelDistance = Math.Sqrt(dx * dx + dy * dy);

            if (pixelDistance < 1.0) {
                CustomMessageBox.Show("두 점 사이의 거리가 너무 가깝습니다.", "캘리브레이션");
                ExitCanvasMode();
                return;
            }

            // NOTE: class name typo in original code: TextInputBoxWinidow (not Window)
            var dlg = new TextInputBoxWinidow(
                string.Format("두 점 사이의 실제 거리(mm)를 입력하세요:\n(픽셀 거리: {0:F1} px)", pixelDistance),
                "");
            dlg.Title = "실제 거리 입력";
            dlg.Owner = Window.GetWindow(this);

            if (dlg.ShowDialog() == true) {
                double realMm;
                if (double.TryParse(dlg.Text, out realMm) && realMm > 0) {
                    double mmPerPixel = realMm / pixelDistance;

                    ApplyCalibrationResult(mmPerPixel);

                    // Show confirmation for 3 seconds (per UI-SPEC)
                    label_message.Content = string.Format("1 px = {0:F4} mm 적용됨", mmPerPixel);
                    label_message.Foreground = new SolidColorBrush(Colors.White);
                    label_message.Visibility = Visibility.Visible;

                    var timer = new System.Windows.Threading.DispatcherTimer();
                    timer.Interval = TimeSpan.FromSeconds(3);
                    timer.Tick += (s, args) => {
                        timer.Stop();
                        label_message.Visibility = Visibility.Collapsed;
                    };
                    timer.Start();
                }
                else {
                    CustomMessageBox.Show("유효한 숫자를 입력하세요.", "캘리브레이션");
                }
            }

            ExitCanvasMode();
        }

        /// <summary>Applies mm/pixel calibration to the current camera's CameraSlaveParam and all FAIs (per D-12).</summary>
        private void ApplyCalibrationResult(double mmPerPixel) {
            var selectedRow = dataGrid_faiResults.SelectedItem as FAIResultRow;
            if (selectedRow?.SourceFAI != null) {
                var shot = selectedRow.SourceFAI.Owner as ShotConfig;
                if (shot == null) {
                    CustomMessageBox.Show("샷 정보를 찾을 수 없습니다.", "캘리브레이션");
                    return;
                }

                // CameraSlaveParam is the shot itself (ShotConfig extends CameraSlaveParam)
                shot.PixelResolution = mmPerPixel;

                // Also update all FAIs under this shot for RoiDefinition compatibility
                foreach (FAIConfig fai in shot.FAIList) {
                    fai.PixelResolutionX = mmPerPixel;
                    fai.PixelResolutionY = mmPerPixel;
                }
            }
        }
    }
}
