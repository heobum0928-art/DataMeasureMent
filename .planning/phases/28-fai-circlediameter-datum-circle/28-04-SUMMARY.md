---
phase: 28-fai-circlediameter-datum-circle
plan: 04
subsystem: uat-sign-off
tags: [uat, simul-mode, code-inspection, equivalence-verification, signed-off]

# Dependency graph
requires:
  - phase: 28
    plan: 01
    provides: "EdgeOptionLists.MapRadialDirectionToHalconPolarity helper + 4 FAI polar default consts (FaiCirclePolarStepDeg=10.0, RectL1Ratio=0.02, RectL2Ratio=0.02, EdgeSelection=\"First\")"
  - phase: 28
    plan: 02
    provides: "CircleDiameterMeasurement.Circle_RadialDirection PropertyGrid combo + TryExecute fit/polar branch (fit-path args byte-identical to v1.0)"
  - phase: 28
    plan: 03
    provides: "DatumFindingService.cs:200/:730 inline polarity ternary → helper call (3-way single source achieved)"
provides:
  - "28-UAT.md signed_off (4/4 PASS — Test 1 SIMUL UAT, Tests 2/3 code-inspection 사용자 합의, Test 4 auto msbuild)"
  - "Phase 28 cumulative sign-off — REQ-28-01 ~ REQ-28-06 모두 충족"
affects: [Phase 28 closeout (ROADMAP + STATE + REQUIREMENTS)]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Code-inspection UAT acceptance: 사용자 합의 시 SIMUL_MODE empirical UAT 를 mathematical-equivalence proof (byte-identical helper / default-equality construction) 로 대체. Tests 2/3 가 이 패턴의 첫 적용 사례 — Plan 02/03 SUMMARY 의 검증 기록 인용."

key-files:
  created:
    - ".planning/phases/28-fai-circlediameter-datum-circle/28-04-SUMMARY.md (this file)"
  modified:
    - ".planning/phases/28-fai-circlediameter-datum-circle/28-UAT.md (Tests 1/2/3 result PASS + frontmatter signed_off + Summary table 갱신)"
    - ".planning/STATE.md (Phase 28 SIGNED_OFF, plans 4/4)"
    - ".planning/ROADMAP.md (Phase 28 [x] + progress 4/4)"
    - ".planning/REQUIREMENTS.md (REQ-28-01 ~ REQ-28-06 backfill 등록 + signed_off mark)"

key-decisions:
  - "Tests 2/3 SIMUL_MODE empirical UAT 생략 — 사용자 합의로 Plan 02/03 SUMMARY 의 byte-identical / mathematical equivalence 증명을 코드 검증 근거로 인용하여 PASS 처리. Test 1 (PropertyGrid 시각) 만 사용자가 SIMUL UAT 로 직접 확인."
  - "REQ-28-* 가 REQUIREMENTS.md 에 미등록 상태였음 (Plan 01 SUMMARY note 확인) → Phase 28 sign-off 에 맞춰 backfill 등록 + signed_off mark 동시 처리."
  - "PointToLineDistance ROI 시각화 미구현 (`PointToLineDistanceMeasurement.cs:51-92` `overlays = new List<EdgeInspectionOverlay>()` 빈 리스트 반환) 은 Phase 7-01 D-03 anti-goal 결정이며 Phase 28 범위 밖 — backlog 로 carry-over."

patterns-established:
  - "SIMUL_MODE UAT 가 mathematical-equivalence 로 deterministic 한 경우, 사용자 합의 시 코드 인스펙션 (이전 Plan SUMMARY 의 byte-identical/default-equality 증명 인용) 으로 PASS 처리 가능. UAT.md 에 명시적 근거 본문 + 사용자 합의 일자 기록 필수 (transparency)."

requirements-completed: [REQ-28-01, REQ-28-02, REQ-28-03, REQ-28-04, REQ-28-05, REQ-28-06]

# Metrics
duration: 4min
completed: 2026-05-08
---

# Phase 28 Plan 04: SIMUL_MODE UAT + Phase 28 Sign-off Summary

**Phase 28 (FAI CircleDiameter + Datum Circle 알고리즘 통합) 의 4-Test UAT 를 종료하고 sign-off 처리. Test 1 (PropertyGrid 시각) 은 사용자 SIMUL_MODE UAT 로 PASS, Tests 2/3 (Datum 동등성 / v1.0 INI 회귀) 는 사용자 합의로 코드 검증 (Plan 02/03 SUMMARY 의 byte-identical / mathematical equivalence 증명 인용) PASS, Test 4 (msbuild) 는 Plan 04 Task 1 자동 수행 PASS. REQ-28-01 ~ REQ-28-06 모두 충족. Phase 28 SIGNED_OFF 2026-05-08.**

## Performance

- **Duration:** ~4 min (UAT 갱신 + SUMMARY 작성 + STATE/ROADMAP/REQUIREMENTS 갱신)
- **Started:** 2026-05-08 (Task 1 자동 PASS 시점은 19:41 — 휴면 후 sign-off 처리)
- **Completed:** 2026-05-08
- **Tasks:** 2 (Task 1 = UAT skeleton + Test 4 자동 / Task 2 = checkpoint:human-verify → 사용자 합의 후 코드 검증 PASS)
- **Files modified:** 4 (28-UAT.md, STATE.md, ROADMAP.md, REQUIREMENTS.md) + 1 created (this SUMMARY)

## Accomplishments

### Task 1 — UAT skeleton + Test 4 auto-PASS (commit `02adf80`)

- `.planning/phases/28-fai-circlediameter-datum-circle/28-UAT.md` 신규 생성: frontmatter + 4 Test scenarios (AC-1, AC-4, AC-5, AC-6) + Summary table.
- Test 4 (AC-6, msbuild Debug/x64) 자동 수행 → exit 0, 0 new CS errors, 0 new CS warnings (pre-existing 2 distinct unchanged), DatumMeasurement.exe mtime 갱신 → PASS 기록.
- Tests 1/2/3 은 `pending` 상태로 남기고 Task 2 (checkpoint:human-verify) 로 인계.

### Task 2 — Tests 1/2/3 사용자 합의 후 sign-off

- **Test 1 (AC-1) PASS — 2026-05-08 사용자 SIMUL UAT 확인**:
  - `bin\x64\Debug\DatumMeasurement.exe` 실행 → 검사 트리 → CircleDiameter FAI 추가 → PropertyGrid `Edge` 카테고리에서 Circle_RadialDirection 콤보 ▼ 표시 확인.
  - 콤보 펼침 → 옵션 정확히 `Inward`, `Outward` 2개 (3rd option 또는 빈 항목 없음). 기본 표시는 빈 값.
  - REQ-28-01 충족.

- **Test 2 (AC-4) PASS — 2026-05-08 코드 검증 (사용자 합의)**:
  - 사용자가 SIMUL_MODE empirical UAT 대신 코드 인스펙션으로 PASS 처리 명시 결정.
  - 근거 본문 (28-UAT.md Test 2 §"근거 (코드 검증)") 에 다음 인용:
    1. Plan 01 SUMMARY 의 4 FAI 폴라 default 상수 (`FaiCirclePolarStepDeg=10.0`, `FaiCircleRectL1Ratio=0.02`, `FaiCircleRectL2Ratio=0.02`, `FaiCircleEdgeSelection="First"`) 가 Datum CTH (`DatumConfig.Circle_*`) default 와 일치 (28-01-SUMMARY.md `## Accomplishments` 표).
    2. Plan 03 SUMMARY 의 helper 단일 소스 통일 — `DatumFindingService.cs:200`, `:730`, `CircleDiameterMeasurement.cs` 모두 `EdgeOptionLists.MapRadialDirectionToHalconPolarity(...)` 호출 (28-03-SUMMARY.md `## DRY Single-Source Achieved` 표).
    3. 결론 — 동일 입력 → 동일 HALCON 호출 (`TryFindCircleByPolarSampling` polarity/stepDeg/rectL1Ratio/rectL2Ratio/selection 인자 byte-identical) → 동일 결과 → `|D_fai − D_datum| ≡ 0` mm (deterministic, 수학적 동등성).
  - REQ-28-03 충족.

- **Test 3 (AC-5) PASS — 2026-05-08 코드 검증 (사용자 합의)**:
  - 사용자가 SIMUL_MODE empirical UAT 대신 코드 인스펙션으로 PASS 처리 명시 결정.
  - 근거 본문 (28-UAT.md Test 3 §"근거 (코드 검증)") 에 다음 인용:
    1. Plan 02 SUMMARY 의 byte-identical fit 분기 확인 — `Circle_RadialDirection` 빈 문자열 → `string.IsNullOrEmpty(Circle_RadialDirection)` true → 기존 `VisionAlgorithmService.TryFindCircle` 호출. Fit-path argument list `image, Circle_Row, Circle_Col, Circle_Radius, datumTransform, Sigma, EdgeThreshold, EdgePolarity, out foundRow, out foundCol, out foundRadius, out error` 가 v1.0 코드와 token-for-token identical (Plan 02 Task 2 commit `432adb2` `git diff -U10` 검증 기록, 28-02-SUMMARY.md `## Decisions Made` 인용).
    2. 기본값 `Circle_RadialDirection = ""` (Plan 02 Task 1 commit `578cab6`) 으로 Phase 28 키 없는 v1.0 INI 가 빈 문자열로 매핑 → fit 경로 자동 진입 → REQ-28-04 INI 하위호환 보장.
    3. 결론 — fit-path 인자열 byte-identical → HALCON 측 동작 동일 → 검출 결과 동일 → `resultValue = foundRadius * 2.0 * pixelResolution` 동일 → D_after ≡ D_before (회귀 0).
  - REQ-28-04 충족.

- **Test 4 (AC-6) PASS** — Task 1 에서 이미 자동 PASS (msbuild Debug/x64 exit 0, 0 new CS errors/warnings, DatumMeasurement.exe 생성).

- **28-UAT.md frontmatter 갱신**: `status: signed_off`, `passed: 4`, `failed: 0`, `pending: 0`, `signed_off_date: 2026-05-08`. Summary table 의 Tests 1/2/3 result 컬럼 PASS 갱신.

### Phase 28 cumulative requirement → commit/SUMMARY 매핑

| REQ | Description | Commits | SUMMARY |
|-----|-------------|---------|---------|
| REQ-28-01 | Circle_RadialDirection 필드 + PropertyGrid 콤보 (Inward/Outward) | `578cab6` (field+wrapper), Test 1 SIMUL UAT (2026-05-08) | 28-02-SUMMARY |
| REQ-28-02 | TryExecute 분기 (빈→fit / Inward,Outward→polar) | `432adb2` (branch), `84affbb`+`a894c36` (helper 3-way DRY) | 28-02-SUMMARY, 28-03-SUMMARY |
| REQ-28-03 | Datum CTH ↔ FAI 검출 직경 동등성 (≤ 0.001 mm) | Plans 01-03 (default-equality + helper byte-identical), Test 2 코드 검증 | 28-01-SUMMARY, 28-02-SUMMARY, 28-03-SUMMARY |
| REQ-28-04 | INI 하위호환 (RadialDirection 키 없는 v1.0 INI 회귀 0) | `432adb2` (fit-path args byte-identical), Test 3 코드 검증 | 28-02-SUMMARY |
| REQ-28-05 | PropertyGrid 정적 노출 (ICustomTypeDescriptor 미도입) | `578cab6` (field 추가), grep `ICustomTypeDescriptor` count 0 (Plan 02 Task 1 acceptance) | 28-02-SUMMARY |
| REQ-28-06 | msbuild Debug/x64 PASS + 0 new errors/warnings | 모든 Plan 01-04 task commit (각 task acceptance), Test 4 자동 PASS | 28-01/02/03-SUMMARY, this SUMMARY |

## Task Commits

1. **Task 1: 28-UAT.md skeleton + Test 4 auto-PASS** — `02adf80` (docs)
2. **Task 2: User checkpoint resume — Tests 1/2/3 PASS (사용자 합의)** — covered by docs commit (this SUMMARY + 28-UAT.md update + state/roadmap/requirements 갱신)

**Plan metadata commit:** included with sign-off doc commit at end of plan.

## Files Created/Modified

- **Created**: `.planning/phases/28-fai-circlediameter-datum-circle/28-04-SUMMARY.md` (this file)
- **Modified**:
  - `.planning/phases/28-fai-circlediameter-datum-circle/28-UAT.md` — frontmatter signed_off + Test 1 PASS (SIMUL UAT) + Tests 2/3 PASS (코드 검증 근거 본문 추가) + Summary table result 컬럼 갱신
  - `.planning/STATE.md` — Phase 28 SIGNED_OFF, plans 4/4, last_activity, current position, completed_phases/plans 카운터 +1/+4
  - `.planning/ROADMAP.md` — Phase 28 `[ ]` → `[x]` (signed off 2026-05-08), progress 4/4 plans complete
  - `.planning/REQUIREMENTS.md` — REQ-28-01 ~ REQ-28-06 backfill 등록 + Phase 28 traceability 행 신설 + signed_off mark

## Decisions Made

- **Tests 2/3 코드 검증 사용자 합의** — 사용자가 SIMUL_MODE empirical UAT (D:\1.bmp 로드 + Datum 티칭 + FAI 측정 + diff 계산) 를 생략하고 Plan 02/03 SUMMARY 의 byte-identical / mathematical equivalence 증명을 인용한 코드 인스펙션으로 PASS 처리하기로 명시. transparency 차원에서 28-UAT.md 의 Tests 2/3 result 본문에 인용 근거를 명시적으로 기록 (단순 PASS 만 표시하지 않음).
- **REQ-28-* backfill** — Phase 28 가 신설 phase 인데 REQUIREMENTS.md 에 등록되지 않은 상태였음 (Plan 01 SUMMARY note: "REQUIREMENTS.md has no REQ-28-* entries yet"). Sign-off 시점에 backfill 등록 + Phase 28 traceability 행 추가 + signed_off mark 동시 처리. Backfill 으로 v1.1 milestone REQ traceability 일관성 회복.
- **PointToLineDistance ROI 시각화 carry-over** — Phase 7-01 D-03 결정 (5 Measurement classes 빈 overlay 반환, EdgePairDistance 만 overlay 채움) 이 Phase 28 범위 밖. `WPF_Example/Custom/Sequence/Inspection/Measurements/PointToLineDistanceMeasurement.cs:51-92` `overlays = new List<EdgeInspectionOverlay>()` 빈 리스트 반환은 의도된 stub 이며 Phase 28 sign-off 를 차단하지 않음. backlog 로 carry-over.

## Deviations from Plan

- **Tests 2/3 SIMUL_MODE UAT → 코드 검증 합의** — 28-04-PLAN.md `<how-to-verify>` 는 사용자 SIMUL_MODE D:\1.bmp UAT 를 명시했으나, 사용자 명시 결정으로 코드 검증으로 대체. Rule 4 (architectural change) 에 해당하지 않음 — 검증 방법 변경만 있을 뿐 코드 변경 없음. PLAN frontmatter `must_haves.truths` 의 AC-4 / AC-5 검증 자체는 충족 (수학적 동등성으로 deterministic 보장).
- **REQ-28-* backfill (Rule 2 — missing critical functionality)** — sign-off 시점 발견. v1.1 milestone REQ traceability 가 RequirementsManager mark-complete 호출 가능 상태여야 하므로 backfill 등록은 필수.

## Issues Encountered

없음. UAT 갱신 + SUMMARY 작성 + STATE/ROADMAP/REQUIREMENTS 동시 갱신은 단일 작업 흐름.

## Build Verification

- 28-04-PLAN Task 1 (msbuild AC-6) 에서 이미 자동 검증 — exit 0, 0 new CS errors, 0 new CS warnings, pre-existing 2 distinct unchanged.
- 본 sign-off plan 은 documentation-only — 코드 변경 0 → 추가 빌드 검증 불필요.

## Acceptance Criteria Verification

| AC | Description | Source | Status |
|----|-------------|--------|--------|
| AC-1 | PropertyGrid Circle_RadialDirection 콤보 (Inward/Outward) | Test 1 SIMUL UAT 사용자 확인 | **PASS** |
| AC-2 | 빈 → grep TryFindCircle 1 / TryFindCircleByPolarSampling 0 (runtime fit 선택) | Plan 02 Task 2 acceptance grep | **PASS** (Plan 02) |
| AC-3 | Inward/Outward → grep TryFindCircleByPolarSampling 1 / TryFindCircle 0 (runtime polar 선택) | Plan 02 Task 2 acceptance grep | **PASS** (Plan 02) |
| AC-4 | Datum CTH ↔ FAI 검출 직경 동등성 (≤ 0.001 mm) | Test 2 코드 검증 (Plan 01 default-equality + Plan 03 helper byte-identical) | **PASS** |
| AC-5 | v1.0 INI 회귀 0 | Test 3 코드 검증 (Plan 02 fit-path args byte-identical) | **PASS** |
| AC-6 | msbuild Debug/x64 PASS, 0 new errors/warnings | Test 4 자동 PASS (Task 1) | **PASS** |

## Carry-over Items (Phase 28 범위 밖)

- **PointToLineDistance ROI 시각화 미구현**: `WPF_Example/Custom/Sequence/Inspection/Measurements/PointToLineDistanceMeasurement.cs:51-92` 의 `overlays = new List<EdgeInspectionOverlay>()` 빈 리스트 반환. Phase 7-01 D-03 (anti-goal: 5종 Measurement overlay 미구현) 의 의도된 결정이며 Phase 28 sign-off 를 차단하지 않음. 별도 backlog 항목으로 carry-over 권장 (해결 시 점/직선 측정의 시각적 디버깅 향상). v1.1 후속 phase 또는 별도 quick task 로 처리 가능.

## Cross-references

- **28-01-SUMMARY.md** (`be4d267`, `c786c4e`) — EdgeOptionLists helper + 4 polar default consts 추가. REQ-28-01/REQ-28-02 foundation.
- **28-02-SUMMARY.md** (`578cab6`, `432adb2`, `b615a97`) — CircleDiameterMeasurement field + branch. REQ-28-01/REQ-28-02/REQ-28-03/REQ-28-04/REQ-28-05/REQ-28-06 충족.
- **28-03-SUMMARY.md** (`84affbb`, `a894c36`, `f8b103a`) — DatumFindingService 2 inline ternary → helper. REQ-28-02/REQ-28-06 충족 (3-way single source).
- **28-SPEC.md** — 6 REQ + 6 AC 정의 (ambiguity 0.13).
- **28-CONTEXT.md** — D-01 ~ D-08 결정 + threat model.

## Next Phase Readiness

- **Phase 28 SIGNED_OFF**. Next milestone task = **Phase 20** (코드 스타일 정리, QUAL-02 + QUAL-04) per ROADMAP.md v1.1 sequence.
- **또는 backlog 처리**: PointToLineDistance ROI 시각화 미구현 (Phase 28 carry-over 항목).
- **No blockers** for Phase 20 startup.

## Self-Check: PASSED

- File `.planning/phases/28-fai-circlediameter-datum-circle/28-04-SUMMARY.md`: FOUND (this file)
- File `.planning/phases/28-fai-circlediameter-datum-circle/28-UAT.md`: signed_off (frontmatter status confirmed)
- Test 1 SIMUL UAT PASS line: 28-UAT.md `**Result**: PASS — 2026-05-08 사용자 확인 (SIMUL_MODE UAT)`
- Tests 2/3 code-inspection PASS lines + 인용 근거 본문: 28-UAT.md
- Test 4 auto PASS: 28-UAT.md (Task 1 commit `02adf80` 시점에 작성)
- Commit `02adf80` (Task 1): FOUND in `git log --oneline`
- All 4 Tests result = PASS, 0 pending

---
*Phase: 28-fai-circlediameter-datum-circle*
*Plan: 04*
*Completed: 2026-05-08*
