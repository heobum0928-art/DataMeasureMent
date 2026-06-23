---
phase: 58-config-camera-a-2026-06-23
verified: 2026-06-23T19:10:00+09:00
status: human_needed
score: 4/4 must-haves verified (code level)
overrides_applied: 0
human_verification:
  - test: "INI round-trip: Setting.ini 저장 후 재로드 — 4개 [ETHERNET_VISION] 키 값 보존 확인"
    expected: "앱 실행 → PropertyGrid 에서 EthernetVisionMode=Tray, EthernetCameraIp='10.0.0.5', EthernetExposure=5000, EthernetPixelResolution=1.234 로 변경 → 앱 종료 → Setting.ini 열어 [ETHERNET_VISION] 섹션 확인 → 앱 재시작 → PropertyGrid 값 일치"
    why_human: "INI reflection Load/Save 루프의 실제 round-trip 은 앱 실행 없이 정적 코드 검사만으로 보장 불가. 특히 EthernetVisionModeValue int-backing 이 [ETHERNET_VISION] 그룹으로 실제 저장되는지 런타임 확인 필요."
  - test: "누락 키 기본값 복원: [ETHERNET_VISION] 섹션 없는 구 INI 로드 시 PixelResolution=8.652 보장"
    expected: "Setting.ini 에서 [ETHERNET_VISION] 섹션 전체 삭제 → 앱 시작 → SystemSetting.Handle.EthernetPixelResolution == 8.652 (PropertyGrid 에서 육안 확인 또는 로그)"
    why_human: "AfterLoad() → RestoreEthernetVisionDefault() 코드 경로는 존재하나, 실제 Load() 완료 시점에 partial AfterLoad() 가 올바르게 호출되는지는 런타임에만 확인 가능."
  - test: "이더넷 카메라 Grab/Live/Stop — SIMUL 모드 폴백 동작"
    expected: "EthernetVisionMode=Tray 로 설정 후 앱 실행 → 실 카메라 없으므로 Connect 실패(로그 '[ETHERNET] connect failed') → EthernetVisionHandler.Camera.Grab() 호출 시 D:\\align_test.bmp 로드 성공 → 이미지 반환"
    why_human: "Connect 실패 경로 + SIMUL 폴백 경로는 코드로 확인됐으나, 실제 Grab() 호출은 Phase 61 UI 전까지 앱 내 진입점이 없음. 테스트 코드 또는 임시 호출 추가 없이는 런타임 확인 불가."
  - test: "Grabber 검사 무영향 확인 — 이더넷 init 실패 후 Top/Bottom/Side 시퀀스 정상 동작"
    expected: "EthernetVisionMode=Tray, 실 이더넷 카메라 없음 → 앱 기동 → '[ETHERNET] connect failed' 로그 후 Grabber(Top/Bottom/Side) 시퀀스 정상 초기화 + SIMUL TCP 검사 명령 응답 정상"
    why_human: "try-catch 격리 코드는 검증됨. 실제 앱 기동 후 Grabber 경로가 영향받지 않는지는 End-to-End 앱 실행으로만 확인 가능."
---

# Phase 58: Config & Camera (A) 검증 보고서

**Phase Goal:** EthernetVisionConfig(INI [ETHERNET_VISION], None/Tray/Bottom 모드) + 독립 이더넷 카메라(Hikvision MvCamCtrl.Net) 연결/grab/live 를 추가하되, 실패해도 기존 Grabber 검사에 무영향이다.
**Verified:** 2026-06-23T19:10:00+09:00
**Status:** human_needed
**Re-verification:** No — 초기 검증

---

## Goal Achievement

### Observable Truths

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | EthernetVisionMode/IP/Exposure/PixelResolution 이 [ETHERNET_VISION] 그룹 INI 에 저장·로드되고, PixelResolution 누락 키 시 8.652 복원 보장 | CODE VERIFIED | `Custom/SystemSetting.cs` L66-82: [Category("ETHERNET_VISION")] 4개 프로퍼티; L34: const 8.652; L36-40: AfterLoad() → RestoreEthernetVisionDefault(); L56-63: bPixelResolutionMissing <= 0.0 → 복원 로직 |
| 2 | 이더넷 카메라 grab/live/stop 동작 — 미연결 시 D:\align_test.bmp 폴백 | CODE VERIFIED | `EthernetAlignCamera.cs` L73-87: Grab() IsOpen 확인 → GrabHalconImage(); null 이면 LoadFallbackImage(); L137-155: File.Exists + ReadImage + Rgb1ToGray 패턴 |
| 3 | 이더넷 카메라 초기화 실패해도 Grabber 검사 정상 | CODE VERIFIED | `SystemHandler.cs` L187-193: EthernetVisionHandler.Handle.Initialize() 가 Step 8 Localize 완료 후 독립 try-catch 블록으로 격리; `EthernetVisionHandler.cs` L29-53: Initialize() 전체 try-catch 래핑 |
| 4 | 기존 Sequence/Action/SystemHandler 무수정 (SystemHandler Initialize 에 try-catch 격리 init 한 줄 ADD 허용) | CODE VERIFIED | git diff 분석: HikCamera.cs/DeviceHandler.cs/VirtualCamera.cs 변경 줄 수 = 0; Sequence_*/Action_* 파일 변경 없음; SystemHandler.cs diff = 추가 8줄만(comment+try-catch), 삭제 0 |

**Score:** 4/4 truths code-verified

---

## Required Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `WPF_Example/Custom/EthernetVision/EEthernetVisionMode.cs` | EEthernetVisionMode enum (None=0, Tray=1, Bottom=2) | VERIFIED | Allman brace, namespace ReringProject.Setting, 3개 멤버 값 일치, commit a91f0f8 |
| `WPF_Example/Custom/SystemSetting.cs` | [ETHERNET_VISION] 4개 프로퍼티 + RestoreEthernetVisionDefault() | VERIFIED | int-backing enum 패턴; const ETHERNET_PIXEL_RESOLUTION_DEFAULT=8.652; AfterLoad() 단일 정의(L36), RestorePcRoleDefault()+RestoreEthernetVisionDefault() 양쪽 호출 |
| `WPF_Example/Custom/EthernetVision/EthernetAlignCamera.cs` | HikCamera composition 래퍼, Connect/Grab/Live/Stop/Close | VERIFIED | 157줄; private HikCamera _hikCamera; IsOpen 계산 프로퍼티; 전 메서드 try-catch; ALIGN_FALLBACK_IMAGE_PATH = D:\align_test.bmp; EnumerateDevice 호출 후 Open; commit 513e3b1 + 57ce769(using fix) |
| `WPF_Example/Custom/EthernetVision/EthernetVisionHandler.cs` | sealed singleton, mode-gated lazy connect | VERIFIED | K&R style; namespace ReringProject; static Handle; EEthernetVisionMode.None 게이트; Connect(EthernetCameraIp); IsInitialized 반영; 전체 try-catch; commit a0e9101 |
| `WPF_Example/SystemHandler.cs` (수정) | EthernetVisionHandler.Handle.Initialize() 한 줄 try-catch ADD | VERIFIED | L187-193: Phase 58 Phase 8 완료 후 삽입; 추가 8줄, 삭제 0줄; commit 57ce769 |
| `WPF_Example/DatumMeasurement.csproj` (수정) | 3개 Compile Include 등록 | VERIFIED | L239-241: EEthernetVisionMode.cs / EthernetAlignCamera.cs / EthernetVisionHandler.cs 모두 등록 |

---

## Key Link Verification

| From | To | Via | Status | Details |
|------|----|-----|--------|---------|
| Custom/SystemSetting.cs AfterLoad() | RestoreEthernetVisionDefault() | 메서드 호출 | WIRED | L39: RestoreEthernetVisionDefault() 호출 존재, 기존 RestorePcRoleDefault() 보존 |
| EthernetAlignCamera.Connect | HikCamera.EnumerateDevice + Open(ip) | composed _hikCamera | WIRED | L52: HikCamera.EnumerateDevice(ip); L58: _hikCamera.Open(ip) |
| EthernetAlignCamera.Grab | GrabHalconImage() / D:\align_test.bmp fallback | IsOpen 분기 | WIRED | L75-81: IsOpen true → GrabHalconImage(); null → LoadFallbackImage(); 미연결 → LoadFallbackImage() |
| SystemHandler.Initialize() | EthernetVisionHandler.Handle.Initialize() | try-catch 블록 | WIRED | L189: EthernetVisionHandler.Handle.Initialize() 정확히 1회, Step 8 완료 후 |
| EthernetVisionHandler.Initialize() | EthernetAlignCamera.Connect(SystemSetting.Handle.EthernetCameraIp) | mode-gate 후 연결 | WIRED | L30: bModeOff = EthernetVisionMode == None; L38-39: Camera.Connect(camIp) |
| EthernetVisionHandler.Initialize() | EEthernetVisionMode.None 게이트 | 조기 반환 | WIRED | L30-35: bModeOff → log + return (연결 시도 없음) |

---

## Data-Flow Trace (Level 4)

이 Phase는 렌더링 UI 컴포넌트를 생성하지 않습니다 (Phase 61에서 추가 예정). 데이터 흐름은 설정 로드 + 카메라 초기화까지이며 화면 표시 경로가 없으므로 Level 4 Data-Flow Trace는 해당 없음 (SKIPPED — no rendering artifact in this phase).

---

## Behavioral Spot-Checks

앱 실행 없이 확인 가능한 항목만 체크합니다.

| Behavior | Check | Result | Status |
|----------|-------|--------|--------|
| EEthernetVisionMode enum 값 | grep "None = 0\|Tray = 1\|Bottom = 2" | 3개 일치 | PASS |
| ETHERNET_PIXEL_RESOLUTION_DEFAULT = 8.652 | grep "8.652" Custom/SystemSetting.cs | L34, L82 2개 확인 | PASS |
| AfterLoad() 정의 단일 | grep AfterLoad Custom/SystemSetting.cs | L36 1개 정의 (L32는 주석) | PASS |
| SystemHandler wire 단일 | grep -c "EthernetVisionHandler.Handle.Initialize" | 1 | PASS |
| csproj 3개 Compile 등록 | L239-241 | EEthernetVisionMode/EthernetAlignCamera/EthernetVisionHandler 모두 | PASS |
| Grabber 파일 변경 없음 | git diff 0줄 for HikCamera/DeviceHandler/VirtualCamera/Sequence_/Action_ | 0줄 확인 | PASS |
| Build | 58-03-SUMMARY: msbuild Debug/x64 exit code 0 | 커밋 57ce769 포함 | PASS (SUMMARY 기록 신뢰) |

Step 7b 런타임 spot-check (실 실행 필요) — 아래 Human Verification 항목으로 이관.

---

## Requirements Coverage

| Requirement | 담당 Plan | Description | Code Status | Evidence |
|-------------|-----------|-------------|-------------|----------|
| AV-01 | 58-01 | EthernetVisionMode(None/Tray/Bottom)+IP/노출/픽셀분해능 INI [ETHERNET_VISION], 미존재 키 8.652 기본값 | CODE SATISFIED | EEthernetVisionMode enum + 4개 [Category("ETHERNET_VISION")] 프로퍼티 + RestoreEthernetVisionDefault() 존재 및 연결 확인 |
| AV-02 | 58-02, 58-03 | 이더넷 카메라 독립 클래스 connect/grab/live/stop, SIMUL → D:\align_test.bmp, 실패해도 Grabber 정상 | CODE SATISFIED | EthernetAlignCamera (composition, DeviceHandler 등록 없음) + EthernetVisionHandler (mode-gate, try-catch) + SystemHandler 격리 init 확인 |

REQUIREMENTS.md L98-101: AV-01/AV-02 모두 [x] 체크됨 (Phase 58 완료 마킹).

---

## Anti-Patterns Found

| File | Line | Pattern | Severity | Impact |
|------|------|---------|----------|--------|
| `EthernetAlignCamera.cs` | L17-18 | `DEFAULT_WIDTH = 5120`, `DEFAULT_HEIGHT = 5120` — 주석에 "플레이스홀더 (Open 후 실 해상도로 덮어씀)" | Info | Open() 호출 전 DeviceInfo 생성에만 사용, 실제 이미지 해상도는 카메라가 결정. 의도된 패턴, 블로커 아님. |

TODO/FIXME/placeholder 주석 없음. return null 패턴은 LoadFallbackImage() 실패 시에만 — 호출자 주석으로 명시됨. 스텁 패턴 없음.

---

## Human Verification Required

### 1. INI Round-trip 확인

**테스트:** PropertyGrid 에서 [ETHERNET_VISION] 값 변경 → 앱 종료 → Setting.ini 직접 확인 → 앱 재시작 후 값 일치 확인

**예상 결과:** Setting.ini 에 `[ETHERNET_VISION]` 섹션이 생성되고, EthernetVisionModeValue / EthernetCameraIp / EthernetExposure / EthernetPixelResolution 키가 변경값으로 저장됨. 재시작 후 PropertyGrid 동일 값.

**왜 Human:** INI reflection Load/Save 루프가 `[Category("ETHERNET_VISION")]` 를 그룹명으로 올바르게 처리하는지는 실제 앱 실행에서만 확인 가능. int-backing 프로퍼티 `EthernetVisionModeValue` 가 INI 에 `EthernetVisionModeValue=0` 키로 저장되어 로드 시 `EthernetVisionMode == EEthernetVisionMode.None` 이 올바른지 런타임 검증 필요.

### 2. 누락 키 기본값 복원 (8.652)

**테스트:** Setting.ini 에서 `[ETHERNET_VISION]` 섹션 전체 또는 `EthernetPixelResolution` 키 삭제 → 앱 시작 → SystemSetting.Handle.EthernetPixelResolution 값 확인 (PropertyGrid 또는 로그)

**예상 결과:** EthernetPixelResolution = 8.652 (RestoreEthernetVisionDefault() 가 0 → 8.652 복원)

**왜 Human:** partial AfterLoad() 구현부가 Load() 완료 후 실제로 호출되는지 런타임에만 확인 가능. 프레임워크 SystemSetting.cs 의 Load() 에서 AfterLoad() 호출 코드를 추가로 검토할 수 있으나, 최종 확인은 앱 실행.

### 3. SIMUL 모드 Grab 폴백 (D:\align_test.bmp)

**테스트:** EthernetVisionMode=Tray 설정 + 실 이더넷 카메라 없음 → 앱 기동 → 로그에서 "[ETHERNET] connect failed" 확인 → Phase 61 이전에는 직접 Grab() 호출 방법 없음 → 선택: (a) 임시 테스트 코드 삽입, (b) Phase 61 UI 탭 완성 후 검증

**예상 결과:** Grab() 호출 시 D:\align_test.bmp 가 존재하면 HImage 반환, 없으면 null + "[ETHERNET] fallback image missing" 로그

**왜 Human:** Phase 61 (TabControl UI) 완성 전까지 EthernetAlignCamera.Grab() 에 접근하는 UI 진입점이 없음. 코드 경로 자체는 완전 구현됨.

### 4. Grabber 무영향 End-to-End 확인

**테스트:** EthernetVisionMode=Tray, 실 이더넷 카메라 없음 → 앱 기동 → "[ETHERNET] connect failed" 또는 "mode=None, skip connect" 로그 후 → SIMUL TCP 검사 명령 전송 → Top/Bottom/Side Grabber 시퀀스 정상 응답

**예상 결과:** 이더넷 init 실패가 Grabber 초기화/검사 흐름에 전혀 영향 없음. IsInitializeFail 플래그 변경 없음.

**왜 Human:** SystemHandler.Initialize() 에서 Grabber Steps 1-8 완료 후 Ethernet init 이 추가된 코드 구조는 확인됨. 실제 런타임에서 예외 발생 시 기존 try-catch 가 흡수하는지 앱 레벨 확인 필요.

---

## Gaps Summary

코드 레벨 갭 없음. 4개 ROADMAP Success Criteria 전부 코드 내에 구현 완료.

Human verification 4건은 런타임/앱 실행이 필요한 항목으로 코드 결함이 아닌 "SIMUL_MODE + Phase 61 UI 미완성"에 의한 단계적 검증 지연입니다:
- SC-1 INI round-trip: 코드 구조 완전, 런타임 확인 필요
- SC-2 Grab/Live/Stop 폴백: 코드 구현 완전, Phase 61 UI 전까지 진입점 없음
- SC-3 Grabber 무영향: 격리 구조 코드 확인, E2E 앱 실행 필요
- SC-4 anti-goal: git diff 로 코드 레벨 확인 완료 (사실상 코드 검증됨)

v1.3 마일스톤 계획에 따라 실 하드웨어 및 Phase 61 UI 완성 후 UAT 수행 권장.

---

_Verified: 2026-06-23T19:10:00+09:00_
_Verifier: Claude (gsd-verifier)_
