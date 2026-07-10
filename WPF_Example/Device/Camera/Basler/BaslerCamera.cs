using System;
using System.Collections.Generic;
using System.Diagnostics;

using System.IO;
using System.Linq;
using System.Net;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Threading;
using System.Runtime.InteropServices;
using Basler.Pylon;
using HalconDotNet;
using OpenCvSharp;
using ReringProject.Halcon;
using ReringProject.Utility;
using ReringProject.Setting;

namespace ReringProject.Device {
    public partial class BaslerCamera : VirtualCamera, IDisposable {
        
        public Basler.Pylon.Camera CameraHandle { get; private set; }
        public PixelDataConverter PixelConverter { get; private set; } = new PixelDataConverter();
        
        private IntPtr ConvertedFrameBuffer = IntPtr.Zero;
        private long ConvertedFrameBufferSize = 0;

        //Event ?앹꽦        
        public override event StateEvent GuiReadyForDisplay = null;

        //Fps 怨꾩궛??
        private Stopwatch mStopWatch = new Stopwatch();

        public override TimeSpan ElapsedTime { get { return mStopWatch.Elapsed; } }

        //static member
        private static Dictionary<string, ICameraInfo> DeviceList = new Dictionary<string, ICameraInfo>();
        public static int GetDeviceCount() {
            return DeviceList.Count;
        }
        /// <summary>
        /// Basler ?μ튂 ?뺣낫瑜?諛섑솚
        /// </summary>
        /// <param name="identifier">?앸퀎??/param>
        /// <returns>?대떦 ?μ튂 ?뺣낫, ?놁쑝硫?null</returns>
        public static ICameraInfo GetDeviceInfo(string identifier) {
            if (DeviceList.ContainsKey(identifier)) return DeviceList[identifier];

            for(int i = 0; i < DeviceList.Count; i++) {
                if (identifier == GetDeviceUserDefinedName(i)) return DeviceList.ElementAt(i).Value;
                if (identifier == GetDeviceIpAddress(i)) return DeviceList.ElementAt(i).Value;
                if (identifier == GetDeviceFriendlyName(i)) return DeviceList.ElementAt(i).Value;
            }
            return null;
        }
        /// <summary>
        /// identifier ?대쫫???μ튂媛 議댁옱?섎뒗吏 ?뺤씤
        /// </summary>
        /// <param name="identifier">?앸퀎??/param>
        /// <returns>議댁옱?섎㈃ true, ?놁쑝硫?false</returns>
        public static bool ContainsDevice(string identifier) {
            if (String.IsNullOrEmpty(identifier)) return false;
            if (GetDeviceInfo(identifier) != null) return true;
            return false;
        }
        /// <summary>
        /// 二쇱뼱吏?index???대떦?섎뒗 ?μ튂 ?뺣낫瑜?諛섑솚
        /// </summary>
        /// <param name="index">?μ튂 index</param>
        /// <returns>?μ튂 ?뺣낫, ?놁쑝硫?null</returns>
        public static ICameraInfo GetDeviceInfo(int index) {
            if (index > DeviceList.Count - 1) return null;
            return DeviceList.ElementAt(index).Value;
        }

        /// <summary>
        /// 二쇱뼱吏?index???대떦?섎뒗 friendly ?μ튂 ?대쫫??諛섑솚
        /// </summary>
        /// <param name="index">?μ튂 index</param>
        /// <returns>?μ튂 ?대쫫</returns>
        public static string GetDeviceFriendlyName(int index) {
            if (index > DeviceList.Count - 1) return null;
            return DeviceList.ElementAt(index).Value[CameraInfoKey.FriendlyName];
        }

        /// <summary>
        /// 二쇱뼱吏?index???대떦?섎뒗 ?ъ슜???뺤쓽???μ튂 ?대쫫??諛섑솚
        /// </summary>
        /// <param name="index">?μ튂 index</param>
        /// <returns>?μ튂 ?대쫫</returns>
        public static string GetDeviceUserDefinedName(int index) {
            if (index > DeviceList.Count - 1) return null;
            return DeviceList.ElementAt(index).Value[CameraInfoKey.UserDefinedName];
        }

        /// <summary>
        /// 二쇱뼱吏?index???대떦?섎뒗 ?μ튂??ip 二쇱냼瑜?諛섑솚
        /// </summary>
        /// <param name="index">?μ튂 index</param>
        /// <returns>?μ튂 ?대쫫</returns>
        public static string GetDeviceIpAddress(int index) {
            if (index > DeviceList.Count - 1) return null;
            if (DeviceList.ElementAt(index).Value[CameraInfoKey.DeviceType] != DeviceType.GigE) return null;
            return DeviceList.ElementAt(index).Value[CameraInfoKey.DeviceIpAddress];
        }

        /// <summary>
        /// 二쇱뼱吏?index???대떦?섎뒗 ?μ튂 ?대쫫??諛섑솚
        /// </summary>
        /// <param name="index">?μ튂 index</param>
        /// <returns>?μ튂 ?대쫫</returns>
        public static string GetDeviceName(int index) {
            string name = GetDeviceUserDefinedName(index);
            if (String.IsNullOrEmpty(name)) name = GetDeviceIpAddress(index);
            if (String.IsNullOrEmpty(name)) name = GetDeviceFriendlyName(index);
            if (String.IsNullOrEmpty(name)) name = GetDeviceUserDefinedName(index);
            return name;
        }

        /// <summary>
        /// ?쒖뒪?쒖뿉 ?곌껐??紐⑤뱺 basler ??낆쓽 ?μ튂瑜?議고쉶
        /// </summary>
        /// <param name="identifiers">李얘퀬???섎뒗 ?μ튂 ?앸퀎??ip二쇱냼 ?먮뒗 ?μ튂 ?대쫫, null?대㈃ 紐⑤몢 李얠쓬)</param>
        /// <returns>李얠? ?μ튂 媛쒖닔</returns>
        public static int EnumerateDevice(params string [] identifiers) {
            string devType = null;
            if ((identifiers != null) && (identifiers.Length > 0)) {
                devType = identifiers[0];
            }

            try {
                DeviceList.Clear();

                List<ICameraInfo> camList = null;
                if (devType != null) camList = CameraFinder.Enumerate(devType);
                else camList = CameraFinder.Enumerate();

                foreach(ICameraInfo camInfo in camList) {
                    //1. user define name
                    string camName = camInfo[CameraInfoKey.UserDefinedName];
                    //2. ip address
                    if (camInfo[CameraInfoKey.DeviceType] == DeviceType.GigE) {
                        if (String.IsNullOrEmpty(camName)) {
                            camName = camInfo[CameraInfoKey.DeviceIpAddress];
                        }
                    }
                    //3. friendly name
                    if (String.IsNullOrEmpty(camName)) {
                        camName = camInfo[CameraInfoKey.FriendlyName];
                    }
                    DeviceList.Add(camName, camInfo);
                }
            }
            catch (Exception e) {
                Logging.PrintLog((int)ELogType.Camera, "[ERROR] Enumerate Device, Ientifier : {0}, ({1})", identifiers.ToString(), e.Message);
            }
            return DeviceList.Count;
        }
        public static bool ContainsDevice(ICameraInfo info) {
            return DeviceList.ContainsValue(info);
        }
        

        /// <summary>
        /// 媛쒕퀎 ?μ튂??????앹꽦??
        /// </summary>
        /// <param name="config">?ㅼ젙 ?뺣낫</param>
        /// <param name="imageType">?대?吏 ???/param>
        /// <param name="devName">?μ튂 ?대쫫</param>
        public BaslerCamera(DisplayConfig config, DeviceInfo info) : base(config, info, ECameraType.Basler) {
            Properties = new BaslerCameraProperty(this);

            mStopWatch = new Stopwatch();
        }
        
        ~BaslerCamera() {
            Dispose();
        }

        public void Dispose() {
            if (CameraHandle != null) {
                CameraHandle.ConnectionLost -= OnConnectionLost;
                CameraHandle.CameraOpened -= OnCameraOpened;
                CameraHandle.CameraClosed -= OnCameraClosed;
                if (CameraHandle.StreamGrabber != null) {
                    CameraHandle.StreamGrabber.GrabStarted -= OnGrabStarted;
                    CameraHandle.StreamGrabber.ImageGrabbed -= OnImageGrabbed;
                    CameraHandle.StreamGrabber.GrabStopped -= OnGrabStopped;
                }
            }
            Close();
            
            PixelConverter.Dispose();

        }
        /// <summary>
        /// ?대떦 ?μ튂瑜??곕떎.
        /// </summary>
        /// <param name="info">?μ튂 ?뺣낫</param>
        /// <returns>?깃났?대㈃ true, ?ㅽ뙣?대㈃ false</returns>
        public bool Open(ICameraInfo info) {
            try {
                this.CameraHandle = new Camera(info);
                CameraHandle.CameraOpened += Configuration.SoftwareTrigger;
                CameraHandle.ConnectionLost += this.OnConnectionLost;
                CameraHandle.CameraOpened += this.OnCameraOpened;
                CameraHandle.CameraClosed += this.OnCameraClosed;
                CameraHandle.StreamGrabber.GrabStarted += this.OnGrabStarted;
                CameraHandle.StreamGrabber.ImageGrabbed += this.OnImageGrabbed;
                CameraHandle.StreamGrabber.GrabStopped += this.OnGrabStopped;

                CameraHandle.Open();

                //媛뺤젣 ?띿꽦 怨좎젙
                //developer custom
                switch (Info.ImageType) {
                    case ECaptureImageType.Color24:
                        CameraHandle.Parameters[PLCamera.PixelFormat].SetValue(PLCamera.PixelFormat.BayerGB8);
                        break;
                    case ECaptureImageType.Gray8:
                        CameraHandle.Parameters[PLCamera.PixelFormat].SetValue(PLCamera.PixelFormat.Mono8);
                        break;
                }
                //set width
                if (CameraHandle.Parameters[PLCamera.Width].IsWritable) {
                    CameraHandle.Parameters[PLCamera.Width].SetValue(Info.Width);
                }
                else {
                    Logging.PrintLog((int)ELogType.Camera, "[ERROR] {0} Set Width to {1} failed!", Info.Identifier, Info.Width);
                }
                int width = (int)CameraHandle.Parameters[PLCamera.Width].GetValue();
                if(width != Info.Width) {
                    Logging.PrintLog((int)ELogType.Camera, "[ERROR] {0} Set Width to {1} failed!", Info.Identifier, Info.Width);
                }
                else {
                    Properties.Width = width;
                }

                //set height
                if (CameraHandle.Parameters[PLCamera.Height].IsWritable) {
                    CameraHandle.Parameters[PLCamera.Height].SetValue(Info.Height);
                }
                else {
                    Logging.PrintLog((int)ELogType.Camera, "[ERROR] {0} Set Height to {1} failed!", Info.Identifier, Info.Height);
                }
                int height = (int)CameraHandle.Parameters[PLCamera.Height].GetValue();
                if(height != Info.Height) {
                    Logging.PrintLog((int)ELogType.Camera, "[ERROR] {0} Set Height to {1} failed!", Info.Identifier, Info.Height);
                }
                else {
                    Properties.Height = height;
                }
                

                if (CameraHandle.Parameters[PLCamera.ReverseX].IsWritable) {
                    CameraHandle.Parameters[PLCamera.ReverseX].SetValue(Info.ReverseX);
                }
                else {
                    Logging.PrintLog((int)ELogType.Camera, "[ERROR] {0} Set ReverseX to {1} failed!", Info.Identifier, Info.ReverseX);
                }

                if (CameraHandle.Parameters[PLCamera.ReverseY].IsWritable) {
                    CameraHandle.Parameters[PLCamera.ReverseY].SetValue(Info.ReverseY);
                }
                else {
                    Logging.PrintLog((int)ELogType.Camera, "[ERROR] {0} Set ReverseY to {1} failed!", Info.Identifier, Info.ReverseY);
                }

                //default
                if (CameraHandle.Parameters[PLCamera.ExposureAuto].IsWritable) {
                    CameraHandle.Parameters[PLCamera.ExposureAuto].SetValue(PLCamera.ExposureAuto.Off);
                }
                if (CameraHandle.Parameters[PLCamera.GainAuto].IsWritable) {
                    CameraHandle.Parameters[PLCamera.GainAuto].SetValue(PLCamera.GainAuto.Off);
                }
                if (CameraHandle.Parameters[PLCamera.GainSelector].IsWritable) {
                    CameraHandle.Parameters[PLCamera.GainSelector].SetValue(PLCamera.GainSelector.All);
                }
                
                //Gamma Enable
                if (CameraHandle.Parameters[PLCamera.GammaEnable].IsWritable) {
                    CameraHandle.Parameters[PLCamera.GammaEnable].SetValue(true);
                    //CameraHandle.Parameters[PLCamera.GammaSelector].SetValue(PLCamera.GammaSelector.sRGB);
                }
                /*
                //Balance White AUto
                if (CameraHandle.Parameters[PLCamera.BalanceWhiteAuto].IsWritable) {
                    CameraHandle.Parameters[PLCamera.BalanceWhiteAuto].SetValue(PLCamera.BalanceWhiteAuto.Once);
                }
               */
                
                //parameter
                Properties.Update();

                //rotate90, 270 寃쎌슦 width, height媛 諛섎?
                if ((Info.RotateAngle == ERotateAngleType._90) || (Info.RotateAngle == ERotateAngleType._270)) {
                    int temp = Properties.Height;
                    Properties.Height = Properties.Width;
                    Properties.Width = temp;
                }

                CaptureMode = ECaptureModeType.Stop;
                //CameraHandle.StreamGrabber.Start(GrabStrategy.OneByOne, GrabLoop.ProvidedByStreamGrabber);
            }
            catch (Exception e) {
                Logging.PrintLog((int)ELogType.Camera, "[ERROR] Camera {0} Open Fail, ({1})", Info.Identifier, e.Message);
                return false;
            }
            return true;
        }

        /// <summary>
        /// index???대떦?섎뒗 ?μ튂瑜??곕떎.
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        public bool Open(int index) {
            if (index > DeviceList.Count - 1) return false;
            return Open(GetDeviceInfo(index));
        }

        /// <summary>
        /// 二쇱뼱吏?ip ?먮뒗 ?대쫫???대떦?섎㈃ open?쒕떎.
        /// </summary>
        /// <param name="param">ip二쇱냼 ?먮뒗 ?μ튂 ?대쫫</param>
        /// <returns></returns>
        public override bool Open(params object[] param) {
            string camIpOrName = null;
            if ((param != null) && (param.Length > 0)) {
                camIpOrName = param[0] as string;
            }
            ICameraInfo camInfo = null;
            if (IPAddress.TryParse(camIpOrName, out _)) {
                camInfo = GetDeviceInfo(camIpOrName);
                if(camInfo == null) {
                    return false;
                }
                
                //camInfo = IpConfigurator.AnnounceRemoteDevice(camIpOrName);
                //IP 二쇱냼濡?李얠븘??移대찓???뺣낫媛 ?μ튂 紐⑸줉???녿뒗 寃쎌슦, 異붽??쒕떎.
                if (!ContainsDevice(camInfo)) {
                    string camName = camInfo[CameraInfoKey.UserDefinedName];
                    if (String.IsNullOrEmpty(camName)) camName = camInfo[CameraInfoKey.FriendlyName];
                    DeviceList.Add(camName, camInfo);
                    Name = camName;
                }
                return this.Open(camInfo);
            }

            camInfo = GetDeviceInfo(camIpOrName);
            return this.Open(camInfo);
        }

        //?μ튂媛 open??寃쎌슦 ?몄텧
        private void OnCameraOpened(object sender, EventArgs args) {
            IsOpen = true;
        }

        //?μ튂媛 close ??寃쎌슦 ?몄텧
        private void OnCameraClosed(object sender, EventArgs args) {
            IsOpen = false;
        }

        // grab???쒖옉??寃쎌슦 ?몄텧
        private void OnGrabStarted(object sender, EventArgs args) {
            IsGrabbing = true;
        }

        // grab???꾨즺??寃쎌슦 ?몄텧
        private void OnGrabStopped(object sender, EventArgs args) {
            IsGrabbing = false;
        }

        //?대?吏 洹몃옪 ?대깽??
        private void OnImageGrabbed(object sender, ImageGrabbedEventArgs args) {
            if ((CaptureMode != ECaptureModeType.Streaming) && (CaptureMode != ECaptureModeType.Trigger)) return;
            try {
                IGrabResult grabResult = args.GrabResult;
                if (grabResult.GrabSucceeded) {
                    UpdateFrameFromGrabResult(grabResult);
                }
                else {
                    Logging.PrintLog((int)ELogType.Camera, "[ERROR] {0} Grab Fail, ErrorCode:{1}, Description:{2}", Name, grabResult.ErrorCode, grabResult.ErrorDescription);
                    Interlocked.Increment(ref errorCount);
                }
            }
            catch(Exception e) {
                Logging.PrintLog((int)ELogType.Camera, "[ERROR] {0} Grab Error, Description:{1}", Name, e.Message);
            }
        }

        // ?곌껐???딆뼱吏?寃쎌슦 ?몄텧
        private void OnConnectionLost(object sender, EventArgs args) {
        }

        public override bool SetTriggerMode(ETriggerSource source, bool forcing=false, bool threading = false) {
            if (CameraHandle == null) return false;
            if (TriggerSource == source && forcing == false) return true; // Source媛 媛숈쑝硫?Skip

            try {
                ResetGrabCount();
                IEnumParameter triggerMode = CameraHandle.Parameters[PLCamera.TriggerMode];
                IEnumParameter triggerSource = CameraHandle.Parameters[PLCamera.TriggerSource];

                string triggerModeState = triggerMode.GetValue();
                if(triggerModeState != PLCamera.TriggerMode.On) {
                    if(triggerMode.TrySetValue(PLCamera.TriggerMode.On) == false) {
                        throw new Exception("Fail to Trigger Mode Set.");
                    }
                }
                string triggerSourceStr = "";
                switch (source) {
                    case ETriggerSource.Hardware_Line0:
                        triggerSourceStr = PLCamera.TriggerSource.Line1;
                        break;
                    case ETriggerSource.Hardware_Line1:
                        triggerSourceStr = PLCamera.TriggerSource.Line2;
                        break;
                    case ETriggerSource.Hardware_Line2:
                        triggerSourceStr = PLCamera.TriggerSource.Line3;
                        break;
                    case ETriggerSource.Hardware_Line3:
                        triggerSourceStr = PLCamera.TriggerSource.Line4;
                        break;
                    case ETriggerSource.Software:
                        triggerSourceStr = PLCamera.TriggerSource.Software;
                        break;
                }
                if(triggerSource.TrySetValue(triggerSourceStr) == false) {
                    throw new Exception("Fail to Trigger Source set " + source.ToString());
                }
                
                if (threading) CameraHandle.StreamGrabber.Start(GrabStrategy.OneByOne, GrabLoop.ProvidedByStreamGrabber);
                else CameraHandle.StreamGrabber.Start(GrabStrategy.OneByOne, GrabLoop.ProvidedByUser);

                if (CameraHandle.CanWaitForFrameTriggerReady) {
                    if (!CameraHandle.WaitForFrameTriggerReady(pConfig.TriggerModeTimeOut, TimeoutHandling.Return)) return false;
                }
                CaptureMode = ECaptureModeType.Trigger;
                TriggerSource = source;
            }
            catch (Exception e) {
                Logging.PrintLog((int)ELogType.Camera, "[ERROR] {0} SetSoftwareTrigger. ({1})", Name, e.Message);
            }
            return true;
        }

        /// <summary>
        /// ?뚰봽?몄썾???몃━嫄?紐⑤뱶濡?蹂寃쏀븳??
        /// </summary>
        /// <param name="threading">?곕젅?쒕줈 泥섎━?섎뒗 寃쎌슦 true</param>
        /// <returns>?깃났?대㈃ true, ?ㅽ뙣硫?false</returns>
        public override bool SetSoftwareTriggerMode(bool threading = false) {
            if (CameraHandle == null) return false;
            if (CaptureMode == ECaptureModeType.Trigger) return true;

            try {
                ResetGrabCount();
                Configuration.SoftwareTrigger(CameraHandle, null);
                if (threading) CameraHandle.StreamGrabber.Start(GrabStrategy.OneByOne, GrabLoop.ProvidedByStreamGrabber);
                else CameraHandle.StreamGrabber.Start(GrabStrategy.OneByOne, GrabLoop.ProvidedByUser);

                if (CameraHandle.CanWaitForFrameTriggerReady) {
                    if (!CameraHandle.WaitForFrameTriggerReady(pConfig.TriggerModeTimeOut, TimeoutHandling.Return)) return false;
                }
                CaptureMode = ECaptureModeType.Trigger;
                TriggerSource = ETriggerSource.Software;
            }
            catch(Exception e) {
                Logging.PrintLog((int)ELogType.Camera, "[ERROR] {0} SetSoftwareTrigger. ({1})", Name, e.Message);
            }
            return true;
        }

        /// <summary>
        /// ?뚰봽?몄썾???몃━嫄곕? ?섑뻾?쒕떎.
        /// </summary>
        /// <returns>?깃났?대㈃ true, ?ㅽ뙣硫?false</returns>
        public override bool ExecuteSoftwareTrigger() {
            if (CameraHandle == null) return false;
            try {
                prevImageCount = imageCount;
                CameraHandle.ExecuteSoftwareTrigger();
            }
            catch(Exception e) {
                Logging.PrintLog((int)ELogType.Camera, "[ERROR] {0} ExecSoftwareTrigger. ({1})", Name, e.Message);
                return false;
            }
            return true;
        }

        /// <summary>
        /// 洹몃옪???깃났?곸쑝濡??섏뿀?붿? ?뺤씤?쒕떎.
        /// 留덉?留?寃???댄썑濡? 罹≪퀜媛 ?깃났?곸쑝濡??섑뻾?섏뿀?붿? ?뺤씤?섎뒗 ?⑸룄濡??ъ슜?????덈떎.
        /// </summary>
        /// <returns>?깃났?대㈃ true, ?ㅽ뙣硫?false</returns>
        public override bool IsGrabbed() {
            if (prevImageCount != imageCount) return true;
            return false;
        }

        /// <summary>
        /// ?μ튂瑜?close ?쒕떎.
        /// </summary>
        public override void Close() {
            try {
                StopStream();
                if (ConvertedFrameBuffer != IntPtr.Zero) {
                    Marshal.FreeHGlobal(ConvertedFrameBuffer);
                    ConvertedFrameBuffer = IntPtr.Zero;
                    ConvertedFrameBufferSize = 0;
                }
                if (CameraHandle != null) {
                    CameraHandle.Close();
                    CameraHandle.Dispose();
                    CameraHandle = null;
                }
            }catch(Exception e) {
                Logging.PrintLog((int)ELogType.Camera, "[ERROR] {0} Close. ({1})", Name, e.Message);
            }
        }
       
        // Update the current frame caches.
        private void UpdateFrameFromGrabResult(IGrabResult grabResult, bool callEvent = true) {
            Interlocked.Increment(ref imageCount);

            lock (Interlock) {
                string interleavedFormat;
                if (Info.ImageType == ECaptureImageType.Color24) {
                    PixelConverter.OutputPixelFormat = PixelType.BGR8packed;
                    interleavedFormat = "bgr";
                }
                else {
                    PixelConverter.OutputPixelFormat = PixelType.Mono8;
                    interleavedFormat = null;
                }

                long destSize = PixelConverter.GetBufferSizeForConversion(grabResult);
                if (ConvertedFrameBufferSize < destSize) {
                    if (ConvertedFrameBuffer != IntPtr.Zero) {
                        Marshal.FreeHGlobal(ConvertedFrameBuffer);
                    }
                    ConvertedFrameBuffer = Marshal.AllocHGlobal((IntPtr)destSize);
                    ConvertedFrameBufferSize = destSize;
                }

                PixelConverter.Convert(ConvertedFrameBuffer, destSize, grabResult);

                HImage sourceImage = new HImage();
                if (Info.ImageType == ECaptureImageType.Color24) {
                    sourceImage.GenImageInterleaved(ConvertedFrameBuffer, interleavedFormat, grabResult.Width, grabResult.Height, -1, "byte", grabResult.Width, grabResult.Height, 0, 0, -1, 0);
                }
                else {
                    sourceImage.GenImage1("byte", grabResult.Width, grabResult.Height, ConvertedFrameBuffer);
                }

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

            }

            if (callEvent && (GuiReadyForDisplay != null)) {
                GuiReadyForDisplay(Name);
            }
        }

        /// <summary>
        /// TriggerMode濡??대?吏 洹몃옪???섑뻾?쒕떎.
        /// </summary>
        /// <returns>?깃났?대㈃ true, ?ㅽ뙣硫?false</returns>
        public override HImage GrabHalconImage() {
            if ((CaptureMode == ECaptureModeType.Streaming)) return null;

            mStopWatch.Restart();

            if (!SetSoftwareTriggerMode()) return null;

            prevImageCount = imageCount;
            ExecuteSoftwareTrigger();
            
            try {
                IGrabResult grabResult = CameraHandle.StreamGrabber.RetrieveResult(pConfig.GrabTimeOut, TimeoutHandling.Return);
                if (grabResult == null) return null;
                using (grabResult) {
                    if (grabResult.GrabSucceeded) {
                        UpdateFrameFromGrabResult(grabResult, false);
                        return LastHalconImage;
                    }

                    Logging.PrintLog((int)ELogType.Camera, "[ERROR] {0} GrabHalconImage. ErrorCode:{1} ({2})", Name, grabResult.ErrorCode, grabResult.ErrorDescription);
                    Interlocked.Increment(ref errorCount);
                }
            }
            catch (Exception e) {
                Trace.Write(e.Message);
                Interlocked.Increment(ref errorCount);
            }
            return null;
        }

        /// <summary>
        /// trigger mode瑜??댁젣?섍퀬 stream???섑뻾?쒕떎.
        /// </summary>
        /// <returns>?깃났?대㈃ true, ?ㅽ뙣硫?false</returns>
        public override bool StartStream() {
            if (CameraHandle == null) return false;

            if (CaptureMode == ECaptureModeType.Streaming) return true;
            if (CaptureMode == ECaptureModeType.Trigger) StopStream();

            try {
                ResetGrabCount();
                Configuration.AcquireContinuous(CameraHandle, null);

                prevImageCount = imageCount;
                CameraHandle.StreamGrabber.Start(GrabStrategy.OneByOne, GrabLoop.ProvidedByStreamGrabber);

                CaptureMode = ECaptureModeType.Streaming;
                mStopWatch.Restart();
            }
            catch (Exception e) {
                Logging.PrintLog((int)ELogType.Camera, "[ERROR] {0} StartStream. ({1})", Name, e.Message);
            }
            return true;
        }

        /// <summary>
        /// stream???뺤??쒕떎.
        /// </summary>
        public override void StopStream() {
            if (CaptureMode == ECaptureModeType.Stop) return;
            try {
                CameraHandle.StreamGrabber.Stop();
                if (mStopWatch.IsRunning) {
                    mStopWatch.Stop();
                }
                IsGrabbing = false;
                CaptureMode = ECaptureModeType.Stop;
            }
            catch(Exception e) {
                Logging.PrintLog((int)ELogType.Camera, "[ERROR] {0} StopStream. ({2})", Name, e.Message);
            }

            base.StopStream();
        }

        public override void ClearLastFrame() {
            base.ClearLastFrame();
            if (ConvertedFrameBuffer != IntPtr.Zero) {
                Marshal.FreeHGlobal(ConvertedFrameBuffer);
                ConvertedFrameBuffer = IntPtr.Zero;
                ConvertedFrameBufferSize = 0;
            }
        }

        public override HImage WaitForHalconTrigger(bool clone = true, int timeOut = 3000) {
            lock (Interlock) {
                prevImageCount = imageCount;
                mStopWatch.Restart();
            }

            while (true) {
                if (mStopWatch.ElapsedMilliseconds > timeOut) return null;
                else if (IsGrabbed()) break;
            }

            if (!clone) {
                return LastGrabHalconImage;
            }

            return LastHalconImage;
        }

    }
}










