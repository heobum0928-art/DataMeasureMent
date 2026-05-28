---
phase: 36-datum-dualimage-coord-anchor-angle-validation-2026-05-28
type: uat
status: pending
updated: 2026-05-28
auto_checks:
  guard_4_files_changed: 0
  msbuild_debug_x64: pass
  msbuild_new_warnings: 0
  msbuild_total_warnings: 14
user_tests:
  total: 7
  pass: 0
  fail: 0
  pending: 7
carry_over_resolves:
  - CO-34.1-01 (Side fixture 실측 검증)
  - CO-34.1-02 (Phase 34.1 Test 4 INI 라운드트립)
  - CO-34.1-09 (좌표계 + 각도 검증 UX)
---

# Phase 36 UAT — Datum DualImage 설계 보강

## Auto Checks (Task 1 완료 — 2026-05-28)

| Check | Status | Detail |
|---|---|---|
| Guard 4 files changed = 0 | PASS | `git diff 9c1caa8..HEAD` 기준 ParamBase.cs / InspectionListView.xaml.cs / InspectionSequence.cs / VisionResponsePacket.cs 모두 0 라인 |
| Baseline sanity (BLOCKER-2) | PASS | `git log 9c1caa8..HEAD -- WPF_Example/` = 8 commits, 모두 phase 36 작업 commit (422c6f6, 998e327, e6cb1bd, 1076e5a, c903a04, e76f985, 78e6b25, 6306967) |
| msbuild Debug/x64 Rebuild | PASS | exit 0, errors 0 |
| msbuild 신규 warning | 0 | total 14 (Phase 36 baseline 일치 — Phase 33 deprecated × 5 + CS0618/CS0162 + MSB3884 등 기존) |

## User Tests (사용자 SIMUL UAT — 7 시나리오)

### Test 1 — SameFrame 가드 동작 검증 (Plan 01)
**Goal:** DualImage Find/Teach 호출 시 width/height 불일치 입력에 대해 영문 에러 메시지로 즉시 차단.
**Steps:**
1. SIMUL 기동 → 임의 Datum 노드 → AlgorithmType = VerticalTwoHorizontalDualImage 선택
2. TeachingImagePath = (가로 페어, 임의 해상도 A) / TeachingImagePath_Vertical = (세로 페어, 다른 해상도 B) 의도적 mismatch 입력
3. Test Find 또는 Teach 실행
**Expected:** "DualImage requires same-frame image pair: horizontal {wH}x{hH} vs vertical {wV}x{hV}" 영문 에러 메시지 노출 (UI 에서는 FormatFindError 변환 한국어 가능)
**Result:** _pending_

### Test 2 — 실측 페어 Teach + Test Find PASS (Plan 02 + Plan 03 / SC-36-1)
**Goal:** Cal_Image/DualImageTest 실측 페어로 DetectedOrigin 이 시각적으로 의미 있는 위치에 표시.
**Steps:**
1. Datum 노드 → DualImage 선택
2. TeachingImagePath = `Cal_Image/DualImageTest/SIDE1_3-1_Datum_A1_A2_backlight_LV30.bmp` (가로축 페어)
3. TeachingImagePath_Vertical = `Cal_Image/DualImageTest/SIDE1_3-1_Datum_B1_backlight_LV30.bmp` (세로축 페어)
4. Vertical_* ROI 1개 + Horizontal_A_* ROI 1개 + Horizontal_B_* ROI 1개 티칭
5. Teach 실행 → 성공 확인 (LastTeachSucceeded=true)
6. Test Find 실행 → DetectedOrigin 좌표가 양쪽 캔버스에 purple 십자 + DetectedRefAngle 실선 화살표로 표시
7. swap Horizontal/Vertical 토글 → 양쪽 캔버스 모두 일관 시각화 유지 (Plan 03 Task 2)
**Expected:** 십자가 화면 안 의미 있는 위치 (실측 fixture 의 ref 점 근처) 에 표시. 좌표 텍스트 "Find (row, col)" 노출.
**Result:** _pending_

### Test 3 — ExpectedAngleDeg PASS 색상 배지 (Plan 02 + Plan 03 / SC-36-2, D-36-06)
**Goal:** AngleTolerance > 0 + |Detected-Expected| ≤ Tolerance 시 연두 배지 표시.
**Steps:**
1. Test 2 의 Teach + Test Find 결과 상태에서, PropertyGrid 에서 DetectedAngleDeg 값 (예: 0.3°) 확인
2. ExpectedAngleDeg = 0.0, AngleTolerance = 1.0 입력 (Plan 02 default)
3. Test Find 재실행
**Expected:** PropertyGrid 인접 색상 배지 = #43A047 연두 "각도 검증 OK" 표시. 시각화에 녹색 점선 Expected 화살표 추가 표시 (DetectedRefAngle 실선과 거의 일치).
**Result:** _pending_

### Test 4 — ExpectedAngleDeg FAIL 색상 배지 (Plan 02 + Plan 03 / SC-36-2, D-36-06)
**Goal:** |Detected-Expected| > Tolerance 시 연빨 배지 표시.
**Steps:**
1. Test 3 상태에서 ExpectedAngleDeg = 30.0 (Detected 와 차이 크게) 으로 변경
2. Test Find 재실행
**Expected:** 배지 = #E53935 연빨 "각도 검증 NG" 표시. 시각화에 빨강 점선 Expected 화살표가 실선과 시각적으로 어긋남.
**Edge case (INFO-2 — wrap-around 검증):** ExpectedAngleDeg=179, DetectedAngleDeg=-179 → wrap-around 공식 `|((Detected - Expected + 540) % 360) - 180|` = 2° → AngleTolerance=5 일 때 PASS 판정. 실측 페어로 -179° 검출 유도 어려울 시 단위 추적 (DatumFindingService 의 wrap-around 라인 grep) 으로 대체.
**Result:** _pending_

### Test 5 — OFF-SCREEN fallback (Plan 03 / SC-36-3, D-36-10)
**Goal:** DetectedOrigin 이 이미지 경계 밖일 때 캔버스 중앙 빨강 십자 + "OFF-SCREEN (row, col)" 표시.
**Steps:**
1. 의도적으로 mismatch 한 ROI 위치 또는 sentinel 0 값으로 Teach → DetectedOrigin 이 음수 또는 width/height 초과되도록 유도
2. Test Find 실행
**Expected:** purple 십자 미표시. 캔버스 중앙에 빨강 십자 + "OFF-SCREEN (row, col)" 텍스트 노출.
**Result:** _pending_

### Test 6 — INI 라운드트립 (Plan 02 / SC-36-4, D-36-14, CO-34.1-02 종결)
**Goal:** Recipe 저장 → 재기동 → Recipe 로드 시 신규 2 필드 + 기존 DualImage 6 필드 byte-identical 보존.
**Steps:**
1. Test 2~4 시나리오의 DualImage 설정 완성 상태 (ExpectedAngleDeg=30.0 / AngleTolerance=1.0 또는 임의값)
2. Recipe Save → 메뉴/버튼 통해 Setting.ini 저장
3. 프로그램 종료 → 재기동
4. 동일 Recipe Load → Datum 노드 선택 → PropertyGrid 6 + 2 필드 검증:
   - AlgorithmType = VerticalTwoHorizontalDualImage
   - Vertical_Row / Col / Phi / Length1 / Length2 (5 필드)
   - Horizontal_A_Row / Col / Phi / Length1 / Length2 (5 필드)
   - Horizontal_B_Row / Col / Phi / Length1 / Length2 (5 필드)
   - TeachingImagePath (가로 페어 절대경로)
   - TeachingImagePath_Vertical (세로 페어 절대경로)
   - **ExpectedAngleDeg (신규)**
   - **AngleTolerance (신규)**
**Expected:** 모든 값 저장 직전과 byte-identical. INI 파일에 `ExpectedAngleDeg=` + `AngleTolerance=` 키 각 1회 출현.
**Verify command (INI 검증):**
```powershell
Select-String -Path 'Setting.ini' -Pattern 'ExpectedAngleDeg='
Select-String -Path 'Setting.ini' -Pattern 'AngleTolerance='
```
**Result:** _pending_

### Test 7 — 1-image algorithm 회귀 smoke (SC-36-5)
**Goal:** TwoLineIntersect / VerticalTwoHorizontal / CircleTwoHorizontal 3종이 Phase 34.1 sign-off 시점과 동일 동작.
**Steps:**
1. AlgorithmType = TwoLineIntersect 선택 → PropertyGrid 에 ExpectedAngleDeg / AngleTolerance **숨김** 확인 (D-36-05). Line1/Line2 ROI 티칭 + Teach + Test Find smoke PASS
2. AlgorithmType = VerticalTwoHorizontal 동일 — ExpectedAngleDeg/AngleTolerance 숨김 + Vertical/Horizontal_A/B ROI Teach + Test Find smoke PASS
3. AlgorithmType = CircleTwoHorizontal 동일 — ExpectedAngleDeg/AngleTolerance 숨김 + Circle/Horizontal_A/B ROI Teach + Test Find smoke PASS
**Expected:** 3종 모두 PropertyGrid 회귀 0 (신규 2필드 숨김 확인) + Teach/Find 동작 정상.
**Result:** _pending_

## Carry-over Resolves

- **CO-34.1-01** (Side fixture 실측 검증) — Test 2 PASS 시 종결.
- **CO-34.1-02** (Phase 34.1 Test 4 INI 라운드트립) — Test 6 PASS 시 종결.
- **CO-34.1-09** (좌표계 + 각도 검증 UX) — Test 1 + 2 + 3 + 4 + 5 모두 PASS 시 종결.

## Notes

- 모든 시각화 검증은 사용자 시각 확인 (SIMUL 화면) 한정. 자동 픽셀 비교 도구 없음.
- 1-image algorithm 회귀 smoke 는 사용자 합의 시 Phase 34.1 sign-off 시점 동작 신뢰로 대체 가능 (Phase 36 코드 변경 영향 분석 — DatumFindingService 단일-이미지 오버로드 본문 0 라인 변경 + DatumConfig IsHiddenForAlgorithm 4 case 1줄씩만 추가).
- 실측 페어 자산 `Cal_Image/DualImageTest/SIDE1_3-1_Datum_A1_A2_backlight_LV30.bmp` + `SIDE1_3-1_Datum_B1_backlight_LV30.bmp` 는 현재 untracked — Phase 36 sign-off 전후 commit 권고 (별도 docs commit).
