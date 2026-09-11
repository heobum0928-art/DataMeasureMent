# Phase 76: SIDE Datum 세로선 끄기 옵션 - Pattern Map

**Mapped:** 2026-09-11
**Files analyzed (all MODIFIED, none created):** 4
**Analogs found:** 4 / 4 (all analogs are in the SAME files being modified — this phase adds sibling code next to existing precedent, it does not import patterns from a different subsystem)

**Hard constraint reminder (CLAUDE.md):** No `.csproj` staging → **no new `.cs` files**. All work goes into the 4 files below. C# 7.2 only. No ternary `?:` / `??` / `?.` / C#8 switch expressions / dated `hbk` comments in new lines. Braces mandatory even for one-liners. Hungarian-ish local names already used inconsistently in this codebase (`hv`/`b`/`n`/`sz`/`d` prefixes are the house style per CLAUDE.md, though existing files don't rigidly follow it — match the surrounding file's own style, not a global ideal).

## File Classification

| File to Modify | Role | Data Flow | Closest Analog (same file, existing case) | Match Quality | Brace Style |
|---|---|---|---|---|---|
| `WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs` | model (config/param, PropertyGrid-bound) | CRUD (INI load/save + UI edit) | `MirrorX`/`MirrorY` (bool property + PropertyChanged) at lines 310-332; `IsHiddenForAlgorithm` switch at lines 1277-1313 | exact | Allman (K&R for some older regions — this file is mixed; the sections you touch are Allman-ish with `{` sometimes same-line for short blocks — match the immediate neighboring property) |
| `WPF_Example/Halcon/Algorithms/DatumFindingService.cs` | service (algorithm/detection) | transform (image → geometry → HomMat2d) | `TryFindVerticalTwoHorizontalDualImage` itself, lines 625-818 (entry-point branch target); `TryFindCircleTwoHorizontal` `AlignPreTransform` usage, lines 259-272 (origin-mapping analog) | exact | Allman |
| `WPF_Example/Halcon/Display/HalconDisplayService.cs` | service (rendering/overlay) | transform (config → HALCON draw calls) | `RenderDatumFindResult`, lines 394-485, specifically the "수직 기준선" block at 450-470 | exact | Allman |
| `WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs` | sequence action (orchestration) | event-driven / CRUD | `BuildDatumCaptureSnapshot`, lines 1389-1400 (`DetectedRefAngle2 != 0.0` sentinel gate) | exact — **no code change expected here**, verify only | Allman |

No files have "no analog" — every touch point sits directly beside an established precedent in the same file.

## Pattern Assignments

### `WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs` (model, CRUD)

**New bool flag — analog is `MirrorX`/`MirrorY`** (lines 306-332):

```csharp
// Source: DatumConfig.cs:306-318 (MirrorX) — copy this shape for the new flag,
// but the new flag does NOT need a user-facing warning dialog (WarnMirrorChanged
// exists because MirrorX changes camera-wide behavior; the new flag is purely
// per-Datum algorithm behavior — a plain auto-property is closer to IsPatternAlignEnabled).
private bool _mirrorX;

[Category("Datum|Mirror")]
[System.ComponentModel.Description("카메라가 사진을 좌우로 뒤집어 찍게 한다. 기본 꺼짐. 다음 촬영부터 바로 적용된다.")]
public bool MirrorX {
    get { return _mirrorX; }
    set {
        if (_mirrorX == value) return; // 같은 값 재저장 시 경고 반복 방지
        _mirrorX = value;
        RaisePropertyChanged(nameof(MirrorX));
        WarnMirrorChanged("좌우 반전(MirrorX)", value);
    }
}
```

**Simpler analog for a flag with NO warning dialog — `IsPatternAlignEnabled`** (line 134, auto-property, no backing field, no RaisePropertyChanged needed since PropertyGrid round-trips via reflection SetValue):

```csharp
// Source: DatumConfig.cs:134
public bool IsPatternAlignEnabled { get; set; } = false;
```

Comment precedent for why bool INI absence is safe (copy this comment style, do not re-derive):
```csharp
// Source: DatumConfig.cs:1217
// IsPatternAlignEnabled 는 bool — INI 키 미존재 시 자동 false(D-11). 별도 폴백 불필요.
```
→ RESEARCH.md §8 confirms this is structurally guaranteed (`Ini.cs:953-960` → `IniValue.ToBool(false)` → `ParamBase.cs:396-399`), so **no `Load()` override needed** for the new flag — unlike `ZIndexA/B` (Int32, which DOES need a `Load()` fallback, see lines ~1318+, not applicable here).

**`[Category("Datum|Algorithm")]` placement precedent** — RESEARCH.md §8 recommends this category (shared with `ExpectedAngleDeg`/`AngleTolerance`/`TwoLineAngleToleranceDeg`). Grep confirms `TwoLineAngleToleranceDeg` category; follow same `[Category]` string literal exactly (case/pipe format) used elsewhere in this file for consistency — do not invent a new category string.

**Algorithm-conditional visibility — `IsHiddenForAlgorithm`** (lines 1277-1313), the switch itself:

```csharp
// Source: DatumConfig.cs:1277-1313 (abbreviated — full switch has 4 cases: TwoLineIntersect,
// CircleTwoHorizontal, VerticalTwoHorizontal, VerticalTwoHorizontalDualImage)
private static bool IsHiddenForAlgorithm(string name, EDatumAlgorithm alg) {
    if (name == "TwoLineAngleToleranceDeg") return true; // 모든 알고리즘에서 PropertyGrid 숨김
    ...
    switch (alg) {
        case EDatumAlgorithm.TwoLineIntersect:
            if (name == "ExpectedAngleDeg" || name == "AngleTolerance") return true; // DualImage 전용 필드 hide
            ...
            return false;
        case EDatumAlgorithm.CircleTwoHorizontal:
            if (name == "ExpectedAngleDeg" || name == "AngleTolerance") return true;
            ...
            return false;
        case EDatumAlgorithm.VerticalTwoHorizontal:
            if (name == "ExpectedAngleDeg" || name == "AngleTolerance") return true;
            ...
            return false;
        case EDatumAlgorithm.VerticalTwoHorizontalDualImage: // hide 조건 없음 → default fallthrough 후 return false → 노출됨
            if (name.StartsWith("Line1_") || name.StartsWith("Line1Detected_")) return true;
            ...
            return false;
    }
    return false;
}
```

**Required edit:** add `if (name == "<NewFlagName>") return true;` to the **first three** `case` blocks only (`TwoLineIntersect`, `CircleTwoHorizontal`, `VerticalTwoHorizontal`) — mirror exactly how `ExpectedAngleDeg`/`AngleTolerance` are hidden in those three cases but NOT in `VerticalTwoHorizontalDualImage`. Do **not** add anything to the `VerticalTwoHorizontalDualImage` case — its absence of a hide rule is what makes the field visible there (fallthrough-to-`return false` pattern, exactly as documented in the comment above the switch at line 1306).

---

### `WPF_Example/Halcon/Algorithms/DatumFindingService.cs` (service, transform)

**Branch insertion point** — entry of `TryFindVerticalTwoHorizontalDualImage` (line 625). RESEARCH.md §10 gives the exact recommended shape:

```csharp
// Source: DatumFindingService.cs:625-654 (existing signature + current first block, for reference —
// DO NOT modify these existing lines; insert the new branch as the very first statement inside the try,
// or immediately after `error = null; HOperatorSet.HomMat2dIdentity(out transform);`)
private bool TryFindVerticalTwoHorizontalDualImage(HImage imageHorizontal, HImage imageVertical, DatumConfig config, out HTuple transform, out string error)
{
    error = null;
    HOperatorSet.HomMat2dIdentity(out transform);

    // NEW: if (config.<NewFlagName>) { return TryFindVerticalTwoHorizontalDualImage_HorizontalOnly(imageHorizontal, config, out transform, out error); }

    HObject contour = null;
    try
    {
        ...
        // Vertical 라인 검출 — imageVertical 사용 (D-34-01)
        if (!TryFindLine(imageVertical, ...))
        {
            error = "Vertical: " + lineError;
            return false;
        }
        ...
```

**Origin-mapping analog — `AlignPreTransform` applied via `AffineTransPoint2d`**, from `TryFindCircleTwoHorizontal` (lines 259-272), this is the EXACT pattern to copy for mapping `RefOriginRow/Col` through the pattern-match transform:

```csharp
// Source: DatumFindingService.cs:259-272 (TryFindCircleTwoHorizontal)
//260618 hbk Phase 54 ALIGN-01 (사용자 설계): 패턴 보정 transform 을 원 ROI 중심에 적용 → 틀어진 부품에서도 원을 덮음.
double circRoiRow = config.CircleROI_Row;
double circRoiCol = config.CircleROI_Col;
if (AlignPreTransform != null && AlignPreTransform.Length > 0)
{
    try
    {
        HTuple acR, acC;
        HOperatorSet.AffineTransPoint2d(AlignPreTransform, circRoiRow, circRoiCol, out acR, out acC);
        circRoiRow = acR.D;
        circRoiCol = acC.D;
    }
    catch { /* 변환 실패 시 원본 ROI 중심 유지 */ }
}
```
Note: this analog uses a bare `catch { }` (swallow) around the HALCON call, consistent with CLAUDE.md's "non-critical cleanup / transform fallback → bare catch acceptable" allowance for `AffineTransPoint2d` transform mapping specifically (not a general license — do not extend bare-catch to new logic elsewhere).

**Existing core pattern to reuse for the new H-only branch** — horizontal A+B edge extraction + line fit + `curAngle` (lines 659-716, 737), copy verbatim (these calls do not change):

```csharp
// Source: DatumFindingService.cs:659-716 (Horizontal_A / Horizontal_B extraction + FitLineContourXld) — reuse unchanged
// Source: DatumFindingService.cs:737
double curAngle = Math.Atan2(hrE.D - hrB.D, hcE.D - hcB.D);
```

**Existing transient-field write pattern** to replicate in the new H-only method (lines 748-766) — note the `hom_mat2d` build order (`Translate` then `Rotate` about `curRow/curCol`, NOT the origin) must stay consistent:

```csharp
// Source: DatumFindingService.cs:748-766
double dRow = curRow.D - config.RefOriginRow;
double dCol = curCol.D - config.RefOriginCol;
double dAngle = curAngle - config.RefAngleRad;
HTuple mat;
HOperatorSet.HomMat2dIdentity(out mat);
HOperatorSet.HomMat2dTranslate(mat, dRow, dCol, out mat);
HOperatorSet.HomMat2dRotate(mat, dAngle, curRow.D, curCol.D, out transform);

config.DetectedOriginRow = curRow.D;
config.DetectedOriginCol = curCol.D;
config.DetectedRefAngle  = curAngle;
config.DetectedRefAngle2 = vertPhiDetected;   // NEW H-only branch: set this to 0.0 instead (sentinel "unset", per RESEARCH.md §4/§5)
...
config.DetectedAngleDeg  = curAngle * 180.0 / System.Math.PI;
```

**Angle-tolerance gate to reuse unchanged** (lines 768-784) — copy into the H-only branch as-is, it only reads `curAngle`, not the vertical line:
```csharp
// Source: DatumFindingService.cs:772-781
if (config.AngleTolerance > 0.0)
{
    double diff = config.DetectedAngleDeg - config.ExpectedAngleDeg;
    diff = ((diff + 540.0) % 360.0) - 180.0;
    double absDiff = System.Math.Abs(diff);
    if (absDiff <= config.AngleTolerance)
        config.AngleValidationStatus = EAngleValidationStatus.Pass;
    else
        config.AngleValidationStatus = EAngleValidationStatus.Fail;
}
```
**CLAUDE.md violation warning:** this existing `if / else` (lines 777-780) has single-statement bodies **without braces**. That is a pre-existing hard-rule violation — do NOT copy the brace-less style into new code; the executor must add braces `{ }` around every branch it writes, even single statements, even when copying this block's logic.

**Error handling pattern** (whole method wrapped) — outer `try { ... } catch (Exception ex) { error = ex.Message; return false; } finally { contour?.Dispose()-equivalent }` — verify actual finally/dispose block beyond line 818 before writing the new private method (not yet read in full; RESEARCH.md confirms method spans to line 818). Executor must Read lines 785-825 directly before implementing, and must NOT use `contour?.Dispose()` (banned `?.`) — use explicit `if (contour != null) { try { contour.Dispose(); } catch { } }` per CLAUDE.md HImage/HObject dispose rule.

---

### `WPF_Example/Halcon/Display/HalconDisplayService.cs` (display service)

**Gate target — "수직 기준선" block**, lines 450-470, inside `RenderDatumFindResult`:

```csharp
// Source: HalconDisplayService.cs:450-470 (current — draws regardless of DetectedRefAngle2 value,
// because Sin(0)=0, Cos(0)=1 still yields a valid unit vector — this is Pitfall 1 from RESEARCH.md)
double vDirRow, vDirCol;
if (datum.DetectedCircleRow != 0.0 || datum.DetectedCircleCol != 0.0)
{
    vDirRow = datum.DetectedCircleRow - datum.DetectedOriginRow;
    vDirCol = datum.DetectedCircleCol - datum.DetectedOriginCol;
}
else
{
    vDirRow = System.Math.Sin(datum.DetectedRefAngle2);
    vDirCol = System.Math.Cos(datum.DetectedRefAngle2);
}
double vDirLen = System.Math.Sqrt(vDirRow * vDirRow + vDirCol * vDirCol);
if (vDirLen > 1e-6)
{
    double vur = vDirRow / vDirLen, vuc = vDirCol / vDirLen;
    HOperatorSet.DispLine(window,
        datum.DetectedOriginRow - datumLineHalf * vur, datum.DetectedOriginCol - datumLineHalf * vuc,
        datum.DetectedOriginRow + datumLineHalf * vur, datum.DetectedOriginCol + datumLineHalf * vuc);
}
```

**Required edit:** wrap this whole block in `if (!datum.<NewFlagName>) { ... }` (i.e., skip entirely when the new flag is on) — do NOT rely on `DetectedRefAngle2` value alone (confirmed unsafe by RESEARCH.md §4/§9/Pitfall-1). This is a **new `if` added around existing code**, not a modification of the existing branching — preserves byte-identical behavior when the flag is false, matching the "new `if` wrapper, zero edits to existing conditions" design mandated in RESEARCH.md §10.

**Method-level entry guard analog** (top of method, line 396, for style — how this file already does early-return guards):
```csharp
// Source: HalconDisplayService.cs:396
if (window == null || datum == null) return;
```
Note: this is a pre-existing multi-condition `||` without braces on a single line — CLAUDE.md discourages long `&&`/`||` chains (3+) but this is only 2 conditions on a guard clause, which is the established idiom in this file; do not flag this one, but do NOT extend the pattern to 3+ conditions in new code — extract a named `bool` instead per CLAUDE.md.

**Whole-method error suppression precedent** (lines 481-484) — existing bare `catch` at method scope, consistent with CLAUDE.md's allowance for display/overlay rendering:
```csharp
// Source: HalconDisplayService.cs:481-484
catch
{
    // Suppress display errors (기존 RenderDatumOverlay / RenderCircleDraft catch 관습 유지)
}
```
No change needed here — the new `if` guard sits inside the existing `try`, so it's automatically covered by this catch.

---

### `WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs` (sequence action) — verification only, no code change expected

**Existing sentinel gate that already protects capture-render output** (lines 1389-1400):

```csharp
// Source: Action_FAIMeasurement.cs:1389-1400
if (dc.LastFindSucceeded && (dc.DetectedOriginRow != 0.0 || dc.DetectedOriginCol != 0.0)) { // 검출 원점 십자
    cap.HasOrigin = true;
    cap.OriginRow = dc.DetectedOriginRow;
    cap.OriginCol = dc.DetectedOriginCol;
    // 검출 기준선(축). 1차=DetectedRefAngle(각도 0 도 유효), 2차=DetectedRefAngle2(0 이면 단일축 datum → 미표시).
    cap.HasAxis1 = true;
    cap.Axis1AngleRad = dc.DetectedRefAngle;
    if (dc.DetectedRefAngle2 != 0.0) {
        cap.HasAxis2 = true;
        cap.Axis2AngleRad = dc.DetectedRefAngle2;
    }
}
```

This block already uses `DetectedRefAngle2 != 0.0` as an "axis-2 configured" sentinel and its own comment (line 1393) documents that convention explicitly. **If the H-only branch in `DatumFindingService.cs` sets `DetectedRefAngle2 = 0.0` (as recommended, RESEARCH.md §4/§5), this file needs NO edit** — the sentinel already does the right thing. Only touch this file if the planner instead chooses a non-zero `DetectedRefAngle2` value, in which case this gate must be revisited (not recommended — breaks D-76-06 per RESEARCH.md §4 table).

**No hard-rule violations found in this excerpt** — braces present on all branches, no ternary/`?.`/`??`.

## Shared Patterns

### `AlignPreTransform` → `AffineTransPoint2d` origin remap
**Source:** `DatumFindingService.cs:259-272` (`TryFindCircleTwoHorizontal`)
**Apply to:** the new H-only private method in the same file, for mapping `config.RefOriginRow/Col` through the pattern-match transform (D-76-05 origin X calculation, RESEARCH.md §5).

### Bool option field on `DatumConfig` + INI hidden-key-safe default
**Source:** `DatumConfig.cs:134` (`IsPatternAlignEnabled`, simple form) or `:306-332` (`MirrorX`, form with change-warning — not needed here)
**Apply to:** new flag property; no `Load()` override needed (verified structurally safe for bool, unlike Int32 fields such as `ZIndexA/B`).

### Algorithm-conditional PropertyGrid visibility
**Source:** `DatumConfig.cs:1277-1313` `IsHiddenForAlgorithm`
**Apply to:** new flag — add hide rule to the 3 non-VTH-DualImage `case` blocks only.

### "New `if` wrapper around existing code, zero edits to existing lines" regression-safety pattern
**Source:** RESEARCH.md §10 design principle, demonstrated at `DatumFindingService.cs:625` (branch insertion) and to be applied at `HalconDisplayService.cs:450` (display gate) and `DatumConfig.cs:1277` (hide-switch case additions).
**Apply to:** all 3 code-change files. This is the core regression-avoidance strategy for the whole phase — every edit must be an added `if`, never a modification of an existing condition or existing line.

### CLAUDE.md violations already present in analog code (do NOT propagate into new lines)
- `DatumFindingService.cs:777-780` — brace-less `if/else` single statements. New code must add braces.
- `HalconDisplayService.cs:396` — 2-condition `||` guard clause without braces (acceptable as guard-clause idiom in this file, but don't extend to 3+ conditions).
- Multiple `hbk`-dated comments throughout both files (e.g. `//260619 hbk Phase 56...`) — these are historical/pre-policy-change and must NOT be imitated; new comments must not use the `//YYMMDD hbk` format (CLAUDE.md 2026-06-11 policy change, explicitly re-stated in 76-CONTEXT.md `## 제약`).
- No ternary/`??`/`?.` found in any of the excerpted ranges — clean on that front.

## No Analog Found

None — every touch point has a directly adjacent, same-file precedent (see table above). This phase is structurally "add a sibling branch/case next to an existing one," which is the best possible analog situation.

## Metadata

**Analog search scope:** `WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs`, `WPF_Example/Halcon/Algorithms/DatumFindingService.cs`, `WPF_Example/Halcon/Display/HalconDisplayService.cs`, `WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs` — all read directly (targeted ranges, non-overlapping) rather than searched broadly, because 76-RESEARCH.md already identified the exact touch points with high confidence (HIGH, code-verified) and named the analogs; this pass re-read those exact locations to extract verbatim excerpts and check for hard-rule violations.
**Files scanned:** 4 (all read via targeted Read calls at the line ranges cited above)
**Pattern extraction date:** 2026-09-11
