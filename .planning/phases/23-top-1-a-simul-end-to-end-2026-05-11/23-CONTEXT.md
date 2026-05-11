# Phase 23: Top #1 A시리즈 Simul end-to-end — Context

**Gathered:** 2026-05-11
**Status:** Ready for planning

<domain>
## Phase Boundary

Top Fixture #1 의 Simul 이미지 1장으로 다음 흐름이 오류 없이 완주한다:

1. 이미지 로드 (InspectionImagePath, Phase 22 인프라 사용)
2. Datum B/C 자동 찾기 (B = X축 horizontal 기준선, C = Y축 vertical 기준선)
3. A1~A5 EdgeToLineDistance 측정 (5개, mm 단위, +Y 부호 규약)
4. 공차 판정 → InspectionListView TreeView 펼침 + strip 5개 동시 색상 표시 (녹=PASS, 적=FAIL)
5. 동일 구조로 A6~A23 INI 추가가 코드 수정 없이 가능 (UAT 에서 A6 1개로 검증)

**범위 내:**
- 신규 `EdgeToLineDistance` Measurement 클래스 (MeasurementFactory 7번째)
- Datum B/C INI 설정 (Top Fixture #1 단독, 알고리즘 종류는 researcher 가 PPT 확인 후 결정)
- TeachingImagePath 자동 로드 구현 (Phase 22 carry-over)
- FAI 추가 채널 검증: INI 직접 편집 + UI 'Add FAI' 버튼 두 경로
- 5개 strip 동시 렌더링 (TreeView 펼침 상태)
- msbuild Debug/x64 PASS + Phase 21 baseline (6 warning occurrences) 유지

**범위 외:**
- Top Fixture #2~ 또는 Bottom Fixture 의 A시리즈 (구조 동일 가정, Phase 23 검증 안 함)
- Polygon/Circle ROI 형태 (Rectangle 만 지원)
- 결과 이미지 리뷰어 / xlsx export (Phase 25)
- 50회 반복도 통계 (Phase 25 OUT-03)
- TCP 응답 분기 (Phase 24 WF-02)
- Y축 외 다른 Datum 축 (X축 거리) 측정

</domain>

<decisions>
## Implementation Decisions

### Datum B/C 구성

- **D-01:** Datum 표현 방식은 **researcher 가 PPT(Datum_정보_260511_2D) 확인 후 결정**. 후보 — (a) Datum 1개 = CTH (B1 홀 Circle + 2 horizontal tangent line, origin=circle center, Y축 자동 도출), (b) Datum 2개 = B(TLI 또는 단일 horizontal) + C(CTH 또는 단일 vertical). researcher 가 PPT 매핑 후 lock-in.
- **D-02:** Y측정 부호 규약 = **+Y (Datum B 위쪽이 양수, 공학 표준)**. HALCON image row 방향(아래쪽 양수)과 반대일 수 있음 — datumTransform 적용 시 부호 반전 확인.
- **D-03:** 이번 Phase 의 Fixture 범위 = **Top Fixture #1 단독**. #2~ 및 Bottom Fixture 는 SC#4 확장성 검증 대상 아님 (구조 동일 가정만).
- **D-04:** **TeachingImagePath 자동 로드 구현** (Phase 22 carry-over) — Datum 첫기 단계에서 `DatumConfig.TeachingImagePath` 가 비어있지 않으면 그 이미지를 재티칭 기준으로 로드하고, 비어있으면 기존 `ShotConfig.SimulImagePath` (= InspectionImagePath) 폴백 (하위호환). 두 경로 동일 파일도 회귀 0 (Phase 22 SC#3 유지).
- **D-05:** 좌표계 표기 정정 = **Datum B = X축 (horizontal line), Datum C = Y축 (vertical line)**. ROADMAP 문구 "Y축 기준선 / X축 기준선" 은 "Y측정 기준선 / X측정 기준선" 의미였으며 CONTEXT/PLAN/SUMMARY 에는 그림 기준 명명으로 통일.

### A1~A5 측정 알고리즘 (ALG-01 잠금)

- **D-06:** ALG-01 정의 = **신규 `EdgeToLineDistance` Measurement 클래스 추가**. `MeasurementFactory` 의 7번째 알고리즘. Point ROI 1개만 fit, datumTransform 적용 후 추출된 Y좌표를 그대로 "Datum B 까지 Y방향 거리" 로 리턴. 별도 Line ROI fit 단계 없음 — Datum B 가 origin Y=0 이라는 점이 알고리즘에 내재.
- **D-07:** Edge 파라미터 노출 = **PointToLineDistance 와 동일 6종** (EdgeThreshold, Sigma, EdgeSampleCount, EdgeTrimCount, EdgePolarity, EdgeDirection) **+ EdgeSelection (First/Last) 명시** (HALCON 필수, memory feedback_halcon_measurepos_must_haves 준수). 총 7 파라미터 per-FAI PropertyGrid 노출. A1~A5 각각 독립 설정 가능.
- **D-08:** Point ROI 형태 = **Rectangle 만** 지원. PolygonPoints, Circle 미지원 — Y방향 수평 에지 1개 추출 목적상 Rectangle 로 충분.
- **D-09:** 측정값 정밀도 = **소수점 3자릿 (0.001mm, 1μm 해상도)**. UI 표시 + 로그 동일 포맷. FAIConfig.MeasuredValue 기존 표기 관례 유지.
- **D-10:** HALCON 매핑 = **measurePhi 명시 + EdgeSelection First/Last 명시** (memory feedback_halcon_measurepos_must_haves 필수 준수). 구현 방식 = `VisionAlgorithmService.TryFitLine` 재사용 (기존 래퍼가 이미 두 요건 준수). 이중 구현 방지.
- **D-11:** Datum 첫기 실패 시 = **`EdgeToLineDistance.TryExecute` → false + error="Datum not found"**. A1~A5 5개 모두 검출 실패 상태로 UI strip 빨강. 이상점 명확 파악, Skip+경고 패턴 금지.

### 확장성 검증 (SC#4)

- **D-12:** SC#4 검증 방식 = **실제 A6 1개 추가 + Simul 동작 검증**. UAT 단계에서 INI 수동 편집 (또는 UI 추가 버튼) → 프로그램 재시작 → 6개 측정값 (A1~A6) UI 표시 + 공차 판정 확인. 정적 grep + 실제 동작 둘 다 (이중 추천 옵션의 정신).
- **D-13:** FAI 추가 지원 채널 = **INI 직접 편집 + UI 'Add FAI' 버튼 둘 다 검증**. EdgeToLineDistance 가 MeasurementFactory.GetTypeNames() 와 FAIConfig.EdgeMeasureType PropertyGrid 드롭다운에 자동 노출되어야 함.
- **D-14:** 확장 한계 명시 = **A23 까지 보장**. 검증은 A6 1개로 한정. Phase 5 의 100개+ 동적 구조 설계가 이미 보장.
- **D-15:** INI 섹션/키 명명 = **기존 IsDynamicFAIMode + InspectionRecipeManager 패턴 그대로**. 신규 명명 규칙 (예: [FAI_Aseries_*]) 도입 금지. FAIName 필드만 "A1", "A2" 등 사용자 명명.

### Simul 이미지 + UI 결과 표시

- **D-16:** Simul 이미지 출처 = **사용자가 PPT(Datum_정보_260511_2D) 기반 실제 도면 반영 이미지를 직접 제공**. 위치 = `D:\TestImg\Datameasurement\` 하위 (Phase 22 UAT 와 동일 디렉토리). researcher/planner 는 이미지 명명 규칙만 사용자에게 확인하고 Phase 23 시작 전 사용자가 비치.
- **D-17:** A1~A5 결과 표시 UI = **InspectionListView TreeView 펼침 + strip 5개 동시 표시**. CO-05 녹/적 색상 적용. 신규 UI 패널/dashboard 추가 없음.
- **D-18:** A1~A5 공차(Nominal/Tolerance) 입력 경로 = **`MeasurementBase` PropertyGrid** (기존 NominalValue/UpperTolerance/LowerTolerance/EvaluateJudgement). EdgeToLineDistance 는 MeasurementBase 상속만으로 자동 획득.
- **D-19:** msbuild 검증 = **Debug/x64 PASS + Phase 21 baseline 동일 warning** (6 occurrences = MSB3884 × 2 + CS0162 × 2 + CS0219 × 2). 신규 warning 0. Release/x64 는 Phase 23 범위 외.

### Claude's Discretion (researcher/planner 위임)

- 신규 `EdgeToLineDistance.cs` 파일 위치 = `WPF_Example/Custom/Sequence/Inspection/Measurements/EdgeToLineDistance.cs` (PointToLineDistance 와 같은 디렉토리, 명확한 patron).
- TryExecute 내부 구조 (Point ROI fit → midpoint → datumTransform 적용 순서). PointToLineDistanceMeasurement.cs L51-L80 참조.
- TeachingImagePath 자동 로드 fallback 로그 메시지 형식.
- A6 추가 UAT 시 INI 섹션 인덱스 (InspectionRecipeManager 기존 인덱스 규칙 그대로).
- ICustomTypeDescriptor hide 규칙 추가 여부 (현재 CircleDiameter 만 hide 적용 — EdgeToLineDistance 는 일단 hide 없음).

### Folded Todos

(없음 — phase 23 매핑 pending todo 0건)

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Phase 22 carry-over (직접 의존)

- `.planning/phases/22-image-dual-structure/22-CONTEXT.md` — TeachingImagePath / InspectionImagePath 역할 분리 결정
- `.planning/phases/22-image-dual-structure/22-02-SUMMARY.md` — InspectionImagePath = ShotConfig.SimulImagePath 의미적 재해석
- `WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs` §TeachingImagePath — Phase 22 IMG-01 필드 정의 (L35)
- `WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs` §L109, §L226 — InspectionImagePath 역할 명시 주석 (Phase 22 IMG-02)

### Phase 21 메모리 이미지 버퍼

- `.planning/phases/21-memory-image-buffer/21-CONTEXT.md` — `ShotConfig._image` lifetime 계약
- `.planning/phases/21-memory-image-buffer/21-VERIFICATION.md` — AC#1/AC#2 검증 결과 (디스크 I/O 0, dispose 입증)

### 기존 Datum 알고리즘 (PPT 매핑 후보)

- `WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs` — Multi-Algorithm dispatch (AlgorithmType + ICustomTypeDescriptor 동적 노출, Phase 17/19)
- `WPF_Example/Custom/Sequence/Inspection/EDatumAlgorithm.cs` — TwoLineIntersect / CircleTwoHorizontal / VerticalTwoHorizontal enum
- `WPF_Example/Halcon/Algorithms/DatumFindingService.cs` — 3 알고리즘 dispatch + datumTransform 계산

### 기존 Measurement 패턴 (EdgeToLineDistance patron)

- `WPF_Example/Custom/Sequence/Inspection/MeasurementBase.cs` — Nominal/Tolerance/EvaluateJudgement/IsPass 자동 적용
- `WPF_Example/Custom/Sequence/Inspection/MeasurementFactory.cs` — Type=문자열 → 클래스 인스턴스 dispatch (7번째 case 추가 지점)
- `WPF_Example/Custom/Sequence/Inspection/Measurements/PointToLineDistanceMeasurement.cs` — 직접 patron. EdgeToLineDistance 의 Point ROI fit 단계 모방.
- `WPF_Example/Halcon/Algorithms/VisionAlgorithmService.cs` §TryFitLine — measurePhi 명시 + EdgeSelection 명시 매핑 이미 준수. 재사용 시 자동 준수.

### Shot-FAI 동적 구조 (SC#4 확장성)

- `WPF_Example/Custom/Sequence/Inspection/InspectionRecipeManager.cs` — Dynamic FAI INI Save/Load
- `WPF_Example/Custom/Sequence/Inspection/ShotConfig.cs` — Shot 컨테이너 + 메모리 이미지 버퍼 (Phase 21)
- `WPF_Example/Custom/Sequence/Inspection/FAIConfig.cs` — IsDynamicFAIMode + ICustomTypeDescriptor (EdgeMeasureType 별 hide, Phase 19 QUAL-03)
- `WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs` — SIMUL 분기 (EStep.Grab L109, GrabOrLoadDatumImage L226)

### UI (결과 표시)

- `WPF_Example/UI/InspectionListView.xaml` + `.xaml.cs` — TreeView + strip 렌더링 (CO-05 녹/적 적용 경로)

### 외부 사용자 제공

- **PPT: Datum_정보_260511_2D** — Top Fixture #1 의 Datum B/C 정의 + A1~A5 ROI 위치 + 공차 표기. researcher 가 phase 시작 전 사용자 확보 (D:\TestImg\Datameasurement\ 동일 디렉토리에 함께 비치 권장).
- **Simul 이미지** — 사용자가 PPT 도면 반영본 직접 제공 (D-16).

### 메모리 (반드시 준수)

- `memory/feedback_halcon_measurepos_must_haves.md` — HALCON MeasurePos 작성 시 measurePhi 명시 매핑 + EdgeSelection (First/Last) 명시 필수, SmallestRectangle2 rp 자동 도출 의존 금지
- `memory/feedback_comment_convention.md` — `//YYMMDD hbk Phase 23` 마커 (`//260511 hbk Phase 23 ALG-01` 등) 신규 라인 필수, Phase 20 D-12 marker stacking 패턴 준수

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets

- **PointToLineDistanceMeasurement.cs** — EdgeToLineDistance 직접 patron. Point ROI fit + midpoint + (현재) Line ROI fit + 수직 거리. EdgeToLineDistance 는 마지막 2 단계를 "datumTransform Y좌표 추출" 로 대체.
- **VisionAlgorithmService.TryFitLine** — measurePhi 명시 + EdgeSelection 명시 매핑 이미 준수. EdgeToLineDistance 의 Point ROI fit 그대로 재사용.
- **MeasurementBase** — Nominal/Tolerance/EvaluateJudgement/IsPass 자동 적용. EdgeToLineDistance 상속만으로 공차 판정 + UI strip 자동.
- **MeasurementFactory** — 7번째 case 추가 + GetTypeNames() 배열 확장만으로 INI Type=EdgeToLineDistance 자동 dispatch + PropertyGrid 드롭다운 자동 노출.
- **InspectionRecipeManager + IsDynamicFAIMode** — A6~A23 INI 추가 시 코드 변경 0 (Phase 5 동적 구조).
- **CO-05 strip 녹/적** — InspectionListView 에 적용됨. A1~A5 strip 동시 렌더링 자동.
- **DatumConfig.TeachingImagePath** — Phase 22 IMG-01 에서 INI 직렬화 완료. Phase 23 는 소비측 (자동 로드) 추가.

### Established Patterns

- **//YYMMDD hbk Phase 23 ALG-01** 마커 — 신규 라인 필수. 기존 주석 위에 stacking (Phase 20 D-12 패턴).
- **ICustomTypeDescriptor 동적 노출** (Phase 19 QUAL-03) — FAIConfig.EdgeMeasureType 별 hide. EdgeToLineDistance 는 일단 hide 없음 (CircleDiameter 만 hide 적용).
- **Trust-based UAT** (Phase 22 Test 2 패턴) — 신규 코드 경로 0 영역은 코드 변경 0 근거로 PASS 가능.
- **HALCON 에러 처리** — `try/catch → return false` (CLAUDE.md 패턴). EdgeToLineDistance.TryExecute 도 동일.

### Integration Points

- `MeasurementFactory.Create(typeName, owner)` switch: `case "EdgeToLineDistance": return new EdgeToLineDistanceMeasurement(owner);`
- `MeasurementFactory.GetTypeNames()` 반환 배열에 "EdgeToLineDistance" 추가
- `DatumConfig.TeachingImagePath` 소비 위치 = `DatumFindingService` 진입 또는 `Action_FAIMeasurement.GrabOrLoadDatumImage` (planner 결정 — researcher 가 PPT 매핑 후 권고)
- InspectionListView strip 렌더링 = 기존 CO-05 경로 그대로
- `Action_FAIMeasurement.EStep.Measure` 흐름에서 `MeasurementBase.TryExecute(image, datumTransform, ...)` 호출 — EdgeToLineDistance 도 동일 인터페이스 만족

</code_context>

<specifics>
## Specific Ideas

- ROADMAP 의 "EdgeToLineDistance" 는 신규 알고리즘 명칭으로 lock (D-06). MeasurementFactory 6종 + EdgeToLineDistance = 7종.
- ROADMAP 문구 "Datum B → Y축 기준선 / Datum C → X축 기준선" 은 "Y측정 기준선 / X측정 기준선" 의미. 그림 기준 명명 = **Datum B = X축 horizontal, Datum C = Y축 vertical** (D-05) 로 모든 후속 문서 통일.
- A1~A5 가 Datum B 위쪽이면 +Y 양수 (D-02). HALCON image row (아래쪽 양수) 와 부호 반전이므로 datumTransform 적용 시 명시 확인.
- Phase 22 의 "두 경로 같은 파일 가능" 합의는 Phase 23 의 TeachingImagePath 자동 로드 추가 후에도 회귀 0 유지 (UAT 검증 항목).
- PPT 매핑은 researcher 가 phase 시작 시 사용자 1:1 확인. Datum 알고리즘 1개 vs 2개 lock-in 은 researcher → planner 흐름에서 결정.

</specifics>

<deferred>
## Deferred Ideas

- **Top Fixture #2~ / Bottom Fixture 의 A시리즈 검증** — Phase 24 검사 워크플로우 end-to-end 또는 별도 phase
- **Polygon/Circle ROI 형태 지원** — 필요성 발생 시 별도 backlog
- **50회 반복도 통계** — Phase 25 OUT-03
- **TCP 응답 분기 (OK/NG/Error)** — Phase 24 WF-02
- **결과 dashboard 패널 신규** — Phase 25 결과 분석 UI
- **EdgeToLineDistance 의 다른 Datum 축 (X축 거리)** — Y축 외 확장 시 별도 phase
- **공차 입력 테이블형 UI 패널** — Phase 25 와 통합 검토
- **EdgeToLineDistance 에 대한 ICustomTypeDescriptor hide 규칙** — 현재 미적용 (필요 시 별도 backlog)

### Reviewed Todos (not folded)

(없음 — phase 23 매핑 todo 0건)

</deferred>

---

*Phase: 23-top-1-a-simul-end-to-end-2026-05-11*
*Context gathered: 2026-05-11*
