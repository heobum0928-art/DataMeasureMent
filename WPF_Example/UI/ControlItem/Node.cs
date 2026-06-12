
using ReringProject.Define;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReringProject.UI {
    public enum ENodeType {
        Recipe,
        Sequence,
        Action,
        SubSequence,
        FAI,
        Datum,
        Measurement,
    }

    public class Node {
        public ENodeType NodeType { get; set; }

        public object ParamData { get; set; }

        public string Name { get; set; }

        public string SequenceName { get; set; }

        public EAction ActionID { get; set; }

        public ESequence SequenceID { get; set; }

        public string ImageSource {
            get {
                switch (NodeType) {
                    case ENodeType.Recipe:
                        return "/Resource/folder.png";
                    case ENodeType.Sequence:
                        return "/Resource/process.png";
                    case ENodeType.Action:
                        return "/Resource/layout.png";
                    case ENodeType.SubSequence:
                        return "/Resource/split.png";
                    case ENodeType.FAI:
                        return "/Resource/chart.png";
                    case ENodeType.Datum:
                        return "/Resource/layout.png";
                    case ENodeType.Measurement:
                        return "/Resource/chart.png";
                }
                return "/Resource/process.png";
            }
            set { }
        }

        public string IconKey {
            get {
                switch (NodeType) {
                    case ENodeType.Recipe:      return "Icon.Recipe";
                    case ENodeType.Sequence:    return "Icon.Sequence";
                    case ENodeType.Action:      return "Icon.Action";
                    case ENodeType.SubSequence: return "Icon.Action";
                    case ENodeType.Datum:       return "Icon.Datum";
                    case ENodeType.FAI:         return "Icon.FAI";
                    case ENodeType.Measurement:
                        var meas = ParamData as ReringProject.Sequence.MeasurementBase;
                        if (meas != null && !string.IsNullOrEmpty(meas.TypeName))
                            return "Icon.Meas." + meas.TypeName;
                        return "Icon.Measurement";
                }
                return "Icon.Default";
            }
        }
    }

    public class CompositeNode : Node {
        public List<Node> Children { get; private set; }

        public CompositeNode() {
            Children = new List<Node>();
        }
    }
}
