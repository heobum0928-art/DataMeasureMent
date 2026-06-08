---
phase: 21-memory-image-buffer
plan: 02
subsystem: lifecycle
tags: [lifecycle, event-subscriber, phase-21, buffer, BUF-01, BUF-02, AC#2]

# Dependency graph
requires:
  - phase: 21-01
    provides: "ShotConfig + InspectionRecipeManager XML doc + EStep.Init marker (BUF-02 lifetime contract documented)"
  - phase: 20-code-style-cleanup
    provides: "D-12 byte-identical preservation rule (untouched lines retain prior style/marker)"
provides:
  - "Custom/SystemHandler.cs partial methods WireBufferLifecycle / UnwireBufferLifecycle / OnRecipeChanged_FlushBuffers — D-02 channel #1 wire-up + D-04 lifecycle protection"
  - "WPF_Example/SystemHandler.cs Initialize() WireBufferLifecycle() invocation (after Sequences.ExecOnCreate, before Recipes.CollectRecipe)"
  - "WPF_Example/SystemHandler.cs Release() UnwireBufferLifecycle() + Sequences.RecipeManager.ClearShots() pair before Sequences.Dispose() — D-02 channel #3 + subscriber detach"
  - "InspectionRecipeManager.ClearShots Logging.PrintLog '[InspectionRecipeManager] ClearShots disposed {N} shot buffers' instrumentation — AC#2 dispose-proof grep target for Plan 03 UAT (D-11 ① equivalent)"
affects:
  - "phase-21-03 (UAT regression — recipe load × N counts ClearShots log lines for AC#2 verification)"
  - "phase-25-OUT-01 (image reviewer reads ShotConfig.GetImage; lifetime now grep-discoverable via XML doc + 3 channel wire-up)"
  - "phase-26 (Hungarian refactor will preserve subscriber method names; rename of `_image`/`_imageLock` will not touch lifecycle wire-up)"

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Partial-class subscriber wire-up: framework Initialize/Release call private partial methods declared in Custom/* file"
    - "Idempotent unsubscribe pattern (delegate -= null is no-op) — defensive lifecycle"
    - "Dispose-proof instrumentation via Logging.PrintLog grep target (AC#2 verification surrogate for HImage instance counter — D-11 ①)"

key-files:
  created: []
  modified:
    - "WPF_Example/Custom/SystemHandler.cs (3 new private/internal methods; +18 lines; existing partial body byte-identical except 1 trailing-blank-line trim before closing brace)"
    - "WPF_Example/SystemHandler.cs (Initialize +2 lines: WireBufferLifecycle marker + call; Release +4 lines: UnwireBufferLifecycle marker + call, ClearShots marker + call; existing operator/dispose lines byte-identical)"
    - "WPF_Example/Custom/Sequence/Inspection/InspectionRecipeManager.cs (+2 lines: marker + Logging.PrintLog as first statement of ClearShots; Plan 01 XML doc above + foreach/Shots.Clear body byte-identical)"

key-decisions:
  - "D-02 channel #1 wire location = Custom/SystemHandler.cs (per Plan 21-PATTERNS recommendation 1a — partial-class extension preserving framework/custom separation)"
  - "Subscriber method visibility: Wire/handler private (only framework partial calls), Unwire internal (lifecycle protection — D-04 Claude's Discretion: explicit unsubscribe even though singleton lifetime makes leak risk effectively zero)"
  - "Logging instrumentation placed BEFORE foreach loop so Shots.Count reflects pre-dispose count (UAT 'N개 buffer 가 있었음' 입증 — vs post-dispose=0)"
  - "Logging format string `[InspectionRecipeManager] ClearShots disposed {0} shot buffers` chosen as a unique grep token (zero pre-existing occurrences in project)"
  - "Initialize() wire position = after Sequences.ExecOnCreate(), before Recipes.CollectRecipe() (per Plan PATTERN recommendation — sequence callbacks all hooked before buffer wire)"
  - "Release() unwire ordering = UnwireBufferLifecycle() FIRST, then ClearShots(), then Sequences.Dispose() (so handler can no longer fire while ClearShots is iterating + before SequenceHandler tears down)"

patterns-established:
  - "Partial-class lifecycle wire pattern: Custom side declares private WireXxx/internal UnwireXxx + private OnXxxEvent_HandlerName; framework Initialize/Release calls them inline"
  - "Dispose-proof Logging instrumentation: trace log with unique '[ClassName] ActionName disposed {N} target' format string for grep-based UAT verification (HImage instance counter equivalent)"

requirements-completed: [BUF-01, BUF-02]

# Metrics
duration: 3min
completed: 2026-05-10
---

# Phase 21 Plan 02: D-02 Channel #1/#3 Wire-up + AC#2 Dispose-proof Instrumentation Summary

**Custom/SystemHandler.cs 에 3개 partial 메서드 (WireBufferLifecycle / UnwireBufferLifecycle / OnRecipeChanged_FlushBuffers) 신설 + framework SystemHandler.Initialize() / Release() 에 wire/unwire/ClearShots 호출 3라인 추가 + InspectionRecipeManager.ClearShots 에 Logging.PrintLog 1라인 instrumentation — Phase 21 D-02 lifetime 3채널 중 #1 (recipe change) + #3 (app shutdown) 코드 wire-up 완성 + AC#2 dispose 입증 grep 도구 확보 (Plan 03 UAT 가 trace 로그에서 카운트). msbuild Debug/x64 PASS, 0 신규 warning, 행위 보존.**

## Performance

- **Duration:** ~3 min
- **Started:** 2026-05-10T12:30:47Z
- **Completed:** 2026-05-10T12:33:21Z
- **Tasks:** 3 (Task 1 Release ClearShots / Task 2 Custom 3 methods + Initialize/Release wire / Task 3 ClearShots Logging)
- **Files modified:** 3
- **Lines added:** 27 (Custom/SystemHandler.cs +18 / WPF_Example/SystemHandler.cs +6 / InspectionRecipeManager.cs +2; net deletions 1 trailing-blank-line trim)
- **Lines deleted:** 1 (cosmetic trailing blank line in Custom/SystemHandler.cs trimmed during method append)
- **Behavior lines preserved byte-identical:** WPF_Example/SystemHandler.cs L168 `Devices.Dispose();`, L171 `Sequences.Dispose();`, L174 `RawImageSaver?.Dispose();`, L178 `Server.Dispose();`, all Plan 01 ShotConfig/InspectionRecipeManager doc lines + ClearShots body (foreach + Shots.Clear()).

## D-02 Channel Wire-up Matrix

| Channel | Trigger | Code Location | Lines Added | Verification |
|---------|---------|---------------|-------------|--------------|
| #1 (recipe change) | `Sequences.OnRecipeChanged` event fires (publisher: SequenceHandler.cs:162) | Custom/SystemHandler.cs `OnRecipeChanged_FlushBuffers` handler + `WireBufferLifecycle/UnwireBufferLifecycle` partial methods + framework SystemHandler.Initialize/Release wire/unwire calls | 18 (Custom) + 6 (framework) = 24 | grep `OnRecipeChanged += / -= / _FlushBuffers` ≥ 1/1/3 ✓ ; Logging line fires per recipe change |
| #2 (sequence reset) | `Action_FAIMeasurement.EStep.Init` enters | (Plan 01 marker — already in place, Plan 02 untouched) | 0 (already done) | Plan 01 marker `//260510 hbk Phase 21: BUF-02 channel #2 — sequence reset 트리거` at Action_FAIMeasurement.cs:60 |
| #3 (app shutdown) | `SystemHandler.Release()` invoked | WPF_Example/SystemHandler.cs Release() — 1 marker + `Sequences.RecipeManager.ClearShots()` line before `Sequences.Dispose()` | 2 | grep `Sequences.RecipeManager.ClearShots` = 1 ✓ ; Logging line fires once per app shutdown |

All 3 channels now produce a single trace log line `[InspectionRecipeManager] ClearShots disposed {N} shot buffers` per dispose event — grep target for Plan 03 UAT.

## Task Commits

1. **Task 1: WPF_Example/SystemHandler.cs Release() ClearShots 호출 추가 (D-02 channel #3)** — `3605eda` (feat)
   - Insert `Sequences.RecipeManager.ClearShots();` immediately before `Sequences.Dispose();`
   - 1 file changed, 2 insertions(+) (1 marker line + 1 call line)
   - Existing `// Release sequences.` comment + `Sequences.Dispose();` byte-identical
2. **Task 2: Custom/SystemHandler.cs 3 메서드 + framework Initialize/Release wire (D-02 channel #1, D-03)** — `86e498d` (feat)
   - Custom/SystemHandler.cs: WireBufferLifecycle (private), UnwireBufferLifecycle (internal), OnRecipeChanged_FlushBuffers (private handler) — 3 methods, 6 hbk markers
   - WPF_Example/SystemHandler.cs Initialize(): +2 lines (marker + WireBufferLifecycle() call) after Sequences.ExecOnCreate()
   - WPF_Example/SystemHandler.cs Release(): +2 lines (marker + UnwireBufferLifecycle() call) before Task 1's ClearShots line
   - 2 files changed, 23 insertions(+), 1 deletion(-) (deletion = trailing blank-line trim)
3. **Task 3: InspectionRecipeManager.ClearShots Logging instrumentation (AC#2)** — `c04ccbb` (feat)
   - Logging.PrintLog as the first statement of ClearShots() — emits Shots.Count BEFORE foreach loop disposes them
   - Plan 01 XML doc block above ClearShots + foreach + Shots.Clear() byte-identical
   - 1 file changed, 2 insertions(+) (1 marker line + 1 Logging.PrintLog line)

**Plan metadata commit:** (to follow — orchestrator will commit SUMMARY.md + STATE.md + ROADMAP.md)

## Methods Added (3 total — all in Custom/SystemHandler.cs)

```csharp
        //260510 hbk Phase 21: BUF-02 channel #1 — recipe change buffer flush wire-up (D-02 / D-03)
        private void WireBufferLifecycle() {
            //260510 hbk Phase 21: OnRecipeChanged subscriber 등록 — Sequences 가 SequenceHandler.Handle 로 초기화된 후 호출되어야 함
            Sequences.OnRecipeChanged += OnRecipeChanged_FlushBuffers;
        }

        //260510 hbk Phase 21: BUF-02 channel #1 — Release 시점 unsubscribe (subscriber lifecycle 보호 — D-04 Claude's Discretion)
        internal void UnwireBufferLifecycle() {
            //260510 hbk Phase 21: 멱등 — 미등록 상태에서도 안전 (delegate -= null 무동작)
            Sequences.OnRecipeChanged -= OnRecipeChanged_FlushBuffers;
        }

        //260510 hbk Phase 21: BUF-02 channel #1 — recipe change → InspectionRecipeManager.ClearShots() 전파
        private void OnRecipeChanged_FlushBuffers(object sender, RecipeChangedEventArgs args) {
            //260510 hbk Phase 21: 모든 Shot 의 image buffer dispose — Load() 경로 의존에서 명시 훅으로 승격
            Sequences.RecipeManager.ClearShots();
        }
```

## Diff — WPF_Example/SystemHandler.cs Initialize() (B-1)

**Before (L132-139, Plan 01 baseline):**
```csharp
            // 6) Hook sequence creation callbacks
            //    Typically sets up per-sequence resources.
            Sequences.ExecOnCreate();

            // 7) Collect recipe list
            //    Scans configured recipe directories.
            Recipes.CollectRecipe();
```

**After (L132-141, Plan 02 +3 lines):**
```csharp
            // 6) Hook sequence creation callbacks
            //    Typically sets up per-sequence resources.
            Sequences.ExecOnCreate();

            //260510 hbk Phase 21: BUF-02 channel #1 — OnRecipeChanged subscriber 등록 (Sequences 가 살아있고 ExecOnCreate 가 끝난 뒤 wire)
            WireBufferLifecycle();

            // 7) Collect recipe list
            //    Scans configured recipe directories.
            Recipes.CollectRecipe();
```

## Diff — WPF_Example/SystemHandler.cs Release() (B-2 + Task 1)

**Before (Plan 01 baseline):**
```csharp
            // Release sequences.
            Sequences.Dispose();
```

**After (Task 1 + B-2 = +4 lines):**
```csharp
            //260510 hbk Phase 21: BUF-02 channel #1 — subscriber 해제 (Sequences 가 살아있는 동안 unwire)
            UnwireBufferLifecycle();
            //260510 hbk Phase 21: BUF-02 channel #3 (app shutdown buffer flush — Sequences.Dispose 가 ClearShots 를 호출하지 않으므로 명시 dispose)
            Sequences.RecipeManager.ClearShots();
            // Release sequences.
            Sequences.Dispose();
```

Order rationale: **unwire first** (handler will no longer fire) → **explicit ClearShots** (last guaranteed buffer dispose) → **Sequences.Dispose** (sequence handler tears down, but RecipeManager already empty). All three lines protect against post-Dispose use-after-free.

## Diff — InspectionRecipeManager.cs ClearShots()

**Before (Plan 01 final state):**
```csharp
        public void ClearShots() {
            foreach (var shot in Shots) {
                shot.ClearImage();
            }
            Shots.Clear();
        }
```

**After (+2 lines as first statements of body):**
```csharp
        public void ClearShots() {
            //260510 hbk Phase 21: BUF-02 dispose 입증 instrumentation — UAT 가 recipe load × N 회 후 이 로그 라인 카운트로 dispose 검증
            Logging.PrintLog((int)ELogType.Trace, "[InspectionRecipeManager] ClearShots disposed {0} shot buffers", Shots.Count);
            foreach (var shot in Shots) {
                shot.ClearImage();
            }
            Shots.Clear();
        }
```

Plan 01 XML doc block (`/// <summary> ... </summary>`) above the method signature is byte-identical preserved. The hbk marker on the line above the doc is also unchanged.

## Marker Comments Added (Plan 02 only — 12 total)

| # | File | Approximate line | Marker text |
|---|------|------------------|-------------|
| 1 | Custom/SystemHandler.cs | header above WireBufferLifecycle | `BUF-02 channel #1 — recipe change buffer flush wire-up (D-02 / D-03)` |
| 2 | Custom/SystemHandler.cs | inside WireBufferLifecycle body | `OnRecipeChanged subscriber 등록 — Sequences 가 SequenceHandler.Handle 로 초기화된 후 호출되어야 함` |
| 3 | Custom/SystemHandler.cs | header above UnwireBufferLifecycle | `BUF-02 channel #1 — Release 시점 unsubscribe (subscriber lifecycle 보호 — D-04 Claude's Discretion)` |
| 4 | Custom/SystemHandler.cs | inside UnwireBufferLifecycle body | `멱등 — 미등록 상태에서도 안전 (delegate -= null 무동작)` |
| 5 | Custom/SystemHandler.cs | header above OnRecipeChanged_FlushBuffers | `BUF-02 channel #1 — recipe change → InspectionRecipeManager.ClearShots() 전파` |
| 6 | Custom/SystemHandler.cs | inside OnRecipeChanged_FlushBuffers body | `모든 Shot 의 image buffer dispose — Load() 경로 의존에서 명시 훅으로 승격` |
| 7 | WPF_Example/SystemHandler.cs | Initialize() above WireBufferLifecycle() call | `BUF-02 channel #1 — OnRecipeChanged subscriber 등록 (Sequences 가 살아있고 ExecOnCreate 가 끝난 뒤 wire)` |
| 8 | WPF_Example/SystemHandler.cs | Release() above UnwireBufferLifecycle() call | `BUF-02 channel #1 — subscriber 해제 (Sequences 가 살아있는 동안 unwire)` |
| 9 | WPF_Example/SystemHandler.cs | Release() above ClearShots line | `BUF-02 channel #3 (app shutdown buffer flush — Sequences.Dispose 가 ClearShots 를 호출하지 않으므로 명시 dispose)` |
| 10 | InspectionRecipeManager.cs | inside ClearShots above Logging line | `BUF-02 dispose 입증 instrumentation — UAT 가 recipe load × N 회 후 이 로그 라인 카운트로 dispose 검증` |

(Acceptance ≥ 8 → 10 actual ✓; Custom side = 6 ≥ 5 ✓; framework side = 3 ≥ 3 ✓.)

## msbuild Verification (Debug/x64)

| Metric | Baseline (pre-Plan-02) | Post-Task-1 | Post-Task-2 | Post-Task-3 |
|--------|------------------------|-------------|-------------|-------------|
| EXITCODE | 0 | 0 | 0 | 0 |
| Errors | 0 | 0 | 0 | 0 |
| Warnings (total) | 3 (env+pre-existing) | 3 | 3 | 3 |
| New warnings introduced by Plan 02 | — | 0 | 0 | 0 |
| Output binary | DatumMeasurement.exe | OK | OK | OK |

**Pre-existing warnings (NOT introduced by Plan 21-02 — files un-touched):**
- `MSB3884` — `MinimumRecommendedRules.ruleset` not found (environmental).
- `CS0162` — `WPF_Example/Device/Camera/VirtualCamera.cs:266` unreachable code.
- `CS0219` — `WPF_Example/Halcon/Algorithms/VisionAlgorithmService.cs:64` unused local.

WPF MSBuild's two-pass XAML compilation (`*_wpftmp.csproj` + main `DatumMeasurement.csproj`) duplicates each warning print but does not produce duplicate compiler warnings — standard artifact.

## grep Verification (verify-block ACs)

| Check | File | Expected | Actual |
|-------|------|----------|--------|
| `Sequences.RecipeManager.ClearShots` count | WPF_Example/SystemHandler.cs | = 1 | 1 ✓ |
| `Sequences.Dispose()` count | WPF_Example/SystemHandler.cs | = 1 (preserved) | 1 ✓ |
| `WireBufferLifecycle()` count (Initialize call) | WPF_Example/SystemHandler.cs | = 1 | 1 ✓ |
| `UnwireBufferLifecycle()` count (Release call) | WPF_Example/SystemHandler.cs | = 1 | 1 ✓ |
| `260510 hbk Phase 21` count | WPF_Example/SystemHandler.cs | ≥ 3 | 3 ✓ |
| `(Wire\|Unwire)BufferLifecycle` declarations | Custom/SystemHandler.cs | ≥ 2 | 2 ✓ (private + internal) |
| `OnRecipeChanged += ` count | Custom/SystemHandler.cs | = 1 | 1 ✓ |
| `OnRecipeChanged -= ` count | Custom/SystemHandler.cs | = 1 | 1 ✓ |
| `OnRecipeChanged_FlushBuffers` count | Custom/SystemHandler.cs | ≥ 3 (decl + += + -=) | 3 ✓ |
| `260510 hbk Phase 21` count | Custom/SystemHandler.cs | ≥ 5 | 6 ✓ |
| `[InspectionRecipeManager] ClearShots disposed` count | InspectionRecipeManager.cs | = 1 | 1 ✓ |
| `Logging.PrintLog` count | InspectionRecipeManager.cs | ≥ 1 | 3 (1 new + 2 pre-existing) ✓ |
| `260510 hbk Phase 21: BUF-02 dispose 입증` count | InspectionRecipeManager.cs | = 1 | 1 ✓ |
| `foreach (var shot in Shots)` count | InspectionRecipeManager.cs | = 1 (preserved) | 1 ✓ |

All 14 grep checks PASS.

## Files Modified

- `WPF_Example/Custom/SystemHandler.cs` — appended 3 new methods (WireBufferLifecycle / UnwireBufferLifecycle / OnRecipeChanged_FlushBuffers) before the closing `}` of the partial class. Existing partial body (MainRun, ProcessLightSet, ProcessRecipeChange, ProcessRecipeGet, ProcessSiteStatus, ProcessTest, SendTestError) byte-identical except 1 cosmetic trailing-blank-line trim before the closing brace (3 blank lines → 1 blank line).
- `WPF_Example/SystemHandler.cs` — Initialize() inserted 2 lines after `Sequences.ExecOnCreate();`; Release() inserted 4 lines before `Sequences.Dispose();`. All other Initialize/Release lines (Setting.Save, Devices.Dispose, RawImageSaver?.Dispose, Server.Dispose, Lights.Release, IsTerminated, mSystemThread.Join, Logging.PrintLog/Stop, IsReleased) byte-identical preserved including the existing `?.` operator (`RawImageSaver?.Dispose();` — Phase 20 D-12 untouched-line preservation).
- `WPF_Example/Custom/Sequence/Inspection/InspectionRecipeManager.cs` — ClearShots body received 2 new lines (1 marker + 1 Logging.PrintLog). Plan 01 XML doc block above the method signature + foreach loop + Shots.Clear() body byte-identical preserved.

## Decisions Made

- **D-02 channel #1 wire location:** Custom/SystemHandler.cs (per Plan 21-PATTERNS recommendation 1a — partial-class extension preserves framework/custom separation).
- **Subscriber method visibility:**
  - `WireBufferLifecycle` = `private` (only the framework partial calls it from Initialize)
  - `UnwireBufferLifecycle` = `internal` (D-04 Claude's Discretion: explicit unsubscribe even though singleton lifetime makes leak risk effectively zero — defensive, allows future test code in same assembly to verify subscriber count)
  - `OnRecipeChanged_FlushBuffers` = `private` (handler implementation detail)
- **Initialize() wire position:** After `Sequences.ExecOnCreate()`, before `Recipes.CollectRecipe()`. Rationale: sequence callbacks all hooked first; subscriber is wired before any recipe load can fire `OnRecipeChanged`.
- **Release() unwire ordering:** UnwireBufferLifecycle first → ClearShots → Sequences.Dispose. Handler can no longer fire while ClearShots iterates; ClearShots is the last guaranteed buffer dispose; Sequences.Dispose tears down a now-empty RecipeManager.
- **Logging instrumentation placement (Task 3):** First statement of ClearShots body, BEFORE the foreach. Captures pre-dispose `Shots.Count` so UAT log can prove "N buffers were live and got disposed" rather than the always-zero post-dispose value.
- **Logging format string:** `[InspectionRecipeManager] ClearShots disposed {0} shot buffers` — chosen for grep uniqueness (verified zero pre-existing occurrences in project before insertion).

## Deviations from Plan

None — plan executed exactly as written. No Rule 1/2/3 auto-fix triggers. The plan's recommended insertion points and method signatures were directly implementable. The only minor adjustment was a 1-line cosmetic trailing-blank-line trim in Custom/SystemHandler.cs (3 blank lines before the closing `}` collapsed to 1) introduced incidentally by the Edit operation that appended the 3 new methods — this is purely whitespace, no behavioral or grep impact.

## Issues Encountered

- **MSBuild path:** Plan 02 referenced VS2019 path; environment has VS2022 Community. Resolved by reusing the existing `.planning/tmp/build.ps1` PowerShell wrapper from Plan 21-01 (no new tooling). Same MSBuild flags (`/p:Configuration=Debug /p:Platform=x64 /verbosity:minimal /nologo`).
- **PreToolUse Read-before-Edit hook reminders:** Three benign reminders fired during Edits even though the target files had been Read earlier in this session and the Edits all applied successfully. Adjusted by re-confirming context after each reminder; no actual rework needed.

## Authentication Gates

None — pure code lifecycle wire-up + Logging instrumentation, no external services or auth surface touched.

## User Setup Required

None.

## Next Phase Readiness

- **Plan 21-03 (UAT regression):** Ready. SIMUL_MODE 1회 검사 + recipe load × N회 → trace 로그에서 `[InspectionRecipeManager] ClearShots disposed {N} shot buffers` 카운트로 AC#2 dispose 입증 가능. AC#1 disk-free path (MainView.DisplayShotImage → shot.GetImage) 도 Plan 01 XML doc 으로 audit 경로 확보.
- **Phase 25 OUT-01 (image reviewer):** Lifetime contract + 3 channel wire-up 모두 grep-discoverable — reviewer 가 ShotConfig.GetImage 를 호출할 때 메모리 buffer lifetime 가 명확히 정의됨.
- **Phase 26 (Hungarian refactor):** subscriber method names follow PascalCase convention (no Hungarian rename needed); `_image`/`_imageLock` rename will not affect Plan 02 wire-up since wire-up references `Sequences.RecipeManager.ClearShots()` (public surface).

## Self-Check: PASSED

**Files exist:**
- WPF_Example/Custom/SystemHandler.cs (modified) — verified
- WPF_Example/SystemHandler.cs (modified) — verified
- WPF_Example/Custom/Sequence/Inspection/InspectionRecipeManager.cs (modified) — verified

**Commits exist:**
- `3605eda` (Task 1) — `git log` confirmed
- `86e498d` (Task 2) — `git log` confirmed
- `c04ccbb` (Task 3) — `git log` confirmed

**Build:** msbuild Debug/x64 EXITCODE=0, 0 new warnings (3 pre-existing baseline preserved).

**grep verification:** All 14 acceptance grep checks passed.

---
*Phase: 21-memory-image-buffer*
*Plan: 02*
*Completed: 2026-05-10*
