---
phase: 72-cpk-rapid-city-a8-1-z-stopper-data-report-r04-raw-data-cpk-e
plan: 02
subsystem: statistics-ui
tags: [chart, canvas, refactor, offscreen-render, export]
requires:
  - "72-01 (MeasurementStat 확장) — 코드 의존은 없고 실행 순서만 의존"
provides:
  - "ChartRenderService.RenderHistogram(Canvas, dW, dH, values, dUsl, dLsl)"
  - "ChartRenderService.RenderTrend(Canvas, dW, dH, values, dMean, dUsl, dLsl)"
  - "Window 인스턴스 없이 오프스크린 Canvas 에 차트를 그릴 수 있는 진입점"
affects:
  - "WPF_Example/UI/Statistics/StatisticsWindow.xaml.cs"
  - "WPF_Example/DatumMeasurement.csproj"
tech-stack:
  added: []
  patterns:
    - "extract-class(순수 이동) — 인스턴스 드로잉 메서드를 정적 헬퍼 클래스로 이동"
    - "measure-injection — ActualWidth/ActualHeight 를 인자(dW/dH)로 승격해 레이아웃 의존 제거"
    - "thin delegating wrapper — 기존 호출부 시그니처 보존"
key-files:
  created:
    - "WPF_Example/UI/Statistics/ChartRenderService.cs"
  modified:
    - "WPF_Example/UI/Statistics/StatisticsWindow.xaml.cs"
    - "WPF_Example/DatumMeasurement.csproj"
decisions:
  - "폭/높이를 인자로 승격 — 오프스크린 Canvas 는 ActualWidth==0 이라 기존 코드로는 빈 이미지가 나온다(이 plan 의 핵심)"
  - "ChartRenderService 안에 .ActualWidth/.ActualHeight 프로퍼티 접근을 0건으로 강제 — 실패 모드 원천 차단"
  - "브러시 6개는 Window 가 아니라 서비스 쪽으로 이동(드로잉 전용) — Window 에는 차트 관련 상태가 남지 않는다"
  - "using 정리는 하지 않음 — 미사용 using 은 경고를 내지 않고, 손대면 회귀 위험만 늘어난다"
  - "날짜 접두 주석(//260707 hbk)은 이동하면서 제거(정책 폐기), 설명 본문은 보존"
metrics:
  duration: "약 12분"
  completed: "2026-08-18"
  tasks: 2
  files: 3
---

# Phase 72 Plan 02: ChartRenderService 추출 Summary

`StatisticsWindow` 에 묶여 있던 히스토그램/추이 Canvas 드로잉 로직을 `ChartRenderService` 정적 클래스로 순수 이동하고, 폭/높이를 인자로 승격해 오프스크린 Canvas 렌더가 가능해졌다.

## What Was Built

### Task 1 — ChartRenderService.cs 신규 + csproj 등록 (`22e3d36`)

`WPF_Example/UI/Statistics/ChartRenderService.cs` (565줄) 신규.

- 상수 3개(`BIN_COUNT=20` / `MAX_X_LABELS=5` / `MERGE_PX=12.0`), Frozen 브러시 6개, 드로잉 메서드 16개를 원본 값 그대로 이동.
- `RenderHistogram` / `RenderTrend` 만 `public static`, 나머지 헬퍼(`TrendIndexToX`, `DrawTrendXLabels`, `DrawTrendSpecMarks`, `DrawAxisLines`, `DrawYTicksCount`, `DrawYTicksValue`, `DrawVLine`, `DrawNoDataText`, `CreateLabel`, `MakeFrozenBrush`, `ComputePaddedRange`, `BuildHistogramBins`, `MinOf`, `MaxOf`)는 `private static`.
- 두 진입점 시그니처에 `Canvas canvas, double dW, double dH` 를 추가하고, 본문의 `canvas_Histogram` / `canvas_Trend` 를 `canvas` 로 치환. `double dW = canvas_X.ActualWidth;` 2줄씩(총 4줄) 삭제.
- `canvas.Children.Clear()` 와 `if (dW <= 0 || dH <= 0) return;` 가드는 유지 — 인자로 0 이 들어와도 기존과 동일하게 조용히 빠진다.
- 여백(40/55/24/10/10), 폰트(10/11/13), 회전(-90), 15% 패딩, 버블 정렬, 그리디 병합 로직 전부 원본 그대로.
- csproj `<Compile Include="UI\Statistics\ChartRenderService.cs" />` 를 `StatisticsWindow.xaml.cs` 블록 바로 앞에 삽입.

### Task 2 — StatisticsWindow 축소 (`3ea58b8`)

`StatisticsWindow.xaml.cs` 796줄 → 260줄 (5 insertions / 542 deletions).

- 이동된 상수/브러시/메서드 전부 제거.
- `RenderHistogram` / `RenderTrend` 는 `private void` 인스턴스 메서드 시그니처를 그대로 유지한 채 `ChartRenderService` 로 위임만 한다 → 호출부 `RenderCurrentSelection()` 무수정.
- `RECIPE_ALL`, `m_lastResult`, 생성자, `Btn_Query_Click`, `DoQuery`, `PopulateRecipeCombo`, `BuildRows`, `CpkToText`, `YieldRateToText`, `Grid_Stats_SelectionChanged`, `Canvas_SizeChanged`, `RenderCurrentSelection`, `ClearCharts` 는 한 글자도 안 바뀌었다.

## Verification

| 항목 | 결과 |
|------|------|
| Task 1 acceptance grep (9건) | 전부 기대값 일치 → `TASK1_OK` |
| Task 2 acceptance grep (8건) | 전부 기대값 일치 → `TASK2_GREP_OK` |
| `\.ActualWidth\|\.ActualHeight` in ChartRenderService.cs | 0건 (오프스크린 실패 모드 차단) |
| 삼항 `[^?]\? .+ : ` (두 파일) | 0 matches |
| msbuild Debug/x64 (scratch OutDir) | exit 0, CS 에러 0, CS0111 0, CS0103 0 |
| 빌드 경고 | 12줄 (CS0618×10 + CS0162×2) = baseline, 신규 경고 0 |
| csproj diff | `<Compile Include>` 1줄 추가만 — OutputPath/DefineConstants 변경 없음 |
| 파일 삭제 | `--diff-filter=D` 결과 없음 |

## Coding Rules 준수

- 삼항 연산자 미사용 (전부 if-else)
- 헝가리언 유지 (`dW`/`dH`/`nCount`/`szLabel`/`bHasSpec`/`m_brushXxx`)
- Allman 브레이스 (신규 파일 + 편집 파일 모두 기존 스타일)
- C# 7.2 문법만 사용 (switch expression / record / nullable reference type 없음)
- 신규 .cs 파일 csproj 수동 등록 완료

## Deviations from Plan

None - plan executed exactly as written.

## Known Stubs

None.

## 다음 plan 참고사항

- `ChartRenderService.RenderHistogram/RenderTrend` 호출 시 **반드시 실제 픽셀 폭/높이를 직접 계산해서 넘겨야 한다.** 오프스크린 Canvas 에서 `ActualWidth` 를 읽어 넘기면 0 이 들어가 가드에 걸려 빈 이미지가 된다.
- 렌더 후 비트맵으로 뽑으려면 Canvas 에 `Measure(new Size(dW, dH))` + `Arrange(new Rect(0, 0, dW, dH))` + `UpdateLayout()` 을 명시 호출해야 자식 요소의 `Canvas.SetLeft/SetTop` 배치가 반영된다(`RenderTargetBitmap` 전제).
- 두 메서드는 진입 시 `canvas.Children.Clear()` 를 호출하므로, 호출 전에 Canvas 에 배경 등을 미리 넣어두면 지워진다.
- 시각 회귀 육안 검증은 72-04 체크포인트에서 수행.

## Self-Check: PASSED

- `WPF_Example/UI/Statistics/ChartRenderService.cs` — FOUND
- `WPF_Example/UI/Statistics/StatisticsWindow.xaml.cs` — FOUND
- `.planning/phases/72-cpk-rapid-city-a8-1-z-stopper-data-report-r04-raw-data-cpk-e/72-02-SUMMARY.md` — FOUND
- commit `22e3d36` — FOUND
- commit `3ea58b8` — FOUND
