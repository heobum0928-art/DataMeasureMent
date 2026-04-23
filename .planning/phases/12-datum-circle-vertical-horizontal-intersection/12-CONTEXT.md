# Phase 12: datum-circle-vertical-horizontal-intersection - Context

**Gathered:** 2026-04-23
**Status:** Ready for planning

<domain>
## Phase Boundary

신규 Datum 알고리즘 2종을 `DatumFindingService`에 **하드코딩 분기**로 추가한다.

- **A) CircleTwoHorizontal** — Circle ROI 1개 + 수평 ROI 2개. 원 중심에서 내린 이미지 Y축 수직 가상선 ∩ 수평 2-ROI 에지점 concat+FitLineContourXld 단일 연장선 = 교점(RefOrigin).
- **B) VerticalTwoHorizontal** — 수직 ROI 1개 + 수평 ROI 2개. 수직 ROI 에지점 FitLineContourXld 수직선 ∩ 수평 2-ROI concat+FitLineContourXld 수평선 = 교점(RefOrigin).

기존 `TwoLineIntersect`와 공존 — `DatumConfig.AlgorithmType` enum으로 선택. INI 하위호환(AlgorithmType 미존재 → TwoLineIntersect 폴백).

Phase 13은 Phase 12의 하드코딩 분기를 Strategy 패턴(`DatumAlgorithmBase` 파생)으로 추출하는 별도 Phase — Phase 12에서는 분기 구현만 수행하되 Phase 13 리팩터가 쉽게 추출할 수 있도록 private 메서드로 정리한다.

</domain>

<spec_lock>
## Requirements (locked via SPEC.md)

**7 requirements are locked.** See `12-SPEC.md` for full requirements, boundaries, and acceptance criteria (ambiguity 0.17 / gate 0.20).

Downstream agents MUST read `12-SPEC.md` before planning or implementing. Requirements are not duplicated here.

**In scope (from SPEC.md):**
- `DatumConfig`에 `AlgorithmType` enum(3값) + Circle ROI 3필드 + 수직 ROI 1세트(기존 Line1 재사용) + 수평 ROI 2세트(Horizontal_A/B, 각 5필드) 추가
- `DatumFindingService.TryTeachDatum`의 `CircleTwoHorizontal` 분기 구현 (Circle 피팅 + 수평 2-ROI concat 피팅 + 수직 가상선×수평 교점 + RefOrigin/RefAngle 저장)
- `DatumFindingService.TryTeachDatum`의 `VerticalTwoHorizontal` 분기 구현 (수직 ROI 라인 피팅 + 수평 2-ROI concat 피팅 + 직선 연립 교점 + RefOrigin/RefAngle 저장)
- 알고리즘별 실패 모드(공통 2종 + Circle 전용 2종 + Vertical 전용 1종) 감지 + error 문자열 반환
- 기존 `TwoLineIntersect` 경로 회귀 0, INI 하위호환
- 합성 입력 + SIMUL_MODE 이미지 검증 (알고리즘 2종 각각)
- Phase 11 Datum 티칭 UI 연결 — 알고리즘별 드로잉 단계 추가

**Out of scope (from SPEC.md):**
- Strategy 패턴 리팩터링(Phase 13 범위)
- 직교 교정(orthogonality correction)
- Phase 11 Circle ROI(CircleDiameterMeasurement)와의 공유 리팩터링
- 런타임 TryFind 재검출(저장된 RefOrigin/RefAngle만 재사용)
- DatumConfig 완전 대체 / 기존 2-Line 모델 제거

</spec_lock>

<decisions>
## Implementation Decisions

### UI 티칭 흐름 + Phase 11 btn_teachDatum 처리

- **D-01: Phase 12가 btn_teachDatum을 신규 추가한다** — Phase 11 Plan 03a까지만 완료(DatumConfig SourceShotName + Line*Detected_* + LastTeachSucceeded + RenderDatumOverlay 확장) → `btn_teachDatum` / `ECanvasMode.TeachDatum` / `EDatumTeachStep` 미구현 상태. Phase 12에서 TwoLineIntersect + CircleTwoHorizontal + VerticalTwoHorizontal 3-way 버튼·상태머신을 **한 번에 설계**한다. Phase 11 CONTEXT.md D-01..D-05에 기록된 Datum 티칭 UI 설계는 Phase 12 범위로 인계되어 그대로 구현(재설계가 아니라 위임 이행).

- **D-02: 알고리즘 선택 UI = PropertyGrid 자동 드롭다운** — `DatumConfig.AlgorithmType`(`EDatumAlgorithm` enum)을 PropertyTools 기본 enum 렌더링에 맡긴다. 별도 ToolBar ComboBox 또는 ContextMenu 만들지 않는다. Phase 4 EdgePolarity/EdgeDirection(`[ItemsSourceProperty]`)와 동일 관습은 enum 자동 렌더링이므로 어노테이션 불필요. Datum 노드 선택 시 기존 PropertyGrid 우측 패널에 자동 노출.

- **D-03: EDatumTeachStep 단일 enum + switch 분기** — `EDatumTeachStep { Line1, Line2, Circle, Vertical, HorizontalA, HorizontalB, Done }` 하나의 enum으로 통합. 알고리즘별 다음 step 결정은 `MainView`에 `GetNextStep(EDatumAlgorithm, EDatumTeachStep)` switch 메서드로 캡슐화:
  - `TwoLineIntersect`: Line1 → Line2 → Done
  - `CircleTwoHorizontal`: Circle → HorizontalA → HorizontalB → Done
  - `VerticalTwoHorizontal`: Vertical → HorizontalA → HorizontalB → Done
  Phase 13은 이 switch를 `DatumAlgorithmBase.GetROISteps()` 가변 배열로 재설계할 예정(13-CONTEXT.md D-12). Phase 12 구조를 private 메서드로 유지하면 13에서 추출이 용이.

- **D-04: 마지막 ROI MouseUp 직후 TryTeachDatum 자동 호출** — 모든 알고리즘 공통. Phase 11 CONTEXT.md D-02 패턴 유지. `Done` step에서 즉시 `DatumFindingService.TryTeachDatum(image, config, out error)` 호출 → `LastTeachSucceeded` / `label_drawHint` 업데이트. 별도 "Teach 실행" 버튼 없음. 성공 시 Phase 11 D-11 오버레이 갱신 트리거.

### Halcon 호출 파이프라인

- **D-05: Circle 피팅은 기존 `VisionAlgorithmService.TryFindCircle` 재사용** — 이미 EdgesSubPix + FitCircleContourXld(`atukey`) 구현 완료(CircleDiameterMeasurement에서 사용 중). `DatumFindingService.TryTeachDatum` 내 CircleTwoHorizontal 분기에서 직접 호출하여 `(centerRow, centerCol, radius)` 획득. 중복 구현 없음. TryFindCircle 시그니처가 ROI 중심/반지름 기반 ReduceDomain을 사용하므로 DatumConfig.CircleROI_Row/Col/Radius 그대로 전달 가능.

- **D-06: 수평 2-ROI concat = 기존 `TryFindLine` 경로 재사용** — ROI_A, ROI_B 각각에 기존 `DatumFindingService.TryFindLine` 내부 파이프라인(GenMeasureRectangle2 → MeasurePos)을 돌려 에지점 `(rowEdge, colEdge)` 튜플 획득. 각 튜플을 `GenContourPolygonXld`로 XLD contour 2개 생성 → `HOperatorSet.ConcatObj(contourA, contourB, out concatContour)` → `FitLineContourXld(concatContour, "tukey", ...)` 1회 호출로 단일 수평선 `(rBegin, cBegin, rEnd, cEnd)` 획득. 기존 TryFindLine을 리팩터하여 edgePoints만 반환하는 private 헬퍼(`TryExtractEdgePoints`)를 추출하면 재사용 가능.

- **D-07: 수직 ROI 피팅 = 기존 `TryFindLine` 재사용 (Line1_* 필드 공유)** — 수직 ROI는 Rect2 형태이므로 TryFindLine을 그대로 호출. DatumConfig에 `Vertical_*` 전용 필드는 신설하지 않는다. 대신 **알고리즘별 시맨틱스**를 코드 레벨에서 재해석:
  - `TwoLineIntersect`: Line1 = 1st 라인 ROI, Line2 = 2nd 라인 ROI
  - `VerticalTwoHorizontal`: Line1 = 수직 ROI, Line2 = 미사용(기본값 0 유지)
  - `CircleTwoHorizontal`: Line1/Line2 = 미사용(기본값 0 유지)
  필드 문서 주석(XML summary)에 알고리즘별 의미 명시 필수 — 유지보수 시 혼란 방지.

- **D-08: 교점 계산은 두 알고리즘 모두 `HOperatorSet.IntersectionLl` 사용** — 기존 TwoLineIntersect 경로와 동일.
  - **VerticalTwoHorizontal**: 수직 라인 `(vrB, vcB, vrE, vcE)` × 수평 결합 라인 `(hrB, hcB, hrE, hcE)` → IntersectionLl 직접 호출.
  - **CircleTwoHorizontal**: 수직 가상선을 "2점 표현"으로 변환 — `(centerRow - 1.0, centerCol)` → `(centerRow + 1.0, centerCol)` 두 점을 IntersectionLl에 전달. 수평 결합 라인과 교차. 결과는 수학적으로 SPEC Req 4(a) `Row = (-a·centerCol - c) / b, Col = centerCol`과 동치.
  `isOverlapping==1 || Infinity || NaN` 검출 로직도 기존 TwoLineIntersect 경로 그대로 재사용.

### DatumConfig 필드 레이아웃 + AlgorithmType 타입

- **D-09: AlgorithmType = C# enum EDatumAlgorithm** — `public enum EDatumAlgorithm { TwoLineIntersect, CircleTwoHorizontal, VerticalTwoHorizontal }`. Phase 13 CONTEXT와 동기. ParamBase 자동 직렬화가 enum.ToString/Enum.Parse 지원 → INI에 문자열로 저장/로드. default(EDatumAlgorithm)=TwoLineIntersect → Phase 4/11 기존 INI(AlgorithmType 미존재) 자연스러운 폴백. PropertyTools가 enum ComboBox 자동 렌더링(`[ItemsSourceProperty]` 어노테이션 불필요). **enum 파일 위치**: Phase 13이 `WPF_Example/Halcon/Algorithms/Datum/EDatumAlgorithm.cs`로 이동시킬 예정이지만, Phase 12는 임시로 `WPF_Example/Custom/Sequence/Inspection/EDatumAlgorithm.cs`(DatumConfig 옆)에 배치. Phase 13이 `Halcon/Algorithms/Datum/` 폴더 신설 시 단순 이동.

- **D-10: Circle ROI 필드 = CircleROI_Row/Col/Radius (Phase 13 정렬)** — Phase 13 CONTEXT D-07과 동일 명명:
  ```csharp
  [Category("Datum|Circle ROI")]
  public double CircleROI_Row    { get; set; } = 0;  // 검색 영역 중심 Y
  public double CircleROI_Col    { get; set; } = 0;  // 검색 영역 중심 X
  public double CircleROI_Radius { get; set; } = 0;  // 검색 영역 반지름
  ```
  휘발성(검출 결과) 필드:
  ```csharp
  [PropertyTools.DataAnnotations.Browsable(false)]
  public double CircleCenter_Row { get; set; }   // 검출된 원 중심 Y
  [PropertyTools.DataAnnotations.Browsable(false)]
  public double CircleCenter_Col { get; set; }   // 검출된 원 중심 X
  [PropertyTools.DataAnnotations.Browsable(false)]
  public double CircleDetected_Radius { get; set; }
  ```
  `CircleROI_Radius > 0`이 ROI 설정 완료 판정 기준.

- **D-11: 수평 2-ROI 필드 = Horizontal_A_* + Horizontal_B_*** — A/B prefix(병렬·비순서) 명명:
  ```csharp
  [Category("Datum|Horizontal A ROI")]
  public double Horizontal_A_Row     { get; set; } = 0;
  public double Horizontal_A_Col     { get; set; } = 0;
  public double Horizontal_A_Phi     { get; set; } = 0;
  public double Horizontal_A_Length1 { get; set; } = 0;
  public double Horizontal_A_Length2 { get; set; } = 0;

  [Category("Datum|Horizontal B ROI")]
  public double Horizontal_B_Row     { get; set; } = 0;
  public double Horizontal_B_Col     { get; set; } = 0;
  public double Horizontal_B_Phi     { get; set; } = 0;
  public double Horizontal_B_Length1 { get; set; } = 0;
  public double Horizontal_B_Length2 { get; set; } = 0;
  ```
  둘 다 `Length1 > 0 && Length2 > 0`이 ROI 설정 완료 판정 기준. 순서 의존성 없음(concat 후 fit이므로 A/B 순서 교환 결과 동일).

- **D-12: 수직 ROI 필드 = 기존 Line1_* 재사용** — DatumConfig에 `Vertical_*` 전용 필드 신설 X. D-07 시맨틱스 참조. XML summary 주석으로 Line1_*의 "알고리즘별 의미"를 명시:
  ```csharp
  /// <summary>
  /// Line1 ROI — 알고리즘별 의미 다름:
  ///   TwoLineIntersect: 1st 라인 ROI (기준 X축 방향)
  ///   VerticalTwoHorizontal: 수직 ROI (수직 에지 라인)
  ///   CircleTwoHorizontal: 미사용 (기본값 0 유지)
  /// </summary>
  ```

- **D-13: 검출 라인 오버레이용 휘발성 필드 매핑** — 기존 `Line1Detected_RBegin/CBegin/REnd/CEnd` + `Line2Detected_*` 재사용:
  - `TwoLineIntersect`: Line1Detected = 1st 검출선, Line2Detected = 2nd 검출선 (기존)
  - `VerticalTwoHorizontal`: Line1Detected = 검출된 수직선, Line2Detected = 검출된 수평 결합선
  - `CircleTwoHorizontal`: Line1Detected = 수직 가상선(centerRow±화면높이, centerCol 2점), Line2Detected = 검출된 수평 결합선
  RenderDatumOverlay는 `LastTeachSucceeded` 분기 하에서 Line1Detected/Line2Detected를 그리므로 추가 필드 불필요. CircleTwoHorizontal의 원 검출 결과 오버레이(노란 원)는 Phase 11 RenderDatumOverlay 확장 시 `AlgorithmType == CircleTwoHorizontal && CircleDetected_Radius > 0` 분기로 추가.

### 실패 감지 + Error 문자열 + 임계값

- **D-14: Error 문자열 포맷 = 기존 `"{컴포넌트}: {세부}"` 패턴 유지** — SPEC.md Acceptance Criteria가 리터럴로 요구하는 문구 그대로 사용:
  - `"Circle fit failed: {details}"` — Circle 피팅 실패 (Req 5c)
  - `"Vertical line fit failed: {details}"` — 수직 라인 피팅 실패 (Req 5e)
  - `"Horizontal line fit failed: insufficient edges ({N})"` — 수평 라인 결합 후 에지점 < 10 (Req 5a)
  - `"Horizontal line fit failed: {details}"` — 수평 결합 FitLineContourXld 예외 (Req 5a)
  - `"Intersection undefined: lines are parallel"` / `"Intersection undefined: lines are collinear"` — 교점 불정의 (Req 5b)
  알고리즘 prefix(`[CircleTwoHorizontal]`) 추가하지 않음 — 호출자(MainView)가 AlgorithmType을 알고 있으므로 중복. 기존 TwoLineIntersect의 `"Line1: ..."` / `"Line2: ..."` / `"Lines are collinear ..."` / `"Lines are parallel ..."` 문구는 그대로 유지(회귀 0).

- **D-15: 수평 라인 concat 피팅 최소 에지점 임계값 = 10 (두 ROI 합산)** — `edgeCountA + edgeCountB < 10`이면 concat 전에 `"Horizontal line fit failed: insufficient edges (N)"` 반환. tukey 로버스트 피팅이 2점으로도 동작하지만 10점 미만이면 노이즈 평균화 효과 소실 → 신뢰 불가. 기존 TryFindLine의 `<2` 체크보다 상향(단일 ROI가 아니라 2-ROI concat이므로 합리적). 임계값은 상수 `const int MIN_HORIZONTAL_EDGES = 10;`로 `DatumFindingService` 내부 선언.

- **D-16: 교점 평행 판정 = 기존 Infinity/NaN 감지만 유지** — 추가 각도차 ε 전처리 없음. `HOperatorSet.IntersectionLl` 결과의 `isOverlapping.I == 1`(중첩) / `double.IsInfinity(curRow.D) || double.IsInfinity(curCol.D) || double.IsNaN(curRow.D) || double.IsNaN(curCol.D)`(평행)만 검사. 기존 TwoLineIntersect와 완전 동일 로직(회귀 0, 코드 재사용). 에지 스냅으로 인한 거의-평행은 실무적으로 Infinity로 발산하므로 감지 가능.

- **D-17: CircleTwoHorizontal 방향 정합성 위반(Req 5d) = Phase 12 MVP 미구현** — 원 중심이 수평선 위/아래 중 어느 쪽 기대인지 운용 정책 미확립. Phase 12는 다음만 수행:
  1. SPEC.md 유지(삭제하지 않음) — 향후 Phase 13에서 정책 확립 후 구현
  2. 코드에 `// TODO: Phase 13 — 방향 정합성 검사 (운용 정책 확립 후 구현)` 주석 추가
  3. SPEC.md Acceptance Criteria "방향 정합성 위반 ... error에 방향 정합성 위반 메시지 포함" 항목은 **Phase 12 완료 조건에서 제외**(Phase 13 acceptance로 이월). Plan 단계에서 UAT 체크리스트 작성 시 이 항목 스킵 표기.
  4. Deferred 섹션에 명시적 기록(아래 `<deferred>` 참조).

### Claude's Discretion

- `EDatumTeachStep` enum 파일 위치 (MainView.xaml.cs 내부 private enum vs 별도 파일). Phase 13에서 어차피 제거 예정이므로 로컬 private 배치 권장.
- `GetNextStep(EDatumAlgorithm, EDatumTeachStep)` 메서드 위치 (MainView.xaml.cs private vs 별도 helper 클래스).
- `label_drawHint` 단계별 안내 문구(예: "Step 1/3: 원 검색 영역을 드래그하세요" 형식) — Phase 13 CONTEXT D-13과 맞추되 세부 문구는 구현 시 확정.
- `ConcatObj`로 결합된 수평선 오버레이 렌더링 범위(두 ROI 외곽 끝 연결 vs 화면 전체 연장). 현재 Line1Detected_*가 FitLineContourXld 반환 `(lr1,lc1)-(lr2,lc2)` 범위로 자동 결정되므로 디스커션 불필요.
- 원 검출 결과 오버레이(노란 원 by CircleDetected_Radius)의 RenderDatumOverlay 내 배치 순서(전경/배경).
- Plan 분할 전략 — 추정:
  1. Plan 01 — EDatumAlgorithm enum + DatumConfig 필드 확장(AlgorithmType, CircleROI_*, CircleCenter_*, Horizontal_A/B_*, Line1_* 주석) + INI 직렬화 하위호환 검증. 순수 데이터 모델.
  2. Plan 02 — DatumFindingService 3-way 분기 + 기존 TryFindLine 리팩터(TryExtractEdgePoints 추출) + CircleTwoHorizontal/VerticalTwoHorizontal 알고리즘 구현 + 실패 모드 감지. SIMUL_MODE 합성 입력 검증.
  3. Plan 03 — btn_teachDatum + ECanvasMode.TeachDatum + EDatumTeachStep 상태머신(3-way switch) + RenderDatumOverlay 확장(원 검출/수직 가상선) + 육안 검증. Plan 02 완료 후 의존.

### Folded Todos

없음 (해당 phase 맞춤형 todo 미수집 — `gsd-sdk todo.match-phase "12"` 결과 count=0)

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Phase 12 Spec + 전후 Phase Context (필독)
- `.planning/phases/12-datum-circle-vertical-horizontal-intersection/12-SPEC.md` — **LOCKED 7 requirements (ambiguity 0.17).** Goal, Requirements, Boundaries, Constraints, Acceptance Criteria 모두 정식 locked. 플래너·연구자·실행자는 이 문서를 단일 사실 소스로 사용.
- `.planning/phases/11-datum-teaching-ui-roi/11-CONTEXT.md` — Phase 11 Datum 티칭 UI 설계(D-01..D-26). Phase 12 D-01/D-03/D-04가 이 문서의 D-01..D-13을 그대로 이어받아 확장. **btn_teachDatum 설계는 이 문서 기준으로 Phase 12 구현**.
- `.planning/phases/13-datum-algorithm-extensibility/13-CONTEXT.md` — Phase 13 Strategy 리팩터 설계. Phase 12가 만든 하드코딩 분기를 Phase 13에서 `DatumAlgorithmBase` 파생 클래스로 추출. Phase 12 D-03(EDatumTeachStep switch)/D-09(EDatumAlgorithm enum 위치)/D-10(CircleROI_* 명명) 등이 Phase 13 예측 구조와 **의도적으로 정렬**되어 리팩터 비용 최소화.

### Phase 12 주요 수정 대상 (데이터 모델)
- `WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs` — `AlgorithmType`(EDatumAlgorithm) + `CircleROI_Row/Col/Radius` + `CircleCenter_Row/Col/Radius`(휘발성) + `Horizontal_A_Row/Col/Phi/Length1/Length2` + `Horizontal_B_*` + `CircleDetected_Radius`(휘발성) 필드 신설. `Line1_*` 필드는 존치하되 XML summary 주석으로 알고리즘별 시맨틱스 명시.
- `WPF_Example/Custom/Sequence/Inspection/EDatumAlgorithm.cs` (신규) — `public enum EDatumAlgorithm { TwoLineIntersect, CircleTwoHorizontal, VerticalTwoHorizontal }` 단일 파일. Phase 13에서 `WPF_Example/Halcon/Algorithms/Datum/`로 이동 예정.

### Phase 12 주요 수정 대상 (알고리즘)
- `WPF_Example/Halcon/Algorithms/DatumFindingService.cs` — `TryTeachDatum`에 `switch(config.AlgorithmType)` 분기 추가. `TwoLineIntersect` 분기는 기존 로직 그대로 유지. `CircleTwoHorizontal` 분기: 기존 `TryFindLine`을 2회(Horizontal_A, B) 호출하여 edgePoints 얻고 `GenContourPolygonXld → ConcatObj → FitLineContourXld`로 수평 결합선 피팅 + `VisionAlgorithmService.TryFindCircle` 호출로 원 중심 획득 + `IntersectionLl`로 교점. `VerticalTwoHorizontal` 분기: `TryFindLine`을 3회(Line1=수직, Horizontal_A, B) 호출하여 수직선 + 수평 결합선 피팅 + `IntersectionLl`로 교점. 기존 `TryFindLine` 내부 구조에서 edgePoint 추출 부분만 `TryExtractEdgePoints` private 헬퍼로 리팩터 가능(선택).
- `WPF_Example/Halcon/Algorithms/VisionAlgorithmService.cs` — **수정 없음**. `TryFindCircle`(이미 존재, CircleDiameterMeasurement에서 사용 중)을 Phase 12에서 재사용만.

### Phase 12 주요 수정 대상 (UI)
- `WPF_Example/UI/ContentItem/MainView.xaml` — `btn_teachDatum` ToggleButton 신설(btn_circleRoi 옆, RoiToggleButtonStyle 재사용). IsEnabled=False 기본. Phase 11 CONTEXT D-01의 위치/스타일 가이드 따라감.
- `WPF_Example/UI/ContentItem/MainView.xaml.cs` — `ECanvasMode`에 `TeachDatum` 추가. `EDatumTeachStep { Line1, Line2, Circle, Vertical, HorizontalA, HorizontalB, Done }` private enum + `_datumTeachStep` 필드. `TeachDatumButton_Click` 핸들러 + `GetNextStep(EDatumAlgorithm, EDatumTeachStep)` switch 메서드. Rect/Circle 드로잉 완료 이벤트 핸들러에서 `_datumTeachStep`에 따라 DatumConfig 해당 필드 기록 후 다음 step. 마지막 step(Done) 즉시 `DatumFindingService.TryTeachDatum` 자동 호출 → `RenderDatumOverlay` 갱신.
- `WPF_Example/UI/ControlItem/InspectionListView.xaml.cs` — Datum 노드 선택 시 `mParentWindow.mainView.btn_teachDatum.IsEnabled = true` 분기 추가(L258 주변 ICameraParam 분기와 동일 패턴). btn_circleRoi는 Datum 노드에서 비활성화 유지(충돌 방지).
- `WPF_Example/Halcon/Display/HalconDisplayService.cs` — `RenderDatumOverlay`에 `AlgorithmType == CircleTwoHorizontal && CircleDetected_Radius > 0` 분기로 `DispCircle` 추가(노란색). 기존 `LastTeachSucceeded` 분기 하 Line1Detected/Line2Detected 렌더링은 그대로 재사용(알고리즘별 시맨틱스만 달라짐 — D-13 참조).

### 재사용 (수정 없음 예상)
- `WPF_Example/UI/ContentItem/HalconViewerControl.xaml.cs` — Phase 11 Plan 01/02에서 `StartCircleDrawing` + `CircleDrawingCompleted` 이벤트 완비. 수정 없음.
- `WPF_Example/UI/ContentItem/MainResultViewerControl.xaml.cs` — Phase 11에서 Circle 드로잉 완성. `HalconViewer_CircleDrawingCompleted` 핸들러는 Datum 티칭 단계에서도 그대로 재사용(MainView에서 `_canvasMode == ECanvasMode.TeachDatum && _datumTeachStep == EDatumTeachStep.Circle`일 때 DatumConfig.CircleROI_* 기록).
- `WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs` — 런타임 TryFindDatum은 저장된 RefOrigin/RefAngle만 사용하므로 알고리즘 무관. 수정 없음(SPEC Out-of-scope).

### Upstream context (지속적 참조)
- `.planning/phases/04-datum/04-CONTEXT.md` — Datum 기반 설계 원칙 (D-03 IntersectionLl, D-15/D-16 TryFindDatum/TryTeachDatum 계약).
- `.planning/phases/06-rapid-city/06-CONTEXT.md` — Multi-Datum 구조, ImageSourceMode, DatumConfig Sequence 레벨 승격.
- `.planning/PROJECT.md` — 기술 스택 제약(.NET Framework 4.8, C# 7.2, Halcon 24.11), 하위호환 원칙.
- `.planning/REQUIREMENTS.md` — v1 요구사항 매트릭스(Phase 12는 RC 확장에 해당하지만 Requirement ID는 SPEC.md에서 자체 관리).

### 참고 문서 (간접)
- `260303_Rapicity_A8_1_Z-Stopper_MSOP_RevB` — Datum B/C 정의, 현장 티칭 절차. CircleTwoHorizontal/VerticalTwoHorizontal 선택 배경.
- `CYG_Rapid_City_DFM-A8_0_Z_Stopper_V1_0` — FAI 레이아웃 및 Datum 위치.

</canonical_refs>

<code_context>
## Existing Code Insights

### 현재 상태 (2026-04-23 시점 — Phase 11 Plan 01/02/03a 완료)

- **Datum 티칭 UI (버튼):** `btn_teachDatum` 미존재(확정). `MainView.xaml` 툴바는 `btn_rectRoi`/`btn_polygonRoi`/`btn_circleRoi`/`btn_calibrate`만 존재. `ECanvasMode = { None, RectRoi, PolygonRoi, CircleRoi, Calibration }` 5값 (`TeachDatum` 없음). `EDatumTeachStep` 미정의.
- **Circle ROI 인프라:** 완비됨. `HalconViewerControl.StartCircleDrawing` + `CircleDrawingCompleted` 이벤트(`CircleDrawCompletedArgs { CenterRow, CenterCol, Radius }`) Phase 11 Plan 02에서 추가됨(MainResultViewerControl.xaml.cs:113/287/961). `MainView.HalconViewer_CircleDrawingCompleted` 핸들러 존재(Line 733).
- **DatumConfig 상태:** Phase 11 Plan 03a 완료. `SourceShotName`, `ReuseFromShotName`, `Line*Detected_RBegin/CBegin/REnd/CEnd` (×2), `LastTeachSucceeded`, `LastFindSucceeded`, `CurrentTransform`(HTuple 휘발) 필드 완비. Circle/Horizontal/Vertical 필드 **없음** — Phase 12 신설.
- **DatumFindingService 상태:** `TryFindDatum`/`TryTeachDatum`(L122) + `TryFindLine`(L224) 완비. `TwoLineIntersect` 경로 100% 동작. `isOverlapping==1 || Infinity || NaN` 평행 감지 완비(L77-87, L175-187). `config.Line*Detected_*` 성공 시 기록(L200-207).
- **VisionAlgorithmService.TryFindCircle:** 존재(EdgesSubPix + atukey + FitCircleContourXld). CircleDiameterMeasurement에서 사용 중. Phase 12에서 DatumFindingService가 재사용.
- **RenderDatumOverlay 상태:** `HalconDisplayService.cs:350`. 기존 Line1/Line2 ROI Rectangle2 표시 + RefOrigin 마젠타 십자(기존) + `LastTeachSucceeded` 분기 하 Line1Detected(노랑)/Line2Detected(시안) + 교점 빨간 십자(20px) 완비. Phase 12가 추가할 것: `AlgorithmType == CircleTwoHorizontal` 분기 하에서 `DispCircle(CircleCenter_Row, CircleCenter_Col, CircleDetected_Radius)` 노란 원 추가.
- **InspectionListView.xaml.cs Datum 노드 Grab 활성화:** Phase 11 Plan 03a에서 Datum 노드 선택 시 Grab/LoadImage 활성화 추가됨(L249, L344 주변 btn_circleRoi 제어와 같은 블록). btn_teachDatum 분기는 Phase 12에서 추가.

### Phase 12 전체 흐름 (개념)

**티칭:**
1. 사용자가 Datum 노드 선택 → PropertyGrid에서 `AlgorithmType` 드롭다운(EDatumAlgorithm) 선택 (기본값 TwoLineIntersect — 기존 Phase 4/11 레시피 동작 동일)
2. `btn_teachDatum` 클릭 → `ECanvasMode.TeachDatum`, 초기 `_datumTeachStep = GetFirstStep(AlgorithmType)`:
   - `TwoLineIntersect` → `Line1`
   - `CircleTwoHorizontal` → `Circle`
   - `VerticalTwoHorizontal` → `Vertical`
3. step에 따라 `StartRectangleDrawing()` 또는 `StartCircleDrawing()` 호출 + `label_drawHint`에 안내("Step 1/3: 원 검색 영역을 드래그하세요" 등)
4. 각 드로잉 완료(MouseUp) → 해당 필드 기록 + `GetNextStep` switch로 다음 step 설정 → 다음 드로잉 시작
5. 마지막 step(Done) 도달 → `DatumFindingService.TryTeachDatum(image, config, out error)` 자동 호출
6. 성공 → `config.LastTeachSucceeded = true` + RenderDatumOverlay 갱신(AlgorithmType별 적절한 검출 오버레이)
7. 실패 → `label_drawHint`에 빨간 에러 메시지(예: "Circle fit failed: no edges found"), ROI 유지하여 재튜닝 가능

**DatumFindingService.TryTeachDatum 내부 (Phase 12 추가):**
```csharp
switch (config.AlgorithmType)
{
    case EDatumAlgorithm.CircleTwoHorizontal:
        return TryTeachCircleTwoHorizontal(image, config, out error);
    case EDatumAlgorithm.VerticalTwoHorizontal:
        return TryTeachVerticalTwoHorizontal(image, config, out error);
    case EDatumAlgorithm.TwoLineIntersect:
    default:
        return TryTeachTwoLineIntersect(image, config, out error); // 기존 로직을 private 메서드로 추출
}
```

### Halcon 핵심 호출 순서

**CircleTwoHorizontal:**
```csharp
// 1) 원 검출 (기존 VisionAlgorithmService.TryFindCircle 재사용)
if (!_visionAlg.TryFindCircle(image, config.CircleROI_Row, config.CircleROI_Col,
                              config.CircleROI_Radius, config.Sigma, config.EdgeThreshold,
                              out double centerRow, out double centerCol, out double radius,
                              out string circleError))
{
    error = "Circle fit failed: " + circleError;
    return false;
}
config.CircleCenter_Row = centerRow;
config.CircleCenter_Col = centerCol;
config.CircleDetected_Radius = radius;

// 2) 수평 ROI A, B 각각에서 에지점 추출 (TryExtractEdgePoints private 헬퍼)
if (!TryExtractEdgePoints(image, imageWidth, imageHeight,
        config.Horizontal_A_Row, config.Horizontal_A_Col, config.Horizontal_A_Phi,
        config.Horizontal_A_Length1, config.Horizontal_A_Length2,
        config.Sigma, config.EdgeThreshold, config.EdgePolarity,
        out HTuple rowEdgeA, out HTuple colEdgeA, out string errA))
{
    error = "Horizontal line fit failed: " + errA;
    return false;
}
if (!TryExtractEdgePoints(image, imageWidth, imageHeight,
        config.Horizontal_B_Row, ..., out HTuple rowEdgeB, out HTuple colEdgeB, out string errB))
{
    error = "Horizontal line fit failed: " + errB;
    return false;
}

// 3) 에지점 개수 합산 임계값 검사
int totalEdges = rowEdgeA.TupleLength() + rowEdgeB.TupleLength();
if (totalEdges < MIN_HORIZONTAL_EDGES)  // = 10
{
    error = "Horizontal line fit failed: insufficient edges (" + totalEdges + ")";
    return false;
}

// 4) XLD contour 2개 → ConcatObj → FitLineContourXld 1회
HOperatorSet.GenContourPolygonXld(out HObject contourA, rowEdgeA, colEdgeA);
HOperatorSet.GenContourPolygonXld(out HObject contourB, rowEdgeB, colEdgeB);
HOperatorSet.ConcatObj(contourA, contourB, out HObject concatContour);
HOperatorSet.FitLineContourXld(concatContour, "tukey", -1, 0, 5, 2,
    out HTuple hrB, out HTuple hcB, out HTuple hrE, out HTuple hcE, out _, out _, out _);

// 5) 수직 가상선 (centerRow±1, centerCol) × 수평 결합선 → IntersectionLl
HOperatorSet.IntersectionLl(
    centerRow - 1.0, centerCol, centerRow + 1.0, centerCol,
    hrB, hcB, hrE, hcE,
    out HTuple curRow, out HTuple curCol, out HTuple isOverlapping);

// 6) 평행/중첩 감지 (기존 로직 재사용)
if (isOverlapping.I == 1 || double.IsInfinity(curRow.D) || ...)
{
    error = "Intersection undefined: lines are parallel/collinear";
    return false;
}

// 7) 저장
config.RefOriginRow = curRow.D;
config.RefOriginCol = curCol.D;
config.RefAngleRad = Math.Atan2(hrE.D - hrB.D, hcE.D - hcB.D);  // 수평선 기울기
config.IsConfigured = true;
config.Line1Detected_* = (수직 가상선 2점, 오버레이용)
config.Line2Detected_* = (hrB/hcB/hrE/hcE, 오버레이용)
config.LastTeachSucceeded = true;
```

**VerticalTwoHorizontal:** 1) 수직 ROI TryFindLine → (vrB, vcB, vrE, vcE) / 2)+3)+4) CircleTwoHorizontal와 동일 수평 결합 / 5) IntersectionLl(vrB/vcB/vrE/vcE, hrB/hcB/hrE/hcE). CircleCenter 단계 없음.

### C# 7.2 / .NET 4.8 제약 유지
- nullable reference types, switch expressions, record 불가.
- out 변수 인라인 선언(C# 7.0)은 허용(기존 Phase 11 Plan 03a 코드에서 사용 중).
- Tuple deconstruction(C# 7.0)도 허용.
- HTuple 인덱싱(.D, .I, .TupleLength()) 기존 패턴 그대로.

### 빌드/검증
- `msbuild WPF_Example/DatumMeasurement.csproj /p:Configuration=Debug /p:Platform=x64` 빌드 성공 + 신규 경고 없음이 필수(SPEC AC).
- SIMUL_MODE + 합성 이미지(알고리즘별 입력)로 티칭 성공/실패 경로 각각 육안 검증.
- 기존 Phase 4/6/11 INI 레시피 로드 → TwoLineIntersect 폴백 회귀 확인.

</code_context>

<specifics>
## Specific Ideas

### EDatumAlgorithm enum 초안
```csharp
// WPF_Example/Custom/Sequence/Inspection/EDatumAlgorithm.cs (Phase 12)
// Phase 13에서 WPF_Example/Halcon/Algorithms/Datum/로 이동 예정
namespace ReringProject.Sequence {
    public enum EDatumAlgorithm {
        TwoLineIntersect,           // 기존 Phase 4 방식 (Line1∩Line2)
        CircleTwoHorizontal,        // Circle 센터 수직 가상선 ∩ 수평 2-ROI concat
        VerticalTwoHorizontal,      // 수직 ROI ∩ 수평 2-ROI concat
    }
}
```

### DatumConfig 신규/수정 필드 초안
```csharp
//260423 hbk Phase 12 — AlgorithmType + Circle/Horizontal ROI 필드 신설
[Category("Datum|Algorithm")]
public EDatumAlgorithm AlgorithmType { get; set; } = EDatumAlgorithm.TwoLineIntersect;

[Category("Datum|Circle ROI")]  // CircleTwoHorizontal 전용
public double CircleROI_Row    { get; set; } = 0;
public double CircleROI_Col    { get; set; } = 0;
public double CircleROI_Radius { get; set; } = 0;

[Category("Datum|Horizontal A ROI")]  // CircleTwoHorizontal + VerticalTwoHorizontal 공용
public double Horizontal_A_Row     { get; set; } = 0;
public double Horizontal_A_Col     { get; set; } = 0;
public double Horizontal_A_Phi     { get; set; } = 0;
public double Horizontal_A_Length1 { get; set; } = 0;
public double Horizontal_A_Length2 { get; set; } = 0;

[Category("Datum|Horizontal B ROI")]
public double Horizontal_B_Row     { get; set; } = 0;
public double Horizontal_B_Col     { get; set; } = 0;
public double Horizontal_B_Phi     { get; set; } = 0;
public double Horizontal_B_Length1 { get; set; } = 0;
public double Horizontal_B_Length2 { get; set; } = 0;

// 휘발성 — Circle 검출 결과
[PropertyTools.DataAnnotations.Browsable(false)]
public double CircleCenter_Row { get; set; }
[PropertyTools.DataAnnotations.Browsable(false)]
public double CircleCenter_Col { get; set; }
[PropertyTools.DataAnnotations.Browsable(false)]
public double CircleDetected_Radius { get; set; }
```

### Line1_* 주석 보강 (의미 시맨틱스 명시)
```csharp
//260409 hbk Phase 4: Line1 ROI
//260423 hbk Phase 12 — 알고리즘별 의미:
//   TwoLineIntersect: 1st 라인 ROI (기준 X축 방향 에지 라인)
//   VerticalTwoHorizontal: 수직 ROI (수직 에지 라인)
//   CircleTwoHorizontal: 미사용 (기본값 0 유지)
[Category("Datum|Line1 ROI")]
public double Line1_Row { get; set; } = 0;
// ... 이하 동일
```

### EDatumTeachStep switch (MainView.xaml.cs private)
```csharp
//260423 hbk Phase 12 — 알고리즘별 ROI 드로잉 단계
private enum EDatumTeachStep { Line1, Line2, Circle, Vertical, HorizontalA, HorizontalB, Done }

private EDatumTeachStep GetFirstStep(EDatumAlgorithm algorithm) {
    switch (algorithm) {
        case EDatumAlgorithm.CircleTwoHorizontal: return EDatumTeachStep.Circle;
        case EDatumAlgorithm.VerticalTwoHorizontal: return EDatumTeachStep.Vertical;
        case EDatumAlgorithm.TwoLineIntersect:
        default: return EDatumTeachStep.Line1;
    }
}

private EDatumTeachStep GetNextStep(EDatumAlgorithm algorithm, EDatumTeachStep current) {
    switch (algorithm) {
        case EDatumAlgorithm.TwoLineIntersect:
            if (current == EDatumTeachStep.Line1) return EDatumTeachStep.Line2;
            return EDatumTeachStep.Done;
        case EDatumAlgorithm.CircleTwoHorizontal:
            if (current == EDatumTeachStep.Circle) return EDatumTeachStep.HorizontalA;
            if (current == EDatumTeachStep.HorizontalA) return EDatumTeachStep.HorizontalB;
            return EDatumTeachStep.Done;
        case EDatumAlgorithm.VerticalTwoHorizontal:
            if (current == EDatumTeachStep.Vertical) return EDatumTeachStep.HorizontalA;
            if (current == EDatumTeachStep.HorizontalA) return EDatumTeachStep.HorizontalB;
            return EDatumTeachStep.Done;
        default: return EDatumTeachStep.Done;
    }
}
```

### DatumFindingService 분기 구조
```csharp
public bool TryTeachDatum(HImage image, DatumConfig config, out string error) {
    error = null;
    if (image == null || config == null) { error = "image or config is null"; return false; }

    switch (config.AlgorithmType) {
        case EDatumAlgorithm.CircleTwoHorizontal:
            return TryTeachCircleTwoHorizontal(image, config, out error);
        case EDatumAlgorithm.VerticalTwoHorizontal:
            return TryTeachVerticalTwoHorizontal(image, config, out error);
        case EDatumAlgorithm.TwoLineIntersect:
        default:
            return TryTeachTwoLineIntersect(image, config, out error);  // 기존 로직 추출
    }
}

private const int MIN_HORIZONTAL_EDGES = 10;  //260423 hbk Phase 12 — D-15

// TryTeachTwoLineIntersect = 기존 TryTeachDatum L122-218 로직 그대로 이동 (회귀 0)
// TryTeachCircleTwoHorizontal, TryTeachVerticalTwoHorizontal = 신규 구현
// TryExtractEdgePoints(image, ...) = 기존 TryFindLine에서 edgePoints 추출 부분 private 헬퍼로 분리
```

### 예상 Plan 분할 (Claude Discretion)
- **Plan 01:** EDatumAlgorithm enum 신설 + DatumConfig 필드 확장(AlgorithmType + CircleROI_* + Horizontal_A/B_* + CircleCenter_* + CircleDetected_Radius) + Line1_* 주석 보강 + INI 하위호환 회귀 확인. 순수 데이터 모델, UI/로직 변경 없음.
- **Plan 02:** DatumFindingService 리팩터(TryTeachTwoLineIntersect 추출, TryExtractEdgePoints 헬퍼 추출) + CircleTwoHorizontal/VerticalTwoHorizontal 분기 구현 + 실패 모드 감지(Circle fit, Vertical line fit, Horizontal line fit insufficient edges, Intersection undefined) + SIMUL_MODE 합성 입력 검증(성공/실패 경로). `// TODO: Phase 13 — 방향 정합성 검사` 주석 추가(Req 5d 이월).
- **Plan 03:** `btn_teachDatum` XAML 신설 + `ECanvasMode.TeachDatum` + `EDatumTeachStep` + `GetFirstStep/GetNextStep` switch + CircleDrawingCompleted/RectangleCommitted 핸들러에서 step별 DatumConfig 기록 + Done 시 TryTeachDatum 자동 호출 + HalconDisplayService.RenderDatumOverlay의 AlgorithmType==CircleTwoHorizontal 분기(노란 원) + InspectionListView.Datum 노드 선택 시 btn_teachDatum.IsEnabled=true. SIMUL_MODE 3-way 티칭 성공 + 육안 검증.

Plan 01 완료 후 Plan 02/03 직렬(Plan 03이 Plan 02의 TryTeachDatum 반환을 사용하므로).

### INI 하위호환 회귀 검증 시나리오
1. Phase 4/11 작성 INI 레시피(AlgorithmType, CircleROI_*, Horizontal_A/B_* 모두 미존재) 로드 → AlgorithmType=TwoLineIntersect, 나머지 0 기본값 → 기존 TwoLineIntersect 동작 100% 재현. RefOrigin/RefAngle/IsConfigured 왕복 보존.
2. Phase 12 AlgorithmType=CircleTwoHorizontal 저장 후 재로드 → AlgorithmType + CircleROI_* + Horizontal_A/B_* 모두 왕복 보존.
3. 동일하게 VerticalTwoHorizontal 왕복 보존.

### SPEC vs Phase 12 Acceptance 차이 (1건)
- SPEC Acceptance Criteria 8번째 항목 "방향 정합성 위반(CircleTwoHorizontal) 입력에서 TryTeachDatum이 false + error에 방향 정합성 위반 메시지 포함" → **Phase 12 미구현(이월)**. 해당 체크박스는 Plan 단계에서 "Phase 13 이월" 표기. SPEC.md 자체는 변경하지 않음(Phase 13이 요구사항을 만족시키도록 설계됨).

</specifics>

<deferred>
## Deferred Ideas

- **CircleTwoHorizontal 방향 정합성 위반 검사 (SPEC Req 5d)** — Phase 12 MVP 미구현. 원 중심이 수평선 위/아래 어느 쪽 기대인지 운용 정책 미확립 → DatumConfig에 기대 방향 필드 추가 없이 Phase 12는 다른 실패 모드만 구현. 코드에 `// TODO: Phase 13 — 방향 정합성 검사 (운용 정책 확립 후 구현)` 주석 + SPEC Acceptance Criteria 해당 항목은 Phase 12 완료 조건에서 제외(Phase 13 acceptance로 이월). SPEC.md 문서는 유지(Phase 13 참조용).
- **Phase 13 Strategy 패턴 추출 (EDatumAlgorithm → DatumAlgorithmBase)** — SPEC Out-of-scope 명시. Phase 12는 하드코딩 `switch(AlgorithmType)` 분기만. Phase 13에서 `WPF_Example/Halcon/Algorithms/Datum/` 폴더 신설 + Base/파생 클래스 추출. 단 Phase 12 구조(enum 단일 파일, private 메서드 분리, MainView.GetNextStep switch)는 Phase 13 리팩터 난이도를 최소화하도록 의도적으로 정렬.
- **직교 교정(orthogonality correction)** — SPEC Out-of-scope. Phase 12는 수평선이 ε 이상 기울어져도 교점 계산은 수행(90° 보정 X).
- **Phase 11 Circle ROI 공유 리팩터** — SPEC Out-of-scope. Phase 11 CircleDiameterMeasurement(`Circle_Row/Col/Radius`)와 Phase 12 Datum Circle(`CircleROI_Row/Col/Radius`)은 별도 코드 경로. 공유 추출은 미수행.
- **런타임 TryFind 재검출 (Grab마다 Circle/수평 재검출)** — SPEC Out-of-scope. 런타임은 저장된 `RefOrigin/RefAngle`만 재사용 + hom_mat2d 변환.
- **DatumConfig Vertical_* 전용 필드 신설** — D-07/D-12에서 기각. Line1_* 재사용 + XML summary 주석으로 시맨틱스 명시.
- **알고리즘 prefix 에러 접두사 (`[CircleTwoHorizontal] ...`)** — D-14에서 기각. 호출자가 AlgorithmType 인지 가능하므로 중복. SPEC Acceptance 문구와 일치 유지.
- **수평 concat 피팅 동적 에지점 임계값 (ROI 크기 기반)** — D-15에서 기각. 고정 10 사용. Phase 13 이후 튜닝 필요 시 별도 Phase.
- **교점 평행 판정 각도차 ε 전처리** — D-16에서 기각. 기존 Infinity/NaN 감지만 유지(IntersectionLl이 실측으로 검출).
- **ROI 드로잉 중간 재진입 UX (단계 중 Escape 키 처리)** — Claude Discretion 수준. Plan 03 구현 시 기존 Rect/Circle 드로잉 Escape 패턴(MainView.xaml.cs:599) 재사용. 별도 논의 불필요.
- **AlgorithmType 변경 시 기존 ROI 좌표 유지 vs 초기화** — Claude Discretion. 권장: 변경 시 `IsConfigured=false, LastTeachSucceeded=false`로 리셋하되 이전 ROI 좌표는 유지(사용자가 필요 시 수동 조정). Plan 03 구현 시 확정.

### Reviewed Todos (not folded)

- 없음 (`gsd-sdk todo.match-phase "12"` 결과 count=0)

</deferred>

---

*Phase: 12-datum-circle-vertical-horizontal-intersection*
*Context gathered: 2026-04-23*
