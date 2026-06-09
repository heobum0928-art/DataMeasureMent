---
phase: 41-cxp-mil-lite-10-0-grab-hw-01-hw-02
plan: "04"
status: signed_off
summary:
  total: 6
  passed: 6
  failed: 0
  pending: 0
updated: "2026-06-09"
---

> **✅ SIGN-OFF (2026-06-09):** 실 CXP 보드(Matrox RapixoCXP) + 카메라(VIEWORKS VP-152MX2-M16I0) 도착 → 프로그램에서 **단발 grab + 라이브 둘 다 동작 확인(Test 6 PASS)**. 보드 도착 후로 보류돼 있던 "실 HW grab 검증" carry-over 종결, HW-02 런타임 PENDING → VERIFIED. SIMUL Test 2~5 는 CO-41-01 + CO-41-02 핫픽스 사용자 동작확인(2026-06-04) 근거로 PASS 정리. 실 HW 역할별(CameraRole) 다중 카메라 부분 등록 경로는 현재 "1대 공유"만 검증됨 → CO-41-03 carry-over.
>
> **⏸ 검증 보류 (2026-06-03):** 사용자 요청으로 Test 2~5 런타임 육안 검증은 나중에 진행. 1차 실행에서 `FileNotFoundException(Matrox.MatroxImagingLibrary)` 크래시 발견 → **핫픽스 CO-41-01 적용·커밋(a397039)**. 재검증 시 반드시 **실행 중인 앱 종료 후 재빌드**하여 핫픽스 반영된 새 exe 로 확인할 것(이전 빌드는 앱 점유로 bin exe 미교체됨).
>
> **🔧 CO-41-02 핫픽스 (2026-06-04, 사용자 동작확인 완료):** 재검증 중 SIMUL_MODE 에서 샷 실행 시 "Sequence is already running" 오진단 발견. 원인 = Phase 41 `RegisterRequiredDevices` 가 역할(CameraRole=TopBottom)별로 `CAM_TOP/CAM_BOTTOM` 만 등록 → 항상 생성되는 Side `InspectionSequence.OnCreate` 가 `CAM_SIDE` 미등록으로 `Context.State=Error` → `SequenceHandler.StateAll` 비-Idle → `IsIdle=false` 가 모든 샷을 "이미 실행 중"으로 차단. (운영 2-PC 도 동일 잠재 버그.) 수정 = ① `Custom/Device/DeviceHandler.cs` SIMUL 은 카메라 3대 전부 등록(실 HW 는 역할별), `RegisterCxpCamera` 헬퍼 도입 ② `Custom/Sequence/SequenceHandler.cs` `IsSequenceActive` 추가 — SIMUL 전체/실 HW 역할별 시퀀스·액션 등록(비활성 시퀀스 미생성). SIMUL 빌드에서 `IsSequenceActive`=항상 true → 시퀀스 동작 회귀 0, 실제 수정은 카메라 등록. msbuild Debug/x64 0 errors + 사용자 런타임 육안 동작확인. **실 HW 역할별 부분 등록 경로는 보드 도착 후 별도 검증(carry-over).**

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

**result:** PASS
**근거:**
- 1차 실행(핫픽스 전): `System.IO.FileNotFoundException: Could not load file or assembly 'Matrox.MatroxImagingLibrary, Version=10.10.614.1'` → `DeviceHandler.Initialize()` → `SystemHandler` 정적 초기화 크래시. 원인 = csproj `Private=False`(DLL bin 미복사) + SIMUL_MODE 에서도 `new MilCamera` JIT 로 Matrox 어셈블리 로드 시도.
- 핫픽스 CO-41-01(a397039): ① DeviceHandler MIL case 를 `#if SIMUL_MODE`→`AddVirtualCamera` 직접 폴백(SIMUL 은 Matrox 런타임 미로드) ② csproj `Private=True`(bin 복사 확인). 컴파일 0 errors 재확인.
- 재검증: 앱 종료 후 재빌드한 새 exe 로 기동 → 크래시 없음 + IsInitializeFail 모달 없음. CO-41-02(2026-06-04) 사용자 동작확인 시 SIMUL 정상 기동 확인. 실 HW 기동(2026-06-09, Debug|x64)도 MIL 카메라 활성 상태로 크래시 없이 메인 화면 표시.

---

## Test 3: SIMUL grab — TopBottom 역할 (CameraRoleValue=0)

**검증 유형:** human-verify (사용자 육안)
**확인 절차:**
1. `Setting.ini` 에서 `CameraRoleValue=0` (TopBottom) 확인 (기본값)
2. 앱 실행 → Top 시퀀스 및 Bottom 시퀀스가 SIMUL 이미지(Cal_Image/ 또는 SimulatedImagePath)를 grab
3. **확인 포인트:** MainView에 Top/Bottom 카메라 이미지가 표시되는가? 측정/검사 1 사이클이 Phase 40.1 baseline과 동일하게 동작하는가?

**result:** PASS
**근거:** CO-41-02(2026-06-04) 핫픽스 재검증 시 사용자 동작확인 — SIMUL TopBottom 시퀀스 grab/표시 정상. CO-41-02 가 SIMUL 카메라 3대 전부 등록하도록 수정하여 "Sequence is already running" 오진단 해소, 샷 실행 정상.

---

## Test 4: SIMUL grab — Side 역할 (CameraRoleValue=1)

**검증 유형:** human-verify (사용자 육안)
**확인 절차:**
1. `Setting.ini` 에서 `CameraRoleValue=1` (Side) 로 변경 후 재기동
2. **확인 포인트:** Side 시퀀스가 SIMUL grab/표시되는가? Top/Bottom 시퀀스는 미등록 상태로 조용히 처리되는가? (역할 분리 동작 확인)
3. 확인 후 `CameraRoleValue=0` 으로 복원 권장

**result:** PASS (재정의 — CO-41-02 로 설계 변경)
**근거:** CO-41-02(2026-06-04) 결정으로 **SIMUL 은 역할과 무관하게 카메라 3대 전부 등록**(`IsSequenceActive`=항상 true)하도록 변경됨 → SIMUL 에서 역할별 부분 등록을 검증하는 본 테스트의 원 의도는 superseded. 실제 역할별(CameraRole) 부분 등록 분리는 **실 HW 경로에서만** 동작하며, 현재 "CXP 1대 공유"만 검증됨 → 다중 카메라 역할 분리는 **CO-41-03 carry-over**. SIMUL 빌드 회귀 0(사용자 동작확인).

---

## Test 5: 회귀 — Datum/FAI/결과 표시 Phase 40.1 baseline 일치

**검증 유형:** human-verify (사용자 육안)
**확인 절차:**
1. CameraRoleValue=0 (TopBottom, 기본) 상태에서 기존 레시피 로드
2. Datum 검출 / FAI 측정 / 결과 표시 경로가 Phase 40.1 baseline과 동일하게 동작하는가?
3. **확인 포인트:** MilCamera가 SIMUL_MODE에서 VirtualCamera 파일 grab으로 폴백하므로 grab 결과는 이전과 동일해야 함. 회귀 현상(측정값 변화/오류/크래시) 없는가?

**result:** PASS
**근거:** CO-41-02(2026-06-04) 재검증 시 사용자 동작확인 — SIMUL 빌드에서 `IsSequenceActive`=항상 true 로 시퀀스 동작 회귀 0, Datum/FAI/결과 표시 Phase 40.1 baseline 일치. 실제 수정은 카메라 등록 경로뿐이며 grab 폴백(LastHalconImage) 경로 불변.

---

## Test 6: 실 HW grab + 라이브 (RapixoCXP + VP-152MX2) — 보드 도착 후 검증

**검증 유형:** human-verify (사용자 육안, 2026-06-09)
**환경:**
- 보드: **Matrox RapixoCXP** (2-connection CXP, 양 채널 link green)
- 카메라: **VIEWORKS VP-152MX2-M16I0** (≈152MP, ≈14192×10640)
- 빌드: **Debug | x64** (SIMUL_MODE off → 실 MIL 경로 활성)
- 실행: MIL **run-time 라이선스** → **Ctrl+F5(디버깅 없이 실행)** (F5 디버깅은 dev 라이선스 필요, "Debugging is not allowed with a run-time license")

**확인 절차 / 결과:**
1. Matrox Capture Works: 카메라 정상 인식 + GenICam Feature Browser 읽힘 + Live 영상 확인 → **PASS**
2. 프로그램에서 단발 grab (`MdigGrab` → `GenImage1` → HImage) → **PASS**
3. 프로그램에서 라이브(연속 grab 스레드 `LiveLoop`) → **PASS**

**result:** PASS
**근거:** 보드 도착 후로 보류돼 있던 "실 HW grab 검증" carry-over(아래 예약 항목) 종결. HW-02 런타임 VERIFIED.

**운영 노트(재현용):**
- `MilCamera.Open()` 은 DCF 없이 `M_DEFAULT` → 카메라 GenICam 설정에 의존(`.mbufi`/DCF 미로드). 카메라 **User Set 에 TriggerMode Off + Mono8 저장/UserSetDefault 지정** 필요(프로그램은 단순 동기 `MdigGrab`, 트리거 설정 코드 없음).
- CXP **단독 점유** — 실행 전 Capture Works / Intellicam 종료.
- Non-paged memory 512MB 는 프로그램(단일 재사용 Mono8 버퍼 ≈151MB)엔 충분(Capture Works 연속 grab 다중 버퍼는 더 요구).
- 실 HW 빌드 = **Debug|x64**(SIMUL_MODE off). run-time 라이선스는 **Ctrl+F5** 또는 exe 직접 실행.

---

## Carry-over 예약

실 HW grab 검증(MdigGrab, M_SYSTEM_DEFAULT, 실 해상도 MdigInquire):
- ~~RAP4G4C12 보드 + ViewWorks 카메라 도착 후 별도 phase 또는 quick task로 재측정~~ → **종결 (Test 6 PASS, 2026-06-09)** — 단, 보드는 RapixoCXP, 카메라는 VP-152MX2 로 확정.
- RESEARCH Open Q1 (시스템 디스크립터 문자열): `MsysAlloc(M_SYSTEM_DEFAULT)` + `MdigAlloc(M_DEV0, "M_DEFAULT")` 로 동작 확인됨.
- RESEARCH Open Q3 (실 해상도): 하드코딩 대신 `MdigInquire(M_SIZE_X/Y)` 런타임 조회로 처리 — VP-152MX2 ≈14192×10640.

**CO-41-03 (신규 carry-over):** 실 HW **역할별(CameraRole) 다중 카메라 부분 등록** 경로는 현재 "CXP 1대 공유"만 검증됨. 2-PC 역할 분리(TopBottom/Side) 다중 카메라 실측은 추가 카메라/구성 확보 후 검증.

---

## HW-01/HW-02 충족 상태

| 요구사항 | 기준 | 상태 |
|----------|-----------|------|
| HW-01: MIL SDK 참조 + 빌드 성공 | csproj HintPath + msbuild 0 errors | SATISFIED (SV-11/SV-12 + Test 1 PASS) |
| HW-02: MilCamera GrabHalconImage → HImage | 실 HW MdigGrab→GenImage1 / SIMUL 폴백 | **VERIFIED** (실 HW Test 6 PASS + SIMUL SV-2~5/Test 3·5 PASS) |
| HW-02: 기존 시퀀스 회귀 없음 | SIMUL grab 경로 불변 | SATISFIED (Test 3/5 PASS, CO-41-02 사용자 동작확인) |
