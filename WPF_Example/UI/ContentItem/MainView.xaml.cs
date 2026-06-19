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

        private enum ECanvasMode { None, RectRoi, PolygonRoi, CircleRoi, TeachDatum, Calibration, PatternRoi, PatternRoi2, AlignLineRoi } //260618 hbk Phase 54 ALIGN-01 / 260619 PatternRoi2 (Phase 55 ALIGN-02)
        private ECanvasMode _canvasMode = ECanvasMode.None;
        private FAIConfig _editingFai;
        private MeasurementBase _editingCircleMeasurement;
        private string _editingCircleFaiName;
        private MeasurementBase _editingMeasurement;
        private string _editingMeasurementFaiName;
        // ArcLineIntersect 4-ROI 순차 드로잉 인덱스 (0=EdgeA1, 1=EdgeB1, 2=EdgeA2, 3=EdgeB2)
        private int _editingMeasurementRoiIndex;
        private enum EDatumTeachStep { Line1, Line2, Circle, Vertical, HorizontalA, HorizontalB, Done }
        private EDatumTeachStep _datumTeachStep = EDatumTeachStep.Line1;
        private DatumConfig _editingDatum;
        // 현재 캔버스에 표시 중인 이미지 축 (가로/세로). DualImage 변형에서만 의미 있음. 세션 한정, INI 미저장 (Datum 노드 이동 시 가로축으로 리셋).
        private ReringProject.Sequence.EImageSource _currentImageSource = ReringProject.Sequence.EImageSource.Horizontal;
        // 현재 선택된 Datum (teach 모드 무관, swap UI 의 대상).
        //  _editingDatum 은 Teach Datum 클릭 시에만 set → 토글 핸들러가 노드 선택 직후 동작하려면 별도 reference 필요.
        //  PublishDatumRoiCandidates 진입 시 갱신, AlgorithmType PropertyChanged 구독 대상.
        private DatumConfig _selectedDatumForSwap;
        // 현재 선택된 DualImage Measurement (_selectedDatumForSwap 대칭, mutex 페어).
        //  세션 한정, INI 미저장. InspectionListView Measurement 노드 선택 시 set, Datum 노드 선택 시 clear.
        private DualImageEdgeDistanceMeasurement _selectedDualImageMeasurement;
        // 배지 색상 정적 frozen brush (인스턴스 GC 방지 + WPF 즉시 반영).
        //  ConvertFromString 매 호출마다 새 brush 생성 → 일부 환경에서 WPF Background 갱신 누락 의심 → 정적/frozen 으로 대체.
        private static readonly SolidColorBrush BadgeBrushHorizontal = CreateFrozenBrush(0x19, 0x76, 0xD2); // Material Blue 700
        private static readonly SolidColorBrush BadgeBrushVertical   = CreateFrozenBrush(0xF5, 0x7C, 0x00); // Material Orange 800
        private static SolidColorBrush CreateFrozenBrush(byte r, byte g, byte b) {
            var brush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(r, g, b));
            brush.Freeze();
            return brush;
        }
        private readonly List<System.Windows.Point> _polygonPoints = new List<System.Windows.Point>();
        private readonly List<System.Windows.Point> _calibrationPoints = new List<System.Windows.Point>();
        private double _lastPointerRow, _lastPointerCol;

        public MainView() {
            InitializeComponent();
            halconViewer.PointerInfoChanged += HalconViewer_PointerInfoChanged;
            halconViewer.RoiMoveCompleted += HalconViewer_RoiMoveCompleted;
            halconViewer.RoiDeleteRequested += HalconViewer_RoiDeleteRequested;
            halconViewer.RoiGeometryChanged += HalconViewer_RoiGeometryChanged;
            // "ROI 다시 그리기": Length/Radius 0 리셋 후 오버레이 갱신
            halconViewer.RoiRedrawRequested += (roiId) =>
            {
                if (_editingDatum != null)
                {
                    ClearDatumRoiFields(_editingDatum, roiId);
                    halconViewer.SetDatumOverlay(_editingDatum, false, false); // 모드 해제 → isEditMode=false 명시
                    PublishDatumRoiCandidates(_editingDatum);
                }
            };
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
        public void DisplayShotImage(ShotConfig shot) {
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
                    if (img != null) img.Dispose();
                }
            } else {
                label_message.Content = "NO Image";
                label_message.Visibility = Visibility.Visible;
            }
        }

        /// <summary>Displays the TeachingImagePath image of the given DatumConfig on the canvas.
        /// Mirrors DisplayShotImage but uses DatumConfig.TeachingImagePath (TeachingImagePath != SimulImagePath 분리 구조).</summary>
        public void DisplayDatumImage(DatumConfig datum) {
            if (datum == null) {
                return;
            }
            string path = datum.TeachingImagePath;
            if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path)) {
                // TeachingImagePath 미설정/파일 없음 — 기존 canvas 유지 (사용자가 Load Image 누르면 갱신)
                return;
            }
            try {
                halconViewer.LoadImage(path);
                label_message.Visibility = Visibility.Collapsed;
            } catch (Exception ex) {
                Logging.PrintErrLog((int)ELogType.Error, ex.Message);
            }
        }

        /// <summary>Resolves the owning ShotConfig for the given measurement and displays its image.</summary>
        public void DisplayMeasurementImage(MeasurementBase measurement) {
            if (measurement == null) return;
            // 이름 round-trip(FindFAIByName) 제거: 여러 Shot 의 FAI 이름이 같으면(기본 FAI_0 등)
            //  첫 Shot 의 FAI 가 반환돼 잘못된 Shot 이미지가 표시됨. 소유 FAIConfig 를 객체 참조로 직접 해석.
            FAIConfig fai = FindFAIContainingMeasurement(measurement);
            if (fai == null) return;
            ShotConfig shot = fai.Owner as ShotConfig;
            DisplayShotImage(shot);
        }

        // btn_teachDatum.IsChecked == true → Edit 모드 (Datum CTH 시 fitting 원 + ROI 사각형 핸들 동시 표시).
        // SetDatumOverlay 다수 호출처에서 변수 shadowing 회피용 helper.
        private bool GetDatumEditMode() {
            return btn_teachDatum != null && btn_teachDatum.IsChecked == true;
        }

        // InspectionListView 등 외부 UserControl 에서 btn_teachDatum 직접 접근 회피.
        public bool IsDatumTeachActive {
            get { return btn_teachDatum != null && btn_teachDatum.IsChecked == true; }
        }

        // 검사 후 FAI/Measurement 노드 클릭 시 측정 결과 + 이미지 + overlay 재현 통합 진입점.
        //  Sequence 동작 변경 0: 측정 재 호출 없이 fai.LastOverlays (Action_FAIMeasurement EStep.Measure 누적) 재 렌더.
        public void RenderInspectionResultForNode(ParamBase param) {
            if (param == null) { halconViewer.ClearFaiCirclePreview(); return; }
            if (param is MeasurementBase meas) {
                DisplayMeasurementImage(meas);
                HighlightSelectedRoi(meas);
                RenderStoredOverlaysForMeasurement(meas);
                // FAI CircleDiameter Measurement 선택 시 Strip preview 활성. 폴라 경로 (Circle_RadialDirection != "") 한정.
                UpdateFaiCirclePreview(meas);
            } else if (param is FAIConfig fai) {
                DisplayFAIImage(fai);
                HighlightSelectedRoi(fai);
                RenderStoredOverlaysForFai(fai);
                halconViewer.ClearFaiCirclePreview(); // FAI 노드 자체는 strip preview 없음 (Measurement 노드만)
            }
        }

        // FAI Strip preview 활성/클리어. 폴라 경로 (Circle_RadialDirection in {Inward, Outward}) 한정.
        //  fit 경로 (Circle_RadialDirection == "") 는 strip 미사용 → preview 클리어.
        //  PropertyGrid 편집 후 strip 갱신: 사용자가 노드를 다시 클릭하면 RenderInspectionResultForNode 가 재 호출 → preview 재 셋. (live INPC 미구현 — 추후 phase)
        private void UpdateFaiCirclePreview(MeasurementBase meas)
        {
            var cd = meas as CircleDiameterMeasurement;
            if (cd == null || string.IsNullOrEmpty(cd.Circle_RadialDirection) || cd.Circle_Radius <= 0)
            {
                halconViewer.ClearFaiCirclePreview();
                return;
            }
            //  datumTransform = identity (UI 시점에 datum transform 미해상 — preview 좌표가 FAI 원본 좌표 기준).
            //  실제 검사 시 transform 적용되어 strip 위치가 약간 달라질 수 있음 (acceptable for preview).
            halconViewer.SetFaiCirclePreview(cd.Circle_Row, cd.Circle_Col, cd.Circle_Radius,
                cd.Circle_PolarStepDeg, cd.Circle_RectL1Ratio, cd.Circle_RectL2Ratio,
                null /* identity transform — preview */);
        }

        // FAI 노드 클릭 시 fai.LastOverlays 전체 재 렌더.
        //  SetInspectionOverlays 는 REPLACE 의미 (Clear + AddRange). null/빈 케이스에 빈 List → prior overlay 안전 클리어.
        private void RenderStoredOverlaysForFai(FAIConfig fai) {
            if (fai == null || fai.LastOverlays == null || fai.LastOverlays.Count == 0) {
                halconViewer.SetInspectionOverlays(new System.Collections.Generic.List<ReringProject.Halcon.Models.EdgeInspectionOverlay>());
                return;
            }
            halconViewer.SetInspectionOverlays(fai.LastOverlays);
        }

        // Measurement 노드 클릭 시 소유 FAI 의 LastOverlays 재 렌더 (전 타입 공통, 타입별 분기 없음).
        private void RenderStoredOverlaysForMeasurement(MeasurementBase meas) {
            if (meas == null) {
                halconViewer.SetInspectionOverlays(new System.Collections.Generic.List<ReringProject.Halcon.Models.EdgeInspectionOverlay>());
                return;
            }
            FAIConfig fai = FindFAIContainingMeasurement(meas); // 객체 참조 round-trip 회피
            RenderStoredOverlaysForFai(fai);
        }

        /// <summary>Binds DataGrid to the InspectionViewModel's MeasurementResults collection.</summary>
        public void SetFAIResultSource(InspectionViewModel vm) {
            dataGrid_faiResults.SetBinding(
                System.Windows.Controls.DataGrid.ItemsSourceProperty,
                new System.Windows.Data.Binding("MeasurementResults") { Source = vm });
        }

        private void FAIResults_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            var selectedRow = dataGrid_faiResults.SelectedItem as MeasurementResultRow;
            if (selectedRow == null) {
                // ItemsSource 교체 시 WPF 가 SelectedItem=null 로 자동 리셋하며 이 핸들러를 동기 발화.
                //  4인자 UpdateDisplayState(null) 는 _selectedRoiId=null clobber → 이후 HighlightSelectedRoi 의 하이라이트를
                //  렌더링 타이밍에 따라 지워버림. 3인자 오버로드(selectedRoiId 미변경)를 써서 트리 선택 하이라이트를 보존한다.
                var allRois = GetCurrentFAIRois();
                if (allRois.Count > 0)
                    halconViewer.UpdateDisplayState(allRois, null, null); // 3인자: _selectedRoiId 보존
                return;
            }

            var rois = GetCurrentFAIRois();
            // 결과행 선택 시 그 측정 전용 ROI(Id="FAIName_측정명")만 하이라이트한다.
            //  해당 측정 ROI 가 없으면 FAIName 으로 폴백 — Render 의 접두사 매칭이 FAI 전체 ROI 를 하이라이트.
            //  selectedRow.FAIName 만 쓰면 FAI 노드 선택과 동일 selId 라 행을 바꿔도 색이 안 변함.
            string composite = selectedRow.FAIName + "_" + selectedRow.MeasurementName;
            string selectedRoiId = selectedRow.FAIName;
            foreach (var r in rois) {
                if (r != null && r.Id == composite) { selectedRoiId = composite; break; }
            }
            halconViewer.UpdateDisplayState(rois, selectedRoiId, null, null);

            // 동일-명 shot 충돌 회피: 측정 객체 참조 우선, 실패 시 이름 폴백
            FAIConfig parentFai = null;
            if (selectedRow.SourceMeasurement != null) parentFai = FindFAIContainingMeasurement(selectedRow.SourceMeasurement);
            if (parentFai == null) parentFai = FindFAIByName(selectedRow.FAIName);
            if (parentFai != null && (parentFai.ROI_Length1 <= 0 || parentFai.ROI_Length2 <= 0)) {
                label_message.Content = "ROI not set";
                label_message.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFAAAAAA"));
                label_message.Visibility = Visibility.Visible;
            }
        }

        /// <summary>Collects RoiDefinitions from all FAIs of the currently displayed shot.</summary>
        private List<RoiDefinition> GetCurrentFAIRois() {
            var result = new List<RoiDefinition>();
            // FAI 객체 dedup (이름 dedup 은 동일-명 shot 의 두 번째 FAI 를 skip → 첫 shot 으로 묶임).
            var seenFais = new HashSet<FAIConfig>();
            foreach (var item in dataGrid_faiResults.Items) {
                var row = item as MeasurementResultRow;
                if (row == null) continue;
                // row 가 보유한 실제 측정 객체(SourceMeasurement)로 소유 FAI 를 참조 해석(ReferenceEquals).
                //  여러 shot 이 동일 FAI 명(기본 FAI_0/1)을 가질 때 FindFAIByName 이 첫-일치(다른) shot 의 FAI 를 반환해
                //  잘못된 shot 의 ROI 가 표시되던 결함 차단. 객체 해석 실패 시에만 이름 폴백(레거시 회귀 0).
                FAIConfig fai = null;
                if (row.SourceMeasurement != null) fai = FindFAIContainingMeasurement(row.SourceMeasurement);
                if (fai == null && !string.IsNullOrEmpty(row.FAIName)) fai = FindFAIByName(row.FAIName);
                if (fai == null) continue;
                if (!seenFais.Add(fai)) continue; // 같은 FAI 객체 중복 수집 방지
                var roi = fai.ToRoiDefinition();
                if (roi.IsTaught) result.Add(roi);
                // Point ROI 보유 측정 타입 동시 수집 (EdgeToLineDistance/EdgeToLineAngle/ArcEdgeDistance)
                foreach (var m in fai.Measurements) {
                    result.AddRange(BuildPointRoiDefinitions(m, fai.FAIName));
                }
            }
            return result;
        }

        // 단일 FAI 의 rect ROI + Point ROI 를 result 에 누적 (GetCurrentFAIRois 와 동일 규칙, DataGrid 비의존 버전).
        private void AppendFaiRois(List<RoiDefinition> result, FAIConfig fai) {
            if (fai == null) return;
            var roi = fai.ToRoiDefinition();
            if (roi.IsTaught) result.Add(roi);
            // Point ROI 보유 측정 타입 동시 수집
            foreach (var m in fai.Measurements) {
                result.AddRange(BuildPointRoiDefinitions(m, fai.FAIName));
            }
        }

        // Point ROI 보유 측정 타입 → RoiDefinition 리스트 변환 (캔버스 렌더용).
        //  Length1/2 미티칭(≤0) 이면 해당 ROI skip. CommitRectRoi 가 Point_Phi=0 으로만 쓰므로 축정렬 bounding box.
        //  ArcLineIntersect 는 EdgeA1/EdgeB1/EdgeA2/EdgeB2 4개 독립 RoiDefinition, 비-ArcLineIntersect 는 0 또는 1개 반환.
        private static List<RoiDefinition> BuildPointRoiDefinitions(MeasurementBase m, string faiName) {
            var result = new List<RoiDefinition>();
            string measName = m.MeasurementName;
            if (string.IsNullOrEmpty(measName)) measName = m.TypeName;

            // ArcLineIntersect: EdgeA1/EdgeB1/EdgeA2/EdgeB2 4개 독립 RoiDefinition. 미티칭 ROI 는 개별 skip.
            var ali = m as ArcLineIntersectDistanceMeasurement;
            if (ali != null) {
                if (ali.EdgeA1_Length1 > 0 && ali.EdgeA1_Length2 > 0) {
                    result.Add(new RoiDefinition {
                        Id = faiName + "_" + measName + "_EdgeA1",
                        Name = measName + "_EdgeA1",
                        Row1 = ali.EdgeA1_Row - ali.EdgeA1_Length1, Column1 = ali.EdgeA1_Col - ali.EdgeA1_Length2,
                        Row2 = ali.EdgeA1_Row + ali.EdgeA1_Length1, Column2 = ali.EdgeA1_Col + ali.EdgeA1_Length2,
                        IsTaught = true
                    });
                }
                if (ali.EdgeB1_Length1 > 0 && ali.EdgeB1_Length2 > 0) {
                    result.Add(new RoiDefinition {
                        Id = faiName + "_" + measName + "_EdgeB1",
                        Name = measName + "_EdgeB1",
                        Row1 = ali.EdgeB1_Row - ali.EdgeB1_Length1, Column1 = ali.EdgeB1_Col - ali.EdgeB1_Length2,
                        Row2 = ali.EdgeB1_Row + ali.EdgeB1_Length1, Column2 = ali.EdgeB1_Col + ali.EdgeB1_Length2,
                        IsTaught = true
                    });
                }
                if (ali.EdgeA2_Length1 > 0 && ali.EdgeA2_Length2 > 0) {
                    result.Add(new RoiDefinition {
                        Id = faiName + "_" + measName + "_EdgeA2",
                        Name = measName + "_EdgeA2",
                        Row1 = ali.EdgeA2_Row - ali.EdgeA2_Length1, Column1 = ali.EdgeA2_Col - ali.EdgeA2_Length2,
                        Row2 = ali.EdgeA2_Row + ali.EdgeA2_Length1, Column2 = ali.EdgeA2_Col + ali.EdgeA2_Length2,
                        IsTaught = true
                    });
                }
                if (ali.EdgeB2_Length1 > 0 && ali.EdgeB2_Length2 > 0) {
                    result.Add(new RoiDefinition {
                        Id = faiName + "_" + measName + "_EdgeB2",
                        Name = measName + "_EdgeB2",
                        Row1 = ali.EdgeB2_Row - ali.EdgeB2_Length1, Column1 = ali.EdgeB2_Col - ali.EdgeB2_Length2,
                        Row2 = ali.EdgeB2_Row + ali.EdgeB2_Length1, Column2 = ali.EdgeB2_Col + ali.EdgeB2_Length2,
                        IsTaught = true
                    });
                }
                return result; // 나머지 타입 분기 통과 불필요
            }

            // DualImage: PointROI + LineROI 2개 독립 RoiDefinition (RoiId suffix "_Point"/"_Line")
            var dual = m as DualImageEdgeDistanceMeasurement;
            if (dual != null) {
                if (dual.PointROI_Length1 > 0 && dual.PointROI_Length2 > 0) {
                    result.Add(new RoiDefinition {
                        Id = faiName + "_" + measName + "_Point",
                        Name = measName + "_Point",
                        Row1 = dual.PointROI_Row - dual.PointROI_Length1, Column1 = dual.PointROI_Col - dual.PointROI_Length2,
                        Row2 = dual.PointROI_Row + dual.PointROI_Length1, Column2 = dual.PointROI_Col + dual.PointROI_Length2,
                        IsTaught = true
                    });
                }
                if (dual.LineROI_Length1 > 0 && dual.LineROI_Length2 > 0) {
                    result.Add(new RoiDefinition {
                        Id = faiName + "_" + measName + "_Line",
                        Name = measName + "_Line",
                        Row1 = dual.LineROI_Row - dual.LineROI_Length1, Column1 = dual.LineROI_Col - dual.LineROI_Length2,
                        Row2 = dual.LineROI_Row + dual.LineROI_Length1, Column2 = dual.LineROI_Col + dual.LineROI_Length2,
                        IsTaught = true
                    });
                }
                return result; // ArcLineIntersect 와 동일하게 조기 종료 (단일 RoiDefinition 분기 건너뜀)
            }

            double pRow = 0, pCol = 0, pLen1 = 0, pLen2 = 0;
            var etld = m as EdgeToLineDistanceMeasurement;
            if (etld != null) { pRow = etld.Point_Row; pCol = etld.Point_Col; pLen1 = etld.Point_Length1; pLen2 = etld.Point_Length2; }
            var etla = m as EdgeToLineAngleMeasurement;
            if (etla != null) { pRow = etla.Point_Row; pCol = etla.Point_Col; pLen1 = etla.Point_Length1; pLen2 = etla.Point_Length2; }
            var aed = m as ArcEdgeDistanceMeasurement;
            if (aed != null) { pRow = aed.Point_Row; pCol = aed.Point_Col; pLen1 = aed.Point_Length1; pLen2 = aed.Point_Length2; }
            var cAngle = m as CompoundAngleMeasurement;
            if (cAngle != null) { pRow = cAngle.Rect_Row; pCol = cAngle.Rect_Col; pLen1 = cAngle.Rect_Length1; pLen2 = cAngle.Rect_Length2; }
            var cCenterC = m as CompoundCenterCDistanceMeasurement;
            if (cCenterC != null) { pRow = cCenterC.Rect_Row; pCol = cCenterC.Rect_Col; pLen1 = cCenterC.Rect_Length1; pLen2 = cCenterC.Rect_Length2; }
            var cCenterB = m as CompoundCenterBDistanceMeasurement;
            if (cCenterB != null) { pRow = cCenterB.Rect_Row; pCol = cCenterB.Rect_Col; pLen1 = cCenterB.Rect_Length1; pLen2 = cCenterB.Rect_Length2; }
            var cShort = m as CompoundShortAxisDistanceMeasurement;
            if (cShort != null) { pRow = cShort.Rect_Row; pCol = cShort.Rect_Col; pLen1 = cShort.Rect_Length1; pLen2 = cShort.Rect_Length2; }
            if (pLen1 <= 0 || pLen2 <= 0) return result; // 미티칭 시 빈 리스트 반환
            result.Add(new RoiDefinition {
                Id = faiName + "_" + measName,
                Name = measName,
                Row1 = pRow - pLen1,
                Column1 = pCol - pLen2,
                Row2 = pRow + pLen1,
                Column2 = pCol + pLen2,
                IsTaught = true
            });
            return result;
        }

        // 주어진 FAI 가 속한 Shot 의 모든 FAI ROI 를 DataGrid 비의존으로 수집한다.
        //  GetCurrentFAIRois 는 dataGrid_faiResults 바인딩 갱신 지연으로 트리 선택보다 한 박자 늦은 ROI 집합을 줘
        //  → 트리 선택 하이라이트가 stale FAI 기준이 되어 Id 불일치(녹색 유지) 발생. anchorFai.Owner(Shot)에서 직접 수집한다.
        private List<RoiDefinition> CollectShotRois(FAIConfig anchorFai) {
            var result = new List<RoiDefinition>();
            if (anchorFai == null) return result;
            ShotConfig shot = anchorFai.Owner as ShotConfig;
            if (shot == null || shot.FAIList == null) {
                AppendFaiRois(result, anchorFai); // Shot 미해결 시 anchor 단독 수집 (fallback)
                return result;
            }
            foreach (FAIConfig fai in shot.FAIList) {
                AppendFaiRois(result, fai);
            }
            return result;
        }

        /// <summary>
        /// 트리에서 선택된 param 의 ROI Id 를 도출해 halconViewer 에 하이라이트를 적용한다.
        /// param 이 FAIConfig 또는 MeasurementBase 가 아니면 하이라이트를 해제한다.
        /// </summary>
        public void HighlightSelectedRoi(ParamBase param) {
            string selRoiId = null;
            string faiNameForFallback = null;
            FAIConfig anchorFai = null;
            if (param is FAIConfig faiSel) {
                // FAI 노드 선택 시 하이라이트 없음(ROI 전부 녹색) — 사용자 결정.
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
                    // anchorFai 를 이름(FindFAIByName) 대신 측정 객체 참조로 해석.
                    //  여러 Shot 동일 FAI 명 시 ROI 하이라이트가 첫 Shot 으로 잘못 묶이던 결함 차단.
                    anchorFai = FindFAIContainingMeasurement(measSel);
                }
            }
            // dataGrid_faiResults 바인딩 지연 회피: 선택 FAI 의 Shot 에서 직접 ROI 수집.
            // FAI 노드 선택 시 그 FAI 의 ROI+측정위치만 표시. 측정 노드는 Shot 컨텍스트 유지(선택 ROI 노란색).
            List<RoiDefinition> rois;
            if (param is FAIConfig) {
                rois = new List<RoiDefinition>();
                AppendFaiRois(rois, anchorFai); // 선택 FAI 단독 ROI (+ measurement point ROI)
            } else {
                rois = CollectShotRois(anchorFai); // 측정 노드 등은 기존대로 Shot 전체 컨텍스트
            }
            // composite ID 매칭 ROI 없으면 부모 FAI ROI 로 fallback (일반 FAI rect ROI 는 Id=FAIName)
            if (!string.IsNullOrEmpty(selRoiId) && !string.IsNullOrEmpty(faiNameForFallback)) {
                bool matched = false;
                foreach (var r in rois) {
                    if (r != null && r.Id == selRoiId) { matched = true; break; }
                }
                if (!matched) selRoiId = faiNameForFallback;
            }
            halconViewer.UpdateDisplayState(rois, selRoiId, null, null);
        }

        private FAIConfig FindFAIByName(string faiName) {
            if (string.IsNullOrEmpty(faiName) || pSeq == null) return null;
            for (int i = 0; i < pSeq.Count; i++) {
                var seq = pSeq[i];
                if (seq == null) continue;
                for (int j = 0; j < seq.ActionCount; j++) {
                    var act = seq[j];
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
            LoadAndDisplay(param, param as IOfflineImageParam);
        }

        // 표시용(displayParam)과 경로 persistence(pathSinkParam) 분리.
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
                if (pathSinkParam != null) {
                    // DualImage + 세로 토글 활성 시 TeachingImagePath_Vertical 로 저장.
                    //  기본 동작 (가로 토글 활성 또는 1-image algorithm) = SetLatestImagePath → TeachingImagePath.
                    //  세로 토글 활성 + DualImage 한정 분기 (DatumConfig.cs 변경 0 가드 유지 위해 외부에서 property 직접 설정).
                    DatumConfig datumSink = pathSinkParam as DatumConfig;
                    if (datumSink != null
                        && datumSink.AlgorithmTypeEnum == EDatumAlgorithm.VerticalTwoHorizontalDualImage
                        && _currentImageSource == ReringProject.Sequence.EImageSource.Vertical) {
                        datumSink.TeachingImagePath_Vertical = dialog.FileName;
                    }
                    else {
                        pathSinkParam.SetLatestImagePath(dialog.FileName); // 기본 (가로 또는 1-image)
                    }
                }
                // Shot 노드 Load 시 _image 버퍼 동기화.
                //  ReferenceEquals(displayParam, pathSinkParam) == true ⇔ 사용자가 Shot 노드에서 Load → 캐시 갱신.
                //  Datum 노드 Load → DatumConfig.TeachingImagePath 만 기록, Shot 캐시 무오염 (TeachingImagePath != SimulImagePath 분리 구조 보존).
                if (displayParam is ShotConfig shot && ReferenceEquals(displayParam, pathSinkParam)) {
                    HImage currentImg = halconViewer.CurrentImage;
                    if (currentImg != null) {
                        shot.SetImage(currentImg); // SetImage 내부 CopyImage 로 소유권 분리
                    }
                }
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

                    // Parent null 안전 의도 보존 (동적 Shot/FAI 대응)
                    string seqName;
                    if (param.Parent != null && param.Parent.Name != null) seqName = param.Parent.Name;
                    else if (context.Source != null && context.Source.Name != null) seqName = context.Source.Name;
                    else seqName = "";
                    foreach (IMainView customView in CustomViewList) {
                        customView.Display(seqName, resultStr, label_message.Foreground, param.OwnerName);
                    }
                    RefreshFAIResultRows();
                    UpdateImageSourceLabel(null, param as ShotConfig);
                });
            }
        }

        public void DisplayActionContext(ActionContext context) {
            ExecuteOnUi(() => {
                RefreshFAIResultRows();
            });
        }

        public void DisplaySequenceContext(SequenceContext context) {
            lock (mDrawInterlock) {
                ExecuteOnUi(() => {
                    DisplayContextToViewer(context, ConvertParamRects(context.ActionParam));
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
                    RefreshFAIResultRows();
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
            _lastPointerRow = e.Y;
            _lastPointerCol = e.X;
        }

        private void UpdatePointerLabel(double x, double y, double? grayValue) {
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
            // 상단 툴바 hover 표시. CurrentImage==null 시 (0,0,null) 발행 — grayValue.HasValue=false 일 때 X/Y 도 N/A 표시.
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
            if (txt_hoverX != null) txt_hoverX.Text = hoverX;
            if (txt_hoverY != null) txt_hoverY.Text = hoverY;
            if (txt_hoverG != null) txt_hoverG.Text = hoverG;
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

            List<RoiDefinition> roiList;
            if (rois == null) roiList = new List<RoiDefinition>();
            else              roiList = rois.ToList();

            if (context.ResultHalconImage != null) {
                try {
                    halconViewer.LoadImage(context.ResultHalconImage);
                    halconViewer.UpdateDisplayState(roiList, context.InspectionOverlays, context.DisplayMessages);
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
                    halconViewer.UpdateDisplayState(roiList, context.InspectionOverlays, context.DisplayMessages);
                    return true;
                }
                catch (Exception ex) {
                    Logging.PrintErrLog((int)ELogType.Error, ex.Message);
                }
            }

            halconViewer.UpdateDisplayState(roiList, context.InspectionOverlays, context.DisplayMessages);
            return true;
        }

        private void MainView_Unloaded(object sender, RoutedEventArgs e) {
            halconViewer.PointerInfoChanged -= HalconViewer_PointerInfoChanged;
            halconViewer.RoiMoveCompleted -= HalconViewer_RoiMoveCompleted;
            halconViewer.RoiDeleteRequested -= HalconViewer_RoiDeleteRequested;
            halconViewer.RoiGeometryChanged -= HalconViewer_RoiGeometryChanged;
        }

        // Datum.* RoiId 이면 FAI 분기 진입 전 먼저 처리, 아니면 FAI 경로.
        private void HalconViewer_RoiGeometryChanged(object sender, RoiGeometryChangedArgs e) {
            if (e == null || string.IsNullOrEmpty(e.RoiId)) return;

            DatumConfig datum;
            if (e.RoiId.StartsWith("Datum.") && IsCurrentNodeDatum(out datum) && datum != null) {
                HandleDatumRoiResize(datum, e);
                return;
            }

            var fai = FindFAIByName(e.RoiId);
            if (fai == null) return;

            if (e.Shape == RoiShape.Circle) {
                // CircleDiameter + CircleCenterDistance 두 타입 모두 Edit write-back
                foreach (var m in fai.Measurements) {
                    var circle = m as CircleDiameterMeasurement;
                    if (circle != null) {
                        circle.Circle_Row = e.CenterRow;
                        circle.Circle_Col = e.CenterCol;
                        circle.Circle_Radius = e.Radius;
                        break;
                    }
                    var circleCtr = m as CircleCenterDistanceMeasurement;
                    if (circleCtr != null) {
                        circleCtr.Circle_Row = e.CenterRow;
                        circleCtr.Circle_Col = e.CenterCol;
                        circleCtr.Circle_Radius = e.Radius;
                        break;
                    }
                }
            }
            else if (e.Shape == RoiShape.Polygon) {
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

        private void HalconViewer_RoiDeleteRequested(object sender, string roiId) {
            if (string.IsNullOrEmpty(roiId)) return;

            // Datum.* RoiId 이면 FAI 분기 진입 전 먼저 처리
            if (roiId.StartsWith("Datum.")) {
                DatumConfig datum;
                if (IsCurrentNodeDatum(out datum)) {
                    // Delete 는 항상 전체 삭제. 부분 수정은 Edit 모드, 단일 재 그리기는 PropertyGrid 0 입력 → wizard 자동 skip.
                    var choice = CustomMessageBox.ShowConfirmation(
                        "ROI 삭제",
                        "현재 Datum 의 모든 ROI 를 삭제하시겠습니까?",
                        MessageBoxButton.OKCancel);
                    if (choice != MessageBoxResult.OK) return;
                    ClearAllDatumRoiFields(datum);
                    try { datum.RaisePropertyChanged(string.Empty); } catch { }
                    if (mParentWindow != null && mParentWindow.inspectionList != null) mParentWindow.inspectionList.RefreshParamEditor();
                    halconViewer.SetDatumOverlay(datum, true, GetDatumEditMode());
                    PublishDatumRoiCandidates(datum);
                }
                return;
            }

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

        private void HalconViewer_RoiMoveCompleted(object sender, RoiMoveCompletedArgs e) {
            if (e == null || string.IsNullOrEmpty(e.RoiId)) return;

            if (e.RoiId.StartsWith("Datum.")) {
                Logging.PrintLog((int)ELogType.Trace,
                    "Datum ROI move: id=" + e.RoiId + " dr=" + e.DeltaRow + " dc=" + e.DeltaCol);
            }

            DatumConfig datum;
            if (e.RoiId.StartsWith("Datum.") && IsCurrentNodeDatum(out datum)) {
                HandleDatumRoiMove(datum, e);
                return;
            }

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
                // CircleCenterDistance 이동 write-back: RoiGeometryChanged(리사이즈)는 처리하나 RoiMoveCompleted(이동)는 빠져
                //  이동 후 GetCurrentFAIRois 재렌더가 stale Circle_* 로 원복되던 결함 수정.
                var circleCtr = m as CircleCenterDistanceMeasurement;
                if (circleCtr != null) {
                    circleCtr.Circle_Row += e.DeltaRow;
                    circleCtr.Circle_Col += e.DeltaCol;
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

        private bool IsCurrentNodeDatum(out DatumConfig datum) {
            if (mParentWindow != null && mParentWindow.inspectionList != null)
                datum = mParentWindow.inspectionList.SelectedParam as DatumConfig;
            else
                datum = null;
            return datum != null;
        }

        private void HandleDatumRoiMove(DatumConfig datum, RoiMoveCompletedArgs e) {
            ApplyDatumRoiDelta(datum, e);
            try { datum.RaisePropertyChanged(string.Empty); } catch { }
            if (mParentWindow != null && mParentWindow.inspectionList != null) mParentWindow.inspectionList.RefreshParamEditor();
            halconViewer.SetDatumOverlay(datum, true, GetDatumEditMode());
            // 자동 재티칭 없음 (수동 btn_teachDatum 트리거 일원화). PublishDatumRoiCandidates / UpdateDatumRefCoordsLabel 만 inline 호출.
            PublishDatumRoiCandidates(datum);
            UpdateDatumRefCoordsLabel(datum);
        }

        // Datum ROI resize 후처리 (HandleDatumRoiMove 패턴 동일, delta vs absolute 차이만)
        private void HandleDatumRoiResize(DatumConfig datum, RoiGeometryChangedArgs e) {
            if (datum == null || e == null) return;

            if (e.RoiId == "Datum.Circle" && e.Shape == RoiShape.Circle) {
                datum.CircleROI_Row = e.CenterRow;
                datum.CircleROI_Col = e.CenterCol;
                datum.CircleROI_Radius = e.Radius;
            }
            // Rect resize 는 write-back 미구현. silent fall-through 방지: 명시적 log + early return.
            //  향후 Vertical/Horizontal Edit 핸들 노출 시 여기에 write-back 구현 후 return 제거.
            else if (e.Shape == RoiShape.Rect) {
                Logging.PrintLog((int)ELogType.Trace,
                    "Datum Rect resize ignored (Phase 14 scope): id=" + e.RoiId);
                return;
            }

            try { datum.RaisePropertyChanged(string.Empty); } catch { }
            if (mParentWindow != null && mParentWindow.inspectionList != null) mParentWindow.inspectionList.RefreshParamEditor();
            halconViewer.SetDatumOverlay(datum, true, GetDatumEditMode());
            // ROI resize 후 LastTeachSucceeded 변경되지 않음 (stale 시각화 의도적 — 사용자가 mismatch 인지)
            PublishDatumRoiCandidates(datum);
            UpdateDatumRefCoordsLabel(datum);
        }

        private void ApplyDatumRoiDelta(DatumConfig datum, RoiMoveCompletedArgs e) {
            if (datum == null || e == null || string.IsNullOrEmpty(e.RoiId)) return;
            switch (e.RoiId) {
                case "Datum.Line1":
                    datum.Line1_Row += e.DeltaRow; datum.Line1_Col += e.DeltaCol; break;
                case "Datum.Line2":
                    datum.Line2_Row += e.DeltaRow; datum.Line2_Col += e.DeltaCol; break;
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

        // RoiId prefix 별 ROI 필드 0 reset (Length1/Length2/Radius). 그리기 가드(if Length1>0 && Length2>0)에 걸려 시각적으로 사라짐.
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
            // 어느 ROI 든 삭제되면 Datum 자체가 불완전 → 검증 disable
            datum.IsConfigured = false;
            datum.LastTeachSucceeded = false;
        }

        private void ClearAllDatumRoiFields(DatumConfig datum) {
            if (datum == null) return;
            ClearDatumRoiFields(datum, "Datum.Line1");
            ClearDatumRoiFields(datum, "Datum.Line2");
            ClearDatumRoiFields(datum, "Datum.Vertical");
            ClearDatumRoiFields(datum, "Datum.Circle");
            ClearDatumRoiFields(datum, "Datum.HorizontalA");
            ClearDatumRoiFields(datum, "Datum.HorizontalB");
        }

        // 알고리즘이 요구하는 ROI 슬롯 비어 있으면 한국어 에러 메시지 반환. null = OK.
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
                // DualImage 변형 가드: 3 ROI + 2 이미지 경로 모두 검증.
                case EDatumAlgorithm.VerticalTwoHorizontalDualImage:
                    if (d.Vertical_Length1 <= 0)
                        return "Vertical ROI 가 없습니다. 캔버스에 수직 ROI 를 그리고 다시 시도하세요.";
                    if (d.Horizontal_A_Length1 <= 0 || d.Horizontal_B_Length1 <= 0)
                        return "Horizontal A/B ROI 가 없습니다. 캔버스에 ROI 를 그리고 다시 시도하세요.";
                    if (string.IsNullOrEmpty(d.TeachingImagePath))
                        return "가로축 티칭 이미지 경로가 비어 있습니다. Datum 노드에서 가로축 이미지를 Load 해주세요.";
                    if (string.IsNullOrEmpty(d.TeachingImagePath_Vertical))
                        return "세로축 티칭 이미지 경로가 비어 있습니다. Datum 노드에서 세로축 이미지를 Load 해주세요.";
                    break;
            }
            return null;
        }

        // [DatumName] 접두사 포함 티칭 에러 메시지 반환.
        private static string FormatTeachError(DatumConfig datum, string err) {
            if (err == null) err = "unknown";
            string prefix;
            if (datum != null && !string.IsNullOrEmpty(datum.DatumName)) prefix = "[" + datum.DatumName + "] ";
            else                                                         prefix = "";
            if (err.IndexOf("no edges", System.StringComparison.OrdinalIgnoreCase) >= 0
                || err.IndexOf("insufficient edges", System.StringComparison.OrdinalIgnoreCase) >= 0
                || err.IndexOf("insufficient polar samples", System.StringComparison.OrdinalIgnoreCase) >= 0) {
                return prefix + "검출된 에지가 없습니다. EdgeDirection 설정을 반대로 변경한 후 다시 시도하세요.";
            }
            return prefix + "티칭에 실패했습니다: " + err;
        }

        // Test Find 실패 사유 모달 메시지 변환. 검출 0개 케이스에 EdgeDirection 힌트 통합.
        private static string FormatFindError(string err) {
            if (err == null) err = "unknown";
            if (err.IndexOf("no edges", System.StringComparison.OrdinalIgnoreCase) >= 0
                || err.IndexOf("insufficient edges", System.StringComparison.OrdinalIgnoreCase) >= 0
                || err.IndexOf("insufficient polar samples", System.StringComparison.OrdinalIgnoreCase) >= 0) {
                return "검출된 에지가 없습니다. EdgeDirection 설정을 반대로 변경한 후 다시 시도하세요.";
            }
            return "Datum Find 에 실패했습니다: " + err;
        }

        // 자동 재티칭 정책 폐지 (수동 btn_teachDatum 트리거 일원화). 시그니처는 호출처 회귀 방지로 보존.
        public void NotifyDatumParamMaybeChanged(DatumConfig datum) {
            return;
        }

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
                string errMsg;
                if (error != null) errMsg = error;
                else               errMsg = "unknown";
                label_drawHint.Content = "Datum ROI 이동 — 재티칭 실패: " + errMsg;
                label_drawHint.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFF87171"));
                label_drawHint.Visibility = Visibility.Visible;
            }
            halconViewer.SetDatumOverlay(datum, true, GetDatumEditMode());
            Logging.PrintLog((int)ELogType.Trace, "InvokeTryTeachDatumForEdit EXIT: LastTeachSucceeded=" + datum.LastTeachSucceeded);
        }

        // 검출 라인/십자/원 좌표(Line1Detected_*/CircleCenter_*/RefOrigin*)는 INI에 0으로만 저장되는 휘발성 필드 →
        //  레시피 로드 후 LastTeachSucceeded=true 라도 좌표가 0 → 라인이 (0,0)에 그려져 안 보임.
        //  이미 티칭된 datum 에 한해 티칭 이미지로 TryTeachDatum 을 조용히 재실행해 좌표 복원 후 렌더.
        //  편집/신규 티칭 정책(수동 티칭)은 무변경 — 본 메서드는 "보기 선택" 복원 전용(모달/상태 변경 없음).
        public void RestoreDatumOverlayFromTeach(DatumConfig datum) {
            if (datum == null) return;
            if (!datum.LastTeachSucceeded) {
                // 티칭 이력 없음 — 복원 불필요. 기존 selection 핸들러의 SetDatumOverlay 가 ROI 만 렌더.
                return;
            }
            TryRestoreDatumGeometry(datum);
            // 복원 성공/실패 무관 렌더 (성공 시 검출 라인 표시, 실패 시 최소 ROI 표시)
            halconViewer.SetDatumOverlay(datum, true, GetDatumEditMode());
        }

        // 휘발성 검출 좌표 복원 계산 전용 (RestoreDatumOverlayFromTeach / ShowResultDatumOverlays 공용).
        //  티칭 이미지로 TryTeachDatum 을 조용히 재실행. 렌더/모달/상태 변경 없음. 실패 silent.
        private void TryRestoreDatumGeometry(DatumConfig datum) {
            if (datum == null) return;
            if (!datum.LastTeachSucceeded) return;
            try {
                var svc = new ReringProject.Halcon.Algorithms.DatumFindingService();
                string error = null;
                if (datum.AlgorithmTypeEnum == EDatumAlgorithm.VerticalTwoHorizontalDualImage) {
                    string pathH = datum.TeachingImagePath;
                    string pathV = datum.TeachingImagePath_Vertical;
                    if (string.IsNullOrEmpty(pathH) || !System.IO.File.Exists(pathH)) return;
                    if (string.IsNullOrEmpty(pathV) || !System.IO.File.Exists(pathV)) return;
                    HImage imgH = null, imgV = null;
                    try {
                        try { imgH = new HImage(pathH); } catch { return; }
                        try { imgV = new HImage(pathV); } catch { return; }
                        svc.TryTeachDatum(imgH, imgV, datum, out error);
                    }
                    finally {
                        if (imgH != null) { try { imgH.Dispose(); } catch { } }
                        if (imgV != null) { try { imgV.Dispose(); } catch { } }
                    }
                }
                else {
                    // 단일 이미지: TeachingImagePath 파일 우선 로드(결정적), 없으면 현재 표시 이미지 fallback.
                    HImage img = null;
                    bool ownImg = false;
                    string path = datum.TeachingImagePath;
                    if (!string.IsNullOrEmpty(path) && System.IO.File.Exists(path)) {
                        try { img = new HImage(path); ownImg = true; } catch { img = null; }
                    }
                    if (img == null) img = halconViewer.CurrentImage; // fallback: 소유권 없음 — dispose 금지
                    if (img == null) return;
                    try {
                        svc.TryTeachDatum(img, datum, out error);
                    }
                    finally {
                        if (ownImg && img != null) { try { img.Dispose(); } catch { } }
                    }
                }
            }
            catch {
                // 복원 실패는 조용히 무시 — 선택 시 모달/에러 표시 금지.
            }
        }

        // 측정/Shot/FAI 노드 선택 시 그 시퀀스 datum 기준선을 결과 화면에 표시.
        //  각 datum 의 휘발 좌표를 silent 복원 후 결과용 오버레이 리스트로 일괄 렌더. 단일 _datumConfig(Datum 편집)는 무오염.
        //  null/빈 → 결과 오버레이 클리어. 측정 결과 이미지는 그대로 두고 datum 라인만 덧그림.
        public void ShowResultDatumOverlays(List<DatumConfig> datums) {
            if (datums == null || datums.Count == 0) {
                halconViewer.ClearResultDatumOverlays();
                return;
            }
            foreach (DatumConfig d in datums) {
                TryRestoreDatumGeometry(d); // 휘발 좌표 복원 (렌더는 아래 일괄 호출)
            }
            halconViewer.SetResultDatumOverlays(datums);
        }

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

        private void UpdateImageSourceLabel(DatumConfig datumConfig, ShotConfig shotConfig) {
            if (txt_imageSourceLabel == null) return;
            if (datumConfig != null && !string.IsNullOrEmpty(datumConfig.TeachingImagePath)) {
                txt_imageSourceLabel.Text = "티칭 이미지: " + datumConfig.TeachingImagePath;
                txt_imageSourceLabel.Visibility = Visibility.Visible;
                return;
            }
            if (shotConfig != null && !string.IsNullOrEmpty(shotConfig.SimulImagePath)) {
                txt_imageSourceLabel.Text = "검사 이미지: " + shotConfig.SimulImagePath;
                txt_imageSourceLabel.Visibility = Visibility.Visible;
                return;
            }
            txt_imageSourceLabel.Text = string.Empty;
            txt_imageSourceLabel.Visibility = Visibility.Collapsed;
        }

        // 자동/수동 swap 의 단일 진입점: _currentImageSource + 배지 텍스트/색상 + ROI 가시성 3자 동시 갱신.
        private void UpdateImageSourceBadge(ReringProject.Sequence.EImageSource source) {
            _currentImageSource = source;

            // (b) 배지 텍스트 + 색상. 정적 frozen brush + SetCurrentValue 로 WPF 갱신 보장.
            if (border_imageSourceBadge != null && txt_imageSourceBadge != null) {
                if (source == ReringProject.Sequence.EImageSource.Horizontal) {
                    // Measurement DualImage 선택 시 명시 경로 vs fallback 여부에 따라 배지 텍스트 보강. Datum 또는 null → 기존 "가로축" 보존.
                    string horizontalBadgeText = "가로축"; // 기본값 (Datum 또는 비-DualImage selection)
                    var measForBadge = _selectedDualImageMeasurement;
                    if (measForBadge != null) {
                        bool hasExplicitHorizontal = !string.IsNullOrEmpty(measForBadge.TeachingImagePath_Horizontal)
                            && System.IO.File.Exists(measForBadge.TeachingImagePath_Horizontal);
                        if (hasExplicitHorizontal)
                            horizontalBadgeText = "가로축 (Measurement)";
                        else
                            horizontalBadgeText = "가로축 (Shot fallback)";
                    }
                    txt_imageSourceBadge.SetCurrentValue(TextBlock.TextProperty, horizontalBadgeText);
                    border_imageSourceBadge.SetCurrentValue(Border.BackgroundProperty, BadgeBrushHorizontal);
                }
                else {
                    txt_imageSourceBadge.SetCurrentValue(TextBlock.TextProperty, "세로축");
                    border_imageSourceBadge.SetCurrentValue(Border.BackgroundProperty, BadgeBrushVertical);
                }
                border_imageSourceBadge.InvalidateVisual(); // 즉시 재렌더 강제
            }

            // 토글 버튼 IsChecked 동기화 (수동 클릭 / 자동 swap 양방향) — 한쪽만 체크 (radio 패턴)
            if (btn_swapHorizontal != null) btn_swapHorizontal.IsChecked = (source == ReringProject.Sequence.EImageSource.Horizontal);
            if (btn_swapVertical   != null) btn_swapVertical.IsChecked   = (source == ReringProject.Sequence.EImageSource.Vertical);

            // (c) ROI 가시성 — 현재 선택된 datum 이 DualImage 일 때만 subset 필터 적용.
            var datumForRoi = _selectedDatumForSwap;
            if (datumForRoi != null
                && datumForRoi.AlgorithmTypeEnum == EDatumAlgorithm.VerticalTwoHorizontalDualImage) {
                PublishDatumRoiCandidates(datumForRoi);
            }
            // swap 후 DisplayMeasurementImage 를 재호출하면 Shot 이미지로 덮어쓰는 회귀 발생.
            //  이미지는 BtnSwap*_Click 가 이미 LoadImage 로 적용 완료 → ROI 하이라이트 + overlay 재 렌더 + circle preview 만 호출.
            //  mutex: PublishDatumRoiCandidates 가 Datum 선택 시 _selectedDualImageMeasurement = null 로 clear → Datum 분기 진입 X.
            if (_selectedDualImageMeasurement != null) {
                HighlightSelectedRoi(_selectedDualImageMeasurement);
                RenderStoredOverlaysForMeasurement(_selectedDualImageMeasurement);
                UpdateFaiCirclePreview(_selectedDualImageMeasurement);
            }
        }

        // 보조 hook: DatumName 등 RaisePropertyChanged 가 fire 하는 property 대응.
        //  AlgorithmType 은 DatumConfig auto property 라 PropertyChanged 미발동 — 본 hook 으로는 못 잡음.
        //  AlgorithmType 변경 시 Visibility 갱신은 InspectionListView.OnParamEditorSelectionChanged whitelist 확장으로 처리.
        //  본 메서드는 RaisePropertyChanged(string.Empty) 대량 갱신 시점에 ROI/Visibility 정합성 보장하는 보호망.
        private void OnSelectedDatumPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e) {
            if (e == null) return;
            var datum = sender as DatumConfig;
            if (datum == null) return;
            // 무차별 재호출 시 무한루프 우려 → string.Empty (bulk) 또는 "AlgorithmType" 만 통과
            if (!string.IsNullOrEmpty(e.PropertyName) && e.PropertyName != "AlgorithmType") return;
            PublishDatumRoiCandidates(datum);
        }

        // 가로축 토글 버튼 Click. 가로축 이미지로 swap + 배지/ROI 갱신.
        //  _selectedDatumForSwap 사용 — teach 모드 진입 전에도 동작.
        private void BtnSwapHorizontal_Click(object sender, RoutedEventArgs e) {
            // Measurement DualImage 가 Datum 보다 우선 (mutex)
            {
                var meas = _selectedDualImageMeasurement;
                if (meas != null) {
                    // Measurement 명시 경로 우선, 없으면 ShotConfig fallback.
                    // CS0136 회피 위해 `hpathMeas` 명명 (outer scope 의 'hpath' 와 충돌 회피).
                    string hpathMeas = meas.TeachingImagePath_Horizontal;
                    if (!string.IsNullOrEmpty(hpathMeas) && System.IO.File.Exists(hpathMeas)) {
                        try { halconViewer.LoadImage(hpathMeas); }
                        catch (Exception ex) { Logging.PrintErrLog((int)ELogType.Error, ex.Message); }
                    }
                    else {
                        // fallback: ShotConfig 이미지
                        FAIConfig fai = FindFAIContainingMeasurement(meas);
                        ShotConfig shot = null;
                        if (fai != null)
                            shot = fai.Owner as ShotConfig;
                        if (shot != null && shot.HasImage) {
                            HImage img = null;
                            try {
                                img = shot.GetImage();
                                if (img != null) halconViewer.LoadImage(img);
                            }
                            catch (Exception ex) { Logging.PrintErrLog((int)ELogType.Error, ex.Message); }
                            finally { if (img != null) img.Dispose(); }
                        }
                    }
                    UpdateImageSourceBadge(ReringProject.Sequence.EImageSource.Horizontal);
                    return; // Measurement 경로 종결, Datum 코드 진입 X
                }
            }
            var d = _selectedDatumForSwap;
            if (d == null) return;
            if (d.AlgorithmTypeEnum != EDatumAlgorithm.VerticalTwoHorizontalDualImage) return;
            string hpath = d.TeachingImagePath;
            if (!string.IsNullOrEmpty(hpath) && System.IO.File.Exists(hpath)) {
                try { halconViewer.LoadImage(hpath); }
                catch (Exception ex) { Logging.PrintErrLog((int)ELogType.Error, ex.Message); }
            }
            // 배지/버튼/현재축 3자 동시 갱신
            UpdateImageSourceBadge(ReringProject.Sequence.EImageSource.Horizontal);
            // swap 직후 RenderDatumFindResult 자동 재실행 (chain: SetDatumOverlay → RenderDatumOverlay → RenderDatumFindResult)
            halconViewer.SetDatumOverlay(_selectedDatumForSwap, true, GetDatumEditMode());
        }

        // 세로축 토글 버튼 Click. 세로축 이미지로 swap + 배지/ROI 갱신.
        //  _selectedDatumForSwap 사용 — teach 모드 진입 전에도 동작.
        private void BtnSwapVertical_Click(object sender, RoutedEventArgs e) {
            // Measurement DualImage 가 Datum 보다 우선 (mutex)
            {
                var meas = _selectedDualImageMeasurement;
                if (meas != null) {
                    // outer scope (Datum 분기) 의 'vpath' 와 충돌 회피 → vpathMeas 로 명명.
                    string vpathMeas = meas.TeachingImagePath_Vertical;
                    if (!string.IsNullOrEmpty(vpathMeas) && System.IO.File.Exists(vpathMeas)) {
                        try { halconViewer.LoadImage(vpathMeas); }
                        catch (Exception ex) { Logging.PrintErrLog((int)ELogType.Error, ex.Message); }
                    }
                    UpdateImageSourceBadge(ReringProject.Sequence.EImageSource.Vertical);
                    return; // Measurement 경로 종결
                }
            }
            var d = _selectedDatumForSwap;
            if (d == null) return;
            if (d.AlgorithmTypeEnum != EDatumAlgorithm.VerticalTwoHorizontalDualImage) return;
            string vpath = d.TeachingImagePath_Vertical;
            if (!string.IsNullOrEmpty(vpath) && System.IO.File.Exists(vpath)) {
                try { halconViewer.LoadImage(vpath); }
                catch (Exception ex) { Logging.PrintErrLog((int)ELogType.Error, ex.Message); }
            }
            // 배지/버튼/현재축 3자 동시 갱신
            UpdateImageSourceBadge(ReringProject.Sequence.EImageSource.Vertical);
            // Vertical 토글 시에도 RenderDatumFindResult chain 트리거 (SameFrame 가정 하 양쪽 캔버스 동일 좌표)
            halconViewer.SetDatumOverlay(_selectedDatumForSwap, true, GetDatumEditMode());
        }

        // DatumConfig → RoiDefinition 리스트 → halconViewer.SetDatumRoiCandidates publish (InspectionListView selection 시 호출)
        public void PublishDatumRoiCandidates(DatumConfig datum) {
            // selection 시점에 reference 좌표 라벨도 동기 갱신
            UpdateDatumRefCoordsLabel(datum);

            // mutex: Datum 노드 선택 시 Measurement clear (PublishMeasurementDualImageSelection 와 대칭)
            if (datum != null && _selectedDualImageMeasurement != null) {
                _selectedDualImageMeasurement = null;
            }

            // datum reference 캐싱 + AlgorithmType PropertyChanged 구독.
            //  swap 토글 핸들러가 teach 모드 전에도 동작하려면 별도 reference 필요 (_editingDatum 은 Teach Datum 클릭 시에만 set).
            //  AlgorithmType 이 PropertyGrid 에서 변경되면 PropertyChanged → 본 메서드 재귀 호출 → Visibility 즉시 갱신.
            DatumConfig priorSelected = _selectedDatumForSwap; // 노드 변경 감지용 prior reference
            if (_selectedDatumForSwap != datum) {
                if (_selectedDatumForSwap != null) _selectedDatumForSwap.PropertyChanged -= OnSelectedDatumPropertyChanged;
                _selectedDatumForSwap = datum;
                if (_selectedDatumForSwap != null) _selectedDatumForSwap.PropertyChanged += OnSelectedDatumPropertyChanged;
            }

            // Datum 노드 (재)선택 시: swap 상태 = 기본 가로축 리셋 (세션 한정).
            // Visibility 동기화: DualImage 변형이면 토글 버튼 + 배지 Visible, 1-image 변형 / null 이면 Collapsed.
            bool isDualImage = (datum != null
                                && datum.AlgorithmTypeEnum == EDatumAlgorithm.VerticalTwoHorizontalDualImage);
            if (btn_swapHorizontal != null)
            {
                if (isDualImage) btn_swapHorizontal.Visibility = Visibility.Visible;
                else btn_swapHorizontal.Visibility = Visibility.Collapsed;
            }
            if (btn_swapVertical != null)
            {
                if (isDualImage) btn_swapVertical.Visibility = Visibility.Visible;
                else btn_swapVertical.Visibility = Visibility.Collapsed;
            }
            if (border_imageSourceBadge != null)
            {
                if (isDualImage) border_imageSourceBadge.Visibility = Visibility.Visible;
                else border_imageSourceBadge.Visibility = Visibility.Collapsed;
            }

            if (isDualImage) {
                // Datum 노드 선택 직후 = 가로축 기본. 단, 본 메서드가 자동/수동 swap 도중 재호출되면 _currentImageSource 가 이미 변경되어 있을 수 있음.
                // 진입점 구분: priorSelected != datum 이면 새 노드 선택 → 리셋. 같으면 swap 진행 중 / AlgorithmType 변경 → 보존.
                if (priorSelected != datum) {
                    _currentImageSource = ReringProject.Sequence.EImageSource.Horizontal;
                    // 배지 텍스트/색상도 가로축으로 동기 (별도 UpdateImageSourceBadge 호출 없이 직접 — 재귀 회피)
                    // 정적 frozen brush + SetCurrentValue (WPF Background 즉시 반영 보장)
                    if (txt_imageSourceBadge != null) txt_imageSourceBadge.SetCurrentValue(TextBlock.TextProperty, "가로축");
                    if (border_imageSourceBadge != null) border_imageSourceBadge.SetCurrentValue(Border.BackgroundProperty, BadgeBrushHorizontal);
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
                    if (datum.Vertical_Length1 > 0 && datum.Vertical_Length2 > 0)
                        list.Add(BuildDatumRectCandidate("Datum.Vertical", datum.Vertical_Row, datum.Vertical_Col, datum.Vertical_Length1, datum.Vertical_Length2));
                    if (datum.Horizontal_A_Length1 > 0 && datum.Horizontal_A_Length2 > 0)
                        list.Add(BuildDatumRectCandidate("Datum.HorizontalA", datum.Horizontal_A_Row, datum.Horizontal_A_Col, datum.Horizontal_A_Length1, datum.Horizontal_A_Length2));
                    if (datum.Horizontal_B_Length1 > 0 && datum.Horizontal_B_Length2 > 0)
                        list.Add(BuildDatumRectCandidate("Datum.HorizontalB", datum.Horizontal_B_Row, datum.Horizontal_B_Col, datum.Horizontal_B_Length1, datum.Horizontal_B_Length2));
                    break;
                // VerticalTwoHorizontalDualImage: 모든 ROI 항상 표시 (축별 subset 토글은 ROI 위치 파악/삭제/이동 시 사용성 저해).
                //  좌표계 불일치 (가로 이미지 위에 Vertical ROI misalign 가능) 는 SIMUL 의사 페어 한계로 CO-34.1-01 종결.
                case EDatumAlgorithm.VerticalTwoHorizontalDualImage:
                    if (datum.Horizontal_A_Length1 > 0 && datum.Horizontal_A_Length2 > 0)
                        list.Add(BuildDatumRectCandidate("Datum.HorizontalA", datum.Horizontal_A_Row, datum.Horizontal_A_Col, datum.Horizontal_A_Length1, datum.Horizontal_A_Length2));
                    if (datum.Horizontal_B_Length1 > 0 && datum.Horizontal_B_Length2 > 0)
                        list.Add(BuildDatumRectCandidate("Datum.HorizontalB", datum.Horizontal_B_Row, datum.Horizontal_B_Col, datum.Horizontal_B_Length1, datum.Horizontal_B_Length2));
                    if (datum.Vertical_Length1 > 0 && datum.Vertical_Length2 > 0)
                        list.Add(BuildDatumRectCandidate("Datum.Vertical", datum.Vertical_Row, datum.Vertical_Col, datum.Vertical_Length1, datum.Vertical_Length2));
                    break;
            }
            halconViewer.SetDatumRoiCandidates(list);
        }

        // PublishDatumRoiCandidates 대칭. Measurement DualImage 노드 선택 시 swap UI owner set + 가로축 리셋 + Visibility 제어.
        //  mutex: 본 메서드와 PublishDatumRoiCandidates 는 _selectedDatumForSwap / _selectedDualImageMeasurement 중 하나만 non-null 임을 보장.
        public void PublishMeasurementDualImageSelection(DualImageEdgeDistanceMeasurement meas) {
            _selectedDualImageMeasurement = meas;

            // mutex: Measurement set 시 Datum clear (동시 선택 상태 차단)
            if (meas != null && _selectedDatumForSwap != null) {
                _selectedDatumForSwap.PropertyChanged -= OnSelectedDatumPropertyChanged;
                _selectedDatumForSwap = null;
            }

            bool isDualImage = (meas != null);
            if (btn_swapHorizontal != null)
            {
                if (isDualImage) btn_swapHorizontal.Visibility = Visibility.Visible;
                else btn_swapHorizontal.Visibility = Visibility.Collapsed;
            }
            if (btn_swapVertical != null)
            {
                if (isDualImage) btn_swapVertical.Visibility = Visibility.Visible;
                else btn_swapVertical.Visibility = Visibility.Collapsed;
            }
            if (border_imageSourceBadge != null)
            {
                if (isDualImage) border_imageSourceBadge.Visibility = Visibility.Visible;
                else border_imageSourceBadge.Visibility = Visibility.Collapsed;
            }

            if (isDualImage) {
                // 새 노드 진입 시 가로축 리셋 (Datum 대칭)
                _currentImageSource = ReringProject.Sequence.EImageSource.Horizontal;
                UpdateImageSourceBadge(ReringProject.Sequence.EImageSource.Horizontal);
            }
        }

        // Rectangle2 (centerRow, centerCol, phi=0, halfH, halfW) → bbox
        private static ReringProject.Halcon.Models.RoiDefinition BuildDatumRectCandidate(string id, double centerRow, double centerCol, double halfH, double halfW) {
            return new ReringProject.Halcon.Models.RoiDefinition {
                Id = id, Name = id,
                Shape = ReringProject.Halcon.Models.RoiShape.Rect,
                Row1 = centerRow - halfH, Row2 = centerRow + halfH,
                Column1 = centerCol - halfW, Column2 = centerCol + halfW,
                IsTaught = true
            };
        }

        // Circle → RoiDefinition
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

        private void MainView_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e) {
            if (e.Key == System.Windows.Input.Key.Escape && _canvasMode != ECanvasMode.None) {
                ExitCanvasMode();
                e.Handled = true;
            }
        }

        private void ExitCanvasMode() {
            // Unsubscribe Halcon 브릿지 이벤트 (safe to call even if not subscribed)
            halconViewer.ImageLeftClicked -= HalconViewer_PolygonMouseDown;
            halconViewer.ImageRightClicked -= HalconViewer_PolygonRightClick;
            halconViewer.RectDrawingCompleted -= HalconViewer_RectDrawingCompleted;
            halconViewer.CircleDrawingCompleted -= HalconViewer_CircleDrawingCompleted;
            // Datum 티칭 핸들러 unsubscribe (Double-subscribe 방지)
            halconViewer.RectDrawingCompleted   -= HalconViewer_DatumRectCompleted;
            halconViewer.CircleDrawingCompleted -= HalconViewer_DatumCircleCompleted;
            // 패턴 ROI 핸들러 unsubscribe (260618 hbk Phase 54 ALIGN-01)
            halconViewer.RectDrawingCompleted   -= HalconViewer_PatternRectCompleted;
            //260619 hbk Phase 55 ALIGN-02 패턴 2 핸들러 unsubscribe
            halconViewer.RectDrawingCompleted   -= HalconViewer_PatternRect2Completed;
            // 직선(tilt) ROI 핸들러 unsubscribe (260618 hbk Phase 54 ALIGN-01)
            halconViewer.RectDrawingCompleted   -= HalconViewer_AlignLineRectCompleted;

            _canvasMode = ECanvasMode.None;
            _editingFai = null;
            _editingCircleMeasurement = null;
            _editingCircleFaiName = null;
            _editingMeasurement = null;
            _editingMeasurementFaiName = null;
            _editingDatum = null;
            btn_rectRoi.IsChecked = false;
            btn_polygonRoi.IsChecked = false;
            btn_circleRoi.IsChecked = false;
            btn_teachDatum.IsChecked = false;
            label_drawHint.Visibility = Visibility.Collapsed;
            label_pointCount.Visibility = Visibility.Collapsed;
            halconViewer.ClearPolygonDraft();
            _polygonPoints.Clear();

            // Calibration cleanup
            halconViewer.ImageLeftClicked -= HalconViewer_CalibrationMouseDown;
            halconViewer.ClearCalibrationOverlay();
            btn_calibrate.Content = "Calibrate";
            _calibrationPoints.Clear();
        }

        private void RectRoiButton_Click(object sender, RoutedEventArgs e) {
            if (btn_rectRoi.IsChecked == true) {
                ExitCanvasMode();
                _canvasMode = ECanvasMode.RectRoi;
                btn_rectRoi.IsChecked = true;

                // Measurement 노드 선택 시 Point_* 보유 타입 우선 처리 (FAIConfig 해석보다 우선)
                MeasurementBase measTarget = FindSelectedRectMeasurement();
                if (measTarget != null) {
                    _editingMeasurement = measTarget;
                    // 이전 미완료 티칭 잔존 인덱스 오염 방지
                    _editingMeasurementRoiIndex = 0;
                    var selRowForMeas = dataGrid_faiResults.SelectedItem as MeasurementResultRow;
                    if (selRowForMeas != null) _editingMeasurementFaiName = selRowForMeas.FAIName;
                    else _editingMeasurementFaiName = FindFaiNameContainingMeasurement(_editingMeasurement);
                    // ArcLineIntersect 순차 4-ROI UX: 첫 드로잉은 교점1 EdgeA1(수직 에지)
                    if (measTarget is ArcLineIntersectDistanceMeasurement)
                        label_drawHint.Content = "교점1 수직 에지(EdgeA1) ROI 를 드래그하세요";
                    else
                        label_drawHint.Content = "드래그하여 Measurement Point ROI를 설정하세요";
                    label_drawHint.Visibility = Visibility.Visible;
                    halconViewer.RectDrawingCompleted += HalconViewer_RectDrawingCompleted;
                    halconViewer.StartRectangleDrawing();
                    return;
                }

                // 신규 FAI(Measurement 0개): 트리 선택을 우선 사용, dataGrid 는 fallback
                FAIConfig faiToEdit = null;
                if (mParentWindow != null && mParentWindow.inspectionList != null)
                    faiToEdit = mParentWindow.inspectionList.SelectedParam as FAIConfig;
                if (faiToEdit == null) {
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
                halconViewer.RectDrawingCompleted += HalconViewer_RectDrawingCompleted;
                halconViewer.StartRectangleDrawing();
            }
            else {
                CommitRectRoi();
            }
        }

        private void HalconViewer_RectDrawingCompleted(object sender, EventArgs e) {
            halconViewer.RectDrawingCompleted -= HalconViewer_RectDrawingCompleted;
            CommitRectRoi();
        }

        private void CommitRectRoi() {
            if (_canvasMode != ECanvasMode.RectRoi) {
                ExitCanvasMode();
                return;
            }

            // Measurement 분기 우선: MeasurementBase 타입별 as 캐스트로 Point_* / LineROI_* 설정
            if (_editingMeasurement != null) {
                var measRoi = halconViewer.CommitActiveRectangle();
                if (measRoi != null) {
                    double mCenterRow = (measRoi.Row1 + measRoi.Row2) / 2.0;
                    double mCenterCol = (measRoi.Column1 + measRoi.Column2) / 2.0;
                    double mHalfHeight = (measRoi.Row2 - measRoi.Row1) / 2.0;
                    double mHalfWidth = (measRoi.Column2 - measRoi.Column1) / 2.0;
                    var etld = _editingMeasurement as EdgeToLineDistanceMeasurement;
                    if (etld != null) { etld.Point_Row = mCenterRow; etld.Point_Col = mCenterCol; etld.Point_Phi = 0.0; etld.Point_Length1 = mHalfHeight; etld.Point_Length2 = mHalfWidth; }
                    var etla = _editingMeasurement as EdgeToLineAngleMeasurement;
                    if (etla != null) { etla.Point_Row = mCenterRow; etla.Point_Col = mCenterCol; etla.Point_Phi = 0.0; etla.Point_Length1 = mHalfHeight; etla.Point_Length2 = mHalfWidth; }
                    var aed = _editingMeasurement as ArcEdgeDistanceMeasurement;
                    if (aed != null) { aed.Point_Row = mCenterRow; aed.Point_Col = mCenterCol; aed.Point_Phi = 0.0; aed.Point_Length1 = mHalfHeight; aed.Point_Length2 = mHalfWidth; }
                    // Compound 4종 단일 Rect ROI write-back (Rect_* 필드명)
                    var cAngle = _editingMeasurement as CompoundAngleMeasurement;
                    if (cAngle != null) { cAngle.Rect_Row = mCenterRow; cAngle.Rect_Col = mCenterCol; cAngle.Rect_Phi = 0.0; cAngle.Rect_Length1 = mHalfHeight; cAngle.Rect_Length2 = mHalfWidth; }
                    var cCenterC = _editingMeasurement as CompoundCenterCDistanceMeasurement;
                    if (cCenterC != null) { cCenterC.Rect_Row = mCenterRow; cCenterC.Rect_Col = mCenterCol; cCenterC.Rect_Phi = 0.0; cCenterC.Rect_Length1 = mHalfHeight; cCenterC.Rect_Length2 = mHalfWidth; }
                    var cCenterB = _editingMeasurement as CompoundCenterBDistanceMeasurement;
                    if (cCenterB != null) { cCenterB.Rect_Row = mCenterRow; cCenterB.Rect_Col = mCenterCol; cCenterB.Rect_Phi = 0.0; cCenterB.Rect_Length1 = mHalfHeight; cCenterB.Rect_Length2 = mHalfWidth; }
                    var cShort = _editingMeasurement as CompoundShortAxisDistanceMeasurement;
                    if (cShort != null) { cShort.Rect_Row = mCenterRow; cShort.Rect_Col = mCenterCol; cShort.Rect_Phi = 0.0; cShort.Rect_Length1 = mHalfHeight; cShort.Rect_Length2 = mHalfWidth; }
                    // DualImage: _currentImageSource 슬롯에 따라 PointROI(가로) vs LineROI(세로) 라우팅 — 보이는 이미지의 ROI 만 그림
                    var dualMeas = _editingMeasurement as DualImageEdgeDistanceMeasurement;
                    if (dualMeas != null) {
                        if (_currentImageSource == ReringProject.Sequence.EImageSource.Vertical) {
                            // 세로 슬롯 표시 중 → LineROI 라우팅
                            dualMeas.LineROI_Row = mCenterRow; dualMeas.LineROI_Col = mCenterCol; dualMeas.LineROI_Phi = 0.0;
                            dualMeas.LineROI_Length1 = mHalfHeight; dualMeas.LineROI_Length2 = mHalfWidth;
                        } else {
                            // 가로 슬롯 표시 중 (기본 Horizontal) → PointROI 라우팅
                            dualMeas.PointROI_Row = mCenterRow; dualMeas.PointROI_Col = mCenterCol; dualMeas.PointROI_Phi = 0.0;
                            dualMeas.PointROI_Length1 = mHalfHeight; dualMeas.PointROI_Length2 = mHalfWidth;
                        }
                    }
                    // ArcLineIntersect 순차 4-ROI 드로잉: 인덱스 0=EdgeA1, 1=EdgeB1, 2=EdgeA2, 3=EdgeB2
                    var ali = _editingMeasurement as ArcLineIntersectDistanceMeasurement;
                    if (ali != null) {
                        if (_editingMeasurementRoiIndex == 0) {
                            ali.EdgeA1_Row = mCenterRow; ali.EdgeA1_Col = mCenterCol; ali.EdgeA1_Phi = 0.0;
                            ali.EdgeA1_Length1 = mHalfHeight; ali.EdgeA1_Length2 = mHalfWidth;
                            _editingMeasurementRoiIndex = 1;
                            label_drawHint.Content = "교점1 수평 에지(EdgeB1) ROI 를 드래그하세요";
                            halconViewer.RectDrawingCompleted += HalconViewer_RectDrawingCompleted;
                            halconViewer.StartRectangleDrawing();
                            return;
                        }
                        else if (_editingMeasurementRoiIndex == 1) {
                            ali.EdgeB1_Row = mCenterRow; ali.EdgeB1_Col = mCenterCol; ali.EdgeB1_Phi = 0.0;
                            ali.EdgeB1_Length1 = mHalfHeight; ali.EdgeB1_Length2 = mHalfWidth;
                            _editingMeasurementRoiIndex = 2;
                            label_drawHint.Content = "교점2 수직 에지(EdgeA2) ROI 를 드래그하세요";
                            halconViewer.RectDrawingCompleted += HalconViewer_RectDrawingCompleted;
                            halconViewer.StartRectangleDrawing();
                            return;
                        }
                        else if (_editingMeasurementRoiIndex == 2) {
                            ali.EdgeA2_Row = mCenterRow; ali.EdgeA2_Col = mCenterCol; ali.EdgeA2_Phi = 0.0;
                            ali.EdgeA2_Length1 = mHalfHeight; ali.EdgeA2_Length2 = mHalfWidth;
                            _editingMeasurementRoiIndex = 3;
                            label_drawHint.Content = "교점2 수평 에지(EdgeB2) ROI 를 드래그하세요";
                            halconViewer.RectDrawingCompleted += HalconViewer_RectDrawingCompleted;
                            halconViewer.StartRectangleDrawing();
                            return;
                        }
                        else { // index == 3: 마지막 ROI EdgeB2 — 정상 종결
                            ali.EdgeB2_Row = mCenterRow; ali.EdgeB2_Col = mCenterCol; ali.EdgeB2_Phi = 0.0;
                            ali.EdgeB2_Length1 = mHalfHeight; ali.EdgeB2_Length2 = mHalfWidth;
                        }
                    }
                    string measSelId = _editingMeasurementFaiName;
                    if (string.IsNullOrEmpty(measSelId))
                        measSelId = FindFaiNameContainingMeasurement(_editingMeasurement);
                    FAIConfig anchorFaiForCommit = FindFAIByName(measSelId);
                    var measRois = CollectShotRois(anchorFaiForCommit);
                    halconViewer.UpdateDisplayState(measRois, measSelId, null, null);
                }
                ExitCanvasMode();
                return;
            }

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

                // Measurement.ROI_* 동기화 블록 제거 — EdgePairDistanceMeasurement 가
                // Owner(FAIConfig).ROI_* 를 직접 참조하도록 변경되어 중복 저장이 사라짐.

                // Refresh canvas to show new ROI
                var rois = GetCurrentFAIRois();
                halconViewer.UpdateDisplayState(rois, _editingFai.FAIName, null, null);
            }
            ExitCanvasMode();
        }

        private void CircleRoiButton_Click(object sender, RoutedEventArgs e) {
            if (btn_circleRoi.IsChecked == true) {
                ExitCanvasMode();
                _canvasMode = ECanvasMode.CircleRoi;
                btn_circleRoi.IsChecked = true;

                MeasurementBase target = FindSelectedCircleMeasurement();
                if (target == null) {
                    CustomMessageBox.Show("Circle ROI", "CircleDiameterMeasurement 또는 CircleCenterDistanceMeasurement를 포함한 FAI를 선택하세요.");
                    ExitCanvasMode();
                    return;
                }
                _editingCircleMeasurement = target;
                var selRowForCircle = dataGrid_faiResults.SelectedItem as MeasurementResultRow;
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

        private void HalconViewer_CircleDrawingCompleted(object sender, CircleDrawCompletedArgs e) {
            CommitCircleRoi(e.CenterRow, e.CenterCol, e.Radius);
        }

        private string FindFaiNameContainingMeasurement(MeasurementBase measurement) {
            if (measurement == null || pSeq == null) return null;
            for (int i = 0; i < pSeq.Count; i++) {
                var seq = pSeq[i];
                if (seq == null) continue;
                for (int j = 0; j < seq.ActionCount; j++) {
                    var act = seq[j];
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

        // FindFaiNameContainingMeasurement 의 참조 버전: 이름 충돌(여러 Shot 동일 FAI 명) 시에도
        //  실제 소유 FAIConfig 객체를 ReferenceEquals 로 정확히 반환. 이미지/ROI 해석이 첫 Shot 으로 잘못 묶이는 결함 차단.
        private FAIConfig FindFAIContainingMeasurement(MeasurementBase measurement) {
            if (measurement == null) return null;
            // RecipeManager.Shots(동적 FAI 단일 소스, 신규 Shot 즉시 반영)를 우선 탐색.
            //  AddShotToSequence 는 새 Shot 을 RecipeManager 에만 넣고 라이브 Action(pSeq) 은 실행 시 지연 동기화하므로,
            //  세션 중 pSeq 만 보면 신규 Shot 측정을 못 찾아 이미지/ROI 가 이전 Shot 으로 남는다(재시작 후엔 정상).
            InspectionRecipeManager recipeManager = null;
            if (SystemHandler.Handle != null && SystemHandler.Handle.Sequences != null)
                recipeManager = SystemHandler.Handle.Sequences.RecipeManager;
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
            // fallback: 레거시/로드 경로(측정이 pSeq Action 에만 존재할 수 있음)
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

        private MeasurementBase FindSelectedCircleMeasurement() {
            // 트리 노드 선택 우선 (Measurement 노드 직접 선택 케이스)
            if (mParentWindow != null && mParentWindow.inspectionList != null) {
                var selParam = mParentWindow.inspectionList.SelectedParam;
                if (selParam is CircleDiameterMeasurement) return (MeasurementBase)selParam;
                if (selParam is CircleCenterDistanceMeasurement) return (MeasurementBase)selParam;
            }
            // fallback — dataGrid 행 선택 경로
            var selectedRow = dataGrid_faiResults.SelectedItem as MeasurementResultRow;
            if (selectedRow != null) {
                FAIConfig fai = FindFAIByName(selectedRow.FAIName);
                if (fai != null) {
                    foreach (var m in fai.Measurements) {
                        var circle = m as CircleDiameterMeasurement;
                        if (circle != null) return circle;
                        var circleCtr = m as CircleCenterDistanceMeasurement;
                        if (circleCtr != null) return circleCtr;
                    }
                }
            }
            return null;
        }

        // Point_* ROI 보유 타입 화이트리스트 (트리 노드 선택 우선, FAI 미생성 신규 measurement 포함)
        private MeasurementBase FindSelectedRectMeasurement() {
            if (mParentWindow != null && mParentWindow.inspectionList != null) {
                var selParam = mParentWindow.inspectionList.SelectedParam;
                if (selParam is EdgeToLineDistanceMeasurement) return (MeasurementBase)selParam;
                if (selParam is EdgeToLineAngleMeasurement) return (MeasurementBase)selParam;
                if (selParam is ArcEdgeDistanceMeasurement) return (MeasurementBase)selParam;
                if (selParam is ArcLineIntersectDistanceMeasurement) return (MeasurementBase)selParam;
                if (selParam is CompoundAngleMeasurement) return (MeasurementBase)selParam;
                if (selParam is CompoundCenterCDistanceMeasurement) return (MeasurementBase)selParam;
                if (selParam is CompoundCenterBDistanceMeasurement) return (MeasurementBase)selParam;
                if (selParam is CompoundShortAxisDistanceMeasurement) return (MeasurementBase)selParam;
                if (selParam is DualImageEdgeDistanceMeasurement) return (MeasurementBase)selParam;
            }
            // fallback — dataGrid 행 선택 경로
            var selectedRow = dataGrid_faiResults.SelectedItem as MeasurementResultRow;
            if (selectedRow != null) {
                FAIConfig fai = FindFAIByName(selectedRow.FAIName);
                if (fai != null) {
                    foreach (var m in fai.Measurements) {
                        if (m is EdgeToLineDistanceMeasurement) return m;
                        if (m is EdgeToLineAngleMeasurement) return m;
                        if (m is ArcEdgeDistanceMeasurement) return m;
                        if (m is ArcLineIntersectDistanceMeasurement) return m;
                        if (m is CompoundAngleMeasurement) return m;
                        if (m is CompoundCenterCDistanceMeasurement) return m;
                        if (m is CompoundCenterBDistanceMeasurement) return m;
                        if (m is CompoundShortAxisDistanceMeasurement) return m;
                        if (m is DualImageEdgeDistanceMeasurement) return m;
                    }
                }
            }
            return null;
        }

        private void CommitCircleRoi(double centerRow, double centerCol, double radius) {
            if (_canvasMode != ECanvasMode.CircleRoi || _editingCircleMeasurement == null || radius <= 0) {
                ExitCanvasMode();
                return;
            }

            // write to the Measurement's own fields (authoritative for Halcon call); 타입별 Circle_* 필드 설정
            var circDiam = _editingCircleMeasurement as CircleDiameterMeasurement;
            if (circDiam != null) { circDiam.Circle_Row = centerRow; circDiam.Circle_Col = centerCol; circDiam.Circle_Radius = radius; }
            var circCtr = _editingCircleMeasurement as CircleCenterDistanceMeasurement;
            if (circCtr != null) { circCtr.Circle_Row = centerRow; circCtr.Circle_Col = centerCol; circCtr.Circle_Radius = radius; }

            // Refresh canvas: RoiDefinition.Id = FAIName(ToRoiDefinition) 과 일치시켜야
            // _selectedRoiId 매치 → Edit/Delete 메뉴 활성화 + 리사이즈 핸들 렌더 동작
            var rois = GetCurrentFAIRois();
            string selId = _editingCircleFaiName;
            if (string.IsNullOrEmpty(selId)) {
                // Fallback: _editingCircleMeasurement 를 포함한 FAI 를 역탐색
                selId = FindFaiNameContainingMeasurement(_editingCircleMeasurement);
            }
            halconViewer.UpdateDisplayState(rois, selId, null, null);

            ExitCanvasMode();
        }

        private void PolygonRoiButton_Click(object sender, RoutedEventArgs e) {
            if (btn_polygonRoi.IsChecked == true) {
                ExitCanvasMode();
                _canvasMode = ECanvasMode.PolygonRoi;
                btn_polygonRoi.IsChecked = true;

                // 신규 FAI(Measurement 0개): 트리 선택을 우선 사용, dataGrid 는 fallback
                FAIConfig faiToEdit = null;
                if (mParentWindow != null && mParentWindow.inspectionList != null)
                    faiToEdit = mParentWindow.inspectionList.SelectedParam as FAIConfig;
                if (faiToEdit == null) {
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

                halconViewer.ImageLeftClicked += HalconViewer_PolygonMouseDown;
                halconViewer.ImageRightClicked += HalconViewer_PolygonRightClick;
            }
            else {
                ExitCanvasMode();
            }
        }

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

        private void Chk_overlayMeasure_Changed(object sender, RoutedEventArgs e) {
            if (halconViewer == null) return;
            halconViewer.SetMeasurementOverlayVisible(chk_overlayMeasure.IsChecked == true);
        }

        private void Chk_overlayDatum_Changed(object sender, RoutedEventArgs e) {
            if (halconViewer == null) return;
            halconViewer.SetDatumOverlayVisible(chk_overlayDatum.IsChecked == true);
        }

        private void CalibrateButton_Click(object sender, RoutedEventArgs e) {
            ExitCanvasMode();
            _canvasMode = ECanvasMode.Calibration;
            _calibrationPoints.Clear();

            btn_calibrate.Content = "Pick Point 1";
            label_drawHint.Content = "캔버스에서 첫 번째 점을 클릭하세요";
            label_drawHint.Visibility = Visibility.Visible;

            halconViewer.ImageLeftClicked += HalconViewer_CalibrationMouseDown;
        }

        private void HalconViewer_CalibrationMouseDown(object sender, MainViewerPointerChangedEventArgs e) {
            if (_canvasMode != ECanvasMode.Calibration) return;

            var pos = new System.Windows.Point(e.X, e.Y);
            _calibrationPoints.Add(pos);
            halconViewer.SetCalibrationOverlay(_calibrationPoints);

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
        private void ApplyCalibrationResult(double mmPerPixel) {
            var selectedRow = dataGrid_faiResults.SelectedItem as MeasurementResultRow;
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

        private void TeachDatumButton_Click(object sender, RoutedEventArgs e) {
            if (btn_teachDatum.IsChecked == true) {
                ExitCanvasMode();
                _canvasMode = ECanvasMode.TeachDatum;
                btn_teachDatum.IsChecked = true;
                halconViewer.IsTeachDatumMode = true;

                DatumConfig datum;
                if (mParentWindow != null && mParentWindow.inspectionList != null) datum = mParentWindow.inspectionList.SelectedParam as DatumConfig;
                else                                                               datum = null;
                if (datum == null) {
                    CustomMessageBox.Show("Datum 노드를 먼저 선택하세요.", "Teach Datum");
                    ExitCanvasMode();
                    return;
                }
                _editingDatum = datum;
                PublishDatumRoiCandidates(datum);

                // IsConfigured=true 인 경우만 ROI 누락 가드 적용 (알고리즘 전환 후 ROI 누락 케이스).
                // IsConfigured=false 면 첫 티칭 → wizard(StartDatumTeachStep) 가 ROI 를 단계별로 그리도록 진행.
                if (datum.IsConfigured) {
                    string missingRoiMsg = ValidateRoiPresence(datum, datum.AlgorithmTypeEnum);
                    if (missingRoiMsg != null) {
                        CustomMessageBox.Show("티칭 실패", missingRoiMsg);
                        btn_teachDatum.IsChecked = false;
                        _canvasMode = ECanvasMode.None;
                        _editingDatum = null;
                        halconViewer.IsEditMode = false;
                        halconViewer.IsTeachDatumMode = false;
                        return;
                    }
                    // 재티칭 확인 모달: IsConfigured=true & 모든 ROI 존재 → 즉시 teach 시나리오에서 사용자 의사 확인.
                    // Silent re-teach 방지 + 버튼 먹힘 시각 신호. ValidateRoiPresence null 통과 = 모든 ROI 존재 보장.
                    var reteachChoice = CustomMessageBox.ShowConfirmation(
                        "재티칭 확인",
                        "이 Datum 은 이미 티칭되어 있습니다.\n기존 ROI 로 재티칭하시겠습니까?\n\n(ROI 를 다시 그리려면 먼저 삭제해 주세요.)",
                        MessageBoxButton.YesNo);
                    if (reteachChoice != MessageBoxResult.Yes) {
                        btn_teachDatum.IsChecked = false;
                        _canvasMode = ECanvasMode.None;
                        _editingDatum = null;
                        halconViewer.IsEditMode = false;
                        halconViewer.IsTeachDatumMode = false;
                        return;
                    }
                }

                // 티칭 모드 진입 시 Edit OFF (그리기 모드 → ROI hit-test 차단)
                halconViewer.IsEditMode = false;

                // Wizard skip-existing: 누락된 단계만 묻기. 모든 ROI 가 이미 있으면 Done → InvokeTryTeachDatum 자동 호출 (즉시 teach).
                _datumTeachStep = GetFirstMissingStep(datum);
                StartDatumTeachStep(_datumTeachStep);
            }
            else {
                halconViewer.IsTeachDatumMode = false;
                ExitCanvasMode();
            }
        }

        // 알고리즘별 ROI 단계 시퀀스 — GetFirstMissingStep/GetNextMissingStep 의 단일 source-of-truth.
        private static EDatumTeachStep[] GetAlgorithmSteps(EDatumAlgorithm alg) {
            switch (alg) {
                case EDatumAlgorithm.TwoLineIntersect:
                    return new[] { EDatumTeachStep.Line1, EDatumTeachStep.Line2 };
                case EDatumAlgorithm.CircleTwoHorizontal:
                    return new[] { EDatumTeachStep.Circle, EDatumTeachStep.HorizontalA, EDatumTeachStep.HorizontalB };
                case EDatumAlgorithm.VerticalTwoHorizontal:
                    return new[] { EDatumTeachStep.Vertical, EDatumTeachStep.HorizontalA, EDatumTeachStep.HorizontalB };
                // DualImage 변형: 순서 = HA → HB → V (가로축 이미지 먼저 → 자동 swap → 세로축 이미지). 1-image VTH (V → HA → HB) 와 의도적으로 다름.
                case EDatumAlgorithm.VerticalTwoHorizontalDualImage:
                    return new[] { EDatumTeachStep.HorizontalA, EDatumTeachStep.HorizontalB, EDatumTeachStep.Vertical };
                default:
                    return new EDatumTeachStep[0];
            }
        }

        // Wizard skip-existing helper: ROI 가 이미 그려진 단계는 누락 X → wizard 가 건너뜀. Length1/Length2/Radius > 0 이면 ROI 존재.
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

        // 첫 누락 단계 반환. 모든 ROI 존재 시 Done (즉시 teach).
        //  한 ROI 만 Length1/Length2=0 이면 그 단계만 묻고 자동 teach.
        private EDatumTeachStep GetFirstMissingStep(DatumConfig datum) {
            foreach (var step in GetAlgorithmSteps(datum.AlgorithmTypeEnum)) {
                if (IsStepMissing(datum, step)) return step;
            }
            return EDatumTeachStep.Done;
        }

        // current 이후 다음 누락 단계 반환 — 잔존 ROI 는 건너뛰고 누락만 묻기.
        private EDatumTeachStep GetNextMissingStep(DatumConfig datum, EDatumTeachStep current) {
            var steps = GetAlgorithmSteps(datum.AlgorithmTypeEnum);
            bool foundCurrent = false;
            foreach (var step in steps) {
                if (foundCurrent && IsStepMissing(datum, step)) return step;
                if (step == current) foundCurrent = true;
            }
            return EDatumTeachStep.Done;
        }

        private void StartDatumTeachStep(EDatumTeachStep step) {
            // Unsubscribe any previous event to avoid double-fire
            halconViewer.RectDrawingCompleted   -= HalconViewer_DatumRectCompleted;
            halconViewer.CircleDrawingCompleted -= HalconViewer_DatumCircleCompleted;

            switch (step) {
                case EDatumTeachStep.Line1:
                    label_drawHint.Content = "Step 1/2: Line1 ROI를 드래그하세요";
                    label_drawHint.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFAAAAAA"));
                    label_drawHint.Visibility = Visibility.Visible;
                    halconViewer.RectDrawingCompleted += HalconViewer_DatumRectCompleted;
                    halconViewer.StartRectangleDrawing();
                    break;
                case EDatumTeachStep.Line2:
                    label_drawHint.Content = "Step 2/2: Line2 ROI를 드래그하세요";
                    label_drawHint.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFAAAAAA"));
                    label_drawHint.Visibility = Visibility.Visible;
                    halconViewer.RectDrawingCompleted += HalconViewer_DatumRectCompleted;
                    halconViewer.StartRectangleDrawing();
                    break;
                case EDatumTeachStep.Vertical:
                    // DualImage 변형이면 진입 직전에 세로축 이미지로 자동 swap.
                    if (_editingDatum != null
                        && _editingDatum.AlgorithmTypeEnum == EDatumAlgorithm.VerticalTwoHorizontalDualImage) {
                        string vpath = _editingDatum.TeachingImagePath_Vertical;
                        if (!string.IsNullOrEmpty(vpath) && System.IO.File.Exists(vpath)) {
                            try { halconViewer.LoadImage(vpath); }
                            catch (Exception ex) { Logging.PrintErrLog((int)ELogType.Error, ex.Message); }
                            // 자동 swap 도 badge/현재축 3자 동시 갱신
                            UpdateImageSourceBadge(ReringProject.Sequence.EImageSource.Vertical);
                        } else {
                            // 빈 경로: badge 만큼은 갱신 (사용자에게 "이제 Vertical step" 시각 신호) 후 드로잉 차단.
                            UpdateImageSourceBadge(ReringProject.Sequence.EImageSource.Vertical);
                            label_drawHint.Content = "세로축 이미지를 Load 해주세요 (PropertyGrid 의 TeachingImagePath_Vertical)";
                            label_drawHint.Foreground = new SolidColorBrush(Colors.Orange);
                            label_drawHint.Visibility = Visibility.Visible;
                            break;
                        }
                        label_drawHint.Content = "Step 3/3: 수직 ROI를 드래그하세요";
                    } else {
                        label_drawHint.Content = "Step 1/3: 수직 ROI를 드래그하세요";
                    }
                    label_drawHint.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFAAAAAA"));
                    label_drawHint.Visibility = Visibility.Visible;
                    halconViewer.RectDrawingCompleted += HalconViewer_DatumRectCompleted;
                    halconViewer.StartRectangleDrawing();
                    break;
                case EDatumTeachStep.HorizontalA:
                    // DualImage 변형: Step 1/3 (가로축 이미지 표시 상태 가정).
                    if (_editingDatum != null
                        && _editingDatum.AlgorithmTypeEnum == EDatumAlgorithm.VerticalTwoHorizontalDualImage) {
                        label_drawHint.Content = "Step 1/3: 수평 A ROI를 드래그하세요";
                    } else {
                        label_drawHint.Content = "Step 2/3: 수평 A ROI를 드래그하세요";
                    }
                    label_drawHint.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFAAAAAA"));
                    label_drawHint.Visibility = Visibility.Visible;
                    halconViewer.RectDrawingCompleted += HalconViewer_DatumRectCompleted;
                    halconViewer.StartRectangleDrawing();
                    break;
                case EDatumTeachStep.HorizontalB:
                    // DualImage 변형: Step 2/3.
                    if (_editingDatum != null
                        && _editingDatum.AlgorithmTypeEnum == EDatumAlgorithm.VerticalTwoHorizontalDualImage) {
                        label_drawHint.Content = "Step 2/3: 수평 B ROI를 드래그하세요";
                    } else {
                        label_drawHint.Content = "Step 3/3: 수평 B ROI를 드래그하세요";
                    }
                    label_drawHint.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFAAAAAA"));
                    label_drawHint.Visibility = Visibility.Visible;
                    halconViewer.RectDrawingCompleted += HalconViewer_DatumRectCompleted;
                    halconViewer.StartRectangleDrawing();
                    break;
                case EDatumTeachStep.Circle:
                    label_drawHint.Content = "Step 1/3: Circle 검색 영역 중심을 클릭 후 드래그하여 반지름을 지정하세요";
                    label_drawHint.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFAAAAAA"));
                    label_drawHint.Visibility = Visibility.Visible;
                    halconViewer.CircleDrawingCompleted += HalconViewer_DatumCircleCompleted;
                    halconViewer.StartCircleDrawing();
                    break;
                case EDatumTeachStep.Done:
                    InvokeTryTeachDatum();
                    break;
            }
        }

        // Rect 완료 (Line1/Line2/Vertical/HorizontalA/HorizontalB 공통)
        private void HalconViewer_DatumRectCompleted(object sender, EventArgs e) {
            halconViewer.RectDrawingCompleted -= HalconViewer_DatumRectCompleted;
            var roi = halconViewer.CommitActiveRectangle();
            if (roi == null || _editingDatum == null) { ExitCanvasMode(); return; }

            // RoiDefinition bbox → Rectangle2 (center, phi=0, halfW=Length1, halfH=Length2)
            //  gen_measure_rectangle2(Row,Col,Phi,Length1,Length2): Phi=0 기준 Length1=X축 절반(halfW), Length2=Y축 절반(halfH).
            //  (Length1=halfH, Length2=halfW) 매핑은 정반대 → 측정 사각형 90° 회전 → MeasurePos 가 의도한 에지를 가로지르지 못함.
            double centerRow = (roi.Row1 + roi.Row2) / 2.0;
            double centerCol = (roi.Column1 + roi.Column2) / 2.0;
            double halfH     = (roi.Row2 - roi.Row1) / 2.0;
            double halfW     = (roi.Column2 - roi.Column1) / 2.0;

            // step 별 DatumConfig 필드 기록 (Length1=halfW X축 절반, Length2=halfH Y축 절반)
            switch (_datumTeachStep) {
                case EDatumTeachStep.Line1:
                    _editingDatum.Line1_Row     = centerRow;
                    _editingDatum.Line1_Col     = centerCol;
                    _editingDatum.Line1_Phi     = 0.0;
                    _editingDatum.Line1_Length1 = halfW;
                    _editingDatum.Line1_Length2 = halfH;
                    break;
                // Vertical case 분리: Line1_* → Vertical_* write-back (의미적 분리)
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
                    _editingDatum.Line2_Length1 = halfW;
                    _editingDatum.Line2_Length2 = halfH;
                    break;
                case EDatumTeachStep.HorizontalA:
                    _editingDatum.Horizontal_A_Row     = centerRow;
                    _editingDatum.Horizontal_A_Col     = centerCol;
                    _editingDatum.Horizontal_A_Phi     = 0.0;
                    _editingDatum.Horizontal_A_Length1 = halfW;
                    _editingDatum.Horizontal_A_Length2 = halfH;
                    break;
                case EDatumTeachStep.HorizontalB:
                    _editingDatum.Horizontal_B_Row     = centerRow;
                    _editingDatum.Horizontal_B_Col     = centerCol;
                    _editingDatum.Horizontal_B_Phi     = 0.0;
                    _editingDatum.Horizontal_B_Length1 = halfW;
                    _editingDatum.Horizontal_B_Length2 = halfH;
                    break;
            }

            // DatumConfig 자동 속성은 INotifyPropertyChanged 미발동 → PropertyGrid 강제 재바인딩 + RaisePropertyChanged 이중 신호
            try { _editingDatum.RaisePropertyChanged(string.Empty); } catch { }
            if (mParentWindow != null && mParentWindow.inspectionList != null) mParentWindow.inspectionList.RefreshParamEditor();
            // 캔버스 오버레이도 새 좌표로 갱신 (Datum ROI Rect/Circle 재렌더)
            halconViewer.SetDatumOverlay(_editingDatum, true, GetDatumEditMode());

            AdvanceDatumTeachStep();
        }

        private void HalconViewer_DatumCircleCompleted(object sender, CircleDrawCompletedArgs e) {
            halconViewer.CircleDrawingCompleted -= HalconViewer_DatumCircleCompleted;
            if (_editingDatum == null || e.Radius <= 0) { ExitCanvasMode(); return; }

            _editingDatum.CircleROI_Row    = e.CenterRow;
            _editingDatum.CircleROI_Col    = e.CenterCol;
            _editingDatum.CircleROI_Radius = e.Radius;

            // PropertyGrid 재바인딩 + Datum 오버레이 갱신 (CircleROI_* write-back 즉시 반영)
            try { _editingDatum.RaisePropertyChanged(string.Empty); } catch { }
            if (mParentWindow != null && mParentWindow.inspectionList != null) mParentWindow.inspectionList.RefreshParamEditor();
            halconViewer.SetDatumOverlay(_editingDatum, true, GetDatumEditMode());

            AdvanceDatumTeachStep();
        }

        // Wizard skip-existing: 잔존 ROI 건너뛰고 다음 누락 단계만 묻기.
        private void AdvanceDatumTeachStep() {
            if (_editingDatum == null) { ExitCanvasMode(); return; }
            _datumTeachStep = GetNextMissingStep(_editingDatum, _datumTeachStep);
            StartDatumTeachStep(_datumTeachStep);
        }

        // 마지막 ROI 직후 DatumFindingService.TryTeachDatum 자동 호출.
        // DualImage 변형 시 두 파일에서 이미지 2개 로드 후 2-image TryTeachDatum 호출 (early-return + try/finally).
        private void InvokeTryTeachDatum() {
            if (_editingDatum == null) { ExitCanvasMode(); return; }

            HImage img = halconViewer.CurrentImage;
            if (img == null) {
                label_drawHint.Visibility = Visibility.Collapsed;
                CustomMessageBox.Show("티칭 실패", "이미지가 없습니다. 먼저 Grab 또는 Load Image 를 수행하세요.");
                _canvasMode = ECanvasMode.None;
                btn_teachDatum.IsChecked = false;
                _editingDatum = null;
                halconViewer.IsTeachDatumMode = false;
                return;
            }

            var svc = new ReringProject.Halcon.Algorithms.DatumFindingService();
            string error = null;
            bool ok = false;

            if (_editingDatum.AlgorithmTypeEnum == EDatumAlgorithm.VerticalTwoHorizontalDualImage) {
                string pathH = _editingDatum.TeachingImagePath;
                string pathV = _editingDatum.TeachingImagePath_Vertical;

                if (string.IsNullOrEmpty(pathH) || !System.IO.File.Exists(pathH)) {
                    ExitTeachWithError("가로축 티칭 이미지 경로가 비어 있거나 파일이 없습니다.");
                    return;
                }
                if (string.IsNullOrEmpty(pathV) || !System.IO.File.Exists(pathV)) {
                    ExitTeachWithError("세로축 티칭 이미지 경로가 비어 있거나 파일이 없습니다.");
                    return;
                }

                // 이미지 2개 로드: try/finally 로 dispose 보장. 로드 실패 시 error 설정 → 공통 결과 처리.
                HImage imgH = null, imgV = null;
                try {
                    try { imgH = new HImage(pathH); }
                    catch (Exception exH) { error = "가로축 이미지 로드 실패: " + exH.Message; ok = false; }

                    if (error == null) {
                        try { imgV = new HImage(pathV); }
                        catch (Exception exV) { error = "세로축 이미지 로드 실패: " + exV.Message; ok = false; }
                    }

                    if (error == null) {
                        ok = svc.TryTeachDatum(imgH, imgV, _editingDatum, out error);
                    }
                } finally {
                    if (imgH != null) { try { imgH.Dispose(); } catch { } }
                    if (imgV != null) { try { imgV.Dispose(); } catch { } }
                }
            } else {
                ok = svc.TryTeachDatum(img, _editingDatum, out error); // 단일-이미지 오버로드
            }

            // 공통 결과 처리 (DualImage / 1-image 양쪽 공통)
            if (ok) {
                label_drawHint.Content = "Datum 티칭 완료 — Recipe Save 권장";
                label_drawHint.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF4ADE80"));
                label_drawHint.Visibility = Visibility.Visible;
                // 오버레이 갱신 (LastTeachSucceeded=true → HalconDisplayService 분기 렌더)
                halconViewer.SetDatumOverlay(_editingDatum, true, GetDatumEditMode());
                PublishDatumRoiCandidates(_editingDatum);
                UpdateDatumRefCoordsLabel(_editingDatum);
            }
            else {
                // teach 실패 사유 모달. FormatTeachError 가 EdgeDirection 힌트 통합.
                label_drawHint.Visibility = Visibility.Collapsed;
                CustomMessageBox.Show("티칭 실패", FormatTeachError(_editingDatum, error));
            }

            // ROI 유지(재튜닝 가능), canvas mode 해제
            _canvasMode = ECanvasMode.None;
            btn_teachDatum.IsChecked = false;
            _editingDatum = null;
            halconViewer.IsTeachDatumMode = false;
        }

        // InvokeTryTeachDatum 의 early-return 헬퍼 (goto 패턴 회피).
        private void ExitTeachWithError(string message) {
            label_drawHint.Visibility = Visibility.Collapsed;
            CustomMessageBox.Show("티칭 실패", message);
            _canvasMode = ECanvasMode.None;
            btn_teachDatum.IsChecked = false;
            _editingDatum = null;
            halconViewer.IsTeachDatumMode = false;
        }

        //260618 hbk Phase 54 ALIGN-01 패턴 ROI 전용 그리기 모드 진입 (D-08) — TeachDatumButton_Click 진입 패턴 미러
        private void DrawPatternRoiButton_Click(object sender, RoutedEventArgs e) {
            DatumConfig datum;
            if (mParentWindow != null && mParentWindow.inspectionList != null) datum = mParentWindow.inspectionList.SelectedParam as DatumConfig;
            else                                                               datum = null;
            if (datum == null) {
                CustomMessageBox.Show("패턴 ROI 그리기", "Datum 노드를 먼저 선택하세요.");
                return;
            }
            ExitCanvasMode();
            _editingDatum = datum;
            _canvasMode = ECanvasMode.PatternRoi;
            label_drawHint.Content = "패턴 ROI: 드래그하여 템플릿 영역을 지정하세요";
            label_drawHint.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFAAAAAA"));
            label_drawHint.Visibility = Visibility.Visible;
            halconViewer.RectDrawingCompleted += HalconViewer_PatternRectCompleted;
            halconViewer.StartRectangleDrawing();
        }

        //260618 hbk Phase 54 ALIGN-01 패턴 ROI write-back (D-08) — HalconViewer_DatumRectCompleted 패턴 미러
        private void HalconViewer_PatternRectCompleted(object sender, EventArgs e) {
            halconViewer.RectDrawingCompleted -= HalconViewer_PatternRectCompleted;
            var roi = halconViewer.CommitActiveRectangle();
            if (roi == null || _editingDatum == null) { ExitCanvasMode(); return; }

            // RoiDefinition bbox → Rectangle2 (center, phi=0, halfW=Length1, halfH=Length2)
            //  Length1=X축 절반(halfW), Length2=Y축 절반(halfH) — DatumRectCompleted 규약 동일
            double centerRow = (roi.Row1 + roi.Row2) / 2.0;
            double centerCol = (roi.Column1 + roi.Column2) / 2.0;
            double halfH     = (roi.Row2 - roi.Row1) / 2.0;
            double halfW     = (roi.Column2 - roi.Column1) / 2.0;

            _editingDatum.PatternRoi_Row     = centerRow;
            _editingDatum.PatternRoi_Col     = centerCol;
            _editingDatum.PatternRoi_Phi     = 0.0;
            _editingDatum.PatternRoi_Length1 = halfW; // X축 절반
            _editingDatum.PatternRoi_Length2 = halfH; // Y축 절반

            // PropertyGrid 재바인딩 (INotifyPropertyChanged 미발동 대응)
            try { _editingDatum.RaisePropertyChanged(string.Empty); } catch { }
            if (mParentWindow != null && mParentWindow.inspectionList != null) mParentWindow.inspectionList.RefreshParamEditor();

            label_drawHint.Content = "패턴 1 ROI 저장 완료 (PatternRoi_* 기록됨)";
            label_drawHint.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF4ADE80"));
            label_drawHint.Visibility = Visibility.Visible;
            _canvasMode = ECanvasMode.None;
            _editingDatum = null;
        }

        //260619 hbk Phase 55 ALIGN-02 패턴 2 ROI 그리기 — DrawPatternRoiButton_Click 미러 (점2 = baseline 각도용)
        private void DrawPatternRoi2Button_Click(object sender, RoutedEventArgs e) {
            DatumConfig datum;
            if (mParentWindow != null && mParentWindow.inspectionList != null) datum = mParentWindow.inspectionList.SelectedParam as DatumConfig;
            else                                                               datum = null;
            if (datum == null) {
                CustomMessageBox.Show("패턴 2 ROI 그리기", "Datum 노드를 먼저 선택하세요.");
                return;
            }
            ExitCanvasMode();
            _editingDatum = datum;
            _canvasMode = ECanvasMode.PatternRoi2;
            label_drawHint.Content = "패턴 2 ROI: 패턴 1 의 반대 대각 끝에 드래그하세요 (멀수록 각도 정밀)";
            label_drawHint.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFAAAAAA"));
            label_drawHint.Visibility = Visibility.Visible;
            halconViewer.RectDrawingCompleted += HalconViewer_PatternRect2Completed;
            halconViewer.StartRectangleDrawing();
        }

        //260619 hbk Phase 55 ALIGN-02 패턴 2 ROI write-back — HalconViewer_PatternRectCompleted 미러
        private void HalconViewer_PatternRect2Completed(object sender, EventArgs e) {
            halconViewer.RectDrawingCompleted -= HalconViewer_PatternRect2Completed;
            var roi = halconViewer.CommitActiveRectangle();
            if (roi == null || _editingDatum == null) { ExitCanvasMode(); return; }

            double centerRow = (roi.Row1 + roi.Row2) / 2.0;
            double centerCol = (roi.Column1 + roi.Column2) / 2.0;
            double halfH     = (roi.Row2 - roi.Row1) / 2.0;
            double halfW     = (roi.Column2 - roi.Column1) / 2.0;

            _editingDatum.PatternRoi2_Row     = centerRow;
            _editingDatum.PatternRoi2_Col     = centerCol;
            _editingDatum.PatternRoi2_Phi     = 0.0;
            _editingDatum.PatternRoi2_Length1 = halfW; // X축 절반
            _editingDatum.PatternRoi2_Length2 = halfH; // Y축 절반

            try { _editingDatum.RaisePropertyChanged(string.Empty); } catch { }
            if (mParentWindow != null && mParentWindow.inspectionList != null) mParentWindow.inspectionList.RefreshParamEditor();

            label_drawHint.Content = "패턴 2 ROI 저장 완료 — [패턴 모델 생성] 클릭 시 모델2 + RefMatch2 기록";
            label_drawHint.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF4ADE80"));
            label_drawHint.Visibility = Visibility.Visible;
            _canvasMode = ECanvasMode.None;
            _editingDatum = null;
        }

        //260618 hbk Phase 54 ALIGN-01 tilt 직선 ROI 전용 그리기 모드 진입 (사용자 설계) — DrawPatternRoiButton_Click 미러
        private void DrawAlignLineRoiButton_Click(object sender, RoutedEventArgs e) {
            DatumConfig datum;
            if (mParentWindow != null && mParentWindow.inspectionList != null) datum = mParentWindow.inspectionList.SelectedParam as DatumConfig;
            else                                                               datum = null;
            if (datum == null) {
                CustomMessageBox.Show("직선 ROI 그리기", "Datum 노드를 먼저 선택하세요.");
                return;
            }
            ExitCanvasMode();
            _editingDatum = datum;
            _canvasMode = ECanvasMode.AlignLineRoi;
            label_drawHint.Content = "직선 ROI: 회전 기준이 될 직선 위에 드래그하세요 (가로 직선=가로로 길게, 세로 직선=세로로 길게)";
            label_drawHint.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFAAAAAA"));
            label_drawHint.Visibility = Visibility.Visible;
            halconViewer.RectDrawingCompleted += HalconViewer_AlignLineRectCompleted;
            halconViewer.StartRectangleDrawing();
        }

        //260618 hbk Phase 54 ALIGN-01 직선 ROI write-back (사용자 설계) — HalconViewer_PatternRectCompleted 미러
        private void HalconViewer_AlignLineRectCompleted(object sender, EventArgs e) {
            halconViewer.RectDrawingCompleted -= HalconViewer_AlignLineRectCompleted;
            var roi = halconViewer.CommitActiveRectangle();
            if (roi == null || _editingDatum == null) { ExitCanvasMode(); return; }

            double centerRow = (roi.Row1 + roi.Row2) / 2.0;
            double centerCol = (roi.Column1 + roi.Column2) / 2.0;
            double halfH     = (roi.Row2 - roi.Row1) / 2.0;
            double halfW     = (roi.Column2 - roi.Column1) / 2.0;

            _editingDatum.AlignLineRoi_Row     = centerRow;
            _editingDatum.AlignLineRoi_Col     = centerCol;
            _editingDatum.AlignLineRoi_Phi     = 0.0;
            _editingDatum.AlignLineRoi_Length1 = halfW; // X축 절반
            _editingDatum.AlignLineRoi_Length2 = halfH; // Y축 절반

            //260618 hbk Phase 54 ALIGN-01 기준각 즉시 캡쳐 (CO-54-04) — 그리는 순간 현재(티칭) 이미지에서 직선 각도 측정.
            //  [패턴 모델 생성] 재클릭 의존 제거. 런타임 θ = 측정각 − 이 기준각.
            string alignHint;
            _editingDatum.EnsurePerRoiDefaults();
            HImage curImg = halconViewer.CurrentImage;
            if (curImg != null) {
                var dfsRef = new ReringProject.Halcon.Algorithms.DatumFindingService();
                double refRad;
                string refErr;
                if (dfsRef.TryGetAlignLineAngle(curImg, _editingDatum, 0.0, 0.0, out refRad, out refErr)) {
                    _editingDatum.AlignLineRefAngleDeg = refRad * 180.0 / Math.PI;
                    alignHint = "직선 ROI 저장 + 기준각 " + _editingDatum.AlignLineRefAngleDeg.ToString("F3") + "° 기록 완료 — Recipe Save 권장";
                } else {
                    alignHint = "[경고] 직선 ROI 저장됨, 기준각 측정 실패(에지 못잡음 — polarity/threshold 확인): " + (refErr ?? "");
                }
            } else {
                alignHint = "직선 ROI 저장됨 — 이미지 없음. Grab/Load 후 다시 그려야 기준각 기록됨";
            }

            try { _editingDatum.RaisePropertyChanged(string.Empty); } catch { }
            if (mParentWindow != null && mParentWindow.inspectionList != null) mParentWindow.inspectionList.RefreshParamEditor();

            label_drawHint.Content = alignHint;
            label_drawHint.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF4ADE80"));
            label_drawHint.Visibility = Visibility.Visible;
            _canvasMode = ECanvasMode.None;
            _editingDatum = null;
        }

        //260618 hbk Phase 54 ALIGN-01 패턴 모델 생성/저장 + ref pose 기록 (D-08/D-09) — InvokeTryTeachDatum 패턴 미러
        private void CreatePatternModelButton_Click(object sender, RoutedEventArgs e) {
            InvokeCreatePatternModel();
        }

        private void InvokeCreatePatternModel() {
            DatumConfig datum;
            if (mParentWindow != null && mParentWindow.inspectionList != null) datum = mParentWindow.inspectionList.SelectedParam as DatumConfig;
            else                                                               datum = null;
            if (datum == null) {
                CustomMessageBox.Show("모델 생성 실패", "Datum 노드를 먼저 선택하세요.");
                return;
            }

            //260618 hbk Phase 54 ALIGN-01 hotfix: 모델 생성 전 기본값 보장. sentinel 0 이면 PatternAngleExtentDeg 가 0
            //  → create_shape_model AngleExtent=0 → 0° 전용 모델 → 1° 회전 매칭 실패. EnsurePerRoiDefaults 가 10°/0.6/100px 복원.
            datum.EnsurePerRoiDefaults();

            HImage img = halconViewer.CurrentImage;
            if (img == null) {
                CustomMessageBox.Show("모델 생성 실패", "이미지가 없습니다. 먼저 Grab 또는 Load Image 를 수행하세요.");
                return;
            }

            // W2: PatternRoi 미확보 시 모델 생성 차단 (silent 실패 0)
            if (datum.PatternRoi_Length1 <= 0.0 || datum.PatternRoi_Length2 <= 0.0) {
                CustomMessageBox.Show("모델 생성 실패", "패턴 ROI(Rect) 를 먼저 그리세요. ([패턴 ROI] 버튼)");
                return;
            }

            // 54-04 런타임 load 와 동일 키 (D-07) — 직접 경로 도출 금지, 헬퍼만 사용
            string modelPath = ReringProject.Sequence.InspectionSequence.ResolveDatumModelPath(datum);
            if (string.IsNullOrEmpty(modelPath)) {
                CustomMessageBox.Show("모델 생성 실패", "모델 경로 도출 실패 (레시피/Shot 확인).");
                return;
            }

            var svc = new ReringProject.Halcon.Algorithms.PatternMatchService();
            string error;
            bool ok = svc.TryCreateModel(img,
                datum.PatternRoi_Row, datum.PatternRoi_Col, datum.PatternRoi_Phi,
                datum.PatternRoi_Length1, datum.PatternRoi_Length2,
                datum.PatternEngine, datum.PatternAngleExtentDeg, modelPath, out error);

            if (ok) {
                // D-09: 티칭 이미지에서 1회 find → ref pose 기록 (런타임과 동일 연산 → 부호 일관성)
                double rr, rc, ra, rs;
                string refError;
                if (svc.TryFindRefPose(img, datum.PatternEngine, modelPath, datum.PatternMinScore, out rr, out rc, out ra, out rs, out refError)) {
                    datum.RefMatchRow     = rr;
                    datum.RefMatchCol     = rc;
                    datum.RefMatchAngleDeg = ra;
                    //260619 hbk Phase 55 ALIGN-02 — 패턴 2 설정 시 모델2 생성 + RefMatch2(위치) 기록. 미설정 시 단일 패턴(x,y+단일각) 동작.
                    //  런타임 θ = 두 RefMatch 중심 baseline 각 − 두 cur 중심 baseline 각. 패턴2 자체 회전각 미사용.
                    string alignMsg;
                    if (datum.PatternRoi2_Length1 > 0.0 && datum.PatternRoi2_Length2 > 0.0) {
                        string modelPath2 = ReringProject.Sequence.InspectionSequence.ResolveDatumModelPath2(datum);
                        string err2 = null;
                        if (!string.IsNullOrEmpty(modelPath2) && svc.TryCreateModel(img,
                                datum.PatternRoi2_Row, datum.PatternRoi2_Col, datum.PatternRoi2_Phi,
                                datum.PatternRoi2_Length1, datum.PatternRoi2_Length2,
                                datum.PatternEngine, datum.PatternAngleExtentDeg, modelPath2, out err2)) {
                            double rr2, rc2, ra2, rs2;
                            string refErr2;
                            if (svc.TryFindRefPose(img, datum.PatternEngine, modelPath2, datum.PatternMinScore, out rr2, out rc2, out ra2, out rs2, out refErr2)) {
                                datum.RefMatch2Row = rr2;
                                datum.RefMatch2Col = rc2;
                                alignMsg = "\n패턴 2 모델 생성 + RefMatch2 기록 (score " + rs2.ToString("F3") + ") — 2-패턴 baseline 회전보정 활성";
                            } else {
                                alignMsg = "\n[경고] 패턴 2 모델 생성됨, ref pose 기록 실패 → 단일 패턴 폴백: " + (refErr2 ?? "");
                            }
                        } else {
                            alignMsg = "\n[경고] 패턴 2 모델 생성 실패 → 단일 패턴 폴백: " + (err2 ?? "");
                        }
                    } else {
                        alignMsg = "\n(패턴 2 미설정 — 단일 패턴 x,y+단일각 보정만. [패턴 2] 버튼으로 그리면 2-점 baseline 회전보정)";
                    }
                    // PropertyGrid 재바인딩
                    try { datum.RaisePropertyChanged(string.Empty); } catch { }
                    if (mParentWindow != null && mParentWindow.inspectionList != null) mParentWindow.inspectionList.RefreshParamEditor();
                    CustomMessageBox.Show("모델 생성 완료", "패턴 모델 생성·ref pose 기록 완료 (score " + rs.ToString("F3") + ") — Recipe Save 권장" + alignMsg);
                }
                else {
                    CustomMessageBox.Show("ref pose 기록 실패", refError);
                }
            }
            else {
                CustomMessageBox.Show("모델 생성 실패", error);
            }
        }

        private void BtnTestFindDatum_Click(object sender, RoutedEventArgs e) {
            // Datum 해결 (InspectionListView 선택 우선, _editingDatum fallback 없음 — teach 세션 독립)
            DatumConfig datum;
            if (mParentWindow != null && mParentWindow.inspectionList != null) datum = mParentWindow.inspectionList.SelectedParam as DatumConfig;
            else                                                               datum = null;
            if (datum == null || !datum.IsConfigured || !datum.LastTeachSucceeded) {
                CustomMessageBox.Show("Datum Find 테스트", "Datum 티칭이 완료된 후 테스트 가능합니다.");
                return;
            }

            var svc = new ReringProject.Halcon.Algorithms.DatumFindingService();
            HTuple transform;
            string error = null;
            bool ok = false;

            // DualImage 변형: 두 파일 직접 로드 + 2-image 오버로드 호출. Teach 와 동일 패턴.
            if (datum.AlgorithmTypeEnum == EDatumAlgorithm.VerticalTwoHorizontalDualImage) {
                string pathH = datum.TeachingImagePath;
                string pathV = datum.TeachingImagePath_Vertical;
                if (string.IsNullOrEmpty(pathH) || !System.IO.File.Exists(pathH)) {
                    CustomMessageBox.Show("Find 실패", "가로축 티칭 이미지 경로가 비어 있거나 파일이 없습니다.");
                    return;
                }
                if (string.IsNullOrEmpty(pathV) || !System.IO.File.Exists(pathV)) {
                    CustomMessageBox.Show("Find 실패", "세로축 티칭 이미지 경로가 비어 있거나 파일이 없습니다.");
                    return;
                }
                HImage imgH = null, imgV = null;
                try {
                    try { imgH = new HImage(pathH); }
                    catch (Exception exH) { error = "가로축 이미지 로드 실패: " + exH.Message; ok = false; }
                    if (error == null) {
                        try { imgV = new HImage(pathV); }
                        catch (Exception exV) { error = "세로축 이미지 로드 실패: " + exV.Message; ok = false; }
                    }
                    if (error == null) {
                        ok = svc.TryFindDatum(imgH, imgV, datum, out transform, out error);
                    }
                    else {
                        HOperatorSet.HomMat2dIdentity(out transform); // 로드 실패 시 transform 초기화
                    }
                } finally {
                    if (imgH != null) { try { imgH.Dispose(); } catch { } }
                    if (imgV != null) { try { imgV.Dispose(); } catch { } }
                }
            }
            else {
                HImage testImage = AskTestImageSource();
                if (testImage == null) return;
                ok = svc.TryFindDatum(testImage, datum, out transform, out error); // 단일-이미지 오버로드
            }

            // label_drawHint / label_testFindResult inline 피드백 폐기 (성공 X / 실패 O 모달 정책)
            label_drawHint.Visibility = Visibility.Collapsed;
            label_testFindResult.Visibility = Visibility.Collapsed;
            if (ok) {
                // 성공: TryFindDatum 이 DetectedOrigin* + LastFindSucceeded write-back → SetDatumOverlay → RenderDatumFindResult chain
                halconViewer.SetDatumOverlay(datum, true, GetDatumEditMode());
                // PropertyGrid 메트릭 갱신 (DetectedEdgeCount/FitRMSE/AngleDeg ReadOnly 표시)
                try { datum.RaisePropertyChanged(string.Empty); } catch { }
                if (mParentWindow != null && mParentWindow.inspectionList != null) {
                    mParentWindow.inspectionList.RefreshParamEditor();
                }
                // 성공 시 모달 X — 사용자가 캔버스 시각화로 즉시 확인
            }
            else {
                // Find 실패 사유 모달. FormatFindError 가 EdgeDirection 힌트 통합.
                CustomMessageBox.Show("Find 실패", FormatFindError(error));
                // 실패 시 오버레이 clear (이전 성공 십자 잔상 제거)
                halconViewer.ClearDatumFindResultOverlay();
            }
        }

        // 테스트 이미지 소스 다이얼로그: 현재 halconViewer.CurrentImage / OpenFileDialog / 취소
        //  반환 HImage 는 halconViewer.CurrentImage 참조 그대로 (별도 Dispose 책임 없음 — halconViewer 가 소유)
        private HImage AskTestImageSource() {
            HImage currentImg = halconViewer.CurrentImage;
            bool hasCurrent = (currentImg != null);

            // 3-way 선택 (MessageBox YesNoCancel: Yes=현재 이미지 / No=파일 선택 / Cancel=취소)
            MessageBoxResult choice;
            if (hasCurrent) {
                choice = MessageBox.Show(
                    "테스트 이미지를 선택하세요.\n\n[예] 현재 이미지로 테스트\n[아니오] 다른 파일 선택...\n[취소] 취소",
                    "Datum Find 테스트 이미지",
                    MessageBoxButton.YesNoCancel,
                    MessageBoxImage.Question);
            }
            else {
                // 현재 이미지 없으면 바로 파일 선택 경로 (2-way)
                choice = MessageBoxResult.No;
            }

            if (choice == MessageBoxResult.Cancel) return null;
            if (choice == MessageBoxResult.Yes) return currentImg;

            var dialog = new OpenFileDialog {
                Filter = "Image Files (*.bmp;*.jpg;*.jpeg;*.png;*.tif;*.tiff)|*.bmp;*.jpg;*.jpeg;*.png;*.tif;*.tiff|All Files (*.*)|*.*"
            };
            if (dialog.ShowDialog() != true) return null;

            try {
                halconViewer.LoadImage(dialog.FileName);
                return halconViewer.CurrentImage;
            }
            catch (Exception ex) {
                Logging.PrintErrLog((int)ELogType.Error, "Datum Test Load fail: " + ex.Message);
                CustomMessageBox.Show("Datum Find 테스트", "이미지 로드 실패: " + ex.Message);
                return null;
            }
        }
    }
}
