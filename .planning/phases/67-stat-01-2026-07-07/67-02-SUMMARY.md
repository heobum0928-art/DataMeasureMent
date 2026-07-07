---
phase: 67-stat-01-2026-07-07
plan: 02
subsystem: database
tags: [csv, statistics, query, repeat-measurement-stats, hungarian-io]

# Dependency graph
requires: ["67-01: StatisticsSavePath\\yyyyMMdd.csv 14컬럼 스키마"]
provides:
  - "MeasurementHistoryCsvLoader.Query(from,to,recipeFilter)→StatisticsQueryResult — 기간·레시피 조회 API"
  - "StatisticsQueryResult DTO(Stats/Series/RecipeNames/TotalRowCount)"
affects: [67-03]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "RFC4180 CSV 파서(따옴표/콤마/개행 이스케이프 해제) — writer 의 Esc() 의 역"
    - "최소 CycleResultDto 래핑 후 기존 RepeatMeasurementStats.AddSample/ComputeAll 재사용(DRY, 통계 로직 재구현 0)"
    - "파일 단위 + 조회 단위 이중 try/catch 격리 — 손상 파일 1개가 전체 Query 를 중단시키지 않음(T-67-04)"

key-files:
  created:
    - WPF_Example/Custom/Sequence/Inspection/MeasurementHistoryCsvLoader.cs
  modified:
    - WPF_Example/DatumMeasurement.csproj

key-decisions:
  - "RepeatMeasurementStats.cs 는 무수정 — CSV 행을 최소 CycleResultDto(1 shot/1 fai/1 meas) 로 감싸 AddSample 을 그대로 호출하여 Cpk/DetectFail 정책을 100% 동일하게 재사용(D-07)"
  - "Series(추이 시계열)는 RepeatMeasurementStats 가 제공하지 않는 순서유지 원시값이므로 loader 가 별도로 Shot/FAI/측정명 키(RepeatMeasurementStats 와 동일 포맷)로 수집(D-13) — Stats↔Series 는 동일 키로 UI 조인 가능"
  - "distinct RecipeNames 는 필터 적용 **전** recipeSet.Add 로 전체 수집 — 드롭다운이 현재 필터와 무관하게 전체 후보를 보여줄 수 있도록(D-11)"
  - "Judgement 컬럼 역매핑은 5분기 if/else(DATUM_FAIL/NO_IMAGE/NO_RESULT/OK/NG)로 MeasurementHistoryCsvWriter.MapJudgement 의 역함수를 구현 — 왕복 검증(round-trip) 일치"

patterns-established: []

requirements-completed: [STAT-01]

# Metrics
duration: 14min
completed: 2026-07-07
---

# Phase 67 Plan 02: 양산 이력 통계 분석 — 조회/집계 계층 Summary

**MeasurementHistoryCsvLoader.Query(from,to,recipeFilter) 가 기간별 CSV를 RFC4180 파싱 후 기존 RepeatMeasurementStats 재사용으로 통계(N/Mean/StdDev/Range/Cpk/OK/NG/DetectFail) + 순서유지 추이 시계열 + distinct 레시피 목록을 한 번에 반환하는 조회 계층**

## Performance

- **Duration:** 약 14분
- **Started:** 2026-07-07T01:19:53Z
- **Completed:** 2026-07-07T01:23:14Z
- **Tasks:** 2/2
- **Files modified:** 2 (1 신규 + 1 수정)

## Accomplishments
- `MeasurementHistoryCsvLoader.Query` 신규 정적 API — 기간(from~to) 일자별 `yyyyMMdd.csv` 파일 존재 검사 후 순차 로드
- RFC4180 CSV 라인 파서(`ParseCsvLine`) — 따옴표 내부 콤마/개행 무시, `""`→`"` 역이스케이프 (writer 의 `Esc()` 역함수)
- 각 CSV 행을 최소 `CycleResultDto`(1 shot/1 fai/1 meas)로 감싸 기존 `RepeatMeasurementStats.AddSample`/`ComputeAll` 을 그대로 재사용 — 통계 로직 재구현 0, Cpk/검출실패 정책 100% 일치(D-07)
- `BuildMeasFromRow` — Judgement 컬럼(DATUM_FAIL/NO_IMAGE/NO_RESULT/OK/NG) 5분기 역매핑으로 `LastSkipReason`/`LastHasResult`/`LastJudgement` 복원
- `StatisticsQueryResult.Series` — OK/NG(측정값 있는 행)만 Shot/FAI/측정명 키(RepeatMeasurementStats 와 동일 포맷)로 파일·행 순서 유지하며 순서 유지 원시값 수집(D-13, 추이 차트용)
- `StatisticsQueryResult.RecipeNames` — 필터 적용 **전** distinct 레시피 전체 수집(D-11, UI 드롭다운용)
- 파일 단위(`LoadFile`) + 조회 단위(`Query`) 이중 try/catch 격리 — 손상/누락 파일이 있어도 나머지 파일 로드 지속(T-67-04)
- msbuild Debug/x64 빌드 PASS (error 0, 기존 경고만 존재), 변경 파일 2개(신규 1 + csproj 2줄 추가) — RepeatMeasurementStats.cs / CycleResultDto.cs 무수정, 회귀 0

## Task Commits

Each task was committed atomically:

1. **Task 1: MeasurementHistoryCsvLoader + StatisticsQueryResult 신규 생성 (D-06/D-07/D-11/D-13)** - `fc00c6b` (feat)
2. **Task 2: csproj 등록 + 빌드 검증 (Debug/x64) + 회귀 0** - `495951c` (chore)

**Plan metadata:** (다음 커밋에서 STATE/ROADMAP 과 함께 기록됨)

## Files Created/Modified
- `WPF_Example/Custom/Sequence/Inspection/MeasurementHistoryCsvLoader.cs` - 신규. Query API + StatisticsQueryResult DTO + RFC4180 파서 + RepeatMeasurementStats 재사용 집계 + 추이 시계열 수집
- `WPF_Example/DatumMeasurement.csproj` - MeasurementHistoryCsvLoader.cs Compile Include 등록

## Decisions Made
- 플랜에 명시된 D-06/D-07/D-11/D-13 잠금 결정을 그대로 구현. 별도 아키텍처 판단 불필요 (Rule 4 트리거 없음).
- RepeatMeasurementStats.cs 는 계획대로 완전 무수정 — 재사용만 수행.

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered

None. 67-01 이 확정한 CSV 스키마(14컬럼, 컬럼 순서/헤더 토큰)가 이번 로더의 상수/인덱스와 정확히 일치하여 별도 조정 없이 그대로 구현되었다.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness
- Plan 03(UI)이 `MeasurementHistoryCsvLoader.Query(from,to,recipeFilter)` 한 번 호출로 통계 테이블(Stats) + 추이 차트 데이터(Series) + 레시피 필터 드롭다운(RecipeNames)을 모두 얻을 수 있는 단일 조회 API 준비 완료.
- Stats/Series 의 키 포맷(`ShotName + "/" + FAIName + "/" + MeasurementName`)이 동일하므로 UI 에서 두 데이터를 직접 조인 가능.
- 실 데이터 조회 육안 UAT(파일 여러 개 누적된 상태에서 Query 결과 확인)는 Plan 03 UI 완성 후 함께 수행 권장.

---
*Phase: 67-stat-01-2026-07-07*
*Completed: 2026-07-07*

## Self-Check: PASSED

All created/modified files and both task commit hashes (fc00c6b, 495951c) verified present.
