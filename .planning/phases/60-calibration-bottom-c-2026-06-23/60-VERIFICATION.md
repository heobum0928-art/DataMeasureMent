---
phase: 60-calibration-bottom-c-2026-06-23
verified: 2026-06-24T00:00:00Z
status: human_needed
score: 2/2 must-haves verified
overrides_applied: 0
human_verification:
  - test: "실 피커 + Cal 지그 36-스텝 회전 캘리브레이션 실행"
    expected: "TryAddStep 36회 → TryComputePickerCenter → PickerCenterRow/Col 비-0 값으로 INI 저장; 앱 재기동 후 값 유지"
    why_human: "SIMUL_MODE 에는 실 피커 회전 시퀀스 없음. 36 Cal 지그 이미지가 필요하며 편심원 피팅 결과(피커센터 row/col)가 물리적으로 타당한지 육안 확인 필요."
  - test: "Bottom 정렬 보정 피커센터 반영 확인 (캘 후)"
    expected: "PickerCenterRow/Col 비-0 저장 후 Bottom 정렬 시, ApplyPickerCenterCorrection 이 호출되고 기존(Phase 59) 결과와 다른 OffsetXmm/Ymm 산출 → 피커 착지 위치 정확도 향상 확인"
    why_human: "PICKER_ROTATION_SIGN=+1.0 (기본값) 및 회전중심 규약이 실 피커 컨트롤러 기준과 일치하는지 Phase 61 UAT 에서 부호·방향 확정 필요."
  - test: "미캘 폴백 확인 (PickerCenterRow/Col = 0)"
    expected: "INI 를 PickerCenterRow=0 / PickerCenterCol=0 으로 리셋 후 Bottom 정렬 시 Phase 59 동작과 byte-identical (OffsetXmm = dCol*resMm, OffsetYmm = dRow*resMm)"
    why_human: "폴백 경로 코드는 검증되었으나, 실 피커 환경에서 회귀 없음 확인 필요."
  - test: "Tray 정렬 회귀 없음 확인"
    expected: "Phase 60 후 Tray 모드 정렬 결과(OffsetXmm/Ymm, HasTheta=false)가 Phase 59 결과와 동일"
    why_human: "Tray 브랜치 코드는 수정 없음 확인되었으나, 실 Tray 정렬 시나리오로 회귀 없음 최종 확인 필요."
---

# Phase 60: Calibration — Bottom (C) Verification Report

**Phase Goal:** Bottom Align 피커 센터 캘리브레이션 — 피커가 지그를 10°×36스텝 회전한 자재(Cal 지그) 중심 궤적(편심원)을 최소자승 피팅 → 원 중심 = 피커 실제 회전중심. 저장 + Bottom 정렬 보정 반영.
**Verified:** 2026-06-24
**Status:** human_needed
**Re-verification:** No — initial verification

---

## Goal Achievement

### Observable Truths

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | SC-1: 36스텝 Cal 지그 중심 궤적 → 편심원 중심(피커센터) 최소자승 계산. per-step center = FitCircleContourXld; eccentric fit = FitCircleContourXld "atukey" | ✓ VERIFIED | `PickerCenterCalibrationService.cs` — TryAddStep: GenCircle→ReduceDomain→EdgesSubPix→FitCircleContourXld #1 ("atukey"). TryComputePickerCenter: GenContourPolygonXld(accumulated)→FitCircleContourXld #2 ("atukey")→store. MIN_STEPS=6 guard + radius guard [1.0, 100000.0 px]. Persists to SystemSetting.Handle.PickerCenterRow/Col + Save(). |
| 2 | SC-3: Bottom 정렬 보정에 피커센터 반영. | ✓ VERIFIED | `AlignShapeMatchService.cs` L408-415: Bottom 브랜치에서 ApplyPickerCenterCorrection 호출. 미캘(0,0) 시 Phase 59 폴백 보장. Tray 브랜치 L417-422: dCol*resMm/dRow*resMm 그대로 (Phase 59 동작 유지). |

**Score:** 2/2 truths verified (code level)

*Note: SC-2 (각도 캘 AV-06) = DROPPED per CONTEXT D-01 user decision 2026-06-24 (Phase 59 2-pattern angle_lx 로 충족, 재도입 안 함). Not a gap.*

---

### Required Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `WPF_Example/Custom/SystemSetting.cs` | PickerCenterRow / PickerCenterCol double properties + AfterLoad guard | ✓ VERIFIED | L100: `public double PickerCenterRow { get; set; } = 0.0;` L103: `public double PickerCenterCol { get; set; } = 0.0;` — [Category("ETHERNET_VISION")]. L69: RestorePickerCenterDefault() no-op guard. L40: AfterLoad() 호출 확인. //260624 hbk Phase 60 마커 존재. |
| `WPF_Example/Custom/EthernetVision/PickerCenterCalibrationService.cs` | Reset/TryAddStep(jig circle fit)/TryComputePickerCenter(eccentric fit); FitCircleContourXld 2회 | ✓ VERIFIED | 신규 파일 존재. Reset() L38, TryAddStep() L50+L58, TryComputePickerCenter() L123. FitCircleContourXld "atukey" 2회 (L87, L149). EdgesSubPix L83. MIN_STEPS/radius guard. AlignShapeMatchService/Matcher/TryFindCenter/VisionAlgorithmService 참조 없음. HTuple/HObject finally dispose 전항목. |
| `WPF_Example/Custom/EthernetVision/AlignShapeMatchService.cs` | ApplyPickerCenterCorrection helper + Bottom 브랜치 배선 | ✓ VERIFIED | L444: private void ApplyPickerCenterCorrection. L451: PickerCenterRow 읽기. L469: HomMat2dRotate(homMat, thetaRad, pickerRow, pickerCol). L411: Run() Bottom 브랜치에서 호출. TryFindCenter 없음. Tray 브랜치 미수정(L418). |
| `WPF_Example/Custom/EthernetVision/EthernetVisionHandler.cs` | PickerCal property + lazy creation | ✓ VERIFIED | L24: `public PickerCenterCalibrationService PickerCal { get; private set; }`. L39: Initialize() 내 `PickerCal = new PickerCenterCalibrationService();`. L67-69: catch 경로 null 가드. //260624 hbk Phase 60 마커 존재. |
| `WPF_Example/DatumMeasurement.csproj` | Compile Include for PickerCenterCalibrationService.cs | ✓ VERIFIED | L244: `<Compile Include="Custom\EthernetVision\PickerCenterCalibrationService.cs" />` |

---

### Key Link Verification

| From | To | Via | Status | Details |
|------|----|-----|--------|---------|
| PickerCenterCalibrationService.TryAddStep | HOperatorSet.FitCircleContourXld (per-step Cal jig circle) | GenCircle→ReduceDomain→EdgesSubPix→FitCircleContourXld "atukey" | ✓ WIRED | L79-88: 전체 체인 확인. |
| PickerCenterCalibrationService.TryComputePickerCenter | SystemSetting.PickerCenterRow/Col + Save | GenContourPolygonXld(accumulated)→FitCircleContourXld→write→Save() | ✓ WIRED | L144-175: write L173-175, Save() L175. |
| AlignShapeMatchService.Run (Bottom branch) | SystemSetting.PickerCenterRow/Col | non-zero picker center → HomMat2dRotate about picker center | ✓ WIRED | L451: 읽기. L469: HomMat2dRotate(homMat, thetaRad, pickerRow, pickerCol). |
| AlignShapeMatchService.ApplyPickerCenterCorrection | HOperatorSet.HomMat2dRotate | rigid rotation of midpoint offset about calibrated picker center | ✓ WIRED | L468-469: HomMat2dIdentity→HomMat2dRotate. L473: AffineTransPoint2d. |
| SystemSetting.AfterLoad | RestorePickerCenterDefault | partial AfterLoad method body call | ✓ WIRED | Custom/SystemSetting.cs L40. |

---

### Data-Flow Trace (Level 4)

| Artifact | Data Variable | Source | Produces Real Data | Status |
|----------|---------------|--------|--------------------|--------|
| PickerCenterCalibrationService | _rows/_cols (accumulated) | TryAddStep → FitCircleContourXld #1 from grabbed image | Yes (circle fit from image edges) | ✓ FLOWING (code path complete; real Cal jig image 필요 — human) |
| AlignShapeMatchService.Run() | corrRow/corrCol | ApplyPickerCenterCorrection → SystemSetting.PickerCenterRow/Col | Yes when calibrated; Phase 59 fallback when (0,0) | ✓ FLOWING (code path; real picker 환경 UAT 필요) |
| SystemSetting | PickerCenterRow/Col | TryComputePickerCenter → SystemSetting.Handle.Save() | Yes (INI persist) | ✓ FLOWING |

---

### Behavioral Spot-Checks

Step 7b: SKIPPED — 실 피커 + 36-스텝 회전 시퀀스 없음 (SIMUL_MODE 에서 Cal 지그 grab 불가). Build pass는 Plan 60-03 Task 3에서 확인 (msbuild Debug/x64, exit 0, error 0).

---

### Requirements Coverage

| Requirement | Source Plan | Description | Status | Evidence |
|-------------|------------|-------------|--------|----------|
| AV-05 | 60-01, 60-02, 60-03 | 피커 편심원 중심 최소자승 계산 | ✓ SATISFIED | PickerCenterCalibrationService 구현 완료 (FitCircleContourXld ×2), Bottom 보정 반영 (ApplyPickerCenterCorrection). |
| AV-06 | (dropped) | 각도 선형 오프셋 보정 | DROPPED — not a gap | CONTEXT D-01: Phase 59 2-pattern angle_lx 로 충족. 사용자 결정 2026-06-24. |

---

### Anti-Patterns Found

| File | Line | Pattern | Severity | Impact |
|------|------|---------|----------|--------|
| AlignShapeMatchService.cs | 442 | `// TODO(Phase 61 UAT): PICKER_ROTATION_SIGN 및 회전 적용점 실 피커 기준 확정.` | ℹ️ Info | 의도적 스텁 — PICKER_ROTATION_SIGN=+1.0 기본값. Phase 61 UAT 에서 실 피커 컨트롤러 규약 기준으로 부호/방향 확정 예정. Phase 60 범위 내 정상. |

No blockers. No missing/stub/unwired artifacts. The PICKER_ROTATION_SIGN TODO is an intentional Phase 61 carry-over per CONTEXT D-06 and D-05 ("부호/회전중심 규약은 피커 컨트롤러 기준 UAT 확정 전 기본 +1").

---

### Build Pass Confirmation

Plan 60-03 Task 3 (SUMMARY) 기록: MSBuild Debug/x64, exit code 0, errors 0, 1 pre-existing warning (MSB3884). PHASE60_START = 48f2c49.

Anti-goal verified: git diff --name-only 48f2c49 HEAD 에서 VisionAlgorithmService.cs / PatternMatchService.cs / RecipeFileHelper.cs / Grabber 파일 전부 UNCHANGED. 이 검증자가 직접 확인 — anti-goal diff 명령 결과 출력 없음 (clean).

---

### Human Verification Required

#### 1. 실 피커 + Cal 지그 36-스텝 회전 캘리브레이션 실행

**Test:** Phase 61 UI 또는 직접 EthernetVisionHandler.Handle.PickerCal.TryAddStep 을 36회 호출 (피커가 Cal 지그를 픽업한 채 10°씩 회전). 이후 TryComputePickerCenter 호출.
**Expected:** PickerCenterRow/Col 비-0 값 저장 → Setting.ini [ETHERNET_VISION] 에 기록. 앱 재기동 후 값 유지. 산출된 편심원 반경이 피커 편심량(물리 단위 px)으로 타당한 범위.
**Why human:** SIMUL_MODE 에는 실 피커 회전 시퀀스 없음. 36 Cal 지그 이미지가 필요하며 편심원 피팅 결과(피커센터 row/col)가 물리적으로 타당한지 육안 확인 필요.

#### 2. Bottom 정렬 보정 피커센터 반영 확인 (캘 후)

**Test:** PickerCenterRow/Col 비-0 저장 후 Bottom 정렬 실행. 로그 [ALIGN_SVC] run OK (Bottom) 의 off=(X,Y)mm 값이 Phase 59 결과와 다른지 확인.
**Expected:** ApplyPickerCenterCorrection 이 non-zero 피커센터로 실행되어 corrRow/corrCol 이 dRow/dCol 과 다름 → 피커 착지 위치 정확도 향상.
**Why human:** PICKER_ROTATION_SIGN=+1.0 (기본값) 및 회전중심 규약이 실 피커 컨트롤러 기준과 일치하는지 Phase 61 UAT 에서 부호·방향 확정 필요.

#### 3. 미캘 폴백 확인 (PickerCenterRow/Col = 0)

**Test:** INI 를 PickerCenterRow=0 / PickerCenterCol=0 으로 리셋(또는 Phase 60 이전 INI 사용) 후 Bottom 정렬 실행.
**Expected:** OffsetXmm = dCol * resMm, OffsetYmm = dRow * resMm (Phase 59 동작과 byte-identical). 로그에 "[ALIGN_SVC] run OK (Bottom)" 출력되며 값이 Phase 59 기준치와 동일.
**Why human:** 폴백 경로 코드는 검증되었으나(bUncalibrated 분기 확인), 실 피커 환경에서 회귀 없음 최종 확인 필요.

#### 4. Tray 정렬 회귀 없음 확인

**Test:** Phase 60 후 Tray 모드 정렬 실행.
**Expected:** OffsetXmm/Ymm 결과가 Phase 59 기준치와 동일, HasTheta=false.
**Why human:** Tray 브랜치 코드 미수정 확인되었으나(L417-422 = dCol*resMm/dRow*resMm, HasTheta=false), 실 Tray 정렬 시나리오로 회귀 없음 최종 확인 필요.

---

### Gaps Summary

갭 없음. 모든 must-have 가 코드 수준에서 검증되었다.

- SC-1 (36-스텝 편심원 피팅 → 피커센터): PickerCenterCalibrationService.cs 완전 구현, FitCircleContourXld 2회, MIN_STEPS/radius guard, SystemSetting 저장.
- SC-3 (Bottom 보정 피커센터 반영): AlignShapeMatchService.ApplyPickerCenterCorrection 배선 완료, 미캘 폴백 + Tray 미수정 확인.
- AV-06 = DROPPED (사용자 결정, not a gap).

Human verification 4건은 실 피커 하드웨어가 필요한 런타임 확인으로, Phase 58/59 와 동일한 "검증 직전 정지, Phase 61 UI 후 일괄 UAT" 방침 (CONTEXT D-06) 에 따른 정상적인 지연이다.

---

*Verified: 2026-06-24*
*Verifier: Claude (gsd-verifier)*
