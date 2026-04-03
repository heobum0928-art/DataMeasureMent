# Phase 1: UI 재설계 - Context

**Gathered:** 2026-04-02
**Status:** Ready for planning

<domain>
## Phase Boundary

Shot/FAI 2계층 TreeView 탐색 구조 + 단일 캔버스 이미지 뷰어 + FAI 측정 결과 테이블 + Shot/FAI CRUD를 구현한다. Phase 5에서 만든 데이터 모델(ShotConfig, FAIConfig, InspectionRecipeManager) 위에 UI를 구축하되, ROI 티칭(Phase 2), 에지 측정 알고리즘(Phase 3), 검사 시퀀스(Phase 4)는 이 Phase의 범위가 아니다.

</domain>

<decisions>
## Implementation Decisions

### TreeView 구조
- **D-01:** Shot > FAI 2계층 트리 구조로 표시한다. Shot 노드를 펼치면 하위 FAI 노드가 보인다.
- **D-02:** TreeView에서 Shot을 선택하면 캔버스에 해당 Shot 이미지가 표시되고, 결과 테이블에 해당 Shot의 FAI 목록이 표시된다.

### 레이아웃 배치
- **D-03:** 3영역 레이아웃: 좌측 TreeView | 우상단 캔버스(이미지) | 우하단 결과 테이블.
- **D-04:** GridSplitter를 사용하여 좌우 경계 및 캔버스/테이블 상하 경계를 드래그로 크기 조절 가능하게 한다.

### 결과 테이블 형식
- **D-05:** 결과 테이블 컬럼: FAI 이름 | 거리(mm) | Spec(Min/Max) | 판정(OK/NG).
- **D-06:** OK은 초록, NG는 빨강 색상 코딩을 적용한다.

### CRUD 인터페이스
- **D-08:** TreeView 상단에 툴바를 배치하고 추가(+)/삭제(−)/편집 버튼을 둔다. 선택된 노드 기준으로 동작한다.
- **D-09:** Phase 1에서 편집 가능한 속성은 이름(Name)만이다. ROI, Tolerance 등은 Phase 2~3에서 다룬다.
- **D-10:** Shot/FAI 삭제 시 확인 다이얼로그를 표시한다.

### Claude's Discretion
- 캔버스 컨트롤은 기존 MainResultViewerControl(Halcon 기반)을 재사용하되, 필요 시 확장한다.
- 기존 InspectionListView의 CompositeNode/NodeViewModel 패턴 참고 여부는 구현 시 판단한다.
- 기존 TabControl은 단일 캔버스로 교체한다.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### 기존 UI 구조
- `WPF_Example/UI/ContentItem/MainView.xaml` — 현재 MainView (TabControl + ComboBox + 캔버스), 교체 대상
- `WPF_Example/UI/ContentItem/MainView.xaml.cs` — MainView 코드비하인드
- `WPF_Example/UI/ControlItem/InspectionListView.xaml.cs` — 기존 TreeView 패턴 (CompositeNode/NodeViewModel)
- `WPF_Example/UI/ControlItem/InspectionListViewModel.cs` — ViewModel 패턴 참고

### Phase 5 데이터 모델
- `WPF_Example/Custom/Sequence/Inspection/ShotConfig.cs` — Shot 데이터 모델
- `WPF_Example/Custom/Sequence/Inspection/FAIConfig.cs` — FAI 데이터 모델 (ROI, Edge, Tolerance)
- `WPF_Example/Custom/Sequence/Inspection/InspectionRecipeManager.cs` — Shot/FAI CRUD + INI 저장/로드

### 프레임워크 참고
- `WPF_Example/Halcon/Display/HalconDisplayService.cs` — 캔버스 오버레이 드로잉
- `WPF_Example/Halcon/HalconImageBridge.cs` — HImage ↔ WPF BitmapSource 변환

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `MainResultViewerControl`: Halcon 이미지 표시용 WPF 컨트롤 — 캔버스로 재사용 가능
- `HalconDisplayService`: 캔버스에 오버레이(ROI, 측정선) 드로잉 — Phase 2~3에서도 사용
- `InspectionListView`의 CompositeNode/NodeViewModel: TreeView 데이터 바인딩 패턴 — 참고 가능
- `InspectionRecipeManager`: Shot/FAI CRUD 메서드 이미 구현됨 — UI에서 호출만 하면 됨

### Established Patterns
- MVVM 일부 적용: Observable base class, INotifyPropertyChanged
- code-behind + ViewModel 혼용 패턴 (일부 뷰는 ViewModel, 일부는 직접 코드비하인드)
- PropertyTools.Wpf 사용 중 (속성 그리드)

### Integration Points
- `SystemHandler.Handle.Sequences` — 시퀀스 목록 접근
- `InspectionRecipeManager` — Shot/FAI 데이터 접근 및 CRUD
- `MainWindow` — MDI 컨테이너, MainView를 호스팅

</code_context>

<specifics>
## Specific Ideas

No specific requirements — open to standard approaches

</specifics>

<deferred>
## Deferred Ideas

- **D-07 (테이블 행 선택 시 캔버스에서 해당 FAI의 ROI 하이라이트):** Phase 2로 이관. ROI 티칭 데이터(RoiDefinition)가 Phase 2에서 생성되므로, ROI 하이라이트 기능은 Phase 2 이후에만 구현 가능하다.

</deferred>

---

*Phase: 01-ui*
*Context gathered: 2026-04-02*
