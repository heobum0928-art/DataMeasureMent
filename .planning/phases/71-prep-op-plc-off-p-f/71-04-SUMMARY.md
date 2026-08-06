---
phase: 71-prep-op-plc-off-p-f
plan: 04
subsystem: api
tags: [tcp-protocol, vision-server, lighting, cycle-lifecycle, uat, datum]

# Dependency graph
requires:
  - phase: 71-prep-op-plc-off-p-f
    plan: "03"
    provides: "S1~S12 정적 전수 검증 PASS, 통합 Debug/x64 빌드 PASS, UAT-A/UAT-B 실기 PASS"
provides:
  - "UAT-C(정상 P 종료 전체소등) 4/4 항목 실기 PASS"
  - "UAT-D(NG 누적 F 종료 전체소등) — 71-03 UAT-B 재인용 + 이번 세션 D-1(중간 index NG) 레시피 대조로 보강 확인 PASS"
  - "UAT-E(Datum 즉시실패 F 경로 소등, path=datum-index0) — 정적검증(71-03 S9)만 PASS, 실기(TCP) 검증은 이 PC(SIDE 전용) 환경 제약으로 불가 확인, 열린 리스크로 문서화"
  - "Phase 71 최종 완료 판정 (CONTEXT.md 8항목 대조표)"
affects: []

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "환경 제약으로 실기 검증 불가한 코드 경로는 정적 검증(배선 확인)만으로 커버하고 열린 리스크로 명시 — A-5(71-03) 와 동일 카테고리 처리"

key-files:
  created: []
  modified: []

key-decisions:
  - "UAT-C/D/E 는 SIMUL_MODE(물리 조명 없음) 이므로 오케스트레이터가 scratchpad TCP 스크립트(send.py/cycle_test.py)로 직접 $PREP/$TEST 왕복을 구동하고 D:\\Data\\LightController 로그를 원본 대조하는 방식으로 진행 — 실행자(이 세션)는 사용자 응답을 대신 지어내지 않고 코드/레시피 분석으로 절차를 지원했다."
  - "UAT-D 는 71-03 UAT-B 데이터 재사용으로 충분하다고 판단 — 재실행 대신 D-1(중간 z_index NG) 요구사항을 D:\\Data\\Recipe\\FAI_1\\main.ini 직접 대조로 사후 보강 확인(FAI_3-1_D1→z=2, FAI_C13-14_P1→z=11, 둘 다 중간 index)."
  - "UAT-E(E-1~E-7) 는 이 PC(CameraRoleValue=1, SIDE 전용)에서 TCP 실기 검증이 구조적으로 불가능함을 두 단계 증거로 확정: (1) 코드 — SIDE 의 4개 Datum 전부 DualImage 크로스-Z 타입이라 z=0/1 각각은 role A/B '캡처만'(Action_FAIMeasurement.cs 의 `MarkDatumFailed 미설정` 분기)이고 실패 판정은 완성 index(z=1/4/8/13)에서만 나 `_failedDatums` 가 z=0 시점엔 항상 비어있음, (2) 프로토콜 — 오케스트레이터의 실측: Datum 전용 z_index(0,1) 는 `[SHOTS]` 목록에 없어(별도 `FIXTURE_SIDE_DATUM_*` 섹션) `$PREP` 의 `ApplyPrepToSequences`→`ApplyShotLights` 자체가 Shot 을 못 찾아 `$PREP_ACK:...,FAIL@` 로 실패하고 `[CycleLightOff]` 가 이 구간에 전혀 안 찍힘. CameraRoleValue 를 TOP/BOTTOM 으로 전환하는 옵션은 SIDE 시퀀스 전체 비활성화라는 더 큰 환경 범위 변경이라 이번 세션에서 채택하지 않고, 정적 검증(71-03 S9)만으로 코드 레벨 커버리지를 확보한 뒤 열린 리스크로 남겼다."
  - "PatternMinScore(Side_Datum_3-1, 원본 0.6) 를 UAT-E 시도 중 2.0 으로 임시 변경했다가 즉시 0.6 으로 원복 + 앱 재시작 — 레시피 상태 UAT 시작 전과 동일함을 확인."

requirements-completed: [PROTO-PREP-01]

# Metrics
duration: ~90min (Task 1~3 체크포인트 처리 + 오케스트레이터 병행 실기 UAT-C/D 분석 + UAT-E 근본원인 코드/레시피 조사)
completed: 2026-08-06
---

# Phase 71 Plan 04: 조명 자동소등 3-시나리오 개별 UAT (UAT-C/D/E) Summary

**정상 P 종료(UAT-C)·NG 누적 F 종료(UAT-D) 전체소등을 로그 원문(`path=scoped`, result OK/NG)으로 실기 확정했고, Datum 즉시실패 경로(UAT-E, `path=datum-index0`)는 이 SIDE 전용 PC에서 TCP로 도달 불가능함을 코드+레시피 이중 증거로 확정, 정적 배선검증(71-03 S9)만으로 코드 커버리지를 남기고 열린 리스크로 문서화했다.**

## Performance

- **Duration:** ~90 min
- **Started:** 2026-08-06T13:39:59Z (71-03 완료 직후 이어서)
- **Completed:** 2026-08-06T15:11:41Z
- **Tasks:** 3/3 (전부 `checkpoint:human-verify`, 코드 변경 없음 — `files_modified: []`)
- **Files modified:** 0 (리포지토리 코드 무수정. `D:\Data\Recipe\FAI_1\main.ini` 의 5개 공차값 + `Side_Datum_3-1.PatternMinScore` 는 UAT 과정에서 임시조작 후 전부 원복 확인됨, 운영데이터라 리포지토리 diff 대상 아님)

## Accomplishments

- **UAT-C (정상 P 종료 전체소등): C-1~C-4 전부 실기 PASS**
  - C-1: 5개 지점 공차 임시확대(정확한 원본값 기록) → 재시작 → z=2,5,6,9,10,11,14,15 전체 완주, 마지막 `$RESULT:1;SIDE_1;P;6;...`(전부 OK) 확인 → 즉시 전부 원복 + 재시작
  - C-2/C-3: 마지막 TEST 응답 직후 `RING/BACK/BAR - Set On : False` 연속 + `[CycleLightOff] Seq=SIDE, path=scoped, z=15, result=OK` 정확히 1줄
  - C-4: 소등 직후 다음 사이클 정상 재점등, 측정값 회귀 없음(`FAI_3-1_D1_P1=92.525=OK`)
- **UAT-D (NG 누적 F 종료 전체소등): 71-03 UAT-B 재인용으로 충족 — 이 세션에서 D-1 요구사항 보강 확인**
  - 71-03 UAT-B 가 이미 D-2(중간구간 `[CycleLightOff]` 0건)/D-3(동시 소등)/D-4(`path=scoped, result=NG` 로그 원문)를 문자 그대로 충족한 상태였음
  - 이번 세션이 추가로 확인한 것: D-1("NG는 중간 z_index 에서 나야 함") 이 실제로 충족됨을 `main.ini` 직접 대조로 증명 — **FAI_3-1_D1 → SHOT_4, ZIndex=2**(중간) / **FAI_C13-14_P1 → SHOT_24, ZIndex=11**(중간) / FAI_C13-14 → SHOT_25, ZIndex=15(마지막). 즉 71-03 UAT-B 의 F 판정은 중간 index(z=2, z=11)에서 이미 NG 가 발생하고 마지막(z=15)까지 측정이 계속된 뒤 종합 F 로 나간 것이 데이터로 확정됨 — UAT-C 와 구분되는 진짜 "중간 NG" 시나리오였다.
  - D-5(공차 원복): N/A — 71-03 UAT-B 는 공차를 건드리지 않은 자연 상태의 레시피로 F 가 나온 것이라 원복할 대상 자체가 없었음
- **UAT-E (Datum 즉시실패 F 경로 소등, `path=datum-index0`): 이 PC 에서 TCP 실기 검증 불가 — 근본원인 이중 확정, 정적검증만으로 코드 커버리지 확보**
  - **1차 조사(코드, 이 세션):** `Action_FAIMeasurement.cs` DatumPhase 루프 확인 — SIDE 의 4개 Datum(`Side_Datum_3-1/3-2/4-2/4-1`) 전부 `VerticalTwoHorizontalDualImage`(크로스-Z) 타입이고, `ZIndexA/ZIndexB` 는 각각 (0,1)/(3,4)/(7,8)/(12,13) — 완성 index(`max(A,B)`) 는 1/4/8/13 이며, 비완성(pending) 캡처 tick 에서는 `MarkDatumFailed` 가 호출되지 않는다("Z1(비완성 index): 캡처만 — 실패 아님" 주석). 즉 z=0 시점엔 `_failedDatums` 가 항상 비어있어 `HandleDatumIndexResponse`→`DetectDatumFailure()` 가 절대 true 를 반환할 수 없다 — `path=datum-index0` 경로가 **이 레시피 구조상 도달 불가**. `ZIndexA=ZIndexB=-1`(z=0 자체가 완성 index) 인 단일이미지 Datum 은 `Top_Datum`/`Bottom_Datum` 뿐이며 둘 다 TOP/BOTTOM 시퀀스 소속(이 PC 는 `CameraRoleValue=1`/SIDE 전용이라 비활성).
  - **2차 조사(프로토콜, 오케스트레이터 실기):** `Side_Datum_3-1.PatternMinScore` 를 도달불가값(2.0)으로 임시변경 → 재시작 → z=0(role A)/z=1(role B, 완성index) 순서로 `$PREP`+`$TEST` 전송 → **두 `$PREP` 모두 `$PREP_ACK:...,FAIL@`**, 로그에 `[PREP] Shot not found for ZIndex=0/1, Seq=SIDE`(기존 Phase 64 로그), `[CycleLightOff]` 이 구간에 전혀 없음. 원인: Datum 전용 z_index(0,1) 는 `[SHOTS]` 목록에 없고 `FIXTURE_SIDE_DATUM_*` 별도 섹션에만 존재 — `ApplyPrepToSequences`→`ApplyShotLights(nZIndex)` 가 매칭 Shot 을 못 찾아 PREP 단계에서부터 막힌다. **1차 조사(z=0 시점 `_failedDatums` 공백)보다 한 단계 더 앞에서 막히는, 독립적인 두 번째 차단 지점.**
  - PatternMinScore 는 즉시 0.6(원본)으로 원복 + 재시작 완료, 레시피 clean 상태 확인.
  - **코드 레벨 커버리지:** 71-03 Task 1 S9(`TryTurnOffLightsOnCycleEnd(datumPacket, "datum-index0", DATUM_Z_INDEX)` 정확히 1회 배선 확인, PASS)가 유일한 검증 근거로 남는다 — 훅 자체는 코드상 올바르게 배선되어 있으나, 이 PC/레시피 조합으로는 실제로 그 코드에 진입하는 시나리오를 TCP 로 만들 수 없다.
  - **환경 대안(채택하지 않음, 사용자 결정 대기):** `CameraRoleValue` 를 TOP 또는 BOTTOM 으로 전환하면 `Top_Datum`/`Bottom_Datum`(z=0 자체가 완성 index) 으로 E-1~E-6 실기 검증이 가능해지지만, 이는 SIDE 시퀀스 전체를 비활성화하는 더 큰 범위의 환경 변경이라 이번 세션에서는 시도하지 않았다.

## Task Commits

이 plan 은 `checkpoint:human-verify` 3개로만 구성되어 코드 변경이 없다(`files_modified: []`). Task 별 코드 커밋은 없으며, 아래 plan 완료 문서 커밋만 발생한다.

**Plan metadata:** (이 커밋 직후 생성 — 해시는 커밋 완료 후 self-check 에서 기록)

## Files Created/Modified

- `.planning/phases/71-prep-op-plc-off-p-f/71-04-SUMMARY.md` (created, 이 파일)
- 코드 파일 무수정. `D:\Data\Recipe\FAI_1\main.ini`(운영 레시피) — UAT-C 용 5개 공차값 임시확대→원복, UAT-E 용 `Side_Datum_3-1.PatternMinScore` 임시변경(0.6→2.0)→원복(0.6). 둘 다 리포지토리 추적 대상이 아니며(`D:\` 드라이브 운영데이터) `git status` 에 나타나지 않는다.

## Decisions Made

프론트매터 `key-decisions` 4항목 참조 — 요약:
1. UAT-C/D/E 는 SIMUL_MODE 특성상 TCP 스크립트+로그 대조 방식으로 검증(물리 조명 부재).
2. UAT-D 는 71-03 UAT-B 재인용 + D-1 보강 확인으로 재실행 없이 종결.
3. UAT-E 는 이 PC 에서 실기 검증 불가를 코드+프로토콜 이중 증거로 확정, 정적검증만으로 코드 커버리지 유지, CameraRoleValue 전환 여부는 사용자 결정으로 이관.
4. UAT-E 시도 중 임시조작(PatternMinScore)은 즉시 원복 완료.

## Deviations from Plan

### 정보성 (Rule 위반 아님 — 계획이 예정한 "환경 미확인" 리스크가 실제로 발현된 경우)

**1. UAT-E(E-1~E-7) 실기 검증이 이 PC 환경에서 구조적으로 불가능함을 확인**
- **Found during:** Task 3 (UAT-E)
- **원인:** 이 PC 는 `CameraRoleValue=1`(SIDE 전용)이며, plan 이 요구한 `path=datum-index0`(Index 0 즉시실패) 경로는 z=0 자체가 Datum 완성 index 인 시퀀스(TOP/BOTTOM)에서만 실제로 트리거 가능하다. SIDE 의 모든 Datum 은 크로스-Z(DualImage) 타입이라 z=0/1 자체가 `[SHOTS]` 미등록(Datum 전용 섹션에만 존재)이라 `$PREP` 단계부터 실패하고, 설사 그 단계를 우회해도 z=0 은 "캡처만"(비완성)이라 `_failedDatums` 가 채워지지 않는다.
- **처리:** 코드를 고치거나 우회하지 않았다(plan 이 지정한 코드 변경 범위 밖). 71-03 S9 정적 검증을 코드 레벨 근거로 유지하고, 실기 검증 갭을 71-03 A-5 와 동일 카테고리의 열린 리스크로 이 SUMMARY 에 명시했다. 사용자가 CameraRoleValue 전환을 승인하면 후속 세션에서 실기 검증을 이어갈 수 있다.
- **Files modified:** 없음(운영 레시피 임시조작은 전부 원복됨, 커밋 대상 아님)
- **Impact on plan:** UAT-E 는 plan 이 요구한 "실기 PASS" 를 달성하지 못했다 — 아래 "Phase 71 최종 완료 판정"에서 항목 6 을 PARTIAL 로 정직하게 표기한다.

---

**Total deviations:** 1 (정보성, 코드 무변경) — **plan 이 요구한 3개 UAT 중 UAT-E 는 완전한 실기 PASS 를 달성하지 못하고 정적검증+열린리스크로 대체 종결됨.**

## Issues Encountered

- SIMUL_MODE 라 물리 조명 관찰이 불가능하여 모든 UAT 를 로그(`D:\Data\LightController\...log`) 원문 대조로 대체 — plan 이 원래 상정한 "육안 확인"과 다른 방식이지만, 로그가 `path`+`z`+`result` 조합으로 정확히 어느 코드 경로가 탔는지 위조 불가능하게 증명하므로 plan 의 검증 목적(threat T-71-34/T-71-35)은 동등하게 충족된다.
- UAT-E 원인 조사 과정에서 코드(Action_FAIMeasurement.cs DatumPhase, InspectionSequence.cs HandleDatumIndexResponse/ApplyPrepToSequences)를 다수 열람했으나 전부 읽기 전용 조사이며 수정하지 않았다.

## User Setup Required

**열린 결정 사항 (사용자 승인 필요, 코드 변경 아님):** UAT-E(E-1~E-6, `path=datum-index0`)의 완전한 실기 검증을 원한다면 `CameraRoleValue` 를 TOP 또는 BOTTOM 으로 임시 전환(SIDE 시퀀스 비활성화 수반)하는 별도 세션이 필요하다. 급하지 않다면 71-03 S9 정적 검증 + 이 SUMMARY 의 근본원인 분석으로 코드 정확성은 이미 충분히 뒷받침된다고 판단되며, 실기 검증은 실제 TOP/BOTTOM PC(PC1) 배치 시점으로 미뤄도 무방하다.

그 외 외부 서비스 설정 불필요.

## Next Phase Readiness

- Phase 71($PREP Op 필드 제거 + 조명 자동소등)의 코드 변경(71-01/71-02)은 전부 완료 + 통합 빌드 PASS + 정적검증 12/12 PASS 상태.
- 실기 검증은 6/8 항목 완전 PASS, 1항목(UAT-E) 정적검증만+열린리스크, 1항목(문서화, N/A) 이다 — 아래 최종 판정표 참조.
- 블로커 없음. 열린 항목(UAT-E 실기, A-5 핸들러 3필드 파서 동기화)은 모두 "코드는 준비됐고 배포/환경 시점에 확인"하는 성격이라 Phase 71 코드 자체의 머지를 막지 않는다.

---

## Phase 71 최종 완료 판정 (CONTEXT.md 완료 기준 8항목)

| # | 항목 | 근거 | 판정 |
|---|---|---|---|
| 1 | Debug/x64 빌드 PASS | 71-03 Task 1 (통합 빌드 0 errors) | ✅ **충족** |
| 2 | `$PREP:1,3@` → `$PREP_ACK:1,3,OK@` | 71-03 UAT-A (A-1~A-4 PASS) | ✅ **충족** |
| 3 | 한 사이클 내 z_index 다중 전환 회귀 0 | 71-03 UAT-B (SIDE 8-샷 풀사이클, 조기소등 0건) | ✅ **충족** |
| 4 | 정상 종료(P) 후 전체 소등 | 71-04 UAT-C (C-1~C-4 전부 실기 PASS, `path=scoped, result=OK` 로그) | ✅ **충족** |
| 5 | NG 누적 종료(F) 후 전체 소등 | 71-04 UAT-D (71-03 UAT-B 재인용 + D-1 보강확인, `path=scoped, result=NG` 로그) | ✅ **충족** |
| 6 | Datum 즉시실패(F) 경로 소등 | 71-03 S9(정적, 훅 배선 확인) **만** PASS. 71-04 UAT-E 실기(TCP) 검증은 이 PC(SIDE 전용) 환경 제약으로 불가 확정(근본원인 이중 확정: DualImage 크로스-Z z=0 캡처-only + `$PREP` Shot 미등록). | ⚠️ **PARTIAL — 코드 정적검증만 충족, 실기 검증은 열린 리스크로 이월** |
| 7 | 구 3필드 요청 처리 방식 = D-71-01 관대한 파싱 | 71-01 확정 + 71-03 UAT-A(A-2/A-3 실기 확인) | ✅ **충족** |
| 8 | 프로토콜 문서 갱신 | CONTEXT locked: `디팜스테크_Vision_Protocol_v1.3.xlsx` 가 이미 목표 상태라 불필요 | ✅ **충족(N/A 대상 확인 완료)** |

**종합: 8항목 중 7항목 완전 충족, 1항목(#6) 코드 레벨 충족 + 실기 검증 열린 리스크.** Phase 71 코드(71-01/71-02)는 프로덕션 반영 가능한 상태이나, `path=datum-index0` 경로의 실기 확인은 TOP 또는 BOTTOM 시퀀스가 활성화된 PC(정식 PC1 배치 환경)에서 별도로 수행되어야 완전 종결된다.

**열린 리스크 (carry-over):**
- **T-71-21 (71-03 유래):** A-5 — 실 핸들러 펌웨어가 3필드 `$PREP_ACK` 파서로 갱신됐는지 미확인. 제어팀(김민우 선임) 별도 조율 필요.
- **T-71-38 (신규, 이 plan):** UAT-E(`path=datum-index0`) 실기 미검증 — TOP/BOTTOM 활성 PC 에서 후속 확인 필요. 코드 정확성은 71-03 S9(정적)로 뒷받침되며, 근본원인(SIDE 레시피 구조상 z=0/1 이 `[SHOTS]` 미등록 + 크로스-Z Datum 의 z=0 은 캡처-only)은 이 SUMMARY 에 상세 기록되어 재조사 불필요.
- **PcRole 원복 여부 (71-03 유래):** 여전히 사용자 결정 대기, 이 plan 의 코드 스코프 밖.

---
*Phase: 71-prep-op-plc-off-p-f*
*Completed: 2026-08-06*
