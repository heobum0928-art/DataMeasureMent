using ReringProject.Define;
using ReringProject.Sequence;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ReringProject.UI;

namespace ReringProject.UI {
    public class InspectionListViewModel : Observable {
        
        private SystemHandler pSystemHandle = null;

        //tree list box
        private CompositeNode Model { get; set; }
    
        public NodeViewModel RootModel { get; set; }

        
        public IEnumerable Root {
            get { yield return RootModel; }
        }

        public IEnumerable Children {
            get { return RootModel.Children; }
        }

        private string _CurrentRecipe;
        public string CurrentRecipe {
            get {
                return _CurrentRecipe;
            }
            set {
                _CurrentRecipe = value;
                this.Model.Name = value;
                RaisePropertyChanged("CurrentRecipe");
                RaisePropertyChanged("Model");
            }
        }

        public int Count { get; set; }

        public InspectionListViewModel() {
            pSystemHandle = SystemHandler.Handle;
            
            this.Model = new CompositeNode { Name = CurrentRecipe, NodeType = ENodeType.Recipe, ParamData = SystemHandler.Handle.Sequences };
            CreateSequenceNode(this.Model);
            this.RootModel = new NodeViewModel(this.Model, null);
        }

        private void CreateSequenceNode(CompositeNode model) {

            //sequence
            for(int i = 0; i < pSystemHandle.Sequences.Count; i++) {
                SequenceBase seq = pSystemHandle.Sequences[i];

                var seqNode = new CompositeNode { Name = seq.Name, NodeType = ENodeType.Sequence, ParamData = seq.Param, SequenceName = seq.Name, SequenceID = seq.ID };
                model.Children.Add(seqNode);

                this.Count++;

                //action
                for (int j = 0; j < seq.ActionCount; j++) {
                    ActionBase act = seq[j];
                    var actNode = new CompositeNode { Name = act.Name, NodeType = ENodeType.Action, ParamData = act.Param, SequenceName = seq.Name, SequenceID = seq.ID, ActionID = act.ID };
                    seqNode.Children.Add(actNode);
                    this.Count++;

                    // FAI child nodes: shown when action param is ShotConfig (IsDynamicFAIMode)
                    if (act.Param is ShotConfig shot) {
                        //260409 hbk Phase 4: Datum child node (D-09)
                        if (shot.Datum != null) {
                            var datumNode = new CompositeNode {
                                Name = "Datum",
                                NodeType = ENodeType.Datum,
                                ParamData = shot.Datum,
                                SequenceName = seq.Name,
                                SequenceID = seq.ID,
                                ActionID = act.ID
                            };
                            actNode.Children.Add(datumNode);
                        }
                        foreach (FAIConfig fai in shot.FAIList) {
                            var faiNode = new CompositeNode { Name = fai.FAIName, NodeType = ENodeType.FAI, ParamData = fai, SequenceName = seq.Name, SequenceID = seq.ID, ActionID = act.ID };
                            actNode.Children.Add(faiNode);
                            this.Count++;
                        }
                    }
                }
            }
        }

        /// <summary>Adds a FAI child node under the given action NodeViewModel at runtime.</summary>
        public void AddFAINode(NodeViewModel actionNode, FAIConfig fai, ESequence seqID, EAction actID) {
            if (actionNode == null || fai == null) return;
            var cn = actionNode.Children;
            var faiNode = new CompositeNode {
                Name = fai.FAIName,
                NodeType = ENodeType.FAI,
                ParamData = fai,
                SequenceName = actionNode.SequenceName,
                SequenceID = seqID,
                ActionID = actID
            };
            // Add to the underlying composite node's children list
            // NodeViewModel.Children are backed by the composite node's Children list via LoadChildren
            // We need to add to the vm's children collection directly
            var faiVm = new NodeViewModel(faiNode, actionNode);
            cn.Add(faiVm);
        }
        
        /// <summary>트리를 재구축한다. Dynamic FAI 모드 전환 후 호출.
        /// Root 교체 대신 ObservableCollection in-place 갱신으로 TreeListBox NullRef 방지.</summary>
        //260408 hbk RaisePropertyChanged("Root") 제거 — TreeListBox 내부 NullRef 원인
        public void RebuildTree() {
            this.Model.Children.Clear();
            this.Count = 0;
            CreateSequenceNode(this.Model);
            RootModel.ReloadChildren();
        }

        public void Select(int count) {
            var children = this.RootModel.Children as IList<NodeViewModel>;
            for (int i = 0; i < count; i++) {
                children[i].IsSelected = true;
            }
        }        
    }
}
