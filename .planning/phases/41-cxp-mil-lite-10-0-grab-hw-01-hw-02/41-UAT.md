---
phase: 41-cxp-mil-lite-10-0-grab-hw-01-hw-02
plan: "04"
status: partial
summary:
  total: 5
  passed: 1
  failed: 0
  pending: 4
updated: "2026-06-03"
---

> **⏸ 검증 보류 (2026-06-03):** 사용자 요청으로 Test 2~5 런타임 육안 검증은 나중에 진행. 1차 실행에서 `FileNotFoundException(Matrox.MatroxImagingLibrary)` 크래시 발견 → **핫픽스 CO-41-01 적용·커밋(a397039)**. 재검증 시 반드시 **실행 중인 앱 종료 후 재빌드**하여 핫픽스 반영된 새 exe 로 확인할 것(이전 빌드는 앱 점유로 bin exe 미교체됨).

# Phase 41 UAT: CXP MIL Lite 10.0 grab 통합 SIMUL 검증

**목적:** Plan 01~03 통합(MilCamera 드라이버 + DeviceHandler MIL factory + RegisterRequiredDevices 역할 분기)이 SIMUL_MODE에서 올바르게 동작함을 검증한다. 실 CXP 보드(RAP4G4C12) + 카메라(ViewWorks 128MP) 미도착이므로 SIMUL_MODE 경로를 1차 gate로 삼는다. 실 HW grab 검증은 보드 도착 후 별도 재측정으로 격리한다.

---

## Test 1: 통합 빌드 0 errors

**검증 유형:** 자동 (Task 1에서 실행됨)
**명령:** `msbuild WPF_Example\DatumMeasurement.csproj /p:Configuration=Debug /p:Platform=x64 /t:Rebuild /v:m`

**result:** PASS
**근거:**
- 빌드 종료 코드: 0 (errors 0)
- 출력 마지막 라인: `DatumMeasurement -> ...\bin\x64\Debug\DatumMeasurement.exe`
- warning 목록: MSB3884×1, CS0618×5, CS0162×1 — 모두 Phase 41 이전 베이스라인 일치, 신규 warning 0
- 자동 확인 일자: 2026-06-02

### 정적 검증 6항목 (자동 — Task 1 Select-String 결과)

| # | 검증 항목 | 패턴 | 결과 |
|---|-----------|------|------|
| SV-1 | VirtualCamera.cs ECameraType.MIL 존재 | `MIL,` (L22) | PASS |
| SV-2 | MilCamera.cs `class MilCamera : VirtualCamera` | L11 확인 | PASS |
| SV-3 | MilCamera.cs `MIL.MdigGrab` | L93 확인 | PASS |
| SV-4 | MilCamera.cs `GenImage1("byte"` | L112/L166 확인 | PASS |
| SV-5 | MilCamera.cs `#if SIMUL_MODE` 폴백 | L38/L87 확인 | PASS |
| SV-6 | DeviceHandler.cs `case ECameraType.MIL:` + `new MilCamera` | L221/L223 확인 | PASS |
| SV-7 | DeviceHandler.cs `AddVirtualCamera` 폴백 | L228 확인 (#if SIMUL_MODE 블록 내) | PASS |
| SV-8 | Custom/DeviceHandler.cs `RegisterRequiredDevices` CameraRole 분기 | L91/L93/L95 확인 | PASS |
| SV-9 | Custom/DeviceHandler.cs `WIDTH_CXP`/`HEIGHT_CXP` 상수 | L29/L30 확인 | PASS |
| SV-10 | Custom/SystemSetting.cs `CameraRole` + `CameraRoleValue` | L10/L20/L24/L25/L26 확인 | PASS |
| SV-11 | csproj `Matrox.MatroxImagingLibrary` Reference | L207/L208 확인 | PASS |
| SV-12 | csproj `MilCamera.cs` Compile Include | L221 확인 | PASS |

---

## Test 2: SIMUL_MODE 앱 기동 (IsInitializeFail 모달 없음)

**검증 유형:** human-verify (사용자 육안)
**확인 절차:**
1. `WPF_Example\bin\x64\Debug\DatumMeasurement.exe` 를 실행하거나 VS2022 Debug/x64 로 시작
2. SIMUL_MODE: MIL Open 실패 시 `AddVirtualCamera` 폴백이 동작해야 함
3. **확인 포인트:** 앱이 크래시 없이 메인 화면이 뜨고, IsInitializeFail 에러 모달이 뜨지 않는가?

**result:** pending (재검증 — 1차 FAIL → CO-41-01 핫픽스 적용)
**근거:**
- 1차 실행(핫픽스 전): `System.IO.FileNotFoundException: Could not load file or assembly 'Matrox.MatroxImagingLibrary, Version=10.10.614.1'` → `DeviceHandler.Initialize()` → `SystemHandler` 정적 초기화 크래시. 원인 = csproj `Private=False`(DLL bin 미복사) + SIMUL_MODE 에서도 `new MilCamera` JIT 로 Matrox 어셈블리 로드 시도.
- 핫픽스 CO-41-01(a397039): ① DeviceHandler MIL case 를 `#if SIMUL_MODE`→`AddVirtualCamera` 직접 폴백(SIMUL 은 Matrox 런타임 미로드) ② csproj `Private=True`(bin 복사 확인). 컴파일 0 errors 재확인.
- 재검증: 앱 종료 후 재빌드한 새 exe 로 기동 확인 필요 (보류).

---

## Test 3: SIMUL grab — TopBottom 역할 (CameraRoleValue=0)

**검증 유형:** human-verify (사용자 육안)
**확인 절차:**
1. `Setting.ini` 에서 `CameraRoleValue=0` (TopBottom) 확인 (기본값)
2. 앱 실행 → Top 시퀀스 및 Bottom 시퀀스가 SIMUL 이미지(Cal_Image/ 또는 SimulatedImagePath)를 grab
3. **확인 포인트:** MainView에 Top/Bottom 카메라 이미지가 표시되는가? 측정/검사 1 사이클이 Phase 40.1 baseline과 동일하게 동작하는가?

**result:** pending
**근거:** 사용자 육안 확인 대기

---

## Test 4: SIMUL grab — Side 역할 (CameraRoleValue=1)

**검증 유형:** human-verify (사용자 육안)
**확인 절차:**
1. `Setting.ini` 에서 `CameraRoleValue=1` (Side) 로 변경 후 재기동
2. **확인 포인트:** Side 시퀀스가 SIMUL grab/표시되는가? Top/Bottom 시퀀스는 미등록 상태로 조용히 처리되는가? (역할 분리 동작 확인)
3. 확인 후 `CameraRoleValue=0` 으로 복원 권장

**result:** pending
**근거:** 사용자 육안 확인 대기

---

## Test 5: 회귀 — Datum/FAI/결과 표시 Phase 40.1 baseline 일치

**검증 유형:** human-verify (사용자 육안)
**확인 절차:**
1. CameraRoleValue=0 (TopBottom, 기본) 상태에서 기존 레시피 로드
2. Datum 검출 / FAI 측정 / 결과 표시 경로가 Phase 40.1 baseline과 동일하게 동작하는가?
3. **확인 포인트:** MilCamera가 SIMUL_MODE에서 VirtualCamera 파일 grab으로 폴백하므로 grab 결과는 이전과 동일해야 함. 회귀 현상(측정값 변화/오류/크래시) 없는가?

**result:** pending
**근거:** 사용자 육안 확인 대기

---

## Carry-over 예약

실 HW grab 검증(MdigGrab, M_SYSTEM_DEFAULT, 실 해상도 MdigInquire):
- RAP4G4C12 보드 + ViewWorks 카메라 도착 후 별도 phase 또는 quick task로 재측정
- RESEARCH Open Q1 (시스템 디스크립터 문자열), Q3 (실 해상도 WIDTH_CXP/HEIGHT_CXP 확정)

---

## HW-01/HW-02 충족 상태

| 요구사항 | SIMUL 기준 | 상태 |
|----------|-----------|------|
| HW-01: MIL SDK 참조 + 빌드 성공 | csproj HintPath + msbuild 0 errors | SATISFIED (SV-11/SV-12 + Test 1 PASS) |
| HW-02: MilCamera GrabHalconImage → HImage (SIMUL 폴백) | #if SIMUL_MODE LastHalconImage 반환 | SIMUL CODE VERIFIED (SV-2~5 PASS); 런타임 PENDING |
| HW-02: 기존 시퀀스 회귀 없음 | SIMUL grab 경로 불변 | PENDING (Test 3/5) |
