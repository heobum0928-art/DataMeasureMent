# Coding Conventions

**Analysis Date:** 2026-04-02

## Naming Patterns

**Files:**
- Action classes: `Action_<SequenceName><Role>.cs` (e.g., `Action_TopInspection.cs`, `Action_FAIMeasurement.cs`)
- Sequence classes: `Sequence_<Name>.cs` (e.g., `Sequence_Top.cs`, `Sequence_Bottom.cs`)
- Date-versioned backup files follow the pattern `Action_BottomInspection_0428.cs` — these are dead weight and NOT compiled into the project. Ignore them; use only the un-dated filename.
- Model/data classes: PascalCase noun (e.g., `RoiDefinition.cs`, `TeachingJob.cs`, `ShotConfig.cs`)
- Services: `<Domain>Service.cs` for new code (e.g., `RawImageSaveService.cs`, `TeachingStorageService.cs`)
- ViewModels: `<Feature>ViewModel.cs` (e.g., `InspectionListViewModel.cs`, `ModelFinderViewModel.cs`)

**Classes:**
- PascalCase always
- Action classes: prefix `Action_` (legacy pattern), or `<Name>Action` in newer code (e.g., `TopInspectionAction`)
- Context classes that accompany actions: `<ActionName>Context` extending `ActionContext`
- Parameter classes for actions: `<ActionName>Param` extending `CameraSlaveParam` or `ParamBase`
- Enum types: prefix `E` + PascalCase (e.g., `ESequence`, `EAction`, `EContextState`, `ELogType`)
- Interface types: prefix `I` + PascalCase (e.g., `IHalconTeachingProvider`, `ICameraParam`, `IDrawableItem`)

**Properties and Fields:**
- Public properties: PascalCase (e.g., `IsInitialized`, `CurrentActionIndex`, `ShotName`)
- Private fields: camelCase, sometimes prefixed with `_` in newer code (e.g., `_image`, `_isStopping`, `_queue`)
- Legacy private fields: prefixed with `p` for pointer-like references to typed instances (e.g., `pMyContext`, `pCamera`, `pSystemHandle`)
- Protected fields in base classes: PascalCase with no prefix (e.g., `Actions`, `CurAction`, `Interlock`)
- Constants: UPPER_SNAKE_CASE (e.g., `MSG_STX`, `MSG_ETX`, `DEFAULT_LOG_EXT`)
- Boolean flags: prefix `Is`, `Has`, or `b` (legacy) (e.g., `IsOpen`, `HasImage`, `bCreated`)

**Methods:**
- PascalCase for all public and protected methods (e.g., `OnCreate`, `FinishAction`, `TryInspectSingleEdge`)
- `Try` prefix for methods using the `out` parameter result pattern returning `bool` (e.g., `TryInspectSingleEdge`, `TryRun`, `TryFitLine`)
- Lifecycle callbacks: `On<Event>` naming (e.g., `OnCreate`, `OnBegin`, `OnEnd`, `OnLoad`, `OnPaused`, `OnResume`)
- Event handlers: `<Subject>Button_Click`, `<Event>Handler` convention

**Enums:**
- Defined in `Custom/Define/ID.cs` or local to the file where used
- Member names: PascalCase (e.g., `EAction.Top_Inspection`, `ESequence.Top`)
- Step enums inside action classes: private `enum EStep { Init, Grab, Measure, End }` pattern

## Code Style

**Formatting:**
- No `.editorconfig` or `.prettierrc` detected; formatting is inconsistent between modules
- Older code (Logging, SequenceBase, VirtualCamera): K&R brace style — opening brace on same line as declaration
- Newer Halcon code (MeasurementAlgorithm, RoiDefinition, TeachingStorageService): Allman brace style — opening brace on its own line
- Use the style of the file/module you are editing; do not mix within one file

**Language Version:**
- C# 7.2 (set in `.csproj`); avoid C# 8.0+ features (`nullable reference types`, `switch expressions`, `record` types)
- Target framework: .NET Framework 4.8

**Brace and Spacing:**
- Single blank line between methods
- No trailing blank lines inside methods
- One statement per line

## Import Organization

**Order (not enforced by tooling, follow this convention):**
1. System namespaces (`System`, `System.Collections.*`, `System.IO`, etc.)
2. Third-party (`HalconDotNet`, `OpenCvSharp`, `PropertyTools.*`)
3. Internal project namespaces (`ReringProject.*`)

**Namespace Hierarchy:**
- Root: `ReringProject`
- Sub-namespaces: `ReringProject.Sequence`, `ReringProject.Device`, `ReringProject.Halcon.Algorithms`, `ReringProject.Halcon.Models`, `ReringProject.Halcon.Services`, `ReringProject.Network`, `ReringProject.UI`, `ReringProject.Utility`, `ReringProject.Setting`, `ReringProject.Define`
- Custom (project-specific overrides): `Custom/` folder files share the same namespaces as their base counterparts; no separate namespace for custom code

## Error Handling

**Halcon algorithm methods:**
- Wrap all `HOperatorSet.*` calls in `try { } catch { return false; }` — bare `catch` suppresses exception detail
- Public entry points validate arguments before calling internal logic
- Example pattern from `MeasurementAlgorithm.cs`:
  ```csharp
  public bool TryInspectSingleEdge(HImage image, RoiDefinition roi, out EdgeInspectionOverlay overlay)
  {
      overlay = null;
      if (image == null || roi == null) return false;
      try
      {
          return TryInspectSingleEdgeInternal(image, roi, out overlay);
      }
      catch
      {
          return false;
      }
  }
  ```

**Sequence/Action errors:**
- Call `FinishAction(EContextResult.Error)` and return `Context` to signal a fatal step failure
- Never throw from `Run()` — return a context with error state
- Errors propagate via `SequenceBase.Error()`, which sets `EContextState.Error`, stops the thread command, and fires `OnError`

**File/IO errors:**
- Wrap in `try/catch (Exception ex)` and log with `Logging.PrintLog` or `Logging.PrintErrLog`
- Non-critical cleanup operations (temp file deletion, log rotation): bare `catch { }` silently swallowing is acceptable here per existing pattern in `TeachingStorageService.CleanupTempImages` and `Logging.DeleteLogByDay`

**Resource disposal:**
- `HImage` objects must be disposed; use `using` blocks for short-lived images or explicit `.Dispose()` in `try/finally`
- Pattern used in `Action_TopInspection.cs`:
  ```csharp
  try {
      // use image
  }
  finally {
      image?.Dispose();
  }
  ```
- Thread-safe image buffers: use `lock (_imageLock)` around all read/write of shared `HImage` fields (see `ShotConfig.cs`)

## Logging

**Framework:** Custom static `Logging` class at `WPF_Example/Utility/Logging.cs`

**Log Types:**
- Defined as `ELogType` enum (e.g., `ELogType.Trace`, `ELogType.Camera`, `ELogType.Error`, `ELogType.Image`)
- Each log type maps to a separate file, registered at startup via `Logging.SetLog(id, name, path)`

**Patterns:**
```csharp
// Standard message
Logging.PrintLog((int)ELogType.Trace, "Message text");
// Formatted
Logging.PrintLog((int)ELogType.Error, "Camera.Display() Fail: {0}", ex.Message);
// Win32 error with caller info
Logging.PrintErrLog((int)ELogType.Error, "operation description");
// CSV columns
Logging.PrintLogToCSV((int)ELogType.Result, col1, col2, col3);
```
- Always cast `ELogType` to `int` when calling `Logging.*`
- Log errors at the site where they are caught, not silently

## Comments

**Korean comments:** Prevalent in older code. New code should prefer English comments to keep consistency with the Halcon/new modules layer.

**When to Comment:**
- Business logic that maps hardware protocol to software concepts (TCP packet fields, zone/site mapping)
- Non-obvious algorithm parameters (why sigma=1.0, why trimCount is applied, etc.)
- Thread-safety intent (e.g., `// Thread-safe image buffer`)
- Stubs that must be replaced: `// Phase 8: Halcon edge measurement will be implemented here`

**XML Doc Comments (`///`):**
- Used on public utility methods in `Logging.cs` and `VirtualCamera.cs`
- Required on new public-facing service/utility methods
- Not required on UI event handlers or override lifecycle methods

**`#region` Blocks:**
- Used in `SequenceBase.cs` to group delegates and enums (`#region delegates`, `#region enums`)
- Use only for top-level logical groupings in large files; do not use inside methods

## Function Design

**Size:** Action `Run()` methods use a `switch ((EStep)Step)` state machine — keep each `case` to fewer than 20 lines. Extract helper methods when logic grows.

**Parameters:** Prefer `out` parameters for result objects in `Try*` methods rather than returning tuples or throwing.

**Return Values:**
- `bool` for success/failure (algorithms, device operations)
- The current `Context` object for `ActionBase.Run()` overrides
- `string` summaries for Halcon batch results (see `MeasurementAlgorithm.Run`)
- Null-safe: return `null` from load helpers when file not found; callers must null-check

## Module Design

**Singleton:** `SystemHandler.Handle` is the application-wide singleton (sealed class, private constructor, static `Handle` property). Do not create new instances.

**Partial Classes:** `SystemHandler` uses `partial` across `SystemHandler.cs` and `Custom/SystemHandler.cs`; `VisionServer` uses `partial` similarly. Extend via partial, not inheritance.

**Exports:** No barrel (`index.cs`) files. Classes are imported directly by namespace.

**Thread Safety:**
- Sequence state machine runs on a dedicated `Thread` per `SequenceBase`
- Shared resources protected by `lock (object)` — see `Logging.lockObject`, `ShotConfig._imageLock`, `VirtualCamera.Interlock`
- `ConcurrentQueue<T>` used for cross-thread messaging (e.g., `ResponseQueue` in `SequenceBase`, `RawImageSaveService._queue`)
- `volatile bool` for termination flags (e.g., `RawImageSaveService._isStopping`, `_isStarted`)

---

*Convention analysis: 2026-04-02*
