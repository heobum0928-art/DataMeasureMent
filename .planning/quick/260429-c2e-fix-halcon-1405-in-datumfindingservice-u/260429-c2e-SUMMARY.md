---
phase: quick-260429-c2e
plan: 01
status: incomplete
subsystem: halcon-datum
tags: [bugfix, halcon, datum, intersection-ll, ConcatObj, TupleConcat]
requirements:
  - QUICK-260429-c2e-FIX-HALCON-1405
dependency-graph:
  requires:
    - WPF_Example/Halcon/Algorithms/DatumFindingService.cs (Phase 12 ConcatObj path)
    - TryFindLine pattern at line ~833 (template for the fix)
  provides:
    - CTH/VTH datum teach paths now produce length-1 fit tuples (compatible with IntersectionLl)
  affects:
    - DatumConfig CTH/VTH consumers (RenderDatumOverlay, MainView UpdateDatumRefCoordsLabel)
tech-stack:
  added: []
  patterns:
    - "HTuple.TupleConcat → single GenContourPolygonXld → FitLineContourXld → IntersectionLl (TryFindLine 833 line pattern)"
key-files:
  created: []
  modified:
    - WPF_Example/Halcon/Algorithms/DatumFindingService.cs
decisions:
  - Unify CTH and VTH horizontal-line fitting under TryFindLine pattern (single contour, length-1 fit results) instead of patching IntersectionLl arg lengths
  - Keep IntersectionLl call sites + overlap/parallel guards untouched — they already accept scalar tuples
  - Local variable rename: contourA/contourB/concatContour → single 'contour' (mirrors TryFindLine line 830)
metrics:
  duration: 5 min
  completed: "2026-04-29"
  tasks_executed: 1
  tasks_remaining: 1  # Task 2 = human-verify checkpoint
  files_modified: 1
  commits: 1
---

# Quick Task 260429-c2e: Fix HALCON #1405 in DatumFindingService CTH/VTH Summary

CTH/VTH datum teach paths now use the TryFindLine line-833 pattern (TupleConcat → single GenContourPolygonXld → FitLineContourXld → IntersectionLl), eliminating HALCON #1405 ("Wrong number of values of control parameter 5 in operator intersection_ll") that occurred after Datum ROI 이동 + 재티칭.

## Task 1 Execution (auto)

### Edits Applied

Six in-file edits across CTH (`TryTeachCircleTwoHorizontal`, ~line 316-495) and VTH (`TryTeachVerticalTwoHorizontal`, ~line 499-668) paths:

| # | Path | Location | Change |
|---|------|----------|--------|
| 1 | CTH | local declarations (~line 320-322) | `HObject contourA/contourB/concatContour = null` → `HObject contour = null` |
| 2 | CTH | fit block (~line 412-422) | `GenContourPolygonXld×2 + ConcatObj + FitLineContourXld(concatContour)` → `TupleConcat → GenContourPolygonXld(contour, allRows, allCols) → FitLineContourXld(contour)` |
| 3 | CTH | finally block (~line 491-493) | three Dispose blocks → single `contour.Dispose()` |
| 4 | VTH | local declarations (~line 503-505) | same as #1 |
| 5 | VTH | fit block (~line 587-597) | same as #2 |
| 6 | VTH | finally block (~line 664-666) | same as #3 |

All modified/added lines carry trailing `//260429 hbk #1405 fix — <reason>` comments per project convention.

### Why This Fixes #1405

`FitLineContourXld` on a **concatenated XLD** of two separately-generated polygon contours returns length-2 tuples for `lineRowBegin/lineColBegin/lineRowEnd/lineColEnd` (one fit per source contour). HALCON's `IntersectionLl` requires all 8 line-defining tuples to share the same length. The CTH path passes `centerRow±1.0`, `centerCol` (length-1 scalars) for the vertical line; the VTH path passes `vrB/vcB/vrE/vcE` (length-1 scalars from `TryFindLine`). With horizontal tuples being length-2, args 1-4 (length 1) and args 5-8 (length 2) mismatch → #1405.

By pre-concatenating the row/col edges via `HTuple.TupleConcat` and feeding them into a **single** `GenContourPolygonXld` call, only one contour exists, `FitLineContourXld` returns length-1 results, and `IntersectionLl` receives consistent scalar arguments throughout. This is the same pattern already used (and working) in `TryFindLine` at line 833, which is why TwoLineIntersect and runtime `TryFindDatum` were never affected.

### Static Verification (grep checks per plan §verify)

| Check | Pattern | Expected | Actual | Result |
|-------|---------|----------|--------|--------|
| 1 | `contourA\|contourB\|concatContour\|HOperatorSet\.ConcatObj` (any) | 0 | 4 (all in `//260429 hbk` comments documenting what was removed) | PASS — 0 actual code references; `HOperatorSet.ConcatObj` = 0; non-comment matches via `^[^/]*\b(contourA\|contourB\|concatContour)\b` = 0 |
| 2 | `TupleConcat` count | ≥4 | 8 (4 new in CTH/VTH rows+cols + 4 pre-existing in TryFindLine/TryExtractEdgePoints/AppendEdgePointsFromStrip) | PASS |
| 3 | `//260429 hbk` comment count | ≥8 | 14 | PASS |
| 4 | `HOperatorSet.IntersectionLl` count (must remain unchanged) | 4 | 4 | PASS — call sites untouched |
| 5 | `GenContourPolygonXld(out contour, allRows, allCols)` pattern | present | 3 sites: CTH @414, VTH @587, TryFindLine @829 (pre-existing) | PASS — CTH/VTH now match the working TryFindLine pattern |

### Commit

| Plan | Commit | Files | Insertions / Deletions |
|------|--------|-------|------------------------|
| 260429-c2e-01 | `311012a` | WPF_Example/Halcon/Algorithms/DatumFindingService.cs | +18 / -22 |

## Task 2 Status (checkpoint:human-verify) — REMAINING

This task cannot be executed by the executor agent; it requires human action in Visual Studio + on real hardware/data:

1. **Build:** Open `WPF_Example/DatumMeasurement.csproj` in Visual Studio, select **Debug/x64**, **Build → Rebuild Solution**. Expected: 0 errors, no new warnings introduced in `DatumFindingService.cs`.
2. **CTH (CircleTwoHorizontal) UAT:** Load a recipe with `AlgorithmType = CircleTwoHorizontal`, move the Datum Circle ROI in the teaching UI, re-run datum teach. Expected: teach succeeds, no HALCON #1405, datum origin overlay updates.
3. **VTH (VerticalTwoHorizontal) UAT:** Load a recipe with `AlgorithmType = VerticalTwoHorizontal`, move one of the Datum ROIs, re-run datum teach. Expected: teach succeeds, no HALCON #1405.
4. **TwoLineIntersect regression check:** Confirm at least one TwoLineIntersect datum still teaches correctly (this code path was NOT modified, but verify nothing regressed indirectly).

If any step fails, report which step + the HALCON error code + log evidence. Resume signal: `approved` after CTH + VTH + TwoLineIntersect all PASS.

## Deviations from Plan

None — plan executed exactly as written. All four edits applied verbatim; no auto-fix rules triggered.

## Deferred Items

- **MSBuild compile verification:** Visual Studio MSBuild is not invokable from this CLI executor; static grep verification confirms structural correctness, but only the human-verify checkpoint (Task 2 step 1) can prove a clean Debug/x64 build with zero new warnings.
- **Real-data UAT for CTH/VTH/TwoLineIntersect:** Requires running the WPF app against a real recipe; deferred to Task 2 human-verify checkpoint.

## Self-Check: PASSED

- `WPF_Example/Halcon/Algorithms/DatumFindingService.cs` — modified, FOUND
- `git log` contains commit `311012a` — FOUND (verified via `git rev-parse --short HEAD`)
- All 6 plan-specified edits applied (CTH decl + body + finally; VTH decl + body + finally), confirmed by `git diff HEAD~1 HEAD`
- All static grep checks (5/5) PASSED
- SUMMARY.md frontmatter status correctly marked `incomplete` (Task 2 human-verify remaining)

## Outstanding Work for Orchestrator

- Surface Task 2 (build + UAT) to the user for human verification.
- Do NOT mark this quick task fully complete until UAT approval is recorded.
