---
phase: quick-260805-jtj
plan: 01
type: execute
wave: 1
depends_on: []
files_modified:
  - WPF_Example/Device/Camera/VirtualCamera.cs
  - WPF_Example/Device/Camera/Mil/MilCamera.cs
  - WPF_Example/Device/DeviceHandler.cs
  - WPF_Example/Custom/Sequence/Bottom/Action_BottomInspection.cs
autonomous: true
requirements: [FIX-JTJ-01]

must_haves:
  truths:
    - "실HW(非SIMUL_MODE) 빌드에서 CAM_BOTTOM 으로 grab 요청 시, CAM_TOP 과 물리 MilCamera 인스턴스를 공유하는 상태에서도 REVERSE_X_BOTTOM=true 가 실제 MdigControl 방향 반전에 적용된다"
    - "같은 조건에서 CAM_TOP grab 은 여전히 자신의 (ReverseX=false, ReverseY=false) 방향으로 정상 동작한다 (회귀 없음)"
    - "물리 MilCamera 인스턴스는 여전히 1개만 생성된다 (MsysAlloc 중복 호출 없음 — 공유 자체는 그대로 유지)"
    - "SIMUL_MODE(Debug/x64, 이 개발 PC 의 유일한 실행 가능 설정) 빌드가 이 변경으로 깨지지 않는다"
    - "BaslerCamera/HikCamera 경로는 이 변경의 영향을 받지 않는다 (VirtualCamera 에 새 오버로드만 추가, 기존 무인자 GrabHalconImage() 시그니처는 무변경)"
  artifacts:
    - path: "WPF_Example/Device/Camera/VirtualCamera.cs"
      provides: "새 가상 오버로드 GrabHalconImage(string requestIdentifier) — 하위 클래스가 역할별 grab 을 구현할 수 있는 계약"
      contains: "public virtual HImage GrabHalconImage(string requestIdentifier)"
    - path: "WPF_Example/Device/Camera/Mil/MilCamera.cs"
      provides: "역할별 DeviceInfo 등록/조회(_roleInfoMap) + grab 시점 방향 재적용"
      contains: "private Dictionary<string, DeviceInfo> _roleInfoMap"
    - path: "WPF_Example/Device/DeviceHandler.cs"
      provides: "MIL 카메라 공유 등록 시 역할별 DeviceInfo 등록 배선 + 범용 grab 경로에 DeviceName 전달"
      contains: "sharedMil.RegisterRoleInfo(id);"
    - path: "WPF_Example/Custom/Sequence/Bottom/Action_BottomInspection.cs"
      provides: "직접 카메라 grab 호출 경로에도 DeviceName 전달(래퍼를 거치지 않는 유일한 호출부)"
      contains: "pCamera.GrabHalconImage(pMyParam.DeviceName);"
  key_links:
    - from: "WPF_Example/Device/DeviceHandler.cs"
      to: "WPF_Example/Device/Camera/Mil/MilCamera.cs"
      via: "Initialize() MIL case 에서 공유 인스턴스에 역할별 DeviceInfo 등록"
      pattern: "sharedMil\\.RegisterRoleInfo\\(id\\)"
    - from: "WPF_Example/Device/DeviceHandler.cs"
      to: "WPF_Example/Device/Camera/VirtualCamera.cs"
      via: "범용 grab 래퍼가 요청자 식별자를 그대로 전달"
      pattern: "cam\\.GrabHalconImage\\(param\\.DeviceName\\)"
    - from: "WPF_Example/Device/Camera/Mil/MilCamera.cs"
      to: "WPF_Example/Device/Camera/Mil/MilCamera.cs"
      via: "GrabFromBuffer 내부에서 역할별 방향을 MdigControl 로 재적용(단발 grab 직전)"
      pattern: "MdigControl\\(MilDigitizer, MIL\\.M_GRAB_DIRECTION_X, grabDirectionX\\)"
    - from: "WPF_Example/Custom/Sequence/Bottom/Action_BottomInspection.cs"
      to: "WPF_Example/Device/Camera/VirtualCamera.cs"
      via: "래퍼를 거치지 않는 직접 grab 호출도 동일 계약 사용"
      pattern: "pCamera\\.GrabHalconImage\\(pMyParam\\.DeviceName\\)"
---

<objective>
CAM_BOTTOM 이 실HW(非SIMUL_MODE, CameraRole=TopBottom)에서 CAM_TOP 과 물리 MilCamera 인스턴스(RapixoCXP 보드 1대)를 공유할 때, 두 카메라가 하나의 `Info`(DeviceInfo) 필드를 공유하기 때문에 `REVERSE_X_BOTTOM=true`(`WPF_Example/Custom/Device/DeviceHandler.cs:38`)가 실제 grab 방향에 전혀 반영되지 않는 버그를 고친다.

물리 자원 공유(1개 MilCamera 인스턴스, 1회 MsysAlloc)는 그대로 유지한 채, grab 을 요청한 논리 카메라(CAM_TOP/CAM_BOTTOM) 별로 올바른 ReverseX/ReverseY/RotateAngle 을 매 grab 호출 시점에 재적용하도록 고친다. Top/Bottom 시퀀스는 이 공유 카메라에 동시에 grab 하지 않는 것이 기존 불변조건(Phase 69 조사로 확인됨)이므로, grab 직전에 방향을 다시 설정하는 방식이 안전하다.

Purpose: 실HW 배치 시 CAM_BOTTOM 촬영 이미지가 좌우 반전 없이 잘못된 방향으로 측정되는 것을 막는다.
Output: VirtualCamera 에 역할 인자를 받는 새 grab 오버로드, MilCamera 의 역할별 DeviceInfo 맵 + grab 시점 방향 재적용, 이를 실제로 배선하는 DeviceHandler/Action_BottomInspection 호출부 수정.

이 환경의 검증 한계 (스코프 결정, 재검토 불필요): 이 개발 PC 는 물리 CXP 프레임그래버 보드가 없는 SIMUL 전용 랩탑이다(DatumMeasurement.csproj 의 4개 빌드 설정 모두 SIMUL_MODE 가 정의되어 있음 — Release 설정도 예외 아님, "이 PC 는 카메라 없는 SIMUL 전용" 주석 참조). 따라서 이 PC 에서는 non-SIMUL_MODE 분기(실제 수정 대상)를 컴파일할 방법이 없다. 이번 plan 의 자동 검증은 Debug/x64(SIMUL_MODE) 빌드 성공 + grep 기반 정적 코드 검토로 한정한다. non-SIMUL_MODE 분기의 실행 동작 확인은 실HW PC 에서 별도로 이뤄져야 한다(이번 plan 범위 밖).
</objective>

<execution_context>
@$HOME/.claude/get-shit-done/workflows/execute-plan.md
@$HOME/.claude/get-shit-done/templates/summary.md
</execution_context>

<context>
@./CLAUDE.md

<coding_rules>
프로젝트 상시 코딩 규칙 (전 태스크 준수):
- C# 7.2 전용. 문자열보간(`$"..."`), 식 본문 멤버(`=>`), `switch` 식, `record`, nullable 참조형식 금지. 문자열은 string.Format 또는 + 연결.
- 삼항 연산자(`?:`) 금지 → 반드시 if/else. 단, MilCamera.cs 167-168행(Open() 내부 기존 삼항 2줄)은 이번 작업 범위 밖이므로 절대 손대지 않는다 — 새로 추가하는 코드에만 if/else 규칙을 적용한다.
- 이 plan 은 MilCamera.cs 기존 코드 스타일(camelCase 지역변수, `_` 접두 private 필드, K&R 중괄호)을 그대로 따른다. 아래 tasks 에 제시된 코드를 그대로 사용할 것 — 변수명 임의 변경 금지(다른 메서드에서 참조하는 이름과 일치해야 함).
- 날짜 주석(//YYMMDD hbk) 규칙은 폐기됨. 비자명한 "왜"만 최소 주석.
- 회귀 0. Basler/HikCamera 경로, MIL Open()/Close()/Properties/노출·게인 공유 로직은 이번 변경의 영향을 받으면 안 된다.
</coding_rules>

<interfaces>
실행자가 코드베이스를 헤맬 필요 없도록 계약을 그대로 박아둔다.

WPF_Example/Device/Camera/VirtualCamera.cs (현재, 455행 부근)
```csharp
public class VirtualCamera {
    protected DeviceInfo Info;
    // ...
    public virtual HImage GrabHalconImage() {
        SetSoftwareTriggerMode();
        return LastHalconImage;
    }
```
BaslerCamera.cs / HikCamera.cs 는 이 무인자 GrabHalconImage() 만 override 한다 — 새 오버로드를 추가해도 이 두 파일은 손댈 필요가 없다.

WPF_Example/Device/DeviceHandler.cs 상단에 정의된 DeviceInfo:
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
}
```

WPF_Example/Sequence/Param/CameraParam.cs (현재)
```csharp
public interface ICameraParam {
    string LightGroupName { get; }
    int LightLevel { get; }
    string DeviceName { get; }
    PropertyItem[] PropertyArray { get; }
    void PutImage(HImage image);
    void PutImage(Mat image);
    string SequenceName { get; }
    string ActionName { get; }
}
```
BottomInspectionParam(Action_BottomInspection.cs 내부, pMyParam 필드 타입)은 이 인터페이스를 구현하며 DeviceName 을 노출한다. pMyParam.DeviceName 은 같은 case 블록 내 로그 호출(391행)에서 이미 사용 중이라 스코프 문제 없다.

WPF_Example/Custom/Device/DeviceHandler.cs (변경 없음, 참고용 — 이번 버그의 원인이 되는 상수)
```csharp
public const bool REVERSE_X_BOTTOM = true;
public const bool REVERSE_Y_BOTTOM = false;
```
</interfaces>
</context>

<tasks>

<task type="auto">
  <name>Task 1: 역할별 grab 오버로드 계약 정의 + MilCamera 구현 (FIX-JTJ-01)</name>
  <files>
    WPF_Example/Device/Camera/VirtualCamera.cs,
    WPF_Example/Device/Camera/Mil/MilCamera.cs
  </files>
  <action>
FIX-JTJ-01 의 핵심 로직을 구현한다. 아래 코드를 그대로 적용한다(변수명·시그니처 변경 금지 — Task 2 가 정확히 이 이름들을 참조한다).

(1) WPF_Example/Device/Camera/VirtualCamera.cs — 기존 `public virtual HImage GrabHalconImage()` (약 455-458행) 바로 아래에 새 오버로드를 추가한다. 시그니처 변경이 아니라 오버로드 추가이므로 BaslerCamera.cs/HikCamera.cs 는 손대지 않는다(둘 다 무인자 버전만 override 하며, 새 오버로드의 기본 구현이 가상 디스패치로 그 override 를 그대로 타게 된다):
```csharp
public virtual HImage GrabHalconImage(string requestIdentifier) {
    return GrabHalconImage();
}
```

(2) WPF_Example/Device/Camera/Mil/MilCamera.cs

using 절 상단에 `using System.Collections.Generic;` 을 추가한다(`using System;` 근처).

생성자 `public MilCamera(DisplayConfig config, DeviceInfo info) : base(...)` 본문 끝에 `RegisterRoleInfo(info);` 호출을 추가한다(기존 `Properties.Width = info.Width; Properties.Height = info.Height;` 다음 줄).

역할별 정보 저장용 필드를 클래스 상단(MilDigitizer/MilBuffer 필드 그룹 근처)에 추가한다:
```csharp
private Dictionary<string, DeviceInfo> _roleInfoMap = new Dictionary<string, DeviceInfo>();
```

GetMilErrorMessage() 바로 앞에 아래 두 메서드를 추가한다:
```csharp
public void RegisterRoleInfo(DeviceInfo roleInfo) {
    if (roleInfo == null) {
        return;
    }
    if (string.IsNullOrEmpty(roleInfo.Identifier)) {
        return;
    }
    _roleInfoMap[roleInfo.Identifier] = roleInfo;
}

private DeviceInfo ResolveRoleInfo(string requestIdentifier) {
    if (requestIdentifier == null) {
        return Info;
    }
    DeviceInfo roleInfo;
    if (_roleInfoMap.TryGetValue(requestIdentifier, out roleInfo)) {
        return roleInfo;
    }
    return Info;
}
```

기존 `public override HImage GrabHalconImage()` (현재 약 222-264행, `#if SIMUL_MODE ... #else ... #endif` 전체 블록)를 아래로 완전히 교체한다. SIMUL_MODE 분기와 스트리밍 가드/try-catch 는 기존 그대로 옮기되, roleInfo 를 도입해 실제 grab 에 사용한다:
```csharp
public override HImage GrabHalconImage() {
    return GrabHalconImage(Info.Identifier);
}

public override HImage GrabHalconImage(string requestIdentifier) {
#if SIMUL_MODE
    // D-11: SIMUL_MODE 에서는 base 파일 grab 경로(LastHalconImage) 로 폴백
    return LastHalconImage;
#else
    // 라이브 스트리밍 중에는 라이브 스레드가 MilBuffer 를 점유한다.
    // 여기서 또 MdigGrab 하면 같은 버퍼를 동시에 건드려 충돌하므로 grab 하지 않는다.
    if (CaptureMode == ECaptureModeType.Streaming) {
        Logging.PrintLog((int)ELogType.Camera, "[WARN] {0} Streaming 모드에서 검사 grab 요청 — stale 프레임 반환 금지, null 처리(라이브뷰 창을 닫고 재시도)", Name);
        return null;
    }

    DeviceInfo roleInfo = ResolveRoleInfo(requestIdentifier);

    try {
        // 단발 grab → 버퍼에서 독립 HImage(복사본) 획득. 요청자(requestIdentifier)의 역할별 방향/회전을 사용한다.
        HImage grabbed = GrabFromBuffer(roleInfo);
        if (grabbed == null) {
            return null;
        }

        lock (Interlock) {
            if (LastGrabHalconImage != null) {
                LastGrabHalconImage.Dispose();
            }
            LastGrabHalconImage = grabbed;
        }
        Interlocked.Increment(ref imageCount);
        return LastHalconImage;
    }
    catch (Exception e) {
        Logging.PrintLog((int)ELogType.Camera, "[ERROR] {0} MilCamera.GrabHalconImage ({1})", Name, e.Message);
        Interlocked.Increment(ref errorCount);
        return null;
    }
#endif
}
```

`private HImage GrabFromBuffer()` 의 시그니처를 `private HImage GrabFromBuffer(DeviceInfo roleInfo)` 로 바꾼다. 메서드 본문 내부 수정 3곳:

1. `MIL.MdigGrab(MilDigitizer, MilBuffer);` 호출 바로 앞에 방향 재적용 코드를 삽입한다(삼항 금지, if/else):
```csharp
MIL_INT grabDirectionX;
if (roleInfo.ReverseX) {
    grabDirectionX = MIL.M_REVERSE;
}
else {
    grabDirectionX = MIL.M_NORMAL;
}
MIL_INT grabDirectionY;
if (roleInfo.ReverseY) {
    grabDirectionY = MIL.M_REVERSE;
}
else {
    grabDirectionY = MIL.M_NORMAL;
}
MIL.MdigControl(MilDigitizer, MIL.M_GRAB_DIRECTION_X, grabDirectionX);
MIL.MdigControl(MilDigitizer, MIL.M_GRAB_DIRECTION_Y, grabDirectionY);
```
2. grab 실패 로그 줄(MappGetError 체크 직후, "[ERROR] {0} MdigGrab failed ..." 줄) 의 `Info.Identifier` 를 `roleInfo.Identifier` 로 바꾼다. 이 메서드 안의 이 로그 줄 1곳만 바꾼다 — 다른 메서드(TryReadFeature/TryWriteFeature/Open/GetMilErrorMessage 등)의 Info.Identifier 는 절대 건드리지 않는다.
3. 회전 처리 if/else-if 체인의 `Info.RotateAngle` 3곳(_90/_180/_270 비교) 을 모두 `roleInfo.RotateAngle` 로 바꾼다.

`LiveLoop()` 안의 `HImage frame = GrabFromBuffer();` 를 `HImage frame = GrabFromBuffer(Info);` 로 바꾼다(라이브 미리보기는 이 인스턴스 자신의 기본 Info 를 그대로 사용 — 이번 수정으로 동작 변경 없음, 새 시그니처에 맞추기 위한 컴파일 대응일 뿐).

절대 손대지 않을 곳: Open() 메서드 내부 167-168행(`MdigControl(..., Info.ReverseX ? MIL.M_REVERSE : MIL.M_NORMAL)` 삼항 2줄) — grab 마다 재적용되므로 이 줄은 이제 무해한 중복 기본값이 된다. TryReadFeature/TryWriteFeature/Open/Close/GetMilErrorMessage/GetFeatureDiagnostics/CreateImageFromPaddedBuffer/SetSoftwareTriggerMode/SetTriggerMode/StartStream/StopStream 은 전부 무변경.
  </action>
  <verify>
    <automated>
cd "C:/Info/Project/DataMeasurement"
V=WPF_Example/Device/Camera/VirtualCamera.cs
M=WPF_Example/Device/Camera/Mil/MilCamera.cs
echo "virtualcamera_overload(want1)=$(grep -cF 'public virtual HImage GrabHalconImage(string requestIdentifier)' "$V")"
echo "role_map_field(want1+)=$(grep -cF '_roleInfoMap' "$M")"
echo "register_role(want1)=$(grep -cF 'public void RegisterRoleInfo(DeviceInfo roleInfo)' "$M")"
echo "resolve_role(want1)=$(grep -cF 'private DeviceInfo ResolveRoleInfo(string requestIdentifier)' "$M")"
echo "ctor_call(want1)=$(grep -cF 'RegisterRoleInfo(info);' "$M")"
echo "noarg_override(want1)=$(grep -cF 'public override HImage GrabHalconImage() {' "$M")"
echo "string_override(want1)=$(grep -cF 'public override HImage GrabHalconImage(string requestIdentifier) {' "$M")"
echo "grabfrombuffer_sig(want1)=$(grep -cF 'private HImage GrabFromBuffer(DeviceInfo roleInfo)' "$M")"
echo "old_call_zeroarg(want0)=$(grep -cE 'GrabFromBuffer\(\)' "$M")"
echo "liveloop_call(want1)=$(grep -cF 'GrabFromBuffer(Info)' "$M")"
echo "dir_x(want1)=$(grep -cF 'MIL.MdigControl(MilDigitizer, MIL.M_GRAB_DIRECTION_X, grabDirectionX);' "$M")"
echo "dir_y(want1)=$(grep -cF 'MIL.MdigControl(MilDigitizer, MIL.M_GRAB_DIRECTION_Y, grabDirectionY);' "$M")"
echo "roleinfo_rotate(want3)=$(grep -cF 'roleInfo.RotateAngle' "$M")"
echo "open_untouched(want1)=$(grep -cF 'MIL.MdigControl(MilDigitizer, MIL.M_GRAB_DIRECTION_X, Info.ReverseX ? MIL.M_REVERSE : MIL.M_NORMAL);' "$M")"
echo "new_ternary_count(want0)="
git diff -U0 -- "$M" "$V" 2>/dev/null | grep '^+' | grep -vE '^\+\+\+' | grep -cE '[?][^?:]*:'
echo "basler_hik_diff_must_be_empty:"
git diff --name-only -- WPF_Example/Device/Camera/Basler/BaslerCamera.cs WPF_Example/Device/Camera/Hik/HikCamera.cs
echo "build_debug_x64_simul:"
"C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/MSBuild.exe" WPF_Example/DatumMeasurement.csproj //t:Build //p:Configuration=Debug //p:Platform=x64 //v:m //nologo 2>&1 | grep -E "error CS|warning CS" | grep -v -E "CS0618|CS0162" | head -20
echo "CS_LIST_ABOVE_MUST_BE_EMPTY"
    </automated>
  </verify>
  <done>
VirtualCamera.cs 에 GrabHalconImage(string requestIdentifier) 가상 오버로드 추가(무인자 버전 무변경, Basler/HIK 파일 diff 없음). MilCamera.cs 에 _roleInfoMap/RegisterRoleInfo/ResolveRoleInfo 추가, 생성자에서 자기 자신을 등록. GrabHalconImage()가 GrabHalconImage(Info.Identifier)로 위임하고, 새 GrabHalconImage(string)이 ResolveRoleInfo → GrabFromBuffer(roleInfo)로 연결된다. GrabFromBuffer(DeviceInfo roleInfo)가 MdigGrab 직전 MdigControl로 X/Y 방향을 재적용하고, 회전 3분기와 실패 로그 1곳에 roleInfo를 사용한다. LiveLoop은 GrabFromBuffer(Info)로 컴파일 대응. Open() 167-168행의 기존 삼항은 무변경. Debug/x64 빌드 error CS 0건, 신규 warning CS 0건.
  </done>
</task>

<task type="auto">
  <name>Task 2: 소비처 배선 — DeviceHandler 등록/래퍼 + Action_BottomInspection 직접호출 (FIX-JTJ-01)</name>
  <files>
    WPF_Example/Device/DeviceHandler.cs,
    WPF_Example/Custom/Sequence/Bottom/Action_BottomInspection.cs
  </files>
  <action>
Task 1 에서 만든 계약을 실제로 사용하도록 호출부 2곳을 배선한다.

(1) WPF_Example/Device/DeviceHandler.cs

`public HImage GrabHalconImage(ICameraParam param)` (약 326-332행) 마지막 줄:
```csharp
return cam.GrabHalconImage();
```
를
```csharp
return cam.GrabHalconImage(param.DeviceName);
```
로 바꾼다. (Action_TopInspection.cs/Action_FAIMeasurement.cs/MainView.xaml.cs 등 이 래퍼를 거치는 모든 grab 호출부가 자동으로 요청자 식별자를 전달하게 된다 — 그 파일들은 수정하지 않는다.)

`Initialize()` 의 `case ECameraType.MIL:` 블록, `#else`(非SIMUL) 분기 안, `if (sharedMil != null) { ... }` 블록에서 `Devices.Add(id.Identifier, sharedMil);` 바로 앞줄에 아래를 추가한다:
```csharp
// 물리 MIL 핸들은 공유하지만, ReverseX/Y·RotateAngle 같은 역할별 grab 방향 설정은 별도로 등록해야
// grab 시점(GrabFromBuffer)에 요청한 논리 카메라(CAM_TOP/CAM_BOTTOM)에 맞는 방향이 적용된다.
sharedMil.RegisterRoleInfo(id);
Devices.Add(id.Identifier, sharedMil);
```

(2) WPF_Example/Custom/Sequence/Bottom/Action_BottomInspection.cs

`EStep.Grab` case 블록(약 388행) 안의:
```csharp
var image = pCamera.GrabHalconImage();
```
를
```csharp
var image = pCamera.GrabHalconImage(pMyParam.DeviceName);
```
로 바꾼다. (이 호출은 `SystemHandler.Handle.Devices.GrabHalconImage(param)` 래퍼를 거치지 않고 카메라 객체를 직접 호출하는 유일한 경로라 별도로 고쳐야 한다. pMyParam.DeviceName 은 같은 case 블록 몇 줄 아래(391행) 로그에서 이미 사용 중이라 스코프 문제 없다.)
  </action>
  <verify>
    <automated>
cd "C:/Info/Project/DataMeasurement"
D=WPF_Example/Device/DeviceHandler.cs
A=WPF_Example/Custom/Sequence/Bottom/Action_BottomInspection.cs
echo "wrapper_new(want1)=$(grep -cF 'return cam.GrabHalconImage(param.DeviceName);' "$D")"
echo "wrapper_old(want0)=$(grep -cF 'return cam.GrabHalconImage();' "$D")"
echo "register_call(want1)=$(grep -cF 'sharedMil.RegisterRoleInfo(id);' "$D")"
echo "order_context:"
grep -A1 -F 'sharedMil.RegisterRoleInfo(id);' "$D"
echo "action_new(want1)=$(grep -cF 'pCamera.GrabHalconImage(pMyParam.DeviceName);' "$A")"
echo "action_old(want0)=$(grep -cE 'pCamera\.GrabHalconImage\(\);' "$A")"
echo "new_ternary_count(want0)="
git diff -U0 -- "$D" "$A" 2>/dev/null | grep '^+' | grep -vE '^\+\+\+' | grep -cE '[?][^?:]*:'
echo "other_callers_diff_must_be_empty:"
git diff --name-only -- WPF_Example/Custom/Sequence/Top/Action_TopInspection.cs WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs WPF_Example/UI/ContentItem/MainView.xaml.cs
echo "build_debug_x64_simul:"
"C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/MSBuild.exe" WPF_Example/DatumMeasurement.csproj //t:Build //p:Configuration=Debug //p:Platform=x64 //v:m //nologo 2>&1 | grep -E "error CS|warning CS" | grep -v -E "CS0618|CS0162" | head -20
echo "CS_LIST_ABOVE_MUST_BE_EMPTY"
    </automated>
  </verify>
  <done>
DeviceHandler.GrabHalconImage(ICameraParam)의 마지막 줄이 param.DeviceName을 전달한다(옛 무인자 호출 0건). Initialize()의 MIL 공유 분기에서 Devices.Add 직전에 sharedMil.RegisterRoleInfo(id)가 호출된다. Action_BottomInspection.cs의 직접 grab 호출이 pMyParam.DeviceName을 전달한다(옛 무인자 호출 0건). Action_TopInspection.cs/Action_FAIMeasurement.cs/MainView.xaml.cs는 diff에 나타나지 않는다(래퍼 수정만으로 자동 적용됨). Debug/x64 빌드 error CS 0건, 신규 warning CS 0건.
  </done>
</task>

</tasks>

<threat_model>
## Trust Boundaries

| Boundary | Description |
|----------|-------------|
| SequenceThread → MIL 하드웨어 그래버 | 검사 스레드가 물리 CXP 프레임그래버(RapixoCXP 보드)를 직접 제어. 외부/네트워크 입력이 이 경계를 넘지 않음(모든 파라미터는 내부 DeviceInfo/설정값). |
| Top 시퀀스 스레드 ↔ Bottom 시퀀스 스레드 (공유 MilCamera 인스턴스) | 두 스레드가 동일 물리 핸들(MilDigitizer/MilBuffer)에 접근 가능. 이번 변경은 이 공유 자체나 상호배제 정책을 바꾸지 않는다 — grab 시점에 조회하는 DeviceInfo 만 역할별로 분리한다. |

## STRIDE Threat Register

| Threat ID | Category | Component | Disposition | Mitigation Plan |
|-----------|----------|-----------|-------------|-----------------|
| T-JTJ-01 | Tampering (내부 상태 오염) | MilCamera._roleInfoMap | accept | 외부 입력이 아닌 내부 DeviceInfo(상수 REVERSE_X_BOTTOM 등)로만 채워짐. 조회 실패 시 ResolveRoleInfo 가 안전하게 자기 자신의 Info 로 폴백(예외 없음). |
| T-JTJ-02 | Denial of Service (동시 grab 경합) | MilCamera.GrabFromBuffer (공유 MilDigitizer/MilBuffer) | accept | Top/Bottom 은 이미 존재하는 상호배제 불변조건(Phase 69 조사로 확인, 동시 grab 하지 않음)에 의존. 이번 변경은 이 불변조건을 강화/약화하지 않으며, 락 도입은 스코프 밖(요청 사항에 명시)이라 하드코딩된 정책 유지로만 대응한다. |
| T-JTJ-03 | Information Disclosure / Repudiation | 없음 (해당 없음) | accept | 이 변경은 로그 방향/문자열 외 사용자 데이터를 다루지 않으며, 새로운 파일 I/O·네트워크 노출이 없다. |
</threat_model>

<verification>
- Debug/x64(SIMUL_MODE) 빌드: error CS 0건, 신규 warning CS 0건
- VirtualCamera.cs 의 새 오버로드가 BaslerCamera.cs/HikCamera.cs 에 diff 를 발생시키지 않음(git diff --name-only 확인)
- MilCamera.cs: _roleInfoMap/RegisterRoleInfo/ResolveRoleInfo 존재, GrabHalconImage() → GrabHalconImage(Info.Identifier) 위임, GrabFromBuffer(DeviceInfo roleInfo) 시그니처, roleInfo.RotateAngle 3곳, MdigControl 방향 재적용 2곳(X/Y)
- MilCamera.cs Open() 167-168행의 기존 삼항 연산자 무변경(정확히 그대로 존재)
- DeviceHandler.cs: GrabHalconImage(ICameraParam) 가 param.DeviceName 전달, Initialize() MIL 공유 분기에서 sharedMil.RegisterRoleInfo(id) 가 Devices.Add 직전에 호출됨
- Action_BottomInspection.cs: 직접 grab 호출이 pMyParam.DeviceName 전달
- Action_TopInspection.cs/Action_FAIMeasurement.cs/MainView.xaml.cs 는 diff 에 나타나지 않음(래퍼만 수정해도 자동 적용되는 설계 검증)
- git diff -U0 신규 추가 라인에 삼항 연산자(`?:`) 0건 (Open() 기존 라인 제외)
- 이 PC 에서는 non-SIMUL_MODE 분기를 컴파일할 수 없으므로, GrabFromBuffer/GrabHalconImage(string) 의 non-SIMUL 분기는 이 검증에서 grep 기반 정적 검토로만 확인되고 실HW 컴파일/런타임 검증은 범위 밖으로 명시적으로 이월한다.
</verification>

<success_criteria>
1. VirtualCamera 에 GrabHalconImage(string requestIdentifier) 오버로드가 추가되고 Basler/HIK 카메라 코드는 무수정이다.
2. MilCamera 가 역할별 DeviceInfo(_roleInfoMap)를 보관하고, grab 시점(GrabFromBuffer)에 요청자 식별자에 맞는 ReverseX/ReverseY/RotateAngle 을 MdigControl 로 재적용한다.
3. DeviceHandler 가 MIL 공유 인스턴스 등록 시 역할별 DeviceInfo 를 등록하고, 범용 grab 래퍼가 요청자 식별자를 전달한다.
4. Action_BottomInspection.cs 의 래퍼를 거치지 않는 직접 grab 호출도 동일하게 요청자 식별자를 전달한다.
5. 물리 MilCamera 인스턴스 공유(1개, MsysAlloc 1회) 는 그대로 유지되며 회귀가 없다.
6. Debug/x64(SIMUL_MODE) 빌드가 통과한다. non-SIMUL_MODE 분기의 실HW 검증은 이 plan 범위 밖으로 명시적으로 이월된다(이 개발 PC 의 하드웨어 제약).
</success_criteria>

<output>
After completion, create `.planning/quick/260805-jtj-cam-bottom-milcamera-reversex-y-grab-dev/260805-jtj-SUMMARY.md`.

SUMMARY 에 아래 1건을 carry-over 로 명시적으로 기록한다:
- non-SIMUL_MODE(실HW) 분기 런타임 검증 미수행 — 이 개발 PC 는 물리 CXP 보드가 없는 SIMUL 전용 랩탑이라 컴파일조차 불가능했다(4개 빌드 설정 전부 SIMUL_MODE 정의됨). 실HW PC 배치 시 CAM_BOTTOM grab 이미지가 CAM_TOP 대비 좌우 반전되어 나오는지(REVERSE_X_BOTTOM=true 정상 적용 여부) 반드시 육안 확인 필요.
</output>
