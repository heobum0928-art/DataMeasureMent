---
phase: 66-ring7-coax-align-2026-06-26
plan: 02
subsystem: align-vision
tags: [halcon, align, coax, light, json, tcp, ethernet-vision]

# Dependency graph
requires:
  - phase: 65-bottom-4jig-face-align-2026-06-25
    provides: "AlignShapeMatchService 슬롯 JSON 경로(BuildJsonPath), AlignRefPose POCO, RunBottomAlign 구조, EBottomAlignSlot enum"
  - phase: 66-ring7-coax-align-2026-06-26 plan 01
    provides: "LightHandler.LIGHT_ALIGN_COAX 상수 등록 확인 (D-04)"
provides:
  - "AlignRefPose.CoaxEnabled/CoaxLevel POCO 필드 — 슬롯/Tray JSON 스키마 확장"
  - "AlignShapeMatchService.GetSlotRefPose — 슬롯 동축값 공개 로드 래퍼"
  - "AlignShapeMatchService.TrySaveCoax — load-merge-save 동축 저장 (티칭 데이터 보존)"
  - "SystemHandler.ApplyCoaxLightForSlot — grab 직전 슬롯 동축 자동 적용 (D-06)"
affects: [66-ring7-coax-align-2026-06-26 plan 03 UI]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "load-merge-save: AlignRefPose JSON 로드 → 특정 필드만 갱신 → 재저장 (티칭 데이터 보존)"
    - "TCP 스레드 동축 폴백: GetSlotRefPose null → bEnabled=false → 동축 OFF (throw 금지, T-66-01)"

key-files:
  created: []
  modified:
    - "WPF_Example/Custom/EthernetVision/AlignRefPose.cs"
    - "WPF_Example/Custom/EthernetVision/AlignShapeMatchService.cs"
    - "WPF_Example/Custom/SystemHandler.cs"

key-decisions:
  - "TrySaveCoax(load-merge-save) 채택: 기존 TrySaveRefPose 시그니처 무변경, 별도 public API로 동축값만 갱신"
  - "GetSlotRefPose: private BuildJsonPath+LoadRefPose 위임 래퍼 — TCP/UI 양측 진입점"
  - "ApplyCoaxLightForSlot 단일 헬퍼: null 폴백 off + 예외 catch → off (TCP 스레드 크래시 방지)"
  - "Custom/SystemHandler.cs 에 using ReringProject.Device 명시 (partial class 파일별 using 독립)"

patterns-established:
  - "슬롯 JSON 필드 추가 = AlignRefPose POCO에 C# 기본값 필드 추가만으로 Newtonsoft.Json 키 부재 자동 폴백(하위호환)"
  - "grab 직전 조명 자동 적용 패턴: ApplyCoaxLightForSlot → Grab() 순서 (검사 ApplyShotLights 패턴 일관)"

requirements-completed: [AV-08]

# Metrics
duration: 8min
completed: 2026-06-28
---

# Phase 66 Plan 02: Align 동축 조명 백엔드 Summary

**AlignRefPose JSON에 CoaxEnabled/CoaxLevel 필드 추가 + GetSlotRefPose/TrySaveCoax public API + RunBottomAlign grab 직전 슬롯 동축 자동 ON (D-05/D-06/D-07)**

## Performance

- **Duration:** 8 min
- **Started:** 2026-06-28T23:43:52Z
- **Completed:** 2026-06-28T23:51:49Z
- **Tasks:** 4 (Task 1~3 기능 + Task 4 빌드 검증)
- **Files modified:** 3

## Accomplishments

- AlignRefPose POCO에 `CoaxEnabled`(bool) + `CoaxLevel`(int) 추가 — 기존 11필드 보존, 구 JSON 키 부재 시 false/0 자동 폴백(하위호환 충족)
- AlignShapeMatchService에 `GetSlotRefPose`(공개 로드 래퍼) + `TrySaveCoax`(load-merge-save) 2개 public API 추가 — 기존 private 메서드 무변경
- SystemHandler에 `ApplyCoaxLightForSlot` 헬퍼 추가 + RunBottomAlign grab 직전 자동 호출 — 미티칭/예외 시 동축 OFF 안전 폴백, throw 없음(TCP 스레드 보호)
- msbuild Debug/x64 PASS (error 0, warning은 기존 미변경 항목만)

## Task Commits

각 태스크를 개별 커밋:

1. **Task 1: AlignRefPose CoaxEnabled/CoaxLevel 추가** - `98d92d7` (feat)
2. **Task 2: AlignShapeMatchService GetSlotRefPose + TrySaveCoax 추가** - `51818b5` (feat)
3. **Task 3: SystemHandler ApplyCoaxLightForSlot + RunBottomAlign 훅** - `d19b2ae` (feat)
4. **Task 4 편차 (Rule 1 버그 수정): using ReringProject.Device 추가** - `f8fb010` (fix)

## Files Created/Modified

- `WPF_Example/Custom/EthernetVision/AlignRefPose.cs` — CoaxEnabled/CoaxLevel 필드 +2
- `WPF_Example/Custom/EthernetVision/AlignShapeMatchService.cs` — GetSlotRefPose + TrySaveCoax public API +63줄
- `WPF_Example/Custom/SystemHandler.cs` — using ReringProject.Device + ApplyCoaxLightForSlot 헬퍼 + grab 직전 호출 +36줄

## Decisions Made

- `TrySaveCoax` load-merge-save 채택: PATTERNS.md의 TrySaveRefPose 시그니처 확장 대신, 별도 public 메서드로 티칭 임계 경로(TryTeach) 회귀 0 보장
- `GetSlotRefPose` mode+slot 2인자 — Tray(slot=None)와 Bottom 슬롯을 단일 시그니처로 처리

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Custom/SystemHandler.cs에 using ReringProject.Device 누락**
- **Found during:** Task 4 (빌드 검증)
- **Issue:** `ApplyCoaxLightForSlot`에서 `LightHandler` 참조 시 CS0103 오류 — Custom/SystemHandler.cs partial class 파일에 `using ReringProject.Device;`가 없었음. 기본 SystemHandler.cs에는 있으나 partial class는 파일별로 using이 독립 적용됨.
- **Fix:** Custom/SystemHandler.cs 첫 줄에 `using ReringProject.Device;` 추가
- **Files modified:** `WPF_Example/Custom/SystemHandler.cs`
- **Verification:** msbuild Debug/x64 재빌드 → error 0 확인
- **Committed in:** `f8fb010` (빌드 수정 커밋)

---

**Total deviations:** 1 auto-fixed (Rule 1 - 컴파일 블로킹 버그)
**Impact on plan:** 필수 수정. 범위 변경 없음.

## Issues Encountered

- partial class 파일별 using 독립 이슈 — Custom/SystemHandler.cs는 기본 SystemHandler.cs의 using을 공유하지 않음. 향후 LightHandler 참조 추가 시 동일 파일에 using 필요.

## Known Stubs

없음 — 모든 구현이 실제 로직으로 완성됨. GetSlotRefPose null 반환 시 ApplyCoaxLightForSlot이 동축 OFF 안전 폴백하므로 미티칭 슬롯도 정상 동작.

## Next Phase Readiness

- Plan 03(UI)이 소비할 공개 API 준비 완료:
  - `AlignShapeMatchService.GetSlotRefPose(EEthernetVisionMode mode, EBottomAlignSlot slot) → AlignRefPose`
  - `AlignShapeMatchService.TrySaveCoax(EEthernetVisionMode mode, EBottomAlignSlot slot, bool coaxEnabled, int coaxLevel, out string error) → bool`
- Plan 03는 BottomVisionView/TrayVisionView UI에서 이 API를 호출하여 CheckBox/Slider 저장 구현 예정

---
*Phase: 66-ring7-coax-align-2026-06-26*
*Completed: 2026-06-28*

## Self-Check: PASSED
