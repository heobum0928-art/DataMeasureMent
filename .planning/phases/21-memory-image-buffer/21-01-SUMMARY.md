---
phase: 21-memory-image-buffer
plan: 01
subsystem: documentation
tags: [documentation, lifecycle, phase-21, buffer, xml-doc, halcon, BUF-02]

# Dependency graph
requires:
  - phase: 20-code-style-cleanup
    provides: "D-12 byte-identical preservation rule (untouched lines retain prior style/marker)"
  - phase: 06 (Phase 6 multi-light + fixture INI)
    provides: "ShotConfig _imageLock + Action_FAIMeasurement.EStep.Init existing path"
provides:
  - "ShotConfig 5 buffer-related members carry /// <summary> blocks documenting BUF-02 lifetime contract"
  - "InspectionRecipeManager.ClearShots carries XML doc enumerating recipe-change + app-shutdown channels (idempotent)"
  - "Action_FAIMeasurement EStep.Init carries D-02 channel #2 marker comment marking sequence-reset trigger"
  - "Code-grep discoverability of buffer lifetime contract via 'Phase 21 BUF-02' / '/// <summary>' on the canonical 3 files"
affects: [phase-21-02 (subscriber wire-up), phase-21-03 (UAT regression), phase-25-OUT-01 (image reviewer reads ShotConfig.GetImage), phase-26 (Hungarian refactor will preserve XML doc context)]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "XML doc ///<summary> for buffer-lifetime members (above signature, below hbk marker line)"
    - "hbk marker '//260510 hbk Phase 21' on a separate line above each XML doc block (no stacking with /// per Phase 20 D-12)"

key-files:
  created: []
  modified:
    - "WPF_Example/Custom/Sequence/Inspection/ShotConfig.cs (5 XML doc + 5 hbk markers, +42 lines, body byte-identical)"
    - "WPF_Example/Custom/Sequence/Inspection/InspectionRecipeManager.cs (1 XML doc + 1 hbk marker, +11 lines, body byte-identical)"
    - "WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs (1 hbk marker comment, +1 line, ?. line preserved)"

key-decisions:
  - "Use XML /// <summary> as the lifetime contract surface (D-06, AC#3 1차 충족) — code-adjacent doc preferred over standalone BUFFER-LIFETIME.md"
  - "Place hbk marker '//260510 hbk Phase 21' on a regular comment line above each /// block (Phase 20 D-12 — markers do not stack with XML doc lines)"
  - "Do NOT modify any '?.' operator line (Phase 20 D-12: untouched lines preserve prior marker/style); ShotConfig L43-L102 + InspectionRecipeManager.ClearShots body + Action_FAIMeasurement L61 (now L61 after marker insert) are byte-identical"
  - "Single ClearAllResults doc references EStep.Init at the implementation level (D-04 confirmed: no SequenceBase.OnReset hook introduced)"

patterns-established:
  - "BUF-02 lifetime XML doc: /// <summary> + ///<remarks-style enumerated channels (1)/(2)/(3) + idempotency note"
  - "Phase 21 marker placement: hbk on plain comment, /// on next lines, then signature — keeps XML doc parser-clean"

requirements-completed: [BUF-01, BUF-02]

# Metrics
duration: 25min
completed: 2026-05-10
---

# Phase 21 Plan 01: ShotConfig + InspectionRecipeManager + Action_FAIMeasurement BUF-02 Lifetime Documentation Summary

**ShotConfig 5 buffer members + InspectionRecipeManager.ClearShots + Action_FAIMeasurement.EStep.Init 에 BUF-02 lifetime 계약 (3 dispose channels: recipe-change / sequence-reset / app-shutdown) 을 XML /// summary + hbk marker 로 코드 인접 문서화 — 행위 변경 0, msbuild PASS, byte-identical 의미 보존**

## Performance

- **Duration:** ~25 min
- **Started:** 2026-05-10T (plan execution start)
- **Completed:** 2026-05-10
- **Tasks:** 2 (Task 1 — ShotConfig 5 members; Task 2 — InspectionRecipeManager.ClearShots + Action_FAIMeasurement EStep.Init)
- **Files modified:** 3
- **Lines added:** 54 (42 in ShotConfig + 11 in InspectionRecipeManager + 1 in Action_FAIMeasurement)
- **Lines deleted:** 0
- **Behavior lines preserved byte-identical:** ShotConfig L44-L46 (_imageLock/_image fields), L57-L62 (constructors), L72-L76 (SetImage body), L89-L92 (GetImage body), L107-L111 (ClearImage body), L138-L144 (ClearAllResults body); InspectionRecipeManager.cs L62-L67 (ClearShots body); Action_FAIMeasurement.cs L61 (`ShotParam?.ClearAllResults();` — `?.` operator un-modified per Phase 20 D-12).

## Accomplishments

- **AC#3 (수명 보장 시점 문서화) 1차 충족** — 6 XML doc blocks (5 in ShotConfig + 1 in InspectionRecipeManager) make BUF-02 lifetime contract grep-discoverable via `/// <summary>` and `260510 hbk Phase 21`.
- **D-02 channel #2 (sequence reset 마킹)** — Action_FAIMeasurement.cs EStep.Init now carries explicit marker comment immediately above `ShotParam?.ClearAllResults();` documenting Run-cycle entry as the sequence-reset dispose trigger.
- **AC#4 회귀 0** — msbuild Debug/x64 PASS, EXITCODE=0, 0 new warnings (3 pre-existing baseline warnings: MSB3884 ruleset env-only, CS0162 VirtualCamera.cs:266, CS0219 VisionAlgorithmService.cs:64 — all in files Plan 21-01 did NOT touch).
- **Phase 20 D-12 compliance** — every `?.` operator on un-modified lines (`_image?.Dispose()`, `image?.CopyImage()`, `_image?.CopyImage()`, `ShotParam?.ClearAllResults()`) retained byte-identical. Markers do NOT stack with XML doc lines (each hbk marker is a plain `//` comment line above the `/// <summary>` block).

## Task Commits

Each task was committed atomically:

1. **Task 1: ShotConfig 5 멤버 XML doc 강화 (D-06)** — `0647704` (docs)
   - 5 `/// <summary>` blocks (HasImage / SetImage / GetImage / ClearImage / ClearAllResults) + 5 `//260510 hbk Phase 21` markers
   - GetImage doc cites both consumer patterns (using-block in Action_FAIMeasurement.cs Measure step + try/finally in MainView.DisplayShotImage)
   - ClearImage doc enumerates 3 dispose channels (recipe change / sequence reset / app shutdown)
   - 1 file changed, 42 insertions(+), 0 deletions(-)
2. **Task 2: ClearShots XML doc + EStep.Init 마킹 (D-06 + D-02 channel #2)** — `5b97d95` (docs)
   - InspectionRecipeManager.cs: 1 `/// <summary>` block above ClearShots + 1 `//260510 hbk Phase 21` marker (channels 1+2 enumerated, idempotent guarantee documented)
   - Action_FAIMeasurement.cs: 1 marker comment line above `ShotParam?.ClearAllResults();` at L61 (`channel #2 — sequence reset 트리거`)
   - 2 files changed, 12 insertions(+), 0 deletions(-)

**Plan metadata commit:** (to follow — orchestrator will commit SUMMARY.md + STATE.md + ROADMAP.md)

## XML Doc Blocks Added (6 total)

| # | File:Line range | Member | Key phrases |
|---|------------------|--------|-------------|
| 1 | ShotConfig.cs:48-51 | `HasImage` | "_imageLock 으로 동기화", "임의 thread 에서 안전" |
| 2 | ShotConfig.cs:65-70 | `SetImage(HImage)` | "clone-on-input", "기존 _image 가 있으면 자동으로 Dispose", "호출자는 입력 image 의 소유권을 그대로 보유" |
| 3 | ShotConfig.cs:79-87 | `GetImage()` | "**호출자가 반환된 HImage 의 Dispose 책임을 진다**", `using` + try/finally 양쪽 패턴 인용 |
| 4 | ShotConfig.cs:95-105 | `ClearImage()` | "(1) 레시피 변경 — Custom/SystemHandler.cs OnRecipeChanged subscriber", "(2) 시퀀스 리셋 — Action_FAIMeasurement.cs EStep.Init", "(3) 앱 종료 — SystemHandler.Release()", "멱등 (idempotent)" |
| 5 | ShotConfig.cs:131-137 | `ClearAllResults()` | "sequence reset 트리거", "Action_FAIMeasurement.cs EStep.Init 단계 (Run 사이클 진입 시 매번)", "별도 OnReset 이벤트/메서드를 SequenceBase 에 도입하지 않고 EStep.Init → ClearAllResults 경로로 충족 (Phase 21 D-04)" |
| 6 | InspectionRecipeManager.cs:52-61 | `ClearShots()` | "(1) 레시피 변경 — Custom/SystemHandler.cs 의 OnRecipeChanged subscriber", "(2) 앱 종료 — SystemHandler.Release() 에서 Sequences.Dispose() 직전 호출", "ClearImage 가 null-safe 이므로 멱등 (idempotent) 호출 안전" |

## Marker Comments Added (7 total)

| # | File:Line | Marker | Purpose |
|---|-----------|--------|---------|
| 1 | ShotConfig.cs:47 | `//260510 hbk Phase 21: BUF-02 lifetime contract — _imageLock 동기화 가시화` | Above HasImage doc |
| 2 | ShotConfig.cs:64 | `//260510 hbk Phase 21: BUF-02 lifetime contract — clone-on-input + 자동 dispose` | Above SetImage doc |
| 3 | ShotConfig.cs:78 | `//260510 hbk Phase 21: BUF-02 lifetime contract — clone-on-output, caller-disposes` | Above GetImage doc |
| 4 | ShotConfig.cs:94 | `//260510 hbk Phase 21: BUF-02 lifetime contract — 명시 해제 hook (3 channels)` | Above ClearImage doc |
| 5 | ShotConfig.cs:130 | `//260510 hbk Phase 21: BUF-02 channel #2 — sequence reset 진입점` | Above ClearAllResults doc |
| 6 | InspectionRecipeManager.cs:51 | `//260510 hbk Phase 21: BUF-02 lifetime owner — recipe change + app shutdown 채널` | Above ClearShots doc |
| 7 | Action_FAIMeasurement.cs:60 | `//260510 hbk Phase 21: BUF-02 channel #2 — sequence reset 트리거 (Run 사이클 진입 시 image buffer + FAI results dispose)` | Inline above `ShotParam?.ClearAllResults();` (no XML doc — single intent marker) |

## msbuild Verification (Debug/x64)

| Metric | Baseline (pre-Plan-01) | Post-Task-1 | Post-Task-2 |
|--------|------------------------|-------------|-------------|
| EXITCODE | 0 | 0 | 0 |
| Errors | 0 | 0 | 0 |
| Warnings (total) | 3 (env+pre-existing) | 3 | 3 |
| New warnings introduced by plan | — | 0 | 0 |
| Output binary | DatumMeasurement.exe | OK | OK |

**Pre-existing warnings (NOT introduced by Plan 21-01, files un-touched):**
- `MSB3884` — `MinimumRecommendedRules.ruleset` not found (environmental, MSBuild-level, no source code).
- `CS0162` — `WPF_Example/Device/Camera/VirtualCamera.cs:266` unreachable code (SIMUL_MODE conditional return shadows runtime branch).
- `CS0219` — `WPF_Example/Halcon/Algorithms/VisionAlgorithmService.cs:64` `scanHorizontal` assigned but unused (legacy local).

The wpftmp warning duplication (each warning prints once for `*_wpftmp.csproj` and once for the main `DatumMeasurement.csproj`) is the standard WPF MSBuild XAML-compilation 2-pass artifact, not a real duplicate warning.

## grep Verification (verify block AC)

| Check | File | Expected | Actual |
|-------|------|----------|--------|
| `/// <summary>` count | ShotConfig.cs | ≥ 5 | 5 (HasImage, SetImage, GetImage, ClearImage, ClearAllResults) |
| `260510 hbk Phase 21` count | ShotConfig.cs | ≥ 5 | 5 |
| 호출자 / "caller MUST dispose" | ShotConfig.cs (GetImage doc) | ≥ 1 | 2 |
| `/// <summary>` count | InspectionRecipeManager.cs | ≥ 1 | 1 (ClearShots) |
| `260510 hbk Phase 21` count | InspectionRecipeManager.cs | ≥ 1 | 1 |
| `260510 hbk Phase 21` count | Action_FAIMeasurement.cs | ≥ 1 | 1 |
| `channel #2` count | Action_FAIMeasurement.cs | ≥ 1 | 1 |
| `ShotParam?.ClearAllResults` count | Action_FAIMeasurement.cs | exactly 1 (byte-identical) | 1 |

## Files Created/Modified

- `WPF_Example/Custom/Sequence/Inspection/ShotConfig.cs` — 5 XML doc blocks + 5 hbk markers (HasImage, SetImage, GetImage, ClearImage, ClearAllResults). Body lines (`_image?.Dispose()`, `_image = image?.CopyImage()`, `return _image?.CopyImage()`, `_image = null`, `foreach (var fai in FAIList) { fai.ClearResult(); }`) byte-identical preserved per Phase 20 D-12.
- `WPF_Example/Custom/Sequence/Inspection/InspectionRecipeManager.cs` — 1 XML doc block + 1 hbk marker above ClearShots. Body (`foreach (var shot in Shots) { shot.ClearImage(); } Shots.Clear();`) byte-identical preserved.
- `WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs` — 1 marker comment line at L60 above `ShotParam?.ClearAllResults();` at L61. The `?.` operator line itself byte-identical preserved.

## Decisions Made

- **D-06 (XML doc surface choice):** Used `/// <summary>` blocks placed above each method/property signature with the hbk marker on a separate `//` comment line directly above the `///` block. Rationale: Phase 20 D-12 stacking rule prevents combining hbk markers with XML doc text on the same line. Compiler treats `///` as documentation comment (separate token type from `//`), so a plain comment marker line gives both grep-discoverability AND clean XML doc parsing.
- **D-04 (no SequenceBase.OnReset):** Confirmed — ShotConfig.ClearAllResults doc explicitly cites "별도 OnReset 이벤트/메서드를 SequenceBase 에 도입하지 않고 EStep.Init → ClearAllResults 경로로 충족" — preserves Phase 21 scope minimization.
- **byte-identical preservation:** All `?.` operators on un-modified lines retained as-is. Phase 26 헝가리안 리팩토링 will normalize style; Phase 21-01 deliberately leaves them.

## Deviations from Plan

None — plan executed exactly as written. Both tasks completed with no auto-fix triggers (no Rule 1/2/3 deviations needed). The plan's `<files_overlap_warning>` block correctly forecasted that Plan 21-02 will additionally modify InspectionRecipeManager.cs (adding `Logging.PrintLog` inside ClearShots body); Plan 21-01 honored that boundary and added only the XML doc.

## Issues Encountered

- **MSBuild path resolution:** Plan referenced VS2019 path; this environment has VS2022 Community at `C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe`. Resolved via PowerShell wrapper script (`.planning/tmp/build.ps1`) that uses VS2022 path. No code/plan change — environmental adjustment only. (Build outputs were captured via Tee-Object, not the plan-specified `findstr` chain — equivalent verification pattern.)
- **PowerShell vs bash quoting:** The plan's `<verify><automated>` cmd-line uses backslashes that get mangled when invoked through git-bash. Worked around by invoking msbuild directly from a PowerShell script — same binary, same flags, same result.

## User Setup Required

None — pure documentation/marker-comment work, no external services touched.

## Next Phase Readiness

- **Plan 21-02 (subscriber wire-up + Release() ClearShots add):** Ready. The XML doc blocks added in Plan 21-01 already reference the channels Plan 21-02 will wire (channel #1 OnRecipeChanged subscriber + channel #3 SystemHandler.Release ClearShots) — Plan 21-02 simply makes the references true at the code level. ClearShots body in InspectionRecipeManager.cs is still byte-identical and ready to receive Plan 21-02's `Logging.PrintLog` line.
- **Plan 21-03 (UAT regression):** Ready. Byte-identical preservation of behavior lines guarantees AC#4 회귀 0 — SIMUL_MODE 1회 검사 should pass without functional change. AC#1 disk-free path (MainView.DisplayShotImage → shot.GetImage) is also documented in the new GetImage XML doc (cites the consumer pattern at MainView.DisplayShotImage) — provides the verification audit trail.
- **Phase 26 헝가리안 리팩토링:** XML doc + marker placement is style-stable (above the signature, on signature-adjacent comment lines) — Phase 26's Hungarian rename of `_image` / `_imageLock` will naturally update the XML doc body text but preserve the marker line layout.

## Self-Check: PASSED

**Files exist:**
- ShotConfig.cs (modified) — verified
- InspectionRecipeManager.cs (modified) — verified
- Action_FAIMeasurement.cs (modified) — verified

**Commits exist:**
- `0647704` (Task 1) — `git log` confirmed
- `5b97d95` (Task 2) — `git log` confirmed

**Build:** msbuild Debug/x64 EXITCODE=0, 0 new warnings.

**grep verification:** All 8 acceptance grep checks passed (counts exactly match expected minimums).

---
*Phase: 21-memory-image-buffer*
*Plan: 01*
*Completed: 2026-05-10*
