---
phase: 02-teaching-calibration
plan: 01
subsystem: ui
tags: [halcon, roi, fai, wpf, rendering, calibration]

requires:
  - phase: 01-ui
    provides: FAIResultRow, InspectionViewModel, DataGrid, halconViewer (MainResultViewerControl), HalconDisplayService

provides:
  - FAIConfig.ToRoiDefinition() converting Rectangle2 center+half-lengths+phi to RoiDefinition bounding box
  - FAIConfig.PolygonPoints string field (INI serialized)
  - FAIConfig.PixelResolutionX/Y fields (INI serialized)
  - CameraSlaveParam.PixelResolution field (mm/pixel, INI serialized)
  - FAIResultRow.SourceFAI property exposing underlying FAIConfig
  - MainResultViewerControl.UpdateDisplayState(rois, selectedRoiId, overlays, messages) overload
  - HalconDisplayService.DrawDirectionArrow — white arrow at selected ROI center
  - MainView.FAIResults_SelectionChanged wired — DataGrid FAI row selection triggers ROI highlight
  - MainView.GetCurrentFAIRois — helper collecting all taught RoiDefinitions from DataGrid

affects: [02-02, plan-03, plan-04]

tech-stack:
  added: []
  patterns:
    - "FAIConfig.ToRoiDefinition(): Rectangle2 center+half-lengths+phi → axis-aligned bounding box via sin/cos"
    - "UpdateDisplayState overload chaining: selectedRoiId variant delegates to base overload"
    - "DrawDirectionArrow: white arrow computed from EdgeDirection string at ROI center"

key-files:
  created: []
  modified:
    - WPF_Example/Custom/Sequence/Inspection/FAIConfig.cs
    - WPF_Example/Sequence/Param/CameraSlaveParam.cs
    - WPF_Example/UI/ViewModel/FAIResultRow.cs
    - WPF_Example/UI/ContentItem/MainResultViewerControl.xaml.cs
    - WPF_Example/Halcon/Display/HalconDisplayService.cs
    - WPF_Example/UI/ContentItem/MainView.xaml.cs

key-decisions:
  - "ToRoiDefinition uses sin/cos of ROI_Phi for backward compat with legacy INI data; new ROI always sets ROI_Phi=0 per D-05"
  - "FAIResultRow.SourceFAI exposes _fai directly — no copy — allows Plan 02 callers to access ROI fields and ToRoiDefinition()"
  - "UpdateDisplayState overload chains to base rather than duplicating state update logic"

patterns-established:
  - "ROI highlight: yellow + linewidth 3 for selected, green + linewidth 2 for unselected — established in HalconDisplayService.Render"
  - "Direction arrow: white 2px line + two arrowhead lines using Math.Atan2 — DrawDirectionArrow pattern"

requirements-completed: [TCH-01, TCH-02]

duration: 25min
completed: 2026-04-08
---

# Phase 02 Plan 01: ROI Highlight Foundation Summary

**FAIConfig.ToRoiDefinition() + SourceFAI exposure + selectedRoiId rendering pipeline + FAI selection → yellow ROI highlight with white direction arrow**

## Performance

- **Duration:** ~25 min
- **Started:** 2026-04-08T00:00:00Z
- **Completed:** 2026-04-08T00:25:00Z
- **Tasks:** 2
- **Files modified:** 6

## Accomplishments

- FAIConfig gains `ToRoiDefinition()` (D-05 backward-compat comment), `PolygonPoints`, `PixelResolutionX/Y` — all INI-serialized via ParamBase
- `CameraSlaveParam` gains `PixelResolution` (mm/pixel) INI field per D-12/D-16
- `FAIResultRow.SourceFAI` exposes underlying `FAIConfig` for Plan 02 ROI drawing/calibration callers
- `MainResultViewerControl` now stores `_selectedRoiId` and passes it to `HalconDisplayService.Render` — previously always `null`
- `HalconDisplayService` renders a white direction arrow (2px + arrowhead) at the center of the selected ROI
- `MainView.FAIResults_SelectionChanged` wired: DataGrid row selection highlights selected ROI yellow and shows "ROI not set" when untaught

## Task Commits

1. **Task 1: Data model extensions** - `5186301` (feat)
2. **Task 2: ROI rendering pipeline** - `4bde33e` (feat)

## Files Created/Modified

- `WPF_Example/Custom/Sequence/Inspection/FAIConfig.cs` - Added PixelResolutionX/Y, PolygonPoints, ToRoiDefinition()
- `WPF_Example/Sequence/Param/CameraSlaveParam.cs` - Added PixelResolution field
- `WPF_Example/UI/ViewModel/FAIResultRow.cs` - Added SourceFAI property
- `WPF_Example/UI/ContentItem/MainResultViewerControl.xaml.cs` - Added _selectedRoiId field + UpdateDisplayState overload + RenderNow fix
- `WPF_Example/Halcon/Display/HalconDisplayService.cs` - Added DrawDirectionArrow + call in Render loop
- `WPF_Example/UI/ContentItem/MainView.xaml.cs` - Wired FAIResults_SelectionChanged + GetCurrentFAIRois helper

## Decisions Made

- `ToRoiDefinition()` uses `Math.Sin/Cos(ROI_Phi)` for backward compat with legacy INI Rectangle2 data. New ROI input always sets `ROI_Phi=0` per D-05, so Rectangle2 is not used for new data.
- `FAIResultRow.SourceFAI` returns `_fai` directly (not a copy) — callers in Plan 02 need to write back ROI values via this reference.
- `UpdateDisplayState` 4-arg overload stores `selectedRoiId` then delegates to the 3-arg base overload to avoid duplicating list-update logic.

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered

None.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- Plan 02-02 can now use `FAIResultRow.SourceFAI.ToRoiDefinition()` and `FAIResultRow.SourceFAI.ROI_*` fields directly
- `halconViewer.UpdateDisplayState(rois, selectedRoiId, overlays, messages)` is ready for Plan 02 teaching interaction
- `PixelResolution` on `CameraSlaveParam` and `PixelResolutionX/Y` on `FAIConfig` are ready for calibration in Plan 02-02

---
*Phase: 02-teaching-calibration*
*Completed: 2026-04-08*

## Self-Check: PASSED

- All 6 source files present
- 02-01-SUMMARY.md present
- Commit 5186301 (Task 1) found
- Commit 4bde33e (Task 2) found
