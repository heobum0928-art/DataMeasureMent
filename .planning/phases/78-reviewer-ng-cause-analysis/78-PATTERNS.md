# Phase 78: 리뷰어 NG 원인 분석 - Pattern Map

**Mapped:** 2026-09-17
**Files analyzed:** 7개 (전부 기존 파일 수정 — 신규 .cs 파일 금지 제약)
**Analogs found:** 7 / 7 (전부 "같은 파일 안의 기존 이웃 패턴"을 그대로 복제 — 이 phase 에 새 아키텍처 없음)

> 이 phase 는 새 파일을 만들지 않는다(78-CONTEXT.md 핸드오프 제약: "신규 .cs 파일 금지"). 아래 표의
> "analog" 는 전부 **같은 파일 안에 이미 있는 형제 클래스/메서드**다 — 플래너는 이 표를 "이 위치에 이
> 스타일로 추가하라"로 읽으면 된다.

## File Classification

| 수정 대상 파일 | 역할(Role) | 데이터 흐름 | 같은 파일 내 Analog | 매치 품질 |
|---|---|---|---|---|
| `WPF_Example/UI/ViewModel/CycleResultDto.cs` (규칙 엔진 `NgCauseAnalyzer` + `NgCauseResult`/`DatumDiagnosticDto`/`ZCandidateScoreDto` 추가) | service(순수 함수) | transform | `ReviewerListLabelBuilder`(같은 파일 189행~) | exact — 동일 파일, 동일 "DTO만 입력받는 static 유틸 클래스" 패턴 |
| `WPF_Example/UI/ViewModel/ReviewMeasurementRow.cs` (`CauseText`/`EvidenceText`/`ActionText` 프로퍼티 추가) | model(뷰 바인딩용 read-only 프로퍼티) | transform | 같은 클래스의 `JudgeText` 계산 로직(생성자 내 분기) | exact — 동일 클래스, 생성자에서 채우는 계산된 문자열 프로퍼티 |
| `WPF_Example/UI/Reviewer/ReviewerWindow.xaml.cs` (배선만: 버튼 삭제 2개 + 신규 버튼 1개 핸들러 + 이미지 로드 경로 교체) | controller(WPF code-behind, request-response) | event-driven | `Button_ExportExcel_Click`(337행)/`Button_ChartSmoke_Click`(605행)/`MeasurementGrid_SelectionChanged`(237행) | exact — 같은 파일의 기존 버튼 핸들러·이미지 로드 패턴 |
| `WPF_Example/UI/Reviewer/ReviewerWindow.xaml` (버튼 2개 삭제, 버튼 1개 추가, 원인 3줄 패널 XAML 추가) | component(XAML) | request-response | 기존 `btn_exportExcel`/`btn_chartSmoke` 버튼 마크업 | exact |
| `WPF_Example/Custom/Export/ExcelExportService.cs` (`Export(CycleResultDto,string)` 삭제 + `NgAccumulationExportService` static class 추가) | service(file-I/O, xlsx) | batch/file-I/O | 같은 파일의 `Export` 메서드(43행) — "열기" 절반만 신규(RepeatExcelExportService 의 SaveAs 패턴과 짝) | role-match — CRUD(신규 저장)에서 append(기존 파일 열기)로 데이터 흐름만 다름 |
| `WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs` (`AlignMatchScore`/`AlignMatchRow`/`AlignMatchCol`/`AlignMatchAngleDeg` 필드 추가) | model(런타임 진단 필드) | CRUD(단순 write-back) | 같은 파일의 `Align2Score`(1082행)/`DetectedAngleDeg`(1123행) | exact — 이웃 필드와 완전히 동일한 목적(패턴매칭 진단값 보관) |
| `WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs` (`TryComposeAlign` 안에서 `datum.AlignMatchScore = curScore` 등 대입) | service(검사 로직 write-back) | event-driven | 같은 메서드 내 기존 `Align2Score` write-back 지점(주변 3435행) | exact |
| `WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs` (Z 후보 점수를 `meas`로 write-back) | service(검사 로직 write-back) | event-driven | 같은 파일의 `meas.LastSelectedZIndex = chosen.ZIndex;`(2394행 부근) | exact |
| `WPF_Example/Custom/Sequence/Inspection/MeasurementBase.cs` (신규 후보 점수 리스트 필드 + 초기화) | model | CRUD | 같은 파일 `LastFitScore`/`LastSelectedZIndex` 필드(124-125행) + 초기화(191-192행) | exact |
| `WPF_Example/Custom/Sequence/Inspection/CycleResultSerializer.cs` (`BuildDto`가 신규 필드 복사) | service(transform, DTO 매핑) | transform | 같은 메서드 내 기존 `fai.OriginImageFileName = ...` 류 복사 라인(97-110행) | exact |

## Pattern Assignments

### `WPF_Example/UI/ViewModel/CycleResultDto.cs` — `NgCauseAnalyzer` (service, transform)

**Analog:** 같은 파일 `ReviewerListLabelBuilder`(189행~) — "DTO 만 입력받는 순수 정적 클래스" 원칙, `SkipReason` 분기 스타일.

**Imports** (파일 상단, 1-5행 — 그대로 재사용, 추가 using 불필요):
```csharp
using System;
using System.Collections.Generic;
using System.Text;
using ReringProject.Halcon.Models;
using ReringProject.Sequence; // SkipReason 상수 참조용
```

**핵심 패턴 — 규칙 → 문구 매핑 스타일** (실제 코드, 210-228행):
```csharp
private static string BuildReasonText(MeasurementResultDto m)
{
    if (m.LastSkipReason == SkipReason.DATUM_FAIL)
    {
        return "DETECT FAIL";
    }
    else if (m.LastSkipReason == SkipReason.NO_IMAGE)
    {
        return "NO IMAGE";
    }
    else if (m.LastSkipReason == SkipReason.MEASURE_FAIL)
    {
        return ReviewMeasurementRow.JUDGE_MEASURE_FAIL;
    }
    else
    {
        return "공차이탈"; // LastHasResult && !LastJudgement
    }
}
```
`NgCauseAnalyzer.Analyze(...)` 의 R1~R4 분기는 이 `if/else if` 사슬을 그대로 복제한다(삼항/switch식 금지 — CLAUDE.md). `else` 마지막 분기 앞에 주석으로 "왜 이 분기인지" 남기는 스타일도 동일하게 유지.

**DTO 클래스 정의 스타일** (13-67행, `CycleResultDto`/`MeasurementResultDto` 필드 선언 관례):
```csharp
public class MeasurementResultDto
{
    public string MeasurementName { get; set; }
    ...
    /// <summary>범위 Shot 에서 이 측정이 채택한 z 번호. -1 = 범위 미적용·옛 JSON(Phase 77 SZF-04, D-77-07 ⑥).</summary>
    public int SelectedZIndex { get; set; } = MeasurementBase.SELECTED_Z_NONE;
}
```
`NgCauseResult`/`DatumDiagnosticDto`/`ZCandidateScoreDto` 는 이 스타일(자동 프로퍼티 + XML 주석으로 "왜/기본값" 설명)을 그대로 따른다. 옛 cycle.json 호환을 위해 리스트 필드는 `= new List<T>()` 이니셜라이저를 반드시 준다(파일 전역에서 `Shots`/`Measurements`/`LastOverlays` 등 전부 이 패턴).

**가드 절 스타일** (연구 문서 인용 — 새로 작성할 메서드 시그니처의 참고 형태, 아직 코드에 없음):
```csharp
public static NgCauseResult Analyze(CycleResultDto cycle, MeasurementResultDto measurement,
    FaiResultDto ownerFai, List<CycleResultDto> recentCyclesSameDate)
{
    if (cycle == null || measurement == null) { return NgCauseResult.Empty(); }
    // R1~R9 순서대로 if/else if 평가 (삼항/switch식 금지, 중괄호 생략 금지 — CLAUDE.md)
}
```

---

### `WPF_Example/UI/ViewModel/ReviewMeasurementRow.cs` — `CauseText`/`EvidenceText`/`ActionText` (model)

**Analog:** 같은 클래스 생성자(55행~)의 `JudgeText` 계산 — `JUDGE_MEASURE_FAIL` 상수(14행) 재사용 스타일.

**핵심 패턴** (실제 코드 확인):
```csharp
public class ReviewMeasurementRow
{
    public const string JUDGE_MEASURE_FAIL = "측정실패";
    ...
    public ReviewMeasurementRow(ShotResultDto shot, FaiResultDto fai, MeasurementResultDto m)
    {
        ...
        JudgeText = JUDGE_MEASURE_FAIL; // 112행 부근, if/else 분기 결과 대입
    }
}
```
신규 3개 프로퍼티는 생성자 안에서 `NgCauseAnalyzer.Analyze(...)` 호출 1번으로 채운다(계산 로직은 `NgCauseAnalyzer` 에만 있고, 이 클래스는 결과를 프로퍼티로 노출만 — CLAUDE.md "ViewModel 은 계산, View 는 바인딩만"과 같은 원칙을 한 단계 더 안쪽에서 지킨다).

---

### `WPF_Example/UI/Reviewer/ReviewerWindow.xaml.cs` — 배선만 (controller, event-driven)

**Analog (삭제 대상 2개 핸들러, 그대로 삭제):**
```csharp
// Source: ReviewerWindow.xaml.cs:337-370 — 삭제 대상 (D-78-06)
private void Button_ExportExcel_Click(object sender, RoutedEventArgs e)
{
    if (_currentCycle == null)
    {
        CustomMessageBox.Show("엑셀 export", "먼저 cycle 을 선택하세요.", MessageBoxImage.Warning);
        return;
    }
    ...
    bool ok = ExcelExportService.Export(_currentCycle, dlg.FileName);
    ...
}
```
```csharp
// Source: ReviewerWindow.xaml.cs:605-623 — 삭제 대상 (D-78-06)
private void Button_ChartSmoke_Click(object sender, RoutedEventArgs e)
{
    string szFolder = SystemHandler.Handle.Setting.ResultSavePath;
    string szMessage;
    bool bOk = ReringProject.Export.ChartImageCapture.TrySaveSmokePng(szFolder, out szMessage);
    ...
    CustomMessageBox.Show(szTitle, szMessage, icon);
}
```
`btn_exportExcel.IsEnabled = (_currentCycle != null);`(122행, `CycleList_SelectionChanged` 안)도 같이 삭제.

**신규 버튼 핸들러 analog (배선 1줄 스타일, D-78-06 유지 대상 "CPK 리포트 export"와 동일 패턴):**
```csharp
// Source: ReviewerWindow.xaml.cs:600 부근 — CustomMessageBox.Show("CPK 리포트 export", msg, icon); 스타일 재사용
// 신규 Button_NgAccumExport_Click 은 NgAccumulationExportService.AppendNgRows(...) 호출 1줄 +
//  성공/실패 CustomMessageBox 안내로 구성 — Button_ExportExcel_Click 의 try/dialog 골격을 재사용하되
//  SaveFileDialog 대신 고정 경로(O-78-01, ResultSavePath 하위) 사용.
```

**이미지 로드 경로 교체 대상 3곳 (NGA-06, `ResultImagePath` → `OriginImageFileName` 우선):**
```csharp
// Source: ReviewerWindow.xaml.cs:166-172 (DisplayCycle, cycle 전체 보기) — 교체 대상
var firstShot = cycle.Shots.FirstOrDefault();
if (firstShot != null
    && !string.IsNullOrEmpty(firstShot.ResultImagePath)
    && File.Exists(firstShot.ResultImagePath))  // 누락 이미지 시 overlay 만 렌더
{
    halconViewer.LoadImage(firstShot.ResultImagePath);
}
```
```csharp
// Source: ReviewerWindow.xaml.cs:254-260 (MeasurementGrid_SelectionChanged, 행 선택 시) — 교체 대상
string imgPath;
if (row.OwnerShot != null) imgPath = row.OwnerShot.ResultImagePath; else imgPath = null;
if (!string.IsNullOrEmpty(imgPath) && File.Exists(imgPath))
{
    halconViewer.LoadImage(imgPath);
}
```
두 지점 모두 "OriginImageFileName 우선 시도(파일 존재 확인) → 없으면 기존 `ResultImagePath` 폴백"으로 바꾼다. `if/else` 가드 스타일(중괄호 생략 없음)을 그대로 유지 — 삼항/null 조건 연산자 금지.

**공유 패턴(에러 안내 다이얼로그):**
```csharp
// Source: ReviewerWindow.xaml.cs:361-369 — 성공/실패 CustomMessageBox 패턴, NG 누적 엑셀 버튼도 동일하게 사용
bool ok = ExcelExportService.Export(_currentCycle, dlg.FileName);
string okMessage;
if (ok) okMessage = "저장 완료:\n" + dlg.FileName; else okMessage = "export 실패 (로그 확인)";
MessageBoxImage okIcon;
if (ok) okIcon = MessageBoxImage.Information; else okIcon = MessageBoxImage.Error;
CustomMessageBox.Show("엑셀 export", okMessage, okIcon);
```

---

### `WPF_Example/Custom/Export/ExcelExportService.cs` — `NgAccumulationExportService` (service, file-I/O)

**Analog:** 같은 파일 `Export(CycleResultDto, string)`(43행~) — 신규 파일 생성 패턴. `RepeatExcelExportService.cs:73,203` — `new XLWorkbook()` + `wb.SaveAs(outputPath)`(항상 새로 만듦, append 없음).

**기존 신규-생성 패턴** (실제 코드):
```csharp
// Source: ExcelExportService.cs:43-56
public static bool Export(CycleResultDto cycle, string outputPath)
{
    if (cycle == null || string.IsNullOrEmpty(outputPath))
    {
        return false;
    }

    try
    {
        using (var wb = new XLWorkbook())
        {
            var ws = wb.Worksheets.Add("Result");
            ws.Cell(1, 1).Value = "모델명";
            ...
```

**신규로 필요한 "열기 vs 새로 만들기" 분기 (이 저장소에 아직 없음 — ClosedXML 공식 API, RESEARCH.md Code Examples 인용):**
```csharp
bool bFileExists = File.Exists(outputPath);
XLWorkbook wb;
if (bFileExists)
{
    wb = new XLWorkbook(outputPath); // 기존 파일 열기
}
else
{
    wb = new XLWorkbook(); // 신규 생성 — Export()/RepeatExcelExportService 와 동일
}
using (wb)
{
    bool bSheetExists = wb.Worksheets.Contains(SHEET_NAME);
    IXLWorksheet ws;
    if (bSheetExists)
    {
        ws = wb.Worksheet(SHEET_NAME);
    }
    else
    {
        ws = wb.Worksheets.Add(SHEET_NAME);
        // 헤더 작성 — Export() 의 ws.Cell(1,1).Value = ... 스타일 재사용
    }
    // dedupe: CycleFolderPath+측정명(O-78-04) 열을 미리 읽어 HashSet 구성 후 없는 것만 append
    try
    {
        if (bFileExists) wb.Save(); else wb.SaveAs(outputPath);
    }
    catch (IOException ex)
    {
        // Pitfall 4 — 파일 잠김(사용자가 Excel 로 열어둠). CustomMessageBox 안내 후 중단.
    }
}
```

**공유 헬퍼(재사용, 재작성 금지 — internal, 같은 어셈블리라 바로 호출 가능):** `BuildJudgementText`/`LoadCaptureImageBytes`/`TryInsertCaptureImage`/`ApplyCaptureColumnWidth` — 이미 `RepeatExcelExportService.cs:294,346,366-367`가 재사용 중. `NgAccumulationExportService`도 사진 경로/판정 텍스트 컬럼에 그대로 호출한다.

**클래스 상단 상수 선언 스타일** (실제 코드, 23-38행 — 매직넘버 금지 원칙 그대로 적용):
```csharp
private const int CAPTURE_IMAGE_COLUMN = 11;
private const int CAPTURE_WAIT_TIMEOUT_MS = 1500;
private const int CAPTURE_WAIT_POLL_MS = 100;
private const int JPEG_MIN_BYTES = 4;
```

---

### `WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs` — 신규 Align 진단 필드 (model)

**Analog:** 같은 파일 `Align2Score`(1078-1082행), `DetectedAngleDeg`(1120-1123행) — 이웃에 그대로 추가.

**Transient(비직렬화 진단값) 스타일** (`Align2Score`, 실제 코드):
```csharp
// 패턴2 매칭 score. DetectedOrigin* 와 동일한 transient 취급(double 이라 INI 에 키는 생기나 소비처 없음).
[System.ComponentModel.Browsable(false)]
[PropertyTools.DataAnnotations.Browsable(false)]
[Newtonsoft.Json.JsonIgnore]
public double Align2Score { get; set; }
```

**PropertyGrid 노출(ReadOnly) 스타일** (`DetectedAngleDeg`, 실제 코드):
```csharp
[Category("Datum|Result")]
[System.ComponentModel.ReadOnly(true)]
[PropertyTools.DataAnnotations.ReadOnly(true)]
public double DetectedAngleDeg { get; set; }
```

**주의:** NGA-07 은 `AlignMatchScore/Row/Col/AngleDeg`를 **cycle.json 에 기록**해야 하므로(D-78-08), `Align2Score`(`[JsonIgnore]`)와 달리 `[Newtonsoft.Json.JsonIgnore]` 를 붙이면 안 된다 — `DetectedOriginRow`/`DetectedAngleDeg`(1032/1123행, JsonIgnore 없음)와 같은 직렬화 정책을 따른다. `[Category("Datum|Result")]` + `ReadOnly(true)` 데코는 그대로 복제해 PropertyGrid 에도 노출한다(연구 문서 권고).

---

### `WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs` — `TryComposeAlign` write-back

**Analog:** 같은 메서드 내 기존 `curScore`/`Align2Score` 처리부(연구 문서 인용, 3394-3405행).

```csharp
// Source: InspectionSequence.cs:3394-3405 (현재 코드, 수정 대상)
double curRow, curCol, curAngleDeg, curScore;
if (!svc.TryFindPose(refImage, datum.PatternEngine, modelPath, ..., out curRow, out curCol, out curAngleDeg, out curScore, out error, ...))
{
    return false; // ALIGN_FAIL
}
// NGA-07 추가 지점 — TryFindPose 성공 직후:
//   datum.AlignMatchScore = curScore;
//   datum.AlignMatchRow = curRow;
//   datum.AlignMatchCol = curCol;
//   datum.AlignMatchAngleDeg = curAngleDeg;
```
기존 `Align2Score` 대입 지점(3435행 부근, 2-패턴 baseline 보정 경로)과 나란히 두어 "1차/2차 패턴 점수 모두 저장"을 대칭적으로 만든다.

---

### `WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs` / `MeasurementBase.cs` — Z 후보 점수 write-back

**Analog:** `meas.LastSelectedZIndex = chosen.ZIndex;` 대입 패턴(2394행 부근) + `MeasurementBase` 초기화 지점.

```csharp
// Source: Action_FAIMeasurement.cs:2389-2394 (현재 코드, 수정 대상)
List<ZFocusRunResult> lstResults = RunZFocusCandidates(meas, transform, pixRes);
ZFocusRunResult chosen = PickZFocusResult(lstResults);
LogZFocusSelection(meas, lstResults, chosen, swMeasureExec);
RecordMeasurementResult(meas, false, chosen.Ok, chosen.Value, chosen.Error, chosen.Overlays, overlayAcc, faiOverlays, dctAlgoUsed, swMeasureExec, acc);
meas.LastSelectedZIndex = chosen.ZIndex;
// NGA-07 추가 지점 — RecordMeasurementResult(ClearResult 호출됨) 뒤에:
//   meas.LastZCandidateScores = BuildCandidateScoreList(lstResults); // 새 헬퍼, 병렬 리스트
```

**초기화 지점(반드시 같이 처리 — Pitfall 5, 잔재 방지):**
```csharp
// Source: MeasurementBase.cs:121-125 (필드 선언) + 191-192 (초기화, 실제 코드)
public const int SELECTED_Z_NONE = -1;
public double LastFitScore;
public int LastSelectedZIndex = SELECTED_Z_NONE;
...
LastFitScore = 0.0; // Phase 77: 이전 사이클 선택 점수 잔재 방지
LastSelectedZIndex = SELECTED_Z_NONE; // Phase 77: 이전 사이클 선택 Z 잔재 방지
```
신규 `LastZCandidateScores`(또는 병렬 리스트) 필드도 필드 선언부(124-125행 이웃)에 추가하고, 191-192행과 같은 지점에 `= null;`(또는 `.Clear()`) 리셋을 반드시 함께 넣는다.

---

### `WPF_Example/Custom/Sequence/Inspection/CycleResultSerializer.cs` — `BuildDto` 신규 필드 복사

**Analog:** 기존 `fai.OriginImageFileName = ...` 류 필드 복사 라인(97-110행 부근, DTO 매핑 관례 — 런타임 객체 필드를 DTO 프로퍼티로 1:1 대입).

```csharp
// 스타일 예시(연구 문서 인용, CycleResultSerializer.cs:97-110 근방 패턴 그대로 복제) —
//   fai.OriginImageFileName = shotFai.LastOriginImageFileName; 류의 단순 대입을
//   measDto.ZCandidateScores = BuildZCandidateScoreDtoList(meas.LastZCandidateScores); 로 확장,
//   datumDto.AlignMatchScore = datum.AlignMatchScore; 류로 Datum 진단값도 동일하게 복사.
```
`TypeNameHandling.None` + try/catch→null(RCE 방지) 정책은 `CycleResultSerializer.Load`가 이미 전역으로 갖고 있으므로 신규 필드도 별도 조치 없이 동일 보호를 상속한다.

## Shared Patterns

### 조건 분기(삼항/null 조건/switch식 금지)
**Source:** 프로젝트 전역(`ReviewerListLabelBuilder.BuildReasonText`, `Button_ExportExcel_Click` 의 `if (ok) okMessage = ...; else okMessage = ...;`)
**Apply to:** `NgCauseAnalyzer`, `ReviewMeasurementRow` 신규 프로퍼티, `ReviewerWindow.xaml.cs` 신규/수정 핸들러, `NgAccumulationExportService` 전부.
```csharp
string okMessage;
if (ok) okMessage = "저장 완료:\n" + dlg.FileName; else okMessage = "export 실패 (로그 확인)";
```

### 에러 안내 다이얼로그
**Source:** `ReviewerWindow.xaml.cs:341,366-369` `CustomMessageBox.Show(title, message, icon)`
**Apply to:** NG 누적 엑셀 버튼 핸들러(성공/실패/파일 잠김 3분기), 사진 없음 안내.

### Halcon/파일 존재 가드
**Source:** `ReviewerWindow.xaml.cs:168-171` `!string.IsNullOrEmpty(path) && File.Exists(path)`
**Apply to:** `OriginImageFileName` 우선 로드 3개 지점 전부(D-78-07).

### 옛 cycle.json 호환(Newtonsoft 기본값 폴백)
**Source:** `CycleResultDto.cs` 전역 — 모든 신규 필드가 `= new List<T>()` 또는 명시적 기본값(`= -1`, `= MeasurementBase.SELECTED_Z_NONE`)을 갖는 관례.
**Apply to:** `DatumDiagnosticDto`/`ZCandidateScoreDto`/`NgCauseResult`의 모든 필드, `NgCauseAnalyzer.Analyze`의 null 가드(`if (cycle == null || measurement == null) { return NgCauseResult.Empty(); }`).

### xlsx 삭제 범위 한정(Pitfall 1)
**Source:** `ExcelExportService.cs` — `Export(CycleResultDto,string)` 퍼블릭 진입점만 삭제 대상. `BuildJudgementText`/`LoadCaptureImageBytes`/`TryInsertCaptureImage`/`ApplyCaptureColumnWidth`는 `RepeatExcelExportService.cs`(유지 대상, 반복도 엑셀 export)가 재사용 중 — 삭제 금지.
**Apply to:** `ExcelExportService.cs` 수정 시 grep `ExcelExportService\.`/`ChartImageCapture\.` 전체 호출부를 먼저 확인.

## No Analog Found

| 파일 | 역할 | 데이터 흐름 | 이유 |
|------|------|-----------|------|
| ClosedXML "기존 파일 열기→append" 로직(`NgAccumulationExportService` 내부) | service | batch/file-I/O | 이 저장소의 기존 3개 export 서비스(`ExcelExportService`/`RepeatExcelExportService`/`CpkReportExportService`) 전부 `SaveFileDialog`로 **신규** 파일만 생성 — "기존 파일 열기→append" 패턴은 처음. ClosedXML 공식 API(`new XLWorkbook(path)`)를 그대로 쓰되, 이 프로젝트에 실사용 예가 없으므로(RESEARCH.md Assumption A5) 계획 단계에서 소규모 tracer로 1회 실증 권고. |

## Metadata

**Analog search scope:** `WPF_Example/UI/ViewModel/`, `WPF_Example/UI/Reviewer/`, `WPF_Example/Custom/Export/`, `WPF_Example/Custom/Sequence/Inspection/`
**Files scanned:** `CycleResultDto.cs`, `ReviewMeasurementRow.cs`, `ReviewerWindow.xaml.cs`, `ExcelExportService.cs`, `RepeatExcelExportService.cs`, `DatumConfig.cs`, `MeasurementBase.cs`, `InspectionSequence.cs`(RESEARCH.md 인용 라인 재확인), `Action_FAIMeasurement.cs`(RESEARCH.md 인용 라인 재확인)
**Pattern extraction date:** 2026-09-17
