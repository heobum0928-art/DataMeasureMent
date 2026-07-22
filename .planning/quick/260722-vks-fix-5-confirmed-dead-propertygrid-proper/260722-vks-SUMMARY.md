---
phase: 260722-vks
plan: 01
subsystem: ui
tags: [propertytools, propertygrid, halcon, measurement, fai, browsable]

# Dependency graph
requires: []
provides:
  - "5개 확정-dead PropertyGrid 프로퍼티 숨김 (CircleCenterDistance/CircleDiameter/CompoundAngle/CompoundCenterBDistance/CompoundCenterCDistance)"
  - "CompoundAngleMeasurement 의 DatumB 기준선 FAI-DatumLine 오버레이 (기존 결과 시각화 완성)"
affects: [inspection-recipe-editor, fai-measurement-propertygrid]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "확정-dead 프로퍼티 숨김: [PropertyTools.DataAnnotations.Browsable(false)] 어노테이션 재사용 (MeasurementBase.MeasCorrectionFactor 패턴), 필드/직렬화/기존 다른 어노테이션은 보존"
    - "ICustomTypeDescriptor 기반 조건부 hide 확장: 기존 IsHiddenForRadialDirection 의 if/else 분기 추가 (새 메커니즘 미도입)"

key-files:
  created: []
  modified:
    - WPF_Example/Custom/Sequence/Inspection/Measurements/CircleCenterDistanceMeasurement.cs
    - WPF_Example/Custom/Sequence/Inspection/Measurements/CircleDiameterMeasurement.cs
    - WPF_Example/Custom/Sequence/Inspection/Measurements/CompoundAngleMeasurement.cs
    - WPF_Example/Custom/Sequence/Inspection/Measurements/CompoundCenterBDistanceMeasurement.cs
    - WPF_Example/Custom/Sequence/Inspection/Measurements/CompoundCenterCDistanceMeasurement.cs

key-decisions:
  - "5개 파일 모두 established [PropertyTools.DataAnnotations.Browsable(false)] 어노테이션만 사용 — 새 hide 메커니즘 발명 없음"
  - "Fix B 는 기존 IsHiddenForRadialDirection 의 if/else 분기 확장으로 구현 — polar 경로에서만 EdgePolarity 숨김, legacy fit 경로(INI 하위호환)는 계속 노출"
  - "Fix C2 는 EdgeToLineAngleMeasurement.cs 의 FAI-DatumLine 패턴을 그대로 재사용 (datumInjected 가드 + 이미 계산된 daR1..daC2), resultValue/기존 오버레이 무변경"

patterns-established:
  - "PropertyGrid dead-property hide: 필드/타입/기본값/직렬화/[ItemsSourceProperty]/[DisplayName] 등 기존 어노테이션은 그대로 두고 [PropertyTools.DataAnnotations.Browsable(false)] 만 추가"

requirements-completed:
  - FIX-A-CCD-EdgePolarity
  - FIX-B-CDD-EdgePolarity
  - FIX-C1-CA-DeadEdge
  - FIX-C2-CA-DatumOverlay
  - FIX-D-CCB-DeadEdge
  - FIX-E-CCC-DeadEdge

# Metrics
duration: ~5min
completed: 2026-07-22
---

# Phase 260722-vks: PropertyGrid Dead-Property Fixes + Missing DatumB Overlay Summary

**5개 확정-dead PropertyGrid 프로퍼티([Browsable(false)])를 숨기고, CompoundAngleMeasurement 에 누락되어 있던 DatumB 기준선 FAI-DatumLine 오버레이를 순수 추가**

## Performance

- **Duration:** ~5 min (task execution span; commits 22:52:54 ~ 22:55:40 KST)
- **Started:** 2026-07-22T22:52Z (approx, first task read+edit)
- **Completed:** 2026-07-22T22:55:40+09:00
- **Tasks:** 5/5 completed
- **Files modified:** 5

## Accomplishments
- CircleCenterDistanceMeasurement / CircleDiameterMeasurement(polar 경로) / CompoundAngleMeasurement / CompoundCenterBDistanceMeasurement / CompoundCenterCDistanceMeasurement 의 결과에 영향 없는 dead 프로퍼티가 PropertyGrid 에 더 이상 편집 컨트롤로 노출되지 않음
- CircleDiameterMeasurement 는 기존 IsHiddenForRadialDirection 메커니즘 확장으로 legacy fit 경로(INI 하위호환)에서 EdgePolarity 표시를 계속 유지하면서 polar 경로에서만 숨김
- CompoundAngleMeasurement 에 이미 계산되어 있던 DatumB 기준선(daR1..daC2)이 FAI-DatumLine 오버레이 + DatumOrigin 마커로 렌더링되어, 각도 판정 기준선을 시각적으로 확인 가능
- 5개 태스크 모두 파일당 atomic 커밋, msbuild Debug/x64 신규 에러/경고 0으로 클린 빌드 확인

## Task Commits

Each task was committed atomically:

1. **Task A: Hide dead EdgePolarity in CircleCenterDistanceMeasurement** - `86ae929` (fix)
2. **Task B: Hide inert EdgePolarity in polar mode of CircleDiameterMeasurement** - `c037f62` (fix)
3. **Task C: Hide 6 dead Edge props + add missing DatumB overlay in CompoundAngleMeasurement** - `c93ecec` (fix)
4. **Task D: Hide 6 dead Edge props in CompoundCenterBDistanceMeasurement** - `3c51887` (fix)
5. **Task E: Hide 6 dead Edge props in CompoundCenterCDistanceMeasurement** - `00e3edc` (fix)

_Docs/state commit (SUMMARY.md, STATE.md) handled separately by the orchestrator, per plan constraints._

## Files Created/Modified
- `WPF_Example/Custom/Sequence/Inspection/Measurements/CircleCenterDistanceMeasurement.cs` - `EdgePolarity` 에 `[Browsable(false)]` + '왜' 주석 추가 (TryFindCircle polarity 인자 미참조 + legacy fit 분기 도달 불가)
- `WPF_Example/Custom/Sequence/Inspection/Measurements/CircleDiameterMeasurement.cs` - `IsHiddenForRadialDirection` 에 `else` 분기 추가 — `Circle_RadialDirection` non-empty(polar) 일 때 `EdgePolarity` hide
- `WPF_Example/Custom/Sequence/Inspection/Measurements/CompoundAngleMeasurement.cs` - 6개 dead Edge 프로퍼티 hide (C1) + FAI-DatumLine 오버레이 추가 (C2, 순수 additive)
- `WPF_Example/Custom/Sequence/Inspection/Measurements/CompoundCenterBDistanceMeasurement.cs` - 6개 dead Edge 프로퍼티 hide, `MeasureAxis` 는 계속 표시
- `WPF_Example/Custom/Sequence/Inspection/Measurements/CompoundCenterCDistanceMeasurement.cs` - 6개 dead Edge 프로퍼티 hide, `MeasureAxis` 는 계속 표시

## Decisions Made
- 모든 hide 는 established `[PropertyTools.DataAnnotations.Browsable(false)]` 패턴만 사용 (MeasurementBase.MeasCorrectionFactor 컨벤션 재사용), 필드/직렬화/기타 어노테이션([ItemsSourceProperty]/[DisplayName])은 보존
- Fix B 는 새 메커니즘을 만들지 않고 기존 `IsHiddenForRadialDirection` 의 `if(empty)/else(non-empty)` 분기로 확장
- Fix C2 는 sibling `EdgeToLineAngleMeasurement.cs` 의 `FAI-DatumLine` 패턴을 필드 순서/스타일까지 동일하게 재사용, `daR1..daC2` 를 재계산하지 않고 (3)에서 이미 계산된 값을 그대로 사용

## Deviations from Plan

None - plan executed exactly as written. All 5 tasks matched the plan's action/verify/done specification. No architectural changes, no additional bug fixes required, no blocking issues encountered.

## Issues Encountered

None. Each task's diff was re-read after edit to confirm `resultValue` calculation, existing overlays, and `TryExecute` logic were unchanged (constraint from orchestrator). Build was clean (0 new errors/warnings) after every task — only the 5 known pre-existing warnings (CS0618 x4 in Sequence_Top.cs/Sequence_Bottom.cs/SequenceHandler.cs, CS0162 x1 in VirtualCamera.cs) appeared, unrelated to this plan's files.

**Note (out-of-scope, not committed):** During each build, MSBuild's project-load step toggled `WPF_Example/DatumMeasurement.csproj`'s `DefineConstants` from `TRACE;DEBUG` to `TRACE;DEBUG;SIMUL_MODE` (a pre-existing local-machine dev setting per the file's own comment: "이 PC 는 카메라 없는 SIMUL 전용"). This is unrelated to the 5 measurement files in scope and was left unstaged/uncommitted, per the scope boundary rule (only auto-fix issues directly caused by this plan's changes).

## User Setup Required

None - no external service configuration required. Pure code-level PropertyGrid visibility + overlay change.

## Next Phase Readiness
- All 6 requirements (FIX-A through FIX-E, including C1/C2) satisfied and verified via clean build.
- No blockers. This quick task is independent of the in-progress Phase 68 (cross-Z dual-image) work — no files outside the 5 measurement files were touched.
- Recommended manual/UAT follow-up (not part of this quick task): open the recipe editor PropertyGrid for each of the 5 measurement types and visually confirm the dead properties no longer appear, and visually confirm the new FAI-DatumLine overlay renders for a CompoundAngle measurement with datum injected.

---
*Phase: 260722-vks*
*Completed: 2026-07-22*

## Self-Check: PASSED

All 5 modified files and the SUMMARY.md confirmed present on disk. All 5 task commit hashes (86ae929, c037f62, c93ecec, 3c51887, 00e3edc) confirmed present in git log.
