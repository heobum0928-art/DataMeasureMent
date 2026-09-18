---
phase: 80-reviewer-ng-cycle-reinspect
plan: 03
subsystem: reviewer-reinspect
tags: [reviewer, mvvm, halcon, datum-photos, overlay, repeat-run]
dependency graph:
  requires:
    - phase: 80-01
      provides: ReviewerReinspectService/ViewModel tracer (single-tick load, snapshot/restore, save/PLC/recipe/shutdown auto-release)
  provides:
    - "BuildPartForSingleCycle 이 고른 사이클의 같은 자재 전체(모든 tick)를 GroupIntoParts 재사용으로 묻는다(D-80-07)"
    - "SavedCycleRerunPlanner.IsDatumPhotoSetComplete — 기준점 사진 짝 완전성 판정(D-80-19), RepeatRunService/ReviewerReinspectService 공용"
    - "ReviewerReinspectService 가 기준점(완전할 때만)·두 장짜리·Z 후보 사진을 적용하고 모든 소유 Shot 의 화면 버퍼·FAI 오버레이를 채운다(D-80-07/08)"
    - "기준점 사진 없음 알림 1개(D-80-09) + 상태 줄 JPG/Z/기준점 안내(D-80-12/14)"
    - "리뷰어 사이클 전체 보기가 띄운 사진의 Shot 선만 그린다(선 겹침 버그 수정, 함께 처리 1)"
    - "RepeatRunService.StartFromSavedCycles 가 리뷰어 사진 사용 중에는 거절(D-80-11 보강 상호 배제)"
  affects:
    - 80-04 (리뷰어 자동 Test Find — 기준점 완전성 판정을 그대로 재사용 가능)
    - 80-05 (실기 UAT: U-2/U-3/U-7/U-10)
tech-stack:
  added: []
  patterns:
    - "GroupIntoParts 재사용 조회 — ValidatePart 를 타지 않는 신규 진입점이 반복검사와 같은 부품 그룹핑 알고리즘을 그대로 호출"
    - "역할 키 완전성 판정 분리 — SavedCycleRerunPlanner.IsDatumPhotoSetComplete 를 RepeatRunService(적용부)와 ReviewerReinspectService(리뷰어 적용부) 가 공용"
    - "선 복사 전용 정적 목록(s_lstOverlayFais) — 사용자 [해제]에서만 비우고, 백그라운드 해제는 다음 검사가 채우도록 목록만 비움"
key-files:
  created: []
  modified:
    - WPF_Example/Custom/Sequence/Inspection/RepeatRunService.cs
    - WPF_Example/Custom/Sequence/Inspection/ReviewerReinspectService.cs
    - WPF_Example/UI/Reviewer/ReviewerReinspectViewModel.cs
    - WPF_Example/UI/ViewModel/ReviewMeasurementRow.cs
    - WPF_Example/UI/Reviewer/ReviewerWindow.xaml.cs
key-decisions:
  - "IsDatumPhotoSetComplete 의 초안에서 File.Exists 체크를 별도 private 헬퍼(IsRoleKeyPhotoUsable)로 뺐다가, Task 1 <verify> 의 complete_exists 그레이(메서드 본문 안에서 직접 'File.Exists' 텍스트를 찾음)를 통과하지 못해 다시 인라인으로 합쳤다 — 동작은 동일, 검증 스크립트가 메서드 자기 본문만 보는 특성 때문."
  - "Task 2 <verify> 의 flag_in_apply 그레이 라인이 'private static void ApplyPartPaths' 를 찾는데 실제 시그니처는 80-01 이 이미 만든 'private static int ApplyPartPaths'(적용된 Shot 사진 수를 반환) 다 — 시그니처를 int 로 유지하고(반환값이 로그에 쓰이므로 바꾸면 회귀), 'int' 로 고친 grep 으로 재확인해 s_bDatumPhotosApplied 분기가 실제로 그 메서드 안에 있음을 검증했다. 코드는 계획 의도와 정확히 일치, plan verify 스크립트 문구만 시그니처와 다르다(문서 버그)."
requirements-completed: [D-80-07, D-80-08, D-80-09, D-80-12, D-80-14, D-80-19, D-80-16, D-80-17]

coverage:
  - id: D1
    description: "BuildPartForSingleCycle 이 반복검사와 같은 GroupIntoParts 규칙으로 고른 사이클이 속한 자재 전체를 묻는다(D-80-07) — 전날 폴더까지 본다"
    requirement: "D-80-07"
    verification:
      - kind: other
        ref: "MSBuild Debug|x64 + git diff 기반 acceptance_criteria 스크립트 (80-03-PLAN.md Task 1 <verify>)"
        status: pass
    human_judgment: true
    rationale: "코드·빌드·정적 diff 검증까지 확인했으나 실제 1738/1743 같은 자재 사진이 맞게 모이는지는 실기 데이터로 사람이 봐야 하는 판단 — 80-05 실기 UAT(U-2)로 이연(Debug 빌드는 라이선스 키가 없어 실행 불가, 사용자 메모리 uat_release_build_only)"
  - id: D2
    description: "IsDatumPhotoSetComplete — 기준점에 필요한 사진 역할(단일/H/V)이 하나라도 없거나 파일이 없으면 false(D-80-19)"
    requirement: "D-80-19"
    verification:
      - kind: other
        ref: "complete_def/complete_exists grep — 80-03-PLAN.md Task 1 <verify>"
        status: pass
    human_judgment: false
  - id: D3
    description: "RepeatRunService.StartFromSavedCycles 가 리뷰어 사진 사용 중이면 거절한다(D-80-11 보강)"
    requirement: "D-80-09"
    verification:
      - kind: other
        ref: "guard/guard_msg grep — 80-03-PLAN.md Task 1 <verify>"
        status: pass
    human_judgment: false
  - id: D4
    description: "기준점(완전할 때만)·두 장짜리·Z 후보 사진 적용 + 모든 소유 Shot 화면 버퍼 + 리뷰어 FAI 선 복사(D-80-07/08)"
    requirement: "D-80-07"
    verification:
      - kind: other
        ref: "complete_call/datum_apply/dual_apply/overlays/overlay_copy/zmiss grep — 80-03-PLAN.md Task 2 <verify>"
        status: pass
    human_judgment: true
    rationale: "정적 검증(코드 존재·호출 관계)까지 확인했으나 실제 화면에서 사진·선이 리뷰어와 똑같이 보이는지는 시각 판단 — 80-05 실기 UAT(U-2)로 이연"
  - id: D5
    description: "기준점 사진 없음 알림 1개(D-80-09) + 상태 줄 JPG/Z 후보/기준점 안내(D-80-12/14)"
    requirement: "D-80-09"
    verification:
      - kind: other
        ref: "alert_hook/alert_call/hints/jpg_text grep (VM) + alert_wire/alert_stmts grep (code-behind) — 80-03-PLAN.md Task 2/3 <verify>"
        status: pass
    human_judgment: true
    rationale: "알림 배선·문구 존재는 정적으로 검증했으나 실제로 기준점 사진이 없는 행에서 알림이 뜨고 이해 가능한지는 사람 확인 — 80-05 실기 UAT(U-3/U-7)로 이연"
  - id: D6
    description: "리뷰어 사이클 전체 보기가 띄운 사진의 Shot 선만 그린다(함께 처리 1 — 선 겹침 버그 수정)"
    requirement: null
    verification:
      - kind: other
        ref: "dc_overload/dc_collect/dc_selectmany/rw_deleted_outside_dc grep — 80-03-PLAN.md Task 3 <verify>"
        status: pass
    human_judgment: true
    rationale: "정적 diff 로 SelectMany 합산 제거·오버로드 호출은 확인했으나 실제 화면에서 선이 겹치지 않는지는 시각 판단 — 80-05 실기 UAT(U-10)로 이연"
  - id: D7
    description: "가독성 규칙 준수 — 새/수정 파일 추가 줄에 삼항·null 병합·null 조건·switch 식·날짜주석 0, HImage 는 finally Dispose, 3개 이상 조건 없음"
    requirement: "D-80-16"
    verification:
      - kind: other
        ref: "ternary/coalesce/nullcond/switchexpr/datesig/qmark/logic3 grep (전 파일 0) + dispose_finally>=1 — 80-03-PLAN.md 전 Task <verify>"
        status: pass
    human_judgment: false

# Metrics
duration: 70min
completed: 2026-09-18
status: complete
---

# Phase 80 Plan 03: 리뷰어 NG 사이클 같은 자재 전체 사진 + 기준점 완전성 + 선 겹침 수정 Summary

80-01 의 tracer(고른 tick 1개)를 같은 자재 전체로 넓혀 모든 Shot 사진 + 기준점(짝이 완전할 때만) + 두 장짜리 + Z 후보 사진을 반복검사와 같은 GroupIntoParts 규칙으로 모아 넣고, 짝이 안 맞으면 알림 1개 후 Shot 사진만 넣었다. 상태 줄에 JPG·Z 후보·기준점 안내를 붙였고, 리뷰어 사이클 전체 보기의 선 겹침 버그(다른 Shot 선이 한 사진에 겹쳐 그려지던 문제)도 고쳤다.

## Performance

- **Duration:** ~70 min
- **Completed:** 2026-09-18
- **Tasks:** 3
- **Files modified:** 5 (신규 파일 없음, D-80-18 은 이 plan 에 해당 없음)

## Accomplishments

- **같은 자재 전체 묻기** (`SavedCycleRerunPlanner.BuildPartForSingleCycle`): PLC 자동 tick 이면 고른 사이클 날짜와 전날 폴더까지 `CollectAutoTicks` → `CompareByInspectionTime` 정렬 → `GroupIntoParts`(반복검사와 같은 알고리즘, `ValidatePart` 는 타지 않음) → `FindPartContainingCycle` 로 고른 사이클이 속한 부품을 찾고, `SeedSelectedTickPhotos` 로 고른 사이클 사진을 먼저 넣어(Fill* 의 "첫 기록 우선" 규칙상) 리뷰어에서 본 사진이 이기게 했다. 수동 RUN 기록(자동 tick 아님)이나 부품을 못 찾은 경우는 고른 사이클 1개짜리 부품으로 폴백한다. `LogSingleCyclePart` 가 걸린 시간(ms)까지 Trace 로그로 남긴다.
- **기준점 사진 완전성 판정** (`SavedCycleRerunPlanner.IsDatumPhotoSetComplete`): 이 기준점이 실제로 요구하는 역할 키(단일/H/V, `ComputeRequiredDatumRoleKeys` 규칙 재사용)가 부품에 전부 있고 파일도 있어야 true — 하나라도 없으면 false(D-80-19). `RepeatRunService.StartFromSavedCycles` 는 리뷰어 사진 사용 중(`ReviewerReinspectService.IsActive`)이면 새 상수 `ERROR_REVIEWER_PHOTOS_ACTIVE` 문구로 거절한다.
- **서비스 적용 확장** (`ReviewerReinspectService`): `LoadForRow` 가 NG 측정의 `DatumRef` 로 기준점 완전성을 먼저 판정하고(`s_bDatumPhotosApplied`), `ApplyPartPaths` 가 완전할 때만 `ApplyDatumPhotos`(RepeatRunService.ApplySavedCyclePart 와 같은 역할 키), 항상 `ApplyDualPhotos`(있는 경로만)를 적용한다. 화면 버퍼는 NG Shot 만이 아니라 사진이 바뀐 모든 소유 Shot 에 `LoadShotBuffer`(HImage try/finally Dispose)로 채운다. `ApplyReviewerOverlays` 가 사진이 바뀐 Shot 마다 그 사진을 낸 원천 tick 의 FAI `LastOverlays` 를 라이브 `FAIConfig` 에 새 List 로 복사해, 메인 화면에서 그 Shot/측정을 누르면 리뷰어에서 본 선이 그대로 보인다. 사용자 [해제]는 `ClearReviewerOverlays()`로 완전히 비우고, 그 외 해제(PLC/레시피/종료)는 목록만 비운다.
- **알림·안내 문구** (`ReviewerReinspectViewModel`): `AlertPresenter` 훅 + `ALERT_TITLE_DATUM_MISSING`/`ALERT_TEXT_DATUM_MISSING`(D-80-09, 이 phase 의 유일한 새 대화상자) — 불러오기 성공 뒤 `MainNavigator` 보다 먼저 호출된다. `BuildStatusText` 가 `HINT_JPG`/`HINT_Z_MISSING`/`HINT_DATUM_KEPT` 를 순서대로 붙인다(D-80-08/12/14).
- **리뷰어 선 겹침 버그 수정** (`ReviewMeasurementRow.ReviewerImagePathResolver` + `ReviewerWindow.DisplayCycle`): 새 오버로드 `ResolveCycleImagePath(cycle, out ownerShot)` + `CollectShotOverlays(shot)` 로, 사이클 전체 보기가 띄운 사진을 낸 Shot 의 선만 그린다(기존 `cycle.Shots.SelectMany(...)` 전 Shot 합산 제거). 기존 오버로드·행 클릭 경로(`ResolveRowImagePath`/`FindMeasuredOriginInShot`/`MeasurementGrid_SelectionChanged`/`ApplyFaiOverlays`/`ShowAxisImage`)는 전부 md5 무변경.
- **알림 배선** (`ReviewerWindow`): 생성자에 `_reinspectVm.AlertPresenter = ShowReinspectAlert;` 1줄, `ShowReinspectAlert` 는 `CustomMessageBox.Show(제목, 문구, MessageBoxImage.Warning)` 1문장(기존 반복검사 알림과 같은 공용 알림 창).

## Task Commits

1. **Task 1: 같은 자재 묻기 + 기준점 완전성 판정 + 저장 사진 재검사 상호 배제** - `14efd8e4` (feat)
2. **Task 2: 서비스·VM — 기준점/두 장짜리/Z 사진 적용, 모든 Shot 버퍼, 리뷰어 선 복사, 알림·안내** - `1af1be7d` (feat)
3. **Task 3: 리뷰어 창 — 선 겹침 수정 + 알림 배선** - `a5ce888d` (feat)

**Plan metadata:** (이 커밋 — docs)

## Files Created/Modified

- `WPF_Example/Custom/Sequence/Inspection/RepeatRunService.cs` — `SavedCycleRerunPlanner.PREVIOUS_DAY_OFFSET`, `BuildPartForSingleCycle` 본문 확장, `FindPartContainingCycle`/`PartContainsCycleKey`/`SeedSelectedTickPhotos`/`LogSingleCyclePart`/`IsDatumPhotoSetComplete`/`FindDatumConfig` 추가, `RepeatRunService.ERROR_REVIEWER_PHOTOS_ACTIVE` + `StartFromSavedCycles` 가드 1개. 기존 메서드 9개(`BuildPlan`/`ValidatePart`/`GroupIntoParts`/`CollectAutoTicks`/Fill 3종/`ComputeRequiredDatumRoleKeys`/`ApplySavedCyclePart`) md5 무변경.
- `WPF_Example/Custom/Sequence/Inspection/ReviewerReinspectService.cs` — `ReviewerReinspectLoadResult.IsDatumPhotoMissing`, `ReviewerReinspectState.IsJpgPhoto/IsZCandidateMissing/IsDatumPhotoKept`(+Clone), `s_bDatumPhotosApplied`/`s_lstOverlayFais`, `JPG_EXTENSION`/`JPEG_EXTENSION`, `ApplyDatumPhotos`/`ApplyDualPhotos`/`FindOwnedDualMeasurement`/`HasShotPhotoChanged`/`IsJpgPath`/`CountZCandidates`/`ApplyReviewerOverlays`/`CopyOverlaysToShot`/`FindFaiDtoByName`/`FindSourceShotDto`/`FindShotDtoInTick`/`ShotHasMatchingOrigin`/`ClearReviewerOverlays` 추가, `ApplyPartPaths`/`LoadForRow`/`ReleaseCore`/`LoadShotBuffer` 본문 확장.
- `WPF_Example/UI/Reviewer/ReviewerReinspectViewModel.cs` — `AlertPresenter` 훅, `ALERT_TITLE_DATUM_MISSING`/`ALERT_TEXT_DATUM_MISSING`/`HINT_JPG`/`HINT_Z_MISSING`/`HINT_DATUM_KEPT`, `ApplySelection`/`BuildStatusText` 본문 확장.
- `WPF_Example/UI/ViewModel/ReviewMeasurementRow.cs` — `ReviewerImagePathResolver.ResolveCycleImagePath(cycle, out ownerShot)`, `CollectShotOverlays(shot)` 추가(기존 메서드 전부 무변경).
- `WPF_Example/UI/Reviewer/ReviewerWindow.xaml.cs` — `DisplayCycle` 전체 보기 오버레이 부분 교체(`SelectMany` 합산 → `ownerShot` 선만), 생성자 `AlertPresenter` 배선 1줄, `ShowReinspectAlert` 추가.

## Decisions Made

- `IsDatumPhotoSetComplete` 의 File.Exists 체크를 처음엔 `IsRoleKeyPhotoUsable` 헬퍼로 분리했다가, Task 1 검증 스크립트의 `complete_exists`(메서드 자기 본문 안에서 'File.Exists' 텍스트를 직접 찾음)를 통과하지 못해 인라인으로 합쳤다 — 동작은 동일, 검증 스크립트의 awk 범위가 그 메서드 몸체만 보기 때문.
- Task 2 검증 스크립트의 `flag_in_apply` 라인이 `private static void ApplyPartPaths` 를 찾지만 실제 시그니처는 80-01 이 이미 만든 `private static int ApplyPartPaths`(적용된 Shot 사진 수를 로그용으로 반환) 다. 반환형을 계획대로 `int` 유지(회귀 방지)하고, `int` 로 고친 grep(`flag_in_apply_corrected=1`)으로 `s_bDatumPhotosApplied` 분기가 그 메서드 안에 실제로 있음을 재확인했다 — plan verify 스크립트의 문구 오류(코드 결함 아님)로 판단.

## Deviations from Plan

### Auto-fixed Issues

**1. [문서 불일치 — Rule 없음, 검증 스크립트 문구 정정] Task 1 `complete_exists` 그레이가 헬퍼 분리를 허용하지 않음**
- **Found during:** Task 1 검증
- **Issue:** `IsDatumPhotoSetComplete` 를 `IsRoleKeyPhotoUsable` 헬퍼로 나눴더니 `complete_exists=0`
- **Fix:** File.Exists 호출을 메서드 본문에 인라인
- **Files modified:** RepeatRunService.cs
- **Verification:** 재실행 후 `complete_exists=1`
- **Committed in:** `14efd8e4`

**2. [문서 불일치 — 검증 스크립트 시그니처 오기] Task 2 `flag_in_apply` 그레이가 `void` 를 찾지만 실제는 `int`**
- **Found during:** Task 2 검증
- **Issue:** `ApplyPartPaths` 는 80-01 부터 `int`(적용된 Shot 사진 수 반환) 였는데 plan 의 grep 패턴이 `void` 로 되어 있어 `flag_in_apply=0`으로 나옴
- **Fix:** 코드는 그대로 두고(반환형을 바꾸면 로그·회귀에 영향), `int` 로 고친 grep 으로 별도 재확인 — `flag_in_apply_corrected=1`
- **Files modified:** 없음(검증 방법만 보정)
- **Verification:** 아래 "Verification Results" 에 두 결과 모두 기록
- **Committed in:** 해당 없음(코드 변경 없음, 검증 확인만)

---

**Total deviations:** 2 (둘 다 계획 문서의 검증 스크립트 문구와 실제 기존 시그니처/메서드 분리 방식의 불일치 — 코드 버그 아님)
**Impact on plan:** 없음. 실제 동작·시그니처·문구는 계획이 요구한 대로 정확히 구현됨.

## Verification Results (plan-level, base = e44086e6)

- `build_exit=0 cs_errors=0` (Debug|x64, MSBuild) — Task 1/2/3 각 단계마다 재확인
- **Task 1** (`RepeatRunService.cs`): 추가 줄 `ternary=0 coalesce=0 nullcond=0 switchexpr=0 datesig=0 qmark=0 logic3=0`, `deleted_vs_phase=0`(phase 시작 대비 기존 반복검사 코드 삭제 0), `group=1 collect=1 fromdates=1 seed=1 validate=0 cyclekey=1`, `complete_def=1 complete_exists=1 guard=1 guard_msg=2`, `same[...]=1` 9개 전부(BuildPlan/ValidatePart/GroupIntoParts/CollectAutoTicks/FillPartDatumPhotos/FillPartShotPhotos/FillPartZRangePhotos/ComputeRequiredDatumRoleKeys/ApplySavedCyclePart 무변경)
- **Task 2** (`ReviewerReinspectService.cs`/`ReviewerReinspectViewModel.cs`): 두 파일 `ternary=0 coalesce=0 nullcond=0 switchexpr=0 datesig=0 qmark=0 logic3=0`, `complete_call=2 datum_apply=2 dual_apply=2 overlays=2 overlay_copy=3 jpg=2 zmiss=3 dispose_finally=1`, `flag_in_apply=0`(plan 그레이 문구 오류, 위 Decisions Made 참고) / `flag_in_apply_corrected=1`(실제 확인), `alert_hook=1 alert_call=1 hints=6 jpg_text=1`
- **Task 3** (`ReviewMeasurementRow.cs`/`ReviewerWindow.xaml.cs`): `deleted=0`(ReviewMeasurementRow.cs 는 추가만) / `deleted=15`(ReviewerWindow.xaml.cs, 전체 diff 기준 — 사이클 전체 보기 오버레이 합산 부분만), 추가 줄 `ternary=0 coalesce=0 nullcond=0 switchexpr=0 datesig=0 qmark=0`, `rr_deleted=0`, `dc_overload=1 dc_collect=1 dc_selectmany=0 dc_eval_kept=1`, `rw_deleted_outside_dc=0`(지운 줄은 사이클 전체 보기 오버레이 부분뿐), `alert_wire=1 alert_stmts=1`, `same[...]=1` 6개 전부(기존 ResolveCycleImagePath/ResolveRowImagePath/FindMeasuredOriginInShot/행 클릭/FAI 선/가로·세로 전환 무변경)

## New Symbols (실제 이름)

- `RepeatRunService`: `ERROR_REVIEWER_PHOTOS_ACTIVE`(const)
- `SavedCycleRerunPlanner`: `PREVIOUS_DAY_OFFSET`(const, -1), `FindPartContainingCycle(List<SavedCycleRerunPart>, string)`, `PartContainsCycleKey(SavedCycleRerunPart, string)`, `SeedSelectedTickPhotos(SavedCycleRerunPart target, SavedCycleRerunPart selectedOnly)`, `LogSingleCyclePart(InspectionSequence, SavedCycleRerunPart, string, long)`, `IsDatumPhotoSetComplete(SavedCycleRerunPart, InspectionSequence, string)`, `FindDatumConfig(InspectionSequence, string)`
- `ReviewerReinspectLoadResult.IsDatumPhotoMissing`
- `ReviewerReinspectState.IsJpgPhoto/IsZCandidateMissing/IsDatumPhotoKept`
- `ReviewerReinspectService`: `JPG_EXTENSION`/`JPEG_EXTENSION`(const), `s_bDatumPhotosApplied`, `s_lstOverlayFais`, `ApplyDatumPhotos(SavedCycleRerunPart, InspectionSequence)`, `ApplyDualPhotos(SavedCycleRerunPart)`, `FindOwnedDualMeasurement(string, string, string)`, `HasShotPhotoChanged(ShotConfig)`, `IsJpgPath(string)`, `CountZCandidates(SavedCycleRerunPart, string)`, `ApplyReviewerOverlays(SavedCycleRerunPart, ShotResultDto)`, `CopyOverlaysToShot(ShotConfig, ShotResultDto)`, `FindFaiDtoByName(List<FaiResultDto>, string)`, `FindSourceShotDto(SavedCycleRerunPart, string, string)`, `FindShotDtoInTick(List<ShotResultDto>, string, string)`, `ShotHasMatchingOrigin(ShotResultDto, string)`, `ClearReviewerOverlays()`
- `ReviewerReinspectViewModel`: `AlertPresenter`(Action<string,string>), `ALERT_TITLE_DATUM_MISSING`/`ALERT_TEXT_DATUM_MISSING`/`HINT_JPG`/`HINT_Z_MISSING`/`HINT_DATUM_KEPT`(const)
- `ReviewMeasurementRow.ReviewerImagePathResolver`: `ResolveCycleImagePath(CycleResultDto, out ShotResultDto)`, `CollectShotOverlays(ShotResultDto)`
- `ReviewerWindow`: `ShowReinspectAlert(string, string)`

## Alert / Hint Text (원문)

- 알림 제목: `"기준점 사진 없음"`
- 알림 문구: `"이 검사에는 짝이 맞는 기준점 사진이 없습니다 (수동 검사 기록이거나 사진 파일이 없음).\nShot 사진만 불러오고, 기준점 사진은 지금 것을 그대로 씁니다.\n기준점 Test Find 는 자동으로 하지 않습니다."`
- JPG 안내: `"  (JPG 사진 — 실제 검사값과 조금 다를 수 있음)"`
- Z 후보 없음 안내: `"  (Z 후보 사진 없음 — 고른 z 사진 1장으로 검사)"`
- 기준점 사진 유지 안내: `"  (기준점 사진 없음 — 지금 기준점 사진 사용)"`
- StartFromSavedCycles 거절 문구: `"리뷰어 사진 사용 중에는 저장 사진 재검사를 시작할 수 없습니다 — 메인 화면 상태 줄의 [해제] 를 누른 뒤 다시 시작하세요"`

## [Rerun] Log Text (원문)

- `"[Rerun] 리뷰어 부품(" + szKind + ") — " + seq.Name + " tick " + part.Ticks.Count + "개 · Shot 사진 " + part.ShotPhotoPaths.Count + " · 기준점 사진 " + part.DatumPhotoPaths.Count + " · Z 후보 Shot " + part.ZRangePhotoPaths.Count + " · " + nElapsedMs + " ms"`
  - `szKind` ∈ { `"수동 기록 — 고른 사이클만"`, `"같은 자재 묶음을 찾지 못함 — 고른 사이클만"`, `"같은 자재"` }
- `[ReviewerLoad]` 불러옴 로그(80-01 형식에 이번 plan 이 덧붙인 부분): `... + " · 기준점 사진 " + szDatumLogText + " · Z 후보 " + nZCandidateCount + "장"` (`szDatumLogText` ∈ { `"적용"`, `"지금 것 유지"` })

## Known Stubs

없음 — 이 plan 은 실제 계산·적용·배선이며 스텁 데이터 없음. 실제 화면 동작(같은 자재 사진·선이 리뷰어와 같게 보이는지, 기준점 없음 알림, JPG/Z 안내, 선 겹침 수정)은 80-05 실기 UAT(U-2/U-3/U-7/U-10)로 이연됨(Debug 빌드는 라이선스 키가 없어 실행 불가 — 사용자 메모리 `uat_release_build_only` 참조).

## Threat Flags

없음 — 이 plan 의 위협 표면은 plan 의 `<threat_model>`(T-80-11~14)에 이미 등록되어 있고 이번 구현이 새 표면을 추가하지 않았다.

## Issues Encountered

없음(위 "Deviations from Plan" 의 2건은 검증 스크립트 문구와 기존 코드 시그니처/메서드 분리 방식의 불일치일 뿐, 구현 자체의 문제는 아니었다).

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- 80-04(리뷰어 자동 Test Find)는 이 plan 의 `IsDatumPhotoSetComplete` 판정을 그대로 재사용해 "기준점 완전할 때만 자동 Test Find" 조건을 만들 수 있다.
- 80-05 실기 UAT 항목: U-2(같은 자재 사진·선), U-3(기준점 없음 알림), U-7(JPG·Z 안내), U-10(선 겹침 수정).
- D-80-11(스냅샷/복원)·D-80-13(OfflineInspectMode)은 80-01 그대로 무변경 — 이 plan 은 기준점/두 장짜리/오버레이 적용 로직만 그 위에 추가했다.

## Self-Check

- [x] `WPF_Example/Custom/Sequence/Inspection/RepeatRunService.cs` — FOUND
- [x] `WPF_Example/Custom/Sequence/Inspection/ReviewerReinspectService.cs` — FOUND
- [x] `WPF_Example/UI/Reviewer/ReviewerReinspectViewModel.cs` — FOUND
- [x] `WPF_Example/UI/ViewModel/ReviewMeasurementRow.cs` — FOUND
- [x] `WPF_Example/UI/Reviewer/ReviewerWindow.xaml.cs` — FOUND
- [x] commit `14efd8e4` — FOUND in git log
- [x] commit `1af1be7d` — FOUND in git log
- [x] commit `a5ce888d` — FOUND in git log
- [x] Debug|x64 build exit 0, cs_errors 0 (재확인)

## Self-Check: PASSED

---
*Phase: 80-reviewer-ng-cycle-reinspect*
*Completed: 2026-09-18*
