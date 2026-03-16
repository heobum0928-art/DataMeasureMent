using System;
using System.Timers;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace ReringProject.UI {
    /// <summary>
    /// MessageBoxWindow.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class MessageBoxWindow : Window {
        MessageBoxModel pModel;
        DispatcherTimer mTimer = null;

        public MessageBoxResult Result { get; private set; }

        public MessageBoxWindow(MessageBoxModel model) {
            InitializeComponent();
            pModel = model;
            DataContext = model;
            if (pModel.Message.Length > 40) {
                textBox_message.FontSize /= 2;
            }

            switch (model.MessageType) {
                case MessageBoxImage.Warning:
                case MessageBoxImage.Error:
                    border_outline.BorderBrush = Brushes.Red;
                    break;
                case MessageBoxImage.Question:
                case MessageBoxImage.Information:
                    border_outline.BorderBrush = Brushes.Aqua;
                    break;
            }

            switch (model.Buttons) {
                case MessageBoxButton.OK:
                    button_ok.Visibility = Visibility.Visible;
                    button_cancel.Visibility = Visibility.Collapsed;
                    button_yes.Visibility = Visibility.Collapsed;
                    button_no.Visibility = Visibility.Collapsed;
                    break;
                case MessageBoxButton.OKCancel:
                    button_ok.Visibility = Visibility.Visible;
                    button_cancel.Visibility = Visibility.Visible;
                    button_yes.Visibility = Visibility.Collapsed;
                    button_no.Visibility = Visibility.Collapsed;
                    break;
                case MessageBoxButton.YesNo:
                    button_ok.Visibility = Visibility.Collapsed;
                    button_cancel.Visibility = Visibility.Collapsed;
                    button_yes.Visibility = Visibility.Visible;
                    button_no.Visibility = Visibility.Visible;
                    break;
                case MessageBoxButton.YesNoCancel:
                    button_ok.Visibility = Visibility.Collapsed;
                    button_cancel.Visibility = Visibility.Visible;
                    button_yes.Visibility = Visibility.Visible;
                    button_no.Visibility = Visibility.Visible;
                    break;
            }

            if (pModel.IsAutoClosing) {
                //if (pModel.AutoClosingTime < 1)     //  07.30 Custom Message Box 사용 시 일정 시간 경과 후 자동 종료되지 않게 하기 위해 코드 추가함.
                //    return;

                mTimer = new DispatcherTimer();
                mTimer.Interval = TimeSpan.FromSeconds(pModel.AutoClosingTime);
                mTimer.Tick += OnTimerTick;
                mTimer.Start();
            }
        }

        private void OnTimerTick(object sender, EventArgs e) {
            mTimer.Stop();
            mTimer.Tick -= OnTimerTick;

            //DialogResult = false;
            Result = MessageBoxResult.Cancel;

            this.Close();
        }

        private void Button_ok_Click(object sender, RoutedEventArgs e) {
            //DialogResult = true;
            Result = MessageBoxResult.OK;
            //this.Visibility = Visibility.Hidden;
            this.Close();
        }

        private void Button_cancel_Click(object sender, RoutedEventArgs e) {
            //DialogResult = false;
            Result = MessageBoxResult.Cancel;
            this.Close();
            //this.Visibility = Visibility.Hidden;
        }

        private void Button_yes_Click(object sender, RoutedEventArgs e) {
            //DialogResult = true;
            Result = MessageBoxResult.Yes;
            this.Close();
            //this.Visibility = Visibility.Hidden;
        }

        private void Button_no_Click(object sender, RoutedEventArgs e) {
            //DialogResult = false;
            Result = MessageBoxResult.No;
            this.Close();
            //this.Visibility = Visibility.Hidden;
        }

        private void Window_Closed(object sender, EventArgs e) {
            if(mTimer != null) mTimer.Stop();
        }
    }

    public class MessageBoxModel {
        public const int TIME_AUTOCLOSING = 7;

        public string Title { get; set; }

        public string Message { get; set; }

        public string ImageSource { get; set; }
       

        public bool IsAutoClosing { get; set; }
        public int AutoClosingTime { get; set; }

        public MessageBoxImage MessageType { get; set; }

        public MessageBoxButton Buttons { get; set; }

        public override string ToString() {
            return string.Format("[MessageBox] {0},{1},{2}", Title, Message, MessageType.ToString());
        }

        //alarm message
        public MessageBoxModel(string title, string message, MessageBoxImage imageType, bool autoClosing=false, int autoClosingTime=TIME_AUTOCLOSING) {
            Title = title;
            Message = message;
            IsAutoClosing = autoClosing;
            AutoClosingTime = autoClosingTime;
            MessageType = imageType;
            Buttons = MessageBoxButton.OK;

            switch (imageType) {
                case MessageBoxImage.Warning:
                case MessageBoxImage.Error:
                    ImageSource = "/Resource/error.png";
                    break;
                case MessageBoxImage.Question:
                case MessageBoxImage.Information:
                    ImageSource = "/Resource/information.png";
                    break;
            }
        }

        //confirmation
        public MessageBoxModel(string title, string message, MessageBoxButton buttons, bool autoClosing=false, int autoClosingTime = TIME_AUTOCLOSING) {
            Title = title;
            Message = message;
            IsAutoClosing = autoClosing;
            AutoClosingTime = autoClosingTime;
            MessageType = MessageBoxImage.Question;
            Buttons = buttons;
            ImageSource = "/Resource/zoom.png";
        }
        
    }
}
