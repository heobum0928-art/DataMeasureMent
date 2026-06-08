---
phase: quick-260511-ucv
plan: 01
type: execute
wave: 1
depends_on: []
files_modified:
  - WPF_Example/UI/ControlItem/InspectionListView.xaml.cs
autonomous: false
requirements:
  - CO-22-01
user_setup: []

must_haves:
  truths:
    - "Datum 노드 클릭 → 즉시 PropertyGrid 가 해당 DatumConfig 의 동적 필터(AlgorithmType 별) 속성을 표시"
    - "FAI 노드 클릭 → 즉시 PropertyGrid 가 해당 FAIConfig 의 동적 필터(EdgeMeasureType 별) 속성을 표시"
    - "Datum ↔ FAI 양방향 전환에서 stale (이전 노드 속성) 표시 0건"
    - "기존 Phase 17 D-09 / Phase 19 fix / Phase 13-07 자동 재티칭 / Phase 17 D-10 AlgorithmType 5-step 리셋 회귀 0"
    - "msbuild Debug/x64 Rebuild PASS (신규 error/warning 0)"
  artifacts:
    - path: "WPF_Example/UI/ControlItem/InspectionListView.xaml.cs"
      provides: "트리 선택 핸들러 root-cause 진단 + 최소 변경 fix + //260511 hbk CO-22-01 마커"
      contains: "InspectionList_SelectionChanged"
    - path: ".planning/quick/260511-ucv-co-22-01-datum-fai-propertygrid/UAT-NOTES.md"
      provides: "SIMUL UAT 시나리오 (Datum↔FAI 양방향 + Datum↔Datum + FAI↔FAI + Measurement 전환) + 사용자 검증 결과"
      contains: "Scenario"
  key_links:
    - from: "treeListBox_sequence.SelectionChanged event"
      to: "ParamEditor.SelectedObject"
      via: "InspectionList_SelectionChanged → NodeType 분기 → force rebind block"
      pattern: "ParamEditor\\.SelectedObject = null.*ParamEditor\\.SelectedObject = "
---

<objective>
**CO-22-01 fix — Datum ↔ FAI 노드 PropertyGrid 전환 시 즉시 갱신 안 됨.**

트리에서 Datum 노드 → FAI 노드 (또는 그 반대) 선택 시 PropertyGrid 가 이전 노드 속성을 그대로 표시하는 stale 버그를 해결한다. Phase 17 (DatumConfig.ICustomTypeDescriptor) + Phase 19 (FAIConfig.ICustomTypeDescriptor) 도입 이후 발견됨. 이미 force rebind 4 분기 (Datum/Measurement/FAI + AlgorithmType ComboBox) 모두 적용되어 있으므로, **추가 force rebind 가 아닌 다른 root cause 탐색** 이 핵심.

Purpose: Phase 22 UAT carry-over 잔여 결함 해소 → v1.1 milestone 다음 phase 로 진행 가능.
Output: InspectionListView.xaml.cs 최소 라인 변경 + SIMUL UAT 사용자 검증 통과.
</objective>

<execution_context>
@$HOME/.claude/get-shit-done/workflows/execute-plan.md
@$HOME/.claude/get-shit-done/templates/summary.md
</execution_context>

<context>
@.planning/STATE.md
@CLAUDE.md
@.planning/phases/22-image-dual-structure/22-UAT.md
@.planning/phases/19-propertygrid-dynamic-exposure/19-VERIFICATION.md
@.planning/debug/phase-19-datumconfig-regression.md
@WPF_Example/UI/ControlItem/InspectionListView.xaml.cs
@WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs
@WPF_Example/Custom/Sequence/Inspection/FAIConfig.cs
@WPF_Example/Custom/Sequence/Inspection/DynamicPropertyHelper.cs

<interfaces>
<!-- 핵심 컨트랙트 (코드 조사 결과 — Task 1 의 root cause 탐색 출발점) -->

InspectionListView.xaml.cs L354-512 `InspectionList_SelectionChanged`:
```csharp
private void InspectionList_SelectionChanged(object sender, SelectionChangedEventArgs e) {
    // L368: object source = e.Source;
    // L369: if (source is TreeListBox) {  ← 게이트 (실패 시 함수 전체 skip)
    //   L370: TreeListBox list = source as TreeListBox;
    //   L371: if (list.SelectedItem is NodeViewModel) {  ← 2단 게이트
    //     L373: object itemParam = item.Param;
    //     L388: mParentWindow.mainView.SetParam(item.SequenceID, param); ← PropertyGrid 미터치
    //     L389: SelectedParam = param;
    //     L398: if (item.NodeType == ENodeType.Datum)    → L421-430 force rebind (null→new)
    //     L438: else if (NodeType == Measurement)        → L445-453 force rebind
    //     L456: else if (NodeType == FAI)                → L465-473 force rebind
    //     L476: else if (NodeType == Action)             → force rebind 없음 ← 의심점 B
    //     L484: else if (NodeType == Sequence)           → force rebind 없음
    //     L490: else                                     → force rebind 없음
```

MainView.xaml.cs L220-226 `SetParam`:
```csharp
public void SetParam(ESequence seqID, ParamBase param) {
    if (pSeq == null || pSeq[seqID] == null) return;
    string selectedSeq = pSeq[seqID].Name;
    if (ContextList != null && ContextList.ContainsKey(selectedSeq)) {
        DisplayParam(ContextList[selectedSeq], param);  // ParamEditor.SelectedObject 미터치
    }
}
```
→ PropertyGrid 갱신은 **오직 InspectionListView 의 force rebind 4 블록** 에서만 발생.

DatumConfig.cs L589-591 + FAIConfig.cs L228-230 (Phase 19 fix):
```csharp
public PropertyDescriptorCollection GetProperties() {
    return BuildFilteredProperties(null);  // ← PropertyTools.Wpf 가 진짜 호출하는 entry
}
```
→ PropertyGrid 의 SelectedObject 가 동일 인스턴스 ref 면 GetProperties() 가 재호출 안 됨 (PropertyTools 내부 캐시).
→ `null` 할당 후 재할당하면 캐시 무효화 → GetProperties() 재호출 → 동적 필터 재적용.
</interfaces>
</context>

<tasks>

<task type="auto">
  <name>Task 1: Root cause 추적 + 최소 변경 fix (InspectionListView.xaml.cs)</name>
  <files>WPF_Example/UI/ControlItem/InspectionListView.xaml.cs</files>
  <action>
**진단 우선 — fix 후순위.**

### Step A. Root cause 후보 검증 (코드 읽기만, write 0)

다음 5개 의심 영역을 코드와 대조하여 **단 하나의 root cause 후보** 로 좁힌다. 각 후보에 대해 PASS/FAIL 판정 + 근거 라인 번호 메모.

후보 (A): **e.Source 게이트 실패** — L368-369 `if (source is TreeListBox)`. TreeListBox SelectionChanged 의 e.Source 는 일반적으로 TreeListBox 자신이지만, WPF 의 ItemsControl 계열에서는 inner ListBoxItem 이 RoutedEvent 의 Source 로 올라오는 경우가 있음. 만약 e.Source 가 ListBoxItem 또는 NodeViewModel 이라면 게이트 실패 → 함수 전체 skip → 이전 노드의 SelectedObject 가 그대로 유지 → **stale**.
  - 검증: PropertyTools.Wpf 의 TreeListBox 가 Selector 를 상속하는지 + e.OriginalSource vs e.Source 차이 + bubbling 경로 확인. 실제 Phase 17/19 의 OnParamEditorSelectionChanged (L72) 는 e.OriginalSource 를 사용하지만, InspectionList_SelectionChanged 는 e.Source 사용 — 이 비대칭이 의심.

후보 (B): **NodeType 분기 누락** — Action/Sequence/else 분기 (L476/L484/L490) 에는 force rebind 가 없음. Datum 노드에서 Action 노드로 이동 후 다시 Datum 또는 FAI 로 이동하면, Action 노드 진입 시 force rebind 가 일어나지 않아 PropertyGrid 가 이전 Datum 의 binding 을 유지한 채 stale 상태로 시작. 다음 Datum/FAI 클릭의 force rebind 가 실행되어도 PropertyTools 내부 캐시가 동일 인스턴스 ref 를 감지하면 재바인딩 무시 가능.
  - 검증: 사용자 보고가 "Datum ↔ FAI 직접 전환" 인지 "Datum → Action → FAI" 경유 전환인지 확인. UAT 시나리오에서 양쪽 모두 재현 필요.

후보 (C): **`_isRebinding` race 에 의한 이중 차단** — L74 의 `if (_isRebinding) return` 가드는 OnParamEditorSelectionChanged 에 있음. 트리 선택 SelectionChanged 가 PropertyGrid 의 ComboBox SelectionChanged 와 동일 dispatcher tick 에 fire 되면, force rebind 중 inner ComboBox 가 NULL→new 의 NULL 단계에서 새 SelectionChanged 를 발생시켜 _isRebinding 이 즉시 false 로 복원되기 전에 외부 핸들러가 재진입할 수 있음. 단 `_isRebinding` 은 OnParamEditorSelectionChanged 에서만 검사되고 InspectionList_SelectionChanged 는 검사 안 함 → 이 후보는 likely FAIL.

후보 (D): **PropertyTools.Wpf 의 SelectedObject 동일 ref 캐시** — Datum→Datum 또는 FAI→FAI 전환 시 동일 Type 의 다른 인스턴스라도 PropertyTools 내부적으로 PropertyDescriptorCollection 을 Type 단위로 캐시. ICustomTypeDescriptor 가 인스턴스 단위로 다른 필터를 적용 (예: AlgorithmType=TwoLineIntersect vs CircleTwoHorizontal) 해야 하는 경우, 같은 Type 의 다른 인스턴스 간 전환 시 캐시된 PropertyDescriptorCollection 이 재사용되어 stale.
  - 검증: 사용자 시나리오에서 같은 Type 간 전환 (Datum_1 → Datum_2 with 다른 AlgorithmType) 도 stale 인지 확인. 만약 다른 Type 간 전환 (Datum → FAI) 만 stale 이면 후보 (A) 또는 (B) 가 진짜.

후보 (E): **e.Handled cascade** — InspectionListView 의 트리 SelectionChanged 와 ParamEditor 의 SelectionChangedEvent AddHandler (L188, handledEventsToo=true) 가 동일 RoutedEvent 채널을 공유. AddHandler 의 `true` 플래그로 ParamEditor 가 이미 e.Handled=true 마킹한 후, 트리 SelectionChanged 가 bubble 되어 InspectionList_SelectionChanged 로 들어오는데 이 핸들러는 e.Handled 검사 안 함 → 무관 (likely FAIL).

**보고:** Step A 의 5개 후보 PASS/FAIL 판정을 UAT-NOTES.md 에 root_cause 섹션으로 기록. **단 하나의 후보만 fix 대상.**

### Step B. Fix 구현 (가장 유력한 후보 1개)

후보 (A) 가 PASS 인 경우 (가장 유력):
```csharp
//260511 hbk CO-22-01 — e.Source 가 inner ListBoxItem 일 때 게이트 실패 → 트리 선택 즉시 force rebind 미실행 → stale.
//  TreeListBox 의 SelectionChanged 는 sender(=TreeListBox) 기준으로 처리하고, e.Source 게이트는 제거.
//  단, ParamEditor 의 inner Selector 가 bubble 시킨 SelectionChanged 와 충돌 방지 위해 sender == treeListBox_sequence 가드.
private void InspectionList_SelectionChanged(object sender, SelectionChangedEventArgs e) {
    if (!(sender is TreeListBox list)) return; //260511 hbk CO-22-01 — sender 기준 게이트 (e.Source 비대칭 회피)
    if (!ReferenceEquals(sender, treeListBox_sequence)) return; //260511 hbk CO-22-01 — ParamEditor inner bubble 차단
    // ... 기존 본문 (object source = e.Source; if (source is TreeListBox) ... 블록을 sender 게이트로 교체) ...
}
```

후보 (B) 가 PASS 인 경우:
```csharp
//260511 hbk CO-22-01 — Action/Sequence/else 분기에도 force rebind 추가
//  Action 노드 (ShotConfig 등) 에서 itemParam 이 ParamBase 인 경우 PropertyGrid 가 ShotConfig 속성을 표시해야 하나 stale.
else if (item.NodeType == ENodeType.Action) {
    // ... 기존 ...
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
// Sequence / else 분기도 동일 패턴 (param == null 경로 포함)
```

후보 (D) 가 PASS 인 경우 (동일 Type 캐시):
```csharp
//260511 hbk CO-22-01 — PropertyTools.Wpf 의 PropertyDescriptorCollection Type 캐시 무효화 위해
//  TypeDescriptor.Refresh(itemParam) 호출 후 force rebind.
//  Refresh 는 GetProperties 캐시를 강제 invalidate 하므로 ICustomTypeDescriptor.GetProperties() 가 재호출됨.
if (ParamEditor != null) {
    _isRebinding = true; //260511 hbk CO-22-01
    try {
        System.ComponentModel.TypeDescriptor.Refresh(itemParam); //260511 hbk CO-22-01
        ParamEditor.SelectedObject = null;
        ParamEditor.SelectedObject = itemParam;
    } finally {
        _isRebinding = false;
    }
}
```

**제약:**
- 변경 라인 최소화 — Step A 에서 PASS 판정된 단 하나의 후보만 fix.
- 모든 수정/추가 라인에 `//260511 hbk CO-22-01` 마커.
- 기존 hbk 마커 (260429 Phase 16, 260503 Phase 17, 260508 Phase 19, 260509 Phase 20) **보존** (Phase 20 D-12 stacking 패턴 — 위에 누적).
- Phase 17 D-09 ICustomTypeDescriptor 동작 회귀 0.
- Phase 19 fix (Datum/Measurement/FAI force rebind 3 블록) 동작 회귀 0.
- Phase 17 D-10 AlgorithmType 5-step 리셋 (L84-121) 동작 회귀 0.
- Phase 13-06/07 자동 재티칭 (TryTriggerDatumAutoReteach) 동작 회귀 0.

### Step C. msbuild Debug/x64 Rebuild 검증

```
msbuild WPF_Example/DatumMeasurement.csproj /p:Configuration=Debug /p:Platform=x64 /t:Rebuild /m
```

기대: PASS, 0 errors, 신규 warning 0 (기존 baseline 6 warnings = Phase 21 기준 보존).
  </action>
  <verify>
    <automated>cd /d C:\Info\Project\DataMeasurement &amp;&amp; "C:\Program Files (x86)\Microsoft Visual Studio\2019\BuildTools\MSBuild\Current\Bin\MSBuild.exe" WPF_Example\DatumMeasurement.csproj /p:Configuration=Debug /p:Platform=x64 /t:Rebuild /m /v:m</automated>
  </verify>
  <done>
- Step A 의 5개 후보 PASS/FAIL 판정이 UAT-NOTES.md 의 root_cause 섹션에 기록됨
- 단 하나의 후보만 fix 대상으로 선정 + 코드 변경
- 모든 변경 라인에 //260511 hbk CO-22-01 마커
- 기존 hbk 마커 보존
- msbuild Debug/x64 Rebuild PASS (0 errors, baseline warning 0 증가)
- InspectionListView.xaml.cs 외 파일 변경 0
  </done>
</task>

<task type="checkpoint:human-verify" gate="blocking">
  <name>Task 2: SIMUL UAT 사용자 검증 (5 scenarios)</name>
  <what-built>
Task 1 의 InspectionListView.xaml.cs root cause fix. PropertyGrid 가 트리 선택 시 즉시 갱신되어야 함.
  </what-built>
  <how-to-verify>
**준비:**
1. DataMeasurement.exe 를 Debug/x64 로 실행 (SIMUL_MODE)
2. 기존 레시피 로드 (Datum >= 2개 + FAI >= 2개 보장; Datum 의 AlgorithmType 은 가능하면 서로 다르게 — TwoLineIntersect / CircleTwoHorizontal / VerticalTwoHorizontal)

**Scenario 1 — Datum ↔ FAI 직접 전환 (메인 버그):**
- (a) 트리에서 Datum_1 노드 클릭 → PropertyGrid 에 DatumConfig 속성 (AlgorithmType, Line1_* 또는 Circle_* 또는 Vertical_*) 즉시 표시 확인
- (b) FAI_1 노드 클릭 → PropertyGrid 가 즉시 FAIConfig 속성 (EdgeMeasureType, EdgeDirection, EdgePolarity 등) 으로 전환 확인
- (c) 다시 Datum_1 클릭 → DatumConfig 속성으로 전환 확인
- (d) FAI_2 클릭 → FAI_2 의 FAIConfig 속성 표시 확인 (FAI_1 잔재 0)

**Scenario 2 — Datum ↔ Datum 전환 (동일 Type, 다른 인스턴스):**
- (a) Datum_1 (AlgorithmType=TwoLineIntersect) → Datum_2 (AlgorithmType=CircleTwoHorizontal) 전환 시 PropertyGrid 의 동적 노출이 즉시 바뀜 (Line1_*/Line2_* 숨김 → Circle_* 노출)
- (b) 역방향 동일

**Scenario 3 — FAI ↔ FAI 전환 (동일 Type, 다른 EdgeMeasureType):**
- (a) FAI_1 (EdgeMeasureType=EdgePairDistance) → FAI_2 (EdgeMeasureType=CircleDiameter) 전환 시 동적 노출 변경 (Circle_RadialDirection 노출/숨김)
- (단, FAI 자체에는 EdgeMeasureType 콤보 노출이 없을 수 있음 — Measurement 노드 검사로 대체 가능)

**Scenario 4 — Action/Sequence 경유 전환 (3-hop):**
- (a) Datum_1 → SHOT_0 (Action 노드) → FAI_1 클릭. PropertyGrid 가 최종 FAI_1 의 FAIConfig 를 정확히 표시 (중간 SHOT 의 ShotConfig 표시 후 FAI 로 갱신)
- (b) Sequence 노드 (Top) → Datum_1 클릭. PropertyGrid 가 Sequence 노드의 null/CameraMasterParam 에서 DatumConfig 로 전환

**Scenario 5 — Phase 17 D-10 회귀 확인 (AlgorithmType 변경):**
- (a) Datum_1 노드 선택 후 PropertyGrid 의 AlgorithmType ComboBox 를 TwoLineIntersect → CircleTwoHorizontal 변경
- (b) PropertyGrid 동적 필터 즉시 갱신 (Line1_* 숨김, Circle_* 노출) + LastTeachSucceeded=false 로 Datum 오버레이 검출 도형 사라짐 (티칭 ROI 는 유지)

**기대 결과:** 5/5 PASS. 단 한 시나리오라도 stale 발생 시 FAIL → 사용자가 어느 시나리오인지 보고 → Task 1 root cause 후보 재선정.
  </how-to-verify>
  <resume-signal>"approved 5/5 PASS" 또는 "FAIL scenario N: [관찰 내용]"</resume-signal>
</task>

</tasks>

<threat_model>
## Trust Boundaries

| Boundary | Description |
|----------|-------------|
| WPF dispatcher → ParamEditor | UI 이벤트 핸들러가 PropertyGrid SelectedObject 를 직접 mutate; 동일 thread 내 race 가능 |
| TreeListBox.SelectionChanged → InspectionListView | RoutedEvent bubble; e.Source 비대칭 가능성 (후보 A) |

## STRIDE Threat Register

| Threat ID | Category | Component | Disposition | Mitigation Plan |
|-----------|----------|-----------|-------------|-----------------|
| T-CO-22-01-01 | Tampering | InspectionList_SelectionChanged | accept | UI 핸들러 단일 thread; 외부 입력 없음 — 코드 변경만으로 표면 확대 0 |
| T-CO-22-01-02 | Denial of Service | force rebind cascade | mitigate | _isRebinding 가드 보존 + ReferenceEquals(sender, treeListBox_sequence) 로 inner bubble 차단 (후보 A fix) |
| T-CO-22-01-03 | Repudiation | hbk 마커 누락 | mitigate | 모든 변경 라인 //260511 hbk CO-22-01 마커 강제 (CLAUDE.md feedback rule) |
</threat_model>

<verification>
- msbuild Debug/x64 Rebuild PASS (0 errors, 신규 warning 0)
- Scenario 1 ~ 5 모두 PASS (사용자 SIMUL UAT)
- 변경 파일 = InspectionListView.xaml.cs **1개만**
- 모든 변경 라인에 //260511 hbk CO-22-01 마커 (grep 확인)
- 기존 hbk 마커 (260429, 260503, 260508, 260509) 보존 (grep 확인)
</verification>

<success_criteria>
- CO-22-01 STATE.md Pending Todos 항목 → resolved 로 갱신 (Task 2 사용자 approved 후)
- 회귀 0: Phase 17 D-09 (ICustomTypeDescriptor) + Phase 19 fix (3 분기 force rebind) + Phase 17 D-10 (AlgorithmType 5-step) + Phase 13-06/07 (자동 재티칭) 모두 동작 보존
- git commit: `fix(quick-260511-ucv): CO-22-01 Datum↔FAI PropertyGrid stale [root cause: X]`
</success_criteria>

<output>
After completion, create `.planning/quick/260511-ucv-co-22-01-datum-fai-propertygrid/SUMMARY.md` with:
- Root cause analysis (Step A 의 5 후보 PASS/FAIL + 선정된 후보)
- 변경 diff 요약 (라인 수 + 패턴)
- UAT 5 scenario 결과
- 회귀 검증 매트릭스 (Phase 17/19 동작)
- STATE.md Pending Todos 업데이트 노트 (CO-22-01 → resolved)
</output>
