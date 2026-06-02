# Phase 41: CXP 카메라 MIL Lite 10.0 grab 드라이버 통합 (HW-01/HW-02) - Context

**Gathered:** 2026-06-02
**Status:** Ready for planning

<domain>
## Phase Boundary

CoaXPress 카메라를 **MIL Lite 10.0**으로 grab 하여 기존 `VirtualCamera` 추상화에 통합한다. 검사 시퀀스/액션 코드는 기존대로 `DeviceHandler.GrabHalconImage(param)` → `HImage`를 받으며, 그 뒤의 실제 grab만 CXP/MIL 경로로 동작한다 (HIK/Basler와 동일 계약).

**In scope:** `MilCamera : VirtualCamera` 신규 드라이버, `ECameraType.MIL` 추가, `DeviceHandler` 인스턴스화 분기, `RegisterRequiredDevices` CXP 재구성, MIL 버퍼→HImage 변환, MIL 리소스 수명, SIMUL_MODE 폴백 유지.
**Out of scope:** 측정 알고리즘/시퀀스 로직 변경, 새 검사 기능, 하드웨어 트리거/연속 스트리밍(추후), TCP/결과 경로 변경.
</domain>

<decisions>
## Implementation Decisions

### 카메라 매핑 / 배포 토폴로지
- **D-01:** 전부 **CXP 카메라** — 기존 HIK(MvCamCtrl.Net) grab 경로를 CXP/MIL로 대체. (HIK 드라이버/enum 코드 잔존 vs 제거 범위는 plan 재량 — 회귀 위험 최소화 우선.)
- **D-02:** 물리 토폴로지 = **PC 2대 + CXP 카메라 2대**. PC1 ↔ 카메라①(**Top + Bottom 시퀀스 2개** 담당, top/bottom 겸용), PC2 ↔ 카메라②(**Side 시퀀스** 담당). 각 PC 별도 연결.
- **D-03:** **소프트웨어 인스턴스당 CXP 카메라 1대만 활성.** `RegisterRequiredDevices`를 "현재 HIK 3대(Top/Side/Bottom) 고정 등록"에서 **PC별 CXP 1대 + 역할(시퀀스) 설정** 구조로 재구성. 역할 선택 메커니즘(SystemSetting/INI 기반)은 plan 재량.

### 그래버 · 카메라 사양
- **D-04:** 그래버 = **Matrox RAP4G4C12** (PCIe **x8**, **4채널**).
- **D-05:** 카메라 = **ViewWorks 128MP, Mono(흑백)**. 프레임당 ~128MB급 대용량 → 버퍼/메모리/성능을 plan에서 반드시 고려(이 규모에서 zero-copy 재검토 여지). 픽셀 포맷 = Mono8 (`ECaptureImageType.Gray8`).
- **D-06:** **MIL DCF 미사용 기본** — 코드 기반 digitizer 설정(default). DCF 필요 여부는 추후 확인(미확정 시 코드 설정 유지).
- **D-07:** CXP 버전(CXP-6/12) **미확정** — plan/research 단계에서 RAP4G4C12 + ViewWorks 스펙으로 확정.

### 트리거 & grab 모드
- **D-08:** **소프트웨어 트리거 단발 grab** (`ETriggerSource.Software`, 검사 1회당 1프레임). 하드웨어 트리거/연속 스트리밍은 미사용(추후 확장 여지).

### MIL → HImage 변환
- **D-09:** MIL 버퍼로 grab → 이미지 **host 메모리 포인터 획득**(`MbufInquire(MIL_HOST_ADDRESS)` / GetHostAddress) → HALCON **`HImage`(HObject)로 버퍼 복사**(`GenImage1`/`GenImageConst` 포인터 기반, Mono8). 행 정렬/패딩(MIL pitch vs HALCON width) 주의. zero-copy 포인터 wrap은 128MP 성능 이슈 시 plan에서 검토.

### 리소스 수명 & 통합 구조
- **D-10:** MIL **Application/System/Digitizer 를 앱 시작 시 1회 할당, 종료 시 해제** — `VirtualCamera.Open/Close` 수명에 정렬. 구조 = `MilCamera : VirtualCamera`(Open/Close/GrabHalconImage/WaitForHalconTrigger/SetTriggerMode 오버라이드) + `ECameraType.MIL` 추가 + `DeviceHandler.InitializeDevices` switch `case MIL → new MilCamera`.

### SIMUL_MODE
- **D-11:** SIMUL_MODE 폴백 **유지** — HW 없을 때 기존 파일/배경 이미지 grab 경로 그대로(`VirtualCamera` SIMUL 분기). CXP 카메라도 SIMUL에서 파일 grab으로 동작.

### Claude's Discretion
- MIL→HImage 정확한 API 호출 시퀀스 및 zero-copy vs copy 최종 선택(128MP 성능 측정 기반).
- HIK 코드 잔존/제거 범위(드라이버 클래스·enum·등록), 회귀 최소화.
- PC별 역할(시퀀스 매핑) 설정의 구체 메커니즘.
- MIL 에러/타임아웃 처리 패턴(기존 VirtualCamera try/catch 관습 답습).
</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### 내부 코드 계약 (통합 지점 — code_context 참조)
- `WPF_Example/Device/Camera/VirtualCamera.cs` — 베이스 클래스 + `ECameraType` enum + SIMUL 분기. MilCamera 가 상속/오버라이드할 가상 메서드 정의.
- `WPF_Example/Device/DeviceHandler.cs` — `InitializeDevices()` `switch(CamType)` 인스턴스화, `DeviceInfo`, `SetRequiredDevice`.
- `WPF_Example/Custom/Device/DeviceHandler.cs` — `RegisterRequiredDevices()` (현재 Top/Side/Bottom = HIK).
- `WPF_Example/Device/Camera/Hik/HikCamera.cs` — 기존 실 카메라 드라이버 오버라이드 패턴(MilCamera 의 closest analog).

### 외부 벤더 문서 (저장소 밖 — research 단계에서 확보 필요)
- **MIL Lite 10.0** API 문서 (Matrox) — `MbufAlloc`/`MdigProcess`/`MbufInquire`/host pointer, digitizer(코드 설정) 사용법. (이 PC 설치본 SDK 문서/예제)
- **Matrox RAP4G4C12** 그래버 매뉴얼 — 채널/CXP 버전/digitizer 설정.
- **ViewWorks 128MP Mono** 카메라 데이터시트 — 해상도/픽셀포맷/CXP 링크 구성.

*내부 spec/ADR 문서는 없음 — 요구는 위 decisions 에 캡처됨.*
</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `VirtualCamera` (base): 가상 메서드 `Open`/`Close`/`GrabHalconImage`/`WaitForHalconTrigger`/`SetTriggerMode`/`StartStream`/`StopStream`/`LoadProperties`/`SaveProperties`. → `MilCamera : VirtualCamera`가 이 표면을 오버라이드.
- `HikCamera` / `BaslerCamera`: 실 카메라 드라이버 구현 선례 — MilCamera 가 답습할 구조(생성자 `(DisplayConfig, DeviceInfo)`, grab→HImage).
- `DeviceInfo(ECameraType, ECaptureImageType, ETriggerSource, id, width, height, reverseX, reverseY, rotateAngle)` — Mono8=`Gray8`, 128MP 해상도 지정.

### Established Patterns
- `DeviceHandler.InitializeDevices()` `switch(id.CamType)` → 카메라 타입별 인스턴스화(`new HikCamera(Config, id)` 등). MIL case 추가.
- `ECameraType { Virtual, Basler, HIK }` → `MIL` 추가.
- `RegisterRequiredDevices()`(Custom)에서 `SetRequiredDevice(...)`로 필요 카메라 등록 — 현재 Top/Side/Bottom HIK. CXP 1대 기준으로 재구성.
- SIMUL_MODE: `VirtualCamera`의 `#if SIMUL_MODE` 파일/배경 이미지 grab 분기 (L260~). MilCamera 도 동일 폴백.
- HALCON 호출 try/catch 관습(알고리즘 계층) — MIL grab 도 동일 에러 격리.

### Integration Points
- 시퀀스/액션은 `DeviceHandler.GrabHalconImage(param)` 만 호출 → MilCamera 추가만으로 전 검사 경로 커버(시퀀스 코드 무변경).
- 카메라 SDK 런타임: MIL Lite 10.0(PC 설치) + `MvCamCtrl.Net`(HIK, 잔존 여부 plan).
</code_context>

<specifics>
## Specific Ideas

- 변환은 "MIL 버퍼에서 GetImagePointer로 HObject 버퍼 복사" (사용자 명시 방식).
- 배포: 동일 앱이 2 PC에 각각 단일 CXP 카메라로 동작 — 역할(Top+Bottom / Side)은 PC별.
- 128MP Mono = 대용량 단일 프레임. 메모리 1회 할당/재사용 + RawImageSaveService 부하 plan 검토 대상.
</specifics>

<deferred>
## Deferred Ideas

- 하드웨어 트리거 / 연속 스트리밍 grab 모드 (현재 소프트웨어 단발만).
- DCF 기반 digitizer 설정 (현재 코드 설정 default; 필요 확인 후).
- zero-copy(포인터 wrap) 무복사 grab — 128MP 성능 병목 확인 시 별도 최적화.
- HIK 드라이버 완전 제거/정리 (현 phase는 CXP 통합 우선, HIK 잔존 허용).

*그 외 논의는 phase 범위 내 유지.*
</deferred>

---

*Phase: 41-cxp-mil-lite-10-0-grab-hw-01-hw-02*
*Context gathered: 2026-06-02*
