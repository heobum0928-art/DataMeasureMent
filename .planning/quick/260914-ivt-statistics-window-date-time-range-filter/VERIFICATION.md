---
task: quick-260914-ivt-statistics-window-date-time-range-filter
verified: 2026-09-14T05:54:52Z
status: passed
score: 8/8 must-haves verified
behavior_unverified: 0
overrides_applied: 0
re_verification: false
---

# Quick Task 260914-ivt: 통계 창 기간을 날짜+시:분으로 — Verification Report

**Task dir:** `.planning/quick/260914-ivt-statistics-window-date-time-range-filter/`
**Base commit:** `93c800be`
**Commits verified:** `25da3d94`, `8268db30`, `f923c7e5`, `ab240e9f`
**Verified:** 2026-09-14T05:54:52Z
**Status:** passed

Note: a previous verification attempt for this task was cut off by a network error before
producing a report. This is a fresh, full re-run (no prior VERIFICATION.md/gaps existed to
carry forward).

## Method

All checks below were re-derived independently against the live working tree — not copied
from SUMMARY.md. Where the SUMMARY reported a scratch-harness number, the harness was
recompiled from source (`SP/IvtHarness.cs`, unchanged from the executor's version) against a
freshly rebuilt `Debug|x64` binary and re-run in this session; the numbers below are from that
independent run.

## Goal Achievement

### 1. Default = unchanged (whole-day regression)

| Check | Result |
|---|---|
| Build (`MSBuild Debug\|x64`), independently re-run | `error CS/MC` count = 0, exit 0 |
| `wholeday` harness vs independent awk baseline, re-run against fresh binary, all 12 CSVs (incl. old 14-column files 0723–0811, 20260724 DATUM_FAIL/NO_IMAGE 11-line file, 20260914 828-line file) | `diff` empty — byte-for-byte identical |
| Multi-day full-day range (20260723→20260914, `FromDates`) total vs sum of the 12 per-day wholeday totals | `20598` == `20598` |
| Genuine day-boundary partial range (20260723 17:00 → 20260724 10:00, real data on both sides of midnight) vs awk-computed boundary counts | loader `TOTAL=216`, independent awk `40 (0723 ≥17:00) + 176 (0724 <10:01:00) = 216` — identical |
| Every deleted/modified pre-existing line in the 5 changed files read against base commit diff | All are semantics-preserving: `Query(DateTime,DateTime,string)` / `QueryCycles(DateTime,DateTime,string)` reduced to one-line delegations to `StatisticsTimeRange.FromDates(...)` (= old 00:00–23:59 meaning, byte-identical result per the whole-day check above); `LoadFile`/`ProcessRow`/`LoadCyclesFromFile`/`ProcessCycleRow` signatures changed to take a `state` bag but internal logic order preserved; old `ParseInspectionTime` deleted with zero remaining references (grep confirmed) |
| Callers of `Query`/`QueryCycles`/`BuildPlan`/`TryStartRerun`/`GetCyclesForExport` (repo-wide grep) | Every call site (`StatisticsWindow.xaml.cs:785,946,1086,1143` and `RepeatRunService.cs` `BuildPlan`) uses the new range-based signature consistently; no stray old-signature callers found anywhere in `WPF_Example` |
| Scratch harness re-run against current Debug build; touches only `SystemHandler`-free code | `IvtHarness.cs` reviewed line-by-line — only calls `MeasurementHistoryCsvLoader.QueryDirectory`, `QueryCyclesDirectory`, `CycleResultSerializer.Load`, `StatRowPresenter.*` (all path-injected/pure, no `SystemHandler.Handle`); output written only under the scratchpad directory; no write to `D:\Data` |

**Truth: VERIFIED.**

### 2. Time filter correctness

| Sub-check | Evidence |
|---|---|
| Inclusive To minute (through :59.999) | `contains` mode re-run: `C\|13:29:59\|True`, `C\|13:29:59.999\|True`, `C\|13:30:00\|False` |
| Multi-day ranges include middle days fully | See "Multi-day full-day range" and "day-boundary partial range" rows above — independently confirmed |
| Invariant-culture parsing of `yyyy-MM-dd HH:mm:ss` | `TryParseInspectionTime` uses `DateTime.TryParseExact(sz, INSPECTION_TIME_FORMAT, CultureInfo.InvariantCulture, DateTimeStyles.None, out dt)` (loader line 147); writer emits the same custom format string (`MeasurementHistoryCsvWriter.cs:99`), which is culture-invariant by construction (no culture-dependent tokens in the format) |
| Corrupt/unparseable rows skipped | Built an isolated fake CSV (`scratchpad/ivt/fakecsv/20260101.csv`, 1 good + 1 corrupt-timestamp + 1 good row) and queried it directly via `QueryDirectory` (no production data touched). Result: `TotalRowCount=2`, `StatsCount=2` — the corrupt row produced neither a stat entry nor a count, and no exception was thrown |
| From > To handled with visible "기간 오류" + rerun refused | r5 (13:30→13:20) re-run: `EMPTY\|True`, `TOTAL\|0`, `SUMMARY\|전체 0항목 · 불량 0 · 결과 없음 0 · 주의 0 · 정상 0`; code review confirms `StatisticsPeriodViewModel.BuildSummaryText` returns `INVALID_RANGE_TEXT` when `range.IsEmpty`, and `StatisticsRerunViewModel.TryStartRerun` returns `false` with the same `INVALID_RANGE_TEXT` message, wired to `CustomMessageBox.Show` in `Btn_Rerun_Click` |

**Truth: VERIFIED.**

### 3. Rerun/export use the same [From, To]

| Check | Evidence |
|---|---|
| All three query paths share one range | `range_calls` grep: `DoQuery`, `Btn_Rerun_Click`, `Btn_CpkExport_Click` all call `m_periodVm.BuildRange()` (3 call sites confirmed) |
| cycle.json `InspectionTime` (rerun) vs CSV `검사일시` (stats/export) boundary consistency | `TICKS` (direct cycle.json count via `IsProtocolDriven && range.Contains(InspectionTime)`) matches `CYCLES` (`QueryCyclesDirectory` count) exactly for every range re-run: r1 92/92, r2 40/40, r3 40/40, r4 0/0, r5 0/0 |
| Export filename unchanged for full days, includes HHmm otherwise | `stamps` mode re-run: `S1\|20260914_20260914` (whole day, byte-identical to old format), `S2\|20260914_0950_20260914_0959`, `S3\|20260913_20260914` (whole-day multi-day), `S4\|20260914_0000_20260914_1200` — all match plan spec exactly |
| Rerun result export keeps the *started* period even if the screen period changes afterward | Code review: `_rerunRange` field is saved inside `TryStartRerun` before the async plan build starts, and `GetExportRange` prefers `_rerunRange` when `IsShowingRerun` is true |

**Truth: VERIFIED.**

### 4. "결과 없음" honesty

| Check | Evidence |
|---|---|
| Zero-result items stay visible, distinguishable | r2 (09:50–09:59) re-run: all 9 items `N=0`, `LEVEL=NoResult`, `CPK=-`; r4 (10:00–12:59, zero records) re-run: all 9 items `N=0`, `REC=0`, `LEVEL=NoResult` |
| Sort order 불량→결과없음→주의→정상 | `EStatLevel` reordered `Bad=0, NoResult=1, Warning=2, Normal=3`; `CompareWorstFirst` compares `(int)StatusLevel` first — verified no other code casts the old enum ordinals (repo-wide grep: only `StatisticsWindow.xaml.cs` references `EStatLevel`) |
| "문제 항목만 보기" includes NoResult | `IsProblemRow` = `StatusLevel != Normal` (unchanged predicate, now covers `NoResult` automatically since `Bad`/`NoResult`/`Warning` all `!= Normal`) |
| Cpk shows "-" for N=0, no NaN/divide-by-zero | `BuildCpkText`: `if (s.N == 0) return NO_VALUE_TEXT;` before ever touching `s.Cpk`; `RepeatMeasurementStats.ComputeAll` only computes Cpk when `n>0`, else leaves `cpk=0` (never reached by the UI since N=0 short-circuits to `"-"`) |
| Histogram/trend unaffected | `Series` population in `ProcessRow` happens only after the same `bInRange` gate already exercised by the range tests above; chart lookup (`m_lastResult.Series.TryGetValue(row.Key, ...)`) is unmodified code |
| "기록(틱)" labeled, not mistaken for expected N | XAML header tooltip: "기간 안 이 항목이 기록된 자동 검사 틱 수. … N 보다 큰 것이 정상 — 누락 수가 아님"; column placed directly before N as specified |
| No part-count column added | Repo-wide check: no "부품" column added to `StatisticsWindow.xaml`; documented as an explicit deferred decision with rationale in SUMMARY |

**Truth: VERIFIED.**

### 5. UI wiring

| Check | Evidence |
|---|---|
| Hour/minute ComboBoxes default 00:00/23:59 before first auto query | `StatisticsPeriodViewModel` field initializers: `_nFromHourIndex=0`, `_nFromMinuteIndex=0`, `_nToHourIndex=StatisticsTimeRange.LAST_HOUR(23)`, `_nToMinuteIndex=StatisticsTimeRange.LAST_MINUTE(59)`; constructor sets `pnl_Period.DataContext = m_periodVm;` synchronously before `DoQuery("")` is called, so the first query already uses these defaults |
| Layout fits window | Window `Width=1280`; visible DataGrid columns sum ≈1190px (computed from XAML widths: 70+70+110+75+140+60+45+75+110+75+70+65+45+45+65+70); filter bar `pnl_Period` fixed-width controls sum ≈648px, well under 1280 with room for text/buttons |

**Truth: VERIFIED.**

### 6. Hard rules (CLAUDE.md 가독성 규칙)

Re-ran the grep checks independently (not copied from SUMMARY) against every added line (`git diff -U0 93c800be -- <file> | grep '^+' | grep -v '^+++'`) in all 4 changed `.cs` files plus the `.xaml`:

| File | tern(`?:`) | coal(`??`) | nullc(`?.`) | switch-expr | hbk | unbraced-branch |
|---|---|---|---|---|---|---|
| MeasurementHistoryCsvLoader.cs | 0 | 0 | 0 | 0 | 0 | 0 |
| RepeatMeasurementStats.cs | 0 | 0 | 0 | 0 | 0 | 0 |
| RepeatRunService.cs | 0 | 0 | 0 | 0 | 0 | 0 |
| StatisticsWindow.xaml.cs | 0 | 0 | 0 | 0 | 0 | 0 |
| StatisticsWindow.xaml (hbk only) | — | — | — | — | 0 | — |

Note: `RepeatMeasurementStats.cs` line 109 (`(shot.ShotName ?? "") + ...`) does contain `??`, but that line is unmodified from base commit (confirmed via `git diff` — only 4 additive hunks touch this file, none at line 109) — a pre-existing pattern outside the scope of this task's added-line grep, correctly excluded by the plan's own verification methodology.

TBD/FIXME/XXX and TODO/HACK/PLACEHOLDER markers: none found in any of the 5 changed files.

MVVM: new period/rerun logic lives in `StatisticsPeriodViewModel`/`StatisticsRerunViewModel`; `StatisticsWindow` code-behind changes are one-line wiring calls (`m_periodVm.BuildRange()`, `.BuildSummaryText(...)`, `.GetExportRange(...)`) — consistent with H10.

`ReviewerWindow.*`, Phase 76 files (`Action_FAIMeasurement.cs`, `DatumConfig.cs`, etc.), `MeasurementHistoryCsvWriter.cs`, and `WPF_Example/DatumMeasurement.csproj` — confirmed untouched via `git diff --name-only 93c800be -- WPF_Example` (exactly the 5 planned files).

**Truth: VERIFIED.**

### 7. SUMMARY honesty

- SUMMARY.md records real, verifiable numbers (wholeday_identical, r1–r5 totals, CYCLES/TICKS/STAMP, contains/stamps outputs) — every number independently reproduced in this verification and matched exactly.
- Deviations section documents 2 items, both scratch-harness/comment issues (CRLF-vs-LF diff false positive; stray `hbk` date-comment removal), explicitly labeled as not affecting app logic — confirmed true by diff inspection (the app `.cs`/`.xaml` diffs contain no harness code).
- MEASURE_FAIL-as-NO_RESULT follow-up is documented under "후속 제안" with a clear explanation of why it's out of scope (would require touching `MeasurementHistoryCsvWriter.cs`, forbidden by H11).
- The 9-item UAT checklist is present and entirely unchecked (`- [ ]` for all 9 items) — correctly left for real-hardware verification, not falsely marked complete.

**Truth: VERIFIED.**

### Required Artifacts

| Artifact | Expected | Status | Details |
|---|---|---|---|
| `MeasurementHistoryCsvLoader.cs` | `StatisticsTimeRange`, period overloads, `QueryDirectory`/`QueryCyclesDirectory`, row-level time filter, out-of-range preservation | ✓ VERIFIED | All present, wired, behaviorally confirmed |
| `RepeatMeasurementStats.cs` | `MeasurementStat.RecordCount` | ✓ VERIFIED | Additive-only diff, 4 hunks, no logic changes |
| `RepeatRunService.cs` | `SavedCycleRerunPlanner.BuildPlan(StatisticsTimeRange, ...)` | ✓ VERIFIED | Signature changed, single caller updated, `CollectAutoTicks` filters by `range.Contains` |
| `StatisticsWindow.xaml.cs` | `StatisticsPeriodViewModel`, `EStatLevel.NoResult`, `StatRow.RecordCount`, summary/judge extensions, rerun/export period wiring | ✓ VERIFIED | All present, wired, behaviorally confirmed |
| `StatisticsWindow.xaml` | Hour/minute ComboBoxes, NoResult purple trigger, RecordCount column, header tooltips, legend/notice relayout | ✓ VERIFIED | All present |

### Key Link Verification

| From | To | Via | Status |
|---|---|---|---|
| `pnl_Period` (DatePicker/ComboBox) | `StatisticsPeriodViewModel` | `pnl_Period.DataContext = m_periodVm`, TwoWay bindings | ✓ WIRED |
| `DoQuery`/`Btn_Rerun_Click`/`Btn_CpkExport_Click` | `StatisticsPeriodViewModel.BuildRange()` | 3 call sites, all confirmed | ✓ WIRED |
| `ProcessRow`/`ProcessCycleRow` | `StatisticsTimeRange.Contains` | Confirmed in source | ✓ WIRED |
| `SavedCycleRerunPlanner.CollectAutoTicks` | `StatisticsTimeRange.Contains(dto.InspectionTime)` | Confirmed in source, `TICKS`==`CYCLES` behaviorally | ✓ WIRED |
| RowStyle DataTrigger | `StatRow.StatusLevel == NoResult` | `Value="NoResult"` DataTrigger present | ✓ WIRED |

### Behavioral Spot-Checks / Probe Execution

All checks in this verification were live, independently-executed behavioral probes (rebuilt binary + recompiled scratch harness + fresh runs), not copies of SUMMARY numbers. Summary:

| Check | Result |
|---|---|
| Debug\|x64 build | 0 errors |
| Whole-day regression (12 CSVs vs awk) | identical |
| Multi-day full range (0723–0914) | 20598 == 20598 |
| Multi-day partial boundary (0723 17:00 → 0724 10:00) | 216 == 216 |
| Corrupt-row isolation test | skipped cleanly, no crash, not counted |
| range r1–r5 (N/REC/LEVEL/CPK/SUMMARY) | all match plan spec |
| contains (9 boundary assertions) | all match |
| stamps (4 filename assertions) | all match |
| CYCLES/TICKS parity | 92/92, 40/40, 40/40, 0/0, 0/0 |
| Hard-rule grep (6 checks × 4 files + xaml hbk) | 0 violations everywhere |

### Requirements Coverage

| Requirement | Description | Status | Evidence |
|---|---|---|---|
| R1-period-date-and-time | Date + hour:minute period, default 00:00–23:59, unchanged results | ✓ SATISFIED | Sections 1, 5 |
| R2-row-level-time-filter | Row-level 검사일시 filter, corrupt rows skipped, date-only overload compat | ✓ SATISFIED | Section 2 |
| R3-same-period-rerun-and-export | Rerun + export share [From,To], filename rule | ✓ SATISFIED | Section 3 |
| R4-data-presence-at-a-glance | NoResult purple row, RecordCount column, summary counts | ✓ SATISFIED | Section 4 |

### Anti-Patterns Found

None. See Section 6 for the independently re-run hard-rule grep results (0 violations across all 5 changed files) and debt-marker scan (none found).

### Human Verification Required

None required to pass this verification — all must-haves are code-verifiable and were independently confirmed against the live build with real data. The SUMMARY's 9-item real-hardware UAT checklist remains correctly unchecked and is out of scope for this code-level verification (it covers visual/interactive confirmation on the physical station, which this environment cannot perform).

### Gaps Summary

No gaps found. All 8 must-have truths (R1–R4 goal items plus hard-rule/SUMMARY-honesty checks) verified independently against the live codebase and a freshly rebuilt binary, not from SUMMARY.md claims. The whole-day regression, multi-day boundary, and corrupt-row isolation checks in particular were re-derived and re-run from scratch in this session (not merely re-read from prior harness output files) to guard against a stale/misleading SUMMARY.

---

_Verified: 2026-09-14T05:54:52Z_
_Verifier: Claude (gsd-verifier)_
