# Phase 31: Datum 기준 측정 알고리즘 확장 — Pattern Map

**Mapped:** 2026-05-19
**Files analyzed:** 13 (9 new + 4 modified)
**Analogs found:** 13 / 13

---

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|---|---|---|---|---|
| `Custom/Sequence/Inspection/IDatumOriginConsumer.cs` | interface | — | `Device/IAxisController.cs` | role-match (interface) |
| `Custom/Sequence/Inspection/Measurements/CircleCenterDistanceMeasurement.cs` | measurement | request-response | `Measurements/CircleDiameterMeasurement.cs` + `EdgeToLineDistanceMeasurement.cs` | exact (circle ROI + datum distance) |
| `Custom/Sequence/Inspection/Measurements/EdgeToLineAngleMeasurement.cs` | measurement | request-response | `Measurements/LineToLineAngleMeasurement.cs` + `EdgeToLineDistanceMeasurement.cs` | exact (TryFitLine + AngleLineLine + datum) |
| `Custom/Sequence/Inspection/Measurements/ArcLineIntersectDistanceMeasurement.cs` | measurement | request-response | `Measurements/CircleDiameterMeasurement.cs` + `EdgeToLineDistanceMeasurement.cs` | role-match (arc fit + line fit + distance) |
| `Custom/Sequence/Inspection/Measurements/CompoundAngleMeasurement.cs` | measurement | request-response | `Measurements/LineToLineAngleMeasurement.cs` + `Measurements/CircleDiameterMeasurement.cs` | role-match (multi-step geometry chain → angle) |
| `Custom/Sequence/Inspection/Measurements/CompoundCenterCDistanceMeasurement.cs` | measurement | request-response | `Measurements/EdgeToLineDistanceMeasurement.cs` | role-match (geometry chain → distance Datum C) |
| `Custom/Sequence/Inspection/Measurements/CompoundCenterBDistanceMeasurement.cs` | measurement | request-response | `Measurements/EdgeToLineDistanceMeasurement.cs` | role-match (geometry chain → distance Datum B) |
| `Custom/Sequence/Inspection/Measurements/ArcEdgeDistanceMeasurement.cs` | measurement | request-response | `Measurements/EdgeToLineDistanceMeasurement.cs` | exact (identical algorithm, MeasureAxis "X" default) |
| `Halcon/Algorithms/VisionAlgorithmService.cs` *(modified)* | service | CRUD/transform | self (existing static helpers) | self-analog |
| `Custom/Sequence/Inspection/MeasurementFactory.cs` *(modified)* | factory | CRUD | self (existing switch+array) | self-analog |
| `Custom/Sequence/Inspection/Action_FAIMeasurement.cs` *(modified)* | action | request-response | self (L161~185 existing block) | self-analog |
| `Custom/Sequence/Inspection/Measurements/EdgeToLineDistanceMeasurement.cs` *(modified)* | measurement | request-response | self (add interface declaration) | self-analog |
| `UI/ContentItem/MainView.xaml.cs` *(modified)* | UI / code-behind | request-response | self (FindSelectedEdgeToLineMeasurement L1377) | self-analog |

---

## Pattern Assignments

### `Custom/Sequence/Inspection/IDatumOriginConsumer.cs` (interface, new)

**Analog:** `WPF_Example/Device/IAxisController.cs`

**Interface convention pattern** (IAxisController.cs lines 1-12):
```csharp
//260409 hbk Phase 5: Z축 이동 인터페이스 스텁 (D-09)
namespace ReringProject.Device {

    /// <summary>
    /// Z축 모터 제어 인터페이스. 실제 HW 연동은 별도 Phase에서 구현.
    /// </summary>
    public interface IAxisController {
        bool MoveToPosition(double positionMm);
        bool IsMoveDone { get; }
        double CurrentPosition { get; }
    }
}
```

**Apply pattern (D-03):**
- 파일 단독: using 없음 (인터페이스 멤버 double/string only)
- namespace: `ReringProject.Sequence` (Measurement layer 와 동일)
- Allman brace 스타일 (IAxisController 는 K&R, 이 파일은 Measurement layer 신규 파일 → Allman 준수)
- XMLDoc `<summary>` 필수 (public interface)
- 마커: `//260519 hbk Phase 31 D-03`

**Full skeleton** (RESEARCH.md Pattern 1 기준):
```csharp
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

**csproj 등록:**
```xml
<Compile Include="Custom\Sequence\Inspection\IDatumOriginConsumer.cs" />
```
등록 위치: `DatumMeasurement.csproj` line 225 (`EdgeToLineDistanceMeasurement.cs`) 바로 아래.

---

### `Custom/Sequence/Inspection/Measurements/CircleCenterDistanceMeasurement.cs` (measurement, new — E8)

**Primary analog:** `Measurements/CircleDiameterMeasurement.cs`
**Secondary analog:** `Measurements/EdgeToLineDistanceMeasurement.cs` (datum 주입 3필드 + projection_pl 블록)

**Imports pattern** (CircleDiameterMeasurement.cs lines 1-7):
```csharp
//260519 hbk Phase 31 D-01 — E8: 원중심 → Datum B Y 거리
using System.Collections.Generic;
using HalconDotNet;
using PropertyTools.DataAnnotations;
using ReringProject.Halcon.Algorithms;
using ReringProject.Halcon.Models;

namespace ReringProject.Sequence
{
```

**Class declaration with interface** (copy CircleDiameterMeasurement + add IDatumOriginConsumer):
```csharp
public class CircleCenterDistanceMeasurement : MeasurementBase, IDatumOriginConsumer //260519 hbk Phase 31 D-01
{
    public override string TypeName { get { return "CircleCenterDistance"; } }
```

**ROI fields pattern** (CircleDiameterMeasurement.cs lines 18-21, Category "Circle|ROI"):
```csharp
[Category("Circle|ROI")]
public double Circle_Row { get; set; }
public double Circle_Col { get; set; }
public double Circle_Radius { get; set; }
```

**Edge fields pattern** (CircleDiameterMeasurement.cs lines 23-31):
```csharp
[Category("Edge")]
public int EdgeThreshold { get; set; } = 10;
public double Sigma { get; set; } = 1.0;
[ItemsSourceProperty(nameof(EdgePolarityList))]
public string EdgePolarity { get; set; } = "DarkToLight";

[PropertyTools.DataAnnotations.Browsable(false)]
public List<string> EdgePolarityList { get { return EdgeOptionLists.FAIPolarities; } }
```

**MeasureAxis field** (EdgeToLineDistanceMeasurement.cs lines 59-64):
```csharp
[Category("Edge")]
[ItemsSourceProperty(nameof(MeasureAxisList))]
public string MeasureAxis { get; set; } = "Y"; // E8 = Datum B Y 방향 — 기본값 "Y"
[PropertyTools.DataAnnotations.Browsable(false)]
public List<string> MeasureAxisList { get { return new List<string> { "Y", "X" }; } }
```

**IDatumOriginConsumer transient 3필드** (EdgeToLineDistanceMeasurement.cs lines 69-80 — 동일하게 복사):
```csharp
[System.ComponentModel.Browsable(false)]
[PropertyTools.DataAnnotations.Browsable(false)]
[Newtonsoft.Json.JsonIgnore]
public double DatumOriginRow { get; set; }
[System.ComponentModel.Browsable(false)]
[PropertyTools.DataAnnotations.Browsable(false)]
[Newtonsoft.Json.JsonIgnore]
public double DatumOriginCol { get; set; }
[System.ComponentModel.Browsable(false)]
[PropertyTools.DataAnnotations.Browsable(false)]
[Newtonsoft.Json.JsonIgnore]
public double DatumAngleRad { get; set; }
```

**TryExecute core pattern** (CircleDiameterMeasurement.cs lines 42-96 + EdgeToLineDistanceMeasurement.cs lines 126-196):
```csharp
public override bool TryExecute(
    HImage image, HTuple datumTransform, double pixelResolution,
    out double resultValue, out string error,
    out List<EdgeInspectionOverlay> overlays)
{
    resultValue = 0; error = null;
    overlays = new List<EdgeInspectionOverlay>();

    var svc = new VisionAlgorithmService();
    double foundRow, foundCol, foundRadius;
    if (!svc.TryFindCircle(image,
        Circle_Row, Circle_Col, Circle_Radius,
        datumTransform,
        Sigma, EdgeThreshold, EdgePolarity,
        out foundRow, out foundCol, out foundRadius, out error))
    {
        return false;
    }
    // D-04 공용 헬퍼 호출 (VisionAlgorithmService.ComputeProjectionDistance)
    resultValue = VisionAlgorithmService.ComputeProjectionDistance(
        foundRow, foundCol,
        DatumOriginRow, DatumOriginCol, DatumAngleRad,
        pixelResolution, MeasureAxis);
    return true;
}
```

**csproj 등록:**
```xml
<Compile Include="Custom\Sequence\Inspection\Measurements\CircleCenterDistanceMeasurement.cs" />
```

---

### `Custom/Sequence/Inspection/Measurements/EdgeToLineAngleMeasurement.cs` (measurement, new — D1/H5)

**Primary analog:** `Measurements/LineToLineAngleMeasurement.cs`
**Secondary analog:** `Measurements/EdgeToLineDistanceMeasurement.cs` (IDatumOriginConsumer 3필드 + Point_* ROI)

**Imports pattern** (LineToLineAngleMeasurement.cs lines 1-7):
```csharp
//260519 hbk Phase 31 D-05 — D1/H5: Datum A 기준 직선 각도
using System.Collections.Generic;
using HalconDotNet;
using PropertyTools.DataAnnotations;
using ReringProject.Halcon.Algorithms;
using ReringProject.Halcon.Models;
```

**Class declaration:**
```csharp
public class EdgeToLineAngleMeasurement : MeasurementBase, IDatumOriginConsumer //260519 hbk Phase 31 D-05
{
    public override string TypeName { get { return "EdgeToLineAngle"; } }
```

**ROI fields** — EdgeToLineDistanceMeasurement `Point_*` 패턴 그대로 (lines 24-29):
```csharp
[Category("Point|ROI")]
public double Point_Row { get; set; }
public double Point_Col { get; set; }
public double Point_Phi { get; set; }
public double Point_Length1 { get; set; }
public double Point_Length2 { get; set; }
```

**Edge fields** (LineToLineAngleMeasurement.cs lines 32-46, 공유 pattern):
```csharp
[Category("Edge")]
public int EdgeThreshold { get; set; } = 10;
public double Sigma { get; set; } = 1.0;
public int EdgeSampleCount { get; set; } = 20;
public int EdgeTrimCount { get; set; } = 10;
[ItemsSourceProperty(nameof(EdgePolarityList))]
public string EdgePolarity { get; set; } = "DarkToLight";
[ItemsSourceProperty(nameof(EdgeDirectionList))]
public string EdgeDirection { get; set; } = "TtoB";

[PropertyTools.DataAnnotations.Browsable(false)]
public List<string> EdgeDirectionList { get { return EdgeOptionLists.Directions; } }
[PropertyTools.DataAnnotations.Browsable(false)]
public List<string> EdgePolarityList { get { return EdgeOptionLists.FAIPolarities; } }
```

**IDatumOriginConsumer 3필드** — EdgeToLineDistanceMeasurement.cs lines 69-80 동일 복사.

**TryExecute core pattern** (LineToLineAngleMeasurement.cs lines 50-90 변형):
```csharp
// (1) TryFitLine — "All" 고정 (D-08 패턴)
double pr1, pc1, pr2, pc2;
if (!svc.TryFitLine(image,
    Point_Row, Point_Col, Point_Phi, Point_Length1, Point_Length2,
    datumTransform,
    EdgeSampleCount, EdgeTrimCount, Sigma, EdgeThreshold,
    EdgeDirection, EdgePolarity,
    out pr1, out pc1, out pr2, out pc2, out error,
    "All"))
{
    return false;
}
// (2) Datum A 기준선 2점 구성 (DatumOriginRow/Col 중심 ±200px)
double sinT = System.Math.Sin(DatumAngleRad);
double cosT = System.Math.Cos(DatumAngleRad);
double daR1 = DatumOriginRow - 200.0 * sinT;
double daC1 = DatumOriginCol - 200.0 * cosT;
double daR2 = DatumOriginRow + 200.0 * sinT;
double daC2 = DatumOriginCol + 200.0 * cosT;
// (3) AngleLineLine (VisionAlgorithmService L567 — static)
resultValue = VisionAlgorithmService.AngleLineLine(
    pr1, pc1, pr2, pc2,
    daR1, daC1, daR2, daC2);
return true;
```

**csproj 등록:**
```xml
<Compile Include="Custom\Sequence\Inspection\Measurements\EdgeToLineAngleMeasurement.cs" />
```

---

### `Custom/Sequence/Inspection/Measurements/ArcLineIntersectDistanceMeasurement.cs` (measurement, new — I9/I10)

**Primary analog:** `Measurements/CircleDiameterMeasurement.cs` (TryFindCircle 패턴)
**Secondary analog:** `Measurements/EdgeToLineDistanceMeasurement.cs` (IDatumOriginConsumer + TryFitLine "All" + ComputeProjectionDistance)

**Class declaration:**
```csharp
public class ArcLineIntersectDistanceMeasurement : MeasurementBase, IDatumOriginConsumer //260519 hbk Phase 31 D-01
{
    public override string TypeName { get { return "ArcLineIntersectDistance"; } }
```

**ROI fields — 3점 arc + 1라인:**
```csharp
// Arc 3점 ROI (각 Point 에서 TryFitLine 으로 에지점 1개 추출 후 3점 FitCircleContourXld)
[Category("Arc|ROI")]
public double Arc_P1_Row { get; set; }
public double Arc_P1_Col { get; set; }
public double Arc_P1_Phi { get; set; }
public double Arc_P1_Length1 { get; set; }
public double Arc_P1_Length2 { get; set; }

public double Arc_P2_Row { get; set; }
public double Arc_P2_Col { get; set; }
public double Arc_P2_Phi { get; set; }
public double Arc_P2_Length1 { get; set; }
public double Arc_P2_Length2 { get; set; }

public double Arc_P3_Row { get; set; }
public double Arc_P3_Col { get; set; }
public double Arc_P3_Phi { get; set; }
public double Arc_P3_Length1 { get; set; }
public double Arc_P3_Length2 { get; set; }

[Category("Line|ROI")]
public double Line_Row { get; set; }
public double Line_Col { get; set; }
public double Line_Phi { get; set; }
public double Line_Length1 { get; set; }
public double Line_Length2 { get; set; }
```

**Edge fields** — LineToLineAngleMeasurement 동일 패턴 (EdgeThreshold/Sigma/EdgeSampleCount/EdgeTrimCount/EdgePolarity/EdgeDirection).

**MeasureAxis** — EdgeToLineDistanceMeasurement 패턴, 기본값 `"X"` (Datum C X 방향).

**IDatumOriginConsumer 3필드** — EdgeToLineDistanceMeasurement.cs lines 69-80 동일.

**TryExecute core pattern:**
```csharp
// (1) 3점 에지 추출 (TryFitLine sampleCount=1 → 에지점 1개)
// 각 Point ROI 에서 TryFitLine → 라인 피팅 결과 중점을 arc 포인트로 사용
// (2) 3점으로 FitCircleContourXld (GenContourPolygonXld → FitCircleContourXld)
// (3) TryFitLine → 라인 (pr1/pc1/pr2/pc2)
// (4) VisionAlgorithmService.TryIntersectCircleLine(
//       foundRow, foundCol, foundRadius,
//       pr1, pc1, pr2, pc2,
//       Line_Row, Line_Col,     // D-10: 라인 ROI 중심 = 교점 선택 기준
//       out intRow, out intCol)
// (5) VisionAlgorithmService.ComputeProjectionDistance(intRow, intCol, DatumOriginRow, ...)
```

**csproj 등록:**
```xml
<Compile Include="Custom\Sequence\Inspection\Measurements\ArcLineIntersectDistanceMeasurement.cs" />
```

---

### `Custom/Sequence/Inspection/Measurements/CompoundAngleMeasurement.cs` (measurement, new — E2)

**Primary analog:** `Measurements/LineToLineAngleMeasurement.cs` (AngleLineLine 패턴)
**Secondary analog:** `Measurements/CircleDiameterMeasurement.cs` (TryFindCircle 3회)

**Class declaration:**
```csharp
public class CompoundAngleMeasurement : MeasurementBase, IDatumOriginConsumer //260519 hbk Phase 31 D-11
{
    public override string TypeName { get { return "CompoundAngle"; } }
```

**ROI fields — 3 circles + 2 lines (E2 기하 체인):**
```csharp
[Category("CL1|ROI")]
public double Cl1_Row { get; set; }
public double Cl1_Col { get; set; }
public double Cl1_Radius { get; set; }

[Category("CL2|ROI")]
public double Cl2_Row { get; set; }
public double Cl2_Col { get; set; }
public double Cl2_Radius { get; set; }

[Category("CL3|ROI")]
public double Cl3_Row { get; set; }
public double Cl3_Col { get; set; }
public double Cl3_Radius { get; set; }

[Category("La|ROI")]
public double La_Row { get; set; }
public double La_Col { get; set; }
public double La_Phi { get; set; }
public double La_Length1 { get; set; }
public double La_Length2 { get; set; }

[Category("Lb|ROI")]
public double Lb_Row { get; set; }
public double Lb_Col { get; set; }
public double Lb_Phi { get; set; }
public double Lb_Length1 { get; set; }
public double Lb_Length2 { get; set; }
```

**Edge fields** — CircleDiameterMeasurement + LineToLineAngleMeasurement 공통 패턴 (EdgeThreshold/Sigma/EdgeSampleCount/EdgeTrimCount/EdgePolarity/EdgeDirection + EdgePolarityList/EdgeDirectionList).

**IDatumOriginConsumer 3필드** — EdgeToLineDistanceMeasurement.cs lines 69-80 동일.

**TryExecute core pattern** (RESEARCH.md Pattern 6 + TryComputeChainPc 헬퍼):
```csharp
// (1) TryFindCircle × 3 → cl1Center(Pd), cl2Center, cl3Center
// (2) TryFitLine × 2 (La, Lb) → la1/2, lb1/2
// (3) TryComputeChainPc: midline Lc + TryIntersectCircleLine × 2 → Pa, Pb → Pc = midpoint
// (4) E2 전용: line Ld = (Pd → Pc)
// (5) AngleLineLine(Ld, DatumB 기준선)
// resultValue = 각도(degree) — pixelResolution 미적용
```

**TryComputeChainPc 내부 private 헬퍼** — RESEARCH.md Code Examples 섹션 전체 코드 참조 (midline + TryIntersectCircleLine 체인).

**csproj 등록:**
```xml
<Compile Include="Custom\Sequence\Inspection\Measurements\CompoundAngleMeasurement.cs" />
```

---

### `Custom/Sequence/Inspection/Measurements/CompoundCenterCDistanceMeasurement.cs` (measurement, new — E9)

**Primary analog:** `Measurements/EdgeToLineDistanceMeasurement.cs` (ComputeProjectionDistance 패턴)
**Secondary analog:** `CompoundAngleMeasurement.cs` (동일 기하 체인 — CL2/CL3/La/Lb)

**Class declaration:**
```csharp
public class CompoundCenterCDistanceMeasurement : MeasurementBase, IDatumOriginConsumer //260519 hbk Phase 31 D-11
{
    public override string TypeName { get { return "CompoundCenterCDistance"; } }
```

**ROI fields** — CompoundAngleMeasurement 와 동일 (CL2, CL3, La, Lb — CL1 생략). Category 이름 동일.

**MeasureAxis** — 기본값 `"X"` (Datum C X 방향, D-07/D-08 확정).

**IDatumOriginConsumer 3필드** — EdgeToLineDistanceMeasurement.cs lines 69-80 동일.

**TryExecute core pattern:**
```csharp
// (1) TryComputeChainPc(CL2, CL3, La, Lb) → pcRow, pcCol
// (2) VisionAlgorithmService.ComputeProjectionDistance(
//       pcRow, pcCol, DatumOriginRow, DatumOriginCol, DatumAngleRad,
//       pixelResolution, MeasureAxis)  // "X" default
```

**csproj 등록:**
```xml
<Compile Include="Custom\Sequence\Inspection\Measurements\CompoundCenterCDistanceMeasurement.cs" />
```

---

### `Custom/Sequence/Inspection/Measurements/CompoundCenterBDistanceMeasurement.cs` (measurement, new — E10)

**Primary analog:** `CompoundCenterCDistanceMeasurement.cs` (동일 구조, DatumRef/MeasureAxis 기본값만 다름)

**Class declaration:**
```csharp
public class CompoundCenterBDistanceMeasurement : MeasurementBase, IDatumOriginConsumer //260519 hbk Phase 31 D-11
{
    public override string TypeName { get { return "CompoundCenterBDistance"; } }
```

**MeasureAxis 기본값** = `"Y"` (Datum B Y 방향, D-07/D-08 확정 E10).

**구조** — CompoundCenterCDistanceMeasurement 와 동일. ROI/Edge 필드 및 TryExecute 기하 체인 동일; `MeasureAxis = "Y"` 만 다름.

**csproj 등록:**
```xml
<Compile Include="Custom\Sequence\Inspection\Measurements\CompoundCenterBDistanceMeasurement.cs" />
```

---

### `Custom/Sequence/Inspection/Measurements/ArcEdgeDistanceMeasurement.cs` (measurement, new — G 시리즈)

**Primary analog:** `Measurements/EdgeToLineDistanceMeasurement.cs` — MSOP 확인으로 알고리즘 동일

**Class declaration:**
```csharp
public class ArcEdgeDistanceMeasurement : MeasurementBase, IDatumOriginConsumer //260519 hbk Phase 31 D-08
{
    public override string TypeName { get { return "ArcEdgeDistance"; } }
```

**ROI fields** — EdgeToLineDistanceMeasurement `Point_*` 패턴 그대로 (Category "Point|ROI").

**MeasureAxis 기본값** = `"X"` (Datum C X 방향, G 시리즈 공통).

**Edge/IDatumOriginConsumer fields** — EdgeToLineDistanceMeasurement.cs 완전 동일.

**TryExecute** — EdgeToLineDistanceMeasurement.TryExecute 와 동일하되 D-04 공용 헬퍼 사용:
```csharp
// TryFitLine("All") → midpoint(pRow, pCol)
// datumOriginInjected guard → ComputeProjectionDistance(pRow, pCol, ...)
// overlay 패턴: EdgeToLineDistanceMeasurement 와 동일 (FAI-Edge1 + FAI-DistLine)
```

**csproj 등록:**
```xml
<Compile Include="Custom\Sequence\Inspection\Measurements\ArcEdgeDistanceMeasurement.cs" />
```

---

### `Halcon/Algorithms/VisionAlgorithmService.cs` (modified — D-04 + D-10 + arc fit)

**Analog:** 자신의 기존 static 메서드 (`AngleLineLine` L567, `IntersectLines` L597, `TryFindCircle` L237)

**신규 추가 메서드 3개:**

**1) `ComputeProjectionDistance` (D-04 공용 헬퍼)** — EdgeToLineDistanceMeasurement.cs L126~196 에서 추출:
```csharp
//260519 hbk Phase 31 D-04 — projection_pl 거리 공용 헬퍼 (EdgeToLineDistanceMeasurement.TryExecute L126~196 추출)
public static double ComputeProjectionDistance(
    double pointRow, double pointCol,
    double datumOriginRow, double datumOriginCol,
    double datumAngleRad,
    double pixelResolution,
    string measureAxis)
{
    // EdgeToLineDistanceMeasurement.TryExecute L138~196 datumOriginInjected 분기 그대로 이식
    // sinT/cosT → cosT≥0 정규화 → axisR1/C1/R2/C2 → ProjectionPl → signedPx → return * pixelResolution
    // try { ProjectionPl... } catch { return 0.0; }
}
```
삽입 위치: `AffineTransformPoint` (L629) 바로 앞.

**2) `TryFitArc` (3점 호 피팅)** — `TryFindCircle` (L237) 패턴 변형:
```csharp
//260519 hbk Phase 31 D-01 — 3점 arc 피팅 (GenContourPolygonXld → FitCircleContourXld)
public bool TryFitArc(
    double p1Row, double p1Col,
    double p2Row, double p2Col,
    double p3Row, double p3Col,
    out double foundRow, out double foundCol, out double foundRadius,
    out string error)
{
    // GenContourPolygonXld(3점) → FitCircleContourXld("atukey", ...) → foundRow/Col/Radius
    // try { ... } catch { error = ex.Message; return false; } finally { contour?.Dispose(); }
}
```

**3) `TryIntersectCircleLine` (D-10 원-직선 교점)** — RESEARCH.md Pattern 5 전체 코드:
```csharp
//260519 hbk Phase 31 D-10 — 원-직선 교점 (2해 → ROI 내부 해 선택)
public static bool TryIntersectCircleLine(
    double cRow, double cCol, double radius,
    double lRow1, double lCol1, double lRow2, double lCol2,
    double roiRow, double roiCol,
    out double intRow, out double intCol)
{
    // RESEARCH.md Pattern 5 skeleton 전체 (2차 방정식 → 2해 → ROI 중심 근접 해 선택)
    // try { ... } catch { return false; }
}
```

삽입 위치: `IntersectLines` (L597) 뒤, `AffineTransformPoint` (L629) 앞.

---

### `Custom/Sequence/Inspection/MeasurementFactory.cs` (modified — D-11)

**Analog:** 자신의 기존 switch/array (lines 14-46)

**switch case 추가 패턴** (lines 16-29 기존 패턴 복사):
```csharp
case "CircleCenterDistance":   //260519 hbk Phase 31 D-01 E8
    return new CircleCenterDistanceMeasurement(owner);
case "EdgeToLineAngle":        //260519 hbk Phase 31 D-05
    return new EdgeToLineAngleMeasurement(owner);
case "ArcLineIntersectDistance": //260519 hbk Phase 31 D-01
    return new ArcLineIntersectDistanceMeasurement(owner);
case "CompoundAngle":          //260519 hbk Phase 31 D-11
    return new CompoundAngleMeasurement(owner);
case "CompoundCenterCDistance": //260519 hbk Phase 31 D-11
    return new CompoundCenterCDistanceMeasurement(owner);
case "CompoundCenterBDistance": //260519 hbk Phase 31 D-11
    return new CompoundCenterBDistanceMeasurement(owner);
case "ArcEdgeDistance":        //260519 hbk Phase 31 D-08
    return new ArcEdgeDistanceMeasurement(owner);
```

**GetTypeNames() 배열 추가 패턴** (lines 37-45 기존 pattern, 동일 7개 신규 항목 append).

---

### `Custom/Sequence/Inspection/Action_FAIMeasurement.cs` (modified — D-03)

**Analog:** 자신의 L161~185 (`meas as EdgeToLineDistanceMeasurement` 하드코딩 블록)

**변경 지점:** EStep.Measure 루프 내 L161~185 블록 전체 교체.

**삭제 대상 (마커 추가 후 제거):**
```csharp
// lines 161-185: var etld = meas as EdgeToLineDistanceMeasurement; if (etld != null) { ... }
// 삭제 라인에 //260519 hbk Phase 31 D-03 removed 마커
```

**대체 코드** (RESEARCH.md Pattern 1 전체):
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

---

### `Custom/Sequence/Inspection/Measurements/EdgeToLineDistanceMeasurement.cs` (modified — D-03)

**변경 내용:** 클래스 선언 1줄에 `IDatumOriginConsumer` 추가.

**현재** (line 19):
```csharp
public class EdgeToLineDistanceMeasurement : MeasurementBase,
    System.ComponentModel.ICustomTypeDescriptor
```

**변경 후:**
```csharp
public class EdgeToLineDistanceMeasurement : MeasurementBase,
    System.ComponentModel.ICustomTypeDescriptor,
    IDatumOriginConsumer //260519 hbk Phase 31 D-03 — 소급 구현 (Action_FAIMeasurement 하드코딩 제거 전제조건)
```

기존 3 transient 필드(`DatumOriginRow/Col/AngleRad`)가 이미 존재하므로 인터페이스 멤버 구현은 자동 충족.

---

### `UI/ContentItem/MainView.xaml.cs` (modified — CO-23.1-02 + CO-23.1-01)

**Analog:** 자신의 `FindSelectedEdgeToLineMeasurement` (lines 1377-1395) 및 `CommitRectRoi` (lines 1239-1297)

#### CO-23.1-02: Rect ROI 버튼 일반화

**`FindSelectedEdgeToLineMeasurement` → 일반화** (현재 lines 1377-1395 패턴):
```csharp
// 현재 패턴 (단일 타입 체크):
private EdgeToLineDistanceMeasurement FindSelectedEdgeToLineMeasurement() {
    var meas = mParentWindow.inspectionList.SelectedParam as EdgeToLineDistanceMeasurement;
    ...
    var etl = m as EdgeToLineDistanceMeasurement; if (etl != null) return etl;
}

// Phase 31 일반화 — FindSelectedRectMeasurement 신규 메서드:
// 타입 화이트리스트: EdgeToLineDistanceMeasurement, EdgeToLineAngleMeasurement,
//   ArcLineIntersectDistanceMeasurement, ArcEdgeDistanceMeasurement,
//   CompoundAngleMeasurement, CompoundCenterCDistanceMeasurement, CompoundCenterBDistanceMeasurement
// 반환 타입: MeasurementBase (Point_* 필드 보유 타입 전체 커버)
```

**`CommitRectRoi`의 `_editingMeasurement.Point_Row` 기록** (lines 1254-1258):
```csharp
// 현재: EdgeToLineDistanceMeasurement 직접 캐스트
_editingMeasurement.Point_Row = mCenterRow;
```
일반화 후: `_editingMeasurement` 필드 타입을 `MeasurementBase` 로 변경하고 C# 7.2 `as` + 명시적 프로퍼티 설정 유지. Point_* 필드는 인터페이스화 없이 각 타입에 직접 설정 (타입 화이트리스트 방식).

**Circle ROI 일반화** (lines 1362-1374 `FindSelectedCircleMeasurement`):
```csharp
// 현재: CircleDiameterMeasurement 만 검사
// Phase 31: CircleCenterDistanceMeasurement 도 추가
var circle = m as CircleDiameterMeasurement;
var circleCtr = m as CircleCenterDistanceMeasurement; //260519 hbk Phase 31
if (circle != null) return circle;
if (circleCtr != null) return circleCtr; //260519 hbk Phase 31
// 반환 타입 → MeasurementBase (Circle_* 필드 공유 타입)
```

#### CO-23.1-01: 듀얼 이미지 뷰어

**현재 상태:** `ShotConfig.SimulImagePath`(검사 이미지)와 `DatumConfig.TeachingImagePath`(티칭 이미지)가 분리되어 있으나 UI 에서 단일 뷰어만 사용.

**구현 방향:** MainView.xaml 에 별도 `Image` 또는 레이블 패널 추가 — Datum 티칭 모드에서 `TeachingImagePath` 경로를 썸네일/별도 HalconViewer 로 표시. 기존 HalconViewer 영역 유지. 상세 구현은 planner 결정.

---

## Shared Patterns

### 패턴 A: IDatumOriginConsumer 3 transient 필드 (모든 신규 측정 타입에 동일 적용)

**Source:** `EdgeToLineDistanceMeasurement.cs` lines 69-80
**Apply to:** CircleCenterDistance, EdgeToLineAngle, ArcLineIntersectDistance, CompoundAngle, CompoundCenterCDistance, CompoundCenterBDistance, ArcEdgeDistance

```csharp
[System.ComponentModel.Browsable(false)]
[PropertyTools.DataAnnotations.Browsable(false)]
[Newtonsoft.Json.JsonIgnore]
public double DatumOriginRow { get; set; }
[System.ComponentModel.Browsable(false)]
[PropertyTools.DataAnnotations.Browsable(false)]
[Newtonsoft.Json.JsonIgnore]
public double DatumOriginCol { get; set; }
[System.ComponentModel.Browsable(false)]
[PropertyTools.DataAnnotations.Browsable(false)]
[Newtonsoft.Json.JsonIgnore]
public double DatumAngleRad { get; set; }
```

---

### 패턴 B: TryExecute 시그니처 (모든 신규 측정 타입)

**Source:** `MeasurementBase.cs` lines 54-60, `EdgeToLineDistanceMeasurement.cs` lines 84-95

```csharp
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
    // ...
}
```

---

### 패턴 C: Halcon 에러 처리 (VisionAlgorithmService 신규 메서드)

**Source:** `VisionAlgorithmService.cs` lines 174-183 (TryFitLine), lines 306-317 (TryFindCircle)

```csharp
try
{
    // HOperatorSet.* 호출
    return true;
}
catch (Exception ex)
{
    error = ex.Message;
    return false;
}
finally
{
    if (contour != null) { try { contour.Dispose(); } catch { } }
}
```

순수 수학 정적 메서드(`TryIntersectCircleLine`, `ComputeProjectionDistance`)는:
```csharp
try { /* 계산 */ return true/value; }
catch { return false/0.0; }
```

---

### 패턴 D: ICustomTypeDescriptor (선택적 — Phase 23.1 패턴)

**Source:** `EdgeToLineDistanceMeasurement.cs` lines 266-300, `DynamicPropertyHelper.cs`
**Apply to:** 타입별 무관 속성 숨김이 필요한 경우. 신규 타입은 EdgeSelection 고정이 없으므로 D-09 에서 discretion 으로 결정. 적용 시 동일 패턴 복사.

```csharp
// 클래스 선언에 ICustomTypeDescriptor 추가
// BuildFilteredProperties + DynamicPropertyHelper.FilterProperties 호출 추가
// ICustomTypeDescriptor 나머지 메서드 boilerplate 12개 동일 복사 (lines 291-300)
```

---

### 패턴 E: MeasurementFactory 등록 쌍 (switch case + GetTypeNames 배열)

**Source:** `MeasurementFactory.cs` lines 28-30 + lines 44-45

각 신규 타입마다 반드시 2곳 추가 — switch case 1개 + GetTypeNames 배열 항목 1개. 둘 중 하나만 추가하면 Pitfall 6 발생.

---

### 패턴 F: csproj Compile ItemGroup 등록

**Source:** `DatumMeasurement.csproj` lines 219-225

신규 파일 9개 각각:
```xml
<Compile Include="Custom\Sequence\Inspection\Measurements\{ClassName}.cs" />
<Compile Include="Custom\Sequence\Inspection\IDatumOriginConsumer.cs" />
```
`EdgeToLineDistanceMeasurement.cs` (line 225) 직후에 그룹으로 삽입.

---

### 패턴 G: 주석 마커 규칙

**Source:** `feedback_comment_convention.md` (memory)

신규 라인: `//260519 hbk Phase 31`
기존 마커 위 stacking: 기존 `//260517 hbk` 라인에 수정 시 → 그 라인 끝에 `//260519 hbk Phase 31 D-0N` 추가.
삭제 라인: `//260519 hbk Phase 31 D-03 removed` 남기고 삭제.

---

### 패턴 H: ComboBox ItemsSource 래퍼 (PropertyGrid)

**Source:** `EdgeToLineDistanceMeasurement.cs` lines 48-54, `CircleDiameterMeasurement.cs` lines 33-38

`[ItemsSourceProperty(nameof(XxxList))]` 와 쌍을 이루는 `[Browsable(false)] public List<string> XxxList` 프로퍼티를 항상 함께 선언. ICustomTypeDescriptor 적용 시 sourceNames 화이트리스트에도 포함 필수.

---

## No Analog Found

없음 — 모든 신규 파일에 대해 역할/데이터 흐름 기준 적합한 기존 analog 발견됨.

---

## Metadata

**Analog search scope:**
- `WPF_Example/Custom/Sequence/Inspection/` (7 analog files read)
- `WPF_Example/Halcon/Algorithms/VisionAlgorithmService.cs` (1 analog file read, 3 non-overlapping ranges)
- `WPF_Example/UI/ContentItem/MainView.xaml.cs` (1 analog file, grep + targeted read)
- `WPF_Example/Device/IAxisController.cs` (interface convention)

**Files scanned:** 13
**Pattern extraction date:** 2026-05-19
