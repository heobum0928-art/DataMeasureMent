# Phase 70: 종합판정 시퀀스 소유권 스코프 조사 - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-08-05
**Phase:** 70-judgement-scope
**Areas discussed:** 버그 판정, 수정 범위, 적용 방식, 검증 방법, 다음 단계

---

## 버그 판정

| Option | Description | Selected |
|--------|-------------|----------|
| 버그로 확정 | 실행은 항상 단일 시퀀스인데 판정만 전역이라 불일치. 3곳 모두 수정 대상으로 진행 | ✓ |
| 추가 실측 증거 필요 | 실제 v2.6 핸들러 통신에서 항상 3개 사이트를 다 돌린 뒤에만 결과를 읽는지 확인 필요 | |

**사용자의 선택:** 버그로 확정 (권장)
**Notes:** 근거로 제시한 것 — (1) TCP v1.0(`ProcessTest`→`StartV1Scoped`), TCP v2.6(`SequenceHandler.Start(TestPacket)`), 수동 UI, 배치 UI 전부 `packet.Identifier`/UI 선택 기준 단일 시퀀스만 트리거하는 코드, 3개를 동시에 묶어 실행하는 경로 없음. (2) 이 판정 계층을 처음 만든 Phase 39 CONTEXT.md D-03이 "정책을 Top/Side/Bottom에 통일 적용"이라는 뜻이지 "shot을 합쳐서 판정"이라는 뜻이 아니었음.

---

## 수정 범위

| Option | Description | Selected |
|--------|-------------|----------|
| 3곳 모두 | ComputeOverallResult + BatchRunService.HandleFinish + AddResponse() v2.6 레거시 경로 전부 동일 필터 적용 | ✓ |
| 화면 UI 2곳만 먼저 | PLC 통신 경로(AddResponse())는 블래스트 반경이 커서 보류, 화면용 2곳만 우선 수정 | |

**사용자의 선택:** 3곳 다 고침 (권장)
**Notes:** 처음 "뭘 수정하는거야" / "잘 이해가 안가는데"로 명확화 요청 → 구체적 시나리오(카메라 3개, Top만 RUN 눌렀을 때 Side/Bottom 미측정 결과가 섞여 오판정)로 재설명 후 재질의. AddResponse()가 실제 PLC/핸들러와 TCP로 연결된 생산 경로임을 명시한 뒤 포함 결정.

---

## 적용 방식

| Option | Description | Selected |
|--------|-------------|----------|
| 바로 적용 | 다른 필터링된 함수들과 동일하게 게이팅 없이 즉시 동작 변경 | ✓ |
| 설정으로 켜고끄기 | 문제 생기면 기존 방식으로 되돌릴 수 있게 플래그 추가 | |

**사용자의 선택:** 바로 적용 (권장)
**Notes:** 기존 `ComputeLastZIndex`/`AggregateIndexFais` 등도 게이팅 없이 직접 적용되어 있어 일관성 우선.

---

## 검증 방법

| Option | Description | Selected |
|--------|-------------|----------|
| SIMUL 재현 테스트 | 다중 시퀀스 레시피로 Top만 RUN → 수정 전 오판정 재현 → 수정 후 정상 판정 확인 | ✓ |
| 코드 검토만으로 충분 | 이미 검증된 패턴 복사이므로 빌드 통과로 충분 | |

**사용자의 선택:** SIMUL 재현 테스트 (권장)
**Notes:** 세션 중 사용자가 추가로 "에이전트 검토도 꼭 확인해"라고 명시 → SIMUL 재현 테스트에 더해 `/gsd-code-review` 필수 포함으로 D-05에 반영.

---

## 다음 단계

| Option | Description | Selected |
|--------|-------------|----------|
| /gsd:quick로 바로 진행 | 규모가 작아(기존 패턴 3곳 복사 수준) 정식 계획 단계 생략 | ✓ |
| /gsd:plan-phase로 정식 계획 수립 | 변경 범위가 이보다 크다고 판단될 때 | |

**사용자의 선택:** /gsd:quick로 바로 진행 (권장)

---

## Claude's Discretion

- 3곳 각각의 정확한 필터 삽입 위치/변수명
- SIMUL 재현 테스트에 사용할 구체적 레시피/시나리오 데이터

## Deferred Ideas

없음 — 논의가 phase 범위 안에서 유지됨.
