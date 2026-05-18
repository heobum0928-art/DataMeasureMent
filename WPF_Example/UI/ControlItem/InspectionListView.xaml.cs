
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives; //260426 hbk Phase 13-06 — TextBoxBase / Selector routed event
using System.Windows.Threading; //260426 hbk Phase 13-07 — DispatcherPriority for re-teach defer
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
        private bool _isRebinding = false; //260503 hbk Phase 17 bugfix — SelectionChanged 무한루프 방지 (SelectedObject 재할당 중 ComboBox 초기화 이벤트 차단)

        //260424 hbk Phase 12 Gap-3 — 외부(MainView Datum 티칭)에서 DatumConfig 필드 write-back 후 PropertyGrid 재바인딩 트리거
        // 자동 속성(set;/get;)은 INotifyPropertyChanged 미발동 → SelectedObject null 후 재할당으로 강제 재렌더
        public void RefreshParamEditor() {
            if (ParamEditor == null) return;
            var current = SelectedParam;
            _isRebinding = true; //260503 hbk Phase 17 bugfix — 재할당 중 SelectionChanged 무한루프 차단
            try {
                ParamEditor.SelectedObject = null;
                ParamEditor.SelectedObject = current;
            } finally {
                _isRebinding = false;
            }
        }

        //260426 hbk Phase 13-06 — UAT Test 6 (minor) gap closure: PropertyGrid 파라미터 변경 → MainView 자동 재티칭 라우팅
        //  ParamEditor 의 routed TextBoxBase.LostFocus / Selector.SelectionChanged 가 fire 시 호출.
        //  SelectedObject 가 teached DatumConfig 일 때만 MainView.NotifyDatumParamMaybeChanged 로 라우팅.
        //260426 hbk Phase 13-07 — UAT Test E cascade fix:
        //  PropertyGrid 내부 navigation Selector(카테고리 ListBox 등) 의 SelectionChanged 가 bubble 되어
        //  Datum→FAI 트리 전환 중간 시점에 SelectedObject 가 아직 Datum 인 채로 fire → 동기 Halcon 호출이
        //  binding 전환을 깨던 회귀를 차단. (a) OriginalSource 필터로 실제 ComboBox/TextBox 셀만 통과
        //  (b) Dispatcher.BeginInvoke(Background) 로 binding 안정 후 재검사.
        private void TryTriggerDatumAutoReteach() {
            if (ParamEditor == null) return;
            //260426 hbk Phase 13-07 — binding 전환이 끝난 후 재검사하도록 Background 우선순위로 defer
            Dispatcher.BeginInvoke(new Action(() => {
                var datum = ParamEditor.SelectedObject as DatumConfig;
                if (datum == null) return;
                if (mParentWindow == null) return;
                var mv = mParentWindow.mainView;
                if (mv == null) return;
                mv.NotifyDatumParamMaybeChanged(datum);
            }), DispatcherPriority.Background);
        }

        //260426 hbk Phase 13-06 — TextBox(숫자/문자 셀) LostFocus 시점: WPF default UpdateSourceTrigger=LostFocus 이므로 binding 은 이미 DatumConfig 에 push 완료
        //260426 hbk Phase 13-07 — OriginalSource 가 실제 TextBoxBase(셀) 일 때만 통과 (헤더/네비게이션 LostFocus 차단)
        private void OnParamEditorLostFocus(object sender, RoutedEventArgs e) {
            if (!(e.OriginalSource is TextBoxBase)) return;
            TryTriggerDatumAutoReteach();
        }

        //260426 hbk Phase 13-06 — ComboBox(EdgeDirection/EdgePolarity 등) SelectionChanged 시점: binding 은 즉시 push
        //260426 hbk Phase 13-07 — OriginalSource 가 실제 ComboBox 일 때만 통과 (PropertyGrid 내부 카테고리 ListBox 등 차단)
        //260503 hbk Phase 17 D-10 — AlgorithmType combobox 변경 감지 + 5-step 리셋 (force rebind + 검출 reset + 시각화 clear + ROI 보존 + 자동 재검출 X)
        private void OnParamEditorSelectionChanged(object sender, SelectionChangedEventArgs e) {
            if (!(e.OriginalSource is ComboBox)) return;
            if (_isRebinding) return; //260503 hbk Phase 17 bugfix — SelectedObject 재할당 중 ComboBox 초기화 이벤트 무시 (무한루프 방지)
            //260503 hbk Phase 17 bugfix#2 — Layout pass 에서 비동기로 도착하는 초기 binding SelectionChanged 차단.
            //  PropertyGrid 가 SelectedObject 재할당 후 ComboBox 를 재생성 → binding 으로 SelectedValue 설정 시
            //  RemovedItems.Count == 0 (no prior selection). 사용자 실제 변경은 RemovedItems 에 old value 포함.
            //  _isRebinding 가드는 동기 경로만 보호 → 비동기 deferred 이벤트는 이 필터로 차단해야 force-rebind → SelectionChanged 무한 루프 방지.
            if (e.RemovedItems == null || e.RemovedItems.Count == 0) return;

            //260503 hbk Phase 17 D-10 — AlgorithmType combobox 변경 분기 (다른 ComboBox: EdgeDirection / EdgeSelection / RadialDirection 등은 기존 경로)
            //260509 hbk Phase 20 — 삼항 → 명시적 if/else (D-01, P-4)
            DatumConfig datum = null;
            if (ParamEditor != null) datum = ParamEditor.SelectedObject as DatumConfig;
            if (datum != null) {
                var combo = e.OriginalSource as ComboBox;
                //260509 hbk Phase 20 — 삼항 → 명시적 if/else (D-01, P-4)
                string newValue = null;
                if (combo != null) newValue = combo.SelectedValue as string;
                //260503 hbk Phase 17 D-10 — AlgorithmType whitelist 가드 (Tampering mitigate T-17-02-01)
                if (!string.IsNullOrEmpty(newValue)
                    && string.Equals(newValue, datum.AlgorithmType, System.StringComparison.Ordinal)
                    && (newValue == "TwoLineIntersect" || newValue == "CircleTwoHorizontal" || newValue == "VerticalTwoHorizontal")) {
                    // Step 1: force rebind — PropertyDescriptor 재계산 + ICustomTypeDescriptor 신규 필터 적용 (Phase 16 D-09/D-10 패턴 재사용)
                    if (ParamEditor != null) {
                        _isRebinding = true; //260503 hbk Phase 17 bugfix — rebind 중 재진입 차단
                        try {
                            ParamEditor.SelectedObject = null;
                            ParamEditor.SelectedObject = datum;
                        } finally {
                            _isRebinding = false;
                        }
                    }
                    // Step 2: 검출 상태 reset (Pattern S5 — LastTeachSucceeded/LastFindSucceeded 가드 false → RenderDatumOverlay 검출 도형 자동 미렌더)
                    datum.LastTeachSucceeded = false; //260503 hbk Phase 17 D-10
                    datum.LastFindSucceeded  = false; //260503 hbk Phase 17 D-10
                    //260503 hbk Phase 17 D-13 — Step 3: DetectedOrigin 시각화 clear (RenderDatumOverlay 가 LastFindSucceeded=false 분기에서 자동 미렌더, 본 라인은 PropertyGrid 메트릭 0 표시 안전 처리)
                    datum.DetectedOriginRow = 0; //260503 hbk Phase 17 D-13
                    datum.DetectedOriginCol = 0; //260503 hbk Phase 17 D-13
                    datum.DetectedRefAngle  = 0; //260503 hbk Phase 17 D-13
                    datum.DetectedEdgeCount = 0; //260503 hbk Phase 17 D-16
                    datum.DetectedFitRMSE   = 0; //260503 hbk Phase 17 D-16
                    datum.DetectedAngleDeg  = 0; //260503 hbk Phase 17 D-16
                    // Step 4: ROI 보존 — 명시적 액션 없음 (DatumConfig ROI 필드 미수정)
                    // Step 5: 자동 재검출 없음 — Phase 16 D-13/D-14 보존 (TryTriggerDatumAutoReteach 호출 없음)
                    // Step 6: 캔버스 시각화 갱신 (RenderDatumOverlay 가 LastTeachSucceeded=false 분기에서 검출 도형 미렌더)
                    if (mParentWindow != null && mParentWindow.mainView != null && mParentWindow.mainView.halconViewer != null) {
                        mParentWindow.mainView.halconViewer.SetDatumOverlay(datum, true);
                    }
                    return; //260503 hbk Phase 17 D-10 — AlgorithmType 변경은 전용 흐름 — TryTriggerDatumAutoReteach 라우팅 안 함 (D-13/D-14 보존)
                }
            }

            // 기타 ComboBox (EdgeDirection / EdgePolarity / EdgeSelection / RadialDirection 등): 기존 경로
            TryTriggerDatumAutoReteach();
        }

        private bool _isControlLoaded = false; //260408 hbk UI 초기화 완료 플래그
        private string _pendingRecipeName = null; //260408 hbk 초기화 전 수신된 레시피명

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

                _isControlLoaded = true; //260408 hbk

                //260408 hbk 초기화 전에 OnLoadRecipe가 먼저 호출된 경우 여기서 트리 재구축
                //260509 hbk Phase 20 — null 병합 연산자 → 명시적 if/else (D-01, P-3)
                string recipeName = SystemHandler.Handle.Setting.CurrentRecipeName;
                if (_pendingRecipeName != null) recipeName = _pendingRecipeName;
                if (!string.IsNullOrEmpty(recipeName)) {
                    ViewModel.CurrentRecipe = recipeName;
                    ViewModel.RebuildTree();
                }

                ViewModel.RootModel.ExpandAll();

                //260426 hbk Phase 13-06 — UAT Test 6 (minor) gap closure: PropertyGrid 파라미터 변경 → 자동 재티칭 트리거
                //  ParamEditor 가 null 이면 (Loaded 이전) skip. 정상 경로에서는 InitializeComponent 가 이미 ParamEditor 를 생성.
                if (ParamEditor != null) {
                    ParamEditor.AddHandler(TextBoxBase.LostFocusEvent, new RoutedEventHandler(OnParamEditorLostFocus), true);
                    ParamEditor.AddHandler(Selector.SelectionChangedEvent, new SelectionChangedEventHandler(OnParamEditorSelectionChanged), true);
                }
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
            //260408 hbk UI 초기화 전이면 보류, ListView_Loaded에서 처리
            if (!_isControlLoaded) {
                _pendingRecipeName = name;
                return;
            }

            ViewModel.CurrentRecipe = name;
            ViewModel.RebuildTree();
            //260509 hbk Phase 20 — null-conditional → 명시적 if/else (D-01, P-14)
            if (ViewModel.RootModel != null) ViewModel.RootModel.ExpandAll();
        }

        //260417 hbk Phase 6-04 UAT: Sequence/Shot/Action 노드 모두 Start 가능 + Shot→Action 지연 동기화
        private void Btn_start_Click(object sender, RoutedEventArgs e) {
            if (treeListBox_sequence.SelectedIndex < 0) return;
            if (!(treeListBox_sequence.SelectedItem is NodeViewModel node)) return;

            // Sequence/Shot/Action 외 노드는 실행 불가
            if (node.NodeType != ENodeType.Sequence && node.NodeType != ENodeType.Action) {
                CustomMessageBox.Show("Error",
                    "Select a Sequence or Shot/Action node to run.",
                    MessageBoxImage.Error);
                return;
            }

            ESequence seqID = node.SequenceID;
            SequenceBase seq = SystemHandler.Handle.Sequences[seqID];
            if (seq == null) {
                CustomMessageBox.Show("Error", "Sequence not found.", MessageBoxImage.Error);
                return;
            }

            //260417 hbk Phase 6-04 UAT: 이미 실행 중일 때 재실행 차단
            if (!SystemHandler.Handle.Sequences.IsIdle) {
                CustomMessageBox.Show("Error", "Sequence is already running.", MessageBoxImage.Error);
                return;
            }

            if (!ResolveRunnableAction(node, seq, out EAction actID)) {
                CustomMessageBox.Show("Error",
                    "There is no action to run.\nSelect a Sequence or Shot node that has a registered action.",
                    MessageBoxImage.Error);
                return;
            }

            //260417 hbk Phase 6-04 UAT: Start 실패 시 침묵 방지 — 실패 원인 진단
            bool started = SystemHandler.Handle.Sequences.Start(seqID, actID);
            if (!started) {
                string diag = string.Format(
                    "Sequence '{0}' failed to start.\n\nAction ID: {1}\nActionCount: {2}\nState: {3}\n\n" +
                    "가능한 원인:\n- Action ID가 등록된 Action 목록에 없음 (GetIndexOf=-1)\n- Sequence가 이미 Idle이 아닌 상태\n- Actions 배열 비어있음",
                    seq.Name, actID, seq.ActionCount, seq.State);
                CustomMessageBox.Show("Start Failed", diag, MessageBoxImage.Error);
                Debug.WriteLine(string.Format("Btn_start_Click: Start failed seq={0} actID={1} count={2} state={3}",
                    seq.Name, actID, seq.ActionCount, seq.State));
            }
        }

        //260417 hbk Phase 6-04 UAT: Sequence/Shot/Action 노드 → 실행 Action ID 해석 (+ 지연 동기화)
        private bool ResolveRunnableAction(NodeViewModel node, SequenceBase seq, out EAction actID) {
            actID = EAction.Unknown;
            if (seq == null) return false;

            // Shot 노드 (Action 타입 + ShotConfig Param): RecipeManager.Shots 인덱스 → seq[i].ID 매핑
            if (node.NodeType == ENodeType.Action && node.Param is ShotConfig shotCfg) {
                var seqHandler = SystemHandler.Handle.Sequences;
                int shotIdx = seqHandler.RecipeManager.Shots.IndexOf(shotCfg);
                if (shotIdx < 0) return false;

                if (shotIdx < seq.ActionCount) {
                    // RebuildInspectionActions 가 호출된 정상 상태: seq[shotIdx] 가 이 Shot의 Action
                    actID = seq[shotIdx].ID;
                    return true;
                }

                // 매핑 실패 (UI에서 Shot 추가 후 RebuildInspectionActions 미호출) → 지연 동기화
                if (seqHandler.IsIdle) {
                    seqHandler.EnableDynamicFAIMode();
                    seqHandler.RebuildInspectionActions(seq.ID);
                    if (shotIdx < seq.ActionCount) {
                        actID = seq[shotIdx].ID;
                        return true;
                    }
                }
                return false;
            }

            // Action 노드 (일반 Action: ShotConfig 아님) — 기존 경로
            if (node.NodeType == ENodeType.Action) {
                if (seq.ActionCount == 0) return false;
                if (node.ActionID != EAction.Unknown) {
                    actID = node.ActionID;
                    return true;
                }
                actID = seq[0].ID;
                return true;
            }

            // Sequence 노드 → 첫 Action 실행
            if (node.NodeType == ENodeType.Sequence) {
                if (seq.ActionCount == 0) return false;
                actID = seq[0].ID;
                return true;
            }

            return false;
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
            button_loadImage.IsEnabled = false;  //260423 hbk Datum 노드 지원: 선택 변경 시 초기화
            //button_showConfig.IsEnabled = false;

            //260423 hbk Phase 11 D-15 — 선택 해제 시 Circle ROI 비활성 (기본값)
            //260424 hbk Phase 12 D-01 — 선택 해제 시 btn_teachDatum 비활성 (Datum 분기에서만 true)
            if (mParentWindow != null && mParentWindow.mainView != null) {
                mParentWindow.mainView.btn_circleRoi.IsEnabled = false;
                mParentWindow.mainView.btn_teachDatum.IsEnabled = false; //260424 hbk Phase 12
            }

            //260511 hbk CO-22-01 — sender 기준 게이트 (e.Source 비대칭 회피)
            //  기존 `e.Source is TreeListBox` 게이트는 TreeListBox 내부 (계층형 inner item / EditableTextBlock 등) 가 발생시킨
            //  Selector.SelectionChanged routed event bubble 시 e.Source 가 inner element 가 되어 게이트 실패 → 함수 전체 skip.
            //  Phase 16 D-09 force rebind 가 ParamEditor.SelectedObject 를 code-behind 로 설정해 XAML 바인딩(L257)을 클리어한 이후,
            //  Datum → FAI 전환에서 본 핸들러가 skip 되면 XAML 바인딩도 force rebind 도 모두 안 돌아 PropertyGrid 가 직전 Datum 으로 stale.
            //  sender 는 항상 XAML 에 핸들러를 등록한 본인 컨트롤 (treeListBox_sequence) → 비대칭 회피.
            //  Phase 17 D-10 OnParamEditorSelectionChanged 의 e.OriginalSource 사용과 비대칭 (의도된 차이) 이었으나, 트리 핸들러는 sender 기준이 정합.
            if (!(sender is TreeListBox list)) return; //260511 hbk CO-22-01
            if (!ReferenceEquals(sender, treeListBox_sequence)) return; //260511 hbk CO-22-01 — 동일 핸들러가 다른 트리에 attach 됐을 때 무관 호출 차단
            {
                if(list.SelectedItem is NodeViewModel) {
                    NodeViewModel item = list.SelectedItem as NodeViewModel;
                    object itemParam = item.Param;

                    //260410 hbk Phase 4 gap fix: clear Datum overlay on any node change
                    mParentWindow.mainView.halconViewer.ClearDatumOverlay();
                    //260426 hbk Phase 13 D-A1 — Datum 후보도 매 selection 마다 우선 clear (Datum 분기에서만 다시 publish)
                    mParentWindow.mainView.halconViewer.ClearDatumRoiCandidates();

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
                    //260409 hbk Phase 4: Datum node selection -> PropertyGrid binding (D-10)
                    if (item.NodeType == ENodeType.Datum) {
                        //260417 hbk Phase 6 Plan 04: Datum CRUD 활성화 (D-25)
                        button_addFAI.IsEnabled = true;
                        button_removeFAI.IsEnabled = true;
                        button_renameFAI.IsEnabled = true;
                        //260423 hbk Datum 노드: Grab/LoadImage 활성화 (DatumConfig → ResolveDatumCameraParam 위임)
                        button_grab.IsEnabled = true;
                        button_loadImage.IsEnabled = true;
                        // PropertyGrid already handled by SetParam above (DatumConfig : ParamBase)
                        if (_inspectionVm != null) _inspectionVm.ClearResults(); //260509 hbk Phase 20
                        //260410 hbk Phase 4 gap fix: show Datum overlay on canvas when Datum node selected
                        if (itemParam is DatumConfig datumCfg) {
                            mParentWindow.mainView.halconViewer.SetDatumOverlay(datumCfg, true);
                            //260426 hbk Phase 13 D-A1 — 이미 티칭된 Datum 도 selection 즉시 ROI hit-test 가능하도록 후보 publish
                            mParentWindow.mainView.PublishDatumRoiCandidates(datumCfg);

                            //260429 hbk Phase 16 D-09/D-10 — Datum 전환 시 PropertyGrid SelectedObject 강제 null→new force rebind.
                            //  이유: Phase 15 UAT Test 10~12 결함 — ROI 이동/생성 후 Datum 전환 시 AlgorithmType combobox 가 stale.
                            //  RaisePropertyChanged + RefreshParamEditor 만으로는 AlgorithmType (string property) combobox 까지 안 닿음.
                            //  SetParam(line 328) 이 SelectedParam 만 갱신하고 ParamEditor.SelectedObject 는 다른 경로(외부 binding)로 갱신될 때
                            //  combobox 가 이전 reference 의 stale 값을 유지함 → null 할당으로 binding 강제 해제 후 새 인스턴스 할당.
                            //260429 hbk Phase 16 D-09 — _editingDatum 등가 reference (ParamEditor.SelectedObject) 강제 재할당
                            //260429 hbk Phase 16 D-11/D-12 — 매 Datum 클릭마다 force rebind (편집 모드 무관) + AlgorithmType 변경 자체는 자동 재티칭 추가 안 함 (D-13 일관)
                            if (ParamEditor != null) {
                                //260429 hbk Phase 16 D-10 — null 할당으로 PropertyGrid 의 기존 binding 강제 해제 후, 새 인스턴스 할당
                                _isRebinding = true; //260503 hbk Phase 17 bugfix — datum 클릭 rebind 중 SelectionChanged 무한루프 차단
                                try {
                                    ParamEditor.SelectedObject = null;
                                    ParamEditor.SelectedObject = datumCfg;
                                } finally {
                                    _isRebinding = false;
                                }
                            }
                        }
                        //260424 hbk Phase 12 D-01 — Datum 노드 선택 시 btn_teachDatum 활성화
                        if (mParentWindow != null && mParentWindow.mainView != null) {
                            mParentWindow.mainView.btn_teachDatum.IsEnabled = true;
                        }
                    }
                    //260417 hbk Phase 6 Plan 04: Measurement node selection (D-24)
                    else if (item.NodeType == ENodeType.Measurement) {
                        button_addFAI.IsEnabled = true;
                        button_removeFAI.IsEnabled = true;
                        button_renameFAI.IsEnabled = true;
                        //260517 hbk Phase 23.1 D-01 — EdgeToLineDistanceMeasurement 노드 선택 시 Rect ROI 버튼 활성화 (다른 measurement 타입은 비활성)
                        bool isEdgeToLine = item.Param is EdgeToLineDistanceMeasurement;
                        if (mParentWindow != null && mParentWindow.mainView != null)
                            mParentWindow.mainView.btn_rectRoi.IsEnabled = isEdgeToLine; //260517 hbk Phase 23.1 D-01
                        // PropertyGrid handled by SetParam (MeasurementBase : ParamBase)
                        //260508 hbk Phase 19 fix — Datum 클릭 force rebind(line 419-420) 후 binding 손상 → Measurement 전환 시 PropertyGrid stale.
                        //  Datum 패턴(Phase 16 D-09)과 동일하게 null→new 강제 재할당.
                        if (ParamEditor != null && itemParam != null) { //260508 hbk Phase 19 fix
                            _isRebinding = true; //260508 hbk Phase 19 fix
                            try {
                                ParamEditor.SelectedObject = null; //260508 hbk Phase 19 fix
                                ParamEditor.SelectedObject = itemParam; //260508 hbk Phase 19 fix
                            } finally {
                                _isRebinding = false; //260508 hbk Phase 19 fix
                            }
                        }
                        if (_inspectionVm != null) _inspectionVm.ClearResults(); //260509 hbk Phase 20
                    }
                    else if (item.NodeType == ENodeType.FAI) {
                        button_addFAI.IsEnabled = true;
                        button_removeFAI.IsEnabled = true;
                        button_renameFAI.IsEnabled = true;
                        if (_inspectionVm != null && itemParam is FAIConfig faiConfig) {
                            _inspectionVm.OnFAISelected(faiConfig);
                            mParentWindow.mainView.DisplayFAIImage(faiConfig);
                            //260508 hbk Phase 19 fix — ICustomTypeDescriptor 추가 후 PropertyGrid binding stale 방지 (Phase 16 D-09 패턴 적용)
                            //  Datum 클릭 force rebind 가 SelectedObject binding 을 끊어 → FAI 전환 시 자동 갱신 안 됨 → 명시적 재할당 필요.
                            if (ParamEditor != null) { //260508 hbk Phase 19 fix
                                _isRebinding = true; //260508 hbk Phase 19 fix
                                try {
                                    ParamEditor.SelectedObject = null; //260508 hbk Phase 19 fix
                                    ParamEditor.SelectedObject = faiConfig; //260508 hbk Phase 19 fix
                                } finally {
                                    _isRebinding = false; //260508 hbk Phase 19 fix
                                }
                            }
                        }
                    }
                    else if (item.NodeType == ENodeType.Action) {
                        button_addFAI.IsEnabled = true;
                        button_removeFAI.IsEnabled = (item.Param is ShotConfig); //260408 hbk Shot 노드면 삭제 가능
                        button_renameFAI.IsEnabled = false;
                        if (_inspectionVm != null) {
                            _inspectionVm.OnActionSelected(item);
                        }
                        //260511 hbk CO-22-01 — Action(ShotConfig) 분기 force rebind 추가.
                        //  Phase 16 D-09 Datum force rebind 가 XAML SelectedObject 바인딩(L257)을 끊은 이후
                        //  Action 노드 클릭 시 PropertyGrid 가 stale (직전 Datum/FAI 유지). Phase 19 fix 와 동일 패턴 적용.
                        if (ParamEditor != null && itemParam is ParamBase) { //260511 hbk CO-22-01
                            _isRebinding = true; //260511 hbk CO-22-01
                            try {
                                ParamEditor.SelectedObject = null; //260511 hbk CO-22-01
                                ParamEditor.SelectedObject = itemParam; //260511 hbk CO-22-01
                            } finally {
                                _isRebinding = false; //260511 hbk CO-22-01
                            }
                        }
                    }
                    else if (item.NodeType == ENodeType.Sequence) {
                        button_addFAI.IsEnabled = true;
                        button_removeFAI.IsEnabled = false;
                        button_renameFAI.IsEnabled = false;
                        if (_inspectionVm != null) _inspectionVm.ClearResults(); //260509 hbk Phase 20
                        //260511 hbk CO-22-01 — Sequence 분기 force rebind (param=null 경로 포함).
                        //  Sequence 노드는 통상 ParamBase 가 없으므로 명시적 null 할당으로 PropertyGrid 비움.
                        if (ParamEditor != null) { //260511 hbk CO-22-01
                            _isRebinding = true; //260511 hbk CO-22-01
                            try {
                                ParamEditor.SelectedObject = null; //260511 hbk CO-22-01
                                if (itemParam is ParamBase) ParamEditor.SelectedObject = itemParam; //260511 hbk CO-22-01
                            } finally {
                                _isRebinding = false; //260511 hbk CO-22-01
                            }
                        }
                    }
                    else {
                        button_addFAI.IsEnabled = false;
                        button_removeFAI.IsEnabled = false;
                        button_renameFAI.IsEnabled = false;
                        if (_inspectionVm != null) _inspectionVm.ClearResults(); //260509 hbk Phase 20
                        //260511 hbk CO-22-01 — else 분기 (unknown NodeType) 도 동일 패턴 — PropertyGrid 명시적 비움.
                        if (ParamEditor != null) { //260511 hbk CO-22-01
                            _isRebinding = true; //260511 hbk CO-22-01
                            try {
                                ParamEditor.SelectedObject = null; //260511 hbk CO-22-01
                                if (itemParam is ParamBase) ParamEditor.SelectedObject = itemParam; //260511 hbk CO-22-01
                            } finally {
                                _isRebinding = false; //260511 hbk CO-22-01
                            }
                        }
                    }

                    //260423 hbk Phase 11 D-15/D-18 — Circle ROI 버튼 활성화 게이팅 (선택 노드 기반)
                    bool circleEnabled = false;
                    if (itemParam is FAIConfig faiForCircle) {
                        foreach (var m in faiForCircle.Measurements) {
                            if (m is CircleDiameterMeasurement) { circleEnabled = true; break; }
                        }
                    }
                    else if (itemParam is CircleDiameterMeasurement) {
                        circleEnabled = true;
                    }
                    if (mParentWindow != null && mParentWindow.mainView != null) {
                        mParentWindow.mainView.btn_circleRoi.IsEnabled = circleEnabled;
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
            //260423 hbk Datum 노드: ICameraParam 미구현이므로 Shot 위임
            if (SelectedParam is DatumConfig datumForGrab) {
                ICameraParam resolved = ResolveDatumCameraParam(datumForGrab);
                if (resolved == null) return;
                if (SystemHandler.Handle.Sequences.IsIdle == false) return;
                mParentWindow.mainView.GrabAndDisplay(resolved);
                return;
            }
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
            //260423 hbk Datum 노드: ICameraParam 미구현이므로 Shot 위임
            if (SelectedParam is DatumConfig datumForLoad) {
                ICameraParam resolved = ResolveDatumCameraParam(datumForLoad);
                if (resolved == null) return;
                //260518 hbk #3 — 표시는 Shot 으로 위임, 경로 저장은 DatumConfig.TeachingImagePath 로
                mParentWindow.mainView.LoadAndDisplay(resolved, datumForLoad);
                return;
            }
            if (!(SelectedParam is ICameraParam)) return;

            ICameraParam camParam = SelectedParam as ICameraParam;
            mParentWindow.mainView.LoadAndDisplay(camParam);
        }

        //260423 hbk Datum 노드: SourceShotName으로 ShotConfig 조회, 없으면 Shots[0] fallback
        private ICameraParam ResolveDatumCameraParam(DatumConfig datum) {
            var shots = SystemHandler.Handle.Sequences.RecipeManager.Shots;
            if (shots.Count == 0) return null;
            if (!string.IsNullOrEmpty(datum.SourceShotName)) {
                ShotConfig matched = shots.FirstOrDefault(s => s.ShotName == datum.SourceShotName);
                if (matched != null) return matched;
            }
            return shots[0];
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

                //260417 hbk Phase 6 Plan 04: Datum 노드 선택 시 Datum 형제 추가 (D-25)
                if (selectedNode.NodeType == ENodeType.Datum) {
                    NodeViewModel seqNodeForDatum = selectedNode.Parent;
                    if (seqNodeForDatum != null && seqNodeForDatum.NodeType == ENodeType.Sequence) {
                        AddDatumToSequence(seqNodeForDatum);
                    }
                    return;
                }

                //260417 hbk Phase 6 Plan 04: Measurement 노드 선택 시 Measurement 형제 추가 (D-24)
                if (selectedNode.NodeType == ENodeType.Measurement) {
                    NodeViewModel faiNodeForMeas = selectedNode.Parent;
                    if (faiNodeForMeas != null && faiNodeForMeas.NodeType == ENodeType.FAI && faiNodeForMeas.Param is FAIConfig faiOwner) {
                        AddMeasurementToFAI(faiNodeForMeas, faiOwner);
                    }
                    return;
                }

                // Sequence 노드 선택 시: Shot 또는 Datum 추가 선택 (D-25)
                if (selectedNode.NodeType == ENodeType.Sequence) {
                    //260417 hbk Phase 6 Plan 04: Yes=Shot, No=Datum
                    MessageBoxResult choice = CustomMessageBox.ShowConfirmation(
                        "추가 항목 선택",
                        "Shot을 추가하려면 Yes, Datum을 추가하려면 No를 누르세요.",
                        MessageBoxButton.YesNoCancel);
                    if (choice == MessageBoxResult.Yes) {
                        AddShotToSequence(selectedNode);
                    }
                    else if (choice == MessageBoxResult.No) {
                        AddDatumToSequence(selectedNode);
                    }
                    return;
                }

                // Action 노드에서 ShotConfig가 아닌 경우: Shot 생성으로 전환
                if (selectedNode.NodeType == ENodeType.Action && !(selectedNode.Param is ShotConfig)) {
                    NodeViewModel seqNode = selectedNode.Parent;
                    if (seqNode != null && seqNode.NodeType == ENodeType.Sequence) {
                        AddShotToSequence(seqNode);
                    }
                    return;
                }

                //260417 hbk Phase 6 Plan 04: FAI 노드 선택 시 Measurement 추가 (D-24)
                if (selectedNode.NodeType == ENodeType.FAI && selectedNode.Param is FAIConfig faiSel) {
                    AddMeasurementToFAI(selectedNode, faiSel);
                    return;
                }

                // Action(ShotConfig) 노드: FAI 추가
                NodeViewModel actionNode = selectedNode;
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

        //260417 hbk Phase 6 Plan 04: Sequence에 Datum을 추가하고 트리에 노드 직접 삽입 (D-25)
        private void AddDatumToSequence(NodeViewModel seqNode) {
            if (!(seqNode.Param is CameraMasterParam)) {
                // Param 검사 — InspectionSequence가 아니면 무시
            }
            SequenceBase seq = SystemHandler.Handle.Sequences[seqNode.SequenceID];
            if (!(seq is InspectionSequence inspSeq)) return;

            string defaultName = "Datum_" + (inspSeq.DatumConfigs.Count + 1);
            if (!TextInputBox.Show("Datum 이름 입력", defaultName, out string datumName)) return;

            DatumConfig newDatum = inspSeq.AddDatum(datumName);
            if (newDatum == null) return;

            ViewModel.AddDatumNode(seqNode, newDatum);
            seqNode.IsExpanded = true;
        }

        //260417 hbk Phase 6 Plan 04: FAI에 Measurement를 추가하고 트리에 노드 직접 삽입 (D-24)
        private void AddMeasurementToFAI(NodeViewModel faiNode, FAIConfig fai) {
            string[] typeNames = MeasurementFactory.GetTypeNames();
            //260509 hbk Phase 20 — 삼항 → 명시적 if/else (D-01, P-4)
            string defaultType = "EdgePairDistance";
            if (typeNames.Length > 0) defaultType = typeNames[0];

            //260508 hbk Quick — TextInputBox 자유 텍스트 → ComboInputBox 콤보 강제 (사용자 입력 실수 방지, MeasurementFactory 단일 소스)
            if (!ComboInputBox.Show("Measurement 타입 선택", typeNames, defaultType, out string typeName)) return;

            MeasurementBase newMeas = fai.AddMeasurement(typeName);
            if (newMeas == null) {
                CustomMessageBox.Show("오류", "유효하지 않은 Measurement 타입: " + typeName, System.Windows.MessageBoxImage.Error);
                return;
            }
            string defaultMeasName = typeName + "_" + fai.Measurements.Count;
            if (TextInputBox.Show("Measurement 이름 입력", defaultMeasName, out string measName)) {
                newMeas.MeasurementName = measName;
            }

            ViewModel.AddMeasurementNode(faiNode, newMeas);
            faiNode.IsExpanded = true;
        }

        /// <summary>Sequence에 새 Shot(+기본FAI)을 추가하고 트리에 노드를 직접 삽입한다.
        /// 시퀀스 Action 교체 없이 데이터만 추가하여 런타임 안전성 확보.</summary>
        private void AddShotToSequence(NodeViewModel seqNode) {
            var seqHandler = SystemHandler.Handle.Sequences;
            string defaultName = "SHOT_" + seqHandler.RecipeManager.ShotCount;
            if (!TextInputBox.Show("Shot 이름 입력", defaultName, out string shotName)) return;

            // 데이터만 추가 (시퀀스 Action 교체 안 함 — 런타임 안전)
            ShotConfig shot = seqHandler.RecipeManager.AddShot(shotName);
            FAIConfig fai = shot.AddFAI("FAI_0");
            seqHandler.EnableDynamicFAIMode(); //260408 hbk 저장 시 SHOTS 포맷으로 기록되도록 활성화

            // 트리에 Shot 노드(Action 타입) + FAI 자식 노드 직접 삽입
            var shotNode = new CompositeNode {
                Name = shot.ShotName,
                NodeType = ENodeType.Action,
                ParamData = shot,
                SequenceName = seqNode.SequenceName,
                SequenceID = seqNode.SequenceID,
                ActionID = EAction.Unknown
            };
            var faiChildNode = new CompositeNode {
                Name = fai.FAIName,
                NodeType = ENodeType.FAI,
                ParamData = fai,
                SequenceName = seqNode.SequenceName,
                SequenceID = seqNode.SequenceID,
                ActionID = EAction.Unknown
            };
            //260413 hbk Phase 6: Datum 노드 제거 — Datum은 Fixture(Sequence) 레벨로 이전 (D-25).
            // TODO: Phase 6 Plan 04에서 Sequence 자식으로 Datum 노드 추가.
            //260417 hbk Phase 6-04 UAT 후속: 여기서 RebuildInspectionActions를 호출하지 않아도,
            // 실행 시 Btn_start_Click → ResolveRunnableAction 이 지연 동기화로 Shot→Action 매핑을 복구한다.
            shotNode.Children.Add(faiChildNode);

            var shotVm = new NodeViewModel(shotNode, seqNode);
            seqNode.Children.Add(shotVm);
            seqNode.IsExpanded = true;
        }

        private void Btn_RemoveFAI_Click(object sender, RoutedEventArgs e) {
            try {
                if (!(treeListBox_sequence.SelectedItem is NodeViewModel selectedNode)) return;

                //260417 hbk Phase 6 Plan 04: Datum 노드 삭제 (D-25)
                if (selectedNode.NodeType == ENodeType.Datum && selectedNode.Param is DatumConfig datumToRemove) {
                    SequenceBase seq = SystemHandler.Handle.Sequences[selectedNode.SequenceID];
                    if (!(seq is InspectionSequence inspSeq)) return;
                    int datumIdx = inspSeq.DatumConfigs.IndexOf(datumToRemove);
                    if (datumIdx < 0) return;

                    MessageBoxResult dr = CustomMessageBox.ShowConfirmation(
                        "Datum 삭제",
                        string.Format("Datum \"{0}\"을(를) 삭제합니다. 계속하시겠습니까?", datumToRemove.DatumName),
                        MessageBoxButton.YesNo);
                    if (dr != MessageBoxResult.Yes) return;

                    if (inspSeq.RemoveDatum(datumIdx)) {
                        selectedNode.Detach();
                    }
                    if (_inspectionVm != null) _inspectionVm.ClearResults(); //260509 hbk Phase 20
                    return;
                }

                //260417 hbk Phase 6 Plan 04: Measurement 노드 삭제 (D-24)
                if (selectedNode.NodeType == ENodeType.Measurement && selectedNode.Param is MeasurementBase measToRemove) {
                    NodeViewModel faiParent = selectedNode.Parent;
                    if (faiParent == null || !(faiParent.Param is FAIConfig faiOwner)) return;
                    int measIdx = faiOwner.Measurements.IndexOf(measToRemove);
                    if (measIdx < 0) return;

                    //260509 hbk Phase 20 — 삼항 → 명시적 if/else (D-01, P-4)
                    string measDisplayName = measToRemove.MeasurementName;
                    if (string.IsNullOrEmpty(measDisplayName)) measDisplayName = measToRemove.TypeName;
                    MessageBoxResult mr = CustomMessageBox.ShowConfirmation(
                        "Measurement 삭제",
                        string.Format("Measurement \"{0}\"을(를) 삭제합니다. 계속하시겠습니까?", measDisplayName),
                        MessageBoxButton.YesNo);
                    if (mr != MessageBoxResult.Yes) return;

                    if (faiOwner.RemoveMeasurement(measIdx)) {
                        selectedNode.Detach();
                    }
                    if (_inspectionVm != null) _inspectionVm.ClearResults(); //260509 hbk Phase 20
                    return;
                }

                //260408 hbk Shot(Action) 노드 선택 시 Shot 삭제
                if (selectedNode.NodeType == ENodeType.Action && selectedNode.Param is ShotConfig shotToRemove) {
                    var seqHandler = SystemHandler.Handle.Sequences;
                    int shotIndex = seqHandler.RecipeManager.Shots.IndexOf(shotToRemove);
                    if (shotIndex < 0) return;

                    MessageBoxResult shotResult = CustomMessageBox.ShowConfirmation(
                        "Shot 삭제",
                        string.Format("Shot \"{0}\"과 하위 FAI를 모두 삭제합니다. 계속하시겠습니까?", shotToRemove.ShotName),
                        MessageBoxButton.YesNo);
                    if (shotResult != MessageBoxResult.Yes) return;

                    seqHandler.RecipeManager.RemoveShot(shotIndex);
                    selectedNode.Detach();
                    if (_inspectionVm != null) _inspectionVm.ClearResults(); //260509 hbk Phase 20
                    return;
                }

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
                if (_inspectionVm != null) _inspectionVm.ClearResults(); //260509 hbk Phase 20
            }
            catch (Exception ex) {
                CustomMessageBox.Show("삭제 오류", ex.Message, System.Windows.MessageBoxImage.Error);
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
