using System;
using HalconDotNet;
using ReringProject.Sequence;
using ReringProject.Setting;
using ReringProject.Utility;

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
        // 수평 2-ROI concat 최소 에지점 (신뢰 라인 피팅 기준)
        private const int MIN_HORIZONTAL_EDGES = 10;

        // Req 5d 방향 정합성 임계각 (고정값; 사용자 튜닝 필요해지면 DatumConfig 필드화 Deferred)
        private const double HORIZONTAL_TOLERANCE_DEG    = 15.0;
        // 실측 fixture 가로/세로축 83.5° (90°에서 6.5° 어긋남) 가 기존 5.0° 한계에 막혀 Teach 불가 → 임시 10.0° 완화.
        //  TODO: 사용자 튜닝 가능하도록 DatumConfig 필드화 검토. TwoLineAngleToleranceDeg 는 TwoLineIntersect 전용이라 이 경로에 미적용.
        private const double PERPENDICULAR_TOLERANCE_DEG = 10.0;

        /// <summary>
        /// 런타임 Datum 찾기: 이미지에서 두 라인을 검출하고 hom_mat2d 변환 행렬을 반환한다.
        /// config.IsConfigured=false이면 identity 변환을 반환(pass-through).
        /// </summary>
        /// <param name="image">검사 이미지</param>
        /// <param name="config">Datum ROI 설정</param>
        /// <param name="transform">출력 변환 행렬 (hom_mat2d)</param>
        /// <param name="error">오류 메시지 (성공 시 null)</param>
        /// <returns>성공 여부</returns>
        public bool TryFindDatum(HImage image, DatumConfig config, out HTuple transform, out string error)
        {
            error = null;
            HOperatorSet.HomMat2dIdentity(out transform);

            // D-08: 미설정 상태이면 identity pass-through
            if (config == null || !config.IsConfigured)
            {
                return true;
            }

            // per-ROI sentinel → 글로벌 복제 (idempotent, 최초 1회 의미)
            config.EnsurePerRoiDefaults();

            // 메서드 시작 시 reset (조기 return / catch 시 false 보장)
            config.LastFindSucceeded = false;

            // AlgorithmTypeEnum switch 로 3-way 분기 (TryTeachDatum 와 동일 패턴).
            //  inline 시 CTH/VTH 검사 시 Line1/Line2 빈 슬롯 접근 → 항상 NG 가 되어 분기 필요.
            switch (config.AlgorithmTypeEnum)
            {
                case EDatumAlgorithm.CircleTwoHorizontal:
                    return TryFindCircleTwoHorizontal(image, config, out transform, out error);
                case EDatumAlgorithm.VerticalTwoHorizontal:
                    return TryFindVerticalTwoHorizontal(image, config, out transform, out error);
                case EDatumAlgorithm.TwoLineIntersect:
                default:
                    return TryFindTwoLineIntersect(image, config, out transform, out error);
            }
        }

        // VerticalTwoHorizontalDualImage 변형 전용 2-image 오버로드.
        //  imageHorizontal: 가로축 이미지 (Horizontal_A + Horizontal_B ROI 검출 대상, TeachingImagePath 로드)
        //  imageVertical:   세로축 이미지 (Vertical ROI 검출 대상, TeachingImagePath_Vertical 로드)
        //  algorithm != VerticalTwoHorizontalDualImage 일 때는 error 반환 (잘못된 오버로드 호출 가드).
        public bool TryFindDatum(HImage imageHorizontal, HImage imageVertical, DatumConfig config, out HTuple transform, out string error)
        {
            error = null;
            HOperatorSet.HomMat2dIdentity(out transform);

            if (imageHorizontal == null || imageVertical == null || config == null)
            {
                error = "image(s) or config is null";
                return false;
            }

            config.EnsurePerRoiDefaults();
            config.LastFindSucceeded = false;

            // SameFrame 가드: DualImage 두 입력은 동일 sensor frame 가정 (동일 카메라 + 다른 조명/Z, 좌표 변환 0).
            //  width/height 불일치 시 IntersectionLl 의 두 픽셀 좌표가 다른 평면이 되어 의미 없는 origin/angle 산출 → 즉시 차단.
            HTuple wH, hH, wV, hV;
            imageHorizontal.GetImageSize(out wH, out hH);
            imageVertical.GetImageSize(out wV, out hV);
            if (wH.I != wV.I || hH.I != hV.I)
            {
                error = "DualImage requires same-frame image pair: horizontal " + wH.I + "x" + hH.I + " vs vertical " + wV.I + "x" + hV.I;
                return false;
            }

            if (config.AlgorithmTypeEnum != EDatumAlgorithm.VerticalTwoHorizontalDualImage)
            {
                error = "Algorithm is not VerticalTwoHorizontalDualImage; use single-image TryFindDatum overload";
                return false;
            }

            return TryFindVerticalTwoHorizontalDualImage(imageHorizontal, imageVertical, config, out transform, out error);
        }

        // 기존 TryFindDatum 본문 (TwoLineIntersect 검출 + transform 빌드) 을 private 으로 분리.
        //  Line1 / Line2 검출 → IntersectionLl → hom_mat2d (translate + rotate) → DetectedOrigin transient.
        private bool TryFindTwoLineIntersect(HImage image, DatumConfig config, out HTuple transform, out string error)
        {
            error = null;
            HOperatorSet.HomMat2dIdentity(out transform);

            try
            {
                HTuple imageWidth, imageHeight;
                image.GetImageSize(out imageWidth, out imageHeight);

                // Line1 검출
                double line1RowBegin, line1ColBegin, line1RowEnd, line1ColEnd;
                HTuple line1RawRows, line1RawCols;
                string lineError;
                // Line1 per-ROI 에지 파라미터 사용 (글로벌 EdgeThreshold/Sigma/EdgePolarity → Line1_*)
                if (!TryFindLine(
                    image, imageWidth, imageHeight,
                    config.Line1_Row, config.Line1_Col, config.Line1_Phi, config.Line1_Length1, config.Line1_Length2,
                    config.Line1_Sigma, config.Line1_EdgeThreshold, config.Line1_EdgePolarity,
                    config.Line1_EdgeDirection, config.Line1_EdgeSelection,
                    config.Line1_EdgeSampleCount, config.Line1_EdgeTrimCount,
                    out line1RowBegin, out line1ColBegin, out line1RowEnd, out line1ColEnd,
                    out line1RawRows, out line1RawCols,
                    out lineError,
                    "Line1"))
                {
                    error = "Line1: " + lineError;
                    return false;
                }
                config.Line1_DetectedEdgeRows = line1RawRows;
                config.Line1_DetectedEdgeCols = line1RawCols;

                // Line2 검출
                double line2RowBegin, line2ColBegin, line2RowEnd, line2ColEnd;
                HTuple line2RawRows, line2RawCols;
                // Line2 per-ROI 에지 파라미터 사용
                if (!TryFindLine(
                    image, imageWidth, imageHeight,
                    config.Line2_Row, config.Line2_Col, config.Line2_Phi, config.Line2_Length1, config.Line2_Length2,
                    config.Line2_Sigma, config.Line2_EdgeThreshold, config.Line2_EdgePolarity,
                    config.Line2_EdgeDirection, config.Line2_EdgeSelection,
                    config.Line2_EdgeSampleCount, config.Line2_EdgeTrimCount,
                    out line2RowBegin, out line2ColBegin, out line2RowEnd, out line2ColEnd,
                    out line2RawRows, out line2RawCols,
                    out lineError,
                    "Line2"))
                {
                    error = "Line2: " + lineError;
                    return false;
                }
                config.Line2_DetectedEdgeRows = line2RawRows;
                config.Line2_DetectedEdgeCols = line2RawCols;

                // D-03: 두 라인 교점 계산
                HTuple curRow, curCol, isOverlapping;
                HOperatorSet.IntersectionLl(
                    line1RowBegin, line1ColBegin, line1RowEnd, line1ColEnd,
                    line2RowBegin, line2ColBegin, line2RowEnd, line2ColEnd,
                    out curRow, out curCol, out isOverlapping);

                // IntersectionLl: isOverlapping==1 means lines are the SAME (collinear).
                // For parallel lines, isOverlapping==0 but intersection coords are at ±Infinity.
                if (isOverlapping.I == 1)
                {
                    error = "Lines are collinear (identical), no unique intersection";
                    return false;
                }
                if (double.IsInfinity(curRow.D) || double.IsInfinity(curCol.D) ||
                    double.IsNaN(curRow.D) || double.IsNaN(curCol.D))
                {
                    error = "Lines are parallel, intersection is at infinity";
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

                // DetectedOrigin transient write-back (RenderDatumFindResult 입력)
                config.DetectedOriginRow = curRow.D;
                config.DetectedOriginCol = curCol.D;
                config.DetectedRefAngle  = curAngle;
                // 2차(수직) 기준선 = Line2 실제 검출 각도 (X축 측정 기준)
                config.DetectedRefAngle2 = Math.Atan2(
                    line2RowEnd - line2RowBegin, line2ColEnd - line2ColBegin);
                // 결과 메트릭 (검출 점 개수 합계 + 각도 deg)
                int line1EdgeCount = 0;
                if (line1RawRows != null) line1EdgeCount = line1RawRows.TupleLength();
                int line2EdgeCount = 0;
                if (line2RawRows != null) line2EdgeCount = line2RawRows.TupleLength();
                config.DetectedEdgeCount = line1EdgeCount + line2EdgeCount;
                config.DetectedFitRMSE   = 0.0; // fit RMSE 미수집 (placeholder)
                config.DetectedAngleDeg  = curAngle * 180.0 / System.Math.PI;
                config.LastFindSucceeded = true;

                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                HOperatorSet.HomMat2dIdentity(out transform);
                return false;
            }
        }

        // CircleTwoHorizontal Find 분기.
        //  검출 로직은 TryTeachCircleTwoHorizontal 와 동일 (Step 1 Circle + Step 2/3 Horizontal A/B + Step 4 라인 fit
        //  + Step 5 수직 가상선 ∩ 수평 결합선). 차이는 마지막에 ref 덮어쓰기 대신 (curRow,curCol,curAngle) 와
        //  config.RefOriginRow/Col/RefAngleRad 의 delta 로 hom_mat2d 빌드.
        private bool TryFindCircleTwoHorizontal(HImage image, DatumConfig config, out HTuple transform, out string error)
        {
            error = null;
            HOperatorSet.HomMat2dIdentity(out transform);

            HObject contour = null;
            try
            {
                HTuple imageWidth, imageHeight;
                image.GetImageSize(out imageWidth, out imageHeight);

                // Step 1: Circle 검출 (TryTeachCircleTwoHorizontal 와 동일)
                var visionSvc = new VisionAlgorithmService();
                double centerRow, centerCol, radius;
                HTuple circleEdgeRows, circleEdgeCols;
                string circleError;
                string circlePolarity = EdgeOptionLists.MapRadialDirectionToHalconPolarity(config.Circle_RadialDirection);
                bool[] unusedStrips; // find 경로는 strip 색상 갱신 없음
                if (!visionSvc.TryFindCircleByPolarSampling(
                        image,
                        config.CircleROI_Row, config.CircleROI_Col, config.CircleROI_Radius,
                        config.Circle_PolarStepDeg, config.Circle_RectL1Ratio, config.Circle_RectL2Ratio,
                        config.Circle_Sigma, config.Circle_EdgeThreshold, circlePolarity,
                        config.Circle_EdgeSelection,
                        null,
                        out centerRow, out centerCol, out radius,
                        out circleEdgeRows, out circleEdgeCols,
                        out unusedStrips, // find 경로는 CircleStripSuccesses 갱신 안 함
                        out circleError))
                {
                    error = "Circle: " + circleError;
                    return false;
                }

                // Step 2: Horizontal A 에지점
                HTuple rowEdgeA, colEdgeA;
                string edgeErrorA;
                if (!TryExtractEdgePoints(
                        image, imageWidth, imageHeight,
                        config.Horizontal_A_Row, config.Horizontal_A_Col, config.Horizontal_A_Phi,
                        config.Horizontal_A_Length1, config.Horizontal_A_Length2,
                        config.Horizontal_A_Sigma, config.Horizontal_A_EdgeThreshold, config.Horizontal_A_EdgePolarity,
                        config.Horizontal_A_EdgeDirection, config.Horizontal_A_EdgeSelection,
                        config.Horizontal_A_EdgeSampleCount, config.Horizontal_A_EdgeTrimCount,
                        out rowEdgeA, out colEdgeA, out edgeErrorA,
                        "Horizontal_A"))
                {
                    error = "Horizontal_A: " + edgeErrorA;
                    return false;
                }

                // Step 3: Horizontal B 에지점
                HTuple rowEdgeB, colEdgeB;
                string edgeErrorB;
                if (!TryExtractEdgePoints(
                        image, imageWidth, imageHeight,
                        config.Horizontal_B_Row, config.Horizontal_B_Col, config.Horizontal_B_Phi,
                        config.Horizontal_B_Length1, config.Horizontal_B_Length2,
                        config.Horizontal_B_Sigma, config.Horizontal_B_EdgeThreshold, config.Horizontal_B_EdgePolarity,
                        config.Horizontal_B_EdgeDirection, config.Horizontal_B_EdgeSelection,
                        config.Horizontal_B_EdgeSampleCount, config.Horizontal_B_EdgeTrimCount,
                        out rowEdgeB, out colEdgeB, out edgeErrorB,
                        "Horizontal_B"))
                {
                    error = "Horizontal_B: " + edgeErrorB;
                    return false;
                }

                int totalEdges = rowEdgeA.TupleLength() + rowEdgeB.TupleLength();
                if (totalEdges < MIN_HORIZONTAL_EDGES)
                {
                    error = "Horizontal line fit failed: insufficient edges (" + totalEdges + ")";
                    return false;
                }

                // Step 4: A+B concat → 단일 라인 fit
                HTuple allRows = rowEdgeA.TupleConcat(rowEdgeB);
                HTuple allCols = colEdgeA.TupleConcat(colEdgeB);
                HOperatorSet.GenContourPolygonXld(out contour, allRows, allCols);

                HTuple hrB, hcB, hrE, hcE, nr, nc, df;
                try
                {
                    HOperatorSet.FitLineContourXld(
                        contour, "tukey", -1, 0, 5, 2,
                        out hrB, out hcB, out hrE, out hcE, out nr, out nc, out df);
                }
                catch (Exception fitEx)
                {
                    error = "Horizontal line fit failed: " + fitEx.Message;
                    return false;
                }

                // Step 5: 수직 가상선 (centerRow ± 1.0, centerCol) × 수평 결합선
                HTuple curRow, curCol, isOverlapping;
                HOperatorSet.IntersectionLl(
                    centerRow - 1.0, centerCol, centerRow + 1.0, centerCol,
                    hrB, hcB, hrE, hcE,
                    out curRow, out curCol, out isOverlapping);

                if (isOverlapping.I == 1)
                {
                    error = "Intersection undefined: lines are collinear";
                    return false;
                }
                if (double.IsInfinity(curRow.D) || double.IsInfinity(curCol.D) ||
                    double.IsNaN(curRow.D) || double.IsNaN(curCol.D))
                {
                    error = "Intersection undefined: lines are parallel";
                    return false;
                }

                // 현재 각도 = 수평 결합선 방향각 (Teach 와 동일 정의)
                double curAngle = Math.Atan2(hrE.D - hrB.D, hcE.D - hcB.D);

                // hom_mat2d 빌드 (translate + rotate around current origin)
                double dRow = curRow.D - config.RefOriginRow;
                double dCol = curCol.D - config.RefOriginCol;
                double dAngle = curAngle - config.RefAngleRad;
                HTuple mat;
                HOperatorSet.HomMat2dIdentity(out mat);
                HOperatorSet.HomMat2dTranslate(mat, dRow, dCol, out mat);
                HOperatorSet.HomMat2dRotate(mat, dAngle, curRow.D, curCol.D, out transform);

                // DetectedOrigin transient write-back (D-13)
                config.DetectedOriginRow = curRow.D;
                config.DetectedOriginCol = curCol.D;
                config.DetectedRefAngle  = curAngle;
                // 2차(수직) 기준선 = 원중심 통과 수직 가상선 (Step 5: centerRow±1, centerCol).
                //  방향벡터 (Δrow=+2, Δcol=0) → Atan2(2,0) = π/2 (순수 이미지-수직). X축 측정 기준.
                config.DetectedRefAngle2 = Math.PI / 2.0;
                config.DetectedCircleRow = centerRow; // E2 CompoundAngle 주입용 원중심
                config.DetectedCircleCol = centerCol;
                config.DetectedEdgeCount = circleEdgeRows.TupleLength() + totalEdges;
                config.DetectedFitRMSE   = 0.0;
                config.DetectedAngleDeg  = curAngle * 180.0 / System.Math.PI;

                // Visual transient 갱신 (검출 원 + 라인 외삽 + raw edge 점 새 위치 반영).
                //  DetectedOrigin 만 갱신하면 CTH 의 검출 원/center cross 가 teach 시점 위치에 박혀 "ROI 옮겨도 갱신 안 됨" 으로 인식됨.
                //  RenderDatumOverlay 가 사용하는 visual field 를 모두 갱신.
                config.CircleCenter_Row      = centerRow;
                config.CircleCenter_Col      = centerCol;
                config.CircleDetected_Radius = radius;
                //  Line1Detected = 수직 가상선 (Teach 와 동일 패턴, 가시 길이 ±50px)
                const double crossHalf = 50.0;
                config.Line1Detected_RBegin = centerRow - crossHalf;
                config.Line1Detected_CBegin = centerCol;
                config.Line1Detected_REnd   = centerRow + crossHalf;
                config.Line1Detected_CEnd   = centerCol;
                //  Line2Detected = 수평 결합 라인
                config.Line2Detected_RBegin = hrB.D;
                config.Line2Detected_CBegin = hcB.D;
                config.Line2Detected_REnd   = hrE.D;
                config.Line2Detected_CEnd   = hcE.D;
                //  Raw edge points (검출 trace 시각화)
                config.Circle_DetectedEdgeRows       = circleEdgeRows;
                config.Circle_DetectedEdgeCols       = circleEdgeCols;
                config.Horizontal_A_DetectedEdgeRows = rowEdgeA;
                config.Horizontal_A_DetectedEdgeCols = colEdgeA;
                config.Horizontal_B_DetectedEdgeRows = rowEdgeB;
                config.Horizontal_B_DetectedEdgeCols = colEdgeB;

                config.LastFindSucceeded = true;

                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                HOperatorSet.HomMat2dIdentity(out transform);
                return false;
            }
            finally
            {
                if (contour != null) { try { contour.Dispose(); } catch { } }
            }
        }

        // VerticalTwoHorizontal Find 분기.
        //  검출: Vertical 라인 + Horizontal A/B 합산 라인 → IntersectionLl. Teach 와 동일.
        //  ValidateHorizontalVerticalAngles 게이트 보존 (Teach 와 동일). 마지막에 transform 빌드.
        private bool TryFindVerticalTwoHorizontal(HImage image, DatumConfig config, out HTuple transform, out string error)
        {
            error = null;
            HOperatorSet.HomMat2dIdentity(out transform);

            HObject contour = null;
            try
            {
                HTuple imageWidth, imageHeight;
                image.GetImageSize(out imageWidth, out imageHeight);

                // Vertical 라인 검출
                double vrB, vcB, vrE, vcE;
                HTuple vertRawRows, vertRawCols;
                string lineError;
                if (!TryFindLine(
                        image, imageWidth, imageHeight,
                        config.Vertical_Row, config.Vertical_Col, config.Vertical_Phi,
                        config.Vertical_Length1, config.Vertical_Length2,
                        config.Vertical_Sigma, config.Vertical_EdgeThreshold, config.Vertical_EdgePolarity,
                        config.Vertical_EdgeDirection, config.Vertical_EdgeSelection,
                        config.Vertical_EdgeSampleCount, config.Vertical_EdgeTrimCount,
                        out vrB, out vcB, out vrE, out vcE,
                        out vertRawRows, out vertRawCols,
                        out lineError,
                        "Vertical"))
                {
                    error = "Vertical: " + lineError;
                    return false;
                }

                // Horizontal A
                HTuple rowEdgeA, colEdgeA;
                string edgeErrorA;
                if (!TryExtractEdgePoints(
                        image, imageWidth, imageHeight,
                        config.Horizontal_A_Row, config.Horizontal_A_Col, config.Horizontal_A_Phi,
                        config.Horizontal_A_Length1, config.Horizontal_A_Length2,
                        config.Horizontal_A_Sigma, config.Horizontal_A_EdgeThreshold, config.Horizontal_A_EdgePolarity,
                        config.Horizontal_A_EdgeDirection, config.Horizontal_A_EdgeSelection,
                        config.Horizontal_A_EdgeSampleCount, config.Horizontal_A_EdgeTrimCount,
                        out rowEdgeA, out colEdgeA, out edgeErrorA,
                        "Horizontal_A"))
                {
                    error = "Horizontal_A: " + edgeErrorA;
                    return false;
                }

                // Horizontal B
                HTuple rowEdgeB, colEdgeB;
                string edgeErrorB;
                if (!TryExtractEdgePoints(
                        image, imageWidth, imageHeight,
                        config.Horizontal_B_Row, config.Horizontal_B_Col, config.Horizontal_B_Phi,
                        config.Horizontal_B_Length1, config.Horizontal_B_Length2,
                        config.Horizontal_B_Sigma, config.Horizontal_B_EdgeThreshold, config.Horizontal_B_EdgePolarity,
                        config.Horizontal_B_EdgeDirection, config.Horizontal_B_EdgeSelection,
                        config.Horizontal_B_EdgeSampleCount, config.Horizontal_B_EdgeTrimCount,
                        out rowEdgeB, out colEdgeB, out edgeErrorB,
                        "Horizontal_B"))
                {
                    error = "Horizontal_B: " + edgeErrorB;
                    return false;
                }

                int totalEdges = rowEdgeA.TupleLength() + rowEdgeB.TupleLength();
                if (totalEdges < MIN_HORIZONTAL_EDGES)
                {
                    error = "Horizontal line fit failed: insufficient edges (" + totalEdges + ")";
                    return false;
                }

                // A+B concat → 라인 fit
                HTuple allRows = rowEdgeA.TupleConcat(rowEdgeB);
                HTuple allCols = colEdgeA.TupleConcat(colEdgeB);
                HOperatorSet.GenContourPolygonXld(out contour, allRows, allCols);

                HTuple hrB, hcB, hrE, hcE, nr, nc, df;
                try
                {
                    HOperatorSet.FitLineContourXld(
                        contour, "tukey", -1, 0, 5, 2,
                        out hrB, out hcB, out hrE, out hcE, out nr, out nc, out df);
                }
                catch (Exception fitEx)
                {
                    error = "Horizontal line fit failed: " + fitEx.Message;
                    return false;
                }

                // 수직 라인 × 수평 결합선
                HTuple curRow, curCol, isOverlapping;
                HOperatorSet.IntersectionLl(
                    vrB, vcB, vrE, vcE,
                    hrB, hcB, hrE, hcE,
                    out curRow, out curCol, out isOverlapping);

                if (isOverlapping.I == 1)
                {
                    error = "Intersection undefined: lines are collinear";
                    return false;
                }
                if (double.IsInfinity(curRow.D) || double.IsInfinity(curCol.D) ||
                    double.IsNaN(curRow.D) || double.IsNaN(curCol.D))
                {
                    error = "Intersection undefined: lines are parallel";
                    return false;
                }

                double curAngle = Math.Atan2(hrE.D - hrB.D, hcE.D - hcB.D);

                // 직각성 검증 (Teach 와 동일)
                double vertPhiDetected = Math.Atan2(vrE - vrB, vcE - vcB);
                string angleError;
                if (!ValidateHorizontalVerticalAngles(curAngle, vertPhiDetected, out angleError))
                {
                    error = angleError;
                    return false;
                }

                // hom_mat2d 빌드
                double dRow = curRow.D - config.RefOriginRow;
                double dCol = curCol.D - config.RefOriginCol;
                double dAngle = curAngle - config.RefAngleRad;
                HTuple mat;
                HOperatorSet.HomMat2dIdentity(out mat);
                HOperatorSet.HomMat2dTranslate(mat, dRow, dCol, out mat);
                HOperatorSet.HomMat2dRotate(mat, dAngle, curRow.D, curCol.D, out transform);

                // DetectedOrigin transient
                config.DetectedOriginRow = curRow.D;
                config.DetectedOriginCol = curCol.D;
                config.DetectedRefAngle  = curAngle;
                // 2차(수직) 기준선 = 수직 에지 실제 검출 각도 (vertPhiDetected). X축 측정 기준.
                config.DetectedRefAngle2 = vertPhiDetected;
                int vertEdgeCount = 0;
                if (vertRawRows != null) vertEdgeCount = vertRawRows.TupleLength();
                config.DetectedEdgeCount = vertEdgeCount + totalEdges;
                config.DetectedFitRMSE   = 0.0;
                config.DetectedAngleDeg  = curAngle * 180.0 / System.Math.PI;

                // Visual transient 갱신 (검출 라인 외삽 + raw edge 점 새 위치 반영).
                //  DetectedOrigin 만 갱신하면 "ROI 옮겨도 갱신 안 됨" 으로 인식됨.
                //  Line1Detected = 검출된 수직 라인, Line2Detected = 수평 결합 라인.
                config.Line1Detected_RBegin = vrB;
                config.Line1Detected_CBegin = vcB;
                config.Line1Detected_REnd   = vrE;
                config.Line1Detected_CEnd   = vcE;
                config.Line2Detected_RBegin = hrB.D;
                config.Line2Detected_CBegin = hcB.D;
                config.Line2Detected_REnd   = hrE.D;
                config.Line2Detected_CEnd   = hcE.D;
                //  Raw edge points (검출 trace 시각화)
                config.Vertical_DetectedEdgeRows     = vertRawRows;
                config.Vertical_DetectedEdgeCols     = vertRawCols;
                config.Horizontal_A_DetectedEdgeRows = rowEdgeA;
                config.Horizontal_A_DetectedEdgeCols = colEdgeA;
                config.Horizontal_B_DetectedEdgeRows = rowEdgeB;
                config.Horizontal_B_DetectedEdgeCols = colEdgeB;

                config.LastFindSucceeded = true;

                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                HOperatorSet.HomMat2dIdentity(out transform);
                return false;
            }
            finally
            {
                if (contour != null) { try { contour.Dispose(); } catch { } }
            }
        }

        //260617 hbk Phase 52 LEVEL-01 레벨링 각도 산출 (D-01). 기준 Datum 의 수평 2-ROI concat 피팅 라인의
        //  수평선(0°) 대비 각도(radian). TryFindVerticalTwoHorizontal 의 수평 피팅 구간만 재사용 — 중복 구현 금지.
        //  angle_lx 등가 = Math.Atan2 (코드베이스 관용, PATTERNS.md 확인). 회전각 부호 규약은 호출부(Plan 03)에서 -angleRad 확정.
        public bool TryGetLevelingAngle(HImage image, DatumConfig config, out double angleRad, out string error)
        {
            angleRad = 0.0;
            error = null;
            HObject contour = null;
            try
            {
                HTuple imageWidth, imageHeight;
                image.GetImageSize(out imageWidth, out imageHeight);

                // Horizontal A 에지점 추출 (TryFindVerticalTwoHorizontal 와 동일 인자)
                HTuple rowEdgeA, colEdgeA;
                string edgeErrorA;
                if (!TryExtractEdgePoints(
                        image, imageWidth, imageHeight,
                        config.Horizontal_A_Row, config.Horizontal_A_Col, config.Horizontal_A_Phi,
                        config.Horizontal_A_Length1, config.Horizontal_A_Length2,
                        config.Horizontal_A_Sigma, config.Horizontal_A_EdgeThreshold, config.Horizontal_A_EdgePolarity,
                        config.Horizontal_A_EdgeDirection, config.Horizontal_A_EdgeSelection,
                        config.Horizontal_A_EdgeSampleCount, config.Horizontal_A_EdgeTrimCount,
                        out rowEdgeA, out colEdgeA, out edgeErrorA,
                        "Horizontal_A"))
                {
                    error = "Horizontal_A: " + edgeErrorA;
                    return false;
                }

                // Horizontal B 에지점 추출
                HTuple rowEdgeB, colEdgeB;
                string edgeErrorB;
                if (!TryExtractEdgePoints(
                        image, imageWidth, imageHeight,
                        config.Horizontal_B_Row, config.Horizontal_B_Col, config.Horizontal_B_Phi,
                        config.Horizontal_B_Length1, config.Horizontal_B_Length2,
                        config.Horizontal_B_Sigma, config.Horizontal_B_EdgeThreshold, config.Horizontal_B_EdgePolarity,
                        config.Horizontal_B_EdgeDirection, config.Horizontal_B_EdgeSelection,
                        config.Horizontal_B_EdgeSampleCount, config.Horizontal_B_EdgeTrimCount,
                        out rowEdgeB, out colEdgeB, out edgeErrorB,
                        "Horizontal_B"))
                {
                    error = "Horizontal_B: " + edgeErrorB;
                    return false;
                }

                int totalEdges = rowEdgeA.TupleLength() + rowEdgeB.TupleLength();
                if (totalEdges < MIN_HORIZONTAL_EDGES)
                {
                    error = "Leveling line fit failed: insufficient edges (" + totalEdges + ")";
                    return false;
                }

                // A+B concat → 라인 fit (TryFindVerticalTwoHorizontal 와 동일)
                HTuple allRows = rowEdgeA.TupleConcat(rowEdgeB);
                HTuple allCols = colEdgeA.TupleConcat(colEdgeB);
                HOperatorSet.GenContourPolygonXld(out contour, allRows, allCols);

                HTuple hrB, hcB, hrE, hcE, nr, nc, df;
                HOperatorSet.FitLineContourXld(
                    contour, "tukey", -1, 0, 5, 2,
                    out hrB, out hcB, out hrE, out hcE, out nr, out nc, out df);

                // 수평선(0°) 대비 라인 각도. HDevelop angle_lx 와 동일 의미 (curAngle 패턴).
                angleRad = Math.Atan2(hrE.D - hrB.D, hcE.D - hcB.D);
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                angleRad = 0.0;
                return false;
            }
            finally
            {
                if (contour != null) { try { contour.Dispose(); } catch { } }
            }
        }

        //260618 hbk Phase 54 ALIGN-01 line-fit ROI 이동 오버로드 (D-02 ② — 큰 X,Y 변위 시 고정 티칭 ROI 가 에지 이탈하는 문제 해소).
        //  dRow,dCol = 매칭 변위(curRow-RefMatchRow, curCol-RefMatchCol). ROI 중심만 이동, Phi/Length/edge 파라미터 무변경.
        //  dRow=dCol=0 이면 기존 4-arg 오버로드와 동일 결과(회귀 0).
        public bool TryGetLevelingAngle(HImage image, DatumConfig config, double dRow, double dCol, out double angleRad, out string error)
        {
            angleRad = 0.0;
            error = null;
            HObject contour = null;
            try
            {
                HTuple imageWidth, imageHeight;
                image.GetImageSize(out imageWidth, out imageHeight);

                HTuple rowEdgeA, colEdgeA;
                string edgeErrorA;
                if (!TryExtractEdgePoints(
                        image, imageWidth, imageHeight,
                        config.Horizontal_A_Row + dRow, config.Horizontal_A_Col + dCol, config.Horizontal_A_Phi,
                        config.Horizontal_A_Length1, config.Horizontal_A_Length2,
                        config.Horizontal_A_Sigma, config.Horizontal_A_EdgeThreshold, config.Horizontal_A_EdgePolarity,
                        config.Horizontal_A_EdgeDirection, config.Horizontal_A_EdgeSelection,
                        config.Horizontal_A_EdgeSampleCount, config.Horizontal_A_EdgeTrimCount,
                        out rowEdgeA, out colEdgeA, out edgeErrorA,
                        "Horizontal_A"))
                {
                    error = "Horizontal_A: " + edgeErrorA;
                    return false;
                }
                HTuple rowEdgeB, colEdgeB;
                string edgeErrorB;
                if (!TryExtractEdgePoints(
                        image, imageWidth, imageHeight,
                        config.Horizontal_B_Row + dRow, config.Horizontal_B_Col + dCol, config.Horizontal_B_Phi,
                        config.Horizontal_B_Length1, config.Horizontal_B_Length2,
                        config.Horizontal_B_Sigma, config.Horizontal_B_EdgeThreshold, config.Horizontal_B_EdgePolarity,
                        config.Horizontal_B_EdgeDirection, config.Horizontal_B_EdgeSelection,
                        config.Horizontal_B_EdgeSampleCount, config.Horizontal_B_EdgeTrimCount,
                        out rowEdgeB, out colEdgeB, out edgeErrorB,
                        "Horizontal_B"))
                {
                    error = "Horizontal_B: " + edgeErrorB;
                    return false;
                }
                int totalEdges = rowEdgeA.TupleLength() + rowEdgeB.TupleLength();
                if (totalEdges < MIN_HORIZONTAL_EDGES)
                {
                    error = "Leveling line fit failed: insufficient edges (" + totalEdges + ")";
                    return false;
                }
                HTuple allRows = rowEdgeA.TupleConcat(rowEdgeB);
                HTuple allCols = colEdgeA.TupleConcat(colEdgeB);
                HOperatorSet.GenContourPolygonXld(out contour, allRows, allCols);
                HTuple hrB, hcB, hrE, hcE, nr, nc, df;
                HOperatorSet.FitLineContourXld(
                    contour, "tukey", -1, 0, 5, 2,
                    out hrB, out hcB, out hrE, out hcE, out nr, out nc, out df);
                angleRad = Math.Atan2(hrE.D - hrB.D, hcE.D - hcB.D);
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                angleRad = 0.0;
                return false;
            }
            finally
            {
                if (contour != null) { try { contour.Dispose(); } catch { } }
            }
        }

        //260618 hbk Phase 54 ALIGN-01 tilt 직선추출 (사용자 설계) — 전용 AlignLineRoi 1개에서 직선 추출 후 절대 각도(rad) 반환.
        //  measure_pos 재사용 (TryExtractEdgePoints + fit_line_contour_xld). 티칭/런타임 동일 호출 → Ref/측정 규약 일관.
        //  dRow,dCol = 패턴 매칭 변위(curRow-RefMatchRow, curCol-RefMatchCol). 티칭 시엔 0,0.
        public bool TryGetAlignLineAngle(HImage image, DatumConfig config, double dRow, double dCol, out double angleRad, out string error)
        {
            angleRad = 0.0;
            error = null;
            HObject contour = null;
            try
            {
                if (config.AlignLineRoi_Length1 <= 0.0 || config.AlignLineRoi_Length2 <= 0.0)
                {
                    error = "AlignLineRoi 미설정 (Length=0)";
                    return false;
                }
                HTuple imageWidth, imageHeight;
                image.GetImageSize(out imageWidth, out imageHeight);

                HTuple rowEdge, colEdge;
                string edgeErr;
                if (!TryExtractEdgePoints(
                        image, imageWidth, imageHeight,
                        config.AlignLineRoi_Row + dRow, config.AlignLineRoi_Col + dCol, config.AlignLineRoi_Phi,
                        config.AlignLineRoi_Length1, config.AlignLineRoi_Length2,
                        config.AlignLineRoi_Sigma, config.AlignLineRoi_EdgeThreshold, config.AlignLineRoi_EdgePolarity,
                        config.AlignLineRoi_EdgeDirection, config.AlignLineRoi_EdgeSelection,
                        config.AlignLineRoi_EdgeSampleCount, config.AlignLineRoi_EdgeTrimCount,
                        out rowEdge, out colEdge, out edgeErr,
                        "AlignLineRoi"))
                {
                    error = "AlignLineRoi: " + edgeErr;
                    return false;
                }
                if (rowEdge.TupleLength() < 2)
                {
                    error = "AlignLineRoi: insufficient edges (" + rowEdge.TupleLength() + ")";
                    return false;
                }
                HOperatorSet.GenContourPolygonXld(out contour, rowEdge, colEdge);
                HTuple hrB, hcB, hrE, hcE, nr, nc, df;
                HOperatorSet.FitLineContourXld(
                    contour, "tukey", -1, 0, 5, 2,
                    out hrB, out hcB, out hrE, out hcE, out nr, out nc, out df);
                angleRad = Math.Atan2(hrE.D - hrB.D, hcE.D - hcB.D);
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                angleRad = 0.0;
                return false;
            }
            finally
            {
                if (contour != null) { try { contour.Dispose(); } catch { } }
            }
        }

        // VerticalTwoHorizontalDualImage Find 분기.
        //  본문 = TryFindVerticalTwoHorizontal 복제 + ROI 별 이미지 입력 분기 (Vertical=imageVertical, Horizontal A/B=imageHorizontal).
        //  나머지 로직 (totalEdges 가드 / TupleConcat / FitLineContourXld / IntersectionLl / Validate / hom_mat2d / Detected transient) 모두 동일.
        private bool TryFindVerticalTwoHorizontalDualImage(HImage imageHorizontal, HImage imageVertical, DatumConfig config, out HTuple transform, out string error)
        {
            error = null;
            HOperatorSet.HomMat2dIdentity(out transform);

            HObject contour = null;
            try
            {
                HTuple imageVerticalWidth, imageVerticalHeight;
                imageVertical.GetImageSize(out imageVerticalWidth, out imageVerticalHeight);

                // Vertical 라인 검출 — imageVertical 사용 (D-34-01)
                double vrB, vcB, vrE, vcE;
                HTuple vertRawRows, vertRawCols;
                string lineError;
                if (!TryFindLine(
                        imageVertical, imageVerticalWidth, imageVerticalHeight,
                        config.Vertical_Row, config.Vertical_Col, config.Vertical_Phi,
                        config.Vertical_Length1, config.Vertical_Length2,
                        config.Vertical_Sigma, config.Vertical_EdgeThreshold, config.Vertical_EdgePolarity,
                        config.Vertical_EdgeDirection, config.Vertical_EdgeSelection,
                        config.Vertical_EdgeSampleCount, config.Vertical_EdgeTrimCount,
                        out vrB, out vcB, out vrE, out vcE,
                        out vertRawRows, out vertRawCols,
                        out lineError,
                        "Vertical"))
                {
                    error = "Vertical: " + lineError;
                    return false;
                }

                HTuple imageHorizontalWidth, imageHorizontalHeight;
                imageHorizontal.GetImageSize(out imageHorizontalWidth, out imageHorizontalHeight);

                // Horizontal A — imageHorizontal 사용 (D-34-01)
                HTuple rowEdgeA, colEdgeA;
                string edgeErrorA;
                if (!TryExtractEdgePoints(
                        imageHorizontal, imageHorizontalWidth, imageHorizontalHeight,
                        config.Horizontal_A_Row, config.Horizontal_A_Col, config.Horizontal_A_Phi,
                        config.Horizontal_A_Length1, config.Horizontal_A_Length2,
                        config.Horizontal_A_Sigma, config.Horizontal_A_EdgeThreshold, config.Horizontal_A_EdgePolarity,
                        config.Horizontal_A_EdgeDirection, config.Horizontal_A_EdgeSelection,
                        config.Horizontal_A_EdgeSampleCount, config.Horizontal_A_EdgeTrimCount,
                        out rowEdgeA, out colEdgeA, out edgeErrorA,
                        "Horizontal_A"))
                {
                    error = "Horizontal_A: " + edgeErrorA;
                    return false;
                }

                // Horizontal B — imageHorizontal 사용 (D-34-01)
                HTuple rowEdgeB, colEdgeB;
                string edgeErrorB;
                if (!TryExtractEdgePoints(
                        imageHorizontal, imageHorizontalWidth, imageHorizontalHeight,
                        config.Horizontal_B_Row, config.Horizontal_B_Col, config.Horizontal_B_Phi,
                        config.Horizontal_B_Length1, config.Horizontal_B_Length2,
                        config.Horizontal_B_Sigma, config.Horizontal_B_EdgeThreshold, config.Horizontal_B_EdgePolarity,
                        config.Horizontal_B_EdgeDirection, config.Horizontal_B_EdgeSelection,
                        config.Horizontal_B_EdgeSampleCount, config.Horizontal_B_EdgeTrimCount,
                        out rowEdgeB, out colEdgeB, out edgeErrorB,
                        "Horizontal_B"))
                {
                    error = "Horizontal_B: " + edgeErrorB;
                    return false;
                }

                int totalEdges = rowEdgeA.TupleLength() + rowEdgeB.TupleLength();
                if (totalEdges < MIN_HORIZONTAL_EDGES)
                {
                    error = "Horizontal line fit failed: insufficient edges (" + totalEdges + ")";
                    return false;
                }

                // A+B concat → 라인 fit
                HTuple allRows = rowEdgeA.TupleConcat(rowEdgeB);
                HTuple allCols = colEdgeA.TupleConcat(colEdgeB);
                HOperatorSet.GenContourPolygonXld(out contour, allRows, allCols);

                HTuple hrB, hcB, hrE, hcE, nr, nc, df;
                try
                {
                    HOperatorSet.FitLineContourXld(
                        contour, "tukey", -1, 0, 5, 2,
                        out hrB, out hcB, out hrE, out hcE, out nr, out nc, out df);
                }
                catch (Exception fitEx)
                {
                    error = "Horizontal line fit failed: " + fitEx.Message;
                    return false;
                }

                // 수직 라인 × 수평 결합선
                HTuple curRow, curCol, isOverlapping;
                HOperatorSet.IntersectionLl(
                    vrB, vcB, vrE, vcE,
                    hrB, hcB, hrE, hcE,
                    out curRow, out curCol, out isOverlapping);

                if (isOverlapping.I == 1)
                {
                    error = "Intersection undefined: lines are collinear";
                    return false;
                }
                if (double.IsInfinity(curRow.D) || double.IsInfinity(curCol.D) ||
                    double.IsNaN(curRow.D) || double.IsNaN(curCol.D))
                {
                    error = "Intersection undefined: lines are parallel";
                    return false;
                }

                double curAngle = Math.Atan2(hrE.D - hrB.D, hcE.D - hcB.D);

                // 직각성 검증 (Teach 와 동일)
                double vertPhiDetected = Math.Atan2(vrE - vrB, vcE - vcB);
                string angleError;
                if (!ValidateHorizontalVerticalAngles(curAngle, vertPhiDetected, out angleError))
                {
                    error = angleError;
                    return false;
                }

                // hom_mat2d 빌드
                double dRow = curRow.D - config.RefOriginRow;
                double dCol = curCol.D - config.RefOriginCol;
                double dAngle = curAngle - config.RefAngleRad;
                HTuple mat;
                HOperatorSet.HomMat2dIdentity(out mat);
                HOperatorSet.HomMat2dTranslate(mat, dRow, dCol, out mat);
                HOperatorSet.HomMat2dRotate(mat, dAngle, curRow.D, curCol.D, out transform);

                // DetectedOrigin transient
                config.DetectedOriginRow = curRow.D;
                config.DetectedOriginCol = curCol.D;
                config.DetectedRefAngle  = curAngle;
                config.DetectedRefAngle2 = vertPhiDetected;
                int vertEdgeCount = 0;
                if (vertRawRows != null) vertEdgeCount = vertRawRows.TupleLength();
                config.DetectedEdgeCount = vertEdgeCount + totalEdges;
                config.DetectedFitRMSE   = 0.0;
                config.DetectedAngleDeg  = curAngle * 180.0 / System.Math.PI;

                // ExpectedAngleDeg / AngleTolerance 게이트 (TwoLineAngleToleranceDeg L915 sentinel 모델과 정렬).
                //  Tolerance > 0 일 때만 활성. Expected=0 + Tolerance>0 도 활성 (사용자가 0° 검증을 의도) — sentinel 단일 조건.
                //  결과는 transient AngleValidationStatus 에 기록. error 반환 안 함 (Find 자체는 PASS 유지 — UI 색상 배지로만 표시 — Plan 03).
                //  wrap-around: (Detected - Expected) 를 [-180, 180] 정규화 후 절댓값 비교 (179 vs -179 = 2° 차이로 정상 판정).
                if (config.AngleTolerance > 0.0)
                {
                    double diff = config.DetectedAngleDeg - config.ExpectedAngleDeg;
                    diff = ((diff + 540.0) % 360.0) - 180.0;
                    double absDiff = System.Math.Abs(diff);
                    if (absDiff <= config.AngleTolerance)
                        config.AngleValidationStatus = EAngleValidationStatus.Pass;
                    else
                        config.AngleValidationStatus = EAngleValidationStatus.Fail;
                }
                else
                {
                    config.AngleValidationStatus = EAngleValidationStatus.None;
                }

                // Visual transient — Line1Detected = 검출된 수직 라인, Line2Detected = 수평 결합 라인.
                config.Line1Detected_RBegin = vrB;
                config.Line1Detected_CBegin = vcB;
                config.Line1Detected_REnd   = vrE;
                config.Line1Detected_CEnd   = vcE;
                config.Line2Detected_RBegin = hrB.D;
                config.Line2Detected_CBegin = hcB.D;
                config.Line2Detected_REnd   = hrE.D;
                config.Line2Detected_CEnd   = hcE.D;
                //  Raw edge points (검출 trace 시각화)
                config.Vertical_DetectedEdgeRows     = vertRawRows;
                config.Vertical_DetectedEdgeCols     = vertRawCols;
                config.Horizontal_A_DetectedEdgeRows = rowEdgeA;
                config.Horizontal_A_DetectedEdgeCols = colEdgeA;
                config.Horizontal_B_DetectedEdgeRows = rowEdgeB;
                config.Horizontal_B_DetectedEdgeCols = colEdgeB;

                config.LastFindSucceeded = true;

                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                HOperatorSet.HomMat2dIdentity(out transform);
                return false;
            }
            finally
            {
                if (contour != null) { try { contour.Dispose(); } catch { } }
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
        // AlgorithmType 기반 3-way 디스패치. 각 알고리즘 본문은 private 메서드로 분리.
        public bool TryTeachDatum(HImage image, DatumConfig config, out string error)
        {
            error = null;

            if (image == null || config == null)
            {
                error = "image or config is null";
                return false;
            }

            // per-ROI sentinel → 글로벌 복제 (idempotent, 최초 1회 의미)
            config.EnsurePerRoiDefaults();

            // AlgorithmTypeEnum 은 string→enum 파싱 헬퍼 (Plan 01, DatumConfig.cs), 미지원 문자열은 TwoLineIntersect 폴백
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

        // VerticalTwoHorizontalDualImage 변형 전용 2-image Teach 오버로드.
        //  imageHorizontal: 가로축 이미지 (Horizontal_A + Horizontal_B ROI 티칭 대상)
        //  imageVertical:   세로축 이미지 (Vertical ROI 티칭 대상)
        //  algorithm != VerticalTwoHorizontalDualImage 일 때는 error 반환 (잘못된 오버로드 호출 가드).
        //  기존 단일-이미지 TryTeachDatum 시그니처는 unchanged — 1-image algorithm 3종 회귀 0 (D-34-13).
        public bool TryTeachDatum(HImage imageHorizontal, HImage imageVertical, DatumConfig config, out string error)
        {
            error = null;

            if (imageHorizontal == null || imageVertical == null || config == null)
            {
                error = "image(s) or config is null";
                return false;
            }

            config.EnsurePerRoiDefaults();

            // SameFrame 가드 (Find 와 대칭). 에러 메시지 포맷 byte-identical — 사용자 진단 일관성.
            HTuple wH, hH, wV, hV;
            imageHorizontal.GetImageSize(out wH, out hH);
            imageVertical.GetImageSize(out wV, out hV);
            if (wH.I != wV.I || hH.I != hV.I)
            {
                error = "DualImage requires same-frame image pair: horizontal " + wH.I + "x" + hH.I + " vs vertical " + wV.I + "x" + hV.I;
                return false;
            }

            if (config.AlgorithmTypeEnum != EDatumAlgorithm.VerticalTwoHorizontalDualImage)
            {
                error = "Algorithm is not VerticalTwoHorizontalDualImage; use single-image TryTeachDatum overload";
                return false;
            }

            return TryTeachVerticalTwoHorizontalDualImage(imageHorizontal, imageVertical, config, out error);
        }

        // 기존 Phase 4 TwoLineIntersect 본문 private 이동 (코드 동일, 회귀 0)
        private bool TryTeachTwoLineIntersect(HImage image, DatumConfig config, out string error)
        {
            error = null;

            try
            {
                HTuple imageWidth, imageHeight;
                image.GetImageSize(out imageWidth, out imageHeight);

                // Line1 검출
                double line1RowBegin, line1ColBegin, line1RowEnd, line1ColEnd;

                HObject horect;
                HOperatorSet.GenRectangle2(out horect, config.Line1_Row, config.Line1_Col, config.Line1_Phi, config.Line1_Length1, config.Line1_Length2);

                string lineError;
                HTuple line1RawRows, line1RawCols;
                // Line1 per-ROI 에지 파라미터 사용 (글로벌 EdgeThreshold/Sigma/EdgePolarity → Line1_*)
                if (!TryFindLine(
                    image, imageWidth, imageHeight,
                    config.Line1_Row, config.Line1_Col, config.Line1_Phi, config.Line1_Length1, config.Line1_Length2,
                    config.Line1_Sigma, config.Line1_EdgeThreshold, config.Line1_EdgePolarity,
                    config.Line1_EdgeDirection, config.Line1_EdgeSelection,
                    config.Line1_EdgeSampleCount, config.Line1_EdgeTrimCount,
                    out line1RowBegin, out line1ColBegin, out line1RowEnd, out line1ColEnd,
                    out line1RawRows, out line1RawCols,
                    out lineError,
                    "Line1"))
                {
                    config.LastTeachSucceeded = false;
                    error = "Line1: " + lineError;
                    return false;
                }
                // Line1 raw 점 DatumConfig 기록
                config.Line1_DetectedEdgeRows = line1RawRows;
                config.Line1_DetectedEdgeCols = line1RawCols;

                // Line2 검출
                double line2RowBegin, line2ColBegin, line2RowEnd, line2ColEnd;
                HTuple line2RawRows, line2RawCols;
                // Line2 per-ROI 에지 파라미터 사용
                if (!TryFindLine(
                    image, imageWidth, imageHeight,
                    config.Line2_Row, config.Line2_Col, config.Line2_Phi, config.Line2_Length1, config.Line2_Length2,
                    config.Line2_Sigma, config.Line2_EdgeThreshold, config.Line2_EdgePolarity,
                    config.Line2_EdgeDirection, config.Line2_EdgeSelection,
                    config.Line2_EdgeSampleCount, config.Line2_EdgeTrimCount,
                    out line2RowBegin, out line2ColBegin, out line2RowEnd, out line2ColEnd,
                    out line2RawRows, out line2RawCols,
                    out lineError,
                    "Line2"))
                {
                    config.LastTeachSucceeded = false;
                    error = "Line2: " + lineError;
                    return false;
                }
                // Line2 raw 점 DatumConfig 기록
                config.Line2_DetectedEdgeRows = line2RawRows;
                config.Line2_DetectedEdgeCols = line2RawCols;

                // D-03: 교점 계산
                HTuple curRow, curCol, isOverlapping;
                HOperatorSet.IntersectionLl(
                    line1RowBegin, line1ColBegin, line1RowEnd, line1ColEnd,
                    line2RowBegin, line2ColBegin, line2RowEnd, line2ColEnd,
                    out curRow, out curCol, out isOverlapping);

                // IntersectionLl: isOverlapping==1 means lines are the SAME (collinear).
                // For parallel lines, isOverlapping==0 but intersection coords are at ±Infinity.
                if (isOverlapping.I == 1)
                {
                    config.LastTeachSucceeded = false;
                    error = "Lines are collinear (identical), no unique intersection";
                    return false;
                }
                if (double.IsInfinity(curRow.D) || double.IsInfinity(curCol.D) ||
                    double.IsNaN(curRow.D) || double.IsNaN(curCol.D))
                {
                    config.LastTeachSucceeded = false;
                    error = "Lines are parallel, intersection is at infinity";
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

                // TwoLineIntersect 두 라인 각도 게이트
                //  사용자 임계각 N=TwoLineAngleToleranceDeg, 0 이면 게이트 off (PASS).
                //  측정값 = |line1 phi - line2 phi| 를 [0,180) 정규화 후 90° 와의 편차.
                //  CircleTwoHorizontal/VerticalTwoHorizontal 의 ValidateHorizontalVerticalAngles 와 별개 게이트 (TwoLineIntersect 한정).
                if (config.TwoLineAngleToleranceDeg > 0.0)
                {
                    double phi1 = Math.Atan2(line1RowEnd - line1RowBegin, line1ColEnd - line1ColBegin);
                    double phi2 = Math.Atan2(line2RowEnd - line2RowBegin, line2ColEnd - line2ColBegin);
                    double deltaDeg = Math.Abs((phi1 - phi2) * 180.0 / Math.PI);
                    while (deltaDeg >= 180.0) deltaDeg -= 180.0;
                    double perpErr = Math.Abs(deltaDeg - 90.0);
                    if (perpErr > config.TwoLineAngleToleranceDeg)
                    {
                        config.LastTeachSucceeded = false;
                        // ASCII (deg, +/-)
                        error = "Two-line angle out of range: "
                              + deltaDeg.ToString("F1")
                              + " deg (expected 90 +/- "
                              + config.TwoLineAngleToleranceDeg.ToString("F1")
                              + " deg)";
                        return false;
                    }
                }

                // 검출 라인 좌표 휘발성 저장 (오버레이용)
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
                if (config != null) { config.LastTeachSucceeded = false; }
                error = ex.Message;
                return false;
            }
        }

        // CircleTwoHorizontal: 원 센터 수직 가상선 ∩ 수평 2-ROI concat 교점 (D-05/D-06/D-08/D-13/D-14)
        private bool TryTeachCircleTwoHorizontal(HImage image, DatumConfig config, out string error)
        {
            error = null;

            // TryFindLine 패턴(833)으로 통일: 단일 contour 만 사용
            HObject contour = null;

            try
            {
                HTuple imageWidth, imageHeight;
                image.GetImageSize(out imageWidth, out imageHeight);

                // TryFindCircle → TryFindCircleByPolarSampling 교체 (raw 점 반환 + 360° 분포)
                var visionSvc = new VisionAlgorithmService();
                double centerRow, centerCol, radius;
                HTuple circleEdgeRows, circleEdgeCols;
                string circleError;
                // 진단 로그 (FAIL contingency 대비, PASS 후 주석 처리 가능)
                Logging.PrintLog((int)ELogType.Trace,
                    "TryTeachCircleTwoHorizontal: ROI=(" + config.CircleROI_Row + "," + config.CircleROI_Col + ",r=" + config.CircleROI_Radius + ") " +
                    "polar(step=" + config.Circle_PolarStepDeg + " L1=" + config.Circle_RectL1Ratio + " L2=" + config.Circle_RectL2Ratio + ")");
                // Circle_RadialDirection ("Inward"/"Outward") → polarity ("positive"/"negative") override (EdgePolarity 무시)
                string circlePolarity = EdgeOptionLists.MapRadialDirectionToHalconPolarity(config.Circle_RadialDirection);
                // Circle per-ROI 에지 파라미터 + Polar sampling 파라미터 (14-04 신규 3 필드)
                bool[] circleStrips;
                if (!visionSvc.TryFindCircleByPolarSampling(
                        image,
                        config.CircleROI_Row, config.CircleROI_Col, config.CircleROI_Radius,
                        config.Circle_PolarStepDeg, config.Circle_RectL1Ratio, config.Circle_RectL2Ratio,
                        config.Circle_Sigma, config.Circle_EdgeThreshold, circlePolarity,
                        config.Circle_EdgeSelection,
                        null, // teaching-phase identity transform (legacy 동일)
                        out centerRow, out centerCol, out radius,
                        out circleEdgeRows, out circleEdgeCols,
                        out circleStrips,
                        out circleError))
                {
                    config.LastTeachSucceeded = false;
                    error = "Circle fit failed: " + circleError;
                    return false;
                }
                config.CircleStripSuccesses = circleStrips;

                config.CircleCenter_Row      = centerRow;
                config.CircleCenter_Col      = centerCol;
                config.CircleDetected_Radius = radius;
                // Phase 13-05 D-VIZ-03 carry-over closure: raw 점 직접 반환 (이전엔 빈 HTuple)
                config.Circle_DetectedEdgeRows = circleEdgeRows;
                config.Circle_DetectedEdgeCols = circleEdgeCols;

                // 수평 A ROI 에지점
                HTuple rowEdgeA, colEdgeA;
                string edgeErrorA;
                // Horizontal_A per-ROI 에지 파라미터 사용
                if (!TryExtractEdgePoints(
                        image, imageWidth, imageHeight,
                        config.Horizontal_A_Row, config.Horizontal_A_Col, config.Horizontal_A_Phi,
                        config.Horizontal_A_Length1, config.Horizontal_A_Length2,
                        config.Horizontal_A_Sigma, config.Horizontal_A_EdgeThreshold, config.Horizontal_A_EdgePolarity,
                        config.Horizontal_A_EdgeDirection, config.Horizontal_A_EdgeSelection,
                        config.Horizontal_A_EdgeSampleCount, config.Horizontal_A_EdgeTrimCount,
                        out rowEdgeA, out colEdgeA, out edgeErrorA,
                        "Horizontal_A"))
                {
                    config.LastTeachSucceeded = false;
                    error = "Horizontal line fit failed: " + edgeErrorA;
                    return false;
                }
                // Horizontal_A raw 점 DatumConfig 기록
                config.Horizontal_A_DetectedEdgeRows = rowEdgeA;
                config.Horizontal_A_DetectedEdgeCols = colEdgeA;

                // 수평 B ROI 에지점
                HTuple rowEdgeB, colEdgeB;
                string edgeErrorB;
                // Horizontal_B per-ROI 에지 파라미터 사용
                if (!TryExtractEdgePoints(
                        image, imageWidth, imageHeight,
                        config.Horizontal_B_Row, config.Horizontal_B_Col, config.Horizontal_B_Phi,
                        config.Horizontal_B_Length1, config.Horizontal_B_Length2,
                        config.Horizontal_B_Sigma, config.Horizontal_B_EdgeThreshold, config.Horizontal_B_EdgePolarity,
                        config.Horizontal_B_EdgeDirection, config.Horizontal_B_EdgeSelection,
                        config.Horizontal_B_EdgeSampleCount, config.Horizontal_B_EdgeTrimCount,
                        out rowEdgeB, out colEdgeB, out edgeErrorB,
                        "Horizontal_B"))
                {
                    config.LastTeachSucceeded = false;
                    error = "Horizontal line fit failed: " + edgeErrorB;
                    return false;
                }
                // Horizontal_B raw 점 DatumConfig 기록
                config.Horizontal_B_DetectedEdgeRows = rowEdgeB;
                config.Horizontal_B_DetectedEdgeCols = colEdgeB;

                // 수평 2-ROI concat 에지점 합계 임계값 검사
                int totalEdges = rowEdgeA.TupleLength() + rowEdgeB.TupleLength();
                if (totalEdges < MIN_HORIZONTAL_EDGES)
                {
                    config.LastTeachSucceeded = false;
                    error = "Horizontal line fit failed: insufficient edges (" + totalEdges + ")";
                    return false;
                }

                // TupleConcat + 단일 GenContourPolygonXld 로 변경 (TryFindLine 833 라인 패턴과 통일). 기존 ConcatObj 패턴은 length-2 fit 결과를 만들어 IntersectionLl 인자 길이 불일치 발생.
                HTuple allRows = rowEdgeA.TupleConcat(rowEdgeB);
                HTuple allCols = colEdgeA.TupleConcat(colEdgeB);
                HOperatorSet.GenContourPolygonXld(out contour, allRows, allCols);

                HTuple hrB, hcB, hrE, hcE, nr, nc, df;
                try
                {
                    HOperatorSet.FitLineContourXld(
                        contour, "tukey", -1, 0, 5, 2,
                        out hrB, out hcB, out hrE, out hcE, out nr, out nc, out df);
                }
                catch (Exception fitEx)
                {
                    config.LastTeachSucceeded = false;
                    error = "Horizontal line fit failed: " + fitEx.Message;
                    return false;
                }

                // 수직 가상선 (centerRow ± 1.0, centerCol) × 수평 결합선 IntersectionLl
                HTuple curRow, curCol, isOverlapping;
                HOperatorSet.IntersectionLl(
                    centerRow - 1.0, centerCol, centerRow + 1.0, centerCol,
                    hrB, hcB, hrE, hcE,
                    out curRow, out curCol, out isOverlapping);

                // 교점 중첩/평행 감지 (기존 TwoLineIntersect 와 동일 로직)
                if (isOverlapping.I == 1)
                {
                    config.LastTeachSucceeded = false;
                    error = "Intersection undefined: lines are collinear";
                    return false;
                }
                if (double.IsInfinity(curRow.D) || double.IsInfinity(curCol.D) ||
                    double.IsNaN(curRow.D) || double.IsNaN(curCol.D))
                {
                    config.LastTeachSucceeded = false;
                    error = "Intersection undefined: lines are parallel";
                    return false;
                }

                // 기준값 저장
                config.RefOriginRow = curRow.D;
                config.RefOriginCol = curCol.D;
                config.RefAngleRad  = Math.Atan2(hrE.D - hrB.D, hcE.D - hcB.D);
                config.IsConfigured = true;

                // 검출 라인 오버레이 필드 재사용 (Line1Detected = 수직 가상선 2점, Line2Detected = 수평 결합선)
                //  RenderDatumOverlay 의 LastTeachSucceeded 분기가 이 필드를 노랑/시안 선으로 렌더.
                const double crossHalf = 50.0;
                config.Line1Detected_RBegin = centerRow - crossHalf;
                config.Line1Detected_CBegin = centerCol;
                config.Line1Detected_REnd   = centerRow + crossHalf;
                config.Line1Detected_CEnd   = centerCol;
                config.Line2Detected_RBegin = hrB.D;
                config.Line2Detected_CBegin = hcB.D;
                config.Line2Detected_REnd   = hrE.D;
                config.Line2Detected_CEnd   = hcE.D;
                config.LastTeachSucceeded   = true;

                // Req 5d 방향 정합성 검증 (CircleTwoHorizontal: 수직 가상선 phi = PI/2 고정)
                double vertPhiCircle = Math.PI / 2.0; // 수직 가상선은 col=const 이므로 Atan2(1,0)=PI/2
                string angleError;
                if (!ValidateHorizontalVerticalAngles(config.RefAngleRad, vertPhiCircle, out angleError))
                {
                    config.LastTeachSucceeded = false;
                    error = angleError;
                    return false;
                }
                return true;
            }
            catch (Exception ex)
            {
                if (config != null) { config.LastTeachSucceeded = false; }
                error = ex.Message;
                return false;
            }
            finally
            {
                // 단일 contour 만 dispose (contourA/contourB/concatContour 제거)
                if (contour != null) { try { contour.Dispose(); } catch { } }
            }
        }

        // VerticalTwoHorizontal: 수직 ROI 라인 ∩ 수평 2-ROI concat 교점 (D-07/D-08/D-13/D-14)
        //  수직 ROI 는 Line1_* 필드 재사용. CircleCenter_* / CircleDetected_Radius 는 건드리지 않음.
        private bool TryTeachVerticalTwoHorizontal(HImage image, DatumConfig config, out string error)
        {
            error = null;

            // TryFindLine 패턴(833)으로 통일: 단일 contour 만 사용
            HObject contour = null;

            try
            {
                HTuple imageWidth, imageHeight;
                image.GetImageSize(out imageWidth, out imageHeight);

                // Vertical_* 슬롯 (의미적 분리, Line1_* 재사용 종료)
                double vrB, vcB, vrE, vcE;
                HTuple vertRawRows, vertRawCols;
                string lineError;
                // Vertical per-ROI 에지 파라미터 사용 (수직 ROI 의미 분리)
                if (!TryFindLine(
                        image, imageWidth, imageHeight,
                        config.Vertical_Row, config.Vertical_Col, config.Vertical_Phi,
                        config.Vertical_Length1, config.Vertical_Length2,
                        config.Vertical_Sigma, config.Vertical_EdgeThreshold, config.Vertical_EdgePolarity,
                        config.Vertical_EdgeDirection, config.Vertical_EdgeSelection,
                        config.Vertical_EdgeSampleCount, config.Vertical_EdgeTrimCount,
                        out vrB, out vcB, out vrE, out vcE,
                        out vertRawRows, out vertRawCols,
                        out lineError,
                        "Vertical"))
                {
                    config.LastTeachSucceeded = false;
                    error = "Vertical line fit failed: " + lineError;
                    return false;
                }
                // 수직 라인 raw 점 Vertical_DetectedEdge* 에 기록 (Line1_DetectedEdge* 슬롯 종료)
                config.Vertical_DetectedEdgeRows = vertRawRows;
                config.Vertical_DetectedEdgeCols = vertRawCols;

                // 수평 A ROI 에지점
                HTuple rowEdgeA, colEdgeA;
                string edgeErrorA;
                // Horizontal_A per-ROI 에지 파라미터 사용
                if (!TryExtractEdgePoints(
                        image, imageWidth, imageHeight,
                        config.Horizontal_A_Row, config.Horizontal_A_Col, config.Horizontal_A_Phi,
                        config.Horizontal_A_Length1, config.Horizontal_A_Length2,
                        config.Horizontal_A_Sigma, config.Horizontal_A_EdgeThreshold, config.Horizontal_A_EdgePolarity,
                        config.Horizontal_A_EdgeDirection, config.Horizontal_A_EdgeSelection,
                        config.Horizontal_A_EdgeSampleCount, config.Horizontal_A_EdgeTrimCount,
                        out rowEdgeA, out colEdgeA, out edgeErrorA,
                        "Horizontal_A"))
                {
                    config.LastTeachSucceeded = false;
                    error = "Horizontal line fit failed: " + edgeErrorA;
                    return false;
                }
                // Horizontal_A raw 점 DatumConfig 기록
                config.Horizontal_A_DetectedEdgeRows = rowEdgeA;
                config.Horizontal_A_DetectedEdgeCols = colEdgeA;

                // 수평 B ROI 에지점
                HTuple rowEdgeB, colEdgeB;
                string edgeErrorB;
                // Horizontal_B per-ROI 에지 파라미터 사용
                if (!TryExtractEdgePoints(
                        image, imageWidth, imageHeight,
                        config.Horizontal_B_Row, config.Horizontal_B_Col, config.Horizontal_B_Phi,
                        config.Horizontal_B_Length1, config.Horizontal_B_Length2,
                        config.Horizontal_B_Sigma, config.Horizontal_B_EdgeThreshold, config.Horizontal_B_EdgePolarity,
                        config.Horizontal_B_EdgeDirection, config.Horizontal_B_EdgeSelection,
                        config.Horizontal_B_EdgeSampleCount, config.Horizontal_B_EdgeTrimCount,
                        out rowEdgeB, out colEdgeB, out edgeErrorB,
                        "Horizontal_B"))
                {
                    config.LastTeachSucceeded = false;
                    error = "Horizontal line fit failed: " + edgeErrorB;
                    return false;
                }
                // Horizontal_B raw 점 DatumConfig 기록
                config.Horizontal_B_DetectedEdgeRows = rowEdgeB;
                config.Horizontal_B_DetectedEdgeCols = colEdgeB;

                // 수평 2-ROI concat 에지점 합계 임계값 검사
                int totalEdges = rowEdgeA.TupleLength() + rowEdgeB.TupleLength();
                if (totalEdges < MIN_HORIZONTAL_EDGES)
                {
                    config.LastTeachSucceeded = false;
                    error = "Horizontal line fit failed: insufficient edges (" + totalEdges + ")";
                    return false;
                }

                // TupleConcat + 단일 GenContourPolygonXld 로 변경 (TryFindLine 833 라인 패턴과 통일). 기존 ConcatObj 패턴은 length-2 fit 결과를 만들어 IntersectionLl 인자 길이 불일치 발생.
                HTuple allRows = rowEdgeA.TupleConcat(rowEdgeB);
                HTuple allCols = colEdgeA.TupleConcat(colEdgeB);
                HOperatorSet.GenContourPolygonXld(out contour, allRows, allCols);

                HTuple hrB, hcB, hrE, hcE, nr, nc, df;
                try
                {
                    HOperatorSet.FitLineContourXld(
                        contour, "tukey", -1, 0, 5, 2,
                        out hrB, out hcB, out hrE, out hcE, out nr, out nc, out df);
                }
                catch (Exception fitEx)
                {
                    config.LastTeachSucceeded = false;
                    error = "Horizontal line fit failed: " + fitEx.Message;
                    return false;
                }

                // 수직 라인 × 수평 결합선 IntersectionLl
                HTuple curRow, curCol, isOverlapping;
                HOperatorSet.IntersectionLl(
                    vrB, vcB, vrE, vcE,
                    hrB, hcB, hrE, hcE,
                    out curRow, out curCol, out isOverlapping);

                // 교점 중첩/평행 감지
                if (isOverlapping.I == 1)
                {
                    config.LastTeachSucceeded = false;
                    error = "Intersection undefined: lines are collinear";
                    return false;
                }
                if (double.IsInfinity(curRow.D) || double.IsInfinity(curCol.D) ||
                    double.IsNaN(curRow.D) || double.IsNaN(curCol.D))
                {
                    config.LastTeachSucceeded = false;
                    error = "Intersection undefined: lines are parallel";
                    return false;
                }

                // 기준값 저장
                config.RefOriginRow = curRow.D;
                config.RefOriginCol = curCol.D;
                config.RefAngleRad  = Math.Atan2(hrE.D - hrB.D, hcE.D - hcB.D);
                config.IsConfigured = true;

                // 검출 라인 오버레이 필드 재사용 (Line1Detected = 수직 검출선, Line2Detected = 수평 결합선)
                config.Line1Detected_RBegin = vrB;
                config.Line1Detected_CBegin = vcB;
                config.Line1Detected_REnd   = vrE;
                config.Line1Detected_CEnd   = vcE;
                config.Line2Detected_RBegin = hrB.D;
                config.Line2Detected_CBegin = hcB.D;
                config.Line2Detected_REnd   = hrE.D;
                config.Line2Detected_CEnd   = hcE.D;
                config.LastTeachSucceeded   = true;

                // Req 5d 방향 정합성 검증 (VerticalTwoHorizontal: 수직 phi 는 검출된 수직 라인 Atan2)
                double vertPhiDetected = Math.Atan2(vrE - vrB, vcE - vcB);
                string angleError;
                if (!ValidateHorizontalVerticalAngles(config.RefAngleRad, vertPhiDetected, out angleError))
                {
                    config.LastTeachSucceeded = false;
                    error = angleError;
                    return false;
                }
                return true;
            }
            catch (Exception ex)
            {
                if (config != null) { config.LastTeachSucceeded = false; }
                error = ex.Message;
                return false;
            }
            finally
            {
                // 단일 contour 만 dispose (contourA/contourB/concatContour 제거)
                if (contour != null) { try { contour.Dispose(); } catch { } }
            }
        }

        // VerticalTwoHorizontalDualImage Teach 분기.
        //  본문 = TryTeachVerticalTwoHorizontal 100% 복제 + ROI 별 이미지 입력 분기 (Vertical=imageVertical, Horizontal A/B=imageHorizontal).
        //  나머지 로직 (Vertical TryFindLine / Horizontal A/B TryExtractEdgePoints / totalEdges 가드 / TupleConcat / FitLineContourXld / IntersectionLl / 기준값 저장 / Line1Detected/Line2Detected transient / ValidateHorizontalVerticalAngles 게이트) 모두 동일.
        private bool TryTeachVerticalTwoHorizontalDualImage(HImage imageHorizontal, HImage imageVertical, DatumConfig config, out string error)
        {
            error = null;

            HObject contour = null;

            try
            {
                HTuple imageVerticalWidth, imageVerticalHeight;
                imageVertical.GetImageSize(out imageVerticalWidth, out imageVerticalHeight);

                // Vertical 라인 검출 — imageVertical 사용 (D-34-01)
                double vrB, vcB, vrE, vcE;
                HTuple vertRawRows, vertRawCols;
                string lineError;
                if (!TryFindLine(
                        imageVertical, imageVerticalWidth, imageVerticalHeight,
                        config.Vertical_Row, config.Vertical_Col, config.Vertical_Phi,
                        config.Vertical_Length1, config.Vertical_Length2,
                        config.Vertical_Sigma, config.Vertical_EdgeThreshold, config.Vertical_EdgePolarity,
                        config.Vertical_EdgeDirection, config.Vertical_EdgeSelection,
                        config.Vertical_EdgeSampleCount, config.Vertical_EdgeTrimCount,
                        out vrB, out vcB, out vrE, out vcE,
                        out vertRawRows, out vertRawCols,
                        out lineError,
                        "Vertical"))
                {
                    config.LastTeachSucceeded = false;
                    error = "Vertical line fit failed: " + lineError;
                    return false;
                }
                config.Vertical_DetectedEdgeRows = vertRawRows;
                config.Vertical_DetectedEdgeCols = vertRawCols;

                HTuple imageHorizontalWidth, imageHorizontalHeight;
                imageHorizontal.GetImageSize(out imageHorizontalWidth, out imageHorizontalHeight);

                // Horizontal A — imageHorizontal 사용 (D-34-01)
                HTuple rowEdgeA, colEdgeA;
                string edgeErrorA;
                if (!TryExtractEdgePoints(
                        imageHorizontal, imageHorizontalWidth, imageHorizontalHeight,
                        config.Horizontal_A_Row, config.Horizontal_A_Col, config.Horizontal_A_Phi,
                        config.Horizontal_A_Length1, config.Horizontal_A_Length2,
                        config.Horizontal_A_Sigma, config.Horizontal_A_EdgeThreshold, config.Horizontal_A_EdgePolarity,
                        config.Horizontal_A_EdgeDirection, config.Horizontal_A_EdgeSelection,
                        config.Horizontal_A_EdgeSampleCount, config.Horizontal_A_EdgeTrimCount,
                        out rowEdgeA, out colEdgeA, out edgeErrorA,
                        "Horizontal_A"))
                {
                    config.LastTeachSucceeded = false;
                    error = "Horizontal line fit failed: " + edgeErrorA;
                    return false;
                }
                config.Horizontal_A_DetectedEdgeRows = rowEdgeA;
                config.Horizontal_A_DetectedEdgeCols = colEdgeA;

                // Horizontal B — imageHorizontal 사용 (D-34-01)
                HTuple rowEdgeB, colEdgeB;
                string edgeErrorB;
                if (!TryExtractEdgePoints(
                        imageHorizontal, imageHorizontalWidth, imageHorizontalHeight,
                        config.Horizontal_B_Row, config.Horizontal_B_Col, config.Horizontal_B_Phi,
                        config.Horizontal_B_Length1, config.Horizontal_B_Length2,
                        config.Horizontal_B_Sigma, config.Horizontal_B_EdgeThreshold, config.Horizontal_B_EdgePolarity,
                        config.Horizontal_B_EdgeDirection, config.Horizontal_B_EdgeSelection,
                        config.Horizontal_B_EdgeSampleCount, config.Horizontal_B_EdgeTrimCount,
                        out rowEdgeB, out colEdgeB, out edgeErrorB,
                        "Horizontal_B"))
                {
                    config.LastTeachSucceeded = false;
                    error = "Horizontal line fit failed: " + edgeErrorB;
                    return false;
                }
                config.Horizontal_B_DetectedEdgeRows = rowEdgeB;
                config.Horizontal_B_DetectedEdgeCols = colEdgeB;

                int totalEdges = rowEdgeA.TupleLength() + rowEdgeB.TupleLength();
                if (totalEdges < MIN_HORIZONTAL_EDGES)
                {
                    config.LastTeachSucceeded = false;
                    error = "Horizontal line fit failed: insufficient edges (" + totalEdges + ")";
                    return false;
                }

                HTuple allRows = rowEdgeA.TupleConcat(rowEdgeB);
                HTuple allCols = colEdgeA.TupleConcat(colEdgeB);
                HOperatorSet.GenContourPolygonXld(out contour, allRows, allCols);

                HTuple hrB, hcB, hrE, hcE, nr, nc, df;
                try
                {
                    HOperatorSet.FitLineContourXld(
                        contour, "tukey", -1, 0, 5, 2,
                        out hrB, out hcB, out hrE, out hcE, out nr, out nc, out df);
                }
                catch (Exception fitEx)
                {
                    config.LastTeachSucceeded = false;
                    error = "Horizontal line fit failed: " + fitEx.Message;
                    return false;
                }

                HTuple curRow, curCol, isOverlapping;
                HOperatorSet.IntersectionLl(
                    vrB, vcB, vrE, vcE,
                    hrB, hcB, hrE, hcE,
                    out curRow, out curCol, out isOverlapping);

                if (isOverlapping.I == 1)
                {
                    config.LastTeachSucceeded = false;
                    error = "Intersection undefined: lines are collinear";
                    return false;
                }
                if (double.IsInfinity(curRow.D) || double.IsInfinity(curCol.D) ||
                    double.IsNaN(curRow.D) || double.IsNaN(curCol.D))
                {
                    config.LastTeachSucceeded = false;
                    error = "Intersection undefined: lines are parallel";
                    return false;
                }

                // 기준값 저장
                config.RefOriginRow = curRow.D;
                config.RefOriginCol = curCol.D;
                config.RefAngleRad  = Math.Atan2(hrE.D - hrB.D, hcE.D - hcB.D);
                config.IsConfigured = true;

                // 검출 라인 오버레이 (Line1Detected = 수직 검출선, Line2Detected = 수평 결합선)
                config.Line1Detected_RBegin = vrB;
                config.Line1Detected_CBegin = vcB;
                config.Line1Detected_REnd   = vrE;
                config.Line1Detected_CEnd   = vcE;
                config.Line2Detected_RBegin = hrB.D;
                config.Line2Detected_CBegin = hcB.D;
                config.Line2Detected_REnd   = hrE.D;
                config.Line2Detected_CEnd   = hcE.D;
                config.LastTeachSucceeded   = true;

                // Req 5d 방향 정합성 검증 (VerticalTwoHorizontalDualImage: 수직 phi 는 검출된 수직 라인 Atan2)
                double vertPhiDetected = Math.Atan2(vrE - vrB, vcE - vcB);
                string angleError;
                if (!ValidateHorizontalVerticalAngles(config.RefAngleRad, vertPhiDetected, out angleError))
                {
                    config.LastTeachSucceeded = false;
                    error = angleError;
                    return false;
                }
                return true;
            }
            catch (Exception ex)
            {
                if (config != null) { config.LastTeachSucceeded = false; }
                error = ex.Message;
                return false;
            }
            finally
            {
                if (contour != null) { try { contour.Dispose(); } catch { } }
            }
        }

        // 수평선 phi 방향 + 수평/수직 직각성 검증 게이트
        //  CircleTwoHorizontal / VerticalTwoHorizontal 공통. TwoLineIntersect 는 무효(호출 안 함).
        //  horizPhiRad = 수평 결합선 Atan2, vertPhiRad = 수직 가상선(Circle) 또는 검출 수직선(Vertical) Atan2.
        //  실패 시 error 에 SPEC AC 리터럴 반환. NaN/Infinity 입력은 수평 체크에서 자연히 걸러짐(Abs > tol).
        private static bool ValidateHorizontalVerticalAngles(
            double horizPhiRad, double vertPhiRad, out string error)
        {
            // 수평 phi 를 [-90°, +90°] 로 normalize 후 절댓값 비교
            double horizDeg = Math.Abs(horizPhiRad * 180.0 / Math.PI);
            if (horizDeg > 90.0) horizDeg = 180.0 - horizDeg;
            if (horizDeg > HORIZONTAL_TOLERANCE_DEG)
            {
                error = "Horizontal line orientation out of range: "
                      + horizDeg.ToString("F1")
                      + " deg (expected +/-"
                      + HORIZONTAL_TOLERANCE_DEG.ToString("F1")
                      + " deg)";
                return false;
            }

            // 수평선과 수직선 사이 각도 = |phi_h - phi_v| 를 [0°, 180°) 로 정규화 → 90° 와의 편차
            double deltaDeg = Math.Abs((horizPhiRad - vertPhiRad) * 180.0 / Math.PI);
            while (deltaDeg >= 180.0) deltaDeg -= 180.0;
            double perpErr = Math.Abs(deltaDeg - 90.0);
            if (perpErr > PERPENDICULAR_TOLERANCE_DEG)
            {
                error = "Horizontal/Vertical perpendicularity violated: delta="
                      + deltaDeg.ToString("F1")
                      + " deg (expected 90 +/-"
                      + PERPENDICULAR_TOLERANCE_DEG.ToString("F1")
                      + " deg)";
                return false;
            }

            error = null;
            return true;
        }

        /// <summary>
        /// Rectangle2 ROI 내에서 에지 포인트를 검출하고 FitLineContourXld로 라인을 피팅한다.
        /// T-04-02: MeasureHandle은 AppendEdgePointsFromStrip 내부 finally 블록에서 strip별로 해제된다.
        /// </summary>
        // per-ROI 에지 파라미터 적용 (direction/sampleCount/trimCount 추가)
        //  이전 호출부의 글로벌 sigma/threshold/polarity 는 ROI 별 값으로 교체됨.
        // strip-loop MeasurePos 누적 패턴으로 전면 교체.
        //  SampleCount = strip 개수 (이전: 최소 에지 게이트). direction = strip 분할 방향.
        //  참조: C:\Info\Project\DatumMeasure\DatumMeasure\Algorithms\MeasurementAlgorithm.cs
        // 기존 SmallestRectangle2 자동 phi 정당화 주석 제거 (실 데이터 UAT 에서 BtoT/TtoB 부호 구분 누락 확인).
        //  direction → measurePhi 명시 매핑은 AppendEdgePointsFromStrip 안으로 이동 (CANONICAL: MeasurementAlgorithm.cs:130-178).
        // raw edge points 외부 노출 (out HTuple edgeRowsOut/edgeColsOut)
        //  caller 가 DatumConfig 의 ROI 별 DetectedEdgeRows/Cols 에 대입 → RenderDatumOverlay 가 점 마커 렌더.
        //  edge 가 0개 검출되면 빈 HTuple 반환 (length 0 → RenderRawEdgePoints 에서 no-op).
        private bool TryFindLine(
            HImage image, HTuple imageWidth, HTuple imageHeight,
            double roiRow, double roiCol, double roiPhi, double roiLength1, double roiLength2,
            double sigma, int threshold, string polarity,
            string direction, string selection,
            int sampleCount, int trimCount,
            out double lineRowBegin, out double lineColBegin,
            out double lineRowEnd, out double lineColEnd,
            out HTuple edgeRowsOut, out HTuple edgeColsOut,
            out string error,
            string roiLabel)
        {
            lineRowBegin = 0;
            lineColBegin = 0;
            lineRowEnd = 0;
            lineColEnd = 0;
            edgeRowsOut = new HTuple();
            edgeColsOut = new HTuple();
            error = null;

            // sanity clamp: PropertyGrid 0/"" 누락 방어 (EnsurePerRoiDefaults 이후에도 이중 방어)
            if (sigma < 0.4) sigma = 1.0;          // Halcon MeasurePos requires sigma >= 0.4
            if (threshold <= 0) threshold = 20;
            if (string.IsNullOrEmpty(polarity)) polarity = "all";
            if (string.IsNullOrEmpty(direction)) direction = "LtoR";
            if (string.IsNullOrEmpty(selection)) selection = "First";
            if (sampleCount < 0) sampleCount = 0;
            if (trimCount < 0) trimCount = 0;

            // bounding box 계산 (Phi=0 저장 규약 기준, halfW/halfH = Length1/Length2)
            double halfW    = roiLength1;
            double halfH    = roiLength2;
            double top      = roiRow - halfH;
            double bottom   = roiRow + halfH;
            double left     = roiCol - halfW;
            double right    = roiCol + halfW;
            double widthPx  = right - left;
            double heightPx = bottom - top;

            // LtoR/RtoL = horizontal strips (row-sliced), TtoB/BtoT = vertical strips (col-sliced)
            bool scanHorizontal = (direction != "TtoB" && direction != "BtoT");
            // sentinel 0 → 기본 20 strips. ?: → 명시 if/else
            int stripCount = 20;
            if (sampleCount > 0) stripCount = sampleCount;
            if (stripCount < 1) stripCount = 1;

            // Trace 로그 인자 임시변수화 (roiLabel null 가드 + scanHorizontal 분기 명시)
            string lbl = "?";
            if (roiLabel != null) lbl = roiLabel;
            string scanLabel = "vertical";
            if (scanHorizontal) scanLabel = "horizontal";

            Logging.PrintLog((int)ELogType.Trace,
                string.Format("[Datum.{0}] strip-loop: bounds top={1:F1} left={2:F1} bottom={3:F1} right={4:F1}  scan={5}  stripCount={6}  sigma={7:F2} threshold={8} polarity={9}",
                    lbl, top, left, bottom, right,
                    scanLabel,
                    stripCount, sigma, threshold, polarity));

            HTuple allRows = new HTuple();
            HTuple allCols = new HTuple();

            try
            {
                if (scanHorizontal)
                {
                    for (int i = 0; i < stripCount; i++)
                    {
                        double r1 = top + (i * heightPx / stripCount);
                        double r2 = top + ((i + 1) * heightPx / stripCount);
                        AppendEdgePointsFromStrip(
                            image, r1, left, r2, right,
                            imageWidth, imageHeight,
                            sigma, threshold, polarity,
                            direction, selection,
                            ref allRows, ref allCols,
                            roiLabel);
                    }
                }
                else
                {
                    for (int i = 0; i < stripCount; i++)
                    {
                        double c1 = left + (i * widthPx / stripCount);
                        double c2 = left + ((i + 1) * widthPx / stripCount);
                        AppendEdgePointsFromStrip(
                            image, top, c1, bottom, c2,
                            imageWidth, imageHeight,
                            sigma, threshold, polarity,
                            direction, selection,
                            ref allRows, ref allCols,
                            roiLabel);
                    }
                }

                int edgeCount = allRows.TupleLength();
                Logging.PrintLog((int)ELogType.Trace,
                    string.Format("[Datum.{0}] strip-loop accumulated {1} edge points across {2} strips",
                        lbl, edgeCount, stripCount));

                // TrimCount: 누적된 모든 점 중 양 끝 제거 (FitLineContourXld 입력 정제)
                if (trimCount > 0 && edgeCount > 2 * trimCount + 1)
                {
                    HTuple trimmedR = allRows.TupleSelectRange(trimCount, edgeCount - trimCount - 1);
                    HTuple trimmedC = allCols.TupleSelectRange(trimCount, edgeCount - trimCount - 1);
                    allRows    = trimmedR;
                    allCols    = trimmedC;
                    edgeCount  = allRows.TupleLength();
                    Logging.PrintLog((int)ELogType.Trace,
                        string.Format("[Datum.{0}] trimmed {1} from each end -> {2} edges remain",
                            lbl, trimCount, edgeCount));
                }

                if (edgeCount < 2)
                {
                    error = string.Format(
                        "[{0}] insufficient edges across {1} strips: got {2} (need >=2). sigma={3:F2} threshold={4} polarity={5} scan={6}",
                        lbl, stripCount, edgeCount, sigma, threshold, polarity,
                        scanLabel);
                    Logging.PrintLog((int)ELogType.Trace, error);
                    return false;
                }

                // 라인 피팅 (FitLineContourXld, tukey 로버스트 추정)
                HObject contour = null;
                try
                {
                    HOperatorSet.GenContourPolygonXld(out contour, allRows, allCols);
                    HTuple lr1, lc1, lr2, lc2, nr, nc, df;
                    HOperatorSet.FitLineContourXld(
                        contour, "tukey", -1, 0, 5, 2,
                        out lr1, out lc1, out lr2, out lc2, out nr, out nc, out df);

                    lineRowBegin = lr1.D;
                    lineColBegin = lc1.D;
                    lineRowEnd   = lr2.D;
                    lineColEnd   = lc2.D;
                    // raw 점 외부 노출 (성공 시에만; 실패는 빈 HTuple 유지)
                    edgeRowsOut = allRows;
                    edgeColsOut = allCols;
                    return true;
                }
                finally
                {
                    if (contour != null) { try { contour.Dispose(); } catch { } }
                }
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        // 단일 Rectangle2 ROI에서 에지점만 추출 (라인 피팅 전 단계). 수평 2-ROI concat 피팅용.
        //  TryFindLine 과 달리 FitLineContourXld 단계를 생략하고 raw edge tuples 반환.
        // per-ROI 에지 파라미터 적용 (direction/sampleCount/trimCount 추가)
        // strip-loop MeasurePos 누적 패턴으로 전면 교체 (TryFindLine 과 동일 구조).
        private bool TryExtractEdgePoints(
            HImage image, HTuple imageWidth, HTuple imageHeight,
            double roiRow, double roiCol, double roiPhi, double roiLength1, double roiLength2,
            double sigma, int threshold, string polarity,
            string direction, string selection,
            int sampleCount, int trimCount,
            out HTuple rowEdge, out HTuple colEdge,
            out string error,
            string roiLabel)
        {
            rowEdge = new HTuple();
            colEdge = new HTuple();
            error = null;

            // sanity clamp (TryFindLine 과 동일 방어)
            if (sigma < 0.4) sigma = 1.0;
            if (threshold <= 0) threshold = 20;
            if (string.IsNullOrEmpty(polarity)) polarity = "all";
            if (string.IsNullOrEmpty(direction)) direction = "LtoR";
            if (string.IsNullOrEmpty(selection)) selection = "First";
            if (sampleCount < 0) sampleCount = 0;
            if (trimCount < 0) trimCount = 0;

            // bounding box 계산 (Phi=0 저장 규약 기준)
            double halfW    = roiLength1;
            double halfH    = roiLength2;
            double top      = roiRow - halfH;
            double bottom   = roiRow + halfH;
            double left     = roiCol - halfW;
            double right    = roiCol + halfW;
            double widthPx  = right - left;
            double heightPx = bottom - top;

            // LtoR/RtoL = horizontal strips, TtoB/BtoT = vertical strips
            bool scanHorizontal = (direction != "TtoB" && direction != "BtoT");
            // sentinel 0 → 기본 20 strips. ?: → 명시 if/else
            int stripCount = 20;
            if (sampleCount > 0) stripCount = sampleCount;
            if (stripCount < 1) stripCount = 1;

            // Trace 로그 인자 임시변수화 (roiLabel null 가드 + scanHorizontal 분기 명시)
            string lbl = "?";
            if (roiLabel != null) lbl = roiLabel;
            string scanLabel = "vertical";
            if (scanHorizontal) scanLabel = "horizontal";

            Logging.PrintLog((int)ELogType.Trace,
                string.Format("[Datum.{0}] strip-loop(extract): bounds top={1:F1} left={2:F1} bottom={3:F1} right={4:F1}  scan={5}  stripCount={6}  sigma={7:F2} threshold={8} polarity={9}",
                    lbl, top, left, bottom, right,
                    scanLabel,
                    stripCount, sigma, threshold, polarity));

            HTuple allRows = new HTuple();
            HTuple allCols = new HTuple();

            try
            {
                if (scanHorizontal)
                {
                    for (int i = 0; i < stripCount; i++)
                    {
                        double r1 = top + (i * heightPx / stripCount);
                        double r2 = top + ((i + 1) * heightPx / stripCount);
                        AppendEdgePointsFromStrip(
                            image, r1, left, r2, right,
                            imageWidth, imageHeight,
                            sigma, threshold, polarity,
                            direction, selection,
                            ref allRows, ref allCols,
                            roiLabel);
                    }
                }
                else
                {
                    for (int i = 0; i < stripCount; i++)
                    {
                        double c1 = left + (i * widthPx / stripCount);
                        double c2 = left + ((i + 1) * widthPx / stripCount);
                        AppendEdgePointsFromStrip(
                            image, top, c1, bottom, c2,
                            imageWidth, imageHeight,
                            sigma, threshold, polarity,
                            direction, selection,
                            ref allRows, ref allCols,
                            roiLabel);
                    }
                }

                int edgeCount = allRows.TupleLength();
                Logging.PrintLog((int)ELogType.Trace,
                    string.Format("[Datum.{0}] strip-loop(extract) accumulated {1} edge points across {2} strips",
                        lbl, edgeCount, stripCount));

                // TrimCount: 누적된 전체 점 양 끝 제거
                if (trimCount > 0 && edgeCount > 2 * trimCount + 1)
                {
                    HTuple trimmedR = allRows.TupleSelectRange(trimCount, edgeCount - trimCount - 1);
                    HTuple trimmedC = allCols.TupleSelectRange(trimCount, edgeCount - trimCount - 1);
                    allRows   = trimmedR;
                    allCols   = trimmedC;
                    edgeCount = allRows.TupleLength();
                    Logging.PrintLog((int)ELogType.Trace,
                        string.Format("[Datum.{0}] trimmed {1} from each end -> {2} edges remain",
                            lbl, trimCount, edgeCount));
                }

                rowEdge = allRows;
                colEdge = allCols;

                // caller 가 concat 후 MIN_HORIZONTAL_EDGES 별도 확인하므로 1개 이상이면 성공으로 반환
                if (edgeCount < 1)
                {
                    error = string.Format(
                        "[{0}] no edges found across {1} strips. sigma={2:F2} threshold={3} polarity={4} scan={5}",
                        lbl, stripCount, sigma, threshold, polarity,
                        scanLabel);
                    Logging.PrintLog((int)ELogType.Trace, error);
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        // 단일 strip 에서 MeasurePos 실행 후 edge 점 누적.
        //  참조: C:\Info\Project\DatumMeasure\DatumMeasure\Algorithms\MeasurementAlgorithm.cs AppendEdgePointsFromStrip.
        //  strip 실패(빈 결과 / 예외)는 swallow — 한 strip 실패가 전체 ROI 를 중단시키지 않음.
        // direction → measurePhi 4-way 명시 매핑 (BtoT/TtoB 부호 구분), selection 인자화.
        //  CANONICAL refs: MeasurementAlgorithm.cs:130-178, FAIEdgeMeasurementService.cs:82-106, VisionAlgorithmService.cs:63-72.
        //  SmallestRectangle2 의 rp 자동 도출은 사용자 의도(BtoT vs TtoB) 를 구분 못 함 → polarity 의미 뒤집힘.
        private void AppendEdgePointsFromStrip(
            HImage image,
            double row1, double col1, double row2, double col2,
            HTuple imageWidth, HTuple imageHeight,
            double sigma, int threshold, string polarity,
            string direction, string selection,
            ref HTuple allRows, ref HTuple allCols,
            string roiLabel)
        {
            // direction → measurePhi (CANONICAL: MeasurementAlgorithm.cs:130-178)
            double measurePhi;
            if (string.Equals(direction, "TtoB", StringComparison.OrdinalIgnoreCase))      measurePhi = -Math.PI / 2.0;
            else if (string.Equals(direction, "BtoT", StringComparison.OrdinalIgnoreCase)) measurePhi = +Math.PI / 2.0;
            else if (string.Equals(direction, "RtoL", StringComparison.OrdinalIgnoreCase)) measurePhi = Math.PI;
            else                                                                            measurePhi = 0.0;

            // selection (PascalCase) → Halcon MeasurePos 인자 (lower). chained ?: → if/else (CANONICAL: MeasurementAlgorithm.cs:178 의미 보존)
            string selectionLower = "first";
            if (string.Equals(selection, "Last", StringComparison.OrdinalIgnoreCase)) selectionLower = "last";
            else if (string.Equals(selection, "All",  StringComparison.OrdinalIgnoreCase)) selectionLower = "all";

            HObject stripRegion = null;
            HTuple measureHandle = null;
            try
            {
                HOperatorSet.GenRectangle1(out stripRegion, row1, col1, row2, col2);

                HTuple rr, rc, rp, rh, rw;
                HOperatorSet.SmallestRectangle2(stripRegion, out rr, out rc, out rp, out rh, out rw);
                // rp(자동 phi) 는 더 이상 사용 안 함; measurePhi 만 사용. rh/rw 는 유효 (strip 의 half-W/H).

                HOperatorSet.GenMeasureRectangle2(
                    rr, rc, measurePhi, rh, rw,
                    imageWidth, imageHeight, "nearest_neighbor",
                    out measureHandle);

                HTuple edgeRows, edgeCols, amp, dist;
                HOperatorSet.MeasurePos(
                    image, measureHandle, sigma, threshold,
                    polarity, selectionLower,
                    out edgeRows, out edgeCols, out amp, out dist);

                // Trace 로그 강화: measurePhi (deg) + selection 노출. null-coalesce → 임시변수 + null 체크 (P-1)
                string lbl = "?";
                if (roiLabel != null) lbl = roiLabel;
                string dirLabel = "?";
                if (direction != null) dirLabel = direction;
                Logging.PrintLog((int)ELogType.Trace,
                    string.Format("[Datum.{0}] strip MeasurePos: dir={1} measurePhi={2:F1}deg sel={3} edges={4}",
                        lbl, dirLabel, measurePhi * 180.0 / Math.PI, selectionLower,
                        edgeRows.TupleLength()));

                if (edgeRows.TupleLength() <= 0 || edgeCols.TupleLength() <= 0)
                {
                    return;
                }

                HOperatorSet.TupleConcat(allRows, edgeRows, out allRows);
                HOperatorSet.TupleConcat(allCols, edgeCols, out allCols);
            }
            catch (Exception ex)
            {
                // 빈 catch 진단 강화: 라벨 + 예외 메시지 (per-strip swallow 정책 유지). null-coalesce → 임시변수 + null 체크 (P-1)
                try
                {
                    string lblCatch = "?";
                    if (roiLabel != null) lblCatch = roiLabel;
                    Logging.PrintLog((int)ELogType.Trace,
                        string.Format("[Datum.{0}] strip swallowed: {1}", lblCatch, ex.Message));
                }
                catch { }
            }
            finally
            {
                if (measureHandle != null) { try { HOperatorSet.CloseMeasure(measureHandle); } catch { } }
                if (stripRegion != null)   { try { stripRegion.Dispose(); } catch { } }
            }
        }
    }
}
