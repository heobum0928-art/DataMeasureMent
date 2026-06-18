using System;
using System.Collections.Generic;
using HalconDotNet;
using ReringProject.Halcon.Models;
using ReringProject.Sequence;

namespace ReringProject.Halcon.Algorithms
{
    /// <summary>
    /// FAIConfig의 ROI 내에서 샘플 스트립 분할 + MeasurePos + FitLineContourXld로
    /// 에지 라인을 검출하고 두 라인 간 수직 거리(mm)를 산출하는 서비스.
    /// MeasurementAlgorithm.TryInspectSingleEdgeInternal 패턴을 따른다.
    /// </summary>
    public class FAIEdgeMeasurementService
    {
        /// <summary>
        /// FAIConfig ROI 영역에서 샘플 스트립 방식으로 에지를 측정한다.
        /// EdgeSelection=Both: 두 피팅 라인 간 수직 거리를 산출.
        /// EdgeSelection=First/Last: 단일 피팅 라인, distance=0.
        /// transform=null이면 원본 ROI 좌표 그대로 사용.
        /// </summary>
        public bool TryMeasure(HImage image, FAIConfig fai, out FAIEdgeMeasurementResult result)
        {
            return TryMeasure(image, fai, null, out result);
        }

        /// <summary>
        /// FAIConfig ROI 영역에서 샘플 스트립 방식으로 에지를 측정한다.
        /// transform: Datum hom_mat2d 변환 행렬. null이면 원본 ROI 좌표 사용.
        /// </summary>
        public bool TryMeasure(HImage image, FAIConfig fai, HTuple transform, out FAIEdgeMeasurementResult result)
        {
            result = null;

            if (image == null || fai == null)
            {
                return false;
            }

            if (fai.ROI_Length1 <= 0 || fai.ROI_Length2 <= 0)
            {
                return false;
            }

            try
            {
                double roiRow = fai.ROI_Row;
                double roiCol = fai.ROI_Col;
                double roiPhi = fai.ROI_Phi;

                // Datum hom_mat2d 변환을 ROI 에 적용
                //260618 hbk Phase 54 ALIGN-01: 점 변환+phi 가산이 아니라 ROI 영역(gen_rectangle2)을
                //  affine_trans_region 으로 이동/회전 후 pose 재추출 (사용자 요청). 에지 추출 알고리즘(strip+measure_pos)은 무변경.
                if (transform != null && transform.Length > 0)
                {
                    HObject roiRegion = null;
                    HObject roiRegionTrans = null;
                    try
                    {
                        HOperatorSet.GenRectangle2(out roiRegion, fai.ROI_Row, fai.ROI_Col, fai.ROI_Phi,
                            fai.ROI_Length1, fai.ROI_Length2);
                        HOperatorSet.AffineTransRegion(roiRegion, out roiRegionTrans, transform, "nearest_neighbor");
                        HTuple sr, sc, sp, sl1, sl2;
                        HOperatorSet.SmallestRectangle2(roiRegionTrans, out sr, out sc, out sp, out sl1, out sl2);
                        roiRow = sr.D;
                        roiCol = sc.D;
                        // smallest_rectangle2 는 긴 변을 따라 phi 정의. 원본 ROI 가 Length2(짧은축 기준)인 경우 90° 어긋날 수 있어
                        // gen_rectangle2 규약(Length1=phi축)과 맞추기 위해 길이 순서 기준으로 phi 보정.
                        if (fai.ROI_Length1 >= fai.ROI_Length2) roiPhi = sp.D;
                        else roiPhi = sp.D + (Math.PI / 2.0);
                    }
                    catch
                    {
                        // Transform failed — use original ROI coordinates
                    }
                    finally
                    {
                        if (roiRegion != null) { try { roiRegion.Dispose(); } catch { } }
                        if (roiRegionTrans != null) { try { roiRegionTrans.Dispose(); } catch { } }
                    }
                }

                HTuple imageWidth, imageHeight;
                image.GetImageSize(out imageWidth, out imageHeight);

                // EdgeDirection -> scanHorizontal, measurePhi 변환
                string edgeDir = fai.EdgeDirection;
                if (edgeDir == null) edgeDir = "LtoR";
                bool scanHorizontal = string.Equals(edgeDir, "LtoR", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(edgeDir, "RtoL", StringComparison.OrdinalIgnoreCase);

                double measurePhi;
                if (string.Equals(edgeDir, "TtoB", StringComparison.OrdinalIgnoreCase))
                {
                    measurePhi = -Math.PI / 2.0;
                }
                else if (string.Equals(edgeDir, "RtoL", StringComparison.OrdinalIgnoreCase))
                {
                    measurePhi = Math.PI;
                }
                else if (string.Equals(edgeDir, "BtoT", StringComparison.OrdinalIgnoreCase))
                {
                    measurePhi = Math.PI / 2.0;
                }
                else
                {
                    measurePhi = 0.0;
                }

                // roiPhi 기반 measurePhi 회전 보정
                measurePhi = measurePhi + (roiPhi - fai.ROI_Phi);

                int sampleCount = Math.Max(1, fai.EdgeSampleCount);
                double sigma = Math.Max(0.4, fai.Sigma);
                int threshold = Math.Max(1, fai.EdgeThreshold);

                string polarity;
                if (string.Equals(fai.EdgePolarity, "LightToDark", StringComparison.OrdinalIgnoreCase)) polarity = "negative";
                else polarity = "positive";

                // ROI 바운딩 박스 (Rectangle2 -> AABB)
                double sinPhi = Math.Sin(roiPhi);
                double cosPhi = Math.Cos(roiPhi);
                double dRow = Math.Abs(fai.ROI_Length1 * cosPhi) + Math.Abs(fai.ROI_Length2 * sinPhi);
                double dCol = Math.Abs(fai.ROI_Length1 * sinPhi) + Math.Abs(fai.ROI_Length2 * cosPhi);
                double top = roiRow - dRow;
                double bottom = roiRow + dRow;
                double left = roiCol - dCol;
                double right = roiCol + dCol;

                if ((bottom - top) < 2.0 || (right - left) < 2.0)
                {
                    return false;
                }

                string edgeSel = fai.EdgeSelection;
                if (edgeSel == null) edgeSel = "First";
                bool isBoth = string.Equals(edgeSel, "Both", StringComparison.OrdinalIgnoreCase);
                bool isLast = string.Equals(edgeSel, "Last", StringComparison.OrdinalIgnoreCase);

                if (isBoth)
                {
                    return TryMeasureBoth(
                        image, imageWidth, imageHeight,
                        scanHorizontal, measurePhi, sampleCount, sigma, threshold, polarity,
                        top, bottom, left, right,
                        fai, out result);
                }
                else
                {
                    return TryMeasureSingle(
                        image, imageWidth, imageHeight,
                        scanHorizontal, measurePhi, sampleCount, sigma, threshold, polarity,
                        top, bottom, left, right,
                        isLast, fai, out result);
                }
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// EdgeSelection=Both: 첫 번째/마지막 에지 포인트를 분리 수집, 각각 라인 피팅 후 수직 거리 산출
        /// </summary>
        private bool TryMeasureBoth(
            HImage image, HTuple imageWidth, HTuple imageHeight,
            bool scanHorizontal, double measurePhi, int sampleCount,
            double sigma, int threshold, string polarity,
            double top, double bottom, double left, double right,
            FAIConfig fai, out FAIEdgeMeasurementResult result)
        {
            result = null;

            var firstRows = new HTuple();
            var firstCols = new HTuple();
            var lastRows = new HTuple();
            var lastCols = new HTuple();
            int totalEdgePoints = 0;

            for (int i = 0; i < sampleCount; i++)
            {
                HObject stripRegion = null;
                HTuple handle = null;
                try
                {
                    if (scanHorizontal)
                    {
                        double row1 = top + (i * (bottom - top) / sampleCount);
                        double row2 = top + ((i + 1) * (bottom - top) / sampleCount);
                        HOperatorSet.GenRectangle1(out stripRegion, row1, left, row2, right);
                    }
                    else
                    {
                        double col1 = left + (i * (right - left) / sampleCount);
                        double col2 = left + ((i + 1) * (right - left) / sampleCount);
                        HOperatorSet.GenRectangle1(out stripRegion, top, col1, bottom, col2);
                    }

                    HTuple rr, rc, rp, rh, rw;
                    HOperatorSet.SmallestRectangle2(stripRegion, out rr, out rc, out rp, out rh, out rw);
                    HOperatorSet.GenMeasureRectangle2(rr, rc, measurePhi, rh, rw,
                        imageWidth, imageHeight, "nearest_neighbor", out handle);

                    // select="all": 스트립 내 모든 에지 검출
                    HTuple rows, cols, amp, dist;
                    HOperatorSet.MeasurePos(image, handle, sigma, threshold,
                        polarity, "all", out rows, out cols, out amp, out dist);

                    int edgeCount = rows.TupleLength();
                    totalEdgePoints += edgeCount;

                    if (edgeCount >= 2)
                    {
                        // 첫 번째 에지 -> firstEdge, 마지막 에지 -> lastEdge
                        HOperatorSet.TupleConcat(firstRows, rows[0], out firstRows);
                        HOperatorSet.TupleConcat(firstCols, cols[0], out firstCols);
                        HOperatorSet.TupleConcat(lastRows, rows[edgeCount - 1], out lastRows);
                        HOperatorSet.TupleConcat(lastCols, cols[edgeCount - 1], out lastCols);
                    }
                    else if (edgeCount == 1)
                    {
                        HOperatorSet.TupleConcat(firstRows, rows[0], out firstRows);
                        HOperatorSet.TupleConcat(firstCols, cols[0], out firstCols);
                    }
                }
                catch // 개별 스트립 실패는 무시
                {
                }
                finally
                {
                    if (handle != null)
                    {
                        try { HOperatorSet.CloseMeasure(handle); } catch { }
                    }
                    if (stripRegion != null)
                    {
                        try { stripRegion.Dispose(); } catch { }
                    }
                }
            }

            int trimCount = fai.EdgeTrimCount;
            if (trimCount > 0)
            {
                TrimExtremePoints(ref firstRows, ref firstCols, scanHorizontal, trimCount);
                TrimExtremePoints(ref lastRows, ref lastCols, scanHorizontal, trimCount);
            }

            if (firstRows.TupleLength() <= 1 || lastRows.TupleLength() <= 1)
            {
                return false;
            }

            // 라인 1 피팅 (첫 번째 에지 포인트들)
            HObject contour1 = null;
            HObject contour2 = null;
            try
            {
                HOperatorSet.GenContourPolygonXld(out contour1, firstRows, firstCols);
                HTuple lr1, lc1, lr2, lc2, nr1, nc1, df1;
                HOperatorSet.FitLineContourXld(contour1, "tukey", -1, 0, 5, 2,
                    out lr1, out lc1, out lr2, out lc2, out nr1, out nc1, out df1);

                double line1Row1 = lr1.D;
                double line1Col1 = lc1.D;
                double line1Row2 = lr2.D;
                double line1Col2 = lc2.D;

                // 라인 2 피팅 (마지막 에지 포인트들)
                HOperatorSet.GenContourPolygonXld(out contour2, lastRows, lastCols);
                HTuple lr3, lc3, lr4, lc4, nr2, nc2, df2;
                HOperatorSet.FitLineContourXld(contour2, "tukey", -1, 0, 5, 2,
                    out lr3, out lc3, out lr4, out lc4, out nr2, out nc2, out df2);

                double line2Row1 = lr3.D;
                double line2Col1 = lc3.D;
                double line2Row2 = lr4.D;
                double line2Col2 = lc4.D;

                double dx = line1Col2 - line1Col1;
                double dy = line1Row2 - line1Row1;
                double len = Math.Sqrt(dx * dx + dy * dy);
                if (len < 1e-9)
                {
                    return false;
                }

                // 법선 벡터 (라인 1에 수직)
                double nx = -dy / len;
                double ny = dx / len;

                double midRow2 = (line2Row1 + line2Row2) / 2.0;
                double midCol2 = (line2Col1 + line2Col2) / 2.0;

                double midRow1 = (line1Row1 + line1Row2) / 2.0;
                double midCol1 = (line1Col1 + line1Col2) / 2.0;

                // 수직 거리 = 법선 벡터와 (중점2-중점1) 내적의 절대값
                double pixelDist = Math.Abs(nx * (midCol2 - midCol1) + ny * (midRow2 - midRow1));

                // mm 변환: 수평 스캔 -> X축 해상도, 수직 스캔 -> Y축 해상도
                double mmDist;
                if (scanHorizontal) mmDist = pixelDist * fai.PixelResolutionX;
                else mmDist = pixelDist * fai.PixelResolutionY;

                var overlays = BuildOverlaysBoth(
                    line1Row1, line1Col1, line1Row2, line1Col2,
                    line2Row1, line2Col1, line2Row2, line2Col2,
                    midRow1, midCol1, midRow2, midCol2);

                result = new FAIEdgeMeasurementResult
                {
                    Edge1Row = midRow1,
                    Edge1Column = midCol1,
                    Edge2Row = midRow2,
                    Edge2Column = midCol2,
                    Line1Row1 = line1Row1,
                    Line1Column1 = line1Col1,
                    Line1Row2 = line1Row2,
                    Line1Column2 = line1Col2,
                    Line2Row1 = line2Row1,
                    Line2Column1 = line2Col1,
                    Line2Row2 = line2Row2,
                    Line2Column2 = line2Col2,
                    EdgePointCount = totalEdgePoints,
                    DistancePixel = pixelDist,
                    DistanceMm = mmDist,
                    Overlays = overlays
                };

                return true;
            }
            catch
            {
                return false;
            }
            finally
            {
                if (contour1 != null) try { contour1.Dispose(); } catch { }
                if (contour2 != null) try { contour2.Dispose(); } catch { }
            }
        }

        /// <summary>
        /// EdgeSelection=First/Last: 단일 에지 라인 피팅, distance=0
        /// </summary>
        private bool TryMeasureSingle(
            HImage image, HTuple imageWidth, HTuple imageHeight,
            bool scanHorizontal, double measurePhi, int sampleCount,
            double sigma, int threshold, string polarity,
            double top, double bottom, double left, double right,
            bool isLast, FAIConfig fai, out FAIEdgeMeasurementResult result)
        {
            result = null;

            string select;
            if (isLast) select = "last";
            else select = "first";
            var allRows = new HTuple();
            var allCols = new HTuple();
            int totalEdgePoints = 0;

            for (int i = 0; i < sampleCount; i++)
            {
                HObject stripRegion = null;
                HTuple handle = null;
                try
                {
                    if (scanHorizontal)
                    {
                        double row1 = top + (i * (bottom - top) / sampleCount);
                        double row2 = top + ((i + 1) * (bottom - top) / sampleCount);
                        HOperatorSet.GenRectangle1(out stripRegion, row1, left, row2, right);
                    }
                    else
                    {
                        double col1 = left + (i * (right - left) / sampleCount);
                        double col2 = left + ((i + 1) * (right - left) / sampleCount);
                        HOperatorSet.GenRectangle1(out stripRegion, top, col1, bottom, col2);
                    }

                    HTuple rr, rc, rp, rh, rw;
                    HOperatorSet.SmallestRectangle2(stripRegion, out rr, out rc, out rp, out rh, out rw);
                    HOperatorSet.GenMeasureRectangle2(rr, rc, measurePhi, rh, rw,
                        imageWidth, imageHeight, "nearest_neighbor", out handle);

                    HTuple rows, cols, amp, dist;
                    HOperatorSet.MeasurePos(image, handle, sigma, threshold,
                        polarity, select, out rows, out cols, out amp, out dist);

                    int edgeCount = rows.TupleLength();
                    totalEdgePoints += edgeCount;

                    if (edgeCount > 0)
                    {
                        HOperatorSet.TupleConcat(allRows, rows, out allRows);
                        HOperatorSet.TupleConcat(allCols, cols, out allCols);
                    }
                }
                catch
                {
                }
                finally
                {
                    if (handle != null)
                    {
                        try { HOperatorSet.CloseMeasure(handle); } catch { }
                    }
                    if (stripRegion != null)
                    {
                        try { stripRegion.Dispose(); } catch { }
                    }
                }
            }

            if (fai.EdgeTrimCount > 0)
            {
                TrimExtremePoints(ref allRows, ref allCols, scanHorizontal, fai.EdgeTrimCount);
            }

            if (allRows.TupleLength() <= 1)
            {
                return false;
            }

            HObject contour = null;
            try
            {
                HOperatorSet.GenContourPolygonXld(out contour, allRows, allCols);
                HTuple lr1, lc1, lr2, lc2, nr, nc, df;
                HOperatorSet.FitLineContourXld(contour, "tukey", -1, 0, 5, 2,
                    out lr1, out lc1, out lr2, out lc2, out nr, out nc, out df);

                double lineRow1 = lr1.D;
                double lineCol1 = lc1.D;
                double lineRow2 = lr2.D;
                double lineCol2 = lc2.D;
                double midRow = (lineRow1 + lineRow2) / 2.0;
                double midCol = (lineCol1 + lineCol2) / 2.0;

                var overlays = BuildOverlaysSingle(lineRow1, lineCol1, lineRow2, lineCol2);

                // 단일 에지, distance=0
                result = new FAIEdgeMeasurementResult
                {
                    Edge1Row = midRow,
                    Edge1Column = midCol,
                    Edge2Row = 0,
                    Edge2Column = 0,
                    Line1Row1 = lineRow1,
                    Line1Column1 = lineCol1,
                    Line1Row2 = lineRow2,
                    Line1Column2 = lineCol2,
                    Line2Row1 = 0,
                    Line2Column1 = 0,
                    Line2Row2 = 0,
                    Line2Column2 = 0,
                    EdgePointCount = totalEdgePoints,
                    DistancePixel = 0,
                    DistanceMm = 0,
                    Overlays = overlays
                };

                return true;
            }
            catch
            {
                return false;
            }
            finally
            {
                if (contour != null) try { contour.Dispose(); } catch { }
            }
        }

        /// <summary>
        /// Both 모드 오버레이: 라인 1, 라인 2, 거리 연결선
        /// </summary>
        private static List<EdgeInspectionOverlay> BuildOverlaysBoth(
            double l1r1, double l1c1, double l1r2, double l1c2,
            double l2r1, double l2c1, double l2r2, double l2c2,
            double midRow1, double midCol1, double midRow2, double midCol2)
        {
            var overlays = new List<EdgeInspectionOverlay>();

            // 피팅 라인 1
            overlays.Add(new EdgeInspectionOverlay
            {
                RoiId = "FAI-Edge1",
                LineRow1 = l1r1,
                LineColumn1 = l1c1,
                LineRow2 = l1r2,
                LineColumn2 = l1c2,
                Points = new List<EdgeInspectionPoint>
                {
                    new EdgeInspectionPoint { Row = midRow1, Column = midCol1 }
                }
            });

            // 피팅 라인 2
            overlays.Add(new EdgeInspectionOverlay
            {
                RoiId = "FAI-Edge2",
                LineRow1 = l2r1,
                LineColumn1 = l2c1,
                LineRow2 = l2r2,
                LineColumn2 = l2c2,
                Points = new List<EdgeInspectionPoint>
                {
                    new EdgeInspectionPoint { Row = midRow2, Column = midCol2 }
                }
            });

            // 라인 간 거리 연결선 (중점 기준)
            overlays.Add(new EdgeInspectionOverlay
            {
                RoiId = "FAI-DistLine",
                LineRow1 = midRow1,
                LineColumn1 = midCol1,
                LineRow2 = midRow2,
                LineColumn2 = midCol2,
                Points = new List<EdgeInspectionPoint>
                {
                    new EdgeInspectionPoint { Row = midRow1, Column = midCol1 },
                    new EdgeInspectionPoint { Row = midRow2, Column = midCol2 }
                }
            });

            return overlays;
        }

        /// <summary>
        /// Single 모드 오버레이: 피팅 라인 1개
        /// </summary>
        private static List<EdgeInspectionOverlay> BuildOverlaysSingle(
            double lr1, double lc1, double lr2, double lc2)
        {
            var overlays = new List<EdgeInspectionOverlay>();

            overlays.Add(new EdgeInspectionOverlay
            {
                RoiId = "FAI-Edge1",
                LineRow1 = lr1,
                LineColumn1 = lc1,
                LineRow2 = lr2,
                LineColumn2 = lc2,
                Points = new List<EdgeInspectionPoint>
                {
                    new EdgeInspectionPoint { Row = (lr1 + lr2) / 2.0, Column = (lc1 + lc2) / 2.0 }
                }
            });

            return overlays;
        }

        /// <summary>
        /// 극값 에지 포인트를 제거한다 (MeasurementAlgorithm.TrimExtremePoints 동일 로직).
        /// scanHorizontal이면 row 기준, 아니면 column 기준으로 정렬 후 양 끝 trimCount개 제거.
        /// </summary>
        private static void TrimExtremePoints(ref HTuple rows, ref HTuple cols, bool scanHorizontal, int trimCount)
        {
            if (trimCount <= 0)
            {
                return;
            }

            int pointCount = rows.TupleLength();
            if (pointCount <= (trimCount * 2))
            {
                return;
            }

            HTuple key;
            if (scanHorizontal) key = rows;
            else key = cols;
            HTuple sortedIndex;
            HOperatorSet.TupleSortIndex(key, out sortedIndex);

            HTuple sortedRows;
            HTuple sortedCols;
            HOperatorSet.TupleSelect(rows, sortedIndex, out sortedRows);
            HOperatorSet.TupleSelect(cols, sortedIndex, out sortedCols);

            int start = trimCount;
            int end = sortedRows.TupleLength() - trimCount - 1;
            if (end <= start)
            {
                return;
            }

            HOperatorSet.TupleSelectRange(sortedRows, start, end, out rows);
            HOperatorSet.TupleSelectRange(sortedCols, start, end, out cols);
        }
    }
}
