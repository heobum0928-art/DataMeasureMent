using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using PropertyTools;
using ReringProject.Define;

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

        public string ImageSource {
            get { return this.Node.ImageSource; }
            set { this.Node.ImageSource = value; RaisePropertyChanged("ImageSource"); }
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
    }
}
