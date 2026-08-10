---
phase: quick-260807-htd
plan: 01
type: execute
wave: 1
depends_on: []
files_modified:
  - WPF_Example/Custom/EthernetVision/EthernetVisionHandler.cs
autonomous: false
requirements: [ETHERNET-ALARM-01]

must_haves:
  truths:
    - "이더넷 정렬 모드가 켜진 상태(Tray=1 / Bottom=2)에서 BottomAlign 카메라 연결에 실패하면, 앱 시작 직후 모달 알람 다이얼로그가 화면에 뜬다 — 더 이상 Camera 로그에만 남고 끝나지 않는다"
    - "다이얼로그 본문에 설정값(`SystemSetting.EthernetCameraIp` 에 들어있는 IP 또는 카메라 이름 문자열)이 그대로 표시되어, 사용자가 무엇을 고쳐야 하는지 화면만 보고 안다"
    - "다이얼로그는 7초 자동닫힘이 아니라 사용자가 닫을 때까지 남는다 (`isAutoClosing=false` — SystemHandler 의 'Camera Initialize Fail' 알림과 동일 옵션)"
    - "`EthernetVisionMode == None`(기능 비활성) 이면 다이얼로그가 뜨지 않는다 — 정상 상태를 오탐으로 알리지 않는다"
    - "연결 성공 시 다이얼로그가 뜨지 않는다"
    - "연결 재시도/타임아웃/열거/폴백 동작은 단 1바이트도 바뀌지 않는다 — `EthernetAlignCamera.cs` 는 무변경"
    - "알람 표시 실패가 초기화를 중단시키지 않는다 — `EthernetVisionHandler.Initialize()` 는 여전히 절대 throw 하지 않는다"
    - "새 Dispatcher 호출을 만들지 않는다 — `CustomMessageBox.Show` 가 내부에서 이미 `App.Current.Dispatcher.BeginInvoke` 로 마샬링하므로 이중 마샬링이 되면 안 된다"
    - "Debug/x64 Rebuild 가 신규 `error CS` 0 건으로 통과한다"
    - "사용자의 미커밋 실HW 세팅 3파일(csproj SIMUL_MODE 제거 / LightHandler 배선표 / SystemHandler memory_allocator 주석)이 baseline 그대로 남고, 커밋에도 포함되지 않는다"
  artifacts:
    - path: "WPF_Example/Custom/EthernetVision/EthernetVisionHandler.cs"
      provides: "연결 실패 알람 헬퍼 ShowConnectFailAlarm + Initialize() 실패 2경로(else/catch) 배선"
      contains: "ShowConnectFailAlarm"
  key_links:
    - from: "EthernetVisionHandler.Initialize() 의 연결 실패 else 분기 (bConnected == false)"
      to: "ShowConnectFailAlarm(camIp, null)"
      via: "기존 Logging.PrintLog 바로 다음 줄 호출"
      pattern: "ShowConnectFailAlarm\\(camIp, null\\)"
    - from: "EthernetVisionHandler.Initialize() 의 catch(Exception) 경로 (모드가 켜져 있었을 때만)"
      to: "ShowConnectFailAlarm(camIp, ex.Message)"
      via: "bModeOn 가드 — 모드 None 이면 알람 없음"
      pattern: "if \\(bModeOn\\)"
    - from: "ShowConnectFailAlarm"
      to: "ReringProject.UI.CustomMessageBox.Show"
      via: "기존 공용 다이얼로그 재사용(신규 UI 수단 도입 금지). 스레드 마샬링은 CustomMessageBox 내부 BeginInvoke 가 담당"
      pattern: "CustomMessageBox\\.Show\\("
---

<objective>
BottomAlign 정렬 카메라(이더넷 / Hik GigE) **연결 실패를 사용자가 화면으로 즉시 알 수 있게** 한다.

현재는 `EthernetVisionHandler.Initialize()` 가 `Camera.Connect(camIp)` 실패 시 `IsInitialized = false` 로 두고
Camera 로그에 `[ETHERNET] connect failed (fallback active)` 한 줄만 남긴다. 사용자는 **아무 것도 못 본 채**
정렬이 폴백 이미지(`D:\align_test.bmp`)로 돌아가는 상태를 모르고 계속 쓴다.

**해결:** 이 프로젝트가 이미 쓰고 있는 `CustomMessageBox` 로 실패 알람을 띄운다.
`SystemHandler` 생성자에서 `DeviceHandler.Initialize()` 실패 시 `CustomMessageBox.Show("Camera Error", ..., Error, true, false)`
를 띄우는 것과 **완전히 동일한 수단·동일한 옵션**을 쓴다. 새 다이얼로그 메커니즘은 만들지 않는다.

**Output:**
- `EthernetVisionHandler.ShowConnectFailAlarm(string camIp, string exMessage)` — private 헬퍼 1개 신설
- `Initialize()` 의 실패 2경로(연결실패 else / 예외 catch)에서 호출 배선
- 변경 파일 **정확히 1개**
</objective>

<execution_context>
@$HOME/.claude/get-shit-done/workflows/execute-plan.md
@$HOME/.claude/get-shit-done/templates/summary.md
</execution_context>

<context>
@./CLAUDE.md
@.planning/CONVENTIONS.md

**코딩 규칙 (이 프로젝트 상시 규칙 — 위반 시 리뷰 반려):**
- C# 7.2 (`nullable reference types`, `switch expression`, `record` 등 8.0+ 문법 금지)
- 삼항 중첩 2depth 이상 금지 / `??` + `?` 혼용 금지 → `if / else` 로 분리
- **해당 파일의 기존 스타일 유지** — `EthernetVisionHandler.cs` 는 **K&R**(여는 중괄호 같은 줄)
- bool 로컬은 이 파일 기존 관례대로 `b` 접두(`bModeOff`, `bConnected`). string 로컬은 이 파일 기존 관례대로
  **접두 없는 camelCase**(`camIp` 전례) — CONVENTIONS.md 의 `sz` 접두는 이 파일에 전례가 없으므로 적용하지 않는다.
- 신규 주석은 `quick-260807-htd:` 접두 + 짧게, **비자명한 "왜"만**.
  `//YYMMDD hbk` 날짜 주석 규칙은 2026-06-11 부로 폐기 — 새로 달지 말 것(기존 것은 그대로 보존).

**⚠ 작업 시작 시점의 미커밋 변경 3건 — 사용자의 실HW bring-up 설정이다. 절대 건드리지도, 커밋하지도 말 것:**

| 파일 | 내용 | baseline `git diff \| git hash-object --stdin` |
|------|------|------------------------------------------------|
| `WPF_Example/DatumMeasurement.csproj` | `DefineConstants` 에서 `SIMUL_MODE` 제거 (이 PC 는 현재 **실HW 모드**) | `f0dd3a511bd51a3cc6df91c555d4336df60e0c0d` |
| `WPF_Example/Custom/Device/LightHandler.cs` | 실배선표 반영 (Controller A 8채널 / B 5채널 재배치) | `3d982f0bf0bb345f5f8103b0420c120c405b2218` |
| `WPF_Example/SystemHandler.cs` | `SetSystem("memory_allocator","system")` 한 줄 주석처리 | `c3cfe91472977903dd2ed061d6b088f92f58c207` |

작업 후에도 이 3개의 해시가 **동일해야** 한다. `git add` 는 `EthernetVisionHandler.cs` **한 파일만**.

**⚠ 열지도 고치지도 말 것:**
- `WPF_Example/Custom/EthernetVision/EthernetAlignCamera.cs` — 연결/열거/폴백 로직. 이번 요구는 "알림 추가"뿐이고
  연결 동작 변경은 명시적 범위 밖이다. `git status` 에 이 파일이 등장하면 실패다.
- `WPF_Example/UI/Dialog/CustomMessageBox.cs` — 공용 다이얼로그. 재사용만 하고 수정 금지.

**이미 확정된 사실 (executor 는 재조사하지 말 것):**
1. `EthernetVisionHandler.Initialize()` 의 호출부는 **단 한 곳** — `WPF_Example/SystemHandler.cs:217`,
   `SystemHandler.Initialize()` 의 맨 마지막. 이건 `MainWindow` 생성자(`MainWindow.xaml.cs:81`)에서 호출되고,
   `MainWindow` 생성자는 `App.Application_Startup`(`App.xaml.cs:53`)에서 실행된다 → **UI 스레드**다.
2. 그럼에도 **여기서 `Dispatcher.Invoke` 를 직접 쓰면 안 된다.** `CustomMessageBox.Show` 가 내부에서
   `App.Current.Dispatcher.BeginInvoke(Normal, ...)` 로 이미 마샬링하며, 람다 내부에도 try-catch 방어가 있다.
   즉 **호출 스레드 무관하게 안전**하다. 밖에서 또 감싸면 이중 마샬링이다.
3. `BeginInvoke` 라 다이얼로그는 `Application_Startup` 이 끝난 뒤(= `view.Show()` 이후) 표시된다.
   `SystemHandler` 생성자의 기존 "Camera Initialize Fail" 알림이 **똑같은 자리에서 똑같은 방식으로 이미 동작 중**이므로
   실증된 경로다 — 새 검증 불필요.
4. `CustomMessageBox.Show(...)` 의 반환값은 `BeginInvoke` 비동기 특성상 항상 `false` 다. **반환값을 쓰지 말 것.**
5. `isAutoClosing` 기본값은 `true`(7초 뒤 자동 닫힘, `MessageBoxModel.TIME_AUTOCLOSING = 7`).
   알람은 놓치면 안 되므로 **명시적으로 `false`** 를 넘긴다.
6. `SystemHandler.IsInitializeFail` 은 **건드리지 않는다.** (a) setter 가 private 이라 외부에서 못 쓰고,
   (b) 이더넷 정렬 실패는 Phase 58 설계상 "Grabber/검사 무영향"인 비차단 실패다.
   이 핸들러의 기존 `IsInitialized` 플래그가 이미 동등한 역할을 한다 — 플래그는 그대로, **알림만 추가**한다.
7. 메시지는 Localize 사전을 타지 않고 **한국어 원문 문자열**을 쓴다. 최근 코드(`BottomVisionView`, `InspectionListView`,
   `ReviewerWindow`)가 전부 원문 한국어를 쓰고 있고, 이 문자열은 사전에 없다.
8. `EEthernetVisionMode`: `None=0`(연결 시도 안 함) / `Tray=1` / `Bottom=2`.
   `SystemSetting.EthernetCameraIp` 기본값 `"192.168.1.100"`, `[Category("ETHERNET_VISION")]` 로 설정창 PropertyGrid 노출.
   `HikCamera.Open(ip)` 이 DeviceList 를 **이름/IP/FriendlyName** 으로 검색하므로 이 설정값에는 IP 뿐 아니라
   카메라 이름("BottomAlign" 등)도 들어갈 수 있다 → 메시지에서 "IP" 라고 단정하지 말고 **"설정값"** 으로 표기한다.

<interfaces>
<!-- 실행자가 코드베이스를 탐색할 필요가 없도록 현재 라이브 코드와 목표 코드를 그대로 옮겨둔다. -->

**[현재] `WPF_Example/Custom/EthernetVision/EthernetVisionHandler.cs` L1~7 (usings)**

```csharp
//260623 hbk Phase 58
using System;
using HalconDotNet;
using ReringProject.Device;
using ReringProject.Setting;
using ReringProject.Utility;
```

**[현재] 같은 파일 L43~81 (`Initialize()` 전체)**

```csharp
        public void Initialize() {
            try {
                //260624 hbk Phase 59 — D-02: Matcher 는 stateless → 모드/연결 결과 무관하게 항상 생성
                Matcher = new AlignShapeMatchService();
                //260624 hbk Phase 60 — D-01: PickerCal stateful → 모드/연결 결과 무관 항상 생성
                PickerCal = new PickerCenterCalibrationService();

                bool bModeOff = SystemSetting.Handle.EthernetVisionMode == EEthernetVisionMode.None;
                if (bModeOff) {
                    Logging.PrintLog((int)ELogType.Camera, "[ETHERNET] mode = None, skip connect");
                    IsInitialized = false;
                    return;
                }

                Camera = new EthernetAlignCamera();
                string camIp = SystemSetting.Handle.EthernetCameraIp;
                bool bConnected = Camera.Connect(camIp);

                IsInitialized = bConnected;
                if (bConnected) {
                    Logging.PrintLog((int)ELogType.Camera, "[ETHERNET] connected: {0}", camIp);
                }
                else {
                    Logging.PrintLog((int)ELogType.Camera, "[ETHERNET] connect failed (fallback active): {0}", camIp);
                }
            }
            catch (Exception ex) {
                IsInitialized = false;
                //260624 hbk Phase 59 — 예외 경로에서도 Matcher null 방지
                if (Matcher == null) {
                    Matcher = new AlignShapeMatchService();
                }
                //260624 hbk Phase 60 — 예외 경로에서도 PickerCal null 방지
                if (PickerCal == null) {
                    PickerCal = new PickerCenterCalibrationService();
                }
                Logging.PrintLog((int)ELogType.Error, "[ETHERNET] EthernetVisionHandler.Initialize error: {0}", ex.Message);
            }
        }
```

**[목표] `Initialize()` 교체본 — 이 형태 그대로 만든다**

```csharp
        public void Initialize() {
            //quick-260807-htd: 예외 경로에서도 같은 알람을 띄우려면 모드/설정값이 try 밖에 살아 있어야 한다.
            bool bModeOn = false;
            string camIp = null;
            try {
                //260624 hbk Phase 59 — D-02: Matcher 는 stateless → 모드/연결 결과 무관하게 항상 생성
                Matcher = new AlignShapeMatchService();
                //260624 hbk Phase 60 — D-01: PickerCal stateful → 모드/연결 결과 무관 항상 생성
                PickerCal = new PickerCenterCalibrationService();

                bool bModeOff = SystemSetting.Handle.EthernetVisionMode == EEthernetVisionMode.None;
                if (bModeOff) {
                    Logging.PrintLog((int)ELogType.Camera, "[ETHERNET] mode = None, skip connect");
                    IsInitialized = false;
                    return;
                }
                bModeOn = true;

                Camera = new EthernetAlignCamera();
                camIp = SystemSetting.Handle.EthernetCameraIp;
                bool bConnected = Camera.Connect(camIp);

                IsInitialized = bConnected;
                if (bConnected) {
                    Logging.PrintLog((int)ELogType.Camera, "[ETHERNET] connected: {0}", camIp);
                }
                else {
                    Logging.PrintLog((int)ELogType.Camera, "[ETHERNET] connect failed (fallback active): {0}", camIp);
                    ShowConnectFailAlarm(camIp, null);
                }
            }
            catch (Exception ex) {
                IsInitialized = false;
                //260624 hbk Phase 59 — 예외 경로에서도 Matcher null 방지
                if (Matcher == null) {
                    Matcher = new AlignShapeMatchService();
                }
                //260624 hbk Phase 60 — 예외 경로에서도 PickerCal null 방지
                if (PickerCal == null) {
                    PickerCal = new PickerCenterCalibrationService();
                }
                Logging.PrintLog((int)ELogType.Error, "[ETHERNET] EthernetVisionHandler.Initialize error: {0}", ex.Message);
                //quick-260807-htd: 모드가 켜져 있었는데 예외로 죽은 것 = 사용자 입장에선 똑같은 "연결 실패"다.
                if (bModeOn) {
                    ShowConnectFailAlarm(camIp, ex.Message);
                }
            }
        }
```

**[목표] 신설 private 헬퍼 — `Initialize()` 닫는 중괄호 다음, 클래스 닫는 중괄호 앞에 삽입**

```csharp
        //quick-260807-htd: 연결 실패가 로그에만 남아 사용자가 몰랐다 → 기존 카메라 실패 알림과 같은 수단으로 통일.
        // 스레드 마샬링을 여기서 하지 않는 이유: CustomMessageBox.Show 가 내부에서 이미
        // App.Current.Dispatcher.BeginInvoke 로 넘기므로 호출 스레드 무관하게 안전하다(이중 마샬링 금지).
        // isAutoClosing=false : 기본 7초 자동닫힘을 끈다. 알람은 사용자가 직접 닫아야 한다.
        private void ShowConnectFailAlarm(string camIp, string exMessage) {
            try {
                string target = camIp;
                if (string.IsNullOrEmpty(target)) {
                    target = "(설정값 없음)";
                }
                string message = string.Format(
                    "BottomAlign 정렬 카메라(이더넷 / Hik GigE)에 연결하지 못했습니다.\n\n" +
                    "설정값 : {0}\n" +
                    "(설정 창 > ETHERNET_VISION > EthernetCameraIp)\n\n" +
                    "확인할 것 : 카메라 전원 / 랜선 / IP 대역 / 다른 프로그램의 카메라 점유\n" +
                    "자세한 원인은 Camera 로그의 [ETHERNET] 항목에 있습니다.\n\n" +
                    "연결될 때까지 정렬은 폴백 이미지로 동작합니다. (일반 검사 기능은 영향 없음)",
                    target);
                if (string.IsNullOrEmpty(exMessage) == false) {
                    message = message + "\n\n예외 : " + exMessage;
                }
                CustomMessageBox.Show("카메라 연결 실패", message, System.Windows.MessageBoxImage.Error, true, false);
            }
            catch (Exception ex) {
                //알림 실패가 초기화를 막으면 안 된다 (CustomMessageBox 내부도 방어하지만 이중 방어)
                Logging.PrintLog((int)ELogType.Error, "[ETHERNET] connect fail alarm show error: {0}", ex.Message);
            }
        }
```

**참고: 그대로 따라 하는 기존 전례 — `WPF_Example/SystemHandler.cs` L102~106**

```csharp
            if (result != EInitializeResult.Success) {
                IsInitializeFail = true;
                //CustomMessageBox.Show("Error", "Camera Initialize Fail", MessageBoxImage.Error);
                CustomMessageBox.Show("Camera Error", "Camera Initialize Fail", MessageBoxImage.Error, true, false);
            }
```

**참고: `CustomMessageBox` 시그니처 (`WPF_Example/UI/Dialog/CustomMessageBox.cs` L24)**

```csharp
public static bool Show(string title, string message,
    MessageBoxImage imageType = MessageBoxImage.Information,
    bool isModal = true, bool isAutoClosing = true,
    int autoClosingTime = MessageBoxModel.TIME_AUTOCLOSING)
```
</interfaces>
</context>

<tasks>

<task type="auto">
  <name>Task 1: 연결 실패 알람 다이얼로그 배선 + Debug/x64 Rebuild</name>
  <files>WPF_Example/Custom/EthernetVision/EthernetVisionHandler.cs</files>
  <action>
`WPF_Example/Custom/EthernetVision/EthernetVisionHandler.cs` **한 파일만** 편집한다.
위 `<interfaces>` 의 **[목표]** 블록 3개를 그대로 옮기는 것이 작업의 전부다. 창작하지 말 것.

**편집 1 — using 추가.**
`using ReringProject.Setting;` 다음 줄에 `using ReringProject.UI;` 를 넣는다(Device → Setting → UI → Utility 알파벳 순서 유지).
`using System.Windows;` 는 **추가하지 않는다** — `MessageBoxImage` 는 헬퍼 안에서 `System.Windows.MessageBoxImage.Error` 로
완전수식한다(`SequenceBase.cs:425` 에 같은 전례가 있고, `HalconDotNet` 과의 타입 충돌 위험을 원천 차단한다).

**편집 2 — `Initialize()` 를 [목표] 교체본으로 바꾼다.**
실제 변경분은 5군데뿐이고 나머지 줄은 **한 글자도 건드리지 않는다**(기존 `//260624 hbk` 주석 전부 보존):
1. `public void Initialize() {` 와 `try {` 사이에 `bool bModeOn = false;` + `string camIp = null;` 선언(+ 이유 주석 1줄) 삽입
2. `if (bModeOff) { ... return; }` 블록 닫는 중괄호 다음 줄에 `bModeOn = true;` 추가
3. `string camIp = SystemSetting.Handle.EthernetCameraIp;` → `camIp = SystemSetting.Handle.EthernetCameraIp;` (선언 → 대입. `string` 키워드 제거)
4. `else` 분기의 `connect failed (fallback active)` 로그 **다음 줄**에 `ShowConnectFailAlarm(camIp, null);` 추가
5. `catch` 블록 마지막 `Logging.PrintLog(... Initialize error ...)` **다음 줄**에 `if (bModeOn) { ShowConnectFailAlarm(camIp, ex.Message); }` 를
   K&R 여러 줄 형태로 추가(+ 이유 주석 1줄)

**편집 3 — `ShowConnectFailAlarm` private 헬퍼를 [목표] 그대로 추가.**
`Initialize()` 의 닫는 중괄호 다음, 클래스 닫는 중괄호 앞. 메시지 문자열은 [목표] 원문 그대로(줄바꿈 `\n` 포함) 쓴다.
`MessageBoxWindow` 는 `\n` 을 줄바꿈으로 렌더한다(`MainWindow.xaml.cs:303` 전례).

**하지 말 것 (전부 의도적 제외):**
- `Dispatcher.Invoke` / `BeginInvoke` 를 이 파일에 추가 — 이중 마샬링이다. verify 가 `Dispatcher` 0건을 검사한다.
- `SystemHandler.IsInitializeFail` 세팅 시도 — private setter + 비차단 실패 설계.
- `EthernetAlignCamera.Connect` 에 실패 사유 out 파라미터 추가 — 연결 로직 무변경이 이번 범위의 하드 제약이다.
  사유는 이미 Camera 로그(`[ETHERNET] Connect: no device found for {ip}` / `found[i]: name=... ip=...`)에 남으며,
  메시지가 그 로그를 보라고 안내한다.
- 재시도 루프 / 타임아웃 / 백그라운드 재연결 추가 — 범위 밖.
- 이미 알려진 한계는 **고치지 말고 SUMMARY 에만 적는다**: 장치 카메라 실패와 이더넷 실패가 동시에 나면
  `CustomMessageBox` 가 이전 다이얼로그를 `Close()` 하고 새 것을 띄우므로(`CustomMessageBox.cs:13~20, 37`)
  나중 것(이더넷)만 보인다. 이건 공용 다이얼로그의 기존 동작이고 이번 범위 밖이다.

**빌드.** 현재 이 PC 에 `DatumMeasurement.exe` 는 실행 중이지 않으므로(planner 확인) 정상 경로 `-t:Rebuild` 가 통과해야 한다.
MSBuild 경로는 이 PC 기준 `C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe` 다
(못 찾으면 `"/c/Program Files (x86)/Microsoft Visual Studio/Installer/vswhere.exe" -latest -requires Microsoft.Component.MSBuild -find "MSBuild/**/Bin/MSBuild.exe"` 로 재확인).
Git Bash 에서는 `//p:` 가 깨지므로 **`-p:` / `-t:` 대시 프리픽스**를 쓴다(직전 세션에서 확인된 이슈).
빌드에 1~2분 걸릴 수 있으니 Bash 툴 timeout 을 300000 으로 준다.
만약 앱이 켜져 있어 `MSB3021/3026/3027/3030` 잠금 오류가 나면 — **프로세스를 절대 죽이지 말고**
`-p:OutputPath="$TEMP/gsd-htd-scratch/bin/" -p:BaseIntermediateOutputPath="$TEMP/gsd-htd-scratch/obj/"` 로 컴파일만 재확인하고
SUMMARY 에 "산출물 잠김으로 스크래치 컴파일 검증" 이라고 남긴다.

**커밋.** `git add` 는 `WPF_Example/Custom/EthernetVision/EthernetVisionHandler.cs` **한 파일만**.
`git commit -m "feat(quick-260807-htd): BottomAlign 이더넷 카메라 연결 실패 시 알람 다이얼로그 표시"`.
`git add .` / `git add -A` / `git commit -a` **절대 금지** — 위 표의 미커밋 실HW 세팅 3파일이 딸려간다.
  </action>
  <verify>
    <automated>cd "C:/code/DataMeasurement" && F=WPF_Example/Custom/EthernetVision/EthernetVisionHandler.cs && CODE=$(grep -v '^[[:space:]]*//' "$F") && echo "=== [1] using ReringProject.UI; : 1 기대 ===" && (grep -c 'using ReringProject.UI;' "$F" || echo 0) && echo "=== [2] ShowConnectFailAlarm : 3 기대 (정의1+호출2) ===" && (echo "$CODE" | grep -c 'ShowConnectFailAlarm' || echo 0) && echo "=== [3] CustomMessageBox.Show( 호출 : 1 기대 ===" && (echo "$CODE" | grep -c 'CustomMessageBox.Show(' || echo 0) && echo "=== [4] 모달+자동닫힘해제 옵션 : 1 기대 ===" && (echo "$CODE" | grep -c 'MessageBoxImage.Error, true, false)' || echo 0) && echo "=== [5] bModeOn 가드 : 3 기대 (선언+대입+if) ===" && (echo "$CODE" | grep -c 'bModeOn' || echo 0) && echo "=== [6] 이중 마샬링 금지 — Dispatcher : 0 기대 ===" && (echo "$CODE" | grep -c 'Dispatcher' || echo 0) && echo "=== [7] IsInitializeFail 미사용 : 0 기대 ===" && (echo "$CODE" | grep -c 'IsInitializeFail' || echo 0) && echo "=== [8] EthernetAlignCamera.cs 무변경 : 빈 출력 기대 ===" && git status --porcelain -- WPF_Example/Custom/EthernetVision/EthernetAlignCamera.cs && echo "=== [9] 미커밋 실HW 3파일 baseline 해시 ===" && (git diff -- WPF_Example/DatumMeasurement.csproj | git hash-object --stdin) && (git diff -- WPF_Example/Custom/Device/LightHandler.cs 2>/dev/null | git hash-object --stdin) && (git diff -- WPF_Example/SystemHandler.cs | git hash-object --stdin) && echo "=== [10] 변경 파일 목록 ===" && git status --porcelain && echo "=== [11] Debug/x64 Rebuild ===" && "/c/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" "WPF_Example/DatumMeasurement.csproj" -t:Rebuild -p:Configuration=Debug -p:Platform=x64 -v:minimal -nologo 2>&1 | grep -iE "error CS|error MSB|warning CS|Build succeeded" | head -25</automated>
  </verify>
  <done>
- [1] `1`, [2] `3`, [3] `1`, [4] `1`, [5] `3`, [6] `0`, [7] `0` — 전부 정확히 일치.
- [8] 빈 출력 (`EthernetAlignCamera.cs` 무변경).
- [9] 해시 3개가 순서대로 `f0dd3a511bd51a3cc6df91c555d4336df60e0c0d` / `3d982f0bf0bb345f5f8103b0420c120c405b2218` / `c3cfe91472977903dd2ed061d6b088f92f58c207` — baseline 과 동일.
- [10] 커밋 전 기준 변경 파일 4개(기존 3 + `EthernetVisionHandler.cs`), 커밋 후 기준 3개(기존 3만 남음).
- [11] `Build succeeded`, 신규 `error CS` 0건 / 신규 `warning CS` 0건 (기존부터 있던 `CS0618`/`CS0162` 류 경고 재등장은 무관).
- 커밋 1건 생성, 그 커밋의 `git show --stat` 파일 목록이 `EthernetVisionHandler.cs` **한 줄뿐**.
  </done>
</task>

<task type="checkpoint:human-verify" gate="blocking">
  <name>Task 2: 실기 확인 — 연결 실패 시 알람 노출 / 정상 시 무알람</name>
  <files>(코드 변경 없음 — 사용자 실기 확인 전용)</files>
  <action>
여기서 **멈추고** 아래 `<how-to-verify>` 절차를 사용자에게 그대로 제시한 뒤 응답을 기다린다.
executor 가 대신 판정하지 말 것 — 다이얼로그가 실제로 화면에 뜨는지는 사람 눈으로만 확인 가능하다.

앱 실행 시 이 프로젝트의 하드 규칙을 지킨다: 빌드 산출물이 잠겨 있어도 **프로세스를 강제 종료하지 않는다.**
사용자가 직접 앱을 껐다 켜도록 안내한다.

사용자가 문제를 보고하면 수정 후 이 체크포인트를 다시 제시한다. "승인" 을 받은 뒤에 SUMMARY 를 작성한다.
  </action>
  <what-built>
`EthernetVisionHandler.Initialize()` 가 BottomAlign 카메라 연결에 실패하면 이제 모달 다이얼로그
"카메라 연결 실패" 를 띄웁니다. 본문에 설정값(EthernetCameraIp)과 확인 항목이 적혀 있고,
7초 자동닫힘 없이 직접 닫을 때까지 남습니다. 연결 로직/재시도/폴백 동작은 전혀 건드리지 않았습니다.
  </what-built>
  <how-to-verify>
**A. 실패 시 알람이 뜨는가 (핵심)**
1. 설정 창 > `ETHERNET_VISION` > `EthernetVisionModeValue` 가 `0` 이면 이 기능은 꺼진 상태라 알람도 안 뜹니다.
   `1`(Tray) 또는 실제 쓰는 값 `2`(Bottom) 로 바꾸고 저장하세요.
2. `EthernetCameraIp` 를 일부러 없는 값(예: `192.168.99.99`)으로 바꾸고 저장 — **원래 값을 먼저 메모해 두세요.**
   (또는 실제로 카메라 랜선을 뽑아도 됩니다.)
3. 프로그램을 껐다 다시 켭니다. `WPF_Example\bin\x64\Debug\DatumMeasurement.exe`
4. 기대: 메인 화면이 뜬 직후 **"카메라 연결 실패"** 다이얼로그가 화면 가운데에 뜬다.
   - 본문에 `설정값 : 192.168.99.99` 가 그대로 보인다.
   - 7초 지나도 **저절로 안 닫힌다** (직접 닫아야 함).
5. 2번에서 메모해 둔 **원래 값으로 되돌리고 저장**하세요.

**B. 정상일 때 헛알람이 없는가**
6. 카메라가 실제로 연결되는 정상 설정으로 재시작 → 다이얼로그가 **안 떠야** 정상입니다.
   (여기서 알람이 뜬다면 그건 오탐이 아니라 실제로 연결이 안 되고 있다는 뜻 — Camera 로그의 `[ETHERNET]` 줄을 보세요.)

**C. 기능 꺼둔 상태에서 조용한가**
7. `EthernetVisionModeValue` 를 `0` 으로 되돌리고 재시작 → 다이얼로그가 **안 떠야** 정상입니다.
   (검사만 쓰는 PC 에서 매번 알람이 뜨면 안 되므로 이 확인이 필요합니다.)

**D. 검사 회귀 없음**
8. 평소 하던 검사 1회(또는 일괄검사)를 그대로 돌려서 이전과 동일하게 동작하는지만 확인하세요.
  </how-to-verify>
  <verify>
    <human-check>A(알람 뜸) / B(정상 시 무알람) / C(모드 OFF 시 무알람) / D(검사 회귀 없음) 사용자 승인</human-check>
  </verify>
  <done>사용자가 A~D 를 확인하고 "승인" 응답</done>
  <resume-signal>"승인" 또는 문제점을 설명해 주세요 (예: "A 는 떴는데 C 에서도 뜬다")</resume-signal>
</task>

</tasks>

<threat_model>
## Trust Boundaries

| Boundary | Description |
|----------|-------------|
| (신규 없음) | 이번 변경은 로컬 UI 알림만 추가한다. 네트워크 입력 파싱, 파일 I/O, 외부 명령 실행, 신규 패키지 설치 모두 없음 |

## STRIDE Threat Register

| Threat ID | Category | Component | Disposition | Mitigation Plan |
|-----------|----------|-----------|-------------|-----------------|
| T-htd-01 | Information Disclosure | `ShowConnectFailAlarm` 메시지에 `ex.Message` / 카메라 설정값 노출 | accept | 로컬 산업용 PC 의 운영자 전용 화면이며, 표시되는 값은 사용자 본인이 설정창에 입력한 값과 예외 메시지뿐이다. 자격증명·PII 없음. 진단 가치가 노출 위험보다 크다 |
| T-htd-02 | Denial of Service | 초기화 경로에서 모달 다이얼로그가 UI 스레드를 잡음 | accept | 앱 기동 시 1회, 실패 시에만 발생. `SystemHandler` 의 기존 "Camera Initialize Fail" 알림과 동일한 노출도 — 신규 위험 아님 |
| T-htd-03 | Denial of Service | 알람 표시 중 예외가 초기화를 중단시킴 | mitigate | `ShowConnectFailAlarm` 전체를 try-catch 로 감싸 로그만 남기고 삼킨다(`CustomMessageBox` 내부 방어와 이중). `Initialize()` 의 throw 금지 계약 유지 |
| T-htd-SC | Tampering | npm/pip/cargo 설치 | N/A | 이번 작업에 패키지 설치 없음 (기존 어셈블리 참조만 사용) |
</threat_model>

<verification>
- Task 1 `<automated>` 11개 항목 전부 기대값 일치 + `Build succeeded`.
- Task 2 사용자 실기 확인 A/B/C/D 승인.
- 최종 `git log -1 --stat` 이 `EthernetVisionHandler.cs` 단일 파일 커밋임을 보여준다.
- `git status --porcelain` 에 미커밋 실HW 3파일만 남고, 그 3파일의 diff 해시가 baseline 과 동일하다.
</verification>

<success_criteria>
- 이더넷 정렬 모드 ON + 연결 실패 → 사용자가 화면에서 즉시 인지 (모달 알람, 자동닫힘 없음, 설정값 표시)
- 모드 OFF 또는 연결 성공 → 알람 없음 (헛알람 0)
- `EthernetAlignCamera.cs` 무변경 = 연결/재시도/폴백 동작 회귀 0
- 변경 파일 정확히 1개, 신규 `error CS` 0건
</success_criteria>

<output>
Create `.planning/quick/260807-htd-bottomalign-ethernet-hik-gige/260807-htd-SUMMARY.md` when done
</output>
</content>
