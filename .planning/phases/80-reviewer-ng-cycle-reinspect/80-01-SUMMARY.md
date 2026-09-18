---
phase: 80-reviewer-ng-cycle-reinspect
plan: 01
subsystem: reviewer-reinspect
tags: [reviewer, mvvm, offline-inspect-mode, recipe-protection, tracer]
dependency graph:
  requires: []
  provides:
    - ReviewerReinspectService (스냅샷/적용/복원/저장 보호/자동 해제 정적 서비스)
    - ReviewerReinspectViewModel (버튼 활성/이유/상태 줄 VM, 싱글턴 Instance)
    - SavedCycleRerunPlanner.BuildPartForSingleCycle (단일 사이클 → 부품 진입점)
  affects:
    - WPF_Example/UI/Reviewer/ReviewerWindow.xaml(.cs)
    - WPF_Example/UI/ContentItem/MainView.xaml(.cs)
    - WPF_Example/MainWindow.xaml.cs
    - WPF_Example/Custom/SystemHandler.cs
    - WPF_Example/Custom/Sequence/Inspection/RepeatRunService.cs
tech-stack:
  added: []
  patterns:
    - "정적 서비스 + lock(s_lock) 스냅샷/복원 (RepeatRunService.SavedCycleOverrideSnapshot 과 동형)"
    - "MVVM 훅 속성(MainNavigator/AlertPresenter 선례 AlignVerifyViewModel Raise 패턴)"
    - "저장 감싸기 — RunWithOriginalPaths(Func<bool>) 로 저장 순간만 원복 후 finally 재주입"
key-files:
  created:
    - WPF_Example/Custom/Sequence/Inspection/ReviewerReinspectService.cs
    - WPF_Example/UI/Reviewer/ReviewerReinspectViewModel.cs
  modified:
    - WPF_Example/Custom/Sequence/Inspection/RepeatRunService.cs
    - WPF_Example/DatumMeasurement.csproj
    - WPF_Example/UI/Reviewer/ReviewerWindow.xaml
    - WPF_Example/UI/Reviewer/ReviewerWindow.xaml.cs
    - WPF_Example/UI/ContentItem/MainView.xaml
    - WPF_Example/UI/ContentItem/MainView.xaml.cs
    - WPF_Example/MainWindow.xaml.cs
    - WPF_Example/Custom/SystemHandler.cs
decisions:
  - "Task 1(tracer) 커밋(55a5085f)에 Task 2 몫인 ReviewerReinspectService.RunWithOriginalPaths/HandleRecipeChanged 본문이 이미 포함됨 — 이번 이어달리기에서는 그 두 메서드를 다시 만들지 않고, 세 호출부(MainWindow.SaveRecipe/ctor/Window_Closing, Custom/SystemHandler.cs MainRun $TEST)만 연결했다"
  - "저장 감싸기는 C# 7.2 식 람다(`() => ...SaveRecipe(...)`)로 SequenceHandler.SaveRecipe 호출 자체는 바꾸지 않았다(계획 지시대로)"
metrics:
  duration: "~25분 (이어달리기, Task 2만)"
  completed: 2026-09-18
status: complete
---

# Phase 80 Plan 01: 리뷰어 사진 불러오기 tracer + 운영 레시피 보호 Summary

리뷰어 NG 행 → 버튼 → 메인 화면 Shot 사진 교체 + 상태 줄/[해제] 로 이어지는 가장 얇은 끝-끝 경로(tracer)를 만들고, 같은 plan 안에서 저장·PLC 자동검사·레시피 변경·프로그램 종료 4경로 모두 리뷰어 사진이 운영 레시피/실검사로 새지 않도록 잠갔다.

## What Was Built

### Task 1 (tracer, 이미 완료 — commit 55a5085f)

- `ReviewerReinspectService.cs`(신규, `ReringProject.Sequence`): 활성 스냅샷 1개(`s_snapshot`)를 `lock(s_lock)` 으로 보호. `CheckRow`(버튼 비활성 이유 판정) → `LoadForRow`(부품 빌드 → 스냅샷/원복 → Shot·Z후보 경로 주입 → OfflineInspectMode 메모리 전용 켜기 → `ClearDatumTransforms` → HImage 버퍼 로드 → Trace 로그 `[ReviewerLoad] 불러옴 — ...`) → `Release`/`ReleaseByUser`(원래 경로·모드 복원, MainRun 스레드 보호를 위해 `ReleaseCore` 전체가 try/catch).
- `ReviewerReinspectViewModel.cs`(신규, `ReringProject.UI`, 싱글턴 `Instance`): `CanApply`/`DisableReasonText`/`StatusText`/`IsActive` 를 서비스 상태에서 조립. `EvaluateSelection`/`ApplySelection`/`ReleaseByUser` 로 창이 부른다.
- `RepeatRunService.SavedCycleRerunPlanner.BuildPartForSingleCycle` — 고른 사이클 1개만으로 `SavedCycleRerunPart` 를 만드는 진입점(기존 `BuildPlan`/`ValidatePart`/반복검사 코드는 한 줄도 바뀌지 않음).
- `ReviewerWindow.xaml(.cs)`: NG 원인 패널 바로 아래 "이 사진으로 파라미터 수정" 버튼 1개 + 이유 한 줄. code-behind 핸들러 본문은 `_reinspectVm.ApplySelection(...)` 1문장.
- `MainView.xaml(.cs)`: 툴바 Row 2 에 노란 상태 줄(`{Binding StatusText}`, `DataTrigger IsActive`) + `[해제]` 버튼. code-behind 핸들러 본문은 `ReviewerReinspectViewModel.Instance.ReleaseByUser();` 1문장.
- `MainWindow.xaml.cs`: `MainNavigator = OnReviewerPhotosLoaded` 훅 등록, `OnReviewerPhotosLoaded` 는 `Activate();` 만 호출.

### Task 2 (이번 이어달리기 — commit 5fbfde1f)

운영 레시피 보호(D-80-10/11) — 서비스 메서드 자체는 Task 1 커밋에 이미 존재했으므로, 세 호출부만 연결:

- **`MainWindow.xaml.cs` 생성자**: `mSystemHandler.Sequences.OnRecipeChanged += ReviewerReinspectService.HandleRecipeChanged;` 추가(레시피 변경 시 자동 해제, D-80-11).
- **`MainWindow.SaveRecipe`**: `mSystemHandler.Sequences.SaveRecipe(name, ERecipeFileType.Ini)` 직접 호출 1줄을 `ReviewerReinspectService.RunWithOriginalPaths(() => mSystemHandler.Sequences.SaveRecipe(name, ERecipeFileType.Ini))` 로 감싸 저장하는 순간만 원래 사진 경로로 되돌리고, 저장 뒤(예외여도) 리뷰어 경로를 재적용한다(D-80-10). `IsSavedCycleRerunActive` 차단·mixedShot 차단은 그대로 그 위에 남음.
- **`MainWindow.Window_Closing`**: `RepeatRunService.RestoreActiveSavedCycleOverridesForShutdown();` 다음, `mSystemHandler.Release();`(Setting.Save 포함) **앞에** `ReviewerReinspectService.Release(ReviewerReinspectService.RELEASE_REASON_SHUTDOWN);` 추가 — 종료 시 켠 OfflineInspectMode 가 파일로 영속화되지 않게 반드시 그 앞.
- **`Custom/SystemHandler.cs` MainRun `case VisionRequestType.Test:`**: `AutoLogout` 다음, `ForceOfflineInspectModeOffForAutoTest();` **바로 앞**에 `ReviewerReinspectService.Release(ReviewerReinspectService.RELEASE_REASON_PLC_TEST);` 추가 — PLC 자동 검사가 리뷰어 사진·오프라인 모드로 돌지 않도록 `ProcessTest` 전에 먼저 되돌린다. `using ReringProject.Sequence;` 는 파일에 이미 있어 추가 using 불필요.

## Verification Results (plan-level, base = 55a5085f^ = d3193656)

- `build_exit=0 cs_errors=0` (Debug|x64, MSBuild)
- 새 파일 2개(`ReviewerReinspectService.cs`, `ReviewerReinspectViewModel.cs`) 전체 줄: `ternary=0 coalesce=0 nullcond=0 switchexpr=0 datesig=0 qmark=0`
- 기존 파일 8개 더한 줄: `ternary=0 coalesce=0 nullcond=0 switchexpr=0 datesig=0 qmark=0`
- 삭제 줄: `RepeatRunService.cs=0`(반복검사 코드 삭제 0, 성공기준 확인), `DatumMeasurement.csproj=0`, `ReviewerWindow.xaml(.cs)=0`, `MainView.xaml(.cs)=0`, `Custom/SystemHandler.cs=0`, `MainWindow.xaml.cs=1`(저장 호출 줄 1개만 교체)
- 배선 사슬: `chain_btn=1 chain_enabled=1 chain_reason=1 btn_text=1`, `chain_apply=1 chain_eval=3`, `chain_load>=1 chain_part>=1`
- 저장/해제: `save_wrap=1 recipe_sub=1 rerun_block_kept=1`, `closing_order_ok=1`(해제가 `mSystemHandler.Release()` 보다 먼저), `plc_order_ok=1`(해제 → `ForceOfflineInspectModeOffForAutoTest` → `ProcessTest` 순서), `save_finally=1 save_reapply=1 release_trycatch=1`

## Commits

- `55a5085f` — feat(80-01): tracer — 리뷰어 버튼 → 서비스 → 부품 진입점 → Shot 사진·OfflineInspectMode 적용 → 메인 상태 줄 + [해제]
- `5fbfde1f` — feat(80-01): task 2 — 저장 시 원래 사진 경로 + PLC $TEST·레시피 변경·프로그램 종료 자동 해제

## New Symbols (실제 이름)

- `EReviewerRowBlock { None, NoRow, NoPhoto, RecipeMismatch, SequenceNotFound, ShotNotFound, SequenceBusy, SavedCycleRerunActive }`
- `ReviewerReinspectLoadResult { IsLoaded, BlockReason, BlockDetail, LiveShot, LiveFai, LiveMeasurement }`
- `ReviewerReinspectState { IsActive, CycleTime, IndexNumber, ShotName, MeasurementName, Clone() }`
- `ReviewerReinspectService { LOG_TAG, RELEASE_REASON_USER, RELEASE_REASON_PLC_TEST, RELEASE_REASON_RECIPE_CHANGED, RELEASE_REASON_SHUTDOWN, StateChanged, IsActive, GetState(), CheckRow(...), LoadForRow(...), Release(string), ReleaseByUser(), RunWithOriginalPaths(Func<bool>), HandleRecipeChanged(object, RecipeChangedEventArgs) }`
- `ReviewerReinspectViewModel { Instance, IsActive, StatusText, CanApply, DisableReasonText, MainNavigator, EvaluateSelection(...), ApplySelection(...), ReleaseByUser() }`
- `SavedCycleRerunPlanner.BuildPartForSingleCycle(CycleResultDto, InspectionSequence, InspectionRecipeManager)`

## Main Screen Status Line Text (원문)

`"리뷰어 사진 사용 중 — MM-dd HH:mm 자재 N"` (자재번호 없으면 `" 자재번호 없음"`), `[해제]` 버튼.

## Deviations from Plan

### Auto-fixed / Structural notes

**1. [계획 순서와 실제 커밋 순서 차이] `RunWithOriginalPaths`/`HandleRecipeChanged` 본문이 Task 1 에서 이미 작성됨**
- **Found during:** Task 2 시작, 이어달리기로 `ReviewerReinspectService.cs` 현재 상태를 다시 읽으며 발견.
- **내용:** Task 1 을 실행한 이전 executor 가 서비스 파일 전체(648줄)를 한 번에 작성하면서 Task 2 몫인 두 메서드까지 포함시켰다. `git show 55a5085f` 로 확인 — 두 메서드가 그 커밋에 포함됨.
- **조치:** 다시 작성하지 않음(중복·충돌 방지). Task 2 는 계획 action (1) 을 건너뛰고 action (2)(3) 만 수행 — `MainWindow.xaml.cs`·`Custom/SystemHandler.cs` 세 호출부 연결.
- **영향:** 없음 — 검증 스크립트의 `save_finally=1 save_reapply=1 release_trycatch=1` 등은 코드 존재 여부만 확인하므로 그대로 통과. 파일 목록·심볼 목록·동작은 계획과 동일.
- **커밋:** 두 메서드 자체는 `55a5085f`, 이번 연결 배선은 `5fbfde1f`.

Deviation Rule 적용: 해당 없음(버그 수정·구조 변경 아님, 실행 순서상 이미 끝난 하위 작업을 건너뛴 것뿐).

## Auth Gates

없음.

## Known Stubs

없음. 이 plan 은 실제 배선·실제 서비스 구현이며 스텁 데이터 없음. 실제 화면 동작(클릭-스루) 확인은 80-05 의 장비 PC Release 빌드 UAT 로 이연됨(Debug 빌드는 라이선스 키가 없어 실행 불가 — 사용자 메모리 `uat_release_build_only` 참조).

## Threat Flags

없음 — 이 plan 의 위협 표면은 plan 의 `<threat_model>`(T-80-01~06) 에 이미 등록되어 있고 이번 구현이 새 표면을 추가하지 않았다.

## Self-Check

- [x] `WPF_Example/Custom/Sequence/Inspection/ReviewerReinspectService.cs` — FOUND
- [x] `WPF_Example/UI/Reviewer/ReviewerReinspectViewModel.cs` — FOUND
- [x] commit `55a5085f` — FOUND in git log
- [x] commit `5fbfde1f` — FOUND in git log
- [x] Debug|x64 build exit 0, cs_errors 0 (재확인, Task 2 반영 후)

## Self-Check: PASSED
