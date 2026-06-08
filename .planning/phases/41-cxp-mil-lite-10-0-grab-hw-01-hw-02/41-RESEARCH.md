# Phase 41: CXP 카메라 MIL Lite 10.0 grab 드라이버 통합 (HW-01/HW-02) - Research

**Researched:** 2026-06-02
**Domain:** MIL Lite 10.0 .NET, CoaXPress 카메라 드라이버, VirtualCamera 통합
**Confidence:** MEDIUM-HIGH (MIL SDK 로컬 설치 확인 + 예제 직접 독해; RAP4G4C12 보드 드라이버는 미설치 상태)

---

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

#### 카메라 매핑 / 배포 토폴로지
- **D-01:** 전부 CXP 카메라 — 기존 HIK(MvCamCtrl.Net) grab 경로를 CXP/MIL로 대체. HIK 드라이버/enum 코드 잔존 vs 제거 범위는 plan 재량 (회귀 위험 최소화 우선).
- **D-02:** 물리 토폴로지 = PC 2대 + CXP 카메라 2대. PC1 ↔ 카메라①(Top + Bottom 시퀀스 2개 담당), PC2 ↔ 카메라②(Side 시퀀스 담당). 각 PC 별도 연결.
- **D-03:** 소프트웨어 인스턴스당 CXP 카메라 1대만 활성. `RegisterRequiredDevices`를 "현재 HIK 3대(Top/Side/Bottom) 고정 등록"에서 **PC별 CXP 1대 + 역할(시퀀스) 설정** 구조로 재구성.

#### 그래버·카메라 사양
- **D-04:** 그래버 = Matrox RAP4G4C12 (PCIe x8, 4채널, CXP-12).
- **D-05:** 카메라 = ViewWorks 128MP, Mono(흑백). 프레임당 ~128MB 대용량. 픽셀 포맷 = Mono8.
- **D-06:** MIL DCF 미사용 기본 — 코드 기반 digitizer 설정(default). DCF 필요 여부 추후 확인.
- **D-07:** CXP 버전 미확정 (RAP4G4C12 스펙상 CXP-12 지원).

#### 트리거 & grab 모드
- **D-08:** 소프트웨어 트리거 단발 grab (ETriggerSource.Software, 검사 1회당 1프레임).

#### MIL → HImage 변환
- **D-09:** MIL 버퍼로 grab → host 메모리 포인터 획득(MbufInquire M_HOST_ADDRESS) → HALCON HImage 버퍼 복사(GenImage1/GenImageConst 포인터 기반, Mono8). zero-copy는 추후 성능 이슈 시 별도 검토.

#### 리소스 수명 & 통합 구조
- **D-10:** MIL Application/System/Digitizer를 앱 시작 시 1회 할당, 종료 시 해제 — VirtualCamera.Open/Close 수명에 정렬. 구조 = `MilCamera : VirtualCamera` + `ECameraType.MIL` 추가 + `DeviceHandler.InitializeDevices` switch `case MIL`.

#### SIMUL_MODE
- **D-11:** SIMUL_MODE 폴백 유지 — HW 없을 때 기존 파일/배경 이미지 grab 경로 그대로(VirtualCamera SIMUL 분기).

### Claude's Discretion
- MIL→HImage 정확한 API 호출 시퀀스 및 zero-copy vs copy 최종 선택(128MP 성능 측정 기반).
- HIK 코드 잔존/제거 범위, 회귀 최소화.
- PC별 역할(시퀀스 매핑) 설정의 구체 메커니즘.
- MIL 에러/타임아웃 처리 패턴.

### Deferred Ideas (OUT OF SCOPE)
- 하드웨어 트리거 / 연속 스트리밍 grab 모드.
- DCF 기반 digitizer 설정.
- zero-copy 무복사 grab (성능 병목 확인 시 별도).
- HIK 드라이버 완전 제거/정리.
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| HW-01 | RAP 4G 4C12 CXP SDK 확정/설치 (Euresys Coaxlink 또는 Matrox — 장비 도착 후 결정) | MIL 10.00 R2 이미 설치 확인. RAP4G4C12 = Matrox Rapixo CXP, MIL SDK 지원 확정. |
| HW-02 | CXP 드라이버 통합 — VirtualCamera 인터페이스 호환, HIK와 동일 GrabHalconImage 경로 | MilCamera : VirtualCamera 패턴, MIL grab→HImage 변환 경로 연구 완료. |
</phase_requirements>

---

## Summary

이 phase는 `MilCamera : VirtualCamera` 신규 드라이버 클래스를 작성하여 Matrox RAP4G4C12 + ViewWorks 128MP CXP 카메라 grab을 기존 `DeviceHandler.GrabHalconImage(param)` → `HImage` 계약 하에 통합한다.

**핵심 사실:**
1. **MIL 10.00 R2 (build 2995)가 이 PC에 설치되어 있음** — `C:\Program Files\Matrox Imaging\MIL\`. .NET 어셈블리 `Matrox.MatroxImagingLibrary.dll` (및 v3.5 variant) 확인 [VERIFIED: 로컬 파일시스템].
2. **RAP4G4C12 보드 드라이버는 아직 미설치** — 레지스트리에 `Imaging board\Host`만 존재 (board: M_SYSTEM_HOST 타입). 실보드 설치 후 추가 드라이버/레지스트리 엔트리 생성 예상 [VERIFIED: 레지스트리 직접 조회].
3. **MIL .NET API 패턴** — `using Matrox.MatroxImagingLibrary;` + `MIL_ID` 타입 + `MIL.MappAllocDefault/MIL.MdigGrab/MIL.MbufInquire` 등 정적 메서드 호출. C# 예제 공식 소스코드 로컬 확인 [VERIFIED: 로컬 예제 직접 독해].
4. **MIL 버퍼→HImage 경로** — `MbufInquire(buf, MIL.M_HOST_ADDRESS, ref ptr)` + `MbufInquire(buf, MIL.M_PITCH, ref pitch)` → `IntPtr`로 캐스팅 → `HImage.GenImage1("byte", width, height, ptr)` — **HikCamera.OnGrabResult의 기존 패턴과 동일**(`sourceImage.GenImage1("byte", w, h, pData)`). 피치 불일치 주의 필요 [VERIFIED: 로컬 예제 + HikCamera.cs].

**Primary recommendation:** MilCamera는 HikCamera를 최소 analog로 삼아 동일한 `GenImage1("byte", ...)` 변환 패턴을 사용하고, Open 시 MIL 객체를 1회 할당·Close 시 역순 해제한다. 소프트웨어 트리거 단발 grab에 `MIL.MdigGrab(MilDigitizer, MilBuffer)`를 사용한다.

---

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| CXP grab 실행 | Device (MilCamera) | — | VirtualCamera 계약: GrabHalconImage가 HImage를 반환하는 단일 책임 |
| MIL 리소스 수명 관리 | Device (MilCamera) | DeviceHandler(Dispose) | Open/Close 수명 = DeviceHandler.Initialize/Dispose 생명주기에 정렬 |
| HImage 제공 | Device (MilCamera) | — | 시퀀스/액션 코드는 GrabHalconImage만 호출 — 변경 0 |
| PC별 역할 선택 | Custom/DeviceHandler | SystemSetting(INI) | RegisterRequiredDevices에서 설정값 읽어 역할(시퀀스명) 결정 |
| SIMUL 폴백 | Device (VirtualCamera base) | MilCamera #if SIMUL_MODE | 기존 패턴 그대로 유지 |
| ECameraType enum 확장 | Device/VirtualCamera.cs | DeviceHandler switch | MIL 추가는 단일 enum 파일 변경 |

---

## Standard Stack

### Core

| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| Matrox.MatroxImagingLibrary.dll | 10.00 R2 (build 2995) | MIL .NET binding — MappAlloc/MdigGrab/MbufInquire 등 | 이 PC에 설치된 유일한 MIL .NET 어셈블리 [VERIFIED: 로컬 파일시스템] |
| halcondotnet.dll | 24.11 Progress Steady | HImage 생성(GenImage1) 및 검사 알고리즘 | 기존 tech stack (CLAUDE.md) |

### Supporting

| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| MvCamCtrl.Net.dll | 4.1.0.3 | HIK 카메라 기존 드라이버 | HIK 코드 잔존 시 유지 (D-01: plan 재량) |

### Alternatives Considered

| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Matrox.MatroxImagingLibrary (MIL 기반) | Euresys Coaxlink SDK | 이 PC에 MIL 설치됨 + CONTEXT.md D-04 RAP4G4C12 = Matrox 보드 → Euresys 불필요 |
| copy 방식(GenImage1) | zero-copy(포인터 wrap) | 128MP = ~128MB/frame. copy는 안전하나 성능 부담. zero-copy는 MIL 버퍼 수명 관리 복잡도 증가 → D-09 deferred |

**Installation (참조):**
```
DLL 참조 경로: C:\Program Files\Matrox Imaging\MIL\MIL.NET\Matrox.MatroxImagingLibrary.dll
.csproj에 HintPath로 추가 (NuGet 미지원, 로컬 DLL 참조 — HikCamera/BaslerCamera 선례와 동일)
```

---

## Architecture Patterns

### System Architecture Diagram

```
[검사 액션 코드 — 무변경]
        |
        | GrabHalconImage(param)
        v
[DeviceHandler.GrabHalconImage]
        |
        | this[param.DeviceName].GrabHalconImage()
        v
[MilCamera : VirtualCamera]
        |-- Open() → MappAlloc → MsysAlloc → MdigAlloc → MbufAlloc2d (1회)
        |-- GrabHalconImage()
        |       |
        |       |-- #if SIMUL_MODE → base.GetCurrentImageNoLock() (파일 grab)
        |       |-- else → MdigGrab(MilDigitizer, MilBuffer) [동기 단발]
        |       |          → MbufInquire(M_HOST_ADDRESS) → IntPtr
        |       |          → MbufInquire(M_PITCH) → pitch
        |       |          → HImage.GenImage1("byte", width, height, ptr)
        |       |          → 피치 불일치 시 MimCopy 또는 행 단위 복사
        |       v
        |   lock(Interlock): LastGrabHalconImage = newImage
        |-- Close() → MbufFree → MdigFree → MsysFree → MappFree (역순)
        v
[HImage] → 기존 검사 알고리즘 (무변경)
```

### Recommended Project Structure

```
WPF_Example/
├── Device/
│   ├── Camera/
│   │   ├── Mil/
│   │   │   └── MilCamera.cs       # 신규 드라이버
│   │   ├── Hik/HikCamera.cs       # 기존 (잔존)
│   │   └── Basler/BaslerCamera.cs # 기존
│   ├── VirtualCamera.cs            # ECameraType.MIL 추가
│   └── DeviceHandler.cs            # case MIL: new MilCamera 추가
└── Custom/
    └── Device/
        └── DeviceHandler.cs        # RegisterRequiredDevices 재구성
```

### Pattern 1: MIL 리소스 할당·해제 수명 (VirtualCamera.Open/Close 정렬)

**What:** Application → System → Digitizer → Buffer를 Open()에서 1회 할당, Close()에서 역순 해제.
**When to use:** MilCamera.Open()/Close() 오버라이드에서만 사용.

```csharp
// Source: 로컬 MIL 예제 MdigGrab.cs + MbufPointerAccess.cs 직접 독해
// [VERIFIED: C:\Users\Public\Documents\Matrox Imaging\MIL\Examples\General\]

// --- Open() ---
using Matrox.MatroxImagingLibrary;

MIL_ID MilApplication = MIL.M_NULL;
MIL_ID MilSystem     = MIL.M_NULL;
MIL_ID MilDigitizer  = MIL.M_NULL;
MIL_ID MilBuffer     = MIL.M_NULL;

// 1. Application 할당
MIL.MappAlloc(MIL.M_DEFAULT, ref MilApplication);

// 2. System 할당 (M_SYSTEM_DEFAULT = MIL Config에서 선택된 보드)
//    RAP4G4C12 설치 후 MIL Config에서 시스템 설정 필요
MIL.MsysAlloc(MIL.M_DEFAULT, MIL.M_SYSTEM_DEFAULT, MIL.M_DEFAULT,
              MIL.M_DEFAULT, ref MilSystem);

// 3. Digitizer 할당 (DCF 미사용 — M_DEFAULT)
MIL.MdigAlloc(MilSystem, MIL.M_DEV0, "M_DEFAULT", MIL.M_DEFAULT,
              ref MilDigitizer);

// 4. Mono8 버퍼 할당 (128MP = ~14000x9000 또는 실제 해상도)
MIL.MbufAlloc2d(MilSystem, Info.Width, Info.Height,
                8 + MIL.M_UNSIGNED,
                MIL.M_IMAGE + MIL.M_GRAB + MIL.M_PROC,
                ref MilBuffer);

// --- Close() (역순) ---
if (MilBuffer     != MIL.M_NULL) { MIL.MbufFree(MilBuffer);     MilBuffer     = MIL.M_NULL; }
if (MilDigitizer  != MIL.M_NULL) { MIL.MdigFree(MilDigitizer);  MilDigitizer  = MIL.M_NULL; }
if (MilSystem     != MIL.M_NULL) { MIL.MsysFree(MilSystem);     MilSystem     = MIL.M_NULL; }
if (MilApplication!= MIL.M_NULL) { MIL.MappFree(MilApplication);MilApplication= MIL.M_NULL; }
```

### Pattern 2: 소프트웨어 트리거 단발 grab → HImage 변환

**What:** MdigGrab(동기) + MbufInquire(M_HOST_ADDRESS) + GenImage1("byte").
**When to use:** MilCamera.GrabHalconImage() 실 HW 경로.

```csharp
// Source: MIL 예제 MdigGrab.cs + MbufPointerAccess.cs + HikCamera.OnGrabResult 패턴
// [VERIFIED: 로컬 예제 직접 독해]

public override HImage GrabHalconImage()
{
    // SIMUL_MODE 폴백
#if SIMUL_MODE
    return LastHalconImage;  // base.GetCurrentImageNoLock() 경로
#endif

    try
    {
        // 동기 단발 grab (소프트웨어 트리거)
        MIL.MdigGrab(MilDigitizer, MilBuffer);
        // 주의: MdigGrab은 동기(블로킹). 타임아웃은 MdigControl(M_GRAB_TIMEOUT)으로 설정.

        // host 포인터 획득
        MIL_INT hostPtr   = MIL.M_NULL;
        MIL_INT pitchByte = MIL.M_NULL;
        MIL.MbufInquire(MilBuffer, MIL.M_HOST_ADDRESS, ref hostPtr);
        MIL.MbufInquire(MilBuffer, MIL.M_PITCH_BYTE,   ref pitchByte);

        if (hostPtr == MIL.M_NULL) return null;

        IntPtr ptr = hostPtr; // [ASSUMED] MIL_INT → IntPtr 암시적 변환 가능 여부 확인 필요

        // 피치 불일치 처리
        // pitchByte == Info.Width → zero-padding 없음 → GenImage1 직접 사용 가능
        // pitchByte >  Info.Width → 패딩 존재 → 행 단위 복사 필요
        HImage newImage;
        if (pitchByte == Info.Width)
        {
            // 직접 포인터 기반 HImage 생성 (HikCamera.OnGrabResult 동일 패턴)
            newImage = new HImage();
            newImage.GenImage1("byte", Info.Width, Info.Height, ptr);
        }
        else
        {
            // 행 단위 복사 (padding strip)
            // [ASSUMED] HALCON GenImageConst 또는 MimCopy를 통해 처리
            newImage = CreateImageFromPaddedBuffer(ptr, Info.Width, Info.Height, (int)pitchByte);
        }

        // 회전 처리 (HikCamera 패턴 동일)
        HImage rotatedImage = ApplyRotation(newImage);

        lock (Interlock)
        {
            LastGrabHalconImage?.Dispose();
            LastGrabHalconImage = rotatedImage;
        }
        Interlocked.Increment(ref imageCount);
        return LastHalconImage; // CopyImage() 반환
    }
    catch (Exception e)
    {
        Logging.PrintLog((int)ELogType.Camera, "[ERROR] {0} MilCamera.GrabHalconImage ({1})", Name, e.Message);
        Interlocked.Increment(ref errorCount);
        return null;
    }
}
```

### Pattern 3: RegisterRequiredDevices 재구성 (PC별 역할 분기)

**What:** SystemSetting INI 값(CameraRole: TopBottom / Side)으로 PC별 등록 카메라를 분기.
**When to use:** `Custom/Device/DeviceHandler.cs` `RegisterRequiredDevices()` 재구성 시.

```csharp
// [ASSUMED] SystemSetting에 CameraRole 프로퍼티 신규 추가 후 INI 저장
// 선례: SystemSetting.Handle.ServerPort (INI 자동 직렬화)

private void RegisterRequiredDevices()
{
    // PC 역할 판별
    // ECameraRole { TopBottom, Side } — 신규 enum (plan 재량)
    ECameraRole role = SystemSetting.Handle.CameraRole;

    if (role == ECameraRole.TopBottom)
    {
        // PC1: 카메라 1대로 Top + Bottom 시퀀스 담당
        SetRequiredDevice(ECameraType.MIL, ECaptureImageType.Gray8,
            ETriggerSource.Software, CAMERA_TOP, WIDTH_CXP, HEIGHT_CXP,
            false, false);
        // Bottom도 동일 MilCamera 인스턴스를 재사용하거나 별도 등록
        // → 동일 물리 카메라 1대 → 동일 MilCamera 인스턴스 공유 여부는 plan에서 결정
    }
    else // Side
    {
        // PC2: 카메라 1대로 Side 시퀀스 담당
        SetRequiredDevice(ECameraType.MIL, ECaptureImageType.Gray8,
            ETriggerSource.Software, CAMERA_SIDE, WIDTH_CXP, HEIGHT_CXP,
            false, false);
    }
}
```

### Anti-Patterns to Avoid

- **MIL 객체를 GrabHalconImage 호출마다 재할당:** Application/System/Digitizer는 앱 시작 시 1회만. 버퍼도 동일 인스턴스 재사용.
- **MIL_INT를 IntPtr 없이 byte* 직접 캐스팅:** MbufPointerAccess 예제대로 `IntPtr MilImagePtrIntPtr = MilImagePtr;` → `byte* addr = (byte*)MilImagePtrIntPtr;` 패턴 필요. unsafe 블록 필수.
- **GenImage1("byte", ...)을 pitch 확인 없이 사용:** 128MP 고해상도 버퍼는 하드웨어 패딩이 존재할 수 있음. MbufInquire(M_PITCH_BYTE)로 확인 후 처리.
- **MappAllocDefault 사용:** 예제용 편의 함수. 실제 드라이버에서는 Application/System/Digitizer를 개별 할당해야 수명 제어 가능.
- **MIL 에러를 무시:** `MIL.MappControl(MIL.M_DEFAULT, MIL.M_ERROR, MIL.M_PRINT_DISABLE)` 후 처리 — 기존 VirtualCamera try/catch 패턴 준수.

---

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| 소프트웨어 트리거 단발 grab | 자체 콜백/이벤트 grab 루프 | `MIL.MdigGrab(digitizer, buf)` 동기 호출 | MIL이 동기 단발 grab 직접 지원. 콜백 불필요 |
| 버퍼 host 포인터 획득 | GetPixels 등 수동 복사 | `MIL.MbufInquire(MIL.M_HOST_ADDRESS)` | MIL 공식 API. 포인터 유효성 보장 |
| HImage 픽셀 포맷 변환 | 직접 byte 변환 | `HImage.GenImage1("byte", ...)` | HikCamera.OnGrabResult 기존 패턴과 동일 |
| MIL 에러 코드 해석 | 자체 에러 테이블 | MIL 에러 핸들러 + Logging.PrintLog | try/catch 관습(CLAUDE.md) 적용 |

**Key insight:** MIL SDK가 소프트웨어 트리거 단발 grab을 `MdigGrab()` 한 줄로 지원. HIK SDK가 콜백+상태머신으로 구현한 것보다 단순하다.

---

## Common Pitfalls

### Pitfall 1: RAP4G4C12 드라이버 미설치 시 MsysAlloc 실패
**What goes wrong:** `MIL.MsysAlloc(M_SYSTEM_DEFAULT, ...)` 호출 시 시스템 디스크립터를 MIL Config에서 찾지 못하고 실패. `MIL_ID = M_NULL` 반환.
**Why it happens:** 현재 이 PC에 RAP4G4C12 보드 드라이버가 미설치 상태(레지스트리에 Host 시스템만 존재). 실보드 설치 후 MIL Config(milcontrolcenter.exe)에서 시스템 선택 필요.
**How to avoid:** SIMUL_MODE에서는 `M_SYSTEM_HOST`를 사용하거나 드라이버 미설치 시 VirtualCamera 폴백. Open() 초기에 MIL 할당 성공 여부를 확인 후 false 반환.
**Warning signs:** MilApplication/MilSystem = M_NULL 상태에서 MdigAlloc 시도.

### Pitfall 2: MbufInquire M_HOST_ADDRESS = M_NULL (비호스트 메모리)
**What goes wrong:** PCIe 보드 메모리에만 버퍼가 존재하고 host 메모리 미매핑 시 포인터가 M_NULL. 이 경우 GenImage1 불가.
**Why it happens:** MbufAlloc2d 시 `M_IMAGE + M_GRAB + M_PROC` 플래그를 사용하면 host accessible 버퍼가 할당되지 않을 수 있음. `M_HOST_ADDRESS`를 얻으려면 `M_PROC` + `M_GRAB` 조합 또는 `MbufControl(M_LOCK)` 후 접근.
**How to avoid:** MbufAlloc2d에 `M_IMAGE + M_GRAB + M_PROC` 사용 (예제 패턴). M_HOST_ADDRESS NULL 체크 후 null 반환 경로 추가.
**Warning signs:** MbufPointerAccess 예제의 `if (MilImagePtr != MIL.M_NULL)` 분기 — 동일 패턴 필수.

### Pitfall 3: MIL_INT → IntPtr 형변환 컴파일 오류
**What goes wrong:** `MIL_INT` (= `long` on 64-bit)을 `IntPtr`로 직접 캐스팅 시 CS0030 컴파일 오류.
**Why it happens:** MIL .NET 공식 예제(MbufPointerAccess.cs L89)에서 `IntPtr MilImagePtrIntPtr = MilImagePtr;` 암시적 변환을 사용 — MIL_INT에 IntPtr 암시적 변환 연산자가 정의된 것으로 보이나 컴파일러 버전에 따라 다를 수 있음.
**How to avoid:** `new IntPtr((long)MilImagePtr)` 명시적 변환을 사용하면 안전. unsafe 블록에서 `(void*)MilImagePtr` 경로도 가능.
**Warning signs:** CS0030 "cannot convert type 'long' to 'System.IntPtr'".

### Pitfall 4: 128MP 버퍼 복사 성능 문제
**What goes wrong:** ~128MB/frame 복사 시 grab→HImage 변환이 수백 ms 소요 → 검사 사이클 시간 초과.
**Why it happens:** ViewWorks 128MP ≈ 14000×9000(추정) × 1byte = ~126MB. Marshal.Copy 또는 행 단위 복사는 메모리 대역폭 한계.
**How to avoid:** GenImage1("byte", w, h, ptr)은 MIL 버퍼 메모리를 직접 wrapping 하는 zero-copy일 수 있음 [ASSUMED]. 실 측정 후 필요 시 D-09 deferred 항목(zero-copy)으로 전환. 1회 버퍼 재사용(매 grab마다 재할당 금지)이 핵심.
**Warning signs:** grab 루프에서 평균 grab time > 검사 사이클 타임.

### Pitfall 5: PC별 역할 없이 전체 시퀀스 등록
**What goes wrong:** PC1에서 Top/Bottom + Side 3개 시퀀스가 등록되면 Side 카메라 없음 → MIL Open 실패 → 전체 초기화 실패(IsInitializeFail=true).
**Why it happens:** RegisterRequiredDevices가 역할 분기 없이 HIK 3대 패턴을 그대로 유지.
**How to avoid:** D-03에 따라 SystemSetting INI CameraRole 기반으로 등록 카메라 분기. SIMUL_MODE에서는 역할 무관하게 VirtualCamera 폴백.

### Pitfall 6: MIL 해제 순서 역전
**What goes wrong:** Digitizer Free 전에 System Free → MdigFree 호출 시 invalid handle.
**Why it happens:** MIL 객체는 의존 계층이 있어 생성 역순으로 해제해야 함.
**How to avoid:** Buffer → Digitizer → System → Application 순서 엄수. 각 Free 전 != M_NULL 체크.

---

## Code Examples

Verified patterns from official sources:

### MIL .NET 기본 할당/해제 (공식 예제 패턴)

```csharp
// Source: C:\Users\Public\Documents\Matrox Imaging\MIL\Examples\General\MdigGrab\C#\MdigGrab.cs
// [VERIFIED: 로컬 예제 직접 독해]

using Matrox.MatroxImagingLibrary;

MIL_ID MilApplication = MIL.M_NULL;
MIL_ID MilSystem      = MIL.M_NULL;
MIL_ID MilDigitizer   = MIL.M_NULL;
MIL_ID MilImage       = MIL.M_NULL;

// 편의 함수 (예제 전용 — 실 드라이버에서는 개별 할당 사용)
MIL.MappAllocDefault(MIL.M_DEFAULT, ref MilApplication, ref MilSystem,
                     ref MilDisplay, ref MilDigitizer, ref MilImage);

// 단발 grab
MIL.MdigGrab(MilDigitizer, MilImage);

// 해제
MIL.MappFreeDefault(MilApplication, MilSystem, MilDisplay, MilDigitizer, MilImage);
```

### MbufInquire M_HOST_ADDRESS → IntPtr (안전한 변환)

```csharp
// Source: C:\Users\Public\Documents\Matrox Imaging\MIL\Examples\General\MbufPointerAccess\C#\MbufPointerAccess.cs L75-90
// [VERIFIED: 로컬 예제 직접 독해]

MIL_INT MilImagePtr   = MIL.M_NULL;
MIL_INT MilImagePitch = MIL.M_NULL;

MIL.MbufControl(MilImage, MIL.M_LOCK, MIL.M_DEFAULT);
MIL.MbufInquire(MilImage, MIL.M_HOST_ADDRESS, ref MilImagePtr);
MIL.MbufInquire(MilImage, MIL.M_PITCH,        ref MilImagePitch);

if (MilImagePtr != MIL.M_NULL)
{
    unsafe
    {
        IntPtr MilImagePtrIntPtr = MilImagePtr; // 공식 예제 패턴
        byte* MilImageAddr = (byte*)MilImagePtrIntPtr;
        // ... 픽셀 처리 후 ...
        MIL.MbufControl(MilImage, MIL.M_MODIFIED, MIL.M_DEFAULT);
        MIL.MbufControl(MilImage, MIL.M_UNLOCK,   MIL.M_DEFAULT);
    }
}
```

### HikCamera.OnGrabResult의 GenImage1 패턴 (MilCamera와 동일)

```csharp
// Source: WPF_Example/Device/Camera/Hik/HikCamera.cs L455-486
// [VERIFIED: 코드베이스 직접 독해]

private void OnGrabResult(IntPtr pData, ref MV_FRAME_OUT_INFO_EX pFrameInfo, IntPtr pUser)
{
    lock (Interlock)
    {
        HImage sourceImage = new HImage();
        sourceImage.GenImage1("byte", (int)pFrameInfo.nWidth, (int)pFrameInfo.nHeight, pData);
        // 회전 처리
        LastGrabHalconImage?.Dispose();
        LastGrabHalconImage = sourceImage;
    }
}
// MilCamera.GrabHalconImage()에서 동일하게: new HImage().GenImage1("byte", w, h, ptr)
```

---

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| HIK USB/GigE 카메라 (MvCamCtrl.Net) | CXP 카메라 (Matrox MIL) | Phase 41 (2026-06) | grab 경로만 변경. 시퀀스/알고리즘 코드 무변경 |
| 3대 카메라(Top/Side/Bottom) 단일 PC | 2대 PC × 1대 카메라 역할 분담 | Phase 41 (2026-06) | RegisterRequiredDevices 재구성 필요 |
| MappAllocDefault 편의 함수 | 개별 MappAlloc/MsysAlloc/MdigAlloc | 실 드라이버 구현 필요 | 수명 관리 세분화 가능 |

**Deprecated/outdated:**
- `ECameraType.HIK` with `new HikCamera(...)`: Phase 41 이후 MIL 경로로 대체. 코드는 잔존 허용(D-01).

---

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | `MIL.MsysAlloc(M_DEFAULT, M_SYSTEM_DEFAULT, ...)` 호출 시 MIL Config에서 선택된 시스템(RAP4G4C12)이 자동 선택됨 | Architecture Patterns (Pattern 1) | 시스템 디스크립터 문자열을 명시해야 할 수도 있음. 예: `"M_SYSTEM_RAPIXO_CXP"` 등 — 보드 드라이버 설치 후 milcontrolcenter.exe에서 확인 필요 |
| A2 | `MIL_INT`에서 `IntPtr`로의 암시적 변환(`IntPtr ptr = milIntValue;`)이 MIL 10.00 .NET에서 유효함 | Code Examples | CS0030 오류 시 `new IntPtr((long)milIntValue)` 명시적 변환으로 교체 |
| A3 | `MIL.MdigAlloc(MilSystem, MIL.M_DEV0, "M_DEFAULT", MIL.M_DEFAULT, ref MilDigitizer)` — DCF 파일 없이 default 채널 0 사용 가능 | Pattern 1 | 특정 카메라(ViewWorks 128MP)의 GenICam 파라미터가 `"M_DEFAULT"` DCF로 자동 설정되지 않을 수 있음 → 해상도/픽셀포맷 수동 설정 필요 가능성 |
| A4 | ViewWorks 128MP ≈ Vieworks VNP-576MX2 (144MP 계열 CXP-12 모노) — 정확한 모델명/해상도 미확인 | Standard Stack | 실제 해상도(WIDTH/HEIGHT 상수), CXP 링크 수 등 DeviceInfo 설정에 영향 |
| A5 | `HImage.GenImage1("byte", w, h, ptr)` 호출이 MIL 버퍼 host 메모리를 copy없이 wrap 함 | Pitfall 4 | 실제로 copy 발생 시 128MP grab 성능 문제. M_PITCH ≠ width 시 추가 copy 필요 |
| A6 | PC1에서 Top/Bottom 시퀀스가 동일 물리 카메라 1대를 순차 공유 가능 (MilCamera 인스턴스 1개 또는 2개 별도 등록) | Pitfall 5 | 공유 방식 결정에 따라 RegisterRequiredDevices 등록 전략이 달라짐 |

---

## Open Questions

1. **RAP4G4C12 MIL 시스템 디스크립터 문자열**
   - What we know: MIL Config(milcontrolcenter.exe)에서 설치된 보드의 시스템 디스크립터를 확인할 수 있음. 현재 이 PC에 보드 미설치.
   - What's unclear: `MsysAlloc`의 두 번째 인자에 전달할 실제 문자열 (예: `"M_SYSTEM_RAPIXO_CXP"` 또는 정수 상수 `MIL.M_SYSTEM_RAPIXO_CXP`).
   - Recommendation: SIMUL 단계에서는 `M_SYSTEM_HOST`로 MsysAlloc 테스트. 실보드 도착 후 milcontrolcenter.exe에서 시스템명 확인 후 상수 결정. Wave 0에서 SIMUL 경로를 먼저 구현.

2. **PC1 Top+Bottom 시퀀스의 카메라 공유 방식**
   - What we know: D-02에 의해 PC1은 Top+Bottom을 담당하되 카메라 1대.
   - What's unclear: Top 시퀀스 grab 후 Bottom 시퀀스 grab 시 동일 MilCamera 인스턴스를 lock으로 순차 사용하는지 vs. Digitizer 채널을 분리해 별도 `MilCamera` 2개를 등록하는지.
   - Recommendation: MilCamera 인스턴스 1개를 CAMERA_TOP 이름으로 등록하고 Bottom 시퀀스 액션은 같은 카메라 이름을 사용하거나, CAMERA_TOP/CAMERA_BOTTOM 각각 등록 후 동일 MIL_ID를 공유하는 방식. Plan에서 결정.

3. **ViewWorks 128MP 정확한 해상도**
   - What we know: Vieworks VNP 계열 128-150MP급 CXP-12 카메라 존재 확인 (VNP-604MX: 14192×10640). "128MP"는 ~14000×9000 추정.
   - What's unclear: 정확한 WIDTH/HEIGHT 상수 값.
   - Recommendation: 카메라 실물 도착 후 MIL MdigInquire(M_SIZE_X/Y) 또는 카메라 데이터시트에서 확정. DeviceInfo 상수는 plan에서 TBD로 처리하고 실측 후 채움.

---

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| Matrox MIL .NET (Matrox.MatroxImagingLibrary.dll) | MilCamera 컴파일·런타임 | ✓ | 10.00 R2 (build 2995) | — |
| RAP4G4C12 보드 드라이버 | 실 CXP grab | ✗ | — | SIMUL_MODE (VirtualCamera 파일 grab) |
| ViewWorks 128MP 카메라 | 실 CXP grab | ✗ | — | SIMUL_MODE |
| milcontrolcenter.exe | 시스템 디스크립터 확인 | ✓ | MIL 10.00 R2 | — |

**Missing dependencies with no fallback:**
- 없음 (SIMUL_MODE로 모든 HW 미설치 경로 커버됨)

**Missing dependencies with fallback:**
- RAP4G4C12 보드 + ViewWorks 카메라: SIMUL_MODE에서 파일 grab으로 전체 경로 검증 가능. 실 HW grab은 보드 설치 후.

---

## Validation Architecture

> nyquist_validation 설정 없음 → 기본 활성화로 처리. 단, 이 phase는 HW 드라이버 코드로 자동화 테스트가 구조적으로 어려움 (MIL HW 의존). SIMUL_MODE 빌드 검증이 핵심.

### Test Framework

| Property | Value |
|----------|-------|
| Framework | MSBuild 수동 빌드 + SIMUL_MODE 런타임 검증 (단위 테스트 프레임워크 없음 — CLAUDE.md 확인) |
| Config file | WPF_Example/DatumMeasurement.csproj (SIMUL_MODE 조건부 빌드) |
| Quick run command | `msbuild WPF_Example\DatumMeasurement.csproj /p:Configuration=Debug /p:Platform=x64` |
| Full suite command | 빌드 성공 + SIMUL_MODE 시작 → 카메라 grab 경로 수동 UAT |

### Phase Requirements → Test Map

| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| HW-01 | RAP4G4C12 MIL SDK 참조 추가 및 빌드 성공 | build | `msbuild /p:Configuration=Debug /p:Platform=x64` | ❌ Wave 0: .csproj 참조 추가 |
| HW-02 | SIMUL_MODE에서 MilCamera GrabHalconImage → HImage 반환 | smoke (SIMUL) | 수동 UAT: 앱 실행 → CXP 카메라 선택 → grab 결과 표시 | ❌ Wave 0: MilCamera.cs 신규 파일 |
| HW-02 | MilCamera 등록 시 기존 HIK 시퀀스 회귀 없음 | regression | 빌드 성공 + SIMUL grab 기존 3 시퀀스 경로 검증 | ❌ Wave 0 |

### Sampling Rate
- **Per task commit:** `msbuild /p:Configuration=Debug /p:Platform=x64` (0 errors 확인)
- **Per wave merge:** 빌드 + SIMUL_MODE 앱 시작 → MilCamera grab path 수동 확인
- **Phase gate:** SIMUL_MODE 5/5 UAT PASS before `/gsd-verify-work`. 실HW 테스트는 보드 도착 후.

### Wave 0 Gaps
- [ ] `WPF_Example/Device/Camera/Mil/MilCamera.cs` — 신규 파일
- [ ] `.csproj`에 `Matrox.MatroxImagingLibrary.dll` 참조 추가 (HintPath)
- [ ] `ECameraType.MIL` enum 값 추가 (VirtualCamera.cs)
- [ ] `DeviceHandler.InitializeDevices` switch case MIL 추가

---

## Security Domain

> 이 phase는 로컬 HW 드라이버 통합 (카메라 grab 전용). 네트워크/인증/입력 검증 없음. 보안 ASVS 적용 불필요.

| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V2 Authentication | no | — |
| V3 Session Management | no | — |
| V4 Access Control | no | — |
| V5 Input Validation | partial | DeviceInfo 파라미터 null-check (기존 패턴) |
| V6 Cryptography | no | — |

---

## Sources

### Primary (HIGH confidence)
- 로컬 MIL 예제 C# 소스: `C:\Users\Public\Documents\Matrox Imaging\MIL\Examples\General\MdigGrab\C#\MdigGrab.cs` — MdigGrab, MappAllocDefault API 확인
- 로컬 MIL 예제 C# 소스: `C:\Users\Public\Documents\Matrox Imaging\MIL\Examples\General\MbufPointerAccess\C#\MbufPointerAccess.cs` — M_HOST_ADDRESS, M_PITCH, IntPtr 변환 패턴 확인
- 레지스트리 `HKLM\SOFTWARE\Matrox\Matrox Imaging Library`: MIL 10.00 R2 build 2995 설치 확인
- `WPF_Example/Device/Camera/Hik/HikCamera.cs`: OnGrabResult GenImage1("byte") 패턴, SetSoftwareTriggerMode, GrabHalconImage 흐름

### Secondary (MEDIUM confidence)
- [Zebra Rapixo RAP4G4C12 spec (automationdistribution.com)](https://automationdistribution.com/zebra-rap4g4c12-rapixo-cxp-quad-cxp-12-pcie-2-1-x8-frame-grabber-with-4gb-and-passive-heatsink-includes-one-1-mdp-to-hd15-gpio-cable-adaptor-note-cable-adaptors-for-second-third-and-fourth-gpios-sold-separately/): PCIe 2.1 x8, CXP-12, 4채널, 4GB
- [Vieworks VNP Series (official)](https://vision.vieworks.com/en/camera/area_scan/VNP_series): VNP-604MX 14192×10640 CXP-6, VNP-576MX2 12000×12000 CXP-12 확인 (정확 모델 미확인)

### Tertiary (LOW confidence)
- `A1~A6` 가정 항목 전체: 로컬 예제 해석 기반 추론. RAP4G4C12 드라이버 미설치로 실 API 동작 미확인.

---

## Metadata

**Confidence breakdown:**
- Standard Stack: HIGH — MIL 10.00 R2 로컬 설치 확인, .NET DLL 존재 확인
- Architecture: MEDIUM — MIL API 패턴은 공식 예제로 확인. 시스템 디스크립터 문자열(A1), PC1 카메라 공유(A6) 미확정
- Pitfalls: MEDIUM-HIGH — MIL 예제 + HikCamera 코드 패턴 기반. 실 HW 동작은 보드 설치 후 확인

**Research date:** 2026-06-02
**Valid until:** 2026-07-02 (MIL 10.00 안정 버전; RAP4G4C12 드라이버 설치 후 A1 재확인 필요)
