---
phase: 68-z-cross-z-dual-image-3-2-vision-protocol-v1-0-md-z1-z2-z-ind
plan: 11
subsystem: inspection-sequence
tags: [vision-protocol-v1, cross-z, gap-closure, uat, recipe-validation]

# Dependency graph
requires:
  - phase: 68-06
    provides: FIX-0 reset-timing fix
  - phase: 68-07
    provides: GAP-1 declared z_index universe
  - phase: 68-08
    provides: CROSS-1 consuming-index re-detection
  - phase: 68-09
    provides: CROSS-2 last-index truth source (ComputeLastZIndex/MaxCrossZCompletionZIndex)
  - phase: 68-10
    provides: GAP-3 immediate-fail gating (default flipped true)
provides:
  - Live SIMUL_MODE UAT of FIX-0/CROSS-1/GAP-1/GAP-2/CROSS-2/GAP-3 against a real SHOT_E5 cross-Z measurement recipe
  - GAP-2 spurious-error-log suppression extended from cross-Z Datum-only indices to cross-Z Measurement capture-only indices (WarnIfEmptyScope + BuildCrossZMeasurementIndexSet)
  - Mixed cross-Z Shot save-time guard (InspectionRecipeManager.FindMixedCrossZShots + MainWindow.SaveRecipe block)
affects: [68-05-uat-resume, side-cross-z-datum-recipe]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Recipe-integrity guard at save time: detect invalid cross-recipe-wide configurations (inconsistent (ZIndexA,ZIndexB) pairs within one Shot) and block CustomMessageBox before persisting, rather than relying on runtime warnings alone."

key-files:
  created:
    - .planning/phases/68-z-cross-z-dual-image-3-2-vision-protocol-v1-0-md-z1-z2-z-ind/68-GAP-UAT.md
  modified:
    - WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs
    - WPF_Example/Custom/Sequence/Inspection/InspectionRecipeManager.cs
    - WPF_Example/MainWindow.xaml.cs

key-decisions:
  - "GAP-3 verification deferred: EnableCrossZDatumImmediateFail is Datum-only and this environment's recipe has no Side cross-Z Datum configured — functional verification blocked until that recipe exists. Code default (true, per 68-10's checkpoint decision) confirmed by inspection."
  - "Mixed-shot contamination (지침 #9) addressed via save-time UI guard rather than runtime grab/measure logic changes — lower risk, and generalized per user direction from '크로스-Z + 일반 혼합만 금지' to '같은 Shot 안 모든 측정은 (ZIndexA,ZIndexB) 짝이 동일해야 함' (also blocks e.g. (1,2) mixed with (3,2) within one Shot)."

patterns-established:
  - "WarnIfEmptyScope-style spurious-log suppression must cover every category of legitimately-empty response (Datum-only AND Measurement-only capture ticks), not just the first one discovered — mirror BuildCrossZDatumIndexSet with BuildCrossZMeasurementIndexSet when adding a new cross-Z owner category."

requirements-completed: [PROTO-Z-CROSS]

# Metrics
duration: ~90min
completed: 2026-07-23
---

# Phase 68 Plan 11: Gap-Closure Integrated UAT + Two New Fixes Found Live

**Live SIMUL_MODE UAT of Phase 68's 6 gap-closure items against a real SHOT_E5 cross-Z measurement recipe confirmed 4 PASS, found and fixed 1 new spurious-error-log bug, found and shipped 1 new save-time guard against mixed-shot recipe misconfiguration; GAP-3 functional verification blocked pending a Side cross-Z Datum recipe.**

## Performance

- **Duration:** ~90 min (Task 1 rebuild/doc already committed in a prior session, `ba72e73`; this session covers Task 2's live human-verify checkpoint plus two in-session fixes)
- **Completed:** 2026-07-23
- **Tasks:** 2 (Task 1: prior session; Task 2: this session, checkpoint + 2 unplanned fixes)
- **Files modified:** 3 source files + 1 new UAT record

## Accomplishments

- Live-verified FIX-0/CROSS-1 (completion-index correction), GAP-1/GAP-2 (no unrelated Shot re-grab), and CROSS-2 (P/F exactly once per cycle) against a real cross-Z-configured `SHOT_E5`/`E5_P2` measurement, using `DebugManualZTrigger` + cross-referenced Trace/Error logs, result-image timestamps, and `cycle.json` snapshots.
- **Found and fixed a new spurious-error-log gap**: `WarnIfEmptyScope`'s suppression only covered cross-Z **Datum**-only indices (GAP-2(f)); cross-Z **Measurement** capture-only indices (e.g. `SHOT_E5` at z=1) were unguarded and logged `[V1Cycle] BuildScopedResponse 빈 결과: ZIndex 매칭 0건` every cycle. Fixed by adding `BuildCrossZMeasurementIndexSet`/`IsZIndexUsedByCrossZMeasurement` (mirrors `BuildCrossZDatumIndexSet`) and extending the suppression condition. Re-verified live: error log silent after fix.
- **Found and fixed mixed-shot contamination live**: reconfigured `SHOT_E5` to `E5_P2`=cross-Z(1,2) + `E5_P1`=normal(-1,-1) and confirmed `E5_P1` incorrectly reports at the z=1 capture-only tick (`cycle.json`: `LastHasResult=true` at z=1). Per user direction, added a save-time guard instead of changing grab/measure execution logic: `InspectionRecipeManager.FindMixedCrossZShots`/`ShotHasInconsistentCrossZPairs` blocks recipe save via `CustomMessageBox` whenever a Shot's measurements have more than one distinct `(ZIndexA,ZIndexB)` pair (covers both cross-Z+normal mixing and mismatched cross-Z pairs like (1,2) vs (3,2), per user's explicit generalization request). Verified live: `main.ini` mtime unchanged across a blocked save attempt.
- GAP-3 (`EnableCrossZDatumImmediateFail`) confirmed at the code level (default `true` per 68-10's checkpoint decision) but functional behavior could not be exercised — this environment's recipe has no Side cross-Z Datum configuration, and the flag only affects cross-Z Datum, not cross-Z Measurement. Also found the local `Setting.ini` (created before 68-10) still persists the stale `False` default — flagged as a deployment note, not fixed here.

## Task Commits

1. **Task 1: gap-closure 검증 항목 + 혼합 Shot 운영 규칙 (68-VALIDATION.md)** - `ba72e73` (docs, prior session)
2. **Task 2a: GAP-2 에러로그 억제 확장 (크로스-Z 측정 capture-only index)** - this commit (fix)
3. **Task 2b: 혼합 크로스-Z Shot 저장 차단 가드** - this commit (feat)
4. **Task 2c: 68-GAP-UAT.md 결과 기록 + plan summary** - this commit (docs)

## Files Created/Modified

- `WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs` - `BuildCrossZMeasurementIndexSet`/`IsZIndexUsedByCrossZMeasurement` added; `WarnIfEmptyScope` suppression condition extended.
- `WPF_Example/Custom/Sequence/Inspection/InspectionRecipeManager.cs` - `FindMixedCrossZShots`/`ShotHasInconsistentCrossZPairs` added.
- `WPF_Example/MainWindow.xaml.cs` - `SaveRecipe` calls the new guard and blocks with `CustomMessageBox` on violation.
- `.planning/phases/68-.../68-GAP-UAT.md` - full test-by-test PASS/FAIL record (new file).

## Decisions Made

See `key-decisions` in frontmatter: GAP-3 verification deferred (no Side cross-Z Datum recipe here); mixed-shot contamination fixed via save-time guard, generalized to "all measurements in one Shot must share the same (ZIndexA,ZIndexB) pair" per user direction (not just "no cross-Z + normal mixing").

## Deviations from Plan

**Two unplanned code fixes were made during Task 2**, which the plan explicitly scoped as "자동 코드 변경 없음" (no automatic code changes, human-verify checkpoint only). Both were made at explicit user request after being surfaced live during testing:

1. **GAP-2 error-log suppression gap** — user said "지금해 다 고치고 넘어가" (fix it now) after the bug was demonstrated live in the running app's error log.
2. **Mixed-shot save guard** — user said "규칙으로 메세지 박스 띄어서 못하게 막는 방법으로 하자... 아예 그렇게 저장하지 못하도록" (block it with a message box at save time) after the contamination was reproduced live, then refined the rule scope themselves ("1,2 하고 3,2가 되면 안되고... 똑같이 맞추도록").

Both fixes were built, rebuilt (Debug/x64, 0 errors), and re-verified live in the running app before being accepted.

## Issues Encountered

None beyond the two gaps documented above (both resolved).

## User Setup Required

None — no external service configuration required.

## Next Phase Readiness

- Side end-to-end cross-Z deployment is now supported by 4/6 gap-closure items with live PASS confirmation; GAP-3's functional path needs a Side cross-Z Datum recipe before it can be verified the same way.
- `main.ini` restored byte-identical to its pre-UAT state (`main.ini.bak_gapuat`, diff confirmed empty) — production recipe unaffected by test edits.
- `Setting.ini` staleness (GAP-3 default) is a carry-over deployment note, not a blocker for this phase.

---
*Phase: 68-z-cross-z-dual-image-3-2-vision-protocol-v1-0-md-z1-z2-z-ind*
*Completed: 2026-07-23*
