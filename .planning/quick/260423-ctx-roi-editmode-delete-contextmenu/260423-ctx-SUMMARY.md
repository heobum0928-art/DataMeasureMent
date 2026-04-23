---
quick_id: 260423-ctx
slug: roi-editmode-delete-contextmenu
status: complete
date: 2026-04-23
---

# Quick Task 260423-ctx: 우클릭 ContextMenu에 Edit/Delete ROI — SUMMARY

## 완료 내용

생성은 기존 ToggleButton(Rect/Circle/Polygon) 유지, 우클릭 ContextMenu에는 선택된 ROI를 대상으로 한 **Edit** / **Delete** 액션을 추가.

- Edit ROI — `_isEditMode` 토글, 이벤트 `RoiEditModeChanged` 발생 (2단계는 상태+시그널만, 실제 핸들 렌더링은 3단계)
- Delete ROI — `RoiDeleteRequested` 이벤트 → MainView에서 FAI의 Rect/Polygon/Circle ROI 필드 초기화 (FAI 자체 유지)
- Edit 모드 중 우클릭 → 모드 종료 (ContextMenu 미표시)

## 파일 변경

### `WPF_Example/UI/ContentItem/MainResultViewerControl.xaml`
- `ViewerContextMenu` 상단에 `EditRoiMenuItem`, `DeleteRoiMenuItem` + Separator 추가

### `WPF_Example/UI/ContentItem/MainResultViewerControl.xaml.cs`
- 필드: `_isEditMode` (bool), public `IsEditMode` 프로퍼티
- 이벤트: `RoiEditModeChanged` (EventHandler<bool>), `RoiDeleteRequested` (EventHandler<string>)
- `ViewerHost_HMouseDown`: 우클릭 처리에 "`_isEditMode` 이면 `SetEditMode(false)` 후 return" 선분기 추가
- `UpdateContextMenuState()`: Edit/Delete MenuItem 활성화 = `_selectedRoiId` 존재 + 해당 ROI `IsTaught` + 드로잉 모드 아님. Edit는 `IsCheckable=true`로 체크마크 표시
- `SetEditMode(bool)` helper — 상태 변경·커서(`Cursors.Cross`↔기본) 전환·이벤트 발생·재렌더
- `EditRoiMenuItem_Click` — 토글
- `DeleteRoiMenuItem_Click` — Edit 모드 중이었다면 종료 후 `RoiDeleteRequested` 발생

### `WPF_Example/UI/ContentItem/MainView.xaml.cs`
- 생성자: `halconViewer.RoiDeleteRequested += HalconViewer_RoiDeleteRequested` 구독
- `MainView_Unloaded`: 구독 해제
- `HalconViewer_RoiDeleteRequested`:
  - `FindFAIByName(roiId)` → FAI의 `ROI_Row/Col/Phi/Length1/Length2 = 0`
  - `PolygonPoints = ""`
  - 모든 `CircleDiameterMeasurement`의 `Circle_Row/Col/Radius = 0`
  - `GetCurrentFAIRois()` + `UpdateDisplayState(..., null, ...)` 재렌더

## 검증

- `msbuild WPF_Example/DatumMeasurement.csproj /p:Configuration=Debug /p:Platform=x64` 빌드 성공 (신규 경고 없음)

## UAT 체크리스트

- [ ] ROI 선택(좌클릭) → 우클릭 → Edit ROI / Delete ROI 메뉴 **활성**
- [ ] ROI 미선택 상태 → 우클릭 → 두 메뉴 **비활성**
- [ ] 드로잉 모드(Rect/Circle/Polygon 버튼 ON) 중 우클릭 → 두 메뉴 **비활성**
- [ ] Edit ROI 클릭 → 체크마크 표시 + 커서 `Cross` 전환 (3단계에서 실제 핸들 렌더)
- [ ] Edit 모드 중 우클릭 → 모드 종료 (ContextMenu 뜨지 않음)
- [ ] Delete ROI 클릭 → 해당 FAI의 ROI 사라짐, FAI 자체는 Inspection 트리에 유지
- [ ] Delete 후 동일 FAI에 Rect ROI 재드로잉 가능

## 비고

- Edit 모드 실제 편집 기능(리사이즈 핸들, Polygon 점 드래그)은 **3단계**에서 구현
- 현재 기존 o53 이동 기능은 Edit 모드 여부와 무관하게 선택 ROI 내부 클릭 시 동작 — 3단계에서 Edit 모드 게이팅으로 통합 검토 필요
