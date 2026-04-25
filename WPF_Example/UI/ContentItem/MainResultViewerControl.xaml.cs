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
        private bool _isEditMode;
        public bool IsEditMode { get { return _isEditMode; } }
        public event EventHandler<bool> RoiEditModeChanged;
        public event EventHandler<string> RoiDeleteRequested;

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
            CurrentImage = string.IsNullOrWhiteSpace(imagePath) ? null : new HImage(imagePath);
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

        //260423 hbk ROI hit-test: 선택된 ROI 내부 클릭인지 판정
        private RoiDefinition HitTestSelectedRoi(Point imagePoint)
        {
            if (string.IsNullOrEmpty(_selectedRoiId))
            {
                return null;
            }

            var roi = _rois.FirstOrDefault(r => r.Id == _selectedRoiId);
            if (roi == null)
            {
                return null;
            }

            if (roi.Shape == RoiShape.Circle)
            {
                double dr = imagePoint.Y - roi.CenterRow;
                double dc = imagePoint.X - roi.CenterCol;
                if (Math.Sqrt(dr * dr + dc * dc) <= roi.Radius)
                {
                    return roi;
                }
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
        private void RenderEditHandles()
        {
            if (!_isEditMode) return;
            var roi = _rois.FirstOrDefault(r => r.Id == _selectedRoiId && r.IsTaught);
            if (roi == null) return;
            var window = ViewerHost.HalconWindow;
            window.SetColor("cyan");
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
            _polygonDraftPoints = points != null ? new List<Point>(points) : null;
            _polygonColor = color ?? "blue";
            Render();
        }

        //260410 hbk Phase 4 gap fix: Datum overlay state
        private DatumConfig _datumConfig;
        private bool _datumSelected;

        //260424 hbk Phase 13 D-07 — 런타임 TryFindDatum 성공 시 주황 십자 렌더 대상 (SetDatumFindResultOverlay 로 주입)
        //  null 이 아니면 Render() 가 _displayService.RenderDatumFindResult 호출.
        //  teach 경로 (_datumConfig + _datumSelected) 와 독립 — 동시 표시 허용 (주황 십자 + 빨간 교점 십자 공존).
        private DatumConfig _datumFindResultOverlay;

        //260410 hbk Phase 4 gap fix: set Datum for overlay rendering
        public void SetDatumOverlay(DatumConfig datum, bool isSelected)
        {
            _datumConfig = datum;
            _datumSelected = isSelected;
            Render();
        }

        //260410 hbk Phase 4 gap fix: clear Datum overlay
        public void ClearDatumOverlay()
        {
            _datumConfig = null;
            _datumSelected = false;
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

        //260408 hbk Calibration 십자+라인 오버레이
        public void SetCalibrationOverlay(IList<Point> points)
        {
            _calibrationPoints = points != null ? new List<Point>(points) : null;
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

            _displayService.Render(
                ViewerHost.HalconWindow,
                CurrentImage,
                _rois,
                _selectedRoiId,
                _rectDraftRoi,
                _inspectionOverlays.Concat(BuildTransientOverlays()).ToList(),
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
            if (_datumConfig != null)
            {
                _displayService.RenderDatumOverlay(ViewerHost.HalconWindow, _datumConfig, _datumSelected);
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
            ZoomAtPointer(mouseState.ImagePoint.Y, mouseState.ImagePoint.X, e.Delta > 0 ? ZoomInScaleFactor : ZoomOutScaleFactor);
            PublishPointerInfo();
        }

        private void ViewerHost_HMouseDown(object sender, HMouseEventArgsWPF e)
        {
            var mouseState = GetMouseState();
            _lastMouseImagePoint = mouseState.ImagePoint;
            if ((mouseState.Buttons & HalconRightButton) == HalconRightButton)
            {
                //260423 hbk Edit 모드 중 우클릭 → 모드 종료, ContextMenu 미표시
                if (_isEditMode)
                {
                    SetEditMode(false);
                    PublishPointerInfo();
                    return;
                }
                //260408 hbk 우클릭 이벤트 브릿지 (Polygon 완성용)
                if (ImageRightClicked != null)
                {
                    ImageRightClicked?.Invoke(this, EventArgs.Empty);
                    PublishPointerInfo();
                    return;
                }
                OpenContextMenu();
                PublishPointerInfo();
                return;
            }

            if ((mouseState.Buttons & HalconLeftButton) != HalconLeftButton)
            {
                return;
            }

            //260423 hbk Edit 모드 전용: 핸들 히트 → 리사이즈 시작, 바디 히트 → 이동 시작
            if (_isEditMode && !IsAnyDrawingModeActive() && HasImage)
            {
                var selectedRoi = _rois.FirstOrDefault(r => r.Id == _selectedRoiId && r.IsTaught);
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

                var hitRoi = HitTestSelectedRoi(mouseState.ImagePoint);
                if (hitRoi != null)
                {
                    _isMovingRoi = true;
                    _moveStartImagePoint = mouseState.ImagePoint;
                    _movingRoiSnapshot = hitRoi.Clone();
                    SetPanCursor(Cursors.SizeAll);
                    PublishPointerInfo();
                    return;
                }
            }

            //260408 hbk Rect drawing mode — start drag
            if (_isDrawingRect && HasImage)
            {
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
            if (_isDrawingCircle && HasImage)
            {
                _circleDraftCenter = mouseState.ImagePoint;
                _circleDraftRadius = 0;
                PublishPointerInfo();
                return;
            }

            //260408 hbk 좌클릭 이벤트 브릿지 (Polygon 점 추가, Calibration 점 선택용)
            if (ImageLeftClicked != null && HasImage)
            {
                var pt = mouseState.ImagePoint;
                ImageLeftClicked?.Invoke(this, new MainViewerPointerChangedEventArgs(pt.X, pt.Y, null));
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
            if (_isDrawingCircle && HasImage)
            {
                double dx = mouseState.ImagePoint.X - _circleDraftCenter.X;
                double dy = mouseState.ImagePoint.Y - _circleDraftCenter.Y;
                _circleDraftRadius = Math.Sqrt(dx * dx + dy * dy);
                Render();
                PublishPointerInfo();
                return;
            }

            if (!_isPanningImage)
            {
                SetPanCursor(CanPanCurrentImage() ? Cursors.Hand : Cursors.Arrow);
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
            if (_isResizingRoi && _resizingRoiSnapshot != null)
            {
                var target = _rois.FirstOrDefault(r => r.Id == _resizingRoiSnapshot.Id);
                string movedId = _resizingRoiSnapshot.Id;
                RoiShape shape = _resizingRoiSnapshot.Shape;
                _isResizingRoi = false;
                _resizingRoiSnapshot = null;
                _resizingHandle = ResizeHandle.None;
                _resizingPolygonIndex = -1;
                SetPanCursor(CanPanCurrentImage() ? Cursors.Hand : Cursors.Arrow);
                if (target != null)
                {
                    RoiGeometryChanged?.Invoke(this, new RoiGeometryChangedArgs
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
                string movedId = _movingRoiSnapshot.Id;
                _isMovingRoi = false;
                _movingRoiSnapshot = null;
                SetPanCursor(CanPanCurrentImage() ? Cursors.Hand : Cursors.Arrow);
                if (Math.Abs(dr) > 0.5 || Math.Abs(dc) > 0.5)
                {
                    RoiMoveCompleted?.Invoke(this, new RoiMoveCompletedArgs
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
                RectDrawingCompleted?.Invoke(this, EventArgs.Empty);
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
                CircleDrawingCompleted?.Invoke(this, new CircleDrawCompletedArgs { CenterRow = cr, CenterCol = cc, Radius = rad });
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
            var rowRatio = current.Height <= 0 ? 0.5 : (row - current.Top) / current.Height;
            var columnRatio = current.Width <= 0 ? 0.5 : (column - current.Left) / current.Width;
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
            SetPanCursor(CanPanCurrentImage() ? Cursors.Hand : Cursors.Arrow);
            Render();
        }

        private void SetImagePartExact(Rect imagePart)
        {
            if (!_isWindowInitialized || !HasImage)
            {
                return;
            }

            SetPartInternal(imagePart);
            SetPanCursor(CanPanCurrentImage() ? Cursors.Hand : Cursors.Arrow);
            Render();
        }

        private void ApplyInitialFitView()
        {
            if (!_isWindowInitialized || !HasImage)
            {
                return;
            }

            SetImagePartExact(CreateFitToWindowImagePart());
            SetPanCursor(CanPanCurrentImage() ? Cursors.Hand : Cursors.Arrow);
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
            SetPanCursor(CanPanCurrentImage() ? Cursors.Hand : Cursors.Arrow);
            PublishPointerInfo();
        }

        private void ViewerHost_MouseEnter(object sender, MouseEventArgs e)
        {
            SetPanCursor(CanPanCurrentImage() ? Cursors.Hand : Cursors.Arrow);
        }

        private void ViewerHost_MouseLeave(object sender, MouseEventArgs e)
        {
            Mouse.OverrideCursor = null;
        }

        private void PublishPointerInfo()
        {
            if (CurrentImage == null)
            {
                PointerInfoChanged?.Invoke(this, new MainViewerPointerChangedEventArgs(0, 0, null));
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

            PointerInfoChanged?.Invoke(this, new MainViewerPointerChangedEventArgs(x, y, grayValue));
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
            if (EditRoiMenuItem != null && DeleteRoiMenuItem != null)
            {
                bool hasSelectedRoi = !string.IsNullOrEmpty(_selectedRoiId)
                    && _rois.Any(r => r.Id == _selectedRoiId && r.IsTaught);
                bool canEdit = hasSelectedRoi && !IsAnyDrawingModeActive();
                EditRoiMenuItem.IsEnabled = canEdit;
                EditRoiMenuItem.IsCheckable = true;
                EditRoiMenuItem.IsChecked = _isEditMode;
                DeleteRoiMenuItem.IsEnabled = canEdit;
            }
        }

        //260423 hbk Edit 모드 토글 헬퍼 (우클릭 종료 경로 + 메뉴 클릭 경로 공용)
        private void SetEditMode(bool enter)
        {
            if (_isEditMode == enter) return;
            _isEditMode = enter;
            SetPanCursor(enter ? Cursors.Cross : (CanPanCurrentImage() ? Cursors.Hand : Cursors.Arrow));
            UpdateContextMenuState();
            Render();
            RoiEditModeChanged?.Invoke(this, enter);
        }

        //260423 hbk ContextMenu: Edit ROI 토글
        private void EditRoiMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_selectedRoiId)) return;
            SetEditMode(!_isEditMode);
        }

        //260423 hbk ContextMenu: Delete ROI — 선택된 ROI 제거 요청 (FAI는 유지, ROI만 clear)
        private void DeleteRoiMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_selectedRoiId)) return;
            string targetId = _selectedRoiId;
            if (_isEditMode) SetEditMode(false);
            RoiDeleteRequested?.Invoke(this, targetId);
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
            PointerInfoChanged?.Invoke(this, new MainViewerPointerChangedEventArgs(0, 0, null));
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

