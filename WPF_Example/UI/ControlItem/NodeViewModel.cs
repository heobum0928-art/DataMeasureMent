using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using PropertyTools;
using ReringProject.Define;
using ReringProject.Sequence;

namespace ReringProject.UI {
    public class NodeViewModel : Observable { //, IDragSource, IDropTarget {
        public NodeViewModel Parent { get; private set; }

        public bool HasItems {
            get {
                this.LoadChildren();
                return this.children.Count > 0;
            }
        }
        /*
        public bool CanDrop(IDragSource node, DropPosition mode, DragDropEffect effect) {
            return node is NodeViewModel && (mode == DropPosition.Add || this.Parent != null);
        }

        public void Drop(IEnumerable<IDragSource> nodes, DropPosition mode, DragDropEffect effect, DragDropKeyStates initialKeyStates) {
            foreach (var node in nodes) {
                this.Drop(node, mode, effect == DragDropEffect.Copy);
            }
        }

        public void Drop(IDragSource node, DropPosition mode, bool copy) {
            var cvm = node as NodeViewModel;
            if (copy) cvm = new NodeViewModel(cvm.Node, cvm.Parent);

            switch (mode) {
                case DropPosition.Add:
                    this.Children.Add(cvm);
                    cvm.Parent = this;
                    this.IsExpanded = true;
                    break;
                case DropPosition.InsertBefore:
                    int index = this.Parent.Children.IndexOf(this);
                    Parent.Children.Insert(index, cvm);
                    cvm.Parent = this.Parent;
                    break;
                case DropPosition.InsertAfter:
                    int index2 = this.Parent.Children.IndexOf(this);
                    Parent.Children.Insert(index2 + 1, cvm);
                    cvm.Parent = this.Parent;
                    break;
            }
        }

        public bool IsDraggable {
            get {
                return false;
                //return Parent != null;
            }
        }
        */
        public void Detach() {
            this.Parent.Children.Remove(this);
            this.Parent = null;
        }

        private Node Node;

        private ObservableCollection<NodeViewModel> children;

        public ObservableCollection<NodeViewModel> Children {
            get {
                this.LoadChildren();
                return children;
            }
        }

        private void LoadChildren() {
            if (children == null) {
                children = new ObservableCollection<NodeViewModel>();
                var cc = this.Node as CompositeNode;
                if (cc != null) {
                    foreach (var child in cc.Children) {
                        // Debug.WriteLine("Creating VM for " + child.Name);
                        children.Add(new NodeViewModel(child, this));
                        // Thread.Sleep(1);
                    }
                }
            }
        }

        /// <summary>기존 children 컬렉션을 비우고 Node의 현재 Children으로 다시 채운다.
        /// ObservableCollection 이벤트를 통해 TreeListBox가 안전하게 갱신된다.</summary>
        public void ReloadChildren() {
            var col = this.Children; // LoadChildren 보장
            col.Clear();
            var cc = this.Node as CompositeNode;
            if (cc != null) {
                foreach (var child in cc.Children) {
                    col.Add(new NodeViewModel(child, this));
                }
            }
            RaisePropertyChanged("HasItems");
        }

        public string ImageSource {
            get { return this.Node.ImageSource; }
            set { this.Node.ImageSource = value; RaisePropertyChanged("ImageSource"); }
        }

        public string IconKey {
            get { return this.Node.IconKey; }
        }

        public string Name {
            get {
                return this.Node.Name;
            }
            set {
                this.Node.Name = value;
                RaisePropertyChanged("Name");
            }
        }

        public object Param {
            get { return this.Node.ParamData; }
            set {
                this.Node.ParamData = value;
                RaisePropertyChanged("ParamData");
            }
        }

        public string SequenceName {
            get {
                return this.Node.SequenceName;
            }
        }

        public EAction ActionID {
            get {
                return this.Node.ActionID;
            }
        }

        public ESequence SequenceID {
            get {
                return this.Node.SequenceID;
            }
        }
        
        public ENodeType NodeType {
            get {
                return this.Node.NodeType;
            }
        }

        private bool isExpanded;

        public bool IsExpanded {
            get {
                return this.isExpanded;
            }
            set {
                if (isExpanded == value) return;
                this.isExpanded = value;
                RaisePropertyChanged("IsExpanded");
                // Debug.WriteLine(Name + ".IsExpanded = " + value);
            }
        }

        private bool isSelected;

        public bool IsSelected {
            get {
                return this.isSelected;
            }
            set {
                if (isSelected == value) return;
                this.isSelected = value;
                RaisePropertyChanged("IsSelected");
                // Debug.WriteLine(Name + ".IsSelected = " + value);
            }
        }

        public int Level { get; set; }

        private bool isEditing;

        public bool IsEditing {
            get {
                return this.isEditing;
            }
            set {
                this.isEditing = value;
                RaisePropertyChanged("IsEditing");
                Debug.WriteLine(Name + ".IsEditing = " + value);
            }
        }

        public NodeViewModel(Node Node, NodeViewModel parent) {
            this.Node = Node;
            this.Parent = parent;
            this.IsExpanded = true;

            // PropertyGrid 에서 DatumName/ShotName/FAIName/MeasurementName 변경 시 Node.Name 동기화 + Tree 헤더 즉시 갱신.
            // NodeViewModel 와 Param 수명 동일 (recipe 재로드 시 둘 다 dispose) → unsubscribe 불필요.
            if (Node != null && Node.ParamData is INotifyPropertyChanged inpc) {
                inpc.PropertyChanged += OnParamPropertyChanged;
            }
        }

        // Param Name 변경 → Node.Name 동기화 + RaisePropertyChanged("Name").
        // MeasurementName 폴백: 빈 문자열이면 TypeName 사용 (InspectionListViewModel 와 일치).
        private void OnParamPropertyChanged(object sender, PropertyChangedEventArgs e) {
            string newName = null;
            switch (e.PropertyName) {
                case nameof(DatumConfig.DatumName):
                    newName = (sender as DatumConfig)?.DatumName;
                    break;
                case nameof(ShotConfig.ShotName):
                    newName = (sender as ShotConfig)?.ShotName;
                    break;
                case nameof(FAIConfig.FAIName):
                    newName = (sender as FAIConfig)?.FAIName;
                    break;
                case nameof(MeasurementBase.MeasurementName):
                    var m = sender as MeasurementBase;
                    if (m != null) {
                        newName = string.IsNullOrEmpty(m.MeasurementName) ? m.TypeName : m.MeasurementName;
                    }
                    break;
                // Datum 검출 실패 배지 갱신: Node.Name 갱신 아님 — RaisePropertyChanged("HasDetectFail") 만 발화하고 Name 분기 미진입.
                case nameof(DatumConfig.LastFindSucceeded):
                    RaisePropertyChanged("HasDetectFail");
                    return;
            }
            if (newName != null && this.Node != null) {
                this.Node.Name = newName;
                RaisePropertyChanged("Name");
                // 자동 재정렬 비활성 (Rename 시 사용자 이동 결과 무효화 방지)
                //if (this.Parent != null) {
                //    InspectionListViewModel.SortNodeChildren(this.Parent);
                //}
            }
        }

        public override string ToString() {
            return Name;
        }

        public NodeViewModel AddChild() {
            var cn = this.Node as CompositeNode;
            if (cn == null) {
                return null;
            }

            var newChild = new CompositeNode() { Name = "New node" };
            cn.Children.Add(newChild);
            var vm = new NodeViewModel(newChild, this);
            this.Children.Add(vm);
            return vm;
        }

        public void ExpandParents() {
            if (this.Parent != null) {
                this.Parent.ExpandParents();
                this.Parent.IsExpanded = true;
            }
        }

        public void ExpandAll() {
            this.IsExpanded = true;
            foreach (var child in this.Children) {
                child.ExpandAll();
            }
        }

        public void CollapseAll() {
            this.IsExpanded = false;
            foreach (var child in this.Children) {
                child.CollapseAll();
            }
        }

        // 기본 펼침을 Shot 레벨까지로 제한: 루트(recipe) + Sequence 노드만 펼치고,
        // 그 하위 Shot(Action)/Datum 노드는 접힘 상태 유지 (FAI/Measurement 상세는 사용자가 Shot 을 펼쳐야 보임).
        public void ExpandToShotLevel() {
            // Sequence 노드 이하(Shot/Datum)는 펼치지 않는다 — 자기 자신만 펼치고 재귀 중단
            if (this.NodeType == ENodeType.Sequence) {
                this.IsExpanded = true;
                foreach (var child in this.Children) {
                    child.IsExpanded = false;
                }
                return;
            }
            this.IsExpanded = true;
            foreach (var child in this.Children) {
                child.ExpandToShotLevel();
            }
        }
    }
}
