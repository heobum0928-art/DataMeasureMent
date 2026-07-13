---
phase: quick-260713-nse
plan: 01
type: execute
wave: 1
depends_on: []
files_modified:
  - WPF_Example/Custom/Sequence/Inspection/ShotConfig.cs
  - WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs
  - WPF_Example/Device/LightController/LightHandler.cs
autonomous: true
requirements: [LIGHT-CH-01]

must_haves:
  truths:
    - "Shot 마다 Ring 6채널 각각의 밝기(0~255)와 On/Off 를 개별 설정할 수 있다"
    - "Shot 마다 Bar 4채널 각각의 밝기(0~255)와 On/Off 를 개별 설정할 수 있다"
    - "검사 PropertyGrid 의 Light 탭에 Ring 6줄 + Bar 4줄이 채널별로 표시된다 (XAML 수정 없이 자동 생성)"
    - "신규 채널 키가 없는 구 레시피를 로드하면 구 RingLight_Brightness/Enabled 가 6채널 전부로, 구 SideLight_* 가 4채널 전부로 브로드캐스트되어 조명이 소등되지 않는다"
    - "$PREP 수신 시 ApplyShotLightsInternal 이 Ring/Bar 를 채널별 개별 API 로 적용하고, 채널명→(controllerIndex, channel) 매핑으로 조회하여 백라이트를 오작동시키지 않는다"
    - "BACK/RING7/ALIGN_COAX 는 기존 그룹 API 동작 그대로 유지되고, TurnOffShotLights 는 무변경으로 전 조명을 소등한다"
    - "Shot Copy/Paste 시 조명 필드(신규 10채널 + 기존 8필드)와 PixelResolution/CorrectionFactor 가 복사된다"
  artifacts:
    - path: "WPF_Example/Custom/Sequence/Inspection/ShotConfig.cs"
      provides: "RingLight_Enabled_1~6 / RingLight_Brightness_1~6 / SideLight_Enabled_1~4 / SideLight_Brightness_1~4 스칼라 프로퍼티 + Load override(구레시피 브로드캐스트 마이그레이션) + CopyTo override"
      contains: "RingLight_Brightness_1"
    - path: "WPF_Example/Device/LightController/LightHandler.cs"
      provides: "채널명 기반 개별 제어 헬퍼(SetChannelOnOff / SetChannelLevel) — 기존 public 시그니처 무변경, 추가만"
      contains: "SetChannelOnOff"
    - path: "WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs"
      provides: "ApplyShotLightsInternal Ring/Bar 채널별 개별 적용"
      contains: "RingLight_Brightness_1"
  key_links:
    - from: "WPF_Example/Custom/Sequence/Inspection/ShotConfig.cs"
      to: "ParamBase.Save/Load (INI 키 = 프로퍼티명)"
      via: "Int32/Boolean 스칼라 프로퍼티 (컬렉션 금지 — ParamBase switch 미지원)"
      pattern: "public int RingLight_Brightness_1"
    - from: "WPF_Example/Custom/Sequence/Inspection/ShotConfig.cs (Load override)"
      to: "IniFile.TryGetSection / IniSection.ContainsKey"
      via: "신규 키 부재 검사 → 구 키 값 브로드캐스트 (CameraSlaveParam.Load:172-182 패턴 복제)"
      pattern: "ContainsKey\\(\"RingLight_Brightness_1\"\\)"
    - from: "WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs (ApplyShotLightsInternal)"
      to: "LightHandler 채널명 헬퍼 → SetOnOff(int,int,bool)/SetLevel(int,int,int)"
      via: "LightHandler.LIGHT_RING_CH1~6 / LIGHT_BAR_1~4 채널명 조회"
      pattern: "SetChannelLevel\\(LightHandler\\.LIGHT_RING_CH1"
---

<objective>
조명 Ring 6채널 + Bar 4채널을 Shot 단위로 **채널별 개별 밝기 + 개별 On/Off** 제어하도록 확장한다.

Purpose: 현재 RING/BAR 는 그룹 통합 제어라 한 Shot 안에서 "링 1·3번만 켜고 2·4·5·6번은 끄기" 같은 조합이 불가능하다. 검사 조건별 조명 조합 자유도를 확보한다.
Output: ShotConfig 스칼라 프로퍼티 20개 + 구 레시피 마이그레이션(Load override) + PropertyGrid 자동 확장 + ApplyShotLightsInternal 채널 API 전환 + CopyTo override.

**최대 리스크 = 구 레시피 조명 전소등 회귀.** ParamBase.Load 는 INI 누락 키를 0/false 로 클로버한다. Task 2 의 Load override 가 없으면 현장 구 레시피 로드 시 Ring/Bar 전 채널 Brightness=0/Enabled=false → 블랙 이미지 → 전 검사 NG. Task 2 는 선택이 아니라 필수 안전장치다.
</objective>

<execution_context>
@$HOME/.claude/get-shit-done/workflows/execute-plan.md
@$HOME/.claude/get-shit-done/templates/summary.md
</execution_context>

<context>
@CLAUDE.md
@.planning/STATE.md

@WPF_Example/Custom/Sequence/Inspection/ShotConfig.cs
@WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs
@WPF_Example/Custom/Device/LightHandler.cs
@WPF_Example/Device/LightController/LightHandler.cs
@WPF_Example/Device/LightController/LightGroup.cs
@WPF_Example/Sequence/Param/CameraSlaveParam.cs

<interfaces>
<!-- 조사 완료된 계약 — 코드베이스 재탐색 불필요 -->

ParamBase 직렬화 지원 타입 (Sequence/Param/ParamBase.cs:318-430, switch(prop.PropertyType.Name)):
  Int32, Double, String, Boolean, Rect, Line, Circle, PropertyItem[], ModelFinderViewModel
  → default: break (int[] / List<int> / bool[] 는 조용히 무시됨. **스칼라 프로퍼티가 유일한 경로**)

누락 키 클로버 (Utility/Ini.cs):
  IniSection.this[string] (953-959) 키 부재 → IniValue.Default
  IniValue.ToInt() → 0 (179), ToBool() → false (153)

마이그레이션 선례 (Sequence/Param/CameraSlaveParam.cs:172-182) — 이 패턴을 그대로 복제:
```csharp
public override bool Load(IniFile loadFile, string groupName) {
    bool result = base.Load(loadFile, groupName);
    IniSection sec;
    if (!loadFile.TryGetSection(groupName, out sec) || sec == null || !sec.ContainsKey("CorrectionFactor")) {
        CorrectionFactor = 1.0;
    }
    return result;
}
```

LightHandler 채널 물리 매핑 (Custom/Device/LightHandler.cs:41-73) — **RegisterLightController 는 이번 작업에서 수정 금지**:
  Controller A (Index=0): ch0~5 = RING_CH1~RING_CH6, ch6 = ALIGN_COAX
  Controller B (Index=1): ch0 = BACK, ch1~4 = BAR_1~BAR_4, ch5 = RING7
  → **Bar 를 ch0 부터 쓰면 백라이트 오작동. 인덱스 하드코딩 금지.**

LightHandler 기존 public API (Device/LightController/LightHandler.cs):
```csharp
public LightGroup GetGroup(string groupName);              // 155
public bool SetOnOff(string groupName, bool onOff);        // 175  (group[i].Index/.Channel 로 개별 API 호출)
public bool SetLevel(string groupName, int level);         // 188
public void SetOnOff(int index, int channel, bool on);     // 271  ← 개별 채널 API (이미 존재)
public void SetLevel(int index, int channel, int level);   // 296  ← 개별 채널 API (이미 존재)
public List<VirtualLightController> Controllers;           // con[j].Name = 채널명, con.Index = 컨트롤러 index
```
LightGroupItem (Device/LightController/LightGroup.cs:9-19): { string Name; int Index; int Channel; }
VirtualLightController: `con[j].Name` (채널명), `con.ChannelCount`, `con.Index`

Slidable 선례 (Device/DisplayConfig.cs:41-42, Device/Camera/Hik/HikCameraProperty.cs:27-28):
  `[Slidable(0, 255)]` — PropertyTools.DataAnnotations. 맨 int 는 평문 텍스트박스(범위 강제 없음).

[Category("탭|그룹")] 상속 규칙 (ShotConfig.cs:61-63): 첫 프로퍼티에만 붙이면 이후 연속 프로퍼티가 상속.
</interfaces>
</context>

<constraints>
**프로젝트 규칙 (위반 시 리젝트):**
- 삼항 연산자 `?:` 금지 → if-else 만 사용
- C# 7.2 문법만 (record / `??=` / `using var` / switch expression 금지)
- 파일별 기존 브레이스 스타일 유지: ShotConfig.cs = 메서드 K&R(`) {`), InspectionSequence.cs = Allman(`)` 다음 줄 `{`), LightHandler.cs = K&R
- **`//YYMMDD hbk` 날짜+이니셜 주석 형식 신규 작성 금지** (2026-06-11 폐기). 기존 주석은 건드리지 말고, 새 주석은 비자명한 "왜"만 한글로 간결히.
- 신규 .cs 파일 없음 (기존 3파일 수정만) → csproj 수정 불필요

**변경 금지 (회귀 가드):**
- `Custom/Device/LightHandler.cs` 의 `RegisterLightController()` — 컨트롤러/그룹 등록 구조 그대로. 임시 배선은 light.ini 로 대응.
- `InspectionSequence.TurnOffShotLights()` (337-344) — 그룹 OFF 가 그대로 유효
- `ApplyShotLightsInternal` 의 BACK / ALIGN_COAX / RING7 분기 — 1채널이라 그룹 API 동작 동일
- LightHandler 기존 public 메서드 시그니처 — **추가만 허용, 수정 금지**
- ShotConfig 의 BackLight_* / CoaxLight_* / Ring7Light_* 필드
- 구 필드 RingLight_Enabled / RingLight_Brightness / SideLight_Enabled / SideLight_Brightness — **삭제 금지** (마이그레이션 소스 + 롤백 여지)
</constraints>

<tasks>

<task type="auto">
  <name>Task 1: ShotConfig 채널별 스칼라 프로퍼티 20개 선언 + 구 필드 UI 숨김</name>
  <files>WPF_Example/Custom/Sequence/Inspection/ShotConfig.cs</files>
  <action>
ShotConfig.cs 의 "Multi-Light" 블록(현재 60-81줄)을 확장한다.

1) 신규 Ring 채널 프로퍼티 12개:
```csharp
[Category("Light|Ring")]
public bool RingLight_Enabled_1 { get; set; }
[Slidable(0, 255)]
public int RingLight_Brightness_1 { get; set; }
public bool RingLight_Enabled_2 { get; set; }
[Slidable(0, 255)]
public int RingLight_Brightness_2 { get; set; }
... (_3 ~ _6 동일 패턴)
```
2) 신규 Bar 채널 프로퍼티 8개 — `[Category("Light|Bar")]` 그룹으로 시작, `SideLight_Enabled_1~4` + `[Slidable(0,255)] SideLight_Brightness_1~4`.
   ※ INI 키 하위호환/코드 일관성 위해 프로퍼티명 접두사는 기존 `SideLight_` 유지 (물리 조명은 Bar).

3) 구 필드 4개(`RingLight_Enabled`, `RingLight_Brightness`, `SideLight_Enabled`, `SideLight_Brightness`)는 **삭제하지 말고 각각 `[Browsable(false)]` 만 추가**하여 PropertyGrid 에서 숨긴다. INI 키/직렬화는 그대로 유지 → Task 2 마이그레이션 소스 + 구버전 롤백 시 graceful downgrade.
   (선례: CoaxLight_* 의 `[Browsable(false)]` 숨김, ShotConfig.cs:69-72)
   ⚠ `[Browsable(false)]` 는 **어트리뷰트가 붙은 프로퍼티 1개에만** 적용되므로 4개 각각에 개별로 붙일 것 (Phase 66 IN-01 함정).
   ⚠ `[Category]` 상속 규칙 때문에 구 필드를 숨겨도 그 다음 프로퍼티가 Category 를 상속하는지 확인 — 각 그룹 첫 "보이는" 프로퍼티에 `[Category]` 가 명시되도록 배치할 것.

4) `using PropertyTools.DataAnnotations;` 는 이미 있음(4줄) — Slidable 도 여기 포함. 추가 using 불필요하나 컴파일 에러 시 확인.

BACK/RING7/ALIGN_COAX 필드는 손대지 않는다.
  </action>
  <verify>
    <automated>"C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/MSBuild.exe" WPF_Example/DatumMeasurement.csproj -p:Configuration=Debug -p:Platform=x64 -t:Build -v:minimal -nologo</automated>
  </verify>
  <done>빌드 PASS. ShotConfig 에 RingLight_Enabled_1~6 / RingLight_Brightness_1~6 / SideLight_Enabled_1~4 / SideLight_Brightness_1~4 (총 20개) 존재. 구 4필드는 `[Browsable(false)]` 부착 상태로 잔존.</done>
</task>

<task type="auto">
  <name>Task 2: ShotConfig.Load override (구 레시피 브로드캐스트 마이그레이션) + CopyTo override</name>
  <files>WPF_Example/Custom/Sequence/Inspection/ShotConfig.cs</files>
  <action>
**(A) Load override — 이번 작업의 핵심 안전장치.**

`CameraSlaveParam.Load`(CameraSlaveParam.cs:172-182) 패턴을 그대로 복제:

```csharp
public override bool Load(IniFile loadFile, string groupName) {
    bool result = base.Load(loadFile, groupName);   // CameraSlaveParam.Load → CorrectionFactor 복원 포함

    // ParamBase.Load 는 INI 누락 키를 0/false 로 덮어쓴다.
    // 구 레시피엔 채널별 키가 없어 Ring/Bar 전 채널이 0/false 로 로드됨 → 조명 전소등 → 전 검사 블랙이미지.
    // 신규 키 부재 = 구 레시피 → 구 통합 필드 값을 채널 전체로 브로드캐스트.
    IniSection sec;
    bool bHasSection = loadFile.TryGetSection(groupName, out sec) && sec != null;

    bool bHasRingChannelKeys = bHasSection && sec.ContainsKey("RingLight_Brightness_1");
    if (!bHasRingChannelKeys) {
        RingLight_Enabled_1 = RingLight_Enabled;  ... _2~_6 동일
        RingLight_Brightness_1 = RingLight_Brightness;  ... _2~_6 동일
    }

    bool bHasBarChannelKeys = bHasSection && sec.ContainsKey("SideLight_Brightness_1");
    if (!bHasBarChannelKeys) {
        SideLight_Enabled_1~4 = SideLight_Enabled;
        SideLight_Brightness_1~4 = SideLight_Brightness;
    }
    return result;
}
```
- 신규 키가 **있으면** 아무것도 하지 않는다 (ParamBase 가 이미 채널별로 정확히 로드함).
- 반드시 `base.Load` **다음**에 실행 (base 가 0 으로 덮어쓴 뒤 복원해야 함).
- 삼항 금지 — 위 예시대로 if 문만.
- `ApplyShotDefaults` 에 넣지 말 것 (IniFile 접근이 없어 키 부재 판별 불가).
- 필요한 using: `ReringProject.Utility` (IniFile/IniSection) — 이미 5줄에 있음.

**(B) CopyTo override 신설 (기존 버그 수정).**

ShotConfig 에 CopyTo override 가 없어서 지금도 Copy/Paste(InspectionListView.xaml.cs:831) 시 조명 8필드·PixelResolution·CorrectionFactor 가 복사되지 않는다. 채널이 20개로 늘면 문제가 커지므로 함께 처리:

```csharp
public override bool CopyTo(ParamBase param) {
    bool result = base.CopyTo(param);   // CameraSlaveParam.CopyTo — LightLevel/PropertyArray/Motor/Frame 등
    ShotConfig target = param as ShotConfig;
    if (target == null) return result;

    // 조명 신규 20채널 + 구 8필드 + 해상도/보정 — base.CopyTo 가 다루지 않는 ShotConfig 고유 필드
    target.RingLight_Enabled_1 = RingLight_Enabled_1;  ... (20개 전부)
    target.RingLight_Enabled = RingLight_Enabled; ... (구 8필드: Ring/Back/Coax/Side/Ring7)
    target.PixelResolution = PixelResolution;       // 프로퍼티 실제 존재 여부 확인 후 포함
    target.CorrectionFactor = CorrectionFactor;
    target.ZPosition / DelayMs / ZIndex 등 — 조명 외 필드는 기존 동작 변화 최소화를 위해 신중히 판단.
    return true;
}
```
- ShotName / FAIList / _image 는 **복사하지 말 것** (이름 충돌·이미지 소유권 문제).
- PixelResolution / CorrectionFactor 가 CameraSlaveParam 소유이고 base.CopyTo 가 이미 복사한다면 중복 대입하지 말 것 — 코드를 확인해 base 가 안 하는 것만 추가.
  </action>
  <verify>
    <automated>"C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/MSBuild.exe" WPF_Example/DatumMeasurement.csproj -p:Configuration=Debug -p:Platform=x64 -t:Build -v:minimal -nologo</automated>
  </verify>
  <done>빌드 PASS. ShotConfig.Load override 가 base.Load 후 신규키 부재 시 구 값을 Ring 6 / Bar 4 채널로 브로드캐스트. ShotConfig.CopyTo override 존재하며 조명 전 필드 복사.</done>
</task>

<task type="auto">
  <name>Task 3: LightHandler 채널명 헬퍼 추가 + ApplyShotLightsInternal 채널별 적용 전환</name>
  <files>WPF_Example/Device/LightController/LightHandler.cs, WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs</files>
  <action>
**(A) LightHandler 채널명 조회 헬퍼 (추가만, 기존 시그니처 무변경).**

기존 `SetOnOff(string, bool)` / `SetLevel(string, int)` 는 **그룹명** 오버로드이므로 시그니처가 겹친다 → 반드시 다른 이름으로 신설:

```csharp
// 채널명 → (controllerIndex, channel) 조회. RegisterLightController 의 SetChannelNames 등록명을 그대로 검색.
// 인덱스 하드코딩 시 Bar(Controller B ch1~4) 를 ch0 부터 쓰면 백라이트를 오작동시키므로 이름 기반 조회를 강제한다.
public bool TryFindChannel(string channelName, out int index, out int channel) {
    index = -1; channel = -1;
    for (int i = 0; i < Controllers.Count; i++) {
        VirtualLightController con = Controllers[i];
        for (int j = 0; j < con.ChannelCount; j++) {
            if (con[j].Name == channelName) {
                index = con.Index;
                channel = j;
                return true;
            }
        }
    }
    return false;
}

public bool SetChannelOnOff(string channelName, bool onOff) {
    int index, channel;
    if (!TryFindChannel(channelName, out index, out channel)) return false;
    SetOnOff(index, channel, onOff);
    Logging.PrintLog((int)ELogType.LightController, "{0} - Set On : {1}", channelName, onOff);
    return true;
}

public bool SetChannelLevel(string channelName, int level) {
    int index, channel;
    if (!TryFindChannel(channelName, out index, out channel)) return false;
    SetLevel(index, channel, level);
    Logging.PrintLog((int)ELogType.LightController, "{0} - Set Level : {1}", channelName, level);
    return true;
}
```
- `Controllers` / `ControllerCount` / `con[j].Name` 접근자는 LightGroup.AddChannel(46-53) 이 이미 동일하게 쓰고 있음 → 그대로 사용.
- 기존 그룹 메서드는 **한 줄도 수정하지 않는다.**

**(B) InspectionSequence.ApplyShotLightsInternal (350-401) Ring/Bar 부분 교체.**

Ring: 그룹 API 2줄 → 채널 6개 개별 분기. Bar: → 채널 4개 개별 분기.
반복 코드를 줄이려면 로컬 헬퍼 하나를 추가한다(삼항 금지, Allman 스타일):

```csharp
// 채널 하나의 ON/OFF + 밝기 적용. Enabled=true 면 ON 후 SetLevel, false 면 OFF 만 (기존 그룹 로직과 순서 동일).
private void ApplyChannelLight(string channelName, bool bEnabled, int nBrightness)
{
    if (bEnabled)
    {
        LightHandler.Handle.SetChannelOnOff(channelName, true);
        LightHandler.Handle.SetChannelLevel(channelName, nBrightness);
    }
    else
    {
        LightHandler.Handle.SetChannelOnOff(channelName, false);
    }
}
```
그리고 ApplyShotLightsInternal 안에서:
```csharp
ApplyChannelLight(LightHandler.LIGHT_RING_CH1, shot.RingLight_Enabled_1, shot.RingLight_Brightness_1);
... CH2~CH6
ApplyChannelLight(LightHandler.LIGHT_BAR_1, shot.SideLight_Enabled_1, shot.SideLight_Brightness_1);
... BAR_2~BAR_4
```
- **BACK / ALIGN_COAX / RING7 분기(362-380, 392-400)는 기존 그룹 API 코드를 그대로 둔다.**
- 구 `RingLight_Enabled/Brightness`, `SideLight_Enabled/Brightness` 는 ApplyShotLightsInternal 에서 **더 이상 소비하지 않는다** (Load override 가 채널 필드로 브로드캐스트하므로 채널 필드만 읽으면 구 레시피도 동일 동작).
- 상단 주석(346-349)의 매핑 설명을 새 구조에 맞게 갱신 (Ring→RING_CH1~6 개별, Bar→BAR_1~4 개별).
- `TurnOffShotLights`(337-344)는 **수정하지 않는다** — 그룹 OFF 가 채널 전체를 덮는다.
  </action>
  <verify>
    <automated>"C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/MSBuild.exe" WPF_Example/DatumMeasurement.csproj -p:Configuration=Debug -p:Platform=x64 -t:Build -v:minimal -nologo</automated>
  </verify>
  <done>빌드 PASS. ApplyShotLightsInternal 이 Ring 6 / Bar 4 를 채널명 기반 개별 API 로 적용. BACK/RING7/ALIGN_COAX 분기 및 TurnOffShotLights 는 git diff 상 무변경.</done>
</task>

<task type="auto">
  <name>Task 4: 마이그레이션 실증 검증 + 회귀 가드 diff 감사</name>
  <files>(코드 수정 없음 — 검증 전용. 하네스는 scratchpad 에만 생성, 레포 오염 금지)</files>
  <action>
**(A) 구 레시피 마이그레이션 실증 (필수 게이트).**

Debug|x64 빌드 산출물(`WPF_Example/bin/x64/Debug/DatumMeasurement.exe`)을 라이브러리로 참조하는 임시 콘솔 하네스를 scratchpad 에 만들어 ShotConfig.Load 를 실제로 태운다.

절차:
1. scratchpad 에 가짜 **구** 레시피 INI 작성 — 섹션 `[SHOT_0_CAM]`, 키는 구 필드만:
   `RingLight_Enabled=True`, `RingLight_Brightness=120`, `SideLight_Enabled=True`, `SideLight_Brightness=80` (채널별 키 없음).
2. scratchpad 에 하네스 .cs 작성 → `new ShotConfig(null)` → `IniFile` 로드 → `shot.Load(iniFile, "SHOT_0_CAM")` 호출 → RingLight_Brightness_1~6 == 120 / RingLight_Enabled_1~6 == true / SideLight_Brightness_1~4 == 80 / SideLight_Enabled_1~4 == true 를 검사하고 결과를 stdout 에 출력.
3. `C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe` 로 컴파일:
   `/r:DatumMeasurement.exe` + 필요한 참조(halcondotnet.dll 등)를 추가, **출력 exe 를 `WPF_Example/bin/x64/Debug/` 에서 실행**해야 의존 DLL 이 해석된다 (하네스 exe 자체는 scratchpad 에 두고 working dir 를 bin 으로 잡거나, bin 에 임시 복사 후 실행 뒤 삭제).
4. 이어서 **신규** 키가 있는 INI(채널별 값이 서로 다른 케이스, 예: CH1=10, CH2=20, ... CH6=60)를 로드해 브로드캐스트가 **덮어쓰지 않는지**(채널별 값 그대로) 확인.

하네스가 런타임 의존성(HALCON/OpenCV/카메라 SDK) 로딩 문제로 실행 불가하면:
- 실패 원인을 SUMMARY 에 명시하고,
- 대안으로 `ShotConfig.Load` / `ParamBase.Load` / `IniSection.ContainsKey` / `IniValue.ToInt()` 호출 경로를 줄 단위로 추적한 정밀 코드 검토 결과(각 단계 값 변화 표)를 SUMMARY 에 근거로 남긴다.
- 이 경우 "현장 구 레시피 첫 로드 시 조명값 육안 확인" 을 HUMAN-UAT 항목으로 명시할 것.

**(B) 회귀 가드 diff 감사.**

`git diff` 로 아래가 **무변경**인지 확인하고 SUMMARY 에 결과를 적는다:
- `Custom/Device/LightHandler.cs` 의 `RegisterLightController()` (파일 전체가 무변경이어야 함)
- `InspectionSequence.TurnOffShotLights()`
- `ApplyShotLightsInternal` 의 BACK / ALIGN_COAX / RING7 분기
- `Device/LightController/LightHandler.cs` 의 기존 public 메서드 시그니처 (추가 라인만 존재하고 기존 라인 수정 0)
- ShotConfig 구 필드 4개 삭제되지 않았는지 (`[Browsable(false)]` 추가만)

**(C) 규칙 감사.** 변경 diff 에 삼항 `?:` 0건, C# 8+ 문법 0건, 신규 `//YYMMDD hbk` 주석 0건 확인.
  </action>
  <verify>
    <automated>"C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/MSBuild.exe" WPF_Example/DatumMeasurement.csproj -p:Configuration=Debug -p:Platform=x64 -t:Build -v:minimal -nologo</automated>
  </verify>
  <done>
빌드 PASS. 구 레시피 로드 → Ring 6채널 전부 120/true, Bar 4채널 전부 80/true 브로드캐스트가 하네스 실행으로 실증됨(또는 실행 불가 사유 + 정밀 코드추적 근거가 SUMMARY 에 기록됨). 신규 키 레시피는 채널별 값이 보존됨. 회귀 가드 5항목 + 규칙 감사 3항목 전부 통과. scratchpad 외 임시 파일 잔존 0.
  </done>
</task>

</tasks>

<verification>
1. Debug|x64 MSBuild PASS (경고 신규 0 목표)
2. 구 레시피(채널 키 없음) → Ring 6 / Bar 4 브로드캐스트 실증
3. 신규 레시피(채널 키 있음) → 채널별 값 보존 (브로드캐스트가 덮어쓰지 않음)
4. git diff: RegisterLightController / TurnOffShotLights / BACK·RING7·ALIGN_COAX 분기 / LightHandler 기존 public 시그니처 무변경
5. 삼항 0 / C# 8+ 0 / 신규 `//YYMMDD hbk` 0
</verification>

<success_criteria>
- ShotConfig 에 채널별 스칼라 20개 존재, PropertyGrid Light 탭에 Ring 6줄 + Bar 4줄 자동 표시 (XAML 무수정)
- 구 레시피 로드 시 조명 소등 회귀 없음 (브로드캐스트 마이그레이션 동작 실증)
- ApplyShotLightsInternal 이 채널명 기반 개별 API 로 Ring/Bar 적용 — 인덱스 하드코딩 0
- BACK/RING7/ALIGN_COAX/TurnOffShotLights/RegisterLightController 회귀 0
- ShotConfig.CopyTo override 로 Copy/Paste 시 조명 필드 복사됨
</success_criteria>

<output>
완료 후 `.planning/quick/260713-nse-on-off-ring-6-bar-4-shotconfig-ui-applys/260713-nse-SUMMARY.md` 생성.

SUMMARY 에 반드시 포함:
- 마이그레이션 검증 방법과 실제 관측값 (하네스 출력 or 코드추적 표)
- 회귀 가드 diff 감사 결과
- HUMAN-UAT 대기 항목: (1) 현장 구 레시피 첫 로드 시 Ring/Bar 밝기값이 구 값으로 채워졌는지 PropertyGrid 육안 확인, (2) 임시 배선(링6+백라이트1) 컨트롤러에서 채널별 개별 점등/소등 물리 확인, (3) 저장 후 재로드 시 채널별 값 유지 확인
- LIGHT-CHANNEL-DESIGN.md 의 D-L01 "Ring 6분할 독립 제어 여부 — 미결" 이 이번 작업으로 해소됨을 기록
</output>
