using ReringProject.Define;
using ReringProject.Sequence;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ReringProject.UI;
using ReringProject.Utility;

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

        // 자연정렬 비교자 (Shot2 < Shot10)
        private static readonly NaturalStringComparer _naturalComparer = new NaturalStringComparer();

        /// <summary>지정한 부모 노드의 직속 children 만 Name 기반 자연정렬 (asc).
        /// ObservableCollection 이벤트로 TreeListBox 안전 갱신 (clear + re-add).</summary>
        public static void SortNodeChildren(NodeViewModel parent) {
            if (parent == null) return;
            var list = parent.Children;
            if (list == null || list.Count <= 1) return;
            var sorted = new List<NodeViewModel>(list);
            sorted.Sort((a, b) => _naturalComparer.Compare(a != null ? a.Name : null, b != null ? b.Name : null));
            // 변경 감지 (no-op skip 으로 RaisePropertyChanged 폭발 방지)
            bool dirty = false;
            for (int i = 0; i < sorted.Count; i++) {
                if (!ReferenceEquals(sorted[i], list[i])) { dirty = true; break; }
            }
            if (!dirty) return;
            list.Clear();
            foreach (var item in sorted) list.Add(item);
        }

        public void SortAllLevels() {
            SortRecursive(RootModel);
        }
        private static void SortRecursive(NodeViewModel node) {
            if (node == null) return;
            SortNodeChildren(node);
            foreach (var child in node.Children)
                SortRecursive(child);
        }

        public InspectionListViewModel() {
            pSystemHandle = SystemHandler.Handle;

            this.Model = new CompositeNode { Name = CurrentRecipe, NodeType = ENodeType.Recipe, ParamData = SystemHandler.Handle.Sequences };
            CreateSequenceNode(this.Model);
            this.RootModel = new NodeViewModel(this.Model, null);
            HookSequenceDisplayNameUpdates();
            // 자동정렬 비활성 (사용자 Move ▲▼ 방식 우선). NaturalStringComparer 코드는 향후 toggle 도입 시 재활용 위해 보존
            //SortAllLevels();
        }

        private void CreateSequenceNode(CompositeNode model) {

            //sequence
            for(int i = 0; i < pSystemHandle.Sequences.Count; i++) {
                SequenceBase seq = pSystemHandle.Sequences[i];

                string seqDisplay = (seq as InspectionSequence)?.GetDisplayName() ?? seq.Name;
                var seqNode = new CompositeNode { Name = seqDisplay, NodeType = ENodeType.Sequence, ParamData = seq.Param, SequenceName = seq.Name, SequenceID = seq.ID };
                model.Children.Add(seqNode);

                this.Count++;

                // Datum 노드를 Sequence 직접 자식으로 추가 — Action 과 형제
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
            // 자동정렬 비활성
            //SortNodeChildren(actionNode);
        }

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
            // 자동정렬 비활성
            //SortNodeChildren(seqNode);
        }

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
            // 자동정렬 비활성
            //SortNodeChildren(faiNode);
        }

        /// <summary>트리를 재구축한다. Dynamic FAI 모드 전환 후 호출.
        /// Root 교체 대신 ObservableCollection in-place 갱신으로 TreeListBox NullRef 방지.</summary>
        public void RebuildTree() {
            this.Model.Children.Clear();
            this.Count = 0;
            CreateSequenceNode(this.Model);
            RootModel.ReloadChildren();
            HookSequenceDisplayNameUpdates();
            // 자동정렬 비활성 (recipe 로드 후 ParamBase 컬렉션 순서 보존)
            //SortAllLevels();
        }

        // 사용자 ▲▼ 이동: NodeViewModel.Children + ParamBase 컬렉션 동시 swap → INI 저장 시 보존
        public static bool MoveNode(NodeViewModel node, int direction) {
            if (node == null || node.Parent == null) return false;
            var siblings = node.Parent.Children;
            int idx = siblings.IndexOf(node);
            if (idx < 0) return false;
            int newIdx = idx + direction;
            if (newIdx < 0 || newIdx >= siblings.Count) return false;

            // [1] ParamBase 컬렉션도 swap (영구 보존)
            if (!SwapParamCollection(node, siblings[newIdx])) return false;

            // [2] NodeViewModel.Children swap (UI 즉시 갱신)
            siblings.Move(idx, newIdx);
            return true;
        }

        // node 및 swap target 의 ParamBase owning collection 에서 index swap
        private static bool SwapParamCollection(NodeViewModel node, NodeViewModel swapWith) {
            if (node == null || swapWith == null) return false;
            object myParam = node.Param;
            object otherParam = swapWith.Param;
            if (myParam == null || otherParam == null) return false;

            switch (node.NodeType) {
                case ENodeType.FAI: {
                    // FAI 의 owner = parent Action(Shot) 의 ShotConfig.FAIList
                    if (node.Parent != null && node.Parent.Param is ShotConfig shot) {
                        return SwapInList(shot.FAIList, myParam as FAIConfig, otherParam as FAIConfig);
                    }
                    break;
                }
                case ENodeType.Measurement: {
                    // Measurement 의 owner = parent FAI 의 FAIConfig.Measurements
                    if (node.Parent != null && node.Parent.Param is FAIConfig fai) {
                        return SwapInList(fai.Measurements, myParam as MeasurementBase, otherParam as MeasurementBase);
                    }
                    break;
                }
                case ENodeType.Datum: {
                    // Datum 의 owner = Sequence 의 InspectionSequence.DatumConfigs
                    SequenceBase seq = SystemHandler.Handle.Sequences[node.SequenceID];
                    if (seq is InspectionSequence inspSeq) {
                        return SwapInList(inspSeq.DatumConfigs, myParam as DatumConfig, otherParam as DatumConfig);
                    }
                    break;
                }
                case ENodeType.Action: {
                    // Shot (Action + ShotConfig Param) 의 owner = RecipeManager.Shots
                    if (myParam is ShotConfig myShot && otherParam is ShotConfig otherShot) {
                        var mgr = SystemHandler.Handle.Sequences.RecipeManager;
                        return SwapInList(mgr.Shots, myShot, otherShot);
                    }
                    return true; //  일반 Action (ShotConfig 아님) 은 ParamBase 컬렉션 변경 0 — UI 만 swap
                }
                default:
                    return true; // Recipe/Sequence 노드 등은 UI 만 swap
            }
            return false;
        }

        private static bool SwapInList<T>(System.Collections.Generic.List<T> list, T a, T b) where T : class {
            if (list == null || a == null || b == null) return false;
            int ia = list.IndexOf(a);
            int ib = list.IndexOf(b);
            if (ia < 0 || ib < 0) return false;
            T tmp = list[ia];
            list[ia] = list[ib];
            list[ib] = tmp;
            return true;
        }

        // Sequence 노드의 InspectionMasterParam.DisplayName 변경 시 트리 라벨 즉시 갱신
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
