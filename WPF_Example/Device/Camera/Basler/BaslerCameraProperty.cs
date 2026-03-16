using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Basler.Pylon;
using Newtonsoft.Json;
using PropertyTools.DataAnnotations;
using ReringProject.Define;
using ReringProject.Setting;
using ReringProject.UI;
using ReringProject.Utility;

namespace ReringProject.Device {
    public partial class BaslerCameraProperty : VirtualCameraProperty {
        private BaslerCamera pHandle = null;

        [Category("Basler")]
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

        public BaslerCameraProperty(BaslerCamera handle) : base() {
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
                    //우선 red로 변경함
                    if (pHandle.CameraHandle.Parameters[PLCamera.BalanceRatioSelector].IsWritable) {
                        pHandle.CameraHandle.Parameters[PLCamera.BalanceRatioSelector].SetValue(PLCamera.BalanceRatioSelector.Red);
                    }
                    else return false;

                    if (pHandle.CameraHandle.Parameters[PLCamera.BalanceRatioRaw].IsReadable) {
                        prop.Min = pHandle.CameraHandle.Parameters[PLCamera.BalanceRatioRaw].GetMinimum();
                        prop.Max = pHandle.CameraHandle.Parameters[PLCamera.BalanceRatioRaw].GetMaximum();
                        prop.Increment = pHandle.CameraHandle.Parameters[PLCamera.BalanceRatioRaw].GetIncrement();
                        //prop.Value = CameraHandle.Parameters[PLCamera.BalanceRatioRaw].GetValue();
                        value = pHandle.CameraHandle.Parameters[PLCamera.BalanceRatioRaw].GetValue();
                    }
                    else return false;
                    break;
                case ECameraPropertyType.BalanceRatioGreen:
                    if (pHandle.CameraHandle.Parameters[PLCamera.BalanceRatioSelector].IsWritable) {
                        pHandle.CameraHandle.Parameters[PLCamera.BalanceRatioSelector].SetValue(PLCamera.BalanceRatioSelector.Green);
                    }
                    else return false;
                    if (pHandle.CameraHandle.Parameters[PLCamera.BalanceRatioRaw].IsReadable) {
                        prop.Min = pHandle.CameraHandle.Parameters[PLCamera.BalanceRatioRaw].GetMinimum();
                        prop.Max = pHandle.CameraHandle.Parameters[PLCamera.BalanceRatioRaw].GetMaximum();
                        prop.Increment = pHandle.CameraHandle.Parameters[PLCamera.BalanceRatioRaw].GetIncrement();
                        //prop.Value = CameraHandle.Parameters[PLCamera.BalanceRatioRaw].GetValue();
                        value = pHandle.CameraHandle.Parameters[PLCamera.BalanceRatioRaw].GetValue();
                    }
                    else return false;
                    break;
                case ECameraPropertyType.BalanceRatioBlue:
                    if (pHandle.CameraHandle.Parameters[PLCamera.BalanceRatioSelector].IsWritable) {
                        pHandle.CameraHandle.Parameters[PLCamera.BalanceRatioSelector].SetValue(PLCamera.BalanceRatioSelector.Blue);
                    }
                    else return false;
                    if (pHandle.CameraHandle.Parameters[PLCamera.BalanceRatioRaw].IsReadable) {
                        prop.Min = pHandle.CameraHandle.Parameters[PLCamera.BalanceRatioRaw].GetMinimum();
                        prop.Max = pHandle.CameraHandle.Parameters[PLCamera.BalanceRatioRaw].GetMaximum();
                        prop.Increment = pHandle.CameraHandle.Parameters[PLCamera.BalanceRatioRaw].GetIncrement();
                        //prop.Value = CameraHandle.Parameters[PLCamera.BalanceRatioRaw].GetValue();
                        value = pHandle.CameraHandle.Parameters[PLCamera.BalanceRatioRaw].GetValue();
                    }
                    else return false;
                    break;
                */
                case ECameraPropertyType.Exposure:
                    if (pHandle.CameraHandle.Parameters[PLCamera.ExposureTimeAbs].IsReadable) {
                        prop.Min = (decimal)pHandle.CameraHandle.Parameters[PLCamera.ExposureTimeAbs].GetMinimum();
                        prop.Max = (decimal)pHandle.CameraHandle.Parameters[PLCamera.ExposureTimeAbs].GetMaximum();
                        prop.Increment = (decimal)0.1f; //CameraHandle.Parameters[PLCamera.ExposureTimeAbs].GetIncrement();
                        value = (decimal)pHandle.CameraHandle.Parameters[PLCamera.ExposureTimeAbs].GetValue();
                    }
                    else return false;
                    break;
                
                case ECameraPropertyType.Gain:
                    if (pHandle.CameraHandle.Parameters[PLCamera.GainRaw].IsReadable) {
                        prop.Min = (decimal)pHandle.CameraHandle.Parameters[PLCamera.GainRaw].GetMinimum();
                        prop.Max = (decimal)pHandle.CameraHandle.Parameters[PLCamera.GainRaw].GetMaximum();
                        prop.Increment = (decimal)pHandle.CameraHandle.Parameters[PLCamera.GainRaw].GetIncrement();
                        value = (decimal)pHandle.CameraHandle.Parameters[PLCamera.GainRaw].GetValue();
                    }
                    else return false;
                    break;
                case ECameraPropertyType.Gamma:
                    if (pHandle.CameraHandle.Parameters[PLCamera.Gamma].IsReadable) {
                        prop.Min = (decimal)pHandle.CameraHandle.Parameters[PLCamera.Gamma].GetMinimum();
                        prop.Max = (decimal)pHandle.CameraHandle.Parameters[PLCamera.Gamma].GetMaximum();
                        prop.Increment = (decimal)0.1f;
                        value = (decimal)pHandle.CameraHandle.Parameters[PLCamera.Gamma].GetValue();
                    }
                    else return false;
                    break;
               /*
                case ECameraPropertyType.XPos:
                    if (pHandle.CameraHandle.Parameters[PLCamera.OffsetX].IsReadable) {
                        prop.Min = (decimal)pHandle.CameraHandle.Parameters[PLCamera.OffsetX].GetMinimum();
                        prop.Max = (decimal)pHandle.CameraHandle.Parameters[PLCamera.OffsetX].GetMaximum();
                        prop.Increment = (decimal)pHandle.CameraHandle.Parameters[PLCamera.OffsetX].GetIncrement();
                        //prop.Value = (decimal)CameraHandle.Parameters[PLCamera.OffsetX].GetValue();
                        value = (decimal)pHandle.CameraHandle.Parameters[PLCamera.OffsetX].GetValue();
                    }
                    else return false;
                    break;
                //return (decimal)CameraHandle.Parameters[PLCamera.OffsetX].GetMinimum();
                case ECameraPropertyType.YPos:
                    if (pHandle.CameraHandle.Parameters[PLCamera.OffsetY].IsReadable) {
                        prop.Min = (decimal)pHandle.CameraHandle.Parameters[PLCamera.OffsetY].GetMinimum();
                        prop.Max = (decimal)pHandle.CameraHandle.Parameters[PLCamera.OffsetY].GetMaximum();
                        prop.Increment = (decimal)pHandle.CameraHandle.Parameters[PLCamera.OffsetY].GetIncrement();
                        //prop.Value = (decimal)CameraHandle.Parameters[PLCamera.OffsetY].GetValue();
                        value = (decimal)pHandle.CameraHandle.Parameters[PLCamera.OffsetY].GetValue();
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
                        if (pHandle.CameraHandle.Parameters[PLCamera.BalanceRatioSelector].IsWritable) {
                            pHandle.CameraHandle.Parameters[PLCamera.BalanceRatioSelector].SetValue(PLCamera.BalanceRatioSelector.Red);
                        }
                        if (pHandle.CameraHandle.Parameters[PLCamera.BalanceRatioRaw].IsWritable) {
                            pHandle.CameraHandle.Parameters[PLCamera.BalanceRatioRaw].SetValue((long)value);
                        }
                        break;
                    case ECameraPropertyType.BalanceRatioBlue:
                        if (pHandle.CameraHandle.Parameters[PLCamera.BalanceRatioSelector].IsWritable) {
                            pHandle.CameraHandle.Parameters[PLCamera.BalanceRatioSelector].SetValue(PLCamera.BalanceRatioSelector.Blue);
                        }
                        if (pHandle.CameraHandle.Parameters[PLCamera.BalanceRatioRaw].IsWritable) {
                            pHandle.CameraHandle.Parameters[PLCamera.BalanceRatioRaw].SetValue((long)value);
                        }
                        break;
                    case ECameraPropertyType.BalanceRatioGreen:
                        if (pHandle.CameraHandle.Parameters[PLCamera.BalanceRatioSelector].IsWritable) {
                            pHandle.CameraHandle.Parameters[PLCamera.BalanceRatioSelector].SetValue(PLCamera.BalanceRatioSelector.Green);
                        }
                        if (pHandle.CameraHandle.Parameters[PLCamera.BalanceRatioRaw].IsWritable) {
                            pHandle.CameraHandle.Parameters[PLCamera.BalanceRatioRaw].SetValue((long)value);
                        }
                        break;
                    */
                    case ECameraPropertyType.Exposure:
                        if (pHandle.CameraHandle.Parameters[PLCamera.ExposureTimeAbs].IsWritable) {
                            return pHandle.CameraHandle.Parameters[PLCamera.ExposureTimeAbs].TrySetValue((double)value);
                        }
                        break;
                    
                    case ECameraPropertyType.Gain:
                        if (pHandle.CameraHandle.Parameters[PLCamera.GainRaw].IsWritable) {
                            return pHandle.CameraHandle.Parameters[PLCamera.GainRaw].TrySetValue((int)value);
                        }
                        break;
                    case ECameraPropertyType.Gamma:
                        if (pHandle.CameraHandle.Parameters[PLCamera.Gamma].IsWritable) {
                            return pHandle.CameraHandle.Parameters[PLCamera.Gamma].TrySetValue((int)value);
                        }
                        break;
                   /*
                    case ECameraPropertyType.XPos:
                        if (pHandle.CameraHandle.Parameters[PLCamera.OffsetX].IsWritable) {
                            return pHandle.CameraHandle.Parameters[PLCamera.OffsetX].TrySetValue((int)value);
                        }
                        break;
                    case ECameraPropertyType.YPos:
                        if (pHandle.CameraHandle.Parameters[PLCamera.OffsetY].IsWritable) {
                            return pHandle.CameraHandle.Parameters[PLCamera.OffsetY].TrySetValue((int)value);
                        }
                        break;
                        */
                }
                Values[type].Value = value;
                RaisePropertyChanged(type.ToString());
            }
            catch (Exception e) {
                Logging.PrintLog((int)ELogType.Camera, "[ERROR] {0} Write Property. ({2})", pHandle.Name, e.Message);
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
                Debug.WriteLine($"[303-BaslerCameraProperty.cs] Exception: {e.ToString()}");
            }
        }
    }
}
