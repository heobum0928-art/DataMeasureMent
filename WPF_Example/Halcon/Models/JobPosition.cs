using System.Collections.Generic;
using System.Runtime.Serialization;

namespace ReringProject.Halcon.Models
{
    [DataContract]
    public class JobPosition
    {
        [DataMember]
        public string JobName { get; set; }

        [DataMember]
        public Dictionary<string, List<string>> ImageFilesByTab { get; set; } = new Dictionary<string, List<string>>();

        [DataMember]
        public Dictionary<string, string> SelectedImageByTab { get; set; } = new Dictionary<string, string>();

        [DataMember]
        public TeachingJob Teaching { get; set; } = new TeachingJob();

        [DataMember(EmitDefaultValue = false)]
        public List<RoiDefinition> Rois { get; set; }

        [DataMember(EmitDefaultValue = false)]
        public string ImagePath { get; set; }

        [DataMember(EmitDefaultValue = false)]
        public TeachingJob TeachingData { get; set; }
    }
}

