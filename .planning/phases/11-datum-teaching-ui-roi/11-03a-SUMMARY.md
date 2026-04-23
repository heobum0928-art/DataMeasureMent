---
phase: 11-datum-teaching-ui-roi
plan: 03a
subsystem: datum-halcon
tags: [wpf, datum, teaching, halcon, model, service, display]
dependency-graph:
  requires:
    - Phase 4 DatumConfig (ParamBase serialization contract)
    - Phase 4 DatumFindingService.TryTeachDatum / TryFindLine (existing line-detection pipeline)
    - Phase 4 HalconDisplayService.RenderDatumOverlay (existing cyan/blue Line ROI + magenta RefOrigin cross body)
    - Plan 11-01 HalconDisplayService Circle branch (must stay untouched)
  provides:
    - DatumConfig.SourceShotName (serializable string) — D-08 image-source Shot inheritance
    - DatumConfig.Line1Detected_RBegin/CBegin/REnd/CEnd + Line2Detected_* (8 volatile doubles) — D-11 overlay coords
    - DatumConfig.LastTeachSucceeded (volatile bool) — D-11 overlay gate + Plan 03b UI status
    - DatumFindingService.TryTeachDatum line-coord writeback on success + LastTeachSucceeded on every exit
    - HalconDisplayService.RenderDatumOverlay additive yellow Line1 + cyan Line2 + red 20px cross branch
  affects:
    - Plan 11-03b — UI wiring will subscribe to SourceShotName + LastTeachSucceeded
tech-stack:
  added: []
  patterns:
    - Volatile ParamBase field pattern (Browsable(false) runtime-only doubles; tolerated INI noise matches CurrentTransform/LastFindSucceeded precedent)
    - Success/failure gating via post-condition bool (mirrors LastFindSucceeded pattern from Phase 4)
    - Additive overlay branch inside existing outer try/catch (preserves existing defensive pattern)
key-files:
  created: []
  modified:
    - WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs
    - WPF_Example/Halcon/Algorithms/DatumFindingService.cs
    - WPF_Example/Halcon/Display/HalconDisplayService.cs
decisions:
  - Scope-guarded RenderDatumOverlay extension — existing cyan/blue/magenta palette preserved (Warning 5); new yellow/cyan/red additive branch gated on LastTeachSucceeded only
  - DatumFindingService TryTeachDatum signature preserved (Option 2 per D-11 Specifics) — writeback via DatumConfig volatile fields
  - LastTeachSucceeded stamped on every exit path (success + 5 failure branches: Line1 fail, Line2 fail, collinear, parallel, catch)
  - Accepted INI noise from 8 Browsable(false) doubles — matches existing ShotConfig/FAIConfig volatile-double precedent (PATTERNS.md §Shared 8)
metrics:
  duration-minutes: 4
  completed-date: 2026-04-23
  tasks: 3
  files: 3
  commits: 3
---

# Phase 11 Plan 03a: Datum Model + Service + Display Layer Summary

Model/service/display foundation for Datum teaching (WR-RT-03). Landed three surgical changes — DatumConfig volatile fields, DatumFindingService line-coord writeback, HalconDisplayService additive overlay branch — entirely independent of the UI wiring that Plan 11-03b will layer on top. Existing cyan/blue/magenta post-teach palette preserved untouched per Warning 5.

## What Was Built

### Task 1 — `DatumConfig.cs` (commit `05545c8`)

**Exact insertions:**
- Line 28-30: `SourceShotName` (Datum|ImageSource category, string, default `""`) inserted immediately after existing `ReuseFromShotName` (L26) — same category group as ImageSourceMode / ReuseFromShotName
- Line 76-94: 8 volatile `[Browsable(false)]` double properties + 1 `LastTeachSucceeded` bool inserted immediately after existing `LastFindSucceeded` (L74) and before the constructor (L96)
  - `Line1Detected_RBegin / CBegin / REnd / CEnd`
  - `Line2Detected_RBegin / CBegin / REnd / CEnd`
  - `LastTeachSucceeded` (bool)

**Unchanged:** all 18 pre-existing fields (DatumName, ImageSourceMode, ReuseFromShotName, Line1_Row/Col/Phi/Length1/Length2, Line2_*, RefOriginRow/Col, RefAngleRad, EdgeThreshold, Sigma, EdgePolarity, EdgePolarityList, IsConfigured, CurrentTransform, LastFindSucceeded), constructor, and brace style (K&R per existing file).

### Task 2 — `DatumFindingService.cs` (commit `9688ce8`)

**Local-variable names verified before writing:** matched exactly — `line1RowBegin / line1ColBegin / line1RowEnd / line1ColEnd` and `line2RowBegin / line2ColBegin / line2RowEnd / line2ColEnd`. **No deviation.**

**Exact insertions (all inside `TryTeachDatum` L122-202):**
- Success-path writeback (9 lines) inserted after `config.IsConfigured = true` (previously L193) and immediately before `return true;` — assigns 8 detected line coords + sets `LastTeachSucceeded = true`
- `config.LastTeachSucceeded = false;` prepended to 5 failure branches:
  1. Line1 `TryFindLine` fail branch (before `error = "Line1: " + lineError;`)
  2. Line2 `TryFindLine` fail branch (before `error = "Line2: " + lineError;`)
  3. Collinear branch (before `error = "Lines are collinear...";`)
  4. Parallel/infinity branch (before `error = "Lines are parallel...";`)
  5. Outer `catch (Exception ex)` branch (before `error = ex.Message;`) — guarded with `if (config != null)` to match defensive contract

**Signature unchanged:** `public bool TryTeachDatum(HImage image, DatumConfig config, out string error)` — identical to pre-edit (Option 2 per plan).

**Unchanged:** RefOrigin/RefAngle/IsConfigured math, IntersectionLl call, TryFindLine body (L207-278), TryFindDatum runtime method (L25-112), all comments and WR-01 guards.

### Task 3 — `HalconDisplayService.cs` (commit `b2afa1c`)

**Exact insertion:** new additive block (25 lines) appended after the existing `if (datum.IsConfigured) { ... magenta cross + label ... }` block (previously L379-397) and before the outer `catch` (L399) inside `RenderDatumOverlay`. All new `HOperatorSet` calls live inside the pre-existing outer try/catch so display errors remain suppressed.

New block draws (gated on `datum.LastTeachSucceeded`):
- Line1 detected in `yellow` via `HOperatorSet.DispLine(window, Line1Detected_RBegin, CBegin, REnd, CEnd)`
- Line2 detected in `cyan` via `HOperatorSet.DispLine(window, Line2Detected_RBegin, CBegin, REnd, CEnd)`
- Red 20px half-length intersection cross at `(RefOriginRow, RefOriginCol)` via 2 additional `HOperatorSet.DispLine` segments, line width 2

**Unchanged:** existing `Render` method (L17-192), `RenderCircleDraft` (L195-211 — Plan 01), `RenderPolygon` / `RenderPolygonPoints` / `RenderCalibrationOverlay` / `DrawDirectionArrow` / `ParsePolygonPoints` / `EnsureFontInitialized` / `DrawRectangleOutline`. Existing RenderDatumOverlay body (cyan/blue rectangle ROIs via `color` local at L354; magenta RefOrigin cross + label at L381/L393) is textually untouched.

## Warning 5 Scope-Guard Compliance (Grep Evidence)

| Check | Pre-edit baseline | Post-edit | Status |
|-------|-------------------|-----------|--------|
| `grep -c '"magenta"'` in HalconDisplayService.cs | 2 | 2 | UNCHANGED — existing RefOrigin cross `SetColor(window, "magenta")` at two sites (label block) preserved |
| `grep -c "RoiShape.Circle"` in HalconDisplayService.cs | 1 | 1 | UNCHANGED — Plan 01 Circle render branch intact |
| Existing cyan/blue Line1/Line2 ROI rect at L354 (`string color = isSelected ? "cyan" : "blue";`) | present | present | UNCHANGED — textually identical |
| Existing `DispRectangle2` calls for Line1/Line2 ROI | 2 | 2 | UNCHANGED |
| New `if (datum.LastTeachSucceeded)` branch | 0 | 1 | ADDED (inside outer try, after magenta block) |
| New `HOperatorSet.DispLine(window` count | 2 | 6 | +4 (Line1 + Line2 + 2 cross segments) |
| New `"yellow"` occurrence inside RenderDatumOverlay | 0 | 1 | ADDED (Line1 detected) |

## Signature Conformance

| Contract | Planned | Delivered | Match |
|----------|---------|-----------|-------|
| `DatumFindingService.TryTeachDatum(HImage, DatumConfig, out string)` | unchanged | unchanged | Exact |
| `DatumConfig.SourceShotName` | `string`, default `""`, Category="Datum|ImageSource" | same | Exact |
| 8 volatile Line*Detected_* doubles | `[Browsable(false)]` runtime-only | same | Exact |
| `LastTeachSucceeded` bool | `[Browsable(false)]` runtime-only | same | Exact |
| RenderDatumOverlay additive branch | `if (datum.LastTeachSucceeded)` inside existing try/catch | same | Exact |
| Cross half-length | 20px | `const double crossHalf = 20.0` | Exact |
| Cross line width | 2 | `HOperatorSet.SetLineWidth(window, 2)` | Exact |

## Deviations from Plan

None. Local variable names in `TryTeachDatum` matched the plan exactly (`line1RowBegin / line1ColBegin / line1RowEnd / line1ColEnd`, `line2RowBegin / ...`). No adjustments needed.

### Auto-fixed Issues

None — all three tasks landed on first edit.

## Verification

| Check | Result |
|-------|--------|
| msbuild Debug/x64 after Task 1 | SUCCEEDED (only pre-existing warnings in VirtualCamera.cs:266 / VisionAlgorithmService.cs:64) |
| msbuild Debug/x64 after Task 2 | SUCCEEDED (same pre-existing warnings only) |
| msbuild Debug/x64 after Task 3 | SUCCEEDED (same pre-existing warnings only) |
| `grep -n "public string SourceShotName" DatumConfig.cs` | 1 match, `= ""` default |
| `grep -c "Line1Detected_\|Line2Detected_" DatumConfig.cs` | 16 (8 decls × 2 refs each: decl + `public double` on same line) — 8 unique Line*Detected_* property names present |
| `grep -n "public bool LastTeachSucceeded" DatumConfig.cs` | 1 match (L94), preceded by `[Browsable(false)]` (L93) |
| `grep -n "260423 hbk Phase 11" DatumConfig.cs` | 2 markers (SourceShotName D-08 + volatile-fields D-11) |
| `grep -c "config.Line1Detected_RBegin = " DatumFindingService.cs` | 1 (success path only) |
| `grep -c "LastTeachSucceeded = true\|LastTeachSucceeded   = true" DatumFindingService.cs` | 1 (success stamp) |
| `grep -c "LastTeachSucceeded = false" DatumFindingService.cs` | 5 (Line1 fail, Line2 fail, collinear, parallel, catch — exceeds ≥3 requirement) |
| `grep -n "public bool TryTeachDatum" DatumFindingService.cs` | 1 match at L122, signature unchanged |
| `grep -c "260423 hbk Phase 11 D-11" DatumFindingService.cs` | 6 (5 failure stamps + 1 success writeback block) |
| `grep -n "if (datum.LastTeachSucceeded)" HalconDisplayService.cs` | 1 match inside RenderDatumOverlay |
| `grep -c "HOperatorSet.DispLine(window" HalconDisplayService.cs` | 6 (baseline 2 + 4 new) |
| `grep -n "const double crossHalf = 20" HalconDisplayService.cs` | 1 match |
| `grep -c '"magenta"' HalconDisplayService.cs` | 2 (UNCHANGED from baseline) |
| `grep -c "RoiShape.Circle" HalconDisplayService.cs` | 1 (UNCHANGED from baseline — Plan 01 unaffected) |
| `grep -c "260423 hbk Phase 11 D-11" HalconDisplayService.cs` | 1 (new additive-branch marker) |

## Regression Check

| Existing code path | Status |
|--------------------|--------|
| DatumConfig DatumName / ImageSourceMode / ReuseFromShotName / Line1_* / Line2_* / RefOrigin* / RefAngleRad / EdgeThreshold / Sigma / EdgePolarity / IsConfigured / CurrentTransform / LastFindSucceeded / EdgePolarityList | Untouched |
| DatumConfig constructor signature + ParamBase inheritance | Untouched |
| DatumFindingService.TryFindDatum (runtime) | Untouched |
| DatumFindingService.TryFindLine private helper | Untouched |
| DatumFindingService.TryTeachDatum signature + RefOrigin/RefAngle/IsConfigured math + IntersectionLl + WR-01 parallel/collinear guards | Untouched |
| HalconDisplayService.Render (Rect/Polygon/Circle/draft/overlays/messages) | Untouched |
| HalconDisplayService.RenderCircleDraft (Plan 01) | Untouched |
| HalconDisplayService.RenderDatumOverlay existing cyan/blue ROI rects + magenta RefOrigin cross + "Datum Origin" label | Untouched |
| HalconDisplayService.RenderPolygon / RenderPolygonPoints / RenderCalibrationOverlay / DrawDirectionArrow / ParsePolygonPoints / EnsureFontInitialized | Untouched |
| Existing Phase 6/7 INI recipes (SourceShotName defaults to "" when key missing) | Compatible |

## Authentication Gates

None encountered.

## Deferred Issues

None. All acceptance criteria pass on a clean Debug/x64 build.

## Stub / Follow-up Consumption

The public surface introduced here has **zero consumers within this plan** — this is by design:

- **`DatumConfig.SourceShotName`** will be read by Plan 03b's Datum Grab/LoadImage button handlers to resolve the inheriting ShotConfig (camera, Z, SimulImagePath, lighting).
- **`DatumConfig.Line*Detected_*` + `LastTeachSucceeded`** are populated by `TryTeachDatum` and consumed by `HalconDisplayService.RenderDatumOverlay`'s new branch — already wired end-to-end within this plan's service/display layer.
- **Plan 03b** will add the UI that actually invokes `TryTeachDatum` (the `btn_teachDatum` ToggleButton click handler in `MainView.xaml.cs`) and observe the `LastTeachSucceeded` flag for success/error hint styling.

Until Plan 11-03b ships, `TryTeachDatum` still has zero callers — the new overlay branch is dormant (LastTeachSucceeded cold-loads as `false`, so the additive block never fires). Build-green and regression-free.

## Self-Check: PASSED

- WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs: FOUND (24 insertions)
- WPF_Example/Halcon/Algorithms/DatumFindingService.cs: FOUND (16 insertions)
- WPF_Example/Halcon/Display/HalconDisplayService.cs: FOUND (26 insertions)
- Commit 05545c8: FOUND (Task 1 — DatumConfig)
- Commit 9688ce8: FOUND (Task 2 — DatumFindingService.TryTeachDatum writeback)
- Commit b2afa1c: FOUND (Task 3 — RenderDatumOverlay additive branch)
- msbuild Debug/x64: PASSED (DatumMeasurement.exe produced, only pre-existing warnings)
- Warning 5 scope guard: PASSED (magenta count + RoiShape.Circle count unchanged from baseline)
