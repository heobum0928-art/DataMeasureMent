---
phase: 71-prep-op-plc-off-p-f
plan: 02
subsystem: api
tags: [inspection-sequence, lighting, cycle-lifecycle, tcp-protocol]

# Dependency graph
requires:
  - phase: 71-prep-op-plc-off-p-f
    plan: "01"
    provides: "$PREP wire 포맷에서 Op 필드 제거 완료, TurnOffShotLights()/TurnOffPrepLights() 무수정 보존(호출자 0개 상태로 이 plan 에 인계)"
provides:
  - "TryTurnOffLightsOnCycleEnd(packet, szPath, nZIndex) 단일 헬퍼 — IsBuffer==false(P/F 확정) 게이트 + [CycleLightOff] 추적 로그"
  - "BuildScopedResponse 훅 — 정상 마지막-index P/F + 크로스-Z Datum 즉시 F 커버"
  - "HandleDatumIndexResponse 훅 — Index 0 Datum 즉시 F(BuildScopedResponse 미경유 별도 경로) 커버"
affects: [71-03, 71-04]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "종료 경로 다중화(3개) → 판정 함수 바깥 단일 헬퍼로 집중, 판정 함수 본문은 무오염 유지"
    - "IsBuffer 게이트를 소등 트리거로 재사용 — 새 상태 플래그 도입 없이 기존 B/P/F 판정 결과만으로 결정"

key-files:
  created: []
  modified:
    - WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs

key-decisions:
  - "TryTurnOffLightsOnCycleEnd 를 TurnOffShotLights() 바로 아래(709번 줄 이후)에 배치 — '이제 TurnOffShotLights 를 누가 부르나' 질문에 파일 내 한 곳에서 답이 되도록"
  - "BuildScopedResponse 에서는 ApplyCycleJudgement + TryApplyCrossZDatumImmediateFail 두 판정이 모두 끝난 뒤 단일 지점에서만 체크 — 두 함수 각각에 훅을 심으면 중복소등/누락 위험이 있어 CONTEXT 권장 설계를 그대로 채택"
  - "HandleDatumIndexResponse 에 별도 두 번째 훅 필수 — Index 0 즉시 F 는 BuildScopedResponse 를 전혀 거치지 않는 경로임을 라이브 코드로 재확인(CONTEXT.md 가 명시적으로 누락 위험 경로로 지목)"

requirements-completed: [PROTO-PREP-01]

# Metrics
duration: 4min
completed: 2026-08-06
---

# Phase 71 Plan 02: 사이클 P/F 확정 시 조명 자동 소등 Summary

**InspectionSequence 에 `TryTurnOffLightsOnCycleEnd` 단일 헬퍼를 신설하고, 사이클이 P/F 로 확정되는 3개 종료 경로(정상 마지막-index, 크로스-Z Datum 즉시 F, Index 0 Datum 즉시 F) 전부에 배선해 `$PREP Op=0` 이 하던 소등 역할을 비전 자신이 판정 시점에 대체하도록 했다.**

## Performance

- **Duration:** 4 min
- **Started:** 2026-08-06T13:03:43Z
- **Completed:** 2026-08-06T13:08:08Z
- **Tasks:** 2 (계획된 2개 모두 완료)
- **Files modified:** 1

## Accomplishments
- `TryTurnOffLightsOnCycleEnd(TestResultPacket packet, string szPath, int nZIndex)` 헬퍼 신설 — `!packet.IsBuffer` 단일 게이트, `TurnOffShotLights()` 재사용, `[CycleLightOff]` 로그 1줄
- `BuildScopedResponse` 훅 배선 — `ApplyCycleJudgement`/`TryApplyCrossZDatumImmediateFail` 두 판정 호출 **직후**, `pMyContext.ResultInfo` 대입 **직전**에 `"scoped"` 경로로 호출(정상 마지막-index P/F + 크로스-Z Datum 즉시 F 동시 커버)
- `HandleDatumIndexResponse` 훅 배선 — `BuildDatumShotResponse()` 반환 **직후**, `PersistAndEnqueueV1` **앞**에 `"datum-index0"` 경로로 호출(`BuildScopedResponse` 를 안 거치는 별도 종료 경로, 누락 위험 경로로 CONTEXT.md 가 명시 지목했던 부분)
- 판정 로직(`ApplyCycleJudgement`/`TryApplyCrossZDatumImmediateFail`/`BuildDatumShotResponse`) 본문, `TurnOffShotLights()` 본문, v2.6 레거시 경로, `TcpServer/`·`Custom/SystemHandler.cs` 전부 무수정(회귀 0) — anti-goal grep 전부 통과

## Task Commits

1. **Task 1: TryTurnOffLightsOnCycleEnd 헬퍼 신설 + BuildScopedResponse 훅** - `a160fc0` (feat)
2. **Task 2: HandleDatumIndexResponse 훅 — Index 0 Datum 즉시실패 별도 종료 경로** - `526b57f` (feat)

_두 커밋 모두 msbuild Debug/x64 빌드 PASS 이후 커밋됨._

## Files Created/Modified
- `WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs` — `TryTurnOffLightsOnCycleEnd` 헬퍼(710-731 부근) + `BuildScopedResponse` 훅(1442) + `HandleDatumIndexResponse` 훅(1421)

## 최종 삽입 위치 (라인 번호는 이 plan 완료 시점 기준, 이후 드리프트 가능)

- 헬퍼 정의: `TurnOffShotLights()`(702-709) 바로 다음 블록, `private void TryTurnOffLightsOnCycleEnd(TestResultPacket packet, string szPath, int nZIndex)` — 시그니처와 게이트 로직은 plan `<action>` 절 코드 그대로 문자 일치.
- 훅 (a) `BuildScopedResponse`: `TryApplyCrossZDatumImmediateFail(packet, nZIndex);` 다음 줄, `pMyContext.ResultInfo = packet.Result;` 앞 줄 — `TryTurnOffLightsOnCycleEnd(packet, "scoped", nZIndex);` — 커버 경로: 정상 마지막-index P/F **+** 크로스-Z Datum 즉시 F(두 판정 결과를 한 지점에서 동시에 관찰).
- 훅 (b) `HandleDatumIndexResponse`: `TestResultPacket datumPacket = BuildDatumShotResponse();` 다음 줄, `PersistAndEnqueueV1(recipeManager, datumPacket);` 앞 줄 — `TryTurnOffLightsOnCycleEnd(datumPacket, "datum-index0", DATUM_Z_INDEX);` — 커버 경로: Index 0 Datum 즉시 F(BuildScopedResponse 미경유).

## 3개 UAT 시나리오별 로그 예시 (71-03/71-04 UAT 에서 그대로 대조용)

- 정상 P (마지막 index 종합 판정 OK): `[CycleLightOff] Seq=BOTTOM, path=scoped, z=3, result=OK`
- NG 누적 F (마지막 index 종합 판정 NG): `[CycleLightOff] Seq=BOTTOM, path=scoped, z=3, result=NG`
- Index 0 Datum 즉시 F: `[CycleLightOff] Seq=BOTTOM, path=datum-index0, z=0, result=NG`
- (참고) 크로스-Z Datum 즉시 F: `[CycleLightOff] Seq=SIDE, path=scoped, z=1, result=NG`
- 중간 index B 응답(다음 촬영 남음): 로그 없음 — `!packet.IsBuffer` 게이트에서 early-return, `TurnOffShotLights()` 자체가 호출되지 않음.

`result` 값은 `packet.Result`(타입 `EVisionResultType` 로 추정)의 `ToString()` 출력이며, `path` 값(`scoped`/`datum-index0`)이 세 경로 중 어느 것이 실제로 소등을 트리거했는지 로그만으로 구분 가능하게 한다.

## 로그 파일 경로

`ELogType.LightController` 는 `SystemHandler.Initialize()`(`SystemHandler.cs:94`)에서 `Logging.SetLog((int)ELogType.LightController, "LightController", Setting.GetLogSavePath(ELogType.LightController))` 로 등록된다. `SystemSetting.GetLogSavePath` 는 `ELogType.LightController` 케이스에서 `LightControllerPath` 프로퍼티(기본값 `D:\Data\LightController`, `Setting.ini` 로 재정의 가능)를 반환한다. `Logging.LogInfo.GetTodaySavePath()` 가 실제 파일명을 `{LogPath}\{yyyy-MM-dd}_{LogName}{FileExt}` 형식으로 조립하므로, 기본 설정 기준 최종 경로는:

```
D:\Data\LightController\yyyy-MM-dd_LightController.log
```

기존 `[PREP] Shot not found for ZIndex=...` 점등 관련 로그(`InspectionSequence.cs:684-685`)도 같은 `ELogType.LightController` 를 쓰므로, UAT 시 점등/소등 이벤트를 이 한 파일에서 시간순으로 대조할 수 있다.

## Decisions Made
- 위 `key-decisions` 프론트매터 3항목 참조. plan 이 지정한 CONTEXT 권장 설계(단일 지점 + 별도 두 번째 훅)를 그대로 채택, 별도 대안 검토 없음.

## Deviations from Plan

None — plan 의 `<action>` 절이 지정한 코드 블록을 문자 그대로 두 지점에 삽입했고, 두 태스크의 acceptance_criteria(정의/호출 카운트, 삽입 위치, anti-goal 오염 검사 3종, ELogType.LightController 카운트, 헬퍼/판정 함수 본문 무수정) 및 plan-level `<verification>` 8개 항목 전부 grep/git diff/msbuild 로 통과 확인했다. 라인 드리프트 사전 확인(`rg -n` 앵커 명령)도 plan 이 예상한 라인 번호와 정확히 일치했다(702/1404/1417/1418, 1390/1396/1397).

## Issues Encountered
None.

## User Setup Required

None - 외부 서비스 설정 불필요.

## Next Phase Readiness
- 조명 자동 소등 훅 2곳(정상 종료 경로 커버 3개 시나리오) 배선 완료, msbuild Debug/x64 빌드 PASS.
- 71-03(UAT — 실기 PLC 연동, 3개 시나리오 `[CycleLightOff]` 로그 대조 + T-71-04 핸들러 4필드→3필드 ACK 파서 동기화 여부 확인)은 이 plan 완료 후 바로 진행 가능.
- `D:\Data\LightController\` 경로가 실제 배포 환경에 존재/쓰기 가능한지, `Setting.ini` 에 재정의된 경로가 있는지는 71-03 UAT 시작 전에 실기 환경에서 확인 필요(이 plan 은 SIMUL_MODE/코드 레벨 검증만 수행, 로그 파일 실제 생성은 미관찰).
- 블로커 없음. `WPF_Example/Halcon/Algorithms/PatternMatchService.cs` / `WPF_Example/Custom/EthernetVision/PickerCenterCalibrationService.cs` 두 사용자 실험 파일은 이 plan 실행 전후로 변경 줄 수 동일, 계속 uncommitted 상태로 보존됨.

---
*Phase: 71-prep-op-plc-off-p-f*
*Completed: 2026-08-06*

## Self-Check: PASSED

- FOUND: WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs
- FOUND: .planning/phases/71-prep-op-plc-off-p-f/71-02-SUMMARY.md
- FOUND commit: a160fc0
- FOUND commit: 526b57f
