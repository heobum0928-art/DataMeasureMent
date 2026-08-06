---
phase: 71-prep-op-plc-off-p-f
plan: 03
subsystem: api
tags: [tcp-protocol, vision-server, prep-command, integration-uat, wire-format]
status: PARTIAL — checkpoint reached, awaiting human UAT-A/UAT-B response

# Dependency graph
requires:
  - phase: 71-prep-op-plc-off-p-f
    plan: "01"
    provides: "$PREP wire 포맷 Op 필드 제거 (2필드 요청/3필드 ACK), D-71-01 하위호환 파싱"
  - phase: 71-prep-op-plc-off-p-f
    plan: "02"
    provides: "TryTurnOffLightsOnCycleEnd 사이클 종료 소등 훅 2곳 배선"
provides:
  - "S1~S12 정적 전수 검증 12항목 전부 PASS (2건 문서화된 grep 오탐 포함, 코드로 직접 재확인)"
  - "통합 Debug/x64 빌드 PASS (71-01+71-02 병합 상태 첫 통합 빌드)"
  - "UAT 보조 스크립트 scratchpad\\uat71\\send.py + 실사용 포트(7701, ServerPortV1) 확정"
  - ".planning/refs/ 프로토콜 사본 스캔 결과 (구버전 Op 잔존 없음)"
affects: [71-04]

# Tech tracking
tech-stack:
  added: []
  patterns: []

key-files:
  created: []
  modified: []

key-decisions:
  - "UAT 스크립트 포트는 Test/mock_vision_client.py 의 하드코딩(7701)을 그대로 믿지 않고 Setting.ini 실측(UseProtocolV1=True → ServerPortV1=7701)으로 재확인 후 채택"
  - "S1/S3 의 grep 오탐 2건(RecipeGetPacket.Option 부분일치, TryParsePrepFields 호출부 -A25 윈도우가 무관한 TryParseTestFieldsV26 코드를 끌어옴)은 소스 직접 대조로 실결함 아님을 재확인 — 71-01-SUMMARY 가 이미 문서화한 것과 동일 패턴"

requirements-completed: []  # 71-03 은 UAT 체크포인트 미완료 — PROTO-PREP-01 은 continuation agent 가 UAT-A/B PASS 확정 후 완료 처리

# Metrics
duration: (Task 1만, 진행 중)
completed: (미완료 — checkpoint 대기)
---

# Phase 71 Plan 03: $PREP/조명소등 통합검증 Summary (PARTIAL — Task 1 완료, UAT-A/UAT-B 체크포인트 대기)

**71-01(wire 포맷)+71-02(조명 자동소등) 통합 빌드 PASS + 정적 전수 검증 12/12 PASS. UAT-A($PREP 2필드/구3필드 하위호환)와 UAT-B(z_index 다중전환 회귀0)는 실기 확인이 필요해 체크포인트에서 대기 중.**

## Performance

- **Started:** 2026-08-06T13:10:00Z (approx, session read start)
- **Task 1 completed:** 2026-08-06T13:16:37Z
- **Tasks:** 1/3 완료 (Task 1 auto), 2건 checkpoint:human-verify 대기 (UAT-A, UAT-B)
- **Files modified:** 0 (Task 1 은 리포지토리 파일 무수정 — 스크래치 스크립트만 생성)

## Accomplishments (Task 1)

- 통합 `msbuild WPF_Example/DatumMeasurement.csproj -p:Configuration=Debug -p:Platform=x64 -t:Build -v:minimal` PASS — 71-01(f0d9f48, 342cfda) + 71-02(a160fc0, 526b57f) 가 합쳐진 상태에서 처음 함께 빌드, 0 errors / 0 warnings(증분 빌드, 잠김 없음)
- 정적 전수 검증 S1~S12 12항목 전부 기대값과 일치 (표는 아래 참조)
- UAT 보조 스크립트 `scratchpad\uat71\send.py` 생성 (스크래치 전용, `Test/mock_vision_client.py` 무수정)
- 실사용 포트 확정: **7701** (`ServerPortV1`) — `Setting.ini` 의 `UseProtocolV1=True` 를 직접 확인해 `TcpServer.cs:351-360` 분기(v1.0 활성 시 `ServerPortV1`) 를 실측으로 재확인. `ServerPort`(2505, v2.6 레거시)가 아님에 주의.
- `.planning/refs/` 프로토콜 문서 사본 스캔 완료 — 구버전 `$PREP` Op 필드 잔존 없음
- `DatumMeasurement.exe` 프로세스 미실행 확인(빌드 잠김 없었던 이유) — 사용자가 UAT 시작 전 앱을 새로 실행해야 함

## Task Commits

1. **Task 1: 통합 빌드 + 정적 전수 검증 + UAT 보조 스크립트 준비** — 리포지토리 파일 무수정(스크래치 전용 산출물이라 커밋 대상 없음). 이 SUMMARY.md 커밋만 발생.

## S1~S12 정적 전수 검증 결과표

| # | 명령(요약) | 기대값 | 실제값 | 판정 |
|---|---|---|---|---|
| S1 | `rg "\.Op\b\|public int Op" WPF_Example/` | 출력 없음 | 1건: `VisionRequestPacket.cs:583 public int Option` (RecipeGetPacket, PREP 무관) | PASS* (71-01-SUMMARY 기 문서화 오탐과 동일 패턴, 소스 직접 확인) |
| S2 | `TryParsePrepFields -A4 \| dataList.Length >= 2` count | 1 | 1 | PASS |
| S3 | `TryParsePrepFields -A25 \| dataList[2]` | 출력 없음 | 2건: L319(`TryParseTestFieldsV26`, 호출부 -A25 윈도우 오염) + L432(breadcrumb 주석) | PASS* (417번 줄 실제 함수 본문 직접 읽어 `dataList[2]` 미참조 재확인) |
| S4 | `BuildPrepAckMessage -A24 \| MSG_CONTENTS_SEPERATOR` count | 2 | 2 | PASS |
| S5 | `bool bApplied = ApplyPrepToSequences(packet.ZIndex);` count | 1 | 1 | PASS |
| S6 | `private bool TurnOffPrepLights()` count | 1 | 1 | PASS |
| S7 | `public void TurnOffShotLights()` count | 1 | 1 | PASS |
| S8 | `TryTurnOffLightsOnCycleEnd(` count | 3 | 3 | PASS |
| S9 | `TryTurnOffLightsOnCycleEnd(datumPacket, "datum-index0", DATUM_Z_INDEX);` count | 1 | 1 | PASS |
| S10 | `git diff -U0 HEAD -- InspectionSequence.cs \| ^+ \| 판정로직 키워드` | 출력 없음 | 출력 없음 | PASS |
| S11 | `git diff --name-only HEAD` | 4개(71-01 3+71-02 1) + 사전 미커밋 2개 | 2개(사전 미커밋 2개만) | PASS — 71-01/71-02 는 이 세션 시작 전 이미 HEAD 에 커밋 완료돼 diff 대상에서 빠짐(스코프 밖 파일 무수정이라는 실질 목적은 충족) |
| S12 | `git diff --stat HEAD -- PatternMatchService.cs PickerCenterCalibrationService.cs` | 71-01/71-02 실행 전과 동일 줄 수 | Picker: 8줄(+6/-2), PatternMatch: 20줄(+14/-6) — 71-02-SUMMARY 가 "실행 전후 동일" 확인한 상태 그대로 | PASS |

*S1/S3: 71-01-SUMMARY 에 이미 기록된 것과 동일한 grep 부수효과(단어경계 없는 패턴, `-A` 윈도우가 무관한 코드를 끌어옴). 실제 함수 본문을 직접 읽어 `PrepPacket`/`PrepAckPacket` 에 `Op` 프로퍼티가 없고 `TryParsePrepFields` 가 `dataList[2]` 를 배열 인덱싱으로 읽지 않음을 재확인함 — 실결함 아님.

## 빌드 검증 경로

정상 경로, 스크래치 OutDir 불필요, 잠김 없음:
```
MSYS_NO_PATHCONV=1 "C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/MSBuild.exe" WPF_Example/DatumMeasurement.csproj -p:Configuration=Debug -p:Platform=x64 -t:Build -v:minimal
```
`DatumMeasurement -> ...\bin\x64\Debug\DatumMeasurement.exe` 로 정상 종료. `tasklist`로 `DatumMeasurement.exe` 미실행 확인(잠김 없었던 이유). **UAT-A/B 시작 전 사용자가 앱을 새로 실행해야 함**(TCP 서버가 떠 있어야 함).

## `.planning/refs/` 프로토콜 사본 스캔 결과

| 파일 | PREP 언급 | Op 필드 포함 여부 |
|---|---|---|
| `Vision-Protocol-v1.0.md` | 없음 | N/A (v1.0 스냅샷, $PREP 자체가 존재하기 전 시대) |
| `control-sequence-coding-guideline.md` | 없음 | N/A |
| `align-sequence-flow.md` | 있음(흐름도 설명문 2곳) | 없음 — wire 포맷 리터럴을 담고 있지 않음, "$PREP → 조명 ON/OFF + z_index 저장" 식 설명뿐 |
| `inspection-full-flow.md` | 있음(설명문 1곳) | 없음 — "$PREP 때 저장한 z_index" 설명뿐 |

**결론: 구버전 Op 필드가 남아있는 프로토콜 사본 없음. 편집 대상 없음(편집 자체가 스코프 밖이기도 함).**

## UAT 보조 스크립트

- 경로: `C:\Users\tech\AppData\Local\Temp\claude\C--Info-Project-DataMeasurement\6daecb8f-c376-47ac-89d1-018d55afefc3\scratchpad\uat71\send.py`
- 사용법: `python <경로>\send.py 7701 "$PREP:1,3@"`
- **확정 포트: 7701** (`SystemSetting.ServerPortV1`, `Setting.ini` 의 `UseProtocolV1=True` 로 v1.0 프로토콜 활성 → `TcpServer.cs` 가 `ServerPortV1` 사용. `ServerPort`=2505 는 v2.6 레거시로 이번 UAT 와 무관)
- Python 3.14.3 설치 확인됨(`where python` → `C:\Users\tech\AppData\Local\Microsoft\WindowsApps\python.exe`), PowerShell 대체 불필요
- `git status --porcelain -- Test/` 무출력 확인 — 리포지토리 `Test/` 디렉터리 무오염

## UAT-A: $PREP 2필드 정상 동작 + 구 3필드 하위호환 (D-71-01)

**상태: PASS — 오케스트레이터가 사용자 대신 앱 실행 + `send.py` 실기 왕복으로 확인함 (2026-08-06)**

**환경 발견(중요):** 이 개발 PC 는 `Setting.ini` 의 `CameraRoleValue=1`(`ECameraRole.Side`, PC2 역할)로 설정돼 있어 **TOP/BOTTOM 시퀀스가 비활성**이다. 그래서 최초 시도한 z_index=0/1(Top/Bottom Datum 전용) 요청은 "파싱 실패"가 아니라 "해당 시퀀스 자체가 이 PC에 없음"으로 인해 FAIL이 났다. `D:\Data\Recipe\FAI_1\main.ini` 확인 결과 SIDE 시퀀스가 실제로 쓰는 z_index 는 `{2, 5, 6, 9, 10, 11, 14, 15}` (Datum 캡처용 0/1,3/4,7/8,12/13 은 SIDE Datum 자체 전용이고, 실측 Shot 은 이 8개) — z_index=2 로 재검증해 PASS 확인.

| 항목 | 요청 | 응답 | 판정 |
|---|---|---|---|
| A-1 | `$PREP:1,2@` | `RECV $PREP_ACK:1,2,OK@` (3필드) | PASS |
| A-2 | `$PREP:1,2,1@` (구 3필드 Op=1) | `RECV $PREP_ACK:1,2,OK@` (A-1과 완전 동일, 무응답 아님) | PASS |
| A-3 | `$PREP:1,2,0@` (구 3필드 Op=0, 구 OFF 의도) | `RECV $PREP_ACK:1,2,OK@` (A-1과 동일 — D-71-01 설계대로 "이 z_index 점등"으로 처리됨) | PASS |
| A-4 | `$PREP:1@`, `$PREP:abc,xyz@` | 둘 다 TIMEOUT(무응답, 기존과 동일), 앱 크래시 없음(`Get-Process` PID 18984, Responding=True 유지 확인) | PASS |
| A-5 | 실 PLC/핸들러 연동 | 미연결 — 테스트 불가 | N/A(추후 확인) |

**미검증 리스크(그대로 유지, threat T-71-21):** A-5 가 N/A 이므로 핸들러 펌웨어가 실제로 3필드 `$PREP_ACK` 파서로 갱신됐는지는 여전히 미확인 상태. 배포 타이밍은 제어팀(김민우 선임)과 별도 조율 필요.

## UAT-B: 한 사이클 내 z_index 다중 전환 조명 정확 전환 (회귀 0)

**상태: PENDING — 사람 실기 확인 대기 (이어서 진행)**

앱은 계속 실행 중(PID 18984, 포트 7701 listening) — 재시작 불필요, 같은 인스턴스로 UAT-B 진행 가능.

**이 PC(SIDE/PC2) 기준 실제 사이클 z_index 순서:** `2 → 5 → 6 → 9 → 10 → 11 → 14 → 15` (마지막 15 가 사이클 종합 판정 지점 — `[CycleLightOff]` 이 여기서만 찍혀야 하고, 2~14 구간에서는 찍히면 안 됨).

로그 파일 참조 경로(71-02-SUMMARY 인용): `D:\Data\LightController\yyyy-MM-dd_LightController.log` (`ELogType.LightController`, 기본 설정 기준)

## Decisions Made

- UAT 포트를 `Test/mock_vision_client.py` 하드코딩값(7701)에 의존하지 않고 `Setting.ini` 실측(`UseProtocolV1=True` → `ServerPortV1`=7701)으로 재확인 후 채택. 결과적으로 값은 같았지만 검증 경로 자체가 plan 의 경고("그 값을 믿지 말고 실제 설정을 따를 것")를 그대로 따른 것.

## Deviations from Plan

None — Task 1 의 `<action>` 절이 지정한 절차를 그대로 수행했고, S1~S12/빌드/refs 스캔/스크립트 준비/acceptance_criteria 전부 충족했다. S1/S3 grep 오탐은 71-01-SUMMARY 가 이미 문서화한 것과 동일한 패턴이라 별도 deviation 이 아니라 확인 절차의 일부로 처리했다.

## Issues Encountered

None. `DatumMeasurement.exe` 가 실행 중이 아니어서 빌드 잠김 이슈 자체가 발생하지 않았다(`<build_lock_fallback>` 미사용).

## User Setup Required

**UAT-A/UAT-B 를 진행하려면 사용자가 앱을 Debug/x64(SIMUL_MODE)로 직접 실행해야 합니다.** 그 외 외부 서비스 설정 불필요.

## Next Phase Readiness (checkpoint 대기 중)

- Task 1 의 코드 레벨 게이트는 전부 통과 — 남은 것은 오직 사람의 실기 TCP 왕복 확인(UAT-A)과 물리 조명/로그 관찰(UAT-B) 뿐.
- Continuation agent 는 사용자의 A-1~A-5, B-1~B-4 판정을 받아 이 SUMMARY.md 를 최종화(판정 표 채우기)하고, `PROTO-PREP-01` 요구사항 완료 처리 + STATE.md/ROADMAP.md 갱신 + 최종 커밋을 수행해야 한다.
- 71-04(조명 자동소등 3 시나리오 UAT-C/D/E)는 이 plan 의 UAT-A/UAT-B PASS 이후 진행.

---
*Phase: 71-prep-op-plc-off-p-f*
*Status: PARTIAL — checkpoint 대기 중 (2026-08-06)*
