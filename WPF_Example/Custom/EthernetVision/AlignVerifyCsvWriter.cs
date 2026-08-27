using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using ReringProject.Setting;
using ReringProject.Utility;

namespace ReringProject {

    /// <summary>
    /// Align 정합 검증 기록을 일자별 CSV(AlignVerifySavePath\yyyyMMdd.csv)에 누적 append 한다.
    /// 이벤트 1건 = 1행. RFC4180 따옴표 이스케이프 + static lock + 신규 파일 헤더 자동 생성.
    ///
    /// 기존 측정이력(MeasurementHistoryCsvWriter)과 완전히 분리된 별도 파일이다.
    /// 통계 화면/CPK 엑셀이 그쪽을 소비하므로 컬럼을 건드리면 안 되기 때문이다.
    /// 쓰기 골격은 그 파일을 그대로 따랐다 — 새 직렬화 방식을 발명하지 않는다.
    ///
    /// 실패는 전부 삼켜 로그만 남긴다. 검사/TCP 에 절대 영향이 없어야 한다.
    /// </summary>
    public static class AlignVerifyCsvWriter {

        private const string CSV_EXT = ".csv";

        /// <summary>20 컬럼 고정. 순서를 바꾸면 AlignVerifyCsvLoader 의 컬럼 인덱스가 깨진다.</summary>
        private const string CSV_HEADER = "기록시각,구분,자재번호,대상,슬롯,시퀀스,Datum,판정,잔여OffsetXmm,잔여OffsetYmm,잔여ThetaDeg,매칭점수,검출Row,검출Col,기준Row,기준Col,해상도mmPerPx,검출시각,실패사유,이미지파일";

        private const string NUM_FORMAT = "F4";
        private const string RES_FORMAT = "F6";
        private const string TIME_FORMAT = "yyyy-MM-dd HH:mm:ss";
        private const string FILE_DATE_FORMAT = "yyyyMMdd";

        private static readonly object s_lock = new object();

        /// <summary>rec 1건을 {AlignVerifySavePath}\yyyyMMdd.csv 에 1행 append 한다.</summary>
        public static void Append(AlignVerifyRecord rec) {
            try {
                if (rec == null) { return; }

                string szDir = SystemHandler.Handle.Setting.AlignVerifySavePath;
                if (string.IsNullOrEmpty(szDir)) { return; }

                string szPath = Path.Combine(szDir, rec.RecordTime.ToString(FILE_DATE_FORMAT) + CSV_EXT);

                var sb = new StringBuilder();
                sb.Append(BuildLine(rec));
                sb.Append(Environment.NewLine);

                lock (s_lock) {
                    Directory.CreateDirectory(szDir);
                    bool bNewFile = !File.Exists(szPath);
                    if (bNewFile) {
                        File.AppendAllText(szPath, CSV_HEADER + Environment.NewLine, Encoding.UTF8);
                    }
                    File.AppendAllText(szPath, sb.ToString(), Encoding.UTF8);
                }
            }
            catch (Exception ex) {
                try { Logging.PrintErrLog((int)ELogType.Error, "[AlignVerifyCsvWriter] Append failed: " + ex.Message); } catch { }
            }
        }

        /// <summary>20개 컬럼을 CSV_HEADER 순서대로 콤마 join 하여 1행 문자열을 생성한다.</summary>
        private static string BuildLine(AlignVerifyRecord rec) {
            // ① 전용 4값 — HasResidual 이 false 면 빈칸. double 0.0 을 값으로 오해하지 않게 한다.
            string szResidualX = "";
            string szResidualY = "";
            string szResidualTheta = "";
            string szScore = "";
            if (rec.HasResidual) {
                szResidualX = rec.ResidualOffsetXmm.ToString(NUM_FORMAT, CultureInfo.InvariantCulture);
                szResidualY = rec.ResidualOffsetYmm.ToString(NUM_FORMAT, CultureInfo.InvariantCulture);
                szResidualTheta = rec.ResidualThetaDeg.ToString(NUM_FORMAT, CultureInfo.InvariantCulture);
                szScore = rec.Score.ToString(NUM_FORMAT, CultureInfo.InvariantCulture);
            }

            // ② 전용 5값 — HasSeatOrigin 이 false 면 빈칸.
            string szDetectedRow = "";
            string szDetectedCol = "";
            string szRefRow = "";
            string szRefCol = "";
            string szResolution = "";
            if (rec.HasSeatOrigin) {
                szDetectedRow = rec.DetectedRow.ToString(NUM_FORMAT, CultureInfo.InvariantCulture);
                szDetectedCol = rec.DetectedCol.ToString(NUM_FORMAT, CultureInfo.InvariantCulture);
                szRefRow = rec.RefRow.ToString(NUM_FORMAT, CultureInfo.InvariantCulture);
                szRefCol = rec.RefCol.ToString(NUM_FORMAT, CultureInfo.InvariantCulture);
                szResolution = rec.PixelResolutionMmPerPx.ToString(RES_FORMAT, CultureInfo.InvariantCulture);
            }

            string szDetectTime = "";
            bool bHasDetectTime = (rec.DetectTime != default(DateTime));
            if (bHasDetectTime) {
                szDetectTime = rec.DetectTime.ToString(TIME_FORMAT);
            }

            var fields = new List<string>
            {
                rec.RecordTime.ToString(TIME_FORMAT),
                Esc(rec.Kind),
                rec.MaterialNo.ToString(CultureInfo.InvariantCulture),
                Esc(rec.Target),
                Esc(rec.SlotToken),
                Esc(rec.SequenceName),
                Esc(rec.DatumName),
                Esc(rec.Judgement),
                szResidualX,
                szResidualY,
                szResidualTheta,
                szScore,
                szDetectedRow,
                szDetectedCol,
                szRefRow,
                szRefCol,
                szResolution,
                szDetectTime,
                Esc(rec.FailReason),
                Esc(rec.ImageFileName)
            };

            return string.Join(",", fields);
        }

        /// <summary>RFC4180 따옴표 이스케이프. 콤마/따옴표/개행 포함 시 전체를 큰따옴표로 감싸고 내부 따옴표를 이중화한다.</summary>
        private static string Esc(string szValue) {
            if (szValue == null) { szValue = ""; }

            bool bNeedQuote = szValue.IndexOf(',') >= 0 || szValue.IndexOf('"') >= 0 || szValue.IndexOf('\r') >= 0 || szValue.IndexOf('\n') >= 0;
            if (bNeedQuote) {
                string szEscaped = szValue.Replace("\"", "\"\"");
                return "\"" + szEscaped + "\"";
            }
            return szValue;
        }
    }
}
