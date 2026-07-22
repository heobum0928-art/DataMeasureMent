---
status: pending
phase: 68-z-cross-z-dual-image-3-2-vision-protocol-v1-0-md-z1-z2-z-ind
source: [68-05-PLAN.md, 68-VALIDATION.md, 68-CONTEXT.md]
started: 2026-07-22
updated: 2026-07-22
---

# Phase 68 Plan 05 — Task 1 UAT Prep (build + trigger-tool decision + scenario procedures)

This file is prepared by Plan 05 Task 1 (`type="auto"`, build+prep only, no source changes).
Task 2 (`type="checkpoint:human-verify"`) executes the 7 scenarios below in SIMUL_MODE and
records PASS/FAIL per scenario. Do not mark any `result:` in "## Tests" until a human has
actually run the scenario in the running application — no result may be inferred or assumed.

**⚠️ REQUIRED READING FOR GAP-CLOSURE PLANNING:** `68-GAP-ANALYSIS.md` (same directory) contains
the synthesized, multi-agent-verified analysis of the gaps found during interactive UAT discussion
below (Gaps section) — including one critical structural bug (cycle-reset timing) not yet reflected
in the raw notes below, corrected/regression-checked fix recommendations for the 3 original gaps,
and a priority order. Read it BEFORE planning — it supersedes the raw Gaps notes below wherever they
conflict, and the raw notes below should be treated as supporting detail/evidence, not the plan basis.

---

## 1. Full Rebuild Result

```
msbuild WPF_Example/DatumMeasurement.csproj -p:Configuration=Debug -p:Platform=x64 -t:Rebuild -v:normal
```

- **Result: PASS** — 0 errors, 10 warnings (all pre-existing CS0618 `TopSequence`/`BottomSequence`/
  `TopInspectionAction`/`BottomInspectionAction` obsolete-member warnings — same 5 warnings counted
  twice because MSBuild compiles the WPF temporary assembly pass and the main assembly pass
  separately; identical to the baseline recorded in every Plan 01-04 SUMMARY). **0 new warnings.**
- `DatumMeasurement.exe` produced at `WPF_Example/bin/x64/Debug/DatumMeasurement.exe`.
- Confirms Plan 01-04's changes compile together as a whole (data model + execution-scope filter +
  cross-Z capture/injection/response-gate + Datum cross-Z all present in the same binary).

---

## 2. UAT Trigger Tool Decision

**Finding: `Test/mock_vision_client.py` does NOT support z_index and cannot be used as-is for
this UAT.**

Evidence:
- `Test/mock_vision_client.py` sends a single hardcoded message
  `$TEST:1,2,BJWC73.20@` and nothing else — no `$PREP` packet is ever sent.
- As of commit `260626 hbk` (`WPF_Example/TcpServer/VisionRequestPacket.cs:327,340,372`), the
  `$TEST` packet **no longer carries a z_index field at all** — `TryParseTestFieldsV1` hardcodes
  `testPacket.TestID = SENTINEL_Z_INDEX_STR` and the comment states z_index is now injected by
  `SystemHandler` from `_lastPrepZIndex`, which is only set by a **preceding `$PREP` packet**
  (`ProcessPrep`, `WPF_Example/Custom/SystemHandler.cs:741-774`, sets `_lastPrepZIndex` when
  `Op!=0`). `ProcessTest` (`Custom/SystemHandler.cs:208-221`) then does
  `packet.TestID = _lastPrepZIndex.ToString()` before dispatch.
- Since `mock_vision_client.py` sends only a bare `$TEST` with no prior `$PREP`, every trigger it
  produces resolves to whatever `_lastPrepZIndex` last was (default/stale), not a chosen z_index —
  it cannot deterministically select z=1 vs z=2 vs z=0.

**Decision: use `DebugManualZTrigger` (in-process PREP+TEST bridge) as the UAT trigger tool for
all 7 scenarios below**, per the plan's `<uat_environment_note>`. This method is already wired
into the running application's UI and requires no code changes, no external script edits, and no
network hop:

- **Location:** `WPF_Example/Custom/SystemHandler.cs:783` `internal bool DebugManualZTrigger(string seqName, int zIndex)`.
  Internally calls `ProcessPrep(new PrepPacket { ZIndex = zIndex, Op = 1 })` (sets
  `_lastPrepZIndex` + turns on that z_index's Shot lights) then `ProcessTest(new TestPacket { Identifier = seqName })`
  — i.e. exactly the production `$PREP`→`$TEST` sequence, just invoked directly instead of over TCP.
- **UI panel:** `WPF_Example/UI/ContentItem/MainView.xaml.cs:121-161` — MainView has a
  "수동 Z 트리거" (manual Z trigger) panel with:
  - `combo_ManualZSeq` — dropdown populated with every registered sequence name (`TOP`, `BOTTOM`, `SIDE`)
  - `txt_ManualZIndex` — integer z_index text box
  - a trigger button (`ManualZTriggerButton_Click`) that calls `DebugManualZTrigger(seqName, nZIndex)`
    and shows a message box with success/failure.
- **Note:** each trigger targets exactly one named sequence (`Sequences[seqName]` in `ProcessTest`).
  Since SHOT_E5 (the recommended scenario 2/2b Shot, see §4 below) is owned by `BOTTOM`, select
  `BOTTOM` in `combo_ManualZSeq` for those scenarios.
- **Pre-check before starting any scenario:** open Settings and confirm `OfflineInspectMode` is
  **OFF** (per plan's `<uat_environment_note>` — SIMUL_MODE's `EStep.DatumPhase`/`Measure` always
  read `SimulImagePath` regardless of this flag, but turning `OfflineInspectMode` ON blocks `$TEST`
  itself at a different gate, which would prevent all 7 scenarios from running at all).

---

## 3. Grab-Count Observability Gap (flagged, not fixed — out of Task 1's no-source-change scope)

Scenarios 1 and 1b ask the tester to confirm "실제 grab된 Shot 로그 카운트" (actual grabbed-Shot
count from logs). A repo-wide check found **no per-Shot grab-success trace log exists** in the
current codebase — `WPF_Example/Sequence/Sequence/SequenceBase.cs` and
`WPF_Example/Sequence/Action/ActionBase.cs` (the tick-execution core) contain zero `Logging.PrintLog`
calls, and camera-driver `ELogType.Camera` logs are almost entirely error-path only (the one
`[GRAB]` success line in `HikCamera.cs:479` is commented out). SIMUL_MODE bypasses camera drivers
entirely (`LoadShotInspectionImage()` loads directly from `SimulImagePath`), so even that
commented-out line would not fire in this UAT.

**This is a real gap, but adding a log line is a source change and out of Task 1's declared scope
(`<files>none (build + UAT prep, no source changes)</files>`)** — flagging here for the tester and
for future phase awareness rather than silently working around it.

**Recommended proxy signals for "was this Shot actually invoked this tick" during UAT** (use one or
both):
1. **InspectionListView grid** — each FAI row's `LastMeasuredValue` / `LastHasResult` /
   `LastJudgement` only update for Shots whose Action actually ran this cycle. Note each targeted
   Shot's displayed value before the trigger, trigger, then confirm only the expected Shots'
   rows changed.
2. **Result image save timestamps** — every measured FAI writes
   `LastOriginImageFileName`/`LastCaptureImageFileName` under
   `D:\Data\Result\Image\<yyMMdd>\<HHmm>\{original,capture}\..._<ShotName>_<FAIName>_..._<HHMMSSfff>.jpg`
   (see e.g. `origin_BOTTOM_FAI_E5_P1P2_OK_102814540.jpg` in the current recipe's last-run data).
   A fresh timestamp within the trigger's time window = that Shot's FAI(s) executed this tick; an
   unchanged/stale timestamp = it did not.

Both signals are already-existing side effects of measurement, not new instrumentation — no
source change needed to use them.

---

## 4. Independence-Test Recipe/Procedure (Scenario 2b prep)

**Candidate Shot: `SHOT_E5` (BOTTOM sequence), recipe `D:\Data\Recipe\FAI_1\main.ini`.**

Why this Shot: it already owns two `DualImageEdgeDistance` measurements
(`SHOT_8_FAI_0_MEAS_0` = `E5_P2`, `SHOT_8_FAI_0_MEAS_1`) with no `ZIndexA`/`ZIndexB` keys in the
INI today (confirmed via `grep -n "ZIndexA\|ZIndexB" main.ini` → zero matches anywhere in the
recipe — this is a fully "unconfigured" recipe, safe starting point). `SHOT_E5`'s own
`ShotConfig.ZIndex` is currently `0` (`SHOT_8_CAM` section, `ZIndex=0`). The only non-zero
`ZIndex` values anywhere in this recipe belong to **TOP** sequence Shots
(`SHOT_A1-23-C1-C12`=1, `SHOT_I9`=2, `SHOT_I10`=3) — BOTTOM/SIDE are entirely `ZIndex=0` today.
This means setting `SHOT_E5`'s measurement `ZIndexA=1`/`ZIndexB=2` immediately creates a
cross-Z owning Shot whose own `ZIndex` (0) already differs from the completion index
(`max(1,2)=2`) — the "third value" independence case is the recipe's natural state, no edit
needed for that half.

**All edits below are made live through the running application's Teaching UI (PropertyGrid),
NOT by hand-editing the INI file directly** — this avoids any risk of corrupting the production
recipe's text structure and lets `IsHiddenForAlgorithm`/`Load()` behave exactly as production
code expects. **Back up `main.ini` before starting** (`copy main.ini main.ini.bak_260722_uat`) so
the recipe can be restored byte-identical after UAT regardless of what the Teaching UI writes back.

**Setup steps (do once, before Scenario 2, then keep for Scenario 2b):**

1. Launch the app in SIMUL_MODE (Debug/x64), load recipe `FAI_1`, confirm `IsRecipeReady=true`.
2. Open the Teaching window → navigate to `SHOT_E5` (BOTTOM) → FAI `FAI_E5` → measurement `E5_P2`
   (`SHOT_8_FAI_0_MEAS_0`, `TypeName=DualImageEdgeDistance`).
3. In the measurement's PropertyGrid, category **"Image|DualImage"**, set:
   - **"Point z_index (ZIndexA)"** = `1`
   - **"Line z_index (ZIndexB)"** = `2`
   (both fields already exist per Plan 01 — no rebuild needed, just PropertyGrid edit + Save recipe.)
4. Save the recipe.

**Config A — shot.ZIndex = third value (default, already true, no further edit needed):**
- `SHOT_E5`'s `ShotConfig.ZIndex` stays at its current value `0` (≠ 1, ≠ 2 — a value unrelated to
  either capture index or the completion index).
- Run Scenario 2 exactly as written (z=1 capture, z=2 report) with this config. Confirms the
  completion-index response gate does not depend on `shot.ZIndex` at all when it's a value with no
  relationship to `ZIndexA`/`ZIndexB`.

**Config B — shot.ZIndex = one of the capture indices (non-completion), for the stronger 2b check:**
- In the Teaching UI, navigate to `SHOT_E5`'s Shot-level properties (not the measurement) and
  change `ZIndex` from `0` to `1` (matches `ZIndexA`, i.e. the *non-completion* capture index —
  this is the specific case the BLOCKER fix (Plan 03 Task 4) targets, since a shot.ZIndex==1 read
  under the old fragile "recipe convention" would have caused the FAI item to wrongly surface in
  the z=1 (non-completion) response).
- Save recipe, repeat Scenario 2's steps (z=1 then z=2), confirm the FAI item is **still absent**
  from the z=1 response and **still present only** in the z=2 response.
- **Revert `ShotConfig.ZIndex` back to `0` after this check** (Teaching UI or restore from the
  `main.ini.bak_260722_uat` backup) so the recipe returns to its pre-UAT state for D-07 regression
  purposes (Scenario 4 needs `SHOT_E5` back in its original, unmodified-INI-default state to prove
  zero regression against the *original* recipe file — restore the backup entirely after all of
  Scenario 2/2b/3 are done, before running Scenario 4).

**Cleanup:** after Scenario 2/2b/3 are complete, restore `main.ini` from
`main.ini.bak_260722_uat` (full file copy-back, not just the two fields) before starting
Scenario 4 (D-07 backward-compat), so Scenario 4 tests the *actual unmodified* production recipe,
not a UAT-modified one.

---

## Current Test

[awaiting human testing — Task 2 checkpoint]

---

## Tests

### 1. z_index 실행 스코프 (D-01/D-01a)
expected: `BOTTOM`(or `TOP`) 시퀀스에 z=1, z=2 각각 `DebugManualZTrigger` 전송 → 각 z에서 매핑된
Shot만 실행됨(§3의 관측 방법으로 확인), 다른 z의 Shot이 재실행되지 않음. z=0 전송 → 전체 Shot 실행
+ Datum 검출 정상(빈 B 응답, TCP 관찰 시).
result: [pending]

### 1b. StartSubset 무관 Shot 재-grab 게이트 (WARNING 1) — 명시적 PASS/FAIL 필수
expected: §4에서 설정한 `SHOT_E5`(ZIndexA=1/ZIndexB=2, own ZIndex=0)처럼 own-ZIndex 그룹과 떨어진
스파스 위치의 크로스-Z owning Shot이 있는 상태에서 z=1, z=2 트리거 → `StartSubset`의 min-max 구간에
낀 **무관 Shot(그 z_index를 own도 크로스-Z도 아닌 Shot)이 재-grab되면 FAIL**. §3 관측 방법으로 구간
내 전체 Shot 목록 확인.
PASS 기준: 무관 Shot 재-grab 0건.
result: [pending]

### 2. 크로스-Z 측정 (D-02/D-02a)
expected: §4 Config A 상태에서 z=1 트리거(이미지만 캡처, `E5_P2` 항목 미보고) → z=2 트리거(거리 계산
완료, 항목 보고) → 측정 mm 값이 두 이미지(z=1 캡처본, z=2 캡처본) 기준으로 올바른지 확인.
PASS 기준: 비완성 index(z=1) 응답에 `E5_P2` 없음 + 완성 index(z=2) 응답에 올바른 mm.
result: [pending]

### 2b. shot.ZIndex 독립성 (BLOCKER 코드레벨 fix 검증 / WARNING 3) — 명시적 PASS/FAIL 필수
expected: §4 Config B(`SHOT_E5.ZIndex=1`, 캡처 index와 동일)로 시나리오 2를 반복 → `E5_P2` 항목이
여전히 완성 index(z=2) 응답에만 담기고 z=1 응답엔 없음. Config A(§4, ZIndex=0=제3값)에서도 동일 결과
확인(이미 시나리오 2에서 검증됨 — 여기선 Config B만 추가 확인).
PASS 기준: 완성 index 응답에만 항목이 담김이 shot.ZIndex 값(0 이든 1이든)과 무관하게 유지.
result: [pending]

### 3. 크로스-Z 리셋 (D-03)
expected: 첫 부품 z=1(SHOT_E5 캡처)까지만 진행 후 중단 → 다음 부품 z=0 전송 → 크로스-Z 저장소가
깨끗이 리셋됐는지(이전 부품 이미지로 오염된 측정값이 다음 z=1→z=2 사이클에 안 나오는지) 확인.
result: [pending]

### 4. 하위호환 (D-07) — §4 cleanup 이후, 원본 main.ini 상태에서 수행
expected: `D:\Data\Recipe\FAI_1\main.ini` (UAT 이전 원본 상태로 복원 후) 로드 → `SHOT_E5` 검사 →
기존과 동일 측정값/동작(static `TeachingImagePath_Vertical`/`_Horizontal` 파일 경로 그대로 사용,
`ZIndexA`/`ZIndexB` = -1/-1 sentinel) 확인.
PASS 기준: 회귀 0.
result: [pending]

### 5. D-08 라이브 재사용
expected: 라이브 grab 경로(또는 SIMUL 반복 사이클) DualImage 측정 실행 → "파일에서 재로드" 관련
로그가 더 이상 안 뜨는지 확인 (`TryGrabOrLoadFaiDualImages`가 `ShotParam.HasImage`를 우선 확인).
result: [pending]

### 6. 오설정 NG (D-05) + Datum 크로스-Z (D-06)
expected: 임의 측정에 `ZIndexA==ZIndexB`(예: 둘 다 1) 설정 → `ZINDEX_MISCONFIGURED` NG 확인.
Side/Bottom Datum(Phase 37 기준, `ZIndexA`/`ZIndexB` 미설정) 회귀 0 확인.
result: [pending]

---

## Summary

total: 8
passed: 0
issues: 0
pending: 8
skipped: 0
blocked: 0

## Gaps

- **Grab-count observability**: no dedicated per-Shot grab-success trace log exists in the
  codebase today (see §3). UAT must rely on InspectionListView grid deltas or result-image
  timestamps as a proxy. Consider a follow-up phase item to add a lightweight
  `Logging.PrintLog(ELogType.Trace, "[Grab] {ShotName} z={nZIndex}")` at `EStep.Grab` entry if
  this proxy proves insufficient during Task 2 execution.
- **`Test/mock_vision_client.py` does not support the current `$PREP`+`$TEST` z_index protocol**
  (see §2) — it predates the `260626` `$PREP`/`$TEST` split and only ever sends a bare legacy
  `$TEST`. Not fixed here (out of Task 1's no-source-change scope); `DebugManualZTrigger` is used
  instead for this UAT. If TCP-level (not in-process) verification is later required, the mock
  script would need a `$PREP:site,z_index,1@` send prior to `$TEST`.
- **Real-hardware (non-SIMUL) deployment risk: `OfflineInspectMode` vs. cross-Z live dual-capture
  are architecturally incompatible.** This facility's real hardware is a manual Z jig with no motor
  (`IAxisController` is a placeholder; see memory `manual-jig-offline-inspect`,
  `.planning/quick/260715-moi-offline-inspect-mode/260715-moi-SUMMARY.md`). That prior session found
  that shots at different physical Z don't reconcile via shared datum + live grab within one cycle,
  and built `OfflineInspectMode` as the production workaround: one pre-captured static image per
  Shot/Datum node (`<recipe>\<node>.png`, single path, overwritten on re-grab). Phase 68's cross-Z
  design requires the *same* Shot to be live-grabbed *twice* (once per z_index role, A then B) —
  which (a) requires `OfflineInspectMode=false` (Pitfall 4 confirms it blocks `$TEST` outright), i.e.
  falls back to the same live-grab-coordination pattern that motivated building OfflineInspectMode
  in the first place, and (b) has no way to represent "two images for one node" under
  OfflineInspectMode's one-image-per-node model even if it were enabled. Phase 68's RESEARCH.md
  Pitfall 4 only resolves the narrower "OfflineInspectMode blocks `$TEST`" symptom for SIMUL UAT —
  it does not address whether live dual-Z capture is viable at all on this manual-jig hardware for
  real (non-SIMUL) production use. Needs a design decision before real-hardware rollout of this
  feature.
- **`FindActionIndicesByZIndex` (execution-scope filter, `InspectionSequence.cs:370-406`) has no
  awareness of `DatumConfig.ZIndexA/ZIndexB`** (Plan 04's Datum-level cross-Z fields) — it only
  matches `ShotConfig.ZIndex` and Shot-owned `DualImageEdgeDistanceMeasurement.ZIndexA/ZIndexB`
  (`DoesShotOwnCrossZIndex`, lines 329-361). If a z_index is used *exclusively* by a cross-Z Datum
  (e.g. a Side setup where z=0/z=1 are both Datum capture positions and no regular measurement Shot
  owns z=1), `FindActionIndicesByZIndex(1)` returns zero matches. `StartV1Scoped`
  (`Custom/SystemHandler.cs:250-253`) then falls back to `StartAll` and logs
  `"[V1Scope] ZIndex=1 매칭 Shot 0건 — StartAll 폴백. 레시피 ZIndex 설정 확인 필요."` every single cycle.
  Functionally the Datum still gets captured (StartAll runs everyone, including the DatumPhase loop
  that checks `nCurZ == datum.ZIndexA/B`), but this is an accidental side effect of the misconfiguration
  safety-net fallback, not a designed path — it produces a spurious error log every cycle and defeats
  D-01's waste-elimination goal entirely for that z_index (re-grabs every Shot instead of just the
  Datum-relevant capture). Needs explicit handling if any sequence's Datum uses a z_index that no
  regular Shot/measurement also owns.
  **CONFIRMED not hypothetical — Side's actual planned recipe uses z=0/z=1 exclusively for Datum
  (2-position Datum capture) with real measurement Shots starting at z=2, per user confirmation
  2026-07-22.** This WILL fire every cycle on Side once deployed as planned.

  **Deeper, more serious consequence found (2026-07-22): breaks the protocol's "Datum failure ⇒
  immediate F" contract, not just wasted re-grab.** `m_bCycleDatumFailed` — the cycle-level flag
  that `ApplyCycleJudgement` (`InspectionSequence.cs:972-989`) OR's into the final P/F verdict — is
  computed **exactly once**, at Index 0 processing time:
  ```csharp
  m_bCycleDatumFailed = DetectDatumFailure();   // AddResponseV1Cycle, Index==0 branch only
  ```
  and never re-evaluated at any later index. `DetectDatumFailure()` just checks whether any
  `DatumConfig` is currently in `_failedDatums` (`IsDatumFailed`, populated only by
  `MarkDatumFailed`, which only fires when detection actually *runs and fails*). For a 2-position
  cross-Z Datum whose real detection is deferred until its completion index (z=1 for Side, since
  `ZIndexA=0, ZIndexB=1`), **at z=0 the datum has only captured role A and is still "pending" — no
  detection attempt has happened yet, so it cannot possibly be in `_failedDatums`.**
  `m_bCycleDatumFailed` is therefore *always* `false` for this Datum, regardless of what happens at
  z=1. If detection then genuinely fails at z=1 (the actual completion tick), `MarkDatumFailed` DOES
  fire and `_failedDatums` DOES get populated — but the cycle-level immediate-F flag was already
  locked in as `false` at z=0 and is never revisited. The protocol's judgment table explicitly
  specifies Datum failure should cause "즉시" (immediate) F — this cannot be honored here; the
  failure surfaces (if at all) only via later measurements' `IsDatumFailed(datumRef)` per-FAI gate
  classifying as `DATUM_FAIL`/'N' and setting `m_bCycleHasNG=true`, which only feeds into the final
  verdict at the *last* index — not immediately at the Datum's actual completion index. Net effect:
  a genuinely failed Side Datum will likely still end up reported as 'F' eventually (assuming later
  indices have measurements referencing this `DatumRef`), but only at the very end of the cycle —
  defeating the fail-fast purpose of "즉시 F" (PLC keeps running now-pointless further indices), and
  in the theoretical edge case where zero later-index measurements reference this DatumRef, the
  failure could go unreported in the cycle verdict entirely. This is a genuine protocol-contract gap
  for any Datum whose cross-Z completion index isn't 0, not merely a wasted-execution inefficiency.
- **Measurement-level cross-Z lighting is architecturally locked to the Shot's single static
  `ZIndex` — it cannot vary between the ZIndexA capture and the ZIndexB capture.** Confirmed via
  `Action_FAIMeasurement.cs:214-221`, executed at the end of every `EStep.DatumPhase` (i.e.
  immediately before every `EStep.Grab`, for every Shot, every tick):
  ```csharp
  // Datum grab 동안 켜져 있던 datum 전용 조명을 이 Shot 본연의 조명으로 되돌린다.
  if (ShotParam != null) {
      parentSeq.ApplyShotLights(ShotParam.ZIndex);   // <- Shot's own fixed ZIndex, NOT the
                                                       //    currently-executing z_index/role
      LightHandler.Handle.WaitForPendingWrites();
  }
  ```
  This runs unconditionally right before the measurement grab, and keys lighting purely off
  `ShotParam.ZIndex` (the Shot's own static field) — **never** off which cross-Z role (A vs B) is
  being captured this tick, and never off `GetExecutionZIndex()`. Consequence: **a single cross-Z
  measurement's two captures (ZIndexA / "세로축" and ZIndexB / "가로축") always use the exact same,
  single, fixed lighting configuration** (whatever matches the Shot's own `ZIndex`) — there is
  currently no mechanism anywhere in the codebase to apply *different* lighting for the two
  capture roles of one cross-Z measurement. (Datum-level cross-Z is unaffected by this — Datum
  lighting is applied via the separate, direct `ApplyDatumLights(datum)` call at
  `Action_FAIMeasurement.cs:100`, called per-Datum-object regardless of z_index matching, so Datum
  capture lighting is correct today.) If a real cross-Z measurement needs different lighting per
  capture position (e.g. two different light types per the protocol's "조명 멀티샷" concept), this
  requires new design work (e.g. per-role light fields on the measurement, applied at the cross-Z
  capture tick) — out of Phase 68's current implementation.
  (Earlier draft of this note also questioned whether `$PREP`'s `ApplyShotLights(nZIndex)` →
  `FindShotByZIndex(nZIndex)` could fail to find a cross-Z shot outright when its own `ZIndex`
  differs from `ZIndexA`/`ZIndexB` — true for the `$PREP`-time call, but moot in practice: this
  same `ApplyShotLights(ShotParam.ZIndex)` call after `DatumPhase` always resolves via the Shot's
  own `ZIndex`, guaranteed to match at minimum itself, and unconditionally overwrites whatever
  `$PREP` set before the grab — so the net risk is "always the wrong/single lighting for one of the
  two roles," not "sometimes no lighting at all.")
- **`z_index=0` executes far more than "Datum only," contradicting the documented design intent.**
  Phase 49's own original decision (`49-CONTEXT.md` D-06) states plainly: **"Index 0은 datum 검출만
  수행"** ("Index 0 performs Datum detection only"). At the wire/response level this holds — z=0's
  `$RESULT` is always an empty `B;0;` buffer response, so the PLC/handler never sees evidence
  otherwise. But at the *execution* level, z=0 maps to `StartAll` (`StartV1Scoped`,
  `Custom/SystemHandler.cs:231-254`), which runs *every* Shot's full Grab+Measure — including Shots
  whose own `ZIndex` is 1, 2, 3... (they get grabbed and measured again, redundantly, at z=0, and then
  once more when their own index arrives). This mismatch between documented intent ("Datum only") and
  actual behavior ("everyone, every cycle") predates Phase 68 — it originates in Phase 49's original
  `StartAll`-at-z=0 implementation — and Phase 68's own CONTEXT.md (D-01a) explicitly re-confirmed
  and kept this behavior rather than fixing it ("waste-elimination은 z>=1에만 적용되고 z=0에는 적용
  안 됨을 인지하고 넘어감"). Correctness (P/F/B judgment) is unaffected since responses are still
  correctly index-scoped; the cost is purely wasted re-grab/re-measure cycles + lighting wear on
  every ZIndex>=1 Shot, twice per part. Flagged here as a known, carried-forward, unfixed tradeoff —
  not a Phase 68 regression, but worth a dedicated follow-up if grab/lighting overhead becomes a real
  concern on production hardware.
