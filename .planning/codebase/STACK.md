---
last_mapped_commit: f30f7c42
analysis_date: 2026-09-15
---

# 기술 스택

**분석 일시:** 2026-09-15

## 프로그래밍 언어

**주 언어:**
- C# 7.2 — 애플리케이션 로직, 디바이스 드라이버, 비전 알고리즘, UI 코드비하인드
  - LangVersion 설정: `<LangVersion>7.2</LangVersion>` (`WPF_Example/DatumMeasurement.csproj`)
  - Unsafe 코드 블록 허용: `<AllowUnsafeBlocks>true</AllowUnsafeBlocks>` — 카메라 픽셀 버퍼 작업에 사용

**부 언어:**
- XAML — WPF UI 레이아웃 및 스타일링 (`WPF_Example/UI/**/*.xaml`, `WPF_Example/MainWindow.xaml`)
- Python — 테스트 목 스크립트 전용 (`Test/mock_vision_client.py`, `Test/mock_vision_server.py`)

## 런타임 환경

**기본 설정:**
- .NET Framework 4.8 (CLR v4.0)
- 대상 플랫폼: x64 (필수, 카메라 SDK 및 HALCON 런타임이 x64만 지원)
  - Debug/AnyCPU: `<PlatformTarget>x64</PlatformTarget>`
  - Debug/x64: `<PlatformTarget>x64</PlatformTarget>`
  - Release/AnyCPU: `<PlatformTarget>AnyCPU</PlatformTarget>`
  - Release/x64: `<PlatformTarget>x64</PlatformTarget>` (실 운영 대상)

**조건부 컴파일 기호:**
- `SIMUL_MODE` — Debug/AnyCPU 빌드에서 활성화, 오프라인 이미지 시뮬레이션 경로 활성화
  - 실 카메라 없는 개발/테스트 PC 전용 플래그
  - `DefineConstants`: `TRACE;DEBUG;SIMUL_MODE` (Debug/AnyCPU)
  - `DefineConstants`: `TRACE` (Release)

**패키지 매니저:**
- NuGet (Classic/packages.config 방식 — SDK 스타일 아님)
- 패키지 설정: `WPF_Example/packages.config`
- 패키지 폴더: `../packages/` (프로젝트 루트 기준)

## 프레임워크

**UI 프레임워크:**
- Windows Presentation Foundation (WPF) — 기본 UI 프레임워크
- WPF.MDI v1.1.1 (로컬 DLL: `bin/x64/Debug/WPF.MDI.dll`) — MDI 윈도우 레이아웃

**빌드 시스템:**
- MSBuild 15.0 (`WPF_Example/DatumMeasurement.csproj`)
- 출력 형식: `<OutputType>WinExe</OutputType>` (Windows 데스크톱 실행 파일)
- 어셈블리명: `DatumMeasurement`
- 루트 네임스페이스: `ReringProject`

**구성:**
- Debug/AnyCPU: 디버그 기호 전체, SIMUL_MODE 활성화
- Debug/x64: 디버그 기호 전체, 실 하드웨어 용
- Release/AnyCPU: PDB만 (기호 축소), 추적 활성화
- Release/x64: 최적화, PDB만, D:\Data 출력 (실 운영 배포)

**아이콘:**
- 애플리케이션 아이콘: `Camera_DDA.ico`

## 주요 의존성

### 이미지 처리 & 알고리즘
- **halcondotnet** (HALCON 24.11 Progress Steady) — 설치 위치: `C:\Program Files\MVTec\HALCON-24.11-Progress-Steady\bin\dotnet35\halcondotnet.dll`
  - HImage, HTuple, 에지 측정 알고리즘 전반
  - `WPF_Example/Halcon/` 디렉토리 전체에서 사용
  
- **Matrox.MatroxImagingLibrary** (MIL Lite 10.0) — 설치 위치: `C:\Program Files\Matrox Imaging\MIL\MIL.NET\`
  - CXP 카메라 grab 전용 (Phase 41 이후)
  - `Device/Camera/Mil/MilCamera.cs`, `Device/Camera/Mil/MilCameraProperty.cs`
  - 참조: `<Private>True</Private>` 설정으로 출력 폴더로 복사

- **OpenCvSharp4** v4.8.0.20230708 — OpenCV 4.8 .NET 바인딩
  - 패키지: `OpenCvSharp4`, `OpenCvSharp4.Extensions`, `OpenCvSharp4.WpfExtensions`, `OpenCvSharp4.runtime.win`
  - HImage ↔ Mat 변환, 카메라 드라이버에서 사용

### 카메라 SDK
- **Basler.Pylon** v1.1.0 — 바슬러 GigE/USB 카메라
  - 참조: `libs\Basler.Pylon.dll`
  - 구현: `WPF_Example/Device/Camera/Basler/BaslerCamera.cs`, `BaslerCameraProperty.cs`

- **MvCamCtrl.Net** v4.1.0.3 — 하이크비전/MvCam 카메라
  - 참조: `libs\MvCamCtrl.Net.dll`
  - 구현: `WPF_Example/Device/Camera/Hik/HikCamera.cs`, `HikCameraProperty.cs`

### 설정 & 직렬화
- **Newtonsoft.Json** v13.0.3 — JSON 직렬화 (설정, 레시피, 계정 DB)
  - `Setting.json` 저장/로드, 검사 결과 직렬화에 사용

- **PropertyTools** v3.1.0 & **PropertyTools.Wpf** v1.0.0 (로컬 DLL) — WPF 속성 그리드
  - 데이터 주석: `[Category]`, `[DirectoryPath]`, `[AutoUpdateText]`
  - SettingsWindow에서 사용

### 데이터 분석 & 통계
- **MathNet.Numerics** v5.0.0 — 수치 계산 (웨이퍼 스캔 검사에서 사용)
- **ZXing.Net** v0.16.9 — 바코드/QR 코드 읽기 (Sequence/SequenceBase.cs, 웨이퍼 스캔)

### 시각화 & 차트
- **ChartDirector.Net** v7.1.0 & **ChartDirector.Net.Desktop.Controls** v7.1.0 — 차팅 라이브러리
  - 웨이퍼 맵 뷰에 사용
  - 참조: `netchartdir.dll`, `ChartDirector.Net.Desktop.Controls.dll`

- **ImageGlass.ImageBox** (로컬 DLL: `bin/x64/Debug/dll/x64/`) — 이미지 뷰어 컨트롤
  - 웨이퍼 맵 UI에 사용

### Excel 내보내기
- **ClosedXML** v0.105.0 — XLSX 생성 (ReportExportService, CpkReportExportService)
- **ClosedXML.Parser** v2.0.0
- **DocumentFormat.OpenXml** v3.1.1 & **DocumentFormat.OpenXml.Framework** v3.1.1 — OpenXML 형식 지원
- **ExcelNumberFormat** v1.1.0 — 숫자 형식 처리
- **RBush.Signed** v4.0.0 — 공간 인덱싱
- **SixLabors.Fonts** v1.0.0 — 폰트 렌더링

### 유틸리티 & 시스템
- **Ookii.Dialogs.Wpf** v5.0.1 — 개선된 파일/폴더 다이얼로그 (DeviceSelector.xaml.cs)
- **System.Drawing.Common** v7.0.0 — 그래픽 작업
- **System.Memory** v4.5.5 — Span/Memory 원시형
- **System.Buffers** v4.5.1
- **System.Runtime.CompilerServices.Unsafe** v6.0.0
- **System.Numerics.Vectors** v4.5.0
- **System.ValueTuple** v4.5.0
- **Microsoft.Bcl.HashCode** v1.1.1

### 레거시 라이브러리 (프로젝트 폴더에 포함)
- **ExternalLib/VisionLib/Alligator/** — 사용자 정의 비전 라이브러리 (AlligatorAlgMil 기반, 레거시)
  - `Alligator.cs`, `AlligatorDef.cs`
  - `AlligatorAlgMil.cs`, `AlligatorAlgMilDef.cs` — MIL 알고리즘 변형

## 설정

### 런타임 설정
**설정 파일 위치:**
- `Setting.ini` — 애플리케이션 기본 디렉토리 (ini 포맷, 레거시)
- `Setting.json` — 애플리케이션 기본 디렉토리 (JSON 포맷, 신규)
- 참조: `WPF_Example/Setting/SystemSetting.cs` (싱글턴 `SystemSetting.Handle`)

**주요 설정:**
- `ServerPort` (int, 기본값 2505) — TCP 비전 서버 포트
- `UseProtocolV1` (bool, 기본값 false) — v1.0 프로토콜 활성화 플래그 (v2.6 병행 모드)
- `RecipeSavePath` (경로, 기본값 D:\Data\Recipe) — 레시피 저장 위치
- `CalibrationSavePath` (경로, D:\Data\Calibration) — 캘리브레이션 데이터
- `TraceLogSavePath`, `ImageSavePath`, `ResultSavePath` 등 (D:\Data 하위) — 로그/이미지 저장 경로
- `Language` (문자열) — 다국어 지원 선택

### 빌드 설정 (App.config)
**파일:** `WPF_Example/App.config`
- 앱 비밀은 없음 (보안 민감 정보 없음)
- 어셈블리 바인딩 리다이렉트만 포함:
  - System.Runtime.CompilerServices.Unsafe: 0.0.0.0-6.0.0.0 → 6.0.0.0
  - System.Memory: 0.0.0.0-4.0.1.2 → 4.0.1.2
  - System.Numerics.Vectors: 0.0.0.0-4.1.4.0 → 4.1.4.0
  - System.Buffers: 0.0.0.0-4.0.3.0 → 4.0.3.0
  - System.ValueTuple: 0.0.0.0-4.0.3.0 → 4.0.3.0
  - Microsoft.Bcl.HashCode: 0.0.0.0-1.0.0.0 → 1.0.0.0

## 플랫폼 요구사항

### 개발 환경
- Windows 7 이상 (WPF 지원)
- Visual Studio 2015 또는 이상 (MSBuild 15.0+ 호환)
- .NET Framework 4.8 개발자 팩 설치

### 실 운영 환경
- Windows 10/11 x64
- .NET Framework 4.8 런타임
- **필수 외부 설치:**
  - HALCON 24.11 Progress Steady: `C:\Program Files\MVTec\HALCON-24.11-Progress-Steady\`
  - Basler Pylon SDK (선택, Basler 카메라 사용 시)
  - Hikvision MvCam SDK (선택, HIK 카메라 사용 시)
  - Matrox Imaging MIL Lite 10.0 (선택, CXP 카메라 사용 시)

### 하드웨어 연결
- GigE/USB 카메라 (Basler 또는 Hikvision)
- Serial COM 포트 (JPF 또는 Pamtek 조명 컨트롤러용)
- 로컬 디스크 스토리지 (모든 데이터 로컬, 클라우드 미사용)

## 배포 구성

**출력 경로:**
- Debug/AnyCPU: `bin\x64\Debug\DatumMeasurement.exe`
- Debug/x64: `bin\x64\Debug\DatumMeasurement.exe`
- Release/AnyCPU: `bin\Release\DatumMeasurement.exe`
- Release/x64: `D:\Data\DatumMeasurement.exe` (실 운영 배포 폴더)

**필수 동반 파일:**
- `DatumMeasurement.exe.config` (App.config 컴파일된 형태)
- `halcondotnet.dll`, `HalconCpp150.dll` (HALCON 런타임)
- `Basler.Pylon.dll`, `MvCamCtrl.Net.dll`, `Matrox.MatroxImagingLibrary.dll` (카메라 SDK)
- `OpenCvSharp.dll`, `OpenCvSharp.Extensions.dll`, `OpenCvSharp.WpfExtensions.dll` (OpenCV 바인딩)
- 기타 NuGet 의존성 DLL들

---

*스택 분석: 2026-09-15*
