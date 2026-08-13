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
  - D:/Data/Recipe/FAI_1/main.ini
autonomous: false
requirements: [QUICK-260813-JNH]

must_haves:
  truths:
    - "SIDE 카메라 grab 이 Datum 의 MirrorX/MirrorY 값에 따라 MIL 하드웨어 grab 방향(M_GRAB_DIRECTION_X/Y)이 반전된 이미지를 돌려준다"
    - "MirrorX/MirrorY 가 둘 다 꺼진 Datum·Shot 은 변경 전과 완전히 동일한 무미러 역할 식별자로 grab 된다 (회귀 0)"
    - "Shot 검사이미지 grab 이 그 Shot 의 DatumRef 를 통해 소유 Datum 의 미러 설정을 그대로 따라간다 (z=14 Shot 이 z=12/13 Datum 과 같은 방향으로 찍힘)"
    - "DatumRef 가 현재 레시피에서 해석되지 않으면 미러를 적용하지 않고(fail-safe) Error 로그에 Shot 이름과 미해석 DatumRef 값이 남는다"
    - "SIDE_SHOT_3_H5 의 stale DatumRef 가 현재 Datum 이름으로 교정되어 InspectionSequence.IsDatumRefUnresolvable 이 false 를 반환한다"
    - "HALCON 소프트웨어 미러(mirror_image / RotateImage) 호출이 diff 에 0건이다"
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
    - path: "D:/Data/Recipe/FAI_1/main.ini"
      provides: "SIDE_SHOT_3_H5 의 stale DatumRef 교정 (Side_Datum_3 → Side_Datum_4-2)"
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
      to: "MilCamera._roleInfoMap (4개 SIDE 역할)"
      via: "milCam.RegisterRoleInfo(mirrorInfo) — MilCamera.cs 무수정"
      pattern: "RegisterRoleInfo"
    - from: "MilCamera.ResolveRoleInfo(requestIdentifier)"
      to: "MIL.MdigControl(M_GRAB_DIRECTION_X/Y)"
      via: "GrabFromBuffer(roleInfo) — 기존 코드, 무수정"
      pattern: "M_GRAB_DIRECTION"
---

<objective>
Part 1(quick-260813-fdt, 커밋 b49d14f)에서 `DatumConfig` 에 추가만 해두고 아무도 읽지 않던 `MirrorX`/`MirrorY` 설정값을, 실제 MIL 카메라 grab 방향 반전으로 연결한다. 추가로 조사 과정에서 실 레시피에서 발견된 고아 `DatumRef` 데이터 결함 1건을 교정한다.

Purpose: SIDE 지그의 특정 물리 포즈(`Side_Datum_4-1`, z=12/13/14)는 카메라가 뒤집힌 방향으로 찍어야 한다. 소프트웨어 미러(HALCON `mirror_image`, ~27ms/장)는 택타임 비용 때문에 이미 기각됐고, MIL 하드웨어 grab 방향 반전(비용 0)만 사용한다.

Output:
- SIDE MIL 역할 4종(무미러 기준 + 미러 3조합)이 앱 시작 시 정적 등록됨
- `DeviceHandler.GrabHalconImage(ICameraParam, string)` 2-인자 오버로드 신설
- grab 호출부 5곳이 올바른 역할 식별자를 선택
- Shot→Datum 역추적 헬퍼 + 해석 실패 시 fail-safe(무미러) + 경고 로그
- 실 레시피의 stale `DatumRef` 1건 교정
</objective>

<execution_context>
@$HOME/.claude/get-shit-done/workflows/execute-plan.md
@$HOME/.claude/get-shit-done/templates/summary.md
</execution_context>

<context>
@.planning/quick/260813-jnh-mirrorx-y-mil-grab-side-datum-part-2-2/260813-jnh-RESEARCH.md
@CLAUDE.md

RESEARCH.md 를 먼저 읽어라. 이 plan 의 모든 file:line 은 거기서 실측 확인된 것이고, 아래 `<interfaces>` 는 plan 작성 시점에 재확인해 옮겨둔 것이다. **코드베이스 재탐색 없이 바로 구현 가능하다.**

## 이 작업의 배경 (한 문장)

SIDE 지그의 4-1 포즈는 물리적으로 뒤집혀 있어서, 그 포즈에서 찍는 사진 3장(Datum 용 z=12, z=13 + 측정 이미지용 z=14)을 카메라가 상하/좌우로 뒤집어서 찍어줘야 한다. Part 1 에서 "뒤집을까요?" 라는 설정 스위치만 만들어 뒀고, 이번 작업이 그 스위치를 실제 카메라에 연결한다.

## 설계 요약 (RESEARCH "설계 A" — 확정, 재검토 불필요)

미러 조합은 (MirrorX, MirrorY) 불리언 2개 = **최대 4가지**뿐이다. 그래서 레시피를 스캔할 필요 없이 **앱 시작 시 4가지 역할을 전부 정적 등록**해두고, grab 할 때 식별자 문자열만 골라 넘긴다.

이 설계를 택한 이유(대안 기각 근거는 RESEARCH §3/§4):
- 레시피는 카메라 초기화보다 **한참 뒤에** 로드된다 → "레시피 보고 등록" 은 구조적으로 불가능
- `MilCamera._roleInfoMap` 에는 `Remove`/`Clear` 가 없다 → 레시피 교체형 등록은 유령 역할이 남는다
- `Devices` 딕셔너리에 가짜 카메라 키를 넣는 방식은 INI 오염 + UI 드롭다운 노출 + SIMUL 전면 실패를 유발한다

## 절대 건드리지 말 것

| 대상 | 이유 |
|---|---|
| `WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs` | Part 1 완료분(b49d14f). `MirrorX`/`MirrorY`/`_suppressMirrorWarning` 재수정 금지 |
| `WPF_Example/Device/Camera/Mil/MilCamera.cs` | `RegisterRoleInfo`/`ResolveRoleInfo`/`GrabFromBuffer` 가 이미 필요한 걸 전부 제공한다. **신규 코드 0줄** |
| TCP 프로토콜 코드 | 범위 밖 |
| TOP/BOTTOM 카메라 설정값(`REVERSE_X_TOP` 등 상수) | 범위 밖. 상수는 한 글자도 바꾸지 않는다 |
| 기존 Datum 판정/측정 로직 | 범위 밖. 이번 변경은 "어떤 방향으로 찍을지" 선택뿐이다 |
| `D:\Data\Recipe\FAI_1\main.ini.bak_gapuat` 등 백업 파일 | 라이브 레시피만 수정한다 |
| HALCON `mirror_image` / `HImage.RotateImage` | 소프트웨어 미러 전면 금지 (택타임 비용) |

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
</interfaces>

## 실 레시피 실측 결과 (plan 작성 시 직접 확인 — Part B 근거)

`D:\Data\Recipe\FAI_1\main.ini` (2026-08-13 13:52, 214,395 bytes)

| 섹션 | ShotName | ZIndex | DatumRef | 판정 |
|---|---|---|---|---|
| `[SHOT_3_FAI_0_MEAS_0]` (L5033) | SIDE_SHOT_3_H5 (L4925) | 9 | `Side_Datum_3` | ⚠ **stale** — 이 이름의 Datum 은 현재 레시피에 없음 |
| `[SHOT_5_FAI_0_MEAS_0/1/2]` (L5326/5365/5404) | **SIDE_SHOT_4-1_F9** (L5218) | **14** | `Side_Datum_4-1` | ✅ **정상** |
| `[SHOT_4_FAI_0_MEAS_0/1]` (L5160/5199) | SIDE_SHOT_1_D1 | 2 | `Side_Datum_3-1` | ✅ 정상 |
| `[SHOT_6_FAI_0_MEAS_0/1]` (L5531/5570) | SIDE_SHOT_2_1_D1 | 5 | `Side_Datum_3-2` | ✅ 정상 |

현재 존재하는 Datum 이름 6개: `Top_Datum`(L13), `Side_Datum_3-1`(L189), `Side_Datum_3-2`(L369), `Side_Datum_4-2`(L549), `Side_Datum_4-1`(L729), `Bottom_Datum`(L913).

**핵심 결론 (Part A 차단 아님):** 이번 작업의 실제 대상인 **`SIDE_SHOT_4-1_F9`(z=14) 의 DatumRef 3개는 이미 전부 `Side_Datum_4-1` 로 정확하다.** 즉 설계 A 의 Shot 측 역추적은 이 포즈에서 정상 동작한다. `SIDE_SHOT_3_H5` 의 stale 참조는 **조사 중 곁가지로 발견된 별개 데이터 결함**이며 Part A 정확성을 막지 않는다 — 그래도 고칠 가치가 있어 Task 1 로 분리했다.

미러 키 현황(전부 False, L219/220, 399/400, 579/580, 759/760) — 즉 **현재 레시피 기준 실행 경로는 100% 무미러**다. 이 사실이 회귀 0 을 구조적으로 보장한다(무미러 = 기존 식별자 그대로).

## Part 1 경고 문구에 대한 메모 (코드 변경 아님)

`DatumConfig.cs:238,252` 의 사용자 안내 문구는 "프로그램을 다시 시작해야 적용된다" 라고 말한다. 그런데 `MilCamera.cs:322-323` 은 **매 grab 직전마다** `MdigControl(M_GRAB_DIRECTION_X/Y)` 를 재적용한다(quick-260805-jtj 이후 확립된 기존 동작). 설계 A 는 레시피 로드에 의존하지 않으므로 실제로는 **재시작 없이도 즉시 반영된다.**

→ 이건 기능상 **무해한 과잉보수 문구**다. `DatumConfig.cs` 는 수정 금지 대상이므로 **이번 작업에서 고치지 않는다.** SUMMARY 에 "알려진 기존 문구 부정확 (무해, 이번 범위 밖)" 으로만 기록하고, Task 4 실기 검증에서 재시작 없이도 되는지 관찰만 남긴다.
</context>

<tasks>

<task type="auto">
  <name>Task 1: 실 레시피의 stale DatumRef 교정 (Part B — 데이터 수정, 백업 먼저)</name>
  <files>D:/Data/Recipe/FAI_1/main.ini</files>
  <action>
`SIDE_SHOT_3_H5`(`[SHOT_3]`, ZIndex=9) 의 측정이 참조하는 `DatumRef=Side_Datum_3` 는 개명 잔재다. 현재 이 이름의 Datum 은 레시피에 존재하지 않으며(개명 후 이름은 `Side_Datum_4-2`), `InspectionSequence.IsDatumRefUnresolvable` 이 true 를 돌려주는 상태다.

**이건 코드가 아니라 운영 레시피 데이터 수정이다. 아래 순서를 반드시 지켜라.**

1. **백업 먼저.** 편집 전에 스크래치로 원본을 복사한다:
   `cp "D:/Data/Recipe/FAI_1/main.ini" "$SCRATCH/main.ini.pre-jnh"`
   (`$SCRATCH` = `C:/Users/tech/AppData/Local/Temp/claude/C--Info-Project-DataMeasurement/9d3a7b4d-2314-4b14-8686-52fd6346a1f9/scratchpad`)
   백업이 실제로 생성됐는지 `ls -la` 로 확인한 뒤에만 편집으로 넘어간다.

2. **정확히 1줄만 바꾼다.** 파일 **5033번째 줄**의
   `DatumRef=Side_Datum_3`  →  `DatumRef=Side_Datum_4-2`

   ⚠ **치명적 주의 — 단순 문자열 치환(sed s/Side_Datum_3/.../g) 절대 금지.** 같은 파일에 `Side_Datum_3-1`(2곳), `Side_Datum_3-2`(2곳) 가 있어서 부분일치로 함께 망가진다. **행 전체가 정확히 `DatumRef=Side_Datum_3` 인 줄 하나**만 교체하라(이 값을 가진 줄은 파일 전체에서 L5033 단 하나뿐임을 이미 확인함). 파일 쓰기 도구를 쓰되, 앵커 문자열은 반드시 줄 시작(`^`)과 줄 끝(`$`)이 고정된 완전 일치로 잡아라.

3. **다른 레시피 사본은 손대지 않는다.** `main.ini.bak_gapuat` 등 백업 파일, `D:\디팜스자료\...` 아래 사본은 **읽기만 하고 수정 금지**. 다른 위치에 같은 stale 값을 가진 라이브 레시피가 또 있는지 조사해서 **발견하면 SUMMARY 에 보고만** 하고 편집하지 마라(사용자 판단 사항).

4. Part A 대상인 `SIDE_SHOT_4-1_F9` 의 DatumRef 3개(L5326/5365/5404 = `Side_Datum_4-1`)는 **이미 정확하므로 절대 건드리지 마라.**

INI 파싱 정합성은 GUI 없이 검증한다: 백업 대비 diff 가 정확히 1줄이고, 줄 수가 동일하고, 다른 DatumRef 값들의 등장 횟수가 그대로면 파서 관점에서 안전하다(`ParamBase.Load` 는 `키=값` 라인 단위 리플렉션이므로 값 문자열 교체만으로 구조가 깨지지 않는다).
  </action>
  <verify>
    <automated>
SCRATCH="C:/Users/tech/AppData/Local/Temp/claude/C--Info-Project-DataMeasurement/9d3a7b4d-2314-4b14-8686-52fd6346a1f9/scratchpad"
INI="D:/Data/Recipe/FAI_1/main.ini"
echo "--- 백업 존재 ---"; ls -la "$SCRATCH/main.ini.pre-jnh"
echo "--- stale 값 0건이어야 함 ---"; grep -c "^DatumRef=Side_Datum_3$" "$INI" || echo 0
echo "--- 교정 값 1건이어야 함 ---"; grep -c "^DatumRef=Side_Datum_4-2$" "$INI" || echo 0
echo "--- 무변경 대조: 3-1=2, 3-2=2, 4-1=3 ---"
grep -c "^DatumRef=Side_Datum_3-1$" "$INI" || echo 0
grep -c "^DatumRef=Side_Datum_3-2$" "$INI" || echo 0
grep -c "^DatumRef=Side_Datum_4-1$" "$INI" || echo 0
echo "--- diff 는 정확히 1줄 변경 => '<' 1 + '>' 1 = 2 ---"; diff "$SCRATCH/main.ini.pre-jnh" "$INI" | grep -c "^[<>]" || echo 0
echo "--- 줄 수 동일해야 함 ---"; wc -l < "$SCRATCH/main.ini.pre-jnh"; wc -l < "$INI"
    </automated>
  </verify>
  <done>
백업 `main.ini.pre-jnh` 존재. `^DatumRef=Side_Datum_3$` = 0건, `^DatumRef=Side_Datum_4-2$` = 1건. `3-1`=2, `3-2`=2, `4-1`=3 (전부 무변경). diff 변경 라인 = 정확히 2(`<` 1줄 + `>` 1줄). 두 파일 줄 수 동일. 다른 위치의 라이브 레시피 사본 조사 결과가 SUMMARY 에 기록됨(수정하지 않음).
  </done>
</task>

<task type="auto">
  <name>Task 2: MIL 미러 역할 4종 정적 등록 + 2-인자 grab 오버로드 (Part A 인프라)</name>
  <files>WPF_Example/Custom/Device/DeviceHandler.cs, WPF_Example/Device/DeviceHandler.cs</files>
  <action>
**먼저 Release/x64 빌드 baseline 을 잡아라.** 이번 신규 코드의 절반(MIL 등록 분기)은 `#else`(비-SIMUL) 안에 들어가는데, Debug/x64 는 `SIMUL_MODE` 를 정의하므로 **Debug 빌드로는 그 코드가 아예 컴파일되지 않는다.** Release/x64(`DefineConstants=TRACE`, csproj:72-74)만이 유일한 컴파일 검증 수단이다. Matrox MIL .NET 참조(`C:\Program Files\Matrox Imaging\MIL\MIL.NET\Matrox.MatroxImagingLibrary.dll`)가 이 PC 에 실재함은 확인했다. **수정 전에** Release/x64 를 1회 빌드해 error/warning 개수를 기록해 둬라(Release baseline 은 Debug 의 12 와 다를 수 있고, 사전에 알려진 값이 없다).

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

**등록 범위 판단(명시적 결정):** `BuildMirrorRoleInfos` 는 등록되는 MIL 역할 **모두**에 대해 호출된다. `CameraRole.Side`(PC2)에서는 `CAM_SIDE` 하나만 등록되므로 **정확히 SIDE 역할 4종**(`CAM_SIDE`, `CAM_SIDE#MX`, `CAM_SIDE#MY`, `CAM_SIDE#MXY`)이 만들어진다 — 요구사항 충족. PC1(TopBottom)에서도 같은 코드가 TOP/BOTTOM 변형을 만들지만, **기본 역할의 값은 한 비트도 바뀌지 않고**(상수 무수정) 변형 역할은 어떤 Datum 도 MirrorX/Y 를 켜지 않는 한 조회되지 않는다 → TOP/BOTTOM 동작 무변경. 특수 분기를 넣지 않는 편이 오히려 안전한 이유: 미등록 식별자는 `ResolveRoleInfo` 가 **기본 `Info` 로 조용히 폴백**하는데, 공유 인스턴스에서 `Info` 는 첫 등록 역할(PC1 이면 TOP)이라 BOTTOM 이 TOP 의 방향을 쓰게 되는 함정이 생긴다. 전 역할 등록이 그 함정을 원천 제거한다.

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
SCRATCH="C:/Users/tech/AppData/Local/Temp/claude/C--Info-Project-DataMeasurement/9d3a7b4d-2314-4b14-8686-52fd6346a1f9/scratchpad"
PROJ="C:/Info/Project/DataMeasurement/WPF_Example/DatumMeasurement.csproj"

echo "===== Debug/x64 (SIMUL_MODE) ====="
"$MSB" "$PROJ" -t:Build -p:Configuration=Debug -p:Platform=x64 -v:minimal -nologo > "$SCRATCH/jnh-t2-debug.log" 2>&1
echo "BUILD_RC=$?"
echo -n "errors="; grep -c ": error" "$SCRATCH/jnh-t2-debug.log" || echo 0
echo -n "warnCS="; grep -c "warning CS" "$SCRATCH/jnh-t2-debug.log" || echo 0

echo "===== Release/x64 (비-SIMUL, MIL 분기 실제 컴파일) ====="
"$MSB" "$PROJ" -t:Build -p:Configuration=Release -p:Platform=x64 -v:minimal -nologo > "$SCRATCH/jnh-t2-release.log" 2>&1
echo "BUILD_RC=$?"
echo -n "errors="; grep -c ": error" "$SCRATCH/jnh-t2-release.log" || echo 0
echo -n "warnCS="; grep -c "warning CS" "$SCRATCH/jnh-t2-release.log" || echo 0

cd "C:/Info/Project/DataMeasurement"
echo "===== 신규 심볼 존재 ====="
grep -n "BuildGrabRoleIdentifier\|BuildMirrorRoleInfos\|CloneRoleInfo\|MIRROR_ROLE_SUFFIX" WPF_Example/Custom/Device/DeviceHandler.cs
grep -n "GrabHalconImage(ICameraParam param, string requestIdentifier)\|registeredMil" WPF_Example/Device/DeviceHandler.cs
echo "===== 금지 패턴 (전부 0건이어야 함) ====="
echo -n "Devices.Add 미러키="; git diff -- WPF_Example/Device/DeviceHandler.cs | grep -c "^+.*Devices.Add.*#M" || echo 0
echo -n "SetRequiredDevice 신규="; git diff -- WPF_Example/Custom/Device/DeviceHandler.cs WPF_Example/Device/DeviceHandler.cs | grep -c "^+.*SetRequiredDevice" || echo 0
echo -n "삼항="; git diff -- WPF_Example/Custom/Device/DeviceHandler.cs WPF_Example/Device/DeviceHandler.cs | grep "^+" | grep -c "[^?]? [^ ]* : " || echo 0
echo -n "REVERSE/ROTATE 상수변경="; git diff -- WPF_Example/Custom/Device/DeviceHandler.cs | grep -c "^-.*REVERSE_\|^-.*ROTATE_" || echo 0
echo -n "MilCamera.cs 수정="; git diff --name-only | grep -c "MilCamera.cs" || echo 0
echo -n "DatumConfig.cs 수정="; git diff --name-only | grep -c "DatumConfig.cs" || echo 0
    </automated>
  </verify>
  <done>
Debug/x64: `BUILD_RC=0`, errors=0, warnCS=12 (기존 baseline 유지).
Release/x64: `BUILD_RC=0`, errors=0, warnCS = 이 태스크 시작 시 기록한 수정 전 Release baseline 과 동일 (증가 0). — Release baseline 이 사전 미지값이므로 반드시 수정 전 측정치와 대조할 것.
`BuildGrabRoleIdentifier`/`BuildMirrorRoleInfos`/`CloneRoleInfo`/`MIRROR_ROLE_SUFFIX_*` 가 Custom/Device/DeviceHandler.cs 에 존재. 2-인자 `GrabHalconImage` 오버로드 + `registeredMil` 루프가 Device/DeviceHandler.cs 에 존재.
금지 패턴 전부 0건: 미러 키의 `Devices.Add` 없음, 신규 `SetRequiredDevice` 없음, 삼항 없음, `REVERSE_*`/`ROTATE_*` 상수 변경 없음, `MilCamera.cs`/`DatumConfig.cs` 무수정.
  </done>
</task>

<task type="auto">
  <name>Task 3: Shot→Datum 미러 역추적(fail-safe) + grab 호출부 5곳 배선</name>
  <files>WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs, WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs, WPF_Example/UI/ContentItem/MainView.xaml.cs</files>
  <action>
### (1) `InspectionSequence.cs` — `ResolveShotGrabMirror` 신설

`IsDatumRefUnresolvable`(:2034-2043) **바로 아래**에 붙이고 **Allman brace 스타일**을 따른다.

```csharp
// quick-260813-jnh: Shot 검사이미지 grab 의 미러 방향 해석. 미러 플래그는 DatumConfig 에만 있고 ShotConfig 에는
//  없으므로, 이 Shot 의 측정들이 참조하는 DatumRef 로 소유 Datum 을 역추적한다(레시피에 실재하는 유일한 연결고리 —
//  '+1 규칙' 같은 새 규약을 발명하지 않는다). 한 물리 포즈가 여러 z 에 걸쳐 있어서(4-1 = Datum z12/13 + Shot z14),
//  Datum 만 미러하고 Shot 을 안 하면 미러된 좌표계로 안 뒤집힌 이미지를 측정하게 되어 전 항목이 어긋난다.
// ※ 해석 실패는 전부 fail-safe(무미러) + Error 로그. 실제로 이 결함 사례(SIDE_SHOT_3_H5)가 라이브 레시피에서
//    발견됐기 때문에, 데이터 1건을 고쳤다고 재발이 없다고 믿지 않는다.
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
현 레시피의 미러 플래그는 8개 전부 `False`(main.ini L219/220, 399/400, 579/580, 759/760)다. `BuildGrabRoleIdentifier` 는 둘 다 false 면 **base 이름을 그대로** 돌려주고, 그러면 `cam.GrabHalconImage(param.DeviceName)` 와 완전히 동일한 인자가 된다 → 모든 기존 Datum/Shot 은 변경 전과 **바이트 단위로 같은 역할**로 grab 된다.
  </action>
  <verify>
    <automated>
MSB="C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/MSBuild.exe"
SCRATCH="C:/Users/tech/AppData/Local/Temp/claude/C--Info-Project-DataMeasurement/9d3a7b4d-2314-4b14-8686-52fd6346a1f9/scratchpad"
PROJ="C:/Info/Project/DataMeasurement/WPF_Example/DatumMeasurement.csproj"

echo "===== Debug/x64 ====="
"$MSB" "$PROJ" -t:Build -p:Configuration=Debug -p:Platform=x64 -v:minimal -nologo > "$SCRATCH/jnh-t3-debug.log" 2>&1
echo "BUILD_RC=$?"
echo -n "errors="; grep -c ": error" "$SCRATCH/jnh-t3-debug.log" || echo 0
echo -n "warnCS="; grep -c "warning CS" "$SCRATCH/jnh-t3-debug.log" || echo 0

echo "===== Release/x64 (비-SIMUL 분기 컴파일) ====="
"$MSB" "$PROJ" -t:Build -p:Configuration=Release -p:Platform=x64 -v:minimal -nologo > "$SCRATCH/jnh-t3-release.log" 2>&1
echo "BUILD_RC=$?"
echo -n "errors="; grep -c ": error" "$SCRATCH/jnh-t3-release.log" || echo 0
echo -n "warnCS="; grep -c "warning CS" "$SCRATCH/jnh-t3-release.log" || echo 0

cd "C:/Info/Project/DataMeasurement"
echo "===== 배선 확인 ====="
echo -n "ResolveShotGrabMirror 정의="; grep -c "public void ResolveShotGrabMirror" WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs || echo 0
echo -n "ShotMirror 경고로그="; grep -c "\[ShotMirror\]" WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs || echo 0
echo -n "FAIMeasurement 2-인자 grab="; grep -c "GrabHalconImage(ShotParam, sz" WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs || echo 0
echo -n "MainView 2-인자 grab="; grep -c "GrabHalconImage(param, ResolveGrabRoleIdentifier" WPF_Example/UI/ContentItem/MainView.xaml.cs || echo 0
echo "--- 남아있는 1-인자 grab (MainView 3곳은 0이어야 함) ---"
grep -n "pDev.GrabHalconImage(param)" WPF_Example/UI/ContentItem/MainView.xaml.cs || echo "none"

echo "===== 금지 패턴 (전부 0건) ====="
echo -n "HALCON 소프트미러="; git diff | grep "^+" | grep -ci "mirror_image\|MirrorImage\|RotateImage" || echo 0
echo -n "삼항="; git diff | grep "^+" | grep -c "[^?]? [^ ]* : " || echo 0
echo -n "금지파일 수정="; git diff --name-only | grep -c "MilCamera.cs\|DatumConfig.cs" || echo 0
echo -n "IsDatumRefUnresolvable 계약변경="; git diff -- WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs | grep -c "^-.*IsDatumRefUnresolvable" || echo 0
echo "--- 변경 파일 목록 (예상 5개 + 무관 PickerCenterCalibrationService.cs) ---"
git diff --name-only
    </automated>
  </verify>
  <done>
Debug/x64: `BUILD_RC=0`, errors=0, warnCS=12.
Release/x64: `BUILD_RC=0`, errors=0, warnCS = Task 2 에서 기록한 수정 전 Release baseline 과 동일.
`ResolveShotGrabMirror` 정의 1건 + `[ShotMirror]` 로그 2건 존재. `Action_FAIMeasurement.cs` 2-인자 grab 2건, `MainView.xaml.cs` 2-인자 grab 3건. `MainView.xaml.cs` 에 남은 1-인자 `pDev.GrabHalconImage(param)` 0건.
금지 패턴 전부 0건: HALCON 소프트웨어 미러 0, 삼항 0, `MilCamera.cs`/`DatumConfig.cs` 무수정, `IsDatumRefUnresolvable` 반환 계약 무변경.
변경 파일이 이 plan 의 5개 소스 파일로 한정됨(기존 미커밋 `PickerCenterCalibrationService.cs` 는 이번 작업과 무관 — 커밋에 포함하지 말 것).
  </done>
</task>

<task type="checkpoint:human-verify" gate="blocking">
  <name>Task 4: 실기 MIL 하드웨어 미러 육안 확인 (SIMUL 로 대체 불가)</name>
  <files>(코드 변경 없음 — 실기 육안 검증 전용 체크포인트)</files>
  <action>실행을 멈추고 사용자에게 아래 what-built / how-to-verify 내용을 그대로 제시한다. 이 태스크에서는 코드나 레시피를 추가로 수정하지 마라. 사용자의 resume-signal 응답을 받은 뒤 그 결과(PASS / 실패한 Test 번호 / defer)를 SUMMARY 에 기록한다.</action>
  <what-built>
**로컬에서 이미 검증된 것(재확인 불필요):**
- Debug/x64 + Release/x64 컴파일 통과 (Release 는 SIMUL_MODE 가 없어 MIL 등록 분기가 실제로 컴파일됨)
- SIDE MIL 역할 4종(`CAM_SIDE`, `CAM_SIDE#MX`, `CAM_SIDE#MY`, `CAM_SIDE#MXY`) 등록 코드
- Shot→DatumRef→Datum 미러 역추적 + 해석 실패 시 무미러 fail-safe + Error 로그
- 실 레시피 stale `DatumRef` 교정 (`SIDE_SHOT_3_H5`: `Side_Datum_3` → `Side_Datum_4-2`), 백업 보관됨
- HALCON 소프트웨어 미러 0건, 삼항 0건

**로컬에서 원리적으로 검증 불가능한 것 = 아래 확인 대상:**
이 개발 PC 의 Debug 빌드는 `SIMUL_MODE` 라서 `MilCamera` 객체 자체가 생성되지 않고 `VirtualCamera` 로 대체된다. `VirtualCamera.GrabHalconImage(string)` 은 **식별자를 통째로 무시**한다(VirtualCamera.cs:460-462). 즉 시뮬에서는 어떤 식별자를 넘겨도 조용히 무시되므로 회귀 위험은 0 이지만, **실제 반전 동작은 절대 재현되지 않는다.** 물리 CXP 카메라가 붙은 SIDE PC 에서만 확인 가능하다.
  </what-built>
  <how-to-verify>
**사전 준비 (중요):**
- 실기 SIDE PC(`CameraRole = Side`)에 **Release/x64 빌드**를 배포한다. Debug 빌드는 SIMUL_MODE 라 MIL grab 자체를 안 한다.
- **DeviceSelector 라이브뷰 창을 반드시 닫아라.** 스트리밍 중이면 검사 grab 이 아예 `null` 을 반환한다(`MilCamera.cs:267-270`). 창을 열어둔 채 "미러가 안 먹는다" 로 오진하기 가장 쉬운 함정이다.
- 참고: 라이브 미리보기 화면은 원래 항상 기본 역할을 쓰므로(`MilCamera.cs:500 LiveLoop`) 미러가 적용되지 않는 게 정상이다. 판단 근거로 쓰지 마라.

**Test 1 — Datum 이미지 반전 (핵심)**
1. 트리에서 `Side_Datum_4-1` 선택 → PropertyGrid `Datum|Mirror` 에서 `MirrorY = True` 로 변경 (경고창 뜨면 읽고 닫기) → 레시피 저장
2. 같은 Datum 노드에서 `검사이미지 Grab` 실행
3. 저장된 bmp 를 열어 **상하가 뒤집혀 있는지** 육안 확인
4. 기대: 뒤집힘. → PASS

**Test 2 — Shot 이미지가 같은 방향으로 따라오는지 (RESEARCH 가정 A1 검증, 이 작업의 존재 이유)**
1. Test 1 과 같은 상태에서 `SIDE_SHOT_4-1_F9`(z=14) 노드 선택 → `검사이미지 Grab`
2. 기대: Datum 이미지와 **동일한 방향으로** 반전됨 (이게 안 되면 미러된 좌표계로 안 뒤집힌 이미지를 측정하게 되어 전 FAI 가 어긋난다)

**Test 3 — 회귀 0 확인 (가장 중요)**
1. `MirrorX/MirrorY` 가 둘 다 `False` 인 나머지 SIDE Datum 3개(`Side_Datum_3-1`, `Side_Datum_3-2`, `Side_Datum_4-2`)와 그에 대응하는 Shot 들을 각각 Grab
2. 기대: 이번 변경 **이전과 완전히 동일한 방향**(=아무 변화 없음)

**Test 4 — 전체 사이클**
1. `MirrorY=True` 유지한 채 TCP `$PREP`/`$TEST` 또는 화면의 수동 z 트리거로 z=12 → 13 → 14 전체 사이클 실행
2. 기대: Datum 검출 성공 + FAI 측정값이 미러 전과 정합적(부호 뒤집힘 없이 같은 값 계열)

**Test 5 — 로그 확인 (Part B fail-safe 검증)**
1. 사이클 로그에서 `[ShotMirror]` Error 가 **0건**인지 확인 (stale DatumRef 를 고쳤으므로 0 이어야 정상)
2. (선택) 일부러 어떤 Shot 의 `DatumRef` 를 없는 이름으로 바꿔 1회 grab → `[ShotMirror] ... 레시피에 없음 — 미러 미적용(무미러)으로 grab` 로그가 뜨고 이미지는 안 뒤집히는지 확인 → 즉시 원복

**Test 6 — 재시작 필요 여부 관찰 (기록용, 판정 아님)**
1. `MirrorY` 를 켜고 **앱을 재시작하지 않은 채로** Grab 해본다
2. 관찰 결과를 기록만 한다. `MilCamera.cs:322-323` 은 매 grab 마다 방향을 재적용하므로 **재시작 없이도 반영될 것으로 예상**된다. Part 1 의 "프로그램을 다시 시작해야 적용된다" 안내 문구는 실제보다 보수적인 것으로 보이나, `DatumConfig.cs` 는 이번 작업의 수정 금지 대상이므로 **문구는 고치지 않는다.** 관찰 결과만 SUMMARY 에 남긴다.
  </how-to-verify>
  <resume-signal>"approved" 입력, 또는 실패한 Test 번호와 관찰 내용을 알려주세요. 실기 카메라가 없어 지금 확인이 불가능하면 "defer" 라고 알려주시면 실기 UAT 미수행 상태로 기록하고 마무리합니다.</resume-signal>
</task>

</tasks>

<threat_model>
## Trust Boundaries

| Boundary | Description |
|----------|-------------|
| 운영 레시피 파일 → 앱 | `D:\Data\Recipe\FAI_1\main.ini` 는 사용자가 편집하는 신뢰 데이터지만, 개명/삭제로 내부 참조가 깨질 수 있다(이번에 실제 1건 발견) |
| Datum 설정 → 카메라 하드웨어 | `MirrorX/MirrorY` 가 물리 grab 방향을 바꾼다 — 같은 카메라를 쓰는 다른 측정까지 영향 |

## STRIDE Threat Register

| Threat ID | Category | Component | Disposition | Mitigation Plan |
|-----------|----------|-----------|-------------|-----------------|
| T-JNH-01 | Tampering (데이터 무결성) | `main.ini` DatumRef 교정 | mitigate | 편집 전 스크래치 백업 필수 + 정확 라인 1건만 교체(부분일치 치환 금지) + diff/줄수/등장횟수 대조 검증 (Task 1) |
| T-JNH-02 | Information Disclosure (조용한 오검) | `ResolveShotGrabMirror` 참조 해석 실패 | mitigate | fail-safe 무미러 + `ELogType.Error` 경고 로그에 Shot 이름·미해석 DatumRef 명시 (Task 3) |
| T-JNH-03 | Denial of Service (UI 오염) | `Devices` 딕셔너리에 미러 키 추가 시 UI 드롭다운/INI 오염 | mitigate | 미러 역할은 `_roleInfoMap` 에만 등록, `Devices.Add`/`SetRequiredDevice` 미사용 — verify 에서 grep 로 0건 강제 (Task 2) |
| T-JNH-04 | Elevation of Privilege | 해당 없음 (로컬 데스크톱 앱, 인증 경계 없음) | accept | 이번 변경은 네트워크/인증 경계를 넘지 않는다 |
| T-JNH-05 | Repudiation | 미러 적용 여부가 로그에 안 남음 | accept | 실패 경로만 로깅. 정상 경로 로깅은 매 grab 노이즈 대비 이득이 없음(육안 확인이 1차 수단) |
</threat_model>

<verification>
1. **빌드 (필수 2종)**
   - Debug/x64: `BUILD_RC=0`, `: error` 0건, `warning CS` **12건**(이 저장소 기존 baseline — 0 아님)
   - Release/x64: `BUILD_RC=0`, `: error` 0건, `warning CS` 는 Task 2 시작 시 기록한 **수정 전 Release baseline 과 동일**
   - Release 를 반드시 도는 이유: Debug 는 `SIMUL_MODE` 를 정의해서 MIL 등록 분기(`#else`)를 **컴파일조차 하지 않는다**. Release/x64 만이 신규 MIL 코드의 유일한 컴파일 검증 수단이다.
   - MSBuild: `C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe` (vswhere 로 확인 완료). 성공 판정은 **프로세스 종료 코드**로 한다 — `-v:minimal -nologo` 는 "Build succeeded." 문자열을 아예 출력하지 않는다.
   - 출력 폴더가 실행 중인 앱에 잠겨 있으면 **프로세스를 죽이지 마라.** 스크래치 OutDir 로 컴파일만 검증하거나 잠김 사실을 보고한다. OutDir 처럼 **값에 슬래시가 들어가는 스위치는 반드시 단일 대시 `-p:Name=값`** 을 쓴다(`//p:` 는 UNC 경로로 뭉개져 MSB1001 발생).

2. **금지 패턴 (전부 0건, grep 강제)**
   - HALCON 소프트웨어 미러: `mirror_image` / `MirrorImage` / `RotateImage` 추가 0건
   - 삼항 연산자 `?:` 추가 0건
   - `MilCamera.cs` 수정 0건, `DatumConfig.cs` 수정 0건
   - 미러 식별자에 대한 `Devices.Add` / `SetRequiredDevice` 0건
   - `REVERSE_X_*` / `REVERSE_Y_*` / `ROTATE_*` 상수 변경 0건

3. **레시피 데이터**
   - `^DatumRef=Side_Datum_3$` 0건, `^DatumRef=Side_Datum_4-2$` 1건
   - `Side_Datum_3-1`=2, `Side_Datum_3-2`=2, `Side_Datum_4-1`=3 (전부 무변경)
   - 백업 대비 diff 가 정확히 1줄 변경, 줄 수 동일

4. **실기 (Task 4 체크포인트)** — SIMUL 로 대체 불가. 물리 CXP 카메라가 붙은 SIDE PC 필요.
</verification>

<success_criteria>
- SIDE MIL 역할 4종(무미러 기준 + `#MX`/`#MY`/`#MXY`)이 `MilCamera._roleInfoMap` 에 앱 시작 시 등록된다. **`MilCamera.cs` 변경 0줄.**
- `DeviceHandler.GrabHalconImage(ICameraParam, string)` 오버로드 존재. 기존 1-인자는 시그니처·동작 무변경으로 위임만 한다.
- Datum 을 손에 쥔 호출부(티칭 Datum grab, 티칭 검사이미지 Grab, 생산 Datum grab)는 `datum.MirrorX/MirrorY` 를 **직접** 읽는다.
- Shot grab 호출부(생산 `EStep.Grab`, 티칭 Shot grab)는 `MeasurementBase.DatumRef` 로 소유 Datum 을 역추적하고, **해석 실패 시 무미러 + Error 로그**로 fail-safe 한다.
- `SIDE_SHOT_4-1_F9` 의 `DatumRef` 가 `Side_Datum_4-1` 로 정확함이 실측 확인됐다(이미 정상 — 수정 불필요, 이 사실을 SUMMARY 에 명시).
- `SIDE_SHOT_3_H5` 의 stale `DatumRef` 가 `Side_Datum_4-2` 로 교정되고 백업이 보관됐다.
- 미러 플래그가 둘 다 꺼진 Datum/Shot(현 레시피 8/8 전부)은 변경 전과 **동일한 역할 식별자**로 grab 된다.
- Debug/x64 + Release/x64 빌드가 baseline 대로 통과한다.
- 실기 확인이 필요한 항목이 Task 4 체크포인트로 분리돼 있고, 로컬 검증 가능 항목과 명확히 구분돼 있다.
</success_criteria>

<output>
완료 후 `.planning/quick/260813-jnh-mirrorx-y-mil-grab-side-datum-part-2-2/260813-jnh-SUMMARY.md` 를 작성한다.

SUMMARY 에 반드시 포함할 것:
1. `SIDE_SHOT_4-1_F9` 의 `DatumRef` 는 **이미 정상**이었고 Part A 를 차단하지 않았다는 실측 결과
2. `SIDE_SHOT_3_H5` stale `DatumRef` 교정 내역 + 백업 경로 + 다른 위치 레시피 사본 조사 결과(수정하지 않음)
3. Part 1 의 "프로그램을 다시 시작해야 적용된다" 안내 문구가 실제보다 보수적이라는 점 — `MilCamera.cs:322-323` 이 매 grab 마다 방향을 재적용하므로 재시작 없이 반영될 것으로 예상. **무해한 기존 과잉보수 문구이며 이번 범위 밖(`DatumConfig.cs` 수정 금지)**
4. 회귀 0 의 구조적 근거: 현 레시피 미러 플래그 8/8 전부 False → `BuildGrabRoleIdentifier` 가 base 이름 그대로 반환 → 기존과 동일 인자
5. Release/x64 baseline 실측값(수정 전/후)
6. Task 4 실기 UAT 결과 (또는 "실기 카메라 미보유로 defer")

커밋은 Task 별 원자 커밋 권장:
- Task 1: 레시피는 저장소 밖 파일이므로 코드 커밋 없음 — SUMMARY 기록으로 갈음
- Task 2: `feat(quick-260813-jnh): SIDE MIL 미러 역할 4종 등록 + grab 2-인자 오버로드`
- Task 3: `feat(quick-260813-jnh): Datum/Shot 미러 설정을 MIL grab 방향에 배선 (fail-safe 포함)`

⚠ 커밋 시 기존 미커밋 파일 `WPF_Example/Custom/EthernetVision/PickerCenterCalibrationService.cs` 는 **이번 작업과 무관하므로 절대 포함하지 마라.** 파일을 명시 지정해 커밋한다.
</output>
</content>
