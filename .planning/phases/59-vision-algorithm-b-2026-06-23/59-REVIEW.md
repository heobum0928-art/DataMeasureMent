---
phase: 59-vision-algorithm-b-2026-06-23
reviewed: 2026-06-24T00:00:00Z
depth: standard
files_reviewed: 5
files_reviewed_list:
  - WPF_Example/Custom/EthernetVision/AlignResult.cs
  - WPF_Example/Custom/EthernetVision/AlignRefPose.cs
  - WPF_Example/Custom/EthernetVision/AlignShapeMatchService.cs
  - WPF_Example/Custom/EthernetVision/EthernetVisionHandler.cs
  - WPF_Example/DatumMeasurement.csproj
findings:
  critical: 0
  warning: 2
  info: 2
  total: 4
status: clean
---

# Phase 59: Code Review Report

**Reviewed:** 2026-06-24
**Depth:** standard
**Files Reviewed:** 5
**Status:** issues_found

## Summary

Phase 59 introduces `AlignShapeMatchService`, `AlignResult`, and `AlignRefPose` to provide Shape-model teach/run for the Ethernet align camera. The architecture is well-structured: failure isolation is solid (both `TryTeach` and `Run` are fully wrapped, every exit path returns false / `Found=false`), resource lifetime is correctly delegated to `PatternMatchService` (which disposes `HObject`/`HTuple` in its own `finally` blocks), and the caller-owned `HImage` is correctly never disposed by this service. The px→mm conversion and theta subtraction are correct (both ref and cur angles are already in degrees from `PatternMatchService`, no double rad/deg conversion occurs).

Two MEDIUM issues and two NIT issues are recorded below.

---

## Warnings

### WR-01: `HasTemplate` Creates Directory as Side-Effect of a Pure Query

**File:** `WPF_Example/Custom/EthernetVision/AlignShapeMatchService.cs:118-124`

**Issue:** `HasTemplate(mode)` is documented as a pure existence check ("둘 다 존재해야 사용 가능"), but it delegates to `GetShmPath(mode)` which unconditionally calls `Directory.CreateDirectory(folder)` at line 61-63. Every time a caller queries `HasTemplate` (e.g., from UI polling or pre-flight checks), it silently creates the `ETHERNET_ALIGN` folder on disk even when no template has ever been taught. This is an unexpected filesystem write from a method named `Has*`, and will litter the recipe directory with empty folders if the mode is changed or the recipe is not yet taught.

**Fix:** Split `GetShmPath` into a path-building helper that does NOT create the directory, and a separate `EnsureDirectory` helper called only from write paths (`TryTeach` / `TrySaveRefPose`). `HasTemplate` should use the no-create variant.

```csharp
// Path-only helper (no IO side-effect)
private string BuildShmPath(EEthernetVisionMode mode) {
    string recipePath = SystemSetting.Handle.RecipeSavePath;
    string recipeName = SystemSetting.Handle.CurrentRecipeName;
    if (string.IsNullOrEmpty(recipeName)) {
        recipeName = DEFAULT_RECIPE_NAME;
    }
    string folder = Path.Combine(recipePath, recipeName, ETHERNET_ALIGN_FOLDER);
    string modeFileName = (mode == EEthernetVisionMode.Bottom) ? "Bottom" : "Tray";
    return Path.Combine(folder, modeFileName + PatternMatchService.EXTENSION_SHAPE_MODEL);
}

// Write helper (creates directory)
private string GetShmPath(EEthernetVisionMode mode) {
    string path = BuildShmPath(mode);
    Directory.CreateDirectory(Path.GetDirectoryName(path));
    return path;
}

// HasTemplate uses read-only variant
public bool HasTemplate(EEthernetVisionMode mode) {
    if (mode == EEthernetVisionMode.None) { return false; }
    string shmPath  = BuildShmPath(mode);
    string jsonPath = Path.ChangeExtension(shmPath, REF_POSE_EXT);
    return File.Exists(shmPath) && File.Exists(jsonPath);
}
```

`TryTeach` and `Run` continue to call `GetShmPath` (write-path), which creates the directory as before.

---

### WR-02: `TryFindPose` in `Run` Passes `roiRow=0, roiCol=0` — Search Rectangle Is Implicitly Full-Image via Clamp

**File:** `WPF_Example/Custom/EthernetVision/AlignShapeMatchService.cs:230-235`

**Issue:** `Run` calls `_matcher.TryFindPose` with `roiRow=0.0, roiCol=0.0, roiLen1=FULL_SEARCH_LEN(99999), roiLen2=FULL_SEARCH_LEN(99999), marginPx=0.0`. Inside `TryFindPose`, the search rectangle is computed as:

```
sr1 = 0.0 - 99999  → clamped to 0
sc1 = 0.0 - 99999  → clamped to 0
sr2 = 0.0 + 99999  → clamped to imgH-1
sc2 = 0.0 + 99999  → clamped to imgW-1
```

This produces a full-image rectangle which is the intended behavior. However, the full-search intent is encoded as a magic number (99999) passed as `roiLen1/roiLen2` using `(0,0)` as a phantom center, which is inconsistent with the `roiRow/roiCol` semantics (they are documented as ROI center, not search-window corners). If `TryFindPose` internals ever change to use `roiRow/roiCol` directly (e.g., to compute the downsample origin), the (0,0) center would produce incorrect scaled coordinates for images whose content is not at the top-left.

Currently safe because `TryFindPose` uses `GenRectangle1` (axis-aligned, based on the corner coordinates post-clamp) and downsample is 1.0 so no scale-back occurs. The correctness concern would only manifest if `downsampleFactor > 1.0` is used in a future call AND the match result is near the actual (0,0) corner — the `curRow / scale` math would still be correct since the coordinate origin is preserved. The risk is documentation-level confusion and future breakage.

**Fix:** Pass the actual image center as `roiRow/roiCol` and a large `marginPx` instead of abusing the ROI-center parameter, OR add an overload for full-image search. At minimum, add a comment clarifying the intent:

```csharp
// 전체 이미지 검색: center=(0,0)+len=99999 → TryFindPose 내부 GenRectangle1 clamp으로 (0,0)~(imgH-1,imgW-1) 커버.
// downsampleFactor=1.0 고정이므로 좌표 스케일 복원 없음 — (0,0) center 가 스케일 오차 유발하지 않음.
bool bFound = _matcher.TryFindPose(
    img, ENGINE, shmPath,
    0.0, 0.0,               // phantom center; 실제 검색범위는 FULL_SEARCH_LEN clamp 으로 결정
    FULL_SEARCH_LEN, FULL_SEARCH_LEN,
    0.0, MIN_SCORE, 1.0,    // marginPx=0, downsample=1 (스케일 복원 불필요)
    out curRow, out curCol, out curAngleDeg, out curScore, out findErr);
```

---

## Info

### IN-01: `TrySaveRefPose` Serializes Without Explicit `TypeNameHandling.None`

**File:** `WPF_Example/Custom/EthernetVision/AlignShapeMatchService.cs:89`

**Issue:** The deserializer (line 107) correctly specifies `TypeNameHandling.None` as RCE mitigation. The serializer at line 89 calls `JsonConvert.SerializeObject(refPose, Formatting.Indented)` without explicit `JsonSerializerSettings`. The default for `TypeNameHandling` in Newtonsoft.Json is already `None`, so no `$type` field is injected and there is no functional difference. However, the asymmetry is inconsistent with the stated security intent comment on line 99 — a reader seeing the serialize call without the matching settings may not trust the round-trip guarantee.

**Fix:** Pass a consistent settings object to the serializer:

```csharp
JsonSerializerSettings saveSettings = new JsonSerializerSettings();
saveSettings.TypeNameHandling = TypeNameHandling.None;
saveSettings.Formatting = Formatting.Indented;
string json = JsonConvert.SerializeObject(refPose, saveSettings);
```

---

### IN-02: `RecipeSavePath` Not Null-Guarded in `GetShmPath`

**File:** `WPF_Example/Custom/EthernetVision/AlignShapeMatchService.cs:54`

**Issue:** `SystemSetting.Handle.RecipeSavePath` has a C# initializer default of `@"D:\Data\Recipe"` (non-null), but if the user blanks it in the Settings UI and saves, it could be empty or null. `Path.Combine(null, ...)` throws `ArgumentNullException`. This exception IS caught by the outer `try/catch` in both `TryTeach` (line 198) and `Run` (line 277), so the service degrades gracefully (returns false / `Found=false`) rather than propagating. Not a crash risk, but the catch absorbs the wrong exception silently.

The existing project pattern (`TeachingStorageService.cs:90-92`) uses a fallback `IsNullOrWhiteSpace` guard on `CurrentRecipeName` but leaves `RecipeSavePath` unchecked there too — so this is consistent with project convention. Log the specific path-null case if added.

**Fix (optional, for consistency with `CurrentRecipeName` guard):**
```csharp
string recipePath = SystemSetting.Handle.RecipeSavePath;
if (string.IsNullOrEmpty(recipePath)) {
    recipePath = @"D:\Data\Recipe";  // 또는 error 반환 처리
}
```

---

## Correctness Checklist (All Pass)

| Concern | Verdict |
|---|---|
| TryTeach/Run never throw — all paths return false/Found=false | PASS |
| null img guard in TryTeach (line 145) and Run (line 209) | PASS |
| mode==None guard in TryTeach (line 149) and Run (line 209) | PASS |
| LoadRefPose null-guard before offset calc (line 220) | PASS |
| HImage NOT disposed by service (caller-owned) | PASS |
| HObject/HTuple disposal — fully delegated to PatternMatchService | PASS |
| offset = cur − ref (not reversed) at lines 252-253 | PASS |
| px→mm: EthernetPixelResolution(μm) / 1000 = mm/px at line 254 | PASS |
| ThetaDeg = curAngleDeg − refAngleDeg (degrees, no re-conversion) | PASS |
| ThetaDeg=0/HasTheta=false for Tray mode (lines 268-269) | PASS |
| JSON deserialization uses TypeNameHandling.None (line 107) | PASS |
| EthernetVisionHandler.Initialize() exception-isolated (line 55) | PASS |
| Matcher null-guard in exception path (lines 58-60) | PASS |

---

_Reviewed: 2026-06-24_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
