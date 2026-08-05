
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;
using PropertyTools.Wpf;
using ReringProject.Define;
using ReringProject.Sequence;
using ReringProject.Setting;
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

        // 260724 hbk PropertyTools.Wpf DataGrid Enter키 커밋 버그 우회.
        //  실제 참조 DLL은 libs\PropertyTools.Wpf.dll(csproj Reference HintPath, v1.0.0.0) — packages\PropertyTools.Wpf.3.1.0
        //  는 packages.config에 남아있는 미사용 NuGet 복원본이며 빌드에 쓰이지 않으므로 재검증 시 이 v1.0.0.0 DLL을 봐야 함.
        //  라이브 디버깅(Value getter/setter 동시 breakpoint)으로 재현 확인 + 위 실제 참조 DLL을 ildasm 역어셈블해
        //  근본원인 검증: DataGrid.TextEditorPreviewKeyDown 은 Enter(Key.Return)에서 UpdateSource() 호출 없이
        //  RemoveEditControl()만 호출하고, RemoveEditControl()도 UIElementCollection.Remove()로 편집용 TextBox를
        //  트리에서 직접 뜯어낼 뿐이라 LostFocus 기반 커밋(TextBox.Text의 기본 UpdateSourceTrigger=LostFocus)이
        //  걸리지 않는다 — Exposure 등 배열 셀에 값 입력 후 Enter 시 Setter가 호출되지 않고 폐기된다. Tab은 이
        //  메서드가 아예 관여하지 않는 키라 표준 포커스-이동 경로를 타 정상 커밋되고, 마우스 클릭도 표준 포커스
        //  전환이라 문제없다([AutoUpdateText]는 PropertyGridControlFactory 전용이라 DataGrid 배열 셀에는 적용
        //  안 됨을 확인 — 이 방식으로는 해결 불가).
        //  PreviewKeyDown은 tunnel 이벤트라 ParamEditor(DataGrid의 상위 트리)에 등록한 이 핸들러가 PropertyTools가
        //  TextBox 인스턴스에 직접 건 핸들러보다 먼저 실행된다 — Enter 전용으로 포커스된 TextBox의 바인딩을
        //  UpdateSource()로 먼저 강제 커밋한 뒤, e.Handled는 건드리지 않아(false 유지) PropertyTools의 기존 Enter
        //  처리(RemoveEditControl 등)가 그대로 이어서 실행되게 한다. Tab/클릭은 Key.Enter 가드로 이 핸들러를
        //  전혀 타지 않으므로 영향 없음. Escape(취소)는 별도 분기(ClearBinding)라 마찬가지로 영향 없음.
        //  참고(잔여 리스크): ParamEditor에는 배열 셀 외에 일반 스칼라 속성(MotorXPos/PixelToUM_Offset 등,
        //  PropertyGridControlFactory가 렌더하는 행)도 같은 트리에 있어 그 행에서 Enter를 눌러도 이 핸들러가
        //  먼저 UpdateSource()를 태운다 — 단순 auto-property라 무해할 것으로 판단되나 정식 검증은 안 됨(라이브 확인 필요).
        //  임시 진단 로그: 사용자가 Enter 커밋이 실제로 동작하는지 로그로 확인하기 위한 용도. 확인 후 제거 여부 재검토.
        private void ParamEditor_PreviewKeyDown(object sender, KeyEventArgs e) {
            if (e.Key != Key.Enter) return;

            if (!(Keyboard.FocusedElement is TextBox tb)) return;

            try {
                var bindingExpr = tb.GetBindingExpression(TextBox.TextProperty);
                if (bindingExpr != null) {
                    bindingExpr.UpdateSource();
                    Logging.PrintLog((int)ELogType.Trace, "[ParamEditor Enter-commit workaround] fired — UpdateSource() 실행됨 (PropertyTools DataGrid Enter 버그 우회)");
                }
            } catch (Exception ex) {
                Logging.PrintLog((int)ELogType.Error, "[ParamEditor Enter-commit workaround] UpdateSource 예외: " + ex.Message);
            }
            // e.Handled를 설정하지 않는다 — PropertyTools의 기존 Enter 처리(RemoveEditControl 등)가 이어서 정상 실행되어야 함
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

            //260805 hbk Phase 69 D-01/D-03: 전역 IsIdle 이 아니라 "이 시퀀스 + 실제 같은 물리 카메라를 쓰는 시퀀스" 만 본다.
            //  다른 물리 카메라를 쓰는 시퀀스가 돌고 있어도 이 시퀀스는 독립 실행 가능(전역 IsIdle 이 막던 결함).
            string sBlockingSeqName;
            if (SystemHandler.Handle.Sequences.TryGetBlockingSequence(seqID, out sBlockingSeqName)) {
                CustomMessageBox.Show("Error",
                    string.Format(
                        "실행할 수 없습니다 — '{0}' 시퀀스가 아직 Idle 이 아닙니다.\n(자기 자신이거나, 같은 물리 카메라를 공유하는 시퀀스입니다.)",
                        sBlockingSeqName),
                    MessageBoxImage.Error);
                return;
            }

            if (!ResolveRunnableAction(node, seq, out EAction actID)) {
                CustomMessageBox.Show("Error",
                    "There is no action to run.\nSelect a Sequence or Shot node that has a registered action.",
                    MessageBoxImage.Error);
                return;
            }

            //260716 hbk 오프라인 모드 켜진 채 라이브 검사 오인 방지 — RUN 시점 확인.
            //  배경: OfflineInspectMode 는 SystemSetting(시스템 전역·영속) 이라 레시피를 바꿔도 켜진 채 남는다.
            //  수동 지그 셋업 후 끄는 걸 잊고 실물 라인에서 RUN 하면, 카메라 앞에 실제 부품이 있어도 과거 저장 이미지로
            //  조용히 측정되어 PASS/FAIL 이 실물과 무관하게 나온다(화면·로그 어디에도 표시 없음). 저장 이미지가 없으면
            //  NG+로그로 드러나지만, 있으면 정상 사이클과 구분 불가 — 그래서 트리거 시점에 명시적으로 확인한다.
            if (SystemHandler.Handle.Setting.OfflineInspectMode) {
                MessageBoxResult offlineAnswer = CustomMessageBox.ShowConfirmation(
                    "오프라인 검사 모드",
                    "OfflineInspectMode 가 켜져 있습니다.\n\n"
                    + "실제 카메라로 촬영하지 않고 저장된 검사이미지로 검사합니다.\n"
                    + "(실물 부품을 지금 촬영해 검사하려면 Setting 에서 OfflineInspectMode 를 끄세요.)\n\n"
                    + "저장 이미지로 계속 진행할까요?",
                    MessageBoxButton.OKCancel);
                if (offlineAnswer != MessageBoxResult.OK) return;
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

            // Shot 노드 (Action 타입 + ShotConfig Param): 살아있는 Actions[] 를 ShotParam 참조 동일성으로 스캔
            if (node.NodeType == ENodeType.Action && node.Param is ShotConfig shotCfg) {
                var seqHandler = SystemHandler.Handle.Sequences;
                //260729 hbk quick-260729-jq5: Shot 삭제(Btn_RemoveFAI_Click)와 순서변경(InspectionListViewModel.MoveNode/
                //  SwapParamCollection)이 RebuildInspectionActions 를 호출하지 않아 RecipeManager.Shots 순번과
                //  SequenceBase.Actions[] 가 어긋나고, 그 결과 서수 기반 매핑이 엉뚱한 Shot 을 실행/측정한 확인된 결함
                //  (실기 재현: SHOT_E1-4 선택 → SHOT_B1-4 실행)이 있었다. 그래서 순번이 아니라 살아있는 Actions[] 의
                //  ShotParam 참조 동일성으로 찾는다 — 서수 기반 폴백 경로는 두지 않는다.
                int idx = ResolveActionIndexByShot(seq, shotCfg);
                if (idx >= 0) {
                    actID = seq[idx].ID;
                    return true;
                }

                // 매핑 실패 (UI에서 Shot 추가 후 RebuildInspectionActions 미호출) → 지연 동기화 후 재스캔
                //260805 hbk Phase 69 D-01: rebuild 는 이 시퀀스의 Actions[] 만 재생성한다(SequenceHandler.RebuildInspectionActions).
                //  다른 시퀀스가 돌고 있는지는 무관 — 대상 시퀀스만 Idle 이면 안전하다.
                if (seqHandler.GetSequenceState(seq.ID) == EContextState.Idle) {
                    seqHandler.EnableDynamicFAIMode();
                    seqHandler.RebuildInspectionActions(seq.ID);
                    idx = ResolveActionIndexByShot(seq, shotCfg);
                    if (idx >= 0) {
                        actID = seq[idx].ID;
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

        //260729 hbk quick-260729-jq5: 살아있는 SequenceBase.Actions[] 를 직접 훑어 동일 ShotConfig 객체 참조를 찾는다.
        //  RecipeManager.Shots 순번(서수)에 의존하지 않으므로 Shot 삭제/순서변경으로 Actions[] 가 낡은 상태여도
        //  엉뚱한 Shot 을 가리키지 않는다 — 못 찾으면 -1 만 반환하고 서수 폴백은 두지 않는다.
        //  미러링 대상: InspectionSequence.FindActionIndicesByZIndex (InspectionSequence.cs L496-520)
        private int ResolveActionIndexByShot(SequenceBase seq, ShotConfig target) {
            if (seq == null || target == null) return -1;
            for (int i = 0; i < seq.ActionCount; i++) {
                var faiAct = seq[i] as Action_FAIMeasurement;
                if (faiAct == null) continue;
                if (ReferenceEquals(faiAct.ShotParam, target)) return i;
            }
            return -1;
        }

        //260729 hbk quick-260729-jq5: 체크된 각 Shot 을 ResolveActionIndexByShot 으로 해석해 (shot, idx) 쌍으로 반환.
        //  못 찾은 Shot 도 idx=-1 로 포함시켜 호출부가 rebuild 필요 여부를 판단할 수 있게 한다.
        private List<Tuple<ShotConfig, int>> ResolveBatchShotIndices(SequenceBase seq, List<ShotConfig> shots) {
            var result = new List<Tuple<ShotConfig, int>>();
            foreach (ShotConfig shot in shots) {
                int idx = ResolveActionIndexByShot(seq, shot);
                result.Add(Tuple.Create(shot, idx));
            }
            return result;
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

            // 일괄검사는 _batchService/_batchShots/_batchAccumulated 를 시퀀스별로 분리하지 않은 단일 공용 필드로
            // 관리한다. Btn_start_Click 과 동일한 시퀀스 단위 판정으로 크로스-시퀀스 동시 실행을 허용하면, 뒤이은
            // _batchService = new BatchRunService() 가 아직 실행 중인 다른 시퀀스의 참조를 덮어써 크래시가 난다.
            // 공용 필드를 시퀀스별로 분리하기 전까지는 전역 IsIdle 게이트를 유지한다.
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

            //260729 hbk quick-260729-jq5: Shots 서수가 아니라 Actions[] 인덱스를 전달한다.
            //  StartSubset 이 min-max 연속구간을 실행하므로 오름차순 정렬이 필수이고, _batchShots 는 indices 와
            //  정렬 정합을 유지해야 한다(같은 (idx, shot) 쌍을 함께 정렬).
            var checkedTargetShots = new List<ShotConfig>();
            foreach (NodeViewModel n in checkedShots) {
                ShotConfig shot = n.Param as ShotConfig;
                if (shot == null) continue;
                checkedTargetShots.Add(shot);
            }

            var resolvedPairs = ResolveBatchShotIndices(inspSeq, checkedTargetShots);
            //260805 hbk Phase 69 D-01: (B)와 같은 이유 — 대상 시퀀스만 Idle 이면 rebuild 안전.
            if (resolvedPairs.Any(p => p.Item2 < 0)
                && SystemHandler.Handle.Sequences.GetSequenceState(seqID) == EContextState.Idle) {
                // 딱 한 번만 rebuild — Shot 마다 반복 호출하면 Actions[] 를 통째로 재생성해 낭비/DoS
                SystemHandler.Handle.Sequences.EnableDynamicFAIMode();
                SystemHandler.Handle.Sequences.RebuildInspectionActions(seqID);
                resolvedPairs = ResolveBatchShotIndices(inspSeq, checkedTargetShots);
            }

            var sortedPairs = resolvedPairs.Where(p => p.Item2 >= 0).OrderBy(p => p.Item2).ToList();
            var indices = sortedPairs.Select(p => p.Item2).ToList();
            var batchShots = sortedPairs.Select(p => p.Item1).ToList();

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
                bool ok = ReringProject.Export.RepeatExcelExportService.ExportBatch(
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
            button_grabInsp.IsEnabled = false;
            button_loadImage.IsEnabled = false;
            //button_showConfig.IsEnabled = false;

            if (mParentWindow != null && mParentWindow.mainView != null) {
                mParentWindow.mainView.btn_circleRoi.IsEnabled = false;
                mParentWindow.mainView.btn_teachDatum.IsEnabled = false;
                //260618 hbk Phase 54 ALIGN-01 패턴 ROI/모델 생성 버튼 초기화 비활성
                //260622 hbk Phase 57.1 D-04(a): 패턴 3버튼 진입 시 무조건 비활성화 → Datum 분기에서만 재활성. 비-Datum 노드 = 비활성 유지.
                mParentWindow.mainView.btn_drawPatternRoi.IsEnabled = false;
                mParentWindow.mainView.btn_createPatternModel.IsEnabled = false;
                mParentWindow.mainView.btn_drawPatternRoi2.IsEnabled = false;
                mParentWindow.mainView.btn_reanchor.IsEnabled = false;
            }

            // e.Source 게이트 대신 sender 기준 게이트 사용:
            // TreeListBox 내부 inner item이 발생시킨 bubble 이벤트에서 e.Source가 inner element가 되어 게이트 실패 → 함수 전체 skip되는 문제 방지.
            // force rebind(D-09)가 XAML 바인딩을 끊은 이후 이 핸들러가 skip되면 PropertyGrid가 stale 상태로 남는다.
            // sender는 항상 XAML에 핸들러를 등록한 본인 컨트롤(treeListBox_sequence) → 비대칭 회피.
            if (!(sender is TreeListBox list)) return;
            if (!ReferenceEquals(sender, treeListBox_sequence)) return;
            //260626 hbk 트리 크래시 수정 ("Cannot call StartAt when content generation is in progress"):
            //  TreeListBox(가상화) 컨테이너 생성 도중 이 핸들러가 동기로 ItemsSource/SelectedObject 를 재교체
            //  (ClearResults / PropertyGrid SelectedObject rebind / RestoreDatumOverlayFromTeach 재티칭)하면
            //  ItemContainerGenerator 가 재진입(StartAt)되어 트리가 뒤섞이며 크래시. 무거운 선택 처리를
            //  Dispatcher(Background)로 한 틱 미뤄 현재 생성 패스 종료 후 실행 → 재진입 차단. 지연 시점 최신
            //  SelectedItem 재조회(빠른 연속 선택 안전). 상단 버튼 비활성화는 즉시 동기 유지.
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background, new Action(() => {
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
                            button_grabInsp.IsEnabled = true;
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
                        button_grabInsp.IsEnabled = true;
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

                            //260622 hbk Phase 57.1 D-03/D-01: Datum 노드 단독 선택 시에도 _resultDatumOverlays 를 채워 cyan 패턴 ROI 렌더 게이트 충족.
                            //  Measurement/FAI/Action 분기와 동일 경로(ShowResultDatumOverlays) — 단일-원소 List. SetDatumOverlay(편집 채널)와 무관(공존).
                            List<DatumConfig> datumOverlayList = new List<DatumConfig> { datumCfg };
                            mParentWindow.mainView.ShowResultDatumOverlays(datumOverlayList);

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
                            mParentWindow.mainView.btn_drawPatternRoi2.IsEnabled = true;
                            mParentWindow.mainView.btn_reanchor.IsEnabled = true;
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
            }));
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
                // [추가 1] CopyTo 로 자식 컬렉션이 통째로 바뀌므로, 갱신할 대상 노드를 미리 잡아둔다.
                NodeViewModel pasteTargetNode = treeListBox_sequence.SelectedItem as NodeViewModel;
                //paste
                if(CopiedParam.CopyTo(SelectedParam) == false) {
                    //fail
                    CustomMessageBox.Show("Fail to Copy", string.Format("Copy Failed From {0} into {1}", CopiedParam.ToString(), SelectedParam.ToString()), MessageBoxImage.Error);
                    return;
                }
                // [추가 2] 복사된 FAI/Measurement 를 트리에 즉시 반영 (전체 RebuildTree 는 쓰지 않는다 —
                //  RebuildTree 는 seq.Actions[] 를 기준으로 돌아서, 아직 Actions[] 에 반영되지 않은
                //  이번 세션 신규 Shot 이 트리에서 사라질 수 있다).
                RefreshChildNodesAfterPaste(pasteTargetNode);
                //success
                mParentWindow.statusBar.Model.SetText(string.Format("Pasted : {0} to {1}",CopiedParam.ToString(), SelectedParam.ToString()));
                int index = treeListBox_sequence.SelectedIndex;

                //reselect (update)
                treeListBox_sequence.UnselectAll();
                treeListBox_sequence.SelectedIndex = index;
            }
        }

        // 붙여넣기로 자식 컬렉션이 교체된 노드의 트리 자식 VM 을 다시 만든다.
        //  Shot 노드 → FAI + 그 아래 Measurement, FAI 노드 → Measurement 만 재구축한다.
        //  그 외 노드 타입(Datum/Sequence)은 자식이 없거나 바뀌지 않으므로 아무것도 하지 않는다.
        private void RefreshChildNodesAfterPaste(NodeViewModel targetNode) {
            if (targetNode == null) return;

            ShotConfig pastedShot = targetNode.Param as ShotConfig;
            if (pastedShot != null) {
                targetNode.Children.Clear();
                for (int i = 0; i < pastedShot.FAIList.Count; i++) {
                    FAIConfig fai = pastedShot.FAIList[i];
                    ViewModel.AddFAINode(targetNode, fai, targetNode.SequenceID, targetNode.ActionID);
                    NodeViewModel faiVm = targetNode.Children[targetNode.Children.Count - 1];
                    for (int j = 0; j < fai.Measurements.Count; j++) {
                        ViewModel.AddMeasurementNode(faiVm, fai.Measurements[j]);
                    }
                }
                targetNode.IsExpanded = true;
                return;
            }

            FAIConfig pastedFai = targetNode.Param as FAIConfig;
            if (pastedFai != null) {
                targetNode.Children.Clear();
                for (int j = 0; j < pastedFai.Measurements.Count; j++) {
                    ViewModel.AddMeasurementNode(targetNode, pastedFai.Measurements[j]);
                }
                targetNode.IsExpanded = true;
            }
        }


        private void button_grab_Click(object sender, RoutedEventArgs e) {
            if (SelectedParam == null) return;
            if (SelectedParam is DatumConfig datumForGrab) {
                ICameraParam resolved = ResolveDatumCameraParam(datumForGrab);
                if (resolved == null) return;
                if (SystemHandler.Handle.Sequences.IsIdle == false) return;
                mParentWindow.mainView.GrabAndDisplay(resolved, datumForGrab);
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

        // 오프라인/수동 검사: 선택 노드(datum/shot)를 라이브 grab → 그 노드 검사이미지 경로에 저장.
        //  Z 모터 없는 수동 지그: datum Z 맞춰 datum 노드 '검사Grab', 각 shot Z 맞춰 shot 노드 '검사Grab' → OfflineInspectMode 검사가 이 저장본 로드.
        private async void button_grabInsp_Click(object sender, RoutedEventArgs e) {
            if (SelectedParam == null) return;
            if (SystemHandler.Handle.Sequences.IsIdle == false) {
                CustomMessageBox.Show("검사이미지 Grab", "시퀀스가 검사 중입니다. Idle 상태에서 다시 시도하세요.", MessageBoxImage.Warning);
                return;
            }
            if (SelectedParam is DatumConfig datumForGrab) {
                ICameraParam resolved = ResolveDatumCameraParam(datumForGrab);
                if (resolved == null) return;
                string savePath = BuildOfflineImagePath("datum_" + datumForGrab.DatumName);
                if (savePath == null) return;
                await mParentWindow.mainView.GrabSaveAndDisplay(resolved, datumForGrab, datumForGrab, savePath);
                RefreshParamEditor(); // TeachingImagePath write-back → PropertyGrid 갱신 (grab 완료 후)
                return;
            }
            if (SelectedParam is ShotConfig shotForGrab) {
                string savePath = BuildOfflineImagePath("shot_" + shotForGrab.ShotName);
                if (savePath == null) return;
                await mParentWindow.mainView.GrabSaveAndDisplay(shotForGrab, null, shotForGrab, savePath);
                RefreshParamEditor(); // SimulImagePath write-back → PropertyGrid 갱신 (grab 완료 후)
                return;
            }
            // 그 외(FAI/Measurement 등) 노드는 검사이미지 대상 아님
            CustomMessageBox.Show("검사이미지 Grab", "Datum 또는 Shot 노드를 선택하세요.", MessageBoxImage.Warning);
        }

        // 오프라인 검사이미지 저장 경로: <ImageSavePath>\OfflineInspect\<recipe>\<baseName>.bmp. 폴더 자동생성. 실패 시 null.
        //  포맷 = bmp(무압축). 검사이미지는 라이브 grab을 그대로 대체해야 하므로 손실압축(jpg) 불가, 무손실이어야 함
        //  (PNG는 무손실이지만 CXP 13376x9528(~1.27억 픽셀) 원본에서 DEFLATE 압축 자체가 tact 병목이 되어 bmp로 전환 — 260716 검사Grab 지연 실측 대응).
        private string BuildOfflineImagePath(string baseName) {
            try {
                string root = SystemHandler.Handle.Setting.ImageSavePath;
                if (string.IsNullOrEmpty(root)) root = AppDomain.CurrentDomain.BaseDirectory;
                string recipe = SystemHandler.Handle.Setting.CurrentRecipeName;
                if (string.IsNullOrEmpty(recipe)) recipe = "default";
                recipe = SanitizeFileName(recipe);
                string dir = Path.Combine(Path.Combine(root, "OfflineInspect"), recipe);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                string safe = SanitizeFileName(baseName);
                if (string.IsNullOrEmpty(safe)) safe = "node";
                return Path.Combine(dir, safe + ".bmp");
            }
            catch (Exception ex) {
                CustomMessageBox.Show("검사이미지 경로 오류", ex.Message, MessageBoxImage.Error);
                return null;
            }
        }

        private static string SanitizeFileName(string name) {
            if (string.IsNullOrEmpty(name)) return name;
            foreach (char c in Path.GetInvalidFileNameChars()) {
                name = name.Replace(c, '_');
            }
            return name;
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

        //260630 hbk — 전체 Shot SimulImagePath 폴더 일괄 설정 (마지막 폴더 기억)
        private static string _lastSimulImageFolder = null;
        private static readonly HashSet<string> SimulFolderImageExts = new HashSet<string>(
            new[] { ".bmp", ".png", ".jpg", ".jpeg", ".tif", ".tiff" },
            StringComparer.OrdinalIgnoreCase);

        private void button_simFolder_Click(object sender, RoutedEventArgs e) {
            //260630 hbk — 폴더 선택 → 알파벳 정렬 이미지 → Shot 순서대로 SimulImagePath 할당
            try {
                var dlg = new Ookii.Dialogs.Wpf.VistaFolderBrowserDialog();
                dlg.Multiselect = false;
                if (!string.IsNullOrEmpty(_lastSimulImageFolder)) {
                    dlg.SelectedPath = _lastSimulImageFolder;
                }
                if (dlg.ShowDialog() != true) {
                    return;
                }

                string folder = dlg.SelectedPath;
                if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder)) {
                    return;
                }
                _lastSimulImageFolder = folder;

                List<string> imagePaths = Directory.GetFiles(folder)
                    .Where(f => SimulFolderImageExts.Contains(Path.GetExtension(f)))
                    .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (imagePaths.Count == 0) {
                    CustomMessageBox.Show("이미지 없음", "bmp/png/jpg/tif 파일이 없습니다:\n" + folder, MessageBoxImage.Warning);
                    return;
                }

                var shots = SystemHandler.Handle.Sequences.RecipeManager.Shots;
                if (shots == null || shots.Count == 0) {
                    CustomMessageBox.Show("Shot 없음", "레시피에 Shot이 없습니다.", MessageBoxImage.Warning);
                    return;
                }

                for (int i = 0; i < shots.Count; i++) {
                    shots[i].SimulImagePath = imagePaths[i % imagePaths.Count];
                }

                RefreshParamEditor();
                CustomMessageBox.Show("SimFolder 완료",
                    string.Format("{0}개 Shot에 이미지 {1}개 할당\n폴더: {2}", shots.Count, imagePaths.Count, folder),
                    MessageBoxImage.Information);
            }
            catch (Exception ex) {
                CustomMessageBox.Show("SimFolder 실패", ex.Message, MessageBoxImage.Error);
            }
        }

        // SourceShotName으로 ShotConfig 조회, 없으면 Shots[0] fallback
        private ICameraParam ResolveDatumCameraParam(DatumConfig datum) {
            var shots = SystemHandler.Handle.Sequences.RecipeManager.Shots;
            if (shots.Count == 0) return null;
            if (!string.IsNullOrEmpty(datum.SourceShotName)) {
                ShotConfig matched = shots.FirstOrDefault(s => s.ShotName == datum.SourceShotName);
                if (matched != null) return matched;
            }
            // 260723 hbk: SourceShotName 미설정 시 "이 datum이 속한 시퀀스의 첫 Shot"으로 폴백해야 하는데
            //  (DatumConfig.cs:32 comment 원래 의도) shots[0]은 전역 Shots 리스트의 첫 항목이라 다른 시퀀스
            //  (예: TOP)의 Shot이 잡혀 잘못된 카메라로 grab되는 결함이 있었다(Side datum grab이 CAM_TOP을
            //  조회해 Device Not Opened 나던 원인). RebuildInspectionActions(SequenceHandler.cs)와 동일한
            //  OwnerSequenceName 폴백 정책(빈 값=TOP)으로 이 datum의 소유 시퀀스로 먼저 필터링한다.
            NodeViewModel selectedNode = treeListBox_sequence.SelectedItem as NodeViewModel;
            string ownerSeqName = selectedNode != null ? selectedNode.SequenceName : null;
            if (!string.IsNullOrEmpty(ownerSeqName)) {
                ShotConfig ownedFirst = shots
                    .Where(s => (string.IsNullOrEmpty(s.OwnerSequenceName) ? SequenceHandler.SEQ_TOP : s.OwnerSequenceName) == ownerSeqName)
                    .OrderBy(s => s.ZIndex)
                    .FirstOrDefault();
                if (ownedFirst != null) return ownedFirst;
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

            // 260724 hbk: 신규 Shot은 재시작 전까지 Grab/조명이 안 되는데, 원인은 서로 독립된 두 필드다 —
            //  (a) DeviceName이 비어 있어 DeviceHandler.GrabHalconImage(this[param.DeviceName])가 실패("Device Not Opened").
            //  (b) Parent가 null이라 CameraSlaveParam.SequenceName도 null이 되어, MainView.GrabAndDisplay의
            //      Sequences[param.SequenceName] 조회가 실패해 ApplyShotLightsDirect 자체가 호출되지 않음
            //      (ApplyShotLightsInternal은 shot 자신의 필드 + static LightHandler.Handle만 사용하므로 어떤
            //      InspectionSequence 인스턴스로 호출하든 결과는 같다 — 문제는 그 인스턴스를 못 찾는 것뿐).
            //  즉 "같은 원인 하나"가 아니라 서로 다른 필드에서 비롯된 두 개의 별개 결함이다.
            //  둘 다 재시작 시 SequenceHandler.TryLoadNewFormat → RebuildInspectionActions → SequenceBase.AddAction이
            //  채워주지만, AddAction은 Actions[]/ChildList 전체를 재구성하는 무거운 경로라 Add-Shot 클릭마다 돌리면
            //  StartCore가 _startLock으로 막아둔 것과 같은 종류의 TOCTOU(check-then-act) 노출을 넓힌다.
            //  여기서는 AddAction의 부수효과 중 이 두 필드에만 필요한 부분을, Actions[]/ChildList는 건드리지 않고
            //  UI 스레드에서 직접 재현한다.
            SequenceBase seq = SystemHandler.Handle.Sequences[seqNode.SequenceID];
            if (seq != null) {
                shot.Parent = seq;
                if (string.IsNullOrEmpty(shot.DeviceName) && seq.Param is CameraMasterParam masterParam) {
                    shot.DeviceName = masterParam.DeviceName;
                }
            }

            // 조명 채널(Ring/Bar/Back/Coax/Ring7 Enabled)은 신규 ShotConfig가 bool 기본값(false)이라 전부 꺼진 채로
            //  생성된다 — 같은 시퀀스 소속 마지막 sibling Shot에서 조명/노광/게인/해상도 설정을 이어받아 합리적인
            //  기본값을 준다(ShotConfig.CopyTo — 조명 8그룹 + DeviceName + Exposure/Gain/PixelResolution/CorrectionFactor
            //  복사; ShotName/_image는 제외; FAIList는 CopyTo가 복사하지만 바로 아래 ClearFAIs로 비운다). sibling이
            //  없으면(해당 시퀀스의 첫 Shot) 아무것도 하지 않는다 — DeviceName은 위에서 이미 채워졌으므로 이 경우에도
            //  Grab은 정상 동작한다.
            //  주의: DeviceName 복사는 ShotConfig.CopyTo의 260723 hbk 수정(base.CopyTo보다 먼저 DeviceName부터 설정)에
            //  의존한다 — 그 수정이 별도로 되돌려지면 이 sibling-copy가 DeviceName만 다시 안 채우게 되지만(조명/노광
            //  등 나머지 필드는 영향 없음), 위 Parent/DeviceName 기본값 설정 블록이 있으므로 Grab 자체는 그래도 정상 동작한다.
            ShotConfig siblingShot = seqHandler.RecipeManager.Shots
                .LastOrDefault(s => s != shot
                    && (string.IsNullOrEmpty(s.OwnerSequenceName) ? SequenceHandler.SEQ_TOP : s.OwnerSequenceName) == ownerSeqName);
            siblingShot?.CopyTo(shot);
            // ShotConfig.CopyTo 가 FAIList 까지 깊은 복사하도록 바뀌었다. 여기서 필요한 건 sibling 의
            //  조명/노광 기본값뿐이고 FAI 는 바로 아래에서 새로 만든다 — 딸려온 FAI 를 비운다.
            //  이 줄이 없으면 Shot 추가 시 sibling 의 FAI 전체가 복제된다.
            shot.ClearFAIs();

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
            // RebuildInspectionActions를 호출하지 않아도, 실행 시 Btn_start_Click → ResolveRunnableAction이 지연 동기화로
            //  Actions[]/ChildList 매핑(RUN 실행에만 필요)을 복구한다 — Grab/조명에 필요한 DeviceName/Parent는 위에서 이미 채웠다.
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
