---
phase: 68-z-cross-z-dual-image-3-2-vision-protocol-v1-0-md-z1-z2-z-ind
plan: 12
subsystem: control-sequence
tags: [tcp-protocol, sequence-engine, cross-z, dual-image, z-index-universe, execution-scope, waste-elimination]

# Dependency graph
requires:
  - phase: 68-07
    provides: "IsDatumOnlyExecutionIndex/FindDatumOnlyActionIndices/AddDatumTriggerActionIndex/FindOwnedActionIndexForSourceShot/AddFallbackTriggerWithLog — z>=1 datum-only execution scope mechanism reused (not modified) by this plan's z=0 path"
  - phase: 68-11
    provides: "code state (Task 1) this plan builds on — Task 2's open human-verify checkpoint is unrelated to this plan's dependency"
provides:
  - "InspectionSequence.FindZeroIndexDatumTriggerActionIndices() — z=0-only representative DatumPhase trigger Action resolution across ALL DatumConfigs (cross-Z or single-position, no ZIndexA/B filter), reusing AddDatumTriggerActionIndex"
  - "InspectionSequence.ShouldSkipMeasurementAfterDatumPhase(int) — single entry point OR-combining the unmodified z>=1 IsDatumOnlyExecutionIndex path with the new z=0 representative-trigger path"
  - "Custom/SystemHandler.StartV1Scoped z=0 branch routes to StartSubset(representative trigger indices) instead of unconditional StartAll; falls back to StartAll (Trace log) only when DatumConfigs empty or no trigger resolved"
  - "Action_FAIMeasurement EStep.DatumPhase termination now calls ShouldSkipMeasurementAfterDatumPhase instead of IsDatumOnlyExecutionIndex directly — z=0 representative trigger Action(s) also skip Grab/Measure"
  - "68-VALIDATION.md Plan 12 verification entries — explicit PASS/FAIL contract delegated to 68-GAP-UAT.md / follow-up UAT"
affects: [68-GAP-UAT, 68-05-UAT-resume]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Parallel-path OR-combination instead of widening an existing predicate's contract: ShouldSkipMeasurementAfterDatumPhase wraps IsDatumOnlyExecutionIndex (unmodified) rather than removing its z=0 guard, because that guard's contract is shared by IsZIndexUsedByCrossZDatum consumers (WarnIfEmptyScope, BuildDeclaredZIndexSet) that must not have their semantics widened to include non-cross-Z Datums"
    - "Trace-level (not Error-level) log for a legitimate empty-scope fallback: the z=0 StartAll fallback (DatumConfigs empty / no trigger resolved) is a normal, valid recipe configuration, not an operator misconfiguration — distinguished in log severity from the z>=1 branch's genuine-error 'ZIndex 매칭 0건' Error log"

key-files:
  created: []
  modified:
    - WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs
    - WPF_Example/Custom/SystemHandler.cs
    - WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs
    - .planning/phases/68-z-cross-z-dual-image-3-2-vision-protocol-v1-0-md-z1-z2-z-ind/68-VALIDATION.md

key-decisions:
  - "Did not touch IsDatumOnlyExecutionIndex's body (its z=0 guard 'if (nZIndex == DATUM_Z_INDEX) return false' stays literally unchanged) — verified via git diff that the only hunk in InspectionSequence.cs is a pure insertion after AddFallbackTriggerWithLog's closing brace, before ParseCurrentZIndex. Widening that predicate would also change IsZIndexUsedByCrossZDatum's downstream consumers (WarnIfEmptyScope suppression, GAP-1 BuildDeclaredZIndexSet), which was explicitly flagged as a contamination risk in the plan's investigation_findings."
  - "FindZeroIndexDatumTriggerActionIndices reuses AddDatumTriggerActionIndex (SourceShotName resolution + fallback-with-log) unchanged rather than duplicating the resolution logic — avoids a 4th near-identical DatumConfigs-traversal helper (per the control-sequence guideline's anti-duplication rule already established in 68-07)."
  - "StartV1Scoped's z=0 empty-fallback log uses ELogType.Trace, not ELogType.Error — this fallback path is a normal, valid recipe shape (no DatumConfigs, or a DatumConfig whose SourceShotName cannot be resolved), unlike the z>=1 branch's genuine-misconfiguration Error log. Distinguishing severity avoids polluting operator-facing error logs for a non-error condition."
  - "Did not modify DetectDatumFailure/ComputeLastZIndex/AggregateIndexFais/BuildDatumShotResponse/WarnIfEmptyScope — confirmed via git diff across all three task commits that none of these functions appear in any changed hunk (only one incidental comment-line reference to WarnIfEmptyScope/BuildDeclaredZIndexSet, not a code edit). Datum immediate-F contract (byte-identical) and P/F/B judgement/completion-index computation are fully preserved."

requirements-completed: [PROTO-Z-CROSS]

# Metrics
duration: ~15min
completed: 2026-07-22
---

# Phase 68 Plan 12: z=0 Datum-Only Representative-Trigger Execution Summary

**Removed wasted full-StartAll execution at z=0 for TOP/BOTTOM/SIDE inspection sequences — z=0 `$TEST` now runs only the representative Datum trigger Action(s) via StartSubset (falling back to StartAll only when DatumConfigs is empty), and those trigger Actions skip Grab/Measure after DatumPhase, exactly like the existing z>=1 cross-Z datum-only mechanism.**

## Performance

- **Duration:** ~15 min
- **Tasks:** 3/3 completed
- **Files modified:** 4

## Accomplishments

- **z=0 representative trigger resolution (Task 1):** `InspectionSequence.FindZeroIndexDatumTriggerActionIndices()` resolves, for every `DatumConfig` in this sequence (cross-Z or single-position — no `ZIndexA`/`ZIndexB` filter, unlike `FindDatumOnlyActionIndices`), the owned Action whose `ShotParam.ShotName` matches `datum.SourceShotName`, reusing the existing `AddDatumTriggerActionIndex` (SourceShotName resolution + explicit fallback-with-log). This closes the gap that ordinary (non-cross-Z) Datums have no `ZIndexA/B` concept and were therefore invisible to the z>=1 mechanism's cross-Z-only filter.
- **Unified skip predicate (Task 1):** `InspectionSequence.ShouldSkipMeasurementAfterDatumPhase(int nZIndex)` OR-combines the unmodified z>=1 `IsDatumOnlyExecutionIndex(nZIndex)` path with a new `nZIndex == DATUM_Z_INDEX` path that returns true only if at least one representative trigger resolves. `IsDatumOnlyExecutionIndex`'s body (including its mandatory z=0 guard) was not touched — verified the sole diff hunk in `InspectionSequence.cs` is a pure insertion between `AddFallbackTriggerWithLog` and `ParseCurrentZIndex`.
- **StartV1Scoped z=0 routing (Task 2):** the z=0 branch in `Custom/SystemHandler.StartV1Scoped` no longer unconditionally calls `seq.StartAll(packet)`. After `BeginCrossZImageCycle()` (FIX-0, unchanged), it now calls `FindZeroIndexDatumTriggerActionIndices()` and routes to `seq.StartSubset(datumZeroIndices.ToArray(), packet)` when at least one trigger resolves; otherwise it logs at `ELogType.Trace` ("DatumConfigs 비어있음(또는 트리거 미해결) — StartAll 폴백") and falls back to `StartAll` — distinguishing this legitimate empty-scope case from the z>=1 branch's genuine-misconfiguration `Error` log. The stale `DATUM_TEST_Z_INDEX` comment ("z=0은 기존처럼 StartAll로 예외 처리") was rewritten to describe the new behavior (matching the 68-10 Task 2 precedent of keeping code and comments in sync). The z>=1 branch (lines below the z=0 block) was verified byte-identical via `git diff`.
- **Action_FAIMeasurement wiring (Task 3):** `EStep.DatumPhase`'s termination now calls `parentSeq.ShouldSkipMeasurementAfterDatumPhase(nCurZ)` instead of `parentSeq.IsDatumOnlyExecutionIndex(nCurZ)` directly — a single call-site swap plus updated comment explaining the z=0 extension. All other logic (`nCurZ` computation, `if/else` structure, `EStep.Grab`/`EStep.End` branching) is unchanged.
- **68-VALIDATION.md:** added a new `## Gap-Closure 검증 항목 추가(68-12: ...)` section with 4 verification rows (representative-trigger-only grab, Datum immediate-F preservation, measurement-value invariance, empty-DatumConfigs fallback) plus explicit delegation of actual PASS/FAIL recording to `68-GAP-UAT.md` / follow-up UAT rounds — this autonomous plan does not skip verification, it registers the contract.

## Task Commits

Each task was committed atomically:

1. **Task 1: InspectionSequence — z=0 representative Datum trigger resolution + unified skip predicate** - `3fe1c74` (feat)
2. **Task 2: SystemHandler — StartV1Scoped z=0 branch routes to representative StartSubset** - `8cdbd71` (feat)
3. **Task 3: Action_FAIMeasurement wiring + 68-VALIDATION.md entries** - `f9f8cdf` (feat)

**Plan metadata:** (this commit, see below)

## Files Created/Modified

- `WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs` — Added `FindZeroIndexDatumTriggerActionIndices()` and `ShouldSkipMeasurementAfterDatumPhase(int)` between `AddFallbackTriggerWithLog` and `ParseCurrentZIndex`. No existing method bodies modified.
- `WPF_Example/Custom/SystemHandler.cs` — `StartV1Scoped`'s z=0 branch now routes through `FindZeroIndexDatumTriggerActionIndices()` → `StartSubset`, falling back to `StartAll` (Trace log) only on empty/unresolved triggers. `DATUM_TEST_Z_INDEX` comment rewritten. z>=1 branch untouched.
- `WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs` — `EStep.DatumPhase` termination now calls `ShouldSkipMeasurementAfterDatumPhase` instead of `IsDatumOnlyExecutionIndex`; comment updated to describe the unified z=0+z>=1 skip decision.
- `.planning/phases/68-z-cross-z-dual-image-3-2-vision-protocol-v1-0-md-z1-z2-z-ind/68-VALIDATION.md` — Added Plan 12 verification section (4 rows + operational-rule cross-reference note + UAT delegation).

## Decisions Made

- Kept `IsDatumOnlyExecutionIndex`/`FindDatumOnlyActionIndices` completely untouched rather than widening their contracts to cover z=0 or non-cross-Z Datums — a new parallel method (`ShouldSkipMeasurementAfterDatumPhase`) OR-combines the two paths instead, per the plan's explicit rationale (avoiding contamination of `IsZIndexUsedByCrossZDatum`'s other consumers).
- Used `ELogType.Trace` (not `Error`) for the z=0 StartAll fallback log, since an empty/unresolved DatumConfigs set at z=0 is a valid recipe shape, not an operator misconfiguration — distinct in severity from the z>=1 branch's genuine "ZIndex 매칭 0건" Error log.
- No changes to `DetectDatumFailure`/`ComputeLastZIndex`/`AggregateIndexFais`/`BuildDatumShotResponse`/`WarnIfEmptyScope` — confirmed via `git diff` across all three commits (only one incidental comment-line mention, no code edits). Datum immediate-F contract and P/F/B judgement logic remain byte-identical.

## Deviations from Plan

None - plan executed exactly as written for all three tasks' `<action>` specs.

## Issues Encountered

None. `msbuild` (Debug/x64) passed after each of the three task commits with 0 errors; only pre-existing CS0618 (obsolete `TopSequence`/`BottomSequence`/`TopInspectionAction`/`BottomInspectionAction` references from the Phase 33 migration) and CS0162 (unreachable code in `VirtualCamera.cs`) warnings appeared, unchanged from before this plan. The uncommitted local `SIMUL_MODE` toggle in `WPF_Example/DatumMeasurement.csproj` was left untouched and unstaged throughout, per the executor instructions.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- z=0 waste elimination is code-complete and builds clean for TOP/BOTTOM/SIDE (the mechanism is sequence-type-agnostic — it operates purely on `InspectionSequence.DatumConfigs`/`Actions`, with no Side-specific branching).
- Not verified at runtime by this plan (code-level only, per its own `<verify>` blocks): an actual SIMUL_MODE run confirming (a) z=0 grabs only the representative trigger Shot(s) and no others, (b) Datum immediate-F still fires at the same timing, (c) measurement values/pass-fail are unchanged pre/post this plan, (d) the empty-DatumConfigs StartAll fallback still runs all Actions. These four checks are now registered in `68-VALIDATION.md`'s new Plan 12 section, delegated to `68-GAP-UAT.md` (68-11 Task 2, still an open human-verify checkpoint) or a follow-up UAT round.
- This plan's code state does not depend on 68-11 Task 2's checkpoint resolution — it builds on 68-11 Task 1's code state only, as scoped in this plan's frontmatter (`depends_on: [68-11]`).
- Ready for the eventual 68-05 UAT resume, alongside the other 68-06~68-11 gap-closure fixes, once a human runs the consolidated UAT pass.

## Self-Check: PASSED

- FOUND: WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs
- FOUND: WPF_Example/Custom/SystemHandler.cs
- FOUND: WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs
- FOUND: .planning/phases/68-z-cross-z-dual-image-3-2-vision-protocol-v1-0-md-z1-z2-z-ind/68-VALIDATION.md
- FOUND commit 3fe1c74 (Task 1)
- FOUND commit 8cdbd71 (Task 2)
- FOUND commit f9f8cdf (Task 3)
- Content verified: `grep` confirms `FindZeroIndexDatumTriggerActionIndices`/`ShouldSkipMeasurementAfterDatumPhase` are defined in `InspectionSequence.cs` and referenced in `SystemHandler.cs`/`Action_FAIMeasurement.cs` respectively; `git diff` across all three commits shows `IsDatumOnlyExecutionIndex`/`FindDatumOnlyActionIndices` bodies unchanged and the z>=1 branch of `StartV1Scoped` unchanged; `grep -c "68-12"` on `68-VALIDATION.md` returns 2 (section header + reference); msbuild Debug/x64 build PASS after each of the three task commits with 0 errors and only pre-existing CS0618/CS0162 warnings present before this plan.

---
*Phase: 68-z-cross-z-dual-image-3-2-vision-protocol-v1-0-md-z1-z2-z-ind*
*Completed: 2026-07-22*
