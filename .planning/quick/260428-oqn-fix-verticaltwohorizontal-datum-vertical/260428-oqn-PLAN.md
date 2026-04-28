---
phase: 260428-oqn-fix-verticaltwohorizontal-datum-vertical
plan: 01
type: execute
wave: 1
depends_on: []
files_modified:
  - WPF_Example/Halcon/Display/HalconDisplayService.cs
autonomous: true
requirements:
  - QFIX-01
must_haves:
  truths:
    - "When AlgorithmType == VerticalTwoHorizontal and Vertical_Length1/Length2 > 0, the Vertical ROI rectangle is drawn on the HWindow."
    - "When AlgorithmType == TwoLineIntersect, the Line1 rectangle continues to render exactly as before (Line1_* values, 'L1' label)."
    - "When AlgorithmType == CircleTwoHorizontal, no Line1/Vertical rectangle is rendered (legacy stale Line1_* INI values are no longer drawn)."
    - "Line2 / Circle / Horizontal_A/B / detected-line / raw-edge-point overlays render unchanged (no regression)."
  artifacts:
    - path: "WPF_Example/Halcon/Display/HalconDisplayService.cs"
      provides: "RenderDatumOverlay split-by-algorithm Line1/Vertical rectangle rendering"
      contains: "datum.AlgorithmTypeEnum == EDatumAlgorithm.VerticalTwoHorizontal"
  key_links:
    - from: "WPF_Example/Halcon/Display/HalconDisplayService.cs RenderDatumOverlay"
      to: "DatumConfig.Vertical_Row/Col/Phi/Length1/Length2"
      via: "HOperatorSet.DispRectangle2 inside VerticalTwoHorizontal branch"
      pattern: "DispRectangle2.*Vertical_Row.*Vertical_Col.*Vertical_Phi.*Vertical_Length1.*Vertical_Length2"
---

<objective>
Fix VerticalTwoHorizontal Datum: render the Vertical ROI rectangle so the user can see and re-teach the vertical search box.

Purpose: Phase 14-03 W4-A migrated the vertical search slot from `Line1_*` to `Vertical_*` in the data model and write paths (DatumFindingService, MainView teach write-back, hit-test). RenderDatumOverlay was not updated, so when the user selects VerticalTwoHorizontal and draws a vertical ROI, `Vertical_Length1/2` are populated but `Line1_Length1/2` stay 0 → the rectangle never renders → ROI is invisible → user cannot teach → A/B detected edges (gated by `LastTeachSucceeded`) also never appear. One render-side change closes both visible symptoms.

Output: `RenderDatumOverlay` in `HalconDisplayService.cs` renders the correct ROI rectangle for each `EDatumAlgorithm` value: `TwoLineIntersect` → Line1, `VerticalTwoHorizontal` → Vertical, `CircleTwoHorizontal` → neither (Line1 unused).
</objective>

<execution_context>
@$HOME/.claude/get-shit-done/workflows/execute-plan.md
</execution_context>

<context>
@./CLAUDE.md
@.planning/STATE.md

<interfaces>
<!-- Extracted from DatumConfig.cs and EDatumAlgorithm.cs — executor uses these directly, no exploration needed. -->

EDatumAlgorithm enum (WPF_Example/Custom/Sequence/Inspection/EDatumAlgorithm.cs):
```csharp
namespace ReringProject.Sequence
{
    public enum EDatumAlgorithm
    {
        TwoLineIntersect,           // Line1 ∩ Line2
        CircleTwoHorizontal,        // Circle center vertical line ∩ Horizontal A∪B
        VerticalTwoHorizontal,      // Vertical ROI ∩ Horizontal A∪B
    }
}
```

DatumConfig fields used by this fix (WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs):
```csharp
// Algorithm selector (string-backed for ParamBase, exposed as enum via AlgorithmTypeEnum)
public EDatumAlgorithm AlgorithmTypeEnum { get; }   // already used at HalconDisplayService.cs line 454, 464, 477, 493, 566

// Line1 group (TwoLineIntersect)
public double Line1_Row, Line1_Col, Line1_Phi, Line1_Length1, Line1_Length2;

// Vertical group (VerticalTwoHorizontal) — populated by EnsurePerRoiDefaults() one-time migration
// from Line1_* if Vertical_Length1 == 0 (DatumConfig.cs:455-461). Idempotent.
public double Vertical_Row, Vertical_Col, Vertical_Phi, Vertical_Length1, Vertical_Length2;
```

Existing helper used inside RenderDatumOverlay:
```csharp
private void DrawRoiLabel(HWindow window, double row, double col, double phi,
                          double length1, double length2, string label);
```
</interfaces>

<existing_code>
<!-- HalconDisplayService.cs lines 444-461 — current implementation to replace. Allman braces (this block already uses Allman). -->

```csharp
// Draw Line1 ROI as Rectangle2
if (datum.Line1_Length1 > 0 && datum.Line1_Length2 > 0)
{
    HOperatorSet.DispRectangle2(window,
        datum.Line1_Row, datum.Line1_Col, datum.Line1_Phi,
        datum.Line1_Length1, datum.Line1_Length2);

    //260424 hbk Phase 12 Gap-2 — ROI 라벨 (수직/수평/라인 구분 가시화)
    //  TwoLineIntersect: "L1" / VerticalTwoHorizontal: "Vert" / CircleTwoHorizontal: Line1 미사용이므로 라벨 생략
    string line1Label = null;
    if (datum.AlgorithmTypeEnum == EDatumAlgorithm.TwoLineIntersect) line1Label = "L1";
    else if (datum.AlgorithmTypeEnum == EDatumAlgorithm.VerticalTwoHorizontal) line1Label = "Vert";
    if (line1Label != null)
    {
        DrawRoiLabel(window, datum.Line1_Row, datum.Line1_Col, datum.Line1_Phi,
            datum.Line1_Length1, datum.Line1_Length2, line1Label);
    }
}
```

Bug: `VerticalTwoHorizontal` reads `Line1_*` (which is 0 after a fresh vertical-only draw), so the `if (datum.Line1_Length1 > 0 && datum.Line1_Length2 > 0)` gate fails and nothing renders.
</existing_code>
</context>

<tasks>

<task type="auto">
  <name>Task 1: Split Line1/Vertical rectangle rendering by AlgorithmType in RenderDatumOverlay</name>
  <files>WPF_Example/Halcon/Display/HalconDisplayService.cs</files>
  <action>
Replace the existing `// Draw Line1 ROI as Rectangle2` block at lines 444-461 with an algorithm-typed split. Keep all surrounding code (lines 430-443 setup, lines 463+ Line2/Circle/Horizontal/origin-cross/detected-line/raw-edge blocks) untouched.

New block (Allman braces, matching this file's newer Halcon module style):

```csharp
                //260428 hbk W4-A 후속 — RenderDatumOverlay 슬롯 분기 수정
                //  Phase 14-03 W4-A 에서 VerticalTwoHorizontal 의 수직 검색 ROI 를 Line1_* → Vertical_* 슬롯으로 이동했으나
                //  렌더 경로가 갱신되지 않아 사용자가 Vertical ROI 를 그려도 사각형이 표시되지 않는 버그가 있었음.
                //  AlgorithmType 별로 그릴 슬롯을 분기:
                //    TwoLineIntersect       → Line1_*  (기존 동작 보존, "L1" 라벨)
                //    VerticalTwoHorizontal  → Vertical_* (신규, "Vert" 라벨)
                //    CircleTwoHorizontal    → 둘 다 미사용 (legacy INI 의 Line1_* 잔류값이 더 이상 잘못 렌더되지 않음)
                if (datum.AlgorithmTypeEnum == EDatumAlgorithm.TwoLineIntersect)
                {
                    if (datum.Line1_Length1 > 0 && datum.Line1_Length2 > 0)
                    {
                        HOperatorSet.DispRectangle2(window,
                            datum.Line1_Row, datum.Line1_Col, datum.Line1_Phi,
                            datum.Line1_Length1, datum.Line1_Length2);

                        DrawRoiLabel(window, datum.Line1_Row, datum.Line1_Col, datum.Line1_Phi,
                            datum.Line1_Length1, datum.Line1_Length2, "L1");
                    }
                }
                else if (datum.AlgorithmTypeEnum == EDatumAlgorithm.VerticalTwoHorizontal)
                {
                    if (datum.Vertical_Length1 > 0 && datum.Vertical_Length2 > 0)
                    {
                        HOperatorSet.DispRectangle2(window,
                            datum.Vertical_Row, datum.Vertical_Col, datum.Vertical_Phi,
                            datum.Vertical_Length1, datum.Vertical_Length2);

                        DrawRoiLabel(window, datum.Vertical_Row, datum.Vertical_Col, datum.Vertical_Phi,
                            datum.Vertical_Length1, datum.Vertical_Length2, "Vert");
                    }
                }
                // CircleTwoHorizontal: Line1/Vertical 모두 렌더하지 않음 (의도적). Horizontal A/B + Circle 만 아래 블록에서 그림.
```

Constraints:
- Do NOT modify lines 463-517 (Line2 + Circle + Horizontal A/B blocks).
- Do NOT modify lines 519-538 (origin cross).
- Do NOT modify lines 540-593 (detected lines + raw edge points + Vertical raw orange points).
- Do NOT change `color`/`lineWidth` setup at lines 436-442.
- Use C# 7.2 syntax only (no switch expressions, no pattern matching beyond what's shown).
- Allman braces (this block already uses them — preserve).
- Comment header MUST be `//260428 hbk` per project convention.
  </action>
  <verify>
    <automated>powershell -NoProfile -Command "& { $f = 'WPF_Example/Halcon/Display/HalconDisplayService.cs'; $c = Get-Content $f -Raw; $checks = @{ 'verticalBranch' = ($c -match 'EDatumAlgorithm\.VerticalTwoHorizontal[\s\S]{0,400}DispRectangle2[\s\S]{0,200}Vertical_Row[\s\S]{0,200}Vertical_Length1[\s\S]{0,50}Vertical_Length2'); 'twoLineBranch' = ($c -match 'EDatumAlgorithm\.TwoLineIntersect[\s\S]{0,400}DispRectangle2[\s\S]{0,200}Line1_Row[\s\S]{0,200}Line1_Length1[\s\S]{0,50}Line1_Length2'); 'vertLabel' = ($c -match 'DrawRoiLabel\([^;]*Vertical_Row[^;]*\"Vert\"'); 'l1Label' = ($c -match 'DrawRoiLabel\([^;]*Line1_Row[^;]*\"L1\"'); 'commentTag' = ($c -match '//260428 hbk'); 'noUnconditionalLine1' = (-not ($c -match '(?ms)// Draw Line1 ROI as Rectangle2\s*\r?\n\s*if \(datum\.Line1_Length1 > 0 && datum\.Line1_Length2 > 0\)')) }; $checks.GetEnumerator() | ForEach-Object { Write-Host ($_.Key + ': ' + $_.Value) }; if ($checks.Values -contains $false) { exit 1 } else { exit 0 } }"</automated>
  </verify>
  <done>
- File `WPF_Example/Halcon/Display/HalconDisplayService.cs` contains an `if/else if` split on `AlgorithmTypeEnum` for Line1 vs Vertical rectangle rendering.
- TwoLineIntersect branch calls `DispRectangle2` with `Line1_*` and `DrawRoiLabel(..., "L1")`.
- VerticalTwoHorizontal branch calls `DispRectangle2` with `Vertical_*` and `DrawRoiLabel(..., "Vert")`.
- CircleTwoHorizontal falls through with no Line1/Vertical rectangle rendered.
- The original unconditional `if (datum.Line1_Length1 > 0 && datum.Line1_Length2 > 0)` block (preceded by `// Draw Line1 ROI as Rectangle2`) is removed.
- Comment block tagged `//260428 hbk` per project convention.
- Solution builds (manual smoke build is part of the next checkpoint task; automated check above is sufficient for this code-only task).
  </done>
</task>

<task type="checkpoint:human-verify" gate="blocking">
  <what-built>RenderDatumOverlay split-by-AlgorithmType: Vertical ROI now renders for VerticalTwoHorizontal.</what-built>
  <how-to-verify>
1. Build `DatumMeasurement.exe` in Debug/x64 (the project's standard config) and launch it.
2. Open any recipe with a Datum and switch its `AlgorithmType` to `VerticalTwoHorizontal` (PropertyGrid).
3. Draw a fresh Vertical ROI on the canvas. **Expected:** A blue (or cyan if selected) rotated rectangle appears immediately, labeled `Vert`.
4. Click "Teach Datum" / run the teach step. **Expected:** A/B detected raw edge points (green / lime green crosses) appear after success — confirms the teach actually ran with the visible Vertical ROI.
5. Switch the same Datum back to `TwoLineIntersect`. **Expected:** Line1 rectangle still renders with its `L1` label (regression check — Phase 12 behavior preserved).
6. Switch to `CircleTwoHorizontal` on a Datum that has legacy `Line1_*` values in INI. **Expected:** No L1/Vert rectangle is drawn; only Circle + Horizontal A/B (intended behavior — stale Line1 no longer leaks).
  </how-to-verify>
  <resume-signal>Type "approved" if all four scenarios match expected, or describe any rendering anomaly.</resume-signal>
</task>

</tasks>

<verification>
- TwoLineIntersect: Line1 rectangle + "L1" label renders when `Line1_Length1/2 > 0`.
- VerticalTwoHorizontal: Vertical rectangle + "Vert" label renders when `Vertical_Length1/2 > 0` (was invisible before fix).
- CircleTwoHorizontal: no Line1/Vertical rectangle (was incorrectly rendering stale Line1 from legacy INI before fix).
- All non-Line1 overlays (Line2, Circle, Horizontal A/B, origin cross, detected lines, raw edge points incl. Vertical orange) unchanged.
- INI hard-compat path via `EnsurePerRoiDefaults()` still migrates legacy `Line1_* → Vertical_*` on first load — no data action needed.
</verification>

<success_criteria>
- User can draw a Vertical ROI under VerticalTwoHorizontal and see the rectangle on screen.
- User can teach Datum with the visible Vertical ROI; A/B detected edges appear on success.
- TwoLineIntersect and CircleTwoHorizontal flows show no visual regression.
- Single file changed: `WPF_Example/Halcon/Display/HalconDisplayService.cs`.
</success_criteria>

<output>
After completion, no SUMMARY needed (quick fix). Confirm via the human-verify checkpoint.
</output>
