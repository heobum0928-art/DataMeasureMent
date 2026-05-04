# Phase 1: UI 재설계 - Context

**Gathered:** 2026-04-02
**Revised:** 2026-04-07
**Status:** Ready for re-planning (direction corrected)

<domain>
## Phase Boundary

기존 InspectionListView의 Sequence→Action 트리 구조 안에서 FAI 검사 항목을 묶음 관리할 수 있도록 확장한다. FAI가 검사의 최소 단위이며, 각 FAI는 자체 이미지 + 카메라 설정 + 측정 파라미터를 소유한다. FAI 안에 측정이 1개면 단독, N개면 묶음이다. 기존 PropertyGrid 편집 흐름을 그대로 활용한다.

**핵심 변경:** MainView에 별도 좌측 TreeView를 만드는 것이 아니라, 기존 InspectionListView 트리 + PropertyGrid 구조 안에서 FAI를 파라미터로 묶어 관리한다.

</domain>

<decisions>
## Implementation Decisions

### FAI = 검사 최소 단위 (이미지 단위)
- **D-01:** FAI가 촬영 + 검사의 단위이다. 각 FAI는 자체 Z축 위치, 카메라 설정, 이미지, 측정 파라미터를 소유한다.
- **D-02:** FAI 내에 측정(Measurement)이 1개면 단독 FAI, N개면 묶음 FAI이다. 묶음 FAI는 1장의 이미지를 공유하면서 여러 측정을 수행한다.
- **D-03:** Shot 계층은 제거한다. FAI가 곧 촬영 단위이므로 Shot이라는 중간 계층은 불필요하다.

### 기존 골격 활용
- **D-04:** InspectionListView의 TreeListBox(Sequence→Action 트리) + PropertyGrid 구조를 그대로 활용한다. 별도 좌측 TreeView를 MainView에 만들지 않는다.
- **D-05:** Action 노드 하위에 FAI 노드를 표시한다. `ENodeType`에 FAI 타입을 추가하여 기존 CompositeNode/NodeViewModel 패턴을 확장한다.
- **D-06:** FAI 노드 선택 시 PropertyGrid에 해당 FAIConfig의 속성(ROI, Edge, Tolerance, Z위치, 카메라 설정 등)이 표시된다.

### 데이터 모델 수정
- **D-07:** ShotConfig를 FAIConfig로 통합하거나, FAIConfig가 카메라 파라미터(CameraSlaveParam)를 직접 상속하도록 변경한다. 각 FAI가 독립적으로 이미지를 관리한다.
- **D-08:** FAI 묶음 구조: 하나의 FAI 안에 여러 Measurement를 가질 수 있다. 이미지는 FAI 단위로 1장 관리.

### CRUD
- **D-09:** FAI 추가/삭제/수정은 기존 InspectionListView의 트리 구조 안에서 동작한다.
- **D-10:** FAI 삭제 시 확인 다이얼로그를 표시한다.

### 결과 표시
- **D-11:** FAI 측정 결과(거리 mm, OK/NG 판정)를 테이블로 표시한다. OK은 초록, NG는 빨강.
- **D-12:** MainView 캔버스에서 선택된 FAI의 이미지를 표시한다.

### Claude's Discretion
- FAI 묶음(그룹) 내 Measurement의 구체적인 데이터 모델 구조
- FAIConfig의 CameraSlaveParam 상속 구조 세부사항
- 기존 InspectionListView 트리에 FAI 노드를 추가하는 구체적인 방법

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### 기존 UI 구조 (핵심 — 이 구조를 확장해야 함)
- `WPF_Example/UI/ControlItem/InspectionListView.xaml` — 기존 TreeListBox + PropertyGrid 구조 (확장 대상)
- `WPF_Example/UI/ControlItem/InspectionListView.xaml.cs` — 기존 트리 선택→PropertyGrid 바인딩 흐름
- `WPF_Example/UI/ControlItem/InspectionListViewModel.cs` — Sequence→Action 트리 생성 패턴
- `WPF_Example/UI/ControlItem/NodeViewModel.cs` — 기존 TreeView 노드 ViewModel
- `WPF_Example/UI/ControlItem/Node.cs` — CompositeNode, ENodeType 정의

### 기존 MainView (FAI 이미지 표시용)
- `WPF_Example/UI/ContentItem/MainView.xaml` — 현재 MainView (이미지 캔버스)
- `WPF_Example/UI/ContentItem/MainView.xaml.cs` — 이미지 표시 + 결과 표시 로직

### Phase 5 데이터 모델 (수정 대상)
- `WPF_Example/Custom/Sequence/Inspection/ShotConfig.cs` — Shot 데이터 모델 → FAI로 통합 필요
- `WPF_Example/Custom/Sequence/Inspection/FAIConfig.cs` — FAI 데이터 모델
- `WPF_Example/Custom/Sequence/Inspection/InspectionRecipeManager.cs` — CRUD + INI 저장/로드
- `WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs` — FAI 측정 Action

### 프레임워크 참고
- `WPF_Example/Sequence/Param/ParamBase.cs` — 파라미터 기반 클래스 (PropertyGrid 바인딩)
- `WPF_Example/Sequence/Param/CameraSlaveParam.cs` — 카메라 파라미터 (FAI가 상속해야 함)
- `WPF_Example/Custom/Sequence/Top/Action_TopInspection.cs` — 기존 Action+Param 패턴 참고

</canonical_refs>

<code_context>
## Existing Code Insights

### 핵심 발견: 기존 InspectionListView 흐름
1. `InspectionListViewModel`이 `CompositeNode` 트리를 생성 (Recipe→Sequence→Action)
2. `NodeViewModel`이 트리 노드를 감싸고, `.Param` 속성으로 ParamBase 객체를 노출
3. `InspectionListView.xaml`의 PropertyGrid가 `SelectedObject="{Binding SelectedItem.Param}"` 바인딩
4. 즉, 트리에서 Action 선택 → PropertyGrid에 해당 Action의 Param이 표시됨
5. **이 흐름에 FAI 노드를 추가하면** FAI 선택 시 FAIConfig가 PropertyGrid에 표시됨

### 기존 ENodeType
```csharp
public enum ENodeType {
    Recipe, Sequence, Action, SubSequence
}
```
여기에 `FAI` (또는 `Measurement`) 타입을 추가하면 트리에 FAI 노드 표시 가능.

### FAIConfig 현재 상태
- `ParamBase` 상속 → PropertyGrid 편집 가능
- ROI, Edge, Tolerance 속성 포함
- 카메라 파라미터 미포함 → CameraSlaveParam 상속으로 변경 필요

### ShotConfig 현재 상태
- `CameraSlaveParam` 상속 + 이미지 버퍼 + FAIList
- Shot 개념이 FAI로 흡수되면 이미지 관리 로직을 FAIConfig로 이동 필요

</code_context>

<specifics>
## Specific Ideas

- FAI 묶음: 1개의 FAIConfig 안에 List<MeasurementConfig>를 두는 방식. 단독 FAI는 MeasurementConfig 1개.
- FAI 묶음의 이미지: FAIConfig 레벨에서 1장 관리. 묶음 내 모든 Measurement가 동일 이미지 사용.

</specifics>

<deferred>
## Deferred Ideas

- ROI 티칭 인터랙션: Phase 2
- 에지 측정 알고리즘 연동: Phase 3
- 검사 시퀀스 Z축 이동 로직: Phase 4

</deferred>

---

*Phase: 01-ui*
*Context gathered: 2026-04-02, revised: 2026-04-07 (direction corrected — FAI=이미지단위, 기존 골격 활용)*
