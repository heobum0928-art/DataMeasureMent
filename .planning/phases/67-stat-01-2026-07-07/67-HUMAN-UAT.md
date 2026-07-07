---
status: partial
phase: 67-stat-01-2026-07-07
source: [67-VERIFICATION.md]
started: 2026-07-07
updated: 2026-07-07
---

## Current Test

[awaiting human testing]

## Tests

### 1. 메뉴 → 통계분석 비모달 동작
expected: MenuBar "통계분석" 버튼 클릭 시 StatisticsWindow 가 비모달로 열리고, 검사 진행 중에도 메인 창과 동시 조작 가능. 재클릭 시 중복 생성 없이 기존 창 포커스(ReviewerWindow 패턴 동일).
result: [pending]

### 2. MenuBar 레이아웃 회귀
expected: 5번째 컬럼(통계분석 버튼) 추가 후 헤더 TextBlock ColumnSpan 및 기존 버튼 정렬이 시각적으로 깨지지 않음.
result: [pending]

### 3. 실 검사 CSV 누적 확인
expected: 실제 검사 사이클 1회 수행 시 StatisticsSavePath\yyyyMMdd.csv 파일이 생성/append 되고, 측정항목당 1행·14컬럼 스키마·RFC4180 이스케이프가 올바름.
result: [pending]

### 4. ChartDirector 렌더 결과 육안 확인
expected: 행 선택 시 히스토그램/추이 차트가 정상 렌더(저장소 내 ChartDirector 최초 사용). USL/LSL 마크·축 표시 확인.
result: [pending]

### 5. 기간·레시피 필터 다건 데이터 확인
expected: 여러 날짜·여러 레시피가 누적된 상태에서 기간 DatePicker + 레시피 콤보 필터가 올바르게 집계(N/Mean/StdDev/Range/Cpk/OK/NG/DetectFail)를 산출.
result: [pending]

## Summary

total: 5
passed: 0
issues: 0
pending: 5
skipped: 0
blocked: 0

## Gaps
