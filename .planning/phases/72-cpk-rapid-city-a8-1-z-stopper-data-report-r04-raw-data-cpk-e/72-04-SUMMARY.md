---
phase: 72-cpk-rapid-city-a8-1-z-stopper-data-report-r04-raw-data-cpk-e
plan: 04
subsystem: export-chart-capture
tags: [chart, offscreen-render, rendertargetbitmap, png, closedxml, diagnostics]
status: partial
task_status:
  - "Task 1 — 완료 (9de4402)"
  - "Task 2 — 완료 (a6e8e59)"
  - "Task 3 — deferred: 사용자 육안 검증 대기(PC 접근 필요). 72-07 UAT 시점에 함께 확인"
requires:
  - "72-02 (ChartRenderService.RenderHistogram / RenderTrend — 폭/높이 인자 버전)"
  - "72-03 (ReviewerWindow 좌측 패널 txt_materialIndex / chk_repeatAccumulate 보존)"
provides:
  - "ChartImageCapture.RenderHistogramPng(List<double>, dUsl, dLsl) → byte[] (PNG)"
  - "ChartImageCapture.RenderTrendPng(List<double>, dMean, dUsl, dLsl) → byte[] (PNG)"
  - "ChartImageCapture.TryInsertChartPicture(IXLWorksheet, nRow, nColumn, byte[]) — xlsx 그림 삽입"
  - "ChartImageCapture.TrySaveSmokePng(szFolder, out szMessage) — 오프스크린 렌더 진단"
  - "ReviewerWindow '차트 이미지 캡처 점검' 영구 진단 버튼(btn_chartSmoke)"
affects:
  - "WPF_Example/UI/Reviewer/ReviewerWindow.xaml(.cs)"
  - "WPF_Example/DatumMeasurement.csproj"
tech-stack:
  added: []
  patterns:
    - "offscreen-render — Window/HWND 없이 bare Canvas + RenderTargetBitmap 으로 PNG 생성 (이 코드베이스 최초)"
    - "explicit-measure-arrange — Measure/Arrange/UpdateLayout 3단계 명시 호출로 자식 배치 확정"
    - "sta-gate — Application.Current.Dispatcher.CheckAccess() 로 UI 스레드 보장, 아니면 Invoke 마샬링"
    - "mirror-verified-pattern — ExcelExportService.TryInsertCaptureImage 스케일 로직 미러(Jpeg→Png)"
    - "dormant-diagnostic-button — ExcelExportSmokeTest 관례를 따른 영구 진단 UI"
key-files:
  created:
    - "WPF_Example/Custom/Export/ChartImageCapture.cs"
  modified:
    - "WPF_Example/UI/Reviewer/ReviewerWindow.xaml"
    - "WPF_Example/UI/Reviewer/ReviewerWindow.xaml.cs"
    - "WPF_Example/DatumMeasurement.csproj"
decisions:
  - "Canvas.Background = Brushes.White 로 흰 배경 확보 — Pbgra32 기본은 투명이라 엑셀에서 회색으로 보일 수 있다. Background 는 자식이 아니라 프로퍼티라 ChartRenderService 의 Children.Clear() 에 지워지지 않는다"
  - "행 높이 조작(ws.Row(n).Height) 미적용 — 차트는 데이터 영역 아래 별도 블록에 놓이므로 데이터 행 높이를 건드리면 안 된다. 따라서 pt 환산 상수(EXCEL_POINTS_PER_PIXEL)도 두지 않았다(참조처 없는 상수 금지)"
  - "폭/높이를 CHART_RENDER_*_PX 상수로 직접 전달 — 오프스크린 Canvas 의 ActualWidth 는 0 이라 그대로 넘기면 빈 이미지가 된다(72-02 경고)"
  - "진단 버튼을 임시가 아닌 영구 도구로 유지 — 프로젝트의 티칭 진단 기능 관례와 동일, 렌더 회귀 발생 시 즉시 재현 가능"
metrics:
  duration: "약 15분"
  completed: "2026-08-18"
  tasks: "2 of 3 (Task 3 deferred)"
  files: 4
---

# Phase 72 Plan 04: 오프스크린 차트 PNG 캡처 Summary

Window 없이 bare `Canvas` 에 차트를 그려 `RenderTargetBitmap` 으로 PNG 를 뽑고 ClosedXML `AddPicture` 로 xlsx 에 종횡비 유지 삽입하는 경로를 만들고, 리뷰어 창에 이를 즉시 점검할 수 있는 영구 진단 버튼을 붙였다.

---

## ⚠ 미검증 리스크 (반드시 읽을 것)

**오프스크린 `RenderTargetBitmap` 캡처가 실제로 유효한 PNG(그림이 들어 있는 이미지)를 만드는지 아직 사람이 확인하지 않았다.**

- 이 Phase 계획상 Task 3(육안 검증)은 **72-05 이후 진행 전에 통과했어야 하는 blocking 체크포인트**였다. 사용자가 모바일 환경이라 PC 접근이 불가하여 **보류(deferred)** 되었고, 나머지 plan(72-05~07) 작성을 먼저 진행하기로 결정했다.
- `RenderTargetBitmap` 오프스크린 캡처는 이 코드베이스에 **전례가 0건**이다(72-PATTERNS §No Analog Found). `Measure/Arrange/UpdateLayout` 누락이나 실측 크기 0 함정에 걸리면 **예외 없이 조용히 빈(백지) PNG** 가 나온다. 빌드 PASS 와 grep 통과는 이 실패 모드를 전혀 잡아내지 못한다.
- **72-07 의 엑셀 차트 삽입은 전적으로 이 경로에 의존한다.** 만약 빈 이미지가 나오는 상태라면, 72-07 산출물의 xlsx 차트 블록이 **에러 하나 없이 조용히 백지**가 된다. 사용자가 보고서를 열어본 뒤에야 알게 되는 형태의 실패다.
- 따라서 **72-07 UAT 시점에 아래 절차를 반드시 먼저 수행**해야 한다. 통과하기 전에는 72-07 결과물의 차트를 신뢰하면 안 된다.

### 육안 검증 절차 (이 대화 없이도 따라갈 수 있도록 그대로 옮김)

1. Visual Studio 에서 **`Debug/x64`** 로 `DatumMeasurement` 를 실행한다.
2. **리뷰어 창**을 연다(기존 진입 경로).
3. 좌측 패널 맨 아래 **"차트 이미지 캡처 점검"** 버튼을 누른다.
4. 메시지 박스에 표시된 두 경로의 PNG 를 연다 (기본 설정 기준 `Setting.ResultSavePath` = `D:\Data\Result`):
   - `chart_smoke_histogram.png`
   - `chart_smoke_trend.png`
5. 아래 4가지를 확인한다:
   - [ ] 이미지가 **480×320 크기이고 흰 배경**이다 (완전 투명 / 완전 백지 아님)
   - [ ] 히스토그램에 **파란 막대들**이 보이고, 좌측에 세로 "빈도(개수)" 라벨과 y축 눈금이 있다
   - [ ] **빨간 USL/LSL 수직선**과 라벨이 보인다
   - [ ] 추이 그래프에 **파란 꺾은선**과 x축 샘플 번호(1..100 중 5개 내외), y축 숫자 라벨이 보인다
6. 텍스트가 깨지거나(□□□) 라벨 위치가 잘리면 그 내용을 기록한다(폰트/DPI 이슈 — 72-07 차트 크기 조정 근거가 된다).

**판정 기준:** 두 PNG 가 `Setting.ResultSavePath` 에 생성되고 **파일 크기 > 5KB**, 그리고 위 4개 항목 전부 확인.

**빈 이미지(하얗기만 함)가 나오는 경우 조치:** `ChartImageCapture.CaptureCanvasPng` 의 `Measure` → `Arrange` → `UpdateLayout` 3단계 순서, 그리고 `RenderHistogramPng`/`RenderTrendPng` 가 `ChartRenderService` 에 넘기는 폭/높이 인자(`CHART_RENDER_WIDTH_PX`/`CHART_RENDER_HEIGHT_PX`)를 재점검한다. `ActualWidth` 가 어딘가로 새어 들어오면 0 이 전달되어 렌더 서비스의 `if (dW <= 0 || dH <= 0) return;` 가드에 걸린다.

---

## What Was Built

### Task 1 — ChartImageCapture.cs 신규 + csproj 등록 (`9de4402`)

`WPF_Example/Custom/Export/ChartImageCapture.cs` (258줄) 신규. `namespace ReringProject.Export`, `public static class ChartImageCapture`.

- **상수 5개**: `CHART_RENDER_WIDTH_PX=480` / `CHART_RENDER_HEIGHT_PX=320`(PNG 해상도), `CHART_BOX_WIDTH_PX=360` / `CHART_BOX_HEIGHT_PX=240`(엑셀 표시 박스), `CHART_DPI=96.0`. 행 높이를 조작하지 않으므로 pt 환산 상수는 두지 않았다.
- **`CaptureCanvasPng(Canvas)`** — `Measure` → `Arrange` → `UpdateLayout` 3단계 후 `RenderTargetBitmap`(Pbgra32) → `PngBitmapEncoder` → `byte[]`. 실패 시 `Logging.PrintErrLog` 후 `null`.
- **`CreateChartCanvas()`** — 480×320 흰 배경 bare Canvas 생성.
- **`InvokeOnUiThread(Func<byte[]>)`** — `Application.Current` null 이면 직접 호출, `Dispatcher.CheckAccess()` 참이면 직접, 아니면 `Dispatcher.Invoke`.
- **`RenderHistogramPng` / `RenderTrendPng`** — 값 없으면 `null`, 아니면 UI 스레드에서 Canvas 생성 → `ChartRenderService` 렌더 → 캡처.
- **`TryInsertChartPicture(IXLWorksheet, nRow, nColumn, byte[])`** — `ExcelExportService.TryInsertCaptureImage`(240-305) 스케일 로직 미러. `AddPicture` 는 `using` 안, 스케일 조작은 밖. `XLPictureFormat.Png`, `dScale > 1.0` 클램프(원본보다 키우지 않음), 최소 1px 가드, `WithPlacement(Move)` → `WithSize` → `MoveTo`. **행 높이 대입 없음.**
- **`TrySaveSmokePng(szFolder, out szMessage)`** — 고정 시드(`Random(20260818)`)로 평균 10.0 ± 0.2 합성 샘플 100개 생성, USL/LSL = 10.2/9.8, PNG 2장을 하드코딩 파일명으로 저장.
- csproj `Custom\Export` 블록에 알파벳 순(`ExcelExportSmokeTest` 앞) `<Compile Include="Custom\Export\ChartImageCapture.cs" />` 1줄 삽입.

### Task 2 — ReviewerWindow 진단 버튼 (`a6e8e59`)

- `ReviewerWindow.xaml`: `btn_repeatExport` 바로 뒤, `</StackPanel>` 앞에 `btn_chartSmoke`("차트 이미지 캡처 점검") 삽입. 72-03 이 추가한 `txt_materialIndex` / `chk_repeatAccumulate` 는 그대로 보존됐다(grep 2건 확인).
- `ReviewerWindow.xaml.cs`: `Button_RepeatExport_Click` 바로 뒤에 `Button_ChartSmoke_Click` 추가. `Setting.ResultSavePath` → `TrySaveSmokePng` → if/else 아이콘 분기 → `CustomMessageBox.Show`. 기존 export 핸들러 관례 그대로.
- 이 버튼은 **영구 진단 도구**로 남긴다 — 나중에 렌더 회귀가 나면 즉시 재현 가능.

## Verification

| 항목 | 결과 |
|------|------|
| Task 1 acceptance grep (13건) | 전부 기대값 일치 — `ClosedXML.Excel.Drawings` 1 / Measure·Arrange·UpdateLayout 각 1 / `XLPictureFormat.Png` 1 / `Brushes.White` 1 / `CheckAccess()` 1 / `TrySaveSmokePng` 1 / `ws.Row(` **0** / `EXCEL_POINTS_PER_PIXEL` **0** / csproj 등록 1 / 삼항 **0** |
| Task 2 acceptance grep (5건) | 전부 기대값 일치 — `btn_chartSmoke` 1 / `Click="Button_ChartSmoke_Click"` 1 / 핸들러 1 / `TrySaveSmokePng(szFolder, out szMessage)` 호출 1 / 삼항 0 |
| verification #2 — `\.ActualWidth\|\.ActualHeight` in ChartImageCapture.cs | **0건** (오프스크린 실패 모드 원천 차단) |
| msbuild Debug/x64 (scratch OutDir) × 2회 | 둘 다 exit 0, CS 에러 0, CS0246 0(`IXLPicture`/`XLPictureFormat`/`XLPicturePlacement` 정상 해석), XAML 컴파일 에러 0 |
| 빌드 경고 | 12줄 (CS0618×10 + CS0162×2) = baseline, 신규 경고 0 |
| csproj diff | `<Compile Include>` **1줄 추가만** — `OutputPath` / `DefineConstants` 변경 없음 (커밋 전 `git diff` 로 직접 확인) |
| 파일 삭제 | 두 커밋 모두 `--diff-filter=D` 결과 없음 |
| 미추적 파일 | 0건 |
| **verification #3 — 육안 검증 체크포인트** | **❌ 미수행 (deferred — 위 ⚠ 섹션 참조)** |

## must_haves 대응

| Truth | 상태 |
|-------|------|
| 오프스크린 Canvas 를 PNG 바이트로 캡처할 수 있다 (빈 이미지가 아니다) | ⚠ **미검증** — 코드 경로는 존재하나 "빈 이미지가 아니다"는 사람 확인 필요 |
| 캡처된 히스토그램/추이 PNG 에 막대·축·USL/LSL 기준선·라벨이 사람 눈에 보인다 | ⚠ **미검증** — Task 3 deferred |
| PNG 를 xlsx 워크시트에 종횡비 유지로 삽입할 수 있다 | ✅ `TryInsertChartPicture` — 검증된 `TryInsertCaptureImage` 패턴 미러, 빌드 PASS |
| 렌더가 UI/STA 스레드에서 수행됨이 보장된다 | ✅ `InvokeOnUiThread` + `Dispatcher.CheckAccess()` 게이트 |

| key_link | 상태 |
|----------|------|
| `ChartImageCapture.RenderHistogramPng` → `ChartRenderService.RenderHistogram` (bare Canvas + 명시 폭/높이) | ✅ `ChartRenderService.RenderHistogram(canvas, CHART_RENDER_WIDTH_PX, ...)` 패턴 일치 |

## Coding Rules 준수

- 삼항 연산자 미사용 (두 파일 `[^?]\? .+ : ` 0 matches) — 전부 if-else
- 헝가리언 (`bEmpty` / `bHasBytes` / `bInvalidSize` / `bOk` / `bBad` / `nRow` / `nOriginalWidth` / `nTargetHeight` / `dScale` / `dUsl` / `szFolder` / `szMessage` / `arrBytes` / `arrHist`)
- Allman 브레이스 (신규 파일 + 편집 파일 모두 기존 스타일)
- C# 7.2 문법만 사용 (람다 OK, switch expression / record / nullable reference types 없음)
- 신규 .cs 파일 csproj 수동 등록 완료
- 날짜 접두 주석 없음

## Threat Model 대응

- **T-72-05 (Tampering, mitigate)** — 적용됨. 파일명이 하드코딩 상수(`chart_smoke_histogram.png` / `chart_smoke_trend.png`)이고 폴더는 앱 설정값(`Setting.ResultSavePath`)이다. 사용자 입력이 경로에 섞이는 지점이 없다.
- **T-72-06 (DoS, accept)** — 그대로. 진단 버튼은 수동 1회 호출이고, export 경로의 반복 렌더 상한은 72-07 의 `MAX_CHART_ROWS` 가 담당한다.

## Deviations from Plan

### Rule 3 이외 — 계획 대비 변경 1건 (오케스트레이터 지시)

**1. [체크포인트 보류] Task 3 육안 검증 미수행**
- **발견 시점:** Task 3 체크포인트 도달 후
- **경위:** blocking 체크포인트에서 정지하고 구조화된 상태를 반환했다. 사용자가 모바일 환경이라 PC 접근이 불가하여, **72-05~07 plan 작성을 먼저 진행**하기로 결정했다.
- **처리:** 자체 승인하지 않았다. Task 3 를 `deferred` 로 명시 기록하고, 위 **⚠ 미검증 리스크** 섹션에 검증 절차 전문과 실패 시 파급 범위를 남겼다.
- **해소 시점:** 72-07 UAT.

Task 1, 2 자체는 plan 에 쓰인 그대로 실행됐다(코드 내용 변경 없음).

## Known Stubs

없음. 다만 위 ⚠ 섹션의 미검증 리스크는 stub 이 아니라 **런타임 동작 미확인**이므로 별도로 취급한다.

## 다음 plan 참고사항

- 72-07 에서 차트를 삽입할 때는 `ChartImageCapture.RenderHistogramPng` / `RenderTrendPng` 로 byte[] 를 얻고 `TryInsertChartPicture(ws, nRow, nColumn, arrBytes)` 를 호출하면 된다. `TryInsertChartPicture` 는 `internal` 이므로 같은 어셈블리에서만 호출 가능하다(현재 단일 어셈블리라 문제 없음).
- **72-07 착수 전 또는 UAT 최초에 반드시 "차트 이미지 캡처 점검" 버튼부터 눌러 볼 것.** 빈 PNG 상태라면 xlsx 차트도 백지다.
- `TryInsertChartPicture` 는 행 높이를 건드리지 않으므로, 차트가 잘려 보이면 72-07 쪽에서 차트 블록 전용 행 높이를 별도로 지정해야 한다(데이터 행이 아닌 차트 블록 행에 한해).
- 폰트/DPI 이슈(라벨 깨짐·잘림) 결론은 육안 검증 후에야 기록 가능하다 — 현재 미확인.

## Self-Check: PASSED

- `WPF_Example/Custom/Export/ChartImageCapture.cs` — FOUND
- `WPF_Example/UI/Reviewer/ReviewerWindow.xaml` — FOUND
- `WPF_Example/UI/Reviewer/ReviewerWindow.xaml.cs` — FOUND
- `WPF_Example/DatumMeasurement.csproj` — FOUND
- commit `9de4402` — FOUND
- commit `a6e8e59` — FOUND
