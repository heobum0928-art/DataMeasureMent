using System;
using System.Collections.Generic;
using System.Linq;
using HalconDotNet;
using ReringProject.Halcon.Models;

namespace ReringProject.Halcon.Algorithms
{
    public class RoiLineIntersectionAlgorithm
    {
        private readonly MeasurementAlgorithm _edgeAlgorithm = new MeasurementAlgorithm();

        public bool TryRun(string imagePath, IEnumerable<RoiDefinition> rois, out RoiLineInspectionResult result)
        {
            result = new RoiLineInspectionResult();
            if (string.IsNullOrWhiteSpace(imagePath) || rois == null)
            {
                return false;
            }

            var taught = rois.Where(roi => roi.IsTaught).ToList();
            if (!taught.Any())
            {
                return false;
            }

            var horizontalRows = new HTuple();
            var horizontalCols = new HTuple();
            var verticalRows = new HTuple();
            var verticalCols = new HTuple();

            foreach (var roi in taught)
            {
                EdgeInspectionOverlay overlay;
                if (!_edgeAlgorithm.TryInspectSingleEdge(imagePath, roi, out overlay))
                {
                    result.ReteachMessages.Add(string.Format("{0}: edge detect failed. Re-teach.", roi.Name));
                    continue;
                }

                result.Overlays.Add(overlay);
                var horizontal = string.Equals(roi.LineOrientation, "Horizontal", StringComparison.OrdinalIgnoreCase);
                AppendOverlayPoints(overlay, horizontal, ref horizontalRows, ref horizontalCols, ref verticalRows, ref verticalCols);
            }

            LineEquation hLine;
            LineEquation vLine;
            if (!TryFitLine(horizontalRows, horizontalCols, out hLine) || !TryFitLine(verticalRows, verticalCols, out vLine))
            {
                return result.Overlays.Count > 0;
            }

            HTuple row;
            HTuple col;
            HTuple overlap;
            HOperatorSet.IntersectionLines(hLine.Row1, hLine.Col1, hLine.Row2, hLine.Col2, vLine.Row1, vLine.Col1, vLine.Row2, vLine.Col2, out row, out col, out overlap);

            result.HasHorizontalLine = true;
            result.HasVerticalLine = true;
            result.HasIntersection = true;
            result.HorizontalRow1 = hLine.Row1;
            result.HorizontalColumn1 = hLine.Col1;
            result.HorizontalRow2 = hLine.Row2;
            result.HorizontalColumn2 = hLine.Col2;
            result.VerticalRow1 = vLine.Row1;
            result.VerticalColumn1 = vLine.Col1;
            result.VerticalRow2 = vLine.Row2;
            result.VerticalColumn2 = vLine.Col2;
            result.IntersectionRow = row.D;
            result.IntersectionColumn = col.D;
            result.HorizontalAngleDeg = ComputeLineAngleDeg(hLine.Row1, hLine.Col1, hLine.Row2, hLine.Col2);
            result.VerticalAngleDeg = ComputeLineAngleDeg(vLine.Row1, vLine.Col1, vLine.Row2, vLine.Col2);
            result.CrossAngleDeg = ComputeCrossAngleDeg(result.HorizontalAngleDeg, result.VerticalAngleDeg);

            result.Overlays.Add(new EdgeInspectionOverlay { RoiId = "Group-H", LineRow1 = hLine.Row1, LineColumn1 = hLine.Col1, LineRow2 = hLine.Row2, LineColumn2 = hLine.Col2 });
            result.Overlays.Add(new EdgeInspectionOverlay { RoiId = "Group-V", LineRow1 = vLine.Row1, LineColumn1 = vLine.Col1, LineRow2 = vLine.Row2, LineColumn2 = vLine.Col2 });
            result.Overlays.Add(BuildLinkOverlay("Cross-H-Link", hLine.Row1, hLine.Col1, hLine.Row2, hLine.Col2, row.D, col.D));
            result.Overlays.Add(BuildLinkOverlay("Cross-V-Link", vLine.Row1, vLine.Col1, vLine.Row2, vLine.Col2, row.D, col.D));
            result.Overlays.Add(new EdgeInspectionOverlay
            {
                RoiId = "Cross",
                Points = new List<EdgeInspectionPoint> { new EdgeInspectionPoint { Row = row.D, Column = col.D } },
                LineRow1 = row.D - 20.0,
                LineColumn1 = col.D - 20.0,
                LineRow2 = row.D + 20.0,
                LineColumn2 = col.D + 20.0
            });

            return true;
        }

        private static void AppendOverlayPoints(EdgeInspectionOverlay overlay, bool horizontal, ref HTuple horizontalRows, ref HTuple horizontalCols, ref HTuple verticalRows, ref HTuple verticalCols)
        {
            var rows = new HTuple();
            var cols = new HTuple();
            foreach (var point in overlay.Points)
            {
                rows[rows.TupleLength()] = point.Row;
                cols[cols.TupleLength()] = point.Column;
            }

            if (horizontal)
            {
                HOperatorSet.TupleConcat(horizontalRows, rows, out horizontalRows);
                HOperatorSet.TupleConcat(horizontalCols, cols, out horizontalCols);
            }
            else
            {
                HOperatorSet.TupleConcat(verticalRows, rows, out verticalRows);
                HOperatorSet.TupleConcat(verticalCols, cols, out verticalCols);
            }
        }

        private static bool TryFitLine(HTuple rows, HTuple cols, out LineEquation line)
        {
            line = new LineEquation();
            if (rows.TupleLength() <= 1)
            {
                return false;
            }

            try
            {
                HObject contour;
                HOperatorSet.GenContourPolygonXld(out contour, rows, cols);
                HTuple r1;
                HTuple c1;
                HTuple r2;
                HTuple c2;
                HTuple nr;
                HTuple nc;
                HTuple dist;
                HOperatorSet.FitLineContourXld(contour, "tukey", -1, 0, 5, 2, out r1, out c1, out r2, out c2, out nr, out nc, out dist);
                line.Row1 = r1.D;
                line.Col1 = c1.D;
                line.Row2 = r2.D;
                line.Col2 = c2.D;
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static double ComputeLineAngleDeg(double row1, double col1, double row2, double col2)
        {
            var angle = Math.Atan2(row2 - row1, col2 - col1) * (180.0 / Math.PI);
            return angle < 0 ? angle + 180.0 : angle;
        }

        private static double ComputeCrossAngleDeg(double angleA, double angleB)
        {
            var diff = Math.Abs(angleA - angleB);
            if (diff > 90.0)
            {
                diff = 180.0 - diff;
            }

            return diff;
        }

        private static EdgeInspectionOverlay BuildLinkOverlay(string roiId, double row1, double col1, double row2, double col2, double crossRow, double crossCol)
        {
            var distanceToStart = ComputeDistance(row1, col1, crossRow, crossCol);
            var distanceToEnd = ComputeDistance(row2, col2, crossRow, crossCol);

            if (distanceToStart <= distanceToEnd)
            {
                return new EdgeInspectionOverlay
                {
                    RoiId = roiId,
                    LineRow1 = row1,
                    LineColumn1 = col1,
                    LineRow2 = crossRow,
                    LineColumn2 = crossCol
                };
            }

            return new EdgeInspectionOverlay
            {
                RoiId = roiId,
                LineRow1 = row2,
                LineColumn1 = col2,
                LineRow2 = crossRow,
                LineColumn2 = crossCol
            };
        }

        private static double ComputeDistance(double row1, double col1, double row2, double col2)
        {
            var dr = row2 - row1;
            var dc = col2 - col1;
            return Math.Sqrt((dr * dr) + (dc * dc));
        }

        private sealed class LineEquation
        {
            public double Row1 { get; set; }
            public double Col1 { get; set; }
            public double Row2 { get; set; }
            public double Col2 { get; set; }
        }
    }
}



