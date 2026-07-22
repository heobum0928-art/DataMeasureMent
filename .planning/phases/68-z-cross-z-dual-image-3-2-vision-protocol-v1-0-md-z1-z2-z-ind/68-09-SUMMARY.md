---
phase: 68-z-cross-z-dual-image-3-2-vision-protocol-v1-0-md-z1-z2-z-ind
plan: 09
subsystem: control-sequence
tags: [tcp-protocol, sequence-engine, cross-z, dual-image, last-index, single-source, bugfix]

# Dependency graph
requires:
  - phase: 68-07
    provides: "InspectionSequence.BuildCrossZDatumIndexSet/DatumConfigs cross-Z scaffolding, GetMeasurementCompletionZIndex/ShotHasCrossZMeasurementCompletingAt completion-index pattern this plan mirrors for Datum"
  - phase: 68-04
    provides: "DatumConfig.ZIndexA/ZIndexB fields, CROSS_Z_UNSET sentinel convention"
provides:
  - "InspectionSequence.GetDatumCompletionZIndex(DatumConfig) — single-source Datum completion-index helper, symmetric with GetMeasurementCompletionZIndex, reserved for GAP-3 (68-10) reuse"
  - "InspectionSequence.ComputeLastZIndex extended to max(owned shot.ZIndex, cross-Z measurement completion index, cross-Z Datum completion index) via new MaxCrossZCompletionZIndex/MaxShotCrossZMeasurementCompletionZIndex sub-helpers"
affects: [68-10, 68-11]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "'Last index' truth source must be widened alongside every new completion-index concept: ComputeLastZIndex previously only knew about owned shot.ZIndex; when GetMeasurementCompletionZIndex introduced a completion index independent of shot.ZIndex (Task 4/BLOCKER, prior wave), the last-index truth source became stale by construction until explicitly reconciled here — a lesson for any future third completion-index source (e.g. an eventual GAP-4) to fold into MaxCrossZCompletionZIndex rather than adding a fourth independent traversal."
    - "Datum completion-index helper made structurally symmetric with the pre-existing measurement completion-index helper (same bIsCrossZ gate, same CROSS_Z_UNSET-vs-max(ZIndexA,ZIndexB) branch shape) so a reader recognizes the pattern immediately and 68-10 can call it without re-deriving the gate."

key-files:
  created: []
  modified:
    - WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs

key-decisions:
  - "GetDatumCompletionZIndex placed adjacent to GetMeasurementCompletionZIndex (not adjacent to ComputeLastZIndex) despite ComputeLastZIndex being its primary current consumer — chosen for discoverability by GAP-3 (68-10), which the gap analysis explicitly names as needing this exact helper for its own Datum completion-index re-evaluation of the 'immediate F' contract. C# member ordering has no compile-time effect, so this is a pure readability/reuse-signaling choice."
  - "Split the crossZ-completion max computation into two sub-helpers (MaxCrossZCompletionZIndex, MaxShotCrossZMeasurementCompletionZIndex) rather than inlining the shot/FAI/measurement traversal directly into ComputeLastZIndex — required by the LOCKED control-sequence coding guideline's ~30-line-per-function ceiling (coding_guideline_scope), matching the same sub-function-extraction pattern 68-07 used for AddDatumTriggerActionIndex."
  - "MaxShotCrossZMeasurementCompletionZIndex's traversal structure intentionally mirrors ShotHasCrossZMeasurementCompletingAt's existing owned-shot/FAI/measurement loop and bIsCrossZ gate (same shape, different aggregation — max vs. equality-match) rather than being refactored into one shared generic traversal, since GetMeasurementCompletionZIndex itself (the actual completion-index computation) is already the single reused source of truth; only the enclosing loop shape repeats, which the gap analysis's LOCKED guideline concerns (duplicate *logic*, not duplicate *loop shape*) do not flag as the kind of duplication requiring extraction."
  - "Did not add the danger rule excluded by the gap analysis's Critique review (\"completion index > max shot.ZIndex ⇒ misconfigured\") anywhere in this plan's new code — CROSS-2 explicitly treats a completion index exceeding the sequence's max owned shot.ZIndex as the *normal*, expected case to fold into the last-index truth source, not a misconfiguration signal (that determination remains exclusively IsZIndexMisconfigured/IsDatumZIndexMisconfigured's responsibility per Plan 03/04/68-07)."

requirements-completed: [PROTO-Z-CROSS]

# Metrics
duration: ~15min
completed: 2026-07-22
---

# Phase 68 Plan 09: CROSS-2 (Last-Index Truth Source Includes Cross-Z Completion Index) Summary

**`ComputeLastZIndex` no longer looks only at the sequence's max owned `shot.ZIndex` — it now also folds in every cross-Z measurement's and cross-Z Datum's completion index, closing the risk of a cross-Z completion index beyond the max `shot.ZIndex` causing two P/F emissions in the same cycle.**

## Performance

- **Duration:** ~15 min
- **Tasks:** 1/1 completed
- **Files modified:** 1

## Accomplishments

- **CROSS-2 closed:** `ComputeLastZIndex(InspectionRecipeManager recipeManager)` (`InspectionSequence.cs`) previously computed `nMax` as the maximum `shot.ZIndex` across this sequence's owned Shots only. It now appends `nMax = System.Math.Max(nMax, MaxCrossZCompletionZIndex(recipeManager));` immediately before `return nMax;`, so the "last index" truth source consumed by `AddResponseV1Cycle`/`HandleDatumIndexResponse` (`bIsLastIndex = m_nCurrentZIndex >= m_nLastZIndex`) can no longer diverge from `GetMeasurementCompletionZIndex`'s completion index — the divergence the gap analysis identified as a general risk (T-68-10, present even without Side's dual-position layout, triggered by any single cross-Z measurement whose completion index exceeds the sequence's max owned `shot.ZIndex`).
- **`GetDatumCompletionZIndex(DatumConfig datum)` — single source for GAP-3 reuse:** new private method, structurally symmetric with the pre-existing `GetMeasurementCompletionZIndex`: returns `Math.Max(datum.ZIndexA, datum.ZIndexB)` when both are set (cross-Z Datum), otherwise `CROSS_Z_UNSET` (non-cross-Z Datum has no completion-index concept). Placed next to `GetMeasurementCompletionZIndex` for the same-pattern recognizability the gap analysis calls for, and reserved by comment for `68-10` (GAP-3, Datum "immediate F" re-evaluation at the completion index) to reuse directly rather than re-deriving the same gate.
- **`MaxCrossZCompletionZIndex(recipeManager)`** — sub-helper: takes the max of (a) `MaxShotCrossZMeasurementCompletionZIndex(shot)` across all recipe Shots, and (b) `GetDatumCompletionZIndex(datum)` (gated `!= CROSS_Z_UNSET`) across all of this sequence's `DatumConfigs`. Returns 0 when neither exists, which is the regression-safe floor `Math.Max(nMax, 0)` in `ComputeLastZIndex` preserves.
- **`MaxShotCrossZMeasurementCompletionZIndex(shot)`** — sub-helper: mirrors the existing owned-shot/FAI/measurement traversal shape of `ShotHasCrossZMeasurementCompletingAt` (same `bOwnedByThisSeq` gate, same `bIsCrossZ` gate on `DualImageEdgeDistanceMeasurement.ZIndexA/ZIndexB != CROSS_Z_UNSET`), but aggregates via `Math.Max(nMax, GetMeasurementCompletionZIndex(meas, shot))` across all cross-Z measurements instead of matching a single target index — computing "what is the highest completion index this owned shot's cross-Z measurements produce," not "does this shot complete at exactly nZIndex."
- **Function-length compliance (D-09/coding_guideline_scope):** `ComputeLastZIndex` stays at 30 lines (one new line added at the end); the new traversal logic was split into the two sub-helpers above rather than inlined, per the LOCKED control-sequence guideline's function-length ceiling explicitly called out in this plan's `<coding_guideline_scope>`.
- Build verified via `msbuild` Debug/x64: 0 errors, `DatumMeasurement.exe` produced.

## Task Commits

Each task was committed atomically:

1. **Task 1: ComputeLastZIndex extension + GetDatumCompletionZIndex single source** - `1c2d8c6` (feat)

**Plan metadata:** (this commit, see below)

## Files Created/Modified

- `WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs` — `ComputeLastZIndex` now folds in `MaxCrossZCompletionZIndex`; added `MaxCrossZCompletionZIndex`, `MaxShotCrossZMeasurementCompletionZIndex` (sub-helpers, adjacent to `ComputeLastZIndex`), and `GetDatumCompletionZIndex` (adjacent to `GetMeasurementCompletionZIndex`, for GAP-3/68-10 reuse).

## Decisions Made

- `GetDatumCompletionZIndex` was placed next to `GetMeasurementCompletionZIndex` rather than next to `ComputeLastZIndex` (its current sole caller via `MaxCrossZCompletionZIndex`) — deliberate discoverability choice for `68-10` (GAP-3), which the gap analysis names as needing exactly this helper. No compile-time effect either way (C# class members are order-independent).
- Did not implement the gap analysis's explicitly-excluded danger rule ("completion index exceeding max shot.ZIndex ⇒ misconfigured") anywhere in this plan — CROSS-2's whole premise is that this situation is a *normal, expected* case that the last-index truth source must accommodate, not a misconfiguration signal. Verified via re-reading the new code: no comparison of `MaxCrossZCompletionZIndex`'s result against a "should not exceed" threshold exists; it is purely folded into `Math.Max`.
- Kept `MaxShotCrossZMeasurementCompletionZIndex`'s owned-shot/FAI/measurement traversal shape intentionally parallel to the pre-existing `ShotHasCrossZMeasurementCompletingAt` rather than factoring both into one shared generic aggregator — the two methods answer different questions (max value vs. exact-match existence) over the same loop shape, and the actual reused computation (`GetMeasurementCompletionZIndex`) is already single-sourced; only the enclosing loop repeats, which is not the kind of duplication the LOCKED guideline's "동일 로직 2회 반복 → 즉시 함수 추출" rule targets (동일 *로직*, not 동일 *순회 형태*).

## Deviations from Plan

None - plan executed exactly as written. The plan's action prose sketched `MaxCrossZCompletionZIndex` as a single sub-function iterating "owned Shot 순회하며 각 Shot 의 ... 완성 index ... + DatumConfigs 순회하며 ..."; this was implemented as `MaxCrossZCompletionZIndex` plus one additional nested sub-helper (`MaxShotCrossZMeasurementCompletionZIndex`) to keep every function under the LOCKED guideline's ~30-line ceiling — the same "split further than the plan's literal code sketch to satisfy D-09" pattern 68-07 documented as a Rule 2 (guideline-compliance) refinement, not a behavioral deviation.

## Issues Encountered

None - single-task plan, build passed on first attempt with 0 errors. `git status` before commit confirmed only `InspectionSequence.cs` was modified (all other files shown as modified in the session's initial git snapshot had already been committed by the preceding 68-07/68-08 plans in this same execution session).

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- CROSS-2 is closed: `ComputeLastZIndex` now reflects the true last index for any recipe where a cross-Z measurement's or cross-Z Datum's completion index exceeds the sequence's max owned `shot.ZIndex` — the two-P/F-per-cycle risk (T-68-10) this plan's threat model registered as `mitigate` is resolved by construction (the same `m_nLastZIndex`/`bIsLastIndex` comparison at the call sites now consumes the widened value; no call-site changes were needed).
- `GetDatumCompletionZIndex` is in place and ready for `68-10` (GAP-3) to reuse directly for its Datum "immediate F" completion-index re-evaluation, per the gap analysis's explicit coordination note ("GAP-3(68-10)가 재사용하는 단일 소스").
- Not yet closed by this plan (tracked for `68-10`/`68-11` per the gap analysis's recommended order): GAP-3 itself (Datum "immediate F" not yet applied to cross-Z completion indices; still gated behind control-team protocol sign-off per the gap analysis's explicit warning that the "2-position Datum" layout is undocumented in `Vision-Protocol-v1.0.md`).
- Not verified by this plan (code-level only, per its own `<verify>` blocks): an actual runtime SIMUL_MODE cycle confirming that a recipe with a cross-Z measurement whose completion index exceeds the max owned `shot.ZIndex` (e.g. an owning Shot with own `ZIndex=0` and a cross-Z measurement `ZIndexA=2,ZIndexB=3`) produces exactly one P/F emission at `z=3`, not two. Deferred to the eventual 68-05 UAT re-run once the full FIX-0/GAP-1/2/3/CROSS-1/2 closure set (68-06 through 68-11) is complete.

## Self-Check: PASSED

- FOUND: WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs
- FOUND commit 1c2d8c6 (Task 1)
- Content verified: `grep` confirms `GetDatumCompletionZIndex`, `MaxCrossZCompletionZIndex`, `MaxShotCrossZMeasurementCompletionZIndex` are all defined and wired (`ComputeLastZIndex` calls `MaxCrossZCompletionZIndex`, which calls both `MaxShotCrossZMeasurementCompletionZIndex` and `GetDatumCompletionZIndex`); no "completion index vs max shot.ZIndex ⇒ misconfigured" comparison exists anywhere in the new code; msbuild Debug/x64 build PASS with 0 errors, `DatumMeasurement.exe` produced.

---
*Phase: 68-z-cross-z-dual-image-3-2-vision-protocol-v1-0-md-z1-z2-z-ind*
*Completed: 2026-07-22*
