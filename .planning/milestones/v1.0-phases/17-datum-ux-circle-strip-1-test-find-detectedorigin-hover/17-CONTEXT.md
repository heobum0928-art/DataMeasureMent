# Phase 17: datum-ux-circle-strip-1-test-find-detectedorigin-hover - Context

**Gathered:** 2026-04-30
**Status:** Ready for planning
**SPEC.md loaded:** No (Phase 16 UAT carry-over 16항목을 직접 lock)

<domain>
## Phase Boundary

Phase 16 UAT 가 carry-over 한 16개 UX/UI 결함을 한 묶음으로 해결한다. 4개 cluster:
(A) Circle 시각화 + EdgeDirection 정책 재설계,
(B) Edit 모드 + 그리기 UX 일관화,
(C) PropertyGrid 동적 노출 + 모달 정책,
(D) 신규 UI 기능 (Test Find DetectedOrigin 시각화 복구 + 마우스 hover 좌표/밝기 + ROI 결과 메트릭 노출).

**알고리즘 코드 보존 원칙 (Phase 16 D-22 연장)** — VisionAlgorithmService / DatumFindingService / DatumConfig 의 *계산 로직* 은 한 줄도 변경하지 않는다. 단 carry #17 (DetectedOrigin transient 필드 + TryFindDatum write-back) 만 알고리즘 결과를 transient 필드에 기록하는 한정 허용 — 계산 로직 변경 0, 신규 필드 write-back 만.

</domain>

<decisions>
## Implementation Decisions

### Cluster A — Circle 시각화 + EdgeDirection 정책

- **D-01:** Circle pre-teach Strip 사각형은 **0° (3시 방향) 고정 1개만 그린다**. 이전 Phase 16 의 "stepCount 개 모두 표시" (16-01 D-01) 정책 폐기. 사용자 의도: 1개 strip 의 방향/크기/내외 관계만 직관 인지하면 충분, 360° 분포는 알고리즘 내부에서만 처리. 구현: `RenderCircleStripOverlay` 의 stepCount 루프 제거, thetaRad=0 단일 사각형만 그림. RectL1Ratio/RectL2Ratio 변경 시 즉시 갱신 (Phase 16 D-03 패턴 유지).

- **D-02:** **Circle_RadialDirection enum 신규 PropertyGrid 필드 추가** — string sentinel "" + EnsurePerRoiDefaults fallback "Inward" + EdgeOptionLists.RadialDirections [Inward, Outward] PascalCase 단일 소스. INI 하위호환 (Phase 13-04 / 15-01 패턴 그대로). DatumConfig 에 Circle_RadialDirection (string) 추가 — Phase 16 D-22 algorithm 보존 제약 하에 *데이터 모델 only* 추가. 알고리즘 (TryFindCircleByPolarSampling) 의 polarity 인자가 "Inward"=positive, "Outward"=negative 로 mapping 되도록 caller 변경 (DatumFindingService.TryTeachCircleTwoHorizontal — 1 라인). VisionAlgorithmService 본체 0 라인.

- **D-03:** **Circle 분기에서 Circle_EdgeDirection 필드 hide** ([Browsable(false)] 동적). AlgorithmType=CircleTwoHorizontal 일 때 PropertyGrid 에 Circle_EdgeDirection 가 안 보이게 숨김. INI 저장은 그대로 (필드 자체 삭제 X — 하위호환). Cluster C 의 ICustomTypeDescriptor 패턴 (D-09) 과 동일 메커니즘 재사용.

- **D-04:** **EdgeDirection 모든 슬롯 × 모든 옵션 허용** — Horizontal_A / Horizontal_B / Vertical / Line1 / Line2 모든 ROI 의 EdgeDirection combobox 에 LtoR/RtoL/TtoB/BtoT 4개 모두 활성. PropertyGrid tooltip 으로 "일반적으로 수평 슬롯에는 LtoR/RtoL 권장" 안내. teach 실패 시 결과 모달 (D-12) 에 "검출 에지 0개 — EdgeDirection 을 반대로 시도해보세요" 힌트 추가. 16-UAT carry #16 의도 그대로.

### Cluster B — Edit 모드 + 그리기 UX

- **D-05:** **그리기 시작은 좌클릭+드래그부터** — btn_teachDatum ON + 캔버스 진입만으로는 미리보기 도형 안 그림. MouseLeftButtonDown 이벤트에서 드래그 시작 + MouseMove 로 크기 확장 + MouseLeftButtonUp 로 확정 (표준 WPF 패턴). 현재 캔버스 진입만으로 도형 따라오는 동작 제거.

- **D-06:** **Edit 모드 안에서만 ROI 이동/리사이즈 가능 (Rect + Circle + Polygon 공통 단일 gate)** — _isEditMode flag 기준으로 HitTestOneRoi 가 일괄 false 반환 (Phase 13-03 HitTestOneRoi gate 확장). Edit OFF 시 어떤 도형도 hit-test 미수행 → 클릭/드래그가 ROI 에 닿지 않음. 현재 Circle 의 항상 리사이즈 가능 결함 (carry #9) + Rect 와 Circle/Polygon 의 비대칭 (carry #13) 동시 해결. Drawing/Edit/View 3-state 분리는 over-engineering 으로 reject — 단일 _isEditMode 로 충분.

- **D-07:** **Delete ROI 모달 — 단일/전체 선택 (현재 Datum 범위)** — 우클릭 컨텍스트 메뉴 'Delete' → CustomMessageBox 3-button 모달: "이 ROI만 삭제" / "현재 Datum 의 모든 ROI 삭제" / "취소". Phase 13-03 의 (title, message) 시그니처 패턴 유지. carry #14 그대로.

### Cluster C — PropertyGrid 동적 노출 + 모달 정책

- **D-08:** **AlgorithmType 별로 그 알고리즘에 속한 파라미터만 PropertyGrid 에 노출** — TLI/CTH/VTH 모두 동일 패턴. Line1_* / Line2_* 는 TLI 에서만, Circle_* 는 CTH 에서만, Vertical_* 는 VTH 에서만. Horizontal_A_* / Horizontal_B_* 는 CTH+VTH 에서만. carry #11 + #15 의도 일치.

- **D-09:** **DatumConfig 에 ICustomTypeDescriptor 구현으로 [Browsable] 동적 제어** — TypeDescriptor.GetProperties 호출 시점에 AlgorithmType 검사하여 PropertyDescriptor 필터링. PropertyTools.Wpf 가 ICustomTypeDescriptor 를 존중함 (PropertyTools.DataAnnotations.Browsable 은 compile-time only — 동적 제어 불가). 컴파일 타임 [Browsable(false)] (예: SourceShotName, *Detected_*, raw edge tuples) 는 그대로 보존, 본 phase 는 **AlgorithmType 의존 동적 필터** 만 추가. 대안 "AlgorithmType 별 wrapper 클래스" reject (INI 직렬화 경로 + ParamBase reflection 충돌 우려).

- **D-10:** **AlgorithmType combobox 변경 시 흐름** — (1) PropertyGrid 즉시 갱신: Phase 16 D-09/D-10 의 SelectedObject null→new force rebind 그대로 재사용 + ICustomTypeDescriptor 가 새 AlgorithmType 으로 PropertyDescriptor 재계산. (2) LastTeachSucceeded=false 로 리셋 + DetectedOriginRow/Col / 검출 원/center cross / 검출 라인 외삽 시각화 모두 clear (RenderDatumOverlay 가 알아서 분기 안 그림). (3) **ROI 자체는 보존** — 사용자가 그린 도형은 알고리즘 변경 시에도 유지하여 재사용 가능. (4) **검출은 자동 실행 안 함** — 사용자가 btn_teachDatum 클릭해야 새 알고리즘으로 티칭 실행 (Phase 16 D-12 / D-13 일관). carry #12 의도 + Phase 16 Auto-reteach off 정책 유지.

- **D-11:** **AlgorithmType 변경 시 ROI 호환성 가드** — TLI ↔ CTH ↔ VTH 의 ROI 종류가 다르므로 (TLI=라인2개, CTH=원+H A/B, VTH=수직+H A/B), 변경 후 새 알고리즘이 요구하는 ROI 슬롯이 비어 있으면 btn_teachDatum 클릭 시 친절한 에러 메시지 ("Circle ROI 가 없습니다 — 캔버스에 원을 그리고 다시 시도하세요") 표시. ROI 자동 삭제 안 함. 현재 알고리즘이 사용하지 않는 ROI 도 보존 (ex. CTH 로 변경해도 Line1/Line2 INI 데이터 유지).

- **D-12:** **모달 정책 — 성공 시 모달 X, 실패 시 사유 모달 (teach + find 양쪽 동일)** — btn_teachDatum / btn_testFindDatum 양쪽 동일. 성공: HalconDisplayService 가 검출 원/center cross / 외삽 라인 / DetectedOrigin 타겟으로 알아서 시각화 — 별도 confirmation 모달 없음. 실패: CustomMessageBox 로 구체적 에러 메시지 표시 ("insufficient polar samples (1)" / "IntersectionLl returned 0 lines" 등). EdgeDirection 0개 검출 시 D-04 의 힌트 추가 ("EdgeDirection 을 반대로 시도해보세요"). carry #6 + #10 의도 그대로.

### Cluster D — 신규 UI 기능

- **D-13:** **DatumConfig 에 transient DetectedOriginRow/Col + DetectedRefAngle 필드 추가** — `[Browsable(false)] [PropertyTools.DataAnnotations.Browsable(false)] [JsonIgnore]` volatile double 3개. ParamBase reflection 자동 무시 + INI 저장/로드 경로 0 영향 (Phase 13-05 raw edges 패턴 연장). DatumFindingService.TryFindDatum (런타임 경로) 가 성공 시 이 필드에 write-back. **TryFindDatum 의 계산 로직은 한 줄도 변경하지 않으며 (Phase 16 D-22), 마지막에 *결과만* 신규 필드에 기록하는 라인 추가** 한정 허용. HalconDisplayService.RenderDatumFindResult (신규 메소드) 가 LastFindSucceeded 분기 하에 DispCross size=14 + 색상 구분 (예: cyan, raw edges 의 회색/녹색과 충돌 회피) + 기준각 화살표 렌더링.

- **D-14:** **DetectedOrigin 갱신 트리거 — btn_testFindDatum 클릭 경로** — 신규 버튼 또는 기존 버튼 재활용은 plan 단계 결정 (Claude's Discretion). 트리거 후 DatumFindingService.TryFindDatum 실행 → 성공 시 transient 필드 write-back → MainView 가 RenderDatumFindResult 호출하여 시각화. ROI 이동 시 자동 재실행은 안 함 (Phase 16 D-13 Auto-reteach off 일관).

- **D-15:** **마우스 hover 좌표 + 밝기 — MainView 상단 툴바 1줄** — `X: nnn  ·  Y: nnn  ·  Gray: nnn` 포맷. MainView.xaml 의 기존 툴바 영역에 TextBlock 3개 추가. HalconViewer MouseMove 이벤트 → 픽셀 좌표 (이미지 row/col, 정수) 변환 → HOperatorSet.GetGrayval 호출 (단일 픽셀 조회 가벼움) → 세 TextBlock 갱신. 이미지 바깥 호버 시 "N/A" 표시. throttle 불필요. DispatcherTimer 안 씀. WaferMapView.xaml.cs:1240 `MouseMove(object, System.Windows.Forms.MouseEventArgs)` 패턴 참고.

- **D-16:** **ROI 결과 메트릭 PropertyGrid 노출 (carry #5 부분 흡수)** — DatumConfig 에 [Browsable(true)] [ReadOnly(true)] transient 필드 일부 추가: DetectedEdgeCount (int) / DetectedFitRMSE (double) / DetectedAngleDeg (double). DatumFindingService 가 성공 시 write-back (D-13 과 동일 정책 — 계산 로직 변경 없이 결과만 기록). 사용자가 PropertyGrid 에서 검출 품질 즉시 확인 가능. **carry #5 의 "별도 미리보기 창" 부분은 deferred** — 본 phase 분량 과대.

### Cross-cutting (전역)

- **D-17:** Phase 16 D-22 (algorithm preservation) **연장** — VisionAlgorithmService.cs / DatumFindingService.cs 의 *계산 로직 라인* 은 0 변경. 허용되는 변경: (a) D-02 의 polarity 인자 mapping 1 라인 (caller 측), (b) D-13 의 transient 필드 write-back 라인 (TryFindDatum 결과 기록), (c) D-16 의 transient 메트릭 write-back 라인. 그 외 0 라인. msbuild Debug/x64 PASS, 신규 warning 0 on 수정 범위.

- **D-18:** **모든 변경 라인 위에 `//260430 hbk Phase 17 <reason>` 주석** (사용자 강제 컨벤션, Phase 16 D-20 연장). grep count acceptance 강제.

- **D-19:** **Allman brace style** — Halcon 모듈 (HalconDisplayService) 기존 스타일 유지. InspectionListView / MainView 는 K&R 스타일 (file 마다 기존 스타일 따름).

- **D-20:** **Phase 14-04 D-13 (`rectPhi=thetaRad`) + Phase 15-02/03 (selection wiring) + Phase 16 D-09/D-10 (force rebind) + D-13/D-14 (Auto-reteach off) 모두 보존** — diff 검증으로 회귀 0 강제.

### Plan 구조 (제안 — planner 가 최종 결정)

- **D-21:** Plan 17-01 = Cluster A (Circle 시각화 1개 + RadialDirection enum + EdgeDirection 정책 D-01~D-04). 주 변경: HalconDisplayService.cs / DatumConfig.cs / DatumFindingService.cs (caller 1라인) / EdgeOptionLists.cs.

- **D-22:** Plan 17-02 = Cluster B + C (Edit 모드 + Drawing UX + PropertyGrid 동적 노출 + 모달 정책 D-05~D-12). 주 변경: MainView.xaml.cs / InspectionListView.xaml.cs / DatumConfig.cs (ICustomTypeDescriptor) / CustomMessageBox 호출 사이트.

- **D-23:** Plan 17-03 = Cluster D (DetectedOrigin + Hover + 결과 메트릭 D-13~D-16). 주 변경: DatumConfig.cs (transient 필드) / DatumFindingService.cs (write-back 라인) / HalconDisplayService.cs (RenderDatumFindResult) / MainView.xaml + xaml.cs (툴바 + MouseMove).

- **D-24:** Plan 17-04 = UAT — Phase 16 carry-over 16항목 검증 + Phase 17 신규 결정 (D-01~D-16) 검증. 통합 시나리오. ≥ 16 시나리오 signed_off.

### Claude's Discretion

- **DispCross size 정확값** (DetectedOrigin 시각화) — D-13 에서 size=14 로 안내했으나 실제 시인성 확인 후 plan 단계에서 12~16 사이 조정 가능.
- **DetectedOrigin 시각화 색상** — cyan 기본 가정, Phase 13-05/16-01 색상 팔레트 (cyan=Line1, magenta=Line2, yellow=center cross, light green=검출원, gray=raw edges) 와 충돌 검토 후 plan 단계에서 결정 (예: orange/purple 후보).
- **btn_testFindDatum 신규 버튼 추가 vs 기존 컨텍스트 메뉴 재활용** — D-14 명시 안 함, plan 단계에서 InspectionListView 우클릭 메뉴 vs MainView 툴바 신규 버튼 결정.
- **AlgorithmType 변경 시 ROI 자동 삭제 안 하지만 호환성 경고 모달 표시 여부** — D-11 에서 friendly error 만 명시, plan 단계에서 변경 즉시 경고 vs btn_teachDatum 시점 경고 결정.
- **Hover 좌표 / 밝기 표시 단위** — D-15 에서 픽셀 row/col + GetGrayval 정수 명시. mm 단위 표시 추가 여부는 plan 단계 (Phase 2 캘리브레이션 PixelResolution 활용 가능, 단 이미지 좌표계 ↔ mm 변환 + Datum 보정 후 vs 보정 전 어느 좌표계 표시할지 결정 필요).

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Phase 16 (carry-over 원천 + 보존 결정)
- `.planning/phases/16-datum-circle-strip-redesign-algorithmtype-binding-fix/16-UAT.md` — Phase 17 carry-over 16항목 정의 (Section "Phase 17 Carry-over"). 본 phase 의 *요구사항 원천*.
- `.planning/phases/16-datum-circle-strip-redesign-algorithmtype-binding-fix/16-CONTEXT.md` — Phase 16 의 결정 (D-01~D-22). 본 phase 가 보존 + 일부 폐기 (D-01 Circle stepCount 시각화 폐기, D-09/D-10/D-13/D-14/D-22 보존).
- `.planning/phases/16-datum-circle-strip-redesign-algorithmtype-binding-fix/16-01-SUMMARY.md` — RenderCircleStripOverlay 구현 (본 phase D-01 에서 1개로 변경).
- `.planning/phases/16-datum-circle-strip-redesign-algorithmtype-binding-fix/16-02-SUMMARY.md` — InspectionListView force rebind + MainView Auto-reteach off (본 phase D-09/D-10 재사용).

### Phase 15 (selection 인자화 + algorithm 보존 대상)
- `.planning/phases/15-halcon-measurepos-measurephi-edgeselection-datumfindingservi/15-02-SUMMARY.md` — AppendEdgePointsFromStrip 4-way measurePhi + 9 caller selection wiring (본 phase 보존).
- `.planning/phases/15-halcon-measurepos-measurephi-edgeselection-datumfindingservi/15-03-SUMMARY.md` — TryFindCircleByPolarSampling selection 인자화 (본 phase D-02 polarity mapping 의 caller 측 1라인 추가 후보).

### Phase 14-04 (Circle 알고리즘 핵심 — 보존)
- `.planning/phases/14-datum-carry-over-circle-vertical-roi-2-out-of-range-ux/14-04-SUMMARY.md` — TryFindCircleByPolarSampling 구현 (rectPhi=thetaRad, 본 phase 보존).

### Phase 13 (raw edges + per-ROI sentinel 패턴)
- `.planning/phases/13-datum-algorithm-extensibility/13-04-SUMMARY.md` — DatumConfig per-ROI sentinel + INI 하위호환 패턴 (본 phase D-02 RadialDirection / D-13 transient 필드 / D-16 결과 메트릭 모두 동일 패턴).
- `.planning/phases/13-datum-algorithm-extensibility/13-05-SUMMARY.md` — RenderRawEdgePoints + DrawExtendedLine + DispCross batch 시각화 패턴 (본 phase D-13 RenderDatumFindResult 의 분리 패턴 참조).

### Phase 12-03 (PropertyGrid binding + DatumConfig RaisePropertyChanged)
- `.planning/phases/12-datum-circle-vertical-horizontal-intersection/12-03-SUMMARY.md` — RaisePropertyChanged + RefreshParamEditor 이중 신호 (본 phase D-09 ICustomTypeDescriptor 구현 후 SelectedObject null→new 와 결합).

### Phase 13-03 (Datum ROI 식별 + CustomMessageBox 시그니처)
- `.planning/phases/13-datum-algorithm-extensibility/13-03-SUMMARY.md` — RoiId.StartsWith("Datum.") gate + HitTestOneRoi 구조 + CustomMessageBox (title, message) swap (본 phase D-06 단일 _isEditMode gate / D-07 Delete 모달 패턴 재사용).

### 코드 파일 (편집 대상)
- `WPF_Example/Halcon/Display/HalconDisplayService.cs` — Circle 분기 RenderCircleStripOverlay 1개로 축소 (D-01) / RenderDatumFindResult 신규 (D-13) / RenderDatumOverlay 의 ICustomTypeDescriptor 와 무관 (시각화 분기는 LastTeachSucceeded + AlgorithmType 으로 그대로).
- `WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs` — Circle_RadialDirection 신규 (D-02) / EnsurePerRoiDefaults 확장 / ICustomTypeDescriptor 구현 (D-09) / DetectedOriginRow/Col + DetectedRefAngle (D-13) / DetectedEdgeCount + FitRMSE + AngleDeg (D-16).
- `WPF_Example/Custom/Sequence/Inspection/EdgeOptionLists.cs` — RadialDirections 추가 (D-02).
- `WPF_Example/Halcon/Algorithms/DatumFindingService.cs` — TryTeachCircleTwoHorizontal Circle_RadialDirection→polarity mapping 1 라인 (D-02 caller) / TryFindDatum 결과 transient write-back 라인 추가 (D-13/D-16) — *계산 로직 0 라인*.
- `WPF_Example/Custom/UI/MainView.xaml.cs` — 좌클릭+드래그 그리기 (D-05) / Edit 모드 단일 gate (D-06) / Delete 모달 호출 (D-07) / AlgorithmType 변경 흐름 (D-10/D-11) / btn_testFindDatum 트리거 (D-14) / MouseMove hover 핸들러 (D-15).
- `WPF_Example/Custom/UI/MainView.xaml` — 상단 툴바 X/Y/Gray TextBlock (D-15).
- `WPF_Example/Custom/UI/InspectionListView.xaml.cs` — Phase 16 D-09/D-10 force rebind 보존 + ICustomTypeDescriptor 와 호환 (D-09).

### 코드 파일 (읽기 전용 — 보존 대상 검증용)
- `WPF_Example/Halcon/Algorithms/VisionAlgorithmService.cs` — TryFindCircleByPolarSampling (Phase 14-04 / 15-03 결정 보존, diff = 0 강제).
- `WPF_Example/Halcon/Algorithms/DatumFindingService.cs` 의 *계산 로직 라인* — TryTeach* / 라인/원 fitting / IntersectionLl 등 모두 보존 (D-17 explicitly 허용 라인 외 diff = 0).

### 외부 참조 패턴
- `WPF_Example/Custom/UI/ContentItem/WaferMapView.xaml.cs` (line 1256 `MouseMove`) — 픽셀 좌표 + 그레이 값 hover 표시 패턴 (D-15 참조).
- `MeasurementAlgorithm.cs:130-178` — measurePhi canonical mapping (Phase 15-02 보존, 본 phase 무관).
- `CLAUDE.md` § Code Style — Allman vs K&R brace style 파일별 따름 (D-19).

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- **HalconDisplayService.RenderCircleStripOverlay** (Phase 16-01) — stepCount 루프를 단일 thetaRad=0 호출로 축소 (D-01).
- **HalconDisplayService.RenderRawEdgePoints + DrawExtendedLine** (Phase 13-05) — DispCross batch 패턴 (D-13 RenderDatumFindResult 의 동일 패턴 신규 메소드 작성 시 참조).
- **DatumConfig.RaisePropertyChanged + InspectionListView.RefreshParamEditor + SelectedObject null→new** (Phase 12-03 + 16-02) — D-10 의 즉시 갱신 패턴 그대로.
- **HitTestOneRoi private static** (Phase 13-03) — Rect/Circle/Polygon 공용. _isEditMode gate 1개 추가로 D-06 달성.
- **CustomMessageBox(title, message)** (Phase 13-03 swap 후 표준) — D-07 Delete 단일/전체 모달 + D-12 실패 사유 모달 모두 재사용.
- **WaferMapView.MouseMove + GetGrayval** — D-15 hover 좌표/밝기 패턴 참고 (단, WaferMapView 는 Mat 기반, 본 phase 는 HImage 기반 → HOperatorSet.GetGrayval 직접).
- **EdgeOptionLists** — Phase 15-01 Selections / Phase 13-04 Directions 패턴. D-02 RadialDirections 동일 패턴 추가.
- **DatumConfig per-ROI sentinel + EnsurePerRoiDefaults** (Phase 13-04) — D-02 / D-13 / D-16 의 신규 필드 모두 동일 패턴 (sentinel "" + idempotent migration).

### Established Patterns
- **//YYMMDD hbk 주석 컨벤션** (D-18, 사용자 강제).
- **PropertyTools.DataAnnotations.Browsable(false)** — DatumConfig 에 다수 (compile-time only). D-09 의 동적 제어는 ICustomTypeDescriptor 로 별도 layer 추가 — 기존 attribute 보존.
- **Allman brace style** — Halcon 모듈 (HalconDisplayService 등). UI 모듈은 K&R. file 마다 기존 스타일 따름 (D-19).
- **transient volatile HTuple/double 필드 + [Browsable(false)] + ParamBase reflection 자동 무시** — Phase 13-05 raw edges 패턴. D-13 / D-16 동일 패턴.
- **AlgorithmType ↔ DatumConfig 의존 분기** — RenderDatumOverlay 가 AlgorithmTypeEnum 으로 분기 (Phase 12-03). D-10 의 시각화 clear 는 RenderDatumOverlay 의 LastTeachSucceeded=false 분기로 자연 처리.

### Integration Points
- **MainView.HalconViewer_DatumRect/CircleCompleted** — 그리기 완료 콜백 (D-05 의 좌클릭+드래그 시작 시점에서 진입).
- **MainView.RoiMoveCompleted** — ROI 이동 완료 콜백 (Phase 16 D-13 Auto-reteach off 보존).
- **MainView.HalconViewer.MouseMove** — D-15 hover 진입점 (이미지 좌표 변환 + GetGrayval 호출).
- **InspectionListView.SelectedNodeChanged + AlgorithmType combobox** — D-10 의 PropertyGrid 갱신 진입점 (Phase 16 D-09/D-10 패턴 그대로).
- **DatumFindingService.TryFindDatum** — D-13 / D-16 의 transient write-back 진입점 (메서드 끝 결과 분기에 1~3 라인 추가).
- **HalconDisplayService.RenderDatumOverlay** — D-13 의 RenderDatumFindResult 호출 추가 위치 (LastFindSucceeded 분기).

### 알려진 제약 (PropertyTools.Wpf)
- `PropertyTools.DataAnnotations.Browsable` 는 **compile-time** attribute 임 (정적). 동적 제어 불가.
- `System.ComponentModel.ICustomTypeDescriptor` 는 PropertyTools.Wpf 가 GetProperties() 호출 시 존중하는 표준 .NET 메커니즘 → D-09 의 정도 (degree-of-freedom).
- DatumConfig 가 ParamBase 를 상속하고 있어 ICustomTypeDescriptor 구현 시 ParamBase reflection (Save/Load) 와 충돌 가능성 존재 — plan 단계에서 ParamBase.GetProperties 사용 vs ICustomTypeDescriptor.GetProperties 사용 경계 확인 필요. 잠재 위험 노트: ICustomTypeDescriptor 가 전체 GetProperties 를 덮어쓰면 ParamBase INI 직렬화가 깨질 수 있음 → **ICustomTypeDescriptor.GetProperties(Attribute[]) 만 PropertyGrid 용으로 필터링하고 GetProperties() 무인자는 base 호출 유지** 가 안전한 구현 방향.

</code_context>

<specifics>
## Specific Ideas

- **사용자 인용 1 (Circle strip 1개):** "16-UAT.md Phase 17 Carry-over #1: 시각화 정책: N개 strip → 1개만 표시" — 본 phase D-01 의 기준점. 0° (3시 방향) 고정 1개 = "1개 strip 의 방향/크기/내외 관계만 직관 인지" 의도 충족.

- **사용자 인용 2 (RadialDirection):** "16-UAT.md Phase 17 Carry-over #2: PropertyGrid: Circle_RadialDirection enum (Inward/Outward)" — D-02 의 직접 근거.

- **사용자 인용 3 (EdgeDirection):** "16-UAT.md Phase 17 Carry-over #16: EdgeDirection 모든 옵션 허용 (slot 무관) + tooltip + 검출 0 시 힌트" — D-04 의 근거.

- **사용자 인용 4 (그리기 시작):** "16-UAT.md Phase 17 Carry-over #8: 그리기 시작: 좌클릭+드래그부터 (캔버스 진입 시 미리보기 X)" — D-05 의 근거.

- **사용자 인용 5 (Edit 모드):** "16-UAT.md Phase 17 Carry-over #9: Circle ROI Edit 모드 결함 (항상 사이즈 변경 가능)" + "#13: Edit 모드에서만 사이즈/이동 가능" — D-06 의 통합 근거.

- **사용자 인용 6 (Delete 모달):** "16-UAT.md Phase 17 Carry-over #14: Delete ROI: 확인 모달 + 단일/전체 (현재 Datum 범위)" — D-07 의 근거.

- **사용자 인용 7 (PropertyGrid 동적 노출):** "16-UAT.md Phase 17 Carry-over #11: 선택된 알고리즘 파라미터만 PropertyGrid 노출" + "#15: 모든 알고리즘 (TLI/CTH/VTH) 동일 패턴 적용" — D-08 / D-09 통합 근거.

- **사용자 인용 8 (AlgorithmType 변경):** "16-UAT.md Phase 17 Carry-over #12: AlgorithmType 변경 → PropertyGrid 즉시 갱신, 검출은 test 시점" — D-10 근거.

- **사용자 인용 9 (모달 정책):** "16-UAT.md Phase 17 Carry-over #6: 성공 시 모달 X (실패 시만 모달)" + "#10: 실패 시 사유 모달 (teach + find 양쪽)" — D-12 통합 근거.

- **사용자 인용 10 (DetectedOrigin):** "16-UAT.md Phase 17 Carry-over #17: Test Find Datum Origin 시각화 결함 — DetectedOriginRow/Col transient 필드 + TryFindDatum 갱신 + RenderDatumFindResult 사용" — D-13 / D-14 의 직접 근거 (구현 경로까지 사용자 명시).

- **사용자 인용 11 (Hover):** "16-UAT.md Phase 17 Carry-over #18: 마우스 hover 좌표 + 밝기 라이브 표시 (상단 툴바 'X / Y / Gray' 1줄)" — D-15 근거.

- **사용자 인용 12 (결과 메트릭):** "16-UAT.md Phase 17 Carry-over #5: 개별 ROI 미리보기 + 에지 품질 메트릭 PropertyGrid 노출" — D-16 의 부분 흡수 근거 (별도 미리보기 창 부분은 deferred).

</specifics>

<deferred>
## Deferred Ideas

- **carry #5 의 별도 미리보기 창 (절단된 ROI 영역 + 에지 키주 표시)** — 본 phase 는 결과 메트릭 PropertyGrid 노출 (D-16) 만 흡수. 미리보기 창은 별도 phase 또는 quick task. 사유: 본 phase 4 cluster 분량 이미 과대 (3 plans + UAT).

- **Drawing/Edit/View 3-state toolbar 분리** — D-06 에서 단일 _isEditMode gate 로 충분하다고 판단. 사용자가 향후 더 정교한 mode 분리 원할 시 별도 phase.

- **mm 단위 hover 좌표 표시** — D-15 에서 픽셀 row/col + GetGrayval 정수만 표시 결정. mm 변환 + Datum 보정 후 좌표는 별도 phase 또는 plan 단계 Claude's Discretion. 사유: 보정 전 vs 보정 후 어느 좌표계 표시할지 결정 필요 (Datum 미티칭 시 보정 불가).

- **AlgorithmType 변경 시 ROI 자동 마이그레이션** — D-11 에서 ROI 자동 삭제 안 하고 보존만. CTH→VTH 시 Vertical 슬롯이 비어 있으므로 사용자가 수동으로 새로 그려야 함. ROI 자동 마이그레이션은 over-engineering (각 알고리즘 ROI 종류가 근본적으로 다름).

- **btn_teachDatum 진행 progress bar / async** — Phase 16 D-15 deferred 그대로 유지. HALCON 호출 1 sec 초과 시 향후 검토.

- **DetectedOrigin 좌표 mm 표시 (상단 툴바)** — D-15 hover 와 별개로, Test Find 후 검출된 Origin 의 픽셀 좌표만 시각화 (D-13). mm 좌표는 deferred.

- **EdgeDirection tooltip 의 다국어 지원** — D-04 tooltip 한글로 작성. LocalizationResource 통합은 deferred.

</deferred>

---

*Phase: 17-datum-ux-circle-strip-1-test-find-detectedorigin-hover*
*Context gathered: 2026-04-30 (interactive discuss-phase)*
*Next step: /gsd-plan-phase 17 — break down into 4 plans (17-01 Cluster A / 17-02 Cluster B+C / 17-03 Cluster D / 17-04 UAT)*
