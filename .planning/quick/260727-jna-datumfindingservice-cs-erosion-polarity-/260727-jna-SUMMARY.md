---
phase: 260727-jna
plan: 01
subsystem: vision-measurement
tags: [halcon, gray_erosion, gray_dilation, morphology, datum, polarity, DatumFindingService]

# Dependency graph
requires: []
provides:
  - Polarity-aware directional grayscale morphology pre-processing at both DatumFindingService strip-loop entry points (TryFindLine and TryExtractEdgePoints)
  - gray_dilation now selected instead of gray_erosion when a Datum ROI's polarity is "negative", so burr/protrusion suppression actually works on bright-background/dark-part edges (previously a silent no-op)
affects: [datum-finding, side-datum-3-vertical-roi, erosion-tact-logging]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "useDilation/opName decision block: bool useDilation = string.Equals(polarity, \"negative\", StringComparison.OrdinalIgnoreCase); declared before the try so both the success path and the catch fallback can reference opName in their Trace log format strings"

key-files:
  created: []
  modified:
    - WPF_Example/Halcon/Algorithms/DatumFindingService.cs

key-decisions:
  - "polarity (not direction) is the correct discriminant for erosion-vs-dilation, since this codebase maps polarity=\"negative\" to light-to-dark transition consistently across ComputeMeasurePhi, MeasurementAlgorithm.cs, FAIEdgeMeasurementService.cs, VisionAlgorithmService.cs, and EdgeOptionLists.cs for all 4 direction values"
  - "Case-insensitive match via string.Equals(..., StringComparison.OrdinalIgnoreCase) to mirror this same file's existing ComputeMeasurePhi convention rather than introducing a new comparison style"
  - "positive/all/anything-else falls through to the pre-existing GrayErosion call with identical arguments -- zero behavior change for the common case, degrading safely on unexpected polarity values"

requirements-completed: [FIX-ErosionPolarity-TryFindLine, FIX-ErosionPolarity-TryExtractEdgePoints]

# Metrics
duration: 25min
completed: 2026-07-27
---

# Quick Task 260727-jna: Datum Erosion Polarity Fix Summary

**Directional pre-erosion in DatumFindingService now switches to gray_dilation for polarity="negative" Datum edges (mirrored at both TryFindLine and TryExtractEdgePoints), fixing a silent no-op that left dark-burr suppression completely ineffective on bright-background/dark-part edges like Side_Datum_3's Vertical RtoL ROI.**

## Performance

- **Duration:** ~25 min
- **Completed:** 2026-07-27T05:39:16Z (06ebe92)
- **Tasks:** 2/2
- **Files modified:** 1

## Accomplishments
- `TryFindLine`'s `if (erosion > 0)` block now selects `HOperatorSet.GrayDilation` when `polarity` case-insensitively equals `"negative"`, and keeps `HOperatorSet.GrayErosion` for every other value (byte-identical to prior behavior for positive/all).
- `TryExtractEdgePoints` (the horizontal 2-ROI concat fit path) received the identical mirrored fix, so a negative-polarity Datum no longer behaves differently depending on which ROI role it's used in.
- Both Trace log lines (success tact log and the catch-block fallback log) now report the operator that actually ran (`gray_erosion` or `gray_dilation`) instead of an unconditionally hardcoded "erosion" label, and the success log additionally now includes `polarity=` for diagnosability.
- `erosion <= 0` remains a complete no-op at both sites — zero new `HOperatorSet` calls, existing recipes byte-identical.

## Task Commits

Each task was committed atomically:

1. **Task 1: Make TryFindLine's directional morphology polarity-aware** - `977e182` (fix)
2. **Task 2: Mirror the polarity fix into TryExtractEdgePoints and verify both sites** - `06ebe92` (fix)

_Both commits modify only `WPF_Example/Halcon/Algorithms/DatumFindingService.cs`; the pre-existing unrelated `WPF_Example/DatumMeasurement.csproj` working-tree modification (local `SIMUL_MODE` DefineConstants toggle) was left untouched and excluded from both commits per the task constraints._

## Files Created/Modified
- `WPF_Example/Halcon/Algorithms/DatumFindingService.cs` - Both `if (erosion > 0)` directional-morphology blocks (inside `TryFindLine` and `TryExtractEdgePoints`) now declare `useDilation`/`opName` before their `try` block and branch `HOperatorSet.GrayDilation` vs `HOperatorSet.GrayErosion` accordingly; both Trace log `string.Format` calls (success + catch fallback) interpolate `opName` instead of a hardcoded operator name.

## Decisions Made
- See `key-decisions` in frontmatter. No architectural decisions were required — this was a scoped, mechanical two-site symmetric fix exactly as specified in the plan.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Substituted MSBuild verify command: `//t:Compile` did not route through WPF markup compilation in this environment**
- **Found during:** Task 1 verification
- **Issue:** The plan's specified verify command (`MSBuild WPF_Example/DatumMeasurement.csproj //t:Compile ...`) failed with exit 1 and ~1272 `CS0103`/`CS1061`/`CS5001` errors, ALL in unrelated files (`MainWindow.xaml.cs`, `MainView.xaml.cs`, `BottomVisionView.xaml.cs`, etc.) complaining that XAML-codegen partial-class members (`mainView`, `tabTray`, `InitializeComponent`, `halconViewer`, ...) don't exist in context. Root cause: in this project, `MarkupCompilePass1`/`MarkupCompilePass2` (which inject the generated `.g.cs` partial-class members into the `Compile` item list) are wired as dependencies of the top-level `Build` target, not of the standalone `Compile`/`CoreCompile` target — so invoking `//t:Compile` directly bypassed WPF codegen item injection for this MSBuild invocation, even though the `.g.cs` files themselves happened to already exist on disk (confirmed correct content by direct inspection) from a separate, unrelated background regeneration. Zero of these errors referenced `DatumFindingService.cs`.
- **Fix:** Used `//t:Build` on the same `.csproj` (still not the `.sln`, still scoped to this one project) instead of `//t:Compile`. This target's dependency chain does include the WPF markup-compile passes, so `@(Compile)` was correctly populated within the same invocation. `DatumMeasurement.exe` turned out not to actually be running at the time of verification (confirmed via `tasklist`), so the build completed a full clean compile-and-copy with exit 0, producing only the plan's documented known baseline warnings (CS0618 x5, CS0162 x1) and zero diagnostics referencing `DatumFindingService.cs` -- a strictly stronger verification signal than the plan's originally-envisioned partial `//t:Compile` check, since it confirms a clean compile across the entire project rather than just this one file's syntax.
- **Files modified:** None (verification-only; no source changes resulted from this deviation).
- **Verification:** Re-ran the identical `//t:Build` command after Task 2's edit; exit 0, identical known-baseline-only warning set, zero `DatumFindingService.cs` diagnostics. Combined with the plan's own structural grep checks (all 7/7 matched their `(want N)` annotations), this satisfies the plan's done-criteria in full.
- **Committed in:** Not applicable to either task commit (verification-only, no code change).

---

**Total deviations:** 1 auto-fixed (1 blocking/verification-tooling, 0 code changes)
**Impact on plan:** No impact on the shipped code — the fix content is exactly as specified in the plan. Only the verification command differed from the plan's literal text, and the substitute produced a strictly more thorough clean-compile signal.

## Issues Encountered
- First `Edit` attempt for Task 1 failed with "String to replace not found in file" despite the `old_string` being copy-derived from a `Read` of the exact line range. Root cause: the plan's `<current_code_site_1>` context block visually renders the preceding comment lines (lines 1693-1703) at the same indentation as the code inside the `if (erosion > 0) { }` block (16 spaces), but the actual file has those comment lines at 12-space indentation (they precede, and are outside, the `if` block). Diagnosed by diffing an extracted actual-lines file against an intended-text file in the scratchpad directory; second `Edit` attempt with corrected 12-space indentation succeeded immediately. Task 2's mirrored edit did not hit this issue since it anchored on an already-verified-correct block (copied directly from a fresh `Read`, not from the plan's visual context block).

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness

**Pending human UAT (not part of this quick task's automated scope):** Real-world validation that the burr actually gets suppressed on `Side_Datum_3`'s Vertical RtoL ROI requires a human running the app against real imagery with `Vertical_Erosion > 0` and `polarity="negative"`, comparing edge detection results before/after this fix. The automated verification performed here (clean Debug/x64 build, 7/7 structural grep checks, symmetry between both call sites) proves correctness of the operator-selection logic and compile integrity, not the vision outcome. Flagging for the user to validate against live camera imagery when convenient.

No blockers for other in-flight work. This is a self-contained, single-file fix with no API surface change.

---
*Phase: 260727-jna*
*Completed: 2026-07-27*

## Self-Check: PASSED

- FOUND: `WPF_Example/Halcon/Algorithms/DatumFindingService.cs`
- FOUND: commit `977e182`
- FOUND: commit `06ebe92`
