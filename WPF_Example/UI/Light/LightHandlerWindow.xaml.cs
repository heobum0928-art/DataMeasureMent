using ReringProject.Device;
using ReringProject.Setting;
using ReringProject.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace ReringProject.UI {
    /// <summary>
    /// LightHandlerView.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class LightHandlerWindow : Window {
        LightHandler pHandle;
        DispatcherTimer mTimer = new DispatcherTimer();
    

        public LightHandlerWindow() {
            
            InitializeComponent();

            pHandle = SystemHandler.Handle.Lights;

            for(int i = 0; i < pHandle.Controllers.Count; i++) {
                VirtualLightController cont = pHandle.Controllers[i];
                LightControllerViewModel controllerModel = new LightControllerViewModel(cont);
                LightControllerView controllerView = new LightControllerView(controllerModel);
                panel_setting.Children.Add(controllerView);
            }
            mTimer.Interval = TimeSpan.FromMilliseconds(500);
            mTimer.Tick += MTimer_Tick;
            mTimer.Start();
        }

        private void MTimer_Tick(object sender, EventArgs e) {
            for (int i = 0; i < panel_setting.Children.Count; i++) {
                UIElement uiElement = panel_setting.Children[i];

                if (uiElement is LightControllerView) {
                    LightControllerView view = uiElement as LightControllerView;
                    view.UpdateBindingTarget();
                }
            }
        }

        private void Btn_cancel_Click(object sender, RoutedEventArgs e) {
            this.DialogResult = false;
            this.Close();
        }

        private void Btn_ok_Click(object sender, RoutedEventArgs e) {
            this.DialogResult = true;
            pHandle.Save();
            this.Close();
        }

        private void Window_Closed(object sender, EventArgs e) {
            mTimer.Stop();
            
            for(int i = 0; i < pHandle.Controllers.Count; i++) {
                if(pHandle.Controllers[i].IsOpen == false) {
                    if(pHandle.Controllers[i].Open() == false) {
                        // Not-yet-wired controllers are expected to fail retry here during
                        // partial site bring-up; log instead of blocking the operator with a modal.
                        Logging.PrintLog((int)ELogType.LightController, "Controller {0} retry-open failed on window close.", i);
                    }
                }
            }
        }
    }
}
