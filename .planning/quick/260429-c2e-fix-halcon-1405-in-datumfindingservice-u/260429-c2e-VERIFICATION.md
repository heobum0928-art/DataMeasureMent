---
phase: quick-260429-c2e
verified: 2026-04-29T00:00:00Z
status: human_needed
score: 5/7 must-haves verified (5 static-PASS; 2 require human runtime UAT; 1 build-check requires Visual Studio)
overrides_applied: 0
human_verification:
  - test: "CTH (CircleTwoHorizontal) datum teach succeeds on real data after Datum ROI 이동 + 재티칭"
    expected: "Teach succeeds (config.LastTeachSucceeded = true). No HALCON #1405 in logs / no error toast. Datum origin (curRow, curCol) updates."
    why_human: "Requires running WPF app against real recipe + camera/image. Cannot be exercised programmatically by static verifier."
  - test: "VTH (VerticalTwoHorizontal) datum teach succeeds on real data after Datum ROI 이동 + 재티칭"
    expected: "Teach succeeds. No HALCON #1405. Datum origin overlay updates correctly."
    why_human: "Requires running WPF app against real recipe + camera/image."
  - test: "Project compiles in Visual Studio Debug/x64 with no new errors/warnings in DatumFindingService.cs"
    expected: "Rebuild Solution → 0 errors, no new warnings introduced by this commit."
    why_human: "No CLI MSBuild available in this session; project requires Visual Studio."
  - test: "TwoLineIntersect datum still teaches correctly (negative regression check)"
    expected: "At least one TwoLineIntersect datum teach passes — diff scope shows no edits to that code path, but runtime regression must be confirmed."
    why_human: "Requires running WPF app against real recipe."
---

# Quick Task 260429-c2e: HALCON #1405 Fix in DatumFindingService Verification Report

**Phase Goal:** Fix HALCON #1405 in DatumFindingService — unify CTH/VTH ConcatObj→FitLine pattern with TryFindLine TupleConcat pattern.

**Verified:** 2026-04-29
**Status:** human_needed (5 static must-haves PASS; 3 runtime/build must-haves require human UAT)
**Re-verification:** No — initial verification.

## Goal Achievement

### Observable Truths

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | CTH datum teach succeeds on real data after ROI 이동 + 재티칭 (no #1405) | ? HUMAN | Runtime behavior on real hardware; cannot be exercised by static verifier. Static prerequisites (length-1 fit tuples → IntersectionLl arg consistency) all PASS. |
| 2 | VTH datum teach succeeds on real data after ROI 이동 + 재티칭 (no #1405) | ? HUMAN | Same as #1 — runtime UAT. Static prerequisites all PASS. |
| 3 | DatumFindingService.cs no longer calls HOperatorSet.ConcatObj in CTH/VTH datum teach paths | ✓ VERIFIED | `Grep HOperatorSet\.ConcatObj` → 0 matches in entire file. Diff at 311012a removes both `HOperatorSet.ConcatObj(contourA, contourB, out concatContour)` calls (CTH @ ~411, VTH @ ~584 of pre-fix file). |
| 4 | Uses HTuple.TupleConcat of row/col edges + single GenContourPolygonXld in CTH/VTH (matching TryFindLine pattern at line 829) | ✓ VERIFIED | CTH: lines 412-414 (`rowEdgeA.TupleConcat(rowEdgeB)`, `colEdgeA.TupleConcat(colEdgeB)`, `GenContourPolygonXld(out contour, allRows, allCols)`). VTH: lines 585-587 (identical). Reference TryFindLine: line 829 (identical pattern). |
| 5 | FitLineContourXld returns length-1 (scalar) HTuples for hrB/hcB/hrE/hcE in CTH/VTH paths, so IntersectionLl args 5-8 are scalar | ✓ VERIFIED | Single contour input → single fit → length-1 outputs. CTH FitLineContourXld @ line 419-421 uses `contour` (single object); VTH @ line 592-594 uses `contour` (single object). Same pattern as TryFindLine line 829-833 which is known-working. IntersectionLl call sites unchanged (CTH line 432-435, VTH line 605-608). |
| 6 | Project compiles in Visual Studio with no new errors/warnings | ? HUMAN | No CLI MSBuild in session. Static structural review of diff shows no syntax issues — all introduced symbols (`allRows`, `allCols`, `contour`) declared before use; removed locals (`contourA`, `contourB`, `concatContour`) have no remaining references. |
| 7 | Behavior of non-2-line algorithms (TwoLineIntersect, FourLines, etc.) unchanged | ✓ VERIFIED | `git show --stat 311012a`: 1 file changed, +18/-22, all hunks confined to CTH (`TryTeachCircleTwoHorizontal`, lines 317-493) and VTH (`TryTeachVerticalTwoHorizontal`, lines 498-664). TwoLineIntersect (IntersectionLl @ line 98, 239), FourLines, TryFindLine (line 829), TryFindCircleByPolarSampling — all untouched. |

**Static-checkable score:** 5/5 PASS. **Runtime/build score:** 0/3 (deferred to human UAT — gates exist in plan as Task 2 checkpoint).

### Required Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `WPF_Example/Halcon/Algorithms/DatumFindingService.cs` | CTH/VTH teach paths fixed to single-contour FitLineContourXld; contains "TupleConcat" | ✓ VERIFIED | Exists, 1024 lines, contains 8 `TupleConcat` references (4 new in CTH/VTH at lines 412-413, 585-586; 4 pre-existing in TryFindLine area / AppendEdgePointsFromStrip at lines 1011-1012). All 4 plan-targeted edits applied. |

### Key Link Verification

| From | To | Via | Status | Details |
|------|-----|-----|--------|---------|
| CTH path (~line 412) | single GenContourPolygonXld + FitLineContourXld + IntersectionLl chain | `HTuple.TupleConcat(rowEdgeA, rowEdgeB)` for rows and cols | ✓ WIRED | Lines 412-414 perform TupleConcat → GenContourPolygonXld → 419-421 FitLineContourXld(contour, ...) → 432-435 IntersectionLl. Linear chain confirmed. |
| VTH path (~line 585) | single GenContourPolygonXld + FitLineContourXld + IntersectionLl chain | `HTuple.TupleConcat(rowEdgeA, rowEdgeB)` for rows and cols | ✓ WIRED | Lines 585-587 perform TupleConcat → GenContourPolygonXld → 592-594 FitLineContourXld(contour, ...) → 605-608 IntersectionLl. Linear chain confirmed. |
| CTH finally block (~line 488-492) | disposal of remaining HObject locals | removed contourB and concatContour disposal lines (locals deleted) | ✓ WIRED | Only `if (contour != null) { try { contour.Dispose(); } catch { } }` remains at line 491. contourA/contourB/concatContour disposals removed (confirmed by diff). |
| VTH finally block (~line 659-663) | disposal of remaining HObject locals | removed contourB and concatContour disposal lines (locals deleted) | ✓ WIRED | Only `if (contour != null) { try { contour.Dispose(); } catch { } }` remains at line 662. contourA/contourB/concatContour disposals removed (confirmed by diff). |

### Behavioral Spot-Checks

| Behavior | Command | Result | Status |
|----------|---------|--------|--------|
| No HOperatorSet.ConcatObj remains in DatumFindingService.cs | `grep HOperatorSet\.ConcatObj` on file | 0 matches | ✓ PASS |
| TupleConcat appears ≥4 times | `grep TupleConcat` count | 8 matches (4 new + 4 pre-existing) | ✓ PASS |
| //260429 hbk comment count ≥8 | `grep //260429 hbk` count | 14 matches | ✓ PASS |
| IntersectionLl call sites count = 4 (unchanged) | `grep HOperatorSet\.IntersectionLl` count | 4 matches (lines 98, 239, 432, 605) | ✓ PASS |
| Non-comment references to contourA/contourB/concatContour | manual inspection of grep matches | 4 matches — all in `//260429 hbk` comment strings (lines 411 prose, 490 comment, 584 prose, 661 comment); 0 actual symbol references | ✓ PASS |
| Diff scope confined to CTH+VTH | `git show --stat 311012a` | 1 file, +18/-22, hunks @ lines 317, 408, 487, 498, 581, 658 — all inside CTH/VTH method bodies | ✓ PASS |
| Reference pattern in TryFindLine matches CTH/VTH new code | Read line 829-833 vs CTH 414-421 vs VTH 587-594 | All three sites use identical `GenContourPolygonXld(out contour, allRows, allCols)` + `FitLineContourXld(contour, "tukey", -1, 0, 5, 2, ...)` shape | ✓ PASS |

### Anti-Patterns Found

| File | Line | Pattern | Severity | Impact |
|------|------|---------|----------|--------|
| (none) | — | — | — | No TODO/FIXME/placeholder/stub introduced by this commit. All edits are concrete code replacements. |

### Comment Convention Check

CLAUDE.md mandates `//260429 hbk` (YYMMDD hbk) trailing comments on modified lines.
- Required: ≥8 (4 lines × 2 paths per plan).
- Actual: 14 occurrences in DatumFindingService.cs.
- Distribution: CTH path lines 320, 411, 412, 413, 414, 420, 490 (7) + VTH path lines 501, 584, 585, 586, 587, 593, 661 (7) = 14.
- Status: ✓ PASS — convention applied correctly.

### Human Verification Required

1. **Build verification**
   - **Test:** Open `WPF_Example/DatumMeasurement.csproj` in Visual Studio, select Debug/x64, **Build → Rebuild Solution**.
   - **Expected:** 0 errors, no new warnings introduced in `DatumFindingService.cs`.
   - **Why human:** No CLI MSBuild available; .NET Framework 4.8 + WPF requires Visual Studio toolchain.

2. **CTH (CircleTwoHorizontal) UAT**
   - **Test:** Load a recipe with `AlgorithmType = CircleTwoHorizontal`. In teaching UI, move the Datum Circle ROI (any small translation). Re-run datum teach.
   - **Expected:** Teach succeeds (`config.LastTeachSucceeded = true`). No HALCON #1405 in logs / no error toast. Datum origin (curRow, curCol) updates to a sensible value (overlay shifts with ROI).
   - **Why human:** Requires running WPF app against real camera/image data and observing teach result.

3. **VTH (VerticalTwoHorizontal) UAT**
   - **Test:** Load a recipe with `AlgorithmType = VerticalTwoHorizontal`. In teaching UI, move one of the Datum ROIs (e.g., one horizontal ROI). Re-run datum teach.
   - **Expected:** Teach succeeds. No HALCON #1405. Datum origin overlay updates correctly.
   - **Why human:** Requires running WPF app against real recipe.

4. **TwoLineIntersect negative regression**
   - **Test:** Confirm at least one TwoLineIntersect datum still teaches correctly.
   - **Expected:** No regression. (Static diff scope confirms this code path was not modified, but runtime check guards against indirect effects.)
   - **Why human:** Requires running WPF app against real recipe.

### Gaps Summary

No static gaps found — all 5 static-checkable must-haves PASS:
- Broken pattern (`HOperatorSet.ConcatObj`, `contourA/contourB/concatContour` locals) fully eliminated.
- New pattern (TupleConcat → single GenContourPolygonXld → FitLineContourXld(contour) → IntersectionLl) correctly applied at both CTH and VTH sites.
- Pattern matches reference template at TryFindLine line 829.
- Diff scope strictly limited to CTH and VTH method bodies — no risk to TwoLineIntersect, FourLines, TryFindLine, or other algorithm paths.
- Comment convention `//260429 hbk` correctly applied (14 occurrences).

3 must-haves remain unverifiable by static analysis and require human action:
1. Visual Studio Debug/x64 clean build.
2. CTH datum teach UAT on real data.
3. VTH datum teach UAT on real data.

Plus 1 negative-regression UAT (TwoLineIntersect) recommended by the plan.

The plan correctly anticipated this division of labor — Task 2 is a `checkpoint:human-verify` gate. Static verification establishes high confidence that the runtime UAT will succeed (the new pattern is identical to known-working TryFindLine), but goal achievement is not provable until a human runs the build + the 3 datum teach scenarios.

---

_Verified: 2026-04-29_
_Verifier: Claude (gsd-verifier)_
