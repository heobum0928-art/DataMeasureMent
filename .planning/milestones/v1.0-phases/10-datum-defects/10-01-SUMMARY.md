---
phase: 10-datum-defects
plan: 01
subsystem: Halcon/Algorithms
tags: [datum, fai, numerical-correctness, wr-01, wr-03]
requires: [Phase 4 DatumFindingService, Phase 4 FAIEdgeMeasurementService, Phase 6 InspectionSequence.TryRunDatumPhase]
provides:
  - "Parallel-line guard (IsInfinity/IsNaN) at 3 intersection call sites"
  - "Correct hom_mat2d rotation extraction (indices 3,0) in FAI ROI transform"
affects: [ALG-05]
tech-stack:
  added: []
  patterns: ["inline guard after Halcon IntersectionLl", "translation-invariant hom_mat2d index extraction"]
key-files:
  created: []
  modified:
    - WPF_Example/Halcon/Algorithms/DatumFindingService.cs
    - WPF_Example/Halcon/Algorithms/VisionAlgorithmService.cs
    - WPF_Example/Halcon/Algorithms/FAIEdgeMeasurementService.cs
decisions:
  - "D-01/D-04: distinct error messages for collinear vs parallel (TryFindDatum, TryTeachDatum)"
  - "D-02/D-03: inline guard at each of 3 sites; no common helper extraction (scope-guard)"
  - "D-04: IntersectLines signature preserved (bool + out); parallel returns false with no error string"
  - "D-05/D-06: rotation extraction uses transform[3]=h10=sin θ and transform[0]=h00=cos θ (translation-invariant indices)"
  - "D-12: every modified/inserted line carries //260423 hbk tag"
metrics:
  duration: "~10 min"
  completed: "2026-04-23"
  tasks: 3
  commits: 3
---

# Phase 10 Plan 01: WR-01/WR-03 Code Fixes Summary

WR-01 parallel-line guard applied at 3 intersection sites and WR-03 hom_mat2d rotation-index bug fixed at 1 site — all via inline edits preserving existing signatures and try/catch scaffolding.

## Files Modified

| File | Lines (approx) | Change |
| --- | --- | --- |
| `WPF_Example/Halcon/Algorithms/DatumFindingService.cs` | 75-87 (TryFindDatum), 175-187 (TryTeachDatum) | +20 / -4 — WR-01 guard (collinear + parallel branches) at 2 sites |
| `WPF_Example/Halcon/Algorithms/VisionAlgorithmService.cs` | 274-280 | +6 / -1 — WR-01 guard in static `IntersectLines` helper |
| `WPF_Example/Halcon/Algorithms/FAIEdgeMeasurementService.cs` | 66-70 | +4 / -2 — WR-03 rotation index fix + hom_mat2d layout comment |

## Before / After Snippets

### WR-01 Site A — `DatumFindingService.TryFindDatum`

Before:
```csharp
if (isOverlapping.I == 1)
{
    error = "Lines are parallel, no intersection";
    return false;
}
```

After:
```csharp
// IntersectionLl: isOverlapping==1 means lines are the SAME (collinear). //260423 hbk WR-01
// For parallel lines, isOverlapping==0 but intersection coords are at ±Infinity. //260423 hbk WR-01
if (isOverlapping.I == 1) //260423 hbk WR-01
{
    error = "Lines are collinear (identical), no unique intersection"; //260423 hbk WR-01
    return false;
}
if (double.IsInfinity(curRow.D) || double.IsInfinity(curCol.D) || //260423 hbk WR-01
    double.IsNaN(curRow.D) || double.IsNaN(curCol.D)) //260423 hbk WR-01
{
    error = "Lines are parallel, intersection is at infinity"; //260423 hbk WR-01
    return false;
}
```

### WR-01 Site B — `DatumFindingService.TryTeachDatum`

Identical replacement to Site A (same original snippet, same new block). Preserves Allman braces.

### WR-01 Site C — `VisionAlgorithmService.IntersectLines` (static utility)

Before:
```csharp
if (isOverlapping.I == 1) return false;
intRow = iRow.D;
intCol = iCol.D;
return true;
```

After:
```csharp
if (isOverlapping.I == 1) return false; //260423 hbk WR-01 collinear
if (double.IsInfinity(iRow.D) || double.IsInfinity(iCol.D) || //260423 hbk WR-01 parallel guard
    double.IsNaN(iRow.D) || double.IsNaN(iCol.D)) //260423 hbk WR-01
{
    return false; //260423 hbk WR-01
}
intRow = iRow.D;
intCol = iCol.D;
return true;
```

Signature `public static bool IntersectLines(double row1a, double col1a, double row1b, double col1b, double row2a, double col2a, double row2b, double col2b, out double intRow, out double intCol)` is unchanged (verified by Grep of `public static bool IntersectLines(` — 1 hit, no new parameter).

### WR-03 Site — `FAIEdgeMeasurementService` ROI transform

Before:
```csharp
// Extract rotation component: transform[0]=cos(theta), transform[1]=-sin(theta)
double rotAngle = Math.Atan2(-transform[1].D, transform[0].D);
roiPhi = fai.ROI_Phi + rotAngle;
```

After:
```csharp
// hom_mat2d layout: [h00, h01, h02, h10, h11, h12, h20, h21, h22] //260423 hbk WR-03
// Indices 0 (h00=cos θ) and 3 (h10=sin θ) are translation-invariant; //260423 hbk WR-03
// index 1 (h01) is contaminated when HomMat2dRotate uses a non-origin center. //260423 hbk WR-03
double rotAngle = Math.Atan2(transform[3].D, transform[0].D); //260423 hbk WR-03
roiPhi = fai.ROI_Phi + rotAngle;
```

## Signature / API Confirmation

| Method | Signature | Unchanged? |
| --- | --- | --- |
| `DatumFindingService.TryFindDatum(HImage, DatumConfig, out HTuple, out string)` | `bool` | yes |
| `DatumFindingService.TryTeachDatum(HImage, DatumConfig, out string)` | `bool` | yes |
| `VisionAlgorithmService.IntersectLines(double x8, out double, out double)` | `bool` | yes |
| `FAIEdgeMeasurementService` outer method | unchanged (only interior rotAngle computation modified) | yes |

No public API break. Callers (`InspectionSequence.TryRunDatumPhase`, `Action_FAIMeasurement`) do not require any update.

## Evidence: `//260423 hbk` Marker Presence

| File | WR tag | Count |
| --- | --- | --- |
| DatumFindingService.cs | //260423 hbk WR-01 | 14 (≥ 12 required) |
| VisionAlgorithmService.cs | //260423 hbk WR-01 | 4 (≥ 3 required) |
| FAIEdgeMeasurementService.cs | //260423 hbk WR-03 | 4 (≥ 1 required) |

Total `//260423 hbk` across the 3 files: 22 (≥ 16 required).

## Acceptance Criteria — Verified

- [x] `grep -c 'IsInfinity(curRow.D)' DatumFindingService.cs` → 2
- [x] `grep -c 'Lines are collinear (identical)' DatumFindingService.cs` → 2
- [x] `grep -c 'Lines are parallel, intersection is at infinity' DatumFindingService.cs` → 2
- [x] `grep -c 'Lines are parallel, no intersection' DatumFindingService.cs` → 0 (old message removed)
- [x] `grep -c 'IsInfinity(iRow.D)' VisionAlgorithmService.cs` → 1
- [x] `grep -c 'IsInfinity(iCol.D)' VisionAlgorithmService.cs` → 1
- [x] `IntersectLines` signature unchanged (1 match, signature line intact)
- [x] `grep -c 'Math.Atan2(transform[3].D, transform[0].D)' FAIEdgeMeasurementService.cs` → 1
- [x] `grep -c 'Math.Atan2(-transform[1].D' FAIEdgeMeasurementService.cs` → 0 (old expression removed)
- [x] hom_mat2d layout comment present (`h00, h01, h02, h10, h11, h12`) → 1 match
- [x] Every inserted/modified line carries `//260423 hbk` with WR tag (verified by Grep counts above)
- [x] File compiles (verified by code-review: exact snippets applied from plan <action> blocks; no stray syntax; braces balanced; Allman style preserved; no new identifiers introduced beyond `System.Math`/`System.Double` BCL methods which are already in scope)

## Build Verification Note

Per executor directive, no msbuild CLI invocation attempted on this Windows machine (no reliable path). Compile-correctness is established by:
1. Edits use only BCL methods already used elsewhere in the file (`double.IsInfinity`, `double.IsNaN`, `Math.Atan2`).
2. `using System;` already present in each file — `System.Math` and `System.Double` are reachable.
3. No signature, namespace, or brace balance changes.
4. Grep confirms no stale artifacts from the old code paths.

## Deviations from Plan

None — plan executed exactly as written. All 3 tasks applied the exact code snippets from the plan's `<action>` blocks.

## Commits

| Task | Commit | Message |
| --- | --- | --- |
| 1 | `c7e741b` | fix(10): WR-01 parallel-line guard in DatumFindingService (2 sites) |
| 2 | `9395303` | fix(10): WR-01 parallel-line guard in VisionAlgorithmService.IntersectLines |
| 3 | `559da6b` | fix(10): WR-03 hom_mat2d rotation extraction uses indices [3],[0] |

## Self-Check: PASSED

- All 3 modified files exist and contain the expected post-fix content (verified by Grep).
- All 3 task commits present in git log (verified inline above).
