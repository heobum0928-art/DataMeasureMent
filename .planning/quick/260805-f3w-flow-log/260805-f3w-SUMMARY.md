---
phase: quick-260805-f3w
plan: 01
subsystem: logging
tags: [halcon, measurepos, logging, trace, inspection-sequence, datum-finding]

requires: []
provides:
  - "ELogType.Flow (=7) 신설 + Flow 로그 카테고리 자동 등록"
  - "FlowLog.CycleEnd — 검사 사이클 1회당 요약 1줄 (시퀀스명/판정/측정개수/NG개수/소요시간)"
  - "DatumFindingService strip 단위 로그 → ROI 단위 요약 병합 + 같은 ROI 예외 dedup 가드"
affects: [inspection-flow-visibility, trace-log-volume]

tech-stack:
  added: []
  patterns:
    - "FlowLog 단일 진입점 패턴: public 메서드 1개, 전체 try/catch, 예외 절대 미전파"
    - "EStripOutcome enum 반환으로 strip 단위 로그를 ROI 단위 집계로 승격 (VisionAlgorithmService.cs 기존 패턴 재사용)"
    - "인스턴스 필드 dedup 가드(_bStripErrorLoggedThisRoi) — ROI 루프 진입 시 리셋, static 금지(스레드 간섭 방지)"

key-files:
  created:
    - WPF_Example/Utility/FlowLog.cs
  modified:
    - WPF_Example/Setting/SystemSetting.cs
    - WPF_Example/SystemHandler.cs
    - WPF_Example/DatumMeasurement.csproj
    - WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs
    - WPF_Example/Halcon/Algorithms/DatumFindingService.cs

key-decisions:
  - "D-F3W-02-REV: 압축 결정 — 사이클 시작 줄 없이 종료 시 요약 1줄만 (시퀀스명+tact+판정+측정/NG개수)"
  - "D-F3W-03 범위 축소: S1(strip 반복 로그)+S2(예외 dedup, S1 종속)만 처리. S3/S4/S6/S7/S8/S9/S10 은 이번 범위 제외(ROI당 1~2줄 수준, 흐름 로그를 가리지 않음)"
  - "S11([ALIGN-DIAG-LIVE] 임시 진단 로그) 삭제, S12([ALIGN]/[ALIGN2] datumDetectRotDeg vs patternThetaDeg 확증 로그)는 절대 유지"

requirements-completed: [D-F3W-01, D-F3W-02-REV, D-F3W-03-S1, D-F3W-03-S2, D-F3W-03-S11, D-F3W-03-S12]

duration: ~10min
completed: 2026-08-05
---

# Quick Task 260805-f3w: 흐름 로그 신설 + strip 스팸 정리 Summary

**검사 사이클 1회당 Flow 로그 1줄(시퀀스명/종합판정/측정·NG개수/소요시간) 신설 + Datum strip 단위 Trace 로그(사이클당 40~240줄)를 ROI 요약 1줄로 축약, 같은 ROI 예외는 첫 1건만 dedup**

## Performance

- **Duration:** ~10분 (커밋 간격 기준)
- **Completed:** 2026-08-05
- **Tasks:** 4/4 (Task 4 는 사용자 실기 승인 완료, 아래 참조)
- **Files modified:** 6 (신규 1 + 수정 5)

## Accomplishments
- `ELogType.Flow` 카테고리 신설 + `FlowLogSavePath`(`D:\Data\Flow`) + `SetLog` 등록 — 로그 창 메뉴에 자동 노출
- `FlowLog.CycleEnd` 단일 진입점 — 검사 사이클 1회 종료 시 `"■ [SIDE] 사이클 종료 — 종합판정 NG (측정 30개 중 6개 벗어남, 소요 4.2초)"` 형식의 요약 1줄만 출력, 전문용어 없음
- `InspectionSequence`: OnStart 는 tact 시계만 리셋(출력 없음), OnFinish/OnStop/OnError 3개 이벤트를 공통 핸들러로 묶어 중복 발화 가드(`_bFlowCycleLogged`)로 사이클당 정확히 1줄 보장
- `DatumFindingService`: strip 단위 `"strip MeasurePos"` 로그(사이클당 40~240줄) 삭제, `EStripOutcome`(Ok/NoEdge/Failed) 집계를 기존 accumulated 요약줄에 병합(`ok N, noEdge N, failed N`)
- 같은 ROI 안에서 strip 예외가 여러 번 나도 `"strip swallowed:"` 로그는 첫 1건만 남고 나머지는 요약줄의 `failed` 카운트로만 집계
- `[ALIGN-DIAG-LIVE]` 임시 진단 로그 2곳 삭제(`diagImgW`/`diagImgH` 전용 코드 포함), `[ALIGN]`/`[ALIGN2]` 확증 로그(S12, `datumDetectRotDeg vs patternThetaDeg`)는 한 글자도 손대지 않고 보존

## Task Commits

Each task was committed atomically:

1. **Task 1: 흐름 로그 카테고리 + FlowLog 헬퍼 신설** - `2857ad7` (feat)
2. **Task 2: 사이클 요약 1줄 배선 + S11 진단 스캐폴딩 삭제** - `9a9d519` (feat)
3. **Task 3: S1 축약 + S2 dedup 가드** - `de48a56` (feat)

Task 4 (checkpoint:human-verify, blocking)는 이번 실행에서 수행하지 않음 — 아래 "다음 단계" 참조.

## Files Created/Modified
- `WPF_Example/Utility/FlowLog.cs` (신규) - Flow 로그 단일 진입점, public 메서드 `CycleEnd` 1개
- `WPF_Example/Setting/SystemSetting.cs` - `ELogType.Flow = 7` 추가(기존 0~6 무변경), `FlowLogSavePath` 프로퍼티, `GetLogSavePath` case 추가
- `WPF_Example/SystemHandler.cs` - `Logging.SetLog((int)ELogType.Flow, ...)` 등록
- `WPF_Example/DatumMeasurement.csproj` - `Utility\FlowLog.cs` Compile Include 추가
- `WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs` - `_flowStopwatch`/`_bFlowCycleLogged` 필드, `HandleFlowLogCycleBegin`/`HandleFlowLogCycleEnd`/`CountFlowCycleResults` 핸들러, `[ALIGN-DIAG-LIVE]` 2곳 삭제
- `WPF_Example/Halcon/Algorithms/DatumFindingService.cs` - `EStripOutcome` enum, `_bStripErrorLoggedThisRoi` 가드 필드, `AppendEdgePointsFromStrip` 반환형 변경(`void`→`EStripOutcome`), `TryFindLine`/`TryExtractEdgePoints` 집계 병합

## Decisions Made
- 사이클 시작 로그는 넣지 않는다(D-F3W-02-REV 압축 결정) — 종료 시 요약 1줄만
- `_bStripErrorLoggedThisRoi` 는 인스턴스 필드로 유지(`DatumFindingService` 는 검출마다 `new` 생성, 스레드 공유 없음) — `static` 으로 만들면 Top/Side/Bottom 동시 실행 시 서로의 가드를 덮어써 다른 ROI 의 첫 예외가 사라지는 회귀가 생김
- 문자열 폴백/판정은 전부 `string.IsNullOrEmpty` 사용(`== null` 단독 비교 금지) — plan-checker 지적사항 반영

## Deviations from Plan

None - 계획대로 정확히 실행됨. plan 의 `<interfaces>` 에 명시된 라인 번호(1605/1783/1799/1811/1881/2039/2055/2067/2139/2182/2195)는 실행 시점(Task 1/2 로 인한 상단 라인 이동 반영 후)까지 전부 정확히 일치했다.

## Issues Encountered
- **동시 세션 간섭(정보용, 조치 불필요):** Task 실행 중 다른 세션이 같은 저장소에 `WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs`, `ShotConfig.cs`(quick-260805-iz8/ivy 추정), `FAIConfig.cs`/`MeasurementBase.cs`/`ParamBase.cs`(quick-260805-iw0, 커밋 `b7c6c19`)를 병행 수정/커밋했다. 이 plan 의 파일 범위와 겹치지 않아 별도 조치 없이 진행했고, 내 3개 태스크 커밋은 각각 `files_modified` 목록에 정확히 일치하는 파일만 포함한다(`git show --stat` 로 확인 완료).
- **csproj 사전 상태 확인:** 세션 시작 시 `git status` 에 `DatumMeasurement.csproj` 가 이미 M 으로 표시돼 있었으나, 이는 세션 시작 이전에 다른 동시 작업(260805-e1l/d9y 등)이 이미 커밋 완료한 잔여 스냅샷이었다(`git diff` 로 내 1줄 추가 외 diff 없음 확인). 실제 충돌 없음.

## User Setup Required
None - 외부 서비스 설정 불필요. `FlowLogSavePath` 기본값(`D:\Data\Flow`)은 다른 `D:\Data\*` 로그 경로와 동일 패턴이라 별도 설정 없이 최초 로그 발생 시 자동 생성된다(`Logging.SetLog` 기존 동작).

## Next Phase Readiness

**Task 4 (checkpoint:human-verify): 승인 완료 (2026-08-05).** 사용자가 실제 앱으로 확인하고 스크린샷 첨부 — Trace 창에 strip 단위 반복 로그가 사라지고 `strip-loop accumulated 92 edge points across 100 strips (ok 92, noEdge 8, failed 0)` 형태의 ROI 요약 1줄만 남았으며, `[ALIGN] Side_Datum_1 datumDetectAngleDeg=-0.137 datumDetectRotDeg=0.000 vs patternThetaDeg=0.000` 확증 로그(S12)도 그대로 보존됨을 확인. 체크리스트 1~4 pass, 5(스크린샷으로 확인), 6(회귀 없음) 전부 승인.

아래는 승인 시 사용된 체크리스트(참고용 기록):

1. `bin/x64/Debug/DatumMeasurement.exe` 실행 (빌드 시 exe 파일잠금 방지를 위해 기존 프로세스는 미리 종료 — 이번 세션의 3회 빌드는 모두 잠금 없이 성공했으나, 실행 중인 인스턴스가 있다면 먼저 종료).
2. 로그 창 메뉴에 **`Flow`** 항목이 새로 보이는지 확인 → 열어 둔다.
3. RUN(또는 일괄검사)로 검사 **2~3 사이클** 실행.
4. **Flow 창** 확인 — 합격 기준:
   - [ ] 사이클 1회당 딱 1줄만 나오는가? (2줄 이상이면 FAIL)
   - [ ] 그 줄에 시퀀스명(예: `[SIDE]`)이 있는가?
   - [ ] 종합판정 OK/NG 가 있는가?
   - [ ] 측정 개수 / 벗어난 개수가 있는가?
   - [ ] 소요시간(초)이 있고, 실제 체감 시간과 얼추 맞는가?
   - [ ] `measurePhi`, `strip`, `transform`, `XLD`, `HTuple` 같은 전문용어가 하나도 없는가?
   - [ ] 여러 시퀀스(Top/Side/Bottom)를 돌렸을 때 어느 줄이 어느 시퀀스인지 구분되는가?
5. **Trace 창** 확인:
   - [ ] `strip MeasurePos` 반복 줄이 사라졌는가?
   - [ ] 검출 실패 ROI 가 있었다면, 그 ROI 의 `strip swallowed:` 줄이 1줄만 나오는가? (실패 총 개수는 같은 ROI 의 `strip-loop accumulated ... (ok N, noEdge N, failed N)` 요약줄의 `failed` 값으로 확인. 실패 ROI 가 없었다면 "해당 없음")
   - [ ] `[ALIGN] ... datumDetectRotDeg ... vs patternThetaDeg ...` 줄은 여전히 있는가? (없으면 FAIL — 즉시 중단)
6. **회귀 확인:** 같은 레시피/같은 이미지로 검사했을 때 측정값과 합불 판정이 이 작업 전과 동일한가?

승인 시 "승인"이라고 입력, 문제 발견 시 어느 줄에서 무엇이 이상한지 구체적으로 알려주세요.

**Carry-over (범위 제외, 필요해지면 별도 작업):**
- 폐기된 단계별 상세 흐름 로그(D-F3W-02, `[1단계]`/`[2단계]`/`[3단계]` 및 측정 항목별 라인) — 필요해지면 재검토
- 범위 제외한 스팸 항목: S3+S4(`VisionAlgorithmService.cs:182/214`), S6+S7+S8(`DatumFindingService.cs` bounds/trimmed 로그 미러 6곳), S9(erosion tact 로그), S10(`FAIEdgeMeasurementService.cs:76 [ALIGN-ROI]`) — 전부 ROI당 1~2줄 수준이라 흐름 로그를 가리지 않는다는 판단으로 이번 범위 제외 (S2 는 S1 에 종속되어 이번에 함께 처리 완료)

---
*Phase: quick-260805-f3w*
*Completed: 2026-08-05*

## Self-Check: PASSED

- Files verified present: `WPF_Example/Utility/FlowLog.cs`, `WPF_Example/Setting/SystemSetting.cs`, `WPF_Example/SystemHandler.cs`, `WPF_Example/DatumMeasurement.csproj`, `WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs`, `WPF_Example/Halcon/Algorithms/DatumFindingService.cs`
- Commits verified present: `2857ad7`, `9a9d519`, `de48a56`
