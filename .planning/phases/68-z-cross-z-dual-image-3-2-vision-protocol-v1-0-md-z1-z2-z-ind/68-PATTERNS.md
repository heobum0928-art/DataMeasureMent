# Phase 68: Z축 교차(Cross-Z) Dual-Image 측정 지원 - Pattern Map

**Mapped:** 2026-07-22
**Files analyzed:** 7 modified (no new files, confirmed by RESEARCH.md "Recommended Project Structure")
**Analogs found:** 7 / 7 (every target file has an in-file or sibling-file analog already in production — this phase is "extend existing pattern", not "invent new pattern")

## File Classification

| File to Modify | Role | Data Flow | Closest Analog (same file unless noted) | Match Quality |
|---|---|---|---|---|
| `WPF_Example/Custom/SystemHandler.cs` (`ProcessTest`) | controller (TCP command dispatcher) | request-response | `ProcessPrep`/`ApplyPrepToSequences` in same file (z_index→behavior branch, `_lastPrepZIndex` already threaded through) | exact |
| `WPF_Example/Custom/Sequence/SequenceHandler.cs` (`RebuildInspectionActions`) | factory / builder | batch (Shot list → Action[] transform) | same method, existing `OwnerSequenceName` filter loop — just add a sort key | exact |
| `WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs` | sequence orchestrator / state store | event-driven + CRUD (cycle-scoped cache) | `_datumTransforms`/`_failedDatums` (member cache, same file) for state; `ShotConfig._image`/`SetImage`/`GetImage` (sibling file) for the **image**-holding lock/Dispose pattern | exact (state lifecycle) / role-match (image lock, cross-file) |
| `WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs` | action/step executor (state machine `Run()`) | event-driven + file-I/O (image grab/load) | same file: `TryGrabOrLoadDualDatumImages` (structurally near-identical twin of `TryGrabOrLoadFaiDualImages`), `MarkMeasurementDatumRefMissing` (explicit-NG pattern), `EStep.DatumPhase` lock block (grab-lock-order precedent) | exact |
| `WPF_Example/Custom/Sequence/Inspection/Measurements/DualImageEdgeDistanceMeasurement.cs` | model + algorithm (pure `ParamBase`-derived measurement) | transform (image → scalar distance) | `MeasurementBase.Load` override (sibling file) for the sentinel-int pattern; own existing `TeachingImagePath_Horizontal/_Vertical` properties for placement/category convention | exact |
| `WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs` | model (ParamBase + `ICustomTypeDescriptor`) | CRUD (recipe-persisted config) | own `TeachingImagePath_Vertical` (placement), own `EnsurePerRoiDefaults` (sentinel normalization — **not** a `Load` override, see Pitfall below), own `IsHiddenForAlgorithm` switch (PropertyGrid hide rule) | exact |
| `WPF_Example/Custom/Sequence/Inspection/SkipReason.cs` | constants | N/A | `DATUM_REF_MISSING` (same file, most recent addition, identical purpose class) | exact |

Reference-only files (read for pattern extraction, **not modified** by this phase — RESEARCH.md lists them "참고용"):
- `WPF_Example/Sequence/Sequence/SequenceBase.cs` — `StartSubset`/`StartCore`/`Actions` field (consumed, not changed)
- `WPF_Example/Sequence/Param/ParamBase.cs` — `Load` reflection loop (base class behavior being worked around)
- `WPF_Example/Custom/Sequence/Inspection/ShotConfig.cs` — `_image`/`SetImage`/`GetImage`/`HasImage` (lock+clone+Dispose analog for cross-Z image storage)
- `WPF_Example/Custom/Sequence/Inspection/MeasurementBase.cs` — `Load` sentinel-restore override (the exact pattern to mirror for `ZIndexA`/`ZIndexB`)

---

## Pattern Assignments

### `WPF_Example/Custom/SystemHandler.cs` — `ProcessTest` (controller, request-response)

**Analog:** same file, `ProcessPrep`/`ApplyPrepToSequences` (lines 707-740, 783+) and `DebugManualZTrigger` (749-779).

**Current code to modify** (lines 200-220):
```csharp
private bool ProcessTest(TestPacket packet) {
    if (!IsRecipeReady) { ... return false; }
    if (Setting.OfflineInspectMode) { ... return false; }
    packet.TestID = _lastPrepZIndex.ToString(); //260626 hbk $PREP z_index 주입
    if (Sequences.IsDynamicFAIMode) {
        string seqName = packet.Identifier;
        SequenceBase seq = Sequences[seqName];
        if (seq == null) return false;
        return seq.StartAll(packet);   // ← D-01 gap: always full-run, z_index ignored
    }
    return Sequences.Start(packet);
}
```

**Pattern to copy — guard-clause chain style** (from `ProcessPrep`, lines 707-740): every branch is `bool bXxx = ...; if (!bXxx) { log + return; }`, no ternary, no null-coalescing (matches D-09 coding guideline). `_lastPrepZIndex` is already the single source of truth for "what z_index is this $TEST for" — reuse it, do not reparse `packet.TestID` a second time in `ProcessTest` (it hasn't been assigned yet at this point in the method).

**Integration point (per CONTEXT.md Claude's Discretion):** this is the natural place to branch `nZIndex == 0` (→ `StartAll`, per D-01a) vs `nZIndex >= 1` (→ `StartSubset` with indices from a new `InspectionSequence.FindActionIndicesByZIndex`-style helper, per D-01/D-01b). `seq` here is `SequenceBase`-typed but the actual runtime instance is always `InspectionSequence` in the dynamic-FAI branch — will need a cast (`seq as InspectionSequence`) to reach the new helper, following the existing cast style used elsewhere in this file (e.g. `masterParam as CameraMasterParam` pattern in `SequenceHandler.RebuildInspectionActions`).

**Do not touch:** the method signature/logic outside the `StartAll` line — comment at line 748 explicitly says "ProcessPrep/ProcessTest 는 프로덕션 TCP 경로 — 시그니처/로직 변경 금지" for `DebugManualZTrigger`'s *caller* contract; the body of `ProcessTest` itself is exactly what D-01 asks to change, so this applies to not breaking the v2.6/legacy branches (`Sequences.Start(packet)` and the `!IsDynamicFAIMode` path) which must stay byte-identical.

---

### `WPF_Example/Custom/Sequence/SequenceHandler.cs` — `RebuildInspectionActions` (factory, batch)

**Analog:** same method, same file (lines 99-130) — no external analog needed, this is a targeted edit to an existing loop.

**Current code** (lines 112-125):
```csharp
var actions = new List<ActionBase>();
int actionIdx = 0;
for (int i = 0; i < RecipeManager.ShotCount; i++) {
    ShotConfig shot = RecipeManager.Shots[i];
    string shotOwner = string.IsNullOrEmpty(shot.OwnerSequenceName) ? SEQ_TOP : shot.OwnerSequenceName;
    if (shotOwner != targetSeqName) continue;
    EAction actionId = (EAction)((int)EAction.FAI_Base + actionIdx);
    string actionName = shot.ShotName ?? $"SHOT_{actionIdx}";
    var action = new Action_FAIMeasurement(actionId, actionName, shot);
    actions.Add(action);
    actionIdx++;
}
if (actions.Count > 0) {
    seq.AddAction(actions.ToArray());
}
```

**What D-01b requires:** filter to this sequence's shots first (existing `shotOwner != targetSeqName` continue), **then sort the filtered subset by `ShotConfig.ZIndex`** before assigning `EAction` ids / building `Action_FAIMeasurement[]` — so that `Actions[]` (consumed by `SequenceBase.StartSubset`'s min-max range) has same-ZIndex shots contiguous. Note this file already uses ternary (`?? SEQ_TOP`) — this file is **not** listed under D-09's coding-guideline scope (only `InspectionSequence.cs`/`Action_FAIMeasurement.cs` z_index-scope code is), so the existing style (ternary, ` ?? `) may be kept consistent with the surrounding file rather than forced into if/else.

**Verification required before/at first task (per CONTEXT.md discretion item):** confirm `EAction` values assigned here (`EAction.FAI_Base + actionIdx`) are never persisted/referenced by fixed index elsewhere — RESEARCH.md Assumption A1 flags this as unverified; a `msbuild` build pass plus a grep for `EAction.FAI_Base` consumers is the cheapest verification.

---

### `WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs` — cross-Z storage + z_index→ActionIndices helper (sequence orchestrator)

**Analog 1 (state lifecycle):** `_datumTransforms`/`_failedDatums` member declarations (lines 55-58) + their reset site `ClearDatumTransforms()` (lines 844-856) + cycle-boundary reset `ResetCycleState()` (lines 486-492).

```csharp
// Source: WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs:54-58
// 런타임 transform 캐시
private readonly Dictionary<string, HTuple> _datumTransforms = new Dictionary<string, HTuple>();
// datum 검출 실패 datum 이름 집합 (per-FAI gate 신호). _datumTransforms 와 동일 lifecycle.
private readonly HashSet<string> _failedDatums = new HashSet<string>();
```

```csharp
// Source: WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs:484-492
//260623 hbk Phase 49 PROTO-05 (D-08): Index 0(Datum 샷) 수신 = 사이클 시작 → 누적 상태 클린 슬레이트.
//  비정상 종료(중단 F) 후에도 다음 사이클 시작이 항상 깨끗 — 마지막 Index 후 리셋(누락 위험) 미채택.
private void ResetCycleState()
{
    m_bCycleHasNG = false;
    m_bCycleDatumFailed = false;
    m_nCurrentZIndex = 0;
    m_nLastZIndex = 0;     // 호출 후 반드시 m_nLastZIndex = ComputeLastZIndex(recipeManager) 재산출 필요 — 호출부 의무
}
```
`ResetCycleState()` is called from `HandleDatumIndexResponse` (around line 595) which fires when `m_nCurrentZIndex == DATUM_Z_INDEX` (0) — this is the exact hook D-03 says to "편승" (piggyback) on. The new cross-Z dictionary's `.Clear()` (with per-entry `HImage.Dispose()` first, per D-02a discretion note) belongs right next to `_datumTransforms.Clear(); _failedDatums.Clear();` inside `ClearDatumTransforms()` **or**, if the cross-Z store must survive across the whole cycle rather than being per-datum-phase, inside `ResetCycleState()` directly — planner must pick based on whether `ClearDatumTransforms()` is called more than once per cycle (it is called from `EStep.DatumPhase`, i.e. potentially every Action/Shot in the cycle, **not** once — so `ResetCycleState()` is the safer reset site for cross-Z data that must span multiple Shots/Actions within one cycle).

**Analog 2 (image ownership: lock + clone + Dispose) — use this for D-02a "store images, not values":**
```csharp
// Source: WPF_Example/Custom/Sequence/Inspection/ShotConfig.cs:236-374
private readonly object _imageLock = new object();
private HImage _image;

public bool HasImage {
    get { lock (_imageLock) { return _image != null; } }
}

public void SetImage(HImage image) {
    lock (_imageLock) {
        if (_image != null) _image.Dispose();
        if (image != null) _image = image.CopyImage();
        else _image = null;
    }
}

public HImage GetImage() {
    lock (_imageLock) {
        if (_image != null) return _image.CopyImage();
        return null;
    }
}
```
This is the exact "own a copy, dispose the old one on replace, return clones to callers" contract the new cross-Z `Dictionary<string, HImage>` (or similar) must follow — callers of `GetImage()`-equivalent own the returned clone and must Dispose it (mirrors `TryGrabOrLoadFaiDualImages`'s Dispose-in-`finally` contract in `Action_FAIMeasurement.cs`).

**Analog 3 (z_index lookup helper, single-match version to extend for multi-match):**
```csharp
// Source: WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs:300-327
private ShotConfig FindShotByZIndex(int nZIndex)
{
    var recipeManager = SystemHandler.Handle.Sequences.RecipeManager;
    if (recipeManager == null) return null;
    foreach (var shot in recipeManager.Shots) {
        if (shot == null) continue;
        bool bOwnedByThisSeq = shot.OwnerSequenceName == Name;
        bool bZMatch = shot.ZIndex == nZIndex;
        bool bInScope = bOwnedByThisSeq && bZMatch;
        if (bInScope) return shot;
    }
    return null;
}
```
D-01's new `FindActionIndicesByZIndex` (RESEARCH.md Code Example 1, no existing analog — genuinely new helper, but modeled 1:1 on this loop's guard-clause style and `OwnerSequenceName`/`ZIndex` double-condition) belongs directly below this method. It must iterate `Actions` (protected field on `SequenceBase`, confirmed accessible — `InspectionSequence` already reads `Actions` elsewhere, e.g. `HandleRunStartResetResults`) rather than `recipeManager.Shots`, since it needs `Actions[]` indices for `StartSubset`, not `ShotConfig` objects.

**Analog 4 (int parse guard, for ZIndexA/B runtime validation, D-05):**
```csharp
// Source: WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs:494-518
private int ParseCurrentZIndex()
{
    int nZ = 0;
    if (RequestPacket == null) return nZ;
    string szTestId = RequestPacket.TestID;
    if (string.IsNullOrEmpty(szTestId)) return nZ;
    int nParsed = 0;
    bool bValid = int.TryParse(szTestId, out nParsed);
    bool bNonNegative = nParsed >= 0;
    if (bValid && bNonNegative) nZ = nParsed;
    return nZ;
}
```
Same defensive shape (TryParse + non-negative check + safe default) should gate `ZIndexA`/`ZIndexB` at measurement-execution time.

**Anti-pattern to avoid (explicitly called out by D-05):**
```csharp
// Source: WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs:907-923 (ResolveDatumModelPath)
ShotConfig matched = null;
if (!string.IsNullOrEmpty(datum.SourceShotName)) {
    foreach (var s in shots) { if (s != null && s.ShotName == datum.SourceShotName) { matched = s; break; } }
}
if (matched == null) matched = shots[0];   // ← silent fallback to Shots[0] — DO NOT copy this for ZIndexA/B
```
D-05 requires ZIndexA/ZIndexB mismatches (equal values, or pointing at a non-existent z_index) to become an **explicit NG with logged reason** (see `MarkMeasurementDatumRefMissing` pattern below), never a silent fallback like this.

---

### `WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs` — live-capture wiring + D-08 fix (action/step executor)

**Analog 1 — D-08 fix, exact bug location** (lines 456-463, inside `TryGrabOrLoadFaiDualImages`, full method 444-483):
```csharp
// Source: WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs:456-463
string pathA;
if (!string.IsNullOrEmpty(dualMeas.TeachingImagePath_Horizontal) && File.Exists(dualMeas.TeachingImagePath_Horizontal)) {
    pathA = dualMeas.TeachingImagePath_Horizontal; // Measurement 명시 경로
}
else {
    pathA = ShotParam.SimulImagePath; // fallback — BUG: never checks ShotParam.HasImage first
}
```
Confirmed live: `ShotParam.HasImage`/`ShotParam.GetImage()` (already populated by the merged `EStep.DatumPhase` grab block at line 207: `if (ShotParam != null && !ShotParam.HasImage) { ... ShotParam.SetImage(image); ... }`) is never consulted here. Fix priority per RESEARCH.md: `ShotParam.HasImage` (live, highest priority) → `TeachingImagePath_Horizontal` (explicit path) → `ShotParam.SimulImagePath` (fallback) — same 3-tier if/else-if/else shape as the existing code, just reordered with a new first branch.

**Analog 2 — structurally near-identical twin for the Datum-side live capture (`TryGrabOrLoadDualDatumImages`, lines 408-438)** — same file already solves "load two images with explicit path-missing errors + paired-Dispose-on-partial-failure":
```csharp
// Source: WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs:408-438
private bool TryGrabOrLoadDualDatumImages(DatumConfig datum, out HImage imageHorizontal, out HImage imageVertical) {
    imageHorizontal = null;
    imageVertical = null;
    if (datum == null) { Logging.PrintErrLog(...); return false; }
    string pathH = datum.TeachingImagePath;
    string pathV = datum.TeachingImagePath_Vertical;
    if (string.IsNullOrEmpty(pathH) || !File.Exists(pathH)) { Logging.PrintErrLog(...); return false; }
    if (string.IsNullOrEmpty(pathV) || !File.Exists(pathV)) { Logging.PrintErrLog(...); return false; }
    try { imageHorizontal = new HImage(pathH); } catch (Exception ex) { ...; imageHorizontal = null; }
    try { imageVertical = new HImage(pathV); } catch (Exception ex) { ...; imageVertical = null; }
    if (imageHorizontal == null || imageVertical == null) {
        if (imageHorizontal != null) { try { imageHorizontal.Dispose(); } catch { } }
        if (imageVertical != null) { try { imageVertical.Dispose(); } catch { } }
        imageHorizontal = null; imageVertical = null;
        return false;
    }
    return true;
}
```
Both `TryGrabOrLoadFaiDualImages` and `TryGrabOrLoadDualDatumImages` need a **third priority tier inserted**: when `ZIndexA`/`ZIndexB` (measurement) or new Datum equivalent are set (≠ -1 sentinel), the image should come from the cross-Z storage (`InspectionSequence` cache, Pattern above) for whichever z_index isn't "this Shot's own index", and from `ShotParam.GetImage()`/live-grab for whichever z_index **is** this Shot's own index. This decision point is the actual new logic for this phase — everything else in this file is unmodified precedent.

**Analog 3 — explicit-NG pattern (D-05, exact precedent to mirror for `ZINDEX_MISCONFIGURED`):**
```csharp
// Source: WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs:606-615
private void MarkMeasurementDatumRefMissing(MeasurementBase meas) {
    meas.ClearResult();
    meas.LastSkipReason = SkipReason.DATUM_REF_MISSING;
    meas.LastJudgement = false;
    string measName = meas.MeasurementName;
    if (measName == null) measName = meas.TypeName;
    string datumRef = meas.DatumRef;
    if (datumRef == null) datumRef = "";
    Logging.PrintLog((int)ELogType.Error, "[FAIMeasurement] Measurement '" + measName + "' skipped — DatumRef '" + datumRef + "' 에 해당하는 Datum 이 레시피에 없음 (오타/개명/삭제 확인 필요, " + meas.LastSkipReason + ")");
}
```
Gate call site precedent (lines 271-287, inside `EStep.Measure`'s `foreach (var meas in fai.Measurements)` loop) — a new `if (parentSeq2 != null && IsZIndexMisconfigured(meas))` check belongs right next to the existing `IsDatumFailed`/`IsDatumRefUnresolvable` gates, same `continue`-after-mark shape:
```csharp
// Source: WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs:278-287 (existing gate shape to mirror)
if (parentSeq2 != null && parentSeq2.IsDatumRefUnresolvable(meas.DatumRef))
{
    MarkMeasurementDatumRefMissing(meas);
    faiAllPass = false;
    measuredCount++;
    continue;
}
```

**Analog 4 — lock-order precedent (mandatory for any new grab call, per canonical_refs `shared-lighthandler-race`):**
```csharp
// Source: WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs:94, 229
lock (LightHandler.Handle.GrabSyncLock) {
    // ... ApplyDatumLights / ApplyShotLights / grab happens here ...
} // lock 종료 — grab 완료 후 Step 전환은 락 밖에서 수행
```
Any new cross-Z live-capture grab (for `ZIndexA`/`ZIndexB` when they don't match this Shot's own `ZIndex`) must happen inside this same `lock (LightHandler.Handle.GrabSyncLock)` block in `EStep.DatumPhase` — `LightHandler.Handle.GrabSyncLock` always outer, `cam.GrabLock` (inside `DeviceHandler.GrabHalconImage`) always inner. Do not add a second, separate lock scope.

**Anti-pattern (Dispose contract to preserve):** `TryExecuteMeasurement` (lines 668-702) already disposes `imgA`/`imgB` in `finally` and resets `RuntimeImageA`/`RuntimeImageB` to null — any change to `TryGrabOrLoadFaiDualImages`'s return contract (e.g. returning a cross-Z-cached image directly instead of a fresh load) must still hand back an image `TryExecuteMeasurement`'s `finally` can safely Dispose (i.e. always return an owned clone, never a reference into the cross-Z cache itself — reuse `ShotConfig.GetImage()`'s "always clone out" convention).

---

### `WPF_Example/Custom/Sequence/Inspection/Measurements/DualImageEdgeDistanceMeasurement.cs` — ZIndexA/ZIndexB fields (model)

**Analog 1 — placement/category convention** (existing properties, lines 22-37):
```csharp
// Source: WPF_Example/Custom/Sequence/Inspection/Measurements/DualImageEdgeDistanceMeasurement.cs:24-37
[Category("Image|DualImage")]
[System.ComponentModel.Description("LineROI 검출용 별도 이미지 경로 — Bottom E5 패턴")]
[DisplayName("세로축 티칭 이미지")]
[InputFilePath(DeviceHandler.EXTENSION_IMAGE, DeviceHandler.FILTER_IMAGE)]
[AutoUpdateText]
public string TeachingImagePath_Vertical { get; set; } = "";

[Category("Image|DualImage")]
[System.ComponentModel.Description("PointROI 검출용 가로축 이미지 경로 — 명시 시 우선 사용, 빈 문자열/파일 부재 시 ShotConfig.SimulImagePath 로 fallback")]
[DisplayName("가로축 티칭 이미지")]
[InputFilePath(DeviceHandler.EXTENSION_IMAGE, DeviceHandler.FILTER_IMAGE)]
[AutoUpdateText]
public string TeachingImagePath_Horizontal { get; set; } = "";
```
New `ZIndexA`/`ZIndexB` int properties belong in the same `"Image|DualImage"` category, immediately below these two — RESEARCH.md's Code Example 2 (lines 252-261) is ready to paste as-is.

**Analog 2 — sentinel-int `Load` override (exact precedent, sibling file `MeasurementBase.cs:143-155`, this class already inherits `MeasurementBase`):**
```csharp
// Source: WPF_Example/Custom/Sequence/Inspection/MeasurementBase.cs:143-155
// 하위호환: ParamBase.Load 는 INI 누락 double 키를 0 으로 덮어쓴다. 구 레시피엔 MeasCorrectionFactor 키가 없어
//  0 으로 로드되면 EvaluateJudgement 에서 value×0=0 → 전 측정 0/NG(회귀). 키 부재 시에만 1.0(무보정) 복원한다.
public override bool Load(IniFile loadFile, string groupName) {
    bool result = base.Load(loadFile, groupName);
    IniSection sec;
    if (!loadFile.TryGetSection(groupName, out sec) || sec == null || !sec.ContainsKey("MeasCorrectionFactor")) {
        MeasCorrectionFactor = 1.0;
    }
    return result;
}
```
`DualImageEdgeDistanceMeasurement` does **not currently override `Load`** (confirmed — only `TryExecute` is overridden). Add a new `override bool Load` here (calling `base.Load()` which resolves to `MeasurementBase.Load()`, which itself calls `ParamBase.Load()` then restores `MeasCorrectionFactor` — so the new override must call `base.Load()` first, exactly like `MeasurementBase.Load` does, then add two more `ContainsKey` guards for `ZIndexA`/`ZIndexB` restoring `-1` each). Do not duplicate the `MeasCorrectionFactor` restoration logic — it's inherited via `base.Load()`.

**Root cause confirmed (why this is needed):** `ParamBase.Load` (`WPF_Example/Sequence/Param/ParamBase.cs:377-380`) unconditionally does `int iValue = loadFile[group][name].ToInt(); prop.SetValue(this, iValue);` for every `Int32` property — a missing INI key resolves to `0` via `IniFile`'s indexer, not the C# property initializer's `-1`. This silently defeats any `= -1` default on `ZIndexA`/`ZIndexB` unless the `Load` override restores it.

---

### `WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs` — ZIndexA/ZIndexB fields (model, D-06)

**Analog 1 — placement convention** (line 38, `TeachingImagePath`; line 129, `TeachingImagePath_Vertical`):
```csharp
// Source: WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs:36-38
[Category("Datum|ImageSource")]
public string TeachingImagePath { get; set; } = "";
```
New `ZIndexA`/`ZIndexB` belong in `"Datum|ImageSource"` category, same block as `TeachingImagePath`/`TeachingImagePath_Vertical`.

**Analog 2 — CRITICAL DIFFERENCE FROM `DualImageEdgeDistanceMeasurement`: `DatumConfig` has NO `Load` override.** Confirmed by direct read (no `override bool Load` found in the file). Its existing sentinel-normalization mechanism is `EnsurePerRoiDefaults()` (lines 824-869), called from **execution entry points**, not from `Load()`:
```csharp
// Source: WPF_Example/Halcon/Algorithms/DatumFindingService.cs:52, 86, 830, 860
config.EnsurePerRoiDefaults();
// Source: WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs:978
datum.EnsurePerRoiDefaults();
// Source: WPF_Example/UI/ContentItem/MainView.xaml.cs:3789
datum.EnsurePerRoiDefaults();
```
```csharp
// Source: WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs:866-868 (sentinel-restore style inside EnsurePerRoiDefaults)
if (string.IsNullOrEmpty(Circle_RadialDirection)) Circle_RadialDirection = "Inward"; // sentinel "" → "Inward" fallback (INI 하위호환)
if (TeachingImagePath == null) TeachingImagePath = "";
if (TeachingImagePath_Vertical == null) TeachingImagePath_Vertical = "";
```
This resolves RESEARCH.md Open Question #6 definitively: **`EnsurePerRoiDefaults()` is the hook**, not a `Load` override — but note it fires at teach/find call sites, not at INI-load time, so between "recipe load" and "first teach/find call" `ZIndexA`/`ZIndexB` would read as raw `0` (not yet normalized to `-1`). Planner must decide (Claude's Discretion) whether that gap matters — if `ZIndexA==0` before normalization could be misread as "z_index 0 explicitly configured" instead of "unset", either (a) add a genuine `override bool Load` to `DatumConfig` for this phase (new precedent, mirrors the `MeasurementBase` pattern instead of the `EnsurePerRoiDefaults` pattern), or (b) add the two `ZIndexA`/`ZIndexB` sentinel checks into `EnsurePerRoiDefaults()` and ensure it's called early enough (e.g. from `Action_FAIMeasurement.EStep.DatumPhase` before ZIndexA/B are first read, which is already the pattern for other per-ROI sentinels consumed at find-time).

**Analog 3 — PropertyGrid hide rule (`ICustomTypeDescriptor`, D-06 "동일하게 적용" — new fields visible only where relevant):**
```csharp
// Source: WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs:982-1014 (IsHiddenForAlgorithm)
case EDatumAlgorithm.VerticalTwoHorizontalDualImage: // DualImage 변형: VTH 와 동일 hide 그룹. 차이 = TeachingImagePath_Vertical 추가 노출
    if (name.StartsWith("Line1_") || name.StartsWith("Line1Detected_")) return true;
    if (name.StartsWith("Line2_") || name.StartsWith("Line2Detected_")) return true;
    if (name.StartsWith("Circle_") || name.StartsWith("CircleROI_") || name.StartsWith("CircleCenter_") || name.StartsWith("CircleDetected_")) return true;
    return false;   // ← ZIndexA/ZIndexB fall through here already, no new hide rule strictly required
```
Since `TeachingImagePath_Vertical` is explicitly hidden for `TwoLineIntersect`/`CircleTwoHorizontal`/`VerticalTwoHorizontal` (lines 986, 993, 1001) but shown for `VerticalTwoHorizontalDualImage`, and `ZIndexA`/`ZIndexB` are semantically tied to the same DualImage-only feature, add matching `if (name == "ZIndexA" || name == "ZIndexB") return true;` lines to the three non-DualImage `case` blocks (986, 993, 1001) so the new fields hide/show exactly in sync with `TeachingImagePath_Vertical`.

---

### `WPF_Example/Custom/Sequence/Inspection/SkipReason.cs` — new constant (D-05)

**Analog:** the file itself — `DATUM_REF_MISSING` is the most recent addition and the exact template:
```csharp
// Source: WPF_Example/Custom/Sequence/Inspection/SkipReason.cs (full file, 13 lines)
//260710 hbk skip-사유 단일 소스 상수. 값은 와이어/CSV/로그에 그대로 나가므로 절대 변경 금지.
namespace ReringProject.Sequence
{
    public static class SkipReason
    {
        public const string DATUM_FAIL = "DATUM_FAIL";
        public const string ALIGN_FAIL = "ALIGN_FAIL";
        public const string NO_IMAGE = "NO_IMAGE";
        public const string DATUM_REF_MISSING = "DATUM_REF_MISSING";
        // ADD: public const string ZINDEX_MISCONFIGURED = "ZINDEX_MISCONFIGURED";
    }
}
```
**Warning carried over from file header comment:** these string values are emitted directly onto the wire/CSV/log — once added, `ZINDEX_MISCONFIGURED` (or whatever name planner picks, per CONTEXT.md discretion) must never be renamed after ship.

---

## Shared Patterns

### 1. Sentinel-int hidden default via `Load` override (D-04/D-07 backward compatibility)
**Source:** `WPF_Example/Custom/Sequence/Inspection/MeasurementBase.cs:143-155` (`MeasCorrectionFactor`)
**Root cause it works around:** `WPF_Example/Sequence/Param/ParamBase.cs:377-380` (`Int32` case unconditionally writes `IniFile` indexer default, which is `0`, not the C# property's own initializer)
**Apply to:** `DualImageEdgeDistanceMeasurement.cs` (via new `Load` override, straightforward — class already inherits `MeasurementBase`). For `DatumConfig.cs`, see the file-specific note above — the mechanism differs (`EnsurePerRoiDefaults`, not `Load`).

### 2. Explicit-NG + `SkipReason`, never silent fallback (D-05)
**Source:** `WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs:606-615` (`MarkMeasurementDatumRefMissing`) + gate call site 278-287
**Anti-pattern to avoid:** `WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs:907-923` (`ResolveDatumModelPath`'s silent `matched = shots[0]` fallback)
**Apply to:** `Action_FAIMeasurement.cs` — new ZIndexA==ZIndexB / non-existent-z_index gate in the `EStep.Measure` loop, same file, same shape as the `IsDatumRefUnresolvable` gate immediately above it.

### 3. Member-cache lifecycle tied to `ResetCycleState()` (D-02/D-03)
**Source:** `WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs:55-58` (declaration), `:486-492` (`ResetCycleState`, the z_index=0 reset hook)
**Apply to:** `InspectionSequence.cs` — new cross-Z `Dictionary<string, HImage>` (or similar) member + its `.Clear()`-with-Dispose call added to `ResetCycleState()`.

### 4. Owned-clone image buffer: lock + `CopyImage()` in, `CopyImage()` out, Dispose-on-replace (D-02a)
**Source:** `WPF_Example/Custom/Sequence/Inspection/ShotConfig.cs:236-374` (`_image`/`SetImage`/`GetImage`/`HasImage`)
**Apply to:** `InspectionSequence.cs` cross-Z storage (store `HImage` copies, not references; dispose replaced/reset entries) and `Action_FAIMeasurement.cs` (whatever reads from the cross-Z store must treat the returned image the same way it already treats `TryGrabOrLoadFaiDualImages`'s output — owned, dispose in `finally`).

### 5. `GrabSyncLock` always outer, `cam.GrabLock` always inner (lock-order regression from 2026-07-21, canonical_refs)
**Source:** `WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs:94-229` (the merged `EStep.DatumPhase` block)
**Apply to:** any new grab call added for cross-Z live capture — must stay inside this existing lock scope, no new lock object.

### 6. Guard-clause / no-ternary control flow (D-09, LOCKED coding guideline — scope-limited)
**Source:** pervasive in `InspectionSequence.cs` and `Action_FAIMeasurement.cs` (e.g. `bool bHasShot = shot != null; if (!bHasShot) { ...; return false; }`)
**Apply to:** ONLY new/modified code in `InspectionSequence.cs` and `Action_FAIMeasurement.cs` (per D-09 explicit scope). `DualImageEdgeDistanceMeasurement.cs`/`DatumConfig.cs` keep their existing camelCase/plain style (pure data/measurement classes, D-09 exempts them). `SequenceHandler.cs`/`SystemHandler.cs` are control/protocol code but D-09's text names only `InspectionSequence.cs`/`Action_FAIMeasurement.cs` explicitly — planner should confirm with user whether `ProcessTest`/`RebuildInspectionActions` edits also need guideline compliance (they already mostly follow it in this codebase regardless, e.g. `ProcessPrep`'s `bIsOn`/`bApplied` style).

---

## No Analog Found

None — every file in RESEARCH.md's "Recommended Project Structure" list has at least a role-match analog already in the same file or an immediate sibling file. The two genuinely novel pieces of code in this phase (`FindActionIndicesByZIndex`-style multi-match helper in `InspectionSequence.cs`, and the cross-Z image `Dictionary` itself) are both modeled directly on existing single-match/single-image analogs cited above (`FindShotByZIndex`, `ShotConfig._image`) rather than invented from scratch.

## Metadata

**Analog search scope:** `WPF_Example/Custom/Sequence/Inspection/**`, `WPF_Example/Custom/SystemHandler.cs`, `WPF_Example/Custom/Sequence/SequenceHandler.cs`, `WPF_Example/Sequence/Sequence/SequenceBase.cs`, `WPF_Example/Sequence/Param/ParamBase.cs`
**Files scanned/read in full or targeted ranges:** `SystemHandler.cs` (Custom), `SequenceHandler.cs` (Custom), `InspectionSequence.cs`, `Action_FAIMeasurement.cs`, `DualImageEdgeDistanceMeasurement.cs`, `MeasurementBase.cs`, `DatumConfig.cs`, `SkipReason.cs`, `ShotConfig.cs`, `SequenceBase.cs`, `ParamBase.cs`
**Pattern extraction date:** 2026-07-22
