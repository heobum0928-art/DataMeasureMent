---
phase: 66-ring7-coax-align-2026-06-26
verified: 2026-06-29T12:00:00Z
status: human_needed
score: 12/12
overrides_applied: 0
gaps: []
human_verification:
  - test: "검사 Shot>Light PropertyGrid 에서 Ring7 / Coax 노출 확인"
    expected: "Ring/Back/Side/Ring7 4종만 보이고, Coax(Enabled + Brightness) 는 완전히 숨겨져 있다"
    why_human: "PropertyGrid UI 렌더는 코드로 검증 불가 — [Browsable(false)] 가 PropertyTools 에 실제 적용되는지 런타임 확인 필요"
  - test: "$PREP:site,z_index,1@ (Op=1) 수신 후 Ring7 조명 켜짐 / $PREP:site,z_index,0@ (Op=0) 수신 후 Ring7 포함 전 조명 소등 확인"
    expected: "Op=1 시 Ring7Light_Enabled=true 인 Shot 에서 LIGHT_RING7 그룹이 ON, Op=0 시 RING7 포함 전 5종 소등"
    why_human: "LightHandler → 실제 조명 HW 제어 결과는 실장비 없이 검증 불가 (SIMUL_MODE 에서는 조명 제어 미작동)"
  - test: "Bottom Align 창 슬롯 선택 → 동축 UI 복원, 체크/슬라이더 조정 → LIGHT_ALIGN_COAX ON/밝기 반영 확인"
    expected: "슬롯 전환 시 JSON 저장값이 chk_coaxEnabled / sld_coaxLevel 에 복원되고, 변경 시 동축 조명이 즉시 반응"
    why_human: "동축 조명 실제 점등 여부는 실HW(COM 포트 연결된 조명 컨트롤러) 없이 확인 불가"
  - test: "Tray Align 창 진입 시 Tray.json 동축값 복원 확인"
    expected: "창 로드 시 LoadTrayCoaxToUi() 가 Tray.json 에서 CoaxEnabled/CoaxLevel 을 읽어 UI 에 표시"
    why_human: "실파일 JSON 쓰기·읽기 사이클을 UI 에서 육안 확인해야 함"
  - test: "Bottom/Tray 창에서 Teach 또는 Run 실행 시 동축 조명이 grab 직전 자동 켜짐 확인"
    expected: "Teach/Run 버튼 클릭 시 ApplyCoaxLight() → 조명 ON → Camera.Grab() 또는 TryTeach() 순서 실행"
    why_human: "타이밍 순서와 실조명 점등은 코드 경로만으로 충분히 보장되지만 실HW 연동 확인이 필요"
---

# Phase 66: Ring7 조명 정합 + Align 동축 UI 검증 보고서

**Phase 목표:** 검사 shot 조명 세트를 HW 사양(Ring/Backlight/Bar/Ring7, shot별 자유 조합)과 정합시키고, Align 카메라 전용 동축(Coax)은 검사에서 숨겨 Align 창(Bottom/Tray)으로 이동한다.
**검증 일시:** 2026-06-29
**상태:** human_needed
**재검증:** 해당 없음 (최초 검증)

---

## Goal Achievement

### Observable Truths

| #  | Truth | Status | Evidence |
|----|-------|--------|----------|
| 1  | 검사 Shot>Light PropertyGrid 에 Ring7 조명(ON/OFF + 밝기)이 노출되어 Ring/Back/Bar/Ring7 4종 자유 조합 가능 | ✓ VERIFIED | `ShotConfig.cs:79-81` — `[Category("Light|Ring7")]` + `public bool Ring7Light_Enabled` + `public int Ring7Light_Brightness` 존재. `//260626 hbk` 주석. |
| 2  | 검사 Shot>Light PropertyGrid 에 Coax 조명이 더 이상 노출되지 않는다 (숨김) | ✓ VERIFIED (코드) / ? HUMAN | `ShotConfig.cs:69,72` — CoaxLight_Enabled 위 `[Browsable(false)]` + CoaxLight_Brightness 위 `[Browsable(false)]` (IN-01 수정 반영). 런타임 UI 실물 확인은 human 필요. |
| 3  | Coax INI 키(CoaxLight_Enabled/CoaxLight_Brightness)는 보존되어 기존 레시피 로드/저장이 깨지지 않는다 (하위호환) | ✓ VERIFIED | `ShotConfig.cs:70-73` — 필드 선언 보존, `[Browsable(false)]` 만 추가. ParamBase 리플렉션은 Browsable 무관 직렬화. |
| 4  | $PREP(ApplyShotLights) 수신 시 Ring7Light_Enabled 인 Shot 은 LIGHT_RING7 그룹이 켜지고 밝기가 적용된다 | ✓ VERIFIED (코드) / ? HUMAN | `InspectionSequence.cs:392-400` — `if (shot.Ring7Light_Enabled)` → `SetOnOff(LIGHT_RING7, true)` + `SetLevel(LIGHT_RING7, shot.Ring7Light_Brightness)` else `SetOnOff(false)`. 실조명 동작은 human 필요. |
| 5  | $PREP Op==0(사이클 종료, TurnOffShotLights) 수신 시 RING7 을 포함한 검사 조명이 모두 소등된다 (점등/소등 대칭) | ✓ VERIFIED (코드) / ? HUMAN | `InspectionSequence.cs:337-344` — TurnOffShotLights 본문에 RING/BACK/ALIGN_COAX/BAR/RING7 5종 소등. RING7 소등이 LIGHT_BAR 뒤에 추가됨. 실동작은 human 필요. |
| 6  | 기존 Ring/Back/Side(BAR)/Coax 매핑 동작은 변경되지 않는다 (회귀 0) | ✓ VERIFIED | `ShotConfig.cs:61-77` 기존 4종 필드 무변경. `InspectionSequence.cs` 기존 4종 if-else 블록 소스 확인. SUMMARY 01: `git diff --stat` 로 추가 라인만 확인됨. |
| 7  | Align 슬롯/Tray 레퍼런스 JSON 에 동축값(CoaxEnabled/CoaxLevel)이 함께 저장된다 | ✓ VERIFIED | `AlignRefPose.cs:43-47` — `public bool CoaxEnabled` + `public int CoaxLevel` POCO 필드 추가. `//260626 hbk` 주석. |
| 8  | 기존 JSON(동축 키 부재=구 레시피) 로드 시 CoaxEnabled=false/CoaxLevel=0 으로 안전 로드된다 (하위호환) | ✓ VERIFIED | POCO C# 기본값(bool→false, int→0) + Newtonsoft.Json 키 부재 자동 폴백 패턴 — Phase 61.1 선례와 동일. |
| 9  | 동축값 저장 시 기존 티칭 데이터(Ref1Row/Col 등)는 덮어쓰이지 않고 보존된다 (load-merge-save) | ✓ VERIFIED | `AlignShapeMatchService.cs:254-270` — `LoadRefPose` → `refPose.CoaxEnabled = coaxEnabled` + `refPose.CoaxLevel = nClamped` → `File.WriteAllText`. 기존 필드 덮어쓰기 없음. WR-01 클램프(0~255)도 적용됨. |
| 10 | $ALIGN_TEST Run(RunBottomAlign) 시 해당 슬롯 저장 동축값이 grab 직전 자동 적용된다 | ✓ VERIFIED | `SystemHandler.cs:301-303` — `ApplyCoaxLightForSlot(slot)` 호출 다음 줄에 `img = ...Camera.Grab()`. 헬퍼 `L357-386` — GetSlotRefPose → SetOnOff/SetLevel/예외 시 off. |
| 11 | Bottom/Tray Align 창 좌측 패널에 동축 컨트롤(ON/OFF 체크박스 + 밝기 슬라이더)이 있다 | ✓ VERIFIED (코드) / ? HUMAN | `BottomVisionView.xaml:228-236` — `chk_coaxEnabled` + `sld_coaxLevel(0~255)` + `lbl_coaxLevel`. `TrayVisionView.xaml:170-179` 동일. RowDefinition Bottom=8개/Tray=7개, airspace-safe 좌측 패널 배치. 실 UI는 human 확인 필요. |
| 12 | Bottom/Tray 에서 슬롯 전환 / 창 진입 시 동축값 복원, 변경 시 즉시 적용+저장, Teach/Run/Grab 직전 자동 적용된다 | ✓ VERIFIED (코드) / ? HUMAN | `BottomVisionView.xaml.cs:185,220,352,392,760-856` — LoadSlotCoaxToUi/ApplyCoaxLight/SaveSlotCoaxToJson + _isLoadingCoax WR-02 가드. `TrayVisionView.xaml.cs:74,104,228,262,318-405` 동일 패턴. |

**점수:** 12/12 truths (코드 레벨 전체 검증, 조명 HW 런타임 동작은 human 필요)

---

## Required Artifacts

| Artifact | 제공 | 상태 | 세부 |
|----------|------|------|------|
| `WPF_Example/Custom/Sequence/Inspection/ShotConfig.cs` | Ring7Light_Enabled/Brightness + CoaxLight_* [Browsable(false)] | ✓ VERIFIED | L79-81 Ring7 추가, L69/72 Browsable(false) 2개, CoaxLight 필드 보존 |
| `WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs` | ApplyShotLightsInternal Ring7 점등 + TurnOffShotLights RING7 소등 | ✓ VERIFIED | L392-400 점등 if-else, L343 소등 1줄, 기존 4종 무변경 |
| `WPF_Example/Custom/EthernetVision/AlignRefPose.cs` | CoaxEnabled/CoaxLevel POCO 필드 | ✓ VERIFIED | L43-47, //260626 hbk 주석 |
| `WPF_Example/Custom/EthernetVision/AlignShapeMatchService.cs` | GetSlotRefPose + TrySaveCoax(0~255 클램프 포함) | ✓ VERIFIED | L221 GetSlotRefPose, L241 TrySaveCoax, L261-270 WR-01 클램프 if-else |
| `WPF_Example/Custom/SystemHandler.cs` | ApplyCoaxLightForSlot 헬퍼 + grab 직전 호출 | ✓ VERIFIED | L301 호출, L357-386 헬퍼, using ReringProject.Device L2 |
| `WPF_Example/Custom/UI/BottomVisionView.xaml` | 동축 GroupBox (chk_coaxEnabled + sld_coaxLevel + lbl_coaxLevel), Row6 | ✓ VERIFIED | L228-236 컨트롤, RowDefinition 8개 |
| `WPF_Example/Custom/UI/BottomVisionView.xaml.cs` | ApplyCoaxLight + 5개 메서드 + _isLoadingCoax 가드 + using Device | ✓ VERIFIED | L9 using, L62 _isLoadingCoax, L760- 메서드 5개 |
| `WPF_Example/Custom/UI/TrayVisionView.xaml` | 동축 GroupBox, Row5 | ✓ VERIFIED | L170-179 컨트롤, RowDefinition 7개 |
| `WPF_Example/Custom/UI/TrayVisionView.xaml.cs` | ApplyCoaxLight + 5개 메서드 + _isLoadingCoax 가드 | ✓ VERIFIED | L47 _isLoadingCoax, L318- 메서드 5개 |

---

## Key Link Verification

| From | To | Via | 상태 | 세부 |
|------|----|-----|------|------|
| ShotConfig.Ring7Light_Enabled/Brightness | InspectionSequence.ApplyShotLightsInternal | shot.Ring7Light_Enabled 분기 → SetOnOff/SetLevel(LIGHT_RING7) | ✓ WIRED | InspectionSequence.cs:392 `if (shot.Ring7Light_Enabled)` 확인 |
| $PREP Op==0 (TurnOffShotLights) | LightHandler.LIGHT_RING7 소등 | SetOnOff(LIGHT_RING7, false) | ✓ WIRED | InspectionSequence.cs:343 LIGHT_RING7 소등 확인 |
| ShotConfig.CoaxLight_Enabled | PropertyGrid 숨김 | [Browsable(false)] 어트리뷰트 | ✓ WIRED | ShotConfig.cs:69,72 — Enabled+Brightness 2필드 모두 적용 |
| SystemHandler.RunBottomAlign | ApplyCoaxLightForSlot(slot) → grab | grab 직전 동축 자동 적용 | ✓ WIRED | SystemHandler.cs:301 호출, L303 Grab() 다음 줄 |
| SystemHandler.ApplyCoaxLightForSlot | Matcher.GetSlotRefPose(Bottom, slot) → LIGHT_ALIGN_COAX | 슬롯 JSON 동축값 읽어 SetOnOff/SetLevel | ✓ WIRED | SystemHandler.cs:361,372,377 확인 |
| AlignShapeMatchService.TrySaveCoax | Bottom_{slot}.json / Tray.json | LoadRefPose → Coax 필드 갱신(0~255 클램프) → File.WriteAllText | ✓ WIRED | AlignShapeMatchService.cs:254-278 확인 |
| BottomVisionView.SlotComboBox_SelectionChanged | Matcher.GetSlotRefPose(Bottom, slot) → chk/sld 복원 | _isLoadingCoax 가드 포함 | ✓ WIRED | BottomVisionView.xaml.cs:185,829-856 확인 |
| CoaxCheckBox_Changed / CoaxSlider_ValueChanged | ApplyCoaxLight() + Matcher.TrySaveCoax(...) | 수동 override 즉시 적용 + 저장 | ✓ WIRED | BottomVisionView.xaml.cs:786-800, TrayVisionView.xaml.cs:344-358 확인 |
| Grab/Teach/Run 핸들러 | ApplyCoaxLight() (호출 직전) | 런타임=티칭 조명 일치 | ✓ WIRED | BottomVisionView.xaml.cs:220,352,392 / TrayVisionView.xaml.cs:104,228,262 확인 |

---

## Data-Flow Trace (Level 4)

| Artifact | 데이터 변수 | 소스 | 실데이터 생성 | 상태 |
|----------|------------|------|--------------|------|
| BottomVisionView.ApplyCoaxLight | chk_coaxEnabled.IsChecked / sld_coaxLevel.Value | WPF 컨트롤 → LightHandler.SetOnOff/SetLevel | UI 입력 → LightHandler COM 제어 — Slider(0~255)로 범위 제한 | ✓ FLOWING |
| AlignShapeMatchService.TrySaveCoax | coaxLevel 클램프 → refPose.CoaxLevel | LoadRefPose JSON → Coax 필드 갱신 → File.WriteAllText | 실제 JSON 파일 읽기/쓰기, 기존 티칭 데이터 병합 | ✓ FLOWING |
| SystemHandler.ApplyCoaxLightForSlot | GetSlotRefPose → bEnabled/nLevel | Bottom_{slot}.json → AlignRefPose.CoaxEnabled/CoaxLevel | JSON null 시 false/0 안전 폴백 | ✓ FLOWING |

---

## Behavioral Spot-Checks

Step 7b: SKIPPED (실 HW 및 COM 포트 연결 없이 조명 제어 동작 검증 불가. 빌드 PASS가 유일한 자동 검증 기준임)

빌드 증거 (SUMMARY 01/02/03/REVIEW-FIX 기록 기준):

| 단계 | 결과 | 증거 |
|------|------|------|
| Plan 01 msbuild Debug/x64 | PASS (error 0) | 66-01-SUMMARY.md 빌드 섹션 |
| Plan 02 msbuild Debug/x64 | PASS (error 0) | 66-02-SUMMARY.md 빌드 섹션 |
| Plan 03 msbuild Debug/x64 | PASS (error 0) | 66-03-SUMMARY.md 빌드 섹션 |
| REVIEW-FIX msbuild Debug/x64 | PASS (error 0) | 66-REVIEW-FIX.md 빌드 섹션 |

---

## Requirements Coverage

| REQ-ID | 출처 Plan | 설명 | 상태 | 증거 |
|--------|----------|------|------|------|
| LIGHT-01 | Plan 01 | 검사 조명 정합(Ring7 추가 + Coax 숨김) | ✓ SATISFIED | ShotConfig Ring7 2필드 + [Browsable(false)] + InspectionSequence LIGHT_RING7 점등/소등 대칭 |
| AV-08 | Plan 02, 03 | Align 동축 조명 백엔드 + UI (Bottom/Tray) | ✓ SATISFIED | AlignRefPose Coax 필드 + GetSlotRefPose/TrySaveCoax API + SystemHandler grab 직전 자동 적용 + Bottom/Tray UI 컨트롤 |

REQUIREMENTS.md 확인: AV-08은 Phase 61 엔트리로 "Tray/BottomVisionView 에 툴바+티칭 패널+검사 결과 패널을 제공하고 HalconViewer 공용"으로 정의됨. Phase 66 은 AV-08 중 동축 조명 부분(동축 ON/OFF + 밝기 컨트롤, JSON 저장, 자동 적용)을 구현하여 AV-08 동축 항목을 충족함.

---

## Anti-Patterns Found

| 파일 | 패턴 | 심각도 | 영향 |
|------|------|--------|------|
| (없음) | REVIEW-FIX에서 IN-01(CoaxLight_Brightness [Browsable(false)] 누락), WR-01(CoaxLevel 무클램프), WR-02(슬라이더 이벤트 연쇄) 3건 모두 수정 완료 (커밋 85767b1/13217ab/28762e6) | - | - |

삼항 연산자 미사용 확인, C# 8.0+ 문법 없음, throw 금지 패턴(TCP/UI 경로 모두) 준수됨.

---

## Human Verification Required

### 1. PropertyGrid Coax 숨김 확인

**Test:** 앱 실행 → 검사 탭 → Shot 선택 → Light 탭 PropertyGrid 확인
**Expected:** Ring/Back/Side/Ring7 4종만 보이며, CoaxLight_Enabled / CoaxLight_Brightness 는 나타나지 않는다
**Why human:** [Browsable(false)] 가 PropertyTools PropertyGrid 에서 실제로 적용되는지 런타임 육안 확인이 필요함

### 2. Ring7 조명 $PREP 점등/소등 대칭 확인

**Test:** Ring7Light_Enabled=true 인 레시피 Shot 으로 $PREP:site,z_index,1@ 송신 후 RING7 조명 ON 확인, 이후 $PREP:site,z_index,0@ 송신 후 RING7 포함 전 조명 OFF 확인
**Expected:** Op=1 시 RING7 켜짐, Op=0 시 RING7 포함 5종 모두 꺼짐 (잔존 없음)
**Why human:** LightHandler → COM 포트 → 실 조명 컨트롤러 동작은 SIMUL_MODE 에서 검증 불가

### 3. Bottom Align 창 동축 복원/즉시적용/저장 확인

**Test:** Bottom 창 진입 → 슬롯 선택(슬롯에 티칭 JSON 있음) → chk/sld 에 저장값 복원 확인 → 체크박스 토글 → 동축 조명 ON/OFF 반응 확인 → 슬라이더 이동 → 밝기 변화 확인
**Expected:** 슬롯 전환 시 UI 복원, 변경 시 즉시 LIGHT_ALIGN_COAX 반응, Bottom_{slot}.json 에 저장
**Why human:** 실 COM 포트 + 조명 컨트롤러 없이는 LIGHT_ALIGN_COAX 실동작 확인 불가

### 4. Tray Align 창 동축 복원 확인

**Test:** Tray 창 진입 → lbl_coaxLevel 에 Tray.json 저장값 표시 확인
**Expected:** LoadTrayCoaxToUi() 가 Tray.json 에서 읽어 chk_coaxEnabled / sld_coaxLevel 에 표시
**Why human:** Tray.json 실파일 쓰기→재진입→복원 사이클을 육안 확인해야 함

### 5. Teach/Run/Grab 직전 동축 자동 점등 확인

**Test:** Bottom/Tray 창에서 동축 ON(bEnabled=true) 상태로 Teach 또는 Run 실행
**Expected:** 버튼 클릭 시 grab 직전 LIGHT_ALIGN_COAX 점등 확인 (조명 컨트롤러 반응)
**Why human:** 타이밍 순서는 코드로 보장되나 실조명 점등은 HW 연동 확인이 필요함

---

## Gaps Summary

없음 — 코드 레벨 must-have 12/12 전체 검증됨. 빌드 4회 PASS (Plan 01/02/03 + REVIEW-FIX). 코드 리뷰 3건(IN-01/WR-01/WR-02) 모두 수정 완료.

잔여 항목은 모두 실HW 동작 확인이 필요한 human_verification 범주임 — 코드 결함이 아니라 실장비 UAT 항목.

---

_Verified: 2026-06-29_
_Verifier: Claude (gsd-verifier)_
