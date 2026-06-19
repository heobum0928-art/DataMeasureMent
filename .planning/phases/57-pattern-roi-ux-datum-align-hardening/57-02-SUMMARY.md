---
phase: 57-pattern-roi-ux-datum-align-hardening
plan: 02
subsystem: inspection
tags: [align, dualimage, pattern-match, datum, halcon, transform, lenient]

# Dependency graph
requires:
  - phase: 57-01
    provides: leveling 제거 (MoveZ→DatumPhase 직결, 동일 3파일 충돌 회피)
  - phase: 54-align
    provides: AlignPreTransform 소비 패턴 + TryComposeAlign rigid transform 산출
  - phase: 55-align-02
    provides: 2-패턴 baseline 각도
provides:
  - "DualImage datum 의 IsPatternAlignEnabled 시 단일 공유 transform align (가로축 매칭 → 가로/세로 ROI 모두 적용)"
  - "TryFindLine 의 AlignPreTransform 소비 (Vertical ROI 도 transform 보정)"
  - "DualImage align 실패 lenient (ALIGN_FAIL → NG, abort 없음)"
affects: [57-03, 57-04, 57-05]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "단일 DatumFindingService 인스턴스에 AlignPreTransform 1회 set → 가로(TryExtractEdgePoints)/세로(TryFindLine) 두 검출 모두 소비"
    - "TryComposeAlign 4-arg → 5-arg(refImageVertical) 위임 오버로드 (기존 호출처 회귀 0)"
    - "alignRot=0 시 enlarged AABB 가 기존 축정렬 bbox 정확 복원 (비-align 회귀 0)"

key-files:
  created: []
  modified:
    - WPF_Example/Halcon/Algorithms/DatumFindingService.cs
    - WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs
    - WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs

key-decisions:
  - "TryComposeAlign 에 5-arg(refImageVertical) 오버로드 추가 — 4-arg 는 refImageVertical=null 로 위임 (단일이미지 호출처 무변경, 회귀 0)"
  - "④단계 검출이 DualImage datum + refImageVertical!=null 이면 2-image TryFindDatum 으로 분기, 그 외 1-image"
  - "TryFindLine 에 TryExtractEdgePoints 와 동형의 AlignPreTransform 소비 이식 — AppendEdgePointsFromStrip 이미 alignRot optional 인자 보유, byte-identical 미러"
  - "#5 lenient 는 이미 구현됨 — DualImage 분기에 MarkAlignFailed 추가만 + 전반 검증 (코드 변경 없이 abort 0 / 종료 무조건 Grab 확인)"

patterns-established:
  - "DualImage 검출에서 단일 alignRigid 가 가로/세로 두 이미지에 글로벌 유효 (텔레센트릭 + Z축만 이동 셋업, D-02)"

requirements-completed: ["#4", "#5"]

# Metrics
duration: ~15min
completed: 2026-06-19
---

# Phase 57 Plan 02: DualImage ALIGN 이식 Summary

**Side VerticalTwoHorizontalDualImage datum 에 단일 공유 transform align 적용 — 가로축(imgH) 패턴매칭으로 산출한 rigid transform `(x,y,θ)` 를 가로/세로 검출 ROI 모두에 적용하고, align 실패 시 lenient NG 처리**

## Performance

- **Duration:** ~15 min
- **Started:** 2026-06-19T17:00:00Z (approx)
- **Completed:** 2026-06-19T17:15:00Z (approx)
- **Tasks:** 3
- **Files modified:** 3

## Accomplishments
- `TryFindLine` 에 `AlignPreTransform` 소비 이식 — DualImage 세로(Vertical) ROI 도 가로축에서 산출한 transform 으로 이동/회전 검출 (D-01 마지막 누락 고리). `TryExtractEdgePoints` 의 enlarged AABB + strip θ회전 패턴과 1:1 동형, `alignRot=0` 시 회귀 0
- `TryComposeAlign` 에 5-arg(`refImageVertical`) 오버로드 추가 + ④단계가 DualImage datum 일 때 2-image `TryFindDatum` 으로 분기 — 단일 `AlignPreTransform` 인스턴스 set 으로 가로(TryExtractEdgePoints)/세로(TryFindLine) 두 검출 모두 동일 transform 적용
- `Action_FAIMeasurement` DualImage 분기 deferred 게이트 해제 → `IsPatternAlignEnabled` 분기 추가 (단일이미지 분기 미러). enabled → align 단독 경로(imgH 패턴매칭), disabled → 기존 2-image 검출(off 회귀 0)
- #5 lenient 정책 검증 완료 — EStep.DatumPhase 모든 실패 분기 abort 0, 종료 무조건 `Step = (int)EStep.Grab`. DualImage align 실패도 `MarkAlignFailed` → 측정 NG(ALIGN_FAIL)
- SIMUL_MODE Debug/x64 Rebuild PASS (error CS 0, error MSB 0, 신규 warning 0, `DatumMeasurement.exe` 생성)

## Task Commits

각 task 원자적 커밋:

1. **Task 1: TryFindLine 에 AlignPreTransform 소비 이식** - `c079b4f` (feat)
2. **Task 2: TryComposeAlign DualImage 인지 검출 분기 + Action DualImage align 배선** - `4eeb71b` (feat)
3. **Task 3: #5 lenient 검증 + SIMUL_MODE Debug/x64 빌드 검증** - 코드 변경 없음 (lenient 이미 구현 — 검증만, 빌드 게이트 PASS, 산출물 gitignored — 별도 커밋 없음)

## Files Created/Modified
- `DatumFindingService.cs` - `TryFindLine` sanity clamp 직후 `AlignPreTransform` 소비 블록 삽입(roiRow/Col 이동 + alignRot 추출), bounding box 를 alignRot 반영 enlarged AABB(`Math.Abs(roiLength1*cosT)...`)로 교체, 두 strip 루프 `AppendEdgePointsFromStrip` 호출에 `alignRot` 전달
- `InspectionSequence.cs` - `TryComposeAlign` 5-arg(`HImage refImageVertical`) 오버로드 신설(4-arg 는 위임), ④단계 검출을 DualImage 인지 분기(2-image vs 1-image `TryFindDatum`)로 교체
- `Action_FAIMeasurement.cs` - EStep.DatumPhase DualImage 분기의 deferred 게이트 해제, `if (datum.IsPatternAlignEnabled)` 분기 추가(`TryComposeAlign(datum, imgH, imgV, modelPath, ...)` → `MarkAlignFailed`), else 기존 `TryRunSingleDatum` 경로 보존

## Decisions Made
- TryComposeAlign 4-arg → 5-arg 위임 오버로드로 단일이미지 호출처(Action_FAIMeasurement 1-image 분기) 무변경 보장 — 회귀 0
- ④단계는 `datum.AlgorithmTypeEnum == VerticalTwoHorizontalDualImage && refImageVertical != null` 일 때만 2-image, 그 외 기존 1-image (혼용 datum 안전)
- TryFindLine 의 bbox 명칭은 기존 halfW/halfH(=col/row half) 유지 — TryExtractEdgePoints 의 halfCol/halfRow 와 의미 동일(length1→col, length2→row), 공식의 length1/length2 배치 그대로 이식하여 alignRot=0 byte-identical
- #5 lenient 코드 변경 0 — DatumPhase 전 실패 분기가 이미 continue/Mark*Failed 로 끝나고 종료가 단일 `Step=Grab`, Measure 루프 게이트(IsDatumFailed → ClearResult + ALIGN_FAIL/DATUM_FAIL + LastJudgement=false + continue)가 NG 보장. DualImage 분기에 MarkAlignFailed 추가만 (Task 2 에 포함)

## Deviations from Plan

None - plan executed exactly as written.

(Task 1 의 bbox 명칭 차이(halfW/halfH vs halfCol/halfRow)는 plan 의 <action> 단계 3 에서 명시적으로 다뤄진 의도된 매핑이며 deviation 아님.)

## Threat Surface
- T-57-03 (DualImage transform 글로벌 유효성): accept — 텔레센트릭 + Z축만 이동 셋업 동일 픽셀 좌표계(D-02). SameFrame 가드(DatumFindingService 2-image 오버로드 width/height 불일치 차단) 유지.
- T-57-04 (align 실패 → 검사 중단): mitigated — DualImage align 실패 `MarkAlignFailed` 후 측정 진행(NG), abort 0. EStep.DatumPhase 종료 무조건 Grab 확인.
- T-57-05 (TryFindLine 비-align 회귀): mitigated — alignRot=0 시 enlarged AABB 가 기존 halfW=length1/halfH=length2 정확 복원, AppendEdgePointsFromStrip alignRot 기본 0. 빌드 게이트 PASS + TryExtractEdgePoints 동형.
- 신규 위협 표면 도입 없음 (기존 ALIGN 경로 DualImage 로 확장, 새 endpoint/auth/schema 0).

## Known Stubs
None — 기존 검증된 ALIGN 경로를 DualImage 로 확장. 신규 stub 0건.

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- DualImage ALIGN 이식 완료 — align-enabled DualImage datum 이 가로축 단일 transform 으로 가로/세로 ROI 모두 보정 검출.
- **UAT 잔여 (실데이터):** 텔레센트릭 셋업 실 Side DualImage 레시피로 (1) align-enabled 시 두 이미지 보정 검출 정확도, (2) align 실패 시 측정 진행(ALIGN_FAIL NG) 확인 필요. 비-align(off) 경로 회귀 0 은 빌드 게이트 + 코드 동형으로 확보, 실데이터 확증은 UAT.

## Self-Check: PASSED

- 3 modified source files + SUMMARY.md: all FOUND
- Task commits c079b4f, 4eeb71b: all FOUND
- grep AlignPreTransform DatumFindingService.cs: 6→10 (TryFindLine 신규 소비 1블록 추가 확인)
- grep TryComposeAlign(datum, imgH, imgV: 1 (DualImage align 호출 추가)
- grep deferred Action_FAIMeasurement.cs: 1 (게이트 해제 마커 주석만, 잔여 deferral 0)
- MSBuild Debug/x64 Rebuild: 0 error CS / 0 error MSB / 0 new warning, exe generated

---
*Phase: 57-pattern-roi-ux-datum-align-hardening*
*Completed: 2026-06-19*
