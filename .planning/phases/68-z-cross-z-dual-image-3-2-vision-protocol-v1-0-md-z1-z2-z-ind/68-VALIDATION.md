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

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify(msbuild) or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify(빌드는 매 task 가능하므로 자동 충족 예상)
- [ ] Wave 0 covers all MISSING references(해당 없음 — 신규 Wave 0 산출물 없음)
- [ ] No watch-mode flags
- [ ] Feedback latency < 60s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending
