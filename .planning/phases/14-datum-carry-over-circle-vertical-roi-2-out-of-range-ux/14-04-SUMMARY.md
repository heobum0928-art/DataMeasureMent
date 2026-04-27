---
phase: 14-datum-carry-over-circle-vertical-roi-2-out-of-range-ux
plan: 04
status: complete
date: 2026-04-27
requirements: [SPEC-14-Req-4]
commits:
  - db01dec feat(14-04): DatumConfig Circle polar-sampling 3 신규 필드 (PolarStepDeg/RectL1/L2Ratio)
  - 0abe32e feat(14-04): VisionAlgorithmService TryFindCircleByPolarSampling 신규 메서드 (additive)
  - 2aa2990 feat(14-04): RunPhiSmokeTest harness — D-13 phi 부호 검증용 (production 영향 0)
files_modified:
  - WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs
  - WPF_Example/Halcon/Algorithms/VisionAlgorithmService.cs
key-files:
  created: []
  modified:
    - WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs
    - WPF_Example/Halcon/Algorithms/VisionAlgorithmService.cs
---

# Plan 14-04 Summary — TryFindCircleByPolarSampling 신규 알고리즘 (legacy carry-over closure 토대)

## Goal Achieved

Phase 13 D-VIZ-03 carry-over closure 토대: legacy `VisionAlgorithmService.TryFindCircle` 가
raw 에지점을 미반환 → CircleTwoHorizontal Circle ROI 노란 raw 점이 빈 HTuple 인 결함을
신규 polar-sampling 알고리즘으로 대체할 수 있는 토대 마련. 신규 메서드는 raw 에지점을
HTuple out 파라미터로 반환. legacy `TryFindCircle` 시그니처 보존 (additive only).

## What Was Built

### Task 1 — DatumConfig.cs (commit db01dec)
Datum|Circle (CTH) ROI 카테고리에 신규 3 필드:
- `Circle_PolarStepDeg` (default 10°) — 회전 각도 step
- `Circle_RectL1Ratio` (default 0.05) — 반경 방향 사각형 ROI length1 비율
- `Circle_RectL2Ratio` (default 0.05) — 접선 방향 사각형 ROI length2 비율

INI 미존재 시 default 자동 fallback. Sanity clamp 은 메서드 진입부에서 처리.

### Task 2 — VisionAlgorithmService.cs (commit 0abe32e)
신규 메서드 `TryFindCircleByPolarSampling` (legacy `TryFindCircle` 직후 추가):
- 360° polar sampling: center+radius 기점에서 stepDeg 회전, 각 각도 θ 에서 작은 사각형
  GenMeasureRectangle2 + MeasurePos 첫 에지점 1개 누적 → FitCircleContourXld.
- raw 에지점 (HTuple edgeRows / edgeCols) out 반환.
- D-13 좌표계 (CCW, 화면 시점):
  rectRow = cRow - radius * Sin(θ), rectCol = cCol + radius * Cos(θ), rectPhi = θ_rad.
- Sanity clamp 진입부: stepDeg(0~30°), L1/L2Ratio(>0), sigma(≥0.4), threshold(>0).
- Datum transform: legacy 패턴 (center 만 변환, radius 무변환).
- try/catch (Exception ex) → return false. per-step swallow. finally CloseMeasure + Dispose.
- legacy `TryFindCircle` 시그니처 보존 (CircleDiameterMeasurement.cs:49 호환, ALG-04 회귀 0).

### Task 3a — RunPhiSmokeTest harness (commit 2aa2990)
- 4 angle (0°/90°/180°/270°) 에서 좌표식을 Trace 로그로 출력하는 진단용 harness.
- production 영향 0 — 외부 호출자가 명시적으로 호출해야만 활성. PASS 후 호출 주석 처리하여
  메서드는 보존 (회귀 시 재호출 가능).

## Verification

- **Build:** `MSBuild Debug/x64` exit 0, 0 errors, 신규 warning 0 (3 task 각각 build).
- **Task 2 빌드 에러 발견 + 해결:** ELogType 네임스페이스를 Define 으로 잘못 적용하여 빌드 에러
  발생. 실제 위치 `ReringProject.Setting.ELogType` 으로 수정 후 빌드 성공 (commit 2aa2990).
- **SIMUL_MODE Smoke Test (Task 3b) — PASS:**
  - 4 angle 좌표 trace 로그 정확 출력.
  - Halcon Rectangle2 phi 부호 검증 — 4 rect 가 right/up/left/down 정확 배치.
  - delta ≤ 5px 모든 angle.
  - D-13 좌표계 부호식 (rectRow = cRow - r*Sin θ, rectCol = cCol + r*Cos θ, rectPhi = θ_rad)
    검증 완료.

## Acceptance Criteria

Plan acceptance criteria (frontmatter `truths`):
- [x] TryFindCircleByPolarSampling 신규 메서드 (Task 2)
- [x] 360° polar sampling, FitCircleContourXld 산출 (Task 2)
- [x] DatumConfig 3 신규 필드 (Task 1)
- [x] raw HTuple out 반환 (Task 2)
- [x] D-13 좌표계 부호식 (Task 2 코드 + Task 3b 검증)
- [x] Halcon Rectangle2 phi 부호 4점 smoke test PASS (Task 3b)

## Notable Deviations

- ELogType 네임스페이스 caveat: 계획서/CLAUDE.md 가 ELogType 위치를 명시하지 않아 처음
  `ReringProject.Define.ELogType` 으로 시도 → 빌드 실패. 실 위치는
  `ReringProject.Setting.ELogType` (SystemSetting.cs:18). 1 줄 수정 후 PASS. 향후 plans
  에서도 동일 caveat 주의.
- RunPhiSmokeTest 호출처: 영구 hidden 디버그 버튼 미추가. 계획서가 옵션 B (호출 라인 주석
  토글) 채택을 명시했으므로, 향후 회귀 검증 시 임시 호출 라인 추가 후 PASS 시 다시 주석 처리.

## Requirements Mapping

- **SPEC-14-Req-4** (legacy TryFindCircle carry-over closure 토대) — COVERED.

## Next

- Plan 14-05 (Wave 4, btn_testFindDatum 통합 검증 — 3 알고리즘 모두에서 검출 정상 시행 확인).
- 14-05 가 TryTeachCircleTwoHorizontal 의 legacy `TryFindCircle` 호출을 신규 polar 메서드로
  교체할지 여부는 14-05 plan 의 scope 에 따름.

