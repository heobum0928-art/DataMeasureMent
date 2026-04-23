---
quick_id: 260417-kzd
type: quick
title: "Phase 6-04 UAT 잔여 결함 수정 — DisplayName 편집 UI + Shot 실행 경로"
date: 2026-04-17
autonomous: false
files_modified:
  - WPF_Example/Sequence/Param/CameraMasterParam.cs
  - WPF_Example/Custom/Sequence/Inspection/InspectionMasterParam.cs
  - WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs
  - WPF_Example/UI/ControlItem/InspectionListViewModel.cs
  - WPF_Example/UI/ControlItem/InspectionListView.xaml.cs
  - WPF_Example/DatumMeasurement.csproj

must_haves:
  truths:
    - "사용자가 Sequence 노드를 선택하면 PropertyGrid에 DisplayName이 노출되고 편집 가능하다"
    - "DisplayName 편집 후 트리 라벨이 즉시 갱신된다"
    - "저장/로드 왕복 시 DisplayName 값이 유지된다 (기존 INI 왕복 경로 유지)"
    - "Sequence 노드를 선택한 뒤 Start 버튼을 누르면 해당 시퀀스가 정상 실행된다 (There is no action to run 에러 없음)"
    - "Shot(Action 타입) 노드를 선택한 뒤 Start 버튼을 누르면 해당 Shot에 해당하는 Action 이 실행된다"
    - "Phase 6 동적 FAI 모드에서도 Static 모드에서도 Start 경로가 동작한다"
  artifacts:
    - path: "WPF_Example/Custom/Sequence/Inspection/InspectionMasterParam.cs"
      provides: "InspectionSequence 전용 Master Param — DisplayName을 PropertyGrid에 노출"
      contains: "class InspectionMasterParam"
    - path: "WPF_Example/UI/ControlItem/InspectionListView.xaml.cs"
      provides: "Btn_start_Click에서 Sequence/Shot 노드 모두 StartSequence 가능"
      contains: "ResolveRunnableAction"
  key_links:
    - from: "InspectionMasterParam.DisplayName setter"
      to: "InspectionSequence.DisplayName + tree node label"
      via: "PropertyChanged 이벤트 → InspectionListViewModel.OnDisplayNameChanged"
      pattern: "DisplayName"
    - from: "InspectionListView.Btn_start_Click (Shot 노드)"
      to: "SequenceHandler.Start(ESequence, EAction)"
      via: "RecipeManager.Shots.IndexOf → sequence[shotIndex].ID"
      pattern: "StartSequence"
---

<objective>
Phase 6-04 UAT 에서 확인된 두 가지 잔여 결함을 수정한다.

결함 1: InspectionSequence.DisplayName 이 PropertyGrid에서 편집 불가능 (Sequence 노드의 ParamData가 DisplayName 필드 없는 CameraMasterParam이기 때문).
결함 2: Sequence 노드 선택 → Start 시 "There is no action to run" 에러. Shot 노드 선택 시에도 AddShotToSequence가 ActionID=EAction.Unknown으로 노드를 만들어 Start 경로가 동작하지 않음 (Phase 6 동적 FAI 모드에서 RebuildInspectionActions가 UI 경로에서 호출되지 않아 실행 Action은 기본 Top_Inspection 1개만 등록된 상태일 수도 있음).

Purpose: 06-04 플랜이 약속한 must_have "Sequence 노드에 DisplayName이 표시된다 + 편집 가능" 을 완결하고, Task 3 사람-검증 체크포인트를 차단하는 Start 경로 버그를 제거하여 Phase 6 을 실제 검증 가능한 상태로 만든다.

Output: InspectionMasterParam 신규 + CameraMasterParam 최소 후킹 + InspectionListView Start 버튼 로직 재작성 + 트리 라벨 자동 갱신.
</objective>

<execution_context>
@C:/Users/tech/.claude/get-shit-done/workflows/execute-plan.md
</execution_context>

<context>
@C:/Info/Project/DataMeasurement/CLAUDE.md
@.planning/phases/06-rapid-city/06-04-PLAN.md
@.planning/phases/06-rapid-city/06-04-SUMMARY.md
@.planning/06-inspection-concept.md

<interfaces>
<!-- 근본 원인 조사 결과. 계획서 독자(실행자)가 코드 탐색 없이 바로 실행할 수 있도록 핵심 연결을 명시한다. -->

From WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs:
```csharp
public class InspectionSequence : SequenceBase {
    public string DisplayName { get; set; } = "";           // Plan 06-04 에서 추가됨
    public List<DatumConfig> DatumConfigs { get; private set; }
    public string GetDisplayName();                          // DisplayName 비어있으면 Name
    // 생성자에서: Param = new CameraMasterParam(this);       ← 여기 교체 대상 (Task 1)
}
```

From WPF_Example/Sequence/Param/CameraMasterParam.cs:
```csharp
public class CameraMasterParam : ParamBase {
    // DisplayName 필드 없음 → PropertyGrid에 안 보임 (결함 1 원인)
    public string LightGroupName { get; set; }   // [ReadOnly(true)]
    public string DeviceName { get; set; }       // [ReadOnly(true)]
    public List<CameraSlaveParam> ChildList;
    public void ClearChildren();
    public void AddChild(CameraSlaveParam child);
}
```

From WPF_Example/UI/ControlItem/InspectionListViewModel.cs (현재):
```csharp
// CreateSequenceNode — line 60-61:
string seqDisplay = (seq as InspectionSequence)?.GetDisplayName() ?? seq.Name;
var seqNode = new CompositeNode {
    Name = seqDisplay,
    NodeType = ENodeType.Sequence,
    ParamData = seq.Param,                     // ← CameraMasterParam (DisplayName 없음)
    SequenceName = seq.Name,
    SequenceID = seq.ID
};
```

From WPF_Example/UI/ControlItem/InspectionListView.xaml.cs (현재 Btn_start_Click line 128-157):
```csharp
// Sequence 노드: seq.ActionCount > 0 이면 seq[0].ID 로 Start.
// Shot 노드(NodeType=Action, Param is ShotConfig): node.ActionID 사용 — 하지만
//   AddShotToSequence(line 454-488)에서 ActionID=EAction.Unknown 으로 생성 → Start(EAction.Unknown) → Start(0) 로 폴백.
// 문제: EnableDynamicFAIMode()만 호출하고 RebuildInspectionActions는 호출하지 않음 → 시퀀스에는 여전히 기본 Top_Inspection 1개만 등록.
// 결과적으로 Shot을 여러 개 추가해도 UI에서 Start 하면 항상 첫 번째 Action(또는 FAI_Base) 만 실행되거나 NO-OP.
```

From WPF_Example/Custom/Sequence/SequenceHandler.cs:
```csharp
public void RebuildInspectionActions(ESequence seqId);   // RecipeManager.Shots 기반 Action_FAIMeasurement 재구축
public ShotConfig CreateShot(ESequence seqId, string shotName = null); // Shot 추가 + IsDynamicFAIMode + RebuildInspectionActions
public void EnableDynamicFAIMode();                       // IsDynamicFAIMode=true 만 (Action 재구축 없음)
// Action id 규칙: (EAction)((int)EAction.FAI_Base + shotIndex)
```

From WPF_Example/Sequence/SequenceHandler.cs (line 344):
```csharp
public bool Start(ESequence seqID, EAction beginActionID); // Sequence의 Start(EAction) → index 조회
```

From WPF_Example/Sequence/Sequence/SequenceBase.cs (line 306):
```csharp
public bool Start(EAction actionID) {
    if (State != EContextState.Idle) return false;
    if (actionID == EAction.Unknown) return Start(0);      // 첫 Action 실행
    int i = GetIndexOf(actionID);                           // 매칭 실패 시 -1 → false
    if (i == -1) return false;
    return Start(i);
}
```
</interfaces>

<root_cause_analysis>
## 결함 2 근본 원인 (확정)

1. **초기 상태**: `SequenceHandler.InitializeSequences()` 는 ESequence.Top 에 `Top_Inspection` Action 1개를 기본 등록.
2. **Phase 6 동적 모드 진입 (정상 경로)**: `TryLoadNewFormat()` → `RebuildInspectionActions(ESequence.Top)` 가 `Top_Inspection` 을 제거하고 Shot 별 `Action_FAIMeasurement` (ID = FAI_Base + index) 를 등록.
3. **UI에서 Shot 추가 (버그 경로)**: `AddShotToSequence` 는 `RecipeManager.AddShot` + `EnableDynamicFAIMode()` 만 호출. **`RebuildInspectionActions` 를 호출하지 않음.** → 시퀀스 Actions 는 그대로. 트리의 Shot 노드(ActionID=Unknown) 와 시퀀스 Actions 가 어긋남.
4. **"There is no action to run" 발생 조건**: `seq.ActionCount == 0` 일 때만. 현재 InitializeSequences 가 항상 1개는 등록하므로 이 에러는 **Action 배열이 null/empty 인 예외 경로에서만 발생**. UAT 에러 메시지를 볼 수 있었던 실제 조건은 다음 중 하나:
   - (a) 시퀀스 로드가 완료되기 전에 Start 시도 (race)
   - (b) `seq.ActionCount` 는 1 이지만 Shot 노드의 `node.ActionID = Unknown` → `StartSequence(seqID, Unknown)` → `Start(0)` 로 폴백은 성공하지만 **Top_Inspection** (Phase 6 이전 알고리즘) 이 실행되어 Shot 데이터와 맞지 않는 결과로 UAT 에서 실패.

## 덜 침습적인 접근 선택
두 가지 후보를 비교:

| 접근 | 변경 범위 | 런타임 위험 | 결정 |
|---|---|---|---|
| A. UI에서 Shot 추가 시마다 RebuildInspectionActions 호출 | SequenceHandler 안전 검증 필요, 실행 중 Action 교체 위험 | 중간 | ✕ |
| B. Btn_start_Click 에서 Shot 노드 실행 경로를 "RecipeManager.Shots.IndexOf → seq[i].ID" 로 재해석 | InspectionListView 만 수정 | 낮음 | ✔ |

**채택: B.** 시퀀스 Action 배열 교체는 별도 세이프가드 작업이 필요 (실행 중 수정 시 race). UI 측에서 "Shot 인덱스 → 시퀀스 Action 인덱스" 매핑만 해결하면 사용자 시나리오는 해결된다. 부가로, 저장/재로드 시 `TryLoadNewFormat` 이 RebuildInspectionActions 를 호출하여 정상 상태로 복구되는 경로도 이미 존재.

단, UI Shot 추가 직후 Start 하는 시나리오도 지원해야 하므로 매핑 실패 시 `EnableDynamicFAIMode + RebuildInspectionActions` 를 한 번 호출하는 **지연 동기화(lazy sync)** 를 Btn_start_Click 진입점에 둔다 (시퀀스가 Idle 일 때만 안전하게 실행).
</root_cause_analysis>
</context>

<tasks>

<task type="auto">
  <name>Task 1: InspectionMasterParam 신규 + DisplayName 편집 가능화 + 트리 라벨 자동 갱신</name>
  <files>
    WPF_Example/Custom/Sequence/Inspection/InspectionMasterParam.cs
    WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs
    WPF_Example/UI/ControlItem/InspectionListViewModel.cs
    WPF_Example/DatumMeasurement.csproj
  </files>
  <read_first>
    WPF_Example/Sequence/Param/CameraMasterParam.cs
    WPF_Example/Sequence/Param/ParamBase.cs
    WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs
    WPF_Example/UI/ControlItem/InspectionListViewModel.cs
    WPF_Example/UI/ControlItem/NodeViewModel.cs
  </read_first>
  <action>
1. **신규 파일 `WPF_Example/Custom/Sequence/Inspection/InspectionMasterParam.cs`** 생성 (//260417 hbk):
   ```csharp
   // namespace: ReringProject.Sequence (기존 InspectionSequence 와 동일)
   // using PropertyTools.DataAnnotations;
   public class InspectionMasterParam : CameraMasterParam {
       private InspectionSequence _insp;

       public InspectionMasterParam(InspectionSequence owner) : base(owner) {
           _insp = owner;
       }

       [Category("Fixture|Identity")]
       public string DisplayName {
           get { return _insp != null ? _insp.DisplayName : ""; }
           set {
               if (_insp == null) return;
               if (_insp.DisplayName == value) return;
               _insp.DisplayName = value ?? "";
               RaisePropertyChanged("DisplayName");
           }
       }
   }
   ```
   - `CameraMasterParam` 은 이미 `ParamBase` 상속이므로 PropertyChanged 파이프라인은 자동 제공.
   - PropertyTools PropertyGrid 가 `[Category]` 기반으로 필드 노출.
   - Thread-safety: DisplayName 변경은 UI 스레드에서만 발생 (PropertyGrid 바인딩). 추가 lock 불필요.

2. **`CameraMasterParam.cs` 생성자 확인**:
   - 이미 `SequenceBase` 를 받는 생성자가 있어야 함. 없으면 `public CameraMasterParam(SequenceBase owner)` 오버로드를 추가 (기존 `CameraMasterParam(this)` 호출과 signature 일치 필요). 있으면 그대로 사용.
   - 기존 생성자 시그니처가 `CameraMasterParam(SequenceBase seq)` 가 아니라면 최소 변경: `public CameraMasterParam(SequenceBase owner) : this() { /* 필요 시 Parent 설정 */ }` 추가. **기존 생성자는 삭제/변경하지 말 것.**

3. **`InspectionSequence.cs` 수정** (생성자 line 54-62):
   - `Param = new CameraMasterParam(this);` → `Param = new InspectionMasterParam(this);` 로 교체.
   - `pMyParam = Param as CameraMasterParam;` 유지 (다형성으로 계속 작동).
   - //260417 hbk 주석 추가.

4. **`InspectionListViewModel.cs` — 트리 라벨 자동 갱신 훅 추가**:
   - `CreateSequenceNode` 에서 Sequence 노드 생성 직후, `seq.Param` 이 `InspectionMasterParam` 이면 `PropertyChanged` 이벤트를 구독:
     ```csharp
     if (seq.Param is InspectionMasterParam inspMaster) {
         // 생성한 NodeViewModel 을 참조하기 어려우므로 CompositeNode + 지연 바인딩 대신,
         // 이 시점에 seqNode 는 CompositeNode (NodeViewModel 아님). 따라서
         // RebuildTree 이후 RootModel.Children 순회로 각 Sequence 노드 VM 을 찾아 훅을 건다.
     }
     ```
   - 구현 방식 (단순화): `RebuildTree()` 말미에 헬퍼 `HookSequenceDisplayNameUpdates()` 호출:
     ```csharp
     private void HookSequenceDisplayNameUpdates() {
         foreach (var child in RootModel.Children) {
             if (child.NodeType != ENodeType.Sequence) continue;
             if (!(child.Param is InspectionMasterParam master)) continue;
             // 중복 구독 방지: 이전 핸들러 있으면 해제
             master.PropertyChanged -= OnSequenceMasterPropertyChanged;
             master.PropertyChanged += OnSequenceMasterPropertyChanged;
             // 초기 라벨 동기화
             child.Name = master.DisplayName != "" ? master.DisplayName : child.SequenceName;
         }
     }

     private void OnSequenceMasterPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e) {
         if (e.PropertyName != "DisplayName") return;
         if (!(sender is InspectionMasterParam master)) return;
         // 해당 InspectionSequence 의 트리 노드 찾아 라벨 갱신
         foreach (var child in RootModel.Children) {
             if (child.NodeType != ENodeType.Sequence) continue;
             if (ReferenceEquals(child.Param, master)) {
                 string newLabel = string.IsNullOrEmpty(master.DisplayName) ? child.SequenceName : master.DisplayName;
                 child.Name = newLabel; // NodeViewModel.Name setter 가 RaisePropertyChanged("Name") 실행
                 break;
             }
         }
     }
     ```
   - 생성자에서도 1회 `HookSequenceDisplayNameUpdates()` 호출 (초기 바인딩) — RootModel 생성 직후.
   - //260417 hbk 주석.

5. **`DatumMeasurement.csproj` 에 InspectionMasterParam.cs Compile Include 추가**:
   - `WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs` 근처 `<Compile Include=...>` 블록에 삽입.

6. **왕복 저장/로드 확인 — 추가 변경 불필요**:
   - `InspectionRecipeManager.SavePhase6Format` 은 `fixtureSeq.GetDisplayName()` 을 사용 → 변경 불필요.
   - `LoadPhase6Format` 은 `fixtureSeq.DisplayName = loadFile["FIXTURE"]["DisplayName"].ToString()` → 변경 불필요.
   - 단, Load 후 트리 라벨 갱신은 `RebuildTree` → `HookSequenceDisplayNameUpdates` 의 초기 동기화 로직이 처리.
  </action>
  <verify>
    <automated>cd C:/Info/Project/DataMeasurement && "/c/Program Files (x86)/Microsoft Visual Studio/2019/Community/MSBuild/Current/Bin/MSBuild.exe" WPF_Example/DatumMeasurement.csproj //p:Configuration=Debug //p:Platform=x64 //v:minimal 2>&1 | tail -15</automated>
  </verify>
  <acceptance_criteria>
    - `WPF_Example/Custom/Sequence/Inspection/InspectionMasterParam.cs` 파일 존재
    - `InspectionMasterParam.cs` 에 `class InspectionMasterParam : CameraMasterParam` 존재
    - `InspectionMasterParam.cs` 에 `public string DisplayName` 프로퍼티 존재 (get/set)
    - `InspectionSequence.cs` 에 `new InspectionMasterParam(this)` 존재
    - `InspectionListViewModel.cs` 에 `OnSequenceMasterPropertyChanged` 또는 `HookSequenceDisplayNameUpdates` 존재
    - `InspectionListViewModel.cs` 에 `InspectionMasterParam` 참조 존재
    - `DatumMeasurement.csproj` 에 `InspectionMasterParam.cs` 참조 존재
    - 빌드 성공 (신규 에러 0건)
  </acceptance_criteria>
  <done>Sequence 노드 선택 → PropertyGrid 에 "Fixture|Identity/DisplayName" 필드 표시 + 편집 가능 + 편집 즉시 트리 라벨 갱신 + 저장/재로드 왕복 유지</done>
</task>

<task type="auto">
  <name>Task 2: Btn_start_Click 재작성 — Shot 인덱스 ↔ Action 매핑 + 지연 동기화</name>
  <files>
    WPF_Example/UI/ControlItem/InspectionListView.xaml.cs
  </files>
  <read_first>
    WPF_Example/UI/ControlItem/InspectionListView.xaml.cs
    WPF_Example/Custom/Sequence/SequenceHandler.cs
    WPF_Example/Custom/Sequence/Inspection/InspectionRecipeManager.cs
    WPF_Example/Sequence/Sequence/SequenceBase.cs
  </read_first>
  <action>
1. **`Btn_start_Click` (line 128-157) 재작성**:
   - 요구사항:
     - (a) Sequence 노드 선택 시 → 해당 시퀀스의 첫 실행 가능한 Action 으로 Start.
     - (b) Shot(Action 타입) 노드 선택 시 → 해당 Shot 의 런타임 Action 으로 Start.
     - (c) Action 노드 선택 시(ShotConfig 가 아닌 일반 Action) → 기존 동작 유지 (node.ActionID 로 Start).
   - 새 헬퍼 `ResolveRunnableAction(NodeViewModel node, SequenceBase seq, out EAction actID)` 추가:
     ```csharp
     //260417 hbk Phase 6-04 UAT: Sequence/Shot 노드 → 실행 Action ID 해석
     private bool ResolveRunnableAction(NodeViewModel node, SequenceBase seq, out EAction actID) {
         actID = EAction.Unknown;
         if (seq == null || seq.ActionCount == 0) return false;

         // Shot 노드 (Action 타입 + ShotConfig Param)
         if (node.NodeType == ENodeType.Action && node.Param is ShotConfig shotCfg) {
             var recipeMgr = SystemHandler.Handle.Sequences.RecipeManager;
             int shotIdx = recipeMgr.Shots.IndexOf(shotCfg);
             if (shotIdx >= 0 && shotIdx < seq.ActionCount) {
                 // RebuildInspectionActions 가 호출된 상태면 FAI_Base + shotIdx == seq[shotIdx].ID
                 actID = seq[shotIdx].ID;
                 return true;
             }
             // 매핑 실패: 지연 동기화 (Idle 일 때만 안전)
             if (SystemHandler.Handle.Sequences.IsIdle) {
                 SystemHandler.Handle.Sequences.EnableDynamicFAIMode();
                 SystemHandler.Handle.Sequences.RebuildInspectionActions(seq.ID);
                 if (shotIdx >= 0 && shotIdx < seq.ActionCount) {
                     actID = seq[shotIdx].ID;
                     return true;
                 }
             }
             return false;
         }

         // Action 노드 (일반 Action: ShotConfig 아님) — 기존 경로
         if (node.NodeType == ENodeType.Action) {
             if (node.ActionID != EAction.Unknown) {
                 actID = node.ActionID;
                 return true;
             }
             actID = seq[0].ID;
             return true;
         }

         // Sequence 노드
         if (node.NodeType == ENodeType.Sequence) {
             actID = seq[0].ID;
             return true;
         }

         return false;
     }
     ```
   - `Btn_start_Click` 본문을 다음과 같이 교체 (//260417 hbk):
     ```csharp
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

         mParentWindow.StartSequence(seqID, actID);
     }
     ```
   - 기존 `GetDefaultRunnableAction` 는 `ResolveRunnableAction` 내부에서 `seq[0].ID` 로 대체되므로 **삭제**.

2. **검증용 Idle 체크**: `SequenceHandler.IsIdle` (line 108) 은 모든 시퀀스 Idle 여부를 반환 — 이미 기존 button_grab_Click 등에서 사용 중이므로 안전한 API.

3. **기존 `AddShotToSequence` 는 변경하지 않음** — Task 2 의 지연 동기화가 매핑 실패를 구제하므로 Shot 추가 경로를 건드릴 필요 없음. 주석만 업데이트:
   - line 481-482 주석 TODO 는 유지하되 "Phase 6-04 UAT 후속: 실행 시 Btn_start_Click 이 RebuildInspectionActions 로 지연 동기화" 한 줄 추가.

4. **주석**: 모든 변경 지점에 `//260417 hbk Phase 6-04 UAT: Shot 실행 경로 수정` 주석.
  </action>
  <verify>
    <automated>cd C:/Info/Project/DataMeasurement && "/c/Program Files (x86)/Microsoft Visual Studio/2019/Community/MSBuild/Current/Bin/MSBuild.exe" WPF_Example/DatumMeasurement.csproj //p:Configuration=Debug //p:Platform=x64 //v:minimal 2>&1 | tail -15</automated>
  </verify>
  <acceptance_criteria>
    - `InspectionListView.xaml.cs` 에 `ResolveRunnableAction` 메서드 존재
    - `InspectionListView.xaml.cs` 에 `RecipeManager.Shots.IndexOf` 또는 `recipeMgr.Shots.IndexOf` 존재
    - `InspectionListView.xaml.cs` 에 `RebuildInspectionActions` 참조 존재 (지연 동기화)
    - `GetDefaultRunnableAction` 메서드 삭제됨 (또는 더 이상 Btn_start_Click 에서 호출되지 않음)
    - `Btn_start_Click` 에서 `SequenceHandler.IsIdle` 체크 존재
    - 빌드 성공 (신규 에러 0건)
  </acceptance_criteria>
  <done>
  - Sequence 노드 선택 + Start → 해당 시퀀스의 첫 Action 실행
  - Shot 노드 선택 + Start → 해당 Shot 에 매핑된 Action_FAIMeasurement 실행 (동적 모드 미진입 시 자동 RebuildInspectionActions)
  - 실행 불가 조건 (Action 없음, 이미 실행 중) 에서 정확한 에러 메시지 노출
  </done>
</task>

<task type="checkpoint:human-verify" gate="blocking">
  <name>Task 3: UAT 재검증 (Phase 6-04 잔여 결함 2건)</name>
  <files></files>
  <what-built>
- `InspectionMasterParam` 신규 — InspectionSequence.Param 을 교체하여 PropertyGrid 에 DisplayName 편집 필드 노출.
- `InspectionListViewModel` 에 PropertyChanged 훅 — DisplayName 편집 시 트리 라벨 즉시 갱신.
- `Btn_start_Click` 재작성 — Sequence/Shot 노드 모두 Start 가능, Shot→Action 매핑 실패 시 RebuildInspectionActions 로 지연 동기화.
  </what-built>
  <how-to-verify>
1. **앱 실행** (Debug x64 빌드 → DatumMeasurement.exe 실행, SIMUL_MODE).
2. **DisplayName 편집 UI (결함 1)**:
   - InspectionListView 트리에서 Sequence 노드 (예: TOP) 선택.
   - PropertyGrid 에 "Fixture|Identity" 카테고리의 "DisplayName" 필드가 보이는지 확인.
   - DisplayName 에 "Rapid City Top" 입력 후 포커스 이동.
   - 트리 라벨이 즉시 "Rapid City Top" 으로 바뀌는지 확인.
   - 레시피 저장 → 앱 재시작 → 동일 레시피 로드 → 라벨이 "Rapid City Top" 으로 복원되는지 확인.
   - DisplayName 을 비우면 라벨이 원래 시퀀스 이름 (TOP) 으로 돌아오는지 확인.
3. **Sequence 노드 Start (결함 2-a)**:
   - TOP Sequence 노드 선택 → Start 버튼 클릭.
   - "There is no action to run" 에러가 **나오지 않고** 시퀀스가 실행되는지 확인.
   - 결과 테이블에 Measurement 행 표시 확인.
4. **Shot 노드 Start (결함 2-b, Phase 6 동적 모드)**:
   - Phase 6 레시피 로드 (Shot 여러 개 있는 상태) 또는 UI 에서 Shot 2~3개 추가.
   - 각 Shot 노드 선택 → Start 버튼 클릭.
   - 각 Shot 에 해당하는 Action_FAIMeasurement 가 실행되는지 확인 (결과 테이블 갱신).
   - Shot 을 UI 로 방금 추가한 경우에도 Start 가 동작하는지 (지연 동기화) 확인.
5. **실행 중 재실행 방지**: 시퀀스 실행 중 Start 재클릭 → "Sequence is already running" 에러 노출 확인.
6. **Action 노드 (일반)**: ShotConfig 가 아닌 Action 노드 (Phase 5 이전 레시피) 는 기존 동작 유지 확인 (가능하면).

**통과 조건**:
- [ ] DisplayName 필드 표시 + 편집 + 트리 즉시 갱신 + 저장/로드 왕복 유지
- [ ] Sequence 노드 Start 정상 실행
- [ ] Shot 노드 Start 정상 실행 (정상 레시피 로드 상태)
- [ ] UI 로 추가한 Shot 노드 Start 정상 실행 (지연 동기화)
- [ ] 이미 실행 중일 때 재실행 차단 메시지 노출
  </how-to-verify>
  <verify>
    <automated>cd C:/Info/Project/DataMeasurement && "/c/Program Files (x86)/Microsoft Visual Studio/2019/Community/MSBuild/Current/Bin/MSBuild.exe" WPF_Example/DatumMeasurement.csproj //p:Configuration=Debug //p:Platform=x64 //v:minimal 2>&1 | tail -5</automated>
  </verify>
  <done>사용자가 5개 시나리오 모두 통과 확인 후 "approved" 입력</done>
  <resume-signal>"approved" 입력 또는 문제점 기술</resume-signal>
</task>

</tasks>

<verification>
- 빌드 성공: msbuild Debug/x64, 신규 에러 0건.
- DisplayName 편집 가능 + 트리 라벨 즉시 갱신 + 저장/로드 왕복 유지.
- Sequence 노드 Start + Shot 노드 Start 모두 정상 동작.
- UI 로 추가한 Shot 노드도 지연 동기화로 Start 가능.
- 실행 중 재시작 차단 메시지.
</verification>

<success_criteria>
- InspectionMasterParam.cs 신규 파일 존재 + PropertyGrid DisplayName 편집 필드 노출.
- InspectionListViewModel.cs DisplayName 편집 이벤트 훅 + 트리 라벨 갱신.
- InspectionListView.xaml.cs Btn_start_Click 재작성 (Sequence/Shot/Action 3가지 노드 지원, 지연 동기화, Idle 체크).
- 사람-검증 UAT 시나리오 5개 전부 통과.
</success_criteria>

<output>
After completion, create `.planning/quick/260417-kzd-phase-6-04-uat-displayname-ui-shot/260417-kzd-SUMMARY.md`
</output>
