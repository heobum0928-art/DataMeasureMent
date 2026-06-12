using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
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

        public App() {
            this.Dispatcher.UnhandledException += this.Dispatcher_UnhandledException;

        }

        private void Application_Startup(object sender, StartupEventArgs e) {
            var resource = App.Current.Resources["DR"] as LocalizationResource;
            if (resource != null) {
                resource.ChangeLanguage("ko-KR");
            }

            CultureInfo c = new CultureInfo("ko-KR");
            var lang = System.Windows.Markup.XmlLanguage.GetLanguage(c.IetfLanguageTag);
            FrameworkElement.LanguageProperty.OverrideMetadata(typeof(FrameworkElement), new FrameworkPropertyMetadata(lang));

            var view = new MainWindow();
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
