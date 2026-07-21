---
phase: 68-z-cross-z-dual-image-3-2-vision-protocol-v1-0-md-z1-z2-z-ind
plan: 02
subsystem: control-sequence
tags: [tcp-protocol, sequence-engine, cross-z, dual-image, execution-scope]

# Dependency graph
requires:
  - phase: 68-01
    provides: "ZIndexA/ZIndexB int fields on DualImageEdgeDistanceMeasurement/DatumConfig (measurement-level, -1 sentinel)"
provides:
  - "RebuildInspectionActions ZIndex-stable-sorted Actions[] (same-ZIndex Shots always contiguous, D-01b)"
  - "InspectionSequence.FindActionIndicesByZIndex(nZIndex) public multi-match helper (own-ZIndex OR owned DualImage ZIndexA/ZIndexB)"
  - "ProcessTest z_index==0 -> StartAll / z_index>=1 -> StartSubset(+empty-match StartAll fallback) execution-scope wiring"
affects: [68-03, 68-04, 68-05]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "LINQ OrderBy (stable sort) for filter-then-sort Shot ordering — List<T>.Sort/Array.Sort rejected as unstable"
    - "Cross-class public exposure for InspectionSequence helpers consumed by Custom/SystemHandler.cs (matches ApplyShotLights/TurnOffShotLights precedent)"

key-files:
  created: []
  modified:
    - WPF_Example/Custom/Sequence/SequenceHandler.cs
    - WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs
    - WPF_Example/Custom/SystemHandler.cs
    - WPF_Example/UI/ControlItem/InspectionListView.xaml.cs

key-decisions:
  - "RebuildInspectionActions filters owned Shots first, then LINQ OrderBy(ZIndex) (stable) — never List<T>.Sort/Array.Sort (unstable, would not guarantee same-ZIndex contiguity)"
  - "FindActionIndicesByZIndex made public (not private as literally stated in plan's Task 2 acceptance criteria) because Task 3's ProcessTest lives on a different class (SystemHandler) and cannot call a private member of InspectionSequence — matches existing ApplyShotLights/TurnOffShotLights public-for-cross-class-call convention in the same file"
  - "z_index==0 (Datum) kept on StartAll unconditionally, never routed through FindActionIndicesByZIndex — Datum detection is embedded in every Action's DatumPhase, not an independent Shot, so filtering would zero out the match set and stop Datum detection (D-01a)"
  - "Empty FindActionIndicesByZIndex match on z>=1 falls back to StartAll with an error log, never a silent no-op (T-68-01 DoS mitigation, matches WarnIfEmptyScope precedent from Phase 49)"

patterns-established:
  - "Filter-owned-then-stable-sort is now the required shape for any future Shots-to-Actions[] rebuild — do not reintroduce List<T>.Sort"

requirements-completed: [PROTO-Z-CROSS]

# Metrics
duration: 20min
completed: 2026-07-22
---

# Phase 68 Plan 02: v1.0 z_index Execution-Scope Filtering Summary

**ProcessTest now routes `$TEST(z=N)` through `InspectionSequence.FindActionIndicesByZIndex` + `SequenceBase.StartSubset` for N>=1 (own-ZIndex Shots plus cross-Z owning Shots matched via ZIndexA/ZIndexB), while z=0 keeps the existing StartAll path — closing the Phase 49 D-01 gap where only response aggregation, not grab execution, was scoped by z_index.**

## Performance

- **Duration:** ~20 min
- **Tasks:** 3/3 completed
- **Files modified:** 4 (3 planned + 1 Rule-1 fix)

## Accomplishments
- `SequenceHandler.RebuildInspectionActions` now filters a sequence's owned Shots first, then applies a LINQ `OrderBy(ZIndex)` **stable** sort (ties keep original append order) before assigning `EAction.FAI_Base + N` ids — same-ZIndex Shots are now always contiguous in `Actions[]`, which `SequenceBase.StartSubset`'s min-max-range execution requires (D-01b).
- `InspectionSequence.FindActionIndicesByZIndex(nZIndex)` is a new multi-match helper returning every `Actions[]` index whose owning Shot matches `nZIndex` — either via `ShotConfig.ZIndex` (its own position) or via a `DualImageEdgeDistanceMeasurement` it owns with `ZIndexA`/`ZIndexB == nZIndex` (cross-Z owning Shot, so one Shot can execute at two different z_index positions per the user-confirmed "one measurement references two z_index" design, not a two-Shot split). Cross-Z matching is isolated in a `DoesShotOwnCrossZIndex` sub-helper per the D-09 coding guideline (guard clauses, Hungarian-prefixed bools, no ternary/null-coalescing).
- `Custom/SystemHandler.ProcessTest` no longer unconditionally calls `seq.StartAll(packet)` in the `IsDynamicFAIMode` branch — a new `StartV1Scoped` helper branches on `_lastPrepZIndex` (the single source of truth for this `$TEST`'s z_index, not a re-parse of `packet.TestID`): `z_index==0` (named `DATUM_TEST_Z_INDEX` const) still calls `StartAll` (D-01a, Datum-detection regression guard), `z_index>=1` calls `FindActionIndicesByZIndex` and `StartSubset` on a non-empty match, falling back to `StartAll` with an error log on an empty match set (never a silent no-op). The v2.6/legacy branch (`Sequences.Start(packet)`) and the `IsRecipeReady` guard are byte-identical.

## Task Commits

Each task was committed atomically:

1. **Task 1: RebuildInspectionActions ZIndex stable sort (D-01b)** - `ce145f3` (feat)
2. **Task 2: FindActionIndicesByZIndex multi-match helper (D-01)** - `ed898c6` (feat)
3. **Task 3: ProcessTest z=0/z>=1 StartAll/StartSubset wiring (D-01/D-01a)** - `f33bade` (feat)

**Plan metadata:** (this commit, see below)

## Files Created/Modified
- `WPF_Example/Custom/Sequence/SequenceHandler.cs` - `RebuildInspectionActions` filters owned Shots then stable-sorts by `ZIndex` (LINQ `OrderBy`) before building `Actions[]`; added `using System.Linq;`.
- `WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs` - Added `DoesShotOwnCrossZIndex(shot, nZIndex)` private sub-helper and `FindActionIndicesByZIndex(nZIndex)` public multi-match helper, placed directly below `FindShotByZIndex`.
- `WPF_Example/Custom/SystemHandler.cs` - Added `DATUM_TEST_Z_INDEX` const and `StartV1Scoped(seq, packet)`; `ProcessTest`'s `IsDynamicFAIMode` branch now calls `StartV1Scoped` instead of unconditional `StartAll`.
- `WPF_Example/UI/ControlItem/InspectionListView.xaml.cs` - `ComputeLocalShotIndex` updated to filter-then-`OrderBy(ZIndex)` the same way `RebuildInspectionActions` does, so `seq[shotIdx]` still resolves to the correct Shot's Action after the reorder (Rule 1 fix, see Deviations).

## Decisions Made
- LINQ `OrderBy` chosen over `List<T>.Sort`/`Array.Sort` specifically because those are unstable sorts in .NET — same-ZIndex Shots would not reliably keep their original append order, undermining the "contiguous block" guarantee D-01b requires for `StartSubset`.
- `FindActionIndicesByZIndex` made `public` rather than the `private` literally specified in the plan's Task 2 acceptance criteria — see Deviations, Rule 3.
- z=0 branch never calls `FindActionIndicesByZIndex` at all (not "call it and it happens to return everything") — kept as a hard `StartAll` short-circuit per D-01a's explicit regression-avoidance rationale (Datum detection is not represented as a z=0 Shot in most recipes).
- Empty-match fallback logs via `Logging.PrintErrLog` with the same "빈 결과 경고, 조용한 무시 금지" shape as Phase 49's `WarnIfEmptyScope`, rather than returning `false` (which would silently drop the `$TEST` with no response).

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Fixed ComputeLocalShotIndex order-mismatch caused by Task 1's reorder**
- **Found during:** Task 1 (RebuildInspectionActions stable sort)
- **Issue:** `InspectionListView.xaml.cs`'s `ComputeLocalShotIndex` (used by the Run button and batch-run features) computed a Shot's local index by filtering `RecipeManager.Shots` in plain append order and returning that position — a comment on the call site explicitly documents this must correspond 1:1 with `RebuildInspectionActions`'s action-index assignment. Once `RebuildInspectionActions` started sorting owned Shots by `ZIndex` (Task 1), any recipe where a sequence's Shots are added in an order that doesn't already happen to be ZIndex-ascending would cause `ComputeLocalShotIndex` to return an index that no longer matches the Shot's actual position in `Actions[]` — the Run button / batch-run (`BatchRunService.StartBatch`) would silently start the wrong Shot's Action.
- **Fix:** Updated `ComputeLocalShotIndex` to filter-then-`OrderBy(ZIndex)` the same way `RebuildInspectionActions` now does, keeping the two index sources in lockstep.
- **Files modified:** `WPF_Example/UI/ControlItem/InspectionListView.xaml.cs`
- **Verification:** msbuild Debug/x64 build PASS; logic mirrors `RebuildInspectionActions`'s new filter+sort exactly (same `OwnerSequenceName` fallback, same `OrderBy(ZIndex)`).
- **Committed in:** `ce145f3` (Task 1 commit)

**2. [Rule 3 - Blocking] Changed FindActionIndicesByZIndex from private to public**
- **Found during:** Task 3 (ProcessTest wiring)
- **Issue:** The plan's Task 2 acceptance criteria literally specifies `private List<int> FindActionIndicesByZIndex(int nZIndex)`, but Task 3's action text requires `Custom/SystemHandler.ProcessTest` — a method on a different class (`SystemHandler`, not `InspectionSequence`) — to call it directly. A `private` member cannot be called from a different class; this would not compile. The plan's own two tasks were mutually inconsistent on this point.
- **Fix:** Changed the access modifier to `public`, matching the established convention already in this exact code path — `InspectionSequence.ApplyShotLights`/`TurnOffShotLights` are `public` for the identical reason (called from `Custom/SystemHandler.ApplyPrepToSequences`/`TurnOffPrepLights`, a different class in the same assembly).
- **Files modified:** `WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs`
- **Verification:** msbuild Debug/x64 build PASS after the change; without it, Task 3's `inspSeq.FindActionIndicesByZIndex(...)` call fails to compile (CS0122 inaccessible member).
- **Committed in:** `f33bade` (Task 3 commit, since the fix was only discovered while implementing Task 3's consumer)

---

**Total deviations:** 2 auto-fixed (1 bug fix caused by this plan's own reorder, 1 blocking cross-task inconsistency in the plan itself)
**Impact on plan:** Both fixes were required for correctness/compilation, not scope creep — the ComputeLocalShotIndex fix prevents a real regression to the existing Run-button/batch-run feature, and the access-modifier fix was the only way Task 3 could compile as specified.

## Issues Encountered
- `msbuild` was not on `PATH` in this environment; located at `C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe`. Also, Git Bash strips a leading `/` from `/p:...`-style MSBuild switches (interpreting them as Unix paths), causing `MSB1008: only one project can be specified` — resolved by using single-dash `-p:`/`-t:`/`-v:` switch syntax instead, which MSBuild accepts identically and Git Bash does not mangle.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness
- Plan 03/04 (cross-Z live-capture wiring in `Action_FAIMeasurement.cs`, D-02/D-02a/D-08 fix) can now rely on: (a) `Actions[]` being ZIndex-contiguous per owning sequence, (b) `FindActionIndicesByZIndex` as the canonical z_index-to-Actions[]-index resolver (already cross-Z aware), and (c) `ProcessTest` actually gating grabs by z_index at runtime, so a cross-Z owning Shot genuinely gets invoked twice (once per its two z_index positions) rather than once as before this plan.
- Plan 05 UAT must specifically verify the scenario flagged in this plan's `<verification>`/`<success_criteria>`: when a cross-Z owning Shot's `ZIndexA`/`ZIndexB` falls outside the contiguous own-ZIndex block, `StartSubset`'s min-max range expansion could re-grab unrelated Shots in between — this is called out as an explicit PASS/FAIL gate (not a soft observation) for Plan 05 Scenario 1, not verified here (no SIMUL_MODE run was performed in this plan; only `msbuild` compiled).
- No backward-compat regression testing against a real old recipe (D-07, z_index=0 exception, SHOT_E5) was performed in this plan either — still deferred to Plan 05 SIMUL UAT per the phase's overall verification plan.

## Self-Check: PASSED

- FOUND: WPF_Example/Custom/Sequence/SequenceHandler.cs
- FOUND: WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs
- FOUND: WPF_Example/Custom/SystemHandler.cs
- FOUND: WPF_Example/UI/ControlItem/InspectionListView.xaml.cs
- FOUND commit ce145f3 (Task 1)
- FOUND commit ed898c6 (Task 2)
- FOUND commit f33bade (Task 3)
- Content verified: `OrderBy(shot => shot.ZIndex)` present in SequenceHandler.cs; `FindActionIndicesByZIndex` present (public) in InspectionSequence.cs; `StartSubset` present in Custom/SystemHandler.cs (StartV1Scoped); msbuild Debug/x64 build PASS after each task with no new warnings (only pre-existing CS0618 obsolete-member warnings unrelated to this plan).

---
*Phase: 68-z-cross-z-dual-image-3-2-vision-protocol-v1-0-md-z1-z2-z-ind*
*Completed: 2026-07-22*
