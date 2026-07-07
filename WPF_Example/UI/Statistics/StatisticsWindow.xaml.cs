//260707 hbk STAT-01: 양산 이력 통계 분석 UI — 조회/테이블/차트(ChartDirector) code-behind
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using ChartDirector;
using ReringProject.Sequence;
using ReringProject.Setting;   //260707 hbk 빌드오류(CS0103) 수정 — ELogType 이 ReringProject.Setting 네임스페이스에 정의됨
using ReringProject.Utility;

namespace ReringProject.UI
{
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

        public string DefectRateText { get; set; } //260707 hbk NG/(OK+NG)

        public string Key { get; set; }             //260707 hbk Series 조인 키(Shot/FAI/측정명)

        public double NominalValue { get; set; }

        public double TolerancePlus { get; set; }

        public double ToleranceMinus { get; set; }
    }

    /// <summary>
    /// 양산 이력 통계 분석 비모달 Window (STAT-01). MeasurementHistoryCsvLoader.Query 를 소비하여
    /// 기간·레시피별 통계 테이블(D-06) + 행 선택 시 히스토그램/추이 차트(ChartDirector, D-12~D-14)를 표시한다.
    /// 라이브 MainView 방해 없는 비모달 별도 Window — ShowDialog 가 아닌 Show() 로 열림 (D-08, ReviewerWindow 미러).
    /// </summary>
    public partial class StatisticsWindow : Window
    {
        private const int BIN_COUNT = 20;             //260707 hbk D-14 히스토그램 bin 수(잠금 결정)
        private const int MAX_X_LABELS = 5;           //260707 hbk 히스토그램 x축 최대 표시 라벨 수(겹침 방지)
        private const int CHART_W = 560;
        private const int CHART_H = 300;
        private const int COLOR_USL = 0xcc0000;        // 빨강 (공차 상한)
        private const int COLOR_LSL = 0xcc0000;        // 빨강 (공차 하한)
        private const int COLOR_MEAN = 0x008800;       // 초록 (평균)
        private const int COLOR_BAR = 0x3366cc;
        private const int COLOR_LINE = 0x3366cc;
        private const string RECIPE_ALL = "전체";      //260707 hbk 레시피 필터 없음 표시 항목

        private StatisticsQueryResult m_lastResult;    //260707 hbk 마지막 조회 결과(Series 조회용 보관)

        public StatisticsWindow()
        {
            InitializeComponent();
            dp_From.SelectedDate = DateTime.Today;   //260707 hbk D-10 기본값 오늘
            dp_To.SelectedDate = DateTime.Today;
            DoQuery("");   // 오픈 시 오늘자 전체 레시피 조회
        }

        private void Btn_Query_Click(object sender, RoutedEventArgs e)
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

            DoQuery(szRecipe);
        }

        /// <summary>기간(DatePicker)/레시피 필터로 조회 후 테이블/드롭다운/차트를 갱신한다. 실패해도 크래시 없이 빈 상태 폴백.</summary>
        private void DoQuery(string szRecipeFilter)
        {
            try
            {
                DateTime dtFrom = DateTime.Today;
                if (dp_From.SelectedDate.HasValue)
                {
                    dtFrom = dp_From.SelectedDate.Value;
                }

                DateTime dtTo = DateTime.Today;
                if (dp_To.SelectedDate.HasValue)
                {
                    dtTo = dp_To.SelectedDate.Value;
                }

                m_lastResult = MeasurementHistoryCsvLoader.Query(dtFrom, dtTo, szRecipeFilter);
                PopulateRecipeCombo(m_lastResult.RecipeNames, szRecipeFilter);
                grid_Stats.ItemsSource = BuildRows(m_lastResult.Stats);
                ClearCharts();   // 새 조회 직후 → 이전 선택 차트 비움(행 선택 시 다시 갱신)
            }
            catch (Exception ex)   //260707 hbk 조회 실패해도 UI 크래시 없이 빈 상태 폴백(ReviewerWindow 패턴)
            {
                try { Logging.PrintErrLog((int)ELogType.Error, "[StatisticsWindow] DoQuery: " + ex.Message); } catch { }
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

        /// <summary>Stats 딕셔너리(Shot/FAI/측정명 키)를 DataGrid 바인딩용 화면 행 리스트로 변환한다.</summary>
        private List<StatRow> BuildRows(Dictionary<string, MeasurementStat> stats)
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
                row.DefectRateText = DefectRateToText(s.OkCount, s.NgCount);
                row.NominalValue = s.NominalValue;
                row.TolerancePlus = s.TolerancePlus;
                row.ToleranceMinus = s.ToleranceMinus;
                rows.Add(row);
            }

            return rows;
        }

        /// <summary>Cpk 표시 문자열 — 무한대/NaN 방어(if/else, 삼항 금지).</summary>
        private string CpkToText(double dCpk)
        {
            if (double.IsPositiveInfinity(dCpk))
            {
                return "∞";   // ∞
            }

            if (double.IsNegativeInfinity(dCpk) || double.IsNaN(dCpk))
            {
                return "-";
            }

            return dCpk.ToString("F3");
        }

        /// <summary>불량률(%) 표시 문자열 = NG/(OK+NG). 분모 0 방어(if/else, 삼항 금지).</summary>
        private string DefectRateToText(int nOk, int nNg)
        {
            int nTotal = nOk + nNg;
            if (nTotal == 0)
            {
                return "-";
            }

            double d = nNg * 100.0 / nTotal;
            return d.ToString("F2") + "%";
        }

        /// <summary>DataGrid 행 선택 시 해당 측정키(Series)의 히스토그램/추이 차트를 갱신한다(D-12).</summary>
        private void Grid_Stats_SelectionChanged(object sender, SelectionChangedEventArgs e)
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

        /// <summary>도수 분포 히스토그램(BarLayer) + USL/LSL 수직 markLine 렌더(D-14).</summary>
        private void RenderHistogram(List<double> values, double dUsl, double dLsl)
        {
            if (values == null || values.Count == 0)
            {
                viewer_Histogram.Chart = null;
                return;
            }

            string[] labels;
            double[] freq = BuildHistogramBins(values, BIN_COUNT, out labels);

            double dMin = MinOf(values);
            double dMax = MaxOf(values);

            XYChart c = new XYChart(CHART_W, CHART_H);
            c.setPlotArea(55, 25, CHART_W - 90, CHART_H - 70);
            c.addBarLayer(freq, COLOR_BAR);
            c.xAxis().setLabels(labels);

            int nLabelStep = (int)Math.Ceiling((double)BIN_COUNT / MAX_X_LABELS);   //260707 hbk 라벨 겹침 방지 스텝
            if (nLabelStep < 1)
            {
                nLabelStep = 1;
            }
            c.xAxis().setLabelStep(nLabelStep);   //260707 hbk 5개 내외만 표시

            // USL/LSL 값 → bin 인덱스 환산 후 수직선 표시(0 나눗셈 방어)
            double dRange = dMax - dMin;
            if (dRange > 0)
            {
                double dUslBin = (dUsl - dMin) / dRange * BIN_COUNT;
                double dLslBin = (dLsl - dMin) / dRange * BIN_COUNT;

                double dEps = 0.5;   //260707 hbk 반 bin 이내면 USL/LSL 동일 위치로 간주(공차 0 포함)
                if (Math.Abs(dUslBin - dLslBin) <= dEps)
                {
                    double dMidBin = (dUslBin + dLslBin) / 2.0;   //260707 hbk 겹침 → 단일 마크로 병합
                    c.xAxis().addMark(dMidBin, COLOR_USL, "USL/LSL");   //260707 hbk 병합 라벨
                }
                else
                {
                    c.xAxis().addMark(dUslBin, COLOR_USL, "USL");
                    c.xAxis().addMark(dLslBin, COLOR_LSL, "LSL");
                }
            }

            viewer_Histogram.Chart = c;
        }

        /// <summary>샘플 인덱스(1..N) 기준 추이 LineLayer + 평균/USL/LSL 수평 markLine 렌더(D-13).</summary>
        private void RenderTrend(List<double> values, double dMean, double dUsl, double dLsl)
        {
            if (values == null || values.Count == 0)
            {
                viewer_Trend.Chart = null;
                return;
            }

            double[] data = values.ToArray();

            XYChart c = new XYChart(CHART_W, CHART_H);
            c.setPlotArea(55, 25, CHART_W - 90, CHART_H - 70);
            c.addLineLayer(data, COLOR_LINE);
            AddSpecMarksY(c, values, dMean, dUsl, dLsl);   //260707 hbk 근접/공차0 마크 병합 렌더

            viewer_Trend.Chart = c;
        }

        /// <summary>추이 차트 평균/USL/LSL 수평 마크를 근접 겹침 방지하여 렌더(공차 0 → USL/LSL 병합). //260707 hbk</summary>
        private void AddSpecMarksY(XYChart c, List<double> values, double dMean, double dUsl, double dLsl)   //260707 hbk 마크 겹침 제거 헬퍼
        {
            double dMin = MinOf(values);   //260707 hbk
            double dMax = MaxOf(values);   //260707 hbk
            double dSpan = dMax - dMin;    //260707 hbk 데이터 스팬 기준 근접 임계

            // USL/LSL 을 스팬에 포함(마크가 데이터 밖일 수 있음)
            if (dUsl > dMax)   //260707 hbk
            {
                dSpan = dUsl - dMin;
            }
            if (dLsl < dMin)   //260707 hbk
            {
                dSpan = dMax - dLsl;
                if (dUsl > dMax)
                {
                    dSpan = dUsl - dLsl;
                }
            }

            double dEps = dSpan * 0.02;   //260707 hbk 스팬 2% 이내면 근접으로 간주
            if (dEps <= 0)   //260707 hbk 스팬 0(전 값 동일) → 절대 최소 임계
            {
                dEps = 1e-9;
            }

            c.yAxis().addMark(dMean, COLOR_MEAN, "평균");   //260707 hbk 평균은 항상

            if (Math.Abs(dUsl - dLsl) <= dEps)   //260707 hbk 공차 0 또는 USL≈LSL → 병합
            {
                double dMid = (dUsl + dLsl) / 2.0;   //260707 hbk
                c.yAxis().addMark(dMid, COLOR_USL, "USL/LSL");   //260707 hbk 단일 병합 마크
            }
            else   //260707 hbk 정상: 개별 마크(회귀 0)
            {
                c.yAxis().addMark(dUsl, COLOR_USL, "USL");   //260707 hbk
                c.yAxis().addMark(dLsl, COLOR_LSL, "LSL");   //260707 hbk
            }
        }

        /// <summary>두 차트를 비운다(새 조회 직후 / 선택 없음 상태).</summary>
        private void ClearCharts()
        {
            viewer_Histogram.Chart = null;
            viewer_Trend.Chart = null;
        }

        /// <summary>min~max 균등 nBins 분할 도수 계산. max==min 이면 단일 bin 처리(0 나눗셈 방어).</summary>
        private double[] BuildHistogramBins(List<double> values, int nBins, out string[] labels)
        {
            double[] freq = new double[nBins];
            labels = new string[nBins];

            double dMin = MinOf(values);
            double dMax = MaxOf(values);
            double dSpan = dMax - dMin;

            if (dSpan <= 0)   //260707 hbk 전 값 동일 → 첫 bin 에 전부 집계(0 나눗셈 방어)
            {
                freq[0] = values.Count;
                for (int i = 0; i < nBins; i++)
                {
                    labels[i] = dMin.ToString("F3");
                }

                return freq;
            }

            double dBinWidth = dSpan / nBins;
            foreach (double v in values)
            {
                int nIdx = (int)((v - dMin) / dBinWidth);
                if (nIdx >= nBins)
                {
                    nIdx = nBins - 1;
                }

                if (nIdx < 0)
                {
                    nIdx = 0;
                }

                freq[nIdx]++;
            }

            for (int i = 0; i < nBins; i++)
            {
                double dCenter = dMin + dBinWidth * (i + 0.5);
                labels[i] = dCenter.ToString("F3");
            }

            return freq;
        }

        /// <summary>List&lt;double&gt; 최솟값 (Linq 미사용 — 단순 for 루프로 가독성 우선).</summary>
        private double MinOf(List<double> values)
        {
            double d = values[0];
            for (int i = 1; i < values.Count; i++)
            {
                if (values[i] < d)
                {
                    d = values[i];
                }
            }

            return d;
        }

        /// <summary>List&lt;double&gt; 최댓값.</summary>
        private double MaxOf(List<double> values)
        {
            double d = values[0];
            for (int i = 1; i < values.Count; i++)
            {
                if (values[i] > d)
                {
                    d = values[i];
                }
            }

            return d;
        }
    }
}
