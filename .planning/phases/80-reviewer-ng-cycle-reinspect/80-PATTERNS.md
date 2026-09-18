# Phase 80: 리뷰어 NG 사이클 재검사 - Pattern Map

**Mapped:** 2026-09-18
**Files analyzed:** 10 (신규 3 + 수정 7)
**Analogs found:** 10 / 10 (전부 role-match 이상, 프로젝트 내부 재사용 우선 원칙 — RESEARCH.md "Don't Hand-Roll" 참고)

모든 패턴 발췌는 CLAUDE.md 🔒 가독성 규칙(삼항·`??`·`?.`·switch식 금지, 중괄호 필수, 헝가리언, C# 7.2)을 통과하는 형태로만 제시한다. 원본 코드가 이미 이 규칙을 어기는 경우, 아래에 "위반 있음 → 준수형" 으로 교정본을 별도 제시한다.

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|---|---|---|---|---|
| `WPF_Example/Custom/Sequence/Inspection/ReviewerReinspectService.cs` (신규) | service (정적, 상태보유) | snapshot/override-restore | `RepeatRunService.cs` 의 `SavedCycleOverrideSnapshot`/`BuildOverrideSnapshot`/`RestoreOverridePathsOnly`/`ApplySavedCyclePart` | exact |
| `WPF_Example/UI/Reviewer/ReviewerReinspectViewModel.cs` (신규, discretion) | ViewModel (INotifyPropertyChanged) | request-response(상태 문자열 조립) | `WPF_Example/UI/Reviewer/AlignVerifyViewModel.cs` | exact |
| `WPF_Example/Custom/Sequence/Inspection/RepeatRunService.cs` (수정 — `SavedCycleRerunPlanner.BuildPartForSingleCycle` 추가) | service | CRUD(조회, 단일부품) | 같은 클래스 내 `BuildPlan`/`GroupIntoParts`/`CollectAutoTicks` | exact (같은 클래스 확장) |
| `WPF_Example/UI/Reviewer/ReviewerWindow.xaml` (수정 — 버튼 1개) | view (XAML) | request-response(클릭 배선) | 같은 파일 `border_ngCause` 패널(:226-229) | exact |
| `WPF_Example/UI/Reviewer/ReviewerWindow.xaml.cs` (수정 — `Button_ApplyRowToMain_Click` 1~2줄 + `DisplayCycle` 오버레이 필터) | controller(code-behind, 배선만) | request-response | 같은 파일 `DisplayCycle`(:136-196), `AlignVerifyWindow.xaml.cs` 의 VM 배선(:11-27) | exact |
| `WPF_Example/UI/ViewModel/ReviewMeasurementRow.cs` (수정 — `ResolveCycleImagePath` 오버로드 추가) | model/resolver | transform | 같은 파일 기존 `ResolveCycleImagePath`/`FindMeasuredOriginInShot` | exact |
| `WPF_Example/UI/ContentItem/MainView.xaml.cs` (수정 — 상태줄 배선 + 자동 Test Find 헬퍼 + Local Ref 시험 찾기 버튼) | controller(code-behind, 최소 추가만) | request-response | 같은 파일 `BtnTestFindDatum_Click`(:4475-4620) | exact |
| `WPF_Example/UI/ControlItem/InspectionListView.xaml.cs` (수정 — `SelectShotAndMeasurement` 헬퍼 추가) | controller(code-behind) | request-response(트리 선택) | 같은 파일 `SetSelectionChange`(:840-853), `AddDatumToSequence`(:1494-1506, `IsExpanded=true` 패턴) | exact |
| `WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs` (수정 — `TryResolveLocalRef` stale 분기 + `TryRecomputeStaleLocalRef` 신규 private) | service(시퀀스 계층) | event-driven(측정 직전 훅) | 같은 파일 `TryResolveLocalRef`(:1947-1983), `GrabOrLoadDatumImage`(:1080) | exact |
| `WPF_Example/DatumMeasurement.csproj` (수정 — `<Compile Include>` 등록) | config | — | 기존 `<Compile Include="Custom\Sequence\Inspection\RepeatRunService.cs" />` 류 항목 | exact |

## Pattern Assignments

### `ReviewerReinspectService.cs` (신규 service, snapshot/override-restore)

**Analog:** `WPF_Example/Custom/Sequence/Inspection/RepeatRunService.cs`

**스냅샷 구조체 패턴** (원본 :442-453, 그대로 재사용 가능 — 이미 규칙 준수):
```csharp
private sealed class SavedCycleOverrideSnapshot
{
    public readonly Dictionary<ShotConfig, string> ShotSimulImagePaths = new Dictionary<ShotConfig, string>();
    public readonly Dictionary<DatumConfig, string> DatumTeachingPaths = new Dictionary<DatumConfig, string>();
    public readonly Dictionary<DatumConfig, string> DatumTeachingPathsVertical = new Dictionary<DatumConfig, string>();
    public readonly Dictionary<DualImageEdgeDistanceMeasurement, string> DualHorizontalPaths = new Dictionary<DualImageEdgeDistanceMeasurement, string>();
    public readonly Dictionary<DualImageEdgeDistanceMeasurement, string> DualVerticalPaths = new Dictionary<DualImageEdgeDistanceMeasurement, string>();
    public readonly List<ShotConfig> OwnedShots = new List<ShotConfig>();
    public bool OfflineBefore;
    public bool OfflineSetByRerun;
    public string RecipeName;
}
```
ReviewerReinspectService 는 이 구조체를 **필드 하나(현재 활성 스냅샷)**로만 갖는다(RepeatRunService 는 부품 큐 순회용, 여기는 "버튼 한 번 = 부품 하나"이므로 `List<>`/큐 불필요 — RESEARCH.md Pattern 2).

**주입 순서 패턴** (원본 :707-765, `ApplySavedCyclePart` — "먼저 전부 원복 후 이번 값으로 덮어쓰기"):
```csharp
private void ApplyPart(SavedCycleRerunPart part)
{
    RestoreOverridePathsOnly(_snapshot);            // 1. 이미 활성 중이면 원복(D-80-11 대체 규칙)
    foreach (var shot in _snapshot.OwnedShots)
    {
        string szShotPath;
        if (part.ShotPhotoPaths.TryGetValue(shot.ShotName, out szShotPath))
        {
            shot.SimulImagePath = szShotPath;         // 2. Shot 사진(D-80-07)
        }
    }
    // ... Datum(D-80-07, 있을 때만), ZRange(D-80-08), Dual(있을 때만) 순서로 이어감
}
```

**복원 패턴** (원본 :495-520, `RestoreOverridePathsOnly` — 그대로 이식):
```csharp
private static void RestoreOverridePathsOnly(SavedCycleOverrideSnapshot snap)
{
    foreach (var pair in snap.ShotSimulImagePaths)
    {
        pair.Key.SimulImagePath = pair.Value;
    }
    // Datum/DualHorizontal/DualVertical 동일 foreach 반복, 끝에 RerunZRangeImagePaths = null
}
```

**주의:** 원본은 `?.`(null 조건) 사용처가 인근 `AddShotToSequence`(:1574 `siblingShot?.CopyTo(shot)`)에 있으나, 위 인용 범위(:442-520, :707-765)는 이미 명시적 `if`/`TryGetValue` 만 써서 규칙을 준수한다 — 새 서비스도 이 형태만 복제한다.

**MVVM 배치:** D-80-17 에 따라 `IsActive`/`StatusText`/`DisableReasonText` 는 서비스가 아니라 `ReviewerReinspectViewModel`(아래)에 둔다. 서비스는 `LoadForRow`/`Release`/`ApplyBeforeSave`/`ReapplyAfterSave` 등 순수 로직 + 이벤트(상태 변경 알림)만 노출한다.

---

### `ReviewerReinspectViewModel.cs` (신규 ViewModel)

**Analog:** `WPF_Example/UI/Reviewer/AlignVerifyViewModel.cs`

**INotifyPropertyChanged 뼈대 패턴** (원본 :29-52):
```csharp
public class ReviewerReinspectViewModel : INotifyPropertyChanged
{
    private const string STATUS_PREFIX = "리뷰어 사진 사용 중 — ";
    private const string JPG_HINT = "  (JPG 사진 — 실제 검사값과 조금 다를 수 있음)";

    public event PropertyChangedEventHandler PropertyChanged;

    private void Raise(string szName)
    {
        PropertyChangedEventHandler handler = PropertyChanged;
        if (handler != null)
        {
            handler(this, new PropertyChangedEventArgs(szName));
        }
    }

    private bool _isActive;
    public bool IsActive
    {
        get { return _isActive; }
        set { _isActive = value; Raise("IsActive"); }
    }

    private string _statusText = "";
    public string StatusText
    {
        get { return _statusText; }
        set { _statusText = value; Raise("StatusText"); }
    }
}
```
D-80-12/14 의 상태줄 문구(`"리뷰어 사진 사용 중 — " + 시각 + " 자재 " + 번호 + JPG 안내`)와 D-80-02 의 버튼 비활성 이유 문구는 이 VM 의 프로퍼티 setter/조립 메서드에서 문자열로 완성해 바인딩만 한다 — code-behind 에서 계산하지 않는다(CLAUDE.md 규칙 4 정면 요구).

**code-behind 배선 패턴** (analog: `WPF_Example/UI/Reviewer/AlignVerifyWindow.xaml.cs:11,16,22,27`):
```csharp
private readonly ReviewerReinspectViewModel _reinspectVm = new ReviewerReinspectViewModel();
// 생성자 안:
DataContext = _reinspectVm;   // 또는 상태줄 전용 UserControl 이면 그 컨트롤의 DataContext
```

---

### `RepeatRunService.cs` — `SavedCycleRerunPlanner.BuildPartForSingleCycle` (신규 메서드, 같은 클래스)

**Analog:** 같은 클래스의 `GroupIntoParts`(:1326-1354, 규칙 준수 확인됨 — 발췌):
```csharp
private static List<SavedCycleRerunPart> GroupIntoParts(List<CycleResultDto> lstTicks, InspectionSequence seq, SavedCycleRerunPlan plan) {
    List<SavedCycleRerunPart> lstParts = new List<SavedCycleRerunPart>();
    int nDatumZ = seq.GetDatumZIndex();
    SavedCycleRerunPart currentPart = null;
    int nPreDatumTickCount = 0;
    foreach (var dto in lstTicks) {
        bool bIsDatumTick = dto.ZIndex == nDatumZ;
        if (bIsDatumTick) {
            currentPart = new SavedCycleRerunPart();
            currentPart.StartTime = dto.InspectionTime;
            currentPart.IndexNumber = dto.IndexNumber;
            lstParts.Add(currentPart);
        }
        if (currentPart == null) { nPreDatumTickCount++; continue; }
        currentPart.Ticks.Add(dto);
    }
    if (nPreDatumTickCount > 0) { RecordExclusion(plan, REASON_NO_DATUM_TICK, nPreDatumTickCount + "건"); }
    return lstParts;
}
```
신규 `BuildPartForSingleCycle`은 이 형제 메서드들(`CollectAutoTicks`/`GroupIntoParts`/`FillPartDatumPhotos`/`FillPartShotPhotos`/`FillPartDualPhotos`/`FillPartZRangePhotos`)을 그대로 호출하고 `ValidatePart`만 건너뛴다(RESEARCH.md Pattern 1의 구현 스케치를 그대로 따름 — 이미 CLAUDE.md 규칙 준수 형태로 작성돼 있음, 삼항/`??`/`?.` 없음).

---

### `ReviewerWindow.xaml` / `.xaml.cs` — 버튼 추가 + `DisplayCycle` 오버레이 필터 수정

**Analog (배치 위치):** `border_ngCause` 패널(ReviewerWindow.xaml:226-229):
```xml
<Border x:Name="border_ngCause" Grid.Row="1" BorderBrush="Gray" BorderThickness="0,1,0,0" Background="#FFF8E1" Padding="6,4">
    ...
    <TextBlock x:Name="txt_ngCauseHeader" Text="NG 원인 분석 (추정)" FontSize="11" Foreground="Gray"/>
    <TextBlock x:Name="txt_ngCausePanel" FontSize="12" TextWrapping="Wrap" Foreground="#334155" .../>
```
D-80-01 이 요구한 "원인 설명 바로 아래, 크게" 배치는 이 `StackPanel` 안, `txt_ngCausePanel` 다음에 `<Button x:Name="btn_applyRowToMain" .../>` 을 추가하는 것으로 만족된다. D-80-02(비활성 이유 표시)는 버튼 옆에 `TextBlock`(바인딩: VM 의 `DisableReasonText`)을 나란히 둔다.

**code-behind 배선 (1~2줄, controller role):**
```csharp
private void Button_ApplyRowToMain_Click(object sender, RoutedEventArgs e)
{
    if (_selectedRow == null || _currentCycle == null) { return; }
    ReviewerReinspectService.LoadForRow(_currentCycle, _selectedRow);
    MainWindow owner = Owner as MainWindow;
    if (owner != null) { owner.Activate(); }
}
```

**오버레이 겹침 버그 수정 — Analog(수정 대상 자체):** `DisplayCycle`(:136-196, 특히 :176/:183-192 발췌 위):
```csharp
string szCycleImagePath = ReviewerImagePathResolver.ResolveCycleImagePath(cycle);
if (!string.IsNullOrEmpty(szCycleImagePath)) { halconViewer.LoadImage(szCycleImagePath); }

var allOverlays = cycle.Shots
    .SelectMany(s => { var t = s.FAIs; if (t == null) t = new List<FaiResultDto>(); return t; })
    .Where(f => f.LastOverlays != null)
    .SelectMany(f => f.LastOverlays)
    .ToList();
halconViewer.SetInspectionOverlays(allOverlays);
```
**교정 방향:** `ResolveCycleImagePath` 를 `out ShotResultDto ownerShot` 오버로드로 바꾸고, `allOverlays` 소스를 `cycle.Shots` 전체가 아니라 `ownerShot.FAIs` 로 좁힌다(RESEARCH.md Pattern 6 스케치 — 이미 규칙 준수 형태, `if/else`만 사용).

---

### `ReviewMeasurementRow.cs` — `ResolveCycleImagePath` 오버로드

**Analog:** 같은 클래스의 기존 `ResolveCycleImagePath`(:241-269)/`FindMeasuredOriginInShot`(:272-292) — private 헬퍼는 그대로 재사용, `public static` 오버로드 하나만 추가(위 DisplayCycle 절 스케치 참고). 기존 오버로드는 무변경(회귀 0).

---

### `MainView.xaml.cs` — 상태줄 배선 + 자동 Test Find 헬퍼 + Local Ref 시험 찾기 버튼

**Analog:** `BtnTestFindDatum_Click`(:4475-4563, DualImage 분기가 이미 다이얼로그 없이 `new HImage(path)` 직접 로드 — D-80-06 요구를 이미 만족하는 기존 코드):
```csharp
if (datum.AlgorithmTypeEnum == EDatumAlgorithm.VerticalTwoHorizontalDualImage) {
    string pathH = datum.TeachingImagePath;
    string pathV = datum.TeachingImagePath_Vertical;
    ...
    HImage imgH = null, imgV = null;
    try {
        try { imgH = new HImage(pathH); }
        catch (Exception exH) { error = "가로축 이미지 로드 실패: " + exH.Message; ok = false; }
        ...
    } finally {
        if (imgH != null) { try { imgH.Dispose(); } catch { } }
        if (imgV != null) { try { imgV.Dispose(); } catch { } }
    }
}
```
1-image 분기(:4545-4563)만 `AskTestImageSource()`를 거친다 — 신규 헬퍼 `RunTestFindForReviewer(DatumConfig datum)` 는 이 분기의 `AskTestImageSource()` 호출을 `new HImage(datum.TeachingImagePath)` 직접 로드로 바꾸고, `svc.TryFindDatum` 대신 `seq.TryRunSingleDatum(datum, img, null, out error)` 을 호출하도록 대체한다(RESEARCH.md Pattern 3 권장 — 캐시 채움 + Local Ref 사전계산). **기존 버튼 코드 자체는 절대 수정하지 않는다**(회귀 0 원칙, RESEARCH.md Anti-Pattern).

**Dispose 규약**은 위 발췌 그대로 `try/finally` + `try { x.Dispose(); } catch { }` 형태를 그대로 복제한다.

**Local Ref 시험 찾기 버튼:** RESEARCH.md Pattern 7 스케치(이미 CLAUDE.md 규칙 준수 — `if/else`, `try/finally`)를 그대로 채택:
```csharp
private void BtnTestFindLocalRef_Click(object sender, RoutedEventArgs e) {
    var etld = mParentWindow.inspectionList.SelectedParam as EdgeToLineDistanceMeasurement;
    if (etld == null) { CustomMessageBox.Show("기준 ROI 시험 찾기", "EdgeToLineDistance 측정을 선택하세요."); return; }
    DatumConfig datum = FindDatumByName(etld.DatumRef);
    bool bMissingTeach = datum == null || string.IsNullOrEmpty(datum.TeachingImagePath) || !File.Exists(datum.TeachingImagePath);
    if (bMissingTeach) {
        CustomMessageBox.Show("기준 ROI 시험 찾기", "이 측정의 기준점에 가로 사진(z1)이 티칭되지 않았습니다.");
        return;
    }
    HImage img = null;
    try {
        img = new HImage(datum.TeachingImagePath);
        halconViewer.LoadImage(datum.TeachingImagePath);
        HTuple identity;
        HOperatorSet.HomMat2dIdentity(out identity);
        LocalRefLineResult result = etld.ComputeLocalRefLine(img, identity);
        if (result.Found) {
            // 기존 overlay 렌더 경로 재사용
        } else {
            CustomMessageBox.Show("기준 ROI 시험 찾기", "띠 에지를 찾지 못했습니다: " + result.Error);
        }
    } finally {
        if (img != null) { try { img.Dispose(); } catch { } }
    }
}
```
상태줄(D-80-12) UI 엘리먼트는 VM(`ReviewerReinspectViewModel`)의 `StatusText`/`IsActive` 에 바인딩만 하고, `[해제]` 버튼 클릭 핸들러는 `ReviewerReinspectService.Release("사용자 해제")` 1줄만 호출한다(controller 역할 최소화, CLAUDE.md 규칙 4).

---

### `InspectionListView.xaml.cs` — `SelectShotAndMeasurement` 신규 헬퍼

**Analog:** 같은 파일 `SetSelectionChange`(:840-853, 이미 규칙 준수 — `if/else`, 중괄호 완비):
```csharp
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
```

**확인된 정확한 시그니처 (`NodeViewModel.cs` 전체 재확인 완료, RESEARCH.md Assumption A1 해소):**
- `public object Param { get; set; }` (:83-89, `Node.ParamData` 위임 — `ShotConfig`/`FAIConfig`/`MeasurementBase`/`DatumConfig` 인스턴스가 그대로 들어있다. `ReferenceEquals` 비교로 live 객체 탐색 가능)
- `public bool IsExpanded { get; set; }` (:117-127, setter 가 변경 시에만 `RaisePropertyChanged` — 바인딩으로 트리에 반영됨)
- `public bool IsSelected { get; set; }` (:129-141, 동일 패턴)
- `public ObservableCollection<NodeViewModel> Children { get; }` (:29-34, `LoadChildren()` 지연 로딩 후 반환)
- **이미 존재하는 조상 펼침 헬퍼:** `public void ExpandParents()` (:243-248) — 재귀적으로 부모 체인의 `IsExpanded=true` 를 설정한다. 신규 헬퍼가 직접 재구현할 필요 없이 이 메서드를 그대로 호출하면 된다.

**신규 헬퍼 스케치 (규칙 준수, `ExpandParents()` 재사용):**
```csharp
public void SelectShotAndMeasurement(ShotConfig liveShot, MeasurementBase liveMeas) {
    if (treeListBox_sequence.Items.Count == 0) { return; }
    NodeViewModel root = treeListBox_sequence.Items[0] as NodeViewModel;
    if (root == null) { return; }
    root.IsExpanded = true;
    NodeViewModel found = FindNodeByParam(root, liveMeas);
    if (found == null) { found = FindNodeByParam(root, liveShot); }
    if (found == null) { return; }
    found.ExpandParents();   // 기존 NodeViewModel.ExpandParents() 재사용 — 신규 순회 코드 불필요
    found.IsExpanded = true;
    found.IsSelected = true;
    treeListBox_sequence.ScrollIntoView(found);
}

private NodeViewModel FindNodeByParam(NodeViewModel node, object targetParam) {
    if (targetParam == null) { return null; }
    if (ReferenceEquals(node.Param, targetParam)) { return node; }
    foreach (NodeViewModel child in node.Children) {
        NodeViewModel result = FindNodeByParam(child, targetParam);
        if (result != null) { return result; }
    }
    return null;
}
```

**재진입 주의(analog):** `TreeListBox_SelectionChanged`(:880-885 부근) 기존 주석이 "컨텐츠 생성 중 동기 재선택 크래시" 를 경고 — 리뷰어 버튼 클릭 직후 이 헬퍼를 곧바로 부르지 말고 `Dispatcher.BeginInvoke(DispatcherPriority.Background, ...)` 로 한 틱 늦춘다(기존 코드의 동일 패턴 재사용, RESEARCH.md Pitfall 5).

---

### `Action_FAIMeasurement.cs` — `TryResolveLocalRef` stale 재계산

**Analog(수정 대상 자체):** `TryResolveLocalRef`(:1947-1983, 이미 규칙 준수 — 명시적 `if`, 중괄호 완비, 헝가리언 `bStale`/`szReason`):
```csharp
private bool TryResolveLocalRef(EdgeToLineDistanceMeasurement etld, InspectionSequence parentSeq2, out string szReason) {
    if (string.IsNullOrEmpty(etld.DatumRef)) {
        szReason = EdgeToLineDistanceMeasurement.LOCAL_REF_REASON_NO_DATUM;
        return false;
    }
    ...
    bool bStale = result.SettingsKey != etld.BuildLocalRefSettingsKey();
    if (bStale) {
        szReason = EdgeToLineDistanceMeasurement.LOCAL_REF_REASON_STALE;
        return false;
    }
    etld.InjectedLocalRef = result;
    szReason = null;
    return true;
}
```
**교정(수정) 방향** — `bStale` 분기 안에서 재계산 시도(RESEARCH.md Pattern 4, 이미 규칙 준수 형태):
```csharp
if (bStale) {
    bool bManualCycle = parentSeq2 == null;
    if (!bManualCycle) {
        bManualCycle = !parentSeq2.IsProtocolDrivenCycle();
    }
    if (bManualCycle) {
        LocalRefLineResult recomputed = TryRecomputeStaleLocalRef(etld, parentSeq2);
        bool bRecomputeOk = recomputed != null && recomputed.Found;
        if (bRecomputeOk) {
            etld.InjectedLocalRef = recomputed;
            szReason = null;
            return true;
        }
    }
    szReason = EdgeToLineDistanceMeasurement.LOCAL_REF_REASON_STALE;
    return false;
}
```
(원본 RESEARCH.md 스케치의 `if (bManualCycle) { ... }` 안에 있던 `||` 조건은 CLAUDE.md 규칙 2 "3개 이상 조건 금지"에는 안 걸리지만, 여기서는 가독성을 위해 `bManualCycle` 을 두 단계 `if` 로 선추출한 형태를 채택한다.)

**신규 헬퍼 `TryRecomputeStaleLocalRef`:** `GrabOrLoadDatumImage`(:1080, 기존 헬퍼 그대로 재사용)와 `HImage` Dispose 는 CLAUDE.md 5) 규칙대로 `finally`:
```csharp
private LocalRefLineResult TryRecomputeStaleLocalRef(EdgeToLineDistanceMeasurement etld, InspectionSequence parentSeq2) {
    DatumConfig datum = FindDatumByName(parentSeq2, etld.DatumRef);
    if (datum == null) { return null; }
    HTuple transform;
    bool bHasTransform = parentSeq2.TryGetDatumTransform(etld.DatumRef, out transform);
    if (!bHasTransform) { return null; }
    HImage img = null;
    try {
        img = GrabOrLoadDatumImage(datum);
        if (img == null) { return null; }
        return etld.ComputeLocalRefLine(img, transform);
    } finally {
        if (img != null) { try { img.Dispose(); } catch { } }
    }
}
```

---

### `DatumMeasurement.csproj` — 신규 .cs 등록 (D-80-18)

**Analog:** 기존 항목들(확인됨):
```xml
<Compile Include="Custom\Sequence\Inspection\RepeatRunService.cs" />
<Compile Include="UI\ControlItem\InspectionListViewModel.cs" />
<Compile Include="UI\Reviewer\ReviewerWindow.xaml.cs">
  ...
</Compile>
<Compile Include="UI\Reviewer\AlignVerifyViewModel.cs" />
<Compile Include="UI\ViewModel\ReviewMeasurementRow.cs" />
```
신규 파일(`ReviewerReinspectService.cs`, `ReviewerReinspectViewModel.cs`)은 `AlignVerifyViewModel.cs` 처럼 `<Compile Include="..." />` **단독 항목**(코드-비하인드가 아니므로 `<DependentUpon>` 불필요)으로 등록한다.

## Shared Patterns

### Dispose 규약(HImage/HObject/HTuple)
**Source:** `MainView.xaml.cs:4512-4543`(BtnTestFindDatum_Click), `Action_FAIMeasurement.cs` 전역 관례
**Apply to:** `ReviewerReinspectService`(이미지를 직접 열지 않음 — 경로 문자열만 다루므로 해당 없음), `MainView.xaml.cs` 신규 헬퍼(RunTestFindForReviewer, BtnTestFindLocalRef_Click), `Action_FAIMeasurement.TryRecomputeStaleLocalRef`
```csharp
HImage img = null;
try {
    img = new HImage(path);
    // ...
} finally {
    if (img != null) { try { img.Dispose(); } catch { } }
}
```

### 스냅샷-먼저-원복-후-재주입 (override 대체 규칙, D-80-11)
**Source:** `RepeatRunService.ApplySavedCyclePart:708`
**Apply to:** `ReviewerReinspectService.LoadForRow` — 이미 활성 상태(다른 NG 행을 이어서 고르는 경우)면 `RestoreOverridePathsOnly` 를 먼저 호출한 뒤 새 부품 값으로 덮어쓴다. 원래 스냅샷 자체는 다시 뜨지 않는다(D-80-11 "원래 값은 처음 것 유지").

### 자동 해제 훅 3곳
**Source:** `MainWindow.xaml.cs:452`(Window_Closing), `Custom/SystemHandler.cs:117`(MainRun VisionRequestType.Test), `Sequence/SequenceHandler.cs:163`(OnRecipeChanged 발화)
**Apply to:** `ReviewerReinspectService.Release(string szReason)` 를 이 3곳에 `RepeatRunService.RestoreActiveSavedCycleOverridesForShutdown()`/`ForceOfflineInspectModeOffForAutoTest()` 바로 옆에 나란히 추가한다.
```csharp
// Window_Closing
RepeatRunService.RestoreActiveSavedCycleOverridesForShutdown();
ReviewerReinspectService.Release("프로그램 종료");   // 신규 1줄
```

### MVVM 배선(코드-비하인드는 배선만)
**Source:** `WPF_Example/UI/Reviewer/AlignVerifyWindow.xaml.cs:11,16,22,27`
**Apply to:** 모든 신규 UI 로직 — `ReviewerWindow.xaml.cs`, `MainView.xaml.cs` 상태줄/해제 버튼, 신규 시험 찾기 버튼. 계산·문자열 조립은 VM/서비스가 하고, code-behind 는 이벤트 → 호출 1줄만.

## No Analog Found

없음 — 전 파일이 role-match 이상의 analog(대부분 exact, 같은 파일/클래스 내부 확장)를 갖는다. Phase 80 은 RESEARCH.md 가 명시하듯 "새 기능"이 아니라 "기존 3개 메커니즘(부품 그룹핑/스냅샷·복원/Test Find)의 재사용"이므로 새로 설계할 필요가 있는 부분(단일-부품 조회, 스냅샷 서비스, 트리 프로그램적 선택)도 전부 같은 파일 내 형제 메서드 또는 인접 파일의 확립된 패턴으로 커버된다.

## Metadata

**Analog search scope:** `WPF_Example/Custom/Sequence/Inspection/`, `WPF_Example/UI/Reviewer/`, `WPF_Example/UI/ViewModel/`, `WPF_Example/UI/ContentItem/`, `WPF_Example/UI/ControlItem/`, `WPF_Example/DatumMeasurement.csproj`, `.claude/hooks/`
**Files scanned (Read 전체 또는 타겟 범위):** `NodeViewModel.cs`(전체), `InspectionListView.xaml.cs`(선택 관련 4개 범위), `RepeatRunService.cs`(스냅샷/주입/복원 3개 범위), `ReviewerWindow.xaml.cs`(DisplayCycle), `ReviewerWindow.xaml`(border_ngCause), `MainView.xaml.cs`(BtnTestFindDatum_Click), `Action_FAIMeasurement.cs`(TryResolveLocalRef), `AlignVerifyViewModel.cs`/`AlignVerifyWindow.xaml.cs`(MVVM analog), `DatumMeasurement.csproj`(Compile Include 형식), `.claude/hooks/orphan-cs-detect.js`/`cs-style-warn.js`/`stop-build-verify.js`
**Pattern extraction date:** 2026-09-18

### `.claude/hooks/` 강제 사항 요약 (플래너 참고)
- `orphan-cs-detect.js` (PostToolUse/Write) — 신규 `.cs` 를 쓰면 `DatumMeasurement.csproj` 의 `<Compile Include>` 목록에 상대경로(`WPF_Example/` 기준, 소문자·슬래시 정규화 비교)가 있는지 확인하고, 없으면 **경고만**(비차단) 출력. `_숫자` 접미사 파일(날짜 백업)은 검사 제외.
- `cs-style-warn.js` (PostToolUse/Edit|Write) — 새로 추가된 텍스트(Write 전체 / Edit 의 new_string)에서만 `record`/`??=`/`using var`(C# 8+ 문법) 와 HALCON `SetColor` 의 비표준 색상 리터럴을 검사, **경고만**(비차단). 삼항/`?.`/`switch식`은 이 훅이 아니라 CLAUDE.md 의 수동 grep 5종으로 커밋 전 별도 확인 필요.
- `stop-build-verify.js` (Stop) — `git status --porcelain -- *.cs` 로 dirty `.cs` 가 있을 때만 `MSBuild.exe ... -p:Configuration=Debug -p:Platform=x64` 를 돌려 `error CS` 가 있으면 **세션을 block**. 신규 파일을 csproj 에 등록하지 않으면 컴파일 자체가 안 되어(orphan) 이 빌드 게이트를 통과해도 실제로는 기능이 빠진 채 배포될 위험 — orphan 경고를 반드시 확인할 것.
