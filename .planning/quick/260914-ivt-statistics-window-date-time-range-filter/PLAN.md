---
phase: quick-260914-ivt
plan: 01
type: execute
mode: quick
wave: 1
depends_on: []
files_modified:
  - WPF_Example/Custom/Sequence/Inspection/MeasurementHistoryCsvLoader.cs
  - WPF_Example/Custom/Sequence/Inspection/RepeatMeasurementStats.cs
  - WPF_Example/Custom/Sequence/Inspection/RepeatRunService.cs
  - WPF_Example/UI/Statistics/StatisticsWindow.xaml
  - WPF_Example/UI/Statistics/StatisticsWindow.xaml.cs
  - .planning/STATE.md
autonomous: true
requirements:
  - R1-period-date-and-time
  - R2-row-level-time-filter
  - R3-same-period-rerun-and-export
  - R4-data-presence-at-a-glance

must_haves:
  truths:
    - "통계 창 기간이 From/To 각각 날짜 + 시(00~23) + 분(00~59) ComboBox 로 선택된다. 창을 열면 From 00:00, To 23:59 이고, 시간을 건드리지 않으면 조회 숫자(N/OK/NG/검출실패/총 행 수/레시피 목록)가 변경 전과 같다 — 실데이터 12개 CSV 전체의 하루 단위 결과가 awk 독립 계산과 한 줄도 다르지 않다."
    - "To 는 그 분의 59초(소수 초 포함)까지 포함된다. From 이 To 보다 늦으면 빈 결과 + 요약 줄에 '기간 오류: 시작이 끝보다 늦습니다' 가 보인다(조용한 빈 표 아님)."
    - "20260914 실데이터: [09:50~09:59] SIDE_1 9개 항목 모두 N=0, [13:20~13:29] 항목당 N=10, 기본값(하루 전체) C13·C14 계열 N=12 · F9 계열 N=13."
    - "기간 안 측정값이 0건인 항목은 표에서 사라지지 않고 보라색 '결과 없음' 행으로 불량 바로 다음 순서에 보인다. 선택 날짜 파일에 줄이 있으나 기간 안에는 줄이 없는 항목도 기록 0 · N 0 으로 남는다."
    - "표에 '기록(틱)' 칸이 N 칸 바로 앞에 있고, 헤더 툴팁이 '틱 수이지 누락 수가 아님'을 설명한다. 검출실패 칸 헤더 툴팁이 DATUM_FAIL + NO_IMAGE 줄 수임을 설명한다. 결과 없음 행의 Cpk 칸은 '-'."
    - "요약 줄 형식: '전체 T항목 · 불량 B · 결과 없음 R · 주의 W · 정상 N', 기간 안 기록 자체가 없는 항목이 있으면 결과 없음 숫자 바로 뒤에 '(기록 없음 X)'."
    - "저장 사진 재검사는 cycle.json InspectionTime 이 [From, To] 안인 자동 tick 만 부품으로 묶고, 원래 통계 비교도 같은 기간으로 조회한다. CPK export 는 같은 기간의 CSV 사이클만 담고, 파일명은 하루 전체면 기존 cpk_report_yyyyMMdd_yyyyMMdd.xlsx, 아니면 yyyyMMdd_HHmm_yyyyMMdd_HHmm 이 붙는다. 재검사 결과 export 파일명은 재검사를 시작한 기간을 따른다."
    - "Debug|x64 빌드 error CS / error MC 0건. 수정 .cs 파일 추가 줄의 하드룰 grep 6종 전부 0, XAML 추가 줄 날짜 꼬리 주석 0."
  artifacts:
    - path: "WPF_Example/Custom/Sequence/Inspection/MeasurementHistoryCsvLoader.cs"
      provides: "StatisticsTimeRange(기간 값 객체), Query/QueryCycles 기간 오버로드 + QueryDirectory/QueryCyclesDirectory(경로 주입, SystemHandler 무관), 행 단위 시각 필터, 기간 밖 항목 보존"
      contains: "public class StatisticsTimeRange"
    - path: "WPF_Example/Custom/Sequence/Inspection/RepeatMeasurementStats.cs"
      provides: "MeasurementStat.RecordCount (AddSample 로 들어온 횟수)"
      contains: "RecordCount"
    - path: "WPF_Example/Custom/Sequence/Inspection/RepeatRunService.cs"
      provides: "SavedCycleRerunPlanner.BuildPlan(StatisticsTimeRange, ...) — InspectionTime 기간 필터"
      contains: "StatisticsTimeRange range"
    - path: "WPF_Example/UI/Statistics/StatisticsWindow.xaml.cs"
      provides: "StatisticsPeriodViewModel, EStatLevel.NoResult, StatRow.RecordCount, 요약/판정 확장, 재검사·export 기간 배선"
      contains: "public class StatisticsPeriodViewModel"
    - path: "WPF_Example/UI/Statistics/StatisticsWindow.xaml"
      provides: "시/분 ComboBox 4개(pnl_Period 바인딩), 결과 없음 보라 트리거, 기록(틱) 칸, 헤더 툴팁, 범례/안내 줄 재배치"
      contains: "pnl_Period"
  key_links:
    - from: "StatisticsWindow.xaml pnl_Period (DatePicker SelectedDate / ComboBox SelectedIndex)"
      to: "StatisticsPeriodViewModel.FromDate/ToDate/FromHourIndex/FromMinuteIndex/ToHourIndex/ToMinuteIndex"
      via: "pnl_Period.DataContext = m_periodVm, TwoWay 바인딩"
      pattern: "FromHourIndex"
    - from: "StatisticsWindow.DoQuery / Btn_Rerun_Click / Btn_CpkExport_Click"
      to: "StatisticsPeriodViewModel.BuildRange()"
      via: "세 경로가 같은 StatisticsTimeRange 를 사용"
      pattern: "m_periodVm\\.BuildRange\\(\\)"
    - from: "MeasurementHistoryCsvLoader.ProcessRow / ProcessCycleRow"
      to: "StatisticsTimeRange.Contains"
      via: "검사일시 파싱 후 [FromInclusive, ToExclusive) 판정"
      pattern: "Range\\.Contains"
    - from: "SavedCycleRerunPlanner.CollectAutoTicks"
      to: "StatisticsTimeRange.Contains(dto.InspectionTime)"
      via: "날짜 폴더 순회 후 tick 시각 필터"
      pattern: "Contains\\(dto\\.InspectionTime\\)"
    - from: "StatisticsWindow.xaml RowStyle DataTrigger"
      to: "StatRow.StatusLevel == EStatLevel.NoResult"
      via: "Binding StatusLevel, Value=NoResult"
      pattern: "Value=\"NoResult\""
---

<objective>
양산 이력 통계 창의 조회 기간을 "날짜 → 날짜 + 시:분" 으로 넓히고(R1), 각 CSV 행의 검사일시로 거른다(R2).
같은 기간을 저장 사진 재검사와 CPK export 에도 그대로 적용한다(R3). "기간 안에 데이터가 나왔는지"를 표·요약에서
한눈에 보이게 한다(R4: 결과 없음 행 강조, 기록(틱) 칸, 검출실패 정의 명시, 요약 개수).

Purpose: 같은 날 테스트 실행과 실제 양산 실행이 섞여 통계가 왜곡되는 문제를 시간 구간 조회로 풀고, 09:50 구간처럼
측정값이 통째로 안 나온 구간이 하루 합계 숫자에 묻혀 안 보이던 문제를 없앤다. 삭제/보관 기능은 범위 밖.

Output: 로더 기간 값 객체 + 행 시각 필터 + 경로 주입 조회, 통계 행 기록 수, 재검사 계획 기간 필터, 통계 창
기간 ViewModel · 시/분 ComboBox · 결과 없음 표시 · 요약 확장 · export 파일명.
</objective>

<execution_context>
@$HOME/.claude/gsd-core/workflows/execute-plan.md
@$HOME/.claude/gsd-core/templates/summary.md
</execution_context>

<context>
@./CLAUDE.md
@WPF_Example/Custom/Sequence/Inspection/MeasurementHistoryCsvLoader.cs
@WPF_Example/UI/Statistics/StatisticsWindow.xaml
@WPF_Example/UI/Statistics/StatisticsWindow.xaml.cs

<hard_rules>
<!-- CLAUDE.md MANDATORY 가독성 규칙 + 이번 작업 제약. 모든 태스크의 신규/수정/이동 줄에 적용 -->
- H1. 삼항 `?:`, null 병합 `??` / `??=`, null 조건 `?.` / `?[]`, C# 8 switch 식(`=>`) 금지 → if/else, 명시적 null 분기, 전통 switch 문(case 마다 break).
- H2. 한 줄 분기도 중괄호. 조건에 `&&`/`||` 3개 이상 늘어놓지 말고 이름 있는 bool 로 선추출. if 중첩 3단계 이상이면 메서드 분리. 가드 절 사용.
- H3. 헝가리언 접두사 b/n/sz/d/hv/dt (지역변수·private 필드). 매직넘버 금지 — 시간 범위(23, 59, 24, 60), 서식 문자열("yyyyMMdd", "yyyyMMdd_HHmm", "D2", "yyyy-MM-dd HH:mm:ss") 전부 이름 있는 const.
- H4. 날짜 꼬리 주석(`//YYMMDD hbk ...`) 신규 금지. 기존 줄을 수정·이동하면 그 줄에 붙은 날짜 꼬리 주석은 지운다(diff 상 추가 줄로 잡힌다). 필요하면 날짜 없는 "왜" 주석으로 대체. 추가 줄의 주석·문자열에 물음표 `?` 를 쓰지 않는다(삼항 grep 오탐 방지). `DateTime?` 선언 줄에는 콜론을 두지 않는다.
- H5. C# 7.2 만. 네 .cs 파일 모두 Allman 브레이스(편집 구간 기준) — 유지.
- H6. 새 .cs 파일 생성 금지(`WPF_Example/DatumMeasurement.csproj` 스테이징/커밋 금지). 새 타입은 기존 파일 안에. XAML 도 기존 파일만.
- H7. `git add -A` / `git add .` 금지 — 경로 지정 스테이징. `git stash` / `git checkout --` / `git restore` 는 이 환경에서 차단됨(쓰지 말 것).
- H8. 운영 데이터 `D:\Data\Statistics`, `D:\Data\Result`, `D:\Data\Recipe` 는 읽기만. 앱 실행 금지. 스크래치 하네스에서 `SystemHandler.Handle` 에 닿는 경로(`Query(DateTime...)`, `Query(StatisticsTimeRange...)`, `QueryCycles(...)` 기간/날짜 오버로드, `BuildPlan`, VM 메서드) 호출 금지 — 경로 주입 메서드(`QueryDirectory`, `QueryCyclesDirectory`)와 순수 정적 헬퍼만 호출.
- H9. Release 빌드/배포(`D:\Data\DatumMeasurement.exe`) 금지 — 오케스트레이터가 사용자 확인 후 처리. `WPF_Example/VersionDefine.cs` 는 건드리지 않는다.
- H10. MVVM: 새 계산 로직은 로더/StatRowPresenter/ViewModel 에. StatisticsWindow code-behind 는 VM/presenter 호출 한 줄 단위 배선만.
- H11. Phase 76 파일(Action_FAIMeasurement.cs, DatumConfig.cs, DatumFindingService.cs, HalconDisplayService.cs, OverlayCaptureRenderer.cs, EdgeInspectionOverlay.cs, VersionDefine.cs)과 `UI/Reviewer/ReviewerWindow.*`, `MeasurementHistoryCsvWriter.cs` 는 수정하지 않는다.
</hard_rules>

<scratch>
SP = `C:/Users/admin/AppData/Local/Temp/claude/C--code-DataMeasurement/0da39e39-7e39-40eb-8182-41eca9b2accd/scratchpad/ivt` (Git Bash 경로: `/c/Users/admin/AppData/Local/Temp/claude/C--code-DataMeasurement/0da39e39-7e39-40eb-8182-41eca9b2accd/scratchpad/ivt`).
빌드: `"C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" WPF_Example/DatumMeasurement.csproj -p:Configuration=Debug -p:Platform=x64 -v:m`
csc: `"C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/Roslyn/csc.exe"`
하네스 선례(작동 확인됨, 읽기 참고용): `.../scratchpad/p76test/P76Test.cs` — `AppDomain.CurrentDomain.AssemblyResolve` 로 `WPF_Example/bin/x64/Debug/` 의 .dll/.exe 를 로드하고, 앱 타입 참조는 `[MethodImpl(MethodImplOptions.NoInlining)]` 메서드 안에서만.
스크래치 파일은 커밋 대상이 아니므로 하드룰 대상이 아니다.
</scratch>

<interfaces>
<!-- 계획 시점에 실제 코드에서 재확인한 사실. 라인번호는 편집 전 기준 — 편집 중 밀리므로 심볼로 찾을 것. -->

MeasurementHistoryCsvLoader.cs (namespace ReringProject.Sequence, Allman, 543줄)
  :17-23  public class StatisticsQueryResult { Stats: Dictionary<string, MeasurementStat>; Series: Dictionary<string, List<double>>; RecipeNames: List<string>; TotalRowCount: int }
  :31-52  consts: CSV_EXT, HEADER_FIRST_TOKEN="검사일시", COLUMN_COUNT=14(15로 올리면 구 14컬럼 파일 전부 손상행 — 유지), COL_TIME=0, COL_RECIPE=1, COL_INDEX=2, COL_SHOT=3, COL_FAI=4, COL_MEASNAME=5, COL_TYPE=6, COL_NOMINAL=7, COL_TOLPLUS=8, COL_TOLMINUS=9, COL_MEASURED=10, COL_JUDGE=11, COL_OVERALL=13, COL_RUNMODE=14
  :58     public static StatisticsQueryResult Query(DateTime dtFrom, DateTime dtTo, string szRecipeFilter)
            :67 szDir = SystemHandler.Handle.Setting.StatisticsSavePath  /  :73 if (dtTo.Date < dtFrom.Date) return  /  :78 for (d = dtFrom.Date; d <= dtTo.Date; d = d.AddDays(1)) → LoadFile  /  :89-91 RecipeNames 정렬, Stats = stats.ComputeAll()
  :102    private static void LoadFile(string szPath, string szRecipeFilter, RepeatMeasurementStats stats, StatisticsQueryResult result, HashSet<string> recipeSet)  — 빈 줄/fields.Count<14/헤더 skip
  :136    private static void ProcessRow(...) — :139 recipeSet.Add(필터 전) → :141 레시피 필터 → :145 IsManualRow → :150 BuildMeasFromRow → :156-162 최소 CycleResultDto 로 stats.AddSample → :165-176 Series(LastHasResult && SkipReason 없음) → :178 TotalRowCount++
  :182    BuildMeasFromRow — Judgement: DATUM_FAIL/NO_IMAGE(SkipReason, HasResult=false), "NO_RESULT"(HasResult=false), "OK", 그 외 전부 NG(HasResult=true)
  :229    private class CycleGroupState { Cycles, Current, LastTime, LastRecipe, LastIndex, SeenKeys, ShotMap, FaiMap }
  :246    public static List<CycleResultDto> QueryCycles(DateTime dtFrom, DateTime dtTo, string szRecipeFilter) — :252 SystemHandler 경로, :258 가드, :263 날짜 루프 → LoadCyclesFromFile
  :347    ProcessCycleRow — 레시피 필터 → IsManualRow → szTime=fields[COL_TIME] … IsNewCycleBoundary(시각/레시피/Index 변화 또는 측정키 재등장) → :369 cycle.InspectionTime = ParseInspectionTime(szTime)
  :409    private static DateTime ParseInspectionTime(string sz) — TryParseExact "yyyy-MM-dd HH:mm:ss" InvariantCulture, 실패 MinValue (호출처는 :369 하나)
  :437    IsManualRow(fields) — fields.Count > COL_RUNMODE 이고 "수동" 이면 true
  :476    ParseDouble(string)

RepeatMeasurementStats.cs (namespace ReringProject.Sequence, Allman)
  :13-35  public class MeasurementStat { ShotName, FAIName, MeasurementName, TypeName, N, Mean, StdDev, Range, Cpk, Cp, UCpk, LCpk, MinValue, MaxValue, NominalValue, TolerancePlus, ToleranceMinus, OkCount, NgCount, DetectFailCount }
  :43-56  private class KeyData { ..., Values, OkCount, NgCount, DetectFailCount, LastNominal, LastTolPlus, LastTolMinus }
  :76     AddSample(dto): 측정마다 :107-117 키 없으면 KeyData 생성(=결과 없어도 키 등록 → N=0 행이 이미 생김) → :120-122 공차 갱신 → :124 DATUM_FAIL 또는 NO_IMAGE 면 DetectFailCount++ / else if LastHasResult 면 Values+OK/NG / 그 외 무시
  :149    ComputeAll(): n=Values.Count, n>0 일 때만 Mean/Cpk 계산(N=0 이면 Cpk=0), :219-241 MeasurementStat 초기화자
  외부 사용: RepeatExcelExportService(DetectFailCount 등 기존 필드만), StatisticsWindow.xaml.cs, RepeatRunService.cs 없음(통계 창 VM 이 사용)

RepeatRunService.cs — SavedCycleRerunPlanner (namespace ReringProject.Sequence, Allman 구간)
  :1153   public static SavedCycleRerunPlan BuildPlan(DateTime dtFrom, DateTime dtTo, string szRecipeName, InspectionSequence seq, InspectionRecipeManager recipeManager)
            :1156 bool bInvalidArgs = seq == null || recipeManager == null || string.IsNullOrEmpty(szRecipeName) || dtTo < dtFrom;  /  :1164 CollectAutoTicks(dtFrom, dtTo, szRecipeName, seq)  /  :1166 InspectionTime 정렬 → :1168 GroupIntoParts(기준점 z tick = 새 부품, 첫 기준점 이전 tick 은 "기준점 촬영 없이 시작된 사이클" 제외 사유)
  :1212   private static List<CycleResultDto> CollectAutoTicks(DateTime dtFrom, DateTime dtTo, string szRecipeName, InspectionSequence seq)
            dCurrent=dtFrom.Date..dLast=dtTo.Date 로 ResultSavePath\yyyyMMdd\*_cycle\cycle.json 전부 CycleResultSerializer.Load → IsEligibleAutoTick(IsProtocolDriven && ZIndex>=0 && RecipeName 일치 && 시퀀스 소유 Shot) 이면 추가. **시각 필터 없음(날짜 폴더 단위).**
  BuildPlan 호출처는 StatisticsWindow.xaml.cs:620 하나뿐(grep 확인).
  CycleResultSerializer.Load(jsonPath) (:270) — JsonConvert.DeserializeObject<CycleResultDto>(TypeNameHandling.None), SystemHandler 미사용 → 하네스에서 호출 가능. cycle.json InspectionTime 은 "2026-09-14T13:20:10.5866273+09:00" (소수 초 + 오프셋 → Local DateTime 으로 역직렬화, 벽시계 비교 가능).

StatisticsWindow.xaml.cs (namespace ReringProject.UI, Allman, 1155줄)
  :18-23  public enum EStatLevel { Bad = 0, Warning = 1, Normal = 2 }  // 정수값 = 나쁜 순 정렬 순위
  :28-85  public class StatRow { ShotName, FAIName, MeasurementName, N, Mean, StdDev, Range, CpkText, OkCount, NgCount, DetectFailCount, YieldRateText, Key, NominalValue, TolerancePlus, ToleranceMinus, StatusLevel, CpkSortValue, ToleranceRangeText, OutOfRangeText, OutOfRangeAmount, OriginalMeanText, DeltaText, DeltaSortValue }
  :90     public static class StatRowPresenter — consts :92-104 (NO_VALUE_TEXT="-", MIN_SAMPLES_FOR_CPK=2 …)
            :107 BuildRows(stats) (:127 row.CpkText = CpkToText(s.Cpk))  /  :205 JudgeStatus(s): NgCount>0→Bad, !IsCpkUsable→Normal, Cpk<1.0→Bad, <1.33→Warning, Normal
            :231 GetCpkSortValue (N<2 → 계산불가 맨 뒤)  /  :301 SortWorstFirst → CompareWorstFirst((int)StatusLevel → CpkSortValue → Key)
            :330 BuildSummary(rows): "전체 {T}항목 · 불량 {B} · 주의 {W} · 정상 {N}" (가운뎃점 U+00B7, switch 문)  /  :362 IsProblemRow: StatusLevel != Normal
            :380 FillRerunComparison(rows, originalStats) — original.N > 0 && row.N > 0 일 때만 비교
  :429    public class StatisticsRerunViewModel : INotifyPropertyChanged
            :562 public bool TryStartRerun(DateTime dtFrom, DateTime dtTo, string szRecipeFilter, out string szError) — :617-623 Task.Run { BuildPlan(dtFrom, dtTo, szCurrentRecipe, seq, recipeManager); MeasurementHistoryCsvLoader.Query(dtFrom, dtTo, szCurrentRecipe); BeginInvoke(OnPlanReady) }
            :690 ApplyRerunEnded — cycles 로 RepeatMeasurementStats 재계산 → BuildRows/FillRerunComparison/SortWorstFirst/BuildSummary
            :776 public List<CycleResultDto> GetCyclesForExport(DateTime dtFrom, DateTime dtTo, string szRecipeFilter) — IsShowingRerun 이면 RerunCycles, 아니면 MeasurementHistoryCsvLoader.QueryCycles(dtFrom, dtTo, szRecipeFilter)
            :798 GetExportFilePrefix() — "cpk_rerun_" / "cpk_report_"
  :813    public partial class StatisticsWindow : Window
            :820 m_rerunVm  /  :822 ctor: :825-826 dp_From/dp_To.SelectedDate = DateTime.Today, :827 pnl_Rerun.DataContext = m_rerunVm, :830 DoQuery("")
            :844 Btn_Rerun_Click: GetSelectedRange(out,out) → m_rerunVm.TryStartRerun(dtFrom, dtTo, szRecipeFilter, out szError)
            :904 DoQuery: GetSelectedRange → Query → PopulateRecipeCombo → BuildRows → SortWorstFirst → ItemsSource → ApplyProblemFilter → :918 txt_Summary.Text = BuildSummary(rows) → ClearCharts → UpdateExportButtonState
            :946 GetSelectedRange(out DateTime dtFrom, out DateTime dtTo): DatePicker 미선택이면 Today
            :962 UpdateExportButtonState: m_lastResult.TotalRowCount > 0
            :977 Btn_CpkExport_Click: :987 GetCyclesForExport(dtFrom, dtTo, recipe) → :999 FileName = prefix + dtFrom.ToString("yyyyMMdd") + "_" + dtTo.ToString("yyyyMMdd") + ".xlsx"
  MainWindow.xaml.cs:415 `new UI.StatisticsWindow()` — 인자 없는 생성자 유지.

StatisticsWindow.xaml
  :22-71 Row0 필터바 Border > StackPanel Vertical:
    :25-40 가로 줄1: "기간" TextBlock, dp_From(130), "~", dp_To(130), "레시피", combo_Recipe(180), btn_Query, btn_CpkExport, :37-39 "※ PLC 자동 검사만 집계 (수동 RUN·일괄·반복 검사 제외)" TextBlock(FontSize 12, #B45309)
    :41-49 가로 줄2: chk_ProblemOnly "문제 항목만 보기 (불량·주의)", txt_Summary, :47-48 범례 TextBlock "빨강 = 불량(NG 발생 또는 Cpk 1.0 미만) · 노랑 = 주의(Cpk 1.0 ~ 1.33)"
    :51-69 pnl_Rerun (DataContext = m_rerunVm)
  :85-103 RowStyle: DataTrigger Bad(#FEE2E2/#991B1B), Warning(#FEF9C3/#854D0E), Trigger IsSelected
  :104-123 Columns 15개: Shot70 FAI70 측정명110 기준75 허용범위140 N45 평균75 원래평균80(Collapsed) 변화80(Collapsed) 벗어난양110 표준편차75 범위70 Cpk65 OK45 NG45 검출실패65 수율70 (보이는 칸 합 ≈1130px, 창 1280)

실데이터 (계획 시점 측정, 읽기만):
  D:\Data\Statistics\*.csv 12개: 따옴표 필드 0, 시각 파싱 실패 행 0, 파일 날짜와 다른 날짜 행 0. 20260723~20260811 은 구 14컬럼, 이후 15컬럼. 20260724 는 수동 아닌 NO_IMAGE/DATUM_FAIL 11줄(검출실패 회귀 검증용).
  20260914.csv (자동 828줄, 전부 FAI_1, OK 0건 — 결과 있는 줄은 전부 NG): 항목 9개(SIDE_SHOT_1_C13-14/FAI_C13-14/C13_P1..P3, C14_P1..P3, SIDE_SHOT_1_F9/FAI_F9/F9_P1..P3).
    하루 전체: 항목당 92줄, N = C13·C14 12, F9 13, 사이클 재조립 92.
    [09:50:00, 09:59:59]: 항목당 40줄, N 0, 사이클 40.   [13:20:00, 13:29:59]: 항목당 40줄, N 10, 사이클 40.   [10:00, 12:59]: 자동·수동 모두 0줄.
  D:\Data\Result\20260914: cycle 폴더 167개, IsProtocolDriven=true 92개. 폴더 시각 09:50~09:59 자동 40개, 13:20~13:29 자동 40개.
</interfaces>
</context>

## 지표 정의와 근거 (R4 — 실행자는 이 정의를 그대로 구현하고 SUMMARY 에 옮겨 적는다)

| 표시 | 정의 | 근거 / 오해 방지 |
|------|------|------------------|
| N (기존) | 기간 안 OK+NG 줄 수 = 측정값이 나온 횟수 | 정의 불변. 헤더 툴팁만 추가 |
| 기록(틱) (신규) | 기간 안 그 항목 줄 수(자동·레시피 필터 통과) = `MeasurementStat.RecordCount` | CSV writer 는 틱마다 그 시퀀스의 모든 측정을 1줄씩 쓴다 → 항목 줄 수 = 그 시퀀스의 검사 틱 수. **N 의 기대값이 아니다**: 한 부품을 여러 z 틱으로 나눠 찍고 항목은 한 틱에서만 잰다(실측 부품당 4틱). 그래서 "누락 수"·"결과 비율" 칸은 만들지 않는다. 쓸모: N=0 일 때 "틱은 40번 돌았는데 결과 0"(09:50) 과 "기간 안 기록 자체 0"(시퀀스가 안 돔) 을 구분 |
| 검출실패 (기존) | DATUM_FAIL + NO_IMAGE 줄 수(RepeatMeasurementStats 규칙 그대로) | CSV 에 항목 단위로 남는 명시적 실패는 이 둘뿐. DETECT_FAIL 은 사이클 종합(OverallCycleResult=N)에만 있음. MEASURE_FAIL/ALIGN_FAIL/DATUM_REF_MISSING 등은 `ClearResult()` 로 HasResult=false 가 되어 writer 가 NO_RESULT 로 기록 → 구조적 빈 줄과 구분 불가. 헤더 툴팁에 이 한계를 적는다. writer 확장은 범위 밖(로더의 "그 외=NG" 분기와 동시 변경 필요) — SUMMARY 에 후속 제안으로 기록 |
| 결과 없음 (신규 상태, 보라) | N == 0 (이때 NG 도 0) | 기존에는 N=0 행이 흰색 "정상" 이었다. 불량 다음 순서로 정렬, "문제 항목만 보기" 에 포함 |
| 요약 "(기록 없음 X)" | 결과 없음 중 RecordCount == 0 인 항목 수 | 선택 날짜 파일에는 줄이 있으나 기간 안에는 줄이 없는 항목 |
| 부품 수 | **넣지 않음** | CSV 에 z 열 없음. 부품 경계는 cycle.json 의 ZIndex == 기준점 z 로만 알 수 있고(SavedCycleRerunPlanner), 이는 결과 폴더 저장 여부·기준점 사진·레시피 z 구성에 따라 달라지는 다른 소스·다른 필터라 CSV 기반 N 옆에 두면 오해를 부른다 |

항목 목록 범위 결정: 선택 날짜 파일에서 레시피·자동 필터를 통과한 항목은 기간 안 줄이 없어도 표에 남긴다(기록 0 · N 0 · 결과 없음). 근거는 두 가지다. 브리프가 "사라지지 않고"를 요구했다. 또 시간을 좁혔을 때 빈 표는 조회 실패처럼 보이므로 "안 나왔다"가 보여야 한다. 공차는 그 항목의 마지막 줄 값을 쓴다(AddSample 의 "마지막 공차값" 정책과 같음). 레시피 콤보 목록은 브리프대로 기간 안 줄에서만 만든다. 기본값(하루 전체)에서는 모든 줄이 기간 안이라 추가 항목이 0개이고 결과가 같다.

<tasks>

<task type="tracer">
  <name>Task 1 (tracer): 기간 값 객체 + 로더 행 시각 필터 + 기간 ViewModel/시·분 ComboBox + DoQuery 배선 — 시:분으로 조회하면 표가 그 구간 행만 반영</name>
  <files>WPF_Example/Custom/Sequence/Inspection/MeasurementHistoryCsvLoader.cs, WPF_Example/UI/Statistics/StatisticsWindow.xaml.cs, WPF_Example/UI/Statistics/StatisticsWindow.xaml</files>
  <action>
시작 전: SP 폴더를 만들고 `git rev-parse HEAD` 를 SP/base.txt 에 저장한다(모든 태스크의 추가 줄 grep 기준, SUMMARY 에 기록). `grep -rn "StatisticsTimeRange\|StatisticsPeriodViewModel\|QueryDirectory" WPF_Example --include=*.cs` 가 0건인지 확인한다(이름 충돌 없음 — 계획 시점 확인됨).

(A) MeasurementHistoryCsvLoader.cs — StatisticsQueryResult 바로 아래에 `public class StatisticsTimeRange` 를 추가한다(R1, R2). 한 줄 XML doc: "통계 조회 기간. [FromInclusive, ToExclusive) 반열린 구간 — To 는 선택한 분의 59초(소수 초 포함)까지 포함".
- const: `public const int LAST_HOUR = 23;`, `public const int LAST_MINUTE = 59;`, `private const int ONE_MINUTE = 1;`, `private const string FILE_DATE_FORMAT = "yyyyMMdd";`, `private const string FILE_DATETIME_FORMAT = "yyyyMMdd_HHmm";`, `private const string FILE_STAMP_SEPARATOR = "_";`.
- 프로퍼티(get; private set;): `DateTime FromInclusive`, `DateTime ToMinute`(선택한 To 분의 시작, 파일명용), `DateTime ToExclusive`(= ToMinute 에 1분 더함).
- `public static StatisticsTimeRange FromParts(DateTime dtFromDate, int nFromHour, int nFromMinute, DateTime dtToDate, int nToHour, int nToMinute)`: 시/분이 범위 밖(음수 또는 LAST_HOUR/LAST_MINUTE 초과, 예: ComboBox 미선택 -1)이면 From 은 0, To 는 LAST_HOUR/LAST_MINUTE 로 대체한다(private static `ClampOrFallback(int nValue, int nMax, int nFallback)`). FromInclusive = dtFromDate.Date + 시 + 분, ToMinute = dtToDate.Date + 시 + 분, ToExclusive = ToMinute.AddMinutes(ONE_MINUTE).
- `public static StatisticsTimeRange FromDates(DateTime dtFromDate, DateTime dtToDate)` = FromParts(dtFromDate, 0, 0, dtToDate, LAST_HOUR, LAST_MINUTE). 날짜만 넘기던 기존 의미(하루 전체)와 같다.
- 읽기 전용 프로퍼티: `bool IsEmpty` (ToExclusive <= FromInclusive — From 이 To 보다 늦은 경우 방어), `bool IsWholeDays` (FromInclusive.TimeOfDay 와 ToExclusive.TimeOfDay 가 둘 다 TimeSpan.Zero), `DateTime FirstDate` (FromInclusive.Date), `DateTime LastDate` (ToMinute.Date).
- `public bool Contains(DateTime dt)` → dt >= FromInclusive 이고 dt < ToExclusive.
- `public string BuildFileStamp()` → IsWholeDays 면 FirstDate(FILE_DATE_FORMAT) + 구분자 + LastDate(FILE_DATE_FORMAT) (기존 export 파일명과 동일), 아니면 FromInclusive(FILE_DATETIME_FORMAT) + 구분자 + ToMinute(FILE_DATETIME_FORMAT). 이 태스크에서는 호출처가 없다(Task 3 이 export 에 연결).

(B) MeasurementHistoryCsvLoader — Query 경로에 행 시각 필터 추가(R2).
- const `INSPECTION_TIME_FORMAT = "yyyy-MM-dd HH:mm:ss"` 추가. `private static bool TryParseInspectionTime(string sz, out DateTime dt)` 를 추가한다(TryParseExact, InvariantCulture, DateTimeStyles.None). 기존 ParseInspectionTime 은 Task 3 까지 그대로 둔다. 서식 문자열만 이 const 로 바꾸는 것은 허용.
- `private class StatsQueryState` 를 CycleGroupState 와 같은 모양으로 추가한다. 필드: `StatisticsTimeRange Range`, `string RecipeFilter`, `RepeatMeasurementStats Stats = new ...`, `StatisticsQueryResult Result = new ...`, `HashSet<string> RecipeSet = new ...`. 매개변수 5개를 들고 다니지 않게 하기 위함이다.
- 기존 `Query(DateTime dtFrom, DateTime dtTo, string szRecipeFilter)` 는 본문을 `return Query(StatisticsTimeRange.FromDates(dtFrom, dtTo), szRecipeFilter);` 한 줄로 바꾼다. 날짜만 넘기는 호출의 동작은 그대로다.
- 새 `public static StatisticsQueryResult Query(StatisticsTimeRange range, string szRecipeFilter)`: try 안에서 szDir = SystemHandler.Handle.Setting.StatisticsSavePath 를 읽는다. 예외면 기존 catch 패턴으로 로그를 남기고 빈 StatisticsQueryResult 를 반환한다. 성공하면 `return QueryDirectory(szDir, range, szRecipeFilter);`.
- 새 `public static StatisticsQueryResult QueryDirectory(string szDir, StatisticsTimeRange range, string szRecipeFilter)` (doc: "경로를 직접 받는 조회 — SystemHandler 없이 호출 가능. 화면은 Query 를 쓴다"). 기존 Query 본문을 옮긴다. 가드 순서: range null → 빈 결과, szDir 비어 있음 → 빈 결과, range.IsEmpty → 빈 결과. 날짜 루프는 range.FirstDate 부터 range.LastDate 까지(AddDays(1)). 파일마다 `LoadFile(szPath, state)`. 끝나면 RecipeNames 정렬과 `Stats = state.Stats.ComputeAll()` 을 한다(기존과 같음). 기존 try/catch 격리와 로그 문구를 유지한다.
- LoadFile 시그니처를 `(string szPath, StatsQueryState state)` 로 바꾼다. 가드(빈 줄 / fields.Count < COLUMN_COUNT / 헤더)는 그대로 둔다.
- ProcessRow 시그니처를 `(List<string> fields, StatsQueryState state)` 로 바꾸고 순서를 다음으로 한다.
  (1) `TryParseInspectionTime(fields[COL_TIME], out dtRow)` 실패면 return — 손상 행 가드와 같은 취급이며, 실데이터 실패 0건이라 기본값 결과는 불변.
  (2) `bool bInRange = state.Range.Contains(dtRow);`
  (3) bInRange 일 때만 RecipeSet.Add — 레시피 목록은 기간 안 줄 기준.
  (4) 레시피 필터 불일치면 return.
  (5) IsManualRow 면 return.
  (6) bInRange 가 아니면 return. Task 2 가 이 return 바로 앞에 "기간 밖 항목 기억"을 넣을 자리이므로 반드시 (4)(5) 뒤에 둔다.
  (7) 이후 BuildMeasFromRow → AddSample → Series → TotalRowCount++ 는 기존 로직 그대로, state 필드를 쓰도록만 바꾼다.
  옮기거나 바꾼 줄의 날짜 꼬리 주석은 hard_rules H4 대로 지운다.

(C) StatisticsWindow.xaml.cs — StatisticsRerunViewModel 위에 `public class StatisticsPeriodViewModel : INotifyPropertyChanged` 를 추가한다(R1, H10). doc: "통계 창 조회 기간(날짜 + 시:분) 선택 상태. 기본 From 00:00, To 23:59 = 하루 전체".
- const: `private const int HOURS_PER_DAY = StatisticsTimeRange.LAST_HOUR + 1;`, `private const int MINUTES_PER_HOUR = StatisticsTimeRange.LAST_MINUTE + 1;`, `private const string TWO_DIGIT_FORMAT = "D2";`, `public const string INVALID_RANGE_TEXT = "기간 오류: 시작이 끝보다 늦습니다";`.
- 바인딩 프로퍼티(StatisticsRerunViewModel 과 같은 RaisePropertyChanged 패턴, private 필드는 헝가리언): `List<string> HourItems` ("00".."23", 생성자에서 for 루프로 채움), `List<string> MinuteItems` ("00".."59"), `DateTime? FromDate` / `DateTime? ToDate` (기본 DateTime.Today), `int FromHourIndex` (기본 0), `int FromMinuteIndex` (기본 0), `int ToHourIndex` (기본 StatisticsTimeRange.LAST_HOUR), `int ToMinuteIndex` (기본 StatisticsTimeRange.LAST_MINUTE).
- `public StatisticsTimeRange BuildRange()`: 날짜가 null 이면 DateTime.Today 로 대체(기존 GetSelectedRange 폴백과 같음). `StatisticsTimeRange.FromParts(from, FromHourIndex, FromMinuteIndex, to, ToHourIndex, ToMinuteIndex)`.
- `public string BuildSummaryText(StatisticsTimeRange range, string szRowSummary)`: range 가 null 이 아니고 IsEmpty 면 INVALID_RANGE_TEXT, 아니면 szRowSummary.

(D) StatisticsWindow 클래스 배선(H10, 한 줄 호출만).
- 필드 `private readonly StatisticsPeriodViewModel m_periodVm = new StatisticsPeriodViewModel();` 추가.
- 생성자에서 dp_From/dp_To.SelectedDate 대입 두 줄을 지우고, `pnl_Period.DataContext = m_periodVm;` 를 DoQuery("") 호출 전에 넣는다.
- DoQuery: GetSelectedRange 호출과 두 지역변수를 `StatisticsTimeRange range = m_periodVm.BuildRange();` 로 바꾼다. `MeasurementHistoryCsvLoader.Query(range, szRecipeFilter)` 를 호출하고, 요약 줄은 `txt_Summary.Text = m_periodVm.BuildSummaryText(range, StatRowPresenter.BuildSummary(rows));` 로 한다. 나머지 순서는 그대로 둔다.
- GetSelectedRange(out, out) 는 이 태스크에서 **그대로 둔다**. Btn_Rerun_Click/Btn_CpkExport_Click 이 아직 쓰고, DatePicker SelectedDate 는 바인딩으로 VM 값을 반영하므로 날짜는 같다. Task 3 이 두 호출처를 바꾸면서 삭제한다.

(E) StatisticsWindow.xaml — 필터바 재배치(R1). 가로폭 1280 에서 시/분 ComboBox 4개가 들어가면 줄1이 넘치므로 안내 문구를 옮긴다.
- 줄1 가로 StackPanel 에 `x:Name="pnl_Period"` 를 붙인다. 순서: "기간" · dp_From(`SelectedDate="{Binding FromDate, Mode=TwoWay}"`) · combo_FromHour(Width 52, `ItemsSource="{Binding HourItems}"`, `SelectedIndex="{Binding FromHourIndex, Mode=TwoWay}"`, Margin 4,0,0,0) · ":" TextBlock · combo_FromMinute(MinuteItems / FromMinuteIndex) · "~" · dp_To(ToDate) · combo_ToHour(ToHourIndex) · ":" · combo_ToMinute(ToMinuteIndex, `ToolTip="끝 시각은 그 분 59초까지 포함"`) · "레시피" · combo_Recipe · btn_Query · btn_CpkExport. 기존 컨트롤의 x:Name, 이벤트, 폭은 유지한다. 안내 TextBlock("※ PLC 자동 검사만 집계 …")은 이 줄에서 뺀다.
- 줄2 (chk_ProblemOnly, txt_Summary)는 그대로 두고, 범례 TextBlock 을 이 줄에서 뺀다.
- 줄2 와 pnl_Rerun 사이에 새 가로 StackPanel(Margin "0,4,0,0")을 둔다. 내용은 범례 TextBlock(Task 1 에서는 문구 불변) + 안내 TextBlock(문구·색·FontSize 불변, Margin "24,0,0,0")이고, 안내 위의 XAML 주석도 함께 옮긴다.
- 필터바 머리 주석(:22)의 "기본 오늘" 설명을 "기간 날짜 + 시:분(기본 00:00 ~ 23:59)" 로 갱신한다. XAML 신규 주석에도 날짜 꼬리 주석을 쓰지 않는다.

(F) 스크래치 하네스 SP/IvtHarness.cs 를 작성한다(커밋 안 함, H8). p76test/P76Test.cs 의 AssemblyResolve + NoInlining 구조를 따른다.
- 결과는 `new UTF8Encoding(false)` 로 파일에 쓴다. 콘솔 코드페이지 문제를 피하기 위함이다.
- 데이터 경로 상수: `D:\Data\Statistics`.
- 모드 `wholeday <out>`: `D:\Data\Statistics` 의 8자리 이름 csv 마다 d 를 구해 `QueryDirectory(dir, StatisticsTimeRange.FromDates(d, d), "")` 를 호출한다. 항목마다 `yyyyMMdd|key|N|DetectFailCount` 를, 파일마다 `yyyyMMdd|TOTAL|TotalRowCount` 를 쓴다.
- 모드 `range <out> <fromDate yyyyMMdd> <fromHHmm> <toDate yyyyMMdd> <toHHmm>`: FromParts 로 기간을 만들어 `QueryDirectory(dir, range, "")` 를 호출한다. 기록 줄은 `EMPTY|<IsEmpty>`, `WHOLE|<IsWholeDays>`, `TOTAL|<TotalRowCount>`, `RECIPES|<쉼표 join>` 이고, 이어서 Stats 키를 ordinal 정렬해 항목마다 `key|N=<N>|OK=<OkCount>|NG=<NgCount>|DF=<DetectFailCount>|SERIES=<Series 개수, 없으면 0>` 를 쓴다.
- 컴파일: `csc -nologo -platform:x64 -out:SP/IvtHarness.exe -r:C:/code/DataMeasurement/WPF_Example/bin/x64/Debug/DatumMeasurement.exe SP/IvtHarness.cs`. 참조 누락 오류가 나면 `C:/Windows/Microsoft.NET/Framework64/v4.0.30319/WPF/` 의 WindowsBase/PresentationCore/PresentationFramework 와 System.Xaml 을 -r 로 추가한다.
- 빌드 후 DatumMeasurement.exe 가 MeasurementHistoryCsvLoader.cs 보다 새 파일인지 확인한다. MSB3027 복사 실패로 오래된 exe 를 검증하는 일을 막기 위함이다.

커밋: `git add WPF_Example/Custom/Sequence/Inspection/MeasurementHistoryCsvLoader.cs WPF_Example/UI/Statistics/StatisticsWindow.xaml.cs WPF_Example/UI/Statistics/StatisticsWindow.xaml` → `feat(quick-260914-ivt): 통계 조회 기간을 날짜+시:분으로 — 행 단위 검사일시 필터 + 기간 ViewModel`. `git status` 에서 csproj 가 스테이징되지 않았는지 확인한다.
  </action>
  <verify>
    <automated>cd /c/code/DataMeasurement && SP=/c/Users/admin/AppData/Local/Temp/claude/C--code-DataMeasurement/0da39e39-7e39-40eb-8182-41eca9b2accd/scratchpad/ivt && BASE=$(cat $SP/base.txt) && "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" WPF_Example/DatumMeasurement.csproj -p:Configuration=Debug -p:Platform=x64 -v:m > $SP/build_t1.log 2>&1; echo "build_errors=$(grep -cE 'error (CS|MC)' $SP/build_t1.log || true)"; [ WPF_Example/bin/x64/Debug/DatumMeasurement.exe -nt WPF_Example/Custom/Sequence/Inspection/MeasurementHistoryCsvLoader.cs ] && echo exe_fresh=1; FAIL=0; for F in WPF_Example/Custom/Sequence/Inspection/MeasurementHistoryCsvLoader.cs WPF_Example/UI/Statistics/StatisticsWindow.xaml.cs; do D="$(git diff -U0 "$BASE" -- "$F" | grep '^+' | grep -v '^+++' || true)"; T=$(printf '%s\n' "$D" | grep -cE '\?[^\?]*:' || true); C=$(printf '%s\n' "$D" | grep -cF '??' || true); N=$(printf '%s\n' "$D" | grep -cF '?.' || true); S=$(printf '%s\n' "$D" | grep -cE 'switch.*=>' || true); H=$(printf '%s\n' "$D" | grep -cF 'hbk' || true); B=$(printf '%s\n' "$D" | grep -cE '^\+\s*(if\s*\(.*\)|else)\s+[^{/ ].*;\s*$' || true); echo "$F tern=$T coal=$C nullc=$N swx=$S hbk=$H nobrace=$B"; [ "$T$C$N$S$H$B" = "000000" ] || FAIL=1; done; echo "xaml_hbk=$(git diff -U0 "$BASE" -- WPF_Example/UI/Statistics/StatisticsWindow.xaml | grep '^+' | grep -v '^+++' | grep -cF hbk || true)"; echo "rule_fail=$FAIL"; cd $SP && "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/Roslyn/csc.exe" -nologo -platform:x64 -out:IvtHarness.exe -r:C:/code/DataMeasurement/WPF_Example/bin/x64/Debug/DatumMeasurement.exe IvtHarness.cs && ./IvtHarness.exe wholeday wd.txt && for f in /d/Data/Statistics/[0-9]*.csv; do d=$(basename "$f" .csv); awk -F',' -v d="$d" '{sub(/\r$/,"")} NF>=14 && $1 !~ /검사일시$/ && $15!="수동" {k=$4"/"$5"/"$6; t++; if(!(k in n)){n[k]=0; df[k]=0} if($12=="DATUM_FAIL"||$12=="NO_IMAGE"){df[k]++} else if($12!="NO_RESULT"){n[k]++}} END{for(k in n) print d"|"k"|"n[k]"|"df[k]; print d"|TOTAL|"t+0}' "$f"; done > wd_expect.txt && LC_ALL=C sort wd.txt > wd_a.txt && LC_ALL=C sort wd_expect.txt > wd_b.txt && diff wd_a.txt wd_b.txt && echo wholeday_identical=1 && ./IvtHarness.exe range r1.txt 20260914 0000 20260914 2359 && ./IvtHarness.exe range r2.txt 20260914 0950 20260914 0959 && ./IvtHarness.exe range r3.txt 20260914 1320 20260914 1329 && ./IvtHarness.exe range r4.txt 20260914 1000 20260914 1259 && ./IvtHarness.exe range r5.txt 20260914 1330 20260914 1320 && echo "r1 N12=$(grep -c '|N=12|' r1.txt) N13=$(grep -c '|N=13|' r1.txt) $(grep -E '^(TOTAL|RECIPES|WHOLE)\|' r1.txt | tr '\n' ' ')" && echo "r2 N0=$(grep -c '|N=0|' r2.txt) $(grep -E '^(TOTAL|WHOLE)\|' r2.txt | tr '\n' ' ')" && echo "r3 N10=$(grep -c '|N=10|' r3.txt) $(grep '^TOTAL|' r3.txt)" && echo "r4 keys=$(grep -c '|N=' r4.txt || true) $(grep '^TOTAL|' r4.txt)" && echo "r5 keys=$(grep -c '|N=' r5.txt || true) $(grep -E '^(EMPTY|TOTAL)\|' r5.txt | tr '\n' ' ')"</automated>
  </verify>
  <done>
- build_errors=0, exe_fresh=1, 두 .cs 파일 grep 6종 전부 0(rule_fail=0), xaml_hbk=0.
- wholeday_identical=1: 12개 CSV 전체(구 14컬럼 포함, 20260724 검출실패 11줄 포함)에서 하루 단위 항목별 N·검출실패·총 행 수가 awk 독립 계산과 일치 → "시간을 안 건드리면 결과 불변".
- r1: N12=6, N13=3, TOTAL|828, RECIPES|FAI_1, WHOLE|True. r2: N0=9, TOTAL|360, WHOLE|False. r3: N10=9, TOTAL|360. r4: keys=0, TOTAL|0 (Task 2 에서 9 로 바뀜). r5: keys=0, EMPTY|True, TOTAL|0.
- 커밋 1개(세 파일만), csproj 미스테이징.
  </done>
</task>

<task type="auto">
  <name>Task 2: "데이터가 나왔나" 한눈에 — RecordCount, 기간 밖 항목 보존, 결과 없음 상태(보라)·정렬·필터, 기록(틱) 칸·헤더 툴팁, 요약 개수</name>
  <files>WPF_Example/Custom/Sequence/Inspection/RepeatMeasurementStats.cs, WPF_Example/Custom/Sequence/Inspection/MeasurementHistoryCsvLoader.cs, WPF_Example/UI/Statistics/StatisticsWindow.xaml.cs, WPF_Example/UI/Statistics/StatisticsWindow.xaml</files>
  <action>
위 "지표 정의와 근거" 표를 그대로 구현한다(R4).

(A) RepeatMeasurementStats.cs
- MeasurementStat 에 `public int RecordCount { get; set; }` 를 추가한다. doc: "AddSample 로 이 키의 측정이 들어온 횟수(결과 유무·스킵 사유 무관). 통계 CSV 조회에서는 기간 안 이 항목 줄 수 = 이 항목 시퀀스의 검사 틱 수. N 의 기대값이 아니다".
- KeyData 에 `public int RecordCount;` 를 추가한다.
- AddSample 에서 키 조회/생성 직후, 공차 갱신 전에 `d.RecordCount++;` 를 넣는다. 기존 DetectFail/Values/OK/NG 분기는 한 글자도 바꾸지 않는다.
- ComputeAll 초기화자에 `RecordCount = d.RecordCount` 를 추가한다.
- 이 파일 기존 줄의 조건 연산자·날짜 주석은 건드리지 않는다(추가 줄만 검사됨). RepeatExcelExportService 는 새 필드를 읽지 않으므로 영향이 없다.

(B) MeasurementHistoryCsvLoader.cs — 기간 밖 항목 보존.
- StatsQueryState 에 `Dictionary<string, MeasurementStat> KeysOutsideRange = new ...` 를 추가한다.
- ProcessRow 의 "bInRange 가 아니면 return" 바로 앞에 `RememberKeyOutsideRange(fields, state);` 를 넣는다. 레시피·수동 필터를 통과한 줄만 기억된다.
- `private static void RememberKeyOutsideRange(List<string> fields, StatsQueryState state)`: 키는 Shot/FAI/측정명(기존 키 포맷)이다. `new MeasurementStat` 에 ShotName, FAIName, MeasurementName, TypeName(COL_TYPE), NominalValue/TolerancePlus/ToleranceMinus(ParseDouble)만 채워 `state.KeysOutsideRange[key]` 에 덮어쓴다(마지막 줄의 공차). N/RecordCount/카운트/Cpk 는 기본값 0.
- `private static void AddKeysWithoutRowsInRange(StatsQueryState state)`: QueryDirectory 에서 `Result.Stats = Stats.ComputeAll()` 바로 뒤에 호출한다. KeysOutsideRange 중 Result.Stats 에 없는 키만 추가한다. Series 와 TotalRowCount 는 건드리지 않으므로, 기간 안 줄이 없으면 export 버튼이 계속 비활성이다.
- doc 주석에 이유를 한 줄로 적는다: 시간을 좁혔을 때 항목이 사라지지 않고 결과 없음으로 보이게 하기 위함.

(C) StatisticsWindow.xaml.cs — StatRowPresenter / 모델.
- EStatLevel 을 `Bad = 0, NoResult = 1, Warning = 2, Normal = 3` 으로 바꾼다. 주석은 "정수값 = 나쁜 순 정렬 순위. NoResult = 기간 안 측정값 0건" 으로 한다. 정수값을 직접 쓰는 곳은 CompareWorstFirst 의 (int) 캐스트뿐이며 순위 의미가 유지된다. 외부 참조는 없다(grep 확인).
- StatRow 에 `public int RecordCount { get; set; }` 를 추가한다. doc: "기간 안 이 항목 기록(틱) 수".
- BuildRows: `row.RecordCount = s.RecordCount;` 를 추가하고, `row.CpkText` 는 새 private static `BuildCpkText(MeasurementStat s)` 로 채운다. s.N == 0 이면 NO_VALUE_TEXT, 아니면 기존 CpkToText(s.Cpk). 결과 없음 행이 "Cpk 0.000" 을 보이는 모순을 막기 위함이다. 평균/표준편차/범위 칸은 double 바인딩이라 이번에 바꾸지 않고 SUMMARY 에 기록한다.
- JudgeStatus: 기존 `NgCount > 0 → Bad` 바로 뒤에 `s.N == 0 → EStatLevel.NoResult` 가드를 넣는다. 나머지 판정은 불변이다. doc 에 "결과 없음은 불량 다음으로 나쁘다(데이터 누락 신호)" 한 줄을 적는다.
- BuildSummary(rows): switch 문에 `case EStatLevel.NoResult:` 를 추가한다(break 포함). 같은 case 안에서 row.RecordCount == 0 이면 nNoRecord 도 센다. 결과 없음 부분 문자열 szNoResultPart 를 먼저 만든다. 기본은 "결과 없음 " + nNoResult 이고, nNoRecord > 0 이면 뒤에 "(기록 없음 " + nNoRecord + ")" 를 붙인다. 최종 형식은 정확히 "전체 {T}항목 · 불량 {B} · " + szNoResultPart + " · 주의 {W} · 정상 {N}" 이다(가운뎃점은 기존과 같은 U+00B7, T 는 네 상태 합).
- IsProblemRow 는 `!= Normal` 이므로 NoResult 가 자동으로 포함된다. 코드 변경 없음을 확인만 한다. 재검사 결과 경로(ApplyRerunEnded)도 같은 presenter 를 쓰므로 자동 반영된다.

(D) StatisticsWindow.xaml
- RowStyle: 기존 Bad DataTrigger 와 Warning DataTrigger 사이에 `Binding="{Binding StatusLevel}" Value="NoResult"` DataTrigger 를 넣는다. Background #EDE9FE, Foreground #5B21B6(보라 — 빨강/노랑과 구분). IsSelected Trigger 는 불변이다.
- Columns: "허용범위" 와 "N" 사이에 `DataGridTextColumn Header="기록(틱)" Binding="{Binding RecordCount}" Width="60"` 을 추가한다. 보이는 칸 합은 약 1190px 로 1280 창에 들어간다.
- 헤더 툴팁은 각 칸의 `DataGridTextColumn.HeaderStyle` → `Style TargetType="DataGridColumnHeader"` → `Setter Property="ToolTip"` 으로 단다.
  - 기록(틱): "기간 안 이 항목이 기록된 자동 검사 틱 수. 한 부품을 여러 z 틱으로 나눠 찍고 항목마다 한 틱에서만 재므로 N 보다 큰 것이 정상 — 누락 수가 아님"
  - N: "기간 안 측정값이 나온 횟수(OK + NG)"
  - 검출실패: "기준점 검출 실패(DATUM_FAIL) + 사진 없음(NO_IMAGE) 줄 수. 측정 알고리즘 실패는 CSV 에 따로 남지 않아 여기에 포함되지 않음"
- chk_ProblemOnly Content 를 "문제 항목만 보기 (불량·결과 없음·주의)" 로 바꾼다.
- 범례 TextBlock Text 를 "빨강 = 불량(NG 발생 또는 Cpk 1.0 미만) · 보라 = 결과 없음(기간 안 측정값 0건) · 노랑 = 주의(Cpk 1.0 ~ 1.33)" 로 바꾼다.

(E) 하네스 SP/IvtHarness.cs 확장. range 모드에서 `StatRowPresenter.BuildRows(result.Stats)` 로 만든 행을 Key 로 매칭한다. 항목 줄 끝에 `|REC=<RecordCount>|LEVEL=<StatusLevel>|CPK=<CpkText>` 를 붙이고, 마지막에 `SUMMARY|<StatRowPresenter.BuildSummary(rows)>` 를 추가한다. wholeday 출력 형식은 바꾸지 않는다(회귀 diff 재사용). 앱 재빌드 후 하네스를 재컴파일한다.

커밋: `git add WPF_Example/Custom/Sequence/Inspection/RepeatMeasurementStats.cs WPF_Example/Custom/Sequence/Inspection/MeasurementHistoryCsvLoader.cs WPF_Example/UI/Statistics/StatisticsWindow.xaml.cs WPF_Example/UI/Statistics/StatisticsWindow.xaml` → `feat(quick-260914-ivt): 통계 창 결과 없음 항목 표시 — 기록(틱) 칸, 보라 상태, 요약 개수, 기간 밖 항목 보존`.
  </action>
  <verify>
    <automated>cd /c/code/DataMeasurement && SP=/c/Users/admin/AppData/Local/Temp/claude/C--code-DataMeasurement/0da39e39-7e39-40eb-8182-41eca9b2accd/scratchpad/ivt && BASE=$(cat $SP/base.txt) && "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" WPF_Example/DatumMeasurement.csproj -p:Configuration=Debug -p:Platform=x64 -v:m > $SP/build_t2.log 2>&1; echo "build_errors=$(grep -cE 'error (CS|MC)' $SP/build_t2.log || true)"; [ WPF_Example/bin/x64/Debug/DatumMeasurement.exe -nt WPF_Example/Custom/Sequence/Inspection/RepeatMeasurementStats.cs ] && echo exe_fresh=1; FAIL=0; for F in WPF_Example/Custom/Sequence/Inspection/RepeatMeasurementStats.cs WPF_Example/Custom/Sequence/Inspection/MeasurementHistoryCsvLoader.cs WPF_Example/UI/Statistics/StatisticsWindow.xaml.cs; do D="$(git diff -U0 "$BASE" -- "$F" | grep '^+' | grep -v '^+++' || true)"; T=$(printf '%s\n' "$D" | grep -cE '\?[^\?]*:' || true); C=$(printf '%s\n' "$D" | grep -cF '??' || true); N=$(printf '%s\n' "$D" | grep -cF '?.' || true); S=$(printf '%s\n' "$D" | grep -cE 'switch.*=>' || true); H=$(printf '%s\n' "$D" | grep -cF 'hbk' || true); B=$(printf '%s\n' "$D" | grep -cE '^\+\s*(if\s*\(.*\)|else)\s+[^{/ ].*;\s*$' || true); echo "$F tern=$T coal=$C nullc=$N swx=$S hbk=$H nobrace=$B"; [ "$T$C$N$S$H$B" = "000000" ] || FAIL=1; done; X="$(git diff -U0 "$BASE" -- WPF_Example/UI/Statistics/StatisticsWindow.xaml | grep '^+' | grep -v '^+++' || true)"; echo "xaml_hbk=$(printf '%s\n' "$X" | grep -cF hbk || true) noresult_trigger=$(printf '%s\n' "$X" | grep -c 'Value="NoResult"' || true) rec_col=$(printf '%s\n' "$X" | grep -c 'Binding RecordCount' || true)"; echo "rule_fail=$FAIL"; cd $SP && "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/Roslyn/csc.exe" -nologo -platform:x64 -out:IvtHarness.exe -r:C:/code/DataMeasurement/WPF_Example/bin/x64/Debug/DatumMeasurement.exe IvtHarness.cs && ./IvtHarness.exe wholeday wd.txt && LC_ALL=C sort wd.txt > wd_a.txt && diff wd_a.txt wd_b.txt && echo wholeday_identical=1 && ./IvtHarness.exe range r1.txt 20260914 0000 20260914 2359 && ./IvtHarness.exe range r2.txt 20260914 0950 20260914 0959 && ./IvtHarness.exe range r3.txt 20260914 1320 20260914 1329 && ./IvtHarness.exe range r4.txt 20260914 1000 20260914 1259 && ./IvtHarness.exe range r5.txt 20260914 1330 20260914 1320 && echo "r1 N12=$(grep -c '|N=12|' r1.txt) N13=$(grep -c '|N=13|' r1.txt) rec92bad=$(grep -c '|REC=92|LEVEL=Bad|' r1.txt)" && grep -qxF 'SUMMARY|전체 9항목 · 불량 9 · 결과 없음 0 · 주의 0 · 정상 0' r1.txt && echo r1_summary_ok=1 && echo "r2 rec40none=$(grep -c '|N=0|.*|REC=40|LEVEL=NoResult|CPK=-$' r2.txt)" && grep -qxF 'SUMMARY|전체 9항목 · 불량 0 · 결과 없음 9 · 주의 0 · 정상 0' r2.txt && echo r2_summary_ok=1 && echo "r3 rec40bad=$(grep -c '|N=10|.*|REC=40|LEVEL=Bad|' r3.txt)" && grep -qxF 'SUMMARY|전체 9항목 · 불량 9 · 결과 없음 0 · 주의 0 · 정상 0' r3.txt && echo r3_summary_ok=1 && echo "r4 rec0none=$(grep -c '|N=0|.*|REC=0|LEVEL=NoResult|CPK=-$' r4.txt) $(grep -E '^(TOTAL|RECIPES)\|' r4.txt | tr '\n' ' ')" && grep -qxF 'SUMMARY|전체 9항목 · 불량 0 · 결과 없음 9(기록 없음 9) · 주의 0 · 정상 0' r4.txt && echo r4_summary_ok=1 && echo "r5 keys=$(grep -c '|N=' r5.txt || true)" && grep -qxF 'SUMMARY|전체 0항목 · 불량 0 · 결과 없음 0 · 주의 0 · 정상 0' r5.txt && echo r5_summary_ok=1</automated>
  </verify>
  <done>
- build_errors=0, exe_fresh=1, 세 .cs 파일 grep 6종 0(rule_fail=0), xaml_hbk=0, noresult_trigger=1, rec_col=1.
- wholeday_identical=1 재확인. 기간 밖 항목 보존이 하루 전체 결과를 바꾸지 않는다.
- r1: N12=6, N13=3, rec92bad=9, r1_summary_ok=1. r2: rec40none=9, r2_summary_ok=1. r3: rec40bad=9, r3_summary_ok=1. r4: rec0none=9, `TOTAL|0`, `RECIPES|` (빈 목록), r4_summary_ok=1. r5: keys=0, r5_summary_ok=1.
- 커밋 1개(네 파일만).
  </done>
</task>

<task type="auto">
  <name>Task 3: 같은 기간을 저장 사진 재검사 · CPK export 에 적용 — QueryCycles 행 필터, BuildPlan InspectionTime 필터, export 파일명 시각 포함</name>
  <files>WPF_Example/Custom/Sequence/Inspection/MeasurementHistoryCsvLoader.cs, WPF_Example/Custom/Sequence/Inspection/RepeatRunService.cs, WPF_Example/UI/Statistics/StatisticsWindow.xaml.cs</files>
  <action>
계획 시점 코드 확인 결과(R3): export 는 `QueryCycles` 가 CSV 를 날짜 파일 단위로 전부 재조립하고, 재검사는 `SavedCycleRerunPlanner.CollectAutoTicks` 가 `ResultSavePath\yyyyMMdd` 날짜 폴더의 cycle.json 을 전부 모은다. 둘 다 시각 필터가 없다. 재검사의 "원래 통계" 비교는 `Query` 를 쓰므로 Task 1 에서 이미 기간 오버로드로 바꿀 수 있다.

(A) MeasurementHistoryCsvLoader.cs — QueryCycles 기간 필터.
- 기존 `QueryCycles(DateTime dtFrom, DateTime dtTo, string szRecipeFilter)` 본문을 `return QueryCycles(StatisticsTimeRange.FromDates(dtFrom, dtTo), szRecipeFilter);` 로 바꾼다.
- 새 `public static List<CycleResultDto> QueryCycles(StatisticsTimeRange range, string szRecipeFilter)`: try 로 SystemHandler 경로를 읽고, 실패하면 로그 후 빈 리스트를 반환한다. 성공하면 `QueryCyclesDirectory(szDir, range, szRecipeFilter)` 를 반환한다.
- 새 `public static List<CycleResultDto> QueryCyclesDirectory(string szDir, StatisticsTimeRange range, string szRecipeFilter)`: 기존 본문을 옮긴다. 가드는 range null / szDir 비어 있음 / range.IsEmpty 이고, 날짜 루프는 range.FirstDate..LastDate 이며, try/catch 격리를 유지한다.
- CycleGroupState 에 `public StatisticsTimeRange Range;` 를 추가하고 QueryCyclesDirectory 에서 설정한다.
- ProcessCycleRow: IsManualRow 가드 뒤, IsNewCycleBoundary 전에 szTime 을 TryParseInspectionTime 으로 파싱한다. 실패면 return, `state.Range.Contains(dtRow)` 가 아니면 return. 경계 상태(LastTime 등)를 갱신하기 전에 빠지므로, 수동 행 skip 과 같은 방식이라 경계 판정이 유지된다.
- 새 사이클 생성 시 `cycle.InspectionTime = dtRow;` 로 한다. 이제 호출처가 없는 `ParseInspectionTime` 은 삭제하고, 삭제 후 `grep -n ParseInspectionTime` 에 TryParseInspectionTime 만 남는지 확인한다.

(B) RepeatRunService.cs — SavedCycleRerunPlanner (Allman 구간, H11 에 따라 Phase 76 무관 파일임을 확인함).
- BuildPlan 시그니처를 `BuildPlan(StatisticsTimeRange range, string szRecipeName, InspectionSequence seq, InspectionRecipeManager recipeManager)` 로 교체한다(유일한 호출처는 이 태스크 (C)에서 바꾼다).
- 인자 가드는 H2 에 맞게 이름 있는 bool 두 개로 나눈다. `bMissingInputs` = seq null 또는 recipeManager null 또는 레시피명 빈값. `bInvalidRange` = range null 또는 range.IsEmpty. 둘 중 하나면 빈 plan 을 반환한다.
- CollectAutoTicks 시그니처를 `(StatisticsTimeRange range, string szRecipeName, InspectionSequence seq)` 로 바꾼다. 날짜 루프는 range.FirstDate..range.LastDate 로 한다. 폴더마다 Load 후, 기존 `if (IsEligibleAutoTick(...)) { Add }` 를 가드 두 개로 바꾼다: 자격이 없으면 continue, `!range.Contains(dto.InspectionTime)` 이면 continue, 그다음 Add. IsEligibleAutoTick 이 null 을 먼저 거르므로 Contains 호출 시 dto 는 null 이 아니다. if 중첩 단계가 늘지 않게 한다.
- 메서드 앞 주석에 "cycle.json InspectionTime(소수 초 포함) 이 기간 안인 tick 만" 을 한 줄 덧붙인다.
- 기간 경계가 부품 중간을 자르는 경우는 기존 규칙으로 처리되고 새 분기는 없다. From 이 부품 중간이면 첫 기준점 tick 이전 tick 이 "기준점 촬영 없이 시작된 사이클" 제외 사유가 되고, To 가 부품 중간이면 ValidatePart 의 Shot 누락 사유가 된다. 이 동작을 SUMMARY 에 기록한다.

(C) StatisticsWindow.xaml.cs — VM 과 배선.
- StatisticsRerunViewModel.TryStartRerun 시그니처를 `(StatisticsTimeRange range, string szRecipeFilter, out string szError)` 로 바꾼다.
  - 기존 가드 맨 앞(IsRerunning 검사 뒤)에 가드를 추가한다: range 가 null 이거나 IsEmpty 면 `szError = StatisticsPeriodViewModel.INVALID_RANGE_TEXT; return false;`.
  - 시작 성공 경로(IsRerunning = true 대입 직전)에서 `_rerunRange = range;` 를 저장한다(새 private 필드 `StatisticsTimeRange _rerunRange`).
  - Task.Run 안 두 호출을 `SavedCycleRerunPlanner.BuildPlan(range, szCurrentRecipe, seq, recipeManager)` 와 `MeasurementHistoryCsvLoader.Query(range, szCurrentRecipe)` 로 바꾼다.
- GetCyclesForExport 시그니처를 `(StatisticsTimeRange range, string szRecipeFilter)` 로 바꾼다. 재검사 결과를 보고 있으면 기존대로 RerunCycles, 아니면 `MeasurementHistoryCsvLoader.QueryCycles(range, szRecipeFilter)`.
- 새 `public StatisticsTimeRange GetExportRange(StatisticsTimeRange currentRange)`: 재검사 결과를 보고 있고 _rerunRange 가 null 이 아니면(이름 있는 bool `bUseRerunRange`) _rerunRange, 아니면 currentRange. 재검사 뒤 시각을 바꾸고 export 해도 파일명이 실제 담긴 사이클의 기간과 맞게 하기 위함이다.
- Btn_Rerun_Click: GetSelectedRange 호출과 두 지역변수를 지우고 `m_rerunVm.TryStartRerun(m_periodVm.BuildRange(), szRecipeFilter, out szError)` 를 호출한다.
- Btn_CpkExport_Click:
  - 먼저 `StatisticsTimeRange range = m_periodVm.BuildRange();` 로 기간을 만들고, `m_rerunVm.GetCyclesForExport(range, szRecipeFilter)` 를 호출한다.
  - 이어서 `StatisticsTimeRange exportRange = m_rerunVm.GetExportRange(range);` 를 구한다.
  - SaveFileDialog FileName 은 `m_rerunVm.GetExportFilePrefix() + exportRange.BuildFileStamp() + EXPORT_FILE_EXT` 로 만든다. `EXPORT_FILE_EXT = ".xlsx"` 는 StatisticsWindow 의 private const 로 추가한다.
  - 하루 전체면 파일명이 기존과 글자까지 같다.
- 이제 호출처가 없는 GetSelectedRange 메서드를 삭제한다. 삭제 후 `grep -n "GetSelectedRange\|dp_From\.SelectedDate\|dp_To\.SelectedDate" StatisticsWindow.xaml.cs` 가 0건인지 확인한다.

(D) 하네스 SP/IvtHarness.cs 확장(H8 — BuildPlan 은 SystemHandler/시퀀스가 필요해 호출하지 않는다).
- range 모드에 기록 줄을 추가한다.
  - `CYCLES|<QueryCyclesDirectory(dir, range, "").Count>`
  - `STAMP|<range.BuildFileStamp()>`
  - `TICKS|<n>`: range.FirstDate..LastDate 의 `D:\Data\Result\yyyyMMdd\*_cycle\cycle.json` 마다 `CycleResultSerializer.Load` 를 호출하고, 결과가 null 이 아니며 IsProtocolDriven 이고 `range.Contains(dto.InspectionTime)` 인 개수를 센다. 재검사 계획이 쓰는 시각 판정 입력(소수 초 + 오프셋 역직렬화)을 실데이터로 검증하기 위함이다.
- 새 모드 `contains <out>`: 아래 9줄을 정확히 이 형식으로 쓴다.
  - 13:20~13:29 구간 R = FromParts(2026-09-14, 13, 20, 2026-09-14, 13, 29) 에 대해:
    - `C|13:20:00|<R.Contains(2026-09-14 13:20:00)>`
    - `C|13:19:59|<…>`
    - `C|13:29:59|<…>`
    - `C|13:29:59.999|<…>` (new DateTime(2026,9,14,13,29,59,999))
    - `C|13:30:00|<…>`
  - W = FromDates(2026-09-14, 2026-09-14) 에 대해:
    - `W|00:00:00|<W.Contains(2026-09-14 00:00:00)>`
    - `W|23:59:59.999|<…>`
    - `W|next00:00:00|<W.Contains(2026-09-15 00:00:00)>`
  - K = FromParts(2026-09-14, -1, -1, 2026-09-14, -1, -1) 에 대해:
    - `K|<K.FromInclusive:yyyy-MM-dd HH:mm>|<K.ToMinute:yyyy-MM-dd HH:mm>|<K.IsWholeDays>`
- 새 모드 `stamps <out>`: 다음 4줄을 쓴다.
  - `S1|` + FromDates(0914,0914).BuildFileStamp()
  - `S2|` + FromParts(0914,9,50,0914,9,59)
  - `S3|` + FromDates(0913,0914)
  - `S4|` + FromParts(0914,0,0,0914,12,0)
- 앱 재빌드 후 하네스를 재컴파일한다.

커밋: `git add WPF_Example/Custom/Sequence/Inspection/MeasurementHistoryCsvLoader.cs WPF_Example/Custom/Sequence/Inspection/RepeatRunService.cs WPF_Example/UI/Statistics/StatisticsWindow.xaml.cs` → `feat(quick-260914-ivt): 통계 창 기간(시:분)을 저장 사진 재검사·CPK export 에 적용 — 파일명 시각 포함`.
  </action>
  <verify>
    <automated>cd /c/code/DataMeasurement && SP=/c/Users/admin/AppData/Local/Temp/claude/C--code-DataMeasurement/0da39e39-7e39-40eb-8182-41eca9b2accd/scratchpad/ivt && BASE=$(cat $SP/base.txt) && "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" WPF_Example/DatumMeasurement.csproj -p:Configuration=Debug -p:Platform=x64 -v:m > $SP/build_t3.log 2>&1; echo "build_errors=$(grep -cE 'error (CS|MC)' $SP/build_t3.log || true)"; [ WPF_Example/bin/x64/Debug/DatumMeasurement.exe -nt WPF_Example/Custom/Sequence/Inspection/RepeatRunService.cs ] && echo exe_fresh=1; FAIL=0; for F in WPF_Example/Custom/Sequence/Inspection/RepeatMeasurementStats.cs WPF_Example/Custom/Sequence/Inspection/MeasurementHistoryCsvLoader.cs WPF_Example/Custom/Sequence/Inspection/RepeatRunService.cs WPF_Example/UI/Statistics/StatisticsWindow.xaml.cs; do D="$(git diff -U0 "$BASE" -- "$F" | grep '^+' | grep -v '^+++' || true)"; T=$(printf '%s\n' "$D" | grep -cE '\?[^\?]*:' || true); C=$(printf '%s\n' "$D" | grep -cF '??' || true); N=$(printf '%s\n' "$D" | grep -cF '?.' || true); S=$(printf '%s\n' "$D" | grep -cE 'switch.*=>' || true); H=$(printf '%s\n' "$D" | grep -cF 'hbk' || true); B=$(printf '%s\n' "$D" | grep -cE '^\+\s*(if\s*\(.*\)|else)\s+[^{/ ].*;\s*$' || true); echo "$F tern=$T coal=$C nullc=$N swx=$S hbk=$H nobrace=$B"; [ "$T$C$N$S$H$B" = "000000" ] || FAIL=1; done; echo "rule_fail=$FAIL"; echo "leftover_refs=$(grep -cE 'GetSelectedRange|dp_From\.SelectedDate|dp_To\.SelectedDate|ParseInspectionTime\(szTime\)' WPF_Example/UI/Statistics/StatisticsWindow.xaml.cs WPF_Example/Custom/Sequence/Inspection/MeasurementHistoryCsvLoader.cs | awk -F: '{s+=$2} END{print s+0}')"; echo "range_calls=$(grep -c 'm_periodVm.BuildRange()' WPF_Example/UI/Statistics/StatisticsWindow.xaml.cs)"; echo "changed_files=$(git diff --name-only "$BASE" -- WPF_Example | tr '\n' ' ')"; cd $SP && "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/Roslyn/csc.exe" -nologo -platform:x64 -out:IvtHarness.exe -r:C:/code/DataMeasurement/WPF_Example/bin/x64/Debug/DatumMeasurement.exe IvtHarness.cs && ./IvtHarness.exe wholeday wd.txt && LC_ALL=C sort wd.txt > wd_a.txt && diff wd_a.txt wd_b.txt && echo wholeday_identical=1 && ./IvtHarness.exe range r1.txt 20260914 0000 20260914 2359 && ./IvtHarness.exe range r2.txt 20260914 0950 20260914 0959 && ./IvtHarness.exe range r3.txt 20260914 1320 20260914 1329 && ./IvtHarness.exe range r4.txt 20260914 1000 20260914 1259 && ./IvtHarness.exe range r5.txt 20260914 1330 20260914 1320 && for r in r1 r2 r3 r4 r5; do echo "$r $(grep -E '^(CYCLES|TICKS|STAMP)\|' $r.txt | tr '\n' ' ')"; done && ./IvtHarness.exe contains c.txt && printf '%s\n' 'C|13:20:00|True' 'C|13:19:59|False' 'C|13:29:59|True' 'C|13:29:59.999|True' 'C|13:30:00|False' 'W|00:00:00|True' 'W|23:59:59.999|True' 'W|next00:00:00|False' 'K|2026-09-14 00:00|2026-09-14 23:59|True' > c_expect.txt && diff c.txt c_expect.txt && echo contains_ok=1 && ./IvtHarness.exe stamps s.txt && printf '%s\n' 'S1|20260914_20260914' 'S2|20260914_0950_20260914_0959' 'S3|20260913_20260914' 'S4|20260914_0000_20260914_1200' > s_expect.txt && diff s.txt s_expect.txt && echo stamps_ok=1</automated>
  </verify>
  <done>
- build_errors=0, exe_fresh=1, 네 .cs 파일 grep 6종 0(rule_fail=0), leftover_refs=0, range_calls=3(DoQuery · Btn_Rerun_Click · Btn_CpkExport_Click).
- changed_files 는 계획한 5개 파일뿐. MeasurementHistoryCsvWriter.cs, Phase 76 파일, ReviewerWindow, csproj 는 없어야 한다.
- wholeday_identical=1 재확인.
- r1: `CYCLES|92 TICKS|92 STAMP|20260914_20260914`. r2: `CYCLES|40 TICKS|40 STAMP|20260914_0950_20260914_0959`. r3: `CYCLES|40 TICKS|40 STAMP|20260914_1320_20260914_1329`. r4: `CYCLES|0 TICKS|0`. r5: `CYCLES|0 TICKS|0`.
- TICKS 가 계획 시점 폴더명 기준 사전 계수(40/40/92)와 다르면 실패로 멈추지 말고, 원인(폴더 시각과 InspectionTime 불일치, 역직렬화 Kind)을 조사해 SUMMARY 에 실측값과 근거를 적는다. 판정 로직 오류면 수정한다.
- contains_ok=1, stamps_ok=1.
- 커밋 1개(세 파일만).
  </done>
</task>

</tasks>

<threat_model>
## Trust Boundaries

| Boundary | Description |
|----------|-------------|
| 로컬 통계 CSV / cycle.json → 통계 창·재검사 계획 | 로컬 디스크 파일을 읽기만 한다. 네트워크 입력 없음. 손상·수기 편집된 파일이 들어올 수 있다 |
| 사용자 기간 선택 → 조회/재검사/export | ComboBox 선택값(미선택 -1 포함)과 DatePicker 텍스트 입력 |

## STRIDE Threat Register

| Threat ID | Category | Component | Severity | Disposition | Mitigation Plan |
|-----------|----------|-----------|----------|-------------|-----------------|
| T-ivt-01 | Tampering | ProcessRow / ProcessCycleRow 검사일시 파싱 | medium | mitigate | TryParseExact 실패 행은 건너뜀. 기존 fields.Count/헤더 가드와 파일 단위 try/catch 격리 유지 — 손상 행 1개가 조회 전체를 막지 않음 |
| T-ivt-02 | Information disclosure (오판 표시) | 기록(틱)/검출실패/결과 없음 지표 | high | mitigate | 기록(틱) 은 누락 수가 아님을 헤더 툴팁에 명시, 비율·부품 수 칸 미생성, 검출실패 한계(MEASURE_FAIL 은 NO_RESULT 로 기록) 툴팁 명시, N=0 행 Cpk "-" |
| T-ivt-03 | Information disclosure (조용한 빈 결과) | From > To 선택 | medium | mitigate | StatisticsTimeRange.IsEmpty → 요약 줄 INVALID_RANGE_TEXT, 재검사는 메시지 박스로 거부 |
| T-ivt-04 | Denial of service | ComboBox SelectedIndex -1 / DatePicker 빈 값 | low | mitigate | FromParts 의 ClampOrFallback(From 0, To 23/59), 날짜 null 은 오늘로 폴백 — 예외 없음 |
| T-ivt-05 | Repudiation (export 기간 불일치) | 재검사 후 기간 변경 → export | low | mitigate | GetExportRange 가 재검사를 시작한 기간으로 파일명 생성 |
| T-ivt-06 | Tampering (운영 데이터) | 스크래치 하네스 | high | mitigate | 하네스는 경로 주입 읽기 메서드만 호출, SystemHandler 경로 금지(H8), 출력은 SP 폴더에만 |
| T-ivt-SC | Tampering | 패키지 설치 | low | accept | 신규 패키지·설치 없음 |
</threat_model>

<verification>
- Task 1~3 의 automated 체인 전부 통과. 마지막 Task 3 체인이 누적 검증이다: 네 .cs 추가 줄 하드룰 0, 하루 단위 회귀 diff 동일, 기간별 N/기록/상태/요약/사이클/tick/파일명/경계 판정.
- `git diff --name-only BASE..HEAD` = 계획한 5개 코드 파일 + (output 단계의) `.planning/STATE.md` 와 SUMMARY 뿐. `git status` 에서 csproj 미스테이징.
- Release 빌드·배포·앱 실행 없음(H9).
</verification>

<success_criteria>
- R1: 날짜 + 시:분 선택, 기본 00:00~23:59, 시간 미변경 시 결과 불변(12개 CSV awk 대조), From > To 방어 + 표시.
- R2: 행 단위 검사일시 필터(통계·추이·레시피 목록), 파싱 실패 행 skip, 날짜만 받는 Query/QueryCycles 오버로드는 하루 전체로 동작 유지.
- R3: 재검사 계획(cycle.json InspectionTime) · 원래 통계 비교 · CPK export(CSV 사이클) 가 같은 기간을 쓰고, export 파일명은 하루 전체면 기존 형식, 아니면 시각 포함.
- R4: 결과 없음 보라 행(불량 다음 정렬, 문제 필터 포함), 기록(틱) 칸 + 툴팁, 검출실패 정의 툴팁, 요약 "결과 없음 R(기록 없음 X)". 부품 수는 근거와 함께 미도입.
- 커밋 3개(태스크별), 하드룰 위반 0.
</success_criteria>

## Source Coverage Audit

| Source item | Plan coverage |
|-------------|---------------|
| 요구 1 기간 날짜+시:분 · 기본 00:00/23:59 · 결과 불변 · From>To 방어 | Task 1 (A)(C)(D)(E), wholeday 회귀 diff, r5 |
| 요구 2 행 단위 시간 필터 · 파싱 실패 skip · 날짜 호출 불변 | Task 1 (B), Task 3 (A) |
| 요구 3 재검사 · CPK export 같은 기간 · 파일명 규칙 | Task 3 (A)(B)(C), stamps/contains/TICKS/CYCLES |
| 요구 4 N=0 항목 유지·구분 표시 · 기록 대비 결과 · 명시적 실패 수 · 요약 추가 · 오해 숫자 금지 | Task 2 전체 + "지표 정의와 근거" |
| 요구 5 범위 밖(삭제/보관, ReviewerWindow, Phase 76) | H11, changed_files 검사 |
| 하드룰 / 금지 / 검증 방식 | hard_rules H1~H10, 각 verify 체인, output 의 STATE 규칙 |

## Artifacts this task produces

- 코드(커밋 3개): `MeasurementHistoryCsvLoader.cs` (StatisticsTimeRange, 기간 오버로드, QueryDirectory/QueryCyclesDirectory, 행 시각 필터, 기간 밖 항목 보존), `RepeatMeasurementStats.cs` (RecordCount), `RepeatRunService.cs` (BuildPlan 기간 필터), `StatisticsWindow.xaml.cs` (StatisticsPeriodViewModel, EStatLevel.NoResult, 요약/판정/배선), `StatisticsWindow.xaml` (시/분 ComboBox, 보라 트리거, 기록(틱) 칸, 툴팁, 범례/안내 줄).
- 스크래치(커밋 안 함): `SP/base.txt`, `SP/IvtHarness.cs`/`.exe`, `SP/wd_*.txt`, `SP/r1~r5.txt`, `SP/c*.txt`, `SP/s*.txt`, `SP/build_t1~t3.log`.
- 문서: `.planning/quick/260914-ivt-statistics-window-date-time-range-filter/260914-ivt-SUMMARY.md`, `.planning/STATE.md` Quick Tasks Completed 표 1행.

<output>
코드 태스크 3개가 끝나면:

1. `.planning/quick/260914-ivt-statistics-window-date-time-range-filter/260914-ivt-SUMMARY.md` 를 작성한다. 포함할 내용:
   - BASE 커밋, 커밋 해시 3개, 태스크별 verify 출력 요약(실측 숫자 그대로).
   - "지표 정의와 근거" 표 전문과 항목 목록 범위 결정.
   - 결정 기록:
     - 결과 없음 행의 평균/표준편차/범위는 0.0000 표시가 유지된다(double 바인딩).
     - 기간 경계가 부품 중간일 때 재검사 제외 사유가 기존 규칙으로 처리된다.
     - 레시피 콤보 목록은 기간 안 줄 기준이다.
     - 재검사 결과 export 파일명은 재검사를 시작한 기간을 따른다.
     - XAML 에는 삼항 grep 을 적용하지 않는다(C# 조건 연산자 규칙 대상 아님).
   - 후속 제안: MEASURE_FAIL 등 항목 단위 실패를 CSV Judgement 로 남기려면 writer 와 로더 "그 외=NG" 분기를 함께 바꿔야 한다.
   - Release 배포 빌드는 수행하지 않았음(오케스트레이터 몫).

   **실기 UAT 대기** 체크리스트(미체크, 사용자가 장비에서 수행):
   - [ ] 1. 통계 창을 열면 기간이 오늘 00:00 ~ 오늘 23:59 이고, 표 숫자·요약이 이전 버전과 같다(20260914 기준 C13·C14 계열 N=12, F9 계열 N=13, 요약 "전체 9항목 · 불량 9 · 결과 없음 0 · 주의 0 · 정상 0").
   - [ ] 2. From 09:50 · To 09:59 로 조회하면 SIDE_1 9개 항목이 모두 보라색 "결과 없음", N 0, 기록(틱) 40, Cpk "-". 요약 "결과 없음 9".
   - [ ] 3. From 13:20 · To 13:29 로 조회하면 항목당 N 10, 기록(틱) 40. 행 클릭 시 히스토그램/추이가 10개 값으로 그려진다.
   - [ ] 4. From 10:00 · To 12:59 로 조회하면 9개 항목이 결과 없음(기록 0), 요약에 "(기록 없음 9)" 가 보이고 CPK export 버튼이 비활성이다.
   - [ ] 5. From 13:30 · To 13:20 처럼 거꾸로 고르면 요약 줄이 "기간 오류: 시작이 끝보다 늦습니다", 재검사 버튼은 같은 문구 메시지 박스로 거부된다.
   - [ ] 6. "문제 항목만 보기" 체크 시 보라 행이 남고, 정렬이 불량 → 결과 없음 → 주의 → 정상 순이다. 기록(틱)/N/검출실패 헤더에 마우스를 올리면 설명 툴팁이 보인다.
   - [ ] 7. 기간 13:20~13:29 로 "저장 사진으로 재검사"를 실행하면 진행 표시의 부품 수가 그 구간 부품만이다(하루 전체로 했을 때보다 적다). 결과 표의 원래 평균이 같은 구간 통계와 맞는다.
   - [ ] 8. 기간 13:20~13:29 에서 CPK export 를 누르면 기본 파일명이 cpk_report_20260914_1320_20260914_1329.xlsx 이고, 파일에 그 구간 사이클만 담긴다. 하루 전체면 cpk_report_20260914_20260914.xlsx(기존과 같음). 재검사 결과를 본 상태에서 시간을 바꾼 뒤 export 해도 파일명은 재검사한 기간이다.
   - [ ] 9. 레시피 콤보, 행 선택 차트, 원래 통계로 버튼 등 기존 기능이 그대로 동작한다.

2. `.planning/STATE.md` 의 "### Quick Tasks Completed" 표 헤더 구분선 바로 아래(최신이 위)에 행 1개를 **Edit 로 수동 추가**한다(gsd state 도구 사용 금지 — 무관한 필드를 바꾼 전력이 있음).
   - 형식: `| 260914-ivt | 2026-09-14 | **통계 창 기간을 날짜+시:분으로.** 행 단위 검사일시 필터(기본 00:00~23:59 = 기존과 동일), 같은 기간을 저장 사진 재검사(cycle.json InspectionTime)·CPK export(파일명 시각 포함)에 적용, 결과 없음 항목 보라 표시·기록(틱) 칸·요약 "결과 없음 R(기록 없음 X)". 부품 수는 CSV 로 신뢰성 있게 못 세서 미도입. | <해시 3개> | 코드 PASS · 하드룰 PASS · 빌드 PASS · 로더 실데이터 대조 PASS · **실기 UAT 대기** |`
   - 확인: `git diff -U0 -- .planning/STATE.md | grep -c '^+[^+]'` = 1, `git diff -U0 -- .planning/STATE.md | grep -c '^-[^-]'` = 0.

3. `git add .planning/quick/260914-ivt-statistics-window-date-time-range-filter/260914-ivt-SUMMARY.md .planning/STATE.md` 로 경로를 지정해 docs 커밋: `docs(quick-260914-ivt): SUMMARY + STATE 기록 — 실기 UAT 대기`.
</output>
