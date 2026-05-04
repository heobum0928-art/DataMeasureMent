# Phase 7: Measurement 오버레이 회귀 수정 - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in 07-CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-04-22
**Phase:** 07-overlay-regression-fix
**Areas discussed:** TryExecute 시그니처, EdgeInspectionOverlay 모델 확장, OK/NG 색상/ID 네이밍, 구현 범위

---

## Gray Area Selection

| Option | Description | Selected |
|--------|-------------|----------|
| TryExecute 시그니처 확장 방식 | overlay 반환 경로 설계 | ✓ |
| EdgeInspectionOverlay 모델 확장 | Circle/Arc 등 새 지오메트리 표현 | ✓ |
| OK/NG 색상/ID 네이밍 규약 | Phase 3 규약 확장 vs 유지 | ✓ |
| 구현 범위 — 6종 전체 vs EdgePair만 | Phase 7 스코프 결정 | ✓ |

**User's choice:** 4개 영역 모두 선택

---

## TryExecute 시그니처 확장 방식

### Q1: overlay 반환 방식

| Option | Description | Selected |
|--------|-------------|----------|
| out 파라미터 추가 | `out List<EdgeInspectionOverlay> overlays` 추가 | ✓ |
| 수집자 파라미터 주입 | 호출부가 준비한 List를 Measurement가 Add | |
| LastOverlays 프로퍼티 | MeasurementBase가 상태로 보관, 호출부가 getter | |

**User's choice:** out 파라미터 추가
**Notes:** C# 7.2 관용, 기존 `LastMeasuredValue`/`LastJudgement`는 상태 프로퍼티이지만 overlay는 호출 단위 산출물로 취급 — 스레드 안전성과 명확성 모두 유리.

### Q2: 측정 실패 시 overlay 처리

| Option | Description | Selected |
|--------|-------------|----------|
| 생성 시도, 실패 전까지 쌓인 overlay 유지 | 부분 지오메트리 시각화로 디버깅 용이 | ✓ |
| 빈 리스트 반환 | 단순, 오해 없음 | |
| null 반환하고 호출부에서 스킵 | null vs empty 구분 가능 | |

**User's choice:** 부분 overlay 유지
**Notes:** FAIEdgeMeasurementService가 이미 실패해도 ROI 박스는 유지하는 패턴과 일치. NG 색상으로 실패 지점 드러남.

### Q3: Measure 루프 누적 범위

| Option | Description | Selected |
|--------|-------------|----------|
| 현재 Shot의 모든 FAI × Measurement | 기존 Shot 단위 표시 유지 | ✓ |
| 선택된 FAI만 | 필터링 UX 필요 | |
| 선택된 Measurement만 | 전체 NG 부위 한눈에 안 보임 | |

**User's choice:** 현재 Shot 전체 누적
**Notes:** 필터링은 별도 phase deferred.

---

## EdgeInspectionOverlay 모델 확장

### Q1: 새 지오메트리(원·각도·링크) 표현 방식

| Option | Description | Selected |
|--------|-------------|----------|
| EdgePair만 복구, 모델 유지 | Phase 7 스코프 최소, 나머지 5종 deferred | ✓ |
| Line/Point만 써서 5종도 분해 구현 | 원을 다각형으로 근사 등 | |
| 모델 확장: Shape enum + Circle/Arc 필드 | 6종 전체 깔끔 | |

**User's choice:** EdgePair만 복구, 모델 유지
**Notes:** 블로커(I1) 복구가 Phase 7의 유일한 목표. 5종 overlay 구현은 scope 밖.

---

## OK/NG 색상/ID 네이밍 규약

### Q1: OK/NG 색상 적용 시점

| Option | Description | Selected |
|--------|-------------|----------|
| Measure 루프에서 judgement 후 suffix 부여 | Phase 3 패턴 + quick 260417-ou8 선례와 일치 | ✓ |
| TryExecute 내부에서 자체 판정하여 suffix | 판정 로직 이중화 위험 | |
| 색상을 ID 대신 별도 속성으로 | 모델 확장 필요(Area 2에서 거절) | |

**User's choice:** 루프에서 suffix 부여
**Notes:** HalconDisplayService의 `FAI-Edge*-OK/-NG` startsWith 분기를 그대로 활용, 서비스 코드 변경 없음.

### Q2: EdgePair 외 5종 처리

| Option | Description | Selected |
|--------|-------------|----------|
| 빈 리스트 반환 | 측정값은 DataGrid, 캔버스 표시 없음 | ✓ |
| TODO 주석 + 빈 리스트 | 미래 phase 지점 명시 | |
| ROI 박스 최소 표시 | RoiDefinition 렌더와 중복 우려 | |

**User's choice:** 빈 리스트 반환
**Notes:** deferred에 5종 각각 기록.

---

## 구현 범위 — 6종 전체 vs EdgePair만

Area 2 Q1에서 이미 결정 (EdgePair만 복구). 별도 질문 생략.

---

## Claude's Discretion

- overlay Clone 필요 여부 (FAIEdgeMeasurementService가 이미 새 리스트 반환 → 그대로 전달 가능).
- suffix 부여 로직 배치 (Measure 루프 인라인 vs 헬퍼 메서드).
- 5종 파생 클래스의 empty list 반환 스타일 (`new List<>()` vs `null` + null-safe 수집).

## Deferred Ideas

- 5종 Measurement 각각의 overlay 구현 (PointToLine, PointToPoint, LineToLineAngle, LineToLineDistance, CircleDiameter).
- EdgeInspectionOverlay 모델 확장 (Shape enum, Circle/Arc 필드).
- FAI/Measurement 단위 선택 필터링.
- Datum 결과 시각화 overlay.
- Nyquist 자동 단위 테스트 (Phase 9로 이관).
