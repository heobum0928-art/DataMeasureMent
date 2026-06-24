---
phase: 61-ui-tabcontrol-d-2026-06-23
plan: "02"
subsystem: UI
tags: [bottom-vision, align, picker-calibration, AV-08, AV-05]
dependency_graph:
  requires:
    - 61-01-SUMMARY.md  # TrayVisionView 패턴 참조
    - EthernetVisionHandler  # Camera/Matcher/PickerCal 싱글턴
    - MainResultViewerControl  # 공유 뷰어 계약
  provides:
    - BottomVisionView  # TabControl [Bottom 비전] 탭 UI
    - AttachSharedViewer(MainResultViewerControl)  # Plan 61-03 소비
  affects: []
tech_stack:
  added: []
  patterns:
    - Thin facade (서비스 위임 — 로직 0)
    - Shared viewer injection (D-03 계약)
    - CircleDrawingCompleted event subscription (중복 방지: -= then +=)
    - PickerCal Reset→TryAddStep×N→TryComputePickerCenter 3-step 캘 패턴
key_files:
  created:
    - WPF_Example/Custom/UI/BottomVisionView.xaml
    - WPF_Example/Custom/UI/BottomVisionView.xaml.cs
  modified: []
decisions:
  - "CalAddStepButton_Click: LoadImage before Dispose (LoadImage clones internally — safe)"
  - "CircleDrawingCompleted: -= then += in AttachSharedViewer to prevent duplicate subscription"
  - "FormatAlignResult: HasTheta branch (Bottom=true → X/Y/Theta/Score; else X/Y/Score)"
  - "RefreshStatus: PickerCal null-safe via null-check before StepCount access"
metrics:
  duration: "3m 30s"
  completed_date: "2026-06-24"
  tasks_completed: 2
  files_created: 2
  files_modified: 0
---

# Phase 61 Plan 02: BottomVisionView 신규 생성 Summary

**One-liner:** Bottom Align 비전 뷰 (Tray facade + Theta 결과 표시 + PickerCal 36-스텝 피커센터 캘)를 `BottomVisionView` UserControl 2파일로 신규 생성.

## What Was Built

`WPF_Example/Custom/UI/BottomVisionView.xaml` + `.xaml.cs` 2 신규 파일. Phase 61 TabControl 의 [Bottom 비전] 탭에 배치될 UserControl.

**구조:**
- XAML: 2-컬럼 airspace-safe Grid (좌측 400px 컨트롤 패널 + 우측 `ViewerHostBorder`)
- 좌측 컨트롤 패널: 헤더 / 상태 라벨 / 카메라 툴바 / 티칭 GroupBox / 결과 GroupBox / **캘리브레이션 GroupBox (Bottom 전용)**
- 우측: `<Border x:Name="ViewerHostBorder"/>` — XAML 에서 뷰어 미선언, `AttachSharedViewer` 로 외부 주입

**코드비하인드 핵심 계약:**
- `public void AttachSharedViewer(MainResultViewerControl viewer)`: ViewerHostBorder.Child 배치 + CircleDrawingCompleted 구독 (-= 후 +=)
- `VIEW_MODE = EEthernetVisionMode.Bottom`
- Grab/Live/Stop → `EthernetVisionHandler.Handle.Camera` 위임
- 2-ROI Teach → `Matcher.TryTeach(..., EEthernetVisionMode.Bottom, ...)`
- Run → `Matcher.Run(_viewer.CurrentImage, VIEW_MODE)` + `FormatAlignResult` (HasTheta=true → Theta 표시)
- CalReset → `PickerCal.Reset()`
- CalDrawRoi → `_viewer.StartCircleDrawing()` (좌표는 `OnCalCircleDrawn` 이벤트로 수거)
- CalAddStep → `Camera.Grab()` + `_viewer.LoadImage(img)` + `PickerCal.TryAddStep(img, row, col, radius, out err)` + `img.Dispose()`
- CalCompute → `PickerCal.TryComputePickerCenter(out r, out c, out rad, out err)` → `lbl_pickerCenter` 표시

## Verification

| Check | Result |
|-------|--------|
| PickerCal.Reset | PASS |
| PickerCal.TryAddStep | PASS |
| PickerCal.TryComputePickerCenter | PASS |
| Matcher.Run | PASS |
| public void AttachSharedViewer | PASS |
| EEthernetVisionMode.Bottom | PASS |

## Deviations from Plan

없음 — 플랜 그대로 실행.

## Known Stubs

없음. `lbl_result` 는 실제 `Matcher.Run` 결과를 표시, `lbl_pickerCenter` 는 실제 `PickerCal.TryComputePickerCenter` 결과를 표시. 위임 서비스(PickerCal/Matcher)가 SIMUL_MODE 폴백 포함하여 실 데이터 없이도 동작.

## Threat Flags

없음 — 신규 네트워크/파일 경계 없음. 로컬 UI ↔ 로컬 인-프로세스 싱글턴만. T-61-03/T-61-04 전부 try-catch + null 가드로 mitigate.

## Self-Check: PASSED

| Item | Status |
|------|--------|
| WPF_Example/Custom/UI/BottomVisionView.xaml | FOUND |
| WPF_Example/Custom/UI/BottomVisionView.xaml.cs | FOUND |
| Commit b0c225a (Task 1 XAML) | FOUND |
| Commit 6a4d776 (Task 2 code-behind) | FOUND |
