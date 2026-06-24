---
phase: 60-calibration-bottom-c-2026-06-23
fixed_at: 2026-06-24T00:00:00Z
review_path: .planning/phases/60-calibration-bottom-c-2026-06-23/60-REVIEW.md
iteration: 1
findings_in_scope: 4
fixed: 4
skipped: 0
status: all_fixed
---

# Phase 60: Code Review Fix Report

**Fixed at:** 2026-06-24
**Source review:** `.planning/phases/60-calibration-bottom-c-2026-06-23/60-REVIEW.md`
**Iteration:** 1

**Summary:**
- Findings in scope: 4 (WR-01, WR-02, WR-03, IN-02; IN-01 skipped per instruction)
- Fixed: 4
- Skipped: 0

## Fixed Issues

### WR-01: TryComputePickerCenter — out 파라미터를 Save() 성공 후에만 할당

**Files modified:** `WPF_Example/Custom/EthernetVision/PickerCenterCalibrationService.cs`
**Commit:** 899e4fe
**Applied fix:** `row = fittedRow; col = fittedCol; radius = fittedRad;` 3행을 `SystemSetting.Handle.Save()` 호출 아래로 이동. Save() 예외 시 catch 가 false 반환, out 파라미터는 메서드 진입부에서 설정한 안전 기본값(0) 유지.

---

### WR-02: TryAddStep — 복수 원 검출 경고 로그 추가

**Files modified:** `WPF_Example/Custom/EthernetVision/PickerCenterCalibrationService.cs`
**Commit:** 899e4fe
**Applied fix:** `fitRow.Length == 0` 체크 직후 `fitRow.Length > 1` 조건 추가, `ELogType.Error` 채널에 `[PICKER_CAL] TryAddStep: 복수 원 검출({0}) — [0] 사용` 로그 기록. [0] 사용 로직은 유지.

---

### WR-03: 미캘 판정 임계 단일소스 통일

**Files modified:** `WPF_Example/Custom/SystemSetting.cs`, `WPF_Example/Custom/EthernetVision/AlignShapeMatchService.cs`
**Commit:** 899e4fe
**Applied fix:**
- `SystemSetting.cs`에 `public const double PICKER_CENTER_ZERO_EPS = 1e-6` 신설 (//260624 hbk Phase 60 주석 포함).
- `AlignShapeMatchService.cs`의 `private const double PICKER_CENTER_ZERO_EPS = 1e-6` 선언 제거, 사용처(`ApplyPickerCenterCorrection` bUncalibrated 판정) 를 `SystemSetting.PICKER_CENTER_ZERO_EPS` 참조로 교체.
- `SystemSetting.RestorePickerCenterDefault`는 IN-02 수정(아래)으로 빈 if 블록이 제거되어 임계 비교 코드가 남지 않으나, 향후 복원 로직 추가 시 같은 const 사용 가이드를 주석으로 명시.

---

### IN-02: RestorePickerCenterDefault — 빈 if 블록 제거

**Files modified:** `WPF_Example/Custom/SystemSetting.cs`
**Commit:** 899e4fe
**Applied fix:** `bool bPickerCenterUncalibrated = ... if (bPickerCenterUncalibrated) { }` 빈 분기 전체 제거. 메서드 내부를 복원 불필요 이유를 설명하는 주석만으로 구성 — 정적 분석 경고 소거, 의도 명확화.

---

## Skipped Issues

### IN-01: ELogType.Camera 로그 채널 — 지시에 따라 스킵

**File:** `WPF_Example/Custom/EthernetVision/PickerCenterCalibrationService.cs:99,177`
**Reason:** 사용자 지시: "IN-01 (log channel — leave as ELogType.Camera)."
**Original issue:** 캘리브레이션 로그를 ELogType.Camera 채널에 기록하는 의미론적 불일치. 실제 버그 아님.

---

## Build Verification

```
MSBuild Debug/x64
Result: 0 errors, 0 warnings (exit code 0)
```

---

_Fixed: 2026-06-24_
_Fixer: Claude (gsd-code-fixer)_
_Iteration: 1_
