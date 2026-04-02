# Codebase Concerns

**Analysis Date:** 2026-04-02

---

## Tech Debt

**Namespace Mismatch (Project Identity)**
- Issue: All source files use `namespace ReringProject.*` but the project is named `DatumMeasurement` / `DDA Vision Inspector`. This is a leftover from an earlier project skeleton that was never renamed.
- Files: Every `.cs` file in `WPF_Example/` (129 files)
- Impact: Confusing for new contributors; find/replace risks introduce typos; tooling (e.g., Roslyn analyzers) may flag this as a mismatch with the assembly name.
- Fix approach: Global find-replace `ReringProject` → `DatumMeasurement` (or chosen canonical name), update `AssemblyInfo.cs`, and verify no external DLL references the old namespace.

**Backup/Version Files Committed to Source Tree**
- Issue: Multiple dated snapshots of the same files live inside the project directory and are tracked by git instead of using proper version control branches.
- Files:
  - `WPF_Example/Custom/Sequence/Bottom/Action_BottomCalibration_0311.cs`
  - `WPF_Example/Custom/Sequence/Bottom/Action_BottomCalibration_0429.cs`
  - `WPF_Example/Custom/Sequence/Bottom/Action_BottomCalibration_0502_Picker넘버변경테스트 진행중.cs`
  - `WPF_Example/Custom/Sequence/Bottom/Action_BottomCalibration_0503_Cal파일저장.cs`
  - `WPF_Example/Custom/Sequence/Bottom/Action_BottomInspection_0320.cs`
  - `WPF_Example/Custom/Sequence/Bottom/Action_BottomInspection_0428.cs`
  - `WPF_Example/Custom/Sequence/Bottom/Sequence_Bottom_0311.cs`
  - `WPF_Example/Custom/Sequence/Bottom/Bottom_Sequence_0428.zip`
  - `WPF_Example/Custom/Sequence/Wafer/Action_WaferScanInspection_0421_대표행렬위치VM코드주석.cs`
  - `WPF_Example/Custom/Sequence/Wafer/Action_WaferScanInspection_0421_대표행렬위치VM코드주석제거.cs`
  - `WPF_Example/Custom/Sequence/Wafer/Action_WaferScanInspection_1210.cs`
  - `CodeBackUp/WPF_Example.zip`
- Impact: Inflates build times and repository size; creates ambiguity about which file is authoritative; spaces and Korean characters in filenames can break build tools on non-Korean locales.
- Fix approach: Delete all dated backup files; rely on `git log` / branches for history. Add a `.gitignore` rule for `*.zip` in source directories.

**Dual Custom/Base Layer Duplication**
- Issue: Two parallel handler trees exist side-by-side for several core concerns:
  - `WPF_Example/Device/DeviceHandler.cs` (base) + `WPF_Example/Custom/Device/DeviceHandler.cs`
  - `WPF_Example/SystemHandler.cs` (base) + `WPF_Example/Custom/SystemHandler.cs`
  - `WPF_Example/TcpServer/ResourceMap.cs` + `WPF_Example/Custom/TcpServer/ResourceMap.cs`
  - `WPF_Example/Setting/SystemSetting.cs` + `WPF_Example/Custom/SystemSetting.cs`
- Impact: Unclear which layer is canonical; risk of diverging logic; partial classes (`partial class SystemHandler`) split across both layers make understanding initialization order non-trivial.
- Fix approach: Consolidate custom/ overrides into proper subclasses or partial files within the same directory as the base.

**Large Monolithic Action Files**
- Issue: Wafer inspection action files exceed 4,000+ lines per file.
- Files:
  - `WPF_Example/Custom/Sequence/Wafer/Action_WaferScanInspection.cs` (4,621 lines)
  - `WPF_Example/Custom/Sequence/Wafer/Action_WaferScanInspection_1210.cs` (4,746 lines)
  - `WPF_Example/Custom/Sequence/Wafer/Action_WaferScanCalibration.cs` (685 lines)
- Impact: Very high cognitive load; difficult to review PRs; multiple responsibilities in a single class violate SRP; slow to navigate.
- Fix approach: Break into sub-steps or strategy classes per inspection phase (Grab, Measure, Evaluate, Report).

**Legacy MIL Algorithm Layer Still Present**
- Issue: The full MIL (Matrox Imaging Library) algorithm wrapper is still in the codebase despite the project's stated goal of migrating to Halcon. These files reference `AlligatorAlgMil` which is the MIL-era library.
- Files:
  - `WPF_Example/ExternalLib/VisionLib/Alligator/Alligator.cs`
  - `WPF_Example/ExternalLib/VisionLib/Alligator/AlligatorDef.cs`
  - `WPF_Example/ExternalLib/VisionLib/AlligatorAlgMil/AlligatorAlgMil.cs`
  - `WPF_Example/ExternalLib/VisionLib/AlligatorAlgMil/AlligatorAlgMilDef.cs`
- Impact: Adds ~775 lines of unmaintained code that must not be compiled in production; `AlligatorAlgMil.cs` contains comments "테스트 필요함" (testing needed) at lines 757/770.
- Fix approach: Remove or archive into a separate legacy branch once Halcon migration is verified complete.

**FAI Measurement Stub Not Implemented**
- Issue: `Action_FAIMeasurement.cs` contains a stub `EStep.Measure` case that always returns pass with nominal values instead of running real Halcon edge measurement.
- Files: `WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs` (lines 70–76)
- Impact: FAI inspection produces no real measurements. Any recipe using FAI mode will always pass regardless of actual die geometry.
- Fix approach: Implement Halcon `MeasurePos` calls using `FAIConfig.ROI_*` parameters and `MeasurementAlgorithm` per Phase 8 plan.

---

## Known Bugs

**Login Password Stored in Plaintext in AccountInfo**
- Symptoms: `AccountInfo.Password` is stored as a plain string and compared directly in `Login()` with `==` (no hashing). The AES encryption only protects the file on disk — in memory, passwords are cleartext.
- Files: `WPF_Example/Login/LoginManager.cs` (lines 51, 247)
- Trigger: Any memory inspection of the process while a user is logged in exposes all passwords.
- Workaround: None. The encrypted file at rest is protected, but in-memory exposure is unavoidable with current design.

**Default Admin Credentials Hardcoded**
- Symptoms: When no `account.db` file exists, the system creates an admin account with `id="admin"`, `password="admin"`. These defaults are never forced to change.
- Files: `WPF_Example/Login/LoginManager.cs` (lines 79–80, 168, 182, 204)
- Trigger: Fresh deployment or deleted `account.db`.
- Workaround: Administrator must manually change the password after first login; there is no enforcement mechanism.

**Recv Buffer Overflow Risk in TcpServer**
- Symptoms: `mRecvBuffer` is fixed at 1024 bytes. The `Recv()` loop writes `mRecvBuffer[RecvCount++]` without checking if `RecvCount` exceeds `SIZE_RECV_BUFFER`. A message longer than 1023 bytes will write past the buffer boundary.
- Files: `WPF_Example/TcpServer/TcpServer.cs` (lines 65, 86, 254)
- Trigger: Any incoming TCP message whose payload length exceeds 1023 bytes.
- Workaround: Keep all protocol messages under 1023 bytes by convention (not enforced in code).

**Static Shared `ipProperties` and `tcpConnections` in TcpServer**
- Symptoms: `ipProperties` and `tcpConnections` are declared as `static` fields on `TcpServer` (line 325–326). `IsConnected()` is called from multiple `ConnectedClient` threads simultaneously. When two clients check connectivity concurrently, both threads read/write the same static `tcpConnections` reference without synchronization.
- Files: `WPF_Example/TcpServer/TcpServer.cs` (lines 120–141, 325–326)
- Trigger: Any scenario with more than one concurrent client connection or rapid connection/disconnection.
- Workaround: In practice `MAX_CONNECTION_COUNT = 1` limits active clients, but the code allows adding to the list without enforcing this limit.

**`GetClient(string)` Uses `.Contains()` for IP Matching**
- Symptoms: `GetClient(string ipAddress)` uses `Contains()` rather than exact equality. If client IP `192.168.1.1` is connected and `1.1` is passed as argument, it will match incorrectly.
- Files: `WPF_Example/TcpServer/TcpServer.cs` (line 462)
- Trigger: When IP address substrings overlap with any connected client's full address.

**`Disconnect()` Joins Thread But Ignores Timeout**
- Symptoms: `Disconnect()` calls `mCommunicationThread.Join(1000)`. The return value of `Join()` is not checked. If the thread does not stop within 1 second, execution continues silently with the thread still running and holding the `NetworkStream`.
- Files: `WPF_Example/TcpServer/TcpServer.cs` (line 286)
- Trigger: Network I/O blocking or unresponsive system thread.

**VirtualCamera `WaitForHalconTrigger` Busy-Waits Forever**
- Symptoms: The virtual camera implementation of `WaitForHalconTrigger()` runs an empty `while(true)` loop until `timeOut` milliseconds elapse. This burns 100% of one CPU core during the wait and provides no image — it returns `LastHalconImage` which may be `null` if no background image is configured.
- Files: `WPF_Example/Device/Camera/VirtualCamera.cs` (lines 547–559)
- Trigger: Called in `SIMUL_MODE` or test setups using `VirtualCamera` with hardware trigger path.
- Workaround: Use `DebugCheck=true` in `BottomInspectionParam` to avoid the hardware trigger path.

**`IsAllOpen()` Only Checks Basler Count**
- Symptoms: `DeviceHandler.IsAllOpen()` only verifies `Basler` camera count against required Basler count. HIK and Virtual cameras are never checked. A system configured for HIK cameras may report "all open" even with zero HIK cameras initialized.
- Files: `WPF_Example/Device/DeviceHandler.cs` (lines 287–290)

**`DeviceHandler.Initialize()` — `IDList.Select()` Used as Presence Check**
- Symptoms: Lines 83 and 89 use `IDList.Select(id => id.CamType == ECameraType.Basler) != null` as a guard before enumerating devices. `Select()` never returns `null` on an `IEnumerable<T>` — this condition is always `true` and the guard has no effect.
- Files: `WPF_Example/Device/DeviceHandler.cs` (lines 83–90)
- Trigger: Always — the guard provides false safety, device enumeration always runs.

---

## Security Considerations

**AES Encryption Key Hardcoded in Source**
- Risk: The AES encryption passphrase `"1Alg!Young!Min22"` and IV seed `"ttEVbAqjGa9WTVYeexersfrvjc1nu7Cm"` are hardcoded string literals in source code. Anyone with repository access can decrypt any `account.db` file from any deployment.
- Files: `WPF_Example/Login/LoginManager.cs` (lines 85, 279, 303)
- Current mitigation: The account file is encrypted on disk using AES-256-CBC.
- Recommendations: Move the key to a per-machine secret (Windows DPAPI, environment variable, or hardware key). At minimum, do not commit key material to source control.

**RijndaelManaged is Deprecated**
- Risk: `RijndaelManaged` is marked obsolete in .NET 6+ (SYSLIB0022). On newer runtimes this will produce compilation warnings and may be removed in future framework versions.
- Files: `WPF_Example/Login/LoginManager.cs` (lines 277, 301)
- Current mitigation: None.
- Recommendations: Replace with `System.Security.Cryptography.Aes.Create()`.

**TCP Server Has No Authentication or TLS**
- Risk: The `VisionServer` / `TcpServer` accept any TCP connection on the configured port with no authentication or encryption. Commands to trigger inspection, change recipes, or query status can be sent by any host on the network.
- Files: `WPF_Example/TcpServer/TcpServer.cs`, `WPF_Example/TcpServer/VisionServer.cs`
- Current mitigation: Relies on network-level isolation (factory LAN).
- Recommendations: Add IP allowlist enforcement at the accept stage; consider HMAC message signing for the proprietary protocol if TLS is impractical.

**Simulation Mode Hard-Codes a Specific File Path**
- Risk: `#if SIMUL_MODE` hard-codes `@"D:\1.bmp"` as the simulated image path. If this build flag is accidentally left enabled in a production build, the system will silently serve a static test image as inspection input.
- Files: `WPF_Example/Device/DeviceHandler.cs` (lines 58–60)
- Recommendations: Add a compile-time assertion or runtime warning when `SIMUL_MODE` is active.

---

## Performance Bottlenecks

**`IsConnected()` Called in Hot Communication Loop**
- Problem: `ConnectedClient.Execute()` calls `IsConnected()` on every loop iteration (every 1 ms sleep). `IsConnected()` calls `IPGlobalProperties.GetActiveTcpConnections()`, which enumerates all TCP connections on the machine — a potentially expensive syscall.
- Files: `WPF_Example/TcpServer/TcpServer.cs` (lines 187–213)
- Cause: Defensive TCP state polling chosen over socket exception handling.
- Improvement path: Replace `IPGlobalProperties` polling with a flag set by `catch (SocketException)` in `Send()`/`Recv()`. The `try/catch` approach is O(1) on the happy path.

**`BitmapSource` Created on Every `Display()` Call**
- Problem: `VirtualCamera.GetPreviewBitmapSource()` creates a new `BitmapSource` (including full pixel array allocation) on every call. If the UI polls at 30 fps with a 2448×2048 camera, this allocates ~14 MB per second of short-lived objects, pressuring the GC.
- Files: `WPF_Example/Device/Camera/VirtualCamera.cs` (lines 562–597)
- Cause: No caching or change detection before re-creating the bitmap.
- Improvement path: Cache the `BitmapSource` and only rebuild when `imageCount` increments.

**`SystemProcess` Thread Runs at `ThreadPriority.Highest`**
- Problem: The main system loop thread is set to `ThreadPriority.Highest`, as is one of the `SequenceBase` constructors. Running multiple Highest-priority threads can starve lower-priority system threads including the WPF dispatcher, causing UI hitches on underpowered hardware.
- Files: `WPF_Example/SystemHandler.cs` (line 126), `WPF_Example/Sequence/Sequence/SequenceBase.cs` (line 81)
- Improvement path: Use `AboveNormal` for the sequence thread; reserve `Highest` only for hard real-time hardware trigger threads.

**Color BitmapSource Pixel Interleaving on Every Frame**
- Problem: `CreateBitmapSource()` for color images manually interleaves R/G/B planar bytes into a packed RGB24 buffer in a C# loop. At 2448×2048 color, this is ~15M iterations per frame.
- Files: `WPF_Example/Device/Camera/VirtualCamera.cs` (lines 580–596)
- Improvement path: Use `Marshal.Copy` + `WriteableBitmap.Lock/WritePixels`, or convert on the Halcon side with `ConvertImageType` before reading pointers.

**Temp Image Cleanup (`CleanupTempImages`) Calls `GetFiles` on Every Teaching Save**
- Problem: Every time an image is saved for the teaching dialog, `CleanupTempImages` enumerates all `.png` files in a temp directory. On slow network drives or directories with many files, this adds latency inside the teaching workflow.
- Files: `WPF_Example/Halcon/Services/TeachingStorageService.cs` (lines 143–160)
- Improvement path: Keep an in-memory count and only trigger cleanup after N saves.

---

## Fragile Areas

**Sequence Action Step Machine — No Mutex on `Step` or `Command`**
- Files: `WPF_Example/Sequence/Sequence/SequenceBase.cs`, `WPF_Example/Custom/Sequence/Bottom/Action_BottomInspection.cs`
- Why fragile: `Command` and `Step` (integer on `ActionBase`) are read and written from both the `MainThread` (sequence thread) and the UI/system thread (which calls `Stop()`, `Pause()`, `Resume()`). No `volatile` or `Interlocked` usage exists on these fields.
- Safe modification: Any change to state transitions should use `volatile` on `Command` or add a lock around state reads/writes.
- Test coverage: No unit or integration tests exist for the sequence state machine.

**`BottomInspectionContext` Fixed-Size Arrays of 10**
- Files: `WPF_Example/Custom/Sequence/Bottom/Action_BottomInspection.cs` (lines 52–59, 100–110)
- Why fragile: All picker/die result arrays are statically sized to 10 (`new double[10]`, `new EVisionResultType[10]`). `Grab_Count` is clamped to `[1, 10]` at runtime but the size assumption is never validated against configuration. If requirements change to 12 pickers, silent index-out-of-range or data truncation occurs.
- Safe modification: Replace magic-number arrays with `List<T>` or make array size a configurable constant derived from `Grab_Count`.

**`SequenceBase` Thread Starts in Constructor Before `OnCreate()`**
- Files: `WPF_Example/Sequence/Sequence/SequenceBase.cs` (lines 73–100)
- Why fragile: Both `SequenceBase` constructors start `MainThread` immediately. `MainThread` polls `bCreated` every 1 second. If initialization (`OnCreate()`) throws, the thread continues running in a degraded `bCreated=false` state indefinitely.
- Safe modification: Move `MainThread.Start()` to an explicit `Start()` method called after `OnCreate()` completes.

**`TeachingStorageService` Uses `DataContractJsonSerializer`**
- Files: `WPF_Example/Halcon/Services/TeachingStorageService.cs` (lines 22–38)
- Why fragile: `DataContractJsonSerializer` requires `[DataContract]` / `[DataMember]` attributes on all serialized types. If `TeachingJob` or `RoiDefinition` is extended with a new property without adding `[DataMember]`, the property is silently dropped on save/load with no error. Existing teaching data can become stale.
- Safe modification: Switch to `Newtonsoft.Json` (already a dependency) for more forgiving serialization, or enforce `[DataMember]` via code review checklist.

**`ResourceMap.Initialize()` Is Never Called**
- Files: `WPF_Example/Custom/TcpServer/ResourceMap.cs` (lines 33–55), `WPF_Example/TcpServer/VisionServer.cs` (line 15)
- Why fragile: `ResourceMap` is constructed in `VisionServer` with `new ResourceMap()`, but `Initialize()` (which populates the camera/light/sequence maps) is never invoked anywhere in the codebase. `SetIdentifier()` will return empty identifiers for all packets, causing every incoming TCP command to fail silently or be routed to null.
- Safe modification: Call `ResourceIdentifier.Initialize()` in the `VisionServer` constructor.

**Dual `Dispose()` / `Disconnect()` on `ConnectedClient`**
- Files: `WPF_Example/TcpServer/TcpServer.cs` (lines 145–147, 282–287, 370–379)
- Why fragile: `Dispose()` calls `Disconnect()`, and `OnAlarmProcess` also calls `Disconnect()` then `Dispose()`. A client that disconnects naturally goes through both paths, calling `Join()` twice on the same thread, and `mClient.Close()` twice (once in `Execute()` after `IsTerminated=true`, once if called externally).
- Safe modification: Add `_disposed` guard flag; use `Interlocked.Exchange` on `IsTerminated`.

---

## Scaling Limits

**TCP Server Hard-Limited to 1 Client**
- Current capacity: `MAX_CONNECTION_COUNT = 1` — but this constant is only used as the initial list capacity, not enforced. Multiple clients can connect simultaneously.
- Limit: Practically 1 simultaneous command client by convention.
- Scaling path: Add an explicit early-rejection in `ConnectionExecute` when `mConnectedClientList.Count >= MAX_CONNECTION_COUNT`.

**INI-Based Recipe Format for FAI Shots**
- Current capacity: INI sections `SHOT_0` through `SHOT_N` with `SHOT_0_FAI_0` through `SHOT_N_FAI_M`.
- Limit: INI files have no schema validation; adding new FAI fields requires careful key-name management. Large recipes (many shots × many FAIs) degrade load performance due to O(n) key lookup in the custom `IniFile` class.
- Scaling path: Migrate recipe persistence to JSON (`Newtonsoft.Json` is already a dependency) with schema versioning.

---

## Dependencies at Risk

**`RijndaelManaged` (Obsolete Crypto API)**
- Risk: Deprecated in .NET 6 (SYSLIB0022); will throw `PlatformNotSupportedException` in restricted environments.
- Impact: Login/account system fails entirely.
- Migration plan: Replace with `Aes.Create()` in `WPF_Example/Login/LoginManager.cs`.

**OpenCvSharp4 Two-Version Conflict**
- Risk: Both `OpenCvSharp4.4.6.0.20220608` and `OpenCvSharp4.4.8.0.20230708` exist in the `packages/` directory, along with matching `OpenCvSharp4.Extensions` and `OpenCvSharp4.Windows` pairs. Only one version should be referenced; the presence of both suggests an incomplete upgrade.
- Impact: Possible DLL conflicts at runtime if binding redirects are not correct.
- Migration plan: Verify the `.csproj` references exactly one version and remove the stale package folder.

**`System.Drawing.Common` Two-Version Conflict**
- Risk: Both `System.Drawing.Common.5.0.3` and `System.Drawing.Common.7.0.0` exist in `packages/`. Same risk as OpenCvSharp.
- Migration plan: Consolidate to the higher version and update the `.csproj`.

---

## Missing Critical Features

**No Bounds Check on TCP Recv Buffer**
- Problem: The 1024-byte receive buffer has no overflow guard. Any protocol extension with larger payloads will silently corrupt memory.
- Blocks: Safe protocol evolution.

**No Log Rotation or Disk Space Management**
- Problem: `LogDeleteDay = 30` is defined in settings but there is no code that actually deletes old log files. Logs accumulate indefinitely.
- Files: `WPF_Example/Setting/SystemSetting.cs` (line 81)
- Blocks: Long-term unattended deployment without manual disk cleanup.

**No Recipe Schema Version**
- Problem: Recipe INI files have no version field. If fields are added or removed, old recipes load silently with zero/default values for new fields, potentially causing incorrect inspection parameters.
- Blocks: Safe recipe format evolution.

---

## Test Coverage Gaps

**No Test Project Exists**
- What's not tested: The entire codebase — sequence state machine, algorithm logic, TCP protocol parsing, login/encryption, recipe load/save.
- Files: Entire `WPF_Example/` directory.
- Risk: Regressions in any subsystem are undetectable until runtime in a production environment.
- Priority: High

**Algorithm Logic Untested**
- What's not tested: `MeasurementAlgorithm.TryInspectSingleEdgeInternal()`, `RoiLineIntersectionAlgorithm.TryRun()`, `TrimExtremePoints()`.
- Files: `WPF_Example/Halcon/Algorithms/MeasurementAlgorithm.cs`, `WPF_Example/Halcon/Algorithms/RoiLineIntersectionAlgorithm.cs`
- Risk: Edge detection regressions (wrong sigma, wrong threshold, wrong polarity) produce silent NG or false OK results with no automated catch.
- Priority: High

**TCP Protocol Parsing Untested**
- What's not tested: `VisionRequestPacket.Convert()`, `VisionResponsePacket.ToString()`, `ResourceMap.SetIdentifier()`.
- Files: `WPF_Example/TcpServer/VisionRequestPacket.cs`, `WPF_Example/TcpServer/VisionResponsePacket.cs`, `WPF_Example/TcpServer/ResourceMap.cs`
- Risk: Protocol format regressions only caught when the physical PLC sends a malformed or unexpected packet.
- Priority: High

**Login and Crypto Untested**
- What's not tested: `Encrypt()` / `Decrypt()` round-trip, `Login()` boundary cases, account persistence.
- Files: `WPF_Example/Login/LoginManager.cs`
- Risk: A `account.db` file written by one version of the software may not be readable by a future version if the crypto changes.
- Priority: Medium

---

*Concerns audit: 2026-04-02*
