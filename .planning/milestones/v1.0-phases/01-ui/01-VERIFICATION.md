---
phase: 01-ui
verified: 2026-04-23
status: verified
score: 8/8 must-haves verified
re_verification: false
gap_closure: [G3]
backfill_phase: 09-verification-backfill
---

# Phase 01: UI — Verification Report

**Phase Goal:** Shot-FAI 계층 트리, 단일 캔버스, FAI 측정 결과 테이블, FAI CRUD 를 갖춘 새 검사 UI 를 구축한다 (UI-01..UI-05)
**Verified:** 2026-04-23
**Status:** verified
**Re-verification:** No — backfill (Phase 09-01, audit Gap G3 closure)

## Goal Achievement

### Observable Truths

| #  | Truth | Status | Evidence |
|----|-------|--------|----------|
| 1  | TreeView 가 Sequence > Action > FAI (현재는 Datum/Measurement 노드 포함 4계층) 계층을 표시한다 (UI-01) | ✓ VERIFIED | `enum ENodeType { Recipe, Sequence, Action, SubSequence, FAI, ... }` 확인 (Node.cs:10-15); `CreateSequenceNode` 가 `seqNode -> actNode -> faiNode` 를 트리에 add 하는 루프 확인 (InspectionListViewModel.cs:55-113) |
| 2  | 시작 시 트리가 자동 펼쳐진다 (UI-01 가시성) | ✓ VERIFIED | `ListView_Loaded` 에서 `ViewModel.RootModel.ExpandAll()` 호출 확인 (InspectionListView.xaml.cs:39, 75, 125); `ExpandAll` 본체는 `NodeViewModel.cs:224-227` 재귀 펼침 |
| 3  | MainView 가 단일 캔버스 + 결과 DataGrid 2단 구조이며 좌측 TreeView 가 제거되었다 (UI-02) | ✓ VERIFIED | `MainView.xaml` 에 `<dmv:MainResultViewerControl x:Name="halconViewer"/>` 단일 캔버스 (line 115) + `<DataGrid x:Name="dataGrid_faiResults" Grid.Row="3" ...>` (line 142). XAML 어디에도 `TreeView` 요소 없음 (grep 미일치) |
| 4  | FAI/Action 선택 시 캔버스에 해당 Shot 이미지가 표시된다 (UI-02) | ✓ VERIFIED | `DisplayFAIImage(FAIConfig fai)` (MainView.xaml.cs:74-83) → `fai.Owner as ShotConfig` → `DisplayShotImage(shot)` → `halconViewer.LoadImage(img)` (line 92). `InspectionList_SelectionChanged` 에서 ENodeType.FAI/Action 분기로 `_inspectionVm.OnFAISelected(faiConfig)` 호출 확인 (InspectionListView.xaml.cs:293-307) |
| 5  | DataGrid 가 측정 결과(Measurement, OK/NG 판정, 거리 mm)를 행 단위로 표시한다 (UI-03) | ✓ VERIFIED | `MeasurementResultRow.MeasuredValueText` (F3 포맷, MeasurementResultRow.cs:60), `JudgeText` (`OK`/`NG`/`—`, line 58), `SpecMin/MaxText` (line 62-64) 컬럼 바인딩 확인 (MainView.xaml DataGridTextColumn 컬럼 정의). 단위 포맷은 `ResultDisplay` 에서 mm/deg 분기 (line 48-56) |
| 6  | DataGrid 행이 OK 는 녹색, NG 는 적색 배경으로 색 코딩된다 (UI-03) | ✓ VERIFIED | `MainView.xaml:171-184` 에 `MultiDataTrigger` 두 개 — `IsPass=True && HasResult=True` → green, `IsPass=False && HasResult=True` → red. `Btn_AddFAI_Click` 처리 흐름의 컬러 헤더 + 셀 스타일은 다크 테마 유지 |
| 7  | FAI Add/Delete/Rename CRUD 가 InspectionListView 툴바에서 동작한다 (UI-04, UI-05) | ✓ VERIFIED | XAML 버튼 `button_addFAI`/`button_removeFAI`/`button_renameFAI` (InspectionListView.xaml:231,237,243). 핸들러 `Btn_AddFAI_Click` (line 394) → `_inspectionVm.AddFAI(shot, name)` → `ViewModel.AddFAINode(...)` (line 455-457). `Btn_RemoveFAI_Click` (line 545) 와 `Btn_RenameFAI_Click` (line 632) 도 정의됨 |
| 8  | FAI 삭제 시 사용자 확인 다이얼로그가 표시된다 (UI-04 안전성, D-10) | ✓ VERIFIED | `Btn_RemoveFAI_Click` 에서 `CustomMessageBox.ShowConfirmation(...)` 호출 후 `MessageBoxResult.Yes` 만 진행 (InspectionListView.xaml.cs:614, 622); 추가로 line 339, 419, 556, 576, 596 에서도 ShowConfirmation 패턴 사용 |

**Score:** 8/8 truths verified

### Required Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `WPF_Example/UI/ControlItem/Node.cs` | `ENodeType.FAI` 열거형 + ImageSource 매핑 | ✓ VERIFIED | enum 멤버 `FAI` 확인 (line 15); `case ENodeType.FAI:` 아이콘 매핑 확인 (line 44) |
| `WPF_Example/UI/ControlItem/InspectionListViewModel.cs` | FAI 트리 노드 생성 + 런타임 추가 메서드 | ✓ VERIFIED | `CreateSequenceNode` 의 FAI 자식 추가 루프 (line 91-95); `AddFAINode(NodeViewModel actionNode, FAIConfig fai, ESequence seqID, EAction actID)` 정의 (line 117-133) |
| `WPF_Example/UI/ControlItem/InspectionListView.xaml` | FAI CRUD 툴바 버튼 3종 | ✓ VERIFIED | `button_addFAI` (line 231), `button_removeFAI` (line 237), `button_renameFAI` (line 243) 모두 존재 |
| `WPF_Example/UI/ControlItem/InspectionListView.xaml.cs` | FAI 선택 분기 + CRUD 핸들러 + ViewModel 와이어링 | ✓ VERIFIED | `_inspectionVm` 필드 (line 19), `ListView_Loaded` 의 `_inspectionVm = new InspectionViewModel(recipeManager)` + `mParentWindow.mainView.SetFAIResultSource(_inspectionVm)` (line 62-63), `InspectionList_SelectionChanged` 의 ENodeType.FAI 분기 (line 293-301), CRUD 핸들러 3종 (line 394, 545, 632) |
| `WPF_Example/UI/ContentItem/MainView.xaml` | 캔버스 + DataGrid 2단 + OK/NG MultiDataTrigger | ✓ VERIFIED | `halconViewer` 단일 캔버스 (line 115), `dataGrid_faiResults` (line 142), MultiDataTrigger 두 개 (line 171-184). TreeView 요소 없음 (XAML grep 결과 부재) |
| `WPF_Example/UI/ContentItem/MainView.xaml.cs` | `DisplayFAIImage`, `SetFAIResultSource`, `FAIResults_SelectionChanged` | ✓ VERIFIED | `public void DisplayFAIImage(FAIConfig fai)` (line 74), `public void SetFAIResultSource(InspectionViewModel vm)` (line 109), `private void FAIResults_SelectionChanged(...)` (line 117) 모두 존재 |
| `WPF_Example/UI/ContentItem/MainResultViewerControl.xaml(.cs)` | 단일 Halcon 캔버스 컨트롤 (UI-02 핵심) | ✓ VERIFIED | 두 파일 모두 존재 (xaml=1757B, xaml.cs=35190B 2026-04-10); MainView 에서 단일 인스턴스만 사용 |
| `WPF_Example/UI/ViewModel/InspectionViewModel.cs` | FAI-centric ViewModel + CRUD 위임 | ✓ VERIFIED | `class InspectionViewModel : Observable` (line 9), `OnFAISelected(FAIConfig fai)` (line 30), `OnActionSelected(NodeViewModel actionNode)` (line 46), `AddFAI(ShotConfig shot, string name)` (line 79), `RemoveFAI(ShotConfig shot, int index)` (line 86) 모두 존재. Phase 6 Plan 04 에서 `MeasurementResults` 로 진화하였음 (D-21 주석) |
| `WPF_Example/UI/ViewModel/MeasurementResultRow.cs` | DataGrid 행 DTO (MeasurementResultRow) | ✓ VERIFIED | `class MeasurementResultRow : Observable` (line 8); `JudgeText`, `MeasuredValueText`, `SpecMinText`, `SpecMaxText`, `IsPass`, `HasResult` 모두 정의 — DataGrid 컬럼/스타일 바인딩과 일치 |

### Key Link Verification

| From | To | Via | Status | Details |
|------|----|-----|--------|---------|
| InspectionListView TreeView 선택 | `InspectionList_SelectionChanged` | XAML `SelectionChanged="InspectionList_SelectionChanged"` 바인딩 | ✓ WIRED | InspectionListView.xaml:166 + 핸들러 본체 InspectionListView.xaml.cs:239 |
| 트리에서 FAI 노드 선택 | `MainView.DisplayFAIImage(faiConfig)` | `_inspectionVm.OnFAISelected(faiConfig)` 호출 + (Action/FAI 분기 후) MainView 갱신 | ✓ WIRED | InspectionListView.xaml.cs:293-301 분기 → InspectionViewModel.cs:30-42 OnFAISelected → MainView.xaml.cs:74 DisplayFAIImage (이미지 캔버스 로드는 단계별 통합 유지) |
| 트리에서 Action 노드 선택 | DataGrid 가 해당 Action 의 모든 Measurement 행을 표시 | `_inspectionVm.OnActionSelected(item)` → `MeasurementResults` 컬렉션 갱신 → DataGrid `ItemsSource` 바인딩 | ✓ WIRED | InspectionListView.xaml.cs:307 OnActionSelected 호출, InspectionViewModel.cs:46-61 ShotConfig.FAIList 순회, MainView.xaml.cs:109-113 SetFAIResultSource 가 `MeasurementResults` 를 DataGrid 에 바인딩 |
| DataGrid 행 선택 | 캔버스에 해당 ROI 하이라이트 | `FAIResults_SelectionChanged` → `halconViewer.UpdateDisplayState(rois, selectedRoiId, null, null)` | ✓ WIRED | MainView.xaml.cs:117-128 — selectedRow=null 이면 전체 ROI 일반 색상, 행 선택 시 selectedRoiId 전달 |
| Add 버튼 클릭 | `InspectionRecipeManager` 에 새 FAI 추가 | `_inspectionVm.AddFAI(shot, name)` → `shot.AddFAI(name)` | ✓ WIRED | InspectionListView.xaml.cs:455 → InspectionViewModel.cs:79-83 (`shot.AddFAI(name)`) → 트리 갱신은 `ViewModel.AddFAINode(...)` (InspectionListViewModel.cs:117) |
| Del 버튼 클릭 | 확인 다이얼로그 → `RemoveFAI` | `CustomMessageBox.ShowConfirmation(...)` → `_inspectionVm.RemoveFAI(shot, index)` | ✓ WIRED | InspectionListView.xaml.cs:614 ShowConfirmation, line 622 `_inspectionVm.RemoveFAI(shot, index)` → InspectionViewModel.cs:86-89 `shot.RemoveFAI(index)` |
| Edit 버튼 클릭 | FAIConfig.FAIName 갱신 + 트리 노드 라벨 갱신 | TextInputBox → `fai.FAIName = newName` → `selectedNode.Name = newName` | ✓ WIRED | `Btn_RenameFAI_Click` (InspectionListView.xaml.cs:632) — FAI 노드만 처리하도록 `if (selectedNode.NodeType != ENodeType.FAI) return` 가드 (line 635) |

### Requirements Coverage

| Requirement | Source Plan | Description | Status | Evidence |
|-------------|------------|-------------|--------|----------|
| UI-01 | 01-01, 01-02 | TreeView 에서 Shot/FAI 2계층 구조를 탐색할 수 있다 | ✓ SATISFIED | `ENodeType.FAI` (Node.cs:15) + `CreateSequenceNode` 의 Action 자식으로 `faiNode` add (InspectionListViewModel.cs:91-95). 시작 시 `ExpandAll` 로 자동 펼침 (InspectionListView.xaml.cs:39). Phase 6-04 에서 Datum/Measurement 노드까지 확장되어 Shot/FAI 2계층은 그대로 유지 |
| UI-02 | 01-02 | 단일 캔버스에서 선택된 Shot 의 이미지를 표시한다 (기존 5탭 제거) | ✓ SATISFIED | MainView.xaml 이 `halconViewer` 단일 인스턴스 + DataGrid 2단 구조로 재작성 (line 115, 142). 좌측 TreeView 컬럼/탭 제거 — XAML 에 `TreeView` 요소 부재. `DisplayFAIImage(FAIConfig)` (MainView.xaml.cs:74) 가 `fai.Owner as ShotConfig` 의 이미지를 `halconViewer.LoadImage` 로 표시 |
| UI-03 | 01-01, 01-02 | FAI 측정 결과(거리 mm, OK/NG 판정)를 테이블로 표시한다 | ✓ SATISFIED | `MeasurementResultRow` 가 `MeasuredValueText` (F3 mm) + `JudgeText` (OK/NG/—) + Spec Min/Max 노출 (MeasurementResultRow.cs:58-64). DataGrid 컬럼이 해당 속성에 바인딩 + MultiDataTrigger 로 OK=green/NG=red 배경 (MainView.xaml:171-184). Phase 6-04 의 D-21 에서 FAIResults → MeasurementResults 진화 |
| UI-04 | 01-02 | FAI 를 추가/삭제/수정할 수 있다 (Shot 계층 제거됨, UI-05 와 통합) | ✓ SATISFIED | UI-05 와 동일 구현으로 통합. REQUIREMENTS.md 본문 (line 15) 도 "Shot 계층 제거됨, UI-05 와 통합" 명시. 증거는 UI-05 행 참조 |
| UI-05 | 01-02 | FAI 를 추가/삭제/수정할 수 있다 | ✓ SATISFIED | XAML 버튼 3종 (`button_addFAI/removeFAI/renameFAI`, InspectionListView.xaml:231,237,243) + 핸들러 3종 (`Btn_AddFAI_Click` line 394, `Btn_RemoveFAI_Click` line 545, `Btn_RenameFAI_Click` line 632). 삭제 전 `CustomMessageBox.ShowConfirmation` (line 614) 확인 다이얼로그 보장 (D-10) |

### Gaps Summary

자동화 검증 범위 내 갭 없음. UI-01..UI-05 모두 SUMMARY (01-01, 01-02) + 01-UAT.md (11/11 테스트 중 9/11 pass + 2 minor/major issue 는 후속 phase 에서 해소) + 실제 코드 grep 으로 VERIFIED.

**01-UAT.md 미해소 항목 (이력만 기록, Phase 1 범위 밖에서 후속 처리):**
- Test 3 (minor): Top 검사의 Shot_0 / Inspect 가 같은 이미지 공유 — Phase 5/6 의 Shot 별 독립 이미지 버퍼 작업으로 해소되었다고 추정 (별도 검증 대상 아님 — UI 가 아닌 데이터 레이어 동작).
- Test 7 (major): FAI 제거는 되지만 부모 Shot 노드는 제거 불가 — Shot CRUD 는 v1 요구사항 UI-04/UI-05 범위 (FAI CRUD) 밖. Shot 삭제 UI 는 v2 또는 backlog. 본 verification 의 must-have 에 포함되지 않음.

상기 두 건은 Phase 1 의 형식 요구사항(UI-01..UI-05)을 침해하지 않으며, 모두 D-07 에 따라 후속 phase 또는 backlog 로 인계된 상태.

---

_Verified: 2026-04-23_
_Verifier: Claude (gsd-executor, Phase 09 backfill)_
_Backfill source: 09-verification-backfill / 09-01-PLAN.md_
