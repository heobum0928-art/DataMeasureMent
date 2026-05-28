# Phase 38: v1.1 Carry-over Cleanup 일괄 - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-05-28
**Phase:** 38-v1-1-carryover-cleanup
**Areas discussed:** #1 측정 타입 정리, #12·#3 중복 필드 정리, #5 픽셀분해능 단일화, #10·#11 정리 범위

---

## Area 선택 (multiSelect)

| Option | Selected |
|--------|----------|
| #1 측정 타입 정리 | ✓ |
| #12·#3 중복 필드 정리 | ✓ |
| #5 픽셀분해능 단일화 | ✓ |
| #10·#11 정리 범위 | ✓ |

#6 각도 파라미터 UI 는 todo(2026-05-28)에서 이미 확정되어 논의 제외.

---

## #1 측정 타입 정리

**Q1 제거 방식**

| Option | Selected |
|--------|----------|
| UI ComboBox에서만 숨김 (권장) | ✓ |
| Factory에서 완전 삭제 | |
| deprecated 주석만 | |

**Q2 숨길 미사용 타입 (multiSelect)**

| Option | Selected |
|--------|----------|
| EdgePairDistance | ✓ |
| PointToLineDistance / PointToPointDistance | ✓ |
| LineToLineAngle / LineToLineDistance | ✓ |
| EdgeToLineAngle | |

**User's choice:** UI 숨김 / 5종(EdgePair, PointToLine, PointToPoint, LineToLineAngle, LineToLineDistance) 숨김. EdgeToLineAngle·CircleDiameter 유지.

---

## #12·#3 중복 필드 정리

**Q1 ReuseFromShotName 처리**

| Option | Selected |
|--------|----------|
| 필드 완전 제거 (권장) | ✓ |
| 사용처 재확인 후 결정 | |
| 유지 | |

**Q2 RectL1Ratio/L2Ratio 통합**

| Option | Selected |
|--------|----------|
| 별도 유지 (권장) | ✓ |
| 단일 비율로 통합 | |
| execute에서 사용 패턴 보고 결정 | |

**User's choice:** ReuseFromShotName 완전 제거(SourceShotName 유지) / RectL1·L2 별도 유지.

---

## #5 픽셀분해능 단일화

**Q1 단일 소스 위치**

| Option | Selected |
|--------|----------|
| 카메라별 단일값 (Top/Bottom/Side) | ✓ |
| Shot별 단일값 | |
| execute에서 구조 확인 후 결정 | |

**Q2 X/Y 분리 처리**

| Option | Selected |
|--------|----------|
| 단일값으로 통합 (X=Y) | ✓ |
| X/Y 분리 유지 | |
| 현재 카메라 실측값 확인 후 | |

**Q3 기존 레시피 마이그레이션**

| Option | Selected |
|--------|----------|
| 로딩 시 카메라값으로 덮어쓰기 (권장) | ✓ |
| 기존값 우선, 카메라값은 fallback | |
| execute에서 실제 레시피 확인 후 | |

**User's choice:** 카메라별 단일값 + X=Y 통합 + 로딩 시 카메라값 덮어쓰기(X≠Y는 X 기준).

---

## #10·#11 정리 범위

**Q1 주석 정리 범위**

| Option | Selected |
|--------|----------|
| 저위험 항목만 (권장) | ✓ |
| 전체 패스 적극 정리 | |
| 이번 phase에서 제외 | |

**Q2 시작 지연 분석 범위**

| Option | Selected |
|--------|----------|
| 원인 식별만, 코드 변경은 carry-over (권장) | |
| 원인 식별 + 저위험 개선까지 | ✓ |
| 이번 phase에서 제외 | |

**User's choice:** 주석 저위험만 / 시작 지연 원인 식별 + 저위험 개선까지.

---

## Claude's Discretion

- #5 단일 소스 저장 위치(카메라 config vs SystemSetting), 분배/참조 방식, 코드 변경 범위
- #11 프로파일링 도구/방법
- #10 정리 대상 주석 구체 판정

## Deferred Ideas

- #3 단일 비율 통합 — v1.2 재검토 가능
- #10 전체 주석 재작성 — v1.2 헝가리안 리팩토링(Phase 26)에 통합
- #11 구조적/고위험 시작 지연 개선 — 별도 phase/carry-over
