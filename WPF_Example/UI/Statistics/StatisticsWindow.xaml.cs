//260707 hbk STAT-01: 양산 이력 통계 분석 UI — 조회/테이블/차트(WPF Canvas 직접 렌더) code-behind
//260707 hbk quick-260707-fdx ChartDirector(유료·워터마크) 제거 → 히스토그램/추이 차트를 WPF Canvas 도형으로 재구현
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
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

        public string YieldRateText { get; set; }  //260707 hbk 수율 OK/(OK+NG) — 값 클수록 좋음(불량률 대체)

        public string Key { get; set; }             //260707 hbk Series 조인 키(Shot/FAI/측정명)

        public double NominalValue { get; set; }

        public double TolerancePlus { get; set; }

        public double ToleranceMinus { get; set; }
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

        public StatisticsWindow()
        {
            InitializeComponent();
            dp_From.SelectedDate = DateTime.Today;   //260707 hbk D-10 기본값 오늘
            dp_To.SelectedDate = DateTime.Today;
            DoQuery("");   // 오픈 시 오늘자 전체 레시피 조회
        }

        private void Btn_Query_Click(object sender, RoutedEventArgs e)
        {
            string szRecipe = GetSelectedRecipeFilter();

            DoQuery(szRecipe);
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
                grid_Stats.ItemsSource = BuildRows(m_lastResult.Stats);
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

                List<CycleResultDto> cycles = MeasurementHistoryCsvLoader.QueryCycles(dtFrom, dtTo, szRecipeFilter);
                if (cycles == null || cycles.Count == 0)
                {
                    CustomMessageBox.Show("CPK 리포트 export", "해당 기간에 데이터가 없습니다.", MessageBoxImage.Warning);
                    return;
                }

                string szRecipeName = szRecipeFilter;
                if (string.IsNullOrEmpty(szRecipeName))
                {
                    szRecipeName = RECIPE_ALL;
                }

                var dlg = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "Excel 파일 (*.xlsx)|*.xlsx",
                    FileName = "cpk_report_" + dtFrom.ToString("yyyyMMdd") + "_" + dtTo.ToString("yyyyMMdd") + ".xlsx",
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
                row.YieldRateText = YieldRateToText(s.OkCount, s.NgCount);   //260707 hbk 불량률→수율
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

        /// <summary>수율(Yield, %) 표시 문자열 = OK/(OK+NG). 값 클수록 좋음. 분모 0 방어(if/else, 삼항 금지). //260707 hbk 불량률→수율 긍정지표 전환</summary>
        private string YieldRateToText(int nOk, int nNg)   //260707 hbk 불량률(DefectRateToText)→수율로 대체
        {
            int nTotal = nOk + nNg;
            if (nTotal == 0)
            {
                return "-";
            }

            double d = nOk * 100.0 / nTotal;   //260707 hbk OK 비율(수율) — 기존 NG 비율에서 뒤집음
            return d.ToString("F2") + "%";
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
    }
}
