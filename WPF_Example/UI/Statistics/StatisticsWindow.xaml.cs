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

        public string DefectRateText { get; set; } //260707 hbk NG/(OK+NG)

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
        private const int BIN_COUNT = 20;             //260707 hbk D-14 히스토그램 bin 수(잠금 결정)
        private const int MAX_X_LABELS = 5;           //260707 hbk 히스토그램/추이 x축 최대 표시 라벨 수(겹침 방지)
        private const double MERGE_PX = 12.0;         //260707 hbk quick-260707-fdx 픽셀 거리 12px 미만이면 라벨 병합
        private const string RECIPE_ALL = "전체";      //260707 hbk 레시피 필터 없음 표시 항목

        //260707 hbk quick-260707-fdx WPF Canvas 렌더용 고정 브러시(Freeze — 성능/스레드 안전)
        private static readonly SolidColorBrush m_brushBar = MakeFrozenBrush(0x33, 0x66, 0xCC);
        private static readonly SolidColorBrush m_brushLine = MakeFrozenBrush(0x33, 0x66, 0xCC);
        private static readonly SolidColorBrush m_brushMean = MakeFrozenBrush(0x00, 0x88, 0x00);
        private static readonly SolidColorBrush m_brushSpec = MakeFrozenBrush(0xCC, 0x00, 0x00);
        private static readonly SolidColorBrush m_brushAxis = MakeFrozenBrush(0x94, 0xA3, 0xB8);
        private static readonly SolidColorBrush m_brushText = MakeFrozenBrush(0x33, 0x33, 0x33);

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

        /// <summary>260707 hbk quick-260707-fdx 도수 분포 히스토그램(Rectangle 막대) + USL/LSL 수직선을 canvas_Histogram 에 직접 렌더(D-14).</summary>
        private void RenderHistogram(List<double> values, double dUsl, double dLsl)
        {
            canvas_Histogram.Children.Clear();
            double dW = canvas_Histogram.ActualWidth;
            double dH = canvas_Histogram.ActualHeight;
            if (dW <= 0 || dH <= 0)
            {
                return;
            }

            if (values == null || values.Count == 0)
            {
                DrawNoDataText(canvas_Histogram, dW, dH);
                return;
            }

            string[] labels;
            double[] freq = BuildHistogramBins(values, BIN_COUNT, out labels);

            double dMin = MinOf(values);
            double dMax = MaxOf(values);

            double dMarginL = 40;
            double dMarginB = 24;
            double dMarginT = 10;
            double dMarginR = 10;
            double dPlotX0 = dMarginL;
            double dPlotY0 = dMarginT;
            double dPlotW = dW - dMarginL - dMarginR;
            double dPlotH = dH - dMarginT - dMarginB;
            if (dPlotW <= 0 || dPlotH <= 0)
            {
                return;
            }

            double dMaxFreq = 0;
            for (int i = 0; i < freq.Length; i++)
            {
                if (freq[i] > dMaxFreq)
                {
                    dMaxFreq = freq[i];
                }
            }

            if (dMaxFreq <= 0)
            {
                dMaxFreq = 1;
            }

            double dBinW = dPlotW / BIN_COUNT;

            // 막대(도수 정규화) //260707 hbk
            for (int i = 0; i < BIN_COUNT; i++)
            {
                double dBarH = freq[i] / dMaxFreq * dPlotH;
                Rectangle rc = new Rectangle();
                rc.Width = Math.Max(dBinW - 1, 1);
                rc.Height = Math.Max(dBarH, 0);
                rc.Fill = m_brushBar;
                Canvas.SetLeft(rc, dPlotX0 + i * dBinW);
                Canvas.SetTop(rc, dPlotY0 + dPlotH - dBarH);
                canvas_Histogram.Children.Add(rc);
            }

            DrawAxisLines(canvas_Histogram, dPlotX0, dPlotY0, dPlotW, dPlotH);

            // x축 라벨(bin 중심값, 5개 내외만 — 겹침 방지) //260707 hbk
            int nLabelStep = (int)Math.Ceiling((double)BIN_COUNT / MAX_X_LABELS);
            if (nLabelStep < 1)
            {
                nLabelStep = 1;
            }

            for (int i = 0; i < BIN_COUNT; i += nLabelStep)
            {
                TextBlock tb = CreateLabel(labels[i], 10, m_brushText);
                canvas_Histogram.Children.Add(tb);
                tb.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                double dCx = dPlotX0 + i * dBinW + dBinW / 2.0;
                Canvas.SetLeft(tb, dCx - tb.DesiredSize.Width / 2.0);
                Canvas.SetTop(tb, dPlotY0 + dPlotH + 2);
            }

            // y축 라벨(0 / 중간 / 최대 도수) //260707 hbk
            DrawYTicksCount(canvas_Histogram, dPlotX0, dPlotY0, dPlotH, dMaxFreq);

            // y축 제목 "빈도(개수)" — 세로 회전 라벨(막대 높이 = 해당 값 구간의 측정 개수) //260707 hbk
            TextBlock tbYTitle = CreateLabel("빈도(개수)", 11, m_brushText);
            tbYTitle.LayoutTransform = new RotateTransform(-90);   //260707 hbk 세로로 회전
            canvas_Histogram.Children.Add(tbYTitle);
            tbYTitle.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Canvas.SetLeft(tbYTitle, 0);
            Canvas.SetTop(tbYTitle, dPlotY0 + (dPlotH - tbYTitle.DesiredSize.Height) / 2.0);

            // USL/LSL 수직선 — 근접(12px 미만) 시 단일 라벨로 병합 //260707 hbk
            double dRange = dMax - dMin;
            if (dRange > 0)
            {
                double dXUsl = dPlotX0 + (dUsl - dMin) / dRange * dPlotW;
                double dXLsl = dPlotX0 + (dLsl - dMin) / dRange * dPlotW;
                bool bUslIn = dXUsl >= dPlotX0 && dXUsl <= dPlotX0 + dPlotW;
                bool bLslIn = dXLsl >= dPlotX0 && dXLsl <= dPlotX0 + dPlotW;

                if (bUslIn && bLslIn && Math.Abs(dXUsl - dXLsl) < MERGE_PX)
                {
                    double dXMid = (dXUsl + dXLsl) / 2.0;
                    DrawVLine(canvas_Histogram, dXMid, dPlotY0, dPlotH, m_brushSpec, "USL/LSL");
                }
                else
                {
                    if (bUslIn)
                    {
                        DrawVLine(canvas_Histogram, dXUsl, dPlotY0, dPlotH, m_brushSpec, "USL");
                    }

                    if (bLslIn)
                    {
                        DrawVLine(canvas_Histogram, dXLsl, dPlotY0, dPlotH, m_brushSpec, "LSL");
                    }
                }
            }
        }

        /// <summary>260707 hbk quick-260707-fdx 샘플 인덱스(1..N) 기준 추이 Polyline + 평균/USL/LSL 수평선을 canvas_Trend 에 직접 렌더(D-13).</summary>
        private void RenderTrend(List<double> values, double dMean, double dUsl, double dLsl)
        {
            canvas_Trend.Children.Clear();
            double dW = canvas_Trend.ActualWidth;
            double dH = canvas_Trend.ActualHeight;
            if (dW <= 0 || dH <= 0)
            {
                return;
            }

            if (values == null || values.Count == 0)
            {
                DrawNoDataText(canvas_Trend, dW, dH);
                return;
            }

            double dMarginL = 55;   // F3 숫자 라벨 표시 위해 히스토그램보다 넓게 //260707 hbk
            double dMarginB = 24;
            double dMarginT = 10;
            double dMarginR = 10;
            double dPlotX0 = dMarginL;
            double dPlotY0 = dMarginT;
            double dPlotW = dW - dMarginL - dMarginR;
            double dPlotH = dH - dMarginT - dMarginB;
            if (dPlotW <= 0 || dPlotH <= 0)
            {
                return;
            }

            double dLo;
            double dHi;
            ComputePaddedRange(values, dMean, dUsl, dLsl, out dLo, out dHi);
            double dSpan = dHi - dLo;
            if (dSpan <= 0)
            {
                dSpan = 1.0;
            }

            int nCount = values.Count;
            PointCollection pts = new PointCollection();
            for (int i = 0; i < nCount; i++)
            {
                double dX = TrendIndexToX(i, nCount, dPlotX0, dPlotW);
                double dY = dPlotY0 + dPlotH - (values[i] - dLo) / dSpan * dPlotH;
                pts.Add(new Point(dX, dY));
            }

            Polyline pl = new Polyline();
            pl.Points = pts;
            pl.Stroke = m_brushLine;
            pl.StrokeThickness = 1.5;
            canvas_Trend.Children.Add(pl);

            DrawAxisLines(canvas_Trend, dPlotX0, dPlotY0, dPlotW, dPlotH);
            DrawYTicksValue(canvas_Trend, dPlotX0, dPlotY0, dPlotH, dLo, dHi);
            DrawTrendXLabels(canvas_Trend, dPlotX0, dPlotY0, dPlotW, dPlotH, nCount);
            DrawTrendSpecMarks(canvas_Trend, dPlotX0, dPlotY0, dPlotW, dPlotH, dLo, dSpan, dMean, dUsl, dLsl);
        }

        /// <summary>260707 hbk quick-260707-fdx 추이 차트 샘플 인덱스(0-base) → x 픽셀 좌표 환산. N=1 이면 플롯 중앙.</summary>
        private double TrendIndexToX(int nIdx, int nCount, double dPlotX0, double dPlotW)
        {
            if (nCount > 1)
            {
                return dPlotX0 + (double)nIdx / (nCount - 1) * dPlotW;
            }

            return dPlotX0 + dPlotW / 2.0;
        }

        /// <summary>260707 hbk quick-260707-fdx 추이 차트 x축 라벨(샘플 번호 1..N, 5개 내외 — 겹침 방지).</summary>
        private void DrawTrendXLabels(Canvas canvas, double dPlotX0, double dPlotY0, double dPlotW, double dPlotH, int nCount)
        {
            int nStep = (int)Math.Ceiling((double)nCount / MAX_X_LABELS);
            if (nStep < 1)
            {
                nStep = 1;
            }

            for (int i = 0; i < nCount; i += nStep)
            {
                double dX = TrendIndexToX(i, nCount, dPlotX0, dPlotW);
                TextBlock tb = CreateLabel((i + 1).ToString(), 10, m_brushText);
                canvas.Children.Add(tb);
                tb.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                Canvas.SetLeft(tb, dX - tb.DesiredSize.Width / 2.0);
                Canvas.SetTop(tb, dPlotY0 + dPlotH + 2);
            }
        }

        /// <summary>260707 hbk quick-260707-fdx 추이 차트 평균/USL/LSL 수평선을 픽셀 y좌표 기준 근접(12px 미만) 그룹으로 병합해 렌더 — 라벨 세로 겹침 제거.</summary>
        private void DrawTrendSpecMarks(Canvas canvas, double dPlotX0, double dPlotY0, double dPlotW, double dPlotH, double dLo, double dSpan, double dMean, double dUsl, double dLsl)
        {
            double[] dVals = new double[3];
            string[] szLabels = new string[3];
            dVals[0] = dLsl;
            szLabels[0] = "LSL";
            dVals[1] = dMean;
            szLabels[1] = "평균";
            dVals[2] = dUsl;
            szLabels[2] = "USL";

            double[] dPixelY = new double[3];
            for (int i = 0; i < 3; i++)
            {
                dPixelY[i] = dPlotY0 + dPlotH - (dVals[i] - dLo) / dSpan * dPlotH;
            }

            // 픽셀Y 오름차순 버블 정렬(3개 — 값/라벨 동반 정렬, LINQ 미사용) //260707 hbk
            for (int i = 0; i < 3; i++)
            {
                for (int j = i + 1; j < 3; j++)
                {
                    if (dPixelY[j] < dPixelY[i])
                    {
                        double dTmpY = dPixelY[i];
                        dPixelY[i] = dPixelY[j];
                        dPixelY[j] = dTmpY;
                        string szTmpL = szLabels[i];
                        szLabels[i] = szLabels[j];
                        szLabels[j] = szTmpL;
                    }
                }
            }

            // 정렬된 마크를 픽셀 거리 기준 그리디 그룹화 → 그룹당 단일 병합 라벨+선 //260707 hbk
            int nStart = 0;
            while (nStart < 3)
            {
                int nEnd = nStart;
                while (nEnd + 1 < 3 && (dPixelY[nEnd + 1] - dPixelY[nStart]) < MERGE_PX)
                {
                    nEnd++;
                }

                double dSumY = 0.0;
                string szMerged = "";
                bool bHasSpec = false;
                for (int k = nStart; k <= nEnd; k++)
                {
                    dSumY += dPixelY[k];
                    if (szMerged.Length == 0)
                    {
                        szMerged = szLabels[k];
                    }
                    else
                    {
                        szMerged = szMerged + "/" + szLabels[k];
                    }

                    if (szLabels[k] == "USL" || szLabels[k] == "LSL")
                    {
                        bHasSpec = true;
                    }
                }

                double dPosY = dSumY / (nEnd - nStart + 1);
                Brush brLine = m_brushMean;
                if (bHasSpec)
                {
                    brLine = m_brushSpec;
                }

                System.Windows.Shapes.Line ln = new System.Windows.Shapes.Line();
                ln.X1 = dPlotX0;
                ln.Y1 = dPosY;
                ln.X2 = dPlotX0 + dPlotW;
                ln.Y2 = dPosY;
                ln.Stroke = brLine;
                ln.StrokeThickness = 1;
                DoubleCollection dash = new DoubleCollection();
                dash.Add(4);
                dash.Add(2);
                ln.StrokeDashArray = dash;
                canvas.Children.Add(ln);

                TextBlock tb = CreateLabel(szMerged, 10, brLine);
                canvas.Children.Add(tb);
                tb.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                double dLabelX = dPlotX0 + dPlotW - tb.DesiredSize.Width - 2;
                double dLabelY = dPosY - tb.DesiredSize.Height - 2;
                if (dLabelY < dPlotY0)
                {
                    dLabelY = dPosY + 2;
                }

                Canvas.SetLeft(tb, dLabelX);
                Canvas.SetTop(tb, dLabelY);

                nStart = nEnd + 1;
            }
        }

        /// <summary>260707 hbk quick-260707-fdx 플롯 영역 좌/하단 축 라인(테두리) 렌더.</summary>
        private void DrawAxisLines(Canvas canvas, double dPlotX0, double dPlotY0, double dPlotW, double dPlotH)
        {
            System.Windows.Shapes.Line lnLeft = new System.Windows.Shapes.Line();
            lnLeft.X1 = dPlotX0;
            lnLeft.Y1 = dPlotY0;
            lnLeft.X2 = dPlotX0;
            lnLeft.Y2 = dPlotY0 + dPlotH;
            lnLeft.Stroke = m_brushAxis;
            lnLeft.StrokeThickness = 1;
            canvas.Children.Add(lnLeft);

            System.Windows.Shapes.Line lnBottom = new System.Windows.Shapes.Line();
            lnBottom.X1 = dPlotX0;
            lnBottom.Y1 = dPlotY0 + dPlotH;
            lnBottom.X2 = dPlotX0 + dPlotW;
            lnBottom.Y2 = dPlotY0 + dPlotH;
            lnBottom.Stroke = m_brushAxis;
            lnBottom.StrokeThickness = 1;
            canvas.Children.Add(lnBottom);
        }

        /// <summary>260707 hbk quick-260707-fdx 히스토그램 y축 도수 눈금(0/중간/최대, 정수 표시).</summary>
        private void DrawYTicksCount(Canvas canvas, double dPlotX0, double dPlotY0, double dPlotH, double dMaxVal)
        {
            const int nTicks = 3;
            for (int i = 0; i < nTicks; i++)
            {
                double dFrac = i / (double)(nTicks - 1);
                double dVal = dMaxVal * dFrac;
                double dY = dPlotY0 + dPlotH - dFrac * dPlotH;
                TextBlock tb = CreateLabel(Math.Round(dVal).ToString(), 10, m_brushText);
                canvas.Children.Add(tb);
                tb.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                Canvas.SetLeft(tb, dPlotX0 - tb.DesiredSize.Width - 4);
                Canvas.SetTop(tb, dY - tb.DesiredSize.Height / 2.0);
            }
        }

        /// <summary>260707 hbk quick-260707-fdx 추이 차트 y축 값 눈금(하한/중간/상한, F3 표시).</summary>
        private void DrawYTicksValue(Canvas canvas, double dPlotX0, double dPlotY0, double dPlotH, double dLo, double dHi)
        {
            const int nTicks = 3;
            for (int i = 0; i < nTicks; i++)
            {
                double dFrac = i / (double)(nTicks - 1);
                double dVal = dLo + (dHi - dLo) * dFrac;
                double dY = dPlotY0 + dPlotH - dFrac * dPlotH;
                TextBlock tb = CreateLabel(dVal.ToString("F3"), 10, m_brushText);
                canvas.Children.Add(tb);
                tb.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                Canvas.SetLeft(tb, dPlotX0 - tb.DesiredSize.Width - 4);
                Canvas.SetTop(tb, dY - tb.DesiredSize.Height / 2.0);
            }
        }

        /// <summary>260707 hbk quick-260707-fdx USL/LSL 수직선 + 상단 라벨(플롯 영역 내에 위치할 때만 호출됨).</summary>
        private void DrawVLine(Canvas canvas, double dX, double dPlotY0, double dPlotH, Brush brush, string szLabel)
        {
            System.Windows.Shapes.Line ln = new System.Windows.Shapes.Line();
            ln.X1 = dX;
            ln.Y1 = dPlotY0;
            ln.X2 = dX;
            ln.Y2 = dPlotY0 + dPlotH;
            ln.Stroke = brush;
            ln.StrokeThickness = 1;
            DoubleCollection dash = new DoubleCollection();
            dash.Add(4);
            dash.Add(2);
            ln.StrokeDashArray = dash;
            canvas.Children.Add(ln);

            TextBlock tb = CreateLabel(szLabel, 10, brush);
            canvas.Children.Add(tb);
            tb.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Canvas.SetLeft(tb, dX - tb.DesiredSize.Width / 2.0);
            Canvas.SetTop(tb, dPlotY0 - tb.DesiredSize.Height - 2);
        }

        /// <summary>260707 hbk quick-260707-fdx 값 없음(N=0) 상태 — 캔버스 중앙에 "데이터 없음" 표시.</summary>
        private void DrawNoDataText(Canvas canvas, double dW, double dH)
        {
            TextBlock tb = CreateLabel("데이터 없음", 13, m_brushAxis);
            canvas.Children.Add(tb);
            tb.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Canvas.SetLeft(tb, (dW - tb.DesiredSize.Width) / 2.0);
            Canvas.SetTop(tb, (dH - tb.DesiredSize.Height) / 2.0);
        }

        /// <summary>260707 hbk quick-260707-fdx Canvas 라벨용 TextBlock 생성 헬퍼.</summary>
        private TextBlock CreateLabel(string szText, double dFontSize, Brush brush)
        {
            TextBlock tb = new TextBlock();
            tb.Text = szText;
            tb.FontSize = dFontSize;
            tb.Foreground = brush;
            return tb;
        }

        /// <summary>260707 hbk quick-260707-fdx RGB 값으로 Freeze 된 SolidColorBrush 생성(정적 필드 초기화용).</summary>
        private static SolidColorBrush MakeFrozenBrush(byte byR, byte byG, byte byB)
        {
            SolidColorBrush brush = new SolidColorBrush(Color.FromRgb(byR, byG, byB));
            brush.Freeze();
            return brush;
        }

        /// <summary>두 차트를 비운다(새 조회 직후 / 선택 없음 상태).</summary>
        private void ClearCharts()
        {
            canvas_Histogram.Children.Clear();
            canvas_Trend.Children.Clear();
        }

        /// <summary>데이터/평균/USL/LSL 을 모두 포함한 y축 표시 범위(하한/상한, 15% 여백)를 계산한다. //260707 hbk</summary>
        private void ComputePaddedRange(List<double> values, double dMean, double dUsl, double dLsl, out double dLoOut, out double dHiOut)   //260707 hbk 축 범위 단일 산출(RenderTrend/DrawTrendSpecMarks 공유)
        {
            double dLo = MinOf(values);   //260707 hbk
            double dHi = MaxOf(values);   //260707 hbk

            double[] extra = new double[3];   //260707 hbk 마크 3종도 범위에 포함
            extra[0] = dMean;
            extra[1] = dUsl;
            extra[2] = dLsl;
            for (int i = 0; i < 3; i++)   //260707 hbk
            {
                if (extra[i] < dLo)
                {
                    dLo = extra[i];
                }
                if (extra[i] > dHi)
                {
                    dHi = extra[i];
                }
            }

            double dPad = (dHi - dLo) * 0.15;   //260707 hbk 15% 여백
            if (dPad <= 0)   //260707 hbk 전 값 동일(범위 0) → 절대 여백으로 축 붕괴 방지
            {
                dPad = Math.Abs(dHi) * 0.1;
                if (dPad <= 0)
                {
                    dPad = 1.0;
                }
            }

            dLoOut = dLo - dPad;   //260707 hbk
            dHiOut = dHi + dPad;   //260707 hbk
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
