# Phase 67: 양산 이력 통계 분석 (STAT-01) - Pattern Map

**Mapped:** 2026-07-07
**Files analyzed:** 6 planned (2 new .cs collection/UI, 1 new .xaml, 3 modified)
**Analogs found:** 5 strong / 6 (ChartDirector = no in-repo analog)

> **CODE-RULES (하위 에이전트 프롬프트에 매번 명시 — 전 plan 강제):**
> - 헝가리언 표기법: 지역/필드 `b/n/f/d/sz/p` 접두, 멤버 `m_`(신규 파일) / 기존 파일은 그 파일 관례(예: MainWindow `mXxx`), 전역 `g_`.
> - **삼항 `?:` 금지 → if/else**. **null 병합 `??` 금지 → if/else** (아래 발췌들엔 `??`가 남아있음 — 신규 코드에선 if/else 로 풀 것).
> - 함수 30줄 이내 / 단일책임. 매직넘버 상수화(UPPER_SNAKE_CASE).
> - C# 7.2 — switch expression / nullable ref / record / init 금지.
> - 수정·추가 라인 끝에 `//260707 hbk` 주석.
> - 브레이스 스타일 = **수정 대상 파일의 관례를 따름** (MainWindow/MenuBar/Logging = K&R 같은 줄, 신규 파일 = Allman 권장; 프로젝트 신규 Halcon/Service 코드는 Allman).

---

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|-------------------|------|-----------|----------------|---------------|
| **NEW** `WPF_Example/Custom/Sequence/Inspection/MeasurementHistoryCsvWriter.cs` (이름 재량) | service/utility | file-I/O (append) | `CycleResultSerializer.SaveAsync` + `Logging.GetTodaySavePath` | role-match (append vs JSON-write) |
| **MOD** `WPF_Example/Setting/SystemSetting.cs` (+`StatisticsSavePath`) | config | — | `ResultSavePath` (line 66) | exact |
| **REUSE (no new file)** `RepeatMeasurementStats` | service | transform/aggregate | `RepeatMeasurementStats.AddSample`/`ComputeAll` | exact (재사용) |
| **NEW** `WPF_Example/UI/Statistics/StatisticsWindow.xaml(.cs)` (위치 재량) | component (view) | request-response (load→bind) | `ReviewerWindow.xaml(.cs)` | exact (미러링) |
| **MOD** `WPF_Example/MainWindow.xaml.cs` (+`EPageType.Statistics`, `mStatisticsWindow`) | route/entry | event-driven | `EPageType.Reviewer` case (line 355-363) | exact |
| **MOD** `WPF_Example/UI/MenuBar.xaml(.cs)` (+"통계분석" 버튼) | component | event-driven | `Button_Reviewer` (MenuBar.xaml 206-212) | exact |
| **NEW** ChartDirector 히스토그램/추이 차트 (StatisticsWindow 내부) | component | transform→render | **없음** — 아래 "No Analog" 참조 | none |

---

## Pattern Assignments

### 1. NEW CSV History Writer (수집 계층) — `MeasurementHistoryCsvWriter` (service, file-I/O)

**Primary analog:** `WPF_Example/Custom/Sequence/Inspection/CycleResultSerializer.cs` `SaveAsync` (line 162-200)
**Secondary analog (daily rotation):** `WPF_Example/Utility/Logging.cs` `GetTodaySavePath` (line 90-92)

**주입 지점 = `SaveAsync` 의 background Task 내부.** D-04 에 따라 v2.6/v1.0/수동 3경로 모두 이 한 곳으로 수렴. cycle.json 쓰기 **직후, 동일 try 안** 또는 별도 try 로 CSV append 호출을 추가한다. `dto` 가 이미 전 Shot/FAI/Measurement 를 담고 있으므로 CSV 행 생성 소스로 그대로 사용.

**SaveAsync 현재 본문 (주입 대상 — line 162-200):**
```csharp
public static void SaveAsync(CycleResultDto dto)
{
    if (dto == null) { return; }

    string dateDir = Path.Combine(
        SystemHandler.Handle.Setting.ResultSavePath,
        dto.InspectionTime.ToString("yyyyMMdd"));
    string cycleDir = Path.Combine(
        dateDir,
        dto.InspectionTime.ToString("HHmmss") + "_cycle");
    dto.CycleFolderPath = cycleDir;

    Task.Factory.StartNew(() =>
    {
        try
        {
            Directory.CreateDirectory(cycleDir);
            string jsonPath = Path.Combine(cycleDir, "cycle.json");
            string json = JsonConvert.SerializeObject(dto, Formatting.Indented);
            File.WriteAllText(jsonPath, json, System.Text.Encoding.UTF8);
            // 260707 hbk STAT-01 D-04: CSV 이력 append 훅 삽입 지점 (별도 try 권장 — JSON 성공/실패와 독립)
            // MeasurementHistoryCsvWriter.Append(dto);
        }
        catch (Exception ex)
        {
            try { Logging.PrintErrLog((int)ELogType.Error, "[CycleResultSerializer] Save failed: " + ex.Message); }
            catch { }
        }
    });
}
```
> **주의:** CSV append 는 검사/TCP 응답 무영향(D-04)이 요구되므로 자체 `try/catch` 로 감싸 JSON 예외와 분리 권장 — 위 예시처럼 `// Append` 호출을 별도 try 블록으로.

**일자별 경로 + append 패턴 (Logging.GetTodaySavePath, line 82-92):**
```csharp
public void CreateFileWriter() {
    if (!Directory.Exists(LogPath)) { Directory.CreateDirectory(LogPath); }
    string path = GetTodaySavePath();
    LogWriter = new StreamWriter(path, true);   // append=true
}
public string GetTodaySavePath() {
    return string.Format("{0}\\{1:yyyy-MM-dd}_{2}{3}", LogPath, DateTime.Now, LogName, FileExt);
}
```
> 신규 writer 는 D-02 스키마 = `StatisticsSavePath\yyyyMMdd.csv` (Logging 은 `yyyy-MM-dd_name.ext` — 포맷만 다름, `Directory.CreateDirectory` + append 원리는 동일).

**동시 쓰기 보호 (D-05):** 신규 writer 에 `private static readonly object s_lock = new object();` → `lock (s_lock) { File.AppendAllText(...); }`. 기존 프로젝트 lock 관례 = `Logging.lockObject`, `ShotConfig._imageLock`.

**CSV 라인 헬퍼 주의:** `Logging.PrintLogToCSV` (line 318-332) 는 `MessageSeperator` 로 단순 join 만 한다 — **RFC4180 따옴표 이스케이프 없음**. D-03 은 측정명/타입명 콤마 대응 이스케이프 필수 → 재사용 불가. 신규 writer 에 이스케이프 헬퍼(값에 `,`/`"`/개행 있으면 `"`로 감싸고 내부 `"`→`""`) 직접 구현.

**CSV 행 소스 = dto 평탄화 (Shot→FAI→Measurement 3중 루프):** `RepeatMeasurementStats.AddSample`(아래) 및 `ReviewerWindow.DisplayCycle`(line 124-142)의 루프 구조를 그대로 복사. D-03 컬럼 = `검사일시 / RecipeName / IndexNumber / ShotName / FAIName / MeasurementName / TypeName / NominalValue / TolerancePlus / ToleranceMinus / MeasuredValue(=LastMeasuredValue) / Judgement / HasResult(=LastHasResult) / OverallCycleResult(=dto.OverallJudgement)`.
- Judgement 매핑 정책 = `MeasurementResultDto.LastSkipReason`("DATUM_FAIL"/"NO_IMAGE") → 검출실패, else `LastJudgement` true=OK/false=NG (RepeatMeasurementStats.AddSample line 108-123 정책과 동일하게).

---

### 2. MOD `SystemSetting.StatisticsSavePath` (config)

**Analog:** `WPF_Example/Setting/SystemSetting.cs` `ResultSavePath` (line 64-66)

```csharp
[DirectoryPath]
[AutoUpdateText]
public string ResultSavePath { get; set; } = AppDomain.CurrentDomain.BaseDirectory + @"Result";
```

**추가할 것 (D-01, ResultSavePath 인접에):**
```csharp
[DirectoryPath]
[AutoUpdateText]
public string StatisticsSavePath { get; set; } = AppDomain.CurrentDomain.BaseDirectory + @"Statistics"; //260707 hbk STAT-01 D-01
```
> `[Category]` 는 인접 멤버가 이미 `[Category("Path|Log")]` 그룹에 속하면 생략 가능(PropertyGrid 는 직전 Category 유지). 필요 시 `[Category("Path|Statistics")]` 명시. **주의(메모리 IN-01/어트리뷰트):** `[Category]`/`[DirectoryPath]`/`[AutoUpdateText]` 어트리뷰트는 바로 다음 멤버 1개에만 적용 — 새 프로퍼티 바로 위에 3종 모두 재기입.
> **누락 키 0-로드 함정(메모리 reference_parambase_missing_key):** 구 INI/JSON 에 `StatisticsSavePath` 키가 없으면 로드시 null/빈값 덮어쓰기 가능. SystemSetting 로드 경로 확인 — 기본값 복원 필요하면 `Custom/SystemSetting.cs` partial 에서 빈값→기본값 보정.

---

### 3. Statistics Computation Reuse — `RepeatMeasurementStats` (service, transform)

**Analog (그대로 재사용):** `WPF_Example/Custom/Sequence/Inspection/RepeatMeasurementStats.cs`

**AddSample 시그니처 + 키 그룹핑 (line 60-127):**
```csharp
public void AddSample(CycleResultDto dto)   // Shot→FAI→Measurement 3중 루프
// key = (shot.ShotName ?? "") + "/" + (fai.FAIName ?? "") + "/" + (m.MeasurementName ?? "")   (line 88)
// 정책 (line 108-123):
//   LastSkipReason == "DATUM_FAIL" || "NO_IMAGE" → DetectFailCount++ (값 제외)
//   else if LastHasResult → Values.Add(LastMeasuredValue); LastJudgement ? OkCount++ : NgCount++
//   LastNominal/LastTolPlus/LastTolMinus = 매 샘플 최신값 갱신
```

**ComputeAll 반환형 (line 133-211):** `Dictionary<string, MeasurementStat>` — key = 위 "Shot/FAI/측정명".
`MeasurementStat` 필드 (line 13-30): `ShotName / FAIName / MeasurementName / TypeName / N / Mean / StdDev / Range / Cpk / NominalValue / TolerancePlus / ToleranceMinus / OkCount / NgCount / DetectFailCount`.
Cpk 로직 (line 175-187): `usl=Nominal+TolPlus`, `lsl=Nominal-|TolMinus|`, `stddev==0 → Cpk=PositiveInfinity`, else `min((usl-mean)/(3σ),(mean-lsl)/(3σ))`. N≥1 mean, N≥2 stddev — **D-06/D-07 정책과 100% 일치 → 수정 없이 재사용**.

**재사용 연결 방식 (Claude 재량, D-15):** CSV 플랫 행 → 최소 `CycleResultDto` 재구성 후 `AddSample`. 각 CSV 행 = 1개 `MeasurementResultDto`. 같은 검사일시(cycle) 행들을 InspectionTime 으로 묶어 CycleResultDto 1개(단, 통계는 측정키 누적이므로 cycle 경계 없이 행마다 `MeasurementResultDto` 를 담은 얇은 Shot/FAI/Measurement 트리로 감싸 AddSample 반복 호출해도 동일 결과). **추이 차트(D-13)용 순서별 원시 측정값 리스트는 RepeatMeasurementStats 가 제공 안 함** — writer/loader 가 측정키별 `List<double>`(순서 유지) 별도 수집 필요.

**호출 패턴 참고:** `WPF_Example/Custom/Export/RepeatExcelExportService.cs` — `AddSample` 루프 → `ComputeAll` → 렌더 흐름.

**CycleResultDto 구조 (WPF_Example/UI/ViewModel/CycleResultDto.cs):**
```
CycleResultDto: InspectionTime(DateTime) / RecipeName / IndexNumber(int, -1=미수신) / OverallJudgement("OK"|"NG"|"DETECT_FAIL") / Shots
  ShotResultDto: ShotName / OwnerSequenceName / ResultImagePath / FAIs
    FaiResultDto: FAIName / IsPass / WasDatumSkipped / Measurements
      MeasurementResultDto: MeasurementName / TypeName / NominalValue / TolerancePlus / ToleranceMinus /
                            LastMeasuredValue / LastJudgement(bool) / LastHasResult(bool) / LastSkipReason(null|"DATUM_FAIL")
```

---

### 4. NEW StatisticsWindow (component, request-response) — `ReviewerWindow` 미러링

**Analog:** `WPF_Example/UI/Reviewer/ReviewerWindow.xaml` + `.xaml.cs`

**code-behind 스켈레톤 (ReviewerWindow.xaml.cs line 21-38):**
```csharp
public partial class ReviewerWindow : Window
{
    private CycleResultDto _currentCycle;
    private List<ReviewMeasurementRow> _allRows = new List<ReviewMeasurementRow>();
    public ReviewerWindow() { InitializeComponent(); }
    // Button click 핸들러 + SelectionChanged 핸들러들 ...
}
```
> ViewModel 클래스 아님 — 직접 code-behind. `ReviewMeasurementRow`/`CycleListItem`(line 461-471) 처럼 화면용 행 클래스를 같은 파일 또는 인접에 선언. INotifyPropertyChanged 미사용(정적 바인딩 + `ItemsSource` 직접 세팅). StatisticsWindow 도 동일 스타일: `MeasurementStat` 을 DataGrid `ItemsSource` 로 직접 바인딩.

**XAML 루트 레이아웃 (ReviewerWindow.xaml line 1-23) — DataGrid + GridSplitter:**
```xml
<Window x:Class="ReringProject.UI.ReviewerWindow" ...
        Title="결과 리뷰어" Height="800" Width="1200" WindowStartupLocation="CenterOwner">
  <Grid>
    <Grid.RowDefinitions><RowDefinition Height="Auto"/><RowDefinition Height="*"/></Grid.RowDefinitions>
    <Grid.ColumnDefinitions>
      <ColumnDefinition Width="220" MinWidth="120"/>
      <ColumnDefinition Width="5"/>            <!-- GridSplitter -->
      <ColumnDefinition Width="*" MinWidth="200"/>
      ...
    </Grid.ColumnDefinitions>
```

**DataGrid 정의 + 판정색 RowStyle (line 107-150):**
```xml
<DataGrid x:Name="dataGrid_measurements" SelectionChanged="MeasurementGrid_SelectionChanged"
          SelectionMode="Single" AutoGenerateColumns="False" CanUserAddRows="False"
          IsReadOnly="True" ...>
  <DataGrid.RowStyle>
    <Style TargetType="DataGridRow"><Style.Triggers>
      <DataTrigger Binding="{Binding JudgeText}" Value="NG"><Setter Property="Background" Value="#FFD6D6"/></DataTrigger>
      ...
    </Style.Triggers></Style>
  </DataGrid.RowStyle>
  <DataGrid.Columns>
    <DataGridTextColumn Header="Shot" Binding="{Binding ShotName}" Width="70"/>
    <DataGridTextColumn Header="측정명" Binding="{Binding MeasurementName}" Width="100"/>
    <DataGridTextColumn Header="Nominal" Binding="{Binding NominalValue, StringFormat=F4}" Width="65"/>
    ...
  </DataGrid.Columns>
</DataGrid>
```
> StatisticsWindow 컬럼 = D-03/통계 = Shot/FAI/측정명/N/Mean/StdDev/Range/Cpk/OK/NG/DetectFail/불량률. `StringFormat=F4` 관례 유지. Cpk=PositiveInfinity 표시 처리 필요(예: "∞" 또는 "-").

**데이터 로드 패턴 (LoadCycleFolders line 53-90) — try/catch + Directory.Exists 가드 + Logging.PrintErrLog:**
```csharp
try {
    if (string.IsNullOrEmpty(path) || !Directory.Exists(path)) { listBox_cycles.ItemsSource = null; return; }
    var items = Directory.GetDirectories(path).Where(...).OrderByDescending(d => d).Select(...).ToList();
    listBox_cycles.ItemsSource = items;
} catch (Exception ex) {
    try { Logging.PrintErrLog((int)ELogType.Error, "[Reviewer] LoadCycleFolders: " + ex.Message); } catch { }
}
```
> StatisticsWindow 로드 = D-10 기본 오늘 하루 → `StatisticsSavePath\{today}.csv` 1개 읽기. D-11 필터 = DatePicker from~to + 레시피 드롭다운(로드된 CSV distinct RecipeName) + [조회]. 파일 미존재/파싱 실패 → 위 try/catch + 빈 목록 폴백 패턴 그대로.

**행 클릭 → 상세 갱신 (MeasurementGrid_SelectionChanged line 212-238):** DataGrid `SelectionChanged` → 선택 행 기준 상세 렌더. D-12 = 테이블 행 클릭 → 해당 Shot/FAI/측정명 히스토그램+추이 갱신(별도 콤보박스 없음). 이 핸들러 구조를 미러링하되 이미지/overlay 대신 차트 갱신.

---

### 5. MOD Menu Entry Point — `EPageType.Statistics` (route)

**Analog:** `WPF_Example/MainWindow.xaml.cs`

**enum (line 20-31):**
```csharp
public enum EPageType {
    Ready, Recipe, Log, Setting, Camera, Light, Connect, Login, ProcessMonitor,
    Reviewer,   // Phase 40 OUT-01 D-08 — 결과 리뷰어 비모달 창
    // Statistics,   //260707 hbk STAT-01 D-09 — 추가 지점
}
```

**멤버 필드 (line 69-70):**
```csharp
private ProcessMonitorWindow mProcMonitorWindow;
private UI.ReviewerWindow mReviewerWindow;
// private UI.StatisticsWindow mStatisticsWindow;   //260707 hbk STAT-01 — 추가
```

**PopupView switch case (line 355-363) — 비모달 Show 재사용 패턴:**
```csharp
case EPageType.Reviewer:   // Phase 40 OUT-01 D-08 — 비모달 Show() (ShowDialog 아님)
    if (mReviewerWindow != null && mReviewerWindow.IsLoaded) {
        mReviewerWindow.Show();
        return;
    }
    mReviewerWindow = new UI.ReviewerWindow();
    mReviewerWindow.Owner = this;
    mReviewerWindow.Show();   // 비모달
    break;
```
> `EPageType.Statistics` case 를 **완전 동일 구조**로 추가 (mStatisticsWindow). `PopupView` 는 하나의 switch (line 276-364) — Statistics case 를 Reviewer 옆에.

**MenuBar 트리거 — 버튼 XAML (MenuBar.xaml line 204-212):**
```xml
<!-- 결과 리뷰어 -->
<Button x:Name="Button_Reviewer" Grid.Column="3" Grid.Row="1" ...
        Click="Button_Reviewer_Click" Style="{StaticResource disalbedStyle}">
    <StackPanel Orientation="Vertical">
        <TextBlock Text="[R]" FontSize="22" .../>
        <TextBlock Text="결과 리뷰어" FontSize="12"/>
    </StackPanel>
</Button>
```

**MenuBar 핸들러 (MenuBar.xaml.cs line 104-107):**
```csharp
private void Button_Reviewer_Click(object sender, RoutedEventArgs e) {
    mParentWindow.PopupView(EPageType.Reviewer);
}
```
> "통계분석" 버튼 추가: `Button_Statistics` + `Button_Statistics_Click` → `PopupView(EPageType.Statistics)`. XAML Grid 배치(Grid.Column/Row) 는 MenuBar 레이아웃 상 빈 셀 확인 필요 — Reviewer 는 Grid.Column="3" Grid.Row="1". Style `disalbedStyle`(오타 그대로 — StaticResource 키명), IsEditable 무관 항상 활성.

---

## Shared Patterns

### 비동기 fire-and-forget + try/catch 격리
**Source:** `CycleResultSerializer.SaveAsync` (line 180-199)
**Apply to:** CSV writer (검사 스레드 무영향 필수 — D-04)
```csharp
Task.Factory.StartNew(() => {
    try { /* file I/O */ }
    catch (Exception ex) {
        try { Logging.PrintErrLog((int)ELogType.Error, "[...] " + ex.Message); } catch { }
    }
});
```
> CSV writer 는 이미 SaveAsync 의 Task 안에서 호출되므로 새 Task 불필요 — 동일 Task 내 별도 try/catch 로 격리만.

### 파일 I/O 가드
**Source:** `ReviewerWindow.LoadCycleFolders` (line 57-62), `SaveAsync` (line 184)
**Apply to:** CSV writer, StatisticsWindow loader
- 쓰기 전 `Directory.CreateDirectory(dir)` (존재해도 무해).
- 읽기 전 `string.IsNullOrEmpty(path) || !File.Exists/Directory.Exists` 가드 → 빈 결과 폴백.

### 에러 로깅
**Source:** 전 analog 공통
**Apply to:** 모든 신규 파일
```csharp
try { Logging.PrintErrLog((int)ELogType.Error, "[클래스명] 메시지: " + ex.Message); } catch { }
```
`ELogType` 는 항상 `(int)` 캐스트. 비치명 파일작업 실패는 `bare catch { }` 허용(프로젝트 관례).

### 동시성 lock
**Source:** `Logging.lockObject`, `ShotConfig._imageLock` (CLAUDE.md Module Design)
**Apply to:** CSV writer (D-05)
```csharp
private static readonly object s_lock = new object();   // 프로세스 내 복수 InspectionSequence append 경합 방지
lock (s_lock) { File.AppendAllText(path, line, Encoding.UTF8); }
```

---

## No Analog Found

| File/기능 | Role | Data Flow | Reason |
|------|------|-----------|--------|
| ChartDirector 히스토그램/추이 차트 | component | transform→render | **소스 전체에 ChartDirector 사용처 0건.** grep 결과 `.cs` 파일 매치 없음 — packages/config/csproj 참조만 존재. |

**ChartDirector 상세:**
- DLL 은 csproj 에 참조됨 (DatumMeasurement.csproj line 91-105): `netchartdir.dll`(assembly `netchartdir`, namespace 통상 `ChartDirector`) + `ChartDirector.Net.Desktop.Controls.dll`(WPF 뷰어 컨트롤 `WPFChartViewer`). net40 타깃.
- **기존 사용 코드가 전무** → planner 는 ChartDirector API(XYChart/BarLayer 히스토그램, LineLayer 추이, USL/LSL/Mean 마크라인 `addMark`, `WPFChartViewer` XAML 배치)를 **from scratch 리서치**해야 함. RESEARCH.md 또는 패키지 XML 문서(`packages/ChartDirector.Net.7.1.0/lib/net40/netchartdir.xml`) 참조 권장.
- D-13 추이 = x축 샘플 인덱스(1,2,3...) LineLayer + USL/LSL/Mean 수평 markLine. D-14 히스토그램 = BarLayer(bin count = sqrt(N) 또는 고정 20, 재량) + USL/LSL 수직 markLine.
- 대안 폴백(리서치 실패 시): WPF 순수 Canvas/Rectangle 로 히스토그램 직접 그리기 가능하나 CONTEXT 는 ChartDirector 기존 의존성 활용 명시 — 우선 ChartDirector 시도.

---

## Metadata

**Analog search scope:** `WPF_Example/Custom/Sequence/Inspection/`, `WPF_Example/UI/Reviewer/`, `WPF_Example/UI/ViewModel/`, `WPF_Example/Setting/`, `WPF_Example/Utility/`, `WPF_Example/MainWindow.xaml.cs`, `WPF_Example/UI/MenuBar.xaml(.cs)`, `WPF_Example/Custom/Export/`, csproj/packages (ChartDirector)
**Files scanned:** 9 read + 5 grep sweeps
**Pattern extraction date:** 2026-07-07
