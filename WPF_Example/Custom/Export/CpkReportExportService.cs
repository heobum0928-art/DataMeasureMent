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
