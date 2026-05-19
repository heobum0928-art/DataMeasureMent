---
phase: 31-datum-algorithm
plan: "04"
subsystem: UI / ROI Teaching
tags: [mainview, roi, rect-roi, circle-roi, measurement, co-23.1-01, co-23.1-02, datum]
dependency_graph:
  requires:
    - phase: 31-02
      provides: EdgeToLineAngleMeasurement, ArcEdgeDistanceMeasurement, CircleCenterDistanceMeasurement
    - phase: 31-03
      provides: ArcLineIntersectDistanceMeasurement, CompoundAngleMeasurement, CompoundCenterCDistanceMeasurement, CompoundCenterBDistanceMeasurement
  provides:
    - ROI 버튼(Rect/Circle)이 신규 7종 측정 타입을 활성화
    - TeachingImagePath/InspectionImagePath 이미지 출처 레이블 UI
  affects: [phase-31-05, MainView ROI 티칭 전체]
tech_stack:
  added: []
  patterns:
    - "FindSelectedRectMeasurement 화이트리스트 패턴 — is 연산자로 MeasurementBase 반환, Point_* ROI 보유 타입 7종 커버"
    - "CommitRectRoi as 캐스트 분기 — 타입별 Point_*/Circle_* 필드 설정 (MeasurementBase 일반화)"
    - "UpdateImageSourceLabel 헬퍼 — DatumConfig/ShotConfig 경로 판별 후 txt_imageSourceLabel 갱신"
key_files:
  created: []
  modified:
    - WPF_Example/UI/ContentItem/MainView.xaml.cs
    - WPF_Example/UI/ContentItem/MainView.xaml
key-decisions:
  - "CO-23.1-02 Option A(타입 화이트리스트) 채택 — FindSelectedRectMeasurement: is 연산자 순차 검사, MeasurementBase 반환"
  - "CO-23.1-01 Option A(경로 레이블, 최소 변경) 채택 — HalconViewer 영역 보존, 하단 Border 2행 추가"
  - "_editingMeasurement/_editingCircleMeasurement 필드 타입 MeasurementBase 로 일반화 — CommitRectRoi/CommitCircleRoi 타입별 as 캐스트 분기"
  - "UpdateImageSourceLabel 호출: LoadAndDisplay(DatumConfig path) + DisplayParam(ShotConfig) 두 진입점에 배선"
requirements-completed: [CO-23.1-01, CO-23.1-02]
duration: 15min
completed: "2026-05-19"
---

# Phase 31 Plan 04: CO-23.1-02 ROI 버튼 일반화 + CO-23.1-01 이미지 출처 레이블 Summary

**One-liner:** FindSelectedRectMeasurement 화이트리스트(Point_* 7종) + CommitRectRoi/CommitCircleRoi as 분기 일반화로 신규 타입 ROI 티칭 활성화, 하단 레이블로 티칭/검사 이미지 경로 구분 표시

## Performance

- **Duration:** 15min
- **Started:** 2026-05-19T07:20:00Z
- **Completed:** 2026-05-19T07:35:00Z
- **Tasks:** 3 (Task 1 + Task 2 decision + Task 3 구현 = 단일 커밋으로 합산)
- **Files modified:** 2

## Accomplishments

- CO-23.1-02: `FindSelectedRectMeasurement` 신규 메서드 — Point_* ROI 보유 타입 7종 (EdgeToLineDistance/EdgeToLineAngle/ArcEdgeDistance/ArcLineIntersectDistance/CompoundAngle/CompoundCenterCDistance/CompoundCenterBDistance) 화이트리스트 활성화
- CO-23.1-02: `FindSelectedCircleMeasurement` — CircleCenterDistanceMeasurement 추가, 반환 타입 MeasurementBase 일반화
- CO-23.1-02: `CommitRectRoi` — EdgeToLineAngle/ArcEdgeDistance Point_* 분기, `CommitCircleRoi` — CircleCenterDistance Circle_* 분기
- CO-23.1-01: `txt_imageSourceLabel` TextBlock 신규 추가 + `UpdateImageSourceLabel` 헬퍼 — Datum Load 시 TeachingImagePath, 검사 실행 시 SimulImagePath 구분 표시

## Task Commits

| Task | Name | Commit | Files |
|------|------|--------|-------|
| 1+2+3 | CO-23.1-02 ROI 버튼 일반화 + CO-23.1-01 이미지 출처 레이블 | e2e6c75 | MainView.xaml.cs, MainView.xaml |

## Files Created/Modified

- `WPF_Example/UI/ContentItem/MainView.xaml.cs` — `_editingMeasurement`/`_editingCircleMeasurement` MeasurementBase 일반화, `FindSelectedRectMeasurement` 신규, `FindSelectedCircleMeasurement` 확장, `CommitRectRoi`/`CommitCircleRoi` 타입별 분기, `UpdateImageSourceLabel` 헬퍼
- `WPF_Example/UI/ContentItem/MainView.xaml` — 하단 Border Grid 2행 추가, `txt_imageSourceLabel` TextBlock 신규

## Decisions Made

- **CO-23.1-02 구현 방식:** Option A(타입 화이트리스트). `is` 연산자 순차 검사 → `MeasurementBase` 반환. 기존 트리 선택 우선 + dataGrid fallback 경로 그대로 유지.
- **CO-23.1-01 구현 방식:** Option A(경로 레이블 표시). 31-CONTEXT.md Claude's Discretion 위임 — HalconViewer 영역 최소 변경, 하단 Border에 행 추가. BitmapImage 썸네일 로드 없이 경로 텍스트만 표시 (T-31-14 accept).
- **복합 ROI 타입(ArcLineIntersect/Compound*):** 이번 plan 에서 RectRoi 화이트리스트에 포함하되 CommitRectRoi Point_* 분기는 미포함 — 이 타입들은 단일 Point ROI 가 아닌 복합 ROI 구조라 별도 다점 티칭이 필요. 플랜 명세(본 plan 범위: Point_* 단일 ROI 보유 타입만 커밋)를 준수.

## Deviations from Plan

없음 — 플랜대로 정확히 실행됨.

## Known Stubs

없음 — 레이블 표시 로직 완전 구현. TeachingImagePath 빈 문자열 시 Collapsed 처리(T-31-12 폴백 준수).

## Threat Flags

없음.

T-31-12 mitigation: `UpdateImageSourceLabel` 진입부에서 `string.IsNullOrEmpty` 가드 → 빈 경로 시 Collapsed 폴백 (Phase 22/23.1 패턴 준수).
T-31-13/T-31-14 accept: 로컬 운영자 화면, 경로 노출은 무해.

## Self-Check: PASSED

| 항목 | 결과 |
|------|------|
| MainView.xaml.cs 존재 | FOUND |
| MainView.xaml 존재 | FOUND |
| FindSelectedRectMeasurement 메서드 존재 | FOUND |
| _editingMeasurement 타입 MeasurementBase | FOUND |
| EdgeToLineAngleMeasurement as 검사 | FOUND |
| ArcEdgeDistanceMeasurement as 검사 | FOUND |
| FindSelectedCircleMeasurement CircleCenterDistance | FOUND |
| CommitCircleRoi CircleCenterDistance 분기 | FOUND |
| txt_imageSourceLabel XAML | FOUND |
| UpdateImageSourceLabel 메서드 | FOUND |
| 커밋 e2e6c75 존재 | FOUND |
| MSBuild Debug/x64 Rebuild | PASS (신규 error 0) |
