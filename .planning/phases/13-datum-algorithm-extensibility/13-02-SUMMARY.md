---
phase: 13-datum-algorithm-extensibility
plan: "02"
subsystem: UI/Datum-FindTest
tags: [datum, ui, runtime-test, phase-13, gap-4]
dependency_graph:
  requires: [13-01]
  provides: [Gap-4-RuntimeFindTestUI]
  affects: [MainView, MainResultViewerControl, HalconDisplayService]
tech_stack:
  added: []
  patterns:
    - "BtnTestFindDatum_Click — K&R brace, SelectedParam DatumConfig 해결, TryFindDatum 호출"
    - "AskTestImageSource — MessageBox.YesNoCancel 3-way 이미지 소스 선택"
    - "RenderDatumFindResult — Allman brace, 주황 3px 20px half 십자 + WriteString"
    - "SetDatumFindResultOverlay/ClearDatumFindResultOverlay — additive overlay, teach 경로 독립"
key_files:
  modified:
    - WPF_Example/UI/ContentItem/MainView.xaml
    - WPF_Example/UI/ContentItem/MainView.xaml.cs
    - WPF_Example/UI/ContentItem/MainResultViewerControl.xaml.cs
    - WPF_Example/Halcon/Display/HalconDisplayService.cs
decisions:
  - "IsEnabled=True on btn_testFindDatum — 핸들러에서 datum null/IsConfigured/LastTeachSucceeded 가드 (InspectionListView 활성화 로직 불변)"
  - "label_testFindResult Grid.Column=1 — label_drawHint 와 동일 컬럼, BtnTestFindDatum_Click 진입 시 drawHint Collapsed + testFindResult Visible 토글"
  - "Task 1 단독 커밋(XAML only) + Task 2+3 합산 커밋(3 cs files) — 빌드는 Task 3 완료 후 검증"
metrics:
  duration: ~25min
  completed_date: "2026-04-25"
  tasks_completed: 3
  tasks_total: 4
  files_changed: 4
uat_status: pending_uat
---

# Phase 13 Plan 02: Runtime TryFindDatum Test UI Summary

**One-liner:** Datum 티칭 완료 후 현재/로드 이미지에서 TryFindDatum을 수동 실행하는 Test Find 버튼 + 주황 3px 십자 오버레이 + 수치 라벨 피드백 UI 추가 (Gap-4 완결).

---

## UAT Status: PENDING

Tasks 1-3 완료, Task 4 (SIMUL_MODE UAT checkpoint) 사용자 검증 대기 중.

---

## Files Changed

| File | Type | Description |
|------|------|-------------|
| `WPF_Example/UI/ContentItem/MainView.xaml` | modified | btn_testFindDatum Button + label_testFindResult Label 추가 |
| `WPF_Example/UI/ContentItem/MainView.xaml.cs` | modified | BtnTestFindDatum_Click + AskTestImageSource 핸들러 추가 |
| `WPF_Example/UI/ContentItem/MainResultViewerControl.xaml.cs` | modified | SetDatumFindResultOverlay/ClearDatumFindResultOverlay + _datumFindResultOverlay 필드 + Render 분기 추가 |
| `WPF_Example/Halcon/Display/HalconDisplayService.cs` | modified | RenderDatumFindResult 메서드 추가 |

---

## Gap-4 Coverage Mapping

| Decision ID | 구현 위치 | 내용 |
|-------------|-----------|------|
| D-05 | `MainView.xaml` `btn_testFindDatum` + `MainView.xaml.cs` `BtnTestFindDatum_Click` | 런타임 TryFindDatum 테스트 진입 버튼 (Datum teach 완료 후 가드) |
| D-06 | `MainView.xaml.cs` `AskTestImageSource` | 3-way 이미지 소스 선택 (현재 이미지 / OpenFileDialog / 취소) |
| D-07 | `HalconDisplayService.cs` `RenderDatumFindResult` + `MainResultViewerControl.xaml.cs` `SetDatumFindResultOverlay` | 성공 주황 3px 20px half 십자 + 좌표 WriteString + LimeGreen label |
| D-08 | `MainView.xaml.cs` `BtnTestFindDatum_Click` 실패 분기 + `ClearDatumFindResultOverlay` | 실패 Red label + 오버레이 clear |

---

## New Public API Catalog

| 메서드 | 파일 | 시그니처 | 설명 |
|--------|------|----------|------|
| `BtnTestFindDatum_Click` | MainView.xaml.cs | `private void BtnTestFindDatum_Click(object sender, RoutedEventArgs e)` | Test Find 버튼 Click 핸들러 |
| `AskTestImageSource` | MainView.xaml.cs | `private HImage AskTestImageSource()` | 이미지 소스 3-way 선택 (현재/파일/취소) |
| `SetDatumFindResultOverlay` | MainResultViewerControl.xaml.cs | `public void SetDatumFindResultOverlay(DatumConfig datum)` | 주황 십자 오버레이 주입 + Render 트리거 |
| `ClearDatumFindResultOverlay` | MainResultViewerControl.xaml.cs | `public void ClearDatumFindResultOverlay()` | 주황 십자 오버레이 clear + Render 트리거 |
| `RenderDatumFindResult` | HalconDisplayService.cs | `public void RenderDatumFindResult(HWindow window, DatumConfig datum)` | 주황 3px 20px half 십자 + 좌표 WriteString |

---

## UX Literal Catalog

| 리터럴 | 위치 | 조건 |
|--------|------|------|
| `"TryFind OK — RefOrigin=({0:F1}, {1:F1}), Angle={2:F3} rad"` | BtnTestFindDatum_Click 성공 분기 | ok=true, LimeGreen (#FF4ADE80) |
| `"TryFind FAIL — " + (error ?? "unknown")` | BtnTestFindDatum_Click 실패 분기 | ok=false, Red (#FFF87171) |
| `"Datum 티칭이 완료된 후 테스트 가능합니다."` | BtnTestFindDatum_Click 진입 가드 | datum==null || !IsConfigured || !LastTeachSucceeded |
| `"테스트 이미지를 선택하세요.\n\n[예] 현재 이미지로 테스트\n[아니오] 다른 파일 선택...\n[취소] 취소"` | AskTestImageSource (hasCurrent=true) | YesNoCancel |
| `"Image Files (*.bmp;*.jpg;*.jpeg;*.png;*.tif;*.tiff)\|..."` | AskTestImageSource OpenFileDialog Filter | LoadAndDisplay 동일 패턴 |
| `"TryFind ({row:F1}, {col:F1})"` | RenderDatumFindResult WriteString | 주황 십자 위 좌표 텍스트 |

---

## Build Result

```
msbuild Debug/x64 → exit 0
Output: DatumMeasurement.exe
Warnings (기존, Plan 02 scope 밖):
  - VirtualCamera.cs CS0162 (unreachable code)
  - VisionAlgorithmService.cs CS0219 (unused variable)
  - MSB3884 (MinimumRecommendedRules.ruleset not found)
신규 warning (Plan 02 수정 파일): 0
```

---

## Commits

| Task | Hash | Message |
|------|------|---------|
| Task 1 (XAML) | 3dcdb10 | feat(phase-13-02): add btn_testFindDatum + label_testFindResult to canvas toolbar |
| Task 2+3 (코드) | ab504a9 | feat(phase-13-02): BtnTestFindDatum_Click + RenderDatumFindResult + SetDatumFindResultOverlay |

---

## UAT Outcome (Task 4 — PENDING)

Task 4: SIMUL_MODE UAT checkpoint — 6 시나리오 검증 대기 중.

| # | 시나리오 | 기대 결과 | 실제 결과 |
|---|---------|-----------|-----------|
| 1 | TwoLineIntersect 성공 (현재 이미지) | LimeGreen label + 주황 십자 | PENDING |
| 2 | CircleTwoHorizontal Find (다른 파일) | halconViewer 교체 + LimeGreen label + 주황 십자 | PENDING |
| 3 | VerticalTwoHorizontal 실패 케이스 | Red label + 주황 십자 clear | PENDING |
| 4 | 미티칭 상태 가드 | CustomMessageBox "Datum 티칭이 완료된 후..." | PENDING |
| 5 | Cancel 경로 | 조용히 닫힘, 상태 변경 없음 | PENDING |
| 6 | 현재 이미지 없음 edge case | 2-way (파일선택/취소) 또는 OpenFileDialog 직행 | PENDING |

---

## Deviations from Plan

Plan Adherence: All tasks executed as specified.

- Task 1 단독 빌드 시 CS1061 예상 (BtnTestFindDatum_Click 핸들러 미존재) — Plan에 명시된 허용 사항. Task 3 완료 후 누적 빌드 exit 0 확인.
- 워크트리 경로 빌드 시 MC3074 에러(PropertyTools.Wpf, WPF.MDI DLL 참조 미해결) — DLL이 `bin/x64/Debug/` 상대 경로로 등록되어 있어 워크트리에서 원본 bin 디렉토리 불가용. 원본 프로젝트 경로(`C:\Info\Project\DataMeasurement`)에서 빌드 시 exit 0 확인. 이는 Phase 12 이전부터 존재하는 환경 제약으로 Plan 02 scope 밖.

---

## Known Stubs

없음 — 모든 신규 메서드가 실제 DatumFindingService.TryFindDatum 호출 및 HalconDisplayService 렌더로 완전 구현됨.

---

## Self-Check

- [x] `WPF_Example/UI/ContentItem/MainView.xaml` 수정 존재 (btn_testFindDatum, label_testFindResult)
- [x] `WPF_Example/UI/ContentItem/MainView.xaml.cs` 수정 존재 (BtnTestFindDatum_Click, AskTestImageSource)
- [x] `WPF_Example/UI/ContentItem/MainResultViewerControl.xaml.cs` 수정 존재 (SetDatumFindResultOverlay, ClearDatumFindResultOverlay, _datumFindResultOverlay, Render 분기)
- [x] `WPF_Example/Halcon/Display/HalconDisplayService.cs` 수정 존재 (RenderDatumFindResult)
- [x] Task 1 커밋 3dcdb10 존재
- [x] Task 2+3 커밋 ab504a9 존재
- [x] msbuild Debug/x64 exit 0 (원본 프로젝트 경로 기준)
- [x] 기존 SetDatumOverlay / RenderDatumOverlay 시그니처 untouched
- [x] STATE.md / ROADMAP.md 수정 없음 (orchestrator 전담)
- [ ] UAT Task 4 — PENDING (사용자 검증 대기)

## Self-Check: PASSED (UAT pending)
