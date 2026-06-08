# Phase 31: Datum 기준 측정 알고리즘 확장 - Context

**Gathered:** 2026-05-19
**Status:** Ready for planning

<domain>
## Phase Boundary

SOP(`Datum_정보_260511_2D`)의 신규 측정 항목을 측정 타입으로 구현하여, Datum 절대 좌표계 기준의
거리·각도·교점 측정을 Phase 23.1 의 `EdgeToLineDistanceMeasurement` 와 동일한 Shot-FAI 구조로
운용 가능하게 한다.

**범위 내:**
- 신규 측정 타입 6종 구현 — E8(원중심→Datum 거리) / D1·H5(EdgeToLineAngle, Datum A 기준 각도) /
  I9·I10(호∩라인 교점→Datum 거리) / CompoundAngle 계열(E2 각도 + E9·E10 거리) / ArcEdgeDistance(호 위 점→Datum 거리)
- Phase 23.1 carry-over 흡수: CO-23.1-01(듀얼 이미지 뷰어) + CO-23.1-02(측정 타입별 Rect ROI 버튼 일반화)
- Datum 절대좌표 주입 경로 일반화 (EdgeToLineDistance 하드코딩 제거)
- EdgeToLineDistance 의 거리 계산 로직(projection_pl 정사영 + MeasureAxis)을 공용 함수로 추출

**범위 외:**
- FAI 인벤토리 전수 검증 (SOP A1~A23/C/I/B/E/F/G/D/H 전 항목 실측) — 별도 검증 phase
- Side 카메라 검사 흐름 e2e — Phase 24 영역
- CXP 하드웨어 통합 — v1.2 (Phase 29/30)
- 신규 측정 알고리즘 외 워크플로우/Export 기능

</domain>

<decisions>
## Implementation Decisions

### 범위 & 타입 우선순위

- **D-01:** 신규 측정 타입 6종(E8 / D1·H5 / I9·I10 / CompoundAngle 계열 / ArcEdgeDistance)을 **Phase 31 한 phase 에서 전부 구현**한다. plan-phase 가 plan 단위로 분할한다. SOP 항목을 한 번에 완결하는 방향.
- **D-02:** Phase 23.1 carry-over **CO-23.1-01·02 둘 다 Phase 31 에 포함**. CO-23.1-02(측정 타입별 Rect ROI 버튼 활성화 일반화)는 신규 타입 ROI 티칭의 **선행조건** — 알고리즘 구현 전에 처리. CO-23.1-01(듀얼 이미지 뷰어)도 동일 phase.
- **D-03:** Datum 절대좌표 주입 경로 = **인터페이스 도입**. 현재 `Action_FAIMeasurement` 가 `meas as EdgeToLineDistanceMeasurement` 로 타입을 하드코딩해 `DatumOriginRow/Col`·`DatumAngleRad` 를 주입(L161~185). 신규 타입 6종 다수가 Datum 절대좌표를 필요로 하므로, "Datum 원점/각도가 필요한 측정"을 표시하는 인터페이스(예: `IDatumOriginConsumer`)를 정의하고 `Action_FAIMeasurement` 는 `meas as IDatumOriginConsumer` **한 분기**로 처리한다. 타입 추가 시 분기 수정 불필요.
- **D-04:** 거리 계산 로직 = **EdgeToLineDistance 의 검증된 계산식을 공용 함수로 추출 후 재사용**. `EdgeToLineDistanceMeasurement.TryExecute` 의 projection_pl 정사영 + MeasureAxis X/Y + 부호 처리 블록(L126~196)을 공용 헬퍼로 빼낸다. 신규 거리 타입(E8 원중심 / I9·I10 교점 / ArcEdgeDistance 호 위 점)은 **측정점만 구하고** 공용 함수를 호출한다. 검증된 로직을 타입별로 재구현하지 않는다.

### D1/H5 알고리즘 (각도 측정)

- **D-05:** D1·H5 = **신규 타입 `EdgeToLineAngle`**. ROI 1개에서 직선을 피팅하고 Datum A 기준선과의 각도(degree)를 반환한다. `EdgeToLineDistanceMeasurement` 의 "ROI 1개 + Datum" 패턴을 거리 대신 **각도**로 연장한 구조. ROADMAP 의 "신규 Datum 각도 타입" 의도를 채택. SOP 슬라이드의 "LineToLineAngle" 표기는 **HALCON 각도 연산의 의미**로 해석하며, 기존 측정 타입 `LineToLineAngle`(ROI 2개) 재사용이 아니다.
- **D-06:** D1/H5 의 각도 기준선 Datum A 공급 = **기존 Datum 메커니즘 재사용**. Datum A(Side View 자재 상단 수평부 접선)도 `DatumConfig` 로 티칭하여 `DetectedRefAngle` 을 산출하고, 측정 객체의 `DatumAngleRad` 에 주입한다(D-03 인터페이스 경로 공용). E8/I9·I10 등과 동일한 datum 주입 경로.

### SOP 불일치 해소

- **D-07:** CompoundAngle 계열 출력 = **E2 는 각도(degree), E9·E10 은 거리(mm)**. E9 = Datum C 기준, E10 = Datum B 기준. SOP 슬라이드 56/57 본문의 'distance'/'angle' 혼선은 — E2 는 MSOP 원본(`Measure the Angle`, 41.36°) 기준 각도로, E9·E10 은 슬라이드 본문의 'distance' 표기 기준 거리로 확정한다.
- **D-08:** ArcEdgeDistance(G 시리즈: G1·G2·G5~G8·G11·G12) = **신규 타입**. 측정점 P1 은 일반 에지점이 아니라 **호(arc) 위의 점**이다. ROI 에서 호를 피팅 → 호 위의 특정 점을 구함 → Datum C 기준 X 거리(mm). EdgeToLineDistance(에지 라인 중점)와 측정점 정의가 다르므로 별도 타입.

### 기하 구성 체인 데이터 모델

- **D-09:** 다단계 기하 구성(중간선·교점·중점 등)은 **measurement 내부에서 일괄 계산**한다. 사용자는 **입력 ROI 만 티칭**(E2/E9/E10: 원 ROI ×N + 라인 ROI ×N). 중간 산출물(midline / crosspoint / centerpoint)은 INI·UI 에 별도 구성 요소로 노출하지 않는다. EdgeToLineDistance 의 "입력 ROI → 내부 계산 → 결과" 패턴 연장 — 티칭 부담 최소화.
- **D-10:** 호∩라인 / 원∩라인 교점이 2개 나올 때 → **ROI 내부의 해**를 선택한다. 라인 ROI 영역 안에 들어오는 교점을 채택하며, 별도 선택 파라미터(Near/Far 등)를 PropertyGrid 에 노출하지 않는다. 사용자가 ROI 를 의도한 위치에 그리는 행위가 곧 교점 선택이 된다.
- **D-11:** E2(각도)와 E9·E10(거리)은 출력 단위가 다르므로 **출력별 별도 측정 타입으로 분리**한다. 단일 타입 + OutputMode 스위치 분기 방식을 쓰지 않는다. `MeasurementFactory` 에 각각 등록 — 타입별 결과 단위가 명확하고 판정 단위 혼선이 없다.

### Claude's Discretion (researcher/planner 위임)

- 인터페이스명(`IDatumOriginConsumer` 등) / 거리 계산 공용 함수의 위치·시그니처·명명.
- 신규 측정 타입 클래스명 (E8 / I9·I10 / CompoundAngle 계열 / ArcEdgeDistance — `EdgeToLineAngle` 외).
- 호 피팅 HALCON 연산자 선택 (3점 호 피팅 vs N점 contour 피팅 — I9/I10 arc, E2/E9/E10 circle).
- ArcEdgeDistance 의 호 위 측정점 정의 (호의 어느 지점 — 중앙/명시 각도) — researcher 가 MSOP S60~S67 재확인.
- E9/E10 과 E2 사이 기하 구성 체인의 공유 범위 (CL2~CL3 공통 부분 재사용 정도).
- 신규 타입에 `ICustomTypeDescriptor` 적용 여부 (타입별 무관 속성 숨김 — Phase 23.1 D-09 패턴).
- CO-23.1-02 ROI 버튼 일반화 구현 형태 / CO-23.1-01 듀얼 이미지 뷰어 UI 형태.
- 신규 타입 PropertyGrid Category 구성 및 ROI 필드 명명.

### Folded Todos

(없음 — phase 31 매핑 pending todo 0건)

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### SOP — 측정 사양의 source of truth

- `.planning/phases/31-datum-algorithm/31-SOP-REFERENCE.md` — Phase 31 SOP 다이제스트. Datum 좌표계 구조 / 신규 측정 타입 6종의 MSOP 절차·공차 / 전체 FAI 인벤토리 / 그레이 에어리어 / MSOP 원문 핵심 절차 인용. **원본 47MB pptx 재파싱 불필요 — 이 파일 사용.**
- 원본 pptx: `C:\Info\Doc\2.디팜스테크\02_설계\SOP\Datum_정보_260511_2D.pptx` (89슬라이드)
- 원본 MSOP PDF: `C:\Info\Doc\2.디팜스테크\02_설계\SOP\260303_Rapicity_A8.1_Z-Stopper_MSOP_RevB_변경내역 표기.pdf` (3,523줄, 충돌 시 MSOP 가 원본) — researcher 가 D-08(ArcEdgeDistance 호 위 점 정의, S60~S67) / D-07(E9·E10 거리 절차) 재확인 시 참조.

### Phase 23.1 — Datum 기준 측정의 직접 선행 (Phase 31 이 계승·확장)

- `.planning/phases/23.1-edgetolinedistance-roi/23.1-CONTEXT.md` — EdgeToLineDistance ROI 측정별 티칭 배선(D-01~D-03), 다점 치수 구조(D-04~D-07), EdgeSelection "All" 고정(D-08~D-11). Phase 31 의 ROI 티칭·다점 구조는 이를 그대로 계승.
- `.planning/phases/23-top-1-a-simul-end-to-end-2026-05-11/23-CONTEXT.md` — Datum CTH, +Y 부호, EdgeToLineDistance 정의, Rectangle ROI, 0.001mm 정밀도.
- `.planning/phases/23-top-1-a-simul-end-to-end-2026-05-11/23-RESEARCH.md` — EdgeToLineDistance 코드 패턴 / TryFitLine 시그니처 / datumTransform Y 추출 / Pitfalls (research 비활성 phase 대비 planner 필독).

### 코드 — 신규 타입의 직접 패턴 원본

- `WPF_Example/Custom/Sequence/Inspection/Measurements/EdgeToLineDistanceMeasurement.cs` — **D-04 거리 계산 공용 함수 추출 원본** (projection_pl 정사영 + MeasureAxis X/Y + 부호처리, L126~196). datum 좌표 주입 transient 필드(`DatumOriginRow/Col`·`DatumAngleRad`, L66~80). `ICustomTypeDescriptor` 동적 노출 패턴(L266~300).
- `WPF_Example/Custom/Sequence/Inspection/Measurements/LineToLineAngleMeasurement.cs` — 각도 측정 패턴 (D-05 `EdgeToLineAngle` 참고 — 단 ROI 2개가 아닌 ROI 1개 + Datum).
- `WPF_Example/Custom/Sequence/Inspection/Measurements/CircleDiameterMeasurement.cs` — 원 ROI + 원 피팅 패턴 (E8 원중심 / E2·E9·E10 원피팅 참고).
- `WPF_Example/Custom/Sequence/Inspection/MeasurementBase.cs` — 측정 추상 기반 클래스. `DatumRef`/`TryExecute` 시그니처/`EvaluateJudgement`.
- `WPF_Example/Custom/Sequence/Inspection/MeasurementFactory.cs` — 타입 문자열 → 인스턴스 매핑 (신규 타입 case 등록 지점 — D-11 각각 등록).
- `WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs` §Measure 루프 (L140~209) — **D-03 인터페이스 일반화 지점** (`meas as EdgeToLineDistanceMeasurement` 하드코딩 → `IDatumOriginConsumer` 분기).
- `WPF_Example/Halcon/Algorithms/VisionAlgorithmService.cs` — `TryFitLine` / `TryFindCircle` / `AngleLineLine` / projection 연산. D-04 공용 함수 / D-08 호 피팅 / D-10 교점 연산의 후보 위치.
- `WPF_Example/Custom/Sequence/Inspection/DynamicPropertyHelper.cs` §`FilterProperties` — ICustomTypeDescriptor 정적 헬퍼 (신규 타입 무관 속성 숨김 시 재사용).
- `WPF_Example/UI/ContentItem/MainView.xaml.cs` §`CommitRectRoi`/`CommitCircleRoi` — CO-23.1-02 ROI 버튼 일반화 지점.

### 메모리 (반드시 준수)

- `memory/feedback_halcon_measurepos_must_haves.md` — HALCON MeasurePos 라인 추출 필수 3종: strip-loop(sampleCount for-loop 누적) + measurePhi 명시 매핑 + EdgeSelection 명시.
- `memory/feedback_comment_convention.md` — 신규/변경 라인에 `//260519 hbk Phase 31` 마커 필수, Phase 20 D-12 marker stacking 패턴 준수.

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets

- **`EdgeToLineDistanceMeasurement.TryExecute` projection_pl 블록 (L126~196)** — D-04 거리 계산 공용 함수의 추출 원본. MeasureAxis X/Y 분기 + cosθ≥0 정규화 + 부호 있는 거리 성분 계산이 이미 검증됨.
- **`MeasurementBase` + `MeasurementFactory`** — 신규 타입 6종은 `MeasurementBase` 파생 + factory case 등록(D-11)으로 추가. 데이터 모델 신규 설계 불필요.
- **`DatumOriginRow/Col`·`DatumAngleRad` transient 필드 패턴** — datum 절대좌표를 측정 객체에 주입하는 경로가 EdgeToLineDistance 에 이미 존재. D-03 인터페이스는 이 3필드를 인터페이스 멤버로 일반화.
- **`ICustomTypeDescriptor` + `DynamicPropertyHelper.FilterProperties`** — 타입별 무관 속성 동적 숨김 (Phase 19/23.1 패턴).
- **`DatumConfig.DetectedRefAngle`** — Datum 기준선 각도. D-06 의 Datum A 각도 공급에 그대로 활용.
- **`InspectionRecipeManager` 동적 INI** (`MeasurementCount` + `[SHOT_s_FAI_f_MEAS_m]`) — 신규 타입 ROI/파라미터를 `ParamBase` reflection 으로 자동 직렬화. INI 측 변경 최소.

### Established Patterns

- **"ROI 1개 + Datum" 측정 패턴** — EdgeToLineDistance 가 확립. D-05 `EdgeToLineAngle`(거리→각도), D-08 ArcEdgeDistance(에지점→호 위 점) 모두 이 패턴의 변형.
- **타입 분기 vs 인터페이스** — 현재 `Action_FAIMeasurement` 는 `meas as 구체타입` 하드코딩. D-03 이 인터페이스로 전환.
- **HALCON 에러 처리** — `try/catch → return false` (CLAUDE.md).
- **주석 마커** — 신규/변경 라인 `//260519 hbk Phase 31`, 기존 마커 위 stacking.

### Integration Points

- `Action_FAIMeasurement` Measure 루프 (L161~185) — `meas as EdgeToLineDistanceMeasurement` → `IDatumOriginConsumer` 분기로 일반화 (D-03).
- `MeasurementFactory.Create` / `GetTypeNames` — 신규 타입 6종 등록 (D-11: E2·E9/E10 별도 등록).
- `VisionAlgorithmService` — D-04 거리 공용 함수 / D-08 호 피팅 / D-10 교점 연산 추가.
- `CommitRectRoi`/`CommitCircleRoi` (MainView.xaml.cs) — CO-23.1-02 측정 타입별 ROI 버튼 활성화 일반화.
- 이미지 뷰어 UI — CO-23.1-01 TeachingImagePath ≠ InspectionImagePath 듀얼 표시.

</code_context>

<specifics>
## Specific Ideas

- SOP 슬라이드와 ROADMAP/MSOP 가 충돌하는 3개 지점을 discuss 에서 확정: (1) D1/H5 = 신규 `EdgeToLineAngle`(SOP 의 "LineToLineAngle" 표기는 연산 의미), (2) CompoundAngle = E2 각도 / E9·E10 거리, (3) ArcEdgeDistance = 호 위 점 기반 신규 타입. 충돌 시 MSOP 원본이 우선.
- Phase 31 의 설계 철학 = "EdgeToLineDistance(Phase 23.1 완성품)의 구조를 측정 대상만 확장" — 새 데이터 모델·새 계산식을 만들지 않고, 검증된 로직을 공용화(D-04)하고 타입을 늘린다(D-01).
- 다단계 기하 측정(E2/E9/E10)도 사용자 입장에서는 "ROI 만 그리면 된다" — 중간 산출물은 전부 measurement 내부 처리(D-09).

</specifics>

<deferred>
## Deferred Ideas

- SOP FAI 인벤토리 전수 실측 검증 (A1~A23 / C / I / B / E / F / G / D / H 전 항목) — Phase 31 은 타입 구현 + 대표 검증, 전수 검증은 별도 phase.
- Side 카메라 검사 흐름 e2e (D1/H5 는 Side View) — Phase 24 워크플로우 영역.
- 호∩라인 교점의 사용자 선택 옵션(Near/Far) — D-10 은 ROI 내부 해 자동 선택. 향후 필요 시 backlog.
- CompoundAngle 중간 산출물의 INI/UI 노출 — D-09 는 내부 처리. 디버깅 요구 발생 시 backlog.

### Reviewed Todos (not folded)

(없음 — phase 31 매핑 todo 0건)

</deferred>

---

*Phase: 31-datum-algorithm*
*Context gathered: 2026-05-19*
