---
phase: 59-vision-algorithm-b-2026-06-23
plan: "02"
subsystem: EthernetVision
tags: [shape-matching, align, halcon, composition, AV-03, AV-04]
dependency_graph:
  requires: [59-01]  # AlignResult.cs, AlignRefPose.cs
  provides: [AlignShapeMatchService]
  affects: [EthernetVisionHandler (Phase 59-03), AlignControlView (Phase 61)]
tech_stack:
  added: []
  patterns:
    - PatternMatchService composition (D-01)
    - Newtonsoft.Json TypeNameHandling.None (T-59-02 RCE mitigation)
    - per-mode .shm + sidecar JSON under ETHERNET_ALIGN\
    - K&R brace style, C# 7.2 if/else
key_files:
  created:
    - WPF_Example/Custom/EthernetVision/AlignShapeMatchService.cs
  modified:
    - WPF_Example/DatumMeasurement.csproj
decisions:
  - "Composition over inheritance/reimplementation: _matcher = new PatternMatchService() (D-01)"
  - "Full-image TryFindPose: roiRow=0, roiCol=0, len=99999, marginPx=0, downsample=1.0 — clamps to image bounds"
  - "Angles from PatternMatchService are already DEGREES — ThetaDeg = curAngleDeg - refAngleDeg, no rad re-conversion"
  - "TrySaveRefPose uses explicit property assignment (C# 7.2 — no object-initializer shorthand with out params)"
metrics:
  duration_minutes: 4
  completed_date: "2026-06-24"
  tasks_completed: 2
  files_created: 1
  files_modified: 1
requirements: [AV-03, AV-04]
---

# Phase 59 Plan 02: AlignShapeMatchService Summary

**One-liner:** Shape-match align orchestration via PatternMatchService composition — per-mode (Tray/Bottom) .shm + ref-pose sidecar JSON, offset px→mm, ThetaDeg for Bottom.

## What Was Built

Created `WPF_Example/Custom/EthernetVision/AlignShapeMatchService.cs` — a new service class that:

1. **Composes** `PatternMatchService` as a `private readonly` field (D-01). No HALCON operators are called directly; all matching logic delegates to the validated engine.
2. **TryTeach**: validates input → selects angle extent by mode (`TRAY=10°`, `BOTTOM=45°`) → `_matcher.TryCreateModel` (write .shm) → `_matcher.TryFindRefPose` (find in same teach image) → `TrySaveRefPose` (write sidecar JSON).
3. **Run**: loads ref pose JSON → `_matcher.TryFindPose` (full-image, `len=99999`, margin=0, downsample=1) → `dRow=cur-ref`, `dCol=cur-ref` → `mm = px × (EthernetPixelResolution/1000)` → sets `OffsetXmm/OffsetYmm`; Bottom additionally sets `ThetaDeg = curAngleDeg - refAngleDeg` + `HasTheta=true`.
4. **HasTemplate / TryLoadTemplate**: file-system presence check on both .shm and .json (Phase 61 UI button gating).

Registered in `DatumMeasurement.csproj` inside the EthernetVision `<Compile>` block.

## Deviations from Plan

None — plan executed exactly as written.

The 59-PATTERNS.md contained C# 8-style object initializers (`new AlignRefPose { ... }`) in its code examples. These were expanded to explicit property-by-property assignment to comply with the C# 7.2 constraint from CLAUDE.md. This is consistent with the plan's directive ("C# 7.2 only — no switch expressions / nullable refs / records").

## Threat Surface Scan

| Flag | File | Description |
|------|------|-------------|
| threat_flag: deserialization | AlignShapeMatchService.cs | LoadRefPose deserializes sidecar JSON — mitigated per T-59-02 via TypeNameHandling.None |

No new network endpoints or auth paths introduced. File I/O is local recipe folder only.

## Known Stubs

None. The service has no placeholder returns; all paths either return a meaningful result or `AlignResult{Found=false}`.

## Self-Check

- [x] `WPF_Example/Custom/EthernetVision/AlignShapeMatchService.cs` exists
- [x] `DatumMeasurement.csproj` contains `Custom\EthernetVision\AlignShapeMatchService.cs`
- [x] Commit d143c34 present
- [x] PatternMatchService.cs unmodified (composition only)

## Self-Check: PASSED
