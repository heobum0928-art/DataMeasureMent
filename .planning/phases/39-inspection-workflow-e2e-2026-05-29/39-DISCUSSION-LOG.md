# Phase 39: 검사 워크플로우 E2E - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-05-29
**Phase:** 39-inspection-workflow-e2e-2026-05-29
**Areas discussed:** Datum 검출 실패 분기 정책 / TCP 결과 코드 매핑 / FAI 측정 실패 처리 + NG 누적 / 검증 범위 + v2.7 경계

---

## Datum 검출 실패 분기 정책

### Q1. Datum 검출 실패 시 어떻게 동작해야 하나요?

| Option | Description | Selected |
|--------|-------------|----------|
| Per-FAI gate (권장) | FAI.DatumRef 기준 해당 datum 실패 FAI/Measurement만 skip+NG, 다른 datum FAI는 정상. _datumTransforms 활용 | ✓ |
| All-or-nothing strict | DatumConfigs 중 하나라도 실패 → 사이클 종료. WF-01 문자 그대로, Phase 37 lenient 회귀 | |
| All-failed strict | DatumConfigs 전부 실패 시에만 종료, 하나라도 성공 시 lenient 유지 | |
| 현행 lenient 유지 | 코드 그대로 — datum 실패도 계속 측정, 종합 판정에만 반영. WF-01 strict 명세 불충족 | |

**User's choice:** Per-FAI gate
**Notes:** Phase 37 lenient의 의도(다른 datum 살아있어도 측정 진행)를 per-FAI 차원으로 끌어내려 WF-01 strict 명세를 정밀하게 충족.

### Q2. Per-FAI gate 에서 datum 실패로 skip 된 FAI 는 어떻게 마킹하나요?

| Option | Description | Selected |
|--------|-------------|----------|
| 검출실패 subtype 구분 (권장) | FAI.IsPass=false + MeasuredValue=0, LastSkipReason 플래그 또는 신규 enum 으로 측정 NG와 구분. UI/Excel 식별 가능 | ✓ |
| 측정 NG 와 동일 처리 | FAI.IsPass=false 일반 NG와 동일. 코드 단순, 분석에서 원인 구분 불가 | |
| Skipped 독립 상태 | FAI.IsPass=null 또는 EMeasureStatus { OK, NG, Skipped } 3-state. 구문 변경 폭 넓음 | |

**User's choice:** 검출실패 subtype 구분

### Q3. 사이클 종합 판정 계층구조는?

| Option | Description | Selected |
|--------|-------------|----------|
| 검출실패 > NG > OK (권장) | datum-skip FAI 1건 이상 = 검출실패. 아니면 측정 NG 있으면 NG. 아니면 OK. 3-state 명확 | ✓ |
| NG 통합 | datum-skip이든 측정 NG든 모두 NG. WF 문자에 맞지 않음 | |
| 측정 NG 우선 | 측정 NG 있으면 NG, 없고 datum-skip만 있으면 검출실패 | |

**User's choice:** 검출실패 > NG > OK

### Q4. datum 실패 시각화/로깅은?

| Option | Description | Selected |
|--------|-------------|----------|
| 로깅 + UI 표시 모두 (권장) | Logging.PrintLog 유지 + MainView Datum overlay 'DETECT FAIL' + InspectionListView 적색 배지 | ✓ |
| 로깅만 | Phase 37 코드 그대로 — 로깅만, UI 변경 없음 | |
| 로깅 + UI + TCP 상세 메타 | TestResultPacket에 datumFailureCount 신규 필드. 프로토콜 확장 | |

**User's choice:** 로깅 + UI 표시 모두

---

## TCP 결과 코드 매핑

### Q5. 사이클 종합 결과 레벨 (responsePacket.Result) — 3-state를 어떤 enum 값에 매핑할까요?

| Option | Description | Selected |
|--------|-------------|----------|
| NotExist 재사용 (권장) | OK=OK, NG=NG, 검출실패=NotExist. enum/wire 추가 0. "지정 위치 없음" ≈ datum 미검출 | ✓ |
| ANG 재사용 | 검출실패=ANG('A'). v2.6 기존 코드, 의미 = Angle Fail로 해석 혼동 우려 | |
| 신규 enum DetectFail | EVisionResultType.DetectFail=5 + wire 'D'. 의미 정확, 제어 측 협의 필수 | |
| NG 로 통합 | responsePacket.Result 는 binary 유지. 제어 측 구분 불가 | |

**User's choice:** NotExist 재사용
**Notes:** wire 포맷 TEST_RESULT_NOTEXIST="N" 이미 정의 — 제어 측 추가 협의 불필요.

### Q6. FAIResults 개별 레벨 (FAIResultData) 은 어떻게 표현하나요?

| Option | Description | Selected |
|--------|-------------|----------|
| datum-skip FAI = 'N', 측정 NG = 'F' (권장) | FAIResultData.Result 3-state. 정상=P, 측정 실패=F, datum-skip=N. wire 포맷 재사용 | ✓ |
| FAIResultData binary (P/F) 유지 | FAIResultData IsPass bool 그대로. datum-skip은 사이클 Result 레벨에서만 표현 | |
| FAIResultData 에 신규 SkipReason 필드 추가 | Result는 binary 유지, SkipReason ('Datum','Measure',null) 별도 필드 — wire 증가 | |

**User's choice:** datum-skip FAI = 'N', 측정 NG = 'F'

---

## FAI 측정 실패 처리 + NG 누적

### Q7. FAI 측정 실패 처리 제약 — 추가할 로직이 있나요?

| Option | Description | Selected |
|--------|-------------|----------|
| 현행 유지 + 명문화/UAT (권장) | 코드 변경 0. PLAN must_haves 에 정책 명시, UAT 에 NG 누적 케이스 추가. 회귀 0 | ✓ |
| 임계 조기 종료 추가 | NG 50% 이상 시 EStep.End 조기 종료. 시간 절약, NG 데이터 손실 | |
| 측정 실패 명세 코드 세분화 | 에지 미검출 / 공차 초과 / HALCON 예외 3개 구분. 구현/UAT 부담 증가 | |

**User's choice:** 현행 유지 + 명문화/UAT

### Q8. TestResultPacket 에서 NG 원인 메타는 어디까지?

| Option | Description | Selected |
|--------|-------------|----------|
| 현행 FAIResults 표현 레벨만 (권장) | FAIResults[i].Result = P/F/N. 원인 자명. wire 추가 0 | ✓ |
| PacketFooter 에 요약 메타 추가 | ngCount / detectFailCount 신규 필드. wire 증가, 제어 측 함의 필요 | |
| TCP 종합+개별 유지, 엑셀 export 에만 원인 메타 | Phase 40 OUT-02 엑셀에서만 세분 표시 | |

**User's choice:** 현행 FAIResults 표현 레벨만

---

## 검증 범위 + v2.7 경계

### Q9. Phase 39 sign-off 기준 은?

| Option | Description | Selected |
|--------|-------------|----------|
| SIMUL UAT 통과로 sign-off (권장) | Cal_Image 파일셋으로 Top/Side/Bottom 멀티샷 3분기 UAT 통과. 실카메라는 Phase 44 분리 | ✓ |
| SIMUL + HIK 실카메라 둘 다 필수 | Phase 44 와 의미 겹침, HW 가용 시기 의존 | |
| SIMUL UAT + 실카메라 smoke 테스트 | 절충안. 1 사이클 돌아가는지만 smoke | |

**User's choice:** SIMUL UAT 통과로 sign-off

### Q10. v2.7 프로토콜과의 경계 — Phase 39 에서 어디까지 선행하나요?

| Option | Description | Selected |
|--------|-------------|----------|
| 순수 v2.6, 내부 모델도 현행 유지 (권장) | NotExist 재사용 외 enum 확장 없음. CycleState/ECycleResult/z_index 미도입. POC 5순위 동기화 부담 0 | ✓ |
| 내부 enum 만 3-state 한정, wire 는 v2.6 | 영역 1/2 합의와 사실상 동등 | |
| v2.7 enum/CycleState 선행 도입 (wire 는 v2.6) | Phase 48/49 구현 쉬워짐, 코드 변경 폭 큼 | |

**User's choice:** 순수 v2.6, 내부 모델도 현행 유지

---

## Claude's Discretion

- LastSkipReason 필드 타입(string vs 신규 enum) — 구현 시 patterns/회귀 고려해 결정.
- UI 적색 배지 / DETECT FAIL 라벨 시각 디테일.
- FAIResultData 시그니처 변경 vs 신규 필드 추가 방식 — wire 포맷 회귀 가드 우선.
- UAT 입력 이미지 정확한 시나리오.

## Deferred Ideas

- EVisionResultType.DetectFail 신규 enum (v2.7 PROTO 차원, Phase 48/49).
- PacketFooter ngCount / detectFailCount 요약 필드 (POC 이후).
- NG 임계 조기 종료 (POC 이후).
- 실HW [STARTUP] 재측정 (Phase 44 CO-38-04).
- 결과 이미지 리뷰어 / 엑셀 export (Phase 40/41).
- CycleState / ECycleResult enum 자동 리셋 (Phase 49 PROTO-03~05).
