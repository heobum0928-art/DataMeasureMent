---
phase: quick-260818-ef5
plan: 01
subsystem: inspection-sequence
tags: [refactor, action-fai-measurement, state-machine, extract-method, csharp]

requires: []
provides:
  - "Action_FAIMeasurement.Run() reduced to a 11-line switch dispatch (was 592 lines)"
  - "11 new private methods (RunInit/RunMoveZ/RunDatumPhase/RunGrab/RunMeasure/RunEnd + ProcessOneDatum/ProcessDatumDualImage/ProcessDatumSingleImage/ProcessOneMeasurement/FinalizeFaiTick)"
  - "[FaiTiming] diagnostic instrumentation fully removed (18 timing-only local variables deleted)"
  - "All 10 code ternary operators converted to if-else with hoisted locals"
affects: [action-fai-measurement, inspection-sequence, datum-phase, measure-phase]

tech-stack:
  added: []
  patterns:
    - "Extract-method refactor via Python line-range scripting (not manual retyping) for byte-identical moves"
    - "continue -> return conversion only when extracting an ENTIRE loop body into its own method"
    - "ref-parameter accumulators instead of new tuple/class types (C# 7.2 constraint, file precedent: MarkAllMeasurementsNoImage(ref int))"

key-files:
  created: []
  modified:
    - "WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs"

key-decisions:
  - "ProcessDatumDualImage intentionally has NO ref int nDatumOk/nDatumFail parameters (D-1 structural lock) — DUAL branch never touched these counters originally, and the signature now makes it impossible to accidentally add them later"
  - "using(image)/try-finally{sharedSrc.Release()} scaffolding kept inside RunMeasure() rather than extracted, per plan's resource-lifetime contract (5-C) — avoids native HImage leak risk"
  - "Reworded 3 replacement comments (originally suggested by the plan's example text) to avoid literally containing the strings 'LogSeqStep(' / '[ALGO]' so the plan's own grep-based regression-count checks (expect exactly 11 / 1) wouldn't false-fail on comment text"

requirements-completed: [REFACTOR-EF5-01, REFACTOR-EF5-02, REFACTOR-EF5-03]

duration: ~40min
completed: 2026-08-18
---

# Quick 260818-ef5: Action_FAIMeasurement.Run() Readability Refactor Summary

**Behavior-preserving extract-method refactor of the 592-line `Run()` state machine into an 11-line switch dispatch plus 11 named private methods, with zero judgement/timing/side-effect changes verified via byte-identical normalized diffs and a 14-item manual 1:1 audit.**

## Performance

- **Duration:** ~40 min
- **Tasks:** 3/3 completed
- **Files modified:** 1 (`WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs`)

## Accomplishments

- `Run()` reduced from 592 lines (L106-L697 pre-refactor) to an 11-line pure switch dispatch
- 6 new `Run*` methods (Init/MoveZ/DatumPhase/Grab/Measure/End) extracted as byte-identical case-body moves (verified via whitespace-normalized diff against the pre-Task-2 commit — zero differences beyond the expected `break;`/case-scope-brace removal)
- 5 further helper methods extracted from the DatumPhase and Measure bodies (`ProcessOneDatum`, `ProcessDatumDualImage`, `ProcessDatumSingleImage`, `ProcessOneMeasurement`, `FinalizeFaiTick`), converting 12 `continue;` statements (6 in DatumPhase, 6 in Measure) to `return;` — valid only because each conversion extracts the loop body **in its entirety**
- All `[FaiTiming]` temporary diagnostic logging removed (4 log call sites + 18 timing-only local variables), while the 4 `[SEQ]`/`[ALGO]`-consumed `Stopwatch` instances were preserved
- All 10 code-level ternary (`?:`) operators converted to if-else with hoisted, Hungarian-prefixed local variables
- Zero behavior changes: all 7 documented "looks-like-a-bug" intentional asymmetries (D-1 through D-7) verified intact after refactor

## Task Commits

Each task was committed atomically:

1. **Task 1: [FaiTiming] removal + ternary→if-else** - `eefda4a` (refactor)
2. **Task 2: Run() switch 6-case extraction to RunXxx() methods** - `2b30ded` (refactor)
3. **Task 3: DatumPhase/Measure loop-body extraction (continue→return)** - `12fa8aa` (refactor)

_No plan-metadata commit was made — per explicit task constraints, SUMMARY.md/STATE.md/PLAN.md are not committed by the executor; the orchestrator handles the docs commit._

## Files Created/Modified

- `WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs` - Run() state machine decomposed into 11 named methods; diagnostic timing code removed; all ternaries converted to if-else

## Decisions Made

- **D-1 structural lock preserved by construction:** `ProcessDatumDualImage(DatumConfig, InspectionSequence)` has no `ref` counter parameters, matching the plan's explicit design intent to make it impossible for a future editor to "fix" the DUAL/1-IMAGE counter asymmetry by simply adding a parameter — the asymmetry is now enforced by the method signature itself, not just convention.
- **Resource-lifetime scaffolding kept in place:** `using (var image = ShotParam.GetImage())`, the `try { sharedSrc = new SharedHImage(...) } ... try { ... } finally { sharedSrc.Release(); }` block (the round-4 fix from quick-260810-egx), and the per-FAI `crossZRoleImage` finally-dispose were all deliberately left inside `RunMeasure()` / `FinalizeFaiTick()` at their original nesting depth rather than being pulled further apart, per the plan's 5-C resource-lifetime contract.
- **Comment wording adjusted from the plan's literal example text** in 3 spots (see Deviations) to keep the plan's own automated regression-count checks meaningful.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug in plan's own example text] Replacement comments literally embedding `LogSeqStep(` / `[ALGO]` would have broken the plan's own regression-count verify checks**
- **Found during:** Task 1, verify step (running the plan's own `automated` grep checks)
- **Issue:** Ground rule G-1 step 4 gives example replacement text for the 4 `//TEMP` comments, e.g. `//260818 hbk [SEQ] DatumPhase 단계 tact 측정용 — 아래 LogSeqStep("DatumPhase", "완료 …") 가 소비한다.` and `//260818 hbk [ALGO] 로그용 측정 실행시간`. Using this literal text made `grep -c 'LogSeqStep\|LogSeqAlgo'` return 14 instead of the required 11, and `grep -c '\[ALGO\]'` return 2 instead of the required 1 — both are explicit must-have truths and automated verify assertions in this same plan.
- **Fix:** Reworded the 3 affected comments to convey the identical intent without the literal substrings the plan's own grep checks are keying on: `//260818 hbk [SEQ] DatumPhase 단계 tact 측정용 — 아래 "완료 —" 단계 요약 로그가 소비한다.` (×3, for DatumPhase/Grab/Measure) and `//260818 hbk 알고리즘 로그용 측정 실행시간` (for the `swMeasureExec` inline comment). The plan itself flagged these examples as "문구(예시, 의미만 맞으면 됨)" (example wording, meaning is what matters), so this is consistent with plan intent.
- **Files modified:** `WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs` (comments only, no code change)
- **Verification:** All Task 1/2/3 automated grep checks pass at exactly the plan's required counts (see Self-Check below)
- **Commit:** `eefda4a`

**2. [Rule 3 - Blocking issue] Task 2's extraction script left a double blank line at 2 method boundaries**
- **Found during:** Task 3 preparation (visual review of Task 2's output before starting Task 3's extraction)
- **Issue:** The Python cut/paste script used to extract the 6 `Run*` methods inserted a blank-line separator after each new method, which combined with the pre-existing blank line already at the original case-boundary location, producing two consecutive blank lines before the `RunGrab` and `RunEnd` comment blocks. Purely cosmetic (would not affect compilation or behavior) but inconsistent with the file's single-blank-line convention between methods.
- **Fix:** Collapsed the 2 double-blank-line runs to single blank lines via a small regex pass (`\n{3,}` → `\n\n`), applied before running Task 2's verify checks.
- **Files modified:** `WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs` (whitespace only)
- **Verification:** Confirmed via `awk`/`sed` line inspection that exactly single blank lines separate all methods; re-ran Task 2's normalized-diff verification afterward with no change in result (still zero diff)
- **Commit:** `2b30ded`

None of the file/line ranges, control-flow, side-effect ordering, or judgement logic were altered by either deviation — both are textual/cosmetic corrections to keep the plan's own automated verification meaningful and the file internally consistent.

## Threat Flags

None. Per the plan's own threat model, this refactor introduces no new trust boundary, network endpoint, auth path, or schema — purely internal method decomposition of an existing single-file state machine. All 4 STRIDE register items (T-ef5-01 through T-ef5-04) were mitigated exactly as the plan prescribed (see §8-C audit below for T-ef5-01/T-ef5-02 evidence).

## RESEARCH §8-C: 14-Item 1:1 Audit (mandatory manual comparison, not satisfied by "build passed")

Baseline for comparison: `cb284f4` (pre-refactor, clean). All line numbers below are from the file state **after** Task 3 (commit `12fa8aa`) unless noted.

| # | Item | Result | Evidence |
|---|------|--------|----------|
| 1 | Call order: `ApplyDatumLights` → `WaitForPendingWrites` → (branch) → detect → `ApplyShotLights` → `WaitForPendingWrites` unchanged | **PASS** | `ProcessOneDatum`: L230 `parentSeq.ApplyDatumLights(datum);` → L234 `LightHandler.Handle.WaitForPendingWrites();` → L235-239 branch dispatch to `ProcessDatumDualImage`/`ProcessDatumSingleImage` (detection happens inside). After the `foreach` closes in `RunDatumPhase` (L178), L182 `parentSeq.ApplyShotLights(ShotParam.ZIndex);` → L185 `LightHandler.Handle.WaitForPendingWrites();`. Identical relative order to `cb284f4`. |
| 2 | Light-restore condition: `ApplyShotLights` still gated on `DatumConfigs.Count > 0` **AND** `ShotParam != null` (D-4) | **PASS** | `RunDatumPhase` L173 `if (parentSeq != null && parentSeq.DatumConfigs.Count > 0) {` wraps the `foreach` **and** the `if (ShotParam != null) { parentSeq.ApplyShotLights(...); ... }` block (L181-186) — same nesting as original, both conditions still required. |
| 3 | Cache-skip (`HasCachedDatumTransform`) still occurs **before** `ApplyDatumLights` | **PASS** | `ProcessOneDatum` L224 `if (!bIsCrossZDatum && parentSeq.HasCachedDatumTransform(...)) { ...; return; }` precedes L230 `parentSeq.ApplyDatumLights(datum);` — same order. |
| 4 | `Mark*` calls = 7 total: `MarkDatumFailed` ×5, `MarkAlignFailed` ×2, same branch mapping | **PASS** | `grep -c 'parentSeq\.MarkDatumFailed('` = 5 (L252 f1, L275 f3, L302 f4b, L320 g2, L352 g4b); `grep -c 'parentSeq\.MarkAlignFailed('` = 2 (L291 f4a, L336 g4a). `f4b` (DUAL non-align else-branch failure, the item the plan specifically warned is easy to miss) confirmed present at L302 inside `ProcessDatumDualImage`'s `else` branch of `if (datum.IsPatternAlignEnabled)`. Split: `ProcessDatumDualImage` = 4 calls (f1/f3/f4a/f4b), `ProcessDatumSingleImage` = 3 calls (g2/g4a/g4b) — matches plan's 4:3 mapping. |
| 5 | Counter asymmetry preserved (D-1/D-2/D-3): DUAL branch still never touches `nDatumOk`/`nDatumFail` | **PASS** | `sed -n '/private void ProcessDatumDualImage(/,/^        }$/p' $F \| grep -c 'nDatumOk\|nDatumFail'` = 0. Enforced structurally: `ProcessDatumDualImage(DatumConfig datum, InspectionSequence parentSeq)` signature has no `ref int` counter parameters at all — cannot access them even if someone tried. |
| 6 | `LogSeqAlgo` still only in the 1-IMAGE branch, called unconditionally (2 call sites) | **PASS** | `grep -n 'LogSeqAlgo'` = L342 (`TryComposeAlign` path) and L358 (`TryRunSingleDatum` path), both inside `ProcessDatumSingleImage`, both placed *after* the inner success/failure if-else (so called regardless of detection result) — same as original. Zero occurrences inside `ProcessDatumDualImage`. |
| 7 | 6 Step transitions preserved: Init→MoveZ / MoveZ→DatumPhase / DatumPhase→(bDatumOnly?End:Grab) / Grab→Measure (unconditional, D-5) / Measure→End / End→FinishAction | **PASS** | L122 `Step = (int)EStep.MoveZ;` (RunInit) / L147 `Step = (int)EStep.DatumPhase;` (RunMoveZ) / L206+L208 conditional End-or-Grab (RunDatumPhase) / L420 `Step = (int)EStep.Measure;` confirmed **outside** (after) the `if (ShotParam != null && !ShotParam.HasImage) { ... }` block in `RunGrab` — unconditional, matching D-5 / L513 `Step = (int)EStep.End;` (RunMeasure) / L717 `FinishAction(finishResult);` (RunEnd). |
| 8 | Dispose order: `imgH`→`imgV` / `img` / `crossZSharedSrc` / `crossZRoleImage` / `sharedSrc.Release()` / `image` (using) / `pMyContext.ResultHalconImage` — all same finally, same order | **PASS** | `ProcessDatumDualImage` finally: L306 `imgH.Dispose()` then L307 `imgV.Dispose()`. `ProcessDatumSingleImage` finally: L361 `img.Dispose()`. `FinalizeFaiTick`: inner finally L692 `crossZSharedSrc.Release()`, outer finally L707 `crossZRoleImage.Dispose()` — same nested try/finally structure as original. `RunMeasure`: `using (var image = ...)` opens L446, outer finally L484 `sharedSrc.Release()`. `pMyContext.ResultHalconImage.Dispose()` appears at L408 (RunGrab, before reassignment) and L700 (FinalizeFaiTick, before crossZ reassignment) — both at original relative positions. |
| 9 | `try` start point still immediately after `sharedSrc` creation (260810 round4 fix) | **PASS** | L451 `SharedHImage sharedSrc = null;` → L452 conditional init → L458 `try {` — the WHY comment (L453-457, "260810 hbk quick-debug(capture-render-per-fai-slow) round4 fix") documenting this exact placement is preserved immediately above. This entire block remained inside `RunMeasure()` untouched by extraction. |
| 10 | `using (var image = ...)` remains in `RunMeasure()`; extracted methods never call `image.Dispose()` | **PASS** | `grep -c 'using (var image = ShotParam.GetImage())'` scoped to `RunMeasure` = 1; `sed -n '/private void ProcessOneMeasurement(/,/^        }$/p' $F \| grep -c 'image\.Dispose()'` = 0. |
| 11 | Both `#if SIMUL_MODE` blocks fully contained within a single method | **PASS** | Block 1 (L138-145) fully inside `RunMoveZ` (L127-161). Block 2 (L374-390) fully inside `RunGrab` (L367-426). (A 3rd `#if SIMUL_MODE` pre-existing block at L746-772 belongs to an untouched helper method outside the Run() extraction scope and a 4th "match" at L1478 is a comment string, not code — both unaffected by this refactor, same as in baseline.) |
| 12 | `pMyContext.AllPass`/`MeasuredCount`/`InspectionOverlays` assignment still outside `if (ShotParam != null)` (D-6) | **PASS** | `RunMeasure` L445 `if (ShotParam != null) {` closes before L495-497 `pMyContext.AllPass = allPass; pMyContext.MeasuredCount = measuredCount; pMyContext.InspectionOverlays = overlayAcc;` — same indent level (12) as the `if`'s own opening brace, i.e. outside it. |
| 13 | 4 preserved Stopwatches (`swDatumPhase`/`swGrabTotal`/`swMeasureTotal`/`swMeasureExec`) still alive, 4 consuming logs still fire with identical format strings | **PASS** | `swDatumPhase`: declared L163, consumed L204 (`[SEQ]` DatumPhase 완료). `swGrabTotal`: declared L370, consumed L418 (`[SEQ]` Grab 완료). `swMeasureTotal`: declared L435, consumed L511 (`[SEQ]` Measure 완료). `swMeasureExec`: declared L624, consumed L650 (`[ALGO]` log). All 4 format strings byte-identical to `cb284f4` (verified via the Task 2 normalized-diff pass, which covers this exact code). |
| 14 | WHY/초보자용 comments moved, not deleted (0 deletions) | **PASS** | `git show eefda4a:$F \| grep -c '260807\|Phase 54\|Phase 57\|Phase 68\|260810\|260729\|260811\|초보자용'` = 58; same grep against the final file (post-Task-3) = 58. Equal count confirms zero comment loss across both extraction tasks. |

**All 14 items PASS.** Combined with Task 2's whitespace-normalized byte-identical diff (zero unexpected differences across all 6 `Run*` methods) and Task 3's line-range-preserving Python extraction (no manual retyping of moved code, only mechanical dedent + `continue;`→`return;` substitution at exactly the 12 expected sites), this constitutes strong evidence that the refactor is behavior-preserving.

## Build Verification (G-5)

Two build configurations were verified at each task boundary (Task 1, Task 2, Task 3) plus once more after the final commit — 4 SIMUL passes + 3 non-SIMUL passes total, all succeeding with warning counts matching the pre-refactor baseline:

| Configuration | Warnings | Baseline | Match |
|---|---|---|---|
| Debug\|x64, SIMUL_MODE (default) | 12 (CS0618×10 + CS0162×2) | 12 | ✅ every check |
| Debug\|x64, non-SIMUL (`DefineConstants=TRACE;DEBUG` override, compiles the `#else` real-hardware branch) | 10 (CS0618×10) | 10 | ✅ every check |

No new `CS0219`/`CS0168` (unused local variable) warnings appeared at any point, confirming the 18 timing-only variables deleted in Task 1 were fully removed (declaration + all usages) with no orphaned references. After each non-SIMUL verification build, the SIMUL configuration was rebuilt (`-t:Rebuild`) to restore `obj/x64/Debug/` state, per G-5's requirement to prevent the classic-csproj incremental-build cache from serving stale non-SIMUL binaries on the user's next build. All builds used scratch `OutputPath` (never touched `D:\Data\DatumMeasurement.exe`), and no running process was ever terminated.

## User UAT Request (not a checkpoint — recorded per plan's `<verification>` section D)

The following should be confirmed by the user on next app run, ideally with 1 full inspection cycle:

1. Does the `[SEQ]` log still print all 3 tact lines (DatumPhase/Grab/Measure completion, each with `{N:F2}초`)?
2. Does the `[ALGO]` log still print once per measurement executed?
3. Are the P/F judgement results and measured values identical to pre-refactor behavior on the same recipe/image set?

## Known Stubs

None.

## Self-Check: PASSED

See below.
