---
phase: 260723-bsf
plan: 01
subsystem: vision-measurement
tags: [halcon, propertytools, fai-measurement, edge-measurement, itemssourceproperty]

# Dependency graph
requires:
  - phase: 260722-p5d
    provides: "Per-point averaging pattern established in 3 sibling measurement files (commits 565aac7/fe1b72f/c77a165)"
  - phase: 260722-vks
    provides: "Prior adversarial-audit batch that found the FAI_2 recipe issues this batch continues investigating"
provides:
  - "EdgePairDistanceMeasurement.EdgeSelection constrained to a Both-only PropertyGrid dropdown (prevents silent-zero result from free-text mistype)"
  - "PointToLineDistanceMeasurement per-point averaged distance (mean of per-point perpendicular distances, not single-midpoint)"
affects: [fai-measurement, propertytools-propertygrid, edge-measurement-averaging]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "EdgeOptionLists dedicated single-value list per measurement type when the functionally-correct value set is a strict subset of the shared Selections list"
    - "Per-point averaging: collect raw edge points via TryFitLine's opt-in collectedEdges param, compute per-point distance, arithmetic mean, with byte-identical single-midpoint fallback when the collected list is empty"

key-files:
  created: []
  modified:
    - WPF_Example/Custom/Sequence/Inspection/EdgeOptionLists.cs
    - WPF_Example/Custom/Sequence/Inspection/Measurements/EdgePairDistanceMeasurement.cs
    - WPF_Example/Custom/Sequence/Inspection/Measurements/PointToLineDistanceMeasurement.cs

key-decisions:
  - "Fix F: added a new EdgeOptionLists.EdgePairSelections list containing only 'Both' rather than reusing/extending the existing Selections list (First/Last/All), since those three values are all wrong for EdgePairDistance and would still permit the silent-zero bug."
  - "Fix G: reused this file's existing unsigned VisionAlgorithmService.DistancePointToLine formula for per-point averaging, rather than porting DualImageEdgeDistanceMeasurement's ProjectionPl+sqrt approach, since PointToLineDistance's existing single-midpoint call already used DistancePointToLine — keeping the sign convention and formula identical, only changing single-point vs multi-point averaging."
  - "Fix G: passed collectedEdges via named argument (skipping the optional selection parameter, which defaults to 'all') so the underlying edge fit is byte-identical to the previous behavior — only the post-fit distance calculation changed."

patterns-established:
  - "When constraining a free-text PropertyGrid property to a single legal value, prefer a small dedicated static list over reusing a broader shared list whose other values would still be functionally wrong."

requirements-completed: [FIX-F-EdgePairSelectionDropdown, FIX-G-PointToLinePerPointAveraging]

# Metrics
duration: 17min
completed: 2026-07-23
---

# Phase 260723-bsf: Fix EdgePairDistance EdgeSelection Dropdown + PointToLineDistance Per-Point Averaging Summary

**Constrained EdgePairDistance.EdgeSelection to a "Both"-only PropertyGrid dropdown (eliminating a silent-zero-result mistype path) and converted PointToLineDistance's single-midpoint distance calculation to a per-point averaged distance, mirroring the pattern already shipped in 3 sibling measurement files.**

## Performance

- **Duration:** 17 min
- **Started:** 2026-07-22T23:20:00Z
- **Completed:** 2026-07-22T23:37:06Z
- **Tasks:** 2
- **Files modified:** 3

## Accomplishments
- Fix F: `EdgePairDistanceMeasurement.EdgeSelection` is now a `[ItemsSourceProperty]`-backed dropdown offering only "Both" — the only value `FAIEdgeMeasurementService.TryMeasure` routes to the real paired-edge distance calculation. Any other value previously routed silently to a `DistanceMm = 0` success path.
- Fix G: `PointToLineDistanceMeasurement.TryExecute` now collects the Point ROI's individual fitted edge points and computes `resultValue` as the arithmetic mean of each point's unsigned perpendicular distance to the fitted reference line, reducing single-sample noise. Falls back byte-identically to the prior single-midpoint distance when no points are collected.
- Both fixes verified with clean Debug/x64 builds (0 errors, 0 new warnings vs the 5 known pre-existing warnings).

## Task Commits

Each task was committed atomically:

1. **Task 1 (Fix F): Constrain EdgePairDistance EdgeSelection to a "Both"-only PropertyGrid dropdown** - `8fdc75c` (fix)
2. **Task 2 (Fix G): Apply per-point averaging to PointToLineDistance point-to-line distance** - `1de66b8` (fix)

_Note: no plan-metadata commit included here — the orchestrator handles the docs commit (SUMMARY.md/STATE.md) separately per this quick task's constraints._

## Files Created/Modified
- `WPF_Example/Custom/Sequence/Inspection/EdgeOptionLists.cs` - Added `EdgePairSelections` static list (`{ "Both" }`) with a comment explaining why the existing `Selections` list (First/Last/All) is not reusable for this measurement type.
- `WPF_Example/Custom/Sequence/Inspection/Measurements/EdgePairDistanceMeasurement.cs` - Added `[ItemsSourceProperty(nameof(EdgeSelectionList))]` to `EdgeSelection` and a `Browsable(false) EdgeSelectionList` getter bound to `EdgeOptionLists.EdgePairSelections`. `TryExecute` body, `temp` FAIConfig construction, and default value ("Both") unchanged.
- `WPF_Example/Custom/Sequence/Inspection/Measurements/PointToLineDistanceMeasurement.cs` - Point ROI `TryFitLine` call now passes `collectedEdges: collectedEdgePoints` (named argument, default `selection = "all"` preserved). Distance calculation loops over collected points, sums `DistancePointToLine` per point, and divides by count; falls back to the original single-midpoint `DistancePointToLine(pRow, pCol, ...)` call when the list is empty. Line ROI fit call unchanged.

## Decisions Made
- Fix F: created a dedicated `EdgePairSelections` list instead of reusing `EdgeOptionLists.Selections`, because First/Last/All are all functionally wrong for `EdgePairDistance` and reusing that list would still allow the silent-zero bug via the UI.
- Fix G: reused this file's existing `VisionAlgorithmService.DistancePointToLine` (unsigned) formula for the per-point loop rather than porting `DualImageEdgeDistanceMeasurement`'s `ProjectionPl` + `Math.Sqrt` approach, keeping the change to "single-point to multi-point averaging" only — no formula/sign-convention change.
- Fix G: used a named argument (`collectedEdges: collectedEdgePoints`) on the Point ROI `TryFitLine` call to skip the optional `selection` parameter, preserving its default `"all"` value so the edge fit itself is unchanged; only the downstream distance computation changed.

## Deviations from Plan

None - plan executed exactly as written for both tasks.

Note: an unrelated, pre-existing local working-tree change to `WPF_Example/DatumMeasurement.csproj` (`DefineConstants` toggling `SIMUL_MODE` on/off — a documented per-developer-machine convention per the inline comment at that line) was present before and after this task's edits, produced by the build tooling on this SIMUL-only dev machine, and was deliberately left unstaged/uncommitted in both task commits as out of scope.

## Issues Encountered
None.

## Known Stubs
None - no stub/placeholder patterns introduced by this change.

## Threat Flags
None - both fixes are input-constraint / calculation-accuracy changes within existing trust boundaries; no new network endpoints, auth paths, file access, or schema changes introduced. See PLAN.md `<threat_model>` (T-bsf-01, T-bsf-02), both dispositioned `mitigate` and addressed by Task 1 / Task 2 respectively.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness
- Both fixes are code-level only; FAI_2 recipe (where both bugs were originally observed) is currently inactive, so no live re-measurement/UAT is required for this quick task.
- `EdgePairDistance` and `PointToLineDistance` measurement types are ready for use in any future recipe without risk of the silent-zero or single-point-noise issues found by the adversarial audit.

---
*Phase: 260723-bsf*
*Completed: 2026-07-23*

## Self-Check: PASSED

- FOUND: WPF_Example/Custom/Sequence/Inspection/EdgeOptionLists.cs
- FOUND: WPF_Example/Custom/Sequence/Inspection/Measurements/EdgePairDistanceMeasurement.cs
- FOUND: WPF_Example/Custom/Sequence/Inspection/Measurements/PointToLineDistanceMeasurement.cs
- FOUND: .planning/quick/260723-bsf-fix-edgepairdistancemeasurement-edgesele/260723-bsf-SUMMARY.md
- FOUND commit: 8fdc75c (Task 1 - Fix F)
- FOUND commit: 1de66b8 (Task 2 - Fix G)
