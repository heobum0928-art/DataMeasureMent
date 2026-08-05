---
phase: quick-260805-mzf
plan: 01
subsystem: infra
tags: [wpf, CaptureImageSaveService, backpressure, memory-leak, crash-fix, ConcurrentQueue]

# Dependency graph
requires: []
provides:
  - "CaptureImageSaveService 저장 큐에 MAX_QUEUE_DEPTH(50) 상한 + _nQueueDepth 카운터 + WaitForQueueSpace() 생산측 백프레셔 도입, 일괄검사 중 저장 워커가 생산 속도를 못 따라가 무제한 적체되어 58.3GB까지 메모리가 폭증하던 크래시의 구조적 원인 제거"
affects: ["Action_FAIMeasurement QueueFaiCapture 호출부(무수정, 시그니처 불변)", "BatchRunService 일괄검사 흐름(호출측 무수정, 저장 지연 시 사이클 자연 감속)"]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "생산측 백프레셔(폴링, 신규 disposable 동기화 객체 미도입) — 기존 Dispose()/_signal.Dispose() 종료 경합을 악화시키지 않도록 Interlocked+Volatile+Thread.Sleep 폴링만 사용"
    - "Enqueue 경로에서 유실 없는 백프레셔: 타임아웃 시에도 대기만 포기하고 enqueue는 항상 수행"

key-files:
  created: []
  modified:
    - "WPF_Example/Utility/CaptureImageSaveService.cs"

key-decisions:
  - "새 AutoResetEvent/SemaphoreSlim 등 disposable 동기화 객체를 추가하지 않고 20ms Thread.Sleep 폴링 방식을 채택 — 기존 Dispose()가 _signal.Dispose()를 워커 종료 보장 없이 호출하는 기존 리스크를 확대하지 않기 위함(플랜 명시 제약)"
  - "상한 초과 시 이미지를 버리거나 건너뛰지 않고 enqueue 시점만 지연시킨다 — 캡쳐 이미지는 불량 판정 증거 자료라 유실 허용 안 됨(30초 타임아웃 후에도 enqueue는 반드시 수행)"
  - "다중 생산자(Top/Side/Bottom 시퀀스 스레드) 경합으로 상한을 소폭(≈동시 생산자 수) 초과할 수 있는 soft cap을 수용 — 하드 세마포어 도입은 disposable 핸들을 늘려 종료 경합 표면적을 키우므로 비채택(플랜 threat model T-mzf-09 accept)"

patterns-established:
  - "저장/백그라운드 워커 서비스에 상한+카운터+생산측 대기 방식 백프레셔를 적용할 때는 새 동기화 프리미티브 추가 대신 기존 필드(volatile bool, Interlocked int)만으로 폴링 구현 — 종료 경로 안전성 우선"

requirements-completed: []  # QUICK-260805-mzf — Task 2(human-verify)가 완료되기 전까지 요구사항 전체 충족 보류. 완료 후 오케스트레이터가 STATE/REQUIREMENTS 갱신 시 반영.

# Metrics
duration: ~15min (Task 1만; Task 2는 사람 실측 대기로 별도)
completed: 2026-08-05
---

# Quick Task 260805-mzf: CaptureImageSaveService 저장 큐 상한 + 백프레셔 Summary

**`CaptureImageSaveService`의 무제한 `ConcurrentQueue`에 상한(50) + 원자적 깊이 카운터 + 생산측(시퀀스 스레드) 폴링 백프레셔를 추가해, 일괄검사 연속 실행 시 저장 워커가 생산 속도를 못 따라가 항목당 ~12MB `HImage` refcount가 무한 적체되며 58.3GB까지 메모리가 폭증해 강제 종료되던 실측 크래시의 구조적 원인을 제거 — 이미지 유실은 0을 보장(타임아웃 시에도 enqueue는 항상 수행)**

## Status

**Task 1 (코드 수정) : 완료.**
**Task 2 (`checkpoint:human-verify` — 일괄검사 20회 이상 실기 메모리/파일수 검증) : 사람 실측 대기 — 이 플랜은 아직 완료되지 않았습니다.**

Task 2는 실제 애플리케이션을 재빌드/실행하고 최소 20회 연속 일괄검사를 돌려 (a) 메모리가 GB 단위로 계속 우상향하지 않는지, (b) `original`/`capture` 폴더의 저장 파일 수가 서로 같고 누락이 없는지를 사람이 직접 확인해야 하는 단계입니다. 이 실행자는 실제 하드웨어/UI를 조작할 수 없으므로 이 단계는 스킵/위조하지 않고 그대로 남겨두었습니다. 아래 "Task 2 — 사람이 수행할 절차"를 참고해 주세요.

## Performance

- **Duration:** ~15 min (Task 1)
- **Completed:** 2026-08-05
- **Tasks:** 1/2 completed (Task 2 = checkpoint, blocked on human)
- **Files modified:** 1

## Accomplishments (Task 1)
- 라이브 파일(`WPF_Example/Utility/CaptureImageSaveService.cs`)을 먼저 읽어 플랜의 인터페이스 명세(필드/메서드 시그니처)와 실제 코드가 일치함을 재확인
- `_renderer` 필드 아래에 `MAX_QUEUE_DEPTH`(50) / `BACKPRESSURE_POLL_MS`(20) / `BACKPRESSURE_MAX_WAIT_MS`(30000) / `BACKPRESSURE_LOG_THRESHOLD_MS`(1000) 상수 + `_nQueueDepth` 필드 + 진단용 `QueueDepth` 읽기 전용 프로퍼티 추가
- `Enqueue`에 `WaitForQueueSpace()` 호출 + `Interlocked.Increment(ref _nQueueDepth)`를 `_queue.Enqueue` 직전에 삽입(기존 null/Shared 누락 방어 로직은 무수정)
- `WaitForQueueSpace()` 신규 추가 — `_isStopping`/`!_workerThread.IsAlive`/`BACKPRESSURE_MAX_WAIT_MS` 3중 탈출 조건을 갖춘 폴링 대기, 타임아웃 시에도 대기만 포기하고 enqueue는 항상 수행(유실 0)
- `WorkLoop`의 `TryDequeue` 2곳(본 루프 + 종료 후 drain 루프) 모두 신규 `ProcessDequeued`를 경유하도록 통일 — `SaveRequest` 직접 호출을 남겨두면 카운터 감소 누락으로 큐가 영구 포화될 위험을 제거
- `ProcessDequeued` 신규 추가 — `try { SaveRequest(request); } finally { Interlocked.Decrement(ref _nQueueDepth); }`로 처리 완료/예외 상관없이 카운터 감소를 단일 지점에서 보장
- `SharedHImage`, `CaptureImageSaveRequest`, `SaveRequest`, `BuildFileName`, `BuildDirectory`, `BuildFilePath`, `Sanitize*`, `Dispose()`, `Start()`는 플랜 지시대로 일체 무수정 확인(회귀 0)
- 정적 grep 검증: `Interlocked.Increment(ref _nQueueDepth)`=1, `Interlocked.Decrement(ref _nQueueDepth)`=1, `ProcessDequeued(`=3(호출 2곳+정의 1), `SaveRequest(`=2(정의 1+`ProcessDequeued` 내부 호출 1) — 플랜 체커가 사전 안내한 보정값(3, 2)과 정확히 일치
- `TryDequeue` 2곳 모두 확인 결과 dequeue 후 폐기/skip 코드 없음(둘 다 `ProcessDequeued` 호출)
- `git diff` 리뷰: 대상 파일 1개만 변경, 추가만 있고 기존 로직 삭제/변형 없음(순수 삽입형 패치)
- MSBuild Debug/x64 빌드: **성공(exit 0)**, 신규 에러 0, 신규 경고 0(`grep -n CaptureImageSaveService` 결과 "none" — 로그에 이 파일 관련 언급 자체가 없어 완전히 클린 컴파일임을 확인). 로그에 남은 warning 전부는 기존 `CS0618`(obsolete `TopInspectionAction`/`BottomInspectionAction`/`TopSequence`/`BottomSequence`)과 `CS0162`(`VirtualCamera.cs` 기존 unreachable code)로, 본 작업과 무관한 기존 경고임

## Task Commits

1. **Task 1: CaptureImageSaveService 큐 상한 + 생산측 백프레셔 구현** - `44339bc` (fix)

_Note: plan 메타데이터(SUMMARY/STATE/ROADMAP/PLAN) 커밋은 오케스트레이터가 별도 처리. Task 2는 checkpoint:human-verify로 코드 변경이 없어 커밋 대상 없음._

## Files Created/Modified
- `WPF_Example/Utility/CaptureImageSaveService.cs` - 저장 큐 상한(`MAX_QUEUE_DEPTH=50`) + `_nQueueDepth` 카운터 + `QueueDepth` 진단 프로퍼티 + `WaitForQueueSpace()`(3중 탈출 폴링 백프레셔) + `ProcessDequeued()`(카운터 감소 단일 지점) 추가. 순증가 58줄 삽입 / 2줄 삭제(둘 다 `SaveRequest` 직접 호출 → `ProcessDequeued` 호출로 치환).

## Decisions Made
- 플랜이 명시한 대로 새 disposable 동기화 객체(AutoResetEvent/SemaphoreSlim 등)를 추가하지 않고 `Thread.Sleep(20)` 폴링 방식을 그대로 채택 — 기존 `Dispose()`의 `_signal.Dispose()` 워커 종료 미보장 경합을 확대하지 않기 위함.
- 타임아웃(30초) 도달 시에도 대기만 포기하고 `enqueue`는 반드시 수행 — 캡쳐 이미지는 불량 판정 증거 자료이므로 유실은 어떤 경우에도 허용하지 않는다는 플랜의 LOCKED 결정을 그대로 따름.
- 빌드 검증 시 실제 저장소의 `bin`/`obj`가 살아있는 Visual Studio 디버그 세션(`DatumMeasurement.exe` PID 22628, `devenv.exe` PID 10068/22248)에 의해 잠겨 있어 최초 시도에서 `MSB3030`(obj\...\DatumMeasurement.exe.config 복사 실패) 에러가 발생. 프로세스를 종료하지 않고 `-p:OutputPath`/`-p:BaseIntermediateOutputPath`로 스크래치 디렉터리를 지정한 대체 빌드로 컴파일 검증만 수행(세션 안전 제약 준수). 실제 저장소 파일은 전혀 건드리지 않음.

## Deviations from Plan

None - 코드 변경은 플랜의 AFTER 텍스트와 100% 동일하게 적용됨. 빌드 검증 방식(대체 OutDir 사용)은 플랜이 아닌 세션 안전 제약에 따른 검증 절차상의 우회이며 코드/커밋 내용에는 영향 없음.

## Issues Encountered
- 최초 MSBuild 호출 시 `bin\x64\Debug\DatumMeasurement.exe` 파일 잠금 경고 10회 재시도 후 성공했으나, 곧이어 실행한 재확인 빌드에서 `obj\x64\Debug\DatumMeasurement.exe.config`를 찾을 수 없다는 `MSB3030` 에러 발생 — 살아있는 VS 디버그 세션과의 `obj`/`bin` 경합으로 판단됨. 프로세스 종료 없이 스크래치 OutDir로 재시도해 해결(위 Decisions 참고). 실제 저장소의 `bin`/`obj`는 이 실행자가 직접 정상적으로 빌드하지 못했으므로, 사용자가 Task 2 절차 1번("앱을 재빌드")에서 정상적으로 재빌드되는지 재확인 필요(코드 자체는 스크래치 빌드로 컴파일 정상 확인됨).

## User Setup Required

None - 외부 서비스 설정 불필요.

## Task 2 — 사람이 수행할 절차 (플랜 원문 그대로, checkpoint:human-verify)

**무엇이 만들어졌는가:** `CaptureImageSaveService` 저장 큐에 상한(50)을 걸고, 상한 도달 시 검사(시퀀스) 스레드가 20ms 단위로 최대 30초까지 대기했다가 반드시 enqueue하도록 백프레셔를 넣었습니다. 이미지는 어떤 경우에도 버려지지 않으며, 대기가 1초를 넘으면 Error 로그에 대기 시간과 큐 깊이가 남습니다.

**확인 절차:**
1. 앱을 재빌드(Debug/x64)한 뒤 실행합니다. (현재 실행 중인 디버그 세션이 있다면 먼저 종료 후 재빌드해야 이번 수정이 반영됩니다.)
2. 작업 관리자(Ctrl+Shift+Esc) → 세부 정보 탭에서 `DatumMeasurement.exe`의 메모리를 볼 수 있게 띄워 둡니다. 시작 시점 메모리를 메모합니다.
3. 일괄검사를 **최소 20회 이상**(가능하면 크래시 재현 때와 같은 회차) 연속 실행합니다.
4. 실행 중 메모리를 관찰합니다.
   - 기대: 초기값 대비 수백 MB 범위 안에서 오르내리고, 계속 우상향으로만 증가하지 않습니다.
   - 실패 신호: GB 단위로 멈추지 않고 계속 증가합니다.
5. 일괄검사 완료 후 저장 폴더를 확인합니다: `{ResultSavePath}\Image\{yyMMdd}\{HHmm}\original`과 `...\capture`.
   - 기대 파일 수: `original` = `capture` = (검사한 FAI 수 × 회차 수). 두 폴더 개수가 서로 같아야 합니다.
6. Error 로그를 확인합니다.
   - `저장 지연으로 검사 사이클 대기 ...ms`가 보이면 → 백프레셔가 정상 동작한 것(정상). 검사가 조금 느려진 대신 메모리를 지킨 것입니다.
   - `저장 큐 백프레셔 타임아웃`이 보이면 → 저장이 30초 동안 1건도 못 빠져나간 것입니다. 저장 경로(네트워크 드라이브 여부)/디스크 속도를 알려주세요.
7. 검사 사이클 체감 속도가 실사용에 문제될 정도로 느려졌는지 알려주세요(상한 50 값 조정 근거로 사용).

**승인 신호:** "승인" 또는 관찰된 문제(메모리 수치 / 누락 파일 수 / 사이클 체감 속도)를 알려주시면 됩니다.

## Next Phase Readiness
- Task 1 코드는 빌드 PASS, 정적 검증 전부 통과, 커밋 완료. 회귀 위험 낮음(호출부 `Action_FAIMeasurement.cs`/`BatchRunService.cs` 무수정, `Enqueue` 시그니처 불변).
- **차단 요소:** Task 2(사람 실측)가 완료되어야 이 quick-task가 최종 완료(SIGNED_OFF) 처리됩니다. 사용자가 위 절차대로 실측 후 결과를 알려주시면 재개하겠습니다.

---
*Phase: quick-260805-mzf*
*Completed: 2026-08-05 (Task 1만; Task 2 대기)*

## Self-Check: PASSED
- FOUND: WPF_Example/Utility/CaptureImageSaveService.cs
- FOUND commit: 44339bc
