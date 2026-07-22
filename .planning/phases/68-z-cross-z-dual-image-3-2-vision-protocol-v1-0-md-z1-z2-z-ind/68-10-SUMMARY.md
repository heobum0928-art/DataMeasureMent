---
phase: 68-z-cross-z-dual-image-3-2-vision-protocol-v1-0-md-z1-z2-z-ind
plan: 10
subsystem: inspection-sequence
tags: [vision-protocol-v1, cross-z-datum, immediate-fail, plc-integration, checkpoint-decision]

# Dependency graph
requires:
  - phase: 68-09
    provides: GetDatumCompletionZIndex (last-index truth source, reused for completion-index re-evaluation)
provides:
  - EnableCrossZDatumImmediateFail gating flag on SystemSetting (default true — enabled after checkpoint decision)
  - m_bImmediateFailSent latch preventing duplicate-F across the z=0 branch and completion-index re-evaluation
  - TryApplyCrossZDatumImmediateFail helper wired into BuildScopedResponse for completion-index cross-Z Datum immediate-F
affects: [68-11, 68-05-uat-resume]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Protocol-ambiguity gate: new behavior ships behind a SystemSetting bool default-OFF + checkpoint:decision, flipped ON only after explicit protocol-table re-reading and justification (no separate PLC round-trip required when reasoning is airtight)"

key-files:
  created: []
  modified:
    - WPF_Example/Setting/SystemSetting.cs
    - WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs

key-decisions:
  - "Checkpoint decision: enable-after-agreement (turn ON now) instead of keep-off. Vision-Protocol-v1.0.md's P/F/B table's F-row 'PLC 동작' column reads 'NG 처리' unconditionally — not scoped to index 0. PLC control logic only branches on B (call next index) vs P/F (this part done, NG/OK handling, move to next part); it never branches on the index number itself. The document's 'Datum(Index 0) 실패 시 즉시' phrase describes the historical fact that Datum has always completed at index 0 until now, not a hard requirement that F can only be sent/interpreted at index 0. Since PLC handling of F is index-agnostic, an F arriving at a non-zero completion index (e.g. z=1 for Side's 2-position Datum) is handled identically to any other F — this resolves the sign-off concern without a separate formal control-team round-trip."

patterns-established:
  - "Vision-Protocol-v1.0.md ambiguity resolution: when a document's illustrative wording (e.g. 'Datum(Index 0)') conflicts with its own generalized rule table (P/F/B PLC action column), the generalized rule table is authoritative — illustrative examples describe observed cases, not constraints."

requirements-completed: [PROTO-Z-CROSS]

# Metrics
duration: ~15min (this continuation session; Task 1 completed in prior session)
completed: 2026-07-22
---

# Phase 68 Plan 10: Cross-Z Datum Immediate-F Gating + Checkpoint Decision Summary

**Cross-Z Datum "immediate F" fail-fast now enabled by default (EnableCrossZDatumImmediateFail=true) after re-reading Vision-Protocol-v1.0.md's P/F/B table confirmed PLC handling of F is index-agnostic.**

## Performance

- **Duration:** ~15 min (this continuation session — resuming after checkpoint:decision resolution; Task 1's gating code was built in a prior session, commit `2a751e5`)
- **Completed:** 2026-07-22T04:53:34Z
- **Tasks:** 2 (Task 1: gating code — prior session; Task 2: checkpoint decision — this session)
- **Files modified:** 2 (`SystemSetting.cs`, `InspectionSequence.cs`)

## Accomplishments

- Verified Task 1's prior work (`EnableCrossZDatumImmediateFail` flag, `m_bImmediateFailSent` latch, `TryApplyCrossZDatumImmediateFail` helper) is present and wired exactly as the plan's `must_haves` describe — confirmed by grep, not assumed.
- Resolved the Task 2 `checkpoint:decision` (gate=blocking): user chose **enable-after-agreement**, flipping `EnableCrossZDatumImmediateFail`'s default from `false` to `true`.
- Rewrote the flag's doc-comment (and the `TryApplyCrossZDatumImmediateFail` helper's header comment) to record the sign-off reasoning in place of the stale "기본 false 유지, 제어팀 합의 전까지 OFF" text, so the code no longer reads as contradicting its own default value.
- Re-verified the reasoning directly against `Vision-Protocol-v1.0.md`'s 판정(P/F/B) table before committing: the F-row's "PLC 동작" column is unconditioned on index ("NG 처리" only); PLC branches only on B vs P/F, never on the index number.
- Build verified (Debug/x64, MSBuild) — 0 errors, only pre-existing `CS0618` obsolete-API warnings unrelated to this change.

## Task Commits

Each task was committed atomically:

1. **Task 1: 게이팅 플래그 + immediate-F latch + 완성 index 재평가(게이팅, 기본 OFF)** - `2a751e5` (feat) — completed in a prior session, verified present in this session via grep before proceeding.
2. **Task 2: 크로스-Z Datum 즉시-F 프로토콜 활성화 결정 (checkpoint:decision → enable-after-agreement)** - `a110ef4` (feat) — flag default flipped to `true`, doc-comments rewritten to reflect the sign-off reasoning.

**Plan metadata:** (this commit, docs: complete plan — see below)

## Files Created/Modified

- `WPF_Example/Setting/SystemSetting.cs` - `EnableCrossZDatumImmediateFail` default changed `false` → `true`; comment rewritten to cite Vision-Protocol-v1.0.md's P/F/B table (F-row "PLC 동작" = "NG 처리", index-unconditioned) as the enabling justification, replacing the old OFF-pending-agreement text.
- `WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs` - `TryApplyCrossZDatumImmediateFail`'s header comment updated to match the ON-by-default rationale (was previously describing a default-OFF, agreement-pending state). No logic changes — the gating helper itself, the `m_bImmediateFailSent` latch, and its wiring into `ResetCycleState()` / `BuildDatumShotResponse()` / `BuildScopedResponse()` were all already correct from Task 1 and required no code changes, only the flag's default value and its accompanying prose.

## Decisions Made

**Checkpoint decision: enable-after-agreement** (see `key-decisions` in frontmatter for full reasoning). Summary: the protocol document's illustrative wording ("Datum(Index 0) 실패 시 즉시") describes when this failure mode has historically occurred, not a hard constraint on which index F can be sent at. The document's own generalized P/F/B rule table shows PLC action for F is uniform ("NG 처리") regardless of index — PLC only distinguishes B (call next index) from P/F (part done, move to next part). This makes F at a non-zero completion index (Side's z=1 for its 2-position Datum) semantically identical, from the PLC's perspective, to F at z=0. The user confirmed this reading is sufficient sign-off in lieu of a separate formal control-team (김민우 선임) round-trip.

## Deviations from Plan

None - plan executed exactly as written. Task 2 was itself the checkpoint; its resolution (enable-after-agreement) is the expected/anticipated non-default branch the plan explicitly provisioned for (`<option id="enable-after-agreement">`), not a deviation from plan structure.

## Issues Encountered

None. Task 1's code was found on disk exactly as the resume-instructions described (confirmed via `git log --oneline --grep=68-10` and targeted `grep` for `EnableCrossZDatumImmediateFail` / `m_bImmediateFailSent` / `TryApplyCrossZDatumImmediateFail` before making any changes).

## Regression Status (D-07 / TOP / BOTTOM)

- **D-07 (per-FAI cycle judgement gate):** Unaffected. `ApplyCycleJudgement`'s `bCycleFail = m_bCycleHasNG || m_bCycleDatumFailed || bEmptyLastScope` logic is untouched — `TryApplyCrossZDatumImmediateFail` runs strictly after `ApplyCycleJudgement` and only *overwrites* `packet.Result`/`packet.IsBuffer` to NG when a cross-Z Datum fails exactly at its completion index; it never suppresses or alters the final-index aggregate judgement path when the flag is OFF or when no cross-Z Datum failure completes at the current index.
- **TOP/BOTTOM:** Unaffected — both cameras have no cross-Z Datum configuration (`GetDatumCompletionZIndex` returns `CROSS_Z_UNSET` for any non-cross-Z Datum, so `TryApplyCrossZDatumImmediateFail`'s `bIsCrossZDatum` check is always false and the loop body never fires for TOP/BOTTOM cycles), regardless of the flag's new default value.
- **Side (2-position Datum, the case this plan targets):** With the flag now ON by default, a cross-Z Datum failure detected at its completion index (z=1) now produces an immediate F at that index, rather than waiting for the final-index aggregate judgement. This is the intended fail-fast behavior per the checkpoint decision.
- **Duplicate-F safety (T-68-11):** `m_bImmediateFailSent` latch (set in both the z=0 immediate-F branch and the completion-index re-evaluation branch, reset in `ResetCycleState()`) still guarantees at most one immediate-F per cycle — this mechanism was unchanged by Task 2; only the flag default was flipped.

## User Setup Required

None - no external service configuration required. This is a pure code + INI-default behavior change; no PLC-side configuration is required for this flag (Vision-Protocol-v1.0.md's F semantics are unchanged from the PLC's perspective per the reasoning above).

## Next Phase Readiness

- Phase 68 gap-closure sequence continues to plan 68-11 (final gap-closure plan before 68-05 UAT resumes).
- Cross-Z Datum immediate-F is now live by default for any recipe with a 2-position (cross-Z) Datum — Side cameras with `ZIndexA`/`ZIndexB` Datum configurations will exhibit fail-fast behavior starting from the next cycle after this build is deployed.
- No blockers for 68-11.

---
*Phase: 68-z-cross-z-dual-image-3-2-vision-protocol-v1-0-md-z1-z2-z-ind*
*Completed: 2026-07-22*
