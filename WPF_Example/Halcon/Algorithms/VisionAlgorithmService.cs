//260413 hbk Phase 6: Halcon 빌딩 블록 서비스 — FitLine, FindCircle, 기하 유틸 (D-18)
using System;
using System.Collections.Generic; //260519 hbk Phase 31 hotfix#5 — TryFitLine collectedEdges 인자
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
            out string error,
            string selection = "all", //260512 hbk Phase 23 ALG-01 — D-10 EdgeSelection 명시 (optional, 기존 caller 호환)
            List<ValueTuple<double, double>> collectedEdges = null) //260519 hbk Phase 31 hotfix#5 — strip-loop 누적 raw 에지점 노출 (opt-in, 기존 caller 호환). 측정 overlay 가시화용.
        {
            row1 = col1 = row2 = col2 = 0;
            error = null;

            if (image == null)
            {
                error = "image is null";
                return false;
            }

            HObject contour = null; //260517 hbk — measureHandle 제거: strip 헬퍼가 strip별 handle 관리
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

                //260509 hbk Phase 20 — ?: → if/else (D-01)
                string pol;
                if (string.Equals(polarity, "LightToDark", StringComparison.OrdinalIgnoreCase))
                {
                    pol = "negative";
                }
                else
                {
                    pol = "positive";
                }

                //260512 hbk Phase 23 ALG-01 — D-10 EdgeSelection 명시 처리 (TryFindCircleByPolarSampling L249-264 패턴 차용)
                string measureSel; //260512 hbk Phase 23 ALG-01
                if (string.Equals(selection, "First", StringComparison.OrdinalIgnoreCase)) //260512 hbk Phase 23 ALG-01
                {
                    measureSel = "first";
                }
                else if (string.Equals(selection, "Last", StringComparison.OrdinalIgnoreCase)) //260512 hbk Phase 23 ALG-01
                {
                    measureSel = "last";
                }
                else
                {
                    measureSel = "all";
                }

                //260517 hbk — 단일 MeasurePos → strip-loop 누적 (CO-23-01 구조적 차단 제거)
                //  근본 원인: 단일 MeasurePos 는 측정 축 1개에서만 에지 반환 → FitLineContourXld 입력 1~2점
                //  → "insufficient edge points (1)" → TryFitLine false → 측정값 '—'.
                //  해결: ROI 를 stripCount 개 strip 으로 쪼개 strip 마다 MeasurePos 누적 (DatumFindingService.TryFindLine 패턴).
                //  rPhi 회전은 strip region 회전 대신 measurePhi 로 흡수 (축 정렬 strip + 회전된 측정 축).
                //  datum 회전 ROI 도 동일 경로.
                double halfW = roiLength1; //260517 hbk
                double halfH = roiLength2; //260517 hbk
                double top = rRow - halfH; //260517 hbk
                double bottom = rRow + halfH; //260517 hbk
                double left = rCol - halfW; //260517 hbk
                double right = rCol + halfW; //260517 hbk
                double widthPx = right - left; //260517 hbk
                double heightPx = bottom - top; //260517 hbk

                //260517 hbk — CANONICAL: DatumFindingService.TryFindLine stripCount 산출 (sentinel 0 → 기본 20)
                int stripCount = 20; //260517 hbk
                if (sampleCount > 0) stripCount = sampleCount; //260517 hbk
                if (stripCount < 1) stripCount = 1; //260517 hbk

                HTuple allRows = new HTuple(); //260517 hbk
                HTuple allCols = new HTuple(); //260517 hbk

                if (scanHorizontal) //260517 hbk
                {
                    for (int i = 0; i < stripCount; i++) //260517 hbk
                    {
                        double r1 = top + (i * heightPx / stripCount); //260517 hbk
                        double r2 = top + ((i + 1) * heightPx / stripCount); //260517 hbk
                        AppendStrip(image, r1, left, r2, right, imageWidth, imageHeight,
                            Math.Max(0.4, sigma), Math.Max(1, threshold), pol, measurePhi, measureSel,
                            ref allRows, ref allCols); //260517 hbk
                    }
                }
                else //260517 hbk
                {
                    for (int i = 0; i < stripCount; i++) //260517 hbk
                    {
                        double c1 = left + (i * widthPx / stripCount); //260517 hbk
                        double c2 = left + ((i + 1) * widthPx / stripCount); //260517 hbk
                        AppendStrip(image, top, c1, bottom, c2, imageWidth, imageHeight,
                            Math.Max(0.4, sigma), Math.Max(1, threshold), pol, measurePhi, measureSel,
                            ref allRows, ref allCols); //260517 hbk
                    }
                }

                //260517 hbk — CANONICAL: TrimCount 적용 (누적 점 양 끝 제거)
                int edgeCount = allRows.TupleLength(); //260517 hbk
                if (trimCount > 0 && edgeCount > 2 * trimCount + 1) //260517 hbk
                {
                    HTuple trimmedR = allRows.TupleSelectRange(trimCount, edgeCount - trimCount - 1); //260517 hbk
                    HTuple trimmedC = allCols.TupleSelectRange(trimCount, edgeCount - trimCount - 1); //260517 hbk
                    allRows = trimmedR; //260517 hbk
                    allCols = trimmedC; //260517 hbk
                    edgeCount = allRows.TupleLength(); //260517 hbk
                }

                //260517 hbk — edge 개수 게이트: strip 누적 후에도 2점 미만이면 실패
                if (edgeCount < 2) //260517 hbk
                {
                    error = "insufficient edge points (" + edgeCount + ") across " + stripCount + " strips"; //260517 hbk
                    return false;
                }

                HOperatorSet.GenContourPolygonXld(out contour, allRows, allCols); //260517 hbk
                HTuple lr1, lc1, lr2, lc2, nr, nc, df;
                HOperatorSet.FitLineContourXld(contour, "tukey", -1, 0, 5, 2,
                    out lr1, out lc1, out lr2, out lc2, out nr, out nc, out df);

                row1 = lr1.D; col1 = lc1.D;
                row2 = lr2.D; col2 = lc2.D;

                //260519 hbk Phase 31 hotfix#5 — opt-in: 라인 피팅에 사용된 trim 후 raw 에지점들을 caller list 에 누적.
                //  EdgeToLineAngle overlay 가시화 — 측정 라인이 어느 점들로 피팅되었는지 사용자 시각 검증용.
                if (collectedEdges != null) //260519 hbk Phase 31 hotfix#5
                {
                    for (int k = 0; k < edgeCount; k++) //260519 hbk Phase 31 hotfix#5
                    {
                        collectedEdges.Add(new ValueTuple<double, double>(allRows[k].D, allCols[k].D)); //260519 hbk Phase 31 hotfix#5
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

        //260517 hbk — 단일 strip 에서 MeasurePos 실행 후 edge 점 누적.
        //  CANONICAL: DatumFindingService.AppendEdgePointsFromStrip. DatumFindingService 의 헬퍼는 private 라 직접 호출 불가
        //  → 동등 구현을 VisionAlgorithmService 내부에 작성. 헬퍼 위치 결정 옵션 (a): 단순함 우선, 공유 추출 없음.
        //  polarity 차이: 이 헬퍼는 Halcon polarity 문자열("positive"/"negative")을 그대로 받는다 (caller 에서 매핑 완료).
        //  measurePhi 차이: caller 가 direction 매핑 + rPhi 회전 보정을 합산한 값을 전달
        //    → 헬퍼는 SmallestRectangle2 자동 phi(rp) 를 쓰지 않고 전달받은 measurePhi 만 사용.
        //  strip 실패(빈 결과 / 예외)는 swallow — 한 strip 실패가 전체 ROI 를 중단시키지 않음.
        private void AppendStrip( //260517 hbk
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
                HOperatorSet.GenRectangle1(out stripRegion, row1, col1, row2, col2); //260517 hbk
                HTuple rr, rc, rp, rh, rw;
                HOperatorSet.SmallestRectangle2(stripRegion, out rr, out rc, out rp, out rh, out rw); //260517 hbk — rp(자동 phi)는 미사용; rr/rc/rh/rw 만 사용
                HOperatorSet.GenMeasureRectangle2(
                    rr, rc, measurePhi, rh, rw, //260517 hbk — measurePhi = direction 매핑 + rPhi 회전 보정 합산값
                    imageWidth, imageHeight, "nearest_neighbor",
                    out measureHandle);
                HTuple edgeRows, edgeCols, amp, dist;
                HOperatorSet.MeasurePos(
                    image, measureHandle, sigma, threshold,
                    polarity, selection, //260517 hbk — polarity: Halcon 문자열 직접 사용 (caller 에서 매핑 완료)
                    out edgeRows, out edgeCols, out amp, out dist);
                if (edgeRows.TupleLength() <= 0 || edgeCols.TupleLength() <= 0)
                {
                    return;
                }
                HOperatorSet.TupleConcat(allRows, edgeRows, out allRows); //260517 hbk
                HOperatorSet.TupleConcat(allCols, edgeCols, out allCols); //260517 hbk
            }
            catch
            {
            }
            finally
            {
                if (measureHandle != null) { try { HOperatorSet.CloseMeasure(measureHandle); } catch { } } //260517 hbk
                if (stripRegion != null) { try { stripRegion.Dispose(); } catch { } } //260517 hbk
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

        //260519 hbk Phase 31 hotfix — polar strip half-extent cap (px). Phase 16 의 12px 고정값을 상향(사용자 결정).
        //  큰 strip = MeasurePos 노이즈 → 측정 실패를 막는 cap 은 유지하되, RectL1/L2Ratio 변경이 더 넓은 범위에서
        //  strip 크기·측정에 반영되도록 상한을 키운다. HalconDisplayService.RenderCircleStrips 와 공유 (WYSIWYG).
        public const double CircleStripHalfExtentCapPx = 36.0; //260519 hbk Phase 31 hotfix

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
            out bool[] stripSuccesses, //260505 hbk Phase 18 CO-05 — per-strip 검출 성공 여부
            out string error)
        {
            foundRow = 0; foundCol = 0; foundRadius = 0;
            edgeRows = new HTuple();
            edgeCols = new HTuple();
            stripSuccesses = null; //260505 hbk Phase 18 CO-05
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
            //260509 hbk Phase 20 — chained ?: → if/else if/else (D-01)
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

                //260430 hbk Quick 260430-hox / 260519 hbk Phase 31 hotfix — strip half-extent cap (CircleStripHalfExtentCapPx).
                //  Phase 16 UAT FAIL root cause: 큰 ratio/radius → strip 거대 → MeasurePos edge 노이즈 → "insufficient polar samples".
                double halfL1 = Math.Min(radius * rectL1Ratio, CircleStripHalfExtentCapPx);
                double halfL2 = Math.Min(radius * rectL2Ratio, CircleStripHalfExtentCapPx);
                if (halfL1 < 1.0) halfL1 = 1.0;
                if (halfL2 < 1.0) halfL2 = 1.0;

                HTuple allRows = new HTuple();
                HTuple allCols = new HTuple();

                int stepCount = (int)Math.Round(360.0 / stepDeg);
                bool[] strips = new bool[stepCount]; //260505 hbk Phase 18 CO-05 — per-strip 성공 여부 배열
                for (int i = 0; i < stepCount; i++)
                {
                    double thetaDeg = i * stepDeg;
                    double thetaRad = thetaDeg * Math.PI / 180.0;
                    //260426 hbk Phase 14-04 D-13 — 화면 CCW 좌표계 (sin 앞 minus)
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
                            sigma, threshold, polarity, selectionLower, //260429 hbk Phase 15 — "all" 하드코딩 → caller selection 반영
                            out eRows, out eCols, out amp, out dist);

                        if (eRows.TupleLength() > 0 && eCols.TupleLength() > 0)
                        {
                            strips[i] = true; //260505 hbk Phase 18 CO-05 — 이 1strip 검출 성공
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

                stripSuccesses = strips; //260505 hbk Phase 18 CO-05 — 호출자에게 per-strip 결과 전달
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
        /// 측정점(pointRow/Col)에서 datum 기준선까지의 부호 있는 거리(mm)를 반환한다.
        /// lineAngleRad = 측정 대상 datum 기준선 각도 — Y측정=1차(수평)선, X측정=2차(수직)선.
        /// measureAxis 는 부호 규약만 결정: Y=+위쪽 양수, X=+오른쪽 양수.
        /// </summary>
        //260519 hbk Phase 31 D-04 — projection_pl 거리 공용 헬퍼
        //260519 hbk Phase 31 hotfix#3 — datumAngleRad → lineAngleRad: 호출측이 축별 기준선 각도를 직접 전달
        public static double ComputeProjectionDistance(
            double pointRow, double pointCol,
            double datumOriginRow, double datumOriginCol,
            double lineAngleRad,
            double pixelResolution,
            string measureAxis)
        {
            //260519 hbk Phase 31 hotfix — foot 미사용 호출 경로: foot 반환 오버로드로 위임
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
        //260519 hbk Phase 31 hotfix — foot 반환 오버로드 (CircleCenterDistance overlay 결함 A/B fix)
        public static double ComputeProjectionDistance(
            double pointRow, double pointCol,
            double datumOriginRow, double datumOriginCol,
            double lineAngleRad,
            double pixelResolution,
            string measureAxis,
            out double footRow, out double footCol, out bool footOk)
        {
            //260520 hbk Phase 31 simplify — HALCON projection_pl 직접 사용. 거리 = |point - foot| (절대값).
            //  사용자 결정: 부호 처리 제거 → 단순 sqrt 거리. 공차 판정은 |측정 - 설계| 로 별도 처리.
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
        //260519 hbk Phase 31 hotfix — datum 기준선 2점 산출 헬퍼 (projection_pl axis + overlay 공용)
        //260519 hbk Phase 31 hotfix#3 — measureAxis 분기 제거: 단일 lineAngleRad 입력 (호출측이 1차/2차 각도 선택)
        public static void GetDatumAxisLine(
            double datumOriginRow, double datumOriginCol,
            double lineAngleRad, double halfLength,
            out double axisR1, out double axisC1, out double axisR2, out double axisC2)
        {
            double dirR = Math.Sin(lineAngleRad); //260519 hbk Phase 31 hotfix#3 — 라인 방향벡터 (sinφ,cosφ)
            double dirC = Math.Cos(lineAngleRad); //260519 hbk Phase 31 hotfix#3
            axisR1 = datumOriginRow - halfLength * dirR; //260519 hbk Phase 31 hotfix#3
            axisC1 = datumOriginCol - halfLength * dirC; //260519 hbk Phase 31 hotfix#3
            axisR2 = datumOriginRow + halfLength * dirR; //260519 hbk Phase 31 hotfix#3
            axisC2 = datumOriginCol + halfLength * dirC; //260519 hbk Phase 31 hotfix#3
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
