# Phase 58: Config & Camera (A) - Pattern Map

**Mapped:** 2026-06-23
**Files analyzed:** 4 new/modified files
**Analogs found:** 4 / 4

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|---|---|---|---|---|
| `WPF_Example/Custom/EthernetVision/EEthernetVisionMode.cs` | model (enum) | — | `WPF_Example/Custom/Sequence/Inspection/EImageSource.cs` + `WPF_Example/Custom/Define/ID.cs` | exact |
| `WPF_Example/Custom/EthernetVision/EthernetAlignCamera.cs` | device wrapper | request-response (grab) | `WPF_Example/Device/Camera/Hik/HikCamera.cs` + `WPF_Example/Device/Camera/VirtualCamera.cs` | exact |
| `WPF_Example/Custom/SystemSetting.cs` (수정) | config | CRUD (INI) | `WPF_Example/Custom/SystemSetting.cs` 기존 Phase 48 블록 | exact (자기 자신 확장) |
| `WPF_Example/Custom/EthernetVision/EthernetVisionHandler.cs` | singleton handler | request-response | `WPF_Example/SystemHandler.cs` + `WPF_Example/Device/DeviceHandler.cs` | role-match |

---

## Pattern Assignments

### `WPF_Example/Custom/EthernetVision/EEthernetVisionMode.cs` (model, —)

**Analogs:**
- `WPF_Example/Custom/Sequence/Inspection/EImageSource.cs` — 단독 파일 E-접두 enum 레이아웃
- `WPF_Example/Custom/Define/ID.cs` — namespace + XML doc 패턴
- `WPF_Example/Custom/SystemSetting.cs` lines 9-13 — 같은 파일 내 enum + `[Category]` int 백킹 선례

**Enum 파일 레이아웃 패턴** (`EImageSource.cs` 전체, 9줄):
```csharp
namespace ReringProject.Sequence
{
    public enum EImageSource
    {
        Horizontal = 0,  // 가로축 (TeachingImagePath). 기본값.
        Vertical   = 1   // 세로축 (TeachingImagePath_Vertical, DualImage 전용).
    }
}
```

**E-접두 enum + 문서화 패턴** (`ID.cs` lines 7-16):
```csharp
namespace ReringProject.Define {

    /// <summary>
    /// 시퀀스의 ID(쓰레드 단위 = 카메라)
    /// </summary>
    public enum ESequence : int {
        Top = 1,
        Side = 2,
        Bottom = 3,
    }
```

**적용 규칙:**
- Allman brace style (신규 Custom 파일 — `EImageSource.cs` 참조)
- namespace `ReringProject.Define` 또는 `ReringProject.Setting` (enum 이 Setting 영역에 속하면 Setting 네임스페이스; Define 에 두면 ID.cs 패턴)
- 권장: `WPF_Example/Custom/EthernetVision/EEthernetVisionMode.cs` 에 단독 배치, namespace `ReringProject.Setting`(SystemSetting 과 같은 어셈블리)
- `None = 0` 이 기본값 — int 백킹 없이 enum 직접; INI 직렬화는 `Custom/SystemSetting.cs` 에서 int 프로퍼티로 노출(아래 참조)

---

### `WPF_Example/Custom/EthernetVision/EthernetAlignCamera.cs` (device wrapper, request-response)

**주 analog:** `WPF_Example/Device/Camera/Hik/HikCamera.cs`
**보조 analog:** `WPF_Example/Device/Camera/VirtualCamera.cs`

**생성자 패턴** (`HikCamera.cs` lines 273-279):
```csharp
public HikCamera(DisplayConfig config, DeviceInfo info) : base(config, info, ECameraType.HIK) {
    Properties = new HikCameraProperty(this);

    mStopwatch = new Stopwatch();
    double frametime = 1 / RENDERFPS;
    FrameDurationTicks = (int)(Stopwatch.Frequency * frametime);
}
```

**`EthernetAlignCamera` 는 `HikCamera` 를 상속하지 않고 composition 보유** (D-01). 따라서 생성자는 내부적으로 `DeviceInfo` + `DisplayConfig` 를 직접 구성하여 `HikCamera` 인스턴스를 생성한다.

**Open(IP string) 패턴** (`HikCamera.cs` lines 292-304):
```csharp
public override bool Open(params object[] param) {
    string camIpOrName = null;
    if ((param != null) && (param.Length > 0)) {
        camIpOrName = param[0] as string;
    }
    int index = GetDeviceIndex(camIpOrName);
    if (index < 0) return false;

    CCameraInfo info = GetDeviceInfo(index);
    if (info == null) return false;

    return Open(info, index);
}
```

**Open(CCameraInfo) 핵심 — CreateHandle/OpenDevice + try-catch** (`HikCamera.cs` lines 306-417, 요약):
```csharp
private bool Open(CCameraInfo info, int id) {
    int nRet = 0;
    try {
        this.CameraHandle = new CCamera();
        this.CameraInfo = info;
        this.ID = id;

        nRet = this.CameraHandle.CreateHandle(ref info);
        if (nRet != CErrorDefine.MV_OK) {
            throw new Exception(string.Format("{0} Handle Create Fail!", GetDeviceName(ref info)));
        }
        nRet = this.CameraHandle.OpenDevice();
        if (nRet != CErrorDefine.MV_OK) {
            throw new Exception(string.Format("{0} Open Device Fail!", GetDeviceName(ref info)));
        }
        // ... GigE 패킷사이즈, PixelFormat, Width, Height 설정 ...
        // ... Callback 등록 (ExceptionCallback, GrabCallback, EventCallback) ...
        CaptureMode = ECaptureModeType.Stop;
    }
    catch (Exception e) {
        Logging.PrintLog((int)ELogType.Camera, "[ERROR] {0} Open Fail. ({1})", Info.Identifier, e.Message);
        return false;
    }
    return true;
}
```

**Close 패턴** (`HikCamera.cs` lines 419-427):
```csharp
public override void Close() {
    if (CameraHandle != null) {
        StopStream();
        if (CameraHandle.IsDeviceConnected()) {
            CameraHandle.CloseDevice();
            CameraHandle.DestroyHandle();
        }
    }
}
```

**GrabHalconImage 패턴** (`HikCamera.cs` lines 493-519):
```csharp
public override HImage GrabHalconImage() {
    if (CaptureMode == ECaptureModeType.Streaming) return null;

    GrabState = EGrabStateType.Grabbing;
    mStopwatch.Restart();

    if (!SetSoftwareTriggerMode()) return null;

    prevImageCount = imageCount;
    ExecuteSoftwareTrigger();

    while (true) {
        if (GrabState == EGrabStateType.Done) { break; }
        else if (GrabState == EGrabStateType.Fail) {
            Logging.PrintLog((int)ELogType.Camera, "[ERROR] {0} Grab Image (software trigger mode), {1}", Name, GrabState.ToString());
            Interlocked.Increment(ref errorCount);
            break;
        }
        else if (mStopwatch.ElapsedMilliseconds >= pConfig.GrabTimeOut) return null;
        Thread.Sleep(1);
    }

    return LastHalconImage;
}
```

**SIMUL 폴백 패턴** (`VirtualCamera.cs` lines 219-244):
```csharp
protected virtual HImage LoadBackgroundImage(string imageFilePath) {
    HImage loadedImage = new HImage();
    loadedImage.ReadImage(imageFilePath);

    HImage normalizedImage = loadedImage;
    if ((Info.ImageType == ECaptureImageType.Gray8) && (normalizedImage.CountChannels().I > 1)) {
        HImage grayImage = normalizedImage.Rgb1ToGray();
        normalizedImage.Dispose();
        normalizedImage = grayImage;
    }

    normalizedImage.GetImageSize(out HTuple width, out HTuple height);
#if SIMUL_MODE
    //260317 keep offline source resolution in simulation mode
    Properties.Width = width.I;
    Properties.Height = height.I;
    return normalizedImage;
#endif
    // ...
}
```

**BackgroundImagePath setter 패턴** (`VirtualCamera.cs` lines 111-134):
```csharp
private string _BackgroundImagePath;
public string BackgroundImagePath {
    get {
        return _BackgroundImagePath;
    }
    set {
        if (value != _BackgroundImagePath) {
            _BackgroundImagePath = value;
            BackgroundImageFileList.Clear();
            if (_BackgroundImagePath == null) return;

            string[] extensions = { ".bmp", ".jpg", ".jpeg", ".png", ".tiff" };
            if (File.Exists(_BackgroundImagePath) && extensions.Any(ext => ext.Equals(
                    Path.GetExtension(_BackgroundImagePath), StringComparison.OrdinalIgnoreCase))) {
                BackgroundImageFileList.Add(_BackgroundImagePath);
            }
            // ...
        }
    }
}
```

**DeviceHandler SIMUL const 참조** (`DeviceHandler.cs` lines 58-61):
```csharp
#if SIMUL_MODE
    //260317 offline auto-run test image
    private const string SimulatedImagePath = @"D:\1.bmp";
#endif
```
Phase 58 에서는 `D:\align_test.bmp` 를 동일 패턴으로 `EthernetAlignCamera` 내부 상수로 선언한다.

**EnumerateDevice 패턴** — `EthernetAlignCamera.Connect()` 내부에서 `HikCamera.EnumerateDevice()` 를 호출한 뒤 IP 로 인덱스를 찾아 내부 `HikCamera` 인스턴스의 `Open(IP)` 를 위임한다 (`HikCamera.cs` lines 155-217 의 정적 메서드 흐름 참조).

**적용 규칙:**
- K&R brace style (HikCamera.cs 와 VirtualCamera.cs 가 K&R — 이 파일을 직접 랩하므로 동일 스타일 유지)
- `IsOpen` bool 프로퍼티 — `VirtualCamera.IsOpen` 선례
- SIMUL 폴백: `#if SIMUL_MODE` 블록 내 `D:\align_test.bmp` 상수
- `HImage` 는 `using` 또는 명시적 `Dispose()` 필수 (CLAUDE.md error handling 규칙)
- namespace `ReringProject.Device`

---

### `WPF_Example/Custom/SystemSetting.cs` (config, CRUD/INI, 수정)

**Analog:** `WPF_Example/Custom/SystemSetting.cs` 기존 Phase 48 블록 (lines 1-52 전체)

**`[Category]` 프로퍼티 + int 백킹 패턴** (lines 19-27):
```csharp
[Category("System|Camera")]
public int CameraRoleValue { get; set; } = 0;   // 0 = TopBottom (기본값)

[Browsable(false)]
public ECameraRole CameraRole {
    get { return (ECameraRole)CameraRoleValue; }
    set { CameraRoleValue = (int)value; }
}
```

**INI 로드 자동화 원리** (`Setting/SystemSetting.cs` Load() lines 197-275):
- `[Category("ETHERNET_VISION")]` 어트리뷰트 부여 시 `Load()` 의 reflection 루프가 자동으로 그룹="ETHERNET_VISION", 키=프로퍼티명 으로 INI read/write
- `Double` 타입은 `loadFile[group][name].ToDouble()` → `prop.SetValue(this, dValue)` 로 처리
- **누락 키는 0/false/null 로 덮어씀** → `AfterLoad()` 에서 방어 필수 (reference_parambase_missing_key_zeroes_default.md)

**AfterLoad + RestoreXxxDefault 패턴** (`Custom/SystemSetting.cs` lines 35-51):
```csharp
partial void AfterLoad()
{
    RestorePcRoleDefault();
}

// PROTO-01: PcRole==0(구 INI 누락 로드) 이면 PC1 기본값(=1) 으로 복원.
private void RestorePcRoleDefault()
{
    bool bPcRoleMissing = PcRole == 0;
    if (bPcRoleMissing)
    {
        PcRole = PC_ROLE_DEFAULT;
    }
}
```

**Phase 58 적용 — `AfterLoad()` 확장 형태:**
```csharp
partial void AfterLoad()
{
    RestorePcRoleDefault();
    RestoreEthernetVisionDefault();  // 추가
}

private void RestoreEthernetVisionDefault()
{
    bool bPixelResolutionMissing = EthernetPixelResolution <= 0.0;
    if (bPixelResolutionMissing)
    {
        EthernetPixelResolution = ETHERNET_PIXEL_RESOLUTION_DEFAULT;
    }
}
```

**신규 프로퍼티 선언 형태:**
```csharp
// 260623 hbk Phase 58
[Category("ETHERNET_VISION")]
public int EthernetVisionModeValue { get; set; } = 0;   // 0 = None

[Browsable(false)]
public EEthernetVisionMode EthernetVisionMode {
    get { return (EEthernetVisionMode)EthernetVisionModeValue; }
    set { EthernetVisionModeValue = (int)value; }
}

[Category("ETHERNET_VISION")]
public string EthernetCameraIp { get; set; } = "192.168.1.100";

[Category("ETHERNET_VISION")]
public double EthernetExposure { get; set; } = 10000.0;

[Category("ETHERNET_VISION")]
public double EthernetPixelResolution { get; set; } = 8.652;
```

**상수:**
```csharp
private const double ETHERNET_PIXEL_RESOLUTION_DEFAULT = 8.652;
```

**적용 규칙:**
- Allman brace style (기존 Phase 48 블록 참조)
- 헝가리언: `bool bXxxMissing` (기존 `bPcRoleMissing` 선례)
- 매직넘버 const 필수 (`PC_ROLE_DEFAULT = 1` 선례 → `ETHERNET_PIXEL_RESOLUTION_DEFAULT = 8.652`)
- `AfterLoad()` 는 기존 `RestorePcRoleDefault()` 호출을 **보존하고 새 호출 추가** — 기존 호출 제거 금지

---

### `WPF_Example/Custom/EthernetVision/EthernetVisionHandler.cs` (singleton handler, request-response)

**주 analog:** `WPF_Example/SystemHandler.cs` (singleton + Initialize 패턴)
**보조 analog:** `WPF_Example/Device/DeviceHandler.cs` (sealed partial + Handle 패턴)

**싱글턴 Handle 패턴** (`SystemHandler.cs` lines 28-29):
```csharp
public static SystemHandler Handle { get; } = new SystemHandler();
```

**DeviceHandler 싱글턴 선언** (`DeviceHandler.cs` line 56):
```csharp
public sealed partial class DeviceHandler : IDisposable {
    public static DeviceHandler Handle { get; } = new DeviceHandler();
```

**Initialize() 성공/실패 반환 + 실패 격리 패턴** (`SystemHandler.cs` lines 96-108):
```csharp
Devices = DeviceHandler.Handle;
EInitializeResult result = Devices.Initialize();
if (result != EInitializeResult.Success) {
    IsInitializeFail = true;
    CustomMessageBox.Show("Camera Error", "Camera Initialize Fail", MessageBoxImage.Error, true, false);
}
```

**SystemHandler.Initialize() 에 한 줄 추가 위치** (`SystemHandler.cs` lines 112-186):
- Step 1~8 중 Step 3 (Server + RawImageSaver) 또는 Step 4 이후에 삽입
- try-catch 격리 → Grabber init 이미 완료된 뒤 실행

**삽입 패턴:**
```csharp
// 260623 hbk Phase 58 — AV-02: 이더넷 정렬 카메라 독립 초기화 (실패해도 Grabber 무영향)
try {
    EthernetVisionHandler.Handle.Initialize();
}
catch (Exception ex) {
    Logging.PrintLog((int)ELogType.Error, "[ETHERNET] EthernetVisionHandler.Initialize failed: {0}", ex.Message);
}
```

**Private 생성자 패턴** (`SystemHandler.cs` lines 76-108 — 생성자에서 config 인스턴스 취득):
```csharp
private SystemHandler() {
    Setting = SystemSetting.Handle;
    // ...
}
```

**EthernetVisionHandler 구조 권장:**
```csharp
public sealed class EthernetVisionHandler {
    public static EthernetVisionHandler Handle { get; } = new EthernetVisionHandler();

    public EthernetAlignCamera Camera { get; private set; }
    public bool IsInitialized { get; private set; } = false;

    private EthernetVisionHandler() { }

    public void Initialize() {
        // mode gate (D-04)
        bool bModeOff = SystemSetting.Handle.EthernetVisionMode == EEthernetVisionMode.None;
        if (bModeOff) { return; }
        // ...
    }
}
```

**적용 규칙:**
- K&R brace style (SystemHandler.cs 가 K&R — handler 는 같은 계층이므로 동일)
- `sealed` + private 생성자 + `static Handle` — 정확히 `DeviceHandler` / `SystemHandler` 패턴
- `IsInitialized` bool 프로퍼티 — `VirtualCamera.IsOpen` 선례와 동일 패턴
- namespace `ReringProject` (최상위, SystemHandler 와 동일 계층)

---

## Shared Patterns

### try-catch 실패 격리
**Source:** `WPF_Example/SystemHandler.cs` lines 96-108, `WPF_Example/Device/Camera/Hik/HikCamera.cs` lines 306-417
**Apply to:** `EthernetAlignCamera.Connect()`, `EthernetVisionHandler.Initialize()`, `SystemHandler.Initialize()` 삽입 라인
```csharp
try {
    // HikCamera or EthernetAlignCamera operation
}
catch (Exception e) {
    Logging.PrintLog((int)ELogType.Camera, "[ERROR] {0} Open Fail. ({1})", Info.Identifier, e.Message);
    return false;
}
```

### HImage 리소스 해제
**Source:** `WPF_Example/Device/Camera/VirtualCamera.cs` lines 89-101
**Apply to:** `EthernetAlignCamera.Grab()` 반환 HImage — caller 책임 (short-lived: `using` 블록 권장)
```csharp
public virtual void ClearLastFrame() {
    // ...
    lock (Interlock) {
        if (LastGrabHalconImage != null) {
            LastGrabHalconImage.Dispose();
            LastGrabHalconImage = null;
        }
    }
}
```

### SIMUL_MODE 컴파일 상수 분기
**Source:** `WPF_Example/Device/Camera/VirtualCamera.cs` lines 231-236, `WPF_Example/Device/DeviceHandler.cs` lines 58-61
**Apply to:** `EthernetAlignCamera.Grab()` 폴백 로직
```csharp
#if SIMUL_MODE
    Properties.Width = width.I;
    Properties.Height = height.I;
    return normalizedImage;
#endif
```

### INI 직렬화 자동화 ([Category])
**Source:** `WPF_Example/Setting/SystemSetting.cs` lines 197-275 (Load 메서드)
**Apply to:** `Custom/SystemSetting.cs` 신규 프로퍼티 — `[Category("ETHERNET_VISION")]` 어트리뷰트 부여로 자동 처리

### Logging 패턴
**Source:** `WPF_Example/Utility/Logging.cs` (참조처: HikCamera.cs, SystemHandler.cs)
**Apply to:** 모든 신규 파일
```csharp
Logging.PrintLog((int)ELogType.Camera, "[ERROR] {0} ...", Name, e.Message);
Logging.PrintLog((int)ELogType.Error, "[ETHERNET] ...", ex.Message);
```
- `ELogType` 은 항상 `(int)` 캐스트
- 이더넷 카메라 로그는 `ELogType.Camera` 사용 (별도 로그 타입 신설 불필요)

---

## No Analog Found

| File | Role | Data Flow | Reason |
|---|---|---|---|
| 없음 | — | — | Phase 58 신규 파일 전부 기존 패턴의 직접 확장 |

---

## Brace Style Summary

| 파일 | 스타일 | 근거 |
|---|---|---|
| `EEthernetVisionMode.cs` | Allman | `EImageSource.cs` (신규 Custom 파일 선례) |
| `EthernetAlignCamera.cs` | K&R | `HikCamera.cs` / `VirtualCamera.cs` 가 K&R |
| `Custom/SystemSetting.cs` (추가 블록) | Allman | 기존 Phase 48 블록이 Allman — 파일 내 일관성 |
| `EthernetVisionHandler.cs` | K&R | `SystemHandler.cs` / `DeviceHandler.cs` 가 K&R |

---

## Metadata

**Analog search scope:** `WPF_Example/Custom/`, `WPF_Example/Device/`, `WPF_Example/Setting/`, `WPF_Example/`
**Files scanned:** 8개 (HikCamera.cs, VirtualCamera.cs, DeviceHandler.cs, Custom/DeviceHandler.cs, SystemHandler.cs, SystemSetting.cs, Custom/SystemSetting.cs, Custom/Define/ID.cs, EImageSource.cs)
**Pattern extraction date:** 2026-06-23
