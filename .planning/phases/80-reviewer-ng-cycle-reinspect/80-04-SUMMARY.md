---
phase: 80-reviewer-ng-cycle-reinspect
plan: 04
subsystem: reviewer-reinspect
tags: [reviewer, mvvm, halcon, test-find, tree-selection, run]
dependency graph:
  requires:
    - phase: 80-02
      provides: DatumTestFindService.TryRunFromTeachingImages(seq, datum, bHoldForManualRun, out szError) — 대화상자 없는 공용 Test Find
    - phase: 80-03
      provides: ReviewerReinspectService.LoadForRow 잠금 안 순서(스냅샷 → ApplyPartPaths → ClearDatumTransforms → 버퍼 → 리뷰어 선), bNeedsDatum/bDatumComplete/szNgDatumRef 지역 값
  provides:
    - "리뷰어에서 불러오면 리뷰어 창은 최소화(닫지 않음)되고 메인이 앞으로 오며, NG Shot·측정 노드가 선택돼 파라미터가 바로 보인다(D-80-04/05)"
    - "메뉴의 리뷰어 버튼을 다시 누르면 최소화가 풀리고 앞으로 온다 — 다른 NG 행을 이어서 고를 수 있다(D-80-04)"
    - "불러오기·해제 때 MainView 의 '같은 Shot 재선택 건너뛰기' 캐시를 잊어, 옛 사진이 남지 않는다(D-80-05)"
    - "기준점 사진이 짝 맞게 들어왔을 때만 불러오기 직후 대화상자 없이 자동 Test Find 1회 — 시퀀스는 시작하지 않는다, 실패하면 상태 줄 안내+로그(D-80-06/D-80-09)"
    - "측정·FAI 노드를 고른 채 RUN 을 눌러도 그 노드가 속한 Shot 이 실행된다 — 이전에는 오류(D-80-05/06)"
    - "리뷰어 사진 사용 중에는 RUN 의 OfflineInspectMode 확인창을 띄우지 않는다(D-80-00) — 그 외에는 그대로"
  affects:
    - 80-05 (실기 UAT U-2: 이어받기·자동 Test Find, U-4: RUN 한 번)
tech-stack:
  added: []
  patterns:
    - "NodeViewModel.Param 참조 동일성 재귀 탐색(FindNodeByParam) — measurement → FAI → shot 순서 폴백, 이미 있는 ExpandParents() 재사용"
    - "Dispatcher Background 지연 선택(SelectNodeDeferred) — 트리 컨테이너 생성 중 동기 재선택 크래시(:880-885 기존 경고) 회피, 같은 노드도 풀었다 다시 걸어 재렌더"
    - "활성→비활성 전이 1회 이벤트(ReviewerReinspectViewModel.Released) — RefreshFromService 가 bWasActive 를 미리 캡처해 상태 전환 순간만 감지, 매 상태 갱신마다 반복 발화하지 않음"
    - "부모 Shot(Action) 노드로 치환 후 기존 RUN 파이프라인 재사용(ResolveRunNodeForSelection) — ResolveRunnableAction·Start 경로는 한 줄도 안 바꾼다"
    - "IsActive 게이트로 확인창 조건부 생략(bAskOfflineConfirm) — 리뷰어가 이미 연 오프라인 모드에만 적용, 수동 오프라인 모드의 확인창은 그대로"
key-files:
  created: []
  modified:
    - WPF_Example/UI/ControlItem/InspectionListView.xaml.cs
    - WPF_Example/MainWindow.xaml.cs
    - WPF_Example/UI/ContentItem/MainView.xaml.cs
    - WPF_Example/UI/Reviewer/ReviewerReinspectViewModel.cs
    - WPF_Example/Custom/Sequence/Inspection/ReviewerReinspectService.cs
key-decisions:
  - "MainWindow.OnReviewerPhotosLoaded 의 기존 주석(80-01 이 남긴 '리뷰어 최소화·트리 선택은 80-04' 플레이스홀더)을 그대로 두고 코드만 추가했다 — 처음에 주석을 실제 내용으로 갱신했더니 Task 1 <verify> 의 deleted=0(네 파일 전부) 이 깨져(deleted=1) 재정정. 동작은 계획과 동일, 파일 내 주석 문구만 원본 유지."
requirements-completed: [D-80-04, D-80-05, D-80-06, D-80-00, D-80-16, D-80-17]

coverage:
  - id: D1
    description: "리뷰어에서 불러오면 리뷰어 창이 최소화되고 메인이 앞으로 오며, 메뉴 버튼으로 다시 열면 복원·활성화된다(D-80-04)"
    requirement: "D-80-04"
    verification:
      - kind: other
        ref: "MSBuild Debug|x64 + git diff 기반 acceptance_criteria 스크립트 (80-04-PLAN.md Task 1 <verify> — nav_min/popup_restore/popup_activate)"
        status: pass
    human_judgment: true
    rationale: "코드·빌드·정적 diff 검증까지 확인했으나 실제 창 최소화/복원이 화면에서 자연스럽게 보이는지는 사람 눈으로 봐야 하는 시각 판단 — 80-05 실기 UAT(U-2)로 이연(Debug 빌드는 라이선스 키가 없어 실행 불가, uat_release_build_only)"
  - id: D2
    description: "NG Shot·측정(FAI) 노드가 트리에서 펼쳐지고 선택되어 속성창에 파라미터가 바로 보인다 — 재진입 크래시 회피를 위해 Dispatcher Background 로 지연(D-80-05)"
    requirement: "D-80-05"
    verification:
      - kind: other
        ref: "select_def/deferred/expand/reselect grep — 80-04-PLAN.md Task 1 <verify>"
        status: pass
    human_judgment: true
    rationale: "정적 검증(메서드 존재·지연 배선·재선택 로직)까지 확인했으나 실제 트리에서 파라미터 그리드가 새 값으로 갱신되는지는 화면 확인 필요 — 80-05 실기 UAT(U-2)로 이연"
  - id: D3
    description: "불러오기·해제 시 MainView 의 '같은 Shot 재선택 시 재로드 건너뛰기' 캐시를 잊어 옛 사진이 남지 않는다"
    requirement: "D-80-05"
    verification:
      - kind: other
        ref: "forget_def/forget_stmts/released_sub/released_evt grep — 80-04-PLAN.md Task 1 <verify>"
        status: pass
    human_judgment: false
  - id: D4
    description: "기준점 사진이 완전할 때만 불러오기 직후 대화상자 없는 자동 Test Find 1회 — 시퀀스를 시작하지 않고, 실패하면 상태 줄 안내+로그(D-80-06/D-80-09)"
    requirement: "D-80-06"
    verification:
      - kind: other
        ref: "helper_call/run_call/eligible/no_seq_start/enum_def/order_ok/hint grep — 80-04-PLAN.md Task 2 <verify>"
        status: pass
    human_judgment: true
    rationale: "정적 검증(호출 순서·시퀀스 미시작·엘리저빌리티 조건)까지 확인했으나 실제 기준점이 화면에서 올바른 위치로 찾아지는지는 시각 판단 — 80-05 실기 UAT(U-2)로 이연"
  - id: D5
    description: "측정·FAI 노드를 고른 채 RUN 을 눌러도 그 노드가 속한 Shot 이 실행되고, Sequence·Shot·Datum 노드의 RUN 은 그대로다"
    requirement: "D-80-05"
    verification:
      - kind: other
        ref: "resolve_line/ask_bool/ask_if/resolve_before_check/helper/helper_action/same_resolve_runnable grep — 80-04-PLAN.md Task 3 <verify>"
        status: pass
    human_judgment: true
    rationale: "정적 diff·md5 비교로 ResolveRunnableAction 무변경과 노드 치환 로직을 확인했으나 실제 RUN 으로 그 Shot 이 재검사되는지는 실기 확인 필요 — 80-05 실기 UAT(U-4)로 이연"
  - id: D6
    description: "리뷰어 사진 사용 중에는 RUN 의 OfflineInspectMode 확인창을 띄우지 않는다 — 리뷰어가 켠 오프라인 모드이고 상태 줄이 이미 보여준다(D-80-00)"
    requirement: "D-80-00"
    verification:
      - kind: other
        ref: "ask_bool/ask_if grep (ReviewerReinspectService.IsActive 게이트) — 80-04-PLAN.md Task 3 <verify>"
        status: pass
    human_judgment: true
    rationale: "조건식 자체는 정적으로 검증했으나 실제 리뷰어 사진 사용 중 RUN 이 확인창 없이 진행되는지는 실기 확인 — 80-05 실기 UAT(U-4)로 이연"
  - id: D7
    description: "가독성 규칙 준수 — 새/수정 파일 추가 줄에 삼항·null 병합·null 조건·switch 식·날짜주석 0, 조건 3개 이상 없음, ResolveRunnableAction md5 무변경"
    requirement: "D-80-16"
    verification:
      - kind: other
        ref: "ternary/coalesce/nullcond/switchexpr/datesig/qmark/logic3 grep (전 파일 0) + same_resolve_runnable=1 — 80-04-PLAN.md 전 Task <verify>"
        status: pass
    human_judgment: false

# Metrics
duration: 40min
completed: 2026-09-18
status: complete
---

# Phase 80 Plan 04: 리뷰어 → 메인 이어받기 + 자동 Test Find + RUN 한 번 Summary

리뷰어에서 불러온 뒤 사용자가 파라미터만 고치고 RUN 한 번만 누르면 되도록 메인 화면을 이어받는 마지막 연결 3개를 배선했다: 리뷰어 창은 최소화(닫지 않음)되고 메인이 앞으로 오며 NG Shot·측정 노드가 선택되어 파라미터가 바로 보이고(D-80-04/05), 기준점 사진이 짝 맞게 들어왔을 때만 대화상자 없는 Test Find 가 자동으로 한 번 돈다(D-80-06). 측정·FAI 노드를 고른 채 RUN 을 누를 수 있게 했고, 리뷰어 사진 사용 중에는 오프라인 확인창을 생략한다(D-80-00).

## Performance

- **Duration:** ~40 min
- **Completed:** 2026-09-18
- **Tasks:** 3
- **Files modified:** 5 (신규 파일 없음)

## Accomplishments

- **리뷰어 최소화·복원 + NG 노드 자동 선택** (`MainWindow.OnReviewerPhotosLoaded`, `InspectionListView.SelectShotAndMeasurement`): 불러오기 성공 시 `mReviewerWindow.WindowState = WindowState.Minimized`(소유 창이라 메인 뒤로 못 가서 최소화로 비킴) 후 `Activate()`, `mainView.ForgetDisplayedShotImage()`, `inspectionList.SelectShotAndMeasurement(result.LiveShot, result.LiveFai, result.LiveMeasurement)` 를 순서대로 호출한다. `SelectShotAndMeasurement` 는 `FindNodeByParam` 으로 트리를 재귀 탐색해(측정 → FAI → Shot 순서 폴백, `ReferenceEquals(node.Param, target)`) 대상 노드를 찾고, 이미 있는 `NodeViewModel.ExpandParents()` 로 조상을 펼친 뒤 `Dispatcher.BeginInvoke(DispatcherPriority.Background, ...)` 로 한 틱 늦춰 `SelectNodeDeferred` 를 실행한다 — 트리 컨테이너 생성 중 동기 재선택 크래시(기존 :880-885 경고와 같은 회피). `SelectNodeDeferred` 는 이전 선택을 먼저 풀고(`prev.IsSelected = false`) 새로 선택해(`node.IsSelected = true`), 같은 노드를 다시 선택해도 새 사진·선이 다시 그려지게 한다. 메뉴의 PopupView Reviewer 케이스는 최소화 상태면 `WindowState.Normal` 로 복원한 뒤 `Show()`·`Activate()` 해 다른 NG 행을 이어서 고를 수 있게 한다.
- **Shot 사진 캐시 잊기** (`MainView.ForgetDisplayedShotImage`): 본문 1문장 `_lastDisplayedImageShot = null;` — 불러오거나 해제한 뒤 같은 Shot 을 다시 눌러도 캐시된 옛 사진이 남지 않는다. 새 `ReviewerReinspectViewModel.Released` 이벤트(활성 → 비활성으로 바뀌는 순간에만 1회 발화, `RefreshFromService` 가 `bWasActive` 를 먼저 캡처)에 생성자에서 구독했다.
- **자동 Test Find** (`ReviewerReinspectService.RunAutoTestFind`, 새 `EReviewerTestFindResult` enum): `LoadForRow` 가 `seq.ClearDatumTransforms()` + 경로·버퍼·선 적용이 끝난 뒤(`s_state = newState;` 다음), `bool bRunTestFind = bNeedsDatum && bDatumComplete;` 로 기준점 사진이 완전할 때만 `DatumTestFindService.TryRunFromTeachingImages(seq, datum, true, out szError)`(대화상자 없음, `bHoldForManualRun=true` — 다음 수동 RUN 이 재사용)를 호출한다. 이 서비스는 시퀀스를 시작하는 줄을 한 줄도 넣지 않았다(D-80-06 — RUN 은 사용자 직접). 결과는 `ReviewerReinspectState.TestFindResult` 에 저장되고 `[ReviewerLoad]` 불러옴 로그 끝에 " · 자동 Test Find 성공/실패/안 함" 이 붙는다. 실패 시 `ReviewerReinspectViewModel.BuildStatusText` 가 `HINT_TESTFIND_FAILED` 를 상태 줄 끝에 붙인다.
- **RUN 한 번 — 노드 해석 + 확인창 생략** (`InspectionListView.Btn_start_Click`, 새 `ResolveRunNodeForSelection`): 선택 노드를 `node = ResolveRunNodeForSelection(node);` 로 먼저 치환한다 — FAI·측정 노드면 부모 체인을 거슬러 첫 `ENodeType.Action` 조상을 찾아 반환하고, 그 외(Sequence·Shot·Datum)는 그대로 반환한다. 이 치환은 노드 종류 오류 검사·`ResolveRunnableAction` 호출보다 먼저 일어나므로 이제 측정·FAI 노드에서도 그 Shot 이 실행된다(이전에는 "Select a Sequence or Shot/Action node to run" 오류). `ResolveRunnableAction` 본문은 md5 완전 무변경. 오프라인 확인 조건은 `bool bAskOfflineConfirm = SystemHandler.Handle.Setting.OfflineInspectMode && !ReviewerReinspectService.IsActive;` 로 바뀌어, 리뷰어 사진 사용 중(`IsActive`)에는 확인창을 띄우지 않는다 — 수동으로 켠 오프라인 모드의 확인창은 그대로.

## Task Commits

Each task was committed atomically:

1. **Task 1: 메인 화면 이어받기 — 리뷰어 최소화·복원, NG Shot·측정 노드 자동 선택, 사진 캐시 잊기** - `58b50da6` (feat)
2. **Task 2: 자동 Test Find — 기준점 사진이 짝 맞게 들어왔을 때만, 대화상자 없이, RUN 은 사용자 몫** - `1140fa0e` (feat)
3. **Task 3: RUN 한 번 — 측정·FAI 노드에서도 그 Shot 실행, 리뷰어 사진 사용 중에는 오프라인 확인창 생략** - `c7b32b75` (feat)

**Plan metadata:** (이 커밋 — docs)

_Note: TDD 아님 — 전부 auto 타입, 각 task 커밋마다 build_exit=0 cs_errors=0 확인 후 커밋._

## Files Created/Modified

- `WPF_Example/UI/ControlItem/InspectionListView.xaml.cs` — `SelectShotAndMeasurement(ShotConfig, FAIConfig, MeasurementBase)`, `SelectNodeDeferred(NodeViewModel)`, `FindNodeByParam(NodeViewModel, object)`, `ResolveRunNodeForSelection(NodeViewModel)` 추가. `Btn_start_Click` 에 노드 치환 1줄 + 오프라인 확인 조건 1줄 교체. `ResolveRunnableAction` 등 나머지 전부 무변경(md5 확인).
- `WPF_Example/MainWindow.xaml.cs` — `OnReviewerPhotosLoaded` 본문 확장(최소화·캐시 잊기·트리 선택 3줄 추가), `PopupView` 의 `EPageType.Reviewer` 케이스에 복원·Activate 2줄 추가.
- `WPF_Example/UI/ContentItem/MainView.xaml.cs` — `ForgetDisplayedShotImage()` 추가, 생성자에 `Released` 구독 1줄.
- `WPF_Example/UI/Reviewer/ReviewerReinspectViewModel.cs` — `public event Action Released;`, `HINT_TESTFIND_FAILED` const, `RefreshFromService` 본문 확장(bWasActive 캡처 + Released 발화), `BuildStatusText` 에 안내 1줄 추가.
- `WPF_Example/Custom/Sequence/Inspection/ReviewerReinspectService.cs` — `EReviewerTestFindResult` enum, `ReviewerReinspectState.TestFindResult`(+Clone), `RunAutoTestFind(InspectionSequence, string, bool)` 추가, `LoadForRow` 본문 확장(자동 Test Find 호출 + 로그 문구 확장).

## Decisions Made

- `MainWindow.OnReviewerPhotosLoaded` 위의 기존 주석("리뷰어 최소화·트리 선택은 80-04")을 처음엔 실제 구현 내용으로 고쳐 썼다가, Task 1 `<verify>` 의 "네 파일 deleted=0" 기준이 깨져(주석 교체 = 1줄 삭제) 원래 주석 문구로 되돌리고 코드만 추가했다 — 동작은 계획과 완전히 동일, 주석 문구만 80-01 원본 유지.

## Deviations from Plan

None - plan executed exactly as written (위 "Decisions Made" 1건은 코드 변경이 아니라 검증 기준을 맞추기 위한 주석 문구 되돌림).

## Verification Results (원문)

**Task 1** (`InspectionListView.xaml.cs`/`MainWindow.xaml.cs`/`MainView.xaml.cs`/`ReviewerReinspectViewModel.cs`):
```
build_exit=0 cs_errors=0
InspectionListView.xaml.cs deleted=0 ternary=0 coalesce=0 nullcond=0 switchexpr=0 datesig=0 qmark=0 logic3=0
MainWindow.xaml.cs deleted=0 ternary=0 coalesce=0 nullcond=0 switchexpr=0 datesig=0 qmark=0 logic3=0
MainView.xaml.cs deleted=0 ternary=0 coalesce=0 nullcond=0 switchexpr=0 datesig=0 qmark=0 logic3=0
ReviewerReinspectViewModel.cs deleted=0 ternary=0 coalesce=0 nullcond=0 switchexpr=0 datesig=0 qmark=0 logic3=0
select_def=1 deferred=1 expand=1 reselect=1
nav_select=1 nav_min=1 nav_forget=1 popup_restore=1 popup_activate=1
forget_def=1 forget_stmts=1 released_sub=1 released_evt=1
```

**Task 2** (`ReviewerReinspectService.cs`/`ReviewerReinspectViewModel.cs`):
```
build_exit=0 cs_errors=0
ReviewerReinspectService.cs ternary=0 coalesce=0 nullcond=0 switchexpr=0 datesig=0 qmark=0 logic3=0
ReviewerReinspectViewModel.cs ternary=0 coalesce=0 nullcond=0 switchexpr=0 datesig=0 qmark=0 logic3=0
helper_call=1 run_call=1 eligible=1 no_seq_start=0 enum_def=1
order_ok=1
hint=2
```

**Task 3** (`InspectionListView.xaml.cs`):
```
build_exit=0 cs_errors=0
il deleted=1 deleted_is_offline_if=1 ternary=0 coalesce=0 nullcond=0 switchexpr=0 datesig=0 qmark=0 logic3=0
resolve_line=1 ask_bool=1 ask_if=1 resolve_before_check=1
helper=1 helper_action=1
same_resolve_runnable=1
```

**최종 재확인** (전체 파일 Debug|x64 빌드): `build_exit=0 cs_errors=0`

## New Symbols (실제 이름)

- `InspectionListView`: `public void SelectShotAndMeasurement(ShotConfig liveShot, FAIConfig liveFai, MeasurementBase liveMeasurement)`, `private void SelectNodeDeferred(NodeViewModel node)`, `private NodeViewModel FindNodeByParam(NodeViewModel node, object target)`, `private static NodeViewModel ResolveRunNodeForSelection(NodeViewModel node)`
- `ReviewerReinspectService`: `public enum EReviewerTestFindResult { NotRun, Succeeded, Failed }`, `private static EReviewerTestFindResult RunAutoTestFind(InspectionSequence seq, string szDatumRef, bool bEligible)`
- `ReviewerReinspectState.TestFindResult`(`EReviewerTestFindResult`, 기본 `NotRun`)
- `ReviewerReinspectViewModel`: `public event Action Released;`, `HINT_TESTFIND_FAILED`(const)
- `MainView`: `public void ForgetDisplayedShotImage()`

## 자동 Test Find 로그 문구 · 상태 줄 안내 (원문)

- `RunAutoTestFind` 자체 로그: `LOG_TAG + "자동 Test Find — " + seq.Name + " · " + szDatumRef + " 성공"` / `" 실패: " + szError` / 기준점 못 찾음: `"자동 Test Find — 기준점을 찾지 못함 · " + seq.Name + " · " + szDatumRef`
- `LoadForRow` 불러옴 로그(80-03 형식에 이번 plan 이 덧붙인 부분): `... + " · 기준점 사진 " + szDatumLogText + " · Z 후보 " + nZCandidateCount + "장 · 자동 Test Find " + szTestFindLogText` (`szTestFindLogText` ∈ { `"성공"`, `"실패"`, `"안 함"` })
- 상태 줄 안내(`HINT_TESTFIND_FAILED`): `"  (기준점 찾기 실패 — 기준점 노드에서 Test Find 로 확인)"`

## RUN 동작 변화 (한 줄 요약)

측정·FAI 노드를 고른 채 RUN 을 누르면 이전에는 "Select a Sequence or Shot/Action node to run" 오류가 났으나, 이제 `ResolveRunNodeForSelection` 이 그 노드의 부모 Shot(Action) 노드로 바꿔 기존 Shot 실행 경로(`ResolveRunnableAction`, 무변경)를 그대로 태워 그 Shot 이 재검사된다.

## Known Stubs

없음 — 이 plan 은 실제 배선·로직이며 스텁 데이터 없음. 실제 화면 동작(최소화·복원, 트리 선택·파라미터 표시, 자동 Test Find 성공/실패, RUN 결과)은 80-05 실기 UAT(U-2/U-4)로 이연됨(Debug 빌드는 라이선스 키가 없어 실행 불가 — 사용자 메모리 `uat_release_build_only` 참조).

## Threat Flags

없음 — 이 plan 의 위협 표면은 plan 의 `<threat_model>`(T-80-15~18)에 이미 등록되어 있고 이번 구현이 새 표면을 추가하지 않았다. T-80-15(기준점 캐시 잔류)는 `order_ok=1`(ClearDatumTransforms 뒤 자동 Test Find)로, T-80-16(오프라인 확인창 생략 오인)은 `ReviewerReinspectService.IsActive` 게이트로, T-80-17(트리 재진입 크래시)은 Dispatcher Background 지연으로, T-80-18(서비스 자체 시퀀스 시작)은 `no_seq_start=0` 으로 각각 완화가 코드에 존재함을 확인했다.

## Issues Encountered

None.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- D-80-04/05/06/00 이 코드로 존재하고 Debug|x64 빌드가 통과한다. 실제 동작(리뷰어 최소화·복원, 트리 선택·파라미터, 자동 Test Find, RUN 한 번)은 80-05 실기 UAT 항목 U-2(이어받기·자동 Test Find), U-4(RUN 한 번)로 확인한다.
- 80-05 는 이 phase(80-01~04)의 전체 흐름 실기 검증 plan — base 해시부터 이번 plan 까지의 커밋을 모두 포함해 사용자 승인을 받는다.

## Self-Check

- [x] `WPF_Example/UI/ControlItem/InspectionListView.xaml.cs` — FOUND
- [x] `WPF_Example/MainWindow.xaml.cs` — FOUND
- [x] `WPF_Example/UI/ContentItem/MainView.xaml.cs` — FOUND
- [x] `WPF_Example/UI/Reviewer/ReviewerReinspectViewModel.cs` — FOUND
- [x] `WPF_Example/Custom/Sequence/Inspection/ReviewerReinspectService.cs` — FOUND
- [x] commit `58b50da6` — FOUND in git log
- [x] commit `1140fa0e` — FOUND in git log
- [x] commit `c7b32b75` — FOUND in git log
- [x] Debug|x64 build exit 0, cs_errors 0 (최종 재확인)

## Self-Check: PASSED

---
*Phase: 80-reviewer-ng-cycle-reinspect*
*Completed: 2026-09-18*
*Base hash: a19ce6322ba93e6c4bbe08854a6a5dc6aabb6590*
