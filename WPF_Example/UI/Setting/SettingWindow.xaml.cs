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
using System.Windows.Shapes;
using ReringProject.Setting;

namespace ReringProject.UI {
    /// <summary>
    /// SettingWindow.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class SettingWindow : Window {
        SystemSetting pSetting;

        public SettingWindow() {
            pSetting = SystemSetting.Handle;
            pSetting.Load();

            InitializeComponent();
            this.DataContext = new SettingViewModel();
        }

        private void Btn_cancel_Click(object sender, RoutedEventArgs e) {
            DialogResult = false;
            Close();
        }

        private void Btn_ok_Click(object sender, RoutedEventArgs e) {
            pSetting.Save();
            
            DialogResult = true;
            Close();
        }

        private void Window_ContentRendered(object sender, EventArgs e) {

        }
    }

    public class SettingViewModel {
        public SystemSetting Setting { get; set; }

        public SettingViewModel() {
            Setting = SystemSetting.Handle;
        }
    }
}
