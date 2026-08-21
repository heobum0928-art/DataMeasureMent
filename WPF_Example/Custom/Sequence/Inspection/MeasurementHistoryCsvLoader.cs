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
        private const int COL_INDEX = 2;
        private const int COL_OVERALL = 13;
        //260820 hbk 검사구분(자동/수동) 컬럼. 이 컬럼 도입(260820) 이전에 쓰인 CSV 는 14컬럼이라 이 인덱스가
        //  아예 없다 — 반드시 fields.Count 확인 후 읽고, 없으면 기존 동작 그대로(수동=false) 취급한다.
        //  COLUMN_COUNT 는 14 로 유지한다: 15 로 올리면 기존 14컬럼 파일이 전부 "손상 행"으로 걸러진다.
        private const int COL_RUNMODE = 14;
        private const string RUNMODE_AUTO_TEXT = "자동";

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
            if (szJudge == SkipReason.DATUM_FAIL) //260710 hbk 상수화
            {
                meas.LastSkipReason = SkipReason.DATUM_FAIL; //260710 hbk 상수화
                meas.LastHasResult = false;
            }
            else if (szJudge == SkipReason.NO_IMAGE) //260710 hbk 상수화
            {
                meas.LastSkipReason = SkipReason.NO_IMAGE; //260710 hbk 상수화
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

        /// <summary>
        /// QueryCycles 의 사이클 경계 판정 상태. CSV 는 사이클 단위 append 라 행이 항상 연속이므로
        /// 전체를 메모리에 모으지 않고 직전 행과의 비교만으로 경계를 찾는다.
        /// </summary>
        private class CycleGroupState
        {
            public List<CycleResultDto> Cycles = new List<CycleResultDto>();
            public CycleResultDto Current;
            public string LastTime;
            public string LastRecipe;
            public string LastIndex;
            public HashSet<string> SeenKeys = new HashSet<string>();
            public Dictionary<string, ShotResultDto> ShotMap = new Dictionary<string, ShotResultDto>();
            public Dictionary<string, FaiResultDto> FaiMap = new Dictionary<string, FaiResultDto>();
        }

        /// <summary>
        /// dtFrom~dtTo 기간의 일자별 CSV 를 읽어 검사 사이클 단위 DTO 목록으로 재조립한다.
        /// CPK 리포트 export 전용 — 화면 통계는 Query() 를 쓴다(무변경).
        /// 반환 순서는 시간 오름차순(오래된 것 → 최신)이며, CSV 의 append 순서를 그대로 따른다.
        /// </summary>
        public static List<CycleResultDto> QueryCycles(DateTime dtFrom, DateTime dtTo, string szRecipeFilter)
        {
            var state = new CycleGroupState();

            try
            {
                string szDir = SystemHandler.Handle.Setting.StatisticsSavePath;
                if (string.IsNullOrEmpty(szDir))
                {
                    return state.Cycles;
                }

                if (dtTo.Date < dtFrom.Date)
                {
                    return state.Cycles;
                }

                for (DateTime d = dtFrom.Date; d <= dtTo.Date; d = d.AddDays(1))
                {
                    string szPath = Path.Combine(szDir, d.ToString("yyyyMMdd") + CSV_EXT);
                    if (!File.Exists(szPath))
                    {
                        continue;
                    }

                    LoadCyclesFromFile(szPath, szRecipeFilter, state);
                }
            }
            catch (Exception ex)
            {
                try { Logging.PrintErrLog((int)ELogType.Error, "[MeasurementHistoryCsvLoader] QueryCycles failed: " + ex.Message); } catch { }
            }

            return state.Cycles;
        }

        /// <summary>szPath 1개 CSV 파일을 사이클 재조립 상태에 누적한다. 파일 단위 실패는 격리한다(LoadFile 동일 패턴).</summary>
        private static void LoadCyclesFromFile(string szPath, string szRecipeFilter, CycleGroupState state)
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
                    if (fields.Count < COLUMN_COUNT)
                    {
                        continue;
                    }

                    if (fields[COL_TIME] == HEADER_FIRST_TOKEN)
                    {
                        continue;
                    }

                    ProcessCycleRow(fields, szRecipeFilter, state);
                }
            }
            catch (Exception ex)
            {
                try { Logging.PrintErrLog((int)ELogType.Error, "[MeasurementHistoryCsvLoader] LoadCyclesFromFile failed: " + szPath + " / " + ex.Message); } catch { }
            }
        }

        /// <summary>
        /// 새 사이클이 시작되는 행인지 판정한다. CSV 타임스탬프가 초 단위라 서로 다른 사이클이
        /// 같은 초에 겹치는 일이 실제로 발생하므로(실데이터 14건) 시간만으로는 나눌 수 없다.
        /// 측정키 재등장을 마지막 방어선으로 둔다 — 한 사이클 안에서 같은 Shot/FAI/측정명은 한 번뿐이다.
        /// </summary>
        private static bool IsNewCycleBoundary(CycleGroupState state, string szTime, string szRecipe, string szIndex, string szKey)
        {
            if (state.Current == null)
            {
                return true;
            }

            if (szTime != state.LastTime || szRecipe != state.LastRecipe)
            {
                return true;
            }

            if (szIndex != state.LastIndex)
            {
                return true;
            }

            if (state.SeenKeys.Contains(szKey))
            {
                return true;
            }

            return false;
        }

        /// <summary>CSV 1행을 사이클 재조립 상태에 반영한다. 측정 DTO 복원은 BuildMeasFromRow 를 그대로 재사용한다(DRY).</summary>
        private static void ProcessCycleRow(List<string> fields, string szRecipeFilter, CycleGroupState state)
        {
            string szRecipe = fields[COL_RECIPE];
            if (!string.IsNullOrEmpty(szRecipeFilter) && szRecipe != szRecipeFilter)
            {
                return;
            }

            string szTime = fields[COL_TIME];
            string szIndex = fields[COL_INDEX];
            string szShot = fields[COL_SHOT];
            string szFai = fields[COL_FAI];
            string szName = fields[COL_MEASNAME];
            string szKey = szShot + "/" + szFai + "/" + szName;

            if (IsNewCycleBoundary(state, szTime, szRecipe, szIndex, szKey))
            {
                var cycle = new CycleResultDto();
                cycle.InspectionTime = ParseInspectionTime(szTime);
                cycle.RecipeName = szRecipe;
                cycle.IndexNumber = ParseIndexNumber(szIndex);
                cycle.OverallJudgement = MapOverallBack(fields[COL_OVERALL]);
                cycle.IsProtocolDriven = ParseRunMode(fields);   //260820 hbk 자동/수동 복원(구 14컬럼 파일이면 false)

                state.Cycles.Add(cycle);
                state.Current = cycle;
                state.SeenKeys.Clear();
                state.ShotMap.Clear();
                state.FaiMap.Clear();
            }

            ShotResultDto shot;
            if (!state.ShotMap.TryGetValue(szShot, out shot))
            {
                shot = new ShotResultDto { ShotName = szShot };
                state.Current.Shots.Add(shot);
                state.ShotMap[szShot] = shot;
            }

            // FAI 맵 키에 ShotName 을 포함해야 다른 Shot 의 동명 FAI 가 섞이지 않는다.
            string szFaiKey = szShot + "/" + szFai;
            FaiResultDto fai;
            if (!state.FaiMap.TryGetValue(szFaiKey, out fai))
            {
                fai = new FaiResultDto { FAIName = szFai };
                shot.FAIs.Add(fai);
                state.FaiMap[szFaiKey] = fai;
            }

            fai.Measurements.Add(BuildMeasFromRow(fields));

            state.SeenKeys.Add(szKey);
            state.LastTime = szTime;
            state.LastRecipe = szRecipe;
            state.LastIndex = szIndex;
        }

        /// <summary>"yyyy-MM-dd HH:mm:ss" 파싱. 실패 시 DateTime.MinValue — 그룹핑은 원본 문자열로 하므로 영향 없다.</summary>
        private static DateTime ParseInspectionTime(string sz)
        {
            DateTime dt;
            if (DateTime.TryParseExact(sz, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out dt))
            {
                return dt;
            }

            return DateTime.MinValue;
        }

        //260820 hbk 검사구분 복원. 컬럼 자체가 없는 구 CSV(14컬럼)는 false(수동) — 그 시절엔 자동/수동을
        //  기록하지 않았으므로 "자동이었다"고 단정할 근거가 없다. 보수적으로 수동 취급한다.
        private static bool ParseRunMode(List<string> fields)
        {
            bool bHasColumn = fields.Count > COL_RUNMODE;
            if (!bHasColumn)
            {
                return false;
            }
            return fields[COL_RUNMODE] == RUNMODE_AUTO_TEXT;
        }

        /// <summary>자재번호 파싱. 공백/실패는 -1(CycleResultDto.IndexNumber 의 미지정 sentinel).</summary>
        private static int ParseIndexNumber(string sz)
        {
            int n;
            if (int.TryParse(sz, NumberStyles.Integer, CultureInfo.InvariantCulture, out n))
            {
                return n;
            }

            return -1;
        }

        /// <summary>CSV 의 P/F/N 을 CycleResultDto.OverallJudgement 값으로 되돌린다(Writer.MapOverall 의 역함수).</summary>
        private static string MapOverallBack(string sz)
        {
            if (sz == "P")
            {
                return "OK";
            }

            if (sz == "F")
            {
                return "NG";
            }

            return "DETECT_FAIL";
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
