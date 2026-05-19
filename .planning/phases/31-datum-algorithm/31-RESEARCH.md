# Phase 31: Datum 기준 측정 알고리즘 확장 — Research

**Researched:** 2026-05-19
**Domain:** HALCON 기반 WPF 비전 검사 — 신규 측정 타입 6종 + CO-23.1-01/02 carry-over
**Confidence:** HIGH (코드 직접 검증) / MEDIUM (MSOP 원문 재확인 포함)

---

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

- **D-01:** 신규 측정 타입 6종(E8 / D1·H5 / I9·I10 / CompoundAngle 계열 / ArcEdgeDistance) 전부 Phase 31 단일 phase 에서 구현. plan-phase 가 plan 단위 분할.
- **D-02:** CO-23.1-01(듀얼 이미지 뷰어) + CO-23.1-02(측정 타입별 Rect ROI 버튼 일반화) 둘 다 Phase 31 포함. CO-23.1-02 가 선행조건 — 알고리즘 구현 전에 처리.
- **D-03:** Datum 절대좌표 주입 = 인터페이스 도입 (`IDatumOriginConsumer`). `Action_FAIMeasurement` 의 `meas as EdgeToLineDistanceMeasurement` 하드코딩 → `meas as IDatumOriginConsumer` 단일 분기로 일반화.
- **D-04:** 거리 계산 로직 = EdgeToLineDistance 의 `projection_pl + MeasureAxis X/Y + 부호 처리` 블록(L126~196)을 공용 함수로 추출 후 신규 거리 타입이 재사용.
- **D-05:** D1·H5 = 신규 타입 `EdgeToLineAngle`. ROI 1개 직선 피팅 + Datum A 기준선 각도.
- **D-06:** D1/H5 의 Datum A 공급 = 기존 `DatumConfig.DetectedRefAngle` 경로 재사용.
- **D-07:** CompoundAngle 출력 = E2 각도(degree) / E9·E10 거리(mm). E9 = Datum C 기준, E10 = Datum B 기준.
- **D-08:** ArcEdgeDistance(G1·G2·G5~G8·G11·G12) = 신규 타입. 측정점 P1 = 호(arc) 위의 점.
- **D-09:** 다단계 기하 구성(중간선·교점·중점) = measurement 내부에서 일괄 계산. 사용자 입력 ROI 만 티칭.
- **D-10:** 호∩라인 교점이 2개일 때 → ROI 내부의 해 선택. 별도 파라미터 노출 없음.
- **D-11:** E2(각도) / E9·E10(거리) = 별도 측정 타입으로 분리. `MeasurementFactory` 에 각각 등록.

### Claude's Discretion (researcher/planner 위임)

- 인터페이스명(`IDatumOriginConsumer` 등) / 거리 계산 공용 함수의 위치·시그니처·명명.
- 신규 측정 타입 클래스명 (E8 / I9·I10 / CompoundAngle 계열 / ArcEdgeDistance — `EdgeToLineAngle` 외).
- 호 피팅 HALCON 연산자 선택 (3점 호 피팅 vs N점 contour 피팅).
- ArcEdgeDistance 의 호 위 측정점 정의.
- E9/E10 과 E2 사이 기하 구성 체인의 공유 범위.
- 신규 타입에 `ICustomTypeDescriptor` 적용 여부.
- CO-23.1-02 ROI 버튼 일반화 구현 형태 / CO-23.1-01 듀얼 이미지 뷰어 UI 형태.
- 신규 타입 PropertyGrid Category 구성 및 ROI 필드 명명.

### Deferred Ideas (OUT OF SCOPE)

- SOP FAI 인벤토리 전수 실측 검증 — 별도 phase.
- Side 카메라 검사 흐름 e2e — Phase 24 영역.
- 호∩라인 교점의 사용자 선택 옵션(Near/Far) — D-10 은 ROI 내부 자동 선택.
- CompoundAngle 중간 산출물의 INI/UI 노출 — D-09 는 내부 처리.

</user_constraints>

---

## Summary

Phase 31 은 Phase 23.1 이 완성한 `EdgeToLineDistanceMeasurement` 구조를 6종의 신규 측정 타입으로 확장한다. 핵심 접근은 **"새 계산식을 만들지 않고, 검증된 로직을 공용화(D-04)하고 타입을 늘린다(D-01)"** 이다.

MSOP 원본(pdf_text.txt) 재확인으로 세 가지 핵심 불확실성이 해소되었다.

1. **ArcEdgeDistance(G 시리즈) 측정점 정의**: MSOP 원본(p.104~111, G1~G12)은 절차가 `"1. Capture points: P1"` 단 한 줄이다. P1 을 캡처하는 방법에 대한 arc 피팅/특정 각도 지정 없이 단순 "포인트 P1 취득 → Datum C→X 거리" 로 기술된다. SOP 데크(슬라이드 43)가 G 시리즈를 "ArcEdgeDistance" 알고리즘으로 분류했으나, MSOP 원본 절차 자체는 `EdgeToLineDistance` 와 절차적으로 동일하다. **결론:** ArcEdgeDistance = EdgeToLineDistance 와 동일한 알고리즘(에지 라인 피팅 + Datum 거리). 별도 타입으로 등록(D-08 유지)하되 `TryExecute` 내부 로직은 EdgeToLineDistance 와 동일하게 공용 헬퍼를 재사용하면 된다. "호 위의 점"이 특별한 arc 피팅을 요구하지 않는다 — ROI 를 P1 위치(호의 엣지)에 그리면 일반 라인 피팅이 그 에지를 검출한다.

2. **E9/E10 절차 확정**: MSOP p.92(E9)는 `"9. Calculate: Measure the distance from Datum C to Pc"` 이며 평가 방법란에는 `"Measure the Angle from Datum C to Pc"` 라는 오기(pdftotext 렌더 부적합 가능성 있음)가 병기된다. D-07 결정("E9 = Datum C 기준 거리")이 절차 본문 9번("distance")과 일치한다. E10 도 동일: `"Measure the distance from Datum B to Pc"`. **결론:** D-07 확정 — E9 거리(Datum C) / E10 거리(Datum B) / E2 각도(Datum B). 평가방법란의 "Angle" 표기는 pdftotext 혼선이며 절차 본문이 우선.

3. **HALCON 호 피팅 연산자 선택**: I9/I10 의 3점 arc 피팅과 E2/E9/E10 의 N점 circle 피팅 모두 `HOperatorSet.FitCircleContourXld` 를 사용한다(기존 `CircleDiameterMeasurement.TryFindCircle` 이 동일 연산자 사용, [VERIFIED: VisionAlgorithmService.cs L292]). 3점 피팅은 정확히 3점이 주어지면 외접원이므로 `GenContourPolygonXld(3점) → FitCircleContourXld` 로 처리 가능. I9/I10 에서는 원 중심+반지름을 구한 후 원과 라인의 교점을 별도 연산으로 구해야 한다.

**Primary recommendation:** D-03 `IDatumOriginConsumer` 인터페이스를 먼저 구현하여 `Action_FAIMeasurement` Datum 주입 경로를 일반화한 뒤, D-04 공용 거리 헬퍼를 `VisionAlgorithmService` 에 정적 메서드로 추출하고, 신규 타입 6종을 순차 추가한다. CO-23.1-02 버튼 일반화는 Wave 0 으로 선행처리한다.

---

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| IDatumOriginConsumer 인터페이스 정의 | Measurement layer | — | 측정 객체가 datum 주입을 직접 수신하는 계층 |
| Datum 절대좌표 주입 (D-03 일반화) | Inspection Sequence (`Action_FAIMeasurement`) | Measurement layer (`IDatumOriginConsumer`) | 기존 L161~185 하드코딩 → 인터페이스 분기 1개로 교체 |
| projection_pl 공용 거리 계산 (D-04) | Halcon Algorithm (`VisionAlgorithmService` static) | Measurement layer (caller) | 검증된 계산식 재사용. static 메서드가 적합 (VisionAlgorithmService 이미 static 헬퍼 보유) |
| E8 원중심 거리 | Measurement layer (`CircleCenterDistanceMeasurement`) | `VisionAlgorithmService.TryFindCircle` | 원 피팅 → 중심점 → 공용 거리 계산 |
| D1/H5 EdgeToLineAngle | Measurement layer (`EdgeToLineAngleMeasurement`) | `VisionAlgorithmService.TryFitLine` + `AngleLineLine` | 직선 피팅 → Datum 기준선과의 각도 |
| I9/I10 호∩라인 교점 거리 | Measurement layer (`ArcLineIntersectDistanceMeasurement`) | `VisionAlgorithmService` (신규 호 피팅 + 교점) | 3점 arc fit → 라인 fit → 교점 → 공용 거리 계산 |
| E2 CompoundAngle 각도 | Measurement layer (`CompoundAngleMeasurement`) | `VisionAlgorithmService.TryFindCircle`, `IntersectLines`, `AngleLineLine` | 다단계 기하 체인 → 최종 라인 → 각도 |
| E9 CompoundCenterDistance(C) / E10 CompoundCenterDistance(B) | Measurement layer (각각 별도 타입) | `VisionAlgorithmService.TryFindCircle`, `IntersectLines`, 공용 거리 | D-11: 별도 타입, 동일 기하 체인 |
| ArcEdgeDistance G 시리즈 | Measurement layer (`ArcEdgeDistanceMeasurement`) | `VisionAlgorithmService.TryFitLine` | MSOP 확인: EdgeToLineDistance 와 동일 알고리즘 — 타입명만 다름 |
| CO-23.1-02 ROI 버튼 일반화 | UI (`MainView.xaml.cs`) | Measurement layer (타입 쿼리) | `FindSelectedEdgeToLineMeasurement` → `FindSelectedRectMeasurement(MeasurementBase)` 일반화 |
| CO-23.1-01 듀얼 이미지 뷰어 | UI (`MainView.xaml.cs`) | Data model (`DatumConfig.TeachingImagePath`, `ShotConfig.SimulImagePath`) | TeachingImage 별도 뷰어 패널 표시 |

---

## Standard Stack

### Core (기존 프로젝트 의존성 — 신규 추가 없음)

| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| `halcondotnet` | 24.11 Progress Steady | `FitCircleContourXld`, `ProjectionPl`, `IntersectionLl`, `AngleLl`, `GenContourPolygonXld` | [VERIFIED: VisionAlgorithmService.cs] 프로젝트 필수 |
| `PropertyTools.Wpf` | v3.1.0 | PropertyGrid `[Category]`, `[ItemsSourceProperty]` | [VERIFIED: packages.config] |
| `Newtonsoft.Json` | v13.0.3 | `[JsonIgnore]` transient 필드 제외 | [VERIFIED: EdgeToLineDistanceMeasurement.cs L71] |

**Installation:** 신규 패키지 없음.

---

## Architecture Patterns

### System Architecture Diagram

```
[Action_FAIMeasurement.EStep.Measure]
        ↓ foreach (FAI) → foreach (Measurement)
[IDatumOriginConsumer 분기 (D-03, Phase 31 신규)]
        ↓ inject: DatumOriginRow/Col, DatumAngleRad
[MeasurementBase.TryExecute]
        │
        ├─ CircleCenterDistanceMeasurement (E8)
        │       ↓ TryFindCircle → center(Pd)
        │       ↓ VisionAlgorithmService.ComputeProjectionDistance (D-04 공용 함수)
        │
        ├─ EdgeToLineAngleMeasurement (D1/H5)
        │       ↓ TryFitLine (strip-loop) → line (pr1..pr2)
        │       ↓ Datum A 기준선 (DatumOriginRow/Col + DatumAngleRad)
        │       ↓ AngleLineLine
        │
        ├─ ArcLineIntersectDistanceMeasurement (I9/I10)
        │       ├─ 3점 arc ROI (P1~P3) → FitCircleContourXld → center(cR,cC) + radius(r)
        │       ├─ TryFitLine → line (lr1..lr2)
        │       ├─ TryIntersectCircleLine(cR,cC,r, lr1..lr2) → [sol1, sol2]
        │       ├─ 교점 선택: ROI 내부의 해 (D-10)
        │       └─ ComputeProjectionDistance
        │
        ├─ CompoundAngleMeasurement (E2)
        │       ├─ 3× TryFindCircle (CL1~CL3) → center(Pd, c2, c3)
        │       ├─ 2× TryFitLine → La, Lb
        │       ├─ midline Lc = MidLine(La, Lb)  [내부 계산]
        │       ├─ IntersectLines(Lc, CL2) → Pa ; IntersectLines(Lc, CL3) → Pb
        │       ├─ Pc = midpoint(Pa, Pb)
        │       ├─ line Ld = (Pd, Pc) (center of CL1 → Pc)
        │       └─ AngleLineLine(Ld, DatumB 기준선)
        │
        ├─ CompoundCenterCDistanceMeasurement (E9)
        │       ├─ 같은 기하 체인 (CL2~CL3 + La+Lb → Pc) — CL1 생략
        │       └─ ComputeProjectionDistance(Pc, DatumC)
        │
        ├─ CompoundCenterBDistanceMeasurement (E10)
        │       └─ 위와 동일, DatumB 기준
        │
        └─ ArcEdgeDistanceMeasurement (G1·G2·G5~G8·G11·G12)
                ↓ TryFitLine (EdgeToLineDistance 와 동일 알고리즘 — MSOP 확인)
                ↓ ComputeProjectionDistance(midpoint, DatumC)
```

### Recommended Project Structure

```
WPF_Example/Custom/Sequence/Inspection/Measurements/
├── EdgeToLineDistanceMeasurement.cs      # 기존 (Phase 23.1)
├── CircleCenterDistanceMeasurement.cs    # Phase 31 신규 — E8
├── EdgeToLineAngleMeasurement.cs         # Phase 31 신규 — D1/H5
├── ArcLineIntersectDistanceMeasurement.cs # Phase 31 신규 — I9/I10
├── CompoundAngleMeasurement.cs           # Phase 31 신규 — E2
├── CompoundCenterCDistanceMeasurement.cs # Phase 31 신규 — E9
├── CompoundCenterBDistanceMeasurement.cs # Phase 31 신규 — E10
└── ArcEdgeDistanceMeasurement.cs         # Phase 31 신규 — G 시리즈

WPF_Example/Custom/Sequence/Inspection/
├── IDatumOriginConsumer.cs               # Phase 31 신규 — D-03 인터페이스

WPF_Example/Halcon/Algorithms/
└── VisionAlgorithmService.cs             # D-04 ComputeProjectionDistance 추가
                                          # TryFitArc (3점 호 피팅) 추가
                                          # TryIntersectCircleLine 추가
```

---

## Pattern 1: IDatumOriginConsumer 인터페이스 (D-03)

**What:** Datum 절대좌표(교점 row/col, 기준각도 rad)를 측정 객체에 주입하는 인터페이스.
**When to use:** 신규 타입 6종 전부 구현. `EdgeToLineDistanceMeasurement` 도 이 인터페이스를 소급 구현하여 하드코딩 제거.

**인터페이스 정의 (권장):**
```csharp
//260519 hbk Phase 31 D-03
public interface IDatumOriginConsumer
{
    double DatumOriginRow { get; set; }
    double DatumOriginCol { get; set; }
    double DatumAngleRad  { get; set; }
}
```

**Action_FAIMeasurement 수정 (L161~185 대체):**
```csharp
//260519 hbk Phase 31 D-03 — IDatumOriginConsumer 일반화 (기존 EdgeToLineDistanceMeasurement 하드코딩 제거)
var consumer = meas as IDatumOriginConsumer;
if (consumer != null)
{
    DatumConfig dc = null;
    if (parentSeq2 != null && parentSeq2.DatumConfigs != null
        && !string.IsNullOrEmpty(meas.DatumRef))
    {
        foreach (var d in parentSeq2.DatumConfigs)
        {
            if (d != null && d.DatumName == meas.DatumRef) { dc = d; break; }
        }
    }
    if (dc != null)
    {
        consumer.DatumOriginRow = dc.DetectedOriginRow;
        consumer.DatumOriginCol = dc.DetectedOriginCol;
        consumer.DatumAngleRad  = dc.DetectedRefAngle;
    }
    else
    {
        consumer.DatumOriginRow = 0.0;
        consumer.DatumOriginCol = 0.0;
        consumer.DatumAngleRad  = 0.0;
    }
}
```
`EdgeToLineDistanceMeasurement` 가 `IDatumOriginConsumer` 를 구현하면 기존 `meas as EdgeToLineDistanceMeasurement` 블록은 전부 삭제 가능.

---

## Pattern 2: D-04 공용 거리 계산 함수 추출

**What:** `EdgeToLineDistanceMeasurement.TryExecute` L126~196 의 `projection_pl` 정사영 + MeasureAxis + 부호 처리 블록을 `VisionAlgorithmService` static 메서드로 추출.
**When to use:** E8, I9/I10, ArcEdgeDistance, E9, E10 이 측정점(row,col)을 구한 뒤 이 함수 1개를 호출.

**시그니처 (권장):**
```csharp
// Source: EdgeToLineDistanceMeasurement.TryExecute L126~196 추출
// 위치: VisionAlgorithmService.cs (static 메서드)
//260519 hbk Phase 31 D-04 — projection_pl 거리 공용 헬퍼
public static double ComputeProjectionDistance(
    double pointRow, double pointCol,           // 측정점 (에지 중점 / 원 중심 / 교점 등)
    double datumOriginRow, double datumOriginCol, // datum 교점 (IDatumOriginConsumer 주입값)
    double datumAngleRad,                        // datum 기준선 각도 (DetectedRefAngle)
    double pixelResolution,                      // mm/pixel
    string measureAxis)                          // "X" or "Y"
{
    // EdgeToLineDistanceMeasurement.TryExecute L126~196 블록 그대로 이식
    // (cosT 정규화 + axisR1/C1/R2/C2 계산 + ProjectionPl + signedPx → resultValue)
    // try { ProjectionPl... } catch { return 0.0; }
}
```

---

## Pattern 3: E8 — 원중심 → Datum 거리 (`CircleCenterDistanceMeasurement`)

**What:** 기존 `CircleDiameterMeasurement.TryFindCircle` 로 원 중심을 구하고, `ComputeProjectionDistance` 로 Datum B → 원중심 Y 거리 계산.

**MSOP 확정 절차 [VERIFIED: pdf_text.txt p.91 L4596~4600]:**
1. P1 이동 → circle tool 원 CL1 취득
2. CL1 중심점 Pd 구성
3. Datum B 기준
4. Datum B → Pd Y 방향 거리

**구현 패턴:**
```csharp
// CircleCenterDistanceMeasurement.TryExecute 골격
var svc = new VisionAlgorithmService();
double foundRow, foundCol, foundRadius;
if (!svc.TryFindCircle(image, Circle_Row, Circle_Col, Circle_Radius,
    datumTransform, Sigma, EdgeThreshold, EdgePolarity,
    out foundRow, out foundCol, out foundRadius, out error))
    return false;
resultValue = VisionAlgorithmService.ComputeProjectionDistance(
    foundRow, foundCol, DatumOriginRow, DatumOriginCol, DatumAngleRad,
    pixelResolution, MeasureAxis);  // default "Y"
return true;
```

**ROI 필드:** `Circle_Row/Col/Radius` (CircleDiameterMeasurement 와 동일 Category `"Circle|ROI"`)
**인터페이스:** `IDatumOriginConsumer` 구현

---

## Pattern 4: D1/H5 — EdgeToLineAngle (`EdgeToLineAngleMeasurement`)

**What:** ROI 1개에서 직선 피팅 → Datum A 기준선(DatumAngleRad)과의 각도(degree).

**MSOP 확정 절차 [VERIFIED: pdf_text.txt p.82 D1, p.115 H5]:**
- D1: 2점으로 라인(LU/MU/MD/LD/RD/RU) → Datum A 기준 각도. 입력 2점 = ROI 2개 (라인 양쪽 끝 에지 각각 1 ROI).
- H5: 2점으로 라인 MN → Datum A 기준 각도. 동일.

**중요 — D1/H5 ROI 구성:** MSOP 는 "P1, P2 두 점으로 직선" 이므로 `TryFitLine` 의 단일 ROI 1개보다 **2-ROI (Line1/Line2) 구성이 더 정확**하다. 단, D-05 는 "ROI 1개 + Datum" 패턴을 채택했으므로 단일 넓은 ROI 로 두 점을 커버하는 방식도 가능. Planner 결정 필요.

**권장 구현 (ROI 1개 방식 — D-05 준수):**
```csharp
// EdgeToLineAngleMeasurement.TryExecute
var svc = new VisionAlgorithmService();
double pr1, pc1, pr2, pc2;
if (!svc.TryFitLine(image,
    Point_Row, Point_Col, Point_Phi, Point_Length1, Point_Length2,
    datumTransform, EdgeSampleCount, EdgeTrimCount, Sigma, EdgeThreshold,
    EdgeDirection, EdgePolarity,
    out pr1, out pc1, out pr2, out pc2, out error, "All"))
    return false;
// Datum A 기준선: (DatumOriginRow/Col, angle = DatumAngleRad)
// 피팅된 라인의 각도: Atan2(pr2-pr1, pc2-pc1) 또는 AngleLineLine
resultValue = VisionAlgorithmService.AngleLineLine(
    pr1, pc1, pr2, pc2,
    DatumOriginRow - 200 * Math.Sin(DatumAngleRad),
    DatumOriginCol - 200 * Math.Cos(DatumAngleRad),
    DatumOriginRow + 200 * Math.Sin(DatumAngleRad),
    DatumOriginCol + 200 * Math.Cos(DatumAngleRad));
return true;
```
`AngleLineLine` 은 `VisionAlgorithmService.cs L567` 에 이미 있음. [VERIFIED: VisionAlgorithmService.cs L568]

---

## Pattern 5: I9/I10 — 호∩라인 교점 → Datum 거리

**What:** 3점으로 arc 피팅 → 직선 피팅 → arc-line 교점 → Datum C X 거리.

**MSOP 확정 절차 [VERIFIED: pdf_text.txt p.121 I9, p.122 I10]:**
- I9: P1~P3 → arc A1, P6~P8 → arc A2 / P5, P10 → lines Lb, Ld / 교점 Pb=A1∩Lb, Pd=A2∩Ld
- I10: 동일 arc / P4, P9 → lines La, Lc / 교점 Pa=A1∩La, Pc=A2∩Lc
- 둘 다 Datum C → X 방향 거리 (2개 교점 각각 독립 측정)

**HALCON 호 피팅 연산자 선택 [ASSUMED — Context7 미조회, 기존 코드 기반 추론]:**
- `TryFindCircle` 과 동일하게 `FitCircleContourXld` 사용.
- 3점 피팅: `GenContourPolygonXld(3점 row/col) → FitCircleContourXld` → center(cR,cC) + radius.
- 단, 3점 `FitCircleContourXld` 는 수렴 보장이 낮을 수 있음. 실측 검증 필요 [LOW confidence].

**원-직선 교점 연산 신규 구현 필요:**
```csharp
// VisionAlgorithmService 에 추가 — D-10: ROI 내부 해 선택
//260519 hbk Phase 31 D-10
public static bool TryIntersectCircleLine(
    double cRow, double cCol, double radius,
    double lRow1, double lCol1, double lRow2, double lCol2,
    double roiRow, double roiCol,   // D-10: ROI 내부 해 선택 기준점
    out double intRow, out double intCol)
{
    // 직선 파라미터화 → 원 방정식 대입 → 2차 방정식 해 2개
    // → ROI 중심(roiRow,roiCol)에 더 가까운 해 선택
    intRow = intCol = 0;
    // [수학 구현 필요 — HALCON IntersectionLl 은 직선-직선만, 원-직선은 없음]
    // 수동 구현: Δ = (lRow2-lRow1)^2 + (lCol2-lCol1)^2 기반 2차 방정식
}
```

**주의:** HALCON `IntersectionLl` 은 직선-직선만 지원. 원-직선 교점은 HALCON 에 직접 API 없음 → 수학 구현 또는 `GetCirclePoints` 류 미사용 [VERIFIED: VisionAlgorithmService.cs IntersectLines L598].

---

## Pattern 6: CompoundAngle 계열 (E2/E9/E10)

**What:** 다단계 기하 구성 체인 — 원 3개 + 라인 2개 → midline → 교점 2개 → 중점 → 최종 측정.

**MSOP 확정 절차 [VERIFIED: pdf_text.txt p.85 E2, p.92 E9, p.93 E10]:**

E2 (각도, Datum B):
1. P1~P3 → CL1~CL3 원 피팅
2. P4,P5 → La, Lb 라인 피팅
3. midline Lc = La+Lb 의 중간선
4. Pa = Lc ∩ CL2 (원-라인 교점), Pb = Lc ∩ CL3
5. Pc = midpoint(Pa, Pb)
6. Pd = center(CL1)
7. line Ld = (Pd → Pc)
8. Datum B 기준 → angle(Ld vs Datum B)

E9 (거리, Datum C): 1~5 번 동일 (CL1 불필요), Pc → Datum C X 거리
E10 (거리, Datum B): 1~5 번 동일, Pc → Datum B Y 거리

**D-11 타입 분리 근거:** E2 는 각도(단위 deg), E9/E10 은 거리(단위 mm) — 단일 타입+OutputMode 스위치 금지.

**공유 기하 체인 범위 권장 (Discretion):**
`E9Measurement` 와 `E10Measurement` 는 기하 체인(CL2/CL3 + La/Lb → Pc) 이 동일하므로 내부 private 헬퍼 메서드 `TryComputeChainPc(...)` 를 공유하고, 최종 `ComputeProjectionDistance` 호출 시 DatumRef 만 달리하는 구조가 자연스럽다. 단, **D-09** 에 따라 중간 산출물은 INI/UI 에 노출 안 함.

**midline 계산 (HALCON 없음 → 수학 구현):**
```csharp
// La: (la1R,la1C) → (la2R,la2C), Lb: (lb1R,lb1C) → (lb2R,lb2C)
// midline = line through midpoint(midpt_La, midpt_Lb) with average direction
// 단순 구현: Lc 중점 = ((la_mid + lb_mid)/2), 방향 = La/Lb 방향 평균
```

---

## Pattern 7: ArcEdgeDistance (G 시리즈) — MSOP 재확인 결과

**What:** MSOP 원본 G1~G12 절차 = `"1. Capture points: P1 / 2. Based on Datum C / 3. Measure from Datum C to P1 in X direction"` — EdgeToLineDistance 와 절차적으로 동일. [VERIFIED: pdf_text.txt p.104~111]

**결론:** `ArcEdgeDistanceMeasurement` 는 `EdgeToLineDistanceMeasurement` 의 알고리즘을 그대로 사용하되:
- 기본 `MeasureAxis = "X"` (Datum C → X 방향)
- ROI 필드명 동일 (`Point_*`)
- `IDatumOriginConsumer` 구현
- `MeasurementFactory` 에 별도 케이스 등록 (D-08)

SOP 슬라이드 43 의 "ArcEdgeDistance" 알고리즘 분류는 측정 대상 에지가 호의 일부(arc edge)임을 의미하는 것으로 해석됨 — ROI 를 호의 에지에 그리면 일반 라인 피팅이 처리 가능.

---

## Pattern 8: CO-23.1-02 ROI 버튼 일반화

**현재 상태 [VERIFIED: MainView.xaml.cs L1194, L1377]:**
- `RectRoiButton_Click` 은 `FindSelectedEdgeToLineMeasurement()` → `EdgeToLineDistanceMeasurement` 만 분기 처리.
- 신규 타입(E8 제외)도 Point ROI 를 사용하므로 ROI 버튼 활성화 필요.

**일반화 방향 (Discretion):**

Option A — 타입 화이트리스트: `FindSelectedEdgeToLineMeasurement` → `FindSelectedRectMeasurement` 로 확장, `MeasurementBase` 파생 타입 중 Point_* 필드 보유 타입을 `as` 로 순차 검사.

Option B — 마커 인터페이스: Point ROI 를 갖는 모든 측정 타입이 `IPointRoiMeasurement` 인터페이스를 구현 → `FindSelected` 가 `as IPointRoiMeasurement` 단일 검사.

**권장 (최소 변경):** Option A. 신규 타입 배열 `RectMeasurementTypes = { typeof(EdgeToLineDistanceMeasurement), typeof(EdgeToLineAngleMeasurement), typeof(ArcEdgeDistanceMeasurement), typeof(CircleCenterDistanceMeasurement), ...}` 로 관리.

Circle ROI(`Circle_*` 필드) 를 갖는 `CircleCenterDistanceMeasurement` 는 기존 `FindSelectedCircleMeasurement` 경로도 일반화 필요.

---

## Pattern 9: CO-23.1-01 듀얼 이미지 뷰어

**현재 상태:** `DatumConfig.TeachingImagePath` 와 `ShotConfig.SimulImagePath` 가 분리(Phase 22)되어 있으나 UI 에서 두 경로를 구분 표시하지 않음.

**구현 방향 (Discretion):** MainView 에 별도 `Image` 또는 `HalconViewer` 패널을 추가하여 Datum 티칭 모드에서 `TeachingImagePath` 를 표시. 또는 기존 뷰어 아래에 썸네일 레이블로 경로만 표시.

---

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| INI 직렬화 (신규 타입 6종 ROI 필드) | 수동 IniFile | `ParamBase` reflection 자동 | 기존 `InspectionRecipeManager` 자동 처리 |
| 공차 판정 | 신규 IsPass | `MeasurementBase.EvaluateJudgement` | 비대칭 공차 포함 [VERIFIED: MeasurementBase.cs L67] |
| 타입 dispatch | switch if 사슬 | `MeasurementFactory.Create(typeName, owner)` | D-11 각각 등록 |
| PropertyGrid 노출 | ViewModel | `[Category]` + `[ItemsSourceProperty]` | 기존 패턴 |
| 원-직선 각도 | 직접 계산 | `VisionAlgorithmService.AngleLineLine` (HOperatorSet.AngleLl 래퍼) [VERIFIED: L568] | 이미 존재 |
| Datum 교점(직선-직선) | 직접 계산 | `VisionAlgorithmService.IntersectLines` (HOperatorSet.IntersectionLl 래퍼) [VERIFIED: L597] | 이미 존재 |
| 원 피팅 (N점) | 직접 계산 | `VisionAlgorithmService.TryFindCircle` (FitCircleContourXld) [VERIFIED: L237] | 이미 존재 |
| 라인 피팅 (strip-loop) | 직접 구현 | `VisionAlgorithmService.TryFitLine` [VERIFIED: L18] | strip-loop + measurePhi + EdgeSelection 이미 구현됨 |
| 원-직선 교점 | HALCON API 탐색 | 수학 구현 (2차 방정식) | HALCON 에 원-직선 교점 전용 연산자 없음 [VERIFIED: IntersectLines 는 직선-직선만] |

---

## Common Pitfalls

### Pitfall 1: Action_FAIMeasurement 에서 EdgeToLineDistanceMeasurement 하드코딩 잔존

**What goes wrong:** IDatumOriginConsumer 인터페이스를 새로 만들었으나 `Action_FAIMeasurement` L161~185 의 `meas as EdgeToLineDistanceMeasurement` 블록을 삭제하지 않아 신규 타입에 Datum 좌표가 주입되지 않음.
**Why it happens:** 인터페이스 추가만 하고 기존 블록을 제거하지 않는 실수.
**How to avoid:** D-03 작업 시 기존 블록 삭제 + `meas as IDatumOriginConsumer` 단일 분기로 교체. 삭제 라인에 `//260519 hbk Phase 31 D-03 removed` 마커.
**Warning signs:** 신규 타입 측정 시 항상 `DatumOriginRow = 0.0` (미주입 상태) → ProjectionPl 결과 이상.

### Pitfall 2: EdgeToLineDistanceMeasurement 가 IDatumOriginConsumer 를 소급 구현 안 함

**What goes wrong:** 기존 `EdgeToLineDistanceMeasurement` 가 인터페이스를 구현하지 않으면 기존 코드의 `meas as EdgeToLineDistanceMeasurement` 분기를 삭제할 수 없어 D-03 의도 미달성.
**How to avoid:** `EdgeToLineDistanceMeasurement : MeasurementBase, IDatumOriginConsumer` 로 소급 수정. 3 필드(`DatumOriginRow/Col/AngleRad`)가 이미 있으므로 `interface` 명시만 추가.

### Pitfall 3: 원-직선 교점에서 해 2개 중 잘못된 해 선택 (D-10)

**What goes wrong:** `TryIntersectCircleLine` 이 ROI 중심과의 거리 기준이 아닌 임의 기준(예: 항상 첫 번째 해)으로 선택 → 측정값 반전.
**How to avoid:** D-10 규칙 — ROI `Arc_Row/Arc_Col` 중심점과 유클리드 거리가 더 가까운 해를 선택. 라인 ROI 의 중심을 선택 기준으로 사용하면 사용자가 ROI 드로잉 위치로 교점을 선택하는 UX 가 자연스러움.

### Pitfall 4: CompoundAngle 에서 midline 계산 오류

**What goes wrong:** La/Lb 의 midline Lc 를 `TryFitLine` 결과 2점의 단순 평균으로 구할 때 방향벡터가 틀림.
**How to avoid:** midline = La 방향벡터 + Lb 방향벡터 평균으로 방향 결정 + midpoint(La 중점, Lb 중점) 를 통과하는 라인으로 정의. 단위벡터 정규화 필수.

### Pitfall 5: MeasurePos strip-loop 누락 (memory feedback 필수)

**What goes wrong:** 신규 타입의 `TryFitLine` 호출을 직접 `MeasurePos` 단일 호출로 작성 → "insufficient edge points" 실패.
**How to avoid:** 라인 에지 추출은 반드시 `VisionAlgorithmService.TryFitLine` 을 호출. 직접 MeasurePos 금지. `TryFitLine` 내부가 이미 strip-loop + measurePhi + EdgeSelection 필수 3종을 구현함. [VERIFIED: VisionAlgorithmService.cs L102~146]

### Pitfall 6: MeasurementFactory 에 신규 타입 등록 누락

**What goes wrong:** 신규 클래스 파일은 생성했으나 `MeasurementFactory.Create` switch + `GetTypeNames()` 에 case/항목 추가를 빠뜨림 → INI `Type=` 로드 시 null, UI ComboBox 에 표시 안 됨.
**How to avoid:** 각 신규 타입 구현 task 에 `MeasurementFactory.cs` 수정을 반드시 포함.

### Pitfall 7: csproj Compile ItemGroup 누락

**What goes wrong:** 신규 .cs 파일이 MSBuild classic-style csproj 에 자동 포함되지 않아 CS0246.
**How to avoid:** 각 신규 파일에 대해 `DatumMeasurement.csproj` Compile ItemGroup 에 1줄 추가. [VERIFIED: Phase 23 Pitfall 5 패턴]

### Pitfall 8: E9/E10 의 DatumRef 혼선

**What goes wrong:** E9 = Datum C (X), E10 = Datum B (Y) 인데 INI 에서 DatumRef 를 반대로 입력하거나 MeasureAxis 기본값이 잘못됨.
**How to avoid:** `CompoundCenterCDistanceMeasurement` = MeasureAxis 기본 "X", `CompoundCenterBDistanceMeasurement` = MeasureAxis 기본 "Y". 타입명 자체가 C/B 를 구분하도록 명명.

---

## Code Examples

### E2/E9/E10 공통 기하 체인 내부 헬퍼 (권장 패턴)

```csharp
// CompoundAngleMeasurement / CompoundCenterCDistanceMeasurement / CompoundCenterBDistanceMeasurement 공통
// 내부 private 메서드 (각 타입 클래스에 복사하거나 static 헬퍼로 추출)
//260519 hbk Phase 31 D-09 — compound 기하 체인 내부 계산
private bool TryComputeChainPoint(HImage image, HTuple datumTransform,
    out double pcRow, out double pcCol, out string error)
{
    pcRow = pcCol = 0; error = null;
    var svc = new VisionAlgorithmService();

    // CL2 피팅
    double cl2R, cl2C, cl2Rad;
    if (!svc.TryFindCircle(image, Cl2_Row, Cl2_Col, Cl2_Radius, datumTransform,
        Sigma, EdgeThreshold, EdgePolarity, out cl2R, out cl2C, out cl2Rad, out error))
        return false;

    // CL3 피팅
    double cl3R, cl3C, cl3Rad;
    if (!svc.TryFindCircle(image, Cl3_Row, Cl3_Col, Cl3_Radius, datumTransform,
        Sigma, EdgeThreshold, EdgePolarity, out cl3R, out cl3C, out cl3Rad, out error))
        return false;

    // La, Lb 라인 피팅
    double la1R,la1C,la2R,la2C, lb1R,lb1C,lb2R,lb2C;
    if (!svc.TryFitLine(image, La_Row,La_Col,La_Phi,La_Length1,La_Length2,
        datumTransform, EdgeSampleCount, EdgeTrimCount, Sigma, EdgeThreshold,
        EdgeDirection, EdgePolarity, out la1R,out la1C,out la2R,out la2C,out error,"All"))
        return false;
    if (!svc.TryFitLine(image, Lb_Row,Lb_Col,Lb_Phi,Lb_Length1,Lb_Length2,
        datumTransform, EdgeSampleCount, EdgeTrimCount, Sigma, EdgeThreshold,
        EdgeDirection, EdgePolarity, out lb1R,out lb1C,out lb2R,out lb2C,out error,"All"))
        return false;

    // midline Lc
    double lcMidR = (la1R+la2R+lb1R+lb2R)/4.0;
    double lcMidC = (la1C+la2C+lb1C+lb2C)/4.0;
    // 방향 = La + Lb 방향 평균 (단위벡터 합)
    double laLenInv = 1.0 / Math.Max(1e-9, Math.Sqrt((la2R-la1R)*(la2R-la1R)+(la2C-la1C)*(la2C-la1C)));
    double lbLenInv = 1.0 / Math.Max(1e-9, Math.Sqrt((lb2R-lb1R)*(lb2R-lb1R)+(lb2C-lb1C)*(lb2C-lb1C)));
    double dirR = (la2R-la1R)*laLenInv + (lb2R-lb1R)*lbLenInv;
    double dirC = (la2C-la1C)*laLenInv + (lb2C-lb1C)*lbLenInv;
    double lcR1 = lcMidR - 200*dirR; double lcC1 = lcMidC - 200*dirC;
    double lcR2 = lcMidR + 200*dirR; double lcC2 = lcMidC + 200*dirC;

    // Pa = Lc ∩ CL2, Pb = Lc ∩ CL3
    double paR, paC, pbR, pbC;
    if (!VisionAlgorithmService.TryIntersectCircleLine(
        cl2R, cl2C, cl2Rad, lcR1, lcC1, lcR2, lcC2, Cl2_Row, Cl2_Col, out paR, out paC))
    { error = "Lc∩CL2 intersection failed"; return false; }
    if (!VisionAlgorithmService.TryIntersectCircleLine(
        cl3R, cl3C, cl3Rad, lcR1, lcC1, lcR2, lcC2, Cl3_Row, Cl3_Col, out pbR, out pbC))
    { error = "Lc∩CL3 intersection failed"; return false; }

    // Pc = midpoint(Pa, Pb)
    pcRow = (paR + pbR) / 2.0; //260519 hbk Phase 31 D-09
    pcCol = (paC + pbC) / 2.0; //260519 hbk Phase 31 D-09
    return true;
}
```

### IDatumOriginConsumer 인터페이스 전체

```csharp
// 파일: WPF_Example/Custom/Sequence/Inspection/IDatumOriginConsumer.cs
//260519 hbk Phase 31 D-03 — Datum 절대좌표 주입 인터페이스
namespace ReringProject.Sequence
{
    /// <summary>
    /// Datum 절대좌표를 측정 객체에 주입하는 표준 인터페이스.
    /// Action_FAIMeasurement.EStep.Measure 에서 DatumConfig 를 찾아 주입.
    /// ParamBase INI 직렬화는 이 인터페이스 무관 (public 프로퍼티 reflection 경로).
    /// </summary>
    public interface IDatumOriginConsumer //260519 hbk Phase 31 D-03
    {
        double DatumOriginRow { get; set; }
        double DatumOriginCol { get; set; }
        double DatumAngleRad  { get; set; }
    }
}
```

### TryIntersectCircleLine 구현 스켈레톤

```csharp
// VisionAlgorithmService.cs 추가
//260519 hbk Phase 31 D-10 — 원-직선 교점 (2해 → ROI 내부 해 선택)
public static bool TryIntersectCircleLine(
    double cRow, double cCol, double radius,
    double lRow1, double lCol1, double lRow2, double lCol2,
    double roiRow, double roiCol,
    out double intRow, out double intCol)
{
    intRow = intCol = 0;
    try
    {
        // 직선 방향벡터: (dR, dC) = (lRow2-lRow1, lCol2-lCol1)
        double dR = lRow2 - lRow1; double dC = lCol2 - lCol1;
        // 원 중심 → 직선 시작점 벡터: (fR, fC)
        double fR = lRow1 - cRow; double fC = lCol1 - cCol;
        // 2차 방정식: t² (dR²+dC²) + 2t(fR·dR+fC·dC) + (fR²+fC²-r²) = 0
        double a = dR*dR + dC*dC;
        if (a < 1e-12) return false; // 직선 길이 0
        double b = 2*(fR*dR + fC*dC);
        double c = fR*fR + fC*fC - radius*radius;
        double disc = b*b - 4*a*c;
        if (disc < 0) return false; // 교점 없음
        double sqrtDisc = Math.Sqrt(disc);
        double t1 = (-b - sqrtDisc) / (2*a);
        double t2 = (-b + sqrtDisc) / (2*a);
        double s1R = lRow1 + t1*dR; double s1C = lCol1 + t1*dC;
        double s2R = lRow1 + t2*dR; double s2C = lCol1 + t2*dC;
        // D-10: ROI 중심에 더 가까운 해 선택
        double d1 = (s1R-roiRow)*(s1R-roiRow) + (s1C-roiCol)*(s1C-roiCol);
        double d2 = (s2R-roiRow)*(s2R-roiRow) + (s2C-roiCol)*(s2C-roiCol);
        if (d1 <= d2) { intRow = s1R; intCol = s1C; }
        else          { intRow = s2R; intCol = s2C; }
        return true;
    }
    catch { return false; }
}
```

---

## D-08 ArcEdgeDistance 호 위 측정점 정의 — 재확인 결과

**MSOP 원본 G1~G12 절차 [VERIFIED: pdf_text.txt p.104~111]:**
```
1. Capture points: P1
2. Based on the Datum C
3. Calculate: Measure the dimension from Datum C to P1 in the X direction
```

P1 캡처 방식에 대한 추가 지침(arc 피팅, 특정 각도의 접점 등)이 없다. MSOP 는 OMM 장비에서 포인트 캡처 툴로 에지 위의 점을 직접 클릭하는 방식이다. 비전 시스템에서 이를 자동화할 때: **P1 위치에 작은 Rectangle ROI 를 그려 에지 라인 피팅 → 라인 중점(pRow, pCol) 이 P1**. 즉 `EdgeToLineDistanceMeasurement` 와 알고리즘이 동일하다.

SOP 슬라이드 43 의 "ArcEdgeDistance" 분류는 **아직 Datum 정보 데크의 Pptx 슬라이드 이미지(S60~S67) 를 파싱하지 못한 상태**이므로 슬라이드 본문 절차를 직접 확인할 수 없다 [LOW confidence for arc-specific interpretation]. 그러나 MSOP 원본이 충돌 시 우선이라는 원칙(31-SOP-REFERENCE.md)에 따라 MSOP 절차("단순 포인트 캡처")를 준용한다.

**결론:** ArcEdgeDistance = EdgeToLineDistance 알고리즘 재사용. `MeasureAxis = "X"` 기본값만 다름.

---

## D-07 E9/E10 거리 절차 — 재확인 결과

**MSOP 원본 [VERIFIED: pdf_text.txt p.92 E9, p.93 E10]:**

E9 (p.92): 
```
8. Based on the Datum C
9. Calculate: Measure the distance from Datum C to Pc
Evaluation method: Measure the Angle from Datum C to Pc  ← 오기 (본문과 불일치)
```

E10 (p.93):
```
8. Based on the Datum C  ← 헤더에 "Datum C" 표기 (하지만 절차에는)
9. Calculate: Measure the distance from Datum B to Pc
Datum: B  ← FAI Datum 필드
```

**확정:** D-07 결정이 맞다.
- E9 = Datum C 기준 Pc 까지 거리(mm), MeasureAxis "X"
- E10 = Datum B 기준 Pc 까지 거리(mm), MeasureAxis "Y"
- 평가방법란 "Angle" 표기는 pdftotext 오기 또는 MSOP 편집 오류이며 절차 본문("distance")이 우선.

---

## Runtime State Inventory

Phase 31 은 신규 타입 추가 phase — rename/refactor 아님.

| Category | Items Found | Action Required |
|----------|-------------|-----------------|
| Stored data | None — verified (INI 레시피는 신규 타입 섹션 없음, 기존 키 변경 없음) | 없음 |
| Live service config | None — verified | 없음 |
| OS-registered state | None — verified | 없음 |
| Secrets/env vars | None — verified | 없음 |
| Build artifacts | 신규 .cs 파일 × 9개 → `DatumMeasurement.csproj` Compile ItemGroup 등록 필요 | 각 파일 추가 시 csproj 1줄 추가 |

---

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| Halcon 24.11 Progress Steady | 모든 Halcon 연산 | ✓ | 24.11 | — |
| .NET Framework 4.8 + C# 7.2 | csproj | ✓ | 4.8 | — |
| MSBuild 15 | Debug/x64 빌드 | ✓ | 15.0 | — |
| SIMUL 이미지 (Bottom/Side/Top) | UAT 검증 | 사용자 제공 | — | 사용자가 각 카메라 Fixture 이미지 제공 |

---

## Open Questions

1. **I9/I10 의 3점 arc ROI 구성**
   - What we know: MSOP 는 "P1~P3 캡처 → arc A1" 이므로 ROI 3개 (각각 1 에지 포인트) 또는 ROI 1개(arc 전체 포함) 중 선택 필요.
   - What's unclear: 사용자가 P1/P2/P3 각각에 Rectangle ROI 를 3개 티칭할 것인가, 아니면 호 전체를 포함하는 1개 큰 ROI 를 티칭할 것인가.
   - Recommendation: **ROI 3개(Arc_P1_*, Arc_P2_*, Arc_P3_*) 방식** — MSOP 와 1:1 대응, 각 포인트에서 `TryFitLine` 으로 에지점 1개 추출 후 3점 `FitCircleContourXld`. 단, 에지점 정확도가 중요하므로 ROI 를 작게 그려야 함. Planner 가 ROI 필드 명명 결정.

2. **D1/H5 ROI 1개 vs 2개**
   - What we know: MSOP 는 P1/P2 두 점으로 직선. D-05 는 "ROI 1개" 를 채택.
   - What's unclear: 작은 ROI 1개로 넓은 면적을 커버할 수 있는지, 아니면 ROI 2개(Line1/Line2) 방식이 더 안정적인지.
   - Recommendation: ROI 1개(strip-loop 방식) 가 D-05 에 부합. 단, ROI 가 충분히 넓어야 에지 분포가 확보됨. Planner 결정.

3. **CompoundAngle E9/E10 와 E2 의 동일 ROI 재사용**
   - What we know: E2/E9/E10 이 동일 Fixture 에서 동일 CL2/CL3/La/Lb 를 사용한다.
   - What's unclear: 동일 Shot 안에서 E2/E9/E10 을 모두 측정할 때 ROI 를 중복 티칭해야 하는지.
   - Recommendation: D-09(내부 계산 캡슐화)에 따라 각 타입이 독립 ROI 를 보유하는 것이 구조적으로 단순. 동일 Shot 에 3개 FAI(E2, E9, E10)를 각각 배치하고 ROI 는 별도 티칭. ROI 공유는 D-09 범위 밖.

---

## Validation Architecture

### Test Framework

| Property | Value |
|----------|-------|
| Framework | 없음 — 자동화 테스트 프레임워크 미도입 (CLAUDE.md: "No test framework detected") |
| Config file | 없음 |
| Quick run command | `MSBuild WPF_Example\DatumMeasurement.csproj /p:Configuration=Debug /p:Platform=x64 /v:minimal /t:Rebuild` |
| Full suite command | SIMUL_MODE 수동 UAT |

### Phase Requirements → Test Map

| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| E8 | 원중심 → Datum B Y 거리(mm) 정상 측정 | UAT (SIMUL) | N/A | ❌ Wave 0 — 31-UAT.md |
| D1/H5 | Datum A 기준 직선 각도(deg) 정상 측정 | UAT (SIMUL) | N/A | ❌ Wave 0 |
| I9/I10 | 호∩라인 교점 → Datum C X 거리(mm) | UAT (SIMUL) | N/A | ❌ Wave 0 |
| E2 | CompoundAngle Datum B 기준 41.36° ±1° | UAT (SIMUL) | N/A | ❌ Wave 0 |
| E9/E10 | CompoundCenter 거리(mm) C/B 각각 | UAT (SIMUL) | N/A | ❌ Wave 0 |
| ArcEdge | G 시리즈 Datum C X 거리(mm) 정상 측정 | UAT (SIMUL) | N/A | ❌ Wave 0 |
| CO-23.1-02 | 신규 측정 타입 Rect ROI 버튼 활성화 | UAT (manual UI) | N/A | ❌ Wave 0 |
| CO-23.1-01 | 듀얼 이미지 표시 | UAT (manual visual) | N/A | ❌ Wave 0 |
| BUILD | Debug/x64 PASS, Phase 21 baseline warning 유지 | Build | `MSBuild ... /t:Rebuild` | ✅ |
| D-03 | IDatumOriginConsumer 주입 → 신규 타입 측정값 정상 | build + UAT | N/A | ❌ Wave 0 |

### Sampling Rate

- **Per task commit:** `MSBuild DatumMeasurement.csproj /p:Configuration=Debug /p:Platform=x64 /v:minimal /t:Rebuild`
- **Per wave merge:** Build PASS + SIMUL_MODE 에서 1개 신규 타입 측정 정상 동작 확인
- **Phase gate:** 31-UAT.md 전 시나리오 PASS + 사용자 사인오프 (`/gsd-verify-work`)

### Wave 0 Gaps

- [ ] `.planning/phases/31-datum-algorithm/31-UAT.md` — 신규 타입별 UAT 시나리오 scaffold
- [ ] SIMUL 이미지 준비 (Bottom Fixture #2, Side Fixture #3, Top Fixture #2) — 사용자 제공
- [ ] `IDatumOriginConsumer.cs` 신규 파일 + csproj 등록
- [ ] 9개 신규 .cs 파일 각각 csproj Compile ItemGroup 등록 확인

---

## Project Constraints (from CLAUDE.md)

- **Tech stack lock:** .NET Framework 4.8 + WPF + Halcon 24.11 — 변경 불가
- **Architecture lock:** SystemHandler 싱글턴 + SequenceBase/ActionBase + MeasurementBase 패턴 유지
- **C# 7.2:** nullable refs / switch expressions / records 금지. default param 허용.
- **INI 하위 호환:** `IsDynamicFAIMode` 분기 + `InspectionRecipeManager` 동적 INI 포맷 유지. 신규 타입도 동일 포맷.
- **Halcon 에러 처리:** `try { HOperatorSet.* } catch { return false; }` 패턴 — `TryIntersectCircleLine` 포함 모든 신규 Halcon 호출에 적용.
- **HImage 관리:** 짧은 수명 HImage 는 `using` 또는 명시적 `.Dispose()`.
- **주석 마커:** 신규/변경 라인 `//260519 hbk Phase 31`, 기존 마커 위 stacking.
- **MeasurePos 필수 3종 (memory feedback):** strip-loop(sampleCount for-loop 누적) + measurePhi 명시 매핑 + EdgeSelection 명시. 단일 MeasurePos 호출 금지. 신규 타입도 모두 `VisionAlgorithmService.TryFitLine` 을 통해 간접 호출.

---

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | ArcEdgeDistance(G 시리즈) = EdgeToLineDistance 와 동일 알고리즘. MSOP 원본 절차가 arc 피팅을 명시하지 않음 근거. SOP 슬라이드 이미지(S60~S67) 미확인. | D-08 재확인 결과 | 슬라이드 이미지에 arc 피팅 명시가 있으면 별도 구현 필요. Severity: MEDIUM — 타입 분리 구조(D-08)는 유지 가능 |
| A2 | I9/I10 의 3점 arc 피팅에서 `FitCircleContourXld` 3점 입력이 수렴함. 실측 테스트 미완료. | Pattern 5 HALCON 호 피팅 | 3점 수렴 불안정 시 다른 방법 필요(세 점의 외접원 수식으로 직접 계산). Severity: LOW — 외접원 수식으로 fallback 가능 |
| A3 | E9/E10 MSOP 평가방법란 "Angle" 표기 = pdftotext 오기. 절차 본문 "distance" 가 실제 의도. | D-07 재확인 결과 | 만약 실제로 각도라면 D-07 결정 위반. Severity: MEDIUM — discuss-phase 에서 이미 D-07 확정됨 |
| A4 | `VisionAlgorithmService.IntersectLines` 가 사용하는 `HOperatorSet.IntersectionLl` = 직선-직선 전용이며 원-직선 교점 API 없음. | Pattern 5 HALCON 교점 연산 | HALCON 24.11 에 `IntersectionCl`(원-직선) 같은 API가 있으면 수학 구현 불필요. 영향 낮음. |

---

## Sources

### Primary (HIGH confidence)

- `WPF_Example/Custom/Sequence/Inspection/Measurements/EdgeToLineDistanceMeasurement.cs` — D-04 추출 원본 L126~196, IDatumOriginConsumer transient 필드 L66~80, ICustomTypeDescriptor 패턴 L266~300 [VERIFIED: 직접 읽기]
- `WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs` L140~245 — D-03 일반화 지점 L161~185 [VERIFIED: 직접 읽기]
- `WPF_Example/Halcon/Algorithms/VisionAlgorithmService.cs` — TryFitLine(strip-loop) L18~183, TryFindCircle L237~317, AngleLineLine L567~592, IntersectLines L597~624 [VERIFIED: 직접 읽기]
- `WPF_Example/Custom/Sequence/Inspection/MeasurementFactory.cs` — 현재 7 타입 등록 [VERIFIED: 직접 읽기]
- `WPF_Example/Custom/Sequence/Inspection/MeasurementBase.cs` — TryExecute 시그니처, EvaluateJudgement [VERIFIED: 직접 읽기]
- `WPF_Example/Custom/Sequence/Inspection/Measurements/CircleDiameterMeasurement.cs` — TryFindCircle 호출 패턴 [VERIFIED: 직접 읽기]
- `WPF_Example/Custom/Sequence/Inspection/Measurements/LineToLineAngleMeasurement.cs` — AngleLineLine 호출 패턴 [VERIFIED: 직접 읽기]
- `WPF_Example/UI/ContentItem/MainView.xaml.cs` — ECanvasMode, CommitRectRoi, FindSelectedEdgeToLineMeasurement, CO-23.1-02 분기 구조 [VERIFIED: 직접 읽기]
- `.planning/tmp/pdf_text.txt` — MSOP 원본 G1~G12 p.104~111, E2 p.85, E8 p.91, E9 p.92, E10 p.93, D1 p.82~83, H5 p.115, I9 p.121, I10 p.122 [VERIFIED: 직접 읽기]

### Secondary (MEDIUM confidence)

- `31-SOP-REFERENCE.md` — Phase 31 SOP 다이제스트 (MSOP 재확인으로 일부 업데이트) [CITED: 프로젝트 파일]
- `31-CONTEXT.md` — D-01~D-11 locked decisions [CITED: 프로젝트 파일]
- `23-RESEARCH.md` — EdgeToLineDistance 코드 패턴, TryFitLine 시그니처, pitfall 목록 [CITED: 프로젝트 파일]

### Tertiary (LOW confidence)

- SOP 슬라이드 이미지(S60~S67) — ArcEdgeDistance 슬라이드 텍스트 미파싱. G 시리즈 측정점 정의 최종 확인 불가 [LOW: 이미지만 존재]

---

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — 기존 의존성 100% 재사용, 신규 추가 0
- Architecture (IDatumOriginConsumer, 공용 거리 함수): HIGH — 기존 코드 직접 검증 기반
- MSOP 절차 (G, D1, H5, I9/I10, E2/E8/E9/E10): HIGH — pdf_text.txt 원본 직접 확인
- ArcEdgeDistance 측정점 정의: MEDIUM — MSOP 는 단순 "포인트 캡처", 슬라이드 이미지 미확인
- 원-직선 교점 수학 구현: MEDIUM — HALCON API 미존재 확인됨, 수학은 표준
- 3점 호 피팅 수렴: LOW — 실측 테스트 미완료

**Research date:** 2026-05-19
**Valid until:** 30 days (코드 안정 — .NET 4.8 + Halcon 24.11 변경 없음 가정)
