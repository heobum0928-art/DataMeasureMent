---
phase: 18
plan: 02
subsystem: UI / TeachDatum Context Menu
tags: [co-04, context-menu, teach-datum, roi-redraw, wpf]
dependency_graph:
  requires: []
  provides: [ROI 다시 그리기 컨텍스트 메뉴 (CO-04)]
  affects:
    - WPF_Example/UI/ContentItem/MainResultViewerControl.xaml
    - WPF_Example/UI/ContentItem/MainResultViewerControl.xaml.cs
    - WPF_Example/UI/ContentItem/MainView.xaml.cs
tech_stack:
  added: []
  patterns:
    - IsTeachDatumMode bool 프로퍼티로 컨텍스트 메뉴 게이팅
    - RoiRedrawRequested System.Action<string> 이벤트 — RoiDeleteRequested 패턴과 동일
    - UpdateContextMenuState HitTestRoiAtPoint 기반 Datum ROI hit-test 분기
key_files:
  created: []
  modified:
    - WPF_Example/UI/ContentItem/MainResultViewerControl.xaml
    - WPF_Example/UI/ContentItem/MainResultViewerControl.xaml.cs
    - WPF_Example/UI/ContentItem/MainView.xaml.cs
decisions:
  - D-07: 노출 조건 = IsTeachDatumMode ON + HitTestRoiAtPoint(Datum.*) 통과
  - D-08: 레이블 = "ROI 다시 그리기" (한국어)
  - D-09: 동작 = ClearDatumRoiFields → SetDatumOverlay → PublishDatumRoiCandidates
  - D-10: 적용 대상 = Datum ROI만 (Id.StartsWith("Datum.") 가드)
metrics:
  duration_minutes: 15
  completed_date: "2026-05-05"
  tasks_completed: 2
  files_modified: 3
---

# Phase 18 Plan 02: ROI 다시 그리기 컨텍스트 메뉴 Summary

**One-liner:** TeachDatum 모드 + Datum ROI 우클릭 시 "ROI 다시 그리기" 메뉴 노출 — ClearDatumRoiFields로 치수 0 리셋하는 escape hatch (CO-04)

## What Was Built

### Task 1: MainResultViewerControl.xaml.cs — 프로퍼티 + 이벤트 + 분기 + 핸들러

- `IsTeachDatumMode` bool 프로퍼티 추가 (MainView.TeachDatumButton_Click에서 true/false 설정)
- `RoiRedrawRequested` `System.Action<string>` 이벤트 추가 (인자 = hit-test 통과한 roiId)
- `UpdateContextMenuState()` 끝 부분에 RedrawRoiMenuItem 가시성 결정 블록 추가:
  - `IsTeachDatumMode` + `HitTestRoiAtPoint` + `Id.StartsWith("Datum.")` 조합
- `RedrawRoiMenuItem_Click` 핸들러 추가 → `RoiRedrawRequested?.Invoke(hitRoi.Id)`

Commit: `bc72e77`

### Task 2: XAML + MainView 연결

- `MainResultViewerControl.xaml`: `RedrawRoiMenuItem` MenuItem 삽입 (DeleteRoiMenuItem 바로 다음, `Visibility="Collapsed"` 초기값)
- `MainView.xaml.cs` 생성자: `RoiRedrawRequested` 구독 람다 — `ClearDatumRoiFields` + `SetDatumOverlay(false)` + `PublishDatumRoiCandidates`
- `TeachDatumButton_Click` ON 경로: `halconViewer.IsTeachDatumMode = true`
- 종료 경로 3개 모두 `IsTeachDatumMode = false` 설정:
  - ValidateRoiPresence 실패 early return
  - else (수동 해제 = 취소)
  - `InvokeTryTeachDatum` img==null early return + 정상 종료

Commit: `387ffe0`

## Commits

| Task | Commit | Message |
|------|--------|---------|
| 1 | bc72e77 | feat(18-02): add IsTeachDatumMode + RoiRedrawRequested + UpdateContextMenuState branch + Click handler |
| 2 | 387ffe0 | feat(18-02): wire RedrawRoiMenuItem XAML + MainView RoiRedrawRequested subscription + IsTeachDatumMode ON/OFF |

## Verification

| Check | Result |
|-------|--------|
| `grep -c "IsTeachDatumMode" MainResultViewerControl.xaml.cs` ≥ 2 | 2 (선언 + UpdateContextMenuState) |
| `grep -c "RoiRedrawRequested" MainResultViewerControl.xaml.cs` ≥ 2 | 2 (선언 + Click 핸들러) |
| `grep -c "RedrawRoiMenuItem" MainResultViewerControl.xaml.cs` ≥ 2 | 3 (UpdateContextMenuState + Click 핸들러 선언) |
| `grep -c "RedrawRoiMenuItem" MainResultViewerControl.xaml` ≥ 1 | 2 (x:Name + Click) |
| `grep -c "ROI 다시 그리기" MainResultViewerControl.xaml` = 1 | 1 |
| `grep -c "RoiRedrawRequested" MainView.xaml.cs` ≥ 1 | 1 |
| `grep -c "IsTeachDatumMode" MainView.xaml.cs` ≥ 2 | 4 (true 1 + false 3) |
| msbuild Debug/x64 PASS (오류 0) | PASS — 기존 경고 3개 유지, 신규 없음 |

## Deviations from Plan

None — 플랜 그대로 실행됨.

## Known Stubs

None.

## Threat Flags

T-18-02-01 (Tampering / ClearDatumRoiFields): 계획된 위협. `_editingDatum != null` null 가드 및 `IsTeachDatumMode` 게이팅으로 mitigate 적용 완료.

## Self-Check: PASSED

- `WPF_Example/UI/ContentItem/MainResultViewerControl.xaml` FOUND
- `WPF_Example/UI/ContentItem/MainResultViewerControl.xaml.cs` FOUND
- `WPF_Example/UI/ContentItem/MainView.xaml.cs` FOUND
- Commit bc72e77 FOUND
- Commit 387ffe0 FOUND
