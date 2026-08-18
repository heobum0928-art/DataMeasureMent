using ClosedXML.Excel;
using ClosedXML.Excel.Drawings;
using ReringProject.Setting;
using ReringProject.UI;
using ReringProject.Utility;
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ReringProject.Export
{
    /// <summary>
    /// 오프스크린 Canvas 에 차트를 그려 PNG 바이트로 뽑고, xlsx 워크시트에 그림으로 삽입한다.
    /// Window/HWND 없이 bare Canvas + RenderTargetBitmap 만 사용한다.
    /// 예외는 전부 흡수하고 null/false 를 반환한다 — 차트 실패가 export 전체를 막지 않는다.
    /// </summary>
    public static class ChartImageCapture
    {
        // 오프스크린 렌더 캔버스 크기(픽셀). 이 값이 곧 PNG 해상도가 된다.
        private const int CHART_RENDER_WIDTH_PX = 480;
        private const int CHART_RENDER_HEIGHT_PX = 320;

        // 엑셀 시트에 표시할 박스 크기(픽셀).
        private const int CHART_BOX_WIDTH_PX = 360;
        private const int CHART_BOX_HEIGHT_PX = 240;

        private const double CHART_DPI = 96.0;

        /// <summary>
        /// 오프스크린 Canvas 를 PNG 바이트로 캡처한다. 실패 시 null.
        /// Window 없이 bare Canvas 를 쓰므로 HWND/PresentationSource 가 필요 없다.
        /// Measure/Arrange/UpdateLayout 3단계는 필수 — 빠뜨리면 조용히 빈 이미지가 나온다.
        /// </summary>
        private static byte[] CaptureCanvasPng(Canvas canvas)
        {
            try
            {
                canvas.Measure(new Size(CHART_RENDER_WIDTH_PX, CHART_RENDER_HEIGHT_PX));
                canvas.Arrange(new Rect(0, 0, CHART_RENDER_WIDTH_PX, CHART_RENDER_HEIGHT_PX));
                canvas.UpdateLayout();

                var rtb = new RenderTargetBitmap(
                    CHART_RENDER_WIDTH_PX, CHART_RENDER_HEIGHT_PX,
                    CHART_DPI, CHART_DPI, PixelFormats.Pbgra32);
                rtb.Render(canvas);

                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(rtb));

                using (var ms = new MemoryStream())
                {
                    encoder.Save(ms);
                    return ms.ToArray();
                }
            }
            catch (Exception ex)
            {
                try
                {
                    Logging.PrintErrLog((int)ELogType.Error, "[ChartImageCapture] CaptureCanvasPng failed: " + ex.Message);
                }
                catch { }

                return null;
            }
        }

        /// <summary>
        /// 렌더 대상 오프스크린 Canvas 를 만든다.
        /// 배경을 흰색으로 지정하는 이유: Pbgra32 기본은 투명이라 엑셀에서 회색으로 보일 수 있다.
        /// Background 는 자식 요소가 아니므로 렌더 서비스의 Children.Clear() 에 지워지지 않는다.
        /// </summary>
        private static Canvas CreateChartCanvas()
        {
            var canvas = new Canvas();
            canvas.Width = CHART_RENDER_WIDTH_PX;
            canvas.Height = CHART_RENDER_HEIGHT_PX;
            canvas.Background = Brushes.White;
            return canvas;
        }

        /// <summary>
        /// UI/STA 스레드에서 렌더 함수를 실행한다. 이미 UI 스레드면 직접 호출.
        /// 백그라운드에서 호출되면 Dispatcher.Invoke 로 마샬링한다(ReviewerWindow 관례).
        /// </summary>
        private static byte[] InvokeOnUiThread(Func<byte[]> fnRender)
        {
            var app = Application.Current;
            if (app == null)
            {
                return fnRender();
            }

            if (app.Dispatcher.CheckAccess())
            {
                return fnRender();
            }

            return app.Dispatcher.Invoke(fnRender);
        }

        /// <summary>도수 분포 히스토그램을 PNG 바이트로 렌더한다. 값이 없으면 null.</summary>
        public static byte[] RenderHistogramPng(List<double> values, double dUsl, double dLsl)
        {
            bool bEmpty = values == null || values.Count == 0;
            if (bEmpty)
            {
                return null;
            }

            return InvokeOnUiThread(() =>
            {
                var canvas = CreateChartCanvas();
                ChartRenderService.RenderHistogram(canvas, CHART_RENDER_WIDTH_PX, CHART_RENDER_HEIGHT_PX, values, dUsl, dLsl);
                return CaptureCanvasPng(canvas);
            });
        }

        /// <summary>샘플 인덱스 기준 추이 차트를 PNG 바이트로 렌더한다. 값이 없으면 null.</summary>
        public static byte[] RenderTrendPng(List<double> values, double dMean, double dUsl, double dLsl)
        {
            bool bEmpty = values == null || values.Count == 0;
            if (bEmpty)
            {
                return null;
            }

            return InvokeOnUiThread(() =>
            {
                var canvas = CreateChartCanvas();
                ChartRenderService.RenderTrend(canvas, CHART_RENDER_WIDTH_PX, CHART_RENDER_HEIGHT_PX, values, dMean, dUsl, dLsl);
                return CaptureCanvasPng(canvas);
            });
        }

        /// <summary>
        /// PNG 바이트를 지정 셀에 종횡비 유지로 삽입한다. 실패해도 export 는 계속된다(throw 금지).
        /// </summary>
        internal static bool TryInsertChartPicture(IXLWorksheet ws, int nRow, int nColumn, byte[] arrBytes)
        {
            bool bHasBytes = arrBytes != null && arrBytes.Length > 0;
            if (!bHasBytes)
            {
                return false;
            }

            try
            {
                IXLPicture pic;
                using (var ms = new MemoryStream(arrBytes))
                {
                    pic = ws.AddPicture(ms, XLPictureFormat.Png);
                }

                int nOriginalWidth = pic.OriginalWidth;
                int nOriginalHeight = pic.OriginalHeight;
                bool bInvalidSize = nOriginalWidth <= 0 || nOriginalHeight <= 0;
                if (bInvalidSize)
                {
                    pic.Delete();
                    return false;
                }

                double dScaleWidth = (double)CHART_BOX_WIDTH_PX / nOriginalWidth;
                double dScaleHeight = (double)CHART_BOX_HEIGHT_PX / nOriginalHeight;
                double dScale = dScaleWidth;
                if (dScaleHeight < dScale)
                {
                    dScale = dScaleHeight;
                }

                if (dScale > 1.0)
                {
                    dScale = 1.0;   // 원본보다 키우지 않는다
                }

                int nTargetWidth = (int)(nOriginalWidth * dScale);
                int nTargetHeight = (int)(nOriginalHeight * dScale);
                if (nTargetWidth < 1)
                {
                    nTargetWidth = 1;
                }

                if (nTargetHeight < 1)
                {
                    nTargetHeight = 1;
                }

                pic.WithPlacement(XLPicturePlacement.Move);
                pic.WithSize(nTargetWidth, nTargetHeight);
                pic.MoveTo(ws.Cell(nRow, nColumn));

                return true;
            }
            catch (Exception ex)
            {
                try
                {
                    Logging.PrintErrLog((int)ELogType.Error, "[ChartImageCapture] chart picture insert failed (row " + nRow + "): " + ex.Message);
                }
                catch { }

                return false;
            }
        }

        /// <summary>
        /// 합성 샘플로 히스토그램/추이 PNG 2장을 지정 폴더에 저장한다. 오프스크린 렌더 동작 점검용.
        /// 반환 = 성공 여부, szMessage = 저장 경로 또는 실패 사유.
        /// </summary>
        public static bool TrySaveSmokePng(string szFolder, out string szMessage)
        {
            szMessage = "";

            try
            {
                var values = new List<double>();
                var rnd = new Random(20260818);
                for (int i = 0; i < 100; i++)
                {
                    values.Add(10.0 + (rnd.NextDouble() - 0.5) * 0.4);
                }

                double dNominal = 10.0;
                double dUsl = dNominal + 0.2;
                double dLsl = dNominal - 0.2;

                byte[] arrHist = RenderHistogramPng(values, dUsl, dLsl);
                byte[] arrTrend = RenderTrendPng(values, dNominal, dUsl, dLsl);

                bool bBad = arrHist == null || arrHist.Length == 0 || arrTrend == null || arrTrend.Length == 0;
                if (bBad)
                {
                    szMessage = "PNG 생성 실패 (빈 바이트) — 로그 확인";
                    return false;
                }

                string szHist = Path.Combine(szFolder, "chart_smoke_histogram.png");
                string szTrend = Path.Combine(szFolder, "chart_smoke_trend.png");
                File.WriteAllBytes(szHist, arrHist);
                File.WriteAllBytes(szTrend, arrTrend);

                szMessage = szHist + "\n" + szTrend;
                return true;
            }
            catch (Exception ex)
            {
                szMessage = ex.Message;
                return false;
            }
        }
    }
}
