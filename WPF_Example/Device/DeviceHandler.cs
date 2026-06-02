using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HalconDotNet;
using ReringProject.Device;
using ReringProject.Sequence;

namespace ReringProject.Device {
    /// <summary>
    /// ?μ튂 ID
    /// </summary>
    public class DeviceInfo {
        public ECameraType CamType;
        public ECaptureImageType ImageType;
        public ETriggerSource TriggerSource;
        public string Identifier;

        public int Width;
        public int Height;

        public bool ReverseX;
        public bool ReverseY;

        public ERotateAngleType RotateAngle = 0;

        public DeviceInfo(ECameraType type, ECaptureImageType imageType, ETriggerSource triggerSource, string id, int width, int height, bool reverseX, bool reverseY, ERotateAngleType rotateAngle = ERotateAngleType._0) {
            CamType = type;
            ImageType = imageType;
            Identifier = id;

            Width = width;
            Height = height;

            ReverseX = reverseX;
            ReverseY = reverseY;
            RotateAngle = rotateAngle;
        }
    }
    
    /// <summary>
    /// ?대?吏 ?쒓났?먯쓽 Initialize() 泥섎━ 寃곌낵
    /// </summary>
    [Flags]
    public enum EInitializeResult {
        Success = 0,                                  //?깃났
        NoCamera = 1 << 0,                       //?곌껐??移대찓???놁쓬
        NotEnoughCamera = 1 << 1,            //?곌껐??移대찓??媛쒖닔 遺議?
        WrongCameraConnected = 1 << 2,   //?섎せ??移대찓???곌껐??
        OpenFail = 1 << 3,                        //?μ튂 ?닿린 ?ㅽ뙣
        Unknown = 1 << 4,                        //?뚯닔?녿뒗 ?먮윭
    }

    public sealed partial class DeviceHandler : IDisposable {
        public static DeviceHandler Handle { get; } = new DeviceHandler();

#if SIMUL_MODE
        //260317 offline auto-run test image
        private const string SimulatedImagePath = @"D:\1.bmp";
#endif

        private List<DeviceInfo> IDList = new List<DeviceInfo>();

        private Dictionary<string, VirtualCamera> Devices = new Dictionary<string, VirtualCamera>();

        public DisplayConfig Config { get; private set; } = new DisplayConfig();

        private DeviceHandler() {
            RegisterRequiredDevices();
        }

        /// <summary>
        /// SystemHandler??Initialize ?쒖젏???몄텧??
        /// 移대찓???먯썝??珥덇린??
        /// </summary>
        /// <returns></returns>
        public EInitializeResult Initialize() {
            EInitializeResult result = EInitializeResult.Success;

            //enum basler device
            int baslerConnectedCount = 0;
            if (IDList.Select(id => id.CamType == ECameraType.Basler) != null) {
                baslerConnectedCount = BaslerCamera.EnumerateDevice();
            }

            //enum hik device
            int hikConnectedCount = 0;
            if(IDList.Select(id => id.CamType == ECameraType.HIK) != null) {
                hikConnectedCount = HikCamera.EnumerateDevice();
            }
            
            int baslerCamIndex = 0;
            int hikCamIndex = 0;

            //open all enumerated devices
            for (int i = 0; i < IDList.Count; i++) {
                DeviceInfo id = IDList[i];
                ECaptureImageType imageType = IDList[i].ImageType;
                ETriggerSource triggerSource = IDList[i].TriggerSource;
                string devName = IDList[i].Identifier;

                switch (IDList[i].CamType) {
                    case ECameraType.Virtual: {
                        if (devName == null) {
                            int devIndex = Devices.Count + 1;
                            id.Identifier = "VirtualCamera" + devIndex.ToString();
                        }
                        AddVirtualCamera(id);
                    }
                    break;
                    case ECameraType.Basler: {
                        if (GetCount(ECameraType.Basler) == GetRequiredCameraCount(ECameraType.Basler)) continue;
                        
                        if (devName == null) {
                            devName = BaslerCamera.GetDeviceName(baslerCamIndex);
                            if (devName == null) {
                                result &= ~EInitializeResult.Success; //success ?쒓굅
                                result |= EInitializeResult.OpenFail;
#if SIMUL_MODE
                                AddVirtualCamera(id);
#endif
                                continue;
                            }
                            else if ((!String.IsNullOrEmpty(id.Identifier)) && (!BaslerCamera.ContainsDevice(id.Identifier))) {
                                result &= ~EInitializeResult.Success; //success ?쒓굅
                                result |= EInitializeResult.OpenFail;
#if SIMUL_MODE
                                AddVirtualCamera(id);
#endif
                                continue;
                            }
                        }

                        if (BaslerCamera.ContainsDevice(devName) == false) {
                            result &= ~EInitializeResult.Success; //success ?쒓굅
                            result |= EInitializeResult.OpenFail;
#if SIMUL_MODE
                            AddVirtualCamera(id);
#endif
                            continue;
                        }

                        //?붽뎄??移대찓?쇰? ?앹꽦?쒕떎. (?댄썑 open?섏? ?딅뜑?쇰룄, ?붽뎄???μ튂 ?대?濡?
                        BaslerCamera newCam = new BaslerCamera(Config, id);

                        //?대쫫??吏?뺣맂 寃쎌슦
                        if ((!String.IsNullOrEmpty(devName)) && (BaslerCamera.ContainsDevice(devName))) {
                            if (!newCam.Open(devName)) {
                                result &= ~EInitializeResult.Success; //success ?쒓굅
                                result |= EInitializeResult.OpenFail;
                            }
                        }
                        //?대쫫??吏?뺣릺吏 ?딆? 寃쎌슦(議고쉶???μ튂瑜??쒖감?곸쑝濡??ㅽ뵂)
                        else if (baslerCamIndex <= BaslerCamera.GetDeviceCount()) { //?곌껐??移대찓??紐⑤몢 ?ㅽ뵂   
                            if (!newCam.Open(baslerCamIndex)) {
                                result &= ~EInitializeResult.Success; //success ?쒓굅
                                result |= EInitializeResult.OpenFail;
                            }
                        }

                        //Add to List
                        Devices.Add(devName, newCam);
                        baslerCamIndex++;
                    }
                    break;
                    case ECameraType.HIK: {
                        if (GetCount(ECameraType.HIK) == GetRequiredCameraCount(ECameraType.HIK)) continue;

                        if (HikCamera.ContainsDevice(devName) == false) {
                            result &= ~EInitializeResult.Success; //success ?쒓굅
                            result |= EInitializeResult.OpenFail;
#if SIMUL_MODE
                                AddVirtualCamera(id);
#endif
                            continue;
                        }
                        if (devName == null) {
                            devName = HikCamera.GetDeviceName(hikCamIndex);
                            if (devName == null) {
                                result &= ~EInitializeResult.Success; //success ?쒓굅
                                result |= EInitializeResult.OpenFail;
#if SIMUL_MODE
                                AddVirtualCamera(id);
#endif
                                continue;
                            }
                            else if ((!String.IsNullOrEmpty(id.Identifier)) && (!HikCamera.ContainsDevice(id.Identifier))) {
                                result &= ~EInitializeResult.Success; //success ?쒓굅
                                result |= EInitializeResult.OpenFail;
#if SIMUL_MODE
                                AddVirtualCamera(id);
#endif
                                continue;
                            }
                        }

                        //?붽뎄??移대찓?쇰? ?앹꽦?쒕떎. (?댄썑 open?섏? ?딅뜑?쇰룄, ?붽뎄???μ튂 ?대?濡?
                        HikCamera newCam = new HikCamera(Config, id);

                        //?대쫫??吏?뺣맂 寃쎌슦
                        if ((!String.IsNullOrEmpty(devName)) && (HikCamera.ContainsDevice(devName))) {
                            if (!newCam.Open(devName)) {
                                result &= ~EInitializeResult.Success; //success ?쒓굅
                                result |= EInitializeResult.OpenFail;
                            }
                        }
                        //?대쫫??吏?뺣릺吏 ?딆? 寃쎌슦(議고쉶???μ튂瑜??쒖감?곸쑝濡??ㅽ뵂)
                        else if (hikCamIndex <= HikCamera.GetDeviceCount()) { //?곌껐??移대찓??紐⑤몢 ?ㅽ뵂   
                            if (!newCam.Open(hikCamIndex)) {
                                result &= ~EInitializeResult.Success; //success ?쒓굅
                                result |= EInitializeResult.OpenFail;
                            }
                        }

                        //Add to List
                        Devices.Add(devName, newCam);
                        hikCamIndex++;
                    }
                    break;
                    case ECameraType.MIL: {
                        //260602 hbk Phase 41 — CXP 카메라 MIL grab. enumerate 없음, Open() 으로 판별.
                        MilCamera newCam = new MilCamera(Config, id);
                        if (!newCam.Open()) {
                            result &= ~EInitializeResult.Success;
                            result |= EInitializeResult.OpenFail;
#if SIMUL_MODE
                            AddVirtualCamera(id);   // 보드 미설치 시 VirtualCamera 파일 grab 폴백 (Pitfall 1)
#endif
                            continue;
                        }
                        Devices.Add(id.Identifier, newCam);
                    }
                    break;

                }
            }
            IDList.Clear();

            return result;
        }

        public VirtualCamera this[string name] {
            get {
                if (name == null) return null;
                if (Devices.ContainsKey(name) == false) return null;
                return Devices[name];
            }
        }

        public VirtualCamera this[int index] {
            get {
                if (index >= Devices.Count) return null;
                return Devices.ElementAt(index).Value;
            }
        }

        public int Count { get => Devices.Count; }

        public int GetCount(ECameraType type) {
            return Devices.Count(d => d.Value.CamType == type);
        }

        public int IndexOf(string name) {
            for(int i = 0; i < Count; i++) {
                if (this[i].Name == name) return i;
            }
            return -1;
        }

        public void StopStreamAll() {
            for(int i = 0; i < Devices.Count; i++) {
                Devices.ElementAt(i).Value.StopStream();
            }
        }

        public void SetRequiredDevice(ECameraType type, ECaptureImageType imageType, ETriggerSource triggerSource, string identifier, int width, int height, bool reverseX, bool reverseY, ERotateAngleType rotateAngle = 0) {
            DeviceInfo newID = new DeviceInfo(type, imageType, triggerSource, identifier, width, height, reverseX, reverseY, rotateAngle);
            IDList.Add(newID);
        }

        private int GetRequiredCameraCount(ECameraType type) {
            return IDList.Count(d => d.CamType == type);
        }

        private void AddVirtualCamera(DeviceInfo id) {
            if (String.IsNullOrEmpty(id.Identifier)) id.Identifier = string.Format("Virtual Camera {0}", GetCount(ECameraType.Virtual) + 1);
            VirtualCamera vCam = new VirtualCamera(Config, id);
            vCam.Open(id.Width, id.Height);
#if SIMUL_MODE
            //260317 offline auto-run test image
            vCam.BackgroundImagePath = SimulatedImagePath;
#endif
            Devices.Add(id.Identifier, vCam);
        }
        
        /// <summary>
        /// 吏?뺣맂 媛쒖닔留뚰겮 紐⑤뱺 ?μ튂媛 ?ㅽ뵂?섏뿀?붿? ?뺤씤?쒕떎.
        /// </summary>
        /// <returns>?붽뎄??媛쒖닔蹂대떎 ?곌껐??媛쒖닔媛 ?곸? 寃쎌슦 false 諛섑솚</returns>
        public bool IsAllOpen() {
            if (GetRequiredCameraCount(ECameraType.Basler) > GetCount(ECameraType.Basler)) return false;

            return true;
        }

        public bool ApplyProperty(ICameraParam param) {
            VirtualCamera cam = this[param.DeviceName];
            if (cam == null) return false;
            if (cam.Properties == null) return false;
            return cam.Properties.ApplyFromParam(param);
        }

        public HImage GrabHalconImage(ICameraParam param) {
            VirtualCamera cam = this[param.DeviceName];
            if (cam == null) return null;
            if (cam.Properties == null) return null;
            if (!cam.Properties.ApplyFromParam(param)) return null;
            return cam.GrabHalconImage();
        }
        

        public void Dispose() {
            //Config.Save();

            for (int i = 0; i < Devices.Values.Count; i++) {
                VirtualCamera cam = Devices.Values.ElementAt(i);
                cam.Close();
                cam = null;
            }
            Devices.Clear();
        }
        
    }
}

