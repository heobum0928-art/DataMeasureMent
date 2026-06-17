---
phase: 52-datum-2026-06-16-poc-1
plan: 02
subsystem: vision-algorithm
tags: [leveling, datum, image-rotation, halcon, affine_trans_image]
requires:
  - "DatumConfig.IsLevelingReference (52-01)"
  - "InspectionSequence leveling 멤버 (52-01)"
  - "DatumFindingService.TryExtractEdgePoints + MIN_HORIZONTAL_EDGES (Phase 12)"
provides:
  - "DatumFindingService.TryGetLevelingAngle — 수평 2-ROI 피팅 레벨링 각도(radian) 산출"
  - "VisionAlgorithmService.RotateImageByAngle — affine_trans_image 임의각 이미지 회전 유틸"
affects:
  - "Plan 03 (Action_FAIMeasurement 회전 전처리 통합 — 이 두 메서드 호출)"
tech-stack:
  added: []
  patterns:
    - "각도 산출 = 기존 Math.Atan2(rEnd-rBegin, cEnd-cBegin) 재사용 (angle_lx 신규 호출 0)"
    - "임의각 회전 = HHomMat2D.HomMat2dRotate(회전중심) + HImage.AffineTransImage 인스턴스 메서드 (변환경로 1순위)"
    - "실패/근사0 = 무회전(원본 복사)/false 폴백, throw 0 (CLAUDE.md Never throw)"
key-files:
  created: []
  modified:
    - "WPF_Example/Halcon/Algorithms/DatumFindingService.cs (TryGetLevelingAngle +80)"
    - "WPF_Example/Halcon/Algorithms/VisionAlgorithmService.cs (RotateImageByAngle +32)"
decisions:
  - "변환경로 1순위 확정: HHomMat2D.HomMat2dRotate + HImage.AffineTransImage 인스턴스 메서드 — msbuild Debug/x64 PASS, fallback 미사용 (3분기 중 1개만 잔존)"
  - "경계처리 adapt_image_size=false (고정 크기) — taught ROI 좌표 정합 보존, 잘림 영역은 constant 배경"
  - "회전각 부호 중립 — 레벨링 방향(-levelingAngle) 부호 확정은 Plan 03 호출부로 위임"
metrics:
  duration: 5
  completed: 2026-06-17
---

# Phase 52 Plan 02: 레벨링 각도 산출 + 임의각 이미지 회전 유틸 Summary

레벨링 기준 Datum 의 수평 2-ROI concat 피팅 라인 각도(radian)를 기존 자산(FitLineContourXld + Math.Atan2)으로 산출하는 `TryGetLevelingAngle`, 그리고 `affine_trans_image` + `hom_mat2d_rotate`(회전중심=이미지 중심)로 임의각 회전 HImage 를 반환하는 `RotateImageByAngle` 유틸을 추가했다. Plan 03 통합이 이 두 메서드를 호출한다.

## What Was Built

### Task 1 — DatumFindingService.TryGetLevelingAngle (commit b3e4a01)
- `public bool TryGetLevelingAngle(HImage image, DatumConfig config, out double angleRad, out string error)`
- `TryFindVerticalTwoHorizontal` 의 **수평 A/B 피팅 구간만** 떼어내 재사용 (D-01 중복 구현 금지 충족). Vertical 검출 / IntersectionLl / ValidateHorizontalVerticalAngles / hom_mat2d 빌드 / DetectedOrigin transient 는 레벨링에 불필요하여 제외.
- `angleRad = Math.Atan2(hrE.D - hrB.D, hcE.D - hcB.D)` — angle_lx 등가, 기존 curAngle 패턴 재사용.
- 에지 합산 < `MIN_HORIZONTAL_EDGES`(10) → false + "insufficient edges" + angleRad 0.0. 예외 시 catch → false + ex.Message + 0.0 (throw 0). contour 는 finally 에서 dispose.

### Task 2 — VisionAlgorithmService.RotateImageByAngle (commit c7b3612)
- `public static HImage RotateImageByAngle(HImage src, double angleRad)`
- 회전중심 = 이미지 중심 `((height-1)/2, (width-1)/2)`. `HHomMat2D.HomMat2dRotate(angleRad, centerRow, centerCol)` → `src.AffineTransImage(mat, "constant", "false")` (변환경로 1순위 — 인스턴스 메서드, HObject 중간객체 없음).
- 근사 0(`System.Math.Abs(angleRad) < 1e-6`) → `src.CopyImage()` (보간 생략). 예외 시 catch → `src.CopyImage()` 무회전 폴백 (throw 0). 반환 HImage 는 src 와 독립 — 호출부 dispose 책임.
- `adapt_image_size="false"` 고정 크기 — taught ROI 좌표 정합 보존, 잘림 영역 constant 배경.

## Deviations from Plan

본 플랜은 직전 미완료 실행에서 두 메서드의 코드가 이미 작성(uncommitted)되어 있었다. 코드는 플랜의 1순위 경로와 byte-수준으로 일치했다. 이번 실행에서 빌드 검증(msbuild Debug/x64 PASS) → acceptance grep 전수 통과 → 태스크별 atomic 커밋으로 마무리했다. 코드 내용 변경 없음, 신규 deviation 없음.

- 변환경로: 1순위(`HHomMat2D` + `HImage.AffineTransImage` 인스턴스 메서드)가 컴파일 PASS → fallback 1/2 미사용. 최종 코드에 변환 경로 1개만 잔존 (acceptance "3개 분기 동시 존재 금지" 충족).

## Verification

- msbuild Debug/x64 Build: **PASS (0 errors)** — 1순위 변환 경로 컴파일 성공 확인.
- grep `public bool TryGetLevelingAngle(...)` → 1
- grep `Math.Atan2(hrE.D - hrB.D, hcE.D - hcB.D)` → 메서드 본문 포함 (재사용 각도 산출)
- TryGetLevelingAngle 본문 내 IntersectionLl / HomMat2d / DetectedOrigin → 0 (각도만 산출)
- grep `public static HImage RotateImageByAngle(...)` → 1
- grep `HomMat2dRotate` (Task 2) → 1, `AffineTransImage` → 1 (코드) + 1 (주석)
- 근사0 분기 `System.Math.Abs(angleRad) < 1e-6` → 1, catch 폴백 `src.CopyImage()` 존재
- `//260617 hbk Phase 52` 주석 양 메서드 포함

## Threat Mitigations Applied

- **T-52-03 (Tampering / malformed 각도)**: TryGetLevelingAngle 에지 부족 가드(MIN_HORIZONTAL_EDGES) + try/catch → false + angleRad 0.0; RotateImageByAngle 근사0/예외 시 원본 복사(무회전) — corrupt 각도가 측정 파괴 불가.
- **T-52-04 (DoS / 대용량 회전)**: accept (단일 사용자 오프라인 앱, 시퀀스당 1회 산출 — D-03).

## Self-Check: PASSED
- WPF_Example/Halcon/Algorithms/DatumFindingService.cs — FOUND (TryGetLevelingAngle present)
- WPF_Example/Halcon/Algorithms/VisionAlgorithmService.cs — FOUND (RotateImageByAngle present)
- commit b3e4a01 — FOUND
- commit c7b3612 — FOUND
