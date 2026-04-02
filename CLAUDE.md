<!-- GSD:project-start source:PROJECT.md -->
## Project

**DataMeasurement**

WPF 기반 산업용 비전 검사 시스템. Halcon 이미지 처리를 사용하여 카메라(Top/Side/Bottom)로 촬영한 이미지에서 에지 측정 및 공차 판정을 수행한다. TCP 서버를 통해 외부 장비(핸들러/호스트)와 통신하며, 시퀀스 엔진이 검사 흐름을 관리한다.

원본 프로젝트(NewDDA, MIL+OpenCV)를 Halcon 기반으로 변환한 프로젝트이다.

**Core Value:** Shot-FAI 2계층 동적 구조로 100개 이상의 검사 항목을 유연하게 관리하고, Halcon 에지 측정으로 정밀한 거리 측정(mm) + 공차 판정을 수행하는 것.

### Constraints

- **Tech stack**: .NET Framework 4.8 + WPF + Halcon 24.11 — 변경 불가
- **Architecture**: SystemHandler 싱글턴 + SequenceBase/ActionBase 패턴 유지
- **Compatibility**: 기존 INI 레시피 포맷과 하위 호환 (IsDynamicFAIMode 분기)
- **Hardware**: HIK 카메라 SDK (MvCamCtrl.Net), 실제 테스트는 SIMUL_MODE로 대체
<!-- GSD:project-end -->

<!-- GSD:stack-start source:codebase/STACK.md -->
## Technology Stack

## Languages
- C# 7.2 - Application logic, device drivers, vision algorithms, UI code-behind
- XAML - WPF UI layout and styling (`WPF_Example/UI/**/*.xaml`, `WPF_Example/MainWindow.xaml`)
- Python - Test mock scripts only (`Test/mock_vision_client.py`, `Test/mock_vision_server.py`)
## Runtime
- .NET Framework 4.8 (CLR v4.0)
- Target platform: x64 (forced in Debug/x64; AnyCPU in Release/AnyCPU)
- NuGet (packages.config format, classic-style — not SDK-style PackageReference)
- Lockfile: `WPF_Example/packages.config` present; `packages/` directory present
## Frameworks
- WPF (Windows Presentation Foundation) - Primary UI framework; MDI window layout via `WPF.MDI` v1.1.1 (local DLL at `bin/x64/Debug/WPF.MDI.dll`)
- MSBuild 15.0 (`WPF_Example/DatumMeasurement.csproj`)
- Output: `DatumMeasurement.exe` (WinExe, namespace root `ReringProject`)
- Configurations: Debug/AnyCPU, Debug/x64, Release/AnyCPU, Release/x64
- Conditional compile symbol: `SIMUL_MODE` (enabled in Debug builds) — enables offline image simulation paths
- Unsafe code blocks: allowed (`AllowUnsafeBlocks=true`) — used in camera pixel buffer operations
- No test framework detected (Python mock scripts exist in `Test/` but are standalone; no xUnit/NUnit/MSTest project)
## Key Dependencies
- `halcondotnet` (HALCON 24.11 Progress Steady) — installed at `C:\Program Files\MVTec\HALCON-24.11-Progress-Steady\bin\dotnet35\halcondotnet.dll`; used throughout `WPF_Example/Halcon/` for HImage, HTuple, edge measurement
- `OpenCvSharp4` v4.8.0 (2023-07-08) — .NET binding for OpenCV 4.8; used in camera drivers and `HalconImageBridge.cs` for HImage↔Mat conversion
- `OpenCvSharp4.Extensions` v4.8.0 — BitmapSource/Bitmap helpers
- `OpenCvSharp4.WpfExtensions` v4.8.0 — WPF BitmapSource output
- `OpenCvSharp4.runtime.win` v4.8.0 — native win64 OpenCV runtime (injected via MSBuild props)
- `Basler.Pylon` v1.1.0 — Basler GigE/USB camera SDK (`bin/x64/Debug/Basler.Pylon.dll`); used in `WPF_Example/Device/Camera/Basler/`
- `MvCamCtrl.Net` v4.1.0.3 — Hikvision/MvCam camera SDK (`bin/x64/Debug/MvCamCtrl.Net.dll`); used in `WPF_Example/Device/Camera/Hik/`
- `Newtonsoft.Json` v13.0.3 — JSON serialization for settings, recipes, account DB
- `MathNet.Numerics` v5.0.0 — numerical computation (used in wafer scan inspection `Custom/Sequence/Wafer/`)
- `ZXing.Net` v0.16.9 — barcode/QR reading (used in `Sequence/Sequence/SequenceBase.cs` and wafer scan)
- `PropertyTools` v3.1.0 + `PropertyTools.Wpf` v3.1.0 — property grid with `[Category]`, `[DirectoryPath]`, `[AutoUpdateText]` annotations; drives the Settings window
- `ChartDirector.Net` v7.1.0 + `ChartDirector.Net.Desktop.Controls` v7.1.0 — charting library (used in wafer map views)
- `Ookii.Dialogs.Wpf` v5.0.1 — improved file/folder dialog (used in `UI/Device/DeviceSelector.xaml.cs`)
- `ImageGlass.ImageBox` (local DLL `bin/x64/Debug/dll/x64/`) — image viewer control (used in wafer map UI)
- `System.Drawing.Common` v7.0.0
- `System.Memory` v4.5.5, `System.Buffers` v4.5.1, `System.Runtime.CompilerServices.Unsafe` v6.0.0 — span/memory primitives
- `System.Numerics.Vectors` v4.5.0, `System.ValueTuple` v4.5.0
- `ExternalLib/VisionLib/Alligator/Alligator.cs`, `AlligatorDef.cs` — custom vision library (Alligator algo, likely legacy MIL-era wrapper)
- `ExternalLib/VisionLib/AlligatorAlgMil/AlligatorAlgMil.cs`, `AlligatorAlgMilDef.cs` — MIL-based algorithm variant
## Configuration
- `WPF_Example/App.config` — assembly binding redirects only (no app secrets); runtime target `v4.0/.NETFramework,Version=v4.8`
- `WPF_Example/Setting/SystemSetting.cs` — singleton `SystemSetting.Handle`; persists as `Setting.ini` + `Setting.json` in the application base directory
- Key configurable paths (all file-system local, no cloud):
- `ServerPort` — TCP vision server port (default 2505)
- `Language` — localization selection (multi-language supported via `LocalizationResource`)
- `WPF_Example/DatumMeasurement.csproj` — MSBuild 15.0 project
- Debug symbols: full in Debug, pdbonly in Release
- Icon: `WPF_Example/Camera_DDA.ico`
- Assembly name: `DatumMeasurement`; root namespace: `ReringProject`
## Platform Requirements
- Windows only (WPF, Win32 P/Invoke in camera drivers, `System.IO.Ports` for serial light controllers)
- HALCON 24.11 Progress Steady must be installed at `C:\Program Files\MVTec\HALCON-24.11-Progress-Steady\`
- Basler Pylon runtime and Hikvision MvCam SDK runtime must be installed/present in `bin/x64/Debug/`
- x64 architecture required for camera SDKs
- Windows x64 desktop application
- Local disk storage for all data (no cloud, no network DB)
- Serial COM ports for light controllers (JPF, Pamtekbrands via `System.IO.Ports.SerialPort`)
- GigE/USB camera hardware (Basler or Hikvision)
<!-- GSD:stack-end -->

<!-- GSD:conventions-start source:CONVENTIONS.md -->
## Conventions

## Naming Patterns
- Action classes: `Action_<SequenceName><Role>.cs` (e.g., `Action_TopInspection.cs`, `Action_FAIMeasurement.cs`)
- Sequence classes: `Sequence_<Name>.cs` (e.g., `Sequence_Top.cs`, `Sequence_Bottom.cs`)
- Date-versioned backup files follow the pattern `Action_BottomInspection_0428.cs` — these are dead weight and NOT compiled into the project. Ignore them; use only the un-dated filename.
- Model/data classes: PascalCase noun (e.g., `RoiDefinition.cs`, `TeachingJob.cs`, `ShotConfig.cs`)
- Services: `<Domain>Service.cs` for new code (e.g., `RawImageSaveService.cs`, `TeachingStorageService.cs`)
- ViewModels: `<Feature>ViewModel.cs` (e.g., `InspectionListViewModel.cs`, `ModelFinderViewModel.cs`)
- PascalCase always
- Action classes: prefix `Action_` (legacy pattern), or `<Name>Action` in newer code (e.g., `TopInspectionAction`)
- Context classes that accompany actions: `<ActionName>Context` extending `ActionContext`
- Parameter classes for actions: `<ActionName>Param` extending `CameraSlaveParam` or `ParamBase`
- Enum types: prefix `E` + PascalCase (e.g., `ESequence`, `EAction`, `EContextState`, `ELogType`)
- Interface types: prefix `I` + PascalCase (e.g., `IHalconTeachingProvider`, `ICameraParam`, `IDrawableItem`)
- Public properties: PascalCase (e.g., `IsInitialized`, `CurrentActionIndex`, `ShotName`)
- Private fields: camelCase, sometimes prefixed with `_` in newer code (e.g., `_image`, `_isStopping`, `_queue`)
- Legacy private fields: prefixed with `p` for pointer-like references to typed instances (e.g., `pMyContext`, `pCamera`, `pSystemHandle`)
- Protected fields in base classes: PascalCase with no prefix (e.g., `Actions`, `CurAction`, `Interlock`)
- Constants: UPPER_SNAKE_CASE (e.g., `MSG_STX`, `MSG_ETX`, `DEFAULT_LOG_EXT`)
- Boolean flags: prefix `Is`, `Has`, or `b` (legacy) (e.g., `IsOpen`, `HasImage`, `bCreated`)
- PascalCase for all public and protected methods (e.g., `OnCreate`, `FinishAction`, `TryInspectSingleEdge`)
- `Try` prefix for methods using the `out` parameter result pattern returning `bool` (e.g., `TryInspectSingleEdge`, `TryRun`, `TryFitLine`)
- Lifecycle callbacks: `On<Event>` naming (e.g., `OnCreate`, `OnBegin`, `OnEnd`, `OnLoad`, `OnPaused`, `OnResume`)
- Event handlers: `<Subject>Button_Click`, `<Event>Handler` convention
- Defined in `Custom/Define/ID.cs` or local to the file where used
- Member names: PascalCase (e.g., `EAction.Top_Inspection`, `ESequence.Top`)
- Step enums inside action classes: private `enum EStep { Init, Grab, Measure, End }` pattern
## Code Style
- No `.editorconfig` or `.prettierrc` detected; formatting is inconsistent between modules
- Older code (Logging, SequenceBase, VirtualCamera): K&R brace style — opening brace on same line as declaration
- Newer Halcon code (MeasurementAlgorithm, RoiDefinition, TeachingStorageService): Allman brace style — opening brace on its own line
- Use the style of the file/module you are editing; do not mix within one file
- C# 7.2 (set in `.csproj`); avoid C# 8.0+ features (`nullable reference types`, `switch expressions`, `record` types)
- Target framework: .NET Framework 4.8
- Single blank line between methods
- No trailing blank lines inside methods
- One statement per line
## Import Organization
- Root: `ReringProject`
- Sub-namespaces: `ReringProject.Sequence`, `ReringProject.Device`, `ReringProject.Halcon.Algorithms`, `ReringProject.Halcon.Models`, `ReringProject.Halcon.Services`, `ReringProject.Network`, `ReringProject.UI`, `ReringProject.Utility`, `ReringProject.Setting`, `ReringProject.Define`
- Custom (project-specific overrides): `Custom/` folder files share the same namespaces as their base counterparts; no separate namespace for custom code
## Error Handling
- Wrap all `HOperatorSet.*` calls in `try { } catch { return false; }` — bare `catch` suppresses exception detail
- Public entry points validate arguments before calling internal logic
- Example pattern from `MeasurementAlgorithm.cs`:
- Call `FinishAction(EContextResult.Error)` and return `Context` to signal a fatal step failure
- Never throw from `Run()` — return a context with error state
- Errors propagate via `SequenceBase.Error()`, which sets `EContextState.Error`, stops the thread command, and fires `OnError`
- Wrap in `try/catch (Exception ex)` and log with `Logging.PrintLog` or `Logging.PrintErrLog`
- Non-critical cleanup operations (temp file deletion, log rotation): bare `catch { }` silently swallowing is acceptable here per existing pattern in `TeachingStorageService.CleanupTempImages` and `Logging.DeleteLogByDay`
- `HImage` objects must be disposed; use `using` blocks for short-lived images or explicit `.Dispose()` in `try/finally`
- Pattern used in `Action_TopInspection.cs`:
- Thread-safe image buffers: use `lock (_imageLock)` around all read/write of shared `HImage` fields (see `ShotConfig.cs`)
## Logging
- Defined as `ELogType` enum (e.g., `ELogType.Trace`, `ELogType.Camera`, `ELogType.Error`, `ELogType.Image`)
- Each log type maps to a separate file, registered at startup via `Logging.SetLog(id, name, path)`
- Always cast `ELogType` to `int` when calling `Logging.*`
- Log errors at the site where they are caught, not silently
## Comments
- Business logic that maps hardware protocol to software concepts (TCP packet fields, zone/site mapping)
- Non-obvious algorithm parameters (why sigma=1.0, why trimCount is applied, etc.)
- Thread-safety intent (e.g., `// Thread-safe image buffer`)
- Stubs that must be replaced: `// Phase 8: Halcon edge measurement will be implemented here`
- Used on public utility methods in `Logging.cs` and `VirtualCamera.cs`
- Required on new public-facing service/utility methods
- Not required on UI event handlers or override lifecycle methods
- Used in `SequenceBase.cs` to group delegates and enums (`#region delegates`, `#region enums`)
- Use only for top-level logical groupings in large files; do not use inside methods
## Function Design
- `bool` for success/failure (algorithms, device operations)
- The current `Context` object for `ActionBase.Run()` overrides
- `string` summaries for Halcon batch results (see `MeasurementAlgorithm.Run`)
- Null-safe: return `null` from load helpers when file not found; callers must null-check
## Module Design
- Sequence state machine runs on a dedicated `Thread` per `SequenceBase`
- Shared resources protected by `lock (object)` — see `Logging.lockObject`, `ShotConfig._imageLock`, `VirtualCamera.Interlock`
- `ConcurrentQueue<T>` used for cross-thread messaging (e.g., `ResponseQueue` in `SequenceBase`, `RawImageSaveService._queue`)
- `volatile bool` for termination flags (e.g., `RawImageSaveService._isStopping`, `_isStarted`)
<!-- GSD:conventions-end -->

<!-- GSD:architecture-start source:ARCHITECTURE.md -->
## Architecture

## Pattern Overview
- A single top-level `SystemHandler` singleton (`WPF_Example/SystemHandler.cs`) owns and coordinates every subsystem: devices, sequences, TCP server, logging, recipe management, and UI state.
- Inspection logic is organized as named **Sequences** (camera threads), each owning an ordered list of **Actions** (step-level work units). Each sequence runs on its own `Thread` at `ThreadPriority.Highest`.
- A second partial class `WPF_Example/Custom/SystemHandler.cs` contains the `MainRun()` polling loop (runs at 1 ms interval) that dispatches incoming TCP packets to sequences and drains response queues.
- The codebase uses a "framework + custom" split: the `WPF_Example/Sequence/` and `WPF_Example/Device/` directories contain reusable base classes, while `WPF_Example/Custom/` contains project-specific overrides and registrations via C# `partial class`.
- UI follows MVVM with limited adoption: some views carry ViewModel classes, others use direct code-behind with `INotifyPropertyChanged`.
## Layers
- Purpose: Application entry, mutex guard, unhandled exception handling, language init.
- Location: `WPF_Example/App.xaml`, `WPF_Example/App.xaml.cs`
- Contains: `App` class; creates `MainWindow` on startup.
- Depends on: `SystemHandler`, `MainWindow`
- Purpose: Singleton that owns all subsystems, drives the system loop, and routes TCP commands to sequences.
- Location: `WPF_Example/SystemHandler.cs` (constructor + Initialize/Release), `WPF_Example/Custom/SystemHandler.cs` (MainRun + ProcessXxx methods)
- Contains: `DeviceHandler`, `LightHandler`, `SequenceHandler`, `VisionServer`, `RawImageSaveService`, `LoginManager`, `RecipeFiles`, `SystemSetting`
- Depends on: all subsystems below
- Used by: UI layer for status queries, any class that calls `SystemHandler.Handle.*`
- Purpose: Generic sequence-state machine infrastructure. Each sequence is a named thread; each action is a step executed within that thread.
- Location: `WPF_Example/Sequence/Sequence/SequenceBase.cs`, `WPF_Example/Sequence/Action/ActionBase.cs`, `WPF_Example/Sequence/SequenceBuilder.cs`, `WPF_Example/Sequence/SequenceHandler.cs`
- Contains: `SequenceBase`, `ActionBase`, `ActionContext`, `SequenceContext`, `ParamBase`, `SequenceBuilder`, `SequenceHandler`
- Depends on: Define enums (`ESequence`, `EAction`), `HalconDotNet`, `DeviceHandler`
- Used by: `SystemHandler`, UI for status display
- Purpose: Concrete sequence and action implementations for this machine (Top, Side, Bottom cameras, FAI measurement).
- Location: `WPF_Example/Custom/Sequence/`
- Depends on: Framework sequence layer, `HalconDotNet`, `DeviceHandler`, `Halcon/` algorithms
- Used by: `SequenceHandler`, `SystemHandler`
- Purpose: Abstract camera access. All cameras are exposed as `VirtualCamera` instances regardless of real hardware (Basler, HIK, or simulated).
- Location: `WPF_Example/Device/DeviceHandler.cs`, `WPF_Example/Device/Camera/VirtualCamera.cs`, `WPF_Example/Device/Camera/Basler/`, `WPF_Example/Device/Camera/Hik/`
- Contains: `DeviceHandler` (singleton), `VirtualCamera`, `BaslerCamera`, `HikCamera`, `DeviceInfo`
- Depends on: Camera SDKs (Basler Pylon, HIK MVS), `HalconDotNet` for `GrabHalconImage()`
- Used by: `SystemHandler`, `ActionBase` subclasses
- Purpose: Project-specific camera names, resolutions, and orientations.
- Location: `WPF_Example/Custom/Device/DeviceHandler.cs`
- Contains: Constants `CAMERA_TOP`, `CAMERA_SIDE`, `CAMERA_BOTTOM`; `RegisterRequiredDevices()` called in `DeviceHandler` constructor.
- Purpose: Wraps Halcon image processing algorithms and teaching data management.
- Location: `WPF_Example/Halcon/`
- Depends on: `HalconDotNet`, `System.Windows`
- Used by: `Action_TopInspection`, future FAI measurement actions
- Purpose: External handler/host communicates with this vision system via TCP. Handles test requests, recipe changes, light control, and site status.
- Location: `WPF_Example/TcpServer/VisionServer.cs`, `VisionRequestPacket.cs`, `VisionResponsePacket.cs`, `TcpServer.cs`
- Contains: `VisionServer`, packet classes (`TestPacket`, `LightPacket`, `RecipeChangePacket`, etc.)
- Depends on: nothing internal (self-contained network layer)
- Used by: `SystemHandler.MainRun()` for recv/send, `Custom/SystemHandler.cs` for dispatch
- Purpose: Maps incoming protocol fields (site, test type) to internal names (sequence name, action name, camera name, light group name).
- Location: `WPF_Example/Custom/TcpServer/ResourceMap.cs` (custom registration), `WPF_Example/TcpServer/ResourceMap.cs` (framework map logic)
- Purpose: INI-based system configuration, recipe file paths, log paths.
- Location: `WPF_Example/Setting/SystemSetting.cs`, `WPF_Example/Custom/SystemSetting.cs`
- Contains: `SystemSetting` (singleton), paths, flags like `SaveFailImage`, `AutoLogoutWhenRecvTest`
- Purpose: WPF presentation. `MainWindow` hosts a menu bar, status bar, and content views. Main content is `MainView` which renders camera images and inspection results.
- Location: `WPF_Example/UI/`, `WPF_Example/Custom/UI/`
- Depends on: `SequenceHandler`, `DeviceHandler`, `Halcon/` services
- Used by: User interaction only
- Purpose: Logging, INI file I/O, raw image save worker.
- Location: `WPF_Example/Utility/`
## Data Flow
- Per-sequence state machine: `EContextState` (Idle → Running → Finish/Error) managed by `ESequenceCommmand` enum.
- `SequenceContext` and `ActionContext` carry the live state, result images, and overlay data that UI consumes.
- `SystemHandler.IsInitializeFail` blocks operation if device or light init failed.
## Key Abstractions
- Purpose: Template method pattern. `SequenceBase` drives the execution loop; concrete actions override `Run()` with step-based (`switch(Step)`) logic.
- Examples: `WPF_Example/Sequence/Sequence/SequenceBase.cs`, `WPF_Example/Sequence/Action/ActionBase.cs`
- Pattern: Each action increments `Step` each call cycle until `FinishAction()` is called; the sequence thread runs at ~5 ms intervals.
- Purpose: Reflection-based INI serialization. Subclasses declare public properties; `ParamBase.Save()`/`Load()` serialize them automatically by type. Custom attributes (`[Rectangle]`, `[ModelFinder]`) self-register drawable overlays.
- Examples: `WPF_Example/Sequence/Param/ParamBase.cs`, `TopInspectionParam` in `Custom/Sequence/Top/Action_TopInspection.cs`
- Purpose: Hardware abstraction. All sequence code calls `DeviceHandler.GrabHalconImage(param)` and receives an `HImage` regardless of whether the underlying camera is Basler, HIK, or a virtual/simulated source.
- Examples: `WPF_Example/Device/Camera/VirtualCamera.cs`
- Purpose: Shared result carrier. Actions write to `ActionContext`; the sequence copies to `SequenceContext` on action finish. UI reads `SequenceContext` for display.
- Examples: `WPF_Example/Sequence/Sequence/SequenceContext.cs`
- Purpose: Protocol adapter. Decouples TCP packet field values (integer site/type codes) from internal string-based identifiers (sequence name, action name, camera name, light group name).
- Examples: `WPF_Example/TcpServer/ResourceMap.cs`, `WPF_Example/Custom/TcpServer/ResourceMap.cs`
- Purpose: Dynamic FAI (First Article Inspection) recipe model. Shots are camera positions; each shot owns N FAI measurements. Supports runtime rebuild of `Action_FAIMeasurement` instances without restart.
- Examples: `WPF_Example/Custom/Sequence/Inspection/InspectionRecipeManager.cs`, `ShotConfig.cs`, `FAIConfig.cs`
## Entry Points
- Location: `WPF_Example/App.xaml` → `Application_Startup` in `WPF_Example/App.xaml.cs`
- Triggers: Windows process launch
- Responsibilities: Mutex guard (single-instance), language init, creates and shows `MainWindow`
- Location: `WPF_Example/MainWindow.xaml.cs` → calls `SystemHandler.Handle.Initialize()`
- Triggers: `MainWindow` constructor/Loaded event
- Responsibilities: Initializes lights, sequences, TCP server, RawImageSaveService, starts system thread
- Location: `WPF_Example/SystemHandler.cs` → `SystemProcess()` → `MainRun()` in `WPF_Example/Custom/SystemHandler.cs`
- Triggers: Background thread, 1 ms polling
- Responsibilities: Route TCP packets to sequences, drain inspection result queues and send TCP responses
- Location: `WPF_Example/Custom/Sequence/SequenceHandler.cs` → `RegisterSequences()`, `RegisterActions()`, `InitializeSequences()`
- Triggers: `SequenceHandler` constructor (called from `SystemHandler.Initialize()`)
- Responsibilities: Creates `TopSequence`, `BottomSequence`, `TopInspectionAction`, etc. and wires them together via `SequenceBuilder`
## Error Handling
- `DeviceHandler.Initialize()` returns `EInitializeResult` flags enum; `SystemHandler` checks and sets `IsInitializeFail = true` and shows `CustomMessageBox` modal.
- `SequenceBase.ExecuteAction()` checks `ActionContext.Result == EContextResult.Error` and transitions sequence to error state, calling `OnError` event.
- `ActionBase` subclasses call `FinishAction(EContextResult.Error)` on failure rather than throwing exceptions.
- Unhandled WPF dispatcher exceptions are caught in `App.Dispatcher_UnhandledException` and displayed via `CustomMessageBox`.
- Image save failures (`SaveResultImage`, `RawImageSaveService`) are caught and logged; they do not abort inspection.
- Halcon algorithm exceptions are swallowed (return `false`) in all public algorithm methods to prevent inspection thread crash.
## Cross-Cutting Concerns
<!-- GSD:architecture-end -->

<!-- GSD:workflow-start source:GSD defaults -->
## GSD Workflow Enforcement

Before using Edit, Write, or other file-changing tools, start work through a GSD command so planning artifacts and execution context stay in sync.

Use these entry points:
- `/gsd:quick` for small fixes, doc updates, and ad-hoc tasks
- `/gsd:debug` for investigation and bug fixing
- `/gsd:execute-phase` for planned phase work

Do not make direct repo edits outside a GSD workflow unless the user explicitly asks to bypass it.
<!-- GSD:workflow-end -->



<!-- GSD:profile-start -->
## Developer Profile

> Profile not yet configured. Run `/gsd:profile-user` to generate your developer profile.
> This section is managed by `generate-claude-profile` -- do not edit manually.
<!-- GSD:profile-end -->
