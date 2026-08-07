---
phase: quick-260807-lbu
plan: 01
type: execute
wave: 1
depends_on: []
files_modified:
  - WPF_Example/SystemHandler.cs
autonomous: false
requirements: [OFFLINE-RESET-01]

must_haves:
  truths:
    - "앱을 새로 켜면 OfflineInspectMode 는 이전 세션에 무엇으로 저장돼 있었든 항상 OFF 로 시작한다"
    - "리셋은 Setting.Load() 가 완전히 끝난 뒤(생성자 L79 Setting = SystemSetting.Handle), 그리고 이 값을 읽는 코드(Sequences 생성 step 2 / VisionServer step 3 / UI)가 돌기 전에 적용된다"
    - "실제로 켜져 있던 경우에만 Setting.ini 에도 False 를 즉시 반영한다 — 그래야 Settings 창을 여는 순간 생성자의 pSetting.Load()(SettingWindow.xaml.cs:26)가 디스크의 True 를 다시 읽어 조용히 되살리는 구멍이 막힌다"
    - "이미 OFF 인 정상 기동에서는 디스크 쓰기가 단 1회도 발생하지 않는다(회귀 표면 0)"
    - "Setting.Save() 가 실패해도(파일 잠김/권한) 앱 시작은 막히지 않는다 — 메모리 값은 이미 OFF 이므로 안전 목적은 이미 달성된 상태다"
    - "실행 중 사용자가 Settings 창에서 OfflineInspectMode 를 직접 켜는 기존 동작은 100% 그대로다 — 켠 뒤 Settings 창을 다시 열어도 켜진 채로 남는다"
    - "InspectionListView.xaml.cs 의 RUN 확인 팝업(L415~429), SystemSetting.cs, Custom/SystemSetting.cs, SettingWindow.xaml.cs, Action_FAIMeasurement.cs 는 단 1바이트도 변하지 않는다"
    - "사용자의 미커밋 실험 3건(csproj 의 SIMUL_MODE 제거 / LightHandler.cs / SystemHandler.cs 의 memory_allocator 주석처리)이 이번 커밋에 딸려 들어가지 않고, 작업 후에도 워킹트리에 그대로 남는다"
    - "Debug/x64 빌드가 신규 error CS 0 / 신규 warning CS 0 으로 통과한다"
  artifacts:
    - path: "WPF_Example/SystemHandler.cs"
      provides: "Initialize() 진입부의 OfflineInspectMode 강제 OFF 블록 (HALCON SetSystem 블록 직후, Stopwatch 직전)"
      contains: "Setting.OfflineInspectMode = false;"
  key_links:
    - from: "SystemSetting 생성자 Load() (SystemSetting.cs:193-195) 및 AfterLoad(), SystemHandler 생성자 L79"
      to: "SystemHandler.Initialize() 의 강제 OFF 블록"
      via: "Load 는 생성자에서 이미 끝났으므로 Initialize() 의 어떤 지점이든 Load 이후가 보장된다"
      pattern: "if \\(Setting\\.OfflineInspectMode\\) \\{"
    - from: "강제 OFF 블록"
      to: "Setting.ini 의 OfflineInspectMode=False"
      via: "리셋이 실제 발생한 경우에만 Setting.Save() 1회 (try/catch 로 시작 차단 방지)"
      pattern: "Setting\\.Save\\(\\);"
    - from: "강제 OFF 블록 위치"
      to: "Action_FAIMeasurement(L252, L527) / InspectionListView(L420) 의 읽기 지점"
      via: "블록이 Sequences 생성(L152), VisionServer 생성(L158), 시스템 스레드 기동(L170), UI InitializeComponent 보다 먼저"
      pattern: "image_cache_capacity 다음 OfflineInspectMode 다음 Stopwatch 순서"
---

<objective>
**앱을 새로 켤 때마다 `OfflineInspectMode` 를 무조건 OFF 로 시작**시킨다. 그게 전부다.

**왜 필요한가 (실제 사고):**
`OfflineInspectMode` 는 레시피가 아니라 `SystemSetting`(시스템 전역·영속) 이라, 한 번 켜두면 앱을 껐다 켜도
켜진 채로 남는다. 이 상태에서는 실HW 빌드(SIMUL_MODE off)에서도 라이브 촬영 대신 **과거에 저장해 둔 이미지**로
검사한다(`Action_FAIMeasurement.cs:252` EStep.Grab, `:527` GrabOrLoadDatumImage 의 `#else` 분기).
오늘 실제로 이 설정이 켜진 채 남아 있어서, TCP 로 들어온 `$TEST` 요청이 **실물 촬영 없이 저장 이미지로 조용히**
처리된 사고가 났다. 수동 UI RUN 버튼에는 확인 팝업(`InspectionListView.xaml.cs:415~429`)이 있지만
TCP `$TEST` 경로(`SystemHandler.ProcessTest`)에는 아무 확인이 없어 핸들러/PLC 쪽에서는 알아챌 방법이 전혀 없다.

**설계 원칙: fail-safe default.** 위험한 쪽(저장 이미지 검사)은 매 기동마다 자동으로 꺼지고,
켜려면 사람이 그때그때 명시적으로 켜야 한다.

**Output:** `WPF_Example/SystemHandler.cs` 의 `Initialize()` 진입부에 강제 OFF 블록 1개. 그 외 파일 변경 0.
</objective>

<execution_context>
@$HOME/.claude/get-shit-done/workflows/execute-plan.md
@$HOME/.claude/get-shit-done/templates/summary.md
</execution_context>

<context>
@./CLAUDE.md

**코딩 규칙 (이 프로젝트 상시 규칙 — 위반 시 리뷰 반려):**
- 삼항연산자 `?:` **금지** → 반드시 `if / else`
- C# 7.2 (`nullable`, `switch expression`, `record` 등 8.0+ 문법 금지), .NET Framework 4.8
- `SystemHandler.cs` 는 **K&R** 브레이스(여는 중괄호 같은 줄, `catch (Exception ex) {`). 이 파일 스타일 그대로 간다.
- 메서드 본문 들여쓰기 = **공백 12칸**
- 로컬 변수는 이 파일 기존 컨벤션(`sw`, `prev`, `result`, `ex`) = plain camelCase. **헝가리언 접두 금지**(이 파일엔 전례 없음)
- 신규 주석은 `quick-260807-lbu:` 접두. `//YYMMDD hbk` 날짜 주석 규칙은 2026-06-11 부로 폐기 — 새로 달지 말 것

---

## 작업 시작 전 반드시 인지: 이 저장소엔 사용자의 미커밋 변경 3건이 있다

`git status --porcelain` 기준 (작업 시작 시점 baseline):

| 파일 | 내용 | 지침 |
|------|------|------|
| `WPF_Example/DatumMeasurement.csproj` | Debug\|x64 의 `DefineConstants` 에서 **`SIMUL_MODE` 제거** (`TRACE;DEBUG;SIMUL_MODE` → `TRACE;DEBUG`) = 실HW 빌드 | **절대 되돌리지 말 것.** 오히려 이 설정 덕에 `OfflineInspectMode` 분기(`#else`)가 컴파일에 실제로 포함된다 |
| `WPF_Example/Custom/Device/LightHandler.cs` | 9줄 수정 (이번 건과 무관) | **열지도 말 것** |
| `WPF_Example/SystemHandler.cs` | L128 `HOperatorSet.SetSystem("memory_allocator", "system");` 를 **주석처리** (사용자의 의도된 실험) | **주석 상태 그대로 유지.** 되돌리지도, 커밋에 넣지도 말 것 |

baseline diff 해시 (작업 후에도 동일해야 함):
```
csproj        : f0dd3a511bd51a3cc6df91c555d4336df60e0c0d
LightHandler  : 3d982f0bf0bb345f5f8103b0420c120c405b2218
SystemHandler : c3cfe91472977903dd2ed061d6b088f92f58c207   (커밋 완료 후 다시 이 값이 되어야 함 — Task 2 참조)
```

> `SystemHandler.cs` 는 **우리가 고칠 파일이면서 동시에 사용자 실험이 들어있는 파일**이다.
> 그래서 `git add WPF_Example/SystemHandler.cs` 를 그냥 하면 사용자의 `memory_allocator` 주석처리가
> 우리 커밋에 딸려 들어간다 — 그건 검증 완료된 HALCON 메모리 수정(커밋 `715f6e2`)을 무관한 커밋에서
> 조용히 되돌리는 셈이라 **금지**다. Task 2 에 이 문제를 분리하는 절차가 있다.

**`git add .` / `git add -A` / `git commit -a` 는 어떤 경우에도 금지.**

---

## 조사 결과 — 이 계획의 근거 (실행자는 아래를 다시 확인할 필요 없다)

### (1) Load() 는 언제 끝나는가 → Initialize() 는 무조건 Load 이후다

```
App 시작
 └ MainWindow ctor (MainWindow.xaml.cs:80)  mSystemHandler = SystemHandler.Handle;
     └ static SystemHandler Handle = new SystemHandler()
         └ SystemHandler private ctor (SystemHandler.cs:79)  Setting = SystemSetting.Handle;
             └ static SystemSetting Handle = new SystemSetting()
                 └ SystemSetting private ctor (SystemSetting.cs:193-195)  Load();
                     └ Load() 끝에서 AfterLoad() → RestorePcRoleDefault() 등 (Custom/SystemSetting.cs:41-48)
 └ MainWindow.xaml.cs:81  mSystemHandler.Initialize();     ← 여기서부터 우리 코드
```
즉 **Initialize() 진입 시점엔 Load()+AfterLoad() 가 이미 100% 끝나 있다.** 순서 걱정 불필요.

### (2) OfflineInspectMode 를 읽는 코드는 정확히 3곳뿐이고, 전부 Initialize() 진입부보다 뒤다

| 위치 | 언제 실행되나 |
|------|---------------|
| `Action_FAIMeasurement.cs:252` (EStep.Grab) | Sequences 생성(Initialize step 2, L152) 이후, 실제 검사 사이클에서 |
| `Action_FAIMeasurement.cs:527` (GrabOrLoadDatumImage) | 동일 |
| `InspectionListView.xaml.cs:420` (RUN 버튼 확인 팝업) | UI 생성(`InitializeComponent()`, MainWindow.xaml.cs:85) 이후 = Initialize() 완료 후 |

TCP 경로도 안전하다: `Server = new VisionServer()` 는 `Initialize()` L158, 시스템 스레드 기동은 L170 —
둘 다 우리 삽입 지점(L136 부근)보다 **뒤**다. 따라서 리셋 전에 `$TEST` 가 들어올 수 없다.

### (3) 즉시 Save 할 것인가? → **"실제로 껐을 때만" Save 한다** (근거 아래)

**Save 해야 하는 결정적 이유 — SettingWindow 가 열릴 때마다 Load() 를 다시 한다:**
```csharp
// WPF_Example/UI/Setting/SettingWindow.xaml.cs:24-26
public SettingWindow() {
    pSetting = SystemSetting.Handle;
    pSetting.Load();          // 창을 열 때마다 Setting.ini 를 통째로 다시 읽는다
```
메모리만 false 로 두면 `Setting.ini` 엔 `True` 가 남는다. 그 상태에서 사용자가 Settings 창을 열기만 해도
(취소를 눌러도) 디스크의 `True` 가 메모리로 되살아나 **시작 시 리셋이 통째로 무효화**된다.
심지어 "꺼졌는지 확인하려고" 설정 창을 여는 행동이 그 자체로 다시 켜버리는, 가장 나쁜 형태의 구멍이다.
→ 그래서 디스크까지 반영한다.

**그런데 왜 "무조건 Save" 가 아니라 "껐을 때만 Save" 인가:**
`Save()` 는 프로퍼티 전체를 INI 로 다시 쓴다. 매 기동마다 무조건 호출하면 정상 상황(이미 false)에서도
쓸데없는 전체 재기록이 생긴다. 조건부로 하면 **정상 기동의 디스크 쓰기는 정확히 0** 이고,
추가된 쓰기는 우리가 고치려는 비정상 상황에서만 딱 1회 발생한다 = 회귀 표면 최소.

**이 Save 가 새로운 위험을 만들지 않는다는 근거:**
- 앱은 이미 `Setting.Save()` 를 일상적으로 호출한다 — `LoadRecipe()` 성공 시마다(`SystemHandler.cs:232`),
  그리고 종료할 때마다(`Release()`, `SystemHandler.cs:247`). 전체 INI 재기록은 이미 매일 여러 번 일어나는 동작이다.
- 우리 호출 시점은 **Load 직후, 아무 런타임 코드도 Setting 을 건드리기 전** — 앱 생애주기 통틀어
  Save 하기에 가장 안전한 순간이다(종료 시 Save 보다 오히려 안전하다).
- `SystemSetting` 의 프로퍼티는 Int32/String/Boolean/Double 뿐이라 Load 의 per-property try/catch 가
  실제로 발동할 파싱 경로(Rect/Line/Circle)가 없다.
- 그래도 파일 잠김/권한으로 던질 수 있으니 **try/catch 로 감싼다** — 여기서 예외가 새면 `MainWindow` 생성자에서
  앱이 죽는다. 메모리 값은 이미 OFF 이므로 Save 실패해도 안전 목적은 달성돼 있다.

### (4) 채택하지 않은 대안: `AfterLoad()` 에 넣기 — **명시적으로 기각**

`Custom/SystemSetting.cs:41-48` 의 `AfterLoad()` → `RestorePcRoleDefault()` 계열은 "Load 후처리 값 보정"의
기존 관례이고, 형식만 보면 거기가 더 자연스러워 보인다. **하지만 넣으면 안 된다.**
`AfterLoad()` 는 `Load()` 가 불릴 때마다 실행되는데, `SettingWindow` 는 **열릴 때마다 `Load()` 를 호출**한다.
→ 사용자가 실행 중에 OfflineInspectMode 를 켜고 OK 를 누른 뒤, 나중에 설정 창을 다시 열면
그 순간 사용자의 ON 이 조용히 꺼져버린다. 이건 이번 작업의 명시적 제약
("실행 중 사용자가 직접 켜는 기능/UI 는 그대로 유지")을 정면으로 위반한다.
→ **반드시 `SystemHandler.Initialize()`(= 앱 시작 시 1회) 에 넣는다.**

<interfaces>
<!-- 실행자가 코드베이스를 탐색할 필요가 없도록 편집 대상 지점의 현재 코드를 그대로 옮겨둔다. -->

`WPF_Example/SystemHandler.cs` L114~148 (현재 상태, 편집 전 라인번호):

```csharp
        // Call after constructor to fully initialize runtime components.
        public void Initialize() {
            // quick-260806-dsn Part A: HALCON 자체 캐시(mimalloc, ... 생략 ...)
            //  이 메서드의 첫 실행문으로 둔다. 실패해도(캐시 힌트 실패일 뿐) 앱 시작을 막지 않는다.
            try {
                // quick-260806-dsn3: ... 생략 ...
                //HOperatorSet.SetSystem("memory_allocator", "system");     L128, 사용자 실험. 손대지 말 것
                HOperatorSet.SetSystem("global_mem_cache", "idle");
                HOperatorSet.SetSystem("temporary_mem_cache", "idle");
                HOperatorSet.SetSystem("image_cache_capacity", 0);
            }
            catch (Exception ex) {
                Logging.PrintLog((int)ELogType.Error, "[STARTUP] HALCON SetSystem memory cache config failed: {0}", ex.Message);
            }
                                                        <-- 여기에 삽입 (L136 빈 줄 자리)
            Stopwatch sw = Stopwatch.StartNew(); //260528 hbk Phase 38 #11
            long prev = 0; //260528 hbk Phase 38 #11 — 직전 단계 누적 시각 (delta 계산용)

            // 1) Light controller open
            ...
            if (Lights.Initialize() == false) {
```

관련 시그니처(이미 이 파일에서 쓰이고 있으므로 추가 using 불필요):
```csharp
// SystemHandler 필드 (L32)
public SystemSetting Setting { get; private set; }

// SystemSetting (Setting/SystemSetting.cs)
public bool OfflineInspectMode { get; set; }   // L169, [Category("System|Enviroment")]
public void Save();                            // L335
public void Load();                            // L251

// Logging 사용 예 (이 파일 내 기존 호출 그대로)
Logging.PrintLog((int)ELogType.Trace, "[SYSTEM] Initialized");
Logging.PrintLog((int)ELogType.Error, "[STARTUP] ... failed: {0}", ex.Message);
```

빌드 환경(planner 실측 확인):
- MSBuild: `C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe` (v18.7.1.23011)
- Git Bash 에서는 `//p:` 가 깨지므로 **`-p:` / `-t:` 대시 프리픽스**를 쓴다
- 빌드에 1~2분 걸릴 수 있으니 Bash 툴 `timeout` 을 `300000` 으로 준다
- 현재 `Setting.ini` 실측값: `WPF_Example/bin/x64/Debug/Setting.ini` L64 `OfflineInspectMode=False`
  (사용자가 사고 후 이미 수동으로 꺼둔 상태 — 그래서 그냥 재시작하면 로그가 안 남는다. Task 3 절차 참조)
</interfaces>
</context>

<tasks>

<task type="auto">
  <name>Task 1: Initialize() 진입부에 OfflineInspectMode 강제 OFF 블록 추가 + Debug/x64 빌드</name>
  <files>WPF_Example/SystemHandler.cs</files>
  <action>
`WPF_Example/SystemHandler.cs` **한 파일만** 수정한다.

**삽입 위치:** HALCON `SetSystem` try/catch 블록이 **끝난 직후**, `Stopwatch sw = Stopwatch.StartNew();` **직전**.

그 HALCON 블록은 자기 주석에 "이 메서드의 첫 실행문으로 둔다" 라고 못박혀 있으므로 **밀어내지 말 것.**
우리 블록은 그 바로 다음에 온다. 그래도 `Lights.Initialize()`(L144), `Sequences`(L152), `VisionServer`(L158),
시스템 스레드 기동(L170) 보다 한참 앞이라 "값을 읽는 코드보다 먼저" 조건은 충분히 만족한다.

**Edit 앵커** (라인번호로 찾지 말고 아래 3줄 블록으로 앵커링할 것 — 파일에서 유일하다):
```
            }

            Stopwatch sw = Stopwatch.StartNew(); //260528 hbk Phase 38 #11
```
결과적으로 `catch 닫는 }` → 빈 줄 → **우리 블록** → 빈 줄 → `Stopwatch sw = ...` 순서가 되면 된다.

**삽입할 코드 (그대로. 들여쓰기 공백 12칸, K&R):**

```csharp
            // quick-260807-lbu: OfflineInspectMode 는 레시피가 아니라 SystemSetting(시스템 전역·영속)이라
            //  한 번 켜두면 앱을 껐다 켜도 켜진 채로 시작한다. 그 상태에서는 실물 촬영 없이 저장 이미지로
            //  검사가 돌아가는데(Action_FAIMeasurement 의 EStep.Grab / GrabOrLoadDatumImage), UI RUN 버튼과 달리
            //  TCP $TEST 경로에는 확인 팝업이 없어 핸들러/PLC 쪽에서 알아챌 방법이 전혀 없다 — 실제 사고 발생.
            //  시작할 때는 무조건 OFF 로 둔다. 켜는 것은 실행 중 Settings 창에서 사용자가 직접 할 때만 허용(그 경로 무변경).
            //  Setting.Load() 는 생성자(Setting = SystemSetting.Handle)에서 이미 끝났고, 이 값을 읽는 코드는
            //  전부 이 시점보다 뒤(Sequences step 2, VisionServer step 3, UI)라 여기가 안전한 지점이다.
            if (Setting.OfflineInspectMode) {
                Setting.OfflineInspectMode = false;
                Logging.PrintLog((int)ELogType.Trace, "[STARTUP] OfflineInspectMode was ON in Setting.ini - forced OFF at startup.");
                // 메모리만 끄면 Setting.ini 엔 True 가 남는다. 그러면 Settings 창을 여는 순간 생성자의
                //  pSetting.Load()(SettingWindow.xaml.cs:26)가 디스크의 True 를 다시 읽어 조용히 되살린다.
                //  그래서 실제로 껐을 때만 디스크까지 반영한다(이미 false 인 정상 기동에는 디스크 쓰기 0).
                //  Save 실패(파일 잠김/권한)해도 메모리는 이미 OFF 이므로 앱 시작을 막지 않는다.
                try {
                    Setting.Save();
                }
                catch (Exception ex) {
                    Logging.PrintLog((int)ELogType.Error, "[STARTUP] OfflineInspectMode reset save failed: {0}", ex.Message);
                }
            }
```

**절대 하지 말 것:**
- L128 `//HOperatorSet.SetSystem("memory_allocator", "system");` — 주석 상태 그대로 둔다. 되돌리지 말 것.
- `WPF_Example/Setting/SystemSetting.cs`, `WPF_Example/Custom/SystemSetting.cs`(`AfterLoad`),
  `WPF_Example/UI/Setting/SettingWindow.xaml.cs`, `WPF_Example/UI/ControlItem/InspectionListView.xaml.cs`,
  `WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs` — **열지도 말 것.** 이번 변경 대상 아님.
- 로그 문자열에 비ASCII 화살표/대시를 넣지 말 것(로그 파일 인코딩 안전). 주석은 한글 그대로 OK.
- 삼항연산자 금지. `else` 절 추가 금지(이미 false 면 아무것도 안 하는 게 맞다).
- 버전 번호(`VersionDefine.cs`) 손대지 말 것 — 이 프로젝트는 quick 여러 건을 모아 별도 `chore(version)` 커밋으로 올린다.

**빌드.** 현재 이 PC 에 `DatumMeasurement.exe` 가 실행 중이면 산출물 잠금(`MSB3021/3026/3027/3030`)이 날 수 있다.
그럴 때 **프로세스를 절대 죽이지 말고**(이 프로젝트 하드 규칙),
`-p:OutputPath="$TEMP/gsd-lbu-scratch/bin/" -p:BaseIntermediateOutputPath="$TEMP/gsd-lbu-scratch/obj/"` 로
컴파일만 재확인한 뒤 SUMMARY 에 "산출물 잠김으로 스크래치 컴파일 검증" 이라고 남긴다.
  </action>
  <verify>
    <automated>cd "C:/code/DataMeasurement" && F=WPF_Example/SystemHandler.cs && CODE=$(grep -v '^[[:space:]]*//' "$F") && echo "=== [1] if 가드 : 1 기대 ===" && (echo "$CODE" | grep -c 'if (Setting.OfflineInspectMode) {' || echo 0) && echo "=== [2] false 대입 : 1 기대 ===" && (echo "$CODE" | grep -c 'Setting.OfflineInspectMode = false;' || echo 0) && echo "=== [3] Setting.Save() : 3 기대 (기존 LoadRecipe+Release 2 + 신규 1) ===" && (echo "$CODE" | grep -c 'Setting.Save();' || echo 0) && echo "=== [4] Save 실패 로그(try/catch 증거) : 1 기대 ===" && (echo "$CODE" | grep -c 'OfflineInspectMode reset save failed' || echo 0) && echo "=== [5] 강제OFF Trace 로그 : 1 기대 ===" && (echo "$CODE" | grep -c 'forced OFF at startup' || echo 0) && echo "=== [6] 신규블록 삼항연산자 : 0 기대 ===" && (echo "$CODE" | grep -c 'OfflineInspectMode.*?.*:' || echo 0) && echo "=== [7] 사용자 실험 보존 : 코드 0 / 주석 1 기대 ===" && (echo "$CODE" | grep -c 'memory_allocator' || echo 0) && (grep -c '^[[:space:]]*//HOperatorSet.SetSystem("memory_allocator", "system");' "$F" || echo 0) && echo "=== [8] 삽입 순서 : SetSystem < 리셋 < Stopwatch < Lights ===" && L_SYS=$(grep -n 'image_cache_capacity' "$F" | head -1 | cut -d: -f1) && L_RST=$(grep -n 'Setting.OfflineInspectMode = false;' "$F" | head -1 | cut -d: -f1) && L_SW=$(grep -n 'Stopwatch sw = Stopwatch.StartNew' "$F" | head -1 | cut -d: -f1) && L_LT=$(grep -n 'Lights.Initialize() == false' "$F" | head -1 | cut -d: -f1) && echo "SetSystem=$L_SYS reset=$L_RST stopwatch=$L_SW lights=$L_LT" && (if [ "$L_SYS" -lt "$L_RST" ] && [ "$L_RST" -lt "$L_SW" ] && [ "$L_SW" -lt "$L_LT" ]; then echo ORDER_OK; else echo ORDER_FAIL; fi) && echo "=== [9] 무변경이어야 할 5개 파일 : 빈 출력 기대 ===" && git status --porcelain -- WPF_Example/Setting/SystemSetting.cs WPF_Example/Custom/SystemSetting.cs WPF_Example/UI/Setting/SettingWindow.xaml.cs WPF_Example/UI/ControlItem/InspectionListView.xaml.cs WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs && echo "=== [10] 미커밋 실험 2건 baseline 해시 ===" && (git diff -- WPF_Example/DatumMeasurement.csproj | git hash-object --stdin) && (git diff -- WPF_Example/Custom/Device/LightHandler.cs 2>/dev/null | git hash-object --stdin) && echo "=== [11] 변경 파일 목록 : 정확히 3개 기대 ===" && git status --porcelain && echo "=== [12] Debug/x64 Rebuild ===" && "/c/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" "WPF_Example/DatumMeasurement.csproj" -t:Rebuild -p:Configuration=Debug -p:Platform=x64 -v:minimal -nologo 2>&1 | grep -iE "error CS|error MSB|warning CS|Build succeeded" | head -25</automated>
  </verify>
  <done>
- [1] `1`, [2] `1`, [3] `3`, [4] `1`, [5] `1`, [6] `0`, [7] `0` 그리고 `1` — 전부 정확히 일치.
- [8] `ORDER_OK` (SetSystem 블록 → 우리 리셋 → Stopwatch → Lights.Initialize 순서).
- [9] 빈 출력 (5개 파일 무변경).
- [10] 해시가 순서대로 `f0dd3a511bd51a3cc6df91c555d4336df60e0c0d` / `3d982f0bf0bb345f5f8103b0420c120c405b2218` — baseline 과 동일.
- [11] 변경 파일 정확히 3개: `WPF_Example/Custom/Device/LightHandler.cs`, `WPF_Example/DatumMeasurement.csproj`, `WPF_Example/SystemHandler.cs`. 새 파일 등장 0.
- [12] `Build succeeded`, 신규 `error CS` 0건 / 신규 `warning CS` 0건
  (기존부터 있던 `CS0618`/`CS0162` 류 경고 재등장은 무관). 산출물 잠김이면 스크래치 OutDir 컴파일 성공으로 대체.
  </done>
</task>

<task type="auto">
  <name>Task 2: 사용자 실험을 분리한 채 SystemHandler.cs 만 커밋</name>
  <files>WPF_Example/SystemHandler.cs (일시 토글 후 원복 — 최종 내용은 Task 1 결과와 동일)</files>
  <action>
문제: `git add WPF_Example/SystemHandler.cs` 를 그냥 하면 **같은 파일에 들어있는 사용자의 미커밋 실험**
(L128 `memory_allocator` 주석처리)까지 우리 커밋에 딸려 들어간다. 그건 검증 완료된 HALCON 메모리 수정
(커밋 `715f6e2`)을 OfflineInspectMode 커밋에서 조용히 되돌리는 셈이라 허용 불가.

아래 **6단계를 순서대로** 수행한다. Edit 툴과 git 명령만 쓴다.
`git checkout` / `git restore` / `git stash` 는 **어떤 경우에도 쓰지 말 것** (Task 1 결과가 날아간다).

**(1) 사용자 실험을 일시적으로 HEAD 상태(주석 해제)로 되돌린다**

Edit — old: `                //HOperatorSet.SetSystem("memory_allocator", "system");`
      new: `                HOperatorSet.SetSystem("memory_allocator", "system");`

(앞 공백 16칸 유지. 이 문자열은 파일에서 유일하다.)

**(2) 스테이징 + 인덱스 내용 검증**
```
git add WPF_Example/SystemHandler.cs
git diff --cached -- WPF_Example/SystemHandler.cs
```
스테이징된 diff 에 `memory_allocator` 가 **한 번도 안 나와야** 하고, `OfflineInspectMode` 블록 추가만 있어야 한다.
안 그러면 **커밋하지 말고 멈춘 뒤** (1)을 다시 확인한다.

**(3) 커밋**
```
git commit -m "$(cat <<'EOF'
fix(quick-260807-lbu): 앱 시작 시 OfflineInspectMode 강제 OFF

OfflineInspectMode 는 SystemSetting(시스템 전역/영속) 이라 한 번 켜두면 앱을 껐다 켜도
켜진 채 시작한다. 그 상태에서는 실물 촬영 없이 저장 이미지로 검사가 도는데, UI RUN 버튼과 달리
TCP TEST 경로에는 확인 팝업이 없어 핸들러/PLC 쪽에서 알아챌 방법이 없다(실제 사고 발생).

SystemHandler.Initialize() 진입부(Load 완료 이후, Sequences/VisionServer/UI 생성 이전)에서
무조건 false 로 리셋한다. 실제로 켜져 있던 경우에만 Setting.ini 에도 즉시 반영하는데,
SettingWindow 는 열릴 때마다 Load() 를 다시 하므로(SettingWindow.xaml.cs:26) 메모리만 끄면
설정 창을 여는 순간 디스크의 True 가 되살아나기 때문이다. Save 는 try/catch 로 감싸
실패해도 앱 시작을 막지 않는다.

실행 중 Settings 창에서 사용자가 직접 켜는 기존 동작은 무변경.

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
EOF
)"
```

**(4) 사용자 실험 원복 (필수 — 빠뜨리면 사용자 워킹트리 상태가 훼손된다)**

Edit — old: `                HOperatorSet.SetSystem("memory_allocator", "system");`
      new: `                //HOperatorSet.SetSystem("memory_allocator", "system");`

**(5) 원복 검증**
`git diff -- WPF_Example/SystemHandler.cs | git hash-object --stdin` 이
**`c3cfe91472977903dd2ed061d6b088f92f58c207`** (작업 시작 시점 baseline)와 같아야 한다.
우리가 삽입한 블록은 L136 부근이고 이 hunk 는 L125~131 이라 hunk 헤더가 안 밀리므로 해시가 그대로여야 한다.
만약 해시가 다르면 즉시 실패로 보지 말고 `git diff -- WPF_Example/SystemHandler.cs` 원문을 확인해
**hunk 가 정확히 1개이고, `-` 1줄 / `+` 1줄이 둘 다 `memory_allocator` 줄인지**로 대체 판정한다
(그것만 만족하면 실질 동등 — SUMMARY 에 해시 불일치 사유를 기록).

**(6) 커밋 내용 최종 확인**
`git show --stat HEAD` 의 파일 목록이 `WPF_Example/SystemHandler.cs` **한 줄뿐**이어야 한다.
  </action>
  <verify>
    <automated>cd "C:/code/DataMeasurement" && echo "=== [1] 커밋 파일 목록 : SystemHandler.cs 한 줄 기대 ===" && git show --stat --format="" HEAD && echo "=== [2] 커밋 diff 에 memory_allocator : 0 기대 ===" && (git show HEAD -- WPF_Example/SystemHandler.cs | grep -c 'memory_allocator' || echo 0) && echo "=== [3] 커밋 diff 에 리셋 추가줄 : 1 기대 ===" && (git show HEAD -- WPF_Example/SystemHandler.cs | grep -c '^+.*Setting.OfflineInspectMode = false;' || echo 0) && echo "=== [4] 워킹트리 실험 원복(주석상태) : 1 기대 ===" && (grep -c '^[[:space:]]*//HOperatorSet.SetSystem("memory_allocator", "system");' WPF_Example/SystemHandler.cs || echo 0) && echo "=== [5] SystemHandler diff 해시 : c3cfe91472977903dd2ed061d6b088f92f58c207 기대 ===" && (git diff -- WPF_Example/SystemHandler.cs | git hash-object --stdin) && echo "=== [6] SystemHandler diff 원문 (hunk 1개 기대) ===" && git diff -- WPF_Example/SystemHandler.cs && echo "=== [7] 나머지 실험 2건 baseline 유지 ===" && (git diff -- WPF_Example/DatumMeasurement.csproj | git hash-object --stdin) && (git diff -- WPF_Example/Custom/Device/LightHandler.cs 2>/dev/null | git hash-object --stdin) && echo "=== [8] 커밋 후 워킹트리 : 3개 파일 M 기대 ===" && git status --porcelain</automated>
  </verify>
  <done>
- [1] `WPF_Example/SystemHandler.cs` 한 줄만.
- [2] `0`, [3] `1`, [4] `1`.
- [5] `c3cfe91472977903dd2ed061d6b088f92f58c207` (또는 [6] 원문이 memory_allocator 1 hunk 뿐임을 확인).
- [7] `f0dd3a511bd51a3cc6df91c555d4336df60e0c0d` / `3d982f0bf0bb345f5f8103b0420c120c405b2218`.
- [8] `M` 3개: `LightHandler.cs`, `DatumMeasurement.csproj`, `SystemHandler.cs`. 커밋 1건 생성됨.
  </done>
</task>

<task type="checkpoint:human-verify" gate="blocking">
  <name>Task 3: 실기 확인 — 재시작하면 꺼져 있고, 실행 중 켜는 건 그대로 되는지</name>
  <files>(코드 변경 없음 — 사용자 확인 전용)</files>
  <action>
여기서 **멈추고** 아래 `<how-to-verify>` 를 사용자에게 그대로 제시한 뒤 응답을 기다린다.
executor 가 대신 판정하지 말 것 — 앱을 껐다 켜는 건 사람만 할 수 있다.

이 프로젝트의 하드 규칙을 지킨다: **빌드 산출물이 잠겨 있어도 프로세스를 강제 종료하지 않는다.**
사용자가 직접 앱을 껐다 켜도록 안내한다.

사용자가 문제를 보고하면 수정 후 이 체크포인트를 다시 제시한다. "승인" 을 받은 뒤에 SUMMARY 를 작성한다.
  </action>
  <what-built>
프로그램을 새로 켤 때마다 **"OfflineInspectMode"(저장된 사진으로 검사하는 모드)를 자동으로 꺼짐 상태로 시작**하게 만들었습니다.

- 전에는: 한 번 켜두면 프로그램을 껐다 켜도 계속 켜진 채로 남아 있었습니다. 그래서 오늘처럼
  실제 부품을 놓고 검사를 걸어도 카메라로 안 찍고 **예전에 저장해둔 사진**으로 조용히 검사가 돌 수 있었습니다.
  화면에도 로그에도 표시가 없어서 알아채기 어려웠습니다.
- 이제는: 프로그램을 새로 켜면 **무조건 꺼진 상태**로 시작합니다.
- **켜는 방법은 그대로입니다.** 필요할 때 설정 창에서 직접 체크하면 그 순간부터 켜집니다.
  다만 프로그램을 껐다 켜면 다시 꺼집니다.
- 켜져 있어서 자동으로 껐을 때는 로그에 한 줄 남겨서, 나중에 "그때 켜져 있었구나" 를 확인할 수 있게 했습니다.
  </what-built>
  <how-to-verify>
아래를 순서대로 해주세요. 지금 설정 파일에는 이미 꺼짐(False)으로 되어 있어서,
**먼저 일부러 켜야** 이번 수정이 동작하는 걸 볼 수 있습니다.

**A. 실행 중에 켜는 게 여전히 되는지 (기존 기능 유지 확인)**
1. 프로그램 실행 → 설정(Setting) 창 열기 → `System|Enviroment` 그룹의 `OfflineInspectMode` 를 **체크(켜기)** → 확인(OK)
2. 설정 창을 **다시 열어봅니다.**
   → **여전히 켜져 있어야 정상입니다.** (실행 중에 켠 건 그대로 유지된다는 뜻)
   ※ 여기서 저절로 꺼져 있으면 **실패**입니다. 알려주세요.

**B. 껐다 켜면 자동으로 꺼지는지 (이번 수정의 핵심)**
3. 프로그램을 **정상 종료**합니다 (X 버튼 → 종료 확인).
4. 프로그램을 **다시 실행**합니다.
5. 설정 창을 열어 `OfflineInspectMode` 를 봅니다.
   → **꺼져 있으면 성공입니다.**

**C. 설정 파일에도 반영됐는지**
6. 메모장으로 `C:\code\DataMeasurement\WPF_Example\bin\x64\Debug\Setting.ini` 를 엽니다.
   (실제로 쓰시는 실행 폴더가 다르면 그 폴더의 `Setting.ini`)
7. `OfflineInspectMode=False` 로 되어 있으면 성공입니다.
   ※ 여기가 `True` 로 남아 있으면, 설정 창을 여는 순간 다시 켜져버리므로 **실패**입니다.

**D. 로그에 흔적이 남았는지**
8. `D:\Data\Trace\` 에서 오늘 날짜 로그 파일을 열고 `OfflineInspectMode` 로 검색합니다.
9. `[STARTUP] OfflineInspectMode was ON in Setting.ini - forced OFF at startup.` 이 한 줄 있으면 성공입니다.

**E. RUN 버튼 확인 팝업이 그대로인지 (건드리지 않았다는 확인)**
10. 다시 설정 창에서 `OfflineInspectMode` 를 **켠 다음**, 검사 목록에서 RUN 버튼을 눌러봅니다.
11. 기존처럼 **"오프라인 검사 모드" 확인 팝업**이 뜨면 정상입니다. 취소를 누르면 검사가 시작되지 않습니다.
12. 확인이 끝나면 설정 창에서 다시 **꺼주세요** (또는 그냥 프로그램을 껐다 켜면 자동으로 꺼집니다).

**F. (선택) 실제 촬영이 되는지 최종 확인**
13. 꺼진 상태에서 검사를 한 번 돌려, 조명이 켜지고 카메라가 실제로 찍는지 눈으로 확인합니다.

---
A~E 가 전부 위에 적힌 대로 나오면 **"승인"** 이라고 알려주세요.
하나라도 다르면 **몇 번 항목이 어떻게 달랐는지** 알려주시면 바로 고치겠습니다.
  </how-to-verify>
  <resume-signal>"승인" 또는 다르게 나온 항목 번호와 증상</resume-signal>
  <done>
사용자가 A~E 전 항목 확인 후 "승인" 응답. 특히:
- A-2 에서 켠 상태가 유지됨 (실행 중 수동 ON 경로 무회귀)
- B-5 에서 재시작 후 꺼져 있음 (이번 수정의 핵심)
- C-7 에서 `Setting.ini` 가 `False` (SettingWindow Load 되살아남 구멍 차단 확인)
- D-9 에서 Trace 로그 1줄 존재
- E-11 에서 기존 RUN 확인 팝업 정상 (UI 로직 무변경)
  </done>
</task>

</tasks>

<threat_model>
## Trust Boundaries

| Boundary | Description |
|----------|-------------|
| 외부 핸들러/PLC → TCP `$TEST` (VisionServer) | 외부에서 들어온 검사 요청. 검사 결과 P/F 가 실제 부품 상태를 반영한다는 **암묵적 신뢰**가 여기서 성립해야 한다 |
| 영속 설정 파일 `Setting.ini` → 런타임 검사 동작 | 이전 세션(혹은 사람의 셋업 작업)이 남긴 상태가 다음 세션의 검사 의미를 조용히 바꾼다 |

## STRIDE Threat Register

| Threat ID | Category | Component | Disposition | Mitigation Plan |
|-----------|----------|-----------|-------------|-----------------|
| T-lbu-01 | Spoofing (결과 위조) | `Action_FAIMeasurement` EStep.Grab / GrabOrLoadDatumImage — 이전 세션에서 켜진 채 남은 `OfflineInspectMode` 로 인해 실물 아닌 저장 이미지가 "이번 부품의 측정 결과"로 PLC 에 보고됨 | mitigate | `SystemHandler.Initialize()` 에서 매 기동 시 무조건 `OfflineInspectMode=false` (fail-safe default). 이 계획의 본체 |
| T-lbu-02 | Tampering (상태 되살아남) | `SettingWindow` 생성자의 `pSetting.Load()` — 디스크에 남은 `True` 가 설정 창을 여는 것만으로 메모리로 복귀해 T-lbu-01 을 재개통 | mitigate | 리셋이 실제 발생한 경우 `Setting.Save()` 로 디스크까지 즉시 동기화. Task 3 C-7 로 실측 검증 |
| T-lbu-03 | Denial of Service (시작 차단) | 신규 `Setting.Save()` 가 파일 잠김/권한으로 throw → `MainWindow` 생성자에서 앱 기동 실패 | mitigate | `try/catch (Exception ex)` + Error 로그. 메모리 값은 이미 OFF 이므로 Save 실패해도 T-lbu-01 은 이미 차단됨 |
| T-lbu-04 | Repudiation (사후 추적 불가) | 저장 이미지로 검사된 사이클이 있었는지 나중에 확인할 수단 부재 | mitigate | 리셋 발생 시 `[STARTUP] OfflineInspectMode was ON in Setting.ini` Trace 로그 1줄 |
| T-lbu-05 | Tampering (무관 변경 유입) | 같은 파일의 사용자 미커밋 실험(`memory_allocator` 주석처리)이 커밋에 섞여 검증된 HALCON 메모리 수정을 조용히 되돌림 | mitigate | Task 2 의 스테이징 분리 절차 + 커밋 diff `memory_allocator` 0건 게이트 + 원복 해시 검증 |
| T-lbu-SC | Tampering (공급망) | 패키지 설치 | n/a | 이번 작업은 신규 패키지 설치 0건 (`packages.config` 무변경) |
</threat_model>

<verification>
- Task 1 자동검증 [1]~[12] 전 항목 기대값 일치 + `Build succeeded`
- Task 2 자동검증 [1]~[8] 전 항목 기대값 일치, 커밋 1건 (`SystemHandler.cs` 단일 파일)
- Task 3 사용자 실기 승인 (A~E)
- 최종 `git status --porcelain` = 사용자 미커밋 실험 3건만 `M` 으로 남음
</verification>

<success_criteria>
1. `SystemHandler.Initialize()` 가 HALCON `SetSystem` 블록 직후 / `Stopwatch` 직전에서 `OfflineInspectMode` 를 false 로 강제하고, 켜져 있던 경우에만 `Setting.Save()` 를 try/catch 로 1회 호출한다.
2. `SystemSetting.cs` / `Custom/SystemSetting.cs`(`AfterLoad`) / `SettingWindow.xaml.cs` / `InspectionListView.xaml.cs` / `Action_FAIMeasurement.cs` 무변경 — 실행 중 사용자가 직접 켜는 경로와 RUN 확인 팝업은 100% 그대로.
3. Debug/x64 빌드 통과 (신규 error/warning 0).
4. 커밋에 `WPF_Example/SystemHandler.cs` 한 파일만 포함되고, 사용자 미커밋 실험 3건이 워킹트리에 baseline 그대로 남는다.
5. 사용자 실기 확인 A~E 승인.
</success_criteria>

<output>
Create `.planning/quick/260807-lbu-offlineinspectmode-off/260807-lbu-SUMMARY.md` when done.

SUMMARY 에 반드시 남길 것:
- Save 여부 결정의 근거 요약 (SettingWindow 생성자 `Load()` 구멍 → 조건부 Save 채택, `AfterLoad()` 대안 기각 사유)
- 빌드가 정상 경로였는지 스크래치 OutDir 였는지
- Task 2 (5) 해시 검증 결과 (일치 / 대체 판정)
- Task 3 사용자 승인 내용
</output>
