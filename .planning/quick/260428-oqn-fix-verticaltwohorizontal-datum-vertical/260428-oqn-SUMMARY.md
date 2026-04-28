---
quick_id: 260428-oqn
status: complete
date: 2026-04-28
commit: c6c15a4
---

# Quick Task 260428-oqn: Fix VerticalTwoHorizontal Vertical_* ROI render

## Root cause

Phase 14-03 W4-A split the VerticalTwoHorizontal vertical-search ROI from the `Line1_*` slot into a dedicated `Vertical_*` slot in three places — teach (`DatumFindingService.TryTeachVerticalTwoHorizontal`), draw write-back (`MainView.xaml.cs` teach step), hit-test (`BuildDatumRectCandidate`) — but `HalconDisplayService.RenderDatumOverlay` was missed and still rendered `Line1_*` unconditionally with only a "Vert" label remap.

Result: in VerticalTwoHorizontal mode, the user's drawn ROI saved into `Vertical_*` was never visualised, so they could not place it correctly. Teach failed → `LastTeachSucceeded` stayed false → the Horizontal A/B raw edge crosses (gated by that flag) also never appeared, masquerading as "edges not detected".

## Fix

Single edit at `WPF_Example/Halcon/Display/HalconDisplayService.cs` in `RenderDatumOverlay`. The unconditional `if (Line1_Length1 > 0 && Line1_Length2 > 0)` rectangle block was split by `AlgorithmTypeEnum`:

- `TwoLineIntersect` → `Line1_*` rectangle + "L1" label (existing behaviour preserved).
- `VerticalTwoHorizontal` → `Vertical_*` rectangle + "Vert" label (NEW).
- `CircleTwoHorizontal` → neither (legacy `Line1_*` INI residue no longer leaks).

All other rendering blocks (Line2, Circle, Horizontal A/B, origin cross, detected lines, raw edge points) untouched.

## Verification

Automated regex checks all PASS in executor (Vertical branch + Line1 branch + both labels + comment tag + no unconditional Line1 block).

Pending human-verify (build + UI smoke):
1. AlgorithmType = `VerticalTwoHorizontal` → draw Vertical ROI → blue/cyan rotated rectangle labeled `Vert` appears.
2. Teach Datum → A/B green/lime crosses appear after success.
3. Switch to `TwoLineIntersect` → Line1 + `L1` label still renders (regression check).
4. Switch to `CircleTwoHorizontal` on a recipe with legacy `Line1_*` INI values → no L1/Vert rectangle drawn.

## Commits

- `c6c15a4` — fix(260428-oqn): split RenderDatumOverlay Line1/Vertical by AlgorithmType
