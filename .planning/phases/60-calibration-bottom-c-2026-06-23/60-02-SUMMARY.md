---
phase: 60-calibration-bottom-c-2026-06-23
plan: 02
subsystem: align-vision
tags: [halcon, shape-match, picker-center, bottom-align, rigid-transform, ethernet-vision]

requires:
  - phase: 60-01
    provides: SystemSetting.PickerCenterRow/Col (D-04 INI properties, uncalibrated = 0.0)
  - phase: 59-vision-algorithm-b-2026-06-23
    provides: AlignShapeMatchService Run() midpoint-offset + angle_lx Bottom branch (D-03'/05')

provides:
  - ApplyPickerCenterCorrection helper: rigid HomMat2dRotate about picker center wired into Run() Bottom branch
  - Calibrated path: Bottom offset rotated about PickerCenterRow/Col by thetaDeg
  - Uncalibrated fallback (0,0): Bottom result byte-identical to Phase 59, zero regression

affects: [60-03, 61-ui-phase, phase-62-tcp-result]

tech-stack:
  added: []
  patterns:
    - "HTuple out-param try/catch/finally dispose for HALCON HomMat2d* + AffineTransPoint2d"
    - "PICKER_ROTATION_SIGN const to parameterize sign/convention pending UAT"
    - "Epsilon guard (PICKER_CENTER_ZERO_EPS=1e-6) for uncalibrated (0,0) detection"

key-files:
  created: []
  modified:
    - WPF_Example/Custom/EthernetVision/AlignShapeMatchService.cs

key-decisions:
  - "D-05: Bottom correction expressed as HomMat2dRotate(dθ, pickerRow, pickerCol) — part rotates about picker center"
  - "Fallback: PickerCenterRow==0 && PickerCenterCol==0 → return input offset unchanged (Phase 59 behavior, zero regression)"
  - "D-02 corrected: no TryFindCenter added — per-step cal jig center is fitted inside PickerCenterCalibrationService (Plan 60-03)"
  - "PICKER_ROTATION_SIGN=+1.0 default — final sign/convention confirmed at Phase 61 UAT with real picker"
  - "Tray branch: offset unchanged from Phase 59 (dCol*resMm / dRow*resMm, no picker-center coupling)"

requirements-completed: [AV-05]

duration: 15min
completed: 2026-06-24
---

# Phase 60 Plan 02: Picker-Center-Aware Bottom Correction Summary

**Bottom align correction expressed as rigid HomMat2dRotate about calibrated picker center (D-05); uncalibrated and Tray paths byte-identical to Phase 59.**

## Performance

- **Duration:** ~15 min
- **Started:** 2026-06-24T00:00:00Z
- **Completed:** 2026-06-24T00:15:00Z
- **Tasks:** 1
- **Files modified:** 1

## Accomplishments
- Added `PICKER_CENTER_ZERO_EPS` and `PICKER_ROTATION_SIGN` consts to the class const block
- Added `ApplyPickerCenterCorrection` private helper: reads `SystemSetting.Handle.PickerCenterRow/Col`, falls back immediately when uncalibrated, otherwise builds a HomMat2dIdentity → HomMat2dRotate about picker center → AffineTransPoint2d; all HTuple handles disposed in finally
- Wired helper into `Run()` Bottom branch only; Tray branch left untouched (`dCol*resMm`/`dRow*resMm`)
- Verified with plan's grep: ApplyPickerCenterCorrection + HomMat2dRotate + PickerCenterRow present; TryFindCenter absent

## Task Commits

1. **Task 1: Picker-center-aware Bottom correction + helper** - `6aa4ac8` (feat)

## Files Created/Modified
- `WPF_Example/Custom/EthernetVision/AlignShapeMatchService.cs` - Added 2 consts, `ApplyPickerCenterCorrection` helper, replaced Bottom branch in `Run()`

## Decisions Made
- Used `HTuple` local variables (not `new HTuple(...)` for output params) consistent with the existing project pattern in `ComputeAngleLx`
- `PICKER_ROTATION_SIGN = 1.0` as default; marked with `// TODO(Phase 61 UAT)` comment for real-picker sign confirmation
- `PICKER_CENTER_ZERO_EPS = 1e-6` tight epsilon to guard floating-point zero without false-triggering on legitimately small but valid coordinates
- Helper placed directly after `Run()` (before closing class brace), consistent with K&R brace style of this file

## Deviations from Plan

None - plan executed exactly as written. HALCON API signatures (`HomMat2dIdentity`, `HomMat2dRotate`, `AffineTransPoint2d`) matched the plan's interface spec.

## Issues Encountered
None.

## Known Stubs
- `PICKER_ROTATION_SIGN = 1.0` — rotation sign/convention is a parameterized placeholder. Confirmed value depends on real picker controller convention; finalized at Phase 61 UAT.

## Threat Surface Scan
No new network endpoints, auth paths, file access patterns, or schema changes introduced. T-60-02 (degenerate transform DoS) mitigated by try-catch + fallback. T-60-05 (tampered INI) accepted per plan.

## Next Phase Readiness
- `AlignShapeMatchService.ApplyPickerCenterCorrection` is wired and ready
- Phase 60-03 (`PickerCenterCalibrationService`) can now write `PickerCenterRow/Col` to `SystemSetting` and the Bottom align path will immediately apply the correction
- Phase 61 UAT must confirm `PICKER_ROTATION_SIGN` and overall sign convention with real picker hardware

---
*Phase: 60-calibration-bottom-c-2026-06-23*
*Completed: 2026-06-24*
