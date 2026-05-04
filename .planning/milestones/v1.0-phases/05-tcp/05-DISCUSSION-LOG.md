# Phase 5: 검사 시퀀스 & TCP - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-04-09
**Phase:** 05-검사 시퀀스 & TCP
**Areas discussed:** Shot 순회 실행 전략, TCP 응답 포맷, Z축 이동 & SIMUL 처리, UI 결과 갱신 타이밍

---

## Shot 순회 실행 전략

| Option | Description | Selected |
|--------|-------------|----------|
| EndActionIndex 확장 | SequenceBase에 StartAll() 메서드 추가: EndActionIndex = Actions.Length-1로 설정. 프레임워크 최소 변경 | ✓ |
| 새 InspectionSequence 클래스 | SequenceBase 상속 전용 시퀀스. 내부에서 Shot 루프 직접 관리. 프레임워크 변경 없지만 시퀀스 구조 변경 더 큼 | |
| 단일 Action 내부 루프 | Action_FAIMeasurement 하나가 RecipeManager.Shots를 직접 순회. 프레임워크 미접촉, Shot별 이벤트 분리 어려움 | |

**User's choice:** EndActionIndex 확장 (추천)
**Notes:** None

### Follow-up: StartAll() 호출 진입점

| Option | Description | Selected |
|--------|-------------|----------|
| IsDynamicFAI 분기 | ProcessTest()에서 IsDynamicFAIMode 체크 → true면 seq.StartAll(), false면 기존 Start(). 변경 지점 1곳 | ✓ |
| Start() 내부 자동 감지 | SequenceBase.Start(packet)에서 Identifier2 매칭 실패 시 전체 순회로 폴백. 암묵적 동작 | |

**User's choice:** IsDynamicFAI 분기 (추천)
**Notes:** None

---

## TCP 응답 포맷

| Option | Description | Selected |
|--------|-------------|----------|
| FAI별 결과 포함 | visionResults[] 배열 패턴 재사용. FAI별 OK/NG + 거리(mm) 리스트 전송. 호스트가 개별 FAI 추적 가능 | ✓ |
| 종합 OK/NG만 | 기존 TestResultPacket 구조 유지. 호스트 프로토콜 변경 최소화 | |
| 단계적 확장 | 1차 종합만, 향후 FAI별 추가. AddResponse()에서 조건 분기 | |

**User's choice:** FAI별 결과 포함 (추천)
**Notes:** None

### Follow-up: FAI 결과 패킷 구조

| Option | Description | Selected |
|--------|-------------|----------|
| 동적 리스트 | FAI 개수 먼저 전송 후 그 수만큼 결과 순차 나열. 레시피별 FAI 수 다르므로 유연 | ✓ |
| 고정 배열 재사용 | Bottom처럼 visionResults[10] 고정. 단순하지만 FAI 10개 초과 시 제한 | |

**User's choice:** 동적 리스트 (추천)
**Notes:** None

---

## Z축 이동 & SIMUL 처리

| Option | Description | Selected |
|--------|-------------|----------|
| 스텁 예약 + SIMUL | MoveZ 스텝 추가, 모터 인터페이스만 정의. SIMUL에서 SimulImagePath 이미지 로드 | ✓ |
| Z축 미구현 (SIMUL만) | Z축 이동 없이 진행. SIMUL에서 SimulImagePath로 이미지 교체. Z축 제어는 별도 Phase | |

**User's choice:** 스텁 예약 + SIMUL (추천)
**Notes:** None

### Follow-up: SIMUL 이미지 로드

| Option | Description | Selected |
|--------|-------------|----------|
| SimulImagePath 우선 | ShotConfig.SimulImagePath 설정 시 해당 경로 로드, 미설정 시 VirtualCamera(D:\1.bmp) 폴백 | ✓ |
| 기존 방식 유지 | 모든 Shot에 동일 D:\1.bmp 사용. Shot별 차별화 불가 | |

**User's choice:** SimulImagePath 우선 (추천)
**Notes:** None

---

## UI 결과 갱신 타이밍

| Option | Description | Selected |
|--------|-------------|----------|
| Shot별 실시간 | 각 Action_FAIMeasurement 완료 시 OnActionChanged 이벤트로 UI 갱신. 사용자가 진행 상황 실시간 확인 | ✓ |
| 전체 완료 후 일괄 | 모든 Shot 완료(Finish) 후 한 번에 UI 갱신. 단순하지만 진행 상황 불가 | |

**User's choice:** Shot별 실시간 (추천)
**Notes:** None

---

## Claude's Discretion

- StartAll() 시그니처 세부
- ExecuteAction() CurrentActionIndex 증가 시점
- IAxisController 인터페이스 메서드 시그니처
- FAI 결과 동적 리스트 클래스명
- 종합 판정 로직 (단순 AND vs 가중치)
- SimulImagePath 이미지 로드 방식

## Deferred Ideas

- Z축 모터 드라이버 실제 구현 — 별도 디바이스 Phase
- 검사 결과 로깅/히스토리 저장 — v2 기능
- 검사 중 사용자 중단(Abort) UX
- Shot 병렬 실행 (멀티 카메라 동시 Grab)
- NG 시 결과 이미지 자동 저장
