using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using ReringProject.Setting;
using ReringProject.Utility;

namespace ReringProject {

    /// <summary>시퀀스+Datum 별 ② 안착 편차 집계 1행.</summary>
    public class AlignVerifySeatStat {
        public string SequenceName = "";
        public string DatumName = "";
        public int Count;
        public double AvgDeviationMm;
        public double MaxDeviationMm;
        public double LastDeviationMm;
        /// <summary>false = mm 환산 불가(해상도 0). 화면은 px 만 보여줘야 한다.</summary>
        public bool HasResolution;
        public double AvgDeviationPx;
        public double MaxDeviationPx;
    }

    /// <summary>Align 정합 조회 1회의 결과 묶음.</summary>
    public class AlignVerifyQueryResult {
        /// <summary>조회 자재의 전체 행(시간순).</summary>
        public List<AlignVerifyRecord> MaterialRows = new List<AlignVerifyRecord>();

        public bool HasMaterialAlign;
        public double MaterialAlignDistanceMm;
        public double MaterialAlignThetaDeg;

        public bool HasMaterialSeat;
        public double MaterialSeatDeviationMm;
        public bool MaterialSeatHasResolution;

        public int TrendAlignCount;
        public double TrendAlignAvgMm;
        public double TrendAlignMaxMm;
        public int TrendSeatCount;
        public double TrendSeatAvgMm;
        public double TrendSeatMaxMm;

        /// <summary>시퀀스+Datum 별 집계. SIDE_1~SIDE_4 가 각각 별도 키라 지그별 집계가 자연히 나온다.</summary>
        public List<AlignVerifySeatStat> SeatStats = new List<AlignVerifySeatStat>();

        public int TotalRowCount;
    }

    /// <summary>
    /// AlignVerifySavePath\yyyyMMdd.csv 를 읽어 자재번호 조인 + 최근 N개 추세 + 시퀀스별 집계를 낸다.
    /// 파싱 골격(컬럼 인덱스 상수 + RFC4180 파서)은 MeasurementHistoryCsvLoader 를 그대로 복제했다.
    /// 실패해도 throw 하지 않는다 — 부분 결과라도 반환하고 로그만 남긴다.
    /// </summary>
    public static class AlignVerifyCsvLoader {

        private const int COL_TIME = 0;
        private const int COL_KIND = 1;
        private const int COL_MATERIAL = 2;
        private const int COL_TARGET = 3;
        private const int COL_SLOT = 4;
        private const int COL_SEQUENCE = 5;
        private const int COL_DATUM = 6;
        private const int COL_JUDGE = 7;
        private const int COL_RESIDUAL_X = 8;
        private const int COL_RESIDUAL_Y = 9;
        private const int COL_RESIDUAL_THETA = 10;
        private const int COL_SCORE = 11;
        private const int COL_DETECTED_ROW = 12;
        private const int COL_DETECTED_COL = 13;
        private const int COL_REF_ROW = 14;
        private const int COL_REF_COL = 15;
        private const int COL_RESOLUTION = 16;
        private const int COL_DETECT_TIME = 17;
        private const int COL_FAIL_REASON = 18;
        private const int COL_IMAGE = 19;

        private const int COLUMN_COUNT = 20;
        private const string HEADER_FIRST_TOKEN = "기록시각";
        private const string CSV_EXT = ".csv";
        private const string FILE_DATE_FORMAT = "yyyyMMdd";
        private const int MAX_DAY_SPAN = 3660;   // 폭주 방어 — 약 10년

        /// <summary>
        /// dtFrom~dtTo 구간의 CSV 를 읽어 조회 결과를 만든다.
        /// nMaterialNo 가 음수면 자재 섹션은 비운다. nRecentCount 는 추세 표본 수.
        /// </summary>
        public static AlignVerifyQueryResult Query(DateTime dtFrom, DateTime dtTo, int nMaterialNo, int nRecentCount) {
            AlignVerifyQueryResult result = new AlignVerifyQueryResult();
            try {
                string szDir = SystemHandler.Handle.Setting.AlignVerifySavePath;
                if (string.IsNullOrEmpty(szDir)) {
                    return result;
                }
                if (Directory.Exists(szDir) == false) {
                    return result;
                }
                if (dtTo.Date < dtFrom.Date) {
                    return result;
                }

                List<AlignVerifyRecord> all = LoadRange(szDir, dtFrom, dtTo);
                all.Sort(CompareByRecordTime);
                result.TotalRowCount = all.Count;

                FillMaterialSection(result, all, nMaterialNo);
                FillTrendSection(result, all, nRecentCount);
                FillSeatStats(result, all);
            }
            catch (Exception ex) {
                try { Logging.PrintErrLog((int)ELogType.Error, "[AlignVerifyCsvLoader] Query failed: " + ex.Message); } catch { }
            }
            return result;
        }

        /// <summary>① 잔여 크기(mm) = sqrt(X²+Y²).</summary>
        public static double ComputeAlignDistanceMm(AlignVerifyRecord rec) {
            if (rec == null) {
                return 0.0;
            }
            return Math.Sqrt(rec.ResidualOffsetXmm * rec.ResidualOffsetXmm
                           + rec.ResidualOffsetYmm * rec.ResidualOffsetYmm);
        }

        /// <summary>
        /// ② 안착 편차. 반환 true 면 outMm 유효, false 면 해상도 미상이라 outPx 만 유효.
        /// PixelResolutionMmPerPx 는 <b>mm/px</b> 다 — EthernetPixelResolution(μm/px)과 혼동하면 1000배 틀린다.
        /// </summary>
        public static bool TryComputeSeatDeviation(AlignVerifyRecord rec, out double outPx, out double outMm) {
            outPx = 0.0;
            outMm = 0.0;
            if (rec == null) {
                return false;
            }

            double dRow = rec.DetectedRow - rec.RefRow;
            double dCol = rec.DetectedCol - rec.RefCol;
            outPx = Math.Sqrt(dRow * dRow + dCol * dCol);

            bool bHasRes = rec.PixelResolutionMmPerPx > 0.0;
            if (bHasRes) {
                outMm = outPx * rec.PixelResolutionMmPerPx;
                return true;
            }
            return false;
        }

        // ---- 내부 ----

        private static List<AlignVerifyRecord> LoadRange(string szDir, DateTime dtFrom, DateTime dtTo) {
            List<AlignVerifyRecord> all = new List<AlignVerifyRecord>();

            DateTime dtCursor = dtFrom.Date;
            DateTime dtEnd = dtTo.Date;
            int nGuard = 0;
            while (dtCursor <= dtEnd) {
                nGuard = nGuard + 1;
                if (nGuard > MAX_DAY_SPAN) {
                    break;
                }

                string szPath = Path.Combine(szDir, dtCursor.ToString(FILE_DATE_FORMAT) + CSV_EXT);
                if (File.Exists(szPath)) {
                    LoadOneFile(szPath, all);
                }
                dtCursor = dtCursor.AddDays(1);
            }
            return all;
        }

        private static void LoadOneFile(string szPath, List<AlignVerifyRecord> sink) {
            try {
                string[] lines = File.ReadAllLines(szPath, Encoding.UTF8);
                foreach (string line in lines) {
                    if (string.IsNullOrEmpty(line)) {
                        continue;
                    }
                    if (line.StartsWith(HEADER_FIRST_TOKEN, StringComparison.Ordinal)) {
                        continue;   // 헤더 행
                    }

                    List<string> fields = ParseCsvLine(line);
                    if (fields.Count < COLUMN_COUNT) {
                        continue;   // 손상 행 방어(원본 로더와 동일)
                    }

                    AlignVerifyRecord rec = BuildRecord(fields);
                    if (rec != null) {
                        sink.Add(rec);
                    }
                }
            }
            catch (Exception ex) {
                try { Logging.PrintErrLog((int)ELogType.Error, "[AlignVerifyCsvLoader] 파일 읽기 실패(건너뜀): " + szPath + " — " + ex.Message); } catch { }
            }
        }

        private static AlignVerifyRecord BuildRecord(List<string> f) {
            AlignVerifyRecord rec = new AlignVerifyRecord();
            rec.RecordTime = ParseTime(f[COL_TIME]);
            rec.Kind = f[COL_KIND];
            rec.MaterialNo = ParseInt(f[COL_MATERIAL], AlignVerifyRecord.NO_MATERIAL);
            rec.Target = f[COL_TARGET];
            rec.SlotToken = f[COL_SLOT];
            rec.SequenceName = f[COL_SEQUENCE];
            rec.DatumName = f[COL_DATUM];
            rec.Judgement = f[COL_JUDGE];
            rec.FailReason = f[COL_FAIL_REASON];
            rec.ImageFileName = f[COL_IMAGE];

            // 빈칸 = 그 섹션이 이 행에 없다는 뜻. double 0.0 으로 채우고 플래그로 구분한다.
            double dResidualX, dResidualY, dResidualTheta, dScore;
            bool bHasX = TryParseDouble(f[COL_RESIDUAL_X], out dResidualX);
            bool bHasY = TryParseDouble(f[COL_RESIDUAL_Y], out dResidualY);
            TryParseDouble(f[COL_RESIDUAL_THETA], out dResidualTheta);
            TryParseDouble(f[COL_SCORE], out dScore);
            if (bHasX && bHasY) {
                rec.ResidualOffsetXmm = dResidualX;
                rec.ResidualOffsetYmm = dResidualY;
                rec.ResidualThetaDeg = dResidualTheta;
                rec.Score = dScore;
                rec.HasResidual = true;
            }

            double dDetRow, dDetCol, dRefRow, dRefCol, dRes;
            bool bHasDetRow = TryParseDouble(f[COL_DETECTED_ROW], out dDetRow);
            bool bHasDetCol = TryParseDouble(f[COL_DETECTED_COL], out dDetCol);
            TryParseDouble(f[COL_REF_ROW], out dRefRow);
            TryParseDouble(f[COL_REF_COL], out dRefCol);
            TryParseDouble(f[COL_RESOLUTION], out dRes);
            if (bHasDetRow && bHasDetCol) {
                rec.DetectedRow = dDetRow;
                rec.DetectedCol = dDetCol;
                rec.RefRow = dRefRow;
                rec.RefCol = dRefCol;
                rec.PixelResolutionMmPerPx = dRes;
                rec.HasSeatOrigin = true;
            }

            rec.DetectTime = ParseTime(f[COL_DETECT_TIME]);
            return rec;
        }

        private static void FillMaterialSection(AlignVerifyQueryResult result, List<AlignVerifyRecord> all, int nMaterialNo) {
            if (nMaterialNo < 0) {
                return;
            }

            double dSeatSum = 0.0;
            int nSeatCount = 0;
            bool bSeatResolutionOk = true;

            foreach (AlignVerifyRecord rec in all) {
                if (rec.MaterialNo != nMaterialNo) {
                    continue;
                }
                result.MaterialRows.Add(rec);

                bool bIsAlign = string.Equals(rec.Kind, AlignVerifyRecord.KIND_ALIGN, StringComparison.Ordinal);
                if (bIsAlign) {
                    if (rec.HasResidual) {
                        // 시간 오름차순이라 뒤에 오는 것이 더 최신 — 그대로 덮어쓰면 최신이 남는다.
                        result.MaterialAlignDistanceMm = ComputeAlignDistanceMm(rec);
                        result.MaterialAlignThetaDeg = rec.ResidualThetaDeg;
                        result.HasMaterialAlign = true;
                    }
                    continue;
                }

                bool bIsSeat = string.Equals(rec.Kind, AlignVerifyRecord.KIND_SEAT, StringComparison.Ordinal);
                if (bIsSeat) {
                    double dPx, dMm;
                    bool bHasMm = TryComputeSeatDeviation(rec, out dPx, out dMm);
                    if (bHasMm) {
                        dSeatSum = dSeatSum + dMm;
                        nSeatCount = nSeatCount + 1;
                    }
                    else {
                        bSeatResolutionOk = false;
                    }
                }
            }

            if (nSeatCount > 0) {
                result.MaterialSeatDeviationMm = dSeatSum / nSeatCount;
                result.HasMaterialSeat = true;
                result.MaterialSeatHasResolution = bSeatResolutionOk;
            }
        }

        private static void FillTrendSection(AlignVerifyQueryResult result, List<AlignVerifyRecord> all, int nRecentCount) {
            int nTake = nRecentCount;
            if (nTake <= 0) {
                return;
            }

            int nStart = all.Count - nTake;
            if (nStart < 0) {
                nStart = 0;
            }

            double dAlignSum = 0.0;
            double dAlignMax = 0.0;
            int nAlignCount = 0;
            double dSeatSum = 0.0;
            double dSeatMax = 0.0;
            int nSeatCount = 0;

            for (int i = nStart; i < all.Count; i++) {
                AlignVerifyRecord rec = all[i];

                bool bIsAlign = string.Equals(rec.Kind, AlignVerifyRecord.KIND_ALIGN, StringComparison.Ordinal);
                if (bIsAlign) {
                    if (rec.HasResidual) {
                        double dDist = ComputeAlignDistanceMm(rec);
                        dAlignSum = dAlignSum + dDist;
                        if (dDist > dAlignMax) { dAlignMax = dDist; }
                        nAlignCount = nAlignCount + 1;
                    }
                    continue;
                }

                bool bIsSeat = string.Equals(rec.Kind, AlignVerifyRecord.KIND_SEAT, StringComparison.Ordinal);
                if (bIsSeat) {
                    double dPx, dMm;
                    bool bHasMm = TryComputeSeatDeviation(rec, out dPx, out dMm);
                    if (bHasMm) {
                        dSeatSum = dSeatSum + dMm;
                        if (dMm > dSeatMax) { dSeatMax = dMm; }
                        nSeatCount = nSeatCount + 1;
                    }
                }
            }

            result.TrendAlignCount = nAlignCount;
            result.TrendAlignMaxMm = dAlignMax;
            if (nAlignCount > 0) {
                result.TrendAlignAvgMm = dAlignSum / nAlignCount;
            }

            result.TrendSeatCount = nSeatCount;
            result.TrendSeatMaxMm = dSeatMax;
            if (nSeatCount > 0) {
                result.TrendSeatAvgMm = dSeatSum / nSeatCount;
            }
        }

        private static void FillSeatStats(AlignVerifyQueryResult result, List<AlignVerifyRecord> all) {
            Dictionary<string, AlignVerifySeatStat> map = new Dictionary<string, AlignVerifySeatStat>();
            Dictionary<string, double> sumMm = new Dictionary<string, double>();
            Dictionary<string, double> sumPx = new Dictionary<string, double>();

            foreach (AlignVerifyRecord rec in all) {
                bool bIsSeat = string.Equals(rec.Kind, AlignVerifyRecord.KIND_SEAT, StringComparison.Ordinal);
                if (bIsSeat == false) {
                    continue;
                }

                string szKey = rec.SequenceName + "/" + rec.DatumName;
                if (map.ContainsKey(szKey) == false) {
                    AlignVerifySeatStat fresh = new AlignVerifySeatStat();
                    fresh.SequenceName = rec.SequenceName;
                    fresh.DatumName = rec.DatumName;
                    fresh.HasResolution = true;
                    map[szKey] = fresh;
                    sumMm[szKey] = 0.0;
                    sumPx[szKey] = 0.0;
                }

                AlignVerifySeatStat stat = map[szKey];
                double dPx, dMm;
                bool bHasMm = TryComputeSeatDeviation(rec, out dPx, out dMm);

                stat.Count = stat.Count + 1;
                sumPx[szKey] = sumPx[szKey] + dPx;
                if (dPx > stat.MaxDeviationPx) { stat.MaxDeviationPx = dPx; }

                if (bHasMm) {
                    sumMm[szKey] = sumMm[szKey] + dMm;
                    if (dMm > stat.MaxDeviationMm) { stat.MaxDeviationMm = dMm; }
                    stat.LastDeviationMm = dMm;   // 시간 오름차순이라 마지막이 최신
                }
                else {
                    stat.HasResolution = false;
                }
            }

            foreach (KeyValuePair<string, AlignVerifySeatStat> kv in map) {
                AlignVerifySeatStat stat = kv.Value;
                if (stat.Count > 0) {
                    stat.AvgDeviationMm = sumMm[kv.Key] / stat.Count;
                    stat.AvgDeviationPx = sumPx[kv.Key] / stat.Count;
                }
                result.SeatStats.Add(stat);
            }
        }

        private static int CompareByRecordTime(AlignVerifyRecord a, AlignVerifyRecord b) {
            return a.RecordTime.CompareTo(b.RecordTime);
        }

        private static DateTime ParseTime(string szValue) {
            if (string.IsNullOrEmpty(szValue)) {
                return default(DateTime);
            }
            DateTime dt;
            bool bOk = DateTime.TryParse(szValue, CultureInfo.InvariantCulture, DateTimeStyles.None, out dt);
            if (bOk) {
                return dt;
            }
            return default(DateTime);
        }

        private static int ParseInt(string szValue, int nFallback) {
            if (string.IsNullOrEmpty(szValue)) {
                return nFallback;
            }
            int n;
            bool bOk = int.TryParse(szValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out n);
            if (bOk) {
                return n;
            }
            return nFallback;
        }

        private static bool TryParseDouble(string szValue, out double dValue) {
            dValue = 0.0;
            if (string.IsNullOrEmpty(szValue)) {
                return false;
            }
            return double.TryParse(szValue, NumberStyles.Float, CultureInfo.InvariantCulture, out dValue);
        }

        /// <summary>RFC4180 CSV 한 줄 파서. 따옴표 안의 콤마/개행을 무시하고 `""` 를 `"` 로 역이스케이프한다.</summary>
        private static List<string> ParseCsvLine(string szLine) {
            var fields = new List<string>();
            var sb = new StringBuilder();
            bool bInQuotes = false;
            int i = 0;

            while (i < szLine.Length) {
                char c = szLine[i];

                if (bInQuotes) {
                    if (c == '"') {
                        if (i + 1 < szLine.Length && szLine[i + 1] == '"') {
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

                if (c == '"') {
                    bInQuotes = true;
                    i++;
                    continue;
                }

                if (c == ',') {
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
