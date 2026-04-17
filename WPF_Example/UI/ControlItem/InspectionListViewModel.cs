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
            //260417 hbk Phase 6-04 UAT: 최초 트리 생성 후 DisplayName 편집 훅 연결 (D-01)
            HookSequenceDisplayNameUpdates();
        }

        private void CreateSequenceNode(CompositeNode model) {

            //sequence
            for(int i = 0; i < pSystemHandle.Sequences.Count; i++) {
                SequenceBase seq = pSystemHandle.Sequences[i];

                //260417 hbk Phase 6 Plan 04: Sequence DisplayName 표시 (D-01)
                string seqDisplay = (seq as InspectionSequence)?.GetDisplayName() ?? seq.Name;
                var seqNode = new CompositeNode { Name = seqDisplay, NodeType = ENodeType.Sequence, ParamData = seq.Param, SequenceName = seq.Name, SequenceID = seq.ID };
                model.Children.Add(seqNode);

                this.Count++;

                //260417 hbk Phase 6 Plan 04: Datum 노드를 Sequence 직접 자식으로 추가 — Action과 형제 (D-25)
                if (seq is InspectionSequence inspSeq) {
                    foreach (DatumConfig datum in inspSeq.DatumConfigs) {
                        var datumNode = new CompositeNode {
                            Name = datum.DatumName ?? "Datum",
                            NodeType = ENodeType.Datum,
                            ParamData = datum,
                            SequenceName = seq.Name,
                            SequenceID = seq.ID
                        };
                        seqNode.Children.Add(datumNode);
                        this.Count++;
                    }
                }

                //action
                for (int j = 0; j < seq.ActionCount; j++) {
                    ActionBase act = seq[j];
                    var actNode = new CompositeNode { Name = act.Name, NodeType = ENodeType.Action, ParamData = act.Param, SequenceName = seq.Name, SequenceID = seq.ID, ActionID = act.ID };
                    seqNode.Children.Add(actNode);
                    this.Count++;

                    // FAI child nodes: shown when action param is ShotConfig (IsDynamicFAIMode)
                    if (act.Param is ShotConfig shot) {
                        foreach (FAIConfig fai in shot.FAIList) {
                            var faiNode = new CompositeNode { Name = fai.FAIName, NodeType = ENodeType.FAI, ParamData = fai, SequenceName = seq.Name, SequenceID = seq.ID, ActionID = act.ID };
                            actNode.Children.Add(faiNode);
                            this.Count++;

                            //260417 hbk Phase 6 Plan 04: FAI 하위 Measurement 노드 추가 (D-24)
                            foreach (MeasurementBase meas in fai.Measurements) {
                                var measNode = new Node {
                                    Name = string.IsNullOrEmpty(meas.MeasurementName) ? meas.TypeName : meas.MeasurementName,
                                    NodeType = ENodeType.Measurement,
                                    ParamData = meas,
                                    SequenceName = seq.Name,
                                    SequenceID = seq.ID,
                                    ActionID = act.ID
                                };
                                faiNode.Children.Add(measNode);
                                this.Count++;
                            }
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

        //260417 hbk Phase 6 Plan 04: Datum 노드를 Sequence 직접 자식으로 삽입 (D-25)
        public void AddDatumNode(NodeViewModel seqNode, DatumConfig datum) {
            if (seqNode == null || datum == null) return;
            var datumNode = new CompositeNode {
                Name = datum.DatumName ?? "Datum",
                NodeType = ENodeType.Datum,
                ParamData = datum,
                SequenceName = seqNode.SequenceName,
                SequenceID = seqNode.SequenceID
            };
            var datumVm = new NodeViewModel(datumNode, seqNode);
            seqNode.Children.Add(datumVm);
        }

        //260417 hbk Phase 6 Plan 04: Measurement 노드를 FAI 자식으로 삽입 (D-24)
        public void AddMeasurementNode(NodeViewModel faiNode, MeasurementBase meas) {
            if (faiNode == null || meas == null) return;
            var measNode = new Node {
                Name = string.IsNullOrEmpty(meas.MeasurementName) ? meas.TypeName : meas.MeasurementName,
                NodeType = ENodeType.Measurement,
                ParamData = meas,
                SequenceName = faiNode.SequenceName,
                SequenceID = faiNode.SequenceID,
                ActionID = faiNode.ActionID
            };
            var measVm = new NodeViewModel(measNode, faiNode);
            faiNode.Children.Add(measVm);
        }
        
        /// <summary>트리를 재구축한다. Dynamic FAI 모드 전환 후 호출.
        /// Root 교체 대신 ObservableCollection in-place 갱신으로 TreeListBox NullRef 방지.</summary>
        //260408 hbk RaisePropertyChanged("Root") 제거 — TreeListBox 내부 NullRef 원인
        public void RebuildTree() {
            this.Model.Children.Clear();
            this.Count = 0;
            CreateSequenceNode(this.Model);
            RootModel.ReloadChildren();
            //260417 hbk Phase 6-04 UAT: 트리 재구축 후 DisplayName 편집 훅 재연결 + 초기 라벨 동기화 (D-01)
            HookSequenceDisplayNameUpdates();
        }

        //260417 hbk Phase 6-04 UAT: Sequence 노드의 InspectionMasterParam.DisplayName 변경 시 트리 라벨 즉시 갱신 (D-01)
        private void HookSequenceDisplayNameUpdates() {
            if (RootModel == null) return;
            foreach (var child in RootModel.Children) {
                if (child.NodeType != ENodeType.Sequence) continue;
                if (!(child.Param is InspectionMasterParam master)) continue;

                // 중복 구독 방지
                master.PropertyChanged -= OnSequenceMasterPropertyChanged;
                master.PropertyChanged += OnSequenceMasterPropertyChanged;

                // 초기 라벨 동기화 (DisplayName 비어있으면 SequenceName 폴백)
                child.Name = string.IsNullOrEmpty(master.DisplayName) ? child.SequenceName : master.DisplayName;
            }
        }

        //260417 hbk Phase 6-04 UAT: DisplayName PropertyChanged 핸들러 (D-01)
        private void OnSequenceMasterPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e) {
            if (e.PropertyName != "DisplayName") return;
            if (!(sender is InspectionMasterParam master)) return;

            foreach (var child in RootModel.Children) {
                if (child.NodeType != ENodeType.Sequence) continue;
                if (ReferenceEquals(child.Param, master)) {
                    string newLabel = string.IsNullOrEmpty(master.DisplayName) ? child.SequenceName : master.DisplayName;
                    child.Name = newLabel; // NodeViewModel.Name setter 가 RaisePropertyChanged("Name") 발생
                    break;
                }
            }
        }

        public void Select(int count) {
            var children = this.RootModel.Children as IList<NodeViewModel>;
            for (int i = 0; i < count; i++) {
                children[i].IsSelected = true;
            }
        }        
    }
}
