using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Diagnostics; //260615 hbk Phase 43.1: 흰 화면 구간 계측 Stopwatch
using ReringProject.UI;
using ReringProject.Utility;
using ReringProject.Setting;
using ReringProject.Properties;
using System.Globalization;

namespace ReringProject {
    /// <summary>
    /// App.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class App : Application {
        Mutex mMutex;

        //260615 hbk Phase 43.1: D-03 — 흰 화면(process→첫 paint) 구간 분해용 절대시각 기준. App 정적 진입부터 측정.
        internal static readonly Stopwatch StartupWatch = Stopwatch.StartNew();

        private System.Windows.SplashScreen _splash; //260615 hbk Phase 43.1: D-02 수동 제어용 splash 참조 (ContentRendered 에서 Close)

        public App() {
            this.Dispatcher.UnhandledException += this.Dispatcher_UnhandledException;

        }

        private void Application_Startup(object sender, StartupEventArgs e) {
            Logging.PrintLog((int)ELogType.Trace, "[STARTUP-WHITE] (a) App.Startup entry: {0} ms", StartupWatch.ElapsedMilliseconds); //260615 hbk Phase 43.1
            //260615 hbk Phase 43.1: D-01/D-02 — 관리 UI(new MainWindow) 이전 즉시 스플래시 표시(autoClose=false). 흰 화면 마스킹.
            try {
                _splash = new System.Windows.SplashScreen("Resource/splash.png");
                _splash.Show(false); // autoClose=false — ContentRendered 까지 유지
            }
            catch (Exception ex) {
                Logging.PrintErrLog((int)ELogType.Error, "[STARTUP-WHITE] splash show fail: " + ex.Message); //260615 hbk Phase 43.1 — 스플래시 실패는 기동을 막지 않음
                _splash = null;
            }
            var resource = App.Current.Resources["DR"] as LocalizationResource;
            if (resource != null) {
                resource.ChangeLanguage("ko-KR");
            }

            CultureInfo c = new CultureInfo("ko-KR");
            var lang = System.Windows.Markup.XmlLanguage.GetLanguage(c.IetfLanguageTag);
            FrameworkElement.LanguageProperty.OverrideMetadata(typeof(FrameworkElement), new FrameworkPropertyMetadata(lang));

            var view = new MainWindow();
            //260615 hbk Phase 43.1: D-02 — 첫 실제 paint(ContentRendered)까지 splash 유지 후 fade close. auto-close 금지(너무 일찍 닫혀 흰 화면 재노출).
            view.ContentRendered += (s, ev) => {
                Logging.PrintLog((int)ELogType.Trace, "[STARTUP-WHITE] (e) ContentRendered first paint: {0} ms", StartupWatch.ElapsedMilliseconds); //260615 hbk Phase 43.1
                if (_splash != null) {
                    _splash.Close(TimeSpan.FromSeconds(0.3)); // 0.3s fade
                    _splash = null;
                }
            };
            view.Show();
        }

        protected override void OnStartup(StartupEventArgs e) {
            string mutexName = AppDomain.CurrentDomain.FriendlyName;
            mMutex = new Mutex(true, mutexName, out bool createNew);

            if (!createNew) {
                CustomMessageBox.Show("Duplicate execution error", "Program is already running.", MessageBoxImage.Error, true);
                Shutdown();
                return;
            }
            base.OnStartup(e);
        }

        private bool _isHandlingException = false; // 재진입 방지 플래그

        private void Dispatcher_UnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e) {
            e.Handled = true;

            // 재진입 방지: 예외 처리 중 또 다른 예외 발생 시 무한 루프 차단
            if (_isHandlingException) return;
            _isHandlingException = true;

            try {
                Logging.PrintErrLog((int)ELogType.Error, "Unhandled: " + e.Exception.ToString());
                CustomMessageBox.Show("Unhandled Exception", e.Exception.ToString(), MessageBoxImage.Error, true, false);
            }
            catch {
                // 메시지 박스 표시 실패 — 무시
            }

            _isHandlingException = false;
        }
    }
}
