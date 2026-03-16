
using Newtonsoft.Json;
using PropertyTools.DataAnnotations;
using ReringProject.UI;
using ReringProject.Utility;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MvCamCtrl.NET.CameraParams;
using MvCamCtrl.NET;
using ReringProject.Setting;

namespace ReringProject.Device {
    public class HikCameraProperty : VirtualCameraProperty{
        private HikCamera pHandle = null;
        
        private CFloatValue floatVal = new CFloatValue();
        private CIntValue intVal = new CIntValue();
        //private MyCamera.MVCC_ENUMVALUE enumVal = new MyCamera.MVCC_ENUMVALUE();

        [Category("HIK")]
        [PropertyItem(ECameraPropertyType.Exposure)]
        [Slidable(DeviceHandler.MIN_EXPOSURE, DeviceHandler.MAX_EXPOSURE, TickFrequency = DeviceHandler.TICK_EXPOSURE)]
        [FormatString("0.00")]
        public double Exposure {
            get {
                ReadProperty(ECameraPropertyType.Exposure, out decimal value);
                return (double)value;
            }
            set {
                WriteProperty(ECameraPropertyType.Exposure, (decimal)value);
            }
        }

        [PropertyItem(ECameraPropertyType.Gamma)]
        [Slidable(DeviceHandler.MIN_GAMMA, DeviceHandler.MAX_GAMMA, TickFrequency = DeviceHandler.TICK_GAMMA)]
        [FormatString("0.00")]
        public double Gamma {
            get {
                ReadProperty(ECameraPropertyType.Gamma, out decimal value);
                return (double)value;
            }
            set {
                WriteProperty(ECameraPropertyType.Gamma, (decimal)value);
            }
        }

        [PropertyItem(ECameraPropertyType.Gain)]
        [Slidable(DeviceHandler.MIN_GAIN, DeviceHandler.MAX_GAIN, TickFrequency = DeviceHandler.TICK_GAIN)]
        [FormatString("0.00")]
        public double Gain {
            get {
                ReadProperty(ECameraPropertyType.Gain, out decimal value);
                return (double)value;
            }
            set {
                WriteProperty(ECameraPropertyType.Gain, (decimal)value);
            }
        }
        

        public HikCameraProperty(HikCamera handle) : base() {
            pHandle = handle;
        }

        public override void Update() {
            base.Update();
            SaveToJson(AppDomain.CurrentDomain.BaseDirectory + pHandle.Name + ".cfg");
        }
        
        /// <summary>
        /// 실제 장치로부터 프로퍼티를 읽고 상태를 갱신한다.
        /// </summary>
        /// <param name="type">읽고자 하는 프로퍼티 타입</param>
        /// <param name="prop">읽은 값을 저장할 프로퍼티</param>
        /// <returns></returns>
        protected override bool ReadProperty(ECameraPropertyType type, out decimal value) {
            value = 0;
            if (pHandle == null) return false;
            if (pHandle.CameraHandle == null) return false;

            PropertyItem prop = Values[type];
            switch (type) {
                /*
                case ECameraPropertyType.Brightness:
                    break;
                case ECameraPropertyType.Contrast:
                    break;
                case ECameraPropertyType.Focus:
                    break;
                case ECameraPropertyType.Saturation:
                    break;
                case ECameraPropertyType.Whitebalance:
                    break;
                case ECameraPropertyType.BalanceRatioRed:
                    if (pHandle.CameraHandle.GetIntValue("BalancedRatioRed", ref intVal) == CErrorDefine.MV_OK) {
                        prop.Min = (decimal)intVal.Min;
                        prop.Max = (decimal)intVal.Max;
                        prop.Increment = (decimal)intVal.Inc;
                        //prop.Value = CameraHandle.Parameters[PLCamera.BalanceRatioRaw].GetValue();
                        value = (decimal)intVal.CurValue;
                    }
                    else return false;
                    break;
                case ECameraPropertyType.BalanceRatioGreen:
                    if (pHandle.CameraHandle.GetIntValue("BalancedRatioGreen", ref intVal) == CErrorDefine.MV_OK) {
                        prop.Min = (decimal)intVal.Min;
                        prop.Max = (decimal)intVal.Max;
                        prop.Increment = (decimal)intVal.Inc;
                        //prop.Value = CameraHandle.Parameters[PLCamera.BalanceRatioRaw].GetValue();
                        value = (decimal)intVal.CurValue;
                    }
                    else return false;
                    break;
                case ECameraPropertyType.BalanceRatioBlue:
                    if (pHandle.CameraHandle.GetIntValue("BalancedRatioBlue", ref intVal) == CErrorDefine.MV_OK) {
                        prop.Min = (decimal)intVal.Min;
                        prop.Max = (decimal)intVal.Max;
                        prop.Increment = (decimal)intVal.Inc;
                        //prop.Value = CameraHandle.Parameters[PLCamera.BalanceRatioRaw].GetValue();
                        value = (decimal)intVal.CurValue;
                    }
                    else return false;
                    break;
                    */
                case ECameraPropertyType.Exposure:
                    if (pHandle.CameraHandle.GetFloatValue("ExposureTime", ref floatVal) == CErrorDefine.MV_OK) {
                        prop.Min = (decimal)floatVal.Min;
                        prop.Max = (decimal)floatVal.Max;
                        prop.Increment = (decimal)0.1f; //CameraHandle.Parameters[PLCamera.ExposureTimeAbs].GetIncrement();
                        value = (decimal)floatVal.CurValue;
                    }
                    else return false;
                    break;
                    
                case ECameraPropertyType.Gain:
                    if (pHandle.CameraHandle.GetFloatValue("Gain", ref floatVal) == CErrorDefine.MV_OK) {
                        prop.Min = (decimal)floatVal.Min;
                        prop.Max = (decimal)floatVal.Max;
                        prop.Increment = (decimal)0.1f;
                        value = (decimal)floatVal.CurValue;
                    }
                    else return false;
                    break;
                case ECameraPropertyType.Gamma:
                    if (pHandle.CameraHandle.GetFloatValue("Gamma", ref floatVal) == CErrorDefine.MV_OK) {
                        prop.Min = (decimal)floatVal.Min;
                        prop.Max = (decimal)floatVal.Max;
                        prop.Increment = (decimal)0.1f;
                        value = (decimal)floatVal.CurValue;
                    }
                    else return false;
                    break;
                    /*
                case ECameraPropertyType.XPos:
                    if (pHandle.CameraHandle.GetIntValue("OffsetX", ref intVal) == CErrorDefine.MV_OK) {
                        prop.Min = (decimal)intVal.Min;
                        prop.Max = (decimal)intVal.Max;
                        prop.Increment = (decimal)intVal.Inc;
                        //prop.Value = (decimal)CameraHandle.Parameters[PLCamera.OffsetX].GetValue();
                        value = (decimal)intVal.CurValue;
                    }
                    else return false;
                    break;
                //return (decimal)CameraHandle.Parameters[PLCamera.OffsetX].GetMinimum();
                case ECameraPropertyType.YPos:
                    if (pHandle.CameraHandle.GetIntValue("OffsetY", ref intVal) == CErrorDefine.MV_OK) {
                        prop.Min = (decimal)intVal.Min;
                        prop.Max = (decimal)intVal.Max;
                        prop.Increment = (decimal)intVal.Inc;
                        //prop.Value = (decimal)CameraHandle.Parameters[PLCamera.OffsetY].GetValue();
                        value = (decimal)intVal.CurValue;
                    }
                    else return false;
                    break;
                    */
            }
            prop.Value = value;
            return true;
        }

        /// <summary>
        /// 주어진 타입의 프로퍼티 값을 write한다.
        /// </summary>
        /// <param name="type">write할 프로퍼티 타입</param>
        /// <param name="value">write할 값</param>
        /// <returns>성공하면 true, 실패면 false</returns>
        protected override bool WriteProperty(ECameraPropertyType type, decimal value) {
            if (pHandle.CameraHandle == null) return false;
            try {
                switch (type) {
                    /*
                    case ECameraPropertyType.Brightness:
                        break;
                    case ECameraPropertyType.Contrast:
                        break;
                    case ECameraPropertyType.Focus:
                        break;
                    case ECameraPropertyType.Saturation:
                        break;
                    case ECameraPropertyType.Whitebalance:
                        break;
                    case ECameraPropertyType.BalanceRatioRed:
                        if (pHandle.CameraHandle.SetIntValue("BalancedRatioRed", (uint)value) != CErrorDefine.MV_OK) {
                            return false;
                        }
                        break;
                    case ECameraPropertyType.BalanceRatioBlue:
                        if (pHandle.CameraHandle.SetIntValue("BalancedRatioBlue", (uint)value) != CErrorDefine.MV_OK) {
                            return false;
                        }
                        break;
                    case ECameraPropertyType.BalanceRatioGreen:
                        if (pHandle.CameraHandle.SetIntValue("BalancedRatioGreen", (uint)value) != CErrorDefine.MV_OK) {
                            return false;
                        }
                        break;
                    */
                    case ECameraPropertyType.Exposure:
                        if (pHandle.CameraHandle.SetFloatValue("ExposureTime", (float)value) != CErrorDefine.MV_OK) {
                            return false;
                        }
                        break;
                    
                    case ECameraPropertyType.Gain:
                        if (pHandle.CameraHandle.SetFloatValue("Gain", (float)value) != CErrorDefine.MV_OK) {
                            return false;
                        }
                        break;
                    case ECameraPropertyType.Gamma:
                        if (pHandle.CameraHandle.SetFloatValue("Gamma", (float)value) != CErrorDefine.MV_OK) {
                            return false;
                        }
                        break;
                        /*
                    case ECameraPropertyType.XPos:
                        if (pHandle.CameraHandle.SetIntValue("OffsetX", (uint)value) != CErrorDefine.MV_OK) {
                            return false;
                        }
                        break;
                    case ECameraPropertyType.YPos:
                        if (pHandle.CameraHandle.SetIntValue("OffsetY", (uint)value)!= CErrorDefine.MV_OK) {
                            return false;
                        }
                        break;
                        */
                }
                Values[type].Value = value;
                RaisePropertyChanged(type.ToString());
            }
            catch (Exception e) {
                Logging.PrintLog((int)ELogType.Camera, "[ERROR] {0} Write Property. Key:{1}, ({2})", pHandle.Name, type.ToString(), e.Message);
                return false;
            }
            return true;
        }

        public override void Load(IniFile loadFile, string group) {
            base.Load(loadFile, group);
        }

        public override void Save(IniFile saveFile, string group) {
            base.Save(saveFile, group);
        }

        public void SaveToJson(string fileName) {
            try {
                StreamWriter saveFile = File.CreateText(fileName);

                //mmf or ect files
                for (int i = 0; i < Values.Count; i++) {
                    PropertyItem prop = Values.ElementAt(i).Value;
                    string json = JsonConvert.SerializeObject(prop);
                    saveFile.Write(json);
                    saveFile.WriteLine();
                }
                saveFile.Flush();
                saveFile.Close();
            }
            catch (Exception e) {
                Logging.PrintErrLog((int)ELogType.Error, string.Format("Exception ReturnCode:{0}", "HikCamera SaveToJson Exception", e.ToString()));
            }
        }
    }
}
