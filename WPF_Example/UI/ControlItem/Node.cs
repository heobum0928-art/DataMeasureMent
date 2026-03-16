
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
