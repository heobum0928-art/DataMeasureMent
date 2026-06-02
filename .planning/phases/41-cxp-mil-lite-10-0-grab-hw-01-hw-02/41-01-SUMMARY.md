---
phase: 41-cxp-mil-lite-10-0-grab-hw-01-hw-02
plan: "01"
subsystem: device-driver
tags: [mil, cxp, camera, build-config, enum]
dependency_graph:
  requires: []
  provides: [ECameraType.MIL enum 멤버, Matrox.MatroxImagingLibrary csproj 참조]
  affects: [WPF_Example/DatumMeasurement.csproj, WPF_Example/Device/Camera/VirtualCamera.cs]
tech_stack:
  added: [Matrox.MatroxImagingLibrary (MIL Lite 10.0 .NET binding)]
  patterns: [절대경로 HintPath (halcondotnet 선례), enum 멤버 추가 (E접두사 PascalCase)]
key_files:
  created: []
  modified:
    - WPF_Example/DatumMeasurement.csproj
    - WPF_Example/Device/Camera/VirtualCamera.cs
decisions:
  - "MIL DLL HintPath = 절대 경로 (halcondotnet 선례 일치, MIL 런타임 PC 설치)"
  - "Private=False: 출력 디렉토리 미복사 (MIL 런타임 PC 설치 전제)"
  - "enum 멤버 추가 위치: HIK 다음 줄 (기존 Virtual/Basler/HIK 보존, 순서 의미 없음)"
metrics:
  duration_seconds: 135
  completed_date: "2026-06-02"
  tasks_completed: 2
  files_modified: 2
---

# Phase 41 Plan 01: MIL DLL 참조 + ECameraType.MIL Foundation Summary

**One-liner:** MIL Lite 10.0 .NET DLL HintPath를 csproj에 추가하고 ECameraType.MIL enum 멤버를 선언하여 후속 MilCamera 드라이버(Plan 02)의 컴파일 foundation 마련.

## Tasks Completed

| Task | Name | Commit | Files |
|------|------|--------|-------|
| 1 | ECameraType enum에 MIL 멤버 추가 | 2c665ae | VirtualCamera.cs |
| 2 | csproj에 Matrox MIL .NET DLL HintPath 참조 추가 | c794a4c | DatumMeasurement.csproj |

## Verification Results

- msbuild Debug/x64 Rebuild: **EXIT CODE 0** (0 errors)
- 신규 warning: 0 (기존 CS0618×5 + CS0162×1 + MSB3884×1 = 모두 기존 베이스라인)
- ECameraType enum: Virtual/Basler/HIK/MIL 4개 멤버 확인
- MIL DLL HintPath: `C:\Program Files\Matrox Imaging\MIL\MIL.NET\Matrox.MatroxImagingLibrary.dll` 확인
- MIL DLL 파일 존재: `Test-Path` True 확인

## Deviations from Plan

None - 계획대로 정확히 실행.

## Known Stubs

없음 — 이 plan은 빌드 메타데이터만 변경 (런타임 동작 없음).

## Threat Flags

없음 — 로컬 빌드 구성 변경만, 런타임/입력 경계 미접촉.

## Self-Check: PASSED

- `WPF_Example/DatumMeasurement.csproj` 존재: FOUND
- `WPF_Example/Device/Camera/VirtualCamera.cs` 존재: FOUND
- commit 2c665ae 존재: FOUND (feat(41-01): ECameraType enum에 MIL 멤버 추가)
- commit c794a4c 존재: FOUND (feat(41-01): csproj에 Matrox MIL .NET DLL HintPath 참조 추가)
