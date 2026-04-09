---
phase: quick-260409-e3v
plan: 01
subsystem: halcon-edge-measurement
tags: [halcon, edge-measurement, fai, line-fitting, sample-strip]
dependency_graph:
  requires: [MeasurementAlgorithm, RoiDefinition]
  provides: [FAIEdgeMeasurementService-v2, FAIConfig-edge-params]
  affects: [Action_FAIMeasurement]
tech_stack:
  added: []
  patterns: [sample-strip-edge-detection, line-fitting, perpendicular-distance]
key_files:
  created: []
  modified:
    - WPF_Example/Custom/Sequence/Inspection/FAIConfig.cs
    - WPF_Example/Halcon/Algorithms/FAIEdgeMeasurementService.cs
    - WPF_Example/Halcon/Models/FAIEdgeMeasurementResult.cs
decisions:
  - "EdgeDirection/EdgeSelection/EdgePolarity stored as string (matching RoiDefinition pattern, validated at use site)"
  - "TrimExtremePoints copied as private static method (avoid coupling to MeasurementAlgorithm)"
  - "Both mode uses perpendicular distance between line midpoints projected onto line1 normal"
metrics:
  duration: 294s
  completed: "2026-04-09T01:18:17Z"
  tasks: 2
  files: 3
---

# Quick Task 260409-e3v: FAI Edge Measurement Rewrite Summary

Sample-strip line-fitting approach ported from MeasurementAlgorithm to FAIEdgeMeasurementService, replacing single-rectangle point-picking with multi-strip edge collection, extreme-point trimming, and FitLineContourXld for reliable edge line detection.

## Tasks Completed

| Task | Name | Commit | Files |
|------|------|--------|-------|
| 1 | Update FAIConfig edge parameters and FAIEdgeMeasurementResult model | 9599bbf | FAIConfig.cs, FAIEdgeMeasurementResult.cs |
| 2 | Rewrite FAIEdgeMeasurementService with sample-strip line-fitting | a65585f | FAIEdgeMeasurementService.cs |

## Changes Made

### Task 1: FAIConfig + FAIEdgeMeasurementResult

**FAIConfig.cs:**
- Deleted `EEdgeMeasureType` enum (FirstToFirst/FirstToLast/LastToFirst/LastToLast)
- Deleted `MeasureType` property
- Replaced `Threshold` (double, default 30) with `EdgeThreshold` (int, default 10) matching RoiDefinition
- Added: `EdgeDirection` (string, "LtoR"), `EdgeSelection` ("First"), `EdgeSampleCount` (20), `EdgeTrimCount` (10), `EdgePolarity` ("DarkToLight")
- Updated `ToRoiDefinition()` to pass all new edge parameters through

**FAIEdgeMeasurementResult.cs:**
- Added `Line1Row1/Column1/Row2/Column2` for first fitted line endpoints
- Added `Line2Row1/Column1/Row2/Column2` for second fitted line endpoints (Both mode)
- Added `EdgePointCount` for total detected points before trim
- Existing Edge1/Edge2 fields now hold fitted line midpoints

### Task 2: FAIEdgeMeasurementService Rewrite

Complete rewrite following MeasurementAlgorithm.TryInspectSingleEdgeInternal pattern:

**EdgeSelection=Both:**
1. Split ROI into N sample strips (EdgeSampleCount)
2. Per strip: GenMeasureRectangle2 + MeasurePos with select="all"
3. First edge point -> firstEdgePoints, last edge point -> lastEdgePoints
4. TrimExtremePoints on both collections
5. FitLineContourXld on each -> line1, line2
6. Perpendicular distance between fitted lines (normal projection)
7. mm conversion using PixelResolutionX (horizontal scan) or PixelResolutionY (vertical scan)

**EdgeSelection=First/Last:**
1. Same sample strip approach but select="first"/"last"
2. Single line fitting, distance=0

**Key safety features:**
- CloseMeasure in per-strip finally blocks
- Input validation with case-insensitive string comparison and defaults for unrecognized values (T-q3-01 mitigated)
- Math.Max guards on sampleCount/sigma/threshold (T-q3-02 mitigated)
- HObject disposal in finally blocks

## Deviations from Plan

None - plan executed exactly as written.

## Verification

- EEdgeMeasureType enum removed: PASS
- EdgeDirection property exists: PASS
- FitLineContourXld in service: PASS
- SelectEdgeIndices removed: PASS
- TryMeasure signature unchanged in Action_FAIMeasurement: PASS
- MSBuild Debug/x64 compilation: PASS (0 errors, only pre-existing warnings)
