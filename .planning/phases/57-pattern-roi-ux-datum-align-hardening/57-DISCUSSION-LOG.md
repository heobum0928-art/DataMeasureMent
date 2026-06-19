# Phase 57: 패턴 ROI UX & Datum 정렬 보강 - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-06-19
**Phase:** 57-pattern-roi-ux-datum-align-hardening
**Areas discussed:** #4 Side 4-ROI 정렬, #1 2개 필수 강제 방식, #6 leveling 제거 범위, #3 magenta 기준선 제거 확인

---

## Gray area 선택

| Option | Description | Selected |
|--------|-------------|----------|
| #4 Side 4-ROI 정렬 | DualImage ALIGN 미적용 상태, 설계 비어있음 (필수) | ✓ |
| #1 2개 필수 강제 방식 | 경고만 vs 하드 블록 | ✓ |
| #6 leveling 제거 범위 | 완전 제거 vs 동작만 vs 비활성 | ✓ |
| #3 magenta 기준선 제거 확인 | Phase 56 추가 기준선 손실 우려 | ✓ |

**User's choice:** 4개 모두 선택
**Notes:** #2(토글)/#5(lenient)는 seed 에서 사전 확정 — 재논의 불필요.

---

## #4 Side 4-ROI (DualImage) 정렬

| Option | Description | Selected |
|--------|-------------|----------|
| 이미지별 분리 매칭 (B) | 가로/세로 각 독립 패턴 → transformH/transformV 2개 | |
| 단일 공유 transform (A) | 가로에서만 매칭 → 가로+세로 ROI 모두 적용 | ✓ |
| 일단 논의 필요 | 물리 셋업 먼저 확인 | (경유) |

**User's choice:** 단일 공유 transform (A)
**Notes:** 사용자가 "같은 부품이니 가로에서만 보정한 x,y,tilt 를 세로에 그대로 적용하면 되지 않나, 4개 칠 필요 없지 않나"로 발의. 물리 셋업 확인 결과 **텔레센트릭 렌즈 + Z축 포커싱만 이동** → 두 이미지 같은 픽셀 좌표계(배율·측면위치 불변) → 단일 rigid transform 이 세로 ROI 에도 수학적으로 정확. seed 의 "세로 분리매칭"은 불필요한 과설계로 판명. 현재 DualImage 는 ALIGN 미적용(deferred, Action_FAIMeasurement.cs:147) → 게이트 해제 필요.

---

## #1 패턴 ROI 2개 필수

| Option | Description | Selected |
|--------|-------------|----------|
| 경고 + override 허용 | 1개면 경고, OK 시 단일 진행 (escape 유지) | ✓ |
| 하드 블록 | 1개면 생성 거부, 2개 강제 | |
| 현상 유지(정보만) | 강제 없음 | |

**User's choice:** 경고 + override 허용
**Notes:** 좋은 패턴 피처 1개뿐인 부품 탈출구 유지. 패턴2는 Phase 55 baseline 각도용 권장사항.

---

## #6 leveling 제거 범위

| Option | Description | Selected |
|--------|-------------|----------|
| 완전 제거(코드+상태+INI) | 필드+EStep.Level+TryCompute/Get+INI 직렬화 모두 | ✓ |
| 동작만 제거(필드 유지) | 소비경로만 제거, 필드/INI 키 유지 | |
| 비활성화만(dead code) | off 고정, 코드 잔존 | |

**User's choice:** 완전 제거
**Notes:** ALIGN 이 위치/tilt 대체로 중복. off 레시피 회귀 0 검증 필수. EStep.Level 상태머신 전이 재배선이 최우선 위험(에이전트 경고). 옛 INI stale 키는 ParamBase.Load 가 무시 → 로드 크래시 없음.

---

## #3 datum 시각화 색상

| Option | Description | Selected |
|--------|-------------|----------|
| 색만 통일(기준선 유지) | magenta+yellow → slate blue recolor, 긴 기준선 유지 | ✓ |
| 완전 제거(십자만) | 기준선 삭제, origin 십자+화살표만 | |
| 길이만 줄이고 유지 | slate blue 통일 + 기준선 짧게 | |

**User's choice:** 색만 통일(기준선 유지)
**Notes:** magenta 긴 기준선은 Phase 56 에서 사용자 요청으로 추가(14208px 이미지 축 시인성). slate blue 30px 화살표만으론 큰 이미지에서 안 보임. seed 의 "제거" 표현은 실제로 "색 중복 정리" 의도 → recolor 로 정보 보존.

---

## Claude's Discretion

- #3 기준선 길이/두께 미세 조정
- #6 EStep.Level 제거 후 상태머신 전이 재배선 구현 방식
- #1 경고 메시지 문구

## Deferred Ideas

- 세로 이미지 별도 패턴 매칭(B): 비텔레센트릭/멀티포지션 셋업 도입 시에만 재고
- leveling 재도입: 계획 없음(완전 제거)
