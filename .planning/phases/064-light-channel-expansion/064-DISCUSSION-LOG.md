# Phase 64: 조명 채널 확장 + z_index 기반 내부 조명 제어 - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-06-25
**Phase:** 64-light-channel-expansion
**Areas discussed:** 제어 흐름 방식, $PREP ACK 여부, Ring 분할 제어, $LIGHT 폐기 정책, CoaxLight 재활용

---

## 제어 흐름 방식 (A vs B)

| Option | Description | Selected |
|--------|-------------|----------|
| A — $PREP 별도 커맨드 선행 | $PREP TCP 전송 → ACK → $TEST 트리거 | ✓ |
| B — $TEST 수신 후 내부 처리 | $TEST 수신 시 z_index 꺼내 내부 조명 세팅 | |

**User's choice:** A — $PREP 선행 커맨드
**Notes:** 미래 HW 트리거 전환 시 $TEST를 HW 펄스로 교체해도 $PREP 로직 재사용 가능. 조명 세팅 + 안정화 시간을 로봇 이동 시간에 숨길 수 있다는 타이밍 장점.

---

## $PREP ACK 여부

| Option | Description | Selected |
|--------|-------------|----------|
| ACK 있음 | $PREP_ACK:site,z_index,OK/FAIL@ 응답 | ✓ |
| ACK 없음 | $PREP 보내고 일정 시간 후 트리거 (타이밍 의존) | |

**User's choice:** ACK 있음
**Notes:** 핸들러가 조명 세팅 완료를 확인 후 트리거 → 안전한 방식.

---

## Ring 6채널 분할 제어 여부

| Option | Description | Selected |
|--------|-------------|----------|
| 통합 1그룹 (RING) | 6채널 동시 동일 레벨 | ✓ |
| 6분할 독립 (RING_CH1~6) | 방향별 다른 레벨 | |
| 배선 확인 후 결정 | 광학부서 대기 | |

**User's choice:** 물리 채널 6개 등록 + RING 통합 그룹으로 동시 제어
**Notes:** 8채널 컨트롤러 2대 구조 확인. 개별 채널 레벨 제어는 향후 phase.

---

## $LIGHT 커맨드 폐기 정책

| Option | Description | Selected |
|--------|-------------|----------|
| 그냥 놔두고 $PREP만 추가 | $LIGHT 코드 무변경 | ✓ |
| UseProtocolV1일 때 $LIGHT 무시 | 핸들러 소프트웨어도 동시 수정 필요 | |

**User's choice:** 그냥 놔두고 $PREP만 추가
**Notes:** 회귀 위험 0. $LIGHT 폐기는 UseProtocolV1 전환 완료 후 별도 phase.

---

## CoaxLight_* 재활용 여부

| Option | Description | Selected |
|--------|-------------|----------|
| 기존 CoaxLight_* → ALIGN_COAX 채널 연결 | INI 키 이름 보존, 기존 레시피 호환 | ✓ |
| 신규 AlignCoax_* 속성 추가 | 기존 레시피 재설정 필요 | |

**User's choice:** 기존 CoaxLight_* 이름 그대로 → ALIGN_COAX 채널 연결
**Notes:** 기존 레시피 파일 재설정 불필요.

---

## Deferred Ideas

- Ring 6채널 개별 레벨 제어 — 향후 phase
- $LIGHT 커맨드 폐기 — UseProtocolV1 전환 완료 후
- HW 트리거 전환 — $PREP 구조 확정 후
