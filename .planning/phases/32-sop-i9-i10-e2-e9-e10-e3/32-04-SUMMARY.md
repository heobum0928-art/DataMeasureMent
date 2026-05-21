---
phase: 32-sop-i9-i10-e2-e9-e10-e3
plan: "04"
subsystem: measurement-algorithm
tags: [halcon, contour, canny, union-adjacent-contours, compound-measurement, datum]

# Dependency graph
requires:
  - phase: 32-sop-i9-i10-e2-e9-e10-e3
    plan: "01"
    provides: "VisionAlgorithmService.TryFindLargestContourRect 공통 컨투어 서비스 메서드"
  - phase: 32-sop-i9-i10-e2-e9-e10-e3
    plan: "02"
    provides: "IDatumOriginConsumer DatumDetectedCircleRow/Col 확장 + 전 구현 클래스 stub"
provides:
  - "E9 CompoundCenterCDistance: LargestRect 중심 → Datum C X 거리 (공통 컨투어 알고리즘)"
  - "E10 CompoundCenterBDistance: LargestRect 중심 → Datum B Y 거리 (공통 컨투어 알고리즘)"
  - "E2 CompoundAngle: LargestRect 중심 ↔ DatumC 검출 원중심 대각선 vs DatumB 각도"
affects: ["32-05", "32-06", "action-fai-measurement", "measurement-factory", "mainview-roi-teaching"]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "공통 컨투어 알고리즘(canny→union_adjacent→LargestRect) 기반 측정 패턴 — E2/E9/E10 공유"
    - "DatumDetectedCircleRow/Col 주입 채널 — E2 실사용, E9/E10 주입만 수신"
    - "Contour 카테고리 파라미터 PropertyGrid 노출 패턴"

key-files:
  created: []
  modified:
    - "WPF_Example/Custom/Sequence/Inspection/Measurements/CompoundCenterCDistanceMeasurement.cs"
    - "WPF_Example/Custom/Sequence/Inspection/Measurements/CompoundCenterBDistanceMeasurement.cs"
    - "WPF_Example/Custom/Sequence/Inspection/Measurements/CompoundAngleMeasurement.cs"

key-decisions:
  - "CL2/CL3 원피팅 + La/Lb 라인 + midline 교점 체인 전면 폐기 — TryFindLargestContourRect 단일 호출로 대체"
  - "E2 DatumC 원중심 미주입(0,0) 시 명시적 error 반환 — T-32-07 mitigation (CircleTwoHorizontal datum 전제 안전 종결)"
  - "Contour 파라미터 4종(CannyAlpha/CannyLow/CannyHigh/UnionDistance) PropertyGrid 사용자 편집 노출 — CONTEXT.md Q5 확정"
  - "CompoundCenter E9/E10 의 DatumDetectedCircleRow/Col 는 주입만 받고 미사용 — E2 전용 채널"

patterns-established:
  - "공통 컨투어 측정 클래스 패턴: Rect ROI 5필드 + Contour 4파라미터 + TryFindLargestContourRect 호출 + ComputeProjectionDistance/AngleLineLine"

requirements-completed: []

# Metrics
duration: 25min
completed: 2026-05-21
---

# Phase 32 Plan 04: E2/E9/E10 Compound 측정 공통 컨투어 알고리즘 재작성 Summary

**CL1~CL3 원피팅 + La/Lb 라인 + midline 교점 체인 기하 체인을 폐기하고 공통 컨투어 알고리즘(canny→union_adjacent_contours→LargestRect 중심) 단일 패턴으로 E2/E9/E10 측정 클래스 3종 전면 재작성**

## Performance

- **Duration:** 약 25분
- **Started:** 2026-05-21T06:15:00Z
- **Completed:** 2026-05-21T06:40:00Z
- **Tasks:** 2
- **Files modified:** 3

## Accomplishments

- E9(CompoundCenterCDistance) / E10(CompoundCenterBDistance) 재작성 — Rect ROI 1개 + TryFindLargestContourRect → LargestRect 중심 → Datum C/B 거리 측정
- E2(CompoundAngle) 재작성 — LargestRect 중심 ↔ DatumC 검출 원중심 대각선 vs DatumB 기준선 각도 1개 산출
- canny/union 파라미터 4종(CannyAlpha/CannyLow/CannyHigh/UnionDistance) PropertyGrid 사용자 편집 노출
- DatumC 원중심 미주입 안전 종결 (T-32-07 mitigation) — "DatumC detected circle center not injected" 명시 오류
- CL1~CL3 / La/Lb 필드 + TryComputeChainPoint 헬퍼 완전 제거 (350줄 → 157줄)
- msbuild Debug/x64 PASS — 신규 오류 0건

## Task Commits

1. **Task 1: CompoundCenterC/B(E9/E10) 재작성** - `4b9e9d2` (feat)
2. **Task 2: CompoundAngle(E2) 재작성** - `eaeba21` (feat)

## Files Created/Modified

- `WPF_Example/Custom/Sequence/Inspection/Measurements/CompoundCenterCDistanceMeasurement.cs` — CL/La/Lb 체인 폐기, Rect ROI + Contour 파라미터 + TryFindLargestContourRect 재작성 (E9 Datum C X 거리)
- `WPF_Example/Custom/Sequence/Inspection/Measurements/CompoundCenterBDistanceMeasurement.cs` — 동일 재작성, TypeName/MeasureAxis 기본값만 차이 (E10 Datum B Y 거리)
- `WPF_Example/Custom/Sequence/Inspection/Measurements/CompoundAngleMeasurement.cs` — CL1~CL3/La/Lb 체인 폐기, Rect ROI + Contour 파라미터 + TryFindLargestContourRect + AngleLineLine (E2 각도)

## Decisions Made

- **CL/La/Lb 체인 폐기:** CONTEXT.md 확정 스펙 3/5번 — ROI 1개 공통 컨투어 알고리즘이 현장 실무 SOP. INI 하위호환 불필요 (Phase 31 신규 타입).
- **DatumDetectedCircleRow/Col E9/E10 미사용:** CompoundCenter 는 Datum 거리 측정 — 검출 원중심 불필요. 주입만 수신하고 미참조 (IDatumOriginConsumer 인터페이스 준수).
- **Contour 카테고리 분리:** Edge 파라미터와 별도 Category("Contour") 로 PropertyGrid 구분 노출.

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered

없음. 빌드 기존 경고(MSB3884 MinimumRecommendedRules.ruleset, CS0162 VirtualCamera SIMUL_MODE) 2건은 Phase 32 이전부터 존재하는 항목으로 신규 회귀 아님.

## Known Stubs

없음. E9/E10 의 DatumDetectedCircleRow/Col 는 의도적 미사용 필드 (IDatumOriginConsumer 인터페이스 이행용 — E2 전용 채널).

## Threat Flags

| Flag | File | Description |
|------|------|-------------|
| T-32-07 mitigated | CompoundAngleMeasurement.cs | DatumDetectedCircleRow/Col 미주입(0,0) 시 명시적 error 반환 — 안전 종결 구현 완료 |

## Next Phase Readiness

- Phase 32 Wave 2 완료 (Plan 03 ArcLineIntersect + Plan 04 Compound 3종). Wave 3 준비 완료.
- 다음: Plan 05 (E3 CompoundShortAxisDistance 신규) + Plan 06 (ROI 티칭 배선 + MeasurementFactory 등록)
- Wave 2 전체 msbuild PASS 확인됨

---
*Phase: 32-sop-i9-i10-e2-e9-e10-e3*
*Completed: 2026-05-21*
