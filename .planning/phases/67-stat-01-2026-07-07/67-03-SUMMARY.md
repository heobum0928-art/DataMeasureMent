---
phase: 67-stat-01-2026-07-07
plan: 03
subsystem: ui
tags: [wpf, chartdirector, statistics-ui, datagrid, non-modal-window]

# Dependency graph
requires:
  - phase: "67-02"
    provides: "MeasurementHistoryCsvLoader.Query(from,to,recipeFilter) → StatisticsQueryResult(Stats/Series/RecipeNames)"
provides:
  - "StatisticsWindow — 비모달 통계 조회 UI (기간+레시피 필터, 통계 테이블, ChartDirector 히스토그램/추이)"
  - "EPageType.Statistics 메뉴 진입점 (MenuBar '통계분석' 버튼 → MainWindow.PopupView)"
affects: []

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "ChartDirector.Net(netchartdir) XYChart 최초 in-repo 사용처 — addBarLayer/addLineLayer/Axis.addMark/setLabels/WPFChartViewer.Chart 확정 API로 렌더"
    - "ReviewerWindow 완전 미러 — 비모달 Window + mXxxWindow 재사용 멤버 + PopupView switch 분기 패턴을 신규 창에도 그대로 적용"

key-files:
  created:
    - WPF_Example/UI/Statistics/StatisticsWindow.xaml
    - WPF_Example/UI/Statistics/StatisticsWindow.xaml.cs
  modified:
    - WPF_Example/MainWindow.xaml.cs
    - WPF_Example/UI/MenuBar.xaml
    - WPF_Example/UI/MenuBar.xaml.cs
    - WPF_Example/DatumMeasurement.csproj

key-decisions:
  - "D-08~D-14 잠금 결정을 그대로 구현: ReviewerWindow 완전 미러(비모달/재사용), 기본 오늘, 행클릭→차트, 히스토그램 bin=20 고정"
  - "CpkText/DefectRateText 는 화면 전용 파생 문자열로 StatRow 에 보관 — MeasurementStat(도메인 모델) 은 무수정"
  - "히스토그램 USL/LSL 은 bin 인덱스로 환산해 xAxis().addMark 에 전달 — addBarLayer 기본 x축(0..N-1 정수 스케일)과 정합"

patterns-established: []

requirements-completed: [STAT-01]

# Metrics
duration: 25min
completed: 2026-07-07
---

# Phase 67 Plan 03: 양산 이력 통계 분석 — UI 계층 Summary

**비모달 StatisticsWindow(ReviewerWindow 미러) 신설 — 기간·레시피 필터 + N/Mean/StdDev/Range/Cpk/OK/NG/불량률 통계 테이블 + ChartDirector 히스토그램(USL/LSL)·추이(평균/USL/LSL) 차트, 메뉴 "통계분석" 버튼으로 진입**

## Performance

- **Duration:** 약 25분
- **Started:** 2026-07-07T02:05:00Z
- **Completed:** 2026-07-07T02:30:00Z
- **Tasks:** 3/3
- **Files modified:** 6 (2 신규 + 4 수정)

## Accomplishments
- `StatisticsWindow` 신규 비모달 창 — 기간 DatePicker(기본 오늘, D-10) + 레시피 콤보("전체"+distinct, D-11) + [조회] 버튼
- `MeasurementHistoryCsvLoader.Query` 소비 → DataGrid 통계 테이블(Shot/FAI/측정명/N/평균/표준편차/범위/Cpk/OK/NG/검출실패/불량률, D-06)
- `Grid_Stats_SelectionChanged` — 행 클릭 시 선택 측정키(Series)로 히스토그램(BarLayer, bin=20 고정, USL/LSL 수직선)과 추이(LineLayer, x=샘플 인덱스, 평균/USL/LSL 수평선) 동시 갱신(D-12/D-13/D-14)
- ChartDirector.Net(netchartdir) 최초 in-repo 사용 — `packages/ChartDirector.Net.7.1.0/lib/net40/netchartdir.xml` 로 `XYChart.setPlotArea/addBarLayer/addLineLayer`, `Axis.addMark/setLabels`, `WPFChartViewer.Chart` 정확한 시그니처를 사전 확인 후 그대로 구현 — 빌드 1회 오류(ELogType 네임스페이스)만 발생, 차트 API 자체는 무오류
- `CpkText`(∞/NaN if/else 방어) / `DefectRateText`(분모 0 if/else 방어) 파생 문자열 — 삼항 연산자 미사용
- `EPageType.Statistics` + `mStatisticsWindow` 멤버 + `PopupView` switch 분기 — `EPageType.Reviewer` 케이스를 그대로 미러(비모달 Show/IsLoaded 재사용, D-08)
- `MenuBar.xaml` "통계분석" 버튼(부모 그리드 5번째 컬럼 추가, 헤더 ColumnSpan 4→5 동반 수정) + `Button_Statistics_Click` 핸들러(D-09)
- msbuild Debug/x64 `Build succeeded` (error 0, 기존 경고만), `ReviewerWindow`/검사·Align 시퀀스 코드 무변경 — 회귀 0

## Task Commits

Each task was committed atomically:

1. **Task 1: StatisticsWindow 신규 생성 (xaml + xaml.cs) — 조회/테이블/차트 (D-10~D-14)** - `c2cbf43` (feat)
2. **Task 2: 메뉴 진입점 — EPageType.Statistics + PopupView 분기 + MenuBar "통계분석" 버튼 (D-08/D-09)** - `57eb9ba` (feat)
3. **Task 3: csproj 등록(StatisticsWindow xaml/cs) + 빌드 검증(Debug/x64) + 회귀 0** - `31f5135` (chore)

**Plan metadata:** (다음 커밋에서 STATE/ROADMAP 과 함께 기록됨)

## Files Created/Modified
- `WPF_Example/UI/Statistics/StatisticsWindow.xaml` - 신규. 기간 필터바 + DataGrid + WPFChartViewer 2개 레이아웃
- `WPF_Example/UI/Statistics/StatisticsWindow.xaml.cs` - 신규. DoQuery/BuildRows/CpkToText/DefectRateToText/Grid_Stats_SelectionChanged/RenderHistogram/RenderTrend/BuildHistogramBins + StatRow 화면 모델
- `WPF_Example/MainWindow.xaml.cs` - EPageType.Statistics 추가, mStatisticsWindow 멤버, PopupView switch 에 Statistics 케이스(Reviewer 미러) 추가
- `WPF_Example/UI/MenuBar.xaml` - "통계분석" 버튼(5번째 컬럼) 추가, 부모 Grid ColumnDefinition 1개 추가, 헤더 TextBlock ColumnSpan 4→5 보정
- `WPF_Example/UI/MenuBar.xaml.cs` - `Button_Statistics_Click` 핸들러 추가
- `WPF_Example/DatumMeasurement.csproj` - StatisticsWindow.xaml(Page)/xaml.cs(Compile) 등록

## Decisions Made
- 플랜에 명시된 D-08~D-14 잠금 결정을 그대로 구현. 별도 아키텍처 판단 불필요 (Rule 4 트리거 없음).
- 히스토그램 USL/LSL 수직선은 addBarLayer 의 기본 x축(정수 인덱스 스케일)에 맞춰 값→bin 인덱스로 환산 후 addMark 에 전달 — 플랜의 "값→bin 인덱스 변환 헬퍼" 지침을 그대로 따름.
- MinOf/MaxOf 를 Linq 대신 단순 for 루프로 구현 — StatRow 등 화면 전용 클래스와 이름 충돌 방지 + 프로젝트 가독성 관례(신입 이해 가능) 우선.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] StatisticsWindow.xaml.cs 빌드 오류(CS0103 'ELogType') 수정**
- **Found during:** Task 3 (msbuild Debug/x64 빌드 검증)
- **Issue:** `using ReringProject.Utility;` 만으로는 `ELogType` 이 해석되지 않음 — `ELogType` 은 `ReringProject.Setting` 네임스페이스(`WPF_Example/Setting/SystemSetting.cs`)에 정의되어 있어 별도 using 필요
- **Fix:** `using ReringProject.Setting;` 추가
- **Files modified:** WPF_Example/UI/Statistics/StatisticsWindow.xaml.cs
- **Verification:** msbuild Debug/x64 재빌드 `Build succeeded`, error 0
- **Committed in:** 31f5135 (Task 3 commit)

**2. [Rule 1 - Bug] MenuBar 헤더 TextBlock ColumnSpan 보정**
- **Found during:** Task 2 (MenuBar.xaml "통계분석" 버튼 추가)
- **Issue:** Button_Reviewer 부모 Grid 에 5번째 ColumnDefinition 을 추가하면서, 그 위 `<TextBlock Grid.ColumnSpan="4">`(컨트롤 그룹 헤더 라벨)가 새 5번째 컬럼을 덮지 못해 레이아웃이 어긋남
- **Fix:** `Grid.ColumnSpan="4"` → `Grid.ColumnSpan="5"` 로 동반 수정
- **Files modified:** WPF_Example/UI/MenuBar.xaml
- **Verification:** msbuild Debug/x64 XAML 파서 통과(빌드 성공), 육안 레이아웃 확인은 UAT 단계 권장
- **Committed in:** 57eb9ba (Task 2 commit)

---

**Total deviations:** 2 auto-fixed (1 blocking, 1 bug)
**Impact on plan:** 둘 다 계획서에 없던 정정이지만 빌드/레이아웃 정합성에 필수 — 스코프 확장 없음.

## Issues Encountered

- MSBuild 실행 시 Git Bash(MSYS) 가 `/p:...` `/nologo` 인자를 경로로 오변환하는 문제 발생 — `MSYS2_ARG_CONV_EXCL="*"` 환경변수로 우회. 코드/빌드 산출물에는 영향 없음(실행 환경 이슈).

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness
- Phase 67(STAT-01) 3개 plan(수집/조회·집계/UI) 전부 완료 — msbuild Debug/x64 PASS, 회귀 0.
- 실 데이터 육안 UAT(메뉴 "통계분석" 클릭 → 기간/레시피 조회 → 행 클릭 → 히스토그램/추이 차트 확인) 는 실 CSV 데이터 누적 후 사용자 UAT 단계에서 수행 권장.
- MenuBar 5번째 컬럼 추가로 인한 레이아웃 변화(버튼 폭 축소 등)는 육안 확인 필요 — 저위험(D-09 명시 요구사항).

---
*Phase: 67-stat-01-2026-07-07*
*Completed: 2026-07-07*

## Self-Check: PASSED

All created/modified files and all three task commit hashes (c2cbf43, 57eb9ba, 31f5135) verified present.
