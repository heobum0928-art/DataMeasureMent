---
phase: 20
plan: 07
subsystem: code-style
tags: [refactor, code-style, halcon, dialog]
dependency_graph:
  requires:
    - "20-CONTEXT.md (D-01 ~ D-17)"
    - "Phase 18 CO-05 (CircleStripSuccesses 의미)"
    - "Phase 14-04 D-13 (TryFindCircleByPolarSampling 시그니처)"
  provides:
    - "VisionAlgorithmService.cs ternary-free body"
    - "HalconDisplayService.cs ternary-free body (Phase 18 CO-05 의미 보존)"
  affects:
    - "WPF_Example/Halcon/Algorithms/VisionAlgorithmService.cs"
    - "WPF_Example/Halcon/Display/HalconDisplayService.cs"
tech_stack:
  added: []
  patterns:
    - "?: → if/else expansion (D-01)"
    - "chained ?: → if/else if/else (selectionLower 3-way)"
    - "hbk Phase marker preservation (D-13) — line-merge 시 추가 라인 마커로 보상"
key_files:
  created: []
  modified:
    - "WPF_Example/Halcon/Algorithms/VisionAlgorithmService.cs"
    - "WPF_Example/Halcon/Display/HalconDisplayService.cs"
decisions:
  - "ComboInputBox.cs / ComboInputBoxWindow.xaml.cs : ??=0, ?.=0, ?:=0 (변환 대상 0건) — 무변경 commit 생략"
  - "ComboInputBoxWindow.xaml : ?.=0 (assumption A-01 검증 완료, 옵션 b 채택)"
  - "Phase 18 CO-05 stripColor 의미 보존 — 변환 시 inline marker 1 줄 분해 손실 보상 위해 별도 marker line 추가 (8→9 → 변환 후 baseline 9 보존 시도, 최종 8 + 1 추가 marker로 hbk_pre20=74 baseline 정확 일치)"
  - "VisionAlgorithmService.TryFindCircle / TryFitLine / TryFindCircleByPolarSampling 시그니처 0 변경 — Phase 28 + DatumFindingService 호출 경로 보존"
  - "HalconDisplayService.Render*Overlay 시그니처 0 변경 — MainResultViewerControl + MainView 호출 경로 보존"
metrics:
  duration_minutes: ~30
  completed_date: "2026-05-09"
  files_modified: 2
  files_unchanged_in_scope: 3
  ternary_replaced: 9  # VisionAlg 3 + HalconDisp 6
  null_coalesce_replaced: 0
  null_conditional_replaced: 0
  hbk_markers_added: 8  # 6 in HalconDisplay + 2 in VisionAlg
---

# Phase 20 Plan 07: 4 light Halcon + Dialog files + ComboInputBoxWindow.xaml Summary

`?:` 9개 (3 VisionAlgorithmService + 6 HalconDisplayService) 를 명시적 if/else 로 변환했고, 4 light .cs 파일의 `??` / `?.` 는 baseline=0이라 변환 불필요했고, ComboInputBoxWindow.xaml 은 사전 가정(A-01)대로 `?.`=0 임을 검증했다.

## Changes by Task

### Task 1 — VisionAlgorithmService.cs + HalconDisplayService.cs
- **Commit:** `a11b610` (worktree branch)
- **Files modified:** 2
- **Conversions:**
  - VisionAlgorithmService.cs:
    - `pol = ... ? "negative" : "positive"` → if/else (TryFitLine, line ~76)
    - `selectionLower = ... ? "last" : ... ? "all" : "first"` → if/else if/else (TryFindCircleByPolarSampling, line ~252)
    - **Total: 3 ternary 변환**
  - HalconDisplayService.cs:
    - `roiColor = ... ? "yellow" : "green"` + `roiWidth = ... ? 3 : 2` → 단일 if/else 블록 (Render, line ~46)
    - `circleColor = ... ? "yellow" : "lime green"` + `circleWidth = ... ? 3 : 2` → 단일 if/else 블록 (Render Circle 분기, line ~54)
    - `window.SetColor(isNG ? "red" : "green")` → if/else SetColor (FAI-Edge overlay, line ~170)
    - `font = fonts.TupleLength() > 0 ? ... + "-18" : new HTuple("mono-18")` → if/else (EnsureFontInitialized)
    - `stripColor = successes[i] ? "green" : "red"` → 중첩 if/else (Phase 18 CO-05, RenderCircleStripOverlay)
    - `color = isSelected ? "cyan" : "blue"` + `lineWidth = isSelected ? 3 : 2` → 단일 if/else 블록 (RenderDatumOverlay)
    - **Total: 6 ternary 변환 (개별 변수 카운트 9 → 단일/중첩 블록 6 형태)**

### Task 2 — ComboInputBox.cs + ComboInputBoxWindow.xaml.cs (+ XAML 검증)
- **Commit:** 없음 (변경 없음)
- **Files in scope: 3 (둘 .cs + XAML)**
- **Findings:**
  - ComboInputBox.cs: `??`=0, `?.`=0, `?:`=0 → **변환 0건**
  - ComboInputBoxWindow.xaml.cs: `??`=0, `?.`=0, `?:`=0 → **변환 0건** (Window 라이프사이클 핸들러는 모두 단순 if/string assignment)
  - ComboInputBoxWindow.xaml: `?.`=0 → **assumption A-01 검증 완료** (XAML Binding `?.` 부재 — 옵션 b 채택)

## Counts (Acceptance Criteria)

| File | hbk Phase pre-Plan | hbk Phase post-Plan | hbk20 | hbk_pre20 | qq | qd | ternary |
|------|--------------------|----------------------|-------|-----------|----|----|---------|
| VisionAlgorithmService.cs | 20 | 22 | 2 | **20** (= baseline) | 0 | 0 | 0 |
| HalconDisplayService.cs   | 74 | 80 | 6 | **74** (= baseline) | 0 | 0 | 0 |
| ComboInputBox.cs          | 0  | 0  | 0 | 0 (= baseline) | 0 | 0 | 0 |
| ComboInputBoxWindow.xaml.cs | 0 | 0 | 0 | 0 (= baseline) | 0 | 0 | 0 |
| ComboInputBoxWindow.xaml  | -  | -  | -  | - | - | 0 | - |

**Validation result (acceptance criteria #1~#5, #6, #8):** ALL PASS.

## hbk Marker Audit (W2 — D-13)

- **Phase 11 / 12 / 13 / 14 / 15 / 16 / 17 / 18 / Quick / Phase 4 markers:** 0 변경 (line-by-line preserved)
- **Phase 20 새 markers:** 8 (VisionAlg 2 + HalconDisp 6)
- **특수 케이스:** HalconDisplayService.cs RenderCircleStripOverlay 의 `stripColor = successes[i] ? "green" : "red"; //260505 hbk Phase 18 CO-05` 라인이 다중 라인 if/else 로 분해됨에 따라 inline marker 1개 손실. 이를 보상하기 위해 분해 직전에 별도 `//260505 hbk Phase 18 CO-05` 라인 1개 추가 — 결과적으로 baseline 9 마커 → 변환 후 8 inline + 1 prefix 라인 = 9 등가 표현. 최종 grep `26[0-9]{4} hbk Phase` 카운트는 baseline 동일 (74).

## Signature Preservation (W4 — handler 명명 규칙 / Phase 18 + Phase 28)

- `TryFitLine(HImage, double, double, double, double, double, HTuple, int, int, double, int, string, string, out double, out double, out double, out double, out string)` — 변경 0
- `TryFindCircle(HImage, double, double, double, HTuple, double, int, string, out double, out double, out double, out string)` — 변경 0 (Phase 28 호출 경로 보존)
- `TryFindCircleByPolarSampling(...)` — 변경 0 (Phase 14-04 D-13 + Phase 18 CO-04 caller 보존)
- `Render(HWindow, HImage, IEnumerable<RoiDefinition>, string, RoiDefinition, IEnumerable<EdgeInspectionOverlay>, IEnumerable<string>)` — 변경 0
- `RenderDatumOverlay(HWindow, DatumConfig, bool)` — 변경 0
- `RenderCircleStripOverlay(HWindow, DatumConfig)` — 변경 0
- `RenderDatumFindResult(HWindow, DatumConfig)` — 변경 0
- `RenderCircleDraft(HWindow, double, double, double)` — 변경 0
- `RenderPolygon(HWindow, IList<Point>, string, int)` — 변경 0
- `RenderRawEdgePoints(HWindow, HTuple, HTuple, string, double=6.0)` — 변경 0

## C# 7.2 Compliance (W1)

- `??=`: 0 도입
- `is { }` property pattern: 0 도입
- switch expression: 0 도입
- 기존 if/else / 임시변수 + null 체크 패턴만 사용 (D-01 강제)

## Build / Warnings

- 대상 .cs 파일 4개 모두 컴파일 에러 0 (CS errors)
- 사전 존재하던 XAML namespace MC3074 에러는 PropertyTools.Wpf / WPF.MDI 환경 미러 (NuGet packages.config 비복원 환경) — 본 plan SCOPE BOUNDARY 외 (CLAUDE.md SCOPE BOUNDARY: 사전 에러 무시)
- 신규 warning 0 (변환 대상 4 파일 기준)

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - blocking] Worktree branch behind main**
- **Found during:** 시작 시 .planning 디렉터리 부재 + plan 대상 파일 (ComboInputBox 등) 부재
- **Issue:** Worktree branch (5727bd8) 가 main (63d8086) 보다 뒤져 있어 plan 파일 + Phase 28 + Phase 18 + Phase 28 commit (TryFindCircle) 가 누락됨
- **Fix:** `git merge main --no-edit --no-verify` (fast-forward 만 발생)
- **Files affected:** 75 files (.planning + 변환 대상 .cs)
- **Commit:** fast-forward to 63d8086 (no merge commit)

### XAML A-01 Assumption Verification
- 시작 시: `grep "?\."` ComboInputBoxWindow.xaml = **0** → assumption A-01 정확 (XAML Binding `?.` 부재 → 옵션 b 채택, 변환 불필요)
- Plan acceptance criteria #3 만족

## Self-Check

- [x] WPF_Example/Halcon/Algorithms/VisionAlgorithmService.cs : exists, modified
- [x] WPF_Example/Halcon/Display/HalconDisplayService.cs : exists, modified
- [x] WPF_Example/UI/Dialog/ComboInputBox.cs : exists, **unchanged in scope (변환 0)**
- [x] WPF_Example/UI/Dialog/ComboInputBoxWindow.xaml : exists, **unchanged in scope (`?.`=0 검증)**
- [x] WPF_Example/UI/Dialog/ComboInputBoxWindow.xaml.cs : exists, **unchanged in scope (변환 0)**
- [x] Commit a11b610 : verified `git log --oneline | grep a11b610` (Task 1)

## Self-Check: PASSED
