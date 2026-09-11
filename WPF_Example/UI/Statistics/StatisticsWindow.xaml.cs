//260707 hbk STAT-01: 양산 이력 통계 분석 UI — 조회/테이블/차트(WPF Canvas 직접 렌더) code-behind
//260707 hbk quick-260707-fdx ChartDirector(유료·워터마크) 제거 → 히스토그램/추이 차트를 WPF Canvas 도형으로 재구현
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using ReringProject.Sequence;
using ReringProject.Setting;   //260707 hbk 빌드오류(CS0103) 수정 — ELogType 이 ReringProject.Setting 네임스페이스에 정의됨
using ReringProject.Utility;

namespace ReringProject.UI
{
    /// <summary>통계 행 상태 3단계. 정수값이 그대로 "나쁜 순" 정렬 순위(작을수록 나쁨, Task 1 R1/R3).</summary>
    public enum EStatLevel
    {
        Bad = 0,
        Warning = 1,
        Normal = 2
    }

    /// <summary>
    /// 통계 조회 결과 1행 — DataGrid 바인딩용 화면 모델(MeasurementStat 을 화면 표시용으로 변환).
    /// </summary>
    public class StatRow
    {
        public string ShotName { get; set; }

        public string FAIName { get; set; }

        public string MeasurementName { get; set; }

        public int N { get; set; }

        public double Mean { get; set; }

        public double StdDev { get; set; }

        public double Range { get; set; }

        public string CpkText { get; set; }        //260707 hbk ∞/NaN 표시 처리

        public int OkCount { get; set; }

        public int NgCount { get; set; }

        public int DetectFailCount { get; set; }

        public string YieldRateText { get; set; }  //260707 hbk 수율 OK/(OK+NG) — 값 클수록 좋음(불량률 대체)

        public string Key { get; set; }             //260707 hbk Series 조인 키(Shot/FAI/측정명)

        public double NominalValue { get; set; }

        public double TolerancePlus { get; set; }

        public double ToleranceMinus { get; set; }

        /// <summary>행 상태(R1) — RowStyle DataTrigger 바인딩 대상.</summary>
        public EStatLevel StatusLevel { get; set; }

        /// <summary>Cpk 칸 헤더 정렬용 숫자 키(R3, SortMemberPath 대상).</summary>
        public double CpkSortValue { get; set; }

        /// <summary>허용범위 칸 "하한 ~ 상한"(R4).</summary>
        public string ToleranceRangeText { get; set; }

        /// <summary>벗어난 양 칸 표시 문자열(R4).</summary>
        public string OutOfRangeText { get; set; }

        /// <summary>벗어난 양 칸 헤더 정렬용 숫자 키(R4). 범위 안/판정불가 = 0.</summary>
        public double OutOfRangeAmount { get; set; }

        /// <summary>quick-260911-fia Task 4: 재검사 표 전용 — 같은 키의 원래(재검사 전) 평균 표시. 비교 불가면 "-".</summary>
        public string OriginalMeanText { get; set; }

        /// <summary>quick-260911-fia Task 4: 재검사 평균 − 원래 평균 표시. 비교 불가면 "-".</summary>
        public string DeltaText { get; set; }

        /// <summary>quick-260911-fia Task 4: 변화 칸 헤더 정렬용 숫자 키 — 변화 크기(절댓값) 순 정렬.</summary>
        public double DeltaSortValue { get; set; }
    }

    /// <summary>
    /// 통계 표 1행의 상태 판정·표시 문자열·정렬 키·요약·필터를 계산하는 순수 정적 헬퍼 (UI 비의존).
    /// </summary>
    public static class StatRowPresenter
    {
        private const double CPK_BAD_LIMIT = 1.0;              // Cpk 이 값 미만이면 불량
        private const double CPK_WARN_LIMIT = 1.33;            // Cpk 이 값 미만이면 주의
        private const int MIN_SAMPLES_FOR_CPK = 2;             // 표본 1개 이하면 산포가 없어 Cpk 판정에 쓰지 않음
        private const double CPK_SORT_INFINITE = double.MaxValue;           // Cpk ∞(산포 0) 정렬 키 — 모든 유한값 뒤
        private const double CPK_SORT_NOT_COMPUTABLE = double.PositiveInfinity; // 계산불가 정렬 키 — ∞ 보다도 뒤(맨 뒤)
        private const string VALUE_FORMAT = "F4";
        private const string CPK_FORMAT = "F3";
        private const string YIELD_FORMAT = "F2";
        private const double PERCENT_SCALE = 100.0;
        private const string NO_VALUE_TEXT = "-";
        private const string UPPER_PREFIX = "상한 +";
        private const string LOWER_PREFIX = "하한 −";           // 유니코드 마이너스(U+2212)
        private const string RANGE_SEPARATOR = " ~ ";

        /// <summary>Stats 딕셔너리(Shot/FAI/측정명 키)를 DataGrid 바인딩용 화면 행 리스트로 변환한다.</summary>
        public static List<StatRow> BuildRows(Dictionary<string, MeasurementStat> stats)
        {
            var rows = new List<StatRow>();
            if (stats == null)
            {
                return rows;
            }

            foreach (var kv in stats)
            {
                MeasurementStat s = kv.Value;
                var row = new StatRow();
                row.Key = kv.Key;
                row.ShotName = s.ShotName;
                row.FAIName = s.FAIName;
                row.MeasurementName = s.MeasurementName;
                row.N = s.N;
                row.Mean = s.Mean;
                row.StdDev = s.StdDev;
                row.Range = s.Range;
                row.CpkText = CpkToText(s.Cpk);
                row.OkCount = s.OkCount;
                row.NgCount = s.NgCount;
                row.DetectFailCount = s.DetectFailCount;
                row.YieldRateText = YieldRateToText(s.OkCount, s.NgCount);   // 불량률→수율 긍정지표 전환
                row.NominalValue = s.NominalValue;
                row.TolerancePlus = s.TolerancePlus;
                row.ToleranceMinus = s.ToleranceMinus;
                row.StatusLevel = JudgeStatus(s);
                row.CpkSortValue = GetCpkSortValue(s);
                row.ToleranceRangeText = BuildToleranceRangeText(s);
                FillOutOfRange(row, s);
                rows.Add(row);
            }

            return rows;
        }

        /// <summary>Cpk 표시 문자열 — 무한대/NaN 방어(if/else, 삼항 금지).</summary>
        public static string CpkToText(double dCpk)
        {
            if (double.IsPositiveInfinity(dCpk))
            {
                return "∞";   // ∞
            }

            if (double.IsNegativeInfinity(dCpk) || double.IsNaN(dCpk))
            {
                return NO_VALUE_TEXT;
            }

            return dCpk.ToString(CPK_FORMAT);
        }

        /// <summary>수율(Yield, %) 표시 문자열 = OK/(OK+NG). 값 클수록 좋음. 분모 0 방어(if/else, 삼항 금지).</summary>
        public static string YieldRateToText(int nOk, int nNg)
        {
            int nTotal = nOk + nNg;
            if (nTotal == 0)
            {
                return NO_VALUE_TEXT;
            }

            double d = nOk * PERCENT_SCALE / nTotal;
            return d.ToString(YIELD_FORMAT) + "%";
        }

        /// <summary>공차 미설정이면 Cpk 가 0 이하로 계산돼(USL=LSL=Nominal) 거짓 불량이 되므로 판정에서 제외한다.</summary>
        private static bool IsCpkUsable(MeasurementStat s)
        {
            bool bTooFewSamples = s.N < MIN_SAMPLES_FOR_CPK;
            bool bNotFinite = double.IsInfinity(s.Cpk) || double.IsNaN(s.Cpk);
            bool bNoTolerance = HasNoTolerance(s);

            if (bTooFewSamples)
            {
                return false;
            }

            if (bNotFinite)
            {
                return false;
            }

            if (bNoTolerance)
            {
                return false;
            }

            return true;
        }

        private static bool HasNoTolerance(MeasurementStat s)
        {
            return s.TolerancePlus == 0.0 && s.ToleranceMinus == 0.0;
        }

        /// <summary>행 상태 판정(R1). NG 1건이라도 있으면 항상 불량 — 은폐 방향 판정 없음.</summary>
        public static EStatLevel JudgeStatus(MeasurementStat s)
        {
            if (s.NgCount > 0)
            {
                return EStatLevel.Bad;
            }

            if (!IsCpkUsable(s))
            {
                return EStatLevel.Normal;   // ∞/계산불가/공차 미설정/표본 부족 → 수율만으로 판정
            }

            if (s.Cpk < CPK_BAD_LIMIT)
            {
                return EStatLevel.Bad;
            }

            if (s.Cpk < CPK_WARN_LIMIT)
            {
                return EStatLevel.Warning;
            }

            return EStatLevel.Normal;
        }

        /// <summary>Cpk 헤더 정렬용 숫자 키(R3). 판정과 일관되게 계산불가 계열은 맨 뒤로 보낸다.</summary>
        public static double GetCpkSortValue(MeasurementStat s)
        {
            if (double.IsPositiveInfinity(s.Cpk))
            {
                return CPK_SORT_INFINITE;
            }

            if (double.IsNegativeInfinity(s.Cpk) || double.IsNaN(s.Cpk))
            {
                return CPK_SORT_NOT_COMPUTABLE;
            }

            if (HasNoTolerance(s))
            {
                return CPK_SORT_NOT_COMPUTABLE;
            }

            if (s.N < MIN_SAMPLES_FOR_CPK)
            {
                return CPK_SORT_NOT_COMPUTABLE;
            }

            return s.Cpk;
        }

        /// <summary>허용범위 칸 문자열 "하한 ~ 상한"(R4). 공차 미설정이면 "-".</summary>
        public static string BuildToleranceRangeText(MeasurementStat s)
        {
            if (HasNoTolerance(s))
            {
                return NO_VALUE_TEXT;
            }

            double dLsl = s.NominalValue - Math.Abs(s.ToleranceMinus);
            double dUsl = s.NominalValue + s.TolerancePlus;
            return dLsl.ToString(VALUE_FORMAT) + RANGE_SEPARATOR + dUsl.ToString(VALUE_FORMAT);
        }

        /// <summary>벗어난 양 칸(R4) — Text/Amount 를 함께 채운다. 기본값은 범위 안/판정불가.</summary>
        private static void FillOutOfRange(StatRow row, MeasurementStat s)
        {
            row.OutOfRangeText = NO_VALUE_TEXT;
            row.OutOfRangeAmount = 0.0;

            if (HasNoTolerance(s))
            {
                return;
            }

            if (s.N == 0)
            {
                return;
            }

            double dLsl = s.NominalValue - Math.Abs(s.ToleranceMinus);
            double dUsl = s.NominalValue + s.TolerancePlus;

            if (s.Mean > dUsl)
            {
                row.OutOfRangeText = UPPER_PREFIX + (s.Mean - dUsl).ToString(VALUE_FORMAT);
                row.OutOfRangeAmount = s.Mean - dUsl;
            }
            else if (s.Mean < dLsl)
            {
                row.OutOfRangeText = LOWER_PREFIX + (dLsl - s.Mean).ToString(VALUE_FORMAT);
                row.OutOfRangeAmount = dLsl - s.Mean;
            }
        }

        /// <summary>기본 정렬을 나쁜 순으로 바꾼다(R3). rows 가 null 이면 아무 것도 하지 않는다.</summary>
        public static void SortWorstFirst(List<StatRow> rows)
        {
            if (rows == null)
            {
                return;
            }

            rows.Sort(CompareWorstFirst);
        }

        /// <summary>불량 → 주의 → 정상, 같은 상태 안에서는 Cpk 오름차순, 그다음 Key 로 안정 정렬을 흉내낸다.</summary>
        private static int CompareWorstFirst(StatRow a, StatRow b)
        {
            int nLevelCompare = ((int)a.StatusLevel).CompareTo((int)b.StatusLevel);
            if (nLevelCompare != 0)
            {
                return nLevelCompare;
            }

            int nCpkCompare = a.CpkSortValue.CompareTo(b.CpkSortValue);
            if (nCpkCompare != 0)
            {
                return nCpkCompare;
            }

            return string.CompareOrdinal(a.Key, b.Key);
        }

        /// <summary>필터바 아래 요약 한 줄(R5). rows 는 필터 무관 전체 기준. null 이면 개수 0.</summary>
        public static string BuildSummary(List<StatRow> rows)
        {
            int nBad = 0;
            int nWarning = 0;
            int nNormal = 0;

            if (rows != null)
            {
                foreach (StatRow row in rows)
                {
                    switch (row.StatusLevel)
                    {
                        case EStatLevel.Bad:
                            nBad++;
                            break;
                        case EStatLevel.Warning:
                            nWarning++;
                            break;
                        case EStatLevel.Normal:
                            nNormal++;
                            break;
                        default:
                            break;
                    }
                }
            }

            int nTotal = nBad + nWarning + nNormal;
            return "전체 " + nTotal + "항목 · 불량 " + nBad + " · 주의 " + nWarning + " · 정상 " + nNormal;
        }

        /// <summary>DataGrid Items.Filter(Predicate&lt;object&gt;) 용 — 문제(불량/주의) 행만 남긴다(R2).</summary>
        public static bool IsProblemRow(object item)
        {
            StatRow row = item as StatRow;
            if (row == null)
            {
                return false;
            }

            return row.StatusLevel != EStatLevel.Normal;
        }

        /// <summary>재검사 전 통계 비교 불가(원래 데이터 없음/표본 0) 정렬 키 — 비교 가능한 값(항상 0 이상)보다 앞으로 보낸다.</summary>
        private const double NO_ORIGINAL_SORT = -1.0;

        /// <summary>
        /// quick-260911-fia Task 4: rows(재검사 결과) 에 같은 키의 원래(재검사 전) 통계를 매칭해
        /// OriginalMeanText/DeltaText/DeltaSortValue 를 채운다. 비교 불가(원래 통계 없음/표본 0)면 "-".
        /// </summary>
        public static void FillRerunComparison(List<StatRow> rows, Dictionary<string, MeasurementStat> originalStats)
        {
            if (rows == null)
            {
                return;
            }

            foreach (StatRow row in rows)
            {
                MeasurementStat original = null;
                bool bHasOriginal = false;
                if (originalStats != null)
                {
                    bHasOriginal = originalStats.TryGetValue(row.Key, out original);
                }

                bool bComparable = bHasOriginal && original.N > 0 && row.N > 0;
                if (!bComparable)
                {
                    row.OriginalMeanText = NO_VALUE_TEXT;
                    row.DeltaText = NO_VALUE_TEXT;
                    row.DeltaSortValue = NO_ORIGINAL_SORT;
                    continue;
                }

                row.OriginalMeanText = original.Mean.ToString(VALUE_FORMAT);

                double dDelta = row.Mean - original.Mean;
                string szDeltaText;
                if (dDelta > 0.0)
                {
                    szDeltaText = "+" + dDelta.ToString(VALUE_FORMAT);
                }
                else
                {
                    szDeltaText = dDelta.ToString(VALUE_FORMAT);
                }
                row.DeltaText = szDeltaText;
                // 가장 많이 변한 항목을 정렬로 쉽게 찾도록 절댓값을 정렬 키로 쓴다(방향 무관, 변화 크기 순).
                row.DeltaSortValue = Math.Abs(dDelta);
            }
        }
    }

    /// <summary>
    /// quick-260911-fia Task 4: "저장 사진으로 재검사" 화면 상태/흐름을 담당하는 ViewModel.
    /// StatisticsWindow code-behind 는 배선만 하고, 계산 로직(계획 조회/통계 집계/원래 평균 비교)은
    /// 이 클래스와 StatRowPresenter/SavedCycleRerunPlanner/RepeatMeasurementStats 에 둔다.
    /// </summary>
    public class StatisticsRerunViewModel : INotifyPropertyChanged
    {
        private const string EXPORT_PREFIX_RERUN = "cpk_rerun_";
        private const string EXPORT_PREFIX_NORMAL = "cpk_report_";

        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>재검사 결과 화면 갱신 준비 완료(통계/행 계산 끝) — UI 스레드에서 발화.</summary>
        public event Action RerunViewReady;

        private void RaisePropertyChanged(string szPropertyName)
        {
            var handler = PropertyChanged;
            if (handler != null)
            {
                handler(this, new PropertyChangedEventArgs(szPropertyName));
            }
        }

        private List<string> _sequenceNames = new List<string>();
        public List<string> SequenceNames
        {
            get { return _sequenceNames; }
            private set { _sequenceNames = value; RaisePropertyChanged("SequenceNames"); }
        }

        private string _selectedSequenceName;
        public string SelectedSequenceName
        {
            get { return _selectedSequenceName; }
            set { _selectedSequenceName = value; RaisePropertyChanged("SelectedSequenceName"); }
        }

        private string _statusText = "";
        public string StatusText
        {
            get { return _statusText; }
            private set { _statusText = value; RaisePropertyChanged("StatusText"); }
        }

        private bool _bIsRerunning;
        public bool IsRerunning
        {
            get { return _bIsRerunning; }
            private set
            {
                _bIsRerunning = value;
                RaisePropertyChanged("IsRerunning");
                RaisePropertyChanged("CanStartRerun");
            }
        }

        /// <summary>재검사 시작 버튼/시퀀스 콤보 활성화 조건 — 실행 중이 아닐 때만.</summary>
        public bool CanStartRerun
        {
            get { return !IsRerunning; }
        }

        private bool _bIsShowingRerun;
        public bool IsShowingRerun
        {
            get { return _bIsShowingRerun; }
            private set { _bIsShowingRerun = value; RaisePropertyChanged("IsShowingRerun"); }
        }

        // 비바인딩(코드에서만 참조) — StatisticsWindow.ApplyRerunView 가 읽는다.
        public List<StatRow> RerunRows { get; private set; } = new List<StatRow>();
        public StatisticsQueryResult RerunResult { get; private set; }
        public string RerunSummaryText { get; private set; } = "";
        public List<CycleResultDto> RerunCycles { get; private set; } = new List<CycleResultDto>();
        public string RerunRecipeName { get; private set; } = "";

        private SavedCycleRerunPlan _currentPlan;
        private Dictionary<string, MeasurementStat> _originalStatsForRerun = new Dictionary<string, MeasurementStat>();
        private RepeatRunService _service;

        /// <summary>이 시퀀스 소유 Shot 이 1개 이상인 InspectionSequence 이름만 콤보에 올린다. 첫 항목 선택.</summary>
        public void LoadSequenceNames()
        {
            List<string> lstNames = new List<string>();
            var seqHandler = SystemHandler.Handle.Sequences;
            bool bHasRecipeManager = seqHandler != null && seqHandler.RecipeManager != null;
            if (bHasRecipeManager)
            {
                for (int i = 0; i < seqHandler.Count; i++)
                {
                    InspectionSequence seq = seqHandler[i] as InspectionSequence;
                    if (seq == null)
                    {
                        continue;
                    }
                    bool bHasOwnedShot = false;
                    foreach (var shot in seqHandler.RecipeManager.Shots)
                    {
                        if (InspectionSequence.IsShotOwnedBySequence(shot, seq.Name))
                        {
                            bHasOwnedShot = true;
                            break;
                        }
                    }
                    if (bHasOwnedShot)
                    {
                        lstNames.Add(seq.Name);
                    }
                }
            }

            SequenceNames = lstNames;
            if (lstNames.Count > 0)
            {
                SelectedSequenceName = lstNames[0];
            }
        }

        private static InspectionSequence FindSequenceByName(string szName)
        {
            var seqHandler = SystemHandler.Handle.Sequences;
            if (seqHandler == null)
            {
                return null;
            }
            for (int i = 0; i < seqHandler.Count; i++)
            {
                InspectionSequence seq = seqHandler[i] as InspectionSequence;
                if (seq != null && string.Equals(seq.Name, szName, StringComparison.Ordinal))
                {
                    return seq;
                }
            }
            return null;
        }

        /// <summary>저장 사진 재검사를 시작한다. 동기 거부 사유는 szError 로 반환.</summary>
        public bool TryStartRerun(DateTime dtFrom, DateTime dtTo, string szRecipeFilter, out string szError)
        {
            szError = null;

            if (IsRerunning)
            {
                szError = "이미 재검사 중입니다";
                return false;
            }
            if (string.IsNullOrEmpty(SelectedSequenceName))
            {
                szError = "재검사 시퀀스를 선택하세요";
                return false;
            }

            InspectionSequence seq = FindSequenceByName(SelectedSequenceName);
            if (seq == null)
            {
                szError = "시퀀스를 찾을 수 없습니다";
                return false;
            }

            string szCurrentRecipe = SystemHandler.Handle.Setting.CurrentRecipeName;
            if (string.IsNullOrEmpty(szCurrentRecipe))
            {
                szError = "현재 불러온 레시피가 없습니다";
                return false;
            }

            bool bRecipeFilterMismatch = !string.IsNullOrEmpty(szRecipeFilter) && !string.Equals(szRecipeFilter, szCurrentRecipe, StringComparison.Ordinal);
            if (bRecipeFilterMismatch)
            {
                szError = "재검사는 지금 불러온 레시피 파라미터로 돕니다. 레시피 콤보를 '전체' 또는 현재 레시피로 두세요";
                return false;
            }

            if (seq.State != EContextState.Idle)
            {
                szError = "시퀀스가 실행 중입니다";
                return false;
            }

            var seqHandler = SystemHandler.Handle.Sequences;
            bool bNoRecipeManager = seqHandler == null || seqHandler.RecipeManager == null;
            if (bNoRecipeManager)
            {
                szError = "레시피 정보를 찾을 수 없습니다";
                return false;
            }

            IsRerunning = true;
            StatusText = "저장 사이클 읽는 중";
            RerunRecipeName = szCurrentRecipe;

            var recipeManager = seqHandler.RecipeManager;
            Task.Run(() =>
            {
                // 레시피 '전체' 선택 시 다른 레시피 값이 섞이지 않도록 원래 통계도 현재 레시피로 명시 조회한다.
                SavedCycleRerunPlan plan = SavedCycleRerunPlanner.BuildPlan(dtFrom, dtTo, szCurrentRecipe, seq, recipeManager);
                StatisticsQueryResult originalResult = MeasurementHistoryCsvLoader.Query(dtFrom, dtTo, szCurrentRecipe);
                Application.Current.Dispatcher.BeginInvoke(new Action(() => OnPlanReady(plan, originalResult, seq)));
            });

            return true;
        }

        private void OnPlanReady(SavedCycleRerunPlan plan, StatisticsQueryResult originalResult, InspectionSequence seq)
        {
            _currentPlan = plan;
            if (originalResult != null)
            {
                _originalStatsForRerun = originalResult.Stats;
            }
            else
            {
                _originalStatsForRerun = new Dictionary<string, MeasurementStat>();
            }

            if (plan.Parts.Count == 0)
            {
                StatusText = "재검사할 부품 없음 — " + plan.BuildExclusionSummary();
                IsRerunning = false;
                return;
            }

            _service = new RepeatRunService();
            _service.OnProgressChanged += HandleServiceProgress;
            _service.OnSavedCycleRerunEnded += HandleServiceEnded;

            string szStartError;
            bool bStarted = _service.StartFromSavedCycles(seq, plan, out szStartError);
            if (!bStarted)
            {
                StatusText = szStartError;
                IsRerunning = false;
                _service.OnProgressChanged -= HandleServiceProgress;
                _service.OnSavedCycleRerunEnded -= HandleServiceEnded;
                _service = null;
                return;
            }

            StatusText = "재검사 중 0/" + plan.Parts.Count + " 부품 (" + plan.BuildExclusionSummary() + ")";
        }

        // RepeatRunService.OnProgressChanged 는 시퀀스 스레드에서 발화(마샬 책임은 구독자) — Dispatcher.BeginInvoke
        // 로 UI 스레드로 넘긴다(Invoke 동기 호출은 데드락 위험이 있어 금지).
        private void HandleServiceProgress(int nCompleted, int nTarget)
        {
            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                StatusText = "재검사 중 " + nCompleted + "/" + nTarget + " 부품 (" + BuildCurrentExclusionSummary() + ")";
            }));
        }

        private void HandleServiceEnded(List<CycleResultDto> cycles, string szReason)
        {
            Application.Current.Dispatcher.BeginInvoke(new Action(() => ApplyRerunEnded(cycles, szReason)));
        }

        private string BuildCurrentExclusionSummary()
        {
            if (_currentPlan == null)
            {
                return "";
            }
            return _currentPlan.BuildExclusionSummary();
        }

        private void ApplyRerunEnded(List<CycleResultDto> cycles, string szReason)
        {
            IsRerunning = false;
            if (_service != null)
            {
                _service.OnProgressChanged -= HandleServiceProgress;
                _service.OnSavedCycleRerunEnded -= HandleServiceEnded;
                _service = null;
            }

            bool bNoResults = cycles == null || cycles.Count == 0;
            if (bNoResults)
            {
                string szEmptyStatus = "재검사 결과 없음";
                if (!string.IsNullOrEmpty(szReason))
                {
                    szEmptyStatus = szEmptyStatus + " — " + szReason;
                }
                StatusText = szEmptyStatus;
                return;
            }

            var stats = new RepeatMeasurementStats();
            foreach (var dto in cycles)
            {
                stats.AddSample(dto);
            }
            Dictionary<string, MeasurementStat> dictStats = stats.ComputeAll();
            Dictionary<string, List<double>> dictSeries = stats.GetSeries();

            var result = new StatisticsQueryResult();
            result.Stats = dictStats;
            result.Series = dictSeries;
            result.RecipeNames = new List<string> { RerunRecipeName };
            result.TotalRowCount = cycles.Count;
            RerunResult = result;

            List<StatRow> rows = StatRowPresenter.BuildRows(dictStats);
            StatRowPresenter.FillRerunComparison(rows, _originalStatsForRerun);
            StatRowPresenter.SortWorstFirst(rows);
            RerunRows = rows;
            RerunSummaryText = StatRowPresenter.BuildSummary(rows);
            RerunCycles = cycles;

            string szCompletedStatus = "재검사 결과 — 부품 " + cycles.Count + "개(" + BuildCurrentExclusionSummary() + ")";
            if (!string.IsNullOrEmpty(szReason))
            {
                szCompletedStatus = szCompletedStatus + " · 중단: " + szReason;
            }
            StatusText = szCompletedStatus;
            IsShowingRerun = true;

            var readyHandler = RerunViewReady;
            if (readyHandler != null)
            {
                readyHandler();
            }
        }

        public void RequestStop()
        {
            if (_service != null)
            {
                _service.RequestStopSavedCycleRerun("사용자 중단");
            }
        }

        public void ReturnToOriginal()
        {
            if (!IsRerunning)
            {
                IsShowingRerun = false;
                StatusText = "";
            }
        }

        /// <summary>통계 창이 닫힐 때 호출 — 실행 중이면 중단 요청(서비스가 스스로 끝까지 복원한다).</summary>
        public void OnWindowClosing()
        {
            if (IsRerunning && _service != null)
            {
                _service.RequestStopSavedCycleRerun("통계 창 닫힘");
            }
        }

        /// <summary>CPK export 대상 사이클 목록 — 재검사 결과를 보고 있으면 그 목록, 아니면 기존 CSV 조회.</summary>
        public List<CycleResultDto> GetCyclesForExport(DateTime dtFrom, DateTime dtTo, string szRecipeFilter)
        {
            if (IsShowingRerun)
            {
                return RerunCycles;
            }
            return MeasurementHistoryCsvLoader.QueryCycles(dtFrom, dtTo, szRecipeFilter);
        }

        public string GetExportRecipeName(string szRecipeFilter, string szAllLabel)
        {
            if (IsShowingRerun)
            {
                return RerunRecipeName;
            }
            if (string.IsNullOrEmpty(szRecipeFilter))
            {
                return szAllLabel;
            }
            return szRecipeFilter;
        }

        public string GetExportFilePrefix()
        {
            if (IsShowingRerun)
            {
                return EXPORT_PREFIX_RERUN;
            }
            return EXPORT_PREFIX_NORMAL;
        }
    }

    /// <summary>
    /// 양산 이력 통계 분석 비모달 Window (STAT-01). MeasurementHistoryCsvLoader.Query 를 소비하여
    /// 기간·레시피별 통계 테이블(D-06) + 행 선택 시 히스토그램/추이 차트(WPF Canvas 직접 렌더, D-12~D-14)를 표시한다.
    /// 라이브 MainView 방해 없는 비모달 별도 Window — ShowDialog 가 아닌 Show() 로 열림 (D-08, ReviewerWindow 미러).
    /// </summary>
    public partial class StatisticsWindow : Window
    {
        private const string RECIPE_ALL = "전체";      //260707 hbk 레시피 필터 없음 표시 항목

        private StatisticsQueryResult m_lastResult;    //260707 hbk 마지막 조회 결과(Series 조회용 보관)

        // quick-260911-fia Task 4: "저장 사진으로 재검사" 화면 상태 — 계산 로직은 전부 VM 에 있다.
        private readonly StatisticsRerunViewModel m_rerunVm = new StatisticsRerunViewModel();

        public StatisticsWindow()
        {
            InitializeComponent();
            dp_From.SelectedDate = DateTime.Today;   //260707 hbk D-10 기본값 오늘
            dp_To.SelectedDate = DateTime.Today;
            pnl_Rerun.DataContext = m_rerunVm;
            m_rerunVm.RerunViewReady += ApplyRerunView;
            m_rerunVm.LoadSequenceNames();
            DoQuery("");   // 오픈 시 오늘자 전체 레시피 조회
        }

        private void Btn_Query_Click(object sender, RoutedEventArgs e)
        {
            m_rerunVm.ReturnToOriginal();
            SetRerunColumnsVisible(false);

            string szRecipe = GetSelectedRecipeFilter();

            DoQuery(szRecipe);
        }

        /// <summary>quick-260911-fia Task 4: "저장 사진으로 재검사" 버튼 — 배선만, 계산은 VM.TryStartRerun.</summary>
        private void Btn_Rerun_Click(object sender, RoutedEventArgs e)
        {
            DateTime dtFrom;
            DateTime dtTo;
            GetSelectedRange(out dtFrom, out dtTo);
            string szRecipeFilter = GetSelectedRecipeFilter();

            string szError;
            if (!m_rerunVm.TryStartRerun(dtFrom, dtTo, szRecipeFilter, out szError))
            {
                CustomMessageBox.Show("저장 사진으로 재검사", szError, MessageBoxImage.Warning);
            }
        }

        private void Btn_RerunStop_Click(object sender, RoutedEventArgs e)
        {
            m_rerunVm.RequestStop();
        }

        private void Btn_BackToOriginal_Click(object sender, RoutedEventArgs e)
        {
            m_rerunVm.ReturnToOriginal();
            SetRerunColumnsVisible(false);
            DoQuery(GetSelectedRecipeFilter());
        }

        /// <summary>quick-260911-fia Task 4: 재검사 종료(VM.RerunViewReady) 시 표/차트/버튼을 재검사 결과로 갱신한다.</summary>
        private void ApplyRerunView()
        {
            m_lastResult = m_rerunVm.RerunResult;
            grid_Stats.ItemsSource = m_rerunVm.RerunRows;
            ApplyProblemFilter();
            txt_Summary.Text = m_rerunVm.RerunSummaryText;
            ClearCharts();
            SetRerunColumnsVisible(true);
            UpdateExportButtonState();
        }

        /// <summary>"원래 평균"/"변화" 칸 표시 여부 — 재검사 결과를 보고 있을 때만 보인다.</summary>
        private void SetRerunColumnsVisible(bool bVisible)
        {
            Visibility visibility;
            if (bVisible)
            {
                visibility = Visibility.Visible;
            }
            else
            {
                visibility = Visibility.Collapsed;
            }
            col_OriginalMean.Visibility = visibility;
            col_Delta.Visibility = visibility;
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            m_rerunVm.OnWindowClosing();
        }

        /// <summary>기간(DatePicker)/레시피 필터로 조회 후 테이블/드롭다운/차트를 갱신한다. 실패해도 크래시 없이 빈 상태 폴백.</summary>
        private void DoQuery(string szRecipeFilter)
        {
            try
            {
                DateTime dtFrom;
                DateTime dtTo;
                GetSelectedRange(out dtFrom, out dtTo);

                m_lastResult = MeasurementHistoryCsvLoader.Query(dtFrom, dtTo, szRecipeFilter);
                PopulateRecipeCombo(m_lastResult.RecipeNames, szRecipeFilter);
                List<StatRow> rows = StatRowPresenter.BuildRows(m_lastResult.Stats);
                StatRowPresenter.SortWorstFirst(rows);
                grid_Stats.ItemsSource = rows;
                ApplyProblemFilter();   // 새 ItemsSource 에도 현재 체크 상태 재적용
                txt_Summary.Text = StatRowPresenter.BuildSummary(rows);   // 전체 rows 기준 — 필터 무관
                ClearCharts();   // 새 조회 직후 → 이전 선택 차트 비움(행 선택 시 다시 갱신)
                UpdateExportButtonState();
            }
            catch (Exception ex)   //260707 hbk 조회 실패해도 UI 크래시 없이 빈 상태 폴백(ReviewerWindow 패턴)
            {
                try { Logging.PrintErrLog((int)ELogType.Error, "[StatisticsWindow] DoQuery: " + ex.Message); } catch { }
                UpdateExportButtonState();   // 예외로 중단되어도 버튼이 이전 상태로 남지 않게 한다
            }
        }

        /// <summary>레시피 콤보 현재 선택 → 필터 문자열. "전체" 또는 미선택이면 빈 문자열(=필터 없음).</summary>
        private string GetSelectedRecipeFilter()
        {
            string szRecipe = "";
            if (combo_Recipe.SelectedItem != null)
            {
                string szSel = combo_Recipe.SelectedItem.ToString();
                if (szSel != RECIPE_ALL)
                {
                    szRecipe = szSel;
                }
            }

            return szRecipe;
        }

        /// <summary>DatePicker 두 개 → 조회 기간. 미선택이면 오늘로 폴백(기존 DoQuery 동작 동일).</summary>
        private void GetSelectedRange(out DateTime dtFrom, out DateTime dtTo)
        {
            dtFrom = DateTime.Today;
            if (dp_From.SelectedDate.HasValue)
            {
                dtFrom = dp_From.SelectedDate.Value;
            }

            dtTo = DateTime.Today;
            if (dp_To.SelectedDate.HasValue)
            {
                dtTo = dp_To.SelectedDate.Value;
            }
        }

        /// <summary>조회 결과가 있을 때만 export 버튼을 연다. 조회 전/0건이면 비활성.</summary>
        private void UpdateExportButtonState()
        {
            bool bEnable = false;
            if (m_lastResult != null && m_lastResult.TotalRowCount > 0)
            {
                bEnable = true;
            }

            btn_CpkExport.IsEnabled = bEnable;
        }

        /// <summary>
        /// 현재 조회 조건(기간/레시피)으로 CSV 이력을 사이클 단위로 재조립해 CPK 리포트 xlsx 를 저장한다.
        /// 화면 통계와 달리 사이클 재구성이 필요하므로 Query() 가 아니라 QueryCycles() 를 쓴다.
        /// </summary>
        private void Btn_CpkExport_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                DateTime dtFrom;
                DateTime dtTo;
                GetSelectedRange(out dtFrom, out dtTo);
                string szRecipeFilter = GetSelectedRecipeFilter();

                // quick-260911-fia Task 4: 지금 보고 있는 쪽(재검사 결과면 재검사 사이클 목록) 기준 export.
                List<CycleResultDto> cycles = m_rerunVm.GetCyclesForExport(dtFrom, dtTo, szRecipeFilter);
                if (cycles == null || cycles.Count == 0)
                {
                    CustomMessageBox.Show("CPK 리포트 export", "해당 기간에 데이터가 없습니다.", MessageBoxImage.Warning);
                    return;
                }

                string szRecipeName = m_rerunVm.GetExportRecipeName(szRecipeFilter, RECIPE_ALL);

                var dlg = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "Excel 파일 (*.xlsx)|*.xlsx",
                    FileName = m_rerunVm.GetExportFilePrefix() + dtFrom.ToString("yyyyMMdd") + "_" + dtTo.ToString("yyyyMMdd") + ".xlsx",
                    InitialDirectory = SystemHandler.Handle.Setting.ResultSavePath
                };

                if (dlg.ShowDialog() == true)
                {
                    bool bOk = ReringProject.Export.CpkReportExportService.ExportCpkReport(
                        cycles, szRecipeName, dlg.FileName,
                        ReringProject.Export.CpkReportExportService.DEFAULT_MAX_RAW_COLUMNS);

                    string szMsg;
                    if (bOk)
                    {
                        szMsg = "저장 완료:\n" + dlg.FileName;
                    }
                    else
                    {
                        szMsg = "export 실패 (로그 확인)";
                    }

                    MessageBoxImage icon;
                    if (bOk)
                    {
                        icon = MessageBoxImage.Information;
                    }
                    else
                    {
                        icon = MessageBoxImage.Error;
                    }

                    CustomMessageBox.Show("CPK 리포트 export", szMsg, icon);
                }
            }
            catch (Exception ex)
            {
                try { Logging.PrintErrLog((int)ELogType.Error, "[StatisticsWindow] Btn_CpkExport_Click: " + ex.Message); } catch { }
                CustomMessageBox.Show("CPK 리포트 export", "export 중 오류가 발생했습니다 (로그 확인)", MessageBoxImage.Error);
            }
        }

        /// <summary>레시피 콤보를 "전체" + distinct 목록으로 재구성한다. 현재 필터가 목록에 있으면 유지, 없으면 "전체" 선택.</summary>
        private void PopulateRecipeCombo(List<string> names, string szCurrent)
        {
            combo_Recipe.Items.Clear();
            combo_Recipe.Items.Add(RECIPE_ALL);
            if (names != null)
            {
                foreach (string sz in names)
                {
                    combo_Recipe.Items.Add(sz);
                }
            }

            if (!string.IsNullOrEmpty(szCurrent) && names != null && names.Contains(szCurrent))
            {
                combo_Recipe.SelectedItem = szCurrent;
            }
            else
            {
                combo_Recipe.SelectedItem = RECIPE_ALL;
            }
        }

        /// <summary>DataGrid 행 선택 시 해당 측정키(Series)의 히스토그램/추이 차트를 갱신한다(D-12).</summary>
        private void Grid_Stats_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            RenderCurrentSelection();
        }

        /// <summary>260707 hbk quick-260707-fdx Canvas 크기 변경(창 리사이즈) 시 현재 선택 행 기준으로 다시 렌더한다. 선택 없으면 아무것도 안 함.</summary>
        private void Canvas_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            RenderCurrentSelection();
        }

        /// <summary>260707 hbk quick-260707-fdx 현재 grid_Stats 선택 행의 Series 값으로 두 차트를 렌더한다(SelectionChanged/SizeChanged 공용).</summary>
        private void RenderCurrentSelection()
        {
            StatRow row = grid_Stats.SelectedItem as StatRow;
            if (row == null)
            {
                return;
            }

            if (m_lastResult == null)
            {
                return;
            }

            List<double> values;
            if (!m_lastResult.Series.TryGetValue(row.Key, out values))
            {
                values = new List<double>();
            }

            double dUsl = row.NominalValue + row.TolerancePlus;
            double dLsl = row.NominalValue - Math.Abs(row.ToleranceMinus);
            RenderHistogram(values, dUsl, dLsl);
            RenderTrend(values, row.Mean, dUsl, dLsl);
        }

        /// <summary>도수 분포 히스토그램을 canvas_Histogram 에 렌더한다. 실제 드로잉은 ChartRenderService 위임.</summary>
        private void RenderHistogram(List<double> values, double dUsl, double dLsl)
        {
            ChartRenderService.RenderHistogram(canvas_Histogram, canvas_Histogram.ActualWidth, canvas_Histogram.ActualHeight, values, dUsl, dLsl);
        }

        /// <summary>샘플 인덱스 기준 추이 차트를 canvas_Trend 에 렌더한다. 실제 드로잉은 ChartRenderService 위임.</summary>
        private void RenderTrend(List<double> values, double dMean, double dUsl, double dLsl)
        {
            ChartRenderService.RenderTrend(canvas_Trend, canvas_Trend.ActualWidth, canvas_Trend.ActualHeight, values, dMean, dUsl, dLsl);
        }

        /// <summary>두 차트를 비운다(새 조회 직후 / 선택 없음 상태).</summary>
        private void ClearCharts()
        {
            canvas_Histogram.Children.Clear();
            canvas_Trend.Children.Clear();
        }

        /// <summary>"문제 항목만 보기" 체크 상태를 grid_Stats 뷰 필터에 반영한다(R2). ItemsSource 없으면 가드.</summary>
        private void ApplyProblemFilter()
        {
            if (grid_Stats.ItemsSource == null)
            {
                return;
            }

            bool bOnlyProblem = chk_ProblemOnly.IsChecked == true;
            if (bOnlyProblem)
            {
                grid_Stats.Items.Filter = new Predicate<object>(StatRowPresenter.IsProblemRow);
            }
            else
            {
                grid_Stats.Items.Filter = null;
            }
        }

        /// <summary>"문제 항목만 보기" 체크박스 변경 시 즉시 필터를 적용한다. 선택 행이 걸러져 사라지면 차트를 비운다.</summary>
        private void Chk_ProblemOnly_Changed(object sender, RoutedEventArgs e)
        {
            try
            {
                ApplyProblemFilter();
                if (grid_Stats.SelectedItem == null)
                {
                    ClearCharts();
                }
            }
            catch (Exception ex)
            {
                try { Logging.PrintErrLog((int)ELogType.Error, "[StatisticsWindow] Chk_ProblemOnly_Changed: " + ex.Message); } catch { }
            }
        }
    }
}
