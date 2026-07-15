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
    
    public class CameraSlaveParam : ParamBase, ICameraParam {
        [Browsable(false)]
        private DeviceHandler pDev;

        [Browsable(false)]
        private LightHandler pLight;
        

        [Category("General|AOI")]
        public double PixelToUM_Offset { get; set; }
        [System.ComponentModel.Description("mm/pixel calibration factor for this camera")]
        public double PixelResolution { get; set; } = 1.0;  //260408 hbk mm/pixel (per D-12)
        //260619 hbk per-shot 측정 보정계수 — 비전측정↔현미경공칭 ~0.5% 캘리브 간극 보정 layer. PixelResolution(고정) 위 런타임 곱. 기본 1.0=무보정(회귀0). ParamBase INI 자동 직렬화, 키 미존재 시 1.0 폴백.
        [System.ComponentModel.Description("Per-shot measurement correction factor (multiplies PixelResolution). 1.0 = no correction.")]
        public double CorrectionFactor { get; set; } = 1.0;  //260619 hbk

        public double MotorXPos { get; set; }
        public double MotorYPos { get; set; }
        public int FrameWidth { get; set; }
        public int FrameHeight { get; set; }
        public int PartNo { get; set; }


        // ShotConfig/TopInspectionParam/BottomInspectionParam(=CameraSlaveParam 파생) 는 이 필드를 세팅한 적이 없다
        // (LightGroupName 은 CameraMasterParam.DefaultLight 로만 세팅됨) — 항상 빈 값으로만 보이는 legacy 단일그룹
        // API 잔재라 PropertyGrid 에서 숨긴다. 채널별 조명은 Light 탭(Category="Light|...")으로 대체됨.
        [Category("Device|Light")]
        [ReadOnly(true)]
        [Browsable(false)]
        public string LightGroupName {
            get {
                return _LightGroupName;
            }
            set {
                if (value == null) return;
                _LightGroupName = value;
            }
        }
        private string _LightGroupName;

        [Browsable(false)]
        public int LightLevel { get; set; }

        [Category("Device|Camera")]
        [ReadOnly(true)]
        public string DeviceName {
            get {
                return _DeviceName;
            }
            set {
                if (value == null) return;

                _DeviceName = value;

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


        public CameraSlaveParam(object owner) :base(owner) {
            pDev = SystemHandler.Handle.Devices;
            pLight = SystemHandler.Handle.Lights;

            this.PropertyNameList = Enum.GetNames(typeof(ECameraPropertyType));
            this.PropertyArray = new PropertyItem[PropertyNameList.Length];
            for (int i = 0; i < this.PropertyArray.Length; i++) {
                this.PropertyArray[i] = new PropertyItem(PropertyNameList[i]);
            }
        }

        public virtual double ConvertPixelToMM(double pixel) {
            double mm = pixel * PixelToUM_Offset / 1000;
            return mm;
        }

        //260619 hbk per-shot 보정계수 적용된 유효 분해능 = PixelResolution × CorrectionFactor. 측정 mm 소비 단일소스(Action_FAIMeasurement :265 + EdgePairDistance :74 양 경로 호출). 메서드 = INI 직렬화 안 됨 → PixelResolution 저장값 불변 보존.
        public double GetEffectivePixelResolution() {
            //260619 hbk CorrectionFactor ≤0(미설정 0 로드 잔재 or 오입력)은 무보정 1.0 으로 안전 처리 — 측정값 0/음수화 방지. >2% 이탈은 가드레일이 별도 경고.
            double factor = (CorrectionFactor > 0.0) ? CorrectionFactor : 1.0;
            return PixelResolution * factor;
        }


        private PropertyItem SearchProperty(ECameraPropertyType type) {
            for(int i = 0; i< PropertyArray.Length; i++) {
                if (PropertyArray[i].GetPropertyType() == type) return PropertyArray[i];
            }
            return null;
        }

        public void PasteFromCamera(VirtualCamera camera) {
            if(camera.Properties == null) {
                return;
            }
            for(int i = 0; i < camera.Properties.Count; i++) {
                ECameraPropertyType type = camera.Properties.GetPropType(i);
                decimal value = camera.Properties[type];
                PropertyItem item = SearchProperty(type);
                if (item == null) continue;
                item.Value = (double)value;
            }
        }

        [Browsable(false)]
        public string SequenceName {
            get {
                if (Parent == null) return null; //260407 hbk 동적 생성 Param은 Parent가 null일 수 있으므로 null 안전 접근
                return Parent.Name; //260612 hbk Wave5
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
            bool result = base.Load(loadFile, groupName);
            //260619 hbk CorrectionFactor 하위호환 복원 — ParamBase.Load 는 Double 프로퍼티마다 INI 를 읽어 누락 키를 ToDouble()=0 으로 덮어쓴다.
            //  구 레시피엔 CorrectionFactor 키가 없어 0 으로 로드 → GetEffectivePixelResolution=PixelResolution×0=0 → 전 측정값 0(회귀).
            //  키 부재 시에만 기본 1.0(무보정) 복원. 키 존재(사용자 설정값)면 그대로 둠.
            IniSection sec;
            if (!loadFile.TryGetSection(groupName, out sec) || sec == null || !sec.ContainsKey("CorrectionFactor")) {
                CorrectionFactor = 1.0;
            }
            return result;
        }

        public override bool Save(IniFile saveFile, string groupName) {
            return base.Save(saveFile, groupName);
        }

        public virtual void PutImage(HImage image) {
        }

        public virtual void PutImage(Mat image) {
        }

        public override bool CopyTo(ParamBase param) {
            if (param is CameraMasterParam) {
                CameraMasterParam masterParam = param as CameraMasterParam;
                return false;
            }
            else if (param is CameraSlaveParam) {
                CameraSlaveParam slaveParam = param as CameraSlaveParam;
                slaveParam.LightLevel = this.LightLevel;

                for (int i = 0; i < this.PropertyNameList.Length; i++) {
                    if (this.PropertyNameList[i] == slaveParam.PropertyNameList[i]) {
                        slaveParam.PropertyArray[i].Value = this.PropertyArray[i].Value;
                    }
                }

                slaveParam.PartNo = this.PartNo;
                slaveParam.MotorXPos = this.MotorXPos;
                slaveParam.MotorYPos = this.MotorYPos;
                slaveParam.FrameWidth = this.FrameWidth;
                slaveParam.FrameHeight = this.FrameHeight;
                slaveParam.PixelToUM_Offset = this.PixelToUM_Offset;
                return true;
            }
            else if (param is CameraParam) {
                CameraParam camParam = param as CameraParam;
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
                camParam.PixelToUM_Offset = this.PixelToUM_Offset;
                return true;
            }
            return false;
        }
    }
}
