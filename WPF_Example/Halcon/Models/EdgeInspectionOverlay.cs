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

    // capture 에 datum 검출 오버레이(녹색 원 + 중심/원점 십자) 포함용 스냅샷.
    // datum 은 시퀀스 단위 검출(모든 FAI 공유)이므로 검사 스레드에서 값만 추출해 워커로 전달(async race 차단).
    public class DatumCaptureOverlay
    {
        public bool HasOrigin { get; set; }   // 검출 원점 십자 표시 여부
        public double OriginRow { get; set; }
        public double OriginCol { get; set; }
        public bool HasCircle { get; set; }    // 검출 원(녹색) 표시 여부
        public double CircleRow { get; set; }
        public double CircleCol { get; set; }
        public double CircleRadius { get; set; }
        // datum 기준선(축). 원점+각도로 렌더러가 이미지 대각 길이만큼 라인 산출.
        public bool HasAxis1 { get; set; }     // 1차(주) 기준선
        public double Axis1AngleRad { get; set; }
        public bool HasAxis2 { get; set; }     // 2차(수직) 기준선
        public double Axis2AngleRad { get; set; }
    }

    public class EdgeInspectionOverlay
    {
        public string RoiId { get; set; }

        // 이 overlay 를 만든 측정 이름(F9_P1 등). 화면에서 "이 선이 어느 항목이냐"를 표시하기 위한 것으로,
        //  RoiId 와 분리한 이유는 RoiId 가 색상/판정 분기(FAI-Edge* StartsWith, -OK/-NG EndsWith)에 쓰이는
        //  식별자라 여기에 이름을 섞으면 그 분기가 전부 깨지기 때문이다. 표시 전용이며 판정에 관여하지 않는다.
        //  구 cycle.json 에는 이 필드가 없어 null 로 로드된다 → 라벨 미표시로 자연 폴백(하위호환).
        public string MeasurementName { get; set; }

        public List<EdgeInspectionPoint> Points { get; set; } = new List<EdgeInspectionPoint>();

        public double LineRow1 { get; set; }

        public double LineColumn1 { get; set; }

        public double LineRow2 { get; set; }

        public double LineColumn2 { get; set; }

        public EdgeInspectionOverlay Clone()
        {
            List<EdgeInspectionPoint> clonedPoints;
            if (Points == null) clonedPoints = new List<EdgeInspectionPoint>();
            else clonedPoints = Points.Select(point => point.Clone()).ToList();
            return new EdgeInspectionOverlay
            {
                RoiId = RoiId,
                MeasurementName = MeasurementName,
                Points = clonedPoints,
                LineRow1 = LineRow1,
                LineColumn1 = LineColumn1,
                LineRow2 = LineRow2,
                LineColumn2 = LineColumn2
            };
        }
    }
}

