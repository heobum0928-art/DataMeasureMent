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
        //260424 hbk Phase 12 D-01 — TeachDatum 모드 추가 (3-way algorithm switch)
        private enum ECanvasMode { None, RectRoi, PolygonRoi, CircleRoi, TeachDatum, Calibration }
        private ECanvasMode _canvasMode = ECanvasMode.None;
        private FAIConfig _editingFai;
        //260423 hbk Phase 11 D-17 — Circle ROI 편집 대상 Measurement
        private CircleDiameterMeasurement _editingCircleMeasurement;
        //260423 hbk Circle ROI 편집 대상 FAI 이름 (RoiDefinition.Id=FAIName 과 일치 유지)
        private string _editingCircleFaiName;
        //260424 hbk Phase 12 D-03 — Datum 티칭 단계 (알고리즘별 switch 로 전이 결정)
        //  Phase 13 에서 DatumAlgorithmBase.GetROISteps() 가변 배열로 재설계 예정 — switch 는 MainView 내 private 유지.
        private enum EDatumTeachStep { Line1, Line2, Circle, Vertical, HorizontalA, HorizontalB, Done }
        private EDatumTeachStep _datumTeachStep = EDatumTeachStep.Line1; //260424 hbk Phase 12 D-03
        private DatumConfig _editingDatum; //260424 hbk Phase 12 — 현재 티칭 중 Datum
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

        //260423 hbk ContextMenu Delete 핸들러
        //260425 hbk Phase 13 D-A — Datum RoiId prefix 면 Datum 분기로 early return (FAI lookup 전에)
        private void HalconViewer_RoiDeleteRequested(object sender, string roiId) {
            if (string.IsNullOrEmpty(roiId)) return;

            //260425 hbk Phase 13 D-A — Datum 분기 우선
            if (roiId.StartsWith("Datum.")) {
                DatumConfig datum;
                if (IsCurrentNodeDatum(out datum)) {
                    ClearDatumRoiFields(datum, roiId);
                    try { datum.RaisePropertyChanged(string.Empty); } catch { }
                    mParentWindow?.inspectionList?.RefreshParamEditor();
                    halconViewer.SetDatumOverlay(datum, true);
                    PublishDatumRoiCandidates(datum); //260425 hbk Phase 13 D-A — 잔존 ROI 만 후보로 남도록 갱신
                }
                return;
            }

            //260423 hbk 기존 FAI 경로 (untouched)
            var fai = FindFAIByName(roiId);
            if (fai == null) return;

            fai.ROI_Row = 0;
            fai.ROI_Col = 0;
            fai.ROI_Phi = 0;
            fai.ROI_Length1 = 0;
            fai.ROI_Length2 = 0;
            fai.PolygonPoints = "";
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
        //260425 hbk Phase 13 D-01..D-04 — Datum 컨텍스트(SelectedParam=DatumConfig + RoiId='Datum.*') 에서는 Datum 분기로 early return
        private void HalconViewer_RoiMoveCompleted(object sender, RoiMoveCompletedArgs e) {
            if (e == null || string.IsNullOrEmpty(e.RoiId)) return;

            //260425 hbk Phase 13 D-01..D-04 — Datum 분기 우선 (FAI lookup 전에)
            DatumConfig datum;
            if (e.RoiId.StartsWith("Datum.") && IsCurrentNodeDatum(out datum)) {
                HandleDatumRoiMove(datum, e);
                return;
            }

            //260423 hbk 기존 FAI 경로 (untouched from Phase 11 Quick 260423-o53)
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

        //260425 hbk Phase 13 D-01 — 현재 선택 노드가 Datum 인지 판정
        private bool IsCurrentNodeDatum(out DatumConfig datum) {
            datum = mParentWindow?.inspectionList?.SelectedParam as DatumConfig;
            return datum != null;
        }

        //260425 hbk Phase 13 D-01..D-04 — Datum ROI 이동 후 처리 (delta + 이중 신호 + 자동 재티칭 + 후보 publish)
        private void HandleDatumRoiMove(DatumConfig datum, RoiMoveCompletedArgs e) {
            ApplyDatumRoiDelta(datum, e);
            try { datum.RaisePropertyChanged(string.Empty); } catch { }
            mParentWindow?.inspectionList?.RefreshParamEditor();
            halconViewer.SetDatumOverlay(datum, true);
            InvokeTryTeachDatumForEdit(datum);
            PublishDatumRoiCandidates(datum);
        }

        //260425 hbk Phase 13 D-02 — RoiId prefix 별 DatumConfig 필드 매핑 (delta 누적)
        private void ApplyDatumRoiDelta(DatumConfig datum, RoiMoveCompletedArgs e) {
            if (datum == null || e == null || string.IsNullOrEmpty(e.RoiId)) return;
            switch (e.RoiId) {
                case "Datum.Line1":
                    datum.Line1_Row += e.DeltaRow; datum.Line1_Col += e.DeltaCol; break;
                case "Datum.Line2":
                    datum.Line2_Row += e.DeltaRow; datum.Line2_Col += e.DeltaCol; break;
                case "Datum.Circle":
                    datum.CircleROI_Row += e.DeltaRow; datum.CircleROI_Col += e.DeltaCol; break;
                case "Datum.HorizontalA":
                    datum.Horizontal_A_Row += e.DeltaRow; datum.Horizontal_A_Col += e.DeltaCol; break;
                case "Datum.HorizontalB":
                    datum.Horizontal_B_Row += e.DeltaRow; datum.Horizontal_B_Col += e.DeltaCol; break;
                default:
                    Logging.PrintLog((int)ELogType.Trace, "Datum ROI move: unknown RoiId=" + e.RoiId);
                    break;
            }
        }

        //260425 hbk Phase 13 D-A — RoiId prefix 별 ROI 필드 0 reset (Length1/Length2/Radius) + IsConfigured/LastTeachSucceeded false
        //  RenderDatumOverlay 의 그리기 가드(if Length1>0 && Length2>0)에 걸려 시각적으로 사라짐.
        private void ClearDatumRoiFields(DatumConfig datum, string roiId) {
            if (datum == null || string.IsNullOrEmpty(roiId)) return;
            switch (roiId) {
                case "Datum.Line1":
                    datum.Line1_Row = 0; datum.Line1_Col = 0; datum.Line1_Phi = 0;
                    datum.Line1_Length1 = 0; datum.Line1_Length2 = 0;
                    break;
                case "Datum.Line2":
                    datum.Line2_Row = 0; datum.Line2_Col = 0; datum.Line2_Phi = 0;
                    datum.Line2_Length1 = 0; datum.Line2_Length2 = 0;
                    break;
                case "Datum.Circle":
                    datum.CircleROI_Row = 0; datum.CircleROI_Col = 0; datum.CircleROI_Radius = 0;
                    break;
                case "Datum.HorizontalA":
                    datum.Horizontal_A_Row = 0; datum.Horizontal_A_Col = 0; datum.Horizontal_A_Phi = 0;
                    datum.Horizontal_A_Length1 = 0; datum.Horizontal_A_Length2 = 0;
                    break;
                case "Datum.HorizontalB":
                    datum.Horizontal_B_Row = 0; datum.Horizontal_B_Col = 0; datum.Horizontal_B_Phi = 0;
                    datum.Horizontal_B_Length1 = 0; datum.Horizontal_B_Length2 = 0;
                    break;
                default:
                    Logging.PrintLog((int)ELogType.Trace, "Datum ROI delete: unknown RoiId=" + roiId);
                    return;
            }
            //260425 hbk Phase 13 D-A — 어느 ROI 든 삭제되면 Datum 자체가 불완전 → 검증 disable
            datum.IsConfigured = false;
            datum.LastTeachSucceeded = false;
        }

        //260425 hbk Phase 13 D-03 — Edit 세션 전용 자동 재티칭 (_editingDatum 건드리지 않음)
        private void InvokeTryTeachDatumForEdit(DatumConfig datum) {
            if (datum == null) return;
            HImage img = halconViewer.CurrentImage;
            if (img == null) return;
            var svc = new ReringProject.Halcon.Algorithms.DatumFindingService();
            string error;
            bool ok = svc.TryTeachDatum(img, datum, out error);
            if (ok) {
                label_drawHint.Content = "Datum ROI 이동 — 재티칭 OK";
                label_drawHint.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF4ADE80"));
                label_drawHint.Visibility = Visibility.Visible;
            }
            else {
                label_drawHint.Content = "Datum ROI 이동 — 재티칭 실패: " + (error ?? "unknown");
                label_drawHint.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFF87171"));
                label_drawHint.Visibility = Visibility.Visible;
            }
            halconViewer.SetDatumOverlay(datum, true);
        }

        //260425 hbk Phase 13 D-02 — DatumConfig → RoiDefinition 리스트 → halconViewer.SetDatumRoiCandidates publish
        //260426 hbk Phase 13 D-A1 — InspectionListView 가 selection 시 호출하도록 public 승격
        public void PublishDatumRoiCandidates(DatumConfig datum) {
            if (datum == null) { halconViewer.ClearDatumRoiCandidates(); return; }
            var list = new List<ReringProject.Halcon.Models.RoiDefinition>();
            switch (datum.AlgorithmTypeEnum) {
                case EDatumAlgorithm.TwoLineIntersect:
                    if (datum.Line1_Length1 > 0 && datum.Line1_Length2 > 0)
                        list.Add(BuildDatumRectCandidate("Datum.Line1", datum.Line1_Row, datum.Line1_Col, datum.Line1_Length1, datum.Line1_Length2));
                    if (datum.Line2_Length1 > 0 && datum.Line2_Length2 > 0)
                        list.Add(BuildDatumRectCandidate("Datum.Line2", datum.Line2_Row, datum.Line2_Col, datum.Line2_Length1, datum.Line2_Length2));
                    break;
                case EDatumAlgorithm.CircleTwoHorizontal:
                    if (datum.CircleROI_Radius > 0)
                        list.Add(BuildDatumCircleCandidate("Datum.Circle", datum.CircleROI_Row, datum.CircleROI_Col, datum.CircleROI_Radius));
                    if (datum.Horizontal_A_Length1 > 0 && datum.Horizontal_A_Length2 > 0)
                        list.Add(BuildDatumRectCandidate("Datum.HorizontalA", datum.Horizontal_A_Row, datum.Horizontal_A_Col, datum.Horizontal_A_Length1, datum.Horizontal_A_Length2));
                    if (datum.Horizontal_B_Length1 > 0 && datum.Horizontal_B_Length2 > 0)
                        list.Add(BuildDatumRectCandidate("Datum.HorizontalB", datum.Horizontal_B_Row, datum.Horizontal_B_Col, datum.Horizontal_B_Length1, datum.Horizontal_B_Length2));
                    break;
                case EDatumAlgorithm.VerticalTwoHorizontal:
                    if (datum.Line1_Length1 > 0 && datum.Line1_Length2 > 0)
                        list.Add(BuildDatumRectCandidate("Datum.Line1", datum.Line1_Row, datum.Line1_Col, datum.Line1_Length1, datum.Line1_Length2));
                    if (datum.Horizontal_A_Length1 > 0 && datum.Horizontal_A_Length2 > 0)
                        list.Add(BuildDatumRectCandidate("Datum.HorizontalA", datum.Horizontal_A_Row, datum.Horizontal_A_Col, datum.Horizontal_A_Length1, datum.Horizontal_A_Length2));
                    if (datum.Horizontal_B_Length1 > 0 && datum.Horizontal_B_Length2 > 0)
                        list.Add(BuildDatumRectCandidate("Datum.HorizontalB", datum.Horizontal_B_Row, datum.Horizontal_B_Col, datum.Horizontal_B_Length1, datum.Horizontal_B_Length2));
                    break;
            }
            halconViewer.SetDatumRoiCandidates(list);
        }

        //260425 hbk Phase 13 D-02 — Rectangle2 (centerRow, centerCol, phi=0, halfH, halfW) → bbox
        private static ReringProject.Halcon.Models.RoiDefinition BuildDatumRectCandidate(string id, double centerRow, double centerCol, double halfH, double halfW) {
            return new ReringProject.Halcon.Models.RoiDefinition {
                Id = id, Name = id,
                Shape = ReringProject.Halcon.Models.RoiShape.Rect,
                Row1 = centerRow - halfH, Row2 = centerRow + halfH,
                Column1 = centerCol - halfW, Column2 = centerCol + halfW,
                IsTaught = true
            };
        }

        //260425 hbk Phase 13 D-02 — Circle → RoiDefinition
        private static ReringProject.Halcon.Models.RoiDefinition BuildDatumCircleCandidate(string id, double centerRow, double centerCol, double radius) {
            return new ReringProject.Halcon.Models.RoiDefinition {
                Id = id, Name = id,
                Shape = ReringProject.Halcon.Models.RoiShape.Circle,
                CenterRow = centerRow, CenterCol = centerCol, Radius = radius,
                IsTaught = true
            };
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
            //260424 hbk Phase 12 — Datum 티칭 핸들러 unsubscribe (Double-subscribe 방지)
            halconViewer.RectDrawingCompleted   -= HalconViewer_DatumRectCompleted;
            halconViewer.CircleDrawingCompleted -= HalconViewer_DatumCircleCompleted;

            _canvasMode = ECanvasMode.None;
            _editingFai = null;
            //260423 hbk Phase 11 — Circle ROI 편집 대상 해제
            _editingCircleMeasurement = null;
            _editingCircleFaiName = null;
            _editingDatum = null; //260424 hbk Phase 12 — Datum 티칭 편집 대상 해제
            btn_rectRoi.IsChecked = false;
            btn_polygonRoi.IsChecked = false;
            //260423 hbk Phase 11 — Circle ROI 토글 해제
            btn_circleRoi.IsChecked = false;
            btn_teachDatum.IsChecked = false; //260424 hbk Phase 12 — Datum 티칭 토글 해제
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

        //260424 hbk Phase 12 D-01/D-03/D-04 — Datum 티칭 토글 진입/취소
        private void TeachDatumButton_Click(object sender, RoutedEventArgs e) {
            if (btn_teachDatum.IsChecked == true) {
                ExitCanvasMode();
                _canvasMode = ECanvasMode.TeachDatum;
                btn_teachDatum.IsChecked = true;

                //260424 hbk Phase 12 — InspectionListView.SelectedParam 으로 DatumConfig 해결 (btn_teachDatum 활성화 조건)
                var datum = mParentWindow?.inspectionList?.SelectedParam as DatumConfig; //260424 hbk Phase 12 — MainWindow.xaml:80 x:Name="inspectionList"
                if (datum == null) {
                    CustomMessageBox.Show("Datum 노드를 먼저 선택하세요.", "Teach Datum");
                    ExitCanvasMode();
                    return;
                }
                _editingDatum = datum;
                //260425 hbk Phase 13 D-01..D-04 — teach 진입 시점에 후보 publish (이후 edit/delete 가능)
                PublishDatumRoiCandidates(datum);

                //260424 hbk Phase 12 D-03 — 알고리즘별 첫 단계 결정 후 StartDatumTeachStep
                _datumTeachStep = GetFirstStep(datum.AlgorithmTypeEnum);
                StartDatumTeachStep(_datumTeachStep);
            }
            else {
                //260424 hbk Phase 12 — 수동 해제 = 취소
                ExitCanvasMode();
            }
        }

        //260424 hbk Phase 12 D-03 — 알고리즘별 첫 ROI 단계
        private EDatumTeachStep GetFirstStep(EDatumAlgorithm algorithm) {
            switch (algorithm) {
                case EDatumAlgorithm.CircleTwoHorizontal:   return EDatumTeachStep.Circle;
                case EDatumAlgorithm.VerticalTwoHorizontal: return EDatumTeachStep.Vertical;
                case EDatumAlgorithm.TwoLineIntersect:
                default:                                     return EDatumTeachStep.Line1;
            }
        }

        //260424 hbk Phase 12 D-03 — 현재 step 다음 step 결정
        private EDatumTeachStep GetNextStep(EDatumAlgorithm algorithm, EDatumTeachStep current) {
            switch (algorithm) {
                case EDatumAlgorithm.TwoLineIntersect:
                    if (current == EDatumTeachStep.Line1) return EDatumTeachStep.Line2;
                    return EDatumTeachStep.Done;
                case EDatumAlgorithm.CircleTwoHorizontal:
                    if (current == EDatumTeachStep.Circle)      return EDatumTeachStep.HorizontalA;
                    if (current == EDatumTeachStep.HorizontalA) return EDatumTeachStep.HorizontalB;
                    return EDatumTeachStep.Done;
                case EDatumAlgorithm.VerticalTwoHorizontal:
                    if (current == EDatumTeachStep.Vertical)    return EDatumTeachStep.HorizontalA;
                    if (current == EDatumTeachStep.HorizontalA) return EDatumTeachStep.HorizontalB;
                    return EDatumTeachStep.Done;
                default: return EDatumTeachStep.Done;
            }
        }

        //260424 hbk Phase 12 — step 시작 (드로잉 이벤트 구독 + label_drawHint + Start*Drawing)
        private void StartDatumTeachStep(EDatumTeachStep step) {
            // Unsubscribe any previous event to avoid double-fire //260424 hbk Phase 12
            halconViewer.RectDrawingCompleted   -= HalconViewer_DatumRectCompleted;
            halconViewer.CircleDrawingCompleted -= HalconViewer_DatumCircleCompleted;

            switch (step) {
                case EDatumTeachStep.Line1:
                    label_drawHint.Content = "Step 1/2: Line1 ROI를 드래그하세요"; //260424 hbk Phase 12
                    label_drawHint.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFAAAAAA")); //260424 hbk Phase 12 info grey
                    label_drawHint.Visibility = Visibility.Visible;
                    halconViewer.RectDrawingCompleted += HalconViewer_DatumRectCompleted;
                    halconViewer.StartRectangleDrawing();
                    break;
                case EDatumTeachStep.Line2:
                    label_drawHint.Content = "Step 2/2: Line2 ROI를 드래그하세요"; //260424 hbk Phase 12
                    label_drawHint.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFAAAAAA")); //260424 hbk Phase 12
                    label_drawHint.Visibility = Visibility.Visible;
                    halconViewer.RectDrawingCompleted += HalconViewer_DatumRectCompleted;
                    halconViewer.StartRectangleDrawing();
                    break;
                case EDatumTeachStep.Vertical:
                    label_drawHint.Content = "Step 1/3: 수직 ROI를 드래그하세요"; //260424 hbk Phase 12
                    label_drawHint.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFAAAAAA")); //260424 hbk Phase 12
                    label_drawHint.Visibility = Visibility.Visible;
                    halconViewer.RectDrawingCompleted += HalconViewer_DatumRectCompleted;
                    halconViewer.StartRectangleDrawing();
                    break;
                case EDatumTeachStep.HorizontalA:
                    label_drawHint.Content = "Step 2/3: 수평 A ROI를 드래그하세요"; //260424 hbk Phase 12
                    label_drawHint.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFAAAAAA")); //260424 hbk Phase 12
                    label_drawHint.Visibility = Visibility.Visible;
                    halconViewer.RectDrawingCompleted += HalconViewer_DatumRectCompleted;
                    halconViewer.StartRectangleDrawing();
                    break;
                case EDatumTeachStep.HorizontalB:
                    label_drawHint.Content = "Step 3/3: 수평 B ROI를 드래그하세요"; //260424 hbk Phase 12
                    label_drawHint.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFAAAAAA")); //260424 hbk Phase 12
                    label_drawHint.Visibility = Visibility.Visible;
                    halconViewer.RectDrawingCompleted += HalconViewer_DatumRectCompleted;
                    halconViewer.StartRectangleDrawing();
                    break;
                case EDatumTeachStep.Circle:
                    label_drawHint.Content = "Step 1/3: Circle 검색 영역 중심을 클릭 후 드래그하여 반지름을 지정하세요"; //260424 hbk Phase 12
                    label_drawHint.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFAAAAAA")); //260424 hbk Phase 12
                    label_drawHint.Visibility = Visibility.Visible;
                    halconViewer.CircleDrawingCompleted += HalconViewer_DatumCircleCompleted;
                    halconViewer.StartCircleDrawing();
                    break;
                case EDatumTeachStep.Done:
                    InvokeTryTeachDatum();
                    break;
            }
        }

        //260426 hbk Phase 13 D-PRP-LENFIX — Rect 완료 (Line1/Line2/Vertical/HorizontalA/HorizontalB 공통)
        private void HalconViewer_DatumRectCompleted(object sender, EventArgs e) {
            halconViewer.RectDrawingCompleted -= HalconViewer_DatumRectCompleted;
            var roi = halconViewer.CommitActiveRectangle();
            if (roi == null || _editingDatum == null) { ExitCanvasMode(); return; }

            //260426 hbk Phase 13 D-PRP-LENFIX — RoiDefinition bbox → Rectangle2 (center, phi=0, halfW=Length1, halfH=Length2) 정정
            //  Halcon gen_measure_rectangle2(Row,Col,Phi,Length1,Length2): Phi=0 기준 Length1=X축 절반(halfW), Length2=Y축 절반(halfH).
            //  Phase 12 의 (Length1=halfH, Length2=halfW) 매핑은 정반대 → 측정 사각형 90° 회전 → MeasurePos 가 의도한 에지를 가로지르지 못함.
            double centerRow = (roi.Row1 + roi.Row2) / 2.0;
            double centerCol = (roi.Column1 + roi.Column2) / 2.0;
            double halfH     = (roi.Row2 - roi.Row1) / 2.0;
            double halfW     = (roi.Column2 - roi.Column1) / 2.0;

            //260426 hbk Phase 13 D-PRP-LENFIX — step 별 DatumConfig 필드 기록 (Length1=halfW, Length2=halfH 정정)
            switch (_datumTeachStep) {
                case EDatumTeachStep.Line1:
                case EDatumTeachStep.Vertical:  //260424 hbk Phase 12 D-07 — Line1 재사용
                    _editingDatum.Line1_Row     = centerRow;
                    _editingDatum.Line1_Col     = centerCol;
                    _editingDatum.Line1_Phi     = 0.0;
                    _editingDatum.Line1_Length1 = halfW; //260426 hbk Phase 13 D-PRP-LENFIX — 정정: halfW(X축 절반)=Length1
                    _editingDatum.Line1_Length2 = halfH; //260426 hbk Phase 13 D-PRP-LENFIX — 정정: halfH(Y축 절반)=Length2
                    break;
                case EDatumTeachStep.Line2:
                    _editingDatum.Line2_Row     = centerRow;
                    _editingDatum.Line2_Col     = centerCol;
                    _editingDatum.Line2_Phi     = 0.0;
                    _editingDatum.Line2_Length1 = halfW; //260426 hbk Phase 13 D-PRP-LENFIX — 정정: halfW=Length1
                    _editingDatum.Line2_Length2 = halfH; //260426 hbk Phase 13 D-PRP-LENFIX — 정정: halfH=Length2
                    break;
                case EDatumTeachStep.HorizontalA:
                    _editingDatum.Horizontal_A_Row     = centerRow;
                    _editingDatum.Horizontal_A_Col     = centerCol;
                    _editingDatum.Horizontal_A_Phi     = 0.0;
                    _editingDatum.Horizontal_A_Length1 = halfW; //260426 hbk Phase 13 D-PRP-LENFIX — 정정: halfW=Length1
                    _editingDatum.Horizontal_A_Length2 = halfH; //260426 hbk Phase 13 D-PRP-LENFIX — 정정: halfH=Length2
                    break;
                case EDatumTeachStep.HorizontalB:
                    _editingDatum.Horizontal_B_Row     = centerRow;
                    _editingDatum.Horizontal_B_Col     = centerCol;
                    _editingDatum.Horizontal_B_Phi     = 0.0;
                    _editingDatum.Horizontal_B_Length1 = halfW; //260426 hbk Phase 13 D-PRP-LENFIX — 정정: halfW=Length1
                    _editingDatum.Horizontal_B_Length2 = halfH; //260426 hbk Phase 13 D-PRP-LENFIX — 정정: halfH=Length2
                    break;
            }

            //260424 hbk Phase 12 Gap-3 — DatumConfig 자동 속성은 INotifyPropertyChanged 미발동 → PropertyGrid 강제 재바인딩 + RaisePropertyChanged 이중 신호
            try { _editingDatum.RaisePropertyChanged(string.Empty); } catch { }
            mParentWindow?.inspectionList?.RefreshParamEditor();
            //260424 hbk Phase 12 Gap-3 — 캔버스 오버레이도 새 좌표로 갱신 (Datum ROI Rect/Circle 재렌더)
            halconViewer.SetDatumOverlay(_editingDatum, true);

            AdvanceDatumTeachStep();
        }

        //260424 hbk Phase 12 — Circle 완료 (CircleTwoHorizontal 첫 step)
        private void HalconViewer_DatumCircleCompleted(object sender, CircleDrawCompletedArgs e) {
            halconViewer.CircleDrawingCompleted -= HalconViewer_DatumCircleCompleted;
            if (_editingDatum == null || e.Radius <= 0) { ExitCanvasMode(); return; }

            _editingDatum.CircleROI_Row    = e.CenterRow; //260424 hbk Phase 12 D-10
            _editingDatum.CircleROI_Col    = e.CenterCol; //260424 hbk Phase 12 D-10
            _editingDatum.CircleROI_Radius = e.Radius;    //260424 hbk Phase 12 D-10

            //260424 hbk Phase 12 Gap-3 — PropertyGrid 재바인딩 + Datum 오버레이 갱신 (CircleROI_* write-back 즉시 반영)
            try { _editingDatum.RaisePropertyChanged(string.Empty); } catch { }
            mParentWindow?.inspectionList?.RefreshParamEditor();
            halconViewer.SetDatumOverlay(_editingDatum, true);

            AdvanceDatumTeachStep();
        }

        //260424 hbk Phase 12 — 다음 step 전이
        private void AdvanceDatumTeachStep() {
            if (_editingDatum == null) { ExitCanvasMode(); return; }
            _datumTeachStep = GetNextStep(_editingDatum.AlgorithmTypeEnum, _datumTeachStep);
            StartDatumTeachStep(_datumTeachStep);
        }

        //260424 hbk Phase 12 D-02 — 마지막 ROI 직후 DatumFindingService.TryTeachDatum 자동 호출
        private void InvokeTryTeachDatum() {
            if (_editingDatum == null) { ExitCanvasMode(); return; }

            HImage img = halconViewer.CurrentImage; //260424 hbk Phase 12 — Phase 11 이미지 로드 이후 상태
            if (img == null) {
                label_drawHint.Content = "Datum 티칭 실패: 이미지가 없습니다. Grab 하세요"; //260424 hbk Phase 12
                label_drawHint.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFF87171")); //260424 hbk Phase 12 error red
                label_drawHint.Visibility = Visibility.Visible;
                _canvasMode = ECanvasMode.None;
                btn_teachDatum.IsChecked = false;
                _editingDatum = null;
                return;
            }

            var svc = new ReringProject.Halcon.Algorithms.DatumFindingService(); //260424 hbk Phase 12 — 무상태 서비스
            string error;
            bool ok = svc.TryTeachDatum(img, _editingDatum, out error);
            if (ok) {
                label_drawHint.Content = "Datum 티칭 완료 — Recipe Save 권장"; //260424 hbk Phase 12
                label_drawHint.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF4ADE80")); //260424 hbk Phase 12 success green
                label_drawHint.Visibility = Visibility.Visible;
                //260424 hbk Phase 12 — 오버레이 갱신 (LastTeachSucceeded=true → HalconDisplayService CircleTwoHorizontal/Horizontal A/B 분기 렌더)
                halconViewer.SetDatumOverlay(_editingDatum, true);
                //260425 hbk Phase 13 D-01..D-04 — teach 완료 시점에 후보 갱신
                PublishDatumRoiCandidates(_editingDatum);
            }
            else {
                label_drawHint.Content = "Datum 티칭 실패: " + (error ?? "unknown"); //260424 hbk Phase 12
                label_drawHint.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFF87171")); //260424 hbk Phase 12 error red
                label_drawHint.Visibility = Visibility.Visible;
            }

            //260424 hbk Phase 12 — ROI 유지(재튜닝 가능), canvas mode 해제
            _canvasMode = ECanvasMode.None;
            btn_teachDatum.IsChecked = false;
            _editingDatum = null;
        }

        //260424 hbk Phase 13 D-05..D-08 — 런타임 TryFindDatum 테스트 진입 (현재/Load 이미지 2-way + 성공 주황 십자 + 실패 에러 메시지)
        private void BtnTestFindDatum_Click(object sender, RoutedEventArgs e) {
            //260424 hbk Phase 13 D-05 — Datum 해결 (InspectionListView 선택 우선, _editingDatum fallback 없음 — teach 세션 독립)
            var datum = mParentWindow?.inspectionList?.SelectedParam as DatumConfig;
            if (datum == null || !datum.IsConfigured || !datum.LastTeachSucceeded) {
                CustomMessageBox.Show("Datum Find 테스트", "Datum 티칭이 완료된 후 테스트 가능합니다."); //260425 hbk Phase 13 cleanup — Plan 02 인자 순서 fix
                return;
            }

            //260424 hbk Phase 13 D-06 — 테스트 이미지 소스 선택 (현재 / Load / 취소)
            HImage testImage = AskTestImageSource();
            if (testImage == null) return; //260424 hbk Phase 13 D-06 — 사용자 취소

            //260424 hbk Phase 13 D-07/D-08 — DatumFindingService.TryFindDatum 호출 (Phase 4 Plan 01 L28 시그니처)
            var svc = new ReringProject.Halcon.Algorithms.DatumFindingService();
            HTuple transform;
            string error;
            bool ok = svc.TryFindDatum(testImage, datum, out transform, out error);

            //260424 hbk Phase 13 D-07/D-08 — label_drawHint 숨기고 label_testFindResult 로 전용 피드백
            label_drawHint.Visibility = Visibility.Collapsed;
            label_testFindResult.Visibility = Visibility.Visible;
            if (ok) {
                label_testFindResult.Content = string.Format(
                    "TryFind OK — RefOrigin=({0:F1}, {1:F1}), Angle={2:F3} rad",
                    datum.RefOriginRow, datum.RefOriginCol, datum.RefAngleRad); //260424 hbk Phase 13 D-07
                label_testFindResult.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF4ADE80")); //260424 hbk Phase 13 D-07 LimeGreen
                //260424 hbk Phase 13 D-07 — 성공 시 주황 십자 오버레이 렌더 (RenderDatumFindResult — HalconDisplayService)
                halconViewer.SetDatumFindResultOverlay(datum);
            }
            else {
                label_testFindResult.Content = "TryFind FAIL — " + (error ?? "unknown"); //260424 hbk Phase 13 D-08
                label_testFindResult.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFF87171")); //260424 hbk Phase 13 D-08 error red
                //260424 hbk Phase 13 D-08 — 실패 시 오버레이 clear (이전 성공 십자 잔상 제거)
                halconViewer.ClearDatumFindResultOverlay();
            }
        }

        //260424 hbk Phase 13 D-06 — 테스트 이미지 소스 다이얼로그: 현재 halconViewer.CurrentImage / OpenFileDialog / 취소
        //  반환 HImage 는 halconViewer.CurrentImage 참조 그대로 (별도 Dispose 책임 없음 — halconViewer 가 소유)
        private HImage AskTestImageSource() {
            HImage currentImg = halconViewer.CurrentImage;
            bool hasCurrent = (currentImg != null);

            //260424 hbk Phase 13 D-06 — 3-way 선택 (MessageBox YesNoCancel: Yes=현재 이미지 / No=파일 선택 / Cancel=취소)
            MessageBoxResult choice;
            if (hasCurrent) {
                choice = MessageBox.Show(
                    "테스트 이미지를 선택하세요.\n\n[예] 현재 이미지로 테스트\n[아니오] 다른 파일 선택...\n[취소] 취소",
                    "Datum Find 테스트 이미지",
                    MessageBoxButton.YesNoCancel,
                    MessageBoxImage.Question);
            }
            else {
                //260424 hbk Phase 13 D-06 — 현재 이미지 없으면 바로 파일 선택 경로 (2-way)
                choice = MessageBoxResult.No; // 파일 선택 분기로 진입
            }

            if (choice == MessageBoxResult.Cancel) return null;
            if (choice == MessageBoxResult.Yes) return currentImg; //260424 hbk Phase 13 D-06 — 현재 이미지 그대로 사용

            //260424 hbk Phase 13 D-06 — No = OpenFileDialog (LoadAndDisplay L264-272 필터 재사용)
            var dialog = new OpenFileDialog {
                Filter = "Image Files (*.bmp;*.jpg;*.jpeg;*.png;*.tif;*.tiff)|*.bmp;*.jpg;*.jpeg;*.png;*.tif;*.tiff|All Files (*.*)|*.*"
            };
            if (dialog.ShowDialog() != true) return null;

            try {
                halconViewer.LoadImage(dialog.FileName); //260424 hbk Phase 13 D-06 — halconViewer 가 CurrentImage 교체 + Render
                return halconViewer.CurrentImage; //260424 hbk Phase 13 D-06 — 로드된 이미지 참조 반환
            }
            catch (Exception ex) {
                Logging.PrintErrLog((int)ELogType.Error, "Datum Test Load fail: " + ex.Message); //260424 hbk Phase 13 D-08
                CustomMessageBox.Show("Datum Find 테스트", "이미지 로드 실패: " + ex.Message); //260425 hbk Phase 13 cleanup
                return null;
            }
        }
    }
}
