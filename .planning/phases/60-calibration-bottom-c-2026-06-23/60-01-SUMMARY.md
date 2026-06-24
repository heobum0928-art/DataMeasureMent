---
phase: 60-calibration-bottom-c-2026-06-23
plan: "01"
subsystem: SystemSetting / ETHERNET_VISION INI
tags: [persistence, calibration, picker-center, ethernet-vision, av-05]
dependency_graph:
  requires: []
  provides: [SystemSetting.PickerCenterRow, SystemSetting.PickerCenterCol]
  affects: [Plan 60-02 (AlignShapeMatchService reads), Plan 60-03 (PickerCenterCalibrationService writes)]
tech_stack:
  added: []
  patterns: [Phase-58 AfterLoad default guard pattern (mirror)]
key_files:
  created: []
  modified:
    - WPF_Example/Custom/SystemSetting.cs
decisions:
  - "D-04: PickerCenterRow/Col stored in [ETHERNET_VISION] INI as machine-level HW cal result (not per-recipe). Default 0 = uncalibrated sentinel."
  - "RestorePickerCenterDefault() is a documented no-op guard: 0 is the correct initial value so no restore is needed; method exists to document intent and ease future non-zero default introduction."
metrics:
  duration: "5 minutes"
  completed: "2026-06-24"
  tasks_completed: 1
  tasks_total: 1
  files_modified: 1
---

# Phase 60 Plan 01: Picker Center INI Persistence (D-04) Summary

**One-liner:** Added `PickerCenterRow`/`PickerCenterCol` double properties to `[ETHERNET_VISION]` INI section with `RestorePickerCenterDefault()` AfterLoad guard, mirroring the Phase 58 `EthernetPixelResolution` pattern; default 0.0 = uncalibrated sentinel.

## Tasks Completed

| # | Name | Commit | Files |
|---|------|--------|-------|
| 1 | Add PickerCenterRow/Col INI properties + AfterLoad guard | 689a831 | WPF_Example/Custom/SystemSetting.cs |

## What Was Built

`WPF_Example/Custom/SystemSetting.cs` gains:

1. Two new `[Category("ETHERNET_VISION")]` `double` properties — `PickerCenterRow` and `PickerCenterCol` — both defaulting to `0.0` (uncalibrated). Placed after `EthernetPixelResolution` to maintain INI section grouping.
2. `RestorePickerCenterDefault()` private method — no-op guard documenting that `(0,0)` is the correct uncalibrated initial state. Placed immediately after `RestoreEthernetVisionDefault()` for symmetry.
3. `AfterLoad()` body extended with `RestorePickerCenterDefault()` call (third restore call after `RestorePcRoleDefault` and `RestoreEthernetVisionDefault`).

All additions use `//260624 hbk Phase 60` markers per project convention.

## Deviations from Plan

None — plan executed exactly as written.

## Integration Points

- **Plan 60-03 (`PickerCenterCalibrationService`):** writes `SystemSetting.Handle.PickerCenterRow/Col` after circle fit, then calls `SystemSetting.Handle.Save()`.
- **Plan 60-02 (`AlignShapeMatchService` correction):** reads `SystemSetting.Handle.PickerCenterRow/Col` to determine picker rotation center for rigid transform.
- **Legacy INI compatibility:** A `Setting.ini` predating Phase 60 will load both keys as `0.0` (ParamBase reflection behavior for missing double keys), which equals the uncalibrated sentinel — no crash, no data loss.

## Known Stubs

None — this plan is a pure persistence layer addition with no UI or algorithm logic.

## Threat Flags

None — as per plan threat model T-60-01 (accepted: local machine-level config, no remote write path, tampered value only degrades local align accuracy and is re-derivable by recalibration).

## Self-Check: PASSED

- [x] `WPF_Example/Custom/SystemSetting.cs` exists and contains `PickerCenterRow` (line 100), `PickerCenterCol` (line 103), `RestorePickerCenterDefault` (line 69), AfterLoad call at line 40
- [x] Commit `689a831` present in git log
- [x] No file deletions in commit
- [x] `[Category("ETHERNET_VISION")]` on both new properties
- [x] `//260624 hbk Phase 60` markers present
