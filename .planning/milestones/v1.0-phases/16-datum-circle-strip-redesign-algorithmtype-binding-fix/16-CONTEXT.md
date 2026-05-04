# Phase 16: datum-circle-strip-redesign-algorithmtype-binding-fix - Context

**Gathered:** 2026-04-29 (--auto mode)
**Status:** Ready for planning
**SPEC.md loaded:** Yes (5 requirements locked, ambiguity 0.17)

<domain>
## Phase Boundary

Datum 티칭 UX 의 시각화 + UI binding + 자동화 정책 3 결함을 한 묶음으로 수정. **모든 알고리즘 코드는 보존** (Phase 14-04 D-13 + Phase 15 결정 그대로) — 본 phase 는 HalconDisplayService rendering, InspectionListView 핸들러, MainView 자동 재티칭 트리거에 한정. SPEC.md 의 5 requirements 가 WHAT 을 잠그고, 본 CONTEXT 가 HOW 결정을 잠근다.

</domain>

<decisions>
## Implementation Decisions

### Circle Pre-teach Strip 시각화 (R1)
- **D-01:** 원 ROI 그린 직후 360°/PolarStepDeg 만큼의 strip 사각형 stepCount 개를 **모두 정적으로** 한 번에 시각화. 사용자가 strip 분포 + RectL1Ratio/RectL2Ratio 의 크기 의도를 한눈에 인지 가능. (대안 1개/4개 표시는 시각적 정보 부족 — 사용자 의도 "초기 호 포함 작은 사각형 셋팅" 에 부합하려면 전체 분포가 필요.)
- **D-02:** Strip 색상은 **회색 thin line** (Phase 13-05 의 cyan/magenta line 색상과 충돌 회피, 노란색은 ROI 라벨 전용). DispLine + GenRectangle2 폴리곤 외곽선 only — fill 없음.
- **D-03:** PropertyGrid 의 RectL1Ratio / RectL2Ratio / PolarStepDeg 변경 시 **즉시 시각화 업데이트** — DatumConfig.RaisePropertyChanged + RefreshParamEditor 패턴 + RenderDatumOverlay 재호출. (Phase 12-03 Gap-3 fix 와 동일 패턴 재사용.)
- **D-04:** 검출된 원 (FitCircle 결과) 은 **티칭 후만** 표시 — LastTeachSucceeded == true 분기.

### Circle Post-teach 검출 결과 시각화 (R2)
- **D-05:** 검출 원 = **녹색 (light green)** — Phase 7 의 녹/적 에지 색상 컨벤션 연장 (검출 성공 = 녹색).
- **D-06:** Center cross = **노란색 + 큰 사이즈** — DispCross size=12 + 굵기 강조 (정밀 원 center 표시가 최종 목적이므로 가장 두드러지게).
- **D-07:** Raw edge points (allRows/allCols) = **회색 작은 십자가** — DispCross size=4 — 검출 과정 trace 용 (Phase 13-05 패턴 연장).
- **D-08:** 시각화 z-order: ROI 경계 (under) → Strip 사각형 (under) → Raw edge points → 검출 원 → Center cross (top) — center 가 가려지지 않게.

### AlgorithmType 핸들러 핵심 수술 (R3)
- **D-09:** InspectionListView 의 Datum 노드 선택 핸들러에서 **`_editingDatum` 강제 재할당** — 이전 reference 보존 안 함, 매번 새로 선택된 DatumConfig 인스턴스로 즉시 교체.
- **D-10:** PropertyGrid SelectedObject **null → newDatum 의 두 단계 force rebind** — null 할당으로 PropertyGrid 의 기존 binding 강제 해제 후, 새 인스턴스 할당. AlgorithmType combobox 가 stale 상태로 남지 않도록 보장. (Phase 12-03 Gap-3 의 RaisePropertyChanged + RefreshParamEditor 만으로는 AlgorithmType 까지 안 닿는 것이 Phase 15 UAT Test 10~12 결함의 근본 원인.)
- **D-11:** ROI 편집 모드 (Datum.TeachDatum / FAI ROI Edit) 진입/종료 시 **`_editingDatum` reference 무영향** — 편집 모드 상태와 reference 교체 로직을 **분리**. 편집 모드 종료 시 마지막 Datum 노드 선택 상태를 보존하지 않고, 다음 노드 클릭 시 항상 새로 교체.
- **D-12:** AlgorithmType combobox 변경 자체는 자동 재티칭 **안 함** — D-13 (Auto-reteach off) 와 일관. 사용자가 명시적으로 btn_teachDatum 클릭해야 새 알고리즘으로 티칭 실행.

### Auto-reteach Off (R4)
- **D-13:** MainView.RoiMoveCompleted 의 **InvokeTryTeachDatum 호출 단순 제거** — Phase 13-04 의 자동 트리거 라인을 직접 삭제. SystemSetting 토글 옵션 도입 안 함 (단순화 우선, 사용자가 항상 수동 트리거 원함).
- **D-14:** ROI 이동 후 검출 결과는 **이전 LastTeachSucceeded 상태를 그대로 유지** — 검출 원/center 시각화는 stale 데이터를 계속 보여줌 (사용자가 새 ROI 위치와 이전 검출의 mismatch 를 시각적으로 인지 가능). 사용자가 btn_teachDatum 클릭하면 갱신.
- **D-15:** btn_teachDatum 클릭 시 **별도 progress UI 없음** — 단순 동기 호출 + 완료 후 시각화 갱신 (HALCON 호출 < 1 sec 추정, progress bar 불필요).
- **D-16:** "티칭 완료" 별도 버튼 **신설 안 함** — btn_teachDatum 단일 트리거로 통일 (사용자 SPEC out-of-scope 명시).

### Plan 구조 (사용자 합의)
- **D-17:** 16-01 = HalconDisplayService Circle overlay 재작성 (R1 + R2 — D-01~D-08).
- **D-18:** 16-02 = AlgorithmType 핸들러 핵심 수술 + Auto-reteach off (R3 + R4 — D-09~D-16).
- **D-19:** 16-03 = UAT (R5 — Phase 15 carry-over 6건 흡수 + Phase 16 신규 시나리오).

### Cross-cutting (전역)
- **D-20:** 모든 코드 수정 라인 위에 `//260429 hbk <reason>` 주석 (사용자 강제 컨벤션).
- **D-21:** 빌드: msbuild Debug/x64 PASS, 신규 warning 0.
- **D-22:** Phase 14-04 D-13 (`rectPhi=thetaRad`) + Phase 15-02 (9-site selection wiring) + Phase 15-03 (selection 인자화) **모두 보존** — diff 검증.

### Claude's Discretion
- Strip 사각형 외곽선 thickness — DispLine 기본 또는 SetLineWidth(2) 중 선택, 시각적 가독성 우선.
- Raw edge points 시각화 batch size 최적화 (Phase 13-05 의 size=6 angle=0 vs 본 phase 의 size=4 — Plan 단계 결정).
- AlgorithmType combobox null→new force rebind 의 정확한 구현 패턴 (PropertyGrid SelectedObject 직접 vs PropertyGrid.Refresh() 호출 — 라이브러리 동작에 따라 plan 단계 결정).
- ROI 편집 모드 종료 핸들러 위치 (MainView vs InspectionListView — 조사 후 plan 결정).

</decisions>

<specifics>
## Specific Ideas

- **사용자 인용 1 (Circle 결함):** "지금 ROI 원 자체를 Rectangle로 바꾸어서 그 사이즈만큼 제자리에서 돌림 그게 아니라 ROI원을 그리고 왼쪽 반지름 까지 점을 위치 이동후 거기서 원호를 포함하는 작은 사각형을 생성 그리고 원 ROI 센터를 회전 중심으로 반시계 방향으로 10도 혹은 1도 사용자 설정으로 회전 이동함 그리고 사각형이 각도가 변함 그 각도로 안쪽에서 바깥쪽 에지를 구할꺼냐 바깥쪽에서 안쪽으로 에지를 구할꺼야 이거를 360도 돌려서 해당 에지점들을 가지고 fit_circle_contour_xld 를 만들어 원을 구함" → **알고리즘 의도 자체는 이미 정확히 구현되어 있으나 시각화에서 인지 못함** → HalconDisplayService 시각화 재작성으로 해결.

- **사용자 인용 2 (Datum binding):** "Datum이 총 3개가 있어, 데이텀 1은 twolineinspect고, datum2는 circleTwoHorizontal이고, datum 3은 VerticalTwohorizontal인데 처음 레시피를 열었을때는 datum을 선택할때마다 해당 알고리즘타입으로 변하는데 ROI를 이동하거나 새로 만들때는 datum을 변화시켜도 알고리즘 타입이 변하지 않아" → 첫 로드 vs ROI 편집 후 차이 → ROI 편집 모드 진입/종료가 `_editingDatum` reference 교체 로직과 충돌.

- **사용자 인용 3 (Auto-reteach off):** "전부 ROI이동 혹은 사이즈만 변경해도 매번 티칭다시 하는데 리소스가 너무 많이 잡아먹는거 같아 차라리 버튼을 만들어 티칭완료 버튼이 필요하지 않을까 싶음" → btn_teachDatum 수동 트리거로 일원화 (별도 "티칭 완료" 버튼은 SPEC out-of-scope 로 분리).

- **사용자 의도 (Circle 시각화 4단계):** "초기 원 ROI를 그리면 반지름 호를 포함 작은 사각형 ROI 생성 ROI 사이즈 및 위치를 시각적으로 표시 파라미터로 수정하면서 크기 조정 / 티칭이 완료되면 실제 그려진 원도 확인이 필요 / 결국 정밀한 원을 만들어 center를 표시하는게 목적임" → 4 단계 시각화 흐름 (D-01 ~ D-06).

</specifics>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Phase 16 SPEC
- `.planning/phases/16-datum-circle-strip-redesign-algorithmtype-binding-fix/16-SPEC.md` — 5 requirements + boundaries + acceptance criteria (locked)

### Phase 15 (carry-over 컨텍스트 + 보존 결정)
- `.planning/phases/15-halcon-measurepos-measurephi-edgeselection-datumfindingservi/15-UAT.md` — Phase 15 partial sign-off, Phase 16 가 흡수해야 할 6 not_tested + 4 FAIL 시나리오
- `.planning/phases/15-halcon-measurepos-measurephi-edgeselection-datumfindingservi/15-04-SUMMARY.md` — Phase 15 carry-over 정리
- `.planning/phases/15-halcon-measurepos-measurephi-edgeselection-datumfindingservi/15-CONTEXT.md` — Phase 15 의 결정 (Phase 16 에서 보존 대상)

### Phase 14-04 (Circle 알고리즘 핵심 — 보존)
- `.planning/phases/14-datum-carry-over-circle-vertical-roi-2-out-of-range-ux/14-04-PLAN.md` — D-13 `rectPhi=thetaRad` 결정 근거
- `.planning/phases/14-datum-carry-over-circle-vertical-roi-2-out-of-range-ux/14-04-SUMMARY.md` — TryFindCircleByPolarSampling 구현 + smoke test 4 PASS 기록

### Phase 12-03 / 13-04 (PropertyGrid binding 패턴 — 본 phase 가 한 단계 더 깊게 수술)
- `.planning/phases/12-datum-circle-vertical-horizontal-intersection/12-03-SUMMARY.md` — RaisePropertyChanged("") + RefreshParamEditor 패턴 (Gap-3 fix)
- `.planning/phases/13-datum-algorithm-extensibility/13-04-SUMMARY.md` — DatumConfig per-ROI sentinel + INI 하위호환 패턴

### Phase 13-05 (시각화 패턴 참조)
- `.planning/phases/13-datum-algorithm-extensibility/13-05-SUMMARY.md` — DrawExtendedLine + RenderRawEdgePoints + DispCross batch size=6 기존 시각화 패턴 (본 phase Strip 시각화 + Raw points 시각화 시 참조)

### 코드 파일 (편집 대상)
- `WPF_Example/Halcon/Services/HalconDisplayService.cs` — RenderDatumOverlay 의 Circle 분기 재작성 (Plan 16-01)
- `WPF_Example/Custom/UI/InspectionListView.xaml.cs` — Datum 선택 핸들러 + `_editingDatum` reference 교체 로직 재설계 (Plan 16-02)
- `WPF_Example/Custom/UI/MainView.xaml.cs` — RoiMoveCompleted 의 InvokeTryTeachDatum 자동 호출 라인 삭제 (Plan 16-02)
- `WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs` — 필요 시 RaisePropertyChanged 보강 (Plan 16-02)

### 코드 파일 (읽기 전용 — 보존 대상 검증용)
- `WPF_Example/Halcon/Algorithms/VisionAlgorithmService.cs` — TryFindCircleByPolarSampling (Phase 14-04 / 15-03 결정 보존, diff 0)
- `WPF_Example/Halcon/Algorithms/DatumFindingService.cs` — Phase 15-02 9-site wiring (보존, diff 0)

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- **HalconDisplayService.RenderDatumOverlay** — 이미 LastTeachSucceeded 분기 + 알고리즘별 분기 (TwoLineIntersect / CTH / VTH) 구조 보유. Circle 분기에 Strip 시각화 + 검출 원 + Center 시각화 추가만 필요.
- **HalconDisplayService.DrawExtendedLine + RenderRawEdgePoints** (Phase 13-05) — Raw points DispCross batch 시각화 패턴 그대로 차용 (D-07).
- **DatumConfig.RaisePropertyChanged("")** + **InspectionListView.RefreshParamEditor()** (Phase 12-03) — PropertyGrid 갱신 이중 신호 패턴. AlgorithmType 까지 닿게 하려면 SelectedObject null→new force rebind 추가 (D-10).
- **MainView.HalconViewer_DatumRect/CircleCompleted** (Phase 12-03) — Datum ROI 그리기 후 PropertyGrid 즉시 갱신 패턴. PropertyGrid 변경 → 시각화 즉시 반영 (D-03) 의 역방향 패턴 참조용.

### Established Patterns
- **//YYMMDD hbk 주석 컨벤션** — 모든 변경 라인 위 (사용자 강제, D-20).
- **Allman brace style** — Halcon 모듈 (HalconDisplayService) 의 기존 스타일 유지.
- **NotifyPropertyChanged 이중 신호** — DatumConfig.RaisePropertyChanged + InspectionListView.RefreshParamEditor (Phase 12-03 Gap-3 패턴).
- **`[Browsable(false)]` + volatile HTuple 필드** — Phase 13-05 의 raw edges write-back 패턴. 본 phase 에서 Circle raw edges 도 동일 패턴으로 (이미 Phase 14-04 에서 edgeRows/edgeCols out 으로 노출됨, 추가 필드 불필요).

### Integration Points
- **InspectionListView.SelectedNodeChanged 핸들러** — Datum 선택 이벤트 진입점. `_editingDatum` reference 교체 로직 재설계 위치 (D-09).
- **MainView.RoiMoveCompleted** — ROI 이동 완료 콜백. InvokeTryTeachDatum 호출 라인 위치 (D-13).
- **MainView.btn_teachDatum_Click** — 수동 티칭 트리거. 변경 없음 (보존, D-15).
- **HalconDisplayService.RenderDatumOverlay** — RenderRawEdgePoints + DrawExtendedLine + 신규 strip 시각화 통합 호출 위치.

</code_context>

<deferred>
## Deferred Ideas

- **PropertyGrid 신규 RadialDirection 필드 (안↔바깥 사용자 선택)** — SPEC out-of-scope 명시. polarity 인자로 충분, 추가 필드 복잡도 대비 가치 낮음. 향후 사용자 요청 시 별도 phase.
- **SystemSetting AutoReteach 토글 옵션** — D-13 에서 단순화 우선으로 제외. 향후 필요 시 별도 quick task.
- **별도 "티칭 완료" 버튼** — SPEC out-of-scope 명시. btn_teachDatum 단일 트리거로 통일.
- **Datum 4번째 알고리즘** — SPEC out-of-scope.
- **btn_teachDatum 진행 progress bar** — D-15 단순화. HALCON 호출 1 sec 초과 시 향후 검토.

</deferred>

---

*Phase: 16-datum-circle-strip-redesign-algorithmtype-binding-fix*
*Auto-mode decisions logged with rationale — review and adjust before /gsd-plan-phase 16 if any decision conflicts with intent*
*Next step: /gsd-plan-phase 16 — break down into 3 plans (16-01 / 16-02 / 16-03 UAT)*
