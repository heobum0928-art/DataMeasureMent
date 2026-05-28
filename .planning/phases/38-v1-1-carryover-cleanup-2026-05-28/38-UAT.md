---
phase: 38-v1-1-carryover-cleanup-2026-05-28
type: uat
status: signed_off
source:
  - 38-03-PLAN.md
started: 2026-05-28
updated: 2026-05-28
auto_checks:
  msbuild_debug_x64: pass
  msbuild_new_warnings: 0
  startup_log_lines: 9 ([STARTUP] Step 1~8 + Total)
user_tests:
  total: 6
  pass: 5
  fail: 0
  partial: 0
  pending: 0
  carry_over: 1
carry_over_open:
  - "CO-38-01 — #5 픽셀분해능 단일소스 UI: Shot 단일값 편집 → 전체 FAI 일괄 적용 + 항목별 PixelResolutionX/Y UI 정리 (D-10 로딩 정규화 범위 초과분)"
  - "CO-38-02 — LoginManager 초기화 808ms lazy-load 검토 (38-STARTUP-ANALYSIS)"
  - "CO-38-03 — SequenceHandler 초기화 550ms 동기 의존성 분석 (38-STARTUP-ANALYSIS)"
  - "CO-38-04 — 실 하드웨어 [STARTUP] 재측정 (38-STARTUP-ANALYSIS)"
---

# Phase 38 UAT — v1.1 Carry-over Cleanup 일괄

> **Sign-off: SIGNED_OFF (partial, 2026-05-28).** 사용자 SIMUL_MODE(Debug/x64) 검증 6항목 중 5 PASS.
> UAT 5(픽셀분해능 단일소스 UI)는 D-10 합의 범위(로딩 시 정규화)는 달성됐으나 런타임/UI 단일소스 기대보다 좁아 **CO-38-01 carry-over** 로 이관(사용자 결정).
> 시작 지연 주범 2개 식별(LoginManager 808ms / SequenceHandler 550ms) — 전부 carry-over (38-STARTUP-ANALYSIS.md).

## Results Summary (2026-05-28)

| Total | Passed | Carry-over | Pending |
|-------|--------|-----------|---------|
| 6 | 5 | 1 | 0 |

| Test | 항목 | 결과 | 비고 |
|---|---|---|---|
| 1 | 시작 지연 계측 (#11, 성공기준 #4) | PASS | [STARTUP] 9줄 출력, Total 1509ms, 주범 2개 식별 |
| 2 | 측정 타입 정리 (#1, 성공기준 #1) | PASS | 미사용 5종 ComboBox 숨김, EdgeToLineAngle/CircleDiameter 노출 유지 |
| 3 | 각도 UI 정리 (#6, 성공기준 #2) | PASS | TwoLineAngleToleranceDeg 숨김 + 배지 기본 OFF + 직각 게이트 유지 |
| 4 | INI 하위호환 (성공기준 #5) | PASS | 구 레시피 로드 무오류 |
| 5 | 픽셀분해능 단일소스 UI (#5, 성공기준 #3) | CARRY-OVER | 항목별 PixelResolutionX/Y UI 잔존 + Shot 단일값 일괄적용 미동작 → CO-38-01 |
| 6 | 빌드 (자동) | PASS | msbuild Debug/x64 PASS, 신규 warning 0 |

## Auto Checks

| Check | Status | Detail |
|---|---|---|
| msbuild Debug/x64 | PASS | exit 0, DatumMeasurement.exe 생성, 신규 warning 0 (기존 CS0618/CS0162/MSB3884 만) |
| [STARTUP] 계측 라인 | PASS | grep "[STARTUP]" = 9 (Step 1~8 + Total), 요구 ≥4 충족 |

## User Tests (사용자 SIMUL UAT — 6 항목)

### Test 1 — 시작 지연 계측 (#11)
**Goal:** Initialize 8단계 경과시간이 로그에 기록되고 지연 주범 1개 이상 식별.
**Expected:** Trace 로그에 `[STARTUP] Step N ...: {ms} ms (cumulative), delta {ms} ms` 9줄 출력.
**Result:** **PASS** — 9줄 정상 출력, Total 1509ms. Step 5 LoginManager(delta 808ms) + Step 2 SequenceHandler(delta 550ms) = 약 90%. 상세 38-STARTUP-ANALYSIS.md.

### Test 2 — 측정 타입 정리 (#1)
**Goal:** FAI Measurement Type ComboBox 에서 미사용 5종 숨김.
**Expected:** EdgePairDistance/PointToLineDistance/PointToPointDistance/LineToLineAngle/LineToLineDistance 미노출, EdgeToLineAngle/CircleDiameter 노출.
**Result:** **PASS** (사용자 확인).

### Test 3 — 각도 UI 정리 (#6)
**Goal:** Datum 각도 검증 기본 OFF + TwoLineAngleToleranceDeg PropertyGrid 숨김 + 직각 게이트 유지.
**Expected:** 신규 Datum 각도 NG 배지 기본 미표시(AngleTolerance=0.0), TwoLineAngleToleranceDeg 미노출, TwoLineIntersect 직각 게이트 동작.
**Result:** **PASS** (사용자 확인).

### Test 4 — INI 하위호환 (성공기준 #5)
**Goal:** 기존 Phase 6 포맷 레시피 로드 무오류.
**Expected:** 파싱 오류 없이 정상 로드 (ReuseFromShotName 등 제거 키 포함해도 무시).
**Result:** **PASS** (사용자 확인).

### Test 5 — 픽셀분해능 단일소스 UI (#5)  → CARRY-OVER (CO-38-01)
**Goal:** Shot 단일 PixelResolution 을 전체 FAI 에 일괄 적용 + 항목별 분해능 UI 정리.
**Expected (사용자):** Shot 의 pixel resolution 을 바꾸면 모든 FAI 에 자동 일괄 적용되고, 항목별 PixelResolution UI 가 보이지 않음.
**Result:** **CARRY-OVER** — 항목별 UI(FAIConfig.PixelResolutionX `[Category("Calibration")]`)가 그대로 노출되고, Shot→FAI cascade 가 캘리브레이션 액션(ApplyCalibrationResult)에서만 발생해 단일값 편집이 일괄 반영되지 않음.
**판정 근거:** D-10 합의 범위는 "로딩 시 카메라 단일값으로 덮어쓰기"이며 38-01 이 이를 구현(LoadPhase6Format 정규화)해 **결정된 범위는 달성**. 사용자가 기대한 런타임/UI 단일소스(항목별 UI 숨김 + 편집 cascade 또는 측정경로 재배선)는 D-10 초과 범위로, v1.1 종결 직전 측정 경로 변경의 회귀 위험을 고려해 **CO-38-01 신규 phase(discuss→plan)로 이관** (사용자 결정 2026-05-28).

### Test 6 — 빌드 (자동)
**Result:** **PASS** — msbuild Debug/x64 exit 0, 신규 warning 0.

## Gaps

### CO-38-01 — #5 픽셀분해능 단일소스 UI (carry-over)
- **status:** carry_over
- **증상:** 항목별 PixelResolutionX/Y 가 PropertyGrid 에 개별 노출되고, Shot 단일값 편집이 전체 FAI 에 일괄 반영되지 않음.
- **범위:** ① FAIConfig.PixelResolutionX/Y PropertyGrid 숨김 ② ShotConfig.PixelResolution 을 단일 소스로 하는 cascade(편집 시) 또는 측정경로 재배선 ③ RoiDefinition/EdgePairDistanceMeasurement 소비 지점 정합.
- **참고:** D-09~D-11 (38-CONTEXT), 분배 코드 MainView.xaml.cs:2017-2041 (ApplyCalibrationResult), FAIConfig.cs:84-86, CameraSlaveParam.cs:26.
- **권장:** 신규 phase 로 discuss→plan (UI 정책 + 측정 단일소스 설계 결정 필요).
