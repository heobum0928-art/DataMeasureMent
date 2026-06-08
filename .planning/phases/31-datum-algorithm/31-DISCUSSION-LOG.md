# Phase 31: Datum 기준 측정 알고리즘 확장 - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-05-19
**Phase:** 31-datum-algorithm
**Areas discussed:** 범위 & 타입 우선순위, D1/H5 알고리즘 정체, SOP 불일치 해소, 기하 구성 체인 데이터 모델

---

## 범위 & 타입 우선순위

### 측정 타입 구현 범위

| Option | Description | Selected |
|--------|-------------|----------|
| 6종 전부 한 Phase | E8/D1·H5/I9·I10/CompoundAngle/ArcEdgeDistance 전부 Phase 31 | ✓ |
| CompoundAngle 제외 → 차기 Phase | 가장 복잡한 CompoundAngle 분리 | |
| 난이도순 단계 축소 | E8만 먼저 → I9/I10 → CompoundAngle 순 | |

### CO-23.1-01·02 처리

| Option | Description | Selected |
|--------|-------------|----------|
| Phase 31에 포함 (선행) | CO-01·02 둘 다 Phase 31, CO-02 선행 | ✓ |
| CO-02만 포함, CO-01 분리 | ROI 버튼만 포함 | |
| 둘 다 분리 | 둘 다 별도 quick | |

### Datum 절대좌표 주입 경로 일반화

| Option | Description | Selected |
|--------|-------------|----------|
| 인터페이스 도입 | IDatumOriginConsumer 류, 한 분기로 처리 | ✓ |
| MeasurementBase로 올림 | 공통 부모에 transient 필드 | |
| Claude 재량 | planner 결정 | |

### 거리 계산 로직 공용화

| Option | Description | Selected |
|--------|-------------|----------|
| EdgeToLineDistance 로직 공용 추출 | projection_pl 검증 로직을 공용 함수로 | ✓ |
| 타입별 독립 구현 | 각 타입이 자체 복제 | |
| Claude 재량 | planner 결정 | |

**User's choice:** 6종 전부 한 Phase / CO-23.1-01·02 둘 다 포함(선행) / 인터페이스 도입 / 로직 공용 추출
**Notes:** 인터페이스·공용화 질문은 사용자 요청으로 평이한 비유("스티커 방식", "계산기 공유")로 재설명 후 둘 다 A안 선택.

---

## D1/H5 알고리즘 정체

### D1/H5 구현 방식

| Option | Description | Selected |
|--------|-------------|----------|
| 신규 타입 (EdgeToLineAngle) | ROI 1개 + Datum A 각도, EdgeToLineDistance 패턴 연장 | ✓ |
| 기존 LineToLineAngle 확장 | 두 선 중 하나를 Datum A로 대체 | |
| Claude 재량 | planner 결정 | |

### Datum A 공급 방식

| Option | Description | Selected |
|--------|-------------|----------|
| 기존 Datum 메커니즘 재사용 | DatumConfig 티칭 → DetectedRefAngle 주입 | ✓ |
| Datum A 전용 처리 | 별도 구성/주입 경로 설계 | |
| Claude 재량 | researcher 확인 | |

**User's choice:** 신규 타입 EdgeToLineAngle / Datum A = 기존 Datum 메커니즘 재사용
**Notes:** SOP의 "LineToLineAngle" 표기는 측정 타입 이름이 아닌 HALCON 각도 연산 의미로 해석.

---

## SOP 불일치 해소

### CompoundAngle E2/E9/E10 출력값

| Option | Description | Selected |
|--------|-------------|----------|
| 3종 모두 각도 | E2/E9/E10 전부 degree | |
| E2=각도, E9/E10=거리 | E2만 Angle, E9/E10은 Datum C/B 거리 | ✓ |
| Claude 재량 | researcher 원본 재확인 | |

### ArcEdgeDistance 정의

| Option | Description | Selected |
|--------|-------------|----------|
| 호 피팅 → 호 위 점 → Datum 거리 | P1이 호 위의 점, 신규 타입 | ✓ |
| EdgeToLineDistance와 동일 | 명칭만 다름 | |
| Claude 재량 | researcher 원본 재확인 | |

**User's choice:** E2=각도 / E9·E10=거리 / ArcEdgeDistance = 호 피팅 기반 신규 타입
**Notes:** SOP 슬라이드 56/57의 distance/angle 혼선 — E2는 MSOP 원본 기준 각도, E9/E10은 슬라이드 본문 distance 채택.

---

## 기하 구성 체인 데이터 모델

### 다단계 기하 구성 표현

| Option | Description | Selected |
|--------|-------------|----------|
| measurement 내부 일괄 처리 | 사용자는 입력 ROI만 티칭, 중간 산출물 비노출 | ✓ |
| 구성 요소를 INI/UI에 노출 | 중간선·교점 별도 편집 가능 | |
| Claude 재량 | planner 결정 | |

### 호∩라인 교점 해 선택

| Option | Description | Selected |
|--------|-------------|----------|
| ROI 내부의 해 | 라인 ROI 영역 안 교점 채택 | ✓ |
| 호 중심에 가까운 해 | 기하 규칙 자동 | |
| measurement별 선택 옵션 | Near/Far PropertyGrid 노출 | |

### E2 vs E9/E10 타입 분할

| Option | Description | Selected |
|--------|-------------|----------|
| 출력별 2개 타입으로 분리 | 각도 타입 / 거리 타입 별개 | ✓ |
| 단일 타입 + 출력 모드 분기 | OutputMode 스위치 | |
| Claude 재량 | planner 결정 | |

**User's choice:** measurement 내부 일괄 처리 / ROI 내부의 해 / 출력별 2개 타입 분리
**Notes:** 사용자 요청으로 일상 비유(요리 레시피의 중간 반죽, 원을 꿰뚫는 직선, 자와 각도기) 재설명 후 3건 모두 A안 선택.

## Claude's Discretion

- 인터페이스명 / 거리 계산 공용 함수 위치·시그니처·명명
- 신규 측정 타입 클래스명 (EdgeToLineAngle 외 5종)
- 호 피팅 HALCON 연산자 선택
- ArcEdgeDistance 호 위 측정점 정의 (researcher MSOP 재확인)
- E9/E10 ↔ E2 기하 구성 체인 공유 범위
- ICustomTypeDescriptor 적용 여부
- CO-23.1-01/02 구현 형태

## Deferred Ideas

- SOP FAI 인벤토리 전수 실측 검증
- Side 카메라 검사 흐름 e2e (Phase 24)
- 호∩라인 교점 사용자 선택 옵션(Near/Far)
- CompoundAngle 중간 산출물 INI/UI 노출
