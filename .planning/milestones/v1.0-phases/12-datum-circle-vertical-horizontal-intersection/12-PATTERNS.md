# Phase 12: datum-circle-vertical-horizontal-intersection — Pattern Map

**Mapped:** 2026-04-23
**Files analyzed:** 8 (1 NEW, 7 MODIFY; 1 REUSE-no-mod)
**Analogs found:** 8 / 8

All Phase 12 touches have in-repo analogs. The enum file is new but sits next to `DatumConfig.cs` (namespace-local sibling); every modified file has a same-file analog block that the new code mirrors.

---

## File Classification

| File (action) | Role | Data Flow | Closest Analog | Match Quality | New/Modified |
|---|---|---|---|---|---|
| `WPF_Example/Custom/Sequence/Inspection/EDatumAlgorithm.cs` | data-model (enum) | serialization (ParamBase string round-trip) | `WPF_Example/Custom/Sequence/Inspection/EdgeOptionLists.cs` (single-type file, same folder, same namespace `ReringProject.Sequence`) | role-match | CREATE-NEW |
| `WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs` | data-model (ParamBase) | serialization (INI, ParamBase reflection) | same file, existing `[Category("Datum|...")]` blocks L16-94 | exact | MODIFY-EXTEND |
| `WPF_Example/Halcon/Algorithms/DatumFindingService.cs` | algorithm service | transform (HImage → RefOrigin/Angle + side-effect on config) | same file, existing `TryTeachDatum` (L122-218) and `TryFindLine` (L224-294) | exact | MODIFY-EXTEND |
| `WPF_Example/Halcon/Algorithms/VisionAlgorithmService.cs` | algorithm service | transform (HImage → circle fit) | `TryFindCircle` (L120-200) already exists; Phase 12 does not touch it | exact | REUSE-NO-MOD |
| `WPF_Example/UI/ContentItem/MainView.xaml` | view (XAML toolbar) | request-response (button click) | same file, `btn_circleRoi` (L82-88), `btn_rectRoi` (L70-74), `RoiToggleButtonStyle` (L10-50) | exact | MODIFY-EXTEND |
| `WPF_Example/UI/ContentItem/MainView.xaml.cs` | view code-behind | event-driven (canvas mode state machine) | same file, `CircleRoiButton_Click` (L699-730), `HalconViewer_CircleDrawingCompleted` (L733-735), `CommitCircleRoi` (L773-797), `ExitCanvasMode` (L606-633) | exact | MODIFY-EXTEND |
| `WPF_Example/UI/ControlItem/InspectionListView.xaml.cs` | view code-behind | event-driven (selection → UI button state) | same file, Datum node branch at `InspectionList_SelectionChanged` L280-294 (Grab/LoadImage enable) + btn_circleRoi gating L333-345 | exact | MODIFY-EXTEND |
| `WPF_Example/Halcon/Display/HalconDisplayService.cs` | rendering service | transform (models → HWindow draw ops) | same file, `RenderDatumOverlay` (L350-429), Circle ROI render branch (L50-65), `RenderCircleDraft` (L195-211) | exact | MODIFY-EXTEND |

---

## Pattern Assignments

### 1. `EDatumAlgorithm.cs` (NEW) — single-enum file, `ReringProject.Sequence` namespace (D-09)

**Role:** data-model (enum). **Data Flow:** ParamBase auto-serializes `EDatumAlgorithm AlgorithmType` by its `ToString()` value (see ParamBase reflection gotcha in Shared Patterns §6) — round-trip safe because INI stores `"TwoLineIntersect"` / `"CircleTwoHorizontal"` / `"VerticalTwoHorizontal"` as strings and `Enum.TryParse`-style load restores the enum. Default value `TwoLineIntersect` satisfies INI hosst-wards-compat fallback (D-09).

**Analog:** `WPF_Example/Custom/Sequence/Inspection/EdgeOptionLists.cs` (same folder, same namespace, single-purpose static/type file).

**Imports pattern** (EdgeOptionLists.cs L1-4):
```csharp
//260423 hbk WR-RT-02 PropertyGrid ComboBox 옵션 단일 소스 — EdgeDirection / EdgePolarity
using System.Collections.Generic;

namespace ReringProject.Sequence
```

**Phase 12 file body** (apply `//260423 hbk` comment convention on every line per user memory `feedback_comment_convention`):
```csharp
//260423 hbk Phase 12: Datum 알고리즘 식별자 (D-09)
// Phase 13에서 WPF_Example/Halcon/Algorithms/Datum/EDatumAlgorithm.cs 로 이동 예정.
// ParamBase 직렬화는 Enum 타입 미지원이지만 Reflection이 string 폴백으로 작동해 INI에 enum.ToString() 으로 저장됨.
namespace ReringProject.Sequence
{
    public enum EDatumAlgorithm
    {
        TwoLineIntersect,           //260423 hbk 기존 Phase 4 방식 (Line1∩Line2) — default
        CircleTwoHorizontal,        //260423 hbk Circle 센터 수직 가상선 ∩ 수평 2-ROI concat
        VerticalTwoHorizontal,      //260423 hbk 수직 ROI ∩ 수평 2-ROI concat
    }
}
```

**Phase 12 application note:** Single file, no dependencies, no imports beyond namespace declaration. Matches EdgeOptionLists.cs shape (single type, one namespace block, comment-heavy header). Phase 13 will physically move this file into a new subfolder — keep it minimal so the move is a pure cut/paste.

**CRITICAL ParamBase serialization check:** `ParamBase.Save/Load` switch (ParamBase.cs L330-364) on `type.Name` cases `Int32/Double/String/Boolean/Rect/Line/Circle/PropertyItem[]/ModelFinderViewModel`. Enum types hit `default: break` and are SKIPPED. Phase 12 planner MUST verify current ParamBase reflection behavior: if enum round-trip truly does not work, fall back to `string AlgorithmType` + centralized `EDatumAlgorithm` parse helper in `DatumConfig` (same pattern as `string EdgePolarity`/`string ImageSourceMode` already in `DatumConfig`). **Recommended by CONTEXT D-09: try enum first; if serialization test fails, demote to `string`.**

---

### 2. `DatumConfig.cs` — AlgorithmType + Circle ROI + Horizontal A/B + volatile Circle detect fields (D-09/D-10/D-11/D-12/D-13)

**Role:** data-model (ParamBase subclass). **Data Flow:** INI round-trip via ParamBase reflection.

**Analog:** same file. Existing `[Category("Datum|ImageSource")]` block L20-30, `[Category("Datum|Line1 ROI")]` block L32-38, `[Category("Datum|Line2 ROI")]` block L40-46, volatile `[Browsable(false)]` block L69-94.

**Existing serializable ROI field pattern (L32-38):**
```csharp
//260409 hbk Phase 4: Line1 ROI (기준 X축 방향 에지 라인)
[Category("Datum|Line1 ROI")]
public double Line1_Row { get; set; } = 0;
public double Line1_Col { get; set; } = 0;
public double Line1_Phi { get; set; } = 0;
public double Line1_Length1 { get; set; } = 0;
public double Line1_Length2 { get; set; } = 0;
```

**Existing volatile (runtime-only) field pattern (L77-85):**
```csharp
//260423 hbk Phase 11 D-11 — 검출 라인 오버레이용 휘발성 필드 (TryTeachDatum 성공 시 DatumFindingService가 기록)
[PropertyTools.DataAnnotations.Browsable(false)]
public double Line1Detected_RBegin { get; set; }
[PropertyTools.DataAnnotations.Browsable(false)]
public double Line1Detected_CBegin { get; set; }
...
```

**Phase 12 additions — exact placement order (after line 30 `SourceShotName`, before line 32 `Line1 ROI` block):**

```csharp
//260423 hbk Phase 12 D-09 — Datum 알고리즘 선택자 (PropertyGrid 자동 enum 드롭다운 렌더)
//  TwoLineIntersect        = 기존 Phase 4 (Line1∩Line2) — default, INI 미존재 필드 폴백
//  CircleTwoHorizontal     = Circle ROI + 수평 2-ROI concat 교점
//  VerticalTwoHorizontal   = 수직 ROI(Line1 재사용) + 수평 2-ROI concat 교점
[Category("Datum|Algorithm")]
public EDatumAlgorithm AlgorithmType { get; set; } = EDatumAlgorithm.TwoLineIntersect;
```

**Line1_* XML summary update (D-12) — modify the existing L32-38 block to add the summary, leaving the fields unchanged:**
```csharp
//260409 hbk Phase 4: Line1 ROI
//260423 hbk Phase 12 D-12 — 알고리즘별 Line1 시맨틱스:
//   TwoLineIntersect:         1st 라인 ROI (기준 X축 방향 에지 라인)
//   VerticalTwoHorizontal:    수직 ROI (수직 에지 라인)
//   CircleTwoHorizontal:      미사용 (기본값 0 유지)
/// <summary>
/// Line1 ROI — 알고리즘별 의미:
///   TwoLineIntersect: 1st 라인 ROI (기준 X축 방향)
///   VerticalTwoHorizontal: 수직 ROI (수직 에지 라인)
///   CircleTwoHorizontal: 미사용 (기본값 0 유지)
/// </summary>
[Category("Datum|Line1 ROI")]
public double Line1_Row { get; set; } = 0;
// ... (나머지 Line1 필드 변경 없음)
```

**Circle ROI + volatile detection fields (D-10) — insert AFTER Line2 ROI block (after L46), BEFORE RefOrigin block (L48):**

```csharp
//260423 hbk Phase 12 D-10 — Circle ROI (CircleTwoHorizontal 전용 검색 영역)
//  CircleROI_Radius > 0 이 ROI 설정 완료 판정 기준.
[Category("Datum|Circle ROI")]
public double CircleROI_Row    { get; set; } = 0;   // 검색 영역 중심 Y (row)
public double CircleROI_Col    { get; set; } = 0;   // 검색 영역 중심 X (col)
public double CircleROI_Radius { get; set; } = 0;   // 검색 영역 반지름 (pixel)

//260423 hbk Phase 12 D-11 — 수평 A ROI (CircleTwoHorizontal + VerticalTwoHorizontal 공용)
//  A/B 순서 의존성 없음 — concat + FitLineContourXld 이므로 교환 대칭.
//  Length1 > 0 && Length2 > 0 이 ROI 설정 완료 판정 기준.
[Category("Datum|Horizontal A ROI")]
public double Horizontal_A_Row     { get; set; } = 0;
public double Horizontal_A_Col     { get; set; } = 0;
public double Horizontal_A_Phi     { get; set; } = 0;
public double Horizontal_A_Length1 { get; set; } = 0;
public double Horizontal_A_Length2 { get; set; } = 0;

//260423 hbk Phase 12 D-11 — 수평 B ROI
[Category("Datum|Horizontal B ROI")]
public double Horizontal_B_Row     { get; set; } = 0;
public double Horizontal_B_Col     { get; set; } = 0;
public double Horizontal_B_Phi     { get; set; } = 0;
public double Horizontal_B_Length1 { get; set; } = 0;
public double Horizontal_B_Length2 { get; set; } = 0;
```

**Circle detection volatile fields (D-10 휘발성) — insert inside existing volatile block (around L77):**
```csharp
//260423 hbk Phase 12 D-10 — Circle 검출 결과 휘발성 (TryTeachDatum 성공 시 DatumFindingService가 기록, INI 저장 안 함이 이상적이지만
//  ParamBase reflection이 Browsable 무시하고 public double 을 직렬화하므로 INI에 0으로 저장됨 — Phase 11 Line*Detected_* 와 동일 수용)
[PropertyTools.DataAnnotations.Browsable(false)]
public double CircleCenter_Row { get; set; }
[PropertyTools.DataAnnotations.Browsable(false)]
public double CircleCenter_Col { get; set; }
[PropertyTools.DataAnnotations.Browsable(false)]
public double CircleDetected_Radius { get; set; }
```

**Phase 12 application note:** All new persistent fields use default `= 0`, so existing Phase 4/11 INI files (field absent) load as `CircleROI_Radius=0`, `Horizontal_A_Length1=0`, `Horizontal_B_Length1=0`, `AlgorithmType=TwoLineIntersect` → `TwoLineIntersect` branch runs → existing behavior retained (SPEC Req 1 Acceptance). Line1_*/Line2_*/RefOrigin*/RefAngle*/IsConfigured fields unchanged — SPEC Out-of-scope requires no removal of the 2-Line model.

---

### 3. `DatumFindingService.cs` — TryTeachDatum 3-way switch + private per-algo methods + TryExtractEdgePoints helper (D-05/D-06/D-07/D-08/D-14/D-15/D-16)

**Role:** algorithm service. **Data Flow:** `HImage in → bool returned + side effects on `DatumConfig` (RefOrigin/RefAngle/IsConfigured/Line*Detected_*/CircleCenter_*/LastTeachSucceeded)`.

**Analog:** same file. Existing `TryTeachDatum` (L122-218) handles `TwoLineIntersect` logic; existing `TryFindLine` (L224-294) handles single-ROI edge extraction + line fit; existing `IntersectionLl` + parallel/collinear guard (L167-187) works for both new algorithms verbatim.

**CRITICAL: Preserve TwoLineIntersect regression 0.** The existing TryTeachDatum body becomes a new private `TryTeachTwoLineIntersect` — move the body unchanged, do NOT rename the out-param names, do NOT change the `"Line1: "` / `"Line2: "` / `"Lines are collinear..."` / `"Lines are parallel..."` error strings (SPEC AC — error strings are literal).

---

**3.1 — Extract private helper `TryExtractEdgePoints` from existing TryFindLine (D-06)**

**Current TryFindLine body (L224-294)** does (a) `GenMeasureRectangle2 → MeasurePos` → get `rowEdge/colEdge` HTuples, (b) `GenContourPolygonXld → FitLineContourXld` → return `(rBegin, cBegin, rEnd, cEnd)`. For CircleTwoHorizontal and VerticalTwoHorizontal we need (a) only — the edge points to be concatenated, NOT a per-ROI fitted line.

**Extract the measurement-handle portion (L242-260 of TryFindLine) into a new private:**

```csharp
//260423 hbk Phase 12 D-06 — 단일 ROI에서 에지점만 추출 (FitLine 수행 전 단계). 수평 2-ROI concat 피팅용.
// 기존 TryFindLine 도 이 헬퍼를 호출하도록 리팩터하면 코드 중복 제거.
private bool TryExtractEdgePoints(
    HImage image, HTuple imageWidth, HTuple imageHeight,
    double roiRow, double roiCol, double roiPhi, double roiLength1, double roiLength2,
    double sigma, int threshold, string polarity,
    out HTuple rowEdge, out HTuple colEdge,
    out string error)
{
    rowEdge = new HTuple();
    colEdge = new HTuple();
    error = null;

    HTuple measureHandle = null;
    try
    {
        HOperatorSet.GenMeasureRectangle2(
            roiRow, roiCol, roiPhi, roiLength1, roiLength2,
            imageWidth, imageHeight, "nearest_neighbor",
            out measureHandle);

        HTuple amp, dist;
        HOperatorSet.MeasurePos(
            image, measureHandle, sigma, threshold, polarity, "all",
            out rowEdge, out colEdge, out amp, out dist);

        return true;
    }
    catch (Exception ex)
    {
        error = ex.Message;
        return false;
    }
    finally
    {
        if (measureHandle != null) { try { HOperatorSet.CloseMeasure(measureHandle); } catch { } }
    }
}
```

Existing `TryFindLine` may remain unchanged (so TwoLineIntersect has zero regression risk) or may be refactored to internally call `TryExtractEdgePoints` then do the single-ROI fit. Planner's choice; CONTEXT D-06 marks the refactor as "선택(optional)".

---

**3.2 — Add `MIN_HORIZONTAL_EDGES` constant (D-15)**

Insert at top of class body, after `public class DatumFindingService { ...`:
```csharp
//260423 hbk Phase 12 D-15 — 수평 2-ROI concat 최소 에지점 (신뢰 라인 피팅 기준)
private const int MIN_HORIZONTAL_EDGES = 10;
```

---

**3.3 — Switch in `TryTeachDatum` (D-04 dispatch)**

Replace the existing TryTeachDatum body with a dispatch:
```csharp
public bool TryTeachDatum(HImage image, DatumConfig config, out string error) //260409 hbk Phase 4 / 260423 hbk Phase 12 D-04
{
    error = null;
    if (image == null || config == null) { error = "image or config is null"; return false; }

    //260423 hbk Phase 12 D-04 — AlgorithmType 분기
    switch (config.AlgorithmType)
    {
        case EDatumAlgorithm.CircleTwoHorizontal:
            return TryTeachCircleTwoHorizontal(image, config, out error);
        case EDatumAlgorithm.VerticalTwoHorizontal:
            return TryTeachVerticalTwoHorizontal(image, config, out error);
        case EDatumAlgorithm.TwoLineIntersect:
        default:
            return TryTeachTwoLineIntersect(image, config, out error); //260423 hbk 기존 L122-218 로직 private 이동
    }
}

//260423 hbk Phase 12 — 기존 Phase 4 로직 private 이동 (회귀 0 — 코드는 원본과 동일, 단 메서드명과 가시성만 변경)
private bool TryTeachTwoLineIntersect(HImage image, DatumConfig config, out string error)
{
    // (원본 TryTeachDatum 본문 L132-217 을 그대로 옮긴다. 에러 문자열 / LastTeachSucceeded / Line*Detected_* 쓰기 모두 원본 동일)
    ...
}
```

---

**3.4 — `TryTeachCircleTwoHorizontal` 구현 (D-05/D-06/D-08/D-13/D-14)**

Core recipe (from CONTEXT code_context):
1. `VisionAlgorithmService.TryFindCircle` (datumTransform=null — teaching 시점에는 identity) → `(centerRow, centerCol, radius)` → `config.CircleCenter_*` + `config.CircleDetected_Radius` 저장. 실패 시 `error = "Circle fit failed: " + circleError`.
2. `TryExtractEdgePoints` × 2회 (Horizontal_A, Horizontal_B) → 각각 `HTuple rowEdgeA/colEdgeA/rowEdgeB/colEdgeB`. 각 호출 실패 시 `error = "Horizontal line fit failed: " + err`.
3. `int totalEdges = rowEdgeA.TupleLength() + rowEdgeB.TupleLength(); if (totalEdges < MIN_HORIZONTAL_EDGES) error = "Horizontal line fit failed: insufficient edges (" + totalEdges + ")"; return false;`.
4. `GenContourPolygonXld × 2 → ConcatObj → FitLineContourXld("tukey", -1, 0, 5, 2, ...)` → `(hrB, hcB, hrE, hcE)` 수평 결합선.
5. `IntersectionLl(centerRow - 1.0, centerCol, centerRow + 1.0, centerCol, hrB, hcB, hrE, hcE, out curRow, out curCol, out isOverlapping)` — 수직 가상선 2점 표현은 `centerRow ± 1.0, centerCol` (D-08).
6. 평행/중첩 감지: `if (isOverlapping.I == 1) error = "Intersection undefined: lines are collinear";` / `if (IsInfinity(curRow.D) || IsInfinity(curCol.D) || IsNaN(curRow.D) || IsNaN(curCol.D)) error = "Intersection undefined: lines are parallel";` (D-14 단, 수평선이 거의 수평이라면 `IntersectionLl` 자체가 Infinity/NaN 으로 발산).
7. 저장: `config.RefOriginRow = curRow.D; config.RefOriginCol = curCol.D; config.RefAngleRad = Math.Atan2(hrE.D - hrB.D, hcE.D - hcB.D); config.IsConfigured = true; config.LastTeachSucceeded = true;` + `Line1Detected_* = (수직 가상선 2점: centerRow±crossSize, centerCol)` + `Line2Detected_* = (hrB, hcB, hrE, hcE)` (D-13 오버레이 재사용).
8. `// TODO: Phase 13 — 방향 정합성 검사 (운용 정책 확립 후 구현)` 주석만 삽입 (D-17 이월, Req 5d는 Phase 12 미구현).

**TryFindCircle 호출 시그니처 (VisionAlgorithmService.cs L120-126 기준, datumTransform=null 필요):**
```csharp
public bool TryFindCircle(
    HImage image,
    double centerRow, double centerCol, double radius,
    HTuple datumTransform,
    double sigma, int threshold, string polarity,
    out double foundRow, out double foundCol, out double foundRadius,
    out string error)
```

**Phase 12 호출 예시 (DatumFindingService 내부):**
```csharp
var visionSvc = new VisionAlgorithmService();
double centerRow, centerCol, radius;
string circleError;
if (!visionSvc.TryFindCircle(image,
        config.CircleROI_Row, config.CircleROI_Col, config.CircleROI_Radius,
        null,  // teaching phase — identity transform
        config.Sigma, config.EdgeThreshold, config.EdgePolarity,
        out centerRow, out centerCol, out radius, out circleError))
{
    config.LastTeachSucceeded = false;
    error = "Circle fit failed: " + circleError; // D-14 SPEC AC literal
    return false;
}
config.CircleCenter_Row       = centerRow;
config.CircleCenter_Col       = centerCol;
config.CircleDetected_Radius  = radius;
```

**ConcatObj + FitLineContourXld 패턴 (SPEC Req 3 target 경로):**
```csharp
HObject contourA = null, contourB = null, concat = null;
try
{
    HOperatorSet.GenContourPolygonXld(out contourA, rowEdgeA, colEdgeA);
    HOperatorSet.GenContourPolygonXld(out contourB, rowEdgeB, colEdgeB);
    HOperatorSet.ConcatObj(contourA, contourB, out concat);

    HTuple hrB, hcB, hrE, hcE, nr, nc, df;
    HOperatorSet.FitLineContourXld(concat, "tukey", -1, 0, 5, 2,
        out hrB, out hcB, out hrE, out hcE, out nr, out nc, out df);
    // hrB, hcB, hrE, hcE = 수평 결합 연장선
    ...
}
catch (Exception ex)
{
    error = "Horizontal line fit failed: " + ex.Message;
    return false;
}
finally
{
    if (contourA != null) { try { contourA.Dispose(); } catch { } }
    if (contourB != null) { try { contourB.Dispose(); } catch { } }
    if (concat   != null) { try { concat.Dispose();   } catch { } }
}
```

---

**3.5 — `TryTeachVerticalTwoHorizontal` 구현 (D-07/D-08/D-13/D-14)**

Core recipe:
1. `TryExtractEdgePoints`(수직 ROI = `config.Line1_*` 재사용 — D-07/D-12) → `rowEdgeV/colEdgeV`. 에지점 < 2 이면 `error = "Vertical line fit failed: insufficient edges"` — **SPEC AC literal "Vertical line fit failed"**.
2. `GenContourPolygonXld → FitLineContourXld` 로 수직선 `(vrB, vcB, vrE, vcE)` 단일 ROI 피팅. (또는 기존 `TryFindLine` 호출로 동일 결과.)
3. 수평 2-ROI concat (§3.4의 단계 2-4 완전 재사용).
4. `IntersectionLl(vrB, vcB, vrE, vcE, hrB, hcB, hrE, hcE, out curRow, out curCol, out isOverlapping)` — **CircleTwoHorizontal의 수직 가상선 치환부만 다름**.
5. 평행/중첩 감지 = §3.4 단계 6 동일.
6. 저장 = §3.4 단계 7 동일 (Line1Detected_* = 수직선, Line2Detected_* = 수평 결합선). CircleCenter_*/CircleDetected_Radius 은 기본값 유지 (건드리지 않음).

**존재하는 `TryFindLine` 호출 예시 (DatumFindingService.cs L140-150 기준):**
```csharp
double vrB, vcB, vrE, vcE;
string lineError;
if (!TryFindLine(
        image, imageWidth, imageHeight,
        config.Line1_Row, config.Line1_Col, config.Line1_Phi,
        config.Line1_Length1, config.Line1_Length2,
        config.Sigma, config.EdgeThreshold, config.EdgePolarity,
        out vrB, out vcB, out vrE, out vcE,
        out lineError))
{
    config.LastTeachSucceeded = false;
    error = "Vertical line fit failed: " + lineError; //260423 hbk Phase 12 D-14 SPEC AC literal (Req 5e)
    return false;
}
```

**Angle 저장 (§3.4 단계 7 변형) — RefAngleRad = 수평 결합선 기울기** (SPEC Req 4 target — Halcon `LineOrientation` 대체로 `Math.Atan2(dRow, dCol)` 사용, 기존 TwoLineIntersect 와 동일 방식):
```csharp
config.RefAngleRad = Math.Atan2(hrE.D - hrB.D, hcE.D - hcB.D); //260423 hbk Phase 12 — 수평선 방향각 (SPEC Req 4)
```

---

**3.6 — Error string literal 준수 (D-14)**

SPEC Acceptance Criteria에 명시된 리터럴 error 문구를 그대로 사용:

| Failure | Error literal |
|---|---|
| Circle 피팅 실패 (Req 5c) | `"Circle fit failed: " + inner` |
| 수직 라인 피팅 실패 (Req 5e) | `"Vertical line fit failed: " + inner` |
| 수평 결합 에지점 부족 (Req 5a) | `"Horizontal line fit failed: insufficient edges (" + totalEdges + ")"` |
| 수평 결합 FitLineContourXld 예외 (Req 5a) | `"Horizontal line fit failed: " + ex.Message` |
| 교점 평행 (Req 5b) | `"Intersection undefined: lines are parallel"` |
| 교점 중첩/같음 (Req 5b) | `"Intersection undefined: lines are collinear"` |
| 기존 TwoLineIntersect (회귀 0) | `"Line1: ..."`, `"Line2: ..."`, `"Lines are collinear ..."`, `"Lines are parallel ..."` — 건드리지 않음 |

**알고리즘 prefix 금지** (D-14) — `"[CircleTwoHorizontal] Circle fit failed..."` 식으로 prefix 추가하지 않는다.

---

### 4. `VisionAlgorithmService.cs` — REUSE, NO MODIFICATION (D-05)

**Role:** algorithm service. **Phase 12 touches this file 0 lines.** `TryFindCircle` (L120-200) signature is already compatible with Phase 12 use (datumTransform nullable, returns foundRow/foundCol/foundRadius + error string). DatumFindingService.TryTeachCircleTwoHorizontal instantiates a new `VisionAlgorithmService` and calls `TryFindCircle` with `datumTransform=null` (teaching-phase identity — exactly like CircleDiameterMeasurement.cs L49-53 except with null transform).

---

### 5. `MainView.xaml` — btn_teachDatum ToggleButton insertion

**Role:** view XAML. **Data Flow:** request-response (button click → code-behind handler).

**Analog:** same file. Existing `btn_circleRoi` at L82-88 is the closest-quality analog (also a ToggleButton, also `IsEnabled="False"`, also uses `RoiToggleButtonStyle`). `btn_rectRoi` L70-74 is secondary analog (same style, but `IsEnabled=True` default — undesired for Datum-only Teach Datum).

**Existing btn_circleRoi pattern (L82-88) — copy verbatim for btn_teachDatum:**
```xml
<!--260423 hbk Phase 11 D-15 Circle ROI 드로잉 토글-->
<ToggleButton x:Name="btn_circleRoi" Content="Circle ROI"
    Click="CircleRoiButton_Click"
    Style="{StaticResource RoiToggleButtonStyle}"
    IsEnabled="False"
    MinWidth="80" Height="28" Padding="12,4"
    FontSize="13" FontWeight="SemiBold" Margin="0,0,4,0"/>
```

**Phase 12 btn_teachDatum — mirror above with width=100 (11-UI-SPEC.md §Layout Contract says "Teach Datum" 11자로 cramped at 80, 100 still within 4-unit scale):**
```xml
<!--260423 hbk Phase 12 D-01 Datum 티칭 토글 (3-way: TwoLineIntersect / CircleTwoHorizontal / VerticalTwoHorizontal)-->
<ToggleButton x:Name="btn_teachDatum" Content="Teach Datum"
    Click="TeachDatumButton_Click"
    Style="{StaticResource RoiToggleButtonStyle}"
    IsEnabled="False"
    MinWidth="100" Height="28" Padding="12,4"
    FontSize="13" FontWeight="SemiBold" Margin="0,0,4,0"/>
```

**Placement:** After `btn_circleRoi` (L88), BEFORE the existing `Separator` (L90-91) + Calibrate group. Optionally insert a second `<Separator Margin="4,4"/>` between btn_teachDatum and the Separator + Calibrate (UI-SPEC §Layout Contract grouping: ROI-shape toggles / Teach Datum / Calibrate as 3 groups). UI-SPEC Anti-Pattern warns against introducing a new accent hue — the existing `RoiToggleButtonStyle` accent `#2563EB` applies correctly to btn_teachDatum `IsChecked=True`.

---

### 6. `MainView.xaml.cs` — ECanvasMode.TeachDatum + EDatumTeachStep + 3-way state machine (D-01/D-02/D-03/D-04)

**Role:** view code-behind. **Data Flow:** event-driven state machine (canvas mode × algorithm × step).

**Analogs (same file):**
- `ECanvasMode` enum (L42)
- `CircleRoiButton_Click` (L699-730) — closest analog for `TeachDatumButton_Click` (ToggleButton entry + selection validation + hint + StartDrawing + unsubscribe-on-Commit pattern).
- `HalconViewer_CircleDrawingCompleted` (L733-735) — analog for per-step drawing-complete callback.
- `CommitCircleRoi` (L773-797) — analog for writing ROI values into a target param + refreshing display.
- `ExitCanvasMode` (L606-633) — must be extended to include Datum-mode cleanup.
- `MainView_PreviewKeyDown` (L598-603) — Escape handler already routes through ExitCanvasMode once TeachDatum is listed.

---

**6.1 — Extend ECanvasMode enum (L42):**
```csharp
//260408 hbk Drawing mode state (ROI 편집 + 캘리브레이션)
//260423 hbk Phase 11 D-15 — CircleRoi 모드 추가
//260423 hbk Phase 12 D-01 — TeachDatum 모드 추가 (3-way algorithm switch: TwoLineIntersect / CircleTwoHorizontal / VerticalTwoHorizontal)
private enum ECanvasMode { None, RectRoi, PolygonRoi, CircleRoi, TeachDatum, Calibration }
```

---

**6.2 — Add private EDatumTeachStep enum + state fields (D-03, near existing L41-51 state block):**
```csharp
//260423 hbk Phase 12 D-03 — Datum 티칭 단계 (알고리즘별 switch 로 전이 결정)
// Phase 13 에서 DatumAlgorithmBase.GetROISteps() 가변 배열로 재설계 예정 — switch 는 MainView 내 private 유지.
private enum EDatumTeachStep { Line1, Line2, Circle, Vertical, HorizontalA, HorizontalB, Done }
private EDatumTeachStep _datumTeachStep = EDatumTeachStep.Line1;
private DatumConfig _editingDatum; //260423 hbk Phase 12 — 현재 티칭 중 Datum
```

---

**6.3 — `TeachDatumButton_Click` — copy CircleRoiButton_Click shape (L699-730):**
```csharp
//260423 hbk Phase 12 D-01/D-03/D-04 — Datum 티칭 토글 진입
private void TeachDatumButton_Click(object sender, RoutedEventArgs e) {
    if (btn_teachDatum.IsChecked == true) {
        ExitCanvasMode();
        _canvasMode = ECanvasMode.TeachDatum;
        btn_teachDatum.IsChecked = true;

        //260423 hbk Phase 12 — Datum 노드 선택 시에만 진입 가능 (InspectionListView.SelectedParam 접근)
        var datum = mParentWindow.inspectionListView.SelectedParam as DatumConfig;
        if (datum == null) {
            CustomMessageBox.Show("Teach Datum", "Datum 노드를 먼저 선택하세요.");
            ExitCanvasMode();
            return;
        }
        _editingDatum = datum;

        //260423 hbk Phase 12 D-03 — 알고리즘별 첫 단계 결정
        _datumTeachStep = GetFirstStep(datum.AlgorithmType);
        StartDatumTeachStep(_datumTeachStep);
    }
    else {
        //260423 hbk 수동 해제 = 취소
        ExitCanvasMode();
    }
}

//260423 hbk Phase 12 D-03 — 알고리즘별 첫 ROI 단계
private EDatumTeachStep GetFirstStep(EDatumAlgorithm algorithm) {
    switch (algorithm) {
        case EDatumAlgorithm.CircleTwoHorizontal:   return EDatumTeachStep.Circle;
        case EDatumAlgorithm.VerticalTwoHorizontal: return EDatumTeachStep.Vertical;
        case EDatumAlgorithm.TwoLineIntersect:
        default:                                     return EDatumTeachStep.Line1;
    }
}

//260423 hbk Phase 12 D-03 — 현재 step 다음 step 결정 (알고리즘 × 현재 step 곱 switch)
private EDatumTeachStep GetNextStep(EDatumAlgorithm algorithm, EDatumTeachStep current) {
    switch (algorithm) {
        case EDatumAlgorithm.TwoLineIntersect:
            if (current == EDatumTeachStep.Line1) return EDatumTeachStep.Line2;
            return EDatumTeachStep.Done;
        case EDatumAlgorithm.CircleTwoHorizontal:
            if (current == EDatumTeachStep.Circle)      return EDatumTeachStep.HorizontalA;
            if (current == EDatumTeachStep.HorizontalA) return EDatumTeachStep.HorizontalB;
            return EDatumTeachStep.Done;
        case EDatumAlgorithm.VerticalTwoHorizontal:
            if (current == EDatumTeachStep.Vertical)    return EDatumTeachStep.HorizontalA;
            if (current == EDatumTeachStep.HorizontalA) return EDatumTeachStep.HorizontalB;
            return EDatumTeachStep.Done;
        default: return EDatumTeachStep.Done;
    }
}

//260423 hbk Phase 12 — step 시작 (드로잉 이벤트 구독 + label_drawHint + Start*Drawing)
private void StartDatumTeachStep(EDatumTeachStep step) {
    // Unsubscribe any previous event to avoid double-fire
    halconViewer.RectDrawingCompleted   -= HalconViewer_DatumRectCompleted;
    halconViewer.CircleDrawingCompleted -= HalconViewer_DatumCircleCompleted;

    switch (step) {
        case EDatumTeachStep.Line1:
        case EDatumTeachStep.Line2:
        case EDatumTeachStep.Vertical:
        case EDatumTeachStep.HorizontalA:
        case EDatumTeachStep.HorizontalB:
            label_drawHint.Content = DatumStepHint(step);  // Claude's Discretion — "Step X/Y: ... ROI 를 드래그하세요"
            label_drawHint.Visibility = Visibility.Visible;
            halconViewer.RectDrawingCompleted += HalconViewer_DatumRectCompleted;
            halconViewer.StartRectangleDrawing();
            break;
        case EDatumTeachStep.Circle:
            label_drawHint.Content = "Circle 검색 영역 중심을 클릭 후 드래그하여 반지름을 지정하세요";
            label_drawHint.Visibility = Visibility.Visible;
            halconViewer.CircleDrawingCompleted += HalconViewer_DatumCircleCompleted;
            halconViewer.StartCircleDrawing();
            break;
        case EDatumTeachStep.Done:
            InvokeTryTeachDatum();  // §6.4
            break;
    }
}
```

---

**6.4 — Drawing-complete handlers — mirror CircleRoiButton_Click + CommitCircleRoi:**
```csharp
//260423 hbk Phase 12 — Rect 완료 (Line1 / Line2 / Vertical / HorizontalA / HorizontalB 공통)
private void HalconViewer_DatumRectCompleted(object sender, EventArgs e) {
    halconViewer.RectDrawingCompleted -= HalconViewer_DatumRectCompleted;
    var roi = halconViewer.CommitActiveRectangle();
    if (roi == null || _editingDatum == null) { ExitCanvasMode(); return; }

    // RoiDefinition bbox → Rectangle2 center+phi+lengths (CommitRectRoi L677-680 과 동일 계산)
    double centerRow = (roi.Row1 + roi.Row2) / 2.0;
    double centerCol = (roi.Column1 + roi.Column2) / 2.0;
    double halfH     = (roi.Row2 - roi.Row1) / 2.0;
    double halfW     = (roi.Column2 - roi.Column1) / 2.0;

    //260423 hbk Phase 12 — step 별 DatumConfig 필드 기록
    switch (_datumTeachStep) {
        case EDatumTeachStep.Line1:
        case EDatumTeachStep.Vertical:  // D-07 Line1 재사용
            _editingDatum.Line1_Row     = centerRow;
            _editingDatum.Line1_Col     = centerCol;
            _editingDatum.Line1_Phi     = 0.0;
            _editingDatum.Line1_Length1 = halfH;
            _editingDatum.Line1_Length2 = halfW;
            break;
        case EDatumTeachStep.Line2:
            _editingDatum.Line2_Row     = centerRow;
            _editingDatum.Line2_Col     = centerCol;
            _editingDatum.Line2_Phi     = 0.0;
            _editingDatum.Line2_Length1 = halfH;
            _editingDatum.Line2_Length2 = halfW;
            break;
        case EDatumTeachStep.HorizontalA:
            _editingDatum.Horizontal_A_Row     = centerRow;
            _editingDatum.Horizontal_A_Col     = centerCol;
            _editingDatum.Horizontal_A_Phi     = 0.0;
            _editingDatum.Horizontal_A_Length1 = halfH;
            _editingDatum.Horizontal_A_Length2 = halfW;
            break;
        case EDatumTeachStep.HorizontalB:
            _editingDatum.Horizontal_B_Row     = centerRow;
            _editingDatum.Horizontal_B_Col     = centerCol;
            _editingDatum.Horizontal_B_Phi     = 0.0;
            _editingDatum.Horizontal_B_Length1 = halfH;
            _editingDatum.Horizontal_B_Length2 = halfW;
            break;
    }

    AdvanceDatumTeachStep();
}

//260423 hbk Phase 12 — Circle 완료 (CircleTwoHorizontal 첫 step)
private void HalconViewer_DatumCircleCompleted(object sender, CircleDrawCompletedArgs e) {
    halconViewer.CircleDrawingCompleted -= HalconViewer_DatumCircleCompleted;
    if (_editingDatum == null || e.Radius <= 0) { ExitCanvasMode(); return; }

    _editingDatum.CircleROI_Row    = e.CenterRow;
    _editingDatum.CircleROI_Col    = e.CenterCol;
    _editingDatum.CircleROI_Radius = e.Radius;

    AdvanceDatumTeachStep();
}

private void AdvanceDatumTeachStep() {
    _datumTeachStep = GetNextStep(_editingDatum.AlgorithmType, _datumTeachStep);
    StartDatumTeachStep(_datumTeachStep);  // Done 이면 InvokeTryTeachDatum 진입
}
```

---

**6.5 — `InvokeTryTeachDatum` (D-02 마지막 ROI 직후 자동 호출):**
```csharp
//260423 hbk Phase 12 D-02 — 마지막 ROI MouseUp 후 DatumFindingService.TryTeachDatum 즉시 호출
private void InvokeTryTeachDatum() {
    if (_editingDatum == null) { ExitCanvasMode(); return; }

    HImage img = halconViewer.CurrentImage; //260423 hbk Phase 11 이미지는 이미 Datum 노드 선택 시점에 로드됨
    if (img == null) {
        label_drawHint.Content = "Datum 티칭 실패: 이미지가 없습니다. Grab 하세요";
        label_drawHint.Foreground = new SolidColorBrush(
            (Color)ColorConverter.ConvertFromString("#FFF87171")); //260423 hbk UI-SPEC error red
        ExitCanvasMode();
        return;
    }

    var svc = new ReringProject.Halcon.Algorithms.DatumFindingService();
    string error;
    bool ok = svc.TryTeachDatum(img, _editingDatum, out error);
    if (ok) {
        label_drawHint.Content = "Datum 티칭 완료 — Recipe Save 권장";
        label_drawHint.Foreground = new SolidColorBrush(
            (Color)ColorConverter.ConvertFromString("#FF4ADE80")); //260423 hbk UI-SPEC success green
        //260423 hbk Phase 11 D-11 — 오버레이 갱신 (LastTeachSucceeded=true → HalconDisplayService 분기)
        halconViewer.SetDatumOverlay(_editingDatum, true);
    }
    else {
        label_drawHint.Content = "Datum 티칭 실패: " + (error ?? "unknown");
        label_drawHint.Foreground = new SolidColorBrush(
            (Color)ColorConverter.ConvertFromString("#FFF87171")); //260423 hbk UI-SPEC error red
    }

    // ROI 유지 — 재튜닝 가능. canvas mode 해제.
    _canvasMode = ECanvasMode.None;
    btn_teachDatum.IsChecked = false;
    _editingDatum = null;
}
```

---

**6.6 — Extend `ExitCanvasMode` (L606-633) to unsubscribe Datum handlers + reset state:**
```csharp
private void ExitCanvasMode() {
    halconViewer.ImageLeftClicked -= HalconViewer_PolygonMouseDown;
    halconViewer.ImageRightClicked -= HalconViewer_PolygonRightClick;
    halconViewer.RectDrawingCompleted -= HalconViewer_RectDrawingCompleted;
    halconViewer.CircleDrawingCompleted -= HalconViewer_CircleDrawingCompleted;
    //260423 hbk Phase 12 — Datum 티칭 핸들러 unsubscribe
    halconViewer.RectDrawingCompleted   -= HalconViewer_DatumRectCompleted;
    halconViewer.CircleDrawingCompleted -= HalconViewer_DatumCircleCompleted;

    _canvasMode = ECanvasMode.None;
    _editingFai = null;
    _editingCircleMeasurement = null;
    _editingCircleFaiName = null;
    _editingDatum = null; //260423 hbk Phase 12
    btn_rectRoi.IsChecked = false;
    btn_polygonRoi.IsChecked = false;
    btn_circleRoi.IsChecked = false;
    btn_teachDatum.IsChecked = false; //260423 hbk Phase 12
    label_drawHint.Visibility = Visibility.Collapsed;
    label_drawHint.Foreground = new SolidColorBrush( //260423 hbk Phase 12 — info 색상 복원 (next draw mode 에서 error red 가 residual 되지 않도록)
        (Color)ColorConverter.ConvertFromString("#FFAAAAAA"));
    label_pointCount.Visibility = Visibility.Collapsed;
    halconViewer.ClearPolygonDraft();
    _polygonPoints.Clear();

    halconViewer.ImageLeftClicked -= HalconViewer_CalibrationMouseDown;
    halconViewer.ClearCalibrationOverlay();
    btn_calibrate.Content = "Calibrate";
    _calibrationPoints.Clear();
}
```

**Phase 12 application note:** Escape-key behavior (MainView_PreviewKeyDown L598-603) is automatic — Escape routes through `ExitCanvasMode()` which already unsubscribes everything. No new Escape handler needed.

---

### 7. `InspectionListView.xaml.cs` — Datum 노드 선택 시 btn_teachDatum 활성화 + btn_circleRoi 억제

**Role:** view code-behind. **Data Flow:** event-driven (SelectionChanged → btn_teachDatum.IsEnabled).

**Analog:** same file. `InspectionList_SelectionChanged` (L240-348). Specifically:
- L249 `mParentWindow.mainView.btn_circleRoi.IsEnabled = false;` (선택 해제 시 초기화)
- L280-294 Datum 노드 분기 (Grab/LoadImage 활성화 + SetDatumOverlay)
- L333-345 Circle 활성화 게이팅 (FAI/Measurement 기반)

**Existing Datum node branch (L280-294):**
```csharp
//260409 hbk Phase 4: Datum node selection -> PropertyGrid binding (D-10)
if (item.NodeType == ENodeType.Datum) {
    //260417 hbk Phase 6 Plan 04: Datum CRUD 활성화 (D-25)
    button_addFAI.IsEnabled = true;
    button_removeFAI.IsEnabled = true;
    button_renameFAI.IsEnabled = true;
    //260423 hbk Datum 노드: Grab/LoadImage 활성화 (DatumConfig → ResolveDatumCameraParam 위임)
    button_grab.IsEnabled = true;
    button_loadImage.IsEnabled = true;
    // PropertyGrid already handled by SetParam above (DatumConfig : ParamBase)
    _inspectionVm?.ClearResults();
    //260410 hbk Phase 4 gap fix: show Datum overlay on canvas when Datum node selected
    if (itemParam is DatumConfig datumCfg) {
        mParentWindow.mainView.halconViewer.SetDatumOverlay(datumCfg, true);
    }
}
```

**Phase 12 addition — insert at the end of this Datum branch (before closing brace at L294):**
```csharp
//260423 hbk Phase 12 D-01 — Datum 노드 선택 시 btn_teachDatum 활성화
if (mParentWindow != null && mParentWindow.mainView != null) {
    mParentWindow.mainView.btn_teachDatum.IsEnabled = true;
}
```

**btn_teachDatum 비활성화 (선택 해제/비-Datum 노드 시) — insert next to L249:**
```csharp
//260423 hbk Phase 11 D-15 — 선택 해제 시 Circle ROI 비활성 (기본값)
if (mParentWindow != null && mParentWindow.mainView != null) {
    mParentWindow.mainView.btn_circleRoi.IsEnabled = false;
    //260423 hbk Phase 12 D-01 — 선택 해제 시 btn_teachDatum 비활성 (Datum 분기에서만 true)
    mParentWindow.mainView.btn_teachDatum.IsEnabled = false;
}
```

**btn_circleRoi suppression — CONTEXT.md canonical_refs 명시: "btn_circleRoi는 Datum 노드에서 비활성화 유지(충돌 방지)":**
Phase 11의 Circle 활성화 게이팅(L333-345)는 `FAIConfig` / `CircleDiameterMeasurement` 일 때만 true가 된다. Datum 노드는 `ENodeType.Datum` 이므로 이 블록은 자연스럽게 `circleEnabled=false` 로 수렴 — 명시적 suppression 코드 추가 불필요. **단 Plan 03 구현 시 Datum 노드에서 btn_circleRoi.IsEnabled 가 false 인지 육안 검증 필수**.

---

### 8. `HalconDisplayService.cs` — RenderDatumOverlay에 CircleTwoHorizontal 원 + 알고리즘별 오버레이 분기 (D-13)

**Role:** rendering service. **Data Flow:** DatumConfig → HWindow draw ops.

**Analog:** same file. `RenderDatumOverlay` (L350-429). 특히:
- L363-376: Line1_*/Line2_* Rectangle2 렌더 (기존)
- L379-397: `datum.IsConfigured` 분기 마젠타 십자 + 라벨 (기존)
- L399-423: `datum.LastTeachSucceeded` 분기 → Line1Detected_*/Line2Detected_* yellow/cyan + red cross 20px (Phase 11 Plan 03a 완료)

**Phase 12 확장 — 두 개 추가 분기:**

**8.1 — `AlgorithmType == CircleTwoHorizontal` 이고 검출 성공 시 CircleDetected_Radius 원 렌더**

CONTEXT.md canonical_refs: `RenderDatumOverlay: add DispCircle for CircleTwoHorizontal`. Existing L399-423 블록(`LastTeachSucceeded`) 내부에 `AlgorithmType == CircleTwoHorizontal && CircleDetected_Radius > 0` 세부 분기 삽입:

```csharp
//260423 hbk Phase 12 D-13 — CircleTwoHorizontal 전용 원 검출 오버레이 (노란 원)
if (datum.AlgorithmType == EDatumAlgorithm.CircleTwoHorizontal && datum.CircleDetected_Radius > 0)
{
    HOperatorSet.SetColor(window, "yellow");
    HOperatorSet.SetLineWidth(window, 2);
    HOperatorSet.DispCircle(window, datum.CircleCenter_Row, datum.CircleCenter_Col, datum.CircleDetected_Radius);
    // 중심점 십자 마커 (6px, 빨강) — 기존 Circle ROI 중심 표기와 동일 (Line 59-63 패턴)
    HOperatorSet.SetColor(window, "red");
    HOperatorSet.SetLineWidth(window, 2);
    HOperatorSet.DispLine(window, datum.CircleCenter_Row - 6, datum.CircleCenter_Col,
                                   datum.CircleCenter_Row + 6, datum.CircleCenter_Col);
    HOperatorSet.DispLine(window, datum.CircleCenter_Row, datum.CircleCenter_Col - 6,
                                   datum.CircleCenter_Row, datum.CircleCenter_Col + 6);
}
```

**8.2 — Circle ROI 검색 영역 오버레이 (티칭 중/완료 무관, `CircleROI_Radius > 0` 일 때):**

기존 Line1/Line2 Rectangle2 블록(L363-376)과 동일 수준에서 Circle ROI Rectangle → DispCircle 로 대체:
```csharp
//260423 hbk Phase 12 D-10 — Circle 검색 영역 (CircleTwoHorizontal 일 때만)
if (datum.AlgorithmType == EDatumAlgorithm.CircleTwoHorizontal && datum.CircleROI_Radius > 0)
{
    // 기존 Line1/Line2 색(cyan/blue + selected) 동일 적용
    HOperatorSet.SetColor(window, color);
    HOperatorSet.SetLineWidth(window, lineWidth);
    HOperatorSet.DispCircle(window, datum.CircleROI_Row, datum.CircleROI_Col, datum.CircleROI_Radius);
}
```

**8.3 — Horizontal_A/B Rectangle2 오버레이 (CircleTwoHorizontal + VerticalTwoHorizontal 공통):**

L363-376의 Line1/Line2 패턴을 Horizontal_A/B 에 동일 적용:
```csharp
//260423 hbk Phase 12 D-11 — Horizontal A/B ROI 렌더 (CircleTwoHorizontal/VerticalTwoHorizontal 공통)
if (datum.AlgorithmType != EDatumAlgorithm.TwoLineIntersect)
{
    if (datum.Horizontal_A_Length1 > 0 && datum.Horizontal_A_Length2 > 0)
    {
        HOperatorSet.DispRectangle2(window,
            datum.Horizontal_A_Row, datum.Horizontal_A_Col, datum.Horizontal_A_Phi,
            datum.Horizontal_A_Length1, datum.Horizontal_A_Length2);
    }
    if (datum.Horizontal_B_Length1 > 0 && datum.Horizontal_B_Length2 > 0)
    {
        HOperatorSet.DispRectangle2(window,
            datum.Horizontal_B_Row, datum.Horizontal_B_Col, datum.Horizontal_B_Phi,
            datum.Horizontal_B_Length1, datum.Horizontal_B_Length2);
    }
}
```

**8.4 — Line2 ROI rectangle 은 TwoLineIntersect 에서만 렌더** (D-13 시맨틱스 정렬):

기존 L370-376 의 `if (datum.Line2_Length1 > 0 && datum.Line2_Length2 > 0)` 조건에 AlgorithmType guard 추가:
```csharp
if (datum.AlgorithmType == EDatumAlgorithm.TwoLineIntersect
    && datum.Line2_Length1 > 0 && datum.Line2_Length2 > 0)
{
    HOperatorSet.DispRectangle2(window,
        datum.Line2_Row, datum.Line2_Col, datum.Line2_Phi,
        datum.Line2_Length1, datum.Line2_Length2);
}
```

**Phase 12 application note:** `using ReringProject.Sequence;` is already in the file (L7), so `EDatumAlgorithm` type is accessible without new imports. The existing `try { ... } catch { }` outer wrapper (L357-428) suppresses any Halcon rendering error — Phase 12 additions are inside the same try block, so display errors are swallowed per existing convention. `LastTeachSucceeded` 분기의 Line1Detected/Line2Detected 렌더링은 그대로 재사용 — 알고리즘별로 시맨틱스만 달라진다 (CircleTwoHorizontal: Line1Detected=수직 가상선 2점, Line2Detected=수평 결합선 / VerticalTwoHorizontal: Line1Detected=수직 검출선, Line2Detected=수평 결합선 — D-13).

---

## Shared Patterns

### Shared 1 — `//260423 hbk` comment marker on every new/modified line (user memory)

**Source:** `feedback_comment_convention.md` user memory + existing style (DatumConfig.cs L28/76, DatumFindingService.cs L147/199, HalconDisplayService.cs L50, MainView.xaml.cs L40-46/82/83/612/698).

**Apply to:** every new line in every Phase 12 file. Every modified line gets a fresh `//260423 hbk Phase 12 [D-nn] — <intent>` comment. Do NOT delete existing `//YYMMDD hbk` markers from earlier phases.

Example (DatumConfig.cs):
```csharp
//260409 hbk Phase 4: ...  (existing — keep)
//260423 hbk Phase 11 D-08 — ...  (existing — keep)
//260423 hbk Phase 12 D-09 — Datum 알고리즘 선택자
```

### Shared 2 — ToggleButton click-handler shape (CircleRoiButton_Click L699-730)

**Source:** MainView.xaml.cs L699-730 (CircleRoiButton_Click).
**Apply to:** `TeachDatumButton_Click`.
**Pattern:**
```csharp
if (btn.IsChecked == true) {
    ExitCanvasMode();          // defensive: clear any prior canvas mode
    _canvasMode = ECanvasMode.TeachDatum;
    btn.IsChecked = true;

    var target = <resolve selected param>;
    if (target == null) { CustomMessageBox.Show(...); ExitCanvasMode(); return; }
    _editing<target> = target;

    label_drawHint.Content = "<안내 문구>";
    label_drawHint.Visibility = Visibility.Visible;
    halconViewer.<Event>Completed += <Handler>;
    halconViewer.Start<Shape>Drawing();
}
else {
    ExitCanvasMode();          // manual unchecked = cancel
}
```

### Shared 3 — Per-drawing-step commit + state advance (CommitCircleRoi L773-797 analog)

**Source:** MainView.xaml.cs L773-797 (CommitCircleRoi).
**Apply to:** each step in Datum teach state machine.
**Pattern:**
1. Validate state (_canvasMode + _editing<target> + input validity)
2. Write to target's fields by copying from ROI/event args
3. Call `halconViewer.UpdateDisplayState(rois, selectedId, null, null)` or `SetDatumOverlay(...)` to refresh
4. Advance state (`_datumTeachStep = GetNextStep(...)`) OR `ExitCanvasMode()` on error

### Shared 4 — Halcon API `try/catch (Exception ex) { error = ex.Message; return false; } finally { handle.Dispose(); }` envelope

**Source:** DatumFindingService.cs L224-294 (TryFindLine), VisionAlgorithmService.cs L37-114 (TryFitLine), `TryFindCircle` L126-200.
**Apply to:** every new Halcon-calling method in Phase 12 (`TryTeachCircleTwoHorizontal`, `TryTeachVerticalTwoHorizontal`, `TryExtractEdgePoints`).
**Pattern:** wrap all `HOperatorSet.*` calls in try/catch returning `false` with error string; finally-dispose `HObject` / `HTuple` handles (Dispose() inside nested try { } catch { } to suppress dispose errors). Never throw from a `Try*` method.

### Shared 5 — ParamBase serialization: enum gotcha + Browsable(false) on volatile fields

**Source:** ParamBase.cs L324-400 (switch on type.Name), Phase 11 PATTERNS.md §Shared 8.
**Apply to:** `DatumConfig.AlgorithmType` (enum) + Circle/Horizontal volatile `double` fields.
**Constraints:**
- `EDatumAlgorithm` enum → ParamBase switch hits `default: break` and SKIPS — Phase 12 planner MUST verify with a round-trip test during Plan 01. Fallback: store as `string AlgorithmType` + internal parse helper (mirrors existing `string ImageSourceMode` at DatumConfig.cs:22).
- `[Browsable(false)]` on volatile `public double CircleCenter_*` / `CircleDetected_Radius` fields: they WILL still be INI-serialized because ParamBase reflects on `public` properties regardless of Browsable. Accept the INI noise (same as Line1Detected_* in Phase 11 Plan 03a).

### Shared 6 — Halcon named color palette (no `set_rgb`)

**Source:** HalconDisplayService.cs throughout.
**Available:** `"red"`, `"yellow"`, `"cyan"`, `"lime green"`, `"magenta"`, `"white"`, `"green"`, `"orange"`, `"blue"`.
**Phase 12 uses:** `"yellow"` for Circle detect outline (§8.1), `"red"` for Circle center cross (§8.1), inherited `"cyan"`/`"blue"` for Horizontal A/B Rectangle2 outlines (§8.3).

### Shared 7 — C# 7.2 / .NET 4.8 constraint

**Source:** CLAUDE.md constraints.
**Forbidden:** `switch` expressions (use `switch` statement), `record` types, nullable reference types (`string?`), target-typed `new()`, `init` setters, top-level statements.
**Allowed:** `out var`, pattern matching `is T x`, tuple `(a, b) = ...`, local functions, interpolated strings.
**Phase 12 impact:** `GetFirstStep`/`GetNextStep` use classic `switch` statements with `return` per case (not expressions); nested `catch (Exception ex)` is the Halcon standard.

### Shared 8 — Brace style: Allman in Halcon code, K&R in Sequence/UI code

**Source:** CLAUDE.md Code Style — "Newer Halcon code (MeasurementAlgorithm, RoiDefinition, TeachingStorageService): Allman brace style". "Older code (Logging, SequenceBase, VirtualCamera): K&R brace style".
**Phase 12 application:**
- `EDatumAlgorithm.cs`, `DatumConfig.cs`, `DatumFindingService.cs`, `VisionAlgorithmService.cs` (already Allman), `HalconDisplayService.cs` (Allman) → **Allman**.
- `MainView.xaml.cs`, `InspectionListView.xaml.cs` → match surrounding style (**K&R** for MainView.xaml.cs — see L43 `private enum ECanvasMode { ... }` same line, L53 `public MainView() {` same line; InspectionListView.xaml.cs mixed but new additions follow the Datum branch K&R at L280).

Never mix styles within one file.

### Shared 9 — DatumFindingService instantiation at call site

**Source:** CircleDiameterMeasurement.cs L47 (`var svc = new VisionAlgorithmService();`), MainView.xaml.cs (Phase 12) pattern.
**Apply to:** `InvokeTryTeachDatum` in MainView.xaml.cs §6.5 — `var svc = new ReringProject.Halcon.Algorithms.DatumFindingService();`. DatumFindingService is stateless; no singleton/DI pattern in project.

---

## No Analog Found

| File | Role | Data Flow | Reason |
|------|------|-----------|--------|
| — | — | — | — |

All Phase 12 additions have in-repo analogs — either same-file analogs (MODIFY-EXTEND) or closely-matched sibling files (EDatumAlgorithm.cs mirrors EdgeOptionLists.cs).

---

## Metadata

**Analog search scope:**
- `WPF_Example/Custom/Sequence/Inspection/` (DatumConfig, EdgeOptionLists, FAIConfig, Measurements/*)
- `WPF_Example/Halcon/Algorithms/` (DatumFindingService, VisionAlgorithmService, MeasurementAlgorithm)
- `WPF_Example/Halcon/Display/` (HalconDisplayService)
- `WPF_Example/UI/ContentItem/` (MainView.xaml, MainView.xaml.cs, MainResultViewerControl.xaml.cs)
- `WPF_Example/UI/ControlItem/` (InspectionListView.xaml.cs)
- `WPF_Example/Sequence/Param/` (ParamBase — serialization reference, Phase 11 PATTERNS §Shared 8)

**Files scanned:** 11
**Pattern extraction date:** 2026-04-23
**Inherited conventions:**
- Korean `//260423 hbk Phase 12 D-nn — <intent>` comment marker on every new/modified line (user memory `feedback_comment_convention`)
- Brace style per CLAUDE.md (Allman in Halcon code, K&R in UI code)
- C# 7.2 / .NET 4.8 (no switch-expressions, no records, no NRT)
- Error-string literals per SPEC Acceptance Criteria (no algorithm prefix — D-14)
- ParamBase reflection serialization constraints (Shared 5)
- Halcon `try { } catch { return false; } finally { Dispose(); }` convention (Shared 4)
- Phase 11 Plan 03a Datum overlay infrastructure (Line*Detected_*, LastTeachSucceeded, RenderDatumOverlay detected-line block at HalconDisplayService.cs:399-423) — Phase 12 extends but does not replace

---

*Phase: 12-datum-circle-vertical-horizontal-intersection*
*Pattern map generated: 2026-04-23*
*Next step: /gsd-plan-phase 12 — consume this PATTERNS.md for Plan 01/02/03 action-by-action references*
