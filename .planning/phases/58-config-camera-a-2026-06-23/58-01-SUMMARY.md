---
phase: 58-config-camera-a-2026-06-23
plan: "01"
subsystem: setting
tags: [ethernet-vision, ini, config, enum, av-01]
dependency_graph:
  requires: []
  provides: [EEthernetVisionMode-enum, ETHERNET_VISION-ini-section]
  affects: [WPF_Example/Custom/SystemSetting.cs, WPF_Example/DatumMeasurement.csproj]
tech_stack:
  added: []
  patterns: [int-backing-enum-ini, AfterLoad-missing-key-restoration, Phase-48-RestoreXxxDefault]
key_files:
  created:
    - WPF_Example/Custom/EthernetVision/EEthernetVisionMode.cs
  modified:
    - WPF_Example/Custom/SystemSetting.cs
    - WPF_Example/DatumMeasurement.csproj
decisions:
  - "EEthernetVisionMode namespace = ReringProject.Setting (same as SystemSetting, no extra using needed)"
  - "int-backing property pattern (EthernetVisionModeValue) because INI reflection Load switch has no enum case"
  - "RestoreEthernetVisionDefault() only guards PixelResolution (None=0 and empty IP are acceptable missing-key results)"
  - "ADD-ONLY: existing Phase 48 CameraRoleValue/ECameraRole/RestorePcRoleDefault preserved intact"
metrics:
  duration_minutes: 10
  completed_date: "2026-06-23"
  tasks_completed: 2
  files_changed: 3
---

# Phase 58 Plan 01: [ETHERNET_VISION] Config Surface Summary

**One-liner:** `[ETHERNET_VISION]` INI 섹션 추가 — `EEthernetVisionMode` 열거형(None/Tray/Bottom) + 4개 SystemSetting 프로퍼티(int-백킹 enum + IP + 노출 + 픽셀분해능) + missing-key 8.652 기본값 복원.

## Tasks Completed

| Task | Name | Commit | Files |
|------|------|--------|-------|
| 1 | EEthernetVisionMode enum + csproj 등록 | a91f0f8 | EEthernetVisionMode.cs (new), DatumMeasurement.csproj |
| 2 | [ETHERNET_VISION] 프로퍼티 + RestoreEthernetVisionDefault | 604e4c8 | Custom/SystemSetting.cs |

## What Was Built

### Task 1: EEthernetVisionMode enum

`WPF_Example/Custom/EthernetVision/EEthernetVisionMode.cs` (신규):
- namespace `ReringProject.Setting` (SystemSetting과 동일 어셈블리 — extra using 불필요)
- `None = 0` / `Tray = 1` / `Bottom = 2`
- Allman brace style (EImageSource.cs 선례)
- `DatumMeasurement.csproj` Compile ItemGroup에 등록

### Task 2: [ETHERNET_VISION] INI 섹션

`WPF_Example/Custom/SystemSetting.cs` (수정):
- `private const double ETHERNET_PIXEL_RESOLUTION_DEFAULT = 8.652`
- `EthernetVisionModeValue` (int, `[Category("ETHERNET_VISION")]`) + `EthernetVisionMode` (EEthernetVisionMode, `[Browsable(false)]`) — int-backing enum 패턴 (Phase 48 CameraRoleValue 선례)
- `EthernetCameraIp` (string, "192.168.1.100"), `EthernetExposure` (double, 10000.0), `EthernetPixelResolution` (double, 8.652)
- `AfterLoad()` 확장: 기존 `RestorePcRoleDefault()` 보존 + `RestoreEthernetVisionDefault()` 추가 호출
- `RestoreEthernetVisionDefault()`: `bool bPixelResolutionMissing = EthernetPixelResolution <= 0.0` → 조건 충족 시 `ETHERNET_PIXEL_RESOLUTION_DEFAULT` 복원

## Decisions Made

1. `EEthernetVisionMode` 네임스페이스 = `ReringProject.Setting` — SystemSetting.cs와 동일 파일 내 using 없이 참조 가능.
2. int-backing 프로퍼티 패턴 채택 (`EthernetVisionModeValue`) — INI reflection Load switch가 Int32만 처리하며 enum case 없음 (D-12 AlgorithmType string 선례와 동일 이유).
3. `RestoreEthernetVisionDefault()`는 PixelResolution만 방어 — Mode 누락 시 None(=0)이 올바른 기본값이고, IP/Exposure 누락 시 ""/"0"은 Phase 62 연결 시점에서 처리 가능.
4. ADD-ONLY 원칙 준수 — Phase 48 `CameraRoleValue`, `ECameraRole`, `PC_ROLE_DEFAULT`, `RestorePcRoleDefault()` 전부 무수정 보존.

## Deviations from Plan

None — 계획 대로 정확히 실행됨.

## Threat Flags

없음 — 로컬 INI 읽기/쓰기만 해당. T-58-01(Tampering, accept disposition) 계획에 기록된 대로.

## Known Stubs

없음 — 이 plan은 config 데이터 모델만 추가하며 UI/동작 연결은 Plan 02~03 범위.

## Self-Check: PASSED

- `WPF_Example/Custom/EthernetVision/EEthernetVisionMode.cs` 존재 확인
- `WPF_Example/Custom/SystemSetting.cs` ETHERNET_PIXEL_RESOLUTION_DEFAULT / EthernetPixelResolution / EthernetVisionModeValue / RestoreEthernetVisionDefault / RestorePcRoleDefault 전부 확인
- `partial void AfterLoad()` 정의 1개 확인 (주석 포함 2회 등장하나 메서드 정의는 1개)
- 커밋 a91f0f8, 604e4c8 존재 확인
