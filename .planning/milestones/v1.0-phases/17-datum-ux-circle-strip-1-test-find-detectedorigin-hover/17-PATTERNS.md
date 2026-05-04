# Phase 17: datum-ux-circle-strip-1-test-find-detectedorigin-hover - Pattern Map

**Mapped:** 2026-04-30
**Files analyzed:** 7 (modify) / 0 (create)
**Analogs found:** 7 / 7 (all in-tree, all exact or role-match)

---

## File Classification

| File | Role | Data Flow | Closest Analog | Match Quality | Brace Style |
|------|------|-----------|----------------|---------------|-------------|
| `WPF_Example/Halcon/Display/HalconDisplayService.cs` (mod) | service / renderer | request-response (paint) | itself — `RenderCircleStripOverlay` (L441–498), `RenderDatumOverlay` (L502–695), `RenderDatumFindResult` (L216–244, replace), `DrawExtendedLine` (L387–408), `RenderRawEdgePoints` (L414–430) | exact (in-file) | Allman |
| `WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs` (mod) | model / config | CRUD (PropertyGrid + INI) | itself — per-ROI sentinel + `EnsurePerRoiDefaults` (L418–494); transient HTuple fields (L385–413); `LastTeachSucceeded` / `RefOriginRow/Col` (L341–370) | exact (in-file) | K&R (file uses `class DatumConfig : ParamBase {` opening on same line) |
| `WPF_Example/Custom/Sequence/Inspection/EdgeOptionLists.cs` (mod) | utility / options-source | static list provider | itself — `Selections` line 22, `Directions` line 13 | exact (in-file) | K&R |
| `WPF_Example/Halcon/Algorithms/DatumFindingService.cs` (mod) | service / algorithm | request-response | itself — `TryFindDatum` write-back pattern (L73–74, 95–96), `TryTeachCircleTwoHorizontal` selection wiring (L347), TLI intersection compute (L98–127) | exact (in-file) | Allman |
| `WPF_Example/UI/ContentItem/MainView.xaml.cs` (mod) | UI controller / code-behind | event-driven | itself — `BtnTestFindDatum_Click` (L1497–1532), `HalconViewer_DatumRectCompleted` (L1368–1429), `HalconViewer_RoiDeleteRequested` (L495–509), `RaisePropertyChanged + RefreshParamEditor` triple (L503–505, 1422–1426, 1441–1443) | exact (in-file) | K&R |
| `WPF_Example/UI/ContentItem/MainView.xaml` (mod) | UI layout | declarative | itself — `canvasToolbar` Grid Column 1/2 (L141–150), `label_pos` (L174–177) | exact (in-file) | n/a (XAML) |
| `WPF_Example/UI/ControlItem/InspectionListView.xaml.cs` (mod) | UI controller / code-behind | event-driven | itself — Datum node selection force rebind (L348–366), `RefreshParamEditor` (L28–33) | exact (in-file) | K&R |

**Cross-file analog (read-only reference):** `WPF_Example/UI/ContentItem/MainResultViewerControl.xaml.cs`
- `HitTestOneRoi` (L332–348) — single-method gate that the `_isEditMode` Phase 17 D-06 control branches in front of (callers L312, L323).
- `PublishPointerInfo` (L1297–1319) — already does `CurrentImage.GetGrayval((int)Math.Round(y), (int)Math.Round(x))[0].D` and fires `PointerInfoChanged`. **Phase 17 D-15 hover does not need a new GetGrayval call** — the existing pipeline already publishes X/Y/Gray; D-15 only adds three TextBlocks bound by `MainView.HalconViewer_PointerInfoChanged → UpdatePointerLabel` (L367–383).

---

## Pattern Assignments

### `HalconDisplayService.cs` — Cluster A D-01 + Cluster D D-13

**Analog:** itself (file uses Allman; 5 reusable helpers already in place).

#### D-01 — `RenderCircleStripOverlay` reduce loop to single rect

Source range: L441–498. Concrete change scope = remove `for (int k = 0; k < stepCount; k++)` loop body, keep the `thetaRad = 0.0` first iteration only.

**Existing scaffold to keep verbatim** (L444–461):
```csharp
if (datum == null) return;
if (datum.CircleROI_Radius <= 0) return;
double stepDeg = datum.Circle_PolarStepDeg;
if (stepDeg < 1.0) stepDeg = 1.0;
if (stepDeg > 30.0) stepDeg = 30.0;
// stepCount/stepRad/loop: REMOVE — not used when only k=0 path remains
double radius  = datum.CircleROI_Radius;
double centerR = datum.CircleROI_Row;
double centerC = datum.CircleROI_Col;
double length1 = Math.Min(radius * datum.Circle_RectL1Ratio, 12.0);
double length2 = Math.Min(radius * datum.Circle_RectL2Ratio, 12.0);
if (length1 < 1.0) length1 = 1.0;
if (length2 < 1.0) length2 = 1.0;
```

**Loop body to keep (single iteration with thetaRad=0.0)** — derived from L469–492:
```csharp
HOperatorSet.SetColor(window, "gray");
HOperatorSet.SetLineWidth(window, 1);
double thetaRad = 0.0; //260430 hbk Phase 17 D-01 — 3시 방향 strip 1개만 표시
double rectRow  = centerR - radius * Math.Sin(thetaRad);
double rectCol  = centerC + radius * Math.Cos(thetaRad);
double rectPhi  = thetaRad;
double cosP = Math.Cos(rectPhi);
double sinP = Math.Sin(rectPhi);
double r1 = rectRow + (-length1) * cosP - (-length2) * sinP;
double c1 = rectCol + (-length1) * sinP + (-length2) * cosP;
// ... r2/r3/r4 corners + 4 DispLine calls (existing lines 480-491)
```

#### D-13 — replace `RenderDatumFindResult` body (L216–244) with purple variant

**Existing method signature to KEEP** (L216):
```csharp
public void RenderDatumFindResult(HWindow window, DatumConfig datum)
```

**Existing body to REPLACE** (L218–243): old impl uses orange + `datum.RefOriginRow/Col`. New impl per UI-SPEC §"RenderDatumFindResult 변경 사항":
```csharp
if (window == null || datum == null) return;
if (!datum.LastFindSucceeded) return; //260430 hbk Phase 17 D-13 — find 성공 분기에서만 렌더
try
{
    // Cross size = 14, line width = 2, color = purple (UI-SPEC LOCKED)
    HOperatorSet.SetColor(window, "purple");
    HOperatorSet.SetLineWidth(window, 2);
    const double crossHalf = 14.0; //260430 hbk Phase 17 D-13
    HOperatorSet.DispLine(window,
        datum.DetectedOriginRow - crossHalf, datum.DetectedOriginCol,
        datum.DetectedOriginRow + crossHalf, datum.DetectedOriginCol);
    HOperatorSet.DispLine(window,
        datum.DetectedOriginRow, datum.DetectedOriginCol - crossHalf,
        datum.DetectedOriginRow, datum.DetectedOriginCol + crossHalf);

    // Coordinate label (above-right of cross — same offset convention as L236)
    EnsureFontInitialized(window);
    HOperatorSet.SetTposition(window,
        datum.DetectedOriginRow - crossHalf - 22,
        datum.DetectedOriginCol + crossHalf + 4);
    HOperatorSet.WriteString(window,
        "Find (" + datum.DetectedOriginRow.ToString("F1") + ", "
                 + datum.DetectedOriginCol.ToString("F1") + ")");

    // DetectedRefAngle arrow — DrawDirectionArrow pattern (L346-379) inlined for purple/length=20
    double angle  = datum.DetectedRefAngle;
    double aLen   = 20.0;
    double headLn = 5.0;
    double endRow = datum.DetectedOriginRow + aLen * Math.Sin(angle);
    double endCol = datum.DetectedOriginCol + aLen * Math.Cos(angle);
    HOperatorSet.DispLine(window, datum.DetectedOriginRow, datum.DetectedOriginCol, endRow, endCol);
    double a1 = angle + 2.5, a2 = angle - 2.5;
    HOperatorSet.DispLine(window, endRow, endCol,
        endRow + headLn * Math.Sin(a1), endCol + headLn * Math.Cos(a1));
    HOperatorSet.DispLine(window, endRow, endCol,
        endRow + headLn * Math.Sin(a2), endCol + headLn * Math.Cos(a2));
}
catch
{
    // Suppress display errors (RenderDatumOverlay catch 관습)
}
```

**Call site addition** — `RenderDatumOverlay` (existing L502–695) tail of `LastTeachSucceeded` branch should add a `RenderDatumFindResult(window, datum);` call at L688 (just before final `}`). Cluster A z-stack (UI-SPEC §"Render Order") requires this purple cross to be drawn LAST.

**Helpers to REUSE without change:** `EnsureFontInitialized` (L246–264), `DrawExtendedLine` (L387–408), `RenderRawEdgePoints` (L414–430). Brace style: Allman.

---

### `DatumConfig.cs` — Cluster A D-02 + Cluster C D-09 + Cluster D D-13/D-16

**Analog:** itself. K&R style (`public class DatumConfig : ParamBase {` opening brace same-line).

#### D-02 — Add `Circle_RadialDirection` field

**Pattern source:** Phase 15 `*_EdgeSelection` family (L115–116, 150–151, 189–190, 231–232). Excerpt L225–239 (Circle group existing):
```csharp
[ItemsSourceProperty(nameof(Circle_EdgeDirectionList))]
public string Circle_EdgeDirection   { get; set; } = "";
// ... Sigma / EdgeThreshold / EdgePolarity ...
[ItemsSourceProperty(nameof(Circle_EdgeSelectionList))] //260429 hbk Phase 15 — EdgeSelection 명시 처리
public string Circle_EdgeSelection   { get; set; } = ""; //260429 hbk Phase 15

[PropertyTools.DataAnnotations.Browsable(false)]
public List<string> Circle_EdgeDirectionList { get { return EdgeOptionLists.Directions; } }
[PropertyTools.DataAnnotations.Browsable(false)]
public List<string> Circle_EdgeSelectionList { get { return EdgeOptionLists.Selections; } }
```

**Add (mirror pattern, sentinel = `""`, fallback = `"Inward"` in EnsurePerRoiDefaults):**
```csharp
[ItemsSourceProperty(nameof(Circle_RadialDirectionList))] //260430 hbk Phase 17 D-02
public string Circle_RadialDirection { get; set; } = ""; //260430 hbk Phase 17 D-02

[PropertyTools.DataAnnotations.Browsable(false)] //260430 hbk Phase 17 D-02
public List<string> Circle_RadialDirectionList { get { return EdgeOptionLists.RadialDirections; } } //260430 hbk Phase 17 D-02
```

**EnsurePerRoiDefaults addition (Circle block, after L453):**
```csharp
if (string.IsNullOrEmpty(Circle_RadialDirection)) Circle_RadialDirection = "Inward"; //260430 hbk Phase 17 D-02
```

#### D-09 — Implement `ICustomTypeDescriptor`

**Constraint (CONTEXT 169–172):** override only `GetProperties(Attribute[])` for PropertyGrid; delegate `GetProperties()` no-arg to base via `TypeDescriptor.GetProperties(this, true)` to protect ParamBase reflection (Save/Load).

**Class declaration change (L14):**
```csharp
public class DatumConfig : ParamBase, System.ComponentModel.ICustomTypeDescriptor { //260430 hbk Phase 17 D-09
```

**Implementation skeleton (insert near end of class, before constructor at L496):**
```csharp
//260430 hbk Phase 17 D-09 — PropertyGrid 동적 노출 (AlgorithmType 별 필터)
//  PropertyTools.Wpf 가 ICustomTypeDescriptor.GetProperties(Attribute[]) 를 호출.
//  ParamBase INI 직렬화는 GetProperties() 무인자 → base(this, true) 위임으로 보호 (CONTEXT 172).
public System.ComponentModel.PropertyDescriptorCollection GetProperties(System.Attribute[] attributes) {
    var all = System.ComponentModel.TypeDescriptor.GetProperties(this, attributes, true);
    var alg = AlgorithmTypeEnum;
    var keep = new List<System.ComponentModel.PropertyDescriptor>();
    foreach (System.ComponentModel.PropertyDescriptor pd in all) {
        if (IsHiddenForAlgorithm(pd.Name, alg)) continue; //260430 hbk Phase 17 D-09
        keep.Add(pd);
    }
    return new System.ComponentModel.PropertyDescriptorCollection(keep.ToArray());
}
public System.ComponentModel.PropertyDescriptorCollection GetProperties() {
    return System.ComponentModel.TypeDescriptor.GetProperties(this, true); //260430 hbk Phase 17 D-09 — INI reflection 경로 보호
}
// Other ICustomTypeDescriptor members → delegate to TypeDescriptor (boilerplate)
public System.ComponentModel.AttributeCollection GetAttributes() { return System.ComponentModel.TypeDescriptor.GetAttributes(this, true); }
public string GetClassName() { return System.ComponentModel.TypeDescriptor.GetClassName(this, true); }
public string GetComponentName() { return System.ComponentModel.TypeDescriptor.GetComponentName(this, true); }
public System.ComponentModel.TypeConverter GetConverter() { return System.ComponentModel.TypeDescriptor.GetConverter(this, true); }
public System.ComponentModel.EventDescriptor GetDefaultEvent() { return System.ComponentModel.TypeDescriptor.GetDefaultEvent(this, true); }
public System.ComponentModel.PropertyDescriptor GetDefaultProperty() { return System.ComponentModel.TypeDescriptor.GetDefaultProperty(this, true); }
public object GetEditor(System.Type editorBaseType) { return System.ComponentModel.TypeDescriptor.GetEditor(this, editorBaseType, true); }
public System.ComponentModel.EventDescriptorCollection GetEvents(System.Attribute[] attributes) { return System.ComponentModel.TypeDescriptor.GetEvents(this, attributes, true); }
public System.ComponentModel.EventDescriptorCollection GetEvents() { return System.ComponentModel.TypeDescriptor.GetEvents(this, true); }
public object GetPropertyOwner(System.ComponentModel.PropertyDescriptor pd) { return this; }

private static bool IsHiddenForAlgorithm(string name, EDatumAlgorithm alg) {
    // UI-SPEC table:
    //   TLI: show Line1_*, Line2_* — hide Circle_*, Vertical_*, Horizontal_A_*, Horizontal_B_*
    //   CTH: show Circle_* (incl RadialDirection), Horizontal_A_*, Horizontal_B_* — hide Line1_*, Line2_*, Vertical_*, Circle_EdgeDirection
    //   VTH: show Vertical_*, Horizontal_A_*, Horizontal_B_* — hide Line1_*, Line2_*, Circle_*
    switch (alg) {
        case EDatumAlgorithm.TwoLineIntersect:
            if (name.StartsWith("Circle_") || name.StartsWith("CircleROI_") || name.StartsWith("CircleCenter_") || name.StartsWith("CircleDetected_")) return true;
            if (name.StartsWith("Vertical_")) return true;
            if (name.StartsWith("Horizontal_A_") || name.StartsWith("Horizontal_B_")) return true;
            return false;
        case EDatumAlgorithm.CircleTwoHorizontal:
            if (name.StartsWith("Line1_") || name.StartsWith("Line1Detected_")) return true;
            if (name.StartsWith("Line2_") || name.StartsWith("Line2Detected_")) return true;
            if (name.StartsWith("Vertical_")) return true;
            if (name == "Circle_EdgeDirection") return true; //260430 hbk Phase 17 D-03
            return false;
        case EDatumAlgorithm.VerticalTwoHorizontal:
            if (name.StartsWith("Line1_") || name.StartsWith("Line1Detected_")) return true;
            if (name.StartsWith("Line2_") || name.StartsWith("Line2Detected_")) return true;
            if (name.StartsWith("Circle_") || name.StartsWith("CircleROI_") || name.StartsWith("CircleCenter_") || name.StartsWith("CircleDetected_")) return true;
            return false;
    }
    return false;
}
```

#### D-13 / D-16 — Add transient fields (volatile double + ReadOnly metric trio)

**Pattern source:** L353–370 (`Line1Detected_*` doubles + `LastTeachSucceeded` bool — same Browsable(false) attribute, same K&R inline declaration).

**Add at end of transient field group (after L413):**
```csharp
//260430 hbk Phase 17 D-13 — DetectedOrigin transient (TryFindDatum write-back, INI 0 영향 — ParamBase double 직렬화하나 [Browsable(false)] 로 PropertyGrid 미표시)
[PropertyTools.DataAnnotations.Browsable(false)]
public double DetectedOriginRow { get; set; } //260430 hbk Phase 17 D-13
[PropertyTools.DataAnnotations.Browsable(false)]
public double DetectedOriginCol { get; set; } //260430 hbk Phase 17 D-13
[PropertyTools.DataAnnotations.Browsable(false)]
public double DetectedRefAngle { get; set; } //260430 hbk Phase 17 D-13

//260430 hbk Phase 17 D-16 — 결과 메트릭 PropertyGrid 노출 (ReadOnly, 사용자가 검출 품질 즉시 확인)
[Category("Datum|Result")] //260430 hbk Phase 17 D-16
[System.ComponentModel.ReadOnly(true)] //260430 hbk Phase 17 D-16
public int DetectedEdgeCount { get; set; } //260430 hbk Phase 17 D-16
[Category("Datum|Result")]
[System.ComponentModel.ReadOnly(true)]
public double DetectedFitRMSE { get; set; } //260430 hbk Phase 17 D-16
[Category("Datum|Result")]
[System.ComponentModel.ReadOnly(true)]
public double DetectedAngleDeg { get; set; } //260430 hbk Phase 17 D-16
```

> **Caveat re: INI:** UI-SPEC §"DatumConfig transient 필드" mandates `[PropertyTools.DataAnnotations.Browsable(false)] [JsonIgnore]` for the 3 origin/angle fields. ParamBase reflection (per CONTEXT 158 + Phase 11 progress: `ParamBase reflection이 Browsable 무시하고 public double 직렬화 — INI에 0으로 기록됨`) WILL serialize these as 0. This is by design (Phase 13-05 D-VIZ-01 pattern at L373). The 3 D-16 result metrics ALSO need [PropertyTools.DataAnnotations.Browsable(true)] (default) but [System.ComponentModel.ReadOnly(true)] to make PropertyTools.Wpf render them read-only. Plan stage must verify PropertyTools honors `System.ComponentModel.ReadOnly` (alternative: `[PropertyTools.DataAnnotations.ReadOnly]` — verify exists).

---

### `EdgeOptionLists.cs` — Cluster A D-02

**Analog:** itself. The whole file is 24 lines; one new line + one comment.

**Add after L22:**
```csharp
// Datum Circle ROI 안→밖 / 밖→안 그라디언트 polarity (CTH only). //260430 hbk Phase 17 D-02
public static readonly List<string> RadialDirections = new List<string> { "Inward", "Outward" }; //260430 hbk Phase 17 D-02
```

---

### `DatumFindingService.cs` — Cluster A D-02 (caller) + Cluster D D-13/D-16 write-back

**Analog:** itself (Allman). **D-17 algorithm preservation:** strict — only the lines explicitly enumerated below.

#### D-02 caller — Circle_RadialDirection → polarity in `TryTeachCircleTwoHorizontal`

**Source:** L342–351. Existing call passes `config.Circle_EdgePolarity` (string, may be "all"/"positive"/"negative"). Phase 17 D-02 maps `RadialDirection` to a polarity OVERRIDE:

**Insert 1 line BEFORE the `visionSvc.TryFindCircleByPolarSampling(` call (L342):**
```csharp
//260430 hbk Phase 17 D-02 — Circle_RadialDirection ("Inward"/"Outward") → polarity ("positive"/"negative") override
string circlePolarity = string.Equals(config.Circle_RadialDirection, "Outward", System.StringComparison.OrdinalIgnoreCase)
    ? "negative" : "positive";
```

**Modify the existing argument** (L346) `config.Circle_EdgePolarity` → `circlePolarity`. This is the *single allowed line* (D-02 spec: "caller 1 라인"). Tag with `//260430 hbk Phase 17 D-02 — RadialDirection 우선 (EdgePolarity 무시)`.

#### D-13 / D-16 — Transient write-back in `TryFindDatum` (TLI path) and `TryTeachCircleTwoHorizontal` (CTH path) on success

**TLI write-back** — `TryFindDatum` at end of try (after L132, before `return true;`):
```csharp
//260430 hbk Phase 17 D-13 — DetectedOrigin transient write-back (RenderDatumFindResult 입력)
config.DetectedOriginRow = curRow.D;
config.DetectedOriginCol = curCol.D;
config.DetectedRefAngle  = curAngle;
//260430 hbk Phase 17 D-16 — 결과 메트릭 (Line1+Line2 raw 점 합계, fit-line 의 사후 RMSE 가 없으면 0 placeholder)
config.DetectedEdgeCount = (line1RawRows?.TupleLength() ?? 0) + (line2RawRows?.TupleLength() ?? 0);
config.DetectedFitRMSE   = 0.0; // ParamBase fit RMSE 미수집 — Plan 단계 결정
config.DetectedAngleDeg  = curAngle * 180.0 / System.Math.PI;
config.LastFindSucceeded = true; //260430 hbk Phase 17 D-13
```

**Pattern source:** existing L73–74 (`config.Line1_DetectedEdgeRows = line1RawRows;`) shows precedent for runtime write-back into transient HTuple field on the success path.

#### D-13 / D-16 — Failure path: `LastFindSucceeded = false`

Source: L138 in catch block. Add **1 line** before `error = ex.Message;`:
```csharp
config.LastFindSucceeded = false; //260430 hbk Phase 17 D-13
```

Same `LastFindSucceeded = false` for each early `return false;` exit (L70, L92, L110, L116). Plan stage decides whether to keep that strict (4 lines + 1 catch) or to use a single boolean local with a single write at exit.

---

### `MainView.xaml.cs` — Cluster B D-05/D-06/D-07 + Cluster C D-10/D-11/D-12 + Cluster D D-14/D-15

**Analog:** itself. K&R style throughout. The file already implements every wiring point — Phase 17 changes are surgical replacements.

#### D-05 — 좌클릭+드래그 그리기 시작 (canvas-enter preview removal)

**Analog:** existing `HalconViewer_DatumRectCompleted` (L1368–1429) and the upstream `RectDrawingCompleted` event subscription (L1323/1330/1337/1344/1351). These already use a *click-to-finalize* model via `MainResultViewerControl.RectDrawingCompleted`. The Phase 17 D-05 fix is in `MainResultViewerControl.xaml.cs` (NOT MainView.xaml.cs) — specifically wherever `_isDrawingRect` is set true on `MouseEnter` / `MouseMove` rather than `MouseLeftButtonDown`.

> **Plan stage MUST locate** the actual offending block in `MainResultViewerControl.xaml.cs` (Grep `_isDrawingRect = true` and `_isDrawingCircle = true`) and gate it behind `MouseLeftButtonDown`. Existing handler scaffold at L876 (`ViewerHost_HMouseMove`) is the analog.

#### D-06 — `_isEditMode` single gate

**Analog:** `MainResultViewerControl.HitTestSelectedRoi` (L305–329) already has both an `_rois` lookup and a `_datumRoiCandidates` fallback. Phase 17 D-06 inserts a `_isEditMode` gate at the top of `HitTestSelectedRoi` (or at every caller — pick one).

**Add field on `MainResultViewerControl`:**
```csharp
//260430 hbk Phase 17 D-06 — Edit 모드 단일 gate (Rect+Circle+Polygon 공통)
private bool _isEditMode = false;
public bool IsEditMode { get { return _isEditMode; } set { _isEditMode = value; } }
```

**Gate at top of `HitTestSelectedRoi` (L305):**
```csharp
private RoiDefinition HitTestSelectedRoi(Point imagePoint)
{
    if (!_isEditMode) return null; //260430 hbk Phase 17 D-06
    // ... existing body ...
}
```

#### D-07 — Delete ROI 3-button modal

**Analog:** `HalconViewer_RoiDeleteRequested` (L495–509). The handler currently calls `ClearDatumRoiFields(datum, roiId)` with no confirmation.

**Pattern source for confirmation:** `CustomMessageBox.ShowConfirmation(string title, string message, MessageBoxButton buttons)` at `WPF_Example/UI/Dialog/CustomMessageBox.cs:71–96`:
```csharp
MessageBoxResult result = CustomMessageBox.ShowConfirmation("Confirmation", "...", MessageBoxButton.OKCancel);
```

**Limitation:** `MessageBoxButton` enum has only `OK` / `OKCancel` / `YesNo` / `YesNoCancel` — there is NO native 3-button "ROI 삭제 / 모든 ROI 삭제 / 취소" variant. **Plan stage decides** between (a) reuse `YesNoCancel` with Yes=this ROI, No=all, Cancel=cancel + tooltip on the dialog title, or (b) extend `CustomMessageBox` with a new overload accepting custom button labels (per UI-SPEC §"Delete ROI 모달" copywriting contract). Option (a) is lower risk — option (b) crosses CustomMessageBox boundary.

**Insertion point** — replace L502 (`ClearDatumRoiFields(datum, roiId);`) with:
```csharp
//260430 hbk Phase 17 D-07 — Delete 모달 (단일 / 전체 / 취소)
var choice = CustomMessageBox.ShowConfirmation("ROI 삭제",
    "선택한 ROI 를 삭제하시겠습니까?\n\n[예] 이 ROI만 삭제\n[아니오] 현재 Datum 의 모든 ROI 삭제\n[취소] 취소",
    MessageBoxButton.YesNoCancel);
if (choice == MessageBoxResult.Cancel || choice == MessageBoxResult.None) return;
if (choice == MessageBoxResult.Yes) {
    ClearDatumRoiFields(datum, roiId);
} else { // No → 전체
    ClearAllDatumRoiFields(datum); //260430 hbk Phase 17 D-07 — Plan 단계 helper 신규
}
```

#### D-10 — AlgorithmType 변경 흐름 (5 step 시퀀스)

**Analog:** L348–366 (InspectionListView Datum node selection) — already has the `SelectedObject = null; SelectedObject = datumCfg;` force-rebind pattern. AlgorithmType combobox change MUST trigger the same sequence.

**Pattern source #1** — RaisePropertyChanged + RefreshParamEditor + SetDatumOverlay triple at L1422–1426:
```csharp
try { _editingDatum.RaisePropertyChanged(string.Empty); } catch { }
mParentWindow?.inspectionList?.RefreshParamEditor();
halconViewer.SetDatumOverlay(_editingDatum, true);
```

**Pattern source #2** — null→new force rebind at InspectionListView.xaml.cs L361–365:
```csharp
if (ParamEditor != null) {
    ParamEditor.SelectedObject = null;
    ParamEditor.SelectedObject = datumCfg;
}
```

**Add** (in `InspectionListView` since AlgorithmType combobox is hosted there) — extend `OnParamEditorSelectionChanged` (L65) to detect the AlgorithmType-property change and run the 5-step reset:
```csharp
private void OnParamEditorSelectionChanged(object sender, SelectionChangedEventArgs e) {
    if (!(e.OriginalSource is ComboBox)) return;
    //260430 hbk Phase 17 D-10 — AlgorithmType combobox 변경 감지
    var datum = ParamEditor?.SelectedObject as DatumConfig;
    if (datum != null) {
        // Step 1: force rebind (PropertyDescriptor 재계산)
        ParamEditor.SelectedObject = null;
        ParamEditor.SelectedObject = datum;
        // Step 2: clear 검출 상태
        datum.LastTeachSucceeded = false; //260430 hbk Phase 17 D-10
        datum.LastFindSucceeded  = false; //260430 hbk Phase 17 D-10
        datum.DetectedOriginRow  = 0;     //260430 hbk Phase 17 D-10
        datum.DetectedOriginCol  = 0;     //260430 hbk Phase 17 D-10
        datum.DetectedRefAngle   = 0;     //260430 hbk Phase 17 D-10
        // Step 3: 캔버스 시각화 갱신 (RenderDatumOverlay 가 LastTeachSucceeded=false 분기에서 검출 도형 자동 미렌더)
        mParentWindow?.mainView?.halconViewer?.SetDatumOverlay(datum, true);
    }
    // Existing TryTriggerDatumAutoReteach is already DISABLED per Phase 16 D-13 — no auto-reteach added here
}
```

> **Plan stage MUST verify** AlgorithmType is in fact a ComboBox cell rather than a raw enum/string — the existing combobox path filter at L66 (`if (!(e.OriginalSource is ComboBox)) return;`) already guards this. The auto-reteach block (`TryTriggerDatumAutoReteach`) was already neutered in Phase 16 — DO NOT re-enable.

#### D-11 — Compatibility guard at btn_teachDatum click

**Analog:** `TeachDatumButton_Click` (L1258–1296 region — see L1258 entry, L1266 message). Already handles "Datum 노드 미선택" case with CustomMessageBox.

**Add new check after L1275 (`_datumTeachStep = GetFirstStep(...)`):**
```csharp
//260430 hbk Phase 17 D-11 — 새 알고리즘이 요구하는 ROI 슬롯 비어 있으면 친절한 에러
string missingRoiMsg = ValidateRoiPresence(datum, datum.AlgorithmTypeEnum); //260430 hbk Phase 17 D-11
if (missingRoiMsg != null) {
    CustomMessageBox.Show("티칭 실패", missingRoiMsg);
    btn_teachDatum.IsChecked = false;
    _canvasMode = ECanvasMode.None;
    return;
}
```

Helper (per UI-SPEC Copywriting §"실패 모달 — teach"):
```csharp
private static string ValidateRoiPresence(DatumConfig d, EDatumAlgorithm alg) {
    switch (alg) {
        case EDatumAlgorithm.TwoLineIntersect:
            if (d.Line1_Length1 <= 0 || d.Line2_Length1 <= 0) return "Line1/Line2 ROI 가 없습니다. 캔버스에 ROI 를 그리고 다시 시도하세요.";
            break;
        case EDatumAlgorithm.CircleTwoHorizontal:
            if (d.CircleROI_Radius <= 0) return "Circle ROI 가 없습니다. 캔버스에 원을 그리고 다시 시도하세요.";
            if (d.Horizontal_A_Length1 <= 0 || d.Horizontal_B_Length1 <= 0) return "Horizontal A/B ROI 가 없습니다. 캔버스에 ROI 를 그리고 다시 시도하세요.";
            break;
        case EDatumAlgorithm.VerticalTwoHorizontal:
            if (d.Vertical_Length1 <= 0) return "Vertical ROI 가 없습니다. 캔버스에 수직 ROI 를 그리고 다시 시도하세요.";
            if (d.Horizontal_A_Length1 <= 0 || d.Horizontal_B_Length1 <= 0) return "Horizontal A/B ROI 가 없습니다. 캔버스에 ROI 를 그리고 다시 시도하세요.";
            break;
    }
    return null;
}
```

#### D-12 — Failure modal upgrade (replace label_drawHint with CustomMessageBox + EdgeDirection hint)

**Analog:** `InvokeTryTeachDatum` failure branch L1484–1492 currently uses `label_drawHint.Content = "Datum 티칭 실패: " + error`. Replace with `CustomMessageBox.Show("티칭 실패", message)` + EdgeDirection hint when error contains "no edges" / "insufficient edges".

**Same pattern in `BtnTestFindDatum_Click`** L1527 (`label_testFindResult.Content = "TryFind FAIL — " + error`) → `CustomMessageBox.Show("Find 실패", FormatFindError(error))`.

```csharp
private static string FormatFindError(string err) {
    if (err == null) err = "unknown";
    //260430 hbk Phase 17 D-04 — 검출 0개 힌트 (Cluster A integration)
    if (err.IndexOf("no edges", System.StringComparison.OrdinalIgnoreCase) >= 0
        || err.IndexOf("insufficient edges", System.StringComparison.OrdinalIgnoreCase) >= 0) {
        return "검출된 에지가 없습니다. EdgeDirection 설정을 반대로 변경한 후 다시 시도하세요.";
    }
    return "Datum Find 에 실패했습니다: " + err;
}
```

#### D-14 — `BtnTestFindDatum_Click` rewrite (existing button, replace logic)

**Analog:** existing handler L1497–1532. Phase 17 D-13 transient write-back already happens in `DatumFindingService.TryFindDatum` (above). Handler change:
1. Replace `halconViewer.SetDatumFindResultOverlay(datum)` (L1524) — keep the call but adjust if `RenderDatumFindResult` is now invoked from inside `RenderDatumOverlay` (then `SetDatumOverlay(datum, true)` is sufficient).
2. Use `CustomMessageBox.Show` per D-12 instead of `label_testFindResult`.
3. Verify `datum.LastFindSucceeded` is set by `TryFindDatum` (Phase 17 D-13 write-back).

#### D-15 — Hover X/Y/Gray TextBlock — REUSE existing pipeline

**Analog:** `HalconViewer_PointerInfoChanged` → `UpdatePointerLabel` (L367–383) ALREADY computes X/Y/Gray and updates `label_pos`. The Phase 17 D-15 work is purely XAML (add 3 TextBlocks in `canvasToolbar` Grid Column 2) + a 1-line addition to `UpdatePointerLabel` that also writes to the new TextBlocks.

**Existing analog body L373–383:**
```csharp
private void UpdatePointerLabel(double x, double y, double? grayValue) {
    if (label_pos == null) return;
    label_pos.Content = string.Format(
        "X:{0:0.0}, Y:{1:0.0}, G:{2}",
        x, y,
        grayValue.HasValue ? grayValue.Value.ToString("0.0") : "-");
}
```

**Phase 17 addition:**
```csharp
private void UpdatePointerLabel(double x, double y, double? grayValue) {
    if (label_pos != null) {
        label_pos.Content = string.Format("X:{0:0.0}, Y:{1:0.0}, G:{2}",
            x, y, grayValue.HasValue ? grayValue.Value.ToString("0.0") : "-");
    }
    //260430 hbk Phase 17 D-15 — 상단 툴바 hover 표시 (정수 + N/A)
    if (txt_hoverX != null) txt_hoverX.Text = "X: " + x.ToString("0");
    if (txt_hoverY != null) txt_hoverY.Text = "Y: " + y.ToString("0");
    if (txt_hoverG != null) txt_hoverG.Text = "Gray: " + (grayValue.HasValue ? grayValue.Value.ToString("0") : "N/A");
}
```

(N/A for X/Y when image-out-of-bounds — `MainResultViewerControl.PublishPointerInfo` L1297–1303 already passes `(0, 0, null)` when `CurrentImage == null`. Plan stage may add N/A text formatting for that case too.)

---

### `MainView.xaml` — Cluster D D-15 (XAML-only)

**Analog:** existing `canvasToolbar` Grid (L61–152). Three column layout already in place. Phase 17 adds 3 TextBlocks in Column 2 (right-aligned, replacing or coexisting with `label_pointCount` at L144–146).

**Existing pattern source (L141–146):**
```xml
<Label x:Name="label_drawHint" Grid.Column="1" Visibility="Collapsed"
       HorizontalAlignment="Center" VerticalContentAlignment="Center"
       FontSize="13" Foreground="#FFAAAAAA"/>
<Label x:Name="label_pointCount" Grid.Column="2" Visibility="Collapsed"
       HorizontalAlignment="Right" VerticalContentAlignment="Center"
       FontSize="13" Foreground="#FFAAAAAA" Margin="0,0,8,0"/>
```

**Add (insert in Column 2 alongside or replacing label_pointCount):**
```xml
<!--260430 hbk Phase 17 D-15 — Hover 좌표 + 밝기 (X / Y / Gray)-->
<StackPanel Grid.Column="2" Orientation="Horizontal" HorizontalAlignment="Right"
            VerticalAlignment="Center" Margin="0,0,8,0">
    <TextBlock x:Name="txt_hoverX" Text="X: N/A" FontSize="13" Foreground="#FFAAAAAA"
               Margin="0,0,8,0" VerticalAlignment="Center"/>
    <TextBlock Text="·" FontSize="13" Foreground="#FFAAAAAA"
               Margin="0,0,8,0" VerticalAlignment="Center"/>
    <TextBlock x:Name="txt_hoverY" Text="Y: N/A" FontSize="13" Foreground="#FFAAAAAA"
               Margin="0,0,8,0" VerticalAlignment="Center"/>
    <TextBlock Text="·" FontSize="13" Foreground="#FFAAAAAA"
               Margin="0,0,8,0" VerticalAlignment="Center"/>
    <TextBlock x:Name="txt_hoverG" Text="Gray: N/A" FontSize="13" Foreground="#FFAAAAAA"
               VerticalAlignment="Center"/>
</StackPanel>
```

> **Plan stage decides** whether to replace `label_pointCount` (which is `Visibility="Collapsed"` and unused for Datum) or to coexist by adjusting Grid.Column ordering. Replacement is simpler.

---

### `InspectionListView.xaml.cs` — Cluster C D-09/D-10 compatibility

**Analog:** L348–366 (Datum node selection force-rebind block already in place since Phase 16-02). Phase 17 D-09 ICustomTypeDescriptor implementation should not break this — the null→new rebind pattern still works because `ParamEditor.SelectedObject = datumCfg` re-triggers `ICustomTypeDescriptor.GetProperties(Attribute[])`.

**No code change needed in this file** unless Plan 17-02 adds the AlgorithmType combobox detection (D-10) here. See `OnParamEditorSelectionChanged` extension under MainView.xaml.cs §D-10 — that addition lives in this file.

**Existing pattern source (L26–33) to KEEP unchanged:**
```csharp
public void RefreshParamEditor() {
    if (ParamEditor == null) return;
    var current = SelectedParam;
    ParamEditor.SelectedObject = null;
    ParamEditor.SelectedObject = current;
}
```

---

## Shared Patterns

### Pattern S1 — `//260430 hbk Phase 17 <reason>` comment convention (D-18)

**Source:** Project-wide convention enforced by `CLAUDE.md` § Conventions and `.claude/projects/.../MEMORY.md` `feedback_comment_convention.md`. Examples in every file already (e.g., HalconDisplayService L432: `//260429 hbk Phase 16 D-01 — 원 ROI 그린 직후 ...`).

**Apply to:** EVERY changed line. Acceptance gate: grep count ≥ delta(LOC) on changed files.

### Pattern S2 — Per-ROI sentinel + EnsurePerRoiDefaults idempotent migration (D-02 / D-13)

**Source:** `DatumConfig.cs` L418–494. The function is called from `TryFindDatum` L46 and `TryTeachDatum` L165. New fields follow the same pattern: declare `string` field with `= ""`, then add a `if (string.IsNullOrEmpty(...)) ... = "fallback";` line in EnsurePerRoiDefaults. Idempotent — safe across multiple calls.

**Apply to:** `Circle_RadialDirection` (D-02). Transient doubles (D-13/D-16) do NOT need EnsurePerRoiDefaults because their default 0.0 is already the "unset" sentinel.

### Pattern S3 — `RaisePropertyChanged + RefreshParamEditor + SetDatumOverlay` triple

**Source:** `MainView.xaml.cs` L503–505, L587–589, L618–620, L1422–1426, L1441–1443 — used 5 times for Datum field write-back ↔ PropertyGrid + canvas re-render synchronization.

**Apply to:** D-10 (AlgorithmType change), D-13 (Test Find result), D-07 (Delete confirmation).

### Pattern S4 — `CustomMessageBox.Show(title, message)` for failures only (D-12)

**Source:** `MainView.xaml.cs` L927, L989, L1091, L1193, L1226, L1241, L1266, L1501, L1569 — all current callers use `(title, message)` signature post-Phase 13-03 swap.

**Apply to:** D-07 (3-button confirmation via `ShowConfirmation`), D-11 (compatibility error), D-12 (teach + find failure).

### Pattern S5 — `LastTeachSucceeded` / `LastFindSucceeded` boolean gate

**Source:** `DatumConfig.cs` L350, L370. Used by `RenderDatumOverlay` L628 (`if (datum.LastTeachSucceeded) { ... DrawExtendedLine ... DispCircle ... }`) to conditionally render result-state overlays.

**Apply to:** Phase 17 `RenderDatumFindResult` (gate on `LastFindSucceeded`); D-10 reset (`= false` on AlgorithmType change).

### Pattern S6 — Allman vs K&R brace style, file-level (D-19)

**Source:** `CLAUDE.md` § Code Style + `17-UI-SPEC.md` § Code Style Contract.

| File | Style |
|------|-------|
| HalconDisplayService.cs | Allman |
| DatumConfig.cs | K&R (existing — `class DatumConfig : ParamBase {` on same line) |
| DatumFindingService.cs | Allman |
| EdgeOptionLists.cs | K&R |
| MainView.xaml.cs | K&R |
| InspectionListView.xaml.cs | K&R |

> **Note:** UI-SPEC §"Code Style Contract" lists DatumConfig as Allman, but the existing file uses K&R (`class DatumConfig : ParamBase {` at L14, opening brace same-line; methods like `EnsurePerRoiDefaults` at L418 also K&R). **Follow the existing in-file style** per CONTEXT D-19 ("file 마다 기존 스타일 따름"). Plan stage should flag this UI-SPEC inconsistency.

---

## No Analog Found

None. All 7 files have exact in-file analogs. All Phase 17 work is incremental modification of existing structure.

---

## Plan-File Mapping (per CONTEXT D-21~D-24)

| Plan | Files | Primary patterns |
|------|-------|------------------|
| **17-01** (Cluster A — D-01~D-04) | HalconDisplayService.cs, DatumConfig.cs (RadialDirection only), DatumFindingService.cs (1 caller line), EdgeOptionLists.cs | S1 + S2 + S6 |
| **17-02** (Cluster B+C — D-05~D-12) | MainView.xaml.cs, MainResultViewerControl.xaml.cs (D-05/D-06 only), DatumConfig.cs (ICustomTypeDescriptor only), InspectionListView.xaml.cs | S1 + S3 + S4 + S6 |
| **17-03** (Cluster D — D-13~D-16) | DatumConfig.cs (transient fields), DatumFindingService.cs (write-back lines), HalconDisplayService.cs (RenderDatumFindResult), MainView.xaml + .cs | S1 + S2 + S5 |
| **17-04** (UAT — D-24) | (no source change) | n/a |

**Cross-plan boundary:** DatumConfig.cs is touched by 3 plans (17-01 / 17-02 / 17-03). Planner should split the file's edits by region: (17-01) Circle_RadialDirection field block; (17-02) ICustomTypeDescriptor implementation block + GetProperties helper; (17-03) DetectedOrigin/RefAngle transient + DetectedEdgeCount/FitRMSE/AngleDeg metric block. EnsurePerRoiDefaults addition is in 17-01.

---

## Metadata

**Analog search scope:** `WPF_Example/Halcon/Display/`, `WPF_Example/Halcon/Algorithms/`, `WPF_Example/Custom/Sequence/Inspection/`, `WPF_Example/UI/ContentItem/`, `WPF_Example/UI/ControlItem/`, `WPF_Example/UI/Dialog/`, `WPF_Example/Custom/UI/ContentItem/`
**Files scanned:** 7 target + 3 reference (MainResultViewerControl, CustomMessageBox, WaferMapView)
**Pattern extraction date:** 2026-04-30
