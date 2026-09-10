---
task: 260910-ly4-measurement-point-roi-search-region-axis
verified: 2026-09-10T16:15:00+09:00
status: passed
score: 7/7 checks verified (code-level); real-hardware UAT correctly left pending
commits_checked:
  - 006ee5ad (fix: axis mapping)
  - c46deabd (feat: bounds log)
  - 617ec068 (docs: SUMMARY + STATE)
---

# Verification Report — 260910-ly4-measurement-point-roi-search-region-axis

**Claim under test:** `VisionAlgorithmService.TryFitLine` searched the transpose of the
drawn measurement ROI (`halfW = roiLength1; halfH = roiLength2;`) and was fixed to
`halfH = roiLength1; halfW = roiLength2;`, plus a new `[FitLine] strip-loop: bounds ...`
observability log was added.

**Verdict: PASSED at the code level.** The axis mapping is correct, consistent with the
display/teaching side, does not touch the locked FAI convention, the new log is
positioned and formatted correctly, only the intended file changed, hard-rule compliance
holds on added lines, and SUMMARY.md accurately reports impact scope and leaves
real-hardware UAT as pending (not falsely claimed done).

## 1. Does the code now search the rectangle the user drew?

Read `WPF_Example/Halcon/Algorithms/VisionAlgorithmService.cs:109-123` (current state):

```csharp
double halfH = roiLength1;
double halfW = roiLength2;
double top = rRow - halfH;
double bottom = rRow + halfH;
double left = rCol - halfW;
double right = rCol + halfW;
double widthPx = right - left;
double heightPx = bottom - top;
```

Traced numerically with the task's own reference values (`Point_Length1=59`,
`Point_Length2=100`, `Row=1961.00686639642`, `Col=989.976408136146` — the on-disk recipe
values documented in PLAN.md `<orchestrator_data_corrections>`):

- `top = 1961.0069 - 59 = 1902.0069`, `bottom = 1961.0069 + 59 = 2020.0069` → **height =
  118 = 2×Point_Length1** ✓
- `left = 989.9764 - 100 = 889.9764`, `right = 989.9764 + 100 = 1089.9764` → **width =
  200 = 2×Point_Length2** ✓

Matches PLAN.md's own `<expected_bounds>` prediction (`top≈1902.0 left≈890.0
bottom≈2020.0 right≈1090.0`) exactly. Before the fix this would have been height=200 /
width=118 (transposed) — confirmed by reading the diff of commit `006ee5ad`, which shows
the pre-fix lines were `halfW = roiLength1; halfH = roiLength2;`.

**Strip-loop branches and `AppendStrip` argument order (unaffected by the swap, verified
by reading, not assumed):**

`AppendStrip` signature (`VisionAlgorithmService.cs:294-296`):
`AppendStrip(HImage image, double row1, double col1, double row2, double col2, ...)`

- `scanHorizontal == true` branch (`:151-173`): splits `heightPx` into `stripCount` rows
  (`r1`/`r2` from `top`), calls `AppendStrip(image, r1, left, r2, right, ...)` →
  `(row1=r1, col1=left, row2=r2, col2=right)`. Correct row/col order, and now iterates
  `heightPx` (=2×roiLength1, the *vertical* extent) — correct for a horizontal-direction
  scan (RtoL/LtoR) that needs vertical strips spanning the full drawn width.
- `scanHorizontal == false` branch (`:176-196`, TtoB/BtoT): splits `widthPx` into
  `stripCount` columns (`c1`/`c2` from `left`), calls
  `AppendStrip(image, top, c1, bottom, c2, ...)` → `(row1=top, col1=c1, row2=bottom,
  col2=c2)`. Correct row/col order, iterates `widthPx` (=2×roiLength2, the *horizontal*
  extent) — correct for a vertical-direction scan needing horizontal strips spanning the
  full drawn height.

Neither branch nor the `AppendStrip` call sites were touched by the diff (confirmed via
`git diff -U0`) — only the two mapping lines and the new log block were added. The
argument order was already correct before this fix and remains correct after it, because
the bug was purely in which `roiLength*` fed `halfH` vs `halfW`, not in how
`top/left/bottom/right` are consumed downstream.

## 2. Consistency with display/teaching side (MainView.xaml.cs, read-only)

`WPF_Example/UI/ContentItem/MainView.xaml.cs` was **not modified** by any of the three
commits (confirmed via `git show --stat` on all three — only
`VisionAlgorithmService.cs`, `.planning/STATE.md`, and this task's `SUMMARY.md` appear).

Read (not modified) to confirm the convention `TryFitLine` now follows matches it:

- `BuildPointRoiDefinitions` (`:428`+): `Row1 = X_Row - X_Length1, Column1 = X_Col -
  X_Length2` (and symmetric `Row2`/`Column2` additions) for every measurement type
  (ArcLineIntersect's 4 edges, DualImage's Point/Line, and the single-ROI types) —
  `Length1` maps to the row axis, `Length2` to the column axis.
- Drag-complete write (`:2803-2865`): `mHalfHeight = (Row2-Row1)/2`, `mHalfWidth =
  (Column2-Column1)/2`, then `*_Length1 = mHalfHeight; *_Length2 = mHalfWidth;` across
  all 9 measurement type branches.
- Resize write (`ApplyPointRoiResize` area, `:955-1017`): `halfR = (row2-row1)/2, halfC =
  (col2-col1)/2`, then `*_Length1 = halfR; *_Length2 = halfC;` — same convention.

All three teaching/display paths use `Length1 = row(vertical) half-extent, Length2 =
column(horizontal) half-extent`, which is exactly what `TryFitLine` now computes
(`halfH = roiLength1, halfW = roiLength2`). **The searched box and the drawn box now
agree.**

## 3. `FAIEdgeMeasurementService.cs` untouched

```
git log --oneline -3 -- WPF_Example/Halcon/Algorithms/FAIEdgeMeasurementService.cs
0522ff5d feat(log): ...
f14c8b9f refactor(57.1-09): ...
00ec5ad1 feat(57.1-02): ...
```

None of `006ee5ad`/`c46deabd`/`617ec068` appear. File is untouched — the opposite,
D-02-locked FAI convention (`ROI_Length1 = phi-direction half-major-axis`) is preserved
as-is.

## 4. New `[FitLine] strip-loop: bounds ...` log placement, values, format

Located at `VisionAlgorithmService.cs:138-149`, between `int failedStrips = 0;` (:136)
and `if (scanHorizontal)` (:151) — i.e. **before the strip loop**, as required.

Logs `top`, `left`, `bottom`, `right` — the exact same locals the loop consumes
immediately afterward (no copies, no recomputation), so the log is guaranteed to reflect
what the loop actually uses. This lets a user check the stated invariant directly from
the log line: `bottom-top == 2*Point_Length1`, `right-left == 2*Point_Length2`.

Format was compared byte-for-byte against `DatumFindingService.cs:1973`
(`strip-loop(extract)`): field order (`top/left/bottom/right/scan/stripCount/sigma/
threshold/polarity`), the double-space before `scan=` and before `stripCount=`, and the
`{0:F1}`/`{6:F2}` numeric formats all match. Output channel is `Logging.PrintLog((int)
ELogType.Algorithm, ...)`, same as the existing `[FitLine]` logs at `:203` and `:210` in
the same file (no new `using` needed, confirmed absent from the diff).

`szScanLabel` is set via explicit `if (scanHorizontal) { szScanLabel = "horizontal"; }`
with a `"vertical"` default and required braces — no ternary.

## 5. Only `VisionAlgorithmService.cs` changed in the code tree

```
git show --stat 006ee5ad   → WPF_Example/Halcon/Algorithms/VisionAlgorithmService.cs only
git show --stat c46deabd   → WPF_Example/Halcon/Algorithms/VisionAlgorithmService.cs only
git show --stat 617ec068   → .planning/STATE.md, .../SUMMARY.md only (docs commit)
```

`WPF_Example/DatumMeasurement.csproj` does not appear in any of the three commits
(`git log --oneline -3 -- WPF_Example/DatumMeasurement.csproj` shows only unrelated
pre-existing commits). Confirmed.

## 6. CLAUDE.md hard-rule compliance (added lines only)

Extracted added lines across both fix commits (`git diff -U0 006ee5ad~1 c46deabd --
VisionAlgorithmService.cs | grep '^+' | grep -v '^+++'`) and ran the grep battery from
`<hard_rules>`:

| Check | Result |
|---|---|
| Ternary `?:` | 0 |
| Null-coalescing `??` | 0 |
| Null-conditional `?.` | 0 |
| C#8 switch expression | 0 |
| `hbk` date-comment | 0 |

Brace check: the new `if (scanHorizontal) { szScanLabel = "horizontal"; }` has braces on
a single-line-bodied branch — compliant. Brace style: Allman (opening brace on its own
line), matching the file's newer-code style. New identifier `szScanLabel` correctly
carries the `sz` (string) Hungarian prefix; pre-existing locals (`halfW`, `halfH`, `top`,
`pol`, etc.) were left unrenamed, as instructed.

## 7. SUMMARY.md honesty check

Confirmed present in SUMMARY.md:
- Impact scope: "17곳, 측정 타입 9종" with the full per-type breakdown
  (`EdgeToLineDistanceMeasurement(1)`, `ArcLineIntersectDistanceMeasurement(4)`, etc.),
  matching PLAN.md's `<impact_scope>` verbatim.
- Explicit note that `Length1 != Length2` taught items "측정값이 달라질 수 있다" and "전
  항목 재검증이 필요" (existing items may measure differently, full re-verification
  needed) — not glossed over.
- Explicit statement "TOP / BOTTOM PC 에도 동일하게 해당된다" (both PCs equally affected)
  with pull/rebuild/re-verify instructions.
- The 4 real-hardware UAT items are listed under "실기 UAT — 사용자가 직접 수행
  (미완료)" with unchecked `- [ ]` boxes, and the top-level status field is
  `code-complete-uat-pending`, not a claim of completion. SUMMARY does not claim the bug
  is confirmed fixed on real hardware — it explicitly states the executor could not
  drive a real camera without SIMUL_MODE and stopped at code+build verification.

No overstatement found — SUMMARY.md's claims are consistent with what the code and git
history actually show.

## Additional checks performed (beyond the 7 requested)

- **Build:** Re-ran `MSBuild WPF_Example/DatumMeasurement.csproj
  /p:Configuration=Debug /p:Platform=x64` independently — build succeeded with 0 errors
  (`DatumMeasurement -> ...\bin\x64\Debug\DatumMeasurement.exe`).
- **Working tree:** `git status --porcelain` shows only the pre-existing untracked
  `PLAN.md` for this task (never committed — a minor process gap, not a code-correctness
  issue; does not affect the fix itself).
- **STATE.md diff:** confirmed the docs commit only appends one new row; no existing
  rows were altered (satisfies the "행 추가만" constraint in `<forbidden>` item 5).

## Gaps

None found at the code level. All 7 requested checks pass, plus build and forbidden-file
guards hold.

## Human Verification Required

The 4 real-hardware UAT items from PLAN.md/SUMMARY.md remain genuinely pending and
require the user to run this on real hardware (not verifiable from the codebase):

### 1. `[FitLine]` bounds log appears with correct invariant
**Test:** Re-run offline inspection on `SIDE_SHOT_2_C13-14_P1`, check
`D:\Data\Algorithm\*_Algorithm.log` for `[FitLine] strip-loop: bounds ...`.
**Expected:** `bottom-top ≈ 118` (2×Point_Length1=59), `right-left ≈ 200`
(2×Point_Length2=100).
**Why human:** Requires real camera image and live log file — code review can only
confirm the log is wired to fire with the correct values, not that the runtime
environment produces them as expected end-to-end.

### 2. Red edge mark lands inside the blue drawn box
**Test:** Visual check of `C13_P1` (EdgeToLineDistance) inspection result overlay.
**Why human:** Visual/rendering outcome, not inferable from source.

### 3. ROI move tolerance
**Test:** Move the ROI slightly, confirm measurement still succeeds (previously failed).
**Why human:** Runtime behavior on real hardware.

### 4. Regression check across other measurement items
**Test:** Confirm previously-passing measurements, especially any with `Length1 !=
Length2`, still behave correctly (values may legitimately change per impact scope, but
must be re-validated against spec/tolerance).
**Why human:** Requires re-running full recipe against real parts.

---

_Verified: 2026-09-10_
_Verifier: Claude (gsd-verifier)_
