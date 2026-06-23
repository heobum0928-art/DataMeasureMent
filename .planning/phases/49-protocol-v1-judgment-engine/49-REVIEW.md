---
phase: 49-protocol-v1-judgment-engine
reviewed: 2026-06-23T00:00:00Z
depth: standard
files_reviewed: 4
files_reviewed_list:
  - WPF_Example/TcpServer/VisionResponsePacket.cs
  - WPF_Example/Custom/Sequence/Inspection/ShotConfig.cs
  - WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs
  - WPF_Example/TcpServer/TcpServer.cs
findings:
  critical: 0
  warning: 3
  info: 4
  total: 7
warning_resolved: 1   # WR-01 fixed 2026-06-23 (commit bc6252b)
status: issues_found
---

# Phase 49: Code Review Report

**Reviewed:** 2026-06-23
**Depth:** standard
**Files Reviewed:** 4
**Status:** issues_found

## Summary

Phase 49 adds the protocol v1.0 P/F/B judgment engine: `ECycleResult` enum, `ShotConfig.ZIndex`, the z_index multi-shot cycle engine in `InspectionSequence` (`AddResponseV1Cycle` plus helpers), and a static→instance refactor of `TcpServer.EncodingType`.

Overall the implementation is careful and well-documented. The v2.6 path is preserved verbatim behind the `UseProtocolV1` branch, so the regression risk to existing behavior is low. The engine is cleanly decomposed into small (≤30-line) helpers, follows the project's if/else + hungarian conventions in the new control code, and stays within C# 7.2.

The findings below are mostly edge-case correctness concerns in the judgment logic, not crashes. The most material one is a silent false-PASS when the recipe has no `ZIndex` values set (last-index `>=` comparison against `m_nLastZIndex == 0`).

## Warnings

### WR-01: Last-Index `>=` comparison can emit a false PASS when the recipe has no ZIndex configured

**Status:** ✅ RESOLVED 2026-06-23 (commit bc6252b) — `nMatchedShots` now passed into `ApplyCycleJudgement`; last-Index with 0 matched shots forces `'F'` (fail-safe). Build PASS.

**File:** `WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs:388-391`, `512-532`
**Issue:**
`AddResponseV1Cycle` computes `m_nLastZIndex = ComputeLastZIndex(...)`, which returns `0` when no owned shot has a non-zero `ZIndex` (the documented "ZIndex 미설정 레시피" case in `ShotConfig.cs:55`). For a measurement index (e.g. `z=1`), `bIsLastIndex = m_nCurrentZIndex >= m_nLastZIndex` evaluates to `1 >= 0 == true`. The packet is then treated as the *last* index. Because no shots match the scope, `AggregateIndexFais` adds zero FAIs, `m_bCycleHasNG` stays `false`, and `ApplyCycleJudgement` returns `EVisionResultType.OK` → wire result `'P'`.

`WarnIfEmptyScope` logs an error in this case, but the packet is still enqueued as a PASS. A misconfigured recipe therefore reports a clean PASS to the handler with no measurements — a silent false-accept, which is the worst failure direction for an inspection system.

**Fix:** When the scope is empty (no matched shots) on a non-datum index, do not emit a final `'P'`. Either force `'F'`, or hold the cycle in Buffer (`'B'`) so the handler cannot mistake an unconfigured recipe for a pass:
```csharp
// In ApplyCycleJudgement or BuildScopedResponse, after WarnIfEmptyScope:
if (bEmptyScopeOnLastIndex)
{
    packet.IsBuffer = false;
    packet.Result = EVisionResultType.NG; // never report PASS on an empty last-index scope
    return;
}
```
(Pass the matched-shot count through to the judgement step so it can distinguish "all OK" from "nothing measured".)

### WR-02: `IsBuffer`/`Result` coupling is fragile — a future change to `MapCycleJudgement` precedence would flip P/F to B

**File:** `WPF_Example/TcpServer/VisionResponsePacket.cs:456-471`, `WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs:517-518`
**Issue:**
`ApplyCycleJudgement` sets `packet.Result = EVisionResultType.OK` for the intermediate (Buffer) case and relies on `MapCycleJudgement` evaluating `IsBuffer` *before* `Result` to emit `'B'`. This implicit ordering dependency spans two files. If `IsBuffer` were ever left at its default `false` on the intermediate path, the engine would silently emit `'P'` (a false PASS during a cycle). The correctness of the whole engine hinges on a single boolean check order that is easy to break in a later edit.

**Fix:** Make the intermediate-Result value non-ambiguous so a missed `IsBuffer` cannot read as PASS. Setting `Result = EVisionResultType.NG` on the Buffer branch (while `IsBuffer=true` still maps to `'B'`) makes the failure mode fail-safe rather than fail-PASS. Alternatively, route serialization through the `ECycleResult` enum that this phase introduced (currently `ECycleResult` is declared but never consumed — see IN-01) so the cycle state is a single explicit value rather than two coupled booleans.

### WR-03: `IndexNumber` sentinel hardcoded as `-1` in two new sites instead of using `SENTINEL_NO_MATERIAL`

**File:** `WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs:540`, and existing `:160`
**Issue:**
`PersistAndEnqueueV1` initializes `int nIndexNumber = -1;` as the no-material fallback. `TestPacket.IndexNumber` is defined with `= SENTINEL_NO_MATERIAL` (`VisionRequestPacket.cs:441`), and the comment there documents `-1` as that sentinel. The new code duplicates the magic literal `-1` rather than referencing the named constant, so if the sentinel ever changes, this path drifts silently. (This mirrors a pre-existing instance at line 160, so it is a propagated smell, not a new pattern.)

**Fix:** Reference the named constant:
```csharp
int nIndexNumber = TestPacket.SENTINEL_NO_MATERIAL;
```

## Info

### IN-01: `ECycleResult` enum is declared but never referenced

**File:** `WPF_Example/TcpServer/VisionResponsePacket.cs:34-38`
**Issue:** The new `ECycleResult { Buffer, Pass, Fail }` enum is added (and well-documented as the D-07 cycle result), but no code reads or writes it — the engine instead uses `IsBuffer` + `EVisionResultType`. As written it is dead code. (Per WR-02, routing serialization through this enum would actually be the cleaner design and would make the type live.)
**Fix:** Either consume `ECycleResult` as the single source of cycle judgement, or remove it until the consumer exists, to avoid a misleading "this is how cycle results are represented" signal for future maintainers.

### IN-02: `ResetCycleState` leaves `m_nLastZIndex = 0` and relies on an external recompute contract

**File:** `WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs:290-296`, `396-404`
**Issue:** `ResetCycleState` zeroes `m_nLastZIndex` and documents (in a comment) that the caller *must* immediately recompute it. The single current caller (`HandleDatumIndexResponse`) does so correctly on the very next line, but this caller-obligation-by-comment pattern is brittle: a second future caller that forgets the recompute would leave `m_nLastZIndex == 0`, feeding the WR-01 false-PASS path. The field is also redundant to set here since every read path recomputes it.
**Fix:** Drop `m_nLastZIndex = 0` from `ResetCycleState` (it only tracks per-index transient state, not cycle-accumulated state), or have `ResetCycleState` take `recipeManager` and recompute internally so the obligation cannot be skipped.

### IN-03: Recoverable result code mapped to `'F'` loses ANG/TECHING distinction on the v1.0 wire (by design, worth flagging)

**File:** `WPF_Example/TcpServer/VisionResponsePacket.cs:464-470`
**Issue:** `MapCycleJudgement` collapses everything that is not Buffer and not OK into `'F'`, including `EVisionResultType.ANG` and `TECHING`. The v2.6 path preserves these as `'A'`/`'T'` (`GetResultString`). This is documented as intentional 3-state wire mapping, but it means a teaching/angle-fail state during a v1.0 cycle is indistinguishable from a real measurement NG. Acceptable if the v1.0 protocol spec only defines P/F/B/N, but confirm with the protocol owner that no consumer needs the ANG/TECHING distinction.
**Fix:** No code change required if the spec is P/F/B only. Otherwise add explicit branches in `MapCycleJudgement` for ANG/TECHING.

### IN-04: `ShotConfig.ZIndex` is not validated for negative values on load

**File:** `WPF_Example/Custom/Sequence/Inspection/ShotConfig.cs:58`
**Issue:** `ZIndex` is reflection-serialized by `ParamBase`, so a hand-edited or corrupted INI could load a negative value. `ParseCurrentZIndex` already normalizes incoming request z-index to non-negative, but a negative *recipe* `ZIndex` is never clamped — it would simply never match `AggregateIndexFais` (`shot.ZIndex == nZIndex` with `nZIndex >= 0`) and could skew `ComputeLastZIndex` (a negative max would still floor at the `nMax = 0` initializer, so the impact is bounded). Low severity; noting for defense-in-depth.
**Fix:** Optionally clamp in `ApplyShotDefaults`: `if (ZIndex < 0) ZIndex = 0;`.

---

## Notes on items explicitly called out in the review request (no defect found)

- **v2.6 vs v1.0 branch correctness:** The `UseProtocolV1` gate in both `AddResponse` (`InspectionSequence.cs:99`) and `VisionResponsePacket.Convert` (`:358`) reads the same singleton (`SystemHandler.Handle.Setting` == `SystemSetting.Handle`, verified). The v2.6 block is preserved verbatim; regression risk is low.
- **z_index parsing:** `ParseCurrentZIndex` correctly handles null/empty/non-numeric/negative `TestID` → 0. `RequestPacket` is typed `TestPacket`, so `TestID` access is valid.
- **Cycle reset:** Resetting on Index 0 (cycle start) rather than after the last index is the safer choice (a cycle aborted mid-stream still starts clean). `m_bCycleDatumFailed` correctly persists through to the last index since it is only cleared on the next Index 0 — so a datum failure at Index 0 still forces `'F'` at the final index even if the handler does not skip.
- **TcpServer instance-field refactor:** `EncodingType` static→instance is correct. `ConvertMessage` (both overloads) now reads `Parent.EncodingType`, `ApplyEncoding` is a plain instance method, and the default remains `MessageEncodingType.Default`, so encoding behavior is unchanged for the single live instance. No regression. (Pre-existing: `Header`/`Trailer` remain static — explicitly noted as out of scope in the code comment.)

---

_Reviewed: 2026-06-23_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
