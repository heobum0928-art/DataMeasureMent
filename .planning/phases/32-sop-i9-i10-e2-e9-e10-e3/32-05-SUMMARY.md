---
phase: 32-sop-i9-i10-e2-e9-e10-e3
plan: "05"
subsystem: measurement-algorithm
tags: [E3, CompoundShortAxisDistance, MeasurementFactory, csproj]
dependency_graph:
  requires:
    - "32-01: VisionAlgorithmService.TryFindLargestContourRect"
  provides:
    - "CompoundShortAxisDistanceMeasurement (E3 신규 타입 — LargestRect 단축 폭)"
  affects:
    - "FAIConfig Type 드롭다운 (MeasurementFactory.GetTypeNames 배열)"
tech_stack:
  added: []
  patterns:
    - "CompoundCenterCDistanceMeasurement 구조 analog (Rect ROI + Contour 4파라미터)"
    - "2 * min(len1,len2) 단축 폭 직접 계산 (intersection_contours_xld 대체)"
key_files:
  created:
    - WPF_Example/Custom/Sequence/Inspection/Measurements/CompoundShortAxisDistanceMeasurement.cs
  modified:
    - WPF_Example/Custom/Sequence/Inspection/MeasurementFactory.cs
    - WPF_Example/DatumMeasurement.csproj
decisions:
  - "E3 TypeName = CompoundShortAxisDistance (CONTEXT.md 미해결#1 확정)"
  - "IDatumOriginConsumer 미구현 — 단축 폭은 사각형 자체 기하, Datum 비의존"
  - "2 * min(length1, length2) 직접 계산 채택 — TryFindLargestContourRect 가 스칼라만 반환하므로 intersection_contours_xld 불필요. 수학적 등가, 교점 0개 위험 없음"
  - "NominalValue/Tolerance 클래스 default 미설정 — 레시피 INI 값으로 주입 (공차 0.600±0.030)"
metrics:
  duration_seconds: 131
  completed_date: "2026-05-21"
  tasks_completed: 2
  tasks_total: 2
  files_created: 1
  files_modified: 2
---

# Phase 32 Plan 05: CompoundShortAxisDistance (E3) 신규 타입 추가 Summary

**한 줄 요약:** E3 단축 폭 측정 신규 타입 CompoundShortAxisDistance — LargestRect min(len1,len2)×2×pixelResolution, Datum 비의존, Factory 등록 + csproj 빌드 포함.

## 완료된 작업

| Task | 이름 | 커밋 | 주요 파일 |
|------|------|------|-----------|
| 1 | CompoundShortAxisDistanceMeasurement.cs 신규 생성 | b2e1722 | CompoundShortAxisDistanceMeasurement.cs (신규) |
| 2 | MeasurementFactory 등록 + csproj Compile 항목 추가 | cd89ad3 | MeasurementFactory.cs, DatumMeasurement.csproj |

## 구현 상세

### Task 1: CompoundShortAxisDistanceMeasurement.cs (E3 신규)

- `MeasurementBase` 상속, `IDatumOriginConsumer` 미구현 (단축 폭 = 사각형 자체 기하)
- `TypeName` = `"CompoundShortAxisDistance"` (Factory 키, 드롭다운 노출값)
- Rect ROI 5필드: `Rect_Row/Col/Phi/Length1/Length2` — `[Category("Rect|ROI")]`
- Contour 4파라미터: `CannyAlpha=1.0`, `CannyLow=20`, `CannyHigh=40`, `UnionDistance=700.0` — `[Category("Contour")]`
- `TryExecute`: `VisionAlgorithmService.TryFindLargestContourRect` 호출 → `min(length1,length2)*2*pixelResolution`
- 공차 default 미설정 — 레시피 INI 주입 (SOP 공차 0.600±0.030)

### Task 2: Factory 등록 + csproj 등록

- `MeasurementFactory.Create` switch: `case "CompoundShortAxisDistance"` 추가 (15번째 case)
- `MeasurementFactory.GetTypeNames` 배열: 마지막 원소로 추가 (기존 콤마 처리)
- `DatumMeasurement.csproj`: CompoundCenterBDistanceMeasurement.cs 항목 다음에 신규 Compile Include 추가
- msbuild Debug/x64 PASS — 0 errors, 기존 경고 2건(MSB3884/CS0162) 유지, 신규 경고 0

## 위협 모델 검증 (T-32-08, T-32-09)

| 위협 | 검증 결과 |
|------|-----------|
| T-32-08: TypeName 불일치 | 클래스 getter / Create switch / GetTypeNames 배열 3곳 모두 `"CompoundShortAxisDistance"` byte-identical — grep 확인 PASS |
| T-32-09: csproj 미등록 → CS0246 | Compile Include 추가 + msbuild PASS로 해소 |

## 빌드 결과

msbuild Debug/x64: **PASS** (0 errors, 신규 warning 0)

## Deviations from Plan

### Auto-fixed Issues

없음 — 플랜 그대로 실행됨.

### 계획 대비 조정 사항

Plan의 acceptance criterion "grep -c 'case \"' MeasurementFactory.cs 가 14"는 기존 13개 case를 전제했으나, 실제 원본 파일에는 이미 14개 case가 있어 신규 추가 후 15개가 됨. 기존 등록 무수정 요건은 충족 (회귀 0).

## Known Stubs

없음.

## Threat Flags

없음 — 신규 타입 + Factory 등록만. 외부 신뢰 불가 입력 없음.

## Self-Check: PASSED

- [x] `WPF_Example/Custom/Sequence/Inspection/Measurements/CompoundShortAxisDistanceMeasurement.cs` 존재
- [x] 커밋 b2e1722 존재
- [x] 커밋 cd89ad3 존재
- [x] msbuild Debug/x64 PASS (0 errors)
