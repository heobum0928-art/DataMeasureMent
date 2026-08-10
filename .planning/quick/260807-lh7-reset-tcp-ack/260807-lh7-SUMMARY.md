---
phase: quick-260807-lh7
plan: 01
subsystem: tcp-protocol
tags: [tcp, vision-server, plc-protocol, reset, thread-safety, inspection-sequence]

# Dependency graph
requires:
  - phase: Phase 71 (prep-op-plc-off-p-f)
    provides: "$PREP/$TEST V1 프로토콜, _lastPrepZIndex, ApplyPrepToSequences 순회 패턴"
provides:
  - "$RESET:site@ 수신 → $RESET_ACK:site,OK|FAIL@ 응답 (신규 TCP 명령)"
  - "z_index 캐시(_lastPrepZIndex) 및 InspectionSequence 사이클 누적 상태 원격 복구 수단"
affects: [tcp-server, inspection-sequence, plc-protocol-docs]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "무락 컬렉션 크로스스레드 접근 방지: State==Idle 게이트 후에만 리셋 호출"
    - "파서 절대-null-금지 패턴 (TryParsePrepFields 하위호환 주석과 동일 계보) — TryParseResetFields는 항상 true 반환, 실패시 sentinel 폴백"

key-files:
  created: []
  modified:
    - WPF_Example/TcpServer/VisionRequestPacket.cs
    - WPF_Example/TcpServer/VisionResponsePacket.cs
    - WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs
    - WPF_Example/Custom/SystemHandler.cs

key-decisions:
  - "$RESET 은 site 필드를 라우팅에 쓰지 않고 ACK echo 전용으로만 사용 — $PREP 과 동일 정책(설계 제약 확인됨)"
  - "리셋 대상이 State!=Idle(검사 실행 중)이면 건드리지 않고 건너뛰며 ACK FAIL — _datumTransforms/_failedDatums(무락 Dictionary/HashSet)를 시퀀스 스레드와 동시 변경하면 컬렉션 손상(무한루프/예외) 위험이 있어 Idle 게이트를 스레드 안전성의 핵심 장치로 채택"
  - "Stop() 을 먼저 호출하지 않음 — SequenceBase.Stop() 은 RequestPacket!=null 이면 AddResponse() 를 호출해 PLC 가 요청하지 않은 $RESULT 를 추가로 내보내는 부작용이 있어 미채택"
  - "m_nLastZIndex 재산출 생략 — 이 필드를 읽는 두 지점(AddResponseV1Cycle, HandleDatumIndexResponse) 모두 읽기 직전 ComputeLastZIndex() 로 스스로 덮으므로 0 상태가 판정에 관여 불가"

patterns-established:
  - "$RESET_ACK 직렬화는 $PREP_ACK 빌더를 그대로 미러(문자열 리터럴 OK/FAIL 유지) — 코드 형태를 동일하게 유지해 유지보수 시 눈으로 비교 가능하게 함"

requirements-completed: [RESET-01, RESET-02, RESET-03, RESET-04]

# Metrics
duration: ~35min
completed: 2026-08-07
---

# Phase quick-260807-lh7: $RESET TCP 명령 신설 Summary

**PLC가 보내는 `$RESET:site@`를 새로 처리해 `_lastPrepZIndex`와 모든 Idle 상태 InspectionSequence의 사이클 누적 상태(NG/즉시-F 래치, 크로스-Z 이미지, Datum transform/실패 집합)를 클린 슬레이트로 되돌리고 `$RESET_ACK:site,OK|FAIL@`로 응답한다 — 실행 중인 시퀀스는 스레드 안전을 위해 건드리지 않고 건너뛴다.**

## Performance

- **Duration:** ~35 min
- **Started:** 2026-08-07T06:08Z (approx)
- **Completed:** 2026-08-07T06:43Z
- **Tasks:** 3/3 completed
- **Files modified:** 4

## Accomplishments
- `$RESET:site@` 수신 경로 신설 — 어떤 변형 입력(`$RESET@`, `$RESET:@`, `$RESET:abc@`)도 무응답으로 떨어지지 않음(사고 재발 방지: PLC ACK 무한 대기 → 라인 정지)
- `$RESET_ACK:site,OK|FAIL@` 응답 신설, PREP_ACK 빌더와 동일 스타일 유지
- `_lastPrepZIndex` 복구 수단 신설 — 이전에는 앱 재시작이 유일한 복구 방법이었음
- `InspectionSequence.ResetCycleStateForProtocolReset()` 공개 진입점 신설 — 사이클 판정 래치 + 크로스-Z 이미지 캐시 + Datum transform/실패 집합을 한 번에 클린 슬레이트로 되돌림
- 스레드 안전성 확보 — `State==Idle` 인 시퀀스만 리셋, 실행 중인 시퀀스는 무락 컬렉션(`_datumTransforms`/`_failedDatums`) 손상 위험을 원천 차단하기 위해 건너뛰고 Error 로그로 보고
- 4개 파일 전부 순수 additive(삭제줄 0), 기존 `$LIGHT/$SITE_STATUS/$TEST/$PREP/$ALIGN_TEST/$ALIGN_CALIB/$ALIVE` 처리 경로 무변경
- Debug/x64 Rebuild(실제 출력 폴더) 신규 `error CS` 0건 통과

## Task Commits

Each task was committed atomically:

1. **Task 1: VisionRequestPacket.cs — $RESET 수신 파싱 신설** - `42fc954` (feat)
2. **Task 2: VisionResponsePacket.cs — $RESET_ACK 응답 신설** - `529b4fc` (feat)
3. **Task 3: 시퀀스 리셋 진입점 + ProcessReset 배선 + Debug/x64 Rebuild** - `24c4592` (feat)

**Plan metadata:** (orchestrator commits separately)

_Note: This plan had no TDD tasks — all `type="auto"`._

## Files Created/Modified
- `WPF_Example/TcpServer/VisionRequestPacket.cs` - `VisionRequestType.Reset` enum + `CMD_RECV_RESET` 상수 + `ResetPacket` 클래스 + `TryParseResetFields`(항상 true 반환) + `Convert(string)` case + `AsReset()`
- `WPF_Example/TcpServer/VisionResponsePacket.cs` - `EVisionResponseType.ResetAck` enum + `CMD_SEND_RESET_ACK` 상수 + `ResetAckPacket` 클래스 + `BuildResetAckMessage` + `Convert` case + `AsResetAck()`
- `WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs` - `public ResetCycleStateForProtocolReset()` — 기존 private `ResetCycleState`/`ClearCrossZImages`/`ClearDatumTransforms` 를 묶은 cross-class 공개 진입점
- `WPF_Example/Custom/SystemHandler.cs` - `MainRun` switch에 `case VisionRequestType.Reset` 추가, `ProcessReset(ResetPacket)` 신설(`_lastPrepZIndex=0` + `ResetSequenceCycleStates()` 호출 + ACK 생성), `ResetSequenceCycleStates()` 신설(`State==Idle` 게이트)

## Decisions Made
- `$RESET` 은 `$PREP` 과 동일하게 site 를 라우팅에 쓰지 않고 ACK echo 전용으로 사용(설계 제약)
- 검사 실행 중(State != Idle)인 시퀀스는 리셋을 건너뛰고 ACK FAIL — 무락 컬렉션(`_datumTransforms`/`_failedDatums`/`_alignFailedDatums`)을 시퀀스 스레드와 동시 변경하는 크래시 위험(무한루프/`InvalidOperationException`)을 원천 차단
- `SequenceBase.Stop()` 선호출 안 함 — PLC 가 요청하지 않은 `$RESULT` 응답을 추가로 발생시키는 부작용 회피
- `m_nLastZIndex` 는 리셋 후 재산출하지 않음 — 이를 읽는 두 지점이 읽기 직전 자체적으로 `ComputeLastZIndex()` 로 덮으므로 무해

## Deviations from Plan

None - plan executed exactly as written. 4개 파일 전부 계획된 삽입 지점대로 순수 additive 변경만 적용했고, 정적 검증 게이트(삭제줄 0, 심볼 카운트, 기존 경로 무변경, Idle 가드 존재, ResourceMap 무변경, 사용자 미커밋 3파일 보존)가 전부 예상대로 통과했다.

**참고(결함 아님):** Task 3 verify 명령의 `grep -c "_lastPrepZIndex = 0;"` 는 계획서상 "1건"을 기대했으나 실측 2건이 나왔다. 원인은 grep 패턴이 우리가 추가한 `ProcessReset` 내부 대입문(`_lastPrepZIndex = 0; //260807...`) 뿐 아니라 기존 필드 선언줄(`private volatile int _lastPrepZIndex = 0;`, 18번째 줄, 커밋 전 원문)도 함께 매칭했기 때문이다. 두 줄 모두 정당한 코드이며 이는 verify 스크립트의 grep 패턴이 느슨했던 것일 뿐, 구현 결함이 아니다.

## Known Stubs

None.

## Threat Flags

None — 이 플랜의 신규 표면(`$RESET` TCP 명령)은 이미 plan의 `<threat_model>`(T-LH7-01~06)에 STRIDE 등록되어 있고, 구현이 그 등록된 완화책(mitigate)을 그대로 따랐다. 신규 네트워크 엔드포인트/인증경로/스키마 변경 등 threat_model 밖의 표면은 발견되지 않았다.

## Verification Results

정적 검증 전부 PASS:
1. 4개 파일 전부 `git diff --numstat` 삭제 컬럼 0 (순수 additive)
2. 신규 심볼 존재/개수 grep 카운트 전부 기대치 일치 (`CMD_RECV_RESET` 3건, `VisionRequestType.Reset` 2건, `class ResetPacket` 1건, `AsReset` 1건, `TryParseResetFields` 코드줄 2건 / `CMD_SEND_RESET_ACK` 2건, `EVisionResponseType.ResetAck` 3건, `class ResetAckPacket` 1건, `AsResetAck` 1건, `BuildResetAckMessage` 코드줄 2건 / `ResetCycleStateForProtocolReset` 2건, `ProcessReset` 2건, `ResetSequenceCycleStates` 2건)
3. 기존 경로(ALIVE 가드, PREP 파서, PREP_ACK 빌더, `ProcessPrep`, `ApplyPrepToSequences`, `ResetCycleState()`, `BeginCrossZImageCycle()`) 원문 그대로 확인
4. `ResourceMap.cs` — `git status`/`git diff` 양쪽 무출력(무변경) 확인
5. `EContextState.Idle` 비교가 `Custom/SystemHandler.cs` 951번째 줄에 실물로 존재
6. 사용자 미커밋 3파일(csproj, `Custom/Device/LightHandler.cs`, `SystemHandler.cs`)이 `git status --porcelain` 에 baseline 그대로 남고 우리 커밋 3건 어디에도 포함되지 않음(각 커밋 diff --stat 로 확인)
7. Debug/x64 Rebuild(`/t:Rebuild`, 실제 출력 폴더 `WPF_Example/bin/x64/Debug/`) — `DatumMeasurement.exe` 생성 확인, 신규 `error CS` 0건 (기존 CS0618 사용 중단 경고만 존재, 이 플랜과 무관)

실기 확인(이 플랜 범위 밖, 사용자/오케스트레이터가 앱 기동 후 TCP 로 별도 수행 필요):
- `$RESET:1@` → `$RESET_ACK:1,OK@` 수신 확인
- `$RESET@`(필드 없음) → `$RESET_ACK:0,OK@` 수신 확인(무응답 아님)
- `$PREP:1,5@` → `$RESET:1@` → `$TEST:...@` 순서 z_index 0(Datum) 처리 Trace 로그 확인

## Self-Check: PASSED

- FOUND: WPF_Example/TcpServer/VisionRequestPacket.cs (ResetPacket/AsReset/TryParseResetFields 확인됨)
- FOUND: WPF_Example/TcpServer/VisionResponsePacket.cs (ResetAckPacket/AsResetAck/BuildResetAckMessage 확인됨)
- FOUND: WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs (ResetCycleStateForProtocolReset 확인됨)
- FOUND: WPF_Example/Custom/SystemHandler.cs (ProcessReset/ResetSequenceCycleStates 확인됨)
- FOUND commit 42fc954 (git log)
- FOUND commit 529b4fc (git log)
- FOUND commit 24c4592 (git log)
- FOUND: WPF_Example/bin/x64/Debug/DatumMeasurement.exe (Rebuild 산출물)
