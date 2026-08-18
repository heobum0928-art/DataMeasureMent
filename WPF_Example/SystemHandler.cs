using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using HalconDotNet;
using ReringProject.Utility;
using ReringProject.Setting;
using ReringProject.Sequence;
using ReringProject.Network;
using ReringProject.Device;
using System.Windows;
using ReringProject.UI;
using ReringProject.UserData;
using ReringProject.Login;
using ReringProject.Properties;

namespace ReringProject {

    public sealed partial class SystemHandler {

        // Application display name.
        //260608 hbk 타이틀 리브랜딩: "DDA Vision Inspector" → "Measurement Vision"
        //260608 hbk MenuBar 로고(OutlinedTextBlock)가 좁은 폭에서 단어 중간 줄바꿈 → "Measurement"/"Vision" 2줄로 명시 개행
        public static string ProjectName { get; } = "Measurement\nVision";

        // Singleton access point.
        public static SystemHandler Handle { get; } = new SystemHandler();

        // Global settings (INI/JSON, paths, etc).
        public SystemSetting Setting { get; private set; }

        // Login/authority manager.
        public LoginManager Login { get; private set; }

        // Camera/device manager.
        public DeviceHandler Devices { get; private set; }

        // Light controller manager.
        public LightHandler Lights { get; private set; }

        // Recipe file manager.
        public RecipeFiles Recipes { get; private set; }

        // Main sequence/state machine handler.
        public SequenceHandler Sequences { get; private set; }

        // TCP vision server.
        public VisionServer Server { get; private set; }

        // Shared user data container.
        public GlobalUserData UserData { get; private set; }

        //260317 raw image save worker for inspection flow
        public RawImageSaveService RawImageSaver { get; private set; }

        //260610 hbk Phase 40.2 — FAI별 캡쳐 이미지 비동기 저장 서비스 (RawImageSaver 와 동일 라이프사이클).
        public CaptureImageSaveService CaptureImageSaver { get; private set; } //260610 hbk Phase 40.2

        // Localization resource (UI strings).
        public LocalizationResource Localize { get; set; }

        // Background system loop thread.
        private Thread mSystemThread;
        // Termination flag for system loop.
        private bool IsTerminated = false;

        // Initialization fail flag to show warnings and block certain actions.
        public bool IsInitializeFail { get; private set; } = false;
        // Indicates resources have been released.
        public bool IsReleased { get; private set; } = false;
        //260615 hbk Phase 43.2: 레시피 비동기 로드 완료 신호 — ProcessTest guard 참조용 (D-B)
        private volatile bool _isRecipeReady = false;
        public bool IsRecipeReady { get { return _isRecipeReady; } set { _isRecipeReady = value; } }

        private SystemHandler() {
            // 1) System setting
            Setting = SystemSetting.Handle;

            // 2) Recipe file info
            Recipes = RecipeFiles.Handle;

            // 3) User data
            UserData = new GlobalUserData();

            // 4) Logging setup
            //    Each log type is mapped to a folder/path from settings.
            Logging.SetLog((int)ELogType.Trace, Enum.GetName(typeof(ELogType), ELogType.Trace), Setting.GetLogSavePath(ELogType.Trace));
            Logging.SetLog((int)ELogType.Camera, Enum.GetName(typeof(ELogType), ELogType.Camera), Setting.GetLogSavePath(ELogType.Camera));
            Logging.SetLog((int)ELogType.TcpConnection, Enum.GetName(typeof(ELogType), ELogType.TcpConnection), Setting.GetLogSavePath(ELogType.TcpConnection));
            Logging.SetLog((int)ELogType.Result, Enum.GetName(typeof(ELogType), ELogType.Result), Setting.GetLogSavePath(ELogType.Result));
            Logging.SetLog((int)ELogType.Error, Enum.GetName(typeof(ELogType), ELogType.Error), Setting.GetLogSavePath(ELogType.Error));
            Logging.SetLog((int)ELogType.LightController, Enum.GetName(typeof(ELogType), ELogType.LightController), Setting.GetLogSavePath(ELogType.LightController));
            Logging.SetLog((int)ELogType.Flow, Enum.GetName(typeof(ELogType), ELogType.Flow), Setting.GetLogSavePath(ELogType.Flow));
            //260818 hbk 알고리즘 내부 진단 전용 탭/파일 — LogView 가 등록된 로그마다 탭을 자동 생성한다.
            Logging.SetLog((int)ELogType.Algorithm, Enum.GetName(typeof(ELogType), ELogType.Algorithm), Setting.GetLogSavePath(ELogType.Algorithm));
            Logging.Start();

            // 5) Device init (camera, IO, etc.)
            //    Failure here sets IsInitializeFail and shows a modal message box.
            Devices = DeviceHandler.Handle;
            EInitializeResult result = Devices.Initialize();
            if (result != EInitializeResult.Success) {
                IsInitializeFail = true;
                //CustomMessageBox.Show("Error", "Camera Initialize Fail", MessageBoxImage.Error);
                CustomMessageBox.Show("Camera Error", "Camera Initialize Fail", MessageBoxImage.Error, true, false);
            }

            // 6) Light controller handler (actual open happens in Initialize()).
            Lights = LightHandler.Handle;
            
        }
        
        //260814 hbk top-release-2x-slower 조사: Windows 시스템 타이머 해상도(기본 15.6ms)를 1ms 로 올린다.
        //  근거: 같은 exe 를 (a) 그냥 실행하면 HALCON 연산이 느리고 (b) 실행 중인 그 프로세스에 나중에 VS 디버거를
        //  붙이면 즉시 빨라지는 현상을 실측 확인했다(10/10 재현). 프로세스 생성 시점에 고정되는 요인(디버그 힙,
        //  환경변수/어피니티 상속 등)은 "이미 돌고 있는 프로세스에 나중에 붙였는데 빨라짐"을 설명할 수 없으므로
        //  전부 배제되고, 디버거가 붙어있는 "동안만" 달라지는 런타임 요인만 남는다. 그 후보 중 하나가 타이머
        //  해상도다 — 디버거/개발도구가 timeBeginPeriod(1) 을 호출하면 스레드 스케줄링 granularity 가 15.6ms 에서
        //  1ms 로 내려가고, 시퀀스 스레드처럼 Sleep/선점이 잦은 워크로드는 그만큼 대기 손실이 줄어든다.
        //  timeBeginPeriod 는 문서화된 공개 Win32 API 이고 정확성에 영향이 없다(전력 소모만 소폭 증가).
        [System.Runtime.InteropServices.DllImport("winmm.dll", EntryPoint = "timeBeginPeriod")]
        private static extern uint TimeBeginPeriod(uint uMilliseconds);

        [System.Runtime.InteropServices.DllImport("ntdll.dll", SetLastError = true)]
        private static extern int NtQueryTimerResolution(out uint MinimumResolution, out uint MaximumResolution, out uint CurrentResolution);

        //260814 hbk Windows 11 공식 문서(SetProcessInformation Remarks): "창을 가진 프로세스가 최소화되거나
        //  완전히 가려지면(fully occluded/minimized), Windows는 그 프로세스의 timeBeginPeriod 요청을 자동으로
        //  무시할 수 있다" — 실측(창을 다른 창으로 덮으면 sleep5 가 5ms→16ms 로 회수, 재현됨)과 정확히 일치한다.
        //  현장 PC는 PLC 가 TCP 로 트리거해서 아무도 창을 보지 않으므로(최소화/타 창에 가려짐 상시), 이 회수가
        //  상시 발동할 위험이 있다. PROCESS_POWER_THROTTLING_IGNORE_TIMER_RESOLUTION 을 명시적으로 "끔"으로
        //  설정하면(ControlMask 지정+StateMask=0) 이 자동회수 자체를 프로세스 단위로 옵트아웃할 수 있다 —
        //  이것도 같은 SetProcessInformation 문서가 공식 제공하는 대응 API. 관리자 권한/재부팅 불필요.
        //  EXECUTION_SPEED 플래그도 같이 꺼서 최소화 시 CPU 클럭/코어 배정이 낮아지는 것까지 예방(오늘은 이게
        //  원인이 아니었다고 확인됐지만, 무인 운영 PC라 방어적으로 같이 막아둔다).
        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        private struct PROCESS_POWER_THROTTLING_STATE {
            public uint Version;
            public uint ControlMask;
            public uint StateMask;
        }

        private const uint PROCESS_POWER_THROTTLING_CURRENT_VERSION = 1;
        private const uint PROCESS_POWER_THROTTLING_EXECUTION_SPEED = 0x1;
        private const uint PROCESS_POWER_THROTTLING_IGNORE_TIMER_RESOLUTION = 0x4;
        private const int ProcessPowerThrottling = 4; // PROCESS_INFORMATION_CLASS.ProcessPowerThrottling

        [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetProcessInformation(IntPtr hProcess, int ProcessInformationClass,
            ref PROCESS_POWER_THROTTLING_STATE ProcessInformation, uint ProcessInformationSize);

        //260814 hbk 최소화/가려짐 상태에서도 요청한 타이머 해상도가 회수되지 않도록 프로세스 단위로 옵트아웃.
        private void DisableTimerResolutionThrottling() {
            try {
                var state = new PROCESS_POWER_THROTTLING_STATE {
                    Version = PROCESS_POWER_THROTTLING_CURRENT_VERSION,
                    ControlMask = PROCESS_POWER_THROTTLING_EXECUTION_SPEED | PROCESS_POWER_THROTTLING_IGNORE_TIMER_RESOLUTION,
                    StateMask = 0 // 두 플래그 모두 "끔" — 스로틀링/타이머해상도 무시 둘 다 옵트아웃
                };
                bool ok = SetProcessInformation(Process.GetCurrentProcess().Handle, ProcessPowerThrottling,
                    ref state, (uint)System.Runtime.InteropServices.Marshal.SizeOf(state));
                Logging.PrintLog((int)ELogType.Trace, "[TimerRes] SetProcessInformation(IgnoreTimerResolution+ExecutionSpeed off) ok={0}", ok);
            }
            catch (Exception ex) {
                Logging.PrintLog((int)ELogType.Error, "[TimerRes] SetProcessInformation 실패(무시하고 계속): {0}", ex.Message);
            }
        }

        //260814 hbk 적용 전/후 실제 해상도를 100ns 단위로 되읽어 로그로 남긴다(추측이 아니라 실측 확인).
        private void ApplyHighResolutionTimer() {
            try {
                DisableTimerResolutionThrottling(); // timeBeginPeriod 보다 먼저 — 요청이 회수되지 않게 먼저 옵트아웃부터
                uint minRes, maxRes, beforeRes;
                NtQueryTimerResolution(out minRes, out maxRes, out beforeRes);
                uint rc = TimeBeginPeriod(1);
                uint afterRes;
                NtQueryTimerResolution(out minRes, out maxRes, out afterRes);
                Logging.PrintLog((int)ELogType.Trace,
                    "[TimerRes] timeBeginPeriod(1) rc={0} before={1:F3}ms after={2:F3}ms (min={3:F3}ms max={4:F3}ms)",
                    rc, beforeRes / 10000.0, afterRes / 10000.0, minRes / 10000.0, maxRes / 10000.0);
            }
            catch (Exception ex) {
                Logging.PrintLog((int)ELogType.Error, "[TimerRes] timeBeginPeriod 실패(무시하고 계속): {0}", ex.Message);
            }
        }

        // Call after constructor to fully initialize runtime components.
        public void Initialize() {
            ApplyHighResolutionTimer(); //260814 hbk top-release-2x-slower 조사 — HALCON SetSystem 보다 먼저, 최대한 이른 시점에


            // quick-260806-dsn Part A: HALCON 자체 캐시(mimalloc, HALCON 24.11 Windows 기본 할당자)가 해제된
            //  메모리를 OS에 즉시 반환하지 않고 계속 쌓아두는 문제의 공식 완화책(memory_management 챕터,
            //  "Handling Suspected Memory Leaks in HALCON" 권장 3줄, 앱 시작 시 1회). 캐시 정책만 바꿀 뿐
            //  기능/정확성에는 영향 없다. Devices/Sequences 등 이후의 모든 Halcon 이미지 처리에 적용되도록
            //  이 메서드의 첫 실행문으로 둔다. 실패해도(캐시 힌트 실패일 뿐) 앱 시작을 막지 않는다.
            try {
                // quick-260806-dsn3: 위 3줄(캐시 idle)로도 메모리가 안 돌아오는 경우를 위한 같은 챕터의 다음 단계 —
                //  HALCON 내부 힙 할당자를 Windows 기본값 mimalloc 에서 Win32 기본 힙(system)으로 전환한다.
                //  문서 원문: "mimalloc tends to cache memory more aggressively than the Win32 default heap
                //  allocator ... set_system('memory_allocator', 'system')". 격리 하네스 실측에서 121MB HImage를
                //  생성/Dispose 반복 시 mimalloc 은 8회차부터 WorkingSet 이 ~152MB 에 영구 고착(GC.Collect 무효)한 반면
                //  'system' 은 15회 전부 ~30MB 로 반환됐다. 할당자 종류 설정이므로 다른 SetSystem 보다 먼저 둔다.
                //  HALCON 내부 할당 경로만 바꾸므로 이미지 데이터/측정 수치에는 영향이 없다.

                HOperatorSet.SetSystem("memory_allocator", "system");
                HOperatorSet.SetSystem("global_mem_cache", "idle");
                //260814 hbk quick-260814-kx5 REVERTED: measure_pos 콜드스타트 완화를 위해 'idle'→'aggregate'로
                //  바꿔 시도했으나(HALCON Memory Management §2.3 근거), 실기 테스트 결과 오히려 더 느려져서
                //  원래 값('idle')으로 되돌림. SequenceBase.ReinforceThreadMemoryCache()도 'idle'로 맞춰뒀다.
                //  top-release-2x-slower.md 근본원인은 여전히 미확정.
                HOperatorSet.SetSystem("temporary_mem_cache", "idle");
                HOperatorSet.SetSystem("image_cache_capacity", 0);
            }
            catch (Exception ex) {
                Logging.PrintLog((int)ELogType.Error, "[STARTUP] HALCON SetSystem memory cache config failed: {0}", ex.Message);
            }

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

            Stopwatch sw = Stopwatch.StartNew(); //260528 hbk Phase 38 #11
            long prev = 0; //260528 hbk Phase 38 #11 — 직전 단계 누적 시각 (delta 계산용)

            // 1) Light controller open
            // Site bring-up may wire controllers in one at a time; each controller already
            // logs its own Open() failure (JPFLightController.Open), so a partial failure here
            // must not block the whole app the way camera/device init failures do.
            if (Lights.Initialize() == false) {
                Logging.PrintLog((int)ELogType.LightController, "One or more light controllers failed to open at startup.");
            }
            Logging.PrintLog((int)ELogType.Trace, "[STARTUP] Step 1 Lights.Initialize: {0} ms (cumulative), delta {1} ms", sw.ElapsedMilliseconds, sw.ElapsedMilliseconds - prev); //260528 hbk Phase 38 #11
            prev = sw.ElapsedMilliseconds; //260528 hbk Phase 38 #11

            // 2) Sequence handler
            //    Owns recipe loading and main runtime states.
            Sequences = SequenceHandler.Handle;
            Logging.PrintLog((int)ELogType.Trace, "[STARTUP] Step 2 SequenceHandler: {0} ms (cumulative), delta {1} ms", sw.ElapsedMilliseconds, sw.ElapsedMilliseconds - prev); //260528 hbk Phase 38 #11
            prev = sw.ElapsedMilliseconds; //260528 hbk Phase 38 #11

            // 3) TCP server
            //    External command/monitoring interface.
            Server = new VisionServer();

            //260317 raw image save worker for inspection flow
            RawImageSaver = new RawImageSaveService();
            RawImageSaver.Start();
            CaptureImageSaver = new CaptureImageSaveService(); //260610 hbk Phase 40.2
            CaptureImageSaver.Start(); //260610 hbk Phase 40.2
            Logging.PrintLog((int)ELogType.Trace, "[STARTUP] Step 3 VisionServer+RawImageSaver: {0} ms (cumulative), delta {1} ms", sw.ElapsedMilliseconds, sw.ElapsedMilliseconds - prev); //260528 hbk Phase 38 #11
            prev = sw.ElapsedMilliseconds; //260528 hbk Phase 38 #11

            // 4) System main loop thread
            //    Runs SystemProcess -> MainRun() in a tight loop.
            mSystemThread = new Thread(SystemProcess);
            mSystemThread.Priority = ThreadPriority.Highest;
            mSystemThread.Name = "SystemProcess";
            mSystemThread.Start();
            Logging.PrintLog((int)ELogType.Trace, "[STARTUP] Step 4 SystemThread.Start: {0} ms (cumulative), delta {1} ms", sw.ElapsedMilliseconds, sw.ElapsedMilliseconds - prev); //260528 hbk Phase 38 #11
            prev = sw.ElapsedMilliseconds; //260528 hbk Phase 38 #11

            // 5) Login manager — background preload (측정 임계 경로 외부)
            //260615 hbk Phase 43: D-03 — 동기 Load() 제거 → 백그라운드 프리로드로 교체 (Step 5 delta 808ms → ~0)
            Login = LoginManager.Handle;           // Handle getter(인스턴스 취득)만 — 생성자에서 Load() 제거됨(Task 1)
            LoginManager.Handle.Preload();         //260615 hbk Phase 43: 백그라운드 Thread 기동 (내부 IsAlive+_isPreloaded guard)
            Logging.PrintLog((int)ELogType.Trace, "[STARTUP] Step 5 LoginManager preload started: {0} ms (cumulative), delta {1} ms", sw.ElapsedMilliseconds, sw.ElapsedMilliseconds - prev); //260615 hbk Phase 43
            prev = sw.ElapsedMilliseconds; //260615 hbk Phase 43

            // 6) Hook sequence creation callbacks
            //    Typically sets up per-sequence resources.
            Sequences.ExecOnCreate();

            //260510 hbk Phase 21: BUF-02 channel #1 — OnRecipeChanged subscriber 등록 (Sequences 가 살아있고 ExecOnCreate 가 끝난 뒤 wire)
            WireBufferLifecycle();
            Logging.PrintLog((int)ELogType.Trace, "[STARTUP] Step 6 ExecOnCreate+WireBuffer: {0} ms (cumulative), delta {1} ms", sw.ElapsedMilliseconds, sw.ElapsedMilliseconds - prev); //260528 hbk Phase 38 #11
            prev = sw.ElapsedMilliseconds; //260528 hbk Phase 38 #11

            // 7) Collect recipe list
            //    Scans configured recipe directories.
            Recipes.CollectRecipe();
            Logging.PrintLog((int)ELogType.Trace, "[STARTUP] Step 7 CollectRecipe: {0} ms (cumulative), delta {1} ms", sw.ElapsedMilliseconds, sw.ElapsedMilliseconds - prev); //260528 hbk Phase 38 #11
            prev = sw.ElapsedMilliseconds; //260528 hbk Phase 38 #11

            //260615 hbk Phase 43: [STARTUP] READY — recipe ready + SystemThread alive + Sequences 구성 완료
            //  = 첫 $TEST 수용 가능 시점 (D-01). Before/After 30% 비교의 단일 기준 지표 (D-02).
            Logging.PrintLog((int)ELogType.Trace, "[STARTUP] READY: {0} ms", sw.ElapsedMilliseconds); //260615 hbk Phase 43

            // 8) Localization resource
            //    Provides runtime language switching.
            Localize = App.Current.Resources["DR"] as LocalizationResource;
            //Localize.LanguageChanged += LanguageChanged;
            Logging.PrintLog((int)ELogType.Trace, "[STARTUP] Step 8 Localize: {0} ms (cumulative), delta {1} ms", sw.ElapsedMilliseconds, sw.ElapsedMilliseconds - prev); //260528 hbk Phase 38 #11

            Logging.PrintLog((int)ELogType.Trace, "[STARTUP] Total Initialize: {0} ms", sw.ElapsedMilliseconds); //260528 hbk Phase 38 #11
            Logging.PrintLog((int)ELogType.Trace, "[SYSTEM] Initialized");

            //260623 hbk Phase 58 — AV-02: 이더넷 정렬 카메라 독립 초기화 (실패해도 Grabber/검사 무영향)
            // 260623 hbk Phase 58 review-fix IN-02: Belt-and-suspenders 의도적 이중 가드.
            // EthernetVisionHandler.Initialize() 는 내부 전체 try-catch 로 완전히 보호되어 절대 throw 하지 않음.
            // 이 외부 catch 는 방어적 레이어로만 존재하며 실제로 발동되지 않음.
            try {
                EthernetVisionHandler.Handle.Initialize();
            }
            catch (Exception ex) {
                Logging.PrintLog((int)ELogType.Error, "[ETHERNET] EthernetVisionHandler.Initialize failed: {0}", ex.Message);
            }
        }

        public bool LoadRecipe(string recipeName) {
            //260615 hbk Phase 43.2: [STARTUP-WHITE] (f) — 레시피 로드 시작. Dispatcher.Background 지연 후 실제 실행 시점 계측 (D-D)
            Logging.PrintLog((int)ELogType.Trace, "[STARTUP-WHITE] (f) recipe load start: {0} ms", App.StartupWatch.ElapsedMilliseconds);

            // Delegate recipe load to sequence handler.
            // ERecipeFileType.Ini implies recipe config stored in INI format.
            bool result = Sequences.LoadRecipe(recipeName, ERecipeFileType.Ini);
            if (result) {
                Setting.Save();
                Logging.PrintLog((int)ELogType.Trace, "[RECIPE] Loaded : {0}", recipeName);
            }
            else {
                Logging.PrintLog((int)ELogType.Trace, "[RECIPE] Load fail : {0}", recipeName);
            }

            //260615 hbk Phase 43.2: [STARTUP-WHITE] (g) — 레시피 로드 완료(성공/실패 무관). 창 표시(e)~레시피 완료(g) 구간 = 비동기 지연 확인 (D-D)
            Logging.PrintLog((int)ELogType.Trace, "[STARTUP-WHITE] (g) recipe load done (result={0}): {1} ms", result, App.StartupWatch.ElapsedMilliseconds);
            return result;
        }
        

        public void Release() {
            // Persist settings.
            Setting.Save();

            // Release device resources.
            Devices.Dispose();

            //quick-260810-e1t: 이더넷 정렬 카메라(Bottom/Tray)는 Devices(Grabber)와 별도 핸들러라 여기서
            // 명시적으로 닫아야 한다 — 안 그러면 프로세스 종료 후에도 카메라 연결이 안 끊긴다.
            // EthernetVisionHandler.Release() 는 내부 전체 try-catch 로 보호되어 절대 throw 하지 않는다
            // (Initialize() 와 동일 패턴, SystemHandler.Initialize() 234~243행 전례). 이 외부 catch 는
            // 방어적 레이어로만 존재.
            try {
                EthernetVisionHandler.Handle.Release();
            }
            catch (Exception ex) {
                Logging.PrintLog((int)ELogType.Error, "[ETHERNET] EthernetVisionHandler.Release failed: {0}", ex.Message);
            }

            //260510 hbk Phase 21: BUF-02 channel #1 — subscriber 해제 (Sequences 가 살아있는 동안 unwire)
            UnwireBufferLifecycle();
            //260510 hbk Phase 21: BUF-02 channel #3 (app shutdown buffer flush — Sequences.Dispose 가 ClearShots 를 호출하지 않으므로 명시 dispose)
            Sequences.RecipeManager.ClearShots();
            // Release sequences.
            Sequences.Dispose();

            //260317 raw image save worker for inspection flow
            RawImageSaver?.Dispose();
            RawImageSaver = null;
            CaptureImageSaver?.Dispose(); //260610 hbk Phase 40.2
            CaptureImageSaver = null; //260610 hbk Phase 40.2

            // Stop TCP server.
            Server.Dispose();

            // Release light controller.
            Lights.Release();

            // Stop system thread.
            // Join timeout prevents UI lockup on shutdown.
            IsTerminated = true;
            mSystemThread.Join(1000);

            Logging.PrintLog((int)ELogType.Trace, "[SYSTEM] Released");

            // Stop logging system last.
            Logging.Stop();
            IsReleased = true;
        }

        private void SystemProcess(object param) {
            // Simple polling loop: run main logic repeatedly.
            while (IsTerminated == false) {
                // MainRun() is implemented in another partial definition.
                MainRun();
                Thread.Sleep(1);
            }
        }
        
    }
}
