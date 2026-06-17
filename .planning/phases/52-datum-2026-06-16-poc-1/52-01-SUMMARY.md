---
phase: 52-datum-2026-06-16-poc-1
plan: 01
subsystem: database
tags: [datum, leveling, ini-recipe, inspection-sequence, parambase]

# Dependency graph
requires:
  - phase: 37-side-multi-datum
    provides: ClearDatumTransforms / TryRunSingleDatum / DatumConfigs per-datum loop (레벨링 캐시 lifecycle 차용)
provides:
  - InspectionSequence.LevelingEnabled (시퀀스 단위 레벨링 토글, D-04, 기본 off)
  - InspectionSequence 레벨링 각도 캐시 (_levelingAngleRad/_levelingComputed + Set/Reset/getter, D-03)
  - DatumConfig.IsLevelingReference (per-datum 기준 플래그, D-01, ParamBase bool 자동 직렬화)
  - FIXTURE 섹션 LevelingEnabled INI save/load (키 미존재 false 폴백)
affects: [52-02-leveling-angle, 52-03-preprocess-rotation, datum-leveling]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "레벨링 각도 캐시 = datum transform 과 동일 lifecycle (ClearDatumTransforms 에서 ResetLeveling)"
    - "INI bool 토글 = .ToBool() 폴백 false (DatumCount .ToInt()=0 폴백과 동일 메커니즘, 회귀 0)"

key-files:
  created: []
  modified:
    - WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs
    - WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs
    - WPF_Example/Custom/Sequence/Inspection/InspectionRecipeManager.cs

key-decisions:
  - "LevelingEnabled = FIXTURE 섹션 수동 키 직렬화(1/0 정수), DisplayName/DatumCount 와 동일 섹션"
  - "IsLevelingReference = DatumConfig ParamBase reflection 자동 직렬화 (_DATUM_{d} 서브섹션) — Save/Load 코드 추가 0"
  - "레벨링 각도 캐시(_levelingAngleRad/_levelingComputed)는 멤버 정의 + 리셋만 제공, 실제 1회 산출은 Plan 03 에서 사용 (interface-first)"

patterns-established:
  - "INI 신규 bool 토글: Save 시 ?1:0, Load 시 .ToBool() (키 미존재 false 폴백 회귀 0)"
  - "PreserveFixtureFromExisting 신규레시피 분기에 신규 키 기본값 명시 (기존데이터 분기는 L112 섹션 통째 복사로 자동 보존)"

requirements-completed: [LEVEL-01]

# Metrics
duration: 8min
completed: 2026-06-17
---

# Phase 52 Plan 01: 레벨링 데이터 모델 + INI 영속 토대 Summary

**시퀀스 단위 레벨링 토글(LevelingEnabled) + 기준 Datum 플래그(IsLevelingReference) + 레벨링 각도 캐시 멤버 정의와 FIXTURE 섹션 INI save/load 토대 구축 (기본 off, 기존 레시피 회귀 0)**

## Performance

- **Duration:** ~8 min
- **Started:** 2026-06-17T03:23:00Z (approx)
- **Completed:** 2026-06-17T03:31:34Z
- **Tasks:** 2
- **Files modified:** 3

## Accomplishments
- InspectionSequence 에 `LevelingEnabled` bool 멤버 추가 (D-04, 기본 off → INI 미존재 폴백 off 회귀 0)
- InspectionSequence 에 레벨링 각도 런타임 캐시 추가: `_levelingAngleRad` / `_levelingComputed` + `LevelingAngleRad`/`LevelingComputed` getter + `SetLevelingAngle()` / `ResetLeveling()` (D-03 시퀀스당 1회 산출 캐시)
- `ClearDatumTransforms()` 가 `ResetLeveling()` 호출 — datum transform 과 동일 사이클 리셋
- DatumConfig 에 `IsLevelingReference` per-datum 플래그 추가 (D-01, ParamBase bool 자동 직렬화, 기본 off)
- InspectionRecipeManager FIXTURE 섹션 `LevelingEnabled` save/load — 키 미존재 시 `.ToBool()` false 폴백
- msbuild Debug/x64 PASS (0 errors, 신규 warning 0)

## Task Commits

Each task was committed atomically:

1. **Task 1: InspectionSequence 레벨링 멤버 + DatumConfig 기준 플래그** - `9156251` (feat)
2. **Task 2: FIXTURE 섹션 LevelingEnabled INI save/load + CameraRole 보존** - `db3e3a7` (feat)

_Note: Task 1 은 plan 에서 tdd="true" 였으나 이 프로젝트에는 단위 테스트 프레임워크가 없어(CLAUDE.md "No test framework detected") 플랜의 `<verify>` 가 명시한 grep 기반 acceptance 로 RED/GREEN 을 대체했다. 모든 grep 기준 PASS._

**Plan metadata:** (this commit)

## Files Created/Modified
- `WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs` - LevelingEnabled 멤버 + 레벨링 각도 캐시 3종 + ClearDatumTransforms ResetLeveling 호출
- `WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs` - IsLevelingReference per-datum bool 플래그 ([Category("Datum|Leveling")])
- `WPF_Example/Custom/Sequence/Inspection/InspectionRecipeManager.cs` - FIXTURE 섹션 LevelingEnabled save/load + Preserve 신규레시피 분기 기본값

## Decisions Made
- **LevelingEnabled INI 직렬화 = FIXTURE 섹션 수동 키** (1/0 정수). DisplayName/DatumCount 와 동일 패턴. InspectionSequence 는 ParamBase 상속이 아니므로 자동 직렬화 불가 → 수동 키 필요.
- **IsLevelingReference 는 DatumConfig ParamBase reflection 자동 직렬화** (`_DATUM_{d}` 서브섹션). Save/Load 코드 추가 0 — InvertSign/TeachingImagePath bool/string 선례 동일.
- **레벨링 각도 캐시는 멤버 정의 + 리셋만 제공** — interface-first. 실제 시퀀스당 1회 산출 소비는 Plan 02(각도 산출)/Plan 03(전처리 회전)에서.

## CameraRole 보존 경로 확인 (plan Task 2 done 기준)
- **LevelingEnabled (FIXTURE 섹션)**: 비활성 시퀀스(타 CameraRole, `ResolveFixtureSequence`==null) 저장 시 `PreserveFixtureFromExisting` 진입. 기존 데이터 존재 분기 = L112 `saveFile[sectionPrefix] = existingFile[sectionPrefix]` 가 FIXTURE 섹션을 통째 복사 → LevelingEnabled 키 자동 보존(추가 코드 0). 신규 레시피(기존 데이터 없음) 분기엔 `LevelingEnabled = 0` 명시 추가.
- **IsLevelingReference (DATUM 서브섹션)**: `PreserveFixtureFromExisting` L115-119 의 `saveFile[datumSection] = existingFile[datumSection]` datum 서브섹션 통째 복사 경로로 보존됨. DatumConfig ParamBase 가 IsLevelingReference 를 `_DATUM_{d}` 에 저장하므로 섹션 복사가 함께 보존 — 추가 코드 0.
- MEMORY `project_recipe_datum_loss_camerarole` 회귀 표면 없음 (Datum 손실 버그와 동일 보존 경로 사용).

## Deviations from Plan

None - plan executed exactly as written. (Task 1 의 TDD 형식은 프로젝트에 단위 테스트 인프라가 없어 플랜이 명시한 grep 기반 검증으로 진행 — 플랜 `<verify>`/`<acceptance_criteria>` 가 grep 으로 정의되어 있어 deviation 아님.)

## Issues Encountered
None. `.ToBool()` API 가 IniFile(ParamBase.cs/SystemSetting.cs/DisplayConfig.cs 선례)에 존재함을 grep 으로 확인 → `.ToInt() == 1` 대체 불필요.

## Known Stubs
- 레벨링 각도 캐시(`_levelingAngleRad`/`_levelingComputed`)는 현재 정의 + 리셋만 존재하고 실제 각도를 산출/소비하는 코드는 없음. **이는 의도된 interface-first stub** — Plan 02(각도 산출)에서 `SetLevelingAngle()` 호출, Plan 03(전처리 회전)에서 `LevelingAngleRad`/`LevelingComputed` 소비. 플랜 objective 에 명시된 설계.

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- Plan 02(레벨링 각도 산출)가 소비할 설정/캐시 멤버 준비 완료: `LevelingEnabled` 게이트, `IsLevelingReference` 기준 datum 선별, `SetLevelingAngle()` 캐시 write.
- Plan 03(전처리 회전 삽입)가 소비할 `LevelingAngleRad`/`LevelingComputed` 준비 완료.
- INI 영속/CameraRole 보존 회귀 0 — 기존 레시피 로드 시 LevelingEnabled=false 폴백 검증.

## Self-Check: PASSED

- 3 modified source files FOUND
- 52-01-SUMMARY.md FOUND
- Commits 9156251, db3e3a7 FOUND in git log

---
*Phase: 52-datum-2026-06-16-poc-1*
*Completed: 2026-06-17*
