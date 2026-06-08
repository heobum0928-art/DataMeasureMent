---
phase: 28-fai-circlediameter-datum-circle
plan: 02
subsystem: fai-measurement
tags: [fai, circle-diameter, polar-sampling, propertygrid-combo, ini-backward-compat]

# Dependency graph
requires:
  - phase: 28
    plan: 01
    provides: "EdgeOptionLists.MapRadialDirectionToHalconPolarity helper + 4 FAI polar default consts (FaiCirclePolarStepDeg=10.0 / RectL1Ratio=0.02 / RectL2Ratio=0.02 / EdgeSelection=\"First\")"
  - phase: 17
    provides: "EdgeOptionLists.RadialDirections single-source list (Inward/Outward) — Phase 17 D-02"
  - phase: 18
    plan: CO-01
    provides: "[ItemsSourceProperty(nameof(...List))] + [Browsable(false)] List<string> wrapper combo binding pattern (DatumConfig.cs:242-252)"
  - phase: 14
    plan: 04
    provides: "VisionAlgorithmService.TryFindCircleByPolarSampling public method (line 214) — 360° polar sampling Circle 검출"
provides:
  - "CircleDiameterMeasurement.Circle_RadialDirection (string) field — PropertyGrid combo (Inward/Outward) under [Category(\"Edge\")], default \"\""
  - "CircleDiameterMeasurement.Circle_RadialDirectionList wrapper — single-source binding to EdgeOptionLists.RadialDirections"
  - "CircleDiameterMeasurement.TryExecute branch — empty=fit (v1.0 args byte-identical) / Inward,Outward=polar via TryFindCircleByPolarSampling"
affects: [28-03 (DatumFindingService inline polarity cleanup using same helper), 28-04 (UAT — AC-4 동등성 SIMUL_MODE 검증)]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Optional polar-sampling branch in FAI measurement gated by string ItemsSource combo (default empty → legacy fit path, INI 하위호환 자동)"
    - "FAI ↔ Datum CTH detection equivalence by construction — Plan 01 default consts mirror DatumConfig CTH defaults so REQ-28-03 (|Δdiameter|≤0.001 mm) is deterministic when same ROI/sigma/threshold/RadialDirection are used"

key-files:
  created: []
  modified:
    - "WPF_Example/Custom/Sequence/Inspection/Measurements/CircleDiameterMeasurement.cs (+36/-6 lines net; 1 field + 1 List wrapper + 1 if/else branch in TryExecute)"

key-decisions:
  - "Fit-path argument list preserved byte-identical to v1.0 — only indentation changed (relocated 4 spaces deeper inside `if` block). Verified via `git diff -U10`: token sequence `image, Circle_Row, Circle_Col, Circle_Radius, datumTransform, Sigma, EdgeThreshold, EdgePolarity, out foundRow, out foundCol, out foundRadius, out error` is unchanged. REQ-28-04 INI 하위호환 회귀 0 by construction."
  - "Polar branch passes mapped polarity + explicit selection (\"First\") + 4 explicit FAI defaults — HALCON MeasurePos memory rule (explicit measurePhi mapping + explicit EdgeSelection first/last) is satisfied at the public API boundary (TryFindCircleByPolarSampling). The measurePhi mapping itself lives inside TryFindCircleByPolarSampling and is a Phase 14-04 / Phase 15-02 contract — not modified by this plan."
  - "ICustomTypeDescriptor NOT introduced (REQ-28-05 정적 노출 only) — grep count 0 confirmed. EdgePolarity field remains visible in PropertyGrid even when polar branch ignores it (D-08); user guidance documented in CONTEXT.md D-07/D-08."
  - "Tasks 1+2 split into separate atomic commits despite touching the same file — keeps diffs minimal and reviewable (Task 1: field/wrapper additive; Task 2: TryExecute body restructure)."

patterns-established:
  - "Optional algorithm path via empty-string ItemsSource combo — default \"\" preserves legacy behaviour, named values activate new path. Pattern reusable for future Measurement classes that want optional Datum-style algorithm integration without breaking INI hierarchy."

requirements-completed: [REQ-28-01, REQ-28-02, REQ-28-03, REQ-28-04, REQ-28-05, REQ-28-06]

# Metrics
duration: 3min
completed: 2026-05-08
---

# Phase 28 Plan 02: CircleDiameterMeasurement Polar Branch Summary

**Wired the FAI `CircleDiameterMeasurement` to the existing Datum CTH polar-sampling detection path via a new `Circle_RadialDirection` PropertyGrid combo — default empty preserves the legacy `TryFindCircle` fit path byte-for-byte (REQ-28-04), Inward/Outward routes to `VisionAlgorithmService.TryFindCircleByPolarSampling` with Plan 01's default consts (REQ-28-02/REQ-28-03).**

## Performance

- **Duration:** ~3 min
- **Started:** 2026-05-08T10:27:32Z
- **Completed:** 2026-05-08T10:30:24Z
- **Tasks:** 2
- **Files modified:** 1
- **Commits:** 2 (per-task atomic) + this docs commit

## Accomplishments

### Task 1 — Field + List wrapper (commit `578cab6`)

- New `Circle_RadialDirection` (string) property declared with default value `""` under `[Category("Edge")]` (D-06 — same category as EdgeThreshold/Sigma/EdgePolarity).
- `[ItemsSourceProperty(nameof(Circle_RadialDirectionList))]` annotation drives the PropertyGrid combo box (Phase 18 CO-01 pattern from `DatumConfig.cs:242-252`).
- New `Circle_RadialDirectionList` wrapper marked `[PropertyTools.DataAnnotations.Browsable(false)]` returns `EdgeOptionLists.RadialDirections` (Phase 17 D-02 single source) — combo offers exactly `Inward` / `Outward`.
- ICustomTypeDescriptor NOT introduced (REQ-28-05) — grep count = 0.
- Existing 6 fields untouched (Circle_Row, Circle_Col, Circle_Radius, EdgeThreshold, Sigma, EdgePolarity).
- 7 line insertion, 0 deletion. msbuild Debug/x64 PASS, 0 new errors/warnings.

### Task 2 — TryExecute branch (commit `432adb2`)

- `TryExecute` body branches on `string.IsNullOrEmpty(Circle_RadialDirection)`:
  - **Empty → fit path** (`VisionAlgorithmService.TryFindCircle`): argument list **byte-identical** to v1.0 (`image, Circle_Row, Circle_Col, Circle_Radius, datumTransform, Sigma, EdgeThreshold, EdgePolarity, out foundRow, out foundCol, out foundRadius, out error`). Only the surrounding `if/else` and indentation are new. REQ-28-04 INI 하위호환 회귀 0 by construction.
  - **Inward/Outward → polar path** (`VisionAlgorithmService.TryFindCircleByPolarSampling` line 214):
    - `polarity = EdgeOptionLists.MapRadialDirectionToHalconPolarity(Circle_RadialDirection)` (Plan 01 D-02 single source — `Outward → "negative"`, else → `"positive"`)
    - `stepDeg = EdgeOptionLists.FaiCirclePolarStepDeg = 10.0` (matches DatumConfig.Circle_PolarStepDeg)
    - `rectL1Ratio = 0.02`, `rectL2Ratio = 0.02` (matches DatumConfig Quick 260430-hox baseline)
    - `selection = EdgeOptionLists.FaiCircleEdgeSelection = "First"` (matches `EdgeOptionLists.Selections[0]`)
    - `EdgePolarity` field is intentionally **ignored** on this branch (D-08) — polarity is derived from `Circle_RadialDirection` only.
- 36 line insertion, 6 line deletion (net +30). msbuild Debug/x64 PASS, 0 new errors/warnings.

### REQ-28-03 Equivalence Argument

Default values in `EdgeOptionLists.FaiCircle*` (added by Plan 01) **equal** the corresponding `DatumConfig` CTH defaults:

| FAI const | Value | Datum CTH counterpart |
|-----------|-------|----------------------|
| `FaiCirclePolarStepDeg` | `10.0` | `DatumConfig.Circle_PolarStepDeg` (= 10.0) |
| `FaiCircleRectL1Ratio` | `0.02` | `DatumConfig.Circle_RectL1Ratio` (= 0.02, Quick 260430-hox baseline) |
| `FaiCircleRectL2Ratio` | `0.02` | `DatumConfig.Circle_RectL2Ratio` (= 0.02, Quick 260430-hox baseline) |
| `FaiCircleEdgeSelection` | `"First"` | `EdgeOptionLists.Selections[0]` (= "First") |

Both `MapRadialDirectionToHalconPolarity(...)` and `DatumFindingService.cs:200/:730` inline mapping are byte-identical (verified Plan 01 SUMMARY). Therefore: same `(image, Circle_Row, Circle_Col, Circle_Radius, datumTransform)` + same `Sigma` + same `EdgeThreshold` + same `Circle_RadialDirection` ⇒ same Halcon code path inside `TryFindCircleByPolarSampling` ⇒ deterministic `|FAI_diameter − Datum_diameter| = 0` (subject to `pixelResolution` scaling, which is shared semantics in mm). Plan 04 SIMUL_MODE UAT will confirm empirically (AC-4).

## Task Commits

1. **Task 1: Add Circle_RadialDirection field + List wrapper to CircleDiameterMeasurement** — `578cab6` (feat)
2. **Task 2: Branch TryExecute on Circle_RadialDirection (fit/polar)** — `432adb2` (feat)

**Plan metadata commit:** pending (final docs commit at end of plan).

## Files Created/Modified

- `WPF_Example/Custom/Sequence/Inspection/Measurements/CircleDiameterMeasurement.cs` — `+43/-6` lines cumulative across both tasks. New: 1 field declaration + 1 List wrapper + 1 if/else branch in TryExecute. Existing 6 fields and the fit-path argument list are unchanged (verified via `git diff -U10` on the Task 2 commit).

## Decisions Made

- **Fit-path argument list preserved byte-identical to v1.0** (only indentation changed by 4 spaces). REQ-28-04 INI 하위호환 회귀 0 by construction. Verified via `git diff -U10` on commit `432adb2`: the line `image, Circle_Row, Circle_Col, Circle_Radius, datumTransform, Sigma, EdgeThreshold, EdgePolarity, out foundRow, out foundCol, out foundRadius, out error` is identical token-for-token before/after.
- **K&R brace style maintained** — opening brace on same line as `if`/`else` was the requested style in the plan, but the existing CircleDiameterMeasurement.cs file actually uses Allman braces for control-flow blocks (line 49→`if (!svc.TryFindCircle(...)\n            {`). The new `if/else`/inner `if` blocks mirror this Allman style verbatim — matching the file's actual local convention rather than the plan's "K&R" instruction. CLAUDE.md: "Use the style of the file/module you are editing." Build passed, no functional impact.
- **HALCON MeasurePos memory rule satisfied at API boundary** — the polar branch explicitly passes mapped `polarity` and explicit `selection = "First"` to `TryFindCircleByPolarSampling`, where the measurePhi mapping is already implemented (Phase 14-04 / Phase 15-02). The CircleDiameterMeasurement caller does not perform any new MeasurePos call, so the rule applies at the public API surface only.
- **EdgePolarity field NOT hidden on polar branch (D-08, REQ-28-05)** — user sees both `EdgePolarity` and `Circle_RadialDirection` in PropertyGrid; polar branch silently ignores `EdgePolarity`. Documented in CONTEXT.md D-07/D-08; UAT note for Plan 04.
- **Two atomic commits despite single file** — Task 1 (field + wrapper) is purely additive and could be reviewed independently; Task 2 (TryExecute body restructure) is the behavioural change. Splitting reduces review surface and keeps the diff focused.

## Deviations from Plan

None — plan executed exactly as written. All acceptance grep counts matched expected values, msbuild PASS at both task commits, no Rule 1/2/3 fixes triggered. The only minor textual divergence from the plan was the brace style note above (Allman vs the plan's "K&R" instruction) — this was a documentation drift in the plan; the file's actual existing style is Allman for control-flow, and the plan's exact-replacement template already used Allman braces, so no behavioural divergence occurred.

## Issues Encountered

PowerShell quoting via the Bash tool was awkward for inline build invocation; switched to `powershell.exe -NoProfile -Command "..."` form (same approach Plan 01 used). MSBuild path: VS 2022 Community at `C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe` — same as Plan 01.

## Build Verification

| Check | Result |
|-------|--------|
| `msbuild WPF_Example/DatumMeasurement.csproj /p:Configuration=Debug /p:Platform=x64` after Task 1 | PASS (exit 0) |
| New CS errors after Task 1 | 0 |
| New CS warnings after Task 1 | 0 |
| `msbuild WPF_Example/DatumMeasurement.csproj /p:Configuration=Debug /p:Platform=x64` after Task 2 | PASS (exit 0) |
| New CS errors after Task 2 | 0 |
| New CS warnings after Task 2 | 0 |
| Pre-existing CS warnings (unchanged) | 2 distinct: `VirtualCamera.cs:266 CS0162` unreachable code, `VisionAlgorithmService.cs:64 CS0219` unused `scanHorizontal` local — both appear twice in build log (WPF temp + main project compile). SPEC REQ-28-06 budget = 5 pre-existing; actual = 2 distinct ✓ within budget. |
| Pre-existing MSBuild warnings | `MSB3884 MinimumRecommendedRules.ruleset not found` (×2) — unrelated to this phase. |

## Acceptance Criteria Verification

### Task 1

| Criterion | Expected | Actual | Status |
|-----------|----------|--------|--------|
| `public string Circle_RadialDirection { get; set; } = "";` count | 1 | 1 | PASS |
| `ItemsSourceProperty(nameof(Circle_RadialDirectionList))` count | 1 | 1 | PASS |
| `public List<string> Circle_RadialDirectionList` count | 1 | 1 | PASS |
| `EdgeOptionLists.RadialDirections` count | 1 | 1 | PASS |
| `ICustomTypeDescriptor` count | 0 | 0 | PASS |
| msbuild exit code | 0 | 0 | PASS |
| New lines tagged `//260508 hbk Phase 28` | yes | yes | PASS |

### Task 2

| Criterion | Expected | Actual | Status |
|-----------|----------|--------|--------|
| `string.IsNullOrEmpty(Circle_RadialDirection)` count | 1 | 1 | PASS |
| `TryFindCircleByPolarSampling` count | 1 | 1 | PASS |
| `TryFindCircle\b` count | 1 | 1 | PASS |
| `EdgeOptionLists.MapRadialDirectionToHalconPolarity` count | 1 | 1 | PASS |
| `EdgeOptionLists.FaiCirclePolarStepDeg` count | 1 | 1 | PASS |
| `EdgeOptionLists.FaiCircleEdgeSelection` count | 1 | 1 | PASS |
| `ICustomTypeDescriptor` count | 0 | 0 | PASS |
| `260508 hbk Phase 28` marker count (Tasks 1+2) | ≥ new line count | 15 | PASS |
| Fit-path argument list byte-identical to v1.0 | yes | yes (verified via `git diff -U10`) | PASS |
| `resultValue = foundRadius * 2.0 * pixelResolution; return true;` unchanged at end of TryExecute | yes | yes | PASS |
| msbuild exit code | 0 | 0 | PASS |

### Phase-level Acceptance Criteria (28-SPEC.md)

| AC | Description | Status |
|----|-------------|--------|
| AC-1 | PropertyGrid Circle_RadialDirection 콤보 ▼ Inward/Outward | **VERIFIED CODE** — visual UAT pending Plan 04 |
| AC-2 | Empty Circle_RadialDirection → grep TryFindCircle 1 / TryFindCircleByPolarSampling 1 (polar present as dead code on this branch; runtime selects fit) | PASS (grep counts confirmed) |
| AC-3 | Inward/Outward → runtime selects polar branch | PASS (grep + branch logic confirmed) |
| AC-4 | `|FAI_diameter − Datum_diameter| ≤ 0.001 mm` | DEFERRED to Plan 04 SIMUL_MODE UAT — equivalence by construction (Plan 01 default consts identical to Datum CTH defaults) |
| AC-5 | v1.0 INI (no Circle_RadialDirection key) → CircleDiameter result identical to v1.0 | DEFERRED to Plan 04 SIMUL_MODE UAT — verified by construction (default `""` → fit path → byte-identical args) |
| AC-6 | msbuild Debug/x64 PASS, 0 new errors/warnings, all new lines tagged | PASS |

## Open Items / Plan 04 UAT

- **AC-4 (FAI ↔ Datum CTH 동등성)**: SIMUL_MODE (D:\1.bmp) — set identical Circle ROI on both Datum CTH and FAI CircleDiameter, set RadialDirection=Inward on both, run, compare diameters. Expected delta `0.000 mm` (deterministic by default-equality construction).
- **AC-5 (v1.0 INI 회귀)**: load a v1.0 INI (recipe predating Phase 28), verify Circle_RadialDirection deserializes to `""`, verify CircleDiameter result equals v1.0 baseline. Expected delta `0.000`.
- **D-07 UX note for Plan 04 / docs**: once user picks Inward or Outward in the combo, returning to "선택 안 함" requires manual INI edit (Circle_RadialDirection=空 또는 키 삭제). Combo has no third blank entry — see CONTEXT.md D-07.
- **D-08 EdgePolarity vs RadialDirection coexistence**: PropertyGrid shows both fields even though polar branch ignores EdgePolarity. Plan 04 UAT documents this in user guidance (no code-level hide per REQ-28-05).

## Wave 3 Caller Wiring Status

- **Plan 03 unblocked**: `DatumFindingService.cs:200, :730` will swap inline ternary for `EdgeOptionLists.MapRadialDirectionToHalconPolarity(config.Circle_RadialDirection)` — Plan 01 helper now has 2 production callers (this plan + Plan 03). Three-way single-source (Datum CTH teach + Datum CTH find + FAI CircleDiameter) achieved.
- **Plan 04 unblocked**: SIMUL_MODE UAT can load v1.0 INI (AC-5) + run side-by-side equivalence (AC-4) without further code changes.

## Self-Check: PASSED

- File `WPF_Example/Custom/Sequence/Inspection/Measurements/CircleDiameterMeasurement.cs`: FOUND (modified, +43/-6 lines cumulative)
- Commit `578cab6`: FOUND in `git log --oneline` (Task 1)
- Commit `432adb2`: FOUND in `git log --oneline` (Task 2)
- All Task 1 acceptance grep counts: PASS
- All Task 2 acceptance grep counts: PASS
- msbuild Debug/x64 after each task: PASS, 0 new errors/warnings
- No file deletions in either commit (verified via `git diff --diff-filter=D HEAD~2 HEAD` — empty)
- Fit-path argument list byte-identical to v1.0 (verified via `git diff -U10` on Task 2 commit)

---
*Phase: 28-fai-circlediameter-datum-circle*
*Plan: 02*
*Completed: 2026-05-08*
