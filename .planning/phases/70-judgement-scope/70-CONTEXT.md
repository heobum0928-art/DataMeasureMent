# Phase 70: 종합판정 시퀀스 소유권 스코프 조사 - Context

**Gathered:** 2026-08-05
**Status:** Ready for planning

<domain>
## Phase Boundary

종합판정(cycle judgement) 함수 3곳이 `recipeManager.Shots`(Top/Side/Bottom 전체 SHOT이 담긴 글로벌 단일 리스트)를 `OwnerSequenceName == Name` 필터 없이 전역 순회해, 단독 시퀀스 RUN 시 무관한 다른 시퀀스의 미측정/리셋된 shot(`FAIConfig.IsPass` 기본값 false)이 섞여 잘못된 NG/NotExist 판정이 나올 수 있는 버그를 확정하고 고친다.

**범위 밖:** 판정 로직 자체의 재설계(계층 구조 `anyDatumSkip > NG > OK`는 변경하지 않음), v1.0 프로토콜 경로(이미 필터링되어 있어 영향 없음), Phase 69(RUN 버튼 응답성/IsIdle 병합 문제 — 별개 이슈).

</domain>

<decisions>
## Implementation Decisions

### A. 버그 확정
- **D-01:** 전역 순회는 **버그로 확정** (의도된 설계 아님). 근거: TCP v1.0(`ProcessTest`→`StartV1Scoped`)/TCP v2.6(`SequenceHandler.Start(TestPacket)`)/수동 UI RUN/배치 RUN 모든 실행 경로가 `packet.Identifier` 또는 UI 선택 기준 **단일 시퀀스만** 트리거한다 — 3개 시퀀스를 동시에 묶어 실행하는 코드 경로는 존재하지 않는다. 이 로직의 원류인 Phase 39 CONTEXT.md D-03("사이클 종합 판정 계층 = 검출실패 > NG > OK ... `InspectionSequence.AddResponse`에서 적용")도 "Top/Side/Bottom에 동일 정책을 통일 적용"이라는 뜻이지 "3개 시퀀스의 shot을 하나로 합쳐 판정"이라는 뜻이 아니다 — 전역 순회는 그 정책을 구현하다 생긴 필터 누락으로 판단.

### B. 수정 범위
- **D-02:** 아래 3곳 전부 수정 대상 (사용자 확인 — 화면 UI 2종만이 아니라 실제 PLC와 물린 v2.6 통신 경로까지 포함):
  1. `InspectionSequence.ComputeOverallResult` (InspectionSequence.cs:342-359) — 수동 RUN(`HandleManualCyclePersist`)이 소비
  2. `BatchRunService.HandleFinish` (BatchRunService.cs:78-147) — 일괄검사(Phase 51 BATCH-01) UI가 소비
  3. `InspectionSequence.AddResponse()` v2.6 레거시 경로 (InspectionSequence.cs:119-206, 특히 라인144 `foreach (var shot in recipeManager.Shots)`) — `SystemHandler.Handle.Setting.UseProtocolV1 == false`일 때 실행되는, 실제 PLC/핸들러와 TCP로 연결된 생산 통신 경로
- v1.0 경로(`AddResponseV1Cycle`→`AggregateIndexFais`, `ComputeLastZIndex`)는 이미 `bool bOwnedByThisSeq = shot.OwnerSequenceName == Name; if (!bOwnedByThisSeq) continue;` 패턴으로 필터링되어 있어 **수정 불필요** — 이 패턴을 3곳에 동일하게 이식하는 작업.

### C. 적용 방식
- **D-03:** **즉시 적용** (설정 플래그로 켜고끄지 않음). 이미 필터링된 다른 함수들(`ComputeLastZIndex`, `AggregateIndexFais` 등)도 게이팅 없이 직접 적용되어 있어 일관성을 위해 동일하게 처리. 신규 설정 항목 추가 없음.

### D. 검증
- **D-04:** **SIMUL_MODE 다중 시퀀스 레시피로 재현 테스트.** Top/Side/Bottom 모두 shot이 있는 레시피에서, Side/Bottom은 이번 세션에 실행하지 않고 Top만 RUN — 수정 전에는 잘못된 NG/NotExist가 나오는지, 수정 후에는 Top 자체 결과대로 정상 판정이 나오는지 육안 확인.
- **D-05:** **구현 완료 후 에이전트 코드 리뷰(`/gsd-code-review`) 필수.** 판정 로직(종합 P/F/N)은 민감한 핵심 코드이므로 SIMUL 재현 테스트에 더해 코드 리뷰 패스를 거쳐야 완료로 간주.

### 코딩 규약
- 기존 필터링된 함수와 동일 패턴 준수: `bool bOwnedByThisSeq = shot.OwnerSequenceName == Name; if (!bOwnedByThisSeq) continue;` — C# 7.2, 삼항연산자 금지(if-else), 헝가리언 표기법(프로젝트 전역 규칙).

### Claude's Discretion
- 3곳 각각의 정확한 필터 삽입 위치/변수명 — 기존 `ComputeLastZIndex`/`AggregateIndexFais`의 코드 스타일을 그대로 따르되, 구현 시 자연스러운 위치 선택.
- SIMUL 재현 테스트에 사용할 구체적 레시피/시나리오 데이터 선택.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### 핵심 수정 대상 코드
- `WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs:342-359` — `ComputeOverallResult` (D-02-1)
- `WPF_Example/Custom/Sequence/Inspection/BatchRunService.cs:78-147` — `HandleFinish` (D-02-2)
- `WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs:119-206` (특히 라인144) — `AddResponse()` v2.6 경로 (D-02-3)

### 참조 패턴 (필터 있음, 그대로 이식)
- `WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs:369-397` — `ComputeLastZIndex` (`bOwnedByThisSeq` 패턴 원본)
- `WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs:1452-1485` — `AggregateIndexFais` (동일 패턴)
- `WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs:237-275` — `HandleRunStartResetResults` (리셋은 이미 시퀀스 스코프 — 판정과의 비대칭이 버그의 근본 원인)

### 관련 실행 경로 (조사로 확정, 참고용)
- `WPF_Example/Custom/SystemHandler.cs:211-224` — `ProcessTest` (TCP v1.0/v2.6 모두 단일 시퀀스만 트리거하는 근거)
- `WPF_Example/Sequence/SequenceHandler.cs:343-353` — `SequenceHandler.Start(TestPacket)` (v2.6 레거시 경로도 단일 시퀀스, 근거)

### 원류 phase 컨텍스트 (히스토리 — D-01 판단 근거)
- `.planning/phases/39-inspection-workflow-e2e-2026-05-29/39-CONTEXT.md` D-03 — 이 판정 계층을 처음 만든 결정. "정책 통일"이지 "샷 합치기" 의도가 아니었음을 확인.
- `.planning/phases/49-protocol-v1-judgment-engine/49-CONTEXT.md` — v1.0 도입 시 v2.6 경로를 "회귀 0"으로 그대로 보존한 배경(AddResponse():122 주석 근거).

### FAI 기본값
- `WPF_Example/Custom/Sequence/Inspection/FAIConfig.cs:94` (`IsPass` 기본값 false) / `:142-146` (`ClearResult()`) — 미측정/리셋 shot이 왜 항상 false인지의 근거.

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `bool bOwnedByThisSeq = shot.OwnerSequenceName == Name; if (!bOwnedByThisSeq) continue;` — 이미 프로젝트 내 여러 곳에 쓰이는 검증된 패턴. 그대로 복사 적용.

### Established Patterns
- `ShotConfig.OwnerSequenceName` — 각 shot이 어느 시퀀스(Top/Side/Bottom) 소유인지 나타내는 필드. 전 프로젝트에서 시퀀스 스코프 필터링의 단일 진실원.

### Integration Points
- `ComputeOverallResult`는 `HandleManualCyclePersist`(수동 RUN 완료 시 OnFinish 구독)가 호출.
- `BatchRunService.HandleFinish`는 `OnFinish` 이벤트 구독으로 배치 사이클마다 호출.
- `AddResponse()`는 `SequenceBase`의 사이클 완료 지점에서 TCP 응답 생성 시 호출(v1.0/v2.6 분기는 `UseProtocolV1` 플래그).

</code_context>

<specifics>
## Specific Ideas

- 사용자가 세션 시작 시 이미 상세 코드 추적(라인 번호, 함수명, 대조군 함수 목록)을 제공 — 이번 discuss는 그 위에 "버그 vs 설계" 판정과 "수정 범위/적용방식/검증방법"만 확정하면 되는 구조였음.
- 사용자가 논의 중 "잘 이해가 안가는데"라고 언급 — 전문용어 없이 구체적 시나리오(카메라 3개, Top만 RUN 눌렀을 때 예시)로 재설명 후 진행. 후속 작업(코드 수정 등) 설명 시에도 같은 톤 유지 권장.
- 사용자가 "에이전트 검토도 꼭 확인해"라고 명시 — SIMUL 재현 테스트에 더해 `/gsd-code-review` 필수 포함(D-05).

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope.

</deferred>

---

*Phase: 70-judgement-scope*
*Context gathered: 2026-08-05*
