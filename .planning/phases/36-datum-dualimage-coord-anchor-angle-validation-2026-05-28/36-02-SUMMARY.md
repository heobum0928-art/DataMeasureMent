---
phase: 36-datum-dualimage-coord-anchor-angle-validation-2026-05-28
plan: 02
subsystem: datum
tags: [phase36, datum, dualimage, expectedangle, tolerance, transient, sentinel, wraparound, ini]

# Dependency graph
requires:
  - phase: 36-datum-dualimage-coord-anchor-angle-validation-2026-05-28
    plan: 01
    provides: "SameFrame guard (Find + Teach DualImage overload) — width/height 일치 검증 보장"
provides:
  - "EAngleValidationStatus enum (None/Pass/Fail) 신규 타입 + csproj Compile 등록"
  - "DatumConfig.ExpectedAngleDeg / AngleTolerance 입력 필드 2종 ([Category('Datum|Algorithm')], DualImage 한정 PropertyGrid 노출)"
  - "DatumConfig.AngleValidationStatus transient (Phase 17 D-13 3-종 데코 — INI/JSON 직렬화 제외)"
  - "IsHiddenForAlgorithm 의 TLI/CTH/VTH 3 case 에 hide 분기 — DualImage 만 두 필드 노출"
  - "TryFindVerticalTwoHorizontalDualImage 의 sentinel + wrap-around 평가 블록 (Tolerance > 0 → Pass/Fail, ==0 → None)"
affects:
  - 36-03 (PropertyGrid 색상 배지 + RenderDatumFindResult 화살표 — 본 plan 의 AngleValidationStatus / ExpectedAngleDeg 소비)
  - 36-04 (정식 UAT — INI 라운드트립 + Pass/Fail 시각화 검증)

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Sentinel 게이트 — Tolerance > 0.0 단일 조건 (Phase 14-02 TwoLineAngleToleranceDeg 1:1 답습)"
    - "Wrap-around 정규화 공식 — ((diff + 540.0) % 360.0) - 180.0 (CONTEXT 권고)"
    - "Transient 3-종 데코 — System.ComponentModel.Browsable(false) + PropertyTools.Browsable(false) + JsonIgnore (Phase 17 D-13)"
    - "신규 .cs 파일 → csproj Compile 명시 등록 (Phase 12 D-Rule3)"

key-files:
  created:
    - "WPF_Example/Custom/Sequence/Inspection/EAngleValidationStatus.cs"
  modified:
    - "WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs"
    - "WPF_Example/Halcon/Algorithms/DatumFindingService.cs"
    - "WPF_Example/DatumMeasurement.csproj"

key-decisions:
  - "EAngleValidationStatus enum 별도 파일 채택 (EDatumAlgorithm.cs 패턴 답습) — DatumConfig nested 또는 bool? 대안 미채택"
  - "EnsurePerRoiDefaults 본문 변경 0 — double CLR default 0.0 이 sentinel 의미와 자동 일치 (D-36-12/13)"
  - "Teach DualImage 본문에는 평가 블록 미추가 — D-36-08 = Test Find 한정"
  - "Find 자체는 PASS 유지 (error 반환 안 함) — TwoLineAngleToleranceDeg 와의 결정적 차이"
  - "DatumFindingService 의 simple name EAngleValidationStatus 사용 — 기존 using ReringProject.Sequence; 보유"

patterns-established:
  - "Sentinel 평가 블록 패턴: DetectedAngleDeg write 직후 → if (Tolerance > 0) wrap-around 후 Pass/Fail / else None"
  - "Hide 분기 1-라인 추가 정책: 새 DualImage 전용 필드는 비-DualImage 3 case 각각에 hide 1줄씩 — DualImage case 0줄 변경 (default 노출)"

requirements-completed:
  - SC-36-2
  - SC-36-4
  - D-36-05
  - D-36-06
  - D-36-07
  - D-36-08
  - D-36-12
  - D-36-13

# Metrics
duration: 25 min
completed: 2026-05-28
---

# Phase 36 Plan 02: DualImage 각도 검증 모델 + 평가 게이트 Summary

**DatumConfig 에 ExpectedAngleDeg/AngleTolerance 입력 필드 + AngleValidationStatus transient 도입 + TryFindVerticalTwoHorizontalDualImage 의 sentinel + wrap-around 자동 Pass/Fail 평가 게이트 추가 (CO-34.1-09 모델/알고리즘 단 완료)**

## Performance

- **Duration:** 약 25 min
- **Tasks:** 3
- **Files modified:** 4 (1 new + 3 modified)
- **Total lines added:** ~62 (enum 16 + DatumConfig 27 + DatumFindingService 19)

## Accomplishments

- `EAngleValidationStatus` enum (None/Pass/Fail) 신규 파일 + csproj 등록 — Phase 12 D-Rule3 패턴.
- DatumConfig 에 `ExpectedAngleDeg` (default 0.0) + `AngleTolerance` (default 1.0) 두 입력 필드 추가, DualImage 알고리즘 한정 PropertyGrid 노출.
- DatumConfig 에 `AngleValidationStatus` transient — Phase 17 D-13 3-종 데코 (Browsable false × 2 + JsonIgnore) 1:1 답습.
- TryFindVerticalTwoHorizontalDualImage 의 DetectedAngleDeg write-back 직후 sentinel 게이트 + wrap-around 정규화 평가 블록 (~17 라인) 삽입.
- 179° vs -179° → 2° 정상 판정 가능한 wrap-around 공식 `((diff + 540.0) % 360.0) - 180.0` byte-identical 적용.
- msbuild Debug/x64 PASS — 신규 warning 0 (Wave 1 baseline 유지).

## Task Commits

1. **Task 1: EAngleValidationStatus enum 신규 파일 + csproj 등록** — `e6cb1bd` (feat)
2. **Task 2: DatumConfig 필드 3종 + Hide 분기 3-라인** — `1076e5a` (feat)
3. **Task 3: TryFindVerticalTwoHorizontalDualImage sentinel + wrap-around 평가 블록** — `c903a04` (feat)

## Files Created/Modified

- `WPF_Example/Custom/Sequence/Inspection/EAngleValidationStatus.cs` (신규) — None=0/Pass=1/Fail=2 enum 정의
- `WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs` — 입력 필드 2 + transient 1 + hide 분기 3 라인
- `WPF_Example/Halcon/Algorithms/DatumFindingService.cs` — Find DualImage 평가 블록 (L731 직후 ~17 라인)
- `WPF_Example/DatumMeasurement.csproj` — `<Compile Include="...EAngleValidationStatus.cs" />` 한 줄 추가

## Decisions Made

- **EAngleValidationStatus 위치:** 별도 파일 `EAngleValidationStatus.cs` (PATTERNS §"5) transient" Claude's Discretion 권장안). EDatumAlgorithm.cs 와 동일 namespace `ReringProject.Sequence`. 결과: DatumConfig / DatumFindingService 모두 simple name 사용 가능 (기존 using 보유).
- **EnsurePerRoiDefaults 본문 변경 0:** PLAN 의 D-36-12/13 단일 sentinel 모델 — double CLR default 0.0 이 sentinel 의미 (Tolerance > 0 활성화) 와 자동 일치. Phase 22 IMG-01 string null 정규화의 형식적 답습 불요.
- **Teach 본문 미터치:** D-36-08 명시 — 평가 게이트는 Test Find 한정 (Teach 는 학습 단계로 검증 의미 없음).
- **wrap-around 공식 byte-identical:** PLAN/PATTERNS 둘 다 동일 공식 `((diff + 540.0) % 360.0) - 180.0` 명시 — 변형 없이 그대로 사용 (179°/-179° 경계 정상 판정 보장).
- **Find 의 PASS 여부 영향 0:** TwoLineAngleToleranceDeg 게이트와의 의도적 차이 — Phase 36 게이트는 transient AngleValidationStatus 에만 기록, Find 자체는 PASS 유지 (UI 색상 배지로만 표시 — Plan 03 분담).

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered

- **Worktree base 정렬 필요:** worktree branch 가 v1.0 archive 시점 (`5727bd8`) 에 있었음. Wave 1 merge 후 main HEAD = `4baf9fd` 와 동기화 필요 → `git reset --hard 4baf9fd` 로 정렬 후 작업 시작. (orchestrator 의 `EnterWorktree` 가 main 대신 archive 지점에서 분기한 패턴 — execute-plan.md 의 worktree_branch_check 매뉴얼 절차 그대로 적용.)
- **msbuild incremental cache:** 두 번째 빌드는 캐싱으로 1 warning 만 보고. 첫 번째 풀 빌드 출력으로 baseline 14 warnings 동일 유지 확인 (Phase 33 deprecated × 5 + CS0162 + MSB3884 × 2 + tmp csproj 중복 메시지). Phase 36 신규 warning 0.

## Verification

| 항목 | 결과 |
|---|---|
| `Test-Path EAngleValidationStatus.cs` | True |
| `grep -c "enum EAngleValidationStatus"` | 1 |
| `grep -c "None = 0"` | 1 |
| `grep -c "EAngleValidationStatus.cs" csproj` | 1 |
| `grep -c "public double ExpectedAngleDeg"` | 1 |
| `grep -c "public double AngleTolerance"` | 1 |
| `grep -c "EAngleValidationStatus AngleValidationStatus"` | 1 |
| Hide 분기 패턴 (TLI/CTH/VTH) | 3 |
| `grep -c "EAngleValidationStatus.Pass"` | 1 |
| `grep -c "EAngleValidationStatus.Fail"` | 1 |
| `grep -c "EAngleValidationStatus.None"` | 1 |
| `grep -c "((diff + 540.0) % 360.0) - 180.0"` | 1 |
| EnsurePerRoiDefaults 변경 | 0 라인 (git diff 확인) |
| VerticalTwoHorizontalDualImage hide case 본문 | 0 변경 (DualImage 필드 노출 유지) |
| Teach DualImage 본문 평가 블록 | 0 (Test Find 한정) |
| Guard 4 파일 변경 | 0 (ParamBase / InspectionListView / InspectionSequence / VisionResponsePacket) |
| msbuild Debug/x64 | exit 0, errors 0, 신규 warning 0 |

## User Setup Required

None - 외부 서비스 설정 불필요.

## Next Phase Readiness

- **Plan 36-03 (Wave 3)** 준비 완료. AngleValidationStatus / ExpectedAngleDeg / AngleTolerance 가 모두 선언 + write-back 보장 → HalconDisplayService RenderDatumFindResult 의 Expected angle 화살표 + PropertyGrid 색상 배지가 본 데이터 소비 가능.
- **Plan 36-04 (Wave 4 UAT)** 입력: INI 라운드트립 (Setting.ini 의 ExpectedAngleDeg=값 / AngleTolerance=값 키), 179°/-179° wrap-around 시나리오, sentinel 비활성 시나리오.

---
*Phase: 36-datum-dualimage-coord-anchor-angle-validation-2026-05-28*
*Plan: 02*
*Completed: 2026-05-28*
