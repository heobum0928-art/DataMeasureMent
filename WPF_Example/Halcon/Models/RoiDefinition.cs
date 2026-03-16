using System.Runtime.Serialization;

namespace ReringProject.Halcon.Models
{
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

