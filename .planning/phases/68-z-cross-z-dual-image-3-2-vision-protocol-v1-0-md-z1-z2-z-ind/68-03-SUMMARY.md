---
phase: 68-z-cross-z-dual-image-3-2-vision-protocol-v1-0-md-z1-z2-z-ind
plan: 03
subsystem: control-sequence
tags: [halcon, tcp-protocol, sequence-engine, cross-z, dual-image, vision-measurement]

# Dependency graph
requires:
  - phase: 68-01
    provides: "ZIndexA/ZIndexB int fields on DualImageEdgeDistanceMeasurement/DatumConfig (-1 sentinel), SkipReason.ZINDEX_MISCONFIGURED"
  - phase: 68-02
    provides: "InspectionSequence.FindActionIndicesByZIndex public multi-match helper, ProcessTest z=0/z>=1 StartAll/StartSubset wiring"
provides:
  - "InspectionSequence cross-Z HImage storage (m_dicCrossZImages) + Store/Take/Has(public)/Clear(private, Dispose) helpers, wired into ResetCycleState() (D-02/D-02a/D-03)"
  - "InspectionSequence.GetExecutionZIndex()/DoesZIndexExistInRecipe() public wrappers for cross-class consumption by Action_FAIMeasurement"
  - "Action_FAIMeasurement.TryGrabOrLoadFaiDualImages live-image-first fix (D-08 bug fix)"
  - "Action_FAIMeasurement cross-Z capture/injection pipeline (ProcessCrossZCaptureTick/TryExecuteCrossZMeasurement) + IsZIndexMisconfigured/MarkMeasurementZIndexMisconfigured D-05 gate"
  - "InspectionSequence.GetMeasurementCompletionZIndex/ShotHasCrossZMeasurementCompletingAt — measurement-index-aware response gate (AggregateIndexFais/AddFaiResult), closing the BLOCKER where completion-index reporting depended on shot.ZIndex recipe convention"
affects: [68-04, 68-05]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Owned-clone image cache keyed by string (ShotName|MeasurementName + role suffix _ZA/_ZB) mirroring ShotConfig._image/SetImage/GetImage contract, applied at the InspectionSequence (cycle-scoped) level instead of per-Shot"
    - "Public cross-class wrapper methods around private single-purpose helpers (GetExecutionZIndex wraps ParseCurrentZIndex, DoesZIndexExistInRecipe wraps FindShotByZIndex) — same convention Plan 02 established for FindActionIndicesByZIndex"
    - "Measurement-level completion-index gate (GetMeasurementCompletionZIndex) decouples TCP response scoping from Shot-level ZIndex, making per-measurement reporting index deterministic and independent of recipe authoring convention"

key-files:
  created: []
  modified:
    - WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs
    - WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs

key-decisions:
  - "Cross-Z storage helpers (StoreCrossZImage/TakeCrossZImageCopy/HasCrossZImage) made public rather than the private literally specified in Task 1's action text, because Action_FAIMeasurement (a different class) must call them directly during EStep.Measure — mirrors Plan 02's FindActionIndicesByZIndex precedent (ApplyShotLights/TurnOffShotLights convention)"
  - "Added two new public wrappers not explicitly named in Task 1 (GetExecutionZIndex, DoesZIndexExistInRecipe) so Action_FAIMeasurement can determine the current $TEST z_index and validate ZIndexA/B existence without duplicating InspectionSequence's private ParseCurrentZIndex/FindShotByZIndex logic"
  - "Cross-Z storage key = ShotName + '|' + MeasurementName (fallback TypeName), not MeasurementName alone — avoids collisions if two different Shots happen to reuse the same measurement name in the same cycle"
  - "Added a defensive 'bRelevant' check in ProcessCrossZCaptureTick (current z_index must equal this specific measurement's ZIndexA or ZIndexB) even though the phase's single real-world example (SHOT_E5) only has one cross-Z pair per Shot — protects against a Shot owning multiple different cross-Z measurement pairs where an unrelated tick would otherwise corrupt state"
  - "Cross-Z capture-failure case (ShotParam.GetImage() unexpectedly null) reuses the existing SkipReason.NO_IMAGE constant rather than inventing a new skip reason, since the semantic (no image available for this measurement) is identical to the existing no-image path"
  - "CROSS_Z_UNSET const added to InspectionSequence.cs (mirroring Action_FAIMeasurement's UNSET_ZINDEX) instead of leaving the -1 sentinel as a literal in Task 4's two new helpers — required by D-09's magic-number-to-const rule"
requirements-completed: [PROTO-Z-CROSS]

# Metrics
duration: ~40min
completed: 2026-07-22
---

# Phase 68 Plan 03: Cross-Z Storage + Capture/Injection + Response Gate Summary

**Cross-Z HImage cache on InspectionSequence (Dispose-on-z=0-reset) + Action_FAIMeasurement capture-at-Z1/inject-and-execute-at-Z2 pipeline (unchanged TryExecute) + D-08 live-image-priority bugfix + measurement-index-aware AggregateIndexFais/AddFaiResult response gate that reports cross-Z FAI items only at the completion z_index regardless of the owning Shot's own ZIndex.**

## Performance

- **Duration:** ~40 min
- **Tasks:** 4/4 completed (+ 1 follow-up D-09 compliance fix)
- **Files modified:** 2

## Accomplishments
- `InspectionSequence` now owns a cycle-scoped `Dictionary<string, HImage>` cross-Z image cache (`m_dicCrossZImages`) with `StoreCrossZImage`/`TakeCrossZImageCopy`/`HasCrossZImage` (public, owned-clone contract identical to `ShotConfig._image`/`SetImage`/`GetImage`) and a private `ClearCrossZImages()` that Disposes every entry, wired into `ResetCycleState()` (the existing z_index=0 cycle-start reset hook) so the store is never manually cleared elsewhere and can never leak across cycles even if Z2 never arrives (PLC abort/skip).
- `TryGrabOrLoadFaiDualImages` (D-08 fix): `pathA` resolution is now a 3-tier if/else-if/else — `ShotParam.HasImage` (live-grabbed image, taken via `GetImage()` clone) is checked **first**, before `TeachingImagePath_Horizontal` and `SimulImagePath`. Previously the live-grabbed image was never consulted and imageA was always reloaded from a static file path even mid-cycle.
- New cross-Z capture/injection pipeline in `Action_FAIMeasurement`: `ProcessCrossZCaptureTick` stashes the current tick's already-grabbed `ShotParam.GetImage()` clone into the InspectionSequence store under a role key (`_ZA` if this tick's z_index equals `ZIndexA`, `_ZB` if it equals `ZIndexB`) — no new grab call, reusing the image `EStep.DatumPhase` already captured inside the existing `GrabSyncLock` scope (lock order preserved per `shared-lighthandler-race`). Once both roles are present, `TryExecuteCrossZMeasurement` takes both clones back out, injects them into `RuntimeImageA`/`RuntimeImageB`, and calls the **unmodified** `DualImageEdgeDistanceMeasurement.TryExecute` exactly once (algorithm untouched, per D-02a). The first (non-completion) tick captures only — no NG, no report, `measuredCount` still increments to keep loop accounting consistent; the completion tick runs `EvaluateJudgement` normally.
- D-05 explicit-NG gate: `IsZIndexMisconfigured` + `MarkMeasurementZIndexMisconfigured` flag `ZIndexA==ZIndexB`, single-set (-1/non‑1 mixed), or a reference to a non-existent recipe z_index as `SkipReason.ZINDEX_MISCONFIGURED` — mirrors the existing `MarkMeasurementDatumRefMissing` pattern (no silent fallback). Recipes with `ZIndexA`/`ZIndexB` both unset (-1) never enter this branch at all (D-07 regression: 0).
- BLOCKER fix: `AggregateIndexFais`/`AddFaiResult` response serialization is now measurement-index-aware. New `GetMeasurementCompletionZIndex` returns `max(ZIndexA, ZIndexB)` for cross-Z measurements (matching the completion-index definition used during execution) and `shot.ZIndex` for everything else. New `ShotHasCrossZMeasurementCompletingAt` extends `AggregateIndexFais`'s in-scope condition (`bZMatch || bCrossZCompletesHere`) so a cross-Z measurement's owning Shot is included in the completion-index response even when that index differs from the Shot's own `ZIndex`. `AddFaiResult` gates each measurement individually by `GetMeasurementCompletionZIndex(meas, shot) == nZIndex`, so a cross-Z FAI item is excluded from non-completion-index responses and included only at the completion index — this is now guaranteed at the code level, independent of the `shot.ZIndex == ZIndexB` recipe-authoring convention the plan flagged as fragile. Non-cross-Z recipes are unaffected (`GetMeasurementCompletionZIndex` always returns `shot.ZIndex`, `ShotHasCrossZMeasurementCompletingAt` always returns `false` → in-scope/`nMatchedShots`/`AddFaiResult` behavior stays byte-identical).

## Task Commits

Each task was committed atomically:

1. **Task 1: InspectionSequence cross-Z image storage + Store/Take/Clear(Dispose) + ResetCycleState wiring (D-02/D-02a/D-03)** - `192a099` (feat)
2. **Task 2: D-08 bugfix — TryGrabOrLoadFaiDualImages live image priority** - `b28beca` (fix)
3. **Task 3: ZIndexA/B cross-Z capture/injection + Z1 pending + D-05 gate (D-02a/D-05)** - `8dbfab0` (feat)
4. **Task 4: measurement-index-aware response gate (AggregateIndexFais/AddFaiResult, BLOCKER)** - `ac5b05b` (feat)
5. **Follow-up: D-09 magic-number compliance fix in Task 4 helpers** - `c03e81f` (fix)

**Plan metadata:** (this commit, see below)

## Files Created/Modified
- `WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs` - Cross-Z `m_dicCrossZImages` store + `StoreCrossZImage`/`TakeCrossZImageCopy`/`HasCrossZImage` (public)/`ClearCrossZImages` (private, wired into `ResetCycleState`); `GetExecutionZIndex`/`DoesZIndexExistInRecipe` public wrappers; `GetMeasurementCompletionZIndex`/`ShotHasCrossZMeasurementCompletingAt` + extended `AggregateIndexFais` in-scope condition + `AddFaiResult(packet, fai, shot, nZIndex)` completion-index gate; `CROSS_Z_UNSET` const.
- `WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs` - `TryGrabOrLoadFaiDualImages` D-08 live-image-priority fix; `UNSET_ZINDEX`/`CROSS_Z_ROLE_SUFFIX_A`/`CROSS_Z_ROLE_SUFFIX_B` consts; `IsZIndexMisconfigured`/`MarkMeasurementZIndexMisconfigured` (D-05); `BuildCrossZMeasurementKey`/`ProcessCrossZCaptureTick`/`TryExecuteCrossZMeasurement` (D-02a); new gate + branch wired into the `EStep.Measure` per-measurement loop right after the existing `IsDatumRefUnresolvable` gate.

## Decisions Made
- Cross-Z storage helpers made `public` (not the `private` literally specified in Task 1) because `Action_FAIMeasurement` — a different class — must call them directly; this follows the exact precedent Plan 02 already established for `FindActionIndicesByZIndex`.
- Added `GetExecutionZIndex`/`DoesZIndexExistInRecipe` as new public wrappers (not explicitly named in the plan's Task 1 acceptance criteria) since Task 3's cross-Z capture logic and D-05 misconfiguration gate both need read access to InspectionSequence-owned recipe/z_index state from `Action_FAIMeasurement`.
- Cross-Z storage key includes the owning Shot's name (`ShotName|MeasurementName`) rather than just the measurement name, to avoid cross-Shot key collisions within the same cycle.
- Added a `bRelevant` guard in `ProcessCrossZCaptureTick` so a tick that doesn't match either `ZIndexA` or `ZIndexB` for a *specific* cross-Z measurement is a no-op rather than corrupting state — a safety net beyond the plan's explicit scope (the real recipe example has one cross-Z pair per Shot, but a Shot with multiple different cross-Z pairs would otherwise misbehave).
- Reused `SkipReason.NO_IMAGE` for the cross-Z capture-failure case (rather than adding a new skip reason) since the semantic is identical to the existing no-image path.
- Added `CROSS_Z_UNSET` const to `InspectionSequence.cs` mirroring `Action_FAIMeasurement`'s `UNSET_ZINDEX`, replacing a bare `-1` literal introduced in Task 4's two new helpers, per D-09's magic-number-to-const requirement.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Made cross-Z storage helpers public instead of private**
- **Found during:** Task 1 (cross-Z storage), confirmed necessary during Task 3 (Action_FAIMeasurement consumer)
- **Issue:** Task 1's acceptance criteria literally says "private 헬퍼 3개" for `StoreCrossZImage`/`TakeCrossZImageCopy`/`HasCrossZImage`, but Task 3 requires `Action_FAIMeasurement` (a different class) to call them directly during `EStep.Measure`. A `private` member cannot be called from a different class — this would not compile.
- **Fix:** Made `StoreCrossZImage`, `TakeCrossZImageCopy`, `HasCrossZImage` `public` (kept `ClearCrossZImages` `private` since it's only called internally from `ResetCycleState`). Matches the exact convention Plan 02 already established for `FindActionIndicesByZIndex`/`ApplyShotLights`/`TurnOffShotLights`.
- **Files modified:** `WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs`
- **Verification:** msbuild Debug/x64 build PASS after Task 3's cross-class calls were wired in.
- **Committed in:** `192a099` (Task 1 commit)

**2. [Rule 3 - Blocking] Added GetExecutionZIndex/DoesZIndexExistInRecipe public wrappers**
- **Found during:** Task 3 (cross-Z capture tick logic + D-05 misconfiguration gate)
- **Issue:** Task 3's action text explicitly says to use "m_nCurrentZIndex/ParseCurrentZIndex" to obtain the current tick's z_index, and to check whether a referenced z_index exists in the recipe — but both are private members of `InspectionSequence`, and the consuming code lives in `Action_FAIMeasurement` (different class).
- **Fix:** Added `public int GetExecutionZIndex()` (thin wrapper around private `ParseCurrentZIndex()`) and `public bool DoesZIndexExistInRecipe(int nZIndex)` (thin wrapper around private `FindShotByZIndex(nZIndex) != null`) to `InspectionSequence.cs`.
- **Files modified:** `WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs`
- **Verification:** msbuild Debug/x64 build PASS; `Action_FAIMeasurement.ProcessCrossZCaptureTick`/`IsZIndexMisconfigured` compile and call these successfully.
- **Committed in:** `192a099` (Task 1 commit, added proactively since Task 3 was known to need it)

**3. [Rule 2 - Missing Critical] Added bRelevant guard for tick-vs-measurement z_index mismatch**
- **Found during:** Task 3 (cross-Z capture tick logic)
- **Issue:** The plan's Task 3 design assumes a Shot's cross-Z measurement(s) are invoked exactly at ticks matching their own `ZIndexA`/`ZIndexB`, but the `EStep.Measure` loop iterates every measurement in the Shot's `FAIList` on every invoked tick. If a Shot owned two different cross-Z measurements with different z-index pairs, a tick relevant to one pair but not the other would otherwise be treated as if it were relevant, corrupting the unrelated measurement's capture state.
- **Fix:** `ProcessCrossZCaptureTick` computes `bRelevant = (nCurZ == ZIndexA) || (nCurZ == ZIndexB)` for the specific measurement being processed and no-ops (no capture, no report, no state change) when neither matches.
- **Files modified:** `WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs`
- **Verification:** msbuild Debug/x64 build PASS. Not exercised by the single-cross-Z-pair-per-Shot real recipe example, but defends against a plausible multi-pair configuration without changing behavior for the common case.
- **Committed in:** `8dbfab0` (Task 3 commit)

**4. [Rule 1 - Bug/Compliance] Replaced magic -1 literal with CROSS_Z_UNSET const in Task 4 helpers**
- **Found during:** Post-Task-4 self-review against the plan's D-09 must-have ("매직넘버 const 준수")
- **Issue:** `GetMeasurementCompletionZIndex` and `ShotHasCrossZMeasurementCompletingAt` (both new `InspectionSequence.cs` code, in scope for D-09) used a bare `-1` literal for the ZIndexA/B unset sentinel instead of a named constant, violating the plan's explicit D-09 requirement.
- **Fix:** Added `private const int CROSS_Z_UNSET = -1;` alongside the existing `DATUM_Z_INDEX` const and replaced both literal usages.
- **Files modified:** `WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs`
- **Verification:** msbuild Debug/x64 build PASS, no new warnings.
- **Committed in:** `c03e81f` (separate follow-up commit after Task 4's `ac5b05b`)

---

**Total deviations:** 4 auto-fixed (2 blocking access-modifier/wrapper additions required for cross-class compilation, 1 missing-critical safety guard, 1 D-09 compliance fix)
**Impact on plan:** All four were necessary for correctness or explicit plan compliance (D-09). No scope creep — no architectural changes, no new files, no behavior changes to non-cross-Z recipes.

## Issues Encountered
- None beyond the deviations documented above. `msbuild` located at `C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe`, invoked via Git Bash with single-dash `-p:`/`-t:`/`-v:` switches (per Plan 02's documented Git Bash argument-mangling workaround).

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness
- Plan 04 (Datum-level `VerticalTwoHorizontalDualImage` cross-Z wiring, per CONTEXT D-06) can reuse the same `InspectionSequence` cross-Z store (`StoreCrossZImage`/`TakeCrossZImageCopy`/`HasCrossZImage`/`ClearCrossZImages`) — the store is keyed by an arbitrary string, not measurement-type-specific, so Datum-level cross-Z capture only needs its own key-building convention (e.g., `ShotName|DatumName` + role suffix) and its own completion-index definition, mirroring `BuildCrossZMeasurementKey`/`ProcessCrossZCaptureTick`/`TryExecuteCrossZMeasurement`.
- Plan 05 (SIMUL UAT) has the full execution + response gate implemented and build-verified, but **no runtime SIMUL_MODE test was performed in this plan** — the scenarios flagged in the plan's `<verification>` section (Z1 capture-only/no-report, Z2 distance-report, completion-index-only response inclusion, `ZINDEX_MISCONFIGURED` NG on `ZIndexA==ZIndexB`, and D-07 regression-zero on the existing SHOT_E5/`main.ini` recipe) are all still pending Plan 05's SIMUL run — this plan's verification is code-level (msbuild + reasoning) only, per the plan's own task-level `<verify>` blocks.
- The `shared-lighthandler-race` lock-order constraint (`GrabSyncLock` outer, `cam.GrabLock` inner) was respected structurally — no new grab call was added; cross-Z capture reuses `ShotParam.GetImage()`, a clone of the image `EStep.DatumPhase` already grabbed inside the existing lock scope. This should be spot-checked during Plan 05's UAT alongside the other lock-order-sensitive paths already tracked in `.planning/debug/shared-lighthandler-race.md` (still `awaiting_human_verify`).

## Self-Check: PASSED

- FOUND: WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs
- FOUND: WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs
- FOUND commit 192a099 (Task 1)
- FOUND commit b28beca (Task 2)
- FOUND commit 8dbfab0 (Task 3)
- FOUND commit ac5b05b (Task 4)
- FOUND commit c03e81f (D-09 follow-up fix)
- Content verified: `m_dicCrossZImages`/`StoreCrossZImage`/`TakeCrossZImageCopy`/`HasCrossZImage`/`ClearCrossZImages`/`GetMeasurementCompletionZIndex`/`ShotHasCrossZMeasurementCompletingAt`/`CROSS_Z_UNSET` present in InspectionSequence.cs; `UNSET_ZINDEX`/`IsZIndexMisconfigured`/`MarkMeasurementZIndexMisconfigured`/`ProcessCrossZCaptureTick`/`TryExecuteCrossZMeasurement`/`BuildCrossZMeasurementKey` present in Action_FAIMeasurement.cs; msbuild Debug/x64 build PASS after every task with no new warnings (only pre-existing CS0618 obsolete-member warnings unrelated to this plan).

---
*Phase: 68-z-cross-z-dual-image-3-2-vision-protocol-v1-0-md-z1-z2-z-ind*
*Completed: 2026-07-22*
