---
phase: quick-260814-dxy
plan: 01
subsystem: infra
tags: [halcon, measurepos, warmup, cold-start, tcp-gate, wpf]

# Dependency graph
requires:
  - phase: debug/top-release-2x-slower
    provides: "Measure 단계(measureExec, MeasurePos/MeasurePairs) 병목 위치 확정(Stopwatch 실측), 근본원인은 미확정"
provides:
  - "IsMeasureWarmupComplete 게이트 플래그(IsRecipeReady와 동일 패턴)"
  - "StartMeasureWarmupAsync/RunMeasureWarmup/TryWarmupOneMeasurement/FindMeasureWarmupShot/ShotHasAnyMeasurement 측정 파이프라인 워밍업 서비스"
  - "TCP $TEST(ProcessTest) + UI RUN(Btn_start_Click)/일괄검사(Btn_batchRun_Click) 워밍업 완료 게이트"
affects: [top-release-2x-slower, measure-pos-cold-start]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "게이트 플래그 패턴 재사용: IsRecipeReady와 동일 volatile bool + get/set 프로퍼티 구조를 IsMeasureWarmupComplete에 그대로 복제"
    - "fail-open 워밍업: Task.Run try/catch/finally에서 예외 무관 항상 게이트 개방, Shot 없음/이미지 없음 시 즉시 개방"

key-files:
  created: []
  modified:
    - WPF_Example/SystemHandler.cs
    - WPF_Example/Custom/SystemHandler.cs
    - WPF_Example/MainWindow.xaml.cs
    - WPF_Example/UI/ControlItem/InspectionListView.xaml.cs

key-decisions:
  - "새 .cs 파일을 만들지 않고 기존 Custom/SystemHandler.cs에 워밍업 로직 전부를 메서드로 추가 — csproj(건들면 안 되는 baseline 파일)를 편집할 필요를 원천 제거"
  - "워밍업은 실제 프로덕션과 동일한 meas.TryExecute() 호출 경로를 태우되 EvaluateJudgement/ClearResult는 호출하지 않아 판정/화면 표시에 영향 0"
  - "MEASURE_WARMUP_ITERATIONS=15를 하드코딩 상수로 결정 — 관측된 워밍업 문턱이 7~36회+(전혀 하락 안 함 포함)로 들쭉날쭉해 Setting.ini로 노출해도 사용자가 정답값을 알 수 없음"
  - "더미 이미지는 (1) SimulImagePath 실존 파일 우선 (2) 없으면 GenImageConst 합성 이미지 폴백 (3) 측정 있는 Shot 자체가 없으면 즉시 게이트 개방"

requirements-completed: [MEASURE-WARMUP-01]

# Metrics
duration: ~15min
completed: 2026-08-14
---

# Quick Task 260814-dxy: 측정 파이프라인 워밍업 (Release 콜드스타트 임시 완화) Summary

**Release 콜드스타트 시 HALCON measureExec(MeasurePos/MeasurePairs) 수 배~10배 저하 비용을, 실제 검사 사이클이 아니라 앱 기동 시점에 대표 Shot 15회 반복 실행으로 미리 치르게 하는 백그라운드 워밍업 + TCP/UI 게이트 배선. 근본원인은 미확정 상태이며, 이것은 "완전 해결"이 아니라 "임시 완화 시도"이다.**

## 중요 — 이 작업은 근본 수정이 아니다

`.planning/debug/top-release-2x-slower.md`(status: `root_cause_narrowed_workaround_pending`)가 명시하듯, Release 콜드스타트 저하의 근본원인은 이 세션 종료 시점까지 확정되지 않았다. 조사 과정에서 관측된 워밍업 문턱 자체가 7회, 12~13회(모호), 26회, 36회+(전혀 하락 안 함)로 재현마다 완전히 들쭉날쭉했다 — 즉 "N회 반복하면 빨라진다"는 고정 임계값 메커니즘 자체가 확증되지 않았다. 이번 워밍업(하드코딩 15회)은:

- **문제를 완전히 해소한다는 보장이 없다.** 관측 범위(7~36회+)의 중간값 근사치를 택했을 뿐, 그 이상의 문턱이 나오는 경우 워밍업 완료 후에도 여전히 느릴 수 있다.
- 원인 후보(HALCON 내부 메모리 캐시 워밍업 vs AV 커널모드 first-touch 개입) 둘 다 미확증 상태에서, "무엇이든 원인이면 다 통하는 범용 완화책"으로 구현한 것 — 근본 수정이 아니라 **비용을 기동 시점으로 이전(shift)** 시키는 우회다.
- 사용자가 사내 IT팀에 별도로 요청 중인 ESET 성능 예외(KB7833) 승인이 근본 해결 후보이며, 이 워밍업은 그 승인을 기다리는 동안의 임시 조치다.

이 SUMMARY를 "저하 문제 해결됨"으로 해석하지 말 것 — `.planning/debug/top-release-2x-slower.md`의 `status`는 여전히 `root_cause_narrowed_workaround_pending`이며 이 워밍업 적용 후에도 갱신되지 않았다(코드측 완화책 추가일 뿐, 근본원인 조사 자체는 진전 없음).

## Performance

- **Duration:** ~15 min (전체 세션, 정확한 시작 타임스탬프 미기록)
- **Completed:** 2026-08-14
- **Tasks:** 2/2 완료
- **Files modified:** 4

## Accomplishments

- `IsMeasureWarmupComplete` 게이트 플래그 추가(`WPF_Example/SystemHandler.cs`) — 기존 `IsRecipeReady`와 완전히 동일한 volatile bool + get/set 프로퍼티 패턴
- 측정 파이프라인 워밍업 서비스 5개 메서드 구현(`WPF_Example/Custom/SystemHandler.cs`): `StartMeasureWarmupAsync`(Task.Run 진입점, fail-open), `RunMeasureWarmup`(대표 Shot 15회 반복), `TryWarmupOneMeasurement`(DualImage 주입 미러링 + 단일 측정 실행), `FindMeasureWarmupShot`(SimulImagePath 우선, GenImageConst 폴백), `ShotHasAnyMeasurement`
- `ProcessTest`(TCP `$TEST` 처리)에 워밍업 완료 게이트 추가 — `IsRecipeReady` 체크 바로 다음, `IsMeasureWarmupComplete=false`면 거부+로그
- `Window_ContentRendered_LoadRecipe`(레시피 있음/없음 양쪽 분기)에서 `StartMeasureWarmupAsync()` 호출 배선
- `Btn_start_Click`/`Btn_batchRun_Click`(수동 RUN/일괄검사)에 동일 게이트 추가 — 미완료 시 `CustomMessageBox` 안내 후 return

## Task Commits

Each task was committed atomically:

1. **Task 1: 워밍업 게이트 플래그 + 워밍업 서비스 + TCP 게이트 배선** - `2fbbe94` (feat)
2. **Task 2: 앱 시작 워밍업 기동 배선 + UI RUN/일괄검사 게이트** - `79974f6` (feat)

_Plan metadata commit will be added separately by the orchestrator (SUMMARY.md/STATE.md/ROADMAP.md/REQUIREMENTS.md 커밋은 이 실행자 범위 밖)._

## Files Created/Modified

- `WPF_Example/SystemHandler.cs` - `IsMeasureWarmupComplete` 게이트 플래그(`_isMeasureWarmupComplete` volatile bool + 프로퍼티) 추가
- `WPF_Example/Custom/SystemHandler.cs` - using 2개 추가(`System.IO`, `ReringProject.Halcon.Models`), `ProcessTest`에 게이트 삽입, 워밍업 서비스 5개 메서드(상수 2개 포함) 신규 추가
- `WPF_Example/MainWindow.xaml.cs` - `Window_ContentRendered_LoadRecipe` 양쪽 분기에 `StartMeasureWarmupAsync()` 호출 추가
- `WPF_Example/UI/ControlItem/InspectionListView.xaml.cs` - `Btn_start_Click`/`Btn_batchRun_Click` 첫 줄에 워밍업 게이트 삽입

## Decisions Made

- 새 `.cs` 파일을 만들지 않고 기존 `Custom/SystemHandler.cs`에 전부 추가 — `DatumMeasurement.csproj`(사용자의 별도 진행 중인 로컬 실험, 절대 건들면 안 되는 baseline 파일)를 편집할 필요를 원천 제거
- 워밍업 반복 횟수는 `MEASURE_WARMUP_ITERATIONS = 15`로 하드코딩(Setting.ini 미노출) — 관측된 워밍업 문턱이 7~36회+(전혀 하락 안 함 포함)로 들쭉날쭉해 사용자가 "정답값"을 알 방법이 없으므로, 설정 UI만 늘고 실효는 없다고 판단
- 워밍업 호출은 `meas.TryExecute()`의 `out` 파라미터만 사용하고 `EvaluateJudgement`/`ClearResult`를 호출하지 않음 — 실제 판정 로직/화면 표시(`LastMeasuredValue`/`LastJudgement`)에 어떤 영향도 주지 않도록 설계
- `datumTransform` 인자는 `null` 전달 — `MeasurementBase.TryExecute` 계약상 identity와 동일(기존 관례 확인됨), 워밍업은 결과 정확도가 필요 없으므로 충분
- 더미 이미지 우선순위: (1) 현재 레시피의 측정 있는 Shot 중 `SimulImagePath` 파일이 실존하는 첫 Shot(실제 코드 경로 재현, 가장 신뢰도 높음) → (2) 없으면 `GenImageConst` 합성 이미지(캐시 워밍 목적이라 에지 검출 성공 여부 무관) → (3) 측정 있는 Shot 자체가 없으면 워밍업 스킵 + 즉시 게이트 개방(fail-open)

## Deviations from Plan

None - plan executed exactly as written. 계획서(`260814-dxy-PLAN.md`)의 interfaces 섹션에 명시된 편집 대상 지점의 현재 코드가 실제 파일 상태와 완전히 일치했고(`Custom/SystemHandler.cs`의 using 목록에 `System.Text`가 추가돼 있는 등 사소한 차이는 있었으나 삽입 지점 자체는 동일), 코드 스니펫을 그대로 삽입/치환하는 것만으로 두 태스크 모두 완료됨.

## Issues Encountered

- **빌드 산출물 잠김(MSB3021/MSB3027)**: `D:\Data\DatumMeasurement.exe`가 실행 중인 프로세스(Visual Studio Insiders PID 31036, DatumMeasurement PID 4220)에 의해 잠겨 있어 두 태스크 모두 표준 빌드에서 최종 복사 단계(`error MSB3027`/`MSB3021`)가 실패했다. 프로젝트 하드 규칙("빌드산출물 잠김→프로세스 종료 금지")에 따라 프로세스를 종료하지 않고, 스크래치 `-p:OutDir=<scratchpad>/build-verify*/`로 별도 컴파일 재검증을 수행 — 두 태스크 모두 `DatumMeasurement -> <scratch>/DatumMeasurement.exe` 생성 성공, `EXIT=0`, `error CS` 0건으로 확인. 표준 빌드 로그에서도 `error CS` 0건이었고, warning은 기존 baseline(CS0618×10 + CS0162×2, wpftmp 프로젝트 중복 계산 포함 정확히 12줄) 그대로였음(신규 warning 0건).

## User Setup Required

None - 코드만으로 완결되는 변경, 외부 서비스/환경변수 설정 불필요. 앱 재시작 시 자동으로 워밍업이 백그라운드에서 실행됨.

## Next Phase Readiness

- 이번 워밍업은 **임시 완화책**이다 — `.planning/debug/top-release-2x-slower.md`의 근본원인 조사는 계속 열려 있는 상태(`status: root_cause_narrowed_workaround_pending`)이며, 이 SUMMARY 작성만으로 그 문서의 status를 갱신하지 않았다.
- 실기 검증(실제 Release 빌드로 콜드스타트 워밍업 15회 후 measureExec 속도가 실제로 개선되는지, TCP `$TEST`/UI RUN이 워밍업 중 정상 거부되는지) 은 이번 세션 범위 밖 — 사용자가 별도로 확인 필요.
- 사용자가 진행 중인 ESET PROTECT 성능 예외(KB7833) 승인이 확정되면, 이 워밍업의 실효성을 재평가(예외 승인 후에도 워밍업이 여전히 필요한지, 혹은 제거해도 되는지)할 필요가 있음.
- `WPF_Example/DatumMeasurement.csproj`, `WPF_Example/Custom/EthernetVision/PickerCenterCalibrationService.cs`(사용자의 별도 진행 중인 로컬 실험) — 이번 작업에서 전혀 건드리지 않았음, baseline 해시(`3daa3bef...`/`86d1071...`)와 완전 일치 확인됨.

## Execution Note (continuation session)

이 실행 세션 시작 시점에 Task 1(`2fbbe94`)과 Task 2(`79974f6`) 커밋 및 이 SUMMARY.md 초안이 이미 작업 트리에 존재했다(이전 세션에서 코드 작성까지 완료됐으나 self-check/STATE.md 갱신은 미완이었던 것으로 추정). 이번 세션은 재실행하지 않고 기존 산출물을 검증만 했다:

- 두 커밋의 diff가 계획서(`260814-dxy-PLAN.md`)의 코드 스니펫과 100% 일치함을 라인 단위로 직접 대조(코멘트 문구, 메서드 시그니처, 게이트 삽입 위치 전부 확인)
- 절대 건들면 안 되는 파일 2종(`DatumMeasurement.csproj`, `PickerCenterCalibrationService.cs`) 해시가 baseline(`3daa3bef...`/`86d1071...`)과 완전 일치 — 이번 작업으로 변경 없음 재확인
- `EvaluateJudgement`/`ClearResult` 미호출 조건을 실제 메서드 본문에서 직접 눈으로 재확인(automated grep이 주석 텍스트를 오탐 매칭하는 것을 실제 소스로 판별 — 계획서에 미리 경고된 케이스)
- Debug/x64 재빌드 실행: `EXIT=0`, `D:\Data\DatumMeasurement.exe` 산출물 생성 성공, `error CS` 0건. 별도 전체 재컴파일에서 warning 12줄(`CS0618`×10 + `CS0162`×2)이 기존 baseline과 정확히 일치함을 확인(신규 warning 0건). 이전 세션이 기록한 산출물 잠김(MSB3027) 문제는 이번 빌드에서 재현되지 않았다(잠그고 있던 프로세스가 이미 종료된 것으로 추정).

## Self-Check: PASSED

- FOUND: WPF_Example/SystemHandler.cs
- FOUND: WPF_Example/Custom/SystemHandler.cs
- FOUND: WPF_Example/MainWindow.xaml.cs
- FOUND: WPF_Example/UI/ControlItem/InspectionListView.xaml.cs
- FOUND commit: 2fbbe94 (Task 1)
- FOUND commit: 79974f6 (Task 2)

---
*Phase: quick-260814-dxy*
*Completed: 2026-08-14*
