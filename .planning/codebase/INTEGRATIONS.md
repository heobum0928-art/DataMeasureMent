# External Integrations

**Analysis Date:** 2026-04-02

## APIs & External Services

**No cloud or internet APIs detected.** This is a fully on-premise, standalone industrial desktop application. All integrations are hardware protocols or local-network TCP.

## Camera Hardware (GigE/USB SDKs)

**Basler Pylon SDK:**
- Purpose: Acquire frames from Basler GigE / USB3 Vision cameras
- SDK: `Basler.Pylon` v1.1.0 (`bin/x64/Debug/Basler.Pylon.dll`)
- Implementation: `WPF_Example/Device/Camera/Basler/BaslerCamera.cs`, `BaslerCameraProperty.cs`
- Key classes: `Basler.Pylon.Camera`, `PixelDataConverter`
- Connection: Camera identified by UserDefinedName, IP address, or FriendlyName (GigE discovery)
- Config env var: None — discovery is automatic via Pylon runtime

**Hikvision MvCam SDK:**
- Purpose: Acquire frames from Hikvision / MvCam GigE cameras
- SDK: `MvCamCtrl.Net` v4.1.0.3 (`bin/x64/Debug/MvCamCtrl.Net.dll`)
- Implementation: `WPF_Example/Device/Camera/Hik/HikCamera.cs`, `HikCameraProperty.cs`
- Key classes: `MvCamCtrl.NET.CCamera`, `CCameraInfo`
- Connection: Enumerated at runtime via SDK; callback-based grab (`cbOutputExdelegate`)
- Config env var: None — discovery via SDK

**Virtual Camera (Simulation):**
- Purpose: Offline/simulation mode when no hardware is connected
- Implementation: `WPF_Example/Device/Camera/VirtualCamera.cs`, `VirtualCameraProperty.cs`
- Activated by: `#if SIMUL_MODE` compile flag (enabled in Debug builds); loads static image from `D:\1.bmp`

## Light Controllers (Serial RS-232)

**JPF Light Controller:**
- Purpose: Multi-channel LED light intensity control
- Protocol: RS-232 serial (`System.IO.Ports.SerialPort`)
- Command format: `#Oa{n}&` (all channels on/off), `#Aa{nnn}&` (set level)
- Implementation: `WPF_Example/Device/LightController/JPFLightController.cs`
- Config: COM port number and baud rate set in `SystemSetting` / INI

**Pamtekbrand Light Controller:**
- Purpose: Multi-channel LED light intensity/ampere control
- Protocol: RS-232 serial (`System.IO.Ports.SerialPort`)
- Command format: `#I{nnnn}&` (ampere limit), `#O{n}{n}&` (on/off per channel)
- Implementation: `WPF_Example/Device/LightController/PamtekLightController.cs`
- Config: COM port number and baud rate set in `SystemSetting` / INI

**Virtual Light Controller:**
- Purpose: Simulation mode — no hardware required
- Implementation: `WPF_Example/Device/LightController/VirtualLightController.cs`

## Machine Vision Engine

**MVTec HALCON 24.11 Progress Steady:**
- Purpose: Core image processing engine — edge measurement, ROI analysis, calibration
- SDK: `halcondotnet.dll` installed at `C:\Program Files\MVTec\HALCON-24.11-Progress-Steady\bin\dotnet35\`
- Usage areas:
  - Edge measurement: `WPF_Example/Halcon/Algorithms/MeasurementAlgorithm.cs`
  - ROI line intersection: `WPF_Example/Halcon/Algorithms/RoiLineIntersectionAlgorithm.cs`
  - Image bridge (HImage ↔ OpenCvSharp Mat): `WPF_Example/Halcon/HalconImageBridge.cs`
  - Teaching/recipe persistence: `WPF_Example/Halcon/Services/TeachingStorageService.cs`
  - Camera drivers: `BaslerCamera.cs`, `HikCamera.cs` both use `HalconDotNet.HImage`
- Key types used: `HImage`, `HTuple`, `HMeasure` (inferred from algorithm patterns)

**OpenCV (via OpenCvSharp4 v4.8):**
- Purpose: Supplemental image processing — pixel buffer manipulation, Mat conversion, WPF display pipeline
- Implementation: `WPF_Example/Halcon/HalconImageBridge.cs` (HImage→Mat), camera display pipelines
- Used alongside HALCON — not a replacement

## TCP Vision Server (Internal Network IPC)

- Purpose: Receives inspection commands from an external host/PLC/EQ controller over LAN
- Protocol: Custom ASCII TCP over `System.Net.Sockets`
- Port: 2505 (configurable via `SystemSetting.ServerPort`)
- Implementation: `WPF_Example/TcpServer/TcpServer.cs`, `VisionServer.cs`
- Packet types (defined in `WPF_Example/TcpServer/VisionRequestPacket.cs`):
  - `RECIPE` — change active recipe
  - `GET_RECIPE` — query recipe list
  - `SITE_STATUS` — query sequence/site status
  - `LIGHT` — control light channels
  - `TEST` — trigger inspection (Calibration or Inspection, by Site: Top=1, Side=2, Bottom=3)
- Response: `VisionResponsePacket` (`WPF_Example/TcpServer/VisionResponsePacket.cs`)
- Resource mapping: `WPF_Example/Custom/TcpServer/ResourceMap.cs` — maps Site+TestType → Sequence/Action/Camera/Light identifiers
- Events: `MessageEventHandler`, `AlarmEventHandler` (connect/disconnect/timeout/parse failures)

## Barcode / QR Reading

- Purpose: Target ID scanning (wafer ID, barcode on DUT)
- Library: `ZXing.Net` v0.16.9
- Implementation: `WPF_Example/Sequence/Sequence/SequenceBase.cs` (TargetID field); wafer scan actions under `WPF_Example/Custom/Sequence/Wafer/`

## Data Storage

**Databases:**
- None — no SQL, SQLite, or ORM detected

**File Storage (all local filesystem):**
- Recipes: INI files + thumbnails under `D:\Data\Recipe\{RecipeName}\` (path configurable)
- Settings: `Setting.ini` + `Setting.json` in application base directory
- Teaching data (ROI/job): JSON via `DataContractJsonSerializer` (`TeachingStorageService`)
- Account database: `account.db` (JSON, encrypted with AES) in application base directory
- Log files: `.log` files under categorized subdirectories (Trace, Camera, Result, Image, Error, LightController, TcpConnection)
- Calibration data: files under `Calibration/` subdirectory
- Raw image saves: background-queued HImage writes (`WPF_Example/Utility/RawImageSaveService.cs`)

**Caching:**
- None — no in-memory or distributed caching

## Authentication & Identity

**Auth Provider:** Custom, local-only

- Implementation: `WPF_Example/Login/LoginManager.cs`
- Roles: `Admin`, `Engineer` (`EAccountGrade` enum)
- Default accounts: `admin`/`admin`, `operator` (no password)
- Storage: `account.db` JSON file in application directory
- Encryption: AES-128 with hardcoded key derived from a literal password string in `LoginManager.cs`
- No external identity provider (no LDAP, Active Directory, OAuth)

## Monitoring & Observability

**Error Tracking:**
- None — no Sentry, Application Insights, or similar

**Logs:**
- Custom `Logging` utility (`WPF_Example/Utility/Logging.cs`) — file-based, queue-buffered, daily rotation
- Log categories: Trace, Camera, LightController, TcpConnection, Result, Image, Error (defined by `ELogType` enum in `WPF_Example/Setting/SystemSetting.cs`)
- Log files stored in per-type directories under `SystemSetting` configured paths

## CI/CD & Deployment

**Hosting:** Windows desktop machine (on-premise, factory floor)

**CI Pipeline:** Not detected — no GitHub Actions, Azure DevOps, or other CI configuration found

**Distribution:** ClickOnce publishing configured in `.csproj` (`PublishUrl`, `IsWebBootstrapper=false`) but `UpdateEnabled=false`; primary distribution is direct binary copy

## Webhooks & Callbacks

**Incoming:**
- TCP connections from external equipment controller on port 2505 (see TCP Vision Server section)

**Outgoing:**
- None — all communication is server-side response to client-initiated commands; no outgoing webhooks

---

*Integration audit: 2026-04-02*
