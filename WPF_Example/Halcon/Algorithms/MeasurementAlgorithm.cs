using System;
using System.Collections.Generic;
using System.Linq;
using HalconDotNet;
using ReringProject.Halcon.Models;

namespace ReringProject.Halcon.Algorithms
{
    public class MeasurementAlgorithm
    {
        public string Run(string imagePath, IEnumerable<RoiDefinition> rois)
        {
            var allRois = rois == null ? new List<RoiDefinition>() : rois.ToList();
            var taughtRois = allRois.Where(roi => roi.IsTaught).ToList();

            if (string.IsNullOrWhiteSpace(imagePath))
            {
                return "Image: None | No inspection executed.";
            }

            if (!taughtRois.Any())
            {
                return string.Format("Image: {0} | ROI: 0/{1} taught", imagePath, allRois.Count);
            }

            try
            {
                using (var image = new HImage(imagePath))
                {
                    var summaries = new List<string>();
                    foreach (var roi in taughtRois)
                    {
                        EdgeInspectionOverlay overlay;
                        if (!TryInspectSingleEdgeInternal(image, roi, out overlay))
                        {
                            summaries.Add(string.Format("{0}: NG", roi.Name));
                            continue;
                        }

                        summaries.Add(string.Format("{0}: OK pts={1}", roi.Name, overlay.Points.Count));
                    }

                    return string.Format("Image: {0} | ROI: {1}/{2} taught | {3}", imagePath, taughtRois.Count, allRois.Count, string.Join(" | ", summaries));
                }
            }
            catch
            {
                return string.Format("Image: {0} | inspection failed.", imagePath);
            }
        }

        public bool TryInspectSingleEdge(string imagePath, RoiDefinition roi, out EdgeInspectionOverlay overlay)
        {
            overlay = null;
            if (string.IsNullOrWhiteSpace(imagePath) || roi == null)
            {
                return false;
            }

            try
            {
                using (var image = new HImage(imagePath))
                {
                    return TryInspectSingleEdgeInternal(image, roi, out overlay);
                }
            }
            catch
            {
                return false;
            }
        }

        private static bool TryInspectSingleEdgeInternal(HImage image, RoiDefinition roi, out EdgeInspectionOverlay overlay)
        {
            overlay = null;
            var top = Math.Min(roi.Row1, roi.Row2);
            var left = Math.Min(roi.Column1, roi.Column2);
            var bottom = Math.Max(roi.Row1, roi.Row2);
            var right = Math.Max(roi.Column1, roi.Column2);
            if ((bottom - top) < 2.0 || (right - left) < 2.0)
            {
                return false;
            }

            HTuple imageWidth;
            HTuple imageHeight;
            image.GetImageSize(out imageWidth, out imageHeight);

            var scanHorizontal = string.Equals(roi.EdgeDirection, "LtoR", StringComparison.OrdinalIgnoreCase)
                || string.Equals(roi.EdgeDirection, "RtoL", StringComparison.OrdinalIgnoreCase);
            var measurePhi = string.Equals(roi.EdgeDirection, "TtoB", StringComparison.OrdinalIgnoreCase)
                ? -Math.PI / 2.0
                : string.Equals(roi.EdgeDirection, "RtoL", StringComparison.OrdinalIgnoreCase)
                    ? Math.PI
                    : string.Equals(roi.EdgeDirection, "BtoT", StringComparison.OrdinalIgnoreCase) ? Math.PI / 2.0 : 0.0;

            var allRows = new HTuple();
            var allCols = new HTuple();
            var sampleCount = Math.Max(1, roi.EdgeSampleCount);

            for (var i = 0; i < sampleCount; i++)
            {
                HObject stripRegion;
                if (scanHorizontal)
                {
                    var row1 = top + (i * (bottom - top) / sampleCount);
                    var row2 = top + ((i + 1) * (bottom - top) / sampleCount);
                    HOperatorSet.GenRectangle1(out stripRegion, row1, left, row2, right);
                }
                else
                {
                    var col1 = left + (i * (right - left) / sampleCount);
                    var col2 = left + ((i + 1) * (right - left) / sampleCount);
                    HOperatorSet.GenRectangle1(out stripRegion, top, col1, bottom, col2);
                }

                HTuple rr;
                HTuple rc;
                HTuple rp;
                HTuple rh;
                HTuple rw;
                HOperatorSet.SmallestRectangle2(stripRegion, out rr, out rc, out rp, out rh, out rw);

                HTuple handle;
                HOperatorSet.GenMeasureRectangle2(rr, rc, measurePhi, rh, rw, imageWidth, imageHeight, "nearest_neighbor", out handle);

                HTuple rows;
                HTuple cols;
                HTuple amp;
                HTuple dist;
                HOperatorSet.MeasurePos(
                    image,
                    handle,
                    Math.Max(0.4, roi.Sigma),
                    Math.Max(1, roi.EdgeThreshold),
                    string.Equals(roi.EdgePolarity, "LightToDark", StringComparison.OrdinalIgnoreCase) ? "negative" : "positive",
                    string.Equals(roi.EdgeSelection, "Last", StringComparison.OrdinalIgnoreCase) ? "last" : string.Equals(roi.EdgeSelection, "All", StringComparison.OrdinalIgnoreCase) ? "all" : "first",
                    out rows,
                    out cols,
                    out amp,
                    out dist);
                HOperatorSet.CloseMeasure(handle);

                if (rows.TupleLength() > 0)
                {
                    HOperatorSet.TupleConcat(allRows, rows, out allRows);
                    HOperatorSet.TupleConcat(allCols, cols, out allCols);
                }
            }

            if (allRows.TupleLength() <= 1 || allCols.TupleLength() <= 1)
            {
                return false;
            }

            if (roi.EdgeTrimCount > 0)
            {
                TrimExtremePoints(ref allRows, ref allCols, scanHorizontal, roi.EdgeTrimCount);
            }

            if (allRows.TupleLength() <= 1 || allCols.TupleLength() <= 1)
            {
                return false;
            }

            HObject contour;
            HOperatorSet.GenContourPolygonXld(out contour, allRows, allCols);
            HTuple lineRow1;
            HTuple lineCol1;
            HTuple lineRow2;
            HTuple lineCol2;
            HTuple nr;
            HTuple nc;
            HTuple distFit;
            HOperatorSet.FitLineContourXld(contour, "tukey", -1, 0, 5, 2, out lineRow1, out lineCol1, out lineRow2, out lineCol2, out nr, out nc, out distFit);

            overlay = new EdgeInspectionOverlay
            {
                RoiId = roi.Id,
                LineRow1 = lineRow1.D,
                LineColumn1 = lineCol1.D,
                LineRow2 = lineRow2.D,
                LineColumn2 = lineCol2.D
            };

            for (var i = 0; i < allRows.TupleLength(); i++)
            {
                overlay.Points.Add(new EdgeInspectionPoint { Row = allRows[i].D, Column = allCols[i].D });
            }

            return true;
        }

        private static void TrimExtremePoints(ref HTuple rows, ref HTuple cols, bool scanHorizontal, int trimCount)
        {
            if (trimCount <= 0)
            {
                return;
            }

            var pointCount = rows.TupleLength();
            if (pointCount <= (trimCount * 2))
            {
                return;
            }

            var key = scanHorizontal ? rows : cols;
            HTuple sortedIndex;
            HOperatorSet.TupleSortIndex(key, out sortedIndex);

            HTuple sortedRows;
            HTuple sortedCols;
            HOperatorSet.TupleSelect(rows, sortedIndex, out sortedRows);
            HOperatorSet.TupleSelect(cols, sortedIndex, out sortedCols);

            var start = trimCount;
            var end = sortedRows.TupleLength() - trimCount - 1;
            if (end <= start)
            {
                return;
            }

            HOperatorSet.TupleSelectRange(sortedRows, start, end, out rows);
            HOperatorSet.TupleSelectRange(sortedCols, start, end, out cols);
        }
    }
}


