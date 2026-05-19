---
phase: 31-datum-algorithm
plan: "03"
subsystem: Measurement / Halcon Algorithm
tags: [datum, measurement, halcon, arc, compound, angle, distance, i9, i10, e2, e9, e10]
dependency_graph:
  requires: [31-01, 31-02]
  provides: [ArcLineIntersectDistanceMeasurement, CompoundAngleMeasurement, CompoundCenterCDistanceMeasurement, CompoundCenterBDistanceMeasurement]
  affects: [MeasurementFactory, Action_FAIMeasurement, InspectionRecipeManager]
tech_stack:
  added: []
  patterns:
    - ArcLineIntersectDistanceMeasurement — TryFitLine("All")×3 에지 중점 → TryFitArc + TryFitLine + TryIntersectCircleLine + ComputeProjectionDistance (I9/I10 호∩라인 교점 거리)
    - CompoundAngleMeasurement — TryFindCircle×3 + TryFitLine("All")×2 + midline Lc + TryIntersectCircleLine×2 → Pa/Pb/Pc + AngleLineLine (E2 복합 각도)
    - CompoundCenterCDistanceMeasurement — 동일 기하 체인(CL1 없음) + ComputeProjectionDistance MeasureAxis="X" (E9 Datum C 거리)
    - CompoundCenterBDistanceMeasurement — 동일 구조 MeasureAxis="Y" (E10 Datum B 거리)
    - private TryComputeChainPoint 헬퍼 — 각 타입 독립 보유 (D-09 캡슐화)
key_files:
  created:
    - WPF_Example/Custom/Sequence/Inspection/Measurements/ArcLineIntersectDistanceMeasurement.cs
    - WPF_Example/Custom/Sequence/Inspection/Measurements/CompoundAngleMeasurement.cs
    - WPF_Example/Custom/Sequence/Inspection/Measurements/CompoundCenterCDistanceMeasurement.cs
    - WPF_Example/Custom/Sequence/Inspection/Measurements/CompoundCenterBDistanceMeasurement.cs
  modified:
    - WPF_Example/Custom/Sequence/Inspection/MeasurementFactory.cs
    - WPF_Example/DatumMeasurement.csproj
decisions:
  - "ArcLineIntersectDistance(I9/I10): 3점 arc ROI 각각 TryFitLine('All') 중점 → TryFitArc → TryFitLine → TryIntersectCircleLine(D-10: Line ROI 중심 교점 선택) → ComputeProjectionDistance, MeasureAxis 기본값 'X'"
  - "CompoundAngle(E2): CL1 TryFindCircle(Pd) + TryComputeChainPoint(CL2/CL3/La/Lb→Pc) + AngleLineLine(Pd→Pc vs DatumB ±200px), pixelResolution 미적용"
  - "CompoundCenterCDistance(E9): TryComputeChainPoint(CL2/CL3/La/Lb→Pc) → ComputeProjectionDistance MeasureAxis='X' (D-07 Datum C X 방향)"
  - "CompoundCenterBDistance(E10): 동일 구조 MeasureAxis='Y' (D-07 Datum B Y 방향, Pitfall 8 방지)"
  - "D-09: 각 타입 독립 private TryComputeChainPoint 헬퍼 — ROI 공유 없음, 중간 산출물 내부 캡슐화"
  - "T-31-08 mitigation: 체인 각 단계(TryFindCircle/TryFitLine/TryIntersectCircleLine) 실패 시 즉시 return false + error 전파"
metrics:
  duration_minutes: 8
  completed_date: "2026-05-19"
  tasks_completed: 3
  files_changed: 6
---

# Phase 31 Plan 03: 복합 신규 측정 타입 4종 (I9/I10/E2/E9/E10) Summary

**One-liner:** 3점 호 피팅+원-직선 교점(ArcLineIntersectDistance) + 다단계 기하 체인 캡슐화(CompoundAngle/CenterC/CenterB) 4종을 IDatumOriginConsumer + Plan 01 헬퍼 호출로 완결

## Tasks Completed

| Task | Name | Commit | Files |
|------|------|--------|-------|
| 1 | ArcLineIntersectDistanceMeasurement (I9/I10) 생성 + csproj 등록 | c6ba08e | ArcLineIntersectDistanceMeasurement.cs, DatumMeasurement.csproj |
| 2 | CompoundAngle(E2)/CompoundCenterC(E9)/CompoundCenterB(E10) 생성 + csproj 등록 | d5cc14d | CompoundAngleMeasurement.cs, CompoundCenterCDistanceMeasurement.cs, CompoundCenterBDistanceMeasurement.cs, DatumMeasurement.csproj |
| 3 | MeasurementFactory 4개 신규 타입 등록 + Rebuild PASS | c9af11c | MeasurementFactory.cs |

## What Was Built

### Task 1: ArcLineIntersectDistanceMeasurement (I9/I10)

`WPF_Example/Custom/Sequence/Inspection/Measurements/ArcLineIntersectDistanceMeasurement.cs` 신규 생성.

- `class ArcLineIntersectDistanceMeasurement : MeasurementBase, IDatumOriginConsumer`
- `TypeName = "ArcLineIntersectDistance"`
- ROI 필드: `Arc_P1/P2/P3` 각 5필드(Row/Col/Phi/Length1/Length2) = 15필드 + `Line_Row/Col/Phi/Length1/Length2` = 5필드
- Edge 필드 풀세트: EdgeThreshold/Sigma/EdgeSampleCount/EdgeTrimCount/EdgePolarity/EdgeDirection + 래퍼 2개
- MeasureAxis 기본값 `"X"` (I9/I10 = Datum C X 방향) + MeasureAxisList 래퍼
- IDatumOriginConsumer transient 3필드 (Browsable(false) + JsonIgnore)
- TryExecute 흐름:
  1. Arc P1/P2/P3 각 ROI TryFitLine("All") → 라인 중점(에지점)
  2. svc.TryFitArc(a1,a2,a3) → arcRow/Col/Radius (T-31-09 mitigation)
  3. svc.TryFitLine(Line_*) "All" → lr1/lc1/lr2/lc2
  4. VisionAlgorithmService.TryIntersectCircleLine(arc, line, Line_Row/Col) → intRow/Col (D-10)
  5. VisionAlgorithmService.ComputeProjectionDistance(intRow, intCol, ...) (D-04)

### Task 2: CompoundAngleMeasurement (E2) + CompoundCenterC/BDistanceMeasurement (E9/E10)

**CompoundAngleMeasurement:**
- `class CompoundAngleMeasurement : MeasurementBase, IDatumOriginConsumer`
- `TypeName = "CompoundAngle"`
- ROI 필드: CL1/CL2/CL3 각 3필드(Row/Col/Radius) + La/Lb 각 5필드(Row/Col/Phi/Length1/Length2)
- Edge 풀세트 + IDatumOriginConsumer 3필드
- private `TryComputeChainPoint` 헬퍼: CL2/CL3 TryFindCircle + La/Lb TryFitLine("All") + midline Lc(4점 평균+단위벡터 합 방향) + TryIntersectCircleLine×2 → Pa/Pb → Pc=midpoint
- TryExecute: CL1 TryFindCircle(Pd) + TryComputeChainPoint(Pc) + Datum B 기준선 ±200px + AngleLineLine(Pd→Pc, daR1/C1~daR2/C2)

**CompoundCenterCDistanceMeasurement:**
- `class CompoundCenterCDistanceMeasurement : MeasurementBase, IDatumOriginConsumer`
- `TypeName = "CompoundCenterCDistance"`
- ROI 필드: CL2/CL3 + La/Lb (CL1 없음 — E9 에는 Pd 불필요)
- MeasureAxis 기본값 `"X"` (Datum C X 방향, D-07)
- private `TryComputeChainPoint` 헬퍼 (CompoundAngle 과 동일 코드 독립 보유, D-09)
- TryExecute: TryComputeChainPoint(Pc) → ComputeProjectionDistance(Pc, MeasureAxis="X")

**CompoundCenterBDistanceMeasurement:**
- CompoundCenterCDistanceMeasurement 와 구조 완전 동일
- `TypeName = "CompoundCenterBDistance"`, MeasureAxis 기본값 `"Y"` (Datum B Y 방향, D-07, Pitfall 8 방지)

### Task 3: MeasurementFactory 등록

`MeasurementFactory.cs` Create switch + GetTypeNames 배열 동시 추가 (Pitfall 6 준수):

```csharp
case "ArcLineIntersectDistance":  // D-01 I9/I10
case "CompoundAngle":              // D-11 E2
case "CompoundCenterCDistance":    // D-11 E9
case "CompoundCenterBDistance":    // D-11 E10
```

GetTypeNames 배열: 기존 10개 원소 뒤에 4개 append → 총 14개.

Phase 31 신규 타입 7종 전체:
- Plan 02: CircleCenterDistance / EdgeToLineAngle / ArcEdgeDistance (3종)
- Plan 03: ArcLineIntersectDistance / CompoundAngle / CompoundCenterCDistance / CompoundCenterBDistance (4종)

MSBuild Debug/x64 Rebuild exit 0, 신규 error/warning 0 (기존 2 warning: MSB3884 + CS0162 유지).

## Verification

- `ArcLineIntersectDistanceMeasurement.cs`: `class ArcLineIntersectDistanceMeasurement : MeasurementBase, IDatumOriginConsumer` 존재
- `CompoundAngleMeasurement.cs`: `class CompoundAngleMeasurement : MeasurementBase, IDatumOriginConsumer` 존재
- `CompoundCenterCDistanceMeasurement.cs`: `class CompoundCenterCDistanceMeasurement : MeasurementBase, IDatumOriginConsumer` 존재
- `CompoundCenterBDistanceMeasurement.cs`: `class CompoundCenterBDistanceMeasurement : MeasurementBase, IDatumOriginConsumer` 존재
- 4개 파일 csproj Compile Include 등록 확인
- MeasurementFactory switch 4 case + GetTypeNames 4 항목 확인
- 모든 TryFitLine 호출 selection 인자 리터럴 `"All"` 확인
- MSBuild Debug/x64 Rebuild PASS (신규 error 0, warning 0)

## Deviations from Plan

None — 계획대로 정확히 실행됨.

## Known Stubs

없음 — 4개 신규 측정 클래스 모두 TryExecute 완전 구현. overlay는 빈 리스트 (본 plan 범위 밖 — plan 명시).

## Threat Flags

없음 — 신규 네트워크 엔드포인트, 파일 접근 패턴, 스키마 변경 없음.

T-31-08 mitigation: TryComputeChainPoint 각 단계(TryFindCircle/TryFitLine/TryIntersectCircleLine) 실패 시 즉시 return false + error 전파. 중간 실패가 측정 실패로 안전 종결.
T-31-09 mitigation: TryFitArc try/catch → return false. TryExecute 가 false 시 측정 실패 처리.
T-31-10 mitigation: TryIntersectCircleLine disc<0 시 return false — 잘못된 ROI 입력이 NaN 전파 없이 측정 실패로 귀결.

## Self-Check: PASSED

| 항목 | 결과 |
|------|------|
| ArcLineIntersectDistanceMeasurement.cs 존재 | FOUND |
| CompoundAngleMeasurement.cs 존재 | FOUND |
| CompoundCenterCDistanceMeasurement.cs 존재 | FOUND |
| CompoundCenterBDistanceMeasurement.cs 존재 | FOUND |
| csproj 4파일 Compile Include | FOUND |
| MeasurementFactory switch 4 case | FOUND |
| MeasurementFactory GetTypeNames 4 항목 | FOUND |
| 커밋 c6ba08e 존재 | FOUND |
| 커밋 d5cc14d 존재 | FOUND |
| 커밋 c9af11c 존재 | FOUND |
| MSBuild Debug/x64 Rebuild | PASS (신규 error 0) |
