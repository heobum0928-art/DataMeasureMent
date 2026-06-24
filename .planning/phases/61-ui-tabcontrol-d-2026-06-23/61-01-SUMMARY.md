---
phase: 61-ui-tabcontrol-d-2026-06-23
plan: "01"
subsystem: UI
tags: [tray-vision, align, shape-match, wpf, airspace, thin-facade]
one_liner: "Tray 비전 UserControl — airspace-safe 2-컬럼 레이아웃 + Camera.Grab/Live/Stop + 2-ROI Shape 티칭 + Matcher.Run X/Y 결과 표시, 공유 뷰어 AttachSharedViewer 계약"
dependency_graph:
  requires:
    - "Phase 58: EthernetVisionHandler.Handle.Camera (EthernetAlignCamera)"
    - "Phase 59: EthernetVisionHandler.Handle.Matcher (AlignShapeMatchService.TryTeach/Run/HasTemplate)"
    - "MainResultViewerControl (LoadImage/StartRectangleDrawing/CommitActiveRectangle/RectDrawingCompleted/CurrentImage)"
    - "RoiDefinition (Row1/Column1/Row2/Column2)"
    - "EEthernetVisionMode.Tray"
  provides:
    - "TrayVisionView.AttachSharedViewer(MainResultViewerControl) — Plan 61-03 소비"
    - "TrayVisionView.xaml — airspace-safe 뷰어 호스트 ViewerHostBorder"
  affects:
    - "Plan 61-03: csproj 등록 + MainWindow 배선 의존"
tech_stack:
  added: []
  patterns:
    - "2-컬럼 Grid airspace-safe 분리 (CalibrationWindow 패턴 차용)"
    - "thin facade — 서비스 호출 위임, UI 로직 0"
    - "2-ROI 슬롯 순서 관리 (DrawRoi1→DrawRoi2→Teach 시퀀스)"
    - "공유 뷰어 주입 계약 AttachSharedViewer"
key_files:
  created:
    - WPF_Example/Custom/UI/TrayVisionView.xaml
    - WPF_Example/Custom/UI/TrayVisionView.xaml.cs
  modified: []
decisions:
  - "2-ROI 슬롯 채우기: DrawRoi1→StartRectangleDrawing(슬롯1), DrawRoi2→CommitActiveRectangle(슬롯1확정)+StartRectangleDrawing(슬롯2), Teach→CommitActiveRectangle(슬롯2확정) 순서로 결정적으로 2개 ROI 수거"
  - "Live 스트림 루프는 Camera.Live() 호출만 — 실제 프레임 push 루프는 Camera 내부에 위임 (초기 트리거만 제공)"
  - "lbl_status: 미연결/대기/LIVE/검사중 4-상태, RefreshStatus 로 초기화"
metrics:
  duration: "149s"
  completed_date: "2026-06-24"
  tasks_completed: 2
  files_created: 2
  files_modified: 0
---

# Phase 61 Plan 01: TrayVisionView 신규 생성 Summary

Tray 비전 UserControl — airspace-safe 2-컬럼 레이아웃 + Camera.Grab/Live/Stop + 2-ROI Shape 티칭 + Matcher.Run X/Y 결과 표시, 공유 뷰어 AttachSharedViewer 계약

## Tasks Completed

| Task | Name | Commit | Files |
|------|------|--------|-------|
| 1 | TrayVisionView.xaml — airspace-safe 레이아웃 | 926eb8d | WPF_Example/Custom/UI/TrayVisionView.xaml |
| 2 | TrayVisionView.xaml.cs — 서비스 배선 facade | f5fcd1c | WPF_Example/Custom/UI/TrayVisionView.xaml.cs |

## Deviations from Plan

None — 플랜을 정확히 따라 실행.

## Verification Results

### Task 1 (XAML) automated grep — PASS
- `x:Name="ViewerHostBorder"` 존재
- `GrabButton_Click` 존재
- `TeachButton_Click` 존재
- `RunButton_Click` 존재

### Task 2 (코드비하인드) automated grep — PASS
- `EthernetVisionHandler.Handle.Camera.Grab` 존재
- `Matcher.TryTeach` 존재
- `Matcher.Run` 존재
- `public void AttachSharedViewer` 존재
- `EEthernetVisionMode.Tray` 존재

### 추가 acceptance criteria
- `namespace ReringProject.Custom.UI` / `partial class TrayVisionView : UserControl` 확인
- `ViewerHostBorder.Child = viewer` 확인
- `Camera.Live()` / `Camera.Stop()` 확인
- `try { ... } catch { }` 전 서비스 호출 감싸짐 확인
- `//260624 hbk Phase 61` 주석 확인

## Anti-Goal 확인

- 기존 파일 수정: 0 (신규 2파일만 생성)
- csproj 등록 미수행 (Wave 1 의도적 분리 — Plan 61-03 에서 일괄)
- MainResultViewerControl / HWindowControlWPF XAML 직접 인스턴스화 없음
- C# 8+ 구문 없음, 삼항 연산자 `?:` 없음

## Known Stubs

없음 — 이 플랜의 출력은 새 파일 2개. 실제 Grab/Run 은 런타임 카메라/매처 서비스에 위임됨. csproj 미등록이라 단독 컴파일 불가 (의도적 wave 분리, Plan 61-03 이 해소).

## Self-Check: PASSED

- `WPF_Example/Custom/UI/TrayVisionView.xaml` 존재 확인
- `WPF_Example/Custom/UI/TrayVisionView.xaml.cs` 존재 확인
- commit 926eb8d 존재 확인 (Task 1)
- commit f5fcd1c 존재 확인 (Task 2)
