---
phase: quick-260908-jzs
plan: 01
subsystem: inspection-review
tags: [reviewer, cycle-json, skip-reason, ui]
dependency-graph:
  requires: []
  provides:
    - SkipReason.MEASURE_FAIL
    - MeasurementBase.LastErrorMessage
    - CycleResultDto.TickJudgement / MeasuredShotNames / ZIndex
    - ReviewerListLabelBuilder
  affects:
    - ReviewerWindow (좌측 cycle 목록, 우측 측정표, 불량만 보기 필터)
    - ExcelExportService (판정 칸 텍스트)
tech-stack:
  added: []
  patterns:
    - "public 필드(프로퍼티 아님)로 INI 직렬화 대상에서 제외"
    - "optional 파라미터로 기존 호출부 무수정 컴파일 보장"
key-files:
  created: []
  modified:
    - WPF_Example/Custom/Sequence/Inspection/SkipReason.cs
    - WPF_Example/Custom/Sequence/Inspection/MeasurementBase.cs
    - WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs
    - WPF_Example/Custom/Sequence/Inspection/CycleResultSerializer.cs
    - WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs
    - WPF_Example/UI/ViewModel/CycleResultDto.cs
    - WPF_Example/UI/ViewModel/ReviewMeasurementRow.cs
    - WPF_Example/Custom/Export/ExcelExportService.cs
    - WPF_Example/UI/Reviewer/ReviewerWindow.xaml
    - WPF_Example/UI/Reviewer/ReviewerWindow.xaml.cs
decisions:
  - "LastErrorMessage 는 public 필드로 선언(프로퍼티 아님) — ParamBase.Save/Load 의 GetProperties 순회에서 제외되어 INI 레시피에 새 키가 새지 않는다"
  - "z 전파는 프로토콜 구동 경로(AddResponse, PersistAndEnqueueV1) 2곳만 — 수동 경로는 ParseCurrentZIndex 가 0 을 반환해 '진짜 z=0'과 구별 불가하므로 항상 -1(수동) 유지"
  - "TickJudgement/MeasuredShotNames/ZIndex 는 전부 옵션/기본값 폴백으로 옛 cycle.json 과 완전 호환"
metrics:
  duration: "~40분"
  completed: "2026-09-08"
---

# Phase quick-260908-jzs Plan 01: 리뷰어 NG 라벨 확장 Summary

측정 실패(에지 0개 등)가 "—"(미측정)로 잘못 보이던 버그를 고치고, 리뷰어 좌측 cycle 목록에 시각/z/Shot/불량 항목 사유/사이클 종합을 한 줄로 보여주도록 확장했다.

## Tasks Completed

1. **Task 1 (요구 A)** — 측정 실패 기록/표시. `SkipReason.MEASURE_FAIL` 상수 추가, `MeasurementBase.LastErrorMessage`(public 필드, INI 비직렬화) 추가, `Action_FAIMeasurement.RecordMeasurementResult` 실패 분기에서 `LastSkipReason=MEASURE_FAIL` + 정제된 에러 원문(개행 치환, 200자 절단) 기록. `CycleResultDto`/`CycleResultSerializer`에 `LastErrorMessage` 전파. `ReviewMeasurementRow`/`ExcelExportService` 판정 분기에 "측정실패" 라벨 추가(두 곳 동일 문자열, `ReviewMeasurementRow.JUDGE_MEASURE_FAIL` 상수 공유).
   - 커밋: `42f98d3e`

2. **Task 2 (요구 B)** — tick 판정/측정 Shot/z 메타 필드. `CycleResultDto`에 `TickJudgement`(OK/NG/null)/`MeasuredShotNames`/`ZIndex`(기본 -1) 필드 + `TICK_OK`/`TICK_NG` 상수 추가. `CycleResultSerializer.BuildDto`에 optional `nZIndex` 인자 + `FillTickSummary` 헬퍼(실제 측정된 항목만으로 tick 판정 산출, `OverallJudgement`/`MapJudgement`는 무변경). `InspectionSequence`의 프로토콜 경로 2곳(`AddResponse`, `PersistAndEnqueueV1`)에서 `IsProtocolDrivenCycle()` 게이트 후 `GetExecutionZIndex()`로 z 실효값 전파. 수동 경로(`HandleManualCyclePersist`, `BatchRunService`, `RepeatRunService`)는 무수정.
   - 커밋: `bea09925`

3. **Task 3 (요구 C, D)** — 리뷰어 목록 라벨/색상/필터. `CycleResultDto.cs`에 `ReviewerListLabelBuilder` 정적 클래스(순수 로직) 추가 — `Build(dto)`로 `HH:mm:ss  z=NN  OK/NG  Shot이름 [← 항목(사유) 외 N] [· 종합 NG]` 형태 조립, `IsFailTick(dto)`로 필터용 불량 판정. `ReviewerWindow.xaml.cs`: `CycleListItem.IsNg` 추가, `LoadCycleFolders`가 `ReviewerListLabelBuilder`를 호출하도록 교체(빈 문자열이면 폴더명 폴백), `_allCycleItems` + `ApplyCycleListFilter()`로 좌측 목록 '불량만 보기' 적용(선택 항목 자동 변경 없음), `ApplyRowFilter` 화이트리스트에 "측정실패" 추가. `ReviewerWindow.xaml`: 목록 항목 `IsNg` DataTrigger로 빨간 글자, 측정표 RowStyle에 "측정실패" 배경색 DataTrigger 추가(IsSelected 트리거는 맨 뒤 유지).
   - 커밋: `1b02cca5`

4. **Task 4 (실기 확인)** — **미실행**. 사람만 할 수 있는 checkpoint. 아래 "Manual Verification Required" 참고.

## Manual Verification Required

Task 4 (`checkpoint:human-verify`, gate="blocking")는 실행하지 않았다. 실기에서 아래 절차로 확인 필요:

1. 새 빌드로 프로그램 실행 → BOTTOM 자동 사이클 1회 수행.
2. 결과 리뷰어 → "날짜 폴더 열기" → 오늘 날짜 폴더 선택.
   - 좌측 목록의 각 줄에 `시각  z=NN  OK/NG  SHOT_이름`이 보이는가?
   - NG 줄에 `← 항목명(사유)`가 붙고 글자가 빨간가?
   - 마지막 tick이 tick 자체는 OK인데 사이클 종합이 NG라면 `· 종합 NG`가 보이는가?
3. 에지 검출이 안 되는 항목이 있는 tick을 선택 → 우측 표 판정 칸이 "—"가 아니라 "측정실패"로 나오는가? 엑셀 export 판정 칸도 동일한가?
4. "불량만 보기" 체크 → 좌측 목록도 불량 tick만 남는가? 해제 시 전부 복귀하는가?
5. 예전 날짜 폴더(2026-09-08 이전, 새 필드 없는 cycle.json)를 열어 → 예전처럼 `시각  OK/NG`로 뜨고 크래시/빈 목록이 없는가?
6. 자동 사이클 판정/TCP 응답이 이전과 동일한가(핸들러 쪽 OK/NG 수신 변화 없음).

승인 또는 문제점 보고 시 재개.

## Verification

- 빌드: `MSBuild ... -p:Configuration=Release -p:Platform=x64 -t:Build` — `error CS` 0회, `error MC` 0회 (Task 1/2/3 각 완료 시점마다 확인)
- 하드룰 grep 게이트 (수정 파일 신규 diff 줄 기준, 전부 0):

| 파일 | 삼항(`?:`) | `??` | `?.` | `switch=>` | 신규 `hbk` |
|---|---|---|---|---|---|
| SkipReason.cs | 0 | 0 | 0 | 0 | 0 |
| MeasurementBase.cs | 0 | 0 | 0 | 0 | 0 |
| Action_FAIMeasurement.cs | 0 | 0 | 0 | 0 | 0 |
| CycleResultDto.cs | 0 | 0 | 0 | 0 | 0 |
| CycleResultSerializer.cs | 0 | 0 | 0 | 0 | 0* |
| ReviewMeasurementRow.cs | 0 | 0 | 0 | 0 | 0 |
| ExcelExportService.cs | 0 | 0 | 0 | 0 | 0 |
| InspectionSequence.cs | 0 | 0 | 0 | 0 | 0* |
| ReviewerWindow.xaml.cs | 0 | 0 | 0 | 0 | 0* |

\* grep이 2건 매칭됐으나 확인 결과 전부 **기존 날짜주석 라인의 후행 쉼표/괄호만 바뀐 것**(예: `bIsProtocolDriven)` → `bIsProtocolDriven,`)이며 신규 작성된 주석이 아니다. 새 날짜 주석은 0건.

- `git status`로 `WPF_Example/DatumMeasurement.csproj`가 각 커밋에서 스테이징되지 않았음을 확인(사전 수정 상태 그대로 unstaged 유지).
- 새 `.cs` 파일 0개 — 모든 신규 로직(`ReviewerListLabelBuilder`)은 기존 `CycleResultDto.cs` 파일 내부에 작성.
- 각 태스크 커밋 후 `git diff --diff-filter=D --name-only HEAD~1 HEAD`로 의도치 않은 삭제 없음 확인(3회 모두 빈 결과).

## Deviations from Plan

None - plan executed exactly as written (Task 1~3). Task 4는 계획대로 실행하지 않고 checkpoint로 남김(오케스트레이터 지시).

## Known Stubs

None.

## Threat Flags

None — 이 plan의 threat_model(T-jzs-01~03)에 명시된 mitigation을 모두 그대로 구현했고, 새로운 네트워크/인증/스키마 경계는 추가되지 않았다.

## Self-Check: PASSED

- 수정 파일 10개 전부 `FOUND` 확인.
- 커밋 해시 3개(`42f98d3e`, `bea09925`, `1b02cca5`) 전부 `git log --oneline --all`에서 `FOUND` 확인.
