# Technology Stack

**Analysis Date:** 2026-04-02

## Languages

**Primary:**
- C# 7.2 - Application logic, device drivers, vision algorithms, UI code-behind

**Secondary:**
- XAML - WPF UI layout and styling (`WPF_Example/UI/**/*.xaml`, `WPF_Example/MainWindow.xaml`)
- Python - Test mock scripts only (`Test/mock_vision_client.py`, `Test/mock_vision_server.py`)

## Runtime

**Environment:**
- .NET Framework 4.8 (CLR v4.0)
- Target platform: x64 (forced in Debug/x64; AnyCPU in Release/AnyCPU)

**Package Manager:**
- NuGet (packages.config format, classic-style — not SDK-style PackageReference)
- Lockfile: `WPF_Example/packages.config` present; `packages/` directory present

## Frameworks

**Core:**
- WPF (Windows Presentation Foundation) - Primary UI framework; MDI window layout via `WPF.MDI` v1.1.1 (local DLL at `bin/x64/Debug/WPF.MDI.dll`)

**Build/Dev:**
- MSBuild 15.0 (`WPF_Example/DatumMeasurement.csproj`)
- Output: `DatumMeasurement.exe` (WinExe, namespace root `ReringProject`)
- Configurations: Debug/AnyCPU, Debug/x64, Release/AnyCPU, Release/x64
- Conditional compile symbol: `SIMUL_MODE` (enabled in Debug builds) — enables offline image simulation paths
- Unsafe code blocks: allowed (`AllowUnsafeBlocks=true`) — used in camera pixel buffer operations

**Testing:**
- No test framework detected (Python mock scripts exist in `Test/` but are standalone; no xUnit/NUnit/MSTest project)

## Key Dependencies

**Vision / Image Processing:**
- `halcondotnet` (HALCON 24.11 Progress Steady) — installed at `C:\Program Files\MVTec\HALCON-24.11-Progress-Steady\bin\dotnet35\halcondotnet.dll`; used throughout `WPF_Example/Halcon/` for HImage, HTuple, edge measurement
- `OpenCvSharp4` v4.8.0 (2023-07-08) — .NET binding for OpenCV 4.8; used in camera drivers and `HalconImageBridge.cs` for HImage↔Mat conversion
- `OpenCvSharp4.Extensions` v4.8.0 — BitmapSource/Bitmap helpers
- `OpenCvSharp4.WpfExtensions` v4.8.0 — WPF BitmapSource output
- `OpenCvSharp4.runtime.win` v4.8.0 — native win64 OpenCV runtime (injected via MSBuild props)

**Camera SDKs (local DLLs — not NuGet):**
- `Basler.Pylon` v1.1.0 — Basler GigE/USB camera SDK (`bin/x64/Debug/Basler.Pylon.dll`); used in `WPF_Example/Device/Camera/Basler/`
- `MvCamCtrl.Net` v4.1.0.3 — Hikvision/MvCam camera SDK (`bin/x64/Debug/MvCamCtrl.Net.dll`); used in `WPF_Example/Device/Camera/Hik/`

**Serialization / Data:**
- `Newtonsoft.Json` v13.0.3 — JSON serialization for settings, recipes, account DB
- `MathNet.Numerics` v5.0.0 — numerical computation (used in wafer scan inspection `Custom/Sequence/Wafer/`)
- `ZXing.Net` v0.16.9 — barcode/QR reading (used in `Sequence/Sequence/SequenceBase.cs` and wafer scan)

**UI Helpers:**
- `PropertyTools` v3.1.0 + `PropertyTools.Wpf` v3.1.0 — property grid with `[Category]`, `[DirectoryPath]`, `[AutoUpdateText]` annotations; drives the Settings window
- `ChartDirector.Net` v7.1.0 + `ChartDirector.Net.Desktop.Controls` v7.1.0 — charting library (used in wafer map views)
- `Ookii.Dialogs.Wpf` v5.0.1 — improved file/folder dialog (used in `UI/Device/DeviceSelector.xaml.cs`)
- `ImageGlass.ImageBox` (local DLL `bin/x64/Debug/dll/x64/`) — image viewer control (used in wafer map UI)
- `System.Drawing.Common` v7.0.0

**Internal / Framework Base:**
- `System.Memory` v4.5.5, `System.Buffers` v4.5.1, `System.Runtime.CompilerServices.Unsafe` v6.0.0 — span/memory primitives
- `System.Numerics.Vectors` v4.5.0, `System.ValueTuple` v4.5.0

**Proprietary Internal Libraries (local DLLs):**
- `ExternalLib/VisionLib/Alligator/Alligator.cs`, `AlligatorDef.cs` — custom vision library (Alligator algo, likely legacy MIL-era wrapper)
- `ExternalLib/VisionLib/AlligatorAlgMil/AlligatorAlgMil.cs`, `AlligatorAlgMilDef.cs` — MIL-based algorithm variant

## Configuration

**Environment / Runtime:**
- `WPF_Example/App.config` — assembly binding redirects only (no app secrets); runtime target `v4.0/.NETFramework,Version=v4.8`
- `WPF_Example/Setting/SystemSetting.cs` — singleton `SystemSetting.Handle`; persists as `Setting.ini` + `Setting.json` in the application base directory
- Key configurable paths (all file-system local, no cloud):
  - `RecipeSavePath` (default `D:\Data\Recipe`)
  - `CalibrationSavePath`, `TraceLogSavePath`, `ImageSavePath`, `ResultSavePath`, `ErrorSavePath`
  - `CameraLogSavePath`, `LightControllerPath`, `TcpConnectionPath`
  - `MapDataLoadPath`, `MapDataSavePath`
- `ServerPort` — TCP vision server port (default 2505)
- `Language` — localization selection (multi-language supported via `LocalizationResource`)

**Build:**
- `WPF_Example/DatumMeasurement.csproj` — MSBuild 15.0 project
- Debug symbols: full in Debug, pdbonly in Release
- Icon: `WPF_Example/Camera_DDA.ico`
- Assembly name: `DatumMeasurement`; root namespace: `ReringProject`

## Platform Requirements

**Development:**
- Windows only (WPF, Win32 P/Invoke in camera drivers, `System.IO.Ports` for serial light controllers)
- HALCON 24.11 Progress Steady must be installed at `C:\Program Files\MVTec\HALCON-24.11-Progress-Steady\`
- Basler Pylon runtime and Hikvision MvCam SDK runtime must be installed/present in `bin/x64/Debug/`
- x64 architecture required for camera SDKs

**Production:**
- Windows x64 desktop application
- Local disk storage for all data (no cloud, no network DB)
- Serial COM ports for light controllers (JPF, Pamtekbrands via `System.IO.Ports.SerialPort`)
- GigE/USB camera hardware (Basler or Hikvision)

---

*Stack analysis: 2026-04-02*
