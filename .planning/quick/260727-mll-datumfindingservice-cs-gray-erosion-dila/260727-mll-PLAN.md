---
phase: 260727-mll
plan: 01
type: execute
wave: 1
depends_on: []
files_modified:
  - WPF_Example/Halcon/Algorithms/DatumFindingService.cs
autonomous: true
requirements:
  - FIX-ErosionClamp-TryFindLine
  - FIX-ErosionClamp-TryExtractEdgePoints

must_haves:
  truths:
    - "The directional structuring element's half-length along the edge (halfLen, fed to HOperatorSet.GenRectangle2) can never exceed the ROI's own half-extent along that same edge direction, at BOTH call sites - so a large Erosion(px) can no longer bleed the smoothing past a corner into an adjacent, differently-oriented edge."
    - "The ROI half-extent is taken from values already computed earlier in the same function (scanHorizontal + the ROI AABB half-extents); no ROI geometry is recomputed or duplicated."
    - "When erosion/2.0 <= the ROI half-extent (the normal, well-sized case), halfLen is exactly erosion/2.0 as before - zero behavior change for existing recipes."
    - "pad is computed AFTER the clamped halfLen and derived from it, is never smaller than halfLen + halfWidth (the rotated-SE worst-case radius), and in the unclamped case is never smaller than today's pad = erosion - so the ReduceDomain domain can never shrink below what the SE needs."
    - "erosion <= 0 remains a complete no-op - the whole if (erosion > 0) block is skipped and zero HOperatorSet calls occur."
    - "const double halfWidth = 0.5 (the across-edge half-width) is unchanged at both sites - this task touches only the along-edge length and the domain padding."
    - "The polarity-aware operator selection from quick task 260727-jna (useDilation / opName / GrayDilation vs GrayErosion) is untouched at both sites."
    - "MSBuild Debug/x64 //t:Build of WPF_Example/DatumMeasurement.csproj exits 0 with no CS diagnostic referencing DatumFindingService.cs."
    - "Exactly one file is modified."
  artifacts:
    - path: "WPF_Example/Halcon/Algorithms/DatumFindingService.cs"
      provides: "ROI-bounded directional gray_erosion/gray_dilation structuring-element length at both strip-loop entry points"
      contains: "roiHalfExtentAlongEdge"
  key_links:
    - from: "scanHorizontal + halfH/halfW (TryFindLine) / halfRow/halfCol (TryExtractEdgePoints)"
      to: "roiHalfExtentAlongEdge"
      via: "explicit if/else (no ternary), reusing values already computed above the erosion block"
      pattern: "double roiHalfExtentAlongEdge;"
    - from: "roiHalfExtentAlongEdge"
      to: "halfLen fed to HOperatorSet.GenRectangle2(out seRegion, centerRC, centerRC, lineAxisPhi, halfLen, halfWidth)"
      via: "explicit upper-bound if statement"
      pattern: "if \\(halfLen > roiHalfExtentAlongEdge\\) halfLen = roiHalfExtentAlongEdge;"
    - from: "clamped halfLen"
      to: "pad -> HOperatorSet.GenRectangle1 / ReduceDomain"
      via: "pad computed after halfLen instead of before it"
      pattern: "double pad = halfLen \\* 2\\.0 \\+ 1\\.0;"
---

<objective>
Bound the directional `gray_erosion`/`gray_dilation` structuring element to the ROI that owns it, in `WPF_Example/Halcon/Algorithms/DatumFindingService.cs`.

**Bug (already diagnosed live with the user, confirmed on before/after zoomed screenshots - do NOT re-investigate):** inside the `if (erosion > 0)` pre-processing block the SE's along-edge half-length is `double halfLen = erosion / 2.0;` - completely unbounded by the ROI's own size. `erosion` comes straight from a user-editable PropertyGrid field (`Vertical_Erosion`, `Horizontal_A_Erosion`, ... in `DatumConfig.cs`) with no enforced relationship to ROI geometry. With `Erosion(px) = 201` (confirmed case -> `halfLen = 100.5px`) on a ROI sitting near a corner where the edge changes direction, the SE reaches past the corner and smears the adjacent, differently-oriented edge as well: the corner nub visibly disappeared on both the vertical and the horizontal portion, when the user's intent was to affect the vertical portion only.

**Fix:** clamp `halfLen` to the ROI's own half-extent measured *along the edge direction*, and derive the `ReduceDomain` padding from that clamped value instead of from raw `erosion`. The ROI is the natural boundary: the user draws it to cover exactly the "pure" straight segment of the edge, so the ROI itself defines how far the smoothing is allowed to reach.

Purpose: make a large `Erosion(px)` safe to use next to a corner - the value can be raised as high as the operator likes without ever affecting a neighbouring edge of different orientation.
Output: 1 modified C# file, 2 mirrored call sites fixed, clean Debug/x64 build.
</objective>

<execution_context>
@$HOME/.claude/get-shit-done/workflows/execute-plan.md
@$HOME/.claude/get-shit-done/templates/summary.md
</execution_context>

<context>
@./CLAUDE.md
@.planning/quick/260727-jna-datumfindingservice-cs-erosion-polarity-/260727-jna-SUMMARY.md

<verified_during_planning>
<!-- Every item below was confirmed by direct inspection of the current working tree during planning. -->
<!-- Do NOT re-investigate any of it. Line numbers are current as of planning; re-locate by content anyway. -->

1. **The two blocks are BYTE-IDENTICAL twins.** `diff` of lines 1704-1765 (inside `TryFindLine`) against lines 1955-2016 (inside `TryExtractEdgePoints`) returns zero differences (the only diff in the compared ranges is one trailing blank line outside the block). Same comments, same variable names, same format strings. **Consequence: any `old_string` taken from inside the block matches 2 sites and the Edit tool will refuse it.** Disambiguation is spelled out in Task 1.

2. **CRITICAL CORRECTION to the bug brief - the two functions use DIFFERENT names for the ROI half-extents:**

   | Function | Column (horizontal) half-extent | Row (vertical) half-extent | Declared at |
   |---|---|---|---|
   | `TryFindLine` | `halfW` | `halfH` | lines 1655-1656 |
   | `TryExtractEdgePoints` | `halfCol` | `halfRow` | lines 1920-1921 |

   `halfH` / `halfW` **do not exist** in `TryExtractEdgePoints`. Using them there is a guaranteed CS0103. Both pairs are computed identically (`Math.Abs(roiLength1 * cosT) + Math.Abs(roiLength2 * sinT)` for the column one, the sin/cos-swapped form for the row one) and both feed `top/bottom/left/right` the same way.

3. **`scanHorizontal` -> which half-extent (mapping verified two independent ways).** `bool scanHorizontal = (direction != "TtoB" && direction != "BtoT");` (line 1665 / 1930).
   - `scanHorizontal == true` (LtoR/RtoL): the strip loop slices by **row** (`r1 = top + i*heightPx/stripCount`, full width `left..right`), the scan runs across columns, so the measured edge runs along the **row** axis -> the ROI's extent along the edge is its row extent -> **`halfH` (site 1) / `halfRow` (site 2)**.
   - `scanHorizontal == false` (TtoB/BtoT): strips are column slices, the edge runs along the **column** axis -> **`halfW` (site 1) / `halfCol` (site 2)**.
   - Cross-check via the SE angle: `lineAxisPhi = ComputeMeasurePhi(direction, alignRot) + PI/2`. For LtoR, `measurePhi = 0` -> `lineAxisPhi = PI/2` -> `GenRectangle2`'s `length1 = halfLen` axis is vertical -> the SE extends +/-halfLen along rows. Consistent.

4. **All three inputs are in scope well before the `if (erosion > 0)` block** at both sites (`scanHorizontal` at 1665/1930; the half-extents at 1655-1656/1920-1921; the block starts at 1712/1963). Nothing new needs to be computed.

5. **Under align (`alignRot != 0`) the half-extents are the enlarged AABB half-extents**, i.e. slightly *larger* than the true rotated-ROI half-extents. That makes the clamp marginally loose under align, never tighter than the real ROI - acceptable and intentional (align rotations here are ~0.1-0.2 deg). Do not "improve" this.

6. **Exact indentation (verified with `cat -A`; file is LF, spaces only, no tabs):**
   - `if (erosion > 0)` and its `{` -> **12 spaces**
   - statements directly inside that `if` (`var erosionSw`, `bool useDilation`, `string opName`, `try`, `catch`) -> **16 spaces**
   - statements inside `try { }` (`double pad`, `HOperatorSet.*`, `double halfLen`, `const double halfWidth`, ...) -> **20 spaces**
   - the comment lines *above* `if (erosion > 0)` (the `HObject roiDomain = null;` declarations and the long `read_gray_se` comment) -> **12 spaces**

   > The 260727-jna executor lost a cycle here: it retyped a plan code block whose comment lines *looked* like they were at 16 spaces when the file has them at 12. **Copy every unchanged line of `old_string` from a fresh `Read`, never from this plan's rendered code fences.**

7. **Current state of the region to change (identical at both sites, `try`-body at 20 spaces):**
```
                    double pad = erosion; // 회전 마스크 worst-case 반경(erosion/2+0.5) 도 이 여유 안에 들어옴 → 값 유지
                    HOperatorSet.GenRectangle1(out roiDomain, top - pad, left - pad, bottom + pad, right + pad);
                    HOperatorSet.ReduceDomain(image, roiDomain, out reducedImage);

                    double measurePhi  = ComputeMeasurePhi(direction, alignRot);
                    double lineAxisPhi = measurePhi + Math.PI / 2.0; // 라인(에지) 방향 = 스캔방향(measurePhi) + 90°
                    double halfLen     = erosion / 2.0; // 라인 방향 반길이(구 gray_erosion_rect(erosion,erosion) 의 "전체 크기=erosion" 의미 보존)
                    const double halfWidth = 0.5;        // 에지 횡단 방향 반폭 고정(~1px, 비노출) — 에지 블러 방지가 이 기능의 존재 이유
```
   Note the blank line between `ReduceDomain` and `measurePhi`, and that a further blank line + `HTuple imgType;` follow the `halfWidth` line (that trailing blank line stays *outside* the edit).

8. **`pad` semantics (why the reorder is required).** `pad` currently sits *above* `halfLen`, so it cannot see the clamped value. It sizes the `GenRectangle1`/`ReduceDomain` domain so every pixel inside the ROI bbox has full SE support. The rotated-SE worst-case reach is `halfLen + halfWidth`; today `pad = erosion = halfLen * 2` covers it. After the clamp, `pad = halfLen * 2.0 + 1.0` keeps that guarantee for *any* halfLen (including a degenerate sub-pixel ROI) **and** is never smaller than today's `pad` when no clamping happens - the domain can only ever grow relative to current behavior in the unaffected case, so there is no regression path. Extra domain outside the bbox never changes a measured value: the strip loop only samples inside `top..bottom` / `left..right`.

9. **Build verification (proven working in 260727-jna, use verbatim):** `//t:Build` on the **.csproj** (not the .sln, not `//t:Compile`). `//t:Compile` bypasses `MarkupCompilePass1/2` in this project and produces ~1272 false-positive CS0103/CS1061 errors in unrelated XAML-backed files. MSBuild lives at `C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/MSBuild.exe`.

10. **Known pre-existing warning baseline (6, none in this file):** CS0618 x5 (`Sequence_Top.cs:19`, `Sequence_Bottom.cs:30`, `SequenceHandler.cs:69/71/73`) + CS0162 x1 (`VirtualCamera.cs:237`). Anything mentioning `DatumFindingService.cs` is new and is a failure.

11. **`DatumMeasurement.exe` was NOT running at planning time** (`tasklist` confirmed), so `//t:Build` completes a full compile-and-copy. If it is running when you execute, the build emits `MSB3027`/`MSB3021` output-copy errors - those are file-lock artifacts, **not** compile failures. Policy (same as 260727-jna): ask the user to close the app, or fall back to careful manual code-read verification (braces balanced, both if/else branches valid C# 7.2, every identifier present in ambient scope) plus the structural greps, and say so explicitly in the SUMMARY.

12. **`WPF_Example/DatumMeasurement.csproj` has a pre-existing, unrelated working-tree modification** (local `SIMUL_MODE` DefineConstants toggle). Leave it alone and exclude it from every commit.

13. **No project skills directory exists** (`.claude/skills/` and `.agents/skills/` both absent) - only `./CLAUDE.md` conventions apply.
</verified_during_planning>

<style_rules>
From `./CLAUDE.md` + this file's local convention + user standing rules:
- **No ternary `?:`** - use explicit `if` / `else`. (Standing rule, applies to every edit in this plan.)
- Single-line `if` without braces is the correct local idiom for these short guards - mirror lines 1667-1668 / 1674-1675 of this same file (`int stripCount = 20; if (sampleCount > 0) stripCount = sampleCount;`).
- Allman braces for multi-line blocks (this file's style). Do not restyle anything you are not changing.
- C# 7.2 / .NET Framework 4.8 - no C# 8+ syntax.
- **No `//YYMMDD hbk` date prefix on new comments.** That convention was retired 2026-06-11. Write only the non-obvious "why".
- Korean comments, matching the surrounding block.
- Do not touch any other file.
</style_rules>
</context>

<tasks>

<task type="auto">
  <name>Task 1: Clamp the SE length to the ROI extent in TryFindLine</name>
  <files>WPF_Example/Halcon/Algorithms/DatumFindingService.cs</files>
  <action>
In `TryFindLine` (starts ~line 1605), inside the `if (erosion > 0)` block (~line 1712), replace the 8-line region shown in `<verified_during_planning>` item 7 with the clamped, reordered version below.

**Step 1 - Read first.** `Read` the file range ~1700-1735. You need the exact text of lines ~1703-1731 including indentation.

**Step 2 - Build a disambiguated `old_string`.** The 8 lines you are replacing exist verbatim **twice** in this file (item 1). A short `old_string` WILL fail with "found 2 matches". Extend `old_string` upward to include the last line of the `read_gray_se` comment block - the 12-space-indented line ending `...sub-pixel edge 위치가 틀어짐).` (~line 1703). That line is unique to `TryFindLine`.

So: `old_string` = lines ~1703 through ~1731 (the unique comment line, the `HObject ... = null;` declarations, `if (erosion > 0) {`, the stopwatch + `useDilation`/`opName` lines from 260727-jna, `try {`, and the 8 lines being changed).
`new_string` = **the exact same lines ~1703 through ~1723 copied verbatim, unchanged**, followed by the replacement text below.

> **Copy lines 1703-1723 from the `Read` output, NOT from this plan.** They are Korean comments and jna-era code that must survive byte-for-byte; this plan deliberately does not reproduce them so you cannot mistype them.

**Step 3 - Replacement text** (this is the only text that comes from the plan; `try`-body indent = exactly 20 spaces):

```
                    double measurePhi  = ComputeMeasurePhi(direction, alignRot);
                    double lineAxisPhi = measurePhi + Math.PI / 2.0; // 라인(에지) 방향 = 스캔방향(measurePhi) + 90°
                    // Erosion(px) 는 PropertyGrid 자유 입력이라 ROI 크기와 무관하게 커질 수 있다(실측 201 → halfLen=100.5px).
                    //  SE 가 ROI 를 넘어서면 모서리 건너편의 다른 방향 에지(Vertical 옆의 Horizontal)까지 같이 뭉갠다.
                    //  사용자는 "순수한 직선 구간"만 덮도록 ROI 를 그리므로 ROI 자체가 곧 스무딩이 미쳐도 되는 범위다
                    //  → ROI 의 에지 방향 반길이로 상한을 걸어 침식/팽창이 ROI 밖으로 번지지 않게 한다.
                    double roiHalfExtentAlongEdge;
                    if (scanHorizontal) roiHalfExtentAlongEdge = halfH; // Vertical 형 ROI: 에지가 행(세로) 축 → 행 반높이
                    else roiHalfExtentAlongEdge = halfW;                // Horizontal 형 ROI: 에지가 열(가로) 축 → 열 반폭
                    double halfLen     = erosion / 2.0; // 라인 방향 반길이(구 gray_erosion_rect(erosion,erosion) 의 "전체 크기=erosion" 의미 보존)
                    if (halfLen > roiHalfExtentAlongEdge) halfLen = roiHalfExtentAlongEdge; // ROI 자체 크기로 상한
                    const double halfWidth = 0.5;        // 에지 횡단 방향 반폭 고정(~1px, 비노출) — 에지 블러 방지가 이 기능의 존재 이유

                    double pad = halfLen * 2.0 + 1.0; // 클램프된 SE 기준 도메인 여유(회전 마스크 worst-case 반경 halfLen+halfWidth 를 항상 상회, 미클램프 시엔 기존 pad=erosion 보다 항상 큼)
                    HOperatorSet.GenRectangle1(out roiDomain, top - pad, left - pad, bottom + pad, right + pad);
                    HOperatorSet.ReduceDomain(image, roiDomain, out reducedImage);
```

What changed, precisely:
- `measurePhi` / `lineAxisPhi` / `halfLen` / `halfWidth` moved **above** the `pad` + `GenRectangle1` + `ReduceDomain` group (that group is otherwise unchanged apart from the `pad` expression).
- `roiHalfExtentAlongEdge` added via explicit if/else on `scanHorizontal` (never a ternary), reusing `halfH` / `halfW` that already exist at lines 1655-1656.
- `halfLen` clamped with an explicit `if` (not `Math.Min`) so it reads like the rest of this file's sanity clamps.
- `pad = erosion` -> `pad = halfLen * 2.0 + 1.0`, now computed after (and from) the clamped `halfLen`.
- The blank line that used to sit between `ReduceDomain` and `measurePhi` now sits between `halfWidth` and `pad`. The blank line + `HTuple imgType;` that follow the old `halfWidth` line are **outside** this edit and stay put.

**MUST NOT CHANGE (verify by reading back after the edit):**
- The `if (erosion > 0)` gate - `erosion <= 0` must remain a total no-op.
- `const double halfWidth = 0.5;` - value and comment unchanged (this task never touches the across-edge width).
- The 260727-jna polarity logic: `bool useDilation = string.Equals(polarity, "negative", ...)`, `string opName = ...`, `if (useDilation) opName = ...`, and the `if (useDilation) { GrayDilation } else { GrayErosion }` block.
- The `try` / `catch (Exception erodeEx)` structure, its `stripImage = image;` fallback, both Trace log `string.Format` calls, and the `finally` disposal block.
- `GetImageType`, `canvasSize`, `centerRC`, `GenImageConst`, `GenRectangle2`, the SE `ReduceDomain` - they simply consume the now-clamped `halfLen` (`canvasSize` shrinking with it is the intended side effect).
- `TryExtractEdgePoints` - that is Task 2. If your edit accidentally hits both sites, the verify below will show it (and site 2 will not compile, because `halfH`/`halfW` do not exist there).
- Any other code, any other file.
  </action>
  <verify>
    <automated>cd "C:/Info/Project/DataMeasurement" && F=WPF_Example/Halcon/Algorithms/DatumFindingService.cs && echo "--- structural (site 1 only) ---" && echo "decl=$(grep -c 'double roiHalfExtentAlongEdge;' $F) (want 1)" && echo "assign_halfH=$(grep -c 'roiHalfExtentAlongEdge = halfH;' $F) (want 1)" && echo "assign_halfW=$(grep -c 'roiHalfExtentAlongEdge = halfW;' $F) (want 1)" && echo "clamp=$(grep -c 'if (halfLen > roiHalfExtentAlongEdge) halfLen = roiHalfExtentAlongEdge;' $F) (want 1)" && echo "old_pad_left=$(grep -c 'double pad = erosion;' $F) (want 1 - site 2 untouched)" && echo "new_pad=$(grep -c 'double pad = halfLen \* 2.0 + 1.0;' $F) (want 1)" && echo "halfWidth=$(grep -c 'const double halfWidth = 0.5;' $F) (want 2 - unchanged)" && echo "gate=$(grep -c 'if (erosion > 0)' $F) (want 2 - unchanged)" && echo "jna_dilation=$(grep -c 'HOperatorSet.GrayDilation' $F) (want 2 - unchanged)" && echo "jna_erosion=$(grep -c 'HOperatorSet.GrayErosion' $F) (want 2 - unchanged)" && echo "--- order (pad must come AFTER halfLen) ---" && paste <(grep -n 'double halfLen     = erosion / 2.0;' $F | cut -d: -f1) <(grep -n 'double pad = halfLen' $F | cut -d: -f1) | awk '{if($2>$1) print "ORDER_OK halfLen@"$1" -> pad@"$2; else print "ORDER_FAIL halfLen@"$1" -> pad@"$2}' && echo "--- build ---" && "C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/MSBuild.exe" WPF_Example/DatumMeasurement.csproj //t:Build //p:Configuration=Debug //p:Platform=x64 //v:m 2>&1 | grep -E "error|warning CS" | grep -v -E "CS0618|CS0162" | head -20; echo "MSBUILD_EXIT=${PIPESTATUS[0]}"</automated>
  </verify>
  <done>
- All ten structural counts match their `(want N)` annotation. In particular `old_pad_left=1` proves the edit hit exactly one site (0 = you edited both, 2 = the edit did not apply).
- `ORDER_OK` is printed for the single clamped site (`pad` line number greater than the `halfLen` line number).
- `MSBUILD_EXIT=0` and the filtered grep prints nothing - no CS error, no warning outside the CS0618/CS0162 baseline, nothing referencing `DatumFindingService.cs`.
- Read-back confirms: braces balanced, `roiHalfExtentAlongEdge` assigned on both if/else branches, no ternary introduced, `const double halfWidth = 0.5;` byte-identical, the jna polarity block byte-identical.
- If the exe lock blocked the build (`MSB3027`/`MSB3021`), the structural checks above plus an explicit manual read-back of the whole `if (erosion > 0)` block stand in - and this substitution is recorded for the SUMMARY.
  </done>
</task>

<task type="auto">
  <name>Task 2: Mirror the clamp into TryExtractEdgePoints and verify both sites</name>
  <files>WPF_Example/Halcon/Algorithms/DatumFindingService.cs</files>
  <action>
Apply the same transformation to the twin block inside `TryExtractEdgePoints` (function starts ~line 1873; `if (erosion > 0)` was at ~1963 before Task 1 - Task 1 added ~7 lines above it, so **re-locate by content**: search for the remaining `double pad = erosion;`, which after Task 1 is unique in the file).

**Disambiguation is free now.** Because Task 1 changed site 1, the 8-line region is unique - a plain `Edit` on just those 8 lines binds correctly. No long anchor needed.

**Replacement text - identical to Task 1 EXCEPT the two identifiers and a shortened comment** (`try`-body indent = exactly 20 spaces):

```
                    double measurePhi  = ComputeMeasurePhi(direction, alignRot);
                    double lineAxisPhi = measurePhi + Math.PI / 2.0; // 라인(에지) 방향 = 스캔방향(measurePhi) + 90°
                    // ROI 를 넘어선 SE 는 모서리 건너편의 다른 방향 에지까지 뭉갠다 → ROI 의 에지 방향 반길이로 상한(상세 주석은 TryFindLine).
                    double roiHalfExtentAlongEdge;
                    if (scanHorizontal) roiHalfExtentAlongEdge = halfRow; // Vertical 형 ROI: 에지가 행(세로) 축 → 행 반높이
                    else roiHalfExtentAlongEdge = halfCol;                // Horizontal 형 ROI: 에지가 열(가로) 축 → 열 반폭
                    double halfLen     = erosion / 2.0; // 라인 방향 반길이(구 gray_erosion_rect(erosion,erosion) 의 "전체 크기=erosion" 의미 보존)
                    if (halfLen > roiHalfExtentAlongEdge) halfLen = roiHalfExtentAlongEdge; // ROI 자체 크기로 상한
                    const double halfWidth = 0.5;        // 에지 횡단 방향 반폭 고정(~1px, 비노출) — 에지 블러 방지가 이 기능의 존재 이유

                    double pad = halfLen * 2.0 + 1.0; // 클램프된 SE 기준 도메인 여유(회전 마스크 worst-case 반경 halfLen+halfWidth 를 항상 상회, 미클램프 시엔 기존 pad=erosion 보다 항상 큼)
                    HOperatorSet.GenRectangle1(out roiDomain, top - pad, left - pad, bottom + pad, right + pad);
                    HOperatorSet.ReduceDomain(image, roiDomain, out reducedImage);
```

**`halfRow` / `halfCol` - not `halfH` / `halfW`.** This function names its AABB half-extents differently (declared at ~lines 1920-1921, shifted by Task 1). `halfH`/`halfW` do not exist here and would be a CS0103. The shorter comment matches this block's existing convention of deferring detail to `TryFindLine` (see the block comment right above the `HObject` declarations here).

Why this second site matters: `TryExtractEdgePoints` is the horizontal 2-ROI concat path. Leaving it unclamped would make the same `Erosion(px)` value behave differently depending on which ROI role a Datum is used in - exactly the asymmetry 260727-jna had to go back and fix.

**MUST NOT CHANGE:** identical restriction list to Task 1 - the `if (erosion > 0)` gate, `const double halfWidth = 0.5;`, the jna polarity block (`useDilation`/`opName`/`GrayDilation`/`GrayErosion`), the try/catch structure and `stripImage = image;` fallback, both Trace logs, the `finally` disposal, the SE construction calls, `TryFindLine` (already done), and any other code or file.
  </action>
  <verify>
    <automated>cd "C:/Info/Project/DataMeasurement" && F=WPF_Example/Halcon/Algorithms/DatumFindingService.cs && echo "--- structural (both sites) ---" && echo "decl=$(grep -c 'double roiHalfExtentAlongEdge;' $F) (want 2)" && echo "assign_halfH=$(grep -c 'roiHalfExtentAlongEdge = halfH;' $F) (want 1)" && echo "assign_halfW=$(grep -c 'roiHalfExtentAlongEdge = halfW;' $F) (want 1)" && echo "assign_halfRow=$(grep -c 'roiHalfExtentAlongEdge = halfRow;' $F) (want 1)" && echo "assign_halfCol=$(grep -c 'roiHalfExtentAlongEdge = halfCol;' $F) (want 1)" && echo "clamp=$(grep -c 'if (halfLen > roiHalfExtentAlongEdge) halfLen = roiHalfExtentAlongEdge;' $F) (want 2)" && echo "old_pad_gone=$(grep -c 'double pad = erosion;' $F) (want 0)" && echo "new_pad=$(grep -c 'double pad = halfLen \* 2.0 + 1.0;' $F) (want 2)" && echo "halfWidth=$(grep -c 'const double halfWidth = 0.5;' $F) (want 2 - unchanged)" && echo "gate=$(grep -c 'if (erosion > 0)' $F) (want 2 - unchanged)" && echo "jna_dilation=$(grep -c 'HOperatorSet.GrayDilation' $F) (want 2 - unchanged)" && echo "jna_erosion=$(grep -c 'HOperatorSet.GrayErosion' $F) (want 2 - unchanged)" && echo "no_ternary=$(grep -c 'roiHalfExtentAlongEdge = .*?.*:' $F) (want 0)" && echo "--- order (pad AFTER halfLen at BOTH sites) ---" && paste <(grep -n 'double halfLen     = erosion / 2.0;' $F | cut -d: -f1) <(grep -n 'double pad = halfLen' $F | cut -d: -f1) | awk '{if($2>$1) print "ORDER_OK halfLen@"$1" -> pad@"$2; else print "ORDER_FAIL halfLen@"$1" -> pad@"$2}' && echo "--- scope ---" && git diff --name-only && echo "--- build ---" && "C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/MSBuild.exe" WPF_Example/DatumMeasurement.csproj //t:Build //p:Configuration=Debug //p:Platform=x64 //v:m 2>&1 | grep -E "error|warning CS" | grep -v -E "CS0618|CS0162" | head -20; echo "MSBUILD_EXIT=${PIPESTATUS[0]}"</automated>
  </verify>
  <done>
- All thirteen structural counts match their `(want N)` annotation - most importantly `decl=2`, `clamp=2`, `new_pad=2`, `old_pad_gone=0`, and the four per-site identifier counts all at 1 (proving site 1 kept `halfH`/`halfW` and site 2 got `halfRow`/`halfCol`).
- `halfWidth=2`, `gate=2`, `jna_dilation=2`, `jna_erosion=2` prove the across-edge width, the `erosion <= 0` no-op gate, and the 260727-jna polarity work are all untouched.
- `ORDER_OK` printed **twice** (once per site).
- `git diff --name-only` lists only `WPF_Example/Halcon/Algorithms/DatumFindingService.cs` and the pre-existing unrelated `WPF_Example/DatumMeasurement.csproj` modification (item 12) - nothing else.
- `MSBUILD_EXIT=0` and the filtered grep prints nothing - no CS error, no warning outside the CS0618/CS0162 baseline, nothing referencing `DatumFindingService.cs`.
- Manual read-back of both blocks confirms: braces balanced, valid C# 7.2, every identifier resolves in its own function's ambient scope (`scanHorizontal`, `halfH`/`halfW` at site 1, `halfRow`/`halfCol` at site 2, `halfLen`, `halfWidth`, `pad`, `roiDomain`, `reducedImage`), and no ternary anywhere in the new code.
- If the exe lock blocked the build, the same fallback policy as Task 1 applies and is recorded in the SUMMARY.
  </done>
</task>

</tasks>

<threat_model>
## Trust Boundaries

| Boundary | Description |
|----------|-------------|
| recipe file (INI) -> vision algorithm | `Erosion(px)` is an operator-editable PropertyGrid field persisted to the INI recipe; its value sizes a HALCON structuring element and an image domain. It is the only externally-influenced input in the modified code. No network, IPC, or file-path input crosses the changed lines. |

## STRIDE Threat Register

| Threat ID | Category | Component | Disposition | Mitigation Plan |
|-----------|----------|-----------|-------------|-----------------|
| T-260727mll-01 | Denial of Service (resource exhaustion) | `erosion` -> `halfLen` -> `canvasSize` / `pad` -> `GenImageConst` + `ReduceDomain` + `GrayErosion` | mitigate | This change *is* the mitigation: `halfLen` is now bounded by the ROI's own half-extent, which transitively bounds `canvasSize` (`2*ceil(halfLen+0.5)+3`) and the padded domain. An absurd `Erosion(px)` (e.g. 20000) can no longer allocate an enormous SE canvas or dilate the reduced domain far past the ROI, so per-ROI erosion tact stays proportional to the ROI, not to the typed number. |
| T-260727mll-02 | Tampering (measurement integrity) | Silent corruption of an adjacent, differently-oriented edge by an over-long SE | mitigate | Root cause of this task. Clamping to the ROI extent makes the ROI the enforced boundary of the smoothing, so a mis-set `Erosion(px)` can degrade only the edge its own ROI covers - it can no longer silently alter a neighbouring datum edge's sub-pixel position across a corner. |
| T-260727mll-03 | Denial of Service | `GenRectangle1` with a reduced/negative-coordinate rectangle after clamping | accept | `pad` only ever shrinks in the clamped case (and grows by 1px otherwise), so the rectangle is never larger than what today's code already produces; negative coordinates near an image border already occur today and are handled by `ReduceDomain`'s intersection with the image extent. The existing `try/catch (Exception erodeEx)` still falls back to the unprocessed source image with a Trace log. No new failure mode. |
</threat_model>

<verification>
1. **Clamp present and correct at both sites:** `roiHalfExtentAlongEdge` is derived by explicit if/else from `scanHorizontal` and the function's own AABB half-extents (`halfH`/`halfW` in `TryFindLine`, `halfRow`/`halfCol` in `TryExtractEdgePoints`), and `halfLen` is bounded by it before it reaches `GenRectangle2`.
2. **Ordering:** `pad` is computed after `halfLen` at both sites (`ORDER_OK` twice) and is `halfLen * 2.0 + 1.0`, never `erosion`.
3. **Non-regression for well-sized ROIs:** when `erosion / 2.0 <= roiHalfExtentAlongEdge` the clamp is a no-op and `halfLen` is bit-identical to today; `pad` is 1px larger, which can only enlarge the reduced domain and therefore cannot change any value the strip loop samples inside the ROI bbox.
4. **No-op preservation:** the `if (erosion > 0)` gate is untouched at both sites (`gate=2`), so `erosion <= 0` still executes zero HOperatorSet calls.
5. **Untouched neighbours:** `const double halfWidth = 0.5;` (x2), the 260727-jna polarity block (`GrayDilation` x2 / `GrayErosion` x2), the try/catch/fallback, the Trace logs, and the `finally` disposal are all byte-identical.
6. **Compile:** `//t:Build` on `WPF_Example/DatumMeasurement.csproj` (Debug/x64) exits 0 with no CS diagnostic naming `DatumFindingService.cs`.
7. **Scope:** `git diff --name-only` shows only `DatumFindingService.cs` (plus the pre-existing unrelated `.csproj` working-tree change, which must not be committed).
</verification>

<success_criteria>
- At both `TryFindLine` and `TryExtractEdgePoints`, the SE half-length passed to `HOperatorSet.GenRectangle2` is `min(erosion / 2.0, ROI half-extent along the edge)` - expressed as an explicit `if`, no ternary, no `Math.Min`.
- `roiHalfExtentAlongEdge` is selected by explicit `if (scanHorizontal) ... else ...` and reuses the half-extents already computed in each function (`halfH`/`halfW` vs `halfRow`/`halfCol`) - nothing recomputed.
- `pad` is computed after the clamped `halfLen`, equals `halfLen * 2.0 + 1.0`, and is never below the `halfLen + halfWidth` worst-case SE reach.
- `erosion <= 0` remains a total no-op; `const double halfWidth = 0.5;` unchanged; the 260727-jna polarity logic unchanged.
- Debug/x64 `//t:Build` clean (exit 0, no new CS diagnostics).
- Exactly one source file modified: `WPF_Example/Halcon/Algorithms/DatumFindingService.cs`.
</success_criteria>

<output>
After completion, create `.planning/quick/260727-mll-datumfindingservice-cs-gray-erosion-dila/260727-mll-SUMMARY.md`.

Notes for the summary:
- **Pending human UAT (outside this task's automated scope):** re-run the confirmed failing case - the ROI near the corner where a Vertical edge meets a Horizontal edge with `Erosion(px) = 201` - and confirm on the same zoomed before/after view that the corner nub survives on the horizontal portion while the vertical portion is still smoothed as intended. The automated checks here prove the clamp exists, is wired to the right half-extent at each site, compiles cleanly, and leaves the erosion<=0 / polarity / halfWidth behavior untouched - they do not prove the vision outcome.
- Record the behavior-change boundary explicitly for the user: recipes where `Erosion(px) / 2 <= ROI half-extent along the edge` are unaffected; recipes above that threshold now get a shorter SE (and a correspondingly smaller `canvasSize`, so erosion tact drops as well).
- If the `//t:Build` verification had to be replaced by the manual read-back fallback (exe lock), state that plainly as a deviation.
</output>
