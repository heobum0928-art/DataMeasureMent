# Phase 9: VERIFICATION 문서 보강 - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-04-23
**Phase:** 09-verification-backfill
**Areas discussed:** Plan split, UAT sign-off location, Phase 6 scope, Phase 3 overlay 반영, Wave 구성, Other specs

---

## Plan Split

| Option | Description | Selected |
|--------|-------------|----------|
| 5개 plan (권장) | 09-01(01-V), 09-02(03-V), 09-03(06-V), 09-04(02-UAT), 09-05(05-UAT). 독립 병렬 가능, 감사 gap별 트레이서빌리티 명확 | ✓ |
| 3개 plan (VERIFICATION 묶기) | 09-01(01/03/06 VERIFICATION 3건), 09-02(02-UAT), 09-03(05-UAT) | |
| 2개 plan (종류별 묶기) | 09-01(VERIFICATION 3건), 09-02(UAT 사인오프 2건) | |
| 1개 plan (단일 bulk) | Phase 8과 같은 방식 | |

**User's choice:** 5개 plan (권장)
**Notes:** 독립 산출물 = 병렬 실행 최적화 + 감사 gap과 plan 1:1 매핑.

---

## UAT Sign-off Location

| Option | Description | Selected |
|--------|-------------|----------|
| 기존 UAT 파일에 result 채움 (권장) | 02-HUMAN-UAT.md 업데이트 + 05-HUMAN-UAT.md 신설. 기존 artifact에 직접 반영 | ✓ |
| VERIFICATION.md frontmatter의 human_verification 업데이트 | UAT 파일은 그대로 두고 VERIFICATION.md frontmatter에 사인오프 추가 | |
| 별도 UAT-SIGNOFF.md 신설 | 02-UAT-SIGNOFF.md, 05-UAT-SIGNOFF.md 새 파일 2건 | |

**User's choice:** 기존 UAT 파일에 result 채움 (권장)
**Notes:** 추적성 유지 + 파일 수 최소화. 05는 기존 파일 없으므로 02 포맷 따라 신설.

---

## Phase 6 VERIFICATION Scope

| Option | Description | Selected |
|--------|-------------|----------|
| RC-01..RC-06 + quick 260417-kzd + Phase 7 regression 복구 (권장) | 세 가지 통합. Gap I1 해소 같이 레퍼런스 | ✓ |
| RC-01..RC-06 + quick UAT만 | Phase 7 regression은 07-02-SUMMARY.md 링크만 | |
| RC-01..RC-06만 (최소) | quick UAT는 Deferred Ideas로 등록 | |

**User's choice:** RC-01..RC-06 + quick 260417-kzd + Phase 7 regression 복구 (권장)
**Notes:** Phase 6 전체 라이프사이클을 한 곳에서 추적 가능.

---

## Phase 3 ALG-04 Overlay 반영

| Option | Description | Selected |
|--------|-------------|----------|
| ALG-04를 'satisfied (via Phase 7)'로 기록 (권장) | Evidence 열에 복구 타임라인 명시 | ✓ |
| ALG-04를 'satisfied' 하되 히스토리는 각주로 | 하단 Notes 섹션에 기록 | |
| ALG-04를 Phase 3 범위에서 제외 | Phase 7로 이관 | |

**User's choice:** ALG-04를 'satisfied (via Phase 7)'로 기록 (권장)
**Notes:** 복구 경로 추적성 확보.

---

## Wave Configuration

| Option | Description | Selected |
|--------|-------------|----------|
| Wave 1 단일 (모든 plan 병렬, 권장) | 5개 plan 각기 다른 파일 생성 = 충돌 없음 | ✓ |
| Wave 1 = VERIFICATION 3건, Wave 2 = UAT 2건 | 논리 순서는 맞지만 실제 의존성 없음 | |
| Wave 1 = 전체 순차 | 병렬화 없음 | |

**User's choice:** Wave 1 단일 (모든 plan 병렬, 권장)
**Notes:** worktree parallelization 활용, 실행 속도 최적화.

---

## Other Specs

| Option | Description | Selected |
|--------|-------------|----------|
| 없음 - 기존 Phase 2/5 VERIFICATION.md 포맷을 그대로 복제 | 02/05 포맷 재사용 + 코드 증거는 grep/Read로 확보 | ✓ |
| v1.0-MILESTONE-AUDIT.md 우선 참조 | 감사 기준으로 gap 해소 명시 | |
| 기타 (직접 입력) | — | |

**User's choice:** 없음 - 기존 Phase 2/5 VERIFICATION.md 포맷을 그대로 복제
**Notes:** v1.0-MILESTONE-AUDIT.md는 canonical_refs에 포함됨. 포맷은 02/05 파일 기준.

---

## Claude's Discretion

- 각 VERIFICATION.md의 Observable Truth 개수 / Key Link 개수 — 플랜 시점에 구체화
- Phase 1 Human Verification 섹션 필요 여부 — researcher가 scout 후 결정
- 커밋 메시지 포맷 — Phase 8과 동일한 `docs(09-0X): ...`

## Deferred Ideas

- tech_debt 해소 (WR-01/03/05) → Phase 10
- Phase 6 Runtime lighting 연결 → backlog/v2
- TestResultPacket multi-Measurement → v2
- Nyquist compliance 전면화 → 별도 phase
- Phase 6 06-VALIDATION.md draft deprecate 처리 → 별도 cleanup phase
