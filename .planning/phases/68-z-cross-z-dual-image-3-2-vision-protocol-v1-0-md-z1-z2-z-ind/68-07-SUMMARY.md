---
phase: 68-z-cross-z-dual-image-3-2-vision-protocol-v1-0-md-z1-z2-z-ind
plan: 07
subsystem: control-sequence
tags: [tcp-protocol, sequence-engine, cross-z, dual-image, z-index-universe, execution-scope, bugfix]

# Dependency graph
requires:
  - phase: 68-06
    provides: "BeginCrossZImageCycle() receive-time cross-Z store reset (FIX-0) — precondition for GAP-1/GAP-2 to have any observable effect"
  - phase: 68-02
    provides: "InspectionSequence.FindActionIndicesByZIndex, Custom/SystemHandler.StartV1Scoped z=0/z>=1 branching"
  - phase: 68-04
    provides: "DatumConfig.ZIndexA/ZIndexB fields, CROSS_Z_UNSET sentinel convention"
provides:
  - "InspectionSequence.BuildCrossZDatumIndexSet()/IsZIndexUsedByCrossZDatum(int) — single-source predicate for 'does this z_index feed a cross-Z Datum', shared by GAP-1 universe and GAP-2 execution scope"
  - "InspectionSequence.BuildDeclaredZIndexSet() — declared z_index universe (owned Shot ZIndex + owned measurement ZIndexA/B + cross-Z Datum ZIndexA/B)"
  - "InspectionSequence.DoesZIndexExistInRecipe(int) rewritten to membership on BuildDeclaredZIndexSet (no longer delegates to FindShotByZIndex alone)"
  - "InspectionSequence.IsDatumOnlyExecutionIndex(int) — z=0-guarded datum-only execution-scope predicate"
  - "InspectionSequence.FindDatumOnlyActionIndices(int) — resolves DatumPhase-trigger Action indices for a datum-only z_index"
  - "InspectionSequence.WarnIfEmptyScope suppressed for datum-only indices"
  - "Custom/SystemHandler.StartV1Scoped empty-match branch routes to datum-only StartSubset before the StartAll fallback"
  - "Action_FAIMeasurement EStep.DatumPhase termination branches to EStep.End (skip Grab/Measure) on datum-only index"
affects: [68-08, 68-09, 68-10, 68-11]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Single-source cross-cutting predicate: when two gap-closure concerns (GAP-1 universe membership, GAP-2 execution scope) both need to know 'is this z_index used by a cross-Z Datum', extract one shared helper (BuildCrossZDatumIndexSet/IsZIndexUsedByCrossZDatum) rather than duplicating the DatumConfigs traversal"
    - "Explicit z=0 guard as the first statement in any new z_index-scope predicate that could be reached from the z=0 path — prevents silently breaking the D-01a StartAll-for-Datum-shot invariant when new scope-narrowing logic is introduced"
    - "Fallback-with-log instead of silent Shots[0]-style fallback: when a datum's SourceShotName cannot be resolved to an owned Action, log the fallback explicitly (D-05 anti-pattern avoidance) rather than choosing a Shot invisibly"

key-files:
  created: []
  modified:
    - WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs
    - WPF_Example/Custom/SystemHandler.cs
    - WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs

key-decisions:
  - "Kept the dangerous 'completion index (max(ZIndexA,ZIndexB)) > max shot.ZIndex = misconfigured' rule OUT of DoesZIndexExistInRecipe entirely — the gap analysis's Critique review found this would re-break the Plan 03 BLOCKER fix (completion index is intentionally independent of shot.ZIndex). BuildDeclaredZIndexSet does pure set membership only; same-value/single-set/undeclared misconfiguration detection stays owned by IsZIndexMisconfigured/IsDatumZIndexMisconfigured exactly as before."
  - "IsDatumOnlyExecutionIndex's z=0 guard is the literal first statement in the method body (before any other computation) — per the gap analysis's explicit blocking-severity note that omitting this guard would let z=0 be misjudged as datum-only and silently break the D-01a StartAll-for-all-Actions guarantee."
  - "FindDatumOnlyActionIndices resolves via SourceShotName first, falling back to the first owned Action only when SourceShotName cannot be matched — and that fallback always emits a PrintLog Error naming the datum, distinguishing it from the D-05 silent Shots[0] anti-pattern (this selects a *trigger Action* for DatumPhase re-entry, not a *data* attribution, so the distinction matters for future readers)."
  - "Refactored the initial single AddDatumTriggerActionIndex draft into three smaller private helpers (AddDatumTriggerActionIndex / FindOwnedActionIndexForSourceShot / AddFallbackTriggerWithLog) to stay under the LOCKED control-sequence guideline's ~30-line-per-function ceiling, rather than accept one longer method."
  - "WarnIfEmptyScope's datum-only suppression check (IsDatumOnlyExecutionIndex) was added as the very first statement so it short-circuits before either FAIResults.Count or nMatchedShots is even inspected for the datum-only case — keeps the existing non-datum-only warning logic completely untouched below it."

requirements-completed: [PROTO-Z-CROSS]

# Metrics
duration: ~35min
completed: 2026-07-22
---

# Phase 68 Plan 07: GAP-1 (Declared z_index Universe) + GAP-2 (Datum-Only Execution Scope) Summary

**Closed two structural gaps blocking Side (z=0,1=Datum dual-position capture, measurement Shots z=2+) deployment: `DoesZIndexExistInRecipe` now checks a declared z_index universe (Shot own ZIndex + measurement ZIndexA/B + Datum ZIndexA/B) instead of only `FindShotByZIndex`, and `StartV1Scoped`'s empty-match branch now runs a minimal DatumPhase-only Action instead of falling back to `StartAll` + per-cycle Error log — both routed through one shared "is this z_index used by a cross-Z Datum" predicate.**

## Performance

- **Duration:** ~35 min
- **Tasks:** 3/3 completed
- **Files modified:** 3

## Accomplishments

- **GAP-1 (declared z_index universe):** `InspectionSequence.BuildDeclaredZIndexSet()` now unions three sources — this sequence's owned Shot own-ZIndex values, owned measurements' `DualImageEdgeDistanceMeasurement.ZIndexA/B`, and `DatumConfigs[*].ZIndexA/B` (via the shared `BuildCrossZDatumIndexSet()`). `DoesZIndexExistInRecipe(int)` was rewritten from a single `FindShotByZIndex(nZIndex) != null` delegation to `BuildDeclaredZIndexSet().Contains(nZIndex)`. This closes the false-hard-failure where Side's z=1 (owned by no Shot, only by the Datum's `ZIndexB=1`) and SHOT_E5-style measurements (own `ZIndex=0`, `ZIndexA=1`) triggered `ZINDEX_MISCONFIGURED` every cycle via `IsDatumZIndexMisconfigured`.
- **Danger-rule exclusion (explicit, per gap analysis Critique):** the originally-considered rule "completion index `max(ZIndexA,ZIndexB)` exceeding the sequence's max shot.ZIndex = misconfigured" was **not** implemented anywhere in `BuildDeclaredZIndexSet`/`DoesZIndexExistInRecipe`. A `grep` check post-implementation confirms no such comparison exists — same-value/single-set/undeclared detection remains exclusively `IsZIndexMisconfigured`/`IsDatumZIndexMisconfigured`'s responsibility (Plan 03/04), preserving the Plan 03 BLOCKER fix (completion index is independent of shot.ZIndex).
- **Single-source cross-Z Datum predicate (shared by GAP-1 and GAP-2):** `BuildCrossZDatumIndexSet()` (private `HashSet<int>`, DatumConfigs traversal gated by `!= CROSS_Z_UNSET`) and its membership wrapper `IsZIndexUsedByCrossZDatum(int)` are the only place in the class that decides "does this z_index feed a cross-Z Datum." `BuildDeclaredZIndexSet` (`UnionWith`) and `IsDatumOnlyExecutionIndex` (direct call) both consume this one helper — no duplicate DatumConfigs-traversal helper was introduced.
- **GAP-2 (datum-only execution scope):** `IsDatumOnlyExecutionIndex(int nZIndex)` returns `false` immediately if `nZIndex == DATUM_Z_INDEX` (the mandatory z=0 guard, first statement in the method), then returns `true` only when the index is used by a cross-Z Datum **and** `FindActionIndicesByZIndex(nZIndex)` finds no regular execution match. `FindDatumOnlyActionIndices(int)` resolves, for each Datum whose `ZIndexA`/`ZIndexB` matches the requested index, the owned Action whose `ShotParam.ShotName == datum.SourceShotName`; if unresolved, it falls back to the first owned Action **with an explicit `PrintLog Error`** naming the Datum (deliberately distinguished from the D-05 silent-`Shots[0]` anti-pattern in code comments, since this selects a DatumPhase trigger Action, not a data attribution).
- **`Custom/SystemHandler.StartV1Scoped` routing:** the z>=1 empty-match branch now checks `inspSeq.IsDatumOnlyExecutionIndex(_lastPrepZIndex)` before the existing `StartAll` + Error-log fallback. When true and at least one trigger Action resolves, it calls `seq.StartSubset(datumIndices.ToArray(), packet)` with **no** Error log (this is the designed normal path for Side z=1, not an operator misconfiguration). Genuine empty matches (datum-only false, or zero resolved triggers) fall through to the pre-existing `StartAll` fallback + Error log unchanged (T-68-01 DoS mitigation preserved).
- **`Action_FAIMeasurement` `AdvanceAfterDatumPhase`:** `EStep.DatumPhase`'s termination now computes `nCurZ = parentSeq.GetExecutionZIndex()` and `bDatumOnly = parentSeq.IsDatumOnlyExecutionIndex(nCurZ)`, advancing to `EStep.End` (skipping `EStep.Grab`/`EStep.Measure`) when `bDatumOnly` is true, and to `EStep.Grab` otherwise (unchanged for z=0 and all normal z>=1 indices). This prevents the Shot's regular measurement from re-executing at the wrong physical Z during a datum-only tick, which would otherwise pollute cycle.json, saved images, and screen display (the GAP-2 residual risk called out in the gap analysis).
- **`WarnIfEmptyScope` suppression:** added `bDatumOnlyIndex = IsDatumOnlyExecutionIndex(nZIndex); if (bDatumOnlyIndex) return;` as the first statement, before either `FAIResults.Count` or `nMatchedShots` is inspected — datum-only indices are legitimately empty-scope (0 measurement items, not a completion index), so the pre-existing per-cycle spurious `PrintErrLog` is now suppressed for them without touching the rest of the existing empty-scope warning logic.

## Task Commits

Each task was committed atomically:

1. **Task 1: InspectionSequence — declared universe (GAP-1) + cross-Z Datum single-source + datum-only execution predicate (GAP-2)** - `5d53e54` (feat)
2. **Task 2: SystemHandler — StartV1Scoped empty-match datum-only routing (GAP-2)** - `86de1ad` (feat)
3. **Task 3: Action_FAIMeasurement — skip Grab/Measure on datum-only index (AdvanceAfterDatumPhase)** - `ecaba52` (feat)

**Plan metadata:** (this commit, see below)

## Files Created/Modified

- `WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs` — Added `BuildCrossZDatumIndexSet`/`IsZIndexUsedByCrossZDatum` (single source), `AddFaiDeclaredZIndices`/`AddShotDeclaredZIndices`/`BuildDeclaredZIndexSet` (GAP-1 universe), rewrote `DoesZIndexExistInRecipe`, added `IsDatumOnlyExecutionIndex`/`FindDatumOnlyActionIndices`/`FindOwnedActionIndexForSourceShot`/`AddDatumTriggerActionIndex`/`AddFallbackTriggerWithLog` (GAP-2 scope + trigger resolution), and added the datum-only suppression guard to `WarnIfEmptyScope`.
- `WPF_Example/Custom/SystemHandler.cs` — `StartV1Scoped`'s empty-match branch now checks `IsDatumOnlyExecutionIndex` → `FindDatumOnlyActionIndices` → `StartSubset` before falling back to the pre-existing `StartAll` + Error-log path.
- `WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs` — `EStep.DatumPhase`'s termination branches to `EStep.End` vs `EStep.Grab` based on `parentSeq.IsDatumOnlyExecutionIndex(parentSeq.GetExecutionZIndex())`.

## Decisions Made

- Did not implement the "completion index vs max shot.ZIndex" misconfiguration rule anywhere — explicitly excluded per the gap analysis's Critique-confirmed blocking regression against the Plan 03 BLOCKER fix. Verified via `grep` post-implementation that no such comparison exists in the new code.
- Split the datum-trigger-resolution logic into three private helpers (`AddDatumTriggerActionIndex`, `FindOwnedActionIndexForSourceShot`, `AddFallbackTriggerWithLog`) instead of one longer method, to keep every function under the LOCKED control-sequence coding guideline's function-length ceiling (structure section: "함수 30줄 초과 → 분리 검토").
- Left `FinishAction`'s resulting Pass/Fail value on the datum-only Action's own context as whatever `Context.Clear()` (invoked for every Action at sequence `StartCore`) sets it to, rather than forcing it to `Pass` — traced through `ExecuteAction`/`ClassifyMeasurement`/`m_bCycleHasNG` and confirmed this per-Action pass/fail value is never read by the TCP response builder (`BuildScopedResponse`/`AggregateIndexFais`/`ApplyCycleJudgement` all key off measurement-level classification, not the Action's own `AllPass`), so it has no protocol-correctness impact either way. The plan's action spec did not ask for an explicit override, so none was added.

## Deviations from Plan

None - plan executed exactly as written for all three tasks' `<action>` specs. One implementation-level addition beyond the plan's literal code sketch: `AddDatumTriggerActionIndex` (Task 1e) was split into three sub-30-line helper functions instead of the one slightly-longer method the plan's action prose implied, to satisfy the LOCKED coding guideline's function-length rule explicitly called out in `<coding_guideline_scope>`. This is a Rule 2 (auto-add missing critical functionality — guideline compliance) refinement, not a behavioral change.

## Issues Encountered

- One ternary-operator slip was caught and fixed during self-review before committing: an initial draft of `FindOwnedActionIndexForSourceShot` used `ShotConfig shot = faiAct == null ? null : faiAct.ShotParam;`, which violates the LOCKED control-sequence guideline's "삼항 연산자 `? :` 금지" rule. Replaced with an explicit `if (faiAct != null) { shot = faiAct.ShotParam; }` block before the first build attempt — no wasted build cycle.
- `msbuild` invoked via Git Bash at `C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe`, each build completed well within the 300s timeout passed to Bash. All three builds passed with 0 errors and only the same pre-existing CS0618 obsolete-member warnings already present before this plan (Sequence_Top/Sequence_Bottom/SequenceHandler legacy references, unrelated to these changes).

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- GAP-1 and GAP-2 are closed for the code paths exercised by Side's z=0,1=Datum / z=2+=measurement layout: `DoesZIndexExistInRecipe(0)`/`DoesZIndexExistInRecipe(1)` now return `true` for a Datum with `ZIndexA=0,ZIndexB=1`, so `IsDatumZIndexMisconfigured` no longer hard-fails Side's Datum every cycle; `StartV1Scoped` at z=1 now runs a single resolved trigger Action via `StartSubset` instead of `StartAll` + Error log.
- This plan builds directly on 68-06 (FIX-0): the cross-Z image store must already survive from z=0 receive-time through to z=1 arrival for GAP-2's datum-only tick to have anything to detect — that precondition is now in place, and 68-07 supplies the routing so the z=1 tick actually happens.
- Not yet closed by this plan (tracked for 68-08 through 68-11 per the gap analysis's recommended order): CROSS-1 (Datum transform lifetime — the detected cross-Z transform is still cleared every `EStep.DatumPhase` entry and not restored at the measurement z_index, so the eventual measurement at z=2 would still resolve to an identity-transform silent fallback even with GAP-1/GAP-2 closed), CROSS-2 (`ComputeLastZIndex` vs measurement/Datum completion-index independence), and GAP-3 (Datum "immediate F" not yet applied to cross-Z completion indices, and gated behind control-team protocol sign-off per the gap analysis).
- Not verified by this plan (code-level only, per its own `<verify>` blocks): an actual runtime SIMUL_MODE run confirming (a) Side's z=1 tick reaches `EStep.DatumPhase` via the new `StartSubset` routing with zero Error logs, and (b) the resulting response is buffered (`B`) rather than triggering a false empty-scope warning. This is deferred to the eventual 68-05 UAT re-run once CROSS-1 (68-08, expected same wave per the gap analysis) restores the measurement-side transform consumption.

## Self-Check: PASSED

- FOUND: WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs
- FOUND: WPF_Example/Custom/SystemHandler.cs
- FOUND: WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs
- FOUND commit 5d53e54 (Task 1)
- FOUND commit 86de1ad (Task 2)
- FOUND commit ecaba52 (Task 3)
- Content verified: `grep` confirms `IsDatumOnlyExecutionIndex` is present and referenced in all three modified files; `IsDatumOnlyExecutionIndex`'s first statement is the `nZIndex == DATUM_Z_INDEX` guard returning `false`; no "completion index vs max shot.ZIndex" comparison exists anywhere in `InspectionSequence.cs`; msbuild Debug/x64 build PASS after each of the three task commits with 0 errors and only the same pre-existing CS0618 warnings present before this plan.

---
*Phase: 68-z-cross-z-dual-image-3-2-vision-protocol-v1-0-md-z1-z2-z-ind*
*Completed: 2026-07-22*
