# Phase 72: CPK 데이터 리포트 출력 재설계 - Pattern Map

**Mapped:** 2026-08-18
**Files analyzed:** 10 (신규 3 / 수정 7)
**Analogs found:** 9 / 10

> **⚠ 계약 우선순위:** CONTEXT.md의 **D-04 (REVISED 260818)** 가 RESEARCH.md의 D-04(자재번호별 동적 시트)보다 우선한다.
> 확정: **시트는 2장 고정** (`RAW DATA(1)`, `1Cav 세부치수_Cpk`), **자재번호(`IndexNumber`)는 시트 축이 아니라 열 축**(`#1`,`#2`,`#3`…).
> RESEARCH.md Pattern 4(GroupBy → 시트 동적 생성)는 **채택하지 않는다**. GroupBy는 "열 순서 결정"에만 쓴다.
> RESEARCH Pattern 4의 코드 예시는 삼항(`group.Key >= 0 ? ... : ...`)을 쓰고 있으므로 **그대로 복사 금지** — 아래 §Shared Patterns/삼항 제거 참조.

---

## File Classification

| New/Modified File | New? | Role | Data Flow | Closest Analog | Match Quality |
|---|---|---|---|---|---|
| `WPF_Example/Custom/Export/CpkReportExportService.cs` | NEW | service (export) | batch / transform | `WPF_Example/Custom/Export/RepeatExcelExportService.cs` | exact |
| `WPF_Example/Custom/Sequence/Inspection/RepeatMeasurementStats.cs` | MODIFY | model + aggregator | transform | (self — 기존 `ComputeAll()` 내부 Cpk 블록) | exact (in-file) |
| `WPF_Example/UI/Statistics/ChartRenderService.cs` | NEW | utility (렌더 헬퍼) | transform (in-memory) | `WPF_Example/UI/Statistics/StatisticsWindow.xaml.cs` `RenderHistogram/RenderTrend` | exact (로직 이동) |
| `WPF_Example/Custom/Export/ChartImageCapture.cs` (또는 CpkReportExportService 내부 private) | NEW | utility (Canvas→PNG) | transform (file-less I/O) | `ExcelExportService.TryInsertCaptureImage` (삽입부만) | partial |
| `WPF_Example/UI/Statistics/StatisticsWindow.xaml.cs` | MODIFY | UI code-behind | event-driven | (self — 얇은 래퍼로 축소) | exact (in-file) |
| `WPF_Example/Custom/Sequence/Inspection/RepeatRunService.cs` | MODIFY | service (실행) | event-driven | `BatchRunService.cs` (쌍둥이 패턴) | exact |
| `WPF_Example/Custom/Sequence/Inspection/BatchRunService.cs` | MODIFY | service (실행) | event-driven | `RepeatRunService.cs` (쌍둥이 패턴) | exact |
| `WPF_Example/UI/Reviewer/ReviewerWindow.xaml` | MODIFY | view (XAML) | — | 같은 파일 `btn_repeatRun`/`lbl_repeatProgress` StackPanel | exact (in-file) |
| `WPF_Example/UI/Reviewer/ReviewerWindow.xaml.cs` | MODIFY | UI code-behind | event-driven | 같은 파일 `Button_RepeatRun_Click` / `Button_RepeatExport_Click` | exact (in-file) |
| `WPF_Example/DatumMeasurement.csproj` | MODIFY | config | — | 기존 `<Compile Include="Custom\Export\...">` 블록 | exact |

---

## Pattern Assignments

### 1. `WPF_Example/Custom/Export/CpkReportExportService.cs` (NEW — service, batch/transform)

**Analog:** `WPF_Example/Custom/Export/RepeatExcelExportService.cs`

이 파일이 **가장 중요한 복사 원본**이다. 클래스 형태(`public static class`), 진입 API 형태, 예외 정책, 시트 작성 루프, `AdjustToContents` 마감까지 그대로 따른다.

**Imports pattern** (RepeatExcelExportService.cs:1-11) — 그대로 복사, `System.IO`/`ClosedXML.Excel.Drawings` 추가:
```csharp
//260818 hbk Phase 72 CPK 데이터 리포트 (RAW DATA + 세부치수_Cpk 2시트)
using ClosedXML.Excel;
using ReringProject.Sequence;
using ReringProject.Setting;
using ReringProject.UI;
using ReringProject.Utility;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace ReringProject.Export
{
```
- 네임스페이스는 폴더(`Custom/Export`)와 무관하게 **`ReringProject.Export`** 고정.
- `ReringProject.UI` = `CycleResultDto` 계열, `ReringProject.Sequence` = `RepeatMeasurementStats`/`SkipReason`.

**공개 진입 API 패턴** (RepeatExcelExportService.cs:38-55) — public 얇은 래퍼 + private `...Internal`:
```csharp
        /// <summary>
        /// 반복검사(Gage R&R) export. 집계 2시트만 생성한다 — 기존 동작/포맷 그대로.
        /// </summary>
        public static bool Export(List<CycleResultDto> cycles, string recipeName, string outputPath)
        {
            return ExportInternal(cycles, recipeName, outputPath, false);
        }

        private static bool ExportInternal(List<CycleResultDto> cycles, string recipeName, string outputPath, bool bWithDetailSheet)
        {
            if (cycles == null || cycles.Count == 0 || string.IsNullOrEmpty(outputPath))
            {
                return false;
            }
```
→ Phase 72는 `public static bool ExportCpkReport(List<CycleResultDto> cycles, string recipeName, string outputPath)` 하나면 충분(플래그 불필요).
**가드 3종(null / Count==0 / 경로 빈값 → false)은 반드시 동일하게 유지.**

**통계 계산 + 워크북 생성 골격** (RepeatExcelExportService.cs:62-76, 203-206):
```csharp
            try
            {
                // 시트1용 통계 계산
                var stats = new RepeatMeasurementStats();
                foreach (var c in cycles)
                {
                    stats.AddSample(c);
                }

                var statDict = stats.ComputeAll();

                using (var wb = new XLWorkbook())
                {
                    var ws1 = wb.Worksheets.Add("반복도 통계");
                    ...
                    wb.SaveAs(outputPath);
                }

                return true;
            }
```
→ Phase 72: `wb.Worksheets.Add("RAW DATA(1)")` 와 `wb.Worksheets.Add("1Cav 세부치수_Cpk")` **정확히 2장**만 Add.

**에러 핸들링 패턴** (RepeatExcelExportService.cs:208-217) — **이 형태를 글자 그대로 복사**:
```csharp
            catch (Exception ex)
            {
                try
                {
                    Logging.PrintErrLog((int)ELogType.Error, "[RepeatExcelExportService] Export failed: " + ex.Message);
                }
                catch { }

                return false;
            }
```
- 로깅 자체가 실패해도 export가 죽지 않도록 **로깅을 bare catch로 한 번 더 감싼다** — 이 프로젝트 관례.
- `(int)ELogType.Error` 캐스팅 필수.
- 태그는 `[CpkReportExportService]` 로 교체.

**헤더 배열 → 루프 기록 패턴** (RepeatExcelExportService.cs:86-92):
```csharp
                    string[] h1 = { "Shot", "FAI", "측정명", "N", "측정값", "Spec", "편차",
                                    "Tol+", "Tol-", "OK수", "NG수", "DETECT_FAIL수" };
                    for (int i = 0; i < h1.Length; i++)
                    {
                        ws1.Cell(5, i + 1).Value = h1[i];
                    }
```
→ RAW DATA 고정 헤더(`Number/도면항목설명/측정방식/설계값/상한공차/하한공차`)와 Cpk 시트 좌측(B~L)/우측(N~V) 헤더 모두 이 배열+루프 형태로. **가변 열(`#1`,`#2`…자재열)은 배열 뒤에 별도 for 루프로 이어붙인다.**

**셀 기록 = `.Value` 고정값만 (D-02)** (RepeatExcelExportService.cs:97-109):
```csharp
                        ws1.Cell(r, 4).Value = s.N;
                        ws1.Cell(r, 5).Value = Math.Round(s.Mean, 6);                       //측정값
                        ws1.Cell(r, 6).Value = s.NominalValue;                              //Spec
                        ws1.Cell(r, 7).Value = Math.Round(Math.Abs(s.Mean - s.NominalValue), 6);
```
- **`FormulaA1` 절대 사용 금지** (D-02). 코드베이스 전체에 `FormulaA1` 사용처 0건 — 이 관례를 깨지 말 것.
- 소수 자릿수 관례: `Math.Round(x, 6)`.
- null 문자열은 `?? ""` 로 방어 (`s.ShotName ?? ""`).

**중첩 순회 + null 가드 패턴** (RepeatExcelExportService.cs:119-160) — RAW DATA pivot 루프의 뼈대:
```csharp
                    foreach (var cycle in cycles)
                    {
                        if (cycle.Shots == null)
                        {
                            continue;
                        }

                        foreach (var shot in cycle.Shots)
                        {
                            if (shot.FAIs == null)
                            {
                                continue;
                            }

                            foreach (var fai in shot.FAIs)
                            {
                                if (fai.Measurements == null)
                                {
                                    continue;
                                }

                                foreach (var m in fai.Measurements)
                                {
```
- 각 레벨마다 `continue` 가드 — 이 4중 루프 형태가 프로젝트 표준.
- 키 규칙은 `RepeatMeasurementStats.AddSample()`(RepeatMeasurementStats.cs:88)과 **반드시 동일**해야 조인이 성립:
```csharp
  string key = (shot.ShotName ?? "") + "/" + (fai.FAIName ?? "") + "/" + (m.MeasurementName ?? "");
```

**측정값 유무 판별 = `LastHasResult`** (RepeatExcelExportService.cs:336-344) — **0.0도 정상값이므로 값으로 판별 금지 (CO-23-01)**:
```csharp
            // 측정값: 0.0 도 정상 결과이므로 값이 아니라 LastHasResult 로 판별한다 (CO-23-01).
            if (m.LastHasResult)
            {
                ws.Cell(nRow, 8).Value = m.LastMeasuredValue;
            }
            else
            {
                ws.Cell(nRow, 8).Value = "-";
            }
```
→ RAW DATA 매트릭스의 결측 칸도 이 `"-"` 관례를 재사용 (RESEARCH Open Question #2 권고와 일치).

**시트 마감** (RepeatExcelExportService.cs:112, 293-294):
```csharp
                    ws1.Columns().AdjustToContents();
                    // 그림이 있으면 AdjustToContents '뒤에' 컬럼 폭을 다시 잡는다
                    ExcelExportService.ApplyCaptureColumnWidth(ws, DETAIL_CAPTURE_IMAGE_COLUMN);
```
→ 차트 이미지를 넣는 시트도 **`AdjustToContents()` 이후에** 이미지 앵커/폭 조정.

**매직넘버 상수화 관례** (RepeatExcelExportService.cs:22-28):
```csharp
        private const string DETAIL_SHEET_NAME = "회차별 상세";
        private const int DETAIL_CAPTURE_IMAGE_COLUMN = 12;
        private const int BATCH_CAPTURE_WAIT_PER_CYCLE_MS = 1000;
```
→ 시트명(`RAW_SHEET_NAME`/`CPK_SHEET_NAME`), 헤더 행 번호, 첫 자재열 컬럼 인덱스, 차트 앵커 셀, Cpk 경고 임계값(`CPK_WARN_THRESHOLD = 1.33`) 전부 `private const` 로. **매직넘버 인라인 금지(D-00 관례).**

**판정 3분기 헬퍼 재사용/미러:** `ExcelExportService.BuildJudgementText(m)` (ExcelExportService.cs:319-353)는 `internal static` 이므로 같은 어셈블리에서 직접 호출 가능 — RAW DATA 결측 사유 표기가 필요하면 재사용. Cpk 시트의 X열 3단계 판정(`NG` > `Cpk` 경고 > `OK`)은 **새 헬퍼**로 만들되 아래 if-else 사다리 형태를 미러:
```csharp
            if (m.LastSkipReason == SkipReason.DATUM_FAIL)
            {
                return "DETECT FAIL";
            }
            else if (m.LastSkipReason == SkipReason.NO_IMAGE)
            {
                return "NO IMAGE";
            }
            else if (m.LastHasResult)
            {
                ...
            }
            else
            {
                return "-";
            }
```

---

### 2. `WPF_Example/Custom/Sequence/Inspection/RepeatMeasurementStats.cs` (MODIFY — model/aggregator, transform)

**Analog:** 자기 자신. 기존 Cpk 블록 **바로 옆에** Cp/UCPK/LCPK를 추가하고, 원시값 Series를 노출한다.

**확장할 DTO** (RepeatMeasurementStats.cs:13-30) — 기존 필드 순서/스타일 유지, `Cpk` 뒤에 3개 추가:
```csharp
    public class MeasurementStat
    {
        public string ShotName { get; set; }
        ...
        public double Range { get; set; }
        public double Cpk { get; set; }
        // ↓ Phase 72 추가 지점 (기존 필드 사이 삽입 금지 — 뒤에 append)
        // public double Cp { get; set; }
        // public double UCpk { get; set; }
        // public double LCpk { get; set; }
        // public double MinValue { get; set; }   // 참고파일 O열
        // public double MaxValue { get; set; }   // 참고파일 N열
        public double NominalValue { get; set; }
```
> `ComputeAll()`은 이미 루프에서 `minVal`/`maxVal`을 계산(RepeatMeasurementStats.cs:150-167)하지만 `Range`만 내보내고 버린다 — Max/Min 컬럼은 **새 계산이 아니라 기존 지역변수 노출**이다.

**핵심 계산 패턴 — 이 블록에 그대로 삽입** (RepeatMeasurementStats.cs:175-187):
```csharp
                    // Cpk = min((USL-mean)/(3*sigma), (mean-LSL)/(3*sigma))
                    double usl = d.LastNominal + d.LastTolPlus;
                    double lsl = d.LastNominal - Math.Abs(d.LastTolMinus);
                    if (stddev == 0)
                    {
                        cpk = double.PositiveInfinity;
                    }
                    else
                    {
                        double cpkUpper = (usl - mean) / (3 * stddev);
                        double cpkLower = (mean - lsl) / (3 * stddev);
                        cpk = Math.Min(cpkUpper, cpkLower);
                    }
```
**Phase 72 확장 지침 (Pitfall 3):**
- `cpkUpper`/`cpkLower` 는 이미 존재 → **지역변수를 승격**해 `UCpk`/`LCpk` 로 내보내기만 한다. 새 계산 추가 금지.
- `Cp = (d.LastTolPlus + Math.Abs(d.LastTolMinus)) / (6 * stddev)` 를 **같은 `if (stddev == 0)` 분기 안**에 넣고, 0 분기에서는 `cp = double.PositiveInfinity;` — Cpk와 가드가 갈라지면 엑셀 표시가 어긋난다.
- `stddev == 0` 판정 관례를 그대로 쓸 것(엡실론 비교로 바꾸지 말 것 — 기존 동작 변경).

**결과 객체 생성 패턴** (RepeatMeasurementStats.cs:190-207) — object initializer, 필드당 1줄:
```csharp
                result[kv.Key] = new MeasurementStat
                {
                    ShotName = d.ShotName,
                    ...
                    Cpk = cpk,
                    NominalValue = d.LastNominal,
                };
```

**Series 노출 (RESEARCH Pattern 2)** — `_data`는 private이므로 **신규 public 메서드**를 추가. 기존 `ComputeAll()` 시그니처는 건드리지 말 것(호출부 3곳: `RepeatExcelExportService`, `StatisticsWindow` 경로, 신규 서비스):
```csharp
        /// <summary>측정키별 원시 측정값 리스트. RAW DATA 매트릭스/차트 렌더 공용. 내부 리스트 복사본을 준다.</summary>
        public Dictionary<string, List<double>> GetSeries()
```
- 내부 `List<double>` 참조를 그대로 반환하면 외부 변조 위험 → `new List<double>(d.Values)` 복사본.
- 단, **`Values`는 `LastSkipReason==DATUM_FAIL/NO_IMAGE`인 회차를 아예 넣지 않는다**(RepeatMeasurementStats.cs:108-123) → **RAW DATA 매트릭스의 "회차 열 정렬"에는 쓸 수 없다.** RAW DATA 열 축은 cycle 인덱스 기반 별도 pivot(위 §1의 4중 루프)으로 만들고, `GetSeries()`는 **차트/통계용**으로만 쓸 것. 이 구분을 혼동하면 회차 열이 밀린다.

---

### 3. `WPF_Example/UI/Statistics/ChartRenderService.cs` (NEW — utility, in-memory transform)

**Analog:** `WPF_Example/UI/Statistics/StatisticsWindow.xaml.cs` (`RenderHistogram` 252-373, `RenderTrend` 376-432 및 그 private 헬퍼들)

**추출 계약:** 로직은 **순수 이동**(pure move). 알고리즘/좌표/여백 상수를 손대면 기존 통계창 시각 회귀가 난다.

**현재 형태 — Window 필드에 결합** (StatisticsWindow.xaml.cs:252-266):
```csharp
        private void RenderHistogram(List<double> values, double dUsl, double dLsl)
        {
            canvas_Histogram.Children.Clear();
            double dW = canvas_Histogram.ActualWidth;
            double dH = canvas_Histogram.ActualHeight;
            if (dW <= 0 || dH <= 0)
            {
                return;
            }

            if (values == null || values.Count == 0)
            {
                DrawNoDataText(canvas_Histogram, dW, dH);
                return;
            }
```
**추출 후 시그니처(권장):**
```csharp
        public static void RenderHistogram(Canvas canvas, double dW, double dH, List<double> values, double dUsl, double dLsl)
```
- `ActualWidth/ActualHeight` 를 **인자 `dW`/`dH` 로 치환**해야 한다 — 오프스크린 bare Canvas는 레이아웃 전 `ActualWidth == 0` 이라 기존 코드 그대로면 `return` 으로 조용히 빈 그림이 나온다. **이게 이 파일에서 가장 흔한 실패 모드다.**
- `canvas_Histogram` → 인자 `canvas` 로 전 치환.
- 함께 옮겨야 하는 private 헬퍼: `BuildHistogramBins`, `DrawNoDataText`, `DrawAxisLines`, `DrawVLine`, `DrawYTicksCount`, `DrawYTicksValue`, `DrawTrendXLabels`, `DrawTrendSpecMarks`, `ComputePaddedRange`, `CreateLabel`, `MinOf`, `MaxOf`, `MakeFrozenBrush`.

**브러시 상수 패턴 — 반드시 함께 이동** (StatisticsWindow.xaml.cs:65-71):
```csharp
        //260707 hbk quick-260707-fdx WPF Canvas 렌더용 고정 브러시(Freeze — 성능/스레드 안전)
        private static readonly SolidColorBrush m_brushBar = MakeFrozenBrush(0x33, 0x66, 0xCC);
        private static readonly SolidColorBrush m_brushSpec = MakeFrozenBrush(0xCC, 0x00, 0x00);
```
- `Freeze()` 된 브러시라 스레드 안전 — 오프스크린 렌더에서도 그대로 유효.

**렌더 상수** (StatisticsWindow.xaml.cs:60-62) — 함께 이동, 값 변경 금지:
```csharp
        private const int BIN_COUNT = 20;
        private const int MAX_X_LABELS = 5;
        private const double MERGE_PX = 12.0;
```

**표시 문자열 관례 (∞/NaN 방어) — export 텍스트에도 재사용** (StatisticsWindow.xaml.cs:184-198):
```csharp
        /// <summary>Cpk 표시 문자열 — 무한대/NaN 방어(if/else, 삼항 금지).</summary>
        private string CpkToText(double dCpk)
        {
            if (double.IsPositiveInfinity(dCpk))
            {
                return "∞";
            }

            if (double.IsNegativeInfinity(dCpk) || double.IsNaN(dCpk))
            {
                return "-";
            }

            return dCpk.ToString("F3");
        }
```
→ Cpk/Cp/UCPK/LCPK 셀은 `stddev==0` 시 `PositiveInfinity` 가 되므로 **엑셀에 raw double을 넣으면 안 된다**. 이 변환기를 export 쪽에서 공유(또는 동일 로직 미러)할 것.

---

### 4. `WPF_Example/Custom/Export/ChartImageCapture.cs` (NEW — utility, Canvas→PNG→xlsx)

**Analog (부분):** `WPF_Example/Custom/Export/ExcelExportService.cs` `TryInsertCaptureImage` (240-305)
> Canvas→`RenderTargetBitmap` 캡처는 **코드베이스 내 전례 없음**(RESEARCH A1). 아래 §No Analog Found 참조. **삽입 절반**만 아래 검증된 패턴을 따른다.

**검증된 AddPicture 패턴** (ExcelExportService.cs:240-305) — `Jpeg`→`Png` 만 교체:
```csharp
        /// <summary>
        /// 종횡비를 유지한 채 셀 박스 안에 맞춰 그림을 넣는다. 실패해도 export 는 계속된다.
        /// </summary>
        internal static bool TryInsertCaptureImage(IXLWorksheet ws, int nRow, int nColumn, byte[] arrBytes)
        {
            bool bHasBytes = arrBytes != null && arrBytes.Length > 0;
            if (!bHasBytes)
            {
                return false;
            }

            try
            {
                IXLPicture pic;
                using (var ms = new MemoryStream(arrBytes))
                {
                    pic = ws.AddPicture(ms, XLPictureFormat.Jpeg);   // Phase 72: XLPictureFormat.Png
                }

                int nOriginalWidth = pic.OriginalWidth;
                int nOriginalHeight = pic.OriginalHeight;
                bool bInvalidSize = nOriginalWidth <= 0 || nOriginalHeight <= 0;
                if (bInvalidSize)
                {
                    pic.Delete();
                    return false;
                }

                double dScaleWidth = (double)CAPTURE_BOX_WIDTH_PX / nOriginalWidth;
                double dScaleHeight = (double)CAPTURE_BOX_HEIGHT_PX / nOriginalHeight;
                double dScale = dScaleWidth;
                if (dScaleHeight < dScale)
                {
                    dScale = dScaleHeight;
                }
                if (dScale > 1.0)
                {
                    dScale = 1.0;   // 원본보다 키우지 않는다
                }
                ...
                pic.WithPlacement(XLPicturePlacement.Move);
                pic.WithSize(nTargetWidth, nTargetHeight);
                pic.MoveTo(ws.Cell(nRow, nColumn));

                ws.Row(nRow).Height = CAPTURE_BOX_HEIGHT_PX * EXCEL_POINTS_PER_PIXEL;

                return true;
            }
            catch (Exception ex)
            {
                try
                {
                    Logging.PrintErrLog((int)ELogType.Error, "[ExcelExportService] capture image insert failed (row " + nRow + "): " + ex.Message);
                }
                catch { }
                return false;
            }
        }
```
**복사 시 지킬 것:**
- `using (var ms = ...)` 블록 **안에서 AddPicture, 밖에서 스케일 조작** — ClosedXML이 스트림을 즉시 소비하는 이 순서가 검증된 형태.
- 실패해도 `false`만 반환하고 export 전체는 계속 — **throw 금지**.
- `bHasBytes` / `bInvalidSize` 처럼 **bool 조건을 `b`-접두 지역변수로 뽑아 쓰는** 헝가리언 관례.

**단위 환산 상수** (ExcelExportService.cs:31-36) — 차트 박스 크기도 이 방식으로:
```csharp
        // 셀 안 이미지 표시 박스(픽셀)
        private const int CAPTURE_BOX_WIDTH_PX = 160;
        private const int CAPTURE_BOX_HEIGHT_PX = 120;

        // 엑셀 단위 환산: 컬럼 폭은 문자수, 행 높이는 포인트(96dpi 기준 1px = 0.75pt)
        private const double EXCEL_PIXELS_PER_WIDTH_UNIT = 7.0;
        private const double EXCEL_POINTS_PER_PIXEL = 0.75;
```

---

### 5. `WPF_Example/UI/Statistics/StatisticsWindow.xaml.cs` (MODIFY — UI code-behind, event-driven)

**계약:** 기존 대화형 통계창 동작 **완전 무변경**. 메서드는 얇은 위임 래퍼로만 축소.

**변경 전** (StatisticsWindow.xaml.cs:245-248):
```csharp
            double dUsl = row.NominalValue + row.TolerancePlus;
            double dLsl = row.NominalValue - Math.Abs(row.ToleranceMinus);
            RenderHistogram(values, dUsl, dLsl);
            RenderTrend(values, row.Mean, dUsl, dLsl);
```
**변경 후 래퍼 형태(권장):**
```csharp
        private void RenderHistogram(List<double> values, double dUsl, double dLsl)
        {
            ChartRenderService.RenderHistogram(canvas_Histogram, canvas_Histogram.ActualWidth, canvas_Histogram.ActualHeight, values, dUsl, dLsl);
        }
```
- `ActualWidth/ActualHeight` 를 **호출자(Window)가 넘긴다** — 이 위치 이동이 헤드리스 재사용의 핵심.
- `ClearCharts()`(679-680)와 `Canvas_SizeChanged`(220-223) 경로는 그대로 유지.
- USL/LSL 산출식 `Nominal + TolPlus` / `Nominal - Math.Abs(TolMinus)` 는 `RepeatMeasurementStats.ComputeAll()`(176-177)과 **동일** — export 쪽에서도 이 식을 그대로 써야 값이 일치한다.

---

### 6. `RepeatRunService.cs` / `BatchRunService.cs` (MODIFY — service, event-driven) — D-05 자재번호 전파

**Analog:** 두 파일이 서로의 쌍둥이. **한쪽에 넣은 변경을 반대쪽에도 동일 형태로** 넣는다(코드 중복 최소화 원칙보다 이 두 파일은 의도적 병행 구조).

**갭 지점 — `nIndexNumber` 미전달** (RepeatRunService.cs:234-236, BatchRunService.cs:158-160 동일):
```csharp
                string recipeName = SystemHandler.Handle.Setting.CurrentRecipeName;
                CycleResultDto dto = CycleResultSerializer.BuildDto(
                    recipeManager, resultType, DateTime.Now, recipeName, seqName);
```
**대상 시그니처** (CycleResultSerializer.cs:35-41) — 이미 6번째 optional 파라미터가 존재. **BuildDto 수정 불필요**:
```csharp
        public static CycleResultDto BuildDto(
            InspectionRecipeManager recipeManager,
            EVisionResultType cycleResult,
            DateTime when,
            string recipeName,
            string ownerSequenceName = null,
            int nIndexNumber = -1)   //260622 hbk Phase 48 PROTO-01: 자재번호 전파 (기본 -1 미수신)
```
→ 서비스에 `public int MaterialIndexNumber { get; set; } = -1;` 류의 필드를 두고 마지막 인자로 넘긴다.

**Start 계열 파라미터 추가 패턴** (RepeatRunService.cs:42, 72) — 기존 호출부 회귀 0을 위해 **optional 파라미터** 또는 프로퍼티 사전 설정:
```csharp
        public void Start(InspectionSequence seq, int targetCount = DEFAULT_REPEAT_COUNT)
        public void StartFromImages(InspectionSequence seq, List<string> imagePaths)
```
- 상태 초기화 블록(54-58 / 89-94)에 자재번호 초기화도 같이 넣을 것:
```csharp
            IsRunning = true;
            _seq = seq;
            _imagePaths = imagePaths;
            TargetCount = imagePaths.Count;
            CompletedCount = 0;
            _collected = new List<CycleResultDto>();
```
- `Stop()`(103-114)에서 상태를 되돌리는 대칭도 유지 — 단, 자재번호는 다음 실행에서 다시 지정되므로 리셋 여부는 재량.
- 센티널 상수: `-1` 을 인라인하지 말고 `private const int MATERIAL_NOT_SET = -1;` (참조: `CaptureImageSaveService.cs:362 FILENAME_NO_MATERIAL = -1`).

**HandleFinish 스레딩 계약:** 전체가 `lock (_lock)` 안(153-256). 자재번호 읽기도 이 락 안에서. `_seq` 를 두 번 읽지 않는 TOCTOU 회피 관례(169-184)를 새 코드에도 적용.

---

### 7. `WPF_Example/UI/Reviewer/ReviewerWindow.xaml` + `.xaml.cs` (MODIFY — view + code-behind) — D-05 입력 UI

**Analog:** 같은 파일의 반복검사 패널 블록.

**XAML 삽입 지점** (ReviewerWindow.xaml:36-47) — `btn_repeatRun` 위에 자재번호 입력 1줄 추가:
```xml
                <!-- 260612 hbk Phase 41.1 OUT-03/OUT-04 반복도 실행 UI -->
                <Separator Margin="0,6,0,6"/>
                <Button x:Name="btn_repeatRun" Content="이미지 폴더 반복 검사"
                        Click="Button_RepeatRun_Click"
                        Padding="8,4" HorizontalAlignment="Stretch"/>
                <TextBlock x:Name="lbl_repeatProgress" Text=""
                           FontSize="11" Foreground="#334155"
                           Margin="0,4,0,0" HorizontalAlignment="Center"/>
                <Button x:Name="btn_repeatExport" Content="반복도 엑셀 export"
                        Click="Button_RepeatExport_Click"
                        IsEnabled="False"
                        Padding="8,4" Margin="0,4,0,0" HorizontalAlignment="Stretch"/>
```
- 명명 규칙: `btn_` / `lbl_` / (신규) `txt_materialIndex` — camelCase 접두.
- 스타일 관례: `Padding="8,4"`, `Margin="0,4,0,0"`, `FontSize="11"`, `Foreground="#334155"`, `HorizontalAlignment="Stretch"`.

**입력 검증 + 사용자 알림 패턴** (ReviewerWindow.xaml.cs:345-361):
```csharp
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
            {
                CustomMessageBox.Show("반복 검사", "선택한 폴더가 존재하지 않습니다.", MessageBoxImage.Warning);
                return;
            }
```
→ 자재번호는 `int.TryParse` 로 파싱하고, 빈 입력이면 `-1`(미지정) 폴백. 파싱 실패는 위 형태로 경고 후 `return`.
```csharp
            int nMaterialIndex = MATERIAL_NOT_SET;
            string szMaterial = txt_materialIndex.Text;
            if (!string.IsNullOrWhiteSpace(szMaterial))
            {
                if (!int.TryParse(szMaterial.Trim(), out nMaterialIndex))
                {
                    CustomMessageBox.Show("반복 검사", "자재번호는 숫자만 입력하세요.", MessageBoxImage.Warning);
                    return;
                }
            }
```

**결과 콜백 = Dispatcher.Invoke 마샬링 필수** (ReviewerWindow.xaml.cs:391-407) — 이 관례를 신규 export 트리거에도 적용:
```csharp
            _repeatService.OnProgressChanged += (current, total) =>
            {
                Dispatcher.Invoke(() =>
                {
                    lbl_repeatProgress.Text = "진행 중: " + current + "/" + total;
                });
            };
```

**export 버튼 핸들러 패턴 (신규 CPK export 버튼용 원본)** (ReviewerWindow.xaml.cs:413-449):
```csharp
        private void Button_RepeatExport_Click(object sender, RoutedEventArgs e)
        {
            if (_repeatCycles == null || _repeatCycles.Count == 0)
            {
                CustomMessageBox.Show("반복도 export", "반복 실행 완료 후 사용하세요.", MessageBoxImage.Warning);
                return;
            }

            string initialDir = SystemHandler.Handle.Setting.ResultSavePath;
            string recipeName = SystemHandler.Handle.Setting.CurrentRecipeName ?? "";

            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "Excel 파일 (*.xlsx)|*.xlsx",
                FileName = "repeat_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".xlsx",
                InitialDirectory = initialDir
            };

            if (dlg.ShowDialog() == true)
            {
                bool ok = ReringProject.Export.RepeatExcelExportService.Export(
                    _repeatCycles, recipeName, dlg.FileName);
                string msg;
                if (ok)
                {
                    msg = "저장 완료:\n" + dlg.FileName;
                }
                else
                {
                    msg = "export 실패 (로그 확인)";
                }

                MessageBoxImage icon;
                if (ok)
                {
                    icon = MessageBoxImage.Information;
                }
                ...
```
- **완전 정규화된 호출** `ReringProject.Export.RepeatExcelExportService.Export(...)` 형태를 유지(이 파일 관례).
- 결과 메시지/아이콘을 `if/else` 로 분기 — **삼항 금지 관례가 이미 적용된 좋은 예시**.
- 이 버튼은 export가 **UI/STA 스레드**에서 호출됨을 보장한다 → 차트 Canvas 렌더가 안전한 유일한 진입점(RESEARCH §Pattern 3 스레딩). **`OnRepeatComplete` 콜백에서 직접 export 호출 금지.**

> **동일 패턴 2호점:** `WPF_Example/UI/ControlItem/InspectionListView.xaml.cs:808-833` `Btn_batchExport_Click`. 단 이 파일은 **K&R 브레이스**(`if (...) {`) 스타일이므로 해당 파일 안에서는 그 스타일을 따를 것 — 파일별 스타일 준수.

---

### 8. `WPF_Example/DatumMeasurement.csproj` (MODIFY — config)

**Analog:** 기존 등록 블록.

**일반 .cs 등록** (csproj:258-261) — 알파벳 순 위치에 삽입:
```xml
    <Compile Include="Custom\Export\ExcelExportSmokeTest.cs" />
    <Compile Include="Custom\Export\ExcelExportService.cs" />
    <Compile Include="Custom\Export\RepeatExcelExportService.cs" />
```
**XAML 동반 .cs 등록** (csproj:405-407, 542) — ChartRenderService는 XAML 없으므로 **위 단순 형태만** 필요:
```xml
    <Compile Include="UI\Statistics\StatisticsWindow.xaml.cs">
      <DependentUpon>StatisticsWindow.xaml</DependentUpon>
    </Compile>
```
- 경로 구분자는 **백슬래시**. SDK-style이 아닌 classic csproj라 **신규 파일은 반드시 수동 등록** — 누락 시 "타입을 찾을 수 없음" 컴파일 에러가 난다.

---

## Shared Patterns

### 삼항 연산자 제거 (전 파일 적용, 최우선)

**주의:** 기존 코드베이스에 삼항이 **남아 있는 파일이 있다** — 복사 시 반드시 if-else로 펴야 한다.

**나쁜 예 (기존 코드, 복사 금지)** — `RepeatExcelExportService.cs:173-174`, `ExcelExportService.cs:58-62,85,94,97,100-102,121-122`:
```csharp
                        double successRate = a.TotalCount > 0 ? (validN * 100.0 / a.TotalCount) : 0.0;
                        double mean2 = validN > 0 ? a.Values.Sum() / validN : 0.0;
                        ws.Cell(1, 2).Value = cycle.RecipeName != null ? cycle.RecipeName : "";
```
**따라야 할 예 (신규 코드 표준)** — `RepeatExcelExportService.cs:305-312`:
```csharp
            if (shot.ShotName != null)
            {
                ws.Cell(nRow, 2).Value = shot.ShotName;
            }
            else
            {
                ws.Cell(nRow, 2).Value = "";
            }
```
그리고 `ExcelExportService.cs:266-275` 의 "비교 후 대입" 형태:
```csharp
                double dScale = dScaleWidth;
                if (dScaleHeight < dScale)
                {
                    dScale = dScaleHeight;
                }
```
> RESEARCH.md Pattern 4의 `group.Key >= 0 ? group.Key.ToString() : "미지정"` 는 **규칙 위반 예시**다 — 그대로 쓰지 말 것.

### 헝가리언 표기법

**Source:** `ExcelExportService.cs:240-290`, `RepeatExcelExportService.cs:236-250`
**Apply to:** 신규 파일 전부, 기존 파일에 추가하는 신규 지역변수/필드

| 접두 | 타입 | 실사용 예 |
|---|---|---|
| `b` | bool | `bHasBytes`, `bInvalidSize`, `bUnderMaxCycles`, `bEmptyScope`, `bOwnedByThisSeq` |
| `n` | int | `nRow`, `nColumn`, `nCycleCount`, `nBudgetMs`, `nOriginalWidth`, `nMatchedFaiCount` |
| `d` | double | `dScale`, `dPlotW`, `dUsl`, `dLsl`, `dMaxFreq` |
| `sz` | string | `szPath`, `szRecipeFilter`, `szSel`, `szMat` |
| `arr` | 배열 | `arrBytes`, `arrCaptureBytes` |
| `dic` | Dictionary | `dicCaptureCache`, `dicCache` |
| `sw` | Stopwatch | `swWaitBudget`, `swBudget` |
| `m_` | Window 인스턴스 필드 | `m_lastResult`, `m_brushBar` |
| `_` | 서비스 클래스 private 필드 | `_seq`, `_collected`, `_lock`, `_data` |

**기존 파일 안에서는 그 파일의 지배적 스타일을 따른다** — `RepeatMeasurementStats.cs`는 `mean`/`stddev`/`cpk` 같은 무접두 지역변수를 쓰므로, 그 파일에 추가하는 `cp`/`cpkUpper`도 동일 스타일 유지(파일 내 혼용 금지).

### 예외 처리 / 로깅

**Source:** `RepeatExcelExportService.cs:208-217`, `ExcelExportService.cs:296-304`, `StatisticsWindow.xaml.cs:120-123`
**Apply to:** 모든 신규 public 진입점

```csharp
            catch (Exception ex)   //260707 hbk 조회 실패해도 UI 크래시 없이 빈 상태 폴백(ReviewerWindow 패턴)
            {
                try { Logging.PrintErrLog((int)ELogType.Error, "[StatisticsWindow] DoQuery: " + ex.Message); } catch { }
            }
```
- 태그는 `[클래스명]` 접두 + 메서드/상황 설명.
- `Logging.PrintErrLog((int)ELogType.Error, ...)` — **`(int)` 캐스팅 필수**.
- 로깅 호출은 항상 bare `catch { }` 로 재감쌈.
- export/알고리즘 계층은 **throw 금지 → false 반환**.

### 브레이스 스타일 (파일별)

| 파일 | 스타일 |
|---|---|
| `Custom/Export/*.cs`, `RepeatMeasurementStats.cs`, `RepeatRunService.cs`, `BatchRunService.cs`, `StatisticsWindow.xaml.cs`, `ReviewerWindow.xaml.cs` | **Allman** (여는 중괄호 새 줄), 단일문 if도 중괄호 필수 |
| `UI/ControlItem/InspectionListView.xaml.cs` | **K&R** (`if (...) {`), 한 줄 if 허용 |

신규 파일은 전부 **Allman**.

### 주석 정책

- 기존 코드에 보이는 `//260616 hbk Phase 51 ...` 날짜 접두 규칙은 **폐기됨** — 신규 주석에 붙이지 말 것.
- 비자명한 "왜"만 최소로. 좋은 예:
```csharp
        // cycle 수에 비례해 늘리되 상한을 둔다. UI 스레드 동기 호출이므로 무한정 블로킹은 금지 (E1L-05).
        // 측정값: 0.0 도 정상 결과이므로 값이 아니라 LastHasResult 로 판별한다 (CO-23-01).
        // 캡쳐이미지 컬럼 폭을 이미지 박스 폭에 맞춘다. AdjustToContents 는 그림을 고려하지 않으므로 '그 뒤에' 불러야 한다.
```
- 신규 `public` 서비스/유틸 메서드에는 `/// <summary>` 필수. UI 이벤트 핸들러/override는 불요.

### C# 7.2 제약

- `switch` **문**만 사용 (switch expression 금지), `record`/nullable reference types 금지.
- 허용되고 실제 쓰이는 것: `out var`(`TryGetValue(key, out d)` — 단 기존 코드는 `KeyData d;` 선언 후 `out d` 형태를 더 자주 씀), `?.Invoke()`, object initializer, `$"..."` 미사용(문자열 `+` 연결이 지배적 관례).
- 문자열 결합은 `+` 사용 — 코드베이스에 보간 문자열이 거의 없다.

---

## No Analog Found

| File / 기능 | Role | Data Flow | Reason |
|---|---|---|---|
| Canvas → `RenderTargetBitmap` → PNG byte[] (오프스크린 캡처 절반) | utility | in-memory transform | 코드베이스 전체에 `RenderTargetBitmap` 사용 전례 0건. HALCON 쪽 `OverlayCaptureRenderer` 는 HALCON 픽셀 페인팅이라 기술적으로 무관(RESEARCH가 Phase 40.2 선례를 명시적으로 무효화). → **RESEARCH.md §Pattern 3 코드 블록을 원본으로 사용**하되, `Measure/Arrange/UpdateLayout` 3단계 누락 시 빈 이미지가 나오는 점과 `ActualWidth==0` 함정(§3 참조)에 주의. Plan에 **조기 1회 육안 검증 task** 를 넣을 것(A1 리스크). |
| 워크시트 상단 요약 블록(`OK/Total`, `NG/Total`, `NG FAI# 리스트`) | export | transform | 기존 export는 좌상단 메타 행(모델명/일시/횟수)만 있고 집계 요약 블록 전례 없음. 가장 가까운 것은 `ExcelExportService.cs:57-73` 메타 블록 — 셀 배치 방식만 차용. |

---

## Metadata

**Analog search scope:** `WPF_Example/Custom/Export/`, `WPF_Example/Custom/Sequence/Inspection/`, `WPF_Example/UI/Statistics/`, `WPF_Example/UI/Reviewer/`, `WPF_Example/UI/ControlItem/`, `WPF_Example/UI/ViewModel/`, `WPF_Example/DatumMeasurement.csproj`
**Files read (full or targeted):** 9
**Pattern extraction date:** 2026-08-18
