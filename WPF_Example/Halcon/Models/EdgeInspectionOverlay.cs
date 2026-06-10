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

    //260610 hbk Phase 40.2 hotfix CO-40.2-11 — capture 에 datum 검출 오버레이(녹색 원 + 중심/원점 십자) 포함용 스냅샷.
    //  datum 은 시퀀스 단위 검출(모든 FAI 공유)이므로 검사 스레드에서 값만 추출해 워커로 전달(async race 차단).
    public class DatumCaptureOverlay
    {
        public bool HasOrigin { get; set; }   //260610 hbk Phase 40.2 hotfix CO-40.2-11 — 검출 원점 십자 표시 여부
        public double OriginRow { get; set; }
        public double OriginCol { get; set; }
        public bool HasCircle { get; set; }    //260610 hbk Phase 40.2 hotfix CO-40.2-11 — 검출 원(녹색) 표시 여부
        public double CircleRow { get; set; }
        public double CircleCol { get; set; }
        public double CircleRadius { get; set; }
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

