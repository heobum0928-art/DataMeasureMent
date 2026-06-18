
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
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
        // SelectedObject 재할당 중 ComboBox 초기화 이벤트가 bubble되어 SelectionChanged 무한루프가 발생하는 것을 차단
        private bool _isRebinding = false;

        //260616 hbk Phase 51 BATCH-01: 일괄 검사 누적 결과 + 서비스 인스턴스 (UI 소유, static 금지)
        private List<CycleResultDto> _batchAccumulated = new List<CycleResultDto>();
        private BatchRunService _batchService;
        //260617 hbk Quick 260617-cq2: 일괄 검사 완료 시 결과 그리드에 펼쳐 표시할 체크 SHOT 목록
        private List<ShotConfig> _batchShots;

        // 자동 속성(set;/get;)은 INotifyPropertyChanged 미발동 → SelectedObject null 후 재할당으로 강제 재렌더
        public void RefreshParamEditor() {
            if (ParamEditor == null) return;
            var current = SelectedParam;
            _isRebinding = true;
            try {
                ParamEditor.SelectedObject = null;
                ParamEditor.SelectedObject = current;
            } finally {
                _isRebinding = false;
            }
        }

        private List<DatumConfig> ResolveSequenceDatums(ESequence seqId) {
            var result = new List<DatumConfig>();
            try {
                var seq = SystemHandler.Handle.Sequences[seqId] as InspectionSequence;
                if (seq == null) return result;
                foreach (DatumConfig d in seq.DatumConfigs) {
                    if (d != null) result.Add(d);
                }
            } catch {
                // 해석 실패 무시 — 빈 리스트 반환 (datum 미표시, 회귀 0)
            }
            return result;
        }

        private List<DatumConfig> ResolveDatumsForMeasurement(ESequence seqId, MeasurementBase meas) {
            var result = new List<DatumConfig>();
            if (meas == null || string.IsNullOrEmpty(meas.DatumRef)) return result;
            foreach (DatumConfig d in ResolveSequenceDatums(seqId)) {
                if (d != null && string.Equals(d.DatumName, meas.DatumRef, StringComparison.Ordinal)) {
                    result.Add(d);
                    break; // DatumName은 시퀀스 내 고유 — 첫 매칭만
                }
            }
            return result;
        }

        private List<DatumConfig> ResolveDatumsForFai(ESequence seqId, FAIConfig fai) {
            var result = new List<DatumConfig>();
            if (fai == null) return result;
            List<DatumConfig> seqDatums = ResolveSequenceDatums(seqId);
            if (seqDatums.Count == 0) return result;
            foreach (MeasurementBase meas in fai.Measurements) {
                if (meas == null || string.IsNullOrEmpty(meas.DatumRef)) continue;
                foreach (DatumConfig d in seqDatums) {
                    if (d != null && string.Equals(d.DatumName, meas.DatumRef, StringComparison.Ordinal)
                        && !result.Contains(d)) {
                        result.Add(d);
                    }
                }
            }
            return result;
        }

        // Shot이 실제 참조하는 datum만으로 좁힌다. 관례상 보통 1개. 무보정(빈 DatumRef) 측정만 있으면 빈 리스트.
        private List<DatumConfig> ResolveDatumsForShot(ESequence seqId, ShotConfig shot) {
            var result = new List<DatumConfig>();
            if (shot == null || shot.FAIList == null) return result;
            List<DatumConfig> seqDatums = ResolveSequenceDatums(seqId);
            if (seqDatums.Count == 0) return result;
            foreach (FAIConfig fai in shot.FAIList) {
                if (fai == null) continue;
                foreach (MeasurementBase meas in fai.Measurements) {
                    if (meas == null || string.IsNullOrEmpty(meas.DatumRef)) continue;
                    foreach (DatumConfig d in seqDatums) {
                        if (d != null && string.Equals(d.DatumName, meas.DatumRef, StringComparison.Ordinal)
                            && !result.Contains(d)) {
                            result.Add(d);
                        }
                    }
                }
            }
            return result;
        }

        // PropertyGrid 파라미터 변경 → MainView 자동 재티칭 라우팅.
        // PropertyGrid 내부 navigation Selector의 SelectionChanged가 bubble되어 binding 전환 중간에 fire될 수 있으므로
        // (a) OriginalSource 필터로 실제 ComboBox/TextBox 셀만 통과, (b) Dispatcher.BeginInvoke(Background)로 binding 안정 후 재검사.
        private void TryTriggerDatumAutoReteach() {
            if (ParamEditor == null) return;
            // binding 전환이 끝난 후 재검사하도록 Background 우선순위로 defer
            Dispatcher.BeginInvoke(new Action(() => {
                var datum = ParamEditor.SelectedObject as DatumConfig;
                if (datum == null) return;
                if (mParentWindow == null) return;
                var mv = mParentWindow.mainView;
                if (mv == null) return;
                mv.NotifyDatumParamMaybeChanged(datum);
            }), DispatcherPriority.Background);
        }

        // OriginalSource가 실제 TextBoxBase(셀)일 때만 통과 (헤더/네비게이션 LostFocus 차단)
        private void OnParamEditorLostFocus(object sender, RoutedEventArgs e) {
            if (!(e.OriginalSource is TextBoxBase)) return;
            TryTriggerDatumAutoReteach();
            TryTriggerMeasurementRoiRefresh();
        }

        // PropertyGrid 측정 파라미터 변경 → MainView ROI 캔버스 재렌더 라우팅.
        // TryTriggerDatumAutoReteach와 동일 패턴 — Background defer로 binding push 완료 후 재검사.
        private void TryTriggerMeasurementRoiRefresh() {
            if (ParamEditor == null) return;
            Dispatcher.BeginInvoke(new Action(() => {
                var meas = ParamEditor.SelectedObject as MeasurementBase;
                if (meas == null) return;
                if (mParentWindow == null) return;
                var mv = mParentWindow.mainView;
                if (mv == null) return;
                mv.HighlightSelectedRoi(meas);
            }), DispatcherPriority.Background);
        }

        // OriginalSource가 실제 ComboBox일 때만 통과 (PropertyGrid 내부 카테고리 ListBox 등 차단).
        // AlgorithmType combobox 변경 감지 + 5-step 리셋 (force rebind + 검출 reset + 시각화 clear + ROI 보존 + 자동 재검출 X)
        private void OnParamEditorSelectionChanged(object sender, SelectionChangedEventArgs e) {
            if (!(e.OriginalSource is ComboBox)) return;
            if (_isRebinding) return;
            // PropertyGrid가 SelectedObject 재할당 후 ComboBox를 재생성 → binding으로 SelectedValue 설정 시
            // RemovedItems.Count == 0 (no prior selection). 사용자 실제 변경은 RemovedItems에 old value 포함.
            // _isRebinding 가드는 동기 경로만 보호 → 비동기 deferred 이벤트는 이 필터로 차단.
            if (e.RemovedItems == null || e.RemovedItems.Count == 0) return;

            DatumConfig datum = null;
            if (ParamEditor != null) datum = ParamEditor.SelectedObject as DatumConfig;
            if (datum != null) {
                var combo = e.OriginalSource as ComboBox;
                string newValue = null;
                if (combo != null) newValue = combo.SelectedValue as string;
                // AlgorithmType whitelist: force rebind이 필요한 타입만 통과
                if (!string.IsNullOrEmpty(newValue)
                    && string.Equals(newValue, datum.AlgorithmType, System.StringComparison.Ordinal)
                    && (newValue == "TwoLineIntersect" || newValue == "CircleTwoHorizontal" || newValue == "VerticalTwoHorizontal" || newValue == "VerticalTwoHorizontalDualImage")) {
                    // Step 1: force rebind — PropertyDescriptor 재계산 + ICustomTypeDescriptor 신규 필터 적용
                    if (ParamEditor != null) {
                        _isRebinding = true;
                        try {
                            ParamEditor.SelectedObject = null;
                            ParamEditor.SelectedObject = datum;
                        } finally {
                            _isRebinding = false;
                        }
                    }
                    // Step 2: 검출 상태 reset — LastTeachSucceeded/LastFindSucceeded false → RenderDatumOverlay 검출 도형 자동 미렌더
                    datum.LastTeachSucceeded = false;
                    datum.LastFindSucceeded  = false;
                    // Step 3: DetectedOrigin 시각화 clear (PropertyGrid 메트릭 0 표시 안전 처리)
                    datum.DetectedOriginRow = 0;
                    datum.DetectedOriginCol = 0;
                    datum.DetectedRefAngle  = 0;
                    datum.DetectedEdgeCount = 0;
                    datum.DetectedFitRMSE   = 0;
                    datum.DetectedAngleDeg  = 0;
                    // Step 4: ROI 보존 — 명시적 액션 없음 (DatumConfig ROI 필드 미수정)
                    // Step 5: 자동 재검출 없음 — Phase 16 D-13/D-14 보존 (TryTriggerDatumAutoReteach 호출 없음)
                    // Step 6: 캔버스 시각화 갱신 (RenderDatumOverlay 가 LastTeachSucceeded=false 분기에서 검출 도형 미렌더)
                    if (mParentWindow != null && mParentWindow.mainView != null && mParentWindow.mainView.halconViewer != null) {
                        mParentWindow.mainView.halconViewer.SetDatumOverlay(datum, true, mParentWindow.mainView.IsDatumTeachActive);
                    }
                    // AlgorithmType 변경 후 swap UI Visibility 갱신 (DualImage ↔ 1-image 전환 양방향).
                    // PublishDatumRoiCandidates 재호출 → isDualImage 재계산 → 토글/배지 Visibility + ROI subset 갱신.
                    if (mParentWindow != null && mParentWindow.mainView != null) {
                        mParentWindow.mainView.PublishDatumRoiCandidates(datum);
                    }
                    return; // AlgorithmType 변경은 전용 흐름 — TryTriggerDatumAutoReteach 라우팅 안 함
                }
            }

            // 기타 ComboBox (EdgeDirection / EdgePolarity / EdgeSelection / RadialDirection 등): 기존 경로
            TryTriggerDatumAutoReteach();
        }

        private bool _isControlLoaded = false;
        private string _pendingRecipeName = null;
        // 기본 Shot 레벨 펼침은 세션 첫 로드 1회만 적용 (이후 사용자 펼침/접힘 상태 유지)
        private bool _initialExpandApplied = false;

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
                    if (ViewModel.RootModel != null) ViewModel.RootModel.ExpandToShotLevel();
                    grid_editor.Visibility = Visibility.Visible;
                    gridSplitter_editor.Visibility = Visibility.Visible;
                    colDefinition_editor.Width = new GridLength(6, GridUnitType.Star);
                }
                else {
                    if (ViewModel.RootModel != null) ViewModel.RootModel.CollapseAll();
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

                _isControlLoaded = true;

                // 초기화 전에 OnLoadRecipe가 먼저 호출된 경우 여기서 트리 재구축
                string recipeName = SystemHandler.Handle.Setting.CurrentRecipeName;
                if (_pendingRecipeName != null) recipeName = _pendingRecipeName;
                if (!string.IsNullOrEmpty(recipeName)) {
                    ViewModel.CurrentRecipe = recipeName;
                    ViewModel.RebuildTree();
                }

                if (!_initialExpandApplied) {
                    ViewModel.RootModel.ExpandToShotLevel();
                    _initialExpandApplied = true;
                }

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
            // UI 초기화 전이면 보류, ListView_Loaded에서 처리
            if (!_isControlLoaded) {
                _pendingRecipeName = name;
                return;
            }

            ViewModel.CurrentRecipe = name;
            ViewModel.RebuildTree();
            // 앱 시작 직후 OnLoadRecipe가 ListView_Loaded보다 먼저 도달하는 경우에는 첫 로드로 간주해 1회 적용
            if (!_initialExpandApplied && ViewModel.RootModel != null) {
                ViewModel.RootModel.ExpandToShotLevel();
                _initialExpandApplied = true;
            }
        }

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

        private bool ResolveRunnableAction(NodeViewModel node, SequenceBase seq, out EAction actID) {
            actID = EAction.Unknown;
            if (seq == null) return false;

            // Shot 노드 (Action 타입 + ShotConfig Param): RecipeManager.Shots 인덱스 → seq[i].ID 매핑
            if (node.NodeType == ENodeType.Action && node.Param is ShotConfig shotCfg) {
                var seqHandler = SystemHandler.Handle.Sequences;
                // 글로벌 IndexOf 대신 시퀀스 소유 Shot만 필터링한 로컬 인덱스 사용.
                // RecipeManager.Shots.IndexOf는 Bottom Shot의 글로벌 인덱스가 Bottom seq.ActionCount를 초과하는 오류를 낸다.
                // RebuildInspectionActions의 actionIdx 부여 로직(SequenceHandler.cs L92-103)과 1:1 대응되어야 함.
                int shotIdx = ComputeLocalShotIndex(seqHandler.RecipeManager, shotCfg, seq.ID);
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

        // 빈 OwnerSequenceName은 TOP으로 폴백 (ApplyShotDefaults / RebuildInspectionActions와 동일 정책)
        private static int ComputeLocalShotIndex(InspectionRecipeManager mgr, ShotConfig target, ESequence seqId) {
            if (mgr == null || target == null) return -1;
            string targetSeqName = SequenceHandler.ResolveSequenceName(seqId);
            int localIdx = 0;
            for (int i = 0; i < mgr.ShotCount; i++) {
                ShotConfig s = mgr.Shots[i];
                string shotOwner;
                if (string.IsNullOrEmpty(s.OwnerSequenceName))
                    shotOwner = SequenceHandler.SEQ_TOP;
                else
                    shotOwner = s.OwnerSequenceName;
                if (shotOwner != targetSeqName) continue;
                if (ReferenceEquals(s, target)) return localIdx;
                localIdx++;
            }
            return -1;
        }

        //260616 hbk Phase 51: 트리에서 체크된 SHOT 노드 수집 (재귀)
        private void CollectCheckedShots(NodeViewModel node, List<NodeViewModel> acc) {
            if (node == null) return;
            if (node.IsChecked && node.IsCheckboxVisible) {
                acc.Add(node);
            }
            foreach (NodeViewModel child in node.Children) {
                CollectCheckedShots(child, acc);
            }
        }

        //260616 hbk Phase 51 BATCH-01: 선택 SHOT 일괄 검사 (D-01/D-02/D-03)
        private void Btn_batchRun_Click(object sender, RoutedEventArgs e) {
            var root = treeListBox_sequence.Items.Count > 0 ? treeListBox_sequence.Items[0] as NodeViewModel : null;
            var checkedShots = new List<NodeViewModel>();
            if (root != null) {
                CollectCheckedShots(root, checkedShots);
            }

            if (checkedShots.Count == 0) {
                CustomMessageBox.Show("일괄 검사", "검사할 SHOT 을 체크하세요.", MessageBoxImage.Warning);
                return;
            }

            // D-02: 모든 체크 SHOT 이 동일 시퀀스 소속이어야 함 (Top끼리 / Bottom끼리)
            ESequence seqID = checkedShots[0].SequenceID;
            foreach (NodeViewModel n in checkedShots) {
                if (n.SequenceID != seqID) {
                    CustomMessageBox.Show("일괄 검사",
                        "한 시퀀스 내의 SHOT 만 함께 선택할 수 있습니다.\n(Top 끼리 / Bottom 끼리)",
                        MessageBoxImage.Error);
                    return;
                }
            }

            if (!SystemHandler.Handle.Sequences.IsIdle) {
                CustomMessageBox.Show("일괄 검사", "시퀀스가 이미 실행 중입니다.", MessageBoxImage.Error);
                return;
            }

            var seqBase = SystemHandler.Handle.Sequences[seqID];
            InspectionSequence inspSeq = seqBase as InspectionSequence;
            if (inspSeq == null) {
                CustomMessageBox.Show("일괄 검사", "InspectionSequence 를 찾을 수 없습니다.", MessageBoxImage.Error);
                return;
            }

            var mgr = SystemHandler.Handle.Sequences.RecipeManager;
            var indices = new List<int>();
            //260617 hbk Quick 260617-cq2: 완료 후 그리드 표시용 체크 SHOT 수집
            var batchShots = new List<ShotConfig>();
            foreach (NodeViewModel n in checkedShots) {
                ShotConfig shot = n.Param as ShotConfig;
                if (shot == null) continue;
                int localIdx = ComputeLocalShotIndex(mgr, shot, seqID);
                if (localIdx >= 0) {
                    indices.Add(localIdx);
                    batchShots.Add(shot);
                }
            }

            if (indices.Count == 0) {
                CustomMessageBox.Show("일괄 검사", "선택 SHOT 의 인덱스를 해석할 수 없습니다.", MessageBoxImage.Error);
                return;
            }

            if (_batchService != null && _batchService.IsRunning) {
                CustomMessageBox.Show("일괄 검사", "일괄 검사가 이미 진행 중입니다.", MessageBoxImage.Warning);
                return;
            }

            _batchShots = batchShots; //260617 hbk Quick 260617-cq2: 완료 핸들러에서 그리드 표시에 사용
            _batchService = new BatchRunService();
            _batchService.OnBatchComplete += OnBatchComplete;
            _batchService.StartBatch(inspSeq, indices);
        }

        //260616 hbk Phase 51 BATCH-01: 일괄 검사 1사이클 완료 → 누적 + Export 버튼 활성 (D-04 append, D-05 수동 Export)
        private void OnBatchComplete(List<CycleResultDto> cycles) {
            Dispatcher.Invoke(new Action(delegate {
                if (cycles != null) {
                    _batchAccumulated.AddRange(cycles);
                }
                btn_batchExport.IsEnabled = (_batchAccumulated.Count > 0);
                //260617 hbk Quick 260617-cq2: 검사한 체크 SHOT 전체 측정 결과를 그리드에 펼쳐 표시.
                //  행이 live 측정 객체를 감싸므로 LastMeasuredValue/판정이 즉시 반영됨.
                if (_inspectionVm != null && _batchShots != null) {
                    _inspectionVm.ShowMeasurementsForShots(_batchShots);
                }
            }));
        }

        //260616 hbk Phase 51 BATCH-01: 누적분 수동 엑셀 Export (D-05/D-06 Phase 40 포맷 재사용)
        private void Btn_batchExport_Click(object sender, RoutedEventArgs e) {
            if (_batchAccumulated == null || _batchAccumulated.Count == 0) {
                CustomMessageBox.Show("일괄 Export", "먼저 일괄 검사를 실행하세요.", MessageBoxImage.Warning);
                return;
            }

            string initialDir = SystemHandler.Handle.Setting.ResultSavePath;
            string recipeName = SystemHandler.Handle.Setting.CurrentRecipeName;
            if (recipeName == null) recipeName = "";

            var dlg = new Microsoft.Win32.SaveFileDialog();
            dlg.Filter = "Excel 파일 (*.xlsx)|*.xlsx";
            dlg.FileName = "batch_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".xlsx";
            dlg.InitialDirectory = initialDir;

            if (dlg.ShowDialog() == true) {
                bool ok = ReringProject.Export.RepeatExcelExportService.Export(
                    _batchAccumulated, recipeName, dlg.FileName);
                string msg;
                if (ok) msg = "저장 완료:\n" + dlg.FileName; else msg = "export 실패 (로그 확인)";
                MessageBoxImage icon;
                if (ok) icon = MessageBoxImage.Information; else icon = MessageBoxImage.Error;
                CustomMessageBox.Show("일괄 엑셀 Export", msg, icon);
            }
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
            button_loadImage.IsEnabled = false;
            //button_showConfig.IsEnabled = false;

            if (mParentWindow != null && mParentWindow.mainView != null) {
                mParentWindow.mainView.btn_circleRoi.IsEnabled = false;
                mParentWindow.mainView.btn_teachDatum.IsEnabled = false;
                //260618 hbk Phase 54 ALIGN-01 패턴 ROI/모델 생성 버튼 초기화 비활성
                mParentWindow.mainView.btn_drawPatternRoi.IsEnabled = false;
                mParentWindow.mainView.btn_createPatternModel.IsEnabled = false;
                mParentWindow.mainView.btn_drawAlignLineRoi.IsEnabled = false;
            }

            // e.Source 게이트 대신 sender 기준 게이트 사용:
            // TreeListBox 내부 inner item이 발생시킨 bubble 이벤트에서 e.Source가 inner element가 되어 게이트 실패 → 함수 전체 skip되는 문제 방지.
            // force rebind(D-09)가 XAML 바인딩을 끊은 이후 이 핸들러가 skip되면 PropertyGrid가 stale 상태로 남는다.
            // sender는 항상 XAML에 핸들러를 등록한 본인 컨트롤(treeListBox_sequence) → 비대칭 회피.
            if (!(sender is TreeListBox list)) return;
            if (!ReferenceEquals(sender, treeListBox_sequence)) return;
            {
                if(list.SelectedItem is NodeViewModel) {
                    NodeViewModel item = list.SelectedItem as NodeViewModel;
                    object itemParam = item.Param;

                    mParentWindow.mainView.halconViewer.ClearDatumOverlay();
                    mParentWindow.mainView.halconViewer.ClearDatumRoiCandidates();
                    mParentWindow.mainView.halconViewer.ClearResultDatumOverlays();

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

                    if (item.NodeType == ENodeType.Datum) {
                        button_addFAI.IsEnabled = true;
                        button_removeFAI.IsEnabled = true;
                        button_renameFAI.IsEnabled = true;
                        button_grab.IsEnabled = true;
                        button_loadImage.IsEnabled = true;
                        // PropertyGrid already handled by SetParam above (DatumConfig : ParamBase)
                        if (_inspectionVm != null) _inspectionVm.ClearResults();
                        if (itemParam is DatumConfig datumCfg) {
                            mParentWindow.mainView.halconViewer.SetDatumOverlay(datumCfg, true, mParentWindow.mainView.IsDatumTeachActive);
                            // 이미 티칭된 Datum도 selection 즉시 ROI hit-test 가능하도록 후보 publish
                            mParentWindow.mainView.PublishDatumRoiCandidates(datumCfg);
                            mParentWindow.mainView.DisplayDatumImage(datumCfg);
                            // 티칭 이미지 로드 후 휘발 검출 좌표 복원(재티칭) → 검출 라인 렌더
                            mParentWindow.mainView.RestoreDatumOverlayFromTeach(datumCfg);

                            // Datum 전환 시 PropertyGrid SelectedObject 강제 null→new force rebind.
                            // ROI 이동/생성 후 Datum 전환 시 AlgorithmType combobox가 stale 해지는 문제 방지:
                            // SetParam이 SelectedParam만 갱신하고 ParamEditor.SelectedObject는 외부 binding으로 갱신될 때
                            // combobox가 이전 reference의 stale 값을 유지함 → null 할당으로 binding 강제 해제 후 새 인스턴스 할당.
                            if (ParamEditor != null) {
                                _isRebinding = true;
                                try {
                                    ParamEditor.SelectedObject = null;
                                    ParamEditor.SelectedObject = datumCfg;
                                } finally {
                                    _isRebinding = false;
                                }
                            }
                        }
                        if (mParentWindow != null && mParentWindow.mainView != null) {
                            mParentWindow.mainView.btn_teachDatum.IsEnabled = true;
                            //260618 hbk Phase 54 ALIGN-01 패턴 ROI/모델 생성 버튼 활성화 (btn_teachDatum 게이팅 동일)
                            mParentWindow.mainView.btn_drawPatternRoi.IsEnabled = true;
                            mParentWindow.mainView.btn_createPatternModel.IsEnabled = true;
                            mParentWindow.mainView.btn_drawAlignLineRoi.IsEnabled = true;
                        }
                    }
                    else if (item.NodeType == ENodeType.Measurement) {
                        button_addFAI.IsEnabled = true;
                        button_removeFAI.IsEnabled = true;
                        button_renameFAI.IsEnabled = true;
                        bool isRectRoiType = item.Param is EdgeToLineDistanceMeasurement
                            || item.Param is EdgeToLineAngleMeasurement
                            || item.Param is ArcEdgeDistanceMeasurement
                            || item.Param is ArcLineIntersectDistanceMeasurement
                            || item.Param is CompoundAngleMeasurement
                            || item.Param is CompoundCenterCDistanceMeasurement
                            || item.Param is CompoundCenterBDistanceMeasurement
                            || item.Param is CompoundShortAxisDistanceMeasurement
                            || item.Param is DualImageEdgeDistanceMeasurement;
                        if (mParentWindow != null && mParentWindow.mainView != null)
                            mParentWindow.mainView.btn_rectRoi.IsEnabled = isRectRoiType;
                        // PropertyGrid handled by SetParam (MeasurementBase : ParamBase)
                        // Datum force rebind 후 binding 손상 → Measurement 전환 시 PropertyGrid stale.
                        // Datum 패턴(D-09)과 동일하게 null→new 강제 재할당.
                        if (ParamEditor != null && itemParam != null) {
                            _isRebinding = true;
                            try {
                                ParamEditor.SelectedObject = null;
                                ParamEditor.SelectedObject = itemParam;
                            } finally {
                                _isRebinding = false;
                            }
                        }
                        if (_inspectionVm != null) _inspectionVm.ClearResults();
                        if (mParentWindow != null && mParentWindow.mainView != null && itemParam is MeasurementBase meas)
                            mParentWindow.mainView.RenderInspectionResultForNode(meas);
                        // 측정 노드 선택 시 그 측정의 DatumRef가 가리키는 시퀀스 datum 1개를 결과 화면에 표시
                        if (mParentWindow != null && mParentWindow.mainView != null && itemParam is MeasurementBase measForDatum) {
                            List<DatumConfig> datumsForMeas = ResolveDatumsForMeasurement(item.SequenceID, measForDatum);
                            mParentWindow.mainView.ShowResultDatumOverlays(datumsForMeas);
                        }
                        // DualImage 선택 시 swap UI owner set (mutex). 비-DualImage는 as 캐스트 결과 null → swap UI 자동 Collapsed.
                        if (mParentWindow != null && mParentWindow.mainView != null) {
                            mParentWindow.mainView.PublishMeasurementDualImageSelection(itemParam as DualImageEdgeDistanceMeasurement);
                        }
                    }
                    else if (item.NodeType == ENodeType.FAI) {
                        button_addFAI.IsEnabled = true;
                        button_removeFAI.IsEnabled = true;
                        button_renameFAI.IsEnabled = true;
                        if (_inspectionVm != null && itemParam is FAIConfig faiConfig) {
                            _inspectionVm.OnFAISelected(faiConfig);
                            mParentWindow.mainView.RenderInspectionResultForNode(faiConfig);
                            // FAI 노드 선택 시 하위 measurement들의 DatumRef가 가리키는 시퀀스 datum(중복 제거)을 결과 화면에 표시
                            if (mParentWindow != null && mParentWindow.mainView != null) {
                                List<DatumConfig> datumsForFai = ResolveDatumsForFai(item.SequenceID, faiConfig);
                                mParentWindow.mainView.ShowResultDatumOverlays(datumsForFai);
                            }
                            // Datum force rebind가 SelectedObject binding을 끊어 → FAI 전환 시 자동 갱신 안 됨 → 명시적 재할당 필요.
                            if (ParamEditor != null) {
                                _isRebinding = true;
                                try {
                                    ParamEditor.SelectedObject = null;
                                    ParamEditor.SelectedObject = faiConfig;
                                } finally {
                                    _isRebinding = false;
                                }
                            }
                        }
                    }
                    else if (item.NodeType == ENodeType.Action) {
                        button_addFAI.IsEnabled = true;
                        button_removeFAI.IsEnabled = (item.Param is ShotConfig);
                        button_renameFAI.IsEnabled = false;
                        if (_inspectionVm != null) {
                            _inspectionVm.OnActionSelected(item);
                        }
                        if (mParentWindow != null && mParentWindow.mainView != null && item.Param is ShotConfig shotSel)
                            mParentWindow.mainView.DisplayShotImage(shotSel);
                        if (mParentWindow != null && mParentWindow.mainView != null && item.Param is ShotConfig shotForDatum) {
                            List<DatumConfig> datumsForShot = ResolveDatumsForShot(item.SequenceID, shotForDatum);
                            mParentWindow.mainView.ShowResultDatumOverlays(datumsForShot);
                        }
                        // Datum force rebind가 XAML SelectedObject 바인딩을 끊은 이후
                        // Action 노드 클릭 시 PropertyGrid가 stale(직전 Datum/FAI 유지) → 명시적 재할당 필요.
                        if (ParamEditor != null && itemParam is ParamBase) {
                            _isRebinding = true;
                            try {
                                ParamEditor.SelectedObject = null;
                                ParamEditor.SelectedObject = itemParam;
                            } finally {
                                _isRebinding = false;
                            }
                        }
                    }
                    else if (item.NodeType == ENodeType.Sequence) {
                        button_addFAI.IsEnabled = true;
                        button_removeFAI.IsEnabled = false;
                        button_renameFAI.IsEnabled = false;
                        if (_inspectionVm != null) _inspectionVm.ClearResults();
                        // Sequence 노드는 통상 ParamBase가 없으므로 명시적 null 할당으로 PropertyGrid 비움.
                        if (ParamEditor != null) {
                            _isRebinding = true;
                            try {
                                ParamEditor.SelectedObject = null;
                                if (itemParam is ParamBase) ParamEditor.SelectedObject = itemParam;
                            } finally {
                                _isRebinding = false;
                            }
                        }
                    }
                    else {
                        button_addFAI.IsEnabled = false;
                        button_removeFAI.IsEnabled = false;
                        button_renameFAI.IsEnabled = false;
                        if (_inspectionVm != null) _inspectionVm.ClearResults();
                        if (ParamEditor != null) {
                            _isRebinding = true;
                            try {
                                ParamEditor.SelectedObject = null;
                                if (itemParam is ParamBase) ParamEditor.SelectedObject = itemParam;
                            } finally {
                                _isRebinding = false;
                            }
                        }
                    }

                    bool circleEnabled = false;
                    if (itemParam is FAIConfig faiForCircle) {
                        foreach (var m in faiForCircle.Measurements) {
                            if (m is CircleDiameterMeasurement || m is CircleCenterDistanceMeasurement) {
                                circleEnabled = true; break;
                            }
                        }
                    }
                    else if (itemParam is CircleDiameterMeasurement || itemParam is CircleCenterDistanceMeasurement) {
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
            if (SelectedParam is DatumConfig datumForLoad) {
                ICameraParam resolved = ResolveDatumCameraParam(datumForLoad);
                if (resolved == null) return;
                mParentWindow.mainView.LoadAndDisplay(resolved, datumForLoad);
                // Datum Load 후 TeachingImagePath 자동속성 write-back은 INPC 미발동 → PropertyGrid 강제 재바인딩
                RefreshParamEditor();
                return;
            }
            if (!(SelectedParam is ICameraParam)) return;

            ICameraParam camParam = SelectedParam as ICameraParam;
            mParentWindow.mainView.LoadAndDisplay(camParam);
            //260616 hbk Phase 51 UAT: SHOT Load 후 SimulImagePath 자동속성 write-back은 INPC 미발동 → PropertyGrid 강제 재바인딩 (Datum 분기와 동일 처리)
            RefreshParamEditor();
        }

        // SourceShotName으로 ShotConfig 조회, 없으면 Shots[0] fallback
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

                if (selectedNode.NodeType == ENodeType.Datum) {
                    NodeViewModel seqNodeForDatum = selectedNode.Parent;
                    if (seqNodeForDatum != null && seqNodeForDatum.NodeType == ENodeType.Sequence) {
                        AddDatumToSequence(seqNodeForDatum);
                    }
                    return;
                }

                if (selectedNode.NodeType == ENodeType.Measurement) {
                    NodeViewModel faiNodeForMeas = selectedNode.Parent;
                    if (faiNodeForMeas != null && faiNodeForMeas.NodeType == ENodeType.FAI && faiNodeForMeas.Param is FAIConfig faiOwner) {
                        AddMeasurementToFAI(faiNodeForMeas, faiOwner);
                    }
                    return;
                }

                // Sequence 노드 선택 시: Shot 또는 Datum 추가 선택 (D-25)
                if (selectedNode.NodeType == ENodeType.Sequence) {
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

        private void AddMeasurementToFAI(NodeViewModel faiNode, FAIConfig fai) {
            string[] typeNames = MeasurementFactory.GetTypeNames();
            string defaultType = "EdgePairDistance";
            if (typeNames.Length > 0) defaultType = typeNames[0];

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
            string ownerSeqName = SequenceHandler.ResolveSequenceName(seqNode.SequenceID);
            ShotConfig shot = seqHandler.RecipeManager.AddShot(shotName, ownerSeqName);
            FAIConfig fai = shot.AddFAI("FAI_0");
            seqHandler.EnableDynamicFAIMode();

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
            // RebuildInspectionActions를 호출하지 않아도, 실행 시 Btn_start_Click → ResolveRunnableAction이 지연 동기화로 Shot→Action 매핑을 복구한다.
            shotNode.Children.Add(faiChildNode);

            var shotVm = new NodeViewModel(shotNode, seqNode);
            seqNode.Children.Add(shotVm);
            //InspectionListViewModel.SortNodeChildren(seqNode);
            seqNode.IsExpanded = true;
        }

        private void Btn_MoveUp_Click(object sender, RoutedEventArgs e) {
            try {
                if (!(treeListBox_sequence.SelectedItem is NodeViewModel sel)) return;
                if (!InspectionListViewModel.MoveNode(sel, -1)) return;
                sel.IsSelected = true; // 이동 후 선택 유지
            } catch { /* UX 안정성 우선 — 예외 swallow */ }
        }

        private void Btn_MoveDown_Click(object sender, RoutedEventArgs e) {
            try {
                if (!(treeListBox_sequence.SelectedItem is NodeViewModel sel)) return;
                if (!InspectionListViewModel.MoveNode(sel, +1)) return;
                sel.IsSelected = true; // 이동 후 선택 유지
            } catch { /* UX 안정성 우선 — 예외 swallow */ }
        }

        private void Btn_RemoveFAI_Click(object sender, RoutedEventArgs e) {
            try {
                if (!(treeListBox_sequence.SelectedItem is NodeViewModel selectedNode)) return;

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
                    if (_inspectionVm != null) _inspectionVm.ClearResults();
                    return;
                }

                if (selectedNode.NodeType == ENodeType.Measurement && selectedNode.Param is MeasurementBase measToRemove) {
                    NodeViewModel faiParent = selectedNode.Parent;
                    if (faiParent == null || !(faiParent.Param is FAIConfig faiOwner)) return;
                    int measIdx = faiOwner.Measurements.IndexOf(measToRemove);
                    if (measIdx < 0) return;

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
                    if (_inspectionVm != null) _inspectionVm.ClearResults();
                    return;
                }

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
                    if (_inspectionVm != null) _inspectionVm.ClearResults();
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
                if (_inspectionVm != null) _inspectionVm.ClearResults();
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
