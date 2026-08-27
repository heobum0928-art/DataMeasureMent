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

        // 본 파일이 잠겨 있을 때 기록을 받아두는 옆 파일. 잠김이 풀리면 본 파일로 합쳐지고 지워진다.
        //  엑셀로 CSV 를 열어두면 그 사이 기록이 통째로 유실됐다(실측 260827: 17:09~17:48 전건 손실).
        //  75 는 분쟁 대비 증거라 "그때 파일이 열려 있었습니다" 는 성립하지 않는다.
        private const string PENDING_SUFFIX = "_pending";

        private static readonly object s_lock = new object();

        /// <summary>rec 1건을 {AlignVerifySavePath}\yyyyMMdd.csv 에 1행 append 한다.</summary>
        // 한 번만 시도한다. 실패 사유는 호출부가 판단한다(잠김이면 보관 파일로 간다).
        private static bool TryAppendBody(string szPath, string szBody) {
            try {
                bool bNewFile = !File.Exists(szPath);
                if (bNewFile) {
                    File.AppendAllText(szPath, CSV_HEADER + Environment.NewLine, Encoding.UTF8);
                }
                File.AppendAllText(szPath, szBody, Encoding.UTF8);
                return true;
            }
            catch (Exception) {
                return false;
            }
        }

        // 보관 파일의 내용을 본 파일 뒤에 붙이고 보관 파일을 지운다.
        //  본 파일이 아직 잠겨 있으면 아무것도 하지 않는다 — 다음 기록 때 다시 시도한다.
        private static void TryMergePending(string szPath, string szPendingPath) {
            try {
                if (File.Exists(szPendingPath) == false) {
                    return;
                }

                string[] lines = File.ReadAllLines(szPendingPath, Encoding.UTF8);
                var sbMerge = new StringBuilder();
                int nRows = 0;
                foreach (string line in lines) {
                    if (string.IsNullOrEmpty(line)) {
                        continue;
                    }
                    // 보관 파일에도 헤더가 들어 있다. 본 파일에 두 번 들어가면 안 된다.
                    string szTrimmed = line.TrimStart('﻿');
                    if (string.Equals(szTrimmed, CSV_HEADER, StringComparison.Ordinal)) {
                        continue;
                    }
                    sbMerge.Append(line);
                    sbMerge.Append(Environment.NewLine);
                    nRows = nRows + 1;
                }

                if (nRows == 0) {
                    try { File.Delete(szPendingPath); } catch { }
                    return;
                }

                bool bMerged = TryAppendBody(szPath, sbMerge.ToString());
                if (bMerged == false) {
                    return;
                }

                try { File.Delete(szPendingPath); } catch { }
                try {
                    Logging.PrintErrLog((int)ELogType.Error,
                        "[AlignVerifyCsvWriter] 보관 파일 " + nRows + "건을 본 파일로 병합했습니다");
                }
                catch { }
            }
            catch (Exception) {
            }
        }

        public static void Append(AlignVerifyRecord rec) {
            try {
                if (rec == null) { return; }

                string szDir = SystemHandler.Handle.Setting.AlignVerifySavePath;
                if (string.IsNullOrEmpty(szDir)) { return; }

                string szPath = Path.Combine(szDir, rec.RecordTime.ToString(FILE_DATE_FORMAT) + CSV_EXT);

                var sb = new StringBuilder();
                sb.Append(BuildLine(rec));
                sb.Append(Environment.NewLine);

                string szPendingPath = Path.Combine(
                    szDir, rec.RecordTime.ToString(FILE_DATE_FORMAT) + PENDING_SUFFIX + CSV_EXT);

                lock (s_lock) {
                    Directory.CreateDirectory(szDir);

                    // 잠김이 풀렸으면 밀려 있던 것부터 본 파일로 되돌린다 — 자가 복구.
                    TryMergePending(szPath, szPendingPath);

                    bool bWritten = TryAppendBody(szPath, sb.ToString());
                    if (bWritten) {
                        return;
                    }

                    // 본 파일이 잠겨 있다. 버리지 않고 옆 파일로 받아둔다.
                    //  재시도 대기는 넣지 않는다 — 이 코드는 PLC 응답 경로에서 도는데
                    //  엑셀 잠김은 몇 분 단위라 기다려봐야 택트만 먹는다.
                    bool bSpilled = TryAppendBody(szPendingPath, sb.ToString());
                    if (bSpilled) {
                        try {
                            Logging.PrintErrLog((int)ELogType.Error,
                                "[AlignVerifyCsvWriter] 본 파일 잠김 — " + PENDING_SUFFIX
                                + " 파일로 보관함(잠김 해제 시 자동 병합)");
                        }
                        catch { }
                        return;
                    }

                    try {
                        Logging.PrintErrLog((int)ELogType.Error,
                            "[AlignVerifyCsvWriter] 본/보관 파일 모두 기록 실패 — 1건 유실");
                    }
                    catch { }
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
