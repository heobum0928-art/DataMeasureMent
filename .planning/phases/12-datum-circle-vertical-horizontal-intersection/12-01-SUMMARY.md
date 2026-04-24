---
phase: 12
plan: 01
subsystem: datum-data-model
tags: [datum, data-model, ini-serialization, parambase, phase-12]
requires:
  - WPF_Example/Sequence/Param/ParamBase.cs (Save/Load switch-case — string/double handlers)
  - WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs baseline (Phase 4/11 fields)
provides:
  - EDatumAlgorithm enum (3 values) — Plan 02 (DatumFindingService) + Plan 03 (UI) consume
  - DatumConfig.AlgorithmType (string) — Plan 03 PropertyGrid drop-down
  - DatumConfig.CircleROI_* (3) / Horizontal_A_* (5) / Horizontal_B_* (5) fields — Plan 02 algorithm inputs
  - DatumConfig.CircleCenter_Row/Col + CircleDetected_Radius volatile fields — Plan 02 writes during TryTeachDatum
affects:
  - WPF_Example/DatumMeasurement.csproj (Compile ItemGroup — registered new file)
tech-stack:
  added: []
  patterns:
    - "string-based enum persistence (ParamBase can't serialize enum types — precedent: ImageSourceMode, EdgePolarity)"
    - "Enum.TryParse with fallback in helper getter for legacy INI backward-compat"
key-files:
  created:
    - WPF_Example/Custom/Sequence/Inspection/EDatumAlgorithm.cs
  modified:
    - WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs
    - WPF_Example/DatumMeasurement.csproj
decisions:
  - "AlgorithmType stored as string (not enum) — ParamBase.Save/Load switch handles only Int32/Double/String/Boolean/Rect/Line/Circle/PropertyItem[]/ModelFinderViewModel; enum would silently drop to INI"
  - "TwoLineIntersect placed first in enum — default(EDatumAlgorithm) == TwoLineIntersect; legacy Phase 4/11 INI (AlgorithmType absent → \"\") → Enum.TryParse fails → TwoLineIntersect fallback, zero regression"
  - "Rule 3 auto-fix: registered EDatumAlgorithm.cs in csproj Compile ItemGroup — absence blocked build (CS0246); not in plan but required for build gate"
  - "Line1 ROI field declarations preserved unchanged; added XML summary comment + per-algorithm D-12 semantic block above them (no signature changes)"
metrics:
  duration_minutes: 10
  completed_date: 2026-04-24
  tasks_total: 3
  tasks_completed: 3
  commits:
    - 2620a1a (Task 1)
    - 594f37e (Task 2)
    - 402da49 (Task 3 — csproj Rule 3 fix)
  build: "Debug/x64 exit 0 — zero new warnings on Plan 01 files"
---

# Phase 12 Plan 01: Datum Data Model (EDatumAlgorithm + DatumConfig extensions) Summary

**One-liner:** Added `EDatumAlgorithm` 3-value enum and extended `DatumConfig` with a string-based `AlgorithmType` selector plus Circle/Horizontal-A/Horizontal-B ROI fields and volatile circle-detection fields — unlocking Plan 02 (algorithms) and Plan 03 (UI) without any regression to the existing Phase 4/11 TwoLineIntersect flow.

## Files Changed

| File | Change | Role |
| ---- | ------ | ---- |
| `WPF_Example/Custom/Sequence/Inspection/EDatumAlgorithm.cs` | **new** | Public enum — 3 members in declaration order: `TwoLineIntersect`, `CircleTwoHorizontal`, `VerticalTwoHorizontal` (Phase 13 move target: `Halcon/Algorithms/Datum/`) |
| `WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs` | **modified** (+79 lines, 0 deletions) | Added 14 persistent fields + 3 volatile fields + AlgorithmType string + 2 helper properties |
| `WPF_Example/DatumMeasurement.csproj` | **modified** (+1 line) | Registered new file in Compile ItemGroup (Rule 3 build-gate fix) |

## Field Count Summary

| Group | Kind | Count | Default |
| ----- | ---- | ----- | ------- |
| `AlgorithmType` | persistent string | 1 | `"TwoLineIntersect"` |
| `AlgorithmTypeList` (ComboBox source) | non-persistent getter | 1 member add | — |
| `AlgorithmTypeEnum` (parse helper) | non-persistent getter | 1 member add | fallback `TwoLineIntersect` |
| `CircleROI_{Row, Col, Radius}` | persistent double | 3 | 0 |
| `Horizontal_A_{Row, Col, Phi, Length1, Length2}` | persistent double | 5 | 0 |
| `Horizontal_B_{Row, Col, Phi, Length1, Length2}` | persistent double | 5 | 0 |
| `CircleCenter_{Row, Col}` + `CircleDetected_Radius` (volatile, `Browsable(false)`) | persistent double | 3 | 0 |
| **Total new DatumConfig members** | | **19** | |

## ParamBase Serialization Strategy

- **AlgorithmType** is stored as `string` — `ParamBase.Save` / `ParamBase.Load` switch-case (L330-363) handles only `Int32 / Double / String / Boolean / Rect / Line / Circle / PropertyItem[] / ModelFinderViewModel`. Enum types would hit `default: break` and be silently skipped.
- **Helper getter `AlgorithmTypeEnum`** runs `System.Enum.TryParse` on the string; any parse failure (legacy INI where field is absent → empty string; unknown value) falls back to `EDatumAlgorithm.TwoLineIntersect`. This guarantees zero regression on Phase 4/11 INI recipes.
- **Circle/Horizontal fields** are all `double` — covered by ParamBase's `"Double"` case. Legacy INI (fields absent) → `IniFile` returns default-initialized values → 0.
- **Volatile `CircleCenter_*` and `CircleDetected_Radius`** follow the Phase 11 `Line*Detected_*` precedent: `[Browsable(false)]` hides them from the PropertyGrid but ParamBase reflection still serializes them (INI writes 0 on first save, Plan 02's `TryTeachDatum` will populate them).

**Round-trip trace (inspection-only, no runtime):** AlgorithmType → `case "String"` handled; Circle/Horizontal fields → `case "Double"` handled; legacy INI (fields absent) → `IniFile` returns default-initialized values → `AlgorithmType` becomes `""` → `AlgorithmTypeEnum` `TryParse` fails → falls back to `TwoLineIntersect` per D-09. ✓

## Build Result

```
msbuild WPF_Example/DatumMeasurement.csproj -p:Configuration=Debug -p:Platform=x64 -v:minimal
→ DatumMeasurement -> bin/x64/Debug/DatumMeasurement.exe (exit 0)
```

Pre-existing warnings (unchanged, NOT Plan 01 scope):
- `VirtualCamera.cs(266,13)` — CS0162 unreachable code
- `VisionAlgorithmService.cs(64,22)` — CS0219 unused local
- `MSB3884` — MinimumRecommendedRules.ruleset missing (MSBuild infra)

**Zero new warnings referencing `DatumConfig.cs` or `EDatumAlgorithm.cs`.**

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Register EDatumAlgorithm.cs in csproj Compile ItemGroup**
- **Found during:** Task 3 (build gate)
- **Issue:** First build after Task 1 produced `CS0246 'EDatumAlgorithm' 형식 또는 네임스페이스 이름을 찾을 수 없습니다.` at DatumConfig.cs:57 because the new file was not in `<Compile Include="..."/>` ItemGroup. The project uses classic-style csproj (packages.config, explicit Compile entries) — new files must be registered manually. The plan EDIT specs did not mention csproj changes.
- **Fix:** Added `<Compile Include="Custom\Sequence\Inspection\EDatumAlgorithm.cs" />` between the existing DatumConfig.cs and EdgeOptionLists.cs Compile entries (preserving folder grouping).
- **Files modified:** `WPF_Example/DatumMeasurement.csproj`
- **Commit:** `402da49`

### Plan Adherence

All other EDITs executed exactly as specified. Every new/modified line carries `//260423 hbk Phase 12 D-XX` marker per CLAUDE.md convention. K&R brace style preserved in `DatumConfig.cs`; Allman used only in the new standalone `EDatumAlgorithm.cs` file per PATTERNS §Shared 8.

## Next

**Plan 02 (DatumFindingService algorithms)** — implement the two new algorithm branches (`CircleTwoHorizontal`, `VerticalTwoHorizontal`) that consume:
- `CircleROI_Row/Col/Radius` → `find_circle` → write `CircleCenter_*` + `CircleDetected_Radius`
- `Horizontal_A_* + Horizontal_B_*` → concat + `FitLineContourXld` → compute intersection
- `Line1_*` (reused for vertical ROI in `VerticalTwoHorizontal` per D-07/D-12)

## Self-Check: PASSED

- [x] `WPF_Example/Custom/Sequence/Inspection/EDatumAlgorithm.cs` — FOUND
- [x] `WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs` — FOUND (modified)
- [x] Commit `2620a1a` (Task 1) — FOUND
- [x] Commit `594f37e` (Task 2) — FOUND
- [x] Commit `402da49` (Task 3 Rule 3 fix) — FOUND
- [x] Build Debug/x64 exit 0 — VERIFIED (DatumMeasurement.exe produced)
- [x] Zero new warnings on Plan 01 files — VERIFIED (grep of build output shows only pre-existing VirtualCamera/VisionAlgorithmService warnings)
- [x] All acceptance-criteria grep counts match expected values (1,1,1,1,1,1,0 for the 7-check panel)
