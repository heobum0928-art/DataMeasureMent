---
phase: quick-260818-ef5
reviewed: 2026-08-18T03:01:59Z
depth: standard
files_reviewed: 1
files_reviewed_list:
  - WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs
findings:
  critical: 0
  warning: 0
  info: 1
  total: 1
status: issues_found
---

# Quick 260818-ef5: Code Review Report

**Reviewed:** 2026-08-18T03:01:59Z
**Depth:** standard
**Files Reviewed:** 1
**Status:** issues_found (1 Info-level maintainability note; zero behavioral/regression issues found)

## Summary

This review independently re-derives the behavior-preservation claim for the 3-commit refactor
(`eefda4a` → `2b30ded` → `12fa8aa`, diffed against `cb284f4`) of `Action_FAIMeasurement.Run()`,
rather than taking the executor's SUMMARY.md 14-item audit on faith. The full `git diff
cb284f4..HEAD` for this file (1207 lines) was read line-by-line and cross-checked against the
current file content (1670 lines), with particular attention to the four risk categories called
out in the task: branch-order changes, side-effect-order changes, closure/capture mistakes, and
`continue`→`return` conversions that don't actually preserve loop semantics.

**Independently verified, with specific evidence (not just re-stating the SUMMARY's claims):**

1. **Switch→dispatch extraction (Task 2, `2b30ded`) is a pure move.** `Run()` now dispatches to
   `RunInit/RunMoveZ/RunDatumPhase/RunGrab/RunMeasure/RunEnd`, one call per case, each followed by
   `break;`, with `return Context;` after the switch in the same relative position as the original.
   Because every original `case` block already ended in `break` (no fallthrough, no shared
   cross-case local-variable scope), converting each case body into a same-named private method is
   a mechanical, risk-free transform. No `default:` case existed before or after — unchanged.

2. **`continue`→`return` conversions (Task 3, `12fa8aa`) are all valid "whole loop body extracted"
   conversions.** I traced all 12 converted sites:
   - `RunDatumPhase`'s `foreach (var datum in parentSeq.DatumConfigs)` body is *exactly*
     `ProcessOneDatum(datum, parentSeq, ref nDatumOk, ref nDatumFail, ref nDatumCached);` — nothing
     follows it in the loop body — so a `return` inside `ProcessOneDatum` (or in a method it calls
     as its own last statement, `ProcessDatumDualImage`/`ProcessDatumSingleImage`) is exactly
     equivalent to the original `continue`. All 6 datum-phase continues (cache-skip, ZIndex
     misconfigured, cross-Z pending, DualImage acquire-fail, and the two 1-image acquire-fail path)
     map 1:1.
   - `RunMeasure`'s `foreach (var meas in fai.Measurements)` body is *exactly*
     `ProcessOneMeasurement(meas, parentSeq2, image, pixRes, ref crossZRoleImage, ref faiAllPass,
     ref measuredCount, ref nMeasNg, overlayAcc, faiOverlays, dctAlgoUsed);` — same "whole body"
     condition holds. All 6 measure-loop continues (datum-skip gate, datum-ref-unresolvable gate,
     ZIndex misconfigured, cross-Z not-relevant, cross-Z capture-not-ok, cross-Z not-completed) map
     1:1 to `return`.
   - `continue` inside a `try` with a `finally` (e.g. the `imgH.Dispose()/imgV.Dispose()` finally in
     `ProcessDatumDualImage`) still runs the `finally` before unwinding in both the original
     (`continue` runs finally, then continues the loop) and refactored (`return` runs finally, then
     returns to caller, which then continues the loop) forms — verified no early-exit path skips a
     `Dispose()`.
   - Grep confirmed zero stray/orphaned `continue;` remain in the extracted methods (the only 3
     `continue;` left in the file are in unrelated, untouched pre-existing loops:
     `BuildDatumCaptureSnapshot`-style datum iteration and `ApplyOverlaySuffixAndAccumulate`'s
     overlay-suffix loop).

3. **`ref`/pass-by-value parameter wiring is correct for all cross-method state.** Verified by
   checking every variable that needs to persist *across* loop iterations (as opposed to being
   loop-invariant) is passed `ref`: `nDatumOk/nDatumFail/nDatumCached` (per-datum accumulators, `ref`
   into `ProcessOneDatum`), `crossZRoleImage/faiAllPass/measuredCount/nMeasNg` (per-measurement
   accumulators, `ref` into `ProcessOneMeasurement`), and `crossZRoleImage/bShotDisplayImageReplaced/
   allPass` (`ref` into `FinalizeFaiTick`). Loop-invariant reference-type collections
   (`overlayAcc`, `faiOverlays`, `dctAlgoUsed`) are correctly passed without `ref` since only their
   contents are mutated, never the reference itself. No lambda/closure was introduced anywhere in
   this diff, so the "captured by the wrong closure" risk category does not apply here — all
   cross-method state flows through explicit parameters.

4. **D-1 structural asymmetry (DUAL branch never touches `nDatumOk`/`nDatumFail`) is preserved by
   construction**, not just convention: `ProcessDatumDualImage(DatumConfig, InspectionSequence)`'s
   signature has no `ref int` counter parameters, so it is now compile-time impossible for it to
   increment them (matches the original DUAL branch, which never called `nDatumOk++`/`nDatumFail++`,
   only the 1-image branch does).

5. **`[FaiTiming]` diagnostic removal (Task 1, `eefda4a`) has zero surviving references.**
   `grep -n "FaiTiming|//TEMP"` over the current file returns no matches. The 4 surviving
   `Stopwatch` instances (`swDatumPhase`, `swGrabTotal`, `swMeasureTotal`, `swMeasureExec`) are all
   still declared and still feed their respective `[SEQ]`/`[ALGO]` consumer logs with byte-identical
   format strings (spot-checked all 6 log format strings against the pre-refactor diff hunks:
   MoveZ, DatumPhase×2, Grab, Measure×2 — all identical).

6. **All 10 ternary→if-else conversions are semantically equivalent** (each hoists to a
   correctly-typed local assigned in both branches, verified individually against the diff), and a
   post-refactor grep for the `cond ? a : b` pattern across the whole file returns zero remaining
   code-level ternaries (matches the project's `feedback_no_ternary_if_else` convention).

7. **Unconditional `Grab`→`Measure` transition (D-5) and both `if (ShotParam != null)`/
   `if (parentSeq != null && parentSeq.DatumConfigs.Count > 0)` gating conditions (D-4) are
   preserved at identical brace-nesting depth** — confirmed by reading exact indentation/brace
   placement in `RunGrab` and `RunDatumPhase`, not just trusting a whitespace-diff claim.

8. **File structural integrity is intact**: class/namespace closing braces at EOF match, no
   orphaned or duplicated code from the extraction script (checked tail of file, `AggregateFaiResult`
   and `MarkAllMeasurementsNoImage` — both pre-existing, untouched helpers from an earlier
   `260702` refactor — are unchanged and correctly still referenced from `FinalizeFaiTick`/
   `RunMeasure`).

**No branch-order, side-effect-order, closure-capture, or continue→return regressions were found.**
The one finding below is a pre-existing-pattern maintainability observation, not a behavioral
defect, and does not block sign-off.

## Info

### IN-01: High positional-parameter count in the newly extracted per-tick helper methods

**File:** `WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs:517-523` (`ProcessOneMeasurement`, 11 params) and `:671-676` (`FinalizeFaiTick`, 10 params)
**Issue:** Both methods take a long, order-sensitive positional parameter list (4 of which are `ref` accumulators: `crossZRoleImage`, `faiAllPass`/`allPass`, `measuredCount`, `nMeasNg` on `ProcessOneMeasurement`; `crossZRoleImage`, `bShotDisplayImageReplaced`, `allPass` on `FinalizeFaiTick`). This is a deliberate, documented trade-off (SUMMARY.md notes the C# 7.2 constraint against new tuple/record types and cites file precedent `MarkAllMeasurementsNoImage(ref int)`), and both current call sites (`RunMeasure:479` and `:481`) match the declared signatures exactly — so this is **not a bug today**. It is flagged only because a long same-typed-in-a-row parameter list (e.g. three `ref bool`/`ref int` in a row) is exactly the shape of change that's easy to silently break in a *future* edit (accidental parameter reordering compiles cleanly if adjacent types match, e.g. swapping two `ref int` or `ref bool` params).
**Fix:** No action required now. If either method's parameter list is touched again, consider using C# 7.2 named arguments at the call sites (`ProcessOneMeasurement(meas: meas, parentSeq2: parentSeq2, ...)`) to make a future reorder immediately visible as a diff, or (longer-term) group the four `ref` accumulators into a small mutable per-tick state class instantiated once per FAI iteration.

---

_Reviewed: 2026-08-18T03:01:59Z_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
