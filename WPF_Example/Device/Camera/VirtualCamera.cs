
using HalconDotNet;
using ReringProject.Setting;
using ReringProject.Utility;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ReringProject.Device {
    public enum ECameraType {
        Virtual,
        Basler,
        HIK,
        MIL,   //260602 hbk Phase 41 — CXP 카메라 MIL Lite 10.0
    }

    /// <summary>
    /// 筌╈돦??筌뤴뫀諭?
    /// </summary>
    public enum ECaptureModeType {
        Stop,
        Streaming,
        Trigger,
    }

    public enum ETriggerSource {
        Software,
        Hardware_Line0,
        Hardware_Line1,
        Hardware_Line2,
        Hardware_Line3,
    }

    /// <summary>
    /// 筌╈돦?????筌왖 ????
    /// </summary>
    public enum ECaptureImageType {
        Color24,
        Gray8,
    }

    /// <summary>
    /// ???筌왖 ???읈 ?醫륁굨
    /// </summary>
    public enum ERotateAngleType {
        _0,
        _90,
        _180,
        _270,
    }

    //Event ?醫륁굨
    public delegate void StateEvent(string name);


    /// <summary>
    /// 揶쎛??燁삳?李?? 筌뤴뫀諭?燁삳?李??곕뮉 ??Class???怨몃꺗獄쏆룇釉???닌뗭겱??롫뮉 野껉퍔???癒?뒅????
    /// 域밸챶???곗춸 FormDeviceSelector???紐꾪뀱??랁? 燁삳?李??筌뤴뫖以??곗쨮???????????덈뼄.
    /// </summary>
    public class VirtualCamera {
        protected DeviceInfo Info;
        public DisplayConfig pConfig { get; private set; }

        public ECameraType CamType { get; private set; }

        public ECaptureModeType CaptureMode { get; protected set; } = ECaptureModeType.Stop;

        public ETriggerSource TriggerSource { get; protected set; } = ETriggerSource.Software;
        
        public string Name { get; protected set; }

        public bool IsOpen { get; protected set; }

        public bool IsGrabbing { get; protected set; }

        public ERotateAngleType RotateAngle { get; set; } = ERotateAngleType._0;

        //lock object 
        protected object Interlock = new object();

        //燁삳?李??곕뮉 UI??μ몵嚥??紐꾪뀱??????덈뮉 ?꾩뮆媛???源?紐? 揶쎛筌욊쑬??
        public virtual event StateEvent GuiReadyForDisplay = null;

        //ErrorCount 
        protected long prevImageCount = 0;
        protected long imageCount = 0;
        public long ImageCount { get { return imageCount; } }
        protected long errorCount = 0;
        public long ErrorCount { get { return errorCount; } }
        public virtual TimeSpan ElapsedTime { get { return new TimeSpan(); } }


        private System.Windows.Media.Pen DrawPen = null;
        /// <summary>
        /// 域밸챶??燁삳똻??紐? ?λ뜃由?酉釉??
        /// </summary>
        public void ResetGrabCount() {
            Interlocked.Exchange(ref imageCount, 0);
            Interlocked.Exchange(ref errorCount, 0);
        }
        /// <summary>
        /// ?Ρ딆겫??筌띾뜆?筌????筌왖??????獄?域밸챶??燁삳똻??紐? ?λ뜃由?酉釉??
        /// </summary>
        public virtual void ClearLastFrame() {
            ResetGrabCount();
            lock (Interlock) {
                if (LastGrabHalconImage != null) {
                    LastGrabHalconImage.Dispose();
                    LastGrabHalconImage = null;
                }
                if (BackgroundImage != null) {
                    BackgroundImage.Dispose();
                    BackgroundImage = null;
                }
            }
        }
        
        protected HImage LastGrabHalconImage = null;
        //background image
        protected HImage BackgroundImage = null;
        public int BackgroundImageIndex { get; private set; } = 0;

        public bool IsGrabFromFile { get; private set; }
        public string SelectedImageFile { get; private set; }

        //background image

        private string _BackgroundImagePath;
        public string BackgroundImagePath {
            get {
                return _BackgroundImagePath;
            }
            set {
                if (value != _BackgroundImagePath) {
                    _BackgroundImagePath = value;

                    BackgroundImageFileList.Clear();
                    if (_BackgroundImagePath == null) return;

                    //path ??곷퓠 鈺곕똻???롫뮉 image ???뵬??嚥≪뮆諭?
                    string[] extensions = { ".bmp", ".jpg", ".jpeg", ".png", ".tiff" };
                    if (File.Exists(_BackgroundImagePath) && extensions.Any(ext => ext.Equals(Path.GetExtension(_BackgroundImagePath), StringComparison.OrdinalIgnoreCase))) {
                        BackgroundImageFileList.Add(_BackgroundImagePath);
                    }
                    else if (Directory.Exists(_BackgroundImagePath)) {
                        IEnumerable<string> fileList = Directory.EnumerateFiles(_BackgroundImagePath, "*.*", SearchOption.TopDirectoryOnly)
                            .Where(s => extensions.Any(ext => ext == Path.GetExtension(s)));

                        BackgroundImageFileList.AddRange(fileList);
                    }
                }
            }
        }
        public List<string> BackgroundImageFileList { get; } = new List<string>();

        //property
        public VirtualCameraProperty Properties { get; set; }

        public VirtualCamera(DisplayConfig config, DeviceInfo info, ECameraType camType = ECameraType.Virtual) {
            Info = info;
            pConfig = config;

            this.Name = Info.Identifier;
            this.CamType = camType;

            DrawPen = new System.Windows.Media.Pen(System.Windows.Media.Brushes.Fuchsia, 4);
            DrawPen.DashStyle = DashStyles.Dash;
        }

        public double CenterX {
            get {
                return this.Properties.Width / 2;
            }
        }

        public double CenterY {
            get {
                return this.Properties.Height / 2;
            }
        }

        public int BackgroundImageCount {
            get {
                return BackgroundImageFileList.Count;
            }
        }

        public string CurrentBackgroundImageFile {
            get {
                if (BackgroundImageIndex >= BackgroundImageFileList.Count) return null;
                return BackgroundImageFileList[BackgroundImageIndex];
            }
        }

        public void IncreaseBackgroundImageIndex() {
            BackgroundImageIndex++;
        }

        public void DecreaseBackgroundImageIndex() {
            BackgroundImageIndex--;
            if (BackgroundImageIndex < 0) BackgroundImageIndex = 0;
        }

        /// <summary>
        /// 筌띾뜆?筌??Ρ딆겫?????筌왖??獄쏆꼹???뺣뼄.
        /// </summary>
        /// <returns></returns>
        protected virtual HImage GetCurrentImageNoLock() {
            if (BackgroundImagePath == null) {
                IsGrabFromFile = false;
                return LastGrabHalconImage;
            }

            string selectedImageFile = null;
            while (selectedImageFile == null) {
                if (BackgroundImageIndex >= BackgroundImageFileList.Count) {
                    BackgroundImageIndex = 0;
                    break;
                }
                selectedImageFile = BackgroundImageFileList[BackgroundImageIndex];
                if (File.Exists(selectedImageFile) == false) selectedImageFile = null;
            }

            if ((SelectedImageFile == selectedImageFile) && (BackgroundImage != null)) {
                IsGrabFromFile = true;
                return BackgroundImage;
            }

            SelectedImageFile = selectedImageFile;
            if (!String.IsNullOrEmpty(SelectedImageFile)) {
                BackgroundImage?.Dispose();
                BackgroundImage = LoadBackgroundImage(SelectedImageFile);
                IsGrabFromFile = BackgroundImage != null;
                return BackgroundImage;
            }

            IsGrabFromFile = false;
            return LastGrabHalconImage;
        }

        protected virtual HImage LoadBackgroundImage(string imageFilePath) {
            HImage loadedImage = new HImage();
            loadedImage.ReadImage(imageFilePath);

            HImage normalizedImage = loadedImage;
            if ((Info.ImageType == ECaptureImageType.Gray8) && (normalizedImage.CountChannels().I > 1)) {
                HImage grayImage = normalizedImage.Rgb1ToGray();
                normalizedImage.Dispose();
                normalizedImage = grayImage;
            }

            normalizedImage.GetImageSize(out HTuple width, out HTuple height);
#if SIMUL_MODE
            //260317 keep offline source resolution in simulation mode
            Properties.Width = width.I;
            Properties.Height = height.I;
            return normalizedImage;
#endif
            if ((width.I == Properties.Width) && (height.I == Properties.Height)) {
                return normalizedImage;
            }

            HImage resizedImage = normalizedImage.ZoomImageSize(Properties.Width, Properties.Height, "constant");
            normalizedImage.Dispose();
            return resizedImage;
        }

        public virtual HImage LastHalconImage {
            get {
                lock (Interlock) {
                    HImage image = GetCurrentImageNoLock();
                    return image?.CopyImage();
                }
            }
        }

        public virtual void RenderCenterLine(DrawingContext dc) {
            double ScaledCenterX = CenterX; // * pConfig.DrawScale;
            double ScaledCenterY = CenterY; // * pConfig.DrawScale;
            double ScaledWidth = Properties.Width; // * pConfig.DrawScale;
            double ScaledHeight = Properties.Height; // * pConfig.DrawScale;
            
            if (pConfig.DrawCenterLine) {
                dc.DrawLine(DrawPen, new System.Windows.Point(0, ScaledCenterY), new System.Windows.Point(ScaledWidth, ScaledCenterY));
                dc.DrawLine(DrawPen, new System.Windows.Point(ScaledCenterX, 0), new System.Windows.Point(ScaledCenterX, ScaledHeight));
            }
            if (pConfig.DrawCenterRect) {
                double ScaledRectWidth = pConfig.CenterRectWidth; // * pConfig.DrawScale;
                double ScaledRectHeight = pConfig.CenterRectHeight; // * pConfig.DrawScale;

                double left = ScaledCenterX - (ScaledRectWidth / 2);
                double top = ScaledCenterY - (ScaledRectHeight / 2);
                double width = ScaledRectWidth;
                double height = ScaledRectHeight;
                System.Windows.Rect rect = new System.Windows.Rect(left, top, width, height);
                dc.DrawRectangle(System.Windows.Media.Brushes.Transparent, DrawPen, rect);
            }
            if (pConfig.DrawCenterCircle) {
                double ScaledCircleRadius = pConfig.CenterCircleRadius; // * pConfig.DrawScale;
                dc.DrawEllipse(System.Windows.Media.Brushes.Transparent, DrawPen, new System.Windows.Point(ScaledCenterX, ScaledCenterY), ScaledCircleRadius, ScaledCircleRadius);
            }
        }
        
        public virtual bool Display(Image control) {
            try {
                BitmapSource frame = GetPreviewBitmapSource();
                if (frame == null) return false;

                control.Source = frame;
                control.Width = frame.PixelWidth * pConfig.DrawScale;
                control.Height = frame.PixelHeight * pConfig.DrawScale;
                return true;
            }
            catch (Exception e) {
                Logging.PrintLog((int)ELogType.Error, "VirtualCamera.Display() Fail :" + e.Message);
                return false;
            }
        }

        public virtual bool SaveImage(string fileName) {
            try {
                using (HImage grabbedImage = LastHalconImage) {
                    if (grabbedImage == null) return false;
                    string extension = Path.GetExtension(fileName)?.TrimStart('.').ToLowerInvariant();
                    string format = extension == "jpg" ? "jpeg" : extension == "jpeg" ? "jpeg" : extension == "tif" ? "tiff" : extension == "tiff" ? "tiff" : extension == "bmp" ? "bmp" : "png";
                    grabbedImage.WriteImage(format, 0, fileName);
                    return true;
                }
            }
            catch (Exception e) {
                Debug.WriteLine($"[VirtualCamera.SaveImage] Exception:{e}");
                return false;
            }
        }
        

        /// <summary>
        /// 雅뚯눘堉깍쭪?parameter ???關?귞몴??怨뺣뼄.
        /// </summary>
        /// <param name="param">雅뚯눘堉깍쭪?parameter??/param>
        /// <returns></returns>
        public virtual bool Open(params object[] param) {
            if(Properties == null) {
                Properties = new VirtualCameraProperty();
                if (param != null && param.Length >= 2) {
                    if (param[0] is int) {
                        int width = (int)param[0];
                        Properties.Width = width;
                    }
                    if (param[1] is int) {
                        int height = (int)param[1];
                        Properties.Height = height;
                    }
                }
            }

            if((Info.RotateAngle == ERotateAngleType._90) || (Info.RotateAngle == ERotateAngleType._270)) {
                int temp = Properties.Height;
                Properties.Height = Properties.Width;
                Properties.Width = temp;
            }
            return true;
        }

        /// <summary>
        /// ?????關?귞몴???ル뮉??
        /// </summary>
        public virtual void Close() {
            StopStream();

            if(BackgroundImage != null) {
                BackgroundImage.Dispose();
                BackgroundImage = null;
            }
            
        }

        /// <summary>
        /// ???뵬嚥≪뮆????袁⑥쨮??노뼒 ?類ｋ궖??嚥≪뮆諭??뺣뼄.
        /// </summary>
        /// <param name="loadFile">???뵬 野껋럥以?/param>
        /// <param name="group">域밸챶竊숋쭗?/param>
        /// <returns>嚥≪뮆諭??源껊궗??롢늺 true, ??쎈솭??롢늺 false.</returns>
        public virtual bool LoadProperties(IniFile loadFile, string group) {
            bool result = true;
            Name = loadFile[group]["Name"].ToString();
            RotateAngle = (ERotateAngleType)loadFile[group]["RotateAngle"].ToInt();
            int propCount = loadFile[group]["PropertyCount"].ToInt(0);
            for (int i = 0; i < propCount; i++) {
                string subGroupName = "Property_" + i.ToString();
                ECameraPropertyType propType = (ECameraPropertyType)loadFile[group][subGroupName + "_Type"].ToInt();

                Properties[propType] = (decimal)loadFile[group][subGroupName + "_Value"].ToDouble();
                
                //if (!WriteProperty(propType, Properties[propType])) result = false;
            }
            return result;
        }

        /// <summary>
        /// ???뵬嚥≪뮆????袁⑥쨮??노뼒 ?類ｋ궖??嚥≪뮆諭??뺣뼄.
        /// </summary>
        /// <param name="filePath">嚥≪뮆諭?????뵬 野껋럥以?/param>
        /// <param name="group">??욧쉐??域밸챶竊숋쭗??關????已???</param>
        /// <returns>?源껊궗????true, ??쎈솭筌?false</returns>
        public virtual bool LoadProperties(string filePath, string group) {
            if (!File.Exists(filePath)) return false;

            IniFile loadFile = new IniFile();
            loadFile.Load(filePath);

            return LoadProperties(loadFile, group);
        }

        /// <summary>
        /// ?袁⑥쨮??노뼒 ?類ｋ궖?????館釉??
        /// </summary>
        /// <param name="saveFile">???館釉????뵬野껋럥以?/param>
        /// <param name="group">??욧쉐??域밸챶竊숋쭗??關????已???</param>
        /// <returns>?源껊궗????true, ??쎈솭筌?false</returns>
        public virtual bool SaveProperties(IniFile saveFile, string group) {
            saveFile[group]["Name"] = Name;
            saveFile[group]["RotateAngle"] = (int)RotateAngle;
            saveFile[group]["PropertyCount"] = Properties.Count;
            for (int i = 0; i < Properties.Count; i++) {
                string subGroupName = "Property_" + i.ToString();
                ECameraPropertyType propType = Properties.GetPropType(i);
                saveFile[group][subGroupName + "_Type"] = (int)propType;
                saveFile[group][subGroupName + "_Value"] = (double)Properties[i];
            }
            return true;
        }
        /// <summary>
        /// ?袁⑥쨮??노뼒 ?類ｋ궖?????館釉??
        /// </summary>
        /// <param name="filePath">???館釉????뵬 野껋럥以?/param>
        /// <param name="group">??욧쉐??域밸챶竊숋쭗??關????已???</param>
        /// <returns>?源껊궗????true, ??쎈솭筌?false</returns>
        public virtual bool SaveProperties(string filePath, string group) {
            IniFile saveFile = new IniFile();

            SaveProperties(saveFile, group);
            
            saveFile.Save(filePath);
            return true;
        }

        public virtual bool SetTriggerMode(ETriggerSource source, bool forcing=false, bool threading = false) {
            CaptureMode = ECaptureModeType.Trigger;
            TriggerSource = source;
            return true;
        }

        public virtual bool SetSoftwareTriggerMode(bool threading = false) {
            CaptureMode = ECaptureModeType.Trigger;
            TriggerSource = ETriggerSource.Software;
            return true;
        }

        public virtual bool ExecuteSoftwareTrigger() {
            return true;
        }

        public virtual bool IsGrabbed() {
            return true;
        }

        public string ModeString {
            get {
                if (BackgroundImage != null) return "Image Loaded";
                else if (CaptureMode == ECaptureModeType.Stop) return "Stop";
                else if (CaptureMode == ECaptureModeType.Streaming) return "Streaming";
                else if (CaptureMode == ECaptureModeType.Trigger) return "Trigger";
                return "Stop";
            }
        }

        public string StateString {
            get {
                if (BackgroundImagePath != null) return string.Format("{0}({1}/{2})", CurrentBackgroundImageFile, BackgroundImageIndex, BackgroundImageCount);
                else if (IsGrabbing) return "Grabbing";
                else return "Ready";
            }
        }

        /// <summary>
        /// ?袁⑥쨮??노뼒 揶쏆뮇?붺몴?獄쏆꼹??
        /// </summary>
        /// <returns></returns>
        public int PropertyCount {
            get => Properties.Count;
        }

        /// <summary>
        /// ????index???袁⑥쨮??노뼒??獄쏆꼹???뺣뼄.
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        public decimal GetProperty(int index) {
            if (index >= Properties.Count) return 0;
            return Properties[index];
        }

        /// <summary>
        /// ???筌왖 域밸챶?????묐뻬??랁?野껉퀗?????筌왖??獄쏆꼹??
        /// </summary>
        /// <returns></returns>
        public virtual HImage GrabHalconImage() {
            SetSoftwareTriggerMode();
            return LastHalconImage;
        }

        /// <summary>
        /// ??쎈뱜?깆눘????뽰삂??뺣뼄.
        /// </summary>
        /// <returns></returns>
        public virtual bool StartStream() {
            CaptureMode = ECaptureModeType.Streaming;

            if(GuiReadyForDisplay != null) {
                GuiReadyForDisplay(Name);
            }
            return true;
        }
        
        /// <summary>
        /// ??쎈뱜?깆눘???類???뺣뼄.
        /// </summary>
        public virtual void StopStream() {
            CaptureMode = ECaptureModeType.Stop;
            ClearLastFrame();
        }

        /// <summary>
        /// ??疫?餓λ쵐肉? trigger?????筌왖??獄쏆꼹???뺣뼄.
        /// </summary>
        /// <param name="timeOut"></param>
        /// <returns></returns>
        public virtual HImage WaitForHalconTrigger(bool clone = true, int timeOut = 3000) {
            Stopwatch watch = new Stopwatch();
            watch.Start();
            while (true) {
                if (watch.ElapsedMilliseconds > timeOut) break;
            }

            if (!clone) {
                lock (Interlock) {
                    return GetCurrentImageNoLock();
                }
            }

            return LastHalconImage;
        }

        public virtual BitmapSource GetPreviewBitmapSource() {
            lock (Interlock) {
                HImage image = GetCurrentImageNoLock();
                return image == null ? null : CreateBitmapSource(image);
            }
        }

        protected static BitmapSource CreateBitmapSource(HImage image) {
            int channelCount = image.CountChannels().I;
            if (channelCount == 1) {
                IntPtr pointer = image.GetImagePointer1(out string _, out int width, out int height);
                byte[] pixels = new byte[width * height];
                Marshal.Copy(pointer, pixels, 0, pixels.Length);
                BitmapSource bitmap = BitmapSource.Create(width, height, 96, 96, PixelFormats.Gray8, null, pixels, width);
                bitmap.Freeze();
                return bitmap;
            }

            image.GetImagePointer3(out IntPtr redPtr, out IntPtr greenPtr, out IntPtr bluePtr, out string _, out int width3, out int height3);
            int pixelCount = width3 * height3;
            byte[] red = new byte[pixelCount];
            byte[] green = new byte[pixelCount];
            byte[] blue = new byte[pixelCount];
            byte[] pixels24 = new byte[pixelCount * 3];
            Marshal.Copy(redPtr, red, 0, pixelCount);
            Marshal.Copy(greenPtr, green, 0, pixelCount);
            Marshal.Copy(bluePtr, blue, 0, pixelCount);

            for (int index = 0, pixelIndex = 0; index < pixelCount; index++, pixelIndex += 3) {
                pixels24[pixelIndex] = red[index];
                pixels24[pixelIndex + 1] = green[index];
                pixels24[pixelIndex + 2] = blue[index];
            }

            BitmapSource bitmapColor = BitmapSource.Create(width3, height3, 96, 96, PixelFormats.Rgb24, null, pixels24, width3 * 3);
            bitmapColor.Freeze();
            return bitmapColor;
        }
    }
}




