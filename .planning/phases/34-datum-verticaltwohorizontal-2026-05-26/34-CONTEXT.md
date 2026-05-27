# Phase 34: Datum VerticalTwoHorizontal 듀얼 티칭 이미지 변형 — Context

**Gathered:** 2026-05-27
**Status:** Ready for planning

<domain>
## Phase Boundary

VerticalTwoHorizontal 알고리즘의 **2-image 변형 1종**을 신규 EDatumAlgorithm 값으로 추가한다.
- 신규 알고리즘은 가로축 ROI 2개(Horizontal_A + Horizontal_B)와 세로축 ROI 1개(Vertical)를 **서로 다른 티칭 이미지**에서 검출하여 Datum 좌표를 결합한다.
- 기존 3종(TwoLineIntersect / CircleTwoHorizontal / VerticalTwoHorizontal — 1-image) 회귀 0.
- 신규 algorithm은 Side fixture 케이스(가로축 ROI 와 세로축 ROI 가 동일 이미지에서 모두 잡히지 않음)를 풀기 위한 것이며, Phase 33 carry-over CO-33-05 / Phase 35 Test 4 Side carry-over 의 prerequisite.

**범위 외 (다른 phase):**
- 다른 algorithm(CircleTwoHorizontal 등)의 dual-image 확장 — 향후 별도 phase
- LineToLineAngle 신규 algorithm — Phase 27 영역
- 검사 워크플로우 end-to-end OK/NG 분기 — Phase 24 영역

</domain>

<decisions>
## Implementation Decisions

### ROI ↔ 이미지 매핑
- **D-34-01**: 기존 `TeachingImagePath` = **가로축** 이미지(Horizontal_A + Horizontal_B ROI 검출 대상). 신규 `TeachingImagePath_Vertical` = **세로축** 이미지(Vertical ROI 검출 대상). ROADMAP §Success 3 채택, project_phase35_progress 메모의 역방향 기록은 정정 대상.
- **D-34-02**: ROI ID 기반 image 선택 분기 — `Datum.HorizontalA`/`Datum.HorizontalB` → image1(TeachingImagePath), `Datum.Vertical` → image2(TeachingImagePath_Vertical). 분기는 `Action_FAIMeasurement.GrabOrLoadDatumImage` 단일 지점에서 algorithm==DualImage 일 때만 실행.

### Enum / 필드 명명
- **D-34-03**: 신규 enum 값 = `EDatumAlgorithm.VerticalTwoHorizontalDualImage`. 기존 3 값 순서 뒤에 추가 (string 직렬화 — Phase 12 D-09 ParamBase 제약 그대로).
- **D-34-04**: 신규 DatumConfig 필드 = `public string TeachingImagePath_Vertical { get; set; } = "";`. INI 키도 동일 이름. ICustomTypeDescriptor 분기로 algorithm==VerticalTwoHorizontalDualImage 일 때만 노출 (Phase 17 패턴 그대로).
- **D-34-05**: AlgorithmType 콤보박스 — `AlgorithmTypeOptions` 리스트에 `EDatumAlgorithm.VerticalTwoHorizontalDualImage.ToString()` 추가 (DatumConfig.cs:80-82 패턴 그대로).

### 티칭 UI 흐름
- **D-34-06**: 이미지 전환 시점 — **자동**. Horizontal_B step 완료 직후 EDatumTeachStep.Vertical 진입 시 HalconViewerControl 이 `TeachingImagePath_Vertical` 자동 로드. 별도 Load 버튼 신설 안 함.
- **D-34-07**: EDatumTeachStep 머신 — 기존 enum 그대로 유지 (Vertical/HorizontalA/HorizontalB 모두 존재). 단, **신규 algorithm 일 때 step 순서**: HorizontalA → HorizontalB → (자동 image 전환) → Vertical → Done. 기존 1-image VerticalTwoHorizontal 의 순서(Vertical → HorizontalA → HorizontalB)와 다름에 주의 — 코드는 algorithm 분기로 step 시퀀스 결정.
- **D-34-08**: 티칭 중 `TeachingImagePath_Vertical` 가 비어 있는 상태로 Vertical step 진입 시 — 캔버스 클리어 + 안내 텍스트 "세로축 이미지를 Load 해주세요" 표시. 저장은 막지 않음 (사용자 워크플로우 자유도).

### 빈 경로 가드 & 런타임 검증
- **D-34-09**: 검사 시점 가드 — `algorithm == VerticalTwoHorizontalDualImage && TeachingImagePath_Vertical == ""` 면 즉시 `EContextResult.Error` 반환 + 로그 + UI 알림. 검사 실패 처리는 기존 ROI 미검출 패턴과 동일.
- **D-34-10**: 검사 실패 메시지 — 기존 MainView.xaml.cs:1029 의 "Vertical ROI 가 없습니다…" 패턴 따라, "세로축 티칭 이미지 경로가 비어 있습니다. Datum 노드에서 세로축 이미지를 Load 해주세요." 형태.

### INI 호환 & 정규화
- **D-34-11**: INI 정규화 — **일률 '' 정규화**. DatumConfig.cs:563 의 TeachingImagePath null 가드 패턴 그대로 적용: `if (TeachingImagePath_Vertical == null) TeachingImagePath_Vertical = "";`. algorithm 종류 무관 적용 (DualImage 가 아니어도 필드 자체는 존재). Phase 22 IMG-01 패턴 계승.
- **D-34-12**: 기존 INI(TeachingImagePath_Vertical 키 미존재) 로드 → DatumConfig.Load 후 정규화 hook 에서 "" 로 초기화 → 기존 1-image algorithm 회귀 0. EnsurePerRoiDefaults 흐름에 포함.

### D-06 가드 충돌 처리
- **D-34-13**: Phase 33 D-06 가드 (`Action_FAIMeasurement.cs` 변경 0 라인) — **Phase 34 한정 해제**. 허용 변경 = `GrabOrLoadDatumImage` 안에 algorithm 분기 추가 (ROI ID 보고 image1 vs image2 선택) **단일 지점만**. 그 외 동일 파일 변경 금지. CONTEXT/PLAN 양쪽에 명시 + verification 시 diff 라인 카운트 검증.
- **D-34-14**: `InspectionSequence.cs`, `VisionResponsePacket.cs` D-06 가드는 **그대로 유지** (Phase 33/35 와 동일). 변경 0 라인.

### Claude's Discretion
- HalconViewerControl 이미지 자동 전환 구현 위치 (`HalconViewerControl.LoadImage` 호출 직접 vs MainView 의 step change 핸들러에서 호출) — planner/researcher 가 기존 패턴 따라 결정.
- ICustomTypeDescriptor 의 IsHiddenForAlgorithm 분기 코드 위치 (DatumConfig.cs:667 함수 안 case 추가) — Phase 17 패턴 자명.
- AlgorithmType 콤보박스 라벨에 한글 표기 추가 여부 — 현재 코드가 ToString() 그대로 노출하므로 그대로 따름.
- 두 이미지가 같은 카메라 해상도/orientation 여야 하는지 가정 검증 — researcher 단계에서 명시.

### UAT 범위
- **D-34-15**: UAT = (1) msbuild Debug/x64 PASS + 신규 warning 0, (2) 기존 1-image 3종 회귀 0 (Top recipe 로드 + 임의 검사 1회), (3) 신규 algorithm SIMUL UAT (Side fixture 이미지 2장으로 Datum 검출 PASS), (4) INI 라운드트립 (TeachingImagePath_Vertical 키 저장/로드 byte-identical). Phase 35 Test 4 Side carry-over 흡수.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Roadmap & 계약
- `.planning/ROADMAP.md` §Phase 34 — Goal / Background / Success Criteria 1~7 / Plans 추정
- `.planning/REQUIREMENTS.md` — (해당 phase 별도 REQ ID 없음, ROADMAP §SC 가 계약)

### 기존 코드 — 변경/확장 대상
- `WPF_Example/Custom/Sequence/Inspection/EDatumAlgorithm.cs` — enum 신규 값 추가 위치
- `WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs` — TeachingImagePath_Vertical 필드 추가 + ICustomTypeDescriptor 분기(IsHiddenForAlgorithm at :667) + AlgorithmTypeOptions(:80) + INI 정규화 hook(:563)
- `WPF_Example/Halcon/Algorithms/DatumFindingService.cs` — 신규 algorithm 분기 추가 (ROI 별 HImage 입력 받아 좌표 결합)
- `WPF_Example/UI/ContentItem/MainView.xaml.cs` — EDatumTeachStep 시퀀스 분기(:59) + 자동 이미지 전환 hook + 검사 가드 메시지(:1029 패턴 참조)
- `WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs` — GrabOrLoadDatumImage 안 ROI ID → image1/image2 선택 분기 (D-34-13 한정 변경)

### 기존 코드 — 변경 0 라인 (D-06 가드)
- `WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs`
- `WPF_Example/TcpServer/VisionResponsePacket.cs`

### 이전 Phase 컨텍스트 (패턴 계승)
- `.planning/phases/12-*/12-CONTEXT.md` — D-09 EDatumAlgorithm enum 도입 결정
- `.planning/phases/17-*/17-CONTEXT.md` — ICustomTypeDescriptor 알고리즘별 필드 가시성 분기 패턴
- `.planning/phases/22-*/22-CONTEXT.md` — IMG-01 TeachingImagePath 단일 필드 + INI 미존재 시 "" 정규화 패턴
- `.planning/phases/33-side-bottom-inspectionsequence-migration/33-CONTEXT.md` — D-06 가드 정의 (Action_FAIMeasurement.cs 변경 0 라인)
- `.planning/phases/35-side-bottom-uat-and-phase33-completion/35-CONTEXT.md` — Phase 35 Test 4 carry-over (Side 실측 SIMUL UAT)

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- **EDatumTeachStep 머신** (MainView.xaml.cs:59) — Vertical/HorizontalA/HorizontalB 모두 이미 존재. 신규 algorithm 은 step **순서**만 분기하면 됨 — enum 값 추가 불요.
- **ICustomTypeDescriptor 분기 helper** (DatumConfig.cs:667 IsHiddenForAlgorithm) — Phase 17 패턴 그대로 case 1개 추가.
- **INI 정규화 hook** (DatumConfig.cs:563 null→"") — Phase 22 IMG-01 패턴 그대로 1줄 추가.
- **AlgorithmTypeOptions 리스트** (DatumConfig.cs:80) — string 항목 1개 추가.
- **MainView.xaml.cs:1029 검사 가드 메시지** — "Vertical ROI 가 없습니다…" 패턴 그대로 신규 가드 메시지 추가.

### Established Patterns
- ParamBase 직렬화 제약: enum 은 string 으로 저장/로드 (DatumConfig.AlgorithmType 패턴 그대로).
- DatumConfig 필드 추가 시 ICustomTypeDescriptor 분기 + INI 정규화 hook 두 곳 동시 처리.
- ROI ID 기반 분기는 Action_FAIMeasurement.GrabOrLoadDatumImage 안에서 명시적 switch.

### Integration Points
- HalconViewerControl 자동 이미지 전환 — MainView 의 step change 핸들러 (현재 코드 패턴 확인 필요 — researcher 영역).
- Side fixture 시뮬레이션 이미지 — Cal_Image/Side/ 경로 (memory project_phase35_progress 의 Side fixture 보고 참조).

</code_context>

<specifics>
## Specific Ideas

- **알고리즘 이름**: `VerticalTwoHorizontalDualImage` — 향후 다른 algorithm 의 dual-image 변형 생기면 동일 접미사(`*DualImage`) 일관성 유지.
- **Side fixture 케이스가 실제 트리거**: Phase 35 Test 4 Side carry-over 해소가 본 phase 의 운영 가치. UAT 시 Side 실측 시나리오로 검증.
- **메모리 정정 필요**: `project_phase35_progress.md` 의 "Side fixture 는 수직 ROI = image1, 수평 ROI 2개 = image2" 기록은 사용자 결정(D-34-01)으로 정반대로 정정. write_context 직후 메모리 업데이트.

</specifics>

<deferred>
## Deferred Ideas

- **다른 algorithm 의 dual-image 변형** (CircleTwoHorizontalDualImage 등) — 본 phase 범위 외. 본 phase 의 enum 명명 패턴(`*DualImage`)이 향후 phase 의 ground work.
- **DatumFindingService HImage 이중 입력 시그니처 표준화** — 본 phase 는 algorithm 분기로 처리. 만약 dual-image variant 가 늘어나면 인터페이스 일반화 검토 (v1.2 후보).
- **두 이미지 해상도/orientation 불일치 검증** — 운영 중 발견 시 별도 phase 로 처리. 본 phase 는 동일 해상도 가정.
- **티칭 중 두 이미지 동시 미리보기** (split-view) — UI 확장 후보. 본 phase 는 step별 단일 이미지 표시.

</deferred>

---

*Phase: 34-datum-verticaltwohorizontal-2026-05-26*
*Context gathered: 2026-05-27*
