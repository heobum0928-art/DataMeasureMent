
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace ReringProject.UI {
    /// <summary>
    /// MenuBar.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class MenuBar : UserControl {
        MainWindow mParentWindow;
        
        public MenuBar() {
            InitializeComponent();

            label_title.Text = SystemHandler.ProjectName;
            label_Version.Text = string.Format("Platform : {0}", SystemHandler.Handle.Recipes.GetVersion());
            label_DLLVersion.Text = string.Format("DLL : {0}", SystemHandler.Handle.Recipes.GetDLLVersion());
        }

        private bool _IsEditable = false;
        public bool IsEditable {
            get {
                return _IsEditable;
            }

            set {
                ButtonSetting.IsEnabled = value;
                Button_Camera.IsEnabled = value;
                Button_Light.IsEnabled = value;
                Button_Connect.IsEnabled = value;
                
                _IsEditable = value;
            }
        }

        private void MenuBar_Loaded(object sender, RoutedEventArgs e) {
            mParentWindow = (MainWindow)Window.GetWindow(this);
            UpdateLoginID(SystemHandler.Handle.Login.LoginID);
        }

        public void UpdateState() {
            this.label_DateTime.Text = DateTime.Now.ToString();
            label_status.Content = SystemHandler.Handle.Sequences.StateAll;
            label_seqName.Content = SystemHandler.Handle.Sequences.StateSequenceName;
        }

        public void UpdateLoginID(string id) {
            this.Dispatcher.Invoke(() => { 
                textBlock_login.Text = id.ToUpper();
            });
        }

        private void ButtonSetting_Click(object sender, RoutedEventArgs e) {
            //mParentWindow.DisplayView(PageType.Setting);
            mParentWindow.PopupView(EPageType.Setting);
        }

        private void Button_Log_Click(object sender, RoutedEventArgs e) {
            mParentWindow.PopupView(EPageType.Log);
        }

        private void Button_Recipe_Click(object sender, RoutedEventArgs e) {
            //mParentWindow.DisplayView(PageType.Recipe);
            mParentWindow.PopupView(EPageType.Recipe);
        }

        private void Button_Camera_Click(object sender, RoutedEventArgs e) {
            mParentWindow.PopupView(EPageType.Camera);
        }

        private void Button_Light_Click(object sender, RoutedEventArgs e) {
            mParentWindow.PopupView(EPageType.Light);
        }

        private void Button_Connect_Click(object sender, RoutedEventArgs e) {
            mParentWindow.PopupView(EPageType.Connect);
        }

        private void Button_Login_Click(object sender, RoutedEventArgs e) {
            mParentWindow.PopupView(EPageType.Login);
        }

        private void Button_CI_Click(object sender, RoutedEventArgs e) {

        }

        private void Label_status_Click(object sender, RoutedEventArgs e) {
            mParentWindow.PopupView(EPageType.ProcessMonitor);
        }

        // 결과 리뷰어 비모달 창 열기
        private void Button_Reviewer_Click(object sender, RoutedEventArgs e) {
            mParentWindow.PopupView(EPageType.Reviewer);
        }

        // 통계분석 비모달 창 열기
        private void Button_Statistics_Click(object sender, RoutedEventArgs e) {   //260707 hbk STAT-01 D-09
            mParentWindow.PopupView(EPageType.Statistics);
        }
    }
}
