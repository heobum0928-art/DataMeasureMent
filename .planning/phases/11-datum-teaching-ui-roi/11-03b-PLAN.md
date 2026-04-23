---
phase: 11-datum-teaching-ui-roi
plan: 03b
type: execute
wave: 3
depends_on: [11-02, 11-03a]
files_modified:
  - WPF_Example/UI/ContentItem/MainView.xaml
  - WPF_Example/UI/ContentItem/MainView.xaml.cs
  - WPF_Example/UI/ControlItem/InspectionListView.xaml.cs
autonomous: false
requirements: []
defects: [WR-RT-03]
decisions: [D-01, D-02, D-03, D-04, D-06, D-07, D-09, D-10, D-12, D-13, D-25, D-26]
tags: [wpf, datum, teaching, ui, canvas-toolbar, state-machine, uat]

must_haves:
  truths:
    - "Datum node selection in InspectionListView enables button_grab and button_loadImage (ICameraParam || DatumConfig predicate at L258)"
    - "Datum node selection enables mParentWindow.mainView.btn_teachDatum.IsEnabled = true; non-Datum selection disables it"
    - "Canvas toolbar has btn_teachDatum ToggleButton reusing RoiToggleButtonStyle, MinWidth=100 (Teach Datum is 11 chars)"
    - "ECanvasMode.TeachDatum + EDatumTeachStep { Line1, Line2, Done } drive a two-step Rect ROI capture"
    - "Second MouseUp (Line2 committed) auto-invokes DatumFindingService.TryTeachDatum(MainResultViewerControl.CurrentImage, _editingDatum, out error) per D-02"
    - "On re-teach when IsConfigured==true, CustomMessageBox.ShowConfirmation(title=\"Datum 재티칭\", UI-SPEC body, OKCancel) is shown; Cancel preserves snapshot"
    - "On TryTeachDatum success, label_drawHint turns green '#FF4ADE80' with 'Datum 티칭 완료 — Recipe Save 권장'; overlay refresh calls halconViewer.SetDatumOverlay(datum, true)"
    - "On TryTeachDatum failure, label_drawHint turns red '#FFF87171' with 'Datum 티칭 실패: {error}'; ROIs retained; re-click re-enters step=Line1"
    - "ImageSourceMode branch: Dedicated → mParentWindow.mainView.GrabAndDisplay(sourceShot); ReuseFromShot → reuse the Shot's cached HImage; ReuseFromShot+no-cache → label_drawHint red \"Shot '{name}'을 먼저 Grab하세요\" + abort per D-10"
    - "ResolveDatumSourceShot helper walks InspectionRecipeManager.Shots (L25) by SourceShotName; empty string → Shots[0] fallback per D-08; not-found → abort + hint per D-10"
    - "Escape during teach restores DatumConfig Line1/Line2 to pre-teach snapshot, exits cleanly (handled in ExitCanvasMode)"
    - "Existing Phase 6/7 INI recipes still load (SourceShotName defaults to \"\")"
    - "Debug/x64 build passes"
    - "SIMUL_MODE UAT golden path + failure path + re-teach + ReuseFromShot branch + existing Phase 4 post-teach palette (cyan/blue/magenta) all verified unchanged"
  artifacts:
    - path: "WPF_Example/UI/ContentItem/MainView.xaml"
      provides: "btn_teachDatum ToggleButton with Separator"
      contains: "btn_teachDatum"
    - path: "WPF_Example/UI/ContentItem/MainView.xaml.cs"
      provides: "ECanvasMode.TeachDatum + EDatumTeachStep + TeachDatumButton_Click + HalconViewer_DatumRectCompleted + ResolveDatumSourceShot + ShowDatumTeachHint"
      contains: "EDatumTeachStep"
    - path: "WPF_Example/UI/ControlItem/InspectionListView.xaml.cs"
      provides: "Datum node → button_grab/button_loadImage + btn_teachDatum.IsEnabled gating + DatumConfig-aware button_grab_Click delegation"
      contains: "DatumConfig"
  key_links:
    - from: "InspectionListView.InspectionList_SelectionChanged L258"
      to: "button_grab.IsEnabled = true + mParentWindow.mainView.btn_teachDatum.IsEnabled = (item.NodeType == ENodeType.Datum)"
      via: "(itemParam is ICameraParam || itemParam is DatumConfig) predicate"
      pattern: "itemParam is DatumConfig"
    - from: "InspectionListView.button_grab_Click (L360-372)"
      to: "MainView.GrabAndDisplay(sourceShot) or reuse of sourceShot cached image"
      via: "ResolveDatumSourceShot walks InspectionRecipeManager.Shots by SourceShotName"
      pattern: "ResolveDatumSourceShot"
    - from: "MainView HalconViewer_DatumRectCompleted (step=Line2 MouseUp)"
      to: "new DatumFindingService().TryTeachDatum(halconViewer.CurrentImage, _editingDatum, out error)"
      via: "auto-invoke after second rect commit"
      pattern: "TryTeachDatum"
    - from: "TryTeachDatum success"
      to: "halconViewer.SetDatumOverlay(_editingDatum, isSelected:true) → HalconDisplayService additive branch from Plan 03a"
      via: "Plan 03a's LastTeachSucceeded==true branch renders detected lines + red cross"
      pattern: "SetDatumOverlay"
---

<objective>
Phase 11 Plan 03b — the UI wiring + toolbar state machine that ties together Plan 11-02 (canvas toolbar extensions) and Plan 11-03a (DatumConfig/DatumFindingService/HalconDisplayService). Closes WR-RT-03 (🔴 Blocker) end-to-end.

Three code tasks + one UAT checkpoint:
1. InspectionListView.xaml.cs — extend the Grab/Load predicate at L258 to include DatumConfig; add a DatumConfig branch in button_grab_Click (L360) that resolves SourceShotName to a Shot via InspectionRecipeManager.Shots; gate mParentWindow.mainView.btn_teachDatum.IsEnabled = (item.NodeType == ENodeType.Datum).
2. MainView.xaml — add btn_teachDatum ToggleButton (MinWidth=100) + Separator to canvas toolbar.
3. MainView.xaml.cs — extend ECanvasMode with TeachDatum; add EDatumTeachStep; add TeachDatumButton_Click with re-teach confirmation; add HalconViewer_DatumRectCompleted for 2-step Rect capture with auto-invoke TryTeachDatum on step=Line2 MouseUp; add ResolveDatumSourceShot helper; add ShowDatumTeachHint internal helper; extend ExitCanvasMode to restore snapshot on Escape during teach.
4. UAT checkpoint — SIMUL_MODE verify golden path + failure path + re-teach confirmation + ReuseFromShot branch + existing Phase 4 post-teach palette (cyan/blue/magenta @L345/L357) unchanged.

Purpose: unblock Datum teaching entirely. Plan 04 then adds workflow guidance (badges + status bar + Test FAI) on top of this functional teach flow.

Output: working Datum teach flow usable in SIMUL_MODE via a real image, visual feedback loop, backward-compat INI.
</objective>

<execution_context>
@$HOME/.claude/get-shit-done/workflows/execute-plan.md
@$HOME/.claude/get-shit-done/templates/summary.md
</execution_context>

<context>
@CLAUDE.md
@.planning/STATE.md
@.planning/phases/11-datum-teaching-ui-roi/11-CONTEXT.md
@.planning/phases/11-datum-teaching-ui-roi/11-UI-SPEC.md
@.planning/phases/11-datum-teaching-ui-roi/11-PATTERNS.md
@.planning/phases/11-datum-teaching-ui-roi/11-03a-SUMMARY.md
@WPF_Example/UI/ContentItem/MainView.xaml
@WPF_Example/UI/ContentItem/MainView.xaml.cs
@WPF_Example/UI/ContentItem/MainResultViewerControl.xaml.cs
@WPF_Example/UI/ControlItem/InspectionListView.xaml.cs
@WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs
@WPF_Example/Custom/Sequence/Inspection/InspectionRecipeManager.cs
@WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs
@WPF_Example/Custom/Sequence/Inspection/ShotConfig.cs

<interfaces>
<!-- Consumed from Plan 03a (already landed by the time this plan runs): -->
<!--   DatumConfig.SourceShotName (string), Line1/2Detected_RBegin/CBegin/REnd/CEnd (double, [Browsable(false)]), LastTeachSucceeded (bool) -->
<!--   DatumFindingService.TryTeachDatum(HImage image, DatumConfig config, out string error) — signature unchanged; writes detected coords + LastTeachSucceeded on all exits -->
<!--   HalconDisplayService.RenderDatumOverlay — additive LastTeachSucceeded==true branch draws yellow Line1 + cyan Line2 + red 20px cross -->

<!-- Consumed from Plan 02 (canvas toolbar extensions, already landed): -->
<!--   btn_rectRoi / btn_polygonRoi / btn_circleRoi handlers exist; IsEnabled gating from InspectionList_SelectionChanged path is set by Plan 02 at InspectionListView.xaml.cs L239-324 (SAME SITE this plan extends for Datum). Plan 02 mirrored the existing "select FAI → enable ROI buttons" pattern. -->

<!-- VERIFIED source-file symbols (read during revision; embed as-is, do not re-verify): -->

```csharp
// WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs L49 (InspectionSequence : SequenceBase)
public List<DatumConfig> DatumConfigs { get; private set; } = new List<DatumConfig>();

// WPF_Example/Custom/Sequence/Inspection/InspectionRecipeManager.cs L25
public List<ShotConfig> Shots { get; private set; } = new List<ShotConfig>();
// InspectionRecipeManager is the per-sequence recipe owner; InspectionSequence.cs:80 iterates via `recipeManager.Shots`.
// Each InspectionSequence exposes its recipeManager — confirm exact accessor name when implementing (likely a field / property; use the same accessor used at InspectionSequence.cs:80).

// WPF_Example/UI/ContentItem/MainResultViewerControl.xaml.cs L100 — PUBLIC accessor, no new method needed:
public HImage CurrentImage { get; private set; }
// (L120: set via `CurrentImage = new HImage(imagePath)`; L133-137 LoadImage path clones the input.)

// WPF_Example/UI/ContentItem/MainView.xaml.cs L200 — existing Grab delegation (ShotConfig : CameraSlaveParam : ICameraParam):
public async void GrabAndDisplay(ICameraParam param, bool eventCall = false);
// Body (L200-219): runs on a Task, calls pLight.ApplyLight(param), pDev.GrabHalconImage(param), param.PutImage(img), then DisplayToViewer on UI thread.
// This is the existing Grab path that non-Datum selections already use (InspectionListView.xaml.cs:371 — `mParentWindow.mainView.GrabAndDisplay(camParam);`). Datum grab will reuse this same method, passing `sourceShot` (a ShotConfig, which IS an ICameraParam).

// WPF_Example/UI/ControlItem/InspectionListView.xaml.cs L239-324 — InspectionList_SelectionChanged
// Key existing sites (post Plan 02):
//   L258: `if (itemParam is ICameraParam) { button_grab.IsEnabled = true; button_loadImage.IsEnabled = true; button_light.IsEnabled = true; }`
//   L273-284: already has `if (item.NodeType == ENodeType.Datum)` block that calls `mParentWindow.mainView.halconViewer.SetDatumOverlay(datumCfg, true)` at L281-283
//   L371: `mParentWindow.mainView.GrabAndDisplay(camParam);` — non-Datum grab delegation used as template for Task 1
```

<!-- Contracts CREATED by this plan (consumed by Plan 04): -->
```csharp
// MainView.xaml.cs — new internal helper for external hint display
internal void ShowDatumTeachHint(string message, bool isError);
// Sets label_drawHint Content/Foreground/Visibility. Foreground: error → #FFF87171, info → #FFAAAAAA.

// MainView.xaml.cs — new state machine
private enum ECanvasMode { None, RectRoi, PolygonRoi, CircleRoi, TeachDatum, Calibration }
private enum EDatumTeachStep { Line1, Line2, Done }
private ReringProject.Sequence.DatumConfig _editingDatum;
```
</interfaces>
</context>

<tasks>

<task type="auto" tdd="false">
  <name>Task 1: InspectionListView.xaml.cs — Datum node Grab/Load enable + btn_teachDatum IsEnabled gating + DatumConfig-aware button_grab_Click delegation</name>
  <files>WPF_Example/UI/ControlItem/InspectionListView.xaml.cs</files>
  <read_first>
    - WPF_Example/UI/ControlItem/InspectionListView.xaml.cs — InspectionList_SelectionChanged at L239-324 (verified during revision: L241-242 reset button_light/button_grab, L248-249 resolves `item` (NodeViewModel) and `itemParam` (object), L253 clears Datum overlay, L258 current ICameraParam gate, L273-284 existing Datum-node branch with overlay hook, L316-321 default else branch, L322-324 close), button_grab_Click at L360-372 (current body: `mParentWindow.mainView.GrabAndDisplay(camParam)` at L371), button_loadImage_Click at L374-379 (parallel to grab, calls `mParentWindow.mainView.LoadAndDisplay(camParam)`)
    - WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs (post Plan 03a) — SourceShotName visible
    - WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs L49 `DatumConfigs` list; L80 shows `recipeManager.Shots` iteration pattern (use this exact accessor pattern in the helper)
    - WPF_Example/Custom/Sequence/Inspection/InspectionRecipeManager.cs L25 `Shots` list
    - WPF_Example/Custom/Sequence/Inspection/ShotConfig.cs — confirm ShotConfig is ICameraParam (it IS, per L200 GrabAndDisplay signature + L371 call-site evidence); confirm Name/ShotName property
    - WPF_Example/UI/ContentItem/MainView.xaml.cs L200 `GrabAndDisplay(ICameraParam param, bool eventCall = false)` — reuse this method; pass `sourceShot` (a ShotConfig which implements ICameraParam)
    - .planning/phases/11-datum-teaching-ui-roi/11-PATTERNS.md §5 "InspectionListView.xaml.cs — SelectionChanged gating extension"
    - Plan 11-02 already pinned the same SelectionChanged site (L239-324) for Rect/Polygon/Circle IsEnabled gating — Datum gating here mirrors that pattern exactly.
  </read_first>
  <action>
    1. Extend the ICameraParam predicate at **L258** to also enable Grab/Load when the selected param is a DatumConfig (D-06):
       ```csharp
       //260423 hbk Phase 11 D-06 — Datum 노드에서도 Grab/Load 활성화
       if (itemParam is ICameraParam || itemParam is ReringProject.Sequence.DatumConfig) {
           button_grab.IsEnabled = true;
           button_loadImage.IsEnabled = true;
           button_light.IsEnabled = (itemParam is ICameraParam); // Light only for real camera params — DatumConfig inherits lighting from SourceShotName Shot
       }
       ```
       Keep the existing line at L263 (`mParentWindow.mainView.SetParam(item.SequenceID, param);`) unchanged so PropertyGrid still populates.

    2. In the existing `if (item.NodeType == ENodeType.Datum)` block (currently L273-284, which already calls `mParentWindow.mainView.halconViewer.SetDatumOverlay(datumCfg, true)` at L281-283), add the btn_teachDatum enable toggle AFTER the SetDatumOverlay call:
       ```csharp
       //260423 hbk Phase 11 D-01 — Datum 노드 선택 시 btn_teachDatum 활성화 (Plan 11-02에서 고정한 같은 사이트에서 Rect/Polygon/Circle 활성화와 동일한 방식으로 mirroring)
       mParentWindow.mainView.btn_teachDatum.IsEnabled = true;
       ```
       In the EVERY OTHER NodeType branch (Measurement L286, FAI L293, Action L302, Sequence L310, default else L316), add **exactly one line** disabling btn_teachDatum:
       ```csharp
       //260423 hbk Phase 11 D-01 — 비 Datum 노드에서는 Teach Datum 비활성화
       mParentWindow.mainView.btn_teachDatum.IsEnabled = false;
       ```
       Keep the existing Rect/Polygon/Circle IsEnabled assignments that Plan 11-02 already installed at this site untouched.

    3. In `button_grab_Click` (L360-372), prepend a DatumConfig branch BEFORE the existing ICameraParam body. The current body fetches `camParam` (the selected item's param as ICameraParam) and calls `mParentWindow.mainView.GrabAndDisplay(camParam)` at L371. The new Datum branch resolves the SourceShotName to a ShotConfig and either delegates to the SAME GrabAndDisplay (Dedicated mode) or reuses the Shot's cached HImage (ReuseFromShot mode):
       ```csharp
       //260423 hbk Phase 11 D-07/D-08/D-09/D-10 — Datum 노드 Grab: SourceShotName Shot을 찾아 위임
       var datum = SelectedParam as ReringProject.Sequence.DatumConfig; // SelectedParam is the field set at InspectionList_SelectionChanged:264
       if (datum != null) {
           ReringProject.Sequence.ShotConfig sourceShot = ResolveDatumSourceShot(datum);
           if (sourceShot == null) {
               //260423 hbk Phase 11 D-10 — SourceShotName 해결 실패 → 인라인 힌트 + 모드 종료
               mParentWindow.mainView.ShowDatumTeachHint(
                   string.IsNullOrEmpty(datum.SourceShotName)
                       ? "Sequence에 Shot이 없습니다"
                       : string.Format("SourceShotName '{0}'을 찾을 수 없습니다", datum.SourceShotName),
                   isError: true);
               return;
           }
           if (string.Equals(datum.ImageSourceMode, "ReuseFromShot", System.StringComparison.OrdinalIgnoreCase)) {
               //260423 hbk Phase 11 D-07/D-10 — ReuseFromShot: Shot의 캐시 이미지를 그대로 표시
               HalconDotNet.HImage cached = sourceShot.GetImage(); // ShotConfig.GetImage() — confirmed accessor; null if never grabbed
               if (cached == null) {
                   mParentWindow.mainView.ShowDatumTeachHint(
                       string.Format("Shot '{0}'을 먼저 Grab하세요", sourceShot.Name),
                       isError: true);
                   return;
               }
               // Reuse the Shot as-if it were the selected ICameraParam: it already is one,
               // but we want to DISPLAY the cached image without re-grabbing. Use MainView.DisplayShotImage (L86 — existing private method).
               mParentWindow.mainView.DisplayExistingShotImage(sourceShot); // new internal helper added in Task 3 below that wraps private DisplayShotImage(L86)
               return;
           }
           //260423 hbk Phase 11 D-07 — Dedicated: 기존 Grab 경로 재사용 (sourceShot은 ShotConfig → ICameraParam)
           mParentWindow.mainView.GrabAndDisplay(sourceShot);
           return;
       }
       // --- existing non-Datum grab logic below (L370-371) unchanged ---
       ```
       If `SelectedParam` field name differs (confirm at L264 during implementation), use the same accessor. If `ShotConfig.GetImage()` has a different name, use the actual name — ShotConfig is the per-shot image holder (PATTERNS.md §8 description + CONTEXT.md D-07).

    4. Do the same for `button_loadImage_Click` at L374-379 — prepend an analogous Datum branch that, for Dedicated mode, calls `mParentWindow.mainView.LoadAndDisplay(sourceShot)` (mirrors L379), and for ReuseFromShot mode behaves identically to grab (reuse cached image). If load-from-file semantics differ, delegate to `LoadAndDisplay(sourceShot)` in both modes since load does not depend on grab cache.

    5. Add ResolveDatumSourceShot helper (private) at the bottom of the class. Traverses from the selected Datum node up to its parent InspectionSequence, then into the sequence's InspectionRecipeManager.Shots by SourceShotName; falls back to Shots[0] when SourceShotName is empty (D-08); returns null when the sequence cannot be resolved or Shots is empty:
       ```csharp
       //260423 hbk Phase 11 D-08 — Datum → Sequence → Shot 해결 (빈 이름이면 첫 Shot fallback)
       private ReringProject.Sequence.ShotConfig ResolveDatumSourceShot(ReringProject.Sequence.DatumConfig datum)
       {
           if (datum == null) return null;

           // Resolve the owning InspectionSequence by walking ContextList / NodeTree.
           // The selected node is `list.SelectedItem as NodeViewModel` (see SelectionChanged L248-249);
           // its SequenceID identifies the sequence. Ask SystemHandler for the InspectionSequence by ID:
           var list = inspectionList; // the TreeListBox reference used throughout this file (verify field name at SelectionChanged L247)
           var selected = list?.SelectedItem as NodeViewModel;
           if (selected == null) return null;

           // Walk up to the Sequence node. NodeTree/NodeViewModel should expose either a Parent chain
           // or a SequenceID the caller can use to look up InspectionSequence via SystemHandler.Handle.
           // Use the existing lookup helper the codebase already has (check InspectionSequence.cs or the
           // SystemHandler.Handle.seqHandler accessor used elsewhere). Equivalent to:
           //   var seq = SystemHandler.Handle.seqHandler[selected.SequenceID] as InspectionSequence;
           // If the codebase uses a different accessor name (e.g. SequenceHandler's indexer), use that — but
           // the selected node's SequenceID (already set at line where mParentWindow.mainView.SetParam(item.SequenceID, param)
           // is called, L263) is the canonical key.

           ReringProject.Sequence.InspectionSequence seq = null;
           try {
               seq = SystemHandler.Handle.seqHandler[selected.SequenceID] as ReringProject.Sequence.InspectionSequence;
           } catch { seq = null; }
           if (seq == null) return null;

           // InspectionRecipeManager accessor — InspectionSequence.cs:80 iterates via `recipeManager.Shots`;
           // use the same field/property name here. If the field is private, use the public `DatumConfigs`-adjacent
           // accessor (likely `seq.RecipeManager.Shots` or similar — confirm by reading InspectionSequence.cs).
           var shots = seq.RecipeManager?.Shots; // if RecipeManager is the public name; otherwise use seq.recipeManager
           if (shots == null || shots.Count == 0) return null;

           if (string.IsNullOrEmpty(datum.SourceShotName)) return shots[0]; // D-08 fallback

           foreach (var s in shots) {
               if (string.Equals(s.Name, datum.SourceShotName, System.StringComparison.Ordinal)) return s;
           }
           return null; // not found → caller produces D-10 hint and aborts
       }
       ```
       **Note on the RecipeManager accessor name:** InspectionSequence.cs:80 uses `recipeManager.Shots` — confirm whether this is a public property or a private field. If private, either (a) add a public pass-through `public InspectionRecipeManager RecipeManager => recipeManager;` inside InspectionSequence.cs (a tiny additional edit — list InspectionSequence.cs in files_modified if you take this path), or (b) use an existing public accessor if one already exists. Prefer option (b) — read InspectionSequence.cs public surface before adding a new property.

    6. Brace style: K&R (UI code). C# 7.2.
    7. `//260423 hbk Phase 11` marker on every new block.
  </action>
  <verify>
    <automated>msbuild WPF_Example/DatumMeasurement.csproj /p:Configuration=Debug /p:Platform=x64 /v:minimal</automated>
  </verify>
  <acceptance_criteria>
    - `grep -n "itemParam is ReringProject.Sequence.DatumConfig" WPF_Example/UI/ControlItem/InspectionListView.xaml.cs` at least one match at the L258-region predicate.
    - `grep -c "btn_teachDatum.IsEnabled = true\\|btn_teachDatum.IsEnabled = false" WPF_Example/UI/ControlItem/InspectionListView.xaml.cs` returns at least 5 (one enable in Datum branch + disables in Measurement/FAI/Action/Sequence/default).
    - `grep -n "private.*ShotConfig ResolveDatumSourceShot" WPF_Example/UI/ControlItem/InspectionListView.xaml.cs` returns one match.
    - `grep -n "SourceShotName" WPF_Example/UI/ControlItem/InspectionListView.xaml.cs` returns at least 2 matches (helper + grab branch).
    - `grep -n "ReuseFromShot" WPF_Example/UI/ControlItem/InspectionListView.xaml.cs` returns at least one match.
    - `grep -n "을 먼저 Grab하세요" WPF_Example/UI/ControlItem/InspectionListView.xaml.cs` returns at least one match (D-10 UI-SPEC verbatim).
    - `grep -n "mParentWindow.mainView.GrabAndDisplay(sourceShot)" WPF_Example/UI/ControlItem/InspectionListView.xaml.cs` returns one match (Dedicated mode delegation).
    - Non-Datum grab paths untouched: `grep -n "mParentWindow.mainView.GrabAndDisplay(camParam)" WPF_Example/UI/ControlItem/InspectionListView.xaml.cs` at L371 still present.
    - `grep -n "260423 hbk Phase 11" WPF_Example/UI/ControlItem/InspectionListView.xaml.cs` appears in multiple locations.
    - msbuild Debug/x64 exits 0.
  </acceptance_criteria>
  <done>Selecting a Datum node enables button_grab/button_loadImage; the canvas btn_teachDatum follows the same gating. Grab/Load on a Datum node delegates to the SourceShotName Shot (Dedicated) or reuses cached image (ReuseFromShot), with D-10 hint + abort when no image is available. Non-Datum flows untouched.</done>
</task>

<task type="auto" tdd="false">
  <name>Task 2: MainView.xaml — btn_teachDatum ToggleButton (MinWidth=100) + Separator</name>
  <files>WPF_Example/UI/ContentItem/MainView.xaml</files>
  <read_first>
    - WPF_Example/UI/ContentItem/MainView.xaml — canvas toolbar StackPanel L62-111 (post Plan 02, now has btn_rectRoi / btn_polygonRoi / btn_circleRoi in group 1 + btn_calibrate in group 3). Verify the existing Separator between ROI group and Calibrate.
    - .planning/phases/11-datum-teaching-ui-roi/11-UI-SPEC.md §Component Inventory "btn_teachDatum spec (D-01)" — MinWidth=100 (Teach Datum is 11 chars; 80 is visually cramped)
    - .planning/phases/11-datum-teaching-ui-roi/11-PATTERNS.md §1 "MainView.xaml — toolbar button additions"
  </read_first>
  <action>
    1. In the canvas toolbar StackPanel, insert btn_teachDatum between the ROI-shape group (btn_rectRoi / btn_polygonRoi / btn_circleRoi) and btn_calibrate, with a Separator on each side:
       ```xml
       <!--260423 hbk Phase 11 D-01 — ROI 그룹과 Calibrate 사이에 Teach Datum 그룹 삽입-->
       <Separator Style="{StaticResource {x:Static ToolBar.SeparatorStyleKey}}" Margin="4,4"/>
       <ToggleButton x:Name="btn_teachDatum" Content="Teach Datum"
           Click="TeachDatumButton_Click"
           Style="{StaticResource RoiToggleButtonStyle}"
           IsEnabled="False"
           MinWidth="100" Height="28" Padding="12,4"
           FontSize="13" FontWeight="SemiBold" Margin="0,0,4,0"/>
       <Separator Style="{StaticResource {x:Static ToolBar.SeparatorStyleKey}}" Margin="4,4"/>
       ```
       **MinWidth="100"** (NOT 80) per UI-SPEC.
       If a Separator already exists at the pre-Calibrate position (from Plan 02), keep only one between Teach Datum and Calibrate (don't double-stack).

    2. Do NOT touch btn_rectRoi, btn_polygonRoi, btn_circleRoi, btn_calibrate, label_drawHint, label_pointCount.
    3. XAML comment marker `<!--260423 hbk Phase 11 D-01-->`.
  </action>
  <verify>
    <automated>msbuild WPF_Example/DatumMeasurement.csproj /p:Configuration=Debug /p:Platform=x64 /v:minimal</automated>
  </verify>
  <acceptance_criteria>
    - `grep -n "x:Name=\"btn_teachDatum\"" WPF_Example/UI/ContentItem/MainView.xaml` returns exactly one match.
    - `grep -n "Content=\"Teach Datum\"" WPF_Example/UI/ContentItem/MainView.xaml` returns exactly one match.
    - `grep -n "Click=\"TeachDatumButton_Click\"" WPF_Example/UI/ContentItem/MainView.xaml` returns one match.
    - `grep -n "MinWidth=\"100\"" WPF_Example/UI/ContentItem/MainView.xaml` includes the btn_teachDatum line.
    - `grep -n "260423 hbk Phase 11 D-01" WPF_Example/UI/ContentItem/MainView.xaml` present.
    - msbuild may fail until Task 3 adds the handler — defer build check to Task 3.
  </acceptance_criteria>
  <done>btn_teachDatum placed between ROI-shape group and Calibrate with correct Separator(s), reuses RoiToggleButtonStyle, initial disabled, MinWidth=100 per UI-SPEC.</done>
</task>

<task type="auto" tdd="false">
  <name>Task 3: MainView.xaml.cs — ECanvasMode.TeachDatum + EDatumTeachStep + TeachDatumButton_Click (re-teach confirmation) + HalconViewer_DatumRectCompleted (2-step Rect + auto-invoke TryTeachDatum using halconViewer.CurrentImage) + ResolveDatumSourceShot helpers + ShowDatumTeachHint + DisplayExistingShotImage + Escape snapshot restore</name>
  <files>WPF_Example/UI/ContentItem/MainView.xaml.cs</files>
  <read_first>
    - WPF_Example/UI/ContentItem/MainView.xaml.cs (post Plan 02) — ECanvasMode enum at L41 (currently { None, RectRoi, PolygonRoi, CircleRoi, Calibration } after Plan 02), state fields L42-46, ExitCanvasMode L495-516, RectRoiButton_Click L520-543 (subscribe + StartRectangleDrawing pattern), CalibrateButton_Click L655-665, Escape handling L488-493, DisplayShotImage private method L86 (existing), GrabAndDisplay public method L200 (existing)
    - WPF_Example/UI/ContentItem/MainResultViewerControl.xaml.cs — `public HImage CurrentImage { get; private set; }` at L100. Use `halconViewer.CurrentImage` directly in TryTeachDatum invocation — accessor verified, no new method needed.
    - WPF_Example/UI/ContentItem/MainResultViewerControl.xaml.cs — `SetDatumOverlay(DatumConfig datum, bool isSelected)` signature unchanged; call this after successful teach so Plan 03a's LastTeachSucceeded branch picks up the overlay refresh.
    - WPF_Example/Halcon/Algorithms/DatumFindingService.cs (post Plan 03a) — TryTeachDatum signature `(HImage image, DatumConfig config, out string error)` → bool
    - WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs (post Plan 03a) — SourceShotName + volatile fields visible
    - WPF_Example/UI/Dialog/CustomMessageBox.cs — ShowConfirmation signature (confirm at implementation time)
    - .planning/phases/11-datum-teaching-ui-roi/11-UI-SPEC.md §Interaction Contract "WR-RT-03" 7-step state machine; §Copywriting Contract verbatim Korean strings
  </read_first>
  <action>
    1. Extend ECanvasMode enum at L41:
       ```csharp
       //260423 hbk Phase 11 D-03 — TeachDatum 모드
       private enum ECanvasMode { None, RectRoi, PolygonRoi, CircleRoi, TeachDatum, Calibration }
       ```

    2. Add nested step enum + state fields near the other canvas state fields (L42-46):
       ```csharp
       //260423 hbk Phase 11 D-03 — Datum 티칭 단계
       private enum EDatumTeachStep { Line1, Line2, Done }
       private EDatumTeachStep _datumTeachStep = EDatumTeachStep.Done;
       private ReringProject.Sequence.DatumConfig _editingDatum;

       // Snapshot for Escape revert (UI-SPEC Interaction Contract WR-RT-03 step 7)
       private double _datumSnap_L1Row, _datumSnap_L1Col, _datumSnap_L1Phi, _datumSnap_L1Len1, _datumSnap_L1Len2;
       private double _datumSnap_L2Row, _datumSnap_L2Col, _datumSnap_L2Phi, _datumSnap_L2Len1, _datumSnap_L2Len2;
       private bool _datumSnap_IsConfigured;
       ```

    3. Add TeachDatumButton_Click (mirrors RectRoiButton_Click L520-543 pattern: call ExitCanvasMode first, subscribe RectDrawingCompleted, call StartRectangleDrawing — this is the "subscribe to HalconViewer Rect-drawing events per D-02" path). Includes D-04 re-teach confirmation:
       ```csharp
       //260423 hbk Phase 11 D-01/D-02/D-04 — Datum 티칭 진입 (mirrors RectRoiButton_Click L520-543)
       private void TeachDatumButton_Click(object sender, RoutedEventArgs e)
       {
           if (btn_teachDatum.IsChecked == true) {
               // Resolve selected Datum via the tree (InspectionListView owns the tree; SelectedParam is the canonical accessor)
               var datum = mParentWindow.inspectionList.SelectedParam as ReringProject.Sequence.DatumConfig;
               if (datum == null) {
                   CustomMessageBox.Show("Teach Datum", "Datum 노드를 선택하세요.");
                   btn_teachDatum.IsChecked = false;
                   return;
               }

               // D-04: re-teach confirmation when already configured (UI-SPEC Copywriting Contract verbatim)
               if (datum.IsConfigured) {
                   var res = CustomMessageBox.ShowConfirmation("Datum 재티칭",
                       "기존 Datum 설정을 덮어씁니다.\n\nLine1/Line2 ROI와 RefOrigin/RefAngle이 초기화됩니다.\n계속하시겠습니까?",
                       System.Windows.MessageBoxButton.OKCancel);
                   if (res != System.Windows.MessageBoxResult.OK) {
                       btn_teachDatum.IsChecked = false;
                       return;
                   }
                   SnapshotDatum(datum);
                   ClearDatumFields(datum);
               } else {
                   SnapshotDatum(datum); // zeroed snapshot equivalent for Escape revert
               }

               ExitCanvasMode();
               _canvasMode = ECanvasMode.TeachDatum;
               _datumTeachStep = EDatumTeachStep.Line1;
               _editingDatum = datum;
               btn_teachDatum.IsChecked = true;

               label_drawHint.Content = "Line1 ROI를 드래그로 지정하세요"; // UI-SPEC verbatim
               label_drawHint.Foreground = new System.Windows.Media.SolidColorBrush(
                   (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FFAAAAAA"));
               label_drawHint.Visibility = Visibility.Visible;

               halconViewer.RectDrawingCompleted += HalconViewer_DatumRectCompleted;
               halconViewer.StartRectangleDrawing();
           }
           else {
               ExitCanvasMode();
           }
       }
       ```

    4. Add HalconViewer_DatumRectCompleted — 2-step Rect capture with auto-invoke TryTeachDatum on step=Line2 MouseUp (D-02). Uses `halconViewer.CurrentImage` (verified PUBLIC accessor at MainResultViewerControl.xaml.cs:100):
       ```csharp
       //260423 hbk Phase 11 D-02 — 2단계 Rect 캡처 + 두 번째 MouseUp 시 자동 TryTeachDatum
       private void HalconViewer_DatumRectCompleted(object sender, EventArgs e)
       {
           if (_canvasMode != ECanvasMode.TeachDatum || _editingDatum == null) return;

           var roi = halconViewer.CommitActiveRectangle();
           if (roi == null) return;

           // Convert Rect corners → center/half-extents/phi=0 (same as CommitRectRoi L551-579)
           double cr = (roi.Row1 + roi.Row2) / 2.0;
           double cc = (roi.Column1 + roi.Column2) / 2.0;
           double hh = System.Math.Abs(roi.Row2 - roi.Row1) / 2.0;
           double hw = System.Math.Abs(roi.Column2 - roi.Column1) / 2.0;

           if (_datumTeachStep == EDatumTeachStep.Line1) {
               _editingDatum.Line1_Row = cr;
               _editingDatum.Line1_Col = cc;
               _editingDatum.Line1_Phi = 0.0;
               _editingDatum.Line1_Length1 = hh;
               _editingDatum.Line1_Length2 = hw;
               _datumTeachStep = EDatumTeachStep.Line2;

               label_drawHint.Content = "Line2 ROI를 드래그로 지정하세요"; // UI-SPEC verbatim
               halconViewer.StartRectangleDrawing(); // re-arm for Line2 (stays subscribed to the event)
               return;
           }

           if (_datumTeachStep == EDatumTeachStep.Line2) {
               _editingDatum.Line2_Row = cr;
               _editingDatum.Line2_Col = cc;
               _editingDatum.Line2_Phi = 0.0;
               _editingDatum.Line2_Length1 = hh;
               _editingDatum.Line2_Length2 = hw;
               _datumTeachStep = EDatumTeachStep.Done;

               //260423 hbk Phase 11 D-02 — 두 번째 ROI MouseUp 확정 순간 자동 TryTeachDatum
               // halconViewer.CurrentImage is the VERIFIED public accessor at MainResultViewerControl.xaml.cs:100
               HalconDotNet.HImage img = halconViewer.CurrentImage;
               string error;
               bool ok = false;
               if (img != null) {
                   ok = new ReringProject.Halcon.Algorithms.DatumFindingService()
                       .TryTeachDatum(img, _editingDatum, out error);
               } else {
                   error = "이미지가 없습니다";
               }

               if (ok) {
                   // D-11 success: green hint + overlay refresh (Plan 03a's LastTeachSucceeded branch picks it up)
                   label_drawHint.Content = "Datum 티칭 완료 — Recipe Save 권장"; // UI-SPEC verbatim
                   label_drawHint.Foreground = new System.Windows.Media.SolidColorBrush(
                       (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FF4ADE80"));
                   halconViewer.SetDatumOverlay(_editingDatum, isSelected: true);
               } else {
                   // D-12 failure: red hint, keep ROI rectangles drawn for repositioning
                   label_drawHint.Content = "Datum 티칭 실패: " + (error ?? "unknown");
                   label_drawHint.Foreground = new System.Windows.Media.SolidColorBrush(
                       (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FFF87171"));
               }

               btn_teachDatum.IsChecked = false;
               _canvasMode = ECanvasMode.None; // but do NOT call ExitCanvasMode — keep overlay visible + snapshot intact
               halconViewer.RectDrawingCompleted -= HalconViewer_DatumRectCompleted;
           }
       }
       ```

    5. Add helpers (Snapshot / Restore / Clear) + ShowDatumTeachHint + DisplayExistingShotImage:
       ```csharp
       private void SnapshotDatum(ReringProject.Sequence.DatumConfig d) {
           _datumSnap_L1Row = d.Line1_Row; _datumSnap_L1Col = d.Line1_Col; _datumSnap_L1Phi = d.Line1_Phi;
           _datumSnap_L1Len1 = d.Line1_Length1; _datumSnap_L1Len2 = d.Line1_Length2;
           _datumSnap_L2Row = d.Line2_Row; _datumSnap_L2Col = d.Line2_Col; _datumSnap_L2Phi = d.Line2_Phi;
           _datumSnap_L2Len1 = d.Line2_Length1; _datumSnap_L2Len2 = d.Line2_Length2;
           _datumSnap_IsConfigured = d.IsConfigured;
       }

       private void RestoreDatumSnapshot(ReringProject.Sequence.DatumConfig d) {
           if (d == null) return;
           d.Line1_Row = _datumSnap_L1Row; d.Line1_Col = _datumSnap_L1Col; d.Line1_Phi = _datumSnap_L1Phi;
           d.Line1_Length1 = _datumSnap_L1Len1; d.Line1_Length2 = _datumSnap_L1Len2;
           d.Line2_Row = _datumSnap_L2Row; d.Line2_Col = _datumSnap_L2Col; d.Line2_Phi = _datumSnap_L2Phi;
           d.Line2_Length1 = _datumSnap_L2Len1; d.Line2_Length2 = _datumSnap_L2Len2;
           d.IsConfigured = _datumSnap_IsConfigured;
       }

       private void ClearDatumFields(ReringProject.Sequence.DatumConfig d) {
           d.Line1_Row = 0; d.Line1_Col = 0; d.Line1_Phi = 0; d.Line1_Length1 = 0; d.Line1_Length2 = 0;
           d.Line2_Row = 0; d.Line2_Col = 0; d.Line2_Phi = 0; d.Line2_Length1 = 0; d.Line2_Length2 = 0;
           d.IsConfigured = false;
           d.LastTeachSucceeded = false;
       }

       //260423 hbk Phase 11 D-10 — 외부(InspectionListView)에서 호출하는 Datum 힌트 표시
       internal void ShowDatumTeachHint(string message, bool isError)
       {
           label_drawHint.Content = message;
           label_drawHint.Foreground = new System.Windows.Media.SolidColorBrush(
               (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(
                   isError ? "#FFF87171" : "#FFAAAAAA"));
           label_drawHint.Visibility = Visibility.Visible;
       }

       //260423 hbk Phase 11 D-07 — ReuseFromShot: Shot의 캐시 이미지를 캔버스에 표시 (기존 private DisplayShotImage L86 공개 래퍼)
       internal void DisplayExistingShotImage(ReringProject.Sequence.ShotConfig shot)
       {
           DisplayShotImage(shot); // existing private at L86; wraps HImage dispatch to halconViewer.LoadImage
       }
       ```

    6. Extend ExitCanvasMode (L495-516) to clean up TeachDatum. Insert the new cleanup block alongside existing Rect/Polygon/Calibration cleanup:
       ```csharp
       //260423 hbk Phase 11 D-07 — TeachDatum 모드 정리 (Escape 포함). UI-SPEC Interaction Contract WR-RT-03 step 7
       if (_canvasMode == ECanvasMode.TeachDatum) {
           halconViewer.RectDrawingCompleted -= HalconViewer_DatumRectCompleted;
           if (_datumTeachStep != EDatumTeachStep.Done && _editingDatum != null) {
               RestoreDatumSnapshot(_editingDatum); // Escape during teach → revert snapshot
           }
       }
       _datumTeachStep = EDatumTeachStep.Done;
       _editingDatum = null;
       btn_teachDatum.IsChecked = false;
       ```
       Place these lines alongside the existing cleanup (don't break Rect/Polygon/Circle cleanup that Plan 02 installed).

    7. Do NOT touch Rect/Polygon/Circle handlers except for the ExitCanvasMode additions. Do NOT break Plan 02's CircleRoi path.

    8. Brace style: K&R (UI code). C# 7.2 only (no switch expressions, no records).
    9. `//260423 hbk Phase 11` marker on every new block.
  </action>
  <verify>
    <automated>msbuild WPF_Example/DatumMeasurement.csproj /p:Configuration=Debug /p:Platform=x64 /v:minimal</automated>
  </verify>
  <acceptance_criteria>
    - `grep -n "ECanvasMode.TeachDatum" WPF_Example/UI/ContentItem/MainView.xaml.cs` returns at least 3 matches.
    - `grep -n "enum EDatumTeachStep" WPF_Example/UI/ContentItem/MainView.xaml.cs` returns exactly one match with `{ Line1, Line2, Done }`.
    - `grep -n "private void TeachDatumButton_Click" WPF_Example/UI/ContentItem/MainView.xaml.cs` returns one match.
    - `grep -n "private void HalconViewer_DatumRectCompleted" WPF_Example/UI/ContentItem/MainView.xaml.cs` returns one match.
    - **Warning 8 fix:** `grep -n "halconViewer.CurrentImage" WPF_Example/UI/ContentItem/MainView.xaml.cs` returns at least one match (concrete public accessor — no placeholder comment).
    - `grep -n "TryTeachDatum(img, _editingDatum, out error)" WPF_Example/UI/ContentItem/MainView.xaml.cs` returns one match.
    - `grep -n "Datum 재티칭" WPF_Example/UI/ContentItem/MainView.xaml.cs` returns one match (UI-SPEC verbatim title).
    - `grep -n "기존 Datum 설정을 덮어씁니다" WPF_Example/UI/ContentItem/MainView.xaml.cs` returns one match.
    - `grep -n "Line1 ROI를 드래그로 지정하세요" WPF_Example/UI/ContentItem/MainView.xaml.cs` returns one match.
    - `grep -n "Line2 ROI를 드래그로 지정하세요" WPF_Example/UI/ContentItem/MainView.xaml.cs` returns one match.
    - `grep -n "Datum 티칭 완료 — Recipe Save 권장" WPF_Example/UI/ContentItem/MainView.xaml.cs` returns one match.
    - `grep -n "Datum 티칭 실패:" WPF_Example/UI/ContentItem/MainView.xaml.cs` returns one match.
    - `grep -c "#FF4ADE80\\|#FFF87171\\|#FFAAAAAA" WPF_Example/UI/ContentItem/MainView.xaml.cs` returns at least 3 distinct hex matches.
    - `grep -n "internal void ShowDatumTeachHint" WPF_Example/UI/ContentItem/MainView.xaml.cs` returns one match.
    - `grep -n "internal void DisplayExistingShotImage" WPF_Example/UI/ContentItem/MainView.xaml.cs` returns one match.
    - `grep -n "RestoreDatumSnapshot\\|SnapshotDatum\\|ClearDatumFields" WPF_Example/UI/ContentItem/MainView.xaml.cs` shows all 3 helpers present.
    - Rect/Polygon/Circle handlers textually unchanged — `grep -c "RectRoiButton_Click\\|PolygonRoiButton_Click\\|CircleRoiButton_Click" WPF_Example/UI/ContentItem/MainView.xaml.cs` unchanged vs pre-edit.
    - `grep -n "260423 hbk Phase 11" WPF_Example/UI/ContentItem/MainView.xaml.cs` appears in multiple locations.
    - msbuild Debug/x64 exits 0.
  </acceptance_criteria>
  <done>Clicking btn_teachDatum on a Datum node (with image loaded) enters ECanvasMode.TeachDatum step=Line1, shows info hint, lets user drag Line1, advances to Line2, drags Line2, auto-invokes TryTeachDatum with halconViewer.CurrentImage. Success → green hint + SetDatumOverlay refresh (Plan 03a renders the yellow/cyan/red detected overlay). Failure → red hint, ROIs retained. Re-teach on configured Datum shows CustomMessageBox confirmation. Escape reverts snapshot. Build passes.</done>
</task>

<task type="checkpoint:human-verify" gate="blocking">
  <name>Task 4: SIMUL_MODE UAT — Datum teaching end-to-end + Warning-5 regression</name>
  <what-built>
    Complete Datum teaching UI — the 🔴 Blocker WR-RT-03 resolution. First UI caller of TryTeachDatum. The UAT also verifies Warning 5: existing Phase 4 post-teach palette (cyan/blue/magenta) remains visible and UNCHANGED.
  </what-built>
  <files>N/A (human verification task — no file edits)</files>
  <action>Pause execution. Human operator performs the verification steps enumerated in `<how-to-verify>` below. Execution resumes when operator provides the resume signal.</action>
  <verify>
    <automated>MISSING — human verification checkpoint (see how-to-verify for manual steps)</automated>
  </verify>
  <done>All `<how-to-verify>` checks pass and operator types the resume signal.</done>
  <how-to-verify>
    1. Launch Debug/x64 with SIMUL_MODE. Load a recipe with a Sequence containing ≥1 DatumConfig and ≥1 ShotConfig with a SimulImagePath containing visible edges suitable for Line1/Line2 detection.
    2. Select the Datum node in InspectionListView. Confirm:
       - button_grab, button_loadImage become ENABLED.
       - button_light remains DISABLED (DatumConfig is not ICameraParam).
       - btn_teachDatum on canvas toolbar becomes ENABLED.
    3. Check DatumConfig.SourceShotName in PropertyGrid; if empty, set to an existing Shot name. (Empty → fallback to Shots[0] per D-08 is also a valid test.)
    4. Click button_grab. Confirm:
       - Image from SourceShotName Shot's SimulImagePath loads onto canvas (Dedicated mode).
       - If ImageSourceMode=ReuseFromShot and the Shot has never been grabbed → label_drawHint red "Shot '{name}'을 먼저 Grab하세요" and no image loads (D-10).
       - If SourceShotName points to a non-existent Shot → label_drawHint red "SourceShotName '{name}'을 찾을 수 없습니다" (D-10).
    5. With an image loaded, click btn_teachDatum. Confirm:
       - Button becomes checked (blue accent).
       - label_drawHint info-gray "Line1 ROI를 드래그로 지정하세요".
    6. Drag a Line1 rectangle over a clear edge. Release. Confirm:
       - Line1 rectangle rendered (existing cyan-per-Phase-4 or new yellow — whichever the current render path chose during drawing; the ROI persists visually).
       - label_drawHint → "Line2 ROI를 드래그로 지정하세요".
    7. Drag a Line2 rectangle. Release. Confirm ONE of:
       - **Success:** label_drawHint green "Datum 티칭 완료 — Recipe Save 권장". Canvas shows:
         * The existing Phase 4 cyan Line1 ROI rect (@L345) + blue Line2 ROI rect (@L357) + magenta RefOrigin cross — **UNCHANGED (Warning 5)**.
         * PLUS the new detected Line1 (yellow) + detected Line2 (cyan) + red 20px intersection cross — layered additively.
       - **Failure:** label_drawHint red "Datum 티칭 실패: {halcon error}"; both ROI rectangles remain drawn for repositioning.
    8. PropertyGrid: DatumConfig.IsConfigured=true on success; Line1/2_Row/Col/Phi/Length1/Length2 reflect drawn rectangles; RefOriginRow/Col + RefAngleRad populated; LastTeachSucceeded=true.
    9. Click btn_teachDatum again on the now-taught Datum. Confirm:
       - CustomMessageBox title "Datum 재티칭", body "기존 Datum 설정을 덮어씁니다…계속하시겠습니까?", OK/Cancel.
       - Cancel → un-checks, no state change.
       - Click btn_teachDatum again, OK → previous fields cleared, teach re-enters step=Line1.
    10. During step=Line1 or Line2, press Escape. Confirm ROIs restored to pre-teach snapshot, mode exits, overlay shows original IsConfigured state.
    11. Select a non-Datum node. Confirm btn_teachDatum becomes DISABLED.
    12. **Warning 5 regression check:** After a successful teach, deselect and reselect the Datum node. Confirm the existing Phase 4 post-teach display (cyan Line1 ROI rect + blue Line2 ROI rect + magenta RefOrigin cross) still appears alongside the new yellow/cyan detected-line + red intersection cross. Both palettes must coexist.
    13. Save recipe; relaunch app; reload recipe. Confirm:
       - DatumConfig.SourceShotName and IsConfigured=true persisted.
       - LastTeachSucceeded=false on cold load (volatile field zero-value) — the new detected-line overlay does NOT appear until next successful TryTeachDatum invocation (expected per D-11 volatile semantics).
       - Old Phase 4 cyan/blue/magenta overlay DOES appear (persistent, loaded from IsConfigured=true).
    14. Load an OLD Phase 6/7 recipe without SourceShotName. Confirm:
       - Recipe loads without exception.
       - DatumConfig.SourceShotName reads as "".
       - No regression in FAI/Rect/Circle flows.
  </how-to-verify>
  <resume-signal>Type "approved" if all 14 steps pass. Otherwise describe failing step + expected vs observed + any console/log output.</resume-signal>
</task>

</tasks>

<threat_model>
## Trust Boundaries

| Boundary | Description |
|----------|-------------|
| Canvas mouse drag → Line1/Line2 ROI state | Trusted local UI input. |
| halconViewer.CurrentImage → TryTeachDatum | Trusted local call. HImage disposal handled by MainResultViewerControl owner. |
| InspectionRecipeManager.Shots lookup by SourceShotName | Operator-authored string; could reference a non-existent Shot. |
| ExitCanvasMode snapshot restore | Local state; no external actor. |

## STRIDE Threat Register

| Threat ID | Category | Component | Disposition | Mitigation Plan |
|-----------|----------|-----------|-------------|-----------------|
| T-11-03b-01 | Tampering | SourceShotName references non-existent Shot | mitigate | ResolveDatumSourceShot returns null → ShowDatumTeachHint error + abort grab. No unhandled exception. Empty SourceShotName → Shots[0] fallback (D-08). |
| T-11-03b-02 | Denial of Service | halconViewer.CurrentImage null mid-teach | mitigate | HalconViewer_DatumRectCompleted null-checks img before TryTeachDatum; on null sets error="이미지가 없습니다" and follows the failure path. No NullReferenceException. |
| T-11-03b-03 | Denial of Service | TryTeachDatum throws on malformed image | mitigate | Plan 03a Task 2 adds config.LastTeachSucceeded=false on the outer catch; the UI failure path handles the `out error` message. No crash. |
| T-11-03b-04 | Tampering | User rapid-clicks btn_teachDatum during teach | mitigate | ExitCanvasMode is idempotent and restores snapshot when _datumTeachStep != Done. Second click on IsConfigured==false re-enters step=Line1 (acceptable per UI-SPEC retry path). |
| T-11-03b-05 | Repudiation | User claims teach not overwritten after re-teach | mitigate | D-04 modal requires explicit OK; Snapshot taken before Clear; Cancel preserves state. No silent overwrite. |
| T-11-03b-06 | Spoofing | Wrong Sequence resolved for ResolveDatumSourceShot | mitigate | Uses the selected NodeViewModel's SequenceID (canonical key set at L263 via SetParam). If SystemHandler.Handle.seqHandler returns null → returns null → D-10 hint + abort. |
| T-11-03b-07 | Information Disclosure | label_drawHint shows raw Halcon `out error` string | accept | Halcon error strings are diagnostic, not sensitive. Matches existing project convention (Phase 4). |
| T-11-03b-08 | Elevation of Privilege | N/A | accept | No privilege boundary. |
</threat_model>

<verification>
- Debug/x64 build green for Tasks 1-3.
- SIMUL_MODE UAT (Task 4) passes all 14 checks — MANDATORY Phase 11 blocker acceptance gate.
- Warning 5 regression: existing cyan/blue/magenta post-teach palette STILL visible on reselect, alongside new yellow/cyan/red detected-line overlay.
- Existing Phase 6/7 INI recipes load without migration (SourceShotName defaults to "").
- Plan 02 Circle ROI flow unregressed (verified in Task 4 step 14).
</verification>

<success_criteria>
- WR-RT-03 resolved end-to-end: user teaches a Datum from the UI (grab image → click btn_teachDatum → drag Line1 → drag Line2 → auto-TryTeachDatum → overlay + success/failure feedback).
- Re-teach, Escape-cancel, failure-retry flows work per UI-SPEC.
- ResolveDatumSourceShot correctly resolves SourceShotName against InspectionRecipeManager.Shots with D-08 fallback + D-10 abort-on-miss.
- halconViewer.CurrentImage is the verified public accessor used for TryTeachDatum invocation (no guesswork).
- Warning 5 scope guard verified via UAT step 12.
- Backward-compat INI verified via UAT step 14.
</success_criteria>

<output>
After completion, create `.planning/phases/11-datum-teaching-ui-roi/11-03b-SUMMARY.md` documenting:
- Exact line-number insertions per file
- Confirmation of the InspectionRecipeManager accessor name actually used (public property vs new pass-through)
- Whether ShotConfig.GetImage() / Name accessor names matched the plan or needed adjustment
- SIMUL_MODE UAT transcript from Task 4 — SimulImagePath used, Shot names, success/failure examples
- Warning 5 regression evidence: screenshots or written confirmation that cyan/blue/magenta post-teach palette still renders
- Confirmation that bugs.md WR-RT-03 can be moved to Fixed on phase completion (Plan 04 final task will do the move)
</output>
</content>
</invoke>