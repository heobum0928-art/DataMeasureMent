---
phase: quick-260423-lws
plan: 01
type: execute
wave: 1
depends_on: []
files_modified:
  - WPF_Example/UI/ControlItem/InspectionListView.xaml.cs
autonomous: true
requirements: [datum-grab-loadimage-fix]

must_haves:
  truths:
    - "Datum 노드 선택 시 button_grab, button_loadImage 모두 활성화된다"
    - "button_grab_Click에서 DatumConfig 선택 상태일 때 SourceShotName 기반으로 ShotConfig를 조회하여 GrabAndDisplay를 호출한다"
    - "button_loadImage_Click에서 동일 패턴으로 LoadAndDisplay를 호출한다"
    - "RecipeManager.Shots가 비어있으면 Grab/LoadImage 동작 없이 조용히 반환한다"
  artifacts:
    - path: "WPF_Example/UI/ControlItem/InspectionListView.xaml.cs"
      provides: "Datum 노드 버튼 활성화 + ResolveDatumCameraParam helper"
      contains: "ResolveDatumCameraParam"
  key_links:
    - from: "InspectionList_SelectionChanged (ENodeType.Datum 블록)"
      to: "button_grab.IsEnabled / button_loadImage.IsEnabled"
      via: "직접 속성 할당"
    - from: "button_grab_Click / button_loadImage_Click"
      to: "ResolveDatumCameraParam"
      via: "DatumConfig is-pattern 분기"
    - from: "ResolveDatumCameraParam"
      to: "SystemHandler.Handle.Sequences.RecipeManager.Shots"
      via: "LINQ FirstOrDefault on ShotName"
---

<objective>
Datum 노드를 선택했을 때 비활성화된 Grab/LoadImage 버튼을 활성화하고,
클릭 핸들러가 DatumConfig.SourceShotName 기반으로 ShotConfig를 조회하여
GrabAndDisplay/LoadAndDisplay를 위임하도록 수정한다.

Purpose: Datum 티칭 워크플로우에서 이미지 캡처 및 로드가 작동하지 않아 블로킹되는 버그 해소.
Output: InspectionListView.xaml.cs 수정 (버튼 활성화 3곳 + 클릭 핸들러 2곳 + helper 1개 추가).
</objective>

<execution_context>
@$HOME/.claude/get-shit-done/workflows/execute-plan.md
@$HOME/.claude/get-shit-done/templates/summary.md
</execution_context>

<context>
@.planning/STATE.md
@.planning/ROADMAP.md

<interfaces>
<!-- 핵심 타입 — 탐색 없이 바로 사용 -->

// WPF_Example/Sequence/Param/CameraParam.cs
public interface ICameraParam {
    string CameraName { get; }
    string LightGroupName { get; }
    int LightLevel { get; }
    // ... (GrabAndDisplay/LoadAndDisplay에 필요한 전부)
}

// WPF_Example/Sequence/Param/CameraSlaveParam.cs
public class CameraSlaveParam : ParamBase, ICameraParam { }

// WPF_Example/Custom/Sequence/Inspection/ShotConfig.cs
public class ShotConfig : CameraSlaveParam {
    public string ShotName { get; set; }
    // ShotConfig : CameraSlaveParam : ICameraParam — 직접 캐스팅 가능
}

// WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs
public class DatumConfig : ParamBase {
    public string SourceShotName { get; set; } = "";
    // ICameraParam 미구현 — 직접 캐스팅 불가
}

// 접근 경로
SystemHandler.Handle.Sequences.RecipeManager.Shots  // List<ShotConfig>
// (SequenceHandler에 RecipeManager 프로퍼티 존재 — InspectionListView.xaml.cs L61 참조)

// UI 메서드 (MainView)
void GrabAndDisplay(ICameraParam param, bool eventCall = false);
void LoadAndDisplay(ICameraParam param);
</interfaces>
</context>

<tasks>

<task type="auto">
  <name>Task 1: InspectionList_SelectionChanged — button_loadImage 초기화 및 Datum 블록 버튼 활성화</name>
  <files>WPF_Example/UI/ControlItem/InspectionListView.xaml.cs</files>
  <action>
두 곳을 수정한다.

**수정 1 — L241 직후 (button_grab.IsEnabled = false; 바로 아래):**
```csharp
button_loadImage.IsEnabled = false;  //260423 hbk Datum 노드 지원: 선택 변경 시 초기화
```
(현재 button_grab은 L242에서 false로 초기화되지만 button_loadImage는 초기화 누락)

**수정 2 — ENodeType.Datum 블록 내부 (L278~289, `button_addFAI.IsEnabled = true;` 바로 아래):**
```csharp
//260423 hbk Datum 노드: Grab/LoadImage 활성화 (DatumConfig → ResolveDatumCameraParam 위임)
button_grab.IsEnabled = true;
button_loadImage.IsEnabled = true;
```

K&R 스타일 유지. 기존 Datum 블록의 들여쓰기·중괄호 스타일과 일치시킨다.
  </action>
  <verify>빌드 성공 (msbuild WPF_Example/DatumMeasurement.csproj /p:Configuration=Debug /p:Platform=x64). Datum 노드 선택 시 두 버튼이 활성화되고, 다른 노드(FAI, Action 등) 선택 시에는 ICameraParam 미구현 노드의 경우 비활성 상태를 유지하는지 육안 확인.</verify>
  <done>Datum 노드 선택 → button_grab, button_loadImage 모두 IsEnabled=true. 빌드 오류 없음.</done>
</task>

<task type="auto">
  <name>Task 2: button_grab_Click / button_loadImage_Click DatumConfig 분기 추가 + ResolveDatumCameraParam helper</name>
  <files>WPF_Example/UI/ControlItem/InspectionListView.xaml.cs</files>
  <action>
**button_grab_Click (현재 L379~391) — ICameraParam 가드 바로 앞에 DatumConfig 분기 삽입:**

기존:
```csharp
private void button_grab_Click(object sender, RoutedEventArgs e) {
    if (SelectedParam == null) return;
    if (!(SelectedParam is ICameraParam)) return;
    ...
    ICameraParam camParam = SelectedParam as ICameraParam;
    mParentWindow.mainView.GrabAndDisplay(camParam);
}
```

수정 후:
```csharp
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
        return;
    }
    ICameraParam camParam = SelectedParam as ICameraParam;
    mParentWindow.mainView.GrabAndDisplay(camParam);
}
```

**button_loadImage_Click (현재 L393~399) — 동일 패턴:**

```csharp
private void button_loadImage_Click(object sender, RoutedEventArgs e) {
    if (SelectedParam == null) return;
    //260423 hbk Datum 노드: ICameraParam 미구현이므로 Shot 위임
    if (SelectedParam is DatumConfig datumForLoad) {
        ICameraParam resolved = ResolveDatumCameraParam(datumForLoad);
        if (resolved == null) return;
        mParentWindow.mainView.LoadAndDisplay(resolved);
        return;
    }
    if (!(SelectedParam is ICameraParam)) return;
    ICameraParam camParam = SelectedParam as ICameraParam;
    mParentWindow.mainView.LoadAndDisplay(camParam);
}
```

**ResolveDatumCameraParam helper — button_loadImage_Click 바로 다음에 추가:**

```csharp
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
```

`using System.Linq;` 가 파일 상단에 없으면 추가한다 (기존 using 목록 확인 후 필요 시만).

K&R 스타일 유지. 변수명은 camelCase.
  </action>
  <verify>
빌드 성공. 런타임 검증 (SIMUL_MODE):
1. Datum 노드 선택 → button_grab 클릭 → GrabAndDisplay 호출됨 (MainView에 이미지 표시 또는 SIMUL 이미지 로드됨).
2. Datum 노드 선택 → button_loadImage 클릭 → LoadAndDisplay 호출됨 (파일 다이얼로그 열림).
3. Shots가 없는 레시피 상태 → Grab/LoadImage 클릭 → 아무 동작 없이 조용히 반환.
4. ICameraParam 구현 노드(ShotConfig) 선택 → 기존 동작 정상 유지.
  </verify>
  <done>
- DatumConfig 선택 시 Grab → GrabAndDisplay(resolvedShot) 호출됨
- DatumConfig 선택 시 LoadImage → LoadAndDisplay(resolvedShot) 호출됨
- Shots 빈 경우 null 반환 후 조용히 종료
- 기존 ICameraParam 경로 동작 변화 없음
- 빌드 오류/경고 없음
  </done>
</task>

</tasks>

<threat_model>
## Trust Boundaries

| Boundary | Description |
|----------|-------------|
| UI → RecipeManager | UI 스레드에서 RecipeManager.Shots 읽기 전용 접근 |

## STRIDE Threat Register

| Threat ID | Category | Component | Disposition | Mitigation Plan |
|-----------|----------|-----------|-------------|-----------------|
| T-lws-01 | Information Disclosure | ResolveDatumCameraParam | accept | Shots는 로컬 레시피 데이터, 외부 노출 없음 |
| T-lws-02 | Denial of Service | shots[0] fallback | mitigate | shots.Count == 0 체크로 IndexOutOfRange 방지 |
</threat_model>

<verification>
1. `msbuild WPF_Example/DatumMeasurement.csproj /p:Configuration=Debug /p:Platform=x64` — 빌드 성공
2. SIMUL_MODE로 실행 → Datum 노드 선택 → Grab/LoadImage 버튼 활성화 확인
3. Grab 클릭 → 이미지 표시 확인
4. LoadImage 클릭 → 파일 다이얼로그 열림 확인
5. ShotConfig(Action) 노드 선택 → 기존 동작 정상 확인
</verification>

<success_criteria>
- Datum 노드 선택 시 button_grab, button_loadImage 활성화
- button_grab_Click, button_loadImage_Click에서 DatumConfig → ResolveDatumCameraParam → ICameraParam 변환 후 MainView 위임
- 빈 Shots 리스트에서 null 반환, 조용히 종료
- 기존 ICameraParam 노드(ShotConfig, CameraParam) 동작 변화 없음
- 빌드 오류 없음
</success_criteria>

<output>
완료 후 `.planning/quick/260423-lws-datum-grab-loadimage-datumconfig/260423-lws-SUMMARY.md` 생성
</output>
