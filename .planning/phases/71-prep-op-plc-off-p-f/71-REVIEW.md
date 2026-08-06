---
phase: 71-prep-op-plc-off-p-f
reviewed: 2026-08-07T00:00:00Z
depth: standard
files_reviewed: 4
files_reviewed_list:
  - WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs
  - WPF_Example/Custom/SystemHandler.cs
  - WPF_Example/TcpServer/VisionRequestPacket.cs
  - WPF_Example/TcpServer/VisionResponsePacket.cs
findings:
  critical: 1
  warning: 1
  info: 3
  total: 5
status: issues_found
---

# Phase 71: Code Review Report

**Reviewed:** 2026-08-07T00:00:00Z
**Depth:** standard
**Files Reviewed:** 4
**Status:** issues_found

## Summary

Reviewed the `$PREP` Op-field removal and the new automatic all-lights-off hook (`TryTurnOffLightsOnCycleEnd`) that replaces the PLC-driven `$PREP Op=0` request.

The `Op` field removal itself is clean: `PrepPacket.Op`/`PrepAckPacket.Op` are fully gone from both packet classes, the wire parser (`TryParsePrepFields`) tolerantly ignores a 3rd legacy field for backward compatibility, and `grep` across the repo (including `Test/*.py` mocks and UI code) found no remaining references to the removed field that would cause a compile error or a silent protocol mismatch. No ternary operators, Hungarian-prefixed bools, and if/else-only control flow are used consistently in the new code, matching project convention.

The one substantive problem is architectural: the new hook decides to turn off **every physical light group** the moment **one** `InspectionSequence`'s own z-index cycle reaches its own last index — but "last index" is deliberately scoped per-sequence (`ComputeLastZIndex` only counts Shots owned by that sequence), while the multi-camera z-index protocol is explicitly designed so that TOP/SIDE/BOTTOM sequences can own disjoint z-indices within one shared PLC cycle for a single part. This lets one sequence's "I'm done" conclusion extinguish lights a sibling sequence still needs mid-capture. There is also a secondary "fail to fire" gap: `Error()`/`Stop()` mid-cycle aborts build a Buffer (`IsBuffer=true`) response, so the light-off hook never runs on error paths, leaving lights on indefinitely — a real regression now that `$PREP Op=0` no longer exists as a fallback explicit-off mechanism.

## Critical Issues

### CR-01: Auto light-off is per-sequence-triggered but globally-scoped — can darken a sibling sequence mid-cycle

**File:** `WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs:702-733`

**Issue:**
`TryTurnOffLightsOnCycleEnd` fires whenever **this** `InspectionSequence` instance's own response has `IsBuffer==false` (i.e., *this sequence's* z-index cycle reached *its own* last index, per `ComputeLastZIndex`, which explicitly filters `shot.OwnerSequenceName == Name` — see lines 450 and 1406-1407). When it fires, it calls `TurnOffShotLights()`:

```csharp
public void TurnOffShotLights()
{
    LightHandler.Handle.SetOnOff(LightHandler.LIGHT_RING, false);
    LightHandler.Handle.SetOnOff(LightHandler.LIGHT_BACK, false);
    LightHandler.Handle.SetOnOff(LightHandler.LIGHT_ALIGN_COAX, false);
    LightHandler.Handle.SetOnOff(LightHandler.LIGHT_BAR, false);
    LightHandler.Handle.SetOnOff(LightHandler.LIGHT_RING7, false);
}
```

This unconditionally switches off **all** light groups (`RING` = `RING_CH1..6`, `BACK`, `ALIGN_COAX`, `BAR` = `BAR_1..4`, `RING7` — see `WPF_Example/Custom/Device/LightHandler.cs:14-32`), not just the channels this sequence's own Shots use. `LightHandler` is a process-wide singleton shared by every `InspectionSequence` thread (TOP/SIDE/BOTTOM), and `ApplyPrepToSequences` (`SystemHandler.cs:851-871`) already shows that a single `$PREP:site,z_index` broadcasts to *every* `InspectionSequence` — only the ones owning a Shot at that z-index actually react. That is exactly the topology this code was built for: one PLC z-index timeline shared across sequences, each sequence owning a disjoint subset of z-indices (e.g. TOP owns z=1,3; SIDE owns z=2,4).

Concretely: if TOP's own last owned z-index is 3, but the overall PLC part-cycle continues to z=4 (owned only by SIDE), then when TOP's response for z=3 goes out with `IsBuffer=false` (because *TOP* thinks its own cycle just ended), `TryTurnOffLightsOnCycleEnd` calls `TurnOffShotLights()` and kills every light group — including the ones SIDE is about to use (or is actively grabbing under) for z=4. This is a genuine risk of dark/underexposed images and silent false-NG results for the sibling sequence, and it is a regression from the previous design: previously only an external, single authority (the PLC sending an explicit `$PREP Op=0`) decided when it was safe to turn off all lights (presumably only once the *entire* multi-camera cycle for the part had finished). Now each sequence makes that global decision unilaterally and independently, with no cross-sequence coordination.

This is consistent with how carefully other parts of this same phase-family guard against cross-sequence interference (e.g. `IsShotOwnedBySequence`, Phase 70 D-02, "Top/Bottom/Side 병렬 실행 간섭 없음" commentary throughout the file) — this hook appears to have been added without the same scoping care.

**Fix:** Scope the "safe to turn off" decision to the whole PLC cycle, not to a single sequence's own last index. Two viable approaches:
1. Track a shared (e.g. `SystemHandler`-level, lock-protected) count/set of sequences that still have an owned Shot beyond the current z-index; only call `TurnOffShotLights()` once every sequence that participates in this cycle has independently reached its own last index.
2. Scope the off action itself — e.g. add a `TurnOffOwnShotLights()` that only switches off the channels this sequence's own Shots turned on (mirroring `ApplyShotLightsInternal`'s channel list per-shot), leaving channels owned by sibling sequences untouched.

```csharp
// Option 2 sketch — scope OFF to the channels this sequence's Shots actually use,
// instead of the always-global TurnOffShotLights().
public void TurnOffOwnShotLights()
{
    // iterate this sequence's owned Shots' channel usage and SetChannelOnOff(..., false)
    // only for channels this sequence applied via ApplyShotLightsInternal.
}
```

## Warnings

### WR-01: Mid-cycle Error()/Stop() abort never sets IsBuffer=false — light-off hook can fail to fire

**File:** `WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs:1395-1446` (see also `WPF_Example/Sequence/Sequence/SequenceBase.cs:388-398, 441-457`)

**Issue:** `SequenceBase.Error()` and `SequenceBase.Stop()` both call `AddResponse()` unconditionally when `RequestPacket != null` (`SequenceBase.cs:392, 449`), which for v1.0 protocol routes into `AddResponseV1Cycle()` → `BuildScopedResponse()`. But `ApplyCycleJudgement` (`InspectionSequence.cs:1683-1704`) decides `IsBuffer` purely from `bIsLastIndex = m_nCurrentZIndex >= m_nLastZIndex` — it has no way to know the response is being built because the sequence just aborted via an exception (`ExecuteAction` → `Error()`, `SequenceBase.cs:216-226`) or a manual `Stop()`. If the abort happens at any z-index short of this sequence's own last index, `packet.IsBuffer` stays `true`, so `TryTurnOffLightsOnCycleEnd`'s early-return guard (`bCycleEnded = !packet.IsBuffer`, `InspectionSequence.cs:725-729`) suppresses the light-off entirely.

Before Phase 71, this gap was harmless because the PLC could always send an explicit `$PREP Op=0` to force lights off regardless of what the vision side thought its cycle state was. With `Op` now removed and `$PREP` reduced to "always turn this z-index's lights on," there is no remaining code path that turns lights off after a mid-cycle exception/abort — they stay on until some later, unrelated `$PREP` happens to reconfigure the same channels for a different Shot.

**Fix:** Have `Error()`/`Stop()` propagate an explicit "this is an abnormal termination" signal into the v1.0 response path (or have `InspectionSequence` override/hook `OnError`/`OnStop`, which already exist as events on `SequenceBase` and are used elsewhere in this same file for `HandleFlowLogCycleEnd`) and call `TurnOffShotLights()` (subject to the CR-01 fix) unconditionally on those events, independent of `IsBuffer` classification.

## Info

### IN-01: Stale comment on `TurnOffShotLights` still describes removed `Op==0` semantics

**File:** `WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs:700-701`
**Issue:** The doc comment above `TurnOffShotLights()` still reads `"$PREP Op==0(사이클 종료) → 전 조명 그룹 소등"`, describing the mechanism Phase 71 just removed. The new call sites are correctly documented on `TryTurnOffLightsOnCycleEnd` (lines 711-717), but this older comment was left as-is and now misleads a reader into thinking `TurnOffShotLights` is still driven by an `Op` field.
**Fix:** Update the comment to point at the new caller (`TryTurnOffLightsOnCycleEnd`) instead of the removed `Op==0` semantics, e.g. "전 조명 그룹 소등 유틸 — 호출부는 TryTurnOffLightsOnCycleEnd(사이클 P/F 확정 시) 및 레거시 TurnOffPrepLights."

### IN-02: Redundant light-off possible when PLC continues past a mid-cycle immediate-F

**File:** `WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs:1442-1443, 1492-1495`
**Issue:** `TryApplyCrossZDatumImmediateFail` can set `packet.IsBuffer=false` before the true last index is reached, and its own re-entry guard (`m_bImmediateFailSent`) only prevents a second *F response*, not a second light-off. If the PLC does not honor the "skip remaining indices after F" convention documented at line 1357 (`"후속 Index skip 은 핸들러 주도(D-05)"` — not code-enforced) and keeps sending further z-index requests, `TryTurnOffLightsOnCycleEnd` will fire again at the real last index (since that classification is independent of `m_bImmediateFailSent`). The `LightHandler.SetOnOff` calls are idempotent so this isn't incorrect, just an extra hardware round-trip and a duplicate `[CycleLightOff]` log line per cycle in that scenario.
**Fix:** Optional — gate `TryTurnOffLightsOnCycleEnd` with `if (m_bImmediateFailSent) { /* already turned off */ }` style short-circuit, or accept as harmless given `SetOnOff` idempotency.

### IN-03: `Try` prefix used on a void, no-out-param method

**File:** `WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs:718`
**Issue:** Project convention reserves the `Try` prefix for the `out`-parameter/`bool`-return pattern (`TryInspectSingleEdge`, `TryFitLine`, etc., per CLAUDE.md). `TryTurnOffLightsOnCycleEnd(TestResultPacket, string, int)` returns `void` and has no `out` parameter — it means "attempt, may no-op" rather than the established pattern. This mirrors the existing sibling `TryApplyCrossZDatumImmediateFail` from Phase 68, so it's a continuation of an already-present local deviation rather than a new one.
**Fix:** Non-blocking; consider a name like `TurnOffLightsIfCycleEnded` if this convention drift is worth tightening in a future pass.

---

_Reviewed: 2026-08-07T00:00:00Z_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
