---
phase: 67-stat-01
reviewed: 2026-07-07T00:00:00Z
depth: standard
files_reviewed: 10
files_reviewed_list:
  - WPF_Example/Custom/Sequence/Inspection/CycleResultSerializer.cs
  - WPF_Example/Custom/Sequence/Inspection/MeasurementHistoryCsvLoader.cs
  - WPF_Example/Custom/Sequence/Inspection/MeasurementHistoryCsvWriter.cs
  - WPF_Example/DatumMeasurement.csproj
  - WPF_Example/MainWindow.xaml.cs
  - WPF_Example/Setting/SystemSetting.cs
  - WPF_Example/UI/MenuBar.xaml
  - WPF_Example/UI/MenuBar.xaml.cs
  - WPF_Example/UI/Statistics/StatisticsWindow.xaml
  - WPF_Example/UI/Statistics/StatisticsWindow.xaml.cs
findings:
  critical: 0
  warning: 2
  info: 3
  total: 5
status: issues_found
---

# Phase 67: Code Review Report

**Reviewed:** 2026-07-07
**Depth:** standard
**Files Reviewed:** 10
**Status:** issues_found

## Summary

STAT-01 adds a production-history statistics feature: a per-measurement CSV writer
(`MeasurementHistoryCsvWriter`), a query/aggregation loader
(`MeasurementHistoryCsvLoader`) that reuses the existing `RepeatMeasurementStats`
engine, a new `StatisticsSavePath` setting, a menu button, and a non-modal
`StatisticsWindow` with ChartDirector histogram/trend charts.

Overall the code is defensive and well-isolated. The CSV append is wrapped in an
independent try/catch inside the existing `CycleResultSerializer.SaveAsync`
background task, so a statistics failure cannot affect the inspection or TCP paths —
no regression risk to the live inspection/reviewer flow. Number formatting uses
`CultureInfo.InvariantCulture` with `F4` on both write and parse (round-trip safe),
the writer holds a static lock for concurrent-sequence append safety, and the
convention bans (no ternary, `if/else`, C# 7.2 only) are respected throughout the
new files. The new `StatisticsSavePath` setting survives old INI files: a missing
key throws inside the guarded `SystemSetting.Load` loop (via `CreateDirectory(null)`)
before `SetValue`, so the C# default initializer is retained — no zero/null clobber.

Two correctness/robustness gaps are worth addressing (CSV writer/reader asymmetry on
embedded newlines, and a concurrent read/append file-sharing collision). The rest are
minor chart/culture polish items.

## Warnings

### WR-01: CSV writer RFC4180-quotes embedded newlines, but the reader is line-based (round-trip asymmetry)

**File:** `WPF_Example/Custom/Sequence/Inspection/MeasurementHistoryCsvWriter.cs:130-141` (writer `Esc`) and `WPF_Example/Custom/Sequence/Inspection/MeasurementHistoryCsvLoader.cs:98-119` (reader `LoadFile`)
**Issue:** `Esc()` correctly wraps a field in quotes and doubles inner quotes when the
value contains `,` `"` `\r` or `\n` (RFC4180). However `LoadFile` reads records with
`File.ReadAllLines(...)`, which splits on every physical newline regardless of quote
state. A field that actually contains an embedded `\r`/`\n` would be written as a
single quoted record spanning two physical lines, then read back as two separate
lines — each parsed independently, each yielding `fields.Count < COLUMN_COUNT`, so
both are silently dropped (`continue`). The `ParseCsvLine` quoted-newline logic is
therefore unreachable for the reader. In this domain the affected fields
(RecipeName/ShotName/FAIName/MeasurementName/TypeName) are very unlikely to contain
newlines, so real-world impact is low, but the write/read contract is inconsistent
and the failure is silent data loss.
**Fix:** Either (a) make the reader record-aware instead of line-aware — accumulate
physical lines until the running quote count is balanced before handing the record to
`ParseCsvLine`; or (b) since the writer already never emits multi-line records in
practice, defensively strip/replace CR/LF in `Esc` (e.g. replace with a space) so the
one-record-per-line invariant the reader relies on is guaranteed:
```csharp
// in Esc(), before quote decision
szValue = szValue.Replace("\r", " ").Replace("\n", " ");
```

### WR-02: Concurrent query-during-append can raise a file-sharing IOException and silently skip a full day's CSV

**File:** `WPF_Example/Custom/Sequence/Inspection/MeasurementHistoryCsvLoader.cs:96-124`
**Issue:** The writer appends under `s_lock` with `File.AppendAllText` (opens the file
for Write). The loader reads with `File.ReadAllLines(szPath, Encoding.UTF8)` with the
default `FileShare.Read`. If an inspection cycle appends to today's file at the exact
moment a user runs a query over a range that includes today, the read open can fail
with a sharing violation. `LoadFile` catches per file and logs, then continues — so
the *entire* day's data is skipped for that query, producing intermittently
incomplete statistics with no user-visible indication. The writer's `s_lock` does not
protect the reader because the reader never takes it.
**Fix:** Open the read with a share mode compatible with the concurrent append, e.g.:
```csharp
string[] lines;
using (var fs = new FileStream(szPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
using (var sr = new StreamReader(fs, Encoding.UTF8))
{
    var list = new List<string>();
    string ln;
    while ((ln = sr.ReadLine()) != null) list.Add(ln);
    lines = list.ToArray();
}
```
Alternatively take `MeasurementHistoryCsvWriter`'s `s_lock` around the read (couples
the two classes, less preferred).

## Info

### IN-01: DateTime formatting/parsing without InvariantCulture (non-Gregorian calendar risk)

**File:** `WPF_Example/Custom/Sequence/Inspection/MeasurementHistoryCsvWriter.cs:39,92` and `WPF_Example/Custom/Sequence/Inspection/MeasurementHistoryCsvLoader.cs:72`
**Issue:** The daily filename (`yyyyMMdd`) and the timestamp column
(`yyyy-MM-dd HH:mm:ss`) are formatted with the current culture. On a machine whose
default culture uses a non-Gregorian calendar (e.g. `th-TH` Buddhist), the year digits
could differ, and would be inconsistent with the rest of the codebase which formats
numbers via `InvariantCulture`. Same machine write+read stays self-consistent, so
impact is effectively nil today, but it is a latent portability inconsistency.
**Fix:** Pass `CultureInfo.InvariantCulture` to these `ToString` calls, matching the
numeric-field formatting already used in `BuildLine`.

### IN-02: Histogram USL/LSL marks can fall outside the plotted bin range and are placed on a categorical axis

**File:** `WPF_Example/UI/Statistics/StatisticsWindow.xaml.cs:254-262`
**Issue:** `dUslBin`/`dLslBin` are computed from the *observed value* min/max (`dMin`,
`dMax`), so when a tolerance limit lies outside the sampled data range the mark index
is negative or `> BIN_COUNT` and the USL/LSL line is drawn off the plot area or
clipped. The marks are also added with numeric coordinates on an x-axis configured via
`setLabels(...)` (categorical), so their placement relative to the bars is
approximate. Cosmetic only — no crash (the `dRange > 0` guard prevents divide-by-zero).
**Fix:** Clamp `dUslBin`/`dLslBin` into `[0, BIN_COUNT]` before `addMark`, or extend
the bin range to include USL/LSL when building the histogram so the spec limits are
always visible and correctly positioned.

### IN-03: ChartDirector charts and the non-modal window are not explicitly disposed

**File:** `WPF_Example/UI/Statistics/StatisticsWindow.xaml.cs:235-293` and `WPF_Example/MainWindow.xaml.cs:366-374`
**Issue:** Each row selection assigns a new `XYChart` to `viewer_Histogram.Chart` /
`viewer_Trend.Chart` without clearing the previous one, and closing the window leaves
`mStatisticsWindow` non-null (a fresh window is created on reopen because `IsLoaded`
is false). Native ChartDirector resources are reclaimed only on GC. This mirrors the
existing `ReviewerWindow` pattern, so it is consistent with the codebase, but under
heavy repeated open/query/select cycles native memory can accumulate transiently.
**Fix:** Optional — null the viewers' `Chart` and `mStatisticsWindow` on window
`Closed`, and/or set `mStatisticsWindow = null` in a `Closed` handler in
`MainWindow.PopupView` for symmetry. Low priority given the mirrored precedent.

---

_Reviewed: 2026-07-07_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
