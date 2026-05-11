# Phase 23: Top #1 A시리즈 Simul end-to-end — Research

**Researched:** 2026-05-11
**Domain:** Halcon-based WPF vision inspection — neue `EdgeToLineDistance` Measurement + Datum B/C 자동 보정 + Simul end-to-end
**Confidence:** HIGH for code patterns / Integration points / EdgeSelection-gap. MEDIUM-PENDING-USER for D-01 (PPT 미존재).

## Summary

Phase 23 의 핵심 신규 산출물은 ① 단일 클래스 `EdgeToLineDistanceMeasurement` (`MeasurementFactory` 7번째 case), ② Datum B/C 설정 lock-in (PPT 부재로 사용자 확인 필요), ③ TeachingImagePath 자동 로드 (Phase 22 carry-over), ④ Simul UAT 5개 strip 동시 표시 검증이다. 인프라는 Phase 5/6/7/17/19/21/22 에서 이미 갖춰져 있어 코드 추가량이 최소화된다.

**중대 발견 (HIGH confidence):**

1. **`VisionAlgorithmService.TryFitLine` 는 EdgeSelection 을 무시한다.** L18-26 시그니처에 `selection` 파라미터가 없고, L94-95 의 `MeasurePos` 호출은 4번째 인자(select)를 하드코딩 `"all"` 로 사용한다. memory feedback (`feedback_halcon_measurepos_must_haves.md`) 의 "EdgeSelection 명시 (first/last/all)" 필수 조건과 **위배**. 기존 `PointToLineDistanceMeasurement` 도 EdgeSelection 필드가 없고 모두 `"all"` 동작이다. → Phase 23 의 `EdgeToLineDistance` 는 D-07 (7개 파라미터 노출) 과 D-10 (EdgeSelection 명시) 를 만족하려면 **TryFitLine 시그니처 확장** 또는 **별도 헬퍼/직접 MeasurePos 호출** 둘 중 하나가 필요하다. → 결정 필요 (recommended: TryFitLine 시그니처 확장, 기존 caller 는 default "all" 로 호환).

2. **Datum 자동 보정의 Y좌표 추출 = `AffineTransPoint2d` (이미지 row 좌표계, 아래쪽 양수)**. `VisionAlgorithmService.TryFitLine` L43-58 이 hom_mat2d 로 ROI center 를 변환하지만, 결과는 **row 좌표** 그대로다. D-02 의 "+Y (Datum B 위쪽이 양수, 공학 표준)" 를 만족하려면 측정 추출 후 **부호 반전 (`y_mm = -row_in_datum_frame * pixelResolution`)** 이 필요하다. 또는 datumTransform 빌드 시 Y 축 자체를 뒤집어야 한다 — 후자는 Datum 1/2/3 알고리즘 모두 영향 → 전자(클라이언트측 부호 처리) 채택 권장.

3. **TeachingImagePath 자동 로드 진입점은 `Action_FAIMeasurement.GrabOrLoadDatumImage(L223-242)` 가 유일한 후보다.** 현재 본문은 `ShotParam.SimulImagePath` 만 로드. Phase 22 IMG-02 SUMMARY 의 carry-over (L226 주석) 가 "재티칭/UI 셋업 경로에서만 참조 (Phase 23 carry-over 가능)" 로 명시했다. Phase 23 에서 본 메서드 진입부에 `DatumConfig.TeachingImagePath` 우선순위 분기를 추가해야 한다. **단**, `GrabOrLoadDatumImage` 가 단일 `parentSeq` 만 받고 어떤 DatumConfig 를 사용할지 알지 못한다 → "첫 번째 DatumConfig 의 TeachingImagePath" 또는 "DatumConfigs 순회하여 모두 같은 TeachingImagePath" 또는 "fallback = ShotParam.SimulImagePath" 중 하나로 lock-in 필요. Recommended: **DatumConfigs[0].TeachingImagePath 가 비어있지 않으면 사용, 아니면 SimulImagePath 폴백** (Top Fixture #1 단일 Datum B/C 시 자명한 선택).

4. **MeasurementFactory 7번째 case + GetTypeNames() 1줄 추가만으로 INI/PropertyGrid 자동 노출 완성.** `FAIConfig.EdgeMeasureType` 의 ItemsSource 는 `MeasurementFactory.GetTypeNames()` 단일 소스 (FAIConfig.cs L59-60 캐시). UI 코드 0 수정. D-13 (INI 직접 편집 + UI 'Add FAI' 둘 다) 자동 충족.

5. **UI strip 5개 동시 표시는 기존 Phase 7/CO-05 경로로 자동 작동.** "strip" 은 `InspectionListView` 의 UI 위젯이 아니라 **Halcon 이미지 viewer 위 오버레이 라인** (`HalconDisplayService.cs:167-180`) 이다. `RoiId.StartsWith("FAI-Edge") + EndsWith("-OK/-NG")` 패턴으로 녹/적 자동. 단, 현재 5종 measurement 중 **`EdgePairDistanceMeasurement` 만 overlay 를 반환** (`FAIEdgeMeasurementService.result.Overlays`) 하고 나머지(PointToLineDistance, PointToPointDistance, LineToLineAngle, CircleDiameter, LineToLineDistance) 는 `overlays = new List<EdgeInspectionOverlay>()` 빈 리스트 (Phase 7-01 D-03). → **`EdgeToLineDistance` 도 PointToLineDistance 패턴(빈 리스트) 을 따르면 5개 strip 의 녹/적 색상 표시가 동작하지 않는다.** Phase 23 에서 overlay 채우기를 추가하든지, SC#2 정의를 "5개 strip 색상" → "5개 FAI 트리 노드 OK/NG 색상" 으로 명확화 필요. → 결정 필요.

**Primary recommendation:**
1. 사용자에게 D-01 (Datum 알고리즘 1개 CTH vs 2개) 1줄 답변 받기 (discuss-phase 에서 처리).
2. EdgeSelection 명시 처리는 `VisionAlgorithmService.TryFitLine` 시그니처에 optional `selection` 인자 추가 (기본값 "all", 기존 caller 호환).
3. +Y 부호 처리는 `EdgeToLineDistance.TryExecute` 내부에서 명시적 `resultValue = -transformedRow_relative * pixelResolution` 으로 처리.
4. `Action_FAIMeasurement.GrabOrLoadDatumImage` 진입부에 `DatumConfigs[0].TeachingImagePath` fallback 분기 추가.
5. SC#2 "strip 색상" 정의 — overlay 미생성 시 Halcon viewer 상 색상 라인 없음을 명시 (또는 EdgeToLineDistance 에 minimal overlay 추가).

## User Constraints (from CONTEXT.md)

### Locked Decisions

(아래 D-01 ~ D-19 는 `23-CONTEXT.md` 에서 그대로 인용. 충돌 금지.)

- **D-01 [PENDING-USER]:** Datum 표현 방식 = **researcher 가 PPT 확인 후 결정**. 후보 (a) Datum 1개 = CTH (B1 홀 Circle + 2 horizontal tangent line, origin=circle center) / (b) Datum 2개 = B(TLI 또는 단일 horizontal) + C(CTH 또는 단일 vertical). **현재 RESEARCH 시점: PPT 미존재 → 사용자 1줄 답변 필요** (Assumptions Log A1).
- **D-02:** Y측정 부호 규약 = **+Y (Datum B 위쪽이 양수, 공학 표준)**. HALCON image row (아래쪽 양수) 와 반대 → datumTransform 적용 시 부호 반전 확인.
- **D-03:** 범위 = **Top Fixture #1 단독**. #2~/Bottom 은 구조 동일 가정만.
- **D-04:** **TeachingImagePath 자동 로드 구현** (Phase 22 carry-over). 비어있지 않으면 사용, 비어있으면 `ShotConfig.SimulImagePath` 폴백.
- **D-05:** 좌표계 표기 정정 = **Datum B = X축 (horizontal), Datum C = Y축 (vertical)**.
- **D-06:** ALG-01 = 신규 `EdgeToLineDistance` Measurement (MeasurementFactory 7번째). Point ROI 1개 fit → datumTransform 적용 → Y좌표 추출 = "Datum B 까지 Y방향 거리".
- **D-07:** Edge 파라미터 노출 = **PointToLineDistance 동일 6종 + EdgeSelection 명시** = 총 7개.
- **D-08:** ROI 형태 = **Rectangle 만**.
- **D-09:** 정밀도 = **소수점 3자릿 (0.001mm)**.
- **D-10:** HALCON 매핑 = measurePhi 명시 + EdgeSelection First/Last 명시 (memory feedback 필수).
- **D-11:** Datum 첫기 실패 시 = `EdgeToLineDistance.TryExecute → false + error="Datum not found"`. A1~A5 5개 모두 strip 빨강.
- **D-12:** SC#4 검증 = 실제 A6 1개 추가 + Simul 동작 검증.
- **D-13:** FAI 추가 채널 = INI 직접 편집 + UI 'Add FAI' 둘 다 검증.
- **D-14:** 확장 한계 = A23 까지 보장, 검증은 A6 1개.
- **D-15:** INI 섹션/키 명명 = 기존 IsDynamicFAIMode + InspectionRecipeManager 패턴 그대로.
- **D-16:** Simul 이미지 = 사용자 직접 제공 (`D:\TestImg\Datameasurement\` 하위).
- **D-17:** UI 결과 표시 = InspectionListView TreeView 펼침 + strip 5개 동시 (CO-05 녹/적).
- **D-18:** 공차 입력 = MeasurementBase PropertyGrid (Nominal/Tolerance/EvaluateJudgement) 자동 획득.
- **D-19:** msbuild = Debug/x64 PASS + Phase 21 baseline 6 warning (MSB3884×2 + CS0162×2 + CS0219×2) 유지, 신규 0.

### Claude's Discretion

- `EdgeToLineDistance.cs` 파일 위치 = `WPF_Example/Custom/Sequence/Inspection/Measurements/EdgeToLineDistance.cs` (PointToLineDistance 와 동일 디렉토리).
- TryExecute 내부 구조 (Point ROI fit → midpoint → datumTransform 적용 순서).
- TeachingImagePath 자동 로드 fallback 로그 메시지 형식.
- A6 추가 UAT INI 섹션 인덱스 (InspectionRecipeManager 기존 인덱스 규칙 그대로).
- ICustomTypeDescriptor hide 규칙 추가 여부 (현재 CircleDiameter 만 hide — EdgeToLineDistance 는 hide 없음).

### Deferred Ideas (OUT OF SCOPE)

- Top Fixture #2~ / Bottom Fixture 의 A시리즈 검증 (Phase 24 또는 별도 phase)
- Polygon/Circle ROI 형태 지원 (별도 backlog)
- 50회 반복도 통계 (Phase 25 OUT-03)
- TCP 응답 분기 (OK/NG/Error) (Phase 24 WF-02)
- 결과 dashboard 패널 신규 (Phase 25)
- EdgeToLineDistance 의 다른 Datum 축 (X축 거리)
- 공차 입력 테이블형 UI 패널 (Phase 25 와 통합)
- EdgeToLineDistance 에 대한 ICustomTypeDescriptor hide 규칙 (별도 backlog)

## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| **ALG-01** | Top Fixture #1 의 Datum B/C 설정 → FAI A1~A5 Y방향 거리(mm) 측정까지 Simul 이미지 1장으로 오류 없이 완주 (확장 A6~A23) | Findings #1~#5 + Standard Stack 항목 (EdgeToLineDistance 클래스 신규, MeasurementFactory 확장, TeachingImagePath 자동 로드, EdgeSelection gap 해소) |

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Datum B/C 자동 찾기 | Halcon Algorithm (`DatumFindingService`) | — | 기존 3-way dispatch (TLI/CTH/VTH) 재사용. Phase 23 변경 없음 |
| EdgeToLineDistance 측정 | Inspection Sequence (Measurement layer) | Halcon Algorithm (`VisionAlgorithmService.TryFitLine`) | 신규 `EdgeToLineDistanceMeasurement` 가 `MeasurementBase` 상속 → `TryExecute` 에서 TryFitLine 호출 |
| TeachingImagePath 자동 로드 | Inspection Sequence (`Action_FAIMeasurement.GrabOrLoadDatumImage`) | Data model (`DatumConfig.TeachingImagePath`) | Phase 22 인프라 소비 — sequence 액션 진입부에서 분기 |
| INI 직렬화 (EdgeToLineDistance) | Data model (`ParamBase` reflection) | — | 기존 자동 직렬화 — 코드 변경 0 |
| UI PropertyGrid 노출 (7 파라미터) | UI (`PropertyGrid` via `PropertyTools.Wpf`) | Data model (`MeasurementBase` 상속) | 기존 `[Category]` 자동 노출 — 새 코드 0 |
| FAI strip 색상 (녹/적) | Halcon Display Service (`HalconDisplayService.RenderDatumOverlay`) | Measurement (overlays 반환) | 기존 CO-05 경로 — RoiId suffix 패턴 자동 |

## Standard Stack

### Core (이미 프로젝트에 포함됨, 신규 의존성 0)

| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| `halcondotnet` | 24.11 Progress Steady | 에지 검출 (`MeasurePos`, `FitLineContourXld`), 변환 (`AffineTransPoint2d`) | [VERIFIED: WPF_Example/packages.config + DatumFindingService.cs] CLAUDE.md "Tech stack 변경 불가" |
| `PropertyTools.Wpf` | v3.1.0 | PropertyGrid 자동 노출 (`[Category]`, `[ItemsSourceProperty]`) | [VERIFIED: packages.config] |
| `Newtonsoft.Json` | v13.0.3 | 사용 없음 (INI 만) | [VERIFIED: 미사용] |

### Supporting

| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| — | — | 신규 추가 없음 | Phase 23 는 기존 인프라 100% 재사용 |

### Alternatives Considered

| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| `TryFitLine` 시그니처 확장 (selection 인자 추가) | `EdgeToLineDistance` 내부에서 `MeasurePos` 직접 호출 (rewrite) | 시그니처 확장이 변경 라인 적고 기존 caller 호환 가능 (default "all" 유지) → **recommended** |
| Y 부호 반전을 Measurement 클라이언트에서 처리 | `datumTransform` 자체를 Y-flip 으로 빌드 (DatumFindingService 변경) | 클라이언트 처리가 Datum 알고리즘 3개 모두 영향 없음 → **recommended** |
| `Action_FAIMeasurement.GrabOrLoadDatumImage` 본문 수정 | DatumFindingService 진입부에서 image 재로드 | GrabOrLoadDatumImage 가 이미 SIMUL 분기 단일 진입점 → **recommended** |

**Installation:** N/A (의존성 추가 없음)

**Version verification:** 신규 패키지 없음 → 검증 불필요.

## Architecture Patterns

### System Architecture Diagram

```
[Simul Image File] (D:\TestImg\Datameasurement\*.bmp, 사용자 제공)
        ↓
[Action_FAIMeasurement.EStep.DatumPhase]
        ↓
[GrabOrLoadDatumImage(parentSeq)]  ← Phase 23 수정 지점 (TeachingImagePath 분기)
        ↓ HImage
[InspectionSequence.TryRunDatumPhase]
        ↓ foreach (DatumConfig)
[DatumFindingService.TryFindDatum] ← AlgorithmTypeEnum switch
        ├─ TwoLineIntersect    (Line1 ∩ Line2)
        ├─ CircleTwoHorizontal (Circle center ∩ Horizontal A+B)         ← D-01 후보 (a) CTH
        └─ VerticalTwoHorizontal (Vertical ∩ Horizontal A+B)
        ↓ HTuple datumTransform (hom_mat2d)
[Action_FAIMeasurement.EStep.Grab]
        ↓
[ShotConfig.SetImage(InspectionImagePath)]  ← Phase 22 IMG-02 path
        ↓ HImage (memory buffer)
[Action_FAIMeasurement.EStep.Measure]
        ↓ foreach (FAI A1~A5) → foreach (Measurement)
[InspectionSequence.TryGetDatumTransform(meas.DatumRef)]
        ↓ HTuple transform
[EdgeToLineDistanceMeasurement.TryExecute]  ← Phase 23 신규
        ├─ VisionAlgorithmService.TryFitLine (Point ROI → fit line)  ← EdgeSelection 추가 필요
        ├─ midpoint (pRow, pCol) 추출
        ├─ AffineTransPoint2d(transform, pRow, pCol) → (datum_row, datum_col)
        ├─ resultValue = -datum_row * pixelResolution  ← D-02 +Y 부호 처리
        └─ overlays = (TBD — 빈 리스트 or minimal overlay)
        ↓ resultValue (mm)
[MeasurementBase.EvaluateJudgement] → IsPass
        ↓
[FAIMeasurementContext.InspectionOverlays] → suffix "-OK"/"-NG" 부여
        ↓
[MainView.halconViewer.SetInspectionOverlays]
        ↓
[HalconDisplayService.RenderDatumOverlay]  ← FAI-Edge*-OK/NG 녹/적 자동
        ↓
[Halcon HWindow 화면 표시 — 5개 strip 동시 (단, overlay 반환 시에만)]
```

### Recommended Project Structure (신규 파일 1개만)

```
WPF_Example/Custom/Sequence/Inspection/Measurements/
├── EdgePairDistanceMeasurement.cs          # 기존
├── PointToLineDistanceMeasurement.cs       # 기존, EdgeToLineDistance patron
├── PointToPointDistanceMeasurement.cs      # 기존
├── LineToLineAngleMeasurement.cs           # 기존
├── CircleDiameterMeasurement.cs            # 기존 (Phase 28)
├── LineToLineDistanceMeasurement.cs        # 기존
└── EdgeToLineDistanceMeasurement.cs        # ★ Phase 23 신규
```

### Pattern 1: MeasurementBase 상속 + Factory 등록

**What:** 새 측정 알고리즘은 `MeasurementBase` 를 상속하고 `MeasurementFactory.Create` switch 에 case 1줄 + `GetTypeNames()` 배열 1줄을 추가하면 INI/UI 자동 노출된다.
**When to use:** 모든 신규 측정 알고리즘.
**Example:**
```csharp
// Source: WPF_Example/Custom/Sequence/Inspection/MeasurementFactory.cs L14-30
// 추가 (예상 patch):
case "EdgeToLineDistance":
    return new EdgeToLineDistanceMeasurement(owner);
// GetTypeNames() 배열에:
"EdgeToLineDistance"
```

### Pattern 2: TryFitLine 재사용 + datumTransform 적용

**What:** Point ROI 의 에지 직선 fit 은 `VisionAlgorithmService.TryFitLine` 단일 헬퍼로 통일된다. datumTransform 이 null/empty 이면 원본 좌표, 아니면 ROI center 를 변환하여 fit.
**When to use:** Point/Line ROI 기반 모든 Measurement.
**Example:**
```csharp
// Source: WPF_Example/Custom/Sequence/Inspection/Measurements/PointToLineDistanceMeasurement.cs L66-74
var svc = new VisionAlgorithmService();
double pr1, pc1, pr2, pc2;
if (!svc.TryFitLine(image,
    Point_Row, Point_Col, Point_Phi, Point_Length1, Point_Length2,
    datumTransform,
    EdgeSampleCount, EdgeTrimCount, Sigma, EdgeThreshold,
    EdgeDirection, EdgePolarity,
    out pr1, out pc1, out pr2, out pc2, out error))
{
    return false;
}
double pRow = (pr1 + pr2) / 2.0;
double pCol = (pc1 + pc2) / 2.0;
```

**중요 (D-10 gap):** 위 호출은 `EdgeSelection` 인자를 받지 않는다. `MeasurePos` 의 4번째 인자가 하드코딩 `"all"` (VisionAlgorithmService.cs:95). EdgeToLineDistance 가 First/Last 명시를 만족하려면 TryFitLine 시그니처 확장 필요.

### Pattern 3: Datum-relative Y 좌표 추출

**What:** datumTransform 으로 변환된 좌표는 image row 기준 (아래쪽 양수). 공학 +Y (위쪽 양수) 로 표기하려면 부호 반전.
**When to use:** Datum 의 Y축 (Datum B) 까지 거리 측정.
**Example (예상 patch):**
```csharp
// EdgeToLineDistanceMeasurement.TryExecute 내부:
// fit 후 (pRow, pCol) = ROI center 의 image-frame 좌표
// transform 은 hom_mat2d = (image → datum-relative)
HTuple tRow, tCol;
HOperatorSet.AffineTransPoint2d(datumTransform, pRow, pCol, out tRow, out tCol);
// tRow = datum-frame 의 row 좌표 (image row 와 동일 부호 — 아래쪽 양수)
// D-02 +Y 부호 = Datum B 위쪽이 양수 = -tRow
resultValue = -tRow.D * pixelResolution; //260511 hbk Phase 23 ALG-01 — D-02 부호 반전
```

### Pattern 4: TeachingImagePath fallback 분기

**What:** Datum 찾기 단계에서 `DatumConfig.TeachingImagePath` 가 비어있지 않으면 그 이미지 우선 로드, 비어있으면 `ShotConfig.SimulImagePath` 폴백.
**When to use:** Phase 23 D-04 carry-over 구현.
**Example (예상 patch):**
```csharp
// Action_FAIMeasurement.cs:223 GrabOrLoadDatumImage 진입부:
private HImage GrabOrLoadDatumImage(InspectionSequence parentSeq) {
    if (ShotParam == null) return null;
    HImage image = null;
    //260511 hbk Phase 23 ALG-01 — TeachingImagePath 우선순위 분기 (Phase 22 carry-over)
    string teachingPath = null;
    if (parentSeq != null && parentSeq.DatumConfigs.Count > 0) {
        teachingPath = parentSeq.DatumConfigs[0].TeachingImagePath;
    }
    #if SIMUL_MODE
    if (!string.IsNullOrEmpty(teachingPath) && File.Exists(teachingPath)) {
        try { image = new HImage(teachingPath); } catch { image = null; }
    }
    // fallback: 기존 SimulImagePath 경로 (Phase 22 IMG-02 의미적 재해석)
    if (image == null && !string.IsNullOrEmpty(ShotParam.SimulImagePath) && File.Exists(ShotParam.SimulImagePath)) {
        try { image = new HImage(ShotParam.SimulImagePath); } catch { image = null; }
    }
    if (image == null) {
        image = SystemHandler.Handle.Devices.GrabHalconImage(ShotParam);
    }
    #else
    image = SystemHandler.Handle.Devices.GrabHalconImage(ShotParam);
    #endif
    return image;
}
```

### Anti-Patterns to Avoid

- **TryFitLine 의 selection 인자 무시:** memory feedback 위배. EdgeSelection 을 PropertyGrid 에 노출만 하고 실 코드에서 `"all"` 로 하드코딩하면 D-10 위배. 반드시 시그니처 확장 또는 직접 MeasurePos 호출로 명시 처리.
- **Datum 1개 (CTH) 인데 Datum 2개 추가:** D-01 결정 lock 전에 INI 구조 만들지 말 것. 사용자 답변 후 PLAN 작성.
- **+Y 부호 반전 누락:** image row 그대로 mm 로 표기 시 Datum B 위쪽 값이 음수 → 사용자 혼란. D-02 명시.
- **TeachingImagePath 미존재 시 예외:** 빈 문자열 → SimulImagePath 폴백 자동 (Phase 22 EnsurePerRoiDefaults 가 null 가드, 빈 문자열은 그대로 유지).
- **5개 strip 시각화 SC#2 모호:** EdgeToLineDistance 가 overlay 안 채우면 Halcon viewer 에 녹/적 라인 표시 안 됨. SC#2 정의 명확화 필요.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| INI 직렬화 | 수동 IniFile 읽기/쓰기 | `ParamBase` reflection (자동) | Phase 5 인프라. EdgeToLineDistance 의 7 필드 모두 자동 직렬화 |
| PropertyGrid 노출 | 별도 ViewModel/Window | `[Category("...")]` + `[ItemsSourceProperty]` | Phase 19 QUAL-03 + 기존 패턴 |
| 공차 판정 | 신규 IsPass 로직 | `MeasurementBase.EvaluateJudgement` | NominalValue/TolerancePlus/ToleranceMinus 자동 |
| 다형성 dispatch (INI Type 매핑) | switch / if 사슬 | `MeasurementFactory.Create(typeName, owner)` | 단일 진입점, 7번째 case 추가만 |
| TreeView 5개 strip 색상 | XAML DataTrigger 추가 | 기존 `HalconDisplayService` RoiId suffix 패턴 (FAI-Edge*-OK/-NG) | 단 overlay 가 반환되어야 동작 |
| Halcon hom_mat2d 변환 | 수동 행렬 곱셈 | `HOperatorSet.AffineTransPoint2d` | 표준 |
| Edge 옵션 ComboBox | 인라인 List<string> | `EdgeOptionLists.Directions/Selections/FAIPolarities` | 정적 readonly 캐시 (Phase 19 fix) |

**Key insight:** Phase 23 의 가치는 **1개 신규 파일 (`EdgeToLineDistanceMeasurement.cs`) + 2 라인 추가 (`MeasurementFactory`) + 1 진입부 분기 (`GrabOrLoadDatumImage`) + TryFitLine 시그니처 확장 1개** 로 100% 충족 가능하다는 점. 신규 추상화 0, 신규 UI 0, 신규 의존성 0.

## Runtime State Inventory

> Phase 23 은 **신규 코드 추가 phase** (rename/refactor/migration 아님) — 본 섹션은 형식상 빈 것으로 두고, 명확한 "없음" 으로 답함.

| Category | Items Found | Action Required |
|----------|-------------|------------------|
| Stored data | None — verified by grep | (해당 없음, 신규 알고리즘 추가만) |
| Live service config | None — verified by grep | (해당 없음) |
| OS-registered state | None — verified by grep | (해당 없음) |
| Secrets/env vars | None — verified by grep | (해당 없음) |
| Build artifacts | None — neue 파일은 csproj Compile ItemGroup 등록 필요 (CS0246 방지 — Phase 12-01 D-?? 패턴) | csproj 1줄 추가 (자동) |

## Common Pitfalls

### Pitfall 1: EdgeSelection PropertyGrid 노출만 하고 실 코드 미적용

**What goes wrong:** D-07 의 EdgeSelection 필드를 EdgeToLineDistance 에 추가하지만 TryFitLine 호출 시 무시됨 → 사용자가 First/Last 변경해도 동작 차이 없음.
**Why it happens:** `VisionAlgorithmService.TryFitLine` 시그니처에 selection 인자 없고 MeasurePos 4번째 인자 하드코딩 `"all"`.
**How to avoid:** TryFitLine 시그니처에 optional `string selection = "all"` 인자 추가 + `MeasurePos` 호출 시 그 값 전달. 기존 4 caller (PointToLineDistance, EdgePairDistance 등 — 단, EdgePairDistance 는 별도 서비스 사용) 는 default "all" 로 호환.
**Warning signs:** UAT 에서 EdgeSelection 을 First→Last 로 바꿔도 측정값 동일.

### Pitfall 2: datumTransform 적용 후 부호 반전 누락

**What goes wrong:** A1~A5 측정값이 Datum B 위쪽인데 음수로 나타남.
**Why it happens:** HALCON image row 좌표계 = 위쪽이 작은 값, 아래쪽이 큰 값. Datum-relative 좌표도 동일. D-02 공학 +Y 정의와 반대.
**How to avoid:** `resultValue = -tRow.D * pixelResolution` 명시 부호 반전.
**Warning signs:** UAT 에서 NominalValue 정의가 양수인데 측정값 음수 → 모두 NG.

### Pitfall 3: TeachingImagePath 빈 문자열 vs null

**What goes wrong:** Phase 22 EnsurePerRoiDefaults 가 `if (TeachingImagePath == null) TeachingImagePath = "";` 만 처리 (DatumConfig.cs:519). `string.IsNullOrEmpty` 가드 없이 `File.Exists(teachingPath)` 호출 시 빈 문자열도 false 반환 (안전) 하나, 코드 가독성 떨어짐.
**Why it happens:** 사용자가 INI 에서 TeachingImagePath 를 수동 비우거나 INI 키 미존재.
**How to avoid:** GrabOrLoadDatumImage 분기에서 `!string.IsNullOrEmpty(teachingPath) && File.Exists(teachingPath)` 2-step 가드.
**Warning signs:** TeachingImagePath 빈 문자열일 때 SimulImagePath 폴백 안 가고 GrabHalconImage 직행.

### Pitfall 4: SC#2 "5개 strip 동시 표시" 해석 모호

**What goes wrong:** SC#2 "OK/NG strip 녹/적" 을 "Halcon viewer 상 5개 색상 라인" 으로 기대했으나 EdgeToLineDistance 가 overlay 안 채워서 색상 라인 미표시.
**Why it happens:** 기존 5종 measurement 중 EdgePairDistance 만 FAIEdgeMeasurementService 통해 overlay 반환. PointToLineDistance 패턴 (`overlays = new List<>()` 빈) 을 그대로 따르면 색상 라인 0개.
**How to avoid:** 두 옵션 — (A) EdgeToLineDistance 에 minimal overlay 추가 (LineRow1/Col1/Row2/Col2 = Point ROI 위/아래 끝점), RoiId = "FAI-Edge1" (suffix 는 Action_FAIMeasurement L177-185 가 자동 부여); (B) SC#2 정의를 "InspectionListView TreeView 노드 색상" 으로 명확화 (단, TreeView 색상 binding 도 현재 없음). **Recommended: (A) minimal overlay**, RoiId="FAI-Edge1", 라인 = Point ROI fit 결과 그대로.
**Warning signs:** UAT 에서 5개 측정값은 OK/NG 출력되나 Halcon 화면에 녹/적 라인 0개.

### Pitfall 5: csproj Compile ItemGroup 누락 (Phase 12-01 D-?? 재발 방지)

**What goes wrong:** 신규 EdgeToLineDistanceMeasurement.cs 파일을 만들었으나 csproj 에 `<Compile Include="...">` 추가 안 함 → 빌드 시 CS0246 type not found.
**Why it happens:** MSBuild 15 classic-style csproj (SDK 아님) — 파일 자동 감지 안 됨.
**How to avoid:** EdgeToLineDistance.cs 추가 시 `WPF_Example/DatumMeasurement.csproj` Compile ItemGroup 에 1줄 추가.
**Warning signs:** msbuild Debug/x64 FAIL with CS0246 EdgeToLineDistanceMeasurement.

### Pitfall 6: Datum B(X horizontal) 측정 시 EdgeDirection 기본값 LtoR 부적합

**What goes wrong:** D-02 의 +Y 측정인데 EdgeToLineDistance 의 Point ROI 가 수평 에지 검출하려면 EdgeDirection = TtoB or BtoT (수직 traversal) 가 적합. 기본값 LtoR 면 수직 에지만 검출.
**Why it happens:** EdgeOptionLists.Directions = LtoR/RtoL/TtoB/BtoT. 수평 에지 검출 시 TtoB/BtoT 필요.
**How to avoid:** EdgeToLineDistance 기본값 = `EdgeDirection = "TtoB"` (Y방향 거리 측정의 의도와 일치). 단, 사용자가 PropertyGrid 에서 변경 가능.
**Warning signs:** UAT 에서 A1~A5 모두 "insufficient edge points" 에러.

### Pitfall 7: 마커 컨벤션 누락

**What goes wrong:** 신규 라인에 `//YYMMDD hbk` 마커 누락 → memory feedback 위배.
**Why it happens:** memory `feedback_comment_convention.md` 명시.
**How to avoid:** 모든 신규 라인에 `//260511 hbk Phase 23 ALG-01` 부착. 변경된 기존 라인은 기존 마커 보존 + 위에 stacking (Phase 20 D-12 패턴).

## Code Examples

### Operation 1: 신규 EdgeToLineDistanceMeasurement.cs 골격 (verified pattern)

```csharp
// Source: WPF_Example/Custom/Sequence/Inspection/Measurements/PointToLineDistanceMeasurement.cs (patron)
// Patch: 신규 파일 WPF_Example/Custom/Sequence/Inspection/Measurements/EdgeToLineDistanceMeasurement.cs

//260511 hbk Phase 23 ALG-01 — Datum-relative Y-distance measurement (single Point ROI fit + datumTransform Y extraction)
using System.Collections.Generic;
using HalconDotNet;
using PropertyTools.DataAnnotations;
using ReringProject.Halcon.Algorithms;
using ReringProject.Halcon.Models;

namespace ReringProject.Sequence
{
    /// <summary>
    /// Point ROI 에서 수평 에지 라인을 피팅하여 중점을 추출하고,
    /// datumTransform 으로 Datum-relative 좌표계로 변환한 후 row 좌표(부호 반전 = +Y 위쪽 양수)를
    /// "Datum B 까지 Y방향 거리" (mm) 로 리턴한다.
    /// 결과 단위: mm (pixelResolution 적용).
    /// </summary>
    public class EdgeToLineDistanceMeasurement : MeasurementBase //260511 hbk Phase 23 ALG-01
    {
        public override string TypeName { get { return "EdgeToLineDistance"; } }

        [Category("Point|ROI")] //260511 hbk Phase 23 ALG-01
        public double Point_Row { get; set; }
        public double Point_Col { get; set; }
        public double Point_Phi { get; set; }
        public double Point_Length1 { get; set; }
        public double Point_Length2 { get; set; }

        [Category("Edge")] //260511 hbk Phase 23 ALG-01
        public int EdgeThreshold { get; set; } = 10;
        public double Sigma { get; set; } = 1.0;
        public int EdgeSampleCount { get; set; } = 20;
        public int EdgeTrimCount { get; set; } = 10;
        [ItemsSourceProperty(nameof(EdgePolarityList))]
        public string EdgePolarity { get; set; } = "DarkToLight";
        [ItemsSourceProperty(nameof(EdgeDirectionList))]
        public string EdgeDirection { get; set; } = "TtoB"; //260511 hbk Phase 23 ALG-01 — 수평 에지 검출 default
        [ItemsSourceProperty(nameof(EdgeSelectionList))] //260511 hbk Phase 23 ALG-01 — D-10 memory feedback
        public string EdgeSelection { get; set; } = "First";

        [PropertyTools.DataAnnotations.Browsable(false)]
        public List<string> EdgeDirectionList { get { return EdgeOptionLists.Directions; } }
        [PropertyTools.DataAnnotations.Browsable(false)]
        public List<string> EdgePolarityList { get { return EdgeOptionLists.FAIPolarities; } }
        [PropertyTools.DataAnnotations.Browsable(false)]
        public List<string> EdgeSelectionList { get { return EdgeOptionLists.Selections; } }

        public EdgeToLineDistanceMeasurement(object owner) : base(owner) { } //260511 hbk Phase 23 ALG-01

        public override bool TryExecute(
            HImage image,
            HTuple datumTransform,
            double pixelResolution,
            out double resultValue,
            out string error,
            out List<EdgeInspectionOverlay> overlays)
        {
            resultValue = 0;
            error = null;
            overlays = new List<EdgeInspectionOverlay>();

            // Datum 미설정 + identity 인 경우는 PointToLineDistance 패턴과 동일하게 통과 가능.
            // datumTransform 자체가 null/empty 면 identity → 원본 image-row 좌표 (Datum 기준 0이 image top).
            // D-11: 호출측 (InspectionSequence.TryGetDatumTransform) 이 DatumRef 매칭 실패 시 identity 반환.
            //       Datum 찾기 단계 실패는 EStep.DatumPhase 에서 FinishAction(Error) 처리 → 이 메서드 도달 안 함.

            var svc = new VisionAlgorithmService();
            double pr1, pc1, pr2, pc2;
            // TryFitLine 시그니처 확장 후 (selection 인자 전달) — Phase 23 의 dependent task
            if (!svc.TryFitLine(image,
                Point_Row, Point_Col, Point_Phi, Point_Length1, Point_Length2,
                datumTransform,
                EdgeSampleCount, EdgeTrimCount, Sigma, EdgeThreshold,
                EdgeDirection, EdgePolarity, EdgeSelection, //260511 hbk Phase 23 ALG-01 — EdgeSelection 명시 (D-10)
                out pr1, out pc1, out pr2, out pc2, out error))
            {
                return false;
            }
            double pRow = (pr1 + pr2) / 2.0;
            double pCol = (pc1 + pc2) / 2.0;

            //260511 hbk Phase 23 ALG-01 — Datum-relative Y 좌표 추출 + D-02 부호 반전 (image row → +Y 위쪽 양수)
            double datumRow = pRow;
            if (datumTransform != null && datumTransform.Length > 0)
            {
                try
                {
                    HTuple tRow, tCol;
                    HOperatorSet.AffineTransPoint2d(datumTransform, pRow, pCol, out tRow, out tCol);
                    datumRow = tRow.D;
                }
                catch
                {
                    // transform 실패 시 원본 좌표 사용 (TryFitLine 패턴 일관성)
                }
            }
            resultValue = -datumRow * pixelResolution; //260511 hbk Phase 23 ALG-01 — D-02 +Y 부호 (위쪽 양수)

            // Optional: minimal overlay for green/red strip 시각화 (Pitfall 4 fix)
            // overlays.Add(new EdgeInspectionOverlay {
            //     RoiId = "FAI-Edge1",
            //     LineRow1 = pr1, LineColumn1 = pc1, LineRow2 = pr2, LineColumn2 = pc2
            // });

            return true;
        }
    }
}
```

### Operation 2: MeasurementFactory + GetTypeNames() 확장 (verified)

```csharp
// Source: WPF_Example/Custom/Sequence/Inspection/MeasurementFactory.cs L14-44
// Patch (planner 결정 위치):

switch (typeName) {
    case "EdgePairDistance":      return new EdgePairDistanceMeasurement(owner);
    case "PointToLineDistance":   return new PointToLineDistanceMeasurement(owner);
    case "PointToPointDistance":  return new PointToPointDistanceMeasurement(owner);
    case "LineToLineAngle":       return new LineToLineAngleMeasurement(owner);
    case "CircleDiameter":        return new CircleDiameterMeasurement(owner);
    case "LineToLineDistance":    return new LineToLineDistanceMeasurement(owner);
    case "EdgeToLineDistance":    return new EdgeToLineDistanceMeasurement(owner); //260511 hbk Phase 23 ALG-01
    default:                       return null;
}

// GetTypeNames():
return new string[] {
    "EdgePairDistance",
    "PointToLineDistance",
    "PointToPointDistance",
    "LineToLineAngle",
    "CircleDiameter",
    "LineToLineDistance",
    "EdgeToLineDistance" //260511 hbk Phase 23 ALG-01
};
```

### Operation 3: VisionAlgorithmService.TryFitLine 시그니처 확장 (recommended)

```csharp
// Source: WPF_Example/Halcon/Algorithms/VisionAlgorithmService.cs L18-95
// Patch:

public bool TryFitLine(
    HImage image,
    double roiRow, double roiCol, double roiPhi,
    double roiLength1, double roiLength2,
    HTuple datumTransform,
    int sampleCount, int trimCount, double sigma, int threshold,
    string direction, string polarity,
    string selection,  //260511 hbk Phase 23 ALG-01 — D-10 memory feedback (optional 인자 추가)
    out double row1, out double col1, out double row2, out double col2,
    out string error)
{
    // ... 기존 본문 그대로 ...
    // L95 MeasurePos 호출 변경:
    string measureSel = "all"; //260511 hbk Phase 23 ALG-01 — 기존 default 유지
    if (string.Equals(selection, "First", System.StringComparison.OrdinalIgnoreCase)) measureSel = "first";
    else if (string.Equals(selection, "Last", System.StringComparison.OrdinalIgnoreCase)) measureSel = "last";
    HOperatorSet.MeasurePos(image, measureHandle,
        Math.Max(0.4, sigma), Math.Max(1, threshold),
        pol, measureSel, out rows, out cols, out amp, out dist); //260511 hbk Phase 23 ALG-01
    // ... 이하 동일 ...
}

// 기존 4 caller (PointToLineDistanceMeasurement L66 + L79, 기타) 는 selection="all" 추가하거나
// **default 인자 사용 옵션** (C# 7.2 지원) 으로 호환 유지 가능.
// 권장: optional param `string selection = "all"` 로 default 부여 → caller 무수정.
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| EEdgeMeasureType (enum) | EdgeDirection/Selection/SampleCount/TrimCount/Polarity (string) | Quick 260409-e3v | EdgeToLineDistance 동일 패턴 |
| FAIConfig 단독 ROI + 측정 알고리즘 인라인 | MeasurementBase 다형성 + Factory | Phase 6 (260413) | 신규 측정 1 클래스 추가만 |
| 수동 INI 직렬화 | ParamBase reflection 자동 | Phase 5 (260409) | 코드 0 |
| Datum 단일 알고리즘 (TLI) | TLI/CTH/VTH 3-way dispatch | Phase 12 (260423) | Phase 23 는 1개 algorithm 선택만 |
| TeachingImagePath 미분리 | TeachingImagePath (Phase 22) + InspectionImagePath (의미적 재해석) | Phase 22 (260511) | Phase 23 가 소비측 신규 분기 |
| EdgeSelection 노출 안 함 | DatumConfig 에 노출 (Phase 15) | Phase 15 (260429) | FAI Measurement 에는 미적용 — Phase 23 이 차이 해소 |

**Deprecated/outdated:**

- `FAIConfig.EdgeSelection` (L70) — `EdgeMeasureType` 가 비-CircleDiameter 인 경우에만 의미. PropertyGrid 노출은 ICustomTypeDescriptor 가 처리 (Phase 19). `MeasurementBase.TryExecute` 에는 전달되지 않음 (별도 EdgeSelection 인스턴스 필드 사용).

## Assumptions Log

> 아래 항목은 사용자 확인 후 lock 가능. discuss-phase 에서 처리 권장.

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | **D-01 = CircleTwoHorizontal (CTH, Datum 1개)** 권장. B1 홀 + 수평 2-ROI 로 origin = circle center + Y축 = horizontal line, 즉 Datum B(X horizontal) 와 C(Y vertical) 가 단일 알고리즘으로 동시 정의됨. | User Constraints D-01 | 매핑 잘못되면 PLAN 의 INI 구조 + DatumConfig 알고리즘 선택 + UAT 시 측정값 부호 모두 영향. **Severity: HIGH** |
| A2 | EdgeToLineDistance 의 EdgeDirection default = "TtoB" (수평 에지 검출, Y방향 거리 측정 의도) | Common Pitfalls #6 | 잘못 시 UAT 0번째에 "insufficient edge points" 에러, 사용자가 PropertyGrid 에서 변경하면 해결 (낮은 risk) |
| A3 | EdgeToLineDistance 의 EdgeSelection default = "First" (DatumConfig 기존 default 와 일치) | Common Pitfalls #6 | 사용자가 PropertyGrid 에서 변경 가능. 낮은 risk |
| A4 | TryFitLine 시그니처 확장 (optional `selection = "all"` 추가) 시 기존 caller (PointToLineDistance L66, L79 등) 호환 — default 인자로 무수정 | Code Examples Op#3 | 시그니처 확장이 C# 7.2 default param 지원 → 무수정 보장. 낮은 risk |
| A5 | SC#2 strip 시각화 = minimal overlay (RoiId="FAI-Edge1") 추가 권장. 기존 CO-05 패턴 자동 연결 | Common Pitfalls #4 | overlay 미추가 시 Halcon viewer 상 색상 라인 0 — UAT 시 사용자가 "녹/적 안 보임" 발견 가능. discuss-phase 에서 명확화 권장 |
| A6 | `GrabOrLoadDatumImage` 에서 `DatumConfigs[0].TeachingImagePath` 만 참조 (다중 Datum 시 첫 번째 만) | Pattern 4 | Phase 23 D-03 = Top Fixture #1 단독 + Datum 1개 가정 → 영향 없음. 다중 Datum 시 검토 필요 (deferred) |
| A7 | A1~A5 의 EdgeDirection 은 모두 동일 (TtoB or BtoT — 수평 에지 검출). PropertyGrid 에서 5개 각각 독립 설정 가능 | Architecture Pattern 2 | A1~A5 가 각각 다른 방향 에지 검출이면 INI 에 각각 다른 EdgeDirection 설정 — D-15 인프라가 자동 지원 |

**즉 이 RESEARCH 의 핵심 가정 = A1 (Datum 알고리즘 1개 = CTH)** 이며, 이 1개 답변으로 Phase 23 lock 완료. 나머지 가정은 PropertyGrid 에서 사용자 조정 가능하므로 risk 낮음.

## Open Questions

1. **PPT(Datum_정보_260511_2D) 의 Datum B/C 매핑 (D-01 lock-in)** [SEVERITY: HIGH]
   - What we know: 위치 `D:\TestImg\Datameasurement\` — 현재 `teaching.bmp` (2022-12-07, 41 MB) 만 존재. PPT 파일 미존재 (검증: `Get-ChildItem`).
   - What's unclear: Datum 표현 방식 (1개 CTH vs 2개 TLI/CTH/VTH), B1 홀 위치 + 반경, 수평 ROI 2개 위치, A1~A5 ROI 위치 + 공차 (NominalValue/TolerancePlus/ToleranceMinus).
   - Recommendation: **discuss-phase 에서 사용자에게 1줄 질문**:
     - "Top Fixture #1 의 Datum 정의 = (a) 1개 = CircleTwoHorizontal (B1 홀 + 수평 2-ROI) / (b) 2개 = B(TwoLineIntersect) + C(CircleTwoHorizontal) / (c) 2개 = B(VerticalTwoHorizontal) + C(... 등). 어느 쪽?"
     - A1~A5 ROI 좌표 + 공차는 사용자가 셋업 시 PropertyGrid 에서 직접 입력 가능 → PPT 없이도 코드 작성 가능. 검증은 사용자가 Simul 이미지 + INI 셋업 후 UAT.
   - Fallback: A1 (CTH 1개) 로 lock 후 진행. UAT 시 결과 부적합하면 carry-over.

2. **SC#2 의 "5개 strip 동시 표시" 정확한 정의**
   - What we know: 기존 CO-05 = HalconDisplayService L167-180 의 RoiId suffix 패턴 (FAI-Edge*-OK/NG → 녹/적). 5종 measurement 중 EdgePairDistance 만 overlay 채움.
   - What's unclear: EdgeToLineDistance 의 PointToLineDistance 패턴 (overlay 빈) 채택 vs minimal overlay 추가.
   - Recommendation: Pitfall 4 의 옵션 (A) — minimal overlay (`RoiId="FAI-Edge1"`, Line = fit 결과) 추가. PLAN 에 명시.

3. **EdgeSelection 명시 처리 — TryFitLine 시그니처 확장 vs 직접 MeasurePos 호출**
   - What we know: 시그니처 확장이 변경 라인 적음 (TryFitLine 1 함수만), 기존 caller 호환 (default 인자).
   - What's unclear: 다른 caller (EdgePairDistanceMeasurement 등) 가 EdgeSelection 명시 처리를 곧 따라가야 하는지.
   - Recommendation: Phase 23 범위는 TryFitLine 시그니처 확장만 + 기존 caller 는 default "all" 유지. EdgePairDistance 등의 EdgeSelection 명시는 별도 phase 또는 carry-over.

4. **Datum 찾기 실패 시 5개 strip 모두 빨강 — UAT 검증 방법**
   - What we know: D-11 = TryExecute → false + error="Datum not found"`. Action_FAIMeasurement L171-175 가 `ClearResult + LastJudgement=false`.
   - What's unclear: Halcon viewer 에 빨강 라인 표시 vs InspectionListView 노드 색상 vs 그냥 메시지만.
   - Recommendation: Datum 실패 시 `EdgeToLineDistance` 가 false 반환 → Action_FAIMeasurement 가 NG 처리 → CO-05 자동 빨강. 단, overlay 가 채워졌을 때만 시각 빨강. discuss-phase 에서 사용자 기대 확인.

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| Halcon 24.11 Progress Steady | EdgeToLineDistance + DatumFinding | ✓ | 24.11 | — |
| .NET Framework 4.8 + C# 7.2 | csproj 통일 | ✓ | 4.8 | — |
| MSBuild 15 | Debug/x64 빌드 | ✓ | 15.0 | — |
| Simul 이미지 (`D:\TestImg\Datameasurement\*.bmp`) | UAT | ✓ (`teaching.bmp` 만) | — | 사용자가 PPT 도면 반영 신규 이미지 제공 (D-16) |
| **PPT(Datum_정보_260511_2D)** | D-01 lock-in | **✗** | — | **사용자 1줄 답변 (discuss-phase)** |

**Missing dependencies with fallback:**
- PPT — discuss-phase 에서 사용자에게 직접 질문하여 1줄 답변 받음. recommended A1 = CTH 1개.

**Missing dependencies blocking:**
- (없음 — 모두 fallback 존재)

## Validation Architecture

### Test Framework

| Property | Value |
|----------|-------|
| Framework | 없음 — 프로젝트에 자동화 테스트 프레임워크 없음 (CLAUDE.md 명시: "No test framework detected") |
| Config file | 없음 |
| Quick run command | `MSBuild WPF_Example\DatumMeasurement.csproj /p:Configuration=Debug /p:Platform=x64 /v:minimal /t:Rebuild` (build-only validation) |
| Full suite command | SIMUL_MODE 수동 UAT (사용자 인터랙티브) |

### Phase Requirements → Test Map

| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| ALG-01 | Simul 이미지 로드 → Datum B/C 찾기 → A1~A5 측정 (mm) | UAT (manual SIMUL) | N/A — SIMUL_MODE 실행 후 사용자 검증 | ❌ Wave 0 — 23-UAT.md 미작성 |
| ALG-01 (SC#2) | A1~A5 OK/NG strip 녹/적 | UAT (manual visual) | N/A — Halcon viewer 시각 확인 | ❌ Wave 0 |
| ALG-01 (SC#3) | TeachingImagePath ≠ InspectionImagePath 분리 동작 | UAT (manual) | N/A — INI 두 파일 분리 후 검증 | ❌ Wave 0 |
| ALG-01 (SC#4) | A6 1개 추가 (INI 직접 편집 + UI 'Add FAI' 둘 다) → 6개 측정값 표시 | UAT (manual extensibility) | N/A — INI/UI 둘 다 시도 | ❌ Wave 0 |
| ALG-01 (SC#5) | msbuild Debug/x64 PASS + 신규 warning 0 (Phase 21 baseline 6 occurrences) | Build | `MSBuild ... /t:Rebuild` + grep `warning` count | ✅ — build_23.log 생성 |

**Sampling Rate:**
- **Per task commit:** msbuild Debug/x64 Rebuild (각 task 완료 시 — 약 30~60초)
- **Per wave merge:** msbuild + SIMUL_MODE 수동 1회 검사 (Datum 찾기 + 1개 FAI 측정 정상 동작 확인)
- **Phase gate:** 23-UAT.md 5/5 (SC#1~SC#5) PASS + 사용자 사인오프 (`/gsd-verify-work`)

### Wave 0 Gaps

- [ ] `.planning/phases/23-top-1-a-simul-end-to-end-2026-05-11/23-UAT.md` — 5 시나리오 (SC#1~SC#5) scaffold
- [ ] Simul 이미지 사용자 제공 — D-16 (사용자 셋업)
- [ ] PPT 답변 받기 — D-01 (discuss-phase 또는 quick AskUser)
- [ ] 측정 정밀도 0.001mm 검증 방법: NominalValue+TolerancePlus 입력 후 측정값 출력 정밀도 확인 (UI 표기 자릿수 + INI 저장 자릿수 둘 다)
- [ ] A6 확장성 검증: UAT 단계에서 INI 수동 추가 + 재기동 + UI 추가 둘 다 시도

## Project Constraints (from CLAUDE.md)

- **Tech stack lock:** .NET Framework 4.8 + WPF + Halcon 24.11 — 변경 불가
- **Architecture lock:** SystemHandler 싱글턴 + SequenceBase/ActionBase 패턴 유지 — Phase 23 는 MeasurementBase 단일 추가만
- **INI 하위 호환:** IsDynamicFAIMode 분기 유지 — Phase 23 신규 measurement 도 같은 포맷
- **Hardware:** HIK 카메라 SDK (MvCamCtrl.Net), 실제 테스트는 SIMUL_MODE 대체 (Phase 23 = SIMUL 전용)
- **C# 7.2:** nullable refs / switch expressions / records 금지. default param 은 사용 가능 (TryFitLine 확장 시 활용)
- **GSD Workflow enforcement:** 직접 repo edit 금지 — `/gsd:execute-phase` 진행
- **Logging:** Logging.PrintLog((int)ELogType.Error/Trace, ...) — 에러는 catch 직후 로그
- **Style:** 편집 대상 파일 스타일 따름 (Allman vs K&R 혼재). EdgeToLineDistance.cs 신규 = PointToLineDistance.cs 와 동일 Allman.
- **Comment convention:** `//260511 hbk Phase 23 ALG-01` 마커 필수 (memory feedback_comment_convention).

## Security Domain

> Phase 23 = 신규 측정 알고리즘 + 이미지 로드 분기 추가. 외부 입력 = INI 파일 경로 (TeachingImagePath), Simul 이미지 파일. 인증/세션/데이터 영속성 변경 없음.

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V2 Authentication | no | (LoginManager 기존 유지) |
| V3 Session Management | no | (WPF 데스크탑 — N/A) |
| V4 Access Control | no | (단일 사용자 데스크탑) |
| V5 Input Validation | yes | `File.Exists()` 가드 + try/catch `new HImage(...)` (기존 패턴) |
| V6 Cryptography | no | (해당 없음) |

### Known Threat Patterns for WPF + Halcon

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| 잘못된 INI 경로 (`TeachingImagePath`) 로 임의 파일 접근 | Tampering | `File.Exists` + try/catch `new HImage` (HALCON 자체 오류 시 image=null 반환). 단일 사용자 데스크탑이므로 실 위협 낮음 |
| Halcon 예외 미처리 → 시퀀스 스레드 크래시 | Denial of Service | try/catch (Exception ex) → return false (CLAUDE.md 패턴). EdgeToLineDistance.TryExecute 도 동일 |
| Datum 첫기 실패 후 stale transform 사용 | Tampering | `InspectionSequence.TryRunDatumPhase` 실패 시 `FinishAction(Error)` (Action_FAIMeasurement L92-95) → Measure 단계 도달 안 함 |

## Sources

### Primary (HIGH confidence)

- `WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs` — TeachingImagePath L35, EnsurePerRoiDefaults L476-579, ICustomTypeDescriptor L586-643
- `WPF_Example/Custom/Sequence/Inspection/EDatumAlgorithm.cs` — 3 enum values
- `WPF_Example/Custom/Sequence/Inspection/MeasurementBase.cs` — TryExecute 시그니처 + EvaluateJudgement
- `WPF_Example/Custom/Sequence/Inspection/MeasurementFactory.cs` — switch + GetTypeNames
- `WPF_Example/Custom/Sequence/Inspection/Measurements/PointToLineDistanceMeasurement.cs` — patron
- `WPF_Example/Halcon/Algorithms/VisionAlgorithmService.cs` — TryFitLine L18-123 (EdgeSelection 누락 verified)
- `WPF_Example/Halcon/Algorithms/DatumFindingService.cs` — 3 알고리즘 dispatch (TLI L68 / CTH L187 / VTH L363)
- `WPF_Example/Halcon/Display/HalconDisplayService.cs:167-180` — FAI-Edge*-OK/NG 색상
- `WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs` L223-242 — GrabOrLoadDatumImage 진입점
- `WPF_Example/Custom/Sequence/Inspection/InspectionRecipeManager.cs` — Phase 6 INI 포맷
- `WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs:144-180` — TryRunDatumPhase + TryGetDatumTransform
- `WPF_Example/Custom/Sequence/Inspection/FAIConfig.cs` — EdgeMeasureType ItemsSource (MeasurementFactory.GetTypeNames 단일 소스)
- `WPF_Example/Custom/Sequence/Inspection/EdgeOptionLists.cs` — Selections L22 = First/Last/All
- `.planning/phases/22-image-dual-structure/22-02-SUMMARY.md` — Phase 22 carry-over 명시 (Phase 23 TeachingImagePath 자동 로드)
- `.planning/phases/21-memory-image-buffer/21-CONTEXT.md` — ShotConfig._image lifetime 계약
- `.planning/phases/23-top-1-a-simul-end-to-end-2026-05-11/23-CONTEXT.md` — D-01~D-19 user decisions

### Secondary (MEDIUM confidence)

- `WPF_Example/UI/ControlItem/InspectionListView.xaml.cs` — TreeView SelectionChanged + PropertyGrid 갱신 (Quick 260511-ucv)
- `WPF_Example/UI/ControlItem/InspectionListView.xaml` — TreeListBox + PropertyGrid 구조

### Tertiary (LOW confidence — 사용자 확인 필요)

- PPT(Datum_정보_260511_2D) — **미존재** (`D:\TestImg\Datameasurement\teaching.bmp` 41 MB 만)
- D-01 lock-in 의 권장 (A1 = CTH 1개) — research 추론, 사용자 확인 필요

## Metadata

**Confidence breakdown:**

- Standard stack: HIGH — 기존 의존성 100% 재사용, 신규 추가 0
- Architecture: HIGH — MeasurementBase 패턴 + Factory + ParamBase reflection 완전 검증
- Pitfalls: HIGH — EdgeSelection gap, +Y 부호, TryFitLine selection 누락 모두 코드에서 직접 확인
- D-01 PPT 매핑: **PENDING-USER** — 사용자 1줄 답변 필요
- SC#2 strip 시각화 해석: MEDIUM — discuss-phase 에서 명확화 권장

**Research date:** 2026-05-11
**Valid until:** 30 days (코드 안정 — `.NET 4.8 + Halcon 24.11` 변경 없음 가정)
