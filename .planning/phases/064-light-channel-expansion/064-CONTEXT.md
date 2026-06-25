# Phase 64: 조명 채널 확장 + z_index 기반 내부 조명 제어 - Context

**Gathered:** 2026-06-25
**Status:** Ready for planning

<domain>
## Phase Boundary

LightHandler를 8채널×2 컨트롤러 구조로 확장하고, `$PREP:site,z_index@` 신규 TCP 커맨드를 추가하여 비전 내부에서 ShotConfig 기반 조명 자동 세팅 흐름을 구현한다. 신규 검사 로직, 시퀀스 구조 변경, Align 관련 코드 무변경.

</domain>

<decisions>
## Implementation Decisions

### 프로토콜 — $PREP 커맨드 (A방식, ACK 있음)

- **D-01:** `$PREP:site,z_index@` 신규 커맨드로 핸들러가 검사 트리거 전 z_index 선행 전달
- **D-02:** 비전은 `$PREP_ACK:site,z_index,OK@` (조명 세팅 완료) 또는 `$PREP_ACK:site,z_index,FAIL@` (Shot 없음 / 조명 오류) 응답
- **D-03:** `$LIGHT` 커맨드는 코드 무변경 — Phase 64에서 건드리지 않음 (회귀 위험 0)
- **D-04:** 미래 HW 트리거 전환 대비 구조 — $PREP 로직은 TCP→HW 전환 시 그대로 재사용 가능

### LightHandler 확장

- **D-05:** CHANNEL_LIMIT: 4 → 8 (base LightHandler.cs)
- **D-06:** Controller A (Index=0): Ring CH1~CH6 (조명1, 6개 물리 채널) + AlignCoax CH7 = 7채널
- **D-07:** Controller B (Index=1): Back + Bar×4 + Ring7 = 6채널
- **D-08:** Ring 6분할은 **물리 채널로 등록**하되, ShotConfig 제어는 **RING 통합 그룹** 하나로 묶어 동시 제어. 개별 채널 레벨 제어는 향후 phase.
- **D-09:** LightGroup 5종: RING (6CH 통합), BACK, BAR (4CH 통합), RING7, ALIGN_COAX

### ShotConfig → LightHandler 연결

- **D-10:** `ApplyShotLights(ShotConfig shot)` 메서드 신규 구현 (InspectionSequence 또는 별도 헬퍼)
  - `RingLight_Enabled/Brightness` → RING 그룹
  - `BackLight_Enabled/Brightness` → BACK 그룹
  - `CoaxLight_Enabled/Brightness` → ALIGN_COAX 그룹 (INI 키 이름 보존 — 기존 레시피 호환)
  - `SideLight_Enabled/Brightness` → BAR 그룹
- **D-11:** CoaxLight_* INI 키 이름 그대로 유지 → 기존 레시피 파일 재설정 불필요

### 시퀀스 흐름

- **D-12:** `$PREP` 수신 → z_index 추출 → 해당 ShotConfig 조회 → `ApplyShotLights()` 실행 → `$PREP_ACK` 응답
- **D-13:** `$TEST` 수신 시점에는 조명이 이미 세팅된 상태 — 기존 검사 흐름 무변경

### 배선 맵 (미확정)

- **D-14:** 광학부서 최종 배선 확인 대기 중. 현재 설계 문서 채널 인덱스 기준으로 구현하고, 확정 후 RegisterLightController() 채널 번호만 수정하는 구조로 작성.

### Claude's Discretion

- `$PREP` 파서를 PrepPacket 별도 클래스로 분리할지 VisionRequestPacket 내 메서드로 처리할지 — Phase 63의 기존 패턴 따름
- `ApplyShotLights()`를 InspectionSequence 내부에 둘지 LightHandler 확장 메서드로 둘지 — 코드 크기와 의존성 기준으로 결정
- SetLevel + SetOnOff 호출 순서 (On → Level vs Level만) — 기존 ApplyLight() 패턴 따름

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### 조명 설계 결정 문서
- `.planning/LIGHT-CHANNEL-DESIGN.md` — 하드웨어 확정 사항(JPF-1208, 채널 배분 7+6), $PREP 프로토콜 포맷, 코드 변경 포인트 상세

### 기존 TCP 구조 (Phase 63 패턴 참조)
- `WPF_Example/TcpServer/VisionRequestPacket.cs` — 기존 패킷 파서 구조 (PrepPacket 추가 시 동일 패턴)
- `WPF_Example/TcpServer/VisionResponsePacket.cs` — 기존 응답 빌더 구조
- `WPF_Example/Custom/SystemHandler.cs` — ProcessXxx() 분기 패턴

### 기존 조명 코드
- `WPF_Example/Device/LightController/LightHandler.cs` — CHANNEL_LIMIT, LightGroup, SetLevel/SetOnOff API
- `WPF_Example/Custom/Device/LightHandler.cs` — 현재 RegisterLightController() (3채널 1대)
- `WPF_Example/Custom/Sequence/Inspection/ShotConfig.cs` — RingLight_*/BackLight_*/CoaxLight_*/SideLight_* 속성 정의

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `LightHandler.SetLevel(groupName, level)` / `SetOnOff(groupName, onOff)` — 그룹 이름으로 조명 제어 API, ApplyShotLights에서 그대로 사용
- `LightHandler.ApplyLight(ICameraParam param)` — 기존 패턴: SetOnOff(true) → SetLevel() 순서 확인
- `JPFLightController(index).SetChannelNames(...)` — 현재 1대 등록 패턴, 2대로 확장
- Phase 63 PrepPacket 구현 없음 — VisionRequestPacket에 새 커맨드 타입 추가 시 기존 패턴(AsXxx() 메서드) 따름

### Established Patterns
- `ProcessXxx(packet)` 패턴: SystemHandler.MainRun() switch-case에서 커맨드 타입별 dispatch
- `UseProtocolV1` 플래그: Phase 48/49에서 확립된 v1.0 vs v2.6 분기 — $PREP도 동일 플래그 적용 고려
- 헝가리언 표기법 + if-else (삼항 연산자 금지) + 함수 30~40줄 (Phase 48/49 코딩 규칙)

### Integration Points
- `SystemHandler.MainRun()` switch-case → ProcessPrep() 추가
- `InspectionSequence` → ApplyShotLights() 추가 (ShotConfig 접근 가능)
- `ShotConfig.cs` → 기존 4종 조명 속성 그대로 사용 (신규 추가 없음)

</code_context>

<specifics>
## Specific Ideas

- **$PREP 타이밍 활용**: 로봇 이동 시간 중 $PREP 처리 → 조명 안정화 시간 숨김 → 트리거 시 즉시 캡처. 이 구조가 미래 HW 트리거 전환의 기반.
- **배선 확정 대기**: 광학부서 채널 맵 확인 후 Controller A/B 채널 인덱스 수정 예정. 코드 구조는 지금 짜고 숫자만 나중에 바꾸는 방식.

</specifics>

<deferred>
## Deferred Ideas

- Ring 6채널 개별 레벨 제어 (ShotConfig에 CH별 속성 추가) — 통합 그룹 동작 확인 후 별도 phase
- $LIGHT 커맨드 폐기 — UseProtocolV1 전환 완료 후 별도 phase에서 제거
- 하드웨어 트리거 전환 ($TEST TCP → HW 펄스) — $PREP 구조 확정 후 별도 phase

</deferred>

---

*Phase: 64-light-channel-expansion*
*Context gathered: 2026-06-25*
