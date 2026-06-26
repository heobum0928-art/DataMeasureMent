# Phase 66: 조명 정합 — 검사 Ring7/Coax + Align 동축 - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-06-26
**Phase:** 66-ring7-coax-align-2026-06-26
**Areas discussed:** 런타임 동축 소유권, Align 동축 저장 단위/위치, 동축 자동적용 시점+수동조정, 검사 Coax 처리

---

## 런타임 동축 소유권 (PC vs PLC)

| Option | Description | Selected |
|--------|-------------|----------|
| 비전 PC 자동 | Run 시 PC가 저장 슬롯 동축값 grab 직전 자동 ON, PLC 미관여. ApplyShotLights 패턴 일관 | ✓ |
| PLC 소유 | PLC가 $ALIGN_TEST 전 동축 ON, PC는 grab만 | |
| 하이브리드 | 평소 PC 자동, PLC 명령 시 PLC 우선 | |

**User's choice:** 비전 PC 자동
**Notes:** 별도 PC·1채널이라 충돌 없음. 검사 조명 흐름과 일관성 확보.

---

## Align 동축 저장 단위/위치

| Option | Description | Selected |
|--------|-------------|----------|
| 슬롯 레퍼런스 JSON | Bottom_{slot}.json에 CoaxEnabled/CoaxLevel 추가. 슬롯 하나로 묶음. Tray 단일 | ✓ |
| 별도 조명 설정 파일 | 동축 설정만 따로(slot→level 맵) | |
| 전역 INI 1값 | SystemSetting 단일 값 | |

**User's choice:** 슬롯 레퍼런스 JSON
**Notes:** Phase 65 슬롯 JSON 경로 재사용. 면별 조명 차이 반영 가능.

---

## 동축 자동적용 시점 + 수동조정

| Option | Description | Selected |
|--------|-------------|----------|
| Teach+Run+Grab 자동 + 수동 | 모든 캡처 직전 저장값 자동 ON + 작업자 슬라이더 override(저장) | ✓ |
| Run만 자동 | 검사 Run 시에만 ON | |
| 수동 전용 | 자동 없음 | |

**User's choice:** Teach+Run+Grab 자동 + 수동
**Notes:** 티칭 조명 = 런타임 조명 일치 보장(매칭 스코어 안정).

---

## 검사 Coax 처리 방식 (Ring7 추가는 확정)

| Option | Description | Selected |
|--------|-------------|----------|
| 숨김 Browsable(false) | INI 키 보존(하위호환) + 검사 PropertyGrid 미노출. 회귀 0 | ✓ |
| 완전 제거 | CoaxLight_* 필드+키 삭제. 구레시피 키 orphan 위험 | |
| 그대로 노출 유지 | 현재처럼 검사에 Coax 노출(비권장) | |

**User's choice:** 숨김 Browsable(false)
**Notes:** Phase 64 D-11 INI 키 보존 원칙과 일관. 런타임 코드(매핑)는 유지.

## Claude's Discretion

- Ring7 PropertyGrid Category 명/순서, 동축 UI 위젯 형태, 슬롯 JSON 동축 키명+기본값 처리.

## Deferred Ideas

- 동축 2채널(단일 PC에 Bottom+Tray 동거 시) — 현재 별도 PC 전제라 불필요.
- 각 PC 동축 물리 배선 채널 매핑 확정 — 실장 시 광학/제어팀 확인(채널 인덱스만 조정).
