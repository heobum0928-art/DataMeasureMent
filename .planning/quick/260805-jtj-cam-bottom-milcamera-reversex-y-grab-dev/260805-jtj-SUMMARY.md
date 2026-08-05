---
phase: quick-260805-jtj
plan: 01
subsystem: device-camera
tags: [halcon, mil-camera, cxp-frame-grabber, virtual-camera, device-handler]

# Dependency graph
requires:
  - phase: 41-cxp-mil-lite-10-0-grab-hw-01-hw-02
    provides: MilCamera CXP grab 드라이버 초기 구현(GrabHalconImage/GrabFromBuffer/Open)
provides:
  - "VirtualCamera.GrabHalconImage(string requestIdentifier) 역할 인자 오버로드 계약"
  - "MilCamera 역할별 DeviceInfo 맵(_roleInfoMap) + grab 시점 방향/회전 재적용"
  - "DeviceHandler/Action_BottomInspection 소비처 배선(요청자 식별자 전달)"
affects: [실HW-CXP-배치, phase-41-HW-UAT, CAM_BOTTOM-검사]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "요청자 식별자(string requestIdentifier) 기반 grab 오버로드 — 물리 리소스는 공유하되 방향/회전 같은 역할별 설정은 grab 호출 시점에 재적용"

key-files:
  created: []
  modified:
    - WPF_Example/Device/Camera/VirtualCamera.cs
    - WPF_Example/Device/Camera/Mil/MilCamera.cs
    - WPF_Example/Device/DeviceHandler.cs
    - WPF_Example/Custom/Sequence/Bottom/Action_BottomInspection.cs

key-decisions:
  - "물리 MilCamera 인스턴스 공유(1개, MsysAlloc 1회) 는 그대로 유지 — 역할별 DeviceInfo만 별도 맵으로 관리"
  - "GrabFromBuffer가 매 grab 직전 MdigControl로 X/Y 방향을 재적용하여 Open() 시점 반영값(한쪽 카메라에만 유효했던 문제)을 대체"
  - "래퍼(DeviceHandler.GrabHalconImage(ICameraParam))를 거치지 않는 유일한 직접호출 경로(Action_BottomInspection.cs)도 동일 계약으로 배선"

patterns-established:
  - "역할별 grab 방향 재적용 패턴: ResolveRoleInfo(requestIdentifier) → GrabFromBuffer(roleInfo) → MdigControl 재적용"

requirements-completed: [FIX-JTJ-01]

# Metrics
duration: 5min
completed: 2026-08-05
---

# Quick 260805-jtj: CAM_BOTTOM MilCamera ReverseX/Y grab 시점 미반영 버그 수정 Summary

**MilCamera에 역할별 DeviceInfo 맵을 도입해, Top/Bottom이 물리 MIL 카메라 인스턴스를 공유해도 CAM_BOTTOM의 REVERSE_X_BOTTOM=true가 grab 시점에 실제 MdigControl 방향 반전으로 재적용되도록 수정**

## Performance

- **Duration:** 5 min (14:26 시작 ~ 14:33 완료, 커밋 타임스탬프 기준)
- **Started:** 2026-08-05T14:26:00+09:00 (추정, 계획 커밋 직후)
- **Completed:** 2026-08-05T14:32:46+09:00
- **Tasks:** 2 completed
- **Files modified:** 4

## Accomplishments
- VirtualCamera에 `GrabHalconImage(string requestIdentifier)` 가상 오버로드 추가 — Basler/HikCamera는 무인자 버전만 override 하므로 수정 없이 자동으로 새 오버로드의 기본 구현(무인자 버전 위임)을 그대로 사용
- MilCamera에 `_roleInfoMap`/`RegisterRoleInfo`/`ResolveRoleInfo`를 도입해 CAM_TOP/CAM_BOTTOM 각각의 DeviceInfo(ReverseX/ReverseY/RotateAngle)를 보관
- `GrabFromBuffer(DeviceInfo roleInfo)`가 `MdigGrab` 직전 `MdigControl(MIL_GRAB_DIRECTION_X/Y)`로 요청자 역할의 방향을 재적용하고, 회전 3분기(_90/_180/_270)도 roleInfo 기준으로 처리
- DeviceHandler의 범용 grab 래퍼(`GrabHalconImage(ICameraParam)`)가 `param.DeviceName`을 전달하도록 배선 — 이 래퍼를 거치는 Action_TopInspection.cs/Action_FAIMeasurement.cs/MainView.xaml.cs는 수정 없이 자동 적용
- DeviceHandler.Initialize()의 MIL 공유 등록 분기에서 `sharedMil.RegisterRoleInfo(id)`를 `Devices.Add` 직전에 호출해 역할별 DeviceInfo를 실제로 등록
- Action_BottomInspection.cs의 래퍼를 거치지 않는 유일한 직접 grab 호출부도 `pMyParam.DeviceName`을 전달하도록 수정

## Task Commits

Each task was committed atomically:

1. **Task 1: 역할별 grab 오버로드 계약 정의 + MilCamera 구현** - `a17ee1a` (fix)
2. **Task 2: 소비처 배선 — DeviceHandler 등록/래퍼 + Action_BottomInspection 직접호출** - `b29c7fa` (fix)

**Plan metadata:** (오케스트레이터가 이후 별도 커밋 예정 — SUMMARY.md/STATE.md/PLAN.md는 이 에이전트가 커밋하지 않음)

## Files Created/Modified
- `WPF_Example/Device/Camera/VirtualCamera.cs` - `GrabHalconImage(string requestIdentifier)` 가상 오버로드 추가(무인자 버전으로 위임하는 기본 구현)
- `WPF_Example/Device/Camera/Mil/MilCamera.cs` - `_roleInfoMap`/`RegisterRoleInfo`/`ResolveRoleInfo` 추가, 생성자 자기등록, `GrabHalconImage()→GrabHalconImage(Info.Identifier)` 위임, `GrabHalconImage(string)` 신설, `GrabFromBuffer(DeviceInfo roleInfo)`로 시그니처 변경(방향 재적용 2줄 + 실패로그 1곳 + 회전 3분기를 roleInfo 기준으로), `LiveLoop()`은 `GrabFromBuffer(Info)`로 컴파일 대응(동작 변경 없음)
- `WPF_Example/Device/DeviceHandler.cs` - `GrabHalconImage(ICameraParam)` 마지막 줄이 `param.DeviceName` 전달, MIL 공유 분기에 `sharedMil.RegisterRoleInfo(id)` 추가
- `WPF_Example/Custom/Sequence/Bottom/Action_BottomInspection.cs` - `EStep.Grab`의 직접 grab 호출이 `pMyParam.DeviceName` 전달

## Decisions Made
- MilCamera.Open() 167-168행의 기존 삼항 연산자(`Info.ReverseX ? MIL.M_REVERSE : MIL.M_NORMAL`)는 계획 지침대로 손대지 않고 그대로 유지. Open() 시점 값은 이제 GrabFromBuffer가 매 grab마다 재적용하는 값으로 항상 덮어써지므로 무해한 초기 기본값으로 남는다.
- LiveLoop()은 자기 자신의 `Info`를 그대로 사용하도록 `GrabFromBuffer(Info)`로 컴파일만 맞췄다 — 라이브 미리보기 동작 변경 없음(계획 명시 사항).

## Deviations from Plan

None - plan executed exactly as written. 코드는 계획의 `<action>` 섹션에 제시된 변수명/시그니처를 그대로 사용했다.

## Issues Encountered

Task 1 검증 스크립트의 `old_call_zeroarg` grep(`GrabFromBuffer\(\)` want0)이 1건 매치되었으나, 이는 실제 호출부가 아니라 46행 근처의 사전 존재 주석(`// (GrabFromBuffer() 의 MappGetError 체크와 동일 패턴 필요)`)이 메서드 이름을 언급한 것이었다. `git diff`로 이 줄이 이번 커밋에서 변경되지 않았음을 확인했고(diff에 나타나지 않음), 실제 호출부 3곳(`GrabHalconImage(string)` 내부, `LiveLoop()`)은 모두 `GrabFromBuffer(roleInfo)`/`GrabFromBuffer(Info)`로 정상 치환되어 있음을 별도 확인했다. 코드 결함이 아니므로 수정하지 않음.

## User Setup Required

None - no external service configuration required.

## Carry-over

**non-SIMUL_MODE(실HW) 분기 런타임 검증 미수행** — 이 개발 PC는 물리 CXP 프레임그래버 보드가 없는 SIMUL 전용 랩탑이며, `DatumMeasurement.csproj`의 4개 빌드 설정(Debug/AnyCPU, Debug/x64, Release/AnyCPU, Release/x64) 모두 `SIMUL_MODE`가 정의되어 있어 non-SIMUL_MODE 분기(이번 수정의 실제 대상)를 이 PC에서는 컴파일조차 할 수 없었다. 이번 plan의 자동 검증은 Debug/x64(SIMUL_MODE) 빌드 성공(error CS 0건, 신규 warning CS 0건) + grep 기반 정적 코드 검토로 한정되었다. 실HW PC 배치 시 CAM_BOTTOM grab 이미지가 CAM_TOP 대비 좌우 반전되어 나오는지(REVERSE_X_BOTTOM=true 정상 적용 여부) 반드시 육안 확인이 필요하다.

## Next Phase Readiness

- 코드 변경은 완료·빌드 통과했으나 이번 버그 수정의 핵심 검증(실HW non-SIMUL_MODE 분기에서 CAM_BOTTOM 이미지 좌우 반전 확인)은 실HW PC에서만 가능 — 위 Carry-over 항목 참조.
- 이 변경은 물리 MIL 인스턴스 공유 자체나 Top/Bottom 상호배제 정책을 바꾸지 않으므로(threat_model T-JTJ-02 참조), 회귀 위험은 낮다.

## Self-Check: PASSED

- FOUND: WPF_Example/Device/Camera/VirtualCamera.cs
- FOUND: WPF_Example/Device/Camera/Mil/MilCamera.cs
- FOUND: WPF_Example/Device/DeviceHandler.cs
- FOUND: WPF_Example/Custom/Sequence/Bottom/Action_BottomInspection.cs
- FOUND: a17ee1a
- FOUND: b29c7fa

---
*Phase: quick-260805-jtj*
*Completed: 2026-08-05*
