# Phase 65: Bottom 6-슬롯 면별 Align (3D/2D × 면) - Context

**Gathered:** 2026-06-26
**Status:** Ready for planning

> **스코프 개정 2026-06-26:** 4슬롯 → **6슬롯**. 명명 확정(3D/2D 그룹). ROADMAP 제목의 "4-지그 Side1~4" 는 **6슬롯**으로 갱신됨(아래 D-01).

<domain>
## Phase Boundary

Bottom 비전이 안착지그 **6종**에 자재를 ideal 안착시키기 위한 **면별 align**. 각 지그마다 독립 모델+레퍼런스(6세트) 티칭 = Bottom 비전 **6번 티칭**. 비전 관점에서는 기존 단일 → **6슬롯 확장**이 본질. 범위:
1. AlignShapeMatchService Bottom 에 **6슬롯** 모델/레퍼런스 + slot(면) 파라미터.
2. BottomVisionView UI: 6슬롯 선택 + 면별 이미지 로드 + 슬롯별 티칭/Run/HasTemplate.
3. SystemHandler.ProcessAlignTest 면별 Run 라우팅 (현재 stub → 실제 Matcher.Run). TCP AlignFace 0~3 → **0~5(6값)** 확장.
4. 회귀 0: Tray(단일, 무변경), MainView 검사, 기존 Bottom 단일 경로.

**범위 밖(고정):** Tray align(Top면 단일, 무변경), 검사 시퀀스/측정, Phase 64 조명·z_index(별도 완료).
</domain>

<decisions>
## Implementation Decisions

### 슬롯 명명/의미 (개정 — 6슬롯)
- **D-01:** Bottom align 슬롯 = **6개**, 2그룹 구조:
  - **3D 그룹 (2)**: `3D_Top`, `3D_Bottom`
  - **2D 그룹 (4)**: `2D_TOP`, `2D_BOTTOM`, `2D_SIDE_1`, `2D_SIDE_2`
  - ROADMAP 의 'Side1=Top정/Side2=Top90/Bottom정/Bottom90' 4자세 framing 및 'TOP/BOTTOM/SIDE_1/SIDE_2' 4값은 **모두 폐기** → 위 6슬롯이 정식.
- **D-02:** 파일 슬롯명 = 의미명 `Bottom_{slot}_1/2.shm` + `Bottom_{slot}.json` (예: `Bottom_3D_Top_1.shm`, `Bottom_2D_SIDE_1_1.shm`). 6세트.
- **D-03:** TCP `AlignFace` index = 0~3 → **0~5(6값)** 확장. **제안 매핑(기존 0~3 호환 보존)**: 0=2D_TOP, 1=2D_BOTTOM, 2=2D_SIDE_1, 3=2D_SIDE_2, 4=3D_Top, 5=3D_Bottom. 최종 index 순서/encoding = **v3.0 프로토콜 스펙 준수**(planner 확인).

### 면별 이미지 입력
- **D-04:** **티칭(ideal 캡처) = 면별 이미지 파일 로드** (Phase 61.1 오프라인 로더 재사용). 슬롯마다 별도 이미지(6장).
- **D-05:** **런타임 Run = 라이브 이더넷 Align 카메라 grab**(실HW). SIMUL_MODE = 파일 로드 폴백. 현재 ProcessAlignTest 에 grab 경로 없음 → 신규 배선.

### $ALIGN_TEST Run 결과 계약
- **D-06:** $ALIGN_TEST(AlignFace) 처리 = (grab) → 해당 슬롯 모델로 `Matcher.Run` → **보정 pose(x/y/θ) + pass/fail** 반환. PLC 가 이 보정값으로 자재를 ideal 안착.
- **D-07:** 반환 그릇 = `AlignResultPacket`(VisionResponsePacket.cs:883-892) 의 `Items`(pose) + `IsPass` + `AlignFace` echo. 현 stub(IsPass=true echo만)을 실측 결과로 교체.
- **D-08:** $ALIGN_TEST/$ALIGN_RESULT 와이어 포맷은 **v3.0 프로토콜 엑셀 스펙(ALIGN_RESULT/AlignFace 모드)을 준수** — 임의 발명 금지. AlignFace 0~5 확장도 이 스펙과 정합.

### 기존 Bottom 단일 모델 하위호환
- **D-09:** 기존 `Bottom_1/2.shm` + `Bottom.json` **단일 경로 폴백 유지** — slot 미지정/-1 시 기존 Bottom 동작 그대로(회귀 0). 6슬롯은 신규 공존. 서비스 시그니처는 slot 기본값 또는 오버로드로 보호.
- **D-10:** Tray 경로(EEthernetVisionMode.Tray) 무변경 보장. Tray_1/2.shm + Tray.json 유지.

### Claude's Discretion
- enum 처리: `EEthernetVisionMode` 확장 vs 별도 slot enum(6값: 3D_Top…2D_SIDE_2) vs int slotIndex — planner 판단(권장: 6값 의미 enum).
- BottomVisionView 6슬롯 전환 위젯(ComboBox vs 6 ToggleButton, 3D/2D 그룹 시각 구분), 슬롯별 ROI 저장 구조(현재 _roi1/_roi2 단일쌍 → 슬롯별).
- AlignResultPacket.Items 의 pose 직렬화 세부 — D-08 스펙 범위 내.
</decisions>

<specifics>
## Specific Ideas
- 6지그 = **3D 검사용 2종(Top/Bottom)** + **2D 검사용 4종(Top/Bottom/Side1/Side2)**. 자재를 각 지그에 ideal 안착시키기 위한 align.
- 지그별 자세/형상 차이가 커서 단일 Shape 모델 angle_extent 로 못 덮음 → **6개 독립 모델 필수**.
- 레퍼런스 = 각 지그의 **ideal 안착 상태 1장 캡처** → 그 슬롯에 티칭.
- PLC 가 지그종류(AlignFace 0~5)를 $ALIGN_TEST 로 통보 → 비전이 해당 슬롯 모델로 정합 → 보정 pose 반환.
- **비전 변경 본질 = 기존 슬롯 메커니즘을 4→6으로 확장.** 새 알고리즘 없음.
</specifics>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Phase 정의
- `.planning/ROADMAP.md` Phase 65 항목 — Goal/Success Criteria (6슬롯으로 갱신됨).
- `.planning/ROADMAP.md` Phase 64 항목 — TCP AlignFace 도입 맥락(이미 0~3 파싱·echo 완료, 0~5 확장 대상).

### 프로토콜 스펙 (D-08 — 반드시 확인)
- v3.0 제어 프로토콜 엑셀 스펙 (파일명 ≈ `…protocol v3_3.xlsx`, 메모리 project_protocol_type_field 참조) — $ALIGN_TEST/$ALIGN_RESULT 필드, AlignFace 모드, pose 반환 계약. planner 가 경로 확인 후 AlignFace 0~5 + ALIGN_RESULT 정합.

### 코드 (회귀 가드 대상)
- 기존 Align 레시피: `D:\Data\Recipe\FAI_1\ETHERNET_ALIGN\` (Bottom_1/2.shm, Bottom.json, Tray_*) — 하위호환 검증 기준.
</canonical_refs>

<code_context>
## Existing Code Insights (사전 Explore 매핑)

### 변경 핵심 지점 (4→6 무관, 슬롯 차원 추가가 본질)
- `AlignShapeMatchService.cs:67-116` BuildShmPath/GetShmPath/BuildJsonPath (mode Tray/Bottom 2분기) — slot 차원 + `Bottom_{slot}` 명명.
- `AlignShapeMatchService.cs:198` HasTemplate, `:219` TryTeach, `:332` Run — slot 파라미터(기본값 -1 폴백=D-09).
- `EEthernetVisionMode.cs:3-9` (None/Tray/Bottom) — 6값 slot enum 또는 slotIndex.
- `BottomVisionView.xaml.cs:26` VIEW_MODE 고정 / `:208` TeachButton / `:259` RunButton / `:508` RefreshStatus — 6슬롯 UI(작업량 최대). `_roi1/_roi2` 단일쌍 → 슬롯별.
- `SystemHandler.cs:227-248` ProcessAlignTest (현 stub) — grab + Matcher.Run(slot) + pose 반환 배선.
- TCP AlignFace 범위: `VisionRequestPacket.cs:406-431`(파싱 0~3) + `AlignResultPacket.AlignFace`(echo) — **0~5 확장** 필요.

### 이미 완료 (재사용)
- TCP AlignFace 0~3 파싱/echo 골격 (0~5 확장만).
- AlignResultPacket(Items/IsPass/AlignFace) — pose 반환 그릇.
- 오프라인 이미지 로더: Phase 61.1 (티칭 이미지 파일 로드 = D-04).

### 무변경(회귀 가드)
- `TrayVisionView.xaml.cs:25` VIEW_MODE=Tray (단일, D-10). EthernetVisionHandler stateless 단일 인스턴스(slot은 파라미터 분기).
</code_context>

<deferred>
## Deferred Ideas
없음 (신규 scope creep 없음 — 4→6은 같은 capability 의 슬롯 수 조정). 별개 트랙: A-01 signed UAT, 트리 StartAt 크래시 UAT.
</deferred>
