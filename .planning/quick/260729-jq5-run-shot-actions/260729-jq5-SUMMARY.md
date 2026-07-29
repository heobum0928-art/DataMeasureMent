---
phase: quick-260729-jq5
plan: 01
subsystem: ui
tags: [wpf, inspection-list-view, sequence-handler, shot-fai, batch-run]

requires: []
provides:
  - "RUN 버튼/일괄 검사가 서수(RecipeManager.Shots 인덱스)가 아니라 살아있는 SequenceBase.Actions[] 의 ShotParam 참조 동일성으로 실행할 Action 을 찾는다"
affects: [inspection-list-view, sequence-handler, batch-run-service]

tech-stack:
  added: []
  patterns:
    - "아이덴티티 스캔(ReferenceEquals(faiAct.ShotParam, target)) — InspectionSequence.FindActionIndicesByZIndex 패턴 재사용"

key-files:
  created: []
  modified:
    - WPF_Example/UI/ControlItem/InspectionListView.xaml.cs
    - WPF_Example/Custom/Sequence/SequenceHandler.cs

key-decisions:
  - "ComputeLocalShotIndex(서수 기반)를 완전 삭제하고 ResolveActionIndexByShot(아이덴티티 스캔)으로 대체 — 계획대로 서수 폴백 경로를 남기지 않음"
  - "Btn_batchRun_Click 의 RebuildInspectionActions 호출을 미해결 Shot 발견 시 1회만 수행(Shot 마다 반복 호출 금지)"

patterns-established:
  - "SequenceBase.Actions[] 를 조회할 때는 seq[i]/seq.ActionCount 인덱서만 사용하고 서수(RecipeManager.Shots 인덱스)에 의존하지 않는다"

requirements-completed: [JQ5-01, JQ5-02, JQ5-03]

duration: 25min
completed: 2026-07-29
---

# Quick Task 260729-jq5: RUN/일괄 검사 Shot 실행 아이덴티티 스캔 전환 Summary

**RUN 버튼과 일괄 검사가 Shot 을 찾을 때 "삭제/순서변경으로 낡을 수 있는 서수" 대신 "살아있는 Actions[] 안의 실제 객체 참조"로 찾도록 바꿔, 다른 Shot 이 대신 실행되던 결함을 없앴다.**

## Performance

- **Duration:** ~25 min
- **Tasks:** 3 (Task 1, 2 auto; Task 3 build 검증)
- **Files modified:** 2

## Accomplishments
- `InspectionListView.ResolveRunnableAction` 의 Shot 분기가 `ReferenceEquals(faiAct.ShotParam, target)` 아이덴티티 스캔(`ResolveActionIndexByShot`)으로 Action 을 찾도록 교체됨. 서수 기반 `shotIdx < seq.ActionCount` 폴백이 완전히 제거되어, Shot 삭제/순서변경 후 Actions[] 가 낡아도 엉뚱한 Shot 이 실행되지 않는다.
- "UI 에서 Shot 추가 직후 RUN"(Actions[] 미재구축 상태) 지연 동기화 경로는 그대로 유지 — `EnableDynamicFAIMode` + `RebuildInspectionActions` 호출 후 아이덴티티로 재스캔한다.
- `Btn_batchRun_Click` 이 체크된 Shot 들을 아이덴티티 스캔(`ResolveBatchShotIndices`)으로 해석하고, 미해결 Shot 이 하나라도 있고 시퀀스가 Idle 이면 `RebuildInspectionActions` 를 **딱 한 번만** 호출한 뒤 전체를 재해석한다. 최종 `indices` 는 오름차순 정렬해 `StartBatch`/`StartSubset` 의 min-max 연속구간 실행 가정을 보존하며, `_batchShots` 는 `indices` 와 1:1 정렬 정합을 유지한다.
- `ComputeLocalShotIndex` (서수+RecipeManager.Shots 기반) 를 코드베이스 전체에서 완전히 삭제.
- `InspectionListView.cs` L1076 근방, `SequenceHandler.cs` L104 근방의 낡은 `ComputeLocalShotIndex` 상호참조 주석을 새 아이덴티티 스캔 방식 설명으로 갱신 (`RebuildInspectionActions` 실행 코드 자체는 무변경).

## Task Commits

Each task was committed atomically:

1. **Task 1: 아이덴티티 스캔 헬퍼 추가 + ResolveRunnableAction Shot 분기 교체** - `0fb6e93` (fix)
2. **Task 2: Btn_batchRun_Click 아이덴티티 해석 + ComputeLocalShotIndex 삭제 + 낡은 주석 정리** - `987eac6` (fix)
3. **Task 3: Debug|x64 빌드 검증 + 회귀 어서션** - 코드 변경 없음(검증 전용, 별도 커밋 없음)

**Plan metadata:** (오케스트레이터가 별도 docs 커밋 처리)

## Files Created/Modified
- `WPF_Example/UI/ControlItem/InspectionListView.xaml.cs` - `ResolveActionIndexByShot`(아이덴티티 스캔 헬퍼), `ResolveRunnableAction` Shot 분기, `ResolveBatchShotIndices`(배치용 헬퍼), `Btn_batchRun_Click` 인덱스 해석 로직, `ComputeLocalShotIndex` 삭제, 낡은 주석 2곳 갱신
- `WPF_Example/Custom/Sequence/SequenceHandler.cs` - `RebuildInspectionActions` 상단 주석 1줄만 갱신 (실행 코드 무변경)

## Decisions Made
- 계획대로 서수 기반 폴백 경로를 어디에도 남기지 않음 — 못 찾으면 무조건 -1/false, rebuild-후-재스캔까지 실패하면 그대로 실패 반환.
- `Btn_batchRun_Click` 에서 rebuild 는 미해결 Shot 이 하나라도 있을 때 딱 1회만 호출(Shot 별 반복 호출 시 Actions[] 를 통째로 재생성하는 낭비/DoS 방지).

## Deviations from Plan

None - 계획대로 실행됨. 원자적 커밋을 위해 Task 1 커밋 시점에는 `ComputeLocalShotIndex` 를 일시적으로 보존했다가(플랜의 Task 구분과 일치시키기 위함), Task 2 커밋에서 완전 삭제하는 순서로 작업했다 — 최종 diff 내용은 플랜과 동일, 순서만 커밋 단위 분리를 위해 조정.

## Issues Encountered
None.

## Regression Assertions (Task 3)

빌드: `MSBuild WPF_Example/DatumMeasurement.csproj -p:Configuration=Debug -p:Platform=x64` → **0 Errors**, exit code 0. `-t:Rebuild` (클린 재빌드)로도 확인. 경고는 기존부터 존재하던 `CS0618`(obsolete `TopSequence`/`BottomSequence`/`TopInspectionAction`/`BottomInspectionAction`) 10건뿐이며 발생 위치(`Sequence_Top.cs:19`, `Sequence_Bottom.cs:30`, `SequenceHandler.cs:69,71,73`)는 이번 변경으로 건드린 라인(104 근방, `InspectionListView.xaml.cs` 전역)과 무관 — 신규 경고 없음.

(a) 비-Shot Action 분기 / Sequence 노드 분기 무변경:
`git diff` 로 `ResolveRunnableAction` 내 `node.NodeType == ENodeType.Action`(일반 Action, `InspectionListView.xaml.cs` L462-470 부근)과 `node.NodeType == ENodeType.Sequence`(L472-478 부근) 블록에 `-`/`+` 라인이 전혀 없음을 확인 (커밋 `0fb6e93` diff 상 Shot 분기·헬퍼 추가부만 변경).

(b) 타 시퀀스 소유 Shot 은 -1 로 확정:
`SequenceHandler.RebuildInspectionActions` (`SequenceHandler.cs` L117-124) 가 `OwnerSequenceName` 으로 자기 시퀀스 소유 Shot 만 필터링해 `Actions[]` 를 구성하므로, 다른 시퀀스 소유 `ShotConfig` 객체는 애초에 그 시퀀스의 `Actions[]` 배열에 존재하지 않는다. 따라서 `ResolveActionIndexByShot`(`InspectionListView.xaml.cs` L483-491)의 `ReferenceEquals` 스캔이 매칭될 수 없어 항상 -1 을 반환한다.

(c) 지연 동기화 rebuild 경로 유지:
`ResolveRunnableAction` (`InspectionListView.xaml.cs` L432-456) 의 Shot 분기 — 1차 스캔 실패 시 `seqHandler.IsIdle` 조건에서 `EnableDynamicFAIMode()` + `RebuildInspectionActions(seq.ID)` 호출 후 `ResolveActionIndexByShot` 로 재스캔하는 3단계 구조가 그대로 존재함(코드 존재 확인, 커밋 `0fb6e93`).

(d) 일괄 검사 오름차순 인덱스 + `_batchShots` 정합:
`Btn_batchRun_Click` (`InspectionListView.xaml.cs` L569-571, 커밋 `987eac6`) — `resolvedPairs.Where(p => p.Item2 >= 0).OrderBy(p => p.Item2).ToList()` 로 (shot, idx) 쌍을 idx 오름차순 정렬 후, `indices`/`batchShots` 를 같은 정렬된 리스트에서 `Select` 로 함께 생성하므로 두 리스트가 항상 같은 순서로 정렬 정합을 유지한다.

## User Setup Required

None - 외부 서비스 설정 불필요.

## Next Phase Readiness
- 이번 변경으로 Shot 삭제/순서변경 후 RUN 이 엉뚱한 Shot 을 실행하던 확인된 결함(실기 재현: SHOT_E1-4 선택 → SHOT_B1-4 실행)이 코드 수준에서 제거됨.
- 실기(HW) 재현 시나리오(SHOT 삭제/순서변경 후 RUN, UI 에서 SHOT 추가 직후 RUN, 일괄 검사)로 UAT 재현/검증은 추후 실기 세션에서 진행 필요 — 이번 세션은 코드 변경 + 빌드 검증까지만 수행.

---
*Phase: quick-260729-jq5*
*Completed: 2026-07-29*

## Self-Check: PASSED

- FOUND: WPF_Example/UI/ControlItem/InspectionListView.xaml.cs
- FOUND: WPF_Example/Custom/Sequence/SequenceHandler.cs
- FOUND commit: 0fb6e93
- FOUND commit: 987eac6
