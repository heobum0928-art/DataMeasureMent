---
phase: 23
plan: 01
subsystem: measurement
tags: [measurement, halcon, edge-to-line-distance, signature-extension, datum-relative]
requirements_completed: [ALG-01 partial — measurement infra (EdgeToLineDistance class + TryFitLine signature extension)]
provides:
  - "EdgeToLineDistanceMeasurement class (Datum-relative Y 거리 측정)"
  - "TryFitLine selection 인자 (EdgeSelection 명시, D-10)"
  - "EdgeToLineDistanceMeasurement csproj 등록 (CS0246 차단)"
affects:
  - "WPF_Example/Custom/Sequence/Inspection/Measurements/EdgeToLineDistanceMeasurement.cs (NEW)"
  - "WPF_Example/Halcon/Algorithms/VisionAlgorithmService.cs (MOD)"
  - "WPF_Example/DatumMeasurement.csproj (MOD)"
dependency_graph:
  requires: []
  provides:
    - "EdgeToLineDistance type for Plan 23-02 MeasurementFactory registration"
    - "TryFitLine(..., selection) signature for Plan 23-02 caller validation"
  affects:
    - "Plan 23-02 (Wave 2): MeasurementFactory case + GetTypeNames + GrabOrLoadDatumImage TeachingImagePath fallback + Datum CTH INI seed"
tech_stack:
  added: []
  patterns:
    - "MeasurementBase 파생 + Factory dispatch (Phase 6, EdgeToLineDistance = 7번째 algorithm)"
    - "PropertyTools PropertyGrid [Category]/[ItemsSourceProperty]/Browsable(false) (Phase 15/19)"
    - "TryFindCircleByPolarSampling selection 변환 패턴 (PascalCase→Halcon lower-case)"
    - "AffineTransPoint2d Datum-relative 좌표 추출 + -datumRow 부호 반전 (D-02)"
    - "//260512 hbk Phase 23 ALG-01 marker (Phase 20 D-12 stacking 준수)"
key_files:
  created:
    - "WPF_Example/Custom/Sequence/Inspection/Measurements/EdgeToLineDistanceMeasurement.cs"
  modified:
    - "WPF_Example/Halcon/Algorithms/VisionAlgorithmService.cs"
    - "WPF_Example/DatumMeasurement.csproj"
decisions:
  - "TryFitLine signature 확장: optional `string selection = \"all\"` 추가 → 5 caller 무수정 호환 (default param, C# 7.2)"
  - "MeasurePos 'all' 하드코딩 제거 → measureSel 변수 분기 (First/Last/All, OrdinalIgnoreCase)"
  - "EdgeToLineDistanceMeasurement overlay = 빈 리스트 (PointToLineDistance 패턴, Phase 7-01 D-03)"
  - "EdgeDirection default = TtoB (수평 에지 검출, Y 거리 측정 의도, RESEARCH Pitfall 6)"
  - "EdgeSelection default = First (DatumConfig 기존 default 일치, D-10 명시)"
  - "D-11 literal guard: TryExecute 진입부에서 datumTransform null/empty → return 'Datum not found' (upstream gating 보조 이중 안전망)"
  - "Y 부호 반전 = 클라이언트측 (`resultValue = -datumRow * pixelResolution`) → 3 Datum 알고리즘 영향 0"
metrics:
  duration_minutes: 12
  completed_date: "2026-05-12"
  tasks_count: 3
  files_created: 1
  files_modified: 2
  commits_count: 2
---

# Phase 23 Plan 01: Top #1 A시리즈 Measurement Infra Summary

**One-liner:** EdgeToLineDistance Measurement 클래스 신규 생성 + VisionAlgorithmService.TryFitLine signature 확장 (EdgeSelection 명시, D-10 memory feedback) — ALG-01 의 measurement infra wave 완료.

## Accomplishments

- **3 files touched** (1 new + 2 modified):
  - 신규: `EdgeToLineDistanceMeasurement.cs` (99 LOC, Allman style, 16 markers)
  - 수정: `VisionAlgorithmService.cs` (TryFitLine signature + measureSel 분기, 6 markers)
  - 수정: `DatumMeasurement.csproj` (Compile Include 1줄)
- **msbuild Debug/x64 Rebuild PASS** — 0 errors, 6 warnings (Phase 21 baseline preserved: MSB3884×2 + CS0162×2 + CS0219×2), 신규 warning 0
- **22 `//260512 hbk Phase 23 ALG-01` markers** across 2 cs files (Plan 요구: ≥8)
- **5 기존 TryFitLine caller 모두 무수정 호환** (default `"all"` 자동 적용 — byte-identical 회귀 0):
  - PointToLineDistanceMeasurement.cs L66, L79
  - PointToPointDistanceMeasurement.cs L65, L78
  - LineToLineAngleMeasurement.cs L65, L76
  - LineToLineDistanceMeasurement.cs L64, L75
- **DatumMeasurement.exe mtime 갱신** (`WPF_Example/bin/x64/Debug/DatumMeasurement.exe`)

## Task Commits

| Task | Description | Commit | Files |
|------|-------------|--------|-------|
| 1 | Extend TryFitLine signature with optional `selection = "all"` + measureSel branch | `e3e28e9` | VisionAlgorithmService.cs |
| 2 | Create EdgeToLineDistanceMeasurement.cs + register in csproj | `249b5a4` | EdgeToLineDistanceMeasurement.cs (new), DatumMeasurement.csproj |
| 3 | msbuild Debug/x64 Rebuild verification (PASS, baseline 6 warnings preserved) | (verification only — no commit, build_23_w1.log gitignored) | — |

## Acceptance Criteria Verification

| AC | Criterion | Status | Evidence |
|----|-----------|--------|----------|
| 1 | TryFitLine signature has `string selection = "all"` | PASS | VisionAlgorithmService.cs:27 |
| 2 | `measureSel` 변수 + First/Last/else→all 3분기 | PASS | VisionAlgorithmService.cs:94-106 |
| 3 | MeasurePos 호출이 `measureSel` 사용 ('all' 하드코딩 제거) | PASS | VisionAlgorithmService.cs:111 (`pol, measureSel, ...`), 0 matches of `pol, "all",` |
| 4 | TryFitLine 마커 ≥ 3 | PASS | 6 markers in VisionAlgorithmService.cs |
| 5 | EdgeToLineDistanceMeasurement.cs class signature + TypeName | PASS | L18 `class EdgeToLineDistanceMeasurement : MeasurementBase`, L20 `TypeName { get { return "EdgeToLineDistance"; } }` |
| 6 | 7 Edge property (Threshold/Sigma/SampleCount/TrimCount/Polarity/Direction/Selection) | PASS | L29-39 |
| 7 | EdgeDirection default = "TtoB" | PASS | L37 |
| 8 | EdgeSelection default = "First" | PASS | L39 |
| 9 | ItemsSourceProperty(nameof(EdgeSelectionList)) + getter | PASS | L38 (attr), L48 (getter) |
| 10 | svc.TryFitLine(...EdgeSelection) 호출 | PASS | L77-80 |
| 11 | AffineTransPoint2d(datumTransform, pRow, pCol, ...) | PASS | L89 |
| 12 | resultValue = -datumRow * pixelResolution (D-02) | PASS | L97 |
| 13 | try/catch AffineTransPoint2d 보호 | PASS | L86-95 |
| 14 | D-11 literal guard `datumTransform == null \|\| datumTransform.Length == 0` | PASS | L63 |
| 15 | error = "Datum not found" 1회 | PASS | L65 |
| 16 | EdgeToLineDistanceMeasurement marker ≥ 5 | PASS | 16 markers |
| 17 | csproj `<Compile Include="...EdgeToLineDistanceMeasurement.cs" />` | PASS | DatumMeasurement.csproj:225 |
| 18 | msbuild PASS, 6 warnings, 0 errors | PASS | build_23_w1.log: 6 warnings (3 codes), 0 errors, EXIT=0 |

## Decisions Made

1. **TryFitLine signature 확장 선택 (vs 직접 MeasurePos 호출):** optional default param 채택으로 5 caller 무수정 보장. C# 7.2 default param 표준 거동 활용. (RESEARCH Op#3 + Assumption A4)
2. **MeasurePos selection 변환 = TryFindCircleByPolarSampling L249-264 패턴 차용:** 동일 파일 내 in-source 패턴 일관 (StringComparison.OrdinalIgnoreCase, `string.Equals` 분기).
3. **Y 부호 반전 = 클라이언트측 (EdgeToLineDistance.TryExecute 내부):** `resultValue = -datumRow * pixelResolution`. 3 Datum 알고리즘 (TLI/CTH/VTH) 영향 0. (D-02 + RESEARCH Pitfall 2)
4. **EdgeToLineDistance overlay = 빈 리스트:** PointToLineDistance 패턴 그대로 (Phase 7-01 D-03). SC#2 strip 시각화는 InspectionListView 노드 색상 (CO-05) 경로 위임. UAT Plan 23-03 에서 사용자 확인. (RESEARCH Pitfall 4)
5. **EdgeDirection default = "TtoB":** 수평 에지 검출 + Y 거리 측정 의도. PointToLineDistance default "LtoR" 와 차이. (RESEARCH Pitfall 6)
6. **EdgeSelection default = "First":** DatumConfig 기존 default 일치. (D-10 memory feedback)
7. **D-11 literal 구현 채택:** TryExecute 진입부에서 `datumTransform == null \|\| datumTransform.Length == 0` 가드 → return "Datum not found". upstream gating (Action_FAIMeasurement.EStep.DatumPhase) 은 보조 이중 안전망 — 경계 케이스 (DatumPhase 통과 후 transform 누락) 도 본 가드로 차단.

## Deviations from Plan

None — plan executed exactly as written. 모든 task의 action/verify/acceptance_criteria 가 변경 없이 충족됨.

## Authentication Gates

None — Phase 23 은 코드 추가/시그니처 확장 (외부 입력/세션/네트워크 변경 0).

## Known Stubs

None — EdgeToLineDistanceMeasurement 의 TryExecute 가 실제 데이터 경로 (TryFitLine → AffineTransPoint2d → -datumRow * pixelResolution) 전부 구현. overlay 빈 리스트는 PointToLineDistance 의 lock-in 패턴 (Phase 7-01 D-03) 으로 stub 아닌 의도된 설계.

## Carry-overs to Plan 23-02 (Wave 2)

- **MeasurementFactory.Create switch 7번째 case 추가:** `case "EdgeToLineDistance": return new EdgeToLineDistanceMeasurement(owner);`
- **MeasurementFactory.GetTypeNames 배열 확장:** `"EdgeToLineDistance"` 1줄 추가 → FAIConfig.EdgeMeasureType PropertyGrid 드롭다운 자동 노출 + INI Type 자동 dispatch.
- **Action_FAIMeasurement.GrabOrLoadDatumImage TeachingImagePath fallback 분기 추가** (D-04, Phase 22 carry-over):
  - 진입부에 `DatumConfigs[0].TeachingImagePath` 우선순위 분기
  - SIMUL_MODE 에서 `!string.IsNullOrEmpty(teachingPath) && File.Exists(teachingPath)` 2-step 가드 → fallback = ShotParam.SimulImagePath
- **Datum CTH INI seed (Top Fixture #1):** D-01 lock 에 따라 CircleTwoHorizontal 알고리즘 단일 DatumConfig 시드 (researcher/planner 후속).

## Build Verification

```
msbuild WPF_Example/DatumMeasurement.csproj -p:Configuration=Debug -p:Platform=x64 -v:minimal -nologo -t:Rebuild
```

- **EXIT:** 0
- **Errors:** 0
- **Warnings:** 6 (Phase 21 baseline)
  - MSB3884 × 2 (MinimumRecommendedRules.ruleset 누락 — pre-existing csproj/wpftmp 페어)
  - CS0162 × 2 (VirtualCamera.cs:266 unreachable code — pre-existing)
  - CS0219 × 2 (VisionAlgorithmService.cs:65 unused 'scanHorizontal' — pre-existing)
- **신규 warning:** 0
- **CS0246 (EdgeToLineDistanceMeasurement type missing):** 부재 — csproj Compile Include 정상 등록
- **출력:** `WPF_Example/bin/x64/Debug/DatumMeasurement.exe` mtime 갱신

## Self-Check: PASSED

**Files verified:**
- FOUND: WPF_Example/Custom/Sequence/Inspection/Measurements/EdgeToLineDistanceMeasurement.cs
- FOUND: WPF_Example/Halcon/Algorithms/VisionAlgorithmService.cs (modifed L27, L93-111)
- FOUND: WPF_Example/DatumMeasurement.csproj (modified L225)
- FOUND: WPF_Example/bin/x64/Debug/DatumMeasurement.exe (rebuild output)

**Commits verified:**
- FOUND: e3e28e9 (Task 1: TryFitLine signature extension)
- FOUND: 249b5a4 (Task 2: EdgeToLineDistanceMeasurement + csproj)

**Marker count verified:**
- VisionAlgorithmService.cs: 6 markers (요구 ≥3) ✓
- EdgeToLineDistanceMeasurement.cs: 16 markers (요구 ≥5) ✓
- Total: 22 markers (요구 ≥8) ✓

**Caller compatibility verified:**
- 4 caller 파일 × 2 호출 = 8 TryFitLine calls — byte-identical (Grep `svc\.TryFitLine` returns 8 across 4 files, same as pre-change)
