# Phase 11: datum-teaching-ui-roi — Pattern Map

**Mapped:** 2026-04-23
**Files analyzed:** 12 (10 MODIFY, 0 CREATE-NEW; 2 touched only as copywriting callers)
**Analogs found:** 12 / 12

---

## File Classification

| File (to modify) | Role | Data Flow | Closest Analog | Match Quality | New/Modified |
|---|---|---|---|---|---|
| `WPF_Example/UI/ContentItem/MainView.xaml` | view (XAML) | request-response (button click) | same file, `btn_rectRoi`/`btn_polygonRoi`/`btn_calibrate` block (L70-101) | exact (same file) | MODIFY-EXTEND |
| `WPF_Example/UI/ContentItem/MainView.xaml.cs` | view code-behind | event-driven (canvas mode state machine) | same file, `RectRoiButton_Click` (L520-579), `CalibrateButton_Click` (L655-731) | exact (same file) | MODIFY-EXTEND |
| `WPF_Example/UI/ContentItem/MainResultViewerControl.xaml.cs` | control (WPF UserControl) | event-driven (mouse → Halcon render) | same file, `StartRectangleDrawing`+`CommitActiveRectangle` (L194-211), `RectDrawingCompleted` event (L63, L497-502) | exact (same file) | MODIFY-EXTEND |
| `WPF_Example/UI/ControlItem/InspectionListView.xaml` | view (XAML) | request-response | same file, toolbar block L182-250 + HierarchicalDataTemplate L43-53 | exact (same file) | MODIFY-EXTEND |
| `WPF_Example/UI/ControlItem/InspectionListView.xaml.cs` | view code-behind | event-driven (selection → UI state) | same file, `InspectionList_SelectionChanged` (L239-324), `button_grab_Click` (L360-372) | exact (same file) | MODIFY-EXTEND |
| `WPF_Example/UI/ControlItem/NodeViewModel.cs` | viewmodel | property-binding (INotifyPropertyChanged via `Observable`) | same file, existing scalar bindings (`ImageSource` L104-107, `Param` L119-125) | exact | MODIFY-EXTEND |
| `WPF_Example/Halcon/Models/RoiDefinition.cs` | model (DataContract) | serialization (JSON + INI pass-through) | same file, existing `[DataMember]` fields L14-65 | exact | MODIFY-EXTEND |
| `WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs` | model (ParamBase) | serialization (INI, ParamBase reflection) | same file, existing `[Category]` blocks L20-67 | exact | MODIFY-EXTEND |
| `WPF_Example/Custom/Sequence/Inspection/Measurements/CircleDiameterMeasurement.cs` | model (MeasurementBase) | serialization + runtime | same file (verify + optional `IHalconTeachingProvider`); interface analog: `TopCalibrationParam : IHalconTeachingProvider` (Action_TopCalibration.cs L49-95) | role-match | MODIFY-EXTEND (optional) |
| `WPF_Example/Halcon/Algorithms/DatumFindingService.cs` | service (algorithm) | transform (HImage → HTuple + side-effect on config) | same file, existing `TryTeachDatum` (L122-201) and `TryFindLine` (L207-277) | exact | MODIFY-EXTEND |
| `WPF_Example/Halcon/Display/HalconDisplayService.cs` | service (rendering) | transform (models → HWindow draw ops) | same file, Rect switch L57-66, Polygon L50-56, `RenderDatumOverlay` L312-367 | exact | MODIFY-EXTEND |
| `WPF_Example/MainWindow.xaml` (status bar) + `WPF_Example/UI/StatusBarModel.cs` | viewmodel (status bar text channel) | event-driven | `StatusBarModel.SetText` (StatusBarModel.cs L112-114), `statusBar.Model.SetText` callers (MainWindow.xaml.cs L140-184, InspectionListView.xaml.cs L329) | exact | REUSE (no modification needed) |

No CREATE-NEW files. All additions are extensions to existing files.

---

## Pattern Assignments

### 1. `MainView.xaml` — toolbar button additions (D-01, D-15, D-23)

**Analog:** same file, `btn_rectRoi` (L70-74), `btn_polygonRoi` (L76-80), `btn_calibrate` (L85-101).

**Copy this for `btn_circleRoi` and `btn_teachDatum` (ToggleButton pattern, L70-80):**
```xml
<ToggleButton x:Name="btn_rectRoi" Content="Rect ROI"
    Click="RectRoiButton_Click"
    Style="{StaticResource RoiToggleButtonStyle}"
    MinWidth="80" Height="28" Padding="12,4"
    FontSize="13" FontWeight="SemiBold" Margin="0,0,4,0"/>
```
- `btn_circleRoi`: identical shape (`MinWidth=80`).
- `btn_teachDatum`: same but `MinWidth=100` (UI-SPEC — "Teach Datum" is 11 chars).

**Copy this for `btn_testFai` (Button pattern, L85-101):**
```xml
<Button x:Name="btn_calibrate" Content="Calibrate"
    Click="CalibrateButton_Click"
    MinWidth="80" Height="28" Padding="12,4"
    FontSize="13" FontWeight="SemiBold"
    Background="#2563EB" Foreground="White"
    BorderBrush="#1D4ED8" BorderThickness="1">
    <Button.Template>
        <ControlTemplate TargetType="Button">
            <Border Background="{TemplateBinding Background}" ... CornerRadius="4" ...>
                <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center"/>
            </Border>
        </ControlTemplate>
    </Button.Template>
</Button>
```
- `btn_testFai`: override `Background="Transparent"`, `Foreground="#FFAAAAAA"`, `BorderBrush="#FF555555"` per UI-SPEC (not accent — Calibrate owns accent). Use `IsEnabled="False"` as initial state.

**Separator pattern (L82-83):**
```xml
<Separator Style="{StaticResource {x:Static ToolBar.SeparatorStyleKey}}" Margin="4,4"/>
```
Drop one between `btn_polygonRoi`+`btn_circleRoi` group and `btn_teachDatum`, and another between `btn_teachDatum` and `btn_calibrate`+`btn_testFai`.

---

### 2. `MainView.xaml.cs` — ECanvasMode extension + button click handlers

**Analog:** same file. Enum at L41, state fields L42-46, `ExitCanvasMode()` L495-516, `RectRoiButton_Click` L520-543, `HalconViewer_RectDrawingCompleted` L546-549, `CommitRectRoi` L551-579, `CalibrateButton_Click` L655-665.

**ECanvasMode extension (L41):**
```csharp
//260408 hbk Drawing mode state (ROI 편집 + 캘리브레이션)
private enum ECanvasMode { None, RectRoi, PolygonRoi, Calibration }
private ECanvasMode _canvasMode = ECanvasMode.None;
```
Extend to `{ None, RectRoi, PolygonRoi, CircleRoi, TeachDatum, Calibration }`. Also add nested `private enum EDatumTeachStep { Line1, Line2, Done }` plus `_datumTeachStep` and `_editingDatum` fields beside `_editingFai` (L43).

**Rect click flow (copy for Circle button) — L520-543:**
```csharp
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
        halconViewer.RectDrawingCompleted += HalconViewer_RectDrawingCompleted;
        halconViewer.StartRectangleDrawing();
    }
    else {
        CommitRectRoi();
    }
}
```
Mirror for `CircleRoiButton_Click`: subscribe a new `halconViewer.CircleDrawingCompleted`, call new `halconViewer.StartCircleDrawing()`, and commit via `CommitCircleRoi()` that writes to the selected Measurement's `Circle_Row/Col/Radius` (D-17) AND the FAI `RoiDefinition.Shape/Center*/Radius` (D-16).

**Commit pattern (L551-579) — copy for `CommitCircleRoi`:**
```csharp
private void CommitRectRoi() {
    if (_canvasMode != ECanvasMode.RectRoi || _editingFai == null) { ExitCanvasMode(); return; }

    var roi = halconViewer.CommitActiveRectangle();
    if (roi != null) {
        double centerRow = (roi.Row1 + roi.Row2) / 2.0;
        double centerCol = (roi.Column1 + roi.Column2) / 2.0;
        double halfHeight = (roi.Row2 - roi.Row1) / 2.0;
        double halfWidth  = (roi.Column2 - roi.Column1) / 2.0;

        _editingFai.ROI_Row = centerRow;
        _editingFai.ROI_Col = centerCol;
        _editingFai.ROI_Phi = 0.0;
        _editingFai.ROI_Length1 = halfHeight;
        _editingFai.ROI_Length2 = halfWidth;

        var rois = GetCurrentFAIRois();
        halconViewer.UpdateDisplayState(rois, _editingFai.FAIName, null, null);
    }
    ExitCanvasMode();
}
```
For `CommitCircleRoi`: receive `(centerRow, centerCol, radius)` from `CircleDrawingCompleted` event args, write to the selected `CircleDiameterMeasurement.Circle_Row/Col/Radius`, then refresh.

**Calibration flow (L655-683) — copy shape for Teach Datum step machine:**
```csharp
private void CalibrateButton_Click(object sender, RoutedEventArgs e) {
    ExitCanvasMode();
    _canvasMode = ECanvasMode.Calibration;
    _calibrationPoints.Clear();

    btn_calibrate.Content = "Pick Point 1";
    label_drawHint.Content = "캔버스에서 첫 번째 점을 클릭하세요";
    label_drawHint.Visibility = Visibility.Visible;

    halconViewer.ImageLeftClicked += HalconViewer_CalibrationMouseDown;
}
```
For `TeachDatumButton_Click`: set `_canvasMode = TeachDatum`, `_datumTeachStep = Line1`, subscribe `RectDrawingCompleted` with a handler that:
1. On first completion, store rect → `_editingDatum.Line1_*`, flip `_datumTeachStep = Line2`, re-call `halconViewer.StartRectangleDrawing()`.
2. On second completion, store rect → `_editingDatum.Line2_*`, then invoke `new DatumFindingService().TryTeachDatum(image, _editingDatum, out string error)` (D-02 auto-invoke), then set hint label success/error.

**ExitCanvasMode (L495-516) — must be extended to clean up new modes:**
```csharp
private void ExitCanvasMode() {
    halconViewer.ImageLeftClicked -= HalconViewer_PolygonMouseDown;
    halconViewer.ImageRightClicked -= HalconViewer_PolygonRightClick;
    halconViewer.RectDrawingCompleted -= HalconViewer_RectDrawingCompleted;

    _canvasMode = ECanvasMode.None;
    _editingFai = null;
    btn_rectRoi.IsChecked = false;
    btn_polygonRoi.IsChecked = false;
    label_drawHint.Visibility = Visibility.Collapsed;
    label_pointCount.Visibility = Visibility.Collapsed;
    ...
}
```
Add `btn_circleRoi.IsChecked = false`, `btn_teachDatum.IsChecked = false`, `_editingDatum = null`, unsubscribe `CircleDrawingCompleted`, unsubscribe any Datum teach handler.

**Escape-key cancel (L488-493) — already routes through `ExitCanvasMode()` once new modes are listed in the enum; no new handler needed.**

---

### 3. `MainResultViewerControl.xaml.cs` — Circle drawing API + Datum render extension

**Analog:** same file. `StartRectangleDrawing`/`CommitActiveRectangle` (L194-211), `RectDrawingCompleted` event (L63), `_rectDraftRoi`/`_rectDragStart`/`_isDrawingRect` fields (L58-60), HMouseDown draft init (L395-408), HMouseMove draft update (L452-466), HMouseUp commit (L496-503), `SetDatumOverlay`/`ClearDatumOverlay` (L222-239), `Render()` dispatcher (L272/296).

**Rect start/commit pair (L192-211) — copy exactly for Circle:**
```csharp
//260408 hbk StartRectangleDrawing 추가 (Rect ROI 드래그 모드)
public void StartRectangleDrawing()
{
    _isDrawingRect = true;
    _rectDraftRoi = null;
    _rectDragStart = new Point(0, 0);
    Render();
}

public RoiDefinition CommitActiveRectangle()
{
    _isDrawingRect = false;
    var roi = _rectDraftRoi;
    _rectDraftRoi = null;
    Render();
    return roi;
}
```
New: `StartCircleDrawing()` sets `_isDrawingCircle = true` + clears draft. `CommitActiveCircle()` returns `(centerRow, centerCol, radius)` via either a struct/tuple or new event args.

**Rect event + fields (L58-63):**
```csharp
//260408 hbk Rect ROI drawing state
private bool _isDrawingRect;
private Point _rectDragStart;
private RoiDefinition _rectDraftRoi;

//260408 hbk Rect 드래그 완료 시 자동 커밋 이벤트
public event EventHandler RectDrawingCompleted;
```
Add mirrors: `_isDrawingCircle`, `_circleDraftCenter` (Point), `_circleDraftRadius` (double); new event `CircleDrawingCompleted` with args carrying `CenterRow/CenterCol/Radius` (use `EventHandler<CircleDrawCompletedArgs>` — define the args class locally near `MainViewerPointerChangedEventArgs` at L17-29 using the same `EventArgs` derivation).

**Draft-during-drag logic (HMouseDown L394-408, HMouseMove L452-466, HMouseUp L497-503):**
```csharp
// HMouseDown
if (_isDrawingRect && HasImage)
{
    _rectDragStart = mouseState.ImagePoint;
    _rectDraftRoi = new RoiDefinition { Id = "draft",
        Row1 = _rectDragStart.Y, Column1 = _rectDragStart.X,
        Row2 = _rectDragStart.Y, Column2 = _rectDragStart.X,
        IsTaught = false };
    PublishPointerInfo();
    return;
}

// HMouseMove
if (_isDrawingRect && _rectDraftRoi != null)
{
    _rectDraftRoi = new RoiDefinition { Id = "draft",
        Row1 = Math.Min(_rectDragStart.Y, mouseState.ImagePoint.Y),
        Column1 = Math.Min(_rectDragStart.X, mouseState.ImagePoint.X),
        Row2 = Math.Max(_rectDragStart.Y, mouseState.ImagePoint.Y),
        Column2 = Math.Max(_rectDragStart.X, mouseState.ImagePoint.X),
        IsTaught = true };
    Render();
    PublishPointerInfo();
    return;
}

// HMouseUp
if (_isDrawingRect && _rectDraftRoi != null)
{
    _isDrawingRect = false;
    Render();
    RectDrawingCompleted?.Invoke(this, EventArgs.Empty);
    return;
}
```
Mirror for Circle: MouseDown stores `_circleDraftCenter = mouseState.ImagePoint` + `_circleDraftRadius = 0`; MouseMove computes `radius = Math.Sqrt(dx*dx+dy*dy)` from the stored center to current pointer; MouseUp raises `CircleDrawingCompleted` with (centerRow=_circleDraftCenter.Y, centerCol=_circleDraftCenter.X, radius).

**Render dispatch (L296-323):**
```csharp
_displayService.Render(
    ViewerHost.HalconWindow, CurrentImage, _rois, _selectedRoiId,
    _rectDraftRoi, _inspectionOverlays, _displayMessages);

if (_polygonDraftPoints != null && _polygonDraftPoints.Count > 0)
    _displayService.RenderPolygon(ViewerHost.HalconWindow, _polygonDraftPoints, _polygonColor, 2);
_displayService.RenderPolygonPoints(ViewerHost.HalconWindow, _polygonDraftPoints, "red");

if (_calibrationPoints != null && _calibrationPoints.Count > 0)
    _displayService.RenderCalibrationOverlay(ViewerHost.HalconWindow, _calibrationPoints);

if (_datumConfig != null)
    _displayService.RenderDatumOverlay(ViewerHost.HalconWindow, _datumConfig, _datumSelected);
```
Add a new branch: `if (_isDrawingCircle && _circleDraftRadius > 0) _displayService.RenderCircleDraft(window, _circleDraftCenter.Y, _circleDraftCenter.X, _circleDraftRadius);`. Committed Circle ROIs render via the main `_displayService.Render(...)` path when `RoiDefinition.Shape == Circle` (D-19 — implemented in `HalconDisplayService` below).

**Datum overlay extension (L222-239) — caller already wired:**
```csharp
private DatumConfig _datumConfig;
private bool _datumSelected;

public void SetDatumOverlay(DatumConfig datum, bool isSelected)
{
    _datumConfig = datum;
    _datumSelected = isSelected;
    Render();
}

public void ClearDatumOverlay()
{
    _datumConfig = null;
    _datumSelected = false;
}
```
No signature change needed — `RenderDatumOverlay` inside `HalconDisplayService` is the extension point (see §10).

---

### 4. `InspectionListView.xaml` — node status badge + optional toolbar buttons

**Analog:** same file, `DataTemplate DataType="{x:Type local:NodeViewModel}"` block L43-53, toolbar buttons L184-201 (icon+label pattern).

**Existing DataTemplate (L43-53):**
```xml
<DataTemplate DataType="{x:Type local:NodeViewModel}">
    <Grid>
        <Grid.ColumnDefinitions>
            <ColumnDefinition Width="Auto"/>
            <ColumnDefinition Width="*"/>
            <ColumnDefinition Width="Auto"/>
        </Grid.ColumnDefinitions>
        <Image Grid.Column="0" Source="{Binding ImageSource}" Width="32" Height="32" Margin="0 0 4 0"/>
        <propToolsWpf:EditableTextBlock Grid.Column="1" Text="{Binding Name}" IsEditing="{Binding IsEditing}"/>
    </Grid>
</DataTemplate>
```
Insert a 4th column or prepend an `Ellipse` before the Image (UI-SPEC choice):
```xml
<Ellipse Width="8" Height="8" Margin="0,0,6,0" VerticalAlignment="Center"
         Fill="{Binding StatusColor}"/>
```
(UI-SPEC Layout Contract — Brush binding source = `NodeViewModel.StatusColor`, see §6.)

**Test FAI button — per UI-SPEC resolved to canvas toolbar (MainView.xaml), NOT InspectionListView.** No toolbar change needed in InspectionListView.xaml for Test FAI. Grab/Load buttons already exist at L184-201; only the code-behind gating changes.

---

### 5. `InspectionListView.xaml.cs` — SelectionChanged gating extension + NextStepHint

**Analog:** same file, `InspectionList_SelectionChanged` (L239-324), `button_grab_Click` (L360-372).

**Current Grab gating (L256-262):**
```csharp
if (itemParam is ParamBase) {
    ParamBase param = itemParam as ParamBase;
    if (itemParam is ICameraParam) {
        button_grab.IsEnabled = true;
        button_loadImage.IsEnabled = true;
        button_light.IsEnabled = true;
    }
    ...
}
```
Extend with option B (CONTEXT.md code_context — lower pollution):
```csharp
if (itemParam is ICameraParam || itemParam is DatumConfig) { // Phase 11 D-06
    button_grab.IsEnabled = true;
    button_loadImage.IsEnabled = true;
    button_light.IsEnabled = (itemParam is ICameraParam); // Light only for real camera params
}
```
Then in `button_grab_Click` (L360-372), add a `DatumConfig` branch that resolves `SourceShotName` to the owning `ShotConfig` and calls `mParentWindow.mainView.GrabAndDisplay(shot)` — shot is an `ICameraParam` (ShotConfig extends CameraSlaveParam per MainView.xaml.cs L746 comment "CameraSlaveParam is the shot itself").

**NextStepHint — push via existing channel (no XAML change):**
```csharp
// StatusBarModel.cs L112-114
public void SetText(string message) { Message = message; }
```
Call site pattern (InspectionListView.xaml.cs L329 style):
```csharp
mParentWindow.statusBar.Model.SetText("Copied : " + CopiedParam.ToString());
```
In the SelectionChanged handler, compute hint text (UI-SPEC Copywriting Contract L322-327) per node type + state and call `mParentWindow.statusBar.Model.SetText(hint)`.

**Test FAI click handler — lives in MainView.xaml.cs (toolbar placement per UI-SPEC).** Pattern: copy `FAIResults_SelectionChanged` (MainView.xaml.cs L117-137) for "find selected FAI" + call each `measurement.TryExecute(...)` (signature at MeasurementBase.cs L47-53). Hard validation uses `CustomMessageBox.Show(title, body, MessageBoxImage.Error)` per UI-SPEC Interaction Contract WR-RT-04.

---

### 6. `NodeViewModel.cs` — `StatusColor` computed brush property (D-20)

**Analog:** same file, `ImageSource` (L104-107) — scalar property delegating to `Node`, `Param` (L119-125) — exposes underlying Node state with `RaisePropertyChanged`.

**Existing scalar property pattern (L104-107):**
```csharp
public string ImageSource {
    get { return this.Node.ImageSource; }
    set { this.Node.ImageSource = value; RaisePropertyChanged("ImageSource"); }
}
```

**New computed brush pattern (Phase 11, C# 7.2 — no switch-expression):**
```csharp
public System.Windows.Media.Brush StatusColor {
    get {
        switch (this.NodeType) {
            case ENodeType.Datum: {
                var d = this.Param as ReringProject.Sequence.DatumConfig;
                return (d != null && d.IsConfigured)
                    ? (System.Windows.Media.Brush)new System.Windows.Media.SolidColorBrush(
                        (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FF22C55E"))
                    : new System.Windows.Media.SolidColorBrush(
                        (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FFEF4444"));
            }
            case ENodeType.FAI: {
                var f = this.Param as ReringProject.Sequence.FAIConfig;
                bool set = (f != null) && (f.ROI_Length1 > 0) && (f.ROI_Length2 > 0);
                return set
                    ? new System.Windows.Media.SolidColorBrush(/*green #FF22C55E*/)
                    : new System.Windows.Media.SolidColorBrush(/*red #FFEF4444*/);
            }
            case ENodeType.Measurement: {
                var m = this.Param as ReringProject.Sequence.MeasurementBase;
                if (m == null) return System.Windows.Media.Brushes.Transparent;
                if (m.TolerancePlus == 0 && m.ToleranceMinus == 0)
                    return new System.Windows.Media.SolidColorBrush(/*yellow #FFFBBF24*/);
                return new System.Windows.Media.SolidColorBrush(/*green #FF22C55E*/);
            }
            default:
                return System.Windows.Media.Brushes.Transparent;
        }
    }
}
```
Precompute static `SolidColorBrush` fields to avoid allocation per read. UI-SPEC §Color table supplies exact hexes: `#FFEF4444` red / `#FFFBBF24` yellow / `#FF22C55E` green / transparent.

Refresh strategy: tree refreshes on Selection/CRUD anyway (CONTEXT.md §Code Insights); no `RaisePropertyChanged("StatusColor")` storm needed. If responsiveness proves insufficient, add explicit raise after `TryTeachDatum`/ROI commit/tolerance edit.

For Circle-only FAI detection in the `FAI` branch: iterate `FAIConfig.MeasurementList` and check whether the only child is `CircleDiameterMeasurement`, then test `m.Circle_Radius > 0` instead of `ROI_Length*`. Same pattern as MainView.xaml.cs L132 `parentFai.ROI_Length1 <= 0`.

---

### 7. `RoiDefinition.cs` — Shape/Center/Radius fields (D-16)

**Analog:** same file, existing `[DataMember]` declarations L8-65 (flat DataContract, no custom serializer; `Clone()` uses `MemberwiseClone`).

**Existing field pattern (L14-38):**
```csharp
[DataMember] public double Row1 { get; set; }
[DataMember] public double Column1 { get; set; }
[DataMember] public double Row2 { get; set; }
[DataMember] public double Column2 { get; set; }
[DataMember] public bool IsTaught { get; set; }
[DataMember] public double Sigma { get; set; } = 1.0;
[DataMember] public int EdgeThreshold { get; set; } = 10;
```

**Additions (D-16):**
```csharp
[DataMember] public RoiShape Shape { get; set; } = RoiShape.Rect; //default = Rect for backward compat
[DataMember] public double CenterRow { get; set; }
[DataMember] public double CenterCol { get; set; }
[DataMember] public double Radius { get; set; }
```
**RoiShape enum — place inside `RoiDefinition.cs` (sibling type in the same namespace) or as a separate file in `WPF_Example/Halcon/Models/RoiShape.cs`. CONTEXT.md leaves location at Claude's Discretion; prefer the sibling-type placement for locality.**

```csharp
public enum RoiShape { Rect, Polygon, Circle }
```

**Backward-compat note:** `DataContractJsonSerializer` (used by `TeachingStorageService.Save<T>` — Services/TeachingStorageService.cs L21-28) tolerates missing members and falls back to the default (`Rect`). Verified against `[DataMember]` default-value semantics in .NET 4.8 — no migration code needed.

**NOT affected by ParamBase serialization:** `RoiDefinition` is a plain `[DataContract]` model, not a `ParamBase` subclass. It round-trips via JSON only. Adding fields is safe.

---

### 8. `DatumConfig.cs` — SourceShotName + volatile detected-line fields (D-08)

**Analog:** same file. Existing fields L20-70, especially the `[PropertyTools.DataAnnotations.Browsable(false)]` pattern for runtime-only fields (L58-59, L66-67, L69-70).

**Existing serializable field (L22-26):**
```csharp
[Category("Datum|ImageSource")]
public string ImageSourceMode { get; set; } = "Dedicated";

[Category("Datum|ImageSource")]
public string ReuseFromShotName { get; set; } = "";
```

**Existing runtime-only field pattern (L66-70):**
```csharp
[PropertyTools.DataAnnotations.Browsable(false)]
public HTuple CurrentTransform { get; set; }

[PropertyTools.DataAnnotations.Browsable(false)]
public bool LastFindSucceeded { get; set; }
```

**Phase 11 additions:**
```csharp
//260423 hbk Phase 11: D-08 — 카메라/Z/조명을 상속할 Shot 이름. 빈 문자열이면 Sequence 첫 Shot fallback.
[Category("Datum|ImageSource")]
public string SourceShotName { get; set; } = "";

//260423 hbk Phase 11: D-11 — 검출 라인 오버레이용 휘발성 필드 (TryTeachDatum 성공 시 기록)
[PropertyTools.DataAnnotations.Browsable(false)]
public double Line1Detected_RBegin { get; set; }
[PropertyTools.DataAnnotations.Browsable(false)]
public double Line1Detected_CBegin { get; set; }
[PropertyTools.DataAnnotations.Browsable(false)]
public double Line1Detected_REnd { get; set; }
[PropertyTools.DataAnnotations.Browsable(false)]
public double Line1Detected_CEnd { get; set; }
[PropertyTools.DataAnnotations.Browsable(false)]
public double Line2Detected_RBegin { get; set; }
[PropertyTools.DataAnnotations.Browsable(false)]
public double Line2Detected_CBegin { get; set; }
[PropertyTools.DataAnnotations.Browsable(false)]
public double Line2Detected_REnd { get; set; }
[PropertyTools.DataAnnotations.Browsable(false)]
public double Line2Detected_CEnd { get; set; }
[PropertyTools.DataAnnotations.Browsable(false)]
public bool LastTeachSucceeded { get; set; }
```

**CRITICAL ParamBase serialization constraint (ParamBase.cs L330-364):** ParamBase save/load uses a `switch(type)` on `Name` with cases `Int32 / Double / String / Boolean / Rect / Line / Circle / PropertyItem[] / ModelFinderViewModel`. The `Browsable(false)` runtime-only `double` fields WILL be INI-serialized because ParamBase reflects on `public` properties regardless of `Browsable`. If this is undesired (wasted INI keys), either (a) make these fields `internal` (but then MainResultViewerControl cross-assembly access breaks), (b) use backing fields with non-public accessor, or (c) accept the INI noise — they're zero on cold load and re-populated on every TryTeachDatum. **Recommended: accept the noise (same pattern already exists for other runtime doubles in `ShotConfig`/`FAIConfig`).**

---

### 9. `CircleDiameterMeasurement.cs` — optional `IHalconTeachingProvider` + field verification (D-17)

**Analog:** `TopCalibrationParam : CameraSlaveParam, IHalconTeachingProvider, IOfflineImageParam` (Action_TopCalibration.cs L49-95), `GetViewerRois` impl at L93-95.

**Existing fields (CircleDiameterMeasurement.cs L18-21) — NO CHANGE REQUIRED:**
```csharp
[Category("Circle|ROI")]
public double Circle_Row { get; set; }
public double Circle_Col { get; set; }
public double Circle_Radius { get; set; }
```
`MainView.CommitCircleRoi()` writes directly to these three fields. Phase 11 does NOT need to alter the measurement's Halcon call path — `TryFindCircle` (L49-56) already reads them.

**Optional IHalconTeachingProvider impl (Action_TopCalibration.cs L93-95):**
```csharp
public IEnumerable<RoiDefinition> GetViewerRois() {
    return TeachingJob?.Rois ?? Enumerable.Empty<RoiDefinition>();
}
```
If CircleDiameterMeasurement implements `IHalconTeachingProvider`, return a single-element list containing a `RoiDefinition { Shape = RoiShape.Circle, CenterRow = Circle_Row, CenterCol = Circle_Col, Radius = Circle_Radius, IsTaught = Circle_Radius > 0, Id = "Circle_" + MeasurementName }`. This lets `MainView.ConvertParamRects` (L425-434) surface the circle in the canvas when the Measurement node is selected in the tree.

**Recommendation per CONTEXT.md deferred list:** skip `IHalconTeachingProvider` in Phase 11 — only CircleDiameter would implement it, breaking symmetry. Instead, have `CommitCircleRoi` push the RoiDefinition into the parent `FAIConfig` (via a per-measurement mapping) so that `GetCurrentFAIRois` (MainView.xaml.cs L142-155) picks it up through the existing `fai.ToRoiDefinition()` path. This requires only extending `FAIConfig.ToRoiDefinition()` — verify signature during implementation.

---

### 10. `DatumFindingService.cs` — TryTeachDatum line-coord writeback (D-11 / Specifics "옵션 2")

**Analog:** same file, existing `TryTeachDatum` (L122-201) and `TryFindLine` private helper (L207-277).

**Existing TryTeachDatum body (L140-162):**
```csharp
double line1RowBegin, line1ColBegin, line1RowEnd, line1ColEnd;
string lineError;
if (!TryFindLine(image, imageWidth, imageHeight,
        config.Line1_Row, config.Line1_Col, config.Line1_Phi,
        config.Line1_Length1, config.Line1_Length2,
        config.Sigma, config.EdgeThreshold, config.EdgePolarity,
        out line1RowBegin, out line1ColBegin, out line1RowEnd, out line1ColEnd,
        out lineError))
{
    error = "Line1: " + lineError;
    return false;
}
// ...same for Line2...
```
These four local doubles per line are currently discarded after the intersection calculation. Phase 11 change: right before `return true;` at L194, assign them to the new volatile fields:
```csharp
// Phase 11: D-11 — 검출 라인 좌표 휘발성 저장 (오버레이용)
config.Line1Detected_RBegin = line1RowBegin;
config.Line1Detected_CBegin = line1ColBegin;
config.Line1Detected_REnd   = line1RowEnd;
config.Line1Detected_CEnd   = line1ColEnd;
config.Line2Detected_RBegin = line2RowBegin;
config.Line2Detected_CBegin = line2ColBegin;
config.Line2Detected_REnd   = line2RowEnd;
config.Line2Detected_CEnd   = line2ColEnd;
config.LastTeachSucceeded   = true;
```
No signature change (Specifics option 2 preserves caller compatibility — but there are currently 0 callers, so changing signature is also risk-free per grep).

**Error-path writes (L197-200):**
```csharp
catch (Exception ex)
{
    error = ex.Message;
    return false;
}
```
Add `config.LastTeachSucceeded = false;` inside the catch and also inside each `return false` branch for Line1/Line2 early-out.

---

### 11. `HalconDisplayService.cs` — Circle render branch + Datum detected-line render (D-11, D-19)

**Analog:** same file. Polygon branch (L50-56), Rect branch (L57-66), draft rect (L69-74), RenderDatumOverlay (L312-367). Halcon API use: `HOperatorSet.SetColor`/`SetLineWidth`/`DispLine`/`DispRectangle2`.

**Existing ROI render switch (L41-67):**
```csharp
if (rois != null)
{
    foreach (var roi in rois)
    {
        string roiColor = roi.Id == selectedRoiId ? "yellow" : "green";
        int roiWidth = roi.Id == selectedRoiId ? 3 : 2;
        window.SetColor(roiColor);
        window.SetLineWidth(roiWidth);

        //260408 hbk Polygon ROI 렌더링 지원
        if (!string.IsNullOrEmpty(roi.PolygonPoints))
        {
            var pts = ParsePolygonPoints(roi.PolygonPoints);
            if (pts != null && pts.Count >= 3)
                RenderPolygon(window, pts, roiColor, roiWidth);
        }
        else if (roi.Row1 != 0 || roi.Column1 != 0 || roi.Row2 != 0 || roi.Column2 != 0)
        {
            DrawRectangleOutline(window, roi.Row1, roi.Column1, roi.Row2, roi.Column2);
        }
        ...
```
**Phase 11 extension — insert a `Shape == Circle` branch BEFORE the polygon check (explicit shape takes precedence over implicit polygon-points presence):**
```csharp
// Phase 11: D-19 Circle ROI 렌더링
if (roi.Shape == RoiShape.Circle)
{
    // lime green committed / yellow selected (UI-SPEC color table)
    window.SetColor(roi.Id == selectedRoiId ? "yellow" : "lime green");
    window.SetLineWidth(roi.Id == selectedRoiId ? 3 : 2);
    HOperatorSet.DispCircle(window, roi.CenterRow, roi.CenterCol, roi.Radius);
    // Center cross marker (6px, red)
    window.SetColor("red");
    window.SetLineWidth(2);
    window.DispLine(roi.CenterRow - 6, roi.CenterCol, roi.CenterRow + 6, roi.CenterCol);
    window.DispLine(roi.CenterRow, roi.CenterCol - 6, roi.CenterRow, roi.CenterCol + 6);
    continue;
}
```

**Draft render (L69-74) — extend with circle draft:**
```csharp
if (draftRoi != null)
{
    window.SetColor("red");
    window.SetLineWidth(3);
    DrawRectangleOutline(window, draftRoi.Row1, draftRoi.Column1, draftRoi.Row2, draftRoi.Column2);
}
```
Add a sibling `RenderCircleDraft` public method:
```csharp
public void RenderCircleDraft(HWindow window, double centerRow, double centerCol, double radius)
{
    if (window == null || radius <= 0) return;
    window.SetColor("red");
    window.SetLineWidth(3);
    HOperatorSet.DispCircle(window, centerRow, centerCol, radius);
    // center cross
    window.DispLine(centerRow - 6, centerCol, centerRow + 6, centerCol);
    window.DispLine(centerRow, centerCol - 6, centerRow, centerCol + 6);
}
```

**RenderDatumOverlay extension (L312-367) — add detected-line + cross rendering after existing ROI rectangle draw:**
```csharp
// Existing (L314-361): draws Line1/Line2 rectangles in cyan/blue, plus magenta cross+label at RefOrigin.
// Phase 11 (D-11): after the existing body, draw detected line segments + red cross when LastTeachSucceeded.

if (datum.LastTeachSucceeded)
{
    // Line1 detected (yellow)
    HOperatorSet.SetColor(window, "yellow");
    HOperatorSet.SetLineWidth(window, 2);
    HOperatorSet.DispLine(window,
        datum.Line1Detected_RBegin, datum.Line1Detected_CBegin,
        datum.Line1Detected_REnd,   datum.Line1Detected_CEnd);

    // Line2 detected (cyan)
    HOperatorSet.SetColor(window, "cyan");
    HOperatorSet.DispLine(window,
        datum.Line2Detected_RBegin, datum.Line2Detected_CBegin,
        datum.Line2Detected_REnd,   datum.Line2Detected_CEnd);

    // Intersection cross (red, 20px, width 2) — per UI-SPEC color table
    HOperatorSet.SetColor(window, "red");
    HOperatorSet.SetLineWidth(window, 2);
    const double crossSize = 20;
    HOperatorSet.DispLine(window, datum.RefOriginRow - crossSize, datum.RefOriginCol,
                                  datum.RefOriginRow + crossSize, datum.RefOriginCol);
    HOperatorSet.DispLine(window, datum.RefOriginRow, datum.RefOriginCol - crossSize,
                                  datum.RefOriginRow, datum.RefOriginCol + crossSize);
}
```
Wrap in the existing outer `try { ... } catch { }` at L321-366 (suppress display errors per existing pattern).

**UI-SPEC color reconciliation note:** the existing RenderDatumOverlay uses `cyan/blue` for Line1/Line2 ROI rectangles and `magenta` for the RefOrigin cross (L318, L345). UI-SPEC asks for `yellow`/`cyan` ROI rectangles during teach and `red` cross. This is a deliberate Phase 11 palette change — either (a) extend `RenderDatumOverlay` to take an `isTeaching` flag, or (b) update the rectangle colors conditionally on `datum.IsConfigured`/`LastTeachSucceeded`. Implementation decision left to executor; CONTEXT.md D-19 and UI-SPEC §Color are both authoritative sources — reconcile by switching colors at render time based on state.

---

### 12. Status bar "next step" hint — REUSE existing channel (D-21)

**Analog:** `StatusBarModel.cs` L112-114 (SetText), callers MainWindow.xaml.cs L140-184, InspectionListView.xaml.cs L329/L350.

**Existing call pattern (InspectionListView.xaml.cs L329):**
```csharp
mParentWindow.statusBar.Model.SetText("Copied : " + CopiedParam.ToString());
```
No XAML change. In `InspectionList_SelectionChanged` (after computing state), call `mParentWindow.statusBar.Model.SetText(hintText)` with the text from UI-SPEC Copywriting Contract (§Status bar rows). No new property/event.

---

## Shared Patterns

### Shared 1 — Korean comment marker for code annotations
**Source:** every modified file (MainView.xaml.cs L40-47, DatumConfig.cs L16-22, HalconDisplayService.cs L50/126/312, etc.)
**Apply to:** every Phase 11 edit.
**Pattern:** prepend `//260423 hbk Phase 11: Dnn — <purpose>` to new lines (per user memory `feedback_comment_convention` — YYMMDD hbk). Match the surrounding file's brace style (Allman in Halcon code, K&R in Sequence/UI code).

### Shared 2 — ToggleButton click-handler shape
**Source:** MainView.xaml.cs L520-543 (`RectRoiButton_Click`).
**Apply to:** `btn_circleRoi`, `btn_teachDatum`.
**Pattern:** `if (btn.IsChecked == true) { ExitCanvasMode(); _canvasMode = ...; btn.IsChecked = true; <validate selection>; <set hint>; <subscribe event>; <call Start*Drawing>; } else { Commit*(); }`. Always call `ExitCanvasMode()` first to defensively clear prior mode.

### Shared 3 — CustomMessageBox for hard validation
**Source:** CustomMessageBox.cs L71-96 (`ShowConfirmation`), MainView.xaml.cs L529/L593 call sites.
**Apply to:** Re-teach confirmation (D-04), FAI ROI unset check (D-22), Datum unset check (D-22).
**Confirmation pattern:**
```csharp
MessageBoxResult res = CustomMessageBox.ShowConfirmation("Datum 재티칭",
    "기존 Datum 설정을 덮어씁니다.\n\nLine1/Line2 ROI와 RefOrigin/RefAngle이 초기화됩니다.\n계속하시겠습니까?",
    MessageBoxButton.OKCancel);
if (res != MessageBoxResult.OK) return;
```
**Error pattern:**
```csharp
CustomMessageBox.Show("검증 실패",
    "이 Sequence에 Datum이 설정되지 않았습니다.\n\nDatum 노드를 선택하여 먼저 티칭하세요.",
    MessageBoxImage.Error);
```
Use title "검증 실패" / "Datum 재티칭" per UI-SPEC Copywriting Contract.

### Shared 4 — HImage disposal discipline
**Source:** MainView.xaml.cs L87-100 (DisplayShotImage), existing `try/finally { img?.Dispose(); }` pattern.
**Apply to:** every new HImage obtained via `Shot.GetImage()`, `DeviceHandler.GrabHalconImage()`, `new HImage(path)`.
**Pattern:**
```csharp
HImage img = null;
try {
    img = shot.GetImage();
    if (img != null) { /* use img */ }
} finally {
    img?.Dispose();
}
```
Never share an `HImage` across threads without copying (HalconImageBridge.Clone).

### Shared 5 — C# 7.2 constraint
**Source:** CLAUDE.md, project-wide.
**Apply to:** everything.
**Forbidden:** `switch` expressions, `record`, nullable reference types (`string?`), top-level statements, target-typed `new()`, `init` setters.
**Allowed:** `out var`, pattern matching (`is T x`), tuple deconstruction, local functions.

### Shared 6 — No new accent hue / no new PNG icon
**Source:** UI-SPEC Anti-Patterns table.
**Apply to:** all new buttons.
**Pattern:** reuse `RoiToggleButtonStyle` (MainView.xaml L10-50) for toggles; reuse existing `#2563EB` accent only for ToggleButton `IsChecked` state. Text labels only, no `Image Source="/Resource/*.png"` additions.

### Shared 7 — Halcon named color palette (no registration)
**Source:** HalconDisplayService.cs palette usage.
**Available colors (already used elsewhere, no `set_rgb` needed):** `red`, `yellow`, `cyan`, `lime green`, `magenta`, `white`, `green`, `orange`, `blue`.
**Apply to:** all new Halcon `SetColor` / `HOperatorSet.SetColor` calls.

### Shared 8 — ParamBase reflection serialization gotcha
**Source:** ParamBase.cs L324-400 (Save/Load switch on `type.Name`).
**Supported types:** `Int32`, `Double`, `String`, `Boolean`, `Rect`, `Line`, `Circle`, `PropertyItem[]`, `ModelFinderViewModel`.
**NOT supported:** enums (will hit `default: break` and silently skip), generic collections, arbitrary DataContract classes.
**Apply to:** any new public property on a `ParamBase` subclass (DatumConfig, CircleDiameterMeasurement, FAIConfig) — keep types within the supported list. Enum-typed properties must be stored as `string` (matching existing `EdgePolarity`, `EdgeDirection` precedent).
**Important Phase 11 implication:** `RoiDefinition.Shape` is safe because `RoiDefinition` is a `[DataContract]` / JSON model, NOT a `ParamBase` subclass. If you ever decide to move `Shape` onto FAIConfig (ParamBase), you MUST store it as string.

---

## No Analog Found

| File | Role | Data Flow | Reason |
|------|------|-----------|--------|
| — | — | — | — |

All Phase 11 changes extend existing files; no file without a close codebase analog.

---

## Metadata

**Analog search scope:**
- `WPF_Example/UI/ContentItem/` (MainView, HalconViewerControl, MainResultViewerControl)
- `WPF_Example/UI/ControlItem/` (InspectionListView, NodeViewModel, Node)
- `WPF_Example/Halcon/Models/` (RoiDefinition + siblings)
- `WPF_Example/Halcon/Display/` (HalconDisplayService)
- `WPF_Example/Halcon/Algorithms/` (DatumFindingService, VisionAlgorithmService)
- `WPF_Example/Halcon/Services/` (TeachingStorageService — IHalconTeachingProvider source)
- `WPF_Example/Custom/Sequence/Inspection/` (DatumConfig, CircleDiameterMeasurement, MeasurementBase, FAIConfig, ShotConfig)
- `WPF_Example/Custom/Sequence/Top/Action_TopCalibration.cs` (IHalconTeachingProvider impl reference)
- `WPF_Example/UI/Dialog/CustomMessageBox.cs`
- `WPF_Example/UI/StatusBarModel.cs`
- `WPF_Example/Sequence/Param/ParamBase.cs`

**Files scanned:** ~15
**Pattern extraction date:** 2026-04-23
**Inherited conventions:** Korean `//YYMMDD hbk <purpose>` comment marker (from user memory), Allman brace in Halcon code / K&R brace elsewhere, C# 7.2, .NET 4.8, `SIMUL_MODE` conditional compile, ParamBase INI reflection serialization, `[DataContract]+[DataMember]` JSON serialization for Halcon models.

---

*Phase: 11-datum-teaching-ui-roi*
*Pattern map generated: 2026-04-23*
