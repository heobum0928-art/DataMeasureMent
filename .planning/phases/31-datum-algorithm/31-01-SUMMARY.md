---
phase: 31-datum-algorithm
plan: "01"
subsystem: Measurement / Halcon Algorithm
tags: [datum, interface, measurement, halcon, projection]
dependency_graph:
  requires: []
  provides: [IDatumOriginConsumer, ComputeProjectionDistance, TryFitArc, TryIntersectCircleLine]
  affects: [Action_FAIMeasurement, EdgeToLineDistanceMeasurement, VisionAlgorithmService]
tech_stack:
  added: []
  patterns:
    - IDatumOriginConsumer 인터페이스 주입 패턴 (D-03)
    - ComputeProjectionDistance 공용 거리 헬퍼 정적 메서드 (D-04)
    - TryFitArc 3점 외접원 피팅 (GenContourPolygonXld → FitCircleContourXld)
    - TryIntersectCircleLine 원-직선 교점 수학 구현 (2차 방정식 + ROI 내부 해 선택)
key_files:
  created:
    - WPF_Example/Custom/Sequence/Inspection/IDatumOriginConsumer.cs
    - .planning/phases/31-datum-algorithm/31-UAT.md
  modified:
    - WPF_Example/Halcon/Algorithms/VisionAlgorithmService.cs
    - WPF_Example/Custom/Sequence/Inspection/Measurements/EdgeToLineDistanceMeasurement.cs
    - WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs
    - WPF_Example/DatumMeasurement.csproj
decisions:
  - "D-03: IDatumOriginConsumer 인터페이스 — namespace ReringProject.Sequence, 3 double 멤버 (DatumOriginRow/Col/AngleRad)"
  - "D-04: ComputeProjectionDistance static 메서드 — EdgeToLineDistanceMeasurement L126~196 projection_pl 블록 이식"
  - "D-10: TryIntersectCircleLine ROI 중심 근접 해 선택 — 별도 파라미터 노출 없음"
  - "Task 3(C) 교체 결정: EdgeToLineDistanceMeasurement.TryExecute 내부 projection_pl 블록 유지 — 회귀 위험 방지. 신규 타입만 ComputeProjectionDistance 호출"
metrics:
  duration_minutes: 5
  completed_date: "2026-05-19"
  tasks_completed: 3
  files_changed: 6
---

# Phase 31 Plan 01: Foundation — IDatumOriginConsumer + 공용 헬퍼 + 소급 일반화 Summary

**One-liner:** IDatumOriginConsumer 인터페이스(D-03) + projection_pl 공용 거리 헬퍼(D-04) + 3점 arc 피팅 + 원-직선 교점으로 Phase 31 신규 타입 6종의 공통 기반 구축

## Tasks Completed

| Task | Name | Commit | Files |
|------|------|--------|-------|
| 1 | IDatumOriginConsumer 인터페이스 생성 + csproj 등록 | 79123b4 | IDatumOriginConsumer.cs, DatumMeasurement.csproj |
| 2 | VisionAlgorithmService 3 메서드 추가 | c7c95ff | VisionAlgorithmService.cs |
| 3 | EdgeToLineDistance 소급 + Action_FAIMeasurement 일반화 + UAT scaffold | 5ed6322 | EdgeToLineDistanceMeasurement.cs, Action_FAIMeasurement.cs, 31-UAT.md |

## What Was Built

### Task 1: IDatumOriginConsumer 인터페이스

`WPF_Example/Custom/Sequence/Inspection/IDatumOriginConsumer.cs` 신규 생성.

- namespace `ReringProject.Sequence`
- 3 double 멤버: `DatumOriginRow`, `DatumOriginCol`, `DatumAngleRad` (get/set)
- `DatumMeasurement.csproj` L226 Compile ItemGroup 등록

### Task 2: VisionAlgorithmService 3 메서드

`IntersectLines` 뒤, `AffineTransformPoint` 앞에 삽입:

1. **`ComputeProjectionDistance` (static)** — `EdgeToLineDistanceMeasurement.TryExecute` L126~196 datumOriginInjected 블록 이식. MeasureAxis X/Y 분기 + cosT≥0 정규화 + ProjectionPl + signedPx → `* pixelResolution`. `try { ... } catch { return 0.0; }`.

2. **`TryFitArc` (instance)** — 3점 외접원 피팅. `GenContourPolygonXld(3점) → FitCircleContourXld("algebraic")`. T-31-03 mitigation: `try/catch → return false`, `finally contour.Dispose()`.

3. **`TryIntersectCircleLine` (static)** — 원-직선 교점 수학 구현(2차 방정식). T-31-02 mitigation: `a < 1e-12` 가드 + `disc < 0` 가드. D-10: ROI 중심 근접 해 선택. `try { ... } catch { return false; }`.

### Task 3: 소급 일반화 + UAT scaffold

- **EdgeToLineDistanceMeasurement 소급:** 클래스 선언에 `, IDatumOriginConsumer` 추가. 기존 3 transient 필드(DatumOriginRow/Col/AngleRad)가 이미 존재하므로 인터페이스 멤버 자동 충족.

- **Action_FAIMeasurement D-03 일반화:** L161~185 `meas as EdgeToLineDistanceMeasurement` 블록 전체 제거 → `meas as IDatumOriginConsumer` 단일 분기로 교체. 신규 타입 6종이 IDatumOriginConsumer 를 구현하면 자동으로 Datum 좌표가 주입됨.

- **31-UAT.md scaffold:** Test 1~9 시나리오 생성 (E8/D1H5/I9I10/E2/E9E10/ArcEdge/CO-23.1-02/CO-23.1-01/BUILD).

- **Task 3(C) 결정:** `EdgeToLineDistanceMeasurement.TryExecute` 내부 projection_pl 블록을 `ComputeProjectionDistance` 호출로 교체하지 않음. 기존 블록은 overlay 계산(footRow/footCol)과 레거시 폴백 경로가 복합적으로 얽혀 있어 교체 시 회귀 위험이 있음. D-04 의도는 신규 타입 재사용이 우선이므로 신규 타입만 `ComputeProjectionDistance`를 호출하는 방식 채택.

## Verification

- **MSBuild Debug/x64 Rebuild:** exit 0, 신규 error/warning 0 (기존 2 warning 유지 — MSB3884 + CS0162, Phase 21 baseline 6 이하)
- `interface IDatumOriginConsumer` 파일 존재 확인
- `as IDatumOriginConsumer` Action_FAIMeasurement 존재 확인
- `as EdgeToLineDistanceMeasurement` Measure 루프 0건 확인 (Pitfall 1 해소)
- `ComputeProjectionDistance` / `TryFitArc` / `TryIntersectCircleLine` 시그니처 존재 확인
- 31-UAT.md Test 1~9 시나리오 scaffold 완료

## Deviations from Plan

### Task 3(C) 교체 보류

**[Rule 1 예방 — 회귀 위험]**

- **발견 시점:** Task 3 실행 중
- **이슈:** `EdgeToLineDistanceMeasurement.TryExecute` 의 projection_pl 블록은 overlay 생성(footRow/footCol 재사용)과 레거시 폴백 경로(datumOriginInjected=false → HomMat2dInvert 경로)가 복합적으로 얽혀 있음. `ComputeProjectionDistance`로 단순 교체하면 footRow/footCol 값이 overlay 코드에 전달되지 않아 FAI-DistLine overlay 가 소실될 가능성 있음.
- **결정:** Plan의 "교체로 인한 회귀가 의심되면 기존 블록 유지" 지침(Task 3 action C 조건부)에 따라 기존 블록 유지. 신규 타입(E8/I9·I10/ArcEdge/E9/E10)만 `ComputeProjectionDistance` 호출.
- **영향:** D-04 의도(신규 타입 재사용) 충족. EdgeToLineDistance 내부 회귀 0.

## Known Stubs

없음 — plan 01 은 인터페이스·헬퍼·인터페이스 소급 전환이 목적이며 UI 렌더링 데이터 경로 없음.

## Threat Flags

없음 — 신규 네트워크 엔드포인트, 파일 접근 패턴, 스키마 변경 없음. T-31-02/T-31-03 mitigation 코드 포함.

## Self-Check: PASSED

| 항목 | 결과 |
|------|------|
| IDatumOriginConsumer.cs 존재 | FOUND |
| 커밋 79123b4 존재 | FOUND |
| 커밋 c7c95ff 존재 | FOUND |
| 커밋 5ed6322 존재 | FOUND |
| MSBuild Debug/x64 Rebuild | PASS (신규 error 0) |
