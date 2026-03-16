using System;
using System.Collections.Generic;
using System.Linq;
using HalconDotNet;
using OpenCvSharp;
using PropertyTools;
using PropertyTools.DataAnnotations;
using ReringProject.Define;
using ReringProject.Device;
using ReringProject.Utility;

namespace ReringProject.Sequence {

    public interface ICameraParam {
        string LightGroupName { get; }

        int LightLevel { get; }

        string DeviceName { get; }

        PropertyItem[] PropertyArray { get; }

        void PutImage(HImage image);
        void PutImage(Mat image);

        string SequenceName { get; }

        string ActionName { get; }
    }

    public interface IOfflineImageParam {
        string GetLatestImagePath();

        void SetLatestImagePath(string imagePath);
    }
    
    public class PropertyItem : Observable {
        
        public string Name { get; private set; }

        public ECameraPropertyType GetPropertyType() {
            if (Name == null) return ECameraPropertyType.Exposure;
            return (ECameraPropertyType)Enum.Parse(typeof(ECameraPropertyType), Name);
        }

        public void SetPropertyType(ECameraPropertyType type) {
            Name = Enum.GetName(typeof(ECameraPropertyType), type);
        }

        //public double Min { get; private set; }

        public double _value = 0;
        public double Value {
            get {
                return this._value;
            }
            set {
                this.SetValue(ref _value, value);
            }
        }

        //public double Max { get; private set; }

        public PropertyItem() {

        }

        public PropertyItem(string name) {
            Name = name;
        }
    }
    
    public class CameraParam : ParamBase, ICameraParam {
        [Browsable(false)]
        private DeviceHandler pDev;

        [Browsable(false)]
        private LightHandler pLight;
        
        
        [Category("General|AOI")]
        //public double PixelToMM_Offset { get; set; }
        public double PixelToUM_Offset { get; set; }    // 02.14 insert

        public double MotorXPos { get; set; }
        public double MotorYPos { get; set; }
        public int FrameWidth { get; set; }
        public int FrameHeight { get; set; }
        public int PartNo { get; set; }


        [Category("Device|Light")]
        //public List<LightSettingItem> LightList { get; } = new List<LightSettingItem>();
        [ItemsSourceProperty("LightGroupList")]
        public string LightGroupName { get; set; }


        [Browsable(false)]
        public List<string> LightGroupList { get { return _LightGroupList; } }
        private static List<string> _LightGroupList;
        public int LightLevel { get; set; }

        [Browsable(false)]
        public List<string> DeviceNameList { get { return _DeviceNameList; } }
        private static List<string> _DeviceNameList;
      

        [Category("Device|Camera")]
        [ItemsSourceProperty("DeviceNameList")]
        public string DeviceName {
            get {
                return _DeviceName;
            }
            set {
                _DeviceName = value;

                //선택한 장치의 현재 property 를 가져온다.
                if (pDev == null) return;
                var selectedDev = pDev[value];
                if (selectedDev == null) return;
                this.PasteFromCamera(selectedDev);
            }
        }
        private string _DeviceName;


        [Browsable(false)]
        public string[] PropertyNameList { get; }


        [Category("Device|Camera")]
        [HeaderPlacement(HeaderPlacement.Collapsed)]
        public PropertyItem[] PropertyArray { get; set; }


        public CameraParam(object parent) :base(parent) {
            pDev = SystemHandler.Handle.Devices;
            pLight = SystemHandler.Handle.Lights;

            this.PropertyNameList = Enum.GetNames(typeof(ECameraPropertyType));
            this.PropertyArray = new PropertyItem[PropertyNameList.Length];
            for (int i = 0; i < this.PropertyArray.Length; i++) {
                this.PropertyArray[i] = new PropertyItem(PropertyNameList[i]);
            }

            if (_DeviceNameList == null) {
                _DeviceNameList = new List<string>();
                for (int i = 0; i < pDev.Count; i++) {
                    _DeviceNameList.Add(pDev[i].Name);
                }
            }

            if (_LightGroupList == null) {
                _LightGroupList = new List<string>();
                for (int i = 0; i < pLight.Groups.Count; i++) {
                    _LightGroupList.Add(pLight.Groups[i].Name);
                }
            }
        }
        
        private PropertyItem SearchProperty(ECameraPropertyType type) {
            for(int i = 0; i< PropertyArray.Length; i++) {
                if (PropertyArray[i].GetPropertyType() == type) return PropertyArray[i];
            }
            return null;
        }

        public void PasteFromCamera(VirtualCamera camera) {
            for(int i = 0; i < camera.Properties.Count; i++) {
                ECameraPropertyType type = camera.Properties.GetPropType(i);
                decimal value = camera.Properties[type];
                PropertyItem item = SearchProperty(type);
                if (item == null) continue;
                item.Value = (double)value;
            }
        }

        public void CopyToCamera(VirtualCamera camera) {
            for(int i = 0; i < PropertyArray.Length; i++) {
                ECameraPropertyType type = PropertyArray[i].GetPropertyType();
                camera.Properties[type] = (decimal)PropertyArray[i].Value;
            }
        }
        
        [Browsable(false)]
        public decimal this[int idx] {
            get {
                if (idx >= PropertyArray.Length) return 0;
                return (decimal)PropertyArray.ElementAt(idx).Value;
            }
        }

        [Browsable(false)]
        public decimal this[string propName] {
            get {
                ECameraPropertyType type = (ECameraPropertyType)Enum.Parse(typeof(ECameraPropertyType), propName);
                foreach(PropertyItem info in PropertyArray) {
                    if(info.GetPropertyType() == type) return (decimal)info.Value;
                }
                return 0;
            }
        }
        
        public override bool Load(IniFile loadFile, string groupName) {
            return base.Load(loadFile, groupName);
        }

        public override bool Save(IniFile saveFile, string groupName) {
            return base.Save(saveFile, groupName);
        }

        [Browsable(false)]
        public string SequenceName {
            get {
                return Parent.Name;
            }
        }

        [Browsable(false)]
        public string ActionName
        {
            get
            {
                if (Owner is ActionBase)
                {
                    return (Owner as ActionBase).Name;
                }
                return null;
            }
        }

        public virtual void PutImage(HImage image) {
            throw new NotImplementedException();
        }

        public virtual void PutImage(Mat image) {
            throw new NotImplementedException();
        }

        public override bool CopyTo(ParamBase param) {
            base.CopyTo(param);
            
            if (param is CameraMasterParam) {
                CameraMasterParam masterParam = param as CameraMasterParam;
                return false;
            }
            else if (param is CameraSlaveParam) {
                CameraSlaveParam slaveParam = param as CameraSlaveParam;

                slaveParam.LightLevel = this.LightLevel;

                for(int i = 0; i < this.PropertyNameList.Length; i++) {
                    if(this.PropertyNameList[i] == slaveParam.PropertyNameList[i]) {
                        slaveParam.PropertyArray[i].Value = this.PropertyArray[i].Value;
                    }
                }

                slaveParam.PartNo = this.PartNo;
                slaveParam.MotorXPos = this.MotorXPos;
                slaveParam.MotorYPos = this.MotorYPos;
                slaveParam.FrameWidth = this.FrameWidth;
                slaveParam.FrameHeight = this.FrameHeight;
                //slaveParam.PixelToMM_Offset = this.PixelToMM_Offset;
                //slaveParam.PixelToUM_Offset = this.PixelToUM_Offset;      // 02.14
                slaveParam.PixelToUM_Offset = this.PixelToUM_Offset;      // 02.14
                return true;
            }
            else if (param is CameraParam) {
                CameraParam camParam = param as CameraParam;
                //camParam.DeviceName = this.DeviceName;
                //camParam.LightGroupName = this.LightGroupName;
                camParam.LightLevel = this.LightLevel;

                for (int i = 0; i < this.PropertyNameList.Length; i++) {
                    if (this.PropertyNameList[i] == camParam.PropertyNameList[i]) {
                        camParam.PropertyArray[i].Value = this.PropertyArray[i].Value;
                    }
                }

                camParam.PartNo = this.PartNo;
                camParam.MotorXPos = this.MotorXPos;
                camParam.MotorYPos = this.MotorYPos;
                camParam.FrameWidth = this.FrameWidth;
                camParam.FrameHeight = this.FrameHeight;
                //camParam.PixelToMM_Offset = this.PixelToMM_Offset;
                camParam.PixelToUM_Offset = this.PixelToUM_Offset;
                return true;
            }
            return false;
        }
    }
}
