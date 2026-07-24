using Newtonsoft.Json;
using PropertyTools.DataAnnotations;
using ReringProject.Setting;
using ReringProject.UI;
using ReringProject.Utility;
using System;
using System.IO;
using System.Linq;

namespace ReringProject.Device {
    // 260723 hbk: MIL(CXP, Top/Side/Bottom 공용) 카메라 Exposure/Gain/Gamma 실 HW 제어 시도.
    //  Basler/HikCameraProperty 와 동일 패턴(PropertyItem 어노테이션 → base 생성자 reflection 이 Values 에 자동 등록).
    //  실제 하드웨어 연동은 MilCamera.TryReadFeature/TryWriteFeature(GenICam feature, MdigControlFeature/
    //  MdigInquireFeature 경유)를 통한다. Width/Height/PixelFormat 은 이 장비에서 같은 API가
    //  "Requested operation not supported"로 실패한 이력이 있어(MilCamera.Open() NOTE 참고),
    //  ExposureTime/Gain/Gamma 도 실기에서 동일하게 지원 안 될 수 있음 — 반드시 실장비로 검증 필요.
    public class MilCameraProperty : VirtualCameraProperty {
        private MilCamera pHandle = null;

        [Category("MIL")]
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

        [Browsable(false)]   //260723 hbk: 이 카메라(CXP/MIL)는 Gamma feature 자체가 없음(MIL error 6501 Feature Access Error, 실기 확인) — Basler/HikCameraProperty와 동일하게 PropertyGrid에서 숨김.
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

        public MilCameraProperty(MilCamera handle) : base() {
            pHandle = handle;
        }

        // GenICam SFNC(표준 feature 명) 매핑. Gain 은 카메라 벤더에 따라 "GainRaw"(정수, 구형 GigE Vision)일 수도
        //  있으나, CXP/3세대 GigE Vision 계열은 대부분 "Gain"(실수, dB) 표준명을 지원 — 실기에서 M_FEATURE_NOT_AVAILABLE
        //  이 뜨면 여기서 이름만 교체하면 된다(하드웨어 연동 지점 단일화).
        private static string FeatureNameOf(ECameraPropertyType type) {
            switch (type) {
                case ECameraPropertyType.Exposure: return "ExposureTime";
                case ECameraPropertyType.Gain: return "Gain";
                case ECameraPropertyType.Gamma: return "Gamma";
                default: return null;
            }
        }

        public override void Update() {
            base.Update();
            if (pHandle == null) return;
            SaveToJson(Path.Combine(SystemSetting.Handle.CameraConfigPath, pHandle.Name + ".cfg"));
        }

        protected override bool ReadProperty(ECameraPropertyType type, out decimal value) {
            value = 0;
            if (pHandle == null) return false;
            string featureName = FeatureNameOf(type);
            if (featureName == null) return false;
            double dValue;
            if (!pHandle.TryReadFeature(featureName, out dValue)) return false;
            value = (decimal)dValue;
            Values[type].Value = value;
            return true;
        }

        protected override bool WriteProperty(ECameraPropertyType type, decimal value) {
            if (pHandle == null) return false;
            string featureName = FeatureNameOf(type);
            if (featureName == null) return false;
            if (!pHandle.TryWriteFeature(featureName, (double)value)) return false;
            Values[type].Value = value;
            RaisePropertyChanged(type.ToString());
            return true;
        }

        public void SaveToJson(string fileName) {
            try {
                StreamWriter saveFile = File.CreateText(fileName);
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
                Logging.PrintLog((int)ELogType.Error, "[ERROR] {0} MilCameraProperty.SaveToJson ({1})", pHandle != null ? pHandle.Name : "", e.Message);
            }
        }
    }
}
