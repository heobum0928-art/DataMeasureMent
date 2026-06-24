---
phase: 59-vision-algorithm-b-2026-06-23
fixed_at: 2026-06-24T00:00:00Z
review_path: .planning/phases/59-vision-algorithm-b-2026-06-23/59-REVIEW.md
iteration: 1
findings_in_scope: 4
fixed: 4
skipped: 0
status: all_fixed
---

# Phase 59: Code Review Fix Report

**Fixed at:** 2026-06-24
**Source review:** `.planning/phases/59-vision-algorithm-b-2026-06-23/59-REVIEW.md`
**Iteration:** 1

**Summary:**
- Findings in scope: 4
- Fixed: 4
- Skipped: 0

## Fixed Issues

### WR-01: HasTemplate Creates Directory as Side-Effect of a Pure Query

**Files modified:** `WPF_Example/Custom/EthernetVision/AlignShapeMatchService.cs`
**Commit:** f8fc978
**Applied fix:** Introduced `BuildShmPath(mode)` — pure path computation, no IO. Existing `GetShmPath(mode)` now delegates to `BuildShmPath` then calls `Directory.CreateDirectory` (write path only). `HasTemplate`, `TryLoadTemplate`, and `Run` switched to `BuildShmPath`. `TryTeach` retains `GetShmPath` as the sole write-path caller. Identical path strings preserved.

---

### WR-02: TryFindPose in Run Passes roiRow=0, roiCol=0 — Search Rectangle Is Implicitly Full-Image via Clamp

**Files modified:** `WPF_Example/Custom/EthernetVision/AlignShapeMatchService.cs`
**Commit:** f8fc978
**Applied fix:** Comment-only. Added a 4-line block comment above the `TryFindPose` call in `Run()` explaining the phantom-center pattern: center=(0,0) + len=99999 → GenRectangle1 clamp covers the full image; downsampleFactor=1.0 means no coordinate-scale restoration is needed. Logic unchanged.

---

### IN-01: TrySaveRefPose Serializes Without Explicit TypeNameHandling.None

**Files modified:** `WPF_Example/Custom/EthernetVision/AlignShapeMatchService.cs`
**Commit:** f8fc978
**Applied fix:** Replaced `JsonConvert.SerializeObject(refPose, Formatting.Indented)` with an explicit `JsonSerializerSettings` object (TypeNameHandling=None, Formatting=Indented) passed to `SerializeObject`. Now symmetric with the deserialize side. Comment references RCE guard intent.

---

### IN-02: RecipeSavePath Not Null-Guarded in GetShmPath

**Files modified:** `WPF_Example/Custom/EthernetVision/AlignShapeMatchService.cs`
**Commit:** f8fc978
**Applied fix:** Added `IsNullOrEmpty(recipePath)` guard at the top of `BuildShmPath` — returns null immediately on empty/null path. Callers (`HasTemplate`, `Run`, `TryTeach`) each handle null return as "no template" / `Found=false` / early error return, preventing `Path.Combine(null, ...)` from reaching the outer try/catch silently.

---

## Skipped Issues

None.

---

_Fixed: 2026-06-24_
_Fixer: Claude (gsd-code-fixer)_
_Iteration: 1_
