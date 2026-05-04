---
phase: 14-datum-carry-over-circle-vertical-roi-2-out-of-range-ux
plan: 05
status: complete
date: 2026-04-27
requirements: [SPEC-14-Req-5]
commits:
  - da9ccbe feat(14-05): TryTeachCircleTwoHorizontal Circle 검출 폴라샘플링 교체 + raw 점 closure
files_modified:
  - WPF_Example/Halcon/Algorithms/DatumFindingService.cs
key-files:
  created: []
  modified:
    - WPF_Example/Halcon/Algorithms/DatumFindingService.cs
---

# Plan 14-05 Summary — btn_testFindDatum 3 알고리즘 통합 검증

## Goal Achieved

Phase 13 UAT Test 3 + D-VIZ-03 carry-over closure: btn_testFindDatum 으로 3 알고리즘
(TwoLineIntersect / CircleTwoHorizontal / VerticalTwoHorizontal) 모두에서 검출이 정상
시행됨을 SIMUL_MODE 사용자 검증으로 확정. 14-01..14-04 의 모든 기능이 통합 동작.

## What Was Built

### Task 1 — DatumFindingService.cs (commit da9ccbe)
TryTeachCircleTwoHorizontal 함수 안 Circle 검출 호출 교체:
- legacy `visionSvc.TryFindCircle` (raw 점 미반환) → 신규 `TryFindCircleByPolarSampling`
  (14-04 추가, raw 점 반환).
- 호출 인자: Circle_PolarStepDeg / RectL1Ratio / RectL2Ratio (14-04 신규 3 필드) 전달.
- raw 점 직접 write-back: config.Circle_DetectedEdgeRows/Cols = circleEdgeRows/Cols
  (이전엔 빈 HTuple — Phase 13-05 D-VIZ-03 이월 결함 closure).
- error literal "Circle fit failed: " 보존 (Phase 12 D-14 SPEC AC literal Req 5c, 회귀 0).
- D-11 진단 로그: 진입부 ROI + polar 파라미터 Trace 출력 (FAIL contingency 대비, PASS 후
  주석 처리 가능).
- 후속 수평 ROI / IntersectionLl / ValidateHorizontalVerticalAngles / RefOrigin 산출 변경 없음.
- TryTeachVerticalTwoHorizontal 변경 없음 (14-03 가 Vertical_* 슬롯 교체 완료).
- legacy TryFindCircle 시그니처 보존 (CircleDiameterMeasurement.cs:49 호환, ALG-04 회귀 0).

## Verification

- **Build:** `MSBuild Debug/x64` exit 0, 0 errors, 신규 warning 0.
- **SIMUL_MODE Final Integration UAT (Task 2) — 3 algorithms verified:**
  - 시나리오 1 TwoLineIntersect — LimeGreen RefOrigin/Angle + 주황 십자 + 14-02 회귀 검증 (60°
    fail) PASS.
  - 시나리오 2 CircleTwoHorizontal — LimeGreen RefOrigin + CircleCenter + Radius +
    **Circle raw 에지점 36개 (step=10° default) 가 360° 분포로 표시** (D-VIZ-03 closure).
    Circle_PolarStepDeg=1 변경 → 360개 표시 (시각적 연속 원).
  - 시나리오 3 VerticalTwoHorizontal — LimeGreen RefOrigin + 주황 십자 + Vertical(orange) +
    HorizA(green) + HorizB(lime green) raw 점 표시. 14-03 회귀 검증 (Vertical_EdgeThreshold
    변경 → 자동 재티칭) PASS.
  - FAI 측정 회귀 없음 — CircleDiameterMeasurement 포함 FAI 측정 PASS, Phase 13 Test 8
    시나리오 유지.
  - INI 저장/재시작 — 모든 레시피 로드 무오류.

## Acceptance Criteria

Plan acceptance criteria (frontmatter `truths`):
- [x] TryTeachCircleTwoHorizontal Circle 검출 호출 교체 완료 (Task 1)
- [x] Circle raw 점 직접 write-back (Task 1)
- [x] btn_testFindDatum 3 알고리즘 모두 PASS (Task 2 Scenario 1/2/3)
- [x] TwoLineIntersect: LimeGreen RefOrigin + 주황 십자, 회귀 없음 (Task 2)
- [x] CircleTwoHorizontal: LimeGreen RefOrigin + Circle raw 점 36개 (Task 2)
- [x] VerticalTwoHorizontal: LimeGreen RefOrigin + Vertical orange + HorizA/B color raw 점 (Task 2)
- [x] FAI 측정 경로 회귀 없음 (Task 2)

## Notable Deviations

- 진단 로그 Trace 출력은 PASS 후에도 코드에 보존 (주석 처리 가능 명시). 향후 회귀 시 재활성
  비용 0.

## Requirements Mapping

- **SPEC-14-Req-5** (3 알고리즘 통합 검증) — COVERED.

## Phase 14 Wrap-up

본 plan 으로 Phase 14 의 5 sub-phase 모두 완료. Datum 3 알고리즘 모두에서 ROI 인터랙션
(이동/resize) → 자동 재티칭 → 검출 결과 시각화 → btn_testFindDatum 통합 운영의 Phase 13
carry-over 결함이 closure.

