---
phase: 28-fai-circlediameter-datum-circle
plan: 01
subsystem: vision-algorithms
tags: [edgeoption, polarity-mapping, polar-defaults, halcon, fai, datum-cth]

# Dependency graph
requires:
  - phase: 17
    provides: "EdgeOptionLists.RadialDirections single-source list (Inward/Outward) — Phase 17 D-02"
  - phase: 14
    provides: "DatumConfig Circle_PolarStepDeg / RectL1Ratio / RectL2Ratio defaults (10.0 / 0.02 / 0.02)"
  - phase: 18
    provides: "Quick 260430-hox baseline RectL1/L2 default 0.02 confirmed"
provides:
  - "EdgeOptionLists.MapRadialDirectionToHalconPolarity(string) static helper — single-source polarity mapping"
  - "EdgeOptionLists.FaiCirclePolarStepDeg const = 10.0"
  - "EdgeOptionLists.FaiCircleRectL1Ratio const = 0.02"
  - "EdgeOptionLists.FaiCircleRectL2Ratio const = 0.02"
  - "EdgeOptionLists.FaiCircleEdgeSelection const = \"First\""
affects: [28-02 (CircleDiameterMeasurement polar branch), 28-03 (DatumFindingService inline cleanup), 28-04 (UAT)]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Single-source polarity mapping helper (eliminates inline string.Equals(...) ? : duplication)"
    - "FAI-side polar default consts mirror Datum CTH defaults — guarantees REQ-28-03 detection equivalence by construction"

key-files:
  created: []
  modified:
    - "WPF_Example/Custom/Sequence/Inspection/EdgeOptionLists.cs (+18 lines, lines 1-27 unchanged)"

key-decisions:
  - "Helper uses fully-qualified System.StringComparison.OrdinalIgnoreCase — no new `using System;` directive needed (matches DatumFindingService.cs:200/:730 inline form for byte-identical semantics)"
  - "Allman braces used in new method body — file had no prior methods to mirror; Allman matches common static-class pattern and avoids C# 7.2 expression-bodied member style"
  - "4 const values placed BEFORE helper (data → behaviour ordering); both groups appended after RadialDirections (Phase 17 D-02) for related-symbol grouping"

patterns-established:
  - "Single-source polarity helper: Replace inline `string.Equals(x, \"Outward\", OrdinalIgnoreCase) ? \"negative\" : \"positive\"` with `EdgeOptionLists.MapRadialDirectionToHalconPolarity(x)` — Plan 03 will apply this to DatumFindingService 2 sites"
  - "FAI-side polar defaults via static const class — keeps PropertyGrid surface unchanged (D-04 SPEC out-of-scope) while enabling polar code path"

requirements-completed: [REQ-28-01, REQ-28-02]

# Metrics
duration: 6min
completed: 2026-05-08
---

# Phase 28 Plan 01: EdgeOptionLists Polarity Helper + FAI Polar Defaults Summary

**Added `MapRadialDirectionToHalconPolarity(string)` static helper plus 4 FAI-side polar default consts (`FaiCirclePolarStepDeg=10.0`, `FaiCircleRectL1Ratio=0.02`, `FaiCircleRectL2Ratio=0.02`, `FaiCircleEdgeSelection="First"`) to `EdgeOptionLists` — foundation symbols for Wave 2 plans, no behaviour change yet.**

## Performance

- **Duration:** ~6 min
- **Started:** 2026-05-08T10:17:30Z
- **Completed:** 2026-05-08T10:23:14Z
- **Tasks:** 1
- **Files modified:** 1

## Accomplishments

- New static helper `EdgeOptionLists.MapRadialDirectionToHalconPolarity(string)` returns `"negative"` for `"Outward"` (case-insensitive) and `"positive"` for everything else (null, empty, `"Inward"`) — byte-identical to existing inline expressions at `DatumFindingService.cs:200` and `:730`.
- 4 new public consts on `EdgeOptionLists`, each value matching its Datum CTH counterpart in `DatumConfig.cs:219-222` (lines 219/221/222) and `EdgeOptionLists.Selections[0]`:
  - `FaiCirclePolarStepDeg = 10.0` ↔ `DatumConfig.Circle_PolarStepDeg = 10.0`
  - `FaiCircleRectL1Ratio = 0.02` ↔ `DatumConfig.Circle_RectL1Ratio = 0.02` (Quick 260430-hox baseline)
  - `FaiCircleRectL2Ratio = 0.02` ↔ `DatumConfig.Circle_RectL2Ratio = 0.02` (Quick 260430-hox baseline)
  - `FaiCircleEdgeSelection = "First"` ↔ `EdgeOptionLists.Selections[0] = "First"`
- These default-equality pairings make REQ-28-03 (`|FAI_diameter − Datum_diameter| ≤ 0.001 mm`) deterministic by construction once Plan 02 wires the polar branch with these consts.
- All 18 new lines tagged `//260508 hbk Phase 28` per project comment convention.
- Zero callers wired in this plan — strictly foundation; Plan 02 (CircleDiameterMeasurement) and Plan 03 (DatumFindingService inline cleanup) will consume these symbols.

## Task Commits

1. **Task 1: Add MapRadialDirectionToHalconPolarity helper + 4 FAI polar default consts to EdgeOptionLists** — `be4d267` (feat)

**Plan metadata commit:** pending (final docs commit at end of plan)

## Files Created/Modified

- `WPF_Example/Custom/Sequence/Inspection/EdgeOptionLists.cs` — appended 18 lines after the existing `RadialDirections` declaration (4 const + 1 static method + section/inline comments). Lines 1-27 untouched.

## Decisions Made

- **Fully-qualified `System.StringComparison`**: Helper uses `System.StringComparison.OrdinalIgnoreCase` rather than adding `using System;`. Reasoning: keep the diff minimal (the file previously had only `using System.Collections.Generic;`) and match the inline form at `DatumFindingService.cs:200`/`730` exactly so byte-identical semantics are obvious from inspection.
- **Allman braces** in the new helper method body. The file had no prior methods to mirror, so Allman is a neutral choice and matches common static-class-with-helpers conventions (e.g., `MeasurementAlgorithm.cs`). Did not introduce expression-bodied members (`=>`) per CLAUDE.md QUAL-02 spirit.
- **Const placement before helper** (data → behaviour ordering). Both groups appended after `RadialDirections` (Phase 17 D-02) so related Circle/RadialDirection-adjacent symbols stay grouped.
- **No reordering, no edits to lines 1-27**: confirmed by `git diff -U0` showing only `+` additions starting at line 28.

## Deviations from Plan

None — plan executed exactly as written. Single task, single file, single commit. All acceptance grep counts matched expected values, msbuild PASS, no Rule 1/2/3 fixes triggered.

## Issues Encountered

None. PowerShell quoting via the Bash tool was awkward for inline acceptance grep checks; switched to the Grep tool for verification (faster, no escape issues). MSBuild path discovery required a one-time `Get-ChildItem` lookup since neither `msbuild` nor a hardcoded VS 2019 path was available — used VS 2022 Community MSBuild at `C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe`.

## Build Verification

- **msbuild WPF_Example/DatumMeasurement.csproj /p:Configuration=Debug /p:Platform=x64**: exit 0 (PASS)
- **New CS errors**: 0
- **New CS warnings**: 0
- **Pre-existing CS warnings (unchanged)**: 2 distinct (`VirtualCamera.cs:266 CS0162` unreachable code; `VisionAlgorithmService.cs:64 CS0219` unused `scanHorizontal` local) — each appears twice in build log because WPF temp project (`*_wpftmp.csproj`) and main project both compile the same source. SPEC REQ-28-06 budget = 5 pre-existing; actual = 2 distinct ✓ within budget.
- **Pre-existing MSBuild warnings**: `MSB3884 MinimumRecommendedRules.ruleset not found` (×2 — temp + main) — unrelated.

## Acceptance Criteria Verification

| Criterion | Expected | Actual | Status |
|-----------|----------|--------|--------|
| `MapRadialDirectionToHalconPolarity(string ` declaration count | 1 | 1 | PASS |
| `FaiCirclePolarStepDeg = 10.0` count | 1 | 1 | PASS |
| `FaiCircleRectL1Ratio = 0.02` count | 1 | 1 | PASS |
| `FaiCircleRectL2Ratio = 0.02` count | 1 | 1 | PASS |
| `FaiCircleEdgeSelection = "First"` count | 1 | 1 | PASS |
| `"Outward".*OrdinalIgnoreCase` (helper body + cross-ref comment) count | ≥1 | 2 | PASS |
| `260508 hbk Phase 28` marker count | ≥7 | 10 | PASS |
| `msbuild Debug/x64` exit code | 0 | 0 | PASS |
| Lines 1-27 unchanged | yes | yes (git diff confirmed `@@ -27,0 +28,18 @@`) | PASS |

## Wave 2 Caller Wiring Status

**No Wave 2 caller has been wired in this plan.** This is foundation-only by design (28-01-PLAN.md objective). Specifically:

- `CircleDiameterMeasurement.cs` — unchanged. Plan 02 will add `Circle_RadialDirection` field + branch into `VisionAlgorithmService.TryFindCircleByPolarSampling` using these new consts and helper.
- `DatumFindingService.cs:200, :730` — unchanged. Plan 03 will replace the two inline `string.Equals(...) ? "negative" : "positive"` expressions with calls to the new helper (D-03 cleanup).

Both consumer files already import `ReringProject.Sequence`, so calling `EdgeOptionLists.MapRadialDirectionToHalconPolarity(...)` and the new const fields requires no new `using` directive in either file.

## User Setup Required

None — no external service configuration required. All changes are in-process static class additions with no hardware/SDK/credential surface.

## Next Phase Readiness

- **Plan 02 unblocked**: `CircleDiameterMeasurement.TryExecute` polar branch can call:
  - `EdgeOptionLists.MapRadialDirectionToHalconPolarity(Circle_RadialDirection)` for polarity
  - `EdgeOptionLists.FaiCirclePolarStepDeg / FaiCircleRectL1Ratio / FaiCircleRectL2Ratio / FaiCircleEdgeSelection` as polar parameters
- **Plan 03 unblocked**: `DatumFindingService.cs` lines 200 and 730 can swap inline ternaries for `EdgeOptionLists.MapRadialDirectionToHalconPolarity(config.Circle_RadialDirection)`.
- **REQ-28-03 동등성 path**: Plans 02+03 with these defaults guarantee FAI ↔ Datum CTH detection equivalence by construction — Plan 04 SIMUL_MODE UAT will confirm `≤ 0.001 mm` empirically.
- **No blockers, no concerns.**

## Self-Check: PASSED

- File `WPF_Example/Custom/Sequence/Inspection/EdgeOptionLists.cs`: FOUND (modified, +18 lines)
- Commit `be4d267`: FOUND in `git log --oneline` (HEAD)
- All acceptance grep counts: PASS
- msbuild Debug/x64: PASS, 0 new errors/warnings
- No deletions in commit (verified via `git diff --diff-filter=D HEAD~1 HEAD` — empty)

---
*Phase: 28-fai-circlediameter-datum-circle*
*Plan: 01*
*Completed: 2026-05-08*
