---
phase: 11-datum-teaching-ui-roi
plan: 03a
type: execute
wave: 2
depends_on: [11-01]
files_modified:
  - WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs
  - WPF_Example/Halcon/Algorithms/DatumFindingService.cs
  - WPF_Example/Halcon/Display/HalconDisplayService.cs
autonomous: true
requirements: []
defects: [WR-RT-03]
decisions: [D-05, D-08, D-11]
tags: [wpf, datum, teaching, halcon, model, service]

must_haves:
  truths:
    - "DatumConfig carries SourceShotName string + 8 volatile Line*Detected_* doubles + LastTeachSucceeded bool (all [Browsable(false)] runtime fields, tagged [IgnoreDataMember] where applicable for ParamBase reflection)"
    - "DatumFindingService.TryTeachDatum writes detected line coordinates to DatumConfig volatile fields on success and sets LastTeachSucceeded on every exit branch"
    - "HalconDisplayService.RenderDatumOverlay gains an ADDITIVE rendering path (gated on datum.LastTeachSucceeded == true) that draws detected Line1 yellow + Line2 cyan + red 20px intersection cross"
    - "Existing post-teach Datum overlay palette (magenta RefOrigin cross + cyan Line1 ROI rectangle + blue Line2 ROI rectangle at L345/L357) is UNTOUCHED — Warning 5"
    - "Debug/x64 build passes"
  artifacts:
    - path: "WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs"
      provides: "SourceShotName + 8 Line*Detected_* doubles + LastTeachSucceeded"
      contains: "SourceShotName"
    - path: "WPF_Example/Halcon/Algorithms/DatumFindingService.cs"
      provides: "TryTeachDatum writeback (detected line coords + LastTeachSucceeded on all exits)"
      contains: "Line1Detected_RBegin"
    - path: "WPF_Example/Halcon/Display/HalconDisplayService.cs"
      provides: "Additive detected-line + red intersection cross rendering gated on LastTeachSucceeded"
      contains: "LastTeachSucceeded"
  key_links:
    - from: "DatumFindingService.TryTeachDatum success"
      to: "DatumConfig.Line*Detected_* + LastTeachSucceeded=true"
      via: "direct field assignment before return true"
      pattern: "LastTeachSucceeded = true"
    - from: "HalconDisplayService.RenderDatumOverlay new branch"
      to: "HOperatorSet.DispLine (yellow Line1, cyan Line2) + red 20px cross"
      via: "if (datum.LastTeachSucceeded) branch, ADDITIVE to existing L312-367 body"
      pattern: "datum\\.LastTeachSucceeded"
---

<objective>
Phase 11 Plan 03a — the model + service + display layer of Datum teaching (WR-RT-03). Lands three changes that are entirely independent of the UI wiring (Plan 03b) and can run in parallel with Plan 02 (Circle ROI UI).

This plan delivers:
1. DatumConfig fields (SourceShotName + volatile detected-line doubles + LastTeachSucceeded) — no UI coupling.
2. DatumFindingService.TryTeachDatum — writeback of detected line coordinates that are currently computed-and-discarded.
3. HalconDisplayService.RenderDatumOverlay — an ADDITIVE new branch that draws the detected-line + red intersection cross overlay. The existing post-teach palette (magenta RefOrigin cross / cyan Line1 ROI rect / blue Line2 ROI rect) is preserved UNCHANGED per Warning 5 scope guard.

Purpose: split the original 11-03 into two waves so the 6 tasks no longer exceed the plan context budget. Plan 03b (next wave) consumes the contracts this plan establishes.

Output: compiling model/service/display layer with new volatile fields populated and new overlay branch ready for the UI wiring in Plan 03b.
</objective>

<execution_context>
@$HOME/.claude/get-shit-done/workflows/execute-plan.md
@$HOME/.claude/get-shit-done/templates/summary.md
</execution_context>

<context>
@CLAUDE.md
@.planning/STATE.md
@.planning/phases/11-datum-teaching-ui-roi/11-CONTEXT.md
@.planning/phases/11-datum-teaching-ui-roi/11-UI-SPEC.md
@.planning/phases/11-datum-teaching-ui-roi/11-PATTERNS.md
@WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs
@WPF_Example/Halcon/Algorithms/DatumFindingService.cs
@WPF_Example/Halcon/Display/HalconDisplayService.cs

<interfaces>
<!-- DatumConfig additions this plan introduces (consumed by this plan's Task 2 + by Plan 03b): -->

```csharp
// DatumConfig additions (in namespace ReringProject.Sequence):
[Category("Datum|ImageSource")]
public string SourceShotName { get; set; } = "";

[PropertyTools.DataAnnotations.Browsable(false)] public double Line1Detected_RBegin { get; set; }
[PropertyTools.DataAnnotations.Browsable(false)] public double Line1Detected_CBegin { get; set; }
[PropertyTools.DataAnnotations.Browsable(false)] public double Line1Detected_REnd { get; set; }
[PropertyTools.DataAnnotations.Browsable(false)] public double Line1Detected_CEnd { get; set; }
[PropertyTools.DataAnnotations.Browsable(false)] public double Line2Detected_RBegin { get; set; }
[PropertyTools.DataAnnotations.Browsable(false)] public double Line2Detected_CBegin { get; set; }
[PropertyTools.DataAnnotations.Browsable(false)] public double Line2Detected_REnd { get; set; }
[PropertyTools.DataAnnotations.Browsable(false)] public double Line2Detected_CEnd { get; set; }
[PropertyTools.DataAnnotations.Browsable(false)] public bool LastTeachSucceeded { get; set; }
```

<!-- DatumFindingService.TryTeachDatum signature UNCHANGED (Option 2): writes detected line coords to DatumConfig volatile fields. Zero existing callers = no compatibility risk. -->

```csharp
// DatumFindingService.cs L122
public bool TryTeachDatum(HImage image, DatumConfig config, out string error);
// Post-condition on success: config.Line1Detected_RBegin/CBegin/REnd/CEnd + Line2Detected_* populated; LastTeachSucceeded=true
// Post-condition on failure: LastTeachSucceeded=false; error set
```

<!-- HalconDisplayService.RenderDatumOverlay existing body (L312-367) is an outer try/catch that renders:
  - Line1/Line2 ROI rectangles in cyan/blue (L345, L357)
  - magenta RefOrigin cross
  Phase 11 03a adds a SEPARATE conditional block at the end of the existing body (inside the same try) that draws the detected lines + red 20px cross ONLY when datum.LastTeachSucceeded==true. The existing cyan/blue/magenta palette is NOT modified. -->
</interfaces>
</context>

<tasks>

<task type="auto" tdd="false">
  <name>Task 1: DatumConfig — SourceShotName + 8 volatile detected-line doubles + LastTeachSucceeded</name>
  <files>WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs</files>
  <read_first>
    - WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs full file — existing [Category] blocks L20-67, existing [Browsable(false)] runtime field pattern at L66-70 (CurrentTransform, LastFindSucceeded), ImageSourceMode L22, ReuseFromShotName L25
    - .planning/phases/11-datum-teaching-ui-roi/11-PATTERNS.md §8 "DatumConfig.cs — SourceShotName + volatile detected-line fields (D-08)"
    - .planning/phases/11-datum-teaching-ui-roi/11-PATTERNS.md §Shared 8 "ParamBase reflection serialization gotcha" — ParamBase Save/Load handles Int32/Double/String/Boolean; no enums here.
  </read_first>
  <action>
    1. Add `SourceShotName` as a serializable string in the Datum|ImageSource category, beside ImageSourceMode (L22) and ReuseFromShotName (L25):
       ```csharp
       //260423 hbk Phase 11 D-08 — 카메라/Z/조명을 상속할 Shot 이름 (빈 문자열이면 Sequence 첫 Shot fallback)
       [Category("Datum|ImageSource")]
       public string SourceShotName { get; set; } = "";
       ```
       Default `""` ensures backward compat: old INI without this key loads as empty-string.

    2. Add 8 volatile double fields + 1 bool as `[Browsable(false)]` runtime-only properties, mirroring the CurrentTransform / LastFindSucceeded pattern at L66-70:
       ```csharp
       //260423 hbk Phase 11 D-11 — 검출 라인 오버레이용 휘발성 필드 (TryTeachDatum 성공 시 DatumFindingService가 기록)
       [PropertyTools.DataAnnotations.Browsable(false)]
       public double Line1Detected_RBegin { get; set; }
       [PropertyTools.DataAnnotations.Browsable(false)]
       public double Line1Detected_CBegin { get; set; }
       [PropertyTools.DataAnnotations.Browsable(false)]
       public double Line1Detected_REnd { get; set; }
       [PropertyTools.DataAnnotations.Browsable(false)]
       public double Line1Detected_CEnd { get; set; }
       [PropertyTools.DataAnnotations.Browsable(false)]
       public double Line2Detected_RBegin { get; set; }
       [PropertyTools.DataAnnotations.Browsable(false)]
       public double Line2Detected_CBegin { get; set; }
       [PropertyTools.DataAnnotations.Browsable(false)]
       public double Line2Detected_REnd { get; set; }
       [PropertyTools.DataAnnotations.Browsable(false)]
       public double Line2Detected_CEnd { get; set; }
       [PropertyTools.DataAnnotations.Browsable(false)]
       public bool LastTeachSucceeded { get; set; }
       ```
       These WILL get INI-serialized by ParamBase reflection (per PATTERNS.md §Shared 8 — Browsable(false) affects PropertyGrid only, not ParamBase reflection). That's acceptable — zero-valued on cold load, re-populated on every TryTeachDatum call. Matches precedent for runtime doubles in ShotConfig/FAIConfig.

    3. Do NOT remove or rename any existing field. Do NOT change ImageSourceMode's type (stays string).
    4. Brace style: match existing file.
    5. `//260423 hbk Phase 11` marker on each added block.
  </action>
  <verify>
    <automated>msbuild WPF_Example/DatumMeasurement.csproj /p:Configuration=Debug /p:Platform=x64 /v:minimal</automated>
  </verify>
  <acceptance_criteria>
    - `grep -n "public string SourceShotName" WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs` returns one match with `= ""` default.
    - `grep -c "Line1Detected_\\|Line2Detected_" WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs` returns exactly 8 property declarations.
    - `grep -n "public bool LastTeachSucceeded" WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs` returns one match with `[Browsable(false)]` above it.
    - All pre-existing fields (ImageSourceMode, ReuseFromShotName, Line1_Row/Col/Phi/Length1/Length2, Line2_*, RefOriginRow/Col, RefAngleRad, IsConfigured, CurrentTransform, LastFindSucceeded) still present — pure addition.
    - `grep -n "260423 hbk Phase 11" WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs` present.
    - msbuild Debug/x64 exits 0.
  </acceptance_criteria>
  <done>DatumConfig carries SourceShotName (serialized) and 9 volatile fields for detected-line overlay. Existing INI format still loads (defaults when keys missing).</done>
</task>

<task type="auto" tdd="false">
  <name>Task 2: DatumFindingService.TryTeachDatum — write detected line coords + LastTeachSucceeded on all exits</name>
  <files>WPF_Example/Halcon/Algorithms/DatumFindingService.cs</files>
  <read_first>
    - WPF_Example/Halcon/Algorithms/DatumFindingService.cs full file — TryTeachDatum L122-201 (locals `line1RowBegin/ColBegin/RowEnd/ColEnd` + `line2*` are computed from TryFindLine and currently discarded after intersection math), TryFindLine L207-277
    - WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs (post Task 1 of this plan) — confirm Line*Detected_* + LastTeachSucceeded visible
    - .planning/phases/11-datum-teaching-ui-roi/11-PATTERNS.md §10 "DatumFindingService.cs — TryTeachDatum line-coord writeback (D-11 / Specifics 옵션 2)"
  </read_first>
  <action>
    1. In TryTeachDatum, immediately before the final `return true;` at the end of the success path (~L194), insert:
       ```csharp
       //260423 hbk Phase 11 D-11 — 검출 라인 좌표 휘발성 저장 (오버레이용)
       config.Line1Detected_RBegin = line1RowBegin;
       config.Line1Detected_CBegin = line1ColBegin;
       config.Line1Detected_REnd   = line1RowEnd;
       config.Line1Detected_CEnd   = line1ColEnd;
       config.Line2Detected_RBegin = line2RowBegin;
       config.Line2Detected_CBegin = line2ColBegin;
       config.Line2Detected_REnd   = line2RowEnd;
       config.Line2Detected_CEnd   = line2ColEnd;
       config.LastTeachSucceeded   = true;
       ```
       If the existing local variable names differ from those above (e.g. different casing), use the EXACT names present in the function body.

    2. On EVERY early-exit failure path, set `config.LastTeachSucceeded = false;` before the `return false;`:
       - Before `error = "Line1: " + lineError; return false;` (Line1 TryFindLine failure branch).
       - Before `error = "Line2: " + lineError; return false;` (Line2 TryFindLine failure branch).
       - Inside the outer `catch (Exception ex) { error = ex.Message; return false; }` at ~L197-200.
       - Any other existing early `return false;` paths — prepend the assignment.

    3. Do NOT change the function signature (Option 2 per Specifics: no caller compatibility risk because grep confirms zero callers pre-phase-11; but we also do not want to churn the shape). Do NOT change RefOrigin / RefAngle / intersection math. Do NOT change TryFindLine.
    4. Brace style: match existing (Halcon code = Allman per PATTERNS.md §Shared 1).
    5. `//260423 hbk Phase 11 D-11` marker on each new block.
  </action>
  <verify>
    <automated>msbuild WPF_Example/DatumMeasurement.csproj /p:Configuration=Debug /p:Platform=x64 /v:minimal</automated>
  </verify>
  <acceptance_criteria>
    - `grep -n "config.Line1Detected_RBegin = " WPF_Example/Halcon/Algorithms/DatumFindingService.cs` returns exactly one match in the success path.
    - `grep -c "LastTeachSucceeded = true\\|LastTeachSucceeded   = true" WPF_Example/Halcon/Algorithms/DatumFindingService.cs` returns exactly 1.
    - `grep -c "LastTeachSucceeded = false" WPF_Example/Halcon/Algorithms/DatumFindingService.cs` returns at least 3 (Line1 fail, Line2 fail, catch).
    - `grep -n "public bool TryTeachDatum" WPF_Example/Halcon/Algorithms/DatumFindingService.cs` returns one match — signature `(HImage image, DatumConfig config, out string error)` unchanged.
    - Existing intersection computation (`HOperatorSet.IntersectionLl` or equivalent) + RefOrigin/RefAngle assignments textually unchanged.
    - `grep -n "260423 hbk Phase 11 D-11" WPF_Example/Halcon/Algorithms/DatumFindingService.cs` present.
    - msbuild Debug/x64 exits 0.
  </acceptance_criteria>
  <done>TryTeachDatum stores detected line coordinates on success and stamps LastTeachSucceeded on every exit branch. Signature unchanged. No regression to RefOrigin/RefAngle math.</done>
</task>

<task type="auto" tdd="false">
  <name>Task 3: HalconDisplayService.RenderDatumOverlay — ADDITIVE detected Line1 yellow + Line2 cyan + red 20px intersection cross (existing palette UNTOUCHED)</name>
  <files>WPF_Example/Halcon/Display/HalconDisplayService.cs</files>
  <read_first>
    - WPF_Example/Halcon/Display/HalconDisplayService.cs — RenderDatumOverlay L312-367. The existing body draws Line1 ROI rectangle in cyan (L345), Line2 ROI rectangle in blue (L357), and a magenta cross at RefOrigin. It is wrapped in an outer try/catch at L321-366.
    - WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs (post Task 1 of this plan) — confirm Line*Detected_* + LastTeachSucceeded visible
    - .planning/phases/11-datum-teaching-ui-roi/11-PATTERNS.md §11 "RenderDatumOverlay extension (L312-367)"
    - .planning/phases/11-datum-teaching-ui-roi/11-UI-SPEC.md §Color "Datum teaching overlay (D-11)" — yellow Line1 detected / cyan Line2 detected / red 20px cross
    - SCOPE GUARD (Warning 5): the existing post-teach palette (cyan Line1 ROI rect @L345, blue Line2 ROI rect @L357, magenta RefOrigin cross) MUST NOT be modified. The new detected-line + red cross render is an ADDITIVE new branch. The yellow/cyan/red palette applies ONLY during teach-time, and on the LastTeachSucceeded==true feedback path. Post-teach display-mode rendering is unchanged.
  </read_first>
  <action>
    1. Inside the existing RenderDatumOverlay method (L312-367), AFTER the existing ROI rectangle + magenta RefOrigin-cross drawing block (i.e. after the existing body completes, but still INSIDE the outer try/catch at L321-366 so display errors remain suppressed), append a new conditional block:
       ```csharp
       //260423 hbk Phase 11 D-11 — 검출 라인 2개 + 교점 오버레이 (TryTeachDatum 성공 시에만, 기존 팔레트는 건드리지 않음)
       if (datum.LastTeachSucceeded)
       {
           // Line1 detected (yellow)
           HOperatorSet.SetColor(window, "yellow");
           HOperatorSet.SetLineWidth(window, 2);
           HOperatorSet.DispLine(window,
               datum.Line1Detected_RBegin, datum.Line1Detected_CBegin,
               datum.Line1Detected_REnd,   datum.Line1Detected_CEnd);

           // Line2 detected (cyan)
           HOperatorSet.SetColor(window, "cyan");
           HOperatorSet.DispLine(window,
               datum.Line2Detected_RBegin, datum.Line2Detected_CBegin,
               datum.Line2Detected_REnd,   datum.Line2Detected_CEnd);

           // Intersection cross (red, 20px half-length, line width 2) — UI-SPEC Datum overlay palette
           HOperatorSet.SetColor(window, "red");
           HOperatorSet.SetLineWidth(window, 2);
           const double crossHalf = 20.0;
           HOperatorSet.DispLine(window, datum.RefOriginRow - crossHalf, datum.RefOriginCol,
                                         datum.RefOriginRow + crossHalf, datum.RefOriginCol);
           HOperatorSet.DispLine(window, datum.RefOriginRow, datum.RefOriginCol - crossHalf,
                                         datum.RefOriginRow, datum.RefOriginCol + crossHalf);
       }
       ```

    2. SCOPE GUARD — DO NOT MODIFY:
       - The cyan Line1 ROI rectangle draw at L345 (existing post-teach style — keep cyan).
       - The blue Line2 ROI rectangle draw at L357 (existing post-teach style — keep blue).
       - The magenta RefOrigin cross draw in the existing body (keep magenta).
       - No new `isTeaching` parameter. No change to signature `RenderDatumOverlay(HWindow, DatumConfig, bool)`.
       The new yellow/cyan detected-line + red intersection cross are an additive overlay that layers ON TOP of the existing cyan/blue/magenta palette. This honors Warning 5: existing post-teach behavior preserved; new overlay applies only when LastTeachSucceeded==true (i.e. during teach feedback and while a just-taught Datum node remains selected).

    3. Wrap all new HOperatorSet calls within the existing outer try/catch at L321-366 (suppress display errors per project convention).

    4. Do NOT touch the Circle ROI render branch from Plan 01 (independent change). Do NOT touch RenderCircleDraft.

    5. Brace style: Allman. C# 7.2.
    6. `//260423 hbk Phase 11 D-11` marker on the new block.
  </action>
  <verify>
    <automated>msbuild WPF_Example/DatumMeasurement.csproj /p:Configuration=Debug /p:Platform=x64 /v:minimal</automated>
  </verify>
  <acceptance_criteria>
    - `grep -n "if (datum.LastTeachSucceeded)" WPF_Example/Halcon/Display/HalconDisplayService.cs` returns one match inside RenderDatumOverlay.
    - `grep -n "Line1Detected_RBegin\\|Line1Detected_REnd" WPF_Example/Halcon/Display/HalconDisplayService.cs` returns matches inside RenderDatumOverlay.
    - `grep -c "HOperatorSet.DispLine(window" WPF_Example/Halcon/Display/HalconDisplayService.cs` increases by at least 4 (Line1 + Line2 + 2 cross segments) vs pre-edit count.
    - `grep -n "const double crossHalf = 20" WPF_Example/Halcon/Display/HalconDisplayService.cs` returns one match.
    - **SCOPE GUARD — Warning 5:** `grep -n "\"magenta\"" WPF_Example/Halcon/Display/HalconDisplayService.cs` count UNCHANGED vs pre-edit (existing RefOrigin cross keeps magenta). `grep -n "\"cyan\"" WPF_Example/Halcon/Display/HalconDisplayService.cs` inside RenderDatumOverlay still contains the existing Line1 ROI rect reference at L345. `grep -n "\"blue\"" WPF_Example/Halcon/Display/HalconDisplayService.cs` inside RenderDatumOverlay still contains the existing Line2 ROI rect reference at L357.
    - `grep -n "\"yellow\"" WPF_Example/Halcon/Display/HalconDisplayService.cs` inside RenderDatumOverlay includes at least one new match (detected Line1 only — not the existing ROI rects).
    - `grep -n "260423 hbk Phase 11 D-11" WPF_Example/Halcon/Display/HalconDisplayService.cs` present.
    - Existing Circle render branch from Plan 01 intact (regression): `grep -n "RoiShape.Circle" WPF_Example/Halcon/Display/HalconDisplayService.cs` unchanged vs pre-edit.
    - msbuild Debug/x64 exits 0.
  </acceptance_criteria>
  <done>RenderDatumOverlay gains a LastTeachSucceeded-gated ADDITIVE branch drawing yellow detected Line1 + cyan detected Line2 + red 20px intersection cross. The existing cyan/blue/magenta post-teach palette is preserved exactly. Plan 01 Circle render unaffected.</done>
</task>

</tasks>

<threat_model>
## Trust Boundaries

| Boundary | Description |
|----------|-------------|
| INI recipe file → DatumConfig deserialization | Operator-editable recipe file with string SourceShotName. Could reference a non-existent Shot (handled by Plan 03b). |
| DatumFindingService.TryTeachDatum ← HImage + DatumConfig | Trusted local call. No network boundary. |
| HalconDisplayService.RenderDatumOverlay ← DatumConfig volatile fields | Trusted (read-only render from local object state). |

## STRIDE Threat Register

| Threat ID | Category | Component | Disposition | Mitigation Plan |
|-----------|----------|-----------|-------------|-----------------|
| T-11-03a-01 | Tampering | SourceShotName references non-existent Shot | transfer | Resolution + error handling deferred to Plan 03b where UI-level grab path exists. This plan only stores the string. |
| T-11-03a-02 | Denial of Service | TryTeachDatum throws on malformed image | mitigate | Existing outer try/catch at DatumFindingService L197-200 catches Exception, returns false — Task 2 also stamps LastTeachSucceeded=false. No crash. |
| T-11-03a-03 | Information Disclosure | DatumConfig volatile coordinates get written to INI | accept | Recipe files are operator-owned; coordinates are geometric calibration data. Consistent with existing precedent (ShotConfig/FAIConfig runtime doubles). |
| T-11-03a-04 | Tampering | RenderDatumOverlay reads stale Line*Detected_* after a failed retry | mitigate | Task 2 writes LastTeachSucceeded=false on failure, gating the new rendering block. Stale coord values remain in memory but are not drawn. |
| T-11-03a-05 | Repudiation | N/A | accept | No audit boundary. |
| T-11-03a-06 | Spoofing | N/A | accept | Local in-process state. |
| T-11-03a-07 | Elevation of Privilege | N/A | accept | No privilege boundary. |
</threat_model>

<verification>
- Debug/x64 build green for all 3 code tasks.
- Existing Phase 6/7 INI recipes load (SourceShotName defaults to "" when missing).
- Existing cyan/blue/magenta Datum overlay palette preserved (Warning 5 scope guard).
- Plan 01 Circle render branch unaffected (regression check in Task 3 acceptance).
- Plan 03b (next wave) will verify the full end-to-end teach flow via UAT.
</verification>

<success_criteria>
- DatumConfig exposes all fields required by Plan 03b (SourceShotName + 8 volatile doubles + LastTeachSucceeded).
- DatumFindingService.TryTeachDatum produces observable side effects (coord writeback + LastTeachSucceeded stamp) that Plan 03b's UI can trigger.
- HalconDisplayService.RenderDatumOverlay produces the LastTeachSucceeded-gated detected-line overlay without regressing the existing post-teach palette.
- Build-green foundation for Plan 03b wiring.
</success_criteria>

<output>
After completion, create `.planning/phases/11-datum-teaching-ui-roi/11-03a-SUMMARY.md` documenting:
- Exact line-number insertions per file (DatumConfig additions, DatumFindingService writeback block site, HalconDisplayService new-branch site)
- Confirmation that the existing cyan/blue/magenta palette was preserved untouched (Warning 5 compliance evidence — cite grep counts)
- Confirmation that DatumFindingService signature unchanged
- Any deviations from local variable names in DatumFindingService (if the existing locals differ from `line1RowBegin` etc.)
</output>
</content>
</invoke>