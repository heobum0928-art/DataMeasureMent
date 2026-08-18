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
    /// 고객 참고양식(Rapid City A8.1_Z Stopper Data Report R04) 대조 CPK 리포트 export.
    /// 시트는 2장 고정: "RAW DATA(1)"(가로형 매트릭스) + "1Cav 세부치수_Cpk"(통계).
    /// 자재번호(CycleResultDto.IndexNumber)는 열 축이며 시트 축이 아니다.
    /// 열 1개 = 검사 1회차이고, 자재번호는 인접 열들을 묶는 라벨로 쓴다(260818 확정).
    /// 예외는 전부 try/catch → false + Logging (기존 export 관례 동일).
    /// </summary>
    public static class CpkReportExportService
    {
        private const string RAW_SHEET_NAME = "RAW DATA(1)";

        private const int RAW_MATERIAL_ROW = 4;        // 자재 라벨 행
        private const int RAW_HEADER_ROW = 5;          // 고정 헤더 + #n 헤더 행
        private const int RAW_FIRST_DATA_ROW = 6;
        private const int RAW_FIRST_SAMPLE_COLUMN = 7; // A~F 가 고정 6열이므로 샘플은 G(7)부터

        private const string NO_VALUE_TEXT = "-";
        private const string MATERIAL_UNSET_LABEL = "미지정";
        private const int MATERIAL_NOT_SET = -1;

        private const string CPK_SHEET_NAME = "1Cav 세부치수_Cpk";
        private const double CPK_WARN_THRESHOLD = 1.33;

        private const int CPK_SUMMARY_OK_ROW = 1;
        private const int CPK_SUMMARY_NG_ROW = 2;
        private const int CPK_SUMMARY_LIST_ROW = 3;
        private const int CPK_HEADER_ROW = 5;
        private const int CPK_FIRST_DATA_ROW = 6;

        // 좌측 블록 B~L, 우측 통계 블록 N~V, 판정 X (참고양식 열 배치 유지, M/W 는 구분 공백열)
        private const int CPK_LEFT_FIRST_COLUMN = 2;    // B
        private const int CPK_RIGHT_FIRST_COLUMN = 14;  // N
        private const int CPK_JUDGE_COLUMN = 24;        // X

        private const string JUDGE_NG = "N G";
        private const string JUDGE_WARN = "Cpk";
        private const string JUDGE_OK = "O K";

        private const string INFINITY_TEXT = "∞";
        private const int STAT_DECIMALS = 6;
        private const string INSPECT_METHOD_TEXT = "AOI";

        /// <summary>RAW DATA 의 샘플 열 1개. cycle(검사 1회차) 1개 = 열 1개. 자재번호는 열 묶음 라벨.</summary>
        private class SampleColumn
        {
            public int MaterialIndex;      // CycleResultDto.IndexNumber (-1 = 미지정)
            public string HeaderLabel;     // "#1", "#2", ...
            public string MaterialLabel;   // "자재 3" 또는 "미지정"
            public CycleResultDto Cycle;
        }

        /// <summary>RAW DATA 의 데이터 행 1개 = 측정 항목 1개. Values/HasValues 는 SampleColumn 리스트와 인덱스 정렬.</summary>
        private class RawRow
        {
            public string Key;
            public string FAIName;
            public string MeasurementName;
            public string TypeName;
            public double NominalValue;
            public double TolerancePlus;
            public double ToleranceMinus;
            public List<double> Values = new List<double>();
            public List<bool> HasValues = new List<bool>();
        }

        /// <summary>
        /// CPK 데이터 리포트를 xlsx 로 저장한다. 시트 2장 고정.
        /// 실패 시 false + 에러 로그 (throw 금지).
        /// </summary>
        public static bool ExportCpkReport(List<CycleResultDto> cycles, string recipeName, string outputPath)
        {
            if (cycles == null || cycles.Count == 0 || string.IsNullOrEmpty(outputPath))
            {
                return false;
            }

            try
            {
                var columns = BuildSampleColumns(cycles);
                var rows = BuildRawRows(columns);

                using (var wb = new XLWorkbook())
                {
                    var wsRaw = wb.Worksheets.Add(RAW_SHEET_NAME);
                    WriteRawDataSheet(wsRaw, columns, rows, recipeName);

                    var stats = new RepeatMeasurementStats();
                    foreach (var c in cycles)
                    {
                        stats.AddSample(c);
                    }

                    Dictionary<string, MeasurementStat> statDict = stats.ComputeAll();

                    var wsCpk = wb.Worksheets.Add(CPK_SHEET_NAME);
                    WriteCpkSheet(wsCpk, rows, statDict);

                    wb.SaveAs(outputPath);
                }

                return true;
            }
            catch (Exception ex)
            {
                try
                {
                    Logging.PrintErrLog((int)ELogType.Error, "[CpkReportExportService] ExportCpkReport failed: " + ex.Message);
                }
                catch { }

                return false;
            }
        }

        /// <summary>RAW DATA(1) 가로형 매트릭스 시트를 기록한다. 1행 = 측정 항목, 열 = 검사 회차(샘플).</summary>
        private static void WriteRawDataSheet(IXLWorksheet ws, List<SampleColumn> columns, List<RawRow> rows, string recipeName)
        {
            ws.Cell(1, 1).Value = "모델명";
            if (recipeName != null)
            {
                ws.Cell(1, 2).Value = recipeName;
            }
            else
            {
                ws.Cell(1, 2).Value = "";
            }

            ws.Cell(2, 1).Value = "측정일시";
            ws.Cell(2, 2).Value = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            ws.Cell(3, 1).Value = "샘플 수";
            ws.Cell(3, 2).Value = columns.Count;

            string[] hFixed = { "Number", "도면항목설명", "측정방식", "설계값", "상한 공차", "하한 공차" };
            for (int i = 0; i < hFixed.Length; i++)
            {
                ws.Cell(RAW_HEADER_ROW, i + 1).Value = hFixed[i];
            }

            for (int i = 0; i < columns.Count; i++)
            {
                int nCol = RAW_FIRST_SAMPLE_COLUMN + i;
                ws.Cell(RAW_MATERIAL_ROW, nCol).Value = columns[i].MaterialLabel;
                ws.Cell(RAW_HEADER_ROW, nCol).Value = columns[i].HeaderLabel;
            }

            int nRow = RAW_FIRST_DATA_ROW;
            foreach (var row in rows)
            {
                ws.Cell(nRow, 1).Value = row.FAIName;
                ws.Cell(nRow, 2).Value = row.MeasurementName;
                ws.Cell(nRow, 3).Value = row.TypeName;
                ws.Cell(nRow, 4).Value = row.NominalValue;
                ws.Cell(nRow, 5).Value = row.TolerancePlus;
                ws.Cell(nRow, 6).Value = row.ToleranceMinus;

                for (int i = 0; i < columns.Count; i++)
                {
                    int nCol = RAW_FIRST_SAMPLE_COLUMN + i;

                    bool bHas = false;
                    if (i < row.HasValues.Count)
                    {
                        bHas = row.HasValues[i];
                    }

                    if (bHas)
                    {
                        ws.Cell(nRow, nCol).Value = Math.Round(row.Values[i], 6);
                    }
                    else
                    {
                        ws.Cell(nRow, nCol).Value = NO_VALUE_TEXT;
                    }
                }

                nRow++;
            }

            ws.Row(RAW_HEADER_ROW).Style.Font.Bold = true;
            ws.SheetView.FreezeRows(RAW_HEADER_ROW);
            ws.SheetView.FreezeColumns(RAW_FIRST_SAMPLE_COLUMN - 1);
            ws.Columns().AdjustToContents();
        }

        /// <summary>1Cav 세부치수_Cpk 통계 시트를 기록한다. 행 순서는 RAW DATA 시트와 동일하다.</summary>
        private static void WriteCpkSheet(IXLWorksheet ws, List<RawRow> rows, Dictionary<string, MeasurementStat> statDict)
        {
            string[] hLeft = { "SPC", "FAI#", "측정 방식", "Datum 유형", "공차 유형",
                               "기준 치수", "+ 공차", "- 공차", "검사 방법", "USL", "LSL" };
            for (int i = 0; i < hLeft.Length; i++)
            {
                ws.Cell(CPK_HEADER_ROW, CPK_LEFT_FIRST_COLUMN + i).Value = hLeft[i];
            }

            string[] hRight = { "Maximum", "Minimum", "Mean", "#1 Target Std Dev", "Std Dev",
                                "Cp", "UCPK", "LCPK", "Cpk" };
            for (int i = 0; i < hRight.Length; i++)
            {
                ws.Cell(CPK_HEADER_ROW, CPK_RIGHT_FIRST_COLUMN + i).Value = hRight[i];
            }

            ws.Cell(CPK_HEADER_ROW, CPK_JUDGE_COLUMN).Value = "Judgment";

            int nRow = CPK_FIRST_DATA_ROW;
            int nSpc = 1;
            int nOk = 0;
            int nNg = 0;
            var ngNames = new List<string>();

            foreach (var row in rows)
            {
                double dUsl = row.NominalValue + row.TolerancePlus;
                double dLsl = row.NominalValue - Math.Abs(row.ToleranceMinus);

                MeasurementStat stat;
                if (!statDict.TryGetValue(row.Key, out stat))
                {
                    stat = null;
                }

                ws.Cell(nRow, 2).Value = nSpc;
                ws.Cell(nRow, 3).Value = row.FAIName;
                ws.Cell(nRow, 4).Value = row.TypeName;
                ws.Cell(nRow, 5).Value = NO_VALUE_TEXT;                                   // Datum 유형 (DTO 미보유, 항상 "-")
                ws.Cell(nRow, 6).Value = BuildToleranceTypeText(row.TolerancePlus, row.ToleranceMinus);
                ws.Cell(nRow, 7).Value = row.NominalValue;
                ws.Cell(nRow, 8).Value = row.TolerancePlus;
                ws.Cell(nRow, 9).Value = row.ToleranceMinus;
                ws.Cell(nRow, 10).Value = INSPECT_METHOD_TEXT;
                ws.Cell(nRow, 11).Value = Math.Round(dUsl, STAT_DECIMALS);
                ws.Cell(nRow, 12).Value = Math.Round(dLsl, STAT_DECIMALS);

                // N==0 엔트리(DATUM_FAIL/NO_IMAGE 만 있는 측정키)는 stat 이 null 이 아니지만 값이 전부 0 이다.
                // 그대로 쓰면 통계칸에 0 이 찍히고 판정만 "-" 가 되어 일관성이 깨지므로 N 까지 확인한다.
                bool bHasStat = stat != null && stat.N > 0;
                if (bHasStat)
                {
                    WriteStatCell(ws, nRow, 14, stat.MaxValue);
                    WriteStatCell(ws, nRow, 15, stat.MinValue);
                    WriteStatCell(ws, nRow, 16, stat.Mean);
                    ws.Cell(nRow, 17).Value = NO_VALUE_TEXT;                              // #1 Target Std Dev (양식 고유, 항상 "-")
                    WriteStatCell(ws, nRow, 18, stat.StdDev);
                    WriteStatCell(ws, nRow, 19, stat.Cp);
                    WriteStatCell(ws, nRow, 20, stat.UCpk);
                    WriteStatCell(ws, nRow, 21, stat.LCpk);
                    WriteStatCell(ws, nRow, 22, stat.Cpk);
                }
                else
                {
                    for (int nCol = 14; nCol <= 22; nCol++)
                    {
                        ws.Cell(nRow, nCol).Value = NO_VALUE_TEXT;
                    }
                }

                string szJudge = BuildCpkJudgement(stat, dUsl, dLsl);
                ws.Cell(nRow, CPK_JUDGE_COLUMN).Value = szJudge;

                if (szJudge == JUDGE_OK)
                {
                    nOk++;
                }
                else if (szJudge == JUDGE_NG)
                {
                    nNg++;
                    if (!ngNames.Contains(row.FAIName))
                    {
                        ngNames.Add(row.FAIName);
                    }
                }

                nRow++;
                nSpc++;
            }

            int nTotal = rows.Count;
            ws.Cell(CPK_SUMMARY_OK_ROW, 1).Value = "OK / Total";
            ws.Cell(CPK_SUMMARY_OK_ROW, 2).Value = nOk + " / " + nTotal;
            ws.Cell(CPK_SUMMARY_NG_ROW, 1).Value = "NG / Total";
            ws.Cell(CPK_SUMMARY_NG_ROW, 2).Value = nNg + " / " + nTotal;
            ws.Cell(CPK_SUMMARY_LIST_ROW, 1).Value = "NG FAI# 항목";

            if (ngNames.Count > 0)
            {
                ws.Cell(CPK_SUMMARY_LIST_ROW, 2).Value = string.Join(", ", ngNames);
            }
            else
            {
                ws.Cell(CPK_SUMMARY_LIST_ROW, 2).Value = NO_VALUE_TEXT;
            }

            ws.Row(CPK_HEADER_ROW).Style.Font.Bold = true;
            ws.SheetView.FreezeRows(CPK_HEADER_ROW);
            ws.Columns().AdjustToContents();
        }

        /// <summary>
        /// 통계 셀 1칸을 기록한다. StdDev==0 이면 Cp/UCPK/LCPK/Cpk 가 PositiveInfinity 이므로
        /// raw double 을 넣으면 엑셀이 깨진다 — 텍스트로 치환한다(StatisticsWindow.CpkToText 미러).
        /// </summary>
        private static void WriteStatCell(IXLWorksheet ws, int nRow, int nColumn, double dValue)
        {
            if (double.IsPositiveInfinity(dValue))
            {
                ws.Cell(nRow, nColumn).Value = INFINITY_TEXT;
                return;
            }

            if (double.IsNegativeInfinity(dValue) || double.IsNaN(dValue))
            {
                ws.Cell(nRow, nColumn).Value = NO_VALUE_TEXT;
                return;
            }

            ws.Cell(nRow, nColumn).Value = Math.Round(dValue, STAT_DECIMALS);
        }

        /// <summary>
        /// 3단계 판정. 참고파일의 다른 시트에는 참/거짓 분기가 동일한 수식 오류
        /// (IF(V&lt;1.33,"O K","O K"))가 있으나 이를 재현하지 않고 모든 항목에 동일 규칙을 적용한다.
        /// 표본이 없으면(N==0) 판정 불가이므로 "-".
        /// min &lt; LSL 또는 max &gt; USL → "N G" / Cpk &lt; 1.33 → "Cpk" / 그 외 → "O K".
        /// </summary>
        private static string BuildCpkJudgement(MeasurementStat stat, double dUsl, double dLsl)
        {
            if (stat == null || stat.N <= 0)
            {
                return NO_VALUE_TEXT;
            }

            if (stat.MinValue < dLsl || stat.MaxValue > dUsl)
            {
                return JUDGE_NG;
            }

            if (stat.Cpk < CPK_WARN_THRESHOLD)
            {
                return JUDGE_WARN;
            }

            return JUDGE_OK;
        }

        /// <summary>공차 유형 라벨. 상/하한 공차 존재 여부로 결정한다.</summary>
        private static string BuildToleranceTypeText(double dTolPlus, double dTolMinus)
        {
            bool bHasUpper = dTolPlus != 0.0;
            bool bHasLower = Math.Abs(dTolMinus) != 0.0;

            if (bHasUpper && bHasLower)
            {
                return "양측";
            }

            if (bHasUpper)
            {
                return "상한";
            }

            if (bHasLower)
            {
                return "하한";
            }

            return "없음";
        }

        /// <summary>
        /// cycle 목록을 자재번호 오름차순(같은 자재 내에서는 입력 순서)으로 정렬해 샘플 열을 만든다.
        /// 열 1개 = cycle 1개(검사 1회차)다 — D-03 의 100회+ 반복을 표현하려면 회차가 열 축이어야 한다.
        /// 자재번호는 열을 묶는 라벨(4행)이며, 정렬 덕분에 같은 자재 회차들이 인접 구간으로 모인다.
        /// </summary>
        private static List<SampleColumn> BuildSampleColumns(List<CycleResultDto> cycles)
        {
            var indexed = new List<KeyValuePair<int, CycleResultDto>>();
            for (int i = 0; i < cycles.Count; i++)
            {
                indexed.Add(new KeyValuePair<int, CycleResultDto>(i, cycles[i]));
            }

            var ordered = indexed
                .OrderBy(p => p.Value.IndexNumber)
                .ThenBy(p => p.Key)
                .ToList();

            var columns = new List<SampleColumn>();
            for (int i = 0; i < ordered.Count; i++)
            {
                var cycle = ordered[i].Value;

                string szMaterial;
                if (cycle.IndexNumber == MATERIAL_NOT_SET)
                {
                    szMaterial = MATERIAL_UNSET_LABEL;
                }
                else
                {
                    szMaterial = "자재 " + cycle.IndexNumber;
                }

                var col = new SampleColumn();
                col.MaterialIndex = cycle.IndexNumber;
                col.HeaderLabel = "#" + (i + 1);
                col.MaterialLabel = szMaterial;
                col.Cycle = cycle;
                columns.Add(col);
            }

            return columns;
        }

        /// <summary>
        /// 샘플 열 순서대로 순회하며 측정항목별 가로형 행을 만든다.
        /// 주의: RepeatMeasurementStats.GetSeries() 는 DATUM_FAIL/NO_IMAGE 회차를 누락시키므로
        /// 열 정렬에 쓸 수 없다 — 그래서 여기서 cycle 인덱스 기반으로 직접 pivot 한다.
        /// </summary>
        private static List<RawRow> BuildRawRows(List<SampleColumn> columns)
        {
            var map = new Dictionary<string, RawRow>();
            var order = new List<string>();

            for (int nCol = 0; nCol < columns.Count; nCol++)
            {
                var cycle = columns[nCol].Cycle;
                if (cycle == null || cycle.Shots == null)
                {
                    continue;
                }

                foreach (var shot in cycle.Shots)
                {
                    if (shot == null || shot.FAIs == null)
                    {
                        continue;
                    }

                    foreach (var fai in shot.FAIs)
                    {
                        if (fai == null || fai.Measurements == null)
                        {
                            continue;
                        }

                        foreach (var m in fai.Measurements)
                        {
                            if (m == null)
                            {
                                continue;
                            }

                            string key = (shot.ShotName ?? "") + "/" + (fai.FAIName ?? "") + "/" + (m.MeasurementName ?? "");

                            RawRow row;
                            if (!map.TryGetValue(key, out row))
                            {
                                row = new RawRow();
                                row.Key = key;
                                row.FAIName = fai.FAIName ?? "";
                                row.MeasurementName = m.MeasurementName ?? "";
                                row.TypeName = m.TypeName ?? "";
                                map[key] = row;
                                order.Add(key);
                            }

                            // 최신 레시피 공차로 갱신 (RepeatMeasurementStats 와 동일 정책)
                            row.NominalValue = m.NominalValue;
                            row.TolerancePlus = m.TolerancePlus;
                            row.ToleranceMinus = m.ToleranceMinus;

                            PadRowTo(row, nCol);

                            // 0.0 도 정상 결과이므로 값이 아니라 LastHasResult 로 판별한다 (CO-23-01).
                            if (m.LastHasResult)
                            {
                                row.Values.Add(m.LastMeasuredValue);
                                row.HasValues.Add(true);
                            }
                            else
                            {
                                row.Values.Add(0.0);
                                row.HasValues.Add(false);
                            }
                        }
                    }
                }
            }

            var result = new List<RawRow>();
            foreach (var key in order)
            {
                RawRow row = map[key];
                PadRowTo(row, columns.Count);
                result.Add(row);
            }

            return result;
        }

        /// <summary>행의 값 목록을 nTargetCount 길이까지 "값 없음"으로 채운다. 열 밀림 방지.</summary>
        private static void PadRowTo(RawRow row, int nTargetCount)
        {
            while (row.Values.Count < nTargetCount)
            {
                row.Values.Add(0.0);
                row.HasValues.Add(false);
            }
        }
    }
}
