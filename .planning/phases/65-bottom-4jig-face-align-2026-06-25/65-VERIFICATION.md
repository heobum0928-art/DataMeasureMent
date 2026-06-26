---
phase: 65-bottom-4jig-face-align-2026-06-25
verified: 2026-06-26T15:00:00Z
status: human_needed
score: 4/5 must-haves verified
overrides_applied: 0
human_verification:
  - test: "실HW 6슬롯 면별 티칭 → 18파일 생성 확인 + PLC $ALIGN_TEST Run 응답 + 회귀 테스트"
    expected: "6슬롯(3D_Top/3D_Bottom/2D_TOP/2D_BOTTOM/2D_SIDE_1/2D_SIDE_2) 각각 티칭 파일(Bottom_{slot}_1.shm/_2.shm/.json) 18개 생성, Bottom_2D_SIDE_1.json 파일명 정상, 슬롯 컨텍스트 전환 시 HasTemplate 라벨 분리, PLC $ALIGN_RESULT AlignFace echo + pose 수치 정합, Tray·단일 Bottom·MainView 검사 회귀 0"
    why_human: "실 이더넷 카메라·실 PLC·6종 안착지그가 필요. 파일 생성 결과·pose 수치·PLC 실제 자재 안착은 코드 검사로 확인 불가."
---

# Phase 65: Bottom 6-슬롯 면별 Align 검증 보고서

**Phase Goal:** Bottom 비전이 안착지그 6종에 자재를 ideal 안착시키기 위한 면별 align. 6슬롯 = 3D 그룹 2개(3D_Top, 3D_Bottom) + 2D 그룹 4개(2D_TOP, 2D_BOTTOM, 2D_SIDE_1, 2D_SIDE_2). 각 지그마다 독립 모델+레퍼런스 티칭(6세트).
**Verified:** 2026-06-26
**Status:** human_needed
**Re-verification:** No — 최초 검증

---

## Goal Achievement

### Observable Truths

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | AlignShapeMatchService가 6슬롯 독립 경로(Bottom_{slot}_1/2.shm + .json)를 생성·사용한다 | VERIFIED | `AlignShapeMatchService.cs:68` `BuildShmPath`에 slot 파라미터 추가. mode==Bottom && slot!=None 시 `"Bottom_" + token` 조합. `BuildJsonPath:122` EndsWith 방식으로 마지막 `_1`만 제거(2D_SIDE_1 오치환 없음). |
| 2 | 슬롯 미지정(None) 시 기존 Bottom 단일 경로로 폴백한다 (회귀 0) | VERIFIED | `BuildShmPath:91` `slot==None → modeFileName = "Bottom"` 분기 명시적 코드 확인. `TryTeach` 기존 오버로드(slot 없음)가 `EBottomAlignSlot.None`으로 신규 오버로드 위임. |
| 3 | AlignFace 0~5 정수를 슬롯으로 매핑하고, 범위 외 값은 None으로 안전 거부한다 | VERIFIED | `EBottomAlignSlot.cs:63` `FromAlignFace` if-else 체인: 0→Slot3DTop … 5→Slot2DSide2, 그 외 default→None. `SystemHandler.cs:252` `bSlotValid=(slot!=None)` 가드 → `FillAlignPoseZero + IsPass=false` 반환. |
| 4 | BottomVisionView에서 6슬롯 선택 후 해당 슬롯으로 TryTeach/Run/HasTemplate가 호출된다 | VERIFIED | `BottomVisionView.xaml:98` `cmb_slot` ComboBox 존재. `.xaml.cs:50` `_selectedSlot = EBottomAlignSlot.None` 필드. `:348,352` TryTeach에 `_selectedSlot` 전달. `:386` Run에 `_selectedSlot` 전달. `:653` HasTemplate에 `_selectedSlot` 전달. |
| 5 | $ALIGN_TEST(BOTTOM, AlignFace 0~5) 수신 시 실측 grab+Matcher.Run 후 pose(OffsetX/OffsetY/Theta)를 AlignResultPacket.Items에 채운다 | VERIFIED | `SystemHandler.cs:251` `FromAlignFace(packet.AlignFace)`→슬롯. `:310` `Matcher.Run(img, Bottom, slot)`. `:354-370` `FillAlignPose`: Items에 OffsetX/OffsetY/Theta 순서 추가. `:325-350` `RunBottomAlign` try/finally에서 HImage.Dispose + DetectedContourXld.Dispose 확인. |

**Score:** 5/5 코드 수준 검증 완료

---

### Deferred Items

없음 — 모든 코드 수준 must-have가 이 Phase 내에서 구현됨. 실HW UAT만 인간 게이트 대기.

---

### Required Artifacts

| Artifact | Expected | Status | Details |
|----------|---------|--------|---------|
| `WPF_Example/Custom/EthernetVision/EBottomAlignSlot.cs` | 6값 슬롯 enum + AlignFace 매퍼 | VERIFIED | 파일 존재. enum 7값(None=-1, Slot3DTop=0~Slot2DSide2=5). `ToFileToken/FromAlignFace/ToDisplayLabel` 3개 매퍼 메서드. if-else 구현(삼항 없음). |
| `WPF_Example/Custom/EthernetVision/AlignShapeMatchService.cs` | slot 파라미터를 받는 경로 헬퍼 + 공개 API | VERIFIED | `BuildShmPath/GetShmPath/BuildJsonPath` 3개 헬퍼에 slot 파라미터. `HasTemplate/TryLoadTemplate/TryTeach(오버로드2개)/Run` 공개 API에 slot 파라미터. 비-slot 메서드 무변경 확인(TrySaveRefPose/LoadRefPose/ComputeAngleLx 등). |
| `WPF_Example/Custom/UI/BottomVisionView.xaml` | 6슬롯 선택 ComboBox(cmb_slot) 포함 | VERIFIED | `cmb_slot` ComboBox + `lbl_slotStatus` TextBlock + `SlotComboBox_SelectionChanged` 이벤트 연결. 기존 위젯 x:Name 무변경 확인. |
| `WPF_Example/Custom/UI/BottomVisionView.xaml.cs` | 슬롯 선택/전환 + slot 인자 전달 + 슬롯별 ROI 보관 | VERIFIED | `_selectedSlot`, `_slotRois(Dictionary)` 필드. `PopulateSlotComboBox`(6개 항목). `SlotComboBox_SelectionChanged`(WR-03 배열 길이 가드 포함). TryTeach/Run/HasTemplate 4개 호출 모두 `_selectedSlot` 전달. |
| `WPF_Example/Custom/SystemHandler.cs` | ProcessAlignTest 실측 배선(stub 제거) | VERIFIED | stub(IsPass=true echo만) → BOTTOM+AlignFace 0~5 시 `RunBottomAlign` 헬퍼 위임 + `FillAlignPose` + `FillAlignPoseZero` 3개 헬퍼. TRAY 경로 무변경(회귀 0). using HalconDotNet 추가(91abe89). |
| `WPF_Example/DatumMeasurement.csproj` | EBottomAlignSlot.cs Compile Include 등록 | VERIFIED | `Custom\EthernetVision\EBottomAlignSlot.cs` Compile Include 라인 존재(L241). |

---

### Key Link Verification

| From | To | Via | Status | Details |
|------|----|-----|--------|---------|
| `AlignShapeMatchService.BuildShmPath` | `Bottom_{slot}_N.shm` 파일명 | slot 토큰 삽입 | WIRED | `EBottomAlignSlotMap.ToFileToken(slot)` 반환값을 `"Bottom_" + token`으로 조합. |
| `EBottomAlignSlotMap.FromAlignFace` | 0~5 → 슬롯 매핑 | if-else 6분기 | WIRED | 0→Slot3DTop, 5→Slot2DSide2, 범위 외→None. |
| `BottomVisionView.TeachButton_Click` | `Matcher.TryTeach(..., VIEW_MODE, _selectedSlot, ...)` | 선택 슬롯 전달 | WIRED | L348-353 슬롯 오버로드 호출 확인. |
| `BottomVisionView.RefreshTeachStatus` | `Matcher.HasTemplate(VIEW_MODE, _selectedSlot)` | 슬롯별 티칭 상태 조회 | WIRED | L653 호출 확인. |
| `SystemHandler.ProcessAlignTest` | `EthernetVisionHandler.Handle.Matcher.Run(img, Bottom, slot)` | AlignFace→슬롯 매핑 후 grab+Run | WIRED | L251(FromAlignFace) → L310(Matcher.Run) 체인 확인. |
| `AlignResult` | `AlignResultPacket.Items(OffsetX/OffsetY/Theta)` | FillAlignPose Items.Add | WIRED | L357-370 Items.Add 3개(OffsetX→OffsetY→Theta 순서). |

---

### Data-Flow Trace (Level 4)

| Artifact | Data Variable | Source | Produces Real Data | Status |
|----------|---------------|--------|--------------------|--------|
| `SystemHandler.ProcessAlignTest` | `AlignResult.OffsetXmm/OffsetYmm/ThetaDeg` | `EthernetVisionHandler.Handle.Camera.Grab()` → `Matcher.Run()` | 실HW=라이브 grab, SIMUL=파일 폴백 | FLOWING (코드), PENDING (실HW 실행) |
| `BottomVisionView.RunButton_Click` | `AlignResult` | `Matcher.Run(_viewer.CurrentImage, VIEW_MODE, _selectedSlot)` | 뷰어 현재 이미지 기반 | FLOWING |
| `AlignResultPacket.Items` | OffsetX/OffsetY/Theta | `FillAlignPose(pResult, res)` — res.OffsetXmm/OffsetYmm/ThetaDeg 직접 대입 | 검출 결과값 실수 | FLOWING |

---

### Behavioral Spot-Checks

Step 7b: SKIPPED — 실HW 이더넷 카메라·PLC 없이 TCP $ALIGN_TEST 흐름을 end-to-end 실행 불가. 코드 레벨 wiring은 Key Link 검증으로 완료.

---

### Requirements Coverage

| Requirement | Source Plan | Description | Status | Evidence |
|-------------|------------|-------------|--------|----------|
| AV-08 | 65-01/02/03/04 | Tray/BottomVisionView 툴바+티칭+검사 결과 패널 + HalconViewer 공유 | SATISFIED (코드), PENDING (실HW UAT) | Plan 65-01: 6슬롯 서비스 기반. Plan 65-02: ComboBox UI + 슬롯별 Teach/Run/HasTemplate. Plan 65-03: ProcessAlignTest 실측 배선. 코드 레벨 충족 확인. 실HW 동작 확인은 작업자 UAT 대기. |
| Phase 64 TCP 연계 | 65-03 | AlignFace 0~5 echo + $ALIGN_RESULT pose 포맷(Phase 64 배경 확장) | SATISFIED | `VisionResponsePacket.cs:882-886` AlignFace 0~5 echo 이미 구현. `BuildAlignResultMessage:529-545` BOTTOM 포맷 `$ALIGN_RESULT:BOTTOM,Mat,AlignFace,OK|NG,OffsetX=val,OffsetY=val,Theta=val@`. 이 Phase는 Items 채움만 추가. |

---

### Anti-Patterns Found

| File | Line | Pattern | Severity | Impact |
|------|------|---------|---------|--------|
| `WPF_Example/Custom/SystemHandler.cs` | 346 | WR-02 (외부 catch에서 FillAlignPoseZero) | 해소됨 | 커밋 d36ea46로 수정 완료. catch 블록에 FillAlignPoseZero 추가. |
| `WPF_Example/Custom/SystemHandler.cs` | 258 | WR-01 (OOB AlignFace NG — Items 빈 채로 응답) | 해소됨 | 커밋 da07f26로 수정 완료. FillAlignPoseZero 추가. |
| `WPF_Example/Custom/UI/BottomVisionView.xaml.cs` | 162 | WR-03 (_slotRois 배열 길이 미검증) | 해소됨 | 커밋 9aa7080으로 수정 완료. savedRois.Length >= 2 가드 추가. |

**잔여 Anti-Pattern: 없음.** 65-REVIEW.md 의 WR-01/WR-02/WR-03 세 경고가 모두 커밋으로 해소됨. IN-01/IN-02/IN-03(정보성)은 비기능성 — 블로커 아님.

---

### CONTEXT.md 결정 준수 확인

| 결정 | 준수 여부 | 증거 |
|------|----------|------|
| D-01: 6슬롯 2그룹(3D 2 + 2D 4) | 준수 | `EBottomAlignSlot` 6값 enum(Slot3DTop~Slot2DSide2) |
| D-02: `Bottom_{slot}_1/2.shm` + `.json` 파일명 | 준수 | `BuildShmPath`: `"Bottom_" + token + "_1/_2.shm"`. `BuildJsonPath`: EndsWith 방식 |
| D-03: AlignFace 0~5 매핑(권위 고정) | 준수 | `FromAlignFace` 0=Slot3DTop~5=Slot2DSide2. VisionResponsePacket comment 일치. |
| D-09: slot None → 기존 Bottom 단일 폴백 | 준수 | `BuildShmPath:91` `slot==None → modeFileName="Bottom"`. 기존 오버로드 TryTeach가 None 위임. |
| D-10: Tray 경로 무변경 | 준수 | `BuildShmPath:95` Tray 분기(`modeFileName="Tray"`) slot 파라미터 무관. |
| D-06/D-07: grab→Run→pose+pass/fail | 준수 | `RunBottomAlign`: Grab→Matcher.Run→FillAlignPose(Items 3개)+IsPass=Found |
| D-08: v3.0 포맷 준수 | 준수 | `FillAlignPose` ItemName="OffsetX"/"OffsetY"/"Theta" — BuildAlignItems가 그대로 직렬화. VisionResponsePacket 무수정. |
| 삼항 연산자 금지 | 준수 | EBottomAlignSlot.cs 전체, AlignShapeMatchService 신규 분기, SystemHandler 신규 메서드 모두 if-else 구현. |
| //260626 hbk 주석 | 준수 | 신규/수정 라인 전체에 주석 확인. |

---

### Human Verification Required

#### 1. 6슬롯 면별 티칭 + 파일 생성 검증

**Test:** Bottom 비전 탭 → cmb_slot에서 슬롯 선택 → 이미지 로드 → ROI 2개 그리기 → 티칭 저장 → 6슬롯 반복. `D:\Data\Recipe\FAI_1\ETHERNET_ALIGN\` 에서 18파일 존재 확인.

**Expected:**
- `Bottom_3D_Top_1.shm`, `Bottom_3D_Top_2.shm`, `Bottom_3D_Top.json`
- `Bottom_3D_Bottom_1.shm`, `Bottom_3D_Bottom_2.shm`, `Bottom_3D_Bottom.json`
- `Bottom_2D_TOP_1.shm` … `Bottom_2D_TOP.json`
- `Bottom_2D_BOTTOM_1.shm` … `Bottom_2D_BOTTOM.json`
- `Bottom_2D_SIDE_1_1.shm`, `Bottom_2D_SIDE_1_2.shm`, **`Bottom_2D_SIDE_1.json`** (오치환 없음 — `Bottom_2D_SIDE.json`가 아님)
- `Bottom_2D_SIDE_2_1.shm` … `Bottom_2D_SIDE_2.json`
- 슬롯 전환 시 HasTemplate 라벨이 슬롯별로 독립적으로 표시됨.

**Why Human:** 실 이더넷 카메라로 이미지를 캡처하고 HALCON Shape 모델을 실제 생성해야 파일 존재를 확인 가능. 특히 `Bottom_2D_SIDE_1.json` 파일명 정확성은 파일 탐색기로만 검증 가능.

#### 2. PLC $ALIGN_TEST AlignFace 0~5 Run + $ALIGN_RESULT pose 검증

**Test:** PLC가 `$ALIGN_TEST:BOTTOM,<자재번호>,<모드>,<AlignFace 0~5>@` 송신 → 비전 응답 확인.

**Expected:**
- `$ALIGN_RESULT:BOTTOM,<자재>,<AlignFace>,OK,OffsetX=val,OffsetY=val,Theta=val@` 형식 응답.
- AlignFace가 echo 정확히 포함됨.
- OffsetX/OffsetY/Theta 수치가 UI Run 결과와 일치.
- AlignFace=6 또는 미티칭 슬롯 → `$ALIGN_RESULT:...,NG,OffsetX=0,OffsetY=0,Theta=0@` 응답 + 로그.
- PLC가 보정값으로 자재를 ideal 안착.

**Why Human:** 실 PLC 통신 및 자재 안착 물리 동작 확인 필요. OffsetX/Y/Theta 수치의 의미적 정확성(mm 단위 보정량이 실제 안착 오차와 일치)은 코드로 검증 불가.

#### 3. 회귀 확인 (Tray / 기존 Bottom 단일 / MainView 검사)

**Test:**
- Tray 비전 탭: 기존 Tray 티칭/Run 정상 동작 확인.
- (옵션) 기존 `Bottom_1.shm`/`Bottom_2.shm`로 slot 미지정 Run 동작 확인.
- MainView 검사(Top/Side/Bottom 측정) 정상 동작 확인.

**Expected:** Tray 경로 무변경, 기존 Bottom 단일 경로 폴백 동작, MainView 검사 영향 없음.

**Why Human:** TrayVisionView/MainView는 코드 grep으로 무변경 확인됐으나, 런타임 통합 동작(탭 전환, 공유 뷰어 분리/부착, 시퀀스 연동)은 실행 환경에서만 검증 가능.

---

### Gaps Summary

코드 수준 필수 항목은 5/5 모두 검증되었다. CONTEXT.md 결정 D-01~D-10 전부 준수. 65-REVIEW.md WR-01/WR-02/WR-03 세 경고가 모두 hotfix 커밋(da07f26/d36ea46/9aa7080)으로 해소됨. 삼항 연산자 규칙, //260626 hbk 주석, C# 7.2 준수 모두 확인.

**인간 게이트 항목만 잔여:**
Plan 65-04 Task 2가 설계상 `autonomous: false` (작업자 실HW UAT 게이트). 실 이더넷 카메라·실 PLC·6종 안착지그가 필요한 항목이므로, 코드 검사로 대체 불가. 작업자 UAT 완료 후 Phase를 SIGNED_OFF 처리한다.

빌드 상태: 최신 커밋(cf6b749 기준) Debug/x64 빌드 CS 에러 0 확인. 변경 파일 6개(EBottomAlignSlot.cs 신규 + AlignShapeMatchService/BottomVisionView(.xaml/.cs)/SystemHandler/csproj 수정) — 예상 범위 내.

---

_Verified: 2026-06-26_
_Verifier: Claude (gsd-verifier)_
