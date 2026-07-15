
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ReringProject.Setting;
using Microsoft.Win32;
using ReringProject.Device;
using System.Windows.Data;
using PropertyTools.Wpf;
using System.Windows.Threading;
using ReringProject.Utility;
using System.Windows.Media.Imaging;
using System.Diagnostics;

namespace ReringProject.UI {
    /// <summary>
    /// DeviceSelector.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class DeviceSelector : System.Windows.Window {
        //private const int DISPLAY_INTERVAL = 50;      // 01.02 Rering 기준 주석 처리.
        private DeviceSelectorModelView ModelView;

        private SystemSetting pSetting;
        private DeviceHandler pDevs = null;
        private LightHandler pLight = null;
        private VirtualCamera pSelectedDevice = null;
        
        private int FPS_AGGREGATION_TIME_S = 10;
        private int imageCountOld = 0;
        private List<int> ImageCounts = new List<int>();
        private DispatcherTimer FpsTimer;
        private int PrevSelectedIndex = -1;

        private const int UPDATE_INTERVAL = 100;            // 01.02 Rering 기준 추가.
        private Stopwatch UpdateCounter = new Stopwatch();  // 01.02 Rering 기준 추가.

        private string InitialSelectedDeviceName = null;

        //private Stopwatch DisplayInterval = new Stopwatch();  // 01.02 Rering 기준 주석 처리.

        //private object mDrawInterlock = new object();

        //public int BackgroundWidth { get; private set; } = DeviceHandler.DEFAULT_WIDTH;
        //public int BackgroundHeight { get; private set; } = DeviceHandler.DEFAULT_HEIGHT;

        public DeviceSelector(string devName = null) {
            pDevs = SystemHandler.Handle.Devices;
            pLight = SystemHandler.Handle.Lights;
            pSetting = SystemHandler.Handle.Setting;

            InitializeComponent();
            ModelView = new DeviceSelectorModelView(this);
            this.DataContext = ModelView;
            
            image_foreground.RenderTransform = scaleTransform;
            
            // 채널별 개별 ON/밝기 제어 (그룹 단위 대신) — LightHandlerWindow "Setting" 탭과 동일하게
            // LightChannelView/LightChannelViewModel 재사용. 그룹 단위로는 RING 전체처럼 여러 채널이 한꺼번에만
            // 켜져서, 개별 채널(RING_CH1 하나만 등) 실시간 테스트가 불가능했다.
            for (int i = 0; i < pLight.Controllers.Count; i++) {
                VirtualLightController controller = pLight.Controllers[i];
                for (int j = 0; j < controller.Channels.Length; j++) {
                    LightChannelViewModel channelModel = new LightChannelViewModel(controller.Channels[j], j);
                    LightChannelView channelView = new LightChannelView(channelModel);
                    stackPanel_light.Children.Add(channelView);
                }
            }
            
            FpsTimer = new DispatcherTimer();
            FpsTimer.Interval = TimeSpan.FromMilliseconds(500);
            FpsTimer.Tick += OnTimerTick;

            InitialSelectedDeviceName = devName;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e) {
            if (combo_device.Items.Count > 0) {
                int index = 0;
                if (InitialSelectedDeviceName != null) {
                    index = pDevs.IndexOf(InitialSelectedDeviceName);
                    if (index == -1) index = 0;
                }
                //ModelView.SelectedNum = index;
                combo_device.SelectedIndex = index;
            }

            //DisplayInterval.Restart();        // 01.02 Rering 기준 주석 처리.
            FpsTimer.Start();
        }

        public DisplayConfig SelectedDisplayConfig {
            get {
                if (ModelView.SelectedItem == null) return null;
                return ModelView.SelectedItem.Config;
            }
        }

        private void OnTimerTick(object sender, EventArgs args) {
            if (IsVisible == false) return;
            if (pSelectedDevice == null) return;
            int imageCount = (int)pSelectedDevice.ImageCount;
            double fpsApproximation = 0.0;

            if (pSelectedDevice.IsGrabbing) {
                if (ImageCounts.Count > 0) {
                    int imageCountCurrent = imageCount - imageCountOld;
                    ImageCounts.Add(imageCountCurrent);
                    while (ImageCounts.Count > FPS_AGGREGATION_TIME_S) {
                        ImageCounts.RemoveAt(0);
                    }
                    int sum = ImageCounts.Sum();
                    fpsApproximation = (double)sum / (double)ImageCounts.Count;
                }
                else {
                    int imageCountCurrent = imageCount - imageCountOld;

                    if (imageCountOld != 0) {
                        ImageCounts.Add(imageCountCurrent);
                    }
                    fpsApproximation = (double)imageCountCurrent;
                }
            }
            //update state
            textBlock_fps.Text = string.Format("FPS:{0:0.0}", fpsApproximation);
            textBlock_state.Text = pSelectedDevice.StateString;
            textBlock_selectedMode.Text = pSelectedDevice.ModeString;

            for(int i = 0; i < stackPanel_light.Children.Count; i++) {
                UIElement uiElement = stackPanel_light.Children[i];
                if(uiElement is LightChannelView) {
                    LightChannelView view = uiElement as LightChannelView;
                    view.UpdateBindingTarget();
                }
            }
            imageCountOld = imageCount;
        }

        public string SelectedDeviceName {
            get {
                if (combo_device.SelectedIndex < 0) return null;
                return combo_device.SelectedItem.ToString();
            }
        }

        //public object CameraDefine { get; private set; }

        private void Btn_ok_Click(object sender, RoutedEventArgs e) {
            if (SelectedDeviceName != null) {
                pDevs.Config.Save();
            }
            this.DialogResult = true;
            this.Close();
        }

        private void Btn_cancel_Click(object sender, RoutedEventArgs e) {
            
            this.DialogResult = false;
            this.Close();
        }
        
        private void Combo_device_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            //if (pSelectedDevice != null) {
            int selected = ModelView.SelectedNum;
            if ((PrevSelectedIndex != -1) && (PrevSelectedIndex != ModelView.SelectedNum)) { 
                pSelectedDevice.StopStream();
                pSelectedDevice.GuiReadyForDisplay -= OnImageReady;
                imageCountOld = 0;
                ImageCounts.Clear();
            }
            
            if (selected >= 0) {
                pSelectedDevice = pDevs[selected];
                pSelectedDevice.GuiReadyForDisplay += OnImageReady;
                ZoomValueChanged();
                pSelectedDevice.StartStream();
                UpdateCounter.Restart();            //01.02 Rering 기준 추가.
                canvas_preview.SetDevice(pSelectedDevice);
            }
            //ChangePropertyUI(selected);
            PrevSelectedIndex = selected;
        }
        

        private bool DisplayToBackground(BitmapSource frame) {
            if (UpdateCounter.ElapsedMilliseconds < UPDATE_INTERVAL) {
                return true;
            }

            try {
                if (frame != null) {
                    canvas_preview.Background = new ImageBrush(frame);
                    ZoomValueChanged();
                }
                else {
                    canvas_preview.Background = System.Windows.Media.Brushes.Black;
                    return false;
                }
            }
            catch (Exception e) {
                Logging.PrintErrLog((int)ELogType.Error, e.Message);
                return false;
            }
            UpdateCounter.Restart();
            return true;
        }

        private void OnImageReady(string name) {
            if (pSelectedDevice == null) return;
            if (pSelectedDevice.Name != name) return;

            Application.Current.Dispatcher.BeginInvoke(new Action(() => {
                BitmapSource grabbedImage = pSelectedDevice.GetPreviewBitmapSource();
                if(grabbedImage != null) {
                    //if (DisplayInterval.ElapsedMilliseconds < DISPLAY_INTERVAL) return;   // 01.02 Rering 기준 주석 처리.
                    DisplayToBackground(grabbedImage);
                    //DisplayInterval.Restart();	//01.02 Rering 기준 주석 처리.
                    //DisplayOverlay(grabbedImage);
                    //pSelectedDevice.Display(image_display);
                    //pSelectedDevice.DisplayCenterLine(image_foreground);
                }
            }));
        }
        

        private void Btn_etc_Click(object sender, RoutedEventArgs e) {
            ContextMenu cm = this.FindResource("menu_etc") as ContextMenu;
            cm.PlacementTarget = sender as Button;
            cm.IsOpen = true;
        }

        private void MenuItem_SaveImage_Click(object sender, RoutedEventArgs e) {
            if (pSelectedDevice == null) return;
            //SaveFileDialog saveDialog = new SaveFileDialog();
            //saveDialog.InitialDirectory = pSetting.GetLogSavePath(ELogType.Image);
            string filePath = pSetting.GetCameraImageSavePath(Name);
            if (pSelectedDevice.SaveImage(filePath) == false) {
                CustomMessageBox.Show("Fail to Image Save", string.Format("Cannot save to that path. Check the storage path and storage capacity. : {0}", filePath), MessageBoxImage.Error);
                return;
            }
            CustomMessageBox.Show("Success to Image Save", string.Format("Image Saved : {0}", filePath), MessageBoxImage.Information, false);
        }

        private void MenuItem_LoadImage_Click(object sender, RoutedEventArgs e) {
            if (pSelectedDevice == null) return;
            MenuItem selected = sender as MenuItem;
            
            Ookii.Dialogs.Wpf.VistaFolderBrowserDialog dlg = new Ookii.Dialogs.Wpf.VistaFolderBrowserDialog();
            dlg.Multiselect = false;
            dlg.RootFolder = Environment.SpecialFolder.CommonStartup;
            dlg.SelectedPath = SystemHandler.Handle.Setting.ImageSavePath;
            if ((bool)dlg.ShowDialog()) {
                pSelectedDevice.BackgroundImagePath = dlg.SelectedPath;
                selected.IsChecked = true;
                /*
                if(pSelectedDevice.CaptureMode == ECaptureModeType.Streaming) {
                    pSelectedDevice.StopStream();
                }
                */
                
            }
            else {
                pSelectedDevice.BackgroundImagePath = null;
                selected.IsChecked = false;
                /*
                if (pSelectedDevice.CaptureMode == ECaptureModeType.Streaming) {
                    pSelectedDevice.StartStream();
                }
                */
            }
            if (pSelectedDevice.CamType == ECameraType.Virtual) {
                BitmapSource grabbedImage = pSelectedDevice.GetPreviewBitmapSource();
                DisplayToBackground(grabbedImage);
                //DisplayOverlay(grabbedImage);
                //pSelectedDevice.Display(image_display);
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e) {
            if (pSelectedDevice != null) {
                pSelectedDevice.StopStream();
                pSelectedDevice.GuiReadyForDisplay -= OnImageReady;
                imageCountOld = 0;
                ImageCounts.Clear();
            }
            FpsTimer.Stop();
        }

        private void Menu_streaming_Click(object sender, RoutedEventArgs e) {
            if (pSelectedDevice == null) return;
            pSelectedDevice.BackgroundImagePath = null;
            pSelectedDevice.StartStream();
        }
        

        public void ZoomValueChanged() {
            //resize 
            if (pSelectedDevice == null) return;
            if (pSelectedDevice.Properties == null) return;
        
            scaleTransform.ScaleX = pDevs.Config.DrawScale;
            scaleTransform.ScaleY = pDevs.Config.DrawScale;

            canvas_preview.Width = pSelectedDevice.Properties.Width * pDevs.Config.DrawScale;
            canvas_preview.Height = pSelectedDevice.Properties.Height * pDevs.Config.DrawScale;
            image_foreground.Width = canvas_preview.Width;
            image_foreground.Height = canvas_preview.Height;
        }

        private void Menu_nextImage_Click(object sender, RoutedEventArgs e) {
            if (pSelectedDevice == null) return;
            pSelectedDevice.IncreaseBackgroundImageIndex();

            if (pSelectedDevice.CamType == ECameraType.Virtual) {
                BitmapSource grabbedImage = pSelectedDevice.GetPreviewBitmapSource();
                DisplayToBackground(grabbedImage);
                //DisplayOverlay(grabbedImage);
                //pSelectedDevice.Display(image_display);
            }
        }

        private void Menu_prevImage_Click(object sender, RoutedEventArgs e) {
            if (pSelectedDevice == null) return;
            pSelectedDevice.DecreaseBackgroundImageIndex();

            if (pSelectedDevice.CamType == ECameraType.Virtual) {
                BitmapSource grabbedImage = pSelectedDevice.GetPreviewBitmapSource();
                DisplayToBackground(grabbedImage);
                //DisplayOverlay(grabbedImage);
                //pSelectedDevice.Display(image_display);
            }
        }

        private void Menu_openDir_Click(object sender, RoutedEventArgs e) {
            string savePath = pSetting.GetLogSavePath(ELogType.Image);
            System.Diagnostics.Process.Start(savePath);
        }
    }
}
