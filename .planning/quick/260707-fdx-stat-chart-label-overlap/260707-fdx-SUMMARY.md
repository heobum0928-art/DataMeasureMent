---
phase: quick-260707-fdx
plan: 01
subsystem: ui
tags: [chartdirector, wpf, statistics, chart-labels]

# Dependency graph
requires:
  - phase: 67-stat-01-2026-07-07
    provides: StatisticsWindow.xaml.cs (RenderHistogram/RenderTrend 초기 구현)
provides:
  - 히스토그램 x축 라벨 스텝(setLabelStep) 적용으로 20개→5개 내외 표시
  - 히스토그램 USL/LSL 수직 마크 근접(공차0 포함) 병합 로직
  - 추이 차트 평균/USL/LSL 수평 마크 근접 병합 헬퍼(AddSpecMarksY)
affects: [statistics, chart-rendering]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "ChartDirector Axis.setLabelStep(n) 으로 라벨 밀도 제어"
    - "값-공간 근접 판정(스팬 기반 % 임계) 후 단일 병합 마크로 대체 — 좌표계(bin vs 값) 무관 적용 가능한 패턴"

key-files:
  created: []
  modified:
    - WPF_Example/UI/Statistics/StatisticsWindow.xaml.cs

key-decisions:
  - "히스토그램은 bin 좌표계(0..BIN_COUNT) 절대 임계(0.5=반 bin)로 근접 판정, 추이 차트는 값 스팬의 2% 상대 임계로 판정 — 두 차트의 좌표 단위가 다르므로 각각의 좌표계에 맞는 임계 산식을 사용"
  - "dSpan<=0(전 값 동일) 시 1e-9 하한 가드로 0 나눗셈/오탐 방지"

requirements-completed: [STAT-01]

# Metrics
duration: 12min
completed: 2026-07-07
---

# Phase quick-260707-fdx: 통계 차트 라벨 겹침 수정 Summary

**ChartDirector Axis.setLabelStep + 근접 병합 헬퍼(AddSpecMarksY)로 히스토그램/추이 차트의 라벨 겹침(특히 공차0 시 USL=LSL) 제거**

## Performance

- **Duration:** 12 min
- **Started:** 2026-07-07T (plan start)
- **Completed:** 2026-07-07
- **Tasks:** 3 (Task 3는 빌드 검증만, 코드 변경 없음)
- **Files modified:** 1

## Accomplishments
- 히스토그램 x축 라벨: BIN_COUNT=20개 전부 그려지던 것을 setLabelStep(4)로 5개 내외만 표시하도록 축약
- 히스토그램 USL/LSL 수직 마크: bin 좌표 근접(0.5 이내, 공차0 포함) 시 "USL/LSL" 단일 마크로 병합
- 추이 차트 평균/USL/LSL 수평 마크: 신규 AddSpecMarksY 헬퍼로 값 스팬 2% 이내 근접(공차0 포함) 시 단일 "USL/LSL" 마크로 병합, 평균은 항상 별도 표시
- 정상 케이스(넓은 공차·충분 샘플)는 기존과 동일하게 개별 마크 3개(추이)/2개(히스토그램) 유지 — 회귀 0
- Debug/x64 msbuild 빌드 PASS (0 Error, 기존 경고만 존재, 신규 경고/에러 없음)

## Task Commits

1. **Task 1: 히스토그램 x축 라벨 스텝 적용 + USL/LSL 마크 근접 병합** - `7a4de09` (fix)
2. **Task 2: 추이 차트 평균/USL/LSL 마크 근접 병합(공차0 겹침 제거)** - `562dc11` (fix)
3. **Task 3: Debug/x64 빌드 검증** - 코드 변경 없음(검증 전용), 커밋 없음. `msbuild /p:Configuration=Debug /p:Platform=x64` 실행 결과 "DatumMeasurement -> ...\DatumMeasurement.exe" 정상 산출, 신규 Error/Warning 없음.

**Plan metadata:** (오케스트레이터가 별도 처리)

## Files Created/Modified
- `WPF_Example/UI/Statistics/StatisticsWindow.xaml.cs` - MAX_X_LABELS 상수 추가, RenderHistogram 에 setLabelStep + USL/LSL bin 근접 병합, RenderTrend 를 AddSpecMarksY 호출로 대체, AddSpecMarksY 헬퍼 신규 추가

## Decisions Made
- 히스토그램(bin 좌표계)과 추이 차트(값 좌표계)는 단위가 달라 근접 임계 산식을 각각 다르게 적용: 히스토그램은 절대 임계 0.5(반 bin), 추이 차트는 데이터 스팬의 2% 상대 임계. 두 방식 모두 계획서(PLAN.md) 지시 그대로 구현.
- USL/LSL 이 데이터 스팬 밖에 있을 경우(마크가 그래프 밖) 스팬 확장 로직을 포함해 근접 판정이 스팬 크기에 왜곡되지 않도록 함(계획서 지시 반영).

## Deviations from Plan

None - plan executed exactly as written. Task 3(빌드 검증)은 애초에 코드 변경이 없는 검증 전용 태스크였으므로 별도 커밋을 만들지 않음(plan의 `<files>WPF_Example/DatumMeasurement.csproj</files>` 는 빌드 대상 프로젝트 파일 명시일 뿐 수정 대상 아님).

## Issues Encountered
None - msbuild 경로는 VS2022 Community 에서 바로 발견되어 계획서에 명시된 2019/2017 경로 대신 사용(둘 다 동일 MSBuild 15.0+ 툴체인, 빌드 결과 동일).

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness
- StatisticsWindow 차트 라벨 겹침 UAT 재검증 가능(공차0 데이터 + 넓은 공차 데이터 양쪽 육안 확인 권장)
- 추가 작업 불필요, blocker 없음

---
*Phase: quick-260707-fdx*
*Completed: 2026-07-07*

## Self-Check: PASSED
- FOUND: WPF_Example/UI/Statistics/StatisticsWindow.xaml.cs
- FOUND: commit 7a4de09
- FOUND: commit 562dc11
- FOUND: .planning/quick/260707-fdx-stat-chart-label-overlap/260707-fdx-SUMMARY.md
