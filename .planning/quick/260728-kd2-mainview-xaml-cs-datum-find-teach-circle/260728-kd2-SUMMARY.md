---
phase: 260728-kd2
plan: 01
subsystem: ui

tags: [halcon, datum, wpf, error-handling, mainview, edge-measurement]

# Dependency graph
requires: []
provides:
  - "FormatFindError / FormatTeachError now preserve the raw DatumFindingService stage-prefixed
    error string (Circle: / Horizontal_A: / Horizontal_B: / Line1: / Line2: / Vertical: /
    Circle fit failed: / Horizontal line fit failed: / Vertical line fit failed:) inside the
    operator-facing modal instead of discarding it"
  - "Circle-stage failures (both Find's \"Circle: \" and Teach's \"Circle fit failed: \" producers)
    now show a RadialDirection(Inward/Outward) remediation hint instead of the incorrect
    EdgeDirection hint (Circle has no EdgeDirection field per DatumConfig.cs:383)"
  - "All other stages (Horizontal_A/B, Line1/Line2, Vertical, unprefixed) keep the pre-existing
    EdgeDirection hint with zero behavior change apart from the newly appended raw err text"
affects: [datum-teaching-ux, datum-find-ux, operator-diagnostics]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Reused existing K&R brace-less single-statement if/else alignment style (matches
      prefix-assignment pattern two lines above in the same methods)"
    - "Reused existing U+2014 em dash convention from MainView.xaml.cs:1137 for the new
      Circle-stage hint string"

key-files:
  created: []
  modified:
    - "WPF_Example/UI/ContentItem/MainView.xaml.cs - FormatFindError/FormatTeachError bodies
      (stage-prefix preservation + Circle-vs-other hint branch)"

key-decisions:
  - "Used err.StartsWith(\"Circle\", OrdinalIgnoreCase) rather than the literal \"Circle:\" the
    plan objective text mentions, because the Teach path's producer emits \"Circle fit failed: \"
    (DatumFindingService.cs:1056) which does not start with \"Circle:\" — a colon-anchored check
    would have left the exact bug being fixed (wrong EdgeDirection hint) in place for the Teach
    button. The plan's own error_string_survey explicitly calls this out as the required choice."

requirements-completed: [FIX-DatumStageError-Find, FIX-DatumStageError-Teach]

# Metrics
duration: ~6min
completed: 2026-07-28
---

# Quick Task 260728-kd2: Datum Find/Teach stage-aware error messages Summary

**FormatFindError/FormatTeachError in MainView.xaml.cs now surface the raw DatumFindingService stage-prefixed error text and pick RadialDirection vs EdgeDirection remediation hints by StartsWith("Circle") detection instead of showing one generic, sometimes-wrong message.**

## Performance

- **Duration:** ~6 min
- **Completed:** 2026-07-28T06:26:44Z
- **Tasks:** 1/1
- **Files modified:** 1

## Accomplishments
- Both methods now concatenate the original `err` string (with its stage prefix such as `Horizontal_A: ` or `Circle fit failed: `) into the modal body instead of dropping it, so the operator can see which ROI failed without checking logs.
- Circle-stage failures — from both the Find producer (`"Circle: "`, DatumFindingService.cs:275) and the Teach producer (`"Circle fit failed: "`, DatumFindingService.cs:1056) — now correctly recommend flipping `RadialDirection(Inward/Outward)`, since Circle has no `EdgeDirection` field (DatumConfig.cs:383).
- All non-Circle failures (Horizontal_A/B, Line1/Line2, Vertical, unprefixed) retain the exact prior `EdgeDirection` hint wording — zero regression for those paths.

## Task Commits

Each task was committed atomically:

1. **Task 1: FormatFindError / FormatTeachError 에 실패 단계 원문 보존 + Circle 전용 RadialDirection 힌트 분기 추가** - `55f801b` (fix)

_No plan metadata commit — orchestrator handles the docs commit in a later step per this run's constraints._

## Files Created/Modified
- `WPF_Example/UI/ContentItem/MainView.xaml.cs` - `FormatTeachError` (line ~2033) and `FormatFindError` (line ~2047): doc-comments updated, and the single generic `return` inside each method's 3-condition edge-failure `if` block replaced with a `string hint;` + brace-less `if/else` (Circle vs other) + a `return` that concatenates the raw `err` and the selected hint. Signatures, the `err == null` guard, the `[DatumName]` prefix computation, the non-edge-failure tail `return`, and all 3 call sites are byte-identical to before.

## Decisions Made
- `err.StartsWith("Circle", System.StringComparison.OrdinalIgnoreCase)` chosen over a colon-anchored `"Circle:"` check — the plan's error_string_survey proved the Teach path's `"Circle fit failed: "` producer would otherwise be missed, silently leaving the exact wrong-hint bug unfixed for the Teach button. This was specified explicitly by the plan, not a deviation.

## Deviations from Plan

None - plan executed exactly as written. All automated gate token counts matched the plan's expected values exactly on first application (`circle_branch=2`, `radial_hint=2`, `hint_local=2`, `raw_err_echoed=2`, `teach_prefix_used=1`, `old_generic_gone=0`, all "kept"/"unchanged" invariants at their pre-existing values).

## Issues Encountered

None. Build produced zero `error CS` diagnostics. The only build-log errors were `MSB3027`/`MSB3021` file-copy-lock errors caused by a running `DatumMeasurement.exe` (PID 18380) + Visual Studio holding the output binary — the plan's verification notes explicitly document this as expected/ignorable and instruct judging success solely by absence of `error CS` lines, which held. Warning count (10 `warning CS0618`) is the same pre-existing 5-warning Phase 33 migration baseline, just double-counted because the build runs a MarkupCompile temp-project pass plus the main project pass; no new warnings were introduced.

## User Setup Required

None - no external service configuration required.

## Human UAT Pending (out of automated executor scope)

The plan flags this explicitly: automated gates only prove the code compiles and contains the correct literal strings/branches — they do not prove the on-screen modal text renders as intended at runtime. Per the plan's `<human_uat_pending>` section, the operator should, after rebuilding with the app closed:
1. Pick a `CircleTwoHorizontal` datum, misconfigure the `Horizontal_A` ROI (e.g. move it to an edge-free area), press Find and Teach — expect the modal to show `Horizontal_A` and an `EdgeDirection` hint (both buttons).
2. Restore `Horizontal_A`, misconfigure the Circle ROI instead (e.g. wrong radius so no circle is found), press Find and Teach — expect the modal to show `Circle` and a `RadialDirection(Inward/Outward)` hint, with no mention of `EdgeDirection`. The Teach button is the case this fix specifically targets (`"Circle fit failed: "` differs from Find's `"Circle: "`).
3. Optionally repeat with a `VerticalTwoHorizontal` datum's Vertical ROI — expect `Vertical` + `EdgeDirection`.

This was not run in this session (requires closing the running `DatumMeasurement.exe`, rebuilding, and interacting with live camera/simulated images through the WPF UI) and remains pending for the user.

## Next Phase Readiness
- Single-file, single-task quick fix is complete and committed (`55f801b`). No follow-on work implied by this task beyond the human UAT above.
- No blockers for other in-flight work; `FormatFindError`/`FormatTeachError` signatures and all 3 call sites are unchanged, so nothing else in the codebase needs updating as a result of this change.

## Self-Check: PASSED

- FOUND: `WPF_Example/UI/ContentItem/MainView.xaml.cs`
- FOUND: commit `55f801b`
- All structural verification gate tokens matched expected `(want N)` values exactly (17/17 checks).
- `git diff --name-only` for the commit shows exactly one file: `WPF_Example/UI/ContentItem/MainView.xaml.cs`.
- Build (`MSBuild //t:Build //p:Configuration=Debug //p:Platform=x64`) produced zero `error CS` lines.

---
*Quick task: 260728-kd2*
*Completed: 2026-07-28*
