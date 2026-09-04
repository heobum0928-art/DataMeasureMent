---
phase: quick-260904-iwm
plan: 01
subsystem: inspection-sequence
tags: [halcon-vision, datum, z-index, tcp-protocol, propertygrid, ini-recipe]

# Dependency graph
requires:
  - phase: none
    provides: n/a (standalone quick task on existing Datum/InspectionSequence subsystem)
provides:
  - "Per-sequence configurable Datum(기준점) z_index via DatumConfig.DatumZIndex PropertyGrid field"
  - "InspectionSequence.GetDatumZIndex() effective-value accessor consumed by every cycle-start decision point"
  - "SystemHandler.ResolveDatumZIndex() helper used by GetPrepZIndex fallback and StartV1Scoped"
affects: [inspection-sequence, tcp-vision-server, datum-recipe-io]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Effective-value accessor pattern: user-specified override (DatumConfig.DatumZIndex) -> computed fallback (owned Shot ZIndex min) -> hard default (0), uncached, recomputed per call"
    - "Load-override ContainsKey guard for Int32 PropertyGrid fields where 0 is a valid explicit value (mirrors existing ZIndexA/ZIndexB pattern)"
    - "Suppress-warning flag renamed from single-purpose (_suppressMirrorWarning) to shared-purpose (_suppressUserEditWarning) as a second user-edit-only warning was added to the same class"

key-files:
  created: []
  modified:
    - WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs
    - WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs
    - WPF_Example/Custom/SystemHandler.cs

key-decisions:
  - "DatumZIndex lives on DatumConfig (Datum node PropertyGrid), not on the sequence node — per user's confirmed decision in the plan"
  - "ParseCurrentZIndex's 0 return (no request packet) is a different concept from the sequence's datum index and was NOT normalized to the datum value — kept as UNSET_CYCLE_Z_INDEX(0), distinguished from real protocol z via IsProtocolDrivenCycle()"
  - "IsDatumOnlyExecutionIndex was NOT given an IsProtocolDrivenCycle() guard — that function is also called before StartSubset/StartAll where RequestPacket may not yet reflect this cycle's value"
  - "m_nLastZIndex > 0 comparison left completely unchanged (value, operator, and structure) — it means 'this sequence owns zero Shots', an orthogonal concept to the datum index"

requirements-completed: [QUICK-260904-IWM]

# Metrics
duration: ~32min
completed: 2026-09-04
---

# Quick Task 260904-iwm: Per-Sequence Datum Cycle-Start Index Summary

**Made the Datum ("기준점") cycle-start z_index configurable per sequence via a new `DatumZIndex` PropertyGrid field on the Datum node, replacing a hardcoded `z==0` assumption that made Bottom (z 11~40) never start a new cycle.**

## Performance

- **Duration:** ~32 min (13:50 plan commit already existed; code execution 14:00–14:22)
- **Tasks:** 2 automated tasks completed (Task 3 is a human-verify checkpoint, documented below, not blocking completion)
- **Files modified:** 3 (`DatumConfig.cs`, `InspectionSequence.cs`, `Custom/SystemHandler.cs`)

## Accomplishments

- `DatumConfig.DatumZIndex` property (default `-1` = auto) added, visible in the Datum PropertyGrid under a new `Datum|Cycle` category, exposed identically across all 4 Datum algorithms (not added to `IsHiddenForAlgorithm`).
- `DatumConfig.Load` override gains a `ContainsKey("DatumZIndex")` guard so recipes without the key load `-1` (auto), not `0` — required because `0` is itself a valid explicit "z=0 is the datum" setting, and reflection-based `ParamBase.Load` otherwise defaults missing `Int32` keys to `0`.
- User edits that differ from the sequence's owned-Shot minimum trigger a non-blocking warning (`WarnDatumZIndexChanged`), following the existing `_suppressUserEditWarning` (renamed from `_suppressMirrorWarning`) suppress-during-load/paste convention.
- `InspectionSequence.TryGetOwnedShotZIndexRange()` and `InspectionSequence.GetDatumZIndex()` added: effective datum index = min of user-specified `DatumZIndex` values across owned `DatumConfigs` (if any `>= 0`) → else min of owned Shot `ZIndex` → else `0`. Uncached by design (recomputed each call, same cost profile as the existing `ComputeLastZIndex`).
- All hardcoded `z==0` comparison points across `InspectionSequence.cs` and `Custom/SystemHandler.cs` now compare against `GetDatumZIndex()` (or, in `SystemHandler`, the new `ResolveDatumZIndex()` helper): `OnStart` cache-clear branch, `IsDatumOnlyExecutionIndex`, `AddResponseV1Cycle`, `HandleDatumIndexResponse`, `TryTurnOffLightsOnCycleEnd` tag/value, `GetPrepZIndex` fallback, `StartV1Scoped` datum branch.
- `FindZeroIndexDatumTriggerActionIndices` renamed to `FindDatumIndexTriggerActionIndices` (2 call sites + declaration) — old name implied z=0-only, which is no longer accurate.
- `DATUM_Z_INDEX` (InspectionSequence) and `DATUM_TEST_Z_INDEX` (SystemHandler) constants removed entirely; `SystemHandler` gained `NO_SEQUENCE_DATUM_Z_INDEX` (cast-failure-only fallback) and `ResolveDatumZIndex(szSeqName)` helper.
- `StartV1Scoped`'s `InspectionSequence` cast was hoisted to the top of the function so both the datum-index comparison and the general execution-scope logic share one cast — `BeginCrossZImageCycle()` call position, `StartSubset`/`StartAll` ordering, and the two defensive `!bIsInspectionSeq` fallback returns were all preserved unchanged (the TOCTOU-sensitive structure called out in the plan was not touched).
- `ParseCurrentZIndex`'s three `0` returns and `ResetCycleState`'s two `0` assignments were promoted to the named constant `UNSET_CYCLE_Z_INDEX` with no behavior change (value stays `0`; this is the "no request packet" sentinel, a different concept from the datum index — per plan analysis point (1)).
- `m_nLastZIndex > 0` (analysis point (3)) and `IsDatumOnlyExecutionIndex`'s top-level `nZIndex == GetDatumZIndex()` guard (analysis point (2)) were left structurally and semantically intact, with clarifying comments added.

## Task Commits

1. **Task 1: Datum PropertyGrid field + sequence effective-value accessors** - `f4c37444` (feat)
2. **Task 2: Replace all hardcoded z==0 comparison points with GetDatumZIndex()** - `92f854ad` (feat)

**Plan metadata commit:** pending (orchestrator handles STATE.md/ROADMAP.md/PLAN.md commit separately per constraints)

_Task 3 (human-verify checkpoint) requires live hardware/UI — see "Checkpoint Task 3" section below._

## Files Created/Modified

- `WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs` - New `DatumZIndex` property (`AUTO_DATUM_Z_INDEX = -1` sentinel), `WarnDatumZIndexChanged()`, Load-override `ContainsKey` guard, `_suppressMirrorWarning` → `_suppressUserEditWarning` rename (7+ sites)
- `WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs` - `TryGetOwnedShotZIndexRange()`, `GetDatumZIndex()`, `MIN_VALID_Z_INDEX`/`UNSET_CYCLE_Z_INDEX` constants, `DATUM_Z_INDEX` constant removed, all comparison points repointed, `FindDatumIndexTriggerActionIndices` rename
- `WPF_Example/Custom/SystemHandler.cs` - `NO_SEQUENCE_DATUM_Z_INDEX` constant (renamed from `DATUM_TEST_Z_INDEX`), `ResolveDatumZIndex()` helper, `GetPrepZIndex`/`StartV1Scoped` repointed, `InspectionSequence` cast hoisted in `StartV1Scoped`

## Decisions Made

- Kept `DatumZIndex` on the Datum node (not the sequence/InspectionMasterParam node) per the plan's binding user decision — required zero UI file changes since `DatumConfig`'s `ICustomTypeDescriptor`/`BuildFilteredProperties` path auto-exposes any new `[Category]`-tagged public property.
- Did not touch `InspectionRecipeManager.cs` — save/load/preserve-inactive-sequence paths are all reflection/section-copy based and needed no changes for the new `DatumZIndex` key.
- Where a required wording fix (e.g. "z=0" → "기준점 index") landed on a pre-existing line still carrying a legacy `//YYMMDD hbk` date-comment tag, stripped only the `hbk` token from that specific line (content and date preserved) rather than skip the fix — satisfies both the CLAUDE.md "no new hbk-style comments" rule and the plan's "diff-added-lines" hard-rule grep gates, which check every added line regardless of the comment's origin.
- Two originally brace-less `if (...) return;` one-liners that had to be touched anyway for the `_suppressMirrorWarning` → `_suppressUserEditWarning` rename were reformatted with braces (the surrounding legacy style is brace-less for these particular one-liners, but the orchestrator's hard style gate applies to all added diff lines with no legacy exception).

## Deviations from Plan

None functionally — the plan's analysis sections (1)-(5) were followed exactly as binding: `ParseCurrentZIndex` stays 0-normalized (semantics unchanged), no `IsProtocolDrivenCycle()` guard was added to `IsDatumOnlyExecutionIndex`, and `m_nLastZIndex > 0` sentinel comparison is byte-for-byte unchanged except for an added clarifying comment.

### Auto-fixed Issues (style-gate compliance, not functional)

**1. [Rule 2-adjacent — CLAUDE.md hard style gate] Stripped `hbk` token from lines that required substantive wording edits**
- **Found during:** Task 2 (comment wording corrections mandated by the plan for "z=0" → "기준점 index" phrasing landed on lines carrying legacy `//YYMMDD hbk` tags)
- **Issue:** The orchestrator's explicit constraint requires `grep -cF 'hbk'` on all `+` diff lines to be 0; several plan-mandated comment corrections sat on pre-existing `hbk`-tagged lines, which would fail that gate the moment the line's text changed
- **Fix:** Removed only the `hbk` substring from the affected tag lines (kept date + task-id + full explanatory body); left unrelated `hbk`-tagged lines that did not need substantive edits completely untouched so they never entered the diff
- **Files modified:** `InspectionSequence.cs`, `Custom/SystemHandler.cs`
- **Verification:** `git diff -U0 <base>..HEAD -- <3 files> | grep '^+' | grep -cF 'hbk'` = 0 (checked after each task and cumulatively)
- **Committed in:** `f4c37444`, `92f854ad`

**2. [Rule 2-adjacent — CLAUDE.md hard style gate] Braced two renamed one-liner ifs**
- **Found during:** Task 1 (`_suppressMirrorWarning` → `_suppressUserEditWarning` rename touched `WarnMirrorChanged`'s brace-less `if (_suppressMirrorWarning) return;` guard, forcing it into the diff)
- **Issue:** Renaming the identifier necessarily re-emits the line in the diff; the orchestrator's brace-less-if gate (`^\+\s*if\s*\(.*\)\s*[^{]+;\s*$` must be 0) makes no exception for lines only touched due to a rename
- **Fix:** Added braces to the two affected `if` statements (in `WarnMirrorChanged` and `DatumConfig.CopyTo`'s null-check was left alone as it was not touched by the rename)
- **Files modified:** `DatumConfig.cs`
- **Verification:** `grep -cE '^\+\s*if\s*\(.*\)\s*[^{]+;\s*$'` on added lines = 0
- **Committed in:** `f4c37444`

---

**Total deviations:** 2 auto-fixed, both mechanical style-gate compliance fixes with zero functional/behavioral impact.
**Impact on plan:** None on functionality. No scope creep — both fixes were confined to lines the plan already required touching.

## Issues Encountered

None blocking. The main friction was reconciling the plan's explicit "update these z=0 comments" instructions with the orchestrator's zero-tolerance `hbk` grep gate on added diff lines when both landed on the same pre-existing line — resolved per Deviation #1 above.

## Checkpoint Task 3 (not executed — requires manual verification by user)

Task 3 in the plan is `type="checkpoint:human-verify" gate="blocking"` and requires live hardware/UI observation (`bin/x64/Debug/DatumMeasurement.exe`, physical Bottom recipe with Shots 11~40, PLC or manual jig RUN). This was **not** executed as part of this automated run and does **not** block plan completion per the orchestrator's instructions. The checklist the user (or a follow-up session) must perform:

1. **PropertyGrid field visibility** — Click a Datum node (e.g. `Datum_1`); confirm the `Datum|Cycle` group and its Z-index field appear, and that the field is visible across all 4 Datum algorithms (TwoLineIntersect / CircleTwoHorizontal / VerticalTwoHorizontal / VerticalTwoHorizontalDualImage). Confirm initial value is `-1`.
2. **Existing-recipe regression (most important)** — Open the current production recipe, run an inspection cycle as usual (PLC or manual jig RUN button). Confirm results/responses are identical to before, and no warning dialog appears while loading the recipe.
3. **Save/reload** — Set a Datum's cycle-start field (e.g. `11` for Bottom), save the recipe, restart the app, reopen the recipe — confirm the value persists. Check `main.ini`'s `[FIXTURE_BOTTOM_DATUM_0]` (or equivalent) section for a `DatumZIndex=11` line. Confirm an inactive sequence's value (e.g. Bottom while in Side mode) also persists after save.
4. **Bottom z=11 live cycle-start** — With a Bottom recipe whose Shots span 11~40, send `$PREP:1,1,11@` then `$TEST`; confirm a new cycle starts (datum response `B`, prior part's results reset), then send 12, 13, ... through 40 and confirm a final P/F verdict. Repeat with the field left at `-1` (auto) and confirm identical behavior.
5. **Warning behavior** — Set the field to a value that does not match the Shot range minimum (e.g. `40` when Shots are 11~40); confirm a warning dialog appears referencing "시작 번호(11)가 아니다", that closing it does not block saving, and that re-entering the same value does not re-trigger the warning. Return the field to `-1` afterward.

## User Setup Required

None - no external service configuration required. No new NuGet packages installed (Rule 3 package-install exclusion did not trigger).

## Next Phase Readiness

- Code changes are complete, committed, and build clean (`error CS 0`, baseline warnings unchanged: `AlignShapeMatchService._matcher2` CS0169 and the pre-existing `TopSequence`/`BottomSequence`/`TopSideInspectionAction`/`BottomInspectionAction` CS0618 obsolete-API warnings).
- Blocked on Task 3 live-hardware UAT before this can be considered production-verified — flagged in STATE.md as pending human verification.
- No follow-up phases are known to depend on this quick task.

---
*Phase: quick-260904-iwm*
*Completed: 2026-09-04*

## Self-Check: PASSED

- FOUND: WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs
- FOUND: WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs
- FOUND: WPF_Example/Custom/SystemHandler.cs
- FOUND: .planning/quick/260904-iwm-datum-z-0/260904-iwm-SUMMARY.md
- FOUND commit: f4c37444
- FOUND commit: 92f854ad
