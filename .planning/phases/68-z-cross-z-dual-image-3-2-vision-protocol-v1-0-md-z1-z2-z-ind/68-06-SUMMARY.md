---
phase: 68-z-cross-z-dual-image-3-2-vision-protocol-v1-0-md-z1-z2-z-ind
plan: 06
subsystem: control-sequence
tags: [tcp-protocol, sequence-engine, cross-z, dual-image, cycle-reset, bugfix]

# Dependency graph
requires:
  - phase: 68-02
    provides: "InspectionSequence.FindActionIndicesByZIndex, Custom/SystemHandler.StartV1Scoped z=0/z>=1 branching"
  - phase: 68-03
    provides: "InspectionSequence cross-Z image store (m_dicCrossZImages) + ResetCycleState wiring (D-02/D-02a/D-03)"
provides:
  - "InspectionSequence.BeginCrossZImageCycle() — sole cross-Z store clear entry point, called at z=0 $TEST receive time (before Action execution)"
  - "ResetCycleState() no longer clears the cross-Z store — accumulator resets (m_bCycleHasNG/m_bCycleDatumFailed/m_nCurrentZIndex/m_nLastZIndex) stay at z=0 response-generation time, unchanged"
  - "Custom/SystemHandler.StartV1Scoped z=0 branch calls BeginCrossZImageCycle() immediately before StartAll(packet)"
affects: [68-07, 68-08, 68-09, 68-10, 68-11]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Cycle-state reset split by write-timing: accumulators written only at response-generation time stay reset there; state written during mid-cycle Action execution (cross-Z image store) must be reset at receive-time instead, before that execution begins"

key-files:
  created: []
  modified:
    - WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs
    - WPF_Example/Custom/SystemHandler.cs

key-decisions:
  - "Kept ClearCrossZImages() private and unchanged (still Dispose-all + Clear); only moved its call site — added a new public BeginCrossZImageCycle() wrapper for the receive-time call, rather than making ClearCrossZImages itself public, so the private Dispose-lifecycle helper stays internal and the public surface only exposes the intent (begin a new cross-Z cycle)"
  - "Did not touch m_bCycleHasNG/m_bCycleDatumFailed/m_nCurrentZIndex/m_nLastZIndex resets in ResetCycleState() — plan's interface notes confirmed these are only written at response-generation time (ClassifyMeasurement/DetectDatumFailure), so leaving them at the existing response-time reset point is provably safe and minimizes the diff"
  - "z=0 branch cast-failure in StartV1Scoped falls back to StartAll without BeginCrossZImageCycle (defensive, not reachable in practice since IsDynamicFAIMode always yields InspectionSequence) — mirrors the existing defensive pattern already used in the z>=1 branch"

requirements-completed: [PROTO-Z-CROSS]

# Metrics
duration: ~20min
completed: 2026-07-22
---

# Phase 68 Plan 06: Cross-Z Cycle Reset Timing Fix (FIX-0) Summary

**Moved the cross-Z image store's Dispose+Clear from z=0 response-generation time (HandleDatumIndexResponse -> ResetCycleState) to z=0 $TEST receive time (StartV1Scoped -> new BeginCrossZImageCycle), fixing the structural bug where role A images captured during z=0's own Action execution were destroyed before z=1 ever arrived.**

## Performance

- **Duration:** ~20 min
- **Tasks:** 2/2 completed
- **Files modified:** 2

## Accomplishments
- `InspectionSequence.ResetCycleState()` no longer calls `ClearCrossZImages()`. It still resets `m_bCycleHasNG`/`m_bCycleDatumFailed`/`m_nCurrentZIndex`/`m_nLastZIndex` exactly as before (these are only ever written at response-generation time, so leaving their reset at `HandleDatumIndexResponse` is safe).
- New public `InspectionSequence.BeginCrossZImageCycle()` is now the sole entry point that clears the cross-Z image store (it delegates to the existing private `ClearCrossZImages()`, whose Dispose-all-then-Clear body is unchanged — no leak-prevention logic was touched, only the call site moved).
- `Custom/SystemHandler.StartV1Scoped`'s z=0 branch now casts `seq` to `InspectionSequence` and calls `BeginCrossZImageCycle()` immediately before `seq.StartAll(packet)` — this is exactly "z=0 `$TEST` receive, before this tick's Action execution begins," which is the timing FIX-0 requires. Cast-failure (defensive, unreachable in practice) skips the reset and calls `StartAll` only, matching the identical defensive pattern already present in the z>=1 branch.
- The z>=1 branch and the v2.6/legacy path (`Sequences.Start(packet)`) are untouched (verified via `git diff`) — only the z=0 branch gained the new call.
- `DebugManualZTrigger` (manual Z bridge used in SIMUL UAT) reuses `ProcessTest` -> `StartV1Scoped`, so it automatically gets the same corrected reset timing with no separate wiring needed.

## Task Commits

Each task was committed atomically:

1. **Task 1: InspectionSequence — move cross-Z store clear to receive-time entry point** - `2477ede` (fix)
2. **Task 2: SystemHandler — StartV1Scoped z=0 branch calls BeginCrossZImageCycle before StartAll** - `7ace745` (feat)

**Plan metadata:** (this commit, see below)

## Files Created/Modified
- `WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs` - Removed `ClearCrossZImages()` call from `ResetCycleState()` (with an updated comment explaining the FIX-0 rationale); added new public `BeginCrossZImageCycle()` next to `ClearCrossZImages()` as the sole clear entry point.
- `WPF_Example/Custom/SystemHandler.cs` - `StartV1Scoped`'s z=0 (`bIsDatumZIndex`) branch now casts `seq` to `InspectionSequence` and calls `BeginCrossZImageCycle()` before `seq.StartAll(packet)`, with a defensive cast-failure fallback.

## Decisions Made
- `ClearCrossZImages()` stays `private` — only a new public `BeginCrossZImageCycle()` wrapper was added, keeping the Dispose-lifecycle helper internal while exposing a clear-intent public entry point for the cross-class caller (`Custom/SystemHandler`), matching the plan's `<action>` spec exactly.
- No changes to the other four `ResetCycleState()` fields — confirmed via the plan's interface notes and by re-reading `ClassifyMeasurement`/`DetectDatumFailure` call sites that they are write-only at response-generation time, making it provably safe to leave their reset where it is.

## Deviations from Plan

None - plan executed exactly as written. Both tasks matched their `<action>` specs precisely (comment content, method placement, cast-and-guard style, defensive fallback).

## Issues Encountered

None. `msbuild` located at `C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe`, invoked via Git Bash (consistent with Plans 02-04's documented workaround). Both task commits build with 0 errors and only the same pre-existing CS0618 obsolete-member warnings already present before this plan (unrelated to these changes).

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- FIX-0 is the "precondition for everything else" fix flagged in 68-GAP-ANALYSIS.md: role A images stored during z=0's DatumPhase now survive past z=0's own response generation and remain available when z=1 (role B) arrives. This unblocks the later gap-closure plans (GAP-1/2/3, CROSS-1/2, tracked as 68-07 through 68-11) — without this fix those plans would have no observable effect (dead code, since role A would already be gone by the time role B arrived).
- D-07 regression check: for any recipe where `ZIndexA`/`ZIndexB` are unset (-1/-1, the existing convention for Top/Bottom and non-cross-Z Side recipes), `m_dicCrossZImages` is never populated by `StoreCrossZImage`/`CaptureAndStoreCrossZDatumImage` (those code paths are gated by `ZIndexA != -1 && ZIndexB != -1` per Plan 03/04), so `BeginCrossZImageCycle()`'s `ClearCrossZImages()` call is a no-op Dispose-nothing/Clear-nothing operation on every z=0 receive — behavior for TOP/BOTTOM sequences and any recipe without cross-Z fields configured is unaffected. This was verified by code-path reasoning (not a runtime SIMUL_MODE test) since Plan 06 is a two-line-call-site-move fix with no new state or branching beyond what Plans 02-04 already introduced.
- Not yet verified by this plan (code-level only, per its own `<verify>` blocks): an actual runtime SIMUL_MODE run confirming z=1's role B triggers a successful merge with the surviving role A from z=0 — this is exactly the scenario 68-07/68-08 (and the eventual 68-05 UAT re-run) are expected to exercise now that this precondition is fixed.

## Self-Check: PASSED

- FOUND: WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs
- FOUND: WPF_Example/Custom/SystemHandler.cs
- FOUND commit 2477ede (Task 1)
- FOUND commit 7ace745 (Task 2)
- Content verified: `ResetCycleState()` body contains no `ClearCrossZImages()` call (grep confirms exactly one call site, inside `BeginCrossZImageCycle()`); `BeginCrossZImageCycle` present (public) in InspectionSequence.cs; `StartV1Scoped` z=0 branch in Custom/SystemHandler.cs calls `inspDatumSeq.BeginCrossZImageCycle()` immediately before `seq.StartAll(packet)`; msbuild Debug/x64 build PASS after each task with no new warnings (only pre-existing CS0618 obsolete-member warnings unrelated to this plan); `git diff` confirms z>=1/v2.6/legacy paths in Custom/SystemHandler.cs are byte-identical to before this plan.

---
*Phase: 68-z-cross-z-dual-image-3-2-vision-protocol-v1-0-md-z1-z2-z-ind*
*Completed: 2026-07-22*
