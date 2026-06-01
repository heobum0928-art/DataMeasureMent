---
phase: 40-export-i-1-2026-06-01
plan: "01"
subsystem: inspection-result-persistence
tags: [json-serialization, cycle-result, dto, file-io, security]
dependency_graph:
  requires: []
  provides:
    - CycleResultDto (cycle 결과 JSON DTO 5종)
    - CycleResultSerializer (BuildDto/SaveAsync/Load 정적 서비스)
    - InspectionSequence.AddResponse cycle 완료 wiring
  affects:
    - WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs
tech_stack:
  added: []
  patterns:
    - Task.Factory.StartNew 비동기 예외격리 (RawImageSaveService 패턴)
    - TypeNameHandling.None JSON 보안 역직렬화
    - DTO 계층 FAIConfig.LastOverlays 복사 ([JsonIgnore] 우회)
key_files:
  created:
    - WPF_Example/UI/ViewModel/CycleResultDto.cs
    - WPF_Example/Custom/Sequence/Inspection/CycleResultSerializer.cs
  modified:
    - WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs
    - WPF_Example/DatumMeasurement.csproj
decisions:
  - "CycleResultSerializer를 ReringProject.Sequence 네임스페이스에 배치 (Custom/Sequence/Inspection 폴더 규칙 일치)"
  - "EVisionResultType → OverallJudgement 3-state 매핑: NotExist=DETECT_FAIL / NG=NG / 그외=OK"
  - "ResultImagePath = ShotConfig.GetLatestImagePath() = SimulImagePath (SaveResultImage 비의존)"
  - "using System; 추가로 Exception 타입 해소 (InspectionSequence.cs 누락 using 보완)"
metrics:
  duration: 264
  completed_date: "2026-06-01"
  tasks_completed: 3
  files_changed: 4
---

# Phase 40 Plan 01: cycle 결과 JSON 영속화 토대 Summary

**한 줄 요약:** Newtonsoft.Json CycleResultDto 5종 DTO + CycleResultSerializer(BuildDto/SaveAsync/Load) + InspectionSequence.AddResponse wiring으로 검사 cycle 완료 시 `Result/{YYYYMMDD}/{HHmmss}_cycle/cycle.json` 자동 생성 달성.

## Completed Tasks

| Task | 이름 | Commit | 핵심 파일 |
|------|------|--------|----------|
| 1 | CycleResultDto JSON 직렬화 DTO 정의 | 9a62091 | CycleResultDto.cs (신규) |
| 2 | CycleResultSerializer 서비스 | 27ee375 | CycleResultSerializer.cs (신규) |
| 3 | InspectionSequence.AddResponse wiring | eed3368 | InspectionSequence.cs (수정) |

## What Was Built

### Task 1: CycleResultDto 5종 DTO (CycleResultDto.cs)

`ReringProject.UI` 네임스페이스에 5개 클래스 정의:
- `CycleResultDto`: InspectionTime / RecipeName / OverallJudgement / CycleFolderPath / Shots
- `ShotResultDto`: ShotName / OwnerSequenceName / ResultImagePath / FAIs
- `FaiResultDto`: FAIName / IsPass / WasDatumSkipped / Measurements / LastOverlays
- `MeasurementResultDto`: 7필드 (MeasurementName/TypeName/NominalValue/TolerancePlus/ToleranceMinus/LastMeasuredValue/LastJudgement/LastHasResult/LastSkipReason)
- `Observable` 상속 없음 — 순수 직렬화 DTO
- `[JsonIgnore]` 사용 없음 — 모든 필드 직렬화 노출
- `EdgeInspectionOverlay` 직렬화 안전 확인 (전 필드 CLR 타입)

### Task 2: CycleResultSerializer 정적 서비스 (CycleResultSerializer.cs)

- `BuildDto()`: recipeManager.Shots 전체 순회, FAIConfig.LastOverlays DTO 복사, MapJudgement 3-state 매핑
- `SaveAsync()`: Task.Factory.StartNew + try/catch 예외격리, `ResultSavePath/{yyyyMMdd}/{HHmmss}_cycle/cycle.json` 저장
- `Load()`: `TypeNameHandling.None` 명시 + try/catch → null 반환 (T-40-02 RCE 방지)
- `using ReringProject.Network;` 추가 (EVisionResultType 해소)

### Task 3: InspectionSequence.AddResponse wiring

삽입 위치: `pMyContext.ResultInfo = responsePacket.Result;` 직후, `ResponseQueue.Enqueue(responsePacket);` 직전

```csharp
try
{
    var cycleDto = CycleResultSerializer.BuildDto(recipeManager, responsePacket.Result, DateTime.Now, SystemHandler.Handle.Setting.CurrentRecipeName);
    CycleResultSerializer.SaveAsync(cycleDto);
}
catch (Exception ex)
{
    try { Logging.PrintErrLog((int)ELogType.Error, "[Phase40] cycle 직렬화 실패(무시): " + ex.Message); } catch { }
}
```

- `using System;` 추가 (Exception 타입 해소)
- Phase 39 WF-02 3-state hierarchy 라인 무수정 (회귀 0)

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] EVisionResultType CS0246 빌드 오류**
- **Found during:** Task 2
- **Issue:** CycleResultSerializer.cs 에서 `EVisionResultType` 참조 시 `using ReringProject.Network;` 누락 → CS0246 빌드 오류
- **Fix:** `using ReringProject.Network;` 추가
- **Files modified:** CycleResultSerializer.cs
- **Commit:** 27ee375

**2. [Rule 3 - Blocking] Exception CS0246 빌드 오류**
- **Found during:** Task 3
- **Issue:** InspectionSequence.cs 에 `using System;` 누락 → `Exception` 타입 CS0246
- **Fix:** `using System;` 추가 (기존 파일에서 누락된 using)
- **Files modified:** InspectionSequence.cs
- **Commit:** eed3368

## Security Controls Applied

| Threat ID | Control | Location |
|-----------|---------|----------|
| T-40-01 | cycleDir = ResultSavePath + DateTime 포맷팅만 사용, 외부 입력 없음 | CycleResultSerializer.SaveAsync |
| T-40-02 | TypeNameHandling.None 명시 + try/catch → null 반환 (RCE 방지) | CycleResultSerializer.Load |
| T-40-03 | BuildDto/SaveAsync를 try/catch로 격리 — TCP 응답 차단 없음 | InspectionSequence.AddResponse |

## Build Verification

- msbuild Debug/x64 PASS (0 errors, 신규 warning 0)
- 기존 warning 7건 (CS0618 × 5, CS0162 × 1, MSB3884 × 1) — 모두 Phase 40 이전부터 존재

## Known Stubs

없음 — 전 필드 실제 데이터 소스에 배선됨.

## Threat Flags

없음 — plan의 threat_model에 포함된 T-40-01/02/03/04 모두 처리됨. 신규 네트워크 엔드포인트/외부 입력 경로 없음.

## Self-Check: PASSED

- [x] WPF_Example/UI/ViewModel/CycleResultDto.cs — FOUND
- [x] WPF_Example/Custom/Sequence/Inspection/CycleResultSerializer.cs — FOUND
- [x] WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs (수정) — FOUND
- [x] commit 9a62091 — FOUND (Task 1)
- [x] commit 27ee375 — FOUND (Task 2)
- [x] commit eed3368 — FOUND (Task 3)
