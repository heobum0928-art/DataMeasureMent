---
phase: quick-260623-jd6
plan: 01
subsystem: UI/HalconViewer
tags: [refactor, magic-const, qual-01]
key-files:
  modified:
    - WPF_Example/UI/ContentItem/HalconViewerControl.xaml.cs
decisions:
  - MinViewPartSize(20.0)을 MinDraftRoiSize(20.0)와 별도 const로 분리 — SetImagePart 화면 최소 픽셀과 드래그 ROI 최소 픽셀은 의미가 다름
  - 지역 const(halfSize=60.0, cornerHit=10.0)를 제거하고 클래스 레벨 const로 일원화
  - 1.0 리터럴 9곳은 문맥 다양(정규화·최소크기·SetPart 픽셀 오프셋 등) → 계획대로 보류
metrics:
  duration: ~10min
  completed: 2026-06-23
---

# Quick 260623-jd6: HalconViewerControl 매직넘버 const화

한 줄 요약: 6개 의미 명확 매직넘버를 PascalCase private const로 추출, 값·분기·API 완전 불변, 빌드 0 errors.

## 추출된 const (클래스 상단 line 26-31)

| const 이름 | 값 | 적용 사용처 (라인 수) |
|---|---|---|
| `MinDraftRoiSize` | 20.0 | UpdateDraftRoi — 드래그 ROI 최소 크기 (2곳) |
| `DraftDefaultHalfSize` | 60.0 | CreateDefaultDraftRoi — 기본 반경 (4곳, 지역 const 제거) |
| `CornerHitThreshold` | 10.0 | GetDraftRoiHitType — 코너 히트 거리 (4곳, 지역 const 제거) |
| `PanMarginScale` | 0.75 | SetImagePart — 팬 마진 비율 (2곳) |
| `RoiClickTolerancePixels` | 3.0 | ViewerHost_HMouseUp — ROI 클릭 판정 (1곳) |
| `MinViewPartSize` | 20.0 | SetImagePart — 화면 최소 뷰 파트 픽셀 (2곳) |

## 보류한 리터럴

- `1.0` (9곳): 문맥이 각각 다름 — 정규화 기준, SetPart 픽셀 오프셋(-1.0), `GetImagePart` min 보정, centerRow/Column 공식 등. 잘못 묶으면 의미 혼동 위험 → 계획 판단 규칙대로 보류.
- `5.0` (ResizeDraftRoi): 이미 지역 `const double minSize = 5.0;` 존재 → 그대로 둠.

## 빌드 결과

- MSBuild Debug/x64: **0 errors, warnings는 기존 동일 (CS0618·CS0162·MSB3884)**
- `DatumMeasurement -> bin\x64\Debug\DatumMeasurement.exe` 생성 확인

## 공개 API / HALCON 불변 확인

- `LoadImage`, `SetRois`, `Render`, `Dispose`, `StartRectangleDrawing`, `CommitActiveRectangle`, `CancelActiveDrawing`, `FitImage`, `SetSelectedRoi`, `SetInspectionOverlays`, `SetDisplayMessages`, `UpdateDisplayState` — 시그니처 변경 없음
- `HOperatorSet.*`, `ViewerHost.HalconWindow.*` 호출 라인 — diff에 미등장
- 이벤트 핸들러 로직, try-catch 구조, `_isXxx` 상태 플래그 — 불변

## Deviations from Plan

없음 — 계획대로 정확히 실행.

## Self-Check: PASSED

- `WPF_Example/UI/ContentItem/HalconViewerControl.xaml.cs` 수정 확인
- commit `70b6cc1` 존재 확인
- 6개 const 선언·치환 grep 확인
- 빌드 0 errors 확인
