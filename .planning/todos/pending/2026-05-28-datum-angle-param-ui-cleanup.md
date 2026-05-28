---
created: 2026-05-28T10:12:27.048Z
title: Datum 각도 파라미터 UI 정리 (AngleTolerance 기본 OFF + TwoLineAngleToleranceDeg 숨김)
area: planning
files:
  - WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs:112
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

### 항목 2 — TwoLineAngleToleranceDeg PropertyGrid 숨김 (사용자 결정 2026-05-28)

`TwoLineAngleToleranceDeg` (DatumConfig.cs:112, 기본 10.0) 는 TwoLineIntersect datum 의 두 선 직각도(90°±N) 에러 게이트다. 안전장치로서 기능은 유효하지만, ExpectedAngle/AngleTolerance/수직도 등 각도 파라미터가 많아 PropertyGrid 에서 사용자가 헷갈린다.

결정: **검사 로직(직각 게이트)은 default 10° 로 그대로 동작 유지하되, PropertyGrid 에서만 숨김.**
- `DatumConfig` ICustomTypeDescriptor 의 IsHiddenForAlgorithm(또는 GetProperties 필터) 에 `TwoLineAngleToleranceDeg` 무조건 hide 추가 (ExpectedAngleDeg/AngleTolerance hide 패턴 L703/710/718 참조).
- 필드/직렬화/게이트 로직(DatumFindingService.cs:957~975)은 무변경 → 잘못된 datum 거부 안전망 보존, INI 회귀 0.
- 사용자가 값 조정할 일이 거의 없고 default 10° 로 충분.

두 항목 모두 Phase 38 "v1.1 Carry-over Cleanup 일괄" 의 #6(미사용/혼란 기능 정리) 로 흡수. 코드 수정은 /gsd-execute-phase 38 에서.
