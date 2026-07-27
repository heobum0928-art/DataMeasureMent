---
phase: 260727-jna
plan: 01
type: execute
wave: 1
depends_on: []
files_modified:
  - WPF_Example/Halcon/Algorithms/DatumFindingService.cs
autonomous: true
requirements:
  - FIX-ErosionPolarity-TryFindLine
  - FIX-ErosionPolarity-TryExtractEdgePoints

must_haves:
  truths:
    - "For a Datum ROI with polarity==\"negative\" and erosion>0, the directional pre-processing calls HOperatorSet.GrayDilation (local-maximum) instead of HOperatorSet.GrayErosion, so dark protrusions poking into a bright background are actually suppressed."
    - "For polarity==\"positive\" or \"all\" (and any other value), the pre-processing still calls HOperatorSet.GrayErosion exactly as before — byte-identical behavior, zero regression."
    - "polarity matching is case-insensitive via string.Equals(..., StringComparison.OrdinalIgnoreCase), matching ComputeMeasurePhi's existing convention in the same file."
    - "The erosion Trace log line reports which operator actually ran (gray_erosion vs gray_dilation) instead of unconditionally saying \"erosion\"; the catch-block fallback log does the same."
    - "erosion<=0 remains a complete no-op — the whole if (erosion > 0) block is skipped and zero new HOperatorSet calls occur (existing recipes byte-identical)."
    - "Both call sites (TryFindLine and TryExtractEdgePoints) receive the identical fix — no asymmetry between the line-fit path and the horizontal 2-ROI concat path."
    - "MSBuild Debug/x64 CoreCompile of WPF_Example/DatumMeasurement.csproj completes with exit code 0 and no CS error/warning referencing DatumFindingService.cs."
  artifacts:
    - path: "WPF_Example/Halcon/Algorithms/DatumFindingService.cs"
      provides: "Polarity-aware directional grayscale morphology pre-processing at both strip-loop entry points"
      contains: "GrayDilation"
  key_links:
    - from: "TryFindLine(... string polarity ...) parameter"
      to: "HOperatorSet.GrayDilation / HOperatorSet.GrayErosion selection"
      via: "bool useDilation = string.Equals(polarity, \"negative\", StringComparison.OrdinalIgnoreCase)"
      pattern: "string\\.Equals\\(polarity, \"negative\", StringComparison\\.OrdinalIgnoreCase\\)"
    - from: "TryExtractEdgePoints(... string polarity ...) parameter"
      to: "HOperatorSet.GrayDilation / HOperatorSet.GrayErosion selection"
      via: "same useDilation branch (mirrored block)"
      pattern: "HOperatorSet\\.GrayDilation\\(reducedImage, seImage, out erodedObj\\)"
    - from: "useDilation / opName"
      to: "Logging.PrintLog Trace messages (success + catch fallback)"
      via: "string.Format with opName interpolated in place of the hardcoded word"
      pattern: "opName"
---

<objective>
Fix the directional grayscale morphology pre-processing in `DatumFindingService.cs` so it respects edge polarity.

The pre-processing step (added 260723/260724) suppresses small 1-2px physical protrusions/burrs along a measured edge before the HALCON MeasurePos strip-loop runs. It builds a thin, edge-direction-oriented structuring element (`GenRectangle2`, half-width fixed ~0.5px across the edge, half-length = erosion/2 along the edge) and feeds it to `HOperatorSet.GrayErosion`.

**Bug:** the operator call is unconditional. `gray_erosion` is a local-minimum filter — it only shrinks BRIGHT regions and therefore cannot remove a small DARK intrusion into a bright region. For a `polarity=="negative"` edge (confirmed live case: `Side_Datum_3` Vertical ROI, `direction="RtoL"` — bright background, dark part, so protrusions are dark material poking into bright background) the erosion is a complete no-op on the very defects it exists to remove. `gray_dilation` (local-maximum: shrinks dark / expands bright) is the correct symmetric operator for that case.

Polarity — not `direction` — is the correct discriminant: this codebase maps "negative" = light-to-dark transition consistently across `ComputeMeasurePhi` (same file), `MeasurementAlgorithm.cs`, `FAIEdgeMeasurementService.cs`, `VisionAlgorithmService.cs`, and `EdgeOptionLists.cs`, and "negative" means "from-side bright, to-side dark" for all 4 direction values.

Purpose: make the burr-suppression feature actually work on negative-polarity Datum edges (currently silently ineffective), with zero behavior change for positive/all.
Output: 1 modified C# file, 2 call sites fixed, clean Debug/x64 compile.
</objective>

<execution_context>
@$HOME/.claude/get-shit-done/workflows/execute-plan.md
@$HOME/.claude/get-shit-done/templates/summary.md
</execution_context>

<context>
@.planning/STATE.md
@./CLAUDE.md

<verified_during_planning>
<!-- All of the following was confirmed by direct inspection during planning. Do NOT re-investigate. -->

1. **Exactly two `GrayErosion` call sites exist in the file** — line 1737 (inside `TryFindLine`) and line 1975 (inside `TryExtractEdgePoints`). No other occurrence anywhere in the file.

2. **`polarity` is already in scope and already normalized at both sites.** Both functions declare `string polarity` as a parameter (`TryFindLine` at line 1608, `TryExtractEdgePoints` at line 1863) and both run `if (string.IsNullOrEmpty(polarity)) polarity = "all";` in their sanity-clamp block, well before the `if (erosion > 0)` block. No null guard is needed at the call site.

3. **`using System;` is present** (line 1 of the file) — `StringComparison` resolves unqualified.

4. **`HOperatorSet.GrayDilation` has an identical signature to `GrayErosion`** (verified by reflection against `C:\Program Files\MVTec\HALCON-24.11-Progress-Steady\bin\dotnet35\halcondotnet.dll`):
```
GrayDilation(HObject image, HObject SE, HObject& imageDilation)
GrayErosion (HObject image, HObject SE, HObject& imageErosion)
```
It is a drop-in replacement — same argument order, same `out HObject` third parameter.

5. **The file's established case-insensitive string convention** (`ComputeMeasurePhi`, lines 2085-2087, and lines 2114-2115):
```csharp
if (string.Equals(direction, "TtoB", StringComparison.OrdinalIgnoreCase))      measurePhi = -Math.PI / 2.0;
else if (string.Equals(direction, "BtoT", StringComparison.OrdinalIgnoreCase)) measurePhi = +Math.PI / 2.0;
```

6. **The file's established "default + single-line if override" idiom** (lines 1667-1668 and 1674-1675) — mirror this exactly for `opName`:
```csharp
int stripCount = 20;
if (sampleCount > 0) stripCount = sampleCount;

string scanLabel = "vertical";
if (scanHorizontal) scanLabel = "horizontal";
```

7. **The two erosion blocks are byte-identical to each other** for the lines being changed. The `Logging.PrintLog` format strings at 1742/1980 and 1749/1987 are literally the same text in both functions. A naive `Edit` with a non-unique `old_string` WILL fail or hit the wrong site — see the Task notes for disambiguation.

8. **Build verification is constrained by a file lock.** `DatumMeasurement.exe` is currently running (PID observed during planning), which locks `bin\x64\Debug\DatumMeasurement.exe`. A full `//t:Build` on the .sln therefore fails with `MSB3027`/`MSB3021` copy errors that are NOT compile failures. Use the `//t:Compile` target on the .csproj instead — it routes through `CoreCompile` (confirmed) and never touches `bin\`, so it is immune to the lock.

9. **Known pre-existing CS warning baseline (6, none in this file):** CS0618 x5 (`Sequence_Top.cs:19`, `Sequence_Bottom.cs:30`, `SequenceHandler.cs:69/71/73`) + CS0162 x1 (`VirtualCamera.cs:237`). Any CS diagnostic mentioning `DatumFindingService.cs` is new and is a failure.
</verified_during_planning>

<current_code_site_1>
<!-- WPF_Example/Halcon/Algorithms/DatumFindingService.cs lines 1712-1752, inside TryFindLine. -->
<!-- Lines 1712-1736 and 1743-1752 shown for context ONLY — do not modify them except where Task 1 says. -->

            if (erosion > 0)
            {
                // 260724 hbk 속도 실측용 임시 계측 — erosion on/off tact 비교 요청. 값 확인 후 제거 여부 재검토.
                var erosionSw = System.Diagnostics.Stopwatch.StartNew();
                try
                {
                    double pad = erosion; // 회전 마스크 worst-case 반경(erosion/2+0.5) 도 이 여유 안에 들어옴 → 값 유지
                    HOperatorSet.GenRectangle1(out roiDomain, top - pad, left - pad, bottom + pad, right + pad);
                    HOperatorSet.ReduceDomain(image, roiDomain, out reducedImage);

                    double measurePhi  = ComputeMeasurePhi(direction, alignRot);
                    double lineAxisPhi = measurePhi + Math.PI / 2.0; // 라인(에지) 방향 = 스캔방향(measurePhi) + 90°
                    double halfLen     = erosion / 2.0; // 라인 방향 반길이(구 gray_erosion_rect(erosion,erosion) 의 "전체 크기=erosion" 의미 보존)
                    const double halfWidth = 0.5;        // 에지 횡단 방향 반폭 고정(~1px, 비노출) — 에지 블러 방지가 이 기능의 존재 이유

                    HTuple imgType;
                    HOperatorSet.GetImageType(image, out imgType); // gray_erosion: SE 픽셀타입은 Image 와 일치해야 함
                    int canvasSize = (int)(2.0 * Math.Ceiling(halfLen + halfWidth) + 3.0); // 임의 회전각에서도 마스크 전체를 담는 정사각 캔버스 + 여유
                    if (canvasSize % 2 == 0) canvasSize++;
                    double centerRC = (canvasSize - 1) / 2.0; // 홀수 캔버스 중앙 정수 픽셀 = SE origin (read_gray_se 규약, 반올림 불필요)

                    HOperatorSet.GenImageConst(out seCanvas, imgType, canvasSize, canvasSize);
                    HOperatorSet.GenRectangle2(out seRegion, centerRC, centerRC, lineAxisPhi, halfLen, halfWidth);
                    HOperatorSet.ReduceDomain(seCanvas, seRegion, out seImage);

                    HOperatorSet.GrayErosion(reducedImage, seImage, out erodedObj);
                    erodedImage = new HImage(erodedObj);
                    stripImage = erodedImage;
                    erosionSw.Stop();
                    Logging.PrintLog((int)ELogType.Trace,
                        string.Format("[Datum.{0}] erosion tact = {1}ms (erosion={2}px, canvas={3}x{3})", lbl, erosionSw.ElapsedMilliseconds, erosion, canvasSize));
                }
                catch (Exception erodeEx)
                {
                    erosionSw.Stop();
                    // erosion 전처리 실패는 non-critical: 원본 image 로 폴백 (strip swallow 정책과 동일 사상)
                    Logging.PrintLog((int)ELogType.Trace,
                        string.Format("[Datum.{0}] gray_erosion(directional) skipped after {1}ms (fallback to source image): {2}", lbl, erosionSw.ElapsedMilliseconds, erodeEx.Message));
                    stripImage = image;
                }
            }
</current_code_site_1>

<current_code_site_2>
<!-- WPF_Example/Halcon/Algorithms/DatumFindingService.cs lines 1950-1990, inside TryExtractEdgePoints. -->
<!-- Structurally IDENTICAL to site 1 for every line Task 2 touches (same comments, same format strings, -->
<!-- same variable names: erosionSw, reducedImage, seImage, erodedObj, erodedImage, stripImage, lbl, canvasSize). -->
<!-- The only nearby difference is the earlier strip-loop trace at line 1930 which says "strip-loop(extract)". -->
</current_code_site_2>
</context>

<tasks>

<task type="auto">
  <name>Task 1: Make TryFindLine's directional morphology polarity-aware</name>
  <files>WPF_Example/Halcon/Algorithms/DatumFindingService.cs</files>
  <action>
In `TryFindLine`, inside the `if (erosion > 0)` block (currently lines ~1712-1752), make three surgical edits. Read the block first to confirm current line numbers — they may have shifted.

**Edit 1 — add the operator-selection decision immediately after the stopwatch line and BEFORE the `try` keyword.**

The `opName` variable MUST be declared outside/before the `try` block, because the `catch` block also needs it. Insert after `var erosionSw = System.Diagnostics.Stopwatch.StartNew();`:

```csharp
                // gray_erosion 은 국소최소 필터라 "밝은" 영역만 깎는다. polarity=negative(밝음→어두움 전이) 에지에서는
                //  돌기가 밝은 배경으로 튀어나온 "어두운" 재질이므로 침식으로는 지워지지 않는다 → 국소최대인
                //  gray_dilation 으로 대칭 처리해야 억제된다. positive/all 은 기존 gray_erosion 유지(회귀 0).
                bool useDilation = string.Equals(polarity, "negative", StringComparison.OrdinalIgnoreCase);
                string opName = "gray_erosion";
                if (useDilation) opName = "gray_dilation";
```

**Edit 2 — replace the unconditional operator call.**

Replace:
```csharp
                    HOperatorSet.GrayErosion(reducedImage, seImage, out erodedObj);
```
with:
```csharp
                    if (useDilation)
                    {
                        HOperatorSet.GrayDilation(reducedImage, seImage, out erodedObj);
                    }
                    else
                    {
                        HOperatorSet.GrayErosion(reducedImage, seImage, out erodedObj);
                    }
```
Keep the two lines that follow it (`erodedImage = new HImage(erodedObj);` and `stripImage = erodedImage;`) exactly as-is — they are operator-agnostic.

**Edit 3 — make both Trace log messages report the operator that actually ran.**

Success log — replace:
```csharp
                        string.Format("[Datum.{0}] erosion tact = {1}ms (erosion={2}px, canvas={3}x{3})", lbl, erosionSw.ElapsedMilliseconds, erosion, canvasSize));
```
with:
```csharp
                        string.Format("[Datum.{0}] {1} tact = {2}ms (erosion={3}px, canvas={4}x{4}, polarity={5})", lbl, opName, erosionSw.ElapsedMilliseconds, erosion, canvasSize, polarity));
```

Catch-block fallback log — replace:
```csharp
                        string.Format("[Datum.{0}] gray_erosion(directional) skipped after {1}ms (fallback to source image): {2}", lbl, erosionSw.ElapsedMilliseconds, erodeEx.Message));
```
with:
```csharp
                        string.Format("[Datum.{0}] {1}(directional) skipped after {2}ms (fallback to source image): {3}", lbl, opName, erosionSw.ElapsedMilliseconds, erodeEx.Message));
```
Note the placeholder indices all shift by one because `opName` is inserted as `{1}`. Reusing `{4}` twice for `canvasSize` is intentional and legal (`string.Format` allows repeated indices) — it preserves the original `canvas={3}x{3}` rendering.

**CRITICAL — Edit-tool disambiguation.** The identical strings exist at the site-2 block in `TryExtractEdgePoints`. Every `old_string` you pass MUST include enough unique surrounding context to bind to the `TryFindLine` block only, or you must use line-number-anchored edits. After Task 1, verify by grep that the file still contains exactly ONE remaining unmodified `erosion tact` / `gray_erosion(directional) skipped` pair (site 2's) — if zero remain, you accidentally edited both and Task 2 is already done; if two remain, your edit did not apply.

**MUST NOT CHANGE (byte-identical after this task):**
- The `if (erosion > 0)` gate itself — `erosion<=0` must remain a total no-op.
- The `try` / `catch (Exception erodeEx)` structure and the `stripImage = image;` fallback.
- The `finally` block's HObject/HImage disposal (~line 1845) — it disposes generically by null-check and does not care which operator populated `erodedObj`.
- The SE construction above the operator call: `GenRectangle1`, `ReduceDomain`, `ComputeMeasurePhi`, `lineAxisPhi`, `halfLen`, `halfWidth`, `GetImageType`, `canvasSize`, `centerRC`, `GenImageConst`, `GenRectangle2`, `ReduceDomain`.
- The existing `//260724 hbk` comments already in the block — leave them alone.
- Any other code in `TryFindLine` or anywhere else in the file.

**Style rules (project CLAUDE.md + this file's local convention):**
- Allman braces (opening brace on its own line) for the `if (useDilation) { ... } else { ... }` operator block.
- Single-line `if` without braces is correct for the short `if (useDilation) opName = "gray_dilation";` guard — it mirrors lines 1667-1668 / 1674-1675 in this same function.
- if/else, never a ternary `?:`.
- Do NOT add a `//YYMMDD hbk` date prefix to the new comment. That convention was formally retired 2026-06-11; only the non-obvious "why" is wanted, and the erosion-vs-dilation polarity reasoning IS the non-obvious why.
- Do NOT touch any other file.
  </action>
  <verify>
    <automated>cd "C:/Info/Project/DataMeasurement" && "C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/MSBuild.exe" WPF_Example/DatumMeasurement.csproj //t:Compile //p:Configuration=Debug //p:Platform=x64 //v:m 2>&1 | grep -E "error|warning CS" | grep -v -E "CS0618|CS0162" | head -20; echo "MSBUILD_EXIT=${PIPESTATUS[0]}"</automated>
  </verify>
  <done>
- `MSBUILD_EXIT=0` and the filtered grep prints nothing (no CS error, no new CS warning outside the known CS0618/CS0162 baseline, and specifically nothing referencing `DatumFindingService.cs`).
- `grep -c "GrayDilation" WPF_Example/Halcon/Algorithms/DatumFindingService.cs` returns 1.
- `grep -c "GrayErosion" WPF_Example/Halcon/Algorithms/DatumFindingService.cs` returns 2 (site 1's else-branch + site 2 still untouched).
- `grep -c "erosion tact" WPF_Example/Halcon/Algorithms/DatumFindingService.cs` returns 1 (only site 2's remains).
- The `if (erosion > 0)` gate and the `catch`/`finally` structure are unchanged.
  </done>
</task>

<task type="auto">
  <name>Task 2: Mirror the polarity fix into TryExtractEdgePoints and verify both sites</name>
  <files>WPF_Example/Halcon/Algorithms/DatumFindingService.cs</files>
  <action>
Apply the exact same three edits from Task 1 to the second block, inside `TryExtractEdgePoints` (`if (erosion > 0)` at ~line 1950, `GrayErosion` at ~line 1975 — line numbers will have shifted by the ~+9 lines Task 1 added, so re-read before editing).

This block is structurally identical to site 1 and uses the same ambient variable names (`erosionSw`, `polarity`, `reducedImage`, `seImage`, `erodedObj`, `erodedImage`, `stripImage`, `lbl`, `canvasSize`), so the replacement text is character-for-character the same as Task 1's:

1. Insert the `useDilation` / `opName` decision (with the same "why" comment) immediately after `var erosionSw = System.Diagnostics.Stopwatch.StartNew();` and before `try`.
2. Replace the unconditional `HOperatorSet.GrayErosion(reducedImage, seImage, out erodedObj);` with the `if (useDilation) { GrayDilation } else { GrayErosion }` Allman block.
3. Update the success `string.Format` and the catch-block fallback `string.Format` to interpolate `opName` with the shifted placeholder indices.

Because Task 1 already changed site 1, the site-2 strings are now unique in the file — a plain `Edit` will bind unambiguously.

Why this second site matters: `TryExtractEdgePoints` is the horizontal 2-ROI concat fit path. Leaving it unfixed would make a negative-polarity Datum behave differently depending on which ROI role it is used in — exactly the kind of asymmetry that produces "works on Vertical, fails on Horizontal_A" bug reports.

**MUST NOT CHANGE:** identical restriction list to Task 1 — the `if (erosion > 0)` gate, try/catch structure and `stripImage = image;` fallback, the `finally` disposal block (~line 2065), the SE construction, the existing `//260724 hbk` comments, and any other code or file.

**Style rules:** identical to Task 1 (Allman braces on the operator if/else, single-line if for the `opName` guard, no ternary, no `//YYMMDD hbk` prefix on the new comment).

After editing, run the full structural check in the verify block below to confirm both sites are symmetric and no hardcoded operator name survives in either log message.
  </action>
  <verify>
    <automated>cd "C:/Info/Project/DataMeasurement" && "C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/MSBuild.exe" WPF_Example/DatumMeasurement.csproj //t:Compile //p:Configuration=Debug //p:Platform=x64 //v:m 2>&1 | grep -E "error|warning CS" | grep -v -E "CS0618|CS0162" | head -20; echo "MSBUILD_EXIT=${PIPESTATUS[0]}"; F=WPF_Example/Halcon/Algorithms/DatumFindingService.cs; echo "GrayDilation=$(grep -c 'HOperatorSet.GrayDilation' $F) (want 2)"; echo "GrayErosion=$(grep -c 'HOperatorSet.GrayErosion' $F) (want 2)"; echo "useDilation_decl=$(grep -c 'bool useDilation = string.Equals(polarity, \"negative\", StringComparison.OrdinalIgnoreCase)' $F) (want 2)"; echo "opName_default=$(grep -c 'string opName = \"gray_erosion\"' $F) (want 2)"; echo "erosion_gate=$(grep -c 'if (erosion > 0)' $F) (want 2)"; echo "hardcoded_success_log=$(grep -c 'erosion tact' $F) (want 0)"; echo "hardcoded_catch_log=$(grep -c 'gray_erosion(directional) skipped' $F) (want 0)"</automated>
  </verify>
  <done>
- `MSBUILD_EXIT=0` and the filtered grep prints nothing — no CS error and no new CS warning outside the known CS0618/CS0162 baseline; nothing references `DatumFindingService.cs`.
- All seven structural counts match their `(want N)` annotation: GrayDilation=2, GrayErosion=2, useDilation_decl=2, opName_default=2, erosion_gate=2, hardcoded_success_log=0, hardcoded_catch_log=0.
- Manual read-back of both blocks confirms: braces balanced, both if/else branches syntactically valid C# 7.2, every referenced identifier exists in ambient scope (`polarity`, `useDilation`, `opName`, `reducedImage`, `seImage`, `erodedObj`, `erosionSw`, `lbl`, `canvasSize`), and `opName` is declared before the `try` so the `catch` can reference it.
- `git diff --stat` shows exactly one file changed: `WPF_Example/Halcon/Algorithms/DatumFindingService.cs`.
  </done>
</task>

</tasks>

<threat_model>
## Trust Boundaries

| Boundary | Description |
|----------|-------------|
| (none crossed) | This change is confined to an in-process HALCON image-processing pre-filter operating on an already-acquired `HImage`. No network, file, user, or IPC input crosses any boundary in the modified code. |

## STRIDE Threat Register

| Threat ID | Category | Component | Disposition | Mitigation Plan |
|-----------|----------|-----------|-------------|-----------------|
| T-260727-01 | Tampering (data integrity) | `polarity` recipe field driving operator selection | mitigate | `polarity` is normalized (`IsNullOrEmpty -> "all"`) before use, and the discriminant is an explicit equality test against a single literal; every non-"negative" value falls through to the pre-existing `GrayErosion` path, so a malformed/unexpected recipe value degrades to today's exact behavior rather than an undefined one. |
| T-260727-02 | Denial of Service | `GrayDilation` throwing on unexpected image/SE type | accept | The existing `try/catch (Exception erodeEx)` already wraps the call and falls back to the unprocessed source image with a Trace log. `GrayDilation` has an identical signature and identical type constraints to `GrayErosion` (verified by reflection), so it introduces no new failure mode the existing handler does not already cover. |
</threat_model>

<verification>
1. **Compile:** `//t:Compile` on `WPF_Example/DatumMeasurement.csproj` (Debug/x64) exits 0 with no CS diagnostic mentioning `DatumFindingService.cs`. Do NOT use a full `//t:Build` on the .sln for this check — `DatumMeasurement.exe` may be running and will produce `MSB3027`/`MSB3021` output-copy errors that are file-lock artifacts, not compile failures.
2. **Symmetry:** both `if (erosion > 0)` blocks contain the identical `useDilation`/`opName` decision and the identical Allman if/else operator selection.
3. **No-op preservation:** the `if (erosion > 0)` gate is untouched at both sites, so `erosion<=0` still executes zero new HOperatorSet calls (existing recipes byte-identical).
4. **Positive/all regression:** for any polarity other than "negative", the executed statement is still `HOperatorSet.GrayErosion(reducedImage, seImage, out erodedObj);` with the same arguments — behaviorally byte-identical to pre-change.
5. **Scope:** `git diff --name-only` lists exactly one file.
</verification>

<success_criteria>
- `HOperatorSet.GrayDilation` is called exactly when `polarity` case-insensitively equals "negative" and `erosion > 0`, at BOTH `TryFindLine` and `TryExtractEdgePoints`.
- `HOperatorSet.GrayErosion` remains the call for "positive", "all", and any other polarity value — zero behavior change for those.
- Both Trace log messages (success and catch-fallback) name the operator that actually ran; no hardcoded "erosion"/"gray_erosion" wording survives in either message at either site.
- `erosion <= 0` remains a total no-op.
- The `if (erosion > 0)` gate, try/catch/fallback structure, `finally` disposal, and SE construction are unchanged at both sites.
- Debug/x64 CoreCompile is clean (exit 0, no new CS diagnostics).
- Exactly one file modified: `WPF_Example/Halcon/Algorithms/DatumFindingService.cs`.
</success_criteria>

<output>
After completion, create `.planning/quick/260727-jna-datumfindingservice-cs-erosion-polarity-/260727-jna-SUMMARY.md`.

Note for the summary: the real-world validation (does the burr actually get suppressed on `Side_Datum_3`'s Vertical RtoL ROI?) requires a human running the app against real imagery with `Vertical_Erosion > 0`. Flag this as a pending human UAT item — the automated verification in this plan proves correctness of the operator selection and compile integrity, not the vision outcome.
</output>
