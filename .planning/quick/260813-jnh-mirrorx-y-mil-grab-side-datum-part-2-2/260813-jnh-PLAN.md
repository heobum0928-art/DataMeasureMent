---
phase: quick-260813-jnh
plan: 01
type: execute
wave: 1
depends_on: []
files_modified:
  - WPF_Example/Custom/Device/DeviceHandler.cs
  - WPF_Example/Device/DeviceHandler.cs
  - WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs
  - WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs
  - WPF_Example/UI/ContentItem/MainView.xaml.cs
autonomous: false
requirements: [QUICK-260813-JNH]

must_haves:
  truths:
    - "SIDE 카메라 grab 이 Datum 의 MirrorX/MirrorY 값에 따라 MIL 하드웨어 grab 방향(M_GRAB_DIRECTION_X/Y)이 반전된 이미지를 돌려준다"
    - "MirrorX/MirrorY 가 둘 다 꺼진 Datum·Shot 은 변경 전과 완전히 동일한 무미러 역할 식별자로 grab 된다 (회귀 0)"
    - "Shot 검사이미지 grab 이 그 Shot 의 측정들이 참조하는 DatumRef 를 통해 소유 Datum 의 미러 설정을 그대로 따라간다"
    - "DatumRef 가 현재 레시피에서 해석되지 않으면 미러를 적용하지 않고(fail-safe) Error 로그에 Shot 이름과 미해석 DatumRef 값이 남는다"
    - "HALCON 소프트웨어 미러(mirror_image / RotateImage) 호출이 diff 에 0건이다"
    - "운영 레시피 파일(D:\\Data\\Recipe\\**)은 읽기만 하고 이번 작업에서 편집되지 않는다"
  artifacts:
    - path: "WPF_Example/Custom/Device/DeviceHandler.cs"
      provides: "미러 역할 식별자 생성(BuildGrabRoleIdentifier) + 미러 역할 DeviceInfo 클론 생성(BuildMirrorRoleInfos/CloneRoleInfo)"
      contains: "BuildGrabRoleIdentifier"
    - path: "WPF_Example/Device/DeviceHandler.cs"
      provides: "GrabHalconImage(ICameraParam, string) 2-인자 오버로드 + MIL 분기 미러 역할 등록 호출"
      contains: "GrabHalconImage(ICameraParam param, string requestIdentifier)"
    - path: "WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs"
      provides: "Shot → DatumRef → DatumConfig 미러 플래그 역추적 + fail-safe 경고 로그 (ResolveShotGrabMirror)"
      contains: "ResolveShotGrabMirror"
    - path: "WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs"
      provides: "생산 경로 grab 2곳 배선 (Shot 검사이미지 grab / Datum 검출 grab)"
    - path: "WPF_Example/UI/ContentItem/MainView.xaml.cs"
      provides: "티칭 경로 grab 3곳 배선 (일반 Grab / Datum Grab / 검사이미지 Grab)"
  key_links:
    - from: "Action_FAIMeasurement.cs EStep.Grab"
      to: "InspectionSequence.ResolveShotGrabMirror"
      via: "ShotParam.Parent as InspectionSequence"
      pattern: "ResolveShotGrabMirror"
    - from: "호출부 5곳"
      to: "DeviceHandler.GrabHalconImage(param, requestIdentifier)"
      via: "DeviceHandler.BuildGrabRoleIdentifier(param.DeviceName, mirrorX, mirrorY)"
      pattern: "GrabHalconImage\\([A-Za-z]+, "
    - from: "DeviceHandler.Initialize() MIL 분기"
      to: "MilCamera._roleInfoMap (역할별 미러 3조합)"
      via: "milCam.RegisterRoleInfo(mirrorInfo) — MilCamera.cs 무수정"
      pattern: "RegisterRoleInfo"
    - from: "MilCamera.ResolveRoleInfo(requestIdentifier)"
      to: "MIL.MdigControl(M_GRAB_DIRECTION_X/Y)"
      via: "GrabFromBuffer(roleInfo) — 기존 코드, 무수정"
      pattern: "M_GRAB_DIRECTION"
---

<objective>
Part 1(quick-260813-fdt, 커밋 b49d14f)에서 `DatumConfig` 에 추가만 해두고 아무도 읽지 않던 `MirrorX`/`MirrorY` 설정값을, 실제 MIL 카메라 grab 방향 반전으로 연결한다.

Purpose: SIDE 지그의 특정 물리 포즈(`Side_Datum_4-1` 계열)는 카메라가 뒤집힌 방향으로 찍어야 한다. 소프트웨어 미러(HALCON `mirror_image`, ~27ms/장)는 택타임 비용 때문에 이미 기각됐고, MIL 하드웨어 grab 방향 반전(비용 0)만 사용한다.

Output:
- MIL 역할별 4종(무미러 기준 + 미러 3조합)이 앱 시작 시 정적 등록됨
- `DeviceHandler.GrabHalconImage(ICameraParam, string)` 2-인자 오버로드 신설
- grab 호출부 5곳이 올바른 역할 식별자를 선택
- Shot→Datum 역추적 헬퍼 + 해석 실패 시 fail-safe(무미러) + 경고 로그

**이 작업은 코드만 바꾼다. 운영 레시피 파일은 한 바이트도 편집하지 않는다.**
</objective>

<execution_context>
@$HOME/.claude/get-shit-done/workflows/execute-plan.md
@$HOME/.claude/get-shit-done/templates/summary.md
</execution_context>

<context>
@.planning/quick/260813-jnh-mirrorx-y-mil-grab-side-datum-part-2-2/260813-jnh-RESEARCH.md
@CLAUDE.md

## ⚠ RESEARCH.md 신뢰 범위 (반드시 먼저 읽을 것)

RESEARCH.md 는 **두 부분으로 나뉘고 신뢰도가 다르다.**

| RESEARCH.md 내용 | 신뢰도 | 근거 |
|---|---|---|
| **소스코드 조사**(파일 경로, file:line, 인터페이스, 설계 A/대안 기각 근거) | ✅ **신뢰 가능** | plan-checker 가 현재 소스 트리와 전부 대조해 일치 확인 |
| **실 레시피(`D:\Data\Recipe\FAI_1\main.ini`) 관련 서술 전부** | ❌ **전면 무효 — 실제 파일과 일치하지 않음** | plan-checker + orchestrator 가 라이브 파일을 직접 반복 확인. 파일 크기·Shot 이름·라인 번호·"고아 DatumRef" 주장이 **모두 실제와 불일치** |

**따라서: RESEARCH.md 안의 어떤 레시피 관련 이름/라인번호/수치도 그대로 쓰지 마라.** 레시피에 관한 사실이 필요하면 아래 "실 레시피 실측 결과(2026-08-13 재확인)" 표만 쓰거나, 직접 파일을 다시 읽어라. 특히 RESEARCH.md 가 주장한 "고아/stale DatumRef 데이터 결함"은 **현재 라이브 레시피에 존재하지 않는다** — 고칠 대상이 없으므로 이 plan 에는 레시피 편집 태스크가 없다.

아래 `<interfaces>` 는 plan 작성 시점에 실제 소스에서 재확인해 옮겨둔 것이다. **코드베이스 재탐색 없이 바로 구현 가능하다.**

## 이 작업의 배경 (한 문장)

SIDE 지그의 4-1 포즈는 물리적으로 뒤집혀 있어서, 그 포즈에서 찍는 사진(Datum 검출용 + 측정 이미지용)을 카메라가 상하/좌우로 뒤집어서 찍어줘야 한다. Part 1 에서 "뒤집을까요?" 라는 설정 스위치만 만들어 뒀고, 이번 작업이 그 스위치를 실제 카메라에 연결한다.

## 설계 요약 (RESEARCH "설계 A" — 확정, 재검토 불필요)

미러 조합은 (MirrorX, MirrorY) 불리언 2개 = **최대 4가지**뿐이다. 그래서 레시피를 스캔할 필요 없이 **앱 시작 시 4가지 역할을 전부 정적 등록**해두고, grab 할 때 식별자 문자열만 골라 넘긴다.

이 설계를 택한 이유(대안 기각 근거는 RESEARCH §3/§4 — 소스코드 조사분이므로 신뢰 가능):
- 레시피는 카메라 초기화보다 **한참 뒤에** 로드된다 → "레시피 보고 등록" 은 구조적으로 불가능
- `MilCamera._roleInfoMap` 에는 `Remove`/`Clear` 가 없다 → 레시피 교체형 등록은 유령 역할이 남는다
- `Devices` 딕셔너리에 가짜 카메라 키를 넣는 방식은 INI 오염 + UI 드롭다운 노출 + SIMUL 전면 실패를 유발한다

## 절대 건드리지 말 것

| 대상 | 이유 |
|---|---|
| `D:\Data\Recipe\**` 전체 (라이브 레시피·백업 사본 모두) | **읽기 전용.** 이번 plan 에는 레시피 편집 태스크가 없다. 어떤 write 도구도 이 경로에 쓰지 마라 |
| `WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs` | Part 1 완료분(b49d14f). `MirrorX`/`MirrorY`/`_suppressMirrorWarning` 재수정 금지 |
| `WPF_Example/Device/Camera/Mil/MilCamera.cs` | `RegisterRoleInfo`/`ResolveRoleInfo`/`GrabFromBuffer` 가 이미 필요한 걸 전부 제공한다. **신규 코드 0줄** |
| TCP 프로토콜 코드 | 범위 밖 |
| TOP/BOTTOM 카메라 설정값(`REVERSE_X_TOP` 등 상수) | 범위 밖. 상수는 한 글자도 바꾸지 않는다 |
| 기존 Datum 판정/측정 로직 | 범위 밖. 이번 변경은 "어떤 방향으로 찍을지" 선택뿐이다 |
| HALCON `mirror_image` / `HImage.RotateImage` | 소프트웨어 미러 전면 금지 (택타임 비용) |
| 실행 중인 `DatumMeasurement.exe` 프로세스 | 빌드 산출물이 잠겨도 **프로세스를 죽이지 마라.** 스크래치 OutDir 로 컴파일만 검증한다(이 plan 의 빌드 스크립트가 이미 그렇게 되어 있다) |

## 코딩 규칙 (매 태스크 적용)

- **삼항 연산자 `?:` 금지** → 반드시 `if-else`
- 헝가리언 접두(`b` bool, `sz` string, `n` int) — 신규 지역변수에 적용
- C# 7.2 (switch expression / record / nullable reference type 금지)
- 각 파일의 **기존 brace 스타일에 맞춘다**:
  - `Device/DeviceHandler.cs`, `Custom/Device/DeviceHandler.cs`, `MainView.xaml.cs`, `Action_FAIMeasurement.cs` → K&R (여는 중괄호 같은 줄)
  - `InspectionSequence.cs` 의 `IsDatumRefUnresolvable` 주변 → Allman (여는 중괄호 다음 줄). 신규 메서드는 그 이웃에 붙이고 Allman 을 따른다
- 함수 30~40줄 상한. 넘으면 private sub-헬퍼로 분리
- 주석은 "왜" 만. 날짜 주석 규칙(`//YYMMDD hbk`)은 2026-06-11 폐기됐으나, 이 저장소는 신규 코드에 `quick-260813-jnh:` 태그를 붙이는 관행이 있다 — 그 형태만 사용

<interfaces>
<!-- plan 작성 시 실제 소스에서 확인해 옮긴 계약. 재탐색 불필요. -->

**WPF_Example/Device/DeviceHandler.cs:14-40 — DeviceInfo (필드 public, 생성자 인자 순서)**
```csharp
public class DeviceInfo {
    public ECameraType CamType;
    public ECaptureImageType ImageType;
    public ETriggerSource TriggerSource;
    public string Identifier;
    public int Width;
    public int Height;
    public bool ReverseX;
    public bool ReverseY;
    public ERotateAngleType RotateAngle = 0;

    public DeviceInfo(ECameraType type, ECaptureImageType imageType, ETriggerSource triggerSource,
                      string id, int width, int height, bool reverseX, bool reverseY,
                      ERotateAngleType rotateAngle = ERotateAngleType._0) { ... }
}
```
⚠ **기존 결함**: 이 생성자는 `TriggerSource` 를 대입하지 않는다(:28-39 본문 확인). 원본 생성자는 고치지 말고, 클론 생성 후 `clone.TriggerSource = baseInfo.TriggerSource;` 로 명시 복사한다.

**WPF_Example/Device/DeviceHandler.cs:329-335 — 공통 grab 병목 (티칭·생산 양쪽이 여기로 수렴)**
```csharp
public HImage GrabHalconImage(ICameraParam param) {
    VirtualCamera cam = this[param.DeviceName];
    if (cam == null) return null;
    if (cam.Properties == null) return null;
    if (!cam.Properties.ApplyFromParam(param)) return null;
    return cam.GrabHalconImage(param.DeviceName);   // ← 카메라 조회와 역할 식별자가 같은 값에 묶여 있음
}
```

**WPF_Example/Device/DeviceHandler.cs:221-249 — MIL 등록 분기 (`#if SIMUL_MODE` / `#else`)**
```csharp
case ECameraType.MIL: {
#if SIMUL_MODE
    AddVirtualCamera(id);
#else
    MilCamera sharedMil = Devices.Values.FirstOrDefault(c => c.CamType == ECameraType.MIL) as MilCamera;
    if (sharedMil != null) {
        sharedMil.RegisterRoleInfo(id);
        Devices.Add(id.Identifier, sharedMil);
    }
    else {
        MilCamera newCam = new MilCamera(Config, id);
        if (!newCam.Open()) {
            result &= ~EInitializeResult.Success;
            result |= EInitializeResult.OpenFail;
            continue;
        }
        Devices.Add(id.Identifier, newCam);
    }
#endif
}
break;
```

**WPF_Example/Device/Camera/Mil/MilCamera.cs:52-71 — 역할 맵 (무수정)**
```csharp
public void RegisterRoleInfo(DeviceInfo roleInfo) { ...; _roleInfoMap[roleInfo.Identifier] = roleInfo; }
private DeviceInfo ResolveRoleInfo(string requestIdentifier) {
    if (requestIdentifier == null) return Info;
    if (_roleInfoMap.TryGetValue(requestIdentifier, out roleInfo)) return roleInfo;
    return Info;                 // ← 미등록 식별자는 기본 Info 로 조용히 폴백
}
```

**WPF_Example/Device/Camera/VirtualCamera.cs:460-462 — SIMUL 안전판**
```csharp
public virtual HImage GrabHalconImage(string requestIdentifier) {
    return GrabHalconImage();     // ← 인자 무시. SIMUL 에서는 어떤 식별자를 넘겨도 회귀 위험 0
}
```

**WPF_Example/Custom/Device/DeviceHandler.cs:14-16, 35-36, 43, 115-126 — 상수와 등록 헬퍼**
```csharp
public const string CAMERA_SIDE = "CAM_SIDE";
public const bool REVERSE_X_SIDE = false;
public const bool REVERSE_Y_SIDE = false;
public const ERotateAngleType ROTATE_SIDE = ERotateAngleType._0;

private void RegisterCxpCamera(string cameraName, bool reverseX, bool reverseY, ERotateAngleType rotate) {
    SetRequiredDevice(ECameraType.MIL, ECaptureImageType.Gray8, ETriggerSource.Software,
                      cameraName, WIDTH_CXP, HEIGHT_CXP, reverseX, reverseY, rotate);
}
```
`using System.Collections.Generic;` 이미 있음(:4). `using ReringProject.Define;` 있음(:1).

**WPF_Example/Custom/Sequence/Inspection/MeasurementBase.cs:18**
```csharp
public string DatumRef { get; set; } = "";     // 빈 문자열 = 무보정(의도), 경고 대상 아님
```

**WPF_Example/Custom/Sequence/Inspection/ShotConfig.cs:9, 38**
```csharp
public class ShotConfig : CameraSlaveParam, IOfflineImageParam { ... }
public List<FAIConfig> FAIList { get; private set; } = new List<FAIConfig>();
// fai.Measurements → List<MeasurementBase>  (Action_FAIMeasurement.cs:342,349 에서 사용 중)
// shot.ShotName, shot.DeviceName, shot.SequenceName, shot.Parent 사용 가능
```

**WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs:52, 2034-2043 — 재사용할 조회 패턴**
```csharp
public List<DatumConfig> DatumConfigs { get; private set; } = new List<DatumConfig>();

public bool IsDatumRefUnresolvable(string datumRef)
{
    if (string.IsNullOrEmpty(datumRef)) return false;
    if (DatumConfigs == null) return true;
    foreach (var d in DatumConfigs)
    {
        if (d != null && d.DatumName == datumRef) return false;
    }
    return true;
}
```
`using ReringProject.Define;`(ELogType) / `using ReringProject.Utility;`(Logging) / `using ReringProject.Device;`(DeviceHandler) 전부 이미 있음(:1-12). `Logging.PrintLog` 이 파일에서 이미 7회 사용 중.

**Action_FAIMeasurement.cs:1182-1189 — 이미 존재하는 DatumRef→DatumConfig 조회 (같은 규약 재사용)**
```csharp
DatumConfig dc = null;
if (parentSeq2 != null && parentSeq2.DatumConfigs != null && !string.IsNullOrEmpty(meas.DatumRef))
{
    foreach (var d in parentSeq2.DatumConfigs)
    {
        if (d != null && d.DatumName == meas.DatumRef) { dc = d; break; }
    }
}
```

**MainView.xaml.cs — grab 3곳 (전부 `lock (mDrawInterlock)` 안, 조명 적용+WaitForPendingWrites 직후)**
```
:1216  GrabAndDisplay(ICameraParam param, bool eventCall)                   grabbedHalconImage = pDev.GrabHalconImage(param);
:1283  GrabAndDisplay(ICameraParam param, DatumConfig datum, bool eventCall) grabbedHalconImage = pDev.GrabHalconImage(param);
:1375  GrabSaveAndDisplay(ICameraParam displayParam, DatumConfig datum, ...) grabbedHalconImage = pDev.GrabHalconImage(param);
```
세 곳 모두 `param is ShotConfig shotForGrab` 패턴매칭(C# 7.0)을 이미 쓰고 있고, `SystemHandler.Handle.Sequences[param.SequenceName] as InspectionSequence` 로 시퀀스를 얻는 선례가 바로 위 줄에 있다.

**DatumConfig.cs:231-261 — Part 1 결과물 (무수정 대상, 읽기만)**
```csharp
private bool _mirrorX;   // C# 초기값 false
public bool MirrorX { get; set; }   // [Category("Datum|Mirror")]
private bool _mirrorY;   // C# 초기값 false
public bool MirrorY { get; set; }   // [Category("Datum|Mirror")]
// 파일 주석(:233-234): "INI 키 미존재 시 ParamBase.Load 의 Boolean case 가 false 를 넣는데
//  C# 초기값과 같으므로 Load 오버라이드 폴백이 필요 없다" — 아래 회귀 0 근거의 핵심
```
</interfaces>

## 실 레시피 실측 결과 (2026-08-13 orchestrator 직접 재확인 — RESEARCH.md 서술을 대체함)

`D:\Data\Recipe\FAI_1\main.ini` — **261,714 bytes / 11,169 lines / 최종 저장 2026-07-29 17:40**

### 현재 존재하는 Datum 6개 (전부)

| 섹션 | DatumName |
|---|---|
| `[FIXTURE_DATUM_0]` (L12) | `Top_Datum` |
| `[FIXTURE_SIDE_DATUM_0]` (L193) | `Side_Datum_3-1` |
| `[FIXTURE_SIDE_DATUM_1]` (L337) | `Side_Datum_3-2` |
| `[FIXTURE_SIDE_DATUM_2]` (L481) | `Side_Datum_4-1` |
| `[FIXTURE_SIDE_DATUM_3]` (L625) | `Side_Datum_4-2` |
| `[FIXTURE_BOTTOM_DATUM_0]` (L773) | `Bottom_Datum` |

### SIDE 시퀀스가 소유한 Shot 7개 (`OwnerSequenceName=SIDE`)

| 섹션 | ShotName | ZIndex | 참조 DatumRef | 측정 수 |
|---|---|---|---|---|
| `[SHOT_3]` (L4790) | `SHOT_3-1` | 0 | `Side_Datum_3-1` | 2 |
| `[SHOT_4]` (L4956) | `SHOT_3-2-1` | 0 | `Side_Datum_3-2` | 2 |
| `[SHOT_5]` (L5122) | `SHOT_3-2-2` | 0 | `Side_Datum_3-2` | 2 |
| `[SHOT_6]` (L5288) | **`SHOT_4-1-1`** | 0 | **`Side_Datum_4-1`** | 6 |
| `[SHOT_22]` (L8109) | **`SHOT_4-1-2`** | 0 | **`Side_Datum_4-1`** | 3 |
| `[SHOT_23]` (L8314) | `SHOT_4-2-1` | 0 | `Side_Datum_4-2` | 1 |
| `[SHOT_24]` (L8441) | `SHOT_4-2-2` | 0 | `Side_Datum_4-2` | 6 |

### 확정 사실 3가지

1. **고아/미해석 `DatumRef` 는 0건이다.** 파일 전체의 `DatumRef=` 값 122건을 전수 확인했고, 전부 위 6개 Datum 이름 중 하나와 정확히 일치한다(`Top_Datum` 61, `Bottom_Datum` 39, `Side_Datum_4-1` 9, `Side_Datum_4-2` 7, `Side_Datum_3-2` 4, `Side_Datum_3-1` 2). → **레시피 데이터 수정 태스크가 필요 없다.** RESEARCH.md 의 "stale DatumRef" 주장은 실제 파일과 무관한 내용이었다.

2. **`Mirror` 문자열이 파일 전체에 0건이다** (대소문자 무시 검색). 즉 `MirrorX=`/`MirrorY=` 키가 아직 저장돼 있지 않다 — 이 레시피는 2026-07-29 저장분이고 Part 1(b49d14f)은 2026-08-13 커밋이라 시점상 당연하다. 다음 번 레시피 저장 때 키가 새로 기록된다.

3. **위 2번이 곧 회귀 0 의 구조적 근거다.** 키 부재 → `ParamBase.Load` 의 Boolean case 가 `false` 를 넣고, C# 초기값도 `false` 다(`DatumConfig.cs:233-234` 주석이 명시). 두 경로 모두 `false` → 모든 Datum 이 무미러 → `BuildGrabRoleIdentifier` 가 base 이름을 **그대로** 반환 → `cam.GrabHalconImage(param.DeviceName)` 와 완전히 동일한 인자. **사용자가 직접 미러를 켜기 전까지는 grab 인자가 바이트 단위로 변경 전과 같다.**

## Part 1 경고 문구에 대한 메모 (코드 변경 아님)

`DatumConfig.cs:238,252` 의 사용자 안내 문구는 "프로그램을 다시 시작해야 적용된다" 라고 말한다. 그런데 `MilCamera.cs:322-323` 은 **매 grab 직전마다** `MdigControl(M_GRAB_DIRECTION_X/Y)` 를 재적용한다(quick-260805-jtj 이후 확립된 기존 동작). 설계 A 는 레시피 로드에 의존하지 않으므로 실제로는 **재시작 없이도 즉시 반영된다.**

→ 이건 기능상 **무해한 과잉보수 문구**다. `DatumConfig.cs` 는 수정 금지 대상이므로 **이번 작업에서 고치지 않는다.** SUMMARY 에 "알려진 기존 문구 부정확 (무해, 이번 범위 밖)" 으로만 기록하고, Task 3 실기 검증에서 재시작 없이도 되는지 관찰만 남긴다.

## 빌드 검증 규약 (Task 1·2 공통 — 반드시 이 형태로)

- **정상 출력 폴더(`bin/x64/*`)로 빌드하지 마라.** 지금 이 PC 에서 `DatumMeasurement.exe` 가 실행 중이라 Debug/x64 정상 경로 빌드는 `MSB3027`/`MSB3021`(파일 잠김)로 **실패한다**(plan-checker 실측 확인, PID 31328). 프로젝트 규칙상 **프로세스를 죽이는 것은 금지**다.
- 그래서 이 plan 의 모든 컴파일 검증은 **스크래치 OutDir + `-t:Rebuild`** 로 한다. 잠김을 원천 회피하면서 매번 전체 컴파일이라 warning 수가 증분빌드에 따라 흔들리지 않는다.
- 성공 판정은 **MSBuild 프로세스 종료 코드**로 한다. `-v:minimal -nologo` 는 "Build succeeded." 문자열을 출력하지 않으므로 문자열 판정은 무조건 실패한다.
- 경로는 **슬래시 방향을 섞지 말 것.** 이 plan 은 **forward slash + 끝에 `/`** 로 통일한다(quick-260813-fdt 에서 실제 성공한 형태).
  - ❌ `-p:OutputPath="C:\...\bin\"` — bash 큰따옴표 안의 끝 `\"` 가 따옴표를 이스케이프해서 명령이 깨진다
  - ❌ `//p:OutputPath=...` — Git Bash(MSYS)가 UNC 경로로 오인해 `MSB1001`
  - ✅ `-p:OutputPath="$SCRATCH/jnh-bin-debug/"`
- **Debug|x64 는 `SIMUL_MODE` 를 정의**해서 MIL 등록 분기(`#else`)를 컴파일조차 하지 않는다. **Release|x64 만이 신규 MIL 코드의 유일한 컴파일 검증 수단**이다. 둘 다 돌린다.
- Debug/x64 warning baseline = **12** (이 저장소의 오래된 고정값, quick-260813-fdt 재확인).
- Release/x64 warning baseline = **사전 미확정값**. plan-checker 가 오늘 측정한 값은 `errors=0, warnings=10` 이지만, **믿을 기준은 Task 1 이 수정 전에 직접 측정해 파일로 기록한 값**이다.
</context>

<tasks>

<task type="auto">
  <name>Task 1: MIL 미러 역할 4종 정적 등록 + 2-인자 grab 오버로드 (인프라)</name>
  <files>WPF_Example/Custom/Device/DeviceHandler.cs, WPF_Example/Device/DeviceHandler.cs</files>
  <action>
### (0) ⚠ 코드를 한 글자도 고치기 **전에** Release/x64 baseline 을 측정해 파일로 기록한다

이 단계를 건너뛰면 Task 1·2 의 `<verify>` 가 `RELEASE_BASELINE=MISSING` 으로 실패한다. 기억에 의존하지 말고 **반드시 파일에 남겨라.**

```bash
MSB="C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/MSBuild.exe"
PROJ="C:/Info/Project/DataMeasurement/WPF_Example/DatumMeasurement.csproj"
SCRATCH="C:/Users/tech/AppData/Local/Temp/claude/C--Info-Project-DataMeasurement/9d3a7b4d-2314-4b14-8686-52fd6346a1f9/scratchpad"
mkdir -p "$SCRATCH"

# 수정 전 Release/x64 — 스크래치 OutDir 로 전체 재빌드 (bin/x64 잠김 회피 + 전체 컴파일 보장)
"$MSB" "$PROJ" -t:Rebuild -p:Configuration=Release -p:Platform=x64 -v:minimal -nologo \
  -p:OutputPath="$SCRATCH/jnh-bin-release/" > "$SCRATCH/jnh-baseline-release.log" 2>&1
RC=$?
echo "BASELINE_BUILD_RC=$RC"
echo "BASELINE_ERRORS=$(grep -c ': error' "$SCRATCH/jnh-baseline-release.log")"
grep -c 'warning CS' "$SCRATCH/jnh-baseline-release.log" > "$SCRATCH/jnh-release-baseline.txt"
echo "BASELINE_WARN_CS_RECORDED=$(cat "$SCRATCH/jnh-release-baseline.txt")"
```

`BASELINE_BUILD_RC` 가 0 이 아니면 **여기서 멈추고 사용자에게 보고하라** — 수정 전 코드가 이미 안 빌드된다는 뜻이므로 이번 작업의 전제가 깨진다. (참고: plan-checker 는 오늘 `RC=0 / errors=0 / warnings=10` 을 관측했다.)

### (1) `WPF_Example/Custom/Device/DeviceHandler.cs` — 상수 + 순수 헬퍼 3개 추가

`RegisterCxpCamera`(:115-126) 아래에 추가한다. 이 파일에는 `#if` 를 넣지 마라(MIL 타입 참조 0 — `DeviceInfo` 만 다룬다).

```csharp
// quick-260813-jnh: 미러 조합은 (X,Y) 불리언 2개 = 최대 4가지뿐이라, 레시피를 스캔하지 않고 앱 시작 시
//  4가지 역할을 전부 정적 등록해 둔다. 레시피 로드 타이밍(카메라 초기화보다 한참 뒤)과 _roleInfoMap 의
//  stale 역할 문제를 동시에 회피하는 유일한 저위험 설계.
public const string MIRROR_ROLE_SUFFIX_X  = "#MX";
public const string MIRROR_ROLE_SUFFIX_Y  = "#MY";
public const string MIRROR_ROLE_SUFFIX_XY = "#MXY";

// 미러 플래그 → grab 역할 식별자. 둘 다 꺼짐이면 기존 식별자를 그대로 돌려준다(회귀 0의 근거).
public static string BuildGrabRoleIdentifier(string szBaseDeviceName, bool bMirrorX, bool bMirrorY) {
    if (string.IsNullOrEmpty(szBaseDeviceName)) return szBaseDeviceName;
    if (bMirrorX && bMirrorY) return szBaseDeviceName + MIRROR_ROLE_SUFFIX_XY;
    if (bMirrorX)             return szBaseDeviceName + MIRROR_ROLE_SUFFIX_X;
    if (bMirrorY)             return szBaseDeviceName + MIRROR_ROLE_SUFFIX_Y;
    return szBaseDeviceName;
}

// 기준 역할(무미러)로부터 미러 3조합의 DeviceInfo 클론을 만든다. 기준값(REVERSE_X_SIDE 등)의 논리 반대가 미러다.
private List<DeviceInfo> BuildMirrorRoleInfos(DeviceInfo baseInfo) { ... 3개 Add ... }

// DeviceInfo 생성자가 TriggerSource 를 대입하지 않는 기존 결함이 있어(:28-39) 클론 후 명시 복사한다.
//  원본 생성자는 다른 호출부 회귀 위험 때문에 고치지 않는다.
private DeviceInfo CloneRoleInfo(DeviceInfo baseInfo, string szSuffix, bool bReverseX, bool bReverseY) { ... }
```

`CloneRoleInfo` 는 `new DeviceInfo(baseInfo.CamType, baseInfo.ImageType, baseInfo.TriggerSource, baseInfo.Identifier + szSuffix, baseInfo.Width, baseInfo.Height, bReverseX, bReverseY, baseInfo.RotateAngle)` 로 만든 뒤 `clone.TriggerSource = baseInfo.TriggerSource;` 를 반드시 넣어라.

`BuildMirrorRoleInfos` 의 3조합(기준값의 논리 반대):
| 접미사 | ReverseX | ReverseY |
|---|---|---|
| `#MX`  | `!baseInfo.ReverseX` | `baseInfo.ReverseY` |
| `#MY`  | `baseInfo.ReverseX`  | `!baseInfo.ReverseY` |
| `#MXY` | `!baseInfo.ReverseX` | `!baseInfo.ReverseY` |

`baseInfo == null` 또는 `Identifier` 가 비면 빈 리스트를 돌려준다.

### (2) `WPF_Example/Device/DeviceHandler.cs:221-249` — MIL 분기에서 미러 역할 등록

`#else`(비-SIMUL) 블록만 수정한다. 두 경로(공유/신규)가 각각 등록을 중복 호출하지 않도록 지역 변수 하나로 합친 뒤 **if/else 밖에서 한 번만** 루프를 돈다:

```csharp
MilCamera sharedMil = Devices.Values.FirstOrDefault(c => c.CamType == ECameraType.MIL) as MilCamera;
MilCamera registeredMil = null;
if (sharedMil != null) {
    sharedMil.RegisterRoleInfo(id);
    Devices.Add(id.Identifier, sharedMil);
    registeredMil = sharedMil;
}
else {
    MilCamera newCam = new MilCamera(Config, id);
    if (!newCam.Open()) {
        result &= ~EInitializeResult.Success;
        result |= EInitializeResult.OpenFail;
        continue;                       // ← Open 실패 시 아래 등록도 건너뛴다(의도된 동작)
    }
    Devices.Add(id.Identifier, newCam);
    registeredMil = newCam;
}
// quick-260813-jnh: 이 역할의 미러 3조합을 _roleInfoMap 에만 추가 등록한다.
//  Devices 딕셔너리에는 절대 넣지 않는다 — ShotConfig.DeviceName 이 INI 에 영속 저장되는 값이고,
//  CameraParam.DeviceNameList 가 Devices 로 UI 드롭다운을 만들기 때문(가짜 장치 노출 금지).
foreach (DeviceInfo mirrorInfo in BuildMirrorRoleInfos(id)) {
    registeredMil.RegisterRoleInfo(mirrorInfo);
}
```

`SetRequiredDevice` 는 **호출하지 않는다**(그건 `IDList` → `Devices` 경로다).

**등록 범위 판단(명시적 결정):** `BuildMirrorRoleInfos` 는 등록되는 MIL 역할 **모두**에 대해 호출된다. `CameraRole.Side`(PC2)에서는 `CAM_SIDE` 하나만 등록되므로 **정확히 SIDE 역할 4종**(`CAM_SIDE`, `CAM_SIDE#MX`, `CAM_SIDE#MY`, `CAM_SIDE#MXY`)이 만들어진다 — 요구사항 충족. PC1(TopBottom)에서도 같은 코드가 TOP/BOTTOM 변형을 만들지만, **기본 역할의 값은 한 비트도 바뀌지 않고**(상수 무수정) 변형 역할은 어떤 Datum 도 MirrorX/Y 를 켜지 않는 한 조회되지 않는다 → TOP/BOTTOM 동작 무변경. 특수 분기(SIDE 전용)를 넣지 않는 편이 오히려 안전한 이유: 미등록 식별자는 `ResolveRoleInfo` 가 **기본 `Info` 로 조용히 폴백**하는데, 공유 인스턴스에서 `Info` 는 첫 등록 역할(PC1 이면 TOP)이라 BOTTOM 이 TOP 의 방향을 쓰게 되는 함정이 생긴다. 전 역할 등록이 그 함정을 원천 제거한다.

### (3) `WPF_Example/Device/DeviceHandler.cs:329-335` — 2-인자 오버로드 신설

기존 1-인자는 **시그니처·동작 모두 유지**하고 신규 오버로드로 위임시킨다(호출부 회귀 0):

```csharp
public HImage GrabHalconImage(ICameraParam param) {
    return GrabHalconImage(param, param.DeviceName);   // 기존과 완전히 동일한 동작
}

// quick-260813-jnh: 카메라 조회(param.DeviceName)와 grab 역할 해석(requestIdentifier)을 분리한다.
//  SIMUL 의 VirtualCamera 는 requestIdentifier 를 무시하므로(VirtualCamera.cs:460-462) 시뮬 회귀 위험 0.
public HImage GrabHalconImage(ICameraParam param, string requestIdentifier) {
    VirtualCamera cam = this[param.DeviceName];        // ← 조회는 계속 base 이름으로
    if (cam == null) return null;
    if (cam.Properties == null) return null;
    if (!cam.Properties.ApplyFromParam(param)) return null;
    return cam.GrabHalconImage(requestIdentifier);
}
```
⚠ 기존 1-인자에 `param == null` 가드는 없었다(호출부가 이미 null 체크). **가드를 새로 추가하지 마라** — 동작 변경 없이 위임만 한다.
  </action>
  <verify>
    <automated>
MSB="C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/MSBuild.exe"
PROJ="C:/Info/Project/DataMeasurement/WPF_Example/DatumMeasurement.csproj"
SCRATCH="C:/Users/tech/AppData/Local/Temp/claude/C--Info-Project-DataMeasurement/9d3a7b4d-2314-4b14-8686-52fd6346a1f9/scratchpad"

echo "===== [0] 수정 전 Release baseline 이 파일로 기록돼 있는가 ====="
if [ -f "$SCRATCH/jnh-release-baseline.txt" ]; then
  BASE_R=$(cat "$SCRATCH/jnh-release-baseline.txt")
else
  BASE_R="MISSING"
fi
echo "RELEASE_BASELINE=$BASE_R"

echo "===== [1] Debug/x64 (SIMUL_MODE) — 스크래치 OutDir Rebuild (bin 잠김 회피, 프로세스 종료 금지) ====="
"$MSB" "$PROJ" -t:Rebuild -p:Configuration=Debug -p:Platform=x64 -v:minimal -nologo \
  -p:OutputPath="$SCRATCH/jnh-bin-debug/" > "$SCRATCH/jnh-t1-debug.log" 2>&1
RC_D=$?
echo "DEBUG_BUILD_RC=$RC_D"
echo "DEBUG_ERRORS=$(grep -c ': error' "$SCRATCH/jnh-t1-debug.log")"
WARN_D=$(grep -c 'warning CS' "$SCRATCH/jnh-t1-debug.log")
echo "DEBUG_WARN_CS=$WARN_D (기대 12)"
if [ "$RC_D" != "0" ]; then echo "MSB3027_HINT: 잠김이면 OutputPath 가 스크래치인지 확인. 절대 프로세스를 죽이지 말 것"; fi

echo "===== [2] Release/x64 (비-SIMUL, MIL 분기 실제 컴파일) — 스크래치 OutDir Rebuild ====="
"$MSB" "$PROJ" -t:Rebuild -p:Configuration=Release -p:Platform=x64 -v:minimal -nologo \
  -p:OutputPath="$SCRATCH/jnh-bin-release/" > "$SCRATCH/jnh-t1-release.log" 2>&1
RC_R=$?
echo "RELEASE_BUILD_RC=$RC_R"
echo "RELEASE_ERRORS=$(grep -c ': error' "$SCRATCH/jnh-t1-release.log")"
WARN_R=$(grep -c 'warning CS' "$SCRATCH/jnh-t1-release.log")
echo "RELEASE_WARN_CS=$WARN_R"
if [ "$WARN_R" = "$BASE_R" ]; then echo "RELEASE_WARN_MATCH=YES"; else echo "RELEASE_WARN_MATCH=NO (baseline=$BASE_R, now=$WARN_R)"; fi

cd "C:/Info/Project/DataMeasurement"
echo "===== [3] 신규 심볼 존재 ====="
grep -n "BuildGrabRoleIdentifier\|BuildMirrorRoleInfos\|CloneRoleInfo\|MIRROR_ROLE_SUFFIX" WPF_Example/Custom/Device/DeviceHandler.cs
grep -n "GrabHalconImage(ICameraParam param, string requestIdentifier)\|registeredMil" WPF_Example/Device/DeviceHandler.cs

echo "===== [4] 금지 패턴 (전부 0건이어야 함) ====="
echo "Devices.Add 미러키=$(git diff -- WPF_Example/Device/DeviceHandler.cs | grep -c '^+.*Devices.Add.*#M')"
echo "SetRequiredDevice 신규=$(git diff -- WPF_Example/Custom/Device/DeviceHandler.cs WPF_Example/Device/DeviceHandler.cs | grep -c '^+.*SetRequiredDevice')"
echo "삼항=$(git diff -- WPF_Example/Custom/Device/DeviceHandler.cs WPF_Example/Device/DeviceHandler.cs | grep '^+' | grep -c '[^?]? [^ ]* : ')"
echo "REVERSE/ROTATE 상수변경=$(git diff -- WPF_Example/Custom/Device/DeviceHandler.cs | grep -c '^-.*REVERSE_\|^-.*ROTATE_')"
echo "MilCamera.cs 수정=$(git diff --name-only | grep -c 'MilCamera.cs')"
echo "DatumConfig.cs 수정=$(git diff --name-only | grep -c 'DatumConfig.cs')"

echo "===== [5] 레시피 무편집 확인 (정보성 — 이 plan 은 레시피에 write 하지 않는다) ====="
ls -la "D:/Data/Recipe/FAI_1/main.ini"
echo "기준값(2026-08-13 실측): 261714 bytes / 11169 lines / mtime 2026-07-29 17:40"
    </automated>
  </verify>
  <done>
`RELEASE_BASELINE` 이 `MISSING` 이 아니다 (수정 전 측정값이 `jnh-release-baseline.txt` 에 기록됨).
Debug/x64: `DEBUG_BUILD_RC=0`, `DEBUG_ERRORS=0`, `DEBUG_WARN_CS=12`.
Release/x64: `RELEASE_BUILD_RC=0`, `RELEASE_ERRORS=0`, `RELEASE_WARN_MATCH=YES` (파일에 기록된 수정 전 baseline 과 정확히 일치 — 증가 0).
`BuildGrabRoleIdentifier`/`BuildMirrorRoleInfos`/`CloneRoleInfo`/`MIRROR_ROLE_SUFFIX_*` 가 Custom/Device/DeviceHandler.cs 에 존재. 2-인자 `GrabHalconImage` 오버로드 + `registeredMil` 루프가 Device/DeviceHandler.cs 에 존재.
금지 패턴 전부 0건: 미러 키의 `Devices.Add` 없음, 신규 `SetRequiredDevice` 없음, 삼항 없음, `REVERSE_*`/`ROTATE_*` 상수 변경 없음, `MilCamera.cs`/`DatumConfig.cs` 무수정.
빌드가 실행 중 프로세스를 죽이지 않고 스크래치 OutDir 로 성공했다.
  </done>
</task>

<task type="auto">
  <name>Task 2: Shot→Datum 미러 역추적(fail-safe) + grab 호출부 5곳 배선</name>
  <files>WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs, WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs, WPF_Example/UI/ContentItem/MainView.xaml.cs</files>
  <action>
### (1) `InspectionSequence.cs` — `ResolveShotGrabMirror` 신설

`IsDatumRefUnresolvable`(:2034-2043) **바로 아래**에 붙이고 **Allman brace 스타일**을 따른다.

```csharp
// quick-260813-jnh: Shot 검사이미지 grab 의 미러 방향 해석. 미러 플래그는 DatumConfig 에만 있고 ShotConfig 에는
//  없으므로, 이 Shot 의 측정들이 참조하는 DatumRef 로 소유 Datum 을 역추적한다(레시피에 실재하는 유일한 연결고리 —
//  '+1 규칙' 같은 새 규약을 발명하지 않는다). 한 물리 포즈를 Datum 검출용 Shot 과 측정용 Shot 이 나눠 쓰는 구조라,
//  Datum 만 미러하고 Shot 을 안 하면 미러된 좌표계로 안 뒤집힌 이미지를 측정하게 되어 전 항목이 어긋난다.
// ※ 해석 실패는 전부 fail-safe(무미러) + Error 로그. 2026-08-13 기준 라이브 레시피에는 미해석 DatumRef 가
//    0건임을 전수 확인했지만, Datum 개명/삭제로 언제든 생길 수 있는 종류의 결함이라 방어한다.
public void ResolveShotGrabMirror(ShotConfig shot, out bool bMirrorX, out bool bMirrorY)
```

동작 규칙(정확히 이대로):
1. `bMirrorX = false; bMirrorY = false;` 로 먼저 초기화한다. **어떤 이탈 경로에서도 무미러가 기본값이다.**
2. `shot == null` 또는 `shot.FAIList == null` 또는 `DatumConfigs == null` → 그대로 반환(로그 없음).
3. `shot.FAIList` → `fai.Measurements` 를 순회한다(null 원소 skip).
4. `string.IsNullOrEmpty(meas.DatumRef)` → **의도된 무보정이므로 skip, 경고 없음**(`IsDatumRefUnresolvable` 과 동일 규약).
5. `DatumRef` 로 `DatumConfigs` 에서 이름 일치 `DatumConfig` 를 찾는다. 조회는 `IsDatumRefUnresolvable`(:2038-2041) 과 **동일한 `foreach` 패턴**을 쓴다 — 중복을 줄이려면 `private DatumConfig FindDatumByName(string szDatumName)` 를 뽑아 두 곳이 공유해도 좋다(다만 `IsDatumRefUnresolvable` 의 **반환 계약은 절대 바꾸지 마라**, 기존 NG 승격 게이트가 의존한다).
6. **못 찾으면**: `bMirrorX/bMirrorY` 를 false 로 되돌리고 아래 로그를 남긴 뒤 **즉시 return**(fail-safe).
   ```csharp
   Logging.PrintLog((int)ELogType.Error, "[ShotMirror] SHOT '" + (shot.ShotName ?? "") + "' 의 DatumRef '" + meas.DatumRef + "' 에 해당하는 Datum 이 레시피에 없음 — 미러 미적용(무미러)으로 grab. Datum 개명/삭제 확인 필요.");
   ```
7. **처음 찾은** Datum 의 `MirrorX`/`MirrorY` 를 채택한다.
8. 이후 찾은 Datum 의 플래그가 **처음 것과 다르면**: 어느 쪽이 맞는지 알 수 없으므로 false 로 되돌리고 아래 로그 후 즉시 return(fail-safe).
   ```csharp
   Logging.PrintLog((int)ELogType.Error, "[ShotMirror] SHOT '" + (shot.ShotName ?? "") + "' 이 미러 설정이 서로 다른 Datum 을 함께 참조 — 미러 미적용(무미러)으로 grab. 레시피 확인 필요.");
   ```

로그 빈도: 이탈 시 **즉시 return** 하므로 grab 1회당 최대 1줄이다. 기존 `MarkMeasurementDatumRefMissing`(Action_FAIMeasurement.cs:1048)이 같은 조건을 이미 measurement 단위로 로깅하고 있으므로 새로운 노이즈 선례를 만드는 것이 아니다.

참고(설계 타당성): 라이브 레시피의 SIDE Shot 7개는 전부 **한 Shot 이 단일 Datum 만** 참조한다(`SHOT_4-1-1`→`Side_Datum_4-1` 6건, `SHOT_4-1-2`→`Side_Datum_4-1` 3건 등). 즉 8번(혼재) 분기는 현재 데이터에서 발동하지 않는 순수 방어 코드다.

30~40줄을 넘으면 내부 measurement 루프를 private sub-헬퍼로 분리해라.

### (2) `Action_FAIMeasurement.cs` — 생산 경로 2곳

**(a) `:276` `EStep.Grab` 의 `#else` 라이브 grab 분기** (`bIsLiveGrabAttempt = true;` 직후)
```csharp
bIsLiveGrabAttempt = true;
InspectionSequence parentSeqForMirror = ShotParam.Parent as InspectionSequence;   // :283 에 동일 선례
bool bShotMirrorX = false;
bool bShotMirrorY = false;
if (parentSeqForMirror != null) parentSeqForMirror.ResolveShotGrabMirror(ShotParam, out bShotMirrorX, out bShotMirrorY);
string szShotRoleId = DeviceHandler.BuildGrabRoleIdentifier(ShotParam.DeviceName, bShotMirrorX, bShotMirrorY);
image = SystemHandler.Handle.Devices.GrabHalconImage(ShotParam, szShotRoleId);
```
아래 `image == null && bIsLiveGrabAttempt` 하드웨어 에러 처리 블록(:282-286)은 **그대로 둔다.**

**(b) `:570` `GrabOrLoadDatumImage(DatumConfig datum)` 의 `#else` else 분기** — 여기는 `datum` 객체를 손에 쥐고 있으므로 역추적 불필요, **직접 읽는다**:
```csharp
bool bDatumMirrorX = false;
bool bDatumMirrorY = false;
if (datum != null) {
    bDatumMirrorX = datum.MirrorX;
    bDatumMirrorY = datum.MirrorY;
}
string szDatumRoleId = DeviceHandler.BuildGrabRoleIdentifier(ShotParam.DeviceName, bDatumMirrorX, bDatumMirrorY);
image = SystemHandler.Handle.Devices.GrabHalconImage(ShotParam, szDatumRoleId);
```
아래 null → `MarkCycleHardwareError` 블록(:573-579)은 **그대로 둔다.**

**(c) `:597` `LoadDatumImageFromPath` 안의 grab 폴백은 수정하지 마라.** `allowGrabFallback=true` 는 SIMUL 경로(:564)에서만 오고, SIMUL 의 `VirtualCamera` 는 식별자를 무시한다 → 배선해도 효과 0, diff 만 늘어난다.

### (3) `MainView.xaml.cs` — 티칭 경로 3곳

세 곳이 같은 로직을 쓰므로 **private static 헬퍼 하나**를 만들고 각 호출부는 한 줄만 바꾼다. 헬퍼는 세 `GrabAndDisplay`/`GrabSaveAndDisplay` 근처(예: `:1196` 오버로드 바로 위)에 두고, 파일 스타일대로 **K&R**:

```csharp
// quick-260813-jnh: grab 역할 식별자 산출. Datum 노드 grab 이면 그 Datum 의 미러를 직접 쓰고,
//  Shot 노드 grab 이면 Shot→DatumRef 역추적으로 소유 Datum 의 미러를 따라간다. 티칭에서 저장한
//  검사이미지를 OfflineInspectMode 검사가 그대로 로드하므로, 여기서 방향이 어긋나면 오프라인 결과가
//  실기와 달라진다 — 그래서 Shot 경로도 생산과 같은 규칙을 쓴다.
private static string ResolveGrabRoleIdentifier(ICameraParam param, DatumConfig datum) {
    if (param == null) return null;
    bool bMirrorX = false;
    bool bMirrorY = false;
    if (datum != null) {
        bMirrorX = datum.MirrorX;
        bMirrorY = datum.MirrorY;
    }
    else if (param is ShotConfig shotForMirror) {
        InspectionSequence mirrorSeq = SystemHandler.Handle.Sequences[param.SequenceName] as InspectionSequence;  // :1208/1271/1363 동일 선례
        if (mirrorSeq != null) mirrorSeq.ResolveShotGrabMirror(shotForMirror, out bMirrorX, out bMirrorY);
    }
    return DeviceHandler.BuildGrabRoleIdentifier(param.DeviceName, bMirrorX, bMirrorY);
}
```

호출부 3곳(전부 `lock (mDrawInterlock)` **안**, 기존 줄을 그 자리에서 교체 — 락 범위/데드락 규약을 절대 바꾸지 마라):
| 위치 | 기존 | 변경 후 |
|---|---|---|
| `:1216` `GrabAndDisplay(param, eventCall)` | `pDev.GrabHalconImage(param)` | `pDev.GrabHalconImage(param, ResolveGrabRoleIdentifier(param, null))` |
| `:1283` `GrabAndDisplay(param, datum, eventCall)` | `pDev.GrabHalconImage(param)` | `pDev.GrabHalconImage(param, ResolveGrabRoleIdentifier(param, datum))` |
| `:1375` `GrabSaveAndDisplay(...)` | `pDev.GrabHalconImage(param)` | `pDev.GrabHalconImage(param, ResolveGrabRoleIdentifier(param, datum))` |

`:3292` 근처 캘리브레이션 grab 등 **그 외 grab 호출부는 손대지 마라** — 1-인자 오버로드가 그대로 유지되므로 자동으로 기존 동작이다.

### 회귀 0 의 구조적 근거 (SUMMARY 에 기록할 것)
라이브 레시피(`main.ini`, 2026-08-13 실측)에는 `MirrorX=`/`MirrorY=` 키가 **0건**이다(파일 전체 대소문자 무시 `mirror` 검색 0건 — 레시피 최종 저장 2026-07-29 이 Part 1 커밋 b49d14f 보다 앞선다). 키 부재 시 `ParamBase.Load` 의 Boolean case 가 `false` 를 넣고 C# 초기값도 `false` 이므로(`DatumConfig.cs:233-234` 주석이 명시), 현재 모든 Datum 은 무미러다. `BuildGrabRoleIdentifier` 는 둘 다 false 면 **base 이름을 그대로** 돌려주고, 그러면 `cam.GrabHalconImage(param.DeviceName)` 와 완전히 동일한 인자가 된다 → 사용자가 직접 미러를 켜기 전까지 모든 기존 Datum/Shot 은 변경 전과 **바이트 단위로 같은 역할**로 grab 된다.
  </action>
  <verify>
    <automated>
MSB="C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/MSBuild.exe"
PROJ="C:/Info/Project/DataMeasurement/WPF_Example/DatumMeasurement.csproj"
SCRATCH="C:/Users/tech/AppData/Local/Temp/claude/C--Info-Project-DataMeasurement/9d3a7b4d-2314-4b14-8686-52fd6346a1f9/scratchpad"

echo "===== [0] Task 1 이 기록한 수정 전 Release baseline 읽기 ====="
if [ -f "$SCRATCH/jnh-release-baseline.txt" ]; then
  BASE_R=$(cat "$SCRATCH/jnh-release-baseline.txt")
else
  BASE_R="MISSING"
fi
echo "RELEASE_BASELINE=$BASE_R"

echo "===== [1] Debug/x64 — 스크래치 OutDir Rebuild (bin 잠김 회피, 프로세스 종료 금지) ====="
"$MSB" "$PROJ" -t:Rebuild -p:Configuration=Debug -p:Platform=x64 -v:minimal -nologo \
  -p:OutputPath="$SCRATCH/jnh-bin-debug/" > "$SCRATCH/jnh-t2-debug.log" 2>&1
RC_D=$?
echo "DEBUG_BUILD_RC=$RC_D"
echo "DEBUG_ERRORS=$(grep -c ': error' "$SCRATCH/jnh-t2-debug.log")"
echo "DEBUG_WARN_CS=$(grep -c 'warning CS' "$SCRATCH/jnh-t2-debug.log") (기대 12)"
if [ "$RC_D" != "0" ]; then echo "MSB3027_HINT: 잠김이면 OutputPath 가 스크래치인지 확인. 절대 프로세스를 죽이지 말 것"; fi

echo "===== [2] Release/x64 (비-SIMUL 분기 컴파일) — 스크래치 OutDir Rebuild ====="
"$MSB" "$PROJ" -t:Rebuild -p:Configuration=Release -p:Platform=x64 -v:minimal -nologo \
  -p:OutputPath="$SCRATCH/jnh-bin-release/" > "$SCRATCH/jnh-t2-release.log" 2>&1
RC_R=$?
echo "RELEASE_BUILD_RC=$RC_R"
echo "RELEASE_ERRORS=$(grep -c ': error' "$SCRATCH/jnh-t2-release.log")"
WARN_R=$(grep -c 'warning CS' "$SCRATCH/jnh-t2-release.log")
echo "RELEASE_WARN_CS=$WARN_R"
if [ "$WARN_R" = "$BASE_R" ]; then echo "RELEASE_WARN_MATCH=YES"; else echo "RELEASE_WARN_MATCH=NO (baseline=$BASE_R, now=$WARN_R)"; fi

cd "C:/Info/Project/DataMeasurement"
echo "===== [3] 배선 확인 ====="
echo "ResolveShotGrabMirror 정의=$(grep -c 'public void ResolveShotGrabMirror' WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs)"
echo "ShotMirror 경고로그=$(grep -c '\[ShotMirror\]' WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs)"
echo "FAIMeasurement 2-인자 grab=$(grep -c 'GrabHalconImage(ShotParam, sz' WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs)"
echo "MainView 2-인자 grab=$(grep -c 'GrabHalconImage(param, ResolveGrabRoleIdentifier' WPF_Example/UI/ContentItem/MainView.xaml.cs)"
echo "--- 남아있는 1-인자 grab (MainView 3곳은 0이어야 함) ---"
grep -n "pDev.GrabHalconImage(param)" WPF_Example/UI/ContentItem/MainView.xaml.cs || echo "none"

echo "===== [4] 금지 패턴 (전부 0건) ====="
echo "HALCON 소프트미러=$(git diff | grep '^+' | grep -ci 'mirror_image\|MirrorImage\|RotateImage')"
echo "삼항=$(git diff | grep '^+' | grep -c '[^?]? [^ ]* : ')"
echo "금지파일 수정=$(git diff --name-only | grep -c 'MilCamera.cs\|DatumConfig.cs')"
echo "IsDatumRefUnresolvable 계약변경=$(git diff -- WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs | grep -c '^-.*IsDatumRefUnresolvable')"
echo "--- 변경 파일 목록 (예상: 이 plan 의 5개 소스 + 무관한 기존 미커밋 PickerCenterCalibrationService.cs) ---"
git diff --name-only

echo "===== [5] 레시피 무편집 확인 (정보성) ====="
ls -la "D:/Data/Recipe/FAI_1/main.ini"
echo "기준값(2026-08-13 실측): 261714 bytes / 11169 lines / mtime 2026-07-29 17:40"
    </automated>
  </verify>
  <done>
`RELEASE_BASELINE` 이 `MISSING` 이 아니다.
Debug/x64: `DEBUG_BUILD_RC=0`, `DEBUG_ERRORS=0`, `DEBUG_WARN_CS=12`.
Release/x64: `RELEASE_BUILD_RC=0`, `RELEASE_ERRORS=0`, `RELEASE_WARN_MATCH=YES` (파일에 기록된 수정 전 baseline 과 일치).
`ResolveShotGrabMirror` 정의 1건 + `[ShotMirror]` 로그 2건 존재. `Action_FAIMeasurement.cs` 2-인자 grab 2건, `MainView.xaml.cs` 2-인자 grab 3건. `MainView.xaml.cs` 에 남은 1-인자 `pDev.GrabHalconImage(param)` 0건.
금지 패턴 전부 0건: HALCON 소프트웨어 미러 0, 삼항 0, `MilCamera.cs`/`DatumConfig.cs` 무수정, `IsDatumRefUnresolvable` 반환 계약 무변경.
변경 파일이 이 plan 의 5개 소스 파일로 한정됨(기존 미커밋 `PickerCenterCalibrationService.cs` 는 이번 작업과 무관 — 커밋에 포함하지 말 것). `D:/Data/Recipe/**` 에 어떤 write 도 하지 않았다.
  </done>
</task>

<task type="checkpoint:human-verify" gate="blocking">
  <name>Task 3: 실기 MIL 하드웨어 미러 육안 확인 (SIMUL 로 대체 불가)</name>
  <files>(코드 변경 없음 — 실기 육안 검증 전용 체크포인트)</files>
  <action>실행을 멈추고 사용자에게 아래 what-built / how-to-verify 내용을 그대로 제시한다. 이 태스크에서는 코드를 추가로 수정하지 마라. 사용자의 resume-signal 응답을 받은 뒤 그 결과(PASS / 실패한 Test 번호 / defer)를 SUMMARY 에 기록한다.</action>
  <what-built>
**로컬에서 이미 검증된 것(재확인 불필요):**
- Debug/x64 + Release/x64 컴파일 통과 (Release 는 SIMUL_MODE 가 없어 MIL 등록 분기가 실제로 컴파일됨). 실행 중인 앱을 죽이지 않고 스크래치 OutDir 로 검증
- MIL 역할 4종(`CAM_SIDE`, `CAM_SIDE#MX`, `CAM_SIDE#MY`, `CAM_SIDE#MXY`) 등록 코드
- Shot→DatumRef→Datum 미러 역추적 + 해석 실패 시 무미러 fail-safe + Error 로그
- HALCON 소프트웨어 미러 0건, 삼항 0건, 운영 레시피 무편집

**로컬에서 원리적으로 검증 불가능한 것 = 아래 확인 대상:**
이 개발 PC 의 Debug 빌드는 `SIMUL_MODE` 라서 `MilCamera` 객체 자체가 생성되지 않고 `VirtualCamera` 로 대체된다. `VirtualCamera.GrabHalconImage(string)` 은 **식별자를 통째로 무시**한다(VirtualCamera.cs:460-462). 즉 시뮬에서는 어떤 식별자를 넘겨도 조용히 무시되므로 회귀 위험은 0 이지만, **실제 반전 동작은 절대 재현되지 않는다.** 물리 CXP 카메라가 붙은 SIDE PC 에서만 확인 가능하다.
  </what-built>
  <how-to-verify>
**아래 Datum/Shot 이름은 2026-08-13 라이브 레시피(`D:\Data\Recipe\FAI_1\main.ini`)를 직접 읽어 확인한 실제 이름이다.** 실기 PC 의 레시피가 다른 파일이면 트리에 보이는 이름을 우선하고, 그 사실을 알려주세요.

**사전 준비 (중요):**
- 실기 SIDE PC(`CameraRole = Side`)에 **Release/x64 빌드**를 배포한다. Debug 빌드는 SIMUL_MODE 라 MIL grab 자체를 안 한다.
- **DeviceSelector 라이브뷰 창을 반드시 닫아라.** 스트리밍 중이면 검사 grab 이 아예 `null` 을 반환한다(`MilCamera.cs:267-270`). 창을 열어둔 채 "미러가 안 먹는다" 로 오진하기 가장 쉬운 함정이다.
- 참고: 라이브 미리보기 화면은 원래 항상 기본 역할을 쓰므로(`MilCamera.cs:500 LiveLoop`) 미러가 적용되지 않는 게 정상이다. 판단 근거로 쓰지 마라.
- 참고: 현재 레시피에는 아직 `MirrorX`/`MirrorY` 키가 **저장돼 있지 않다**(레시피가 Part 1 이전인 2026-07-29 저장분). 키가 없으면 꺼짐(false)으로 읽히므로 정상이며, 아래 Test 1 에서 값을 바꾸고 저장하면 그때 키가 기록된다.

**Test 1 — Datum 이미지 반전 (핵심)**
1. 트리에서 **`Side_Datum_4-1`** 선택 → PropertyGrid `Datum|Mirror` 에서 `MirrorY = True` 로 변경 (경고창 뜨면 읽고 닫기) → 레시피 저장
2. 같은 Datum 노드에서 `검사이미지 Grab` 실행
3. 저장된 bmp 를 열어 **상하가 뒤집혀 있는지** 육안 확인
4. 기대: 뒤집힘. → PASS

**Test 2 — Shot 이미지가 같은 방향으로 따라오는지 (설계 A 의 핵심 가정 검증, 이 작업의 존재 이유)**
1. Test 1 과 같은 상태에서 **`SHOT_4-1-1`** 노드 선택 → `검사이미지 Grab`
2. 이어서 **`SHOT_4-1-2`** 노드도 같은 방식으로 Grab
3. 두 Shot 모두 측정이 `Side_Datum_4-1` 을 참조하므로(레시피 실측: `SHOT_4-1-1` 6건, `SHOT_4-1-2` 3건 전부 `Side_Datum_4-1`), Test 1 의 Datum 이미지와 **동일한 방향으로** 반전돼야 한다
4. 기대: Datum 이미지와 같은 방향. (이게 안 되면 미러된 좌표계로 안 뒤집힌 이미지를 측정하게 되어 전 FAI 가 어긋난다)

**Test 3 — 회귀 0 확인 (가장 중요)**
1. `MirrorX/MirrorY` 를 건드리지 않은 나머지 SIDE Datum 3개와 그에 대응하는 Shot 을 각각 Grab:
   - `Side_Datum_3-1` ↔ `SHOT_3-1`
   - `Side_Datum_3-2` ↔ `SHOT_3-2-1`, `SHOT_3-2-2`
   - `Side_Datum_4-2` ↔ `SHOT_4-2-1`, `SHOT_4-2-2`
2. 기대: 이번 변경 **이전과 완전히 동일한 방향**(=아무 변화 없음)

**Test 4 — 전체 사이클**
1. `Side_Datum_4-1` 의 `MirrorY=True` 를 유지한 채 SIDE 시퀀스 전체 검사 사이클을 1회 실행한다 (TCP `$PREP`/`$TEST` 또는 화면의 수동 실행 중 평소 쓰는 방법)
2. 기대: `Side_Datum_4-1` Datum 검출 성공 + 해당 FAI 측정값이 미러 전과 정합적(부호 뒤집힘 없이 같은 값 계열)
3. 같은 사이클에서 `Side_Datum_3-1` / `3-2` / `4-2` 계열 FAI 측정값도 미러 전과 동일한지 확인(회귀 0)

**Test 5 — fail-safe 로그 확인**
1. 사이클 로그에서 `[ShotMirror]` Error 가 **0건**인지 확인
   (근거: 2026-08-13 실측으로 라이브 레시피의 `DatumRef` 122건이 전부 실재하는 Datum 6개 중 하나와 정확히 일치함을 확인했다 → 정상 상태에서는 0건이어야 한다)
2. (선택) 일부러 어떤 Shot 측정의 `DatumRef` 를 없는 이름으로 바꿔 1회 grab → `[ShotMirror] ... 레시피에 없음 — 미러 미적용(무미러)으로 grab` 로그가 뜨고 이미지는 안 뒤집히는지 확인 → **즉시 원복**

**Test 6 — 재시작 필요 여부 관찰 (기록용, 판정 아님)**
1. `MirrorY` 를 켜고 **앱을 재시작하지 않은 채로** Grab 해본다
2. 관찰 결과를 기록만 한다. `MilCamera.cs:322-323` 은 매 grab 마다 방향을 재적용하므로 **재시작 없이도 반영될 것으로 예상**된다. Part 1 의 "프로그램을 다시 시작해야 적용된다" 안내 문구는 실제보다 보수적인 것으로 보이나, `DatumConfig.cs` 는 이번 작업의 수정 금지 대상이므로 **문구는 고치지 않는다.** 관찰 결과만 SUMMARY 에 남긴다.

**마무리:** Test 1 에서 켠 `Side_Datum_4-1` 의 `MirrorY` 를 실제 운용값(뒤집혀야 하면 True 유지, 아니면 False 로 원복)으로 정리하고 저장해 주세요.
  </how-to-verify>
  <resume-signal>"approved" 입력, 또는 실패한 Test 번호와 관찰 내용을 알려주세요. 실기 카메라가 없어 지금 확인이 불가능하면 "defer" 라고 알려주시면 실기 UAT 미수행 상태로 기록하고 마무리합니다.</resume-signal>
</task>

</tasks>

<threat_model>
## Trust Boundaries

| Boundary | Description |
|----------|-------------|
| 운영 레시피 파일 → 앱 | `D:\Data\Recipe\FAI_1\main.ini` 는 사용자가 편집하는 신뢰 데이터지만, Datum 개명/삭제로 내부 참조가 언제든 깨질 수 있다(현재는 0건) |
| Datum 설정 → 카메라 하드웨어 | `MirrorX/MirrorY` 가 물리 grab 방향을 바꾼다 — 같은 카메라를 쓰는 다른 측정까지 영향 |
| 실행 중 프로세스 → 빌드 산출물 | `bin/x64/Debug` 가 실행 중 앱에 잠긴다. 잘못 대응하면 사용자 프로세스를 죽이는 사고로 이어진다 |

## STRIDE Threat Register

| Threat ID | Category | Component | Disposition | Mitigation Plan |
|-----------|----------|-----------|-------------|-----------------|
| T-JNH-01 | Tampering (운영 데이터 무결성) | `D:\Data\Recipe\**` | mitigate | **이 plan 은 레시피를 읽기만 한다.** 편집 태스크가 존재하지 않으며, Task 1·2 verify 가 파일 크기/줄수/mtime 을 기준값과 함께 출력해 무편집을 눈으로 확인시킨다. (조사 단계에서 보고된 "고아 DatumRef" 는 실측 결과 존재하지 않았고, 근거 없는 데이터 수정이 더 큰 위험이라 판단해 제거함) |
| T-JNH-02 | Information Disclosure (조용한 오검) | `ResolveShotGrabMirror` 참조 해석 실패 | mitigate | fail-safe 무미러 + `ELogType.Error` 경고 로그에 Shot 이름·미해석 DatumRef 명시 (Task 2) |
| T-JNH-03 | Denial of Service (UI 오염) | `Devices` 딕셔너리에 미러 키 추가 시 UI 드롭다운/INI 오염 | mitigate | 미러 역할은 `_roleInfoMap` 에만 등록, `Devices.Add`/`SetRequiredDevice` 미사용 — verify 에서 grep 로 0건 강제 (Task 1) |
| T-JNH-04 | Denial of Service (사용자 작업 파괴) | 빌드 산출물 잠김 대응 중 실행 중인 앱 강제 종료 | mitigate | 모든 컴파일 검증이 스크래치 OutDir 로 나가 잠김 자체가 발생하지 않는다. verify 스크립트에 프로세스 종료 금지 힌트 문구 인라인 |
| T-JNH-05 | Elevation of Privilege | 해당 없음 (로컬 데스크톱 앱, 인증 경계 없음) | accept | 이번 변경은 네트워크/인증 경계를 넘지 않는다 |
| T-JNH-06 | Repudiation | 미러 적용 여부가 로그에 안 남음 | accept | 실패 경로만 로깅. 정상 경로 로깅은 매 grab 노이즈 대비 이득이 없음(육안 확인이 1차 수단) |
</threat_model>

<verification>
1. **빌드 (필수 2종, 둘 다 스크래치 OutDir + `-t:Rebuild`)**
   - Debug/x64: `DEBUG_BUILD_RC=0`, `: error` 0건, `warning CS` **12건**(이 저장소 기존 baseline — 0 아님)
   - Release/x64: `RELEASE_BUILD_RC=0`, `: error` 0건, `RELEASE_WARN_MATCH=YES` — Task 1 이 **수정 전에 측정해 `$SCRATCH/jnh-release-baseline.txt` 에 기록한 값**과 일치. 이 파일이 없으면(`MISSING`) 검증 실패로 간주한다
   - Release 를 반드시 도는 이유: Debug 는 `SIMUL_MODE` 를 정의해서 MIL 등록 분기(`#else`)를 **컴파일조차 하지 않는다**. Release/x64 만이 신규 MIL 코드의 유일한 컴파일 검증 수단이다
   - MSBuild: `C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe`. 성공 판정은 **프로세스 종료 코드**로 한다 — `-v:minimal -nologo` 는 "Build succeeded." 문자열을 아예 출력하지 않는다
   - **잠김 대응:** `bin/x64/*` 로는 아예 빌드하지 않는다. 실행 중인 앱이 Debug 산출물을 잠그고 있고(plan-checker 실측 `MSB3027`/`MSB3021`), 프로젝트 규칙상 **프로세스를 죽이는 것은 금지**다. 스위치는 단일 대시 `-p:` + **forward slash 통일 + 끝에 `/`** (`//p:` 는 MSB1001, 끝 `\"` 는 bash 이스케이프 사고)

2. **금지 패턴 (전부 0건, grep 강제)**
   - HALCON 소프트웨어 미러: `mirror_image` / `MirrorImage` / `RotateImage` 추가 0건
   - 삼항 연산자 `?:` 추가 0건
   - `MilCamera.cs` 수정 0건, `DatumConfig.cs` 수정 0건
   - 미러 식별자에 대한 `Devices.Add` / `SetRequiredDevice` 0건
   - `REVERSE_X_*` / `REVERSE_Y_*` / `ROTATE_*` 상수 변경 0건

3. **레시피 무편집**
   - `D:\Data\Recipe\**` 에 어떤 write 도구도 사용하지 않았다
   - Task 1·2 verify 의 `ls -la` 출력이 기준값(261714 bytes, 최종 저장 2026-07-29 17:40)과 같다. 다르면 앱이 저장한 것인지 확인만 하고, 편집 도구로 만진 적이 없음을 SUMMARY 에 명시한다

4. **실기 (Task 3 체크포인트)** — SIMUL 로 대체 불가. 물리 CXP 카메라가 붙은 SIDE PC 필요.
</verification>

<success_criteria>
- MIL 역할 4종(무미러 기준 + `#MX`/`#MY`/`#MXY`)이 `MilCamera._roleInfoMap` 에 앱 시작 시 등록된다. **`MilCamera.cs` 변경 0줄.**
- `DeviceHandler.GrabHalconImage(ICameraParam, string)` 오버로드 존재. 기존 1-인자는 시그니처·동작 무변경으로 위임만 한다.
- Datum 을 손에 쥔 호출부(티칭 Datum grab, 티칭 검사이미지 Grab, 생산 Datum grab)는 `datum.MirrorX/MirrorY` 를 **직접** 읽는다.
- Shot grab 호출부(생산 `EStep.Grab`, 티칭 Shot grab)는 `MeasurementBase.DatumRef` 로 소유 Datum 을 역추적하고, **해석 실패 시 무미러 + Error 로그**로 fail-safe 한다.
- 라이브 레시피의 SIDE Shot ↔ Datum 참조가 실측 확인됐다: `SHOT_4-1-1`(6건)·`SHOT_4-1-2`(3건) → `Side_Datum_4-1`. 미해석 `DatumRef` 0건 → 레시피 수정 불필요(SUMMARY 에 명시).
- 미러 플래그가 꺼진 Datum/Shot(현 레시피 전부 — `Mirror` 키 자체가 0건)은 변경 전과 **동일한 역할 식별자**로 grab 된다.
- Debug/x64 + Release/x64 빌드가 baseline 대로 통과하고, 그 과정에서 실행 중인 앱 프로세스를 죽이지 않았다.
- 운영 레시피 파일이 이번 작업에서 편집되지 않았다.
- 실기 확인이 필요한 항목이 Task 3 체크포인트로 분리돼 있고, 로컬 검증 가능 항목과 명확히 구분돼 있다.
</success_criteria>

<output>
완료 후 `.planning/quick/260813-jnh-mirrorx-y-mil-grab-side-datum-part-2-2/260813-jnh-SUMMARY.md` 를 작성한다.

SUMMARY 에 반드시 포함할 것:
1. **RESEARCH.md 의 레시피 관련 서술이 실제 파일과 불일치했다는 사실과 그 처리** — 원래 계획돼 있던 "stale DatumRef 교정" 태스크는 대상 결함이 라이브 레시피에 존재하지 않아(2026-08-13 전수 실측: `DatumRef=` 122건 전부 실재 Datum 6개와 일치) **plan 에서 제거**했고, 레시피 파일은 한 바이트도 편집하지 않았다
2. 라이브 레시피 실측 결과: Datum 6개(`Top_Datum`/`Side_Datum_3-1`/`Side_Datum_3-2`/`Side_Datum_4-1`/`Side_Datum_4-2`/`Bottom_Datum`), SIDE Shot 7개와 그 `DatumRef` 매핑
3. Part 1 의 "프로그램을 다시 시작해야 적용된다" 안내 문구가 실제보다 보수적이라는 점 — `MilCamera.cs:322-323` 이 매 grab 마다 방향을 재적용하므로 재시작 없이 반영될 것으로 예상. **무해한 기존 과잉보수 문구이며 이번 범위 밖(`DatumConfig.cs` 수정 금지)**
4. 회귀 0 의 구조적 근거: 현 레시피에 `Mirror` 키 0건(레시피가 Part 1 커밋보다 앞선 2026-07-29 저장분) → 전부 false 로드 → `BuildGrabRoleIdentifier` 가 base 이름 그대로 반환 → 기존과 동일 인자
5. Release/x64 baseline 실측값(수정 전 기록값 / 수정 후 값)과 Debug/x64 12 warning 유지 여부. 스크래치 OutDir 을 쓴 이유(실행 중 앱이 `bin/x64/Debug` 를 잠금, 프로세스 종료 금지 규칙)
6. Task 3 실기 UAT 결과 (또는 "실기 카메라 미보유로 defer")

커밋은 Task 별 원자 커밋 권장:
- Task 1: `feat(quick-260813-jnh): MIL 미러 역할 4종 등록 + grab 2-인자 오버로드`
- Task 2: `feat(quick-260813-jnh): Datum/Shot 미러 설정을 MIL grab 방향에 배선 (fail-safe 포함)`

⚠ 커밋 시 기존 미커밋 파일 `WPF_Example/Custom/EthernetVision/PickerCenterCalibrationService.cs` 는 **이번 작업과 무관하므로 절대 포함하지 마라.** 파일을 명시 지정해 커밋한다.
</output>
