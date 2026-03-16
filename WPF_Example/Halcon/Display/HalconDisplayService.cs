using System;
using System.Collections.Generic;
using System.Linq;
using HalconDotNet;
using ReringProject.Halcon.Models;

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
                    window.SetColor(roi.Id == selectedRoiId ? "yellow" : "green");
                    window.SetLineWidth(roi.Id == selectedRoiId ? 3 : 2);
                    DrawRectangleOutline(window, roi.Row1, roi.Column1, roi.Row2, roi.Column2);
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
    }
}














