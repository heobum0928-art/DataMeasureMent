using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using HalconDotNet;
using ReringProject.Halcon.Models;
using ReringProject.Sequence;

namespace ReringProject.Halcon.Display
{
    public class HalconDisplayService
    {
        private bool _isFontInitialized;
        private static readonly HTuple MessageTextParamNames = new HTuple("box");
        private static readonly HTuple MessageTextParamValues = new HTuple("false");

        public void Render(
            HWindow window,
            HImage image,
            IEnumerable<RoiDefinition> rois,
            string selectedRoiId,
            RoiDefinition draftRoi = null,
            IEnumerable<EdgeInspectionOverlay> inspectionOverlays = null,
            IEnumerable<string> displayMessages = null)
        {
            if (window == null)
            {
                return;
            }

            EnsureFontInitialized(window);
            window.ClearWindow();
            window.SetDraw("margin");
            window.SetLineWidth(2);

            if (image != null)
            {
                window.DispObj(image);
            }

            if (rois != null)
            {
                foreach (var roi in rois)
                {
                    string roiColor = roi.Id == selectedRoiId ? "yellow" : "green";
                    int roiWidth = roi.Id == selectedRoiId ? 3 : 2;
                    window.SetColor(roiColor);
                    window.SetLineWidth(roiWidth);

                    //260408 hbk Polygon ROI 렌더링 지원
                    if (!string.IsNullOrEmpty(roi.PolygonPoints))
                    {
                        var pts = ParsePolygonPoints(roi.PolygonPoints);
                        if (pts != null && pts.Count >= 3)
                            RenderPolygon(window, pts, roiColor, roiWidth);
                    }
                    else if (roi.Row1 != 0 || roi.Column1 != 0 || roi.Row2 != 0 || roi.Column2 != 0)
                    {
                        DrawRectangleOutline(window, roi.Row1, roi.Column1, roi.Row2, roi.Column2);
                    }

                    if (roi.Id == selectedRoiId && roi.IsTaught)
                    {
                        DrawDirectionArrow(window, roi);
                    }
                }
            }

            if (draftRoi != null)
            {
                window.SetColor("red");
                window.SetLineWidth(3);
                DrawRectangleOutline(window, draftRoi.Row1, draftRoi.Column1, draftRoi.Row2, draftRoi.Column2);
            }

            if (inspectionOverlays != null)
            {
                foreach (var overlay in inspectionOverlays)
                {
                    if (string.Equals(overlay.RoiId, "Group-H", StringComparison.OrdinalIgnoreCase))
                    {
                        window.SetColor("cyan");
                        window.SetLineWidth(3);
                    }
                    else if (string.Equals(overlay.RoiId, "Group-V", StringComparison.OrdinalIgnoreCase))
                    {
                        window.SetColor("lime green");
                        window.SetLineWidth(3);
                    }
                    else if (string.Equals(overlay.RoiId, "Cross-H-Link", StringComparison.OrdinalIgnoreCase))
                    {
                        window.SetColor("cyan");
                        window.SetLineWidth(4);
                    }
                    else if (string.Equals(overlay.RoiId, "Cross-V-Link", StringComparison.OrdinalIgnoreCase))
                    {
                        window.SetColor("lime green");
                        window.SetLineWidth(4);
                    }
                    else if (string.Equals(overlay.RoiId, "Cross", StringComparison.OrdinalIgnoreCase))
                    {
                        window.SetColor("orange");
                        window.SetLineWidth(4);
                    }
                    else if (string.Equals(overlay.RoiId, "Main-Crosshair-H", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(overlay.RoiId, "Main-Crosshair-V", StringComparison.OrdinalIgnoreCase))
                    {
                        window.SetColor("yellow");
                        window.SetLineWidth(1);
                    }
                    else if (string.Equals(overlay.RoiId, "ManualMeasure-Line", StringComparison.OrdinalIgnoreCase))
                    {
                        window.SetColor("orange");
                        window.SetLineWidth(3);
                    }
                    else if (string.Equals(overlay.RoiId, "ManualMeasure-Start", StringComparison.OrdinalIgnoreCase))
                    {
                        window.SetColor("green");
                        window.SetLineWidth(3);
                    }
                    else if (string.Equals(overlay.RoiId, "ManualMeasure-End", StringComparison.OrdinalIgnoreCase))
                    {
                        window.SetColor("red");
                        window.SetLineWidth(3);
                    }
                    //260409 hbk Phase 3: FAI edge measurement result overlay colors
                    else if (overlay.RoiId != null && overlay.RoiId.StartsWith("FAI-Edge", StringComparison.OrdinalIgnoreCase))
                    {
                        bool isNG = overlay.RoiId.EndsWith("-NG", StringComparison.OrdinalIgnoreCase);
                        window.SetColor(isNG ? "red" : "green");
                        window.SetLineWidth(2);
                    }
                    else if (string.Equals(overlay.RoiId, "FAI-DistLine", StringComparison.OrdinalIgnoreCase))
                    {
                        window.SetColor("cyan");
                        window.SetLineWidth(1);
                    }
                    else
                    {
                        window.SetColor("blue");
                        window.SetLineWidth(2);
                    }

                    window.DispLine(overlay.LineRow1, overlay.LineColumn1, overlay.LineRow2, overlay.LineColumn2);
                    if (overlay.Points == null)
                    {
                        continue;
                    }

                    foreach (var point in overlay.Points)
                    {
                        const double size = 8.0;
                        window.DispLine(point.Row - size, point.Column - size, point.Row + size, point.Column + size);
                        window.DispLine(point.Row - size, point.Column + size, point.Row + size, point.Column - size);
                    }
                }
            }

            if (displayMessages == null)
            {
                return;
            }

            var line = 0;
            foreach (var message in displayMessages)
            {
                if (string.IsNullOrWhiteSpace(message))
                {
                    continue;
                }

                window.DispText(message, "window", 12 + (line * 28), 12, "yellow", MessageTextParamNames, MessageTextParamValues);
                line++;
            }
        }

        private void EnsureFontInitialized(HWindow window)
        {
            if (_isFontInitialized)
            {
                return;
            }

            try
            {
                HTuple fonts;
                HOperatorSet.QueryFont(window, out fonts);
                var font = fonts.TupleLength() > 0 ? fonts.TupleSelect(0) + "-18" : new HTuple("mono-18");
                window.SetFont(font);
                _isFontInitialized = true;
            }
            catch
            {
            }
        }

        private static void DrawRectangleOutline(HWindow window, double row1, double col1, double row2, double col2)
        {
            window.DispLine(row1, col1, row1, col2);
            window.DispLine(row1, col2, row2, col2);
            window.DispLine(row2, col2, row2, col1);
            window.DispLine(row2, col1, row1, col1);
        }

        //260408 hbk RenderPolygon 추가 (Polygon ROI 렌더링)
        /// <summary>Renders a polygon outline on the HWindow. Points as (col, row) pairs (X=col, Y=row).</summary>
        public void RenderPolygon(HWindow window, IList<Point> points, string color, int lineWidth)
        {
            if (window == null || points == null || points.Count < 3) return;
            window.SetColor(color);
            window.SetLineWidth(lineWidth);
            for (int i = 0; i < points.Count; i++)
            {
                int next = (i + 1) % points.Count;
                window.DispLine(points[i].Y, points[i].X, points[next].Y, points[next].X);
            }
        }

        //260408 hbk RenderPolygonPoints — 큰 + 표시 + 점 사이 라인
        /// <summary>Renders polygon draft points (large cross marks + connecting lines during drawing).</summary>
        public void RenderPolygonPoints(HWindow window, IList<Point> points, string color)
        {
            if (window == null || points == null) return;
            int crossSize = 12;

            // 점 사이 연결선
            if (points.Count >= 2)
            {
                window.SetColor("cyan");
                window.SetLineWidth(1);
                for (int i = 0; i < points.Count - 1; i++)
                {
                    window.DispLine(points[i].Y, points[i].X, points[i + 1].Y, points[i + 1].X);
                }
            }

            // 각 점에 큰 + 표시
            window.SetColor(color);
            window.SetLineWidth(2);
            foreach (var pt in points)
            {
                window.DispLine(pt.Y - crossSize, pt.X, pt.Y + crossSize, pt.X);
                window.DispLine(pt.Y, pt.X - crossSize, pt.Y, pt.X + crossSize);
            }
        }

        //260408 hbk Calibration 십자 + 라인 렌더링
        /// <summary>Renders calibration crosshairs and connecting line.</summary>
        public void RenderCalibrationOverlay(HWindow window, IList<Point> points)
        {
            if (window == null || points == null || points.Count == 0) return;
            int crossSize = 20;

            window.SetColor("yellow");
            window.SetLineWidth(2);
            // 첫 번째 점 십자
            var p1 = points[0];
            window.DispLine(p1.Y - crossSize, p1.X, p1.Y + crossSize, p1.X);
            window.DispLine(p1.Y, p1.X - crossSize, p1.Y, p1.X + crossSize);

            if (points.Count >= 2)
            {
                // 두 번째 점 십자
                var p2 = points[1];
                window.DispLine(p2.Y - crossSize, p2.X, p2.Y + crossSize, p2.X);
                window.DispLine(p2.Y, p2.X - crossSize, p2.Y, p2.X + crossSize);

                // 두 점 사이 연결선
                window.SetColor("green");
                window.SetLineWidth(1);
                window.DispLine(p1.Y, p1.X, p2.Y, p2.X);
            }
        }

        //260408 hbk DrawDirectionArrow 추가 (에지 방향 화살표)
        /// <summary>Draws edge search direction arrow at ROI center (per D-02). White, 2px line + arrowhead.</summary>
        private static void DrawDirectionArrow(HWindow window, RoiDefinition roi)
        {
            double centerRow = (roi.Row1 + roi.Row2) / 2.0;
            double centerCol = (roi.Column1 + roi.Column2) / 2.0;
            double arrowLength = 15.0;

            // Determine arrow direction from EdgeDirection
            double dRow = 0, dCol = 0;
            switch (roi.EdgeDirection)
            {
                case "LtoR": dCol = arrowLength; break;
                case "RtoL": dCol = -arrowLength; break;
                case "TtoB": dRow = arrowLength; break;
                case "BtoT": dRow = -arrowLength; break;
                default: dCol = arrowLength; break;
            }

            window.SetColor("white");
            window.SetLineWidth(2);
            // Main line
            window.DispLine(centerRow - dRow / 2, centerCol - dCol / 2,
                             centerRow + dRow / 2, centerCol + dCol / 2);
            // Arrowhead (two short lines at 30-degree angle)
            double headLen = 5.0;
            double endRow = centerRow + dRow / 2;
            double endCol = centerCol + dCol / 2;
            double angle = Math.Atan2(dRow, dCol);
            double a1 = angle + 2.5;  // ~143 degrees
            double a2 = angle - 2.5;  // ~-143 degrees
            window.DispLine(endRow, endCol,
                             endRow + headLen * Math.Sin(a1), endCol + headLen * Math.Cos(a1));
            window.DispLine(endRow, endCol,
                             endRow + headLen * Math.Sin(a2), endCol + headLen * Math.Cos(a2));
        }

        //260409 hbk Phase 4: render Datum Line ROI overlays on canvas (D-12)
        /// <summary>Renders Datum Line1/Line2 ROI rectangles and reference origin cross on HWindow.</summary>
        public void RenderDatumOverlay(HWindow window, DatumConfig datum, bool isSelected)
        {
            if (datum == null) return;

            string color = isSelected ? "cyan" : "blue";
            int lineWidth = isSelected ? 3 : 2;

            try
            {
                HOperatorSet.SetColor(window, color);
                HOperatorSet.SetLineWidth(window, lineWidth);

                // Draw Line1 ROI as Rectangle2
                if (datum.Line1_Length1 > 0 && datum.Line1_Length2 > 0)
                {
                    HOperatorSet.DispRectangle2(window,
                        datum.Line1_Row, datum.Line1_Col, datum.Line1_Phi,
                        datum.Line1_Length1, datum.Line1_Length2);
                }

                // Draw Line2 ROI as Rectangle2
                if (datum.Line2_Length1 > 0 && datum.Line2_Length2 > 0)
                {
                    HOperatorSet.DispRectangle2(window,
                        datum.Line2_Row, datum.Line2_Col, datum.Line2_Phi,
                        datum.Line2_Length1, datum.Line2_Length2);
                }

                // Draw reference origin cross if configured
                if (datum.IsConfigured)
                {
                    HOperatorSet.SetColor(window, "magenta");
                    HOperatorSet.SetLineWidth(window, 2);
                    double crossSize = 15;
                    // Horizontal line
                    HOperatorSet.DispLine(window,
                        datum.RefOriginRow, datum.RefOriginCol - crossSize,
                        datum.RefOriginRow, datum.RefOriginCol + crossSize);
                    // Vertical line
                    HOperatorSet.DispLine(window,
                        datum.RefOriginRow - crossSize, datum.RefOriginCol,
                        datum.RefOriginRow + crossSize, datum.RefOriginCol);
                    // Label
                    HOperatorSet.SetColor(window, "magenta");
                    EnsureFontInitialized(window);
                    HOperatorSet.SetTposition(window, datum.RefOriginRow - crossSize - 15, datum.RefOriginCol + 5);
                    HOperatorSet.WriteString(window, "Datum Origin");
                }
            }
            catch
            {
                // Suppress display errors
            }
        }

        //260408 hbk PolygonPoints 문자열 파싱 ("x1,y1;x2,y2;..." → List<Point>)
        private static IList<Point> ParsePolygonPoints(string polygonPoints)
        {
            if (string.IsNullOrEmpty(polygonPoints)) return null;
            var result = new List<Point>();
            var pairs = polygonPoints.Split(';');
            foreach (var pair in pairs)
            {
                var parts = pair.Split(',');
                if (parts.Length >= 2 &&
                    double.TryParse(parts[0], out double x) &&
                    double.TryParse(parts[1], out double y))
                {
                    result.Add(new Point(x, y));
                }
            }
            return result;
        }
    }
}














