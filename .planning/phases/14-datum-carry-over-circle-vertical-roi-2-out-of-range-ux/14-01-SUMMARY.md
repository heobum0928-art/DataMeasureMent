---
phase: 14-datum-carry-over-circle-vertical-roi-2-out-of-range-ux
plan: 01
status: complete
date: 2026-04-27
requirements: [SPEC-14-Req-1]
commits:
  - 45b3771 feat(14-01): MainResultViewerControl Datum Circle Edit handles + resize support
  - 9148976 feat(14-01): MainView HandleDatumRoiResize + Move 회귀 Dispatcher defer fix
files_modified:
  - WPF_Example/UI/ContentItem/MainResultViewerControl.xaml.cs
  - WPF_Example/UI/ContentItem/MainView.xaml.cs
key-files:
  created: []
  modified:
    - WPF_Example/UI/ContentItem/MainResultViewerControl.xaml.cs
    - WPF_Example/UI/ContentItem/MainView.xaml.cs
---

# Plan 14-01 Summary — Datum Circle Move 회귀 + Resize 미동작 fix

## Goal Achieved

Phase 13 UAT carry-over Test 1 결함 (Move 회귀 + Circle Datum ROI Resize 미동작) 을 closure.
Datum 3 알고리즘 모두에서 ROI 인터랙션 (이동/resize) 후 자동 재티칭 → 검출 결과 즉시 갱신
이라는 Phase 13 UX 약속을 Circle 케이스까지 마무리.

## What Was Built

### Task 1 — MainResultViewerControl.xaml.cs (commit 45b3771)
- **RenderEditHandles:** `_isEditMode || _datumRoiCandidates` 게이트 + Datum Circle fallback.
  핸들 색 cyan→yellow (D-01 Datum 시각 일관).
- **MouseDown resize entry:** "Datum 은 리사이즈 미지원" 가드 제거. Edit 모드 OR Datum 후보
  존재 시 `HitTestEditHandle` 활성. N/S/E/W 4 핸들 모두 단일 `ResizeHandle.CircleRadius` enum 으로
  동일 동작 보장 (D-02).
- **MouseMove resize:** `_rois.FirstOrDefault` → `_datumRoiCandidates` lookup fallback.
- **MouseUp resize:** 동일 fallback + `RoiGeometryChanged` 발행 (D-04 단일 이벤트 확장,
  신규 이벤트 신설 X).

### Task 2 — MainView.xaml.cs (commit 9148976)
- **HalconViewer_RoiGeometryChanged:** Datum.* RoiId 면 FAI 분기 진입 전에
  `HandleDatumRoiResize` 라우팅 후 return.
- **HandleDatumRoiResize 신규:** Datum.Circle 절대 좌표 직접 대입
  (CircleROI_Row/Col/Radius) + write-back 이중 신호 (RaisePropertyChanged + RefreshParamEditor +
  SetDatumOverlay) + `Dispatcher.BeginInvoke(Background)` 자동 재티칭 defer.
- **HandleDatumRoiMove (Move 회귀 fix, D-03):** 자동 재티칭 호출
  (`InvokeTryTeachDatumForEdit` + `PublishDatumRoiCandidates` + `UpdateDatumRefCoordsLabel`) 을
  `Dispatcher.BeginInvoke(Background)` 람다로 defer (Phase 13-07 Fix A 패턴 재사용).
- **진단 로깅:** `HalconViewer_RoiMoveCompleted` 진입 시 Datum.* 이면 id/dr/dc Trace 로그.
  `InvokeTryTeachDatumForEdit` 진입/종료에 IsConfigured/LastTeachSucceeded Trace 로그
  (Phase 14-05 verify PASS 시 제거 가능).

## Verification

- **Build:** `MSBuild Debug/x64` exit 0, 0 errors, 신규 warning 0 (Task 1 / Task 2 각각 build).
- **SIMUL_MODE UAT (Task 3):** 사용자 육안 검증 모든 시나리오 PASS:
  - Scenario 3 Move — Circle ROI 드래그 이동 시 검출 원/raw 점 즉시 갱신 (자동 재티칭 발동)
  - Scenario 4 Resize E 핸들 — 노란 사각형 표시 + 드래그 시 반경 변경 + 검출 원 새 반경 갱신
  - Scenario 5 Resize N/S/W 핸들 — 모두 동일 동작
  - Scenario 6 회귀 없음 — TwoLineIntersect Line1/Line2 ROI 드래그 이동 기존 동작 유지

## Acceptance Criteria

Plan acceptance criteria (frontmatter `truths`):
- [x] Circle Datum ROI 드래그 이동 자동 재티칭 → 검출 원/raw 점 즉시 갱신 (UAT Scenario 3)
- [x] Circle Datum ROI Edit 모드 N/S/E/W 4 노란 사각형 핸들 시각 표시 (UAT Scenario 4)
- [x] 4 핸들 어디든 잡고 드래그 시 반경 변경 (UAT Scenario 4-5)
- [x] Resize 완료 시 자동 재티칭 → 검출 원 새 반경 갱신 (UAT Scenario 4-5)

## Notable Deviations

- 빌드 환경: 계획서는 VS2017 Professional 경로 명시되어 있으나, 실제 환경은 VS2017 Community.
  MSBuild 15.0 동일 버전이므로 .NET Framework 4.8 빌드 동일 산출. 향후 plans 도 동일 경로 차이
  무시 가능.
- `HandleDatumRoiMove` 의 `RaisePropertyChanged` + `RefreshParamEditor` + `SetDatumOverlay`
  3-step write-back 은 BeginInvoke 외부에 보존 — UI 즉시 갱신 (PropertyGrid 동기화) 보장,
  자동 재티칭만 defer.

## Requirements Mapping

- **SPEC-14-Req-1** (Circle Datum ROI Move 회귀 + Resize 미동작 fix) — COVERED.
  - Acceptance criteria 1-4 (Move 자동 재티칭 / N/S/E/W 4 핸들 시각 + 동작 / Resize 자동 재티칭) 충족.

## Next

- Plan 14-02 (TwoLineIntersect 직각성 게이트) — Wave 1 동시 실행 가능 (다른 파일).

