---
phase: quick-260907-fkh
plan: 01
subsystem: UI/Device (DeviceSelector 카메라 미리보기)
tags: [wpf, camera-preview, zoom, wheel-input, mvvm]
dependency-graph:
  requires: []
  provides:
    - "DeviceSelector 미리보기 마우스 휠 줌 (Ctrl 불필요, 커서 앵커)"
    - "PreviewZoomCalculator 순수 줌 계산 (배율/오프셋)"
    - "VirtualCamera.RenderCenterLine(dc, dViewScale) 화면 두께 고정 오버로드"
  affects:
    - "WPF_Example/UI/Device/DeviceSelectorModelView.cs"
    - "WPF_Example/UI/Device/DeviceSelector.xaml"
    - "WPF_Example/UI/Device/DeviceSelector.xaml.cs"
    - "WPF_Example/UI/Device/CanvasViewer.cs"
    - "WPF_Example/Device/Camera/VirtualCamera.cs"
tech-stack:
  added: []
  patterns:
    - "MVVM 계산 분리: 코드비하인드는 배선만, PreviewZoomCalculator 정적 클래스가 순수 산술 담당"
key-files:
  created: []
  modified:
    - WPF_Example/UI/Device/DeviceSelectorModelView.cs
    - WPF_Example/UI/Device/DeviceSelector.xaml
    - WPF_Example/UI/Device/DeviceSelector.xaml.cs
    - WPF_Example/UI/Device/CanvasViewer.cs
    - WPF_Example/Device/Camera/VirtualCamera.cs
decisions:
  - "VirtualCamera.RenderCenterLine 1-인자 오버로드는 시그니처/좌표계를 그대로 두고, 2-인자(dc, dViewScale) 오버로드를 신규 추가해서 두께만 dViewScale 로 나눔. 1-인자 위임(delegate) 방식 대신 로직 복제를 선택 — RuntimeResizer.cs 가 1-인자 오버로드를 계속 쓰므로 회귀 위험을 최소화."
  - "CanvasViewer.OnRender 의 dc.PushTransform(this.RenderTransform) 를 제거. WPF 가 RenderTransform 을 Visual 전체(OnRender 결과 포함)에 이미 자동 적용하므로, 추가 Push 는 배율을 두 번 곱해 십자를 어긋나게 하던 버그였음."
  - "배율 단일 소스는 ModelView.DrawScale 세터를 그대로 사용 — 휠 핸들러가 scaleTransform/canvas 크기를 직접 만지지 않아 spin_zoom(SpinControl) 표시와 항상 동기화됨."
metrics:
  duration: "~40분"
  completed: "2026-09-07"
---

# Phase quick-260907-fkh Plan 01: 카메라 미리보기 휠 줌 + 십자 정합 Summary

DeviceSelector 카메라 미리보기에 커서-앵커 방식 마우스 휠 줌(1.25배 스텝, 0.25~2.0 클램프)을 추가하고, 센터 십자/사각형/원이 이중 스케일 버그 없이 정확한 위치·고정 두께로 그려지도록 수정.

## What Was Built

**Task 1 — `WPF_Example/UI/Device/DeviceSelectorModelView.cs`**
`PreviewZoomCalculator` 정적 클래스 추가:
- `GetNextScale(dCurrentScale, bZoomIn)`: 휠 한 칸당 1.25배(`ZOOM_STEP_FACTOR`), `DisplayConfig.DrawScaleLowLimit`/`HighLimit` (0.25~2.0)로 클램프.
- `GetAnchoredOffset(...)`: 커서 아래 이미지 픽셀 좌표를 구하고, 새 배율에서 같은 화면 위치가 되도록 스크롤 오프셋 재계산. 음수/최대 클램프는 하지 않음(ScrollViewer 가 자체 클램프).

**Task 2 — `WPF_Example/Device/Camera/VirtualCamera.cs`, `WPF_Example/UI/Device/CanvasViewer.cs`**
- `VirtualCamera`: `CENTER_PEN_THICKNESS` const(4) 도입, 생성자의 매직넘버 제거. 새 오버로드 `RenderCenterLine(DrawingContext dc, double dViewScale)` 추가 — 두께 `CENTER_PEN_THICKNESS / dViewScale` 인 로컬 Pen 사용, `dViewScale <= 0`이면 기존 1-인자 메서드로 폴백. 기존 1-인자 오버로드(RuntimeResizer 용)는 무변경.
- `CanvasViewer.OnRender`: `dc.PushTransform(this.RenderTransform)` 제거(이중 스케일 원인), `this.RenderTransform`이 `ScaleTransform`이면 `ScaleX`를 `dViewScale`로 읽어 `RenderCenterLine(dc, dViewScale)` 호출.

**Task 3 — `WPF_Example/UI/Device/DeviceSelector.xaml`, `.xaml.cs`**
- XAML: `scrollViewer`에 `PreviewMouseWheel="ScrollViewer_PreviewMouseWheel"` 배선(터널링 이벤트로 기본 세로 스크롤보다 먼저 처리).
- 코드비하인드: `ScrollViewer_PreviewMouseWheel` 핸들러 — 가드 → `PreviewZoomCalculator.GetNextScale` → `ModelView.DrawScale = dNewScale`(단일 소스, spin_zoom 자동 동기화) → 실제 반영값 재확인 → `scrollViewer.UpdateLayout()` → `PreviewZoomCalculator.GetAnchoredOffset` → `ScrollToHorizontalOffset`/`ScrollToVerticalOffset` → `e.Handled = true`.
- `ZoomValueChanged()` 끝에 `canvas_preview.InvalidateVisual()` 추가(스트리밍 정지 중 배율 변경 시 십자 재렌더 보장). 스크롤 오프셋은 이 메서드에서 건드리지 않음(100ms 라이브 갱신 중 줌/스크롤 유지 요구사항).
- 빌드(Release|x64): `error CS` 0건 확인.

## Deviations from Plan

None - plan executed exactly as written.

## Grep Gate Results (추가 라인 기준, 5개 파일)

| 항목 | Task 1 | Task 2 | Task 3 |
|---|---|---|---|
| 삼항 `\?[^?]*:` | 0 | 0 | 0 |
| `??` | 0 | 0 | 0 |
| `?.` | 0 | 0 | 0 |
| `switch.*=>` | 0 | 0 | 0 |
| `hbk` 날짜주석 | 0 | 0 | 0 |

브레이스 없는 한 줄 분기 육안 확인: 신규 추가 라인 중 없음. (기존 `if (pSelectedDevice == null) return;` 2줄은 사전 존재 라인으로 이번에 손대지 않음 — 근접한 주석/공백만 수정.)

## Build

`MSBuild.exe WPF_Example/DatumMeasurement.csproj -p:Configuration=Release -p:Platform=x64 -t:Build` → `error CS` 0건.

## Commits

1. `ad8d974c` feat(quick-260907-fkh): PreviewZoomCalculator 순수 줌 계산 추가
2. `c2204572` fix(quick-260907-fkh): 센터 십자 이중 스케일 제거 + 화면 두께 고정
3. `aa5d166f` feat(quick-260907-fkh): 마우스 휠 카메라 미리보기 줌 배선 (커서 앵커)

`WPF_Example/DatumMeasurement.csproj`는 다른 세션이 수정 중이므로 스테이징/커밋하지 않음(3개 커밋 모두 확인됨).

## Manual Verification Required

Task 4(실기 확인 checkpoint)는 사람만 수행 가능하여 실행하지 않았습니다. 프로그램을 Debug|x64 로 실행한 뒤 아래 절차를 확인해 주세요:

1. 카메라 창(DeviceSelector) 열기 → 카메라 선택해서 라이브 확인.
2. Display 탭에서 Center Line / Center Rect / Center Circle 켜기.
3. 배율 1.0 에서 십자 교차점이 이미지 센터(CenterX/Y) 위에 있는지 확인.
4. 이미지 특징점(예: 부품 모서리) 위에 마우스를 두고 휠을 위로 3~4칸:
   - 확대되는가?
   - 커서 아래 특징점이 화면상 거의 그 자리에 남는가? (수 픽셀 오차 허용)
   - 우측 상단 spin_zoom 값이 같이 올라가는가?
5. 확대 상태에서 십자 교차점이 여전히 같은 지점(센터) 위에 있는지, 선 두께가 1.0 때와 비슷한지 확인.
6. 스크롤바로 이동한 뒤에도 십자가 이미지의 같은 지점에 붙어 있는지 확인.
7. 10초 이상 라이브를 그대로 두고 배율/스크롤 위치가 저절로 원위치로 튀지 않는지 확인.
8. 휠을 아래로 계속 굴려 최소 배율(0.25)에서, 위로 굴려 최대(2.0)에서 화면이 튀지 않는지 확인.
9. 회귀 확인: MainView 뷰어 센터 표시가 예전과 동일한지, 조명 패널이 정상인지 확인.

승인/이상 항목을 알려주시면 이어서 처리하겠습니다.

## Self-Check: PASSED

- `WPF_Example/UI/Device/DeviceSelectorModelView.cs` — FOUND, contains `class PreviewZoomCalculator`
- `WPF_Example/UI/Device/CanvasViewer.cs` — FOUND, `PushTransform` 호출 0건
- `WPF_Example/Device/Camera/VirtualCamera.cs` — FOUND, 2-인자 `RenderCenterLine` 오버로드 존재
- `WPF_Example/UI/Device/DeviceSelector.xaml` — FOUND, `PreviewMouseWheel` 배선 존재
- `WPF_Example/UI/Device/DeviceSelector.xaml.cs` — FOUND, `ScrollViewer_PreviewMouseWheel`/`InvalidateVisual` 존재
- 커밋 `ad8d974c`, `c2204572`, `aa5d166f` — 모두 `git log --oneline`에서 확인됨
