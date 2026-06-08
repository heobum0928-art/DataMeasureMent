---
phase: 41-cxp-mil-lite-10-0-grab-hw-01-hw-02
plan: "03"
subsystem: device-driver
tags: [mil, cxp, camera, device-handler, system-setting, registration, role, simul-mode]
dependency_graph:
  requires: [41-02 — MilCamera : VirtualCamera 드라이버 클래스]
  provides: [DeviceHandler ECameraType.MIL factory switch, PC별 CXP 등록, CameraRole INI 설정]
  affects:
    - WPF_Example/Device/DeviceHandler.cs
    - WPF_Example/Custom/Device/DeviceHandler.cs
    - WPF_Example/Custom/SystemSetting.cs
tech_stack:
  added: []
  patterns:
    - int 백킹 프로퍼티로 enum INI 직렬화 우회 (D-12 AlgorithmType string 선례 동일 패턴)
    - DeviceHandler switch case ECameraType.MIL — enumerate 없음, Open() 으로 판별
    - RegisterRequiredDevices SystemSetting.Handle.CameraRole if/else 역할 분기
    - SIMUL_MODE AddVirtualCamera 폴백 — HW 미설치 시 앱 기동 차단 방지 (T-41-06)
key_files:
  created: []
  modified:
    - WPF_Example/Custom/SystemSetting.cs
    - WPF_Example/Device/DeviceHandler.cs
    - WPF_Example/Custom/Device/DeviceHandler.cs
decisions:
  - "ECameraRole enum을 ReringProject.Setting namespace에 배치 — Custom/SystemSetting.cs 단일 정의, Device/Custom 두 곳 using 참조"
  - "CameraRoleValue int 백킹 프로퍼티 + [Browsable(false)] CameraRole 변환 프로퍼티 — Save/Load switch(type) enum 미지원 회피 (Phase 12 D-12 선례)"
  - "MIL case: enumerate 없이 Open() 성공/실패로만 판별 — HIK EnumerateDevice/ContainsDevice 단계 불필요 (MdigAlloc 직접 연결)"
  - "PC1(TopBottom): CAMERA_TOP + CAMERA_BOTTOM 각각 ECameraType.MIL로 등록 — 동일 물리 카메라 2개 시퀀스 담당 (RESEARCH Open Q2 권고)"
  - "SIMUL_MODE 폴백: MIL Open 실패 시 AddVirtualCamera(id) — 기존 3 시퀀스 파일 grab 경로 회귀 0 (D-11)"
metrics:
  duration_seconds: 181
  completed_date: "2026-06-02"
  tasks_completed: 3
  files_modified: 3
---

# Phase 41 Plan 03: DeviceHandler MIL Factory + 역할 기반 등록 Summary

**One-liner:** DeviceHandler switch에 ECameraType.MIL case를 추가하고 RegisterRequiredDevices를 SystemSetting CameraRole(TopBottom/Side) 기반 PC별 CXP 1대 등록으로 재구성하여 MilCamera가 시퀀스 grab 경로에 통합됨.

## Tasks Completed

| Task | Name | Commit | Files |
|------|------|--------|-------|
| 1 | SystemSetting CameraRole INI 설정 + ECameraRole enum | 12c7aaf | WPF_Example/Custom/SystemSetting.cs |
| 2 | DeviceHandler switch case ECameraType.MIL + SIMUL 폴백 | 72ad767 | WPF_Example/Device/DeviceHandler.cs |
| 3 | RegisterRequiredDevices 역할 분기 재구성 + CXP 상수 + 빌드 | 5554f0f | WPF_Example/Custom/Device/DeviceHandler.cs |

## Verification Results

- msbuild Debug/x64 Rebuild: **0 errors** (`DatumMeasurement.exe` 생성 확인)
- 신규 warning: 0 (기존 CS0618×3 + CS0162×1 + MSB3884×2 = 베이스라인 일치)
- `case ECameraType.MIL:` + `new MilCamera(Config, id)`: FOUND
- `#if SIMUL_MODE AddVirtualCamera(id)` 폴백: FOUND
- `ECameraRole` enum (TopBottom/Side): FOUND
- `CameraRoleValue int` + `[Browsable(false)] CameraRole`: FOUND
- `SystemSetting.Handle.CameraRole` 역할 분기: FOUND
- `WIDTH_CXP` / `HEIGHT_CXP` 상수: FOUND
- 기존 HIK case 무변경: CONFIRMED

## Plan Truths Verification

| Truth | Status |
|-------|--------|
| DeviceHandler.InitializeDevices switch 가 ECameraType.MIL 케이스에서 new MilCamera 를 인스턴스화한다 | VERIFIED |
| MIL Open 실패 시 SIMUL_MODE 에서 AddVirtualCamera 폴백으로 초기화가 계속된다 | VERIFIED |
| RegisterRequiredDevices 가 SystemSetting CameraRole(TopBottom/Side) 기반으로 PC별 CXP 1대를 등록한다 | VERIFIED |
| SystemSetting 이 CameraRole 을 INI 직렬화 가능한 타입(int)으로 영속화한다 | VERIFIED |
| 솔루션이 msbuild Debug/x64 0 errors 빌드된다 | VERIFIED |

## Deviations from Plan

None - 계획대로 정확히 실행.

## Key Findings (read_first 확인 결과)

- Custom/SystemSetting.cs 에 이미 `using System.ComponentModel` import 있음 → `[Browsable(false)]` 추가 없이 사용 가능
- Custom/Device/DeviceHandler.cs 에 `using ReringProject.Setting` 없음 → Task 3에서 추가
- DeviceHandler.cs switch 의 `id`, `devName` 변수는 L98-101 for 루프 내 선언 → MIL case에서 추가 선언 없이 사용 가능 (CS0136 미발생)
- `id.Identifier`가 `devName`과 동일 값 → `Devices.Add(id.Identifier, newCam)` 패턴으로 HIK의 `Devices.Add(devName, newCam)` 와 동등
- REVERSE_X_BOTTOM = true (기존 설정) → RegisterRequiredDevices에서 그대로 전달하여 이미지 방향 보존

## Known Stubs

- `WIDTH_CXP = 14192`, `HEIGHT_CXP = 10640`: ViewWorks VNP-604MX 기준 추정값 (TBD). 카메라 실물 도착 후 `MdigInquire(MIL.M_SIZE_X/Y)` 또는 데이터시트로 확정 필요 (RESEARCH Open Q3).

## Threat Flags

없음 — T-41-05/T-41-06 모두 PLAN.md 위협 등록과 일치하여 mitigate 구현 완료:
- T-41-05 (Tampering/잘못된 CameraRoleValue): if (role == ECameraRole.TopBottom) ... else → else 분기가 미정의 정수값도 Side 경로로 안전 폴백 (크래시 없음)
- T-41-06 (Denial of Service/MIL Open 실패): SIMUL_MODE AddVirtualCamera 폴백으로 초기화 계속 (IsInitializeFail 차단 방지)

## Self-Check: PASSED

- `WPF_Example/Custom/SystemSetting.cs` 존재: FOUND (18 라인 추가)
- `WPF_Example/Device/DeviceHandler.cs` case MIL: FOUND (L221)
- `WPF_Example/Custom/Device/DeviceHandler.cs` WIDTH_CXP/역할 분기: FOUND
- commit 12c7aaf 존재: FOUND (feat(41-03): ECameraRole enum + CameraRole INI)
- commit 72ad767 존재: FOUND (feat(41-03): DeviceHandler switch case MIL)
- commit 5554f0f 존재: FOUND (feat(41-03): RegisterRequiredDevices 역할 분기)
