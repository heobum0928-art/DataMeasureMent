# Phase 35: Side/Bottom 실측 UAT + Phase 33 마이그레이션 보강 - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-05-26
**Phase:** 35-side-bottom-uat-and-phase33-completion
**Areas discussed:** A. OwnerSequenceId 아키텍처 타입, B. INI 하위호환 폴백 전략, C. 이미지 갱신 회귀 수정 범위, D. SIMUL UAT 범위 및 순서

---

## A. OwnerSequenceId 아키텍처 타입

| Option | Description | Selected |
|--------|-------------|----------|
| string `OwnerSequenceName` ("TOP"/"SIDE"/"BOTTOM") | ParamBase reflection 자동 직렬화. DatumConfig.AlgorithmType 패턴 재사용. INI 가독성. | ✓ |
| ESequence enum 직접 저장 | 타입 안전성 최강. 단점: ParamBase 자동 직렬화 미지원 → 수동 case 추가. | |
| int `OwnerSequenceID` (ESequence cast) | ParamBase Int32 자동 지원. 단점: enum 값 변경 시 INI 깨짐, 가독성 낮음. | |

**User's choice:** string `OwnerSequenceName` (Recommended)
**Notes:** Phase 12-01 D-04 선례 (DatumConfig.AlgorithmType 가 enum 대신 string 채택) 와 동일 패턴.

---

## B. INI 하위호환 폴백 전략

| Option | Description | Selected |
|--------|-------------|----------|
| Top 자동 폴백 | OwnerSequenceName == "" 이면 SEQ_TOP 할당. 회귀 0. | ✓ (Claude's Discretion) |
| Phase 6 reject (경고 다이얼로그) | 사용자가 인지 가능하나 UX 방해 | |
| 수동 마이그레이션 (Phase 33 D-04 방식) | 자동 변환 불가 명시 | |

**User's choice:** [No preference] → Claude's Discretion
**Notes:** Phase 33 이전 모든 Shots 는 Top 소속이므로 자동 폴백이 회귀 0 + UX 부담 0. EnsurePerRoiDefaults 패턴 재사용.

---

## C. 이미지 갱신 회귀 수정 범위

| Option | Description | Selected |
|--------|-------------|----------|
| 광범위 수정 — 캐시 로직 리팩터링 | LoadImage 두 오버로드 일관성 + 캐시 무효화 명시 + 노드 이벤트 재로드 강제. 근본 해결. | ✓ |
| 최소 수정 — 특정 증상 대응 | Datum Load 후 canvas 갱신만. Phase 32-06 fix 연장. | |
| ForceRefresh API 추가 | 우회로서 명시 호출. 숨겨진 상태 증가 위험. | |

**User's choice:** 광범위 수정 (Recommended)
**Notes:** Phase 32-06 fix (4ea5bcc/9c482dd) 의 cover 못한 시나리오 (Datum 노드 Load 후 ROI 티칭 시 stale canvas) 가 다시 발견되어 근본 해결 채택.

---

## D. SIMUL UAT 범위 및 순서

### D-1 동작 순서
| Option | Description | Selected |
|--------|-------------|----------|
| 아키텍처 먼저 → UAT 나중 | 1) OwnerSequenceId → Bottom Shot 재로드 검증 → 2) 이미지 hotfix → Datum 재검증 → 3) 통합 UAT. 구조 안정 후 UAT. | ✓ |
| UAT 먼저 → 증상별 fix | 자주 실행하며 증상마다 fix. 단점: 아키텍처 갈등 트러블슈팅 어려움. | |
| 병렬 처리 — 단일 wave | 속도 빠르나 atomic commit 원칙 위반. | |

**User's choice:** 아키텍처 먼저 → UAT 나중 (Recommended)

### D-2 Phase 33 4 미수행 테스트 처리
| Option | Description | Selected |
|--------|-------------|----------|
| Phase 35 에서 재수행 | Test 2/3/4/5 모두 Phase 35 sign-off 조건. Phase 35 완료 시 Phase 33 도 retro 완전 sign-off. | ✓ |
| Phase 33 partial 유지 | Phase 35 는 OwnerSequenceId + 이미지 hotfix 만. Side/Bottom Datum 검증 갭 잔존 위험. | |

**User's choice:** Phase 35 에서 재수행 (Recommended)
**Notes:** Phase 33 의 33-UAT.md frontmatter `status: partial` → `signed_off` 로 retro 업데이트 — Phase 35 sign-off 시점에 일괄 처리.

---

## Claude's Discretion

- B. INI 하위호환 폴백 전략 (사용자 [No preference] → Top 자동 폴백 채택)
- Plan 분할 단위 (~4 plans 예상)
- 헬퍼 메서드 위치
- 트리 재구축 분기 알고리즘 (RecipeManager 시퀀스별 컬렉션 vs UI 필터링)
- 이미지 캐시 무효화 트리거 (path 비교 / hash / explicit dispose 추적)

## Deferred Ideas

- CO-33-05 (Datum 듀얼 티칭 이미지) → 이미 Phase 34 분리
- CO-33-01 (Bottom Multi-Die 자동 매핑) → v1.2 이연
- Phase 24 검사 워크플로우 end-to-end → Phase 35 sign-off 후 후속
