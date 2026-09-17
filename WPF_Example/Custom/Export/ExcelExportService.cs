using ClosedXML.Excel;
using ClosedXML.Excel.Drawings;
using ReringProject.Sequence; //260710 hbk SkipReason 상수 참조용
using ReringProject.Setting;
using ReringProject.UI;
using ReringProject.Utility;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace ReringProject.Export
{
    /// <summary>
    /// 결과 xlsx 공용 헬퍼 — 판정 문구, 캡쳐 JPG 대기·셀 삽입. 반복도·일괄검사 export(RepeatExcelExportService)가 공유한다.
    /// </summary>
    public static class ExcelExportService
    {
        // 비동기 write 레이스 대기 파라미터 (CONTEXT 재량 범위 1~2초 안에서 선택)
        private const int CAPTURE_WAIT_TIMEOUT_MS = 1500;     // 파일 1개당 최대 대기
        private const int CAPTURE_WAIT_POLL_MS = 100;         // 폴링 간격

        // 셀 안 이미지 표시 박스(픽셀)
        private const int CAPTURE_BOX_WIDTH_PX = 160;
        private const int CAPTURE_BOX_HEIGHT_PX = 120;

        // 엑셀 단위 환산: 컬럼 폭은 문자수, 행 높이는 포인트(96dpi 기준 1px = 0.75pt)
        private const double EXCEL_PIXELS_PER_WIDTH_UNIT = 7.0;
        private const double EXCEL_POINTS_PER_PIXEL = 0.75;

        private const int JPEG_MIN_BYTES = 4;                 // SOI(2) + EOI(2) 최소 길이

        /// <summary>
        /// 캡쳐 JPG 를 바이트로 읽는다. 경로당 1회만 실제 대기/읽기 (결과가 null 이어도 캐시).
        /// swBudget/nBudgetMs 는 export 1회 전체 폴링 대기 상한 — 호출자가 데이터 규모에 맞춰 정한다.
        /// </summary>
        internal static byte[] LoadCaptureImageBytes(string szPath, Dictionary<string, byte[]> dicCache, Stopwatch swBudget, int nBudgetMs)
        {
            if (string.IsNullOrEmpty(szPath))
            {
                return null;
            }

            if (dicCache.ContainsKey(szPath))
            {
                return dicCache[szPath];
            }

            byte[] arrBytes = WaitForCaptureImage(szPath, swBudget, nBudgetMs);
            dicCache[szPath] = arrBytes;

            bool bLoadFailed = arrBytes == null;
            if (bLoadFailed)
            {
                try
                {
                    Logging.PrintErrLog((int)ELogType.Error, "[ExcelExportService] capture image not ready, cell left blank: " + szPath);
                }
                catch { }
            }

            return arrBytes;
        }

        /// <summary>
        /// CaptureImageSaveService 워커가 JPG 를 비동기로 쓰므로 export 시점에 아직 없거나 쓰는 중일 수 있다.
        /// </summary>
        private static byte[] WaitForCaptureImage(string szPath, Stopwatch swBudget, int nBudgetMs)
        {
            int nWaitedMs = 0;
            while (true)
            {
                if (File.Exists(szPath))
                {
                    byte[] arrBytes = TryReadCompleteJpeg(szPath);
                    if (arrBytes != null)
                    {
                        return arrBytes;
                    }
                }

                bool bBudgetLeft = swBudget.ElapsedMilliseconds < nBudgetMs;
                bool bTimeLeft = nWaitedMs < CAPTURE_WAIT_TIMEOUT_MS;
                if (!bBudgetLeft || !bTimeLeft)
                {
                    return null;
                }

                Thread.Sleep(CAPTURE_WAIT_POLL_MS);
                nWaitedMs += CAPTURE_WAIT_POLL_MS;
            }
        }

        /// <summary>
        /// 워커가 쓰는 도중에 읽으면 잘린 JPEG 이 나온다 → EOI 마커(FF D9)로 파일 완결 여부를 확인한다.
        /// </summary>
        private static byte[] TryReadCompleteJpeg(string szPath)
        {
            try
            {
                byte[] arrBytes = File.ReadAllBytes(szPath);
                if (arrBytes.Length < JPEG_MIN_BYTES)
                {
                    return null;
                }

                bool bHasJpegEoi = arrBytes[arrBytes.Length - 2] == 0xFF && arrBytes[arrBytes.Length - 1] == 0xD9;
                if (!bHasJpegEoi)
                {
                    return null;
                }

                return arrBytes;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 종횡비를 유지한 채 셀 박스 안에 맞춰 그림을 넣는다. 실패해도 export 는 계속된다.
        /// </summary>
        internal static bool TryInsertCaptureImage(IXLWorksheet ws, int nRow, int nColumn, byte[] arrBytes)
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
                    pic = ws.AddPicture(ms, XLPictureFormat.Jpeg);
                }

                int nOriginalWidth = pic.OriginalWidth;
                int nOriginalHeight = pic.OriginalHeight;
                bool bInvalidSize = nOriginalWidth <= 0 || nOriginalHeight <= 0;
                if (bInvalidSize)
                {
                    pic.Delete();
                    return false;
                }

                double dScaleWidth = (double)CAPTURE_BOX_WIDTH_PX / nOriginalWidth;
                double dScaleHeight = (double)CAPTURE_BOX_HEIGHT_PX / nOriginalHeight;
                double dScale = dScaleWidth;
                if (dScaleHeight < dScale)
                {
                    dScale = dScaleHeight;
                }
                if (dScale > 1.0)
                {
                    dScale = 1.0;   // 원본보다 키우지 않는다
                }

                int nTargetWidth = (int)Math.Round(nOriginalWidth * dScale);
                int nTargetHeight = (int)Math.Round(nOriginalHeight * dScale);
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

                ws.Row(nRow).Height = CAPTURE_BOX_HEIGHT_PX * EXCEL_POINTS_PER_PIXEL;

                return true;
            }
            catch (Exception ex)
            {
                try
                {
                    Logging.PrintErrLog((int)ELogType.Error, "[ExcelExportService] capture image insert failed (row " + nRow + "): " + ex.Message);
                }
                catch { }
                return false;
            }
        }

        /// <summary>
        /// 캡쳐이미지 컬럼 폭을 이미지 박스 폭에 맞춘다. AdjustToContents 는 그림을 고려하지 않으므로 '그 뒤에' 불러야 한다.
        /// </summary>
        internal static void ApplyCaptureColumnWidth(IXLWorksheet ws, int nColumn)
        {
            ws.Column(nColumn).Width = CAPTURE_BOX_WIDTH_PX / EXCEL_PIXELS_PER_WIDTH_UNIT;
        }

        /// <summary>
        /// 측정 1건의 판정 표시 문자열. DATUM_FAIL > NO_IMAGE > CROSS_Z_INCOMPLETE > MEASURE_FAIL > OK/NG > "-" 순서.
        /// ReviewMeasurementRow 로직과 일치해야 하며, 일괄검사 상세 시트도 이 함수를 공유한다.
        /// </summary>
        internal static string BuildJudgementText(MeasurementResultDto m)
        {
            if (m == null)
            {
                return "-";
            }

            if (m.LastSkipReason == SkipReason.DATUM_FAIL) //260710 hbk 상수화
            {
                return "DETECT FAIL";
            }
            else if (m.LastSkipReason == SkipReason.NO_IMAGE) //260616 hbk NO_IMAGE 라벨 //260710 hbk 상수화
            {
                return "NO IMAGE";
            }
            else if (m.LastSkipReason == SkipReason.CROSS_Z_INCOMPLETE) //260729 hbk quick-fix(260729-e9q): 크로스-Z 미측정 라벨 (ReviewMeasurementRow 로직 일치)
            {
                return "CROSS-Z INCOMPLETE";
            }
            else if (m.LastSkipReason == SkipReason.MEASURE_FAIL)
            {
                return ReviewMeasurementRow.JUDGE_MEASURE_FAIL;
            }
            else if (m.LastHasResult)
            {
                if (m.LastJudgement)
                {
                    return "OK";
                }
                else
                {
                    return "NG";
                }
            }
            else
            {
                return "-";
            }
        }
    }
}
