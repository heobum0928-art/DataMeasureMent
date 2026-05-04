---
phase: 14-datum-carry-over-circle-vertical-roi-2-out-of-range-ux
fixed_at: 2026-04-27T00:00:00Z
review_path: .planning/phases/14-datum-carry-over-circle-vertical-roi-2-out-of-range-ux/14-REVIEW.md
iteration: 1
findings_in_scope: 2
fixed: 2
skipped: 0
status: all_fixed
---

# Phase 14: Code Review Fix Report

**Fixed at:** 2026-04-27
**Source review:** .planning/phases/14-datum-carry-over-circle-vertical-roi-2-out-of-range-ux/14-REVIEW.md
**Iteration:** 1

**Summary:**
- Findings in scope: 2 (Critical+Warning only; 5 Info findings out of scope)
- Fixed: 2
- Skipped: 0

## Fixed Issues

### WR-01: Datum Rect ROI resize silently drops geometry write-back, then auto-reteach uses stale config

**Files modified:** `WPF_Example/UI/ContentItem/MainView.xaml.cs`
**Commit:** 9e0369b
**Applied fix:** Added an explicit `else if (e.Shape == RoiShape.Rect)` branch to `HandleDatumRoiResize` that emits a Trace log (`"Datum Rect resize ignored (Phase 14 scope): id=..."`) and `return`s early. This prevents the dispatcher-deferred `InvokeTryTeachDatumForEdit` from running auto-reteach against unchanged `DatumConfig.Vertical_*`/`Horizontal_*_*` fields. Diagnostic comment notes that future Vertical/Horizontal Edit handle exposure should add Rect write-back here and remove the early return. K&R brace style preserved (UI file convention). `//260427 hbk` annotation per project convention.

### WR-02: `RunPhiSmokeTest` compares two identical expressions, so delta is always 0

**Files modified:** `WPF_Example/Halcon/Algorithms/VisionAlgorithmService.cs`
**Commit:** a920efe
**Applied fix:** Replaced the dead `expectedRow`/`expectedCol` (which used the same polar formula as `rectRow`/`rectCol`) with a hand-precomputed reference table covering the four canonical thetas: 0° (right, +col), 90° (up, -row), 180° (left, -col), 270° (down, +row). Used a `double[][]` jagged array of `(thetaDeg, expRow, expCol)` triples to stay compatible with C# 7.2 (no value tuples needed beyond what's already in the project). If a sin/cos sign convention regresses in the formula under test, delta will diverge from 0 and surface in the Trace log. Allman brace style preserved (Halcon file convention). `//260427 hbk` annotation per project convention.

---

_Fixed: 2026-04-27_
_Fixer: Claude (gsd-code-fixer)_
_Iteration: 1_
