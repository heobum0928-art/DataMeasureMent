using HalconDotNet;
using ReringProject.Halcon;
using ReringProject.Setting;
using ReringProject.Utility;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using MvCamCtrl.NET;
using MvCamCtrl.NET.CameraParams;

namespace ReringProject.Device {

    public enum EGrabStateType {
        Ready,
        Grabbing,
        Done,
        Fail,
    }
    public partial class HikCamera : VirtualCamera, IDisposable {
        //static member
        private static Dictionary<string, CCameraInfo> DeviceList = new Dictionary<string, CCameraInfo>();
        private static List<CCameraInfo> mInternalDeviceList = new List<CCameraInfo>();


        private Stopwatch mStopwatch = new Stopwatch();
        public CCamera CameraHandle { get; private set; }
        private CCameraInfo CameraInfo;
        private EGrabStateType GrabState { get; set; } = EGrabStateType.Ready;
        public bool IsEnableGrabEvent { get; set; } = true;


        public override event StateEvent GuiReadyForDisplay = null;

        //hik callback
        private cbEventdelegateEx EventCallback;
        private cbExceptiondelegate ExceptionCallback;
        private cbOutputExdelegate GrabCallback;

        public static int GetDeviceCount() {
            return DeviceList.Count;
        }

        /// <summary>
        /// Basler 장치 정보를 반환
        /// </summary>
        /// <param name="identifier">식별자</param>
        /// <returns>해당 장치 정보, 없으면 null</returns>
        public static CCameraInfo GetDeviceInfo(string identifier) {
            if (DeviceList.ContainsKey(identifier)) return DeviceList[identifier];

            for (int i = 0; i < DeviceList.Count; i++) {
                if (identifier == GetDeviceUserDefinedName(i)) return DeviceList.ElementAt(i).Value;
                if (identifier == GetDeviceIpAddress(i)) return DeviceList.ElementAt(i).Value;
                if (identifier == GetDeviceFriendlyName(i)) return DeviceList.ElementAt(i).Value;
            }
            return null;
        }

        /// <summary>
        /// hik 장치 정보를 반환
        /// </summary>
        /// <param name="identifier">인덱스</param>
        /// <returns>해당 장치 정보, 없으면 -1</returns>
        public static int GetDeviceIndex(string identifier) {
            if (DeviceList.ContainsKey(identifier)) return DeviceList.Keys.ToList().IndexOf(identifier);

            for (int i = 0; i < DeviceList.Count; i++) {
                if (identifier == GetDeviceUserDefinedName(i)) return i;
                if (identifier == GetDeviceIpAddress(i)) return i;
                if (identifier == GetDeviceFriendlyName(i)) return i;
            }
            return -1;
        }

        /// <summary>
        /// identifier 이름의 장치가 존재하는지 확인
        /// </summary>
        /// <param name="identifier">식별자</param>
        /// <returns>존재하면 true, 없으면 false</returns>
        public static bool ContainsDevice(string identifier) {
            if (String.IsNullOrEmpty(identifier)) return false;
            if (GetDeviceInfo(identifier) != null) return true;
            return false;
        }

        /// <summary>
        /// 주어진 index에 해당하는 장치 정보를 반환
        /// </summary>
        /// <param name="index">장치 index</param>
        /// <returns>장치 정보, 없으면 null</returns>
        public static CCameraInfo GetDeviceInfo(int index) {
            if (index > DeviceList.Count - 1) return null;
            return DeviceList.ElementAt(index).Value;
        }

        /// <summary>
        /// 주어진 index에 해당하는 friendly 장치 이름을 반환
        /// </summary>
        /// <param name="index">장치 index</param>
        /// <returns>장치 이름</returns>
        public static string GetDeviceFriendlyName(int index) {
            if (index > DeviceList.Count - 1) return null;
            CCameraInfo device = DeviceList.ElementAt(index).Value;
            
            if (device.nTLayerType == CSystem.MV_GIGE_DEVICE) {

                CGigECameraInfo gigeInfo = (CGigECameraInfo)device;
                return gigeInfo.chModelName + " (" + gigeInfo.chSerialNumber + ")";
            }
            else if (device.nTLayerType == CSystem.MV_USB_DEVICE) {

                CUSBCameraInfo usbInfo = (CUSBCameraInfo)device;
                return usbInfo.chModelName + "(" + usbInfo.chSerialNumber + ")";
            }
            return null;
        }

        /// <summary>
        /// 주어진 index에 해당하는 사용자 정의된 장치 이름을 반환
        /// </summary>
        /// <param name="index">장치 index</param>
        /// <returns>장치 이름</returns>
        public static string GetDeviceUserDefinedName(int index) {
            if (index > DeviceList.Count - 1) return null;
            CCameraInfo device = DeviceList.ElementAt(index).Value;
            if (device.nTLayerType == CSystem.MV_GIGE_DEVICE) {
                CGigECameraInfo gigeInfo = (CGigECameraInfo)device;
                return gigeInfo.UserDefinedName;
            }
            else if (device.nTLayerType == CSystem.MV_USB_DEVICE) {
                CUSBCameraInfo usbInfo = (CUSBCameraInfo)device;
                return usbInfo.UserDefinedName;
            }
            return null;
        }

        /// <summary>
        /// 시스템에 연결된 모든 basler 타입의 장치를 조회
        /// </summary>
        /// <param name="identifiers">찾고자 하는 장치 식별자(ip주소 또는 장치 이름, null이면 모두 찾음)</param>
        /// <returns>찾은 장치 개수</returns>
        public static int EnumerateDevice(params string[] identifiers) {
            string devType = null;
            if ((identifiers != null) && (identifiers.Length > 0)) {
                devType = identifiers[0];
            }

            try {
                DeviceList.Clear();

                uint DEV_QUREY_TYPE = 0;
                if (devType != null) {
                    if (devType.ToUpper().Contains("USB")) DEV_QUREY_TYPE |= CSystem.MV_USB_DEVICE;
                    if (devType.ToUpper().Contains("GIGE")) DEV_QUREY_TYPE |= CSystem.MV_GIGE_DEVICE;
                }
                else {
                    DEV_QUREY_TYPE = CSystem.MV_USB_DEVICE | CSystem.MV_GIGE_DEVICE;
                }

                if (CSystem.EnumDevices(DEV_QUREY_TYPE, ref mInternalDeviceList) != CErrorDefine.MV_OK) {
                    return (int)mInternalDeviceList.Count;
                }

                for (int i = 0; i < mInternalDeviceList.Count; i++) {
                    //0. create dev
                    CCameraInfo device = mInternalDeviceList[i];

                    //1. user define name
                    string camName = null;
                    if (device.nTLayerType == CSystem.MV_GIGE_DEVICE) {
                        CGigECameraInfo gigeInfo = (CGigECameraInfo)device;
                        camName = gigeInfo.UserDefinedName;

                        if (String.IsNullOrEmpty(camName)) {
                            camName = gigeInfo.chModelName + " (" + gigeInfo.chSerialNumber + ")";
                        }

                    }
                    else if (device.nTLayerType == CSystem.MV_USB_DEVICE) {
                        CUSBCameraInfo usbInfo = (CUSBCameraInfo)device;
                        camName = usbInfo.UserDefinedName;

                        if (String.IsNullOrEmpty(camName)) {
                            camName = /*usbInfo.chManufacturerName + " " + */usbInfo.chModelName + " (" + usbInfo.chSerialNumber + ")";
                        }
                    }

                    DeviceList.Add(camName, device);
                }
            }
            catch (Exception e) {
                Logging.PrintLog((int)ELogType.Camera, "[ERROR] Enumerate Device, Ientifier : {0}, ({1})", identifiers.ToString(), e.Message);
            }
            return DeviceList.Count;
        }

        /// <summary>
        /// 주어진 index에 해당하는 장치의 ip 주소를 반환
        /// </summary>
        /// <param name="index">장치 index</param>
        /// <returns>장치 이름</returns>
        public static string GetDeviceIpAddress(int index) {
            if (index > DeviceList.Count - 1) return null;
            if (DeviceList.ElementAt(index).Value.nTLayerType != CSystem.MV_GIGE_DEVICE) return null;

            CCameraInfo device = DeviceList.ElementAt(index).Value;
            CGigECameraInfo gigeInfo = (CGigECameraInfo)device;

            uint nIp1 = ((gigeInfo.nCurrentIp & 0xff000000) >> 24);
            uint nIp2 = ((gigeInfo.nCurrentIp & 0x00ff0000) >> 16);
            uint nIp3 = ((gigeInfo.nCurrentIp & 0x0000ff00) >> 8);
            uint nIp4 = (gigeInfo.nCurrentIp & 0x000000ff);
            return string.Format("{0}.{1}.{2}.{3}", nIp1, nIp2, nIp3, nIp4);
        }

        public static bool ContainsDevice(CCameraInfo info) {
            return DeviceList.ContainsValue(info);
        }

        /// <summary>
        /// 주어진 index에 해당하는 장치 이름을 반환
        /// </summary>
        /// <param name="index">장치 index</param>
        /// <returns>장치 이름</returns>
        public static string GetDeviceName(int index) {
            string name = GetDeviceUserDefinedName(index);
            if (String.IsNullOrEmpty(name)) name = GetDeviceIpAddress(index);
            if (String.IsNullOrEmpty(name)) name = GetDeviceFriendlyName(index);
            return name;
        }

        private static string GetDeviceName(ref CCameraInfo info) {
            string name = null;
            if (info.nTLayerType == CSystem.MV_GIGE_DEVICE) {
                CGigECameraInfo gigeInfo = (CGigECameraInfo)info;
                name = gigeInfo.UserDefinedName;
                if (String.IsNullOrEmpty(name)) {
                    name = gigeInfo.chModelName + " (" + gigeInfo.chSerialNumber + ")";
                }
            }
            else if (info.nTLayerType == CSystem.MV_USB_DEVICE) {
                CUSBCameraInfo usbInfo = (CUSBCameraInfo)info;
                name = usbInfo.UserDefinedName;
                if (String.IsNullOrEmpty(name)) {
                    name = usbInfo.chModelName + " (" + usbInfo.chSerialNumber + ")";
                }
            }
            return name;
        }

        public HikCamera(DisplayConfig config, DeviceInfo info) : base(config, info, ECameraType.HIK) {
            Properties = new HikCameraProperty(this);

            mStopwatch = new Stopwatch();
        }

        ~HikCamera() {
            Dispose();
        }

        public void Dispose() {
            Close();
        }

        public override bool Open(params object[] param) {
            string camIpOrName = null;
            if ((param != null) && (param.Length > 0)) {
                camIpOrName = param[0] as string;
            }
            int index = GetDeviceIndex(camIpOrName);
            if (index < 0) return false;

            CCameraInfo info = GetDeviceInfo(index);
            if (info == null) return false;

            return Open(info, index);
        }

        private bool Open(CCameraInfo info, int id) {
            int nRet = 0;
            try {
                this.CameraHandle = new CCamera();
                this.CameraInfo = info;

                nRet = this.CameraHandle.CreateHandle(ref info);
                if (nRet != CErrorDefine.MV_OK) {
                    throw new Exception(string.Format("{0} Handle Create Fail!", GetDeviceName(ref info)));
                }
                
                nRet = this.CameraHandle.OpenDevice();
                if(nRet != CErrorDefine.MV_OK) {
                    throw new Exception(string.Format("{0} Open Device Fail!", GetDeviceName(ref info)));
                }
                
                //set packet size 
                if (info.nTLayerType == CSystem.MV_GIGE_DEVICE) {
                    int nPacketSize = CameraHandle.GIGE_GetOptimalPacketSize();
                    if(nPacketSize > 0) {
                        if(CameraHandle.SetIntValue("GevSCPSPacketSize", (uint)nPacketSize) != CErrorDefine.MV_OK) {
                            Logging.PrintLog((int)ELogType.Camera, "[ERROR] {0} Set Packet Size failed!", Info.Identifier);
                            return false;
                        }
                    }
                    else {
                        Logging.PrintLog((int)ELogType.Camera, "[ERROR] {0} Get Packet Size failed!", Info.Identifier);
                        return false;
                    }
                }

                switch (Info.ImageType) {
                    case ECaptureImageType.Color24:
                        CameraHandle.SetEnumValue("PixelFormat", (uint)MvGvspPixelType.PixelType_Gvsp_BayerGB8);
                        break;
                    case ECaptureImageType.Gray8:
                        //CameraHandle.MV_CC_SetPixelFormat_NET((int)MyCamera.MvGvspPixelType.PixelType_Gvsp_Mono8);
                        CameraHandle.SetEnumValue("PixelFormat", (uint)MvGvspPixelType.PixelType_Gvsp_Mono8);
                        break;
                }
                CIntValue intVal = new CIntValue();
                CameraHandle.SetIntValue("Width", Info.Width);
                if (CameraHandle.GetIntValue("Width", ref intVal) == CErrorDefine.MV_OK) {
                    Properties.Width = (int)intVal.CurValue;
                }
                else {
                    Properties.Width = Info.Width;
                    Logging.PrintLog((int)ELogType.Camera, "[ERROR] {0} Set Width to {1} failed!", Info.Identifier, Info.Width);
                }

                CameraHandle.SetIntValue("Height", Info.Height);
                if(CameraHandle.GetIntValue("Height", ref intVal) == CErrorDefine.MV_OK) {
                    Properties.Height = (int)intVal.CurValue;
                }
                else {
                    Properties.Height = Info.Height;
                    Logging.PrintLog((int)ELogType.Camera, "[ERROR] {0} Set Height to {1} failed!", Info.Identifier, Info.Height);
                }
                bool boolVal = true;
                CameraHandle.SetBoolValue("ReverseX", Info.ReverseX);
                if(CameraHandle.GetBoolValue("ReverseX", ref boolVal) == CErrorDefine.MV_OK) {
                    if(Info.ReverseX != boolVal) {
                        Logging.PrintLog((int)ELogType.Camera, "[ERROR] {0} Set ReverseX to {1} failed!", Info.Identifier, Info.ReverseX);
                    }
                }
                else {
                    Logging.PrintLog((int)ELogType.Camera, "[ERROR] {0} Set ReverseX to {1} failed!", Info.Identifier, Info.ReverseX);
                }

                CameraHandle.SetBoolValue("ReverseY", Info.ReverseY);
                if(CameraHandle.GetBoolValue("ReverseY", ref boolVal) == CErrorDefine.MV_OK) {
                    if(Info.ReverseY != boolVal) {
                        Logging.PrintLog((int)ELogType.Camera, "[ERROR] {0} Set ReverseY to {1} failed!", Info.Identifier, Info.ReverseY);
                    }
                }
                else {
                    Logging.PrintLog((int)ELogType.Camera, "[ERROR] {0} Set ReverseY to {1} failed!", Info.Identifier, Info.ReverseY);
                }
                
                CameraHandle.SetEnumValue("AcquisitionMode", (uint)MV_CAM_ACQUISITION_MODE.MV_ACQ_MODE_CONTINUOUS);
                CameraHandle.SetEnumValue("ExposureAutoMode", (uint)MV_CAM_EXPOSURE_AUTO_MODE.MV_EXPOSURE_AUTO_MODE_OFF);
                CameraHandle.SetEnumValue("GainMode", (uint)MV_CAM_GAIN_MODE.MV_GAIN_MODE_OFF);

                //Register default Event
                EventCallback = new cbEventdelegateEx(OnEvent);
                ExceptionCallback = new cbExceptiondelegate(OnException);
                GrabCallback = new cbOutputExdelegate(OnGrabResult);
                this.CameraHandle.RegisterExceptionCallBack(ExceptionCallback, (IntPtr)id);
                this.CameraHandle.RegisterImageCallBackEx(GrabCallback, (IntPtr)id);
                this.CameraHandle.RegisterAllEventCallBack(EventCallback, (IntPtr)id);

                Properties.Update();
                //rotate90, 270 경우 width, height가 반대
                if ((Info.RotateAngle == ERotateAngleType._90) || (Info.RotateAngle == ERotateAngleType._270)) {
                    int temp = Properties.Height;
                    Properties.Height = Properties.Width;
                    Properties.Width = temp;
                }

                CaptureMode = ECaptureModeType.Stop;
            }
            catch (Exception e) {
                Logging.PrintLog((int)ELogType.Camera, "[ERROR] {0} Open Fail. ({1})", Info.Identifier, e.Message);
                return false;
            }
            return true;
        }

        public override void Close() {
            if (CameraHandle != null) {
                StopStream();
                if (CameraHandle.IsDeviceConnected()) {
                    CameraHandle.CloseDevice();
                    CameraHandle.DestroyHandle();
                }
            }
        }

        public override bool ExecuteSoftwareTrigger() {
            if(CaptureMode != ECaptureModeType.Trigger) {
                SetSoftwareTriggerMode();
            }

            if(CameraHandle.SetCommandValue("TriggerSoftware") != CErrorDefine.MV_OK) {
                Logging.PrintLog((int)ELogType.Camera, "[ERROR] {0}, Execute Software Trigger failed!", Name);
                return false;
            }
            return true;
        }

        private void OnException(uint nMsgType, IntPtr pUser) {
            Logging.PrintLog((int)ELogType.Camera, "[ERROR] {0}, Occurs Exception {0}", Name, nMsgType);
        }

        private void OnEvent(ref MV_EVENT_OUT_INFO pEventInfo, IntPtr pUser) {
            Logging.PrintLog((int)ELogType.Camera, "[EVENT] {0}, Occurs Event {1}, {2}", Name, pEventInfo.nEventID, pEventInfo.EventName);
        }
        
        private void OnGrabResult(IntPtr pData, ref MV_FRAME_OUT_INFO_EX pFrameInfo, IntPtr pUser) {
            try
            {
                Interlocked.Increment(ref imageCount);

                lock (Interlock) {
                    HImage sourceImage = new HImage();
                    sourceImage.GenImage1("byte", (int)pFrameInfo.nWidth, (int)pFrameInfo.nHeight, pData);

                    HImage rotatedImage = sourceImage;
                    if (Info.RotateAngle == ERotateAngleType._90) {
                        rotatedImage = sourceImage.RotateImage(90.0, "constant");
                        sourceImage.Dispose();
                    }
                    else if (Info.RotateAngle == ERotateAngleType._180) {
                        rotatedImage = sourceImage.RotateImage(180.0, "constant");
                        sourceImage.Dispose();
                    }
                    else if (Info.RotateAngle == ERotateAngleType._270) {
                        rotatedImage = sourceImage.RotateImage(270.0, "constant");
                        sourceImage.Dispose();
                    }

                    LastGrabHalconImage?.Dispose();
                    LastGrabHalconImage = rotatedImage;

                    GrabState = EGrabStateType.Done;
                }
            }
            catch (Exception e) {
                Logging.PrintLog((int)ELogType.Camera, "[ERROR] {0} On Grab Result, ({1})", Name, e.Message);
                GrabState = EGrabStateType.Fail;
            }

            if(IsEnableGrabEvent && GuiReadyForDisplay != null) {
                GuiReadyForDisplay(Name);
            }
        }

        public HImage GrabHalconImage(bool enableGrabEvent = false) {
            IsEnableGrabEvent = enableGrabEvent;
            return GrabHalconImage();
        }

        public override HImage GrabHalconImage() {
            if (CaptureMode == ECaptureModeType.Streaming) return null;

            GrabState = EGrabStateType.Grabbing;
            mStopwatch.Restart();

            if (!SetSoftwareTriggerMode()) return null;
           
            prevImageCount = imageCount;
            ExecuteSoftwareTrigger();

            while (true) {
                if (GrabState == EGrabStateType.Done) {
                    //Logging.PrintLog((int)ELogType.Camera, "[GRAB] {0} GrabImage. ImageCount:{1}", Name, ImageCount);
                    break;
                }
                else if (GrabState == EGrabStateType.Fail) {
                    Logging.PrintLog((int)ELogType.Camera, "[ERROR] {0} Grab Image (software trigger mode), {1}", Name, GrabState.ToString());
                    Interlocked.Increment(ref errorCount);
                    break;
                }
                else if (mStopwatch.ElapsedMilliseconds >= pConfig.GrabTimeOut) return null;
                Thread.Sleep(1);
            }
            
            return LastHalconImage;
        }

        public override void ClearLastFrame() {
            base.ClearLastFrame();
        }

        public override bool IsGrabbed() {
            if (prevImageCount != imageCount) return true;
            return false;
        }

        public override bool SetTriggerMode(ETriggerSource source, bool forcing= false, bool threading = false) {
            if (CameraHandle == null) return false;
            //if (TriggerSource == source && forcing == false) return true; // Source가 같으면 Skip 12.27

            try {
                StopStream();
                ResetGrabCount();
                uint sourceNum = 0;
                switch (source) {
                    case ETriggerSource.Hardware_Line0:
                        sourceNum = (uint)MV_CAM_TRIGGER_SOURCE.MV_TRIGGER_SOURCE_LINE0;
                        break;
                    case ETriggerSource.Hardware_Line1:
                        sourceNum = (uint)MV_CAM_TRIGGER_SOURCE.MV_TRIGGER_SOURCE_LINE1;
                        break;
                    case ETriggerSource.Hardware_Line2:
                        sourceNum = (uint)MV_CAM_TRIGGER_SOURCE.MV_TRIGGER_SOURCE_LINE2;
                        break;
                    case ETriggerSource.Hardware_Line3:
                        sourceNum = (uint)MV_CAM_TRIGGER_SOURCE.MV_TRIGGER_SOURCE_LINE3;
                        break;
                    case ETriggerSource.Software:
                        sourceNum = (uint)MV_CAM_TRIGGER_SOURCE.MV_TRIGGER_SOURCE_SOFTWARE;
                        break;
                }
                if (CameraHandle.SetEnumValue("TriggerSource", sourceNum) != CErrorDefine.MV_OK) {
                    Logging.PrintLog((int)ELogType.Camera, "[ERROR] {0} set Trigger source to {1} failed!", Name, source.ToString());
                    return false;
                }

                if (CameraHandle.SetEnumValue("TriggerMode", (uint)MV_CAM_TRIGGER_MODE.MV_TRIGGER_MODE_ON) != CErrorDefine.MV_OK) {
                    Logging.PrintLog((int)ELogType.Camera, "[ERROR] {0} set Trigger mode to On failed!", Name);
                    return false;
                }

                if (CameraHandle.StartGrabbing() != CErrorDefine.MV_OK) {
                    Logging.PrintLog((int)ELogType.Camera, "[ERROR] {0} Start Grabbing failed!", Name);
                    return false;
                }

                CaptureMode = ECaptureModeType.Trigger;
                TriggerSource = source;
            }
            catch(Exception e) {
                Logging.PrintLog((int)ELogType.Camera, "[ERROR] {0} SetHardwareTriggerMode. ({1})", Name, e.Message);
                return false;
            }
            return true;
        }

        public override bool SetSoftwareTriggerMode(bool threading = false) {
            if (CameraHandle == null) return false;
            //if (TriggerSource == ETriggerSource.Software) return true; //이미 SW trigger 이면 Skip (231127 제거 => 확인 필요)

            try {
                StopStream();
                ResetGrabCount();
                if (CameraHandle.SetEnumValue("TriggerSource", (uint)MV_CAM_TRIGGER_SOURCE.MV_TRIGGER_SOURCE_SOFTWARE) != CErrorDefine.MV_OK) {
                    Logging.PrintLog((int)ELogType.Camera, "[ERROR] {0} set Trigger source to Software failed!", Name);
                    return false;
                }

                if (CameraHandle.SetEnumValue("TriggerMode", (uint)MV_CAM_TRIGGER_MODE.MV_TRIGGER_MODE_ON) != CErrorDefine.MV_OK) {
                    Logging.PrintLog((int)ELogType.Camera, "[ERROR] {0} set Trigger mode to On failed!", Name);
                    return false;
                }

                if (CameraHandle.StartGrabbing() != CErrorDefine.MV_OK) {
                    Logging.PrintLog((int)ELogType.Camera, "[ERROR] {0} Start Grabbing failed!", Name);
                    return false;
                }

                CaptureMode = ECaptureModeType.Trigger;
                TriggerSource = ETriggerSource.Software;
            }
            catch (Exception e) {
                Logging.PrintLog((int)ELogType.Camera, "[ERROR] {0} SetSoftwareTriggerMode. ({1})", Name, e.Message);
                return false;
            }
            return true;
        }

        public override bool StartStream() {
            if (CameraHandle == null) return false;

            if (CaptureMode == ECaptureModeType.Streaming) return true;
            
            try {
                ResetGrabCount();

                if (CameraHandle.SetEnumValue("TriggerMode", (uint)MV_CAM_TRIGGER_MODE.MV_TRIGGER_MODE_OFF) != CErrorDefine.MV_OK) {
                    return false;
                }

                if (CameraHandle.StartGrabbing() != CErrorDefine.MV_OK) {
                    return false;
                }

                prevImageCount = imageCount;
                CaptureMode = ECaptureModeType.Streaming;
                mStopwatch.Restart();
            }
            catch (Exception e) {
                Logging.PrintLog((int)ELogType.Camera, "[ERROR] {0} StartStream. ({1})", Name, e.Message);
            }

            return true;
        }

        public override void StopStream() {
            if(CaptureMode == ECaptureModeType.Stop) return;
            try {
                CameraHandle.StopGrabbing();
                if (mStopwatch.IsRunning) {
                    mStopwatch.Stop();
                }
                IsGrabbing = false;
                CaptureMode = ECaptureModeType.Stop;
            }
            catch (Exception e) {
                Logging.PrintLog((int)ELogType.Camera, "[ERROR] {0} StopStream. ({1})", Name, e.Message);
            }

            base.StopStream(); 
        }

        public override HImage WaitForHalconTrigger(bool clone, int timeOut = 3000) {
            lock (Interlock) {
                prevImageCount = imageCount;
                GrabState = EGrabStateType.Grabbing;
                mStopwatch.Restart();
            }
            while (true) {
                if (mStopwatch.ElapsedMilliseconds > timeOut) return null;
                else if ((GrabState == EGrabStateType.Done)) break;
            }

            if (!clone) {
                return LastGrabHalconImage;
            }

            return LastHalconImage;
        }
    }
}



