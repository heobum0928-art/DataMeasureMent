---
phase: 58-config-camera-a-2026-06-23
plan: "02"
subsystem: EthernetVision / Device
tags: [ethernet-camera, hikvision, composition, simul-fallback, AV-02]
dependency_graph:
  requires: [58-01]
  provides: [EthernetAlignCamera facade, HikCamera composition wrapper]
  affects: [DatumMeasurement.csproj]
tech_stack:
  added: []
  patterns: [HikCamera composition (no inheritance), SIMUL fallback via File.Exists+ReadImage, per-method try-catch failure isolation]
key_files:
  created:
    - WPF_Example/Custom/EthernetVision/EthernetAlignCamera.cs
  modified:
    - WPF_Example/DatumMeasurement.csproj
decisions:
  - "HikCamera composed as private field — no inheritance and no DeviceHandler registration per D-01"
  - "EnumerateDevice called with ip string (GigE path); 0-device guard logs and returns false before Open"
  - "Grab fallback path: real grab -> null check -> LoadFallbackImage (D:\\align_test.bmp); no #if SIMUL_MODE guard needed because Connect fails naturally without hardware"
  - "LoadFallbackImage uses File.Exists guard + ReadImage + Rgb1ToGray conversion (VirtualCamera.LoadBackgroundImage recipe)"
  - "Every public method (Connect/Grab/Live/Stop/Close) individually wrapped in try-catch; no method throws to caller"
metrics:
  duration_minutes: 5
  completed_date: "2026-06-23"
  tasks_completed: 2
  tasks_total: 2
  files_created: 1
  files_modified: 1
---

# Phase 58 Plan 02: EthernetAlignCamera Composition Wrapper Summary

**One-liner:** Hikvision GigE 정렬 카메라용 독립 facade — HikCamera composition + Connect/Grab/Live/Stop/Close + D:\align_test.bmp SIMUL 폴백, 전 메서드 try-catch 격리.

## Tasks Completed

| Task | Name | Commit | Files |
|------|------|--------|-------|
| 1 | Create EthernetAlignCamera wrapper | 513e3b1 | WPF_Example/Custom/EthernetVision/EthernetAlignCamera.cs |
| 2 | Register EthernetAlignCamera.cs in csproj | 05ee60e | WPF_Example/DatumMeasurement.csproj |

## What Was Built

`EthernetAlignCamera` 클래스 신규 생성:
- `private HikCamera _hikCamera` 으로 조합(composition) 보유 — DeviceHandler 등록 없음, 상속 없음 (D-01)
- `Connect(string ip)`: DisplayConfig + DeviceInfo 구성 → EnumerateDevice(ip) → Open(ip)
- `Grab()`: IsOpen이면 GrabHalconImage(); null이면 LoadFallbackImage() 폴백
- `Live()` / `Stop()` / `Close()`: StartStream / StopStream / Close 위임
- `LoadFallbackImage()`: File.Exists 가드 + ReadImage + Rgb1ToGray 변환 (VirtualCamera 패턴)
- `IsOpen`: `(_hikCamera != null) && _hikCamera.IsOpen` 계산 프로퍼티

## Deviations from Plan

### Plan vs Actual API Name Check

계획에서 언급된 HikCamera API 이름을 실제 소스와 대조:
- `EnumerateDevice(params string[])` — 실제 소스 line 155 일치
- `Open(params object[])` — 실제 소스 line 292 일치; `Open(ip)` 형태로 호출 가능 (`param[0] as string`)
- `GrabHalconImage()` — 실제 소스 line 488 일치 (내부적으로 소프트웨어 트리거)
- `StartStream()` — 실제 소스 line 612 일치 (`override bool`)
- `StopStream()` — 실제 소스 line 639 일치 (`override void`)
- `Close()` — 실제 소스 line 419 일치 (`override void`)
- `IsOpen { get; protected set; }` — VirtualCamera 에서 상속 (line 66 확인)

**편차 없음 — 모든 계획된 API 이름이 실제 HikCamera.cs 와 일치.**

### Auto-fixed Issues

없음 — 계획대로 정확히 실행.

## Known Stubs

없음 — 폴백 이미지 경로(D:\align_test.bmp)는 SIMUL/실패 시 의도된 동작이며, Phase 61 뷰어 연결 전까지 Live()는 반환값/로그로만 확인.

## Threat Surface Scan

계획의 `<threat_model>` 내 T-58-02 / T-58-03 mitigation 모두 구현:
- T-58-02 (DoS — 연결 불가 카메라): EnumerateDevice 0-device guard + HikCamera.Open 내부 try-catch + EthernetAlignCamera 전 메서드 try-catch → 실패 시 false/null 반환, 블록 없음
- T-58-03 (Tampering — align_test.bmp): File.Exists 가드 + ReadImage try-catch → 없거나 손상돼도 크래시 없음

신규 네트워크 리스너 없음. 기존 TCP 경로 무수정.

## Self-Check: PASSED

- [FOUND] WPF_Example/Custom/EthernetVision/EthernetAlignCamera.cs
- [FOUND] commit 513e3b1 (feat(58-02): add EthernetAlignCamera)
- [FOUND] commit 05ee60e (chore(58-02): register EthernetAlignCamera.cs in csproj)
- [FOUND] Compile entry in DatumMeasurement.csproj: Custom\EthernetVision\EthernetAlignCamera.cs
