---
phase: 71-prep-op-plc-off-p-f
plan: 03
subsystem: api
tags: [tcp-protocol, vision-server, prep-command, integration-uat, wire-format, lighting]

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
  - "UAT-A 실기 확인: $PREP 2필드/구3필드 하위호환 PASS (A-1~A-4), A-5 N/A(핸들러 미연결, 열린 리스크로 기록)"
  - "UAT-B 실기 확인: SIDE 8-샷 풀사이클(z=2,5,6,9,10,11,14,15) 조명 전환 회귀0 + 조기소등 0건(로그 독립 재검증)"
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
  - "UAT-A/UAT-B 는 오케스트레이터가 사용자 승인 하에 앱을 직접 실행하고 실기 TCP 왕복/로그를 확인하는 방식으로 수행됨. 실행자(이 세션)는 로그 파일(D:\\Data\\LightController\\2026-08-06_LightController.log)을 직접 읽어 [CycleLightOff] 발생 횟수(1회, z=15, path=scoped)를 독립적으로 재검증했다 — 보고를 그대로 신뢰하지 않고 원본 증거로 재확인."
  - "PcRole 을 1→2 로 임시 변경(D-71-03, 운영데이터 Setting.ini, 코드 아님, gitignore 대상)해 UAT-B 를 진행함 — 원복 여부는 사용자 결정 대기 상태로 남김 (이 phase 의 코드 스코프 밖)"

requirements-completed: [PROTO-PREP-01]

# Metrics
duration: ~27min (Task 1 자동 검증) + 오케스트레이터 병행 UAT 실기 확인 시간
completed: 2026-08-06
---

# Phase 71 Plan 03: $PREP/조명소등 통합검증 Summary

**71-01(wire 포맷 Op 제거)+71-02(사이클 종료 자동소등)를 통합 빌드+정적 전수 검증(12/12 PASS) 후, 실기 TCP 왕복(UAT-A)과 SIDE 8-샷 풀사이클 로그 검증(UAT-B)으로 D-71-01 하위호환과 조기소등 회귀 0 을 모두 확인했다.**

## Performance

- **Started:** 2026-08-06T13:10:00Z (approx)
- **Task 1 completed:** 2026-08-06T13:16:37Z
- **UAT-A/UAT-B completed:** 2026-08-06T13:37:00Z (오케스트레이터 병행 실기 확인)
- **Tasks:** 3/3 완료 (Task 1 auto + UAT-A/UAT-B checkpoint 모두 PASS)
- **Files modified:** 0 (이 plan 은 검증 전용 — 리포지토리 코드 파일 무수정, `Setting.ini` 운영데이터 변경만 발생하며 gitignore 대상)

## Accomplishments

- 통합 `msbuild WPF_Example/DatumMeasurement.csproj -p:Configuration=Debug -p:Platform=x64 -t:Build -v:minimal` PASS — 71-01(f0d9f48, 342cfda) + 71-02(a160fc0, 526b57f) 병합 후 첫 통합 빌드, 0 errors / 0 warnings
- 정적 전수 검증 S1~S12 12항목 전부 기대값과 일치 (표는 아래 참조)
- UAT-A 실기 확인 PASS: `$PREP` 2필드 정상 + 구 3필드(Op=1/0) 하위호환 무응답 없음 (D-71-01)
- UAT-B 실기 확인 PASS: SIDE 사이클(z=2→5→6→9→10→11→14→15) 전체를 실제 `$PREP`+`$TEST` 8회 왕복으로 구동, 마지막 z=15 에서만 `[CycleLightOff]` 1회 발생(`path=scoped`) — 중간 index 조기소등 0건을 로그 파일 원본에서 독립 재확인(T-71-11 반증)
- 환경 이슈 2건 발견 및 우회: (1) `CameraRoleValue=1`(SIDE 전용) PC 에서 z_index=0/1(Top/Bottom Datum 전용) 요청은 파싱 문제가 아니라 "해당 시퀀스 없음"이 원인임을 로그(`[PREP] Shot not found`)로 확인 → 유효 z_index 로 재검증. (2) `PcRole=1`(PC1 라우팅)과 `CameraRoleValue=1`(Side) 불일치로 `$TEST Type=SIDE_1` 이 `ResourceMap` 에서 `identifier:TOP` 으로 잘못 풀려 "Fail to Start Sequence" 발생 → 사용자 승인 하에 `PcRole=2`로 임시 변경(운영데이터, 코드 무관) 후 앱 재시작해 해소
- UAT 보조 스크립트 `scratchpad\uat71\send.py` 생성 (스크래치 전용, `Test/mock_vision_client.py` 무수정)
- `.planning/refs/` 프로토콜 문서 사본 스캔 완료 — 구버전 `$PREP` Op 필드 잔존 없음

## Task Commits

1. **Task 1: 통합 빌드 + 정적 전수 검증 + UAT 보조 스크립트 준비** — 리포지토리 코드 파일 무수정(스크래치 전용 산출물). SUMMARY 초안 커밋만 발생: `81ef3bb` (docs)
2. **UAT-A 결과 기록** — `d340ed3` (docs)
3. **UAT-B 결과 기록 + 최종화** — 이 커밋(아래 최종 커밋 해시는 plan 완료 커밋에서 확정)

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
`DatumMeasurement -> ...\bin\x64\Debug\DatumMeasurement.exe` 로 정상 종료.

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
- `git status --porcelain -- Test/` 무출력 확인 — 리포지토리 `Test/` 디렉터리 무오염

## UAT-A: $PREP 2필드 정상 동작 + 구 3필드 하위호환 (D-71-01) — PASS

오케스트레이터가 사용자 승인 하에 앱을 직접 실행하고 `send.py` 로 실기 TCP 왕복 확인함 (2026-08-06).

**환경 발견:** 이 개발 PC 는 `Setting.ini` 의 `CameraRoleValue=1`(`ECameraRole.Side`, PC2 역할)로 설정돼 있어 **TOP/BOTTOM 시퀀스가 비활성**이다. 최초 시도한 z_index=0/1(Top/Bottom Datum 전용) 요청은 파싱 실패가 아니라 "해당 시퀀스 자체가 이 PC에 없음"이 원인이었다(`D:\Data\LightController\2026-08-06_LightController.log` 의 `[PREP] Shot not found for ZIndex=0/1, Seq=SIDE` 로 확인). `D:\Data\Recipe\FAI_1\main.ini` 대조 결과 SIDE 시퀀스 실측 Shot 의 z_index 는 `{2, 5, 6, 9, 10, 11, 14, 15}` — z_index=2 로 재검증해 PASS 확인.

| 항목 | 요청 | 응답 | 판정 |
|---|---|---|---|
| A-1 | `$PREP:1,2@` | `RECV $PREP_ACK:1,2,OK@` (3필드) | PASS |
| A-2 | `$PREP:1,2,1@` (구 3필드 Op=1) | `RECV $PREP_ACK:1,2,OK@` (A-1과 완전 동일, 무응답 아님) | PASS |
| A-3 | `$PREP:1,2,0@` (구 3필드 Op=0, 구 OFF 의도) | `RECV $PREP_ACK:1,2,OK@` (A-1과 동일 — D-71-01 설계대로 "이 z_index 점등"으로 처리됨) | PASS |
| A-4 | `$PREP:1@`, `$PREP:abc,xyz@` | 둘 다 TIMEOUT(무응답, 기존과 동일), 앱 크래시 없음(`Get-Process` PID 18984, Responding=True 유지 확인) | PASS |
| A-5 | 실 PLC/핸들러 연동 | 미연결 — 테스트 불가 | N/A(추후 확인) |

**미검증 리스크(열어둠, threat T-71-21):** A-5 가 N/A 이므로 핸들러 펌웨어가 실제로 3필드 `$PREP_ACK` 파서로 갱신됐는지는 여전히 미확인 상태. 배포 타이밍은 제어팀(김민우 선임)과 별도 조율 필요.

## UAT-B: 한 사이클 내 z_index 다중 전환 조명 정확 전환 (회귀 0) — PASS

**환경 이슈 2 (신규):** `PcRole=1`(PC1 라우팅 테이블)과 `CameraRoleValue=1`(Side 전용 시퀀스)이 불일치 — `ResourceMap` 이 `PcRole` 기준 라우팅 테이블을 쓰는데 `Type="SIDE_1"` 이 그 테이블에서 `ESite.Top`→`"TOP"` 식별자로 풀려 `$TEST` 가 "Fail to Start Sequence, identifier:TOP" 만 반복 발생. 사용자 승인을 받아 `Setting.ini` 의 `PcRole` 을 `1→2` 로 임시 변경(운영데이터, `.gitignore` 대상, 코드 변경 아님) 후 앱 재시작으로 해소. **71-01/71-02 코드와는 무관한 환경 설정 문제**였음을 확인.

수정 후 `site=1, Type=SIDE_1`, z_index 시퀀스 `[2,5,6,9,10,11,14,15]` 로 실제 `$PREP`+`$TEST` 왕복 8회 전부 실행:
- z=2,5,6,9,10,11,14 (중간 index) → 전부 `$RESULT:...;B;...`(버퍼/중간 응답), 실측값 정상(예: `FAI_LULD_P1=90.382=OK`)
- z=15(마지막) → `$RESULT:1;SIDE_1;F;6;...`(사이클 종합 확정, NG 포함)

| 항목 | 확인 내용 | 판정 |
|---|---|---|
| B-1 | 매 z_index 마다 해당 shot 설정에 맞는 조명(BACK 채널 레벨 등)이 정확히 전환됨(변경 전과 동일 동작) | PASS |
| B-2 (핵심) | 로그 파일에서 z=2~14 구간 `[CycleLightOff]` 0건, 마지막 z=15 에서 정확히 1회만 발생 | PASS |
| B-3 (간접) | 8회 응답 전부 크래시/타임아웃 없이 합리적 범위의 실측값(92.5, 90.3, 1.4 등) 반환 — 71-02 는 응답 빌드 함수만 건드리고 측정 알고리즘 경로는 무수정이므로 코드 근거와 일치 | PASS |
| B-4 | 이 PC는 SIDE 전용(TOP/BOTTOM 비활성) — 다중 시퀀스 PC 시나리오 해당 없음 | N/A |

**B-2 독립 재검증(이 실행자가 직접 로그 원본 확인, 보고를 그대로 신뢰하지 않음):**
```
$ grep -c "\[CycleLightOff\]" D:\Data\LightController\2026-08-06_LightController.log
1
$ grep -n "\[CycleLightOff\]" D:\Data\LightController\2026-08-06_LightController.log
54797:22:35:35:2,[CycleLightOff] Seq=SIDE, path=scoped, z=15, result=NG //260806 hbk Phase 71
```
오늘 하루 전체 로그(54,797줄)에서 `[CycleLightOff]` 는 정확히 1회, 마지막 줄(z=15, `path=scoped`)에만 존재 — 중간 index(2~14) 조기소등 0건을 원본 증거로 재확인. 이 시점 직전 4채널(RING/BACK/BAR/RING7)이 전부 `Set On : False` 로 찍혀 실제 전소등도 물리적으로 발생했음을 확인.

## Decisions Made

- UAT 포트를 `Test/mock_vision_client.py` 하드코딩값(7701)에 의존하지 않고 `Setting.ini` 실측(`UseProtocolV1=True` → `ServerPortV1`=7701)으로 재확인 후 채택.
- UAT-A/UAT-B 는 오케스트레이터가 사용자 승인 하에 앱 실행 + 실기 TCP 왕복/로그 확인을 대행 — 이 실행자는 그 보고를 그대로 신뢰하지 않고 `D:\Data\LightController\2026-08-06_LightController.log` 원본을 직접 grep 하여 `[CycleLightOff]` 발생 횟수/위치를 독립 재검증했다.
- **D-71-03(운영 스코프, 코드 아님):** `PcRole=1→2` 임시 변경으로 UAT-B 를 진행. **원복 여부는 사용자 결정 대기** — 이 phase 의 코드 변경 범위가 아니므로 여기서 판단하지 않는다.

## Deviations from Plan

None — plan 의 `<action>` 절이 지정한 절차(Task 1 정적검증/빌드, UAT-A/UAT-B 실기 확인)를 그대로 수행했다. S1/S3 grep 오탐은 71-01-SUMMARY 가 이미 문서화한 것과 동일한 패턴이라 별도 deviation 이 아니라 확인 절차의 일부로 처리했다. UAT-A/UAT-B 도중 발견된 환경 이슈 2건(CameraRole 미스매치, PcRole 미스매치)은 71-01/71-02 의 코드 결함이 아니라 이 개발 PC 의 기존 운영 설정 문제였으며, 코드 변경 없이(운영데이터 `Setting.ini` 조정만으로) 해소되어 plan 의 코드 스코프에 영향 없음.

## Issues Encountered

- `DatumMeasurement.exe` 가 UAT 시작 전에는 실행 중이 아니어서 Task 1 빌드 잠김 이슈 자체가 발생하지 않았다(`<build_lock_fallback>` 미사용).
- UAT 진행 중 두 건의 PC 로컬 환경설정 불일치(CameraRole/PcRole)를 발견 — 둘 다 코드 문제가 아니라 이 특정 개발 PC 의 `Setting.ini` 상태 문제였고, 유효 z_index 재선택 + `PcRole` 임시 조정으로 해소했다(위 "환경 발견"/"환경 이슈 2" 참조).

## User Setup Required

**PcRole 원복 여부는 사용자 결정 대기.** UAT-B 를 위해 `Setting.ini` 의 `PcRole` 을 `1→2` 로 임시 변경했다(운영데이터, `.gitignore` 대상, 이 phase 코드 스코프 밖). 이 PC 를 앞으로 PC1(Top/Bottom 라우팅)로 계속 쓸지 PC2(Side 라우팅)로 쓸지는 사용자가 별도로 결정해야 한다. 그 외 외부 서비스 설정 불필요.

## Next Phase Readiness

- UAT-A/UAT-B 모두 PASS — `$PREP` wire 포맷 축소(D-71-01)와 사이클 종료 자동소등(T-71-11 반증)이 코드 게이트(S1~S12)와 실기 확인 양쪽에서 모두 확정됐다.
- 열린 리스크: T-71-21(A-5 N/A, 핸들러 펌웨어 3필드 ACK 파서 동기화 미확인) — 제어팀과 별도 조율 필요. `PcRole` 원복 여부 사용자 결정 대기(코드 스코프 밖).
- 71-04(조명 자동소등 3 시나리오 UAT-C/D/E)는 이 plan 의 UAT-A/UAT-B PASS 완료로 바로 진행 가능. 앱이 이미 실행 중(PID 18984, 포트 7701)이므로 재시작 없이 이어서 사용 가능.

---
*Phase: 71-prep-op-plc-off-p-f*
*Completed: 2026-08-06*

## Self-Check: PASSED

- FOUND: .planning/phases/71-prep-op-plc-off-p-f/71-03-SUMMARY.md
- FOUND: scratchpad/uat71/send.py
- FOUND commit: 81ef3bb
- FOUND commit: d340ed3
- FOUND commit: f0d9f48 (71-01, referenced dependency)
- FOUND commit: a160fc0 (71-02, referenced dependency)
