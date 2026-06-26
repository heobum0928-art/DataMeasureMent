# Phase 66: 조명 정합 — 검사 Ring7/Coax + Align 동축 - Pattern Map

**Mapped:** 2026-06-26
**Files analyzed:** 7개 (수정 대상)
**Analogs found:** 7 / 7

---

## File Classification

| 수정 파일 | Role | Data Flow | 가장 가까운 Analog | Match 품질 |
|-----------|------|-----------|-------------------|-----------|
| `WPF_Example/Custom/Sequence/Inspection/ShotConfig.cs` | model | CRUD | 자기 자신 (기존 Light 필드 복제) | exact |
| `WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs` | service | request-response | 자기 자신 (ApplyShotLightsInternal 블록 복제) | exact |
| `WPF_Example/Custom/EthernetVision/AlignRefPose.cs` | model | CRUD | 자기 자신 (Phase 61.1 Roi1Len1 추가 선례) | exact |
| `WPF_Example/Custom/EthernetVision/AlignShapeMatchService.cs` | service | CRUD | 자기 자신 (TrySaveRefPose/LoadRefPose 패턴) | exact |
| `WPF_Example/Custom/UI/BottomVisionView.xaml(.cs)` | component | request-response | `WPF_Example/Custom/UI/TrayVisionView.xaml(.cs)` | exact |
| `WPF_Example/Custom/UI/TrayVisionView.xaml(.cs)` | component | request-response | `WPF_Example/Custom/UI/BottomVisionView.xaml(.cs)` | exact |
| `WPF_Example/Custom/SystemHandler.cs` §RunBottomAlign | service | request-response | 자기 자신 (RunBottomAlign 내부 grab 직전 삽입) | exact |

---

## Pattern Assignments

### 1. `ShotConfig.cs` — Ring7 필드 추가 + CoaxLight 숨김 (D-01, D-03)

**Analog:** `WPF_Example/Custom/Sequence/Inspection/ShotConfig.cs` :60-75 (기존 조명 4종 필드)

**Role:** model / CRUD

**기존 조명 필드 패턴** (lines 60-75):
```csharp
// Multi-Light — Ring/Back/Coax/Side 조명 필드 8개
[Category("Light|Ring")]
public bool RingLight_Enabled { get; set; }
public int RingLight_Brightness { get; set; }

[Category("Light|Back")]
public bool BackLight_Enabled { get; set; }
public int BackLight_Brightness { get; set; }

[Category("Light|Coax")]
public bool CoaxLight_Enabled { get; set; }
public int CoaxLight_Brightness { get; set; }

[Category("Light|Side")]
public bool SideLight_Enabled { get; set; }
public int SideLight_Brightness { get; set; }
```

**`[Browsable(false)]` 기존 사용 패턴** (lines 30-31, 85-86):
```csharp
[Browsable(false)]
public List<FAIConfig> FAIList { get; private set; } = new List<FAIConfig>();
...
[Browsable(false)]
public bool HasImage { ... }
```

**수정 목표:**

D-01 — Ring7 필드 추가: `CoaxLight_*` 블록 바로 뒤(현재 line 72 다음)에 삽입:
```csharp
[Category("Light|Ring7")]
public bool Ring7Light_Enabled { get; set; }
public int Ring7Light_Brightness { get; set; }
```

D-03 — CoaxLight 숨김: `[Category("Light|Coax")]` 위에 `[Browsable(false)]` 추가:
```csharp
[Browsable(false)]
[Category("Light|Coax")]
public bool CoaxLight_Enabled { get; set; }
public int CoaxLight_Brightness { get; set; }
```

> **주의:** 현재 `[Browsable(false)]`와 `[Category]`가 같은 속성에 공존 가능한지 확인 필요.
> PropertyTools PropertyGrid는 `[Browsable(false)]`를 우선 적용하여 Category와 무관하게 숨긴다.
> INI 직렬화(`ParamBase.Save/Load`)는 `[Browsable]` 무관 — 직렬화는 계속 동작하므로 하위호환 유지.

---

### 2. `InspectionSequence.cs` §ApplyShotLightsInternal — Ring7 매핑 추가 (D-02)

**Analog:** `WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs` :339-380

**Role:** service / request-response

**Line drift 확인:** CONTEXT.md가 "Ring7 추가 지점 ~:336/361" 언급. 실제 `ApplyShotLightsInternal`는 line 339~380. Ring7 삽입 지점은 line 369 (SideLight 블록) 앞, 즉 line 370 앞 위치. **CONTEXT.md 라인 번호와 3~5줄 차이 — 실제 삽입 위치 = :369 직전**.

**기존 Enabled→SetOnOff+SetLevel 패턴** (lines 341-380):
```csharp
if (shot.RingLight_Enabled)
{
    LightHandler.Handle.SetOnOff(LightHandler.LIGHT_RING, true);
    LightHandler.Handle.SetLevel(LightHandler.LIGHT_RING, shot.RingLight_Brightness);
}
else
{
    LightHandler.Handle.SetOnOff(LightHandler.LIGHT_RING, false);
}

if (shot.BackLight_Enabled)
{
    LightHandler.Handle.SetOnOff(LightHandler.LIGHT_BACK, true);
    LightHandler.Handle.SetLevel(LightHandler.LIGHT_BACK, shot.BackLight_Brightness);
}
else
{
    LightHandler.Handle.SetOnOff(LightHandler.LIGHT_BACK, false);
}

if (shot.CoaxLight_Enabled)
{
    LightHandler.Handle.SetOnOff(LightHandler.LIGHT_ALIGN_COAX, true);
    LightHandler.Handle.SetLevel(LightHandler.LIGHT_ALIGN_COAX, shot.CoaxLight_Brightness);
}
else
{
    LightHandler.Handle.SetOnOff(LightHandler.LIGHT_ALIGN_COAX, false);
}

if (shot.SideLight_Enabled)
{
    LightHandler.Handle.SetOnOff(LightHandler.LIGHT_BAR, true);
    LightHandler.Handle.SetLevel(LightHandler.LIGHT_BAR, shot.SideLight_Brightness);
}
else
{
    LightHandler.Handle.SetOnOff(LightHandler.LIGHT_BAR, false);
}
```

**수정 목표 — line 379 (SideLight 블록 닫는 `}`) 뒤에 Ring7 블록 추가:**
```csharp
if (shot.Ring7Light_Enabled)
{
    LightHandler.Handle.SetOnOff(LightHandler.LIGHT_RING7, true);
    LightHandler.Handle.SetLevel(LightHandler.LIGHT_RING7, shot.Ring7Light_Brightness);
}
else
{
    LightHandler.Handle.SetOnOff(LightHandler.LIGHT_RING7, false);
}
```

**주석 라인 업데이트:** line 336 주석도 "Ring7Light → RING7" 추가:
```csharp
//  RingLight → RING / BackLight → BACK / CoaxLight → ALIGN_COAX / SideLight → BAR / Ring7Light → RING7
```

---

### 3. `LightHandler.cs` — 그룹 상수 참조 (변경 없음, 확인용)

**Role:** config / CRUD
**변경:** 없음. 아래 상수가 이미 등록되어 있음을 확인.

**그룹 상수 및 등록** (`WPF_Example/Custom/Device/LightHandler.cs` :20-72):
```csharp
public const string LIGHT_ALIGN_COAX = "ALIGN_COAX";   // line 20
public const string LIGHT_RING7      = "RING7";         // line 28
public const string LIGHT_RING       = "RING";          // line 31
public const string LIGHT_BAR        = "BAR";           // line 32

// LightGroup 등록 (lines 68-72):
Groups.Add(new LightGroup(LIGHT_RING7).AddChannel(LIGHT_RING7));           // line 69
Groups.Add(new LightGroup(LIGHT_ALIGN_COAX).AddChannel(LIGHT_ALIGN_COAX)); // line 72
```

**SetOnOff/SetLevel 시그니처** (`WPF_Example/Device/LightController/LightHandler.cs` :162-198):
```csharp
public bool GetOnOff(string groupName)  // line 162
public bool SetOnOff(string groupName, bool onOff)  // line 175
public bool SetLevel(string groupName, int level)   // line 188
```

---

### 4. `AlignRefPose.cs` — Coax 필드 추가 (D-05)

**Analog:** `WPF_Example/Custom/EthernetVision/AlignRefPose.cs` :33-41 (Phase 61.1 Roi1Len1 추가 선례)

**Role:** model / CRUD

**기존 Phase 61.1 필드 추가 선례** (lines 33-41):
```csharp
//260625 hbk Phase 61.1 F2 — 티칭 ROI 크기(반폭...). 구 레시피엔 0 → 60px 폴백.
/// <summary>TL(ROI1) Col 반폭.</summary>
public double Roi1Len1 { get; set; }
/// <summary>TL(ROI1) Row 반폭.</summary>
public double Roi1Len2 { get; set; }
/// <summary>BR(ROI2) Col 반폭.</summary>
public double Roi2Len1 { get; set; }
/// <summary>BR(ROI2) Row 반폭.</summary>
public double Roi2Len2 { get; set; }
```

**JSON 직렬화 특성:** POCO 클래스 — Newtonsoft.Json이 필드 부재 시 기본값(false/0)으로 역직렬화.
키 부재 = C# 기본값(bool=false, int=0) → 하위호환 자동 충족 (D-05 Discretion 조건).

**수정 목표 — line 41 뒤에 추가:**
```csharp
//260626 hbk Phase 66 — Align 동축 조명 저장. 키 부재(구 JSON) → false/0 (하위호환, D-05).
/// <summary>Align 동축 조명 ON/OFF. 저장 시점 슬롯/Tray 설정값.</summary>
public bool CoaxEnabled { get; set; }
/// <summary>Align 동축 밝기 0~255. 저장 시점 설정값.</summary>
public int CoaxLevel { get; set; }
```

---

### 5. `AlignShapeMatchService.cs` — TrySaveRefPose/LoadRefPose에 Coax 직렬화 (D-05)

**Analog:** `WPF_Example/Custom/EthernetVision/AlignShapeMatchService.cs` :163-214

**Role:** service / CRUD

**TrySaveRefPose 패턴** (lines 163-198):
```csharp
private bool TrySaveRefPose(string jsonPath,
    double ref1Row, double ref1Col, ... ,
    out string error) {
    error = null;
    try {
        AlignRefPose refPose = new AlignRefPose();
        refPose.Ref1Row        = ref1Row;
        // ... 각 필드 대입 ...
        refPose.Roi1Len1 = roi1Len1;   // Phase 61.1 추가 선례

        JsonSerializerSettings saveSettings = new JsonSerializerSettings();
        saveSettings.TypeNameHandling = TypeNameHandling.None;
        saveSettings.Formatting = Formatting.Indented;
        string json = JsonConvert.SerializeObject(refPose, saveSettings);
        File.WriteAllText(jsonPath, json, System.Text.Encoding.UTF8);
        return true;
    }
    catch (Exception ex) {
        error = "TrySaveRefPose: " + ex.Message;
        return false;
    }
}
```

**LoadRefPose 패턴** (lines 201-214):
```csharp
private AlignRefPose LoadRefPose(string jsonPath) {
    try {
        if (!File.Exists(jsonPath)) {
            return null;
        }
        string json = File.ReadAllText(jsonPath, System.Text.Encoding.UTF8);
        JsonSerializerSettings settings = new JsonSerializerSettings();
        settings.TypeNameHandling = TypeNameHandling.None;
        return JsonConvert.DeserializeObject<AlignRefPose>(json, settings);
    }
    catch {
        return null;
    }
}
```

**수정 목표:**

D-05에서 동축 저장 = 슬롯 JSON(`Bottom_{slot}.json`)에 필드 추가. 실제로는 `AlignRefPose` POCO에 `CoaxEnabled`/`CoaxLevel` 추가(패턴 #4)로 충분하다. `TrySaveRefPose` 시그니처를 `bool coaxEnabled, int coaxLevel` 파라미터 추가로 확장하고, 내부에서:
```csharp
refPose.CoaxEnabled = coaxEnabled;
refPose.CoaxLevel   = coaxLevel;
```
대입 후 기존 직렬화 코드 그대로 실행. `LoadRefPose`는 무변경 (POCO의 키 부재 = 0/false 자동 처리).

**호출부 (`TryTeach` 내부, ~line 349):**
```csharp
bool bSaved = TrySaveRefPose(jsonPath,
    ref1Row, ref1Col, ref2Row, ref2Col,
    refBaselineRad, angleExtentDeg,
    roi1Len1, roi1Len2, roi2Len1, roi2Len2,
    out error);
```
→ 파라미터 끝에 `coaxEnabled, coaxLevel` 추가 형태로 확장.

**Tray 동축 저장:** Tray는 슬롯 없음 → `Tray.json` (단일) 동일 구조. 동일 `TrySaveRefPose`/`LoadRefPose` 재사용. 전달값 = TrayVisionView UI에서 읽은 `chk_coaxEnabled.IsChecked`, `sld_coaxLevel.Value`.

---

### 6. `BottomVisionView.xaml` — 동축 컨트롤 추가 (D-04)

**Analog:** `WPF_Example/Custom/UI/BottomVisionView.xaml` :164-177 (기존 CheckBox 패턴)

**Role:** component / request-response

**기존 CheckBox 패턴** (lines 166-169):
```xml
<CheckBox x:Name="chk_showRoi" Content="ROI 표시" IsChecked="True" Margin="0,8,0,0"
          Checked="ShowRoiCheckBox_Changed" Unchecked="ShowRoiCheckBox_Changed"/>
<CheckBox x:Name="chk_showEdge" Content="에지 표시" IsChecked="True" Margin="0,4,0,0"
          Checked="ShowEdgeCheckBox_Changed" Unchecked="ShowEdgeCheckBox_Changed"/>
```

**좌측 패널 Grid 구조** (lines 65-226): 현재 Row 0~5(0=헤더, 1=상태, 2=카메라그룹, 3=티칭, 4=검사결과, 5=피커캘, 6=여백). `RowDefinition` 추가 또는 GroupBox를 기존 GroupBox 중 하나에 끼워넣어야 함.

**권장 삽입 위치:** Row 2 `GroupBox("카메라 / 이미지 로더")` 내부 `StackPanel` 끝(슬롯 ComboBox+버튼 그룹 다음), 또는 Row 2와 Row 3 사이에 새 Row+`GroupBox("동축 조명")` 추가.

**수정 목표 — 새 GroupBox 삽입 (Row 2 뒤, Row 3 앞에 새 RowDefinition 추가 후):**
```xml
<!--260626 hbk Phase 66 — 동축 조명 컨트롤 (D-04, ALIGN_COAX 단일 채널)-->
<GroupBox Grid.Row="3" Header="동축 조명 (Align 전용)" Margin="0,0,0,8">
    <StackPanel Margin="8">
        <CheckBox x:Name="chk_coaxEnabled" Content="동축 ON/OFF"
                  Margin="0,0,0,4"
                  Checked="CoaxCheckBox_Changed" Unchecked="CoaxCheckBox_Changed"/>
        <StackPanel Orientation="Horizontal" Margin="0,0,0,4">
            <TextBlock Text="밝기:" VerticalAlignment="Center" Margin="0,0,6,0" FontSize="12"/>
            <Slider x:Name="sld_coaxLevel"
                    Minimum="0" Maximum="255" Width="160"
                    ValueChanged="CoaxSlider_ValueChanged"
                    VerticalAlignment="Center"/>
            <TextBlock x:Name="lbl_coaxLevel" Text="0" Margin="6,0,0,0"
                       FontSize="12" VerticalAlignment="Center" Width="28"/>
        </StackPanel>
    </StackPanel>
</GroupBox>
```

> 이미 있는 Row들(3=티칭, 4=검사결과, 5=피커캘)을 4/5/6으로 순번 이동 + RowDefinition 1개 추가 필요.

---

### 7. `BottomVisionView.xaml.cs` — 동축 이벤트 핸들러 + Grab/Teach/Run 직전 자동적용 (D-06, D-07)

**Analog:** `WPF_Example/Custom/UI/BottomVisionView.xaml.cs` (GrabButton_Click :190-231, TeachButton_Click :311-371, RunButton_Click :375-402)

**Role:** component / request-response

**GrabButton_Click 패턴** (lines 190-231) — 삽입점 = `Camera.Grab()` 호출 직전:
```csharp
private void GrabButton_Click(object sender, RoutedEventArgs e) {
    // ... SIMUL_MODE 분기 ...
#else
    if (EthernetVisionHandler.Handle.Camera == null) { ... return; }
    try {
        // 260626 hbk Phase 66 — grab 직전 동축 자동 적용 (D-06/D-07)
        ApplyCoaxLight();
        HImage img = EthernetVisionHandler.Handle.Camera.Grab();
        // ...
    }
#endif
}
```

**TeachButton_Click 패턴** (lines 311-371) — 삽입점 = `TryTeach` 호출 직전:
```csharp
private void TeachButton_Click(object sender, RoutedEventArgs e) {
    // ... 슬롯 가드, ROI 유효성 검증 ...
    try {
        // 260626 hbk Phase 66 — 티칭 직전 동축 자동 적용 (D-07 Teach도 동일 조명)
        ApplyCoaxLight();
        bool bOk = EthernetVisionHandler.Handle.Matcher.TryTeach(...);
        // ...
    }
}
```

**RunButton_Click 패턴** (lines 375-402) — 삽입점 = `Matcher.Run()` 호출 직전:
```csharp
private void RunButton_Click(object sender, RoutedEventArgs e) {
    // ...
    try {
        // 260626 hbk Phase 66 — 검사 직전 동축 자동 적용 (D-07 Run도 동일 조명)
        ApplyCoaxLight();
        AlignResult res = EthernetVisionHandler.Handle.Matcher.Run(_viewer.CurrentImage, VIEW_MODE, _selectedSlot);
        // ...
    }
}
```

**신규 헬퍼 + 이벤트 핸들러:**
```csharp
// 260626 hbk Phase 66 — 저장된 동축값 ALIGN_COAX 그룹에 적용 (D-06 자동 적용 공통 경로)
//  CoaxEnabled=true: SetOnOff(true)+SetLevel. false: SetOnOff(false)만.
private void ApplyCoaxLight()
{
    bool bEnabled = (chk_coaxEnabled.IsChecked == true);
    int nLevel = (int)sld_coaxLevel.Value;
    if (bEnabled)
    {
        LightHandler.Handle.SetOnOff(LightHandler.LIGHT_ALIGN_COAX, true);
        LightHandler.Handle.SetLevel(LightHandler.LIGHT_ALIGN_COAX, nLevel);
    }
    else
    {
        LightHandler.Handle.SetOnOff(LightHandler.LIGHT_ALIGN_COAX, false);
    }
}

// 260626 hbk Phase 66 — CheckBox 변경 → 즉시 적용 + JSON 저장
private void CoaxCheckBox_Changed(object sender, RoutedEventArgs e)
{
    ApplyCoaxLight();
    SaveSlotCoaxToJson();   // 슬롯 JSON 갱신
}

// 260626 hbk Phase 66 — Slider 변경 → 즉시 적용 + lbl 갱신 + JSON 저장
private void CoaxSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
{
    int nLevel = (int)e.NewValue;
    lbl_coaxLevel.Text = nLevel.ToString();
    ApplyCoaxLight();
    SaveSlotCoaxToJson();
}
```

**JSON 저장 헬퍼 (D-05 — 슬롯별 JSON 갱신):**
```csharp
// 260626 hbk Phase 66 — 현재 선택 슬롯 JSON에 CoaxEnabled/Level 반영 (TrySaveRefPose 재호출)
// 티칭 데이터(Ref1Row/Col 등)는 LoadRefPose로 읽어 덮어쓰지 않도록 주의
private void SaveSlotCoaxToJson()
{
    // 구현: LoadRefPose → refPose.CoaxEnabled/CoaxLevel 덮어쓰기 → TrySaveRefPose 재호출
    // 미티칭 슬롯(json 없음)이면 저장 스킵 (null guard)
}
```

**Loading 시 복원 패턴 (SlotComboBox_SelectionChanged에서 JSON 로드 후 UI 업데이트):**
```csharp
private void SlotComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
{
    // ... 기존 _selectedSlot 갱신 / ROI 복원 ...
    // 260626 hbk Phase 66 — 슬롯 JSON에서 Coax 설정 복원
    AlignRefPose refPose = /* LoadRefPose(jsonPath) */;
    if (refPose != null)
    {
        chk_coaxEnabled.IsChecked = refPose.CoaxEnabled;
        sld_coaxLevel.Value = refPose.CoaxLevel;
        lbl_coaxLevel.Text = refPose.CoaxLevel.ToString();
    }
    else
    {
        // 미티칭 슬롯 — 기본값 off/0
        chk_coaxEnabled.IsChecked = false;
        sld_coaxLevel.Value = 0;
        lbl_coaxLevel.Text = "0";
    }
}
```

**using 추가 필요 여부:** 기존 `BottomVisionView.xaml.cs`에 `using ReringProject.Device;`가 없음(TrayVisionView에는 있음). LightHandler 참조 위해 추가 필요.

---

### 8. `TrayVisionView.xaml(.cs)` — 동축 컨트롤 추가 (D-04, D-05 Tray 단일)

**Analog:** `WPF_Example/Custom/UI/BottomVisionView.xaml(.cs)` (패턴 #6/#7과 동일)

**Role:** component / request-response

**XAML 좌측 패널 삽입 위치:** 현재 Row 5=여백. TrayVisionView.xaml :143 Row 4="검사결과" GroupBox 끝 다음, Row 5=여백 Grid 앞에 새 RowDefinition + GroupBox 삽입.

**수정 목표 (XAML):**
```xml
<!--260626 hbk Phase 66 — 동축 조명 컨트롤 (D-04, ALIGN_COAX 단일)-->
<GroupBox Grid.Row="5" Header="동축 조명 (Align 전용)" Margin="0,0,0,8">
    <!-- BottomVisionView 패턴 #6과 동일 구조 -->
</GroupBox>
<!--Row 6: 여백 (기존 Row 5 → 6으로 이동)-->
<Grid Grid.Row="6"/>
```

**xaml.cs 수정:** Tray는 슬롯 없음 → `SaveSlotCoaxToJson()` 대신 `SaveTrayCoaxToJson()` (Tray 단일 JSON `Tray.json` 갱신). 나머지 `ApplyCoaxLight()`, `CoaxCheckBox_Changed`, `CoaxSlider_ValueChanged` 패턴은 Bottom과 동일.

**using 추가 (line 9, 이미 있음):**
```csharp
using ReringProject.Device;   // TrayVisionView.xaml.cs line 9 — 이미 존재
```

---

### 9. `SystemHandler.cs` §RunBottomAlign — grab 직전 동축 자동적용 (D-06)

**Analog:** `WPF_Example/Custom/SystemHandler.cs` :271-350 (RunBottomAlign 내부)

**Role:** service / request-response

**삽입점 확인:** line 300 `img = EthernetVisionHandler.Handle.Camera.Grab();` 직전.

**기존 RunBottomAlign grab 직전 코드** (lines 295-308):
```csharp
HImage img = null;
AlignResult res = null;
try
{
    //260626 hbk EthernetAlignCamera.Grab() — IsOpen 이면 라이브 grab, 아니면 폴백
    img = EthernetVisionHandler.Handle.Camera.Grab();
    if (img == null)
    {
        // ... NG 반환
    }
```

**수정 목표 — line 300 `img = ...Grab();` 앞에 삽입:**
```csharp
//260626 hbk Phase 66 — grab 직전 해당 슬롯 동축값 자동 적용 (D-06/D-07)
ApplyCoaxLightForSlot(slot);
img = EthernetVisionHandler.Handle.Camera.Grab();
```

**신규 헬퍼 메서드 (RunBottomAlign 아래 추가):**
```csharp
//260626 hbk Phase 66 — 슬롯 JSON에서 CoaxEnabled/CoaxLevel 읽어 ALIGN_COAX 적용 (D-06)
//  JSON 없음(미티칭) → 동축 off. 예외 → 로그 후 off (TCP 스레드 크래시 방지).
private void ApplyCoaxLightForSlot(EBottomAlignSlot slot)
{
    try
    {
        AlignRefPose refPose = EthernetVisionHandler.Handle.Matcher.LoadSlotRefPose(slot);
        bool bEnabled = refPose != null && refPose.CoaxEnabled;
        int nLevel = (refPose != null) ? refPose.CoaxLevel : 0;
        if (bEnabled)
        {
            LightHandler.Handle.SetOnOff(LightHandler.LIGHT_ALIGN_COAX, true);
            LightHandler.Handle.SetLevel(LightHandler.LIGHT_ALIGN_COAX, nLevel);
        }
        else
        {
            LightHandler.Handle.SetOnOff(LightHandler.LIGHT_ALIGN_COAX, false);
        }
    }
    catch (Exception ex)
    {
        Logging.PrintLog((int)ELogType.Error,
            "[ALIGN_TEST] ApplyCoaxLightForSlot 예외: {0} //260626 hbk", ex.Message);
        LightHandler.Handle.SetOnOff(LightHandler.LIGHT_ALIGN_COAX, false);
    }
}
```

> `EthernetVisionHandler.Handle.Matcher.LoadSlotRefPose(slot)` — AlignShapeMatchService에 `public` 경로 헬퍼 노출이 필요. 현재 `LoadRefPose`는 private. 대안: AlignShapeMatchService에 `public AlignRefPose GetSlotRefPose(EBottomAlignSlot slot)` 래퍼 추가 또는 SystemHandler에서 직접 json 경로 계산.

---

## Shared Patterns

### 조명 On/Off+Level 적용 순서
**Source:** `WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs` :341-345
**Apply to:** SystemHandler.ApplyCoaxLightForSlot, BottomVisionView.ApplyCoaxLight, TrayVisionView.ApplyCoaxLight
```csharp
// Enabled=true: SetOnOff(true) 먼저, 이후 SetLevel
LightHandler.Handle.SetOnOff(groupName, true);
LightHandler.Handle.SetLevel(groupName, level);
// Enabled=false: SetOnOff(false)만
LightHandler.Handle.SetOnOff(groupName, false);
```

### JSON 직렬화 (TypeNameHandling.None)
**Source:** `WPF_Example/Custom/EthernetVision/AlignShapeMatchService.cs` :186-191
**Apply to:** TrySaveRefPose 수정부
```csharp
JsonSerializerSettings saveSettings = new JsonSerializerSettings();
saveSettings.TypeNameHandling = TypeNameHandling.None;
saveSettings.Formatting = Formatting.Indented;
string json = JsonConvert.SerializeObject(refPose, saveSettings);
File.WriteAllText(jsonPath, json, System.Text.Encoding.UTF8);
```

### 예외 처리 (UI 핸들러)
**Source:** `WPF_Example/Custom/UI/BottomVisionView.xaml.cs` :227-229
**Apply to:** CoaxCheckBox_Changed, CoaxSlider_ValueChanged
```csharp
catch (Exception ex) {
    lbl_status.Text = "Grab 오류: " + ex.Message;
}
```

### 예외 처리 (TCP 스레드)
**Source:** `WPF_Example/Custom/SystemHandler.cs` :342-348
**Apply to:** ApplyCoaxLightForSlot
```csharp
catch (Exception ex)
{
    Logging.PrintLog((int)ELogType.Error,
        "[ALIGN_TEST] RunBottomAlign 예외: {0} //260626 hbk", ex.Message);
    // throw 금지 (TCP 스레드 크래시 방지, T-65-06)
    return false;
}
```

### ParamBase [Category] + [Browsable(false)] 공존
**Source:** `WPF_Example/Custom/Sequence/Inspection/ShotConfig.cs` :30-31, :85-86
**Apply to:** ShotConfig.cs CoaxLight 필드 숨김 (D-03)
- `[Browsable(false)]` 는 PropertyGrid 숨김 전용. `ParamBase.Save/Load`는 리플렉션으로 INI를 별도 처리하므로 직렬화 계속 동작.

---

## Line Drift 정리

| 파일 | CONTEXT.md 참조 | 실제 측정 라인 | 비고 |
|------|-----------------|---------------|------|
| `ShotConfig.cs` 조명 필드 | :60-75 | :60-75 | 일치 |
| `InspectionSequence.cs` ApplyShotLights 시작 | :321 | :321 | 일치 |
| `InspectionSequence.cs` Ring7 추가 지점 | ~:336/361 | Ring7 삽입 = line 379 뒤(SideLight 블록 끝) | CONTEXT가 "인근"으로 표현. 실제 삽입은 :380 앞 |
| `InspectionSequence.cs` Coax 매핑 | :361-368 | :361-368 | 일치 |
| `LightHandler.cs` RING7 등록 | ~:69 | :69 | 일치 |
| `LightHandler.cs` ALIGN_COAX 등록 | ~:72 | :72 | 일치 |

---

## No Analog Found

없음. 모든 파일에 직접 복제할 기존 코드 패턴이 존재한다.

---

## 추가 설계 결정 사항 (Planner를 위한 명시)

1. **AlignShapeMatchService.LoadRefPose 공개:** `private AlignRefPose LoadRefPose(string jsonPath)` → TCP 경로(`SystemHandler.ApplyCoaxLightForSlot`)에서 JSON 읽기 위해 public 래퍼 또는 json path 계산 로직 공개 필요. 권장: `public AlignRefPose GetSlotRefPose(EBottomAlignSlot slot)` 추가 (한 줄 래퍼).

2. **TrySaveRefPose 시그니처 확장:** 기존 `bool coaxEnabled, int coaxLevel` 파라미터를 끝에 추가. 호출부(TryTeach 내 line ~349) 도 업데이트 필요.

3. **BottomVisionView using 추가:** `using ReringProject.Device;` 없음 → LightHandler 참조 위해 line 1~13 블록에 추가.

4. **Slider 바인딩 방식:** PropertyTools.Wpf `HeaderedEntrySlider`는 Align 창에 어울리지 않음 (스타일 불일치). 표준 WPF `<Slider>` + `<TextBlock>` ValueChanged 핸들러 패턴 사용 (BottomVisionView.xaml의 chk_showRoi 스타일과 일관).

5. **Tray 동축 저장 JSON:** `{RecipeSavePath}\{RecipeName}\ETHERNET_ALIGN\Tray.json` (기존 BuildJsonPath(Tray, None) 경로). Tray는 슬롯 없으므로 단일 파일.

---

## Metadata

**Analog 검색 범위:** `WPF_Example/Custom/`, `WPF_Example/Device/`, `WPF_Example/UI/Light/`
**파일 스캔:** 12개
**Pattern extraction date:** 2026-06-26
