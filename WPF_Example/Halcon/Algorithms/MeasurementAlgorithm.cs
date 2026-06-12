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
            List<RoiDefinition> allRois;
            if (rois == null) allRois = new List<RoiDefinition>();
            else allRois = rois.ToList();
            if (string.IsNullOrWhiteSpace(imagePath))
            {
                return "Image: None | No inspection executed.";
            }

            try
            {
                using (var image = new HImage(imagePath))
                {
                    return Run(image, allRois, imagePath);
                }
            }
            catch
            {
                return string.Format("Image: {0} | inspection failed.", imagePath);
            }
        }

        public string Run(HImage image, IEnumerable<RoiDefinition> rois)
        {
            List<RoiDefinition> allRois;
            if (rois == null) allRois = new List<RoiDefinition>();
            else allRois = rois.ToList();
            return Run(image, allRois, "(memory)");
        }

        private string Run(HImage image, List<RoiDefinition> allRois, string imageLabel)
        {
            var taughtRois = allRois.Where(roi => roi.IsTaught).ToList();

            if (image == null)
            {
                return "Image: None | No inspection executed.";
            }

            if (!taughtRois.Any())
            {
                return string.Format("Image: {0} | ROI: 0/{1} taught", imageLabel, allRois.Count);
            }

            try
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

                return string.Format("Image: {0} | ROI: {1}/{2} taught | {3}", imageLabel, taughtRois.Count, allRois.Count, string.Join(" | ", summaries));
            }
            catch
            {
                return string.Format("Image: {0} | inspection failed.", imageLabel);
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
                    return TryInspectSingleEdge(image, roi, out overlay);
                }
            }
            catch
            {
                return false;
            }
        }

        public bool TryInspectSingleEdge(HImage image, RoiDefinition roi, out EdgeInspectionOverlay overlay)
        {
            overlay = null;
            if (image == null || roi == null)
            {
                return false;
            }

            try
            {
                return TryInspectSingleEdgeInternal(image, roi, out overlay);
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
            double measurePhi;
            if (string.Equals(roi.EdgeDirection, "TtoB", StringComparison.OrdinalIgnoreCase))
                measurePhi = -Math.PI / 2.0;
            else if (string.Equals(roi.EdgeDirection, "RtoL", StringComparison.OrdinalIgnoreCase))
                measurePhi = Math.PI;
            else if (string.Equals(roi.EdgeDirection, "BtoT", StringComparison.OrdinalIgnoreCase))
                measurePhi = Math.PI / 2.0;
            else
                measurePhi = 0.0;

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
                string polarity;
                if (string.Equals(roi.EdgePolarity, "LightToDark", StringComparison.OrdinalIgnoreCase)) polarity = "negative";
                else polarity = "positive";
                string selection;
                if (string.Equals(roi.EdgeSelection, "Last", StringComparison.OrdinalIgnoreCase)) selection = "last";
                else if (string.Equals(roi.EdgeSelection, "All", StringComparison.OrdinalIgnoreCase)) selection = "all";
                else selection = "first";
                HOperatorSet.MeasurePos(
                    image,
                    handle,
                    Math.Max(0.4, roi.Sigma),
                    Math.Max(1, roi.EdgeThreshold),
                    polarity,
                    selection,
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

            HTuple key;
            if (scanHorizontal) key = rows;
            else key = cols;
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