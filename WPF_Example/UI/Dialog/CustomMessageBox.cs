using ReringProject.Setting;
using ReringProject.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace ReringProject.UI {
    public static class CustomMessageBox {
        private static MessageBoxWindow _MessageBox;
        private static void Close() {
            if (_MessageBox != null) {
                if (_MessageBox.IsVisible) {
                    _MessageBox.Close();
                    _MessageBox = null;
                }
            }
        }

        public static Window Parent { get; set; } = null;

        public static bool Show(string title, string message, MessageBoxImage imageType= MessageBoxImage.Information, bool isModal=true, bool isAutoClosing=true, int autoClosingTime=MessageBoxModel.TIME_AUTOCLOSING) {
            MessageBoxModel model = new MessageBoxModel(title, message, imageType, isAutoClosing, autoClosingTime);
            Logging.PrintLog((int)ELogType.Trace, model.ToString());
            return Show(model, isModal);
        }

        private static bool Show(MessageBoxModel model, bool isModal=true) {

            bool dialogResult = false;

            try {
                // App.Current.Dispatcher ... 주석처리 해제 02.19
                App.Current.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Normal, new Action(() => {
                    try { //260408 hbk BeginInvoke 람다 내부 예외 방어
                        Close();

                        _MessageBox = new MessageBoxWindow(model);
                        if (Parent != null && Parent.IsLoaded) {
                            _MessageBox.Owner = Parent;
                        }
                        _MessageBox.Topmost = true;

                        if (Parent == null || !Parent.IsLoaded) {
                            _MessageBox.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                        }
                        if (isModal) {
                            var result = _MessageBox.ShowDialog();
                            dialogResult = result == true;
                        }
                        else {
                            _MessageBox.Show();
                            dialogResult = true;
                        }
                    }
                    catch (Exception ex) {
                        Logging.PrintErrLog((int)ELogType.Error, "CustomMessageBox.Show lambda: " + ex.Message);
                    }
                }));
                // App.Current.Dispatcher ... 주석처리 해제 02.19
            }
            catch(Exception e) {
                //e.Message;
                Logging.PrintErrLog((int)ELogType.Error, e.Message);
            }
            return dialogResult;
        }

        public static MessageBoxResult ShowConfirmation(string title, string message, MessageBoxButton buttons) {
            try { //260408 hbk 앱 종료/에러 중 호출 시 예외 방어
                Close();
                MessageBoxModel model = new MessageBoxModel(title, message, buttons, false);

                _MessageBox = new MessageBoxWindow(model);
                if (Parent != null && Parent.IsLoaded) {
                    _MessageBox.Owner = Parent;
                }
                _MessageBox.Topmost = true;

                MessageBoxResult result = MessageBoxResult.None;
                if (Parent == null || !Parent.IsLoaded) {
                    _MessageBox.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                }

                _MessageBox.ShowDialog();
                result = _MessageBox.Result;
                Logging.PrintLog((int)ELogType.Trace, string.Format("{0} => {1}", model.ToString(), result.ToString()));
                return result;
            }
            catch (Exception e) {
                Logging.PrintErrLog((int)ELogType.Error, "ShowConfirmation: " + e.Message);
                return MessageBoxResult.None;
            }
        }
    }
}
