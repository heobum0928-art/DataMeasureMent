---
phase: 260727-mll
plan: 01
subsystem: vision-measurement
tags: [halcon, gray_erosion, gray_dilation, morphology, datum, roi-clamp, DatumFindingService]

# Dependency graph
requires:
  - phase: 260727-jna
    provides: Polarity-aware directional gray_erosion/gray_dilation operator selection (useDilation/opName) at both DatumFindingService strip-loop entry points
provides:
  - ROI-bounded directional structuring-element half-length (roiHalfExtentAlongEdge clamp) at both DatumFindingService strip-loop entry points (TryFindLine and TryExtractEdgePoints), so a large Erosion(px) can no longer bleed smoothing past a corner into an adjacent, differently-oriented edge
affects: [datum-finding, corner-adjacent-datum-rois, erosion-tact-logging]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "roiHalfExtentAlongEdge selected via explicit if (scanHorizontal) roiHalfExtentAlongEdge = <rowExtent>; else roiHalfExtentAlongEdge = <colExtent>; reusing each function's own pre-existing AABB half-extent variables (halfH/halfW in TryFindLine, halfRow/halfCol in TryExtractEdgePoints -- names differ between the two functions), then halfLen clamped with an explicit if (no ternary, no Math.Min), and pad recomputed from the clamped halfLen instead of raw erosion"

key-files:
  created: []
  modified:
    - WPF_Example/Halcon/Algorithms/DatumFindingService.cs

key-decisions:
  - "Clamp bound = the ROI's own half-extent along the edge axis, reusing values already computed earlier in each function -- no new geometry, no hardcoded limit. The ROI the operator draws is already the natural boundary of where smoothing should be allowed to reach."
  - "pad recomputed as halfLen*2.0+1.0 after the clamp (was pad=erosion, computed before halfLen existed) so ReduceDomain's domain always has full SE support for the (possibly smaller) clamped halfLen, while never shrinking below today's pad in the unclamped/well-sized-ROI case -- zero regression path."
  - "Under align (alignRot != 0) the half-extents consumed are the enlarged AABB half-extents (slightly larger than the true rotated-ROI half-extents), making the clamp marginally loose under align. Left as-is per plan -- align rotations here are ~0.1-0.2 deg, not worth tightening."

requirements-completed: [FIX-ErosionClamp-TryFindLine, FIX-ErosionClamp-TryExtractEdgePoints]

# Metrics
duration: ~12min
completed: 2026-07-27
---

# Quick Task 260727-mll: Datum Erosion ROI-Extent Clamp Summary

**Directional pre-erosion/dilation structuring-element half-length in `DatumFindingService` is now clamped to the ROI's own half-extent along the edge axis (mirrored at both `TryFindLine` and `TryExtractEdgePoints`), so a large `Erosion(px)` (e.g. 201) can no longer smear an adjacent, differently-oriented edge across a corner.**

## Performance

- **Duration:** ~12 min
- **Completed:** 2026-07-27T07:40:20Z (982f2c1)
- **Tasks:** 2/2
- **Files modified:** 1

## Accomplishments
- `TryFindLine`'s `if (erosion > 0)` block now derives `roiHalfExtentAlongEdge` via explicit `if (scanHorizontal) ... else ...` on the function's own `halfH`/`halfW`, and clamps `halfLen = erosion / 2.0` to it with an explicit `if` before it ever reaches `HOperatorSet.GenRectangle2`.
- `TryExtractEdgePoints` received the identical mirrored fix, using that function's own `halfRow`/`halfCol` names (verified distinct from `TryFindLine`'s `halfH`/`halfW` -- using the wrong pair would have been a CS0103).
- `pad` (the `ReduceDomain` padding) is now computed *after*, and *from*, the clamped `halfLen` (`halfLen * 2.0 + 1.0`) instead of from raw `erosion` -- so the reduced-domain guarantee tracks the actual (possibly shrunk) SE size.
- `erosion <= 0` remains a complete no-op at both sites (gate untouched); `const double halfWidth = 0.5;` unchanged at both sites; the 260727-jna polarity logic (`useDilation`/`opName`/`GrayDilation`/`GrayErosion`) untouched at both sites.
- For any recipe where `erosion / 2.0 <= roiHalfExtentAlongEdge` (the normal, well-sized-ROI case), `halfLen` is bit-identical to before -- zero behavior change.

## Task Commits

Each task was committed atomically:

1. **Task 1: Clamp the SE length to the ROI extent in TryFindLine** - `af27fae` (fix)
2. **Task 2: Mirror the clamp into TryExtractEdgePoints and verify both sites** - `982f2c1` (fix)

_Both commits modify only `WPF_Example/Halcon/Algorithms/DatumFindingService.cs`; the pre-existing unrelated `WPF_Example/DatumMeasurement.csproj` working-tree modification (local `SIMUL_MODE` DefineConstants toggle) was left untouched and excluded from both commits per the task constraints._

## Files Created/Modified
- `WPF_Example/Halcon/Algorithms/DatumFindingService.cs` - Both `if (erosion > 0)` directional-morphology blocks (inside `TryFindLine` and `TryExtractEdgePoints`) now compute `measurePhi`/`lineAxisPhi` first, derive `roiHalfExtentAlongEdge` from the ROI's own AABB half-extent along the edge axis, clamp `halfLen` to it, and only then compute `pad` from the clamped `halfLen` before the `GenRectangle1`/`ReduceDomain` domain-sizing calls.

## Decisions Made
See `key-decisions` in frontmatter. No architectural decisions were required -- this was a scoped, mechanical two-site symmetric fix exactly as specified in the plan (reorder + clamp + reuse of already-computed geometry, no new geometry computation).

## Build Verification

`DatumMeasurement.exe` (PID 23532) was confirmed running via `tasklist` before any edits began.

**Structural checks (Task 2's final pass, both sites):** all 13 counts matched their `(want N)` annotation --
`decl=2, assign_halfH=1, assign_halfW=1, assign_halfRow=1, assign_halfCol=1, clamp=2, old_pad_gone=0, new_pad=2, halfWidth=2, gate=2, jna_dilation=2, jna_erosion=2, no_ternary=0`.
`ORDER_OK` printed for both sites (`halfLen@1733 -> pad@1737`, `halfLen@1989 -> pad@1993`).
`git diff --name-only` after both commits shows only `WPF_Example/Halcon/Algorithms/DatumFindingService.cs`.

**`//t:Build` (Debug/x64) result:** exit code 1, but **not a compile failure** -- see Deviations below for the full explanation. `obj\x64\Debug\DatumMeasurement.exe` was freshly regenerated (timestamp matched the build run) and the build log contained zero `error CS` lines and zero unexpected `warning CS` lines; only the plan's documented pre-existing baseline (`CS0618` x5, `CS0162` x1 -- each appearing twice, once per WPF inner/outer build pass, none referencing `DatumFindingService.cs`). The only errors were `MSB3027`/`MSB3021` on the final `bin\` copy step, caused by the running `DatumMeasurement.exe` holding the output file lock.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking, environmental] `DatumMeasurement.exe` running blocked the final MSBuild output-copy step; substituted compile-success evidence + manual read-back per the plan's own pre-authorized fallback policy**
- **Found during:** Task 1 and Task 2 verification (checked via `tasklist` before starting; confirmed again when `//t:Build` was actually run after Task 2)
- **Issue:** The plan (verified_during_planning item 11) explicitly anticipated this exact scenario: `DatumMeasurement.exe` was running, so `//t:Build` completed a full compile but failed at the final `obj\...\DatumMeasurement.exe` -> `bin\...\DatumMeasurement.exe` copy with `MSB3027`/`MSB3021` (file locked by the running process), giving an overall exit code of 1 despite the C# compilation itself succeeding cleanly.
- **Fix/Resolution:** Per the plan's own stated policy for this scenario, treated it as a file-lock artifact rather than a compile failure, backed by concrete evidence rather than just asserting it: (a) `obj\x64\Debug\DatumMeasurement.exe`'s file timestamp matched the build run, proving `CoreCompile` executed and produced a fresh output assembly; (b) the build log's only `error` lines were the two copy-lock errors, zero `error CS####`; (c) all structural grep checks passed at both sites; (d) manual read-back of both edited blocks confirmed balanced braces, valid C# 7.2 in both `if`/`else` branches, and every identifier (`scanHorizontal`, `halfH`/`halfW` at site 1, `halfRow`/`halfCol` at site 2, `halfLen`, `halfWidth`, `pad`, `roiDomain`, `reducedImage`) resolving in its own function's ambient scope, with no ternary introduced anywhere.
- **Files modified:** None (verification-only; no source changes resulted from this deviation).
- **Verification:** See "Build Verification" section above.
- **Committed in:** Not applicable to either task commit (verification-only, no code change).

---

**Total deviations:** 1 auto-fixed (1 blocking/verification-tooling, 0 code changes)
**Impact on plan:** No impact on the shipped code -- the fix content is exactly as specified in the plan at both sites. Only the final build verification step hit the plan's own documented file-lock contingency; the compile-success evidence gathered is at least as strong as a clean `exit 0` would have been.

## Issues Encountered
None beyond the documented build-lock deviation above. Both `Edit` operations matched on the first attempt (Task 1's extended anchor through the unique `TryFindLine`-only comment line correctly disambiguated it from the byte-identical twin block in `TryExtractEdgePoints`; Task 2's short anchor was unique automatically once Task 1 had already changed the `TryFindLine` copy).

## User Setup Required
None - no external service configuration required.

## Threat Model Check

The plan's STRIDE register (`T-260727mll-01/02/03`, all disposition `mitigate` or `accept`) covers exactly this change and no new surface was introduced: `roiHalfExtentAlongEdge` is derived purely from ROI geometry already in scope, no new external input, no new file/network/auth surface. `T-260727mll-01` (DoS via unbounded `Erosion(px)` sizing `canvasSize`/`pad`) and `T-260727mll-02` (silent corruption of a neighbouring edge) are both directly mitigated by this implementation as designed. No Threat Flags to record.

## Next Phase Readiness

**Pending human UAT (not part of this quick task's automated scope):** re-run the confirmed failing case -- the ROI near the corner where a Vertical edge meets a Horizontal edge with `Erosion(px) = 201` -- and confirm on the same zoomed before/after view that the corner nub survives on the horizontal portion while the vertical portion is still smoothed as intended. The automated checks here prove the clamp exists, is wired to the correct half-extent at each site, compiles cleanly, and leaves the `erosion<=0` / polarity / `halfWidth` behavior untouched -- they do not prove the vision outcome.

**Behavior-change boundary for the user:** recipes where `Erosion(px) / 2 <= ROI half-extent along the edge` are completely unaffected (bit-identical `halfLen`). Recipes above that threshold now get a shorter SE than before (and a correspondingly smaller `canvasSize`, so erosion tact drops too) -- this is the intended fix, but any recipe currently relying on an over-sized `Erosion(px)` for its *intended* edge (not just tolerating the corner-bleed side effect) will see a milder smoothing effect than before on that edge.

No blockers for other in-flight work. This is a self-contained, single-file fix with no API surface change.

---
*Phase: 260727-mll*
*Completed: 2026-07-27*

## Self-Check: PASSED

- FOUND: `WPF_Example/Halcon/Algorithms/DatumFindingService.cs`
- FOUND: commit `af27fae`
- FOUND: commit `982f2c1`
