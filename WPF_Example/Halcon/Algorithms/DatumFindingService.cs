//260409 hbk Phase 4: Datum 찾기 서비스 — D-15, D-16
using System;
using HalconDotNet;
using ReringProject.Sequence;
using ReringProject.Setting;  //260426 hbk Phase 13 D-PRP-HOTFIX — ELogType
using ReringProject.Utility;  //260426 hbk Phase 13 D-PRP-HOTFIX — Logging

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

        //260424 hbk Phase 13 D-10 — Req 5d 방향 정합성 임계각 (고정값; 사용자 튜닝 필요해지면 DatumConfig 필드화 Deferred)
        private const double HORIZONTAL_TOLERANCE_DEG    = 15.0;
        private const double PERPENDICULAR_TOLERANCE_DEG = 5.0;

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

            //260425 hbk Phase 13 D-PRP-06 — per-ROI sentinel → 글로벌 복제 (idempotent, 최초 1회 의미)
            config.EnsurePerRoiDefaults();

            //260503 hbk Phase 17 D-13 — 메서드 시작 시 reset (조기 return / catch 시 false 보장)
            config.LastFindSucceeded = false;

            //260503 hbk Phase 17 hotfix#7 — TryFindDatum 알고리즘 분기 추가 (Phase 12 D-04 누락 정정).
            //  기존: TwoLineIntersect 본문 inline → CTH/VTH 검사 시 Line1/Line2 빈 슬롯 접근 → 항상 NG.
            //  수정: AlgorithmTypeEnum switch 로 3-way 분기 (TryTeachDatum 와 동일 패턴).
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

        //260503 hbk Phase 17 hotfix#7 — 기존 TryFindDatum 본문 (TwoLineIntersect 검출 + transform 빌드) 을 private 으로 분리.
        //  로직 변경 0: Line1 / Line2 검출 → IntersectionLl → hom_mat2d (translate + rotate) → DetectedOrigin transient.
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
                HTuple line1RawRows, line1RawCols; //260425 hbk Phase 13 D-VIZ-03 — raw 점 수신용
                string lineError;
                //260425 hbk Phase 13 D-PRP-05 — Line1 per-ROI 에지 파라미터 사용 (글로벌 EdgeThreshold/Sigma/EdgePolarity → Line1_*)
                if (!TryFindLine(
                    image, imageWidth, imageHeight,
                    config.Line1_Row, config.Line1_Col, config.Line1_Phi, config.Line1_Length1, config.Line1_Length2,
                    config.Line1_Sigma, config.Line1_EdgeThreshold, config.Line1_EdgePolarity,
                    config.Line1_EdgeDirection, config.Line1_EdgeSelection,            //260429 hbk Phase 15 — EdgeSelection 전파 (DatumConfig → MeasurePos)
                    config.Line1_EdgeSampleCount, config.Line1_EdgeTrimCount,
                    out line1RowBegin, out line1ColBegin, out line1RowEnd, out line1ColEnd,
                    out line1RawRows, out line1RawCols, //260425 hbk Phase 13 D-VIZ-03 — raw 점 수신
                    out lineError,
                    "Line1")) //260426 hbk Phase 13 D-PRP-HOTFIX — ROI 레이블 추가 (진단 로그용)
                {
                    error = "Line1: " + lineError;
                    return false;
                }
                //260425 hbk Phase 13 D-VIZ-03 — Line1 raw 점 DatumConfig 기록 (런타임 진단/시각화)
                config.Line1_DetectedEdgeRows = line1RawRows;
                config.Line1_DetectedEdgeCols = line1RawCols;

                // Line2 검출
                double line2RowBegin, line2ColBegin, line2RowEnd, line2ColEnd;
                HTuple line2RawRows, line2RawCols; //260425 hbk Phase 13 D-VIZ-03 — raw 점 수신용
                //260425 hbk Phase 13 D-PRP-05 — Line2 per-ROI 에지 파라미터 사용
                if (!TryFindLine(
                    image, imageWidth, imageHeight,
                    config.Line2_Row, config.Line2_Col, config.Line2_Phi, config.Line2_Length1, config.Line2_Length2,
                    config.Line2_Sigma, config.Line2_EdgeThreshold, config.Line2_EdgePolarity,
                    config.Line2_EdgeDirection, config.Line2_EdgeSelection,            //260429 hbk Phase 15 — EdgeSelection 전파 (DatumConfig → MeasurePos)
                    config.Line2_EdgeSampleCount, config.Line2_EdgeTrimCount,
                    out line2RowBegin, out line2ColBegin, out line2RowEnd, out line2ColEnd,
                    out line2RawRows, out line2RawCols, //260425 hbk Phase 13 D-VIZ-03 — raw 점 수신
                    out lineError,
                    "Line2")) //260426 hbk Phase 13 D-PRP-HOTFIX — ROI 레이블 추가 (진단 로그용)
                {
                    error = "Line2: " + lineError;
                    return false;
                }
                //260425 hbk Phase 13 D-VIZ-03 — Line2 raw 점 DatumConfig 기록
                config.Line2_DetectedEdgeRows = line2RawRows;
                config.Line2_DetectedEdgeCols = line2RawCols;

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

                //260503 hbk Phase 17 D-13 — DetectedOrigin transient write-back (RenderDatumFindResult 입력)
                config.DetectedOriginRow = curRow.D; //260503 hbk Phase 17 D-13
                config.DetectedOriginCol = curCol.D; //260503 hbk Phase 17 D-13
                config.DetectedRefAngle  = curAngle; //260503 hbk Phase 17 D-13
                //260519 hbk Phase 31 hotfix#3 — 2차(수직) 기준선 = Line2 실제 검출 각도 (X축 측정 기준)
                config.DetectedRefAngle2 = Math.Atan2(
                    line2RowEnd - line2RowBegin, line2ColEnd - line2ColBegin); //260519 hbk Phase 31 hotfix#3
                //260509 hbk Phase 20 — 결과 메트릭 (검출 점 개수 합계 + 각도 deg). ?: → 명시 if/else (P-9 HTuple null 가드)
                int line1EdgeCount = 0;
                if (line1RawRows != null) line1EdgeCount = line1RawRows.TupleLength(); //260509 hbk Phase 20
                int line2EdgeCount = 0;
                if (line2RawRows != null) line2EdgeCount = line2RawRows.TupleLength(); //260509 hbk Phase 20
                config.DetectedEdgeCount = line1EdgeCount + line2EdgeCount; //260509 hbk Phase 20
                config.DetectedFitRMSE   = 0.0; //260503 hbk Phase 17 D-16 — fit RMSE 미수집 (placeholder)
                config.DetectedAngleDeg  = curAngle * 180.0 / System.Math.PI; //260503 hbk Phase 17 D-16
                config.LastFindSucceeded = true; //260503 hbk Phase 17 D-13

                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                HOperatorSet.HomMat2dIdentity(out transform);
                return false;
            }
        }

        //260503 hbk Phase 17 hotfix#7 — CircleTwoHorizontal Find 분기 (Phase 12 D-04 누락 정정).
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
                string circlePolarity = EdgeOptionLists.MapRadialDirectionToHalconPolarity(config.Circle_RadialDirection); //260508 hbk Phase 28 D-03 — inline ternary → helper 호출 (DRY)
                bool[] unusedStrips; //260505 hbk Phase 18 CO-05 — D-14: find 경로는 strip 색상 갱신 없음
                if (!visionSvc.TryFindCircleByPolarSampling(
                        image,
                        config.CircleROI_Row, config.CircleROI_Col, config.CircleROI_Radius,
                        config.Circle_PolarStepDeg, config.Circle_RectL1Ratio, config.Circle_RectL2Ratio,
                        config.Circle_Sigma, config.Circle_EdgeThreshold, circlePolarity,
                        config.Circle_EdgeSelection,
                        null,
                        out centerRow, out centerCol, out radius,
                        out circleEdgeRows, out circleEdgeCols,
                        out unusedStrips, //260505 hbk Phase 18 CO-05 — D-14: find 경로는 CircleStripSuccesses 갱신 안 함
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
                //260519 hbk Phase 31 hotfix#3 — 2차(수직) 기준선 = 원중심 통과 수직 가상선 (Step 5: centerRow±1, centerCol).
                //  방향벡터 (Δrow=+2, Δcol=0) → Atan2(2,0) = π/2 (순수 이미지-수직). X축 측정 기준.
                config.DetectedRefAngle2 = Math.PI / 2.0; //260519 hbk Phase 31 hotfix#3
                config.DetectedEdgeCount = circleEdgeRows.TupleLength() + totalEdges;
                config.DetectedFitRMSE   = 0.0;
                config.DetectedAngleDeg  = curAngle * 180.0 / System.Math.PI;

                //260503 hbk Phase 17 hotfix#8 — Visual transient 갱신 (검출 원 + 라인 외삽 + raw edge 점 새 위치 반영).
                //  hotfix#7 가 DetectedOrigin 만 갱신 → CTH 의 검출 원/center cross 가 teach 시점 위치에 박힘 →
                //  사용자가 "ROI 옮겨도 갱신 안 됨" 으로 인식. RenderDatumOverlay 가 사용하는 visual field 를 모두 갱신.
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

        //260503 hbk Phase 17 hotfix#7 — VerticalTwoHorizontal Find 분기 (Phase 12 D-04 누락 정정).
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
                //260519 hbk Phase 31 hotfix#3 — 2차(수직) 기준선 = 수직 에지 실제 검출 각도 (vertPhiDetected). X축 측정 기준.
                config.DetectedRefAngle2 = vertPhiDetected; //260519 hbk Phase 31 hotfix#3
                //260509 hbk Phase 20 — ?: → 명시 if/else (P-9 HTuple null 가드)
                int vertEdgeCount = 0;
                if (vertRawRows != null) vertEdgeCount = vertRawRows.TupleLength(); //260509 hbk Phase 20
                config.DetectedEdgeCount = vertEdgeCount + totalEdges; //260509 hbk Phase 20
                config.DetectedFitRMSE   = 0.0;
                config.DetectedAngleDeg  = curAngle * 180.0 / System.Math.PI;

                //260503 hbk Phase 17 hotfix#8 — Visual transient 갱신 (검출 라인 외삽 + raw edge 점 새 위치 반영).
                //  hotfix#7 가 DetectedOrigin 만 갱신 → 사용자가 "ROI 옮겨도 갱신 안 됨" 으로 인식.
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

            //260425 hbk Phase 13 D-PRP-06 — per-ROI sentinel → 글로벌 복제 (idempotent, 최초 1회 의미)
            config.EnsurePerRoiDefaults();

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

                HObject horect;
                HOperatorSet.GenRectangle2(out horect, config.Line1_Row, config.Line1_Col, config.Line1_Phi, config.Line1_Length1, config.Line1_Length2);

                string lineError;
                HTuple line1RawRows, line1RawCols; //260425 hbk Phase 13 D-VIZ-03 — raw 점 수신용
                //260425 hbk Phase 13 D-PRP-05 — Line1 per-ROI 에지 파라미터 사용 (글로벌 EdgeThreshold/Sigma/EdgePolarity → Line1_*)
                if (!TryFindLine(
                    image, imageWidth, imageHeight,
                    config.Line1_Row, config.Line1_Col, config.Line1_Phi, config.Line1_Length1, config.Line1_Length2,
                    config.Line1_Sigma, config.Line1_EdgeThreshold, config.Line1_EdgePolarity,
                    config.Line1_EdgeDirection, config.Line1_EdgeSelection,            //260429 hbk Phase 15 — EdgeSelection 전파 (DatumConfig → MeasurePos)
                    config.Line1_EdgeSampleCount, config.Line1_EdgeTrimCount,
                    out line1RowBegin, out line1ColBegin, out line1RowEnd, out line1ColEnd,
                    out line1RawRows, out line1RawCols, //260425 hbk Phase 13 D-VIZ-03 — raw 점 수신
                    out lineError,
                    "Line1")) //260426 hbk Phase 13 D-PRP-HOTFIX — ROI 레이블 추가 (진단 로그용)
                {
                    config.LastTeachSucceeded = false; //260423 hbk Phase 11 D-11
                    error = "Line1: " + lineError;
                    return false;
                }
                //260425 hbk Phase 13 D-VIZ-03 — Line1 raw 점 DatumConfig 기록
                config.Line1_DetectedEdgeRows = line1RawRows;
                config.Line1_DetectedEdgeCols = line1RawCols;

                // Line2 검출
                double line2RowBegin, line2ColBegin, line2RowEnd, line2ColEnd;
                HTuple line2RawRows, line2RawCols; //260425 hbk Phase 13 D-VIZ-03 — raw 점 수신용
                //260425 hbk Phase 13 D-PRP-05 — Line2 per-ROI 에지 파라미터 사용
                if (!TryFindLine(
                    image, imageWidth, imageHeight,
                    config.Line2_Row, config.Line2_Col, config.Line2_Phi, config.Line2_Length1, config.Line2_Length2,
                    config.Line2_Sigma, config.Line2_EdgeThreshold, config.Line2_EdgePolarity,
                    config.Line2_EdgeDirection, config.Line2_EdgeSelection,            //260429 hbk Phase 15 — EdgeSelection 전파 (DatumConfig → MeasurePos)
                    config.Line2_EdgeSampleCount, config.Line2_EdgeTrimCount,
                    out line2RowBegin, out line2ColBegin, out line2RowEnd, out line2ColEnd,
                    out line2RawRows, out line2RawCols, //260425 hbk Phase 13 D-VIZ-03 — raw 점 수신
                    out lineError,
                    "Line2")) //260426 hbk Phase 13 D-PRP-HOTFIX — ROI 레이블 추가 (진단 로그용)
                {
                    config.LastTeachSucceeded = false; //260423 hbk Phase 11 D-11
                    error = "Line2: " + lineError;
                    return false;
                }
                //260425 hbk Phase 13 D-VIZ-03 — Line2 raw 점 DatumConfig 기록
                config.Line2_DetectedEdgeRows = line2RawRows;
                config.Line2_DetectedEdgeCols = line2RawCols;

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

                //260426 hbk Phase 14-02 Req 2 — TwoLineIntersect 두 라인 각도 게이트
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
                        //260426 hbk Phase 14-02 SPEC AC literal — ASCII (deg, +/-)
                        error = "Two-line angle out of range: "
                              + deltaDeg.ToString("F1")
                              + " deg (expected 90 +/- "
                              + config.TwoLineAngleToleranceDeg.ToString("F1")
                              + " deg)";
                        return false;
                    }
                }

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

        //260423 hbk Phase 12 — CircleTwoHorizontal: 원 센터 수직 가상선 ∩ 수평 2-ROI concat 교점 (D-05/D-06/D-08/D-13/D-14)
        private bool TryTeachCircleTwoHorizontal(HImage image, DatumConfig config, out string error)
        {
            error = null;

            //260429 hbk #1405 fix — TryFindLine 패턴(833)으로 통일: 단일 contour 만 사용
            HObject contour = null;

            try
            {
                HTuple imageWidth, imageHeight;
                image.GetImageSize(out imageWidth, out imageHeight);

                //260426 hbk Phase 14-05 D-10 — TryFindCircle → TryFindCircleByPolarSampling 교체 (raw 점 반환 + 360° 분포)
                var visionSvc = new VisionAlgorithmService();
                double centerRow, centerCol, radius;
                HTuple circleEdgeRows, circleEdgeCols;
                string circleError;
                //260426 hbk Phase 14-05 D-11 — 진단 로그 (FAIL contingency 대비, PASS 후 주석 처리 가능)
                Logging.PrintLog((int)ELogType.Trace,
                    "TryTeachCircleTwoHorizontal: ROI=(" + config.CircleROI_Row + "," + config.CircleROI_Col + ",r=" + config.CircleROI_Radius + ") " +
                    "polar(step=" + config.Circle_PolarStepDeg + " L1=" + config.Circle_RectL1Ratio + " L2=" + config.Circle_RectL2Ratio + ")");
                //260503 hbk Phase 17 D-02 — Circle_RadialDirection ("Inward"/"Outward") → polarity ("positive"/"negative") override (EdgePolarity 무시)
                string circlePolarity = EdgeOptionLists.MapRadialDirectionToHalconPolarity(config.Circle_RadialDirection); //260508 hbk Phase 28 D-03 — inline ternary → helper 호출 (DRY)
                //260426 hbk Phase 14-05 — Circle per-ROI 에지 파라미터 + Polar sampling 파라미터 (14-04 신규 3 필드)
                bool[] circleStrips; //260505 hbk Phase 18 CO-05
                if (!visionSvc.TryFindCircleByPolarSampling(
                        image,
                        config.CircleROI_Row, config.CircleROI_Col, config.CircleROI_Radius,
                        config.Circle_PolarStepDeg, config.Circle_RectL1Ratio, config.Circle_RectL2Ratio,
                        config.Circle_Sigma, config.Circle_EdgeThreshold, circlePolarity, //260503 hbk Phase 17 D-02 — RadialDirection 우선 (EdgePolarity 미사용)
                        config.Circle_EdgeSelection, //260429 hbk Phase 15 — Circle_EdgeSelection 전파 (PropertyGrid → MeasurePos selection)
                        null, // teaching-phase identity transform (legacy 동일)
                        out centerRow, out centerCol, out radius,
                        out circleEdgeRows, out circleEdgeCols,
                        out circleStrips, //260505 hbk Phase 18 CO-05
                        out circleError))
                {
                    config.LastTeachSucceeded = false;
                    error = "Circle fit failed: " + circleError; //260423 hbk Phase 12 D-14 SPEC AC literal (Req 5c) 보존
                    return false;
                }
                config.CircleStripSuccesses = circleStrips; //260505 hbk Phase 18 CO-05 — per-strip 성공 여부 보관 (D-14: teach 경로만)

                config.CircleCenter_Row      = centerRow;
                config.CircleCenter_Col      = centerCol;
                config.CircleDetected_Radius = radius;
                //260426 hbk Phase 14-05 — Phase 13-05 D-VIZ-03 carry-over closure: raw 점 직접 반환 (이전엔 빈 HTuple)
                config.Circle_DetectedEdgeRows = circleEdgeRows;
                config.Circle_DetectedEdgeCols = circleEdgeCols;

                //260423 hbk Phase 12 D-06 — 수평 A ROI 에지점
                HTuple rowEdgeA, colEdgeA;
                string edgeErrorA;
                //260425 hbk Phase 13 D-PRP-05 — Horizontal_A per-ROI 에지 파라미터 사용
                if (!TryExtractEdgePoints(
                        image, imageWidth, imageHeight,
                        config.Horizontal_A_Row, config.Horizontal_A_Col, config.Horizontal_A_Phi,
                        config.Horizontal_A_Length1, config.Horizontal_A_Length2,
                        config.Horizontal_A_Sigma, config.Horizontal_A_EdgeThreshold, config.Horizontal_A_EdgePolarity,
                        config.Horizontal_A_EdgeDirection, config.Horizontal_A_EdgeSelection,            //260429 hbk Phase 15 — EdgeSelection 전파 (DatumConfig → MeasurePos)
                        config.Horizontal_A_EdgeSampleCount, config.Horizontal_A_EdgeTrimCount,
                        out rowEdgeA, out colEdgeA, out edgeErrorA,
                        "Horizontal_A")) //260426 hbk Phase 13 D-PRP-HOTFIX — ROI 레이블 추가 (진단 로그용)
                {
                    config.LastTeachSucceeded = false;
                    error = "Horizontal line fit failed: " + edgeErrorA; //260423 hbk Phase 12 D-14 (Req 5a)
                    return false;
                }
                //260425 hbk Phase 13 D-VIZ-03 — Horizontal_A raw 점 DatumConfig 기록
                config.Horizontal_A_DetectedEdgeRows = rowEdgeA;
                config.Horizontal_A_DetectedEdgeCols = colEdgeA;

                //260423 hbk Phase 12 D-06 — 수평 B ROI 에지점
                HTuple rowEdgeB, colEdgeB;
                string edgeErrorB;
                //260425 hbk Phase 13 D-PRP-05 — Horizontal_B per-ROI 에지 파라미터 사용
                if (!TryExtractEdgePoints(
                        image, imageWidth, imageHeight,
                        config.Horizontal_B_Row, config.Horizontal_B_Col, config.Horizontal_B_Phi,
                        config.Horizontal_B_Length1, config.Horizontal_B_Length2,
                        config.Horizontal_B_Sigma, config.Horizontal_B_EdgeThreshold, config.Horizontal_B_EdgePolarity,
                        config.Horizontal_B_EdgeDirection, config.Horizontal_B_EdgeSelection,            //260429 hbk Phase 15 — EdgeSelection 전파 (DatumConfig → MeasurePos)
                        config.Horizontal_B_EdgeSampleCount, config.Horizontal_B_EdgeTrimCount,
                        out rowEdgeB, out colEdgeB, out edgeErrorB,
                        "Horizontal_B")) //260426 hbk Phase 13 D-PRP-HOTFIX — ROI 레이블 추가 (진단 로그용)
                {
                    config.LastTeachSucceeded = false;
                    error = "Horizontal line fit failed: " + edgeErrorB; //260423 hbk Phase 12 D-14 (Req 5a)
                    return false;
                }
                //260425 hbk Phase 13 D-VIZ-03 — Horizontal_B raw 점 DatumConfig 기록
                config.Horizontal_B_DetectedEdgeRows = rowEdgeB;
                config.Horizontal_B_DetectedEdgeCols = colEdgeB;

                //260423 hbk Phase 12 D-15 — 수평 2-ROI concat 에지점 합계 임계값 검사
                int totalEdges = rowEdgeA.TupleLength() + rowEdgeB.TupleLength();
                if (totalEdges < MIN_HORIZONTAL_EDGES)
                {
                    config.LastTeachSucceeded = false;
                    error = "Horizontal line fit failed: insufficient edges (" + totalEdges + ")"; //260423 hbk Phase 12 D-14/D-15 SPEC AC literal (Req 5a)
                    return false;
                }

                //260429 hbk #1405 fix — TupleConcat + 단일 GenContourPolygonXld 로 변경 (TryFindLine 833 라인 패턴과 통일). 기존 ConcatObj 패턴은 length-2 fit 결과를 만들어 IntersectionLl 인자 길이 불일치 발생.
                HTuple allRows = rowEdgeA.TupleConcat(rowEdgeB); //260429 hbk #1405 fix — 행 에지 결합
                HTuple allCols = colEdgeA.TupleConcat(colEdgeB); //260429 hbk #1405 fix — 열 에지 결합
                HOperatorSet.GenContourPolygonXld(out contour, allRows, allCols); //260429 hbk #1405 fix — 단일 컨투어 생성

                HTuple hrB, hcB, hrE, hcE, nr, nc, df;
                try
                {
                    HOperatorSet.FitLineContourXld(
                        contour, "tukey", -1, 0, 5, 2, //260429 hbk #1405 fix — concatContour → contour
                        out hrB, out hcB, out hrE, out hcE, out nr, out nc, out df);
                }
                catch (Exception fitEx)
                {
                    config.LastTeachSucceeded = false;
                    error = "Horizontal line fit failed: " + fitEx.Message; //260423 hbk Phase 12 D-14 (Req 5a)
                    return false;
                }

                //260423 hbk Phase 12 D-08 — 수직 가상선 (centerRow ± 1.0, centerCol) × 수평 결합선 IntersectionLl
                HTuple curRow, curCol, isOverlapping;
                HOperatorSet.IntersectionLl(
                    centerRow - 1.0, centerCol, centerRow + 1.0, centerCol,
                    hrB, hcB, hrE, hcE,
                    out curRow, out curCol, out isOverlapping);

                //260423 hbk Phase 12 D-14/D-16 — 교점 중첩/평행 감지 (기존 TwoLineIntersect 와 동일 로직)
                if (isOverlapping.I == 1)
                {
                    config.LastTeachSucceeded = false;
                    error = "Intersection undefined: lines are collinear"; //260423 hbk Phase 12 D-14 SPEC AC literal (Req 5b)
                    return false;
                }
                if (double.IsInfinity(curRow.D) || double.IsInfinity(curCol.D) ||
                    double.IsNaN(curRow.D) || double.IsNaN(curCol.D))
                {
                    config.LastTeachSucceeded = false;
                    error = "Intersection undefined: lines are parallel"; //260423 hbk Phase 12 D-14 SPEC AC literal (Req 5b)
                    return false;
                }

                //260423 hbk Phase 12 — 기준값 저장
                config.RefOriginRow = curRow.D;
                config.RefOriginCol = curCol.D;
                config.RefAngleRad  = Math.Atan2(hrE.D - hrB.D, hcE.D - hcB.D); //260423 hbk SPEC Req 4 — 수평 결합선 방향각
                config.IsConfigured = true;

                //260423 hbk Phase 12 D-13 — 검출 라인 오버레이 필드 재사용 (Line1Detected = 수직 가상선 2점, Line2Detected = 수평 결합선)
                //260423 hbk  RenderDatumOverlay 의 LastTeachSucceeded 분기가 이 필드를 노랑/시안 선으로 렌더.
                const double crossHalf = 50.0; //260423 hbk Phase 12 — 수직 가상선 오버레이 가시 길이 (±50px)
                config.Line1Detected_RBegin = centerRow - crossHalf;
                config.Line1Detected_CBegin = centerCol;
                config.Line1Detected_REnd   = centerRow + crossHalf;
                config.Line1Detected_CEnd   = centerCol;
                config.Line2Detected_RBegin = hrB.D;
                config.Line2Detected_CBegin = hcB.D;
                config.Line2Detected_REnd   = hrE.D;
                config.Line2Detected_CEnd   = hcE.D;
                config.LastTeachSucceeded   = true;

                //260424 hbk Phase 13 D-09..D-12 — Req 5d 방향 정합성 검증 (CircleTwoHorizontal: 수직 가상선 phi = PI/2 고정)
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
                //260429 hbk #1405 fix — 단일 contour 만 dispose (contourA/contourB/concatContour 제거)
                if (contour != null) { try { contour.Dispose(); } catch { } }
            }
        }

        //260423 hbk Phase 12 — VerticalTwoHorizontal: 수직 ROI 라인 ∩ 수평 2-ROI concat 교점 (D-07/D-08/D-13/D-14)
        //260423 hbk  수직 ROI 는 Line1_* 필드 재사용 (D-07/D-12). CircleCenter_* / CircleDetected_Radius 는 건드리지 않음.
        private bool TryTeachVerticalTwoHorizontal(HImage image, DatumConfig config, out string error)
        {
            error = null;

            //260429 hbk #1405 fix — TryFindLine 패턴(833)으로 통일: 단일 contour 만 사용
            HObject contour = null;

            try
            {
                HTuple imageWidth, imageHeight;
                image.GetImageSize(out imageWidth, out imageHeight);

                //260426 hbk Phase 14-03 Req 3 — Vertical_* 슬롯 (의미적 분리, Line1_* 재사용 종료)
                double vrB, vcB, vrE, vcE;
                HTuple vertRawRows, vertRawCols; //260425 hbk Phase 13 D-VIZ-03 — 수직 라인 raw 점 수신용
                string lineError;
                //260426 hbk Phase 14-03 — Vertical per-ROI 에지 파라미터 사용 (수직 ROI 의미 분리)
                if (!TryFindLine(
                        image, imageWidth, imageHeight,
                        config.Vertical_Row, config.Vertical_Col, config.Vertical_Phi,
                        config.Vertical_Length1, config.Vertical_Length2,
                        config.Vertical_Sigma, config.Vertical_EdgeThreshold, config.Vertical_EdgePolarity,
                        config.Vertical_EdgeDirection, config.Vertical_EdgeSelection,            //260429 hbk Phase 15 — EdgeSelection 전파 (DatumConfig → MeasurePos)
                        config.Vertical_EdgeSampleCount, config.Vertical_EdgeTrimCount,
                        out vrB, out vcB, out vrE, out vcE,
                        out vertRawRows, out vertRawCols,
                        out lineError,
                        "Vertical")) //260426 hbk Phase 14-03 — 진단 로그 레이블 변경 ("Line1(vertical)" → "Vertical")
                {
                    config.LastTeachSucceeded = false;
                    error = "Vertical line fit failed: " + lineError; //260423 hbk Phase 12 D-14 SPEC AC literal (Req 5e)
                    return false;
                }
                //260426 hbk Phase 14-03 Req 3 — 수직 라인 raw 점 Vertical_DetectedEdge* 에 기록 (Line1_DetectedEdge* 슬롯 종료)
                config.Vertical_DetectedEdgeRows = vertRawRows;
                config.Vertical_DetectedEdgeCols = vertRawCols;

                //260423 hbk Phase 12 D-06 — 수평 A ROI 에지점
                HTuple rowEdgeA, colEdgeA;
                string edgeErrorA;
                //260425 hbk Phase 13 D-PRP-05 — Horizontal_A per-ROI 에지 파라미터 사용
                if (!TryExtractEdgePoints(
                        image, imageWidth, imageHeight,
                        config.Horizontal_A_Row, config.Horizontal_A_Col, config.Horizontal_A_Phi,
                        config.Horizontal_A_Length1, config.Horizontal_A_Length2,
                        config.Horizontal_A_Sigma, config.Horizontal_A_EdgeThreshold, config.Horizontal_A_EdgePolarity,
                        config.Horizontal_A_EdgeDirection, config.Horizontal_A_EdgeSelection,            //260429 hbk Phase 15 — EdgeSelection 전파 (DatumConfig → MeasurePos)
                        config.Horizontal_A_EdgeSampleCount, config.Horizontal_A_EdgeTrimCount,
                        out rowEdgeA, out colEdgeA, out edgeErrorA,
                        "Horizontal_A")) //260426 hbk Phase 13 D-PRP-HOTFIX — ROI 레이블 추가 (진단 로그용)
                {
                    config.LastTeachSucceeded = false;
                    error = "Horizontal line fit failed: " + edgeErrorA; //260423 hbk Phase 12 D-14 (Req 5a)
                    return false;
                }
                //260425 hbk Phase 13 D-VIZ-03 — Horizontal_A raw 점 DatumConfig 기록
                config.Horizontal_A_DetectedEdgeRows = rowEdgeA;
                config.Horizontal_A_DetectedEdgeCols = colEdgeA;

                //260423 hbk Phase 12 D-06 — 수평 B ROI 에지점
                HTuple rowEdgeB, colEdgeB;
                string edgeErrorB;
                //260425 hbk Phase 13 D-PRP-05 — Horizontal_B per-ROI 에지 파라미터 사용
                if (!TryExtractEdgePoints(
                        image, imageWidth, imageHeight,
                        config.Horizontal_B_Row, config.Horizontal_B_Col, config.Horizontal_B_Phi,
                        config.Horizontal_B_Length1, config.Horizontal_B_Length2,
                        config.Horizontal_B_Sigma, config.Horizontal_B_EdgeThreshold, config.Horizontal_B_EdgePolarity,
                        config.Horizontal_B_EdgeDirection, config.Horizontal_B_EdgeSelection,            //260429 hbk Phase 15 — EdgeSelection 전파 (DatumConfig → MeasurePos)
                        config.Horizontal_B_EdgeSampleCount, config.Horizontal_B_EdgeTrimCount,
                        out rowEdgeB, out colEdgeB, out edgeErrorB,
                        "Horizontal_B")) //260426 hbk Phase 13 D-PRP-HOTFIX — ROI 레이블 추가 (진단 로그용)
                {
                    config.LastTeachSucceeded = false;
                    error = "Horizontal line fit failed: " + edgeErrorB; //260423 hbk Phase 12 D-14 (Req 5a)
                    return false;
                }
                //260425 hbk Phase 13 D-VIZ-03 — Horizontal_B raw 점 DatumConfig 기록
                config.Horizontal_B_DetectedEdgeRows = rowEdgeB;
                config.Horizontal_B_DetectedEdgeCols = colEdgeB;

                //260423 hbk Phase 12 D-15 — 수평 2-ROI concat 에지점 합계 임계값 검사
                int totalEdges = rowEdgeA.TupleLength() + rowEdgeB.TupleLength();
                if (totalEdges < MIN_HORIZONTAL_EDGES)
                {
                    config.LastTeachSucceeded = false;
                    error = "Horizontal line fit failed: insufficient edges (" + totalEdges + ")"; //260423 hbk Phase 12 D-14/D-15 SPEC AC literal (Req 5a)
                    return false;
                }

                //260429 hbk #1405 fix — TupleConcat + 단일 GenContourPolygonXld 로 변경 (TryFindLine 833 라인 패턴과 통일). 기존 ConcatObj 패턴은 length-2 fit 결과를 만들어 IntersectionLl 인자 길이 불일치 발생.
                HTuple allRows = rowEdgeA.TupleConcat(rowEdgeB); //260429 hbk #1405 fix — 행 에지 결합
                HTuple allCols = colEdgeA.TupleConcat(colEdgeB); //260429 hbk #1405 fix — 열 에지 결합
                HOperatorSet.GenContourPolygonXld(out contour, allRows, allCols); //260429 hbk #1405 fix — 단일 컨투어 생성

                HTuple hrB, hcB, hrE, hcE, nr, nc, df;
                try
                {
                    HOperatorSet.FitLineContourXld(
                        contour, "tukey", -1, 0, 5, 2, //260429 hbk #1405 fix — concatContour → contour
                        out hrB, out hcB, out hrE, out hcE, out nr, out nc, out df);
                }
                catch (Exception fitEx)
                {
                    config.LastTeachSucceeded = false;
                    error = "Horizontal line fit failed: " + fitEx.Message; //260423 hbk Phase 12 D-14 (Req 5a)
                    return false;
                }

                //260423 hbk Phase 12 D-08 — 수직 라인 × 수평 결합선 IntersectionLl
                HTuple curRow, curCol, isOverlapping;
                HOperatorSet.IntersectionLl(
                    vrB, vcB, vrE, vcE,
                    hrB, hcB, hrE, hcE,
                    out curRow, out curCol, out isOverlapping);

                //260423 hbk Phase 12 D-14/D-16 — 교점 중첩/평행 감지
                if (isOverlapping.I == 1)
                {
                    config.LastTeachSucceeded = false;
                    error = "Intersection undefined: lines are collinear"; //260423 hbk Phase 12 D-14 SPEC AC literal (Req 5b)
                    return false;
                }
                if (double.IsInfinity(curRow.D) || double.IsInfinity(curCol.D) ||
                    double.IsNaN(curRow.D) || double.IsNaN(curCol.D))
                {
                    config.LastTeachSucceeded = false;
                    error = "Intersection undefined: lines are parallel"; //260423 hbk Phase 12 D-14 SPEC AC literal (Req 5b)
                    return false;
                }

                //260423 hbk Phase 12 — 기준값 저장
                config.RefOriginRow = curRow.D;
                config.RefOriginCol = curCol.D;
                config.RefAngleRad  = Math.Atan2(hrE.D - hrB.D, hcE.D - hcB.D); //260423 hbk SPEC Req 4 — 수평 결합선 방향각
                config.IsConfigured = true;

                //260423 hbk Phase 12 D-13 — 검출 라인 오버레이 필드 재사용 (Line1Detected = 수직 검출선, Line2Detected = 수평 결합선)
                config.Line1Detected_RBegin = vrB;
                config.Line1Detected_CBegin = vcB;
                config.Line1Detected_REnd   = vrE;
                config.Line1Detected_CEnd   = vcE;
                config.Line2Detected_RBegin = hrB.D;
                config.Line2Detected_CBegin = hcB.D;
                config.Line2Detected_REnd   = hrE.D;
                config.Line2Detected_CEnd   = hcE.D;
                config.LastTeachSucceeded   = true;

                //260424 hbk Phase 13 D-09..D-12 — Req 5d 방향 정합성 검증 (VerticalTwoHorizontal: 수직 phi 는 검출된 수직 라인 Atan2)
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
                //260429 hbk #1405 fix — 단일 contour 만 dispose (contourA/contourB/concatContour 제거)
                if (contour != null) { try { contour.Dispose(); } catch { } }
            }
        }

        //260424 hbk Phase 13 D-09..D-12 — 수평선 phi 방향 + 수평/수직 직각성 검증 게이트
        //  CircleTwoHorizontal / VerticalTwoHorizontal 공통. TwoLineIntersect 는 무효(호출 안 함).
        //  horizPhiRad = 수평 결합선 Atan2, vertPhiRad = 수직 가상선(Circle) 또는 검출 수직선(Vertical) Atan2.
        //  실패 시 error 에 SPEC AC 리터럴 반환. NaN/Infinity 입력은 수평 체크에서 자연히 걸러짐(Abs > tol).
        private static bool ValidateHorizontalVerticalAngles(
            double horizPhiRad, double vertPhiRad, out string error)
        {
            //260424 hbk Phase 13 D-09 — 수평 phi 를 [-90°, +90°] 로 normalize 후 절댓값 비교
            double horizDeg = Math.Abs(horizPhiRad * 180.0 / Math.PI);
            if (horizDeg > 90.0) horizDeg = 180.0 - horizDeg;
            if (horizDeg > HORIZONTAL_TOLERANCE_DEG)
            {
                error = "Horizontal line orientation out of range: "
                      + horizDeg.ToString("F1")
                      + " deg (expected +/-"
                      + HORIZONTAL_TOLERANCE_DEG.ToString("F1")
                      + " deg)"; //260424 hbk Phase 13 D-11 SPEC AC literal (Req 5d)
                return false;
            }

            //260424 hbk Phase 13 D-09 — 수평선과 수직선 사이 각도 = |phi_h - phi_v| 를 [0°, 180°) 로 정규화 → 90° 와의 편차
            double deltaDeg = Math.Abs((horizPhiRad - vertPhiRad) * 180.0 / Math.PI);
            while (deltaDeg >= 180.0) deltaDeg -= 180.0;
            double perpErr = Math.Abs(deltaDeg - 90.0);
            if (perpErr > PERPENDICULAR_TOLERANCE_DEG)
            {
                error = "Horizontal/Vertical perpendicularity violated: delta="
                      + deltaDeg.ToString("F1")
                      + " deg (expected 90 +/-"
                      + PERPENDICULAR_TOLERANCE_DEG.ToString("F1")
                      + " deg)"; //260424 hbk Phase 13 D-11 SPEC AC literal (Req 5d)
                return false;
            }

            error = null;
            return true;
        }

        /// <summary>
        /// Rectangle2 ROI 내에서 에지 포인트를 검출하고 FitLineContourXld로 라인을 피팅한다.
        /// T-04-02: MeasureHandle은 AppendEdgePointsFromStrip 내부 finally 블록에서 strip별로 해제된다.
        /// </summary>
        //260425 hbk Phase 13 D-PRP-04 — per-ROI 에지 파라미터 적용 (direction/sampleCount/trimCount 추가)
        //  이전 호출부의 글로벌 sigma/threshold/polarity 는 ROI 별 값으로 교체됨.
        //260426 hbk Phase 13 D-PRP-LOOP — strip-loop MeasurePos 누적 패턴으로 전면 교체.
        //  SampleCount = strip 개수 (이전: 최소 에지 게이트). direction = strip 분할 방향.
        //  참조: C:\Info\Project\DatumMeasure\DatumMeasure\Algorithms\MeasurementAlgorithm.cs
        //260429 hbk Phase 15 — 기존 SmallestRectangle2 자동 phi 정당화 주석 제거 (실 데이터 UAT 에서 BtoT/TtoB 부호 구분 누락 확인).
        //  direction → measurePhi 명시 매핑은 AppendEdgePointsFromStrip 안으로 이동 (CANONICAL: MeasurementAlgorithm.cs:130-178).
        //260425 hbk Phase 13 D-VIZ-02 — raw edge points 외부 노출 (out HTuple edgeRowsOut/edgeColsOut)
        //  caller 가 DatumConfig 의 ROI 별 DetectedEdgeRows/Cols 에 대입 → RenderDatumOverlay 가 점 마커 렌더.
        //  edge 가 0개 검출되면 빈 HTuple 반환 (length 0 → RenderRawEdgePoints 에서 no-op).
        private bool TryFindLine( //260409 hbk Phase 4
            HImage image, HTuple imageWidth, HTuple imageHeight,
            double roiRow, double roiCol, double roiPhi, double roiLength1, double roiLength2,
            double sigma, int threshold, string polarity,
            string direction, string selection,                 //260429 hbk Phase 15 — selection 파라미터 추가 (DatumConfig.*_EdgeSelection 전파)
            int sampleCount, int trimCount,
            out double lineRowBegin, out double lineColBegin,
            out double lineRowEnd, out double lineColEnd,
            out HTuple edgeRowsOut, out HTuple edgeColsOut, //260425 hbk Phase 13 D-VIZ-02 — raw 점 외부 노출
            out string error,
            string roiLabel) //260426 hbk Phase 13 D-PRP-HOTFIX — 진단 로그용 ROI 레이블
        {
            lineRowBegin = 0;
            lineColBegin = 0;
            lineRowEnd = 0;
            lineColEnd = 0;
            edgeRowsOut = new HTuple(); //260425 hbk Phase 13 D-VIZ-02 — 실패 분기에서도 null 방지
            edgeColsOut = new HTuple();
            error = null;

            //260426 hbk Phase 13 D-PRP-HOTFIX — sanity clamp: PropertyGrid 0/"" 누락 방어 (EnsurePerRoiDefaults 이후에도 이중 방어)
            if (sigma < 0.4) sigma = 1.0;          // Halcon MeasurePos requires sigma >= 0.4
            if (threshold <= 0) threshold = 20;
            if (string.IsNullOrEmpty(polarity)) polarity = "all";
            if (string.IsNullOrEmpty(direction)) direction = "LtoR";
            if (string.IsNullOrEmpty(selection)) selection = "First"; //260429 hbk Phase 15 — Halcon "first" 매핑 (canonical)
            if (sampleCount < 0) sampleCount = 0;
            if (trimCount < 0) trimCount = 0;

            //260426 hbk Phase 13 D-PRP-LOOP — bounding box 계산 (Phi=0 저장 규약 기준, halfW/halfH = Length1/Length2)
            double halfW    = roiLength1;
            double halfH    = roiLength2;
            double top      = roiRow - halfH;
            double bottom   = roiRow + halfH;
            double left     = roiCol - halfW;
            double right    = roiCol + halfW;
            double widthPx  = right - left;
            double heightPx = bottom - top;

            //260426 hbk Phase 13 D-PRP-LOOP — LtoR/RtoL = horizontal strips (row-sliced), TtoB/BtoT = vertical strips (col-sliced)
            bool scanHorizontal = (direction != "TtoB" && direction != "BtoT");
            //260509 hbk Phase 20 — sentinel 0 → 기본 20 strips. ?: → 명시 if/else
            int stripCount = 20;
            if (sampleCount > 0) stripCount = sampleCount; //260509 hbk Phase 20
            if (stripCount < 1) stripCount = 1;

            //260509 hbk Phase 20 — Trace 로그 인자 임시변수화 (roiLabel null 가드 + scanHorizontal 분기 명시)
            string lbl = "?";
            if (roiLabel != null) lbl = roiLabel; //260509 hbk Phase 20
            string scanLabel = "vertical";
            if (scanHorizontal) scanLabel = "horizontal"; //260509 hbk Phase 20

            Logging.PrintLog((int)ELogType.Trace,
                string.Format("[Datum.{0}] strip-loop: bounds top={1:F1} left={2:F1} bottom={3:F1} right={4:F1}  scan={5}  stripCount={6}  sigma={7:F2} threshold={8} polarity={9}",
                    lbl, top, left, bottom, right, //260509 hbk Phase 20
                    scanLabel, //260509 hbk Phase 20
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
                            direction, selection,                          //260429 hbk Phase 15 — measurePhi 매핑 + selection 전파
                            ref allRows, ref allCols,
                            roiLabel);                                     //260429 hbk Phase 15 — Trace 로그용 ROI 레이블 전달
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
                            direction, selection,                          //260429 hbk Phase 15 — measurePhi 매핑 + selection 전파
                            ref allRows, ref allCols,
                            roiLabel);                                     //260429 hbk Phase 15 — Trace 로그용 ROI 레이블 전달
                    }
                }

                int edgeCount = allRows.TupleLength();
                Logging.PrintLog((int)ELogType.Trace,
                    string.Format("[Datum.{0}] strip-loop accumulated {1} edge points across {2} strips",
                        lbl, edgeCount, stripCount)); //260509 hbk Phase 20

                //260426 hbk Phase 13 D-PRP-LOOP — TrimCount: 누적된 모든 점 중 양 끝 제거 (FitLineContourXld 입력 정제)
                if (trimCount > 0 && edgeCount > 2 * trimCount + 1)
                {
                    HTuple trimmedR = allRows.TupleSelectRange(trimCount, edgeCount - trimCount - 1);
                    HTuple trimmedC = allCols.TupleSelectRange(trimCount, edgeCount - trimCount - 1);
                    allRows    = trimmedR;
                    allCols    = trimmedC;
                    edgeCount  = allRows.TupleLength();
                    Logging.PrintLog((int)ELogType.Trace,
                        string.Format("[Datum.{0}] trimmed {1} from each end -> {2} edges remain",
                            lbl, trimCount, edgeCount)); //260509 hbk Phase 20
                }

                if (edgeCount < 2)
                {
                    error = string.Format(
                        "[{0}] insufficient edges across {1} strips: got {2} (need >=2). sigma={3:F2} threshold={4} polarity={5} scan={6}",
                        lbl, stripCount, edgeCount, sigma, threshold, polarity, //260509 hbk Phase 20
                        scanLabel); //260509 hbk Phase 20
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
                    //260425 hbk Phase 13 D-VIZ-02 — raw 점 외부 노출 (성공 시에만; 실패는 빈 HTuple 유지)
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

        //260423 hbk Phase 12 D-06 — 단일 Rectangle2 ROI에서 에지점만 추출 (라인 피팅 전 단계). 수평 2-ROI concat 피팅용.
        //260423 hbk  TryFindLine 과 달리 FitLineContourXld 단계를 생략하고 raw edge tuples 반환.
        //260425 hbk Phase 13 D-PRP-04 — per-ROI 에지 파라미터 적용 (direction/sampleCount/trimCount 추가)
        //260426 hbk Phase 13 D-PRP-LOOP — strip-loop MeasurePos 누적 패턴으로 전면 교체 (TryFindLine 과 동일 구조).
        private bool TryExtractEdgePoints(
            HImage image, HTuple imageWidth, HTuple imageHeight,
            double roiRow, double roiCol, double roiPhi, double roiLength1, double roiLength2,
            double sigma, int threshold, string polarity,
            string direction, string selection,                 //260429 hbk Phase 15 — selection 파라미터 추가
            int sampleCount, int trimCount,
            out HTuple rowEdge, out HTuple colEdge,
            out string error,
            string roiLabel) //260426 hbk Phase 13 D-PRP-HOTFIX — 진단 로그용 ROI 레이블
        {
            rowEdge = new HTuple();
            colEdge = new HTuple();
            error = null;

            //260426 hbk Phase 13 D-PRP-HOTFIX — sanity clamp (TryFindLine 과 동일 방어)
            if (sigma < 0.4) sigma = 1.0;
            if (threshold <= 0) threshold = 20;
            if (string.IsNullOrEmpty(polarity)) polarity = "all";
            if (string.IsNullOrEmpty(direction)) direction = "LtoR";
            if (string.IsNullOrEmpty(selection)) selection = "First"; //260429 hbk Phase 15
            if (sampleCount < 0) sampleCount = 0;
            if (trimCount < 0) trimCount = 0;

            //260426 hbk Phase 13 D-PRP-LOOP — bounding box 계산 (Phi=0 저장 규약 기준)
            double halfW    = roiLength1;
            double halfH    = roiLength2;
            double top      = roiRow - halfH;
            double bottom   = roiRow + halfH;
            double left     = roiCol - halfW;
            double right    = roiCol + halfW;
            double widthPx  = right - left;
            double heightPx = bottom - top;

            //260426 hbk Phase 13 D-PRP-LOOP — LtoR/RtoL = horizontal strips, TtoB/BtoT = vertical strips
            bool scanHorizontal = (direction != "TtoB" && direction != "BtoT");
            //260509 hbk Phase 20 — sentinel 0 → 기본 20 strips. ?: → 명시 if/else
            int stripCount = 20;
            if (sampleCount > 0) stripCount = sampleCount; //260509 hbk Phase 20
            if (stripCount < 1) stripCount = 1;

            //260509 hbk Phase 20 — Trace 로그 인자 임시변수화 (roiLabel null 가드 + scanHorizontal 분기 명시)
            string lbl = "?";
            if (roiLabel != null) lbl = roiLabel; //260509 hbk Phase 20
            string scanLabel = "vertical";
            if (scanHorizontal) scanLabel = "horizontal"; //260509 hbk Phase 20

            Logging.PrintLog((int)ELogType.Trace,
                string.Format("[Datum.{0}] strip-loop(extract): bounds top={1:F1} left={2:F1} bottom={3:F1} right={4:F1}  scan={5}  stripCount={6}  sigma={7:F2} threshold={8} polarity={9}",
                    lbl, top, left, bottom, right, //260509 hbk Phase 20
                    scanLabel, //260509 hbk Phase 20
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
                            direction, selection,                          //260429 hbk Phase 15 — measurePhi 매핑 + selection 전파
                            ref allRows, ref allCols,
                            roiLabel);                                     //260429 hbk Phase 15
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
                            direction, selection,                          //260429 hbk Phase 15 — measurePhi 매핑 + selection 전파
                            ref allRows, ref allCols,
                            roiLabel);                                     //260429 hbk Phase 15
                    }
                }

                int edgeCount = allRows.TupleLength();
                Logging.PrintLog((int)ELogType.Trace,
                    string.Format("[Datum.{0}] strip-loop(extract) accumulated {1} edge points across {2} strips",
                        lbl, edgeCount, stripCount)); //260509 hbk Phase 20

                //260426 hbk Phase 13 D-PRP-LOOP — TrimCount: 누적된 전체 점 양 끝 제거
                if (trimCount > 0 && edgeCount > 2 * trimCount + 1)
                {
                    HTuple trimmedR = allRows.TupleSelectRange(trimCount, edgeCount - trimCount - 1);
                    HTuple trimmedC = allCols.TupleSelectRange(trimCount, edgeCount - trimCount - 1);
                    allRows   = trimmedR;
                    allCols   = trimmedC;
                    edgeCount = allRows.TupleLength();
                    Logging.PrintLog((int)ELogType.Trace,
                        string.Format("[Datum.{0}] trimmed {1} from each end -> {2} edges remain",
                            lbl, trimCount, edgeCount)); //260509 hbk Phase 20
                }

                rowEdge = allRows;
                colEdge = allCols;

                // caller 가 concat 후 MIN_HORIZONTAL_EDGES 별도 확인하므로 1개 이상이면 성공으로 반환
                if (edgeCount < 1)
                {
                    error = string.Format(
                        "[{0}] no edges found across {1} strips. sigma={2:F2} threshold={3} polarity={4} scan={5}",
                        lbl, stripCount, sigma, threshold, polarity, //260509 hbk Phase 20
                        scanLabel); //260509 hbk Phase 20
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

        //260426 hbk Phase 13 D-PRP-LOOP — 단일 strip 에서 MeasurePos 실행 후 edge 점 누적.
        //  참조: C:\Info\Project\DatumMeasure\DatumMeasure\Algorithms\MeasurementAlgorithm.cs AppendEdgePointsFromStrip.
        //  strip 실패(빈 결과 / 예외)는 swallow — 한 strip 실패가 전체 ROI 를 중단시키지 않음.
        //260429 hbk Phase 15 — direction → measurePhi 4-way 명시 매핑 (BtoT/TtoB 부호 구분), selection 인자화.
        //  CANONICAL refs: MeasurementAlgorithm.cs:130-178, FAIEdgeMeasurementService.cs:82-106, VisionAlgorithmService.cs:63-72.
        //  SmallestRectangle2 의 rp 자동 도출은 사용자 의도(BtoT vs TtoB) 를 구분 못 함 → polarity 의미 뒤집힘.
        private void AppendEdgePointsFromStrip(
            HImage image,
            double row1, double col1, double row2, double col2,
            HTuple imageWidth, HTuple imageHeight,
            double sigma, int threshold, string polarity,
            string direction, string selection,                          //260429 hbk Phase 15 — measurePhi 매핑 + selection 명시화
            ref HTuple allRows, ref HTuple allCols,
            string roiLabel)                                              //260429 hbk Phase 15 — Trace 로그용 ROI 레이블 (Claude's Discretion)
        {
            //260429 hbk Phase 15 — direction → measurePhi (CANONICAL: MeasurementAlgorithm.cs:130-178)
            double measurePhi;
            if (string.Equals(direction, "TtoB", StringComparison.OrdinalIgnoreCase))      measurePhi = -Math.PI / 2.0;
            else if (string.Equals(direction, "BtoT", StringComparison.OrdinalIgnoreCase)) measurePhi = +Math.PI / 2.0;
            else if (string.Equals(direction, "RtoL", StringComparison.OrdinalIgnoreCase)) measurePhi = Math.PI;
            else                                                                            measurePhi = 0.0; //260429 hbk Phase 15 — LtoR 기본

            //260509 hbk Phase 20 — selection (PascalCase) → Halcon MeasurePos 인자 (lower). chained ?: → if/else (CANONICAL: MeasurementAlgorithm.cs:178 의미 보존)
            string selectionLower = "first";
            if (string.Equals(selection, "Last", StringComparison.OrdinalIgnoreCase)) selectionLower = "last"; //260509 hbk Phase 20
            else if (string.Equals(selection, "All",  StringComparison.OrdinalIgnoreCase)) selectionLower = "all"; //260509 hbk Phase 20

            HObject stripRegion = null;
            HTuple measureHandle = null;
            try
            {
                HOperatorSet.GenRectangle1(out stripRegion, row1, col1, row2, col2);

                HTuple rr, rc, rp, rh, rw;
                HOperatorSet.SmallestRectangle2(stripRegion, out rr, out rc, out rp, out rh, out rw);
                //260429 hbk Phase 15 — rp(자동 phi) 는 더 이상 사용 안 함; measurePhi 만 사용. rh/rw 는 유효 (strip 의 half-W/H).

                HOperatorSet.GenMeasureRectangle2(
                    rr, rc, measurePhi, rh, rw,                          //260429 hbk Phase 15 — rp → measurePhi
                    imageWidth, imageHeight, "nearest_neighbor",
                    out measureHandle);

                HTuple edgeRows, edgeCols, amp, dist;
                HOperatorSet.MeasurePos(
                    image, measureHandle, sigma, threshold,
                    polarity, selectionLower,                             //260429 hbk Phase 15 — "all" → selectionLower
                    out edgeRows, out edgeCols, out amp, out dist);

                //260509 hbk Phase 20 — Trace 로그 강화: measurePhi (deg) + selection 노출. null-coalesce → 임시변수 + null 체크 (P-1)
                string lbl = "?";
                if (roiLabel != null) lbl = roiLabel; //260509 hbk Phase 20
                string dirLabel = "?";
                if (direction != null) dirLabel = direction; //260509 hbk Phase 20
                Logging.PrintLog((int)ELogType.Trace,
                    string.Format("[Datum.{0}] strip MeasurePos: dir={1} measurePhi={2:F1}deg sel={3} edges={4}",
                        lbl, dirLabel, measurePhi * 180.0 / Math.PI, selectionLower, //260509 hbk Phase 20
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
                //260509 hbk Phase 20 — 빈 catch 진단 강화: 라벨 + 예외 메시지 (per-strip swallow 정책 유지). null-coalesce → 임시변수 + null 체크 (P-1)
                try
                {
                    string lblCatch = "?";
                    if (roiLabel != null) lblCatch = roiLabel; //260509 hbk Phase 20
                    Logging.PrintLog((int)ELogType.Trace,
                        string.Format("[Datum.{0}] strip swallowed: {1}", lblCatch, ex.Message)); //260509 hbk Phase 20
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
