using System;
using System.Collections.Generic;
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
        public static string ProjectName { get; } = "DDA Vision Inspector";

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
            // 1) Light controller open
            if (Lights.Initialize() == false) {
                IsInitializeFail = true;
                //CustomMessageBox.Show("Error", "Light Controller Open Fail", MessageBoxImage.Error);
                CustomMessageBox.Show("Light Error", "Light Controller Open Fail", MessageBoxImage.Error, true, false);
            }
            // 2) Sequence handler
            //    Owns recipe loading and main runtime states.
            Sequences = SequenceHandler.Handle;
            
            // 3) TCP server
            //    External command/monitoring interface.
            Server = new VisionServer();

            //260317 raw image save worker for inspection flow
            RawImageSaver = new RawImageSaveService();
            RawImageSaver.Start();
            
            // 4) System main loop thread
            //    Runs SystemProcess -> MainRun() in a tight loop.
            mSystemThread = new Thread(SystemProcess);
            mSystemThread.Priority = ThreadPriority.Highest;
            mSystemThread.Name = "SystemProcess";
            mSystemThread.Start();

            // 5) Login manager
            Login = LoginManager.Handle;

            // 6) Hook sequence creation callbacks
            //    Typically sets up per-sequence resources.
            Sequences.ExecOnCreate();

            //260510 hbk Phase 21: BUF-02 channel #1 — OnRecipeChanged subscriber 등록 (Sequences 가 살아있고 ExecOnCreate 가 끝난 뒤 wire)
            WireBufferLifecycle();

            // 7) Collect recipe list
            //    Scans configured recipe directories.
            Recipes.CollectRecipe();

            // 8) Localization resource
            //    Provides runtime language switching.
            Localize = App.Current.Resources["DR"] as LocalizationResource;
            //Localize.LanguageChanged += LanguageChanged;

            Logging.PrintLog((int)ELogType.Trace, "[SYSTEM] Initialized");
        }

        public bool LoadRecipe(string recipeName) {
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
