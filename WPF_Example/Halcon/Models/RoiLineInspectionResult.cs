using System.Collections.Generic;

namespace ReringProject.Halcon.Models
{
    public class RoiLineInspectionResult
    {
        public List<EdgeInspectionOverlay> Overlays { get; set; } = new List<EdgeInspectionOverlay>();

        public bool HasHorizontalLine { get; set; }

        public bool HasVerticalLine { get; set; }

        public bool HasIntersection { get; set; }

        public double IntersectionRow { get; set; }

        public double IntersectionColumn { get; set; }

        public double HorizontalRow1 { get; set; }

        public double HorizontalColumn1 { get; set; }

        public double HorizontalRow2 { get; set; }

        public double HorizontalColumn2 { get; set; }

        public double VerticalRow1 { get; set; }

        public double VerticalColumn1 { get; set; }

        public double VerticalRow2 { get; set; }

        public double VerticalColumn2 { get; set; }

        public double HorizontalAngleDeg { get; set; }

        public double VerticalAngleDeg { get; set; }

        public double CrossAngleDeg { get; set; }

        public List<string> ReteachMessages { get; set; } = new List<string>();
    }
}

