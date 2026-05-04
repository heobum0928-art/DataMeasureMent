# Phase 14: Datum carry-over (Circle 알고리즘 재설계 + Vertical 파라미터 그룹 + ROI 이동 회귀 + 2종 알고리즘 정상화 + out-of-range UX 게이트) — Specification

**Created:** 2026-04-26
**Ambiguity score:** 0.15 (gate ≤ 0.20)
**Requirements:** 5 locked

## Goal

Phase 13 UAT carry-over 5건을 5 sub-phase 로 처리하여 Datum 3 알고리즘(TwoLineIntersect / CircleTwoHorizontal / VerticalTwoHorizontal) 모두에서 (a) Circle ROI 이동/resize 가 검출 결과 갱신까지 이어지고, (b) Vertical ROI 가 의미적으로 별도 파라미터 그룹으로 노출되며, (c) Circle 검출이 360° polar sampling 방식으로 raw 점까지 시각화되고, (d) 비정상 두 라인 각도가 모든 알고리즘에서 fail 라벨로 거부되며, (e) btn_testFindDatum 으로 3 알고리즘 모두 정상 시행이 검증된다.

## Background

Phase 13 UAT (2026-04-26, `13-UAT.md`) — 8 시나리오 중 5 PASS, 2 issue (major), 1 blocked. Test 6 (minor) 만 13-06+13-07 hot-fix 로 closure. 잔여 5건은 코드/데이터 모델 변경이 필요해 routing decision (commit `d9b5cc8`) 으로 Phase 14 carry-over.

코드 현황(2026-04-26):
- `DatumConfig` (`WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs`) 5 ROI 그룹(Line1/Line2/Circle/Horizontal_A/Horizontal_B) × 6 edge 필드. **`Vertical_*` 그룹 없음** — `TryTeachVerticalTwoHorizontal` (`DatumFindingService.cs:486-503`) 가 `config.Line1_*` 슬롯을 재사용. 사용자 PropertyGrid 에서 "Line1" 카테고리만 보여 Vertical 파라미터 의미 불명 (Test 1 issue 2 직접 원인)
- `VisionAlgorithmService.TryFindCircle` (`VisionAlgorithmService.cs:120-200`) 는 `EdgesSubPix` + `FitCircleContourXld` 사용, raw 에지점 미반환 → 13-05 시각화에서 Circle ROI 노란 raw 점이 빈 HTuple 로 비어있음 (D-VIZ-03 carry-over 직접 원인)
- `MainResultViewerControl.HitTestOneRoi` (`MainResultViewerControl.xaml.cs:332-348`) 는 Circle 분기 정상 / `MainView.PublishDatumRoiCandidates` (`MainView.xaml.cs:711-718`) 도 CircleTwoHorizontal 케이스 정상 / `ApplyDatumRoiDelta` (`MainView.xaml.cs:587-588`) 도 Datum.Circle 분기 존재 → Circle 이동 wiring 자체는 살아있는데 사용자 시점에서 (1) 이동 후 자동 재티칭 결과 미반영, (2) Edit 모드 핸들 활성화는 되나 resize 동작 X (Test 1 issue 1)
- `ValidateHorizontalVerticalAngles` (`DatumFindingService.cs`) 는 CircleTwoHorizontal/VerticalTwoHorizontal 만 호출. TwoLineIntersect 에는 게이트 없음 (Phase 13 D-12 의도) — 사용자 UX 기대(어떤 알고리즘이든 휘어졌으면 fail) 와 불일치 (Test 2 blocked → UX 갭)
- UAT Test 3: btn_testFindDatum 으로 TwoLineIntersect 만 정상 시행. CircleTwoHorizontal/VerticalTwoHorizontal 은 정상 시행 X (Vertical 파라미터 누락 + Circle raw 점 미반환 + Phi 와이어링 의심 등 복합 원인)

## Requirements

1. **Circle ROI 이동/resize 회귀 fix**: 티칭 완료된 Circle ROI 가 드래그 이동/resize 되면 자동 재티칭이 발동되어 검출 결과가 즉시 갱신된다.
   - Current: 이동/오버레이 갱신 됨, **자동 재티칭 미발동** (사용자 시점 "에지 추출 안 보임"); Edit 모드 4 핸들(N/S/E/W) 활성화는 되나 **resize 동작 X**
   - Target: (a) Circle ROI 드래그 이동 시 자동 재티칭 발동되어 검출 원/raw 점 즉시 갱신, (b) Edit 모드 N/S/E/W 핸들 드래그로 반경 변경 가능 + 변경 후 자동 재티칭 발동
   - Acceptance: SIMUL_MODE 에서 CircleTwoHorizontal Datum 티칭 후 (1) Circle ROI 내부 드래그 이동 → 검출 원/raw 점 새 위치 반영, (2) E 핸들 드래그 → 반경 변경 + 검출 원 새 반경 반영. 두 시나리오 사용자 육안 PASS

2. **TwoLineIntersect 각도 out-of-range 게이트**: TwoLineIntersect 알고리즘에서도 두 라인 사이 각도가 사용자 정의 임계각을 벗어나면 fail 라벨로 거부한다.
   - Current: TwoLineIntersect 는 두 라인 검출 성공만 보고 각도 무검증 (Phase 13 D-12 의도) — 휘어진 라인이어도 datum origin 산출
   - Target: `DatumConfig` 신규 PropertyGrid 필드 `TwoLineAngleToleranceDeg` (default 10°, 범위 0~45°, 0=off) 추가. `TryTeachTwoLineIntersect` 끝부분에서 두 라인 각도 차의 절댓값이 90°±N° 를 벗어나면 `LastTeachSucceeded=false` + fail 라벨 텍스트 `"Two-line angle out of range: <측정값>° (expected 90°±<N>°)"`. INI 하위호환: 미존재 시 default 10° 적용
   - Acceptance: SIMUL_MODE 에서 (1) 정상 90° 두 라인 → PASS, (2) 두 라인 각도 60° + N=10° → fail 라벨 위 문구 표시 + LastTeachSucceeded=false, (3) N=0 → 어떤 각도여도 PASS (게이트 off)

3. **Vertical 에지 파라미터 그룹 신설**: VerticalTwoHorizontal 알고리즘의 수직 ROI 가 의미적으로 별도 그룹(`Vertical_*`)으로 분리되고, PropertyGrid 에서 "Vertical" 카테고리로 노출되며, 기존 INI 는 자동 마이그레이션된다.
   - Current: `TryTeachVerticalTwoHorizontal` 가 `config.Line1_*` 슬롯 재사용. 사용자가 PropertyGrid 에서 "Line1_EdgeThreshold" 항목을 봐야 수직 ROI 파라미터를 튜닝 → 의미 혼동 + Test 1 issue 2 "Vertical 파라미터들이 보이지 않음"
   - Target: `DatumConfig` 에 신규 13 필드 추가:
     - geometry 5: `Vertical_Row/Col/Phi/Length1/Length2`
     - edge 6: `Vertical_EdgeThreshold/Sigma/EdgeDirection/EdgeSampleCount/EdgeTrimCount/EdgePolarity`
     - raw 2: `Vertical_DetectedEdgeRows/Cols` (HTuple, [Browsable(false)])
     - PropertyGrid Category("Vertical") 부착, 기존 5 ROI 그룹 패턴 동일.
     `EnsurePerRoiDefaults` 에 Vertical 분기 추가 (sentinel=0/"" 일 때 글로벌 fallback 복제). `TryTeachVerticalTwoHorizontal` 의 `config.Line1_*` 참조를 `config.Vertical_*` 로 교체. `PublishDatumRoiCandidates` VerticalTwoHorizontal 케이스에서 RoiId `"Datum.Vertical"` 발행 + `ApplyDatumRoiDelta` / `ClearDatumRoiFields` 에 Datum.Vertical 분기 추가. INI 하위호환: 기존 INI 의 Line1_* 값을 Vertical_* sentinel 일 때 1회 복사 (idempotent)
   - Acceptance: (1) 새 DatumConfig 의 PropertyGrid 에 Vertical 카테고리 6 edge 필드 노출, (2) 기존 Phase 12/13 VerticalTwoHorizontal INI 로드 시 Vertical_* 값이 Line1_* 와 동일하게 자동 채워짐, (3) VerticalTwoHorizontal 티칭 후 PropertyGrid `Vertical_EdgeThreshold` 변경 → 자동 재티칭 → 검출 라인 변화 SIMUL_MODE 시각 확인

4. **Circle 신규 polar-sampling 알고리즘**: VisionAlgorithmService 에 360° polar sampling 방식의 신규 Circle 검출 메서드를 추가하고, raw 에지점을 반환한다.
   - Current: `TryFindCircle` 는 `EdgesSubPix` + `FitCircleContourXld` 만 사용하고 raw 에지점 미반환 → Circle ROI 노란 raw 점이 빈 HTuple
   - Target: 신규 메서드 `TryFindCircleByPolarSampling(image, centerRow, centerCol, radius, stepDeg, rectL1Ratio, rectL2Ratio, sigma, threshold, polarity, datumTransform, out foundRow, out foundCol, out foundRadius, out HTuple edgeRows, out HTuple edgeCols, out error)` 추가.
     동작: 시작각 0°(오른쪽) 부터 **CCW** 로 stepDeg 씩 회전, 각 각도 θ 에서:
     - rect 중심 = (centerRow + radius·sin(θ), centerCol + radius·cos(θ))
     - rect phi = θ (반경 방향이 rect 의 length1 축)
     - halfL1 = radius · rectL1Ratio, halfL2 = radius · rectL2Ratio
     - GenMeasureRectangle2 → MeasurePos → 첫 에지점 1개 추출 (없으면 skip)
     - (360/stepDeg) 점 누적 → FitCircleContourXld 로 center+radius 산출
     PropertyGrid 노출 파라미터 (DatumConfig 의 Circle 그룹 확장):
     - `Circle_PolarStepDeg` (default 10°, 범위 1~30°)
     - `Circle_RectL1Ratio` (default 0.05)
     - `Circle_RectL2Ratio` (default 0.05)
     기존 `TryFindCircle` (legacy) 은 그대로 유지 (`CircleDiameterMeasurement.cs:49` 호환 — additive 만)
   - Acceptance: (1) SIMUL_MODE 에서 step=10° 호출 시 36 점 raw 점이 360° 분포로 캔버스에 노란 십자 표시, (2) step=1° 변경 시 360 점 표시 (시각적으로 거의 연속 원), (3) FitCircleContourXld center 가 ground truth(±2px) 와 일치, (4) `CircleDiameterMeasurement` FAI 측정 회귀 없음

5. **CircleTwoHorizontal / VerticalTwoHorizontal btn_testFindDatum 정상화**: btn_testFindDatum 으로 3 알고리즘(TwoLineIntersect / CircleTwoHorizontal / VerticalTwoHorizontal) 모두에서 검출이 정상 시행되어 RefOrigin 이 산출된다.
   - Current: UAT Test 3 — TwoLineIntersect 만 정상 시행. Circle/CircleTwoHorizontal/VerticalTwoHorizontal 은 정상 시행 X (Vertical 파라미터 누락 + Circle raw 점 미반환 + 13-04 strip-loop 패턴 오적용 / Phi 와이어링 결함 의심)
   - Target: 14-03 (Vertical 그룹) + 14-04 (Circle 신규 알고리즘) 통합 후 `TryFindDatum` 의 CircleTwoHorizontal/VerticalTwoHorizontal 경로 재검증. CircleTwoHorizontal 의 Circle 검출은 신규 `TryFindCircleByPolarSampling` 으로 교체. 발견된 결함 (Phi 와이어링 / per-ROI 파라미터 누락 / strip-loop 오적용 등) fix
   - Acceptance: SIMUL_MODE btn_testFindDatum 에서 (1) TwoLineIntersect 정상 시행 (LimeGreen `"TryFind OK — RefOrigin=(...), Angle=... rad"` + 주황 십자) — 회귀 없음, (2) CircleTwoHorizontal 정상 시행 (LimeGreen RefOrigin + CircleCenter + Radius 라벨 + 주황 십자 + Circle raw 점), (3) VerticalTwoHorizontal 정상 시행 (LimeGreen RefOrigin + 주황 십자 + Vertical/HorizA/HorizB raw 점)

## Boundaries

**In scope:**
- Circle ROI 이동 후 자동 재티칭 발동 wiring fix (14-01)
- Circle ROI Edit 모드 N/S/E/W 핸들 resize 동작 + 자동 재티칭 (14-01)
- TwoLineIntersect 두 라인 각도 게이트 + PropertyGrid `TwoLineAngleToleranceDeg` 필드 (14-02)
- DatumConfig Vertical 그룹 13 필드 신설 (5 geometry + 6 edge + 2 raw) + PropertyGrid Category + INI 마이그레이션 (14-03)
- DatumFindingService.TryTeachVerticalTwoHorizontal 의 Line1_* → Vertical_* 슬롯 교체 (14-03)
- MainView PublishDatumRoiCandidates / ApplyDatumRoiDelta / ClearDatumRoiFields 의 Datum.Vertical 분기 추가 (14-03)
- VisionAlgorithmService 신규 `TryFindCircleByPolarSampling` 메서드 + 3 PropertyGrid 파라미터 (Circle_PolarStepDeg / RectL1Ratio / RectL2Ratio) (14-04)
- DatumFindingService.TryTeachCircleTwoHorizontal 의 Circle 검출 호출을 신규 polar sampling 으로 교체 (14-05)
- btn_testFindDatum 으로 3 알고리즘 PASS 검증 + 발견된 결함 fix (14-05)

**Out of scope:**
- FAI 측정 경로 변경 — Datum 변경과 독립 유지 (Phase 13 ALG-04 회귀 없음 보장)
- ROI Edit 모드 전반 재설계 (Polygon 이동 / Rect 회귀 버그 / Edit 모드 진입 UX) — 별도 백로그 (memory: project_roi_edit_mode_deferred). Phase 14 는 **Circle resize 한 케이스만** 좁혀서 처리
- `CircleDiameterMeasurement` 의 legacy `TryFindCircle` 호출 변경 — 시그니처 보존 / 회귀 차단
- Halcon `MeasureCircle` 등 SDK 빌트인 사용 — 사용자 지정 회전+사각형ROI 방식만 채택
- Strategy 패턴 / 알고리즘 클래스 추상화 리팩터 — Phase 13 deferred 결정 그대로 유지
- TCP / 시퀀스 / 외부 통신 영향 — Phase 14 는 UI / 티칭 / 데이터 모델 한정

## Constraints

- 기존 INI 레시피(Phase 4/11/12/13) 100% 하위호환 — `EnsurePerRoiDefaults` idempotent 마이그레이션 패턴 연장 (sentinel=0/"" 검출 시 1회 복사). Vertical 그룹은 sentinel 시 Line1_* 에서 복사
- DatumConfig PropertyGrid 36+ auto-property 패턴 유지 — Phase 13-06+13-07 자동 재티칭 트리거(`NotifyDatumParamMaybeChanged`) 와 충돌 없음. Vertical_* 신규 필드도 `IsConfigured` 가드 안에서 동일하게 동작
- C# 7.2 / .NET 4.8 / Halcon 24.11 / WPF — `unsafe` / 신규 NuGet 추가 금지
- 코드 스타일 file-local 일관성 (DatumConfig/DatumFindingService 는 Allman, MainView 는 K&R)
- additive 변경만 — 기존 `DatumConfig` / `DatumFindingService` / `MainView` 공용 시그니처 유지 (Phase 13 success criterion 6 연장). `TryFindCircleByPolarSampling` 은 신규 메서드, `TryFindCircle` 은 그대로
- Phase 13-07 cascade/recovery hot-fix (`Dispatcher.BeginInvoke(Background)` defer + `IsConfigured`-only 가드) 패턴 유지

## Acceptance Criteria

- [ ] Circle ROI 드래그 이동 → 검출 원/노란 raw 점 새 위치 반영 (SIMUL_MODE 사용자 육안 PASS)
- [ ] Circle ROI Edit 모드 E 핸들 드래그 → 반경 변경 + 검출 원 새 반경 반영
- [ ] Circle ROI N/S/W 핸들도 동일하게 반경 변경 동작
- [ ] TwoLineIntersect 두 라인 각도 60° + N=10° 입력 → fail 라벨 `"Two-line angle out of range: ...° (expected 90°±10°)"` 표시 + LastTeachSucceeded=false
- [ ] TwoLineIntersect N=0 입력 → 게이트 off, 어떤 각도여도 PASS
- [ ] DatumConfig PropertyGrid Vertical 카테고리 6 edge 필드 노출 (VerticalTwoHorizontal 알고리즘 선택 시)
- [ ] 기존 Phase 12/13 VerticalTwoHorizontal INI 로드 시 Vertical_* 값이 Line1_* 와 동일 값으로 자동 채워짐
- [ ] VerticalTwoHorizontal 티칭 후 Vertical_EdgeThreshold 변경 → 자동 재티칭 발동 + 검출 라인 시각 변화
- [ ] CircleTwoHorizontal Circle ROI 노란 raw 점 36 개(step=10°) 360° 분포 표시
- [ ] Circle_PolarStepDeg=1 변경 → raw 점 360 개 표시
- [ ] CircleTwoHorizontal 신규 polar sampling center 가 기존 알고리즘 center 와 ±2px 일치 (ground truth)
- [ ] btn_testFindDatum: TwoLineIntersect 정상 시행 (LimeGreen 좌표 + 주황 십자) — 회귀 없음
- [ ] btn_testFindDatum: CircleTwoHorizontal 정상 시행 (RefOrigin + CircleCenter + Radius 라벨 + 주황 십자 + Circle raw 점)
- [ ] btn_testFindDatum: VerticalTwoHorizontal 정상 시행 (RefOrigin + 주황 십자 + Vertical/HorizA/HorizB raw 점)
- [ ] FAI 측정 경로 회귀 없음 — `CircleDiameterMeasurement` 포함 (Phase 13 Test 8 시나리오 PASS 유지)
- [ ] 기존 Phase 4/11/12/13 INI 레시피 로드 무오류

## Ambiguity Report

| Dimension          | Score | Min  | Status | Notes                                                          |
|--------------------|-------|------|--------|----------------------------------------------------------------|
| Goal Clarity       | 0.92  | 0.75 | ✓      | 5 sub-phase 목적 명확, carry-over 5건 1:1 매핑                |
| Boundary Clarity   | 0.85  | 0.70 | ✓      | sub-phase 분할 14-01..14-05 락, in/out 명시                    |
| Constraint Clarity | 0.80  | 0.65 | ✓      | additive 만 + INI 하위호환 패턴 13-04/13-05 연장               |
| Acceptance Criteria| 0.80  | 0.70 | ✓      | 15 pass/fail 항목, SIMUL_MODE 육안 + 자동화 가능 분리          |
| **Ambiguity**      | 0.15  | ≤0.20| ✓      |                                                                |

## Sub-phase Plan Skeleton (locked at SPEC time, plan-phase 에서 정밀화)

| sub-phase | Title                                      | Maps to Req | Depends |
|-----------|--------------------------------------------|-------------|---------|
| 14-01     | Circle ROI 이동 자동 재티칭 + resize fix   | 1           | —       |
| 14-02     | TwoLineIntersect 각도 out-of-range 게이트  | 2           | —       |
| 14-03     | Vertical 에지 파라미터 그룹 신설           | 3           | —       |
| 14-04     | Circle polar-sampling 신규 알고리즘        | 4           | —       |
| 14-05     | CircleTwoH / VerticalTwoH 정상화 + 결함 fix | 5           | 14-03, 14-04 |

## Interview Log

| Round | Perspective         | Question summary                                                       | Decision locked                                                                                                  |
|-------|---------------------|------------------------------------------------------------------------|------------------------------------------------------------------------------------------------------------------|
| 1     | Researcher          | Q1 Circle ROI 이동 회귀 현상                                          | 이동/오버레이는 됨, **자동 재티칭 미발동** (사용자 시점 에지 추출 안 보임), Edit 핸들 활성화는 되나 **resize 미동작** |
| 1     | Researcher          | Q2 Vertical 파라미터 그룹 방식                                        | (a) 신규 6 edge + 5 geometry + 2 raw 필드 셋 추가 + INI 마이그레이션                                              |
| 1     | Researcher          | Q3 Circle 알고리즘 재설계 핵심                                        | center+radius 기점 360° 회전, 각 각도에 작은 사각형 ROI → GenMeasureRectangle+MeasurePos 에지 1점, 누적 후 FitCircle |
| 2     | Researcher+Simplifier | Q4 Circle 신규 알고리즘 옵션                                          | step default 10° / 범위 1~30°, 시작각 0° 고정, 회전 CCW, 사각형 ROI = 반경의 5%                                 |
| 2     | Simplifier          | Q5 CircleTwoH/VerticalTwoH 정상 시행 책임 범위                        | (a) 조사+fix 풀스코프, btn_testFindDatum 3 알고리즘 PASS 가 AC                                                   |
| 2     | Simplifier          | Q6 TwoLineIntersect 각도 게이트 적용 여부                             | (a) Yes 적용                                                                                                     |
| 3     | Boundary Keeper     | Q7 Circle 사각형 ROI L1/L2 분해                                       | (d) PropertyGrid 입력 default 5%/5% (`Circle_RectL1Ratio` / `Circle_RectL2Ratio`)                                |
| 3     | Boundary Keeper     | Q8 TwoLineIntersect 게이트 N 값                                       | (d) PropertyGrid 입력 default 10° (`TwoLineAngleToleranceDeg`, 0=off)                                            |
| 3     | Boundary Keeper     | Q9 Sub-phase 분할 확정                                                | (a) 5 sub-phase 그대로 (14-01 ROI회귀, 14-02 게이트, 14-03 Vertical, 14-04 Circle algo, 14-05 정상화)            |

---

*Phase: 14-datum-carry-over-circle-vertical-roi-2-out-of-range-ux*
*Spec created: 2026-04-26*
*Next step: /gsd-discuss-phase 14 — implementation decisions (sub-phase 별 plan 분해, RoiId 마이그레이션 정책, polar sampling 좌표계 부호 컨벤션, Circle resize 핸들 hit-test 추가, INI 마이그레이션 idempotency 가드)*
