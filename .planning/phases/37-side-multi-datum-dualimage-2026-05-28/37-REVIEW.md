---
phase: 37-side-multi-datum-dualimage-2026-05-28
reviewed: 2026-05-28T08:56:55Z
depth: standard
files_reviewed: 3
files_reviewed_list:
  - WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs
  - WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs
  - WPF_Example/UI/ContentItem/MainView.xaml.cs
findings:
  critical: 0
  warning: 2
  info: 3
  total: 5
status: issues_found
---

# Phase 37: Code Review Report

**Reviewed:** 2026-05-28T08:56:55Z
**Depth:** standard
**Files Reviewed:** 3
**Status:** issues_found

## Summary

Phase 37 converts the Datum phase from a single-shot, all-or-nothing flow into a
per-datum loop with lenient skip semantics, and replaces name-based FAI lookups
in MainView with object-reference resolution. The implementation is solid on the
points flagged for focus:

- **Per-datum loop correctness:** The `EStep.DatumPhase` loop in
  `Action_FAIMeasurement.cs` correctly branches per-datum on `AlgorithmTypeEnum`,
  calls `ClearDatumTransforms()` once before the loop, and accumulates via
  `TryRunSingleDatum`. Mixed-algorithm fixtures (some DualImage, some 1-image)
  are handled.
- **Lenient skip (no silent total failure):** Every skip path logs to
  `ELogType.Error` with the datum name before `continue`, so a partial failure is
  observable. Empty `DatumConfigs` still falls through to pass-through (D-10).
- **HImage disposal:** Both the DualImage branch (`try/finally` disposing `imgH`
  and `imgV`) and the 1-image branch (`try/finally` disposing `img`) dispose
  correctly, with bare `catch` on dispose per project convention. The measurement
  image is correctly separated via `ShotParam.GetImage()` inside a `using` in
  `EStep.Measure`.
- **Null-safety in `FindFAIContainingMeasurement`:** Guards `measurement`,
  `SystemHandler.Handle`, `.Sequences`, `recipeManager`, `recipeManager.Shots`,
  `rmShot`, and `pSeq`. The RecipeManager-first / pSeq-fallback strategy correctly
  addresses the multi-Shot duplicate-name and new-Shot-not-reflected bugs.
- **Legacy single-datum regression:** The 1-image path preserves the
  TeachingImagePath -> SimulImagePath -> grab fallback chain, so existing
  single-datum recipes behave as before.

Two warnings concern the accumulation contract (silent overwrite on duplicate /
empty datum names) and a residual inconsistency in the now-unused overload.
Three info items cover dead code and minor robustness.

## Warnings

### WR-01: Empty or duplicate `DatumName` silently collides in `_datumTransforms`

**File:** `WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs:214`
**Issue:** `TryRunSingleDatum` stores the transform with key `datum.DatumName ?? ""`.
If two datums in `DatumConfigs` have the same name (or both have null/empty
names, both mapping to the `""` key), the second silently overwrites the first
in `_datumTransforms`. With the new per-datum loop that runs *all* datums, this
is now reachable in normal multi-datum operation (Phase 37's whole purpose),
whereas before only `DatumConfigs[0]` was used. Downstream, `EStep.Measure`
resolves `meas.DatumRef` against these keys via `TryGetDatumTransform`, so a
measurement referencing the shadowed datum silently picks up the wrong transform.
`AddDatum` auto-generates unique default names, but user edits / INI load do not
enforce uniqueness, and empty names are not rejected.
**Fix:** Detect collisions and log instead of silently overwriting. For example,
before storing:
```csharp
string key = datum.DatumName ?? "";
if (_datumTransforms.ContainsKey(key)) {
    Logging.PrintLog((int)ELogType.Error,
        "[Datum] DatumName 중복/빈값 '" + key + "' — 이전 transform 덮어씀 (DatumRef 충돌 위험)");
}
_datumTransforms[key] = transform;
```
Consider also rejecting empty `DatumName` at the `AddDatum`/teach boundary.

### WR-02: `TryRunDatumPhase(image1, image2)` overload uses `datum.AlgorithmTypeEnum` without a null guard

**File:** `WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs:182-183`
**Issue:** The 2-image overload iterates `foreach (var datum in DatumConfigs)` and
immediately dereferences `datum.AlgorithmTypeEnum` with no `if (datum == null) continue;`
guard. The 1-image overload (`TryRunDatumPhase`, line 160) and the new
`TryRunSingleDatum` (line 199) and the action loop (line 85) all guard against
null datums; this overload does not, so a null entry in `DatumConfigs` throws
`NullReferenceException` here — which, given the Phase 37 lenient intent, would
abort the whole phase rather than skip one datum.
**Fix:** Add the same guard used elsewhere:
```csharp
foreach (var datum in DatumConfigs) {
    if (datum == null) continue; //260528 hbk Phase 37
    HTuple transform; string datumError; bool ok;
    ...
}
```
Note: this overload is currently not called from production code (see IN-01), so
impact is latent — but it should be fixed or removed for consistency.

## Info

### IN-01: Both `TryRunDatumPhase` overloads are now dead code in the production tree

**File:** `WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs:146` and `:175`
**Issue:** After Phase 37, `Action_FAIMeasurement.EStep.DatumPhase` drives the
datum loop through `ClearDatumTransforms()` + `TryRunSingleDatum(...)`. A
repo-wide search shows neither `TryRunDatumPhase(HImage, out string)` nor
`TryRunDatumPhase(HImage, HImage, out string)` is called anywhere under
`WPF_Example/` (only definitions remain; the `.claude/worktrees/` hits are agent
scratch copies, not compiled). They are now dead public API.
**Fix:** Either remove both overloads, or add a `//260528 hbk Phase 37` comment
documenting that they are retained intentionally (e.g. external/legacy callers).
Removing reduces the maintenance surface and eliminates the WR-02 latent bug.

### IN-02: `TryGetDatumTransform` returns success for unstored (skipped) datum keys only by miss, but identity fallback lives in the caller

**File:** `WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs:219-225` (and caller `Action_FAIMeasurement.cs:166-173`)
**Issue:** When a datum is skipped (find failed), its key is absent from
`_datumTransforms`, so `TryGetDatumTransform` returns `false`, and the
`EStep.Measure` caller falls back to `HomMat2dIdentity`. This is the intended
lenient behavior, and it is correct — but the contract is implicit: a measurement
whose `DatumRef` points at a *failed* datum is silently measured in the
uncorrected (identity) frame rather than being marked indeterminate. This is a
behavioral note, not a defect, but worth a one-line comment at the fallback site
so a future reader does not mistake "datum failed" for "no datum configured."
**Fix:** Add a clarifying comment at `Action_FAIMeasurement.cs:166-173` noting
that an absent transform may mean either "DatumRef unset" or "referenced datum
find failed (skipped)," both of which fall back to identity.

### IN-03: `FindFAIContainingMeasurement` duplicates the pSeq traversal already in `FindFaiNameContainingMeasurement`

**File:** `WPF_Example/UI/ContentItem/MainView.xaml.cs:1732-1766`
**Issue:** The new `FindFAIContainingMeasurement` adds a RecipeManager-first pass
(the actual fix) followed by a pSeq fallback loop that is structurally identical
to the existing `FindFaiNameContainingMeasurement` (lines 1710-1728). The two
name-based callers (`HighlightSelectedRoi` at 381, edit paths at 1513/1626) still
use the older name-returning method, so the object-reference fix is only partially
adopted and the traversal logic is now duplicated in three places.
**Fix:** Consider refactoring `FindFaiNameContainingMeasurement` to delegate:
`var fai = FindFAIContainingMeasurement(m); return fai?.FAIName;` (expand the `?.`
to an explicit null-check per C# 7.2 / file convention). This removes the
duplicated loop and ensures name lookups inherit the same RecipeManager-first
correctness. Low priority — current behavior is correct.

---

_Reviewed: 2026-05-28T08:56:55Z_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
