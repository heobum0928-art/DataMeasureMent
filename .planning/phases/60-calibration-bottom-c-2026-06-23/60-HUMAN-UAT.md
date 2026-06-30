---
status: approved-pending-hw
phase: 60-calibration-bottom-c-2026-06-23
source: [60-VERIFICATION.md]
started: 2026-06-24
updated: 2026-06-30
---

## Current Test

[awaiting human testing]

## Tests

### 1. 실 피커 + Cal 지그 36-스텝 캘리브레이션
expected: 피커가 Cal 지그를 픽업한 채 10°×36스텝 회전 → 각 스텝 `TryAddStep`(지그 원 `fit_circle_contour_xld` → 중심) → `TryComputePickerCenter`(36점 편심원 atukey fit) → 피커센터(row,col) 산출 → Setting.ini `[ETHERNET_VISION]` PickerCenterRow/Col 저장. 산출 중심이 물리적으로 타당(이미지 내, 반경 정상).
result: [pending]

### 2. Bottom 보정 피커센터 반영 (캘 후) — 부호/방향 확정
expected: 캘 완료(피커센터 ≠ 0) 후 Bottom Run → 보정이 **피커센터를 회전중심**으로 적용(`HomMat2dRotate`). `PICKER_ROTATION_SIGN` 부호/회전방향을 **실 피커 컨트롤러 기준으로 확정**(현 기본 +1.0).
result: [pending]

### 3. 미캘 폴백 회귀 없음
expected: PickerCenterRow/Col = 0(미캘) 상태에서 Bottom Run 결과가 Phase 59(2-패턴 midpoint) 동작과 동일(byte-identical) — 회귀 0.
result: [pending]

### 4. Tray 정렬 회귀 없음
expected: Phase 60 적용 후 Tray 모드 결과가 Phase 59 기준치와 동일 — Tray 분기 미수정 확인.
result: [pending]

## Summary

total: 4
passed: 0
issues: 0
pending: 4
skipped: 0
blocked: 0

## Gaps

> 비고: 4건 모두 코드 결함 아님 — 실 피커 하드웨어 + 36-스텝 회전이 필요한 런타임 항목(SIMUL 단독 불가).
> 코드 must-have 2/2 검증 완료(60-VERIFICATION.md, human_needed). msbuild Debug/x64 PASS, anti-goal(VisionAlgorithmService/PatternMatchService/RecipeFileHelper/Grabber 무수정), 코드리뷰 clean(WR-01 HIGH[out-before-Save] + WR-02/03 + IN-02 수정).
> **AV-06(각도 캘) 폐기** — Phase 59 2-패턴 angle_lx 가 각도 산출, 별도 각도 캘 불필요(사용자 결정).
> Phase 61(TabControl UI) 완성 후 58/59/60 UAT 일괄. Test 2 부호/회전중심은 실 피커 컨트롤러 규약 확정 필요.
