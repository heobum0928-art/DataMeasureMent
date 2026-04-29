//260413 hbk Phase 6: Halcon 빌딩 블록 서비스 — FitLine, FindCircle, 기하 유틸 (D-18)
using System;
using HalconDotNet;

namespace ReringProject.Halcon.Algorithms
{
    /// <summary>
    /// Phase 6 Multi-Algorithm 측정 클래스들이 공용으로 사용하는 Halcon 빌딩 블록.
    /// 모든 Halcon 호출은 try { ... } catch { return false; } 패턴 (프로젝트 컨벤션).
    /// 순수 수학 연산은 static 메서드로 제공한다.
    /// </summary>
    public class VisionAlgorithmService //260413 hbk
    {
        /// <summary>
        /// ROI(Rectangle2) 내부에서 에지 포인트를 검출하고 FitLineContourXld로 직선을 피팅한다.
        /// datumTransform이 유효하면 ROI 좌표를 변환한 뒤 피팅한다(Datum 런타임 보정).
        /// </summary>
        public bool TryFitLine( //260413 hbk
            HImage image,
            double roiRow, double roiCol, double roiPhi,
            double roiLength1, double roiLength2,
            HTuple datumTransform,
            int sampleCount, int trimCount, double sigma, int threshold,
            string direction, string polarity,
            out double row1, out double col1, out double row2, out double col2,
            out string error)
        {
            row1 = col1 = row2 = col2 = 0;
            error = null;

            if (image == null)
            {
                error = "image is null";
                return false;
            }

            HTuple measureHandle = null;
            HObject contour = null;
            try
            {
                double rRow = roiRow, rCol = roiCol, rPhi = roiPhi;

                if (datumTransform != null && datumTransform.Length > 0)
                {
                    try
                    {
                        HTuple tRow, tCol;
                        HOperatorSet.AffineTransPoint2d(datumTransform, roiRow, roiCol, out tRow, out tCol);
                        rRow = tRow.D;
                        rCol = tCol.D;
                        double rotAngle = Math.Atan2(-datumTransform[1].D, datumTransform[0].D);
                        rPhi = roiPhi + rotAngle;
                    }
                    catch
                    {
                        // transform 실패 시 원본 좌표 사용
                    }
                }

                HTuple imageWidth, imageHeight;
                image.GetImageSize(out imageWidth, out imageHeight);

                double measurePhi;
                bool scanHorizontal = true;
                if (string.Equals(direction, "TtoB", StringComparison.OrdinalIgnoreCase))
                { measurePhi = -Math.PI / 2.0; scanHorizontal = false; }
                else if (string.Equals(direction, "BtoT", StringComparison.OrdinalIgnoreCase))
                { measurePhi = Math.PI / 2.0; scanHorizontal = false; }
                else if (string.Equals(direction, "RtoL", StringComparison.OrdinalIgnoreCase))
                { measurePhi = Math.PI; }
                else
                { measurePhi = 0.0; }

                measurePhi = measurePhi + (rPhi - roiPhi);

                string pol = string.Equals(polarity, "LightToDark", StringComparison.OrdinalIgnoreCase)
                    ? "negative" : "positive";

                HOperatorSet.GenMeasureRectangle2(
                    rRow, rCol, rPhi, roiLength1, roiLength2,
                    imageWidth, imageHeight, "nearest_neighbor",
                    out measureHandle);

                HTuple rows, cols, amp, dist;
                HOperatorSet.MeasurePos(image, measureHandle,
                    Math.Max(0.4, sigma), Math.Max(1, threshold),
                    pol, "all", out rows, out cols, out amp, out dist);

                int edgeCount = rows.TupleLength();
                if (edgeCount < 2)
                {
                    error = "insufficient edge points (" + edgeCount + ")";
                    return false;
                }

                HOperatorSet.GenContourPolygonXld(out contour, rows, cols);
                HTuple lr1, lc1, lr2, lc2, nr, nc, df;
                HOperatorSet.FitLineContourXld(contour, "tukey", -1, 0, 5, 2,
                    out lr1, out lc1, out lr2, out lc2, out nr, out nc, out df);

                row1 = lr1.D; col1 = lc1.D;
                row2 = lr2.D; col2 = lc2.D;
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
                if (contour != null) { try { contour.Dispose(); } catch { } }
            }
        }

        /// <summary>
        /// 원형 ROI에서 에지 포인트를 검출하고 FitCircleContourXld로 원을 피팅한다.
        /// </summary>
        public bool TryFindCircle( //260413 hbk
            HImage image,
            double centerRow, double centerCol, double radius,
            HTuple datumTransform,
            double sigma, int threshold, string polarity,
            out double foundRow, out double foundCol, out double foundRadius,
            out string error)
        {
            foundRow = foundCol = foundRadius = 0;
            error = null;

            if (image == null)
            {
                error = "image is null";
                return false;
            }

            HObject circleRegion = null;
            HObject circleBorder = null;
            HObject edges = null;
            try
            {
                double cRow = centerRow, cCol = centerCol;
                if (datumTransform != null && datumTransform.Length > 0)
                {
                    try
                    {
                        HTuple tRow, tCol;
                        HOperatorSet.AffineTransPoint2d(datumTransform, centerRow, centerCol, out tRow, out tCol);
                        cRow = tRow.D;
                        cCol = tCol.D;
                    }
                    catch { }
                }

                HTuple imageWidth, imageHeight;
                image.GetImageSize(out imageWidth, out imageHeight);

                HOperatorSet.GenCircle(out circleRegion, cRow, cCol, radius);
                HObject imageReduced;
                HOperatorSet.ReduceDomain(image, circleRegion, out imageReduced);

                try
                {
                    HOperatorSet.EdgesSubPix(imageReduced, out edges,
                        "canny", Math.Max(0.4, sigma), Math.Max(1, threshold / 2), Math.Max(2, threshold));
                    imageReduced.Dispose();
                }
                catch
                {
                    imageReduced.Dispose();
                    throw;
                }

                HTuple row, column, rad, startPhi, endPhi, pointOrder;
                HOperatorSet.FitCircleContourXld(edges, "atukey", -1, 2, 0, 5, 2,
                    out row, out column, out rad, out startPhi, out endPhi, out pointOrder);

                if (row.Length == 0)
                {
                    error = "no circle fitted";
                    return false;
                }

                foundRow = row[0].D;
                foundCol = column[0].D;
                foundRadius = rad[0].D;
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
            finally
            {
                if (circleRegion != null) { try { circleRegion.Dispose(); } catch { } }
                if (circleBorder != null) { try { circleBorder.Dispose(); } catch { } }
                if (edges != null) { try { edges.Dispose(); } catch { } }
            }
        }

        /// <summary>
        /// 360° polar sampling 방식의 Circle 검출.
        /// center+radius 기점에서 stepDeg 간격으로 회전하며, 각 각도 θ 에서 작은 사각형 ROI 의 MeasurePos
        /// 첫 에지점 1개 추출 → 누적 → FitCircleContourXld. raw 에지점 HTuple out 반환
        /// (legacy TryFindCircle 의 미반환 결함 closure).
        /// </summary>
        //260426 hbk Phase 14-04 Req 4 — D-12/D-13 좌표계: 화면 시점 CCW (0°=right, 90°=up, 180°=left, 270°=down)
        //   rect 중심 row = centerRow - radius * sin(theta_rad)  (sin 앞 minus: 화면 위쪽 = row 감소)
        //   rect 중심 col = centerCol + radius * cos(theta_rad)
        //   rect phi      = theta_rad (반경 방향 = rect length1 축; Halcon Rectangle2 phi 는 horizontal axis 기준 CCW radian)
        //   주의: Halcon Rectangle2 phi 정의 SDK 문서 재확인 필수 (D-13 caveat).
        //        Task 3 smoke test (θ=0°/90°/180°/270°) 가 4 위치 PASS 시 부호식 검증 완료.
        public bool TryFindCircleByPolarSampling(
            HImage image,
            double centerRow, double centerCol, double radius,
            double stepDeg, double rectL1Ratio, double rectL2Ratio,
            double sigma, int threshold, string polarity, string selection, //260429 hbk Phase 15 — selection 명시 처리 ("all" 하드코딩 제거)
            HTuple datumTransform,
            out double foundRow, out double foundCol, out double foundRadius,
            out HTuple edgeRows, out HTuple edgeCols,
            out string error)
        {
            foundRow = 0; foundCol = 0; foundRadius = 0;
            edgeRows = new HTuple();
            edgeCols = new HTuple();
            error = null;

            if (image == null) { error = "image is null"; return false; }

            //260426 hbk Phase 14-04 — Sanity clamp (sentinel/0 방어)
            if (stepDeg <= 0)         stepDeg     = 10.0;
            if (stepDeg > 30)         stepDeg     = 30.0;
            if (rectL1Ratio <= 0)     rectL1Ratio = 0.05;
            if (rectL2Ratio <= 0)     rectL2Ratio = 0.05;
            if (sigma < 0.4)          sigma       = 1.0;
            if (threshold <= 0)       threshold   = 20;
            if (string.IsNullOrEmpty(polarity)) polarity = "all";
            //260429 hbk Phase 15 — selection sanity clamp + PascalCase → Halcon lower-case 변환 (CANONICAL: MeasurementAlgorithm.cs:178)
            if (string.IsNullOrEmpty(selection)) selection = "First";
            string selectionLower =
                string.Equals(selection, "Last", StringComparison.OrdinalIgnoreCase) ? "last" :
                string.Equals(selection, "All",  StringComparison.OrdinalIgnoreCase) ? "all"  : "first";
            if (radius <= 0)          { error = "radius must be > 0"; return false; }

            //260426 hbk Phase 14-04 — Datum transform (legacy TryFindCircle 패턴 — center 만 변환, radius 는 무변환)
            double cRow = centerRow, cCol = centerCol;
            if (datumTransform != null && datumTransform.Length > 0)
            {
                try
                {
                    HTuple tRow, tCol;
                    HOperatorSet.AffineTransPoint2d(datumTransform, centerRow, centerCol, out tRow, out tCol);
                    cRow = tRow.D;
                    cCol = tCol.D;
                }
                catch
                {
                    // Identity transform fallback — 이전 동작 유지
                }
            }

            HObject contour = null;
            try
            {
                HTuple imageWidth, imageHeight;
                image.GetImageSize(out imageWidth, out imageHeight);

                double halfL1 = radius * rectL1Ratio;
                double halfL2 = radius * rectL2Ratio;
                if (halfL1 < 1.0) halfL1 = 1.0;
                if (halfL2 < 1.0) halfL2 = 1.0;

                HTuple allRows = new HTuple();
                HTuple allCols = new HTuple();

                int stepCount = (int)Math.Round(360.0 / stepDeg);
                for (int i = 0; i < stepCount; i++)
                {
                    double thetaDeg = i * stepDeg;
                    double thetaRad = thetaDeg * Math.PI / 180.0;
                    //260426 hbk Phase 14-04 D-13 — 화면 CCW 좌표계 (sin 앞 minus)
                    double rectRow = cRow - radius * Math.Sin(thetaRad);
                    double rectCol = cCol + radius * Math.Cos(thetaRad);
                    double rectPhi = thetaRad; // 반경 방향 = rect length1 축

                    HTuple measureHandle = null;
                    try
                    {
                        HOperatorSet.GenMeasureRectangle2(
                            rectRow, rectCol, rectPhi, halfL1, halfL2,
                            imageWidth, imageHeight, "nearest_neighbor",
                            out measureHandle);

                        HTuple eRows, eCols, amp, dist;
                        HOperatorSet.MeasurePos(image, measureHandle,
                            sigma, threshold, polarity, selectionLower, //260429 hbk Phase 15 — "all" 하드코딩 → caller selection 반영
                            out eRows, out eCols, out amp, out dist);

                        if (eRows.TupleLength() > 0 && eCols.TupleLength() > 0)
                        {
                            //260429 hbk Phase 15 — selection 정책 분기: First/Last 는 단일점 누적(Phase 14-04 stepCount 보존), All 은 전체 누적
                            if (string.Equals(selectionLower, "all", StringComparison.OrdinalIgnoreCase))
                            {
                                HOperatorSet.TupleConcat(allRows, eRows, out allRows);
                                HOperatorSet.TupleConcat(allCols, eCols, out allCols);
                            }
                            else
                            {
                                //260426 hbk Phase 14-04 — 첫 에지점 1개만 누적 (회전 sweep 의 의도) — First/Last 모드는 Halcon 자체가 1점 반환
                                HOperatorSet.TupleConcat(allRows, eRows[0], out allRows);
                                HOperatorSet.TupleConcat(allCols, eCols[0], out allCols);
                            }
                        }
                    }
                    catch
                    {
                        //260426 hbk Phase 14-04 — per-step 실패 swallow (AppendEdgePointsFromStrip 관습) — 나머지 step 계속
                    }
                    finally
                    {
                        if (measureHandle != null)
                        {
                            try { HOperatorSet.CloseMeasure(measureHandle); } catch { }
                        }
                    }
                }

                edgeRows = allRows;
                edgeCols = allCols;

                if (allRows.TupleLength() < 3)
                {
                    error = "insufficient polar samples (" + allRows.TupleLength() + ")";
                    return false;
                }

                //260426 hbk Phase 14-04 — FitCircleContourXld (legacy TryFindCircle 패턴 — atukey robust)
                HOperatorSet.GenContourPolygonXld(out contour, allRows, allCols);

                HTuple row, column, rad, startPhi, endPhi, pointOrder;
                HOperatorSet.FitCircleContourXld(contour, "atukey", -1, 2, 0, 5, 2,
                    out row, out column, out rad, out startPhi, out endPhi, out pointOrder);

                if (row.Length == 0) { error = "no circle fitted"; return false; }

                foundRow = row[0].D;
                foundCol = column[0].D;
                foundRadius = rad[0].D;
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
            finally
            {
                if (contour != null) { try { contour.Dispose(); } catch { } }
            }
        }

        //260426 hbk Phase 14-04 W1 — D-13 부호식 4점 smoke harness (production 영향 0; 외부 호출자만 활성).
        //  PASS 후 호출 주석 처리하여 dormant 상태로 보존. 메서드 자체는 영구 코드 — 회귀 시 재호출.
        //  실제 Halcon Rectangle2 phi 부호 검증은 SIMUL_MODE 에서 사람이 화면 시각으로 4 rect (right/up/left/down)
        //  배치 확인 (Task 3b). 본 harness 는 좌표 계산 식만 trace 로그로 노출.
        public void RunPhiSmokeTest(HImage image, double centerRow, double centerCol, double radius)
        {
            if (image == null) return;
            //260427 hbk Phase 14 WR-02 — 기대값을 hand-precomputed 독립 reference 로 교체 (sin/cos 부호 회귀 시 delta 발산).
            //  화면 CCW: 0°=right(+col), 90°=up(-row), 180°=left(-col), 270°=down(+row).
            //  cases: (thetaDeg, expRow, expCol)
            double[][] cases = new double[][]
            {
                new double[] {   0.0, centerRow,            centerCol + radius },
                new double[] {  90.0, centerRow - radius,   centerCol          },
                new double[] { 180.0, centerRow,            centerCol - radius },
                new double[] { 270.0, centerRow + radius,   centerCol          },
            };
            foreach (double[] c in cases)
            {
                double thetaDeg = c[0];
                double expRow = c[1];
                double expCol = c[2];
                double thetaRad = thetaDeg * Math.PI / 180.0;
                //260426 hbk Phase 14-04 D-13 — 화면 CCW 좌표계 (sin 앞 minus, TryFindCircleByPolarSampling 와 동일 식)
                double rectRow = centerRow - radius * Math.Sin(thetaRad);
                double rectCol = centerCol + radius * Math.Cos(thetaRad);
                double dRow = Math.Abs(rectRow - expRow);
                double dCol = Math.Abs(rectCol - expCol);
                double delta = Math.Sqrt(dRow * dRow + dCol * dCol);

                ReringProject.Utility.Logging.PrintLog((int)ReringProject.Setting.ELogType.Trace,
                    "PHI_SMOKE: theta=" + thetaDeg.ToString("F0") +
                    " expected=(" + expRow.ToString("F1") + "," + expCol.ToString("F1") + ")" +
                    " actual=(" + rectRow.ToString("F1") + "," + rectCol.ToString("F1") + ")" +
                    " delta=" + delta.ToString("F2"));
            }
        }

        /// <summary>
        /// 점과 직선 사이의 수직 거리(픽셀). 순수 수학 — 교차곱 기반.
        /// </summary>
        public static double DistancePointToLine( //260413 hbk
            double pRow, double pCol,
            double lRow1, double lCol1, double lRow2, double lCol2)
        {
            double dx = lCol2 - lCol1;
            double dy = lRow2 - lRow1;
            double len = Math.Sqrt(dx * dx + dy * dy);
            if (len < 1e-9) return 0.0;
            double cross = Math.Abs(dx * (lRow1 - pRow) - dy * (lCol1 - pCol));
            return cross / len;
        }

        /// <summary>
        /// 두 점 사이의 유클리드 거리(픽셀).
        /// </summary>
        public static double DistancePointToPoint( //260413 hbk
            double row1, double col1, double row2, double col2)
        {
            double dr = row2 - row1;
            double dc = col2 - col1;
            return Math.Sqrt(dr * dr + dc * dc);
        }

        /// <summary>
        /// 두 직선 사이의 각도(degree, 0~180). Halcon AngleLl 사용.
        /// </summary>
        public static double AngleLineLine( //260413 hbk
            double row1a, double col1a, double row1b, double col1b,
            double row2a, double col2a, double row2b, double col2b)
        {
            try
            {
                HTuple angleRad;
                HOperatorSet.AngleLl(row1a, col1a, row1b, col1b, row2a, col2a, row2b, col2b, out angleRad);
                double deg = angleRad.D * 180.0 / Math.PI;
                if (deg < 0) deg = -deg;
                if (deg > 180.0) deg = 360.0 - deg;
                return deg;
            }
            catch
            {
                double dx1 = col1b - col1a, dy1 = row1b - row1a;
                double dx2 = col2b - col2a, dy2 = row2b - row2a;
                double dot = dx1 * dx2 + dy1 * dy2;
                double m1 = Math.Sqrt(dx1 * dx1 + dy1 * dy1);
                double m2 = Math.Sqrt(dx2 * dx2 + dy2 * dy2);
                if (m1 < 1e-9 || m2 < 1e-9) return 0.0;
                double c = dot / (m1 * m2);
                if (c > 1) c = 1; if (c < -1) c = -1;
                return Math.Acos(c) * 180.0 / Math.PI;
            }
        }

        /// <summary>
        /// 두 직선의 교점을 구한다. 평행이면 false.
        /// </summary>
        public static bool IntersectLines( //260413 hbk
            double row1a, double col1a, double row1b, double col1b,
            double row2a, double col2a, double row2b, double col2b,
            out double intRow, out double intCol)
        {
            intRow = 0; intCol = 0;
            try
            {
                HTuple iRow, iCol, isOverlapping;
                HOperatorSet.IntersectionLl(
                    row1a, col1a, row1b, col1b,
                    row2a, col2a, row2b, col2b,
                    out iRow, out iCol, out isOverlapping);
                if (isOverlapping.I == 1) return false; //260423 hbk WR-01 collinear
                if (double.IsInfinity(iRow.D) || double.IsInfinity(iCol.D) || //260423 hbk WR-01 parallel guard
                    double.IsNaN(iRow.D) || double.IsNaN(iCol.D)) //260423 hbk WR-01
                {
                    return false; //260423 hbk WR-01
                }
                intRow = iRow.D;
                intCol = iCol.D;
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 점(row,col)에 hom_mat2d 변환을 적용한다.
        /// </summary>
        public static void AffineTransformPoint( //260413 hbk
            HTuple homMat2D, double row, double col,
            out double transRow, out double transCol)
        {
            transRow = row;
            transCol = col;
            if (homMat2D == null || homMat2D.Length == 0) return;
            try
            {
                HTuple tr, tc;
                HOperatorSet.AffineTransPoint2d(homMat2D, row, col, out tr, out tc);
                transRow = tr.D;
                transCol = tc.D;
            }
            catch { }
        }
    }
}
