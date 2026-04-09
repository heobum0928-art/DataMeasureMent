---
phase: 04-datum
reviewed: 2026-04-09T00:00:00Z
depth: standard
files_reviewed: 8
files_reviewed_list:
  - WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs
  - WPF_Example/Halcon/Algorithms/DatumFindingService.cs
  - WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs
  - WPF_Example/Halcon/Algorithms/FAIEdgeMeasurementService.cs
  - WPF_Example/Halcon/Display/HalconDisplayService.cs
  - WPF_Example/UI/ControlItem/InspectionListView.xaml.cs
  - WPF_Example/UI/ControlItem/InspectionListViewModel.cs
  - WPF_Example/UI/ControlItem/Node.cs
findings:
  critical: 0
  warning: 5
  info: 4
  total: 9
status: issues_found
---

# Phase 04: Code Review Report

**Reviewed:** 2026-04-09
**Depth:** standard
**Files Reviewed:** 8
**Status:** issues_found

## Summary

Phase 4 adds Datum coordinate correction: `DatumConfig` data model, `DatumFindingService` algorithm, integration into `Action_FAIMeasurement`, display support in `HalconDisplayService`, and a new `ENodeType.Datum` tree node. The overall architecture is consistent with the existing codebase patterns and the implementation is clean.

No critical (security/crash) issues were found. Five warnings were identified — all are correctness risks that can produce wrong measurement results or silent misbehavior at runtime. Four info items cover dead code, naming, and minor quality gaps.

---

## Warnings

### WR-01: `IntersectionLl` parallelism check uses wrong HTuple value

**File:** `WPF_Example/Halcon/Algorithms/DatumFindingService.cs:75` and `:163`

**Issue:** The Halcon operator `IntersectionLl` sets `isOverlapping` to `1` when lines are **the same** (collinear/overlapping), not when they are parallel. For parallel (non-intersecting) lines, the returned `isOverlapping` is `0` but the intersection coordinates are at infinity. The code checks `if (isOverlapping.I == 1)` to detect the "no intersection" case, which is the inverse of the actual semantic. Parallel lines will not be caught by this guard, and `.D` on an infinity coordinate will produce a `double.PositiveInfinity` origin stored in `RefOriginRow/Col` or fed into the transform — causing all subsequent FAI measurements to use garbage coordinates with no error reported.

**Fix:**
```csharp
// IntersectionLl: isOverlapping==1 means lines are the SAME (collinear).
// For parallel lines, intersection coords are at infinity — detect by range check.
if (isOverlapping.I == 1)
{
    error = "Lines are collinear (identical), no unique intersection";
    return false;
}
if (double.IsInfinity(curRow.D) || double.IsInfinity(curCol.D) ||
    double.IsNaN(curRow.D) || double.IsNaN(curCol.D))
{
    error = "Lines are parallel, intersection is at infinity";
    return false;
}
```

Apply the same fix in both `TryFindDatum` (line 75) and `TryTeachDatum` (line 163).

---

### WR-02: `HTuple.CurrentTransform` disposed while referenced — not thread-safe

**File:** `WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs:50`

**Issue:** `DatumConfig.CurrentTransform` is an `HTuple` public property with no access guard. `ShotConfig.ClearAllResults()` sets it to `null` (which also resets the HTuple), while `Action_FAIMeasurement.Run()` reads it in the Measure step. If `ClearAllResults` is called from a different thread (e.g., a new inspection starts before the previous Measure step reads the transform), the field can be nulled mid-use. Although the current call graph makes this unlikely in practice (the Measure step sets the transform just before reading it), the public `set` accessor combined with `ShotConfig.ClearAllResults` operating on the same object without a lock creates a latent race.

**Fix:** Follow the existing `_imageLock` pattern in `ShotConfig`. Either make the CurrentTransform write/read inside the same lock guard used for image access, or gate `ClearAllResults` calls to happen only when the sequence is idle (which the sequence engine already does for `Init` step). At minimum, add an XML doc comment stating the field is only valid during the Measure step and must not be read outside the sequence thread.

---

### WR-03: Transform rotation extraction uses incorrect `hom_mat2d` element indices

**File:** `WPF_Example/Halcon/Algorithms/FAIEdgeMeasurementService.cs:67`

**Issue:** The code extracts the rotation angle from the Datum `hom_mat2d` transform as:
```csharp
double rotAngle = Math.Atan2(-transform[1].D, transform[0].D);
```
A Halcon `hom_mat2d` is a 9-element row-major 3x3 matrix stored as `[h00, h01, h02, h10, h11, h12, h20, h21, h22]`. The rotation components are `h00 = cos(theta)` at index 0 and `h10 = sin(theta)` at index **3**, not index 1. Index 1 is `h01 = -sin(theta)` (correct sign negation: `Atan2(-h01, h00)` would work), but the comment says `transform[1] = -sin(theta)` while the actual Halcon layout has `h01 = -sin(theta)` only for a pure rotation without translation. For a composed translate-then-rotate matrix (which is what `HomMat2dTranslate` + `HomMat2dRotate` produces), `h01` is not exactly `-sin(theta)` because the rotation center shifts the off-diagonal terms. The approach should instead use `Math.Atan2(transform[3].D, transform[0].D)` (indices for `h10` and `h00` in a standard Halcon hom_mat2d).

In practice the error magnitude is small for small translation offsets, but this will cause a systematic ROI angle bias proportional to the translation component when Datum correction is active.

**Fix:**
```csharp
// hom_mat2d layout: [h00, h01, h02, h10, h11, h12, h20, h21, h22]
// Pure rotation: h00 = cos(theta), h10 = sin(theta) (indices 0 and 3)
double rotAngle = Math.Atan2(transform[3].D, transform[0].D);
roiPhi = fai.ROI_Phi + rotAngle;
```

---

### WR-04: `GetDefaultRunnableAction` loop returns on first iteration unconditionally

**File:** `WPF_Example/UI/ControlItem/InspectionListView.xaml.cs:164`

**Issue:** The method iterates with `for (var i = 0; i < sequence.ActionCount; i++)` but always `return sequence[i].ID` on the first pass (`i == 0`), making the loop body execute exactly once regardless of `ActionCount`. The code immediately below the loop returns `EAction.Unknown`, making that line dead code. The intent is clearly to return the first action's ID, which works correctly — but if the intent was to find the first *runnable* action (e.g., skipping disabled actions), the loop is broken. If the intent is just "get index 0", the loop is misleading.

**Fix:** Replace the loop with a direct index access, or implement the intended filter logic:
```csharp
private static EAction GetDefaultRunnableAction(SequenceBase sequence)
{
    if (sequence == null || sequence.ActionCount == 0)
    {
        return EAction.Unknown;
    }
    return sequence[0].ID;
}
```

---

### WR-05: `DatumConfig.LastFindSucceeded` set to `true` before transform is stored, not reset on failure path

**File:** `WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs:96`

**Issue:** In the Datum failure branch (lines 88–94), the code calls `fai.ClearResult()` and sets `pMyContext.AllPass = false`, then breaks out. However, `ShotParam.Datum.LastFindSucceeded` is not explicitly set to `false` in the failure branch — it retains whatever value it had from the previous inspection cycle. On line 96, `LastFindSucceeded = true` is set immediately after `TryFindDatum` succeeds, which is correct, but if a subsequent inspection fails the Datum find, the stale `true` value remains on the `DatumConfig` object. Any UI or diagnostic code reading `LastFindSucceeded` between inspections will see an incorrect "last succeeded" status.

**Fix:** Add an explicit reset before the early break:
```csharp
if (!datumService.TryFindDatum(image, ShotParam.Datum, out datumTransform, out datumError))
{
    Logging.PrintLog((int)ELogType.Error, "Datum find failed: " + datumError);
    ShotParam.Datum.LastFindSucceeded = false; // explicitly reset
    foreach (var fai in ShotParam.FAIList) { fai.ClearResult(); }
    pMyContext.AllPass = false;
    pMyContext.MeasuredCount = ShotParam.FAIList.Count;
    Step = (int)EStep.End;
    break;
}
```

---

## Info

### IN-01: `DatumConfig.CurrentTransform` is not serializable but carries no disposal concern documented

**File:** `WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs:49-50`

**Issue:** The XML doc comment on the class says `HTuple` fields are "런타임 전용" (runtime only) and not part of INI serialization, which is correct. However, `HTuple` is an unmanaged Halcon wrapper. When `DatumConfig` is replaced (e.g., recipe reload) the `CurrentTransform` HTuple may hold an unmanaged handle. `ParamBase` does not implement `IDisposable`, so there is no disposal path for this field. In the current flow `ShotConfig.ClearAllResults` sets it to `null` (which triggers the HTuple's finalizer), so GC will eventually free it — but explicit disposal would be safer and consistent with the `HImage` disposal pattern used elsewhere.

**Suggestion:** Set `CurrentTransform = new HTuple()` (empty) rather than `null` in `ClearAllResults`, and consider adding a `Dispose()` call pattern for `DatumConfig` if recipe hot-swap is added in a future phase.

---

### IN-02: `HalconDisplayService.RenderDatumOverlay` mixes `HOperatorSet.*` and `window.*` API styles

**File:** `WPF_Example/Halcon/Display/HalconDisplayService.cs:314-366`

**Issue:** The existing `Render` method uses the `window.SetColor()` / `window.DispLine()` instance-style API throughout. `RenderDatumOverlay` (added in Phase 4) uses the static `HOperatorSet.SetColor(window, ...)` / `HOperatorSet.DispLine(window, ...)` form. Both are functionally equivalent in Halcon, but mixing styles within the same service class reduces readability and is inconsistent with the file's convention.

**Suggestion:** Rewrite `RenderDatumOverlay` to use the instance-style API (`window.SetColor(...)`, `window.DispLine(...)`) to match the rest of the file.

---

### IN-03: `Node.ImageSource` has a `set` accessor with no backing field or notification

**File:** `WPF_Example/UI/ControlItem/Node.cs:32`

**Issue:** The `ImageSource` property has a `set { }` body that discards the assigned value — the setter is a no-op. The `NodeViewModel` wraps this and calls `RaisePropertyChanged("ImageSource")` in its own setter, but since the underlying `Node.ImageSource` ignores the write, any assigned value is lost. The `get` uses a `switch` on `NodeType` so it is read-only by nature; the setter is dead code that could mislead future developers.

**Suggestion:** Remove the `set` accessor from `Node.ImageSource` and mark the property with the comment `// computed, read-only` to make the intent explicit:
```csharp
public string ImageSource
{
    get { /* switch ... */ }
    // No setter: value is derived from NodeType
}
```

---

### IN-04: `InspectionListViewModel.Select` has an unchecked cast and is not called anywhere

**File:** `WPF_Example/UI/ControlItem/InspectionListViewModel.cs:124`

**Issue:** `Select(int count)` casts `RootModel.Children` to `IList<NodeViewModel>` without checking. `ObservableCollection<T>` implements `IList<T>` so the cast will succeed at runtime, but it also accesses `children[i]` without bounds-checking `count` against `Children.Count`. Additionally, a search of the codebase reveals no callers of this method — it appears to be dead code left from an earlier iteration.

**Suggestion:** If `Select` is not needed, remove it. If it is needed in a future phase, add a bounds check:
```csharp
public void Select(int count)
{
    var children = this.RootModel.Children;
    int limit = Math.Min(count, children.Count);
    for (int i = 0; i < limit; i++)
    {
        children[i].IsSelected = true;
    }
}
```

---

_Reviewed: 2026-04-09_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
