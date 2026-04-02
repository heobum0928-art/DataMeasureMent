# Codebase Structure

**Analysis Date:** 2026-04-02

## Directory Layout

```
DataMeasurement/                        # Solution root
├── DatumMeasurement.sln                # Visual Studio solution
├── WPF_Example/                        # Single C# project (DatumMeasurement)
│   ├── App.xaml / App.xaml.cs         # Application entry
│   ├── MainWindow.xaml / .cs          # Main shell window
│   ├── SystemHandler.cs               # System orchestrator (partial base)
│   ├── DatumMeasurement.csproj        # Project file
│   ├── Custom/                        # Project-specific overrides (partial classes + registrations)
│   │   ├── Define/ID.cs               # ESequence, EAction enum definitions
│   │   ├── Device/                    # Camera names, resolution constants, RegisterRequiredDevices()
│   │   ├── ErrorCode/                 # Project-specific error codes
│   │   ├── Sequence/                  # Concrete sequences and actions
│   │   │   ├── SequenceHandler.cs     # Partial: RegisterSequences/Actions/InitializeSequences
│   │   │   ├── Top/                   # TopSequence, Action_TopInspection
│   │   │   ├── Bottom/                # BottomSequence, Action_BottomInspection
│   │   │   ├── Inspection/            # Action_FAIMeasurement, InspectionRecipeManager, ShotConfig, FAIConfig
│   │   │   └── Wafer/                 # WaferScan sequences (legacy/alternate)
│   │   ├── SystemHandler.cs           # Partial: MainRun() and ProcessXxx() methods
│   │   ├── SystemSetting.cs           # Partial: project-specific setting fields
│   │   ├── TcpServer/ResourceMap.cs   # Maps protocol site/type codes to internal resource names
│   │   ├── UI/                        # Custom UI controls (WaferMapView, WaferMapRightView)
│   │   └── UserData/GlobalUserData.cs # Shared cross-sequence runtime state
│   ├── Device/                        # Camera hardware abstraction (framework)
│   │   ├── DeviceHandler.cs           # Singleton: open/close/enumerate cameras
│   │   ├── Camera/
│   │   │   ├── VirtualCamera.cs       # Unified camera interface used by all actions
│   │   │   ├── Basler/                # BaslerCamera implementation
│   │   │   └── Hik/                   # HikCamera implementation
│   │   └── LightController/           # Light controller driver
│   ├── ExternalLib/                   # Bundled external vision libraries (Alligator MIL wrapper)
│   ├── Halcon/                        # Halcon image processing layer
│   │   ├── Algorithms/
│   │   │   ├── MeasurementAlgorithm.cs         # Edge measurement via MeasurePos
│   │   │   └── RoiLineIntersectionAlgorithm.cs # Line intersection via edge detection
│   │   ├── Display/HalconDisplayService.cs     # WPF overlay drawing
│   │   ├── HalconImageBridge.cs                # HImage ↔ WPF BitmapSource conversion
│   │   ├── Models/                             # RoiDefinition, TeachingJob, EdgeInspectionOverlay, etc.
│   │   └── Services/TeachingStorageService.cs  # Teaching job JSON I/O + HalconTeachingHelper static
│   ├── Login/                         # LoginManager, user account model
│   ├── Sequence/                      # Sequence engine framework (base classes only)
│   │   ├── Action/ActionBase.cs       # Base for all actions
│   │   ├── Param/                     # ParamBase, CameraParam, CameraMasterParam, CameraSlaveParam
│   │   ├── Sequence/
│   │   │   ├── SequenceBase.cs        # Thread-per-sequence state machine
│   │   │   └── SequenceContext.cs     # Shared result + state carrier
│   │   ├── SequenceBuilder.cs         # Builder pattern for wiring sequences+actions
│   │   └── SequenceHandler.cs         # Singleton registry; recipe load/save
│   ├── Setting/SystemSetting.cs       # INI-backed system configuration singleton
│   ├── TcpServer/                     # TCP server framework
│   │   ├── VisionServer.cs            # Server lifecycle
│   │   ├── TcpServer.cs               # Low-level TCP accept/send/recv
│   │   ├── ResourceMap.cs             # Protocol-to-resource mapping (framework logic)
│   │   ├── VisionRequestPacket.cs     # Inbound packet hierarchy
│   │   └── VisionResponsePacket.cs    # Outbound packet hierarchy
│   ├── UI/                            # WPF views and view models
│   │   ├── MenuBar.xaml / .cs         # Top menu bar
│   │   ├── StatusBar.xaml / .cs       # Bottom status bar
│   │   ├── ContentItem/               # Full-panel views (MainView, HalconViewerControl, LogView)
│   │   ├── ControlItem/               # Sub-panel controls (InspectionListView, CalibrationView)
│   │   ├── Dialog/                    # Modal dialogs (TeachingWindow, CustomMessageBox)
│   │   ├── Device/                    # DeviceSelector, CanvasViewer
│   │   ├── Light/                     # Light controller UI
│   │   ├── Log/                       # Log viewer windows
│   │   ├── Login/                     # Login / account manager windows
│   │   ├── ProcessMonitor/            # Process monitor window
│   │   ├── Recipe/                    # Recipe open/list dialogs
│   │   ├── Setting/                   # System settings window
│   │   ├── TcpServer/                 # TCP server status window
│   │   ├── Theme/                     # Custom WPF controls (OutlinedTextBlock)
│   │   └── ViewModel/                 # Shared view models (CalibrationViewModel, ModelFinderViewModel, Observable)
│   ├── Utility/                       # Cross-cutting utilities
│   │   └── RawImageSaveService.cs     # Background thread raw image writer
│   ├── Properties/                    # Assembly info, localization resources
│   └── Resource/                      # Static resources (logo, icon)
├── Calibration/                        # Calibration data files (.cal)
├── Recipe/                             # Recipe archive (.zip)
├── Image/                              # Test image archive (.zip)
├── MapFile/                            # Map file archive (.zip)
├── Cal_Image/                          # Calibration source images
├── Test/                               # Python test scripts for TCP protocol
│   ├── mock_vision_client.py
│   ├── mock_vision_server.py
│   └── HandlerCommunicationTest.py
├── Document/                           # Protocol and manual documentation
├── CodeBackUp/                         # Legacy code zip backups
└── .planning/codebase/                 # GSD analysis documents (this file)
```

## Directory Purposes

**`WPF_Example/Custom/`:**
- Purpose: All project-specific code. This is where machine-specific constants, sequence registrations, and action implementations live. Everything here uses `partial class` to extend framework types.
- Key files: `Define/ID.cs` (enums), `Custom/Sequence/SequenceHandler.cs` (registrations), `Custom/SystemHandler.cs` (MainRun dispatch)

**`WPF_Example/Sequence/` (framework):**
- Purpose: Reusable sequence engine. Contains base classes and the builder/handler that are not machine-specific.
- Key files: `SequenceBase.cs`, `ActionBase.cs`, `SequenceContext.cs`, `ParamBase.cs`, `SequenceBuilder.cs`, `SequenceHandler.cs`

**`WPF_Example/Custom/Sequence/`:**
- Purpose: Concrete actions and sequences for this machine. Each camera direction has its own subdirectory.
- Key files:
  - `Top/Sequence_Top.cs` — `TopSequence`, camera setup, `AddResponse()` for TCP result
  - `Top/Action_TopInspection.cs` — `TopInspectionAction`, `TopInspectionParam`, `TopInspectionContext`
  - `Bottom/Sequence_Bottom.cs`, `Bottom/Action_BottomInspection.cs` — Bottom camera equivalents
  - `Inspection/Action_FAIMeasurement.cs` — dynamic FAI action (step-based: Init → Grab → Measure → End)
  - `Inspection/InspectionRecipeManager.cs` — Shot/FAI list manager with INI serialization
  - `Inspection/ShotConfig.cs` — Per-shot camera params + FAI list
  - `Inspection/FAIConfig.cs` — Single FAI measurement definition

**`WPF_Example/Halcon/`:**
- Purpose: All Halcon-specific image processing code. Algorithms accept `HImage` and return overlay/result models. Teaching storage is JSON via `DataContractJsonSerializer`.
- Key files: `Algorithms/RoiLineIntersectionAlgorithm.cs` (used in production), `Services/TeachingStorageService.cs` + `HalconTeachingHelper` static

**`WPF_Example/Device/`:**
- Purpose: Camera hardware drivers and the `DeviceHandler` singleton. The `VirtualCamera` type is the only type that action code ever interacts with directly.

**`WPF_Example/UI/`:**
- Purpose: All WPF XAML and code-behind. Primary inspection display is `ContentItem/MainView.xaml`. Teaching ROI dialog is `Dialog/TeachingWindow.xaml`.

**`WPF_Example/Utility/`:**
- Purpose: Stateless helpers and background services. `RawImageSaveService` decouples raw image writes from the inspection thread.

## Key File Locations

**Entry Points:**
- `WPF_Example/App.xaml.cs`: Application startup, mutex guard
- `WPF_Example/MainWindow.xaml.cs`: Calls `SystemHandler.Handle.Initialize()`
- `WPF_Example/SystemHandler.cs`: Singleton constructor — device, logging, recipe init

**System Loop:**
- `WPF_Example/Custom/SystemHandler.cs`: `MainRun()` — TCP dispatch and response drain

**Sequence/Action Registration:**
- `WPF_Example/Custom/Sequence/SequenceHandler.cs`: `RegisterSequences()`, `RegisterActions()`, `InitializeSequences()`

**ID Definitions:**
- `WPF_Example/Custom/Define/ID.cs`: `ESequence`, `EAction` enums — add new IDs here first

**Camera Constants:**
- `WPF_Example/Custom/Device/DeviceHandler.cs`: Camera names, resolutions, `RegisterRequiredDevices()`

**Protocol Resource Mapping:**
- `WPF_Example/Custom/TcpServer/ResourceMap.cs`: Maps `(ESite, ETestType)` to sequence/action/camera/light names

**Recipe Persistence:**
- `WPF_Example/Sequence/SequenceHandler.cs`: `LoadFromIni()`, `SaveToIni()`, `TryLoadNewFormat()`, `SaveNewFormat()`

**Teaching Storage:**
- `WPF_Example/Halcon/Services/TeachingStorageService.cs`: `HalconTeachingHelper.LoadJob()`, `SaveJob()`, `BuildFixedTeachingPath()`

**Param Serialization:**
- `WPF_Example/Sequence/Param/ParamBase.cs`: Reflection-based INI load/save for all `ParamBase` subclasses

## Naming Conventions

**Files:**
- Framework base classes: `{Noun}Base.cs` (e.g., `SequenceBase.cs`, `ActionBase.cs`, `ParamBase.cs`)
- Custom actions: `Action_{Direction}{Type}.cs` (e.g., `Action_TopInspection.cs`, `Action_BottomInspection.cs`, `Action_FAIMeasurement.cs`)
- Custom sequences: `Sequence_{Direction}.cs` (e.g., `Sequence_Top.cs`, `Sequence_Bottom.cs`)
- Context classes: `{ActionName}Context` defined in the same file as the action
- Param classes: `{ActionName}Param` defined in the same file as the action
- XAML/code-behind pairs: `{Name}.xaml` + `{Name}.xaml.cs`

**Directories:**
- Pascal case for all source directories (e.g., `Custom`, `Sequence`, `Halcon`, `Device`)
- Camera-direction grouping within `Custom/Sequence/`: `Top/`, `Side/` (implicit via SEQ_SIDE constant), `Bottom/`, `Inspection/`, `Wafer/`

**Classes and Properties:**
- Singletons expose themselves as `public static {Type} Handle { get; }` (e.g., `SystemHandler.Handle`, `DeviceHandler.Handle`, `SequenceHandler.Handle`)
- Private member fields: `m{Name}` prefix (e.g., `mSystemThread`) or `p{Name}` for cached subsystem references (e.g., `pMyContext`, `pCamera`)
- Constants: SCREAMING_SNAKE_CASE (e.g., `CAMERA_TOP`, `SEQ_BOTTOM`, `ACT_INSPECT`)
- Events: `On{Event}` (e.g., `OnStart`, `OnFinish`, `OnError`, `OnRecipeChanged`)

## Where to Add New Code

**New Camera Direction (e.g., adding "Side" as a fully implemented sequence):**
1. Add `ESequence.Side` value to `WPF_Example/Custom/Define/ID.cs` (already present as value 2)
2. Add `EAction.Side_Inspection` to the same file (already present)
3. Create `WPF_Example/Custom/Sequence/Side/Sequence_Side.cs` — extend `SequenceBase` or `TopSequence`
4. Create `WPF_Example/Custom/Sequence/Side/Action_SideInspection.cs` — extend `ActionBase`
5. Add camera constants to `WPF_Example/Custom/Device/DeviceHandler.cs` and call `SetRequiredDevice()` in `RegisterRequiredDevices()`
6. Register in `WPF_Example/Custom/Sequence/SequenceHandler.cs` `RegisterSequences()` and `RegisterActions()` and `InitializeSequences()`
7. Add protocol mapping to `WPF_Example/Custom/TcpServer/ResourceMap.cs`

**New FAI Shot:**
- Add via `InspectionRecipeManager.AddShot()` in `WPF_Example/Custom/Sequence/Inspection/InspectionRecipeManager.cs`
- The sequence handler will call `RebuildInspectionActions()` after recipe load

**New FAI Measurement per Shot:**
- Add via `ShotConfig.AddFAI()` in `WPF_Example/Custom/Sequence/Inspection/ShotConfig.cs`
- `FAIConfig` in `WPF_Example/Custom/Sequence/Inspection/FAIConfig.cs` holds measurement definition

**New Recipe Parameter:**
- Add a public property to the relevant `ParamBase` subclass (e.g., `TopInspectionParam`). `ParamBase.Save()`/`Load()` will automatically serialize it if the type is `Int32`, `Double`, `String`, `Boolean`, `Rect`, `Line`, or `Circle`.

**New Halcon Algorithm:**
- Create a new class in `WPF_Example/Halcon/Algorithms/`
- Return results as a model type in `WPF_Example/Halcon/Models/`
- Consume it from the relevant action's `Run()` method

**New UI Window:**
- Place XAML + code-behind in the appropriate `WPF_Example/UI/{Category}/` subdirectory
- Use `SystemHandler.Handle.Sequences` and `SystemHandler.Handle.Devices` for data access
- For dialogs, follow the `CustomMessageBox`/`TeachingWindow` pattern in `WPF_Example/UI/Dialog/`

**New Utility/Service:**
- Add to `WPF_Example/Utility/`
- Register and call `Start()` in `SystemHandler.Initialize()`, `Dispose()` in `SystemHandler.Release()`

## Special Directories

**`WPF_Example/Custom/Sequence/Bottom/` (archived files):**
- Contains multiple versioned backup files: `Action_BottomCalibration_0311.cs`, `_0429.cs`, `_0502_...cs`, etc.
- Purpose: Inline version history left in the directory (not proper git history)
- Generated: No
- Committed: Yes (these are source-controlled but should not be compiled — check `.csproj` for exclusions)

**`WPF_Example/ExternalLib/VisionLib/`:**
- Purpose: Bundled Alligator MIL-compatible library (legacy from the MIL→Halcon migration origin)
- Generated: No
- Committed: Yes

**`WPF_Example/bin/`, `WPF_Example/obj/`:**
- Purpose: Build output and intermediate files
- Generated: Yes
- Committed: No (in `.gitignore`)

**`.planning/codebase/`:**
- Purpose: GSD analysis documents consumed by `/gsd:plan-phase` and `/gsd:execute-phase`
- Generated: By GSD map-codebase command
- Committed: Yes

**`Test/`:**
- Purpose: Python scripts for simulating TCP handler communication (`mock_vision_client.py`, `mock_vision_server.py`, `HandlerCommunicationTest.py`)
- Generated: No
- Committed: Yes (development/debug tools only)

---

*Structure analysis: 2026-04-02*
