using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using HalconDotNet;
using ReringProject.Halcon;
using ReringProject.Halcon.Display;
using ReringProject.Halcon.Models;
using ReringProject.Sequence;

namespace ReringProject.UI
{
    public sealed class MainViewerPointerChangedEventArgs : EventArgs
    {
        public MainViewerPointerChangedEventArgs(double x, double y, double? grayValue)
        {
            X = x;
            Y = y;
            GrayValue = grayValue;
        }

        public double X { get; }
        public double Y { get; }
        public double? GrayValue { get; }
    }

    //260423 hbk Phase 11 D-14 — Circle 드래그 완료 인자
    public class CircleDrawCompletedArgs : EventArgs
    {
        public double CenterRow { get; set; }
        public double CenterCol { get; set; }
        public double Radius { get; set; }
    }

    //260423 hbk ROI 이동 완료 인자
    public class RoiMoveCompletedArgs : EventArgs
    {
        public string RoiId { get; set; }
        public double DeltaRow { get; set; }
        public double DeltaCol { get; set; }
    }

    //260423 hbk ROI 리사이즈/정점편집 완료 인자 (절대 좌표 전달)
    public class RoiGeometryChangedArgs : EventArgs
    {
        public string RoiId { get; set; }
        public RoiShape Shape { get; set; }
        public double Row1 { get; set; }
        public double Column1 { get; set; }
        public double Row2 { get; set; }
        public double Column2 { get; set; }
        public double CenterRow { get; set; }
        public double CenterCol { get; set; }
        public double Radius { get; set; }
        public string PolygonPoints { get; set; }
    }

    //260423 hbk ROI 리사이즈 핸들 식별
    internal enum ResizeHandle
    {
        None,
        RectTL, RectT, RectTR,
        RectL,           RectR,
        RectBL, RectB, RectBR,
        CircleRadius,
        PolygonVertex
    }

    public partial class MainResultViewerControl : UserControl, IDisposable
    {
        private const double ZoomInScaleFactor = 0.65;
        private const double ZoomOutScaleFactor = 1.55;
        private const int HalconLeftButton = 1;
        private const int HalconRightButton = 4;

        private readonly HalconDisplayService _displayService = new HalconDisplayService();
        private readonly List<RoiDefinition> _rois = new List<RoiDefinition>();
        private readonly List<EdgeInspectionOverlay> _inspectionOverlays = new List<EdgeInspectionOverlay>();
        private readonly List<string> _displayMessages = new List<string>();
        private string _selectedRoiId; //260408 hbk 선택 ROI 하이라이트용
        private bool _isWindowInitialized;
        private bool _isPanningImage;
        private bool _renderPending;
        private bool _manualToolsEnabled = true;
        private bool _manualMeasureMode;
        private bool _crosshairEnabled;
        private double _imageWidth;
        private double _imageHeight;
        private Point _panStartPoint;
        private Rect _panStartImagePart;
        private Point _lastMouseImagePoint;
        private Point? _manualMeasureStartPoint;
        private Point? _manualMeasureEndPoint;

        //260408 hbk Rect ROI drawing state
        private bool _isDrawingRect;
        private Point _rectDragStart;
        private RoiDefinition _rectDraftRoi;

        //260408 hbk Rect 드래그 완료 시 자동 커밋 이벤트
        public event EventHandler RectDrawingCompleted;

        //260423 hbk Phase 11 D-14 — Circle ROI drawing state
        private bool _isDrawingCircle;
        private Point _circleDraftCenter;
        private double _circleDraftRadius;

        //260423 hbk Phase 11 D-14 — Circle 드래그 완료 이벤트
        public event EventHandler<CircleDrawCompletedArgs> CircleDrawingCompleted;

        //260423 hbk ROI 이동 완료 이벤트
        public event EventHandler<RoiMoveCompletedArgs> RoiMoveCompleted;

        //260423 hbk ROI 이동 상태
        private bool _isMovingRoi;
        private Point _moveStartImagePoint;
        private RoiDefinition _movingRoiSnapshot;

        //260423 hbk Edit 모드 상태 + 삭제 이벤트
        //260503 hbk Phase 17 D-06 — Edit 모드 단일 gate (Rect+Circle+Polygon 공통). setter 노출 (외부 wiring: MainView btn_teachDatum / ExitCanvasMode 등)
        private bool _isEditMode;
        public bool IsEditMode {
            get { return _isEditMode; }
            set { SetEditMode(value); } //260503 hbk Phase 17 D-06 — setter 가 SetEditMode 위임 (RoiEditModeChanged 이벤트 + 컨텍스트 메뉴 동기화 보존)
        }
        public event EventHandler<bool> RoiEditModeChanged;
        public event EventHandler<string> RoiDeleteRequested;

        //260505 hbk Phase 18 CO-04 — TeachDatum 모드 여부. MainView.TeachDatumButton_Click 에서 true/false 설정.
        //  UpdateContextMenuState 가 "ROI 다시 그리기" 메뉴 가시성 결정에 사용.
        public bool IsTeachDatumMode { get; set; } //260505 hbk Phase 18 CO-04

        //260505 hbk Phase 18 CO-04 — "ROI 다시 그리기" 메뉴 클릭 시 발생. 인자 = hit-test 통과한 roiId.
        public event System.Action<string> RoiRedrawRequested; //260505 hbk Phase 18 CO-04

        //260423 hbk 리사이즈 상태 + 기하 변경 이벤트
        private bool _isResizingRoi;
        private ResizeHandle _resizingHandle;
        private int _resizingPolygonIndex;
        private RoiDefinition _resizingRoiSnapshot;
        private const double HandleHalfSizeImage = 10.0; //260423 hbk 핸들 정사각형 반변(이미지 px)
        private const double HandleHitRadiusImage = 14.0; //260423 hbk 핸들 히트테스트 허용 반경(이미지 px)
        public event EventHandler<RoiGeometryChangedArgs> RoiGeometryChanged;

        //260408 hbk Polygon draft rendering state
        private IList<Point> _polygonDraftPoints;
        private string _polygonColor = "blue";

        //260408 hbk Calibration 오버레이 상태
        private IList<Point> _calibrationPoints;

        public MainResultViewerControl()
        {
            InitializeComponent();
            ViewerHost.ForceCursor = true;
            ViewerBorder.ForceCursor = true;
            ViewerHost.HInitWindow += ViewerHost_HInitWindow;
            ViewerHost.HMouseDown += ViewerHost_HMouseDown;
            ViewerHost.HMouseMove += ViewerHost_HMouseMove;
            ViewerHost.HMouseUp += ViewerHost_HMouseUp;
            ViewerHost.HMouseWheel += ViewerHost_HMouseWheel;
            ViewerHost.MouseLeftButtonDown += ViewerHost_MouseLeftButtonDown;
            ViewerHost.SizeChanged += ViewerHost_SizeChanged;
            ViewerHost.MouseEnter += ViewerHost_MouseEnter;
            ViewerHost.MouseLeave += ViewerHost_MouseLeave;
            ViewerBorder.MouseEnter += ViewerHost_MouseEnter;
            ViewerBorder.MouseLeave += ViewerHost_MouseLeave;
            Unloaded += MainResultViewerControl_Unloaded;
            UpdateContextMenuState();
            SetPanCursor(Cursors.Arrow);
        }

        public event EventHandler<MainViewerPointerChangedEventArgs> PointerInfoChanged;

        //260408 hbk HWindowControlWPF는 Win32 호스팅이라 WPF MouseLeftButtonDown이 전달 안됨
        //Halcon HMouseDown에서 이미지 좌표 클릭 이벤트를 브릿지
        public event EventHandler<MainViewerPointerChangedEventArgs> ImageLeftClicked;
        public event EventHandler ImageRightClicked;

        public HImage CurrentImage { get; private set; }

        public string CurrentImagePath { get; private set; }

        public void SetManualToolsEnabled(bool enabled)
        {
            _manualToolsEnabled = enabled;
            if (!enabled)
            {
                ResetManualToolState();
            }

            UpdateContextMenuState();
            Render();
        }

        public void LoadImage(string imagePath)
        {
            DisposeImage();
            CurrentImagePath = imagePath;
            //260509 hbk Phase 20 — ternary expanded
            if (string.IsNullOrWhiteSpace(imagePath)) CurrentImage = null;
            else                                      CurrentImage = new HImage(imagePath);
            UpdateImageMetadata();
            if (HasImage)
            {
                _lastMouseImagePoint = GetImageCenterPoint();
            }

            UpdateContextMenuState();
            ApplyInitialFitView();
            PublishPointerInfo();
            Render();
        }

        public void LoadImage(HImage image)
        {
            DisposeImage();
            CurrentImagePath = null;
            CurrentImage = HalconImageBridge.Clone(image);
            UpdateImageMetadata();
            if (HasImage)
            {
                _lastMouseImagePoint = GetImageCenterPoint();
            }

            UpdateContextMenuState();
            ApplyInitialFitView();
            PublishPointerInfo();
            Render();
        }

        //260408 hbk UpdateDisplayState 4인자 오버로드 추가 (selectedRoiId 지원)
        /// <summary>Updates display with ROI highlight support (per D-01, D-03).</summary>
        public void UpdateDisplayState(IEnumerable<RoiDefinition> rois, string selectedRoiId,
            IEnumerable<EdgeInspectionOverlay> overlays, IEnumerable<string> messages)
        {
            _selectedRoiId = selectedRoiId;
            UpdateDisplayState(rois, overlays, messages);
        }

        public void UpdateDisplayState(IEnumerable<RoiDefinition> rois, IEnumerable<EdgeInspectionOverlay> overlays, IEnumerable<string> messages)
        {
            _rois.Clear();
            if (rois != null)
            {
                _rois.AddRange(rois.Select(roi => roi.Clone()));
            }

            _inspectionOverlays.Clear();
            if (overlays != null)
            {
                _inspectionOverlays.AddRange(overlays.Select(overlay => overlay.Clone()));
            }

            _displayMessages.Clear();
            if (messages != null)
            {
                _displayMessages.AddRange(messages.Where(message => !string.IsNullOrWhiteSpace(message)));
            }

            Render();
        }

        //260529 hbk Phase 39.1-03 G4-01 — overlay 만 갱신 (rois/selectedRoiId/messages 보존). 노드 클릭 시 fai.LastOverlays 재 렌더 용.
        //  HalconViewerControl.SetInspectionOverlays (REPLACE 의미: Clear + AddRange) 와 동일 패턴.
        //  HighlightSelectedRoi 직후에 호출되어 _selectedRoiId 와 _rois 를 보존해야 함 (UpdateDisplayState 는 _rois 도 Clear 함 → 부적합).
        public void SetInspectionOverlays(IEnumerable<EdgeInspectionOverlay> overlays) //260529 hbk Phase 39.1-03 G4-01
        {
            _inspectionOverlays.Clear(); //260529 hbk Phase 39.1-03 G4-01
            if (overlays != null) //260529 hbk Phase 39.1-03 G4-01
            {
                _inspectionOverlays.AddRange(overlays.Select(overlay => overlay.Clone())); //260529 hbk Phase 39.1-03 G4-01
            }
            Render(); //260529 hbk Phase 39.1-03 G4-01
        }

        public void FitImage()
        {
            if (!_isWindowInitialized || CurrentImage == null)
            {
                return;
            }

            SetImagePartExact(CreateFitToWindowImagePart());
        }

        //260408 hbk StartRectangleDrawing 추가 (Rect ROI 드래그 모드)
        /// <summary>Enters rect drag-to-draw mode. User drags on canvas to define a rectangle ROI.</summary>
        public void StartRectangleDrawing()
        {
            _isDrawingRect = true;
            _rectDraftRoi = null;
            _rectDragStart = new Point(0, 0);
            Render();
        }

        //260408 hbk CommitActiveRectangle 추가
        /// <summary>Commits the currently drawn rectangle draft and exits draw mode. Returns the RoiDefinition or null.</summary>
        public RoiDefinition CommitActiveRectangle()
        {
            _isDrawingRect = false;
            var roi = _rectDraftRoi;
            _rectDraftRoi = null;
            Render();
            return roi;
        }

        //260423 hbk Phase 11 D-14 — Circle ROI 드래그 모드 진입
        public void StartCircleDrawing()
        {
            _isDrawingCircle = true;
            _circleDraftCenter = new Point(0, 0);
            _circleDraftRadius = 0;
            Render();
        }

        //260423 hbk Phase 11 D-14 — Circle 드래그 취소/종료용 (대칭성 유지)
        public void CommitActiveCircle()
        {
            _isDrawingCircle = false;
            _circleDraftRadius = 0;
            Render();
        }

        //260423 hbk ROI hit-test
        //260425 hbk Phase 13 D-02 — _rois (FAI) FirstOrDefault null fallback 으로 _datumRoiCandidates 검사
        //260503 hbk Phase 17 D-06 — Edit OFF 시 hit-test 불가 (Rect+Circle+Polygon 단일 gate, carry #9/#13 해소)
        private RoiDefinition HitTestSelectedRoi(Point imagePoint)
        {
            if (!_isEditMode) return null; //260503 hbk Phase 17 D-06 — Edit 모드 단일 gate
            if (!string.IsNullOrEmpty(_selectedRoiId))
            {
                var roi = _rois.FirstOrDefault(r => r.Id == _selectedRoiId);
                if (roi != null)
                {
                    var hit = HitTestOneRoi(roi, imagePoint);
                    if (hit != null) return hit;
                }
            }

            //260425 hbk Phase 13 D-02 — Datum 후보 fallback (_selectedRoiId 무관 hit 허용)
            if (_datumRoiCandidates != null && _datumRoiCandidates.Count > 0)
            {
                foreach (var candidate in _datumRoiCandidates)
                {
                    if (candidate == null) continue;
                    var hit = HitTestOneRoi(candidate, imagePoint);
                    if (hit != null) return hit;
                }
            }

            return null;
        }

        //260503 hbk Phase 17 hotfix#5 — Edit 모드 무관 hit-test (우클릭 시 _selectedRoiId 갱신용).
        //  HitTestSelectedRoi 의 _isEditMode 가드가 막던 진입 경로 우회 — ContextMenu Edit/Delete 활성화에 필요한
        //  _selectedRoiId 를 Edit OFF 에서도 갱신 가능. 좌클릭 분기는 여전히 _isEditMode 단일 gate 유지 (hotfix#4 보존).
        private RoiDefinition HitTestRoiAtPoint(Point imagePoint)
        {
            if (!string.IsNullOrEmpty(_selectedRoiId))
            {
                var roi = _rois.FirstOrDefault(r => r.Id == _selectedRoiId);
                if (roi != null)
                {
                    var hit = HitTestOneRoi(roi, imagePoint);
                    if (hit != null) return hit;
                }
            }
            foreach (var roi in _rois)
            {
                if (roi == null) continue;
                var hit = HitTestOneRoi(roi, imagePoint);
                if (hit != null) return hit;
            }
            if (_datumRoiCandidates != null)
            {
                foreach (var candidate in _datumRoiCandidates)
                {
                    if (candidate == null) continue;
                    var hit = HitTestOneRoi(candidate, imagePoint);
                    if (hit != null) return hit;
                }
            }
            return null;
        }

        //260425 hbk Phase 13 D-02 — Rect/Circle 공통 hit 판정 helper
        private static RoiDefinition HitTestOneRoi(RoiDefinition roi, Point imagePoint)
        {
            if (roi == null) return null;
            if (roi.Shape == RoiShape.Circle)
            {
                double dr = imagePoint.Y - roi.CenterRow;
                double dc = imagePoint.X - roi.CenterCol;
                if (Math.Sqrt(dr * dr + dc * dc) <= roi.Radius) return roi;
                return null;
            }
            if (imagePoint.Y >= roi.Row1 && imagePoint.Y <= roi.Row2 &&
                imagePoint.X >= roi.Column1 && imagePoint.X <= roi.Column2)
            {
                return roi;
            }
            return null;
        }

        //260423 hbk 드로잉 모드 진입 중이면 이동 차단
        private bool IsAnyDrawingModeActive()
        {
            return _isDrawingRect || _isDrawingCircle
                || (_polygonDraftPoints != null && _polygonDraftPoints.Count > 0)
                || (_calibrationPoints != null && _calibrationPoints.Count > 0);
        }

        //260423 hbk 선택 ROI의 Edit 핸들 위치 산출 (Edit 모드 전용)
        // Rect: 4 코너 + 4 변 중점, Circle: 동쪽 반경 1개, Polygon: 각 꼭짓점
        private List<(ResizeHandle Handle, int PolyIndex, Point Pos)> GetEditHandles(RoiDefinition roi)
        {
            var list = new List<(ResizeHandle, int, Point)>();
            if (roi == null) return list;

            if (roi.Shape == RoiShape.Circle)
            {
                //260423 hbk Circle 리사이즈 핸들: N/S/E/W 4개 (모두 동일 로직 — center 기준 거리=newRadius)
                list.Add((ResizeHandle.CircleRadius, -1, new Point(roi.CenterCol + roi.Radius, roi.CenterRow))); // E
                list.Add((ResizeHandle.CircleRadius, -1, new Point(roi.CenterCol - roi.Radius, roi.CenterRow))); // W
                list.Add((ResizeHandle.CircleRadius, -1, new Point(roi.CenterCol, roi.CenterRow - roi.Radius))); // N
                list.Add((ResizeHandle.CircleRadius, -1, new Point(roi.CenterCol, roi.CenterRow + roi.Radius))); // S
                return list;
            }

            if (!string.IsNullOrEmpty(roi.PolygonPoints))
            {
                var pts = ParsePolygonPointsLocal(roi.PolygonPoints);
                for (int i = 0; i < pts.Count; i++)
                {
                    list.Add((ResizeHandle.PolygonVertex, i, pts[i]));
                }
                return list;
            }

            // Rect
            double r1 = roi.Row1, r2 = roi.Row2, c1 = roi.Column1, c2 = roi.Column2;
            double midR = (r1 + r2) / 2.0, midC = (c1 + c2) / 2.0;
            list.Add((ResizeHandle.RectTL, -1, new Point(c1, r1)));
            list.Add((ResizeHandle.RectT,  -1, new Point(midC, r1)));
            list.Add((ResizeHandle.RectTR, -1, new Point(c2, r1)));
            list.Add((ResizeHandle.RectL,  -1, new Point(c1, midR)));
            list.Add((ResizeHandle.RectR,  -1, new Point(c2, midR)));
            list.Add((ResizeHandle.RectBL, -1, new Point(c1, r2)));
            list.Add((ResizeHandle.RectB,  -1, new Point(midC, r2)));
            list.Add((ResizeHandle.RectBR, -1, new Point(c2, r2)));
            return list;
        }

        //260423 hbk 핸들 히트테스트 — 선택 ROI 핸들 중 클릭 위치와 가장 가까운 것 반환
        private (ResizeHandle Handle, int PolyIndex) HitTestEditHandle(RoiDefinition roi, Point imagePoint)
        {
            foreach (var h in GetEditHandles(roi))
            {
                double dx = imagePoint.X - h.Pos.X;
                double dy = imagePoint.Y - h.Pos.Y;
                if (Math.Sqrt(dx * dx + dy * dy) <= HandleHitRadiusImage)
                {
                    return (h.Handle, h.PolyIndex);
                }
            }
            return (ResizeHandle.None, -1);
        }

        //260423 hbk 리사이즈 진행 중 — snapshot 기준으로 target ROI 기하 갱신
        private void ApplyResizeToTarget(RoiDefinition target, Point imagePoint)
        {
            if (target == null || _resizingRoiSnapshot == null) return;
            var snap = _resizingRoiSnapshot;

            if (_resizingHandle == ResizeHandle.CircleRadius)
            {
                double dx = imagePoint.X - snap.CenterCol;
                double dy = imagePoint.Y - snap.CenterRow;
                double newRadius = Math.Sqrt(dx * dx + dy * dy);
                target.Radius = Math.Max(1.0, newRadius);
                return;
            }

            if (_resizingHandle == ResizeHandle.PolygonVertex)
            {
                var pts = ParsePolygonPointsLocal(snap.PolygonPoints);
                if (_resizingPolygonIndex < 0 || _resizingPolygonIndex >= pts.Count) return;
                pts[_resizingPolygonIndex] = imagePoint;
                target.PolygonPoints = SerializePolygonPointsLocal(pts);
                return;
            }

            // Rect 핸들 — snapshot 기준으로 끝점만 갱신
            double r1 = snap.Row1, r2 = snap.Row2, c1 = snap.Column1, c2 = snap.Column2;
            switch (_resizingHandle)
            {
                case ResizeHandle.RectTL: r1 = imagePoint.Y; c1 = imagePoint.X; break;
                case ResizeHandle.RectT:  r1 = imagePoint.Y; break;
                case ResizeHandle.RectTR: r1 = imagePoint.Y; c2 = imagePoint.X; break;
                case ResizeHandle.RectL:  c1 = imagePoint.X; break;
                case ResizeHandle.RectR:  c2 = imagePoint.X; break;
                case ResizeHandle.RectBL: r2 = imagePoint.Y; c1 = imagePoint.X; break;
                case ResizeHandle.RectB:  r2 = imagePoint.Y; break;
                case ResizeHandle.RectBR: r2 = imagePoint.Y; c2 = imagePoint.X; break;
            }
            // 정규화 (Row1 < Row2, Col1 < Col2, 최소 1px)
            target.Row1 = Math.Min(r1, r2);
            target.Row2 = Math.Max(r1, r2);
            target.Column1 = Math.Min(c1, c2);
            target.Column2 = Math.Max(c1, c2);
            if (target.Row2 - target.Row1 < 1.0) target.Row2 = target.Row1 + 1.0;
            if (target.Column2 - target.Column1 < 1.0) target.Column2 = target.Column1 + 1.0;
        }

        //260423 hbk PolygonPoints 로컬 파서/시리얼라이저 (HalconDisplayService의 ParsePolygonPoints 비공개)
        private static List<Point> ParsePolygonPointsLocal(string polygonPoints)
        {
            var list = new List<Point>();
            if (string.IsNullOrWhiteSpace(polygonPoints)) return list;
            var pairs = polygonPoints.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var pair in pairs)
            {
                var xy = pair.Split(',');
                if (xy.Length != 2) continue;
                if (double.TryParse(xy[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double x)
                    && double.TryParse(xy[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double y))
                {
                    list.Add(new Point(x, y));
                }
            }
            return list;
        }

        private static string SerializePolygonPointsLocal(IList<Point> pts)
        {
            if (pts == null || pts.Count == 0) return "";
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < pts.Count; i++)
            {
                if (i > 0) sb.Append(';');
                sb.Append(pts[i].X.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
                sb.Append(',');
                sb.Append(pts[i].Y.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
            }
            return sb.ToString();
        }

        //260423 hbk Edit 모드 핸들 렌더링 — 메인 ROI 렌더 뒤에 호출
        //260426 hbk Phase 14-01 D-01 — Datum Circle Edit 핸들도 보이도록: _isEditMode OR _datumRoiCandidates 존재 시 활성. lookup 은 _rois → _datumRoiCandidates fallback.
        private void RenderEditHandles()
        {
            bool datumCandidatesPresent = (_datumRoiCandidates != null && _datumRoiCandidates.Count > 0);
            if (!_isEditMode && !datumCandidatesPresent) return;

            RoiDefinition roi = null;
            if (_isEditMode && !string.IsNullOrEmpty(_selectedRoiId))
            {
                roi = _rois.FirstOrDefault(r => r.Id == _selectedRoiId && r.IsTaught);
            }
            if (roi == null && datumCandidatesPresent)
            {
                //260426 hbk Phase 14-01 — Datum Circle 후보 fallback (Edit 모드 비활성이어도 핸들 노출)
                roi = _datumRoiCandidates.FirstOrDefault(r => r != null && r.Shape == RoiShape.Circle);
            }
            if (roi == null) return;

            var window = ViewerHost.HalconWindow;
            //260426 hbk Phase 14-01 — Datum 시각 일관 (cyan→yellow) 핸들 색
            window.SetColor("yellow");
            window.SetLineWidth(2);
            foreach (var h in GetEditHandles(roi))
            {
                window.DispRectangle1(
                    h.Pos.Y - HandleHalfSizeImage,
                    h.Pos.X - HandleHalfSizeImage,
                    h.Pos.Y + HandleHalfSizeImage,
                    h.Pos.X + HandleHalfSizeImage);
            }
        }

        //260408 hbk SetPolygonDraft/ClearPolygonDraft 추가 (Polygon ROI 드로잉)
        /// <summary>Sets the polygon draft points for rendering during polygon drawing mode.</summary>
        public void SetPolygonDraft(IList<Point> points, string color)
        {
            //260509 hbk Phase 20 — ternary + ?? expanded
            if (points != null) _polygonDraftPoints = new List<Point>(points);
            else                _polygonDraftPoints = null;
            if (color != null) _polygonColor = color;
            else               _polygonColor = "blue";
            Render();
        }

        //260410 hbk Phase 4 gap fix: Datum overlay state
        private DatumConfig _datumConfig;

        //260602 hbk Phase 40.1 CO-40.1-02 — 측정/Shot/FAI 노드 선택 시 표시할 "결과용" datum 리스트.
        //  단일 _datumConfig(Datum 노드 편집 경로)와 분리 — 한 시퀀스 여러 datum(Phase 37 Side) 동시 표시 가능.
        //  _datumOverlayVisible('Datum 라인' 체크박스) 게이트 공용.
        private List<DatumConfig> _resultDatumOverlays = new List<DatumConfig>();
        private bool _datumSelected;
        //260529 hbk Phase 39.1-04 G4-03 — Datum CTH Edit 모드 트리거. btn_teachDatum.IsChecked 기반 호출자가 SetDatumOverlay 인자로 전달.
        private bool _datumIsEditMode = false; //260529 hbk Phase 39.1-04 G4-03

        //260601 hbk Phase 40.1 #2 — 측정 overlay 토글 게이트 (기본 ON)
        private bool _measurementOverlayVisible = true; //260601 hbk Phase 40.1 #2
        //260601 hbk Phase 40.1 #2 — Datum 라인 토글 게이트 (기본 ON)
        private bool _datumOverlayVisible = true; //260601 hbk Phase 40.1 #2

        //260529 hbk CO-39.1-01 rev2 — FAI CircleDiameter Strip preview state (Edit 모드 = FAI 노드 선택 시).
        //  검사 overlay 와 독립: 검사 결과는 LastOverlays 에 (원 + 지름 라인), preview 는 노드 선택 시 RenderNow 직접 호출.
        private double _faiCirclePreviewRow, _faiCirclePreviewCol, _faiCirclePreviewRadius; //260529 hbk CO-39.1-01 rev2
        private double _faiCirclePreviewStepDeg, _faiCirclePreviewL1Ratio, _faiCirclePreviewL2Ratio; //260529 hbk CO-39.1-01 rev2
        private HTuple _faiCirclePreviewTransform; //260529 hbk CO-39.1-01 rev2 — datum transform (identity 가능)
        private bool _faiCirclePreviewActive = false; //260529 hbk CO-39.1-01 rev2

        //260424 hbk Phase 13 D-07 — 런타임 TryFindDatum 성공 시 주황 십자 렌더 대상 (SetDatumFindResultOverlay 로 주입)
        //  null 이 아니면 Render() 가 _displayService.RenderDatumFindResult 호출.
        //  teach 경로 (_datumConfig + _datumSelected) 와 독립 — 동시 표시 허용 (주황 십자 + 빨간 교점 십자 공존).
        private DatumConfig _datumFindResultOverlay;

        //260410 hbk Phase 4 gap fix: set Datum for overlay rendering
        //260529 hbk Phase 39.1-04 G4-03 — isEditMode 옵션 인자 추가 (기본 false). MainView.GetDatumEditMode() / IsDatumTeachActive 기반 전달.
        public void SetDatumOverlay(DatumConfig datum, bool isSelected, bool isEditMode = false) //260529 hbk Phase 39.1-04 G4-03
        {
            _datumConfig = datum;
            _datumSelected = isSelected;
            _datumIsEditMode = isEditMode; //260529 hbk Phase 39.1-04 G4-03
            Render();
        }

        //260601 hbk Phase 40.1 #2 — 측정 overlay 가시성 토글 (MainView 체크박스에서 호출). 즉시 재렌더.
        public void SetMeasurementOverlayVisible(bool visible)
        {
            _measurementOverlayVisible = visible;
            Render();
        }

        //260601 hbk Phase 40.1 #2 — Datum 라인 가시성 토글 (MainView 체크박스에서 호출). 즉시 재렌더.
        public void SetDatumOverlayVisible(bool visible)
        {
            _datumOverlayVisible = visible;
            Render();
        }

        //260410 hbk Phase 4 gap fix: clear Datum overlay
        public void ClearDatumOverlay()
        {
            _datumConfig = null;
            _datumSelected = false;
            _datumIsEditMode = false; //260529 hbk Phase 39.1-04 G4-03 — Edit 모드 리셋
        }

        //260602 hbk Phase 40.1 CO-40.1-02 — 측정/Shot/FAI 노드의 결과용 datum 기준선 리스트 설정 후 재렌더.
        public void SetResultDatumOverlays(List<DatumConfig> datums)
        {
            _resultDatumOverlays = datums ?? new List<DatumConfig>();
            Render();
        }

        //260602 hbk Phase 40.1 CO-40.1-02 — 결과용 datum 오버레이 제거(이전 노드 잔상 차단) 후 재렌더.
        public void ClearResultDatumOverlays()
        {
            _resultDatumOverlays = new List<DatumConfig>();
            Render();
        }

        //260529 hbk CO-39.1-01 rev2 — FAI CircleDiameter Strip preview set (Edit 모드).
        //  호출자: MainView.RenderInspectionResultForNode 가 param=CircleDiameterMeasurement 일 때 호출.
        //  successes=null gray strip 표시. Render() 호출로 즉시 반영.
        public void SetFaiCirclePreview(double row, double col, double radius,
            double stepDeg, double l1Ratio, double l2Ratio,
            HTuple datumTransform)
        {
            _faiCirclePreviewRow = row;
            _faiCirclePreviewCol = col;
            _faiCirclePreviewRadius = radius;
            _faiCirclePreviewStepDeg = stepDeg;
            _faiCirclePreviewL1Ratio = l1Ratio;
            _faiCirclePreviewL2Ratio = l2Ratio;
            _faiCirclePreviewTransform = datumTransform;
            _faiCirclePreviewActive = true;
            Render();
        }

        //260529 hbk CO-39.1-01 rev2 — FAI Circle preview 클리어 (다른 노드 선택 / Datum 노드 등).
        public void ClearFaiCirclePreview()
        {
            _faiCirclePreviewActive = false;
            _faiCirclePreviewTransform = null;
            Render();
        }

        //260424 hbk Phase 13 D-07 — 런타임 TryFindDatum 성공 시 주황 십자 오버레이 set
        public void SetDatumFindResultOverlay(DatumConfig datum)
        {
            _datumFindResultOverlay = datum;
            Render();
        }

        //260424 hbk Phase 13 D-08 — 실패 / 재시도 시 주황 십자 오버레이 clear
        public void ClearDatumFindResultOverlay()
        {
            _datumFindResultOverlay = null;
            Render();
        }

        //260425 hbk Phase 13 D-02 — Datum ROI 편집 후보 (Datum 노드 선택 시 MainView 가 주입)
        private List<RoiDefinition> _datumRoiCandidates = new List<RoiDefinition>();

        //260425 hbk Phase 13 D-02 — Datum ROI 후보 주입 (MainView publish 진입점)
        public void SetDatumRoiCandidates(IList<RoiDefinition> datumRois)
        {
            if (datumRois == null)
            {
                _datumRoiCandidates.Clear();
            }
            else
            {
                _datumRoiCandidates = datumRois.Where(r => r != null).Select(r => r.Clone()).ToList();
            }
        }

        //260425 hbk Phase 13 D-02 — Datum 노드 비선택 시 후보 clear
        public void ClearDatumRoiCandidates()
        {
            _datumRoiCandidates.Clear();
        }

        //260408 hbk Calibration 십자+라인 오버레이
        public void SetCalibrationOverlay(IList<Point> points)
        {
            //260509 hbk Phase 20 — ternary expanded
            if (points != null) _calibrationPoints = new List<Point>(points);
            else                _calibrationPoints = null;
            Render();
        }

        public void ClearCalibrationOverlay()
        {
            _calibrationPoints = null;
            Render();
        }

        /// <summary>Clears the polygon draft overlay.</summary>
        public void ClearPolygonDraft()
        {
            _polygonDraftPoints = null;
            Render();
        }

        public void Dispose()
        {
            DisposeImage();
        }

        private void MainResultViewerControl_Unloaded(object sender, RoutedEventArgs e)
        {
            DisposeImage();
            Mouse.OverrideCursor = null;
        }

        private void Render()
        {
            if (!_isWindowInitialized || _renderPending)
            {
                return;
            }

            _renderPending = true;
            Dispatcher.BeginInvoke(DispatcherPriority.Render, new Action(RenderNow));
        }

        private void RenderNow()
        {
            _renderPending = false;
            if (!_isWindowInitialized)
            {
                return;
            }

            if (!HasImage)
            {
                return;
            }

            //260601 hbk Phase 40.1 #2 — 측정 overlay 토글 OFF 시 빈 리스트 (rois/messages 보존)
            List<EdgeInspectionOverlay> measOverlays = _measurementOverlayVisible
                ? _inspectionOverlays.Concat(BuildTransientOverlays()).ToList()
                : new List<EdgeInspectionOverlay>();
            _displayService.Render(
                ViewerHost.HalconWindow,
                CurrentImage,
                _rois,
                _selectedRoiId,
                _rectDraftRoi,
                measOverlays,
                _displayMessages.Concat(BuildTransientMessages()).ToList());

            //260408 hbk Render polygon draft overlay after main render
            if (_polygonDraftPoints != null && _polygonDraftPoints.Count > 0)
            {
                if (_polygonDraftPoints.Count >= 3)
                    _displayService.RenderPolygon(ViewerHost.HalconWindow, _polygonDraftPoints, _polygonColor, 2);
                _displayService.RenderPolygonPoints(ViewerHost.HalconWindow, _polygonDraftPoints, "red");
            }

            //260408 hbk Render calibration overlay
            if (_calibrationPoints != null && _calibrationPoints.Count > 0)
            {
                _displayService.RenderCalibrationOverlay(ViewerHost.HalconWindow, _calibrationPoints);
            }

            //260410 hbk Phase 4 gap fix: render Datum Line ROI overlay
            //260529 hbk Phase 39.1-04 G4-03 — _datumIsEditMode 전달 (CTH Edit 모드 분리)
            //260601 hbk Phase 40.1 #2 — Datum 라인 토글 게이트
            if (_datumConfig != null && _datumOverlayVisible)
            {
                _displayService.RenderDatumOverlay(ViewerHost.HalconWindow, _datumConfig, _datumSelected, _datumIsEditMode); //260529 hbk Phase 39.1-04 G4-03
            }

            //260602 hbk Phase 40.1 CO-40.1-02 — 측정/Shot/FAI 노드 선택 시 그 시퀀스 datum 기준선도 함께 표시 (토글 게이트 공용).
            //  isSelected=false → blue, editMode=false. 단일 _datumConfig 경로(Datum 노드)와 공존.
            if (_datumOverlayVisible && _resultDatumOverlays != null)
            {
                foreach (DatumConfig d in _resultDatumOverlays)
                {
                    if (d != null)
                    {
                        _displayService.RenderDatumOverlay(ViewerHost.HalconWindow, d, false, false);
                    }
                }
            }

            //260529 hbk CO-39.1-01 rev2 — FAI CircleDiameter Strip preview (Edit 모드 = FAI 노드 선택 시).
            //  검사 결과 (LastOverlays 의 원 + 지름) 가 그려진 후 위에 strip 사각형 preview 를 덧붙임.
            if (_faiCirclePreviewActive && _faiCirclePreviewRadius > 0)
            {
                _displayService.RenderFaiCircleStripPreview(ViewerHost.HalconWindow,
                    _faiCirclePreviewRow, _faiCirclePreviewCol, _faiCirclePreviewRadius,
                    _faiCirclePreviewStepDeg, _faiCirclePreviewL1Ratio, _faiCirclePreviewL2Ratio,
                    _faiCirclePreviewTransform); //260529 hbk CO-39.1-01 rev2
            }

            //260424 hbk Phase 13 D-07 — 런타임 TryFindDatum 결과 주황 십자 오버레이 (teach 경로와 독립, 동시 표시 허용)
            if (_datumFindResultOverlay != null)
            {
                _displayService.RenderDatumFindResult(ViewerHost.HalconWindow, _datumFindResultOverlay);
            }

            //260423 hbk Phase 11 D-14 — Circle 드래그 미리보기 렌더
            if (_isDrawingCircle && _circleDraftRadius > 0)
            {
                _displayService.RenderCircleDraft(ViewerHost.HalconWindow, _circleDraftCenter.Y, _circleDraftCenter.X, _circleDraftRadius);
            }

            //260423 hbk Edit 모드 핸들 렌더 (메인 ROI 위에 덧그림)
            RenderEditHandles();
        }

        private void ViewerHost_HInitWindow(object sender, EventArgs e)
        {
            _isWindowInitialized = true;
            if (HasImage)
            {
                ApplyInitialFitView();
            }

            Render();
        }

        private void ViewerHost_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (!_isWindowInitialized || !HasImage)
            {
                return;
            }

            SetImagePartExact(CreateFitToWindowImagePart());
        }

        private void ViewerHost_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount < 2)
            {
                return;
            }

            FitImage();
            e.Handled = true;
        }

        private void ViewerHost_HMouseWheel(object sender, HMouseEventArgsWPF e)
        {
            if (!HasImage)
            {
                return;
            }

            var mouseState = GetMouseState();
            _lastMouseImagePoint = mouseState.ImagePoint;
            //260509 hbk Phase 20 — ternary expanded
            double zoomFactor;
            if (e.Delta > 0) zoomFactor = ZoomInScaleFactor;
            else             zoomFactor = ZoomOutScaleFactor;
            ZoomAtPointer(mouseState.ImagePoint.Y, mouseState.ImagePoint.X, zoomFactor);
            PublishPointerInfo();
        }

        private void ViewerHost_HMouseDown(object sender, HMouseEventArgsWPF e)
        {
            var mouseState = GetMouseState();
            _lastMouseImagePoint = mouseState.ImagePoint;
            if ((mouseState.Buttons & HalconRightButton) == HalconRightButton)
            {
                //260503 hbk Phase 17 hotfix#5 (C/D 회귀) — 우클릭 시 Edit 모드 무관 hit-test 로 _selectedRoiId 갱신.
                //  사유: hotfix#4 가 Phase 13 datumCandidatesPresent bypass 를 제거하면서 Edit 진입점이 막힘 (닭/달걀).
                //  HitTestRoiAtPoint 는 _isEditMode 게이트 없이 hit-test → ContextMenu Edit/Delete 활성화에 필요한 _selectedRoiId 보장.
                //  이동/리사이즈는 좌클릭 분기에서 여전히 Edit ON 일 때만 시작 (단일 gate 보존).
                var rightClickHit = HitTestRoiAtPoint(mouseState.ImagePoint);
                if (rightClickHit != null && rightClickHit.Id != null)
                {
                    _selectedRoiId = rightClickHit.Id;
                }

                //260503 hbk Phase 17 hotfix#5 — Edit ON + 빈 영역 우클릭 → 모드 종료 (Phase 13 종료 단축 보존)
                if (_isEditMode && rightClickHit == null)
                {
                    SetEditMode(false);
                    PublishPointerInfo();
                    return;
                }

                //260408 hbk 우클릭 이벤트 브릿지 (Polygon 완성용) — Edit OFF 일 때만 Polygon 우클릭 처리
                if (!_isEditMode && rightClickHit == null && ImageRightClicked != null)
                {
                    //260509 hbk Phase 20 — D-02 race-safe handler temp
                    var imageRightClickedHandler = ImageRightClicked;
                    if (imageRightClickedHandler != null) imageRightClickedHandler(this, EventArgs.Empty);
                    PublishPointerInfo();
                    return;
                }

                UpdateContextMenuState();
                OpenContextMenu();
                PublishPointerInfo();
                return;
            }

            if ((mouseState.Buttons & HalconLeftButton) != HalconLeftButton)
            {
                return;
            }

            //260423 hbk Edit 모드 전용: 핸들 히트 → 리사이즈 시작, 바디 히트 → 이동 시작
            //260503 hbk Phase 17 hotfix#4 (Test 6 / D-06) — Phase 13 D-01 의 datumCandidatesPresent bypass 제거.
            //  사유: Edit OFF 인데도 Datum 후보 publish 만으로 핸들 hit-test 활성 → 사이즈 변경 가능 회귀.
            //  UI-SPEC § _isEditMode 상태머신 (단일 gate) 준수: Edit ON 일 때만 ROI 변형 허용.
            if (_isEditMode && !IsAnyDrawingModeActive() && HasImage)
            {
                //260426 hbk Phase 14-01 D-02 — Edit 모드 시 핸들 hit-test 활성 (Datum Circle resize 지원, 4 핸들 동일 동작)
                {
                    RoiDefinition selectedRoi = null;
                    if (!string.IsNullOrEmpty(_selectedRoiId))
                    {
                        selectedRoi = _rois.FirstOrDefault(r => r.Id == _selectedRoiId && r.IsTaught);
                    }
                    if (selectedRoi == null && _datumRoiCandidates != null && _datumRoiCandidates.Count > 0)
                    {
                        //260426 hbk Phase 14-01 — Datum Circle fallback (mouse 위치 hit-test 는 GetEditHandles 가 처리)
                        selectedRoi = _datumRoiCandidates.FirstOrDefault(r => r != null && r.Shape == RoiShape.Circle);
                    }
                    if (selectedRoi != null)
                    {
                        var handleHit = HitTestEditHandle(selectedRoi, mouseState.ImagePoint);
                        if (handleHit.Handle != ResizeHandle.None)
                        {
                            _isResizingRoi = true;
                            _resizingHandle = handleHit.Handle;
                            _resizingPolygonIndex = handleHit.PolyIndex;
                            _resizingRoiSnapshot = selectedRoi.Clone();
                            SetPanCursor(Cursors.SizeAll);
                            PublishPointerInfo();
                            return;
                        }
                    }
                }

                var hitRoi = HitTestSelectedRoi(mouseState.ImagePoint);
                if (hitRoi != null)
                {
                    //260425 hbk Phase 13 D-02 — Datum hit 시 _selectedRoiId 도 set → 기존 컨텍스트 메뉴 Delete 가 그대로 동작
                    if (hitRoi.Id != null && hitRoi.Id.StartsWith("Datum."))
                    {
                        _selectedRoiId = hitRoi.Id;
                    }
                    _isMovingRoi = true;
                    _moveStartImagePoint = mouseState.ImagePoint;
                    _movingRoiSnapshot = hitRoi.Clone();
                    SetPanCursor(Cursors.SizeAll);
                    PublishPointerInfo();
                    return;
                }
            }

            //260408 hbk Rect drawing mode — start drag
            //260503 hbk Phase 17 D-05 — 좌클릭+드래그부터 그리기 시작 (캔버스 hover 미리보기 제거). 본 분기는 HMouseDown 좌클릭 게이트(L765) 통과 후 진입 — 명시적 D-05 가드.
            if (_isDrawingRect && HasImage)
            {
                if ((mouseState.Buttons & HalconLeftButton) != HalconLeftButton) return; //260503 hbk Phase 17 D-05 — 좌클릭 없이 미리보기 시작 금지
                _rectDragStart = mouseState.ImagePoint;
                _rectDraftRoi = new RoiDefinition
                {
                    Id = "draft",
                    Row1 = _rectDragStart.Y,
                    Column1 = _rectDragStart.X,
                    Row2 = _rectDragStart.Y,
                    Column2 = _rectDragStart.X,
                    IsTaught = false
                };
                PublishPointerInfo();
                return;
            }

            //260423 hbk Phase 11 D-14 — Circle drawing mode: start drag (center click)
            //260503 hbk Phase 17 D-05 — 좌클릭+드래그부터 그리기 시작 (캔버스 hover 미리보기 제거)
            if (_isDrawingCircle && HasImage)
            {
                if ((mouseState.Buttons & HalconLeftButton) != HalconLeftButton) return; //260503 hbk Phase 17 D-05 — 좌클릭 없이 미리보기 시작 금지
                _circleDraftCenter = mouseState.ImagePoint;
                _circleDraftRadius = 0;
                PublishPointerInfo();
                return;
            }

            //260408 hbk 좌클릭 이벤트 브릿지 (Polygon 점 추가, Calibration 점 선택용)
            if (ImageLeftClicked != null && HasImage)
            {
                var pt = mouseState.ImagePoint;
                //260509 hbk Phase 20 — D-02 race-safe handler temp
                var imageLeftClickedHandler = ImageLeftClicked;
                if (imageLeftClickedHandler != null) imageLeftClickedHandler(this, new MainViewerPointerChangedEventArgs(pt.X, pt.Y, null));
                PublishPointerInfo();
                return;
            }

            if (_manualMeasureMode && _manualToolsEnabled && HasImage)
            {
                ApplyManualMeasurePoint(mouseState.ImagePoint);
                PublishPointerInfo();
                return;
            }

            if (!CanPanCurrentImage())
            {
                return;
            }

            _isPanningImage = true;
            _panStartPoint = new Point(e.X, e.Y);
            _panStartImagePart = GetImagePart();
            ViewerHost.Focus();
            SetPanCursor(Cursors.Hand);
            _lastMouseImagePoint = mouseState.ImagePoint;
            PublishPointerInfo();
        }

        private void ViewerHost_HMouseMove(object sender, HMouseEventArgsWPF e)
        {
            if (!HasImage)
            {
                return;
            }

            var mouseState = GetMouseState();
            _lastMouseImagePoint = mouseState.ImagePoint;

            //260423 hbk 리사이즈 중: snapshot 기준 target 기하 갱신
            if (_isResizingRoi && _resizingRoiSnapshot != null)
            {
                var target = _rois.FirstOrDefault(r => r.Id == _resizingRoiSnapshot.Id);
                //260426 hbk Phase 14-01 — Datum Circle resize 시 _rois 에 없으면 _datumRoiCandidates lookup
                if (target == null && _datumRoiCandidates != null)
                {
                    target = _datumRoiCandidates.FirstOrDefault(r => r != null && r.Id == _resizingRoiSnapshot.Id);
                }
                if (target != null)
                {
                    ApplyResizeToTarget(target, mouseState.ImagePoint);
                    Render();
                }
                PublishPointerInfo();
                return;
            }

            //260423 hbk ROI 이동 중: _rois 해당 항목 좌표 갱신 후 Render
            if (_isMovingRoi && _movingRoiSnapshot != null)
            {
                double dr = mouseState.ImagePoint.Y - _moveStartImagePoint.Y;
                double dc = mouseState.ImagePoint.X - _moveStartImagePoint.X;
                var target = _rois.FirstOrDefault(r => r.Id == _movingRoiSnapshot.Id);
                if (target != null)
                {
                    if (target.Shape == RoiShape.Circle)
                    {
                        target.CenterRow = _movingRoiSnapshot.CenterRow + dr;
                        target.CenterCol = _movingRoiSnapshot.CenterCol + dc;
                    }
                    else
                    {
                        target.Row1 = _movingRoiSnapshot.Row1 + dr;
                        target.Column1 = _movingRoiSnapshot.Column1 + dc;
                        target.Row2 = _movingRoiSnapshot.Row2 + dr;
                        target.Column2 = _movingRoiSnapshot.Column2 + dc;
                    }
                    Render();
                }
                else
                {
                    //260425 hbk Phase 13 D-02 — Datum ROI 드래그 비주얼: _rois 에 없으면 _datumRoiCandidates 검색
                    var datumTarget = _datumRoiCandidates != null
                        ? _datumRoiCandidates.FirstOrDefault(r => r != null && r.Id == _movingRoiSnapshot.Id)
                        : null;
                    if (datumTarget != null)
                    {
                        if (datumTarget.Shape == RoiShape.Circle)
                        {
                            datumTarget.CenterRow = _movingRoiSnapshot.CenterRow + dr;
                            datumTarget.CenterCol = _movingRoiSnapshot.CenterCol + dc;
                        }
                        else
                        {
                            datumTarget.Row1 = _movingRoiSnapshot.Row1 + dr;
                            datumTarget.Column1 = _movingRoiSnapshot.Column1 + dc;
                            datumTarget.Row2 = _movingRoiSnapshot.Row2 + dr;
                            datumTarget.Column2 = _movingRoiSnapshot.Column2 + dc;
                        }
                        Render();
                    }
                }
                PublishPointerInfo();
                return;
            }

            //260408 hbk Update rect draft while dragging
            if (_isDrawingRect && _rectDraftRoi != null)
            {
                _rectDraftRoi = new RoiDefinition
                {
                    Id = "draft",
                    Row1 = Math.Min(_rectDragStart.Y, mouseState.ImagePoint.Y),
                    Column1 = Math.Min(_rectDragStart.X, mouseState.ImagePoint.X),
                    Row2 = Math.Max(_rectDragStart.Y, mouseState.ImagePoint.Y),
                    Column2 = Math.Max(_rectDragStart.X, mouseState.ImagePoint.X),
                    IsTaught = true
                };
                Render();
                PublishPointerInfo();
                return;
            }

            //260423 hbk Phase 11 D-14 — Circle drawing mode: update radius while dragging
            //260503 hbk Phase 17 hotfix#4 (Test 5) — 좌클릭 누른 상태 + center 클릭 완료 후에만 반지름 갱신.
            //  기존: _circleDraftCenter (0,0) 초기화 + hover 만으로 (0,0)→포인터 거대 원 렌더 회귀.
            //  수정: 좌클릭 가드 + center sentinel((0,0)) 가드 (Rect 분기의 _rectDraftRoi != null 패턴 동일).
            if (_isDrawingCircle && HasImage)
            {
                if ((mouseState.Buttons & HalconLeftButton) != HalconLeftButton) return;
                if (_circleDraftCenter.X == 0 && _circleDraftCenter.Y == 0) return;
                double dx = mouseState.ImagePoint.X - _circleDraftCenter.X;
                double dy = mouseState.ImagePoint.Y - _circleDraftCenter.Y;
                _circleDraftRadius = Math.Sqrt(dx * dx + dy * dy);
                Render();
                PublishPointerInfo();
                return;
            }

            if (!_isPanningImage)
            {
                //260509 hbk Phase 20 — ternary expanded
                if (CanPanCurrentImage()) SetPanCursor(Cursors.Hand);
                else                      SetPanCursor(Cursors.Arrow);
                PublishPointerInfo();
                return;
            }

            var currentPoint = new Point(e.X, e.Y);
            var deltaX = currentPoint.X - _panStartPoint.X;
            var deltaY = currentPoint.Y - _panStartPoint.Y;

            if (ViewerHost.ActualWidth <= 0 || ViewerHost.ActualHeight <= 0)
            {
                return;
            }

            var imageDeltaColumn = deltaX * (_panStartImagePart.Width / ViewerHost.ActualWidth);
            var imageDeltaRow = deltaY * (_panStartImagePart.Height / ViewerHost.ActualHeight);
            SetImagePart(new Rect(
                _panStartImagePart.Left - imageDeltaColumn,
                _panStartImagePart.Top - imageDeltaRow,
                _panStartImagePart.Width,
                _panStartImagePart.Height));
            PublishPointerInfo();
        }

        private void ViewerHost_HMouseUp(object sender, HMouseEventArgsWPF e)
        {
            //260423 hbk 리사이즈 완료: 절대 기하 RoiGeometryChanged로 발생
            //260426 hbk Phase 14-01 D-04 — Datum Circle resize 완료 시 RoiGeometryChanged 발행 (단일 이벤트 확장, 신규 이벤트 X)
            if (_isResizingRoi && _resizingRoiSnapshot != null)
            {
                var target = _rois.FirstOrDefault(r => r.Id == _resizingRoiSnapshot.Id);
                if (target == null && _datumRoiCandidates != null)
                {
                    target = _datumRoiCandidates.FirstOrDefault(r => r != null && r.Id == _resizingRoiSnapshot.Id);
                }
                string movedId = _resizingRoiSnapshot.Id;
                RoiShape shape = _resizingRoiSnapshot.Shape;
                _isResizingRoi = false;
                _resizingRoiSnapshot = null;
                _resizingHandle = ResizeHandle.None;
                _resizingPolygonIndex = -1;
                //260509 hbk Phase 20 — ternary expanded
                if (CanPanCurrentImage()) SetPanCursor(Cursors.Hand);
                else                      SetPanCursor(Cursors.Arrow);
                if (target != null)
                {
                    //260509 hbk Phase 20 — D-02 race-safe handler temp
                    var roiGeometryChangedHandler = RoiGeometryChanged;
                    if (roiGeometryChangedHandler != null) roiGeometryChangedHandler(this, new RoiGeometryChangedArgs
                    {
                        RoiId = movedId,
                        Shape = shape,
                        Row1 = target.Row1,
                        Column1 = target.Column1,
                        Row2 = target.Row2,
                        Column2 = target.Column2,
                        CenterRow = target.CenterRow,
                        CenterCol = target.CenterCol,
                        Radius = target.Radius,
                        PolygonPoints = target.PolygonPoints
                    });
                }
                return;
            }

            //260423 hbk ROI 이동 완료: 델타 계산 후 RoiMoveCompleted 발생
            if (_isMovingRoi && _movingRoiSnapshot != null)
            {
                double dr = 0;
                double dc = 0;
                var target = _rois.FirstOrDefault(r => r.Id == _movingRoiSnapshot.Id);
                if (target != null)
                {
                    if (target.Shape == RoiShape.Circle)
                    {
                        dr = target.CenterRow - _movingRoiSnapshot.CenterRow;
                        dc = target.CenterCol - _movingRoiSnapshot.CenterCol;
                    }
                    else
                    {
                        dr = target.Row1 - _movingRoiSnapshot.Row1;
                        dc = target.Column1 - _movingRoiSnapshot.Column1;
                    }
                }
                else
                {
                    //260425 hbk Phase 13 D-02 — Datum ROI 이동 완료: _rois 에 없으면 _datumRoiCandidates 로 델타 계산
                    var datumTarget = _datumRoiCandidates != null
                        ? _datumRoiCandidates.FirstOrDefault(r => r != null && r.Id == _movingRoiSnapshot.Id)
                        : null;
                    if (datumTarget != null)
                    {
                        if (datumTarget.Shape == RoiShape.Circle)
                        {
                            dr = datumTarget.CenterRow - _movingRoiSnapshot.CenterRow;
                            dc = datumTarget.CenterCol - _movingRoiSnapshot.CenterCol;
                        }
                        else
                        {
                            dr = datumTarget.Row1 - _movingRoiSnapshot.Row1;
                            dc = datumTarget.Column1 - _movingRoiSnapshot.Column1;
                        }
                    }
                }
                string movedId = _movingRoiSnapshot.Id;
                _isMovingRoi = false;
                _movingRoiSnapshot = null;
                //260509 hbk Phase 20 — ternary expanded
                if (CanPanCurrentImage()) SetPanCursor(Cursors.Hand);
                else                      SetPanCursor(Cursors.Arrow);
                if (Math.Abs(dr) > 0.5 || Math.Abs(dc) > 0.5)
                {
                    //260509 hbk Phase 20 — D-02 race-safe handler temp
                    var roiMoveCompletedHandler = RoiMoveCompleted;
                    if (roiMoveCompletedHandler != null) roiMoveCompletedHandler(this, new RoiMoveCompletedArgs
                    {
                        RoiId = movedId,
                        DeltaRow = dr,
                        DeltaCol = dc
                    });
                }
                return;
            }

            //260408 hbk On mouse up during rect drawing, finalize and notify
            if (_isDrawingRect && _rectDraftRoi != null)
            {
                _isDrawingRect = false;
                Render();
                //260509 hbk Phase 20 — D-02 race-safe handler temp
                var rectDrawingCompletedHandler = RectDrawingCompleted;
                if (rectDrawingCompletedHandler != null) rectDrawingCompletedHandler(this, EventArgs.Empty);
                return;
            }

            //260423 hbk Phase 11 D-14 — Circle drawing finalize: raise CircleDrawingCompleted
            if (_isDrawingCircle && _circleDraftRadius > 0)
            {
                _isDrawingCircle = false;
                double cr = _circleDraftCenter.Y;  // image Row = Y
                double cc = _circleDraftCenter.X;  // image Col = X
                double rad = _circleDraftRadius;
                Render();
                //260509 hbk Phase 20 — D-02 race-safe handler temp
                var circleDrawingCompletedHandler = CircleDrawingCompleted;
                if (circleDrawingCompletedHandler != null) circleDrawingCompletedHandler(this, new CircleDrawCompletedArgs { CenterRow = cr, CenterCol = cc, Radius = rad });
                _circleDraftRadius = 0;
                return;
            }

            EndPan();
        }

        private void ZoomInMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (!HasImage)
            {
                return;
            }

            var part = GetImagePart();
            ZoomAtPointer(part.Top + (part.Height / 2.0), part.Left + (part.Width / 2.0), ZoomInScaleFactor);
        }

        private void ZoomOutMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (!HasImage)
            {
                return;
            }

            var part = GetImagePart();
            ZoomAtPointer(part.Top + (part.Height / 2.0), part.Left + (part.Width / 2.0), ZoomOutScaleFactor);
        }

        private void FitImageMenuItem_Click(object sender, RoutedEventArgs e)
        {
            FitImage();
        }

        private void ManualMeasureMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (!_manualToolsEnabled || !HasImage)
            {
                return;
            }

            _manualMeasureMode = !_manualMeasureMode;
            if (!_manualMeasureMode)
            {
                _manualMeasureStartPoint = null;
                _manualMeasureEndPoint = null;
            }

            UpdateContextMenuState();
            Render();
        }

        private void ClearMeasureMenuItem_Click(object sender, RoutedEventArgs e)
        {
            _manualMeasureStartPoint = null;
            _manualMeasureEndPoint = null;
            _manualMeasureMode = false;
            UpdateContextMenuState();
            Render();
        }

        private void ZoomAtPointer(double row, double column, double scaleFactor)
        {
            var current = GetImagePart();
            var newWidth = current.Width * scaleFactor;
            var newHeight = current.Height * scaleFactor;
            //260509 hbk Phase 20 — ternaries expanded
            double rowRatio;
            if (current.Height <= 0) rowRatio = 0.5;
            else                     rowRatio = (row - current.Top) / current.Height;
            double columnRatio;
            if (current.Width <= 0) columnRatio = 0.5;
            else                    columnRatio = (column - current.Left) / current.Width;
            var newTop = row - (newHeight * rowRatio);
            var newLeft = column - (newWidth * columnRatio);
            SetImagePart(new Rect(newLeft, newTop, newWidth, newHeight));
        }

        private bool CanPanCurrentImage()
        {
            if (!HasImage || _manualMeasureMode || !_manualToolsEnabled)
            {
                return false;
            }

            var imagePart = GetImagePart();
            return imagePart.Width < _imageWidth || imagePart.Height < _imageHeight;
        }

        private void SetImagePart(Rect imagePart)
        {
            if (!_isWindowInitialized || !HasImage)
            {
                return;
            }

            var normalizedWidth = Math.Max(20.0, imagePart.Width);
            var normalizedHeight = Math.Max(20.0, imagePart.Height);

            if (ViewerHost.ActualWidth > 0 && ViewerHost.ActualHeight > 0)
            {
                var viewerAspect = ViewerHost.ActualWidth / ViewerHost.ActualHeight;
                var partAspect = normalizedWidth / normalizedHeight;
                if (partAspect > viewerAspect)
                {
                    normalizedHeight = normalizedWidth / viewerAspect;
                }
                else
                {
                    normalizedWidth = normalizedHeight * viewerAspect;
                }
            }

            var horizontalMargin = Math.Max(normalizedWidth, _imageWidth) * 0.75;
            var verticalMargin = Math.Max(normalizedHeight, _imageHeight) * 0.75;

            var minLeft = -horizontalMargin;
            var maxLeft = _imageWidth - normalizedWidth + horizontalMargin;
            var minTop = -verticalMargin;
            var maxTop = _imageHeight - normalizedHeight + verticalMargin;

            var normalizedLeft = Math.Max(minLeft, Math.Min(maxLeft, imagePart.Left));
            var normalizedTop = Math.Max(minTop, Math.Min(maxTop, imagePart.Top));

            SetPartInternal(new Rect(normalizedLeft, normalizedTop, normalizedWidth, normalizedHeight));
            //260509 hbk Phase 20 — ternary expanded
            if (CanPanCurrentImage()) SetPanCursor(Cursors.Hand);
            else                      SetPanCursor(Cursors.Arrow);
            Render();
        }

        private void SetImagePartExact(Rect imagePart)
        {
            if (!_isWindowInitialized || !HasImage)
            {
                return;
            }

            SetPartInternal(imagePart);
            //260509 hbk Phase 20 — ternary expanded
            if (CanPanCurrentImage()) SetPanCursor(Cursors.Hand);
            else                      SetPanCursor(Cursors.Arrow);
            Render();
        }

        private void ApplyInitialFitView()
        {
            if (!_isWindowInitialized || !HasImage)
            {
                return;
            }

            SetImagePartExact(CreateFitToWindowImagePart());
            //260509 hbk Phase 20 — ternary expanded
            if (CanPanCurrentImage()) SetPanCursor(Cursors.Hand);
            else                      SetPanCursor(Cursors.Arrow);
        }

        private void SetPanCursor(Cursor cursor)
        {
            Cursor = cursor;
            ViewerHost.Cursor = cursor;
            ViewerBorder.Cursor = cursor;
            if (_isPanningImage || ViewerHost.IsMouseOver || ViewerBorder.IsMouseOver || IsMouseOver)
            {
                Mouse.OverrideCursor = cursor;
            }
        }

        private void EndPan()
        {
            if (!_isPanningImage)
            {
                return;
            }

            _isPanningImage = false;
            //260509 hbk Phase 20 — ternary expanded
            if (CanPanCurrentImage()) SetPanCursor(Cursors.Hand);
            else                      SetPanCursor(Cursors.Arrow);
            PublishPointerInfo();
        }

        private void ViewerHost_MouseEnter(object sender, MouseEventArgs e)
        {
            //260509 hbk Phase 20 — ternary expanded
            if (CanPanCurrentImage()) SetPanCursor(Cursors.Hand);
            else                      SetPanCursor(Cursors.Arrow);
        }

        private void ViewerHost_MouseLeave(object sender, MouseEventArgs e)
        {
            Mouse.OverrideCursor = null;
        }

        private void PublishPointerInfo()
        {
            if (CurrentImage == null)
            {
                //260509 hbk Phase 20 — D-02 race-safe handler temp
                var pointerInfoChangedHandlerNull = PointerInfoChanged;
                if (pointerInfoChangedHandlerNull != null) pointerInfoChangedHandlerNull(this, new MainViewerPointerChangedEventArgs(0, 0, null));
                return;
            }

            double? grayValue = null;

            var x = Math.Max(0.0, Math.Min(_imageWidth - 1.0, _lastMouseImagePoint.X));
            var y = Math.Max(0.0, Math.Min(_imageHeight - 1.0, _lastMouseImagePoint.Y));

            try
            {
                grayValue = CurrentImage.GetGrayval((int)Math.Round(y), (int)Math.Round(x))[0].D;
            }
            catch
            {
            }

            //260509 hbk Phase 20 — D-02 race-safe handler temp
            var pointerInfoChangedHandler = PointerInfoChanged;
            if (pointerInfoChangedHandler != null) pointerInfoChangedHandler(this, new MainViewerPointerChangedEventArgs(x, y, grayValue));
        }

        private void ApplyManualMeasurePoint(Point imagePoint)
        {
            if (!_manualMeasureStartPoint.HasValue || (_manualMeasureStartPoint.HasValue && _manualMeasureEndPoint.HasValue))
            {
                _manualMeasureStartPoint = imagePoint;
                _manualMeasureEndPoint = null;
            }
            else
            {
                _manualMeasureEndPoint = imagePoint;
            }

            Render();
        }

        private MouseState GetMouseState()
        {
            if (!_isWindowInitialized)
            {
                return new MouseState(_lastMouseImagePoint, 0);
            }

            try
            {
                int row;
                int col;
                int button;
                ViewerHost.HalconWindow.GetMposition(out row, out col, out button);
                return new MouseState(new Point(col, row), button);
            }
            catch (HalconException)
            {
                return new MouseState(_lastMouseImagePoint, 0);
            }
        }

        private void UpdateContextMenuState()
        {
            if (ManualMeasureMenuItem == null || ClearMeasureMenuItem == null)
            {
                return;
            }

            var isImageLoaded = CurrentImage != null;
            CrosshairMenuItem.IsEnabled = _manualToolsEnabled && isImageLoaded;
            CrosshairMenuItem.IsCheckable = true;
            CrosshairMenuItem.IsChecked = _crosshairEnabled;
            ManualMeasureMenuItem.IsEnabled = _manualToolsEnabled && isImageLoaded;
            ManualMeasureMenuItem.IsCheckable = true;
            ManualMeasureMenuItem.IsChecked = _manualMeasureMode;
            ClearMeasureMenuItem.IsEnabled = isImageLoaded && (_manualMeasureStartPoint.HasValue || _manualMeasureEndPoint.HasValue || _manualMeasureMode);

            //260423 hbk Edit/Delete ROI 메뉴 활성화 (선택 ROI 존재 + 드로잉 모드 아님)
            //260426 hbk Phase 13-05 hotfix — Datum ROI (_datumRoiCandidates) 도 hasSelectedRoi 판정에 포함.
            //  13-03 에서 Datum hit 시 _selectedRoiId = "Datum.*" 를 set 하지만 _rois (FAI 전용) 에는 없으므로 항상 false 였음.
            //260503 hbk Phase 17 hotfix#5 — Edit 메뉴는 진입점 보장을 위해 (이미지 + 그리기 X) 일 때 항상 활성.
            //  이전: hasSelectedRoi 게이트 → _selectedRoiId 비어있으면 Edit 토글 클릭 불가 (닭/달걀).
            //  지금: Edit 는 토글이므로 ROI 선택 무관하게 모드 진입 가능. Delete 만 _selectedRoiId 요구.
            if (EditRoiMenuItem != null && DeleteRoiMenuItem != null)
            {
                bool hasSelectedRoi = !string.IsNullOrEmpty(_selectedRoiId)
                    && (_rois.Any(r => r.Id == _selectedRoiId && r.IsTaught)
                        || (_datumRoiCandidates != null
                            && _datumRoiCandidates.Any(r => r != null && r.Id == _selectedRoiId && r.IsTaught)));
                bool drawing = IsAnyDrawingModeActive();
                EditRoiMenuItem.IsEnabled = isImageLoaded && !drawing; //260503 hbk Phase 17 hotfix#5 — Edit 진입점 항상 보장
                EditRoiMenuItem.IsCheckable = true;
                EditRoiMenuItem.IsChecked = _isEditMode;
                DeleteRoiMenuItem.IsEnabled = hasSelectedRoi && !drawing; //260503 hbk Phase 17 hotfix#5 — Delete 는 선택 필요
            }

            //260505 hbk Phase 18 CO-04 — "ROI 다시 그리기" 메뉴: TeachDatum 모드 + 우클릭 위치에 Datum ROI 있을 때만 표시
            if (RedrawRoiMenuItem != null) //260509 hbk Phase 20 (Phase 18 CO-04)
            {
                //260509 hbk Phase 20 — ternaries expanded; Phase 18 CO-04 의도 보존
                RoiDefinition hitRoi;
                if (IsTeachDatumMode) hitRoi = HitTestRoiAtPoint(_lastMouseImagePoint);
                else                  hitRoi = null;
                bool isDatumRoi = hitRoi != null && hitRoi.Id != null && hitRoi.Id.StartsWith("Datum.");
                if (isDatumRoi) RedrawRoiMenuItem.Visibility = Visibility.Visible;
                else            RedrawRoiMenuItem.Visibility = Visibility.Collapsed;
            }
        }

        //260423 hbk Edit 모드 토글 헬퍼 (우클릭 종료 경로 + 메뉴 클릭 경로 공용)
        private void SetEditMode(bool enter)
        {
            if (_isEditMode == enter) return;
            _isEditMode = enter;
            //260509 hbk Phase 20 — nested ternary expanded
            if (enter) {
                SetPanCursor(Cursors.Cross);
            } else if (CanPanCurrentImage()) {
                SetPanCursor(Cursors.Hand);
            } else {
                SetPanCursor(Cursors.Arrow);
            }
            UpdateContextMenuState();
            Render();
            //260509 hbk Phase 20 — D-02 race-safe handler temp
            var roiEditModeChangedHandler = RoiEditModeChanged;
            if (roiEditModeChangedHandler != null) roiEditModeChangedHandler(this, enter);
        }

        //260423 hbk ContextMenu: Edit ROI 토글
        //260503 hbk Phase 17 hotfix#5 — _selectedRoiId 가드 제거. Edit 토글은 진입점이므로 ROI 선택 무관.
        //  ROI 가 없어도 Edit ON 으로 두면 사용자가 ROI body 좌클릭하면 hit-test → _selectedRoiId 갱신 → 이동/리사이즈.
        private void EditRoiMenuItem_Click(object sender, RoutedEventArgs e)
        {
            SetEditMode(!_isEditMode);
        }

        //260423 hbk ContextMenu: Delete ROI — 선택된 ROI 제거 요청 (FAI는 유지, ROI만 clear)
        private void DeleteRoiMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_selectedRoiId)) return;
            string targetId = _selectedRoiId;
            if (_isEditMode) SetEditMode(false);
            //260509 hbk Phase 20 — D-02 race-safe handler temp
            var roiDeleteRequestedHandler = RoiDeleteRequested;
            if (roiDeleteRequestedHandler != null) roiDeleteRequestedHandler(this, targetId);
        }

        //260505 hbk Phase 18 CO-04 — "ROI 다시 그리기" 클릭: hit-test 후 이벤트 발행 → MainView가 ClearDatumRoiFields 호출
        private void RedrawRoiMenuItem_Click(object sender, RoutedEventArgs e) //260509 hbk Phase 20 (Phase 18 CO-04)
        {
            var hitRoi = HitTestRoiAtPoint(_lastMouseImagePoint);
            if (hitRoi != null && hitRoi.Id != null && hitRoi.Id.StartsWith("Datum."))
            {
                //260509 hbk Phase 20 — D-02 race-safe handler temp
                var roiRedrawRequestedHandler = RoiRedrawRequested;
                if (roiRedrawRequestedHandler != null) roiRedrawRequestedHandler(hitRoi.Id);
            }
        }

        private IEnumerable<EdgeInspectionOverlay> BuildTransientOverlays()
        {
            if (CurrentImage == null)
            {
                return Enumerable.Empty<EdgeInspectionOverlay>();
            }

            var overlays = new List<EdgeInspectionOverlay>();
            if (_crosshairEnabled)
            {
                var part = GetImagePart();
                var crosshairPoint = GetViewportCenterPoint(part);
                var crosshairRow = crosshairPoint.Y;
                var crosshairColumn = crosshairPoint.X;
                overlays.Add(new EdgeInspectionOverlay
                {
                    RoiId = "Main-Crosshair-H",
                    LineRow1 = crosshairRow,
                    LineColumn1 = part.Left,
                    LineRow2 = crosshairRow,
                    LineColumn2 = part.Left + part.Width
                });
                overlays.Add(new EdgeInspectionOverlay
                {
                    RoiId = "Main-Crosshair-V",
                    LineRow1 = part.Top,
                    LineColumn1 = crosshairColumn,
                    LineRow2 = part.Top + part.Height,
                    LineColumn2 = crosshairColumn
                });
            }

            if (_manualMeasureStartPoint.HasValue)
            {
                overlays.Add(CreatePointOverlay("ManualMeasure-Start", _manualMeasureStartPoint.Value));
            }

            if (_manualMeasureEndPoint.HasValue)
            {
                overlays.Add(CreatePointOverlay("ManualMeasure-End", _manualMeasureEndPoint.Value));
                overlays.Add(new EdgeInspectionOverlay
                {
                    RoiId = "ManualMeasure-Line",
                    LineRow1 = _manualMeasureStartPoint.Value.Y,
                    LineColumn1 = _manualMeasureStartPoint.Value.X,
                    LineRow2 = _manualMeasureEndPoint.Value.Y,
                    LineColumn2 = _manualMeasureEndPoint.Value.X
                });
            }

            return overlays;
        }

        private IEnumerable<string> BuildTransientMessages()
        {
            if (!_manualToolsEnabled)
            {
                return new[] { "Manual Tools Locked" };
            }

            if (_manualMeasureMode && !_manualMeasureStartPoint.HasValue)
            {
                return new[] { "Manual Measure: Select first point" };
            }

            if (_manualMeasureMode && _manualMeasureStartPoint.HasValue && !_manualMeasureEndPoint.HasValue)
            {
                return new[] { "Manual Measure: Select second point" };
            }

            if (_manualMeasureStartPoint.HasValue && _manualMeasureEndPoint.HasValue)
            {
                var distance = GetDistance(_manualMeasureStartPoint.Value, _manualMeasureEndPoint.Value);
                return new[] { string.Format("Distance: {0:0.00} px", distance) };
            }

            return Enumerable.Empty<string>();
        }

        private static EdgeInspectionOverlay CreatePointOverlay(string roiId, Point point)
        {
            return new EdgeInspectionOverlay
            {
                RoiId = roiId,
                LineRow1 = point.Y,
                LineColumn1 = point.X,
                LineRow2 = point.Y,
                LineColumn2 = point.X,
                Points = new List<EdgeInspectionPoint>
                {
                    new EdgeInspectionPoint
                    {
                        Row = point.Y,
                        Column = point.X
                    }
                }
            };
        }

        private static double GetDistance(Point start, Point end)
        {
            var deltaX = end.X - start.X;
            var deltaY = end.Y - start.Y;
            return Math.Sqrt((deltaX * deltaX) + (deltaY * deltaY));
        }

        private struct MouseState
        {
            public MouseState(Point imagePoint, int buttons)
            {
                ImagePoint = imagePoint;
                Buttons = buttons;
            }

            public Point ImagePoint { get; }

            public int Buttons { get; }
        }

        private Rect CreateFitToWindowImagePart()
        {
            var viewerWidth = Math.Max(1.0, ViewerHost.ActualWidth);
            var viewerHeight = Math.Max(1.0, ViewerHost.ActualHeight);
            var viewerAspect = viewerWidth / viewerHeight;
            var imageAspect = _imageWidth / _imageHeight;

            var partWidth = _imageWidth;
            var partHeight = _imageHeight;
            if (imageAspect > viewerAspect)
            {
                partHeight = _imageWidth / viewerAspect;
            }
            else
            {
                partWidth = _imageHeight * viewerAspect;
            }

            return new Rect(
                (_imageWidth - partWidth) / 2.0,
                (_imageHeight - partHeight) / 2.0,
                partWidth,
                partHeight);
        }

        private Rect GetImagePart()
        {
            if (!_isWindowInitialized)
            {
                return ViewerHost.ImagePart;
            }

            HTuple row1;
            HTuple col1;
            HTuple row2;
            HTuple col2;
            ViewerHost.HalconWindow.GetPart(out row1, out col1, out row2, out col2);
            return new Rect(col1.D, row1.D, Math.Max(1.0, col2.D - col1.D + 1.0), Math.Max(1.0, row2.D - row1.D + 1.0));
        }

        private void SetPartInternal(Rect imagePart)
        {
            ViewerHost.ImagePart = imagePart;
            if (_isWindowInitialized)
            {
                ViewerHost.HalconWindow.SetPart(
                    (int)Math.Floor(imagePart.Top),
                    (int)Math.Floor(imagePart.Left),
                    (int)Math.Ceiling(imagePart.Top + imagePart.Height - 1.0),
                    (int)Math.Ceiling(imagePart.Left + imagePart.Width - 1.0));
            }
        }

        private void DisposeImage()
        {
            if (!HasImage)
            {
                return;
            }

            CurrentImage.Dispose();
            CurrentImage = null;
            CurrentImagePath = null;
            UpdateImageMetadata();
            ResetManualToolState();
            UpdateContextMenuState();
            SetPanCursor(Cursors.Arrow);
            Mouse.OverrideCursor = null;
            //260509 hbk Phase 20 — D-02 race-safe handler temp
            var pointerInfoResetHandler = PointerInfoChanged;
            if (pointerInfoResetHandler != null) pointerInfoResetHandler(this, new MainViewerPointerChangedEventArgs(0, 0, null));
        }

        private void OpenContextMenu()
        {
            UpdateContextMenuState();
            ViewerContextMenu.PlacementTarget = ViewerBorder;
            ViewerContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint;
            ViewerContextMenu.IsOpen = true;
        }

        private void CrosshairMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (!_manualToolsEnabled || CurrentImage == null)
            {
                return;
            }

            _crosshairEnabled = !_crosshairEnabled;
            UpdateContextMenuState();
            Render();
        }

        private Point GetImageCenterPoint()
        {
            if (CurrentImage == null)
            {
                return new Point(0, 0);
            }
            return new Point(_imageWidth / 2.0, _imageHeight / 2.0);
        }

        private static Point GetViewportCenterPoint(Rect imagePart)
        {
            return new Point(
                imagePart.Left + (imagePart.Width / 2.0),
                imagePart.Top + (imagePart.Height / 2.0));
        }

        private bool HasImage
        {
            get { return CurrentImage != null; }
        }

        private void UpdateImageMetadata()
        {
            if (!HasImage)
            {
                _imageWidth = 0;
                _imageHeight = 0;
                return;
            }

            HTuple imageWidth;
            HTuple imageHeight;
            CurrentImage.GetImageSize(out imageWidth, out imageHeight);
            _imageWidth = imageWidth.D;
            _imageHeight = imageHeight.D;
        }

        private void ResetManualToolState()
        {
            _crosshairEnabled = false;
            _manualMeasureMode = false;
            _manualMeasureStartPoint = null;
            _manualMeasureEndPoint = null;
        }
    }
}

