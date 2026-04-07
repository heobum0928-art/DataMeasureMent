
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using PropertyTools.Wpf;
using ReringProject.Define;
using ReringProject.Sequence;
using ReringProject.Utility;

namespace ReringProject.UI {
    /// <summary>
    /// InspectionListView.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class InspectionListView : UserControl {
        private MainWindow mParentWindow;
        private InspectionListViewModel ViewModel;
        private InspectionViewModel _inspectionVm;
        public ParamBase SelectedParam { get; private set; } = null;
        private ParamBase CopiedParam = null;

        public InspectionListView() {
            InitializeComponent();
            ViewModel = new InspectionListViewModel();
            this.DataContext = ViewModel;
        }

        private bool _IsEditable = false;
        public bool IsEditable {
            get {
                return _IsEditable;
            }
            set {
                if (value) {
                    //treelistview의 seuqnce 항목을 펼친다. or 자식 항목 표시
                    ViewModel.RootModel.ExpandAll();
                    grid_editor.Visibility = Visibility.Visible;
                    gridSplitter_editor.Visibility = Visibility.Visible;
                    colDefinition_editor.Width = new GridLength(6, GridUnitType.Star);
                }
                else {
                    grid_editor.Visibility = Visibility.Collapsed;
                    gridSplitter_editor.Visibility = Visibility.Collapsed;
                    colDefinition_editor.Width = new GridLength(0, GridUnitType.Star);
                    CopiedParam = null;
                    button_paste.IsEnabled = false;
                }

                ParamEditor.IsEnabled = value;
                btn_RecipeSelect.IsEnabled = value;
                _IsEditable = value;
            }
        }

        private void ListView_Loaded(object sender, RoutedEventArgs e) {
            try {
                mParentWindow = (MainWindow)Window.GetWindow(this);
                var recipeManager = SystemHandler.Handle.Sequences.RecipeManager;
                _inspectionVm = new InspectionViewModel(recipeManager);
                mParentWindow.mainView.SetFAIResultSource(_inspectionVm);
                // Auto-expand tree so nodes are visible even when IsEditable == false
                ViewModel.RootModel.ExpandAll();
            }
            catch (Exception ex) {
                System.Diagnostics.Debug.WriteLine("InspectionListView.ListView_Loaded error: " + ex.Message);
            }
        }

        private void Btn_RecipeSelect_Click(object sender, RoutedEventArgs e) {
            ContextMenu cm = this.FindResource("menu_control") as ContextMenu;
            cm.PlacementTarget = sender as Button;
            cm.IsOpen = true;
        }

        private void MenuItem_Save_Click(object sender, RoutedEventArgs e) {
            mParentWindow.SaveRecipe(tb_RecipeName.Text);
        }

        private void MenuItem_Save_As_Click(object sender, RoutedEventArgs e) {
            string curRecipe = SystemHandler.Handle.Setting.CurrentRecipeName;
            bool dlgResult = TextInputBox.Show("Enter the name of new recipe to copy.", curRecipe, out string inputText);
            if (dlgResult == false) {
                return;
            }
            string newName = inputText;
            if (newName == curRecipe) {
                CustomMessageBox.Show("Error", "Recipe name to be copied must be different.", MessageBoxImage.Error);
                return;
            }
            if (RecipeFiles.Handle.HasRecipe(newName)) {
                if (CustomMessageBox.ShowConfirmation(newName + " Has Already Exists.", "Are you sure you want to overwrite the existing directory?", MessageBoxButton.YesNo) != MessageBoxResult.Yes) {
                    return;
                }
            }
            RecipeFiles.Handle.Copy(curRecipe, newName);
            SystemHandler.Handle.Recipes.CollectRecipe();
        }

        private void MenuItem_open_Click(object sender, RoutedEventArgs e) {
            mParentWindow.PopupView(EPageType.Recipe);
        }

        public void OnLoadRecipe(string name) {
            ViewModel.CurrentRecipe = name;
            NodeViewModel root = treeListBox_sequence.Items[0] as NodeViewModel;
            root.Name = name;
        }

        private void Btn_start_Click(object sender, RoutedEventArgs e) {
            if (treeListBox_sequence.SelectedIndex < 0) return;
            //get selected sequence
            if (treeListBox_sequence.SelectedItem is NodeViewModel) {
                NodeViewModel node = treeListBox_sequence.SelectedItem as NodeViewModel;

                ESequence seqID;
                EAction actID;
                if (node.NodeType == ENodeType.Action) {
                    seqID = node.SequenceID;
                    actID = node.ActionID;
                    mParentWindow.StartSequence(seqID, actID);
                    return;
                }
                else if(node.NodeType == ENodeType.Sequence) {
                    seqID = node.SequenceID;
                    // Run the first action exposed in the UI.
                    SequenceBase seq = SystemHandler.Handle.Sequences[seqID];
                    if(seq != null) {
                        actID = GetDefaultRunnableAction(seq);
                        if (actID != EAction.Unknown) {
                            mParentWindow.StartSequence(seqID, actID);
                            return;
                        }
                    }
                }
                //show error msg
                CustomMessageBox.Show("Error", "There is no action to run.\nSelect the sequence or action you want to perform.", MessageBoxImage.Error);
            }
        }

        private static EAction GetDefaultRunnableAction(SequenceBase sequence) {
            if (sequence == null) {
                return EAction.Unknown;
            }

            for (var i = 0; i < sequence.ActionCount; i++) {
                return sequence[i].ID;
            }

            return EAction.Unknown;
        }

        public void SetSelectionChange(string seqName) {
            NodeViewModel root = treeListBox_sequence.Items[0] as NodeViewModel;
            root.IsExpanded = true;
            for (int i = 0; i < treeListBox_sequence.Items.Count; i++) {
                NodeViewModel item = treeListBox_sequence.Items[i] as NodeViewModel;
                if((item.NodeType == ENodeType.Sequence) && (item.Name == seqName)) {
                    item.IsSelected = true;
                    treeListBox_sequence.ScrollIntoView(item);
                }
                else {
                    item.IsSelected = false;
                }
            }
        }

        private void InspectionList_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            if (mParentWindow == null) mParentWindow = (MainWindow)Window.GetWindow(this);
            button_light.IsEnabled = false;
            button_grab.IsEnabled = false;
            //button_showConfig.IsEnabled = false;

            object source = e.Source;
            if(source is TreeListBox) {
                TreeListBox list = source as TreeListBox;
                if(list.SelectedItem is NodeViewModel) {
                    NodeViewModel item = list.SelectedItem as NodeViewModel;
                    object itemParam = item.Param;

                    //param
                    if (itemParam is ParamBase) { //action or FAI
                        ParamBase param = itemParam as ParamBase;
                        if (itemParam is ICameraParam) {
                            button_grab.IsEnabled = true;
                            button_loadImage.IsEnabled = true;
                            button_light.IsEnabled = true;
                        }
                        mParentWindow.mainView.SetParam(item.SequenceID, param);
                        SelectedParam = param;
                    }
                    else { //recipe
                        mParentWindow.mainView.SetParam(item.SequenceID, null);
                        SelectedParam = null;
                    }

                    // FAI CRUD button state + InspectionViewModel update
                    if (item.NodeType == ENodeType.FAI) {
                        button_addFAI.IsEnabled = true;
                        button_removeFAI.IsEnabled = true;
                        button_renameFAI.IsEnabled = true;
                        if (_inspectionVm != null && itemParam is FAIConfig faiConfig) {
                            _inspectionVm.OnFAISelected(faiConfig);
                            mParentWindow.mainView.DisplayFAIImage(faiConfig);
                        }
                    }
                    else if (item.NodeType == ENodeType.Action) {
                        button_addFAI.IsEnabled = true;
                        button_removeFAI.IsEnabled = false;
                        button_renameFAI.IsEnabled = false;
                        if (_inspectionVm != null) {
                            _inspectionVm.OnActionSelected(item);
                        }
                    }
                    else {
                        button_addFAI.IsEnabled = false;
                        button_removeFAI.IsEnabled = false;
                        button_renameFAI.IsEnabled = false;
                        _inspectionVm?.ClearResults();
                    }
                }
            }
        }

        private void button_copy_Click(object sender, RoutedEventArgs e) {
            if(SelectedParam != null) {
                CopiedParam = SelectedParam;
                mParentWindow.statusBar.Model.SetText("Copied : " + CopiedParam.ToString());
                button_paste.IsEnabled = true;
            }
        }

        private void button_paste_Click(object sender, RoutedEventArgs e) {
            if((SelectedParam != null) && (CopiedParam != null)) {
                //confirm message
                if (SelectedParam == CopiedParam) return;

                MessageBoxResult res = CustomMessageBox.ShowConfirmation("Confirmation", string.Format("Would you like to paste {0} into {1}?", CopiedParam.ToString(), SelectedParam.ToString()), MessageBoxButton.OKCancel);
                if(res != MessageBoxResult.OK) {
                    return;
                }
                //paste
                if(CopiedParam.CopyTo(SelectedParam) == false) {
                    //fail
                    CustomMessageBox.Show("Fail to Copy", string.Format("Copy Failed From {0} into {1}", CopiedParam.ToString(), SelectedParam.ToString()), MessageBoxImage.Error);
                    return;
                }
                //success
                mParentWindow.statusBar.Model.SetText(string.Format("Pasted : {0} to {1}",CopiedParam.ToString(), SelectedParam.ToString()));
                int index = treeListBox_sequence.SelectedIndex;

                //reselect (update)
                treeListBox_sequence.UnselectAll();
                treeListBox_sequence.SelectedIndex = index;
            }
        }


        private void button_grab_Click(object sender, RoutedEventArgs e) {
            if (SelectedParam == null) return;
            if (!(SelectedParam is ICameraParam)) return;
            if (SystemHandler.Handle.Sequences.IsIdle == false) {
                //show message 
                return;
            }
            //Debug.WriteLine($"217-InspectionListView.xaml.cs SelectedParam:{SelectedParam.ToString()}");
            //list에서 선택된 node의 param을 가져옴
            //param으로 grab 수행하여 결과 drawing
            ICameraParam camParam = SelectedParam as ICameraParam;
            mParentWindow.mainView.GrabAndDisplay(camParam);
        }

        private void button_loadImage_Click(object sender, RoutedEventArgs e) {
            if (SelectedParam == null) return;
            if (!(SelectedParam is ICameraParam)) return;

            ICameraParam camParam = SelectedParam as ICameraParam;
            mParentWindow.mainView.LoadAndDisplay(camParam);
        }

        private void button_light_Click(object sender, RoutedEventArgs e) {
            //light
            if (SelectedParam == null) return;
            if (!(SelectedParam is ICameraParam)) return;
            if(SystemHandler.Handle.Sequences.IsIdle == false) {
                return;
            }
            ICameraParam camParam = SelectedParam as ICameraParam;
            SystemHandler.Handle.Lights.SetLevel(camParam.LightGroupName, camParam.LightLevel);
            SystemHandler.Handle.Lights.SetOnOff(camParam.LightGroupName, true);
        }

        private void Btn_AddFAI_Click(object sender, RoutedEventArgs e) {
            try {
                if (!(treeListBox_sequence.SelectedItem is NodeViewModel selectedNode)) return;
                NodeViewModel actionNode = selectedNode;
                if (selectedNode.NodeType == ENodeType.FAI) {
                    actionNode = selectedNode.Parent;
                }
                if (actionNode == null || actionNode.NodeType != ENodeType.Action) return;
                if (!(actionNode.Param is ShotConfig shot)) return;

                string defaultName = "FAI_" + shot.FAIList.Count;
                if (!TextInputBox.Show("FAI 이름 입력", defaultName, out string name)) return;

                FAIConfig newFai = _inspectionVm.AddFAI(shot, name);
                if (newFai != null) {
                    ViewModel.AddFAINode(actionNode, newFai, actionNode.SequenceID, actionNode.ActionID);
                }
            }
            catch (Exception ex) {
                CustomMessageBox.Show("FAI 추가 오류", ex.Message, System.Windows.MessageBoxImage.Error);
            }
        }

        private void Btn_RemoveFAI_Click(object sender, RoutedEventArgs e) {
            try {
                if (!(treeListBox_sequence.SelectedItem is NodeViewModel selectedNode)) return;
                if (selectedNode.NodeType != ENodeType.FAI) return;
                if (!(selectedNode.Param is FAIConfig fai)) return;
                NodeViewModel actionNode = selectedNode.Parent;
                if (actionNode == null || !(actionNode.Param is ShotConfig shot)) return;

                // Per D-10: confirmation dialog
                MessageBoxResult result = CustomMessageBox.ShowConfirmation(
                    "FAI 삭제",
                    string.Format("FAI \"{0}\"을(를) 삭제합니다. 계속하시겠습니까?", fai.FAIName),
                    MessageBoxButton.YesNo);
                if (result != MessageBoxResult.Yes) return;

                int index = shot.FAIList.IndexOf(fai);
                if (index >= 0) {
                    _inspectionVm.RemoveFAI(shot, index);
                    selectedNode.Detach();
                }
                _inspectionVm?.ClearResults();
            }
            catch (Exception ex) {
                CustomMessageBox.Show("FAI 삭제 오류", ex.Message, System.Windows.MessageBoxImage.Error);
            }
        }

        private void Btn_RenameFAI_Click(object sender, RoutedEventArgs e) {
            try {
                if (!(treeListBox_sequence.SelectedItem is NodeViewModel selectedNode)) return;
                if (selectedNode.NodeType != ENodeType.FAI) return;
                if (!(selectedNode.Param is FAIConfig fai)) return;

                if (!TextInputBox.Show("FAI 이름 수정", fai.FAIName, out string newName)) return;
                fai.FAIName = newName;
                selectedNode.Name = newName;
            }
            catch (Exception ex) {
                CustomMessageBox.Show("FAI 이름 수정 오류", ex.Message, System.Windows.MessageBoxImage.Error);
            }
        }
    }
}
