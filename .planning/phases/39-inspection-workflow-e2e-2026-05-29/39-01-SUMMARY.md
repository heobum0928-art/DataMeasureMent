---
phase: 39-inspection-workflow-e2e-2026-05-29
plan: 01
type: summary
status: complete
date: 2026-05-29
commits: [931c93b, a1211f0, fd2b827]
files_modified:
  - WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs
  - WPF_Example/Custom/Sequence/Inspection/FAIConfig.cs
  - WPF_Example/Custom/Sequence/Inspection/MeasurementBase.cs
  - WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs
requirements_addressed: [WF-01, WF-02]
---

# Plan 39-01 SUMMARY — Datum per-FAI gate foundation

## Output

Wave 1 foundation 4 파일 / 3 commits / 57 insertions(+).

### 신규 인터페이스 5종

| 위치 | 멤버 | 시그니처 | 용도 |
|------|------|----------|------|
| InspectionSequence | `_failedDatums` | `private readonly HashSet<string>` | datum 검출 실패 이름 집합 (per-FAI gate 신호) |
| InspectionSequence | `MarkDatumFailed(string)` | `public void` | DatumPhase 실패 분기에서 호출, idempotent add |
| InspectionSequence | `IsDatumFailed(string)` | `public bool` | Measure 게이트에서 호출, null/empty → false |
| FAIConfig | `WasDatumSkipped` | `[Browsable(false)] public bool` | AddResponse cycle aggregation 입력 (NotExist 매핑) |
| MeasurementBase | `LastSkipReason` | `[Browsable(false)] public string` | UI 'DETECT FAIL' 라벨 + Excel export 분기 |

### 동작 흐름

1. **EStep.DatumPhase** (Action_FAIMeasurement L80-121):
   - DualImage 이미지 취득 실패 → `MarkDatumFailed` + continue
   - DualImage 검출 실패 (TryRunSingleDatum) → `MarkDatumFailed`
   - 1-image 이미지 취득 실패 → `MarkDatumFailed` + continue
   - 1-image 검출 실패 (TryRunSingleDatum) → `MarkDatumFailed`

2. **EStep.Measure** (Action_FAIMeasurement L164-178 — 게이트 블록 신규):
   - `IsDatumFailed(meas.DatumRef)` true → skip 분기:
     - `meas.ClearResult()` (LastSkipReason=null 포함, MeasurementBase Task 2)
     - `meas.LastSkipReason = "DATUM_FAIL"`
     - `meas.LastJudgement = false`
     - Logging.PrintLog (Error)
     - `faiAllPass = false; measuredCount++; continue;`

3. **FAI 집계** (Action_FAIMeasurement L259-265):
   - `fai.Measurements` 중 1건이라도 `LastSkipReason == "DATUM_FAIL"` → `fai.WasDatumSkipped = true`
   - System.Linq 미도입 (for-loop, CLAUDE.md C# 7.2 컨벤션)

4. **ClearAllResults cascade** (Action_FAIMeasurement L61 → ShotParam → FAIConfig → MeasurementBase):
   - `FAIConfig.ClearResult` → `WasDatumSkipped = false` (Task 2)
   - `MeasurementBase.ClearResult` → `LastSkipReason = null` (Task 2)

5. **ClearDatumTransforms** (InspectionSequence L194-198):
   - `_datumTransforms.Clear()` + `_failedDatums.Clear()` 동일 lifecycle

## Phase 37 D-37-03 lenient 회귀 0 증거

| 보존 항목 | 위치 | 검증 |
|-----------|------|------|
| `Step = (int)EStep.Grab;` | Action_FAIMeasurement.cs:123 | grep 매치, datum 부분 실패해도 측정 진행 |
| `try/catch lenient` (Measurement TryExecute) | Action_FAIMeasurement.cs:212-219 | 변경 0, 첫 NG 후에도 다음 meas 진행 |
| `identity fallback` | Action_FAIMeasurement.cs:179-187 | 변경 0 (line 번호는 게이트 블록 삽입 후 갱신) |
| `TryGetDatumTransform` 시그니처 | InspectionSequence.cs:233-239 | 변경 0, grep 매치 |
| `TryRunDatumPhase` 두 오버로드 | InspectionSequence.cs:146-189 | 변경 0 |
| `TryRunSingleDatum` | InspectionSequence.cs:215-230 | 변경 0 |
| Phase 17 D-13 `RenderDatumFindResult` | HalconDisplayService.cs (Plan 03 영역) | 본 plan 무수정 |
| Phase 7-02 overlay suffix | Action_FAIMeasurement.cs:229-238 | 변경 0 |
| Phase 31 D-03 IDatumOriginConsumer | Action_FAIMeasurement.cs:177-207 | 변경 0 |

## Plan 02/03 이 사용할 인터페이스

### Plan 02 (TCP wire)
- `fai.WasDatumSkipped` → AddResponse anyDatumSkip 누적
- `EVisionResultType.NotExist` 매핑 (기존 v2.6 enum 재사용 — D-10 가드)
- 신규 ctor `FAIResultData(string, EVisionResultType, double)` 호출

### Plan 03 (UI overlay)
- `meas.LastSkipReason == "DATUM_FAIL"` (string compare) → UI 'DETECT FAIL' 라벨 분기
- `datum.LastFindSucceeded` (기존, Phase 37) → overlay 라벨 게이트
- DatumConfig INPC + HasDetectFail computed (Plan 03 신규)

## Build verification

| Commit | Files | Insertions | msbuild |
|--------|-------|-----------|---------|
| 931c93b | InspectionSequence.cs | +18 | PASS (errors 0, warnings 베이스라인) |
| a1211f0 | FAIConfig.cs + MeasurementBase.cs | +14 | PASS |
| fd2b827 | Action_FAIMeasurement.cs | +25 | PASS |

신규 warning 0. 기존 마커 100% 보존 (260413 Phase 6 / 260422 Phase 7 / 260517 CO-23-01 / 260519 Phase 31 / 260521 Phase 32 / 260526 CO-31-01 / 260528 Phase 37).
