---
phase: 52-datum-2026-06-16-poc-1
reviewed: 2026-06-17T00:00:00Z
depth: standard
files_reviewed: 6
files_reviewed_list:
  - WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs
  - WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs
  - WPF_Example/Custom/Sequence/Inspection/InspectionRecipeManager.cs
  - WPF_Example/Halcon/Algorithms/DatumFindingService.cs
  - WPF_Example/Halcon/Algorithms/VisionAlgorithmService.cs
  - WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs
findings:
  critical: 0
  warning: 2
  info: 3
  total: 5
status: issues_found
---

# Phase 52: Code Review Report

**Reviewed:** 2026-06-17
**Depth:** standard
**Files Reviewed:** 6
**Status:** issues_found

## Summary

Phase 52 adds Datum-edge image leveling (LEVEL-01): a per-sequence leveling angle is computed once
(D-03) from the leveling-reference Datum's horizontal 2-ROI fit line, and the same `-angle` rotation
about the image center is applied to both the Datum-detection input images (D-02) and the SHOT
measurement image. The diff is small and additive (+231 lines, all insertions), well-commented, and
follows existing project conventions (`//260617 hbk` markers, `try/catch` HALCON wrapping, lenient
no-abort fallback, INI backward-compat with `ToBool()` off fallback).

The two correctness invariants called out for this review hold:

- **Sign/center consistency** — Datum-detection rotation (`Action_FAIMeasurement.cs:116`,
  `datumLevelAngle = -parentSeq.LevelingAngleRad`) and Grab/measurement rotation
  (`Action_FAIMeasurement.cs:222`, `-rotSeq.LevelingAngleRad`) use the **same angle and same sign**,
  and both go through `RotateImageByAngle`, which always rotates about the **image center**
  (`(height-1)/2`, `(width-1)/2`). The DualImage path applies the identical `-angle` to both images
  (`:136`, `:138`). No mismatch found.
- **INI backward-compat** — `LevelingEnabled` defaults to `false` (`InspectionSequence.cs:49`), is
  loaded via `ToBool()` which falls back to `false` on a missing key
  (`InspectionRecipeManager.cs:138`), and the save path is in the same FIXTURE section as
  `DisplayName`. Existing recipes regress to off (no leveling) as required.

No critical issues. Two warnings concern an HImage leak risk on the rotation-swap paths and a
potential reference/measurement image mismatch in SIMUL mode. Three info items cover minor
robustness and convention points.

## Warnings

### WR-01: HImage leak if `Dispose()` throws during rotation swap (img not reassigned, original orphaned)

**File:** `WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs:137-139`, `:169`, `:224-225`
**Issue:** The rotation-swap pattern disposes the original then reassigns:
```csharp
HImage hRot = VisionAlgorithmService.RotateImageByAngle(imgH, datumLevelAngle);
if (hRot != null) { imgH.Dispose(); imgH = hRot; }
```
`HImage.Dispose()` can throw (e.g., already-disposed/native error). The project convention everywhere
else wraps short-lived HImage disposal in `try { ... } catch { }` (see the `finally` blocks at
`:152-153`, `:182-184`, and `GrabOrLoadDualDatumImages` at `:506-507`). Here the bare `imgH.Dispose()`
is unguarded. If it throws, `imgH = hRot` never executes, the rotated buffer (`hRot`) is leaked, and
the exception escapes the per-datum `try` to the action loop. The Grab path (`:224`) has the same shape
but is inside a broader region; still, `image.Dispose()` is unguarded while every other disposal in this
file is guarded.
**Fix:** Guard the swap disposals to match the file's established pattern, and dispose the new buffer
if the swap cannot complete:
```csharp
HImage hRot = VisionAlgorithmService.RotateImageByAngle(imgH, datumLevelAngle);
if (hRot != null) { try { imgH.Dispose(); } catch { } imgH = hRot; }
// repeat for imgV (:138-139), the 1-image img (:169), and the Grab image (:224-225)
```

### WR-02: Leveling angle computed from a different image than the one measured (SIMUL role split)

**File:** `WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs:84-101` (Level step) vs `:204-227` (Grab)
**Issue:** In SIMUL mode the Level step sources the reference image via `GrabOrLoadDatumImage(refDatum)`,
which prefers `datum.TeachingImagePath` and only falls back to `ShotParam.SimulImagePath` (`:466-472`).
The SHOT measurement image in the Grab step is loaded strictly from `ShotParam.SimulImagePath`
(`:204-206`). If the leveling-reference Datum has a dedicated `TeachingImagePath` set (the normal
DualImage/dedicated-source setup), the leveling angle characterizes the *teaching* image's tilt, but the
correction `-angle` is then applied to a *different* measurement image. The two tilts need not match, so
the measurement image can be rotated by an angle that does not correspond to its own skew — a silent
correctness error that only manifests with real data (out of v1 static scope, but flagged per the
sign/center-consistency directive). In non-SIMUL mode both paths use
`GrabHalconImage(ShotParam)` from the same live camera, so production is unaffected.
**Fix:** For the leveling angle, prefer the same source the Grab step will measure
(`ShotParam.SimulImagePath` in SIMUL), or document/assert that the leveling-reference Datum's image
source must equal the SHOT measurement source. At minimum, log which image path was used for angle
computation so a teach/measure mismatch is diagnosable.

## Info

### IN-01: `RotateImageByAngle` catch fallback can still throw out of the method

**File:** `WPF_Example/Halcon/Algorithms/VisionAlgorithmService.cs:1177-1181`
**Issue:** The catch block returns `src.CopyImage()`. If `CopyImage()` itself throws (disposed/native
error), the exception escapes despite the method's "throw 금지" comment, and the unguarded caller
disposals (WR-01) would then orphan buffers. Low likelihood, but the stated contract is "never throw".
**Fix:** Wrap the fallback or return `null` on double-failure and let callers keep the original:
```csharp
catch
{
    try { return src.CopyImage(); } catch { return null; }
}
```
Note callers already handle `null` by skipping the swap (`if (rotated != null)`), so returning `null`
is the safer no-op contract.

### IN-02: Redundant null re-check of `parentSeq` after it is already dereferenced

**File:** `WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs:115`
**Issue:** `bool datumLevelOn = (parentSeq != null && parentSeq.LevelingEnabled && parentSeq.LevelingComputed);`
sits inside `if (parentSeq != null && parentSeq.DatumConfigs.Count > 0)` (`:112`), where `parentSeq` is
already known non-null and was dereferenced on `:113` (`parentSeq.ClearDatumTransforms()`). The leading
`parentSeq != null` is dead.
**Fix:** Drop the redundant check: `bool datumLevelOn = parentSeq.LevelingEnabled && parentSeq.LevelingComputed;`

### IN-03: `TryComputeLevelingAngle` return value discarded; failure path is silent at call site

**File:** `WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs:96`
**Issue:** `lvlSeq.TryComputeLevelingAngle(refImage, out angleRad);` ignores the `bool` result and the
`out angleRad` value. This is intentional (the method caches internally and logs its own failures, and
the design is lenient/no-abort), but discarding a `Try*` result is unusual for the codebase convention
and makes the call read as if the angle is used locally when it is not. The `angleRad` local is written
and never read.
**Fix:** Either remove the unused local by ignoring the out value is not possible in C# 7.2 without a
discard variable (`out _` is allowed in C# 7.0+, so it compiles), or add a brief comment at the call
site that the result is intentionally ignored because caching + logging happen inside. Optionally:
`if (!lvlSeq.TryComputeLevelingAngle(refImage, out _)) { /* already logged, lenient */ }`

---

_Reviewed: 2026-06-17_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
