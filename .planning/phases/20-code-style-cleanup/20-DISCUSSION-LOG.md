# Phase 20: 코드 스타일 정리 — Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-05-08
**Phase:** 20-code-style-cleanup
**Areas discussed:** 연산자 정책, 변환 파일 스코프, 주석 정리 기준, 회귀 검증 방법

---

## 연산자 정책

### Q1. ROADMAP/QUAL-02 vs CONVENTIONS.md §2 충돌 해결

| Option | Description | Selected |
|--------|-------------|----------|
| ROADMAP 엄격 해석 (모두 if/else) | `?:`/`??`/`?.` 세 종류 모두 명시적 if/else 또는 null 체크 분해. CONVENTIONS.md §2 허용 규칙 폐기. v1.1 소스 일관성 우선. | ✓ |
| CONVENTIONS.md 절충안 | `??` 단독 + 1-depth `?:` 허용, `??·?:` 혼용·중첩·`?.` 만 변환. 한 줄 보그더 가독성 유지. | |
| 다이제스트 수정 결정 | CONVENTIONS.md §2 를 v1.1 관점으로 재기동 (CODE-RULES.md 신규 작성). | |

**User's choice:** ROADMAP 엄격 해석.
**Notes:** v1.1 소스 일관성을 위해 가장 엄격한 정책 채택. CONVENTIONS.md §2 는 Phase 20 한정으로 ROADMAP 우선.

### Q2. `event?.Invoke(this, e)` 패턴 처리

| Option | Description | Selected |
|--------|-------------|----------|
| 명시 null 체크 후 Invoke (var handler) | `var handler = MyEvent; if (handler != null) handler(this, e);` 멀티스레드 경주 안전. | ✓ |
| 직접 null 체크 (`if (event != null) event(this, e)`) | 임시변수 없이 — 멀티스레드 경주 공간 존재. | |
| `?.` 예외 — event 호출만 허용 | event invocation 은 `?.` 유지. | |

**User's choice:** 임시변수 패턴.
**Notes:** 멀티스레드 안전 우선.

### Q3. `obj?.Field ?? defaultValue` + 채이닝 분해

| Option | Description | Selected |
|--------|-------------|----------|
| 일차 if/else 분해 + 임시변수 | `var v = defaultValue; if (obj != null) v = obj.Field;` 명확하고 읽기 쉬움. | ✓ |
| TryGet 계열 도입 | `if (TryGetField(obj, out var v))` — 신규 helper 양산. | |
| 조기 반환 | `if (obj == null) return defaultValue; var v = obj.Field;` — 메서드 쪼개지만 일부 케이스만 가능. | |

**User's choice:** 일차 if/else 분해 + 임시변수.

### Q4. LINQ chain `?.` + expression-bodied `=> _field`

| Option | Description | Selected |
|--------|-------------|----------|
| 둘 다 유지 | LINQ chain 끝 `?.` 와 expr-bodied member 모두 Phase 20 변환 대상 아님. CONTEXT.md 예외 명문화. | ✓ |
| LINQ 유지 / expr-body 변환 | expr-bodied auto-property 는 `{ get { return _field; } }` 로 전환. | |
| 둘 다 변환 | ROADMAP 엄격 해석 관점 — LINQ 결과도 임시변수, expr-body 도 명시 분해. | |

**User's choice:** 둘 다 유지 (가독성·직관성).
**Notes:** "쉽게설" 모호 답변 → "추천은?" 후속 → "가독성 및 직관적인 코드가 좋" 으로 lock.

---

## 변환 파일 스코프

### Q5. "변경된 또는 새 파일" 정의 — 변환 대상 범위

| Option | Description | Selected |
|--------|-------------|----------|
| A: v1.1 hbk 마커 14 파일 | 트래킹 명확. 변환 포인트 ~148. AC #3 회귀 검증 현실적. | ✓ |
| B: v1.1 milestone git diff 전체 | 자연스러운 정의. 실제 A 와 거의 동일 (현 시점). | |
| C: 핵심 모델/서비스 ~25 파일 | v1.1 일관 + 회귀 부담 출. | |
| D: 코드베이스 전체 (158 파일, ~1100 변환) | Phase 26 와 중복/충돌. 일정 파괴. | |

**User's choice:** A — 14 파일.
**Notes:** "문제가 되는점 얘기해봐" → 트레이드오프 설명 후 lock.

### Q6. 14 파일 내 처리 깊이

| Option | Description | Selected |
|--------|-------------|----------|
| 파일 전체 정리 | 선택된 14 파일 내 hbk 마커 없는 구 코드 라인도 변환. 파일 단위 일관성. | ✓ |
| v1.1 hbk 라인만 | 더 좁은 범위, 파일 안 혼란 우려. | |

**User's choice:** 파일 전체 정리.

### Q7. 이전 hbk 마커 스택 vs 교체

| Option | Description | Selected |
|--------|-------------|----------|
| 이전 hbk 유지 + Phase 20 추가 (스택) | 변경 이력 보존. | |
| Phase 20 마커로 교체 | 노이즈 감축, git log 위임. | ✓ |
| Phase 20 마커 생략 | memory rule 위반. | |

**User's choice:** Phase 20 마커로 교체 (스택 X).
**Notes:** "주석이 너무 많아서 줄이고 싶은게 목표야" — 사용자 명시 목표.

---

## 주석 정리 기준

### Q8. "what" 주석 제거 임계

| Option | Description | Selected |
|--------|-------------|----------|
| 코드로 다 드러나는 내용 제거 | 메서드/변수명으로 명확한 것 제거. 알고리즘 의도, '왜 이 수치' 보존. | ✓ |
| 설명 주석 모두 제거 (region/XML doc 만) | 극단적 축약 — 산업 도메인 의도 손실. | |
| "why" 좀 느슨하게 유지 | 애매하면 유지. 노이즈 감축 적음. | |

**User's choice:** 코드로 다 드러나는 내용 제거.

### Q9. hbk 마커 주석 자체 정리

| Option | Description | Selected |
|--------|-------------|----------|
| Phase 20 변환 라인만 교체 + 이외 라인 현 상태 유지 | 안전, scope 명확. | ✓ |
| 14 파일 전체 hbk 마커 과감 제거 | 일괄 압축. 마이그레이션 리스크. | |
| 모든 이월 hbk 마커 제거 | git blame 100% 의존. memory rule 충돌. | |

**User's choice:** Phase 20 변환 라인만 교체.

### Q10. XML doc + `#region` 처리

| Option | Description | Selected |
|--------|-------------|----------|
| 둘 다 유지 | CONVENTIONS.md 일관, IDE intellisense 보존. | ✓ |
| XML doc 유지 / region 제거 | region 일부만 제거. | |
| XML doc 제거 / region 유지 | what 주석 관점 — 일부 IDE 손실. | |

**User's choice:** 둘 다 유지.

### Q11. 정리 자동화 vs 수동

| Option | Description | Selected |
|--------|-------------|----------|
| 수동 리뷰 (파일별 발수 점검) | 안전, 한국어 주석 nuance 최적화. | ✓ |
| 자동 패턴 그러스트 후 수동 검증 | grep 제거 제안 → 사용자 결정. | |
| AI 리뷰에 위임 | `/gsd-code-review` 한 번에. nuance 손실 우려. | |

**User's choice:** 수동 리뷰.

---

## 회귀 검증 방법 (AC #3)

### Q12. "로직 차이 0" 입증 방법

| Option | Description | Selected |
|--------|-------------|----------|
| msbuild PASS + Datum 1회 + FAI 1회 결과 비교 | 현실적, Phase 28 패턴 동일. | ✓ |
| 50 회 반복 동등성 (Phase 25 모듈 도입 이전) | 확실하지만 수동 너부쇄. | |
| msbuild PASS + warning 0 만 | 입증 부족. | |

**User's choice:** Datum 1회 + FAI 1회 결과 비교.

### Q13. SIMUL_MODE 회귀 레시피

| Option | Description | Selected |
|--------|-------------|----------|
| Phase 28 SIMUL UAT 레시피 재사용 | 이미 검증된 고정점. 14 파일 변환 대부분 커버. | ✓ |
| 테스트용 신규 작성 | 재현 구축 부담. | |
| v1.0 마이그레이션 레시피 | 존재 여부 불명. | |

**User's choice:** Phase 28 SIMUL UAT 레시피 재사용.

### Q14. 비교 임계

| Option | Description | Selected |
|--------|-------------|----------|
| byte-identical (1e-9 mm) | `?:` → if/else 는 의미 동등 → 차이 = 버그. | ✓ |
| 1e-6 mm | 부동소수 안전 마진. | |
| 0.001 mm (Phase 28 수준) | 너무 느슨. | |

**User's choice:** byte-identical (1e-9 mm).

### Q15. msbuild warning 임계

| Option | Description | Selected |
|--------|-------------|----------|
| 신규 warning 0 | 기존 warning 보존, 변환이 새 warning 도입 금지. | ✓ |
| warning 완전 0 | 기존 warning 도 수정 — 범위 출. | |

**User's choice:** 신규 warning 0.

---

## Claude's Discretion

- 14 파일 plan wave 분할 (의존 그룹 vs 5/5/4) — 플래너 결정.
- "what" 판정 경계 케이스 — 플래너/실행자 case-by-case.
- `?.` 분해 시 임시변수 명명 — 플래너 결정.

## Deferred Ideas

- Phase 26 헝가리안 시 나머지 ~144 파일 `?:`/`??`/`?.` 변환 흡수 여부.
- Expression-bodied member 변환 정책 (Phase 26 또는 별도).
- CONVENTIONS.md → CODE-RULES.md 이관 (Phase 26).
- v1.1 후속 phase 21~27 의 새 변경 라인 정책 적용 (CONVENTIONS.md/메모리 권고).
- 50 회 반복 GR&R 회귀 (Phase 25 OUT-03 도입 후 보강).

---

*Generated: 2026-05-08*
