---
phase: 68
slug: z-cross-z-dual-image-3-2-vision-protocol-v1-0-md-z1-z2-z-ind
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-07-22
---

# Phase 68 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | 없음 — 프로젝트에 xUnit/NUnit/MSTest 미설치(`packages.config` 확인, CLAUDE.md 명시). `Test/`는 Python mock TCP 클라이언트/서버 스크립트만 존재. |
| **Config file** | 없음 |
| **Quick run command** | `msbuild DatumMeasurement.sln /p:Configuration=Debug /p:Platform=x64 /t:Build` (0 errors / 0 new warnings = 최소 합격선) |
| **Full suite command** | 동일(자동화 assertion 스위트 없음) — "빌드 PASS + Human UAT" 조합이 이 프로젝트의 검증 관례(Phase 49-02-SUMMARY.md UAT 패턴) |
| **Estimated runtime** | ~30-60초 (msbuild) |

---

## Sampling Rate

- **After every task commit:** `msbuild DatumMeasurement.sln /p:Configuration=Debug /p:Platform=x64 /t:Build`
- **After every plan wave:** 전체 재빌드 + Human UAT 후보 목록 작성(Phase 49-02-SUMMARY.md `## Human UAT Candidates` 형식 재사용)
- **Before `/gsd:verify-work`:** SIMUL_MODE 수동 z_index 시퀀스 UAT 최소 1회(`DebugManualZTrigger` 또는 `Test/mock_vision_client.py`)
- **Max feedback latency:** ~60초 (빌드 기준 — 자동화 테스트 없어 사람 UAT는 latency 목표 대상 아님)

---

## Per-Task Verification Map

*Formal REQ-ID가 이 phase에 없으므로(REQUIREMENTS.md gap — Open Question, planner/사용자 확인 필요) CONTEXT.md 결정(D-01~D-09) 단위로 매핑. planner가 실제 task 분해 시 아래를 세분화할 것.*

| Task ID | Plan | Wave | Decision | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|----------|------------|-----------------|-----------|-------------------|-------------|--------|
| TBD | TBD | TBD | D-01/D-01a/D-01b (z_index 실행 스코프 + Datum 예외 + Actions[] 정렬) | V5 입력검증 | z_index 범위밖/음수 시 크래시 없이 안전 처리(ParseCurrentZIndex 기존 가드 재사용) | build+manual | `msbuild ...` | ✅ | ⬜ pending |
| TBD | TBD | TBD | D-02/D-02a/D-03 (크로스-Z 이미지 저장 + z=0 리셋) | — | Z2 미도달 시 다음 사이클 z=0 도착 시 저장 이미지 Dispose+클린 리셋 | build+manual | `msbuild ...` | ✅ | ⬜ pending |
| TBD | TBD | TBD | D-04/D-05 (ZIndexA/B 필드 + 명시적 NG) | V5 입력검증 | ZIndexA==ZIndexB 또는 존재하지 않는 index → NG(SkipReason, 로그) — 조용한 폴백 금지 | build+manual | `msbuild ...` | ✅ | ⬜ pending |
| TBD | TBD | TBD | D-06 (Datum VerticalTwoHorizontalDualImage 동일 적용) | — | Side/Bottom Datum 회귀 0 | build+manual | `msbuild ...` | ✅ | ⬜ pending |
| TBD | TBD | TBD | D-07 (기존 레시피 하위호환) | — | SHOT_E5(D:\Data\Recipe\FAI_1\main.ini) 등 기존 레시피 회귀 0 | manual | 없음(레시피 로드+검사 육안) | ❌ W0 | ⬜ pending |
| TBD | TBD | TBD | D-08 (imageA 라이브그랩 무시 버그 수정) | — | 라이브 grab 시 "파일 재로드" 로그 미발생 확인 | build+manual | `msbuild ...` | ✅ | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- 자동화 테스트 프레임워크 자체가 프로젝트에 없음 — 신규 xUnit/NUnit 도입은 이 phase 범위 밖(QUAL-01/별도 검토 대상, CLAUDE.md 명시 관례).
- `Test/mock_vision_client.py`가 z_index 파라미터를 이미 지원하는지 UAT 준비 단계에서 확인 필요 — 미지원 시 Wave 0에서 스크립트 보강 검토(자동화 테스트가 아니라 수동 UAT 보조 도구이므로 선택적).
- *"기존 인프라(msbuild + Human UAT)가 phase 요구를 충족 — 신규 Wave 0 산출물 없음."*

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|--------------------|
| z_index=N `$TEST` → 매핑 Shot만 실행(D-01) | D-01 | 자동화 테스트 프레임워크 없음, 실제 grab 횟수/로그 확인 필요 | SIMUL_MODE에서 `DebugManualZTrigger`(또는 mock TCP client)로 z=1, z=2 각각 전송 → 각 z에서 실제 grab된 Shot 로그 카운트 확인, 다른 z의 Shot이 재-grab 안 됐는지 확인 |
| z_index=0(Datum) 요청 시 전체 실행 유지(D-01a) | D-01a | 회귀 확인은 실제 시퀀스 동작 관찰 필요 | z=0 `$TEST` 전송 → 기존과 동일하게 전체 Shot 실행 + Datum 검출 정상 동작(빈 B 응답) 확인 |
| Z1→Z2 크로스-Z 캡처+계산(D-02/D-02a) | D-02 | 두 번의 순차 TCP 요청 + 최종 측정값 확인이 필요, 자동 assertion 없음 | z=ZIndexA `$TEST` 전송(이미지만 캡처됨, 항목 미보고) → z=ZIndexB `$TEST` 전송(거리 계산 완료, 항목 보고) → 측정값이 두 이미지 기준으로 올바른지 육안 확인 |
| 다음 부품 z=0 재수신 시 잔류 이미지 없음(D-03) | D-03 | 메모리 누수/오염은 정적 분석으로 확인 불가 | 첫 부품 Z1까지만 진행 후 중단 → 다음 부품 z=0 전송 → 크로스-Z 저장소가 깨끗하게 리셋됐는지(이전 부품 이미지로 오염된 측정값 안 나오는지) 확인 |
| SHOT_E5 등 기존 레시피 회귀 0(D-07) | D-07 | 실제 레시피 파일 로드 + 검사 실행 필요 | `D:\Data\Recipe\FAI_1\main.ini` 로드 → SHOT_E5 검사 실행 → 기존과 동일한 측정값/동작 확인(ZIndexA/B 미설정이므로 static 파일 경로 그대로 사용됨) |
| imageA 라이브그랩 재사용(D-08) | D-08 | 로그 부재 확인은 육안 필요 | 실HW 또는 SIMUL 라이브 grab 경로에서 DualImage 측정 실행 → "파일에서 재로드" 관련 로그가 더 이상 안 뜨는지 확인 |

---

## Gap-Closure 검증 항목(68-06~68-10)

Side(z=0,1=Datum 2위치 캡처, 측정 Shot z=2+) 배포 전 68-05 UAT 중 발견된 6개 갭(`68-GAP-ANALYSIS.md`)의
수정(68-06~68-10, `68-11` 통합 재빌드로 함께 컴파일 확인됨)에 대한 명시적 PASS/FAIL 확인 항목. 각 항목은
`68-GAP-UAT.md`(신규, 68-11 Task 2)에서 사람이 실제 SIMUL_MODE 실행으로 PASS/FAIL을 기록한다 — 이 표는
"무엇을 확인해야 하는가"의 계약이며, 실제 result는 여기서 추정하지 않는다.

| 수정 | 검증 항목 | PASS 기준 |
|------|-----------|-----------|
| FIX-0(사이클 리셋 타이밍, 68-06) | z=0에서 캡처된 role A 이미지가 z=0 자신의 응답 생성 시점 이후에도 저장소에 생존, z=1 도착 시 role B와 합쳐져 검출 성공 | z=1 응답에서 크로스-Z Datum 검출이 `bPending=true`로 영구 대기하지 않고 완료됨 |
| GAP-1(선언 z_index 유니버스, 68-07) | Side z=1(own ZIndex를 가진 Shot 없음, Datum ZIndexB=1로만 선언)과 SHOT_E5류(own ZIndex=0, 측정 ZIndexA=1)가 `ZINDEX_MISCONFIGURED` 하드 실패를 발생시키지 않음 | 두 케이스 모두 사이클마다 `ZINDEX_MISCONFIGURED` NG 없음 |
| GAP-2(datum-only 실행 스코프, 68-07) | z=1(datum-only, 일반 실행 매칭 0건) `$TEST` → `datum.SourceShotName` Shot만 DatumPhase까지 실행되고 Grab/Measure는 스킵, 무관 Shot 재-grab 없음 | InspectionListView 델타/저장이미지 타임스탬프로 무관 Shot 재-grab 0건 확인 + 매 사이클 "[V1Scope] 매칭 0건" Error 로그 0건 |
| CROSS-1(Datum transform 수명, 68-08) | z=2(측정 tick)에서 z=1 검출 transform이 무보정 identity로 폴백하지 않고 재검출되어 사용됨 | ZIndexA/B 미설정 대비 보정값 차이가 관측됨(무보정이면 값이 동일/부자연스러움) |
| CROSS-2(마지막 index 진실원, 68-09) | 크로스-Z 완성 index(예: Side z=1)가 시퀀스 최대 shot.ZIndex를 넘어도 `ComputeLastZIndex`가 이를 포함해 한 사이클 P/F가 정확히 1회 송신됨 | 사이클당 `$RESULT` P/F 정확히 1회(중복 송신 없음) |
| GAP-3(즉시-F 게이팅, 68-10) | `EnableCrossZDatumImmediateFail` 기본값 **ON**(true) — 68-10 checkpoint 결정("enable-after-agreement": `Vision-Protocol-v1.0.md` 판정(P/F/B)표의 F행 "PLC 동작"이 "NG 처리"로만 기재되어 index 번호와 무관하며, PLC는 B(다음 index 호출) vs P/F(이 부품 완료)로만 분기하고 index 숫자 자체로는 분기하지 않으므로 완성 index가 0이 아니어도 F 해석은 동일하다는 근거로 제어팀 재확인 없이 활성화 확정). ON 상태에서 크로스-Z Datum 실패가 **완성 index**(own index 0이 아니어도 됨 — 예: Side z=1)에서 즉시 F로 반영됨. `m_bImmediateFailSent` latch가 z=0 즉시-F 분기와 완성-index 재평가 분기 양쪽에 세팅되어 한 사이클 중복 F 없음(CROSS-2와 결합 확인) | z=1(완성 index)에서 Datum 실패 시 그 index에서 즉시 F 응답, 마지막 index까지 대기하지 않음 + 한 사이클 F가 정확히 1회(z=0과 z=1 양쪽에서 중복 송신 없음) |

## 운영 규칙 — 크로스-Z 측정 Shot 격리

**규칙:** 크로스-Z 측정(또는 크로스-Z Datum)은 전용 Shot에 격리하고, 그 Shot의 own `ZIndex`를 완성
index(=`max(ZIndexA, ZIndexB)`)로 설정할 것. own `ZIndex`를 0(또는 캡처 index들과 무관한 제3의 값)으로
방치하면 v1.0에서 그 Shot의 일반(비-크로스-Z) 측정이 영원히 보고되지 않거나, own/ZIndexA/ZIndexB 세
시점 모두에서 그 Shot이 실행되는 문제가 생긴다.

**혼합 Shot 오염 (GAP-2 남은 리스크, 이번 phase 코드 수정 대상 아님):** 크로스-Z 측정을 owning하는 Shot에
일반(비-크로스-Z) 측정이 함께 있으면, 그 Shot은 own ZIndex 시점 + ZIndexA 시점 + ZIndexB 시점 총 세
번 실행된다. 측정 완성-index 게이트가 `$RESULT`(와이어) 자체는 보호하므로 P/F/B 판정 오염은 없지만,
**cycle.json 스냅샷 + 저장 이미지 + 화면표시 + "Measurement failed" 에러로그가 매 사이클 오염/발생**한다
(일반 측정이 own ZIndex가 아닌 물리 Z에서 잘못 재실행되기 때문). 이 오염은 코드로 막지 않고 **운영
규칙(레시피 작성 가이드)으로 관리**한다 — 위 격리 규칙을 따르면 발생하지 않는다.

**UAT 확인 항목(68-GAP-UAT.md, 68-11 Task 2):** 혼합 Shot 레시피(크로스-Z owning Shot에 비-크로스-Z
측정을 추가)로 완성 index를 트리거 → cycle.json/저장이미지/화면에 무관 측정이 잘못된 Z 값으로 오염되는지
확인하는 항목을 UAT 시나리오에 추가한다(운영 규칙 위반 케이스의 실증 — 위 규칙을 지키면 이 오염이 없어야
함을 대조 확인).

---

## Gap-Closure 검증 항목 추가(68-12: z=0 대표 Datum 트리거 실행, 낭비 제거)

사용자가 68-05 UAT 이후 추가로 확정한 요구사항 — z_index=0(`$TEST`) 수신 시 `StartAll`로 이 시퀀스의
전체 측정 Shot Grab+Measure가 낭비 실행되고 그 결과가 전부 버려지던 문제를, TOP/BOTTOM/SIDE 전체에서
제거한다(Side 한정 아님). 아래 항목은 `68-GAP-UAT.md`(68-11 Task 2) 또는 그 후속 UAT 라운드에서 사람이
SIMUL_MODE로 실행해 실제 PASS/FAIL을 기록한다 — 이 표는 "무엇을 확인해야 하는가"의 계약이며, 이 플랜
자체(68-12, autonomous)는 검증을 생략하지 않고 이 표를 등록하는 것으로 위임한다.

| 수정 | 검증 항목 | PASS 기준 |
|------|-----------|-----------|
| Plan 12(z=0 대표 트리거 실행) | z=0 `$TEST` 수신 시 대표 Datum 트리거 Action(들) 외 다른 측정 Shot의 grab 카운트/타임스탬프 변화가 없어야 함 | InspectionListView 델타/저장이미지 타임스탬프로 대표 트리거 Shot 외 다른 Shot의 grab 0건 확인(TOP/BOTTOM/SIDE 각각) |
| Plan 12(Datum 검출 유지) | Datum 강제 실패 시나리오(예: 티칭 이미지 제거/조명 오프)에서 z=0 응답이 이 플랜 적용 전과 동일 타이밍으로 즉시 F | `BuildDatumShotResponse`의 기존 즉시-F 분기가 byte-identical하게 그대로 발동(응답 지연/누락 없음) |
| Plan 12(측정값 불변) | 동일 레시피/동일 이미지로 재실행 시 각 측정 Shot의 own z_index(또는 크로스-Z 완성 index)에서 산출되는 측정값/P·F·B 판정이 이 플랜 적용 전/후 완전히 동일 | 재실행 결과값 diff 0 — 완성 index 계산/측정값 산출 경로 무변경 확인 |
| Plan 12(빈 DatumConfigs 폴백) | DatumConfigs가 비어있는(또는 대표 트리거가 하나도 해석 안 되는) 시퀀스는 z=0에서 기존과 동일하게 StartAll로 안전 폴백 | z=0에서 이 시퀀스의 모든 Action이 정상 실행됨(실행 범위 회귀 0), Trace 로그("DatumConfigs 비어있음... StartAll 폴백")만 남고 Error 로그 없음 |

**참고:** 이 변경으로 z=0에서의 혼합 Shot 오염 기여분(위 "운영 규칙 — 크로스-Z 측정 Shot 격리" 절이 설명하는
위험의 z=0 쪽 원인)도 함께 사라진다 — z=0에서는 이제 대표 트리거 Action(들) 외 어떤 측정 Shot의 Grab/Measure도
실행되지 않으므로, 혼합 Shot 오염은 크로스-Z 측정의 own ZIndexA/B tick에서만 발생 가능하다(GAP-2 남은 리스크는
무변경 유지, z=0 기여분만 제거됨).

이 표의 실제 result(PASS/FAIL)는 `68-GAP-UAT.md`(68-11 Task 2) 또는 그 후속 UAT 라운드에서 사람이
SIMUL_MODE로 실행해 기록한다 — 이 플랜 자체는 autonomous(체크포인트 없음)이지만 검증을 생략하지 않는다.

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify(msbuild) or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify(빌드는 매 task 가능하므로 자동 충족 예상)
- [ ] Wave 0 covers all MISSING references(해당 없음 — 신규 Wave 0 산출물 없음)
- [ ] No watch-mode flags
- [ ] Feedback latency < 60s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
