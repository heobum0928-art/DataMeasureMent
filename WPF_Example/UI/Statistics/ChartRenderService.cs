using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace ReringProject.UI
{
    /// <summary>
    /// 히스토그램/추이 차트를 임의의 Canvas 위에 그리는 정적 드로잉 헬퍼.
    /// StatisticsWindow 에서 순수 이동한 로직 — 알고리즘/좌표/여백 상수는 변경 금지(시각 회귀 방지).
    /// 폭/높이를 인자로 받는 이유: 레이아웃 전 오프스크린 Canvas 는 실측 크기가 0 이라
    /// 호출자가 실제 크기를 알려주지 않으면 아무것도 그려지지 않는다.
    /// </summary>
    public static class ChartRenderService
    {
        private const int BIN_COUNT = 20;             // D-14 히스토그램 bin 수(잠금 결정)
        private const int MAX_X_LABELS = 5;           // 히스토그램/추이 x축 최대 표시 라벨 수(겹침 방지)
        private const double MERGE_PX = 12.0;         // 픽셀 거리 12px 미만이면 라벨 병합

        // WPF Canvas 렌더용 고정 브러시(Freeze — 성능/스레드 안전)
        private static readonly SolidColorBrush m_brushBar = MakeFrozenBrush(0x33, 0x66, 0xCC);
        private static readonly SolidColorBrush m_brushLine = MakeFrozenBrush(0x33, 0x66, 0xCC);
        private static readonly SolidColorBrush m_brushMean = MakeFrozenBrush(0x00, 0x88, 0x00);
        private static readonly SolidColorBrush m_brushSpec = MakeFrozenBrush(0xCC, 0x00, 0x00);
        private static readonly SolidColorBrush m_brushAxis = MakeFrozenBrush(0x94, 0xA3, 0xB8);
        private static readonly SolidColorBrush m_brushText = MakeFrozenBrush(0x33, 0x33, 0x33);

        /// <summary>도수 분포 히스토그램(Rectangle 막대) + USL/LSL 수직선을 지정 Canvas 에 직접 렌더(D-14).</summary>
        public static void RenderHistogram(Canvas canvas, double dW, double dH, List<double> values, double dUsl, double dLsl)
        {
            canvas.Children.Clear();
            if (dW <= 0 || dH <= 0)
            {
                return;
            }

            if (values == null || values.Count == 0)
            {
                DrawNoDataText(canvas, dW, dH);
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

            // 막대(도수 정규화)
            for (int i = 0; i < BIN_COUNT; i++)
            {
                double dBarH = freq[i] / dMaxFreq * dPlotH;
                Rectangle rc = new Rectangle();
                rc.Width = Math.Max(dBinW - 1, 1);
                rc.Height = Math.Max(dBarH, 0);
                rc.Fill = m_brushBar;
                Canvas.SetLeft(rc, dPlotX0 + i * dBinW);
                Canvas.SetTop(rc, dPlotY0 + dPlotH - dBarH);
                canvas.Children.Add(rc);
            }

            DrawAxisLines(canvas, dPlotX0, dPlotY0, dPlotW, dPlotH);

            // x축 라벨(bin 중심값, 5개 내외만 — 겹침 방지)
            int nLabelStep = (int)Math.Ceiling((double)BIN_COUNT / MAX_X_LABELS);
            if (nLabelStep < 1)
            {
                nLabelStep = 1;
            }

            for (int i = 0; i < BIN_COUNT; i += nLabelStep)
            {
                TextBlock tb = CreateLabel(labels[i], 10, m_brushText);
                canvas.Children.Add(tb);
                tb.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                double dCx = dPlotX0 + i * dBinW + dBinW / 2.0;
                Canvas.SetLeft(tb, dCx - tb.DesiredSize.Width / 2.0);
                Canvas.SetTop(tb, dPlotY0 + dPlotH + 2);
            }

            // y축 라벨(0 / 중간 / 최대 도수)
            DrawYTicksCount(canvas, dPlotX0, dPlotY0, dPlotH, dMaxFreq);

            // y축 제목 "빈도(개수)" — 세로 회전 라벨(막대 높이 = 해당 값 구간의 측정 개수)
            TextBlock tbYTitle = CreateLabel("빈도(개수)", 11, m_brushText);
            tbYTitle.LayoutTransform = new RotateTransform(-90);   // 세로로 회전
            canvas.Children.Add(tbYTitle);
            tbYTitle.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Canvas.SetLeft(tbYTitle, 0);
            Canvas.SetTop(tbYTitle, dPlotY0 + (dPlotH - tbYTitle.DesiredSize.Height) / 2.0);

            // USL/LSL 수직선 — 근접(12px 미만) 시 단일 라벨로 병합
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
                    DrawVLine(canvas, dXMid, dPlotY0, dPlotH, m_brushSpec, "USL/LSL");
                }
                else
                {
                    if (bUslIn)
                    {
                        DrawVLine(canvas, dXUsl, dPlotY0, dPlotH, m_brushSpec, "USL");
                    }

                    if (bLslIn)
                    {
                        DrawVLine(canvas, dXLsl, dPlotY0, dPlotH, m_brushSpec, "LSL");
                    }
                }
            }
        }

        /// <summary>샘플 인덱스(1..N) 기준 추이 Polyline + 평균/USL/LSL 수평선을 지정 Canvas 에 직접 렌더(D-13).</summary>
        public static void RenderTrend(Canvas canvas, double dW, double dH, List<double> values, double dMean, double dUsl, double dLsl)
        {
            canvas.Children.Clear();
            if (dW <= 0 || dH <= 0)
            {
                return;
            }

            if (values == null || values.Count == 0)
            {
                DrawNoDataText(canvas, dW, dH);
                return;
            }

            double dMarginL = 55;   // F3 숫자 라벨 표시 위해 히스토그램보다 넓게
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
            canvas.Children.Add(pl);

            DrawAxisLines(canvas, dPlotX0, dPlotY0, dPlotW, dPlotH);
            DrawYTicksValue(canvas, dPlotX0, dPlotY0, dPlotH, dLo, dHi);
            DrawTrendXLabels(canvas, dPlotX0, dPlotY0, dPlotW, dPlotH, nCount);
            DrawTrendSpecMarks(canvas, dPlotX0, dPlotY0, dPlotW, dPlotH, dLo, dSpan, dMean, dUsl, dLsl);
        }

        /// <summary>추이 차트 샘플 인덱스(0-base) → x 픽셀 좌표 환산. N=1 이면 플롯 중앙.</summary>
        private static double TrendIndexToX(int nIdx, int nCount, double dPlotX0, double dPlotW)
        {
            if (nCount > 1)
            {
                return dPlotX0 + (double)nIdx / (nCount - 1) * dPlotW;
            }

            return dPlotX0 + dPlotW / 2.0;
        }

        /// <summary>추이 차트 x축 라벨(샘플 번호 1..N, 5개 내외 — 겹침 방지).</summary>
        private static void DrawTrendXLabels(Canvas canvas, double dPlotX0, double dPlotY0, double dPlotW, double dPlotH, int nCount)
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

        /// <summary>추이 차트 평균/USL/LSL 수평선을 픽셀 y좌표 기준 근접(12px 미만) 그룹으로 병합해 렌더 — 라벨 세로 겹침 제거.</summary>
        private static void DrawTrendSpecMarks(Canvas canvas, double dPlotX0, double dPlotY0, double dPlotW, double dPlotH, double dLo, double dSpan, double dMean, double dUsl, double dLsl)
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

            // 픽셀Y 오름차순 버블 정렬(3개 — 값/라벨 동반 정렬, LINQ 미사용)
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

            // 정렬된 마크를 픽셀 거리 기준 그리디 그룹화 → 그룹당 단일 병합 라벨+선
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

        /// <summary>플롯 영역 좌/하단 축 라인(테두리) 렌더.</summary>
        private static void DrawAxisLines(Canvas canvas, double dPlotX0, double dPlotY0, double dPlotW, double dPlotH)
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

        /// <summary>히스토그램 y축 도수 눈금(0/중간/최대, 정수 표시).</summary>
        private static void DrawYTicksCount(Canvas canvas, double dPlotX0, double dPlotY0, double dPlotH, double dMaxVal)
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

        /// <summary>추이 차트 y축 값 눈금(하한/중간/상한, F3 표시).</summary>
        private static void DrawYTicksValue(Canvas canvas, double dPlotX0, double dPlotY0, double dPlotH, double dLo, double dHi)
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

        /// <summary>USL/LSL 수직선 + 상단 라벨(플롯 영역 내에 위치할 때만 호출됨).</summary>
        private static void DrawVLine(Canvas canvas, double dX, double dPlotY0, double dPlotH, Brush brush, string szLabel)
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

        /// <summary>값 없음(N=0) 상태 — 캔버스 중앙에 "데이터 없음" 표시.</summary>
        private static void DrawNoDataText(Canvas canvas, double dW, double dH)
        {
            TextBlock tb = CreateLabel("데이터 없음", 13, m_brushAxis);
            canvas.Children.Add(tb);
            tb.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Canvas.SetLeft(tb, (dW - tb.DesiredSize.Width) / 2.0);
            Canvas.SetTop(tb, (dH - tb.DesiredSize.Height) / 2.0);
        }

        /// <summary>Canvas 라벨용 TextBlock 생성 헬퍼.</summary>
        private static TextBlock CreateLabel(string szText, double dFontSize, Brush brush)
        {
            TextBlock tb = new TextBlock();
            tb.Text = szText;
            tb.FontSize = dFontSize;
            tb.Foreground = brush;
            return tb;
        }

        /// <summary>RGB 값으로 Freeze 된 SolidColorBrush 생성(정적 필드 초기화용).</summary>
        private static SolidColorBrush MakeFrozenBrush(byte byR, byte byG, byte byB)
        {
            SolidColorBrush brush = new SolidColorBrush(Color.FromRgb(byR, byG, byB));
            brush.Freeze();
            return brush;
        }

        /// <summary>데이터/평균/USL/LSL 을 모두 포함한 y축 표시 범위(하한/상한, 15% 여백)를 계산한다.</summary>
        private static void ComputePaddedRange(List<double> values, double dMean, double dUsl, double dLsl, out double dLoOut, out double dHiOut)   // 축 범위 단일 산출(RenderTrend/DrawTrendSpecMarks 공유)
        {
            double dLo = MinOf(values);
            double dHi = MaxOf(values);

            double[] extra = new double[3];   // 마크 3종도 범위에 포함
            extra[0] = dMean;
            extra[1] = dUsl;
            extra[2] = dLsl;
            for (int i = 0; i < 3; i++)
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

            double dPad = (dHi - dLo) * 0.15;   // 15% 여백
            if (dPad <= 0)   // 전 값 동일(범위 0) → 절대 여백으로 축 붕괴 방지
            {
                dPad = Math.Abs(dHi) * 0.1;
                if (dPad <= 0)
                {
                    dPad = 1.0;
                }
            }

            dLoOut = dLo - dPad;
            dHiOut = dHi + dPad;
        }

        /// <summary>min~max 균등 nBins 분할 도수 계산. max==min 이면 단일 bin 처리(0 나눗셈 방어).</summary>
        private static double[] BuildHistogramBins(List<double> values, int nBins, out string[] labels)
        {
            double[] freq = new double[nBins];
            labels = new string[nBins];

            double dMin = MinOf(values);
            double dMax = MaxOf(values);
            double dSpan = dMax - dMin;

            if (dSpan <= 0)   // 전 값 동일 → 첫 bin 에 전부 집계(0 나눗셈 방어)
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
        private static double MinOf(List<double> values)
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
        private static double MaxOf(List<double> values)
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
