//260612 hbk Phase 41.1 OUT-03/OUT-04 반복도+알고리즘 통계 xlsx export
using ClosedXML.Excel;
using ReringProject.Sequence;
using ReringProject.Setting;
using ReringProject.UI;
using ReringProject.Utility;
using System;
using System.Collections.Generic;
using System.Diagnostics;   // Stopwatch (전체 대기 예산)
using System.Linq;

namespace ReringProject.Export
{
    /// <summary>
    /// 반복 측정 결과(List&lt;CycleResultDto&gt;)를 2-시트 xlsx 로 export 한다.
    /// 시트1 "반복도 통계" (OUT-03): Shot/FAI/측정명별 N/Mean/StdDev/Range/Cpk/OK/NG/DETECT_FAIL.
    /// 시트2 "알고리즘 통계" (OUT-04): TypeName → 카테고리 집계 (N/성공률/Mean/StdDev).
    /// 예외는 전부 try/catch → false + Logging (T-40-12 패턴 동일).
    /// </summary>
    public static class RepeatExcelExportService
    {
        private const string DETAIL_SHEET_NAME = "회차별 상세";
        private const int DETAIL_CAPTURE_IMAGE_COLUMN = 12;   // 회차(1) 가 앞에 붙어 단일 cycle 포맷보다 1 밀림

        // cycle 수에 비례해 늘리되 상한을 둔다. UI 스레드 동기 호출이므로 무한정 블로킹은 금지 (E1L-05).
        private const int BATCH_CAPTURE_WAIT_PER_CYCLE_MS = 1000;
        private const int BATCH_CAPTURE_WAIT_BUDGET_MIN_MS = 5000;
        private const int BATCH_CAPTURE_WAIT_BUDGET_MAX_MS = 15000;

        private class AlgoAggData
        {
            public string Category;
            public string TypeName;
            public int TotalCount;
            public List<double> Values = new List<double>();
        }

        /// <summary>
        /// 반복검사(Gage R&R) export. 집계 2시트만 생성한다 — 기존 동작/포맷 그대로.
        /// </summary>
        public static bool Export(List<CycleResultDto> cycles, string recipeName, string outputPath)
        {
            return ExportInternal(cycles, recipeName, outputPath, false);
        }

        /// <summary>
        /// 일괄검사(Batch) export. 집계 2시트 + 회차별 상세 시트(캡쳐이미지 포함)를 생성한다.
        /// 회차별 실물 부품이 서로 다르므로 cycle 별 측정값/이미지가 의미를 가진다.
        /// </summary>
        public static bool ExportBatch(List<CycleResultDto> cycles, string recipeName, string outputPath)
        {
            return ExportInternal(cycles, recipeName, outputPath, true);
        }

        private static bool ExportInternal(List<CycleResultDto> cycles, string recipeName, string outputPath, bool bWithDetailSheet)
        {
            if (cycles == null || cycles.Count == 0 || string.IsNullOrEmpty(outputPath))
            {
                return false;
            }

            try
            {
                // 시트1용 통계 계산
                var stats = new RepeatMeasurementStats();
                foreach (var c in cycles)
                {
                    stats.AddSample(c);
                }

                var statDict = stats.ComputeAll();

                using (var wb = new XLWorkbook())
                {
                    // === 시트1: 반복도 통계 (OUT-03) ===
                    var ws1 = wb.Worksheets.Add("반복도 통계");

                    ws1.Cell(1, 1).Value = "모델명";
                    ws1.Cell(1, 2).Value = recipeName ?? "";
                    ws1.Cell(2, 1).Value = "측정일시";
                    ws1.Cell(2, 2).Value = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                    ws1.Cell(3, 1).Value = "반복횟수";
                    ws1.Cell(3, 2).Value = cycles.Count;

                    //260616 hbk Phase 51 UAT: CPK/StdDev/Range 제거, Mean→측정값, Nominal→Spec, 편차(측정값-Spec) 추가
                    string[] h1 = { "Shot", "FAI", "측정명", "N", "측정값", "Spec", "편차",
                                    "Tol+", "Tol-", "OK수", "NG수", "DETECT_FAIL수" };
                    for (int i = 0; i < h1.Length; i++)
                    {
                        ws1.Cell(5, i + 1).Value = h1[i];
                    }

                    int r = 6;
                    foreach (var kv in statDict)
                    {
                        var s = kv.Value;
                        ws1.Cell(r, 1).Value = s.ShotName ?? "";
                        ws1.Cell(r, 2).Value = s.FAIName ?? "";
                        ws1.Cell(r, 3).Value = s.MeasurementName ?? "";
                        ws1.Cell(r, 4).Value = s.N;
                        ws1.Cell(r, 5).Value = Math.Round(s.Mean, 6);                       //측정값
                        ws1.Cell(r, 6).Value = s.NominalValue;                              //Spec
                        ws1.Cell(r, 7).Value = Math.Round(Math.Abs(s.Mean - s.NominalValue), 6); //편차 = |측정값 - Spec| (절대값)
                        ws1.Cell(r, 8).Value = s.TolerancePlus;
                        ws1.Cell(r, 9).Value = s.ToleranceMinus;
                        ws1.Cell(r, 10).Value = s.OkCount;
                        ws1.Cell(r, 11).Value = s.NgCount;
                        ws1.Cell(r, 12).Value = s.DetectFailCount;
                        r++;
                    }

                    ws1.Columns().AdjustToContents();

                    // === 시트2: 알고리즘 통계 (OUT-04) ===
                    var ws2 = wb.Worksheets.Add("알고리즘 통계");

                    var algoMap = new Dictionary<string, AlgoAggData>();
                    foreach (var cycle in cycles)
                    {
                        if (cycle.Shots == null)
                        {
                            continue;
                        }

                        foreach (var shot in cycle.Shots)
                        {
                            if (shot.FAIs == null)
                            {
                                continue;
                            }

                            foreach (var fai in shot.FAIs)
                            {
                                if (fai.Measurements == null)
                                {
                                    continue;
                                }

                                foreach (var m in fai.Measurements)
                                {
                                    string typeName = m.TypeName ?? "Other";
                                    string cat = MapAlgorithmCategory(typeName);
                                    string algoKey = cat + "|" + typeName;

                                    AlgoAggData agg;
                                    if (!algoMap.TryGetValue(algoKey, out agg))
                                    {
                                        agg = new AlgoAggData { Category = cat, TypeName = typeName };
                                        algoMap[algoKey] = agg;
                                    }

                                    agg.TotalCount++;
                                    if (m.LastHasResult)
                                    {
                                        agg.Values.Add(m.LastMeasuredValue);
                                    }
                                }
                            }
                        }
                    }

                    string[] h2 = { "알고리즘 분류", "TypeName", "N(측정수)", "성공률(%)", "Mean", "StdDev" };
                    for (int i = 0; i < h2.Length; i++)
                    {
                        ws2.Cell(1, i + 1).Value = h2[i];
                    }

                    int r2 = 2;
                    foreach (var kv2 in algoMap)
                    {
                        var a = kv2.Value;
                        int validN = a.Values.Count;
                        double successRate = a.TotalCount > 0 ? (validN * 100.0 / a.TotalCount) : 0.0;
                        double mean2 = validN > 0 ? a.Values.Sum() / validN : 0.0;
                        double stddev2 = 0.0;
                        if (validN > 1)
                        {
                            double sumSq = 0;
                            foreach (var v in a.Values)
                            {
                                sumSq += (v - mean2) * (v - mean2);
                            }

                            stddev2 = Math.Sqrt(sumSq / (validN - 1));
                        }

                        ws2.Cell(r2, 1).Value = a.Category;
                        ws2.Cell(r2, 2).Value = a.TypeName;
                        ws2.Cell(r2, 3).Value = validN;
                        ws2.Cell(r2, 4).Value = Math.Round(successRate, 1);
                        ws2.Cell(r2, 5).Value = Math.Round(mean2, 6);
                        ws2.Cell(r2, 6).Value = Math.Round(stddev2, 6);
                        r2++;
                    }

                    ws2.Columns().AdjustToContents();

                    if (bWithDetailSheet)
                    {
                        AppendDetailSheet(wb, cycles);
                    }

                    wb.SaveAs(outputPath);
                }

                return true;
            }
            catch (Exception ex)
            {
                try
                {
                    Logging.PrintErrLog((int)ELogType.Error, "[RepeatExcelExportService] Export failed: " + ex.Message);
                }
                catch { }

                return false;
            }
        }

        /// <summary>
        /// 회차별 상세 시트: cycle(=실물 부품 1개) × Shot × FAI × 측정을 1행씩 펼치고 캡쳐이미지를 셀에 넣는다.
        /// 이미지 로드/삽입은 ExcelExportService 헬퍼를 그대로 재사용한다 (로직 복제 금지).
        /// </summary>
        private static void AppendDetailSheet(XLWorkbook wb, List<CycleResultDto> cycles)
        {
            var ws = wb.Worksheets.Add(DETAIL_SHEET_NAME);

            string[] h3 = { "회차", "Shot", "FAI", "측정명", "Nominal", "Tol+", "Tol-", "측정값", "판정",
                            "원본이미지 경로", "캡쳐이미지 경로", "캡쳐이미지" };
            for (int i = 0; i < h3.Length; i++)
            {
                ws.Cell(1, i + 1).Value = h3[i];
            }

            // cycle 수에 비례한 대기 예산(하한/상한 클램프). 곱셈 오버플로 방지를 위해 상한 초과 여부를 먼저 판정한다.
            int nCycleCount = cycles.Count;
            int nBudgetMs = BATCH_CAPTURE_WAIT_BUDGET_MAX_MS;
            bool bUnderMaxCycles = nCycleCount < (BATCH_CAPTURE_WAIT_BUDGET_MAX_MS / BATCH_CAPTURE_WAIT_PER_CYCLE_MS);
            if (bUnderMaxCycles)
            {
                nBudgetMs = nCycleCount * BATCH_CAPTURE_WAIT_PER_CYCLE_MS;
            }
            if (nBudgetMs < BATCH_CAPTURE_WAIT_BUDGET_MIN_MS)
            {
                nBudgetMs = BATCH_CAPTURE_WAIT_BUDGET_MIN_MS;
            }

            // 시트 전체에서 공유하는 캐시/Stopwatch 1개 — cycle 마다 새로 만들면 예산이 곱해져 상한이 무의미해진다.
            var dicCaptureCache = new Dictionary<string, byte[]>();
            var swWaitBudget = Stopwatch.StartNew();

            int nRow = 2;
            int nCycleNo = 1;
            foreach (var cycle in cycles)
            {
                if (cycle == null)
                {
                    nCycleNo++;
                    continue;
                }

                if (cycle.Shots == null)
                {
                    nCycleNo++;
                    continue;
                }

                foreach (var shot in cycle.Shots)
                {
                    if (shot.FAIs == null)
                    {
                        continue;
                    }

                    foreach (var fai in shot.FAIs)
                    {
                        if (fai.Measurements == null)
                        {
                            continue;
                        }

                        foreach (var m in fai.Measurements)
                        {
                            WriteDetailRow(ws, nRow, nCycleNo, shot, fai, m, dicCaptureCache, swWaitBudget, nBudgetMs);
                            nRow++;
                        }
                    }
                }

                nCycleNo++;
            }

            ws.Columns().AdjustToContents();
            ExcelExportService.ApplyCaptureColumnWidth(ws, DETAIL_CAPTURE_IMAGE_COLUMN);
        }

        /// <summary>
        /// 상세 시트 1행 = 측정 1건. 값/판정은 단일 cycle 포맷(ExcelExportService)과 동일하게 맞춘다.
        /// </summary>
        private static void WriteDetailRow(IXLWorksheet ws, int nRow, int nCycleNo, ShotResultDto shot, FaiResultDto fai,
                                           MeasurementResultDto m, Dictionary<string, byte[]> dicCache, Stopwatch swBudget, int nBudgetMs)
        {
            ws.Cell(nRow, 1).Value = nCycleNo;

            if (shot.ShotName != null)
            {
                ws.Cell(nRow, 2).Value = shot.ShotName;
            }
            else
            {
                ws.Cell(nRow, 2).Value = "";
            }

            if (fai.FAIName != null)
            {
                ws.Cell(nRow, 3).Value = fai.FAIName;
            }
            else
            {
                ws.Cell(nRow, 3).Value = "";
            }

            if (m.MeasurementName != null)
            {
                ws.Cell(nRow, 4).Value = m.MeasurementName;
            }
            else
            {
                ws.Cell(nRow, 4).Value = "";
            }

            ws.Cell(nRow, 5).Value = m.NominalValue;
            ws.Cell(nRow, 6).Value = m.TolerancePlus;
            ws.Cell(nRow, 7).Value = m.ToleranceMinus;

            // 측정값: 0.0 도 정상 결과이므로 값이 아니라 LastHasResult 로 판별한다 (CO-23-01).
            if (m.LastHasResult)
            {
                ws.Cell(nRow, 8).Value = m.LastMeasuredValue;
            }
            else
            {
                ws.Cell(nRow, 8).Value = "-";
            }

            ws.Cell(nRow, 9).Value = ExcelExportService.BuildJudgementText(m);

            if (fai.OriginImageFileName != null)
            {
                ws.Cell(nRow, 10).Value = fai.OriginImageFileName;
            }
            else
            {
                ws.Cell(nRow, 10).Value = "";
            }

            if (fai.CaptureImageFileName != null)
            {
                ws.Cell(nRow, 11).Value = fai.CaptureImageFileName;
            }
            else
            {
                ws.Cell(nRow, 11).Value = "";
            }

            byte[] arrCaptureBytes = ExcelExportService.LoadCaptureImageBytes(fai.CaptureImageFileName, dicCache, swBudget, nBudgetMs);
            ExcelExportService.TryInsertCaptureImage(ws, nRow, DETAIL_CAPTURE_IMAGE_COLUMN, arrCaptureBytes);
        }

        private static string MapAlgorithmCategory(string typeName)
        {
            if (typeName == "TwoLineIntersect" || typeName == "PointToPoint" ||
                typeName == "LineToLineAngle" || typeName == "LineToLineDistance" ||
                typeName == "PointToLineDistance")
            {
                return "TLI";
            }

            if (typeName == "CircleTwoHorizontal" || typeName == "CircleDiameterMeasurement")
            {
                return "CTH";
            }

            if (typeName == "VerticalTwoHorizontal")
            {
                return "VTH";
            }

            if (typeName == "EdgeToLineDistance" || typeName == "EdgeToLineAngle")
            {
                return "EdgeToLine";
            }

            if (typeName == "ArcEdgeDistance" || typeName == "ArcLineIntersectDistance" ||
                typeName == "CircleCenterDistance")
            {
                return "ArcEdge";
            }

            if (typeName == "CompoundAngle" || typeName == "CompoundCenterCDistance" ||
                typeName == "CompoundCenterBDistance" || typeName == "CompoundShortAxisDistance")
            {
                return "Compound";
            }

            if (typeName == "DualImageEdgeDistanceMeasurement")
            {
                return "DualImage";
            }

            return "Other";
        }
    }
}
