# Phase 2: 티칭 & 캘리브레이션 - Context

**Gathered:** 2026-04-07
**Status:** Ready for planning

<domain>
## Phase Boundary

FAI ROI를 캔버스에 시각적으로 표시하고, Rectangle1/Polygon 두 가지 ROI 타입의 생성/삭제를 지원하며, ROI 설정을 INI에 저장/로드하고, 2점 클릭 기준 오브젝트 측정 방식으로 카메라별 픽셀-mm 캘리브레이션을 수행하는 것.

Phase 1에서 완성된 3영역 레이아웃(TreeView | 캔버스 | 결과 테이블)과 FAI CRUD 위에 구축한다. 에지 측정 알고리즘 연동과 측정값 오버레이는 Phase 3 범위이다.

</domain>

<decisions>
## Implementation Decisions

### ROI 오버레이 표시
- **D-01:** 트리에서 FAI 선택 시 해당 FAI의 ROI만 캔버스에 표시한다. HalconDisplayService의 기존 selectedRoiId 패턴 활용.
- **D-02:** ROI 오버레이에 에지 검색 방향을 화살표로 표시한다 (사각형 + 화살표).
- **D-03:** ROI 색상은 기존 패턴 유지: 기본 초록, 선택된 ROI 노란색.
- **D-04:** 측정값 텍스트 오버레이는 Phase 3에서 추가한다.

### ROI 타입
- **D-05:** Rectangle1(축 정렬 사각형) + Polygon 두 가지 ROI 타입을 지원한다. Rectangle2(회전 사각형)는 사용하지 않는다.
- **D-06:** Polygon ROI는 시각화(관심 영역 표시)용이다. 실제 에지 측정은 Polygon 내부에 별도로 설정하는 Rectangle1 ROI에서 수행한다.
- **D-07:** Polygon은 마우스로 점을 하나씩 찍고 우클릭으로 완성하는 방식. 점 개수는 3~20개 제한.
- **D-08:** Polygon 편집은 생성만 지원. 수정이 필요하면 삭제 후 재생성.

### ROI 편집 방식
- **D-09:** Rectangle1 ROI는 MainView 캔버스에서 마우스 드래그로 설정한다. HalconViewerControl.StartRectangleDrawing/CommitActiveRectangle 활용.
- **D-10:** ROI 편집은 MainView 캔버스에서 직접 수행한다 (TeachingWindow 다이얼로그 사용 안 함).
- **D-11:** "Rect ROI" 버튼과 "Polygon ROI" 버튼을 캔버스 상단 툴바에 별도로 배치한다. 각 버튼을 누르면 해당 ROI 타입의 편집 모드 진입, 다시 누르면 해제.

### 캘리브레이션
- **D-12:** 픽셀-mm 변환 계수는 카메라별 1개로 관리한다. CameraSlaveParam에 PixelResolution 필드를 추가.
- **D-13:** 캘리브레이션 방식은 기준 오브젝트 측정: 캔버스에서 2점을 클릭하여 픽셀 거리를 측정하고, 실제 거리(mm)를 입력하면 mm/pixel을 자동 계산하여 적용.

### 저장 구조
- **D-14:** ROI 데이터는 INI에만 저장한다 (별도 JSON 없음). FAIConfig의 ROI 필드는 기존 ParamBase.Save/Load로 자동 저장.
- **D-15:** Polygon 점 데이터는 "x1,y1;x2,y2;x3,y3" 형식의 직렬화 문자열로 FAIConfig의 string 필드에 저장. ParamBase의 string 직렬화로 INI에 자동 저장/로드.
- **D-16:** 캘리브레이션 계수는 CameraSlaveParam의 PixelResolution 필드로 INI에 저장. 레시피별 관리.

### Claude's Discretion
- FAIConfig.ToRoiDefinition() 변환 메서드의 세부 구현
- Polygon ROI 클래스의 데이터 모델 구조
- 캔버스 툴바 UI의 구체적인 XAML 레이아웃
- 2점 클릭 캘리브레이션의 UI 플로우 세부 (다이얼로그 vs 인라인)
- HalconDisplayService에 Polygon 렌더링 추가 방식

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### 기존 Halcon 디스플레이/티칭 인프라
- `WPF_Example/Halcon/Display/HalconDisplayService.cs` — ROI 사각형 렌더링, EdgeInspectionOverlay 렌더링 패턴
- `WPF_Example/Halcon/Services/TeachingStorageService.cs` — TeachingStorageService, HalconTeachingHelper (경로 계산, Job 저장/로드)
- `WPF_Example/Halcon/Models/RoiDefinition.cs` — ROI 좌표 + 에지 파라미터 + PixelResolutionX/Y
- `WPF_Example/Halcon/Models/TeachingJob.cs` — TeachingJob 구조 (Rois 리스트 포함)
- `WPF_Example/Halcon/Models/EdgeInspectionOverlay.cs` — 에지 측정 오버레이 구조

### Phase 1 완성 UI (확장 대상)
- `WPF_Example/UI/ContentItem/MainView.xaml` — MainView 3영역 레이아웃 (캔버스 상단 툴바 추가 위치)
- `WPF_Example/UI/ContentItem/MainView.xaml.cs` — FAIResults_SelectionChanged stub (Phase 2에서 채워야 함)
- `WPF_Example/UI/ControlItem/InspectionListView.xaml.cs` — 트리 선택 이벤트 + FAI CRUD
- `WPF_Example/UI/ContentItem/HalconViewerControl.xaml.cs` — StartRectangleDrawing, CommitActiveRectangle, SetRois, SetSelectedRoi
- `WPF_Example/UI/ContentItem/MainResultViewerControl.xaml.cs` — UpdateDisplayState(rois, overlays, messages)

### FAI 데이터 모델 (수정 대상)
- `WPF_Example/Custom/Sequence/Inspection/FAIConfig.cs` — ROI_Row/Col/Phi/Length1/Length2, 에지 파라미터, 공차
- `WPF_Example/Sequence/Param/CameraSlaveParam.cs` — 카메라 파라미터 기반 클래스 (PixelResolution 추가 위치)
- `WPF_Example/Sequence/Param/ParamBase.cs` — INI 직렬화 기반 클래스

### 기존 캘리브레이션 참고
- `WPF_Example/UI/ControlItem/CalibrationView.xaml.cs` — 기존 캘리브레이션 뷰 (최소 구현)
- `WPF_Example/UI/ViewModel/CalibrationViewModel.cs` — 기존 캘리브레이션 ViewModel (ModelFinder 패턴)

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `HalconViewerControl`: ROI 드로잉 (StartRectangleDrawing/CommitActiveRectangle), ROI 렌더링 (SetRois/SetSelectedRoi) 이미 완전히 구현됨 — Rectangle1 드래그 편집에 직접 활용
- `HalconDisplayService.Render()`: rois + selectedRoiId + inspectionOverlays 렌더링 — Polygon 렌더링 확장 필요
- `TeachingStorageService`: DataContractJsonSerializer 기반 — ROI 저장에는 사용하지 않지만 참조 가능
- `HalconTeachingHelper`: BuildFixedTeachingPath, RectToRoi, BuildBounds 등 헬퍼 — ROI 변환 참조

### Established Patterns
- ParamBase INI 직렬화: public property → 자동 INI 저장/로드 (string, double, int, enum 지원)
- HalconDisplayService 색상 규칙: green=기본 ROI, yellow=선택, red=draft, blue=기타
- FAIConfig → ParamBase 상속: ROI 필드는 이미 INI에 저장됨

### Integration Points
- MainView.FAIResults_SelectionChanged: Phase 1에서 stub으로 남겨둠 — Phase 2에서 ROI 하이라이트 연결
- InspectionViewModel: FAI 선택 시 캔버스에 ROI 전달하는 경로 추가 필요
- CameraSlaveParam: PixelResolution 필드 추가 → 하위 모든 파라미터에서 접근 가능

</code_context>

<specifics>
## Specific Ideas

- Polygon ROI: 마우스로 점을 하나씩 찍고, 우클릭으로 완성. 3~20점 제한. 생성만 지원 (수정 시 삭제 후 재생성).
- 캘리브레이션: 캔버스에서 2점 클릭 → 픽셀 거리 자동 계산 → 실제 거리(mm) 입력 다이얼로그 → mm/pixel 자동 적용.
- ROI 타입 선택: 캔버스 상단 툴바에 "Rect ROI" | "Polygon ROI" 버튼 2개 분리 배치.

</specifics>

<deferred>
## Deferred Ideas

- 측정값 텍스트 오버레이 (ROI 옆에 "3.25mm OK") — Phase 3
- Polygon ROI 점 드래그 수정/개별 점 삭제 — v2 이후
- Rectangle2(회전 사각형) ROI 지원 — 현재 불필요

</deferred>

---

*Phase: 02-teaching-calibration*
*Context gathered: 2026-04-07*
