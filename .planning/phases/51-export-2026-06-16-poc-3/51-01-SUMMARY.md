---
phase: 51-export-2026-06-16-poc-3
plan: 01
subsystem: sequence
tags: [batch-run, sequence-engine, inspection, wpf, csharp]

# Dependency graph
requires:
  - phase: 40-result-analysis-export-2026-06-01
    provides: CycleResultSerializer.BuildDto, CycleResultDto, RepeatRunService pattern
  - phase: 33-inspection-sequence-2026-05-26
    provides: InspectionSequence, SequenceBase, StartAll pattern
provides:
  - SequenceBase.StartSubset(int[] actionIndices, TestPacket packet) 부분 실행 진입점
  - BatchRunService: 선택 SHOT 인덱스 집합 1사이클 일괄 실행 + OnBatchComplete(List<CycleResultDto>) 계약
affects:
  - 51-02 (UI Wave): StartSubset + BatchRunService.StartBatch / OnBatchComplete 소비

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "StartSubset: 연속범위(first/last index) 방식으로 SequenceBase 부분 실행"
    - "BatchRunService: RepeatRunService 파생 Start→OnFinish→HandleFinish→누적 패턴, SaveAsync 미호출(HandleManualCyclePersist 위임)"

key-files:
  created:
    - WPF_Example/Custom/Sequence/Inspection/BatchRunService.cs
  modified:
    - WPF_Example/Sequence/Sequence/SequenceBase.cs
    - WPF_Example/DatumMeasurement.csproj

key-decisions:
  - "StartSubset 전략 = 연속 범위(first/last index): UI(51-02)가 동일 시퀀스 내 SHOT 선택을 강제하므로 정확. 불연속 SHOT 엄밀 필터는 향후 마스크 전략으로 교체 가능 (별도 phase)"
  - "BatchRunService.HandleFinish SaveAsync 미호출: InspectionSequence.HandleManualCyclePersist(packet==null 수동 경로)가 이미 저장 — 중복 저장 방지. BuildDto + 누적만 수행"
  - "OnBatchComplete 시그니처 List<CycleResultDto>: RepeatRunService.OnRepeatComplete 정합으로 Phase 41.1(Gage R&R) 재사용 경로 확보(D-08)"

patterns-established:
  - "StartSubset 패턴: StartAll 변형, actionIndices 정렬→first/last→CurrentActionIndex/EndActionIndex 설정, 경계 가드 포함"
  - "BatchRunService 패턴: RepeatRunService 구조 파생(IsRunning 가드, _lock HandleFinish, Dispatcher.Background TriggerNext), 중복 저장 방지 계약"

requirements-completed: [BATCH-01]

# Metrics
duration: 20min
completed: 2026-06-16
---

# Phase 51 Plan 01: 시퀀스 일괄 검사 백엔드 Summary

**SequenceBase.StartSubset(int[] actionIndices) 부분 실행 진입점 + BatchRunService(RepeatRunService 파생) 추가로 선택 SHOT 1사이클 일괄 실행 백엔드 계약 확정**

## Performance

- **Duration:** 20 min
- **Started:** 2026-06-16T00:00:00Z
- **Completed:** 2026-06-16T00:20:00Z
- **Tasks:** 3
- **Files modified:** 3 (1 created, 2 modified)

## Accomplishments
- SequenceBase.StartSubset: StartAll과 동일 상태 전이 방식으로 선택 인덱스 범위(first~last)만 실행하는 진입점 추가. 기존 StartAll 본문 무변경(회귀 0).
- BatchRunService.cs 신규: RepeatRunService 파생 패턴으로 StartBatch → OnFinish → HandleFinish → BuildDto 누적 → OnBatchComplete 발화. SaveAsync 미호출로 HandleManualCyclePersist 위임(중복 저장 방지).
- Debug|x64 빌드 0 errors 확인 (기존 CS0618/CS0162 warning만 존재 — Phase 51 코드 신규 warning 0).

## Task Commits

각 태스크를 원자적으로 커밋:

1. **Task 1: SequenceBase.StartSubset 추가** - `48f29bd` (feat)
2. **Task 2: BatchRunService.cs 신규 + csproj 등록** - `5ba31e8` (feat)
3. **Task 3: Debug|x64 빌드 검증** - (빌드 아티팩트, 추가 커밋 없음)

## Files Created/Modified
- `WPF_Example/Sequence/Sequence/SequenceBase.cs` - StartSubset 메서드 추가 (L362~L386, K&R 스타일)
- `WPF_Example/Custom/Sequence/Inspection/BatchRunService.cs` - 신규 생성. RepeatRunService 파생 일괄 검사 실행 서비스
- `WPF_Example/DatumMeasurement.csproj` - BatchRunService.cs Compile 등록 (RepeatRunService.cs 바로 아래)

## Decisions Made
- **StartSubset 연속 범위 전략**: 정렬 후 first/last 인덱스로 CurrentActionIndex/EndActionIndex 설정. 불연속 SHOT 간격에는 비선택 SHOT도 실행되나, 51-02 UI가 동일 시퀀스 내 SHOT 선택을 강제하므로 일반 운용에서 정확. 향후 불연속 엄밀 필터가 필요하면 마스크 전략으로 교체(별도 phase).
- **SaveAsync 미호출 계약**: InspectionSequence.HandleManualCyclePersist가 packet==null 수동 경로에서 이미 CycleResultSerializer.SaveAsync를 호출. BatchRunService.HandleFinish는 SaveAsync를 호출하지 않고 BuildDto + _collected 누적만 수행하여 중복 저장 방지.

## Deviations from Plan

없음 — 플랜대로 정확히 실행됨.

## 주의/한계 (PLAN 요구사항)

**StartSubset 연속 범위 전략의 한계:**
- `first~last` 사이의 비선택 SHOT(인덱스가 없는 중간 SHOT)도 실행된다.
- Phase 51의 UI(51-02)는 동일 시퀀스 내 SHOT 선택을 강제하고, 일반 운용(시퀀스 전체 또는 연속 SHOT 선택)에서 정확하다.
- 불연속 SHOT 선택의 엄밀 필터링이 향후 필요하면 마스크 필터 전략으로 교체한다(별도 phase).

**SaveAsync 미호출 / HandleManualCyclePersist 위임 계약:**
- BatchRunService.HandleFinish는 CycleResultSerializer.SaveAsync를 호출하지 않는다.
- InspectionSequence 생성자에서 OnFinish에 등록된 HandleManualCyclePersist가 RequestPacket==null(수동 경로)일 때 SaveAsync를 이미 호출한다.
- 이 계약이 InspectionSequence 측에서 변경되면 BatchRunService도 검토 필요.

**51-02 UI 소비 계약:**
- `BatchRunService.OnBatchComplete` 시그니처: `Action<List<CycleResultDto>>`
- `BatchRunService.StartBatch(InspectionSequence seq, List<int> selectedShotIndices)` 진입점
- `SequenceBase.StartSubset(int[] actionIndices, TestPacket packet)` 진입점

## Issues Encountered
없음

## Next Phase Readiness
- 51-02(Wave 2 UI): InspectionListView에 체크박스 다중 선택 + "일괄 검사" 버튼 + BatchRunService 소비 + 누적 xlsx Export 버튼 구현 가능 상태.
- BatchRunService.OnBatchComplete(List<CycleResultDto>) 계약 확정 — 51-02가 탐색 없이 구현 가능.

## Self-Check: PASSED

- `WPF_Example/Sequence/Sequence/SequenceBase.cs` — StartSubset 존재 확인 (grep 1건)
- `WPF_Example/Custom/Sequence/Inspection/BatchRunService.cs` — 파일 생성 확인
- `WPF_Example/DatumMeasurement.csproj` — BatchRunService.cs Compile 등록 확인 (grep 1건)
- `CycleResultSerializer.SaveAsync` — BatchRunService.cs 내 호출 0건 확인
- 커밋 48f29bd, 5ba31e8 — git log 확인

---
*Phase: 51-export-2026-06-16-poc-3*
*Completed: 2026-06-16*
