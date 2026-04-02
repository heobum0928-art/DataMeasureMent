# Architecture

**Analysis Date:** 2026-04-02

## Pattern Overview

**Overall:** Singleton Orchestrator + Command/State-Machine Sequence Pattern

**Key Characteristics:**
- A single top-level `SystemHandler` singleton (`WPF_Example/SystemHandler.cs`) owns and coordinates every subsystem: devices, sequences, TCP server, logging, recipe management, and UI state.
- Inspection logic is organized as named **Sequences** (camera threads), each owning an ordered list of **Actions** (step-level work units). Each sequence runs on its own `Thread` at `ThreadPriority.Highest`.
- A second partial class `WPF_Example/Custom/SystemHandler.cs` contains the `MainRun()` polling loop (runs at 1 ms interval) that dispatches incoming TCP packets to sequences and drains response queues.
- The codebase uses a "framework + custom" split: the `WPF_Example/Sequence/` and `WPF_Example/Device/` directories contain reusable base classes, while `WPF_Example/Custom/` contains project-specific overrides and registrations via C# `partial class`.
- UI follows MVVM with limited adoption: some views carry ViewModel classes, others use direct code-behind with `INotifyPropertyChanged`.

## Layers

**Application Bootstrap:**
- Purpose: Application entry, mutex guard, unhandled exception handling, language init.
- Location: `WPF_Example/App.xaml`, `WPF_Example/App.xaml.cs`
- Contains: `App` class; creates `MainWindow` on startup.
- Depends on: `SystemHandler`, `MainWindow`

**System Orchestrator:**
- Purpose: Singleton that owns all subsystems, drives the system loop, and routes TCP commands to sequences.
- Location: `WPF_Example/SystemHandler.cs` (constructor + Initialize/Release), `WPF_Example/Custom/SystemHandler.cs` (MainRun + ProcessXxx methods)
- Contains: `DeviceHandler`, `LightHandler`, `SequenceHandler`, `VisionServer`, `RawImageSaveService`, `LoginManager`, `RecipeFiles`, `SystemSetting`
- Depends on: all subsystems below
- Used by: UI layer for status queries, any class that calls `SystemHandler.Handle.*`

**Sequence Engine (Framework):**
- Purpose: Generic sequence-state machine infrastructure. Each sequence is a named thread; each action is a step executed within that thread.
- Location: `WPF_Example/Sequence/Sequence/SequenceBase.cs`, `WPF_Example/Sequence/Action/ActionBase.cs`, `WPF_Example/Sequence/SequenceBuilder.cs`, `WPF_Example/Sequence/SequenceHandler.cs`
- Contains: `SequenceBase`, `ActionBase`, `ActionContext`, `SequenceContext`, `ParamBase`, `SequenceBuilder`, `SequenceHandler`
- Depends on: Define enums (`ESequence`, `EAction`), `HalconDotNet`, `DeviceHandler`
- Used by: `SystemHandler`, UI for status display

**Custom Sequences (Project-Specific):**
- Purpose: Concrete sequence and action implementations for this machine (Top, Side, Bottom cameras, FAI measurement).
- Location: `WPF_Example/Custom/Sequence/`
  - `Top/Sequence_Top.cs`, `Top/Action_TopInspection.cs`
  - `Bottom/Sequence_Bottom.cs`, `Bottom/Action_BottomInspection.cs`
  - `Inspection/Action_FAIMeasurement.cs`, `Inspection/InspectionRecipeManager.cs`, `Inspection/ShotConfig.cs`, `Inspection/FAIConfig.cs`
  - `SequenceHandler.cs` (partial) — registers sequences/actions at startup
- Depends on: Framework sequence layer, `HalconDotNet`, `DeviceHandler`, `Halcon/` algorithms
- Used by: `SequenceHandler`, `SystemHandler`

**Device Layer (Framework):**
- Purpose: Abstract camera access. All cameras are exposed as `VirtualCamera` instances regardless of real hardware (Basler, HIK, or simulated).
- Location: `WPF_Example/Device/DeviceHandler.cs`, `WPF_Example/Device/Camera/VirtualCamera.cs`, `WPF_Example/Device/Camera/Basler/`, `WPF_Example/Device/Camera/Hik/`
- Contains: `DeviceHandler` (singleton), `VirtualCamera`, `BaslerCamera`, `HikCamera`, `DeviceInfo`
- Depends on: Camera SDKs (Basler Pylon, HIK MVS), `HalconDotNet` for `GrabHalconImage()`
- Used by: `SystemHandler`, `ActionBase` subclasses

**Custom Device Registration:**
- Purpose: Project-specific camera names, resolutions, and orientations.
- Location: `WPF_Example/Custom/Device/DeviceHandler.cs`
- Contains: Constants `CAMERA_TOP`, `CAMERA_SIDE`, `CAMERA_BOTTOM`; `RegisterRequiredDevices()` called in `DeviceHandler` constructor.

**Halcon Vision Layer:**
- Purpose: Wraps Halcon image processing algorithms and teaching data management.
- Location: `WPF_Example/Halcon/`
  - `Algorithms/MeasurementAlgorithm.cs` — edge measurement via `HOperatorSet.MeasurePos`
  - `Algorithms/RoiLineIntersectionAlgorithm.cs` — line intersection via edge detection
  - `Services/TeachingStorageService.cs`, `HalconTeachingHelper` (static) — teaching job JSON I/O
  - `Models/RoiDefinition.cs`, `TeachingJob.cs`, `EdgeInspectionOverlay.cs` — data models
  - `Display/HalconDisplayService.cs` — draw overlays on WPF canvas
- Depends on: `HalconDotNet`, `System.Windows`
- Used by: `Action_TopInspection`, future FAI measurement actions

**TCP Communication Layer (Framework):**
- Purpose: External handler/host communicates with this vision system via TCP. Handles test requests, recipe changes, light control, and site status.
- Location: `WPF_Example/TcpServer/VisionServer.cs`, `VisionRequestPacket.cs`, `VisionResponsePacket.cs`, `TcpServer.cs`
- Contains: `VisionServer`, packet classes (`TestPacket`, `LightPacket`, `RecipeChangePacket`, etc.)
- Depends on: nothing internal (self-contained network layer)
- Used by: `SystemHandler.MainRun()` for recv/send, `Custom/SystemHandler.cs` for dispatch

**TCP Resource Map (Custom):**
- Purpose: Maps incoming protocol fields (site, test type) to internal names (sequence name, action name, camera name, light group name).
- Location: `WPF_Example/Custom/TcpServer/ResourceMap.cs` (custom registration), `WPF_Example/TcpServer/ResourceMap.cs` (framework map logic)

**Setting Layer:**
- Purpose: INI-based system configuration, recipe file paths, log paths.
- Location: `WPF_Example/Setting/SystemSetting.cs`, `WPF_Example/Custom/SystemSetting.cs`
- Contains: `SystemSetting` (singleton), paths, flags like `SaveFailImage`, `AutoLogoutWhenRecvTest`

**UI Layer:**
- Purpose: WPF presentation. `MainWindow` hosts a menu bar, status bar, and content views. Main content is `MainView` which renders camera images and inspection results.
- Location: `WPF_Example/UI/`, `WPF_Example/Custom/UI/`
  - `UI/ContentItem/MainView.xaml` + `.cs` — primary image viewer, sequence selector
  - `UI/ControlItem/InspectionListView.xaml` + `InspectionListViewModel.cs` — inspection result list
  - `UI/Dialog/TeachingWindow.xaml` — ROI teaching dialog
  - `UI/Light/`, `UI/Log/`, `UI/Recipe/`, `UI/Setting/`, `UI/Login/`, `UI/TcpServer/` — secondary windows
- Depends on: `SequenceHandler`, `DeviceHandler`, `Halcon/` services
- Used by: User interaction only

**Utility Layer:**
- Purpose: Logging, INI file I/O, raw image save worker.
- Location: `WPF_Example/Utility/`
  - `RawImageSaveService.cs` — dedicated background thread saves raw grab images off the inspection hot path
  - Logging, IniFile, and other helpers (referenced but not individually explored)

## Data Flow

**Standard Inspection Flow (TCP-triggered):**

1. External handler sends `TestPacket` over TCP to `VisionServer`.
2. `SystemHandler.MainRun()` dequeues the packet and calls `ProcessTest(packet)`.
3. `ResourceMap.SetIdentifier()` translates `(site, testType)` → `(sequenceName, actionName)`.
4. `SequenceHandler.Start(packet)` finds the target `SequenceBase` and calls `seq.Start(packet)`.
5. The sequence thread (running `MainExecute`) detects `Command = Start`, selects the matching `ActionBase`, and calls `ExecuteAction()`.
6. `ActionBase.Run()` grabs a `HImage` from `DeviceHandler.GrabHalconImage()`, queues a copy to `RawImageSaveService`, runs the Halcon algorithm (`RoiLineIntersectionAlgorithm` or `MeasurementAlgorithm`), and writes results to `ActionContext`.
7. On finish, `SequenceBase.Finish()` enqueues a `TestResultPacket` in `ResponseQueue`.
8. `SystemHandler.MainRun()` drains `ResponseQueue` and sends `TestResultPacket` back via `VisionServer`.

**Recipe Load Flow:**

1. UI or TCP `RecipeChangePacket` calls `SystemHandler.LoadRecipe(name)`.
2. `SequenceHandler.LoadRecipe()` reads the INI file. If `[SHOTS]` section is present, `TryLoadNewFormat()` activates and `InspectionRecipeManager.Load()` populates `ShotConfig` list; `RebuildInspectionActions()` regenerates `Action_FAIMeasurement` instances dynamically.
3. Legacy format reads `Param0..N` sections into each `ActionBase.Param` via `ParamBase.Load()`.
4. `ExecOnLoad()` calls `OnLoad()` on every sequence/action, applying camera and light properties.

**State Management:**
- Per-sequence state machine: `EContextState` (Idle → Running → Finish/Error) managed by `ESequenceCommmand` enum.
- `SequenceContext` and `ActionContext` carry the live state, result images, and overlay data that UI consumes.
- `SystemHandler.IsInitializeFail` blocks operation if device or light init failed.

## Key Abstractions

**SequenceBase / ActionBase:**
- Purpose: Template method pattern. `SequenceBase` drives the execution loop; concrete actions override `Run()` with step-based (`switch(Step)`) logic.
- Examples: `WPF_Example/Sequence/Sequence/SequenceBase.cs`, `WPF_Example/Sequence/Action/ActionBase.cs`
- Pattern: Each action increments `Step` each call cycle until `FinishAction()` is called; the sequence thread runs at ~5 ms intervals.

**ParamBase:**
- Purpose: Reflection-based INI serialization. Subclasses declare public properties; `ParamBase.Save()`/`Load()` serialize them automatically by type. Custom attributes (`[Rectangle]`, `[ModelFinder]`) self-register drawable overlays.
- Examples: `WPF_Example/Sequence/Param/ParamBase.cs`, `TopInspectionParam` in `Custom/Sequence/Top/Action_TopInspection.cs`

**VirtualCamera:**
- Purpose: Hardware abstraction. All sequence code calls `DeviceHandler.GrabHalconImage(param)` and receives an `HImage` regardless of whether the underlying camera is Basler, HIK, or a virtual/simulated source.
- Examples: `WPF_Example/Device/Camera/VirtualCamera.cs`

**SequenceContext / ActionContext:**
- Purpose: Shared result carrier. Actions write to `ActionContext`; the sequence copies to `SequenceContext` on action finish. UI reads `SequenceContext` for display.
- Examples: `WPF_Example/Sequence/Sequence/SequenceContext.cs`

**ResourceMap:**
- Purpose: Protocol adapter. Decouples TCP packet field values (integer site/type codes) from internal string-based identifiers (sequence name, action name, camera name, light group name).
- Examples: `WPF_Example/TcpServer/ResourceMap.cs`, `WPF_Example/Custom/TcpServer/ResourceMap.cs`

**InspectionRecipeManager + ShotConfig + FAIConfig:**
- Purpose: Dynamic FAI (First Article Inspection) recipe model. Shots are camera positions; each shot owns N FAI measurements. Supports runtime rebuild of `Action_FAIMeasurement` instances without restart.
- Examples: `WPF_Example/Custom/Sequence/Inspection/InspectionRecipeManager.cs`, `ShotConfig.cs`, `FAIConfig.cs`

## Entry Points

**Application Start:**
- Location: `WPF_Example/App.xaml` → `Application_Startup` in `WPF_Example/App.xaml.cs`
- Triggers: Windows process launch
- Responsibilities: Mutex guard (single-instance), language init, creates and shows `MainWindow`

**System Initialization:**
- Location: `WPF_Example/MainWindow.xaml.cs` → calls `SystemHandler.Handle.Initialize()`
- Triggers: `MainWindow` constructor/Loaded event
- Responsibilities: Initializes lights, sequences, TCP server, RawImageSaveService, starts system thread

**System Loop:**
- Location: `WPF_Example/SystemHandler.cs` → `SystemProcess()` → `MainRun()` in `WPF_Example/Custom/SystemHandler.cs`
- Triggers: Background thread, 1 ms polling
- Responsibilities: Route TCP packets to sequences, drain inspection result queues and send TCP responses

**Sequence Registration:**
- Location: `WPF_Example/Custom/Sequence/SequenceHandler.cs` → `RegisterSequences()`, `RegisterActions()`, `InitializeSequences()`
- Triggers: `SequenceHandler` constructor (called from `SystemHandler.Initialize()`)
- Responsibilities: Creates `TopSequence`, `BottomSequence`, `TopInspectionAction`, etc. and wires them together via `SequenceBuilder`

## Error Handling

**Strategy:** Fail-fast at initialization; result-code propagation during runtime.

**Patterns:**
- `DeviceHandler.Initialize()` returns `EInitializeResult` flags enum; `SystemHandler` checks and sets `IsInitializeFail = true` and shows `CustomMessageBox` modal.
- `SequenceBase.ExecuteAction()` checks `ActionContext.Result == EContextResult.Error` and transitions sequence to error state, calling `OnError` event.
- `ActionBase` subclasses call `FinishAction(EContextResult.Error)` on failure rather than throwing exceptions.
- Unhandled WPF dispatcher exceptions are caught in `App.Dispatcher_UnhandledException` and displayed via `CustomMessageBox`.
- Image save failures (`SaveResultImage`, `RawImageSaveService`) are caught and logged; they do not abort inspection.
- Halcon algorithm exceptions are swallowed (return `false`) in all public algorithm methods to prevent inspection thread crash.

## Cross-Cutting Concerns

**Logging:** `Logging` static utility with typed channels (`ELogType.Trace`, `Camera`, `TcpConnection`, `Result`, `Error`, `LightController`, `Image`). Called as `Logging.PrintLog((int)ELogType.Trace, "[TAG] message", ...)`.

**Validation:** Recipe version check on load in `SequenceHandler.LoadFromIni()` — currently a no-op stub (comment notes mismatch is detected but no action taken).

**Authentication:** `LoginManager` singleton tracks login state. `Setting.AutoLogoutWhenRecvTest` auto-logs out on TCP test receipt. UI `IsEditable` flag is toggled based on login level.

**Simulation Mode:** `#if SIMUL_MODE` compile flag in `DeviceHandler` replaces missing cameras with `VirtualCamera` backed by a static image path (`D:\1.bmp`). Action code also supports offline inspection via `IOfflineImageParam.GetLatestImagePath()`.

---

*Architecture analysis: 2026-04-02*
