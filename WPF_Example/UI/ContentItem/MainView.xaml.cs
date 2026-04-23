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

        //260408 hbk Drawing mode state (ROI 편집 + 캘리브레이션)
        //260423 hbk Phase 11 D-15 — CircleRoi 모드 추가
        private enum ECanvasMode { None, RectRoi, PolygonRoi, CircleRoi, Calibration }
        private ECanvasMode _canvasMode = ECanvasMode.None;
        private FAIConfig _editingFai;
        //260423 hbk Phase 11 D-17 — Circle ROI 편집 대상 Measurement
        private CircleDiameterMeasurement _editingCircleMeasurement;
        //260423 hbk Circle ROI 편집 대상 FAI 이름 (RoiDefinition.Id=FAIName 과 일치 유지)
        private string _editingCircleFaiName;
        private readonly List<System.Windows.Point> _polygonPoints = new List<System.Windows.Point>();
        private readonly List<System.Windows.Point> _calibrationPoints = new List<System.Windows.Point>();
        private double _lastPointerRow, _lastPointerCol; //260408 hbk 마지막 이미지 좌표 (polygon/calibration 클릭용)

        public MainView() {
            InitializeComponent();
            halconViewer.PointerInfoChanged += HalconViewer_PointerInfoChanged;
            //260423 hbk ROI 이동 완료 이벤트 구독
            halconViewer.RoiMoveCompleted += HalconViewer_RoiMoveCompleted;
            //260423 hbk ROI 삭제 요청 이벤트 구독 (ContextMenu)
            halconViewer.RoiDeleteRequested += HalconViewer_RoiDeleteRequested;
            //260423 hbk ROI 기하 변경(리사이즈/정점편집) 이벤트 구독
            halconViewer.RoiGeometryChanged += HalconViewer_RoiGeometryChanged;
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

        /// <summary>Binds DataGrid to the InspectionViewModel's MeasurementResults collection.</summary>
        //260417 hbk Phase 6 Plan 04: FAIResults → MeasurementResults 바인딩 (D-21)
        public void SetFAIResultSource(InspectionViewModel vm) {
            dataGrid_faiResults.SetBinding(
                System.Windows.Controls.DataGrid.ItemsSourceProperty,
                new System.Windows.Data.Binding("MeasurementResults") { Source = vm });
        }

        //260408 hbk FAI 선택 시 ROI 하이라이트 + 'ROI not set' 힌트
        //260417 hbk Phase 6 Plan 04: MeasurementResultRow 기준으로 마이그레이션 — FAIName으로 ROI 조회 (D-21)
        private void FAIResults_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            var selectedRow = dataGrid_faiResults.SelectedItem as MeasurementResultRow;
            if (selectedRow == null) {
                var allRois = GetCurrentFAIRois();
                if (allRois.Count > 0)
                    halconViewer.UpdateDisplayState(allRois, null, null, null);
                return;
            }

            var rois = GetCurrentFAIRois();
            string selectedRoiId = selectedRow.FAIName;
            halconViewer.UpdateDisplayState(rois, selectedRoiId, null, null);

            //260417 hbk Phase 6 Plan 04: 선택된 행의 FAIConfig를 트리에서 조회해 ROI hint 표시
            FAIConfig parentFai = FindFAIByName(selectedRow.FAIName);
            if (parentFai != null && (parentFai.ROI_Length1 <= 0 || parentFai.ROI_Length2 <= 0)) {
                label_message.Content = "ROI not set";
                label_message.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFAAAAAA"));
                label_message.Visibility = Visibility.Visible;
            }
        }

        //260408 hbk GetCurrentFAIRois 추가 (전체 FAI ROI 수집)
        //260417 hbk Phase 6 Plan 04: MeasurementResultRow.FAIName 기준 중복 제거 + FindFAIByName 사용 (D-21)
        /// <summary>Collects RoiDefinitions from all FAIs of the currently displayed shot.</summary>
        private List<RoiDefinition> GetCurrentFAIRois() {
            var result = new List<RoiDefinition>();
            var seen = new HashSet<string>();
            foreach (var item in dataGrid_faiResults.Items) {
                var row = item as MeasurementResultRow;
                if (row == null || string.IsNullOrEmpty(row.FAIName)) continue;
                if (!seen.Add(row.FAIName)) continue;
                FAIConfig fai = FindFAIByName(row.FAIName);
                if (fai == null) continue;
                var roi = fai.ToRoiDefinition();
                if (roi.IsTaught) result.Add(roi);
            }
            return result;
        }

        //260417 hbk Phase 6 Plan 04: 모든 시퀀스/Shot에서 FAIName으로 FAIConfig 조회 (D-21)
        private FAIConfig FindFAIByName(string faiName) {
            if (string.IsNullOrEmpty(faiName) || pSeq == null) return null;
            for (int i = 0; i < pSeq.Count; i++) {
                var seq = pSeq[i];
                if (seq == null) continue;
                for (int j = 0; j < seq.ActionCount; j++) {
                    var act = seq[j];
                    if (act?.Param is ShotConfig shot) {
                        foreach (FAIConfig fai in shot.FAIList) {
                            if (string.Equals(fai.FAIName, faiName, StringComparison.Ordinal)) return fai;
                        }
                    }
                }
            }
            return null;
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
                    RefreshFAIResultRows(); //260409 hbk Phase 3
                });
            }
        }

        //260409 hbk Phase 5: Shot별 Action 완료 시 실시간 FAI 결과 갱신 (D-12)
        public void DisplayActionContext(ActionContext context) {
            ExecuteOnUi(() => {
                RefreshFAIResultRows();
            });
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
                    RefreshFAIResultRows(); //260409 hbk Phase 3
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
            _lastPointerRow = e.Y; //260408 hbk
            _lastPointerCol = e.X; //260408 hbk
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
                    halconViewer.UpdateDisplayState(roiList, context.InspectionOverlays, context.DisplayMessages); //260409 hbk
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
                    halconViewer.UpdateDisplayState(roiList, context.InspectionOverlays, context.DisplayMessages); //260409 hbk
                    return true;
                }
                catch (Exception ex) {
                    Logging.PrintErrLog((int)ELogType.Error, ex.Message);
                }
            }

            halconViewer.UpdateDisplayState(roiList, context.InspectionOverlays, context.DisplayMessages); //260409 hbk
            return true;
        }

        private void MainView_Unloaded(object sender, RoutedEventArgs e) {
            halconViewer.PointerInfoChanged -= HalconViewer_PointerInfoChanged;
            //260423 hbk ROI 이동 이벤트 구독 해제
            halconViewer.RoiMoveCompleted -= HalconViewer_RoiMoveCompleted;
            //260423 hbk ROI 삭제 이벤트 구독 해제
            halconViewer.RoiDeleteRequested -= HalconViewer_RoiDeleteRequested;
            //260423 hbk ROI 기하 변경 이벤트 구독 해제
            halconViewer.RoiGeometryChanged -= HalconViewer_RoiGeometryChanged;
        }

        //260423 hbk ROI 기하 변경(리사이즈/정점편집) → FAI 모델 좌표/크기 반영
        private void HalconViewer_RoiGeometryChanged(object sender, RoiGeometryChangedArgs e) {
            if (e == null || string.IsNullOrEmpty(e.RoiId)) return;

            var fai = FindFAIByName(e.RoiId);
            if (fai == null) return;

            if (e.Shape == RoiShape.Circle) {
                foreach (var m in fai.Measurements) {
                    var circle = m as CircleDiameterMeasurement;
                    if (circle != null) {
                        circle.Circle_Row = e.CenterRow;
                        circle.Circle_Col = e.CenterCol;
                        circle.Circle_Radius = e.Radius;
                        break;
                    }
                }
            }
            else if (e.Shape == RoiShape.Polygon) {
                fai.PolygonPoints = e.PolygonPoints ?? "";
            }
            else {
                // Rect — bounding box로부터 center + half-length 재계산 (ROI_Phi=0 가정)
                double cRow = (e.Row1 + e.Row2) / 2.0;
                double cCol = (e.Column1 + e.Column2) / 2.0;
                double halfR = (e.Row2 - e.Row1) / 2.0;
                double halfC = (e.Column2 - e.Column1) / 2.0;
                fai.ROI_Row = cRow;
                fai.ROI_Col = cCol;
                fai.ROI_Phi = 0;
                fai.ROI_Length1 = halfR;
                fai.ROI_Length2 = halfC;
            }

            var rois = GetCurrentFAIRois();
            halconViewer.UpdateDisplayState(rois, e.RoiId, null, null);
        }

        //260423 hbk ROI 삭제 요청 → FAI의 ROI 필드 초기화 (FAI 자체는 유지)
        private void HalconViewer_RoiDeleteRequested(object sender, string roiId) {
            if (string.IsNullOrEmpty(roiId)) return;

            var fai = FindFAIByName(roiId);
            if (fai == null) return;

            // Rect ROI clear
            fai.ROI_Row = 0;
            fai.ROI_Col = 0;
            fai.ROI_Phi = 0;
            fai.ROI_Length1 = 0;
            fai.ROI_Length2 = 0;
            // Polygon ROI clear
            fai.PolygonPoints = "";
            // Circle ROI clear (CircleDiameterMeasurement.Circle_Radius = 0 → ToRoiDefinition에서 hasCircle=false)
            foreach (var m in fai.Measurements) {
                var circle = m as CircleDiameterMeasurement;
                if (circle != null) {
                    circle.Circle_Row = 0;
                    circle.Circle_Col = 0;
                    circle.Circle_Radius = 0;
                }
            }

            var rois = GetCurrentFAIRois();
            halconViewer.UpdateDisplayState(rois, null, null, null);
        }

        //260423 hbk ROI 이동 완료 → FAI 모델 좌표 반영
        private void HalconViewer_RoiMoveCompleted(object sender, RoiMoveCompletedArgs e) {
            if (e == null || string.IsNullOrEmpty(e.RoiId)) return;

            var fai = FindFAIByName(e.RoiId);
            if (fai == null) return;

            bool handledCircle = false;
            foreach (var m in fai.Measurements) {
                var circle = m as CircleDiameterMeasurement;
                if (circle != null) {
                    circle.Circle_Row += e.DeltaRow;
                    circle.Circle_Col += e.DeltaCol;
                    handledCircle = true;
                    break;
                }
            }
            if (!handledCircle) {
                fai.ROI_Row += e.DeltaRow;
                fai.ROI_Col += e.DeltaCol;
            }

            var rois = GetCurrentFAIRois();
            halconViewer.UpdateDisplayState(rois, e.RoiId, null, null);
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

        //260409 hbk Phase 3: refresh FAI result rows after measurement
        //260417 hbk Phase 6 Plan 04: MeasurementResultRow로 마이그레이션 (D-21)
        private void RefreshFAIResultRows() {
            if (dataGrid_faiResults == null || dataGrid_faiResults.ItemsSource == null) return;
            foreach (var item in dataGrid_faiResults.ItemsSource) {
                var row = item as MeasurementResultRow;
                if (row != null) row.Refresh();
            }
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

        //260408 hbk Escape 키 → 드로잉 모드 취소
        private void MainView_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e) {
            if (e.Key == System.Windows.Input.Key.Escape && _canvasMode != ECanvasMode.None) {
                ExitCanvasMode();
                e.Handled = true;
            }
        }

        //260408 hbk 드로잉 모드 종료 + 상태 초기화
        private void ExitCanvasMode() {
            // Unsubscribe Halcon 브릿지 이벤트 (safe to call even if not subscribed)
            halconViewer.ImageLeftClicked -= HalconViewer_PolygonMouseDown;
            halconViewer.ImageRightClicked -= HalconViewer_PolygonRightClick;
            halconViewer.RectDrawingCompleted -= HalconViewer_RectDrawingCompleted;
            //260423 hbk Phase 11 — Circle ROI 모드 정리
            halconViewer.CircleDrawingCompleted -= HalconViewer_CircleDrawingCompleted;

            _canvasMode = ECanvasMode.None;
            _editingFai = null;
            //260423 hbk Phase 11 — Circle ROI 편집 대상 해제
            _editingCircleMeasurement = null;
            _editingCircleFaiName = null;
            btn_rectRoi.IsChecked = false;
            btn_polygonRoi.IsChecked = false;
            //260423 hbk Phase 11 — Circle ROI 토글 해제
            btn_circleRoi.IsChecked = false;
            label_drawHint.Visibility = Visibility.Collapsed;
            label_pointCount.Visibility = Visibility.Collapsed;
            halconViewer.ClearPolygonDraft();
            _polygonPoints.Clear();

            // Calibration cleanup
            halconViewer.ImageLeftClicked -= HalconViewer_CalibrationMouseDown;
            halconViewer.ClearCalibrationOverlay(); //260408 hbk
            btn_calibrate.Content = "Calibrate";
            _calibrationPoints.Clear();
        }

        //260408 hbk Rect ROI 드로잉 모드
        //260417 hbk Phase 6 Plan 04: MeasurementResultRow → FAIName으로 FAIConfig 조회 (D-21)
        private void RectRoiButton_Click(object sender, RoutedEventArgs e) {
            if (btn_rectRoi.IsChecked == true) {
                ExitCanvasMode();
                _canvasMode = ECanvasMode.RectRoi;
                btn_rectRoi.IsChecked = true;

                var selectedRow = dataGrid_faiResults.SelectedItem as MeasurementResultRow;
                FAIConfig faiToEdit = selectedRow != null ? FindFAIByName(selectedRow.FAIName) : null;
                if (faiToEdit == null) {
                    CustomMessageBox.Show("FAI를 먼저 선택하세요.", "Rect ROI");
                    ExitCanvasMode();
                    return;
                }
                _editingFai = faiToEdit;

                label_drawHint.Content = "드래그하여 ROI를 설정하세요";
                label_drawHint.Visibility = Visibility.Visible;
                halconViewer.RectDrawingCompleted += HalconViewer_RectDrawingCompleted; //260408 hbk 마우스업 자동커밋
                halconViewer.StartRectangleDrawing();
            }
            else {
                CommitRectRoi();
            }
        }

        //260408 hbk 마우스 업 시 Rect ROI 자동 커밋
        private void HalconViewer_RectDrawingCompleted(object sender, EventArgs e) {
            halconViewer.RectDrawingCompleted -= HalconViewer_RectDrawingCompleted;
            CommitRectRoi();
        }

        private void CommitRectRoi() {
            if (_canvasMode != ECanvasMode.RectRoi || _editingFai == null) {
                ExitCanvasMode();
                return;
            }

            var roi = halconViewer.CommitActiveRectangle();
            if (roi != null) {
                // Convert RoiDefinition bounding box back to center+half-lengths (per D-05: phi=0 for new ROI)
                double centerRow = (roi.Row1 + roi.Row2) / 2.0;
                double centerCol = (roi.Column1 + roi.Column2) / 2.0;
                double halfHeight = (roi.Row2 - roi.Row1) / 2.0;
                double halfWidth = (roi.Column2 - roi.Column1) / 2.0;

                _editingFai.ROI_Row = centerRow;
                _editingFai.ROI_Col = centerCol;
                _editingFai.ROI_Phi = 0.0;
                _editingFai.ROI_Length1 = halfHeight;
                _editingFai.ROI_Length2 = halfWidth;

                //260417 hbk Measurement.ROI_* 동기화 블록 제거 — EdgePairDistanceMeasurement가
                // Owner(FAIConfig).ROI_*를 직접 참조하도록 변경되어 중복 저장이 사라짐.

                // Refresh canvas to show new ROI
                var rois = GetCurrentFAIRois();
                halconViewer.UpdateDisplayState(rois, _editingFai.FAIName, null, null);
            }
            ExitCanvasMode();
        }

        //260423 hbk Phase 11 D-14/D-15 — Circle ROI 드로잉 진입/취소
        private void CircleRoiButton_Click(object sender, RoutedEventArgs e) {
            if (btn_circleRoi.IsChecked == true) {
                ExitCanvasMode();
                _canvasMode = ECanvasMode.CircleRoi;
                btn_circleRoi.IsChecked = true;

                //260423 hbk Phase 11 D-17/D-18 — 선택된 FAI에서 CircleDiameterMeasurement 해석
                CircleDiameterMeasurement target = FindSelectedCircleMeasurement();
                if (target == null) {
                    CustomMessageBox.Show("Circle ROI", "CircleDiameterMeasurement을 포함한 FAI를 선택하세요.");
                    ExitCanvasMode();
                    return;
                }
                _editingCircleMeasurement = target;
                //260423 hbk Commit 시 selection id 를 FAIName 으로 맞추기 위해 캡처
                var selRowForCircle = dataGrid_faiResults.SelectedItem as MeasurementResultRow;
                _editingCircleFaiName = selRowForCircle?.FAIName;

                label_drawHint.Content = "중심을 클릭 후 드래그하여 반지름을 지정하세요";
                label_drawHint.Foreground = new SolidColorBrush(
                    (Color)ColorConverter.ConvertFromString("#FFAAAAAA"));
                label_drawHint.Visibility = Visibility.Visible;

                halconViewer.CircleDrawingCompleted += HalconViewer_CircleDrawingCompleted;
                halconViewer.StartCircleDrawing();
            }
            else {
                // Manual toggle off = cancel draft
                halconViewer.CommitActiveCircle();
                ExitCanvasMode();
            }
        }

        //260423 hbk Phase 11 — Circle 드래그 완료 수신 → CommitCircleRoi로 위임
        private void HalconViewer_CircleDrawingCompleted(object sender, CircleDrawCompletedArgs e) {
            CommitCircleRoi(e.CenterRow, e.CenterCol, e.Radius);
        }

        //260423 hbk Measurement 인스턴스를 소유한 FAIConfig 의 FAIName 역탐색 (fallback)
        private string FindFaiNameContainingMeasurement(MeasurementBase measurement) {
            if (measurement == null || pSeq == null) return null;
            for (int i = 0; i < pSeq.Count; i++) {
                var seq = pSeq[i];
                if (seq == null) continue;
                for (int j = 0; j < seq.ActionCount; j++) {
                    var act = seq[j];
                    if (act?.Param is ShotConfig shot) {
                        foreach (FAIConfig fai in shot.FAIList) {
                            foreach (var m in fai.Measurements) {
                                if (ReferenceEquals(m, measurement)) return fai.FAIName;
                            }
                        }
                    }
                }
            }
            return null;
        }

        //260423 hbk Phase 11 D-17/D-18 — 선택된 FAI에서 CircleDiameterMeasurement 해석
        private CircleDiameterMeasurement FindSelectedCircleMeasurement() {
            var selectedRow = dataGrid_faiResults.SelectedItem as MeasurementResultRow;
            if (selectedRow != null) {
                FAIConfig fai = FindFAIByName(selectedRow.FAIName);
                if (fai != null) {
                    foreach (var m in fai.Measurements) {
                        var circle = m as CircleDiameterMeasurement;
                        if (circle != null) return circle;
                    }
                }
            }
            return null;
        }

        //260423 hbk Phase 11 D-17 — Circle 드래그 결과를 Measurement에 기록
        private void CommitCircleRoi(double centerRow, double centerCol, double radius) {
            if (_canvasMode != ECanvasMode.CircleRoi || _editingCircleMeasurement == null || radius <= 0) {
                ExitCanvasMode();
                return;
            }

            // D-17: write to the Measurement's own fields (authoritative for Halcon call)
            _editingCircleMeasurement.Circle_Row = centerRow;
            _editingCircleMeasurement.Circle_Col = centerCol;
            _editingCircleMeasurement.Circle_Radius = radius;

            // Refresh canvas using GetCurrentFAIRois — FAIConfig.ToRoiDefinition() Circle branch (Task 3)
            // emits Shape=Circle so HalconDisplayService (Plan 01) renders committed circle.
            var rois = GetCurrentFAIRois();
            //260423 hbk FIX: RoiDefinition.Id = FAIName (ToRoiDefinition) 과 일치시켜야
            // _selectedRoiId 매치 → Edit/Delete 메뉴 활성화 + 리사이즈 핸들 렌더 동작
            string selId = _editingCircleFaiName;
            if (string.IsNullOrEmpty(selId)) {
                // Fallback: _editingCircleMeasurement 를 포함한 FAI 를 역탐색
                selId = FindFaiNameContainingMeasurement(_editingCircleMeasurement);
            }
            halconViewer.UpdateDisplayState(rois, selId, null, null);

            ExitCanvasMode();
        }

        //260408 hbk Polygon ROI 드로잉 모드
        private void PolygonRoiButton_Click(object sender, RoutedEventArgs e) {
            if (btn_polygonRoi.IsChecked == true) {
                ExitCanvasMode();
                _canvasMode = ECanvasMode.PolygonRoi;
                btn_polygonRoi.IsChecked = true;

                //260417 hbk Phase 6 Plan 04: MeasurementResultRow → FAIName으로 FAIConfig 조회 (D-21)
                var selectedRow = dataGrid_faiResults.SelectedItem as MeasurementResultRow;
                FAIConfig faiToEdit = selectedRow != null ? FindFAIByName(selectedRow.FAIName) : null;
                if (faiToEdit == null) {
                    CustomMessageBox.Show("FAI를 먼저 선택하세요.", "Polygon ROI");
                    ExitCanvasMode();
                    return;
                }
                _editingFai = faiToEdit;
                _polygonPoints.Clear();

                label_drawHint.Content = "점을 클릭, 우클릭으로 완성 (최소 3점)";
                label_drawHint.Visibility = Visibility.Visible;
                label_pointCount.Content = "0 / 20 pts";
                label_pointCount.Visibility = Visibility.Visible;

                halconViewer.ImageLeftClicked += HalconViewer_PolygonMouseDown; //260408 hbk Halcon HMouseDown 브릿지
                halconViewer.ImageRightClicked += HalconViewer_PolygonRightClick;
            }
            else {
                ExitCanvasMode();
            }
        }

        //260408 hbk Halcon HMouseDown 브릿지 이벤트 핸들러 (WPF MouseButtonEventArgs → MainViewerPointerChangedEventArgs)
        private void HalconViewer_PolygonMouseDown(object sender, MainViewerPointerChangedEventArgs e) {
            if (_canvasMode != ECanvasMode.PolygonRoi) return;

            if (_polygonPoints.Count >= 20) {
                label_pointCount.Content = "20 / 20 pts MAX";
                return;
            }

            var imagePoint = new System.Windows.Point(e.X, e.Y);
            _polygonPoints.Add(imagePoint);
            label_pointCount.Content = string.Format("{0} / 20 pts", _polygonPoints.Count);

            halconViewer.SetPolygonDraft(_polygonPoints, "red");
        }

        private void HalconViewer_PolygonRightClick(object sender, EventArgs e) {
            if (_canvasMode != ECanvasMode.PolygonRoi) return;

            if (_polygonPoints.Count >= 3) {
                CompletePolygon();
            }
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

        //260408 hbk 2점 캘리브레이션 플로우
        private void CalibrateButton_Click(object sender, RoutedEventArgs e) {
            ExitCanvasMode();
            _canvasMode = ECanvasMode.Calibration;
            _calibrationPoints.Clear();

            btn_calibrate.Content = "Pick Point 1";
            label_drawHint.Content = "캔버스에서 첫 번째 점을 클릭하세요";
            label_drawHint.Visibility = Visibility.Visible;

            halconViewer.ImageLeftClicked += HalconViewer_CalibrationMouseDown; //260408 hbk Halcon 브릿지
        }

        //260408 hbk Halcon HMouseDown 브릿지 이벤트 핸들러
        private void HalconViewer_CalibrationMouseDown(object sender, MainViewerPointerChangedEventArgs e) {
            if (_canvasMode != ECanvasMode.Calibration) return;

            var pos = new System.Windows.Point(e.X, e.Y);
            _calibrationPoints.Add(pos);
            halconViewer.SetCalibrationOverlay(_calibrationPoints); //260408 hbk 십자+라인 오버레이 업데이트

            if (_calibrationPoints.Count == 1) {
                btn_calibrate.Content = "Pick Point 2";
                label_drawHint.Content = "캔버스에서 두 번째 점을 클릭하세요";
            }
            else if (_calibrationPoints.Count == 2) {
                halconViewer.ImageLeftClicked -= HalconViewer_CalibrationMouseDown;
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
        //260417 hbk Phase 6 Plan 04: MeasurementResultRow → FindFAIByName (D-21)
        private void ApplyCalibrationResult(double mmPerPixel) {
            var selectedRow = dataGrid_faiResults.SelectedItem as MeasurementResultRow;
            FAIConfig anchorFai = selectedRow != null ? FindFAIByName(selectedRow.FAIName) : null;
            if (anchorFai != null) {
                var shot = anchorFai.Owner as ShotConfig;
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
