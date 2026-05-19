---
phase: 31-datum-algorithm
plan: "02"
subsystem: Measurement / Halcon Algorithm
tags: [datum, measurement, halcon, circle, angle, arc, e8, d1, h5, g-series]
dependency_graph:
  requires: [31-01]
  provides: [CircleCenterDistanceMeasurement, EdgeToLineAngleMeasurement, ArcEdgeDistanceMeasurement]
  affects: [MeasurementFactory, Action_FAIMeasurement, InspectionRecipeManager]
tech_stack:
  added: []
  patterns:
    - CircleCenterDistanceMeasurement — TryFindCircle + ComputeProjectionDistance (E8 원중심 거리)
    - EdgeToLineAngleMeasurement — TryFitLine("All") + AngleLineLine + Datum 기준선 2점 구성 (D1/H5 각도)
    - ArcEdgeDistanceMeasurement — TryFitLine("All") + ComputeProjectionDistance (G 시리즈 호 에지 거리)
key_files:
  created:
    - WPF_Example/Custom/Sequence/Inspection/Measurements/CircleCenterDistanceMeasurement.cs
    - WPF_Example/Custom/Sequence/Inspection/Measurements/EdgeToLineAngleMeasurement.cs
    - WPF_Example/Custom/Sequence/Inspection/Measurements/ArcEdgeDistanceMeasurement.cs
  modified:
    - WPF_Example/Custom/Sequence/Inspection/MeasurementFactory.cs
    - WPF_Example/DatumMeasurement.csproj
decisions:
  - "CircleCenterDistanceMeasurement(E8): TryFindCircle → ComputeProjectionDistance, MeasureAxis 기본값 Y (Datum B Y 방향)"
  - "EdgeToLineAngleMeasurement(D1/H5): TryFitLine('All') → AngleLineLine, Datum 기준선 ±200px 2점 구성"
  - "ArcEdgeDistanceMeasurement(G시리즈): TryFitLine('All') 라인 중점 → ComputeProjectionDistance, MeasureAxis 기본값 X (Datum C X)"
  - "ArcEdgeDistanceMeasurement overlay: FAI-Edge1 + FAI-DistLine (HomMat2dInvert 폴백 경로, EdgeToLineDistance 동일)"
  - "EdgeToLineAngleMeasurement overlay: 빈 리스트 (각도 타입 — 시각화 불필요, LineToLineAngle 동일 패턴)"
metrics:
  duration_minutes: 4
  completed_date: "2026-05-19"
  tasks_completed: 3
  files_changed: 5
---

# Phase 31 Plan 02: 신규 측정 타입 3종 (E8/D1·H5/G 시리즈) Summary

**One-liner:** CircleCenterDistance(E8 원중심→Datum 거리) + EdgeToLineAngle(D1/H5 Datum A 기준 각도) + ArcEdgeDistance(G 시리즈 호 에지→Datum 거리) 3종을 IDatumOriginConsumer 구현 + Plan 01 공용 헬퍼 호출로 완결

## Tasks Completed

| Task | Name | Commit | Files |
|------|------|--------|-------|
| 1 | CircleCenterDistanceMeasurement (E8) 생성 + csproj 등록 | 8bbaa81 | CircleCenterDistanceMeasurement.cs, DatumMeasurement.csproj |
| 2 | EdgeToLineAngle(D1/H5) + ArcEdgeDistance(G) 생성 + csproj 등록 | 8771f6b | EdgeToLineAngleMeasurement.cs, ArcEdgeDistanceMeasurement.cs, DatumMeasurement.csproj |
| 3 | MeasurementFactory 3개 신규 타입 등록 + 빌드 검증 | 85d65db | MeasurementFactory.cs |

## What Was Built

### Task 1: CircleCenterDistanceMeasurement (E8)

`WPF_Example/Custom/Sequence/Inspection/Measurements/CircleCenterDistanceMeasurement.cs` 신규 생성.

- `class CircleCenterDistanceMeasurement : MeasurementBase, IDatumOriginConsumer`
- `TypeName = "CircleCenterDistance"`
- ROI 필드: `Circle_Row/Col/Radius` (Category "Circle|ROI")
- Edge 필드: `EdgeThreshold=10 / Sigma=1.0 / EdgePolarity="DarkToLight" + EdgePolarityList 래퍼`
- MeasureAxis 필드: 기본값 `"Y"` (E8 = Datum B Y 방향) + MeasureAxisList 래퍼
- IDatumOriginConsumer transient 3필드: `DatumOriginRow/Col/AngleRad` ([Browsable(false)] + [JsonIgnore])
- TryExecute: `TryFindCircle → ComputeProjectionDistance(D-04 공용 헬퍼)`
- csproj Compile Include 등록

### Task 2: EdgeToLineAngleMeasurement (D1/H5) + ArcEdgeDistanceMeasurement (G)

**EdgeToLineAngleMeasurement:**
- `class EdgeToLineAngleMeasurement : MeasurementBase, IDatumOriginConsumer`
- `TypeName = "EdgeToLineAngle"`
- ROI 필드: `Point_Row/Col/Phi/Length1/Length2` (Category "Point|ROI")
- Edge 필드 풀세트: EdgeThreshold/Sigma/EdgeSampleCount=20/EdgeTrimCount=10/EdgePolarity/EdgeDirection + 래퍼 2개
- IDatumOriginConsumer transient 3필드
- TryExecute: `TryFitLine("All")` → Datum A 기준선 ±200px 2점 구성 → `AngleLineLine` → degree 반환
- EdgeSelection "All" 고정 (memory feedback 준수)

**ArcEdgeDistanceMeasurement:**
- `class ArcEdgeDistanceMeasurement : MeasurementBase, IDatumOriginConsumer`
- `TypeName = "ArcEdgeDistance"`
- ROI/Edge 필드: EdgeToLineDistanceMeasurement 완전 동일 구조
- MeasureAxis 기본값 `"X"` (G 시리즈 = Datum C X 방향)
- TryExecute: `TryFitLine("All")` → 라인 중점 계산 → `ComputeProjectionDistance` → FAI-Edge1 + FAI-DistLine overlay

### Task 3: MeasurementFactory 등록

`MeasurementFactory.cs` Create switch + GetTypeNames 배열 동시 추가 (Pitfall 6 준수):

```csharp
case "CircleCenterDistance":  // D-01 E8
case "EdgeToLineAngle":       // D-05
case "ArcEdgeDistance":       // D-08
```

GetTypeNames 배열: 기존 7개 원소 뒤에 3개 append → 총 10개.

MSBuild Debug/x64 Rebuild exit 0, 신규 error/warning 0 (기존 2 warning: MSB3884 + CS0162 유지).

## Verification

- `CircleCenterDistanceMeasurement.cs`: `class CircleCenterDistanceMeasurement : MeasurementBase, IDatumOriginConsumer` 존재 확인
- `EdgeToLineAngleMeasurement.cs`: `class EdgeToLineAngleMeasurement : MeasurementBase, IDatumOriginConsumer` 존재 확인
- `ArcEdgeDistanceMeasurement.cs`: `class ArcEdgeDistanceMeasurement : MeasurementBase, IDatumOriginConsumer` 존재 확인
- 3개 파일 csproj Compile Include 등록 확인
- MeasurementFactory switch 3 case + GetTypeNames 3 항목 확인
- 모든 TryFitLine 호출 selection 인자 리터럴 `"All"` 확인
- MSBuild Debug/x64 Rebuild PASS (신규 error 0, warning 0)

## Deviations from Plan

None — 계획대로 정확히 실행됨.

## Known Stubs

없음 — 3개 신규 측정 클래스 모두 TryExecute 완전 구현. overlay는 EdgeToLineAngleMeasurement만 빈 리스트 (각도 타입은 시각화 선이 없음 — LineToLineAngleMeasurement 동일 정책).

## Threat Flags

없음 — 신규 네트워크 엔드포인트, 파일 접근 패턴, 스키마 변경 없음.
T-31-05 mitigation: VisionAlgorithmService.TryFindCircle/TryFitLine 기존 try/catch→false 보장, TryExecute가 false 반환 시 측정 실패로 처리.
T-31-06 accept: ComputeProjectionDistance의 비"X" 입력 Y 폴백 — 로컬 운영자 INI 조작 시 측정 오류일 뿐 보안 영향 없음.

## Self-Check: PASSED

| 항목 | 결과 |
|------|------|
| CircleCenterDistanceMeasurement.cs 존재 | FOUND |
| EdgeToLineAngleMeasurement.cs 존재 | FOUND |
| ArcEdgeDistanceMeasurement.cs 존재 | FOUND |
| csproj 3파일 Compile Include | FOUND |
| MeasurementFactory switch 3 case | FOUND |
| MeasurementFactory GetTypeNames 3 항목 | FOUND |
| 커밋 8bbaa81 존재 | FOUND |
| 커밋 8771f6b 존재 | FOUND |
| 커밋 85d65db 존재 | FOUND |
| MSBuild Debug/x64 Rebuild | PASS (신규 error 0) |
