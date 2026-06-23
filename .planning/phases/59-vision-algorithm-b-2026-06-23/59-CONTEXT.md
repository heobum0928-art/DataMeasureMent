# Phase 59: Vision Algorithm (B) - Context

**Gathered:** 2026-06-24
**Status:** Ready for planning
**Mode:** `--auto` (사용자 외부 — Claude 가 권장 설계를 정하고 근거 문서화. 사용자 복귀 시 검토/수정 가능. UAT 는 Phase 58 과 동일하게 검증 직전 정지.)

<domain>
## Phase Boundary

v1.3 Align 비전의 **알고리즘 계층**. Phase 58(config+카메라) 위에 Shape Matching 정렬 엔진을 추가한다. 두 요구사항:

1. **AV-03 — Shape Model 티칭/매칭**: ROI 지정 → `create_shape_model` 티칭 → `.shm` 저장/로드, `find_shape_model` 로 매칭 위치(Row/Col/Angle/Score) 산출 (Halcon try-catch).
2. **AV-04 — 모드별 Offset 산출**: Tray 모드 = X/Y Offset, Bottom 모드 = X/Y + Theta. 각 모드 별도 템플릿.

**이 phase 범위 밖 (별도):** ROI 드로잉/버튼/뷰어 등 UI = Phase 61. 피커센터/각도 캘리브레이션 = Phase 60. `$RESULT` TCP 전송 = Phase 62. **Phase 59 는 서비스/알고리즘 계층만** — UI 없음. 티칭/매칭은 서비스 API 로 노출하고, 실제 ROI 드로잉 UI 배선은 Phase 61. UAT 런타임 항목은 Phase 58 처럼 Phase 61 UI 완성 후 일괄(검증 직전 정지).

**핵심 제약 (전 phase 공통):** 기존 Grabber 검사 코드(Sequence/Action/SystemHandler) **절대 수정 금지 — 추가만**. 이더넷 align 실패해도 Grabber 무영향. 헝가리언 · C# 7.2(switch expression/nullable refs/records 금지) · **모든 HALCON 호출 try-catch** · HObject/HTuple/HImage dispose(finally) · 함수 30줄 이하 · 매직넘버 const.

</domain>

<decisions>
## Implementation Decisions

### 서비스 구조 (D-01)
- **D-01:** 신규 `AlignShapeMatchService` 클래스(`WPF_Example/Custom/EthernetVision/`)를 만들고, **HALCON shape-matching 연산은 기존 `PatternMatchService`(`Halcon/Algorithms/PatternMatchService.cs`, phase 54~56)의 public 메서드를 재사용(composition/위임)** 한다. PatternMatchService 는 **수정하지 않음**.
  - 근거: PatternMatchService 는 이미 검증된 범용 알고리즘(Sequence/Action/SystemHandler 아님 → anti-goal 대상 아님)이고, public API(`TryCreateModel`/`TryFindRefPose`/`TryFindPose`/`TryBuildAlignRigid`)가 primitive 파라미터(HImage, double, string)만 받아 Grabber 결합 없음 → 호출만으로 안전 재사용. Phase 58 의 HikCamera 합성 재사용과 동일 패턴.
  - PatternMatchService 메서드는 무상태(modelId 등 호출-로컬) → 이더넷 경로 호출이 Grabber Datum align 에 영향 0.
  - `AlignShapeMatchService` 가 추가하는 것: 모드별(Tray/Bottom) 오케스트레이션 — 템플릿 선택, 티칭→.shm+ref pose, 매칭→offset(X/Y 또는 X/Y/Theta), px→mm 변환.

### 소유/통합 (D-02)
- **D-02:** `EthernetVisionHandler`(phase 58 싱글턴)가 `AlignShapeMatchService` 를 소유·노출한다 (`public AlignShapeMatchService Matcher { get; private set; }`), phase 58 D-03 아키텍처(handler 가 config+카메라 소유) 연장. 입력 이미지 = `EthernetVisionHandler.Handle.Camera.Grab()` (HImage, 호출자 dispose). **SystemHandler 추가 수정 없음**(phase 58 의 한 줄 init 이 handler 를 이미 기동 — Matcher 는 handler 내부에서 lazy 생성).

### Shape Model 파라미터 (D-03)
- **D-03:** PatternMatchService 기본값 재사용 — engine="Shape", NumLevels=4, Contrast="auto", MinContrast=10, Metric="use_polarity", greediness=0.9. AngleExtent(deg)는 모드별 파라미터: **Tray** 는 위치 위주라 작은 각도 허용(예: ±10°), **Bottom** 은 Theta 산출이 목적이라 넓은 각도(예: ±45° 또는 필요 시 ±180°). 정확한 기본값은 const 로 노출(런타임 튜닝/UAT 시 조정). angle/extent 는 PatternMatchService 규약(rad, CCW) 따름.

### .shm + 레퍼런스 포즈 저장 (D-04)
- **D-04:** 모드별 별도 템플릿 — `Tray.shm` / `Bottom.shm`. 저장 위치는 기존 recipe 폴더 규약 재사용하되 **이더넷 전용 하위 폴더로 격리**: `{RecipeSavePath}\{CurrentRecipeName}\ETHERNET_ALIGN\{Tray|Bottom}.shm` (RecipeFileHelper 패턴 참조, 신규 헬퍼 또는 직접 Path.Combine).
  - **레퍼런스 포즈 저장 필수**: offset = 현재매칭 − 티칭레퍼런스 이므로, 티칭 시 모델 생성 직후 같은 이미지에서 find → 레퍼런스 포즈(RefRow/RefCol/RefAngle) 산출 → `.shm` 옆 **사이드카 json**(Newtonsoft.Json — 기존 의존성)에 {RefRow, RefCol, RefAngle, AngleExtentDeg, Engine} 저장. 매칭 시 로드.

### Offset 산출 (D-05)
- **D-05:** find 결과(curRow/curCol/curAngle) − 레퍼런스(refRow/refCol/refAngle):
  - 픽셀 offset: `dRow = curRow − refRow`, `dCol = curCol − refCol`.
  - px→mm: `mm = px × (EthernetPixelResolution / 1000.0)` (8.652 μm/px → 0.008652 mm/px). **Row→OffsetY, Col→OffsetX** (이미지 좌표 규약; 부호/축 매핑은 UAT 에서 실 장비 기준 확정 — CONTEXT 에 명시).
  - **Tray**: OffsetX(mm), OffsetY(mm).
  - **Bottom**: OffsetX(mm), OffsetY(mm), **Theta(deg) = curAngle − refAngle**.
  - 결과 모델: 신규 `AlignResult` 클래스 {bool Found, double Score, double OffsetXmm, double OffsetYmm, double ThetaDeg, bool HasTheta}. Phase 62 TCP 가 소비.

### 실패 격리 (D-06)
- **D-06:** 전 public 메서드(Teach/Run) try-catch — 매칭 실패/모델 부재/Score 미달 시 `AlignResult{Found=false}` 반환, 예외 절대 throw 안 함(Grabber 무영향). HALCON 핸들(HImage/HShapeModel/HTuple)은 finally dispose. minScore const(예: 0.5) 미달 = not-found.

### API 표면 (D-07)
- **D-07:** `AlignShapeMatchService` public:
  - `bool TryTeach(HImage img, double roiRow, double roiCol, double roiPhi, double roiLen1, double roiLen2, EEthernetVisionMode mode, out string error)` — create+write .shm + ref pose json.
  - `AlignResult Run(HImage img, EEthernetVisionMode mode)` — read .shm+ref → find → offset.
  - `bool TryLoadTemplate(EEthernetVisionMode mode)` / `bool HasTemplate(EEthernetVisionMode mode)`.
  - ROI 입력은 파라미터(드로잉 UI 는 Phase 61). 매칭 입력 이미지는 호출자(또는 handler.Camera.Grab()) 제공.

### Claude's Discretion
- `AlignResult` 의 정확한 형태(struct vs class), 사이드카 json 스키마, 헬퍼 메서드 위치, const 명칭/값(AngleExtent 기본, minScore), Row↔Y/Col↔X 부호 규약 초기값(UAT 에서 확정).
- PatternMatchService 재사용 시 정확한 호출 어댑테이션(시그니처는 planner 가 실 소스로 확인).

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### v1.3 요구사항 / 로드맵
- `.planning/ROADMAP.md` §"Phase 59: Vision Algorithm (B)" — Goal/Success Criteria + v1.3 공통 제약.
- `.planning/REQUIREMENTS.md` line 104~105 (AV-03/AV-04) + line 116 제약 블록.
- `.planning/phases/58-config-camera-a-2026-06-23/58-CONTEXT.md` — phase 58 잠금 결정(handler/카메라/config 아키텍처).

### 재사용할 기존 Shape Matching 코드 (핵심 — 반드시 읽을 것)
- `WPF_Example/Halcon/Algorithms/PatternMatchService.cs` — **재사용 대상**. `TryCreateModel`(L56: reduce_domain+create_shape_model NumLevels=4/contrast auto/MinContrast 10 + write_shape_model .shm), `TryFindRefPose`(L170: read+find → ref Row/Col/Angle/Score), `TryFindPose`(L304: 검색영역 margin find + downsample), `TryBuildAlignRigid`(L479: vector_angle_to_rigid). **수정 금지, 호출만.**
- `WPF_Example/Halcon/.../RecipeFileHelper.cs` `GetPatternModelFilePath()` — `.shm`/`.ncm` 경로 규약(`{RecipeSavePath}\{recipe}\{seq}\{act}\{name}.shm`). 이더넷은 `ETHERNET_ALIGN` 하위로 격리.
- (참조) `WPF_Example/Custom/Sequence/.../InspectionSequence.cs` `TryComposeAlign` (L775~) — offset(dRow/dCol) + baseline angle(Phase 55) 계산 예시. **읽기 전용 참조**, 수정 금지(Grabber).

### Phase 58 통합 지점 (신규 — phase 58 산출물)
- `WPF_Example/Custom/EthernetVision/EthernetVisionHandler.cs` — 싱글턴 `Handle`, `.Camera`(EthernetAlignCamera), `.IsInitialized`, `.Initialize()`. **Matcher 프로퍼티 추가 위치.**
- `WPF_Example/Custom/EthernetVision/EthernetAlignCamera.cs` — `Grab()` → HImage(호출자 dispose), 폴백 `D:\align_test.bmp`.
- `WPF_Example/Custom/SystemSetting.cs` `[ETHERNET_VISION]` — `EthernetVisionMode`(None/Tray/Bottom), `EthernetPixelResolution`(8.652 μm/px → px×res/1000=mm).
- `WPF_Example/Custom/EthernetVision/EEthernetVisionMode.cs` — None/Tray/Bottom enum.

> 외부 형식 스펙(ADR 등) 없음 — 요구사항은 ROADMAP/REQUIREMENTS + 위 코드 패턴(특히 PatternMatchService)으로 충분.

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `PatternMatchService` (Halcon/Algorithms/): create/find/read/write_shape_model + vector_angle_to_rigid 완성 — AlignShapeMatchService 가 composition 으로 호출. 검증된 파라미터(NumLevels 4, contrast auto, MinContrast 10) 그대로 채택.
- `RecipeFileHelper.GetPatternModelFilePath` 패턴: .shm 경로 규약. 이더넷은 ETHERNET_ALIGN 하위 폴더.
- `EthernetVisionHandler.Handle` / `.Camera.Grab()` (phase 58): 이미지 소스 + 소유 지점.
- `SystemSetting.Handle.EthernetPixelResolution`(8.652) / `EthernetVisionMode`: px→mm + 템플릿 선택.
- Newtonsoft.Json(기존 의존성): 레퍼런스 포즈 사이드카 직렬화.

### Established Patterns
- HALCON 알고리즘 = try-catch + finally dispose (PatternMatchService 전반 패턴).
- 싱글턴 handler 가 서브 서비스 소유(SystemHandler→DeviceHandler 패턴 = EthernetVisionHandler→Matcher).
- enum 모드 분기 = classic switch/if-else(C# 7.2).

### Integration Points
- `EthernetVisionHandler` 에 `Matcher` 프로퍼티 + lazy 생성 추가(handler 내부 — SystemHandler/기존 파일 추가 수정 0).
- 신규 파일: `AlignShapeMatchService.cs`, `AlignResult.cs`(+ 사이드카 모델) — 전부 `Custom/EthernetVision/`.
- DatumMeasurement.csproj 에 신규 .cs 등록.

</code_context>

<specifics>
## Specific Ideas

- "신규 설계 말고 기존 패턴 확장": Phase 59 도 의도적으로 신규 알고리즘 최소화 — PatternMatchService(create/find/read/write_shape_model) 전면 재사용, 신규는 모드 오케스트레이션(Tray/Bottom 템플릿·offset·px→mm)+결과모델뿐.
- Row↔Y / Col↔X 부호·축 매핑은 실 장비(Tray/Bottom 좌표계) 기준이라 초기값 후 UAT 확정 — CONTEXT 의 D-05 에 가정 명시.
- Phase 58 과 동일 chain: 외부라 이번 59 는 --auto(Claude 결정), 검증 직전 정지. 사용자 복귀 시 CONTEXT 검토 + 58/59 UAT 일괄.

</specifics>

<deferred>
## Deferred Ideas

- 피커 센터 캘(36스텝 편심원) + 각도 캘 → **Phase 60 (AV-05/06)**.
- ROI 드로잉/Grab/Live/Stop 툴바/티칭 버튼/결과 패널 UI(TabControl) → **Phase 61 (AV-07/08)**.
- `$RESULT site=TRAY/BOTTOM` (OffsetX/Y[/Theta]) TCP 전송 → **Phase 62 (AV-09)**.
- EthernetExposure 실제 카메라 적용(SetFloatValue ExposureTime) → Phase 58 WR-03 이연(Phase 59/61 카메라 런타임 배선 시).

</deferred>

---

*Phase: 59-vision-algorithm-b-2026-06-24*
*Context gathered: 2026-06-24 (--auto, Claude-decided design)*
