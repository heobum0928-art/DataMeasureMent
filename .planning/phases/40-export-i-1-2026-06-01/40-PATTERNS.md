# Phase 40: 결과 분석 & Export I — 패턴 맵

**작성일:** 2026-06-01
**분석 대상 파일 수:** 6개 (신규 4 + 수정 2)
**아날로그 발견:** 6 / 6

---

## 파일 분류

| 신규/수정 파일 | 역할 | 데이터 흐름 | 가장 가까운 아날로그 | 매칭 품질 |
|---------------|------|------------|-------------------|----------|
| `WPF_Example/UI/ViewModel/CycleResultDto.cs` | model | CRUD | `WPF_Example/UI/ViewModel/MeasurementResultRow.cs` | role-match |
| `WPF_Example/Custom/Sequence/Inspection/CycleResultSerializer.cs` | service | file-I/O | `WPF_Example/Utility/RawImageSaveService.cs` | exact |
| `WPF_Example/Custom/Export/ExcelExportService.cs` | service | transform | `WPF_Example/Utility/RawImageSaveService.cs` (파일 저장 구조) | role-match |
| `WPF_Example/UI/Reviewer/ReviewerWindow.xaml(.cs)` | component(Window) | request-response | `WPF_Example/UI/ProcessMonitor/ProcessMonitorWindow.xaml.cs` | role-match |
| `WPF_Example/MainWindow.xaml.cs` (수정) | component | request-response | 자기 자신 — `PopupView()` + `EPageType` 확장 | exact |
| `WPF_Example/UI/MenuBar.xaml.cs` (수정) | component | request-response | 자기 자신 — 버튼 클릭 → `PopupView()` 패턴 | exact |

---

## 패턴 할당

---

### `WPF_Example/UI/ViewModel/CycleResultDto.cs` (model, CRUD)

**아날로그:** `WPF_Example/UI/ViewModel/MeasurementResultRow.cs`
**보조 아날로그:** `WPF_Example/Custom/Sequence/Inspection/FAIConfig.cs:94-114`, `WPF_Example/Custom/Sequence/Inspection/MeasurementBase.cs:43-58`

#### 임포트 패턴 (`MeasurementResultRow.cs:1-5`)
```csharp
using System;
using ReringProject.Sequence;

namespace ReringProject.UI
{
```
**CycleResultDto 적용:** 네임스페이스는 `ReringProject.UI` 사용. 외부 의존(Halcon 등) 없이 순수 CLR 타입만 사용.

#### 핵심 DTO 패턴 (`MeasurementResultRow.cs:8-16`, `FAIConfig.cs:98-114`, `MeasurementBase.cs:43-58`)
```csharp
// MeasurementResultRow — 라이브 측정값 래핑 DTO
public class MeasurementResultRow : Observable
{
    private readonly MeasurementBase _measurement;
    private readonly string _faiName;
    public MeasurementResultRow(MeasurementBase measurement, string faiName) { ... }
    public string FAIName { get { return _faiName; } }
    public double NominalValue { get { return _measurement.NominalValue; } }
    public bool HasResult { get { return _measurement.LastHasResult; } }  // CO-23-01: 0.0도 정상
    public string JudgeText { get { return HasResult ? (IsPass ? "OK" : "NG") : "—"; } }
}

// FAIConfig — [JsonIgnore] LastOverlays (JSON 직렬화 제외 예시)
[Newtonsoft.Json.JsonIgnore]
public List<EdgeInspectionOverlay> LastOverlays { get; set; } = new List<EdgeInspectionOverlay>();

// MeasurementBase — 직렬화 대상 필드들
public double LastMeasuredValue { get; set; }
public bool LastJudgement { get; set; }     // true=OK
public bool LastHasResult { get; set; }     // CO-23-01: 0.0 구분
public string LastSkipReason { get; set; }  // null or "DATUM_FAIL"
```

**CycleResultDto 적용:**
- `Observable` 상속 불필요 — 순수 직렬화 DTO이므로 일반 `class`
- `[JsonIgnore]` 사용하지 않음 — DTO 계층은 모든 필드를 직렬화 대상으로 노출
- `FAIConfig.LastOverlays`는 `[JsonIgnore]`이므로 반드시 DTO 계층(`FaiResultDto`)에서 별도 복사 후 직렬화
- `JudgeText` 로직(`HasResult ? (IsPass ? "OK" : "NG") : "—"`) → DTO의 `OverallJudgement` 필드 값 규칙과 동일하게 3분기("OK"/"NG"/"DETECT_FAIL") 사용
- `LastSkipReason == "DATUM_FAIL"` → xlsx에서 "DETECT FAIL" 표시 (MeasurementBase.cs:54-58 주석 근거)

#### EdgeInspectionOverlay 직렬화 안전성 확인 (`EdgeInspectionOverlay.cs:22-48`)
```csharp
public class EdgeInspectionOverlay
{
    public string RoiId { get; set; }                              // CLR string
    public List<EdgeInspectionPoint> Points { get; set; }          // CLR List
    public double LineRow1 { get; set; }                           // CLR double
    public double LineColumn1 { get; set; }
    public double LineRow2 { get; set; }
    public double LineColumn2 { get; set; }
}
public class EdgeInspectionPoint
{
    public double Row { get; set; }
    public double Column { get; set; }
}
```
**모든 필드가 CLR 타입** → `[JsonIgnore]` 없이 `Newtonsoft.Json.JsonConvert.SerializeObject` 직접 사용 가능. HalconDotNet 타입 없음 (RESEARCH Pitfall 3 해소 확인).

---

### `WPF_Example/Custom/Sequence/Inspection/CycleResultSerializer.cs` (service, file-I/O)

**아날로그:** `WPF_Example/Utility/RawImageSaveService.cs:71-93`

#### 임포트 패턴 (`RawImageSaveService.cs:1-8`)
```csharp
using ReringProject.Network;
using ReringProject.Setting;
using System;
using System.IO;
using System.Threading.Tasks;     // Task.Factory.StartNew 패턴

namespace ReringProject.Utility {  // CycleResultSerializer 는 Custom/Sequence/Inspection → ReringProject.Sequence 또는 신규 네임스페이스
```
**CycleResultSerializer 적용:** `using Newtonsoft.Json;` 추가. 네임스페이스는 `ReringProject.Sequence` (Custom 폴더 규칙).

#### 날짜 폴더 저장 패턴 (`RawImageSaveService.cs:71-89`) — **복사 핵심**
```csharp
private static void SaveRequest(RawImageSaveRequest request) {
    try {
        // 날짜 기반 폴더 경로 구성 패턴
        string baseDirectory = SystemHandler.Handle.Setting.GetLogSavePath(
            ELogType.Image, "Raw", request.Timestamp.ToString("yyyyMMdd"));
        Directory.CreateDirectory(baseDirectory);

        // 파일명 구성 — HHmmssfff + 식별자 조합
        string fileName = string.Format("{0}_{1}_{2}_{3}.png",
            request.Timestamp.ToString("HHmmssfff"), sequence, action, testId);
        string filePath = Path.Combine(baseDirectory, fileName);

        request.Image.WriteImage("png", 0, filePath);
    }
    catch (Exception ex) {
        Logging.PrintErrLog((int)ELogType.Error,
            string.Format("Raw image save failed: {0}", ex.Message));
    }
    finally {
        request.Dispose();
    }
}
```
**CycleResultSerializer 적용 차이점:**
- `GetLogSavePath(ELogType.Image, ...)` 대신 `SystemHandler.Handle.Setting.ResultSavePath` + `Path.Combine(dateDir, cycleDir)` 직접 사용 (D-03)
- `WriteImage` 대신 `File.WriteAllText(jsonPath, json, Encoding.UTF8)`
- `finally { request.Dispose(); }` → JSON 서비스에는 Dispose 불필요이나 예외 격리는 동일하게 적용

#### 비동기 예외 격리 패턴 (`SequenceBase.cs:382-415`)
```csharp
// SaveResultImage 에서 가져온 비동기 + 예외 격리 패턴
Task.Factory.StartNew((object obj) => {
    try {
        // ... 파일 저장 ...
    }
    catch (Exception ex) {
        try { Logging.PrintErrLog((int)ELogType.Error, "[CycleResultSerializer] Save failed: " + ex.Message); } catch { }
    }
    finally {
        // 리소스 정리
    }
}, snapshot);
```
**적용 근거:** `AddResponse()` 내부에서 직렬화 예외 시 TCP 응답 누락 방지 (RESEARCH Pitfall 4). `SaveResultImage`의 Task 격리 패턴을 그대로 사용.

#### cycle 완료 wiring 위치 (`InspectionSequence.cs:75-124`)
```csharp
protected override void AddResponse() {
    if (RequestPacket == null) return;
    var recipeManager = SystemHandler.Handle.Sequences.RecipeManager;

    // ... anyDatumSkip / allPass 계산 루프 (Shots/FAIs 순회) ...

    if (anyDatumSkip) responsePacket.Result = EVisionResultType.NotExist;
    else if (!allPass) responsePacket.Result = EVisionResultType.NG;
    else responsePacket.Result = EVisionResultType.OK;

    // [신규 삽입 위치] — 종합판정 확정 직후, ResponseQueue.Enqueue 직전
    // recipeManager.Shots 의 전 FAI 결과가 이미 채워진 시점
    ResponseQueue.Enqueue(responsePacket);   // ← 기존 마지막 줄
}
```
**삽입 위치:** `responsePacket.Result = EVisionResultType.OK/NG/NotExist;` 결정 직후, `ResponseQueue.Enqueue(responsePacket);` 직전에 `CycleResultSerializer.SaveAsync(...)` 호출.

---

### `WPF_Example/Custom/Export/ExcelExportService.cs` (service, transform)

**아날로그:** `WPF_Example/Utility/RawImageSaveService.cs` (파일 저장 구조) + `WPF_Example/UI/ViewModel/MeasurementResultRow.cs` (결과 컬럼 구성)

#### 임포트 패턴
```csharp
using ClosedXML.Excel;
using ReringProject.UI;             // CycleResultDto 참조
using System;
using System.IO;

namespace ReringProject.Utility {   // 또는 ReringProject.Export (신규 네임스페이스 선택 시)
```

#### 핵심 xlsx 빌드 패턴 (RESEARCH Pattern 4 기반, `MeasurementResultRow.cs:40-66` 컬럼 매핑)
```csharp
// D-06 메타 헤더 블록
ws.Cell(1, 1).Value = "모델명";
ws.Cell(1, 2).Value = cycle.RecipeName;
ws.Cell(2, 1).Value = "검사일시";
ws.Cell(2, 2).Value = cycle.InspectionTime.ToString("yyyy-MM-dd HH:mm:ss");
ws.Cell(3, 1).Value = "종합판정";
ws.Cell(3, 2).Value = cycle.OverallJudgement;

// D-05 행 구조 — MeasurementResultRow 컬럼 순서 참고
// Shot / FAI / 측정명 / Nominal / Tol+ / Tol- / 측정값 / 판정 / 이미지
ws.Cell(row, 4).Value = meas.NominalValue;          // MeasurementResultRow.NominalValue
ws.Cell(row, 5).Value = meas.TolerancePlus;
ws.Cell(row, 6).Value = meas.ToleranceMinus;
ws.Cell(row, 7).Value = meas.LastHasResult ? meas.LastMeasuredValue : double.NaN;  // HasResult 구분
ws.Cell(row, 8).Value = meas.LastSkipReason == "DATUM_FAIL"
    ? "DETECT FAIL"
    : (meas.LastHasResult ? (meas.LastJudgement ? "OK" : "NG") : "-");  // JudgeText 로직 재사용

// D-07 하이퍼링크
ws.Cell(row, 9).Value = "이미지 열기";
ws.Cell(row, 9).Hyperlink = new XLHyperlink(shot.ResultImagePath);  // 절대 경로
```

#### 에러 처리 패턴 (`RawImageSaveService.cs:86-93`)
```csharp
// 파일 저장 서비스의 예외 처리 패턴 그대로 적용
try {
    // ... wb.SaveAs(outputPath) ...
}
catch (Exception ex) {
    Logging.PrintErrLog((int)ELogType.Error,
        string.Format("[ExcelExportService] Export failed: {0}", ex.Message));
}
```

---

### `WPF_Example/UI/Reviewer/ReviewerWindow.xaml(.cs)` (component/Window, request-response)

**아날로그:** `WPF_Example/UI/ProcessMonitor/ProcessMonitorWindow.xaml.cs` (독립 Window 구조)
**보조 아날로그:** `WPF_Example/UI/Device/DeviceSelector.xaml.cs:250-263` (Ookii 다이얼로그)

#### XAML Window 선언 패턴 (`ProcessMonitorWindow.xaml:1-8`)
```xml
<Window x:Class="ReringProject.UI.ReviewerWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:local="clr-namespace:ReringProject.UI"
        Title="결과 리뷰어" Height="800" Width="1200"
        WindowStartupLocation="CenterOwner">
```
**적용:** `WindowStartupLocation="CenterOwner"`, Owner는 MainWindow에서 설정.

#### 코드-비하인드 기본 구조 (`ProcessMonitorWindow.xaml.cs:8-20`)
```csharp
namespace ReringProject.UI {
    public partial class ReviewerWindow : Window {
        // ViewModel 또는 직접 필드
        private CycleResultDto _currentCycle;

        public ReviewerWindow() {
            InitializeComponent();
            // DataContext = new ReviewerViewModel();  // 필요 시 MVVM 적용
        }
    }
}
```

#### Ookii 폴더 다이얼로그 패턴 (`DeviceSelector.xaml.cs:250-263`) — **복사 핵심**
```csharp
private void Button_LoadFolder_Click(object sender, RoutedEventArgs e) {
    Ookii.Dialogs.Wpf.VistaFolderBrowserDialog dlg =
        new Ookii.Dialogs.Wpf.VistaFolderBrowserDialog();
    dlg.Multiselect = false;
    // DeviceSelector 는 RootFolder 도 설정하나 Reviewer 는 SelectedPath 만으로 충분
    dlg.SelectedPath = SystemHandler.Handle.Setting.ResultSavePath;  // D-09: ResultSavePath 기본
    if ((bool)dlg.ShowDialog()) {
        LoadCycleFolders(dlg.SelectedPath);
    }
}
```
**차이점:** `dlg.RootFolder = Environment.SpecialFolder.CommonStartup;` 라인 제거 — ResultSavePath 직접 열기.

#### overlay 재렌더 패턴 (`MainView.xaml.cs:243-252`) — **복사 핵심**
```csharp
// RenderStoredOverlaysForFai() 패턴 — 리뷰어 Window에 동일하게 사용
private void LoadCycleResult(ShotResultDto shot, FaiResultDto fai)
{
    // 1. 이미지 로드 (경로 기반) — HalconViewerControl.LoadImage(string) 오버로드
    halconViewer.LoadImage(shot.ResultImagePath);

    // 2. overlay 재렌더 — REPLACE 의미 (Clear + AddRange)
    if (fai.LastOverlays == null || fai.LastOverlays.Count == 0)
    {
        halconViewer.SetInspectionOverlays(
            new System.Collections.Generic.List<EdgeInspectionOverlay>());  // prior overlay 클리어
        return;
    }
    halconViewer.SetInspectionOverlays(fai.LastOverlays);  // RenderStoredOverlaysForFai 동일 호출

    // 3. 측정 테이블 갱신
    dataGrid_measurements.ItemsSource = BuildMeasurementRows(fai);
}
```
**순서 주의:** `LoadImage()` → `SetInspectionOverlays()` 순서 유지 (RESEARCH Pitfall 6).

#### XAML DataGrid 패턴 (`ProcessMonitorWindow.xaml:18-26`)
```xml
<!-- cycle 목록 ListBox -->
<ListBox x:Name="listBox_cycles" SelectionChanged="CycleList_SelectionChanged">
    <ListBox.ItemTemplate>
        <DataTemplate>
            <TextBlock Text="{Binding DisplayText}" />
        </DataTemplate>
    </ListBox.ItemTemplate>
</ListBox>

<!-- 측정 결과 DataGrid — ProcessMonitorWindow 의 DataGrid 구조 참고 -->
<DataGrid x:Name="dataGrid_measurements" AutoGenerateColumns="False"
          CanUserAddRows="False" CanUserDeleteRows="False">
    <DataGrid.Columns>
        <DataGridTextColumn Header="Shot"    Binding="{Binding ShotName}"          Width="80"/>
        <DataGridTextColumn Header="FAI"     Binding="{Binding FAIName}"           Width="80"/>
        <DataGridTextColumn Header="측정명"  Binding="{Binding MeasurementName}"   Width="120"/>
        <DataGridTextColumn Header="Nominal" Binding="{Binding NominalValue}"      Width="70"/>
        <DataGridTextColumn Header="Tol+"    Binding="{Binding TolerancePlus}"     Width="60"/>
        <DataGridTextColumn Header="Tol-"    Binding="{Binding ToleranceMinus}"    Width="60"/>
        <DataGridTextColumn Header="측정값"  Binding="{Binding ResultDisplay}"     Width="80"/>
        <DataGridTextColumn Header="판정"    Binding="{Binding JudgeText}"         Width="60"/>
    </DataGrid.Columns>
</DataGrid>
```
**`ResultDisplay` / `JudgeText` 로직:** `MeasurementResultRow.cs:52-62` 의 포맷팅 로직(단위 분기: "deg"/"mm", `HasResult` 분기) 리뷰어용 그리드 행 클래스에도 동일하게 적용.

---

### `WPF_Example/MainWindow.xaml.cs` (수정 — EPageType + PopupView 확장)

**아날로그:** 자기 자신 (`MainWindow.xaml.cs:20-30`, `260-338`)

#### EPageType 확장 패턴 (`MainWindow.xaml.cs:20-30`)
```csharp
public enum EPageType {
    Ready,
    Recipe,
    Log,
    Setting,
    Camera,
    Light,
    Connect,
    Login,
    ProcessMonitor,
    Reviewer,          // [신규 추가] Phase 40 D-08
}
```

#### PopupView 비모달 Show() 패턴 (`MainWindow.xaml.cs:327-337`)
```csharp
// ProcessMonitorWindow 의 비모달 Show() 패턴 — Reviewer 도 동일
// (LazyInit: 이미 열려 있으면 Show()만 호출)
private ReviewerWindow mReviewerWindow;

case EPageType.Reviewer:
    if (mReviewerWindow != null && mReviewerWindow.IsLoaded)
    {
        mReviewerWindow.Show();
        return;
    }
    mReviewerWindow = new ReviewerWindow();
    mReviewerWindow.Owner = this;
    mReviewerWindow.Show();   // ShowDialog() 아님 — 비모달, 라이브 검사 방해 안 함
    break;
```
**Show() vs ShowDialog() 선택 근거:** D-08 "라이브 검사 화면 방해 없이" → 비모달 Show(). ProcessMonitorWindow도 동일 패턴(L334-336).

---

### `WPF_Example/UI/MenuBar.xaml.cs` (수정 — 리뷰어 버튼 추가)

**아날로그:** 자기 자신 (`MenuBar.xaml.cs:71-101`)

#### 버튼 클릭 → PopupView 패턴 (`MenuBar.xaml.cs:71-102`)
```csharp
private void Button_Reviewer_Click(object sender, RoutedEventArgs e) {
    mParentWindow.PopupView(EPageType.Reviewer);  // 기존 버튼 클릭 패턴과 동일
}
```

#### IsEditable 게이팅 패턴 (`MenuBar.xaml.cs:33-47`)
```csharp
// 기존 IsEditable setter — 리뷰어 버튼은 IsEditable 무관(읽기 전용 뷰어이므로 항상 활성)
public bool IsEditable {
    set {
        ButtonSetting.IsEnabled = value;
        Button_Camera.IsEnabled = value;
        // ...
        // Button_Reviewer 는 추가하지 않음 — 리뷰어는 검사 중에도 접근 가능
    }
}
```

---

## 공유 패턴 (Cross-Cutting)

---

### 에러 처리 — 예외 격리
**출처:** `WPF_Example/Utility/RawImageSaveService.cs:86-93`, `WPF_Example/Sequence/Sequence/SequenceBase.cs:400-415`
**적용 대상:** `CycleResultSerializer.cs`, `ExcelExportService.cs`
```csharp
// 1. 서비스 레이어 에러 처리 — PrintErrLog 호출 후 조용히 반환
catch (Exception ex) {
    Logging.PrintErrLog((int)ELogType.Error,
        string.Format("[ServiceName] operation failed: {0}", ex.Message));
}

// 2. AddResponse() 내 직렬화 예외 격리 — 이중 try/catch
try {
    CycleResultSerializer.SaveAsync(dto);
}
catch (Exception ex) {
    try { Logging.PrintErrLog((int)ELogType.Error, "[CycleResult] " + ex.Message); } catch { }
    // TCP 응답은 계속 진행
}
```

---

### JSON 직렬화
**출처:** `WPF_Example/Custom/Sequence/Inspection/FAIConfig.cs:113` (`[JsonIgnore]` 사용 패턴)
**적용 대상:** `CycleResultDto.cs`, `CycleResultSerializer.cs`
```csharp
// FAIConfig 에서 [JsonIgnore] 를 제거해서 직렬화하는 것이 아니라,
// 별도 DTO(FaiResultDto)에 LastOverlays 를 복사하여 [JsonIgnore] 없이 직렬화.
// Newtonsoft.Json.JsonConvert.SerializeObject(dto, Formatting.Indented)
// 역직렬화: JsonConvert.DeserializeObject<CycleResultDto>(json)
```

---

### Logging 호출 규칙
**출처:** `WPF_Example/Utility/RawImageSaveService.cs:88`, `SequenceBase.cs:412`
**적용 대상:** 모든 신규 서비스 파일
```csharp
// ELogType 을 (int) 캐스트하여 호출 — CLAUDE.md 규칙
Logging.PrintErrLog((int)ELogType.Error, "메시지");
```

---

### HalconViewerControl 사용 (리뷰어 독립 인스턴스)
**출처:** `WPF_Example/UI/ContentItem/HalconViewerControl.xaml.cs:112-170`
**적용 대상:** `ReviewerWindow.xaml`
```csharp
// 공개 API — 리뷰어에서 순서대로 호출
halconViewer.LoadImage(string imagePath);           // 경로 기반 로드 (L112-130)
halconViewer.SetInspectionOverlays(IEnumerable<EdgeInspectionOverlay> overlays);  // REPLACE 의미 (L161-170)
// 순서 반드시: LoadImage → SetInspectionOverlays (Pitfall 6)
```

---

## 아날로그 없음 (No Analog Found)

| 파일 | 역할 | 데이터 흐름 | 이유 |
|------|------|------------|------|
| `WPF_Example/packages.config` + `App.config` 수정 | config | — | ClosedXML 전이 의존성 수동 추가 — 코드베이스 내 선례 없음. RESEARCH.md Standard Stack 섹션의 PM Console 지침 사용 |

---

## 메타데이터

**아날로그 검색 범위:** `WPF_Example/Utility/`, `WPF_Example/UI/`, `WPF_Example/Custom/Sequence/Inspection/`, `WPF_Example/Halcon/`, `WPF_Example/Sequence/`
**스캔 파일 수:** 12개 직접 읽음
**패턴 추출일:** 2026-06-01
