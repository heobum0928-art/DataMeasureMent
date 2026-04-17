
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
        Datum, //260409 hbk Phase 4: Datum node type (D-09)
        Measurement, //260417 hbk Phase 6 Plan 04: FAI 하위 Measurement node (D-24)
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
                        return "/Resource/layout.png"; //260409 hbk Phase 4: reuse layout icon for Datum (D-09)
                    case ENodeType.Measurement:
                        return "/Resource/chart.png"; //260417 hbk Phase 6 Plan 04: reuse chart icon for Measurement (D-24)
                }
                return "/Resource/process.png";
            }
            set { }
        }
        
    }

    public class CompositeNode : Node {
        public List<Node> Children { get; private set; }

        public CompositeNode() {
            Children = new List<Node>();
        }
    }
}
