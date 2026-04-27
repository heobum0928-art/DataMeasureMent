---
phase: 14-datum-carry-over-circle-vertical-roi-2-out-of-range-ux
plan: 02
status: complete
date: 2026-04-27
requirements: [SPEC-14-Req-2]
commits:
  - 5a750fe feat(14-02): DatumConfig TwoLineAngleToleranceDeg PropertyGrid 필드 (default 10°)
  - 4a4ddda feat(14-02): TryTeachTwoLineIntersect 두 라인 직각성 게이트
files_modified:
  - WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs
  - WPF_Example/Halcon/Algorithms/DatumFindingService.cs
key-files:
  created: []
  modified:
    - WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs
    - WPF_Example/Halcon/Algorithms/DatumFindingService.cs
---

# Plan 14-02 Summary — TwoLineIntersect 두 라인 직각성 게이트

## Goal Achieved

Phase 13 UAT Test 2 carry-over (UX 갭) closure: TwoLineIntersect 알고리즘이 두 라인 각도
무검증으로 휘어진 라인에서도 datum origin 산출하던 문제 해결. CircleTwoHorizontal/
VerticalTwoHorizontal 의 ValidateHorizontalVerticalAngles 와 별개로, TwoLineIntersect 전용
직각성 게이트를 PropertyGrid 사용자 임계각으로 추가.

## What Was Built

### Task 1 — DatumConfig.cs (commit 5a750fe)
- 신규 PropertyGrid 필드 `TwoLineAngleToleranceDeg` (`[Category("Datum|Algorithm")]`,
  default `10.0`).
- `0` = 게이트 off (어떤 각도여도 PASS).
- `10` = default (90°±10° 허용).
- ParamBase reflection 자동 직렬화 → INI 키 미존재 시 default 10° 자동 적용 (회귀 0).

### Task 2 — DatumFindingService.cs (commit 4a4ddda)
`TryTeachTwoLineIntersect` 끝부분 (config.IsConfigured=true 직후, Line1Detected_* 저장 직전)
에 게이트 블록 삽입:
```
phi1 = Atan2(line1RowEnd-RowBegin, line1ColEnd-ColBegin)
phi2 = Atan2(line2RowEnd-RowBegin, line2ColEnd-ColBegin)
deltaDeg = |phi1 - phi2| * 180/π → [0,180) 정규화
perpErr = |deltaDeg - 90.0|
if (TwoLineAngleToleranceDeg > 0 && perpErr > tol):
    LastTeachSucceeded = false
    error = "Two-line angle out of range: <측정값> deg (expected 90 +/- <tol> deg)"
    return false
```
- 게이트 실패 시 Line1Detected_* 미저장 → 이전 성공 잔류 허용 (D-13 설계 보존).
- ValidateHorizontalVerticalAngles (CircleTwoHorizontal/VerticalTwoHorizontal 전용) 무관 —
  별 함수, 회귀 0.

## Verification

- **Build:** `MSBuild Debug/x64` exit 0, 0 errors, 신규 warning 0 (Task 1/2 각각 build).
- **SIMUL_MODE UAT (Task 3) — 모든 시나리오 PASS:**
  - Scenario 1 PropertyGrid 노출 — `Datum|Algorithm` 카테고리에 `TwoLineAngleToleranceDeg`
    default 10.0 표시.
  - Scenario 2 PASS (90° 두 라인) — `LastTeachSucceeded=true`, RefOrigin 산출.
  - Scenario 3 FAIL (60° 두 라인 + N=10) — fail 라벨 정확 형식, `LastTeachSucceeded=false`.
  - Scenario 4 OFF (N=0) — 게이트 통과, 어떤 각도여도 PASS.
  - Scenario 5 INI 하위호환 — Phase 4/11/12/13 기존 INI 로드 시 default 10° 자동 적용.

## Acceptance Criteria

Plan acceptance criteria (frontmatter `truths`):
- [x] `TwoLineAngleToleranceDeg` PropertyGrid 노출, default 10° (UAT Scenario 1)
- [x] 90°±N° 벗어남 → `LastTeachSucceeded=false` + fail 라벨 (UAT Scenario 3)
- [x] N=0 → 게이트 off (UAT Scenario 4)
- [x] INI 하위호환 (UAT Scenario 5)

## Notable Deviations

- 신규 필드 위치: 계획서 "AlgorithmType 직후" 명시되어 있었으나, `AlgorithmTypeList`
  (Browsable=false) + `AlgorithmTypeEnum` (Browsable=false) 헬퍼들과의 코드 응집을 위해
  `AlgorithmTypeEnum` 직후 (L66 부근) 에 삽입. PropertyGrid 노출 결과 동일
  (Browsable=false 헬퍼들은 그리드에 안 보임).

## Requirements Mapping

- **SPEC-14-Req-2** (TwoLineIntersect 직각성 게이트) — COVERED.

## Next

- Plan 14-03 (Wave 2, VerticalTwoHorizontal Vertical_* 슬롯 분리) — 14-02 와 같은 파일들 일부
  수정 예정 (DatumConfig.cs, DatumFindingService.cs). Wave 2 단독 실행이므로 wait.

