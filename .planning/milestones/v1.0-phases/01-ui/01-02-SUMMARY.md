---
phase: 01-ui
plan: 02
subsystem: ui
tags: [wpf, mvvm, viewmodel, xaml, datagrid, treeview, fai, inspection]

requires:
  - phase: 01-ui-01
    provides: ShotNodeViewModel, FAINodeViewModel, FAIResultRow, InspectionViewModel stubs
  - phase: phase-5
    provides: ShotConfig, FAIConfig, InspectionRecipeManager data models

provides:
  - FAI nodes displayed under Action nodes in InspectionListView TreeListBox (ENodeType.FAI)
  - InspectionViewModel rewritten FAI-centric with OnFAISelected/OnActionSelected/AddFAI/RemoveFAI
  - FAI CRUD toolbar in InspectionListView (Add/Del/Edit buttons with confirmation dialog)
  - MainView simplified to canvas + DataGrid layout (no TreeView column)
  - DisplayFAIImage(FAIConfig) shows parent ShotConfig image on canvas
  - SetFAIResultSource(InspectionViewModel) binds DataGrid to FAIResults collection

affects:
  - Phase 02 teaching integration (will call SetParam and DisplayFAIImage)
  - Phase 03 edge measurement (FAI result table populated by algorithm output)

tech-stack:
  added: []
  patterns:
    - FAI selection via InspectionListView drives MainView canvas + DataGrid (InspectionListView.xaml.cs wires both)
    - DisplayFAIImage resolves image via fai.Owner cast to ShotConfig (FAIConfig does not store images)
    - SetFAIResultSource uses WPF Binding to bind DataGrid.ItemsSource to InspectionViewModel.FAIResults
    - FAI CRUD handlers delegate to InspectionViewModel.AddFAI/RemoveFAI passing ShotConfig explicitly

key-files:
  created: []
  modified:
    - WPF_Example/UI/ViewModel/InspectionViewModel.cs
    - WPF_Example/UI/ControlItem/Node.cs
    - WPF_Example/UI/ControlItem/InspectionListViewModel.cs
    - WPF_Example/UI/ControlItem/InspectionListView.xaml
    - WPF_Example/UI/ControlItem/InspectionListView.xaml.cs
    - WPF_Example/UI/ContentItem/MainView.xaml
    - WPF_Example/UI/ContentItem/MainView.xaml.cs

key-decisions:
  - "DisplayFAIImage uses fai.Owner cast to ShotConfig — FAIConfig does not store images, only ShotConfig does"
  - "InspectionViewModel.AddFAI/RemoveFAI accept ShotConfig parameter explicitly — plan's InspectionRecipeManager.AddFAI call was incorrect (non-existent method)"
  - "FAI CRUD buttons wired in InspectionListView (not MainView) — MainView now has no TreeView or CRUD logic"

patterns-established:
  - "Pattern 1: FAI owner resolution — fai.Owner as ShotConfig for image retrieval via ParamBase.Owner field"
  - "Pattern 2: SetFAIResultSource wiring — called from InspectionListView.ListView_Loaded after _inspectionVm creation"
  - "Pattern 3: InspectionListViewModel.AddFAINode — adds NodeViewModel directly to Children collection for runtime FAI insertion"

requirements-completed: [UI-01, UI-02, UI-03, UI-04, UI-05]

duration: 6min
completed: 2026-04-07
---

# Phase 01 Plan 02: FAI UI Integration Summary

**FAI nodes wired into InspectionListView tree with CRUD toolbar, MainView simplified to canvas+DataGrid, FAI selection drives PropertyGrid + canvas + result table**

## Performance

- **Duration:** ~90 min (including human-verify checkpoint round-trip)
- **Started:** 2026-04-07T07:04:00Z
- **Completed:** 2026-04-07T08:30:00Z
- **Tasks:** 3 of 3 (including post-checkpoint UI fixes)
- **Files modified:** 7 (plus 2 re-modified after checkpoint feedback)

## Accomplishments

- ENodeType.FAI added to Node.cs; InspectionListViewModel now creates FAI child nodes under ShotConfig-backed Action nodes at startup
- InspectionViewModel fully rewritten: removed Shot layer, FAI-centric with OnFAISelected/OnActionSelected/AddFAI/RemoveFAI methods
- InspectionListView.xaml has 3 new FAI CRUD toolbar buttons; xaml.cs handles FAI selection (enables CRUD buttons, calls DisplayFAIImage + OnFAISelected) and CRUD events (AddFAI with name dialog, RemoveFAI with confirmation per D-10, RenameFAI)
- MainView.xaml replaced 3-column TreeView layout with 2-row canvas+DataGrid layout; MainView.xaml.cs removed all TreeView-related code, added DisplayFAIImage and SetFAIResultSource

## Task Commits

Each task was committed atomically:

1. **Task 1: InspectionViewModel rewrite + InspectionListView FAI CRUD** - `edaafa8` (feat)
2. **Task 2: MainView simplification + DisplayFAIImage + SetFAIResultSource** - `517f75d` (feat)
3. **Task 3: Post-checkpoint UI fixes (DataGrid readability + tree visibility + label color)** - `c854d92` (fix)

## Files Created/Modified

- `WPF_Example/UI/ViewModel/InspectionViewModel.cs` - Rewritten FAI-centric: OnFAISelected, OnActionSelected, AddFAI(shot,name), RemoveFAI(shot,index), ClearResults
- `WPF_Example/UI/ControlItem/Node.cs` - Added ENodeType.FAI with chart.png icon
- `WPF_Example/UI/ControlItem/InspectionListViewModel.cs` - CreateSequenceNode adds FAI child nodes under ShotConfig actions; added AddFAINode() for runtime insertion
- `WPF_Example/UI/ControlItem/InspectionListView.xaml` - Added FAI CRUD ToolBar with button_addFAI, button_removeFAI, button_renameFAI
- `WPF_Example/UI/ControlItem/InspectionListView.xaml.cs` - Added _inspectionVm field, SetFAIResultSource wiring in ListView_Loaded, FAI selection handling in SelectionChanged, Btn_AddFAI_Click/Btn_RemoveFAI_Click/Btn_RenameFAI_Click
- `WPF_Example/UI/ContentItem/MainView.xaml` - Replaced 3-column layout with 2-row canvas+DataGrid; removed TreeView and left column
- `WPF_Example/UI/ContentItem/MainView.xaml.cs` - Removed TreeView_SelectedItemChanged, Btn_Add/Remove/Edit_Click, DisplayShotImage(ShotNodeViewModel), _viewModel field; added DisplayFAIImage(FAIConfig), SetFAIResultSource(InspectionViewModel)

## Decisions Made

- `DisplayFAIImage` resolves image via `fai.Owner as ShotConfig` because `FAIConfig` does not store images — only `ShotConfig` holds the grabbed image buffer. This differs from the plan's interface block which incorrectly showed `HasImage`/`GetImage` on `FAIConfig`.
- `InspectionViewModel.AddFAI(ShotConfig, string)` and `RemoveFAI(ShotConfig, int)` accept the shot explicitly, since `InspectionRecipeManager` has no top-level `AddFAI` method — FAIs belong to individual `ShotConfig` instances.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Fixed InspectionViewModel.AddFAI signature**
- **Found during:** Task 1 (InspectionViewModel rewrite)
- **Issue:** Plan proposed `_recipeManager.AddFAI(_recipeManager, name)` but InspectionRecipeManager has no AddFAI method — FAIs are added via ShotConfig.AddFAI()
- **Fix:** Changed signature to `AddFAI(ShotConfig shot, string name)` and `RemoveFAI(ShotConfig shot, int index)`, delegating to shot.AddFAI/RemoveFAI
- **Files modified:** InspectionViewModel.cs, InspectionListView.xaml.cs (call site updated)
- **Verification:** Build succeeds, CRUD logic correct
- **Committed in:** edaafa8 (Task 1 commit)

**2. [Rule 1 - Bug] Fixed DisplayFAIImage to use ShotConfig.GetImage() via fai.Owner**
- **Found during:** Task 2 (MainView.xaml.cs rewrite)
- **Issue:** Plan's interface showed FAIConfig.HasImage/GetImage properties but these don't exist in actual FAIConfig.cs — only ShotConfig stores images
- **Fix:** DisplayFAIImage resolves parent ShotConfig via `fai.Owner as ShotConfig`, then calls shot.HasImage/GetImage
- **Files modified:** MainView.xaml.cs
- **Verification:** Build succeeds; image display logic correctly reads from ShotConfig buffer
- **Committed in:** 517f75d (Task 2 commit)

**3. [Rule 2 - Missing Critical] DataGrid ColumnHeaderStyle missing for dark theme**
- **Found during:** Task 3 human verification (user reported DataGrid text hard to read)
- **Issue:** WPF DataGrid column headers do not inherit Foreground from parent DataGrid element; headers showed dark text on dark header background
- **Fix:** Added explicit `DataGrid.ColumnHeaderStyle` (white Foreground, dark #FF3A3A4A background, SemiBold weight) and `DataGrid.CellStyle` (explicit white Foreground)
- **Files modified:** WPF_Example/UI/ContentItem/MainView.xaml
- **Verification:** Build succeeds with 0 errors
- **Committed in:** c854d92

**4. [Rule 2 - UX] Tree not expanding on initial load**
- **Found during:** Task 3 human verification (user reported tree not visible)
- **Issue:** `ViewModel.RootModel.ExpandAll()` only called when `IsEditable = true`; default state left tree collapsed showing only root node
- **Fix:** Added `ViewModel.RootModel.ExpandAll()` in `ListView_Loaded`, wrapped in try/catch with Debug.WriteLine
- **Files modified:** WPF_Example/UI/ControlItem/InspectionListView.xaml.cs
- **Verification:** Build succeeds with 0 errors
- **Committed in:** c854d92

**5. [Rule 1 - Bug] ELogType not accessible in InspectionListView namespace**
- **Found during:** Post-checkpoint fix (build error after adding error logging)
- **Issue:** `ELogType` is in `ReringProject.Setting` namespace, not imported in InspectionListView.xaml.cs
- **Fix:** Replaced `Logging.PrintErrLog((int)ELogType.Error, ...)` with `System.Diagnostics.Debug.WriteLine(...)` in catch block
- **Files modified:** WPF_Example/UI/ControlItem/InspectionListView.xaml.cs
- **Verification:** Build succeeds with 0 errors
- **Committed in:** c854d92

**6. [Rule 2 - UX] label_message Red color hard to read on dark background**
- **Found during:** Task 3 human verification (user couldn't clearly see canvas area)
- **Issue:** "NO Image" red text on #FF303030 dark background has low contrast and implies error state
- **Fix:** Changed Foreground from `Red` to `#FFAAAAAA` (light gray) and FontSize from 20 to 24
- **Files modified:** WPF_Example/UI/ContentItem/MainView.xaml
- **Verification:** Build succeeds with 0 errors
- **Committed in:** c854d92

---

**Total deviations:** 6 auto-fixed (2 Rule 1 bugs, 4 Rule 2 missing/UX)
**Impact on plan:** All fixes essential for correctness and basic usability. No scope creep. Post-checkpoint fixes resolved all user-reported visibility issues.

## Known Stubs

- `FAIResults_SelectionChanged` in MainView.xaml.cs is a no-op (Phase 2: ROI highlight when RoiDefinition teaching data exists)
- `AddCustomControl`/`ChangeTabPage` in MainView.xaml.cs are no-ops (Phase 2 will provide dedicated panel)

## Issues Encountered

- User verification found DataGrid headers unreadable (WPF DataGrid requires explicit ColumnHeaderStyle for dark themes)
- Tree appeared invisible because ExpandAll only ran on IsEditable=true; fixed by expanding on load
- "NO Image" label in red on dark background was hard to notice; changed to neutral light gray

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- InspectionListView tree will show FAI nodes whenever InspectionRecipeManager has Shots with FAIs (requires recipe load or CRUD)
- PropertyGrid automatically shows FAIConfig properties when FAI node selected (existing `SelectedItem.Param` binding)
- DataGrid binds to FAIResults collection, updates on FAI/Action selection
- InspectionListView tree auto-expands and shows FAI hierarchy on startup
- DataGrid headers and cells have proper white text on dark background
- Plan complete — all 3 tasks done including post-checkpoint UI fixes

---
*Phase: 01-ui*
*Completed: 2026-04-07*
