# Phase 49: P/F/B 판정 엔진 - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-06-23
**Phase:** 49-protocol-v1-judgment-engine
**Areas discussed:** A. 사이클 아키텍처, B. "마지막 Index" 판별, C. Datum(Index 0) 처리, D. enum/리셋/CO-48-01

---

## A. 사이클 아키텍처

### A-1. $TEST(z_index) 검사 범위

| Option | Description | Selected |
|--------|-------------|----------|
| 해당 Index Shot만 | z_index 매핑 Shot/FAI 그룹만 검사, 조명전환 멀티샷 모델 일치 | ✓ |
| 전체 Shot 검사 유지 | 기존 AddResponse처럼 매 $TEST 전체 검사 — spec 충돌 | |

**User's choice:** 해당 Index Shot만

### A-2. 사이클 NG/상태 누적 위치

| Option | Description | Selected |
|--------|-------------|----------|
| InspectionSequence 멤버 상태 | 기존 _failedDatums와 동일 lifecycle에 멤버 추가, 신규 클래스 최소 | ✓ |
| 신규 CycleStateMachine 클래스 | 전용 클래스 분리, 단위 테스트 쉬우나 상태 이중화 위험 | |

**User's choice:** InspectionSequence 멤버 상태
**Notes:** 사용자가 개념 설명 요청 → m_bCycleHasNG 같은 "기억 변수"를 어디 두느냐 비유(주머니 vs 클립보드)로 설명 후 옵션 1 선택.

---

## B. "마지막 Index" 판별

| Option | Description | Selected |
|--------|-------------|----------|
| 레시피 z_index 최댓값 자동 | 그 Site Shot 최대 z_index = 마지막, 추가 설정 불필요 | ✓ |
| ShotConfig.IsLastIndex 명시 플래그 | INI 명시 저장, 명시적이나 사람 지정 필요 | |
| 별도 Index Table config | PLC 미러, 설정 소스 이중 관리 | |

**User's choice:** 레시피 z_index 최댓값 자동
**Notes:** 전제 — PLC Index Table = 레시피 Shot 구성 일치 (주석+검증으로 고정).

---

## C. Datum(Index 0) 처리

### C-1. Datum 실패 판정 문자

| Option | Description | Selected |
|--------|-------------|----------|
| UseProtocolV1 분기에서만 F | v1.0 경로만 'F', v2.6은 'N' 유지 → 회귀 0 | ✓ |
| 전면 F로 통일 | v2.6/v1.0 모두 F — 기존 핸들러 동작 변경 위험 | |

**User's choice:** UseProtocolV1 분기에서만 F

### C-2. Datum 실패 후 후속 Index 처리

| Option | Description | Selected |
|--------|-------------|----------|
| 핸들러 주도 | Vision은 F 즉시 응답만 + 사이클 F 마킹, 핸들러가 종료 | ✓ |
| Vision 내부 skip 강제 | 핸들러 계약 위반 대비 추가 로직 | |

**User's choice:** 핸들러 주도

---

## D. enum / 리셋 / CO-48-01

### D-1. enum 신설 범위

| Option | Description | Selected |
|--------|-------------|----------|
| ECycleResult만 + 멤버 bool | ECycleResult{Buffer,Pass,Fail} 1개, 라이프사이클은 멤버 bool | ✓ |
| CycleState 라이프사이클 enum도 추가 | 두 enum, 의미 중복 가능 | |

**User's choice:** ECycleResult만 + 멤버 bool

### D-2. 사이클 상태 자동 리셋 시점

| Option | Description | Selected |
|--------|-------------|----------|
| Index 0 수신 시 = 사이클 시작 | 비정상 종료 후에도 항상 깨끗한 시작 | ✓ |
| 마지막 Index 송신 후 | 자연스러우나 중단 시 리셋 누락 가능 | |

**User's choice:** Index 0 수신 시

### D-3. CO-48-01 흡수 여부

| Option | Description | Selected |
|--------|-------------|----------|
| Phase 49에 흡수 | EncodingType static→instance, 제어 코드 정리 동반 | ✓ |
| 별도 이연 | 판정엔진 집중 | |

**User's choice:** Phase 49에 흡수

---

## Claude's Discretion

- ECycleResult↔IsBuffer/Result 매핑 코드 위치 (InspectionSequence vs VisionResponsePacket 헬퍼)
- 멤버 상태 필드 정확한 이름/개수 (헝가리언 준수 하에)

## Deferred Ideas

- 교차-Z 측정 (요구 3-2) — 신규 영역
- PROTO-06 통신 회귀 — Phase 50
- 분단위 데이터 저장 (요구 4) — 별도
- TcpServer Header/Trailer static (동일 문제, 범위 밖 기록만)
