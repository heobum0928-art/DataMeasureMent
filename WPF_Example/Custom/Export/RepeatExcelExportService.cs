//260612 hbk Phase 41.1 OUT-03/OUT-04 반복도+알고리즘 통계 xlsx export
using ClosedXML.Excel;
using ReringProject.Sequence;
using ReringProject.Setting;
using ReringProject.UI;
using ReringProject.Utility;
using System;
using System.Collections.Generic;
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
        private class AlgoAggData
        {
            public string Category;
            public string TypeName;
            public int TotalCount;
            public List<double> Values = new List<double>();
        }

        /// <summary>
        /// 수집된 CycleResultDto 목록으로 2-시트 xlsx 를 생성한다.
        /// 실패 시 false 반환 + Logging.
        /// </summary>
        public static bool Export(List<CycleResultDto> cycles, string recipeName, string outputPath)
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
