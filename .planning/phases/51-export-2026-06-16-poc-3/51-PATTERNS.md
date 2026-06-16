# Phase 51: 시퀀스 일괄 검사 & 일괄 Export - Pattern Map

**Mapped:** 2026-06-16
**Files analyzed:** 5 new/modified files
**Analogs found:** 5 / 5

---

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|---|---|---|---|---|
| `InspectionListView.xaml` (modify) | component (XAML) | event-driven | `InspectionListView.xaml` 自体 — 트리 + RUN 버튼 추가 | exact (self-extension) |
| `InspectionListView.xaml.cs` (modify) | component (code-behind) | event-driven | `InspectionListView.xaml.cs` 自体 — `Btn_start_Click` 확장 | exact (self-extension) |
| `NodeViewModel.cs` (modify) | viewmodel | transform | `NodeViewModel.cs` 自体 — `IsChecked` 필드 추가 | exact (self-extension) |
| `BatchRunService.cs` (new) | service | batch / event-driven | `RepeatRunService.cs` — Start/OnFinish/HandleFinish/누적 패턴 완전 일치 | exact |
| `InspectionListView.xaml` 내 Export 버튼 (XAML 수정) | component (XAML) | request-response | `ReviewerWindow.xaml.cs` Button_ExportExcel_Click (L284-317) | role-match |

---

## Pattern Assignments

### `BatchRunService.cs` (service, batch/event-driven)

**Analog:** `WPF_Example/Custom/Sequence/Inspection/RepeatRunService.cs`

이 파일이 Phase 51 의 핵심 신규 파일이다. RepeatRunService 와 동일한 Start → OnFinish → HandleFinish → 누적(_collected) 패턴을 써야 하며, 차이는 "N회 반복" 대신 "선택된 Shot 인덱스 집합을 1사이클 실행"이다.

**Imports pattern** (lines 1-9):
```csharp
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ReringProject.Network;
using ReringProject.Setting;
using ReringProject.UI;
using ReringProject.Utility;

namespace ReringProject.Sequence
```

**서비스 소유권 선언 패턴** (RepeatRunService.cs lines 17-34):
```csharp
/// <summary>
/// InspectionSequence 를 지정 횟수만큼 자동 반복 실행하고 결과를 누적한다.
/// UI 레이어가 인스턴스를 소유한다 (static 금지 — 다중 동시 실행 방지 용이).
/// </summary>
public class RepeatRunService
{
    public event Action<List<CycleResultDto>> OnRepeatComplete;
    public event Action<int, int> OnProgressChanged;

    public bool IsRunning { get; private set; }
    public int CompletedCount { get; private set; }
    public int TargetCount { get; private set; }

    private InspectionSequence _seq;
    private List<CycleResultDto> _collected;
    private EventSequenceStateChanged _onFinishHandler;
    private readonly object _lock = new object();
}
```

**Start 진입/중복 방지 패턴** (RepeatRunService.cs lines 42-64):
```csharp
public void Start(InspectionSequence seq, int targetCount = DEFAULT_REPEAT_COUNT)
{
    if (IsRunning)
    {
        return;
    }
    if (seq == null)
    {
        return;
    }

    IsRunning = true;
    _seq = seq;
    TargetCount = targetCount;
    CompletedCount = 0;
    _collected = new List<CycleResultDto>();

    _onFinishHandler = (ctx) => HandleFinish(ctx);
    _seq.OnFinish += _onFinishHandler;

    TriggerNext();
}
```

**BatchRunService 용 새 진입점 — selectedShotIndices 매개변수 추가 (analog 변형):**

RepeatRunService.Start() 와 동일한 보호/등록 패턴에, 추가로 `List<int> selectedShotIndices`(실행할 로컬 Shot 인덱스 목록)를 받아 저장한다. TriggerNext()에서 `StartSubset(selectedShotIndices)` 호출로 전체 StartAll 대신 부분 실행 경로를 쓴다.

```csharp
// Phase 51: 선택된 Shot 인덱스 집합으로 1사이클 일괄 검사 시작
// analog = RepeatRunService.Start 와 동일 구조, targetCount=1 고정
public void StartBatch(InspectionSequence seq, List<int> selectedShotIndices)
{
    if (IsRunning) return;
    if (seq == null || selectedShotIndices == null || selectedShotIndices.Count == 0) return;

    IsRunning = true;
    _seq = seq;
    _selectedIndices = selectedShotIndices;   // new field
    TargetCount = 1;   // 1사이클 수동 모드 — 실모드 append는 외부에서 반복 호출
    CompletedCount = 0;
    _collected = new List<CycleResultDto>();

    _onFinishHandler = (ctx) => HandleFinish(ctx);
    _seq.OnFinish += _onFinishHandler;

    TriggerNext();
}
```

**HandleFinish — BuildDto + 누적 패턴** (RepeatRunService.cs lines 153-224):
```csharp
private void HandleFinish(SequenceContext ctx)
{
    lock (_lock)
    {
        var seqHandler = SystemHandler.Handle.Sequences;
        if (seqHandler == null) return;
        var recipeManager = seqHandler.RecipeManager;
        if (recipeManager == null) return;

        bool anySkip = false;
        bool allPass = true;
        foreach (var shot in recipeManager.Shots)
        {
            foreach (var fai in shot.FAIList)
            {
                if (fai.WasDatumSkipped) anySkip = true;
                else if (!fai.IsPass) allPass = false;
            }
        }

        EVisionResultType resultType;
        if (anySkip)       resultType = EVisionResultType.NotExist;
        else if (!allPass) resultType = EVisionResultType.NG;
        else               resultType = EVisionResultType.OK;

        string recipeName = SystemHandler.Handle.Setting.CurrentRecipeName;
        string seqName = _seq != null ? _seq.Name : null;
        CycleResultDto dto = CycleResultSerializer.BuildDto(
            recipeManager, resultType, DateTime.Now, recipeName, seqName);

        CycleResultSerializer.SaveAsync(dto);   // 비동기 영속화 (InspectionSequence.HandleManualCyclePersist 와 중복 주의)
        _collected.Add(dto);
        CompletedCount++;

        OnProgressChanged?.Invoke(CompletedCount, TargetCount);

        if (CompletedCount >= TargetCount)
        {
            var finalList = new List<CycleResultDto>(_collected);
            Stop();
            OnRepeatComplete?.Invoke(finalList);
        }
        else
        {
            TriggerNext();
        }
    }
}
```

**중복 저장 주의:** `InspectionSequence.HandleManualCyclePersist`(InspectionSequence.cs line 153)는 RequestPacket==null 수동 실행 시 이미 `CycleResultSerializer.SaveAsync` 를 호출한다. BatchRunService.HandleFinish 에서도 SaveAsync 를 호출하면 중복 저장이 발생한다. RepeatRunService.cs line 206 주석("기존 경로 영속화 유지…수동 경로이므로 OnFinish 가 1회 발화")과 동일하게 Phase 51 의 BatchRunService 는 SaveAsync 호출을 생략하거나 중복 방지 조건을 추가해야 한다.

**TriggerNext — Dispatcher.Background + State 체크 패턴** (RepeatRunService.cs lines 227-264):
```csharp
private void TriggerNext()
{
    if (!IsRunning || _seq == null) return;

    if (_seq.State == EContextState.Idle)
    {
        System.Windows.Application.Current.Dispatcher.BeginInvoke(
            System.Windows.Threading.DispatcherPriority.Background,
            new Action(() =>
            {
                if (!IsRunning || _seq == null) return;
                if (_seq.State == EContextState.Idle)
                {
                    // BatchRunService: StartAll 대신 StartSubset(_selectedIndices) 호출
                    _seq.StartAll(null);
                }
                else
                {
                    Task.Delay(50).ContinueWith(_ => TriggerNext());
                }
            }));
    }
    else
    {
        Task.Delay(50).ContinueWith(_ => TriggerNext());
    }
}
```

**Stop 패턴** (RepeatRunService.cs lines 102-114):
```csharp
public void Stop()
{
    if (_seq != null && _onFinishHandler != null)
    {
        _seq.OnFinish -= _onFinishHandler;
    }
    IsRunning = false;
    _onFinishHandler = null;
    _seq = null;
}
```

---

### `SequenceBase.cs` 에 `StartSubset(int[] indices)` 추가 (SequenceBase, batch)

**Analog:** `WPF_Example/Sequence/Sequence/SequenceBase.cs` — `StartAll` 메서드 (lines 341-359)

`StartAll` 은 `CurrentActionIndex=0`, `EndActionIndex=Actions.Length-1` 로 전체를 실행한다. 선택 SHOT 부분 실행을 위해 `StartSubset(int[] actionIndices)` 오버로드를 추가해야 한다. 구현 전략 두 가지 중 플래너가 선택한다:

**옵션 A — StartSubset(연속 범위):** 선택 인덱스를 정렬 후 첫/끝 인덱스로 CurrentActionIndex/EndActionIndex 설정. 중간 누락 인덱스가 있으면 불가.

**옵션 B — 마스크 필터:** Actions 배열 앞에 선택 인덱스 집합을 저장하고 ExecuteAction 후 다음 인덱스 진행 시 필터를 거친다.

**StartAll 패턴 (복사 기준)** (SequenceBase.cs lines 342-359):
```csharp
//260409 hbk Phase 5: 모든 Action 순차 실행 (D-01)
public bool StartAll(TestPacket packet) {
    if (State != EContextState.Idle) return false;
    if (Actions == null || Actions.Length == 0) return false;

    RequestPacket = packet;
    CurrentActionIndex = 0;
    EndActionIndex = Actions.Length - 1;

    Context.Clear();
    IsFinished = false;

    if (OnStart != null) {
        OnStart.Invoke(Context);
    }
    Command = ESequenceCommmand.Start;
    return true;
}
```

**StartSubset 추가 패턴 (Phase 51 신규):**
```csharp
//260616 hbk Phase 51: 선택 Action 인덱스 집합으로 부분 실행 (D-01 SHOT 다중 선택)
public bool StartSubset(int[] actionIndices, TestPacket packet = null) {
    if (State != EContextState.Idle) return false;
    if (Actions == null || Actions.Length == 0) return false;
    if (actionIndices == null || actionIndices.Length == 0) return false;

    // 연속 범위 전략: 정렬 후 첫/끝으로 CurrentActionIndex/EndActionIndex 설정
    // 중간 누락 Shot 처리가 필요하면 마스크 필터 전략으로 교체
    Array.Sort(actionIndices);
    int first = actionIndices[0];
    int last  = actionIndices[actionIndices.Length - 1];
    if (first < 0 || last >= Actions.Length) return false;

    RequestPacket = packet;
    CurrentActionIndex = first;
    EndActionIndex = last;

    Context.Clear();
    IsFinished = false;

    if (OnStart != null) {
        OnStart.Invoke(Context);
    }
    Command = ESequenceCommmand.Start;
    return true;
}
```

---

### `InspectionListView.xaml` (component XAML, 기존 파일 수정)

**Analog:** `WPF_Example/UI/ControlItem/InspectionListView.xaml` 자체 — 기존 btn_start 버튼과 treeListBox_sequence 패턴 재사용

**현재 트리 컨트롤** (InspectionListView.xaml lines 172-174):
```xml
<propToolsWpf:TreeListBox x:Name="treeListBox_sequence"
    Grid.Column="0" Grid.Row="1"
    HierarchySource="{Binding Root}" Indentation="12"
    AllowDrop="False"
    SelectionChanged="InspectionList_SelectionChanged">
</propToolsWpf:TreeListBox>
```

TreeListBox 는 현재 `SelectionMode` 속성이 명시되지 않았다(단일 선택). `PropertyTools.Wpf.TreeListBox` 가 `SelectionMode.Extended` 를 지원하는지 확인이 필요하다. 지원한다면 XAML에 `SelectionMode="Extended"` 추가로 Ctrl/Shift 다중 선택을 활성화할 수 있다.

**지원 여부 불확실 시 대안 — NodeViewModel.IsChecked 체크박스 패턴:**
NodeViewModel 에 `bool IsChecked` 프로퍼티를 추가하고, DataTemplate 에 `CheckBox IsChecked={Binding IsChecked}` 를 삽입하여 SHOT 노드에만 체크박스를 표시한다.

**기존 DataTemplate** (InspectionListView.xaml lines 46-60):
```xml
<DataTemplate DataType="{x:Type local:NodeViewModel}">
    <Grid>
        <Grid.ColumnDefinitions>
            <ColumnDefinition Width="Auto"/>
            <ColumnDefinition Width="*"/>
            <ColumnDefinition Width="Auto"/>
        </Grid.ColumnDefinitions>
        <Path Grid.Column="0" Width="24" Height="24" Margin="0 0 4 0"
              Stretch="Uniform"
              Fill="{Binding Path=(TextElement.Foreground), RelativeSource={RelativeSource Self}}"
              Data="{Binding IconKey, Converter={StaticResource iconKeyToGeometry}}"/>
        <propToolsWpf:EditableTextBlock Grid.Column="1" Text="{Binding Name}"
              IsEditing="{Binding IsEditing}"/>
    </Grid>
</DataTemplate>
```

체크박스 추가 시 Grid.Column="2" 에 `CheckBox Visibility="{Binding IsCheckboxVisible}"` 를 삽입하고, NodeViewModel 에 `IsCheckboxVisible`(SHOT 노드만 true), `IsChecked` 프로퍼티를 추가한다.

**"일괄 검사" 버튼 추가 위치** — 기존 btn_start 옆 (InspectionListView.xaml lines 160-165):
```xml
<Button x:Name="btn_start" Grid.Column="1" Grid.Row="0" Margin="0,2,0,2" FontSize="18" Click="Btn_start_Click">
    <StackPanel Orientation="Horizontal" HorizontalAlignment="Left">
        <Image Source="/resource/process.png"></Image>
        <TextBlock Text="RUN"></TextBlock>
    </StackPanel>
</Button>
```

Phase 51 에서는 이 버튼 옆에 `btn_batchRun`(일괄 검사)과 `btn_batchExport`(누적 엑셀) 버튼을 추가한다. 레이아웃은 기존 btn_RecipeSelect 와 동일한 방식(Grid Column 확장 또는 StackPanel 추가).

---

### `InspectionListView.xaml.cs` (component code-behind, 기존 파일 수정)

**Analog:** `WPF_Example/UI/ControlItem/InspectionListView.xaml.cs` 자체 — `Btn_start_Click` + `ResolveRunnableAction` + `ComputeLocalShotIndex` 패턴 재사용

**현재 단일 실행 진입점** (InspectionListView.xaml.cs lines 323-364):
```csharp
private void Btn_start_Click(object sender, RoutedEventArgs e) {
    if (treeListBox_sequence.SelectedIndex < 0) return;
    if (!(treeListBox_sequence.SelectedItem is NodeViewModel node)) return;

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
    ...
}
```

**로컬 Shot 인덱스 계산 패턴** (InspectionListView.xaml.cs lines 419-435):
```csharp
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
```

**Phase 51 신규 `Btn_batchRun_Click` 패턴 (이 메서드들을 직접 재사용):**

1. 체크된 SHOT 노드를 수집 (`treeListBox_sequence.Items` 순회 + `node.IsChecked == true && node.NodeType == ENodeType.Action && node.Param is ShotConfig`)
2. 동일 시퀀스 소속 검증 (D-02: 모든 체크 노드의 SequenceID 동일 확인)
3. 각 SHOT 에 대해 `ComputeLocalShotIndex` 로 로컬 인덱스 수집
4. `InspectionSequence seq = SystemHandler.Handle.Sequences[seqID] as InspectionSequence`
5. `_batchService = new BatchRunService(); _batchService.StartBatch(seq, selectedIndices)`

**누적 결과 보관 + Export 버튼 활성 패턴** (ReviewerWindow.xaml.cs lines 385-409 참조):
```csharp
// Phase 51: InspectionListView 의 _batchAccumulated 필드
private List<CycleResultDto> _batchAccumulated = new List<CycleResultDto>();
private BatchRunService _batchService;

// OnRepeatComplete 핸들러에서 누적 append
_batchService.OnRepeatComplete += (cycles) =>
{
    Dispatcher.Invoke(() =>
    {
        if (cycles != null)
        {
            _batchAccumulated.AddRange(cycles);
        }
        btn_batchExport.IsEnabled = (_batchAccumulated.Count > 0);
    });
};
```

---

### `NodeViewModel.cs` (viewmodel, 기존 파일 수정)

**Analog:** `WPF_Example/UI/ControlItem/NodeViewModel.cs` 자체 — `IsSelected`, `IsExpanded`, `IsEditing` 프로퍼티 패턴 재사용

**기존 bool 프로퍼티 패턴** (NodeViewModel.cs lines 170-182):
```csharp
private bool isSelected;

public bool IsSelected {
    get {
        return this.isSelected;
    }
    set {
        if (isSelected == value) return;
        this.isSelected = value;
        RaisePropertyChanged("IsSelected");
    }
}
```

**Phase 51 추가 프로퍼티 — 동일 패턴으로 복사:**
```csharp
//260616 hbk Phase 51: SHOT 다중 선택용 체크박스 상태
private bool _isChecked;

public bool IsChecked {
    get { return _isChecked; }
    set {
        if (_isChecked == value) return;
        _isChecked = value;
        RaisePropertyChanged("IsChecked");
    }
}

// SHOT 노드(Action + ShotConfig param)만 체크박스 노출
public bool IsCheckboxVisible {
    get { return NodeType == ENodeType.Action && Param is ShotConfig; }
}
```

---

### 수동 Export 버튼 (InspectionListView.xaml.cs 내 핸들러)

**Analog:** `WPF_Example/UI/Reviewer/ReviewerWindow.xaml.cs` — `Button_ExportExcel_Click` (lines 284-317)

```csharp
private void Button_ExportExcel_Click(object sender, RoutedEventArgs e)
{
    if (_currentCycle == null)
    {
        CustomMessageBox.Show("엑셀 export", "먼저 cycle 을 선택하세요.", MessageBoxImage.Warning);
        return;
    }

    string initialDir;
    if (!string.IsNullOrEmpty(_currentCycle.CycleFolderPath) && Directory.Exists(_currentCycle.CycleFolderPath))
        initialDir = _currentCycle.CycleFolderPath;
    else
        initialDir = SystemHandler.Handle.Setting.ResultSavePath;

    var dlg = new Microsoft.Win32.SaveFileDialog
    {
        Filter = "Excel 파일 (*.xlsx)|*.xlsx",
        FileName = "result_" + _currentCycle.InspectionTime.ToString("yyyyMMdd_HHmmss") + ".xlsx",
        InitialDirectory = initialDir
    };

    if (dlg.ShowDialog() == true)
    {
        bool ok = ExcelExportService.Export(_currentCycle, dlg.FileName);
        string okMessage;
        if (ok) okMessage = "저장 완료:\n" + dlg.FileName; else okMessage = "export 실패 (로그 확인)";
        MessageBoxImage okIcon;
        if (ok) okIcon = MessageBoxImage.Information; else okIcon = MessageBoxImage.Error;
        CustomMessageBox.Show("엑셀 export", okMessage, okIcon);
    }
}
```

**Phase 51 의 `Btn_batchExport_Click` 차이점:**
- `_currentCycle` 대신 `_batchAccumulated` (List) 를 순회
- 복수 CycleResultDto → 단일 xlsx: `ExcelExportService.Export` 는 1개 cycle만 지원하므로 시트 분리(한 시트에 이어 쓰기 또는 다중 시트) 전략이 필요하다. D-06("기존 포맷 그대로") 에 따라 가장 단순한 방법은 첫 번째 누적 dto를 merge하거나 `RepeatExcelExportService.Export(_batchAccumulated, ...)` 패턴을 재사용한다.
- `RepeatExcelExportService`(이미 존재, ReviewerWindow.xaml.cs line 433 참조)가 `List<CycleResultDto>` → xlsx 를 지원하는지 확인 필요 — 지원하면 그대로 재사용.

---

## Shared Patterns

### 이중 저장 방지 (중요)
**Source:** `WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs` lines 153-171 (`HandleManualCyclePersist`) + `RepeatRunService.cs` line 206 주석
**Apply to:** `BatchRunService.HandleFinish`

`InspectionSequence` 는 `OnFinish` 구독으로 수동 경로(`RequestPacket==null`)에서 이미 `CycleResultSerializer.SaveAsync`를 호출한다. `BatchRunService.HandleFinish` 에서 추가로 SaveAsync 를 호출하면 같은 사이클이 두 번 저장된다. `RepeatRunService` 는 이 점을 인지하고 주석으로만 경고했다(실제 중복 발생 가능성). Phase 51 에서는 BatchRunService 가 SaveAsync 를 호출하지 않고 InspectionSequence 의 HandleManualCyclePersist 에 위임하거나, OnFinish 구독 순서를 보장하여 BatchRunService 가 먼저 dto를 빌드하고 저장은 위임하는 방식을 택한다.

### Dispatcher.BeginInvoke(Background) 안전 실행 패턴
**Source:** `WPF_Example/Custom/Sequence/Inspection/RepeatRunService.cs` lines 239-257
**Apply to:** `BatchRunService.TriggerNext`, `InspectionListView.xaml.cs` OnRepeatComplete 핸들러

UI 스레드에서 시퀀스를 시작하되, `Background` 우선순위로 지연하여 이전 OnFinish 핸들러(Normal 우선순위 이미지 표시 등)가 먼저 완료되도록 한다.

### IsIdle 사전 검사
**Source:** `InspectionListView.xaml.cs` lines 342-345
**Apply to:** `Btn_batchRun_Click`

```csharp
if (!SystemHandler.Handle.Sequences.IsIdle) {
    CustomMessageBox.Show("Error", "Sequence is already running.", MessageBoxImage.Error);
    return;
}
```

### K&R vs Allman 브레이스 스타일
**Apply to:** 모든 수정 파일

- `InspectionListView.xaml.cs` — K&R (기존 스타일 유지)
- `NodeViewModel.cs` — K&R (기존 스타일 유지)
- `RepeatRunService.cs` / `BatchRunService.cs` — Allman (신규 서비스 파일 스타일)
- `SequenceBase.cs` — K&R (기존 스타일 유지)

### //YYMMDD hbk 주석 의무
**Apply to:** 모든 추가/수정 코드 라인
```csharp
//260616 hbk Phase 51: [변경 내용 설명]
```

---

## 핵심 설계 질문 — 플래너가 결정해야 할 항목

| 질문 | 선택지 | 근거 패턴 |
|---|---|---|
| 다중 선택 UI 방식 | (A) TreeListBox SelectionMode="Extended" (Ctrl/Shift) vs (B) NodeViewModel.IsChecked 체크박스 | TreeListBox XAML 에 SelectionMode 미지정 → PropertyTools 지원 여부 확인 필요 |
| StartSubset 구현 전략 | (A) 연속 범위(CurrentActionIndex/EndActionIndex) vs (B) 불연속 마스크(_selectedIndices 필터) | SequenceBase.StartAll lines 347-348 기반; 불연속 SHOT 선택 시 B 필요 |
| 다중 Dto → xlsx | (A) 기존 ExcelExportService 순차 Export(시트 여러 개) vs (B) RepeatExcelExportService 재사용 | ReviewerWindow.xaml.cs line 433 — RepeatExcelExportService.Export(List, ...) 이미 존재 |
| Export 버튼 위치 | (A) InspectionListView 내 신규 btn_batchExport vs (B) ReviewerWindow 재사용 | D-05: 수동 Export; InspectionListView 가 일괄 검사 트리거 소유하므로 A 권장 |

---

## No Analog Found

없음. 모든 파일에 대해 기존 코드베이스 내 충분한 analog가 확인되었다.

---

## Metadata

**Analog search scope:** `WPF_Example/Custom/Sequence/Inspection/`, `WPF_Example/UI/ControlItem/`, `WPF_Example/UI/Reviewer/`, `WPF_Example/Sequence/Sequence/`, `WPF_Example/Custom/`
**Files scanned:** 9
**Pattern extraction date:** 2026-06-16
