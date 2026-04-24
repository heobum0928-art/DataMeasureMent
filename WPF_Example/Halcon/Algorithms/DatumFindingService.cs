//260409 hbk Phase 4: Datum 찾기 서비스 — D-15, D-16
using System;
using HalconDotNet;
using ReringProject.Sequence;

namespace ReringProject.Halcon.Algorithms
{
    /// <summary>
    /// 이미지에서 두 에지 라인을 검출하고 교점(원점)과 각도를 구하여
    /// hom_mat2d 변환 행렬(런타임 보정용)과 티칭 기준값을 제공하는 서비스.
    /// D-15: TryFindDatum(런타임) + TryTeachDatum(티칭).
    /// D-16: GenMeasureRectangle2 → MeasurePos → FitLineContourXld → IntersectionLl 파이프라인.
    /// </summary>
    public class DatumFindingService
    {
        //260423 hbk Phase 12 D-15 — 수평 2-ROI concat 최소 에지점 (신뢰 라인 피팅 기준)
        private const int MIN_HORIZONTAL_EDGES = 10;

        /// <summary>
        /// 런타임 Datum 찾기: 이미지에서 두 라인을 검출하고 hom_mat2d 변환 행렬을 반환한다.
        /// config.IsConfigured=false이면 identity 변환을 반환(pass-through).
        /// </summary>
        /// <param name="image">검사 이미지</param>
        /// <param name="config">Datum ROI 설정</param>
        /// <param name="transform">출력 변환 행렬 (hom_mat2d)</param>
        /// <param name="error">오류 메시지 (성공 시 null)</param>
        /// <returns>성공 여부</returns>
        public bool TryFindDatum(HImage image, DatumConfig config, out HTuple transform, out string error) //260409 hbk Phase 4: D-15, D-16
        {
            error = null;
            HOperatorSet.HomMat2dIdentity(out transform);

            // D-08: 미설정 상태이면 identity pass-through
            if (config == null || !config.IsConfigured)
            {
                return true;
            }

            try
            {
                HTuple imageWidth, imageHeight;
                image.GetImageSize(out imageWidth, out imageHeight);

                // Line1 검출
                double line1RowBegin, line1ColBegin, line1RowEnd, line1ColEnd;
                string lineError;
                if (!TryFindLine(
                    image, imageWidth, imageHeight,
                    config.Line1_Row, config.Line1_Col, config.Line1_Phi, config.Line1_Length1, config.Line1_Length2,
                    config.Sigma, config.EdgeThreshold, config.EdgePolarity,
                    out line1RowBegin, out line1ColBegin, out line1RowEnd, out line1ColEnd,
                    out lineError))
                {
                    error = "Line1: " + lineError;
                    return false;
                }

                // Line2 검출
                double line2RowBegin, line2ColBegin, line2RowEnd, line2ColEnd;
                if (!TryFindLine(
                    image, imageWidth, imageHeight,
                    config.Line2_Row, config.Line2_Col, config.Line2_Phi, config.Line2_Length1, config.Line2_Length2,
                    config.Sigma, config.EdgeThreshold, config.EdgePolarity,
                    out line2RowBegin, out line2ColBegin, out line2RowEnd, out line2ColEnd,
                    out lineError))
                {
                    error = "Line2: " + lineError;
                    return false;
                }

                // D-03: 두 라인 교점 계산
                HTuple curRow, curCol, isOverlapping;
                HOperatorSet.IntersectionLl(
                    line1RowBegin, line1ColBegin, line1RowEnd, line1ColEnd,
                    line2RowBegin, line2ColBegin, line2RowEnd, line2ColEnd,
                    out curRow, out curCol, out isOverlapping);

                // IntersectionLl: isOverlapping==1 means lines are the SAME (collinear). //260423 hbk WR-01
                // For parallel lines, isOverlapping==0 but intersection coords are at ±Infinity. //260423 hbk WR-01
                if (isOverlapping.I == 1) //260423 hbk WR-01
                {
                    error = "Lines are collinear (identical), no unique intersection"; //260423 hbk WR-01
                    return false;
                }
                if (double.IsInfinity(curRow.D) || double.IsInfinity(curCol.D) || //260423 hbk WR-01
                    double.IsNaN(curRow.D) || double.IsNaN(curCol.D)) //260423 hbk WR-01
                {
                    error = "Lines are parallel, intersection is at infinity"; //260423 hbk WR-01
                    return false;
                }

                // Line1 방향 기준 현재 각도
                double curAngle = Math.Atan2(
                    line1RowEnd - line1RowBegin,
                    line1ColEnd - line1ColBegin);

                // D-07: hom_mat2d 변환 빌드 (평행이동 + 회전)
                double dRow = curRow.D - config.RefOriginRow;
                double dCol = curCol.D - config.RefOriginCol;
                double dAngle = curAngle - config.RefAngleRad;

                HTuple mat;
                HOperatorSet.HomMat2dIdentity(out mat);
                HOperatorSet.HomMat2dTranslate(mat, dRow, dCol, out mat);
                HOperatorSet.HomMat2dRotate(mat, dAngle, curRow.D, curCol.D, out transform);

                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                HOperatorSet.HomMat2dIdentity(out transform);
                return false;
            }
        }

        /// <summary>
        /// 티칭용 Datum 찾기: 이미지에서 두 라인을 검출하고 기준 원점/각도를 DatumConfig에 저장한다.
        /// 성공 시 config.IsConfigured = true로 설정된다.
        /// </summary>
        /// <param name="image">티칭 이미지</param>
        /// <param name="config">Datum ROI 설정 (기준값 저장 대상)</param>
        /// <param name="error">오류 메시지 (성공 시 null)</param>
        /// <returns>성공 여부</returns>
        //260409 hbk Phase 4: D-13
        //260423 hbk Phase 12 D-04 — AlgorithmType 기반 3-way 디스패치. 각 알고리즘 본문은 private 메서드로 분리.
        public bool TryTeachDatum(HImage image, DatumConfig config, out string error)
        {
            error = null;

            if (image == null || config == null)
            {
                error = "image or config is null";
                return false;
            }

            //260423 hbk Phase 12 D-04 — AlgorithmTypeEnum 은 string→enum 파싱 헬퍼 (Plan 01, DatumConfig.cs), 미지원 문자열은 TwoLineIntersect 폴백
            switch (config.AlgorithmTypeEnum)
            {
                case EDatumAlgorithm.CircleTwoHorizontal:
                    return TryTeachCircleTwoHorizontal(image, config, out error);
                case EDatumAlgorithm.VerticalTwoHorizontal:
                    return TryTeachVerticalTwoHorizontal(image, config, out error);
                case EDatumAlgorithm.TwoLineIntersect:
                default:
                    return TryTeachTwoLineIntersect(image, config, out error);
            }
        }

        //260423 hbk Phase 12 D-04 — 기존 Phase 4 TwoLineIntersect 본문 private 이동 (코드 동일, 회귀 0)
        private bool TryTeachTwoLineIntersect(HImage image, DatumConfig config, out string error)
        {
            error = null;

            try
            {
                HTuple imageWidth, imageHeight;
                image.GetImageSize(out imageWidth, out imageHeight);

                // Line1 검출
                double line1RowBegin, line1ColBegin, line1RowEnd, line1ColEnd;
                string lineError;
                if (!TryFindLine(
                    image, imageWidth, imageHeight,
                    config.Line1_Row, config.Line1_Col, config.Line1_Phi, config.Line1_Length1, config.Line1_Length2,
                    config.Sigma, config.EdgeThreshold, config.EdgePolarity,
                    out line1RowBegin, out line1ColBegin, out line1RowEnd, out line1ColEnd,
                    out lineError))
                {
                    config.LastTeachSucceeded = false; //260423 hbk Phase 11 D-11
                    error = "Line1: " + lineError;
                    return false;
                }

                // Line2 검출
                double line2RowBegin, line2ColBegin, line2RowEnd, line2ColEnd;
                if (!TryFindLine(
                    image, imageWidth, imageHeight,
                    config.Line2_Row, config.Line2_Col, config.Line2_Phi, config.Line2_Length1, config.Line2_Length2,
                    config.Sigma, config.EdgeThreshold, config.EdgePolarity,
                    out line2RowBegin, out line2ColBegin, out line2RowEnd, out line2ColEnd,
                    out lineError))
                {
                    config.LastTeachSucceeded = false; //260423 hbk Phase 11 D-11
                    error = "Line2: " + lineError;
                    return false;
                }

                // D-03: 교점 계산
                HTuple curRow, curCol, isOverlapping;
                HOperatorSet.IntersectionLl(
                    line1RowBegin, line1ColBegin, line1RowEnd, line1ColEnd,
                    line2RowBegin, line2ColBegin, line2RowEnd, line2ColEnd,
                    out curRow, out curCol, out isOverlapping);

                // IntersectionLl: isOverlapping==1 means lines are the SAME (collinear). //260423 hbk WR-01
                // For parallel lines, isOverlapping==0 but intersection coords are at ±Infinity. //260423 hbk WR-01
                if (isOverlapping.I == 1) //260423 hbk WR-01
                {
                    config.LastTeachSucceeded = false; //260423 hbk Phase 11 D-11
                    error = "Lines are collinear (identical), no unique intersection"; //260423 hbk WR-01
                    return false;
                }
                if (double.IsInfinity(curRow.D) || double.IsInfinity(curCol.D) || //260423 hbk WR-01
                    double.IsNaN(curRow.D) || double.IsNaN(curCol.D)) //260423 hbk WR-01
                {
                    config.LastTeachSucceeded = false; //260423 hbk Phase 11 D-11
                    error = "Lines are parallel, intersection is at infinity"; //260423 hbk WR-01
                    return false;
                }

                // D-13: 기준값 저장
                double curAngle = Math.Atan2(
                    line1RowEnd - line1RowBegin,
                    line1ColEnd - line1ColBegin);

                config.RefOriginRow = curRow.D;
                config.RefOriginCol = curCol.D;
                config.RefAngleRad = curAngle;
                config.IsConfigured = true;

                //260423 hbk Phase 11 D-11 — 검출 라인 좌표 휘발성 저장 (오버레이용)
                config.Line1Detected_RBegin = line1RowBegin;
                config.Line1Detected_CBegin = line1ColBegin;
                config.Line1Detected_REnd   = line1RowEnd;
                config.Line1Detected_CEnd   = line1ColEnd;
                config.Line2Detected_RBegin = line2RowBegin;
                config.Line2Detected_CBegin = line2ColBegin;
                config.Line2Detected_REnd   = line2RowEnd;
                config.Line2Detected_CEnd   = line2ColEnd;
                config.LastTeachSucceeded   = true;

                return true;
            }
            catch (Exception ex)
            {
                if (config != null) { config.LastTeachSucceeded = false; } //260423 hbk Phase 11 D-11
                error = ex.Message;
                return false;
            }
        }

        //260423 hbk Phase 12 — CircleTwoHorizontal 본문 (Task 2 에서 구현)
        private bool TryTeachCircleTwoHorizontal(HImage image, DatumConfig config, out string error)
        {
            error = "not implemented (Task 2)";
            return false;
        }

        //260423 hbk Phase 12 — VerticalTwoHorizontal 본문 (Task 3 에서 구현)
        private bool TryTeachVerticalTwoHorizontal(HImage image, DatumConfig config, out string error)
        {
            error = "not implemented (Task 3)";
            return false;
        }

        /// <summary>
        /// Rectangle2 ROI 내에서 에지 포인트를 검출하고 FitLineContourXld로 라인을 피팅한다.
        /// T-04-02: MeasureHandle은 finally 블록에서 해제된다.
        /// </summary>
        private bool TryFindLine( //260409 hbk Phase 4
            HImage image, HTuple imageWidth, HTuple imageHeight,
            double roiRow, double roiCol, double roiPhi, double roiLength1, double roiLength2,
            double sigma, int threshold, string polarity,
            out double lineRowBegin, out double lineColBegin,
            out double lineRowEnd, out double lineColEnd,
            out string error)
        {
            lineRowBegin = 0;
            lineColBegin = 0;
            lineRowEnd = 0;
            lineColEnd = 0;
            error = null;

            HTuple measureHandle = null;
            HObject contour = null;

            try
            {
                // 에지 측정 핸들 생성
                HOperatorSet.GenMeasureRectangle2(
                    roiRow, roiCol, roiPhi, roiLength1, roiLength2,
                    imageWidth, imageHeight, "nearest_neighbor",
                    out measureHandle);

                // 에지 포인트 검출
                HTuple rowEdge, colEdge, amp, dist;
                HOperatorSet.MeasurePos(
                    image, measureHandle, sigma, threshold,
                    polarity, "all",
                    out rowEdge, out colEdge, out amp, out dist);

                int edgeCount = rowEdge.TupleLength();
                if (edgeCount < 2)
                {
                    error = "insufficient edge points (" + edgeCount + ")";
                    return false;
                }

                // 라인 피팅 (FitLineContourXld, tukey 로버스트 추정)
                HOperatorSet.GenContourPolygonXld(out contour, rowEdge, colEdge);
                HTuple lr1, lc1, lr2, lc2, nr, nc, df;
                HOperatorSet.FitLineContourXld(
                    contour, "tukey", -1, 0, 5, 2,
                    out lr1, out lc1, out lr2, out lc2, out nr, out nc, out df);

                lineRowBegin = lr1.D;
                lineColBegin = lc1.D;
                lineRowEnd = lr2.D;
                lineColEnd = lc2.D;

                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
            finally
            {
                // T-04-02: MeasureHandle 항상 해제
                if (measureHandle != null)
                {
                    try { HOperatorSet.CloseMeasure(measureHandle); } catch { }
                }
                if (contour != null)
                {
                    try { contour.Dispose(); } catch { }
                }
            }
        }

        //260423 hbk Phase 12 D-06 — 단일 Rectangle2 ROI에서 에지점만 추출 (라인 피팅 전 단계). 수평 2-ROI concat 피팅용.
        //260423 hbk  TryFindLine 과 달리 FitLineContourXld 단계를 생략하고 raw edge tuples 반환.
        private bool TryExtractEdgePoints(
            HImage image, HTuple imageWidth, HTuple imageHeight,
            double roiRow, double roiCol, double roiPhi, double roiLength1, double roiLength2,
            double sigma, int threshold, string polarity,
            out HTuple rowEdge, out HTuple colEdge,
            out string error)
        {
            rowEdge = new HTuple();
            colEdge = new HTuple();
            error = null;

            HTuple measureHandle = null;
            try
            {
                HOperatorSet.GenMeasureRectangle2(
                    roiRow, roiCol, roiPhi, roiLength1, roiLength2,
                    imageWidth, imageHeight, "nearest_neighbor",
                    out measureHandle);

                HTuple amp, dist;
                HOperatorSet.MeasurePos(
                    image, measureHandle, sigma, threshold, polarity, "all",
                    out rowEdge, out colEdge, out amp, out dist);

                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
            finally
            {
                if (measureHandle != null) { try { HOperatorSet.CloseMeasure(measureHandle); } catch { } }
            }
        }
    }
}
