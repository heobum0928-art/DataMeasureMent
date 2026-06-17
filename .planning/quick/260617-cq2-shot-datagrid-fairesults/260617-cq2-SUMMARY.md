---
quick_id: 260617-cq2
slug: shot-datagrid-fairesults
date: 2026-06-17
status: complete
commits:
  - 44a9575
---

# Quick Task 260617-cq2 — Summary

## 문제
일괄 검사(체크박스 다중 SHOT)를 실행해도 결과 리스트박스(`dataGrid_faiResults`)에
측정 결과가 표시되지 않음. 단일 RUN 은 정상.

## 원인 (Phase 51 설계 갭)
- `dataGrid_faiResults` 는 `InspectionViewModel.MeasurementResults` 에 바인딩되어
  **현재 선택된 단일 노드(FAI/Shot)** 의 측정만 행으로 빌드(`OnFAISelected`/`OnActionSelected`).
- 검사 후 갱신(`RefreshFAIResultRows`)은 **기존 행만** `row.Refresh()` — 새 행을 안 만듦.
- 일괄 검사(`Btn_batchRun_Click` → `BatchRunService`)는 완료 시 `_batchAccumulated` 에
  DTO 만 누적해 **xlsx Export 용으로만** 사용 — 그리드를 전혀 안 건드림.
- → 라이브 그리드 표시는 Phase 51 범위 밖이었음(Export 로만 UAT 검증).

## 수정
1. `InspectionViewModel.ShowMeasurementsForShots(List<ShotConfig>)` 신규 —
   `OnActionSelected` 의 단일-shot 평탄화를 다중-shot 으로 확장. 모든 shot 의
   FAI × Measurement 를 `MeasurementResultRow` 행으로 `MeasurementResults` 교체.
2. `InspectionListView`:
   - 필드 `_batchShots` 추가
   - `Btn_batchRun_Click` 에서 체크된 ShotConfig 수집(인덱스 해석 성공분과 동기)
   - `OnBatchComplete` Dispatcher 블록에서 `_inspectionVm.ShowMeasurementsForShots(_batchShots)` 호출

행은 live `MeasurementBase` 를 래핑하므로 검사 후 `LastMeasuredValue`/판정이 즉시 반영됨.

## 회귀 안전성
- `FAIName` 순수 유지 — 동일-명 FAI 는 MainView 가 `SourceMeasurement` ReferenceEquals
  (`MainView.xaml.cs:292`)로 소유 FAI 해석 → ROI 하이라이트/편집 회귀 0.
- 단일 RUN 경로 무변경. Export/누적 로직 무변경.

## 검증
- msbuild Debug/x64 0 errors.
- 커밋: 44a9575 (code), docs(quick-260617-cq2) (artifacts).

## 잔여 (SIMUL UAT 대기)
체크 다중 SHOT 일괄 검사 → 결과 그리드에 전체 측정값/판정 표시 육안 확인 필요.

## Out of scope
- SHOT 명 컬럼 신설(현 컬럼으로 식별 충분, 회귀 회피)
