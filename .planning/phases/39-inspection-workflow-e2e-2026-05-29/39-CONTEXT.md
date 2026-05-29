# Phase 39: 검사 워크플로우 E2E - Context

**Gathered:** 2026-05-29
**Status:** Ready for planning

<domain>
## Phase Boundary

Datum 검출 → FAI 측정 → 결과 처리(OK / NG / 검출 실패)까지 1 사이클이 SIMUL 모드에서 끊김 없이 통과하고, 3분기 후속 동작 명세를 현행 v2.6 프로토콜 기준으로 적용. Top/Side/Bottom 멀티샷 시퀀스 모두 동일 정책으로 통일.

**핵심 충돌점:** Phase 37 D-37-03(datum 실패 시 lenient continue+log+identity transform) ↔ WF-01 명세(datum 실패 → 후속 FAI skip + 검출실패 분기). 본 phase가 해소.

**Out of scope:** 실HW [STARTUP] 재측정(Phase 44 CO-38-04), v2.7 프로토콜(Phase 48/49 PROTO-01~05), 결과 분석/Export(Phase 40/41 OUT-01~04).

</domain>

<decisions>
## Implementation Decisions

### Area 1 — Datum 검출 실패 분기 정책

- **D-01:** **Per-FAI gate** 채택. `Measurement.DatumRef` 기준, 해당 datum이 실패한 FAI/Measurement만 skip+NG mark. 다른 datum 기반 FAI는 정상 측정 진행. `_datumTransforms` dictionary가 이미 per-datum transform을 보관하므로 lookup miss(또는 별도 "failed" flag) 기준으로 게이트.
- **D-02:** **검출실패 subtype 구분.** `FAI.IsPass=false` + `MeasuredValue=0` 유지(회귀 0). 추가 플래그(예: `Measurement.LastSkipReason` string 또는 `EMeasureStatus` enum — 구현 시 결정)로 측정 NG와 datum-skip을 구분. UI/Excel export에서 식별 가능해야 함.
- **D-03:** **사이클 종합 판정 계층 = 검출실패 > NG > OK.** datum-skip FAI 1건이라도 있으면 사이클 = 검출실패. 아니면 측정 NG 1건이라도 있으면 NG. 아니면 OK. `InspectionSequence.AddResponse`(L68~97)에서 적용.
- **D-04:** **로깅 + UI 표시.** Phase 37 Logging.PrintLog(ELogType.Error) 유지. MainView Datum overlay에 'DETECT FAIL' 라벨 + InspectionListView Datum 노드 적색 배지 추가. PropertyGrid 결과행 비교용.

### Area 2 — TCP 결과 코드 매핑 (v2.6 wire 유지)

- **D-05:** **사이클 결과 = `EVisionResultType.NotExist` 재사용** ('N' 코드). enum/wire 포맷 신규 추가 없음. 의미적 일치(NotExist = "지정 위치 없음" ≈ datum 미검출). `responsePacket.Result = (anyDatumSkip ? NotExist : !allPass ? NG : OK)` 형태로 1라인 변경.
- **D-06:** **FAIResults[i] = P / F / N 3-state.** 정상 측정 PASS='P', 측정 실패='F', datum-skip='N'. `FAIResultData` 시그니처에 ResultCode 필드 추가(또는 IsPass bool을 EVisionResultType-like로 확장). wire 포맷은 기존 TEST_RESULT_* 상수 재사용.

### Area 3 — FAI 측정 실패 처리 + NG 누적

- **D-07:** **현행 코드 유지 + 명문화/UAT 추가.** `EStep.Measure` 루프(L154~263)의 try/catch lenient 정책 유지. PLAN.md `must_haves`에 "측정 실패 시 다음 FAI 계속 진행, allPass=false 누적" 명문화. UAT에 NG 2건 이상 누적 시나리오 케이스 추가. **임계 조기 종료 도입 안함.**
- **D-08:** **TCP 메타는 FAIResults 표현 레벨에서 끝.** `TestResultPacket`에 신규 ngCount/detectFailCount footer 필드 추가 없음. 'N'/'F' 개별 코드로 원인 자명. wire 포맷 증가 0.

### Area 4 — 검증 범위 + v2.7 경계

- **D-09:** **Phase 39 sign-off = SIMUL UAT 통과.** Cal_Image/ 파일셋으로 Top/Side/Bottom 멀티샷 사이클 3분기(OK/NG/검출실패) UAT 통과로 완료. 실카메라 검증은 Phase 44 CO-38-04로 분리. POC 일정 안전 우선.
- **D-10:** **v2.6 순수 유지.** `CycleState` / `ECycleResult { P, F, B }` / z_index 등 v2.7 용어 도입 안함. Phase 48/49가 별도 도입. 단, NotExist 재사용은 v2.6 enum 내 기존 값이므로 무관.

### Claude's Discretion

- LastSkipReason 필드 타입(string vs 신규 enum) — 구현 시 patterns/회귀 고려해 결정.
- UI 적색 배지/DETECT FAIL 라벨의 시각 디테일(색상값, 폰트, 위치).
- UAT 케이스 정확한 입력 데이터(어떤 Cal_Image 파일로 datum 실패 케이스를 만들지).
- FAIResultData 시그니처 변경 vs 신규 필드 추가 방식 — wire 포맷 회귀 가드 우선.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### 명세 / 요구사항
- `.planning/ROADMAP.md` §Phase 39 — Goal / Scope / Success Criteria
- `.planning/REQUIREMENTS.md` — WF-01, WF-02 정의

### 핵심 수정 대상 코드
- `WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs` §EStep.DatumPhase (L80-121) — D-01 per-FAI gate 진입점, Phase 37 lenient 코드 유지 + 실패 datum flag 기록
- `WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs` §EStep.Measure (L154-263) — D-01 gate 적용 위치, D-02 LastSkipReason 기록
- `WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs` §AddResponse (L68-97) — D-03 3-state 계층 판정, D-05/D-06 ResultCode 매핑
- `WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs` §InspectionSequenceContext (L17-37) — D-03 cycle 상태 carry
- `WPF_Example/TcpServer/VisionResponsePacket.cs` §EVisionResultType (L22-28) — D-05 NotExist 재사용 (변경 없음, 참조용)
- `WPF_Example/TcpServer/VisionResponsePacket.cs` §TestResultPacket Result/FAIResults (L495 부근) + §TEST_RESULT_NOTEXIST="N" (L50) — D-06 FAIResults 시그니처 변경 대상
- `WPF_Example/Custom/Sequence/Inspection/FAIConfig.cs` — D-02 LastSkipReason 필드 추가 후보 위치
- `WPF_Example/Custom/Sequence/Inspection/MeasurementBase.cs` — D-02 대안 위치
- `WPF_Example/Custom/SystemHandler.cs` (L165, 170, 175, 179) — 결과 전달 경로, D-05 ResultCode 흐름 검증

### UI 변경 (D-04)
- `WPF_Example/UI/ContentItem/MainView.xaml.cs` — Datum overlay 'DETECT FAIL' 라벨 추가 위치
- `WPF_Example/Halcon/Services/HalconDisplayService.cs` (RenderDatumOverlay 부근) — overlay 라벨 렌더
- `WPF_Example/UI/ContentItem/InspectionListView.xaml.cs` — Datum 노드 적색 배지 표시

### 직전 phase 정책 충돌점 (히스토리)
- `.planning/phases/37-side-multi-datum-dualimage-2026-05-28/37-CONTEXT.md` — Phase 37 D-37-03 lenient 결정. 본 phase가 per-FAI gate로 해소(전면 회귀 아님).

### 검증 자산
- `Cal_Image/` 폴더 — SIMUL UAT 입력 이미지 셋
- `Cal_Image/DualImageTest/SIDE1_*` — Side multi-datum UAT 자산 (Phase 37 carry-over)

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- **`InspectionSequence._datumTransforms` (Dictionary<string, HTuple>)** — per-datum transform cache. lookup miss를 per-FAI gate의 검출실패 신호로 직접 사용 가능 (D-01).
- **`InspectionSequence.TryGetDatumTransform(meas.DatumRef, out transform)`** (L166 부근에서 이미 사용) — 현재 miss 시 identity fallback. D-01 적용 시 fallback 대신 skip 분기.
- **`EVisionResultType` enum** + **TEST_RESULT_NOTEXIST="N"** wire 상수 — D-05 추가 작업 없이 재사용.
- **Phase 37 per-datum loop 구조** (Action_FAIMeasurement L82-117) — 그대로 두고 datum 실패 시 `_datumTransforms` 미저장 (또는 별도 `_failedDatums` set) 만으로 gate 신호 충분.

### Established Patterns
- **try/catch lenient 정책** (L212-219, L88-100) — D-07 그대로 유지. NG/예외 누적 후 EStep.End에서 종합.
- **`pMyContext.AllPass` 누적** (L258) — bool 1개 → 3-state(예: `EVisionResultType cycleResult`) 1라인 확장으로 D-03 충족.
- **메모리 stale 방지 패턴** — Phase 22 IMG-02, Phase 37 hotfix B (e.Source 비대칭) — UI 갱신 시 동일 패턴 적용 (D-04).
- **마커 주석** — `//260529 hbk Phase 39 WF-01/WF-02 ...` 컨벤션 (memory: feedback_comment_convention).

### Integration Points
- **`InspectionSequence.AddResponse()`** — 사이클 종합 판정 단일 지점 (D-03/D-05 적용).
- **`Action_FAIMeasurement.EStep.Measure` 루프 시작부** (L160 부근, foreach fai in ShotParam.FAIList) — per-FAI gate 게이팅 라인 추가 위치 (D-01).
- **`MeasurementBase.ClearResult()`** — D-02 LastSkipReason 동시 리셋 필요.
- **TestResultPacket 직렬화 경로** — D-06 적용 시 FAIResultData 시그니처 영향, 와이어 회귀 가드 필수.

### Anti-patterns / Landmines
- **Phase 37 hotfix B 패턴**: 새 Shot/Datum 추가 시 라이브 Action(pSeq)와 RecipeManager.Shots 비동기화. 본 phase 신규 필드 추가 시 직렬화/UI 동기화 양쪽 확인 필수.
- **Phase 22 IMG-02**: TeachingImagePath와 SimulImagePath 역할 분리. D-01 구현 중 이미지 경로 잘못 참조 금지.
- **Phase 20 D-12 marker stacking**: 기존 마커 보존, 위에 누적 append.

</code_context>

<specifics>
## Specific Ideas

- Phase 37 lenient는 "다른 datum이 살아있어도 측정 진행" 의도가 옳음 — 본 phase의 per-FAI gate는 그 의도를 명시적으로 **per-FAI 차원**으로 끌어내림.
- SIMUL 검증: Cal_Image/DualImageTest/SIDE1_3-1_Datum_* 파일들이 이미 작업 디렉토리에 있음 (git status). UAT 자산으로 활용.
- 'N' 코드 재사용 결정의 근거: 제어 측 코드가 이미 "OK / NG / NotExist" 3분기를 파싱 가능 (`TEST_RESULT_NOTEXIST = "N"`). v2.7 PROTO-01 가 도입되어도 'N' 의미와 충돌 없음.
- Per-FAI gate 구현 옵션 후보(planner가 결정):
  - 옵션 A: `_failedDatums HashSet<string>` 추가, DatumPhase에서 실패 datum.DatumName 기록, Measure 루프에서 contains 체크.
  - 옵션 B: `_datumTransforms`에 실패도 항상 저장(failed flag tuple 동봉), TryGetDatumTransform이 (transform, succeeded) 반환.
  - 옵션 C: 별도 메서드 `IsDatumFailed(string datumRef)` 추가.

</specifics>

<deferred>
## Deferred Ideas

- **EVisionResultType.DetectFail 신규 enum** — v2.7 PROTO 차원에서 별도 검토. v2.6 호환을 위해 본 phase에서 도입 안함.
- **PacketFooter ngCount / detectFailCount 요약 필드** — POC 시연 이후 Phase 48에서 검토. 현 phase는 FAIResults[i] 표현 레벨로 충분.
- **NG 임계 조기 종료** (예: 50% 실패 시 EStep.End jump) — 사이클 시간 절약 가능하나, NG 입증 데이터 손실 우려로 POC 이후 결정.
- **실HW [STARTUP] 재측정** — Phase 44 CO-38-04에서 처리.
- **결과 이미지 리뷰어 / 엑셀 export** — Phase 40/41 OUT-01~04에서 처리. 단, 본 phase에서 'N'/'F' 코드 + LastSkipReason을 일관되게 남겨두어 Phase 40/41 입력 데이터로 사용 가능하게 함.
- **CycleState / ECycleResult enum + 자동 리셋** — Phase 49 PROTO-03~05에서 도입.

</deferred>

---

*Phase: 39-inspection-workflow-e2e-2026-05-29*
*Context gathered: 2026-05-29 (4 areas discussed, 10 decisions locked)*
