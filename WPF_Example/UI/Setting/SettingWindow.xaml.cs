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
        int pOriginalCameraRoleValue; //260611 hbk 설정 창 진입 시점의 CameraRole 원본값 (변경 감지용)

        public SettingWindow() {
            pSetting = SystemSetting.Handle;
            pSetting.Load();

            pOriginalCameraRoleValue = pSetting.CameraRoleValue; //260611 hbk 원본 CameraRole 보관

            InitializeComponent();
            this.DataContext = new SettingViewModel();
        }

        private void Btn_cancel_Click(object sender, RoutedEventArgs e) {
            DialogResult = false;
            Close();
        }

        private void Btn_ok_Click(object sender, RoutedEventArgs e) {
            //260611 hbk CameraRole(검사 모드) 변경 시 경고 확인 — 함부로 못 바꾸게
            if (pSetting.CameraRoleValue != pOriginalCameraRoleValue) {
                ECameraRole oldRole = (ECameraRole)pOriginalCameraRoleValue; //260611 hbk
                ECameraRole newRole = (ECameraRole)pSetting.CameraRoleValue; //260611 hbk
                string msg = "카메라 역할(검사 모드)을 [" + oldRole + "] → [" + newRole + "] 으로 바꿉니다.\n"
                           + "이 PC 의 검사 담당(Top/Bottom 또는 Side)이 바뀌며, 프로그램을 껐다 켜야 적용됩니다.\n"
                           + "계속하시겠습니까?"; //260611 hbk
                MessageBoxResult confirm = MessageBox.Show(msg, "카메라 역할 변경 확인",
                    MessageBoxButton.YesNo, MessageBoxImage.Warning); //260611 hbk
                if (confirm != MessageBoxResult.Yes) {
                    pSetting.CameraRoleValue = pOriginalCameraRoleValue; //260611 hbk 취소 시 원복, 창 유지
                    return;
                }

                pSetting.Save(); //260611 hbk
                MessageBox.Show("프로그램을 재시작하면 새 모드로 시작됩니다.", "재시작 필요",
                    MessageBoxButton.OK, MessageBoxImage.Information); //260611 hbk 재시작 안내
                DialogResult = true;
                Close();
                return;
            }

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
