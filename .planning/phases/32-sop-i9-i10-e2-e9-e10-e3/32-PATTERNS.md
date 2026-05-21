# Phase 32: 측정 알고리즘 SOP 재정합 - Pattern Map

**Mapped:** 2026-05-21
**Files analyzed:** 9 (5 rewrite + 1 new + 3 infra modify)
**Analogs found:** 9 / 9

> 본 phase 는 Phase 31 이 만든 측정 클래스 5종을 알고리즘만 재작성한다.
> 클래스의 **구조**(frontmatter, 구현 인터페이스, TypeName, ctor, IDatumOriginConsumer 4필드,
> EdgeOptionLists 래퍼, MeasurementBase 상속)는 그대로 유지하고 `TryExecute` 본문과
> ROI 필드 세트만 교체한다. 따라서 각 클래스는 **자기 자신이 구조 analog**이다.
> 새 알고리즘 본문 패턴은 Phase 31 UAT PASS 한 단일 ROI 타입(`ArcEdgeDistanceMeasurement`)을 참조한다.

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|-------------------|------|-----------|----------------|---------------|
| `Measurements/ArcLineIntersectDistanceMeasurement.cs` (rewrite) | measurement (algorithm class) | transform (ROI→edge→geometry) | 자기 구조 + `ArcEdgeDistanceMeasurement.cs` (단일 ROI 본문) | exact (구조) / role-match (본문) |
| `Measurements/CompoundAngleMeasurement.cs` (rewrite) | measurement (algorithm class) | transform | 자기 구조 + `ArcEdgeDistanceMeasurement.cs` | exact (구조) / role-match (본문) |
| `Measurements/CompoundCenterCDistanceMeasurement.cs` (rewrite) | measurement (algorithm class) | transform | 자기 구조 + `ArcEdgeDistanceMeasurement.cs` | exact (구조) / role-match (본문) |
| `Measurements/CompoundCenterBDistanceMeasurement.cs` (rewrite) | measurement (algorithm class) | transform | 자기 구조 + `CompoundCenterCDistanceMeasurement.cs` (쌍둥이) | exact |
| `Measurements/CompoundShortAxisDistanceMeasurement.cs` (E3, **new**) | measurement (algorithm class) | transform | `CompoundCenterCDistanceMeasurement.cs` (rewritten) | role-match |
| `Halcon/Algorithms/VisionAlgorithmService.cs` (modify) | service (Halcon building blocks) | transform | 기존 `TryFitArc` / `TryIntersectCircleLine` / `IntersectLines` (동일 파일 내 메서드) | exact |
| `MeasurementFactory.cs` (modify) | factory (string→instance) | request-response | 기존 `case "ArcEdgeDistance":` 등록 라인 | exact |
| `DatumConfig.cs` + `IDatumOriginConsumer.cs` + `Action_FAIMeasurement.cs` (modify — 검출 원중심 주입 채널) | model + interface + action | event-driven (티칭→측정 주입) | 기존 `DetectedRefAngle2` write-back + `IDatumOriginConsumer` 주입 경로 | exact |
| `UI/ContentItem/MainView.xaml.cs` (modify — ROI 티칭 배선) | view (code-behind) | event-driven (UI 드로잉) | 기존 `FindSelectedRectMeasurement` / `CommitRectRoi` / `BuildPointRoiDefinition` | exact |

## Pattern Assignments

### `ArcLineIntersectDistanceMeasurement.cs` (measurement, transform — rewrite)

**구조 analog:** 자기 자신 (Phase 31 D-01). **알고리즘 본문 analog:** `ArcEdgeDistanceMeasurement.cs`.

**유지할 구조** (현 파일 L1~107, L181~191):
- frontmatter `//YYMMDD hbk` 코멘트 컨벤션 — 신규 코멘트는 `//260521 hbk Phase 32 ...`
- 클래스 선언 `: MeasurementBase, IDatumOriginConsumer`, `TypeName` getter, ctor `(object owner) : base(owner)`
- `Edge` 카테고리 6 필드 (`EdgeThreshold/Sigma/EdgeSampleCount/EdgeTrimCount/EdgePolarity/EdgeDirection`)
- `EdgeDirectionList`/`EdgePolarityList` PropertyGrid ComboBox 래퍼 (`EdgeOptionLists.Directions` 등)
- `MeasureAxis` + `MeasureAxisList` (I9/I10 = X 기본)
- `IDatumOriginConsumer` 4 transient 필드 (`DatumOriginRow/Col/DatumAngleRad/DatumAngle2Rad`) — 3중 attribute:
```csharp
[System.ComponentModel.Browsable(false)]
[PropertyTools.DataAnnotations.Browsable(false)]
[Newtonsoft.Json.JsonIgnore]
public double DatumOriginRow { get; set; }
```

**제거할 구조** (Q5/Q확정 — INI 하위호환 불필요):
- `Arc_P1/P2/P3_*` 15 필드 (현 L22~40), `Line_*` 5 필드 (현 L43~48)

**신규 ROI 필드** — `ArcEdgeDistanceMeasurement.cs` L21~26 의 `Point_*` 5필드 세트를 **2벌** 복제 (수직 에지용 + 수평 에지용):
```csharp
[Category("EdgeA|ROI")]                       // 한쪽 수직 에지
public double EdgeA_Row { get; set; }
public double EdgeA_Col { get; set; }
public double EdgeA_Phi { get; set; }
public double EdgeA_Length1 { get; set; }
public double EdgeA_Length2 { get; set; }
// [Category("EdgeB|ROI")] EdgeB_* 동일 5필드 — 한쪽 수평 에지
```

**새 알고리즘 본문** — 현 파일 L113~119 의 `TryFitLine` 호출 패턴 그대로 사용 (각 ROI 1회씩 2회):
```csharp
var svc = new VisionAlgorithmService();
double a1r1, a1c1, a1r2, a1c2;
if (!svc.TryFitLine(image,
    EdgeA_Row, EdgeA_Col, EdgeA_Phi, EdgeA_Length1, EdgeA_Length2,
    datumTransform,
    EdgeSampleCount, EdgeTrimCount, Sigma, EdgeThreshold,
    EdgeDirection, EdgePolarity,
    out a1r1, out a1c1, out a1r2, out a1c2, out error,
    "All"))   // EdgeSelection "All" 고정 — memory feedback 필수
    return false;
// EdgeB 동일 → b1r1..b1c2
```
교점은 신규 `VisionAlgorithmService.TryIntersectLines` 래퍼 호출 (기존 static `IntersectLines` L615~642 와 동일 시그니처 — `out intRow, out intCol`, 평행/근접 시 false). 거리는 현 L194~198 그대로 `ComputeProjectionDistance(intRow, intCol, DatumOriginRow, DatumOriginCol, measureLineAngle, pixelResolution, MeasureAxis)`.

**미사용 헬퍼:** `svc.TryFitArc` / `VisionAlgorithmService.TryIntersectCircleLine` 호출 제거. CONTEXT.md 미해결#5 — 타 사용처 없으면 폐기 검토 (단 Compound* 도 재작성으로 `TryIntersectCircleLine` 사용 사라짐 → 전체 미사용 가능).

---

### `CompoundAngleMeasurement.cs` (measurement, transform — rewrite, E2)

**구조 analog:** 자기 자신 (Phase 31 D-11). **알고리즘 본문 analog:** `ArcEdgeDistanceMeasurement.cs` + 공통 컨투어 서비스 메서드.

**제거:** `Cl1/Cl2/Cl3_*` 9 필드 (현 L22~37), `La/Lb_*` 10 필드 (현 L40~53), `TryComputeChainPoint` private 헬퍼 (현 L100~179) — CL/La/Lb 체인 전체 폐기.

**유지:** 클래스 선언/`TypeName`="CompoundAngle"/ctor/`IDatumOriginConsumer` 4필드/Edge 6필드/ComboBox 래퍼.

**신규 ROI 필드** — `ArcEdgeDistanceMeasurement.cs` L21~26 의 `Point_*` 5필드 1벌 (`Rect_Row/Col/Phi/Length1/Length2`).

**신규 PropertyGrid 파라미터** (CONTEXT Q5) — Edge 6필드와 동형으로 `Contour` 카테고리 추가:
```csharp
[Category("Contour")]
public double CannyAlpha { get; set; } = 1.0;
public int CannyLow { get; set; } = 20;
public int CannyHigh { get; set; } = 40;
public double UnionDistance { get; set; } = 700.0;
```

**새 알고리즘 본문:**
1. 신규 공통 컨투어 서비스 호출 → `LargestRect` 중심 (`out centerRow, centerCol, phi, length1, length2`)
2. DatumC 검출 원중심 주입값 사용 (신규 4번째 채널 — `DatumDetectedCircleRow/Col`, 아래 인프라 참조)
3. 대각선 라인 = (centerRow,centerCol) ↔ (DatumDetectedCircleRow,Col)
4. DatumB 기준선 2점 — 현 파일 L215~220 패턴 그대로:
```csharp
double sinT = System.Math.Sin(DatumAngleRad), cosT = System.Math.Cos(DatumAngleRad);
double daR1 = DatumOriginRow - 200.0 * sinT;  double daC1 = DatumOriginCol - 200.0 * cosT;
double daR2 = DatumOriginRow + 200.0 * sinT;  double daC2 = DatumOriginCol + 200.0 * cosT;
resultValue = VisionAlgorithmService.AngleLineLine(
    centerRow, centerCol, circRow, circCol,   // 대각선
    daR1, daC1, daR2, daC2);                  // DatumB 기준선
```

---

### `CompoundCenterCDistanceMeasurement.cs` (measurement, transform — rewrite, E9)

**구조 analog:** 자기 자신. **본문 analog:** `ArcEdgeDistanceMeasurement.cs`.

**제거:** `Cl2/Cl3_*` + `La/Lb_*` 16 필드 + `TryComputeChainPoint`.

**신규 ROI:** `Rect_*` 5필드 1벌. **신규 파라미터:** `Contour` 카테고리 4필드 (E2 와 동일).

**새 본문:**
```csharp
// 공통 컨투어 서비스 → LargestRect 중심
double centerRow, centerCol, phi, len1, len2;
if (!svc.TryFindLargestContourRect(image, Rect_Row, Rect_Col, Rect_Phi,
        Rect_Length1, Rect_Length2, datumTransform,
        CannyAlpha, CannyLow, CannyHigh, UnionDistance,
        out centerRow, out centerCol, out phi, out len1, out len2, out error))
    return false;
// E9 = LargestRect 중심 → Datum C X 거리. 현 L206~210 그대로 유지.
double measureLineAngle = (MeasureAxis == "X") ? DatumAngle2Rad : DatumAngleRad;
resultValue = VisionAlgorithmService.ComputeProjectionDistance(
    centerRow, centerCol, DatumOriginRow, DatumOriginCol,
    measureLineAngle, pixelResolution, MeasureAxis);
```
`MeasureAxis` 기본값 "X" 유지 (E9 = Datum C X).

---

### `CompoundCenterBDistanceMeasurement.cs` (measurement, transform — rewrite, E10)

**analog:** `CompoundCenterCDistanceMeasurement.cs` (재작성본) — **완전 쌍둥이**. 차이 2개만:
- `TypeName` getter `"CompoundCenterBDistance"`
- `MeasureAxis` 기본값 `"Y"` (현 L71, `MeasureAxisList` 도 `{ "Y", "X" }`)

E9 재작성본을 그대로 복제 후 위 2개만 변경.

---

### `CompoundShortAxisDistanceMeasurement.cs` (measurement, transform — NEW, E3)

**analog:** `CompoundCenterCDistanceMeasurement.cs` (재작성본).

E3 = SOP p.50 La/Lb 2직선 거리, 공차 **0.600±0.030** (NominalValue/Tolerance 는 레시피 INI 값 — 클래스는 default 미설정).

E9 재작성본을 복제하되 차이:
- `TypeName` getter — CONTEXT 미해결#1: `"CompoundShortAxisDistance"` 권장 (계획 시 확정)
- 결과 = LargestRect 단축 폭 — `IDatumOriginConsumer`/`MeasureAxis`/Datum 의존 **불필요** (단축 폭은 사각형 자체 기하). 단 `IDatumOriginConsumer` 구현 유지 여부는 계획 시 결정 (Datum 미사용이면 인터페이스 미구현 + 4필드 제거 가능).
- 새 본문: 공통 컨투어 서비스로 `shape_trans` 된 LargestRect XLD 획득 → 단축 방향(`phi + π/2`) 선과 사각형 컨투어의 교점 2개 → 두 점 거리 × pixelResolution.
  교점은 `intersection_contours_xld` — 신규 서비스 메서드 `TryIntersectContours` (CONTEXT 미해결#3 참조 — 평행/0교점 안전 종결).

---

### `Halcon/Algorithms/VisionAlgorithmService.cs` (service — modify)

**analog:** 동일 파일 내 기존 메서드 — 신규 메서드 3개 추가.

**(1) 공통 컨투어 알고리즘 메서드** — `TryFindCircle` (L249~329) 의 `GenCircle→ReduceDomain→EdgesSubPix` 패턴 + `TryFitLine` (L19~) 의 ROI/datumTransform 시그니처 차용:
```csharp
//260521 hbk Phase 32 — 공통 컨투어 알고리즘 (E2/E3/E9/E10 공유). LargestRect 산출.
public bool TryFindLargestContourRect(
    HImage image,
    double roiRow, double roiCol, double roiPhi, double roiLength1, double roiLength2,
    HTuple datumTransform,
    double cannyAlpha, int cannyLow, int cannyHigh, double unionDistance,
    out double centerRow, out double centerCol, out double phi,
    out double length1, out double length2, out string error)
{
    centerRow = centerCol = phi = length1 = length2 = 0;
    error = null;
    if (image == null) { error = "image is null"; return false; }
    HObject rect = null, imageReduced = null, edges = null,
            unionContours = null, rectXld = null, largestRect = null;
    try {
        // datumTransform 적용 — TryFitLine L45~60 동일 패턴
        // GenRectangle2 → ReduceDomain → EdgesSubPix("canny",alpha,low,high)
        //   → UnionAdjacentContoursXld(unionDistance,1,"attr_keep")
        //   → ShapeTransXld("rectangle2") → AreaCenterXld → TupleMax/TupleFind
        //   → SelectObj(maxIdx+1) → SmallestRectangle2Xld
        ...
        return true;
    }
    catch (Exception ex) { error = ex.Message; return false; }   // CONTEXT 미해결#4 — 0개 검출 시 false
    finally { /* 모든 HObject Dispose — TryFindCircle L325~328 패턴 */ }
}
```
**주의:** `HOperatorSet.*` 호출은 전부 `try { } catch { return false; }` (CLAUDE.md 컨벤션, 현 파일 전체 동일). `union/canny 가 사각형 0개` → `error` 세팅 후 false (안전 종결).

**(2) `intersection_lines` 래퍼** — 기존 static `IntersectLines` (L615~642) 와 동일 시그니처/가드 패턴. `intersection_lines` HALCON 연산자로 교체하거나 기존 `IntersectionLl` 유지(직선-직선은 동등). 평행/근접 시 `isOverlapping`/`IsInfinity` 가드로 false. 신규 public 이름 `TryIntersectLines` 권장 (ArcLineIntersect 호출용).

**(3) `intersection_contours_xld` 활용** — `TryFitArc` (L736~768) 의 `GenContourPolygonXld` + try/catch/finally 골격 차용. 신규 `TryIntersectContours` (E3 단축 거리용 — 단축선 ↔ 사각형 XLD 교점 2점).

**미사용 검토:** `TryFitArc` (L736~768), `TryIntersectCircleLine` (L775~811) — Phase 32 재작성 후 호출처 없음 → CONTEXT 미해결#5, 폐기 가능 (계획 시 grep 확인).

---

### `MeasurementFactory.cs` (factory — modify)

**analog:** 기존 등록 라인 (L34~43). E3 신규 타입 1개 추가 — `Create` switch + `GetTypeNames` 배열 양쪽:
```csharp
// Create() switch 에 추가:
case "CompoundShortAxisDistance": //260521 hbk Phase 32 E3
    return new CompoundShortAxisDistanceMeasurement(owner);
// GetTypeNames() 배열에 추가:
"CompoundShortAxisDistance", //260521 hbk Phase 32 E3
```
TypeName 문자열은 클래스 `TypeName` getter 와 **반드시 일치** (CONTEXT 미해결#1 — 계획 시 확정). 기존 `ArcLineIntersectDistance`/`CompoundAngle`/`CompoundCenterCDistance`/`CompoundCenterBDistance` 등록은 TypeName 미변경 시 수정 불필요.

---

### DatumC 검출 원중심 주입 채널 (model + interface + action — modify)

CONTEXT Q3(a): `IDatumOriginConsumer` 확장. 기존 `DatumAngle2Rad` 가 Phase 31 hotfix#3 에서 인터페이스에 추가된 패턴 그대로 따른다.

**(a) `DatumConfig.cs`** — `DetectedRefAngle2` (L466~471) 와 동형으로 검출 원중심 transient 필드 신설:
```csharp
//260521 hbk Phase 32 — CircleTwoHorizontal 검출 원(B1 홀) 중심. E2 CompoundAngle 주입용.
[System.ComponentModel.Browsable(false)]
[PropertyTools.DataAnnotations.Browsable(false)]
[Newtonsoft.Json.JsonIgnore]
public double DetectedCircleRow { get; set; }
public double DetectedCircleCol { get; set; }   // 3중 attribute 반복
```
`DatumFindingService.TryFindCircleTwoHorizontal` (L190~) 가 검출 원중심을 write-back — 현재 `DetectedOriginRow` (L315) 에 datum 원점만 쓰므로, 원중심도 별도 write-back 추가 (DatumFindingService 도 수정 대상 — 계획 시 plan 에 포함).

**(b) `IDatumOriginConsumer.cs`** — 인터페이스에 2 프로퍼티 추가 (`DatumAngle2Rad` L16 추가 패턴 동일):
```csharp
//260521 hbk Phase 32 — DatumC 검출 원(B1 홀) 중심. E2 만 사용, 타 타입은 0 주입.
double DatumDetectedCircleRow { get; set; }
double DatumDetectedCircleCol { get; set; }
```
**전 IDatumOriginConsumer 구현 클래스가 이 2 프로퍼티를 추가 구현해야 함** (인터페이스 확장이므로) — `ArcEdgeDistanceMeasurement`/`EdgeToLineDistanceMeasurement`/`EdgeToLineAngleMeasurement`/`CircleCenterDistanceMeasurement` 등. E2 외 타입은 transient 4필드와 동일하게 3중 attribute 부착 후 미사용 (0 주입). **plan 시 영향 범위 grep 필수.**

**(c) `Action_FAIMeasurement.cs`** — `EStep.Measure` 의 IDatumOriginConsumer 주입 블록 (L160~185) 에 2줄 추가:
```csharp
if (dc != null) {
    consumer.DatumOriginRow = dc.DetectedOriginRow;
    consumer.DatumOriginCol = dc.DetectedOriginCol;
    consumer.DatumAngleRad  = dc.DetectedRefAngle;
    consumer.DatumAngle2Rad = dc.DetectedRefAngle2;
    consumer.DatumDetectedCircleRow = dc.DetectedCircleRow; //260521 hbk Phase 32
    consumer.DatumDetectedCircleCol = dc.DetectedCircleCol; //260521 hbk Phase 32
}
else { /* 미주입 분기에도 0 세팅 2줄 추가 — L181~184 패턴 */ }
```

---

### `UI/ContentItem/MainView.xaml.cs` (view — modify, ROI 티칭 배선)

**analog:** 기존 `FindSelectedRectMeasurement` (L1440~1469) / `CommitRectRoi` (L1287~1348) / `BuildPointRoiDefinition` (L222~242).

**E2/E3/E9/E10 = 단일 Rect ROI** — 기존 단일 ROI 경로 그대로 사용. 3곳에 신규 E3 타입(+ ArcLineIntersect 가 신규 ROI 필드명을 쓰면 그곳도) 분기 추가:

- `FindSelectedRectMeasurement` (L1444~1450, L1458~1464) — `is` 화이트리스트에 `CompoundShortAxisDistanceMeasurement` 추가. ArcLineIntersect/CompoundAngle/CompoundCenterC·B 는 이미 등록됨 (L1447~1450) — 유지.
- `CommitRectRoi` (L1304~1309) — `as` 캐스트 분기 추가. 기존은 `EdgeToLineDistance/EdgeToLineAngle/ArcEdgeDistance` 의 `Point_*` 만 기록. 재작성된 Compound* 가 `Rect_*` 필드명을 쓰면 새 분기 필요:
```csharp
var cca = _editingMeasurement as CompoundAngleMeasurement; //260521 hbk Phase 32
if (cca != null) { cca.Rect_Row=mCenterRow; cca.Rect_Col=mCenterCol; cca.Rect_Phi=0.0; cca.Rect_Length1=mHalfHeight; cca.Rect_Length2=mHalfWidth; }
// CompoundCenterC/B/ShortAxis 동일
```
- `BuildPointRoiDefinition` (L224~229) — `as` 캐스트 분기 추가 (캔버스 렌더용 RoiDefinition 변환). 신규 `Rect_*` 필드명 사용.

**ArcLineIntersect = Rect 2개 티칭** (CONTEXT 미해결#2 — 순차 2회 드로잉 vs Datum 위저드 축소). 계획 시 UX 확정 필요. 순차 2회 드로잉이면 `_editingMeasurement` + 활성 ROI 인덱스(0/1) 상태 1개 추가, `CommitRectRoi` 의 measurement 분기에서 `EdgeA_*`/`EdgeB_*` 중 활성 ROI 만 기록. Datum 위저드 축소면 `EDatumTeachStep` 류 step enum 패턴 참조.

## Shared Patterns

### `//YYMMDD hbk` 코멘트 컨벤션
**Source:** memory `feedback_comment_convention.md` + 전 측정 클래스
**Apply to:** 모든 수정/신규 파일
모든 변경 라인에 `//260521 hbk Phase 32 ...` 부착. 기존 Phase 31 코멘트(`//260519 hbk Phase 31 ...`)는 알고리즘 폐기 시 함께 제거.

### HALCON 호출 try/catch 가드
**Source:** `VisionAlgorithmService.cs` 전체 (특히 `TryFitLine` L41~195, `TryFindCircle` L269~328)
**Apply to:** `VisionAlgorithmService` 신규 메서드 3개
모든 `HOperatorSet.*` 를 `try { } catch (Exception ex) { error = ex.Message; return false; }` 로 감싸고, 모든 `HObject` 를 `finally` 에서 `try { x.Dispose(); } catch { }` 로 해제. 실패 = `false` 반환 (예외 throw 금지 — CLAUDE.md 컨벤션).

### EdgeSelection "All" 명시 고정
**Source:** memory `feedback_halcon_measurepos_must_haves.md` + 전 측정 클래스의 `TryFitLine(..., "All")` 호출
**Apply to:** ArcLineIntersect 의 2회 `TryFitLine` 호출
`TryFitLine` 마지막 인자 `selection`을 반드시 `"All"` 로 명시. 단일 MeasurePos 금지 — `TryFitLine` 내부가 strip-loop 누적 (`VisionAlgorithmService.cs` L104~148).

### IDatumOriginConsumer transient 필드 3중 attribute
**Source:** `ArcEdgeDistanceMeasurement.cs` L53~68 / `IDatumOriginConsumer.cs`
**Apply to:** 전 측정 클래스의 datum 주입 필드 (+ 신규 `DatumDetectedCircle*`)
```csharp
[System.ComponentModel.Browsable(false)]      // ICustomTypeDescriptor 경로 안전판
[PropertyTools.DataAnnotations.Browsable(false)]  // PropertyGrid 미표시
[Newtonsoft.Json.JsonIgnore]                  // JSON 직렬화 제외
```

### PropertyGrid 사용자 편집 파라미터
**Source:** `ArcEdgeDistanceMeasurement.cs` L28~36 (`[Category("Edge")]` + 기본값)
**Apply to:** E2/E3/E9/E10 의 신규 canny/union 파라미터
일반 `public` 자동 프로퍼티 + `[Category("...")]` + C# 기본값. INI 직렬화는 `ParamBase` reflection 이 자동 처리. ComboBox 가 필요하면 `[ItemsSourceProperty(nameof(XxxList))]` + `[Browsable(false)]` 래퍼 리스트 (Edge 6필드 패턴).

### MeasurementBase 표준 멤버
**Source:** `MeasurementBase.cs` L46~60
**Apply to:** E3 신규 클래스
`abstract string TypeName` 구현, `abstract bool TryExecute(HImage, HTuple, double, out double, out string, out List<EdgeInspectionOverlay>)` override, ctor `(object owner) : base(owner)`. 공차 판정(`EvaluateJudgement`)/`NominalValue`/`TolerancePlus`/`ToleranceMinus` 는 base 가 제공 — 클래스에서 재정의 금지.

## No Analog Found

없음 — 모든 신규/수정 파일이 동일 디렉터리 내 강한 analog 보유.

| File | 비고 |
|------|------|
| `intersection_lines` / `intersection_contours_xld` HALCON 연산자 자체 | 코드 analog 없음 — HALCON 문서(memory `halcon_2d_measuring.md`) 참조. C# 래핑 골격은 기존 `IntersectLines`/`TryFitArc` 패턴 사용. |

## Metadata

**Analog search scope:**
- `WPF_Example/Custom/Sequence/Inspection/` (측정 클래스, factory, base, interface, action, config)
- `WPF_Example/Halcon/Algorithms/` (VisionAlgorithmService, DatumFindingService)
- `WPF_Example/UI/ContentItem/MainView.xaml.cs` (ROI 티칭 배선)

**Files scanned:** 13 (read) — `.claude/worktrees/` 미러는 노이즈로 제외, 실 소스 트리만 분석.
**Pattern extraction date:** 2026-05-21
