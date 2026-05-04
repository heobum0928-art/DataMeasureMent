# Phase 16: datum-circle-strip-redesign-algorithmtype-binding-fix — Specification

**Created:** 2026-04-29
**Ambiguity score:** 0.17 (gate: ≤ 0.20)
**Requirements:** 5 locked

## Goal

Datum 티칭 UX 의 두 결함을 해결한다 — (a) Circle 알고리즘의 strip 시각화가 의도(원호를 포함하는 작은 사각형 stepCount 개) 대신 잘못된 큰 사각형으로 그려지는 문제를 수정하고, (b) ROI 이동/생성 후 Datum 1↔2↔3 전환 시 PropertyGrid AlgorithmType + 실제 티칭 동작이 stale 해지는 문제를 핸들러 재설계로 해결하며, (c) 매번 자동 재티칭으로 인한 리소스 과부하를 제거하고 사용자가 btn_teachDatum 으로 명시적 트리거하도록 정책을 바꾼다.

## Background

Phase 15 UAT (2026-04-29) 에서 사용자 실측으로 두 결함이 노출됨:

- **Test 5 (Circle 시각화 결함)**: VisionAlgorithmService.TryFindCircleByPolarSampling 의 알고리즘 코드 (Phase 14-04 / 15-03 완성) 자체는 정확. 그러나 HalconDisplayService 의 Circle overlay 렌더링이 작은 strip 들 (RectL1Ratio·radius × RectL2Ratio·radius) 을 stepCount 개 표시하는 대신 ROI 원만한 큰 사각형 1개를 회전시키는 형태로 그려짐 → 사용자가 "ROI 원 자체를 Rectangle 로 바꾸어서 그 사이즈만큼 제자리에서 돌림" 으로 인식. 코드 결함 아닌 시각화 결함.
- **Test 10~12 (Datum AlgorithmType binding 결함)**: 첫 레시피 로드 시엔 Datum 1 (TwoLineIntersect), Datum 2 (CircleTwoHorizontal), Datum 3 (VerticalTwoHorizontal) 전환 시 PropertyGrid + 티칭 동작 정상. ROI 이동/생성 후엔 Datum 변경해도 PropertyGrid AlgorithmType combobox + 실제 티칭 모두 이전 알고리즘으로 stale → 데이터 소스 자체가 stale (UI binding refresh 만의 문제 아님). InspectionListView 의 Datum 선택 핸들러 / `_editingDatum` reference 교체 로직이 ROI 편집 모드 진입/종료 후 깨짐.
- **추가 발견 (Phase 13-04 후속)**: ROI 이동/사이즈 변경 시마다 RoiMoveCompleted → InvokeTryTeachDatum 자동 호출 (Phase 13-04 패턴) → HALCON edge measurement 이 매번 실행되어 리소스 과부하. 사용자가 "차라리 버튼을 만들어 티칭완료 버튼이 필요" 라고 명시.

관련 코드:
- `WPF_Example/Halcon/Services/HalconDisplayService.cs` — RenderDatumOverlay (Circle 분기)
- `WPF_Example/Custom/UI/InspectionListView.xaml.cs` — Datum 선택 핸들러, `_editingDatum`, `RefreshParamEditor`
- `WPF_Example/Custom/UI/MainView.xaml.cs` — RoiMoveCompleted, InvokeTryTeachDatum
- `WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs` — RaisePropertyChanged

보존 (out-of-scope):
- `WPF_Example/Halcon/Algorithms/VisionAlgorithmService.cs` — TryFindCircleByPolarSampling 알고리즘 코드 (Phase 14-04 D-13 `rectPhi=thetaRad` + Phase 15-03 selection 분기 그대로 유지)
- `WPF_Example/Halcon/Algorithms/DatumFindingService.cs` — Phase 15-02 의 9-site selection wiring + AppendEdgePointsFromStrip measurePhi 4-way 매핑 그대로 유지

## Requirements

1. **Circle pre-teach strip 시각화**: 원 ROI 그린 직후 strip 사각형 셋팅이 시각적으로 표시된다 (검출된 원 자체는 표시되지 않음).
   - Current: HalconDisplayService Circle overlay 가 ROI 그린 직후 작은 strip 형태 시각화를 안 함 (또는 큰 사각형으로 잘못 그림). 사용자가 PropertyGrid 의 RectL1Ratio/RectL2Ratio/PolarStepDeg 변경 시 시각적 피드백 부족.
   - Target: ROI 원 그린 직후, 원호 위의 작은 사각형 strip (RectL1Ratio·radius × RectL2Ratio·radius) 형태가 화면에 시각화됨. PropertyGrid 파라미터 변경 시 시각화 업데이트. 검출된 원 (FitCircle 결과) 은 티칭 후에만 표시.
   - Acceptance: 원 ROI 그린 직후 화면에 strip 사각형 형태가 보이며, RectL1Ratio 0.05 → 0.20 변경 시 strip 크기가 시각적으로 변함. 검출된 원은 티칭 전엔 안 보임.

2. **Circle post-teach 검출 결과 시각화**: 티칭 완료 후 검출된 원 + center cross 가 표시된다.
   - Current: 티칭 후 검출된 원/center 시각화가 일관되지 않음 (Phase 13-05 일부 raw 점 시각화는 있으나 fit 된 원 + center 표시 미흡).
   - Target: btn_teachDatum 클릭 후 LastTeachSucceeded 분기에서 검출 원 (FitCircle 결과 center+radius) + center cross + raw edge points 시각화.
   - Acceptance: 정상 ROI 로 btn_teachDatum 누르면 검출된 원 + center 가 화면에 표시되며, 화면 좌표가 PropertyGrid 의 검출 결과 좌표와 일치.

3. **Datum AlgorithmType 핸들러 핵심 수술**: ROI 이동/생성 후에도 Datum 1/2/3 전환 시 PropertyGrid AlgorithmType + 실제 티칭 동작이 모두 새 Datum 의 알고리즘을 즉시 반영한다.
   - Current: 첫 레시피 로드 시엔 Datum 전환 정상. ROI 이동/생성 후엔 Datum 변경해도 PropertyGrid AlgorithmType combobox stale + btn_teachDatum 클릭 시 이전 알고리즘으로 티칭됨 (`_editingDatum` reference 가 stale).
   - Target: InspectionListView Datum 선택 핸들러 + `_editingDatum` 교체 로직 재설계. ROI 편집 모드 진입/종료가 reference 교체에 영향을 주지 않게 함. PropertyGrid SelectedObject 재바인딩 (RaisePropertyChanged + RefreshParamEditor) 도 함께.
   - Acceptance: ROI 임의 이동 후 Datum 1 → 2 → 3 순서 클릭 시 매번 PropertyGrid AlgorithmType combobox 가 즉시 (TwoLineIntersect → CircleTwoHorizontal → VerticalTwoHorizontal) 갱신되며, 각 Datum 에서 btn_teachDatum 클릭 시 해당 알고리즘 코드 경로가 실제 실행됨 (로그/decompile 로 검증).

4. **Auto-reteach off (수동 트리거 정책)**: ROI 이동/사이즈 변경 시 자동 재티칭이 트리거되지 않는다. btn_teachDatum 수동 클릭으로만 검출이 갱신된다.
   - Current: MainView RoiMoveCompleted 가 InvokeTryTeachDatum 을 자동 호출 (Phase 13-04 패턴). ROI 한 번 움직일 때마다 HALCON edge measurement 실행 → 사용자가 리소스 과부하 호소.
   - Target: RoiMoveCompleted 의 자동 InvokeTryTeachDatum 호출 제거. ROI 이동 후엔 검출 결과가 stale 인 상태로 유지되며, 사용자가 btn_teachDatum 을 수동 클릭해야 갱신됨. 시각화 (LastTeachSucceeded 분기) 는 명시적 재티칭 전까지 이전 결과를 보여주거나 stale 표시.
   - Acceptance: ROI 임의 이동 5회 시 HALCON edge measurement 호출 0회 (Logging 로그 확인 — `MeasurePos` 또는 `TryFindLine` trace 0건 추가됨). btn_teachDatum 1회 클릭 시 1회 실행.

5. **UAT — Phase 15 carry-over 6 시나리오 흡수 + Phase 16 신규 시나리오 검증**: 사용자 육안 검증으로 Phase 15 의 not_tested 6건 + Phase 16 신규 4건 모두 PASS.
   - Current: Phase 15 UAT 에서 Test 5/10/11/12 FAIL, Test 6/7/8/13/14/15 not_tested.
   - Target: Phase 16 UAT 에서 Phase 15 Test 5/6/7/8/10/11/12/13/14/15 모두 PASS + 신규 Test (pre-teach strip 시각화, post-teach 검출 원 시각화, AlgorithmType 핸들러 정상, Auto-reteach off 동작) PASS.
   - Acceptance: 16-UAT.md 작성, status: signed_off, passed ≥ 14, failed = 0.

## Boundaries

**In scope:**
- HalconDisplayService Circle overlay 재작성 — pre-teach strip 시각화 + post-teach 검출 원/center 시각화
- InspectionListView Datum 선택 핸들러 + `_editingDatum` 교체 로직 재설계
- DatumConfig 의 PropertyGrid 갱신 패턴 (RaisePropertyChanged + RefreshParamEditor) 활용
- MainView RoiMoveCompleted 자동 재티칭 트리거 제거
- btn_teachDatum 수동 트리거 보존
- Phase 15 의 모든 코드 결정 보존 (measurePhi 4-way / EdgeSelection 9-site wiring / Circle selection 인자화)
- 빌드 PASS, 신규 warning 0
- 16-UAT.md 작성 + 사용자 사인오프

**Out of scope:**
- VisionAlgorithmService.TryFindCircleByPolarSampling 알고리즘 코드 변경 — Phase 14-04 D-13 `rectPhi=thetaRad` + Phase 15-03 selection 분기 보존 (사용자 확인: 시각화만 결함)
- DatumConfig 데이터 모델 신규 필드 추가 — Phase 15-01 의 EdgeSelection 6 ROI 재사용
- DatumFindingService.cs 변경 — Phase 15-02 결정 보존
- AppendEdgePointsFromStrip / TryFindLine / TryExtractEdgePoints 변경 — Phase 15-02 결정 보존
- INI 포맷 변경 — 기존 레시피 무손실 하위호환 유지
- 새 PropertyGrid 필드 추가 (안↔바깥 방향 옵션 등) — 사용자 의도가 polarity 인자로 충분, 추가 필드 불필요
- Datum 4번째 알고리즘 추가 — 현 3개 (TwoLineIntersect / CTH / VTH) 유지
- 별도 "티칭 완료" 버튼 신설 — btn_teachDatum 단일 트리거로 통일 (단순화)

**Reason for exclusions:**
- 알고리즘 코드 보존: Phase 14-04 의 4-점 phi smoke test PASS + Phase 15 의 4 EdgeDirection PASS 로 알고리즘은 검증됨. 시각화/UI 결함만 별도 phase 로 처리.
- INI 포맷 보존: Phase 15 와 동일 — 기존 사용자 레시피 손실 방지.
- 추가 PropertyGrid 필드 미도입: 사용자가 "안↔바깥" 을 polarity (light↔dark) 의미로 사용 중. 별도 RadialDirection 필드는 복잡도만 증가.

## Constraints

- HALCON 24.11 + .NET Framework 4.8 + WPF — 변경 불가
- SystemHandler 싱글턴 + SequenceBase/ActionBase 패턴 유지
- INI 레시피 하위호환 — 기존 레시피 로드 후 동작 동일 (자동 재티칭만 사용자 명시적 트리거로 변경)
- Phase 14-04 / 15-01 / 15-02 / 15-03 의 모든 코드 결정 보존 (Out-of-scope 명시)
- 각 코드 수정 라인 위에 `//YYMMDD hbk <reason>` 주석 (사용자 강제 컨벤션)
- 빌드: msbuild Debug/x64 PASS, 신규 warning 0
- 실 데이터 UAT 필수 — Halcon + 카메라 의존, SIMUL 만으로는 검증 불가

## Acceptance Criteria

- [ ] msbuild Debug/x64 PASS, 신규 warning 0 (DatumFindingService.cs / VisionAlgorithmService.cs / DatumConfig.cs 무수정 검증)
- [ ] 원 ROI 그린 직후 화면에 strip 사각형 형태 (작은 사각형 stepCount 개 또는 대표 형태) 가 보임
- [ ] PropertyGrid 의 RectL1Ratio 0.05 → 0.20 변경 시 strip 시각화 크기 변함
- [ ] btn_teachDatum 클릭 후 검출된 원 + center cross 가 화면에 표시됨
- [ ] ROI 이동 후 Datum 1 → 2 → 3 순서 클릭 시 PropertyGrid AlgorithmType combobox 가 즉시 (TwoLineIntersect → CircleTwoHorizontal → VerticalTwoHorizontal) 갱신됨
- [ ] ROI 이동 후 Datum 2 클릭 → btn_teachDatum 클릭 시 CircleTwoHorizontal 코드 경로가 실제 실행됨 (이전 알고리즘 stale 호출 0)
- [ ] ROI 임의 이동 5회 시 HALCON edge measurement 자동 호출 0회 (logging trace 검증)
- [ ] btn_teachDatum 1회 클릭 시 1회 실행
- [ ] 16-UAT.md 작성 + status: signed_off + passed ≥ 14 + failed = 0
- [ ] Phase 15 UAT not_tested 6 시나리오 흡수 검증 (Test 6/7/8/13/14/15 PASS)

## Ambiguity Report

| Dimension          | Score | Min  | Status | Notes                                         |
|--------------------|-------|------|--------|-----------------------------------------------|
| Goal Clarity       | 0.85  | 0.75 | ✓      | 시각화 결함 + binding 결함 + auto-reteach off 명확 |
| Boundary Clarity   | 0.85  | 0.70 | ✓      | 알고리즘 보존 + 신규 필드 미도입 + 별도 버튼 미도입 명시 |
| Constraint Clarity | 0.75  | 0.65 | ✓      | INI 하위호환 + Phase 14-04/15 결정 보존 + 빌드 게이트 |
| Acceptance Criteria| 0.85  | 0.70 | ✓      | 10 pass/fail 체크박스 + UAT 시나리오 흡수 명시       |
| **Ambiguity**      | 0.17  | ≤0.20| ✓      | 게이트 통과                                    |

Status: ✓ = met minimum, ⚠ = below minimum (planner treats as assumption)

## Interview Log

| Round | Perspective     | Question summary                                  | Decision locked                                                |
|-------|-----------------|-------------------------------------------------|----------------------------------------------------------------|
| 1     | Researcher      | Circle 결함이 알고리즘인지 시각화인지?              | 시각화만 — HalconDisplayService Circle overlay 재작성, 알고리즘 코드 보존 |
| 1     | Researcher      | AlgorithmType binding 결함 범위는?                 | 데이터 + UI 둘 다 stale — 핸들러 핵심 수술 필요                 |
| 2     | Boundary Keeper | 시각화 재작성 범위?                                | 원 자체 (티칭 후만) + Strip 사각형 + Raw 에지점 + ROI 경계         |
| 2     | Boundary Keeper | Binding fix 깊이?                                | 핵심 수술 — Datum 선택 핸들러 / `_editingDatum` 교체 로직 재설계 |
| 2     | Boundary Keeper | Plan 구조?                                       | 2 plans + UAT (16-01 / 16-02 / 16-03 UAT)                    |
| 3     | Failure Analyst | Pre-teach strip 시각화 모드?                       | 초기 strip 셋팅 시각화만, 검출 원은 티칭 후. + Auto-reteach off 신규 요구사항 발견 |
| 3     | Failure Analyst | AlgorithmType 변경 후 자동 재티칭?                 | 수동 (btn_teachDatum 클릭 필요) — 리소스 과부하 회피             |
| 3     | Failure Analyst | Auto-reteach off 위치?                          | 16-02 안에 합치기 (동일 핸들러 영역)                            |

---

*Phase: 16-datum-circle-strip-redesign-algorithmtype-binding-fix*
*Spec created: 2026-04-29*
*Next step: /gsd-discuss-phase 16 — implementation decisions (HalconDisplayService 시각화 패턴 / 핸들러 재설계 디테일 / btn_teachDatum 수동 트리거 UI 흐름)*
