using System.Collections.Generic;
using System.Runtime.Serialization;

namespace ReringProject.Halcon.Models
{
    [DataContract]
    public class TeachingJob
    {
        [DataMember]
        public string JobName { get; set; }

        [DataMember]
        public string ImagePath { get; set; }

        [DataMember]
        public List<RoiDefinition> Rois { get; set; } = new List<RoiDefinition>();

        [DataMember]
        public double OutputOffsetX { get; set; }

        [DataMember]
        public double OutputOffsetY { get; set; }

        [DataMember]
        public double OutputOffsetTheta { get; set; }
    }
}

