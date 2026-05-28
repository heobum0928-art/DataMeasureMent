# Phase 36: Datum DualImage 설계 보강 - Context

**Gathered:** 2026-05-28
**Status:** Ready for planning

<domain>
## Phase Boundary

DualImage (`EDatumAlgorithm.VerticalTwoHorizontalDualImage`) 알고리즘에서:
1. 두 이미지 (가로축/세로축) 픽셀 좌표계 관계를 **SameFrame 계약**으로 명시 잠금하고 런타임 가드 추가
2. 검출 angle 의 정확성을 사용자가 검증할 수 있는 입력 파라미터 (`ExpectedAngleDeg` + `AngleTolerance`) 추가 + PropertyGrid PASS/FAIL 색상 배지
3. Test Find 시각화 강화 — DetectedOrigin 십자 + 각도 화살표를 현재 표시 캔버스에 렌더 + 화면 밖일 때 중앙 fallback
4. INI 라운드트립 (Phase 34.1 Test 4 동시 종결)

**스코프 밖:**
- per-image transform / HomMat2d 도입 (다른 카메라 페어 케이스) → Phase 27 또는 v1.2 이연
- Teach 시 ExpectedAngleDeg 자동 제안 (auto-populate)
- MainView toast / 외부 상태바 표시
- 헝가리안 적용 (v1.2 이연)

</domain>

<decisions>
## Implementation Decisions

### 좌표계 모델 + anchor 산출 (Area 1)

- **D-36-01:** **동일 카메라 + 다른 조명/Z 가정** — DualImage 의 두 이미지가 동일 sensor frame 보장 (Phase 27 통찰과 정렬). 좌표 변환 0.
- **D-36-02:** **anchor / offset / transform 모델 도입 안 함** — CO-34.1-09 의 "좌표계 통합 부재" 진단은 잘못. SameFrame 가정 하에 IntersectionLl 호출 (Find L674-677 / Teach L1424-1428) 이 이미 올바름. 진짜 문제는 시각화 + 검증 UX.
- **D-36-03:** **런타임 SameFrame 가드** — `TryFindDatum(imageHorizontal, imageVertical, ...)` (DatumFindingService L71) 및 `TryTeachDatum` 오버로드 (L796) 진입부에서 `imageHorizontal.Width == imageVertical.Width && Height 동일` 검증. 불일치 시 명확한 error 메시지로 즉시 반환 (예: `"DualImage requires same-frame image pair: horizontal {wH}x{hH} vs vertical {wV}x{hV}"`).
- **D-36-04:** **DatumConfig 메타 필드 추가 0** — 가드 4파일 (DatumConfig 핵심 + ParamBase serializer + InspectionListView whitelist + HalconDisplayService 1-image 분기) 변경 0 유지. SameFrame 계약은 코드 가드 + 주석으로 표현.

### ExpectedAngleDeg / AngleTolerance UX (Area 2)

- **D-36-05:** **DualImage 전용 필드** — DatumConfig 공동 필드로 선언하되 PropertyGrid Hide 분기 (L678-696 패턴) 에서 `VerticalTwoHorizontalDualImage` 만 노출. 기존 1-image algorithm 3종 (TwoLineIntersect / VerticalTwoHorizontal / CircleTwoHorizontal) PropertyGrid 회귀 표면 최소.
- **D-36-06:** **PASS/FAIL 표시 = 기존 `DetectedAngleDeg` 셀 배경 색상 배지** — PropertyGrid 의 ReadOnly DetectedAngleDeg 필드 배경을 PASS=#43A047 (연두) / FAIL=#E53935 (연빨) / 미평가 또는 sentinel=default 로 전환. 새 ReadOnly 상태 필드 없음 — DataTrigger 또는 동등 메커니즘.
- **D-36-07:** **기본값** — `AngleTolerance` = **1.0°**, `ExpectedAngleDeg` 입력 범위 = **[-180, 180]** (atan2 출력과 일치). INI 키 미존재 시 두 값 모두 0.0 → 미평가 sentinel (D-36-13).
- **D-36-08:** **Test Find 성공 직후 자동 평가** — `DatumFindingService.TryFindVerticalTwoHorizontalDualImage` 가 `DetectedAngleDeg` write-back (L720) 직후 PASS/FAIL 계산. 결과는 새 transient 필드 (예: `AngleValidationStatus` enum 또는 bool?) 에 기록 — INI/JSON 직렬화 제외 (`[Browsable(false)]` + `[JsonIgnore]`, Phase 17 D-13 transient 패턴 답습).

### Test Find 시각화 fallback (Area 3)

- **D-36-09:** **현재 표시 중 캔버스에 DetectedOrigin 렌더** — DualImage 상태에서 가로/세로 토글 어느 쪽이든 SameFrame 가정으로 좌표 유효. `HalconDisplayService.RenderDatumOverlay` 가 DualImage 분기에서 양쪽 캔버스 모두 십자 렌더 (CO-34.1-06 패턴 답습). 토글 이벤트에서 재호출 트리거 필요.
- **D-36-10:** **OFF-SCREEN fallback** — DetectedOrigin (Row, Col) 이 이미지 경계 `[0, width) × [0, height)` 밖일 때:
  - 캔버스 중앙에 fallback 십자
  - `(Row, Col)` 수치 텍스트 표시
  - `"OFF-SCREEN"` 라벨 표시
  - HalconDisplayService 신규 헬퍼 `DrawOriginFallback(window, w, h, row, col)`.
- **D-36-11:** **각도 화살표 시각화** — Origin 십자에서 방향선 추가:
  - 실선 화살표 = `DetectedRefAngle` 방향 (검출값)
  - 점선 화살표 = `ExpectedAngleDeg` 방향 (사용자 입력, 검증 활성 시에만)
  - 길이 약 40~80 픽셀 (이미지 크기에 비례 또는 고정)
  - PASS 시 두 화살표 시각적 일치, FAIL 시 시각적 어긋남

### INI 직렬화 + 하위호환 (Area 4)

- **D-36-12:** **Phase 22 IMG-01 + D-34-11 패턴 1:1 답습** — `ExpectedAngleDeg` + `AngleTolerance` 는 DatumConfig 공동 필드로 선언 (algorithm 무관 일률 직렬화). `EnsurePerRoiDefaults` 진입 시 null 정규화 (double 이라 null 불가, 0.0 sentinel 만 검사). PropertyGrid Hide 분기에서 DualImage 외 algorithm 은 hidden.
- **D-36-13:** **Sentinel = "Expected==0.0 또는 Tolerance==0.0 → 검증 off"** — TwoLineAngleToleranceDeg (L915) 패턴과 정렬. 기존 INI (Phase 35 이전 키 부재) 로 들어온 설정은 자연스럽게 미검증. 사용자가 명시적으로 두 값 모두 입력해야 검증 활성. 사용자가 0° 검증을 원하면 Tolerance 만 양수로 (예: Expected=0.0, Tolerance=0.5) — 그 경우 두 값 곱이 양수가 아니라 `Tolerance > 0` 만으로 활성화하는 방법도 검토 가능 (planner 가 판단).
- **D-36-14:** **Test 4 UAT = Phase 34.1 Test 4 클론** — Recipe 저장 → 제품 종료 → 재기동 → Recipe 로드 → DualImage 6 필드 (Algorithm/Vert/HorA/HorB/VPath/...) + Phase 36 신규 2 필드 (ExpectedAngleDeg/AngleTolerance) 라운드트립 일치 검증. Phase 22 INI MUST_HAVE 철칙 자동 적용.

### Claude's Discretion

- `AngleValidationStatus` 의 구체 타입 (enum 3값 `None/Pass/Fail` vs `bool?` vs 두 필드) — planner 가 가장 정합성 높은 표현 선택
- 색상 배지 메커니즘 (DataTrigger / IValueConverter / 코드비하인드) — 기존 InspectionListView PropertyGrid 패턴 따라
- 각도 화살표 정확한 길이/스타일 픽셀값 — 시각적 명료성 우선, planner 재량
- "각도 차이" 계산 시 wrap-around 처리 (예: Detected=179°, Expected=-179° 는 실제 2° 차이) — `Math.Abs(((Detected - Expected + 540) % 360) - 180)` 등 정합 공식, planner 가 결정

### Folded Todos

(해당 없음)

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### 부모 Phase / Carry-over 출처
- `.planning/phases/34.1-datum-dualimage-swap-ux-2026-05-27/` — Phase 34.1 partial signed_off, CO-34.1-09 carry-over 발생 시점
- `.planning/phases/34-datum-dualimage-2026-05-26/` — Phase 34 DualImage 신설 컨텍스트 (D-34-01/02/04/05/11/13/14 가드)

### ROADMAP / 상위
- `.planning/ROADMAP.md` §"Phase 36: Datum DualImage 설계 보강" (L244-259) — Goal, Success Criteria 6 항목, Background
- `.planning/REQUIREMENTS.md` — v1.1 milestone 요구사항 (DUI / IMG 시리즈)

### 알고리즘 / 코어
- `WPF_Example/Halcon/Algorithms/DatumFindingService.cs` — L71 (TryFindDatum DualImage 오버로드), L579 (TryFindVerticalTwoHorizontalDualImage), L674-677 (IntersectionLl Find), L796 (TryTeachDatum DualImage 오버로드), L1321 (TryTeachVerticalTwoHorizontalDualImage), L1424-1428 (IntersectionLl Teach), L915 (TwoLineAngleToleranceDeg sentinel 패턴 참고)
- `WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs` — L44/L50 (TeachingImagePath / TeachingImagePath_Vertical), L468-487 (DetectedOrigin/Angle transient), L506-522 (Datum|Result PropertyGrid 표시), L570-571 (EnsurePerRoiDefaults null 가드 - Phase 22 IMG-01 / D-34-11 패턴), L678-696 (PropertyGrid Hide 분기), L915 (TwoLineAngleToleranceDeg 게이트)
- `WPF_Example/Custom/Sequence/Inspection/EDatumAlgorithm.cs` — `VerticalTwoHorizontalDualImage` enum

### 시각화 / UI
- `WPF_Example/Halcon/Display/HalconDisplayService.cs` — `RenderDatumOverlay` (CO-34.1-06 hotfix 패턴 진입점), `Render` 메인 메서드 (L19)
- `WPF_Example/UI/ContentItem/MainView.xaml.cs` — `BtnTestFindDatum_Click` (CO-34.1-08 hotfix 진입점, DualImage 분기 보강 위치)
- `WPF_Example/UI/ContentItem/MainView.xaml` L54 — Halcon HWindowControlWPF airspace 주석 (CO-34.1-04 학습)
- `WPF_Example/UI/ControlItem/InspectionListView.xaml.cs` — `OnParamEditorSelectionChanged` whitelist (Phase 17 D-10, Phase 34 D-34-13 가드)
- `WPF_Example/UI/ControlItem/InspectionListViewModel.cs` — PropertyGrid 바인딩

### 실측 페어 (UAT 자산)
- `Cal_Image/DualImageTest/SIDE1_3-1_Datum_A1_A2_backlight_LV30.bmp` (가로축 페어)
- `Cal_Image/DualImageTest/SIDE1_3-1_Datum_B1_backlight_LV30.bmp` (세로축 페어)

### 패턴 / 가드 학습
- Phase 17 D-13 — Detected* transient PropertyGrid 미표시 + JsonIgnore 패턴
- Phase 17 D-16 — DetectedEdgeCount/FitRMSE/AngleDeg ReadOnly PropertyGrid 노출
- Phase 22 IMG-01 — INI 키 미존재 시 null 가드 / EnsurePerRoiDefaults 일률 정규화
- Phase 34 D-34-11 — TeachingImagePath_Vertical 1:1 답습 패턴 (algorithm 무관 일률 정규화)
- Phase 34 D-34-13/14, Phase 34.1 D-34.1-07 — 가드 4파일 변경 0 원칙

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets

- **DatumFindingService.TryFindVerticalTwoHorizontalDualImage** (L579-790) — 이미 imageHorizontal/imageVertical 두 입력 받음. SameFrame 가드는 진입부에 라인 2~3줄 추가만으로 충분.
- **TryTeachVerticalTwoHorizontalDualImage** (L1321 이후) — Find 와 대칭 구조. 동일 가드 패턴 미러링.
- **DetectedAngleDeg PropertyGrid 표시** (DatumConfig L519-522) — ReadOnly 셀이 이미 존재 → 색상 배지만 추가하면 D-36-06 충족.
- **EnsurePerRoiDefaults** (DatumConfig L527-) — Phase 22 IMG-01 패턴이 이미 자리잡힘. 신규 필드 2종 정규화 라인 2줄 추가 (D-36-12).
- **TwoLineAngleToleranceDeg 게이트** (DatumFindingService L915-929) — sentinel 패턴 정확히 동일 → 코드 답습 (D-36-13).
- **HalconDisplayService.RenderDatumOverlay** (CO-34.1-06 hotfix 후) — DualImage 분기 이미 존재. fallback 십자 + 각도 화살표는 신규 헬퍼로 추가.

### Established Patterns

- **transient 필드** = `[System.ComponentModel.Browsable(false)]` + `[PropertyTools.DataAnnotations.Browsable(false)]` + `[Newtonsoft.Json.JsonIgnore]` 세트 (Phase 17 D-13). `AngleValidationStatus` 가 transient 이면 동일 데코레이션.
- **DualImage 한정 PropertyGrid Hide** = `IsBrowsable` 또는 PropertyGrid 커스터마이저 / `case EDatumAlgorithm.VerticalTwoHorizontalDualImage:` 분기 (DatumConfig L696) → 동일 분기에 신규 필드 노출 조건 추가.
- **YYMMDD hbk Phase 36 주석** — 모든 신규/변경 라인에 `//260528 hbk Phase 36 D-36-XX` 부착 (사용자 피드백 메모리).

### Integration Points

- **DatumFindingService 신규 가드** — DualImage 오버로드 진입부 (Find L76 / Teach L800). 1-image 오버로드 영향 0.
- **PropertyGrid 색상 배지** — InspectionListView (또는 DatumConfig PropertyGrid 가 노출되는 view) DataTrigger / 커스텀 TemplateSelector. ListViewModel 이 새 transient status 필드를 감지.
- **HalconDisplayService 신규 헬퍼** — `DrawOriginFallback`, `DrawAngleArrow(detectedRad, expectedRad)` 헬퍼 신설. RenderDatumOverlay 의 DualImage 분기에서 호출.
- **MainView.xaml.cs BtnTestFindDatum_Click** — CO-34.1-08 hotfix 진입점. DualImage 분기에서 결과 처리 시 색상 배지 트리거 (DatumFindingService 가 transient status 쓰면 자동).
- **PropertyGrid 토글 이벤트** — DualImage 가로/세로 토글 시 RenderDatumOverlay 재호출 트리거 (D-36-09 필수).

</code_context>

<specifics>
## Specific Ideas

- 사용자 케이스 (2026-05-26 결정): "동일 카메라에 광원과 Z-Position 만 변경하여 촬영한 이미지 2장 → 좌표계 변환 0" (Phase 27 컨텍스트). Phase 36 의 SameFrame 계약은 이 가정의 명시화.
- 실측 페어 UAT 의 핵심 검증 시나리오: A1_A2 (가로) + B1 (세로) backlight LV30 페어에 ExpectedAngleDeg 입력 → Test Find → PropertyGrid 색상 + 시각화 일치 → "정확히 되었는지 안되었는지 판단" 가능 (사용자 피드백 2026-05-28 해소).
- Phase 34.1 학습 (회고록 후보): enum 추가 시 사용처 grep 검증 누락 → 6 hotfix 발생. Phase 36 은 enum 추가 0 (가드 4파일 변경 0 보존) → 동일 회귀 패턴 회피.

</specifics>

<deferred>
## Deferred Ideas

- **per-image transform / HomMat2d 모드** — 다른 카메라 또는 다른 시점 페어 케이스. Phase 27 에서 검토 또는 v1.2 이연. 현재 단일 카메라 가정 한정.
- **Teach 후 ExpectedAngleDeg 자동 제안 (auto-populate)** — Teach 성공 시 검출 angle 을 Expected 기본값으로 제안. UX 개선이지만 Phase 36 스코프 외 (사용자 명시 입력 우선). 별도 v1.1 신규 phase 또는 backlog.
- **MainView toast / 외부 상태바 PASS/FAIL** — D-34.1-07 가드 외 파일 변경 발생. PropertyGrid 배지로 충분.
- **TeachingImagePath/Vertical [Browse...] 버튼 UX** — `project_datum_image_input_ux_idea` 참조 (별도 v1.1 phase 후보).

</deferred>

---

*Phase: 36-datum-dualimage-coord-anchor-angle-validation-2026-05-28*
*Context gathered: 2026-05-28*
