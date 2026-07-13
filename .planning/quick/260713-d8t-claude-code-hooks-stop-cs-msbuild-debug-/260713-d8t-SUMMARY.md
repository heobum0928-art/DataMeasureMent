---
phase: quick-260713-d8t
plan: 01
subsystem: infra
tags: [claude-code, hooks, msbuild, nodejs, csproj, halcon]

# Dependency graph
requires: []
provides:
  - "Project-level .claude/settings.json (committed, not settings.local.json) wiring Stop + PostToolUse hooks"
  - "stop-build-verify.js: MSBuild Debug|x64 build gate on Stop, blocks Claude only when *.cs changes introduce error CS"
  - "orphan-cs-detect.js: PostToolUse(Write) warn when a new .cs is missing from DatumMeasurement.csproj Compile Include"
  - "cs-style-warn.js: PostToolUse(Edit|Write) added-line-only warn on C# 8+ syntax and non-standard HALCON SetColor colors"
affects: [future-cs-edits, gsd-execute-phase-cs-tasks]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Node.js Claude Code hook: stdin JSON read with 3s timeout, try/catch wrapping all logic, default process.exit(0) fail-open"
    - "No shebang line in hook scripts — Node strips shebang only when a file is the main module loaded via `node file.js`, not when content is passed through `new Function()` (breaks JSON-schema/lint style self-checks); hooks are always invoked via `node <path>`, so a shebang is functionally inert here and actively breaks the plan's own verify command"
    - ".gitignore carve-out pattern: `.claude/*` + explicit `!` negations for the specific committed paths, so `settings.local.json` and `worktrees/` stay ignored while `settings.json` and `hooks/` are tracked"

key-files:
  created:
    - .claude/settings.json
    - .claude/hooks/stop-build-verify.js
    - .claude/hooks/orphan-cs-detect.js
    - .claude/hooks/cs-style-warn.js
  modified:
    - .gitignore

key-decisions:
  - "Removed the `#!/usr/bin/env node` shebang line from all three hook scripts — it broke the plan's own automated verify command (`new Function(...)` throws SyntaxError on a leading shebang), and it serves no purpose since these scripts are only ever invoked as `node <path>`, never executed directly."
  - "Changed .gitignore from a blanket `.claude/` ignore to `.claude/*` plus explicit `!` negations for `settings.json` and `hooks/`, so the plan's requirement (commit settings.json + hooks, leave settings.local.json/worktrees ignored) is satisfiable — this was a hard blocker: `git add .claude/settings.json` failed outright before this fix."

patterns-established:
  - "Pattern 1: Claude Code project hooks live under .claude/hooks/*.js, registered from .claude/settings.json (tracked), never settings.local.json (untracked); each hook fails open on any error/timeout."

requirements-completed: [QUICK-260713-d8t]

# Metrics
duration: 20min
completed: 2026-07-13
---

# Quick Task 260713-d8t: Claude Code Hooks (Stop C# MSBuild Debug Gate + Style/Orphan Warnings) Summary

**Three project-level Claude Code hooks (Stop MSBuild gate, orphan .cs detector, C# 8+/HALCON-color style warner) wired via a newly-committed `.claude/settings.json`, all fail-open by design.**

## Performance

- **Duration:** 20 min
- **Started:** 2026-07-13T09:35:00+09:00 (approx.)
- **Completed:** 2026-07-13T09:55:44+09:00
- **Tasks:** 2
- **Files modified:** 5 (4 created, 1 modified)

## Accomplishments
- `stop-build-verify.js` blocks the Stop event with `{"decision":"block"}` when MSBuild Debug|x64 reports `error CS` on dirty `.cs` files, and fails open (no build, or build-but-allow) on every other path — no `.cs` changes, missing MSBuild.exe, spawn error, or 60s timeout.
- `orphan-cs-detect.js` warns (non-blocking `additionalContext`) when a written `.cs` file is not registered under `<Compile Include>` in `WPF_Example/DatumMeasurement.csproj`, and correctly skips CLAUDE.md's dated-backup convention (e.g. `Action_BottomInspection_0428.cs`).
- `cs-style-warn.js` scans only added text (`new_string` for Edit, `content` for Write — never a whole legacy file) for C# 8+ syntax (`record`, `??=`, `using var`) and non-whitelisted HALCON `SetColor` literals, emitting a non-blocking bullet-list warning.
- `.claude/settings.json` (fresh, tracked file) registers all three hooks via `node "$CLAUDE_PROJECT_DIR/.claude/hooks/<name>.js"`; `.claude/settings.local.json` and `.claude/worktrees/` remain untouched and gitignored.

## Task Commits

Each task was committed atomically:

1. **Task 1: Write three hook scripts + register in .claude/settings.json** - `d450880` (feat)
2. **Task 2: Smoke-test all three hooks and dry-run the MSBuild command** - no code changes (verification-only task; a live whitespace edit to `WPF_Example/App.xaml.cs` was made via the Edit tool to fire the real PostToolUse hooks, then reverted — working tree confirmed clean, no commit needed)

**Plan metadata:** committed separately by the orchestrator (docs artifacts excluded from this executor's commits per instructions).

## Files Created/Modified
- `.claude/settings.json` - Registers Stop, PostToolUse(Write), PostToolUse(Edit|Write) hooks
- `.claude/hooks/stop-build-verify.js` - MSBuild Debug|x64 gate on Stop event (107 lines)
- `.claude/hooks/orphan-cs-detect.js` - csproj Compile Include orphan warning on Write (78 lines)
- `.claude/hooks/cs-style-warn.js` - C# 8+/HALCON SetColor added-line warning on Edit|Write (97 lines)
- `.gitignore` - Carved out `.claude/settings.json` and `.claude/hooks/` from the blanket `.claude/` ignore rule

## Decisions Made
- Dropped the Node shebang line from all 3 hooks (see key-decisions above) — functionally inert for this use case, but broke the plan's own verify command via `new Function()`.
- Reworked `.gitignore`'s `.claude/` rule into `.claude/*` + explicit negations, the minimal change that keeps `settings.local.json`/`worktrees/` ignored while making `settings.json`/`hooks/` trackable.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Removed shebang line from all three hook scripts**
- **Found during:** Task 1 (automated verify step)
- **Issue:** The plan's own `<verify><automated>` command parses each hook file with `new Function(fs.readFileSync(...))`. A leading `#!/usr/bin/env node` shebang is only special-cased by Node when the file is the process's main module (`node file.js`); inside `new Function()` it is invalid JS and throws `SyntaxError: Invalid or unexpected token`.
- **Fix:** Removed the shebang comment line from `stop-build-verify.js`, `orphan-cs-detect.js`, and `cs-style-warn.js`. No functional change — these scripts are always invoked as `node <path>` per `.claude/settings.json`, never executed directly, so the shebang was decorative only.
- **Files modified:** `.claude/hooks/stop-build-verify.js`, `.claude/hooks/orphan-cs-detect.js`, `.claude/hooks/cs-style-warn.js`
- **Verification:** Re-ran the plan's exact verify command; output `OK`.
- **Committed in:** `d450880` (Task 1 commit)

**2. [Rule 3 - Blocking] `.claude/` was fully gitignored, blocking `git add .claude/settings.json`**
- **Found during:** Task 1 (post-write staging step)
- **Issue:** `.gitignore` line 30 was a blanket `.claude/` ignore ("Claude Code harness (worktrees, local settings)"). `git add .claude/settings.json` failed with "The following paths are ignored by one of your .gitignore files". The plan explicitly requires `.claude/settings.json` to be committed (not `settings.local.json`).
- **Fix:** Changed the rule to `.claude/*` plus explicit negations `!.claude/settings.json`, `!.claude/hooks/`, `!.claude/hooks/*`. Verified via `git check-ignore -v` that `.claude/settings.local.json` and `.claude/worktrees` remain ignored, while `settings.json` and the three hook files stage cleanly.
- **Files modified:** `.gitignore`
- **Verification:** `git status --short .claude/` showed the 4 intended files as addable; `git check-ignore -v .claude/settings.local.json .claude/worktrees` confirmed both still ignored after the change.
- **Committed in:** `d450880` (Task 1 commit)

---

**Total deviations:** 2 auto-fixed (both Rule 3 - blocking issues preventing task completion)
**Impact on plan:** Both fixes were required just to satisfy the plan's own stated verification/output requirements (valid-JS self-check, and "committed via `.claude/settings.json`"). No scope creep — no new files, no architectural changes.

## Issues Encountered
None beyond the two auto-fixed blocking issues above.

## User Setup Required
None - no external service configuration required. Hooks activate automatically for any Claude Code session opened in this repo (project-level `.claude/settings.json` is picked up alongside the user's global `~/.claude/settings.json`; both fire, per the plan's noted union behavior).

## Next Phase Readiness
- All three hooks verified via synthetic stdin (fail-open on malformed/empty input, loop-guard via `stop_hook_active`, git-gate skips builds when no `.cs` dirty, MSBuild dry-run clean with 0 `error CS`, orphan detector finds 0 real orphans + warns on a fake path + skips dated backups, style warner correctly flags `record`/`??=`/`using var`/invalid `SetColor` and stays silent on clean code and whitelisted/hex colors).
- Live smoke test: a real Edit-tool whitespace-only change to `WPF_Example/App.xaml.cs` was made and reverted; working tree confirmed byte-clean afterward (`git status --short` / `git diff --stat` both empty for that file).
- No blockers for future `.cs` work — the Stop hook will now automatically gate future sessions' C# edits against MSBuild compile errors.

---
*Phase: quick-260713-d8t*
*Completed: 2026-07-13*

## Self-Check: PASSED

All created files verified present on disk (.claude/settings.json, 3 hook scripts, .gitignore, this SUMMARY.md). Commit `d450880` verified present in `git log --oneline --all`.
