---
phase: 41-cxp-mil-lite-10-0-grab-hw-01-hw-02
plan: "02"
subsystem: device-driver
tags: [mil, cxp, camera, virtualcamera, halcon, driver, simul-mode]
dependency_graph:
  requires: [41-01 — ECameraType.MIL enum + Matrox.MatroxImagingLibrary HintPath]
  provides: [MilCamera : VirtualCamera 드라이버 클래스, MIL 동기 grab → HImage 변환 경로]
  affects:
    - WPF_Example/Device/Camera/Mil/MilCamera.cs
    - WPF_Example/DatumMeasurement.csproj
tech_stack:
  added: []
  patterns:
    - MIL Lite 10.0 동기 단발 MdigGrab + MbufInquire(M_HOST_ADDRESS) → GenImage1("byte") 변환
    - M_LOCK/M_UNLOCK 로 MIL 버퍼 host 포인터 수명 보호
    - CreateImageFromPaddedBuffer: pitch > width 시 행 단위 복사 (128MP HW 준비)
    - SIMUL_MODE 분기: LastHalconImage 파일 grab 폴백 (D-11)
    - new IntPtr((long)MIL_INT) 명시적 변환 (Pitfall 3 — CS0030 방지)
key_files:
  created:
    - WPF_Example/Device/Camera/Mil/MilCamera.cs
  modified:
    - WPF_Example/DatumMeasurement.csproj
decisions:
  - "IsOpen, CaptureMode, TriggerSource 모두 protected set — 파생 클래스에서 직접 set"
  - "null-conditional ?.Dispose() 대신 명시적 if (x != null) 사용 (Phase 20 D-04 스타일)"
  - "unsafe 블록을 GrabHalconImage 본문이 아닌 CreateImageFromPaddedBuffer 헬퍼에만 격리"
  - "Open()에서 IsOpen = true + CaptureMode = Stop 명시적 설정 (HikCamera L410 참조)"
metrics:
  duration_seconds: 420
  completed_date: "2026-06-02"
  tasks_completed: 2
  files_modified: 2
---

# Phase 41 Plan 02: MilCamera 드라이버 클래스 작성 Summary

**One-liner:** VirtualCamera 계약을 상속한 MilCamera 드라이버를 신규 작성하여 MIL Lite 10.0 동기 MdigGrab → HALCON GenImage1("byte") 경로를 구현하고, SIMUL_MODE 폴백 포함 msbuild Debug/x64 0 errors 빌드 달성.

## Tasks Completed

| Task | Name | Commit | Files |
|------|------|--------|-------|
| 1 | MilCamera.cs 드라이버 클래스 신규 작성 | a777b36 | WPF_Example/Device/Camera/Mil/MilCamera.cs |
| 2 | csproj Compile Include 추가 + 빌드 검증 | 12a7a83 | WPF_Example/DatumMeasurement.csproj |

## Verification Results

- msbuild Debug/x64 Rebuild: **EXIT CODE 0** (0 errors)
- 신규 warning: 0 (기존 CS0618×5 + CS0162×1 + MSB3884×2 = 모두 기존 베이스라인)
- `class MilCamera : VirtualCamera` 선언: FOUND
- `using Matrox.MatroxImagingLibrary;`: FOUND
- `MIL.MdigGrab`, `MIL.MbufInquire`, `GenImage1("byte"`: FOUND
- `#if SIMUL_MODE` 분기 + `return LastHalconImage`: FOUND
- Close() Buffer→Digitizer→System→Application 역순 해제: CONFIRMED
- `new IntPtr((long)hostPtr)` 명시적 변환: FOUND
- C# 8.0+ 기능 (switch expression / record / nullable refs): 0건

## Plan Truths Verification

| Truth | Status |
|-------|--------|
| MilCamera가 VirtualCamera 상속, GrabHalconImage 오버라이드, HImage 반환 | VERIFIED |
| MilCamera.Open이 MIL Application/System/Digitizer/Buffer 1회 할당, Close 역순 해제 | VERIFIED |
| SIMUL_MODE 빌드에서 GrabHalconImage가 base 파일 grab 경로(LastHalconImage) 폴백 | VERIFIED |
| 솔루션이 MilCamera.cs 포함 상태로 msbuild Debug/x64 0 errors 빌드 | VERIFIED |

## Deviations from Plan

None - 계획대로 정확히 실행.

## Key Findings (read_first 확인 결과)

- `IsOpen { get; protected set; }` — 파생 클래스에서 set 가능 → PLAN.md 코드 그대로 적용
- `CaptureMode { get; protected set; }`, `TriggerSource { get; protected set; }` — 동일하게 set 가능
- `LastGrabHalconImage` — protected 필드, 파생에서 직접 접근 가능
- PATTERNS.md의 GrabHalconImage 코드에서 `?.Dispose()` null-conditional 사용 → CLAUDE.md Phase 20 D-04 스타일(명시적 if)로 교정
- PATTERNS.md의 unsafe 블록이 lock(Interlock) 바깥에 위치하는 구조 → PLAN.md 코드의 올바른 구조 채택 (unsafe는 헬퍼에만)

## Known Stubs

없음 — 실 HW 경로(#else 분기)는 MIL 드라이버 미설치 환경에서 SIMUL 폴백으로 안전하게 격리됨.
실 RAP4G4C12 보드 설치 후 MsysAlloc 시스템 디스크립터 확인 필요 (Plan 02 Open Q1, RESEARCH A1).

## Threat Flags

없음 — T-41-02/T-41-03/T-41-04 모두 PLAN.md 위협 등록과 일치하여 mitigate 구현 완료:
- T-41-02 (리소스 누수): Close() 역순 해제 + != M_NULL 체크 + null화 + ~MilCamera→Dispose→Close
- T-41-03 (dangling pointer): M_LOCK + M_NULL 체크 + pitch 분기 CopyImage + M_UNLOCK
- T-41-04 (HW 미설치 크래시): SIMUL_MODE M_SYSTEM_HOST + try/catch + M_NULL 체크 false 반환

## Self-Check: PASSED

- `WPF_Example/Device/Camera/Mil/MilCamera.cs` 존재: FOUND
- `WPF_Example/DatumMeasurement.csproj` 존재: FOUND
- commit a777b36 존재: FOUND (feat(41-02): MilCamera : VirtualCamera 드라이버 클래스 신규 작성)
- commit 12a7a83 존재: FOUND (feat(41-02): csproj에 MilCamera.cs Compile Include 추가 + msbuild 0 errors 검증)
