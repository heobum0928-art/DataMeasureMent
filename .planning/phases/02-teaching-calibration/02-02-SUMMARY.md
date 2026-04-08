---
phase: 02-teaching-calibration
plan: 02
subsystem: ui
tags: [wpf, halcon, roi, calibration, canvas]

requires:
  - phase: 02-01
    provides: "FAIConfig ROI 필드, ToRoiDefinition(), selectedRoiId 렌더링 파이프라인"
provides:
  - "캔버스 ROI 편집 툴바 (Rect/Polygon/Calibrate)"
  - "Rect ROI 드래그 드로잉 + FAIConfig 저장"
  - "Polygon ROI 점 클릭 생성 + 우클릭 완성"
  - "2점 캘리브레이션 mm/pixel 플로우"
affects: [phase-03, measurement, teaching]

tech-stack:
  added: []
  patterns: [ECanvasMode state machine, mouse event-driven drawing]

key-files:
  created: []
  modified:
    - WPF_Example/UI/ContentItem/MainView.xaml
    - WPF_Example/UI/ContentItem/MainView.xaml.cs
    - WPF_Example/Halcon/Display/HalconDisplayService.cs
    - WPF_Example/UI/ContentItem/MainResultViewerControl.xaml.cs

key-decisions:
  - "ECanvasMode enum으로 Idle/RectDraw/PolygonDraw/Calibrate 상태 관리"
  - "Polygon 완성은 우클릭, 취소는 Escape 키"
  - "캘리브레이션은 2점 클릭 → mm 입력 다이얼로그 → mm/pixel 계산"

patterns-established:
  - "Canvas toolbar: Border 내 ToggleButton 스타일 (RoiToggleButtonStyle)"
  - "Drawing mode: ECanvasMode + ExitCanvasMode() cleanup 패턴"

requirements-completed: [TCH-01, ALG-03]

duration: 15min
completed: 2026-04-08
---

# Phase 02-02: ROI 편집 툴바 + 캘리브레이션 Summary

**캔버스 상단 ROI 편집 툴바(Rect/Polygon/Calibrate) + 드래그 ROI 드로잉 + 2점 mm/pixel 캘리브레이션 플로우 구현**

## Performance

- **Duration:** ~15 min
- **Started:** 2026-04-08T12:45:00+09:00
- **Completed:** 2026-04-08T12:50:00+09:00
- **Tasks:** 4 (combined into 1 atomic commit)
- **Files modified:** 4

## Accomplishments
- 캔버스 상단에 Rect ROI / Polygon ROI / Calibrate 3개 버튼 툴바 추가
- Rect ROI: 마우스 드래그로 사각형 ROI 그리기 → FAIConfig에 저장
- Polygon ROI: 점 클릭으로 다각형 생성, 우클릭으로 완성 → FAIConfig.PolygonPoints에 저장
- 2점 캘리브레이션: 2점 클릭 → 픽셀 거리 계산 → mm 입력 → CameraSlaveParam.PixelResolution에 저장
- Escape 키로 모든 드로잉 모드 취소 + 툴바 idle 복귀

## Task Commits

1. **Tasks 1-4: Canvas toolbar + ROI drawing + Calibration** - `aa4ae83` (feat)

## Files Created/Modified
- `WPF_Example/UI/ContentItem/MainView.xaml` - RoiToggleButtonStyle + canvasToolbar + 3 buttons + hint labels
- `WPF_Example/UI/ContentItem/MainView.xaml.cs` - ECanvasMode, Rect/Polygon/Calibrate handlers, ExitCanvasMode, Escape key
- `WPF_Example/Halcon/Display/HalconDisplayService.cs` - RenderPolygon, RenderPolygonPoints methods
- `WPF_Example/UI/ContentItem/MainResultViewerControl.xaml.cs` - StartRectangleDrawing, CommitActiveRectangle, SetPolygonDraft, ClearPolygonDraft, mouse handlers

## Decisions Made
- ECanvasMode enum으로 4가지 모드 상태 관리 (Idle, RectDraw, PolygonDraw, Calibrate)
- Polygon 완성을 우클릭으로, 취소를 Escape로 통일
- 캘리브레이션 mm 입력에 표준 InputBox 다이얼로그 사용

## Deviations from Plan
None - 4개 태스크가 하나의 커밋으로 합쳐짐 (에이전트 에러로 인한 atomic commit 대신 단일 커밋)

## Issues Encountered
- 에이전트가 SUMMARY 생성 전 내부 에러로 종료됨 → 오케스트레이터가 SUMMARY 생성

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- ROI 편집 + 캘리브레이션 UI 완성 → Phase 3에서 Halcon 에지 측정 알고리즘 연동 가능
- 사용자 검증 필요: 실제 이미지에서 ROI 드로잉, 캘리브레이션 플로우 테스트

---
*Phase: 02-teaching-calibration*
*Completed: 2026-04-08*
