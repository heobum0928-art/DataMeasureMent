using System;
using System.Collections.Generic;
using HalconDotNet;

namespace ReringProject.Halcon.Algorithms
{
    /// <summary>
    /// Phase 6 Multi-Algorithm 측정 클래스들이 공용으로 사용하는 Halcon 빌딩 블록.
    /// 모든 Halcon 호출은 try { ... } catch { return false; } 패턴 (프로젝트 컨벤션).
    /// 순수 수학 연산은 static 메서드로 제공한다.
    /// </summary>
    public class VisionAlgorithmService
    {
        /// <summary>
        /// ROI(Rectangle2) 내부에서 에지 포인트를 검출하고 FitLineContourXld로 직선을 피팅한다.
        /// datumTransform이 유효하면 ROI 좌표를 변환한 뒤 피팅한다(Datum 런타임 보정).
        /// </summary>
        public bool TryFitLine(
            HImage image,
            double roiRow, double roiCol, double roiPhi,
            double roiLength1, double roiLength2,
            HTuple datumTransform,
            int sampleCount, int trimCount, double sigma, int threshold,
            string direction, string polarity,
            out double row1, out double col1, out double row2, out double col2,
            out string error,
            string selection = "all",
            // collectedEdges: opt-in, strip-loop 누적 raw 에지점을 caller list 에 노출 (측정 overlay 가시화용)
            List<ValueTuple<double, double>> collectedEdges = null)
        {
            row1 = col1 = row2 = col2 = 0;
            error = null;

            if (image == null)
            {
                error = "image is null";
                return false;
            }

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

                string pol;
                if (string.Equals(polarity, "LightToDark", StringComparison.OrdinalIgnoreCase))
                {
                    pol = "negative";
                }
                else
                {
                    pol = "positive";
                }

                // EdgeSelection 명시 처리
                string measureSel;
                if (string.Equals(selection, "First", StringComparison.OrdinalIgnoreCase))
                {
                    measureSel = "first";
                }
                else if (string.Equals(selection, "Last", StringComparison.OrdinalIgnoreCase))
                {
                    measureSel = "last";
                }
                else
                {
                    measureSel = "all";
                }

                // 단일 MeasurePos 는 측정 축 1개에서만 에지 반환 → FitLineContourXld 입력 1~2점 → "insufficient edge
                // points" → 측정 실패. ROI 를 stripCount 개 strip 으로 쪼개 strip 마다 MeasurePos 누적.
                // rPhi 회전은 strip region 회전 대신 measurePhi 로 흡수 (축 정렬 strip + 회전된 측정 축). datum 회전 ROI 동일 경로.
                double halfW = roiLength1;
                double halfH = roiLength2;
                double top = rRow - halfH;
                double bottom = rRow + halfH;
                double left = rCol - halfW;
                double right = rCol + halfW;
                double widthPx = right - left;
                double heightPx = bottom - top;

                // stripCount: sentinel 0 → 기본 20
                int stripCount = 20;
                if (sampleCount > 0) stripCount = sampleCount;
                if (stripCount < 1) stripCount = 1;

                HTuple allRows = new HTuple();
                HTuple allCols = new HTuple();

                if (scanHorizontal)
                {
                    for (int i = 0; i < stripCount; i++)
                    {
                        double r1 = top + (i * heightPx / stripCount);
                        double r2 = top + ((i + 1) * heightPx / stripCount);
                        AppendStrip(image, r1, left, r2, right, imageWidth, imageHeight,
                            Math.Max(0.4, sigma), Math.Max(1, threshold), pol, measurePhi, measureSel,
                            ref allRows, ref allCols);
                    }
                }
                else
                {
                    for (int i = 0; i < stripCount; i++)
                    {
                        double c1 = left + (i * widthPx / stripCount);
                        double c2 = left + ((i + 1) * widthPx / stripCount);
                        AppendStrip(image, top, c1, bottom, c2, imageWidth, imageHeight,
                            Math.Max(0.4, sigma), Math.Max(1, threshold), pol, measurePhi, measureSel,
                            ref allRows, ref allCols);
                    }
                }

                // TrimCount 적용 (누적 점 양 끝 제거)
                int edgeCount = allRows.TupleLength();
                if (trimCount > 0 && edgeCount > 2 * trimCount + 1)
                {
                    HTuple trimmedR = allRows.TupleSelectRange(trimCount, edgeCount - trimCount - 1);
                    HTuple trimmedC = allCols.TupleSelectRange(trimCount, edgeCount - trimCount - 1);
                    allRows = trimmedR;
                    allCols = trimmedC;
                    edgeCount = allRows.TupleLength();
                }

                if (edgeCount < 2)
                {
                    error = "insufficient edge points (" + edgeCount + ") across " + stripCount + " strips";
                    return false;
                }

                HOperatorSet.GenContourPolygonXld(out contour, allRows, allCols);
                HTuple lr1, lc1, lr2, lc2, nr, nc, df;
                HOperatorSet.FitLineContourXld(contour, "tukey", -1, 0, 5, 2,
                    out lr1, out lc1, out lr2, out lc2, out nr, out nc, out df);

                row1 = lr1.D; col1 = lc1.D;
                row2 = lr2.D; col2 = lc2.D;

                // opt-in: 라인 피팅에 사용된 trim 후 raw 에지점들을 caller list 에 누적 (overlay 가시화용)
                if (collectedEdges != null)
                {
                    for (int k = 0; k < edgeCount; k++)
                    {
                        collectedEdges.Add(new ValueTuple<double, double>(allRows[k].D, allCols[k].D));
                    }
                }
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

        // 단일 strip 에서 MeasurePos 실행 후 edge 점 누적.
        //  polarity: 이 헬퍼는 Halcon polarity 문자열("positive"/"negative")을 그대로 받는다 (caller 에서 매핑 완료).
        //  measurePhi: caller 가 direction 매핑 + rPhi 회전 보정을 합산한 값을 전달
        //    → 헬퍼는 SmallestRectangle2 자동 phi(rp) 를 쓰지 않고 전달받은 measurePhi 만 사용.
        //  strip 실패(빈 결과 / 예외)는 swallow — 한 strip 실패가 전체 ROI 를 중단시키지 않음.
        private void AppendStrip(
            HImage image,
            double row1, double col1, double row2, double col2,
            HTuple imageWidth, HTuple imageHeight,
            double sigma, int threshold, string polarity,
            double measurePhi, string selection,
            ref HTuple allRows, ref HTuple allCols)
        {
            HObject stripRegion = null;
            HTuple measureHandle = null;
            try
            {
                HOperatorSet.GenRectangle1(out stripRegion, row1, col1, row2, col2);
                HTuple rr, rc, rp, rh, rw;
                // rp(자동 phi)는 미사용; rr/rc/rh/rw 만 사용
                HOperatorSet.SmallestRectangle2(stripRegion, out rr, out rc, out rp, out rh, out rw);
                HOperatorSet.GenMeasureRectangle2(
                    rr, rc, measurePhi, rh, rw,
                    imageWidth, imageHeight, "nearest_neighbor",
                    out measureHandle);
                HTuple edgeRows, edgeCols, amp, dist;
                HOperatorSet.MeasurePos(
                    image, measureHandle, sigma, threshold,
                    polarity, selection,
                    out edgeRows, out edgeCols, out amp, out dist);
                if (edgeRows.TupleLength() <= 0 || edgeCols.TupleLength() <= 0)
                {
                    return;
                }
                HOperatorSet.TupleConcat(allRows, edgeRows, out allRows);
                HOperatorSet.TupleConcat(allCols, edgeCols, out allCols);
            }
            catch
            {
            }
            finally
            {
                if (measureHandle != null) { try { HOperatorSet.CloseMeasure(measureHandle); } catch { } }
                if (stripRegion != null) { try { stripRegion.Dispose(); } catch { } }
            }
        }

        /// <summary>
        /// 원형 ROI에서 에지 포인트를 검출하고 FitCircleContourXld로 원을 피팅한다.
        /// </summary>
        public bool TryFindCircle(
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

        // polar strip half-extent cap (px). 큰 strip = MeasurePos 노이즈 → 측정 실패를 막는 cap.
        //  HalconDisplayService.RenderCircleStrips 와 공유 (WYSIWYG).
        public const double CircleStripHalfExtentCapPx = 36.0;

        /// <summary>
        /// 360° polar sampling 방식의 Circle 검출.
        /// center+radius 기점에서 stepDeg 간격으로 회전하며, 각 각도 θ 에서 작은 사각형 ROI 의 MeasurePos
        /// 첫 에지점 1개 추출 → 누적 → FitCircleContourXld. raw 에지점 HTuple out 반환
        /// (legacy TryFindCircle 의 미반환 결함 closure).
        /// </summary>
        // 좌표계: 화면 시점 CCW (0°=right, 90°=up, 180°=left, 270°=down)
        //   rect 중심 row = centerRow - radius * sin(theta_rad)  (sin 앞 minus: 화면 위쪽 = row 감소)
        //   rect 중심 col = centerCol + radius * cos(theta_rad)
        //   rect phi      = theta_rad (반경 방향 = rect length1 축; Halcon Rectangle2 phi 는 horizontal axis 기준 CCW radian)
        public bool TryFindCircleByPolarSampling(
            HImage image,
            double centerRow, double centerCol, double radius,
            double stepDeg, double rectL1Ratio, double rectL2Ratio,
            double sigma, int threshold, string polarity, string selection,
            HTuple datumTransform,
            out double foundRow, out double foundCol, out double foundRadius,
            out HTuple edgeRows, out HTuple edgeCols,
            out bool[] stripSuccesses, // per-strip 검출 성공 여부
            out string error)
        {
            foundRow = 0; foundCol = 0; foundRadius = 0;
            edgeRows = new HTuple();
            edgeCols = new HTuple();
            stripSuccesses = null;
            error = null;

            if (image == null) { error = "image is null"; return false; }

            // Sanity clamp (sentinel/0 방어)
            if (stepDeg <= 0)         stepDeg     = 10.0;
            if (stepDeg > 30)         stepDeg     = 30.0;
            if (rectL1Ratio <= 0)     rectL1Ratio = 0.05;
            if (rectL2Ratio <= 0)     rectL2Ratio = 0.05;
            if (sigma < 0.4)          sigma       = 1.0;
            if (threshold <= 0)       threshold   = 20;
            if (string.IsNullOrEmpty(polarity)) polarity = "all";
            // selection sanity clamp + PascalCase → Halcon lower-case 변환
            if (string.IsNullOrEmpty(selection)) selection = "First";
            string selectionLower;
            if (string.Equals(selection, "Last", StringComparison.OrdinalIgnoreCase))
            {
                selectionLower = "last";
            }
            else if (string.Equals(selection, "All", StringComparison.OrdinalIgnoreCase))
            {
                selectionLower = "all";
            }
            else
            {
                selectionLower = "first";
            }
            if (radius <= 0)          { error = "radius must be > 0"; return false; }

            // Datum transform (center 만 변환, radius 는 무변환)
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

                // strip half-extent cap: 큰 ratio/radius → strip 거대 → MeasurePos edge 노이즈 → "insufficient polar samples".
                double halfL1 = Math.Min(radius * rectL1Ratio, CircleStripHalfExtentCapPx);
                double halfL2 = Math.Min(radius * rectL2Ratio, CircleStripHalfExtentCapPx);
                if (halfL1 < 1.0) halfL1 = 1.0;
                if (halfL2 < 1.0) halfL2 = 1.0;

                HTuple allRows = new HTuple();
                HTuple allCols = new HTuple();

                int stepCount = (int)Math.Round(360.0 / stepDeg);
                bool[] strips = new bool[stepCount];
                for (int i = 0; i < stepCount; i++)
                {
                    double thetaDeg = i * stepDeg;
                    double thetaRad = thetaDeg * Math.PI / 180.0;
                    // 화면 CCW 좌표계 (sin 앞 minus)
                    double rectRow = cRow - radius * Math.Sin(thetaRad);
                    double rectCol = cCol + radius * Math.Cos(thetaRad);
                    double rectPhi = thetaRad; // 반경 방향 = rect length1 축

                    HObject horotteRect;
                    HOperatorSet.GenRectangle2(out horotteRect, rectRow, rectCol, rectPhi, halfL1, halfL2);

                    HTuple measureHandle = null;
                    try
                    {
                        HOperatorSet.GenMeasureRectangle2(
                            rectRow, rectCol, rectPhi, halfL1, halfL2,
                            imageWidth, imageHeight, "nearest_neighbor",
                            out measureHandle);

                        HTuple eRows, eCols, amp, dist;
                        HOperatorSet.MeasurePos(image, measureHandle,
                            sigma, threshold, polarity, selectionLower,
                            out eRows, out eCols, out amp, out dist);

                        if (eRows.TupleLength() > 0 && eCols.TupleLength() > 0)
                        {
                            strips[i] = true;
                            // selection 정책 분기: First/Last 는 단일점 누적(stepCount 보존), All 은 전체 누적
                            if (string.Equals(selectionLower, "all", StringComparison.OrdinalIgnoreCase))
                            {
                                HOperatorSet.TupleConcat(allRows, eRows, out allRows);
                                HOperatorSet.TupleConcat(allCols, eCols, out allCols);
                            }
                            else
                            {
                                // 첫 에지점 1개만 누적 (회전 sweep 의 의도)
                                HOperatorSet.TupleConcat(allRows, eRows[0], out allRows);
                                HOperatorSet.TupleConcat(allCols, eCols[0], out allCols);
                            }
                        }
                    }
                    catch
                    {
                        // per-step 실패 swallow — 나머지 step 계속
                    }
                    finally
                    {
                        if (measureHandle != null)
                        {
                            try { HOperatorSet.CloseMeasure(measureHandle); } catch { }
                        }
                    }
                }

                stripSuccesses = strips;
                edgeRows = allRows;
                edgeCols = allCols;

                if (allRows.TupleLength() < 3)
                {
                    error = "insufficient polar samples (" + allRows.TupleLength() + ")";
                    return false;
                }

                // FitCircleContourXld — atukey robust
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

        // 부호식 4점 smoke harness (sin/cos 부호 회귀 검증용). 좌표 계산 식을 trace 로그로 노출.
        public void RunPhiSmokeTest(HImage image, double centerRow, double centerCol, double radius)
        {
            if (image == null) return;
            // 기대값을 hand-precomputed 독립 reference 로 사용 (sin/cos 부호 회귀 시 delta 발산).
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
                // 화면 CCW 좌표계 (sin 앞 minus, TryFindCircleByPolarSampling 와 동일 식)
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
        public static double DistancePointToLine(
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
        public static double DistancePointToPoint(
            double row1, double col1, double row2, double col2)
        {
            double dr = row2 - row1;
            double dc = col2 - col1;
            return Math.Sqrt(dr * dr + dc * dc);
        }

        /// <summary>
        /// 두 직선 사이의 각도(degree, 0~180). Halcon AngleLl 사용.
        /// </summary>
        public static double AngleLineLine(
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
        public static bool IntersectLines(
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
                if (isOverlapping.I == 1) return false; // collinear
                if (double.IsInfinity(iRow.D) || double.IsInfinity(iCol.D) || // parallel guard
                    double.IsNaN(iRow.D) || double.IsNaN(iCol.D))
                {
                    return false;
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
        /// 측정점(pointRow/Col)에서 datum 기준선까지의 부호 있는 거리(mm)를 반환한다.
        /// lineAngleRad = 측정 대상 datum 기준선 각도 — Y측정=1차(수평)선, X측정=2차(수직)선.
        /// measureAxis 는 부호 규약만 결정: Y=+위쪽 양수, X=+오른쪽 양수.
        /// </summary>
        public static double ComputeProjectionDistance(
            double pointRow, double pointCol,
            double datumOriginRow, double datumOriginCol,
            double lineAngleRad,
            double pixelResolution,
            string measureAxis)
        {
            // foot 미사용 호출 경로: foot 반환 오버로드로 위임
            double footRow, footCol;
            bool footOk;
            return ComputeProjectionDistance(pointRow, pointCol,
                datumOriginRow, datumOriginCol, lineAngleRad,
                pixelResolution, measureAxis,
                out footRow, out footCol, out footOk);
        }

        /// <summary>
        /// ComputeProjectionDistance 오버로드 — datum 기준선 위 수선의 발(foot) 좌표를 함께 반환한다.
        /// overlay(FAI-DistLine: 측정점→foot 수직 드롭선) 표시용. footOk=false 면 projection 실패(거리 0).
        /// </summary>
        public static double ComputeProjectionDistance(
            double pointRow, double pointCol,
            double datumOriginRow, double datumOriginCol,
            double lineAngleRad,
            double pixelResolution,
            string measureAxis,
            out double footRow, out double footCol, out bool footOk)
        {
            // HALCON projection_pl 직접 사용. 거리 = |point - foot| (절대값, 부호 처리 없음).
            //  measureAxis 인자는 caller 호환성 유지용 (내부 미사용).
            footRow = pointRow; footCol = pointCol; footOk = false;

            // datum 라인 2점 (projection_pl 은 직선 정의만 필요, 길이 무관 — ±200px 임의 선택)
            double dirR = Math.Sin(lineAngleRad);
            double dirC = Math.Cos(lineAngleRad);
            double r1 = datumOriginRow - 200.0 * dirR;
            double c1 = datumOriginCol - 200.0 * dirC;
            double r2 = datumOriginRow + 200.0 * dirR;
            double c2 = datumOriginCol + 200.0 * dirC;

            try
            {
                HTuple prRow, prCol;
                HOperatorSet.ProjectionPl(pointRow, pointCol, r1, c1, r2, c2, out prRow, out prCol);
                footRow = prRow.D;
                footCol = prCol.D;
                footOk = true;

                double dr = pointRow - footRow;
                double dc = pointCol - footCol;
                double distPx = Math.Sqrt(dr * dr + dc * dc);
                return distPx * pixelResolution;
            }
            catch
            {
                return 0.0;
            }
        }

        /// <summary>
        /// datum 교점(datumOrigin)을 지나는 기준선(각도 lineAngleRad)의 양 끝점(±halfLength)을 산출한다.
        /// 방향벡터 (sin φ, cos φ). projection_pl axis + CircleCenterDistance overlay(이미지 대각선 길이)가 공유.
        /// </summary>
        public static void GetDatumAxisLine(
            double datumOriginRow, double datumOriginCol,
            double lineAngleRad, double halfLength,
            out double axisR1, out double axisC1, out double axisR2, out double axisC2)
        {
            double dirR = Math.Sin(lineAngleRad); // 라인 방향벡터 (sinφ,cosφ)
            double dirC = Math.Cos(lineAngleRad);
            axisR1 = datumOriginRow - halfLength * dirR;
            axisC1 = datumOriginCol - halfLength * dirC;
            axisR2 = datumOriginRow + halfLength * dirR;
            axisC2 = datumOriginCol + halfLength * dirC;
        }

        /// <summary>
        /// 3점(row/col)으로 외접원을 피팅한다 (GenContourPolygonXld → FitCircleContourXld).
        /// I9/I10 호∩라인 교점 측정에서 arc 3점 피팅에 사용.
        /// </summary>
        //260519 hbk Phase 31 D-01 — 3점 arc 피팅 (GenContourPolygonXld → FitCircleContourXld)
        public bool TryFitArc(
            double p1Row, double p1Col,
            double p2Row, double p2Col,
            double p3Row, double p3Col,
            out double foundRow, out double foundCol, out double foundRadius,
            out string error)
        {
            foundRow = foundCol = foundRadius = 0;
            error = null;
            HObject contour = null;
            try //260519 hbk Phase 31 D-01
            {
                HTuple rows = new HTuple(new double[] { p1Row, p2Row, p3Row }); //260519 hbk Phase 31 D-01
                HTuple cols = new HTuple(new double[] { p1Col, p2Col, p3Col }); //260519 hbk Phase 31 D-01
                HOperatorSet.GenContourPolygonXld(out contour, rows, cols); //260519 hbk Phase 31 D-01
                HTuple cR, cC, rad, startPhi, endPhi, pointOrder;
                HOperatorSet.FitCircleContourXld(contour, "algebraic", -1, 0, 0, 3, 2, //260519 hbk Phase 31 D-01
                    out cR, out cC, out rad, out startPhi, out endPhi, out pointOrder);
                foundRow = cR.D; //260519 hbk Phase 31 D-01
                foundCol = cC.D; //260519 hbk Phase 31 D-01
                foundRadius = rad.D; //260519 hbk Phase 31 D-01
                return true;
            }
            catch (Exception ex) //260519 hbk Phase 31 D-01 — T-31-03: FitCircleContourXld 예외(3점 비수렴) → false, 메모리 누수 차단
            {
                error = ex.Message;
                return false;
            }
            finally //260519 hbk Phase 31 D-01 — T-31-03 mitigation: contour Dispose
            {
                if (contour != null) { try { contour.Dispose(); } catch { } } //260519 hbk Phase 31 D-01
            }
        }

        /// <summary>
        /// Rect ROI 1개에서 Canny 에지 → UnionAdjacentContours → ShapeTransXld("rectangle2") 파이프라인으로
        /// 가장 면적이 큰 사각형 XLD의 중심/각도/장단축 길이를 산출한다 (E2/E3/E9/E10 공통 컨투어 알고리즘).
        /// 사각형 0개 검출 시 예외 throw 없이 error 세팅 후 false 반환 (CONTEXT.md 미해결#4).
        /// </summary>
        //260521 hbk Phase 32 — 공통 컨투어 알고리즘 (E2/E3/E9/E10 공유). LargestRect 산출.
        public bool TryFindLargestContourRect( //260521 hbk Phase 32
            HImage image, //260521 hbk Phase 32
            double roiRow, double roiCol, double roiPhi, double roiLength1, double roiLength2, //260521 hbk Phase 32
            HTuple datumTransform, //260521 hbk Phase 32
            double cannyAlpha, int cannyLow, int cannyHigh, double unionDistance, //260521 hbk Phase 32
            out double centerRow, out double centerCol, out double phi, //260521 hbk Phase 32
            out double length1, out double length2, out string error) //260521 hbk Phase 32
        {
            centerRow = centerCol = phi = length1 = length2 = 0; //260521 hbk Phase 32
            error = null; //260521 hbk Phase 32

            if (image == null) //260521 hbk Phase 32
            {
                error = "image is null"; //260521 hbk Phase 32
                return false; //260521 hbk Phase 32
            }

            HObject rect = null; //260521 hbk Phase 32
            HObject imageReduced = null; //260521 hbk Phase 32
            HObject edges = null; //260521 hbk Phase 32
            HObject unionContours = null; //260521 hbk Phase 32
            HObject rectXld = null; //260521 hbk Phase 32
            HObject largestRect = null; //260521 hbk Phase 32

            try //260521 hbk Phase 32
            {
                // datumTransform 적용 — TryFitLine L45~60 / TryFindCircle L271~282 패턴
                double cRow = roiRow, cCol = roiCol, cPhi = roiPhi; //260521 hbk Phase 32
                if (datumTransform != null && datumTransform.Length > 0) //260521 hbk Phase 32
                {
                    try //260521 hbk Phase 32
                    {
                        HTuple tRow, tCol; //260521 hbk Phase 32
                        HOperatorSet.AffineTransPoint2d(datumTransform, roiRow, roiCol, out tRow, out tCol); //260521 hbk Phase 32
                        cRow = tRow.D; //260521 hbk Phase 32
                        cCol = tCol.D; //260521 hbk Phase 32
                        double rotAngle = Math.Atan2(-datumTransform[1].D, datumTransform[0].D); //260521 hbk Phase 32
                        cPhi = roiPhi + rotAngle; //260521 hbk Phase 32
                    }
                    catch { } //260521 hbk Phase 32 — transform 실패 시 원본 좌표 사용
                }

                // Rect ROI 영역 생성 → domain 축소
                HOperatorSet.GenRectangle2(out rect, cRow, cCol, cPhi, roiLength1, roiLength2); //260521 hbk Phase 32
                HOperatorSet.ReduceDomain(image, rect, out imageReduced); //260521 hbk Phase 32

                // Canny 에지 검출 (사용자 스크립트: edges_sub_pix(imageReduced, edges, 'canny', 1, 20, 40))
                HOperatorSet.EdgesSubPix(imageReduced, out edges, "canny", cannyAlpha, cannyLow, cannyHigh); //260521 hbk Phase 32

                // 인접 컨투어 병합 (사용자 스크립트: union_adjacent_contours_xld(edges, unionContours, 700, 1, 'attr_keep'))
                HOperatorSet.UnionAdjacentContoursXld(edges, out unionContours, unionDistance, 1, "attr_keep"); //260521 hbk Phase 32

                // 각 컨투어를 최소외접사각형 XLD 로 변환 (사용자 스크립트: shape_trans_xld(unionContours, rectXld, 'rectangle2'))
                HOperatorSet.ShapeTransXld(unionContours, out rectXld, "rectangle2"); //260521 hbk Phase 32

                // 각 사각형 XLD 의 면적/중심 산출
                HTuple area, rowC, colC, ptOrder; //260521 hbk Phase 32
                HOperatorSet.AreaCenterXld(rectXld, out area, out rowC, out colC, out ptOrder); //260521 hbk Phase 32

                // 사각형 0개 검출 시 안전 종결 (CONTEXT.md 미해결#4)
                if (area.Length == 0) //260521 hbk Phase 32
                {
                    error = "no contour rectangle detected"; //260521 hbk Phase 32
                    return false; //260521 hbk Phase 32
                }

                // 최대 면적 인덱스 산출 (사용자 스크립트: tuple_max / tuple_find)
                HTuple maxArea, maxIdx; //260521 hbk Phase 32
                HOperatorSet.TupleMax(area, out maxArea); //260521 hbk Phase 32
                HOperatorSet.TupleFind(area, maxArea, out maxIdx); //260521 hbk Phase 32

                // select_obj: HALCON 은 1-based → maxIdx[0].I + 1 정수 인덱스 사용
                HOperatorSet.SelectObj(rectXld, out largestRect, maxIdx[0].I + 1); //260521 hbk Phase 32

                // 가장 면적이 큰 사각형 XLD 에서 중심/각도/장단축 길이 산출
                HTuple cRowT, cColT, phiT, len1T, len2T; //260521 hbk Phase 32
                HOperatorSet.SmallestRectangle2Xld(largestRect, out cRowT, out cColT, out phiT, out len1T, out len2T); //260521 hbk Phase 32

                centerRow = cRowT.D; //260521 hbk Phase 32
                centerCol = cColT.D; //260521 hbk Phase 32
                phi = phiT.D; //260521 hbk Phase 32
                length1 = len1T.D; //260521 hbk Phase 32
                length2 = len2T.D; //260521 hbk Phase 32
                return true; //260521 hbk Phase 32
            }
            catch (Exception ex) //260521 hbk Phase 32 — CONTEXT.md 미해결#4 안전 종결: 예외 throw 금지 (CLAUDE.md 컨벤션)
            {
                error = ex.Message; //260521 hbk Phase 32
                return false; //260521 hbk Phase 32
            }
            finally //260521 hbk Phase 32 — 모든 HObject Dispose (T-32-02 mitigation: 네이티브 메모리 누수 방지)
            {
                if (rect != null) { try { rect.Dispose(); } catch { } } //260521 hbk Phase 32
                if (imageReduced != null) { try { imageReduced.Dispose(); } catch { } } //260521 hbk Phase 32
                if (edges != null) { try { edges.Dispose(); } catch { } } //260521 hbk Phase 32
                if (unionContours != null) { try { unionContours.Dispose(); } catch { } } //260521 hbk Phase 32
                if (rectXld != null) { try { rectXld.Dispose(); } catch { } } //260521 hbk Phase 32
                if (largestRect != null) { try { largestRect.Dispose(); } catch { } } //260521 hbk Phase 32
            }
        }

        /// <summary>
        /// 원과 직선의 교점을 구한다. 2해 중 ROI 중심(roiRow/Col)에 더 가까운 해를 반환한다 (D-10).
        /// HALCON IntersectionLl 은 직선-직선 전용이므로 수학 구현(2차 방정식).
        /// </summary>
        //260519 hbk Phase 31 D-10 — 원-직선 교점 (2해 → ROI 내부 해 선택). T-31-02: 0-나눗셈/음수 판별식 가드
        public static bool TryIntersectCircleLine(
            double cRow, double cCol, double radius,
            double lRow1, double lCol1, double lRow2, double lCol2,
            double roiRow, double roiCol,
            out double intRow, out double intCol)
        {
            intRow = intCol = 0;
            try //260519 hbk Phase 31 D-10
            {
                // 직선 방향벡터: (dR, dC) = (lRow2-lRow1, lCol2-lCol1)
                double dR = lRow2 - lRow1; double dC = lCol2 - lCol1; //260519 hbk Phase 31 D-10
                // 원 중심 → 직선 시작점 벡터: (fR, fC)
                double fR = lRow1 - cRow; double fC = lCol1 - cCol; //260519 hbk Phase 31 D-10
                // 2차 방정식: t²(dR²+dC²) + 2t(fR·dR+fC·dC) + (fR²+fC²-r²) = 0
                double a = dR * dR + dC * dC; //260519 hbk Phase 31 D-10
                if (a < 1e-12) return false; // T-31-02: 직선 길이 0 가드 //260519 hbk Phase 31 D-10
                double b = 2 * (fR * dR + fC * dC); //260519 hbk Phase 31 D-10
                double c = fR * fR + fC * fC - radius * radius; //260519 hbk Phase 31 D-10
                double disc = b * b - 4 * a * c; //260519 hbk Phase 31 D-10
                if (disc < 0) return false; // T-31-02: 교점 없음(음수 판별식) 가드 //260519 hbk Phase 31 D-10
                double sqrtDisc = Math.Sqrt(disc); //260519 hbk Phase 31 D-10
                double t1 = (-b - sqrtDisc) / (2 * a); //260519 hbk Phase 31 D-10
                double t2 = (-b + sqrtDisc) / (2 * a); //260519 hbk Phase 31 D-10
                double s1R = lRow1 + t1 * dR; double s1C = lCol1 + t1 * dC; //260519 hbk Phase 31 D-10
                double s2R = lRow1 + t2 * dR; double s2C = lCol1 + t2 * dC; //260519 hbk Phase 31 D-10
                // D-10: ROI 중심에 더 가까운 해 선택
                double d1 = (s1R - roiRow) * (s1R - roiRow) + (s1C - roiCol) * (s1C - roiCol); //260519 hbk Phase 31 D-10
                double d2 = (s2R - roiRow) * (s2R - roiRow) + (s2C - roiCol) * (s2C - roiCol); //260519 hbk Phase 31 D-10
                if (d1 <= d2) { intRow = s1R; intCol = s1C; } //260519 hbk Phase 31 D-10
                else          { intRow = s2R; intCol = s2C; } //260519 hbk Phase 31 D-10
                return true;
            }
            catch //260519 hbk Phase 31 D-10
            {
                return false;
            }
        }

        /// <summary>
        /// 두 직선의 교점을 구한다 (ArcLineIntersect 호출용). 평행/근접/중첩 시 false (측정값 '—').
        /// 기존 static IntersectLines 의 isOverlapping.I==1 / IsInfinity / IsNaN 가드를 그대로 유지하고
        /// 호출 이름만 명확히 한 래퍼 메서드 (CONTEXT.md 미해결#3).
        /// </summary>
        //260521 hbk Phase 32 — 두 직선 교점 (ArcLineIntersect 호출용). 평행/근접/중첩 시 false (측정값 '—').
        public static bool TryIntersectLines( //260521 hbk Phase 32
            double row1a, double col1a, double row1b, double col1b, //260521 hbk Phase 32
            double row2a, double col2a, double row2b, double col2b, //260521 hbk Phase 32
            out double intRow, out double intCol) //260521 hbk Phase 32
        {
            // 기존 IntersectLines 로 위임 — isOverlapping.I==1 / IsInfinity / IsNaN 가드 이미 존재 (본문 무수정)
            return IntersectLines(row1a, col1a, row1b, col1b, //260521 hbk Phase 32
                                  row2a, col2a, row2b, col2b, //260521 hbk Phase 32
                                  out intRow, out intCol); //260521 hbk Phase 32
        }

        /// <summary>
        /// E3 reference algorithm: canny → union_adjacent_contours_xld → shape_trans_xld('rectangle2') →
        /// select_obj(max area) → LargestRect XLD → get_contour_xld 코너 → Edge1Len/Edge2Len 비교로 긴변 선택 →
        /// fit_line_contour_xld('tukey') 로 refined Phi → 중심 통과 phi+π/2 측정선 →
        /// intersection_contours_xld(measureLine, LargestRect, 'all') 로 교점 2개 산출.
        /// 측정 결과값(=교점 간 거리)·overlay 좌표를 한 번에 반환 — 측정 클래스는 HOperatorSet 직접 호출 없음.
        /// </summary>
        //260523 hbk Phase 32 — E3 reference algorithm: LargestRect XLD 기반 긴변 fit + 측정선 contour intersection.
        public bool TryFindShortAxisIntersections( //260523 hbk Phase 32 — E3 reference algorithm
            HImage image, //260523 hbk Phase 32 — E3 reference algorithm
            double roiRow, double roiCol, double roiPhi, double roiLength1, double roiLength2, //260523 hbk Phase 32 — E3 reference algorithm
            HTuple datumTransform, //260523 hbk Phase 32 — E3 reference algorithm
            double cannyAlpha, int cannyLow, int cannyHigh, double unionDistance, //260523 hbk Phase 32 — E3 reference algorithm
            double crossLen, //260523 hbk Phase 32 — E3 reference algorithm: 측정선 반길이 (예: 500 px)
            out double centerRow, out double centerCol, //260523 hbk Phase 32 — E3 reference algorithm
            out double phi, out double length1, out double length2, //260523 hbk Phase 32 — E3 reference algorithm: refined phi(fit_line) + scalar
            out double longEdge1AR, out double longEdge1AC, out double longEdge1BR, out double longEdge1BC, //260523 hbk Phase 32 — E3 reference algorithm: 긴변1 양 끝
            out double longEdge2AR, out double longEdge2AC, out double longEdge2BR, out double longEdge2BC, //260523 hbk Phase 32 — E3 reference algorithm: 긴변2(opposite) 양 끝
            out double measureRow1, out double measureCol1, out double measureRow2, out double measureCol2, //260523 hbk Phase 32 — E3 reference algorithm: 측정선 양 끝
            out double int1Row, out double int1Col, out double int2Row, out double int2Col, //260523 hbk Phase 32 — E3 reference algorithm: 교점 2개
            out string error) //260523 hbk Phase 32 — E3 reference algorithm
        {
            centerRow = centerCol = phi = length1 = length2 = 0; //260523 hbk Phase 32 — E3 reference algorithm
            longEdge1AR = longEdge1AC = longEdge1BR = longEdge1BC = 0; //260523 hbk Phase 32 — E3 reference algorithm
            longEdge2AR = longEdge2AC = longEdge2BR = longEdge2BC = 0; //260523 hbk Phase 32 — E3 reference algorithm
            measureRow1 = measureCol1 = measureRow2 = measureCol2 = 0; //260523 hbk Phase 32 — E3 reference algorithm
            int1Row = int1Col = int2Row = int2Col = 0; //260523 hbk Phase 32 — E3 reference algorithm
            error = null; //260523 hbk Phase 32 — E3 reference algorithm

            if (image == null) //260523 hbk Phase 32 — E3 reference algorithm
            {
                error = "image is null"; //260523 hbk Phase 32 — E3 reference algorithm
                return false; //260523 hbk Phase 32 — E3 reference algorithm
            }

            HObject rect = null; //260523 hbk Phase 32 — E3 reference algorithm
            HObject imageReduced = null; //260523 hbk Phase 32 — E3 reference algorithm
            HObject edges = null; //260523 hbk Phase 32 — E3 reference algorithm
            HObject unionContours = null; //260523 hbk Phase 32 — E3 reference algorithm
            HObject rectXld = null; //260523 hbk Phase 32 — E3 reference algorithm
            HObject largestRect = null; //260523 hbk Phase 32 — E3 reference algorithm
            HObject longEdgeContour = null; //260523 hbk Phase 32 — E3 reference algorithm
            HObject measureLineContour = null; //260523 hbk Phase 32 — E3 reference algorithm

            try //260523 hbk Phase 32 — E3 reference algorithm
            {
                // datumTransform 적용 — TryFindLargestContourRect L802~816 패턴
                double cRow = roiRow, cCol = roiCol, cPhi = roiPhi; //260523 hbk Phase 32 — E3 reference algorithm
                if (datumTransform != null && datumTransform.Length > 0) //260523 hbk Phase 32 — E3 reference algorithm
                {
                    try //260523 hbk Phase 32 — E3 reference algorithm
                    {
                        HTuple tRow, tCol; //260523 hbk Phase 32 — E3 reference algorithm
                        HOperatorSet.AffineTransPoint2d(datumTransform, roiRow, roiCol, out tRow, out tCol); //260523 hbk Phase 32 — E3 reference algorithm
                        cRow = tRow.D; //260523 hbk Phase 32 — E3 reference algorithm
                        cCol = tCol.D; //260523 hbk Phase 32 — E3 reference algorithm
                        double rotAngle = Math.Atan2(-datumTransform[1].D, datumTransform[0].D); //260523 hbk Phase 32 — E3 reference algorithm
                        cPhi = roiPhi + rotAngle; //260523 hbk Phase 32 — E3 reference algorithm
                    }
                    catch { } //260523 hbk Phase 32 — E3 reference algorithm: transform 실패 시 원본 좌표 사용
                }

                // Step 1: ROI 영역 → reduce_domain → edges_sub_pix(canny) → union_adjacent_contours_xld
                HOperatorSet.GenRectangle2(out rect, cRow, cCol, cPhi, roiLength1, roiLength2); //260523 hbk Phase 32 — E3 reference algorithm
                HOperatorSet.ReduceDomain(image, rect, out imageReduced); //260523 hbk Phase 32 — E3 reference algorithm
                HOperatorSet.EdgesSubPix(imageReduced, out edges, "canny", cannyAlpha, cannyLow, cannyHigh); //260523 hbk Phase 32 — E3 reference algorithm
                HOperatorSet.UnionAdjacentContoursXld(edges, out unionContours, unionDistance, 1, "attr_keep"); //260523 hbk Phase 32 — E3 reference algorithm

                // Step 2: shape_trans_xld('rectangle2') → area_center_xld → tuple_max/tuple_find → select_obj(LargestRect)
                HOperatorSet.ShapeTransXld(unionContours, out rectXld, "rectangle2"); //260523 hbk Phase 32 — E3 reference algorithm

                HTuple area, rowC, colC, ptOrder; //260523 hbk Phase 32 — E3 reference algorithm
                HOperatorSet.AreaCenterXld(rectXld, out area, out rowC, out colC, out ptOrder); //260523 hbk Phase 32 — E3 reference algorithm
                if (area.Length == 0) //260523 hbk Phase 32 — E3 reference algorithm
                {
                    error = "no contour rectangle detected"; //260523 hbk Phase 32 — E3 reference algorithm
                    return false; //260523 hbk Phase 32 — E3 reference algorithm
                }

                HTuple maxArea, maxIdx; //260523 hbk Phase 32 — E3 reference algorithm
                HOperatorSet.TupleMax(area, out maxArea); //260523 hbk Phase 32 — E3 reference algorithm
                HOperatorSet.TupleFind(area, maxArea, out maxIdx); //260523 hbk Phase 32 — E3 reference algorithm
                HOperatorSet.SelectObj(rectXld, out largestRect, maxIdx[0].I + 1); //260523 hbk Phase 32 — E3 reference algorithm

                // Step 3: smallest_rectangle2_xld(LargestRect) → center + scalar phi/length1/length2 (phi 는 Step 5 에서 fit_line 결과로 덮어쓴다)
                HTuple cRowT, cColT, phiT, len1T, len2T; //260523 hbk Phase 32 — E3 reference algorithm
                HOperatorSet.SmallestRectangle2Xld(largestRect, out cRowT, out cColT, out phiT, out len1T, out len2T); //260523 hbk Phase 32 — E3 reference algorithm
                centerRow = cRowT.D; //260523 hbk Phase 32 — E3 reference algorithm
                centerCol = cColT.D; //260523 hbk Phase 32 — E3 reference algorithm
                length1 = len1T.D; //260523 hbk Phase 32 — E3 reference algorithm
                length2 = len2T.D; //260523 hbk Phase 32 — E3 reference algorithm

                // Step 4: get_contour_xld → 5 corners (rectangle2 XLD: P0,P1,P2,P3,P0)
                HTuple rows, cols; //260523 hbk Phase 32 — E3 reference algorithm
                HOperatorSet.GetContourXld(largestRect, out rows, out cols); //260523 hbk Phase 32 — E3 reference algorithm
                if (rows.Length < 4) //260523 hbk Phase 32 — E3 reference algorithm
                {
                    error = "LargestRect XLD has " + rows.Length + " points (expected ≥ 4)"; //260523 hbk Phase 32 — E3 reference algorithm
                    return false; //260523 hbk Phase 32 — E3 reference algorithm
                }

                double r0 = rows[0].D, c0 = cols[0].D; //260523 hbk Phase 32 — E3 reference algorithm
                double r1 = rows[1].D, c1 = cols[1].D; //260523 hbk Phase 32 — E3 reference algorithm
                double r2 = rows[2].D, c2 = cols[2].D; //260523 hbk Phase 32 — E3 reference algorithm
                double r3 = rows[3].D, c3 = cols[3].D; //260523 hbk Phase 32 — E3 reference algorithm

                // Edge1Len(P0→P1) vs Edge2Len(P1→P2) — 긴 변 판별 (사용자 reference 스크립트)
                double edge1Len = Math.Sqrt((r1 - r0) * (r1 - r0) + (c1 - c0) * (c1 - c0)); //260523 hbk Phase 32 — E3 reference algorithm
                double edge2Len = Math.Sqrt((r2 - r1) * (r2 - r1) + (c2 - c1) * (c2 - c1)); //260523 hbk Phase 32 — E3 reference algorithm

                if (edge1Len >= edge2Len) //260523 hbk Phase 32 — E3 reference algorithm
                {
                    // 긴변 = P0→P1 (LongEdge1), opposite = P3→P2 (LongEdge2, 평행)
                    longEdge1AR = r0; longEdge1AC = c0; longEdge1BR = r1; longEdge1BC = c1; //260523 hbk Phase 32 — E3 reference algorithm
                    longEdge2AR = r3; longEdge2AC = c3; longEdge2BR = r2; longEdge2BC = c2; //260523 hbk Phase 32 — E3 reference algorithm
                }
                else //260523 hbk Phase 32 — E3 reference algorithm
                {
                    // 긴변 = P1→P2 (LongEdge1), opposite = P0→P3 (LongEdge2, 평행)
                    longEdge1AR = r1; longEdge1AC = c1; longEdge1BR = r2; longEdge1BC = c2; //260523 hbk Phase 32 — E3 reference algorithm
                    longEdge2AR = r0; longEdge2AC = c0; longEdge2BR = r3; longEdge2BC = c3; //260523 hbk Phase 32 — E3 reference algorithm
                }

                // Step 5: gen_contour_polygon_xld(LongEdge) → fit_line_contour_xld('tukey') → atan2 로 refined Phi
                HOperatorSet.GenContourPolygonXld(out longEdgeContour, //260523 hbk Phase 32 — E3 reference algorithm
                    new HTuple(longEdge1AR, longEdge1BR), //260523 hbk Phase 32 — E3 reference algorithm
                    new HTuple(longEdge1AC, longEdge1BC)); //260523 hbk Phase 32 — E3 reference algorithm

                HTuple rowBegin, colBegin, rowEnd, colEnd, lineNr, lineNc, lineDist; //260523 hbk Phase 32 — E3 reference algorithm
                HOperatorSet.FitLineContourXld(longEdgeContour, "tukey", -1, 0, 5, 2, //260523 hbk Phase 32 — E3 reference algorithm
                    out rowBegin, out colBegin, out rowEnd, out colEnd, //260523 hbk Phase 32 — E3 reference algorithm
                    out lineNr, out lineNc, out lineDist); //260523 hbk Phase 32 — E3 reference algorithm

                phi = Math.Atan2(rowEnd.D - rowBegin.D, colEnd.D - colBegin.D); //260523 hbk Phase 32 — E3 reference algorithm: refined Phi from fit_line direction

                // Step 6: PhiPerp = phi + π/2 → 중심 통과 측정선 양 끝 (center ± crossLen)
                double phiPerp = phi + Math.PI / 2.0; //260523 hbk Phase 32 — E3 reference algorithm
                double sinPP = Math.Sin(phiPerp); //260523 hbk Phase 32 — E3 reference algorithm
                double cosPP = Math.Cos(phiPerp); //260523 hbk Phase 32 — E3 reference algorithm
                measureRow1 = centerRow - crossLen * sinPP; //260523 hbk Phase 32 — E3 reference algorithm
                measureCol1 = centerCol - crossLen * cosPP; //260523 hbk Phase 32 — E3 reference algorithm
                measureRow2 = centerRow + crossLen * sinPP; //260523 hbk Phase 32 — E3 reference algorithm
                measureCol2 = centerCol + crossLen * cosPP; //260523 hbk Phase 32 — E3 reference algorithm

                // Step 7: intersection_contours_xld(measureLine, LargestRect, 'all') → 교점 2개
                HOperatorSet.GenContourPolygonXld(out measureLineContour, //260523 hbk Phase 32 — E3 reference algorithm
                    new HTuple(measureRow1, measureRow2), //260523 hbk Phase 32 — E3 reference algorithm
                    new HTuple(measureCol1, measureCol2)); //260523 hbk Phase 32 — E3 reference algorithm

                HTuple iR, iC, isOverlap; //260523 hbk Phase 32 — E3 reference algorithm
                HOperatorSet.IntersectionContoursXld(measureLineContour, largestRect, "all", out iR, out iC, out isOverlap); //260523 hbk Phase 32 — E3 reference algorithm

                if (iR.Length < 2) //260523 hbk Phase 32 — E3 reference algorithm
                {
                    error = "measure line intersects LargestRect at " + iR.Length + " point(s) (expected ≥ 2)"; //260523 hbk Phase 32 — E3 reference algorithm
                    return false; //260523 hbk Phase 32 — E3 reference algorithm
                }

                int1Row = iR[0].D; int1Col = iC[0].D; //260523 hbk Phase 32 — E3 reference algorithm
                int2Row = iR[1].D; int2Col = iC[1].D; //260523 hbk Phase 32 — E3 reference algorithm
                return true; //260523 hbk Phase 32 — E3 reference algorithm
            }
            catch (Exception ex) //260523 hbk Phase 32 — E3 reference algorithm: HALCON 예외 → false (시퀀스 스레드 무크래시)
            {
                error = ex.Message; //260523 hbk Phase 32 — E3 reference algorithm
                return false; //260523 hbk Phase 32 — E3 reference algorithm
            }
            finally //260523 hbk Phase 32 — E3 reference algorithm: 모든 HObject Dispose (T-32-02 패턴)
            {
                if (rect != null) { try { rect.Dispose(); } catch { } } //260523 hbk Phase 32 — E3 reference algorithm
                if (imageReduced != null) { try { imageReduced.Dispose(); } catch { } } //260523 hbk Phase 32 — E3 reference algorithm
                if (edges != null) { try { edges.Dispose(); } catch { } } //260523 hbk Phase 32 — E3 reference algorithm
                if (unionContours != null) { try { unionContours.Dispose(); } catch { } } //260523 hbk Phase 32 — E3 reference algorithm
                if (rectXld != null) { try { rectXld.Dispose(); } catch { } } //260523 hbk Phase 32 — E3 reference algorithm
                if (largestRect != null) { try { largestRect.Dispose(); } catch { } } //260523 hbk Phase 32 — E3 reference algorithm
                if (longEdgeContour != null) { try { longEdgeContour.Dispose(); } catch { } } //260523 hbk Phase 32 — E3 reference algorithm
                if (measureLineContour != null) { try { measureLineContour.Dispose(); } catch { } } //260523 hbk Phase 32 — E3 reference algorithm
            }
        }

        /// <summary>
        /// 단축 방향 선분과 사각형 XLD 컨투어의 교점 2개를 산출한다 (E3 단축 거리 측정용).
        /// 교점 0개 또는 1개이면 false (CONTEXT.md 미해결#3 안전 종결).
        /// rectContour 는 호출측(E3 측정 클래스)이 소유/Dispose — 본 메서드는 Dispose 하지 않는다.
        /// </summary>
        //260521 hbk Phase 32 — 단축선 ↔ 사각형 XLD 교점 2점 (E3 단축 거리). 0교점/1교점 시 false (안전 종결).
        public bool TryIntersectContours( //260521 hbk Phase 32
            HObject rectContour, //260521 hbk Phase 32
            double lineRow1, double lineCol1, double lineRow2, double lineCol2, //260521 hbk Phase 32
            out double iRow1, out double iCol1, out double iRow2, out double iCol2, //260521 hbk Phase 32
            out string error) //260521 hbk Phase 32
        {
            iRow1 = iCol1 = iRow2 = iCol2 = 0; //260521 hbk Phase 32
            error = null; //260521 hbk Phase 32

            HObject lineContour = null; //260521 hbk Phase 32
            HObject intersectionPoints = null; //260521 hbk Phase 32
            try //260521 hbk Phase 32
            {
                // 단축 방향 선분을 XLD 컨투어로 변환 (TryFitArc L750 의 GenContourPolygonXld 패턴)
                HOperatorSet.GenContourPolygonXld( //260521 hbk Phase 32
                    out lineContour, //260521 hbk Phase 32
                    new HTuple(lineRow1, lineRow2), //260521 hbk Phase 32
                    new HTuple(lineCol1, lineCol2)); //260521 hbk Phase 32

                // 두 컨투어의 교점 산출 (HALCON intersection_contours_xld — out isOverlapping 포함 3-out 시그니처)
                HTuple iR, iC, isOverlap; //260521 hbk Phase 32
                HOperatorSet.IntersectionContoursXld(rectContour, lineContour, "mutual", out iR, out iC, out isOverlap); //260521 hbk Phase 32

                // 교점 2개 미만 시 안전 종결 (CONTEXT.md 미해결#3)
                if (iR.Length < 2) //260521 hbk Phase 32
                {
                    error = "short-axis line intersects rectangle at " + iR.Length + " point(s)"; //260521 hbk Phase 32
                    return false; //260521 hbk Phase 32
                }

                iRow1 = iR[0].D; //260521 hbk Phase 32
                iCol1 = iC[0].D; //260521 hbk Phase 32
                iRow2 = iR[1].D; //260521 hbk Phase 32
                iCol2 = iC[1].D; //260521 hbk Phase 32
                return true; //260521 hbk Phase 32
            }
            catch (Exception ex) //260521 hbk Phase 32 — T-32-01 mitigation: HALCON 예외가 검사 스레드를 크래시시키지 않음
            {
                error = ex.Message; //260521 hbk Phase 32
                return false; //260521 hbk Phase 32
            }
            finally //260521 hbk Phase 32 — T-32-02 mitigation: 네이티브 메모리 누수 방지
            {
                if (lineContour != null) { try { lineContour.Dispose(); } catch { } } //260521 hbk Phase 32
                if (intersectionPoints != null) { try { intersectionPoints.Dispose(); } catch { } } //260521 hbk Phase 32
                // 주의: rectContour 는 호출측이 소유/Dispose — 본 메서드에서 Dispose 하지 않음 //260521 hbk Phase 32
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
