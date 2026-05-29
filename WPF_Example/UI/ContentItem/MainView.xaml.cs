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
        //260519 hbk Phase 31 CO-23.1-02 — 타입 MeasurementBase 로 일반화 (CircleDiameter + CircleCenterDistance 커버)
        private MeasurementBase _editingCircleMeasurement;
        //260423 hbk Circle ROI 편집 대상 FAI 이름 (RoiDefinition.Id=FAIName 과 일치 유지)
        private string _editingCircleFaiName;
        //260517 hbk Phase 23.1 D-01 — EdgeToLineDistance Rect ROI 편집 대상 Measurement
        //260519 hbk Phase 31 CO-23.1-02 — 타입 MeasurementBase 로 일반화 (Point_* ROI 보유 타입 전체 커버)
        private MeasurementBase _editingMeasurement;
        //260517 hbk Phase 23.1 D-01 — Rect ROI 편집 대상 FAI 이름 (UpdateDisplayState selId 용)
        private string _editingMeasurementFaiName;
        private int _editingMeasurementRoiIndex; //260521 hbk Phase 32 I9/I10-redesign — ArcLineIntersect 4-ROI 순차 드로잉 인덱스 (0=EdgeA1, 1=EdgeB1, 2=EdgeA2, 3=EdgeB2)
        //260424 hbk Phase 12 D-03 — Datum 티칭 단계 (알고리즘별 switch 로 전이 결정)
        //  Phase 13 에서 DatumAlgorithmBase.GetROISteps() 가변 배열로 재설계 예정 — switch 는 MainView 내 private 유지.
        private enum EDatumTeachStep { Line1, Line2, Circle, Vertical, HorizontalA, HorizontalB, Done }
        private EDatumTeachStep _datumTeachStep = EDatumTeachStep.Line1; //260424 hbk Phase 12 D-03
        private DatumConfig _editingDatum; //260424 hbk Phase 12 — 현재 티칭 중 Datum
        //260527 hbk Phase 34.1 D-34.1-08/12 — 현재 캔버스에 표시 중인 이미지 축 (가로/세로). DualImage 변형에서만 의미 있음.
        //  세션 한정, INI 미저장 (Datum 노드 이동 시 가로축으로 리셋).
        private ReringProject.Sequence.EImageSource _currentImageSource = ReringProject.Sequence.EImageSource.Horizontal;
        //260527 hbk Phase 34.1 CO-34.1-02 hotfix — 현재 선택된 Datum (teach 모드 무관, swap UI 의 대상).
        //  _editingDatum 은 Teach Datum 클릭 시에만 set → 토글 핸들러가 노드 선택 직후 동작하려면 별도 reference 필요.
        //  PublishDatumRoiCandidates 진입 시 갱신, AlgorithmType PropertyChanged 구독 대상.
        private DatumConfig _selectedDatumForSwap;
        //260527 hbk Phase 34.1 CO-34.1-03 hotfix — 배지 색상 정적 frozen brush (인스턴스 GC 방지 + WPF 즉시 반영).
        //  ConvertFromString 매 호출마다 새 brush 생성 → 일부 환경에서 WPF Background 갱신 누락 의심 → 정적/frozen 으로 대체.
        private static readonly SolidColorBrush BadgeBrushHorizontal = CreateFrozenBrush(0x19, 0x76, 0xD2); //260527 hbk Phase 34.1 — Material Blue 700
        private static readonly SolidColorBrush BadgeBrushVertical   = CreateFrozenBrush(0xF5, 0x7C, 0x00); //260527 hbk Phase 34.1 — Material Orange 800
        private static SolidColorBrush CreateFrozenBrush(byte r, byte g, byte b) {
            var brush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(r, g, b));
            brush.Freeze();
            return brush;
        }
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
            //260505 hbk Phase 18 CO-04 — "ROI 다시 그리기": Length/Radius 0 리셋 후 오버레이 갱신
            halconViewer.RoiRedrawRequested += (roiId) => //260505 hbk Phase 18 CO-04
            { //260505 hbk Phase 18 CO-04
                if (_editingDatum != null) //260505 hbk Phase 18 CO-04
                { //260505 hbk Phase 18 CO-04
                    ClearDatumRoiFields(_editingDatum, roiId); //260505 hbk Phase 18 CO-04
                    halconViewer.SetDatumOverlay(_editingDatum, false); //260505 hbk Phase 18 CO-04
                    PublishDatumRoiCandidates(_editingDatum); //260505 hbk Phase 18 CO-04
                } //260505 hbk Phase 18 CO-04
            }; //260505 hbk Phase 18 CO-04
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
        public void DisplayShotImage(ShotConfig shot) { //260521 hbk Phase 32 UAT — private → public (Shot/Measurement 노드 선택 시 InspectionListView 에서 호출)
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
                    if (img != null) img.Dispose(); //260509 hbk Phase 20 — ?. expanded
                }
            } else {
                label_message.Content = "NO Image";
                label_message.Visibility = Visibility.Visible;
            }
        }

        //260527 hbk Phase 35 — CO-33-02 hotfix: Datum 노드 선택 시 TeachingImagePath 표시 (Shot/Measurement 와 일관성 확보, stale canvas 차단).
        //  Phase 22 IMG-02 dual-image 구조 (TeachingImagePath != SimulImagePath) 보존 — Datum 전용 이미지 canvas 직접 표시.
        /// <summary>Displays the TeachingImagePath image of the given DatumConfig on the canvas.
        /// Mirrors DisplayShotImage but uses DatumConfig.TeachingImagePath (Phase 22 IMG-02 분리 구조).</summary>
        public void DisplayDatumImage(DatumConfig datum) { //260527 hbk Phase 35
            if (datum == null) { //260527 hbk Phase 35
                return; //260527 hbk Phase 35
            }
            string path = datum.TeachingImagePath; //260527 hbk Phase 35
            if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path)) { //260527 hbk Phase 35
                // TeachingImagePath 미설정/파일 없음 — 기존 canvas 유지 (사용자가 Load Image 누르면 갱신)
                return; //260527 hbk Phase 35
            }
            try { //260527 hbk Phase 35
                halconViewer.LoadImage(path); //260527 hbk Phase 35
                label_message.Visibility = Visibility.Collapsed; //260527 hbk Phase 35
            } catch (Exception ex) { //260527 hbk Phase 35
                Logging.PrintErrLog((int)ELogType.Error, ex.Message); //260527 hbk Phase 35
            }
        }

        //260521 hbk Phase 32 UAT — Measurement 노드 선택 시 소유 Shot 이미지 표시 진입점.
        //  MeasurementBase → FAI(FindFaiNameContainingMeasurement) → ShotConfig(FAIConfig.Owner) → DisplayShotImage.
        /// <summary>Resolves the owning ShotConfig for the given measurement and displays its image.</summary>
        public void DisplayMeasurementImage(MeasurementBase measurement) { //260521 hbk Phase 32 UAT
            if (measurement == null) return; //260521 hbk Phase 32 UAT
            //260528 hbk Phase 37 — 이름 round-trip(FindFAIByName) 제거: 여러 Shot 의 FAI 이름이 같으면(기본 FAI_0 등)
            //  첫 Shot 의 FAI 가 반환돼 잘못된 Shot 이미지가 표시됨. 소유 FAIConfig 를 객체 참조로 직접 해석.
            FAIConfig fai = FindFAIContainingMeasurement(measurement); //260528 hbk Phase 37
            if (fai == null) return; //260521 hbk Phase 32 UAT
            ShotConfig shot = fai.Owner as ShotConfig; //260521 hbk Phase 32 UAT
            DisplayShotImage(shot); //260521 hbk Phase 32 UAT
        } //260521 hbk Phase 32 UAT

        //260529 hbk Phase 39.1-03 G4-01/G4-02 — 검사 후 FAI/Measurement 노드 클릭 시 측정 결과 + 이미지 + overlay 재현 통합 진입점.
        //  Sequence 동작 변경 0: 측정 재 호출 없이 fai.LastOverlays (Action_FAIMeasurement EStep.Measure 누적) 재 렌더.
        //  전 FAI 타입 공통 동작 (D-G4-02 — CircleDiameter / EdgeToLineDistance / PointToLineDistance / EdgePairDistance / EdgeToLineAngle / ArcEdgeDistance).
        public void RenderInspectionResultForNode(ParamBase param) { //260529 hbk Phase 39.1-03 G4-01
            if (param == null) return; //260529 hbk Phase 39.1-03 G4-01
            if (param is MeasurementBase meas) { //260529 hbk Phase 39.1-03 G4-01
                DisplayMeasurementImage(meas); //260529 hbk Phase 39.1-03 G4-01
                HighlightSelectedRoi(meas); //260529 hbk Phase 39.1-03 G4-01
                RenderStoredOverlaysForMeasurement(meas); //260529 hbk Phase 39.1-03 G4-01
            } else if (param is FAIConfig fai) { //260529 hbk Phase 39.1-03 G4-01
                DisplayFAIImage(fai); //260529 hbk Phase 39.1-03 G4-01
                HighlightSelectedRoi(fai); //260529 hbk Phase 39.1-03 G4-01
                RenderStoredOverlaysForFai(fai); //260529 hbk Phase 39.1-03 G4-01
            }
        }

        //260529 hbk Phase 39.1-03 G4-01 — FAI 노드 클릭 시 fai.LastOverlays 전체 재 렌더.
        //  W2 (260529) 확인: HalconViewerControl.SetInspectionOverlays 는 REPLACE 의미 (Clear + AddRange).
        //  null/빈 케이스에 빈 List 호출 → prior overlay 안전 클리어.
        private void RenderStoredOverlaysForFai(FAIConfig fai) { //260529 hbk Phase 39.1-03 G4-01
            if (fai == null || fai.LastOverlays == null || fai.LastOverlays.Count == 0) { //260529 hbk Phase 39.1-03 G4-01
                halconViewer.SetInspectionOverlays(new System.Collections.Generic.List<ReringProject.Halcon.Models.EdgeInspectionOverlay>()); //260529 hbk Phase 39.1-03 G4-01
                return; //260529 hbk Phase 39.1-03 G4-01
            }
            halconViewer.SetInspectionOverlays(fai.LastOverlays); //260529 hbk Phase 39.1-03 G4-01
        }

        //260529 hbk Phase 39.1-03 G4-01 — Measurement 노드 클릭 시 소유 FAI 의 LastOverlays 재 렌더 (D-G4-02 전 타입 공통, 타입별 분기 없음).
        private void RenderStoredOverlaysForMeasurement(MeasurementBase meas) { //260529 hbk Phase 39.1-03 G4-01
            if (meas == null) { //260529 hbk Phase 39.1-03 G4-01
                halconViewer.SetInspectionOverlays(new System.Collections.Generic.List<ReringProject.Halcon.Models.EdgeInspectionOverlay>()); //260529 hbk Phase 39.1-03 G4-01
                return; //260529 hbk Phase 39.1-03 G4-01
            }
            FAIConfig fai = FindFAIContainingMeasurement(meas); //260528 hbk Phase 37 hotfix A/B — 객체 참조 round-trip 회피
            RenderStoredOverlaysForFai(fai); //260529 hbk Phase 39.1-03 G4-01
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
                //260519 hbk #6-a — ItemsSource 교체 시 WPF 가 SelectedItem=null 로 자동 리셋하며 이 핸들러를 동기 발화.
                //  4인자 UpdateDisplayState(null) 는 _selectedRoiId=null clobber → 이후 HighlightSelectedRoi 의 하이라이트를
                //  렌더링 타이밍에 따라 지워버림. 3인자 오버로드(selectedRoiId 미변경)를 써서 트리 선택 하이라이트를 보존한다.
                var allRois = GetCurrentFAIRois();
                if (allRois.Count > 0)
                    halconViewer.UpdateDisplayState(allRois, null, null); //260519 hbk #6-a — 3인자: _selectedRoiId 보존
                return;
            }

            var rois = GetCurrentFAIRois();
            //260519 hbk #6-a — 결과행 선택 시 그 측정 전용 ROI(Id="FAIName_측정명")만 하이라이트한다.
            //  해당 측정 ROI 가 없으면 FAIName 으로 폴백 — Render 의 접두사 매칭이 FAI 전체 ROI 를 하이라이트.
            //  selectedRow.FAIName 만 쓰면 FAI 노드 선택과 동일 selId 라 행을 바꿔도 색이 안 변함.
            string composite = selectedRow.FAIName + "_" + selectedRow.MeasurementName;
            string selectedRoiId = selectedRow.FAIName;
            foreach (var r in rois) {
                if (r != null && r.Id == composite) { selectedRoiId = composite; break; }
            }
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
                //260517 hbk Phase 23.1 D-03 / 260519 hbk Phase 31 hotfix#4 — Point ROI 보유 측정 타입 동시 수집
                //  (EdgeToLineDistance/EdgeToLineAngle/ArcEdgeDistance — Phase 31 신규 타입 캔버스 렌더 누락 수정)
                foreach (var m in fai.Measurements) {
                    result.AddRange(BuildPointRoiDefinitions(m, fai.FAIName)); //260521 hbk Phase 32 UAT
                }
            }
            return result;
        }

        //260519 hbk #6-a — 단일 FAI 의 rect ROI + EdgeToLineDistance Point ROI 를 result 에 누적한다.
        //  GetCurrentFAIRois 의 FAI별 수집 규칙(Id/IsTaught/Point ROI)과 동일 — DataGrid 비의존 버전이 공유한다.
        private void AppendFaiRois(List<RoiDefinition> result, FAIConfig fai) {
            if (fai == null) return;
            var roi = fai.ToRoiDefinition();
            if (roi.IsTaught) result.Add(roi);
            //260517 hbk Phase 23.1 D-03 / 260519 hbk Phase 31 hotfix#4 — Point ROI 보유 측정 타입 동시 수집
            foreach (var m in fai.Measurements) {
                result.AddRange(BuildPointRoiDefinitions(m, fai.FAIName)); //260521 hbk Phase 32 UAT
            }
        }

        //260519 hbk Phase 31 hotfix#4 — Point ROI 보유 측정 타입 → RoiDefinition 리스트 변환 (캔버스 렌더용).
        //  EdgeToLineDistance(Phase 23.1) 만 수집하던 누락을 EdgeToLineAngle/ArcEdgeDistance 까지 일반화.
        //  Length1/2 미티칭(≤0) 이면 해당 ROI skip. CommitRectRoi 가 Point_Phi=0 으로만 쓰므로 축정렬 bounding box.
        //260521 hbk Phase 32 UAT — 반환 타입 List<RoiDefinition> 으로 변경: ArcLineIntersect 는 EdgeA + EdgeB 2개 반환.
        //  비-ArcLineIntersect 타입은 0 또는 1개짜리 리스트 반환 (기존 null 반환 규칙 보존: 길이 0 = skip).
        private static List<RoiDefinition> BuildPointRoiDefinitions(MeasurementBase m, string faiName) { //260521 hbk Phase 32 UAT
            var result = new List<RoiDefinition>(); //260521 hbk Phase 32 UAT
            string measName = m.MeasurementName; //260521 hbk Phase 32 UAT
            if (string.IsNullOrEmpty(measName)) measName = m.TypeName; //260521 hbk Phase 32 UAT

            //260521 hbk Phase 32 I9/I10-redesign — ArcLineIntersect: EdgeA1/EdgeB1/EdgeA2/EdgeB2 4개 독립 RoiDefinition. 미티칭 ROI 는 개별 skip.
            var ali = m as ArcLineIntersectDistanceMeasurement; //260521 hbk Phase 32 I9/I10-redesign
            if (ali != null) {
                if (ali.EdgeA1_Length1 > 0 && ali.EdgeA1_Length2 > 0) { //260521 hbk Phase 32 I9/I10-redesign
                    result.Add(new RoiDefinition {
                        Id = faiName + "_" + measName + "_EdgeA1", //260521 hbk Phase 32 I9/I10-redesign
                        Name = measName + "_EdgeA1", //260521 hbk Phase 32 I9/I10-redesign
                        Row1 = ali.EdgeA1_Row - ali.EdgeA1_Length1, Column1 = ali.EdgeA1_Col - ali.EdgeA1_Length2,
                        Row2 = ali.EdgeA1_Row + ali.EdgeA1_Length1, Column2 = ali.EdgeA1_Col + ali.EdgeA1_Length2,
                        IsTaught = true
                    }); //260521 hbk Phase 32 I9/I10-redesign
                } //260521 hbk Phase 32 I9/I10-redesign
                if (ali.EdgeB1_Length1 > 0 && ali.EdgeB1_Length2 > 0) { //260521 hbk Phase 32 I9/I10-redesign
                    result.Add(new RoiDefinition {
                        Id = faiName + "_" + measName + "_EdgeB1", //260521 hbk Phase 32 I9/I10-redesign
                        Name = measName + "_EdgeB1", //260521 hbk Phase 32 I9/I10-redesign
                        Row1 = ali.EdgeB1_Row - ali.EdgeB1_Length1, Column1 = ali.EdgeB1_Col - ali.EdgeB1_Length2,
                        Row2 = ali.EdgeB1_Row + ali.EdgeB1_Length1, Column2 = ali.EdgeB1_Col + ali.EdgeB1_Length2,
                        IsTaught = true
                    }); //260521 hbk Phase 32 I9/I10-redesign
                } //260521 hbk Phase 32 I9/I10-redesign
                if (ali.EdgeA2_Length1 > 0 && ali.EdgeA2_Length2 > 0) { //260521 hbk Phase 32 I9/I10-redesign
                    result.Add(new RoiDefinition {
                        Id = faiName + "_" + measName + "_EdgeA2", //260521 hbk Phase 32 I9/I10-redesign
                        Name = measName + "_EdgeA2", //260521 hbk Phase 32 I9/I10-redesign
                        Row1 = ali.EdgeA2_Row - ali.EdgeA2_Length1, Column1 = ali.EdgeA2_Col - ali.EdgeA2_Length2,
                        Row2 = ali.EdgeA2_Row + ali.EdgeA2_Length1, Column2 = ali.EdgeA2_Col + ali.EdgeA2_Length2,
                        IsTaught = true
                    }); //260521 hbk Phase 32 I9/I10-redesign
                } //260521 hbk Phase 32 I9/I10-redesign
                if (ali.EdgeB2_Length1 > 0 && ali.EdgeB2_Length2 > 0) { //260521 hbk Phase 32 I9/I10-redesign
                    result.Add(new RoiDefinition {
                        Id = faiName + "_" + measName + "_EdgeB2", //260521 hbk Phase 32 I9/I10-redesign
                        Name = measName + "_EdgeB2", //260521 hbk Phase 32 I9/I10-redesign
                        Row1 = ali.EdgeB2_Row - ali.EdgeB2_Length1, Column1 = ali.EdgeB2_Col - ali.EdgeB2_Length2,
                        Row2 = ali.EdgeB2_Row + ali.EdgeB2_Length1, Column2 = ali.EdgeB2_Col + ali.EdgeB2_Length2,
                        IsTaught = true
                    }); //260521 hbk Phase 32 I9/I10-redesign
                } //260521 hbk Phase 32 I9/I10-redesign
                return result; //260521 hbk Phase 32 I9/I10-redesign — 나머지 타입 분기 통과 불필요
            }

            double pRow = 0, pCol = 0, pLen1 = 0, pLen2 = 0;
            var etld = m as EdgeToLineDistanceMeasurement;
            if (etld != null) { pRow = etld.Point_Row; pCol = etld.Point_Col; pLen1 = etld.Point_Length1; pLen2 = etld.Point_Length2; }
            var etla = m as EdgeToLineAngleMeasurement;
            if (etla != null) { pRow = etla.Point_Row; pCol = etla.Point_Col; pLen1 = etla.Point_Length1; pLen2 = etla.Point_Length2; }
            var aed = m as ArcEdgeDistanceMeasurement;
            if (aed != null) { pRow = aed.Point_Row; pCol = aed.Point_Col; pLen1 = aed.Point_Length1; pLen2 = aed.Point_Length2; }
            var cAngle = m as CompoundAngleMeasurement; //260521 hbk Phase 32
            if (cAngle != null) { pRow = cAngle.Rect_Row; pCol = cAngle.Rect_Col; pLen1 = cAngle.Rect_Length1; pLen2 = cAngle.Rect_Length2; }
            var cCenterC = m as CompoundCenterCDistanceMeasurement; //260521 hbk Phase 32
            if (cCenterC != null) { pRow = cCenterC.Rect_Row; pCol = cCenterC.Rect_Col; pLen1 = cCenterC.Rect_Length1; pLen2 = cCenterC.Rect_Length2; }
            var cCenterB = m as CompoundCenterBDistanceMeasurement; //260521 hbk Phase 32
            if (cCenterB != null) { pRow = cCenterB.Rect_Row; pCol = cCenterB.Rect_Col; pLen1 = cCenterB.Rect_Length1; pLen2 = cCenterB.Rect_Length2; }
            var cShort = m as CompoundShortAxisDistanceMeasurement; //260523 hbk Phase 32 — E3 단축 환원
            if (cShort != null) { pRow = cShort.Rect_Row; pCol = cShort.Rect_Col; pLen1 = cShort.Rect_Length1; pLen2 = cShort.Rect_Length2; }
            if (pLen1 <= 0 || pLen2 <= 0) return result; //260521 hbk Phase 32 UAT — 미티칭 시 빈 리스트 반환 (기존 null 규칙 대체)
            result.Add(new RoiDefinition { //260521 hbk Phase 32 UAT
                Id = faiName + "_" + measName,
                Name = measName,
                Row1 = pRow - pLen1,
                Column1 = pCol - pLen2,
                Row2 = pRow + pLen1,
                Column2 = pCol + pLen2,
                IsTaught = true
            });
            return result; //260521 hbk Phase 32 UAT
        }

        //260519 hbk #6-a — 주어진 FAI 가 속한 Shot 의 모든 FAI ROI 를 DataGrid 비의존으로 수집한다.
        //  GetCurrentFAIRois 는 dataGrid_faiResults 바인딩 갱신 지연으로 트리 선택보다 한 박자 늦은 ROI 집합을 줘
        //  → 트리 선택 하이라이트가 stale FAI 기준이 되어 Id 불일치(녹색 유지) 발생. anchorFai.Owner(Shot)에서 직접 수집한다.
        private List<RoiDefinition> CollectShotRois(FAIConfig anchorFai) {
            var result = new List<RoiDefinition>();
            if (anchorFai == null) return result;
            ShotConfig shot = anchorFai.Owner as ShotConfig;
            if (shot == null || shot.FAIList == null) {
                AppendFaiRois(result, anchorFai); //260519 hbk #6-a — Shot 미해결 시 anchor 단독 수집 (fallback)
                return result;
            }
            foreach (FAIConfig fai in shot.FAIList) {
                AppendFaiRois(result, fai);
            }
            return result;
        }

        //260518 hbk #6 — 선택된 Measurement/FAI 노드의 ROI 를 캔버스에서 노란색 하이라이트한다.
        /// <summary>
        /// 트리에서 선택된 param 의 ROI Id 를 도출해 halconViewer 에 하이라이트를 적용한다.
        /// param 이 FAIConfig 또는 MeasurementBase 가 아니면 하이라이트를 해제한다.
        /// </summary>
        public void HighlightSelectedRoi(ParamBase param) {
            //260519 hbk #6-a — 선택 노드의 ROI 하이라이트 ID 도출 + 대상 FAI(anchorFai) 해결
            string selRoiId = null;
            string faiNameForFallback = null;
            FAIConfig anchorFai = null;
            if (param is FAIConfig faiSel) {
                //260519 hbk #6-a — FAI 노드 선택 시 하이라이트 없음(ROI 전부 녹색) — 사용자 결정.
                //  측정 노드/결과행을 선택해야 그 ROI 1개가 노란색. selRoiId 는 null 유지.
                anchorFai = faiSel;
            }
            else if (param is MeasurementBase measSel) {
                string faiName = FindFaiNameContainingMeasurement(measSel);
                faiNameForFallback = faiName;
                string mName = measSel.MeasurementName;
                if (string.IsNullOrEmpty(mName)) mName = measSel.TypeName;
                if (!string.IsNullOrEmpty(faiName)) {
                    selRoiId = faiName + "_" + mName;
                    //260528 hbk Phase 37 — anchorFai 를 이름(FindFAIByName) 대신 측정 객체 참조로 해석.
                    //  여러 Shot 동일 FAI 명 시 ROI 하이라이트가 첫 Shot 으로 잘못 묶이던 결함 차단(DisplayMeasurementImage 와 동일 원인).
                    anchorFai = FindFAIContainingMeasurement(measSel); //260528 hbk Phase 37
                }
            }
            //260519 hbk #6-a — dataGrid_faiResults 바인딩 지연 회피: 선택 FAI 의 Shot 에서 직접 ROI 수집
            var rois = CollectShotRois(anchorFai);
            //260519 hbk #6-a — composite ID 매칭 ROI 없으면 부모 FAI ROI 로 fallback (일반 FAI rect ROI 는 Id=FAIName)
            if (!string.IsNullOrEmpty(selRoiId) && !string.IsNullOrEmpty(faiNameForFallback)) {
                bool matched = false;
                foreach (var r in rois) {
                    if (r != null && r.Id == selRoiId) { matched = true; break; }
                }
                if (!matched) selRoiId = faiNameForFallback;
            }
            //260519 hbk #6-a — UI 스레드(InspectionListView SelectionChanged)에서 직접 호출 — 타이밍 레이스 제거.
            halconViewer.UpdateDisplayState(rois, selRoiId, null, null); //260519 hbk #6-a
        }

        //260417 hbk Phase 6 Plan 04: 모든 시퀀스/Shot에서 FAIName으로 FAIConfig 조회 (D-21)
        private FAIConfig FindFAIByName(string faiName) {
            if (string.IsNullOrEmpty(faiName) || pSeq == null) return null;
            for (int i = 0; i < pSeq.Count; i++) {
                var seq = pSeq[i];
                if (seq == null) continue;
                for (int j = 0; j < seq.ActionCount; j++) {
                    var act = seq[j];
                    //260509 hbk Phase 20 — ?. expanded to explicit null-check
                    if (act != null && act.Param is ShotConfig shot) {
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
                            //260509 hbk Phase 20 — ternary expanded
                            if (pDev[param.DeviceName].IsGrabFromFile) resultStr = "Grab From File";
                            else                                       resultStr = "Grab Success";
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
            //260518 hbk #3 — 1-인자 오버로드는 표시/경로저장 동일 param 위임 (코드 중복 제거)
            LoadAndDisplay(param, param as IOfflineImageParam);
        }

        //260518 hbk #3 — 표시용(displayParam)과 경로 persistence(pathSinkParam) 분리.
        //  Datum 노드 Load 시 표시는 Shot 으로 위임하되 경로는 DatumConfig.TeachingImagePath 로 저장하기 위함.
        /// <summary>
        /// 이미지를 로드해 표시하고, 선택 경로를 pathSinkParam 에 기록한다.
        /// pathSinkParam 이 null 이면 경로 persistence 를 건너뛴다.
        /// </summary>
        public void LoadAndDisplay(ICameraParam displayParam, IOfflineImageParam pathSinkParam) {
            ICameraParam param = displayParam;
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
                if (pathSinkParam != null) { //260518 hbk #3 — 경로저장 대상 분리
                    //260527 hbk Phase 34.1 CO-34.1-07 hotfix — DualImage + 세로 토글 활성 시 TeachingImagePath_Vertical 로 저장.
                    //  기본 동작 (가로 토글 활성 또는 1-image algorithm) = SetLatestImagePath → TeachingImagePath.
                    //  세로 토글 활성 + DualImage 한정 분기 (DatumConfig.cs 변경 0 가드 유지 위해 외부에서 property 직접 설정).
                    DatumConfig datumSink = pathSinkParam as DatumConfig;
                    if (datumSink != null
                        && datumSink.AlgorithmTypeEnum == EDatumAlgorithm.VerticalTwoHorizontalDualImage
                        && _currentImageSource == ReringProject.Sequence.EImageSource.Vertical) {
                        datumSink.TeachingImagePath_Vertical = dialog.FileName; //260527 hbk Phase 34.1 CO-34.1-07
                    }
                    else {
                        pathSinkParam.SetLatestImagePath(dialog.FileName); //260527 hbk Phase 34.1 CO-34.1-07 — 기본 (가로 또는 1-image)
                    }
                }
                //260521 hbk Phase 32 UAT — Shot 노드 Load 시 _image 버퍼 동기화
                //  displayParam == pathSinkParam (동일 참조) 일 때만 Shot 캐시 갱신.
                //  Datum 노드 Load 는 displayParam=ShotConfig, pathSinkParam=DatumConfig (참조 불일치) → 건너뜀.
                //260527 hbk Phase 35 — CO-33-02 의도 강화: 본 가드는 ShotConfig._image 캐시 오염 방지의 단일 책임을 가진다.
                //  ReferenceEquals(displayParam, pathSinkParam) == true ⇔ 사용자가 Shot 노드에서 Load → 캐시 갱신.
                //  Datum 노드 Load → DatumConfig.TeachingImagePath 만 기록, Shot 캐시 무오염 (Phase 22 IMG-02 분리 구조 보존). 동작 byte-identical.
                if (displayParam is ShotConfig shot && ReferenceEquals(displayParam, pathSinkParam)) {
                    HImage currentImg = halconViewer.CurrentImage; //260521 hbk Phase 32 UAT
                    if (currentImg != null) {
                        shot.SetImage(currentImg); //260521 hbk Phase 32 UAT — SetImage 내부 CopyImage 로 소유권 분리
                    }
                }
                //260519 hbk Phase 31 CO-23.1-01 — 이미지 출처 레이블 갱신 (Load 시 경로 확인)
                UpdateImageSourceLabel(pathSinkParam as DatumConfig, param as ShotConfig);

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
                    //260509 hbk Phase 20 — ternary expanded; Phase 7 Timer null 체크 의도 보존
                    double elapsed;
                    if (context.Timer != null) elapsed = context.Timer.Elapsed.TotalMilliseconds / 1000.0;
                    else                       elapsed = 0;
                    var resultStr = string.Format("{0}\n{1} ({2:0.00}s)", param, context.ResultString, elapsed);
                    label_message.Content = string.Format(
                        "{0}\n{1}",
                        resultStr,
                        BuildViewerStateSummary(context.ResultImagePath, ConvertParamRects(param), context.InspectionOverlays));
                    label_message.Foreground = GetResultBrush(context.Result);
                    label_message.Visibility = Visibility.Visible;

                    //260509 hbk Phase 20 — ?. + ?? chain expanded; Parent null 안전 의도 보존 (동적 Shot/FAI 대응)
                    string seqName;
                    if (param.Parent != null && param.Parent.Name != null) seqName = param.Parent.Name;
                    else if (context.Source != null && context.Source.Name != null) seqName = context.Source.Name;
                    else seqName = "";
                    foreach (IMainView customView in CustomViewList) {
                        customView.Display(seqName, resultStr, label_message.Foreground, param.OwnerName);
                    }
                    RefreshFAIResultRows(); //260409 hbk Phase 3
                    //260519 hbk Phase 31 CO-23.1-01 — 검사 실행 결과 표시 시 이미지 출처 레이블 갱신
                    UpdateImageSourceLabel(null, param as ShotConfig);
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
                    //260509 hbk Phase 20 — ternary expanded
                    string name;
                    if (context.ActionParam != null) name = context.ActionParam.ToString();
                    else                             name = context.Source.Name;
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
            //260509 hbk Phase 20 — ternaries expanded; Phase 17 D-15 hover 표시 의도 보존
            string grayStr;
            if (grayValue.HasValue) grayStr = grayValue.Value.ToString("0.0");
            else                    grayStr = "-";
            if (label_pos != null) {
                label_pos.Content = string.Format(
                    "X:{0:0.0}, Y:{1:0.0}, G:{2}",
                    x,
                    y,
                    grayStr);
            }
            //260503 hbk Phase 17 D-15 — 상단 툴바 hover 표시 (정수 + N/A, mm 변환은 deferred)
            //  PublishPointerInfo (MainResultViewerControl L1297-1319) 가 CurrentImage==null 시 (0,0,null) 발행 — grayValue.HasValue=false 일 때 X/Y 도 N/A 표시.
            //  신규 GetGrayval 호출 0 — 기존 PointerInfoChanged 파이프라인 재사용 (PATTERNS gap #4).
            string hoverX, hoverY, hoverG;
            if (grayValue.HasValue) {
                hoverX = "X: " + x.ToString("0");
                hoverY = "Y: " + y.ToString("0");
                hoverG = "Gray: " + grayValue.Value.ToString("0");
            } else {
                hoverX = "X: N/A";
                hoverY = "Y: N/A";
                hoverG = "Gray: N/A";
            }
            if (txt_hoverX != null) txt_hoverX.Text = hoverX; //260509 hbk Phase 20 (Phase 17 D-15)
            if (txt_hoverY != null) txt_hoverY.Text = hoverY; //260509 hbk Phase 20 (Phase 17 D-15)
            if (txt_hoverG != null) txt_hoverG.Text = hoverG; //260509 hbk Phase 20 (Phase 17 D-15)
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

            //260509 hbk Phase 20 — ternary expanded
            List<RoiDefinition> roiList;
            if (rois == null) roiList = new List<RoiDefinition>();
            else              roiList = rois.ToList();

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
        //260426 hbk Phase 14-01 D-04 — Datum 분기 우선 (FAI lookup 전에) — 단일 RoiGeometryChanged 이벤트 확장
        private void HalconViewer_RoiGeometryChanged(object sender, RoiGeometryChangedArgs e) {
            if (e == null || string.IsNullOrEmpty(e.RoiId)) return;

            //260426 hbk Phase 14-01 — Datum.* RoiId 면 FAI 분기 진입 전에 처리하고 return
            DatumConfig datum;
            if (e.RoiId.StartsWith("Datum.") && IsCurrentNodeDatum(out datum) && datum != null) {
                HandleDatumRoiResize(datum, e);
                return;
            }

            var fai = FindFAIByName(e.RoiId);
            if (fai == null) return;

            if (e.Shape == RoiShape.Circle) {
                //260519 hbk Phase 31 CO-23.1-02 — CircleDiameter + CircleCenterDistance 두 타입 모두 Edit write-back
                foreach (var m in fai.Measurements) {
                    var circle = m as CircleDiameterMeasurement;
                    if (circle != null) {
                        circle.Circle_Row = e.CenterRow;
                        circle.Circle_Col = e.CenterCol;
                        circle.Circle_Radius = e.Radius;
                        break;
                    }
                    var circleCtr = m as CircleCenterDistanceMeasurement; //260519 hbk Phase 31 CO-23.1-02
                    if (circleCtr != null) {
                        circleCtr.Circle_Row = e.CenterRow;
                        circleCtr.Circle_Col = e.CenterCol;
                        circleCtr.Circle_Radius = e.Radius;
                        break;
                    }
                }
            }
            else if (e.Shape == RoiShape.Polygon) {
                //260509 hbk Phase 20 — ?? expanded
                if (e.PolygonPoints != null) fai.PolygonPoints = e.PolygonPoints;
                else                         fai.PolygonPoints = "";
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
        //260503 hbk Phase 17 D-07 — Datum ROI Delete 시 3-button 모달 (단일 / 전체 / 취소). YesNoCancel 재사용 (PATTERNS gap #3 옵션 a).
        private void HalconViewer_RoiDeleteRequested(object sender, string roiId) {
            if (string.IsNullOrEmpty(roiId)) return;

            //260425 hbk Phase 13 D-A — Datum 분기 우선
            if (roiId.StartsWith("Datum.")) {
                DatumConfig datum;
                if (IsCurrentNodeDatum(out datum)) {
                    //260504 hbk Phase 17 hotfix#9 (Option B) — Delete 모달 단순화 (3-button → 2-button).
                    //  사유: 단일 ROI 삭제 후 Wizard 가 Step 1 부터 무조건 시작 → 잔존 ROI 도 다시 그려야 했음.
                    //  hotfix#9 의 Option A (Wizard skip-existing) 와 함께 깔끔한 워크플로우 구성:
                    //  Delete = 항상 전체 삭제 / 부분 수정은 Edit 모드 / 단일 재 그리기는 PropertyGrid 0 입력 → wizard 자동 skip.
                    var choice = CustomMessageBox.ShowConfirmation(
                        "ROI 삭제",
                        "현재 Datum 의 모든 ROI 를 삭제하시겠습니까?",
                        MessageBoxButton.OKCancel);
                    if (choice != MessageBoxResult.OK) return;
                    ClearAllDatumRoiFields(datum); //260504 hbk Phase 17 hotfix#9 (Option B) — 항상 전체 삭제
                    try { datum.RaisePropertyChanged(string.Empty); } catch { }
                    //260509 hbk Phase 20 — chained ?. expanded
                    if (mParentWindow != null && mParentWindow.inspectionList != null) mParentWindow.inspectionList.RefreshParamEditor();
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

            //260426 hbk Phase 14-01 — Move 자동 재티칭 회귀 진단 로그 (Phase 14-05 verify 시 PASS 면 제거 가능)
            if (e.RoiId.StartsWith("Datum.")) {
                Logging.PrintLog((int)ELogType.Trace,
                    "Datum ROI move: id=" + e.RoiId + " dr=" + e.DeltaRow + " dc=" + e.DeltaCol);
            }

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
                //260521 hbk Phase 31 UAT Test7 — CircleCenterDistance 이동 write-back 누락 수정.
                //  RoiGeometryChanged(리사이즈)는 L648 에서 처리하나 RoiMoveCompleted(이동)는 빠져
                //  이동 후 GetCurrentFAIRois 재렌더가 stale Circle_* 로 원복되던 결함.
                var circleCtr = m as CircleCenterDistanceMeasurement; //260521 hbk Phase 31 UAT Test7
                if (circleCtr != null) {
                    circleCtr.Circle_Row += e.DeltaRow; //260521 hbk Phase 31 UAT Test7
                    circleCtr.Circle_Col += e.DeltaCol; //260521 hbk Phase 31 UAT Test7
                    handledCircle = true; //260521 hbk Phase 31 UAT Test7
                    break; //260521 hbk Phase 31 UAT Test7
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
            //260509 hbk Phase 20 — chained ?. expanded
            if (mParentWindow != null && mParentWindow.inspectionList != null)
                datum = mParentWindow.inspectionList.SelectedParam as DatumConfig;
            else
                datum = null;
            return datum != null;
        }

        //260425 hbk Phase 13 D-01..D-04 — Datum ROI 이동 후 처리 (delta + 이중 신호 + 자동 재티칭 + 후보 publish)
        //260426 hbk Phase 14-01 D-03 — Move 자동 재티칭 미발동 회귀 fix: Dispatcher.BeginInvoke(Background) defer (Phase 13-07 Fix A 패턴)
        //260429 hbk Phase 16 D-13 — Auto-reteach off: ROI 이동 후 자동 InvokeTryTeachDatumForEdit 호출 삭제 (사용자가 btn_teachDatum 으로 수동 트리거)
        //260429 hbk Phase 16 D-14 — ROI 이동 후 LastTeachSucceeded 변경되지 않음 → 검출 원/center 시각화는 stale 데이터를 그대로 보여줌 (사용자가 mismatch 인지)
        private void HandleDatumRoiMove(DatumConfig datum, RoiMoveCompletedArgs e) {
            ApplyDatumRoiDelta(datum, e);
            try { datum.RaisePropertyChanged(string.Empty); } catch { }
            //260509 hbk Phase 20 — chained ?. expanded
            if (mParentWindow != null && mParentWindow.inspectionList != null) mParentWindow.inspectionList.RefreshParamEditor();
            halconViewer.SetDatumOverlay(datum, true);
            //260429 hbk Phase 16 D-13 — Dispatcher.BeginInvoke 자동 재티칭 블록 삭제 (CONTEXT D-13 verbatim).
            //  Phase 14-01 D-03 의 Background defer 패턴은 자동 재티칭이 fire 되어야만 의미 있음.
            //  본 phase 에서 자동 재티칭 자체를 제거하므로 PublishDatumRoiCandidates / UpdateDatumRefCoordsLabel 만 inline 호출.
            PublishDatumRoiCandidates(datum);
            //260425 hbk Phase 13 D-VIZ-06 — ROI 이동 후 좌표 라벨 갱신 (자동 재티칭 없이도 호출 — stale 좌표 표시)
            UpdateDatumRefCoordsLabel(datum);
        }

        //260426 hbk Phase 14-01 D-04 — Datum ROI resize 후처리 (HandleDatumRoiMove 5-step 패턴 동일, delta vs absolute 차이만)
        private void HandleDatumRoiResize(DatumConfig datum, RoiGeometryChangedArgs e) {
            if (datum == null || e == null) return;

            //260426 hbk Phase 14-01 — Circle 절대 좌표 직접 대입 (resize 는 delta 가 아닌 새 절대값)
            if (e.RoiId == "Datum.Circle" && e.Shape == RoiShape.Circle) {
                datum.CircleROI_Row = e.CenterRow;
                datum.CircleROI_Col = e.CenterCol;
                datum.CircleROI_Radius = e.Radius;
            }
            //260427 hbk Phase 14 WR-01 — Rect resize 는 write-back 미구현 (Phase 14 scope 외).
            //  silent fall-through 방지: 명시적 log + early return 으로 stale geometry 자동 재티칭 차단.
            //  향후 Vertical/Horizontal Edit 핸들 노출 시 여기에 write-back 구현 후 return 제거.
            else if (e.Shape == RoiShape.Rect) {
                Logging.PrintLog((int)ELogType.Trace,
                    "Datum Rect resize ignored (Phase 14 scope): id=" + e.RoiId);
                return;
            }

            //260426 hbk Phase 14-01 — write-back 후 이중 신호 (HandleDatumRoiMove 패턴)
            try { datum.RaisePropertyChanged(string.Empty); } catch { }
            //260509 hbk Phase 20 — chained ?. expanded
            if (mParentWindow != null && mParentWindow.inspectionList != null) mParentWindow.inspectionList.RefreshParamEditor();
            halconViewer.SetDatumOverlay(datum, true);

            //260429 hbk Phase 16 D-13 — Dispatcher.BeginInvoke 자동 재티칭 블록 삭제 (CONTEXT D-13 verbatim).
            //260429 hbk Phase 16 D-14 — ROI resize 후 LastTeachSucceeded 변경되지 않음 (stale 시각화 의도적)
            PublishDatumRoiCandidates(datum);
            UpdateDatumRefCoordsLabel(datum);
        }

        //260425 hbk Phase 13 D-02 — RoiId prefix 별 DatumConfig 필드 매핑 (delta 누적)
        private void ApplyDatumRoiDelta(DatumConfig datum, RoiMoveCompletedArgs e) {
            if (datum == null || e == null || string.IsNullOrEmpty(e.RoiId)) return;
            switch (e.RoiId) {
                case "Datum.Line1":
                    datum.Line1_Row += e.DeltaRow; datum.Line1_Col += e.DeltaCol; break;
                case "Datum.Line2":
                    datum.Line2_Row += e.DeltaRow; datum.Line2_Col += e.DeltaCol; break;
                //260426 hbk Phase 14-03 Req 3 — Vertical ROI 이동 (Line1_* 슬롯 종료)
                case "Datum.Vertical":
                    datum.Vertical_Row += e.DeltaRow; datum.Vertical_Col += e.DeltaCol; break;
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
                //260426 hbk Phase 14-03 Req 3 — Vertical ROI 클리어
                case "Datum.Vertical":
                    datum.Vertical_Row = 0; datum.Vertical_Col = 0; datum.Vertical_Phi = 0;
                    datum.Vertical_Length1 = 0; datum.Vertical_Length2 = 0;
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

        //260503 hbk Phase 17 D-07 — 현재 Datum 의 모든 ROI 필드 0 리셋 (Delete 모달 [아니오] 분기)
        //  ClearDatumRoiFields 의 6 RoiId 분기를 모두 호출. IsConfigured/LastTeachSucceeded 는 마지막 호출에서 false 로 set.
        private void ClearAllDatumRoiFields(DatumConfig datum) {
            if (datum == null) return;
            ClearDatumRoiFields(datum, "Datum.Line1");
            ClearDatumRoiFields(datum, "Datum.Line2");
            ClearDatumRoiFields(datum, "Datum.Vertical");
            ClearDatumRoiFields(datum, "Datum.Circle");
            ClearDatumRoiFields(datum, "Datum.HorizontalA");
            ClearDatumRoiFields(datum, "Datum.HorizontalB");
        }

        //260503 hbk Phase 17 D-11 — btn_teachDatum 호환성 가드: 새 알고리즘이 요구하는 ROI 슬롯 비어 있으면 친절한 한국어 에러 (UI-SPEC Copywriting Contract)
        //  반환 null = OK. 비어 있으면 사용자에게 표시할 메시지 반환.
        private static string ValidateRoiPresence(DatumConfig d, EDatumAlgorithm alg) {
            if (d == null) return null;
            switch (alg) {
                case EDatumAlgorithm.TwoLineIntersect:
                    if (d.Line1_Length1 <= 0 || d.Line2_Length1 <= 0)
                        return "Line1/Line2 ROI 가 없습니다. 캔버스에 ROI 를 그리고 다시 시도하세요.";
                    break;
                case EDatumAlgorithm.CircleTwoHorizontal:
                    if (d.CircleROI_Radius <= 0)
                        return "Circle ROI 가 없습니다. 캔버스에 원을 그리고 다시 시도하세요.";
                    if (d.Horizontal_A_Length1 <= 0 || d.Horizontal_B_Length1 <= 0)
                        return "Horizontal A/B ROI 가 없습니다. 캔버스에 ROI 를 그리고 다시 시도하세요.";
                    break;
                case EDatumAlgorithm.VerticalTwoHorizontal:
                    if (d.Vertical_Length1 <= 0)
                        return "Vertical ROI 가 없습니다. 캔버스에 수직 ROI 를 그리고 다시 시도하세요.";
                    if (d.Horizontal_A_Length1 <= 0 || d.Horizontal_B_Length1 <= 0)
                        return "Horizontal A/B ROI 가 없습니다. 캔버스에 ROI 를 그리고 다시 시도하세요.";
                    break;
                //260527 hbk Phase 34 D-34-09/10 — DualImage 변형 가드: 3 ROI + 2 이미지 경로 모두 검증.
                case EDatumAlgorithm.VerticalTwoHorizontalDualImage:
                    if (d.Vertical_Length1 <= 0) //260527 hbk Phase 34
                        return "Vertical ROI 가 없습니다. 캔버스에 수직 ROI 를 그리고 다시 시도하세요."; //260527 hbk Phase 34
                    if (d.Horizontal_A_Length1 <= 0 || d.Horizontal_B_Length1 <= 0) //260527 hbk Phase 34
                        return "Horizontal A/B ROI 가 없습니다. 캔버스에 ROI 를 그리고 다시 시도하세요."; //260527 hbk Phase 34
                    if (string.IsNullOrEmpty(d.TeachingImagePath)) //260527 hbk Phase 34 D-34-10
                        return "가로축 티칭 이미지 경로가 비어 있습니다. Datum 노드에서 가로축 이미지를 Load 해주세요."; //260527 hbk Phase 34 D-34-10
                    if (string.IsNullOrEmpty(d.TeachingImagePath_Vertical)) //260527 hbk Phase 34 D-34-10
                        return "세로축 티칭 이미지 경로가 비어 있습니다. Datum 노드에서 세로축 이미지를 Load 해주세요."; //260527 hbk Phase 34 D-34-10
                    break;
            }
            return null;
        }

        //260505 hbk Phase 18 CO-06 — datum 인자 추가 → 에러 메시지에 [DatumName] 접두사 포함 (D-17)
        private static string FormatTeachError(DatumConfig datum, string err) { //260505 hbk Phase 18 CO-06
            if (err == null) err = "unknown"; //260505 hbk Phase 18 CO-06
            //260509 hbk Phase 20 — ternary expanded; Phase 18 CO-06 [DatumName] 접두사 의도 보존
            string prefix;
            if (datum != null && !string.IsNullOrEmpty(datum.DatumName)) prefix = "[" + datum.DatumName + "] ";
            else                                                         prefix = "";
            if (err.IndexOf("no edges", System.StringComparison.OrdinalIgnoreCase) >= 0 //260505 hbk Phase 18 CO-06
                || err.IndexOf("insufficient edges", System.StringComparison.OrdinalIgnoreCase) >= 0 //260505 hbk Phase 18 CO-06
                || err.IndexOf("insufficient polar samples", System.StringComparison.OrdinalIgnoreCase) >= 0) { //260505 hbk Phase 18 CO-06
                return prefix + "검출된 에지가 없습니다. EdgeDirection 설정을 반대로 변경한 후 다시 시도하세요."; //260505 hbk Phase 18 CO-06
            } //260505 hbk Phase 18 CO-06
            return prefix + "티칭에 실패했습니다: " + err; //260505 hbk Phase 18 CO-06
        } //260505 hbk Phase 18 CO-06

        //260503 hbk Phase 17 D-12 + D-04 — Test Find 실패 사유 모달 메시지 변환. 검출 0개 케이스에 EdgeDirection 힌트 통합.
        private static string FormatFindError(string err) {
            if (err == null) err = "unknown";
            if (err.IndexOf("no edges", System.StringComparison.OrdinalIgnoreCase) >= 0
                || err.IndexOf("insufficient edges", System.StringComparison.OrdinalIgnoreCase) >= 0
                || err.IndexOf("insufficient polar samples", System.StringComparison.OrdinalIgnoreCase) >= 0) {
                return "검출된 에지가 없습니다. EdgeDirection 설정을 반대로 변경한 후 다시 시도하세요."; //260503 hbk Phase 17 D-04 — EdgeDirection 힌트
            }
            return "Datum Find 에 실패했습니다: " + err;
        }

        //260426 hbk Phase 13-06 — UAT Test 6 (minor) gap closure: PropertyGrid 파라미터 변경 → 자동 재티칭 트리거
        //260426 hbk Phase 13-07 — UAT Test D recovery fix: LastTeachSucceeded 가드 제거 (IsConfigured만 유지)
        //260429 hbk Phase 16 D-12/D-13 — Auto-reteach off (수동 트리거 일원화):
        //  PropertyGrid 파라미터 변경 (EdgeDirection / EdgePolarity / AlgorithmType / RectL1Ratio 등) 시 자동 재티칭 안 함.
        //  사용자가 btn_teachDatum 수동 클릭해야만 검출 갱신. 시그니처는 호출처 (InspectionListView.TryTriggerDatumAutoReteach 등) 회귀 방지로 보존.
        public void NotifyDatumParamMaybeChanged(DatumConfig datum) {
            //260429 hbk Phase 16 D-12/D-13 — noop: 자동 재티칭 정책 폐지 (CONTEXT D-13 verbatim "단순화 우선")
            return;
        }

        //260425 hbk Phase 13 D-03 — Edit 세션 전용 자동 재티칭 (_editingDatum 건드리지 않음)
        //260426 hbk Phase 14-01 — 진단: 자동 재티칭 진입/종료 로깅 (Move 회귀 추적)
        private void InvokeTryTeachDatumForEdit(DatumConfig datum) {
            if (datum == null) return;
            Logging.PrintLog((int)ELogType.Trace, "InvokeTryTeachDatumForEdit ENTRY: IsConfigured=" + datum.IsConfigured);
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
                //260509 hbk Phase 20 — ?? expanded
                string errMsg;
                if (error != null) errMsg = error;
                else               errMsg = "unknown";
                label_drawHint.Content = "Datum ROI 이동 — 재티칭 실패: " + errMsg;
                label_drawHint.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFF87171"));
                label_drawHint.Visibility = Visibility.Visible;
            }
            halconViewer.SetDatumOverlay(datum, true);
            Logging.PrintLog((int)ELogType.Trace, "InvokeTryTeachDatumForEdit EXIT: LastTeachSucceeded=" + datum.LastTeachSucceeded);
        }

        //260425 hbk Phase 13 D-VIZ-06 — Datum reference 좌표 텍스트 갱신
        //  IsConfigured && LastTeachSucceeded 시 RefOrigin + Angle (+ CircleCenter/Radius) 표시.
        //  null 또는 미설정 시 회색 'Datum 미설정'.
        //  호출 3 지점: InspectionListView Datum 노드 선택 (PublishDatumRoiCandidates 내부) /
        //              InvokeTryTeachDatum 성공 분기 / HandleDatumRoiMove 말미.
        private void UpdateDatumRefCoordsLabel(DatumConfig datum) {
            if (label_datumRefCoords == null) return;
            if (datum == null) {
                label_datumRefCoords.Visibility = Visibility.Collapsed;
                return;
            }
            if (!datum.IsConfigured || !datum.LastTeachSucceeded) {
                label_datumRefCoords.Content = "Datum 미설정";
                label_datumRefCoords.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF888888"));
                label_datumRefCoords.Visibility = Visibility.Visible;
                return;
            }
            double angleDeg = datum.RefAngleRad * 180.0 / Math.PI;
            string text = "RefOrigin = (R: " + datum.RefOriginRow.ToString("F1")
                        + ", C: " + datum.RefOriginCol.ToString("F1")
                        + "), Angle = " + angleDeg.ToString("F2") + " deg";
            if (datum.AlgorithmTypeEnum == EDatumAlgorithm.CircleTwoHorizontal && datum.CircleDetected_Radius > 0) {
                text += "  |  CircleCenter = (R: " + datum.CircleCenter_Row.ToString("F1")
                      + ", C: " + datum.CircleCenter_Col.ToString("F1")
                      + "), Radius = " + datum.CircleDetected_Radius.ToString("F2");
            }
            label_datumRefCoords.Content = text;
            label_datumRefCoords.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFCBD5E0"));
            label_datumRefCoords.Visibility = Visibility.Visible;
        }

        //260519 hbk Phase 31 CO-23.1-01 — 이미지 출처 레이블 갱신
        //  datumConfig != null 이면 티칭 이미지(TeachingImagePath) 표시,
        //  shotConfig != null 이면 검사 이미지(SimulImagePath) 표시.
        //  둘 다 null 이거나 경로가 빈 문자열이면 레이블 Collapsed.
        //  T-31-12 mitigation: 경로 노출은 로컬 운영자 화면 전용 — File.Exists 가드 불필요 (레이블 표시 only).
        private void UpdateImageSourceLabel(DatumConfig datumConfig, ShotConfig shotConfig) {
            if (txt_imageSourceLabel == null) return;
            if (datumConfig != null && !string.IsNullOrEmpty(datumConfig.TeachingImagePath)) {
                txt_imageSourceLabel.Text = "티칭 이미지: " + datumConfig.TeachingImagePath; //260519 hbk Phase 31 CO-23.1-01
                txt_imageSourceLabel.Visibility = Visibility.Visible;
                return;
            }
            if (shotConfig != null && !string.IsNullOrEmpty(shotConfig.SimulImagePath)) {
                txt_imageSourceLabel.Text = "검사 이미지: " + shotConfig.SimulImagePath; //260519 hbk Phase 31 CO-23.1-01
                txt_imageSourceLabel.Visibility = Visibility.Visible;
                return;
            }
            txt_imageSourceLabel.Text = string.Empty; //260519 hbk Phase 31 CO-23.1-01
            txt_imageSourceLabel.Visibility = Visibility.Collapsed;
        }

        //260527 hbk Phase 34.1 D-34.1-15 — 자동/수동 swap 의 단일 진입점.
        //  3자 동시 갱신: (a) _currentImageSource 필드 + (b) 배지 텍스트/색상 + (c) ROI 가시성 (PublishDatumRoiCandidates 재호출 → 현재 축 ROI subset 만 표시).
        //  자동 swap (StartDatumTeachStep(Vertical) at L1994~) + 수동 swap (BtnSwap*_Click) 모두 본 메서드 경유.
        private void UpdateImageSourceBadge(ReringProject.Sequence.EImageSource source) {
            _currentImageSource = source;

            // (b) 배지 텍스트 + 색상 — D-34.1-14 잠금값
            //260527 hbk Phase 34.1 CO-34.1-03 hotfix — 정적 frozen brush + SetCurrentValue 로 WPF 갱신 보장.
            if (border_imageSourceBadge != null && txt_imageSourceBadge != null) {
                if (source == ReringProject.Sequence.EImageSource.Horizontal) {
                    txt_imageSourceBadge.SetCurrentValue(TextBlock.TextProperty, "가로축"); //260527 hbk Phase 34.1 D-34.1-14
                    border_imageSourceBadge.SetCurrentValue(Border.BackgroundProperty, BadgeBrushHorizontal); //260527 hbk Phase 34.1 CO-34.1-03
                }
                else {
                    txt_imageSourceBadge.SetCurrentValue(TextBlock.TextProperty, "세로축"); //260527 hbk Phase 34.1 D-34.1-14
                    border_imageSourceBadge.SetCurrentValue(Border.BackgroundProperty, BadgeBrushVertical); //260527 hbk Phase 34.1 CO-34.1-03
                }
                border_imageSourceBadge.InvalidateVisual(); //260527 hbk Phase 34.1 CO-34.1-03 — 즉시 재렌더 강제
            }

            // 토글 버튼 IsChecked 동기화 (수동 클릭 / 자동 swap 양방향) — 한쪽만 체크 (radio 패턴)
            if (btn_swapHorizontal != null) btn_swapHorizontal.IsChecked = (source == ReringProject.Sequence.EImageSource.Horizontal); //260527 hbk Phase 34.1
            if (btn_swapVertical   != null) btn_swapVertical.IsChecked   = (source == ReringProject.Sequence.EImageSource.Vertical);   //260527 hbk Phase 34.1

            // (c) ROI 가시성 — 현재 선택된 datum 이 DualImage 일 때만 subset 필터 적용. 다른 algorithm 은 PublishDatumRoiCandidates 가 알아서 처리.
            //260527 hbk Phase 34.1 CO-34.1-02 hotfix BUG-B — _editingDatum → _selectedDatumForSwap 교체.
            var datumForRoi = _selectedDatumForSwap; //260527 hbk Phase 34.1 CO-34.1-02
            if (datumForRoi != null
                && datumForRoi.AlgorithmTypeEnum == EDatumAlgorithm.VerticalTwoHorizontalDualImage) { //260527 hbk Phase 34.1 D-34.1-10
                PublishDatumRoiCandidates(datumForRoi); //260527 hbk Phase 34.1 — DualImage 분기가 _currentImageSource 참조하여 subset 만 publish
            }
        }

        //260527 hbk Phase 34.1 CO-34.1-02 hotfix — 보조 hook (DatumName 등 명시적 RaisePropertyChanged 가 fire 하는 property 대응).
        //  AlgorithmType 은 DatumConfig L76 의 auto property 라 PropertyChanged 미발동 — 본 hook 으로는 못 잡음.
        //  AlgorithmType 변경 시 Visibility 갱신은 InspectionListView.OnParamEditorSelectionChanged 의 whitelist 확장으로 처리 (별도 hotfix).
        //  본 메서드는 RaisePropertyChanged(string.Empty) 대량 갱신 시점에 ROI/Visibility 정합성 보장하는 보호망.
        private void OnSelectedDatumPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e) {
            if (e == null) return;
            var datum = sender as DatumConfig;
            if (datum == null) return;
            //260527 hbk Phase 34.1 CO-34.1-02 — name 비교 없이 무차별 재호출 시 무한루프 우려 → string.Empty (bulk) 또는 "AlgorithmType" 만 통과
            if (!string.IsNullOrEmpty(e.PropertyName) && e.PropertyName != "AlgorithmType") return;
            PublishDatumRoiCandidates(datum);
        }

        //260527 hbk Phase 34.1 D-34.1-02 — 가로축 토글 버튼 Click. 가로축 이미지로 swap + 배지/ROI 갱신.
        //260527 hbk Phase 34.1 CO-34.1-02 hotfix BUG-B — _editingDatum → _selectedDatumForSwap 교체 (teach 모드 진입 전에도 동작).
        private void BtnSwapHorizontal_Click(object sender, RoutedEventArgs e) {
            var d = _selectedDatumForSwap; //260527 hbk Phase 34.1 CO-34.1-02
            if (d == null) return;
            if (d.AlgorithmTypeEnum != EDatumAlgorithm.VerticalTwoHorizontalDualImage) return; //260527 hbk Phase 34.1 D-34.1-09 — DualImage 외 케이스 가드
            string hpath = d.TeachingImagePath;
            if (!string.IsNullOrEmpty(hpath) && System.IO.File.Exists(hpath)) {
                try { halconViewer.LoadImage(hpath); } //260527 hbk Phase 34.1 — 자동 swap (L1994~) 와 동일 경로
                catch (Exception ex) { Logging.PrintErrLog((int)ELogType.Error, ex.Message); }
            }
            UpdateImageSourceBadge(ReringProject.Sequence.EImageSource.Horizontal); //260527 hbk Phase 34.1 D-34.1-15 — 3자 동시 갱신
            //260528 hbk Phase 36 D-36-09 — Test Find 결과 양쪽 캔버스에 일관 렌더. swap 직후 RenderDatumFindResult 가 LastFindSucceeded gate 안에서 자동 재실행 (chain: SetDatumOverlay → RenderDatumOverlay → RenderDatumFindResult).
            halconViewer.SetDatumOverlay(_selectedDatumForSwap, true); //260528 hbk Phase 36 D-36-09
        }

        //260527 hbk Phase 34.1 D-34.1-02 — 세로축 토글 버튼 Click. 세로축 이미지로 swap + 배지/ROI 갱신.
        //260527 hbk Phase 34.1 CO-34.1-02 hotfix BUG-B — _editingDatum → _selectedDatumForSwap 교체.
        private void BtnSwapVertical_Click(object sender, RoutedEventArgs e) {
            var d = _selectedDatumForSwap; //260527 hbk Phase 34.1 CO-34.1-02
            if (d == null) return;
            if (d.AlgorithmTypeEnum != EDatumAlgorithm.VerticalTwoHorizontalDualImage) return; //260527 hbk Phase 34.1 D-34.1-09
            string vpath = d.TeachingImagePath_Vertical;
            if (!string.IsNullOrEmpty(vpath) && System.IO.File.Exists(vpath)) {
                try { halconViewer.LoadImage(vpath); }
                catch (Exception ex) { Logging.PrintErrLog((int)ELogType.Error, ex.Message); }
            }
            UpdateImageSourceBadge(ReringProject.Sequence.EImageSource.Vertical); //260527 hbk Phase 34.1 D-34.1-15
            //260528 hbk Phase 36 D-36-09 — Vertical 토글 시에도 동일 chain 트리거 (SameFrame 가정 하 양쪽 캔버스 동일 좌표).
            halconViewer.SetDatumOverlay(_selectedDatumForSwap, true); //260528 hbk Phase 36 D-36-09
        }

        //260425 hbk Phase 13 D-02 — DatumConfig → RoiDefinition 리스트 → halconViewer.SetDatumRoiCandidates publish
        //260426 hbk Phase 13 D-A1 — InspectionListView 가 selection 시 호출하도록 public 승격
        public void PublishDatumRoiCandidates(DatumConfig datum) {
            //260425 hbk Phase 13 D-VIZ-06 — selection 시점에 reference 좌표 라벨도 동기 갱신
            UpdateDatumRefCoordsLabel(datum);

            //260527 hbk Phase 34.1 CO-34.1-02 hotfix BUG-A/B — datum reference 캐싱 + AlgorithmType PropertyChanged 구독.
            //  swap 토글 핸들러가 teach 모드 전에도 동작하려면 별도 reference 필요 (_editingDatum 은 Teach Datum 클릭 시에만 set).
            //  AlgorithmType 이 PropertyGrid 에서 변경되면 PropertyChanged 가 fire → 본 메서드 재귀 호출 → Visibility 즉시 갱신.
            DatumConfig priorSelected = _selectedDatumForSwap; //260527 hbk Phase 34.1 CO-34.1-02 — D-34.1-08 노드 변경 감지용 prior reference
            if (_selectedDatumForSwap != datum) {
                if (_selectedDatumForSwap != null) _selectedDatumForSwap.PropertyChanged -= OnSelectedDatumPropertyChanged; //260527 hbk Phase 34.1 CO-34.1-02
                _selectedDatumForSwap = datum; //260527 hbk Phase 34.1 CO-34.1-02
                if (_selectedDatumForSwap != null) _selectedDatumForSwap.PropertyChanged += OnSelectedDatumPropertyChanged; //260527 hbk Phase 34.1 CO-34.1-02
            }

            //260527 hbk Phase 34.1 D-34.1-08/09 — Datum 노드 (재)선택 시: swap 상태 = 기본 가로축 리셋 (세션 한정).
            //  Visibility 동기화: DualImage 변형이면 토글 버튼 + 배지 Visible, 1-image 변형 / null 이면 Collapsed.
            bool isDualImage = (datum != null //260527 hbk Phase 34.1 D-34.1-09
                                && datum.AlgorithmTypeEnum == EDatumAlgorithm.VerticalTwoHorizontalDualImage);
            if (btn_swapHorizontal != null) btn_swapHorizontal.Visibility = isDualImage ? Visibility.Visible : Visibility.Collapsed; //260527 hbk Phase 34.1 D-34.1-09
            if (btn_swapVertical   != null) btn_swapVertical.Visibility   = isDualImage ? Visibility.Visible : Visibility.Collapsed; //260527 hbk Phase 34.1 D-34.1-09
            if (border_imageSourceBadge != null) border_imageSourceBadge.Visibility = isDualImage ? Visibility.Visible : Visibility.Collapsed; //260527 hbk Phase 34.1 D-34.1-09

            if (isDualImage) {
                // Datum 노드 선택 직후 = 가로축 기본 (D-34.1-08). 단, 본 메서드가 자동/수동 swap 도중 재호출되면 _currentImageSource 가 이미 변경되어 있을 수 있음.
                // 진입점 구분: priorSelected != datum 이면 새 노드 선택 → 리셋. 같으면 swap 진행 중 / AlgorithmType 변경 → 보존.
                //260527 hbk Phase 34.1 CO-34.1-02 hotfix — _editingDatum (teach 모드 한정) → priorSelected (swap UI selection) 교체.
                if (priorSelected != datum) { //260527 hbk Phase 34.1 D-34.1-08 + CO-34.1-02 — 새 노드 진입만 리셋
                    _currentImageSource = ReringProject.Sequence.EImageSource.Horizontal;
                    // 배지 텍스트/색상도 가로축으로 동기 (별도 UpdateImageSourceBadge 호출 없이 직접 — 재귀 회피)
                    //260527 hbk Phase 34.1 CO-34.1-03 hotfix — 정적 frozen brush + SetCurrentValue
                    if (txt_imageSourceBadge != null) txt_imageSourceBadge.SetCurrentValue(TextBlock.TextProperty, "가로축");
                    if (border_imageSourceBadge != null) border_imageSourceBadge.SetCurrentValue(Border.BackgroundProperty, BadgeBrushHorizontal); //260527 hbk Phase 34.1 CO-34.1-03
                    if (btn_swapHorizontal != null) btn_swapHorizontal.IsChecked = true;
                    if (btn_swapVertical   != null) btn_swapVertical.IsChecked   = false;
                }
            }

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
                    //260426 hbk Phase 14-03 Req 3 — Line1_* → Vertical_* 슬롯 교체 (RoiId 도 Datum.Vertical 로)
                    if (datum.Vertical_Length1 > 0 && datum.Vertical_Length2 > 0)
                        list.Add(BuildDatumRectCandidate("Datum.Vertical", datum.Vertical_Row, datum.Vertical_Col, datum.Vertical_Length1, datum.Vertical_Length2));
                    if (datum.Horizontal_A_Length1 > 0 && datum.Horizontal_A_Length2 > 0)
                        list.Add(BuildDatumRectCandidate("Datum.HorizontalA", datum.Horizontal_A_Row, datum.Horizontal_A_Col, datum.Horizontal_A_Length1, datum.Horizontal_A_Length2));
                    if (datum.Horizontal_B_Length1 > 0 && datum.Horizontal_B_Length2 > 0)
                        list.Add(BuildDatumRectCandidate("Datum.HorizontalB", datum.Horizontal_B_Row, datum.Horizontal_B_Col, datum.Horizontal_B_Length1, datum.Horizontal_B_Length2));
                    break;
                //260527 hbk Phase 34.1 D-34.1-10 — DualImage 변형 ROI publish.
                //  당초 설계: 축별 subset 토글 (가로축 표시 시 HA+HB 만, 세로축 표시 시 Vertical 만).
                //260527 hbk Phase 34.1 CO-34.1-05 hotfix — UAT 결과 subset 토글이 ROI 위치 파악/삭제/이동 시 사용성 저해.
                //  설계 변경: VerticalTwoHorizontal (1-image) 와 동일하게 모든 ROI 항상 표시. 사용자가 다른 축으로 swap 해도 기존 ROI 위치 보존되어 편집 가능.
                //  좌표계 불일치 (가로 이미지 위에 Vertical ROI 표시 시 misalign 가능) 는 SIMUL 의사 페어 한계로 CO-34.1-01 에서 종결.
                case EDatumAlgorithm.VerticalTwoHorizontalDualImage:
                    if (datum.Horizontal_A_Length1 > 0 && datum.Horizontal_A_Length2 > 0) //260527 hbk Phase 34.1 CO-34.1-05
                        list.Add(BuildDatumRectCandidate("Datum.HorizontalA", datum.Horizontal_A_Row, datum.Horizontal_A_Col, datum.Horizontal_A_Length1, datum.Horizontal_A_Length2));
                    if (datum.Horizontal_B_Length1 > 0 && datum.Horizontal_B_Length2 > 0) //260527 hbk Phase 34.1 CO-34.1-05
                        list.Add(BuildDatumRectCandidate("Datum.HorizontalB", datum.Horizontal_B_Row, datum.Horizontal_B_Col, datum.Horizontal_B_Length1, datum.Horizontal_B_Length2));
                    if (datum.Vertical_Length1 > 0 && datum.Vertical_Length2 > 0) //260527 hbk Phase 34.1 CO-34.1-05
                        list.Add(BuildDatumRectCandidate("Datum.Vertical", datum.Vertical_Row, datum.Vertical_Col, datum.Vertical_Length1, datum.Vertical_Length2));
                    break; //260527 hbk Phase 34.1
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
                //260509 hbk Phase 20 — ternary expanded
                string roiName;
                if (string.IsNullOrWhiteSpace(name)) roiName = "Rect " + i;
                else                                 roiName = name;
                rois.Add(new RoiDefinition {
                    Id = "Rect_" + i,
                    Name = roiName,
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
            //260509 hbk Phase 20 — 3 ternaries expanded
            int roiCount;
            if (rois == null) roiCount = 0;
            else              roiCount = rois.Count();
            int overlayCount;
            if (overlays == null) overlayCount = 0;
            else                  overlayCount = overlays.Count();
            string imgLabel;
            if (string.IsNullOrWhiteSpace(imagePath)) imgLabel = "null";
            else                                      imgLabel = Path.GetFileName(imagePath);
            return string.Format(
                "IMG:{0} | ROI:{1} | OVR:{2}",
                imgLabel,
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
            //260517 hbk Phase 23.1 D-01 — Rect ROI 편집 대상 Measurement 해제
            _editingMeasurement = null;
            _editingMeasurementFaiName = null;
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

                //260517 hbk Phase 23.1 D-01 — Measurement 노드 선택 시 EdgeToLineDistanceMeasurement 대상 분기 (FAIConfig 해석보다 우선)
                //260519 hbk Phase 31 CO-23.1-02 — FindSelectedRectMeasurement 로 교체 (Point_* 보유 타입 화이트리스트)
                MeasurementBase measTarget = FindSelectedRectMeasurement();
                if (measTarget != null) {
                    _editingMeasurement = measTarget;
                    _editingMeasurementRoiIndex = 0; //260521 hbk Phase 32 — 인덱스 초기화 (이전 미완료 티칭 잔존 인덱스 오염 방지, T-32-10)
                    var selRowForMeas = dataGrid_faiResults.SelectedItem as MeasurementResultRow;
                    if (selRowForMeas != null) _editingMeasurementFaiName = selRowForMeas.FAIName;
                    else _editingMeasurementFaiName = FindFaiNameContainingMeasurement(_editingMeasurement);
                    //260521 hbk Phase 32 I9/I10-redesign — ArcLineIntersect 순차 4-ROI UX: 첫 드로잉은 교점1 EdgeA1(수직 에지)
                    if (measTarget is ArcLineIntersectDistanceMeasurement) //260521 hbk Phase 32 I9/I10-redesign
                        label_drawHint.Content = "교점1 수직 에지(EdgeA1) ROI 를 드래그하세요"; //260521 hbk Phase 32 I9/I10-redesign
                    else //260521 hbk Phase 32
                        label_drawHint.Content = "드래그하여 Measurement Point ROI를 설정하세요";
                    label_drawHint.Visibility = Visibility.Visible;
                    halconViewer.RectDrawingCompleted += HalconViewer_RectDrawingCompleted;
                    halconViewer.StartRectangleDrawing();
                    return;
                }

                //260511 hbk 신규 FAI(Measurement 0개) 회귀 — 트리 선택을 우선 사용, dataGrid 는 fallback
                FAIConfig faiToEdit = null;
                if (mParentWindow != null && mParentWindow.inspectionList != null)
                    faiToEdit = mParentWindow.inspectionList.SelectedParam as FAIConfig;
                if (faiToEdit == null) {
                    //260511 hbk fallback — 기존 dataGrid 행 선택 경로 보존
                    var selectedRow = dataGrid_faiResults.SelectedItem as MeasurementResultRow;
                    if (selectedRow != null) faiToEdit = FindFAIByName(selectedRow.FAIName);
                }
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
            //260517 hbk Phase 23.1 D-01 — canvas mode 가드 (Measurement/FAI 공통)
            if (_canvasMode != ECanvasMode.RectRoi) {
                ExitCanvasMode();
                return;
            }

            //260517 hbk Phase 23.1 D-01 — Measurement 분기 우선: EdgeToLineDistanceMeasurement.Point_* write-back
            //260519 hbk Phase 31 CO-23.1-02 — MeasurementBase 타입 일반화, as 캐스트 분기로 Point_* 설정
            if (_editingMeasurement != null) {
                var measRoi = halconViewer.CommitActiveRectangle();
                if (measRoi != null) {
                    double mCenterRow = (measRoi.Row1 + measRoi.Row2) / 2.0;
                    double mCenterCol = (measRoi.Column1 + measRoi.Column2) / 2.0;
                    double mHalfHeight = (measRoi.Row2 - measRoi.Row1) / 2.0;
                    double mHalfWidth = (measRoi.Column2 - measRoi.Column1) / 2.0;
                    //260519 hbk Phase 31 CO-23.1-02 — 측정 타입별 Point ROI 기록 일반화
                    var etld = _editingMeasurement as EdgeToLineDistanceMeasurement;
                    if (etld != null) { etld.Point_Row = mCenterRow; etld.Point_Col = mCenterCol; etld.Point_Phi = 0.0; etld.Point_Length1 = mHalfHeight; etld.Point_Length2 = mHalfWidth; }
                    var etla = _editingMeasurement as EdgeToLineAngleMeasurement; //260519 hbk Phase 31 CO-23.1-02
                    if (etla != null) { etla.Point_Row = mCenterRow; etla.Point_Col = mCenterCol; etla.Point_Phi = 0.0; etla.Point_Length1 = mHalfHeight; etla.Point_Length2 = mHalfWidth; }
                    var aed = _editingMeasurement as ArcEdgeDistanceMeasurement; //260519 hbk Phase 31 CO-23.1-02
                    if (aed != null) { aed.Point_Row = mCenterRow; aed.Point_Col = mCenterCol; aed.Point_Phi = 0.0; aed.Point_Length1 = mHalfHeight; aed.Point_Length2 = mHalfWidth; }
                    //260521 hbk Phase 32 — Compound 4종 단일 Rect ROI write-back (Rect_* 필드명)
                    var cAngle = _editingMeasurement as CompoundAngleMeasurement; //260521 hbk Phase 32
                    if (cAngle != null) { cAngle.Rect_Row = mCenterRow; cAngle.Rect_Col = mCenterCol; cAngle.Rect_Phi = 0.0; cAngle.Rect_Length1 = mHalfHeight; cAngle.Rect_Length2 = mHalfWidth; }
                    var cCenterC = _editingMeasurement as CompoundCenterCDistanceMeasurement; //260521 hbk Phase 32
                    if (cCenterC != null) { cCenterC.Rect_Row = mCenterRow; cCenterC.Rect_Col = mCenterCol; cCenterC.Rect_Phi = 0.0; cCenterC.Rect_Length1 = mHalfHeight; cCenterC.Rect_Length2 = mHalfWidth; }
                    var cCenterB = _editingMeasurement as CompoundCenterBDistanceMeasurement; //260521 hbk Phase 32
                    if (cCenterB != null) { cCenterB.Rect_Row = mCenterRow; cCenterB.Rect_Col = mCenterCol; cCenterB.Rect_Phi = 0.0; cCenterB.Rect_Length1 = mHalfHeight; cCenterB.Rect_Length2 = mHalfWidth; }
                    var cShort = _editingMeasurement as CompoundShortAxisDistanceMeasurement; //260523 hbk Phase 32 — E3 단축 환원
                    if (cShort != null) { cShort.Rect_Row = mCenterRow; cShort.Rect_Col = mCenterCol; cShort.Rect_Phi = 0.0; cShort.Rect_Length1 = mHalfHeight; cShort.Rect_Length2 = mHalfWidth; }
                    //260521 hbk Phase 32 I9/I10-redesign — ArcLineIntersect 순차 4-ROI 드로잉: 인덱스 0=EdgeA1, 1=EdgeB1, 2=EdgeA2, 3=EdgeB2
                    var ali = _editingMeasurement as ArcLineIntersectDistanceMeasurement; //260521 hbk Phase 32 I9/I10-redesign
                    if (ali != null) {
                        if (_editingMeasurementRoiIndex == 0) {
                            ali.EdgeA1_Row = mCenterRow; ali.EdgeA1_Col = mCenterCol; ali.EdgeA1_Phi = 0.0;
                            ali.EdgeA1_Length1 = mHalfHeight; ali.EdgeA1_Length2 = mHalfWidth; //260521 hbk Phase 32 I9/I10-redesign
                            _editingMeasurementRoiIndex = 1; //260521 hbk Phase 32 I9/I10-redesign
                            label_drawHint.Content = "교점1 수평 에지(EdgeB1) ROI 를 드래그하세요"; //260521 hbk Phase 32 I9/I10-redesign
                            halconViewer.RectDrawingCompleted += HalconViewer_RectDrawingCompleted; //260521 hbk Phase 32 재무장
                            halconViewer.StartRectangleDrawing(); //260521 hbk Phase 32 I9/I10-redesign
                            return; //260521 hbk Phase 32 I9/I10-redesign — ExitCanvasMode 미호출
                        }
                        else if (_editingMeasurementRoiIndex == 1) {
                            ali.EdgeB1_Row = mCenterRow; ali.EdgeB1_Col = mCenterCol; ali.EdgeB1_Phi = 0.0;
                            ali.EdgeB1_Length1 = mHalfHeight; ali.EdgeB1_Length2 = mHalfWidth; //260521 hbk Phase 32 I9/I10-redesign
                            _editingMeasurementRoiIndex = 2; //260521 hbk Phase 32 I9/I10-redesign
                            label_drawHint.Content = "교점2 수직 에지(EdgeA2) ROI 를 드래그하세요"; //260521 hbk Phase 32 I9/I10-redesign
                            halconViewer.RectDrawingCompleted += HalconViewer_RectDrawingCompleted; //260521 hbk Phase 32 재무장
                            halconViewer.StartRectangleDrawing(); //260521 hbk Phase 32 I9/I10-redesign
                            return; //260521 hbk Phase 32 I9/I10-redesign — ExitCanvasMode 미호출
                        }
                        else if (_editingMeasurementRoiIndex == 2) {
                            ali.EdgeA2_Row = mCenterRow; ali.EdgeA2_Col = mCenterCol; ali.EdgeA2_Phi = 0.0;
                            ali.EdgeA2_Length1 = mHalfHeight; ali.EdgeA2_Length2 = mHalfWidth; //260521 hbk Phase 32 I9/I10-redesign
                            _editingMeasurementRoiIndex = 3; //260521 hbk Phase 32 I9/I10-redesign
                            label_drawHint.Content = "교점2 수평 에지(EdgeB2) ROI 를 드래그하세요"; //260521 hbk Phase 32 I9/I10-redesign
                            halconViewer.RectDrawingCompleted += HalconViewer_RectDrawingCompleted; //260521 hbk Phase 32 재무장
                            halconViewer.StartRectangleDrawing(); //260521 hbk Phase 32 I9/I10-redesign
                            return; //260521 hbk Phase 32 I9/I10-redesign — ExitCanvasMode 미호출
                        }
                        else { // index == 3: 마지막 ROI EdgeB2 — 정상 종결
                            ali.EdgeB2_Row = mCenterRow; ali.EdgeB2_Col = mCenterCol; ali.EdgeB2_Phi = 0.0;
                            ali.EdgeB2_Length1 = mHalfHeight; ali.EdgeB2_Length2 = mHalfWidth; //260521 hbk Phase 32 I9/I10-redesign
                        } //260521 hbk Phase 32 I9/I10-redesign — index 3: EdgeB2 기록 후 아래 ExitCanvasMode 로 종결
                    }
                    string measSelId = _editingMeasurementFaiName;
                    if (string.IsNullOrEmpty(measSelId))
                        measSelId = FindFaiNameContainingMeasurement(_editingMeasurement);
                    //260521 hbk Phase 32 UAT — DataGrid 비의존 Shot 단위 ROI 수집으로 교체 (Measurement 노드 선택 시 ClearResults() 가 DataGrid 를 비워 GetCurrentFAIRois() 가 빈 리스트 반환 → ROI 미표시 결함)
                    //  CollectShotRois 는 anchorFai.Owner(ShotConfig).FAIList 전체를 AppendFaiRois 로 수집 — DataGrid 무관 (Phase 31 #6-a 패턴 재사용)
                    FAIConfig anchorFaiForCommit = FindFAIByName(measSelId); //260521 hbk Phase 32 UAT
                    var measRois = CollectShotRois(anchorFaiForCommit); //260521 hbk Phase 32 UAT — GetCurrentFAIRois() 교체
                    halconViewer.UpdateDisplayState(measRois, measSelId, null, null);
                }
                ExitCanvasMode();
                return;
            }

            //260517 hbk Phase 23.1 D-01 — 기존 FAIConfig 경로 (무수정)
            if (_editingFai == null) {
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
                //260519 hbk Phase 31 CO-23.1-02 — 반환 타입 MeasurementBase 로 일반화 (CircleCenterDistance 포함)
                MeasurementBase target = FindSelectedCircleMeasurement();
                if (target == null) {
                    CustomMessageBox.Show("Circle ROI", "CircleDiameterMeasurement 또는 CircleCenterDistanceMeasurement를 포함한 FAI를 선택하세요.");
                    ExitCanvasMode();
                    return;
                }
                _editingCircleMeasurement = target;
                //260423 hbk Commit 시 selection id 를 FAIName 으로 맞추기 위해 캡처
                var selRowForCircle = dataGrid_faiResults.SelectedItem as MeasurementResultRow;
                //260509 hbk Phase 20 — ?. expanded
                if (selRowForCircle != null) _editingCircleFaiName = selRowForCircle.FAIName;
                else                         _editingCircleFaiName = null;

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
                    //260509 hbk Phase 20 — ?. expanded to explicit null-check
                    if (act != null && act.Param is ShotConfig shot) {
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

        //260528 hbk Phase 37 — FindFaiNameContainingMeasurement 의 참조 버전: 이름 충돌(여러 Shot 동일 FAI 명) 시에도
        //  실제 소유 FAIConfig 객체를 ReferenceEquals 로 정확히 반환. 이미지/ROI 해석이 첫 Shot 으로 잘못 묶이는 결함 차단.
        private FAIConfig FindFAIContainingMeasurement(MeasurementBase measurement) {
            if (measurement == null) return null;
            //260528 hbk Phase 37 — 우선 RecipeManager.Shots(동적 FAI 단일 소스, 신규 Shot 즉시 반영) 에서 탐색.
            //  AddShotToSequence 는 새 Shot 을 RecipeManager 에만 넣고 라이브 Action(pSeq) 은 실행 시 지연 동기화하므로,
            //  세션 중 pSeq 만 보면 신규 Shot 측정을 못 찾아 이미지/ROI 가 이전 Shot 으로 남는다(재시작 후엔 정상).
            var recipeManager = (SystemHandler.Handle != null && SystemHandler.Handle.Sequences != null)
                ? SystemHandler.Handle.Sequences.RecipeManager : null;
            if (recipeManager != null && recipeManager.Shots != null) {
                foreach (ShotConfig rmShot in recipeManager.Shots) {
                    if (rmShot == null) continue;
                    foreach (FAIConfig fai in rmShot.FAIList) {
                        foreach (var m in fai.Measurements) {
                            if (ReferenceEquals(m, measurement)) return fai;
                        }
                    }
                }
            }
            //260528 hbk Phase 37 — fallback: 레거시/로드 경로(측정이 pSeq Action 에만 존재할 수 있음)
            if (pSeq == null) return null;
            for (int i = 0; i < pSeq.Count; i++) {
                var seq = pSeq[i];
                if (seq == null) continue;
                for (int j = 0; j < seq.ActionCount; j++) {
                    var act = seq[j];
                    if (act != null && act.Param is ShotConfig shot) {
                        foreach (FAIConfig fai in shot.FAIList) {
                            foreach (var m in fai.Measurements) {
                                if (ReferenceEquals(m, measurement)) return fai;
                            }
                        }
                    }
                }
            }
            return null;
        }

        //260423 hbk Phase 11 D-17/D-18 — 선택된 FAI에서 CircleDiameterMeasurement 해석
        //260519 hbk Phase 31 CO-23.1-02 — 반환 타입 MeasurementBase 로 일반화, CircleCenterDistanceMeasurement 추가
        private MeasurementBase FindSelectedCircleMeasurement() {
            //260519 hbk Phase 31 CO-23.1-02 — 트리 노드 선택 우선 (FindSelectedRectMeasurement 와 대칭, Measurement 노드 직접 선택 케이스)
            if (mParentWindow != null && mParentWindow.inspectionList != null) {
                var selParam = mParentWindow.inspectionList.SelectedParam;
                if (selParam is CircleDiameterMeasurement) return (MeasurementBase)selParam; //260519 hbk Phase 31 CO-23.1-02
                if (selParam is CircleCenterDistanceMeasurement) return (MeasurementBase)selParam; //260519 hbk Phase 31 CO-23.1-02
            }
            // fallback — dataGrid 행 선택 경로
            var selectedRow = dataGrid_faiResults.SelectedItem as MeasurementResultRow;
            if (selectedRow != null) {
                FAIConfig fai = FindFAIByName(selectedRow.FAIName);
                if (fai != null) {
                    foreach (var m in fai.Measurements) {
                        var circle = m as CircleDiameterMeasurement;
                        if (circle != null) return circle;
                        var circleCtr = m as CircleCenterDistanceMeasurement; //260519 hbk Phase 31 CO-23.1-02
                        if (circleCtr != null) return circleCtr; //260519 hbk Phase 31 CO-23.1-02
                    }
                }
            }
            return null;
        }

        //260517 hbk Phase 23.1 D-01 — 선택된 트리/결과 행에서 EdgeToLineDistanceMeasurement 해석 (D-02: 이 타입만 대상)
        //260519 hbk Phase 31 CO-23.1-02 — FindSelectedRectMeasurement 로 일반화 (Point_* ROI 보유 타입 화이트리스트)
        private MeasurementBase FindSelectedRectMeasurement() {
            // 트리 노드 선택 우선 (FAI 미생성 신규 measurement 케이스 포함)
            if (mParentWindow != null && mParentWindow.inspectionList != null) {
                var selParam = mParentWindow.inspectionList.SelectedParam;
                if (selParam is EdgeToLineDistanceMeasurement) return (MeasurementBase)selParam;
                if (selParam is EdgeToLineAngleMeasurement) return (MeasurementBase)selParam; //260519 hbk Phase 31 CO-23.1-02
                if (selParam is ArcEdgeDistanceMeasurement) return (MeasurementBase)selParam; //260519 hbk Phase 31 CO-23.1-02
                if (selParam is ArcLineIntersectDistanceMeasurement) return (MeasurementBase)selParam; //260519 hbk Phase 31 CO-23.1-02
                if (selParam is CompoundAngleMeasurement) return (MeasurementBase)selParam; //260519 hbk Phase 31 CO-23.1-02
                if (selParam is CompoundCenterCDistanceMeasurement) return (MeasurementBase)selParam; //260519 hbk Phase 31 CO-23.1-02
                if (selParam is CompoundCenterBDistanceMeasurement) return (MeasurementBase)selParam; //260519 hbk Phase 31 CO-23.1-02
                if (selParam is CompoundShortAxisDistanceMeasurement) return (MeasurementBase)selParam; //260523 hbk Phase 32 — E3 단축 환원
            }
            // fallback — dataGrid 행 선택 경로
            var selectedRow = dataGrid_faiResults.SelectedItem as MeasurementResultRow;
            if (selectedRow != null) {
                FAIConfig fai = FindFAIByName(selectedRow.FAIName);
                if (fai != null) {
                    foreach (var m in fai.Measurements) {
                        if (m is EdgeToLineDistanceMeasurement) return m;
                        if (m is EdgeToLineAngleMeasurement) return m; //260519 hbk Phase 31 CO-23.1-02
                        if (m is ArcEdgeDistanceMeasurement) return m; //260519 hbk Phase 31 CO-23.1-02
                        if (m is ArcLineIntersectDistanceMeasurement) return m; //260519 hbk Phase 31 CO-23.1-02
                        if (m is CompoundAngleMeasurement) return m; //260519 hbk Phase 31 CO-23.1-02
                        if (m is CompoundCenterCDistanceMeasurement) return m; //260519 hbk Phase 31 CO-23.1-02
                        if (m is CompoundCenterBDistanceMeasurement) return m; //260519 hbk Phase 31 CO-23.1-02
                        if (m is CompoundShortAxisDistanceMeasurement) return m; //260523 hbk Phase 32 — E3 단축 환원
                    }
                }
            }
            return null;
        }

        //260423 hbk Phase 11 D-17 — Circle 드래그 결과를 Measurement에 기록
        //260519 hbk Phase 31 CO-23.1-02 — _editingCircleMeasurement 타입 MeasurementBase 로 일반화 (Circle_* as 분기)
        private void CommitCircleRoi(double centerRow, double centerCol, double radius) {
            if (_canvasMode != ECanvasMode.CircleRoi || _editingCircleMeasurement == null || radius <= 0) {
                ExitCanvasMode();
                return;
            }

            // D-17: write to the Measurement's own fields (authoritative for Halcon call)
            //260519 hbk Phase 31 CO-23.1-02 — 타입별 Circle_* 필드 설정 분기
            var circDiam = _editingCircleMeasurement as CircleDiameterMeasurement;
            if (circDiam != null) { circDiam.Circle_Row = centerRow; circDiam.Circle_Col = centerCol; circDiam.Circle_Radius = radius; }
            var circCtr = _editingCircleMeasurement as CircleCenterDistanceMeasurement; //260519 hbk Phase 31 CO-23.1-02
            if (circCtr != null) { circCtr.Circle_Row = centerRow; circCtr.Circle_Col = centerCol; circCtr.Circle_Radius = radius; }

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

                //260511 hbk 신규 FAI(Measurement 0개) 회귀 — 트리 선택을 우선 사용, dataGrid 는 fallback
                FAIConfig faiToEdit = null;
                if (mParentWindow != null && mParentWindow.inspectionList != null)
                    faiToEdit = mParentWindow.inspectionList.SelectedParam as FAIConfig;
                if (faiToEdit == null) {
                    //260511 hbk fallback — 기존 dataGrid 행 선택 경로 보존
                    var selectedRow = dataGrid_faiResults.SelectedItem as MeasurementResultRow;
                    if (selectedRow != null) faiToEdit = FindFAIByName(selectedRow.FAIName);
                }
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
            //260509 hbk Phase 20 — ternary expanded
            FAIConfig anchorFai;
            if (selectedRow != null) anchorFai = FindFAIByName(selectedRow.FAIName);
            else                     anchorFai = null;
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
                halconViewer.IsTeachDatumMode = true; //260505 hbk Phase 18 CO-04 — "ROI 다시 그리기" 메뉴 활성화

                //260424 hbk Phase 12 — InspectionListView.SelectedParam 으로 DatumConfig 해결 (btn_teachDatum 활성화 조건)
                //260509 hbk Phase 20 — chained ?. expanded
                DatumConfig datum;
                if (mParentWindow != null && mParentWindow.inspectionList != null) datum = mParentWindow.inspectionList.SelectedParam as DatumConfig;
                else                                                               datum = null;
                if (datum == null) {
                    CustomMessageBox.Show("Datum 노드를 먼저 선택하세요.", "Teach Datum");
                    ExitCanvasMode();
                    return;
                }
                _editingDatum = datum;
                //260425 hbk Phase 13 D-01..D-04 — teach 진입 시점에 후보 publish (이후 edit/delete 가능)
                PublishDatumRoiCandidates(datum);

                //260503 hbk Phase 17 D-11 — 새 알고리즘이 요구하는 ROI 슬롯 비어 있으면 친절한 에러 모달 (UI-SPEC Copywriting Contract)
                //260503 hbk Phase 17 bugfix#3 — IsConfigured=true 인 경우만 가드 적용 (알고리즘 전환 후 ROI 누락 케이스).
                //  IsConfigured=false 면 첫 티칭 → wizard(StartDatumTeachStep) 가 ROI 를 단계별로 그리도록 진행.
                //  UAT Test 10 (새 Datum + ROI 미생성 → 모달) 와 UI-SPEC § btn_teachDatum 상태머신 (Drawing 모드 ON) 충돌 →
                //  UI-SPEC 우선 (사용자 워크플로우 차단 회피). Test 10 은 carry-over.
                if (datum.IsConfigured) {
                    string missingRoiMsg = ValidateRoiPresence(datum, datum.AlgorithmTypeEnum);
                    if (missingRoiMsg != null) {
                        CustomMessageBox.Show("티칭 실패", missingRoiMsg); //260503 hbk Phase 17 D-11 — 한국어 친절한 에러
                        btn_teachDatum.IsChecked = false;
                        _canvasMode = ECanvasMode.None;
                        _editingDatum = null;
                        halconViewer.IsEditMode = false; //260503 hbk Phase 17 D-06 wiring — 티칭 미시작 시 Edit 모드 해제
                        halconViewer.IsTeachDatumMode = false; //260505 hbk Phase 18 CO-04 — 티칭 미시작 시 메뉴 숨김
                        return;
                    }
                    //260507 hbk Phase 18 18-07 — 재티칭 확인 모달: IsConfigured=true & 모든 ROI 존재 → 즉시 teach 시나리오에서 사용자 의사 확인.
                    //  Silent re-teach 방지 + 버튼 먹힘 시각 신호 제공. ValidateRoiPresence 가 null 통과한 시점이므로 모든 ROI 존재 보장.
                    var reteachChoice = CustomMessageBox.ShowConfirmation( //260507 hbk Phase 18 18-07
                        "재티칭 확인", //260507 hbk Phase 18 18-07
                        "이 Datum 은 이미 티칭되어 있습니다.\n기존 ROI 로 재티칭하시겠습니까?\n\n(ROI 를 다시 그리려면 먼저 삭제해 주세요.)", //260507 hbk Phase 18 18-07
                        MessageBoxButton.YesNo); //260507 hbk Phase 18 18-07
                    if (reteachChoice != MessageBoxResult.Yes) { //260507 hbk Phase 18 18-07
                        btn_teachDatum.IsChecked = false; //260507 hbk Phase 18 18-07
                        _canvasMode = ECanvasMode.None; //260507 hbk Phase 18 18-07
                        _editingDatum = null; //260507 hbk Phase 18 18-07
                        halconViewer.IsEditMode = false; //260507 hbk Phase 18 18-07
                        halconViewer.IsTeachDatumMode = false; //260507 hbk Phase 18 18-07
                        return; //260507 hbk Phase 18 18-07
                    } //260507 hbk Phase 18 18-07
                }

                //260503 hbk Phase 17 D-06 wiring — 티칭 모드 진입 시 Edit OFF (그리기 모드 → ROI hit-test 차단)
                halconViewer.IsEditMode = false;

                //260424 hbk Phase 12 D-03 — 알고리즘별 첫 단계 결정 후 StartDatumTeachStep
                //260504 hbk Phase 17 hotfix#9 (Option A) — Wizard skip-existing: 누락된 단계만 묻기.
                //  GetFirstStep → GetFirstMissingStep 교체. 모든 ROI 가 이미 있으면 Done → InvokeTryTeachDatum 자동 호출 (즉시 teach).
                _datumTeachStep = GetFirstMissingStep(datum);
                StartDatumTeachStep(_datumTeachStep);
            }
            else {
                //260424 hbk Phase 12 — 수동 해제 = 취소
                halconViewer.IsTeachDatumMode = false; //260505 hbk Phase 18 CO-04 — TeachDatum 종료 시 메뉴 숨김
                ExitCanvasMode();
            }
        }

        //260424 hbk Phase 12 D-03 — 알고리즘별 ROI 단계 시퀀스
        //260504 hbk Phase 17 hotfix#9 (Option A) — 단일 source-of-truth, GetFirstMissingStep/GetNextMissingStep 가 사용.
        private static EDatumTeachStep[] GetAlgorithmSteps(EDatumAlgorithm alg) {
            switch (alg) {
                case EDatumAlgorithm.TwoLineIntersect:
                    return new[] { EDatumTeachStep.Line1, EDatumTeachStep.Line2 };
                case EDatumAlgorithm.CircleTwoHorizontal:
                    return new[] { EDatumTeachStep.Circle, EDatumTeachStep.HorizontalA, EDatumTeachStep.HorizontalB };
                case EDatumAlgorithm.VerticalTwoHorizontal:
                    return new[] { EDatumTeachStep.Vertical, EDatumTeachStep.HorizontalA, EDatumTeachStep.HorizontalB };
                //260527 hbk Phase 34 D-34-07 — DualImage 변형: 순서 = HA → HB → V (가로축 이미지 먼저 → 자동 swap → 세로축 이미지). 1-image VTH (V → HA → HB) 와 의도적으로 다름.
                case EDatumAlgorithm.VerticalTwoHorizontalDualImage:
                    return new[] { EDatumTeachStep.HorizontalA, EDatumTeachStep.HorizontalB, EDatumTeachStep.Vertical }; //260527 hbk Phase 34 D-34-07
                default:
                    return new EDatumTeachStep[0];
            }
        }

        //260504 hbk Phase 17 hotfix#9 (Option A) — Wizard skip-existing helper.
        //  ROI 가 이미 그려진 단계는 누락 X → wizard 가 건너뜀. Length1/Length2/Radius > 0 이면 ROI 존재.
        private static bool IsStepMissing(DatumConfig d, EDatumTeachStep step) {
            switch (step) {
                case EDatumTeachStep.Line1:       return d.Line1_Length1 <= 0 || d.Line1_Length2 <= 0;
                case EDatumTeachStep.Line2:       return d.Line2_Length1 <= 0 || d.Line2_Length2 <= 0;
                case EDatumTeachStep.Vertical:    return d.Vertical_Length1 <= 0 || d.Vertical_Length2 <= 0;
                case EDatumTeachStep.Circle:      return d.CircleROI_Radius <= 0;
                case EDatumTeachStep.HorizontalA: return d.Horizontal_A_Length1 <= 0 || d.Horizontal_A_Length2 <= 0;
                case EDatumTeachStep.HorizontalB: return d.Horizontal_B_Length1 <= 0 || d.Horizontal_B_Length2 <= 0;
                default: return false;
            }
        }

        //260504 hbk Phase 17 hotfix#9 (Option A) — 첫 누락 단계 반환. 모든 ROI 존재 시 Done (즉시 teach).
        //  사용자 시나리오: 한 ROI 만 PropertyGrid 에서 Length1/Length2 = 0 입력 후 Teach Datum → 그 단계만 묻고 자동 teach.
        private EDatumTeachStep GetFirstMissingStep(DatumConfig datum) {
            foreach (var step in GetAlgorithmSteps(datum.AlgorithmTypeEnum)) {
                if (IsStepMissing(datum, step)) return step;
            }
            return EDatumTeachStep.Done;
        }

        //260504 hbk Phase 17 hotfix#9 (Option A) — current 이후 다음 누락 단계 반환.
        //  Wizard 가 한 단계 완료 후 다음 누락 단계로 점프 — 잔존 ROI 는 건너뛰고 누락만 묻기.
        private EDatumTeachStep GetNextMissingStep(DatumConfig datum, EDatumTeachStep current) {
            var steps = GetAlgorithmSteps(datum.AlgorithmTypeEnum);
            bool foundCurrent = false;
            foreach (var step in steps) {
                if (foundCurrent && IsStepMissing(datum, step)) return step;
                if (step == current) foundCurrent = true;
            }
            return EDatumTeachStep.Done;
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
                    //260527 hbk Phase 34 D-34-06 — DualImage 변형이면 진입 직전에 세로축 이미지로 자동 swap.
                    if (_editingDatum != null
                        && _editingDatum.AlgorithmTypeEnum == EDatumAlgorithm.VerticalTwoHorizontalDualImage) { //260527 hbk Phase 34 D-34-06
                        string vpath = _editingDatum.TeachingImagePath_Vertical; //260527 hbk Phase 34 D-34-06
                        if (!string.IsNullOrEmpty(vpath) && System.IO.File.Exists(vpath)) { //260527 hbk Phase 34
                            try { halconViewer.LoadImage(vpath); } //260527 hbk Phase 34 D-34-06
                            catch (Exception ex) { Logging.PrintErrLog((int)ELogType.Error, ex.Message); } //260527 hbk Phase 34
                            UpdateImageSourceBadge(ReringProject.Sequence.EImageSource.Vertical); //260527 hbk Phase 34.1 D-34.1-15 — 자동 swap 도 3자 동시 갱신
                        } else {
                            //260527 hbk Phase 34 D-34-08 — 빈 경로: 안내 + 드로잉 차단 (저장은 차단하지 않음).
                            //260527 hbk Phase 34.1 CO-34.1-02 hotfix BUG-C — vpath 빈 경로여도 badge 만큼은 갱신 (사용자에게 "이제 Vertical step" 시각 신호).
                            UpdateImageSourceBadge(ReringProject.Sequence.EImageSource.Vertical); //260527 hbk Phase 34.1 CO-34.1-02
                            label_drawHint.Content = "세로축 이미지를 Load 해주세요 (PropertyGrid 의 TeachingImagePath_Vertical)"; //260527 hbk Phase 34 D-34-08 + CO-34.1-02 — hint 강화
                            label_drawHint.Foreground = new SolidColorBrush(Colors.Orange); //260527 hbk Phase 34
                            label_drawHint.Visibility = Visibility.Visible; //260527 hbk Phase 34
                            break; //260527 hbk Phase 34 — 드로잉 시작 안 함 (switch case 종료)
                        }
                        label_drawHint.Content = "Step 3/3: 수직 ROI를 드래그하세요"; //260527 hbk Phase 34 D-34-07
                    } else {
                        label_drawHint.Content = "Step 1/3: 수직 ROI를 드래그하세요"; //260424 hbk Phase 12 — 기존 1-image VTH 라벨 보존
                    }
                    label_drawHint.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFAAAAAA")); //260424 hbk Phase 12
                    label_drawHint.Visibility = Visibility.Visible;
                    halconViewer.RectDrawingCompleted += HalconViewer_DatumRectCompleted;
                    halconViewer.StartRectangleDrawing();
                    break;
                case EDatumTeachStep.HorizontalA:
                    //260527 hbk Phase 34 D-34-07 — DualImage 변형: Step 1/3 (가로축 이미지 표시 상태 가정).
                    if (_editingDatum != null
                        && _editingDatum.AlgorithmTypeEnum == EDatumAlgorithm.VerticalTwoHorizontalDualImage) { //260527 hbk Phase 34 D-34-07
                        label_drawHint.Content = "Step 1/3: 수평 A ROI를 드래그하세요"; //260527 hbk Phase 34 D-34-07
                    } else {
                        label_drawHint.Content = "Step 2/3: 수평 A ROI를 드래그하세요"; //260424 hbk Phase 12 — 기존 1-image VTH/CTH 라벨 보존
                    }
                    label_drawHint.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFAAAAAA")); //260424 hbk Phase 12
                    label_drawHint.Visibility = Visibility.Visible;
                    halconViewer.RectDrawingCompleted += HalconViewer_DatumRectCompleted;
                    halconViewer.StartRectangleDrawing();
                    break;
                case EDatumTeachStep.HorizontalB:
                    //260527 hbk Phase 34 D-34-07 — DualImage 변형: Step 2/3.
                    if (_editingDatum != null
                        && _editingDatum.AlgorithmTypeEnum == EDatumAlgorithm.VerticalTwoHorizontalDualImage) { //260527 hbk Phase 34 D-34-07
                        label_drawHint.Content = "Step 2/3: 수평 B ROI를 드래그하세요"; //260527 hbk Phase 34 D-34-07
                    } else {
                        label_drawHint.Content = "Step 3/3: 수평 B ROI를 드래그하세요"; //260424 hbk Phase 12 — 기존 1-image VTH/CTH 라벨 보존
                    }
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
            //260426 hbk Phase 14-03 W4 precheck — EDatumTeachStep.Vertical: true (이미 정의됨, MainView.xaml.cs:52). Branch A 적용 — Line1/Vertical case 분리.
            switch (_datumTeachStep) {
                case EDatumTeachStep.Line1:
                    _editingDatum.Line1_Row     = centerRow;
                    _editingDatum.Line1_Col     = centerCol;
                    _editingDatum.Line1_Phi     = 0.0;
                    _editingDatum.Line1_Length1 = halfW; //260426 hbk Phase 13 D-PRP-LENFIX — 정정: halfW(X축 절반)=Length1
                    _editingDatum.Line1_Length2 = halfH; //260426 hbk Phase 13 D-PRP-LENFIX — 정정: halfH(Y축 절반)=Length2
                    break;
                //260426 hbk Phase 14-03 W4-A — Vertical case 분리: Line1_* → Vertical_* write-back (의미적 분리)
                case EDatumTeachStep.Vertical:
                    _editingDatum.Vertical_Row     = centerRow;
                    _editingDatum.Vertical_Col     = centerCol;
                    _editingDatum.Vertical_Phi     = 0.0;
                    _editingDatum.Vertical_Length1 = halfW;
                    _editingDatum.Vertical_Length2 = halfH;
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
            //260509 hbk Phase 20 — chained ?. expanded
            if (mParentWindow != null && mParentWindow.inspectionList != null) mParentWindow.inspectionList.RefreshParamEditor();
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
            //260509 hbk Phase 20 — chained ?. expanded
            if (mParentWindow != null && mParentWindow.inspectionList != null) mParentWindow.inspectionList.RefreshParamEditor();
            halconViewer.SetDatumOverlay(_editingDatum, true);

            AdvanceDatumTeachStep();
        }

        //260424 hbk Phase 12 — 다음 step 전이
        //260504 hbk Phase 17 hotfix#9 (Option A) — Wizard skip-existing: 잔존 ROI 건너뛰고 다음 누락 단계만 묻기.
        private void AdvanceDatumTeachStep() {
            if (_editingDatum == null) { ExitCanvasMode(); return; }
            _datumTeachStep = GetNextMissingStep(_editingDatum, _datumTeachStep);
            StartDatumTeachStep(_datumTeachStep);
        }

        //260424 hbk Phase 12 D-02 — 마지막 ROI 직후 DatumFindingService.TryTeachDatum 자동 호출
        //260527 hbk Phase 34 D-34-01/02 — DualImage 변형 시 두 파일에서 이미지 2개 로드 후 신규 2-image TryTeachDatum 호출 (goto 패턴 0 — early-return + try/finally).
        private void InvokeTryTeachDatum() {
            if (_editingDatum == null) { ExitCanvasMode(); return; }

            HImage img = halconViewer.CurrentImage; //260424 hbk Phase 12 — Phase 11 이미지 로드 이후 상태
            if (img == null) {
                //260503 hbk Phase 17 D-12 — label_drawHint 사유 표시 폐기 → CustomMessageBox
                label_drawHint.Visibility = Visibility.Collapsed;
                CustomMessageBox.Show("티칭 실패", "이미지가 없습니다. 먼저 Grab 또는 Load Image 를 수행하세요."); //260503 hbk Phase 17 D-12
                _canvasMode = ECanvasMode.None;
                btn_teachDatum.IsChecked = false;
                _editingDatum = null;
                halconViewer.IsTeachDatumMode = false; //260505 hbk Phase 18 CO-04 — TeachDatum 종료 시 메뉴 숨김
                return;
            }

            var svc = new ReringProject.Halcon.Algorithms.DatumFindingService(); //260424 hbk Phase 12 — 무상태 서비스
            string error = null; //260527 hbk Phase 34 — DualImage 분기 공용 변수
            bool ok = false; //260527 hbk Phase 34 — DualImage 분기 공용 변수

            //260527 hbk Phase 34 D-34-01/02 — DualImage 변형: 두 파일에서 이미지 2개 로드 + 신규 2-image TryTeachDatum 호출. goto 패턴 0 (early-return + try/finally).
            if (_editingDatum.AlgorithmTypeEnum == EDatumAlgorithm.VerticalTwoHorizontalDualImage) { //260527 hbk Phase 34
                string pathH = _editingDatum.TeachingImagePath; //260527 hbk Phase 34
                string pathV = _editingDatum.TeachingImagePath_Vertical; //260527 hbk Phase 34

                //260527 hbk Phase 34 D-34-10 — 빈 경로 / 파일 없음 가드 (early-return).
                if (string.IsNullOrEmpty(pathH) || !System.IO.File.Exists(pathH)) { //260527 hbk Phase 34
                    ExitTeachWithError("가로축 티칭 이미지 경로가 비어 있거나 파일이 없습니다."); //260527 hbk Phase 34 D-34-10
                    return; //260527 hbk Phase 34
                }
                if (string.IsNullOrEmpty(pathV) || !System.IO.File.Exists(pathV)) { //260527 hbk Phase 34
                    ExitTeachWithError("세로축 티칭 이미지 경로가 비어 있거나 파일이 없습니다."); //260527 hbk Phase 34 D-34-10
                    return; //260527 hbk Phase 34
                }

                //260527 hbk Phase 34 — 이미지 2개 로드: try/finally 로 dispose 보장. 로드 실패 시 error 설정 후 try 블록 종료 → 공통 결과 처리.
                HImage imgH = null, imgV = null; //260527 hbk Phase 34
                try {
                    try { imgH = new HImage(pathH); } //260527 hbk Phase 34
                    catch (Exception exH) { error = "가로축 이미지 로드 실패: " + exH.Message; ok = false; } //260527 hbk Phase 34

                    if (error == null) { //260527 hbk Phase 34 — 가로축 로드 성공 시에만 세로축 시도
                        try { imgV = new HImage(pathV); } //260527 hbk Phase 34
                        catch (Exception exV) { error = "세로축 이미지 로드 실패: " + exV.Message; ok = false; } //260527 hbk Phase 34
                    }

                    if (error == null) { //260527 hbk Phase 34 — 두 이미지 모두 성공 시에만 TryTeachDatum 호출
                        ok = svc.TryTeachDatum(imgH, imgV, _editingDatum, out error); //260527 hbk Phase 34 D-34-01/02 — 2-image 오버로드
                    }
                } finally {
                    if (imgH != null) { try { imgH.Dispose(); } catch { } } //260527 hbk Phase 34
                    if (imgV != null) { try { imgV.Dispose(); } catch { } } //260527 hbk Phase 34
                }
            } else {
                ok = svc.TryTeachDatum(img, _editingDatum, out error); //기존 단일-이미지 오버로드 (회귀 0)
            }

            //공통 결과 처리 (DualImage / 1-image 양쪽 공통 — goto 0)
            if (ok) {
                label_drawHint.Content = "Datum 티칭 완료 — Recipe Save 권장"; //260424 hbk Phase 12
                label_drawHint.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF4ADE80")); //260424 hbk Phase 12 success green
                label_drawHint.Visibility = Visibility.Visible;
                //260424 hbk Phase 12 — 오버레이 갱신 (LastTeachSucceeded=true → HalconDisplayService CircleTwoHorizontal/Horizontal A/B 분기 렌더)
                halconViewer.SetDatumOverlay(_editingDatum, true);
                //260425 hbk Phase 13 D-01..D-04 — teach 완료 시점에 후보 갱신
                PublishDatumRoiCandidates(_editingDatum);
                //260425 hbk Phase 13 D-VIZ-06 — teach 성공 시 좌표 라벨 갱신 (PublishDatumRoiCandidates 가 이미 호출하나 명시 보장)
                UpdateDatumRefCoordsLabel(_editingDatum);
            }
            else {
                //260503 hbk Phase 17 D-12 — teach 실패 사유 모달 (label_drawHint 사유 표시 패턴 폐기). FormatTeachError 가 D-04 EdgeDirection 힌트 통합.
                label_drawHint.Visibility = Visibility.Collapsed;
                CustomMessageBox.Show("티칭 실패", FormatTeachError(_editingDatum, error)); //260505 hbk Phase 18 CO-06
            }

            //260424 hbk Phase 12 — ROI 유지(재튜닝 가능), canvas mode 해제
            _canvasMode = ECanvasMode.None;
            btn_teachDatum.IsChecked = false;
            _editingDatum = null;
            halconViewer.IsTeachDatumMode = false; //260505 hbk Phase 18 CO-04 — TeachDatum 종료 시 메뉴 숨김
        }

        //260527 hbk Phase 34 — InvokeTryTeachDatum 의 early-return 헬퍼 (goto 패턴 회피).
        private void ExitTeachWithError(string message) {
            label_drawHint.Visibility = Visibility.Collapsed; //260527 hbk Phase 34
            CustomMessageBox.Show("티칭 실패", message); //260527 hbk Phase 34
            _canvasMode = ECanvasMode.None; //260527 hbk Phase 34
            btn_teachDatum.IsChecked = false; //260527 hbk Phase 34
            _editingDatum = null; //260527 hbk Phase 34
            halconViewer.IsTeachDatumMode = false; //260527 hbk Phase 34
        }

        //260424 hbk Phase 13 D-05..D-08 — 런타임 TryFindDatum 테스트 진입 (현재/Load 이미지 2-way + 성공 주황 십자 + 실패 에러 메시지)
        private void BtnTestFindDatum_Click(object sender, RoutedEventArgs e) {
            //260424 hbk Phase 13 D-05 — Datum 해결 (InspectionListView 선택 우선, _editingDatum fallback 없음 — teach 세션 독립)
            //260509 hbk Phase 20 — chained ?. expanded
            DatumConfig datum;
            if (mParentWindow != null && mParentWindow.inspectionList != null) datum = mParentWindow.inspectionList.SelectedParam as DatumConfig;
            else                                                               datum = null;
            if (datum == null || !datum.IsConfigured || !datum.LastTeachSucceeded) {
                CustomMessageBox.Show("Datum Find 테스트", "Datum 티칭이 완료된 후 테스트 가능합니다."); //260425 hbk Phase 13 cleanup — Plan 02 인자 순서 fix
                return;
            }

            //260424 hbk Phase 13 D-07/D-08 — DatumFindingService.TryFindDatum 호출 (Phase 4 Plan 01 L28 시그니처)
            var svc = new ReringProject.Halcon.Algorithms.DatumFindingService();
            HTuple transform;
            string error = null;
            bool ok = false;

            //260528 hbk Phase 34.1 CO-34.1-08 hotfix — DualImage 변형은 두 파일 직접 로드 + 2-image 오버로드 호출 (Phase 34 D-34-01/02 누락 site, BtnTestFindDatum_Click). Teach 와 동일 패턴.
            if (datum.AlgorithmTypeEnum == EDatumAlgorithm.VerticalTwoHorizontalDualImage) { //260528 hbk Phase 34.1 CO-34.1-08
                string pathH = datum.TeachingImagePath; //260528 hbk Phase 34.1 CO-34.1-08
                string pathV = datum.TeachingImagePath_Vertical; //260528 hbk Phase 34.1 CO-34.1-08
                if (string.IsNullOrEmpty(pathH) || !System.IO.File.Exists(pathH)) { //260528 hbk Phase 34.1 CO-34.1-08
                    CustomMessageBox.Show("Find 실패", "가로축 티칭 이미지 경로가 비어 있거나 파일이 없습니다."); //260528 hbk Phase 34.1 CO-34.1-08
                    return; //260528 hbk Phase 34.1 CO-34.1-08
                }
                if (string.IsNullOrEmpty(pathV) || !System.IO.File.Exists(pathV)) { //260528 hbk Phase 34.1 CO-34.1-08
                    CustomMessageBox.Show("Find 실패", "세로축 티칭 이미지 경로가 비어 있거나 파일이 없습니다."); //260528 hbk Phase 34.1 CO-34.1-08
                    return; //260528 hbk Phase 34.1 CO-34.1-08
                }
                HImage imgH = null, imgV = null; //260528 hbk Phase 34.1 CO-34.1-08
                try {
                    try { imgH = new HImage(pathH); } //260528 hbk Phase 34.1 CO-34.1-08
                    catch (Exception exH) { error = "가로축 이미지 로드 실패: " + exH.Message; ok = false; } //260528 hbk Phase 34.1 CO-34.1-08
                    if (error == null) { //260528 hbk Phase 34.1 CO-34.1-08
                        try { imgV = new HImage(pathV); } //260528 hbk Phase 34.1 CO-34.1-08
                        catch (Exception exV) { error = "세로축 이미지 로드 실패: " + exV.Message; ok = false; } //260528 hbk Phase 34.1 CO-34.1-08
                    }
                    if (error == null) { //260528 hbk Phase 34.1 CO-34.1-08
                        ok = svc.TryFindDatum(imgH, imgV, datum, out transform, out error); //260528 hbk Phase 34.1 CO-34.1-08 — 2-image 오버로드
                    }
                    else {
                        HOperatorSet.HomMat2dIdentity(out transform); //260528 hbk Phase 34.1 CO-34.1-08 — 로드 실패 transform 초기화
                    }
                } finally {
                    if (imgH != null) { try { imgH.Dispose(); } catch { } } //260528 hbk Phase 34.1 CO-34.1-08
                    if (imgV != null) { try { imgV.Dispose(); } catch { } } //260528 hbk Phase 34.1 CO-34.1-08
                }
            }
            else {
                //260424 hbk Phase 13 D-06 — 테스트 이미지 소스 선택 (현재 / Load / 취소)
                HImage testImage = AskTestImageSource();
                if (testImage == null) return; //260424 hbk Phase 13 D-06 — 사용자 취소
                ok = svc.TryFindDatum(testImage, datum, out transform, out error); //단일-이미지 오버로드 (회귀 0)
            }

            //260503 hbk Phase 17 D-12/D-14 — label_drawHint / label_testFindResult inline 피드백 폐기, 성공/실패 모두 모달 정책 (성공 X / 실패 O)
            label_drawHint.Visibility = Visibility.Collapsed; //260503 hbk Phase 17 D-14
            label_testFindResult.Visibility = Visibility.Collapsed; //260503 hbk Phase 17 D-14 — inline 표시 사용 안 함
            if (ok) {
                //260503 hbk Phase 17 D-14 — 성공: 시각화 자동 (TryFindDatum 이 DetectedOrigin* + LastFindSucceeded write-back → SetDatumOverlay → RenderDatumOverlay 가 RenderDatumFindResult 자동 호출 chain)
                halconViewer.SetDatumOverlay(datum, true); //260503 hbk Phase 17 D-14 — purple cross + 좌표 + 화살표 (HalconDisplayService.RenderDatumFindResult)
                //260503 hbk Phase 17 D-14 — PropertyGrid 메트릭 갱신 (DetectedEdgeCount/FitRMSE/AngleDeg ReadOnly 표시)
                try { datum.RaisePropertyChanged(string.Empty); } catch { } //260503 hbk Phase 17 D-14
                if (mParentWindow != null && mParentWindow.inspectionList != null) {
                    mParentWindow.inspectionList.RefreshParamEditor(); //260503 hbk Phase 17 D-14
                }
                //260503 hbk Phase 17 D-12 — 성공 시 모달 X (UI-SPEC LOCKED — 사용자가 캔버스 시각화로 즉시 확인)
            }
            else {
                //260503 hbk Phase 17 D-12 — Test Find 실패 사유 모달 (label_testFindResult inline 표시 폐기). FormatFindError 가 D-04 EdgeDirection 힌트 통합.
                CustomMessageBox.Show("Find 실패", FormatFindError(error)); //260503 hbk Phase 17 D-12
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
