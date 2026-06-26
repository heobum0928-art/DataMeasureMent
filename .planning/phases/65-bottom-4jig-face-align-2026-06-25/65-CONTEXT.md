# Phase 65: Bottom 4-지그 면별 Align (Side1~4) - Context

**Gathered:** 2026-06-26
**Status:** Ready for planning

<domain>
## Phase Boundary

Bottom 비전이 안착지그 **4종**에 자재를 ideal 안착시키기 위한 **면별 align**. 각 지그마다 독립 모델+레퍼런스(4세트) 티칭. 범위:
1. AlignShapeMatchService Bottom 에 4슬롯 모델/레퍼런스 + site(face) 파라미터.
2. BottomVisionView UI: 4슬롯 선택 + 면별 이미지 로드 + 슬롯별 티칭/Run/HasTemplate.
3. SystemHandler.ProcessAlignTest 면별 Run 라우팅 (현재 stub → 실제 Matcher.Run).
4. 회귀 0: Tray(단일, 무변경), MainView 검사, 기존 Bottom 단일 경로.

**범위 밖(고정):** Tray align(Top면 단일, 무변경), 검사 시퀀스/측정, Phase 64 조명·z_index(별도 완료).
</domain>

<decisions>
## Implementation Decisions

### 4슬롯 명명/의미
- **D-01:** 4슬롯 = **TOP / BOTTOM / SIDE_1 / SIDE_2** (코드 `AlignFace` 0~3 현행 그대로 유지). ROADMAP 의 'Side1=Top정/Side2=Top90/Side3=Bottom정/Side4=Bottom90' 표현은 **폐기**(stale framing).
- **D-02:** 파일 슬롯명 = `Bottom_S1~S4_1/2.shm` + `Bottom_S1~S4.json` (SC#1 유지). AlignFace→슬롯 인덱스 매핑 = 인덱스 순서: AlignFace 0(TOP)=S1, 1(BOTTOM)=S2, 2(SIDE_1)=S3, 3(SIDE_2)=S4. UI 라벨 = TOP/BOTTOM/SIDE_1/SIDE_2.

### 면별 이미지 입력
- **D-03:** **티칭(ideal 캡처) = 면별 이미지 파일 로드** (Phase 61.1 오프라인 로더 재사용, SC#2 "면별 이미지 따로 로드"). 슬롯마다 별도 이미지.
- **D-04:** **런타임 Run = 라이브 이더넷 Align 카메라 grab**(실HW). SIMUL_MODE = 파일 로드 폴백. 현재 ProcessAlignTest 에 grab 경로 없음 → 신규 배선.

### $ALIGN_TEST Run 결과 계약
- **D-05:** $ALIGN_TEST(AlignFace) 처리 = (grab) → 해당 face 모델로 `Matcher.Run` → **보정 pose(x/y/θ) + pass/fail** 반환. PLC 가 이 보정값으로 자재를 ideal 안착시킨다.
- **D-06:** 반환 그릇 = `AlignResultPacket`(VisionResponsePacket.cs:883-892) 의 `Items`(pose) + `IsPass` + `AlignFace` echo. 현 stub(IsPass=true echo만)을 실측 결과로 교체.
- **D-07:** $ALIGN_TEST/$ALIGN_RESULT 와이어 포맷은 **v3.0 프로토콜 엑셀 스펙(ALIGN_RESULT/AlignFace 4모드)을 준수**한다 — 임의 발명 금지. planner 가 스펙 확인 후 필드 확정.

### 기존 Bottom 단일 모델 하위호환
- **D-08:** 기존 `Bottom_1/2.shm` + `Bottom.json` **단일 경로 폴백 유지** — faceIndex 미지정/-1 시 기존 Bottom 동작 그대로(회귀 0). 4슬롯은 신규 공존. 서비스 시그니처는 `faceIndex` 기본값 또는 오버로드로 보호.
- **D-09:** Tray 경로(EEthernetVisionMode.Tray) 무변경 보장. Tray_1/2.shm + Tray.json 유지.

### Claude's Discretion
- enum 처리: `EEthernetVisionMode` 에 Side 슬롯 추가 vs 별도 `EAlignFace`/int faceIndex 파라미터 — planner 판단.
- BottomVisionView 슬롯 전환 위젯(ComboBox vs 4 ToggleButton), 슬롯별 ROI 저장 구조(현재 _roi1/_roi2 단일쌍 → 슬롯별).
- AlignResultPacket.Items 의 pose 직렬화 세부(필드명/단위) — D-07 스펙 범위 내.
</decisions>

<specifics>
## Specific Ideas
- 4지그 = 자재 4측면 검사를 위한 안착 자세 4종. 지그별 자세 차이가 커서 단일 Shape 모델 angle_extent 로 못 덮음 → **4개 독립 모델 필수**(seed 근거).
- 레퍼런스 = 각 지그의 **ideal 안착 상태 1장 캡처** → 그 슬롯에 티칭.
- PLC 가 지그종류(AlignFace 0~3)를 $ALIGN_TEST 로 통보 → 비전이 해당 슬롯 모델로 정합.
</specifics>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Phase 정의
- `.planning/ROADMAP.md` Phase 65 항목 — Goal/Success Criteria(4) + 파일 슬롯 명명(Bottom_S1~S4).
- `.planning/ROADMAP.md` Phase 64 항목 — TCP AlignFace 도입 맥락(이미 0~3 파싱·echo 완료).

### 프로토콜 스펙 (D-07 — 반드시 확인)
- v3.0 제어 프로토콜 엑셀 스펙 (파일명 ≈ `…protocol v3_3.xlsx`, 메모리 project_protocol_type_field 참조) — $ALIGN_TEST/$ALIGN_RESULT 필드, AlignFace 4모드, Bottom AlignFace 필드 계약. planner 가 경로 확인 후 와이어 포맷 정합.

### 코드 (회귀 가드 대상)
- 기존 Align 레시피: `D:\Data\Recipe\FAI_1\ETHERNET_ALIGN\` (Bottom_1/2.shm, Bottom.json, Tray_*) — 하위호환 검증 기준.
</canonical_refs>

<code_context>
## Existing Code Insights (사전 Explore 매핑)

### 변경 핵심 지점
- `AlignShapeMatchService.cs:67-116` BuildShmPath/GetShmPath/BuildJsonPath (mode Tray/Bottom 2분기) — faceIndex 차원 + Bottom_S1~S4 명명.
- `AlignShapeMatchService.cs:198` HasTemplate, `:219` TryTeach, `:332` Run — faceIndex 파라미터(기본값 -1 폴백=D-08).
- `EEthernetVisionMode.cs:3-9` (None/Tray/Bottom) — Side 슬롯 enum 또는 faceIndex.
- `BottomVisionView.xaml.cs:26` VIEW_MODE 고정 / `:208` TeachButton / `:259` RunButton / `:508` RefreshStatus — 4슬롯 UI(작업량 최대). `_roi1/_roi2` 단일쌍 → 슬롯별.
- `SystemHandler.cs:227-248` ProcessAlignTest (현 stub: echo+IsPass=true, "면별 라우팅 향후 phase" 주석) — grab + Matcher.Run(face) + pose 반환 배선.

### 이미 완료 (재사용)
- TCP 수신: `VisionRequestPacket.cs:406-431` AlignFace 0~3 파싱 + `AlignTestPacket.AlignFace`.
- TCP 응답: `VisionResponsePacket.cs:883-892` AlignResultPacket(Items/IsPass/AlignFace echo) — pose 반환 그릇.
- 오프라인 이미지 로더: Phase 61.1 (티칭 이미지 파일 로드 = D-03).

### 무변경(회귀 가드)
- `TrayVisionView.xaml.cs:25` VIEW_MODE=Tray (단일, D-09). EthernetVisionHandler stateless 단일 인스턴스(face는 파라미터 분기).
</code_context>

<deferred>
## Deferred Ideas
없음 (신규 scope creep 없음). 별개 트랙(이번 phase 무관): A-01 signed UAT, 트리 StartAt 크래시 UAT.
</deferred>
