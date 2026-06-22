# Phase 49: 제어 프로토콜 v1.0 — P/F/B 판정 엔진 + Datum 빈 응답 + CycleState - Context

**Gathered:** 2026-06-23
**Status:** Ready for planning

<domain>
## Phase Boundary

Phase 48이 만든 RESULT 직렬화(`BuildResultMessageV1` / `MapCycleJudgement`)와 빈 hook(`TestResultPacket.IsBuffer` = "Phase 49가 채울 자리") 위에, **z_index 멀티샷 사이클을 가로지르는 판정 상태 엔진**을 얹는다. 무엇을 B/P/F로 채울지 결정하는 주체를 추가한다 — 중간 Index=B(NG 포함 가능), 마지막 Index에서만 종합 P/F 1회, Datum(Index 0)=빈 응답·실패 시 즉시 F.

**범위 밖:** 실 핸들러 통신 회귀 시험(Phase 50 — PROTO-06), 교차-Z 측정(Z1 보유→Z2 측정, 요구 3-2 — 신규 영역).
</domain>

<decisions>
## Implementation Decisions

### A. 사이클 아키텍처
- **D-01 (A-1):** `$TEST:site,null,z_index@` 1건은 **그 z_index에 매핑된 Shot/FAI 그룹만** 검사한다 (전체 Shot 재검사 금지). 조명전환 멀티샷 모델(BOTTOM Idx1=Type1 / Idx2=Type2)과 일치. z_index↔Shot 매핑은 Phase 48 ResourceMap이 이미 제공.
- **D-02 (A-2):** 멀티 Index를 가로지르는 사이클 NG/상태 누적은 **InspectionSequence 멤버 상태**로 보유한다 (신규 CycleStateMachine 클래스 미도입). 기존 `_failedDatums` / `_datumTransforms`(요청 간 캐시)와 동일 lifecycle에 `m_bCycleHasNG` 등 멤버를 추가. 상태 이중화 위험 회피.

### B. "마지막 Index" 판별
- **D-03:** 응답 B(중간) vs P/F(마지막) 분기 트리거 = **레시피에서 z_index 최댓값 자동 산출**. 그 Site Shot들의 최대 z_index = 마지막 Index. 별도 설정/플래그 불필요, 레시피 단일 진실원. **전제(코드 주석+검증으로 고정): PLC Index Table = 레시피 Shot 구성 일치.** 통신은 Index 번호만 옴(PLC가 Index Table 관리).

### C. Datum(Index 0) 처리
- **D-04 (C-1):** Datum 검출 실패 판정 문자 = **UseProtocolV1 분기에서만 'F'**. 기존 v2.6 경로는 'N'(NotExist) 그대로 유지 → 회귀 0. Phase 48 공존 정책과 일치.
- **D-05 (C-2):** Datum 실패 후 후속 Index skip은 **핸들러 주도**. Vision은 Datum 실패 시 'F' 즉시 응답만 하고, 사이클 상태를 F로 마킹. 핸들러가 P/F 수신 시 다음 Index 미송신(spec "응답 P/F면 종료"). Vision 내부 명시 skip 강제 불필요.
- **D-06:** Index 0(Datum 샷) 정상 시 빈 응답 `RESULT:site;B;0;`. Phase 48의 `FAICount=0 → 빈 항목` 직렬화가 이미 규격 충족. Index 0은 datum 검출만 수행 → 변환을 `_datumTransforms`에 캐시 → Index 1+ 측정이 소비.

### D. enum / 리셋 / CO-48-01
- **D-07 (D-1):** enum 신설 범위 = **`ECycleResult { Buffer, Pass, Fail }` 1개만**. 라이프사이클 상태는 멤버 bool(`m_bCycleHasNG` 등)로 표현 — A-2(멤버 상태) 결정과 일관. CycleState 라이프사이클 enum 미도입(의미 중복 회피). PROTO-05 enum 요구 충족.
- **D-08 (D-2):** 사이클 상태 자동 리셋 시점 = **$TEST z_index=0(Index 0) 수신 시 = 사이클 시작**. 비정상 종료(중단 F) 후에도 다음 사이클 시작이 항상 깨끗한 슬레이트. 마지막 Index 송신 후 리셋(누락 위험) 미채택.
- **D-09 (D-3):** **CO-48-01 흡수** — Phase 48 review CR-01: `TcpServer.EncodingType` static 필드 → instance 필드. 다중 인스턴스 전역 인코딩 오염 시한폭탄 제거. 제어 코드 터치하는 김에 동반 정리. 인코딩 동작 회귀 0.

### 코딩 규약 (LOCKED — 제어 코드 전체)
- **D-10:** 본 phase 신규/수정 제어 코드는 `control-sequence-coding-guideline.md` 준수: 헝가리언 접두사(b/n/f/d/sz/p, 멤버 m_, 전역 g_) + `if/else if/else`만(삼항 `?:` / null병합 `??` 금지) + 조건식 변수화 + 매직넘버 enum/상수화 + 함수 30줄 초과 분리. CLAUDE.md "파일 스타일 따르기"보다 **우선**.

### Claude's Discretion
- `ECycleResult`↔`TestResultPacket.IsBuffer`/`Result` 매핑 코드 위치 (InspectionSequence vs VisionResponsePacket 헬퍼) — `MapCycleJudgement`(48-03) 소비 형태는 planner 재량.
- 멤버 상태 필드 정확한 이름/개수 (`m_bCycleHasNG`, `m_nCurrentZIndex`, `m_nLastZIndex` 등) — 코딩지침 헝가리언 준수 하에 planner 재량.
</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### 프로토콜 규격 (canonical spec)
- `.planning/refs/Vision-Protocol-v1.0.md` — §검사 시퀀스(Index 기반 멀티샷) / §판정(P/F/B 3-state, 발생 시점·PLC 동작) / §RESULT 포맷 / §Index Table(Site별 Idx0 Datum~마지막 Idx). **Phase 49 판정 규칙의 단일 진실원.**
- `.planning/refs/control-sequence-coding-guideline.md` — **LOCKED 코딩지침**. 헝가리언/if-else/함수분리. 제어 코드 작성 시 CLAUDE.md보다 우선 (D-10).

### Phase 48 토대 (Phase 49가 소비)
- `.planning/phases/48-protocol-v1-test-result-site-material/48-03-SUMMARY.md` — `BuildResultMessageV1` / `MapCycleJudgement`(IsBuffer 최우선) / `BuildFaiItemsV1` / `TestResultPacket.IsBuffer` hook("Phase 49가 채울 자리") / RESULT 3단 구분자 상수.
- `.planning/phases/48-protocol-v1-test-result-site-material/48-REVIEW.md` — **CR-01 = CO-48-01** (EncodingType static→instance, D-09 흡수 대상). 수정 가이드 코드 포함.
- `.planning/phases/48-protocol-v1-test-result-site-material/48-CONTEXT.md` — UseProtocolV1/PcRole 공존 분기 정책 (D-04 근거).

### Phase 39 토대 (3분기 + NG 누적)
- `.planning/phases/39-inspection-workflow-e2e-2026-05-29/39-CONTEXT.md` — sequence OK/NG/검출실패 3분기 + 사이클 NG 누적 계층(`anyDatumSkip > NG > OK`). Phase 49가 Index 사이클로 확장.
</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs:86` `AddResponse()` — 현재 $TEST 1건 → 전체 Shot 루프 → 종합 P/F/N 1건(`anyDatumSkip > !allPass > OK` 계층) + CycleResultSerializer 영속화. **Phase 49가 Index-scoped + 사이클 누적으로 확장할 핵심 진입점.**
- `InspectionSequence._datumTransforms` / `_failedDatums` — datum 변환·검출실패를 **요청 간 캐시**하는 기존 멤버. D-06(Index 0 검출→Index 1+ 소비), D-02(사이클 상태 멤버) 가 그대로 활용.
- `VisionResponsePacket.BuildResultMessageV1` / `MapCycleJudgement` / `BuildFaiItemsV1` (Phase 48, 48-03) — IsBuffer 최우선 → B, Result==OK → P, 그 외 → F. **직렬화 완성됨 — Phase 49는 IsBuffer/Result를 채우기만.**
- `TestResultPacket.IsBuffer` — Phase 48이 명시적으로 남긴 빈 hook (기본 false → 항상 P/F). Phase 49 엔진이 중간 Index에 true 설정.

### Established Patterns
- ResourceMap z_index↔Shot 매핑 (Phase 48) — D-01 검사 범위 스코핑의 기반.
- `EVisionResultType` (OK/NG/NotExist) + `TestResultPacket.GetResultString` 자동 매핑 — D-04 'N' vs 'F' 분기 지점.
- TCP dispatch: `Custom/SystemHandler.cs:19`(PopResponse drain) / `:235` `ProcessTest` / `InspectionSequence.AddResponse`→`ResponseQueue.Enqueue`.

### Integration Points
- `WPF_Example/TcpServer/TcpServer.cs:76` `EncodingType` static 필드 — **CO-48-01(D-09) 수정 대상** → instance 필드 + `ApplyEncoding` static→instance.
- `UseProtocolV1` 플래그(SystemSetting, Phase 48) — D-04 분기 가드.
</code_context>

<specifics>
## Specific Ideas

- 판정 흐름 골격(자재 1개): Index 0 수신→리셋(D-08)+datum 검출(실패 시 즉시 F, D-04)→빈 B(D-06) / 중간 Index→해당 Shot 측정+NG면 `m_bCycleHasNG=true`(D-02)+응답 B / 마지막 Index(z_index 최댓값, D-03)→해당 Shot 측정+`m_bCycleHasNG` 반영 종합 P/F 1회.
- 핵심 불변식: "NG 발견돼도 마지막 Index까지 측정 진행"(데이터 수집) — 중간 Index에서 NG가 나도 응답은 B, 종료는 마지막 Index에서만.
</specifics>

<deferred>
## Deferred Ideas

- **교차-Z 측정 (요구 3-2)**: Z1 정보 보유→Z2에서 측정하는 교차-Z 상태 머신 — 시퀀스 엔진 확장 필요. 별도 신규 영역(Phase 49 범위 밖).
- **PROTO-06 통신 회귀 시험**: 제어팀(김민우 선임) 동기화 후 실 핸들러 통신 회귀 — Phase 50.
- **분단위 데이터 저장 (요구 4)**: RawImageSaveService 일별→분단위 — SMALL, 별도.
- `TcpServer.cs` Header/Trailer static 필드(EncodingType와 동일 문제) — Phase 48 이전부터 존재, CO-48-01 범위 밖(기록만).
</deferred>

---

*Phase: 49-protocol-v1-judgment-engine*
*Context gathered: 2026-06-23*
