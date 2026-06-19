---
phase: 57-pattern-roi-ux-datum-align-hardening
plan: 05
subsystem: ui-result-viewer
tags: [pattern-roi, overlay-toggle, halcon, wpf-xaml, visualization]
requires:
  - "RenderResultRoiBoxes(IList<double[]>, color, lineWidth) — HalconDisplayService (변경 없음)"
  - "DatumConfig PatternRoi_*/PatternRoi2_* double 필드 (Phase 54 ALIGN-01)"
  - "SetDatumOverlayVisible 미러 원본 (MainResultViewerControl)"
provides:
  - "SetPatternRoiOverlayVisible 토글 + 패턴 ROI cyan 렌더 게이트 (결과화면)"
  - "chk_overlayPattern 토글 체크박스 (MainView 툴바)"
affects:
  - "WPF_Example/UI/ContentItem/MainResultViewerControl.xaml.cs"
  - "WPF_Example/UI/ContentItem/MainView.xaml"
  - "WPF_Example/UI/ContentItem/MainView.xaml.cs"
tech-stack:
  added: []
  patterns:
    - "datum 토글(SetDatumOverlayVisible) 미러 — 게이트 필드 + setter + RenderNow 게이트 동형"
    - "HALCON SetColor 유효 색상명만 (cyan, RenderResultRoiBoxes 내 검증됨)"
key-files:
  created: []
  modified:
    - "WPF_Example/UI/ContentItem/MainResultViewerControl.xaml.cs"
    - "WPF_Example/UI/ContentItem/MainView.xaml"
    - "WPF_Example/UI/ContentItem/MainView.xaml.cs"
decisions:
  - "패턴 ROI 색 = cyan (datum orange / 측정 green / datum 기준선 slate blue 와 구분, T-57-09/10 mitigation)"
  - "패턴 ROI 소스 = _resultDatumOverlays(List<DatumConfig>) 순회 → PatternRoi/PatternRoi2 sentinel(Length1/2 > 0) 판정 후 double[]{row,col,phi,l1,l2} 구성"
  - "RenderResultRoiBoxes 시그니처는 IList<double[]> — List<double[]> 전달로 호환 (변경 0)"
metrics:
  duration_min: 6
  completed: "2026-06-19"
  tasks: 3
  files: 3
---

# Phase 57 Plan 05: 패턴 ROI 표시/숨김 토글 Summary

결과화면(MainResultViewerControl)에 패턴 매칭 ROI(패턴1/패턴2) 표시/숨김 토글을 추가했다. 기존 datum/측정 토글(SetDatumOverlayVisible) 미러 패턴으로 게이트 필드 + setter + RenderNow 렌더 게이트를 동형 구현하고, MainView 툴바에 chk_overlayPattern 체크박스 + 핸들러를 측정/Datum 토글과 나란히 배치했다. 패턴 ROI 는 datum(orange)·측정(green)·datum 기준선(slate blue)과 구분되는 cyan 으로 렌더된다 (D-15).

## What Was Built

### Task 1: MainResultViewerControl 패턴 ROI 토글 게이트 + 렌더 (cf97c5b)
- 게이트 필드 `private bool _patternRoiOverlayVisible = true;` 추가 (`_datumOverlayVisible` 옆, :585).
- 토글 setter `SetPatternRoiOverlayVisible(bool)` = `_patternRoiOverlayVisible = visible; Render();` 추가 (`SetDatumOverlayVisible` 옆, 동형).
- RenderNow 에 패턴 ROI 렌더 게이트 추가 (orange datum ROI 게이트 직후):
  - `_patternRoiOverlayVisible && _resultDatumOverlays != null && Count > 0` 게이트.
  - `_resultDatumOverlays`(List<DatumConfig>) 순회 → 각 datum 의 `PatternRoi_Length1/2 > 0.0` 및 `PatternRoi2_Length1/2 > 0.0` sentinel 판정 후 `double[]{row,col,phi,l1,l2}` 구성.
  - `RenderResultRoiBoxes(window, patternRects, "cyan", 2)` 호출.
- double[] 순서 `{row,col,phi,l1,l2}` 는 RenderResultRoiBoxes 의 `r[0]=row .. r[4]=l2` (DispRectangle2 인자, :496) 규약과 일치 확인 — orange/green ROI 와 동일 규약.

### Task 2: MainView 패턴 ROI 토글 체크박스 + 핸들러 (9a290a7)
- MainView.xaml: `chk_overlayDatum` 직후, 같은 StackPanel 내에 `chk_overlayPattern` CheckBox 추가 (Content "패턴 ROI", IsChecked="True", Checked/Unchecked="Chk_overlayPattern_Changed"). 측정 overlay / Datum 라인 토글과 나란히 배치.
- MainView.xaml.cs: `Chk_overlayDatum_Changed` 직후에 `Chk_overlayPattern_Changed` 핸들러 추가 — `halconViewer.SetPatternRoiOverlayVisible(chk_overlayPattern.IsChecked == true);` 호출 (null 가드 포함, datum 핸들러 미러).
- x:Name(chk_overlayPattern) ↔ 핸들러명(Chk_overlayPattern_Changed) XAML/code-behind 일치 — Task 3 빌드로 markup 검증.

### Task 3: SIMUL_MODE Debug/x64 빌드 검증
- MSBuild Debug/x64 Rebuild → `error CS / MC / MarkupCompile 0건`, 신규 warning CS 0건.
- DatumMeasurement.exe 생성 확인 (`bin/x64/Debug/DatumMeasurement.exe`).
- source 변경 없음 → 별도 commit 없음.

## Verification Results

- `SetPatternRoiOverlayVisible|_patternRoiOverlayVisible` in MainResultViewerControl.xaml.cs: **4건** (필드 1 + setter 시그니처 1 + setter 본문 1 + 게이트 1) (PASS)
- `chk_overlayPattern|Chk_overlayPattern_Changed` in MainView.xaml: **2건** (x:Name + Checked/Unchecked 핸들러 참조) (PASS)
- `chk_overlayPattern|Chk_overlayPattern_Changed` in MainView.xaml.cs: **2건** (핸들러 정의 + SetPatternRoiOverlayVisible 호출 라인의 chk_overlayPattern 참조) (PASS)
- double[] 원소 순서 {row,col,phi,l1,l2} ↔ RenderResultRoiBoxes r[0..4] 규약 일치 (PASS)
- 패턴 ROI 색 = "cyan" (RenderResultRoiBoxes 내 기존 사용된 유효 HALCON 색상명) (PASS)
- MSBuild Debug/x64 Rebuild: error CS/MC/MarkupCompile **0건**, DatumMeasurement.exe 생성 (PASS)
- 변경 라인 주석: `//260619 hbk Phase 57 #2 ...` 부착 (PASS)

## Deviations from Plan

None - plan executed exactly as written.

`RenderResultRoiBoxes` 의 실제 시그니처는 `IList<double[]>`(플랜 interfaces 표기 `List<double[]>` 와 차이) 이나, `List<double[]>` 가 `IList<double[]>` 를 구현하므로 인자 전달 호환 — 코드 변경 불필요.

빌드 출력의 warning(CS0618 Phase 33 deprecated 타입, CS0162 VirtualCamera unreachable, MSB3884 ruleset 부재)은 모두 phase 57 이전 baseline 으로 본 plan 변경과 무관 (신규 warning 0).

## Self-Check: PASSED

- FOUND: WPF_Example/UI/ContentItem/MainResultViewerControl.xaml.cs (_patternRoiOverlayVisible + SetPatternRoiOverlayVisible + cyan 게이트)
- FOUND: WPF_Example/UI/ContentItem/MainView.xaml (chk_overlayPattern)
- FOUND: WPF_Example/UI/ContentItem/MainView.xaml.cs (Chk_overlayPattern_Changed)
- FOUND: commit cf97c5b (Task 1)
- FOUND: commit 9a290a7 (Task 2)
- Build: 0 error CS / markup error, DatumMeasurement.exe 생성
