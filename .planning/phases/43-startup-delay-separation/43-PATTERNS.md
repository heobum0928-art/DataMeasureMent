# Phase 43: 시작지연 분리 - Pattern Map

**Mapped:** 2026-06-15
**Files analyzed:** 4 (수정 대상 파일)
**Analogs found:** 4 / 4

## File Classification

| 수정 대상 파일 | Role | Data Flow | Closest Analog | Match Quality |
|---|---|---|---|---|
| `WPF_Example/SystemHandler.cs` §Initialize() | service (orchestrator) | request-response (init sequencing) | `WPF_Example/SystemHandler.cs` §Initialize() 자체 (기존 Step 구조 확장) | self-extend |
| `WPF_Example/Login/LoginManager.cs` | service (singleton) | event-driven (background preload + readiness signal) | `WPF_Example/Utility/RawImageSaveService.cs` | exact (volatile flag + Thread + Start()) |
| `WPF_Example/Custom/SystemHandler.cs` §MainRun() | controller (dispatcher loop) | event-driven (TCP packet routing) | 동일 파일 (readiness guard 추가) | self-extend |
| `WPF_Example/UI/Login/LoginWindow.xaml.cs` §LoginWindow() | component (UI entry) | request-response (preload wait + login) | `WPF_Example/UI/Login/LoginWindow.xaml.cs` 자체 | self-extend |

---

## Pattern Assignments

### `WPF_Example/SystemHandler.cs` §Initialize() (service, init sequencing)

**Analog:** 동일 파일 — 기존 Step1~8 구조를 그대로 유지하며 Step 5 LoginManager 동기 접근을 제거하고 백그라운드 프리로드로 교체. READY 마커를 Step 7 CollectRecipe 완료 직후 삽입.

**[STARTUP] 계측 인프라 패턴** (lines 110-175):
```csharp
Stopwatch sw = Stopwatch.StartNew(); //260528 hbk Phase 38 #11
long prev = 0;                        //260528 hbk Phase 38 #11

// Step N 완료 후 동일 형식으로 로그 출력
Logging.PrintLog((int)ELogType.Trace,
    "[STARTUP] Step N Label: {0} ms (cumulative), delta {1} ms",
    sw.ElapsedMilliseconds, sw.ElapsedMilliseconds - prev); //260528 hbk Phase 38 #11
prev = sw.ElapsedMilliseconds; //260528 hbk Phase 38 #11
```

**기존 Step 5 LoginManager 동기 접근 패턴 (제거 대상)** (lines 149-152):
```csharp
// 5) Login manager
Login = LoginManager.Handle;   // <-- 이 라인이 808ms 소요: 백그라운드로 이동
Logging.PrintLog((int)ELogType.Trace,
    "[STARTUP] Step 5 LoginManager: {0} ms (cumulative), delta {1} ms",
    sw.ElapsedMilliseconds, sw.ElapsedMilliseconds - prev);
prev = sw.ElapsedMilliseconds;
```

**교체 패턴 (백그라운드 프리로드 기동):**
```csharp
// 5) Login manager — background preload (측정 임계 경로 외부)
//260615 hbk Phase 43: D-03 — 동기 접근 제거 → 백그라운드 프리로드로 교체
Login = LoginManager.Handle;           // Handle getter 호출만(인스턴스 취득); 실제 Load()는 Preload()에서
LoginManager.Handle.Preload();         // 백그라운드 Thread 기동 (내부에서 _isStarted guard)
Logging.PrintLog((int)ELogType.Trace,
    "[STARTUP] Step 5 LoginManager preload started: {0} ms (cumulative), delta {1} ms",
    sw.ElapsedMilliseconds, sw.ElapsedMilliseconds - prev); //260615 hbk Phase 43
prev = sw.ElapsedMilliseconds;
```

**[STARTUP] READY 마커 삽입 위치 (D-02):**  
삽입 위치 = Step 7 CollectRecipe 완료 직후, Step 8 Localize 직전.  
이 시점에서 recipe 스캔 완료 + SystemThread(Step 4) alive + Sequences(Step 2) 구성 완료 → 첫 $TEST 수용 가능.

```csharp
// 7) Collect recipe list
Recipes.CollectRecipe();
Logging.PrintLog((int)ELogType.Trace,
    "[STARTUP] Step 7 CollectRecipe: {0} ms (cumulative), delta {1} ms",
    sw.ElapsedMilliseconds, sw.ElapsedMilliseconds - prev); //260528 hbk Phase 38 #11
prev = sw.ElapsedMilliseconds;

// READY: recipe ready + seq thread alive = 첫 $TEST 수용 가능 시점 (D-01/D-02)
//260615 hbk Phase 43: [STARTUP] READY 마커 — Before/After 비교 단일 기준 지표
Logging.PrintLog((int)ELogType.Trace,
    "[STARTUP] READY: {0} ms", sw.ElapsedMilliseconds);
```

---

### `WPF_Example/Login/LoginManager.cs` (service, event-driven background preload)

**Analog:** `WPF_Example/Utility/RawImageSaveService.cs` — volatile flag + Thread + Start() + AutoResetEvent 패턴을 LoginManager 프리로드에 적용.

**RawImageSaveService 백그라운드 워커 패턴** (lines 25-44):
```csharp
// 필드 선언 패턴
private readonly Thread _workerThread;
private volatile bool _isStopping;
private volatile bool _isStarted;

// 생성자에서 Thread 객체만 생성, Start 는 별도 메서드
public RawImageSaveService() {
    _workerThread = new Thread(WorkLoop) {
        IsBackground = true,
        Name = "RawImageSaveService",
        Priority = ThreadPriority.BelowNormal
    };
}

// Start(): isStarted guard 로 이중 기동 방지
public void Start() {
    if (!_isStarted) {
        _workerThread.Start();
        _isStarted = true;
    }
}
```

**CaptureImageSaveService Start() guard 패턴** (lines 95-100):
```csharp
public void Start() {
    if (!_isStarted) {
        _workerThread.Start();
        _isStarted = true;
    }
}
```

**LoginManager에 추가할 프리로드 패턴 (신규):**
```csharp
// 필드 (기존 private 필드들 아래에 추가)
private readonly Thread _preloadThread;
private volatile bool _isPreloaded;  // Load() 완료 여부 신호

// 생성자: Load() 를 동기 호출하는 대신 Thread 객체만 준비
private LoginManager() {
    ACCOUNT_FILE = AppDomain.CurrentDomain.BaseDirectory + @"account.db";
    // Load()는 Preload() 호출로 백그라운드에서 수행
    _preloadThread = new Thread(PreloadWorker) {
        IsBackground = true,
        Name = "LoginManagerPreload",
        Priority = ThreadPriority.BelowNormal
    };
}

/// <summary>백그라운드에서 account.db 를 읽어 AccountList 를 준비한다.
/// SystemHandler.Initialize() Step 5 에서 한 번 호출. 이중 호출 무해.</summary>
public void Preload() {
    if (!_isPreloaded && !_preloadThread.IsAlive) {
        _preloadThread.Start();
    }
}

private void PreloadWorker() {
    if (!Load()) {
        // 오류 또는 파일 없음 — Load() 내부에서 기본 admin 추가
    }
    _isPreloaded = true; //260615 hbk Phase 43: 완료 신호 (LoginWindow 에서 확인)
    Logging.PrintLog((int)ELogType.Trace, "[LOGIN] Preload complete");
}

// LoginWindow 가 호출 — 프리로드 미완 시 완료까지 대기 (D-04: 로그인 지연 0 목표)
public void EnsureLoaded() {
    if (_isPreloaded) return;
    _preloadThread.Join(); // 이미 완료됐으면 즉시 반환
}
```

**기존 LoginManager 싱글턴 Handle getter 패턴** (line 88):
```csharp
public static LoginManager Handle { get; } = new LoginManager();
```
참고: Handle 첫 접근 시 생성자 실행. Phase 43 이후 생성자에서 Load()를 제거하고 Preload()로 분리.

**기존 Load() 메서드 시그니처** (lines 164-208):
```csharp
public bool Load() {
    if (File.Exists(ACCOUNT_FILE) == false) {
        if (CountOf(EAccountGrade.Admin) == 0) {
            AccountList.Add(new AccountInfo(DEFAULT_ADMIN_ID, EAccountGrade.Admin, DEFAULT_ADMIN_PASSWORD));
        }
        return false;
    }
    // AES decrypt → JsonConvert.DeserializeObject → AccountList 갱신
    // ...
    return true;
}
```
Load() 자체는 변경 없음. PreloadWorker()에서 호출하는 대상.

---

### `WPF_Example/Custom/SystemHandler.cs` §MainRun() (controller, event-driven)

**Analog:** 동일 파일 — `case VisionRequestType.Test:` 블록에 readiness guard 추가.

**기존 ProcessTest 디스패치 패턴** (lines 51-57):
```csharp
case VisionRequestType.Test:
    if (Setting.AutoLogoutWhenRecvTest && Login.IsLogin) { Login.LogOut(); }

    if (!ProcessTest(packet.AsTest())) {
        Logging.PrintLog((int)ELogType.Error,
            "Client {0} : Fail to Start Sequence. sender:{1}, identifier:{2}",
            i, packet.Sender, packet.Identifier);
        responsePacket = SendTestError(packet.AsTest());
    }
    break;
```

**Phase 43 추가 패턴 — readiness guard (D-10: race 없음 보장):**  
Sequences(Step 2) + SystemThread(Step 4) + CollectRecipe(Step 7) 완료 후 mSystemThread가 MainRun 루프에 진입하므로, 실제 $TEST 수신 시점에는 이미 recipe/seq 준비 완료 상태. 추가 guard는 LoginManager 프리로드 미완 여부만 확인하면 됨.

```csharp
case VisionRequestType.Test:
    //260615 hbk Phase 43: D-10 — LoginManager 프리로드 미완 시에도 TEST 자체는 차단하지 않음
    //  Login.IsLogin 체크(AutoLogout 분기)는 _isPreloaded 여부와 무관하게 동작
    //  (IsLogin 기본값 false → AutoLogout 조건 불충족 → 안전 통과)
    if (Setting.AutoLogoutWhenRecvTest && Login.IsLogin) { Login.LogOut(); }

    if (!ProcessTest(packet.AsTest())) {
        Logging.PrintLog((int)ELogType.Error,
            "Client {0} : Fail to Start Sequence. sender:{1}, identifier:{2}",
            i, packet.Sender, packet.Identifier);
        responsePacket = SendTestError(packet.AsTest());
    }
    break;
```

참고: `Login.IsLogin` 은 `_isPreloaded` 이전에는 기본값 `false` 이므로 AutoLogout 분기가 안전하게 통과됨. 별도 guard 불필요.

---

### `WPF_Example/UI/Login/LoginWindow.xaml.cs` (component, request-response)

**Analog:** 동일 파일 — 생성자에서 `SystemHandler.Handle.Login` 취득 후 `EnsureLoaded()` 호출 추가.

**기존 LoginWindow 생성자 패턴** (lines 18-24):
```csharp
public LoginWindow() {
    InitializeComponent();
    Login = SystemHandler.Handle.Login;

    //init combobox
    comboBox_id.ItemsSource = Login.GetIDList();
}
```

**Phase 43 수정 패턴:**
```csharp
public LoginWindow() {
    InitializeComponent();
    Login = SystemHandler.Handle.Login;
    //260615 hbk Phase 43: D-04 — 백그라운드 프리로드 미완 시 대기 (로그인 지연 0 목표)
    Login.EnsureLoaded();

    //init combobox
    comboBox_id.ItemsSource = Login.GetIDList();
}
```

`EnsureLoaded()` 는 `_isPreloaded == true` 이면 즉시 반환(대기 시간 0), 미완이면 `_preloadThread.Join()` 으로 완료까지 대기. 로그인 다이얼로그는 UI 스레드에서 `ShowDialog()` 로 표시되므로 Join 호출이 UI 차단이지만, 다이얼로그 자체가 모달 차단이라 허용 가능.

---

## Shared Patterns

### [STARTUP] 계측 로그 형식
**Source:** `WPF_Example/SystemHandler.cs` lines 119-175 (Phase 38, //260528 hbk)
**Apply to:** SystemHandler.cs Initialize() 의 모든 신규/수정 Step 로그 라인
```csharp
Logging.PrintLog((int)ELogType.Trace,
    "[STARTUP] Step N Label: {0} ms (cumulative), delta {1} ms",
    sw.ElapsedMilliseconds, sw.ElapsedMilliseconds - prev); //260615 hbk Phase 43
prev = sw.ElapsedMilliseconds;
```

### volatile + Thread 백그라운드 워커 패턴
**Source:** `WPF_Example/Utility/RawImageSaveService.cs` lines 25-44
**Apply to:** `LoginManager.cs` 의 신규 `_preloadThread`, `_isPreloaded` 필드 및 `Preload()` 메서드
```csharp
private readonly Thread _workerThread;
private volatile bool _isStopping;
private volatile bool _isStarted;
```

### 싱글턴 Handle getter 패턴
**Source:** `WPF_Example/Login/LoginManager.cs` line 88 / `WPF_Example/Sequence/SequenceHandler.cs` line 29
**Apply to:** LoginManager — Handle getter 자체는 변경 없음; 생성자에서만 Load() 제거
```csharp
public static LoginManager Handle { get; } = new LoginManager();
```

### Logging.PrintLog 호출 규칙
**Source:** `WPF_Example/SystemHandler.cs` line 119 외 다수
**Apply to:** 신규 로그 라인 전체 (`[LOGIN]`, `[STARTUP]` prefix)
```csharp
Logging.PrintLog((int)ELogType.Trace, "[PREFIX] message {0}", arg);
// ELogType 을 int 로 캐스팅 필수 (CLAUDE.md Logging 규칙)
```

### //YYMMDD hbk 주석 규칙
**Source:** CLAUDE.md + 코드베이스 전반 (`//260528 hbk`, `//260615 hbk`)
**Apply to:** 신규 추가/수정 모든 라인
```csharp
//260615 hbk Phase 43: D-03 — 설명
```

---

## No Analog Found

없음. 4개 수정 대상 파일 모두 기존 코드 내 강력한 analog 확인됨.

---

## Metadata

**Analog search scope:** `WPF_Example/`, `WPF_Example/Utility/`, `WPF_Example/Login/`, `WPF_Example/Custom/`, `WPF_Example/Sequence/`
**Files scanned:** 8개 (SystemHandler.cs × 2, LoginManager.cs, RawImageSaveService.cs, CaptureImageSaveService.cs, SequenceHandler.cs, MainWindow.xaml.cs, LoginWindow.xaml.cs)
**Pattern extraction date:** 2026-06-15
