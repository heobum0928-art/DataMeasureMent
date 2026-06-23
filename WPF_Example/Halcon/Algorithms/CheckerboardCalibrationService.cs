//260623 hbk Phase 53: 체커보드 픽셀 캘리브레이션 서비스 (CAL-01)
using System;
using System.Collections.Generic;
using HalconDotNet;

namespace ReringProject.Halcon.Algorithms
{
    /// <summary>
    /// 체커보드(흑백 격자) 이미지를 입력받아 ① saddle_points_sub_pix 로 내부 코너를 서브픽셀 검출,
    /// ② 행/열 인접 간격을 median 통계로 추정, ③ 사용자 입력 칸 크기(mm)로 mm/px 직접 산출(D-07),
    /// ④ 중앙↔외곽 간격 편차%(D-05)를 계산하는 순수 알고리즘 서비스.
    /// 모든 Halcon 호출은 try { ... } catch { return false; } 패턴 (프로젝트 컨벤션).
    /// 순수 수학 연산은 static 메서드로 제공한다.
    /// 풀 카메라 캘리브(HALCON 전용 점판) 미사용(D-07 LOCK), undistort 미구현(D-08 LOCK).
    /// </summary>
    public class CheckerboardCalibrationService
    {
        //260623 hbk Phase 53: saddle 검출 / 왜곡 임계 / 최소 코너 수 상수 (A2/A3 UAT 튜닝 대비 노출)
        public const double DefaultSaddleSigma = 1.0;
        public const double DefaultSaddleThreshold = 5.0;
        public const double DistortionWarnThresholdPct = 1.0;
        public const int MinCornerCount = 12;
        //260623 hbk: 구조 가드 임계 — pitchGuess 붕괴(가짜 근접점) / 격자 불규칙(변동계수) 경고용 (HALCON 네이티브 하드닝)
        private const double PitchDivergenceWarnRatio = 1.4;   // medGap/pitchGuess 초과 시 가짜 근접점 의심
        private const double GridIrregularityWarnCv = 0.18;    // 격자 간격 변동계수(CV) 초과 시 불규칙 경고

        /// <summary>
        /// 체커보드 이미지로부터 mm/px 를 산출한다 (기본 saddle/왜곡 임계 사용).
        /// 긴 오버로드에 const 기본값을 위임한다.
        /// </summary>
        public bool TryCalibrate(HImage image, double knownMmPerCell, out CalibrationResult result, out string error)
        {
            //260623 hbk Phase 53: 짧은 오버로드 → 긴 오버로드 위임 (const 기본값)
            return TryCalibrate(image, knownMmPerCell, DefaultSaddleSigma, DefaultSaddleThreshold, DistortionWarnThresholdPct, out result, out error);
        }

        /// <summary>
        /// 체커보드 이미지로부터 mm/px 를 산출한다 (saddle Sigma/Threshold + 왜곡 임계% 노출, A2/A3 UAT 튜닝용).
        /// </summary>
        public bool TryCalibrate(HImage image, double knownMmPerCell, double saddleSigma, double saddleThreshold, double distortionWarnPct, out CalibrationResult result, out string error)
        {
            //260623 hbk: ROI 없는 호출 → ROI 오버로드에 위임 (전체 이미지)
            return TryCalibrate(image, knownMmPerCell, saddleSigma, saddleThreshold, distortionWarnPct, false, 0, 0, 0, 0, out result, out error);
        }

        /// <summary>
        /// ROI(reduce_domain) 지원 오버로드 — 보드 영역만 검출해 배경 가짜 saddle 을 억제한다 (HALCON 네이티브 하드닝).
        /// useRoi=false 면 전체 이미지(기존 동작). 코너 좌표는 reduce_domain 후에도 원본 이미지 좌표계로 반환된다.
        /// </summary>
        //260623 hbk: ROI(reduce_domain) 지원 오버로드 (배경 가짜점 억제)
        public bool TryCalibrate(HImage image, double knownMmPerCell, double saddleSigma, double saddleThreshold, double distortionWarnPct, bool useRoi, double roiRow1, double roiCol1, double roiRow2, double roiCol2, out CalibrationResult result, out string error)
        {
            //260623 hbk Phase 53: 코너검출→median 간격→mm/px→외곽 편차% (D-07/D-02/D-05)
            result = null;
            error = null;

            //260623 hbk Phase 53: 입력 가드 (V5 입력 검증)
            if (image == null)
            {
                error = "image is null";
                return false;
            }
            if (knownMmPerCell <= 0)
            {
                error = "칸 크기(mm) 가 유효하지 않습니다";
                return false;
            }

            //260623 hbk Phase 53: 코너 검출 (saddle point, try/catch return false)
            //260623 hbk: ROI 지정 시 reduce_domain 으로 검출 영역 한정 → 배경 가짜점 억제. detImage(reduced)는 검출 후 dispose.
            HTuple rows, cols;
            HImage detImage = image;
            bool reduced = false;
            try
            {
                if (useRoi && Math.Abs(roiRow2 - roiRow1) > 1.0 && Math.Abs(roiCol2 - roiCol1) > 1.0)
                {
                    HRegion roi = new HRegion(Math.Min(roiRow1, roiRow2), Math.Min(roiCol1, roiCol2), Math.Max(roiRow1, roiRow2), Math.Max(roiCol1, roiCol2));
                    detImage = image.ReduceDomain(roi);
                    roi.Dispose();
                    reduced = true;
                }
                HOperatorSet.SaddlePointsSubPix(detImage, "facet", saddleSigma, saddleThreshold, out rows, out cols);
            }
            catch
            {
                if (reduced && detImage != null)
                {
                    detImage.Dispose();
                }
                error = "코너 검출 실패 (조명/대비/ROI 확인)";
                return false;
            }
            if (reduced && detImage != null)
            {
                detImage.Dispose();
            }
            if (rows == null || rows.Length < MinCornerCount)
            {
                int found = (rows == null) ? 0 : rows.Length;
                error = "코너 부족 (검출 " + found + "개)";
                return false;
            }

            //260623 hbk Phase 53: 격자 피치 1차 추정 (최근접 이웃 간격 median) → 행/열 버킷팅 허용오차
            double pitchGuess = EstimatePitchGuess(rows, cols);
            if (pitchGuess <= 0)
            {
                error = "격자 피치 추정 실패";
                return false;
            }

            //260623 hbk Phase 53: 행/열 인접 간격 수집 (telecentric 축정렬 가정)
            List<EdgeGap> gapsX = CollectRowAdjacentColGaps(rows, cols, pitchGuess);
            List<EdgeGap> gapsY = CollectColAdjacentRowGaps(rows, cols, pitchGuess);

            List<double> gapValsX = ExtractGapValues(gapsX);
            List<double> gapValsY = ExtractGapValues(gapsY);

            double medGapX = Median(gapValsX);
            double medGapY = Median(gapValsY);
            double medGap = (medGapX + medGapY) / 2.0;
            if (medGap <= 0 || medGapX <= 0 || medGapY <= 0)
            {
                error = "유효 간격 없음";
                return false;
            }

            //260623 hbk: 구조 가드 — ① pitchGuess(최근접이웃) 대비 medGap 괴리 = 가짜 근접점/피치 붕괴 의심,
            //  ② 격자 간격 변동계수(CV) 과대 = 불규칙. saddle 단독 의존의 토폴로지 미검증을 보완 (육안 오버레이와 병행).
            double pitchDivergence = (pitchGuess > 0) ? (medGap / pitchGuess) : 0;
            List<double> allGaps = new List<double>(gapValsX);
            allGaps.AddRange(gapValsY);
            double gridCv = CoeffOfVariation(allGaps);
            bool structureWarn = false;
            string structureNote = "구조 점검 OK";
            if (pitchDivergence > PitchDivergenceWarnRatio)
            {
                structureWarn = true;
                structureNote = "가짜 근접점 의심 (피치 붕괴 " + pitchDivergence.ToString("F2") + "×)";
            }
            else if (gridCv > GridIrregularityWarnCv)
            {
                structureWarn = true;
                structureNote = "격자 불규칙 (간격 변동 " + (gridCv * 100.0).ToString("F1") + "%)";
            }

            //260623 hbk Phase 53: mm/px 산출 (D-02 단일 평균 + X/Y 리포트)
            double mmPerPixel = knownMmPerCell / medGap;
            double mmPerPixelX = knownMmPerCell / medGapX;
            double mmPerPixelY = knownMmPerCell / medGapY;

            //260623 hbk Phase 53: 중앙↔외곽 편차% (D-05 왜곡 게이트)
            double centerMean, outerMean;
            double devPct = ComputeCenterOuterDeviationPct(gapsX, gapsY, image, out centerMean, out outerMean);

            //260623 hbk: 검출 코너 좌표 HTuple → double[] (오버레이 가시화 + 리포트 노출)
            int cornerCount = rows.Length;
            double[] cornerRows = new double[cornerCount];
            double[] cornerCols = new double[cornerCount];
            for (int i = 0; i < cornerCount; i++)
            {
                cornerRows[i] = rows[i].D;
                cornerCols[i] = cols[i].D;
            }

            //260623 hbk: X/Y 축별 중앙↔외곽 편차% (리포트 보강)
            double devXPct = ComputeAxisDeviationPct(gapsX, image);
            double devYPct = ComputeAxisDeviationPct(gapsY, image);

            result = new CalibrationResult
            {
                MmPerPixel = mmPerPixel,
                MmPerPixelX = mmPerPixelX,
                MmPerPixelY = mmPerPixelY,
                MeanSpacingPx = medGap,
                CenterOuterDeviationPct = devPct,
                IsDistortionWarn = devPct > distortionWarnPct,
                CornerCount = cornerCount,
                //260623 hbk: 코너 좌표 + 중앙/외곽 평균(px) + X/Y 편차% 노출
                CornerRows = cornerRows,
                CornerCols = cornerCols,
                CenterMeanPx = centerMean,
                OuterMeanPx = outerMean,
                DeviationXPct = devXPct,
                DeviationYPct = devYPct,
                //260623 hbk: 구조 가드 결과 노출 (가짜점/불규칙 경고 + 지표)
                IsStructureWarn = structureWarn,
                StructureNote = structureNote,
                GridRegularityCv = gridCv,
                PitchGuessPx = pitchGuess
            };
            return true;
        }

        /// <summary>
        /// 격자 피치 1차 추정: 각 코너의 최근접 이웃 거리의 median. 버킷팅 허용오차 기준값.
        /// </summary>
        //260623 hbk Phase 53: 최근접 이웃 거리 median 으로 피치 추정
        private static double EstimatePitchGuess(HTuple rows, HTuple cols)
        {
            int n = rows.Length;
            List<double> nearest = new List<double>();
            for (int i = 0; i < n; i++)
            {
                double ri = rows[i].D;
                double ci = cols[i].D;
                double best = double.MaxValue;
                for (int j = 0; j < n; j++)
                {
                    if (j == i)
                    {
                        continue;
                    }
                    double dr = rows[j].D - ri;
                    double dc = cols[j].D - ci;
                    double dist = Math.Sqrt(dr * dr + dc * dc);
                    if (dist < best)
                    {
                        best = dist;
                    }
                }
                if (best < double.MaxValue)
                {
                    nearest.Add(best);
                }
            }
            return Median(nearest);
        }

        /// <summary>
        /// 행 버킷팅(같은 row 밴드) → 버킷 내 col 정렬 → 인접 col 차(가로 간격) 수집.
        /// 2×피치 점프(누락 코너)는 medGap 의 1.5배 초과로 절사하지 않고 median 이 자연 배제.
        /// </summary>
        //260623 hbk Phase 53: 행 버킷팅 → 가로 간격 + 중점 좌표 수집
        private static List<EdgeGap> CollectRowAdjacentColGaps(HTuple rows, HTuple cols, double pitchGuess)
        {
            double tol = pitchGuess * 0.5;
            Dictionary<int, List<int>> buckets = BucketByAxis(rows, pitchGuess);
            List<EdgeGap> gaps = new List<EdgeGap>();
            foreach (KeyValuePair<int, List<int>> bucket in buckets)
            {
                List<int> idx = bucket.Value;
                idx.Sort(delegate (int a, int b) { return cols[a].D.CompareTo(cols[b].D); });
                for (int k = 1; k < idx.Count; k++)
                {
                    int prev = idx[k - 1];
                    int cur = idx[k];
                    double gap = cols[cur].D - cols[prev].D;
                    if (gap <= tol)
                    {
                        continue; // 같은 코너 중복/노이즈
                    }
                    double midRow = (rows[prev].D + rows[cur].D) / 2.0;
                    double midCol = (cols[prev].D + cols[cur].D) / 2.0;
                    gaps.Add(new EdgeGap(gap, midRow, midCol));
                }
            }
            return gaps;
        }

        /// <summary>
        /// 열 버킷팅(같은 col 밴드) → 버킷 내 row 정렬 → 인접 row 차(세로 간격) 수집.
        /// </summary>
        //260623 hbk Phase 53: 열 버킷팅 → 세로 간격 + 중점 좌표 수집
        private static List<EdgeGap> CollectColAdjacentRowGaps(HTuple rows, HTuple cols, double pitchGuess)
        {
            double tol = pitchGuess * 0.5;
            Dictionary<int, List<int>> buckets = BucketByAxis(cols, pitchGuess);
            List<EdgeGap> gaps = new List<EdgeGap>();
            foreach (KeyValuePair<int, List<int>> bucket in buckets)
            {
                List<int> idx = bucket.Value;
                idx.Sort(delegate (int a, int b) { return rows[a].D.CompareTo(rows[b].D); });
                for (int k = 1; k < idx.Count; k++)
                {
                    int prev = idx[k - 1];
                    int cur = idx[k];
                    double gap = rows[cur].D - rows[prev].D;
                    if (gap <= tol)
                    {
                        continue;
                    }
                    double midRow = (rows[prev].D + rows[cur].D) / 2.0;
                    double midCol = (cols[prev].D + cols[cur].D) / 2.0;
                    gaps.Add(new EdgeGap(gap, midRow, midCol));
                }
            }
            return gaps;
        }

        /// <summary>
        /// 좌표 값을 round(value / pitchGuess) 인덱스로 버킷팅 → 인덱스별 코너 리스트.
        /// </summary>
        //260623 hbk Phase 53: 축 좌표를 피치 기준 round 버킷으로 그룹화
        private static Dictionary<int, List<int>> BucketByAxis(HTuple axisValues, double pitchGuess)
        {
            Dictionary<int, List<int>> buckets = new Dictionary<int, List<int>>();
            int n = axisValues.Length;
            for (int i = 0; i < n; i++)
            {
                int key = (int)Math.Round(axisValues[i].D / pitchGuess);
                if (!buckets.ContainsKey(key))
                {
                    buckets[key] = new List<int>();
                }
                buckets[key].Add(i);
            }
            return buckets;
        }

        /// <summary>
        /// EdgeGap 리스트에서 간격값만 추출.
        /// </summary>
        //260623 hbk Phase 53: 간격값 추출 헬퍼
        private static List<double> ExtractGapValues(List<EdgeGap> gaps)
        {
            List<double> vals = new List<double>();
            for (int i = 0; i < gaps.Count; i++)
            {
                vals.Add(gaps[i].Gap);
            }
            return vals;
        }

        /// <summary>
        /// 정렬 후 중앙값. 빈 리스트면 0. 누락 코너의 2×피치 점프를 자연 배제 (Anti-Pattern: mean 직접 사용 금지).
        /// </summary>
        //260623 hbk Phase 53: median 헬퍼 (robust 통계)
        private static double Median(List<double> vals)
        {
            if (vals == null || vals.Count == 0)
            {
                return 0;
            }
            List<double> sorted = new List<double>(vals);
            sorted.Sort();
            int m = sorted.Count;
            if ((m % 2) == 1)
            {
                return sorted[m / 2];
            }
            return (sorted[m / 2 - 1] + sorted[m / 2]) / 2.0;
        }

        /// <summary>
        /// 중앙↔외곽 간격 편차% (D-05 게이트). 각 간격 중점의 이미지 중심으로부터 거리로 중앙/외곽 그룹 분리.
        /// r ≤ 반대각선×0.33 → 중앙부, r ≥ 반대각선×0.66 → 외곽부. 한쪽 비면 0% (가드).
        /// </summary>
        //260623 hbk Phase 53: 중앙↔외곽 편차% 산출 (D-05)
        private static double ComputeCenterOuterDeviationPct(List<EdgeGap> gapsX, List<EdgeGap> gapsY, HImage image, out double centerMean, out double outerMean)
        {
            centerMean = 0;
            outerMean = 0;

            HTuple w, h;
            try
            {
                image.GetImageSize(out w, out h);
            }
            catch
            {
                return 0; // 크기 조회 실패 → 편차 판정 보류
            }
            double width = w.D;
            double height = h.D;
            double cx = width / 2.0;
            double cy = height / 2.0;
            double diag = Math.Sqrt(width * width + height * height) / 2.0;
            if (diag <= 0)
            {
                return 0;
            }
            double innerR = diag * 0.33;
            double outerR = diag * 0.66;

            List<double> centerGaps = new List<double>();
            List<double> outerGaps = new List<double>();
            AccumulateRadialGroups(gapsX, cx, cy, innerR, outerR, centerGaps, outerGaps);
            AccumulateRadialGroups(gapsY, cx, cy, innerR, outerR, centerGaps, outerGaps);

            if (centerGaps.Count == 0 || outerGaps.Count == 0)
            {
                return 0; // 한쪽 그룹 비면 편차 판정 불가 → 0% 가드
            }
            centerMean = Mean(centerGaps);
            outerMean = Mean(outerGaps);
            if (centerMean <= 0)
            {
                return 0;
            }
            return Math.Abs(outerMean - centerMean) / centerMean * 100.0;
        }

        /// <summary>
        /// 단일 축(가로 또는 세로) gaps 의 중앙↔외곽 편차% (X/Y 축별 리포트용).
        /// 종합 ComputeCenterOuterDeviationPct 와 동일 반경 게이트 + 0% 가드. 한쪽 그룹 비면 0.
        /// </summary>
        //260623 hbk: 단일 축 gaps 의 중앙↔외곽 편차% (X/Y 축별 리포트용). 종합과 동일 반경 게이트 + 0% 가드.
        private static double ComputeAxisDeviationPct(List<EdgeGap> gaps, HImage image)
        {
            HTuple w, h;
            try
            {
                image.GetImageSize(out w, out h);
            }
            catch
            {
                return 0; // 크기 조회 실패 → 편차 판정 보류
            }
            double width = w.D;
            double height = h.D;
            double cx = width / 2.0;
            double cy = height / 2.0;
            double diag = Math.Sqrt(width * width + height * height) / 2.0;
            if (diag <= 0)
            {
                return 0;
            }
            double innerR = diag * 0.33;
            double outerR = diag * 0.66;

            List<double> centerGaps = new List<double>();
            List<double> outerGaps = new List<double>();
            AccumulateRadialGroups(gaps, cx, cy, innerR, outerR, centerGaps, outerGaps);

            if (centerGaps.Count == 0 || outerGaps.Count == 0)
            {
                return 0; // 한쪽 그룹 비면 편차 판정 불가 → 0% 가드
            }
            double centerMean = Mean(centerGaps);
            double outerMean = Mean(outerGaps);
            if (centerMean <= 0)
            {
                return 0;
            }
            return Math.Abs(outerMean - centerMean) / centerMean * 100.0;
        }

        /// <summary>
        /// 각 간격의 중점 거리로 중앙/외곽 그룹에 누적 (경계대 제외로 대비 선명화).
        /// </summary>
        //260623 hbk Phase 53: 반경 기준 중앙/외곽 분류
        private static void AccumulateRadialGroups(List<EdgeGap> gaps, double cx, double cy, double innerR, double outerR, List<double> centerGaps, List<double> outerGaps)
        {
            for (int i = 0; i < gaps.Count; i++)
            {
                EdgeGap g = gaps[i];
                double dr = g.MidRow - cy;
                double dc = g.MidCol - cx;
                double r = Math.Sqrt(dr * dr + dc * dc);
                if (r <= innerR)
                {
                    centerGaps.Add(g.Gap);
                }
                else if (r >= outerR)
                {
                    outerGaps.Add(g.Gap);
                }
            }
        }

        /// <summary>
        /// 산술 평균. 빈 리스트면 0.
        /// </summary>
        //260623 hbk Phase 53: mean 헬퍼 (중앙/외곽 평균 간격용)
        private static double Mean(List<double> vals)
        {
            if (vals == null || vals.Count == 0)
            {
                return 0;
            }
            double sum = 0;
            for (int i = 0; i < vals.Count; i++)
            {
                sum += vals[i];
            }
            return sum / vals.Count;
        }

        /// <summary>
        /// 변동계수(stdev/mean) — 격자 간격 규칙성 지표. 클수록 불규칙(가짜점/누락/왜곡). 빈·단일 리스트면 0.
        /// </summary>
        //260623 hbk: 격자 규칙성 변동계수 헬퍼 (구조 가드)
        private static double CoeffOfVariation(List<double> vals)
        {
            if (vals == null || vals.Count < 2)
            {
                return 0;
            }
            double mean = Mean(vals);
            if (mean <= 0)
            {
                return 0;
            }
            double sumSq = 0;
            for (int i = 0; i < vals.Count; i++)
            {
                double d = vals[i] - mean;
                sumSq += d * d;
            }
            double variance = sumSq / vals.Count;
            return Math.Sqrt(variance) / mean;
        }
    }

    /// <summary>
    /// 인접 코너 간격 1건 — 간격값(px)과 그 간격 중점 좌표(왜곡 반경 판정용).
    /// </summary>
    //260623 hbk Phase 53: 간격 + 중점 좌표 캐리어
    internal struct EdgeGap
    {
        public double Gap;
        public double MidRow;
        public double MidCol;

        public EdgeGap(double gap, double midRow, double midCol)
        {
            Gap = gap;
            MidRow = midRow;
            MidCol = midCol;
        }
    }

    /// <summary>
    /// 캘리브 산출 결과 구조체 — 다음 wave 의 CalibrationWindow 가 소비하는 계약.
    /// MmPerPixel 만 PixelResolution 에 적용(D-02 단일 평균), X/Y 는 리포트 참고값.
    /// </summary>
    //260623 hbk Phase 53: 캘리브 결과 계약 클래스
    public class CalibrationResult
    {
        public double MmPerPixel { get; set; }          // 단일 적용값 (D-02)
        public double MmPerPixelX { get; set; }         // 리포트 참고값
        public double MmPerPixelY { get; set; }         // 리포트 참고값
        public double MeanSpacingPx { get; set; }       // 평균(가로·세로) 격자 간격(px)
        public double CenterOuterDeviationPct { get; set; } // 중앙↔외곽 간격 편차% (D-05)
        public bool IsDistortionWarn { get; set; }      // 편차% 임계 초과 경고 (D-05 게이트)
        public int CornerCount { get; set; }            // 검출된 코너 수
        //260623 hbk: 코너 오버레이 가시화 + 왜곡 상세 리포트용 신규 멤버
        public double[] CornerRows { get; set; }        // 검출 코너 row 좌표 전체 (length == CornerCount)
        public double[] CornerCols { get; set; }        // 검출 코너 col 좌표 전체 (length == CornerCount)
        public double CenterMeanPx { get; set; }        // 중앙부 평균 간격(px)
        public double OuterMeanPx { get; set; }         // 외곽부 평균 간격(px)
        public double DeviationXPct { get; set; }       // X축(가로) 중앙↔외곽 편차%
        public double DeviationYPct { get; set; }       // Y축(세로) 중앙↔외곽 편차%
        //260623 hbk: 구조 가드 — saddle 단독 토폴로지 미검증 보완 (가짜점/불규칙 경고)
        public bool IsStructureWarn { get; set; }       // 가짜 근접점/격자 불규칙 경고
        public string StructureNote { get; set; }       // 구조 점검 메시지 (리포트 표시)
        public double GridRegularityCv { get; set; }    // 격자 간격 변동계수 (작을수록 규칙적)
        public double PitchGuessPx { get; set; }        // 최근접이웃 피치 추정(px) — medGap 대비 붕괴 판정
    }
}
