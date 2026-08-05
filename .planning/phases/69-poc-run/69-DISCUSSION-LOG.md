# Phase 69: 메인 화면 POC 패널 정리 + RUN 버튼 간헐적 미동작 조사 - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-08-05
**Phase:** 69-poc-run
**Areas discussed:** IsIdle 수정 범위, POC 패널 처리, 실패 사유 메시지 포함 여부

---

## IsIdle 수정 범위

| Option | Description | Selected |
|--------|-------------|----------|
| RUN 버튼만 시퀀스별 체크로 교체 | InspectionListView 호출부만 국소 수정, StateAll/IsIdle 자체는 안 건드림 | (초기 응답 대신 사용자가 안전성 우려 제기) |
| SequenceHandler 자체에 시퀀스별 IsIdle 오버로드 추가 | 인프라 단위 수정, 영향범위 더 넓음 | |

**사용자의 실제 응답:** "기존 Dual image도 문제없이 Run에서 동작이 되려나?? 아니면 2개다 냅둘까" — 옵션을 고르지 않고 안전성 리스크를 직접 질문함.

**추가 조사 결과:** 에이전트가 `DeviceHandler.cs:221-244`를 조사해 TopBottom 역할 실HW에서 Top/Bottom이 동일 `MilCamera` 객체를 공유하고 grab 경로에 lock이 없음을 확인. 순수 시퀀스별 독립 판정은 실HW에서 새로운 하드웨어 경합 위험을 만들 수 있음이 밝혀짐.

**최종 결정 (재확인 질문 2회 후 확정):** 시퀀스별 독립 판정 + 물리 카메라를 실제로 공유하는 경우에만 상호배타 유지(SIMUL_MODE는 항상 독립 실행 허용, 실HW는 공유 카메라 조합만 차단). 첫 확인 질문("무슨말인지 모르겠네")에 대해 쉬운 설명으로 재답변 후 "응, 그렇게 진행해줘"로 승인.

**Notes:** 사용자의 최초 질문이 없었다면 실HW 하드웨어 경합 버그를 새로 만들 뻔한 케이스 — discuss-phase 의 안전장치 역할이 실제로 작동한 사례.

---

## POC 패널 처리

| Option | Description | Selected |
|--------|-------------|----------|
| 지금처럼 유지 | z_index 지정 수동 테스트 수단 유지 | ✓ |
| 삭제 | 이미 실제 프로덕션 경로와 동일 코드라 따로 둘 이유 없음 | |
| 단순화(개발자 메뉴/디버그 빌드에만 노출) | 일반 운영자에겐 숨기고 개발 용도로만 유지 | |

**사용자의 선택:** 지금처럼 유지.
**Notes:** IAxisController/Z축 실이동 관련 추가 구현은 불필요 — 이미 사용자가 원하는 상태(Z축 실이동은 처음부터 외부 PLC 담당)로 확인됨.

---

## 실패 사유 메시지 포함 여부

| Option | Description | Selected |
|--------|-------------|----------|
| 이번에 포함 | IsIdle 수정과 같은 파일(InspectionListView.xaml.cs)을 건드리는 작업이라 함께 하는 게 효율적 | ✓ |
| 별도로 미루기(그룹 D-3와 통합) | SequenceContext/ActionContext 의 NG 사유 데이터 구조 리서치가 선행되어야 함 | |

**사용자의 선택:** 이번에 포함(추천 옵션 선택).
**Notes:** 이건 그룹 D-3("RUN 실행은 됐지만 NG난 이유 설명")와는 다른 주제 — "RUN 자체가 막혔을 때 어느 시퀀스 때문인지"만 다룬다. `SequenceHandler._StateSeqName` 필드를 재사용하면 될 것으로 보임.

---

## Claude's Discretion

- IsIdle/StateAll 수정 레이어(SequenceHandler 오버로드 vs 호출부 국소 수정)
- 카메라 공유 여부 판정 로직의 정확한 구현 위치
- 실패 메시지 정확한 문구

## Deferred Ideas

- REVERSE_X_BOTTOM 미적용 버그 (별도 작업 task_aabad99c 로 분리)
- 그룹 D-3 (RUN 실행 후 NG 사유 다이얼로그) — 별도 phase, 리서치 선행 필요
