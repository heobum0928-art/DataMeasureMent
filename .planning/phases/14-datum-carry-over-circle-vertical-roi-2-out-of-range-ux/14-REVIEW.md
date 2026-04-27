---
phase: 14-datum-carry-over-circle-vertical-roi-2-out-of-range-ux
reviewed: 2026-04-27T00:00:00Z
depth: standard
files_reviewed: 6
files_reviewed_list:
  - WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs
  - WPF_Example/Halcon/Algorithms/DatumFindingService.cs
  - WPF_Example/Halcon/Algorithms/VisionAlgorithmService.cs
  - WPF_Example/Halcon/Display/HalconDisplayService.cs
  - WPF_Example/UI/ContentItem/MainResultViewerControl.xaml.cs
  - WPF_Example/UI/ContentItem/MainView.xaml.cs
findings:
  critical: 0
  warning: 2
  info: 5
  total: 7
status: issues_found
---

# Phase 14: Code Review Report

**Reviewed:** 2026-04-27
**Depth:** standard
**Files Reviewed:** 6
**Status:** issues_found

## Summary

Phase 14 covered five carry-over fixes: Datum Circle Move/Resize handle support, TwoLineIntersect orthogonality gate, Vertical group separation from `Line1_*` slot reuse, polar-sampling Circle detection (`TryFindCircleByPolarSampling`), and out-of-range error UX literals.

Overall the changes are well-structured and follow existing project conventions (Allman braces in Halcon files, K&R in older UI; HOperatorSet calls wrapped in try/catch; HObject/measureHandle disposed in finally; per-step `Logging.PrintLog` traces). The new Vertical slot migration is idempotent and INI-backwards-compatible. The polar sampling design correctly accumulates raw edge points and reuses `AppendEdgePointsFromStrip` swallow conventions.

Issues identified:

- One **Warning** in `MainView.HandleDatumRoiResize` where Rect-shaped Datum ROI resize events fall through silently without DatumConfig write-back, then trigger an auto-reteach against stale geometry.
- One **Warning** about a no-op smoke harness (`RunPhiSmokeTest`) whose "expected" and "actual" expressions are identical, so the trace log always reports `delta=0.00` and validates nothing.
- Several **Info** items: leftover diagnostic `Logging.PrintLog` calls explicitly marked for later removal, an undocumented upper clamp on `stepDeg`, magic-number constants, and intentional but surprising auto-population of Vertical fields from Line1 fields during INI migration.

No critical bugs, security issues, or HImage/handle leaks identified.

## Warnings

### WR-01: Datum Rect ROI resize silently drops geometry write-back, then auto-reteach uses stale config

**File:** `WPF_Example/UI/ContentItem/MainView.xaml.cs:597-620`
**Issue:** `HandleDatumRoiResize` only writes back when `e.RoiId == "Datum.Circle"`. For any Rect-shaped Datum ROI (`Datum.Vertical`, `Datum.HorizontalA`, `Datum.HorizontalB`, `Datum.Line1`, `Datum.Line2`), the early-return at line 456 prevents the FAI handler from running, but the Rect branch in this method is empty (only a comment "Rect 핸들은 Phase 14 scope 외"). The dispatcher-deferred `InvokeTryTeachDatumForEdit` then runs against the unchanged `DatumConfig.Vertical_*`/`Horizontal_*_*` fields, so re-teach uses stale geometry while the viewer's RoiDefinition has already drifted.

In Phase 14 the Edit handles are restricted to `RoiShape.Circle` (`MainResultViewerControl.xaml.cs:508`, `MainResultViewerControl.xaml.cs:785`), so today this path is normally unreachable. However, the silent fall-through is fragile: if a future change widens handle exposure (e.g., Vertical Edit handles) the auto-reteach will run on stale config, producing confusing PASS/FAIL inconsistency with no diagnostic.

**Fix:** Either explicitly log/refuse non-Circle Rect resize, or implement Rect write-back now to remove the latent hazard:

```csharp
private void HandleDatumRoiResize(DatumConfig datum, RoiGeometryChangedArgs e) {
    if (datum == null || e == null) return;

    if (e.RoiId == "Datum.Circle" && e.Shape == RoiShape.Circle) {
        datum.CircleROI_Row = e.CenterRow;
        datum.CircleROI_Col = e.CenterCol;
        datum.CircleROI_Radius = e.Radius;
    }
    else if (e.Shape == RoiShape.Rect) {
        // Defensive: log so this never silently re-teaches against stale geometry
        Logging.PrintLog((int)ELogType.Trace,
            "Datum Rect resize ignored (Phase 14 scope): id=" + e.RoiId);
        return; // 자동 재티칭 skip (stale geometry)
    }
    // ... existing post-write-back block ...
}
```

### WR-02: `RunPhiSmokeTest` compares two identical expressions, so delta is always 0 (no actual verification)

**File:** `WPF_Example/Halcon/Algorithms/VisionAlgorithmService.cs:353-376`
**Issue:** Both `rectRow`/`rectCol` and `expectedRow`/`expectedCol` use the same formula (`centerRow - radius * Math.Sin(thetaRad)`, `centerCol + radius * Math.Cos(thetaRad)`). The computed `delta = sqrt(0 + 0) = 0` for every call. The trace logs always read `delta=0.00`, so reading them gives false confidence that the phi sign convention is verified — in fact the harness only echoes the same calculation back to itself. The internal comment ("동일 식 (좌표 계산 자체 검증). 실 위치는 Halcon debugger 가 시각으로 확인.") acknowledges this, but the method name `RunPhiSmokeTest` and "expected vs actual" log format invite a future reader to trust the delta as a real test.

**Fix:** Either delete the harness (the production code path is unchanged) or replace `expected*` with an independent reference formula that would diverge if a sign flip were introduced — for example, hand-precomputed expected coordinates for the four canonical thetas:

```csharp
public void RunPhiSmokeTest(HImage image, double centerRow, double centerCol, double radius)
{
    if (image == null) return;
    // Expected (row,col) per screen-CCW: 0°=right, 90°=up, 180°=left, 270°=down.
    // If sign convention regresses, these literals will diverge from the polar formula.
    var cases = new (double thetaDeg, double expRow, double expCol)[]
    {
        (  0.0, centerRow,            centerCol + radius),
        ( 90.0, centerRow - radius,   centerCol         ),
        (180.0, centerRow,            centerCol - radius),
        (270.0, centerRow + radius,   centerCol         ),
    };
    foreach (var c in cases)
    {
        double thetaRad = c.thetaDeg * Math.PI / 180.0;
        double rectRow = centerRow - radius * Math.Sin(thetaRad);
        double rectCol = centerCol + radius * Math.Cos(thetaRad);
        double dRow = Math.Abs(rectRow - c.expRow);
        double dCol = Math.Abs(rectCol - c.expCol);
        ReringProject.Utility.Logging.PrintLog((int)ReringProject.Setting.ELogType.Trace,
            "PHI_SMOKE: theta=" + c.thetaDeg.ToString("F0") +
            " expected=(" + c.expRow.ToString("F1") + "," + c.expCol.ToString("F1") + ")" +
            " actual=(" + rectRow.ToString("F1") + "," + rectCol.ToString("F1") + ")" +
            " delta=" + Math.Sqrt(dRow * dRow + dCol * dCol).ToString("F2"));
    }
}
```

## Info

### IN-01: `stepDeg > 30` upper clamp is undocumented and silently caps user input

**File:** `WPF_Example/Halcon/Algorithms/VisionAlgorithmService.cs:232-233`
**Issue:** `if (stepDeg > 30) stepDeg = 30.0;` silently caps user-supplied `Circle_PolarStepDeg`. The DatumConfig comment (`DatumConfig.cs:201`) states the range hint as "1~30°" but doesn't note the silent upper-clamp. A user who enters 45° to deliberately reduce sample count gets quietly rewritten to 30° with no log/UX feedback.
**Fix:** Either add a `Logging.PrintLog` warning when the clamp activates, or surface the cap in the DatumConfig property comment so the PropertyGrid hint matches runtime behavior.

### IN-02: Diagnostic `Logging.PrintLog` calls left in HandleDatumRoiMove / InvokeTryTeachDatumForEdit (acknowledged)

**File:** `WPF_Example/UI/ContentItem/MainView.xaml.cs:539-543, 700, 717`
**Issue:** Three diagnostic Trace logs are explicitly tagged "Phase 14-05 verify 시 PASS 면 제거 가능" / "진단" — Phase 14 is now marked complete (per `STATE.md`). They will continue to write to the trace log every time a Datum ROI is moved or auto-reteach fires.
**Fix:** Now that Phase 14 has shipped, prune them in a follow-up cleanup commit.

### IN-03: `MIN_HORIZONTAL_EDGES` magic threshold is a class-level const but `2*trimCount + 1` ad-hoc threshold is inline

**File:** `WPF_Example/Halcon/Algorithms/DatumFindingService.cs:19, 807, 819, 944, 960`
**Issue:** `MIN_HORIZONTAL_EDGES = 10` is a named constant, but the strip-loop uses inline magic numbers `2 * trimCount + 1` (the trimming pre-condition) and `< 2` (post-trim minimum for FitLine). All three numbers describe the same "minimum edges to fit a line" idea but live in three different forms.
**Fix:** Hoist the post-trim minimum to a named const, e.g. `private const int MIN_FITLINE_EDGES = 2;` and reuse it in both `TryFindLine` and `TryExtractEdgePoints`.

### IN-04: EnsurePerRoiDefaults migration silently copies Line1 geometry into Vertical_* when user switches algorithms

**File:** `WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs:451-459`
**Issue:** When a recipe is loaded with `AlgorithmType=VerticalTwoHorizontal` and `Vertical_Length1==0`, the code silently copies `Line1_Row/Col/Phi/Length1/Length2` into the Vertical slot. This is documented as intentional INI-migration behavior (`D-05`), but the same code path triggers if a user manually switches a TwoLineIntersect recipe to VerticalTwoHorizontal in the PropertyGrid: their old Line1 geometry will pre-populate Vertical without confirmation. Acceptable per the design comment but worth surfacing to the operator (a one-shot label on first load would close this gap).
**Fix:** No code change required if the team accepts the design. If user-visibility is desired, raise `label_drawHint` once after the migration runs ("Vertical ROI imported from Line1 — review before re-teach").

### IN-05: `Sigma=0.5` user input gets silently overwritten by sanity clamp

**File:** `WPF_Example/Halcon/Algorithms/DatumFindingService.cs:741, 878`
**Issue:** `if (sigma < 0.4) sigma = 1.0;` — Halcon `MeasurePos` actually accepts sigma>=0.4. A user explicitly entering `0.3` (slightly out of range) gets silently bumped to `1.0` rather than the boundary value `0.4`. The reset is conservative but jumps to a far-away default.
**Fix:** Snap to the Halcon minimum instead of the legacy default:
```csharp
if (sigma < 0.4) sigma = 0.4;  // Halcon MeasurePos minimum (preserve user intent better than 1.0)
```
(EnsurePerRoiDefaults at the DatumConfig layer still maps 0 → 1.0, so this only affects sub-0.4 non-zero entries.)

---

_Reviewed: 2026-04-27_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
