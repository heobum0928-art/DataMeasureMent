---
phase: quick-260902-fwj-grab
plan: 01
type: execute
wave: 1
depends_on: []
files_modified:
  - WPF_Example/Custom/SystemSetting.cs
  - WPF_Example/Custom/UI/TrayVisionView.xaml.cs
  - WPF_Example/Custom/UI/BottomVisionView.xaml.cs
autonomous: false
requirements: [QG-01, QG-02, QG-03]

must_haves:
  truths:
    - "Tray/Bottom 얼라인 화면에서 수동 Grab 버튼을 누르면 설정한 시간(ms) 뒤 동축 조명이 꺼진다"
    - "연속으로 Grab 을 누르면 소등 시각이 마지막 Grab 기준으로 다시 계산된다"
    - "Live 를 켜면 대기 중이던 소등 예약이 취소되어 Live 도중 조명이 꺼지지 않는다"
    - "Stop 을 누르면 잔여 소등 예약이 남지 않는다"
    - "설정값이 0 이하이면 자동 소등이 전혀 일어나지 않는다(기존 동작 유지)"
    - "자동 검사 사이클(Action_FAIMeasurement 등)과 티칭 경로의 조명 동작은 이번 변경 전후가 동일하다"
  artifacts:
    - path: "WPF_Example/Custom/SystemSetting.cs"
      provides: "AlignCoaxAutoOffMs 설정 + ALIGN_COAX_AUTO_OFF_MS_DEFAULT 상수"
      contains: "AlignCoaxAutoOffMs"
    - path: "WPF_Example/Custom/UI/TrayVisionView.xaml.cs"
      provides: "_coaxAutoOffTimer + Start/Cancel/Tick 3메서드 + Grab/Live/Stop 배선"
      contains: "_coaxAutoOffTimer"
    - path: "WPF_Example/Custom/UI/BottomVisionView.xaml.cs"
      provides: "_coaxAutoOffTimer + Start/Cancel/Tick 3메서드 + Grab/Live/Stop 배선"
      contains: "_coaxAutoOffTimer"
  key_links:
    - from: "GrabButton_Click (#else 실HW 분기)"
      to: "StartCoaxAutoOffTimer()"
      via: "try 블록의 finally"
      pattern: "finally \\{[^}]*StartCoaxAutoOffTimer"
    - from: "StartCoaxAutoOffTimer()"
      to: "SystemSetting.Handle.AlignCoaxAutoOffMs"
      via: "설정값 읽기 + 0 이하 게이트"
      pattern: "SystemSetting\\.Handle\\.AlignCoaxAutoOffMs"
    - from: "CoaxAutoOffTimer_Tick()"
      to: "LightHandler.Handle.SetOnOff(LIGHT_ALIGN_COAX, false)"
      via: "1회성 Tick 소등"
      pattern: "SetOnOff\\(LightHandler\\.LIGHT_ALIGN_COAX, false\\)"
    - from: "LiveButton_Click (bOk 성공 분기)"
      to: "CancelCoaxAutoOffTimer()"
      via: "ApplyCoaxLight() 호출 직전"
      pattern: "CancelCoaxAutoOffTimer\\(\\);"
    - from: "StopButton_Click"
      to: "CancelCoaxAutoOffTimer()"
      via: "StopLiveTimer() 옆"
      pattern: "CancelCoaxAutoOffTimer\\(\\);"
---

<objective>
얼라인 화면(Tray/Bottom)의 **수동 Grab 버튼**으로 촬영한 뒤, 설정한 시간(ms)이 지나면 동축 조명이 자동으로 꺼지게 한다. 시간은 `SystemSetting` 항목으로 조절하고, 0 이하로 두면 자동 소등이 꺼진다(지금 동작 그대로).

Purpose: 지금은 Grab 으로 켠 동축이 Stop 을 누르거나 체크박스를 끌 때까지 계속 켜져 있다. 수동 촬영 뒤 조명이 방치되는 것을 없앤다.
Output: 설정 1개 + 두 화면 각각 타이머 필드 1개 / 메서드 3개 / 호출 3곳.

**범위 한정(사용자 명시)**: 이 화면들의 **Grab 버튼(수동 촬영)** 전용이다. 자동 검사 사이클(`Action_FAIMeasurement` 등)과 티칭 경로는 이번 작업 대상이 **아니며 건드리지 않는다**.
</objective>

<execution_context>
@$HOME/.claude/get-shit-done/workflows/execute-plan.md
</execution_context>

<context>
@./CLAUDE.md

관련 소스(수정 대상):
- `WPF_Example/Custom/SystemSetting.cs`
- `WPF_Example/Custom/UI/TrayVisionView.xaml.cs`
- `WPF_Example/Custom/UI/BottomVisionView.xaml.cs`

<interfaces>
<!-- 플래너가 실제 코드로 확인한 계약. 실행자는 이 정보로 충분하며 추가 탐색이 필요 없다. -->

LightHandler (`WPF_Example/Custom/Device/LightHandler.cs`, namespace `ReringProject.Device`):
- `public const string LIGHT_ALIGN_COAX = "ALIGN_COAX";`
- `LightHandler.Handle.SetOnOff(string group, bool on)`
- `LightHandler.Handle.SetLevel(string group, int level)`

두 View 모두 `using ReringProject.Device;`, `using ReringProject.Setting;`, `using System.Windows.Threading;` 를 이미 들고 있다. **using 추가 불필요.**

기존 Live 타이머 선례(그대로 따를 패턴):
- 필드: `private DispatcherTimer _liveTimer;` — Tray 55행 / Bottom 61행
- `StartLiveTimer()` : `if (_liveTimer != null) { return; }` → `new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) }` → `Tick +=` → `Start()`
- `StopLiveTimer()` : null 가드 → `Stop()` → `Tick -=` → `= null`

확인된 앵커 라인(작업 시작 시 실제 파일로 재확인할 것):

| 앵커 | Tray | Bottom |
|------|------|--------|
| `private void GrabButton_Click` | 122 | 310 |
| Grab `#else` | 139 | 327 |
| Grab `lbl_status.Text = "취득 실패 (폴백 없음)";` | 150 | 338 |
| Grab 성공 `lbl_status.Text = "대기";` | 158 | 346 |
| Grab `catch` 의 `"Grab 오류: "` | 161 | 349 |
| Grab `#endif` | 163 | 351 |
| `private void LiveButton_Click` | 166 | 354 |
| Live 성공분기 `ApplyCoaxLight();` | 180 | 368 |
| `private void StopButton_Click` | 194 | 382 |
| Stop 의 `StopLiveTimer();` | 201 | 389 |
| `LiveTimer_Tick` 끝 | 254 | 425 부근 |

`SystemSetting` 사용 선례(같은 View 안에 이미 있음): `SystemSetting.Handle.EthernetPixelResolution`, `SystemSetting.Handle.Save()`.
</interfaces>

<critical_findings>
플래너가 코드에서 확인한 사실. 실행자는 **이 결정을 뒤집지 말 것**.

**F1 — `[Category]` 는 반드시 이웃과 같은 짧은 형태로 쓴다.**
`WPF_Example/Custom/SystemSetting.cs` 는 `using System.ComponentModel;` 만 들고 있어서 짧은 `[Category("...")]` 가 `System.ComponentModel.CategoryAttribute` 로 잡힌다. base `SystemSetting.Load()/Save()`(`WPF_Example/Setting/SystemSetting.cs` 292-310행)는 `PropertyTools.DataAnnotations.CategoryAttribute` 만 인식한다. 그리고 그 반복문의 `group` 변수는 **인식되는 어트리뷰트를 만날 때만 갱신되고 그 외에는 직전 값을 그대로 유지한다(sticky).**

결론:
- 짧은 형태로 쓰면 이 프로퍼티는 `group` 을 **바꾸지 않으므로**, 다른 어떤 프로퍼티의 INI 섹션에도 영향을 주지 않는다. Save/Load 가 같은 로직이라 왕복은 항상 일치한다. → **안전**
- 완전정규화(`[PropertyTools.DataAnnotations.Category("ETHERNET_VISION")]`)로 쓰면 `group` 이 바뀌고, 리플렉션 순서상 뒤따르는(어트리뷰트가 인식되지 않는) 프로퍼티들의 섹션이 이동한다. `PickerCenterRow/Col`(HW 캘 결과) 같은 **기존 저장값이 조용히 0 으로 유실될 수 있다.** → **금지**

`ETHERNET_VISION` 블록 **맨 끝(`CalibSearchCol2` 뒤)** 에 이웃과 똑같은 짧은 형태로 추가한다. 파일 상단 260818 주석이 완전정규화를 권하지만, 그것은 그때 새로 만든 연속 블록 얘기이고 이 블록에 적용하면 위 유실 위험이 생긴다.

**F2 — 기존 설치(Setting.ini 에 키가 없는 경우)에서는 3000 이 아니라 0 으로 로드된다.**
`IniSection` 인덱서는 키가 없으면 `IniValue.Default` 를 돌려주고 `ToInt()` 가 예외 없이 `0` 을 준다(`WPF_Example/Utility/Ini.cs` 953-966, 179-185행). base Load 의 `case "Int32"` 가 이 0 으로 C# 초기값(3000)을 덮어쓴다.

의도적으로 **그대로 둔다.** `AfterLoad()` 에서 "0 이면 3000 으로 복원"을 하면 사용자가 0 을 넣어 자동 소등을 끌 방법이 사라진다 — 요구사항 "0 이하 = 비활성"과 정면 충돌한다.
실제 영향: **기존 PC 에서는 설정 창에서 값을 한 번 넣어야 기능이 켜진다.** 이건 하위호환(기존 동작 유지)이기도 하다. 체크포인트 안내문에 반드시 넣는다.

**F3 — Grab 실패/예외에도 소등 예약을 건다(`finally`).**
근거: 예약을 걸지 말지의 기준은 "촬영 성공"이 아니라 **"이번 클릭이 `ApplyCoaxLight()` 로 조명을 켰는가"** 다. `img == null` 조기 return 이나 `Camera.Grab()` 예외 상황은 이미 `ApplyCoaxLight()` 가 실행된 뒤이므로 조명이 켜져 있고, 여기서 예약을 안 걸면 **가장 눈치채기 어려운 실패 경로에서 조명이 방치된다.** `try` 의 `finally` 한 곳에 넣으면 성공/실패/예외 3경로가 중복 없이 한 번에 덮인다.
`Camera == null` 조기 return 은 `try` **밖**이라 `ApplyCoaxLight()` 가 실행되지 않았고 `finally` 도 타지 않는다 — 예약 없음이 맞다.
두 화면 동일하게 처리한다.

**F4 — `#if SIMUL_MODE` 의 `#else`(실HW) 경로만 손댄다.**
SIMUL 쪽 Grab 은 `Ookii.Dialogs` 파일 열기 다이얼로그라 카메라도 조명도 쓰지 않는다(Tray 123-138 / Bottom 311-326행에서 확인). 조명과 무관하므로 `#else` 한정이 맞다.
`LiveButton_Click` / `StopButton_Click` 은 `#if` 밖이라 두 빌드 구성 모두에서 컴파일된다. 타이머 필드와 3개 메서드도 `#if` 밖에 둔다 → **SIMUL_MODE 정의 여부와 무관하게 컴파일 깨지지 않는다.**

**F5 — 티칭/검사 버튼과의 충돌은 없다.**
`DispatcherTimer` 는 UI 스레드에서 돈다. 동기 버튼 핸들러(`TeachButton_Click`, `RunButton_Click`) 실행 도중에 Tick 이 끼어들 수 없다. 따라서 그 경로의 `ApplyCoaxLight()` 호출들은 손댈 필요가 없다(사용자 지정 무변경 대상).
</critical_findings>
</context>

<tasks>

<task type="auto">
  <name>Task 1: SystemSetting 에 동축 자동 소등 딜레이 설정 추가</name>
  <files>WPF_Example/Custom/SystemSetting.cs</files>
  <action>
`ALIGN_VERIFY_IMAGE_KEEP_DAYS_DEFAULT` 등 기존 상수들이 모여 있는 곳(파일 상단 33-51행 구간)에 기본값 상수를 추가한다. 매직넘버 금지 규칙 준수:

`private const int ALIGN_COAX_AUTO_OFF_MS_DEFAULT = 3000;`

상수 위에 "왜"를 한 줄로 남긴다: 얼라인 화면 수동 Grab 뒤 동축을 자동으로 끄기까지의 대기 시간(ms), 0 이하는 자동 소등 안 함.

그 다음 파일 **맨 끝** `CalibSearchCol2` 프로퍼티 **뒤**에 설정 프로퍼티를 추가한다:

`[Category("ETHERNET_VISION")]` — **이웃과 똑같은 짧은 형태로 쓴다. F1 참조. 완전정규화(`PropertyTools.DataAnnotations.` 접두) 로 "고치지" 말 것 — 기존 저장값이 유실된다.**
`public int AlignCoaxAutoOffMs { get; set; } = ALIGN_COAX_AUTO_OFF_MS_DEFAULT;`

프로퍼티 주석에 의미를 명확히 적는다: 얼라인 화면 Grab 버튼(수동 촬영) 전용, 이 시간(ms) 뒤 동축 소등, 0 이하면 자동 소등 안 함, 기존 Setting.ini 에 키가 없으면 0 으로 로드되므로 설정 창에서 한 번 넣어야 켜진다(F2).

`AfterLoad()` 는 **건드리지 않는다**(F2 — 0 복원 로직을 넣으면 사용자가 기능을 끌 수 없다).

주석에 물음표(`?`) 문자를 쓰지 말 것 — Task 3 의 스타일 게이트(삼항 grep)에 걸린다.
  </action>
  <verify>
    <automated>grep -c "AlignCoaxAutoOffMs" WPF_Example/Custom/SystemSetting.cs   # 2 이상(상수 사용 1 + 프로퍼티 1)</automated>
    <automated>grep -n "AlignCoaxAutoOffMs" WPF_Example/Custom/SystemSetting.cs | grep -c "PropertyTools.DataAnnotations.Category"   # 0 이어야 함 (F1)</automated>
    <automated>grep -n "ALIGN_COAX_AUTO_OFF_MS_DEFAULT" WPF_Example/Custom/SystemSetting.cs   # const 선언 + 프로퍼티 초기값 2군데</automated>
  </verify>
  <done>`SystemSetting.Handle.AlignCoaxAutoOffMs` 로 읽을 수 있는 int 설정이 존재하고, 기본값 상수가 선언돼 있으며, `[Category]` 는 이웃 ETHERNET_VISION 항목과 동일한 짧은 형태다.</done>
</task>

<task type="auto">
  <name>Task 2: Tray/Bottom 두 화면에 1회성 소등 타이머 배선</name>
  <files>WPF_Example/Custom/UI/TrayVisionView.xaml.cs, WPF_Example/Custom/UI/BottomVisionView.xaml.cs</files>
  <action>
**두 파일에 동일한 변경을 넣는다. 공통 베이스 클래스/헬퍼로 묶는 리팩토링을 시도하지 말 것** — 두 화면은 이미 같은 코드를 각자 들고 있는 구조이고, CLAUDE.md 는 "이번에 손대는 지점에만" 적용하라고 못박는다. 기존 code-behind 구조를 그대로 따른다(K&R 중괄호, 두 파일 모두).

**(a) 타이머 필드 — `private DispatcherTimer _liveTimer;` 바로 아래 (Tray 55행 / Bottom 61행)**

`private DispatcherTimer _coaxAutoOffTimer;`

주석: Grab 버튼(수동 촬영) 전용 동축 자동 소등 타이머. 1회성 — Tick 에서 즉시 자기 자신을 정지한다. 자동 검사 사이클/티칭 경로와는 무관하다.

**(b) 메서드 3개 — `LiveTimer_Tick` 메서드 바로 뒤에 추가 (Tray 254행 뒤 / Bottom 425행 부근 뒤)**

1. `private void StartCoaxAutoOffTimer()`
   - 맨 먼저 `CancelCoaxAutoOffTimer();` 호출 → 연속 Grab 시 마지막 Grab 기준으로 리셋된다.
   - `int nDelayMs = SystemSetting.Handle.AlignCoaxAutoOffMs;`
   - `if (nDelayMs <= 0) { return; }` — 자동 소등 비활성(기존 동작 유지).
   - `_coaxAutoOffTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(nDelayMs) };`
   - `_coaxAutoOffTimer.Tick += CoaxAutoOffTimer_Tick;`
   - `_coaxAutoOffTimer.Start();`

2. `private void CancelCoaxAutoOffTimer()`
   - `if (_coaxAutoOffTimer == null) { return; }`
   - `Stop()` → `Tick -= CoaxAutoOffTimer_Tick` → `_coaxAutoOffTimer = null;`
   - `StopLiveTimer()` 와 완전히 같은 관용구.

3. `private void CoaxAutoOffTimer_Tick(object sender, EventArgs e)`
   - **첫 줄에서 `CancelCoaxAutoOffTimer();`** — 1회만 발화하게 만든다(반복 소등 방지).
   - `try { LightHandler.Handle.SetOnOff(LightHandler.LIGHT_ALIGN_COAX, false); }`
   - `catch (Exception ex) { lbl_status.Text = "동축 소등 오류: " + ex.Message; }` — 이 화면 규약대로 throw 금지, 상태 라벨만.
   - UI 체크박스 상태와 무관하게 무조건 소등한다. `StopButton_Click` 의 기존 소등과 같은 규칙이다.

각 메서드 위에 XML `<summary>` 한 줄씩. **물음표(`?`) 문자를 쓰지 말 것**(스타일 게이트).

**(c) `GrabButton_Click` — `#else`(실HW) 분기의 try 에 `finally` 추가**

기존:
`try { ApplyCoaxLight(); ... lbl_status.Text = "대기"; } catch (Exception ex) { lbl_status.Text = "Grab 오류: " + ex.Message; }`

여기에 `catch` 뒤로 `finally` 블록을 붙이고 그 안에서 `StartCoaxAutoOffTimer();` 한 번만 호출한다.
`finally` 위에 근거 주석: 성공/취득실패/예외 어느 경로든 `ApplyCoaxLight()` 는 이미 실행됐으므로 조명이 켜져 있다. 세 경로 모두에서 소등을 예약한다(F3).
`try` 블록 안의 기존 코드(`ApplyCoaxLight()` 호출, `img == null` 조기 return, `_viewer.LoadImage`, `img.Dispose()`, 상태 라벨)는 **한 줄도 바꾸지 않는다.**
`#if SIMUL_MODE` 쪽(파일 선택 다이얼로그)은 **손대지 않는다**(F4).

**(d) `LiveButton_Click` — 성공 분기의 `ApplyCoaxLight();` 바로 앞에 `CancelCoaxAutoOffTimer();`**

`if (bOk) { ... btn_live.IsEnabled = false; CancelCoaxAutoOffTimer(); ApplyCoaxLight(); StartLiveTimer(); }`
주석: 직전 Grab 이 걸어둔 소등 예약이 Live 도중에 터져 조명을 꺼버리는 것을 막는다.
`else` 분기(Live 실패)에는 넣지 않는다 — 조명은 여전히 Grab 이 켜둔 상태이므로 예약이 살아 있는 것이 맞다.

**(e) `StopButton_Click` — `StopLiveTimer();` 바로 다음 줄에 `CancelCoaxAutoOffTimer();`**

Stop 은 그 아래에서 이미 무조건 소등하므로, 남은 예약을 정리만 한다. 커밋 `a4da35bc` 가 넣은 `LightHandler.Handle.SetOnOff(..., false);` 두 줄은 **그대로 둔다.**

**무변경 대상(재확인)**: `ApplyCoaxLight()` 본문, 티칭(`TeachButton_Click`)·검사(`RunButton_Click`)·캘리브 경로의 기존 `ApplyCoaxLight()` 호출, `_liveTimer` 관련 로직 전부, 버튼 활성화 토글, `WPF_Example/DatumMeasurement.csproj`.
  </action>
  <verify>
    <automated>for f in WPF_Example/Custom/UI/TrayVisionView.xaml.cs WPF_Example/Custom/UI/BottomVisionView.xaml.cs; do echo "$f: field=$(grep -c '_coaxAutoOffTimer;' $f) start=$(grep -c 'StartCoaxAutoOffTimer' $f) cancel=$(grep -c 'CancelCoaxAutoOffTimer' $f)"; done   # 각 파일 field=1, start=2(정의+finally), cancel=4(정의+Start내부+Live+Stop) 이상</automated>
    <automated>grep -n -A2 'finally' WPF_Example/Custom/UI/TrayVisionView.xaml.cs | grep -c StartCoaxAutoOffTimer   # 1</automated>
    <automated>grep -n -B2 'ApplyCoaxLight();$' WPF_Example/Custom/UI/BottomVisionView.xaml.cs | grep -c CancelCoaxAutoOffTimer   # 1 (Live 성공분기)</automated>
    <automated>git diff -- WPF_Example/DatumMeasurement.csproj | grep -c 'SIMUL_MODE'   # 2 (사용자 실험분 그대로, 우리가 건드리지 않았음)</automated>
  </verify>
  <done>두 화면 모두: 필드 1개 + 메서드 3개가 존재하고, Grab 의 실HW 분기 `finally` 에서 예약, Live 성공분기에서 `ApplyCoaxLight()` 앞 취소, Stop 에서 정리가 배선돼 있다. `#if SIMUL_MODE` 구조와 무변경 대상은 그대로다.</done>
</task>

<task type="auto">
  <name>Task 3: 스타일 게이트 + 빌드 + 커밋</name>
  <files>(검증 전용 — 소스 수정 없음)</files>
  <action>
**(a) CLAUDE.md 하드룰 게이트 — 이번에 추가된 줄에만 적용한다.**
세 파일 모두 기존 코드에 `?.` / 삼항 / 날짜주석이 이미 존재하므로 파일 전체 grep 은 0 이 될 수 없다. **diff 의 추가 라인만** 검사한다:

```
git diff -U0 -- WPF_Example/Custom/SystemSetting.cs WPF_Example/Custom/UI/TrayVisionView.xaml.cs WPF_Example/Custom/UI/BottomVisionView.xaml.cs \
  | grep '^+' | grep -v '^+++' > /tmp/added.txt
grep -cE '\?[^\?]*:' /tmp/added.txt    # 삼항 → 0
grep -cF '??'        /tmp/added.txt    # null 병합 → 0
grep -cF '?.'        /tmp/added.txt    # null 조건 → 0
grep -cE 'switch.*=>' /tmp/added.txt   # switch 식 → 0
grep -cF 'hbk'       /tmp/added.txt    # 날짜 주석 → 0
```
전부 `0` 이어야 한다. `0` 이 아니면 해당 줄을 고친 뒤 다시 돌린다(주석 안의 물음표도 삼항 grep 에 걸리므로 물음표를 지운다).

추가로 육안 확인: 한 줄짜리 분기에도 중괄호가 있는지, 헝가리언(`nDelayMs`, `bOk`, `szMsg`)을 지켰는지, 매직넘버가 없는지(딜레이는 설정값, 기본값은 상수).

**(b) 빌드 — Debug|x64.**

```
"C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" \
  WPF_Example/DatumMeasurement.csproj -p:Configuration=Debug -p:Platform=x64 -m -v:m
```
통과 기준: **`error CS` 0건.** 사용자가 Visual Studio 로 실행 중이면 bin 복사 실패(`MSB3027` / `MSB3021`)가 날 수 있는데 **허용**한다. **프로세스를 강제 종료하지 말 것.**
확인: `... 2>&1 | grep -c "error CS"` → `0`.

참고: 현재 워킹트리의 csproj 는 `SIMUL_MODE` 가 빠져 있어(사용자 미커밋 실험) 이 빌드가 **실HW `#else` 경로를 실제로 컴파일한다** — 이번 변경의 핵심 경로가 검증된다.

**(c) 커밋 — 파일을 명시적으로만 스테이징.**

`git add .` / `git add -A` **금지.** 사용자 미커밋 실험 `WPF_Example/DatumMeasurement.csproj`(SIMUL_MODE 제거)가 워킹트리에 있으므로 절대 함께 올라가면 안 된다.

```
git add WPF_Example/Custom/SystemSetting.cs \
        WPF_Example/Custom/UI/TrayVisionView.xaml.cs \
        WPF_Example/Custom/UI/BottomVisionView.xaml.cs
git diff --cached --name-only
```
스테이징 목록에 **정확히 이 3개만** 있어야 한다. `DatumMeasurement.csproj` 가 보이면 `git restore --staged WPF_Example/DatumMeasurement.csproj` 로 빼고 다시 확인한다.

커밋 메시지(날짜 주석 규칙과 별개, 커밋 메시지는 한글 허용):
`feat(quick-260902-fwj): 얼라인 수동 Grab 후 동축 자동 소등(설정 딜레이)`

계획 문서도 같이 커밋한다: `git add .planning/quick/260902-fwj-grab/`
  </action>
  <verify>
    <automated>git diff -U0 -- WPF_Example/Custom/SystemSetting.cs WPF_Example/Custom/UI/TrayVisionView.xaml.cs WPF_Example/Custom/UI/BottomVisionView.xaml.cs | grep '^+' | grep -v '^+++' | grep -cE '\?[^\?]*:|\?\?|\?\.|switch.*=>|hbk'   # 0</automated>
    <automated>"C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" WPF_Example/DatumMeasurement.csproj -p:Configuration=Debug -p:Platform=x64 -m -v:m 2>&1 | grep -c "error CS"   # 0</automated>
    <automated>git diff --cached --name-only | grep -c "DatumMeasurement.csproj"   # 0</automated>
  </verify>
  <done>추가 라인에 금지 문법 0건, `error CS` 0건, 스테이징에 csproj 미포함, 커밋 완료.</done>
</task>

<task type="checkpoint:human-verify" gate="blocking">
  <name>Task 4: 실기 육안 확인 (Tray/Bottom)</name>
  <action>실행을 여기서 멈추고 아래 <how-to-verify> 절차를 사용자에게 그대로 보여준다. 코드 수정 없음 — 사용자 응답을 받은 뒤에만 다음으로 진행한다.</action>
  <what-built>
얼라인 화면(Tray/Bottom)의 Grab 버튼으로 촬영하면, 설정한 시간이 지난 뒤 동축 조명이 스스로 꺼집니다. 시간은 설정 창에서 바꿀 수 있고, 0 으로 두면 지금처럼 안 꺼집니다.
  </what-built>
  <how-to-verify>
**먼저 설정부터 하세요 (중요).**
이미 쓰던 PC 라면 설정 파일에 이 항목이 없어서 **처음에는 0(자동 소등 꺼짐)** 으로 읽힙니다. 그러니 프로그램을 켜고 설정 창을 열어 `AlignCoaxAutoOffMs` 항목을 찾아 **3000**(=3초)을 넣고 저장하세요. 항목이 안 보이면 여기서 멈추고 알려주세요.

그 다음 Tray 얼라인 화면에서 아래 4가지를 확인합니다. 끝나면 Bottom 얼라인 화면에서 똑같이 한 번 더 합니다.

1. **시간 뒤 꺼지는가**
   동축 체크박스를 켜고 밝기를 올린 뒤 [Grab] 을 한 번 누릅니다.
   → 사진이 찍히고, 약 3초 뒤 동축 조명이 저절로 꺼져야 합니다.

2. **연속으로 누르면 밀리는가**
   [Grab] 을 누르고 1~2초 뒤에 다시 [Grab] 을 누릅니다.
   → 첫 번째 누른 시점 기준 3초가 아니라, **마지막으로 누른 시점 기준 3초 뒤**에 꺼져야 합니다. (중간에 깜빡 꺼지면 안 됩니다.)

3. **Live 중에는 안 꺼지는가**
   [Grab] 을 누르고 1초쯤 뒤에 [Live] 를 누릅니다.
   → Live 화면이 나오는 동안 조명이 계속 켜져 있어야 합니다. 3초쯤 지났을 때 툭 꺼지면 실패입니다.
   [Stop] 을 누르면 (원래대로) 조명이 꺼집니다.

4. **0 으로 두면 안 꺼지는가**
   설정에서 값을 **0** 으로 바꾸고 저장한 뒤 [Grab] 을 누릅니다.
   → 조명이 계속 켜져 있어야 합니다(예전 그대로).

**그리고 안 바뀌었는지 확인할 것 하나**
평소 하던 검사 한 사이클을 돌려서, 검사 중 조명이 예전과 똑같이 동작하는지만 봐주세요. 이번 작업은 화면의 Grab 버튼만 건드렸습니다.
  </how-to-verify>
  <resume-signal>"승인" 이라고 쓰시거나, 어느 항목이 어떻게 달랐는지 알려주세요</resume-signal>
</task>

</tasks>

<threat_model>
## Trust Boundaries

| Boundary | Description |
|----------|-------------|
| 로컬 설정파일(Setting.ini) → 앱 | 사람이 편집 가능한 정수값이 타이머 간격으로 들어온다 |
| 앱 → 조명 컨트롤러(시리얼) | 소등 명령 1회 발신 |

## STRIDE Threat Register

| Threat ID | Category | Component | Disposition | Mitigation Plan |
|-----------|----------|-----------|-------------|-----------------|
| T-FWJ-01 | Denial of Service | `StartCoaxAutoOffTimer` | mitigate | 설정값 `<= 0` 게이트로 0/음수 Interval 예외 차단. `DispatcherTimer` 1개만 유지(재호출 시 Cancel 후 재생성)해 타이머 누적 방지 |
| T-FWJ-02 | Tampering | `Setting.ini` 의 `AlignCoaxAutoOffMs` | accept | 로컬 장비 설정파일. 잘못된 값의 최대 영향은 조명 소등 타이밍 오차뿐이며 검사 결과에 영향 없음 |
| T-FWJ-03 | Denial of Service | `CoaxAutoOffTimer_Tick` → 시리얼 조명 | mitigate | Tick 첫 줄에서 자기 자신을 Cancel 해 1회 발화 보장(반복 소등 명령 폭주 방지) + try/catch 로 throw 금지 |
| T-FWJ-SC | Tampering | 패키지 설치 | n/a | 이번 작업에 신규 패키지 설치 없음 |
</threat_model>

<verification>
1. 추가된 diff 라인에 금지 문법(삼항/`??`/`?.`/switch 식/날짜주석) 0건
2. Debug|x64 빌드 `error CS` 0건 (bin 복사 실패 MSB3027/MSB3021 은 허용)
3. `git diff --cached --name-only` 에 `WPF_Example/DatumMeasurement.csproj` 없음
4. 두 화면의 `#if SIMUL_MODE` / `#else` / `#endif` 구조가 변경 전과 동일
5. `ApplyCoaxLight()` 본문 및 티칭/검사 경로 호출부 무변경 (`git diff` 로 확인)
6. 실기 육안 확인 4항목 통과 (Tray/Bottom 각각)
</verification>

<success_criteria>
- `SystemSetting.Handle.AlignCoaxAutoOffMs` 설정이 존재하고 설정 창에서 편집 가능하다
- Tray/Bottom 두 화면에서 수동 Grab 후 설정 시간 뒤 동축이 자동 소등된다
- 연속 Grab 시 마지막 Grab 기준으로 소등 시각이 리셋된다
- Live 시작 시 대기 중인 소등 예약이 취소되어 Live 도중 조명이 꺼지지 않는다
- 설정값 0 이하면 자동 소등이 일어나지 않는다(기존 동작 유지)
- 자동 검사 사이클/티칭 경로의 조명 동작 무변경
- 사용자 미커밋 csproj 실험분이 커밋에 포함되지 않는다
</success_criteria>

<output>
Create `.planning/quick/260902-fwj-grab/260902-fwj-SUMMARY.md` when done
</output>
</content>
</invoke>
