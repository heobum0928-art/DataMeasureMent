---
phase: 66-ring7-coax-align-2026-06-26
plan: 03
subsystem: align-vision
tags: [halcon, align, coax, light, wpf, xaml, bottom-vision, tray-vision, ui]

# Dependency graph
requires:
  - phase: 66-ring7-coax-align-2026-06-26 plan 02
    provides: "AlignShapeMatchService.GetSlotRefPose/TrySaveCoax API + AlignRefPose.CoaxEnabled/CoaxLevel POCO 필드"
  - phase: 66-ring7-coax-align-2026-06-26 plan 01
    provides: "LightHandler.LIGHT_ALIGN_COAX 상수 등록"
provides:
  - "BottomVisionView 좌측 패널 동축 GroupBox(chk_coaxEnabled + sld_coaxLevel + lbl_coaxLevel) — Row6"
  - "TrayVisionView 좌측 패널 동축 GroupBox(chk_coaxEnabled + sld_coaxLevel + lbl_coaxLevel) — Row5"
  - "Bottom: 슬롯 전환 시 GetSlotRefPose로 동축값 복원 + 체크/슬라이더 변경 시 TrySaveCoax로 슬롯 JSON 저장"
  - "Tray: Loaded 시 GetSlotRefPose(slot=None)로 동축값 복원 + 변경 시 TrySaveCoax(slot=None)로 Tray.json 저장"
  - "Grab(#else)/Teach/Run 직전 ApplyCoaxLight() 자동 적용 — 티칭=런타임 조명 일치(D-07)"
affects: []

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "동축 자동 적용 패턴: ApplyCoaxLight() → Camera.Grab()/TryTeach()/Matcher.Run() 순서(D-07)"
    - "슬롯 동축 복원 패턴: 슬롯 전환 이벤트 끝에 GetSlotRefPose→chk/sld/lbl 갱신"
    - "WPF 동축 컨트롤: 표준 CheckBox + Slider + TextBlock(PropertyTools HeaderedEntrySlider 미사용 — PATTERNS.md #4)"

key-files:
  created: []
  modified:
    - "WPF_Example/Custom/UI/BottomVisionView.xaml"
    - "WPF_Example/Custom/UI/BottomVisionView.xaml.cs"
    - "WPF_Example/Custom/UI/TrayVisionView.xaml"
    - "WPF_Example/Custom/UI/TrayVisionView.xaml.cs"

key-decisions:
  - "XAML airspace-safe 배치: 동축 GroupBox는 좌측 WPF 패널(네이티브 HWND 밖)에만 배치 — HALCON 뷰어 위 오버레이 없음"
  - "BottomVisionView using ReringProject.Device 추가: partial class 파일별 using 독립(TrayVisionView는 이미 존재)"
  - "AlignRefPose 루트 네임스페이스 접근: using ReringProject; 없이도 자식 ns(ReringProject.Custom.UI)에서 컴파일 정상"
  - "SIMUL_MODE Grab 분기(파일 다이얼로그): 실카메라 없음 → ApplyCoaxLight() 추가 불필요(#else에만 추가)"

patterns-established:
  - "try/catch + throw 금지 패턴: 동축 핸들러 예외 시 lbl_status.Text 갱신만(T-66-UI-01 완화)"
  - "SaveSlotCoaxToJson/SaveTrayCoaxToJson 즉시 저장: CheckBox/Slider 변경 즉시 TrySaveCoax 호출(수동 override 즉시 반영)"

requirements-completed: [AV-08]

# Metrics
duration: 15min
completed: 2026-06-29
---

# Phase 66 Plan 03: Align 동축 UI Summary

**Bottom/Tray Align 창 좌측 패널에 동축 CheckBox+Slider 추가, 슬롯별/Tray 단일 JSON 복원·저장, Grab/Teach/Run 직전 LIGHT_ALIGN_COAX 자동 적용으로 티칭=런타임 조명 일치(D-07) 구현**

## Performance

- **Duration:** 15 min
- **Started:** 2026-06-29T00:00:00Z
- **Completed:** 2026-06-29T00:15:00Z
- **Tasks:** 3 (Task 1 Bottom UI + Task 2 Tray UI + Task 3 빌드 검증)
- **Files modified:** 4

## Accomplishments

- BottomVisionView.xaml: 동축 GroupBox(chk_coaxEnabled + sld_coaxLevel + lbl_coaxLevel) Row6 추가, RowDefinition 7→8개, 여백→Row7
- BottomVisionView.xaml.cs: using ReringProject.Device 추가 + ApplyCoaxLight/CoaxCheckBox_Changed/CoaxSlider_ValueChanged/SaveSlotCoaxToJson/LoadSlotCoaxToUi 5개 메서드 추가. 슬롯 전환 시 복원 + Grab(#else)/Teach/Run 직전 자동 적용
- TrayVisionView.xaml: 동축 GroupBox Row5 추가, RowDefinition 6→7개, 여백→Row6
- TrayVisionView.xaml.cs: ApplyCoaxLight/CoaxCheckBox_Changed/CoaxSlider_ValueChanged/SaveTrayCoaxToJson/LoadTrayCoaxToUi 5개 메서드 추가. Loaded 복원 + Grab(#else)/Teach/Run 직전 자동 적용. slot=None으로 Tray 단일 경로 처리
- msbuild Debug/x64 PASS, error 0건, warning은 기존 미변경 항목만

## Task Commits

각 태스크를 개별 커밋:

1. **Task 1: BottomVisionView 동축 컨트롤 + 슬롯 복원/저장 + 자동적용** - `4f0d8f6` (feat)
2. **Task 2: TrayVisionView 동축 컨트롤 + 단일 복원/저장 + 자동적용** - `1d47a6d` (feat)
3. **Task 3: 빌드 검증 (Debug/x64) PASS** - `ada1387` (chore)

## Files Created/Modified

- `WPF_Example/Custom/UI/BottomVisionView.xaml` — 동축 GroupBox Row6 추가, RowDefinition 8개
- `WPF_Example/Custom/UI/BottomVisionView.xaml.cs` — using Device + 동축 5개 메서드 + 자동 적용 3곳
- `WPF_Example/Custom/UI/TrayVisionView.xaml` — 동축 GroupBox Row5 추가, RowDefinition 7개
- `WPF_Example/Custom/UI/TrayVisionView.xaml.cs` — 동축 5개 메서드 + Loaded 복원 + 자동 적용 3곳

## Decisions Made

- airspace-safe: 동축 컨트롤을 좌측 WPF 패널(Grid Column 0)에만 배치 — HALCON HWND(Column 1) 위 WPF 오버레이 없음
- SIMUL_MODE Grab(파일 다이얼로그) 분기는 실카메라 없음으로 ApplyCoaxLight() 미추가 (#else 분기에만 추가)
- TrayVisionView는 슬롯 개념 없음 → slot=None으로 Tray.json 단일 경로 처리(D-05)

## Deviations from Plan

None - plan executed exactly as written. AlignRefPose 루트 네임스페이스 접근이 `using ReringProject;` 없이도 빌드 통과되어 추가 불필요(자식 ns 자동 접근).

## Issues Encountered

- MSBuild.rsp 응답 파일이 Git Bash에서 실행 시 `/p:` 스위치를 제거하는 문제 → 배치 파일(.bat) 경유로 우회

## Known Stubs

없음. 모든 동축 컨트롤이 실제 LightHandler.Handle.SetOnOff/SetLevel + AlignShapeMatchService.GetSlotRefPose/TrySaveCoax로 연결됨.

## Next Phase Readiness

- Phase 66 전체 3개 plan 완료. AV-08(Align 동축 조명) 요구사항 충족.
- 실HW 연결 후 작업자 UAT: Bottom 슬롯별 동축 티칭 + Tray 동축 티칭 → Run 시 조명 재현 확인 필요.

---
*Phase: 66-ring7-coax-align-2026-06-26*
*Completed: 2026-06-29*

## Self-Check: PASSED
