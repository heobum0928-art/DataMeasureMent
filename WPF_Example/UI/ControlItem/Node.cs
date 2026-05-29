
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

        //260530 hbk Phase 39.2 D-G4 — NodeType + Measurement TypeName 기반 Geometry ResourceKey
        //  Recipe/Sequence/Action/Datum/FAI = NodeType 별 단일 키.
        //  Measurement = "Icon.Meas." + TypeName (12 종 + DualImageEdgeDistance Plan 01 포함).
        //  미지정 / 알 수 없는 ParamData = "Icon.Measurement" fallback.
        //  최종 fallback (알 수 없는 NodeType) = "Icon.Default".
        public string IconKey { //260530 hbk Phase 39.2 D-G4
            get {
                switch (NodeType) {
                    case ENodeType.Recipe:      return "Icon.Recipe"; //260530 hbk Phase 39.2 D-G4
                    case ENodeType.Sequence:    return "Icon.Sequence"; //260530 hbk Phase 39.2 D-G4
                    case ENodeType.Action:      return "Icon.Action"; //260530 hbk Phase 39.2 D-G4
                    case ENodeType.SubSequence: return "Icon.Action"; //260530 hbk Phase 39.2 D-G4 — SubSequence = Action 과 동일 아이콘
                    case ENodeType.Datum:       return "Icon.Datum"; //260530 hbk Phase 39.2 D-G4
                    case ENodeType.FAI:         return "Icon.FAI"; //260530 hbk Phase 39.2 D-G4
                    case ENodeType.Measurement: //260530 hbk Phase 39.2 D-G4
                        var meas = ParamData as ReringProject.Sequence.MeasurementBase; //260530 hbk Phase 39.2 D-G4
                        if (meas != null && !string.IsNullOrEmpty(meas.TypeName)) //260530 hbk Phase 39.2 D-G4
                            return "Icon.Meas." + meas.TypeName; //260530 hbk Phase 39.2 D-G4
                        return "Icon.Measurement"; //260530 hbk Phase 39.2 D-G4 — fallback (ParamData 미상)
                }
                return "Icon.Default"; //260530 hbk Phase 39.2 D-G4
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
