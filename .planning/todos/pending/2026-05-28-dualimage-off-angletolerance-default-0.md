---
created: 2026-05-28T10:12:27.048Z
title: DualImage 각도 검증 기본값 OFF (AngleTolerance 1.0→0.0)
area: planning
files:
  - WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs:126
---

## Problem

DualImage(VerticalTwoHorizontalDualImage) 의 `ExpectedAngleDeg`/`AngleTolerance` (Phase 36 추가, DatumConfig.cs:119/126) 가 기본값 `AngleTolerance=1.0` (켜짐) 이라, 사용자가 아무 설정 안 해도 가로축 각도가 0° 근처가 아니면 NG 색상 배지가 떠 혼란을 준다.

근본적으로 이 각도 검증은 대부분 중복이다:
- `ValidateHorizontalVerticalAngles` (DatumFindingService.cs:1530) 가 이미 두 에러 게이트 보유 — 가로선 수평도 `HORIZONTAL_TOLERANCE_DEG=15°` (DatumFindingService.cs:22) + 가로↔세로 직각도 `PERPENDICULAR_TOLERANCE_DEG=10°` (L25).
- DualImage 가로축 절대각(`DetectedAngleDeg`) 은 이미 15° 게이트로 0°±15° 강제됨 → `ExpectedAngleDeg=0` 배지 검증은 그것과 거의 동일.
- 고유 가치는 (a) 15°보다 더 타이트하게 가로각을 "보고" 싶을 때(tolerance<15 입력) 또는 (b) 비수평 기준 datum (Expected≠0) 케이스뿐. 단 배지일 뿐 검출 거부는 안 함.

## Solution

옵션 A 채택 (사용자 결정 2026-05-28): 기능은 유지하되 기본값을 OFF 로.
- `DatumConfig.cs:126` `AngleTolerance` 기본값 `1.0` → `0.0` (sentinel=off, DatumFindingService.cs:739 의 `if (config.AngleTolerance > 0.0)` 게이트가 0 이면 AngleValidationStatus=None 으로 배지 미표시).
- `ExpectedAngleDeg` 기본 `0.0` 유지.
- 특수 케이스(타이트 각도 확인/비수평 datum)만 사용자가 tolerance>0 직접 입력해 opt-in.
- 코드 1줄 변경. INI 하위호환: 기존 레시피에 AngleTolerance 키 있으면 그 값 우선(회귀 0), 신규/미존재 시 0.0.
- Phase 38 "v1.1 Carry-over Cleanup 일괄" 의 #6(미사용 기능 정리) 항목으로 흡수 예정. 코드 수정은 /gsd-execute-phase 38 에서.
