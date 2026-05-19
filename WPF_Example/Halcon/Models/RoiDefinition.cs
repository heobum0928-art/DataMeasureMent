using System.Runtime.Serialization;

namespace ReringProject.Halcon.Models
{
    //260423 hbk Phase 11 D-16 — ROI 모양 enum (Rect/Polygon 하위호환: 기본값 Rect)
    public enum RoiShape { Rect, Polygon, Circle }

    [DataContract]
    public class RoiDefinition
    {
        [DataMember]
        public string Id { get; set; }

        [DataMember]
        public string Name { get; set; }

        [DataMember]
        public double Row1 { get; set; }

        [DataMember]
        public double Column1 { get; set; }

        [DataMember]
        public double Row2 { get; set; }

        [DataMember]
        public double Column2 { get; set; }

        [DataMember]
        public bool IsTaught { get; set; }

        [DataMember]
        public string TeachingValue { get; set; }

        [DataMember]
        public double Sigma { get; set; } = 1.0;

        [DataMember]
        public int EdgeThreshold { get; set; } = 10;

        //260423 hbk Phase 11 D-16 — Circle ROI 지원 (Rect/Polygon 하위호환: 기본값 Rect)
        [DataMember]
        public RoiShape Shape { get; set; } = RoiShape.Rect;

        [DataMember]
        public double CenterRow { get; set; }

        [DataMember]
        public double CenterCol { get; set; }

        [DataMember]
        public double Radius { get; set; }

        //260519 hbk Phase 31 CO-23.1-02 — Circle polar-sampling strip 시각화 파라미터.
        //  > 0 일 때만 HalconDisplayService 가 360° strip 사각형 overlay 를 렌더 (0 = strip 미표시).
        [DataMember]
        public double CirclePolarStepDeg { get; set; } //260519 hbk Phase 31 CO-23.1-02
        [DataMember]
        public double CircleRectL1Ratio { get; set; } //260519 hbk Phase 31 CO-23.1-02
        [DataMember]
        public double CircleRectL2Ratio { get; set; } //260519 hbk Phase 31 CO-23.1-02

        [DataMember]
        public int EdgeSampleCount { get; set; } = 20;

        [DataMember]
        public int EdgeTrimCount { get; set; } = 10;

        [DataMember]
        public string EdgeDirection { get; set; } = "LtoR";

        [DataMember]
        public string EdgePolarity { get; set; } = "DarkToLight";

        [DataMember]
        public string EdgeSelection { get; set; } = "First";

        [DataMember]
        public string LineOrientation { get; set; } = "Horizontal";

        //260408 hbk Polygon ROI 좌표 ("x1,y1;x2,y2;..." 포맷)
        [DataMember]
        public string PolygonPoints { get; set; } = "";

        [DataMember]
        public double PixelResolutionX { get; set; } = 1.0;

        [DataMember]
        public double PixelResolutionY { get; set; } = 1.0;

        public bool Contains(double row, double column)
        {
            return row >= Row1 && row <= Row2 && column >= Column1 && column <= Column2;
        }

        public RoiDefinition Clone()
        {
            return (RoiDefinition)MemberwiseClone();
        }

        public override string ToString()
        {
            return string.Format("{0} [{1}]", Name, IsTaught ? "Taught" : "Pending");
        }
    }
}

