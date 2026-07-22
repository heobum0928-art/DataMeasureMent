---
phase: 68-z-cross-z-dual-image-3-2-vision-protocol-v1-0-md-z1-z2-z-ind
plan: 08
subsystem: control-sequence
tags: [cross-z, dual-image, datum, transform-lifetime, sequence-engine, bugfix]

# Dependency graph
requires:
  - phase: 68-07
    provides: "InspectionSequence.IsDatumOnlyExecutionIndex/FindDatumOnlyActionIndices (GAP-2) and BuildDeclaredZIndexSet/DoesZIndexExistInRecipe (GAP-1) — the datum-only execution scope and z_index universe this plan's consuming-index re-detection now actually gets to run inside of"
  - phase: 68-04
    provides: "DatumConfig.ZIndexA/ZIndexB fields, CROSS_Z_UNSET/UNSET_ZINDEX sentinel convention, TryGrabOrLoadCrossZDatumImages/CaptureAndStoreCrossZDatumImage/TryTakeCompletedCrossZDatumImages/BuildCrossZDatumKey cross-Z Datum store scaffolding"
provides:
  - "Action_FAIMeasurement.TryReDetectCrossZDatumFromStore(datum, parentSeq, out imgH, out imgV) — deterministic re-detection at consuming z_index using both stored role images"
  - "Action_FAIMeasurement.IsCrossZDatumBothStored / ResolveCrossZDatumRoleKeys / TryTakeCrossZImageClones — shared key-derivation and clone-take helpers reused by both the original completion-index path and the new consuming-index re-detect path"
affects: [68-09, 68-10, 68-11]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Consuming-index re-detection instead of transform caching: rather than persisting _datumTransforms across the per-Action ClearDatumTransforms() reset, the fix re-derives the transform deterministically from already-stored source images at every consuming tick. This avoids introducing new cross-tick mutable state while still restoring correctness."
    - "Single-source key derivation for a cache lookup used by two different call sites (completion tick vs. consuming tick): extract the two-line role-key derivation (ResolveCrossZDatumRoleKeys) and the four-line clone-take-with-rollback logic (TryTakeCrossZImageClones) into shared private helpers rather than duplicating them, per the LOCKED control-sequence guideline's 'identical logic twice → extract immediately' rule."

key-files:
  created: []
  modified:
    - WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs

key-decisions:
  - "Consuming-index re-detection is gated on 'both role images already stored' (IsCrossZDatumBothStored), not on any transform-lifetime/caching mechanism — this keeps ClearDatumTransforms()'s per-Action clear-and-rebuild-from-scratch model completely intact (no new persistent state added) while ensuring the rebuild actually succeeds at consuming indices too, closing CROSS-1 without touching the reset semantics 68-06 (FIX-0) established."
  - "When both role images are confirmed stored but TryTakeCrossZImageClones still fails to retrieve clones (race/edge case), the method returns false (not bPending=true) — treated as a genuine failure the same way the original completion-index path already treats a post-bCompleted clone failure, so the caller's existing MarkDatumFailed handling covers it without new branching."
  - "Refactored TryTakeCompletedCrossZDatumImages's inline key-derivation and clone-take logic into ResolveCrossZDatumRoleKeys/TryTakeCrossZImageClones rather than leaving the new TryReDetectCrossZDatumFromStore as a near-duplicate — required by the LOCKED coding guideline (D-09) since the same clone-take-with-rollback logic would otherwise appear twice."

requirements-completed: [PROTO-Z-CROSS]

# Metrics
duration: ~10min
completed: 2026-07-22
---

# Phase 68 Plan 08: CROSS-1 (Cross-Z Datum Transform Lifetime) Summary

**Cross-Z Datum-based measurements no longer silently fall back to an uncorrected identity transform at their consuming z_index — `TryGrabOrLoadCrossZDatumImages` now deterministically re-detects from the two already-stored role images whenever the current tick isn't the Datum's own ZIndexA/B.**

## Performance

- **Duration:** ~10 min
- **Tasks:** 1/1 completed
- **Files modified:** 1

## Accomplishments

- **CROSS-1 closed:** `TryGrabOrLoadCrossZDatumImages`'s `!bRelevant` branch (reached at every z_index that is not the Datum's own `ZIndexA`/`ZIndexB` — e.g. the z=2 measurement tick for a Side Datum with `ZIndexA=0,ZIndexB=1`) no longer unconditionally sets `bPending=true` and returns with no re-detection. It now checks `IsCrossZDatumBothStored` and, when both role images are already in the cross-Z store, calls the new `TryReDetectCrossZDatumFromStore` to return clones of both images — letting the existing `EStep.DatumPhase` call site run `TryRunSingleDatum`/`TryComposeAlign` again and repopulate `_datumTransforms`, which `ClearDatumTransforms()` (line 93) wipes on every Action's `EStep.DatumPhase` re-entry. Without this, `ResolveDatumTransform` would silently fall back to identity at the measurement tick even after a successful z=1 detection, and an uncorrected measurement would be reported as a normal pass/fail.
- **Shared helper extraction (D-09 compliance):** `ResolveCrossZDatumRoleKeys` (single-source `keyA`/`keyB` derivation from `BuildCrossZDatumKey`) and `TryTakeCrossZImageClones` (clone-take-with-rollback-on-partial-failure) are now used by both the pre-existing completion-index path (`TryTakeCompletedCrossZDatumImages`) and the new consuming-index path (`TryReDetectCrossZDatumFromStore`) — no duplicated key-building or clone-retrieval logic was introduced.
- **Regression boundary preserved:** the change is scoped entirely to `TryGrabOrLoadCrossZDatumImages`'s `!bRelevant` branch and its two new sibling helpers. `TryLoadStaticDualDatumImages` (static/D-07 path) and the 1-image `GrabOrLoadDatumImage` path are byte-identical to before this plan — confirmed via `git diff` showing no changes outside the cross-Z DualImage block.
- Build verified via `msbuild` Debug/x64: 0 errors, `DatumMeasurement.exe` produced.

## Task Commits

Each task was committed atomically:

1. **Task 1: TryGrabOrLoadCrossZDatumImages — consuming-index deterministic re-detection from store** - `62cd735` (fix)

**Plan metadata:** (this commit, see below)

## Files Created/Modified

- `WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs` — `TryGrabOrLoadCrossZDatumImages`'s `!bRelevant` branch now calls `IsCrossZDatumBothStored`/`TryReDetectCrossZDatumFromStore` before falling back to `bPending=true`; added `ResolveCrossZDatumRoleKeys`, `IsCrossZDatumBothStored`, `TryReDetectCrossZDatumFromStore`, `TryTakeCrossZImageClones`; refactored `TryTakeCompletedCrossZDatumImages` to reuse the two new shared helpers instead of inlining key derivation and clone retrieval.

## Decisions Made

- Did not introduce any transform-caching or cross-tick persistence mechanism to keep `_datumTransforms` alive past `ClearDatumTransforms()` — chose deterministic re-detection from already-stored source images instead, since this requires zero changes to the existing per-Action clear-and-rebuild lifecycle that 68-06/68-07 depend on, and keeps the "recompute, don't cache" invariant the rest of the Datum pipeline already follows.
- Treated a clone-retrieval failure at the consuming-index re-detect path (after `IsCrossZDatumBothStored` already confirmed both keys present) as a genuine failure (`bPending` stays `false`, method returns `false`) rather than `bPending=true` — mirrors the existing behavior in `TryTakeCompletedCrossZDatumImages` where a post-`bCompleted` clone failure is likewise treated as a real failure, not a "wait for next tick" signal.
- Extracted `ResolveCrossZDatumRoleKeys`/`TryTakeCrossZImageClones` as shared helpers rather than writing `TryReDetectCrossZDatumFromStore` as a self-contained near-duplicate of `TryTakeCompletedCrossZDatumImages`'s tail — required by the LOCKED control-sequence coding guideline's explicit "identical logic appearing twice → extract immediately" rule (D-09), which this plan's `<coding_guideline_scope>` calls out.

## Deviations from Plan

None - plan executed exactly as written. The plan's action prose named `TryReDetectCrossZDatumFromStore` as the required new helper and allowed splitting the "re-detection acquisition logic" into it to keep functions under 30 lines; the additional `ResolveCrossZDatumRoleKeys`/`IsCrossZDatumBothStored`/`TryTakeCrossZImageClones` split is a direct, in-scope consequence of the plan's own instruction to "재검출 취득 로직은 TryReDetectCrossZDatumFromStore 로 분리" and "BuildCrossZDatumKey 재사용해 키 도출 단일화(중복 순회 금지)" — not a deviation, an implementation detail of satisfying that instruction without duplicating the existing clone-take logic in `TryTakeCompletedCrossZDatumImages`.

## Issues Encountered

None - single-task plan, build passed on first attempt with 0 errors.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- CROSS-1 is closed for the cross-Z Datum DualImage path: a Datum with `ZIndexA`/`ZIndexB` both set now has its transform restored (via deterministic re-detection, not a stale cache) at every subsequent consuming z_index in the same cycle, not just at the completion index where it was first detected.
- Non-cross-Z Datum paths (`ZIndexA`/`ZIndexB` both `UNSET_ZINDEX`, i.e. `TryLoadStaticDualDatumImages` and the 1-image static path) are unmodified — D-07 regression-0 preserved, confirmed via `git diff` scope check.
- Not yet closed by this plan (tracked for 68-09 through 68-11 per the gap analysis's recommended order): CROSS-2 (`ComputeLastZIndex` vs. measurement/Datum completion-index independence — risk of two P/F emissions per cycle when a cross-Z completion index exceeds the sequence's max `shot.ZIndex`), and GAP-3 (Datum "immediate F" not yet applied to cross-Z completion indices, gated behind control-team protocol sign-off per the gap analysis).
- Not verified by this plan (code-level only, per its own `<verify>` blocks): an actual runtime SIMUL_MODE cycle confirming that a Side Datum with `ZIndexA=0,ZIndexB=1` produces a non-identity, correctly-corrected measurement result at z=2 — this is deferred to the eventual 68-05 UAT re-run once the full FIX-0/GAP-1/2/3/CROSS-1/2 closure set (68-06 through 68-11) is complete.

## Self-Check: PASSED

- FOUND: WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs
- FOUND commit 62cd735 (Task 1)
- Content verified: `grep` confirms `TryReDetectCrossZDatumFromStore` is defined and called from `TryGrabOrLoadCrossZDatumImages`'s `!bRelevant` branch; `TryLoadStaticDualDatumImages` is present and unchanged (only referenced, not modified, per `git diff` scope); `git diff` confirms all changes are confined to the cross-Z DualImage block (lines ~538-635), with the 1-image static datum path and `TryLoadStaticDualDatumImages` showing zero diff; msbuild Debug/x64 build PASS with 0 errors, `DatumMeasurement.exe` produced.

---
*Phase: 68-z-cross-z-dual-image-3-2-vision-protocol-v1-0-md-z1-z2-z-ind*
*Completed: 2026-07-22*
