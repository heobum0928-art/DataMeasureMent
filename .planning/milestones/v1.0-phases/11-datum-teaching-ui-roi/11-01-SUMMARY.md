---
phase: 11-datum-teaching-ui-roi
plan: 01
subsystem: halcon-ui
tags: [wpf, halcon, roi, circle, infrastructure]
dependency-graph:
  requires:
    - HalconDotNet 24.11 (HOperatorSet.DispCircle / DispLine)
    - Phase 02 RoiDefinition DataContract model
    - Phase 02 HalconDisplayService.Render foreach loop
    - Phase 02 MainResultViewerControl Rect drag state machine (mirrored)
  provides:
    - RoiShape enum (Rect/Polygon/Circle) in ReringProject.Halcon.Models
    - RoiDefinition.Shape / CenterRow / CenterCol / Radius [DataMember] fields
    - HalconDisplayService Circle render branch (disp_circle + red center cross)
    - HalconDisplayService.RenderCircleDraft(HWindow, double, double, double)
    - MainResultViewerControl.StartCircleDrawing / CommitActiveCircle API
    - MainResultViewerControl.CircleDrawingCompleted event + CircleDrawCompletedArgs
  affects:
    - Plan 11-02 (Circle ROI toolbar button + CircleDiameterMeasurement wiring) — consumer of all APIs above
tech-stack:
  added: []
  patterns:
    - DataContract backward-compat via default member value (Shape defaults to RoiShape.Rect when absent in JSON)
    - Rubber-band drag (MouseDown=center, MouseMove=radius preview, MouseUp=commit) mirroring existing Rect drag
    - Halcon display defensive try/catch (matches RenderDatumOverlay pattern)
key-files:
  created: []
  modified:
    - WPF_Example/Halcon/Models/RoiDefinition.cs
    - WPF_Example/Halcon/Display/HalconDisplayService.cs
    - WPF_Example/UI/ContentItem/MainResultViewerControl.xaml.cs
decisions:
  - Placed RoiShape enum as sibling type inside RoiDefinition.cs (Claude's Discretion per CONTEXT.md; matches PATTERNS.md §7 locality recommendation)
  - Matched actual file brace style per file — MainResultViewerControl.xaml.cs uses Allman (not K&R as PATTERNS.md §Shared 1 states); followed existing file over the pattern doc
  - HalconDisplayService Circle branch uses window.SetColor/SetLineWidth extension form (consistent with surrounding ROI foreach loop at L45-48); RenderCircleDraft mirrors same form
  - CircleDrawCompletedArgs declared as separate top-level class (same namespace as MainViewerPointerChangedEventArgs) — mirrors existing precedent
metrics:
  duration-minutes: 12
  completed-date: 2026-04-23
  tasks: 3
  files: 3
  commits: 3
---

# Phase 11 Plan 01: Circle ROI Infrastructure Summary

Pure infrastructure for Circle ROI: extended `RoiDefinition` data model, added Circle render branch + draft preview helper to `HalconDisplayService`, and exposed `StartCircleDrawing` / `CommitActiveCircle` / `CircleDrawingCompleted` on `MainResultViewerControl`. No toolbar buttons, no `CircleDiameterMeasurement` wiring, no state-machine changes — those belong to Plan 11-02 which consumes these APIs.

## What Was Built

### Task 1 — `RoiDefinition.cs` (commit `cdf45eb`)
- Added `public enum RoiShape { Rect, Polygon, Circle }` as sibling type above the `RoiDefinition` class declaration (same namespace `ReringProject.Halcon.Models`, line 6).
- Added four `[DataMember]` properties immediately after the existing `EdgeThreshold` field (lines 42-53):
  - `RoiShape Shape { get; set; } = RoiShape.Rect;`
  - `double CenterRow { get; set; }`
  - `double CenterCol { get; set; }`
  - `double Radius { get; set; }`
- Existing 16 `[DataMember]` fields unchanged. `Clone()` unchanged (MemberwiseClone already covers new auto-properties).
- Backward compatibility: JSON missing `Shape` → default `RoiShape.Rect` → legacy Rect/Polygon INI recipes continue to load correctly.

### Task 2 — `HalconDisplayService.cs` (commit `1297e99`)
- Inside `Render(...)` `foreach (var roi in rois)` loop, added new Circle branch **before** the polygon check (lines 50-66). Branch uses `continue` so it short-circuits both the polygon and rect paths.
  - Unselected: `lime green`, 2px. Selected: `yellow`, 3px. `HOperatorSet.DispCircle(window, roi.CenterRow, roi.CenterCol, roi.Radius)`.
  - Red 6px center cross (two `window.DispLine` segments) drawn after outline.
- Added new public method `RenderCircleDraft(HWindow window, double centerRow, double centerCol, double radius)` after `Render(...)` (lines 196-212).
  - Guards `window == null || radius <= 0` (threat mitigation T-11-01-03).
  - Red outline + red 6px center cross.
  - Wrapped in try/catch per existing defensive pattern.
- Existing Rect/Polygon/Datum/Calibration/overlay/message code textually unchanged.

### Task 3 — `MainResultViewerControl.xaml.cs` (commit `5176cbe`)
- Added `public class CircleDrawCompletedArgs : EventArgs` at file scope (lines 31-37), mirroring `MainViewerPointerChangedEventArgs` precedent.
- Added three private fields beside Rect drawing state (lines 72-75): `_isDrawingCircle`, `_circleDraftCenter` (System.Windows.Point), `_circleDraftRadius`.
- Added public event `CircleDrawingCompleted` of type `EventHandler<CircleDrawCompletedArgs>` (line 79).
- Added public API pair after `CommitActiveRectangle` (lines 222-237):
  - `StartCircleDrawing()` — sets `_isDrawingCircle = true`, clears draft, triggers `Render()`.
  - `CommitActiveCircle()` — cancel/end symmetric to `CommitActiveRectangle`.
- Extended `HMouseDown` with Circle-mode branch (after Rect branch) — stores center, resets radius, early return.
- Extended `HMouseMove` with Circle-mode branch — computes Euclidean radius from stored center, re-renders, early return.
- Extended `HMouseUp` with Circle-mode branch — sets `_isDrawingCircle = false`, raises `CircleDrawingCompleted` with image coords (CenterRow=Y, CenterCol=X), early return. Only fires when `_circleDraftRadius > 0` (degenerate click discarded).
- Extended `RenderNow` (Render dispatch) with `RenderCircleDraft` call when `_isDrawingCircle && _circleDraftRadius > 0`, placed after the existing Datum overlay branch.

## Verification

All acceptance criteria met:

| Check | Result |
|-------|--------|
| `grep -c "enum RoiShape"` in RoiDefinition.cs | 1 (exact: `{ Rect, Polygon, Circle }`) |
| 4 new `[DataMember]` fields added | Verified — Shape/CenterRow/CenterCol/Radius |
| `grep -c "roi.Shape == RoiShape.Circle"` in HalconDisplayService.cs | 1 (inside Render foreach) |
| `grep -c "public void RenderCircleDraft"` in HalconDisplayService.cs | 1 |
| `DispCircle` occurrences in HalconDisplayService.cs | 2 (Render branch + RenderCircleDraft) |
| `lime green` + `yellow` in new Circle branch | Both present |
| `grep -c "StartCircleDrawing"` | 1 |
| `grep -c "CommitActiveCircle"` | 1 |
| `grep -c "CircleDrawingCompleted?.Invoke"` | 1 |
| `grep -c "class CircleDrawCompletedArgs"` | 1 |
| `_isDrawingCircle` usages | 8 (decl + field + HMouseDown + HMouseMove + HMouseUp × 2 + StartCircleDrawing + Render dispatch) |
| `_isDrawingRect` regression count unchanged | 7 (baseline preserved) |
| `260423 hbk` markers present | 9 in MainResultViewerControl.xaml.cs (per task 3 needing ≥5 across 5 insertion points) |
| msbuild Debug/x64 | 0 (succeeded with only pre-existing warnings in VirtualCamera.cs:266 and VisionAlgorithmService.cs:64) |

Project-level verification from plan:
- `grep -rn "RoiShape.Circle" WPF_Example/`: only `HalconDisplayService.cs:51` (render switch site). Plan 02 will add consumer sites.
- `grep -rn "CircleDrawingCompleted" WPF_Example/`: only one declaration (MainResultViewerControl.xaml.cs:79) and one internal raiser (line 572) — zero external subscribers, as expected until Plan 02.

## Regression Check

| Existing code path | Status |
|--------------------|--------|
| RoiDefinition.Row1/Col1/Row2/Col2/PolygonPoints fields | Untouched |
| RoiDefinition.Clone() | Untouched |
| HalconDisplayService Rect branch (`roi.Row1 != 0 \|\| ...`) | Untouched |
| HalconDisplayService Polygon branch (PolygonPoints parse) | Untouched |
| HalconDisplayService draft Rect render | Untouched |
| HalconDisplayService RenderDatumOverlay | Untouched (Plan 03 territory) |
| MainResultViewerControl Rect drag state/events | Untouched — grep count for `_isDrawingRect` unchanged (7) |
| MainResultViewerControl Polygon / Calibration / Datum overlays | Untouched |
| MainResultViewerControl Pan / Zoom / ManualMeasure / Crosshair | Untouched |

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 — Structural edit recovery] Initial HalconDisplayService edit incorrectly placed RenderCircleDraft mid-method**
- **Found during:** Task 2 mid-execution, after first `Edit` tool invocation for the draft renderer.
- **Issue:** The edit sequence inadvertently closed the `Render(...)` method body before the inspectionOverlays/displayMessages loops, orphaning the remainder of the method as part of `RenderCircleDraft`.
- **Fix:** Reverted the misplaced method boundary with a corrective Edit, then re-added `RenderCircleDraft` as a proper standalone method after `Render(...)` closes (line 196). Build confirmed clean.
- **Files modified:** WPF_Example/Halcon/Display/HalconDisplayService.cs
- **Commit:** Rolled into the single Task 2 commit (`1297e99`) — no separate fix commit needed since the flaw never left staging.

### API Shape Conformance

| Contract item | Planned | Delivered | Match |
|---------------|---------|-----------|-------|
| `public enum RoiShape { Rect, Polygon, Circle }` | sibling of RoiDefinition | sibling, line 6 | Exact |
| 4 `[DataMember]` fields on RoiDefinition | Shape/CenterRow/CenterCol/Radius | Same, lines 43-53 | Exact |
| `public void StartCircleDrawing()` | — | Same signature | Exact |
| `public void CommitActiveCircle()` | `void` (not `RoiDefinition` return) | `void` | Exact |
| `public event EventHandler<CircleDrawCompletedArgs> CircleDrawingCompleted` | — | Same | Exact |
| `CircleDrawCompletedArgs { CenterRow, CenterCol, Radius }` | — | Same | Exact |
| `public void RenderCircleDraft(HWindow, double, double, double)` | — | Same | Exact |
| Circle render branch BEFORE polygon check | — | Lines 50-66 before polygon L68 | Exact |

No deviations from planned API shape.

### Brace Style Reconciliation

PATTERNS.md §Shared 1 states UI files use K&R; the target file `MainResultViewerControl.xaml.cs` **actually** uses Allman throughout (verified by inspecting existing `StartRectangleDrawing`, `CommitActiveRectangle`, `HMouseDown`, `HMouseMove`, `HMouseUp`). Followed the file's actual style over the pattern doc — rationale: plan Task 3 step 9 explicitly directs "MATCH EXISTING FILE", and consistency within the file is the higher-order requirement.

## Authentication Gates

None encountered.

## Deferred Issues

None. All acceptance criteria pass on a clean Debug/x64 build.

## Stub / Follow-up Consumption

The public surface introduced here has **zero subscribers** in this plan — this is by design. Plan 11-02 will:
- Add `btn_circleRoi` toolbar button in `MainView.xaml` and a `CircleRoiButton_Click` handler in `MainView.xaml.cs`
- Subscribe `halconViewer.CircleDrawingCompleted` in the Circle-mode entry
- Write committed (CenterRow/CenterCol/Radius) into the selected `CircleDiameterMeasurement.Circle_Row/Col/Radius` (D-17) and update the FAI's `RoiDefinition.Shape = RoiShape.Circle` + Center/Radius (D-16)

Until Plan 11-02 ships, this plan's Circle APIs are dormant but build-clean and regression-free.

## Self-Check: PASSED

- RoiDefinition.cs: FOUND (17 additions, line-count delta +16)
- HalconDisplayService.cs: FOUND (36 additions across 2 sites)
- MainResultViewerControl.xaml.cs: FOUND (72 additions across 7 sites)
- Commit cdf45eb: FOUND (Task 1 — RoiDefinition.cs)
- Commit 1297e99: FOUND (Task 2 — HalconDisplayService.cs)
- Commit 5176cbe: FOUND (Task 3 — MainResultViewerControl.xaml.cs)
- msbuild Debug/x64: PASSED (DatumMeasurement.exe produced, only pre-existing warnings)
