---
phase: 260723-bsf
plan: 01
type: execute
wave: 1
depends_on: []
files_modified:
  - WPF_Example/Custom/Sequence/Inspection/EdgeOptionLists.cs
  - WPF_Example/Custom/Sequence/Inspection/Measurements/EdgePairDistanceMeasurement.cs
  - WPF_Example/Custom/Sequence/Inspection/Measurements/PointToLineDistanceMeasurement.cs
autonomous: true
requirements:
  - FIX-F-EdgePairSelectionDropdown
  - FIX-G-PointToLinePerPointAveraging

must_haves:
  truths:
    - "A recipe author editing an EdgePairDistance measurement in the PropertyGrid can only choose \"Both\" for EdgeSelection (constrained dropdown, no free-text entry of a wrong value)."
    - "EdgePairDistanceMeasurement.TryExecute distance logic and FAIEdgeMeasurementService.TryMeasure branching remain byte-unchanged (input-constraint-only fix)."
    - "PointToLineDistance.resultValue is the arithmetic mean of each collected edge point's perpendicular distance to the fitted reference line — NOT the distance of a single averaged midpoint."
    - "PointToLineDistance falls back to the existing single-midpoint distance when no edge points are collected (no NaN/crash regression)."
    - "MSBuild Debug/x64 passes with 0 new errors and 0 new warnings vs the known 5-warning baseline."
  artifacts:
    - path: "WPF_Example/Custom/Sequence/Inspection/EdgeOptionLists.cs"
      provides: "New dedicated single-value option list for EdgePair EdgeSelection"
      contains: "Both"
    - path: "WPF_Example/Custom/Sequence/Inspection/Measurements/EdgePairDistanceMeasurement.cs"
      provides: "ItemsSourceProperty-constrained EdgeSelection dropdown"
      contains: "ItemsSourceProperty"
    - path: "WPF_Example/Custom/Sequence/Inspection/Measurements/PointToLineDistanceMeasurement.cs"
      provides: "Per-point averaged point-to-line distance"
      contains: "collectedEdge"
  key_links:
    - from: "EdgePairDistanceMeasurement.EdgeSelection"
      to: "EdgeOptionLists (new EdgePair selection list)"
      via: "[ItemsSourceProperty(nameof(EdgeSelectionList))] -> EdgeSelectionList getter"
      pattern: "ItemsSourceProperty\\(nameof\\(EdgeSelectionList\\)\\)"
    - from: "PointToLineDistanceMeasurement collected edge points"
      to: "VisionAlgorithmService.DistancePointToLine"
      via: "per-point loop, arithmetic mean of distances"
      pattern: "DistancePointToLine"
---

<objective>
Fix two independently-verified, live-reachable bugs in FAI measurement types (both confirmed present in the currently-inactive D:\Data\Recipe\FAI_2 recipe, but real reachable code paths regardless of loaded recipe). Both are the third batch from the same multi-agent adversarial audit that produced quick-tasks 260722-p5d and 260722-vks.

- Fix F: `EdgePairDistanceMeasurement.EdgeSelection` is a free-text PropertyGrid field. Because `FAIEdgeMeasurementService.TryMeasure` treats any value other than case-insensitive "Both" as the single-edge path (which hardcodes `DistanceMm = 0` and returns success), a mistyped value silently produces a wrong zero result for a measurement whose entire purpose is a non-zero paired-edge distance. Constrain the property to a proper `[ItemsSourceProperty]` dropdown offering only the functionally-correct value ("Both").
- Fix G: `PointToLineDistanceMeasurement.TryExecute` collapses the Point ROI's fitted line into a single midpoint and computes one point-to-line distance. Apply the same per-point averaging pattern already shipped in 3 sibling files (260722-p5d, commits 565aac7 / fe1b72f / c77a165): collect the individual fitted edge points, compute each point's distance to the reference line, and average — reducing single-point noise.

Purpose: Eliminate a silent-wrong-zero result (Fix F) and align PointToLineDistance with the established per-point averaging convention (Fix G).
Output: 3 modified C# files, 2 atomic commits (one per fix), clean Debug/x64 build.
</objective>

<execution_context>
@$HOME/.claude/get-shit-done/workflows/execute-plan.md
@$HOME/.claude/get-shit-done/templates/summary.md
</execution_context>

<context>
@.planning/STATE.md
@./CLAUDE.md

<interfaces>
<!-- Extracted from codebase during planning. Executor should use these directly. -->

FAIEdgeMeasurementService.TryMeasure branching (WPF_Example/Halcon/Algorithms/FAIEdgeMeasurementService.cs:146-166) — DO NOT MODIFY, shown for context only:
  string edgeSel = fai.EdgeSelection;
  if (edgeSel == null) edgeSel = "First";
  bool isBoth = string.Equals(edgeSel, "Both", StringComparison.OrdinalIgnoreCase);
  // isBoth -> TryMeasureBoth (real paired-edge distance)
  // else   -> TryMeasureSingle (DistanceMm = 0, returns true)  <-- silent-wrong path
  // CONCLUSION: "Both" (case-insensitive) is the ONLY value that yields a correct non-zero result.

EdgeOptionLists existing lists (WPF_Example/Custom/Sequence/Inspection/EdgeOptionLists.cs):
  public static readonly List<string> Selections = new List<string> { "First", "Last", "All" };  // lacks "Both", has 2 wrong values -> DO NOT reuse
  // Sibling measurement pattern (e.g. ArcEdgeDistanceMeasurement.cs, DualImageEdgeDistanceMeasurement.cs):
  //   [ItemsSourceProperty(nameof(EdgeSelectionList))] public string EdgeSelection { get; set; } = "All";
  //   [PropertyTools.DataAnnotations.Browsable(false)] public List<string> EdgeSelectionList { get { return EdgeOptionLists.Selections; } }

VisionAlgorithmService.DistancePointToLine (WPF_Example/Halcon/Algorithms/VisionAlgorithmService.cs:585-595) — UNSIGNED perpendicular distance:
  public static double DistancePointToLine(double pRow, double pCol, double lRow1, double lCol1, double lRow2, double lCol2)
  // returns Math.Abs(cross) / len  -> always >= 0 (unsigned). Averaging = mean of unsigned per-point distances.

VisionAlgorithmService.TryFitLine signature (WPF_Example/Halcon/Algorithms/VisionAlgorithmService.cs:20-31):
  public bool TryFitLine(HImage image, double roiRow, double roiCol, double roiPhi, double roiLength1, double roiLength2,
      HTuple datumTransform, int sampleCount, int trimCount, double sigma, int threshold, string direction, string polarity,
      out double row1, out double col1, out double row2, out double col2, out string error,
      string selection = "all",
      List<ValueTuple<double, double>> collectedEdges = null);  // opt-in trailing param: strip-loop raw edge points (post-trim, actually used in fit)

DualImageEdgeDistanceMeasurement.cs per-point averaging (the mirror pattern for Fix G, uses ProjectionPl+sqrt; PointToLine must instead reuse DistancePointToLine): see WPF_Example/Custom/Sequence/Inspection/Measurements/DualImageEdgeDistanceMeasurement.cs:186-271.
</interfaces>

<recipe_findings>
<!-- Investigation-only: D:\Data\Recipe\FAI_2\main.ini (older, currently INACTIVE recipe, last touched 2026-05-11). DO NOT modify any .ini file. -->
- The MeasurementBase EdgePairDistance instance (TypeName=EdgePairDistance, MeasurementName=EdgePairDistance_1) has EdgeSelection=Both — already correctly configured. Fix F is preventive: it stops a future free-text mistype from silently zeroing the result.
- A separate legacy FAIConfig section (EdgeMeasureType=EdgePairDistance, line ~178) has EdgeSelection=First — this uses the old FAIConfig key path, not the MeasurementBase property Fix F touches. Left as-is (no .ini edits).
- A PointToLineDistance instance (MeasurementName=PointToLineDistance_2) exists in FAI_2. NominalValue/MeasuredValue were 0 (incomplete attempt) per the 260722-vks audit.
</recipe_findings>
</context>

<tasks>

<task type="auto" tdd="false">
  <name>Task 1 (Fix F): Constrain EdgePairDistance EdgeSelection to a "Both"-only PropertyGrid dropdown</name>
  <files>WPF_Example/Custom/Sequence/Inspection/EdgeOptionLists.cs, WPF_Example/Custom/Sequence/Inspection/Measurements/EdgePairDistanceMeasurement.cs</files>
  <action>
Add a new dedicated shared option list to EdgeOptionLists.cs alongside the existing `Selections` list, containing ONLY the functionally-correct value: a single-element `new List<string> { "Both" }` (e.g. named `EdgePairSelections`), with an XML/inline comment stating that EdgePairDistance is only correct with "Both" because FAIEdgeMeasurementService.TryMeasure routes any non-"Both" value to the DistanceMm=0 single-edge path. Do NOT reuse `EdgeOptionLists.Selections` (it lacks "Both" and includes First/Last/All which are wrong for this type).

In EdgePairDistanceMeasurement.cs, convert the existing free-text `EdgeSelection` property (~line 27, default "Both") to a constrained dropdown, matching the exact sibling style used in this folder (ArcEdgeDistanceMeasurement / DualImageEdgeDistanceMeasurement): add `[ItemsSourceProperty(nameof(EdgeSelectionList))]` above `EdgeSelection`, and add a backing `[PropertyTools.DataAnnotations.Browsable(false)] public List<string> EdgeSelectionList { get { return EdgeOptionLists.EdgePairSelections; } }` near the other `*List` getters (EdgeDirectionList/EdgePolarityList around lines 35-38). Keep `EdgeSelection` as `string` (INI backward-compat preserved — the default stays "Both").

This is a PropertyGrid input-constraint fix ONLY. Do NOT touch TryExecute's distance-calculation logic, the `temp` FAIConfig construction, or FAIEdgeMeasurementService.TryMeasure. Preserve this file's Allman/existing brace and naming style, C# 7.2 / .NET 4.8 (no C# 8+ features).
  </action>
  <verify>
    <automated>cd /c/code/DataMeasureMent && "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" DatumMeasurement.sln //p:Configuration=Debug //p:Platform=x64 //t:Build //v:m 2>&1 | grep -E "error|Warning\(s\)|Error\(s\)" | tail -20</automated>
    <manual>git diff confirms: (1) EdgeOptionLists.cs gained a new "Both"-only list; (2) EdgePairDistanceMeasurement.cs EdgeSelection now has [ItemsSourceProperty(nameof(EdgeSelectionList))] + backing EdgeSelectionList getter bound to the new list; (3) NO change to TryExecute body or FAIEdgeMeasurementService.cs. Build shows 0 errors and the warning count is unchanged vs the 5-warning baseline (CS0618/CS0162 in Sequence_Top.cs/Sequence_Bottom.cs/SequenceHandler.cs/VirtualCamera.cs).</manual>
  </verify>
  <done>EdgePairDistance's EdgeSelection renders as a constrained ComboBox offering only "Both"; TryExecute and FAIEdgeMeasurementService are unchanged; Debug/x64 builds clean (0 new errors/warnings). Commit this file pair as one atomic commit.</done>
</task>

<task type="auto" tdd="false">
  <name>Task 2 (Fix G): Apply per-point averaging to PointToLineDistance point-to-line distance</name>
  <files>WPF_Example/Custom/Sequence/Inspection/Measurements/PointToLineDistanceMeasurement.cs</files>
  <action>
Mirror the established per-point averaging pattern from DualImageEdgeDistanceMeasurement.cs (lines 186-271), but reuse THIS file's existing distance formula `VisionAlgorithmService.DistancePointToLine` (unsigned) instead of DualImage's ProjectionPl+sqrt. The Point ROI is the point side; the Line ROI stays the reference line.

1. Before the Point ROI `TryFitLine` call (~line 66), declare `List<System.ValueTuple<double, double>> collectedEdgePoints = new List<System.ValueTuple<double, double>>();`. Pass it into that same call via a NAMED argument `collectedEdges: collectedEdgePoints` (do NOT pass a positional `selection` value — keep TryFitLine's default `selection = "all"` unchanged so the fit itself is byte-identical to today). Leave the existing midpoint computation (`pRow`/`pCol` from pr1/pr2, pc1/pc2, ~lines 75-76) in place as the fallback point.

2. Keep the Line ROI `TryFitLine` call (~lines 79-87) completely unchanged (it is the reference line, not the point side).

3. Replace the single `DistancePointToLine` call (~line 89) with: if `collectedEdgePoints` is empty, keep the existing single-midpoint behavior — `distPixel = DistancePointToLine(pRow, pCol, lr1, lc1, lr2, lc2)`. Otherwise loop over `collectedEdgePoints`, compute `DistancePointToLine(ep.Item1, ep.Item2, lr1, lc1, lr2, lc2)` for each point, sum, and take the arithmetic MEAN of the per-point distances (mean of distances — NOT distance of an averaged point). Set `resultValue = meanDistPixel * pixelResolution`. This preserves the existing unsigned sign convention (DistancePointToLine returns Math.Abs(...)).

4. Overlays: this file currently emits NO overlays (the `overlays` list is initialized empty and never populated). Do NOT introduce a resultValue regression. Adding overlays is optional; if any single point/foot marker overlay is added, its DISPLAY position must be the average of the collected points' positions and must NEVER feed back into resultValue's math. The minimal, faithful change keeps overlays empty as today.

Preserve this file's existing brace/naming style, C# 7.2 / .NET 4.8 (no C# 8+ features). Guard against divide-by-zero (only average when count >= 1).
  </action>
  <verify>
    <automated>cd /c/code/DataMeasureMent && "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" DatumMeasurement.sln //p:Configuration=Debug //p:Platform=x64 //t:Build //v:m 2>&1 | grep -E "error|Warning\(s\)|Error\(s\)" | tail -20</automated>
    <manual>Read the diff and confirm the math: resultValue = (sum over points of DistancePointToLine(point, line)) / count * pixelResolution — i.e. MEAN OF PER-POINT DISTANCES, not DistancePointToLine(averagedPoint, line). Confirm sign convention is unchanged (DistancePointToLine is unsigned). Confirm the empty-list fallback reproduces today's exact single-midpoint result (no NaN/crash regression). Confirm the Line ROI fit call and the point-side default selection ("all") are unchanged. Build: 0 errors, warning count unchanged vs 5-warning baseline.</manual>
  </verify>
  <done>PointToLineDistance.resultValue equals the mean of each collected edge point's perpendicular distance to the fitted reference line, with a clean fallback to the prior single-midpoint distance when no points are collected; unsigned convention preserved; Debug/x64 builds clean. Commit as a second atomic commit.</done>
</task>

</tasks>

<threat_model>
## Trust Boundaries

| Boundary | Description |
|----------|-------------|
| operator -> PropertyGrid recipe editor | Recipe authoring is local, single-operator; no untrusted remote input crosses here. |
| recipe .ini file -> ParamBase.Load | Local trusted config file; no new parsing surface introduced by this change. |

## STRIDE Threat Register

| Threat ID | Category | Component | Disposition | Mitigation Plan |
|-----------|----------|-----------|-------------|-----------------|
| T-bsf-01 | Tampering | EdgePairDistance EdgeSelection free-text -> silent zero result | mitigate | Constrain PropertyGrid input to the single functionally-correct value "Both" via [ItemsSourceProperty] (Task 1). |
| T-bsf-02 | Information Disclosure | PointToLineDistance result accuracy (single-point noise) | mitigate | Per-point averaging over collected edge points reduces single-sample noise (Task 2); empty-list fallback prevents NaN. |
| T-bsf-SC | Tampering | npm/pip/cargo installs | accept | No package installs in this task; no dependency changes. |
</threat_model>

<verification>
- MSBuild Debug/x64 (`DatumMeasurement.sln`) completes with 0 errors and 0 new warnings vs the known 5-warning baseline (CS0618/CS0162 in Sequence_Top.cs / Sequence_Bottom.cs / SequenceHandler.cs / VirtualCamera.cs).
- `git diff` self-review per task: Fix F is input-constraint-only (no TryExecute / service behavior change); Fix G's resultValue is the mean of per-point distances (not distance of an averaged point) with a preserved single-midpoint fallback and unchanged unsigned sign convention.
- No .ini recipe file is modified.
- No live-app re-measurement required (FAI_2 is inactive) — code-level + build verification only.
</verification>

<success_criteria>
- EdgePairDistanceMeasurement.EdgeSelection is a constrained ComboBox offering only "Both"; a recipe author can no longer type a value that silently zeros the paired-edge distance.
- EdgePairDistanceMeasurement.TryExecute and FAIEdgeMeasurementService.TryMeasure are byte-unchanged.
- PointToLineDistanceMeasurement.resultValue is the arithmetic mean of per-point perpendicular distances to the fitted reference line, with a no-regression single-midpoint fallback.
- Two atomic commits (one per file/fix). Debug/x64 build is clean.
</success_criteria>

<output>
Create `.planning/quick/260723-bsf-fix-edgepairdistancemeasurement-edgesele/260723-bsf-SUMMARY.md` when done.
</output>
