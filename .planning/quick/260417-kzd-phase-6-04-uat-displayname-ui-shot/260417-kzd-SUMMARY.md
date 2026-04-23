---
id: 260417-kzd
title: Phase 6-04 UAT 잔여 결함 수정 — DisplayName 편집 UI + Shot 실행 경로
date: 2026-04-17
status: completed
uat_verified: 2026-04-22
---

# Summary: Phase 6-04 UAT DisplayName + Shot Start 경로

## 배경
Phase 6-04 Plan 완료 후 UAT에서 두 가지 결함 확인:
- 결함 1: Sequence 노드 선택 시 PropertyGrid에 `DisplayName` 필드가 보이지 않음 (`CameraMasterParam`에 해당 필드 없음)
- 결함 2: Sequence / Shot 노드 선택 → Start 시 "There is no action to run" 또는 엉뚱한 Action 실행

## 변경

### Task 1 — InspectionMasterParam 신규 (커밋 `40ea796`)
- `WPF_Example/Custom/Sequence/Inspection/InspectionMasterParam.cs` 추가
  - `CameraMasterParam` 상속, `DisplayName` 프로퍼티를 `[Category("Fixture|Identity")]`로 노출
  - setter에서 `_insp.DisplayName` 갱신 + `RaisePropertyChanged`
- `InspectionSequence.cs`: 생성자에서 `Param = new InspectionMasterParam(this)`
- `InspectionListViewModel.cs`:
  - `HookSequenceDisplayNameUpdates()` 헬퍼 추가 → `RebuildTree` 말미 호출
  - `OnSequenceMasterPropertyChanged` 핸들러 → DisplayName 변경 시 트리 노드 라벨 즉시 갱신
- `DatumMeasurement.csproj`에 신규 파일 `<Compile Include>` 추가

### Task 2 — Btn_start_Click 재작성 (커밋 `a44debd`)
- `ResolveRunnableAction(NodeViewModel, SequenceBase, out EAction)` 헬퍼 추가
  - Sequence 노드 → `seq[0].ID`
  - Shot 노드(Param=ShotConfig) → `RecipeManager.Shots.IndexOf(shotCfg)` → `seq[shotIdx].ID`
  - 매핑 실패 시 Idle 조건에서 `EnableDynamicFAIMode + RebuildInspectionActions`로 지연 동기화 후 재시도
  - 일반 Action 노드 → `node.ActionID` (기존 경로)
- `Btn_start_Click`: Idle 체크 → Resolve → `StartSequence(seqID, actID)` 단순화
- 구 `GetDefaultRunnableAction` 삭제

### Task 3 — UAT 재검증 후속 수정
UAT 중 발견된 부수 문제를 같은 가지에서 정리:
- `40a7cca fix(06-04)` Start 실패 시 진단 다이얼로그 + `Debug.WriteLine`
- `84b1bfb chore(06-04)` statusBar 진단 메시지 (임시)
- `44523ad fix(06-04)` FAIConfig ↔ Measurement ROI 동기화 (선행 표면 패치)
- `abe8f55 fix(06-04)` 공차 단일 소스화 — `FAIConfig.NominalValue/Upper/LowerTolerance` 제거 (판정은 `MeasurementBase.EvaluateJudgement`만 사용)

## 결과
- Sequence 노드 PropertyGrid에 `Fixture|Identity / DisplayName` 편집 필드 노출
- DisplayName 편집 시 트리 라벨 즉시 반영, 저장/로드 왕복 유지
- Sequence 노드 Start → 첫 Action 실행
- Shot 노드 Start → 해당 Shot의 `Action_FAIMeasurement` 실행 (UI에서 갓 추가한 Shot도 지연 동기화로 동작)
- 실행 중 재클릭 시 "Sequence is already running" 차단

## 검증
- `msbuild Debug/x64` 빌드 성공
- 사용자 UAT: 5개 시나리오 통과 확인 (2026-04-22)
  - DisplayName 표시 / 편집 / 트리 갱신 / 저장-로드 왕복
  - Sequence 노드 Start
  - Shot 노드 Start (로드 상태)
  - UI 추가 Shot Start (지연 동기화)
  - 실행 중 재시작 차단 메시지

## 관련 커밋
- `40ea796`, `a44debd` (Task 1/2 본체)
- `40a7cca`, `84b1bfb`, `44523ad`, `abe8f55` (UAT 후속 정리)
- 병렬 quick `260417-ou8` (`5bfde87` ROI 단일 소스화)도 Phase 6-04 UAT 동일 창구에서 처리됨
