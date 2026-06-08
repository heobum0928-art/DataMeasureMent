# Phase 41: CXP 카메라 MIL Lite 10.0 grab 드라이버 통합 - Pattern Map

**Mapped:** 2026-06-02
**Files analyzed:** 5 (신규 1 + 수정 4)
**Analogs found:** 5 / 5

---

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|---|---|---|---|---|
| `WPF_Example/Device/Camera/Mil/MilCamera.cs` | driver (camera) | request-response (grab→HImage) | `WPF_Example/Device/Camera/Hik/HikCamera.cs` | exact |
| `WPF_Example/Device/Camera/VirtualCamera.cs` | base class / enum | — | self (enum 확장) | exact |
| `WPF_Example/Device/DeviceHandler.cs` | factory / switch | request-response | self (case 추가) | exact |
| `WPF_Example/Custom/Device/DeviceHandler.cs` | config / registration | — | self (재구성) | exact |
| `WPF_Example/DatumMeasurement.csproj` | config (build) | — | `MvCamCtrl.Net` / `halcondotnet` HintPath 블록 | exact |

---

## Pattern Assignments

### `WPF_Example/Device/Camera/Mil/MilCamera.cs` (신규 드라이버, request-response)

**Analog:** `WPF_Example/Device/Camera/Hik/HikCamera.cs`

**Imports pattern** (HikCamera.cs lines 1–16):
```csharp
using HalconDotNet;
using ReringProject.Halcon;
using ReringProject.Setting;
using ReringProject.Utility;
using System;
using System.Runtime.InteropServices;
using System.Threading;
// MilCamera 에서는 MvCamCtrl.NET 대신:
using Matrox.MatroxImagingLibrary;
```

**클래스 선언 패턴** (HikCamera.cs line 25):
```csharp
// HikCamera 선례:
public partial class HikCamera : VirtualCamera, IDisposable {

// MilCamera 동일 구조:
public class MilCamera : VirtualCamera, IDisposable {
```

**생성자 시그니처 패턴** (HikCamera.cs lines 273–279):
```csharp
// HikCamera 생성자 — MilCamera 가 복사할 시그니처
public HikCamera(DisplayConfig config, DeviceInfo info) : base(config, info, ECameraType.HIK) {
    Properties = new HikCameraProperty(this);
    // ...
}

// MilCamera 대응:
public MilCamera(DisplayConfig config, DeviceInfo info) : base(config, info, ECameraType.MIL) {
    Properties = new VirtualCameraProperty();
    Properties.Width  = info.Width;
    Properties.Height = info.Height;
}
```

**소멸자 + Dispose 패턴** (HikCamera.cs lines 281–290):
```csharp
~HikCamera() {
    Dispose();
}
public void Dispose() {
    Close();
}
```

**Open() — MIL 리소스 1회 할당 패턴**
RESEARCH.md Pattern 1 기준. HikCamera.Open() lines 292–416 구조를 참고하여 아래로 변환:
```csharp
// [VERIFIED: 로컬 MIL 예제 MdigGrab.cs + RESEARCH.md Pattern 1]
public override bool Open(params object[] param) {
    try {
        // 1. Application
        MIL.MappAlloc(MIL.M_DEFAULT, ref MilApplication);
        if (MilApplication == MIL.M_NULL) throw new Exception("MappAlloc failed");

        // 2. System  (SIMUL_MODE → M_SYSTEM_HOST, HW → M_SYSTEM_DEFAULT)
#if SIMUL_MODE
        MIL.MsysAlloc(MIL.M_DEFAULT, MIL.M_SYSTEM_HOST,
                      MIL.M_DEFAULT, MIL.M_DEFAULT, ref MilSystem);
#else
        MIL.MsysAlloc(MIL.M_DEFAULT, MIL.M_SYSTEM_DEFAULT,
                      MIL.M_DEFAULT, MIL.M_DEFAULT, ref MilSystem);
#endif
        if (MilSystem == MIL.M_NULL) throw new Exception("MsysAlloc failed");

        // 3. Digitizer (DCF 미사용 — M_DEFAULT)
        MIL.MdigAlloc(MilSystem, MIL.M_DEV0, "M_DEFAULT",
                      MIL.M_DEFAULT, ref MilDigitizer);
        if (MilDigitizer == MIL.M_NULL) throw new Exception("MdigAlloc failed");

        // 4. Buffer (Mono8, 1회 할당)
        MIL.MbufAlloc2d(MilSystem, Info.Width, Info.Height,
                        8 + MIL.M_UNSIGNED,
                        MIL.M_IMAGE + MIL.M_GRAB + MIL.M_PROC,
                        ref MilBuffer);
        if (MilBuffer == MIL.M_NULL) throw new Exception("MbufAlloc2d failed");

        // 회전 적용 시 width/height 교환 (HikCamera.Open L404 동일)
        if (Info.RotateAngle == ERotateAngleType._90 ||
            Info.RotateAngle == ERotateAngleType._270) {
            int tmp = Properties.Height;
            Properties.Height = Properties.Width;
            Properties.Width = tmp;
        }
        return true;
    }
    catch (Exception e) {
        Logging.PrintLog((int)ELogType.Camera, "[ERROR] {0} MilCamera.Open ({1})", Info.Identifier, e.Message);
        return false;
    }
}
```

**Close() — MIL 리소스 역순 해제 패턴**
```csharp
// HikCamera.Close() lines 419–427 구조 대응
public override void Close() {
    // MIL 역순 해제: Buffer → Digitizer → System → Application
    if (MilBuffer     != MIL.M_NULL) { MIL.MbufFree(MilBuffer);      MilBuffer     = MIL.M_NULL; }
    if (MilDigitizer  != MIL.M_NULL) { MIL.MdigFree(MilDigitizer);   MilDigitizer  = MIL.M_NULL; }
    if (MilSystem     != MIL.M_NULL) { MIL.MsysFree(MilSystem);      MilSystem     = MIL.M_NULL; }
    if (MilApplication!= MIL.M_NULL) { MIL.MappFree(MilApplication); MilApplication= MIL.M_NULL; }
}
```

**GrabHalconImage() — 핵심 grab → HImage 변환 패턴**
HikCamera.cs lines 449–486 (`OnGrabResult`)의 `GenImage1("byte", w, h, pData)` 패턴을 동기 MdigGrab으로 변환:
```csharp
// [VERIFIED: HikCamera.OnGrabResult L455-486 + MIL 예제 MbufPointerAccess.cs]
public override HImage GrabHalconImage() {
#if SIMUL_MODE
    return LastHalconImage;  // VirtualCamera.GetCurrentImageNoLock() 경로
#endif
    try {
        // 소프트웨어 트리거 단발 grab (동기 블로킹)
        MIL.MdigGrab(MilDigitizer, MilBuffer);

        // host 포인터 획득 (MbufPointerAccess 예제 패턴)
        MIL_INT hostPtr   = MIL.M_NULL;
        MIL_INT pitchByte = MIL.M_NULL;
        MIL.MbufControl(MilBuffer, MIL.M_LOCK, MIL.M_DEFAULT);
        MIL.MbufInquire(MilBuffer, MIL.M_HOST_ADDRESS, ref hostPtr);
        MIL.MbufInquire(MilBuffer, MIL.M_PITCH_BYTE,   ref pitchByte);

        if (hostPtr == MIL.M_NULL) {
            MIL.MbufControl(MilBuffer, MIL.M_UNLOCK, MIL.M_DEFAULT);
            return null;
        }

        unsafe {
            IntPtr ptr = new IntPtr((long)hostPtr); // Pitfall 3: 명시적 변환
            HImage sourceImage = new HImage();

            if (pitchByte == Info.Width) {
                // pitch == width → padding 없음 → GenImage1 직접 사용
                // HikCamera.OnGrabResult L455-456 완전 동일 패턴
                sourceImage.GenImage1("byte", Info.Width, Info.Height, ptr);
            }
            else {
                // pitch > width → padding 존재 → 행 단위 복사
                sourceImage = CreateImageFromPaddedBuffer(ptr, Info.Width, Info.Height, (int)pitchByte);
            }
            MIL.MbufControl(MilBuffer, MIL.M_UNLOCK, MIL.M_DEFAULT);

            // 회전 처리 (HikCamera.OnGrabResult L458-470 동일)
            HImage rotatedImage = sourceImage;
            if (Info.RotateAngle == ERotateAngleType._90) {
                rotatedImage = sourceImage.RotateImage(90.0, "constant");
                sourceImage.Dispose();
            }
            else if (Info.RotateAngle == ERotateAngleType._180) {
                rotatedImage = sourceImage.RotateImage(180.0, "constant");
                sourceImage.Dispose();
            }
            else if (Info.RotateAngle == ERotateAngleType._270) {
                rotatedImage = sourceImage.RotateImage(270.0, "constant");
                sourceImage.Dispose();
            }

            lock (Interlock) {
                // HikCamera.OnGrabResult L472-473 동일 패턴
                LastGrabHalconImage?.Dispose();
                LastGrabHalconImage = rotatedImage;
            }
            Interlocked.Increment(ref imageCount);
            return LastHalconImage; // VirtualCamera.LastHalconImage → CopyImage()
        }
    }
    catch (Exception e) {
        Logging.PrintLog((int)ELogType.Camera, "[ERROR] {0} MilCamera.GrabHalconImage ({1})", Name, e.Message);
        Interlocked.Increment(ref errorCount);
        return null;
    }
}
```

**SetSoftwareTriggerMode / SetTriggerMode 패턴** (HikCamera.cs lines 530–578):
```csharp
// MIL 소프트웨어 트리거 단발 모드 — MdigGrab 자체가 동기이므로
// HIK처럼 StartGrabbing/StopGrabbing 루프가 불필요.
// CaptureMode / TriggerSource 상태만 갱신.
public override bool SetSoftwareTriggerMode(bool threading = false) {
    CaptureMode  = ECaptureModeType.Trigger;
    TriggerSource = ETriggerSource.Software;
    return true;
}

public override bool SetTriggerMode(ETriggerSource source, bool forcing = false, bool threading = false) {
    CaptureMode  = ECaptureModeType.Trigger;
    TriggerSource = source;
    return true;
}
```

**에러 처리 패턴** (CLAUDE.md + HikCamera.cs 전반):
```csharp
// 모든 public 메서드: try { } catch (Exception e) { Logging.PrintLog; return false/null; }
// Halcon 호출 실패 → catch swallow 허용 (CLAUDE.md 에러 처리 정책)
// MIL 객체 null 체크 전 → M_NULL 비교
```

---

### `WPF_Example/Device/Camera/VirtualCamera.cs` (enum 확장)

**Analog:** self

**ECameraType enum 확장 패턴** (VirtualCamera.cs lines 18–22):
```csharp
// 현재
public enum ECameraType {
    Virtual,
    Basler,
    HIK,
}

// Phase 41 수정 — MIL 추가
public enum ECameraType {
    Virtual,
    Basler,
    HIK,
    MIL,   // 260602 hbk — CXP 카메라 MIL Lite 10.0
}
```

수정 범위는 이 4줄 enum 정의뿐. 다른 메서드 변경 없음.

---

### `WPF_Example/Device/DeviceHandler.cs` (factory switch case 추가)

**Analog:** self — 기존 `case ECameraType.HIK` 블록 (lines 167–219)

**switch case 추가 패턴** (DeviceHandler.cs lines 167–219에 바로 이어서):
```csharp
// 기존 HIK case (lines 167-219) 구조를 그대로 복사 후 MIL 로 변환
case ECameraType.MIL: {
    // MIL 은 enumerate 단계 없음 — Open() 내에서 MsysAlloc/MdigAlloc 로 직접 연결
    MilCamera newCam = new MilCamera(Config, id);
    if (!newCam.Open()) {
        result &= ~EInitializeResult.Success;
        result |= EInitializeResult.OpenFail;
#if SIMUL_MODE
        AddVirtualCamera(id);
#endif
        continue;
    }
    Devices.Add(id.Identifier, newCam);
}
break;
```

**MIL enumerate 불필요 이유:**
HIK는 `HikCamera.EnumerateDevice()` → DeviceList 조회 → ContainsDevice 체크 흐름이지만,
MIL은 MdigAlloc()이 직접 보드/채널에 연결하므로 사전 enumerate 단계가 없음.
Open() 성공/실패로만 판별 (RESEARCH.md "Primary recommendation" 참조).

---

### `WPF_Example/Custom/Device/DeviceHandler.cs` (RegisterRequiredDevices 재구성)

**Analog:** self (현재 lines 86–121)

**현재 코드** (Custom/DeviceHandler.cs lines 86–121):
```csharp
// 현재: HIK 3대 고정 등록
private void RegisterRequiredDevices() {
    SetRequiredDevice(ECameraType.HIK, ECaptureImageType.Gray8, ETriggerSource.Software,
        CAMERA_TOP,    WIDTH_TOP,    HEIGHT_TOP,    REVERSE_X_TOP,    REVERSE_Y_TOP,    ROTATE_TOP);
    SetRequiredDevice(ECameraType.HIK, ECaptureImageType.Gray8, ETriggerSource.Software,
        CAMERA_SIDE,   WIDTH_SIDE,   HEIGHT_SIDE,   REVERSE_X_SIDE,   REVERSE_Y_SIDE,   ROTATE_SIDE);
    SetRequiredDevice(ECameraType.HIK, ECaptureImageType.Gray8, ETriggerSource.Hardware_Line0,
        CAMERA_BOTTOM, WIDTH_BOTTOM, HEIGHT_BOTTOM, REVERSE_X_BOTTOM, REVERSE_Y_BOTTOM, ROTATE_BOTTOM);
}
```

**Phase 41 재구성 패턴** (RESEARCH.md Pattern 3 기준):
```csharp
// 신규 상수 (Custom/DeviceHandler.cs 상단에 추가)
// 260602 hbk — CXP ViewWorks 128MP 해상도 (실물 도착 후 확정)
public const int WIDTH_CXP  = 14192;  // TBD: MdigInquire M_SIZE_X 확인 후 채움
public const int HEIGHT_CXP = 10640;  // TBD: MdigInquire M_SIZE_Y 확인 후 채움

private void RegisterRequiredDevices() {
    // D-03: PC별 역할 분기 — SystemSetting INI 기반
    // [ASSUMED] SystemSetting.CameraRole 프로퍼티 추가 필요
    // 선례: SystemSetting.Handle.ServerPort (INI 자동 직렬화, SystemSetting.cs L36)
    ECameraRole role = SystemSetting.Handle.CameraRole;

    if (role == ECameraRole.TopBottom) {
        // PC1: 카메라 1대 — Top + Bottom 시퀀스 담당
        SetRequiredDevice(ECameraType.MIL, ECaptureImageType.Gray8, ETriggerSource.Software,
            CAMERA_TOP, WIDTH_CXP, HEIGHT_CXP, false, false);
        // Bottom 시퀀스가 동일 물리 카메라 공유 시 CAMERA_TOP 이름 재사용,
        // 또는 CAMERA_BOTTOM 별도 등록 후 MilCamera 인스턴스 1개를 양쪽에 map — plan 에서 결정
    }
    else { // ECameraRole.Side
        // PC2: 카메라 1대 — Side 시퀀스 담당
        SetRequiredDevice(ECameraType.MIL, ECaptureImageType.Gray8, ETriggerSource.Software,
            CAMERA_SIDE, WIDTH_CXP, HEIGHT_CXP, false, false);
    }
}
```

**ECameraRole enum (신규, Custom/DeviceHandler.cs 상단 또는 Define/ID.cs)**:
```csharp
// 선례: ESequence, EAction (Define/ID.cs) — E 접두사 + PascalCase
public enum ECameraRole {
    TopBottom,  // PC1: Top + Bottom 시퀀스
    Side,       // PC2: Side 시퀀스
}
```

**SystemSetting 프로퍼티 추가 선례** (SystemSetting.cs lines 35–36):
```csharp
// 기존 선례 — INI 자동 직렬화
[Category("Connection|Server")]
public int ServerPort { get; set; } = 2505;

// CameraRole 추가 패턴 (Custom/SystemSetting.cs에 추가):
[Category("System|Camera")]
public ECameraRole CameraRole { get; set; } = ECameraRole.TopBottom;
```

---

### `WPF_Example/DatumMeasurement.csproj` (MIL DLL 참조 추가)

**Analog:** `MvCamCtrl.Net` / `halcondotnet` 참조 블록

**기존 로컬 DLL HintPath 참조 패턴** (.csproj lines 99–105, 203–205):
```xml
<!-- HIK SDK (bin/x64/Debug/ 로컬 DLL) -->
<Reference Include="MvCamCtrl.Net, Version=4.1.0.3, Culture=neutral, PublicKeyToken=52fddfb3f94be800, processorArchitecture=AMD64">
  <SpecificVersion>False</SpecificVersion>
  <HintPath>bin\x64\Debug\MvCamCtrl.Net.dll</HintPath>
</Reference>

<!-- HALCON (절대 경로 로컬 DLL) -->
<Reference Include="halcondotnet">
  <HintPath>C:\Program Files\MVTec\HALCON-24.11-Progress-Steady\bin\dotnet35\halcondotnet.dll</HintPath>
</Reference>
```

**MIL DLL 추가 패턴** (RESEARCH.md Standard Stack 기준):
```xml
<!-- 260602 hbk — Matrox MIL Lite 10.0 .NET binding -->
<Reference Include="Matrox.MatroxImagingLibrary">
  <HintPath>C:\Program Files\Matrox Imaging\MIL\MIL.NET\Matrox.MatroxImagingLibrary.dll</HintPath>
  <Private>False</Private>
</Reference>
```

`Private=False` 는 런타임 DLL을 출력 디렉토리에 복사하지 않도록 설정 (MIL 런타임은 PC에 설치되어 있으므로).
halcondotnet과 동일한 절대 경로 HintPath 패턴 사용.

**Compile Include 추가 패턴** (.csproj lines 263–266 구조 참조):
```xml
<!-- 기존 선례 -->
<Compile Include="Device\Camera\Basler\BaslerCamera.cs" />
<Compile Include="Device\Camera\Hik\HikCamera.cs" />

<!-- 신규 MilCamera -->
<Compile Include="Device\Camera\Mil\MilCamera.cs" />
```

---

## Shared Patterns

### SIMUL_MODE 폴백
**Source:** `WPF_Example/Device/Camera/VirtualCamera.cs` lines 215–246, DeviceHandler.cs lines 120–130
**Apply to:** `MilCamera.GrabHalconImage()`, `MilCamera.Open()`

```csharp
// VirtualCamera.GetCurrentImageNoLock() SIMUL 분기 (lines 215-246)
// → MilCamera.GrabHalconImage() 에서 #if SIMUL_MODE 분기로 동일 처리:
#if SIMUL_MODE
    return LastHalconImage; // VirtualCamera.BackgroundImagePath 파일 grab 경로
#endif

// DeviceHandler.Initialize() SIMUL 폴백 (lines 120-130):
#if SIMUL_MODE
    AddVirtualCamera(id); // Open 실패 시 VirtualCamera 인스턴스로 대체
#endif
```

### 에러 처리 / 로깅
**Source:** `WPF_Example/Device/Camera/Hik/HikCamera.cs` 전반 (lines 412–414, 478–480, 604–607)
**Apply to:** `MilCamera` 모든 public 메서드

```csharp
// try/catch 구조 — CLAUDE.md 에러 처리 정책
catch (Exception e) {
    Logging.PrintLog((int)ELogType.Camera, "[ERROR] {0} <메서드명> ({1})", Name, e.Message);
    return false; // 또는 null
}
```

### HImage GenImage1 변환
**Source:** `WPF_Example/Device/Camera/Hik/HikCamera.cs` lines 455–456
**Apply to:** `MilCamera.GrabHalconImage()` 실 HW 경로

```csharp
// HIK의 콜백 → MIL의 동기 MdigGrab 후 동일 패턴
HImage sourceImage = new HImage();
sourceImage.GenImage1("byte", (int)width, (int)height, pData); // pData = IntPtr
```

### lock(Interlock) 이미지 버퍼 보호
**Source:** `WPF_Example/Device/Camera/Hik/HikCamera.cs` lines 454, 472–473
**Apply to:** `MilCamera.GrabHalconImage()` 이미지 할당/치환 구간

```csharp
// HikCamera.OnGrabResult 내 lock 패턴 — MilCamera도 동일 적용
lock (Interlock) {
    LastGrabHalconImage?.Dispose();
    LastGrabHalconImage = rotatedImage;
}
```

### 회전 처리 (RotateImage)
**Source:** `WPF_Example/Device/Camera/Hik/HikCamera.cs` lines 458–470
**Apply to:** `MilCamera.GrabHalconImage()`

```csharp
HImage rotatedImage = sourceImage;
if (Info.RotateAngle == ERotateAngleType._90) {
    rotatedImage = sourceImage.RotateImage(90.0, "constant");
    sourceImage.Dispose();
}
else if (Info.RotateAngle == ERotateAngleType._180) {
    rotatedImage = sourceImage.RotateImage(180.0, "constant");
    sourceImage.Dispose();
}
else if (Info.RotateAngle == ERotateAngleType._270) {
    rotatedImage = sourceImage.RotateImage(270.0, "constant");
    sourceImage.Dispose();
}
```

---

## No Analog Found

없음 — 모든 파일에 대해 코드베이스 내 직접 analog 발견.

---

## Critical Pitfalls (RESEARCH.md 요약 → planner 참조용)

| 항목 | 위험 | 처리 방법 |
|---|---|---|
| MIL_INT → IntPtr 변환 | CS0030 컴파일 오류 | `new IntPtr((long)hostPtr)` 명시적 변환 사용 |
| M_HOST_ADDRESS = M_NULL | GenImage1 불가 | MbufControl(M_LOCK) 후 null 체크 필수 |
| MIL 해제 순서 역전 | invalid handle | Buffer → Digitizer → System → Application 순서 엄수 |
| Open 시 M_SYSTEM_DEFAULT 실패 | 보드 드라이버 미설치 시 MsysAlloc 실패 | SIMUL_MODE에서 M_SYSTEM_HOST 사용, 실 HW Open 실패 시 AddVirtualCamera 폴백 |
| 매 grab마다 버퍼 재할당 | 성능 저하 | Open()에서 1회 MbufAlloc2d, GrabHalconImage에서 재사용 |
| unsafe 블록 누락 | IntPtr→byte* 캐스팅 불가 | `AllowUnsafeBlocks=true` 이미 설정됨 (CLAUDE.md) |

---

## Metadata

**Analog search scope:** `WPF_Example/Device/`, `WPF_Example/Custom/Device/`, `WPF_Example/Setting/`
**Files scanned:** 6 (HikCamera.cs, VirtualCamera.cs, BaslerCamera.cs, DeviceHandler.cs, Custom/DeviceHandler.cs, SystemSetting.cs)
**Pattern extraction date:** 2026-06-02
