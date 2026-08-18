# Phase 72: CPK 데이터 리포트 출력 재설계 - Research

**Researched:** 2026-08-18
**Domain:** WPF/.NET Framework 4.8 Excel export (ClosedXML) + WPF Canvas 헤드리스 렌더 + Cpk/Cp 통계
**Confidence:** HIGH (모든 핵심 주장이 코드베이스 직접 확인 또는 참고파일 openpyxl 직접 분석으로 검증됨)

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

- **D-01. 그래프 — 이미지 삽입 방식 채택.** ClosedXML 0.105.0에는 네이티브 차트 API 없음(`AddChart`/`IXLChart` 전무, `AddPicture`만 가능). `WPF_Example/UI/Statistics/StatisticsWindow.xaml.cs`의 기존 `RenderHistogram()`/`RenderTrend()`(WPF Canvas 렌더, USL/LSL/평균 기준선 포함, BIN_COUNT=20)를 재활용. 방식: Canvas를 `RenderTargetBitmap`으로 캡처 → PNG 바이트 인코딩 → `IXLWorksheet.AddPicture()`로 삽입. 참고파일도 실제로는 EMF/PNG 정지 이미지 붙여넣기(zip 검증: `xl/charts/` 0개). 헤드리스(비HW) 환경에서 동작해야 함 — Phase 40.2의 "헤드리스 HALCON 버퍼윈도우 dump" 선례 참고.
- **D-02. 엑셀 수식 vs 고정값 — 고정값 채택.** Max/Min/Mean/StdDev/Cp/Cpk/USL/LSL 전부 C#에서 계산 후 `.Value`로 기록. `FormulaA1` 미사용. 기존 export 코드 전체가 `.Value`만 쓰는 일관된 관행.
- **D-03. 100회+ 반복 검증 데이터 — RepeatRunService 폴더 반복 재사용.** `WPF_Example/Custom/Sequence/Inspection/RepeatRunService.cs`의 `StartFromImages(seq, imagePaths)`(quick-260615-dx7)로 확보. 신규 인프라 불필요.
- **D-04. 시트/컬럼 구조 — 참고파일과 100% 동일, "Cavity"는 자재번호 단위로 재해석.** "1Cav/2Cav"는 물리 캐비티가 아니라 자재번호 구분. 시트명: `1Cav 세부치수_Cpk` → `{자재번호} 세부치수_Cpk`, `RAW DATA(1)/(2)` → `RAW DATA({자재번호})`. **고정 2개가 아니라 실제 배치 결과의 distinct 자재번호 개수만큼 동적 생성 (하드코딩 금지).** 컬럼 구조(좌측 SPC~LSL, 우측 Max/Min/Mean/StdDev/Cp/UCPK/LCPK/Cpk, 판정 OK/NG/Cpk경고, 상단 OK/Total·NG/Total 요약)는 참고파일 그대로 이식.

### Claude's Discretion

- RAW DATA 시트와 Cpk 상세 시트를 하나의 export 흐름(신규 서비스 또는 `RepeatExcelExportService` 확장) 중 어느 쪽으로 구현할지의 정확한 클래스/메서드 배치.
- PNG 이미지 삽입 시 셀 앵커 위치, 이미지 크기·해상도의 구체적 값.
- 자재번호 축 그룹핑을 어느 시점(배치 결과 집계 시 vs export 시)에 수행할지.
- 판정 등급(OK/NG/Cpk경고) 임계값은 참고파일 수식 그대로: `min<LSL 또는 max>USL → NG`, `Cpk<1.33 → Cpk(경고)`, `그 외 → OK`.

### Deferred Ideas (OUT OF SCOPE)

- ClosedXML을 차트 지원 라이브러리로 교체 — 리스크 크고 다른 export 경로 전체에 영향, 범위 밖. 이미지 삽입 방식(D-01)으로 대체.
- 엑셀 라이브 수식(FormulaA1) 방식 — 이번엔 고정값(D-02).
- `검사성적서` 시트가 정확히 "자재번호 통합 합계"인지 여부 — **본 연구에서 확정함, 아래 Summary/Open Questions 참고. CONTEXT.md의 가정("합계로 추정")은 틀렸음이 openpyxl 직접 분석으로 확인됨.**

</user_constraints>

## Summary

이 Phase는 순수 export-표현 계층 확장이며 판정 로직(P/F/B)이나 `RepeatMeasurementStats`의 기존 계산은 건드리지 않는다. 핵심 작업은 4가지: (1) `RepeatMeasurementStats`/`MeasurementStat`에 Cp/UCPK/LCPK를 추가(Cpk와 동일 가드 패턴 재사용) + 원시값 리스트(Series)를 노출, (2) `List<CycleResultDto>`를 `IndexNumber`(자재번호) 기준으로 그룹핑해 시트를 동적 생성하는 신규 export 메서드, (3) Shot/FAI/측정값을 세로형에서 "1행=FAI, 열=반복회차" 가로형으로 pivot하는 RAW DATA 시트 빌더, (4) `StatisticsWindow`의 Canvas 렌더 로직을 Window에서 분리해 헤드리스로 호출 가능한 형태로 리팩터링한 뒤 `RenderTargetBitmap`→PNG→`ws.AddPicture()`로 삽입.

**참고파일을 openpyxl로 직접 분석한 결과, CONTEXT.md가 "확인 필요"로 남긴 `검사성적서` 질문에 결정적 증거가 나왔다: `검사성적서`는 `1Cav`/`2Cav` 시트의 합계가 아니다.** 모든 상세 시트(`검사성적서`, `1Cav_Cpk`, `2Cav_Cpk`)는 라이브 `VLOOKUP` 수식으로 `RAW DATA(1)`/`RAW DATA(2)`에서 원시값을 끌어오는 구조인데, `1Cav_Cpk`는 `RAW DATA(1)`의 32개 샘플 열(AJ:BO) 전체를 쓰는 반면 `검사성적서`는 단 4개 열(Y:AB — `RAW DATA(1)`에서 2개 + `RAW DATA(2)`에서 2개)만 풀링해서 별도로 Mean/StdDev/Cpk를 계산한다. 그 결과 FAI 항목 리스트는 동일(163개)하지만 실제 계산값과 OK/NG 카운트가 서로 다르다(검사성적서 144 OK/162, 1Cav 135 OK/163). 즉 `검사성적서`는 "전체 자재 통합 합계"가 아니라 **원본 참고파일 특유의 소량-샘플 별도 발췌 뷰**이며, 이 표본 선택 규칙(자재당 2개씩)은 일반화 가능한 공식이 아니라 참고파일 작성 시점의 수기 발췌로 보인다. 이 부분은 그대로 이식할 가치가 낮다 — Open Questions에 대안을 제시한다.

또한 D-01이 참고 선례로 지목한 "Phase 40.2 헤드리스 HALCON 버퍼윈도우 dump" 패턴은 **더 이상 존재하지 않는다.** `OverlayCaptureRenderer.cs` 상단 주석이 명시하듯, 초기에는 `open_window`+`disp_obj`+`DumpWindowImage` 방식이었으나 FAI당 300-660ms의 성능 문제로 완전히 폐기되고(2026-08-10, round7) 채널 분해 기반 픽셀 페인팅으로 교체되었다. 따라서 이 Phase가 실제로 참고할 수 있는 "헤드리스 렌더" 선례는 없다 — WPF `Canvas`/`RenderTargetBitmap` 오프스크린 렌더는 HALCON 윈도우 문제와는 기술적으로 무관한 표준 WPF 기법이므로 별도로 검증했다(아래 Architecture Patterns 참고). 다행히 이 Phase는 그 성능 문제와 규모가 다르다(FAI당이 아니라 export 1회당 자재번호 수 × 2장).

**Primary recommendation:** `RepeatMeasurementStats`를 확장(Cp/UCPK/LCPK + Series 노출)하고, `StatisticsWindow`의 Canvas 드로잉 로직을 `ChartRenderService` 같은 독립 헬퍼로 추출해 bare `Canvas`(Window 불필요) 위에서 호출하도록 리팩터링한 뒤, 신규 `CpkReportExportService`(또는 `RepeatExcelExportService` 확장)에서 자재번호별로 그룹핑해 시트를 동적 생성한다. `검사성적서`는 "합계"가 아니라 "전체 자재 풀링 Cpk"로 재정의해 구현할 것을 권장(Open Questions 참고).

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Cp/UCPK/LCPK 계산 | Backend(Sequence) — `RepeatMeasurementStats` | — | 기존 Cpk 계산과 동일 계층, 판정 로직과 같은 소스에 있어야 일관성 유지 |
| 자재번호별 그룹핑 | Export 계층(`RepeatExcelExportService`/신규 서비스) | — | `CycleResultDto.IndexNumber`는 이미 Backend DTO에 존재 — export 시점에 그룹핑이 가장 단순(재사용 데이터 변경 없음) |
| RAW DATA 가로형 pivot | Export 계층 | — | 기존 세로형 "회차별 상세"와 동일 데이터 소스(`List<CycleResultDto>`)에서 파생, 별도 저장 불필요 |
| 차트(히스토그램/추이) 렌더 | UI 계층(WPF Canvas, `StatisticsWindow` 소속 로직을 추출) | Export 계층(캡처 트리거) | 렌더 자체는 WPF 오브젝트라 UI/STA 스레드 소속 필수. Export가 호출자일 뿐 소유자는 아님 |
| PNG→xlsx 삽입 | Export 계층(ClosedXML) | — | 기존 `ExcelExportService.TryInsertCaptureImage`와 동일 계층/패턴 |

## Standard Stack

### Core (기존 재사용 — 신규 의존성 없음)

| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| ClosedXML | 0.105.0 [VERIFIED: packages/ClosedXML.0.105.0] | xlsx 생성/시트/셀/이미지 삽입 | 이미 `ExcelExportService`/`RepeatExcelExportService`가 사용 중, 프로젝트 표준 |
| System.Windows.Media.Imaging (WPF 내장) | .NET FW 4.8 내장 | `RenderTargetBitmap` + `PngBitmapEncoder`로 Canvas→PNG | 외부 패키지 불필요, WPF 표준 API |

**Version verification:** `packages/ClosedXML.0.105.0/lib/netstandard2.1/ClosedXML.xml` 존재 확인 — 프로젝트가 이미 이 버전에 고정(packages.config). 별도 업그레이드 불필요/권장하지 않음(D-01이 명시적으로 라이브러리 교체를 범위 밖으로 결정).

### Alternatives Considered

| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| ClosedXML AddPicture(정지 이미지) | ClosedXML.Report / EPPlus 네이티브 차트 | D-01이 이미 기각(라이브러리 교체 리스크) |
| WPF Canvas RenderTargetBitmap | HALCON HWindow dump (Phase 40.2 초기 방식) | Phase 40.2 자체가 이 방식을 폐기함(느림) — 애초에 이 Phase 대상(WPF UI 그래프)이 아니라 HALCON 이미지 캡쳐용이었으므로 무관 |

**Installation:** 불필요 — 신규 NuGet 패키지 없음. `AddPicture(Stream, XLPictureFormat.Png)` [VERIFIED via WebSearch: ClosedXML GitHub — `XLPictureFormat` enum에 `Png` 포함, 코어 그래픽 엔진이 png/jpg/emf 지원] + 기존 코드가 이미 `XLPictureFormat.Jpeg`로 검증됨(`ExcelExportService.cs:253`).

## Architecture Patterns

### System Architecture Diagram

```
[List<CycleResultDto>]  (RepeatRunService.StartFromImages 결과 또는 BatchRunService 결과 또는 실운영 CSV/JSON 이력)
        │
        ├─▶ [Group by IndexNumber(자재번호)] ── (신규, D-04)
        │        │
        │        ├─▶ 자재번호 A 그룹 ─┬─▶ RepeatMeasurementStats.AddSample × N
        │        │                    │        │
        │        │                    │        ▼
        │        │                    │   ComputeAll() → Dictionary<key, MeasurementStat>
        │        │                    │        │ (Cp/UCPK/LCPK 확장 필요)
        │        │                    │        ▼
        │        │                    ├─▶ "{자재A} 세부치수_Cpk" 시트 (N/Max/Min/Mean/StdDev/Cp/UCPK/LCPK/Cpk/판정)
        │        │                    │
        │        │                    └─▶ Pivot(Shot/FAI × 회차) → "RAW DATA({자재A})" 시트 (가로형)
        │        │
        │        └─▶ 자재번호 B 그룹 ─▶ (동일 처리, 동적 반복)
        │
        └─▶ [전체 풀링] ──▶ "검사성적서" 시트 (자재 무관 전체 통합 뷰 — 권장안, Open Questions 참고)

[선택된 MeasurementStat.Key] ──▶ [Series 원시값 리스트] (신규 노출 필요)
        │
        ▼
[ChartRenderService (신규 추출)] ── bare Canvas 생성 → Measure/Arrange/UpdateLayout → RenderHistogram/RenderTrend 로직 실행
        │
        ▼
[RenderTargetBitmap.Render(canvas)] → [PngBitmapEncoder] → byte[]
        │
        ▼
[ws.AddPicture(stream, XLPictureFormat.Png).WithSize(...).MoveTo(cell)]
```

### Recommended Project Structure

```
WPF_Example/
├── Custom/Sequence/Inspection/
│   └── RepeatMeasurementStats.cs      # 확장: Cp/UCPK/LCPK 필드 추가 + Series(원시값) 노출 메서드
├── Custom/Export/
│   ├── RepeatExcelExportService.cs    # 확장 지점(Claude's Discretion) — 또는
│   └── CpkReportExportService.cs      # 신규: 자재번호별 시트 동적 생성 + RAW DATA pivot + 차트 삽입 오케스트레이션
├── UI/Statistics/
│   ├── StatisticsWindow.xaml.cs       # RenderHistogram/RenderTrend 로직을 아래로 위임하도록 리팩터링
│   └── ChartRenderService.cs          # 신규(권장): Canvas 인자를 받는 순수 드로잉 헬퍼 — Window 비의존
```

### Pattern 1: RepeatMeasurementStats 확장 (Cp/UCPK/LCPK)

**What:** 기존 `MeasurementStat`/`ComputeAll()`에 Cp, UCPK, LCPK 필드 추가. Cpk는 이미 `cpk = Math.Min(cpkUpper, cpkLower)`로 계산되지만 `cpkUpper`/`cpkLower`는 지역변수라 노출되지 않는다 — 이 둘을 각각 `UCPK`/`LCPK`로 그대로 노출하면 되고, 추가 계산은 Cp 하나뿐이다.

**When to use:** `RepeatMeasurementStats.ComputeAll()` 내부, 기존 Cpk 계산 바로 옆.

**Example (참고파일 수식 확인됨 — `.planning` 외부 참고파일 openpyxl 직접 분석):**
```csharp
// Source: WPF_Example/Custom/Sequence/Inspection/RepeatMeasurementStats.cs (기존 패턴 확장)
// 참고파일 수식(1Cav_Cpk!S14): =(H14+ABS(I14))/(6*R14)  →  Cp = (TolPlus + |TolMinus|) / (6 * StdDev)
if (stddev == 0)
{
    cp = double.PositiveInfinity;   // 기존 Cpk 가드와 동일 패턴 재사용
    cpk = double.PositiveInfinity;
}
else
{
    cp = (d.LastTolPlus + Math.Abs(d.LastTolMinus)) / (6 * stddev);
    double cpkUpper = (usl - mean) / (3 * stddev);   // = UCPK
    double cpkLower = (mean - lsl) / (3 * stddev);   // = LCPK
    cpk = Math.Min(cpkUpper, cpkLower);
}
```
`MeasurementStat`에 `public double Cp`, `public double UCpk`, `public double LCpk` 3개 필드 추가.

### Pattern 2: 원시값 Series 노출 (그래프/RAW DATA 공통 필요)

**What:** 현재 `RepeatMeasurementStats._data[key].Values`(List&lt;double&gt;)는 private이고 `ComputeAll()`이 집계값만 반환한다. 그래프(히스토그램/추이)와 RAW DATA 가로형 매트릭스 둘 다 원시값 리스트가 필요하므로, `ComputeAll()`과 별개로 `public Dictionary<string, List<double>> GetSeries()`를 추가하거나 `MeasurementStat`에 `List<double> Values`를 포함시킨다.

**When to use:** RAW DATA pivot과 차트 렌더 모두 이 지점에서 공유.

**Why not reuse StatisticsWindow's CSV 경로:** `StatisticsWindow`는 `MeasurementHistoryCsvLoader.Query()`(CSV 파일 재조회, `StatisticsQueryResult.Series`)로 원시값을 얻는데, 이는 `CycleResultSerializer.SaveAsync()`가 매 cycle마다 `MeasurementHistoryCsvWriter.Append(dto)`를 호출해 CSV에 쌓아야만 존재한다. `List<CycleResultDto>`가 이미 메모리에 있는 상황(반복도/일괄 export)에서 굳이 CSV 왕복을 거칠 필요 없다 — `RepeatMeasurementStats`가 이미 `AddSample()`로 값을 모으고 있으므로 거기서 바로 노출하는 것이 가장 단순하고 정확하다(날짜/레시피 필터 불일치로 엉뚱한 값이 섞일 위험도 없음).

### Pattern 3: 헤드리스 Canvas 렌더 (Window 불필요)

**What:** `StatisticsWindow.RenderHistogram()`/`RenderTrend()`는 현재 `private` 인스턴스 메서드이며 XAML 명명 요소(`canvas_Histogram`/`canvas_Trend`, `Window` 소속)를 직접 참조한다. 이를 그대로 재사용하려면 `StatisticsWindow` 전체(DataGrid/DatePicker 포함)를 인스턴스화해야 하는데, 이는 불필요하게 무겁고 `m_lastResult`(CSV 조회 결과) 의존성까지 끌고 온다.

**Recommended approach:** 드로잉 로직을 `Canvas` 인자를 받는 정적/독립 헬퍼(`ChartRenderService.RenderHistogram(Canvas canvas, double width, double height, List<double> values, double usl, double lsl)` 형태)로 추출한다. `StatisticsWindow`의 기존 메서드는 이 헬퍼를 호출하는 얇은 래퍼로 남긴다(D-08 계약 — 기존 대화형 통계창 동작 무변경).

**Off-screen 캡처 절차 (bare Canvas, Window 불필요 — 표준 WPF 기법):**
```csharp
// Window를 만들지 않고 bare Canvas만 생성 — HWND/PresentationSource 불필요.
var canvas = new Canvas { Width = 480, Height = 320 };
ChartRenderService.RenderHistogram(canvas, 480, 320, values, usl, lsl);  // 기존 RenderHistogram 로직 이식

canvas.Measure(new Size(480, 320));
canvas.Arrange(new Rect(0, 0, 480, 320));
canvas.UpdateLayout();

var rtb = new RenderTargetBitmap(480, 320, 96, 96, PixelFormats.Pbgra32);
rtb.Render(canvas);

var encoder = new PngBitmapEncoder();
encoder.Frames.Add(BitmapFrame.Create(rtb));
using (var ms = new MemoryStream())
{
    encoder.Save(ms);
    ws.AddPicture(ms, XLPictureFormat.Png).WithPlacement(XLPicturePlacement.Move).MoveTo(ws.Cell(row, col));
}
```
[ASSUMED — WPF 표준 오프스크린 렌더 패턴, 이 프로젝트 코드베이스에서 직접 사용된 전례는 없음. `Window.Measure/Arrange`가 아니라 `Canvas`(비-`Window` `FrameworkElement`)를 대상으로 하므로 HWND 의존 이슈를 원천 회피한다는 것이 핵심 근거]

**스레딩:** 이 앱은 서비스/콘솔 모드가 없는 순수 WPF 데스크톱 앱이며 MainWindow가 항상 STA UI 스레드에서 실행 중이다. `RepeatExcelExportService.Export()`/`ExportBatch()`가 호출되는 두 실제 진입점(`ReviewerWindow.Button_RepeatExport_Click`, `InspectionListView.Btn_batchExport_Click`)은 **이미 버튼 클릭 핸들러 = UI/STA 스레드**이므로 추가 `Dispatcher` 마샬링이 필요 없다 [VERIFIED: 코드 직접 확인]. 단, 만약 export를 `RepeatRunService.OnRepeatComplete`(백그라운드 시퀀스 스레드에서 발화 가능) 콜백에서 직접 트리거하도록 설계 변경한다면 기존 패턴(`ReviewerWindow.xaml.cs:398`처럼 `Dispatcher.Invoke`로 감싸기)을 반드시 따라야 한다.

**Phase 40.2 "헤드리스 HALCON" 선례는 이 문제에 적용 불가:** `OverlayCaptureRenderer.cs` 상단 주석에 명시된 대로, 원래 있던 `open_window`+`DumpWindowImage` 방식은 FAI당 300-660ms의 성능 문제로 **완전히 폐기**되고(2026-08-10) 채널 분해 픽셀 페인팅으로 교체되었다. 즉 이 선례는 "재사용할 헤드리스 렌더 기법"이 아니라 "창 기반 캡처를 피하라는 반면교사"에 가깝다. 다행히 WPF Canvas 캡처는 HALCON 윈도우/HWND 문제와 무관한 별개 기술이므로 이 폐기 이력이 직접 재발할 위험은 낮다(export 1회당 자재번호 수 × 2장 수준의 빈도, FAI당 수백 회 호출과는 규모가 다름).

### Pattern 4: 자재번호별 동적 시트 그룹핑

**What:** `List<CycleResultDto>`를 `IndexNumber`로 `GroupBy` 후 distinct 개수만큼 시트 생성.

```csharp
// Source: 신규 패턴 — CycleResultDto.IndexNumber 필드는 기존(Phase 48 PROTO-01) 확인됨
var groups = cycles.GroupBy(c => c.IndexNumber).OrderBy(g => g.Key);
foreach (var group in groups)
{
    string materialLabel = group.Key >= 0 ? group.Key.ToString() : "미지정";
    var ws = wb.Worksheets.Add(materialLabel + " 세부치수_Cpk");
    var wsRaw = wb.Worksheets.Add("RAW DATA(" + materialLabel + ")");
    // group.ToList()로 RepeatMeasurementStats.AddSample() 반복 → 해당 자재번호 전용 통계
}
```

### Pattern 5: RAW DATA 가로형 Pivot

**What:** 기존 "회차별 상세"(Sheet, `AppendDetailSheet`)는 세로형(1행=1측정×1회차)이다. RAW DATA는 가로형(1행=FAI 측정항목, 열=회차#1..#N)이 필요 — 참고파일 실측 구조와 일치(`RAW DATA(1)` 시트: B열=FAI#, E/F/G=설계값/공차, H열부터 `#1`,`#2`...`#32`가 개별 반복회차 컬럼).

```csharp
// cycles 는 이미 자재번호로 그룹핑된 하위 리스트(Pattern 4)
// key = ShotName/FAIName/MeasurementName — RepeatMeasurementStats 와 동일 키 규칙 재사용
var pivotRows = new Dictionary<string, List<double>>();  // key → [cycle1값, cycle2값, ...]
int cycleIndex = 0;
foreach (var cycle in cycles)
{
    foreach (var shot in cycle.Shots)
        foreach (var fai in shot.FAIs)
            foreach (var m in fai.Measurements)
            {
                string key = shot.ShotName + "/" + fai.FAIName + "/" + m.MeasurementName;
                if (!pivotRows.TryGetValue(key, out var list)) { list = new List<double>(); pivotRows[key] = list; }
                while (list.Count < cycleIndex) list.Add(double.NaN);  // 회차 누락분 자리 채움(측정 skip 대응)
                list.Add(m.LastHasResult ? m.LastMeasuredValue : double.NaN);
            }
    cycleIndex++;
}
// 이후 pivotRows 를 시트에 1행=1키, 열=cycleIndex 순서로 기록
```

**기존 "회차별 상세"와의 관계:** 재사용 가능한 것은 **데이터 소스**(동일 `List<CycleResultDto>`)와 **키 규칙**(Shot/FAI/측정명, `RepeatMeasurementStats` 키와 동일 문자열 조합)뿐이다. 순회 로직/셀 기록 로직 자체는 세로형(`AppendDetailSheet`)과 가로형(RAW DATA)이 근본적으로 다른 pivot이라 로직 공유는 어렵다 — 새로 작성해야 한다.

### Anti-Patterns to Avoid

- **`검사성적서`를 `1Cav_Cpk`+`2Cav_Cpk`의 산술 합계로 구현하는 것** — 참고파일 실측 결과 두 값이 다르다(다른 Mean/StdDev/OK-NG 카운트). 단순 합계 공식은 참고파일과 불일치하는 잘못된 재현이다.
- **StatisticsWindow(Window)를 통째로 headless 인스턴스화하는 것** — DataGrid/DatePicker 등 무관한 컨트롤까지 초기화 비용 발생, CSV 재조회 경로와 얽혀 데이터 정합성 리스크 증가. Canvas만 분리해서 쓸 것.
- **RepeatRunService/BatchRunService가 만드는 CycleResultDto의 IndexNumber가 항상 -1이라는 것을 잊고 UAT 계획을 짜는 것** — 아래 Common Pitfalls 참고.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| xlsx 이미지 삽입 | 수동 zip/OOXML drawing XML 조작 | `ws.AddPicture(stream, XLPictureFormat.Png)` | 이미 `ExcelExportService.TryInsertCaptureImage`가 검증된 패턴(JPEG로) 보유, PNG도 동일 API |
| PNG 인코딩 | 수동 픽셀 버퍼 압축 | `PngBitmapEncoder`(WPF 내장) | .NET Framework 표준, 외부 의존성 불필요 |
| Cpk 계산 재작성 | 새 통계 클래스 | `RepeatMeasurementStats` 확장 | 이미 검증된 계산·가드(stddev=0 → PositiveInfinity) 존재, 중복 구현 시 회귀 위험 |

**Key insight:** 이 Phase의 모든 "새 문제"는 사실 기존 코드의 확장/재조합으로 풀린다 — 완전히 새로운 알고리즘이 필요한 지점이 없다.

## Common Pitfalls

### Pitfall 1: RepeatRunService/BatchRunService는 IndexNumber를 항상 -1로 만든다

**What goes wrong:** D-03이 제안하는 UAT 데이터 확보 방법(`RepeatRunService.StartFromImages`)으로 100회 이상 반복 실행해도, D-04가 요구하는 "여러 자재번호로 나뉜 동적 시트" 시나리오를 검증할 수 없다.

**Why it happens:** `RepeatRunService.HandleFinish()`와 `BatchRunService`의 해당 지점 모두 `CycleResultSerializer.BuildDto(recipeManager, resultType, DateTime.Now, recipeName, seqName)`를 호출하는데 **`nIndexNumber` 인자를 넘기지 않는다** [VERIFIED: 코드 직접 확인, `RepeatRunService.cs:235`, `BatchRunService.cs:159`]. `BuildDto`의 해당 매개변수는 기본값 `-1`(미수신 sentinel)이므로, TCP `$TEST` 프로토콜을 거치지 않는 이 두 서비스가 만드는 모든 `CycleResultDto`는 예외 없이 `IndexNumber = -1`이다.

**How to avoid:** UAT에서 여러 자재번호 그룹을 재현하려면 (a) `StartFromImages`를 자재군별로 별도 실행(예: 자재A 폴더로 1회, 자재B 폴더로 1회) 후 두 `List<CycleResultDto>`를 합치기 전에 `foreach(var dto in listA) dto.IndexNumber = 1;`처럼 **테스트 하네스 코드에서 직접 patch**(운영 코드 변경 없음, `IndexNumber`는 public setter 보유) — 가장 낮은 리스크. 또는 (b) 실제 TCP `$TEST:site,Type,자재번호,...` 명령으로 여러 자재번호를 순차 전송(Test 폴더의 `mock_vision_client.py` 재사용 가능)해 실제 프로토콜 경로로 데이터를 쌓는 방법. 둘 중 어느 쪽이든 export 로직 자체는 `IndexNumber` 값에만 의존하므로 프로덕션 코드는 무영향.

**Warning signs:** UAT에서 자재번호 시트가 항상 1개("미지정")만 생성된다면 이 문제다 — export 로직 버그가 아니라 테스트 데이터 생성 방식의 한계다.

### Pitfall 2: `검사성적서`를 참고파일처럼 문자 그대로 재현하려 하면 안 된다

**What goes wrong:** 참고파일의 `검사성적서`는 `RAW DATA(1)`의 처음 2개 샘플 열 + `RAW DATA(2)`의 처음 2개 샘플 열, 총 4개 샘플만 풀링한 별도 계산이다 [VERIFIED: openpyxl로 수식 직접 확인 — `검사성적서!Y14` = `VLOOKUP($C14,'RAW DATA(1)'!...,7,0)`, `Z14`=col 8, `AA14`='RAW DATA(2)' col 7, `AB14`=col 8 — 딱 4열]. 이는 "샘플 4개짜리 최초 물량 인수 검사"라는 참고파일 작성 당시의 수기 관행으로 보이며, 자동화 export 시스템이 일반화할 수 있는 규칙이 아니다(왜 2개씩인지, 어느 2개인지 근거 없음).

**How to avoid:** 문자 그대로 "각 자재번호에서 앞 2개 샘플만" 재현하지 말 것. 대신 "전체 자재번호를 통합 풀링한 Mean/StdDev/Cpk"(즉 자재 구분 없이 `List<CycleResultDto>` 전체로 `RepeatMeasurementStats` 1회 계산)로 재정의해 구현할 것을 권장 — 참고파일의 "다자재 통합 뷰"라는 의도는 보존하면서 임의성을 제거한다. 이 재정의는 사용자 확인이 필요하다(Open Questions 참고).

### Pitfall 3: Cp/UCPK/LCPK의 0-나누기(StdDev=0) 처리를 Cpk와 다르게 만들지 말 것

**What goes wrong:** 새 Cp 계산을 별도 헬퍼로 만들면서 StdDev=0 가드를 빠뜨리면 `double.NaN`/`Infinity` 처리가 기존 Cpk와 어긋나 엑셀에 `#DIV/0!` 대신 `NaN` 텍스트가 찍히는 등 표시 불일치가 생긴다.

**How to avoid:** 기존 `RepeatMeasurementStats.ComputeAll()`의 `if (stddev == 0) { cpk = double.PositiveInfinity; }` 가드를 Cp에도 동일 적용(Pattern 1 코드 예시 참고). `StatisticsWindow.CpkToText()`의 `PositiveInfinity → "∞"` 변환 관례도 export 텍스트 표시에 재사용 검토.

### Pitfall 4: 참고파일 Judgment 컬럼의 "Cpk 경고" 분기가 시트마다 다르다

**What goes wrong:** 참고파일 `검사성적서!W14` 수식은 `IF(V14<1.33,"O K","O K")`로 **참/거짓 두 분기가 동일**해서 사실상 "Cpk 경고" 라벨이 절대 나오지 않는 버그가 있다(반면 `1Cav_Cpk!X14`는 `IF(V14<1.33,"Cpk","O K")`로 정상 3분기). 이를 그대로 베끼면 자동화 시스템에 의도치 않은 버그가 이식된다.

**How to avoid:** CONTEXT.md의 Claude's Discretion에 명시된 3단계 판정(`NG` > `Cpk<1.33 → Cpk경고` > `OK`)을 **모든 시트에 일관되게** 적용할 것 — 참고파일의 시트별 불일치를 재현하지 말 것.

## Code Examples

### 기존 검증된 AddPicture 패턴 (JPEG, 이 코드베이스에서 그대로 동작 중)
```csharp
// Source: WPF_Example/Custom/Export/ExcelExportService.cs:240-305 (TryInsertCaptureImage)
IXLPicture pic;
using (var ms = new MemoryStream(arrBytes))
{
    pic = ws.AddPicture(ms, XLPictureFormat.Jpeg);   // PNG 대체 시 XLPictureFormat.Png
}
int nOriginalWidth = pic.OriginalWidth;
int nOriginalHeight = pic.OriginalHeight;
// ... 종횡비 유지 스케일 계산 ...
pic.WithPlacement(XLPicturePlacement.Move);
pic.WithSize(nTargetWidth, nTargetHeight);
pic.MoveTo(ws.Cell(nRow, nColumn));
```

### 기존 검증된 IndexNumber 메타 행 패턴
```csharp
// Source: WPF_Example/Custom/Export/ExcelExportService.cs:64-73
ws.Cell(4, 1).Value = "자재번호";
if (cycle.IndexNumber >= 0)
{
    ws.Cell(4, 2).Value = cycle.IndexNumber;
}
else
{
    ws.Cell(4, 2).Value = "-";
}
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|---------------|--------|
| ChartDirector(유료 차트 라이브러리) | WPF Canvas 직접 렌더(`RenderHistogram`/`RenderTrend`) | 2026-07-07 (quick-260707-fdx) | 워터마크 제거, 하지만 export 재사용을 염두에 둔 설계가 아니었음(Window 결합) — 이번 Phase가 최초로 export 재사용 필요 |
| HALCON HWindow dump(캡처 렌더) | 채널 분해 픽셀 페인팅(`OverlayCaptureRenderer`) | 2026-08-10 (round7) | 캡처 이미지 성능 4-6배 개선. **Phase 72의 "헤드리스 렌더" 참고 대상이 아님**(대상 기술 다름) |
| CPK/StdDev/Range 엑셀 컬럼 | 측정값/Spec/편차로 대체(단순화) | 2026-06-16 (Phase 51 UAT) | 이번 Phase가 정확히 이 결정을 되돌리는 성격(D-02 참고) — 회귀 아님, 사용자 요청 |

**Deprecated/outdated:** Phase 40.2 CONTEXT.md가 언급한 "헤드리스 HALCON 버퍼윈도우 dump" 자체가 이미 폐기된 코드 경로 — 참고 자료로 사용하지 말 것.

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | bare `Canvas`(Window 미소속)를 `Measure/Arrange/UpdateLayout` 후 `RenderTargetBitmap.Render()`로 캡처하는 것이 이 프로젝트 환경(.NET FW 4.8 WPF)에서 그대로 동작한다 | Architecture Patterns > Pattern 3 | 만약 `Canvas` 단독으로 폰트 렌더링(TextBlock 등)이 `Window`의 `TextOptions`/`DPI` 컨텍스트 없이 어긋나면, 렌더 결과가 흐리거나 텍스트 크기가 예상과 다를 수 있음 — 실제 빌드+수동 캡처 1회 테스트로 조기 검증 권장 |
| A2 | `검사성적서`를 "전체 자재 통합 풀링" 재정의로 구현하는 것이 사용자 의도에 부합한다 | Common Pitfalls #2, Open Questions | 사용자가 실제로는 참고파일의 "자재당 2개 발췌" 규칙을 원했다면 재작업 필요 — 반드시 discuss/plan 단계에서 확인 필요 |

**참고:** D-01/D-02/D-03/D-04 자체는 이미 CONTEXT.md에서 사용자 확정 결정이므로 여기 Assumptions Log에 포함하지 않음(재검토 대상 아님). 위 A1/A2는 본 연구가 CONTEXT.md 이후 새로 발견/도출한 보조 주장만 포함.

## Open Questions

1. **`검사성적서` 시트를 어떻게 재정의할 것인가?**
   - What we know: 참고파일에서는 자재당 2개 샘플만 풀링한 별도 계산(재현 가치 낮음, Pitfall 2 참고). FAI 항목 리스트와 컬럼 레이아웃은 `1Cav_Cpk`와 동일.
   - What's unclear: 사용자가 "전체 자재 통합 뷰"를 원하는지, 아니면 이 시트를 아예 생략하고 자재별 시트만 남길지, 혹은 특정 자재(예: 가장 최근/가장 많이 검사된 자재)를 대표로 쓸지.
   - Recommendation: discuss-phase 또는 plan 단계에서 "전체 자재번호 통합 풀링(모든 cycle 합산)"으로 확정 제안 — 참고파일이 손상되지 않은 원본 의도(다자재 통합 최종 성적서)를 보존하면서 임의성을 제거하는 가장 단순한 해석.

2. **RAW DATA 시트의 "회차 누락"(측정 skip) 표시 규칙**
   - What we know: 참고파일 RAW DATA는 매 열이 실제 측정값(숫자)만 있고 빈칸/에러 표시 규칙이 명시적이지 않음. 이 코드베이스는 `LastHasResult=false`(DATUM_FAIL/NO_IMAGE 등)를 명확히 구분한다(`ExcelExportService.BuildJudgementText` 패턴).
   - What's unclear: RAW DATA 가로형 매트릭스에서 skip된 회차를 빈 셀로 둘지, "-"로 표시할지, 텍스트 사유(DETECT FAIL 등)를 넣을지.
   - Recommendation: 기존 세로형 상세 시트의 `"-"` 관례(측정값 없음 표시)를 그대로 재사용 — 신규 규칙 발명 불필요.

3. **1개 워크북 내 시트 수 상한 이슈**
   - What we know: 자재번호 수 × 2(Cpk 시트 + RAW DATA 시트) + 안내/검사성적서 2개 = 예상 시트 수. UAT에서 자재 2~3종이면 6~8개 시트, 문제 없음.
   - What's unclear: 실운영에서 자재번호 종류가 매우 많아지면(예: 수십 종) 시트 수가 폭증할 위험 — CONTEXT.md는 이를 "하드코딩 금지, 동적 생성"으로만 명시했지 상한을 두지 않음.
   - Recommendation: 이번 Phase 범위에서는 상한 없이 구현(사용자 결정 범위 밖으로 판단), 실사용 패턴 관찰 후 필요 시 별도 Phase에서 페이지네이션/필터 고려.

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | 없음 — 이 프로젝트는 xUnit/NUnit/MSTest 미도입 [VERIFIED: CLAUDE.md 명시, 코드베이스 전수 확인 결과 `*.Tests.csproj` 부재] |
| Config file | 없음 |
| Quick run command | 해당 없음 — 수동 UAT만 |
| Full suite command | 해당 없음 |

이 프로젝트의 기존 검증 방식은 전부 **msbuild 빌드 확인 + 코드리뷰 + 수동 UAT**(다른 phase들과 동일 패턴, 예: Phase 40.2/51/71). Phase 72도 동일 패턴을 따라야 한다.

### Phase Requirements → Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| D-01 | 그래프 이미지가 xlsx에 삽입됨(육안 확인) | manual-only | 없음(엑셀 열어 육안 확인) | N/A |
| D-02 | 셀 값이 수식이 아닌 고정값(`.Value`)으로 기록됨 | manual/코드리뷰 | 엑셀 셀 클릭 시 수식 표시줄에 값만 나오는지 확인, 또는 코드리뷰로 `.FormulaA1` 미사용 확인 | N/A |
| D-03 | 100회+ 반복 데이터로 통계가 정상 계산됨 | manual UAT | `RepeatRunService.StartFromImages` 실행 후 export → N=100+ 확인 | 기존 기능(신규 아님) |
| D-04 | 자재번호별 시트가 동적으로 생성됨(1개/2개/3개+ 모두) | manual UAT | Pitfall 1의 test-harness IndexNumber patch 방법으로 자재군 2~3개 시뮬레이션 후 export | N/A |

### Sampling Rate
- **Per task commit:** msbuild Debug/x64 빌드 확인(CS 에러 0)
- **Per wave merge:** 빌드 + 신규 export 로직 코드리뷰
- **Phase gate:** 전체 UAT(자재 1개/다중 자재/그래프 삽입/Cpk 값 육안 대조) 통과 후 `/gsd-verify-work`

### Wave 0 Gaps
- 자동화 테스트 프레임워크 부재는 이 프로젝트의 기존 상태이며 이번 Phase가 새로 만들 필요 없음(다른 모든 phase와 동일 관례를 따름) — Wave 0 갭 없음.
- UAT 데이터 준비용 다중 자재번호 시뮬레이션 스크립트/절차는 필요(Pitfall 1 방법론) — plan 단계에서 별도 task로 명시 권장.

## Security Domain

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V2 Authentication | no | 이 앱은 로컬 데스크톱, export 기능에 별도 인증 없음(기존 LoginManager 범위 밖) |
| V3 Session Management | no | 해당 없음 |
| V4 Access Control | no | 해당 없음 |
| V5 Input Validation | yes | 파일 저장 경로는 기존 `SaveFileDialog`(사용자 선택, OS 검증) 사용 — 신규 외부 입력 없음. `IndexNumber`는 이미 검증된 TCP 파서(Phase 48 `ParseMaterialField`)를 거친 값 재사용 |
| V6 Cryptography | no | 해당 없음 — 암호화 대상 데이터 없음 |

### Known Threat Patterns for {stack}

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| 경로 조작(export 파일명에 자재번호 텍스트 삽입 시) | Tampering | 자재번호는 `int`(IndexNumber) 또는 파싱된 정수이므로 파일명/시트명 injection 위험 없음(문자열 조작 불가능한 타입) — 단, 시트명 32자 제한/특수문자(`\/*?[]:`) 이슈는 Excel 자체 제약이라 ClosedXML이 예외를 던짐(try/catch 기존 관례로 충분 방어) |
| DoS(과도한 시트/이미지로 export 시간 폭증) | Denial of Service | 기존 `RepeatExcelExportService`의 `BATCH_CAPTURE_WAIT_BUDGET_MAX_MS` 같은 상한 패턴을 신규 export에도 적용 검토(대량 자재번호 시나리오, Open Questions #3 참고) |

## Sources

### Primary (HIGH confidence — 코드베이스 직접 확인)
- `WPF_Example/Custom/Export/ExcelExportService.cs` — `AddPicture`/`TryInsertCaptureImage` 검증된 패턴
- `WPF_Example/Custom/Export/RepeatExcelExportService.cs` — 기존 시트 생성/헤더 관례
- `WPF_Example/Custom/Sequence/Inspection/RepeatMeasurementStats.cs` — Cpk 계산·가드 패턴
- `WPF_Example/UI/ViewModel/CycleResultDto.cs` — `IndexNumber` 필드 확인
- `WPF_Example/TcpServer/VisionRequestPacket.cs` — `IndexNumber`=자재번호 확인(PROTO-01)
- `WPF_Example/UI/Statistics/StatisticsWindow.xaml.cs` + `.xaml` — `RenderHistogram`/`RenderTrend` 정확한 구조, Canvas 명명 요소 확인
- `WPF_Example/Halcon/Display/OverlayCaptureRenderer.cs` — Phase 40.2 헤드리스 방식 폐기 이력 확인
- `WPF_Example/Custom/Sequence/Inspection/RepeatRunService.cs`, `BatchRunService.cs` — IndexNumber 미전파 확인
- `WPF_Example/UI/Reviewer/ReviewerWindow.xaml.cs`, `WPF_Example/UI/ControlItem/InspectionListView.xaml.cs` — export 호출 스레딩 컨텍스트(UI/STA) 확인
- `C:\Info\Doc\2.디팜스테크\12_Data\Rapid City A8.1_Z Stopper_ Data Report_R04_260623_AOI 국산화 개발.xlsx` — openpyxl 직접 분석(수식+값 양쪽), `검사성적서`/`1Cav_Cpk`/`2Cav_Cpk`/`RAW DATA(1)/(2)` 전체 구조·수식 확인

### Secondary (MEDIUM confidence)
- WebSearch로 확인한 ClosedXML `XLPictureFormat.Png` enum 존재(GitHub 소스 기반 응답) — 코드베이스에 PNG 사용 전례는 없으나 Jpeg 전례와 동일 API 패턴이라 리스크 낮음

### Tertiary (LOW confidence)
- 없음

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — 신규 의존성 없음, 전부 기존 코드베이스 검증된 패턴 재사용
- Architecture: HIGH — 핵심 통합 지점(threading, IndexNumber 전파, Canvas 구조) 전부 코드 직접 확인
- Pitfalls: HIGH — 4개 중 3개는 코드/참고파일 직접 검증, 1개(A1, bare Canvas 캡처)는 표준 WPF 기법이나 이 프로젝트 내 전례 없어 실측 검증 권장

**Research date:** 2026-08-18
**Valid until:** 코드/참고파일 기반 정적 분석이라 만료 개념 낮음 — 단, `IndexNumber`/`RepeatMeasurementStats` 관련 코드가 이 Phase 착수 전에 다른 quick task로 변경되면 재확인 필요
