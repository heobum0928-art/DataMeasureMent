---
phase: quick-260807-d6w
plan: 01
subsystem: vision-algorithms
tags: [halcon, pattern-matching, datum, property-grid]

# Dependency graph
requires:
  - phase: quick-260728 (hotfix 시리즈)
    provides: "PatternMatchService.TryFindPose/TryFindRefPose 및 DatumConfig.EnsurePerRoiDefaults 의 현재 배선(Phase 54 ALIGN-01 기반)"
provides:
  - "DatumConfig.FindAngleExtentDeg — PropertyGrid Datum|PatternAlign 신규 항목, sentinel 0 → 3.0(±3°) 자동 복원"
  - "PatternMatchService.TryFindPose/TryFindRefPose 선택적 파라미터 double angleExtentDeg = 180.0 — Find 런타임 각도 검색범위를 호출부가 지정 가능"
  - "Datum 계열 Find 호출부 6곳(InspectionSequence 2 + MainView 4) datum.FindAngleExtentDeg 배선 완료"
affects: [datum-pattern-align, align-shape-match-unaffected]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "런타임 검색용 신규 설정값(FindAngleExtentDeg, ±N 규약)과 모델생성용 기존 설정값(PatternAngleExtentDeg, 전체 span 규약)을 이름은 비슷하지만 완전히 별개 필드로 유지 — 혼동 방지를 위해 XML doc/Description 에 규약 차이 명시"
    - "공용 헬퍼 시그니처에 선택적 파라미터(default 180.0)를 추가해 기존 호출부(Align)를 무변경으로 남기면서 신규 호출부(Datum)만 다른 값을 전달하는 하위호환 확장 패턴"

key-files:
  created: []
  modified:
    - WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs
    - WPF_Example/Halcon/Algorithms/PatternMatchService.cs
    - WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs
    - WPF_Example/UI/ContentItem/MainView.xaml.cs

key-decisions:
  - "사용자가 PatternMatchService.cs 에 직접 하드코딩해뒀던 실험(htRad = TupleRad(5), ±5° 고정)을 정식 설정으로 승격하며 완전히 대체 — 되돌리지 않고 대체"
  - "PatternMatchService.cs 에 섞여있던 무관한 미커밋 변경(SubPixel \"true\"→\"false\", NCC 호출 2곳)은 이번 작업 범위 밖으로 판단해 그대로 보존 — 되돌리지도 손대지도 않음"
  - "PickerCenterCalibrationService.cs 의 별건 미커밋 실험(자체 인라인 FindShapeModel 호출, ±5°)은 완전히 별도 이연 사안으로 판단해 읽지도 고치지도 커밋에 포함하지도 않음"

requirements-completed: [DATUM-FIND-ANGLE-01]

# Metrics
duration: ~12min
completed: 2026-08-07
---

# Quick Task 260807-d6w: Datum 런타임 Find 각도 검색범위 FindAngleExtentDeg 설정 신설 Summary

**Datum 패턴정렬의 런타임 Find 각도 검색범위를 PropertyGrid 설정값(FindAngleExtentDeg, 기본 ±3°)으로 승격 — PatternMatchService.TryFindPose/TryFindRefPose 에 선택적 파라미터를 추가해 Align 4개 호출부는 100% 무변경으로 유지**

## Performance

- **Duration:** ~12 min
- **Completed:** 2026-08-07
- **Tasks:** 2 of 2
- **Files modified:** 4

## Accomplishments

- `DatumConfig.cs` 에 `FindAngleExtentDeg` 프로퍼티 신설(Datum|PatternAlign 카테고리) — 사용자가 PropertyGrid 에서 직접 Datum 별 회전검색 범위를 튜닝 가능. `EnsurePerRoiDefaults()` 에 sentinel 0 → 3.0 복원 라인 추가로 구 레시피(INI 키 없음)도 자동으로 ±3° 검색을 갖게 됨(0으로 남아 회전검색이 완전히 죽는 회귀 없음).
- `PatternMatchService.TryFindPose`/`TryFindRefPose` 시그니처 끝에 선택적 파라미터 `double angleExtentDeg = 180.0` 추가 — 사용자가 하드코딩해뒀던 `htRad`(`TupleGenConst`+`TupleRad(5)`, ±5° 고정) 실험 코드 4곳을 전부 삭제하고 `findAngleExtentRad = angleExtentDeg * Math.PI / 180.0` 변환 + `-findAngleExtentRad, 2.0 * findAngleExtentRad` 로 대체.
- Datum 전용 Find 호출부 6곳(InspectionSequence.TryComposeAlign 패턴1/패턴2, MainView.RefreshPatternRefPoseAfterTeach 패턴1/패턴2, MainView 모델생성 직후 ref pose 기록 패턴1/패턴2)에 `datum.FindAngleExtentDeg` 를 마지막 인자로 배선. 6곳 모두 이미 상위에서 `EnsurePerRoiDefaults()`/`datum.EnsurePerRoiDefaults()` 를 호출하고 있어 별도 호출 추가는 불필요했음(확인만).
- Align(`AlignShapeMatchService.cs`) 4개 호출부는 선택적 파라미터를 생략하므로 단 한 글자도 수정되지 않았고, 기존 전방위(±180° = 360°) 검색 동작이 수학적으로 완전히 동일하게 유지됨(`git diff` 완전 무변화로 확인).
- `PickerCenterCalibrationService.cs` 의 별건 미커밋 실험은 이번 커밋에 전혀 포함되지 않았고 baseline(diff numstat `6 2`, diff hash `73a89c282724fedf25b7dcf8919b09251578d789`)이 작업 전후로 완전히 동일함을 확인.

## Task Commits

Each task was committed atomically:

1. **Task 1: FindAngleExtentDeg 설정 신설 + PatternMatchService 선택적 파라미터화** - `7b5e5fe` (feat)
2. **Task 2: Datum 호출부 6곳 배선 + Align/Picker 무변경 증명 + 정식 Rebuild** - `820ce3e` (feat)

**Plan metadata:** 본 SUMMARY.md 및 STATE.md/ROADMAP.md는 오케스트레이터가 별도 커밋 (실행자는 커밋하지 않음).

_Note: 이 quick task는 TDD 대상이 아님(설정값 배선 + 시그니처 확장) — RED/GREEN 게이트 해당 없음._

## Files Created/Modified

- `WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs` - `FindAngleExtentDeg` 프로퍼티(sentinel 0) + `EnsurePerRoiDefaults()` 에 `= 3.0` 복원 라인 추가
- `WPF_Example/Halcon/Algorithms/PatternMatchService.cs` - `TryFindPose`/`TryFindRefPose` 에 선택적 파라미터 `double angleExtentDeg = 180.0` 추가, 하드코딩 `htRad` 실험 4곳을 `findAngleExtentRad` 변환으로 대체(`SubPixel "false"` 2곳은 무변경 보존)
- `WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs` - `TryComposeAlign` 의 `TryFindPose` 호출 2곳에 `datum.FindAngleExtentDeg` 인자 추가
- `WPF_Example/UI/ContentItem/MainView.xaml.cs` - 재티칭 ref 재앵커 2곳 + 모델생성 직후 ref pose 기록 2곳, 총 4개 `TryFindPose` 호출에 `datum.FindAngleExtentDeg` 인자 추가

## Verification Results

자동 검증(`<automated>` 커맨드) 결과, 두 Task 모두 plan 이 요구한 값과 정확히 일치:

**Task 1**
- `htRad` 잔존 0건, `findAngleExtentRad` 6건(선언 2 + 인자 4), `double angleExtentDeg = 180.0` 2건, `"false"`(SubPixel) 2건 유지, `DatumConfig.cs` 의 `FindAngleExtentDeg` 프로퍼티 1건 + sentinel 복원(`= 3.0;`) 1건 — 전부 grep 카운트 일치.
- 스크래치 OutDir 컴파일(`-p:OutputPath=$TEMP/gsd-d6w-scratch/bin/` 등) — 이 시점에 호출부를 하나도 안 고쳤는데도 컴파일 성공(선택적 파라미터가 하위호환을 보장한다는 증거). `error CS` 0건.

**Task 2**
- `InspectionSequence.cs` `datum.FindAngleExtentDeg` 2건, `MainView.xaml.cs` 4건 — 일치.
- 6개 호출부 앞 `EnsurePerRoiDefaults()`(InspectionSequence 1) / `datum.EnsurePerRoiDefaults()`(MainView 2) 존재 확인 — 일치.
- `AlignShapeMatchService.cs` — `git status --porcelain` 및 `git diff --stat` 모두 완전 공백(무변경).
- `PickerCenterCalibrationService.cs` — `git diff --numstat` = `6 2`, `git diff | git hash-object --stdin` = `73a89c282724fedf25b7dcf8919b09251578d789` — 작업 시작 시점 baseline 과 100% 동일.
- Debug/x64 `/t:Rebuild` — 실행 중인 앱이 `bin\x64\Debug\DatumMeasurement.exe` 를 잠그고 있어 최종 복사 단계에서 `MSB3021`/`MSB3027`(재시도 10회 초과) 발생. **프로세스를 죽이지 않고**, 컴파일 단계 로그에서 `error CS` 0건임을 먼저 확인한 뒤, 스크래치 OutDir 로 동일 `/t:Rebuild` 를 재실행 — exit 0, `error CS` 0건, `error MSB` 0건, 변경 4개 파일(`PatternMatchService.cs`/`DatumConfig.cs`/`InspectionSequence.cs`/`MainView.xaml.cs`) 관련 신규 warning 0건(기존에 있던 `CS0618`/`CS0162` 경고만 그대로 재등장, 이번 변경과 무관).
- `git diff --diff-filter=D --name-only HEAD~1 HEAD` — 두 커밋 모두 의도치 않은 삭제 없음.

## Decisions Made

- Plan 이 지정한 대로 실행 — 별도 아키텍처 결정 없음(plan-checker 가 사전에 PASS 확인).
- 빌드 산출물 잠김 상황에서 프로젝트 하드 규칙(`never_kill_process_for_build_lock`)에 따라 프로세스를 종료하지 않고 스크래치 OutDir 컴파일로 대체 검증 — plan 의 지시와도 일치.

## Deviations from Plan

None - plan 원문 그대로 실행. 유일한 이탈은 정상 경로 `/t:Rebuild` 가 실행 중인 앱의 exe 잠금으로 실패한 것인데, 이는 plan 자체가 명시적으로 예견하고 대응 절차(스크래치 OutDir 컴파일 검증 + SUMMARY 기록)를 지정해둔 케이스이므로 편차(deviation)가 아니라 plan 이 계획한 분기 실행이다.

## Issues Encountered

- Windows/MSYS(Git Bash) 환경에서 `//p:...` 이중 슬래시 스위치가 MSBuild 로 전달되는 도중 일부만 단일 슬래시로 정규화되어 `MSB1001`(알 수 없는 스위치) 오류가 발생. `/p:` 대신 `-p:`(대시 프리픽스, MSBuild 의 대체 스위치 문법) 로 바꿔 우회 — 코드 변경과 무관한 로컬 셸 이슈이며 최종 컴파일 결과에는 영향 없음.
- Debug/x64 `/t:Rebuild` 가 실행 중인 앱의 exe 잠금(`MSB3021`/`MSB3027`)으로 최종 링크 단계에서 실패 — 프로세스 종료 없이 스크래치 OutDir 로 동일 Rebuild 를 재실행해 컴파일 성공(`error CS`/`error MSB` 0건)을 확인. 실행 중인 앱을 재시작하면 정상 경로 `bin\x64\Debug\` 산출물도 갱신될 것으로 예상되나, 앱 종료는 사용자 판단 영역이라 이번 세션에서 강제하지 않음.

## User Setup Required

None - 외부 서비스 설정 불필요.

## Next Phase Readiness

- `DatumConfig.FindAngleExtentDeg` 는 PropertyGrid 에서 바로 확인/조정 가능한 상태 — 앱을 재시작(잠긴 exe 갱신)하면 사용자가 UAT 로 직접 값 변경 및 검색범위 좁힘 효과를 확인할 수 있음.
- 정상 빌드 산출물(`bin\x64\Debug\DatumMeasurement.exe`) 갱신을 위해서는 현재 실행 중인 앱 인스턴스를 사용자가 직접 종료 후 재빌드/재실행해야 함 — 코드 자체는 이미 컴파일 검증 완료 상태라 블로커는 아님.
- Align/Picker 두 파일 모두 무변경 확인됐으므로 후속 작업에서 이 quick task 의 diff 를 신뢰하고 별도 회귀 테스트 없이 진행 가능.

---
*Phase: quick-260807-d6w*
*Completed: 2026-08-07*

## Self-Check: PASSED

- FOUND: WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs
- FOUND: WPF_Example/Halcon/Algorithms/PatternMatchService.cs
- FOUND: WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs
- FOUND: WPF_Example/UI/ContentItem/MainView.xaml.cs
- FOUND commit: 7b5e5fe (Task 1)
- FOUND commit: 820ce3e (Task 2)
