---
phase: 58-config-camera-a-2026-06-23
reviewed: 2026-06-23T00:00:00Z
depth: standard
files_reviewed: 6
files_reviewed_list:
  - WPF_Example/Custom/EthernetVision/EEthernetVisionMode.cs
  - WPF_Example/Custom/EthernetVision/EthernetAlignCamera.cs
  - WPF_Example/Custom/EthernetVision/EthernetVisionHandler.cs
  - WPF_Example/Custom/SystemSetting.cs
  - WPF_Example/SystemHandler.cs
  - WPF_Example/DatumMeasurement.csproj
findings:
  critical: 0
  warning: 3
  info: 2
  total: 5
status: clean
---

# Phase 58: Code Review Report

**Reviewed:** 2026-06-23
**Depth:** standard
**Files Reviewed:** 6
**Status:** issues_found

## Summary

Phase 58 adds the `EEthernetVisionMode` enum, `EthernetAlignCamera` composition wrapper over `HikCamera`, the `EthernetVisionHandler` sealed singleton, and `[ETHERNET_VISION]` INI properties in `SystemSetting`. The phase constraint — "failure must never propagate to the Grabber" — is correctly achieved at every call site: all public methods are fully try-caught, and `SystemHandler` adds a redundant outer guard as belt-and-suspenders.

**Core correctness is sound.** The failure-isolation goal is met. HImage lifetime in `LoadFallbackImage()` is correct (intermediate `loaded` is disposed before returning `gray`). Null-deref on `_hikCamera` before `Connect()` succeeds is guarded in every method. INI round-trip for the int-backed `EthernetVisionModeValue ↔ EthernetVisionMode` pattern follows the established `CameraRoleValue` precedent correctly.

Three **warnings** need attention before Phase 59 calls `Grab()` in production paths: a static-dictionary collision risk, a leaked HImage branch in `LoadFallbackImage`, and a dead INI property for exposure.

---

## Warnings

### WR-01: `EnumerateDevice(ip)` clears the shared static `DeviceList` — potential collision with Grabber camera enumeration

**File:** `WPF_Example/Custom/EthernetVision/EthernetAlignCamera.cs:52`

**Issue:** `HikCamera.EnumerateDevice()` operates on a `private static Dictionary<string, CCameraInfo> DeviceList` (HikCamera.cs:30). Every call to `EnumerateDevice` begins with `DeviceList.Clear()` (HikCamera.cs:162). `Connect()` calls `HikCamera.EnumerateDevice(ip)` after constructing the `_hikCamera` instance. If `DeviceHandler` has already enumerated and stored HIK cameras in that same static dictionary, `Connect()` will wipe those entries. Any subsequent call to `HikCamera.Open(ip)` by the Grabber path would find `DeviceList` empty or containing only the align camera, causing `GetDeviceIndex()` to return `-1` and `Open()` to fail silently.

In practice this is unlikely to fire under normal startup ordering (Grabber opens first, then `EthernetVisionHandler.Initialize()` runs at the end of `SystemHandler.Initialize()`), but the race window exists when `EthernetVisionHandler.Initialize()` is called while the Grabber sequences are alive and could re-enumerate (e.g. reconnect after cable loss).

**Fix:** Call `EnumerateDevice` before constructing the shared `HikCamera` instance, or filter: enumerate all GigE devices once, then select the right one by IP without clearing the dictionary. The safest fix for Phase 58 scope is to enumerate first, check the count, then call `Open`:

```csharp
// In Connect(), swap the order:
int deviceCount = HikCamera.EnumerateDevice(ip);   // enumerate FIRST
if (deviceCount == 0) {
    Logging.PrintLog((int)ELogType.Camera, "[ETHERNET] Connect: no device found for {0}", ip);
    return false;
}
_hikCamera = new HikCamera(config, info);          // construct AFTER count confirmed
bool bOpened = _hikCamera.Open(ip);
return bOpened;
```

This still clears `DeviceList`, but at least it re-populates it with the full enumeration result before the Grabber could rely on stale entries. A more robust long-term fix is to give the align camera its own enumeration scope (separate static list), but that requires modifying `HikCamera.cs` which is out of Phase 58 scope per D-02.

---

### WR-02: `LoadFallbackImage()` leaks `loaded` HImage when `ReadImage` succeeds but `CountChannels()` throws

**File:** `WPF_Example/Custom/EthernetVision/EthernetAlignCamera.cs:143-155`

**Issue:** The happy-path logic:

```csharp
HImage loaded = new HImage();
loaded.ReadImage(ALIGN_FALLBACK_IMAGE_PATH);
if (loaded.CountChannels().I > 1) {
    HImage gray = loaded.Rgb1ToGray();
    loaded.Dispose();
    return gray;
}
return loaded;
```

If `CountChannels()` throws a HALCON exception (e.g. malformed BMP, partially written file), control jumps to the `catch` block, which returns `null` — but `loaded` is neither disposed nor assigned to a local that is in scope for cleanup. `loaded` leaks as an orphaned HALCON object.

`ReadImage` succeeding is not a guarantee of a valid image (HALCON may accept the file header but produce a degenerate image where `CountChannels` fails). HALCON image objects hold native memory; leaked `HImage` instances accumulate until GC finalizer runs, which is non-deterministic for native resources.

**Fix:** Wrap with `try/finally` to ensure disposal:

```csharp
private HImage LoadFallbackImage()
{
    try {
        if (!File.Exists(ALIGN_FALLBACK_IMAGE_PATH)) {
            Logging.PrintLog((int)ELogType.Camera, "[ETHERNET] fallback image missing: {0}", ALIGN_FALLBACK_IMAGE_PATH);
            return null;
        }
        HImage loaded = new HImage();
        loaded.ReadImage(ALIGN_FALLBACK_IMAGE_PATH);
        if (loaded.CountChannels().I > 1) {
            HImage gray = loaded.Rgb1ToGray();
            loaded.Dispose();   // dispose before returning gray
            return gray;
        }
        return loaded;
    }
    catch (Exception ex) {
        Logging.PrintLog((int)ELogType.Camera, "[ETHERNET] LoadFallbackImage failed: {0}", ex.Message);
        return null;
    }
}
```

Minimum fix: add a `loaded?.Dispose()` call in the `catch` block, or declare `loaded` outside the try and add a `finally { if (returnValue == null) loaded?.Dispose(); }`. The cleanest pattern matching project convention (`Action_TopInspection.cs` `try/finally`) is:

```csharp
HImage loaded = null;
try {
    if (!File.Exists(ALIGN_FALLBACK_IMAGE_PATH)) { ... return null; }
    loaded = new HImage();
    loaded.ReadImage(ALIGN_FALLBACK_IMAGE_PATH);
    if (loaded.CountChannels().I > 1) {
        HImage gray = loaded.Rgb1ToGray();
        loaded.Dispose();
        loaded = null;
        return gray;
    }
    HImage result = loaded;
    loaded = null;
    return result;
}
catch (Exception ex) {
    Logging.PrintLog((int)ELogType.Camera, "[ETHERNET] LoadFallbackImage failed: {0}", ex.Message);
    return null;
}
finally {
    loaded?.Dispose();  // no-op if already set null on success paths
}
```

---

### WR-03: `EthernetExposure` is serialized to INI but never applied to the camera — silent dead property

**File:** `WPF_Example/Custom/SystemSetting.cs:79`

**Issue:** `EthernetExposure` is declared with `[Category("ETHERNET_VISION")]`, so it serializes to `Setting.ini` and appears in the PropertyGrid UI. Users can change this value. However, `EthernetAlignCamera.Connect()` never reads it and never calls any exposure setter on `_hikCamera`. The `HikCamera.Open()` path sets `ExposureAutoMode` to OFF but does not set a specific exposure time value.

Result: the INI entry creates a user expectation that changing the exposure value has an effect, but it has none. This is a correctness gap — the align camera will run at whatever default exposure the firmware powers up with, regardless of the configured value.

**Fix (Phase 59 scope is acceptable):** Either apply the exposure immediately after `Open()` in `Connect()`:

```csharp
// After bOpened = _hikCamera.Open(ip) returns true:
if (bOpened) {
    double exposureUs = SystemSetting.Handle.EthernetExposure;
    _hikCamera.CameraHandle.SetFloatValue("ExposureTime", (float)exposureUs);
}
```

Or, if intentionally deferred to Phase 61, mark the property with a `[Description("Phase 61: 카메라 Open 후 SetFloatValue(ExposureTime) 로 적용 예정")]` annotation and add a `// TODO(Phase61)` comment at the declaration site so the property is not an invisible no-op.

---

## Info

### IN-01: `EthernetAlignCamera.Connect()` constructs `_hikCamera` before confirming a device exists — wasteful on repeated failures

**File:** `WPF_Example/Custom/EthernetVision/EthernetAlignCamera.cs:50-56`

**Issue:** `new HikCamera(config, info)` is called (allocating the wrapper) before `EnumerateDevice` confirms any device is reachable. On a machine with no align camera (Mode=Tray but camera offline), `Connect()` is called once at startup, allocates the `HikCamera` instance, then returns `false`. The instance is not disposed — `_hikCamera` is left non-null but with `IsOpen == false`. This is benign since `Grab()` falls through to the fallback, but it is a mild resource concern and is also corrected by the WR-01 fix (swap order: enumerate first, construct second).

**Fix:** Already addressed by the WR-01 reorder suggestion. No separate fix needed.

---

### IN-02: Double try-catch on `EthernetVisionHandler.Initialize()` is redundant

**File:** `WPF_Example/SystemHandler.cs:188-193` and `WPF_Example/Custom/EthernetVision/EthernetVisionHandler.cs:28-52`

**Issue:** `EthernetVisionHandler.Initialize()` already wraps its entire body in `try/catch (Exception ex)` and never rethrows. The outer `try/catch` added in `SystemHandler.cs` (lines 188-193) will therefore never catch anything — `Initialize()` cannot throw. The outer guard is harmless (belt-and-suspenders was explicitly the design intent per D-04), but it logs a misleading `[ETHERNET] EthernetVisionHandler.Initialize failed` message that can never actually appear, which could confuse future debugging if the log entry is searched.

**Fix:** Either remove the outer `try/catch` in `SystemHandler.cs` and rely on the inner one, or add a comment clarifying the double guard is intentional:

```csharp
// Belt-and-suspenders: EthernetVisionHandler.Initialize() is already fully try-caught internally.
// This outer guard is a defensive layer only — it should never fire.
try {
    EthernetVisionHandler.Handle.Initialize();
}
catch (Exception ex) {
    Logging.PrintLog((int)ELogType.Error, "[ETHERNET] EthernetVisionHandler.Initialize failed: {0}", ex.Message);
}
```

---

_Reviewed: 2026-06-23_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
