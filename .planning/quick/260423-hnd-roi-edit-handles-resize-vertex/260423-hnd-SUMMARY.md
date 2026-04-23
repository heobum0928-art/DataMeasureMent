---
quick_id: 260423-hnd
slug: roi-edit-handles-resize-vertex
status: complete
date: 2026-04-23
---

# Quick Task 260423-hnd: Edit 모드 핸들 — 리사이즈 + Polygon 정점 편집 — SUMMARY

## 완료 내용

2단계에서 도입한 Edit 모드에 실제 편집 기능 구현:
- **Rect**: 4 코너 + 4 변 중점 (총 8개 핸들) → 드래그로 리사이즈
- **Circle**: 중심에서 동쪽으로 반경 1개 핸들 → 드래그로 반경 변경 (이동은 기존 바디 드래그)
- **Polygon**: 각 꼭짓점에 핸들 → 드래그로 해당 정점 이동
- **Edit 모드 게이팅**: 이동/리사이즈 모두 `_isEditMode == true`일 때만 작동. (기존 o53의 "드로잉 모드 아니면 바로 이동" 동작을 Edit 모드 전용으로 전환)

## 파일 변경

### `WPF_Example/UI/ContentItem/MainResultViewerControl.xaml.cs`
- **타입**: `RoiGeometryChangedArgs`, `ResizeHandle` enum 추가
- **필드**: `_isResizingRoi`, `_resizingHandle`, `_resizingPolygonIndex`, `_resizingRoiSnapshot`, 상수 `HandleHalfSizeImage=5`, `HandleHitRadiusImage=8`
- **이벤트**: `RoiGeometryChanged` (리사이즈/정점편집 절대 좌표 방출)
- **helper**:
  - `GetEditHandles(roi)` — ROI 타입별 핸들 위치 리스트 산출
  - `HitTestEditHandle(roi, point)` — 핸들 반경 내 클릭 판정
  - `ApplyResizeToTarget(target, point)` — snapshot 기준 target 기하 갱신 (핸들별 분기 + Rect 정규화 + 최소 1px 보정)
  - `ParsePolygonPointsLocal` / `SerializePolygonPointsLocal` — `"x,y;x,y;..."` 포맷 파서/시리얼라이저 (HalconDisplayService 내부 함수가 비공개이므로 로컬 사본)
  - `RenderEditHandles()` — cyan `DispRectangle1`로 작은 정사각형 덧그림
- **HMouseDown**: Edit 모드 분기 추가 — 핸들 히트 → 리사이즈 시작(snapshot clone), 바디 히트 → 이동 시작. Edit 모드가 아니면 이동/리사이즈 모두 진입 차단.
- **HMouseMove**: `_isResizingRoi` 브랜치 추가 → `ApplyResizeToTarget` + Render
- **HMouseUp**: `_isResizingRoi` 브랜치 추가 → 상태 리셋 + `RoiGeometryChanged` 방출 (RoiId/Shape/Rect 좌표/Circle 좌표/Polygon 문자열 전부 동봉)
- **RenderNow**: 마지막에 `RenderEditHandles()` 호출 (Edit 모드 + 선택 ROI 있을 때만 렌더)

### `WPF_Example/UI/ContentItem/MainView.xaml.cs`
- 생성자: `halconViewer.RoiGeometryChanged += HalconViewer_RoiGeometryChanged` 구독
- `MainView_Unloaded`: 구독 해제
- `HalconViewer_RoiGeometryChanged`:
  - `FindFAIByName(e.RoiId)` → FAI 갱신
  - `Circle`: 첫 `CircleDiameterMeasurement`의 `Circle_Row/Col/Radius` 갱신
  - `Polygon`: `fai.PolygonPoints = e.PolygonPoints`
  - `Rect`: 바운딩박스 → `ROI_Row/Col` = center, `ROI_Length1/2` = half-row/col, `ROI_Phi=0` (D-05 Rectangle1 호환)
  - `GetCurrentFAIRois()` + `UpdateDisplayState(..., e.RoiId, null, null)` 재렌더

## 검증

- `msbuild WPF_Example/DatumMeasurement.csproj /p:Configuration=Debug /p:Platform=x64` 빌드 성공
- 신규 경고 없음

## UAT 체크리스트

### Rect
- [ ] Rect ROI 선택 → 우클릭 → Edit ROI → 8개 cyan 핸들 렌더
- [ ] 각 코너 드래그 → 대각 방향 리사이즈 (자기 반대 코너 고정)
- [ ] 각 변 중점 드래그 → 해당 변만 이동 (수평/수직 1축)
- [ ] ROI 내부 바디 드래그 → 이동 (기존 o53 동작)
- [ ] 리사이즈 후 Row/Col 자동 정규화 (drag past opposite edge 허용, 음수 width 방지)
- [ ] 우클릭 → Edit 모드 종료, 핸들 사라짐, 일반 상태 복귀

### Circle
- [ ] Circle ROI 선택 → Edit → 동쪽(오른쪽) 1개 핸들 렌더
- [ ] 핸들 드래그 → 반경 변경 (중심 고정)
- [ ] 바디 드래그 → 이동

### Polygon
- [ ] Polygon ROI 선택 → Edit → 모든 꼭짓점에 핸들
- [ ] 특정 꼭짓점 드래그 → 해당 점만 이동, 나머지 고정
- [ ] 우클릭 → Edit 모드 종료

### 게이팅
- [ ] Edit 모드 아닐 때: 바디 드래그 = pan/이동 아님(= 아무 일 없음), 핸들 없음
- [ ] 드로잉 모드(Rect/Circle 버튼 ON) 중엔 Edit 메뉴 비활성 (2단계 규칙)

## 비고 / 후속

- **Polygon 전체 이동**: 현재 `HitTestSelectedRoi`는 Polygon 바디 히트를 지원하지 않음 (Rect bbox / Circle 원판만). 전체 이동이 필요하면 `HitTestSelectedRoi`에 Polygon 분기(point-in-polygon) 추가 필요 — 후속 작업.
- **핸들 크기**: 이미지 좌표 기준 5px 반변 / 8px 히트 반경 고정. 줌 레벨에 따라 너무 작거나 클 수 있음 → 필요 시 `GetImagePart()` 기반 스케일 보정 고려.
- **ROI_Phi**: Rect 리사이즈 결과는 항상 `Phi=0` (D-05 "Rectangle2 미사용" 정책 준수). 회전된 Rect 편집은 지원 안 함.
- **Rect 최소 크기**: 1px로 하드 보정 — 사용자가 코너를 반대 코너 너머로 끌어도 뒤집히지 않고 1px로 붙음.
- **CircleDiameterMeasurement**: FAI에 여러 개 있어도 첫 번째만 업데이트 (기존 RoiMoveCompleted 동일 규칙).
