//260707 hbk STAT-01: 양산 이력 통계 조회/집계 계층 — CSV 를 읽어 RepeatMeasurementStats 재사용 집계 + 추이 시계열 산출
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using ReringProject.UI;
using ReringProject.Utility;
using ReringProject.Setting;

namespace ReringProject.Sequence
{
    /// <summary>
    /// MeasurementHistoryCsvLoader.Query() 의 반환 컨테이너.
    /// Stats = 측정키(Shot/FAI/측정명)별 통계, Series = 추이용 순서유지 원시값, RecipeNames = 필터무관 distinct 목록.
    /// </summary>
    public class StatisticsQueryResult
    {
        public Dictionary<string, MeasurementStat> Stats = new Dictionary<string, MeasurementStat>();   //260707 hbk 키=Shot/FAI/측정명
        public Dictionary<string, List<double>> Series = new Dictionary<string, List<double>>();         //260707 hbk D-13 순서유지 원시값
        public List<string> RecipeNames = new List<string>();   //260707 hbk D-11 필터무관 distinct 레시피
        public int TotalRowCount;                               //260707 hbk 로드된 데이터 행 수(헤더 제외)
    }

    /// <summary>
    /// StatisticsSavePath\yyyyMMdd.csv 를 기간·레시피로 조회하여 통계/추이/레시피목록을 산출한다.
    /// 통계 계산은 RepeatMeasurementStats.AddSample/ComputeAll 을 그대로 재사용한다(DRY, 수정 없음).
    /// </summary>
    public static class MeasurementHistoryCsvLoader
    {
        private const string CSV_EXT = ".csv";
        private const string HEADER_FIRST_TOKEN = "검사일시";
        private const int COLUMN_COUNT = 14;
        private const int COL_TIME = 0;
        private const int COL_RECIPE = 1;
        private const int COL_SHOT = 3;
        private const int COL_FAI = 4;
        private const int COL_MEASNAME = 5;
        private const int COL_TYPE = 6;
        private const int COL_NOMINAL = 7;
        private const int COL_TOLPLUS = 8;
        private const int COL_TOLMINUS = 9;
        private const int COL_MEASURED = 10;
        private const int COL_JUDGE = 11;

        /// <summary>
        /// dtFrom~dtTo 기간의 일자별 CSV 를 읽어 통계/추이/레시피목록을 반환한다.
        /// szRecipeFilter 가 null/빈문자열이면 전체 레시피를 집계한다.
        /// </summary>
        public static StatisticsQueryResult Query(DateTime dtFrom, DateTime dtTo, string szRecipeFilter)
        {
            var result = new StatisticsQueryResult();

            try
            {
                var stats = new RepeatMeasurementStats();
                var recipeSet = new HashSet<string>();

                string szDir = SystemHandler.Handle.Setting.StatisticsSavePath;   //260707 hbk STAT-01 D-01
                if (string.IsNullOrEmpty(szDir))
                {
                    return result;
                }

                if (dtTo.Date < dtFrom.Date)   //260707 hbk from>to 방어
                {
                    return result;
                }

                for (DateTime d = dtFrom.Date; d <= dtTo.Date; d = d.AddDays(1))
                {
                    string szPath = Path.Combine(szDir, d.ToString("yyyyMMdd") + CSV_EXT);
                    if (!File.Exists(szPath))
                    {
                        continue;
                    }

                    LoadFile(szPath, szRecipeFilter, stats, result, recipeSet);
                }

                result.RecipeNames = new List<string>(recipeSet);
                result.RecipeNames.Sort();
                result.Stats = stats.ComputeAll();
            }
            catch (Exception ex)   //260707 hbk 방어적 격리 — 조회 실패해도 UI 크래시 없이 빈 결과 반환
            {
                try { Logging.PrintErrLog((int)ELogType.Error, "[MeasurementHistoryCsvLoader] Query failed: " + ex.Message); } catch { }
            }

            return result;
        }

        /// <summary>szPath 1개 CSV 파일을 읽어 라인 단위로 ProcessRow 에 위임한다. 파일 단위 실패는 격리하여 다음 파일 로드를 막지 않는다.</summary>
        private static void LoadFile(string szPath, string szRecipeFilter, RepeatMeasurementStats stats, StatisticsQueryResult result, HashSet<string> recipeSet)
        {
            try
            {
                string[] lines = File.ReadAllLines(szPath, Encoding.UTF8);

                foreach (string line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line))
                    {
                        continue;
                    }

                    List<string> fields = ParseCsvLine(line);
                    if (fields.Count < COLUMN_COUNT)   //260707 hbk 손상/불완전 행 가드(T-67-04)
                    {
                        continue;
                    }

                    if (fields[COL_TIME] == HEADER_FIRST_TOKEN)   //260707 hbk 헤더 라인 skip
                    {
                        continue;
                    }

                    ProcessRow(fields, szRecipeFilter, stats, result, recipeSet);
                }
            }
            catch (Exception ex)   //260707 hbk 파일 단위 격리 — 손상 파일 1개가 전체 Query 를 중단시키지 않음
            {
                try { Logging.PrintErrLog((int)ELogType.Error, "[MeasurementHistoryCsvLoader] LoadFile failed: " + szPath + " / " + ex.Message); } catch { }
            }
        }

        /// <summary>CSV 1행을 처리한다. distinct 레시피 수집(필터 전) → 필터 적용 → 통계 누적 → 추이 시계열 수집.</summary>
        private static void ProcessRow(List<string> fields, string szRecipeFilter, RepeatMeasurementStats stats, StatisticsQueryResult result, HashSet<string> recipeSet)
        {
            string szRecipe = fields[COL_RECIPE];
            recipeSet.Add(szRecipe);   //260707 hbk D-11 필터 전에 distinct 수집(드롭다운용)

            if (!string.IsNullOrEmpty(szRecipeFilter) && szRecipe != szRecipeFilter)
            {
                return;
            }

            MeasurementResultDto meas = BuildMeasFromRow(fields);
            string szShot = fields[COL_SHOT];
            string szFai = fields[COL_FAI];
            string szName = fields[COL_MEASNAME];

            // 통계 누적: 최소 CycleResultDto 로 감싸 기존 RepeatMeasurementStats 재사용(D-07, DRY)
            var dto = new CycleResultDto();
            var shot = new ShotResultDto { ShotName = szShot };
            var fai = new FaiResultDto { FAIName = szFai };
            fai.Measurements.Add(meas);
            shot.FAIs.Add(fai);
            dto.Shots.Add(shot);
            stats.AddSample(dto);

            // 추이 시계열(D-13): OK/NG(측정값 있는 것)만 순서대로 수집
            if (meas.LastHasResult && string.IsNullOrEmpty(meas.LastSkipReason))
            {
                string szKey = szShot + "/" + szFai + "/" + szName;   //260707 hbk RepeatMeasurementStats 키 포맷 일치
                List<double> series;
                if (!result.Series.TryGetValue(szKey, out series))
                {
                    series = new List<double>();
                    result.Series[szKey] = series;
                }

                series.Add(meas.LastMeasuredValue);
            }

            result.TotalRowCount++;
        }

        /// <summary>CSV 필드를 MeasurementResultDto 로 역구성한다. Judgement 컬럼 5분기(D-06/D-07 정책 재현).</summary>
        private static MeasurementResultDto BuildMeasFromRow(List<string> fields)
        {
            var meas = new MeasurementResultDto();
            meas.MeasurementName = fields[COL_MEASNAME];
            meas.TypeName = fields[COL_TYPE];
            meas.NominalValue = ParseDouble(fields[COL_NOMINAL]);
            meas.TolerancePlus = ParseDouble(fields[COL_TOLPLUS]);
            meas.ToleranceMinus = ParseDouble(fields[COL_TOLMINUS]);

            string szJudge = fields[COL_JUDGE];
            if (szJudge == "DATUM_FAIL")
            {
                meas.LastSkipReason = "DATUM_FAIL";
                meas.LastHasResult = false;
            }
            else if (szJudge == "NO_IMAGE")
            {
                meas.LastSkipReason = "NO_IMAGE";
                meas.LastHasResult = false;
            }
            else if (szJudge == "NO_RESULT")
            {
                meas.LastSkipReason = null;
                meas.LastHasResult = false;
            }
            else if (szJudge == "OK")
            {
                meas.LastSkipReason = null;
                meas.LastHasResult = true;
                meas.LastJudgement = true;
                meas.LastMeasuredValue = ParseDouble(fields[COL_MEASURED]);
            }
            else   // NG
            {
                meas.LastSkipReason = null;
                meas.LastHasResult = true;
                meas.LastJudgement = false;
                meas.LastMeasuredValue = ParseDouble(fields[COL_MEASURED]);
            }

            return meas;
        }

        /// <summary>InvariantCulture 숫자 파싱. 실패 시 0.0 폴백(T-67-04).</summary>
        private static double ParseDouble(string sz)
        {
            double d;
            if (double.TryParse(sz, NumberStyles.Any, CultureInfo.InvariantCulture, out d))
            {
                return d;
            }

            return 0.0;
        }

        /// <summary>RFC4180 CSV 한 줄 파서. 따옴표로 감싸진 필드 내부의 콤마/개행을 무시하고, `""` 를 `"` 로 역이스케이프한다.</summary>
        private static List<string> ParseCsvLine(string szLine)
        {
            var fields = new List<string>();
            var sb = new StringBuilder();
            bool bInQuotes = false;
            int i = 0;

            while (i < szLine.Length)
            {
                char c = szLine[i];

                if (bInQuotes)
                {
                    if (c == '"')
                    {
                        if (i + 1 < szLine.Length && szLine[i + 1] == '"')
                        {
                            sb.Append('"');
                            i += 2;
                            continue;
                        }

                        bInQuotes = false;
                        i++;
                        continue;
                    }

                    sb.Append(c);
                    i++;
                    continue;
                }

                if (c == '"')
                {
                    bInQuotes = true;
                    i++;
                    continue;
                }

                if (c == ',')
                {
                    fields.Add(sb.ToString());
                    sb.Clear();
                    i++;
                    continue;
                }

                sb.Append(c);
                i++;
            }

            fields.Add(sb.ToString());
            return fields;
        }
    }
}
