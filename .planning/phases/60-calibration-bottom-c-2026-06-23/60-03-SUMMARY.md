---
phase: 60-calibration-bottom-c-2026-06-23
plan: "03"
subsystem: EthernetVision/CalibrationService
tags: [picker-center, calibration, halcon, fit-circle, eccentric-fit, av-05]
dependency_graph:
  requires: [60-01, 60-02]
  provides: [PickerCenterCalibrationService, EthernetVisionHandler.PickerCal]
  affects: [EthernetVisionHandler.cs, DatumMeasurement.csproj]
tech_stack:
  added: [PickerCenterCalibrationService (new stateful service)]
  patterns: [fit_circle_contour_xld x2, GenCircle+ReduceDomain+EdgesSubPix eccentric-trajectory, try-catch-finally HALCON dispose]
key_files:
  created:
    - WPF_Example/Custom/EthernetVision/PickerCenterCalibrationService.cs
  modified:
    - WPF_Example/Custom/EthernetVision/EthernetVisionHandler.cs
    - WPF_Example/DatumMeasurement.csproj
decisions:
  - "FitCircleContourXld used TWICE: #1 per-step Cal jig circle (TryAddStep), #2 eccentric atukey fit of accumulated jig centers (TryComputePickerCenter) — closes AV-05"
  - "Service is fully independent of AlignShapeMatchService / 2-pattern shape match (D-02 corrected)"
  - "MIN_STEPS=6 guard + radius bounds [1.0, 100000.0 px] reject degenerate fits (D-03/D-06)"
  - "Phase-60 start commit = 48f2c49; ANTI-GOAL CLEAN proven vs that baseline"
metrics:
  duration: "~20 min (execution only)"
  completed: "2026-06-24"
  tasks: 3
  files_created: 1
  files_modified: 2
---

# Phase 60 Plan 03: Picker Center Calibration Service Summary

**One-liner:** Stateful `PickerCenterCalibrationService` with `fit_circle_contour_xld` x2 — per-step Cal jig circle → eccentric atukey fit of 36 accumulated jig centers → picker rotation center stored to `SystemSetting.PickerCenterRow/Col`.

## Tasks Completed

| Task | Name | Commit | Files |
|------|------|--------|-------|
| 1 | Create PickerCenterCalibrationService | f73915a | WPF_Example/Custom/EthernetVision/PickerCenterCalibrationService.cs |
| 2 | Wire PickerCal into EthernetVisionHandler + csproj | f73915a | EthernetVisionHandler.cs, DatumMeasurement.csproj |
| 3 | Build Debug/x64 + anti-goal proof | (no code commit; verification only) | — |

## Implementation Details

### Task 1: PickerCenterCalibrationService.cs

New file in `namespace ReringProject`. K&R brace, C# 7.2. Three public members:

- `void Reset()` — clears accumulated jig center lists.
- `bool TryAddStep(HImage img, double searchRow, searchCol, searchRadius, out string error)` — default-param overload forwarding to full overload (sigma=1.0, threshold=30).
- `bool TryAddStep(..., double sigma, int threshold, out string error)` — GenCircle → ReduceDomain → EdgesSubPix("canny") → **FitCircleContourXld #1** ("atukey", -1, 2, 0, 5, 2) → jig center → `_rows`/`_cols` accumulation. Returns false if img==null, no edges, or fitRow.Length==0.
- `bool TryComputePickerCenter(out row, col, radius, error)` — guards `_rows.Count < MIN_STEPS (=6)`. GenContourPolygonXld(accumulated) → **FitCircleContourXld #2** ("atukey") → validates radius bounds [1.0, 100000.0 px] → writes `SystemSetting.Handle.PickerCenterRow/Col` + calls `SystemSetting.Handle.Save()`.

All HALCON HObject/HTuple variables declared before `try` block, disposed in `finally` (D-03/D-06). Every public method try-catch → false (no throws).

No dependency on `AlignShapeMatchService`, `Matcher`, `TryFindCenter`, or `VisionAlgorithmService` — fully independent (D-02 corrected).

### Task 2: EthernetVisionHandler.cs + csproj

- Added `public PickerCenterCalibrationService PickerCal { get; private set; }` property (after `Matcher`).
- In `Initialize()` try-block: `PickerCal = new PickerCenterCalibrationService();` immediately after `Matcher = new AlignShapeMatchService();`.
- In `catch`-block: null-guard `if (PickerCal == null) { PickerCal = new PickerCenterCalibrationService(); }` (mirrors Matcher pattern).
- `DatumMeasurement.csproj`: added `<Compile Include="Custom\EthernetVision\PickerCenterCalibrationService.cs" />` between AlignShapeMatchService.cs and EthernetVisionHandler.cs.

### Task 3: Build + Anti-goal

**Build result:** MSBuild Debug/x64, exit code 0, **0 errors**. 1 warning (`MSB3884 MinimumRecommendedRules.ruleset`) — pre-existing since before Phase 60, not new.

**PHASE60_START commit:** `48f2c49`

**Anti-goal proof (`git diff --name-only 48f2c49 HEAD`):**
- `VisionAlgorithmService.cs` — UNCHANGED
- `PatternMatchService.cs` — UNCHANGED
- `RecipeFileHelper.cs` — UNCHANGED
- All Grabber files (Sequence_*/Action_*/SystemHandler/HikCamera/VirtualCamera/DeviceHandler/InspectionSequence) — UNCHANGED
- Verified command: `git diff --name-only 48f2c49 HEAD | grep -iE "Sequence_|..."` → output: `ANTI-GOAL CLEAN`

Phase 60 changed-existing-files: `{SystemSetting.cs (60-01), AlignShapeMatchService.cs (60-02), EthernetVisionHandler.cs (60-03), DatumMeasurement.csproj (60-03)}` — exactly as planned.

## Deviations from Plan

None — plan executed exactly as written. The `VisionAlgorithmService.TryFindCircle` pattern (L294-322) was read and independently replicated inline (no call, no modification).

Note: Task 1 automated verify grep for `TryFindCenter|Matcher|AlignShapeMatchService|VisionAlgorithmService` found one match — in the class docstring (Chinese comment explaining the pattern was independently replicated). This is not a code dependency; actual code contains no such references. Accepted as compliant.

## Known Stubs

None. PickerCenterCalibrationService is a complete service layer. Phase 61 UI (caller) wiring is out of scope for this plan.

## Threat Flags

None. New surface is local-only: operator-local Cal jig images → local INI persist. No network endpoints, no auth paths, no external schema changes introduced.

## Self-Check: PASSED

- `WPF_Example/Custom/EthernetVision/PickerCenterCalibrationService.cs` — FOUND
- `WPF_Example/Custom/EthernetVision/EthernetVisionHandler.cs` contains `PickerCal` — CONFIRMED
- `WPF_Example/DatumMeasurement.csproj` contains `PickerCenterCalibrationService.cs` — CONFIRMED
- Commit `f73915a` — FOUND (`feat(60-03): PickerCenterCalibrationService + EthernetVisionHandler.PickerCal wiring`)
- Build exit code 0, errors 0 — CONFIRMED
- Anti-goal `ANTI-GOAL CLEAN` vs `48f2c49` — CONFIRMED
