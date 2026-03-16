using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using ReringProject.Device;

namespace ReringProject.UI {
    public class DeviceModelView {
        public VirtualCamera CameraHandle { get; private set; }
        
        private string _Name;
        public string Name { get { return _Name; } set { _Name = value; } }
        
        public string SourceImage { get; set; }

        public VirtualCameraProperty Property { get; set; }

        public DisplayConfig Config { get; set; }

        public String Mode { get; set; }            // 01.08 insert resolve : BindingExpression path error: 'Mode Property not found on Object'
        
        public int Width {
            get {
                return (int)CameraHandle.Properties.Width;
            }
        }

        public int Height {
            get {
                return (int)CameraHandle.Properties.Height;
            }
        }

        public DeviceModelView(VirtualCamera cameraHandle, string image) {
            CameraHandle = cameraHandle;
            Name = CameraHandle.Name;
            Property = CameraHandle.Properties;
            SourceImage = image;
            Config = CameraHandle.pConfig;
        }
    }

    public class DeviceSelectorModelView : INotifyPropertyChanged 
    {
        public const string IMAGE_DEFAULT = "/Resource/camera.png";
        public const string IMAGE_BASLER = "/Resource/basler.png";
        public const string IMAGE_HIK = "/Resource/hik.png";

        private DeviceHandler pDevs;
        private DeviceSelector Parent;

        public event PropertyChangedEventHandler PropertyChanged;
        public DeviceModelView SelectedItem { get; set; }

        public ContextMenu menu_etc { get; set; }   // 01. 08 insert resolve : BindingExpression path error: 'menu_etc Property not found on Object'

        private int _SelectedNum = -1;
        public int SelectedNum {
            get { return _SelectedNum; }
            set {
                _SelectedNum = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("SelectedNum"));
            }
        }

        private int _DropdownHeight = 600;
        public int DropdownHeight {
            get { return _DropdownHeight; }
            set { _DropdownHeight = value; }
        }

        public double DrawScale {
            get {
                return pDevs.Config.DrawScale;
            }
            set {
                pDevs.Config.DrawScale = value;
                Parent.ZoomValueChanged();
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("DrawScale"));
            }
        }

        public double DrawScaleMin {
            get {
                return DisplayConfig.DrawScaleLowLimit;
            }
        }

        public double DrawScaleMax {
            get {
                return DisplayConfig.DrawScaleHighLimit;
            }
        }

        public List<DeviceModelView> DeviceList { get; set; } = new List<DeviceModelView>();
        
        public DeviceSelectorModelView(DeviceSelector parent) {
            Parent = parent;
            SystemHandler sys = SystemHandler.Handle;
            pDevs = sys.Devices;

            for(int i = 0; i < pDevs.Count; i++) {
                string imagePath = IMAGE_DEFAULT;
                //if (pDevs[i].CamType == ECameraType.Basler) imagePath = IMAGE_BASLER;
                //else if (pDevs[i].CamType == ECameraType.HIK) imagePath = IMAGE_HIK;

                DeviceList.Add(new DeviceModelView(pDevs[i], imagePath));
            }
        }
        

    }
}
