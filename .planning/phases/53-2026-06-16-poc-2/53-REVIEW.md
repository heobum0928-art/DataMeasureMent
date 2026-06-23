---
phase: 53-2026-06-16-poc-2
reviewed: 2026-06-23T00:00:00Z
depth: standard
files_reviewed: 5
files_reviewed_list:
  - WPF_Example/Halcon/Algorithms/CheckerboardCalibrationService.cs
  - WPF_Example/UI/Dialog/CalibrationWindow.xaml.cs
  - WPF_Example/UI/ContentItem/MainView.xaml.cs
  - WPF_Example/UI/Dialog/CalibrationWindow.xaml
  - WPF_Example/UI/ContentItem/MainView.xaml
findings:
  critical: 0
  warning: 2
  info: 4
  total: 6
status: issues_found
---

# Phase 53: Code Review Report

**Reviewed:** 2026-06-23
**Depth:** standard
**Files Reviewed:** 5
**Status:** issues_found

## Summary

Reviewed the Phase 53 checkerboard pixel calibration feature: the `CheckerboardCalibrationService` HALCON algorithm (saddle_points_sub_pix → median pitch → mm/px + center/outer distortion %), the `CalibrationWindow` WPF dialog, and the MainView wiring that batch-applies mm/px to all active-sequence shots and persists via `SaveRecipe()`.

Overall the code is clean and follows project conventions well:
- HALCON calls are wrapped in `try { } catch { return false; }` per convention.
- Robust statistics (median) used to reject missing-corner 2×pitch jumps (good).
- The batch-apply path correctly gates mutation behind a `CustomMessageBox.ShowConfirmation` modal (D-06) **before** any `shot.PixelResolution` write, and reuses `MainWindow.SaveRecipe()` which carries the existing-file preservation guard (3faa91b) so inactive-sequence Datums are not lost. This is correct.
- The `ApplyRequested` event handler is unsubscribed in a `finally` after `ShowDialog()` — no handler leak.
- Input validation (cell mm > 0, null image, corner count floor) is present.
- The service reads `CalibrationViewer.CurrentImage` without disposing it — correct, because `HalconViewerControl` owns and disposes that HImage internally.

Two memory-leak issues stand out (both HALCON native image handles), plus several minor polish items.

## Warnings

### WR-01: CalibrationWindow never disposes its HalconViewerControl (HImage handle leak)

**File:** `WPF_Example/UI/Dialog/CalibrationWindow.xaml.cs` (whole class) / `WPF_Example/UI/Dialog/CalibrationWindow.xaml:122`
**Issue:** `CalibrationViewer` is a `HalconViewerControl`, which is `IDisposable` and holds a live `HImage` in `CurrentImage` (allocated in `LoadImage`). The window has no `Closed`/`OnClosed` handler and never calls `CalibrationViewer.Dispose()`. Every time the calibration window is opened and an image is loaded/grabbed, the native HALCON image handle is leaked when the window closes. The cited reference pattern does dispose its viewer — `TeachingWindow.xaml.cs:313` calls `TeachingViewer.Dispose();` — so this is an omission relative to the established convention.
**Fix:** Add a `Closed` handler that disposes the viewer:
```csharp
public CalibrationWindow()
{
    InitializeComponent();
    // ...existing init...
    Closed += CalibrationWindow_Closed;
}

private void CalibrationWindow_Closed(object sender, EventArgs e)
{
    //260623 hbk Phase 53: 뷰어 HImage 핸들 해제 (TeachingWindow.Dispose 패턴)
    CalibrationViewer.Dispose();
}
```

### WR-02: GrabCalibrationImage leaks the grabbed HImage on every live capture

**File:** `WPF_Example/UI/ContentItem/MainView.xaml.cs:2433`
**Issue:** `HImage grabbed = pDev.GrabHalconImage(camShot);` allocates a fresh HALCON image. It is passed to `HalconTeachingHelper.SaveTempImage(...)`, which (per `TeachingStorageService.SaveTempImage`) only *reads* the image to write a PNG and does **not** take ownership or dispose it. `grabbed` is never disposed on any return path, so each live capture leaks one native HImage. The analogous inspection paths in `Action_TopInspection.cs` always dispose the grabbed image (via `PutImage` ownership transfer or a `finally { image.Dispose(); }`); this path does neither.
**Fix:** Dispose `grabbed` after saving:
```csharp
HImage grabbed = pDev.GrabHalconImage(camShot);
if (grabbed == null) return null;
try
{
    return HalconTeachingHelper.SaveTempImage("Calibration_" + activeSeq, grabbed);
}
finally
{
    grabbed.Dispose();   //260623 hbk Phase 53: grab 원본 해제 (SaveTempImage 는 borrow)
}
```
(The outer `catch { return null; }` already prevents throw; the `finally` runs before it.)

## Info

### IN-01: `tol` computed but the row/col dedup comment references a different threshold

**File:** `WPF_Example/Halcon/Algorithms/CheckerboardCalibrationService.cs:161,191`
**Issue:** `double tol = pitchGuess * 0.5;` is used to skip near-zero gaps (`if (gap <= tol) continue;`), which is correct for rejecting duplicate corners. However the XML doc above `CollectRowAdjacentColGaps` (lines 155-156) talks about a "medGap 의 1.5배 초과" cutoff that does not exist in the code — the upper (2×pitch) jump is only handled implicitly by the median. The comment is slightly misleading versus the implementation.
**Fix:** Reword the doc-comment to describe only what the code does (lower-bound `tol` dedup + median naturally rejecting 2×pitch outliers), removing the "1.5배" reference.

### IN-02: `centerMean`/`outerMean` out-params computed but never consumed by callers

**File:** `WPF_Example/Halcon/Algorithms/CheckerboardCalibrationService.cs:104,274`
**Issue:** `ComputeCenterOuterDeviationPct` returns the deviation % *and* also exposes `centerMean`/`outerMean` via `out`. The only caller (`TryCalibrate`) discards both — they are written into locals `centerMean`/`outerMean` (line 103) that are never read. Minor dead output.
**Fix:** Either drop the two `out` parameters (simplifies signature) or surface them on `CalibrationResult` if useful for the report. Not load-bearing.

### IN-03: `ApplyCalibrationResult` (pre-existing 2-point path) silently no-ops when no FAI row is selected

**File:** `WPF_Example/UI/ContentItem/MainView.xaml.cs:2298-2319`
**Issue:** Not introduced by this phase, but adjacent to it: when `anchorFai == null` the method returns without applying or informing the user. The new checkerboard path (`ApplyCheckerboardCalibration`) handles the no-selection case gracefully via `ResolveActiveSequenceForCalibration` falling back to `SEQ_TOP`, so the two calibration entry points now differ in behavior. Worth a glance to confirm the divergence is intentional (it appears to be, per D-03 "active sequence whole-shot" vs the old "single selected shot" semantics).
**Fix:** No change required if intentional; consider a status hint in the old path for consistency.

### IN-04: Magic radial-band fractions (0.33 / 0.66) are inline literals

**File:** `WPF_Example/Halcon/Algorithms/CheckerboardCalibrationService.cs:297-298`
**Issue:** `innerR = diag * 0.33;` and `outerR = diag * 0.66;` define the center/outer distortion bands as bare literals. The other tunables (`DefaultSaddleSigma`, `DistortionWarnThresholdPct`, `MinCornerCount`) are named `const`s deliberately exposed for UAT tuning; these two bands are not.
**Fix:** Promote to named consts (e.g., `private const double CenterBandFraction = 0.33; private const double OuterBandFraction = 0.66;`) for consistency and future tuning. Low priority.

---

_Reviewed: 2026-06-23_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
