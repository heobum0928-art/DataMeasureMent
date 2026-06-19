---
phase: 57-pattern-roi-ux-datum-align-hardening
plan: 01
subsystem: inspection
tags: [leveling-removal, state-machine, datum, ini-serialization, halcon, dead-code]

# Dependency graph
requires:
  - phase: 54-align
    provides: ALIGN 패턴매칭 위치/tilt 보정 (leveling 대체재)
  - phase: 55-align-02
    provides: 2-패턴 baseline 각도 (θ 보정)
provides:
  - "leveling 멤버/메서드/INI 키 0건인 코드베이스"
  - "MoveZ → DatumPhase 직결 측정 상태머신 (EStep.Level 폐기)"
  - "off 레시피 회귀 0 (stale INI 키 무시, 명시 read 제거)"
affects: [57-02, 57-03, 57-04, 57-05]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "ParamBase 자동 직렬화 프로퍼티 제거 → 옛 INI stale 키 자동 무시 (로드 크래시 0)"
    - "명시 INI read(InspectionRecipeManager) 동시 제거로 KeyNotFound 회피"

key-files:
  created: []
  modified:
    - WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs
    - WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs
    - WPF_Example/Halcon/Algorithms/DatumFindingService.cs
    - WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs
    - WPF_Example/Custom/Sequence/Inspection/InspectionMasterParam.cs
    - WPF_Example/Custom/Sequence/Inspection/InspectionRecipeManager.cs
    - WPF_Example/Halcon/Algorithms/PatternMatchService.cs

key-decisions:
  - "EStep enum 멤버 순서 유지(Init/MoveZ/DatumPhase/Grab/Measure/End) — switch 명칭 기반이라 정수값 변동 안전"
  - "옛 INI 의 stale LevelingEnabled/IsLevelingReference 키는 ParamBase.Load 가 매칭 프로퍼티 부재 시 무시 — 명시 read 제거로 KeyNotFound 회피 (D-14)"
  - "PatternMatchService 의 폐기된 TryGetLevelingAngle 참조 doc 주석을 ALIGN 으로 갱신 (Rule 1 — 본 task 가 유발한 stale 참조)"

patterns-established:
  - "죽은 기능 제거: 호출처(상태머신 case) 선 제거 → 호출 대상(서비스 메서드) 후 제거 순서로 CS0103/CS1501 빌드 차단 회피"

requirements-completed: ["#6"]

# Metrics
duration: ~10min
completed: 2026-06-19
---

# Phase 57 Plan 01: leveling 완전 제거 Summary

**미사용 leveling(레벨링/tilt 보정) 잔재를 코드·측정 상태머신·INI 직렬화에서 전수 제거 — MoveZ→DatumPhase 직결, ALIGN(Phase 54/55)이 위치/tilt 보정을 완전 대체**

## Performance

- **Duration:** ~10 min
- **Started:** 2026-06-19T16:50:00Z (approx)
- **Completed:** 2026-06-19T16:58:00Z (approx)
- **Tasks:** 3
- **Files modified:** 7

## Accomplishments
- 측정 상태머신에서 `EStep.Level` enum 멤버 + 케이스 블록(SIMUL 진단 로그 포함) 완전 제거, `MoveZ → DatumPhase` 직결로 재배선
- leveling 멤버/메서드/INI 키 전수 제거: `LevelingEnabled`(시퀀스+PropertyGrid), 캐시 멤버 5종, `TryComputeLevelingAngle`, `TryGetLevelingAngle` 양 오버로드(4-arg/6-arg), `IsLevelingReference`, INI save/preserve/load 키 3곳
- ALIGN(#4) 경로(`AlignPreTransform`/`TryFindVerticalTwoHorizontalDualImage`) 무손상 — 57-02 Wave 2 충돌 회피
- SIMUL_MODE Debug/x64 Rebuild PASS (error CS 0, error MSB 0, 신규 warning 0, `DatumMeasurement.exe` 생성)

## Task Commits

각 task 원자적 커밋:

1. **Task 1: EStep.Level 제거 + 상태머신 MoveZ→DatumPhase 재배선** - `40ffe36` (refactor)
2. **Task 2: leveling 멤버/메서드/INI 키 전수 제거** - `d10c884` (refactor)
3. **Task 3: SIMUL_MODE Debug/x64 빌드 검증** - 코드 변경 없음 (빌드 게이트 PASS, 산출물 gitignored — 별도 커밋 없음)

## Files Created/Modified
- `Action_FAIMeasurement.cs` - EStep enum 에서 `Level,` 제거, MoveZ 종료 전이 `EStep.Level`→`EStep.DatumPhase`, `case EStep.Level` 블록 전체 삭제
- `InspectionSequence.cs` - `LevelingEnabled` + 캐시 멤버/메서드(`_levelingAngleRad`/`_levelingComputed`/`LevelingAngleRad`/`LevelingComputed`/`SetLevelingAngle`/`ResetLeveling`) + `TryComputeLevelingAngle` 제거, `ClearDatumTransforms` 내 `ResetLeveling()` 호출 제거
- `DatumFindingService.cs` - `TryGetLevelingAngle` 4-arg/6-arg 오버로드 제거 (AlignPreTransform/DualImage 무손상)
- `DatumConfig.cs` - `IsLevelingReference` 프로퍼티 + `[Category("Datum|Leveling")]` 속성 제거 (PatternRoi*/TeachingImagePath 보존)
- `InspectionMasterParam.cs` - `LevelingEnabled` PropertyGrid 프로퍼티 제거 (DisplayName 보존)
- `InspectionRecipeManager.cs` - save/preserve/load 의 `["LevelingEnabled"]` 키 접근 3곳 제거
- `PatternMatchService.cs` - 폐기된 `TryGetLevelingAngle` 참조 doc 주석을 ALIGN 으로 갱신 (Rule 1)

## Decisions Made
- EStep enum 멤버 순서 유지 — switch 가 명칭 기반이라 정수값 변동은 안전하지만 혼동 방지 위해 순서 보존
- 옛 INI 의 stale `LevelingEnabled`/`IsLevelingReference` 키는 ParamBase.Load 가 매칭 프로퍼티 부재 시 무시 → 로드 크래시 0 (D-14). 명시 read(InspectionRecipeManager :139) 도 동시 제거하여 KeyNotFound 회피
- 제거 순서: Task 1(호출처 EStep.Level) 선행 → Task 2(호출 대상 메서드) 후행으로 CS0103/CS1501 빌드 차단 위험 최소화

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] PatternMatchService.cs 의 폐기 메서드 참조 doc 주석 갱신**
- **Found during:** Task 2 (프로젝트 전수 grep 검증)
- **Issue:** `PatternMatchService.cs:16` XML doc 주석이 본 task 가 제거한 `DatumFindingService.TryGetLevelingAngle(line-fit)` 을 "정밀 θ 담당"으로 참조 — 제거 후 존재하지 않는 메서드를 가리키는 stale 참조 (컴파일 영향 없으나 유지보수 오도)
- **Fix:** 주석을 `coarse x,y + θ는 ALIGN(패턴매칭 rigid transform)이 담당` 으로 갱신 + hbk 마커 추가
- **Files modified:** WPF_Example/Halcon/Algorithms/PatternMatchService.cs
- **Verification:** grep 으로 코드 참조 0건 확인, 빌드 PASS
- **Committed in:** d10c884 (Task 2 commit)

---

**Total deviations:** 1 auto-fixed (1 bug — stale doc 참조)
**Impact on plan:** 본 task 가 직접 유발한 stale 참조의 정정. scope creep 없음 (계획 파일 목록 외 1파일이나 leveling 제거의 직접 부산물).

## Issues Encountered
- MSBuild 호출 시 Git Bash 가 `/p:` 스타일 스위치를 경로로 오변환하여 MSB1008 발생 → `-p:` 대시 스타일로 교체하여 해결 (빌드 명령 형식 문제, 코드 무관)

## Threat Surface
- T-57-01 (ParamBase.Load 옛 INI DoS): mitigated — 명시 read 제거 + 매칭 프로퍼티 부재 시 stale 키 무시. 빌드 PASS 로 명시 read 제거 확증. 실데이터 로드 크래시 0 은 후속 UAT 에서 확인.
- T-57-02 (EStep 상태머신 전이 누락): mitigated — MoveZ→DatumPhase 명시 재배선, switch 명칭 기반, 빌드 게이트로 미해결 전이 차단 확인.
- 신규 위협 표면 도입 없음 (제거 전용 plan).

## Known Stubs
None — 제거 전용 plan, 신규 stub 0건.

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- leveling 제거 완료 — Wave 2 (57-02 #4 DualImage ALIGN 이식) 가 안전하게 진행 가능. `AlignPreTransform`/`TryFindVerticalTwoHorizontalDualImage` 무손상 확인.
- **UAT 잔여:** off 레시피(stale leveling 키 포함) 실데이터 로드 크래시 0 확증은 빌드 게이트 이후 실데이터 UAT 로 확인 필요.

## Self-Check: PASSED

- 6 modified source files + SUMMARY.md: all FOUND
- Task commits 40ffe36, d10c884: all FOUND
- grep leveling references: 0 code references (hbk removal-marker comments only)
- MSBuild Debug/x64 Rebuild: 0 error CS / 0 error MSB / 0 new warning, exe generated

---
*Phase: 57-pattern-roi-ux-datum-align-hardening*
*Completed: 2026-06-19*
