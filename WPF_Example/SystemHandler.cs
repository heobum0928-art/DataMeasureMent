using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
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
        
        // Call after constructor to fully initialize runtime components.
        public void Initialize() {
            Stopwatch sw = Stopwatch.StartNew(); //260528 hbk Phase 38 #11
            long prev = 0; //260528 hbk Phase 38 #11 — 직전 단계 누적 시각 (delta 계산용)

            // 1) Light controller open
            if (Lights.Initialize() == false) {
                IsInitializeFail = true;
                //CustomMessageBox.Show("Error", "Light Controller Open Fail", MessageBoxImage.Error);
                CustomMessageBox.Show("Light Error", "Light Controller Open Fail", MessageBoxImage.Error, true, false);
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
