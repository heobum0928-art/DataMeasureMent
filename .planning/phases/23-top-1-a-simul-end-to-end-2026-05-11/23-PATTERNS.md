# Phase 23: Top #1 A시리즈 Simul end-to-end — Pattern Map

**Mapped:** 2026-05-11
**Files analyzed:** 6 (1 new + 5 modified)
**Analogs found:** 6 / 6 (exact-match patron for every file)

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|-------------------|------|-----------|----------------|---------------|
| `WPF_Example/Custom/Sequence/Inspection/Measurements/EdgeToLineDistanceMeasurement.cs` (NEW) | Measurement (MeasurementBase 파생) | transform (image+transform → mm) | `Measurements/PointToLineDistanceMeasurement.cs` | exact (직접 patron) |
| `WPF_Example/Custom/Sequence/Inspection/MeasurementFactory.cs` (MOD) | Factory dispatch | request-response (string → instance) | self (`MeasurementFactory.cs` L14-30) | exact (in-file pattern) |
| `WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs` (MOD) | Action step (image acquisition) | file-I/O + fallback chain | self (`Action_FAIMeasurement.cs` L223-242 본문) | exact (in-file extension) |
| `WPF_Example/Halcon/Algorithms/VisionAlgorithmService.cs` (MOD) | Halcon service (signature ext) | request-response | `TryFindCircleByPolarSampling` L222-264 (selection param 이미 통과) | role-match (동일 파일 내 다른 메서드) |
| `WPF_Example/Custom/Sequence/Inspection/FAIConfig.cs` (자동, 검증만) | Config (PropertyGrid ItemsSource) | reflection (no code change) | self (`FAIConfig.cs` L59-60 ItemsSource 캐시) | exact (단일 소스이므로 자동) |
| `WPF_Example/DatumMeasurement.csproj` (MOD) | MSBuild project | build manifest | self (L218-222 Compile ItemGroup) | exact (in-file pattern) |

---

## Pattern Assignments

### File 1: `EdgeToLineDistanceMeasurement.cs` (NEW)

**Role:** Measurement (MeasurementBase 파생) — Point ROI fit → datumTransform 적용 → Y좌표 추출
**Data flow:** `HImage + HTuple datumTransform → double resultValue (mm)` + `out List<EdgeInspectionOverlay>`
**Analog:** `WPF_Example/Custom/Sequence/Inspection/Measurements/PointToLineDistanceMeasurement.cs` (전체 95 라인)

#### A. Imports + file-header pattern (analog L1-8)

```csharp
//260413 hbk Phase 6: 점-선 거리 측정 (D-15)
using System.Collections.Generic; //260422 hbk Phase 7: List<T> (D-01)
using HalconDotNet;
using PropertyTools.DataAnnotations;
using ReringProject.Halcon.Algorithms;
using ReringProject.Halcon.Models; //260422 hbk Phase 7: EdgeInspectionOverlay (D-01)

namespace ReringProject.Sequence
{
```

**For new file** — 상단 헤더 마커를 `//260511 hbk Phase 23 ALG-01: Datum Y거리 측정 (D-06)` 으로 교체. 나머지 5 using + namespace 동일.

#### B. Class declaration + TypeName property (analog L15-17)

```csharp
public class PointToLineDistanceMeasurement : MeasurementBase //260413 hbk
{
    public override string TypeName { get { return "PointToLineDistance"; } }
```

**Copy:** `MeasurementBase` 상속 + `override string TypeName { get { return "EdgeToLineDistance"; } }` (D-06 알고리즘명 lock).

#### C. ROI category + 5개 Point ROI 필드 (analog L19-24)

```csharp
[Category("Point|ROI")]
public double Point_Row { get; set; }
public double Point_Col { get; set; }
public double Point_Phi { get; set; }
public double Point_Length1 { get; set; }
public double Point_Length2 { get; set; }
```

**Copy 그대로.** Line_* 5필드는 EdgeToLineDistance 에 **불필요** (별도 Line ROI 없음 — datumTransform 으로 Y=0 내재). D-08 = Rectangle only.

#### D. Edge 6 파라미터 + EdgeSelection 7번째 추가 (analog L33-47)

```csharp
[Category("Edge")]
public int EdgeThreshold { get; set; } = 10;
public double Sigma { get; set; } = 1.0;
public int EdgeSampleCount { get; set; } = 20;
public int EdgeTrimCount { get; set; } = 10;
[ItemsSourceProperty(nameof(EdgePolarityList))] //260423 hbk WR-RT-02 ComboBox 처리
public string EdgePolarity { get; set; } = "DarkToLight";
[ItemsSourceProperty(nameof(EdgeDirectionList))] //260423 hbk WR-RT-02 ComboBox 처리
public string EdgeDirection { get; set; } = "LtoR";

[PropertyTools.DataAnnotations.Browsable(false)]
public List<string> EdgeDirectionList { get { return EdgeOptionLists.Directions; } }
[PropertyTools.DataAnnotations.Browsable(false)]
public List<string> EdgePolarityList { get { return EdgeOptionLists.FAIPolarities; } }
```

**Modifications for new file (D-07 + D-10 + Pitfall 6):**
1. `EdgeDirection default = "TtoB"` (수평 에지 검출, Y방향 거리 측정 의도 — RESEARCH Pitfall 6).
2. **신규 EdgeSelection 추가** (D-10 memory feedback 필수):
   ```csharp
   [ItemsSourceProperty(nameof(EdgeSelectionList))] //260511 hbk Phase 23 ALG-01 — D-10
   public string EdgeSelection { get; set; } = "First";

   [PropertyTools.DataAnnotations.Browsable(false)]
   public List<string> EdgeSelectionList { get { return EdgeOptionLists.Selections; } }
   ```
   (참조: `EdgeOptionLists.Selections = { "First", "Last", "All" }` — L22)

#### E. Constructor (analog L49)

```csharp
public PointToLineDistanceMeasurement(object owner) : base(owner) { } //260413 hbk
```

**Copy 그대로** — 이름만 변경 + 마커 `//260511 hbk Phase 23 ALG-01`.

#### F. TryExecute core pattern (analog L51-92) — **핵심 차이점 있음**

**Analog (PointToLineDistance) — Line ROI 2회 fit 후 수직거리:**

```csharp
public override bool TryExecute( //260413 hbk //260422 hbk Phase 7: out overlays 추가 (D-01)
    HImage image,
    HTuple datumTransform,
    double pixelResolution,
    out double resultValue,
    out string error,
    out List<EdgeInspectionOverlay> overlays)
{
    resultValue = 0;
    error = null;
    overlays = new List<EdgeInspectionOverlay>(); //260422 hbk Phase 7: 5종 overlay 미구현 — 빈 리스트 반환 (D-03)

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
    // ... 이후 Line ROI 2차 fit + DistancePointToLine (EdgeToLineDistance 에서는 제거) ...
}
```

**For new file (D-06 + D-02 + D-10 적용):**

- Line ROI fit 단계 (analog L78-87) **삭제**.
- TryFitLine 호출 시 `EdgeSelection` 인자 1개 추가 (signature 확장과 매칭, File 4 참조).
- midpoint 추출 후 datumTransform 으로 점 변환 → Y 좌표 부호 반전 → resultValue.

권장 패치 (RESEARCH Code Examples Operation 1 의 골격 + D-02 부호 반전):

```csharp
if (!svc.TryFitLine(image,
    Point_Row, Point_Col, Point_Phi, Point_Length1, Point_Length2,
    datumTransform,
    EdgeSampleCount, EdgeTrimCount, Sigma, EdgeThreshold,
    EdgeDirection, EdgePolarity, EdgeSelection, //260511 hbk Phase 23 ALG-01 — D-10 EdgeSelection 명시
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
        // transform 실패 시 image-row 좌표 사용 (TryFitLine 패턴 일관성)
    }
}
resultValue = -datumRow * pixelResolution; //260511 hbk Phase 23 ALG-01 — D-02 +Y 부호 (위쪽 양수)
return true;
```

#### G. Error handling convention

- HOperatorSet 호출 = `try { ... } catch { }` (CLAUDE.md "HOperatorSet 호출은 bare catch + return false")
- 본 메서드는 throw 금지, `out error` 채우고 `return false` (analog L73, L86 패턴)

#### H. Style — **Allman** (analog L9, L16, L18 — opening brace on its own line). 신규 파일도 Allman 강제.

---

### File 2: `MeasurementFactory.cs` (MOD)

**Role:** String→instance dispatch + UI ComboBox 단일 소스
**Analog:** self (in-file pattern)

#### A. Switch case 7번째 추가 (L14-30)

```csharp
switch (typeName)
{
    case "EdgePairDistance":
        return new EdgePairDistanceMeasurement(owner);
    case "PointToLineDistance":
        return new PointToLineDistanceMeasurement(owner);
    case "PointToPointDistance":
        return new PointToPointDistanceMeasurement(owner);
    case "LineToLineAngle":
        return new LineToLineAngleMeasurement(owner);
    case "CircleDiameter":
        return new CircleDiameterMeasurement(owner);
    case "LineToLineDistance":
        return new LineToLineDistanceMeasurement(owner);
    default:
        return null;
}
```

**Patch (D-06):** "LineToLineDistance" case 와 default 사이에 7번째 case 삽입:
```csharp
case "EdgeToLineDistance":
    return new EdgeToLineDistanceMeasurement(owner); //260511 hbk Phase 23 ALG-01
```

#### B. GetTypeNames() 배열 7번째 추가 (L33-44)

```csharp
public static string[] GetTypeNames() //260413 hbk UI ComboBox용
{
    return new string[]
    {
        "EdgePairDistance",
        "PointToLineDistance",
        "PointToPointDistance",
        "LineToLineAngle",
        "CircleDiameter",
        "LineToLineDistance"
    };
}
```

**Patch:** 배열 마지막 원소 다음 라인에 `"EdgeToLineDistance" //260511 hbk Phase 23 ALG-01` 추가 + 직전 라인 끝 콤마 보충.

#### C. Convention notes
- 미등록 타입 = `null` 반환 (call site 책임). 본 파일에서 보존.
- Style = Allman (확인 L11, L14).

---

### File 3: `Action_FAIMeasurement.cs` (MOD) — `GrabOrLoadDatumImage` L221-242

**Role:** Datum 이미지 취득 진입점 (SIMUL 단일 진입점)
**Analog:** self (existing method body) + RESEARCH Code Examples Pattern 4

#### A. 현재 본문 (L221-242, K&R style — opening brace on same line as declaration)

```csharp
//260413 hbk Phase 6: Datum 이미지 취득 — Dedicated만 우선 지원 (D-07, D-08)
// ReuseFromShot 모드는 향후 Plan 04 UI 작업과 함께 구현.
private HImage GrabOrLoadDatumImage(InspectionSequence parentSeq) {
    if (ShotParam == null) return null;
    HImage image = null;
    //260511 hbk Phase 22 IMG-02 — Datum 찾기 단계의 이미지 = InspectionImagePath (= ShotParam.SimulImagePath) 사용. TeachingImagePath (DatumConfig 보존) 는 본 메서드에서 미참조 — 재티칭/UI 셋업 경로에서만 참조 (Phase 23 carry-over 가능).
    #if SIMUL_MODE
    if (!string.IsNullOrEmpty(ShotParam.SimulImagePath) && File.Exists(ShotParam.SimulImagePath)) {
        try {
            image = new HImage(ShotParam.SimulImagePath);
        } catch {
            image = null;
        }
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

#### B. Patch pattern (D-04 carry-over + Pitfall 3)

진입부에 `DatumConfigs[0].TeachingImagePath` 우선순위 분기 stacking. **기존 Phase 22 IMG-02 주석은 보존하고 그 위/아래에 Phase 23 마커 stacking** (memory feedback_comment_convention 의 stacking 규칙).

```csharp
private HImage GrabOrLoadDatumImage(InspectionSequence parentSeq) {
    if (ShotParam == null) return null;
    HImage image = null;
    //260511 hbk Phase 22 IMG-02 — (기존 주석 보존)
    //260511 hbk Phase 23 ALG-01 — D-04 TeachingImagePath 자동 로드 (Phase 22 carry-over). 비어있지 않으면 우선, 비어있으면 SimulImagePath 폴백.
    string teachingPath = null;
    if (parentSeq != null && parentSeq.DatumConfigs != null && parentSeq.DatumConfigs.Count > 0) {
        teachingPath = parentSeq.DatumConfigs[0].TeachingImagePath;
    }
    #if SIMUL_MODE
    if (!string.IsNullOrEmpty(teachingPath) && File.Exists(teachingPath)) {
        try { image = new HImage(teachingPath); } catch { image = null; }
    }
    if (image == null && !string.IsNullOrEmpty(ShotParam.SimulImagePath) && File.Exists(ShotParam.SimulImagePath)) {
        try {
            image = new HImage(ShotParam.SimulImagePath);
        } catch {
            image = null;
        }
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

#### C. Convention notes
- **Style = K&R** (기존 본문 L223 `private HImage ... {` 동일 라인 brace). 신규 라인도 K&R 유지.
- `parentSeq.DatumConfigs` 멤버 존재 확인 필요 (RESEARCH Assumption A6 — 다중 Datum 시 첫 번째만, Phase 23 D-03 Top Fixture #1 단독 가정으로 안전).
- `Pitfall 3` — `string.IsNullOrEmpty(teachingPath) && File.Exists(teachingPath)` 2-step 가드 필수.

---

### File 4: `VisionAlgorithmService.cs` — `TryFitLine` 시그니처 확장 (MOD)

**Role:** Halcon 빌딩 블록 (FitLine 헬퍼)
**Analog (in-file):** `TryFindCircleByPolarSampling` L222-264 — 동일 파일 내 다른 메서드가 이미 `string selection` 인자를 받아 `selectionLower` 로 변환 + `MeasurePos` 4번째 인자 전달하는 정확한 패턴 보유.

#### A. 동일 파일 내 selection 처리 패턴 (L222-264)

```csharp
public bool TryFindCircleByPolarSampling(
    HImage image,
    double centerRow, double centerCol, double radius,
    double stepDeg, double rectL1Ratio, double rectL2Ratio,
    double sigma, int threshold, string polarity, string selection, //260429 hbk Phase 15 — selection 명시 처리 ("all" 하드코딩 제거)
    HTuple datumTransform,
    ...)
{
    ...
    //260429 hbk Phase 15 — selection sanity clamp + PascalCase → Halcon lower-case 변환 (CANONICAL: MeasurementAlgorithm.cs:178)
    if (string.IsNullOrEmpty(selection)) selection = "First";
    string selectionLower;
    if (string.Equals(selection, "Last", StringComparison.OrdinalIgnoreCase))
    {
        selectionLower = "last";
    }
    else if (string.Equals(selection, "All", StringComparison.OrdinalIgnoreCase))
    {
        selectionLower = "all";
    }
    else
    {
        selectionLower = "first";
    }
    ...
    HOperatorSet.MeasurePos(image, measureHandle,
        sigma, threshold, polarity, selectionLower, //260429 hbk Phase 15 — "all" 하드코딩 → caller selection 반영
        out eRows, out eCols, out amp, out dist);
```

**이것이 Phase 23 의 TryFitLine 확장 패턴.** 동일 변환 로직 (PascalCase → lower-case) 을 그대로 적용.

#### B. 현재 TryFitLine 본문에서 변경 지점

**현재 시그니처 (L18-26):**
```csharp
public bool TryFitLine( //260413 hbk
    HImage image,
    double roiRow, double roiCol, double roiPhi,
    double roiLength1, double roiLength2,
    HTuple datumTransform,
    int sampleCount, int trimCount, double sigma, int threshold,
    string direction, string polarity,
    out double row1, out double col1, out double row2, out double col2,
    out string error)
```

**현재 MeasurePos 호출 (L93-95):**
```csharp
HTuple rows, cols, amp, dist;
HOperatorSet.MeasurePos(image, measureHandle,
    Math.Max(0.4, sigma), Math.Max(1, threshold),
    pol, "all", out rows, out cols, out amp, out dist);
```

#### C. Patch pattern (RESEARCH Code Examples Operation 3 + 동일 파일 패턴 일관)

**시그니처 변경 (default param 활용 — RESEARCH Assumption A4: C# 7.2 default param 지원, 기존 5 caller 무수정):**

```csharp
public bool TryFitLine( //260413 hbk
    HImage image,
    double roiRow, double roiCol, double roiPhi,
    double roiLength1, double roiLength2,
    HTuple datumTransform,
    int sampleCount, int trimCount, double sigma, int threshold,
    string direction, string polarity,
    out double row1, out double col1, out double row2, out double col2,
    out string error,
    string selection = "all") //260511 hbk Phase 23 ALG-01 — D-10 memory feedback (optional, 기존 caller 호환)
```

**중요:** C# 의 `out` 파라미터 + default param 조합 시 default 가 있는 인자는 **반드시 마지막에 위치**. 즉 `out string error` 뒤에 `selection` 추가. 또는 `out` 인자들 앞에 `selection = "all"` 위치.

**MeasurePos 호출 변경 (TryFindCircleByPolarSampling L252-264 패턴 그대로 차용):**

```csharp
//260511 hbk Phase 23 ALG-01 — D-10 EdgeSelection 명시 (TryFindCircleByPolarSampling 와 동일 변환 로직)
string measureSel;
if (string.Equals(selection, "First", StringComparison.OrdinalIgnoreCase))
{
    measureSel = "first";
}
else if (string.Equals(selection, "Last", StringComparison.OrdinalIgnoreCase))
{
    measureSel = "last";
}
else
{
    measureSel = "all";
}

HTuple rows, cols, amp, dist;
HOperatorSet.MeasurePos(image, measureHandle,
    Math.Max(0.4, sigma), Math.Max(1, threshold),
    pol, measureSel, out rows, out cols, out amp, out dist); //260511 hbk Phase 23 ALG-01
```

#### D. 기존 caller 영향 분석

5 caller (Grep 검증):
1. `Measurements/PointToLineDistanceMeasurement.cs:66` — Point ROI fit
2. `Measurements/PointToLineDistanceMeasurement.cs:79` — Line ROI fit
3. `Measurements/PointToPointDistanceMeasurement.cs:65, 78`
4. `Measurements/LineToLineAngleMeasurement.cs:65, 76`
5. `Measurements/LineToLineDistanceMeasurement.cs:64, 75`

**모두 default `"all"` 로 호환 — 무수정 보장** (RESEARCH Assumption A4).

**EdgeToLineDistanceMeasurement** 는 caller 로 추가되어 `selection: EdgeSelection` 인자 전달.

#### E. Style — Allman (analog L20, L27, L31 — opening brace on its own line).

---

### File 5: `FAIConfig.cs` (자동 — 검증만)

**Role:** PropertyGrid `EdgeMeasureType` ItemsSource (단일 소스 = `MeasurementFactory.GetTypeNames()`)
**Pattern:** 코드 변경 0 — Factory.GetTypeNames() 에 "EdgeToLineDistance" 추가만으로 자동 노출.

#### A. 검증해야 할 패턴 (RESEARCH Finding #4)

`FAIConfig.cs` L59-60 (RESEARCH 인용):
- `EdgeMeasureType` 의 ItemsSource = `MeasurementFactory.GetTypeNames()` 캐시
- File 2 의 GetTypeNames() 변경만으로 INI Type 자동 dispatch + UI 드롭다운 자동 노출
- D-13 (INI 직접 편집 + UI 'Add FAI' 둘 다) 자동 충족

**Action:** Plan 단계에서 "FAIConfig 변경 없음 — Factory 1줄 추가로 충족" 명시. 단, UAT (SC#4 A6 추가) 시 PropertyGrid 드롭다운에 "EdgeToLineDistance" 실제 표시 확인.

---

### File 6: `DatumMeasurement.csproj` (MOD) — Compile ItemGroup

**Role:** MSBuild project — 신규 .cs 등록 (CS0246 type-not-found 방지)
**Analog (in-file):** L218-222 (Measurements 디렉토리 기존 등록 라인들)

#### A. 기존 패턴 (L218-222)

```xml
<Compile Include="Custom\Sequence\Inspection\DynamicPropertyHelper.cs" />
<Compile Include="Custom\Sequence\Inspection\Measurements\EdgePairDistanceMeasurement.cs" />
<Compile Include="Custom\Sequence\Inspection\Measurements\PointToLineDistanceMeasurement.cs" />
<Compile Include="Custom\Sequence\Inspection\Measurements\PointToPointDistanceMeasurement.cs" />
<Compile Include="Custom\Sequence\Inspection\Measurements\LineToLineAngleMeasurement.cs" />
```

#### B. Patch — 새 Compile 라인 1개 추가 (RESEARCH Pitfall 5)

`PointToLineDistanceMeasurement.cs` 등록 라인 (L220) 다음에 1줄 삽입 (또는 Measurements 그룹 끝):

```xml
<Compile Include="Custom\Sequence\Inspection\Measurements\EdgeToLineDistanceMeasurement.cs" />
```

#### C. Convention notes
- **No marker in csproj** — `//YYMMDD hbk` 주석은 cs 파일에만 적용. csproj 는 plain XML.
- MSBuild 15 classic-style — SDK-style 자동 감지 없음. Manual `<Compile>` 등록 필수.

---

## Shared Patterns

### Pattern A: `//YYMMDD hbk Phase 23 ALG-01` marker (memory feedback_comment_convention)

**Source:** `memory/feedback_comment_convention.md` + Phase 20 D-12 stacking 패턴
**Apply to:** All new/modified 코드 라인 (1, 3, 4 — csproj 제외)
**Convention:**
- 신규 라인 = `//260511 hbk Phase 23 ALG-01 — <reason>`
- 기존 라인 수정 시 = 기존 마커 보존 + 새 마커 stacking (위 또는 끝)
- 예: PointToLine analog L38 `//260423 hbk WR-RT-02 ComboBox 처리` 를 새 파일에서는 `//260511 hbk Phase 23 ALG-01 — ComboBox 처리` 로 교체

### Pattern B: HOperatorSet 에러 처리 (CLAUDE.md "Error Handling")

**Source:** `VisionAlgorithmService.cs` L39-122 (try-catch-finally 패턴)
**Apply to:** EdgeToLineDistanceMeasurement.TryExecute 의 `AffineTransPoint2d` 호출
**Pattern:**
```csharp
try
{
    HOperatorSet.AffineTransPoint2d(...);
}
catch
{
    // fallback to identity (analog VisionAlgorithmService L43-58 의 catch 와 동일)
}
```
- `try/catch → return false` 또는 `try/catch → fallback` 둘 다 허용
- `out error` 채우고 `return false` 가 표준 (PointToLine L72-73)

### Pattern C: Allman vs K&R brace style (per-file)

| File | Style | Source confirmation |
|------|-------|---------------------|
| `EdgeToLineDistanceMeasurement.cs` (NEW) | **Allman** | analog PointToLineDistance L9, L16, L18 |
| `MeasurementFactory.cs` (MOD) | **Allman** | self L11, L14 |
| `Action_FAIMeasurement.cs` (MOD) | **K&R** | self L223 `private HImage ... {` same-line brace |
| `VisionAlgorithmService.cs` (MOD) | **Allman** | self L20, L27, L31 |
| `DatumMeasurement.csproj` (MOD) | XML | (N/A) |

**CLAUDE.md:** "Use the style of the file/module you are editing; do not mix within one file"

### Pattern D: PropertyGrid ComboBox ItemsSource (Phase 15 WR-RT-02 패턴)

**Source:** `EdgeOptionLists.cs` L13 (Directions) + L22 (Selections) + `DatumConfig.cs` L129/L165/L205/L254 (Selection 사용 사례 4건)
**Apply to:** `EdgeToLineDistanceMeasurement` 의 EdgeDirection/Polarity/Selection 노출
**Pattern:**
```csharp
[ItemsSourceProperty(nameof(EdgeSelectionList))]
public string EdgeSelection { get; set; } = "First";

[PropertyTools.DataAnnotations.Browsable(false)]
public List<string> EdgeSelectionList { get { return EdgeOptionLists.Selections; } }
```
**중요:** `Browsable(false)` 가 없으면 PropertyGrid 에 `List<string>` 자체가 노출됨. Phase 15 가 추가한 가드.

### Pattern E: csproj `<Compile Include>` 등록 (Pitfall 5 재발 방지)

**Source:** `DatumMeasurement.csproj` L218-222
**Apply to:** 모든 신규 .cs 파일
**Pattern:** `<Compile Include="<path>\<File>.cs" />` 1줄을 인접 Compile 그룹 끝에 추가
**Symptom if missed:** CS0246 (type or namespace not found) at build time

### Pattern F: TryFitLine 호출자 패턴 (변경 영향 분석)

**Source:** 5 caller 위치 (Grep 결과):
- `Measurements/PointToLineDistanceMeasurement.cs:66, 79`
- `Measurements/PointToPointDistanceMeasurement.cs:65, 78`
- `Measurements/LineToLineAngleMeasurement.cs:65, 76`
- `Measurements/LineToLineDistanceMeasurement.cs:64, 75`

**Apply to:** TryFitLine 시그니처 확장 시 호환 검증
**Guarantee:** `string selection = "all"` default param 으로 추가 시 5 caller 전부 무수정 호환 (C# 7.2 default param 표준 거동, RESEARCH Assumption A4).

---

## No Analog Found

(없음 — 모든 신규/수정 파일에 정확한 patron 존재)

---

## Metadata

**Analog search scope:**
- `WPF_Example/Custom/Sequence/Inspection/Measurements/` (전체 5 파일)
- `WPF_Example/Custom/Sequence/Inspection/MeasurementFactory.cs`
- `WPF_Example/Custom/Sequence/Inspection/MeasurementBase.cs`
- `WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs` (L200-244)
- `WPF_Example/Halcon/Algorithms/VisionAlgorithmService.cs` (전체)
- `WPF_Example/Custom/Sequence/Inspection/EdgeOptionLists.cs`
- `WPF_Example/DatumMeasurement.csproj` (Compile ItemGroup)

**Files scanned:** 8
**Pattern extraction date:** 2026-05-11
**Project conventions verified:**
- CLAUDE.md (style, error handling, .NET 4.8 / C# 7.2)
- memory/feedback_comment_convention.md (`//260511 hbk Phase 23 ALG-01` 마커)
- memory/feedback_halcon_measurepos_must_haves.md (measurePhi + EdgeSelection 명시)
