---
phase: 78-reviewer-ng-cause-analysis
plan: 03
subsystem: inspection
tags: [halcon, cycle-json, ng-cause-analysis, diagnostics, datum, z-range]

requires:
  - phase: 78-reviewer-ng-cause-analysis (78-01)
    provides: "NGA-07 DTO 계약(ZCandidateScoreDto/DatumDiagnosticDto/ShotResultDto.ZRangeStartIndex/ZRangeEndIndex/MeasurementResultDto.ZCandidateScores/CycleResultDto.DatumDiagnostics) 선언"
provides:
  - "MeasurementBase.LastZCandidateScores(필드+JsonIgnore) + Action_FAIMeasurement.BuildZCandidateScoreList — Z 후보 선명도 write-back"
  - "CycleResultSerializer.BuildDto 의 ZRangeStartIndex/ZRangeEndIndex(IsZRangeEnabled 가드)·ZCandidateScores 복사"
  - "DatumConfig.LastAlignMatchScore/Row/Col/AngleDeg(필드+JsonIgnore) + TryComposeAlign 리셋·기록"
  - "CycleResultSerializer.BuildDatumDiagnostic(비유한수 ToFiniteOrZero, 신선/묵은 판정) + InspectionSequence.BuildTickDatumDiagnosticsSnapshot(저장 3곳 배선)"
affects: [78-04, 78-05, 78-06, 78-07]

tech-stack:
  added: []
  patterns:
    - "검사 스레드가 이미 계산한 런타임 값을 새 계산 없이 cycle.json DTO 로 복사만 한다 — MeasurementBase/DatumConfig 의 새 필드는 public 필드(프로퍼티 아님) + [Newtonsoft.Json.JsonIgnore] 로 INI·레시피 JSON·붙여넣기에서 제외되고, DatumDiagnosticDto/ZCandidateScoreDto(JsonIgnore 없음)로만 cycle.json 에 노출된다"
    - "BuildTickDatumDiagnosticsSnapshot 은 전체를 try/catch 로 감싸 검사 스레드 밖으로 예외를 내보내지 않는다(HandleFlowLogCycleEnd 와 동일 격리 규약), 저장 3곳 모두 SaveAsync 직전에 1줄 배선"

key-files:
  created: []
  modified:
    - WPF_Example/Custom/Sequence/Inspection/MeasurementBase.cs
    - WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs
    - WPF_Example/Custom/Sequence/Inspection/CycleResultSerializer.cs
    - WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs
    - WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs

key-decisions: []

patterns-established: []

requirements-completed: [NGA-07, NGA-05]

coverage:
  - id: D1
    description: "Z 범위 Shot 의 후보 z 마다 {ZIndex, Ok, Score} 가 cycle.json MeasurementResultDto.ZCandidateScores 에, 검사 당시 ZIndex/ZIndexEnd 가 ShotResultDto.ZRangeStartIndex/ZRangeEndIndex 에 기록된다"
    requirement: "NGA-07"
    verification:
      - kind: other
        ref: "NgCauseProbe.exe <binDir> json <scratchDir> <oldCycleJsonPath> (repo 밖 probe) — json roundtrip=PASS (4후보 ZIndex/Ok/Score 비트 단위 일치, ZRangeStartIndex=3/ZRangeEndIndex=5 왕복)"
        status: pass
    human_judgment: false
  - id: D2
    description: "모든 cycle.json 에 시퀀스 Datum 마다 DatumDiagnostics{DatumName, IsDetected, IsDetectedThisTick, DetectTime, OriginRow/Col, AngleDeg, EdgeCount, FitRmse, AlignMatchScore/Row/Col/AngleDeg, Align2Score, AlignThetaDeg} 가 기록된다"
    requirement: "NGA-07"
    verification:
      - kind: other
        ref: "NgCauseProbe.exe json — datummap fresh/stale/never/null/roundtrip 전부 PASS, json datum_fields=4 datum_jsonignore=4"
        status: pass
    human_judgment: false
  - id: D3
    description: "검사 판정·PLC 응답·통계 CSV 는 바뀌지 않는다 — 5개 파일 모두 기존 줄 삭제 0, 추가 줄에 판정·응답 대입 없음, MeasurementHistoryCsvWriter.cs 무변경"
    requirement: "NGA-05"
    verification:
      - kind: other
        ref: "git diff -w 5개 파일 deleted=0 전부, judge(LastJudgement=/LastMeasuredValue=/FaiAllPass=/ResponseQueue/LastFindSucceeded=) grep=0 전부, csv_untouched=0, untouched(MainView.xaml.cs/csproj)=0, newcs=0"
        status: pass
    human_judgment: false
  - id: D4
    description: "새 런타임 보관 칸(MeasurementBase.LastZCandidateScores, DatumConfig.LastAlignMatchScore/Row/Col/AngleDeg)은 public 필드 + JsonIgnore 라 레시피 INI·레시피 JSON·붙여넣기 복사에 새 키가 생기지 않는다"
    requirement: "NGA-05"
    verification:
      - kind: other
        ref: "리플렉션(NgCauseProbe json) — meas_field=1 meas_prop=0 meas_jsonignore=1, datum_fields=4 datum_jsonignore=4"
        status: pass
    human_judgment: false
  - id: D5
    description: "SIMUL 또는 실기 자동 사이클 1회 후 실제 cycle.json 에 DatumDiagnostics·ZCandidateScores·ZRangeStartIndex 가 채워진다"
    requirement: "NGA-07"
    verification:
      - kind: other
        ref: "실행 하네스 없음 — 78-07 UAT U-6 에서 사람이 확인"
        status: pass
    human_judgment: true
    rationale: "이 plan 은 probe(메모리 DTO 왕복 + DatumConfig 직접 조작)로 매핑·직렬화 로직만 검증했다. 실제 검사 스레드가 이 값을 채우는지는 하드웨어/시퀀스 실행이 필요해 78-07 UAT backstop 으로 남는다."

duration: 약 20분
completed: 2026-09-17
status: complete
---

# Phase 78 Plan 03: Z 후보 선명도·기준점 진단 값 cycle.json 기록 Summary

**로그에만 있던 Z 후보별 선명도 점수와 기준점(원점·각도·1번 패턴 매칭 점수)을 검사 로직·판정을 건드리지 않고 cycle.json 에 기록만 추가한 2커밋(D-78-08)**

## Performance

- **Duration:** 약 20분
- **Started:** 2026-09-17 (78-02 마지막 커밋 직후)
- **Completed:** 2026-09-17
- **Tasks:** 2 (둘 다 auto)
- **Files modified:** 5 (MeasurementBase.cs, Action_FAIMeasurement.cs, CycleResultSerializer.cs, DatumConfig.cs, InspectionSequence.cs)

## Base / 커밋 해시

- **base-78-03 (편집 전 HEAD):** `a8979a3671909af8f7eab76ca4e15f6293bbb915`
- **Task 1 커밋:** `1622f78b` — feat(78-03): Z 후보 선명도·검사 당시 Z 범위 cycle.json 기록
- **Task 2 커밋:** `8c6e0e90` — feat(78-03): 기준점 진단 값 cycle.json 기록

## 빌드 결과

Debug|x64 (Release 금지, D:\Data 읽기만) — 두 커밋 시점 모두 `msbuild_exit=0`, errors 0.

## Probe json 출력 원문

Task 1 커밋 후:
```
json roundtrip=PASS
json old zstart=-1 zend=-1 cand=0 datum=0
json meas_field=1 meas_prop=0 meas_jsonignore=1
```

Task 2 커밋 후(누적 재실행 — Task 1 출력 유지 + datummap 신규):
```
json roundtrip=PASS
json old zstart=-1 zend=-1 cand=0 datum=0
json meas_field=1 meas_prop=0 meas_jsonignore=1
datummap fresh=PASS
datummap stale=PASS
datummap never=PASS
datummap null=PASS
datummap roundtrip=PASS
json datum_fields=4 datum_jsonignore=4
```

`json old ...`(20260601/163944_cycle/cycle.json, 옛 파일)는 두 커밋 시점 모두 `zstart=-1 zend=-1 cand=0 datum=0` — 옛 cycle.json 은 신규 필드 없이 기본값(-1/빈 배열)으로 그대로 로드된다(NGA-05).

## 파일별 `git diff -w` 삭제 수 (base → HEAD, 전체 plan 누적)

| 파일 | added | deleted |
|---|---|---|
| WPF_Example/Custom/Sequence/Inspection/MeasurementBase.cs | 5 | 0 |
| WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs | 24 | 0 |
| WPF_Example/Custom/Sequence/Inspection/CycleResultSerializer.cs | 68 | 0 |
| WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs | 13 | 0 |
| WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs | 33 | 0 |

전부 기존 줄 사이 삽입만 — 5개 파일 모두 삭제 0. 하드룰 grep(`ternary/coalesce/nullcond/switchexpr/datesig/logic3`)도 5개 파일 전부 0. `judge`(`LastJudgement=`/`LastMeasuredValue=`/`FaiAllPass=`/`ResponseQueue`/`LastFindSucceeded=`) grep 도 5개 파일 전부 0 — 판정·응답 경로에 새 대입이 없다. `MeasurementHistoryCsvWriter.cs`/`MainView.xaml.cs`/`DatumMeasurement.csproj` 무변경, 신규 `.cs` 파일 0.

## 새 심볼 목록 (Artifacts 표 78-03 행과 대조)

| 심볼 | 파일 | Artifacts 표 대조 |
|---|---|---|
| `LastZCandidateScores`(필드, JsonIgnore) | MeasurementBase.cs | 일치 — ClearResult 에서 `= null` 리셋 |
| `BuildZCandidateScoreList(List<ZFocusRunResult>)` | Action_FAIMeasurement.cs | 일치 — `ExecuteZRangeSelection` 의 `ApplySelectedZLabel` 다음 줄에서 write-back |
| `bZRangeEnabled` 가드 + `shotDto.ZRangeStartIndex/ZRangeEndIndex` 대입, `measDto.ZCandidateScores` 복사 | CycleResultSerializer.cs (BuildDto) | 일치 |
| `ALIGN_MATCH_NONE`, `LastAlignMatchScore/Row/Col/AngleDeg`(필드, JsonIgnore) | DatumConfig.cs | 일치 |
| `TryComposeAlign` 매칭 전 4개 리셋 + `TryFindPose` 성공 직후 4개 기록 | InspectionSequence.cs | 일치 |
| `NON_FINITE_REPLACEMENT`, `ToFiniteOrZero(double)`, `BuildDatumDiagnostic(DatumConfig, DateTime)` | CycleResultSerializer.cs | 일치 |
| `BuildTickDatumDiagnosticsSnapshot()` + 저장 3곳(`AddResponse`/`HandleManualCyclePersist`/`PersistAndEnqueueV1`) `cycleDto.DatumDiagnostics = ...` 배선(SaveAsync 직전) | InspectionSequence.cs | 일치 |

## cycle.json 새 키 예시 (probe 가 실제로 쓴 파일에서 발췌)

`roundtrip/cycle.json`(Task 1, Z 후보 4개 — 후보 z6 은 실패 후보로 Ok=false/Score=0.0 도 그대로 기록됨을 보여준다):
```json
"ZRangeStartIndex": 3,
"ZRangeEndIndex": 5,
...
"ZCandidateScores": [
  { "ZIndex": 3, "Ok": true, "Score": 28.284271247461902 },
  { "ZIndex": 4, "Ok": true, "Score": 30.1 },
  { "ZIndex": 5, "Ok": true, "Score": 30.1 },
  { "ZIndex": 6, "Ok": false, "Score": 0.0 }
]
```

`datummap/cycle.json`(Task 2 — `DetectedFitRMSE=double.NaN`/`AlignThetaDeg=double.PositiveInfinity` 로 주입한 값이 `ToFiniteOrZero` 를 거쳐 `0.0` 으로 기록됨을 보여준다):
```json
"DatumDiagnostics": [
  {
    "DatumName": "Datum_1",
    "IsDetected": true,
    "IsDetectedThisTick": true,
    "DetectTime": "2026-09-17T18:33:32.5134817+09:00",
    "OriginRow": 6601.9,
    "OriginCol": 634.2,
    "AngleDeg": 0.015,
    "EdgeCount": 40,
    "FitRmse": 0.0,
    "AlignMatchScore": 0.98,
    "AlignMatchRow": 0.0,
    "AlignMatchCol": 0.0,
    "AlignMatchAngleDeg": 0.0,
    "Align2Score": 0.0,
    "AlignThetaDeg": 0.0
  }
]
```

## Task Commits

1. **Task 1: Z 후보 선명도·검사 당시 Z 범위 기록** - `1622f78b` (feat)
2. **Task 2: 기준점 진단 값(원점·각도·에지 수·RMSE·패턴 점수) 기록** - `8c6e0e90` (feat)

**Plan metadata:** (이 커밋) - `docs(78-03): complete Z 후보·기준점 진단 값 기록 plan`

## Files Created/Modified

- `WPF_Example/Custom/Sequence/Inspection/MeasurementBase.cs` - `LastZCandidateScores`(필드+JsonIgnore) 추가, `ClearResult` 리셋
- `WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs` - `BuildZCandidateScoreList` 신설(재계산 없음), `ExecuteZRangeSelection` write-back 1줄
- `WPF_Example/Custom/Sequence/Inspection/CycleResultSerializer.cs` - `BuildDto` 가 `ZRangeStartIndex/ZRangeEndIndex`(IsZRangeEnabled 가드)·`ZCandidateScores` 복사, `ToFiniteOrZero`/`BuildDatumDiagnostic` 신설
- `WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs` - `ALIGN_MATCH_NONE` + `LastAlignMatchScore/Row/Col/AngleDeg`(필드+JsonIgnore) 추가
- `WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs` - `TryComposeAlign` 리셋·기록 8줄, `BuildTickDatumDiagnosticsSnapshot` 신설, 저장 3곳 배선

## Decisions Made

없음 — 계획(78-03-PLAN.md)이 삽입 위치·필드 형태(프로퍼티 아닌 public 필드)·리셋 지점을 정확히 지정했고 그대로 구현했다. 유일하게 실행 중 확인이 필요했던 지점은 `LastFindSucceeded = true` 대입이 `_lastFindTimeUtc` 를 자동 스탬프하는지 여부였는데(계획이 가정한 동작), `DatumConfig.cs:931` setter 를 읽어 확인 후 probe 의 `datummap fresh/stale` 케이스로 실증했다 — 계획과 정확히 일치.

## Deviations from Plan

None - 계획대로 실행. 모든 acceptance criteria(빌드 0/probe PASS 전체/그레프 수치 전부)가 계획이 명시한 값과 정확히 일치했다.

## Issues Encountered

None — probe(`NgCauseProbe.cs`, 저장소 밖 `C:/Users/admin/AppData/Local/Temp/p78-probe/`)에 `json` 모드를 신설(Task 1: roundtrip/old/reflection, Task 2: datummap 4케이스+roundtrip+reflection)했고 두 Task 모두 1회 컴파일·실행으로 통과했다.

## User Setup Required

None - 외부 서비스 설정 불필요.

## Next Phase Readiness

- 78-02 가 이미 소비하도록 배선해 둔 R8(초점 범위 끝)·R5(기준점 흔들림) 근거 텍스트가 이제 실제 값으로 채워질 준비가 됐다 — 78-02 의 `NgCauseProbe.exe rows` 를 재실행하면 `newfields`>0 이 되고 R8/R5 근거 문구에 후보 점수·각도·매칭 점수가 실제 숫자로 나타난다(78-02 SUMMARY "Next Phase Readiness"에 이미 예고됨).
- 실제 자동/수동 사이클 1회 후 cycle.json 에 이 필드들이 채워지는지는 78-07 UAT U-6 대기(하드웨어/시퀀스 실행 필요, probe 로 대체 불가).
- 옛 cycle.json(20260601 등) 호환은 이 plan 의 probe `json old ...=0` 으로 재확인됨(78-01/78-02 의 `newfields=0` 결과와 일치).
- 블로커 없음.

---
*Phase: 78-reviewer-ng-cause-analysis*
*Completed: 2026-09-17*
