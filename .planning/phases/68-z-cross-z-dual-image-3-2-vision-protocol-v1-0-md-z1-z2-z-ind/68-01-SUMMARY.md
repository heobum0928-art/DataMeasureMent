---
phase: 68-z-cross-z-dual-image-3-2-vision-protocol-v1-0-md-z1-z2-z-ind
plan: 01
subsystem: vision-measurement
tags: [halcon, ini-serialization, propertygrid, cross-z, dual-image, backward-compat]

# Dependency graph
requires: []
provides:
  - "ZIndexA/ZIndexB int fields on DualImageEdgeDistanceMeasurement (measurement-level, default -1 unset)"
  - "ZIndexA/ZIndexB int fields on DatumConfig (Datum-level, default -1 unset), Category(Datum|ImageSource)"
  - "Load() overrides on both classes restoring -1 sentinel when INI key absent (old-recipe compat)"
  - "IsHiddenForAlgorithm sync — ZIndexA/ZIndexB hidden for TwoLineIntersect/CircleTwoHorizontal/VerticalTwoHorizontal, exposed only for VerticalTwoHorizontalDualImage"
  - "SkipReason.ZINDEX_MISCONFIGURED constant — single-source NG reason for cross-Z misconfiguration"
affects: [68-02, 68-03, 68-04, 68-05]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "ContainsKey-guard Load() override for Int32 sentinel restoration (mirrors MeasurementBase.MeasCorrectionFactor / CameraSlaveParam.CorrectionFactor pattern)"

key-files:
  created: []
  modified:
    - WPF_Example/Custom/Sequence/Inspection/Measurements/DualImageEdgeDistanceMeasurement.cs
    - WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs
    - WPF_Example/Custom/Sequence/Inspection/SkipReason.cs

key-decisions:
  - "ZIndexA/ZIndexB fields live on the measurement/Datum class itself, not on Shot — one measurement/Datum references two z_index positions (per CONTEXT decision, not a two-Shot split)"
  - "Sentinel -1 chosen over 0 because ParamBase.Load's Int32 reflection path overwrites missing INI keys with 0, which would be indistinguishable from a legitimate z_index=0"
  - "New Load() override added to DatumConfig (previously had none) rather than relying on EnsurePerRoiDefaults, since that hook only runs at find-time and cannot distinguish '0 loaded from missing key' from 'user explicitly set 0'"

patterns-established:
  - "ZIndexA/ZIndexB PropertyGrid visibility mirrors TeachingImagePath_Vertical exactly — hidden for TLI/CTH/VTH, exposed only for VerticalTwoHorizontalDualImage"

requirements-completed: [PROTO-Z-CROSS]

# Metrics
duration: 15min
completed: 2026-07-22
---

# Phase 68 Plan 01: Cross-Z Data Model + Backward-Compat Contract Summary

**Added ZIndexA/ZIndexB int fields (default -1 sentinel) to DualImageEdgeDistanceMeasurement and DatumConfig with Load() override restoring the sentinel on old-recipe load, plus a single-source SkipReason.ZINDEX_MISCONFIGURED constant for downstream cross-Z NG handling.**

## Performance

- **Duration:** ~15 min
- **Tasks:** 3/3 completed
- **Files modified:** 3

## Accomplishments
- `DualImageEdgeDistanceMeasurement` now carries `ZIndexA`/`ZIndexB` (Category "Image|DualImage", default -1) plus a `Load` override that restores -1 when the INI key is absent, delegating `MeasCorrectionFactor` restoration to `base.Load()` unchanged.
- `DatumConfig` gained matching `ZIndexA`/`ZIndexB` (Category "Datum|ImageSource", default -1), a brand-new `Load` override (DatumConfig previously had none — `EnsurePerRoiDefaults` is find-time only and can't distinguish "0 from missing key" vs. "user-set 0"), and `IsHiddenForAlgorithm` updated in all three non-DualImage cases (TwoLineIntersect, CircleTwoHorizontal, VerticalTwoHorizontal) so the fields are hidden everywhere except `VerticalTwoHorizontalDualImage` — mirroring `TeachingImagePath_Vertical`'s existing visibility rule exactly.
- `SkipReason.ZINDEX_MISCONFIGURED = "ZINDEX_MISCONFIGURED"` added as the single-source NG reason string for cross-Z misconfiguration (e.g. ZIndexA==ZIndexB, or a reference to a non-existent z_index), for Plan 03/04 to consume at execution time.

## Task Commits

Each task was committed atomically:

1. **Task 1: DualImageEdgeDistanceMeasurement ZIndexA/ZIndexB + Load override** - `f3f3a49` (feat)
2. **Task 2: DatumConfig ZIndexA/ZIndexB + Load override + IsHiddenForAlgorithm sync** - `13d2347` (feat)
3. **Task 3: SkipReason.ZINDEX_MISCONFIGURED constant** - `956ea4a` (feat)

**Plan metadata:** (this commit, see below)

## Files Created/Modified
- `WPF_Example/Custom/Sequence/Inspection/Measurements/DualImageEdgeDistanceMeasurement.cs` - Added `ZIndexA`/`ZIndexB` int properties (-1 default) + `Load` override with ContainsKey guard restoring -1 on missing key; added `using ReringProject.Utility;` for `IniFile`/`IniSection`.
- `WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs` - Added `ZIndexA`/`ZIndexB` int properties (-1 default) + new `Load` override (ContainsKey guard); added ZIndexA/ZIndexB hide lines to the three non-DualImage `IsHiddenForAlgorithm` cases.
- `WPF_Example/Custom/Sequence/Inspection/SkipReason.cs` - Added `ZINDEX_MISCONFIGURED` constant after `DATUM_REF_MISSING`.

## Decisions Made
- Sentinel value `-1` (not `0`) chosen because `ParamBase.Load`'s Int32 reflection path silently writes `0` for any missing INI key — using `-1` as "unset" avoids collision with a legitimate `z_index=0` (Datum shot convention per RESEARCH: `z_index=0` is a valid, meaningful value in this protocol, so `0` cannot double as sentinel).
- `DatumConfig.Load` override is newly introduced (class had none before) rather than reusing `EnsurePerRoiDefaults`, because that hook runs at find-time (UI action), not at INI load time, and can't distinguish "0 restored from a missing key" from "user explicitly configured 0".
- `IsHiddenForAlgorithm` updated in lockstep with `TeachingImagePath_Vertical`'s existing hide rules so ZIndexA/ZIndexB visibility exactly matches the existing DualImage-only field, avoiding a second, divergent visibility rule to maintain.

## Deviations from Plan

None - plan executed exactly as written. All three tasks matched their `<action>` specs precisely (field placement, Load override shape, hide-case additions, constant naming).

## Issues Encountered

None. `DualImageEdgeDistanceMeasurement.cs` required an additional `using ReringProject.Utility;` import for `IniFile`/`IniSection` types (not previously imported in that file) — this was a routine consequence of adding the `Load` override and not a deviation from the planned action; `DatumConfig.cs` already had that using directive.

## Next Phase Readiness

- Wave 2 (Plan 02, execution-scope filter) can now read `ZIndexA`/`ZIndexB` off both `DualImageEdgeDistanceMeasurement` and `DatumConfig` to determine which z_index positions the owning Shot must execute at.
- Wave 3/4 (Plan 03/04, cross-Z capture) have `SkipReason.ZINDEX_MISCONFIGURED` available as the NG-reason constant for explicit misconfiguration detection (ZIndexA==ZIndexB, non-existent z_index) — actual validation logic is out of scope for this plan (D-05, deferred to Plan 03/04 per plan objective).
- Backward-compat regression (D-07) is not yet verified against a real old recipe — deferred to Plan 05 SIMUL UAT (SHOT_E5 / `D:\Data\Recipe\FAI_1\main.ini` load), per this plan's `<verification>` section. No blocker for Plan 02.

---
*Phase: 68-z-cross-z-dual-image-3-2-vision-protocol-v1-0-md-z1-z2-z-ind*
*Completed: 2026-07-22*
