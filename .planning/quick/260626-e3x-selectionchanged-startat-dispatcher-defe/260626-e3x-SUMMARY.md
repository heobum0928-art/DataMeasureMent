---
quick_id: 260626-e3x
slug: selectionchanged-startat-dispatcher-defe
status: complete
date: 2026-06-26
commits:
  - efd5894
---

# Quick 260626-e3x — 트리 SelectionChanged StartAt 재진입 크래시 수정

## 증상 (사용자 보고, 2026-06-25)
프로그램 실행 중 트리 노드 클릭 → 트리 뒤섞임 + 크래시:
`System.InvalidOperationException: Cannot call StartAt when content generation is in progress`
(Panel.GenerateChildren → ItemContainerGenerator.OnRefresh ← WeakEventManager).

## 진단
- 스택의 `OnRefresh`(컬렉션 Reset) + WeakEventManager = 컨테이너 생성 도중 ItemsControl 재진입.
- 교차 스레드 가능성 배제: 레시피 변경 경로 `InspectionListView.OnLoadRecipe → RebuildTree → ReloadChildren →
  Clear()(Reset)`는 `MainWindow.OnLoadRecipe`(MainWindow.xaml.cs:257-261)에서 이미 `Dispatcher.BeginInvoke`로
  UI 스레드 마샬링됨.
- 결론 = **UI 스레드 재진입**: `InspectionList_SelectionChanged`(InspectionListView.xaml.cs:577~)가
  TreeListBox(가상화, `IsDeferredScrollingEnabled=True`) 생성 도중 **동기로** ItemsSource/SelectedObject 를 재교체:
  - `_inspectionVm.ClearResults()` → `MeasurementResults = new ObservableCollection(...)` (소스 교체)
  - `ParamEditor.SelectedObject = null → 새 객체` ×3 (force rebind)
  - `RestoreDatumOverlayFromTeach(datumCfg)` 동기 재티칭 → `LastFindSucceeded` → NodeViewModel 알림
  → generator 가 StartAt 재진입 → 크래시.

## 수정
`WPF_Example/UI/ControlItem/InspectionListView.xaml.cs` InspectionList_SelectionChanged:
- 무거운 선택 처리 본문(노드별 분기 전체)을 `Dispatcher.BeginInvoke(DispatcherPriority.Background, ...)` 로 래핑
  → TreeListBox 가 현재 컨테이너 생성 패스를 끝낸 뒤 실행 → 재진입 차단.
- 지연 콜백은 `list.SelectedItem` 을 **재조회**(빠른 연속 선택 시 최신만 적용, stale 방지).
- 상단 버튼 비활성화(즉시 피드백)는 동기 유지. `_isRebinding` 가드 set/reset 은 콜백 내에서 그대로 유지.

## 검증
- ✅ MSBuild Debug/x64 빌드 PASS (DatumMeasurement.exe 생성).
- ⏳ **재현기 부재 → 실환경 UAT 필요** (아래).

## UAT (사용자)
1. 검사 실행 포함 다양한 상태에서 트리 노드를 빠르게/반복 클릭 → StartAt 크래시 재발 없는지.
2. 선택 시 PropertyGrid·오버레이·버튼 활성화가 정상 갱신되는지(한 프레임 지연 허용, 기능 동일).
3. Datum/Measurement/FAI/Shot/Sequence 각 노드 선택 동작 회귀 0 확인 (특히 Datum 재티칭 표시, DualImage swap UI).

## 비고
지연(BeginInvoke Background)은 통상 다음 idle 틱에 실행 → 체감 지연 없음. 회귀 시 단일 커밋(efd5894) 되돌리기 용이.
