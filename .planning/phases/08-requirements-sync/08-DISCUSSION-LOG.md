# Phase 8: 요구사항 & 트레이서빌리티 동기화 - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-04-23
**Phase:** 08-requirements-sync
**Areas discussed:** RC 정의 본문 형식, RC 체크박스/Status 값, Phase 2/3/5 Status 정합화 기준, 섹션 배치/Coverage 표기

---

## Area selection

| Option | Description | Selected |
|--------|-------------|----------|
| RC 정의 본문 형식 | 한 줄 요약 vs + Acceptance Criteria vs + Gap 메타 | ✓ |
| RC 체크박스/Status 값 | Phase 6 구현 완료, VERIFICATION은 Phase 9 예정 | ✓ |
| Phase 2/3/5 Status 정합화 기준 | 감사 Gap G2 stale traceability 해소 | ✓ |
| 섹션 배치/Coverage 표기 | 독립 섹션 위치 및 Coverage 상세도 | ✓ |

**User's choice:** 네 영역 전부 선택.

---

## RC 정의 본문 형식

| Option | Description | Selected |
|--------|-------------|----------|
| 한 줄 요약 | Phase 6 RESEARCH.md L90-95 한 줄 요약, 기존 UI/TCH/ALG/SEQ와 동일 스타일 | ✓ |
| 요약 + Acceptance Criteria 한 줄 | 정의 + 검증 포인트 한 줄, 문서 길이 ~6줄 증가 | |
| 요약 + Gap Closure 메타 | 정의 + 감사 Gap ID 부착 | |

**User's choice:** 한 줄 요약.
**Notes:** 기존 문서 톤 유지 — 경량성 우선. Acceptance criteria는 Phase 9 VERIFICATION.md에서 별도 다룸(D-01, deferred).

---

## RC 체크박스/Status 값

| Option | Description | Selected |
|--------|-------------|----------|
| [x] + 'Complete' | Phase 1/3 패턴과 일치 (VERIFICATION 없이도 Complete) | ✓ |
| [x] + 'Implemented (verif. pending P9)' | 2단계 절차, 감사 투명성 ↑ 문서 노이즈 ↑ | |
| [ ] + 'Pending (P9 verify)' | 가장 보수적, Phase 9 후 갱신 | |

**User's choice:** [x] + 'Complete'.
**Notes:** 기존 Phase 1/3와 일관성 유지. Phase 9 VERIFICATION이 Status를 되돌릴 필요 없음(D-02).

---

## Phase 2 (TCH-01/TCH-02/ALG-03) 정합화

| Option | Description | Selected |
|--------|-------------|----------|
| 전부 Complete 유지 | Phase 2 UAT 버그는 별도 토도/quick 경로, 트레이서빌리티 오염 방지 | ✓ |
| Partial (TCH-01/02 Complete, ALG-03 Pending) | ALG-03 캘리브레이션만 Pending 명시 | |
| 전부 Pending 유지 | 감사 human_needed 상태, Phase 9 UAT 사인오프 후 변경 | |

**User's choice:** 전부 Complete 유지.
**Notes:** Phase 2 UAT FAI 저장 버그는 quick 토도/debug 세션으로 이관. 요구사항 트레이서빌리티에는 반영 안 함(D-03).

---

## Phase 5 (SEQ-01..04) 및 ALG-04 (Phase 3→Phase 7) Status

| Option | Description | Selected |
|--------|-------------|----------|
| SEQ 전부 Complete + ALG-04 Complete | Phase 5 Shot-FAI 데이터 모델 완성 + Phase 7 UAT 사인오프(2026-04-23) | ✓ |
| SEQ Complete + ALG-04 Pending | ALG-04는 Phase 9 VERIFICATION 전까지 Pending | |
| SEQ는 SEQ-04만 Pending 유지 | TCP 전송 미검증시 보수적 | |

**User's choice:** SEQ 전부 Complete + ALG-04 Complete.
**Notes:** Phase 5 데이터 모델 + Action_FAIMeasurement 루프 완성으로 SEQ 요구사항 충족. ALG-04는 오늘 Phase 7 Plan 02 UAT 통과(D-04, D-05).

---

## "Rapid City 확장" 섹션 배치

| Option | Description | Selected |
|--------|-------------|----------|
| '검사 시퀀스' 다음 | 독립 섹션, Phase 6 구조 확장 성격 반영 | ✓ |
| v1 Requirements 마지막 하위 섹션 | 기존 속성별 그룹핑 유지 | |
| 각 기존 섹션 그룹별 분산 | RC별로 UI/시퀀스/알고리즘으로 분산 | |

**User's choice:** '검사 시퀀스' 다음.
**Notes:** v1 Requirements 그룹 안에 독립 섹션으로 — 'Rapid City 확장 = Phase 6 = 구조 확장' 인식 유지(D-06).

---

## Coverage 카운트 표기

| Option | Description | Selected |
|--------|-------------|----------|
| 단순 총합 + 기존 ID 나열 | 기존 포맷 그대로 | ✓ |
| + Phase별 breakdown 테이블 | 감사 편의성 ↑ 문서 경량성 ↓ | |
| + Status 요약 (Complete/Pending 개수) | 동기화 상태 한 줄 명시 | |

**User's choice:** 단순 총합 + 기존 ID 나열.
**Notes:** 기존 L89-93 포맷 유지. Phase별 breakdown은 deferred(D-07).

---

## Claude's Discretion

- RC-01..RC-06 한 줄 정의의 정확한 한국어 표현(RESEARCH.md 문장 직접 복사 vs 경량 리라이트).
- 섹션 제목 정확 표기("Rapid City 확장" / "Rapid City 확장 (Phase 6)").
- Traceability 표 RC 행 Phase 열 "Phase 6 (등록은 Phase 8)" → "Phase 6" 단순화 여부.

## Deferred Ideas

- Phase별 Status breakdown 테이블.
- RC 정의에 Gap ID 메타 부착.
- RC 정의에 Acceptance Criteria 줄 추가.
- Phase 2 FAI 저장 버그 trace — 별도 토도/debug 경로.
