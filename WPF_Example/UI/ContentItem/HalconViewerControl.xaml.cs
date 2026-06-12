using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using ReringProject.Halcon.Display;
using ReringProject.Halcon.Models;
using HalconDotNet;
using ReringProject.Halcon;

namespace ReringProject.UI
{
    public partial class HalconViewerControl : UserControl, IDisposable
    {
        private const double ZoomInScaleFactor = 0.65;
        private const double ZoomOutScaleFactor = 1.55;
        private const int HalconLeftButton = 1;
        private const int HalconRightButton = 4;
        private const int PointerGrayThrottleMs = 40;
        private const int PanRenderThrottleMs = 16;

        private readonly HalconDisplayService _displayService = new HalconDisplayService();
        private readonly List<RoiDefinition> _rois = new List<RoiDefinition>();
        private readonly List<EdgeInspectionOverlay> _inspectionOverlays = new List<EdgeInspectionOverlay>();
        private readonly List<string> _displayMessages = new List<string>();
        private HTuple _activeDrawingHandle;
        private RoiDefinition _draftRoi;
        private bool _isDrawingRoi;
        private bool _isDraggingDraftRoi;
        private bool _isMovingDraftRoi;
        private bool _isResizingDraftRoi;
        private bool _isPanningImage;
        private bool _isWindowInitialized;
        private bool _isOneToOneMode;
        private bool _allowWheelZoomWhileDrawing;
        private double _dragStartRow;
        private double _dragStartColumn;
        private double _moveOffsetRow;
        private double _moveOffsetColumn;
        private double _moveHeight;
        private double _moveWidth;
        private double _resizeAnchorRow;
        private double _resizeAnchorColumn;
        private double _lastPointerRow;
        private double _lastPointerColumn;
        private double _mouseDownRow;
        private double _mouseDownColumn;
        private double _imageWidth;
        private double _imageHeight;
        private Point _panStartPoint;
        private Rect _panStartImagePart;
        private bool _renderPending;
        private string _pendingClickRoiId;
        private string _selectedRoiId;
        private int _lastPointerGrayTick;
        private int _lastPanRenderTick;

        public HalconViewerControl()
        {
            InitializeComponent();
            ViewerHost.ForceCursor = true;
            ViewerBorder.ForceCursor = true;
            ConfigureNavigationMode();
            ViewerHost.HInitWindow += ViewerHost_HInitWindow;
            ViewerHost.HMouseMove += ViewerHost_HMouseMove;
            ViewerHost.HMouseDown += ViewerHost_HMouseDown;
            ViewerHost.HMouseUp += ViewerHost_HMouseUp;
            ViewerHost.HMouseWheel += ViewerHost_HMouseWheel;
            ViewerHost.MouseRightButtonUp += ViewerHost_MouseRightButtonUp;
            ViewerHost.SizeChanged += ViewerHost_SizeChanged;
            ViewerHost.MouseEnter += ViewerHost_MouseEnter;
            ViewerHost.MouseLeave += ViewerHost_MouseLeave;
            ViewerBorder.MouseEnter += ViewerHost_MouseEnter;
            ViewerBorder.MouseLeave += ViewerHost_MouseLeave;
            Unloaded += HalconViewerControl_Unloaded;
        }

        public event EventHandler<ViewerPointerChangedEventArgs> PointerChanged;

        public event EventHandler ViewerRightClicked;

        public event EventHandler<RoiDefinition> RoiClicked;

        public event EventHandler<RoiDefinition> DraftRoiChanged;

        public HImage CurrentImage { get; private set; }

        public string CurrentImagePath { get; private set; }

        public IReadOnlyList<RoiDefinition> Rois
        {
            get { return _rois.AsReadOnly(); }
        }

        public bool AllowWheelZoomWhileDrawing
        {
            get { return _allowWheelZoomWhileDrawing; }
            set { _allowWheelZoomWhileDrawing = value; }
        }

        public bool EnableLeftDragPan { get; set; } = true;

        public bool EnableMiddleDragPan { get; set; }

        public bool EnableRoiSelection { get; set; } = true;

        // CO-33-02 hotfix: 이전 HImage 로드 후 캐시 hit 무효화 (CurrentImagePath="" 일 때 다른 path 와 잘못된 비교 차단)
        public void LoadImage(string imagePath)
        {
            bool cacheHit = HasImage
                            && !string.IsNullOrEmpty(CurrentImagePath)   // 빈 문자열(HImage 직접 로드 상태) 시 캐시 hit 차단
                            && string.Equals(CurrentImagePath, imagePath, StringComparison.OrdinalIgnoreCase);
            if (cacheHit)
            {
                ApplyInitialFitView();
                Render();
                return;
            }

            DisposeImage();
            // null 방지 (정규화: null=초기화 전, ""=HImage 로드, non-empty=path 로드)
            if (imagePath == null)
                CurrentImagePath = "";
            else
                CurrentImagePath = imagePath;
            if (string.IsNullOrWhiteSpace(imagePath))
                CurrentImage = null;
            else
                CurrentImage = new HImage(imagePath);
            UpdateImageMetadata();
            ApplyInitialFitView();
            Render();
        }

        // CO-33-02 hotfix: HImage 오버로드도 sourceContext 보존하여 캐시 일관성 확보 (default 인자 → 기존 호출 site 무수정 호환)
        public void LoadImage(HImage image, string sourceContext = null)
        {
            DisposeImage();
            // null/empty → "" 정규화 (HImage 직접 로드 상태 표현)
            if (sourceContext == null)
                CurrentImagePath = "";
            else
                CurrentImagePath = sourceContext;
            CurrentImage = HalconImageBridge.Clone(image);
            UpdateImageMetadata();
            ApplyInitialFitView();
            Render();
        }

        public void SetRois(IEnumerable<RoiDefinition> rois)
        {
            _rois.Clear();
            if (rois != null)
            {
                _rois.AddRange(rois.Select(roi => roi.Clone()));
            }

            Render();
        }

        public void SetSelectedRoi(string roiId)
        {
            _selectedRoiId = roiId;
            Render();
        }

        public void SetInspectionOverlays(IEnumerable<EdgeInspectionOverlay> overlays)
        {
            _inspectionOverlays.Clear();
            if (overlays != null)
            {
                _inspectionOverlays.AddRange(overlays.Select(overlay => overlay.Clone()));
            }

            Render();
        }

        public void SetDisplayMessages(IEnumerable<string> messages)
        {
            _displayMessages.Clear();
            if (messages != null)
            {
                _displayMessages.AddRange(messages.Where(message => !string.IsNullOrWhiteSpace(message)));
            }

            Render();
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

        public void StartRectangleDrawing(RoiDefinition seed = null)
        {
            CancelActiveDrawing();

            if (CurrentImage == null)
            {
                return;
            }

            ConfigureDrawingMode();
            var hasValidSeed =
                seed != null &&
                seed.IsTaught &&
                Math.Abs(seed.Row2 - seed.Row1) > 1.0 &&
                Math.Abs(seed.Column2 - seed.Column1) > 1.0;

            if (!hasValidSeed)
            {
                _draftRoi = CreateDefaultDraftRoi();
            }
            else
            {
                _draftRoi = new RoiDefinition
                {
                    Row1 = seed.Row1,
                    Column1 = seed.Column1,
                    Row2 = seed.Row2,
                    Column2 = seed.Column2
                };
            }
            CreateReferenceDrawingObject();
            RaiseDraftRoiChanged();
            Render();
        }

        public RoiDefinition CommitActiveRectangle(string roiId, string roiName, string teachingValue)
        {
            if (!HasActiveDrawingHandle())
            {
                if (_draftRoi == null)
                {
                    return null;
                }
            }

            var row1 = _draftRoi.Row1;
            var column1 = _draftRoi.Column1;
            var row2 = _draftRoi.Row2;
            var column2 = _draftRoi.Column2;

            if (HasActiveDrawingHandle())
            {
                HTuple values;
                HOperatorSet.GetDrawingObjectParams(_activeDrawingHandle, new HTuple(new[] { "row1", "column1", "row2", "column2" }), out values);
                row1 = values[0].D;
                column1 = values[1].D;
                row2 = values[2].D;
                column2 = values[3].D;
            }

            CancelActiveDrawing();

            var roi = new RoiDefinition
            {
                Id = roiId,
                Name = roiName,
                Row1 = Math.Min(row1, row2),
                Column1 = Math.Min(column1, column2),
                Row2 = Math.Max(row1, row2),
                Column2 = Math.Max(column1, column2),
                IsTaught = true,
                TeachingValue = teachingValue
            };

            UpsertRoi(roi);
            _selectedRoiId = roi.Id;
            Render();
            return roi;
        }

        public void CancelActiveDrawing()
        {
            DisposeActiveDrawingObject();
            _draftRoi = null;
            _isDrawingRoi = false;
            ClearDrawingActionFlags();
            ConfigureNavigationMode();
            RaiseDraftRoiChanged();
            Render();
        }

        public void FitImage()
        {
            if (!_isWindowInitialized)
            {
                return;
            }

            if (CurrentImage == null)
            {
                return;
            }

            _isOneToOneMode = false;
            SetImagePartExact(CreateFitToWindowImagePart());
        }

        public void Render()
        {
            if (!_isWindowInitialized)
            {
                return;
            }

            if (_renderPending)
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

            _displayService.Render(ViewerHost.HalconWindow, CurrentImage, _rois, _selectedRoiId, _draftRoi, _inspectionOverlays, _displayMessages);
            AttachActiveDrawingObject();
        }

        public void Dispose()
        {
            Unloaded -= HalconViewerControl_Unloaded;
            CancelActiveDrawing();
            DisposeImage();
        }

        private void HalconViewerControl_Unloaded(object sender, RoutedEventArgs e)
        {
            CancelActiveDrawing();
            DisposeImage();
            Mouse.OverrideCursor = null;
        }

        private void UpsertRoi(RoiDefinition roi)
        {
            var existing = _rois.FirstOrDefault(item => item.Id == roi.Id);
            if (existing != null)
            {
                _rois.Remove(existing);
            }

            _rois.Add(roi);
        }

        private void ViewerHost_HInitWindow(object sender, EventArgs e)
        {
            _isWindowInitialized = true;
            if (CurrentImage != null)
            {
                ApplyInitialFitView();
            }

            Render();
        }

        private void ViewerHost_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (!_isWindowInitialized || CurrentImage == null)
            {
                return;
            }

            if (_isOneToOneMode)
            {
                var current = GetImagePart();
                var centerRow = current.Top + (current.Height / 2.0);
                var centerColumn = current.Left + (current.Width / 2.0);
                SetImagePartExact(CreateOneToOneImagePart(centerRow, centerColumn));
                return;
            }

            SetImagePartExact(CreateFitToWindowImagePart());
        }

        private void ViewerHost_HMouseMove(object sender, HMouseEventArgsWPF e)
        {
            _lastPointerRow = e.Row;
            _lastPointerColumn = e.Column;

            if (_isPanningImage)
            {
                var mouseState = GetMouseState();
                var isStillDragging =
                    (EnableLeftDragPan && (mouseState.Buttons & HalconLeftButton) == HalconLeftButton) ||
                    (EnableMiddleDragPan && (mouseState.Buttons & 2) == 2);

                if (!isStillDragging)
                {
                    EndPanMode();
                    return;
                }

                UpdatePanPosition(new Point(e.X, e.Y));
                return;
            }

            if (_isDrawingRoi && _isDraggingDraftRoi)
            {
                UpdateDraftRoi(e.Row, e.Column);
                Render();
            }
            else if (_isDrawingRoi && _isMovingDraftRoi)
            {
                MoveDraftRoi(e.Row, e.Column);
                Render();
            }
            else if (_isDrawingRoi && _isResizingDraftRoi)
            {
                ResizeDraftRoi(e.Row, e.Column);
                Render();
            }

            if (_isDrawingRoi)
            {
                UpdateDrawingCursor(e.Row, e.Column);
            }
            else
            {
                UpdateNavigationCursor();
            }

            var grayValue = TryGetPointerGrayValue(e.Row, e.Column);

            if (PointerChanged != null)
                PointerChanged.Invoke(this, new ViewerPointerChangedEventArgs(e.Row, e.Column, grayValue));
        }

        private void ViewerHost_HMouseDown(object sender, HMouseEventArgsWPF e)
        {
            var mouseState = GetMouseState();
            _lastPointerRow = mouseState.ImagePoint.Y;
            _lastPointerColumn = mouseState.ImagePoint.X;

            if (_isDrawingRoi)
            {
                HandleDrawingMouseDown(e);
                return;
            }

            if ((mouseState.Buttons & HalconRightButton) == HalconRightButton)
            {
                ViewerContextMenu.PlacementTarget = ViewerBorder;
                ViewerContextMenu.Placement = PlacementMode.MousePoint;
                ViewerContextMenu.IsOpen = true;
                if (ViewerRightClicked != null)
                    ViewerRightClicked.Invoke(this, EventArgs.Empty);
                return;
            }

            _mouseDownRow = e.Row;
            _mouseDownColumn = e.Column;
            _pendingClickRoiId = null;

            var canStartPan =
                CanPanCurrentImage() &&
                ((EnableLeftDragPan && (mouseState.Buttons & HalconLeftButton) == HalconLeftButton)
                || (EnableMiddleDragPan && (mouseState.Buttons & 2) == 2));

            if (canStartPan)
            {
                BeginPanMode(new Point(e.X, e.Y));
            }

            if (!EnableRoiSelection)
            {
                return;
            }

            var clicked = _rois.FirstOrDefault(roi => roi.Contains(e.Row, e.Column));
            if (clicked == null)
            {
                return;
            }

            _pendingClickRoiId = clicked.Id;
        }

        private void ViewerHost_HMouseUp(object sender, HMouseEventArgsWPF e)
        {
            if (_isPanningImage)
            {
                EndPanMode();
            }

            if (EnableRoiSelection && !_isDrawingRoi && !string.IsNullOrWhiteSpace(_pendingClickRoiId))
            {
                var moved = Distance(_mouseDownRow, _mouseDownColumn, e.Row, e.Column);
                if (moved <= 3.0)
                {
                    var clicked = _rois.FirstOrDefault(roi => roi.Id == _pendingClickRoiId);
                    if (clicked != null)
                    {
                        _selectedRoiId = clicked.Id;
                        Render();
                        if (RoiClicked != null)
                            RoiClicked.Invoke(this, clicked.Clone());
                    }
                }

                _pendingClickRoiId = null;
            }

            if (!_isDrawingRoi || (!_isDraggingDraftRoi && !_isMovingDraftRoi && !_isResizingDraftRoi))
            {
                return;
            }

            if (_isDraggingDraftRoi)
            {
                UpdateDraftRoi(e.Row, e.Column, true);
            }
            else
            {
                if (_isMovingDraftRoi)
                {
                    MoveDraftRoi(e.Row, e.Column);
                }
                else
                {
                    ResizeDraftRoi(e.Row, e.Column, true);
                }
            }

            ClearDrawingActionFlags();
            CreateReferenceDrawingObject();
            Render();
            UpdateDrawingCursor(e.Row, e.Column);
        }

        private void ViewerHost_HMouseWheel(object sender, HMouseEventArgsWPF e)
        {
            if (CurrentImage == null)
            {
                return;
            }

            if (_isDrawingRoi && (_isDraggingDraftRoi || _isMovingDraftRoi || _isResizingDraftRoi))
            {
                return;
            }

            if (_isDrawingRoi && !_allowWheelZoomWhileDrawing)
            {
                return;
            }

            _lastPointerRow = e.Row;
            _lastPointerColumn = e.Column;
            if (e.Delta > 0)
                ZoomAtPointer(ZoomInScaleFactor);
            else
                ZoomAtPointer(ZoomOutScaleFactor);
            UpdateNavigationCursor();
        }

        private void ViewerHost_MouseRightButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            ViewerContextMenu.PlacementTarget = ViewerBorder;
            ViewerContextMenu.Placement = PlacementMode.MousePoint;
            ViewerContextMenu.IsOpen = true;
            if (ViewerRightClicked != null)
                ViewerRightClicked.Invoke(this, EventArgs.Empty);
            e.Handled = true;
        }

        private void ZoomInMenuItem_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            ZoomAtPointer(ZoomInScaleFactor);
        }

        private void ZoomOutMenuItem_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            ZoomAtPointer(ZoomOutScaleFactor);
        }

        private void FitImageMenuItem_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            FitImage();
        }

        private void DisposeImage()
        {
            if (CurrentImage == null)
            {
                return;
            }

            CurrentImage.Dispose();
            CurrentImage = null;
            // CO-33-02 hotfix: null 대신 "" 사용 (정규화 정책: null=초기화 전, ""=HImage 로드/Dispose 후, non-empty=path)
            CurrentImagePath = "";
            UpdateImageMetadata();
        }

        private void ConfigureNavigationMode()
        {
            _isDrawingRoi = false;
            UpdateNavigationCursor();
        }

        private void ConfigureDrawingMode()
        {
            _isDrawingRoi = true;
            ViewerHost.Cursor = Cursors.Hand;
        }

        private void ZoomAtPointer(double scaleFactor)
        {
            if (CurrentImage == null)
            {
                return;
            }

            var imagePart = GetImagePart();
            if (imagePart.Width <= 0 || imagePart.Height <= 0)
            {
                return;
            }

            var zoomCenter = GetValidZoomCenter();
            var newWidth = imagePart.Width * scaleFactor;
            var newHeight = imagePart.Height * scaleFactor;
            var newLeft = zoomCenter.Item2 - ((zoomCenter.Item2 - imagePart.Left) * scaleFactor);
            var newTop = zoomCenter.Item1 - ((zoomCenter.Item1 - imagePart.Top) * scaleFactor);

            SetImagePart(new Rect(newLeft, newTop, newWidth, newHeight));
        }

        private Tuple<double, double> GetValidZoomCenter()
        {
            if (!HasImage)
            {
                return Tuple.Create(0d, 0d);
            }

            double centerRow;
            if (_lastPointerRow > 0)
                centerRow = _lastPointerRow;
            else
                centerRow = _imageHeight / 2.0;
            double centerColumn;
            if (_lastPointerColumn > 0)
                centerColumn = _lastPointerColumn;
            else
                centerColumn = _imageWidth / 2.0;

            return Tuple.Create(centerRow, centerColumn);
        }

        private void UpdateDraftRoi(double endRow, double endColumn, bool enforceMinimumSize = false)
        {
            var row1 = Math.Min(_dragStartRow, endRow);
            var column1 = Math.Min(_dragStartColumn, endColumn);
            var row2 = Math.Max(_dragStartRow, endRow);
            var column2 = Math.Max(_dragStartColumn, endColumn);

            if (enforceMinimumSize && System.Math.Abs(row2 - row1) < 1.0)
            {
                row2 = row1 + 20.0;
            }

            if (enforceMinimumSize && System.Math.Abs(column2 - column1) < 1.0)
            {
                column2 = column1 + 20.0;
            }

            _draftRoi = new RoiDefinition
            {
                Row1 = row1,
                Column1 = column1,
                Row2 = row2,
                Column2 = column2
            };
            RaiseDraftRoiChanged();
        }

        private void CreateReferenceDrawingObject()
        {
            DisposeActiveDrawingObject();

            if (_draftRoi == null || !_isWindowInitialized)
            {
                return;
            }

            HOperatorSet.CreateDrawingObjectRectangle1(
                _draftRoi.Row1,
                _draftRoi.Column1,
                _draftRoi.Row2,
                _draftRoi.Column2,
                out _activeDrawingHandle);
            HOperatorSet.SetDrawingObjectParams(_activeDrawingHandle, "color", "red");
            HOperatorSet.SetDrawingObjectParams(_activeDrawingHandle, "line_width", 3.0);
            AttachActiveDrawingObject();
        }

        private void MoveDraftRoi(double row, double column)
        {
            if (_draftRoi == null || !HasImage)
            {
                return;
            }

            var top = row - _moveOffsetRow;
            var left = column - _moveOffsetColumn;
            var maxTop = Math.Max(0.0, _imageHeight - _moveHeight);
            var maxLeft = Math.Max(0.0, _imageWidth - _moveWidth);
            var normalizedTop = Math.Max(0.0, Math.Min(maxTop, top));
            var normalizedLeft = Math.Max(0.0, Math.Min(maxLeft, left));

            _draftRoi = new RoiDefinition
            {
                Row1 = normalizedTop,
                Column1 = normalizedLeft,
                Row2 = normalizedTop + _moveHeight,
                Column2 = normalizedLeft + _moveWidth
            };

            RaiseDraftRoiChanged();
        }

        private void ResizeDraftRoi(double row, double column, bool enforceMinimumSize = false)
        {
            if (_draftRoi == null)
            {
                return;
            }

            var newRow1 = Math.Min(_resizeAnchorRow, row);
            var newColumn1 = Math.Min(_resizeAnchorColumn, column);
            var newRow2 = Math.Max(_resizeAnchorRow, row);
            var newColumn2 = Math.Max(_resizeAnchorColumn, column);

            const double minSize = 5.0;
            if (enforceMinimumSize && Math.Abs(newRow2 - newRow1) < minSize)
            {
                newRow2 = newRow1 + minSize;
            }

            if (enforceMinimumSize && Math.Abs(newColumn2 - newColumn1) < minSize)
            {
                newColumn2 = newColumn1 + minSize;
            }

            _draftRoi = new RoiDefinition
            {
                Row1 = newRow1,
                Column1 = newColumn1,
                Row2 = newRow2,
                Column2 = newColumn2
            };

            RaiseDraftRoiChanged();
        }

        private void DisposeActiveDrawingObject()
        {
            if (!HasActiveDrawingHandle())
            {
                return;
            }

            try
            {
                if (_isWindowInitialized)
                {
                    ViewerHost.HalconWindow.DetachDrawingObjectFromWindow(new HDrawingObject(_activeDrawingHandle));
                }
            }
            catch
            {
            }

            try
            {
                HOperatorSet.ClearDrawingObject(_activeDrawingHandle);
            }
            catch
            {
            }

            _activeDrawingHandle = null;
        }

        private bool CanPanCurrentImage()
        {
            if (!HasImage)
            {
                return false;
            }

            var imagePart = GetImagePart();
            return imagePart.Width < _imageWidth || imagePart.Height < _imageHeight;
        }

        private void SetImagePart(Rect imagePart, bool refresh = true)
        {
            if (!_isWindowInitialized || !HasImage)
            {
                return;
            }

            var normalizedWidth = Math.Max(20.0, imagePart.Width);
            var normalizedHeight = Math.Max(20.0, imagePart.Height);

            // Keep display scale identical on X/Y axes by matching the visible image part
            // to the current viewer aspect ratio.
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
            UpdateNavigationCursor();
            if (refresh)
            {
                Render();
            }
        }

        private void SetImagePartExact(Rect imagePart)
        {
            if (!_isWindowInitialized || !HasImage)
            {
                return;
            }

            SetPartInternal(imagePart);
            UpdateNavigationCursor();
            Render();
        }

        private Rect CreateOneToOneImagePart(double? centerRow = null, double? centerColumn = null)
        {
            var viewPixelWidth = GetViewerPixelWidth();
            var viewPixelHeight = GetViewerPixelHeight();
            if (viewPixelWidth <= 0 || viewPixelHeight <= 0)
            {
                return new Rect(0, 0, _imageWidth, _imageHeight);
            }

            var targetWidth = viewPixelWidth;
            var targetHeight = viewPixelHeight;
            double targetCenterRow;
            if (centerRow == null)
                targetCenterRow = (_imageHeight - 1.0) / 2.0;
            else
                targetCenterRow = centerRow.Value;
            double targetCenterColumn;
            if (centerColumn == null)
                targetCenterColumn = (_imageWidth - 1.0) / 2.0;
            else
                targetCenterColumn = centerColumn.Value;

            var left = targetCenterColumn - (targetWidth / 2.0);
            var top = targetCenterRow - (targetHeight / 2.0);
            return new Rect(left, top, targetWidth, targetHeight);
        }

        private void ApplyInitialFitView()
        {
            if (!_isWindowInitialized || CurrentImage == null)
            {
                return;
            }

            _isOneToOneMode = false;
            SetImagePartExact(CreateFitToWindowImagePart());
        }

        private Rect CreateFitToWindowImagePart()
        {
            if (ViewerHost.ActualWidth <= 0 || ViewerHost.ActualHeight <= 0)
            {
                return new Rect(0, 0, _imageWidth, _imageHeight);
            }

            var viewerAspect = ViewerHost.ActualWidth / ViewerHost.ActualHeight;
            var imageAspect = _imageWidth / _imageHeight;

            double partWidth;
            double partHeight;
            if (imageAspect > viewerAspect)
            {
                partWidth = _imageWidth;
                partHeight = partWidth / viewerAspect;
            }
            else
            {
                partHeight = _imageHeight;
                partWidth = partHeight * viewerAspect;
            }

            var partLeft = (_imageWidth - partWidth) / 2.0;
            var partTop = (_imageHeight - partHeight) / 2.0;
            return new Rect(partLeft, partTop, partWidth, partHeight);
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
            return new Rect(
                col1.D,
                row1.D,
                Math.Max(1.0, col2.D - col1.D + 1.0),
                Math.Max(1.0, row2.D - row1.D + 1.0));
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

        private double GetViewerPixelWidth()
        {
            var source = PresentationSource.FromVisual(ViewerHost);
            if (source == null || source.CompositionTarget == null)
            {
                return Math.Max(1.0, ViewerHost.ActualWidth);
            }

            var toDevice = source.CompositionTarget.TransformToDevice;
            return Math.Max(1.0, ViewerHost.ActualWidth * toDevice.M11);
        }

        private double GetViewerPixelHeight()
        {
            var source = PresentationSource.FromVisual(ViewerHost);
            if (source == null || source.CompositionTarget == null)
            {
                return Math.Max(1.0, ViewerHost.ActualHeight);
            }

            var toDevice = source.CompositionTarget.TransformToDevice;
            return Math.Max(1.0, ViewerHost.ActualHeight * toDevice.M22);
        }

        private RoiDefinition CreateDefaultDraftRoi()
        {
            double centerRow;
            if (_lastPointerRow > 0)
                centerRow = _lastPointerRow;
            else
                centerRow = _imageHeight / 2.0;
            double centerColumn;
            if (_lastPointerColumn > 0)
                centerColumn = _lastPointerColumn;
            else
                centerColumn = _imageWidth / 2.0;
            const double halfSize = 60.0;

            return new RoiDefinition
            {
                Row1 = Math.Max(0.0, centerRow - halfSize),
                Column1 = Math.Max(0.0, centerColumn - halfSize),
                Row2 = Math.Min(_imageHeight - 1.0, centerRow + halfSize),
                Column2 = Math.Min(_imageWidth - 1.0, centerColumn + halfSize)
            };
        }

        private void RaiseDraftRoiChanged()
        {
            if (DraftRoiChanged != null)
            {
                RoiDefinition draftArg;
                if (_draftRoi == null)
                    draftArg = null;
                else
                    draftArg = _draftRoi.Clone();
                DraftRoiChanged.Invoke(this, draftArg);
            }
        }

        private void AttachActiveDrawingObject()
        {
            if (!_isWindowInitialized || !HasActiveDrawingHandle())
            {
                return;
            }

            try
            {
                ViewerHost.HalconWindow.AttachDrawingObjectToWindow(new HDrawingObject(_activeDrawingHandle));
            }
            catch
            {
            }
        }

        private bool HasActiveDrawingHandle()
        {
            return _activeDrawingHandle != null && _activeDrawingHandle.Length > 0;
        }

        private void UpdateDrawingCursor(double row, double column)
        {
            if (!_isDrawingRoi)
            {
                return;
            }

            var hitType = GetDraftRoiHitType(row, column);
            if (hitType == DraftRoiHitType.TopLeft || hitType == DraftRoiHitType.BottomRight)
            {
                ViewerHost.Cursor = Cursors.SizeNWSE;
                return;
            }

            if (hitType == DraftRoiHitType.TopRight || hitType == DraftRoiHitType.BottomLeft)
            {
                ViewerHost.Cursor = Cursors.SizeNESW;
                return;
            }

            if (hitType == DraftRoiHitType.Inside)
                ViewerHost.Cursor = Cursors.Cross;
            else
                ViewerHost.Cursor = Cursors.Hand;
        }

        private void ViewerHost_MouseEnter(object sender, MouseEventArgs e)
        {
            if (_isDrawingRoi)
            {
                return;
            }

            UpdateNavigationCursor();
        }

        private void ViewerHost_MouseLeave(object sender, MouseEventArgs e)
        {
            if (_isDrawingRoi)
            {
                return;
            }

            Mouse.OverrideCursor = null;
        }

        private void UpdateNavigationCursor()
        {
            if (_isDrawingRoi)
            {
                return;
            }

            if (_isPanningImage)
            {
                SetViewerCursor(Cursors.Hand);
                return;
            }

            if (CanPanCurrentImage())
                SetViewerCursor(Cursors.Hand);
            else
                SetViewerCursor(Cursors.Arrow);
        }

        private void SetViewerCursor(Cursor cursor)
        {
            Cursor = cursor;
            ViewerHost.Cursor = cursor;
            ViewerBorder.Cursor = cursor;

            if (_isPanningImage || ViewerHost.IsMouseOver || ViewerBorder.IsMouseOver || IsMouseOver)
            {
                Mouse.OverrideCursor = cursor;
            }
        }

        private void HandleDrawingMouseDown(HMouseEventArgsWPF e)
        {
            if (!e.Button.HasValue || e.Button.Value != MouseButton.Left)
            {
                return;
            }

            DisposeActiveDrawingObject();
            ClearDrawingActionFlags();

            var hitType = GetDraftRoiHitType(e.Row, e.Column);
            if (IsCornerHit(hitType))
            {
                _isResizingDraftRoi = true;
                SetResizeAnchor(hitType);
            }
            else if (hitType == DraftRoiHitType.Inside)
            {
                _isMovingDraftRoi = true;
                _moveOffsetRow = e.Row - _draftRoi.Row1;
                _moveOffsetColumn = e.Column - _draftRoi.Column1;
                _moveHeight = _draftRoi.Row2 - _draftRoi.Row1;
                _moveWidth = _draftRoi.Column2 - _draftRoi.Column1;
            }
            else
            {
                _isDraggingDraftRoi = true;
                _dragStartRow = e.Row;
                _dragStartColumn = e.Column;
                _draftRoi = new RoiDefinition
                {
                    Row1 = e.Row,
                    Column1 = e.Column,
                    Row2 = e.Row,
                    Column2 = e.Column
                };
                RaiseDraftRoiChanged();
            }

            Render();
        }

        private DraftRoiHitType GetDraftRoiHitType(double row, double column)
        {
            if (_draftRoi == null)
            {
                return DraftRoiHitType.None;
            }

            const double cornerHit = 10.0;
            if (Distance(row, column, _draftRoi.Row1, _draftRoi.Column1) <= cornerHit)
            {
                return DraftRoiHitType.TopLeft;
            }

            if (Distance(row, column, _draftRoi.Row1, _draftRoi.Column2) <= cornerHit)
            {
                return DraftRoiHitType.TopRight;
            }

            if (Distance(row, column, _draftRoi.Row2, _draftRoi.Column1) <= cornerHit)
            {
                return DraftRoiHitType.BottomLeft;
            }

            if (Distance(row, column, _draftRoi.Row2, _draftRoi.Column2) <= cornerHit)
            {
                return DraftRoiHitType.BottomRight;
            }

            if (_draftRoi.Contains(row, column))
                return DraftRoiHitType.Inside;
            return DraftRoiHitType.None;
        }

        private void SetResizeAnchor(DraftRoiHitType hitType)
        {
            switch (hitType)
            {
                case DraftRoiHitType.TopLeft:
                    _resizeAnchorRow = _draftRoi.Row2;
                    _resizeAnchorColumn = _draftRoi.Column2;
                    break;
                case DraftRoiHitType.TopRight:
                    _resizeAnchorRow = _draftRoi.Row2;
                    _resizeAnchorColumn = _draftRoi.Column1;
                    break;
                case DraftRoiHitType.BottomLeft:
                    _resizeAnchorRow = _draftRoi.Row1;
                    _resizeAnchorColumn = _draftRoi.Column2;
                    break;
                case DraftRoiHitType.BottomRight:
                    _resizeAnchorRow = _draftRoi.Row1;
                    _resizeAnchorColumn = _draftRoi.Column1;
                    break;
                default:
                    _resizeAnchorRow = _draftRoi.Row1;
                    _resizeAnchorColumn = _draftRoi.Column1;
                    break;
            }
        }

        private static bool IsCornerHit(DraftRoiHitType hitType)
        {
            return hitType == DraftRoiHitType.TopLeft
                   || hitType == DraftRoiHitType.TopRight
                   || hitType == DraftRoiHitType.BottomLeft
                   || hitType == DraftRoiHitType.BottomRight;
        }

        private void BeginPanMode(Point startPoint)
        {
            _isPanningImage = true;
            _panStartPoint = startPoint;
            _panStartImagePart = GetImagePart();
            ViewerHost.Focus();
            SetViewerCursor(Cursors.Hand);
        }

        private void UpdatePanPosition(Point currentPoint)
        {
            if (!_isPanningImage || CurrentImage == null)
            {
                return;
            }

            if (ViewerHost.ActualWidth <= 0 || ViewerHost.ActualHeight <= 0)
            {
                return;
            }

            var deltaX = currentPoint.X - _panStartPoint.X;
            var deltaY = currentPoint.Y - _panStartPoint.Y;
            var imageDeltaColumn = deltaX * (_panStartImagePart.Width / ViewerHost.ActualWidth);
            var imageDeltaRow = deltaY * (_panStartImagePart.Height / ViewerHost.ActualHeight);

            var targetPart = new Rect(
                _panStartImagePart.Left - imageDeltaColumn,
                _panStartImagePart.Top - imageDeltaRow,
                _panStartImagePart.Width,
                _panStartImagePart.Height);

            var tick = Environment.TickCount;
            if ((tick - _lastPanRenderTick) < PanRenderThrottleMs)
            {
                SetImagePart(targetPart, false);
                return;
            }

            _lastPanRenderTick = tick;
            SetImagePart(targetPart);
        }

        private void EndPanMode()
        {
            _isPanningImage = false;
            _lastPanRenderTick = 0;
            UpdateNavigationCursor();
            Render();
        }

        private void ClearDrawingActionFlags()
        {
            _isDraggingDraftRoi = false;
            _isMovingDraftRoi = false;
            _isResizingDraftRoi = false;
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

        private MouseState GetMouseState()
        {
            if (!_isWindowInitialized)
            {
                return new MouseState(new Point(_lastPointerColumn, _lastPointerRow), 0);
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
                return new MouseState(new Point(_lastPointerColumn, _lastPointerRow), 0);
            }
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

        private static double Distance(double row1, double col1, double row2, double col2)
        {
            var dr = row1 - row2;
            var dc = col1 - col2;
            return Math.Sqrt((dr * dr) + (dc * dc));
        }

        private double? TryGetPointerGrayValue(double row, double column)
        {
            if (CurrentImage == null || row < 0 || column < 0)
            {
                return null;
            }

            var tick = Environment.TickCount;
            if ((tick - _lastPointerGrayTick) < PointerGrayThrottleMs)
            {
                return null;
            }

            _lastPointerGrayTick = tick;

            try
            {
                return CurrentImage.GetGrayval((int)row, (int)column)[0].D;
            }
            catch
            {
                return null;
            }
        }
    }

    internal enum DraftRoiHitType
    {
        None = 0,
        Inside,
        TopLeft,
        TopRight,
        BottomLeft,
        BottomRight
    }

    public class ViewerPointerChangedEventArgs : EventArgs
    {
        public ViewerPointerChangedEventArgs(double row, double column, double? grayValue)
        {
            Row = row;
            Column = column;
            GrayValue = grayValue;
        }

        public double Row { get; private set; }

        public double Column { get; private set; }

        public double? GrayValue { get; private set; }
    }
}




