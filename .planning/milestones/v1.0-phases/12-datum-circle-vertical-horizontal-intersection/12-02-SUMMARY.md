---
phase: 12
plan: 02
subsystem: datum-finding-algorithms
tags: [datum, halcon, algorithm, phase-12, simul-mode]
requires:
  - WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs (Plan 01 — AlgorithmTypeEnum + CircleROI_* + Horizontal_A/B_*)
  - WPF_Example/Custom/Sequence/Inspection/EDatumAlgorithm.cs (Plan 01 — 3-value enum)
  - WPF_Example/Halcon/Algorithms/VisionAlgorithmService.cs (TryFindCircle signature L120-200, reused unchanged)
provides:
  - DatumFindingService.TryTeachDatum → 3-way dispatch on AlgorithmTypeEnum (Plan 03 UI calls into this)
  - TryTeachCircleTwoHorizontal: Circle fit + horizontal 2-ROI concat + vertical virtual line intersection
  - TryTeachVerticalTwoHorizontal: vertical ROI fit + horizontal 2-ROI concat + intersection
  - Private TryExtractEdgePoints helper (single-ROI edge extraction, skips FitLine step)
  - Private const MIN_HORIZONTAL_EDGES = 10 (horizontal concat threshold)
affects:
  - WPF_Example/Halcon/Algorithms/DatumFindingService.cs (single file modified)
tech-stack:
  added: []
  patterns:
    - "Halcon 2-ROI concat pipeline: GenContourPolygonXld ×2 → ConcatObj → FitLineContourXld tukey"
    - "Vertical virtual line as 2-point representation for IntersectionLl (centerRow±1.0, centerCol)"
    - "VisionAlgorithmService.TryFindCircle reuse with datumTransform=null (teaching identity)"
    - "HObject disposal in finally (contourA, contourB, concatContour)"
    - "Error-literal contract: SPEC AC strings used byte-for-byte, no algorithm prefix (D-14)"
key-files:
  created: []
  modified:
    - WPF_Example/Halcon/Algorithms/DatumFindingService.cs
decisions:
  - "TryTeachDatum dispatches on config.AlgorithmTypeEnum (string→enum parse helper from Plan 01), default case falls back to TwoLineIntersect — zero regression for legacy Phase 4/11 INI"
  - "Phase 4 body moved verbatim to private TryTeachTwoLineIntersect — all 4 legacy error literals ('Line1: ', 'Line2: ', 'Lines are collinear (identical)…', 'Lines are parallel…') preserved byte-for-byte; Phase 11 D-11 LastTeachSucceeded writes preserved in same order"
  - "TryFindLine kept as-is (optional refactor deferred) — TwoLineIntersect regression risk = 0; new TryExtractEdgePoints is sibling helper, not integrated"
  - "Public TryFindDatum (runtime) untouched per SPEC Out-of-scope — Phase 12 is teaching-only; runtime re-detection per algorithm is explicitly deferred"
  - "Req 5d (direction consistency for CircleTwoHorizontal) deferred to Phase 13 per D-17 — literal '// TODO: Phase 13 — 방향 정합성 검사' comment in code carries the deferral marker"
  - "Vertical virtual line for CircleTwoHorizontal represented as 2 points (centerRow−1.0, centerCol) and (centerRow+1.0, centerCol) — IntersectionLl-compatible; mathematically equivalent to SPEC Req 4(a) closed-form"
  - "RefAngleRad for both new algorithms = Atan2(hrE−hrB, hcE−hcB) of horizontal concat line (SPEC Req 4)"
  - "Line1Detected / Line2Detected volatile fields reused as overlay source per D-13: Circle algorithm uses Line1Detected for vertical virtual line ±50px, Vertical algorithm uses Line1Detected for the detected vertical line; both use Line2Detected for horizontal concat"
metrics:
  duration_minutes: 8
  completed_date: 2026-04-24
  tasks_total: 3
  tasks_completed: 3
  commits:
    - 6f6db7b (Task 1 — dispatch refactor + helpers)
    - e6cc52e (Task 2 — TryTeachCircleTwoHorizontal)
    - 0e9c1f2 (Task 3 — TryTeachVerticalTwoHorizontal)
  build: "msbuild Debug/x64 exit 0 — zero new warnings on DatumFindingService.cs"
---

# Phase 12 Plan 02: Datum Finding Algorithms (Circle + VerticalTwoHorizontal) Summary

**One-liner:** Refactored `DatumFindingService.TryTeachDatum` into a 3-way dispatch on `AlgorithmTypeEnum` and implemented two new teaching algorithms (`CircleTwoHorizontal`, `VerticalTwoHorizontal`) that share a `TryExtractEdgePoints` helper plus a 2-ROI horizontal concat + `FitLineContourXld` pipeline — with all Phase 4 TwoLineIntersect behavior moved verbatim into a private method (regression 0).

## Files Changed

| File | Change | Role |
| ---- | ------ | ---- |
| `WPF_Example/Halcon/Algorithms/DatumFindingService.cs` | **modified** (+264 lines net) | Added dispatch, 2 new algorithms, 1 helper, 1 constant |

## Method Inventory

| Member | Visibility | State | Notes |
| ------ | ---------- | ----- | ----- |
| `MIN_HORIZONTAL_EDGES = 10` | private const | **NEW** | D-15 threshold for horizontal concat edge count |
| `TryFindDatum(HImage, DatumConfig, out HTuple, out string)` | public | **UNCHANGED** | Runtime correction path — SPEC Out-of-scope excludes algorithm-specific runtime re-detection |
| `TryTeachDatum(HImage, DatumConfig, out string)` | public | **MODIFIED** | Now a 6-line dispatch on `config.AlgorithmTypeEnum` |
| `TryTeachTwoLineIntersect(HImage, DatumConfig, out string)` | private | **NEW (body carried from Phase 4)** | Phase 4 logic verbatim — regression 0 |
| `TryTeachCircleTwoHorizontal(HImage, DatumConfig, out string)` | private | **NEW** | Req 2 + 3 + 4a + 5a/b/c + 6 |
| `TryTeachVerticalTwoHorizontal(HImage, DatumConfig, out string)` | private | **NEW** | Req 2b + 3 + 4b + 5a/b/e + 6 |
| `TryFindLine(...)` | private | **UNCHANGED** | Kept as-is; optional refactor deferred (TwoLineIntersect regression risk = 0) |
| `TryExtractEdgePoints(...)` | private | **NEW** | Raw edge tuples from a single Rectangle2 ROI, MeasureHandle disposed in finally |

## Dispatch Shape

```csharp
public bool TryTeachDatum(HImage image, DatumConfig config, out string error)
{
    error = null;
    if (image == null || config == null) { error = "image or config is null"; return false; }

    switch (config.AlgorithmTypeEnum)
    {
        case EDatumAlgorithm.CircleTwoHorizontal:   return TryTeachCircleTwoHorizontal(image, config, out error);
        case EDatumAlgorithm.VerticalTwoHorizontal: return TryTeachVerticalTwoHorizontal(image, config, out error);
        case EDatumAlgorithm.TwoLineIntersect:
        default:                                    return TryTeachTwoLineIntersect(image, config, out error);
    }
}
```

## SPEC Requirements Coverage

| Req | Status | Evidence |
| --- | ------ | -------- |
| Req 2 — Circle fit algorithm | SATISFIED | `VisionAlgorithmService.TryFindCircle` call with `null` transform → `CircleCenter_Row/Col` + `CircleDetected_Radius` writeback in `TryTeachCircleTwoHorizontal` |
| Req 2b — Vertical ROI line fit | SATISFIED | `TryFindLine` reused with `Line1_*` fields per D-07/D-12 in `TryTeachVerticalTwoHorizontal` |
| Req 3 — Horizontal 2-ROI concat | SATISFIED | `TryExtractEdgePoints` ×2 → `GenContourPolygonXld` ×2 → `ConcatObj` → `FitLineContourXld("tukey", -1, 0, 5, 2, …)` in both new algorithms |
| Req 4a — Circle × horizontal intersection | SATISFIED | `IntersectionLl` with 2-point vertical virtual line `(centerRow±1.0, centerCol)` × horizontal concat line |
| Req 4b — Vertical × horizontal intersection | SATISFIED | `IntersectionLl(vrB, vcB, vrE, vcE, hrB, hcB, hrE, hcE, …)` |
| Req 5a — Horizontal line fit failures | SATISFIED | 3 literal forms: `"Horizontal line fit failed: " + {edgeErrorA|edgeErrorB|fitEx.Message}` + `"Horizontal line fit failed: insufficient edges (N)"` (MIN_HORIZONTAL_EDGES guard) |
| Req 5b — Intersection undefined | SATISFIED | `"Intersection undefined: lines are collinear"` (isOverlapping.I==1) + `"Intersection undefined: lines are parallel"` (Infinity/NaN) — in both algorithms |
| Req 5c — Circle fit failed | SATISFIED | `"Circle fit failed: " + circleError` after `TryFindCircle` false return |
| Req 5d — Direction consistency | **DEFERRED to Phase 13 (D-17)** | `// TODO: Phase 13 — 방향 정합성 검사 (운용 정책 확립 후 구현)` literal comment present in `TryTeachCircleTwoHorizontal` success branch |
| Req 5e — Vertical line fit failed | SATISFIED | `"Vertical line fit failed: " + lineError` after `TryFindLine` false return in `TryTeachVerticalTwoHorizontal` |
| Req 6 — Teach success writeback | SATISFIED | Both algorithms set `RefOriginRow/Col/RefAngleRad`, `IsConfigured = true`, `LastTeachSucceeded = true`, `Line1Detected_*`, `Line2Detected_*` on success; Plan 01 guarantees INI round-trip of these fields |

## Error-Literal Checklist (10 literals)

| # | Literal | Algorithm | Count (file) | Status |
| - | ------- | --------- | ------------ | ------ |
| 1 | `"Circle fit failed: " + circleError` | CircleTwoHorizontal (Req 5c) | 1 | PASS |
| 2 | `"Vertical line fit failed: " + lineError` | VerticalTwoHorizontal (Req 5e) | 1 | PASS |
| 3 | `"Horizontal line fit failed: " + edgeErrorA` | both (Req 5a — A path) | 2 | PASS |
| 4 | `"Horizontal line fit failed: " + edgeErrorB` | both (Req 5a — B path) | 2 | PASS |
| 5 | `"Horizontal line fit failed: insufficient edges (" + totalEdges + ")"` | both (Req 5a — D-15) | 2 | PASS |
| 6 | `"Horizontal line fit failed: " + fitEx.Message` | both (Req 5a — FitLine ex) | 2 | PASS |
| 7 | `"Intersection undefined: lines are collinear"` | both new (Req 5b) | 2 | PASS |
| 8 | `"Intersection undefined: lines are parallel"` | both new (Req 5b) | 2 | PASS |
| 9 (legacy) | `"Lines are collinear (identical), no unique intersection"` | TwoLineIntersect (Phase 4) | 1 (Teach path) + 1 (Find runtime) = 2 file occurrences | PRESERVED |
| 10 (legacy) | `"Lines are parallel, intersection is at infinity"` | TwoLineIntersect (Phase 4) | 1 (Teach) + 1 (Find) = 2 file occurrences | PRESERVED |

Legacy `"Line1: " + lineError` / `"Line2: " + lineError` also preserved byte-for-byte — 2 file occurrences each (1 in `TryFindDatum` untouched runtime + 1 in `TryTeachTwoLineIntersect` moved verbatim).

## Dispatch Path Walk-through (SIMUL_MODE code inspection)

**AlgorithmType="TwoLineIntersect"** → `TryTeachTwoLineIntersect` → reuses Phase 4 logic unchanged:
- TryFindLine(Line1_*) → TryFindLine(Line2_*) → IntersectionLl → writeback → Line1Detected=Line1, Line2Detected=Line2.
- Phase 4/11 UAT-approved behavior; no regression risk.

**AlgorithmType="CircleTwoHorizontal"** → `TryTeachCircleTwoHorizontal`:
- TryFindCircle(CircleROI_*) → writes CircleCenter_Row/Col + CircleDetected_Radius
- TryExtractEdgePoints(Horizontal_A_*) + TryExtractEdgePoints(Horizontal_B_*)
- Count gate: rowEdgeA.TupleLength + rowEdgeB.TupleLength < 10 → fail
- GenContourPolygonXld×2 → ConcatObj → FitLineContourXld(tukey) → (hrB, hcB, hrE, hcE)
- IntersectionLl((centerRow−1, centerCol) → (centerRow+1, centerCol)) × (hrB,hcB)→(hrE,hcE))
- isOverlapping.I==1 / Infinity / NaN guards
- Success writeback + Line1Detected=vertical virtual line ±50px, Line2Detected=horizontal concat
- `// TODO: Phase 13 — 방향 정합성 검사` deferral marker present.

**AlgorithmType="VerticalTwoHorizontal"** → `TryTeachVerticalTwoHorizontal`:
- TryFindLine(Line1_*) (vertical ROI per D-07) → (vrB, vcB, vrE, vcE)
- TryExtractEdgePoints(Horizontal_A_*) + TryExtractEdgePoints(Horizontal_B_*)
- Count gate: same as Circle path
- Horizontal concat fit: same pipeline
- IntersectionLl(vertical line × horizontal concat)
- Same collinear/parallel guards
- Success writeback + Line1Detected=detected vertical line, Line2Detected=horizontal concat
- CircleCenter_*/CircleDetected_Radius untouched (stay at defaults or previous state).

## Build Result

```
msbuild WPF_Example/DatumMeasurement.csproj -p:Configuration=Debug -p:Platform=x64 -v:minimal
→ DatumMeasurement -> bin/x64/Debug/DatumMeasurement.exe (exit 0)
```

Pre-existing warnings only (NOT Plan 02 scope — present before Task 1):
- `VirtualCamera.cs(266,13)` CS0162 unreachable code
- `VisionAlgorithmService.cs(64,22)` CS0219 unused local
- `MSB3884` MinimumRecommendedRules.ruleset missing

**Zero new warnings referencing `DatumFindingService.cs`.**

## Deviations from Plan

### Plan Adherence

All 3 tasks executed exactly as specified. EDIT blocks in Task 1/2/3 action sections applied verbatim. Every new/modified line carries the `//260423 hbk Phase 12` marker per CLAUDE.md comment convention.

### Grep AC Observations (not deviations — plan-level verification confirms intent)

The plan's Task 1 AC includes `grep -cF 'error = "Line1: " + lineError;' returns 1`, but the actual file has 2 occurrences: one in the untouched public `TryFindDatum` (runtime path, L54 — explicitly NOT ALLOWED to modify per plan) and one in the newly-moved private `TryTeachTwoLineIntersect` (L171 — carried verbatim per plan's Task 1 EDIT 3). Both are **required** by the plan's "Do NOT modify TryFindDatum" constraint + "move body verbatim" instruction. Same applies to `"Line2: "`, `"Lines are collinear (identical)…"`, `"Lines are parallel…"` legacy literals (each appears twice in file by design). The plan-level `<verification>` block phrases this correctly as "preserved verbatim" (no exact count given), so the file is conformant to plan intent.

## Next

**Plan 03 (UI wiring)** — add `btn_teachDatum` + `ECanvasMode.TeachDatum` + `EDatumTeachStep` state machine in MainView (3-way algorithm switch with per-step drawing handlers), extend `HalconDisplayService.RenderDatumOverlay` with algorithm-aware branches (Circle detection overlay, Horizontal A/B Rectangle2, Line2 ROI suppression for non-TwoLineIntersect), wire `InspectionListView` Datum-node selection to enable `btn_teachDatum`, and perform SIMUL_MODE 3-way visual verification.

## Self-Check: PASSED

- [x] `WPF_Example/Halcon/Algorithms/DatumFindingService.cs` — FOUND (modified)
- [x] Commit `6f6db7b` (Task 1) — FOUND in git log
- [x] Commit `e6cc52e` (Task 2) — FOUND in git log
- [x] Commit `0e9c1f2` (Task 3) — FOUND in git log
- [x] Build Debug/x64 exit 0 — VERIFIED (DatumMeasurement.exe produced)
- [x] Zero new warnings on DatumFindingService.cs — VERIFIED (grep of build output shows only pre-existing VirtualCamera/VisionAlgorithmService warnings)
- [x] `grep -c "case EDatumAlgorithm\\." DatumFindingService.cs` returns 3 (plan-level verification) — VERIFIED
- [x] Dispatch + 2 stubs replaced + helper + const all present — VERIFIED via 29-line grep panel
- [x] `"TODO: Phase 13 — 방향 정합성 검사"` comment present (Req 5d D-17 deferral) — VERIFIED
- [x] Every new/modified line carries `//260423 hbk Phase 12` or preserved legacy marker — VERIFIED by code inspection
