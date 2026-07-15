---
phase: quick-260714-qbz
plan: 01
type: execute
wave: 1
depends_on: []
files_modified:
  - WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs
  - WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs
  - WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs
  - WPF_Example/UI/ContentItem/MainView.xaml.cs
  - WPF_Example/UI/ControlItem/InspectionListView.xaml.cs
autonomous: true
requirements: [DATUM-LIGHT-01]

must_haves:
  truths:
    - "DatumConfig PropertyGrid 에 Ring 6채널 + Bar 4채널 + Back + Ring7 + Coax 개별 On/밝기 입력란이 표시된다 (지금은 조명 필드 자체가 없어 입력할 곳이 없었음)"
    - "티칭 Grab 버튼을 누르면 Datum 전용 조명이 실제로 켜진 상태에서 이미지를 획득한다 (지금은 ApplyLight(param) 이 미설정 LightGroupName 을 읽어 아무 조명도 안 켬)"
    - "런타임 $TEST 검사 중 DatumPhase 단계에서도 Datum 전용 조명이 켜진 채로 datum grab 이 수행되고, 그 직후 다시 해당 Z-index Shot 조명으로 복원되어 EStep.Grab 의 측정 grab 에 영향이 없다"
    - "Shot 의 ApplyShotLightsInternal/ApplyShotLights/TurnOffShotLights 는 무변경 — 기존 $PREP 흐름 회귀 0"
    - "구 Datum 레시피(조명 키 없음)를 로드해도 크래시/예외 없이 전 채널 Off(0/false) 로 안전하게 시작한다"
  artifacts:
    - path: "WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs"
      provides: "RingLight_Enabled_1~6/Brightness_1~6, SideLight_Enabled_1~4/Brightness_1~4, BackLight_*, Ring7Light_*, CoaxLight_* — ShotConfig 와 동일 명명"
      contains: "RingLight_Brightness_1"
    - path: "WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs"
      provides: "ApplyDatumLights(DatumConfig) 공개 진입점 + ApplyDatumLightsInternal 채널별 적용"
      contains: "ApplyDatumLightsInternal"
    - path: "WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs"
      provides: "EStep.DatumPhase 루프에서 datum 조명 적용 → grab → 루프 종료 후 Shot 조명 복원"
      contains: "ApplyDatumLights"
    - path: "WPF_Example/UI/ContentItem/MainView.xaml.cs"
      provides: "GrabAndDisplay(ICameraParam, DatumConfig) 오버로드 — Datum 티칭 grab 시 datum 전용 조명 적용"
      contains: "GrabAndDisplay(ICameraParam param, DatumConfig datum"
  key_links:
    - from: "WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs (EStep.DatumPhase, ~90줄)"
      to: "InspectionSequence.ApplyDatumLights(datum)"
      via: "parentSeq.ApplyDatumLights(datum) 호출 → GrabOrLoadDatumImage 직전"
      pattern: "ApplyDatumLights\\(datum\\)"
    - from: "WPF_Example/UI/ControlItem/InspectionListView.xaml.cs (button_grab_Click)"
      to: "MainView.GrabAndDisplay(ICameraParam, DatumConfig)"
      via: "datumForGrab 을 새 오버로드로 전달 (기존 1-인자 오버로드는 Shot 등 비-Datum 노드용으로 유지)"
      pattern: "GrabAndDisplay\\(resolved, datumForGrab\\)"
---

<objective>
Datum 노드에 전용 per-channel 조명 필드(Ring 6 + Bar 4 + Back + Ring7 + Coax)를 추가하고, 티칭 Grab과 런타임 DatumPhase 검출 양쪽에서 실제로 적용되도록 배선한다.

Purpose: 지금 DatumConfig 는 조명 필드가 아예 없어 SourceShotName 으로 지정한 Shot 의 조명을 암묵적으로 상속하는데, 사용자가 "조명값 넣는 곳이 없다"고 지적함. 게다가 조사 결과 티칭 Grab 경로(`MainView.GrabAndDisplay` → `LightHandler.ApplyLight(param)`)는 애초에 미설정 `LightGroupName`/`LightLevel` 을 읽어 **아무 조명도 켜지 않는 상태**였음 — Datum 뿐 아니라 Shot 조차 티칭 시점엔 조명이 안 켜졌던 기존 결함. 이번 작업으로 Datum 전용 조명 필드 + teaching/runtime 양쪽 적용을 함께 배선해 이 갭을 해소한다.

Output: DatumConfig 조명 필드 20개(ShotConfig 와 동일 명명) + InspectionSequence.ApplyDatumLights + Action_FAIMeasurement DatumPhase 훅 + MainView.GrabAndDisplay 오버로드 + InspectionListView 호출부 갱신.

**리스크 경계**: 이번 작업은 Datum grab 경로에만 조명을 배선한다. 일반 Shot 의 teaching grab 이 여전히 조명 없이 동작하는 기존 결함은 **범위 밖**(별도 후속 작업) — Shot 은 이미 검증된 `$PREP`→`ApplyShotLightsInternal` 런타임 경로가 있어 급하지 않음.
</objective>

<execution_context>
GSD 스킬이 이 세션에 로드되어 있지 않아 `.planning/quick/` 컨벤션을 수동으로 따라 작성/실행한다. 완료 후 SUMMARY.md 를 이 디렉터리에 함께 남긴다.
</execution_context>

<context>
@CLAUDE.md

@WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs
@WPF_Example/Custom/Sequence/Inspection/ShotConfig.cs
@WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs
@WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs
@WPF_Example/Device/LightController/LightHandler.cs
@WPF_Example/UI/ContentItem/MainView.xaml.cs
@WPF_Example/UI/ControlItem/InspectionListView.xaml.cs

<interfaces>
<!-- 사전 조사 완료 (병렬 조사 에이전트 2회 + 직접 코드 검증) — 재탐색 불필요 -->

**A. 런타임 datum 검출 경로 (Action_FAIMeasurement.cs)**
- `EStep.DatumPhase`(~81줄) 가 `foreach (var datum in parentSeq.DatumConfigs)` 로 순회, `parentSeq.TryRunSingleDatum(...)` 호출.
- `GrabOrLoadDatumImage(datum)`(~138/316줄) — 실 HW 는 `GrabHalconImage(ShotParam)` 로 **자체 fresh grab** (Shot 이미 grab 한 걸 재사용 안 함).
- 조명은 오직 `$PREP` → `SystemHandler.ProcessPrep` → `ApplyPrepToSequences` → `InspectionSequence.ApplyShotLights(nZIndex)` 로만 세팅됨. Datum 전용 `$PREP` 는 없음 — 즉 지금은 datum 검출이 "마지막 $PREP z_index 가 선택한 Shot 조명" 아래서 이뤄짐.
- **훅 지점**: `DatumPhase` 루프(90줄 부근, null 체크 직후) 에서 이미 `DatumConfig datum` 을 들고 있음 → 여기서 `parentSeq.ApplyDatumLights(datum)` 호출 → `GrabOrLoadDatumImage` 직전. 루프 종료 후, `Step = EStep.Grab` 전(182줄 부근)에 `parentSeq.ApplyShotLights(m_nCurrentZIndex)` 재호출로 Shot 조명 복원 — `EStep.Grab` 의 `GrabHalconImage(ShotParam)`(208줄) 이 정상적으로 Shot 조명을 봐야 하므로 필수.
- `$PREP`/z_index 프로토콜 변경 불필요 — 순수 내부 훅으로 충분.

**B. InspectionSequence.cs 기존 조명 적용 메서드 전문 (그대로 미러링할 패턴)**
```csharp
public bool ApplyShotLights(int nZIndex) {
    ShotConfig shot = FindShotByZIndex(nZIndex);
    if (shot == null) { Logging.PrintLog(...); return false; }
    ApplyShotLightsInternal(shot);
    return true;
}

private void ApplyShotLightsInternal(ShotConfig shot) {
    ApplyChannelLight(LightHandler.LIGHT_RING_CH1, shot.RingLight_Enabled_1, shot.RingLight_Brightness_1);
    ... CH2~CH6 동일 ...
    if (shot.BackLight_Enabled) { LightHandler.Handle.SetOnOff(LightHandler.LIGHT_BACK, true); LightHandler.Handle.SetLevel(LightHandler.LIGHT_BACK, shot.BackLight_Brightness); }
    else { LightHandler.Handle.SetOnOff(LightHandler.LIGHT_BACK, false); }
    if (shot.CoaxLight_Enabled) { ... LIGHT_ALIGN_COAX ... } else { ... false ... }
    ApplyChannelLight(LightHandler.LIGHT_BAR_1, shot.SideLight_Enabled_1, shot.SideLight_Brightness_1);
    ... BAR_2~BAR_4 동일 ...
    if (shot.Ring7Light_Enabled) { ... LIGHT_RING7 ... } else { ... false ... }
}

private void ApplyChannelLight(string channelName, bool bEnabled, int nBrightness) {
    if (bEnabled) { LightHandler.Handle.SetChannelOnOff(channelName, true); LightHandler.Handle.SetChannelLevel(channelName, nBrightness); }
    else { LightHandler.Handle.SetChannelOnOff(channelName, false); }
}
```
`ApplyChannelLight` 는 **재사용 가능**(private 이지만 같은 클래스 내 신규 메서드에서 그대로 호출 가능) — 중복 작성 불필요.

**C. Teaching grab 경로 (MainView.xaml.cs / InspectionListView.xaml.cs)**
- `GrabAndDisplay` 는 현재 **1개 오버로드만 존재**: `public async void GrabAndDisplay(ICameraParam param, bool eventCall = false)`(676줄). 조명은 681줄 `pLight.ApplyLight(param)` — DatumConfig 를 못 받으므로 datum 인지 불가능.
- 대조: `LoadAndDisplay` 는 이미 2-인자 오버로드가 있어 `pathSinkParam as DatumConfig` 로 datum 을 복구함(750줄 부근) — 이 패턴을 GrabAndDisplay 에도 이식.
- 호출부: `InspectionListView.xaml.cs` `button_grab_Click`(847~854줄) — `if (SelectedParam is DatumConfig datumForGrab) { ICameraParam resolved = ResolveDatumCameraParam(datumForGrab); ... mParentWindow.mainView.GrabAndDisplay(resolved); return; }` — 지금은 datumForGrab 을 버림.
- `LightHandler.ApplyLight(ICameraParam, bool)` 오버로드는 그대로 유지(다른 호출부 있음) — 새 조명 적용은 `InspectionSequence.ApplyDatumLights` 재사용(별도 LightHandler 메서드 신설 불필요, InspectionSequence 인스턴스 접근 필요 → `SystemHandler.Handle.Sequences[ESequence.Top] as InspectionSequence` 류 접근 또는 아래 D 참고).

**D. ApplyDatumLights 를 MainView(UI 레이어)에서 호출하는 방법**
`InspectionSequence` 는 `ESequence` 별 인스턴스(Top/Side/Bottom)라 "어느 시퀀스의" ApplyDatumLights 를 쓸지 정해야 한다. Datum 은 `SourceShotName` → `ShotConfig.OwnerSequenceName` 으로 소속 시퀀스가 정해지므로(`ResolveDatumModelPath` 선례, InspectionSequence.cs:808~850), 티칭 grab 시에도 동일 방식으로 시퀀스를 역추적해 `ApplyDatumLights` 를 호출한다. 새 `ApplyDatumLights` 는 `InspectionSequence`(인스턴스 메서드)에 두되, MainView 에서는 `SystemHandler.Handle.Sequences[ESequence.Top]`(또는 역추적한 seq) `as InspectionSequence` 로 얻어 호출.
간단화: Datum 조명은 물리 조명 그룹/채널 자체이지 시퀀스별로 다른 하드웨어가 아니므로(LightHandler 는 전역 싱글턴), 시퀀스 인스턴스 아무거나 잡아도 결과는 동일하다 — 다만 `ApplyDatumLightsInternal` 을 `InspectionSequence`(non-static) 에 두는 기존 패턴을 존중해 `SystemHandler.Handle.Sequences[ESequence.Top] as InspectionSequence` 로 접근한다(항상 등록되어 있음, TopBottom/Side 역할 무관 — DeviceHandler.RegisterRequiredDevices 와 무관한 순수 LightHandler 위임이므로 seq 선택은 무관).

**E. DatumConfig PropertyGrid 필터 (IsHiddenForAlgorithm, ~824~856줄)**
새 조명 필드명(`RingLight_*`/`SideLight_*`/`BackLight_*`/`Ring7Light_*`/`CoaxLight_*`)은 `Line1_`/`Line2_`/`Circle_`/`CircleROI_`/`CircleCenter_`/`CircleDetected_`/`Vertical_`/`Horizontal_A_`/`Horizontal_B_` 접두사와 전혀 겹치지 않으므로 `IsHiddenForAlgorithm` 이 무조건 `false` 반환 → **모든 AlgorithmType 에서 노출**. `sourceNames` 화이트리스트(ItemsSource 리스트 프로퍼티용) 수정 불필요.

**F. Slidable 슬라이더 버그 재발 방지**
오늘 이미 고친 ShotConfig 슬라이더 버그(RaisePropertyChanged 누락)와 동일 함정. `RingLight_Brightness_1~6`/`SideLight_Brightness_1~4` 는 **처음부터** backing-field + `RaisePropertyChanged(nameof(...))` 패턴으로 작성한다(ShotConfig.cs 의 기존 수정 코드를 그대로 참고/복사). `Enabled_*`(bool 체크박스) 는 단일 컨트롤이라 INPC 불필요(ShotConfig 선례와 동일).

**G. INI 직렬화**
`DatumConfig : ParamBase`, Save/Load override 없음(확인됨) → 신규 public int/bool 오토프로퍼티는 `ParamBase` 리플렉션(Int32/Boolean case)으로 자동 직렬화. 구 Datum 레시피엔 이 키가 없으므로 로드 시 0/false 기본값 — **의도된 안전 기본값**(전소등)이라 ShotConfig 같은 브로드캐스트 마이그레이션 불필요(마이그레이션 소스가 되는 구 통합 필드 자체가 없음 — 신규 기능이므로).
</interfaces>
</context>

<constraints>
**프로젝트 규칙 (위반 시 리젝트):**
- 삼항 연산자 `?:` 금지 → if-else 만 사용
- C# 7.2 문법만
- 파일별 기존 브레이스 스타일 유지: DatumConfig.cs/ShotConfig.cs = 메서드 K&R, InspectionSequence.cs = Allman, Action_FAIMeasurement.cs = 기존 스타일 확인 후 매칭
- `//YYMMDD hbk` 형식 신규 주석 금지(2026-06-11 폐기) — 새 주석은 비자명한 "왜"만 간결히
- 신규 .cs 파일 없음 → csproj 수정 불필요

**변경 금지 (회귀 가드):**
- `ApplyShotLightsInternal` / `ApplyShotLights` / `TurnOffShotLights` / `ApplyChannelLight` — 시그니처·본문 무변경(호출만 추가)
- `LightHandler.cs` — 채널 헬퍼(`SetChannelOnOff`/`SetChannelLevel`)는 이미 존재, 신규 LightHandler 수정 불필요
- `Action_FAIMeasurement.cs` 의 `EStep.Grab`/`EStep.Measure`/`EStep.End` 분기 로직 — DatumPhase 앞뒤로만 훅 삽입, 다른 스텝 무변경
- 기존 `GrabAndDisplay(ICameraParam, bool)` 1-인자 오버로드 — 무변경(Shot 등 비-Datum 호출부가 계속 사용)
- ShotConfig.cs — 이번 작업 대상 아님(오늘 이미 별도로 슬라이더 버그 수정 완료)
</constraints>

<tasks>

<task type="auto">
  <name>Task 1: DatumConfig 조명 필드 20개 추가</name>
  <files>WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs</files>
  <action>
DatumConfig.cs 끝부분(`IsConfigured` 필드 근처, `Datum|Status` 카테고리 앞)에 `Datum|Light` 카테고리 그룹 신설.

ShotConfig.cs 의 이미 수정된 패턴을 그대로 복제(Enabled=plain auto-property, Brightness=backing-field+RaisePropertyChanged):

```csharp
[Category("Datum|Light Ring")]
public bool RingLight_Enabled_1 { get; set; }
private int _ringLightBrightness1;
[Slidable(0, 255)]
public int RingLight_Brightness_1 {
    get { return _ringLightBrightness1; }
    set {
        if (_ringLightBrightness1 == value) return;
        _ringLightBrightness1 = value;
        RaisePropertyChanged(nameof(RingLight_Brightness_1));
    }
}
... (CH2~CH6 동일 패턴, private 필드명 _ringLightBrightness2~6)

[Category("Datum|Light Bar")]
public bool SideLight_Enabled_1 { get; set; }
private int _sideLightBrightness1;
[Slidable(0, 255)]
public int SideLight_Brightness_1 { ... 동일 패턴 ... }
... (Bar 2~4 동일)

[Category("Datum|Light Back")]
public bool BackLight_Enabled { get; set; }
public int BackLight_Brightness { get; set; }

[Category("Datum|Light Ring7")]
public bool Ring7Light_Enabled { get; set; }
public int Ring7Light_Brightness { get; set; }

[Category("Datum|Light Coax")]
public bool CoaxLight_Enabled { get; set; }
public int CoaxLight_Brightness { get; set; }
```

주의:
- `[Slidable(0, 255)]` 는 이미 `using PropertyTools.DataAnnotations;`(4줄, 이미 있음)에 포함 — 추가 using 불필요.
- ShotConfig 와 달리 **구 통합 필드/마이그레이션 불필요**(Datum 은 처음부터 채널별) — Load override 추가하지 않는다.
- `IsHiddenForAlgorithm`(824~856줄)은 새 필드명이 어떤 hide 접두사와도 안 겹치므로 **수정 불필요** — 그대로 둔다(모든 AlgorithmType 에서 자동 노출됨, Interface E 참고).
- Category 상속 규칙(연속 프로퍼티가 이전 Category 상속) 주의해 그룹 첫 프로퍼티에만 `[Category]` 명시.
  </action>
  <verify>
    <automated>"C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" WPF_Example/DatumMeasurement.csproj -p:Configuration=Debug -p:Platform=x64 -t:Build -v:minimal -nologo</automated>
  </verify>
  <done>빌드 PASS. DatumConfig 에 RingLight_Enabled_1~6/Brightness_1~6, SideLight_Enabled_1~4/Brightness_1~4, BackLight_*, Ring7Light_*, CoaxLight_* (총 20개) 존재. Brightness 10개(Ring6+Bar4)는 backing-field+RaisePropertyChanged 패턴.</done>
</task>

<task type="auto">
  <name>Task 2: InspectionSequence.ApplyDatumLights 신설</name>
  <files>WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs</files>
  <action>
`ApplyShotLightsInternal`/`ApplyChannelLight` 바로 아래(또는 `ApplyShotLights` 근처)에 병렬 메서드 추가:

```csharp
public void ApplyDatumLights(DatumConfig datum) {
    if (datum == null) return;
    ApplyDatumLightsInternal(datum);
}

private void ApplyDatumLightsInternal(DatumConfig datum) {
    ApplyChannelLight(LightHandler.LIGHT_RING_CH1, datum.RingLight_Enabled_1, datum.RingLight_Brightness_1);
    ApplyChannelLight(LightHandler.LIGHT_RING_CH2, datum.RingLight_Enabled_2, datum.RingLight_Brightness_2);
    ApplyChannelLight(LightHandler.LIGHT_RING_CH3, datum.RingLight_Enabled_3, datum.RingLight_Brightness_3);
    ApplyChannelLight(LightHandler.LIGHT_RING_CH4, datum.RingLight_Enabled_4, datum.RingLight_Brightness_4);
    ApplyChannelLight(LightHandler.LIGHT_RING_CH5, datum.RingLight_Enabled_5, datum.RingLight_Brightness_5);
    ApplyChannelLight(LightHandler.LIGHT_RING_CH6, datum.RingLight_Enabled_6, datum.RingLight_Brightness_6);

    if (datum.BackLight_Enabled) {
        LightHandler.Handle.SetOnOff(LightHandler.LIGHT_BACK, true);
        LightHandler.Handle.SetLevel(LightHandler.LIGHT_BACK, datum.BackLight_Brightness);
    }
    else {
        LightHandler.Handle.SetOnOff(LightHandler.LIGHT_BACK, false);
    }

    if (datum.CoaxLight_Enabled) {
        LightHandler.Handle.SetOnOff(LightHandler.LIGHT_ALIGN_COAX, true);
        LightHandler.Handle.SetLevel(LightHandler.LIGHT_ALIGN_COAX, datum.CoaxLight_Brightness);
    }
    else {
        LightHandler.Handle.SetOnOff(LightHandler.LIGHT_ALIGN_COAX, false);
    }

    ApplyChannelLight(LightHandler.LIGHT_BAR_1, datum.SideLight_Enabled_1, datum.SideLight_Brightness_1);
    ApplyChannelLight(LightHandler.LIGHT_BAR_2, datum.SideLight_Enabled_2, datum.SideLight_Brightness_2);
    ApplyChannelLight(LightHandler.LIGHT_BAR_3, datum.SideLight_Enabled_3, datum.SideLight_Brightness_3);
    ApplyChannelLight(LightHandler.LIGHT_BAR_4, datum.SideLight_Enabled_4, datum.SideLight_Brightness_4);

    if (datum.Ring7Light_Enabled) {
        LightHandler.Handle.SetOnOff(LightHandler.LIGHT_RING7, true);
        LightHandler.Handle.SetLevel(LightHandler.LIGHT_RING7, datum.Ring7Light_Brightness);
    }
    else {
        LightHandler.Handle.SetOnOff(LightHandler.LIGHT_RING7, false);
    }
}
```
- `ApplyChannelLight` 는 이미 private 로 같은 클래스에 존재 — 재사용(재작성 금지).
- `ApplyShotLightsInternal`/`ApplyShotLights`/`TurnOffShotLights` 는 **한 글자도 수정하지 않는다**.
  </action>
  <verify>
    <automated>"C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" WPF_Example/DatumMeasurement.csproj -p:Configuration=Debug -p:Platform=x64 -t:Build -v:minimal -nologo</automated>
  </verify>
  <done>빌드 PASS. InspectionSequence 에 public ApplyDatumLights(DatumConfig) + private ApplyDatumLightsInternal 존재. 기존 Shot 조명 메서드 3종 git diff 무변경.</done>
</task>

<task type="auto">
  <name>Task 3: Action_FAIMeasurement DatumPhase 훅 배선 (런타임)</name>
  <files>WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs</files>
  <action>
`EStep.DatumPhase`(~81줄) 의 `foreach (var datum in parentSeq.DatumConfigs)` 루프(~89~90줄, null 체크 직후)에 삽입:

```csharp
parentSeq.ApplyDatumLights(datum);
```
`GrabOrLoadDatumImage(datum)` 호출 **직전**에 위치시킨다.

루프가 끝난 뒤, `Step = EStep.Grab` 으로 넘어가기 전(~182줄 부근)에 Shot 조명을 복원:

```csharp
parentSeq.ApplyShotLights(m_nCurrentZIndex);
```
(변수명 `m_nCurrentZIndex` 는 실제 필드명을 파일에서 확인 후 정확히 사용 — 다를 경우 EStep.Grab 이 사용하는 것과 동일한 z_index 변수를 재사용할 것.)

SIMUL_MODE 분기 유무 확인: `GrabOrLoadDatumImage` 가 SIMUL 에서는 파일 로드 경로(TeachingImagePath)를 타므로 조명 호출 자체는 무해하게 실행되지만 이미지에 영향 없음 — 분기 불필요, 항상 호출.
  </action>
  <verify>
    <automated>"C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" WPF_Example/DatumMeasurement.csproj -p:Configuration=Debug -p:Platform=x64 -t:Build -v:minimal -nologo</automated>
  </verify>
  <done>빌드 PASS. DatumPhase 루프에서 datum 별 ApplyDatumLights 호출 후 grab, 루프 종료 후 ApplyShotLights 로 Shot 조명 복원. EStep.Grab/Measure/End 로직 git diff 무변경.</done>
</task>

<task type="auto">
  <name>Task 4: MainView.GrabAndDisplay 오버로드 + InspectionListView 호출부 갱신 (티칭)</name>
  <files>WPF_Example/UI/ContentItem/MainView.xaml.cs, WPF_Example/UI/ControlItem/InspectionListView.xaml.cs</files>
  <action>
**(A) MainView.xaml.cs** — `GrabAndDisplay(ICameraParam param, bool eventCall = false)`(676줄) 바로 위/아래에 새 오버로드 추가:

```csharp
public async void GrabAndDisplay(ICameraParam param, DatumConfig datum, bool eventCall = false) {
    if (param == null || !pSeq.IsIdle || GrabTask != null) return;

    GrabTask = Task.Run(() => {
        lock (mDrawInterlock) {
            if (datum != null) {
                InspectionSequence seq = SystemHandler.Handle.Sequences[ESequence.Top] as InspectionSequence;
                if (seq != null) seq.ApplyDatumLights(datum);
            }
            else {
                pLight.ApplyLight(param);
            }
            HImage grabbedHalconImage = pDev.GrabHalconImage(param);
            param.PutImage(grabbedHalconImage);
            ... (기존 676줄 본문의 나머지를 그대로 이식 — GrabTask 마무리/이벤트/디스플레이 로직 등, 새로 만들지 말고 원본 복사)
        }
    });
    ...
}
```
- 실제로는 **기존 `GrabAndDisplay(ICameraParam, bool)` 본문을 그대로 복사**하고 조명 적용 줄(681줄 `pLight.ApplyLight(param);`)만 위 if/else 로 교체하는 방식으로 작성한다(중복 로직을 새로 설계하지 말 것 — 원본 그대로 두고 조명 한 줄만 분기).
- 기존 1-인자 오버로드는 내부에서 `datum=null` 로 새 오버로드를 호출하도록 리팩터링해도 되고, 완전히 독립 유지해도 된다 — 더 단순하고 회귀 위험 적은 쪽을 택할 것(권장: 기존 오버로드 본문 무변경 유지 + 새 오버로드만 추가, 코드 일부 중복은 감수).
- `ESequence`/`InspectionSequence` using 이미 파일에 있는지 확인(대부분 이미 참조 중).

**(B) InspectionListView.xaml.cs** — `button_grab_Click`(847~854줄):
```csharp
if (SelectedParam is DatumConfig datumForGrab) {
    ICameraParam resolved = ResolveDatumCameraParam(datumForGrab);
    if (resolved == null) return;
    if (SystemHandler.Handle.Sequences.IsIdle == false) return;
    mParentWindow.mainView.GrabAndDisplay(resolved, datumForGrab);   // ← datumForGrab 추가 전달
    return;
}
```
  </action>
  <verify>
    <automated>"C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" WPF_Example/DatumMeasurement.csproj -p:Configuration=Debug -p:Platform=x64 -t:Build -v:minimal -nologo</automated>
  </verify>
  <done>빌드 PASS. GrabAndDisplay(ICameraParam, DatumConfig, bool) 오버로드 존재, Datum 이면 ApplyDatumLights, 아니면 기존 ApplyLight(param) 그대로. button_grab_Click 이 datumForGrab 을 전달. 기존 1-인자 오버로드/다른 호출부 무변경.</done>
</task>

<task type="auto">
  <name>Task 5: 회귀 가드 diff 감사 + 규칙 감사</name>
  <files>(코드 수정 없음 — 검증 전용)</files>
  <action>
`git diff` 로 아래가 **무변경**인지 확인:
- `ApplyShotLightsInternal` / `ApplyShotLights` / `TurnOffShotLights` / `ApplyChannelLight` 본문
- `LightHandler.cs` 전체
- `Action_FAIMeasurement.cs` 의 `EStep.Grab`/`EStep.Measure`/`EStep.End` 분기
- 기존 `GrabAndDisplay(ICameraParam, bool)` 1-인자 오버로드 본문(새 오버로드 추가는 허용, 기존 오버로드 수정은 회귀 가드 대상)

규칙 감사: 변경 diff 에 삼항 `?:` 0건, C# 8+ 문법 0건, 신규 `//YYMMDD hbk` 주석 0건.
  </action>
  <verify>
    <automated>"C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" WPF_Example/DatumMeasurement.csproj -p:Configuration=Debug -p:Platform=x64 -t:Rebuild -v:minimal -nologo</automated>
  </verify>
  <done>Rebuild PASS(0 errors, 신규 warning 0). 회귀 가드 4항목 + 규칙 감사 3항목 전부 통과.</done>
</task>

</tasks>

<verification>
1. Debug|x64 MSBuild PASS (신규 경고 0)
2. DatumConfig PropertyGrid 에 Light 카테고리(Ring/Bar/Back/Ring7/Coax) 20개 필드 표시 확인 — 슬라이더 드래그 시 옆 숫자칸도 갱신되는지(오늘 고친 패턴 재사용 확인)
3. git diff: ApplyShotLightsInternal/ApplyShotLights/TurnOffShotLights/LightHandler.cs/EStep.Grab·Measure·End/기존 GrabAndDisplay 1-인자 오버로드 — 전부 무변경
4. 삼항 0 / C# 8+ 0 / 신규 `//YYMMDD hbk` 0
</verification>

<success_criteria>
- DatumConfig 에 조명 입력 필드 20개 존재, PropertyGrid 에 자동 노출(모든 AlgorithmType)
- 티칭 Grab 시 Datum 이면 ApplyDatumLights, 아니면 기존 ApplyLight(param) 그대로 — 분기 정확
- 런타임 DatumPhase 에서 datum 조명 적용 후 grab, 종료 후 Shot 조명 복원 — EStep.Grab 회귀 없음
- 기존 Shot 조명 경로($PREP/ApplyShotLightsInternal) 회귀 0
</success_criteria>

<output>
완료 후 `.planning/quick/260714-qbz-datum-per-channel-light/260714-qbz-SUMMARY.md` 생성.

SUMMARY 에 반드시 포함:
- 각 Task 별 실제 반영 내용(줄 번호 포함)
- 회귀 가드 diff 감사 결과
- HUMAN-UAT 대기 항목: (1) DatumConfig Light 필드 PropertyGrid 육안 확인 + 슬라이더 갱신 확인, (2) 실 하드웨어에서 Datum 티칭 Grab 시 지정 채널이 실제로 점등되는지 확인, (3) 런타임 $TEST 사이클에서 DatumPhase 조명 적용 후 EStep.Grab 조명이 정상적으로 Shot 조명으로 복원되는지 확인(측정 이미지가 Datum 조명 아래서 찍히지 않는지)
- Datum 이 SourceShotName 으로 상속하던 기존 암묵적 조명 상속 동작과의 관계 — 이번 작업 이후에도 Datum 조명 필드를 비워두면(전부 0/false) 어떻게 되는지 명시(전소등, Shot 상속 안 함 — 사용자에게 안내 필요할 수 있음)
</output>
