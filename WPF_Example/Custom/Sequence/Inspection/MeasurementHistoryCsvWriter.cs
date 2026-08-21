//260707 hbk STAT-01: 양산 이력 통계 수집 계층 — CycleResultDto 를 측정항목당 1행 CSV 로 append
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
    /// 검사 완료 결과(CycleResultDto)를 일자별 CSV(StatisticsSavePath\yyyyMMdd.csv)에 누적 append 한다.
    /// 측정 항목당 1행. RFC4180 따옴표 이스케이프 + static lock(D-05) + 신규 파일 헤더 자동 생성.
    /// 실패는 호출부(CycleResultSerializer.SaveAsync)의 독립 try/catch 로 격리되어 검사/TCP 무영향(D-04).
    /// </summary>
    public static class MeasurementHistoryCsvWriter
    {
        private const string CSV_EXT = ".csv";
        //260820 hbk 검사구분(자동/수동) 컬럼은 반드시 맨 뒤에 추가한다 — MeasurementHistoryCsvLoader 가 고정
        //  컬럼 인덱스(COL_TIME=0 ~ COL_OVERALL=13)로 읽고 "fields.Count < COLUMN_COUNT(14)" 로만 가드하므로,
        //  뒤에 붙이면 기존 14컬럼 파일도 그대로 읽히고 앞 컬럼 위치도 안 바뀐다(하위호환).
        private const string CSV_HEADER = "검사일시,RecipeName,IndexNumber,ShotName,FAIName,MeasurementName,TypeName,NominalValue,TolerancePlus,ToleranceMinus,MeasuredValue,Judgement,HasResult,OverallCycleResult,검사구분";

        //260820 hbk 검사구분 표기값 — 사람이 엑셀에서 바로 읽고 필터할 수 있게 한글 고정 문자열.
        private const string RUNMODE_AUTO = "자동";
        private const string RUNMODE_MANUAL = "수동";
        private const string NUM_FORMAT = "F4";

        private static readonly object s_lock = new object();   //260707 hbk STAT-01 D-05: 복수 InspectionSequence append 경합 방지

        /// <summary>
        /// dto 를 Shot→FAI→Measurement 로 평탄화하여 측정 항목당 1행을 StatisticsSavePath\yyyyMMdd.csv 에 append 한다.
        /// </summary>
        public static void Append(CycleResultDto dto)
        {
            try
            {
                if (dto == null) { return; }                              //260707 hbk STAT-01
                if (dto.Shots == null) { return; }                        //260707 hbk STAT-01

                string szDir = SystemHandler.Handle.Setting.StatisticsSavePath;   //260707 hbk STAT-01 D-01
                if (string.IsNullOrEmpty(szDir)) { return; }

                string szPath = Path.Combine(szDir, dto.InspectionTime.ToString("yyyyMMdd") + CSV_EXT);   //260707 hbk STAT-01 D-02: 일자별 1파일
                string szOverall = MapOverall(dto.OverallJudgement);       //260707 hbk STAT-01 D-03

                var sb = new StringBuilder();
                AppendMeasurementLines(sb, dto, szOverall);

                if (sb.Length == 0) { return; }                           //260707 hbk STAT-01 측정 0건이면 파일 생성 불필요

                lock (s_lock)                                             //260707 hbk STAT-01 D-05
                {
                    Directory.CreateDirectory(szDir);                     //260707 hbk 존재해도 무해
                    bool bNewFile = !File.Exists(szPath);                 //260707 hbk 신규 파일 → 헤더 1행
                    if (bNewFile)
                    {
                        File.AppendAllText(szPath, CSV_HEADER + Environment.NewLine, Encoding.UTF8);
                    }
                    File.AppendAllText(szPath, sb.ToString(), Encoding.UTF8);
                }
            }
            catch (Exception ex)   //260707 hbk STAT-01 D-04: 방어적 이중 격리 — 검사/TCP 무영향
            {
                try { Logging.PrintErrLog((int)ELogType.Error, "[MeasurementHistoryCsvWriter] Append failed: " + ex.Message); } catch { }
            }
        }

        /// <summary>
        /// dto 의 Shot→FAI→Measurement 3중 루프를 평탄화하여 sb 에 라인 단위로 누적한다.
        /// </summary>
        private static void AppendMeasurementLines(StringBuilder sb, CycleResultDto dto, string szOverall)
        {
            foreach (var shot in dto.Shots)
            {
                if (shot == null || shot.FAIs == null) { continue; }      //260707 hbk STAT-01 null 가드

                foreach (var fai in shot.FAIs)
                {
                    if (fai == null || fai.Measurements == null) { continue; }

                    foreach (var meas in fai.Measurements)
                    {
                        if (meas == null) { continue; }
                        sb.Append(BuildLine(dto, shot, fai, meas, szOverall));
                        sb.Append(Environment.NewLine);
                    }
                }
            }
        }

        /// <summary>15개 컬럼을 CSV_HEADER 순서대로 콤마 join 하여 1행 문자열을 생성한다.</summary>
        private static string BuildLine(CycleResultDto dto, ShotResultDto shot, FaiResultDto fai, MeasurementResultDto meas, string szOverall)
        {
            var fields = new List<string>
            {
                dto.InspectionTime.ToString("yyyy-MM-dd HH:mm:ss"),                              //260707 hbk STAT-01
                Esc(dto.RecipeName),
                dto.IndexNumber.ToString(CultureInfo.InvariantCulture),
                Esc(shot.ShotName),
                Esc(fai.FAIName),
                Esc(meas.MeasurementName),
                Esc(meas.TypeName),
                meas.NominalValue.ToString(NUM_FORMAT, CultureInfo.InvariantCulture),
                meas.TolerancePlus.ToString(NUM_FORMAT, CultureInfo.InvariantCulture),
                meas.ToleranceMinus.ToString(NUM_FORMAT, CultureInfo.InvariantCulture),
                meas.LastMeasuredValue.ToString(NUM_FORMAT, CultureInfo.InvariantCulture),
                MapJudgement(meas),
                meas.LastHasResult.ToString(),
                Esc(szOverall),
                MapRunMode(dto)   //260820 hbk 검사구분(자동/수동) — 맨 뒤 컬럼(하위호환)
            };

            return string.Join(",", fields);
        }

        //260820 hbk 검사구분 매핑 — PLC $TEST 로 시작한 사이클이면 "자동", 화면 RUN/일괄검사/반복검사면 "수동".
        private static string MapRunMode(CycleResultDto dto)
        {
            if (dto.IsProtocolDriven)
            {
                return RUNMODE_AUTO;
            }
            return RUNMODE_MANUAL;
        }

        /// <summary>RepeatMeasurementStats.AddSample 정책과 일치하는 측정 판정 문자열 매핑.</summary>
        private static string MapJudgement(MeasurementResultDto meas)
        {
            if (meas.LastSkipReason == SkipReason.DATUM_FAIL) { return SkipReason.DATUM_FAIL; }   //260707 hbk STAT-01 //260710 hbk 상수화
            if (meas.LastSkipReason == SkipReason.NO_IMAGE) { return SkipReason.NO_IMAGE; }       //260707 hbk STAT-01 //260710 hbk 상수화
            if (meas.LastHasResult == false) { return "NO_RESULT"; }            //260707 hbk 미측정(값 없음) — 로더가 통계서 제외
            if (meas.LastJudgement) { return "OK"; }
            return "NG";
        }

        /// <summary>dto.OverallJudgement("OK"/"NG"/"DETECT_FAIL") → D-03 의 P|F|N 매핑.</summary>
        private static string MapOverall(string szOverallJudgement)
        {
            if (szOverallJudgement == "OK") { return "P"; }   //260707 hbk STAT-01 D-03
            if (szOverallJudgement == "NG") { return "F"; }   //260707 hbk STAT-01 D-03
            return "N";   // DETECT_FAIL 또는 기타
        }

        /// <summary>RFC4180 따옴표 이스케이프. 콤마/따옴표/개행 포함 시 전체를 큰따옴표로 감싸고 내부 따옴표를 이중화한다.</summary>
        private static string Esc(string szValue)
        {
            if (szValue == null) { szValue = ""; }   //260707 hbk STAT-01 D-03

            bool bNeedQuote = szValue.IndexOf(',') >= 0 || szValue.IndexOf('"') >= 0 || szValue.IndexOf('\r') >= 0 || szValue.IndexOf('\n') >= 0;
            if (bNeedQuote)
            {
                string szEscaped = szValue.Replace("\"", "\"\"");
                return "\"" + szEscaped + "\"";
            }
            return szValue;
        }
    }
}
