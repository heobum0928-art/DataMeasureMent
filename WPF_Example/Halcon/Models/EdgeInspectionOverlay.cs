using System.Collections.Generic;
using System.Linq;

namespace ReringProject.Halcon.Models
{
    public class EdgeInspectionPoint
    {
        public double Row { get; set; }

        public double Column { get; set; }

        public EdgeInspectionPoint Clone()
        {
            return new EdgeInspectionPoint
            {
                Row = Row,
                Column = Column
            };
        }
    }

    public class EdgeInspectionOverlay
    {
        public string RoiId { get; set; }

        public List<EdgeInspectionPoint> Points { get; set; } = new List<EdgeInspectionPoint>();

        public double LineRow1 { get; set; }

        public double LineColumn1 { get; set; }

        public double LineRow2 { get; set; }

        public double LineColumn2 { get; set; }

        public EdgeInspectionOverlay Clone()
        {
            return new EdgeInspectionOverlay
            {
                RoiId = RoiId,
                Points = Points == null ? new List<EdgeInspectionPoint>() : Points.Select(point => point.Clone()).ToList(),
                LineRow1 = LineRow1,
                LineColumn1 = LineColumn1,
                LineRow2 = LineRow2,
                LineColumn2 = LineColumn2
            };
        }
    }
}

