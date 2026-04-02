# Phase 1: UI 재설계 - Research

**Researched:** 2026-04-02
**Domain:** WPF MVVM — TreeView, DataGrid, GridSplitter, CRUD dialog, Halcon viewer integration
**Confidence:** HIGH

---

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

- **D-01:** Shot > FAI 2계층 트리 구조로 표시한다. Shot 노드를 펼치면 하위 FAI 노드가 보인다.
- **D-02:** TreeView에서 Shot을 선택하면 캔버스에 해당 Shot 이미지가 표시되고, 결과 테이블에 해당 Shot의 FAI 목록이 표시된다.
- **D-03:** 3영역 레이아웃: 좌측 TreeView | 우상단 캔버스(이미지) | 우하단 결과 테이블.
- **D-04:** GridSplitter를 사용하여 좌우 경계 및 캔버스/테이블 상하 경계를 드래그로 크기 조절 가능하게 한다.
- **D-05:** 결과 테이블 컬럼: FAI 이름 | 거리(mm) | Spec(Min/Max) | 판정(OK/NG).
- **D-06:** OK은 초록, NG는 빨강 색상 코딩을 적용한다.
- **D-07:** 테이블 행 선택 시 캔버스에서 해당 FAI의 ROI가 하이라이트 표시된다.
- **D-08:** TreeView 상단에 툴바를 배치하고 추가(+)/삭제(−)/편집 버튼을 둔다. 선택된 노드 기준으로 동작한다.
- **D-09:** Phase 1에서 편집 가능한 속성은 이름(Name)만이다. ROI, Tolerance 등은 Phase 2~3에서 다룬다.
- **D-10:** Shot/FAI 삭제 시 확인 다이얼로그를 표시한다.

### Claude's Discretion

- 캔버스 컨트롤은 기존 MainResultViewerControl(Halcon 기반)을 재사용하되, 필요 시 확장한다.
- 기존 InspectionListView의 CompositeNode/NodeViewModel 패턴 참고 여부는 구현 시 판단한다.
- 기존 TabControl은 단일 캔버스로 교체한다.

### Deferred Ideas (OUT OF SCOPE)

None — discussion stayed within phase scope
</user_constraints>

---

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| UI-01 | TreeView에서 Shot/FAI 2계층 구조를 탐색할 수 있다 | ShotConfig.FAIList 직접 바인딩. 새 ShotFAIViewModel 계층 구조 필요. |
| UI-02 | 단일 캔버스에서 선택된 Shot의 이미지를 표시한다 (기존 5탭 제거) | MainResultViewerControl 재사용. MainView.xaml의 TabControl을 Grid 3분할로 교체. |
| UI-03 | FAI 측정 결과(거리 mm, OK/NG 판정)를 테이블로 표시한다 | DataGrid + DataTrigger 색상 바인딩. FAIConfig.MeasuredValue, IsPass 직접 사용. |
| UI-04 | Shot을 추가/삭제/수정할 수 있다 | InspectionRecipeManager.AddShot/RemoveShot 호출. TextInputBox.Show로 이름 입력. CustomMessageBox.ShowConfirmation으로 삭제 확인. |
| UI-05 | FAI를 추가/삭제/수정할 수 있다 | ShotConfig.AddFAI/RemoveFAI 호출. 동일 dialog 패턴. |
</phase_requirements>

---

## Summary

Phase 1은 기존 `MainView.xaml`의 TabControl 중심 구조를 Shot/FAI 2계층 탐색 구조로 전면 교체한다. 데이터 모델(ShotConfig, FAIConfig, InspectionRecipeManager)은 Phase 5에서 이미 완성되었으며 이 Phase에서 UI만 추가한다.

핵심 작업은 세 가지다. 첫째, `MainView.xaml`의 레이아웃을 3영역(TreeView | 캔버스 | 결과 테이블)으로 재구성한다. 둘째, Shot/FAI 데이터를 트리로 표현할 새로운 ViewModel 계층(`ShotNodeViewModel`, `FAINodeViewModel`)을 작성한다. 셋째, CRUD 동작(추가/삭제/이름수정)을 툴바 버튼으로 연결하고 기존 `InspectionRecipeManager` 메서드를 호출한다.

기존 프로젝트에는 재사용 가능한 자산이 풍부하다. `MainResultViewerControl`은 Halcon 이미지 표시 로직이 완비되어 있고, `TextInputBox.Show`와 `CustomMessageBox.ShowConfirmation`은 CRUD dialog에 그대로 사용할 수 있다. `Observable` 베이스 클래스와 `INotifyPropertyChanged` 패턴도 이미 확립되어 있다.

**Primary recommendation:** `MainView.xaml`을 `InspectionView.xaml`(또는 기존 파일 교체)로 재구성하되, 새로운 `InspectionViewModel`을 ViewModel로 두고 TreeView 선택 변경 시 캔버스와 결과 테이블이 함께 갱신되도록 연결한다.

---

## Standard Stack

### Core (이미 프로젝트에 존재)

| 컴포넌트 | 버전/위치 | 목적 | 비고 |
|---------|---------|------|------|
| WPF TreeView | .NET Framework 4.8 내장 | Shot/FAI 2계층 트리 표시 | HierarchicalDataTemplate 사용 |
| WPF DataGrid | .NET Framework 4.8 내장 | FAI 결과 테이블 | DataTrigger로 OK/NG 색상 |
| WPF GridSplitter | .NET Framework 4.8 내장 | 영역 크기 조절 | 이미 MainWindow에 사용 중 |
| `MainResultViewerControl` | `UI/ContentItem/MainResultViewerControl.xaml.cs` | Halcon 이미지 캔버스 | 재사용 |
| `InspectionRecipeManager` | `Custom/Sequence/Inspection/InspectionRecipeManager.cs` | Shot/FAI CRUD + INI 저장 | 이미 완성됨 |
| `Observable` | `UI/ViewModel/Observable.cs` | INotifyPropertyChanged 베이스 | 모든 ViewModel이 상속 |
| `TextInputBox` | `UI/Dialog/TextInputBox.cs` | 이름 입력 dialog | `TextInputBox.Show(title, initial, out text)` |
| `CustomMessageBox` | `UI/Dialog/CustomMessageBox.cs` | 삭제 확인 dialog | `ShowConfirmation(title, msg, YesNo)` |

### 교체/추가 대상

| 대상 | 현재 상태 | Phase 1 후 |
|------|---------|----------|
| `MainView.xaml` | TabControl 1개 탭 + ComboBox | 3영역 Grid(TreeView+캔버스+DataGrid) |
| `MainViewModel` | SelectedSeqName/Index | `InspectionViewModel`으로 교체 또는 확장 |
| `NodeViewModel` | ESequence/EAction 기반 | Shot/FAI 전용 VM으로 신규 작성 |

### Alternatives Considered

| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| WPF 내장 TreeView | PropertyTools.Wpf의 TreeListBox | TreeListBox는 이미 InspectionListView에서 사용 중이지만 PropertyTools 의존성 추가. 내장 TreeView가 더 단순하고 HierarchicalDataTemplate 제어가 명확함. |
| DataGrid 색상 DataTrigger | CellStyle + Converter | DataTrigger가 더 직관적이고 코드 없이 XAML만으로 처리 가능 |

---

## Architecture Patterns

### Recommended Project Structure (새 파일)

```
WPF_Example/
├── UI/
│   ├── ContentItem/
│   │   └── MainView.xaml(.cs)          ← 기존 파일을 3영역 레이아웃으로 교체
│   └── ViewModel/
│       ├── ShotNodeViewModel.cs         ← 신규: Shot 트리 노드 VM
│       ├── FAINodeViewModel.cs          ← 신규: FAI 트리 노드 VM
│       └── InspectionViewModel.cs       ← 신규: 전체 뷰 조율 VM
```

### Pattern 1: HierarchicalDataTemplate으로 2계층 TreeView

**What:** WPF TreeView의 `HierarchicalDataTemplate`을 사용하여 Shot 아래에 FAI 하위 노드를 자동으로 렌더링.

**When to use:** Shot → FAI 2계층이 고정된 구조이므로 PropertyTools TreeListBox보다 내장 TreeView가 더 단순.

```xml
<!-- Source: WPF .NET Framework 4.8 내장 패턴 -->
<TreeView ItemsSource="{Binding Shots}">
    <TreeView.Resources>
        <!-- Shot 노드 -->
        <HierarchicalDataTemplate DataType="{x:Type local:ShotNodeViewModel}"
                                  ItemsSource="{Binding FAIItems}">
            <TextBlock Text="{Binding Name}"/>
        </HierarchicalDataTemplate>
        <!-- FAI 리프 노드 -->
        <DataTemplate DataType="{x:Type local:FAINodeViewModel}">
            <TextBlock Text="{Binding Name}"/>
        </DataTemplate>
    </TreeView.Resources>
</TreeView>
```

### Pattern 2: 3영역 Grid + GridSplitter

**What:** `MainView.xaml`의 최상위 레이아웃을 Column 2개(좌=TreeView, 우=Row 2개로 분할)로 구성.

```xml
<!-- Source: 기존 MainWindow.xaml 패턴 참고 -->
<Grid>
    <Grid.ColumnDefinitions>
        <ColumnDefinition Width="25*"/>
        <ColumnDefinition Width="5"/>
        <ColumnDefinition Width="75*"/>
    </Grid.ColumnDefinitions>
    <!-- 좌: TreeView + 툴바 -->
    <!-- 수직 GridSplitter -->
    <Grid Grid.Column="2">
        <Grid.RowDefinitions>
            <RowDefinition Height="6*"/>
            <RowDefinition Height="5"/>
            <RowDefinition Height="4*"/>
        </Grid.RowDefinitions>
        <!-- 우상단: 캔버스(MainResultViewerControl) -->
        <!-- 수평 GridSplitter -->
        <!-- 우하단: DataGrid -->
    </Grid>
</Grid>
```

### Pattern 3: DataGrid OK/NG 색상 코딩

**What:** `DataGrid.RowStyle` + `DataTrigger`로 `IsPass` 속성에 따라 행 배경색 변경.

```xml
<!-- Source: WPF DataTrigger 표준 패턴 -->
<DataGrid ItemsSource="{Binding SelectedShotFAIResults}" AutoGenerateColumns="False"
          IsReadOnly="True" SelectionChanged="FAIResults_SelectionChanged">
    <DataGrid.RowStyle>
        <Style TargetType="DataGridRow">
            <Style.Triggers>
                <DataTrigger Binding="{Binding IsPass}" Value="True">
                    <Setter Property="Background" Value="#FF204020"/>
                    <!-- 어두운 초록 (기존 프로젝트 색상 테마와 맞춤) -->
                </DataTrigger>
                <DataTrigger Binding="{Binding IsPass}" Value="False">
                    <Setter Property="Background" Value="#FF402020"/>
                    <!-- 어두운 빨강 -->
                </DataTrigger>
            </Style.Triggers>
        </Style>
    </DataGrid.RowStyle>
    <DataGrid.Columns>
        <DataGridTextColumn Header="FAI" Binding="{Binding FAIName}"/>
        <DataGridTextColumn Header="거리(mm)" Binding="{Binding MeasuredValue, StringFormat=F3}"/>
        <DataGridTextColumn Header="Spec(Min)" Binding="{Binding SpecMin, StringFormat=F3}"/>
        <DataGridTextColumn Header="Spec(Max)" Binding="{Binding SpecMax, StringFormat=F3}"/>
        <DataGridTextColumn Header="판정" Binding="{Binding JudgeText}"/>
    </DataGrid.Columns>
</DataGrid>
```

### Pattern 4: CRUD 툴바 (선택 노드 기준 동작)

**What:** TreeView 상단 ToolBar에 추가/삭제/편집 버튼. 선택된 노드 타입(Shot/FAI)에 따라 동작이 달라진다.

```csharp
// Source: 기존 InspectionListView.cs 패턴 참고
private void Btn_Add_Click(object sender, RoutedEventArgs e) {
    if (ViewModel.SelectedNode is ShotNodeViewModel) {
        // Shot이 선택된 경우 → FAI 추가
        bool ok = TextInputBox.Show("FAI 이름 입력", "FAI_0", out string name);
        if (!ok) return;
        ViewModel.AddFAIToSelectedShot(name);
    } else if (ViewModel.SelectedNode == null) {
        // 아무것도 선택되지 않은 경우 → Shot 추가
        bool ok = TextInputBox.Show("Shot 이름 입력", "SHOT_0", out string name);
        if (!ok) return;
        ViewModel.AddShot(name);
    }
}
```

### Pattern 5: TreeView 선택 → 캔버스 + 테이블 연동

**What:** TreeView의 `SelectedItemChanged` 이벤트(또는 `SelectedItem` 바인딩)로 Shot 선택 시 캔버스와 테이블을 함께 갱신.

```csharp
// Source: 기존 MainView.cs DisplayContextToViewer 패턴 참고
private void TreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e) {
    if (e.NewValue is ShotNodeViewModel shotVm) {
        // 캔버스: Shot 이미지 표시
        HImage img = shotVm.ShotConfig.GetImage();
        if (img != null) {
            halconViewer.LoadImage(img);
        }
        // 결과 테이블: 해당 Shot의 FAI 결과 목록으로 교체
        ViewModel.SelectedShotFAIResults = shotVm.FAIResultRows;
    }
}
```

### Pattern 6: FAI 행 선택 → ROI 하이라이트 (D-07)

**What:** DataGrid의 `SelectionChanged`로 선택된 FAI의 ROI를 `MainResultViewerControl.UpdateDisplayState`에 전달.

```csharp
// Source: 기존 MainView.cs UpdateDisplayState 호출 패턴
private void FAIResults_SelectionChanged(object sender, SelectionChangedEventArgs e) {
    if (dataGrid.SelectedItem is FAIResultRow row) {
        var roi = row.GetRoiDefinition();
        halconViewer.UpdateDisplayState(new[] { roi }, null, null);
    }
}
```

### Anti-Patterns to Avoid

- **TabControl 유지:** 기존 TabControl을 그대로 두고 새 탭을 추가하는 방식 — D-03 결정과 충돌. 완전 교체해야 한다.
- **NodeViewModel 재사용:** 기존 `NodeViewModel`은 `ESequence`/`EAction` 기반이라 Shot/FAI에 맞지 않음. Shot/FAI 전용 VM을 새로 작성한다.
- **결과 테이블을 ListBox로:** DataGrid를 사용해야 컬럼별 정렬과 행 선택이 자연스럽게 된다.
- **직접 FAIConfig 리스트 바인딩:** `FAIConfig`는 `[Browsable(false)]` 등 속성 그리드용 어노테이션이 섞여 있으므로 결과 테이블용 Row DTO(`FAIResultRow`)를 별도 정의하는 것이 깔끔하다.
- **이미지 없는 Shot 선택 시 예외:** `ShotConfig.GetImage()`가 null을 반환할 수 있으므로 null 체크 필수.

---

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| 이름 입력 dialog | 별도 Window 클래스 | `TextInputBox.Show` | 이미 프로젝트에 구현됨 |
| 삭제 확인 dialog | 별도 확인 Window | `CustomMessageBox.ShowConfirmation` | 이미 프로젝트에 구현됨 |
| Shot/FAI CRUD 로직 | List 직접 조작 | `InspectionRecipeManager.AddShot/RemoveShot`, `ShotConfig.AddFAI/RemoveFAI` | 이미 구현됨. INI 저장 로직 포함 |
| Halcon 이미지 표시 | HWindow 직접 조작 | `MainResultViewerControl.LoadImage` + `UpdateDisplayState` | 패닝/줌/오버레이 모두 내장 |
| OK/NG 색상 로직 | Converter 클래스 | DataTrigger (XAML) | Converter 없이 IsPass bool로 직접 바인딩 가능 |

**Key insight:** 데이터 레이어(CRUD, INI 저장)와 이미지 뷰어는 완성된 자산을 호출만 하면 된다. Phase 1에서 새로 작성할 것은 ViewModel 3개와 MainView.xaml 레이아웃 재구성뿐이다.

---

## Common Pitfalls

### Pitfall 1: TreeView SelectedItem 바인딩 함정

**What goes wrong:** WPF TreeView의 `SelectedItem`은 읽기 전용(read-only)이라 Two-way 바인딩이 안 된다.

**Why it happens:** WPF TreeView 설계상 SelectedItem은 DependencyProperty이나 setter가 없다.

**How to avoid:** `SelectedItemChanged` 이벤트 핸들러에서 ViewModel의 `SelectedNode` 프로퍼티를 코드비하인드에서 직접 설정하거나, TreeViewItem의 `IsSelected` 속성을 NodeViewModel에 바인딩한다.

```csharp
// code-behind에서 직접 설정 (프로젝트 기존 패턴과 일치)
private void TreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e) {
    ViewModel.SelectedNode = e.NewValue as ShotFAINodeBase;
}
```

### Pitfall 2: ObservableCollection 미사용으로 트리 갱신 안 됨

**What goes wrong:** Shot/FAI를 추가/삭제해도 TreeView가 갱신되지 않는다.

**Why it happens:** `List<T>`는 INotifyCollectionChanged를 구현하지 않으므로 WPF에서 변경을 감지하지 못한다.

**How to avoid:** ViewModel에서 Shot 목록을 `ObservableCollection<ShotNodeViewModel>`로 선언해야 한다. 단, `InspectionRecipeManager.Shots`는 `List<ShotConfig>`이므로 ViewModel이 CRUD 이후 직접 `ObservableCollection`을 동기화한다.

### Pitfall 3: DataGrid IsReadOnly + 행 선택 충돌

**What goes wrong:** DataGrid에서 행 선택 시 편집 모드로 진입하거나 의도치 않은 포커스 이동 발생.

**Why it happens:** DataGrid 기본 SelectionUnit이 FullRow이지만 IsReadOnly=False이면 셀 편집이 활성화된다.

**How to avoid:** `IsReadOnly="True"` 명시, `SelectionUnit="FullRow"` 설정.

### Pitfall 4: HImage Dispose 타이밍

**What goes wrong:** TreeView에서 Shot을 빠르게 전환하면 이전 이미지가 Dispose된 뒤 캔버스가 참조해 예외 발생.

**Why it happens:** `ShotConfig.GetImage()`는 복사본을 반환하지만 캔버스에 전달하기 전에 지역변수가 GC되면 문제가 될 수 있다.

**How to avoid:** `MainResultViewerControl.LoadImage(HImage)`는 내부에서 이미지를 보관하므로 호출 후 지역변수를 `Dispose`하는 것이 안전하다. 기존 `MainView.GrabAndDisplay` 패턴을 따른다.

### Pitfall 5: Shot/FAI 삭제 후 인덱스 불일치

**What goes wrong:** `InspectionRecipeManager.RemoveShot(index)` 호출 후 ViewModel의 ObservableCollection과 인덱스가 불일치.

**Why it happens:** List 인덱스와 ObservableCollection 인덱스가 별도로 관리되면 drift 발생.

**How to avoid:** Shot 삭제 시 ViewModel에서 `_shots.RemoveAt(vmIndex)`와 `_recipeManager.RemoveShot(vmIndex)`를 원자적으로 처리. 항상 ViewModel 인덱스 기준으로 제거한다.

---

## Code Examples

### ShotNodeViewModel 골격

```csharp
// Source: 기존 NodeViewModel.cs + Observable.cs 패턴 참고
public class ShotNodeViewModel : Observable
{
    public ShotConfig ShotConfig { get; }

    private ObservableCollection<FAINodeViewModel> _faiItems;
    public ObservableCollection<FAINodeViewModel> FAIItems
    {
        get { return _faiItems; }
    }

    public string Name
    {
        get { return ShotConfig.ShotName; }
        set
        {
            ShotConfig.ShotName = value;
            RaisePropertyChanged("Name");
        }
    }

    public ShotNodeViewModel(ShotConfig shot)
    {
        ShotConfig = shot;
        _faiItems = new ObservableCollection<FAINodeViewModel>();
        foreach (var fai in shot.FAIList)
        {
            _faiItems.Add(new FAINodeViewModel(fai));
        }
    }
}
```

### FAIResultRow DTO

```csharp
// 결과 테이블용 경량 DTO (FAIConfig 직접 노출 회피)
public class FAIResultRow : Observable
{
    private readonly FAIConfig _fai;

    public string FAIName => _fai.FAIName;
    public double MeasuredValue => _fai.MeasuredValue;
    public double SpecMin => _fai.NominalValue - Math.Abs(_fai.LowerTolerance);
    public double SpecMax => _fai.NominalValue + Math.Abs(_fai.UpperTolerance);
    public bool IsPass => _fai.IsPass;
    public string JudgeText => _fai.IsPass ? "OK" : "NG";

    public FAIResultRow(FAIConfig fai) { _fai = fai; }

    public RoiDefinition GetRoiDefinition()
    {
        return new RoiDefinition
        {
            Id = _fai.FAIName,
            Name = _fai.FAIName,
            Row1 = _fai.ROI_Row - _fai.ROI_Length2,
            Column1 = _fai.ROI_Col - _fai.ROI_Length1,
            Row2 = _fai.ROI_Row + _fai.ROI_Length2,
            Column2 = _fai.ROI_Col + _fai.ROI_Length1,
            IsTaught = true
        };
    }

    public void Refresh()
    {
        RaisePropertyChanged("MeasuredValue");
        RaisePropertyChanged("IsPass");
        RaisePropertyChanged("JudgeText");
    }
}
```

### InspectionViewModel 핵심 구조

```csharp
public class InspectionViewModel : Observable
{
    private readonly InspectionRecipeManager _recipeManager;
    private ObservableCollection<ShotNodeViewModel> _shots;

    public ObservableCollection<ShotNodeViewModel> Shots => _shots;

    private ShotFAINodeBase _selectedNode;
    public ShotFAINodeBase SelectedNode
    {
        get { return _selectedNode; }
        set { _selectedNode = value; RaisePropertyChanged("SelectedNode"); OnSelectionChanged(); }
    }

    private ObservableCollection<FAIResultRow> _selectedShotFAIResults;
    public ObservableCollection<FAIResultRow> SelectedShotFAIResults
    {
        get { return _selectedShotFAIResults; }
        set { _selectedShotFAIResults = value; RaisePropertyChanged("SelectedShotFAIResults"); }
    }

    // AddShot, RemoveShot, AddFAI, RemoveFAI 메서드
    // → InspectionRecipeManager 위임 후 _shots ObservableCollection 동기화
}
```

---

## Integration Points

### MainWindow.xaml 변경 불필요

현재 `MainWindow.xaml`은 `<ui:MainView x:Name="mainView">` 를 그대로 유지한다. `MainView.xaml` 내부만 교체한다. `InspectionListView`(우측 패널)는 기존 시퀀스 트리 용도로 유지되므로 충돌하지 않는다.

### InspectionRecipeManager 접근 경로

```csharp
// SystemHandler → SequenceHandler → 커스텀 등록된 시퀀스 → InspectionRecipeManager
// 현재 Phase 5에서 어디에 배치되었는지 확인 필요
// Action_FAIMeasurement.cs 내에서 InspectionRecipeManager를 보유할 가능성 높음
```

Phase 1 계획 시 `InspectionRecipeManager` 인스턴스 접근 경로를 `Action_FAIMeasurement.cs`에서 확인 후 ViewModel에 주입하거나 `SystemHandler.Handle`을 통해 접근하는 방식을 결정한다.

---

## Validation Architecture

nyquist_validation이 활성화되어 있으나, 이 프로젝트는 테스트 프레임워크가 없다(Python mock 스크립트만 존재, xUnit/NUnit/MSTest 없음). WPF UI 레이어는 자동화 단위 테스트 적용 범위가 제한적이다.

### Test Framework
| Property | Value |
|----------|-------|
| Framework | 없음 (No test project detected) |
| Config file | 없음 |
| Quick run command | 수동 UI 검증 (build + run) |
| Full suite command | `msbuild WPF_Example\DatumMeasurement.csproj /p:Configuration=Debug` |

### Phase Requirements → Test Map

| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| UI-01 | TreeView Shot 노드 펼치면 FAI 표시 | manual UI | 빌드 후 직접 확인 | N/A |
| UI-02 | Shot 선택 시 캔버스 이미지 표시 | manual UI | 빌드 후 직접 확인 | N/A |
| UI-03 | FAI 결과 테이블 색상 표시 | manual UI | 빌드 후 직접 확인 | N/A |
| UI-04 | Shot CRUD 동작 | manual UI | 빌드 후 직접 확인 | N/A |
| UI-05 | FAI CRUD 동작 | manual UI | 빌드 후 직접 확인 | N/A |

### Wave 0 Gaps

- 테스트 프레임워크가 없으므로 자동화 테스트 설정 불필요. 각 Wave 완료 후 빌드 성공 여부로 검증.
- 빌드 명령: `msbuild "WPF_Example\DatumMeasurement.csproj" /p:Configuration=Debug /p:Platform=x64`

---

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| .NET Framework 4.8 | WPF 빌드 | 전제 | 4.8 | 없음 |
| HALCON 24.11 | MainResultViewerControl | 전제 | 24.11 | SIMUL_MODE |
| MSBuild 15.0 | 빌드 | 전제 | 15.0 | — |

Phase 1은 외부 서비스나 새 SDK에 의존하지 않는다. 모든 의존성은 기존 프로젝트 설정에 포함되어 있다.

---

## Open Questions

1. **InspectionRecipeManager 인스턴스 위치**
   - What we know: `InspectionRecipeManager`는 Phase 5에서 생성되었으나 `Action_FAIMeasurement.cs` 내부에 있을 가능성이 높다
   - What's unclear: ViewModel이 어떤 경로로 이 인스턴스에 접근해야 하는가 (`SystemHandler.Handle`을 통해 노출되었는지 여부)
   - Recommendation: `Action_FAIMeasurement.cs` 상단을 확인하여 인스턴스 보유 위치를 파악한 뒤, `SystemHandler.Handle`에 공개 프로퍼티로 노출하거나 ViewModel 생성 시 직접 주입하는 방식을 선택

2. **Shot 이미지 없는 초기 상태 처리**
   - What we know: `ShotConfig.HasImage`가 false인 경우 캔버스에 표시할 것이 없다
   - What's unclear: 기본 placeholder 이미지를 표시할지 빈 캔버스를 유지할지
   - Recommendation: 빈 캔버스 유지 + `label_message`에 "No Image" 표시 (기존 `MainView.xaml`의 현재 상태와 동일)

3. **결과 테이블 검사 전 상태**
   - What we know: 검사 전에는 `FAIConfig.MeasuredValue=0`, `IsPass=false`
   - What's unclear: IsPass=false가 검사 미실시인지 NG인지 구분 필요
   - Recommendation: `FAIConfig`에 `HasResult` bool 필드 추가(또는 MeasuredValue=-1 센티넬 값)로 "검사 전" 상태를 별도로 표현하고 테이블에서 "—"로 표시

---

## Project Constraints (from CLAUDE.md)

- Tech stack: .NET Framework 4.8 + WPF + Halcon 24.11 — 변경 불가
- Architecture: SystemHandler 싱글턴 + SequenceBase/ActionBase 패턴 유지
- C# 7.2만 사용 (C# 8.0+ 기능 금지: nullable reference types, switch expressions, record types)
- NuGet은 packages.config 형식 (SDK-style PackageReference 아님)
- 코드 스타일: 편집하는 파일의 기존 스타일을 따름 (K&R 또는 Allman 혼용 금지)
- UI 파일: `WPF_Example/UI/**/*.xaml` 위치 준수
- 에러 처리: UI 이벤트 핸들러에서 예외가 발생하면 `FinishAction(EContextResult.Error)` 대신 `CustomMessageBox.Show`로 표시
- 새 공개 유틸리티 메서드에는 XML doc 주석 필요
- `HImage` 사용 시 `using` 또는 명시적 `Dispose()` 필수
- Bool 플래그: `Is`, `Has` 접두사 사용
- ViewModel 네이밍: `<Feature>ViewModel.cs`
- 신규 서비스: `<Domain>Service.cs` 패턴

---

## Sources

### Primary (HIGH confidence)

- 프로젝트 소스 직접 분석 (`MainView.xaml`, `MainView.xaml.cs`, `InspectionListView.xaml`, `NodeViewModel.cs`, `Node.cs`, `Observable.cs`)
- Phase 5 데이터 모델 직접 분석 (`ShotConfig.cs`, `FAIConfig.cs`, `InspectionRecipeManager.cs`)
- WPF HierarchicalDataTemplate — .NET Framework 4.8 내장, 공식 문서 일치
- WPF DataGrid DataTrigger — .NET Framework 4.8 내장, 표준 패턴

### Secondary (MEDIUM confidence)

- 기존 `MainResultViewerControl.xaml.cs` 공개 API(`LoadImage`, `UpdateDisplayState`) 분석 기반 통합 패턴

---

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — 모든 컴포넌트가 이미 프로젝트에 존재하며 소스 확인됨
- Architecture: HIGH — 기존 패턴 직접 확인 후 확장한 설계
- Pitfalls: HIGH — WPF TreeView/DataGrid의 알려진 특성 + 프로젝트 특수 사항 모두 확인됨
- Integration: MEDIUM — InspectionRecipeManager 접근 경로는 Action_FAIMeasurement.cs 확인 필요

**Research date:** 2026-04-02
**Valid until:** 2026-05-02 (스택이 안정적이므로 30일)
