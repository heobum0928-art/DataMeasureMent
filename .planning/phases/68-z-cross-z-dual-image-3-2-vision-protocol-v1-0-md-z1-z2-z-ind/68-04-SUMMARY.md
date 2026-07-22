---
phase: 68-z-cross-z-dual-image-3-2-vision-protocol-v1-0-md-z1-z2-z-ind
plan: 04
subsystem: control-sequence
tags: [halcon, tcp-protocol, sequence-engine, cross-z, dual-image, datum, vision-measurement]

# Dependency graph
requires:
  - phase: 68-01
    provides: "DatumConfig.ZIndexA/ZIndexB int fields (-1 sentinel), SkipReason.ZINDEX_MISCONFIGURED"
  - phase: 68-03
    provides: "InspectionSequence cross-Z HImage storage (StoreCrossZImage/TakeCrossZImageCopy/HasCrossZImage/ClearCrossZImages), GetExecutionZIndex()/DoesZIndexExistInRecipe() public wrappers"
provides:
  - "Action_FAIMeasurement.TryGrabOrLoadDualDatumImages cross-Z branch (D-06) — Datum(VerticalTwoHorizontalDualImage) DualImage now captures its two ROI images from two live z_index grabs instead of static TeachingImagePath/_Vertical files, when DatumConfig.ZIndexA/ZIndexB are both set"
  - "TryGrabOrLoadCrossZDatumImages/CaptureAndStoreCrossZDatumImage/TryTakeCompletedCrossZDatumImages/BuildCrossZDatumKey helpers reusing the Plan 03 cross-Z image store under a 'DATUM|'-prefixed key namespace"
  - "IsDatumZIndexMisconfigured gate (D-05) — explicit NG (ZINDEX_MISCONFIGURED log + MarkDatumFailed + RuntimeDetectFailed) for single-set/same-value/non-existent ZIndexA/B references, called unconditionally for every VerticalTwoHorizontalDualImage datum"
  - "Code comment documenting the WARNING 2 correctness dependency (DatumPhase full re-detection + Plan 02 empty-match->StartAll fallback) for future maintainers"
affects: [68-05]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Datum-level mirror of the Plan 03 measurement-level cross-Z capture/injection pattern (ProcessCrossZCaptureTick/TryExecuteCrossZMeasurement), reusing the same InspectionSequence image store with a namespaced key prefix ('DATUM|' vs 'ShotName|MeasName') to avoid collisions"
    - "bPending out-parameter convention to distinguish 'not yet complete, not a failure' from 'actual failure' at a bool-returning method boundary — lets the caller skip MarkDatumFailed on the non-completion tick"
    - "Unconditional-caller misconfiguration gate variant (IsDatumZIndexMisconfigured) that must special-case the 'both unset' case explicitly, unlike the measurement-level IsZIndexMisconfigured which assumes the caller pre-filters via bHasAnyZIndex before calling"

key-files:
  created: []
  modified:
    - WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs

key-decisions:
  - "Live capture for cross-Z Datum images reuses the existing GrabOrLoadDatumImage helper (the same grab path Side/Bottom Datum already uses for its 1-image variant) rather than reusing ShotParam.GetImage() the way measurement-level cross-Z does — DatumPhase runs BEFORE EStep.Grab in the per-tick state machine, so no Shot-owned live image exists yet at Datum-capture time; a fresh grab (or SIMUL/offline load) is required"
  - "Cross-Z Datum store keys use a 'DATUM|' + DatumName prefix (distinct from the measurement-level 'ShotName|MeasName' prefix) to guarantee no key collision between the two cross-Z namespaces sharing the same InspectionSequence dictionary"
  - "Split the plan's two tasks into two separate git commits (Task 1: capture/injection mechanics; Task 2: misconfiguration gate + WARNING 2 comment) by temporarily removing Task 2's code, committing Task 1, then re-adding and committing Task 2 — both intermediate and final states build clean, preserving per-task atomic commit traceability"
  - "IsDatumZIndexMisconfigured explicitly short-circuits the both-unset (-1/-1) case to false before the same-value check, because unlike the measurement-level gate (which the caller only invokes after confirming 'at least one index set'), this gate is called unconditionally for every VerticalTwoHorizontalDualImage datum — without this early return, -1==-1 would be misclassified as 'same value' misconfiguration and break D-07 regression-zero for existing unconfigured Side/Bottom Datums"

requirements-completed: [PROTO-Z-CROSS]

# Metrics
duration: ~30min
completed: 2026-07-22
---

# Phase 68 Plan 04: Datum Cross-Z DualImage Capture + Misconfiguration Gate Summary

**Datum(VerticalTwoHorizontalDualImage) now supports the same cross-Z live-capture pattern as measurement-level DualImage (Plan 03) — two ROI images sourced from two different z_index live grabs via a shared InspectionSequence image store, with explicit NG on misconfigured ZIndexA/B and zero regression for existing static-teaching-image Side/Bottom Datums.**

## Performance

- **Duration:** ~30 min
- **Tasks:** 2/2 completed
- **Files modified:** 1

## Accomplishments
- `TryGrabOrLoadDualDatumImages` now branches on `datum.ZIndexA != -1 && datum.ZIndexB != -1`: when both are set, it delegates to a new `TryGrabOrLoadCrossZDatumImages` cross-Z path; when unset (-1/-1, the existing Side/Bottom Datum convention), it delegates to `TryLoadStaticDualDatumImages` — an exact copy of the original static-file-load body, preserving D-07 byte-identical behavior.
- New cross-Z capture pipeline (`TryGrabOrLoadCrossZDatumImages` → `CaptureAndStoreCrossZDatumImage` → `TryTakeCompletedCrossZDatumImages`): on each `EStep.DatumPhase` tick, if the current `$TEST` z_index (`parentSeq.GetExecutionZIndex()`) matches this datum's `ZIndexA` or `ZIndexB`, it live-grabs a fresh image via the existing `GrabOrLoadDatumImage` precedent (no new grab mechanism, still inside the pre-existing `GrabSyncLock` scope) and stores it in the Plan 03 shared cross-Z `InspectionSequence` store under a `"DATUM|" + DatumName + "_ZA"/"_ZB"` key. Once both roles are present (completion tick, functionally `max(ZIndexA, ZIndexB)` since z_index execution proceeds in increasing order), it takes owned clones of both and returns them for the unchanged `TryRunSingleDatum`/`TryComposeAlign` call; on the non-completion tick it returns `bPending=true` (not a failure) so the caller skips silently without `MarkDatumFailed`.
- `IsDatumZIndexMisconfigured` (D-05) gate added right after entering the `VerticalTwoHorizontalDualImage` branch, before `TryGrabOrLoadDualDatumImages` is called: flags single-set (-1/non‑1 mixed), same-value, or a reference to a non-existent recipe z_index as `SkipReason.ZINDEX_MISCONFIGURED` with an explicit `MarkDatumFailed` + `RuntimeDetectFailed=true` (no silent static fallback). Unlike the measurement-level `IsZIndexMisconfigured` (Plan 03), this gate is invoked unconditionally for every such datum, so it explicitly returns `false` for the both-unset (-1/-1) case before the same-value check — otherwise `-1 == -1` would be misclassified as misconfigured and break existing unconfigured Side/Bottom Datums.
- A code comment documents the WARNING 2 correctness dependency: Datum cross-Z relies on (a) `EStep.DatumPhase` re-detecting the sequence's full `DatumConfigs` list on every Action run (not a per-z_index Datum lookup) and (b) Plan 02's `ProcessTest` empty-match→`StartAll` fallback — so both z_index ticks end up re-running this Datum's detection loop regardless of which specific Shot's Action happens to execute. This is flagged so future changes to either behavior trigger re-verification of Datum cross-Z.

## Task Commits

Each task was committed atomically:

1. **Task 1: TryGrabOrLoadDualDatumImages 크로스-Z 라이브 캡처/주입 + Z1 보류(D-06)** - `cfedd41` (feat)
2. **Task 2: Datum ZIndexA/B 오설정 게이트 + 정정성 의존 주석 + Side/Bottom Datum 회귀 빌드 검증 (D-05 for Datum + WARNING 2)** - `6de3252` (feat)

**Plan metadata:** (this commit, see below)

## Files Created/Modified
- `WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs` - `CROSS_Z_DATUM_KEY_PREFIX` const; `TryGrabOrLoadDualDatumImages` signature change (`+InspectionSequence parentSeq, +out bool bPending`) with cross-Z/static branch; new `TryLoadStaticDualDatumImages`/`TryGrabOrLoadCrossZDatumImages`/`CaptureAndStoreCrossZDatumImage`/`TryTakeCompletedCrossZDatumImages`/`BuildCrossZDatumKey`/`IsDatumZIndexMisconfigured` private helpers; `EStep.DatumPhase` `VerticalTwoHorizontalDualImage` branch updated to call the misconfiguration gate first, then handle the `bPending` vs real-failure distinction on `TryGrabOrLoadDualDatumImages` failure.

## Decisions Made
- Cross-Z Datum image capture reuses `GrabOrLoadDatumImage` (the existing 1-image Datum grab precedent: real HW `GrabHalconImage(ShotParam)`, SIMUL/offline `LoadDatumImageFromPath` fallback chain) rather than `ShotParam.GetImage()` (the measurement-level cross-Z reuse target) — because `EStep.DatumPhase` runs before `EStep.Grab` in the per-tick state machine, so no Shot-owned live image exists yet when a Datum's cross-Z tick fires.
- Cross-Z Datum store keys use a `"DATUM|"` prefix (vs. the measurement-level `"ShotName|MeasName"` format) to guarantee the two cross-Z namespaces sharing the same `InspectionSequence.m_dicCrossZImages` dictionary never collide.
- Split the plan's two tasks into two separate atomic commits by writing all the code in one pass, then temporarily removing Task 2's gate/comment/helper, building+committing Task 1 alone, then re-adding and building+committing Task 2 — both intermediate states build clean (0 errors, 0 new warnings), preserving accurate per-task commit history.
- `IsDatumZIndexMisconfigured` explicitly short-circuits the both-unset (-1/-1) case to `false` before the same-value check, because this gate (unlike the measurement-level one) is called unconditionally for every `VerticalTwoHorizontalDualImage` datum rather than only after the caller confirms "at least one index is set".

## Deviations from Plan

None - plan executed exactly as written. Both tasks' `<action>` specs were implemented as designed (cross-Z branch + pending signal in Task 1; misconfiguration gate + WARNING 2 comment in Task 2). The only structural choice beyond the literal action text was splitting the implementation into helper methods (`TryLoadStaticDualDatumImages`, `TryGrabOrLoadCrossZDatumImages`, `CaptureAndStoreCrossZDatumImage`, `TryTakeCompletedCrossZDatumImages`, `BuildCrossZDatumKey`) to keep each function under the D-09 30-line guideline — this is a direct application of the plan's own `<coding_guideline_scope>` requirement, not a deviation.

## Issues Encountered

None. `msbuild` located at `C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe`, invoked via Git Bash with single-dash `-p:`/`-t:`/`-v:` switches (per Plan 02's documented Git Bash argument-mangling workaround). Both task commits build with 0 errors and only the same pre-existing CS0618 obsolete-member warnings already present before this plan (unrelated to these changes).

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- Plan 05 (SIMUL UAT) can now exercise: (1) a `VerticalTwoHorizontalDualImage` Datum with `ZIndexA`/`ZIndexB` both set to two distinct existing recipe z_index values — expect Z1-tick capture-only (no NG, no `RuntimeDetectFailed`), Z2-tick (the larger of the two) completes detection via the unchanged `TryRunSingleDatum`; (2) `ZIndexA == ZIndexB` on a Datum — expect immediate `MarkDatumFailed` + `ZINDEX_MISCONFIGURED` log; (3) existing unconfigured (-1/-1) Side/Bottom Datum recipes (Phase 37 baseline) — expect byte-identical static-file-load behavior (D-07 regression 0).
- **Not yet verified by this plan (code-level only, per its own `<verify>` blocks):** actual runtime SIMUL_MODE execution of the three UAT scenarios above; the `shared-lighthandler-race` lock-order constraint (`GrabSyncLock` outer) was respected structurally (no new lock scope introduced, cross-Z Datum capture happens inside the pre-existing lock via the existing `GrabOrLoadDatumImage` call), consistent with the still-`awaiting_human_verify` status tracked in `.planning/debug/shared-lighthandler-race.md`.
- **Observation for Plan 05 UAT awareness (not a defect in this plan's scope, no fix applied):** unlike measurement-level cross-Z (where Plan 03 Task 4 added a completion-index-aware response gate because a cross-Z measurement's `owning Shot` could report at a z_index different from its own `ZIndex`), Datum cross-Z failures are NOT guaranteed to be visible at the z_index=0 "Datum shot" response fast-path (`HandleDatumIndexResponse` → `DetectDatumFailure()` → immediate-F), because a cross-Z Datum's detection only completes at `max(ZIndexA, ZIndexB)`, which is typically `>= 1`, i.e. *after* the z=0 response has already been sent. This plan's scope (per its own must-haves/threat-model) covers only capture mechanics (D-06), the pending/failure distinction (D-02a), and the misconfiguration gate (D-05) — it does not touch cycle-level response timing. In practice, any measurement whose `DatumRef` points at a cross-Z Datum will still correctly resolve to NG by the *last* z_index response (via the existing `IsDatumFailed`/`WasDatumSkipped` aggregation, unaffected by this plan), so the overall cycle P/F outcome is still correct — only the z=0 immediate-F fast-path is unavailable for cross-Z Datums specifically. Flagging this for Plan 05 UAT / future phase awareness since it was not called out in this plan's must-haves and would constitute a Rule 4 architectural change (cycle-state/response-gating logic) outside this plan's locked task scope.

## Self-Check: PASSED

- FOUND: WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs
- FOUND commit cfedd41 (Task 1)
- FOUND commit 6de3252 (Task 2)
- Content verified: `TryGrabOrLoadCrossZDatumImages`/`CaptureAndStoreCrossZDatumImage`/`TryTakeCompletedCrossZDatumImages`/`BuildCrossZDatumKey`/`CROSS_Z_DATUM_KEY_PREFIX`/`IsDatumZIndexMisconfigured` all present in Action_FAIMeasurement.cs; msbuild Debug/x64 build PASS after each task commit with no new warnings (only pre-existing CS0618 obsolete-member warnings unrelated to this plan).

---
*Phase: 68-z-cross-z-dual-image-3-2-vision-protocol-v1-0-md-z1-z2-z-ind*
*Completed: 2026-07-22*
