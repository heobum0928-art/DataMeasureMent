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

            //260423 hbk Phase 11 D-14 — Circle 드래그 미리보기 렌더
            if (_isDrawingCircle && _circleDraftRadius > 0)
            {
                _displayService.RenderCircleDraft(ViewerHost.HalconWindow, _circleDraftCenter.Y, _circleDraftCenter.X, _circleDraftRadius);
            }
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

