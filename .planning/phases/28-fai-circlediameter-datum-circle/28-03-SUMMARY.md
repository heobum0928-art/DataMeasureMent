---
phase: 28-fai-circlediameter-datum-circle
plan: 03
subsystem: vision-algorithms
tags: [datum, polarity-mapping-cleanup, dry-up, datumfindingservice]

# Dependency graph
requires:
  - phase: 28
    plan: 01
    provides: "EdgeOptionLists.MapRadialDirectionToHalconPolarity(string) static helper — single-source polarity mapping (byte-identical to inline form)"
  - phase: 17
    plan: 02
    provides: "Phase 17 D-02 — Circle_RadialDirection ('Inward'/'Outward') → polarity ('positive'/'negative') override pattern in DatumFindingService"
  - phase: 14
    plan: 05
    provides: "TryFindCircleByPolarSampling integration in DatumFindingService.cs:200/:730 (Phase 14-05 D-10)"
provides:
  - "DatumFindingService.TryFindCircleTwoHorizontal — polarity mapping via EdgeOptionLists helper (line 200)"
  - "DatumFindingService.TryTeachCircleTwoHorizontal — polarity mapping via EdgeOptionLists helper (line 730)"
  - "Three-way single-source polarity mapping achieved (Datum CTH find + Datum CTH teach + FAI CircleDiameter polar — all call EdgeOptionLists.MapRadialDirectionToHalconPolarity)"
affects: [28-04 (UAT — Datum CTH regression-0 by mathematical equivalence)]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Inline ternary → helper call cleanup (DRY): polarity mapping rule lives in exactly ONE location (EdgeOptionLists.MapRadialDirectionToHalconPolarity)"
    - "Service-layer cleanup (D-03 carved scope): modifies service class — does NOT violate SPEC §Out-of-scope '다른 Measurement 클래스 변경 금지'"

key-files:
  created: []
  modified:
    - "WPF_Example/Halcon/Algorithms/DatumFindingService.cs (2 lines replaced — line 200 + line 730; net 0 line-count change)"

key-decisions:
  - "Both lines replaced as exact line-for-line substitutions (1 insertion + 1 deletion per task) — diff minimality preserved per CONTEXT.md D-03"
  - "Phase 17 D-02 comment on line 729 preserved verbatim — historical decision trail kept intact even though now slightly redundant with the helper (avoids unrelated diff churn)"
  - "Used unqualified `EdgeOptionLists.MapRadialDirectionToHalconPolarity` (not fully-qualified) — `using ReringProject.Sequence;` already on line 4, matches file's existing convention for already-imported types"
  - "Two atomic commits despite single file — Task 1 (line 200, find path) and Task 2 (line 730, teach path) committed separately for reviewer locality and to match the 2-task plan structure"

patterns-established:
  - "Line-for-line helper-call replacement: when removing duplicated inline expressions in favor of a tested helper, prefer atomic per-site commits with `git diff --numstat` showing 1+/1- per task to keep reviewer cognitive load minimal"

requirements-completed: [REQ-28-02, REQ-28-06]

# Metrics
duration: 2min
completed: 2026-05-08
---

# Phase 28 Plan 03: DatumFindingService Polarity Inline Cleanup Summary

**Replaced 2 duplicated inline polarity-mapping ternaries in `DatumFindingService.cs` (line 200 in `TryFindCircleTwoHorizontal` + line 730 in `TryTeachCircleTwoHorizontal`) with calls to the Plan 01 `EdgeOptionLists.MapRadialDirectionToHalconPolarity` helper. Polarity mapping rule now lives in exactly ONE location — three-way single source achieved across Datum CTH find, Datum CTH teach, and FAI CircleDiameter polar. Datum CTH runtime/teaching regression = 0 by mathematical equivalence (Plan 01 acceptance proves byte-identical mapping).**

## Performance

- **Duration:** ~2 min
- **Started:** 2026-05-08T19:34:38Z
- **Completed:** 2026-05-08T19:36:27Z
- **Tasks:** 2
- **Files modified:** 1
- **Commits:** 2 (per-task atomic) + this docs commit

## Accomplishments

### Task 1 — Line 200 helper call (commit `84affbb`)

- `DatumFindingService.cs:200` (inside `TryFindCircleTwoHorizontal` runtime path): inline ternary `string.Equals(config.Circle_RadialDirection, "Outward", System.StringComparison.OrdinalIgnoreCase) ? "negative" : "positive"` → `EdgeOptionLists.MapRadialDirectionToHalconPolarity(config.Circle_RadialDirection)`.
- Variable name `circlePolarity` and type `string` preserved — immediately-following `TryFindCircleByPolarSampling` call (line 202) consumes it unchanged at argument position 9 (`polarity`).
- `bool[] unusedStrips;` declaration on line 201 (Phase 18 CO-05) untouched.
- 1 insertion + 1 deletion (line-for-line replacement). `//260508 hbk Phase 28 D-03 — inline ternary → helper 호출 (DRY)` marker added.
- msbuild Debug/x64 PASS, 0 new errors/warnings.

### Task 2 — Line 730 helper call (commit `a894c36`)

- `DatumFindingService.cs:730` (inside `TryTeachCircleTwoHorizontal` teaching path): identical inline ternary → identical helper call.
- Phase 17 D-02 historical comment on line 729 (`//260503 hbk Phase 17 D-02 — Circle_RadialDirection ("Inward"/"Outward") → polarity ("positive"/"negative") override (EdgePolarity 무시)`) preserved verbatim — decision trail kept intact even though now slightly redundant with the helper, avoiding unrelated diff churn.
- Phase 14-05 polar-call site (lines 731-748) and Phase 18 CO-05 `circleStrips` (line 732) untouched.
- 1 insertion + 1 deletion (line-for-line replacement). `//260508 hbk Phase 28 D-03 — inline ternary → helper 호출 (DRY)` marker added.
- msbuild Debug/x64 PASS, 0 new errors/warnings.

### DRY Single-Source Achieved

After both tasks, the polarity mapping rule (`"Outward" (case-insensitive) → "negative"`, else → `"positive"`) lives in exactly ONE physical location:

| Caller | File:Line | Form |
|--------|-----------|------|
| Datum CTH find | DatumFindingService.cs:200 | `EdgeOptionLists.MapRadialDirectionToHalconPolarity(config.Circle_RadialDirection)` |
| Datum CTH teach | DatumFindingService.cs:730 | `EdgeOptionLists.MapRadialDirectionToHalconPolarity(config.Circle_RadialDirection)` |
| FAI CircleDiameter polar | CircleDiameterMeasurement.cs (Plan 02) | `EdgeOptionLists.MapRadialDirectionToHalconPolarity(Circle_RadialDirection)` |
| Helper definition | EdgeOptionLists.cs (Plan 01) | `string.Equals(radial, "Outward", System.StringComparison.OrdinalIgnoreCase) ? "negative" : "positive"` |

Three call sites, one definition. D-03 service-layer cleanup complete.

## Task Commits

1. **Task 1: Replace inline polarity ternary at DatumFindingService.cs:200** — `84affbb` (refactor)
2. **Task 2: Replace inline polarity ternary at DatumFindingService.cs:730** — `a894c36` (refactor)

**Plan metadata commit:** pending (final docs commit at end of plan).

## Files Created/Modified

- `WPF_Example/Halcon/Algorithms/DatumFindingService.cs` — 2 lines replaced (line 200 + line 730), net 0 line-count change. Lines 1-7 (using directives), all of `TryFindLine`, `TryExtractEdgePoints`, `TryFindCircleTwoHorizontal` Step 2+3 (lines 218-end), and the entire teaching dispatch (lines ~600-715) untouched. The `circlePolarity` variable, its consumer (`TryFindCircleByPolarSampling` argument 9), and all surrounding declarations (`unusedStrips`, `circleStrips`, edge tuples) preserved.

## Decisions Made

- **Line-for-line replacement** (1 insertion + 1 deletion per task, verified via `git diff --numstat` after each commit). Keeps the diff minimal and unambiguous — reviewer sees exactly the substitution intended, nothing else.
- **Phase 17 D-02 comment preserved** on line 729 even though the helper now subsumes the override semantics. Removing the comment would obscure the historical decision (Phase 17: Circle_RadialDirection takes precedence over EdgePolarity for Datum CTH). Keeping it documents *why* the polarity is computed from RadialDirection rather than read directly from EdgePolarity, which is information the helper signature alone does not convey.
- **Unqualified helper reference** (`EdgeOptionLists.MapRadialDirectionToHalconPolarity` rather than `ReringProject.Sequence.EdgeOptionLists.MapRadialDirectionToHalconPolarity`) — `using ReringProject.Sequence;` is already on line 4 of `DatumFindingService.cs`. The file's existing convention for already-imported types is unqualified usage; matching that convention.
- **Two atomic commits** despite same file. Task 1 (find path) and Task 2 (teach path) commit separately so each commit's diff is exactly 1+/1−, and reviewers can see line 200 and line 730 changes in context-isolated form. Matches the 2-task plan structure 1:1.
- **Used `Edit` tool with surrounding-line uniqueness anchor** — Task 2 anchored on the Phase 17 D-02 comment (line 729) + the inline ternary (line 730) to disambiguate from Task 1's site (line 200), since both lines had identical inline expressions.

## Deviations from Plan

None — plan executed exactly as written. Both tasks single-line replacements, both verified via grep counts and msbuild. No Rule 1/2/3 fixes triggered.

## Issues Encountered

None. The PreToolUse hook fired a "READ-BEFORE-EDIT" reminder twice (once per task) despite the file having been read earlier in the session at the relevant line ranges (1-10, 185-219, 715-749). Re-reading the surrounding lines (15-line window) after each edit served double duty as both compliance and post-edit verification. No data loss, no behavioural impact.

## Build Verification

| Check | Result |
|-------|--------|
| `msbuild WPF_Example/DatumMeasurement.csproj /p:Configuration=Debug /p:Platform=x64` after Task 1 | PASS (DatumMeasurement.exe produced) |
| New CS errors after Task 1 | 0 |
| New CS warnings after Task 1 | 0 |
| `msbuild WPF_Example/DatumMeasurement.csproj /p:Configuration=Debug /p:Platform=x64` after Task 2 | PASS (DatumMeasurement.exe produced) |
| New CS errors after Task 2 | 0 |
| New CS warnings after Task 2 | 0 |
| Pre-existing CS warnings (unchanged) | 2 distinct: `VirtualCamera.cs:266 CS0162` unreachable code, `VisionAlgorithmService.cs:64 CS0219` unused `scanHorizontal` local — each appears twice in build log (WPF temp + main project compile). SPEC REQ-28-06 budget = 5 pre-existing; actual = 2 distinct ✓ within budget. |
| Pre-existing MSBuild warnings | `MSB3884 MinimumRecommendedRules.ruleset not found` (×2) — unrelated to this phase. |

## Acceptance Criteria Verification

### Task 1

| Criterion | Expected | Actual | Status |
|-----------|----------|--------|--------|
| `EdgeOptionLists.MapRadialDirectionToHalconPolarity(config.Circle_RadialDirection)` count after Task 1 | ≥ 1 | 1 | PASS |
| `circlePolarity` variable still in `TryFindCircleTwoHorizontal` body | yes | yes (line 200 + consumer line 206) | PASS |
| Inline ternary on line 200 removed | yes | yes (verified via grep) | PASS |
| msbuild exit code | 0 | 0 | PASS |

### Task 2

| Criterion | Expected | Actual | Status |
|-----------|----------|--------|--------|
| `string.Equals\(config\.Circle_RadialDirection.*"Outward".*"negative"` count | 0 | 0 | PASS |
| `EdgeOptionLists\.MapRadialDirectionToHalconPolarity\(config\.Circle_RadialDirection\)` count | 2 | 2 | PASS |
| `circlePolarity` reference count (file-wide) | ≥ 4 (2 declarations + 2 consumers) | 4 | PASS |
| `if (!visionSvc.TryFindCircleByPolarSampling(` near line 733 unchanged | yes | yes (verified via re-read of lines 733-743) | PASS |
| msbuild exit code | 0 | 0 | PASS |
| `git diff --numstat` Task 2 commit | 1+/1- | 1+/1- | PASS |
| All new lines tagged `//260508 hbk Phase 28 D-03` | yes | yes (2 markers added — 1 per task) | PASS |

### Plan-level Acceptance Criteria

| Criterion | Status |
|-----------|--------|
| 2 inline ternaries → 2 helper calls (line 200 + line 730) | PASS |
| DRY single source: polarity mapping rule lives in exactly 1 place (EdgeOptionLists.MapRadialDirectionToHalconPolarity) | PASS |
| msbuild Debug/x64 PASS, 0 new CS errors/warnings | PASS |
| All new lines tagged `//260508 hbk Phase 28 D-03` | PASS |
| Datum CTH UAT not required (mathematical equivalence per Plan 01) | DOCUMENTED — Plan 04 covers FAI-side UAT only |

## Datum CTH Regression Argument

**No Datum CTH UAT required.** Plan 01's acceptance proved that:

```
EdgeOptionLists.MapRadialDirectionToHalconPolarity(x)
  ≡ string.Equals(x, "Outward", System.StringComparison.OrdinalIgnoreCase) ? "negative" : "positive"
```

is byte-identical for **all** string inputs (null, empty, "Outward" any case, "Inward", anything else → "positive"). Therefore, replacing the inline expressions on line 200 and line 730 with helper calls produces the same `circlePolarity` value for the same `config.Circle_RadialDirection` input — the downstream `TryFindCircleByPolarSampling` call receives identical arguments, and the Halcon code path is identical. Datum CTH find and teach paths are regression-0 by mathematical equivalence, not by empirical UAT. Plan 04 covers FAI-side UAT (AC-4 / AC-5) only.

## Open Items / Plan 04 UAT

- **Datum CTH find regression**: NOT REQUIRED for this plan (mathematical equivalence — see above).
- **Datum CTH teach regression**: NOT REQUIRED for this plan (same argument).
- **FAI CircleDiameter equivalence (AC-4)**: deferred to Plan 04 — covers FAI ↔ Datum CTH side-by-side detection comparison in SIMUL_MODE.
- **v1.0 INI 회귀 (AC-5)**: deferred to Plan 04.

## Wave 3 Caller Wiring Status

- **Plan 04 unblocked**: FAI-side UAT (AC-4 / AC-5) can proceed without further code changes. All three callers of the polarity helper (Datum CTH find, Datum CTH teach, FAI CircleDiameter polar) are now wired and msbuild-verified. SIMUL_MODE side-by-side equivalence is ready to be empirically confirmed.

## Self-Check: PASSED

- File `WPF_Example/Halcon/Algorithms/DatumFindingService.cs`: FOUND (modified, 2 lines replaced — verified via Read of lines 195-209 and 725-744)
- Commit `84affbb`: FOUND in `git log --oneline` (Task 1)
- Commit `a894c36`: FOUND in `git log --oneline` (Task 2)
- Both Task 1 and Task 2 acceptance grep counts: PASS
- msbuild Debug/x64 after each task: PASS, 0 new errors/warnings
- No file deletions in either commit (verified via `git diff --diff-filter=D HEAD~2 HEAD` — empty)
- Helper grep count exactly 2 (verified)
- Inline ternary grep count exactly 0 (verified)

---
*Phase: 28-fai-circlediameter-datum-circle*
*Plan: 03*
*Completed: 2026-05-08*
