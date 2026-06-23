# Phase 53: 픽셀 캘리브레이션 (체커보드) - Pattern Map

**Mapped:** 2026-06-23
**Files analyzed:** 3 new + 2 modified
**Analogs found:** 5 / 5

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|-------------------|------|-----------|----------------|---------------|
| `WPF_Example/Halcon/Algorithms/CheckerboardCalibrationService.cs` (new) | service (Halcon algorithm) | transform / request-response | `WPF_Example/Halcon/Algorithms/VisionAlgorithmService.cs` | exact |
| `WPF_Example/UI/Dialog/CalibrationWindow.xaml` (new) | UI window (view) | request-response | `WPF_Example/UI/Dialog/TeachingWindow.xaml` | exact |
| `WPF_Example/UI/Dialog/CalibrationWindow.xaml.cs` (new) | UI window (code-behind) | event-driven / request-response | `WPF_Example/UI/Dialog/TeachingWindow.xaml.cs` | exact |
| `WPF_Example/UI/ContentItem/MainView.xaml.cs` (modified — apply wiring) | UI controller | CRUD (write PixelResolution) | `MainView.ApplyCalibrationResult` (same file) | exact (extend in place) |
| `WPF_Example/DatumMeasurement.csproj` (modified — register new files) | config | build | existing `<Compile>`/`<Page>` entries | n/a |

## Pattern Assignments

### `CheckerboardCalibrationService.cs` (Halcon algorithm service, transform)

**Analog:** `WPF_Example/Halcon/Algorithms/VisionAlgorithmService.cs`

**Class header + namespace + convention doc** (lines 1-13):
```csharp
using System;
using System.Collections.Generic;
using HalconDotNet;

namespace ReringProject.Halcon.Algorithms
{
    /// <summary>
    /// ... 모든 Halcon 호출은 try { ... } catch { return false; } 패턴 (프로젝트 컨벤션).
    /// 순수 수학 연산은 static 메서드로 제공한다.
    /// </summary>
    public class VisionAlgorithmService
```
> Copy: Allman braces, `using HalconDotNet;`, namespace `ReringProject.Halcon.Algorithms`, XML-doc노트, "순수 수학 = static" 정신. New service follows the same shape.

**HALCON wrapper pattern — Try 접두 + out 결과 + bool + try/catch** (RESEARCH Pattern 1 골격, modeled on `TryFitLine` lines 18-35):
```csharp
public bool TryDetectCheckerboardCorners(HImage image, out HTuple rows, out HTuple cols, out string error)
{
    rows = cols = null; error = null;
    if (image == null) { error = "image is null"; return false; }
    try
    {
        HOperatorSet.SaddlePointsSubPix(image, "facet", 1.0, 5.0, out rows, out cols);
    }
    catch { error = "saddle point 검출 실패"; return false; }
    if (rows == null || rows.Length < MinCornerCount) { error = "코너 부족"; return false; }
    return true;
}
```
> `out` 초기화 → null/인자 가드 → 단일 try 안에 HOperatorSet 호출 → bare `catch { error=...; return false; }` → 후처리 가드. CLAUDE.md 에러처리 컨벤션과 일치.

**Robust 절사 통계 — REUSE, do not hand-roll** (`SortAndTrimPercent`, lines 188-204):
```csharp
public static void SortAndTrimPercent(ref HTuple rows, ref HTuple cols, bool scanHorizontal, int trimPercent)
{
    if (rows == null || cols == null) return;
    int n = rows.TupleLength();
    if (trimPercent <= 0 || n < 4) return;
    HTuple key = scanHorizontal ? rows : cols;
    HTuple order = key.TupleSortIndex();
    rows = rows.TupleSelect(order);
    cols = cols.TupleSelect(order);
    int pct = trimPercent; if (pct > 49) pct = 49;
    int removeEach = (int)(n * pct / 100.0);
    if (removeEach > 0 && (n - 2 * removeEach) >= 2)
    {
        rows = rows.TupleSelectRange(removeEach, n - removeEach - 1);
        cols = cols.TupleSelectRange(removeEach, n - removeEach - 1);
    }
}
```
> RESEARCH "Don't Hand-Roll" 및 Pattern 2 가 이 메서드 재사용을 명시. 격자 간격 list 의 이상치(2×피치 점프) 절사에 활용하거나, 새 `static double Median(List<double>)` 헬퍼를 동일 `public static` 스타일로 추가. **새 정렬/절사 구현 금지.**

> **CLAUDE.md null-safe 계약:** load helper 는 실패 시 `false`+`out error` 반환, caller null-check. 절대 `Run()`/검출에서 throw 금지.

---

### `CalibrationWindow.xaml` (UI window view, request-response)

**Analog:** `WPF_Example/UI/Dialog/TeachingWindow.xaml`

**Window 선언 + 크기 + 시작위치** (lines 1-11):
```xml
<Window x:Class="ReringProject.UI.TeachingWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:viewers="clr-namespace:ReringProject.UI"
        Title="HALCON Teaching"
        Height="860" Width="1400"
        WindowStartupLocation="CenterOwner">
```
> Copy: `x:Class="ReringProject.UI.CalibrationWindow"`, `xmlns:viewers` 별칭, `WindowStartupLocation="CenterOwner"`. UI 네임스페이스는 `ReringProject.UI` (Dialog 하위라도 동일).

**좌측 패널 입력/버튼 + 우측 HALCON 뷰어 + 하단 StatusBar 레이아웃** (2-column Grid, lines 58-169):
```xml
<Grid Background="#E2E8F0">
    <Grid.ColumnDefinitions>
        <ColumnDefinition Width="430"/>   <!-- 입력/버튼/리포트 -->
        <ColumnDefinition Width="*"/>     <!-- 이미지 -->
    </Grid.ColumnDefinitions>
    ...
    <viewers:HalconViewerControl x:Name="TeachingViewer"/>
    <StatusBar Grid.Row="1" ...>
        <StatusBarItem><TextBlock x:Name="TeachingStatusTextBlock" Text="Ready"/></StatusBarItem>
    </StatusBar>
</Grid>
```
> Copy: 좌(고정폭 입력 패널)/우(`HalconViewerControl` + StatusBar) 2-컬럼. 캘리브 창은 좌측에 — 칸 크기(mm) 입력 TextBox(D-01 직전값 기억), [이미지 로드]/[라이브 촬상]/[검출]/[적용] 버튼(WrapPanel, `TeachingActionButtonStyle` 스타일 복사 가능), 리포트 TextBlock + 왜곡 경고 라벨(D-05) 배치. 우측 뷰어 이름은 `CalibrationViewer` 권장.

**버튼 스타일 리소스 + Click 핸들러 바인딩** (lines 12-56, 91-102):
```xml
<Button Style="{StaticResource TeachingActionButtonStyle}" Content="Load Image" Click="LoadImageButton_Click"/>
<Button Style="{StaticResource TeachingActionButtonStyle}" Content="Grab Image" Click="GrabImageButton_Click"/>
```
> `Window.Resources` 내 `TeachingActionButtonStyle` 통째 복사 후 `CalibrationActionButtonStyle` 로 사용. 색/hover/disabled 트리거 포함.

---

### `CalibrationWindow.xaml.cs` (UI window code-behind, event-driven)

**Analog:** `WPF_Example/UI/Dialog/TeachingWindow.xaml.cs`

**using + partial class + 필드 + 생성자 viewer 와이어업** (lines 1-43):
```csharp
using System;
using System.Globalization;
using System.Windows;
using Microsoft.Win32;
using ReringProject.Halcon.Algorithms;
namespace ReringProject.UI
{
    public partial class TeachingWindow : Window
    {
        private readonly MeasurementAlgorithm _algorithm = new MeasurementAlgorithm();
        public TeachingWindow()
        {
            InitializeComponent();
            ...
        }
        public Func<string> ImageGrabber { get; set; }
```
> Copy: `partial class CalibrationWindow : Window`, `InitializeComponent()`, `readonly CheckerboardCalibrationService _calibService = new ...()`. `Microsoft.Win32` (OpenFileDialog), `System.Globalization`(double 파싱) using.

**이미지 로드 핸들러 + OpenFileDialog** (lines 135-144):
```csharp
private void LoadImageButton_Click(object sender, RoutedEventArgs e)
{
    var dialog = new OpenFileDialog { Filter = "Image Files (*.bmp;*.jpg;*.jpeg;*.png;*.tif;*.tiff)|...|All Files (*.*)|*.*" };
    if (dialog.ShowDialog(this) != true) { return; }
    LoadImage(dialog.FileName);
    TeachingStatusTextBlock.Text = string.Format("Loaded image: {0}", dialog.FileName);
}
```
> 동일 패턴으로 [이미지 로드]. 표시는 `CalibrationViewer.LoadImage(path)` (HalconViewerControl.LoadImage(string), 아래 참조).

**라이브 촬상 — ImageGrabber Func 주입 패턴 + null 가드** (lines 36, 145-158):
```csharp
public Func<string> ImageGrabber { get; set; }
private void GrabImageButton_Click(object sender, RoutedEventArgs e)
{
    if (ImageGrabber == null) { TeachingStatusTextBlock.Text = "Grab is not available..."; return; }
    var imagePath = ImageGrabber();
    if (string.IsNullOrWhiteSpace(imagePath)) { TeachingStatusTextBlock.Text = "Grab failed."; return; }
    LoadImage(imagePath);
}
```
> Caller(MainView)가 `ImageGrabber`/카메라 grab 델리게이트 주입. D-04 SIMUL 분기는 RESEARCH Pattern 4 — 생성자/Loaded 에서:
```csharp
#if SIMUL_MODE
    btn_liveCapture.IsEnabled = false;
    btn_liveCapture.ToolTip = "SIMUL 모드에서는 이미지 로드만 가능합니다.";
#endif
```

**입력 검증 (V5)** — `double.TryParse(text, out mm) && mm > 0` 패턴 (MainView FinishCalibration line 2270 참조). knownMm 파싱 + 코너 수 가드 + null HImage 가드.

---

### `MainView.xaml.cs` — Apply 반영 wiring (UI controller, CRUD write)

**Analog:** 같은 파일 내 `ApplyCalibrationResult` / `FinishCalibration` (lines 2247-2318) — **확장**.

**기존 단일-FAI 반영 로직** (lines 2297-2317, D-03 으로 "활성 시퀀스 전체 shot" 확장 대상):
```csharp
private void ApplyCalibrationResult(double mmPerPixel) {
    var selectedRow = dataGrid_faiResults.SelectedItem as MeasurementResultRow;
    FAIConfig anchorFai = (selectedRow != null) ? FindFAIByName(selectedRow.FAIName) : null;
    if (anchorFai != null) {
        var shot = anchorFai.Owner as ShotConfig;
        if (shot == null) { CustomMessageBox.Show("샷 정보를 찾을 수 없습니다.", "캘리브레이션"); return; }
        shot.PixelResolution = mmPerPixel;                 // Phase 42 단일소스
        foreach (FAIConfig fai in shot.FAIList) {
            fai.PixelResolutionX = mmPerPixel;             // INI 호환 보존
            fai.PixelResolutionY = mmPerPixel;
        }
    }
}
```
> **확장 (D-03, RESEARCH Pattern 5):** "선택 FAI 1개" → `recipeManager.Shots` 중 `OwnerSequenceName == activeSeq` 전체 loop. Shots 단일소스 접근은 같은 파일 line 2011-2013 패턴:
```csharp
InspectionRecipeManager recipeManager = SystemHandler.Handle.Sequences.RecipeManager;
foreach (ShotConfig shot in recipeManager.Shots)
{
    string owner = string.IsNullOrEmpty(shot.OwnerSequenceName) ? "TOP" : shot.OwnerSequenceName;
    if (owner != activeSeq) continue;
    shot.PixelResolution = mmPerPixel;
    foreach (FAIConfig fai in shot.FAIList) { fai.PixelResolutionX = mmPerPixel; fai.PixelResolutionY = mmPerPixel; }
}
```
> `shot.PixelResolution = mmPerPixel` + FAI `PixelResolutionX/Y` 동반 갱신은 기존 로직 **그대로 유지**(INI 호환). 빈 OwnerSequenceName → "TOP" 폴백은 기존 정책.

**저장 — REUSE `MainWindow.SaveRecipe()`** (`MainWindow.xaml.cs` lines 263-273):
```csharp
public void SaveRecipe(string name=null) {
    if(mSystemHandler.Sequences.StateAll == EContextState.Running) { ...return; }
    if(!mSystemHandler.Sequences.SaveRecipe(name, ERecipeFileType.Ini)) { ...return; }
    CustomMessageBox.Show(... "Save Recipe Success" ...);
}
```
> [적용] 핸들러 끝에서 호출(Running 가드 + existingFile 보존 경로 내장). **직접 INI 쓰기 금지.** ⚠️ 메모리 `project_recipe_datum_loss_camerarole`(3faa91b): 비활성 시퀀스 소실 가드 경로(`SaveNewFormat(saveFile, existingFile)`)를 타는지 plan 단계서 확인 (Assumption A5).

---

## Shared Patterns

### HALCON 이미지 로드/표시
**Source:** `WPF_Example/UI/ContentItem/HalconViewerControl.xaml.cs` — `LoadImage(string imagePath)` (line 119), `LoadImage(HImage image, string sourceContext=null)` (line 147)
**Apply to:** CalibrationWindow (이미지 로드 + 라이브 촬상 표시 둘 다)
> 파일 경로는 `LoadImage(string)`, grab 한 `HImage` 직접 표시는 `LoadImage(HImage, sourceContext)`. `sourceContext==null` 시 CurrentImagePath="" 로 캐시 hit 차단 — grab 프레임 반복 표시에 적합.

### 라이브 grab (D-04 라이브 경로)
**Source:** `WPF_Example/Device/DeviceHandler.cs` — `GrabHalconImage(ICameraParam param)` (line 326)
**Apply to:** CalibrationWindow 라이브 촬상 (실 HW). SIMUL 은 비활성(Pattern 4).
```csharp
public HImage GrabHalconImage(ICameraParam param) {
    VirtualCamera cam = this[param.DeviceName];
    if (cam == null) return null;
    ...
}
```
> `SystemHandler.Handle.Sequences.RecipeManager` / `DeviceHandler.Handle.GrabHalconImage(camParam)` → HImage. 단일 콜로 정지 프레임 1장 (별도 freeze 상태머신 불필요, RESEARCH Pattern 4 주의).

### HALCON 에러처리 (CLAUDE.md 컨벤션)
**Source:** `VisionAlgorithmService.cs` 전반, CLAUDE.md
**Apply to:** CheckerboardCalibrationService 모든 메서드
> 모든 `HOperatorSet.*` → `try { } catch { return false; }`. cleanup(measureHandle close 등)는 `try { ... } catch { }` 허용. Allman 브레이스(신규 Halcon 코드). C# 7.2 — switch expression/record/nullable-ref 금지. 코드 수정 시 `//260623 hbk Phase 53:` 주석 필수.

## No Analog Found

| File | Role | Data Flow | Reason |
|------|------|-----------|--------|
| (없음) | — | — | 모든 신규 파일이 기존 강 매치 analog 보유 |

> 단, **격자 정렬(행/열 클러스터링)** 과 **중앙↔외곽 편차%**(Pattern 2/3)는 직접 analog 없는 순수 신규 C# 수학. `SortAndTrimPercent`/`Median` 통계 헬퍼 스타일(static)만 차용, 그리드 버킷팅 로직 자체는 RESEARCH Pattern 2/3 공식대로 신규 작성. (caltab/undistort 도입 금지 — D-07/D-08.)

## Metadata

**Analog search scope:** `WPF_Example/Halcon/Algorithms`, `WPF_Example/UI/Dialog`, `WPF_Example/UI/Reviewer`, `WPF_Example/UI/ContentItem`, `WPF_Example/Device`, `WPF_Example/MainWindow.xaml.cs`
**Files scanned:** 7 (VisionAlgorithmService, TeachingWindow.xaml/.cs, MainView.xaml.cs, MainWindow.xaml.cs, DeviceHandler.cs, HalconViewerControl.xaml.cs)
**Pattern extraction date:** 2026-06-23
