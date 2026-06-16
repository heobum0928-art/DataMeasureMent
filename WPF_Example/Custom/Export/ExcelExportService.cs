using ClosedXML.Excel;
using ReringProject.Setting;
using ReringProject.UI;
using ReringProject.Utility;
using System;
using System.Collections.Generic;
using System.IO;

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

                    // D-05 테이블 헤더 (행 5)
                    // "이미지" 하이퍼링크 컬럼 폐기 → 절대 경로(경로\파일명) 텍스트 2컬럼으로 교체.
                    int hr = 5;
                    string[] headers = { "Shot", "FAI", "측정명", "Nominal", "Tol+", "Tol-", "측정값", "판정", "원본이미지 경로", "캡쳐이미지 경로" };
                    for (int i = 0; i < headers.Length; i++)
                    {
                        ws.Cell(hr, i + 1).Value = headers[i];
                    }

                    int row = hr + 1;
                    List<ShotResultDto> shots = cycle.Shots != null ? cycle.Shots : new List<ShotResultDto>();
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
                                if (m.LastSkipReason == "DATUM_FAIL")
                                {
                                    ws.Cell(row, 8).Value = "DETECT FAIL";
                                }
                                else if (m.LastSkipReason == "NO_IMAGE") //260616 hbk NO_IMAGE 라벨
                                {
                                    ws.Cell(row, 8).Value = "NO IMAGE";
                                }
                                else if (m.LastHasResult)
                                {
                                    ws.Cell(row, 8).Value = m.LastJudgement ? "OK" : "NG";
                                }
                                else
                                {
                                    ws.Cell(row, 8).Value = "-";
                                }

                                // D-07 하이퍼링크 폐기(한글경로 file:/// 퍼센트인코딩 이슈 해소), 절대 경로 텍스트로 대체.
                                ws.Cell(row, 9).Value  = fai.OriginImageFileName != null ? fai.OriginImageFileName : "";
                                ws.Cell(row, 10).Value = fai.CaptureImageFileName != null ? fai.CaptureImageFileName : "";

                                row++;
                            }
                        }
                    }

                    ws.Columns().AdjustToContents();
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
    }
}
