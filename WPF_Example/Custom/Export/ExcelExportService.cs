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
    /// 1회 검사 cycle 결과(CycleResultDto, Plan 01 단일 소스)를 xlsx 로 export 한다 (OUT-02).
    /// 상단 메타 헤더 블록(모델명·검사일시·종합판정) + 1행=1측정 테이블(Shot/FAI/측정명/Nominal/Tol+/Tol-/측정값/판정)
    /// + 이미지 하이퍼링크 컬럼. 리뷰어 [엑셀 export] 버튼이 현재 선택된 cycle 을 전달한다.
    /// 예외는 전부 try/catch → false 반환 + Logging (T-40-12: 앱 크래시 0).
    /// </summary>
    public static class ExcelExportService
    {
        private const int CAPTURE_IMAGE_COLUMN = 11;          // 캡쳐이미지 경로(10) 바로 뒤

        // 비동기 write 레이스 대기 파라미터 (CONTEXT 재량 범위 1~2초 안에서 선택)
        private const int CAPTURE_WAIT_TIMEOUT_MS = 1500;     // 파일 1개당 최대 대기
        private const int CAPTURE_WAIT_POLL_MS = 100;         // 폴링 간격
        private const int CAPTURE_WAIT_BUDGET_MS = 5000;      // export 1회 전체 대기 예산 상한

        // 셀 안 이미지 표시 박스(픽셀)
        private const int CAPTURE_BOX_WIDTH_PX = 160;
        private const int CAPTURE_BOX_HEIGHT_PX = 120;

        // 엑셀 단위 환산: 컬럼 폭은 문자수, 행 높이는 포인트(96dpi 기준 1px = 0.75pt)
        private const double EXCEL_PIXELS_PER_WIDTH_UNIT = 7.0;
        private const double EXCEL_POINTS_PER_PIXEL = 0.75;

        private const int JPEG_MIN_BYTES = 4;                 // SOI(2) + EOI(2) 최소 길이

        /// <summary>
        /// cycle 을 outputPath 에 xlsx 로 저장한다. 성공 시 true, 실패(null 인자/예외) 시 false.
        /// </summary>
        public static bool Export(CycleResultDto cycle, string outputPath)
        {
            if (cycle == null || string.IsNullOrEmpty(outputPath))
            {
                return false;
            }

            try
            {
                using (var wb = new XLWorkbook())
                {
                    var ws = wb.Worksheets.Add("Result");

                    // D-06 메타 헤더 블록 (행 1~3)
                    ws.Cell(1, 1).Value = "모델명";
                    ws.Cell(1, 2).Value = cycle.RecipeName != null ? cycle.RecipeName : "";
                    ws.Cell(2, 1).Value = "검사일시";
                    ws.Cell(2, 2).Value = cycle.InspectionTime.ToString("yyyy-MM-dd HH:mm:ss");
                    ws.Cell(3, 1).Value = "종합판정";
                    ws.Cell(3, 2).Value = cycle.OverallJudgement != null ? cycle.OverallJudgement : "";

                    //260622 hbk Phase 48 PROTO-01: 자재번호 메타 행 (D-05). IndexNumber -1 = 미수신 → "-".
                    ws.Cell(4, 1).Value = "자재번호";
                    if (cycle.IndexNumber >= 0)
                    {
                        ws.Cell(4, 2).Value = cycle.IndexNumber;
                    }
                    else
                    {
                        ws.Cell(4, 2).Value = "-";
                    }

                    // D-05 테이블 헤더 (행 6 — 자재번호 행 추가로 5→6 이동)
                    // "이미지" 하이퍼링크 컬럼 폐기 → 절대 경로(경로\파일명) 텍스트 2컬럼으로 교체.
                    int hr = 6;  //260622 hbk Phase 48 PROTO-01: 자재번호 행(행 4) 추가에 따른 오프셋 조정 (5→6)
                    string[] headers = { "Shot", "FAI", "측정명", "Nominal", "Tol+", "Tol-", "측정값", "판정", "원본이미지 경로", "캡쳐이미지 경로", "캡쳐이미지" };
                    for (int i = 0; i < headers.Length; i++)
                    {
                        ws.Cell(hr, i + 1).Value = headers[i];
                    }

                    int row = hr + 1;
                    List<ShotResultDto> shots = cycle.Shots != null ? cycle.Shots : new List<ShotResultDto>();

                    // 같은 FAI 의 여러 측정 행은 캡쳐 경로가 동일하다 → 경로당 1회만 대기/읽기 (null 결과도 캐시해서 재대기 방지)
                    var dicCaptureCache = new Dictionary<string, byte[]>();
                    // export 1회 전체 폴링 대기 상한. UI 스레드 호출이므로 이미지가 통째로 없는 cycle 에서 수 분 멈추는 것을 막는다.
                    var swWaitBudget = Stopwatch.StartNew();

                    foreach (var shot in shots)
                    {
                        List<FaiResultDto> fais = shot.FAIs != null ? shot.FAIs : new List<FaiResultDto>();
                        foreach (var fai in fais)
                        {
                            List<MeasurementResultDto> measurements = fai.Measurements != null ? fai.Measurements : new List<MeasurementResultDto>();
                            foreach (var m in measurements)
                            {
                                ws.Cell(row, 1).Value = shot.ShotName != null ? shot.ShotName : "";
                                ws.Cell(row, 2).Value = fai.FAIName != null ? fai.FAIName : "";
                                ws.Cell(row, 3).Value = m.MeasurementName != null ? m.MeasurementName : "";
                                ws.Cell(row, 4).Value = m.NominalValue;
                                ws.Cell(row, 5).Value = m.TolerancePlus;
                                ws.Cell(row, 6).Value = m.ToleranceMinus;

                                // 측정값: CO-23-01 — 0.0 도 정상 결과, HasResult 플래그로 판별
                                if (m.LastHasResult)
                                {
                                    ws.Cell(row, 7).Value = m.LastMeasuredValue;
                                }
                                else
                                {
                                    ws.Cell(row, 7).Value = "-";
                                }

                                // 판정 3분기: DATUM_FAIL > HasResult 유무 > OK/NG (ReviewMeasurementRow 로직 일치)
                                ws.Cell(row, 8).Value = BuildJudgementText(m);

                                // D-07 하이퍼링크 폐기(한글경로 file:/// 퍼센트인코딩 이슈 해소), 절대 경로 텍스트로 대체.
                                ws.Cell(row, 9).Value  = fai.OriginImageFileName != null ? fai.OriginImageFileName : "";
                                ws.Cell(row, 10).Value = fai.CaptureImageFileName != null ? fai.CaptureImageFileName : "";

                                byte[] arrCaptureBytes = LoadCaptureImageBytes(fai.CaptureImageFileName, dicCaptureCache, swWaitBudget, CAPTURE_WAIT_BUDGET_MS);
                                TryInsertCaptureImage(ws, row, CAPTURE_IMAGE_COLUMN, arrCaptureBytes);

                                row++;
                            }
                        }
                    }

                    ws.Columns().AdjustToContents();
                    ApplyCaptureColumnWidth(ws, CAPTURE_IMAGE_COLUMN);
                    wb.SaveAs(outputPath);
                    return true;
                }
            }
            catch (Exception ex)
            {
                try
                {
                    Logging.PrintErrLog((int)ELogType.Error, "[ExcelExportService] Export failed: " + ex.Message);
                }
                catch { }
                return false;
            }
        }

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
        /// 측정 1건의 판정 표시 문자열. DATUM_FAIL > NO_IMAGE > CROSS_Z_INCOMPLETE > OK/NG > "-" 순서.
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
