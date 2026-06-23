---
phase: 58-config-camera-a-2026-06-23
fixed_at: 2026-06-23T00:00:00Z
review_path: .planning/phases/58-config-camera-a-2026-06-23/58-REVIEW.md
iteration: 1
findings_in_scope: 5
fixed: 4
skipped: 0
deferred_annotated: 1
status: all_fixed
---

# Phase 58: Code Review Fix Report

**Fixed at:** 2026-06-23
**Source review:** `.planning/phases/58-config-camera-a-2026-06-23/58-REVIEW.md`
**Iteration:** 1

**Summary:**
- Findings in scope: 5 (WR-01, WR-02, WR-03, IN-01, IN-02)
- Fixed (code change + commit): 3 (WR-01/IN-01 combined, WR-02, IN-02)
- Deferred + annotated (per task instructions): 1 (WR-03)
- Skipped: 0
- Build result: **0 errors** (MSBuild Debug/x64, exit code 0)

---

## Fixed Issues

### WR-01 / IN-01: EnumerateDevice before HikCamera ctor in Connect()

**File modified:** `WPF_Example/Custom/EthernetVision/EthernetAlignCamera.cs`
**Commit:** `cc170cd`
**Applied fix:**
Moved `HikCamera.EnumerateDevice(ip)` call to the top of `Connect()`, before `new HikCamera(config, info)`. The device count guard now runs first; the `HikCamera` instance is only constructed after the device list confirms at least one device is reachable. Added a 4-line block comment explaining: (a) EnumerateDevice clears the shared static `DeviceList`, (b) construction only happens after count confirmed, (c) `SystemHandler` always calls this after `DeviceHandler` (Grabber) init so there is no concurrent enumeration. IN-01 (wasteful construction before device confirmed) is resolved as a side-effect of the WR-01 reorder.

---

### WR-02: try/finally HImage dispose guard in LoadFallbackImage()

**File modified:** `WPF_Example/Custom/EthernetVision/EthernetAlignCamera.cs`
**Commit:** `cc170cd` (same file, same atomic commit as WR-01)
**Applied fix:**
Restructured `LoadFallbackImage()` using the `try/finally` + null-sentinel pattern from REVIEW.md WR-02 and project convention (`Action_TopInspection.cs`). `loaded` is declared as `null` before the `try`. On the color path: `loaded.Dispose()` then `loaded = null` before returning `gray`. On the grayscale path: `result = loaded; loaded = null; return result` transfers ownership to caller. `finally { loaded?.Dispose(); }` fires as a no-op on success paths and disposes on any exception (including `CountChannels()` throw after `ReadImage()` succeeds). No change to public API or return semantics.

---

### IN-02: Clarifying comment on intentional double try-catch

**File modified:** `WPF_Example/SystemHandler.cs`
**Commit:** `6dd5d55`
**Applied fix:**
Added a 3-line comment immediately above the `try` block at line 188, stating: (1) this is intentional belt-and-suspenders, (2) `EthernetVisionHandler.Initialize()` is fully try-catch'd internally and never throws, (3) the outer catch should never fire. No logic change.

---

## Deferred / Annotated Issues

### WR-03: EthernetExposure INI property not yet applied to camera

**File:** `WPF_Example/Custom/SystemSetting.cs:79`
**Action:** Annotation added per task instructions — no code implementation.
**Commit:** `8d825f1`
**Annotation placed:** One-line deferral comment added directly above the `EthernetExposure` property declaration:

```
//260623 hbk Phase 58: EthernetExposure 적용은 Phase 59/61 카메라 런타임 배선 시
// (SetFloatValue ExposureTime) — 현재는 config 저장만
```

`SetFloatValue("ExposureTime", ...)` wiring is deferred to Phase 59/61 per task instructions and REVIEW.md WR-03 guidance. Property remains config-storage-only until that phase.

---

## Build Result

```
MSBuild Debug/x64 — exit code: 0 (0 errors, 0 warnings from modified files)
```

All three source changes (EthernetAlignCamera.cs + SystemSetting.cs + SystemHandler.cs) compile cleanly.

---

_Fixed: 2026-06-23_
_Fixer: Claude (gsd-code-fixer)_
_Iteration: 1_
