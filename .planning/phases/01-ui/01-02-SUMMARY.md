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

- **Duration:** 6 min
- **Started:** 2026-04-07T07:04:00Z
- **Completed:** 2026-04-07T07:09:34Z
- **Tasks:** 2 of 3 (Task 3 = checkpoint:human-verify, awaiting user approval)
- **Files modified:** 7

## Accomplishments

- ENodeType.FAI added to Node.cs; InspectionListViewModel now creates FAI child nodes under ShotConfig-backed Action nodes at startup
- InspectionViewModel fully rewritten: removed Shot layer, FAI-centric with OnFAISelected/OnActionSelected/AddFAI/RemoveFAI methods
- InspectionListView.xaml has 3 new FAI CRUD toolbar buttons; xaml.cs handles FAI selection (enables CRUD buttons, calls DisplayFAIImage + OnFAISelected) and CRUD events (AddFAI with name dialog, RemoveFAI with confirmation per D-10, RenameFAI)
- MainView.xaml replaced 3-column TreeView layout with 2-row canvas+DataGrid layout; MainView.xaml.cs removed all TreeView-related code, added DisplayFAIImage and SetFAIResultSource

## Task Commits

Each task was committed atomically:

1. **Task 1: InspectionViewModel rewrite + InspectionListView FAI CRUD** - `edaafa8` (feat)
2. **Task 2: MainView simplification + DisplayFAIImage + SetFAIResultSource** - `517f75d` (feat)
3. **Task 3: Visual verification** - Awaiting user checkpoint approval

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

---

**Total deviations:** 2 auto-fixed (2 Rule 1 bugs)
**Impact on plan:** Both fixes essential for correctness. No scope creep. Plan's interface block was aspirational/incorrect; actual codebase architecture (FAI images live on ShotConfig) was followed.

## Known Stubs

None — all data flows use real InspectionRecipeManager/ShotConfig/FAIConfig objects. DataGrid shows "--" (via FAIResultRow.JudgeText/MeasuredValueText) for unmeasured FAIs which is correct behavior.

## Issues Encountered

None beyond the two auto-fixed bugs described above.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- InspectionListView tree will show FAI nodes whenever InspectionRecipeManager has Shots with FAIs (requires recipe load or CRUD)
- PropertyGrid automatically shows FAIConfig properties when FAI node selected (existing `SelectedItem.Param` binding)
- DataGrid binds to FAIResults collection, updates on FAI/Action selection
- Awaiting user verification (Task 3 checkpoint) before marking plan complete

---
*Phase: 01-ui*
*Completed: 2026-04-07*
