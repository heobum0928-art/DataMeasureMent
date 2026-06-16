# Phase 51: 시퀀스 일괄 검사 & 일괄 Export - Context

**Gathered:** 2026-06-16
**Status:** Ready for planning

<domain>
## Phase Boundary

샷 노드를 개별 트리거하는 현재 검사 방식을, 트리에서 **다중 선택한 SHOT들을 한 번에(일괄) 실행**하고 전 결과를 누적하여 **엑셀로 일괄 추출**할 수 있게 한다. POC 2026-06-30 산출물(Top/Bottom 시퀀스 전체 측정값 일괄 엑셀)을 충족하는 것이 목표.

알고리즘 신규 없음 — 기존 일괄 실행/DTO/Export 인프라(`StartAll`, `CycleResultSerializer.BuildDto`, `ExcelExportService.Export`)를 다중 선택 SHOT 실행 UX로 묶고, SIMUL/실모드 동작과 수동 Export 경로를 정의하는 작업.

</domain>

<decisions>
## Implementation Decisions

### 실행 트리거 & 선택 범위
- **D-01:** 검사 트리거는 트리에서 **SHOT 다중 선택 → 선택된 SHOT만 일괄 실행**. 전부 선택 시 전부, 2개 선택 시 2개만. 현재 InspectionListView 의 단일 노드 "Start" 방식을 다중 선택 실행으로 확장.
- **D-02:** 다중 선택은 **한 시퀀스 내에서만** 허용 (Top끼리 / Bottom끼리). Top+Bottom 교차 선택은 불가 (deferred). 선택 SHOT 전체가 동일 시퀀스 소속이어야 실행.

### SIMUL vs 실모드 동작
- **D-03:** SIMUL 모드 = 선택 SHOT들이 각자의 `SimulImagePath` 로 1회씩 일괄 실행된 뒤 결과 누적.
- **D-04:** 실 모드 = 검사할 때마다(검사 사이클마다) 결과 + 엑셀 데이터에 append(채우는 방식). SIMUL 처럼 "한 번에 전부"가 아니라 검사가 발생할 때마다 누적.

### Export 시점/방식 (Phase 51 범위 = 수동 모드)
- **D-05:** 수동 모드 = 일괄 검사 결과를 누적만 하고, 사용자가 **수동 Export 버튼**으로 누적분을 xlsx 추출.
- **D-06:** 엑셀 레이아웃 = 기존 **Phase 40 `ExcelExportService` 포맷 그대로 재사용** — 전 SHOT/FAI/측정이 한 시트에 행으로, SHOT 이름 컬럼으로 구분. 신규 컬럼/시트 분리 없음.

### Gage R&R 범위 분리 (중요)
- **D-07:** Gage R&R 모드(N회 반복하며 매회 값 채움 + 자동 저장, 실모드)는 **Phase 41.1 (반복도/Gage R&R, 현재 deferred)** 영역. Phase 51 에서는 구현하지 않는다.
- **D-08:** 단, Phase 51 의 **결과 누적 + Export 경로를 Gage R&R(41.1)가 그대로 재사용**할 수 있도록 설계한다 (RepeatRunService 의 누적 패턴과 정합). 코드 중복 0 지향.

### Claude's Discretion (planner/researcher 결정)
- 다중 선택 UI 구현 방식 (트리 체크박스 vs Ctrl/Shift 다중선택) — InspectionListView 기존 패턴에 맞춰 결정.
- "선택 SHOT만 실행" 메커니즘 — 현재 `StartAll` 은 시퀀스 전 SHOT 실행. 선택 SHOT만 돌리려면 필터/부분 실행 경로 필요 (StartAll 확장 vs 선택 인덱스 집합 전달).
- 수동 Export 버튼 위치 — 기존 ReviewerWindow Export(단일 사이클) 재사용 vs InspectionListView 신규 버튼.
- 누적 단위(검사 1회 = 1 CycleResultDto) 와 다중 SHOT 부분 실행 시 DTO 구성 방식.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Roadmap / 요구사항
- `.planning/ROADMAP.md` — Phase 51 항목 (Goal/Scope/Success, BATCH-01)
- `.planning/ROADMAP.md` — Phase 40 (Export I) / Phase 41.1 (Export II — 반복도/Gage R&R, deferred) 인접 범위

### 재사용 코드 (필독)
- `WPF_Example/Custom/Sequence/Inspection/RepeatRunService.cs` — 시퀀스 반복 실행 + 누적 패턴 (StartAll + BuildDto + SaveAsync, Gage R&R 재사용 베이스)
- `WPF_Example/Custom/Sequence/Inspection/CycleResultSerializer.cs` — `BuildDto(recipeManager, resultType, …)` 전 SHOT DTO 생성
- `WPF_Example/Custom/Export/ExcelExportService.cs` — `Export(cycle, path)` xlsx 생성 (기존 포맷, D-06 재사용 대상)
- `WPF_Example/UI/Reviewer/ReviewerWindow.xaml.cs` — 단일 사이클 Export(L308) + 반복 Export(L433) 기존 트리거
- `WPF_Example/UI/ControlItem/InspectionListView.xaml.cs` — `Btn_start_Click` / `ResolveRunnableAction` (현재 단일 노드 실행), 트리 다중 선택 추가 지점
- `WPF_Example/Sequence/Sequence/SequenceBase.cs` — `StartAll(packet)` (L342, 전 SHOT 1 사이클 실행)

[외부 spec/ADR 없음 — 결정은 위 코드 자산 + 본 CONTEXT 에 캡처됨]

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `SequenceBase.StartAll(packet)`: 시퀀스 전 SHOT 1 사이클 실행. 선택 SHOT만 실행하려면 필터 경로 필요.
- `CycleResultSerializer.BuildDto(recipeManager, resultType, DateTime, recipeName, seqName)`: 전 SHOT/FAI 결과를 1 CycleResultDto 로 직렬화.
- `ExcelExportService.Export(cycle, path)`: CycleResultDto → xlsx (D-06 그대로 재사용).
- `RepeatRunService`: OnFinish 구독 → HandleFinish 에서 BuildDto + 누적 (`_collected`) + 진행률 이벤트. Gage R&R(41.1)/일괄 누적의 베이스 패턴.
- `RecipeManager.Shots`: SHOT 목록 (선택 대상).

### Established Patterns
- 검사 실행: `SequenceHandler.Start(seqID, actID)` (단일/부분) vs `StartAll` (전 SHOT). TCP 경로는 StartAll 사용.
- 결과 누적: OnFinish 이벤트 → recipeManager 순회로 OK/NG/NotExist 산출 → BuildDto.
- Export: ReviewerWindow 에서 `_currentCycle` → ExcelExportService.Export(파일 다이얼로그).

### Integration Points
- InspectionListView 트리: SHOT 다중 선택 UI + "선택 SHOT 일괄 검사" 실행.
- 수동 Export 버튼: ReviewerWindow Export 재사용 또는 InspectionListView 신규.
- 누적 저장 경로: RepeatRunService 패턴 재사용 (Gage R&R 41.1 와 공유).

</code_context>

<specifics>
## Specific Ideas

- 사용자 표현: "shot 별로 검사하는 거 말고 한번에 Top이면 Top Bottom이면 Bottom 한번에 데이터 나오게" → 다중 선택 SHOT 일괄 실행 + 일괄 엑셀.
- "수동 모드 = 누적 후 수동 Export / Gage R&R 모드 = 매회 채움 + 자동 저장(실모드)" — 두 운용 모드 구분. Phase 51 = 수동 모드, Gage R&R = 41.1.

</specifics>

<deferred>
## Deferred Ideas

- **Gage R&R 모드 (N회 반복 + 매회 append + 자동 저장)** → Phase 41.1 (반복도/Gage R&R). Phase 51 누적/Export 경로 재사용.
- **Top+Bottom 교차 다중 선택** → 보류 (D-02 로 한 시퀀스 내로 한정). 필요 시 향후 phase.

</deferred>

---

*Phase: 51-export-2026-06-16-poc-3*
*Context gathered: 2026-06-16*
