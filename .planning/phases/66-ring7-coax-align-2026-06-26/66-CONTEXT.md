# Phase 66: 조명 정합 — 검사 Ring7/Coax + Align 동축 - Context

**Gathered:** 2026-06-26
**Status:** Ready for planning

<domain>
## Phase Boundary

검사(inspection) shot 조명 세트를 하드웨어 사양(Ring/Backlight/Bar/Ring7, shot별 자유 조합)과 정합시키고, 얼라인 카메라 전용 동축(Coax)을 검사에서 숨겨 Align 창(Bottom/Tray)으로 이동한다.

**In scope:**
- 검사 ShotConfig에 Ring7 조명 추가(자유 조합), CoaxLight 검사 숨김
- ApplyShotLights에 Ring7→RING7 매핑 추가
- Bottom/Tray Align 창에 동축(ALIGN_COAX) ON/OFF + 밝기 컨트롤
- Bottom 6슬롯별 동축값 저장 + Run/Teach/Grab 직전 자동 적용

**Out of scope (다른 phase/불필요):**
- 컨트롤러 채널 재배선/재등록 (현 구성 유지 — D-08)
- 새 조명 그룹 추가 (LightHandler 5그룹 그대로)
- 검사 조명 알고리즘/측정 로직 변경

</domain>

<decisions>
## Implementation Decisions

### 검사 조명 정합 (Inspection ShotConfig)
- **D-01:** 검사 shot 조명 = Ring/Back/Bar/Ring7 4종 **자유 조합**(고정 매핑 없음). `ShotConfig`에 `Ring7Light_Enabled`(bool) + `Ring7Light_Brightness`(int) 추가, `[Category("Light|Ring7")]`. 기존 RingLight/BackLight/SideLight 무변경 → 회귀 0.
- **D-02:** `InspectionSequence.ApplyShotLights`에 Ring7Light → `LIGHT_RING7` 그룹 매핑 추가 (Enabled→SetOnOff, Brightness→SetLevel; 기존 4종과 동일 if-else 패턴, InspectionSequence.cs:336/361 인근).
- **D-03:** 검사 ShotConfig의 `CoaxLight_*`(Enabled/Brightness)는 `[Browsable(false)]`로 **숨김** — INI 키 보존(하위호환, Phase 64 D-11 일관), 검사 PropertyGrid(Shot>Light 탭) 미노출. 필드/그룹 매핑 코드 자체는 유지(런타임은 Align 경로가 ALIGN_COAX 사용).

### Align 동축 조명 (Bottom/Tray 창)
- **D-04:** 동축(`LIGHT_ALIGN_COAX`)은 얼라인 카메라 전용·**단일 채널**. Bottom/Tray Align 창 좌측 패널(Phase 65 슬롯 선택 위젯과 같은 영역)에 동축 **ON/OFF + 밝기(0~255)** 컨트롤 1개 추가. 그룹 드롭다운 불필요(채널 1개).
- **D-05:** 저장 단위 = Bottom은 6슬롯별 동축값(Enabled/Level)을 **기존 슬롯 레퍼런스 JSON `Bottom_{slot}.json`에 필드 추가**로 저장(슬롯 하나로 모델+레퍼런스+조명 묶음). Tray는 Tray align 설정에 단일 동축값. (Phase 65 AlignShapeMatchService JSON 경로 재사용)
- **D-06:** 런타임 소유권 = **비전 PC 자동**. `$ALIGN_TEST` Run(`RunBottomAlign`) 시 PC가 해당 슬롯 저장 동축값을 **grab 직전 자동 ON** (검사 ApplyShotLights 패턴 일관). PLC는 동축 미관여. 별도 PC·1채널이라 충돌 없음.
- **D-07:** 자동 적용 시점 = **Teach/Run/Grab 직전 모두** 저장 동축값 자동 ON + 작업자 **슬라이더 수동 override** 허용(조정값 슬롯 JSON에 저장). 티칭 조명 = 런타임 조명 일치 보장(매칭 스코어 안정).

### 하드웨어/채널 (확정 — 변경 없음)
- **D-08:** 컨트롤러 채널 **재배선 없음**. 현 LightHandler(Ctrl A: Ring 6CH + ALIGN_COAX, Ctrl B: BACK + BAR×4 + RING7 = 6+6+동축1)가 AOI POC v1.5 도면(JPF-1208-8ch×2, 6+6 검사 + 동축 한쪽·PC별)과 일치. 동축은 PC별 1채널.

### Claude's Discretion
- Ring7 PropertyGrid Category 명/표시 순서 — 기존 조명 4종과 동일 패턴 따름.
- 동축 컨트롤 UI 위젯 형태(체크박스+슬라이더/숫자입력) — 기존 Align 창 스타일 일치.
- 슬롯 JSON 동축 필드 직렬화 키명 + 키 부재 시 기본값(off/0) 하위호환 처리.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### 조명 아키텍처 (Phase 64 LIGHT-01)
- `.planning/ROADMAP.md` §"Phase 64: 조명 채널 확장" — LightHandler 8ch×2 + LightGroup 5종(RING/BACK/BAR/RING7/ALIGN_COAX) + ShotConfig 4필드→그룹 매핑 + D-11 INI 키 보존 원칙
- `WPF_Example/Custom/Device/LightHandler.cs` — 그룹 등록(RING7 :69, ALIGN_COAX :72), 채널 구성(Ctrl A/B :43-53)
- `WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs` §ApplyShotLights(:321-380) — 4종 조명→그룹 매핑(Ring7 추가 지점, Coax 매핑 :361-368)
- `WPF_Example/Custom/Sequence/Inspection/ShotConfig.cs` :60-75 — 조명 4필드(Ring7 추가 / Coax 숨김 대상)

### Align 슬롯 구조 (Phase 65)
- `.planning/phases/65-bottom-4jig-face-align-2026-06-25/65-CONTEXT.md` — Bottom 6슬롯 정의 + 레퍼런스 JSON 구조
- `.planning/phases/65-bottom-4jig-face-align-2026-06-25/65-01-SUMMARY.md` — AlignShapeMatchService 슬롯 경로 헬퍼 + `Bottom_{slot}.json`(동축값 추가 위치)
- `WPF_Example/Custom/EthernetVision/AlignShapeMatchService.cs` — 슬롯 모델/레퍼런스 로드·저장 경로
- `WPF_Example/Custom/UI/BottomVisionView.xaml(.cs)` / `TrayVisionView.xaml(.cs)` — 동축 컨트롤 추가 대상(좌측 패널)
- `WPF_Example/Custom/SystemHandler.cs` §RunBottomAlign — Run 시 동축 자동 적용 지점

### 런타임 메모리 (repo 외 — 참고)
- `~/.claude/projects/C--Info-Project-DataMeasurement/memory/project_aoi_poc_lighting_config.md` — AOI POC HW 구성 + 조명 정합 결함 요약

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `LightHandler.Handle.SetOnOff(group,bool)` / `SetLevel(group,0~255)` / `GetOnOff` / `GetLevel` — 동축 제어 그대로 재사용(검사 ApplyShotLights에서 검증됨).
- `InspectionSequence.ApplyShotLights` — Ring7 매핑 추가 시 기존 if-else 블록 복제.
- Phase 65 BottomVisionView 슬롯 선택 위젯/패널 — 동축 컨트롤 같은 영역에 배치.
- AlignShapeMatchService 슬롯 JSON 로드/저장 경로 — 동축 필드 추가에 재사용.

### Established Patterns
- ShotConfig 조명 필드 = `[Category("Light|XXX")]` + ParamBase 자동 INI 직렬화 → PropertyGrid 자동 노출. Ring7도 동일 패턴.
- `[Browsable(false)]` = INI 키 유지하며 PropertyGrid 숨김(기존 사용 사례 있음).
- 그룹별 SetOnOff→SetLevel 순서(Enabled true 시), Enabled false 시 SetOnOff(false)만 (InspectionSequence.cs:337-369).

### Integration Points
- 검사: ShotConfig(필드) + InspectionSequence.ApplyShotLights(매핑) — 2파일.
- Align: BottomVisionView/TrayVisionView(UI+자동적용) + AlignShapeMatchService(JSON 동축 필드) + SystemHandler.RunBottomAlign(Run 시 적용) — 동축 경로.

</code_context>

<specifics>
## Specific Ideas

- 외부 근거 문서: AOI POC 2D/ZSTOPPER **v1.5**(TIS Corporation, 2026-06-11, 사용자 업로드 PDF). TRAY ALIGN 조명 = Coaxial `JL-C-76_50-CLW-B`. 컨트롤러 `JPF-1208-8ch × 4 (TOP 2/BOTTOM 2)`.
- 사용자 확정(2026-06-26): "검사는 shot마다 조명 막 조합(Backlight+Ring 등 정해진 것 없음)", "동축은 얼라인 카메라 전용", "컨트롤러 8ch 2개 6+6 + 얼라인 조명 한쪽에 PC별".
- 목표 결과: 검사 Shot>Light 탭 = Ring/Back/Bar/Ring7 (이미지1 HW와 1:1), 동축은 Align 창에서만.

</specifics>

<deferred>
## Deferred Ideas

- 동축 채널 2개(같은 PC에 Bottom+Tray 동거 시 ALIGN_COAX_2) — 현재 별도 PC·1채널 전제라 불필요. 향후 단일 PC 통합 시 재검토.
- 각 PC 동축 물리 배선 채널 매핑 확정(JPF 컨트롤러 어느 채널) — 실장 시 광학/제어팀 확인(채널 인덱스만 조정, 본 phase 코드 구조엔 영향 없음).

</deferred>

---

*Phase: 66-ring7-coax-align-2026-06-26*
*Context gathered: 2026-06-26*
