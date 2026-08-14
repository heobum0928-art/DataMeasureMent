---
phase: quick-260814-warmup-thread-fix
plan: 01
subsystem: infra
tags: [threading, halcon, warmup, sequencebase, systemhandler]

# Dependency graph
requires:
  - phase: quick-260814-dxy
    provides: "측정 파이프라인 워밍업 게이트(IsMeasureWarmupComplete) + 앱 시작 워밍업 기동 배선"
  - phase: quick-260814-warmup-transform-fix
    provides: "identity HTuple 로 measure_pos 실제 성공(success=735 fail=150) — datumTransform null 가드 근본원인 수정"
provides:
  - "SequenceBase.CallbackQueue(ConcurrentQueue<Action>) + EnqueueCallback + MainExecute 매 iteration 드레인 — 재사용 가능한 시퀀스 전용 콜백 큐 인프라"
  - "StartMeasureWarmupAsync 가 시퀀스별 MainThread 콜백으로 워밍업을 실행(실제 검사와 동일 스레드)"
  - "Interlocked 카운트다운 + 30초 타임아웃 감시자로 IsMeasureWarmupComplete fail-open 게이트"
  - "RunMeasureWarmup/FindMeasureWarmupShot sequenceName 파라미터화 — 시퀀스가 실제 소유한 Shot 우선 선택"
affects: [warmup, measure-pipeline, sequencebase, threading]

tech-stack:
  added: []
  patterns:
    - "SequenceBase 콜백 큐(ConcurrentQueue<Action> + EnqueueCallback + DrainCallbackQueue) — 다른 서브시스템도 시퀀스 전용 스레드에서 코드를 실행해야 할 때 재사용 가능"

key-files:
  created: []
  modified:
    - WPF_Example/Sequence/Sequence/SequenceBase.cs
    - WPF_Example/Custom/SystemHandler.cs

key-decisions:
  - "워밍업 로직 자체(RunMeasureWarmup)는 Task.Run 대신 각 SequenceBase.MainThread 위에서 EnqueueCallback 으로 실행 — HALCON temp-mem 캐시가 스레드별로 관리되므로 실제 검사가 도는 그 스레드를 데워야 의미가 있다"
  - "완료 감시자(카운트다운 폴링)는 계속 Task.Run(스레드풀) 유지 — HALCON 을 직접 건드리지 않는 순수 폴링이라 문제 없음"
  - "FindMeasureWarmupShot 은 sequenceName 소유 Shot 우선 → 소유 Shot 이미지 없으면 아무 Shot 폴백(기존 동작 유지) → 합성 이미지 최종 폴백 — 완전 스킵보다 낫다는 기존 방침 유지"

requirements-completed: [MEASURE-WARMUP-01]

duration: 15min
completed: 2026-08-14
---

# Quick 260814-warmup-thread-fix: 측정 워밍업 실행 스레드 근본원인 수정 Summary

**측정 파이프라인 워밍업을 Task.Run(스레드풀 임의 스레드) 대신 각 SequenceBase.MainThread(실제 검사가 도는 그 스레드) 위에서 실행하도록 SequenceBase 에 콜백 큐 인프라를 추가하고 SystemHandler 워밍업 배선을 재구성**

## Performance

- **Duration:** 약 15분
- **Started:** 2026-08-14 (세션 시작)
- **Completed:** 2026-08-14T13:35:13+09:00
- **Tasks:** 2/2
- **Files modified:** 2

## Accomplishments
- `SequenceBase` 에 `CallbackQueue`(`ConcurrentQueue<Action>`) + `EnqueueCallback` + `DrainCallbackQueue` 추가 — `MainExecute()` 루프가 `Command`/`bCreated` 상태와 무관하게 매 iteration 무조건 드레인
- `SystemHandler.StartMeasureWarmupAsync()` 를 재배선 — 등록된 각 시퀀스(`SequenceHandler` 로 이미 PC 의 `PcRole`/`CameraRole` 필터링된 실제 활성 시퀀스만)에 `targetSeq.EnqueueCallback(() => RunMeasureWarmup(sequenceName))` 로 워밍업을 넣고, `Interlocked` 카운트다운 + 30초 타임아웃 감시자로 `IsMeasureWarmupComplete` 게이트를 연다(fail-open 유지)
- `RunMeasureWarmup`/`FindMeasureWarmupShot` 에 `sequenceName` 파라미터 추가 — 그 시퀀스가 실제로 소유한 Shot(`OwnerSequenceName` 일치)을 우선 선택해 프로덕션 경로와 최대한 가깝게 재현, 없으면 기존처럼 아무 Shot/합성 이미지 폴백

## Task Commits

Each task was committed atomically:

1. **Task 1: SequenceBase 에 시퀀스 전용 콜백 큐 인프라 추가** - `d8eab9b` (feat)
2. **Task 2: SystemHandler 워밍업을 시퀀스별 콜백 큐로 재배선 + 완료 카운팅 게이트** - `e65c24b` (feat)

## Files Created/Modified
- `WPF_Example/Sequence/Sequence/SequenceBase.cs` - `CallbackQueue`/`EnqueueCallback`/`DrainCallbackQueue` 추가, `MainExecute()` 루프 맨 앞에서 매 iteration 드레인
- `WPF_Example/Custom/SystemHandler.cs` - `MEASURE_WARMUP_TIMEOUT_MS` 상수, `StartMeasureWarmupAsync()` 시퀀스별 콜백 배선+카운트다운 게이트, `RunMeasureWarmup(string sequenceName)`, `FindMeasureWarmupShot(string sequenceName, ...)` owner-우선 선택

## Decisions Made
- 워밍업 콜백 실행 스레드를 스레드풀에서 시퀀스 전용 `MainThread` 로 옮긴 것이 이번 수정의 핵심(근본원인 수정) — HALCON temp-mem 캐시가 per-thread 이므로 다른 스레드를 데워봐야 실제 검사 스레드에는 효과가 없었다
- 완료 대기용 카운트다운 감시자는 그대로 `Task.Run`(스레드풀)에 남겨둠 — 이 감시자는 폴링만 하고 HALCON API 를 전혀 호출하지 않으므로 문제 없음, fail-open 원칙(30초 타임아웃)도 그대로 유지

## Deviations from Plan

None - plan executed exactly as written. Interfaces 블록에 명시된 "현재 코드"/"교체 후" 스니펫을 그대로 적용했다. (참고: `StartMeasureWarmupAsync()` 위 주석에 quick-260814-dxy 배경 설명이 상수 블록 앞과 함수 바로 앞 두 군데에 나타나는데, 이는 plan interfaces 블록의 "교체 후" 스니펫에 명시된 그대로이며 별도 임의 수정 없음.)

## Issues Encountered
- Debug/x64 빌드 시 실제 `OutDir`(`D:\Data\DatumMeasurement.exe`)로의 최종 복사 단계가 앱이 실행 중이라 잠겨 있어 `MSB3021/MSB3026/MSB3027` 로 실패했다. 프로젝트 하드 규칙("빌드산출물 잠김 → 프로세스 절대 종료 금지")에 따라 프로세스를 죽이지 않고, 스크래치 `OutDir`+`BaseIntermediateOutputPath` 로 `-t:Rebuild` 를 재실행해 컴파일만 재검증했다 — `error CS`/`error MSB` 0건, `warning CS0618`×10 + `warning CS0162`×2 = 정확히 baseline 12줄과 일치, 신규 warning 0건, `DatumMeasurement -> ...\build-verify\DatumMeasurement.exe` 산출물 정상 생성 확인.

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- 코드/빌드 레벨 검증은 전부 완료(grep 체크 10/10 통과, 컴파일 성공, 금지 파일 2개 무변경 확인).
- **런타임 검증은 사용자 몫이다.** 앱을 재시작한 뒤:
  1. `D:\Data\Trace` 최신 로그에서 `[MeasureWarmup] 완료 seq=... shot=...` 라인이 등록된 시퀀스 수만큼(예: Side 전용 PC 라면 1줄, Top+Bottom PC 라면 2줄) 나오는지 확인 — 이전(스레드풀 단일 실행, `seq=` 없는 로그)과 달리 시퀀스별로 각각 로그가 남아야 정상.
  2. 워밍업 완료 직후 실제 Top/Side/Bottom 검사 사이클(RUN 버튼 또는 TCP `$TEST`) 속도가 이전(3.5~5.1초)보다 개선되는지 — **이건 사용자가 직접 확인해야 한다.** 스레드 문제를 고쳤어도 100% 개선을 보장하지 않으며(다른 요인이 남아있을 수 있음), 이 세션 범위에서는 실측하지 않았다.
- 앱 실행 중이던 프로세스가 최신 빌드 산출물을 D:\Data 에 반영받지 못한 상태다(잠김으로 복사 실패) — 사용자가 앱을 재시작해야 이번 수정이 실제로 적용된 바이너리가 로드된다.

---
*Phase: quick-260814-warmup-thread-fix*
*Completed: 2026-08-14*

## Self-Check: PASSED
- FOUND: WPF_Example/Sequence/Sequence/SequenceBase.cs
- FOUND: WPF_Example/Custom/SystemHandler.cs
- FOUND: .planning/quick/260814-warmup-thread-fix/260814-warmup-thread-fix-SUMMARY.md
- FOUND commit: d8eab9b
- FOUND commit: e65c24b
