# Phase 58: Config & Camera (A) - Context

**Gathered:** 2026-06-23
**Status:** Ready for planning

<domain>
## Phase Boundary

v1.3 Align 비전(이더넷 카메라) 마일스톤의 **기반(foundation) phase**. 두 가지만 추가한다:

1. **AV-01 — EthernetVisionConfig**: INI `[ETHERNET_VISION]` 섹션에 `EthernetVisionMode`(None/Tray/Bottom) + 카메라 IP + 노출 + 픽셀분해능(기본 8.652 μm/px) 저장·로드. 미존재 키는 기본값 보장.
2. **AV-02 — 독립 이더넷 카메라**: Hikvision MV-CH250-90GM(MvCamCtrl.Net) 을 독립 클래스로 connect/grab/live/stop. 미연결/SIMUL 이면 `D:\align_test.bmp` 폴백. 실패해도 Grabber(Top/Bottom/Side) 검사 무영향.

**이 phase 범위 밖 (59~62 별도):** Shape Matching 알고리즘(59), 캘리브레이션(60), TabControl UI(61), TCP 전송(62). Phase 58 은 config + 카메라 배선만, UI/알고리즘/TCP 없음.

**핵심 제약 (전 phase 공통):** 기존 Grabber 코드(Sequence/Action/SystemHandler 검사 경로) **절대 수정 금지 — 추가만**. 이더넷 카메라 실패해도 Grabber 정상. 헝가리언 표기법 · C# 7.2(switch expression/nullable refs/records 금지) · Halcon 호출 try-catch · 함수 30줄 이하 · 매직넘버 const · SIMUL_MODE.

</domain>

<decisions>
## Implementation Decisions

### 카메라 클래스 구조 (D-01)
- **D-01:** 신규 래퍼 클래스 `EthernetAlignCamera` 를 만들고, **내부에 기존 `HikCamera` 인스턴스를 보유(composition)** 한다.
  - 근거: AV-02 "독립 클래스로" 요건 충족 + 검증된 MvCamCtrl 연결/grab 코드 재사용(중복 0) + `HikCamera` 원본 무수정.
  - `HikCamera` 는 `생성자(DisplayConfig, DeviceInfo) + Open(IP)` 로 **DeviceHandler 등록 없이 독립 인스턴스화 가능**(스카우팅 확인). 따라서 DeviceHandler/RegisterRequiredDevices 경로를 타지 않아 Grabber 카메라 레지스트리와 분리.
  - 래퍼는 Connect/Grab/Live(stream)/Stop/Close 의 단순 facade. grab 은 `HikCamera.GrabHalconImage()` → `HImage` 위임.

### Config 저장 위치 (D-02)
- **D-02:** 기존 `Setting.ini` 에 **`[ETHERNET_VISION]` 섹션 추가**. `Custom/SystemSetting.cs` 에 프로퍼티 추가 + `AfterLoad()` partial 훅에서 미존재-키 기본값 복원.
  - 근거: 기존 INI 인프라/PropertyGrid 재사용. **Phase 48 PcRole 패턴**(`RestorePcRoleDefault`) 이 "미존재 키 → 0 로 로드되는 문제 방어" 를 이미 구현 → 8.652 기본값에 그대로 적용.
  - "추가만" 이므로 `Custom/SystemSetting.cs` 신규 프로퍼티/메서드 추가는 무수정 제약 위배 아님(framework `Setting/SystemSetting.cs` Load 루프는 reflection 기반이라 자동 처리).
  - 미존재-키 기본값 복원 필수 항목: `PixelResolution = 8.652`(0 로드 시 복원), `EthernetVisionMode = None`(enum 기본). IP/노출은 빈값/합리적 기본.

### 조율 구조 (D-03)
- **D-03:** 전용 **`EthernetVisionHandler` 싱글턴** 을 Phase 58 에서 신설. config + `EthernetAlignCamera` 를 소유한다.
  - 근거: 기반 phase 에서 59(align service)/60(calib)/62(TCP) 가 얹힐 깔끔한 골격 확보. "완전 독립 서브시스템" framing 과 일치.
  - `SystemHandler.Initialize()` 에 **try-catch 로 감싼 실패-격리 init 한 줄** 만 추가 (이더넷 init 실패가 Grabber init/검사에 전파되지 않도록). 이는 검사 경로 수정이 아닌 독립 init 추가.

### 연결 시점 / 생명주기 (D-04)
- **D-04:** **모드 게이트 + 지연 연결.** `EthernetVisionMode == None` 이면 연결 시도조차 안 함(기능 비활성). Tray/Bottom 모드일 때만 (핸들러 init 또는 탭 진입 시) 연결.
  - 연결/grab 실패 → try-catch 격리, Grabber 무영향, 카메라 상태=미연결.
  - 실 카메라 없음(SIMUL 또는 connect 실패) → `D:\align_test.bmp` 폴백 로드. 기존 `VirtualCamera.LoadBackgroundImage` + `BackgroundImagePath` 패턴 재사용(SIMUL 시 해상도 자동 적응).

### Claude's Discretion
- `EEthernetVisionMode { None, Tray, Bottom }` enum 명/위치 (프로젝트 `E` 접두 컨벤션 따름).
- `[ETHERNET_VISION]` INI 키 정확한 이름 (예: `EthernetVisionMode`, `CameraIp`, `Exposure`, `PixelResolution`).
- 노출/IP 기본값(스펙 미지정 → 합리적 placeholder). Live(stream) 표시 메커니즘 세부(HikCamera StartStream/StopStream 위임).
- `EthernetVisionHandler` 의 정확한 파일 위치(`Custom/` 하위 권장).

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### v1.3 요구사항 / 로드맵
- `.planning/ROADMAP.md` — "v1.3 Phases" 섹션 → Phase 58 Goal/Success Criteria + 전 phase 공통 제약.
- `.planning/REQUIREMENTS.md` §"Config & Camera (Phase 58 / A)" (line 97~99) — AV-01/AV-02 정의 + line 116 제약 블록.
- `.planning/PROJECT.md` §"Current Milestone: v1.3" (line 119~132) — 마일스톤 목표 + "코드 작성 전 phase별 설계 동의" 규칙.

### 재사용할 기존 코드 패턴 (현재 코드베이스)
- `WPF_Example/Custom/SystemSetting.cs` — `AfterLoad()` partial 훅 + `RestorePcRoleDefault()`(Phase 48). **D-02 미존재-키 기본값 복원의 직접 템플릿.**
- `WPF_Example/Setting/SystemSetting.cs` §`Load()` (line 197~275) — reflection 기반 [Category]→INI group 매핑. 신규 프로퍼티 자동 처리 원리.
- `WPF_Example/Device/Camera/Hik/HikCamera.cs` — `Open(CCameraInfo,int)`(line 306~417, MvCamCtrl 연결), `GrabHalconImage()`(493~519), `OnGrabResult`(callback→HImage), `EnumerateDevice`/`Open(IP)`. **D-01 내부 재사용 대상.**
- `WPF_Example/Device/Camera/VirtualCamera.cs` §`LoadBackgroundImage`(line 219~244) + `BackgroundImagePath` — **D-04 SIMUL 폴백 패턴.**
- `WPF_Example/Device/DeviceHandler.cs` (line 60 `SimulatedImagePath`) — SIMUL_MODE 폴백 const 패턴 참조(`D:\1.bmp`→`D:\align_test.bmp`).

### 구조 참조 (외부 백업 프로젝트)
- `D:\Backup\파이널비전\WPF_Example_260604` — HikCamera 클래스 + TabControl 패턴 참조용. **주의: 이 레퍼런스에는 Tray/Bottom Align 서브시스템 자체는 없음**(단일 카메라 + Shot 탭). Phase 58 은 레퍼런스의 카메라/INI 패턴을 "확장"하되 align 배선은 신규. (TabControl 은 Phase 61.)

> 외부 형식 스펙(ADR 등) 없음 — 요구사항은 ROADMAP/REQUIREMENTS + 위 코드 패턴으로 충분.

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `HikCamera`(Device/Camera/Hik/HikCamera.cs): `생성자 + Open(IP)` 로 DeviceHandler 등록 없이 독립 인스턴스화 가능 → `EthernetAlignCamera` 내부에 그대로 보유. `GrabHalconImage()` 가 `HImage` 반환.
- `Custom/SystemSetting.cs` `AfterLoad()` + `RestoreXxxDefault()`: 미존재 INI 키 기본값 복원의 검증된 패턴(Phase 48). 8.652 에 그대로 적용.
- `VirtualCamera.LoadBackgroundImage` + `BackgroundImagePath`: SIMUL/폴백 이미지 로드(`D:\align_test.bmp`), SIMUL 시 해상도 자동 적응.

### Established Patterns
- INI 직렬화: `Setting/SystemSetting.cs Load()` 가 `[Category("X")]` → INI group, 프로퍼티명 → key 로 reflection 자동 매핑. 신규 프로퍼티는 [Category("ETHERNET_VISION")] 부여 시 자동 로드/저장.
- 싱글턴 핸들러: `SystemHandler.Handle` / `DeviceHandler.Handle` 패턴 → `EthernetVisionHandler.Handle` 미러.
- enum `E` 접두 컨벤션: `ESequence`, `ECaptureImageType` → `EEthernetVisionMode`.

### Integration Points
- `SystemHandler.Initialize()`: try-catch 로 감싼 `EthernetVisionHandler.Handle.Initialize()` **한 줄 추가**(실패-격리). 이것이 기존 검사 경로에 닿는 유일한 지점 — 검사 로직 변경 0.
- `Custom/SystemSetting.cs`: `[ETHERNET_VISION]` 프로퍼티 + `AfterLoad()` 기본값 복원 추가.
- 신규 파일: `EthernetAlignCamera`, `EthernetVisionHandler`, `EEthernetVisionMode`(위치는 planner 결정, `Custom/` 하위 권장).

</code_context>

<specifics>
## Specific Ideas

- "신규 설계 말고 기존 패턴 확장"(사용자 지침): Phase 58 은 의도적으로 신규 코드 최소화 — HikCamera/SystemSetting INI/SIMUL 폴백 전부 재사용, 신규는 래퍼+핸들러+config 프로퍼티+enum 뿐.
- 픽셀분해능 8.652 μm/px 는 align 비전 **독립 도메인** 값. 기존 측정의 `ShotConfig.PixelResolution`(Phase 42 단일소스)과 무관 — 별도 `[ETHERNET_VISION]` PixelResolution.
- 진행 방식: 사용자가 **chain 모드** 선택(2026-06-23) — discuss(설계 동의)→plan→execute 자동, UAT 직전 정지. 58~62 매 phase 반복.

</specifics>

<deferred>
## Deferred Ideas

- Shape Matching 티칭/매칭(.shm), Tray X/Y · Bottom X/Y/Theta Offset 산출 → **Phase 59 (AV-03/04)**.
- 피커 센터 캘(36스텝 편심원) + 각도 캘 → **Phase 60 (AV-05/06)**.
- MainWindow TabControl([검사]/[Tray]/[Bottom]) + 모드별 Visibility + 툴바/티칭/결과 패널 → **Phase 61 (AV-07/08)**.
- `$RESULT site=TRAY/BOTTOM` TCP 전송 → **Phase 62 (AV-09)**.

</deferred>

---

*Phase: 58-config-camera-a-2026-06-23*
*Context gathered: 2026-06-23*
