
using System;
using ReringProject.Define;
using ReringProject.UI;
using ReringProject.Utility;
using PropertyTools.DataAnnotations;
using ReringProject.Device;
using AlligatorAlgMil;
using System.IO;
using OpenCvSharp;
using System.Windows;
using ReringProject.Network;
using ReringProject.Setting;
using static AlligatorAlgMil.ALLIGATOR_ALG_MIL;
using System.Diagnostics;
using System.Collections.Generic;
using System.Threading;

namespace ReringProject.Sequence {

    public class BottomDieInfo
    {
        public double DieCenter_X { get; set; } // int -> double
        public double DieCenter_Y { get; set; } // int -> double
        public double DieAngle { get; set; }
        public int ContourCount { get; set; }
        public double ContourArea { get; set; }
        public int ApexCount { get; set; }
        public bool Judgment { get; set; }
        public double CenterOffsetXmm { get; set; }
        public double CenterOffsetYmm { get; set; }

        public Mat image { get; set; }

        public BottomDieInfo()
        {
            DieCenter_X = 0;
            DieCenter_Y = 0;
            DieAngle = 0;
            ContourCount = 0;
            ContourArea = 0;
            ApexCount = 0;
            Judgment = false;

            CenterOffsetXmm = 0;
            CenterOffsetYmm = 0;

            image = null;
        }
    }

    public class BottomInspectionContext : ActionContext {
        //Response data
        public double CenterOffsetXmm { get; set; }
        public double CenterOffsetYmm { get; set; }
        public double dAngle { get; set; }
        public int DieCenter_X { get; set; }
        public int DieCenter_Y { get; set; }

        public double Die_Area { get; set; }        // 01.04
        public double Die_Area_Min { get; set; }        // 01.04
        public double Die_Area_Max { get; set; }        // 01.04
        public EVisionResultType InspectResult { get; set; }

        public double ScreenCenter_X { get; set; }      // mm  = pCamera.Width/2 * 1PixelResolution.
        public double ScreenCenter_Y { get; set; }      // mm  = pCamera.Height/2 * 1PiexelResolution.

        // 01.04 insert Start
        public string SeqName { get; set; }
        
        public double Cal_XBase { get; set; }
        public double Cal_YBase { get; set; }
        // 01.04 insert End

        // 02.29 Insert Start
        public double[] X_Picker_CalValue = new double[10];
        public double[] Y_Picker_CalValue = new double[10];
        public int CalSelector { get; set; }
        // 02.29 Insert End


        //Thread 처리 Array
        public double[] CenterOffsetXmmArray { get; set; } = new double[10];
        public double[] CenterOffsetYmmArray { get; set; } = new double[10];
        public double[] dAngleArray { get; set; } = new double[10];
        public double [] DieCenter_XArray { get; set; } = new double[10];   // int -> double
        public double [] DieCenter_YArray { get; set; } = new double[10];   // int -> double
        public EVisionResultType[] InspectResultArray { get; set; } = new EVisionResultType[10];

        public bool bFinish { get; set; }
        public string ProcessName { get; set; }
        public int GrabCount { get; set; }

        //Thread에 전달 Parameter
        public string strParamBaseName { get; set; }
        public string strActionBaseName { get; set; }
        public string strID { get; set; }

        public Dictionary<int, BottomDieInfo> BottomDie = new Dictionary<int, BottomDieInfo>();

        //public System.Windows.Rect ROI_Rect = new System.Windows.Rect();
        public OpenCvSharp.Rect ROI_Rect = new OpenCvSharp.Rect();
        public OpenCvSharp.Rect Inner_ROI_Rect = new OpenCvSharp.Rect();    // 03.13 Insert

        public Mat DrawResult = new Mat();      // 01.03
        public int PickerNum { get; set; }

        public EVisionResultType ModelFinderResult { get; set; }    // 01.09

        public Mat mask = new Mat(2048, 2448, MatType.CV_8UC1);     // created Black Mask Image 01.11

        public BottomInspectionContext(ActionBase source) : base(source)
        {
        }

        public override void Clear() {

            GrabCount = 0;
            CenterOffsetXmm = 0;
            CenterOffsetYmm = 0;

            dAngle = 0;
            DieCenter_X = 0;
            DieCenter_Y = 0;

            bFinish = false;

            ProcessName = "None";

            BottomDie.Clear();

            InspectResult = EVisionResultType.NG;

            // 초기화 코드 추가
            for (int i=0; i<10; i++)
            {
                CenterOffsetXmmArray[i] = 0;
                CenterOffsetYmmArray[i] = 0;
                dAngleArray[i] = 0;
                DieCenter_XArray[i] = 0;
                DieCenter_YArray[i] = 0;
                InspectResultArray[i] = EVisionResultType.NG;

                // 02.29 Insert Start
                X_Picker_CalValue[i] = 0;
                Y_Picker_CalValue[i] = 0;
                // 02.29 Insert End
            }

            ROI_Rect.X = 0;
            ROI_Rect.Y = 0;

            DrawResult = null;      // 01.03 insert
            PickerNum = 0;          // 01.03 insert

            Die_Area = 0;           // 01.04 insert
            Die_Area_Min = 0;       // 01.04 insert
            Die_Area_Max = 0;       // 01.04 insert

            //01.08 insert Start
            SeqName = null;
            
            Cal_XBase = 0;
            Cal_YBase = 0;
            //01.08 insert End

            base.Clear();
        }
    }

    //[Serializable]
    public class BottomInspectionParam : CameraSlaveParam {
        public readonly int AlgIndex;
        public readonly int ModelIndex;
        private Mat GrayImage = null;

        [Category("Bottom Inspection")]
        [Rectangle, Converter(typeof(UI.RectConverter))]
        public System.Windows.Rect ROI { get; set; }

        //view는 반드시 생성자에서 생성해야 한다.
        [Content]
        public ModelFinderView Module { get; private set; }

        [ModelFinder, Browsable(false)]
        public ModelFinderViewModel ModuleModel{ get; set; } = new ModelFinderViewModel("Module", EAlgorithmType.ModelFinder);



        [Category("Inspection Param")]
        [DisplayName("Process Name")]
        [ReadOnly(true)]
        public string ProcessName
        {
            get { return _ProcessName; }
            set
            {
                _ProcessName = value;
                RaisePropertyChanged("ProcessName");
            }
        }
        private string _ProcessName;

        [DisplayName("Die Width(um)")]
        public double Die_Width
        {
            get { return _Die_Wdith; }
            set
            {
                _Die_Wdith = value;
                RaisePropertyChanged("Die_Width");
            }
        }
        private double _Die_Wdith;

        [DisplayName("Die Height(um)")]
        public double Die_Height
        {
            get { return _Die_Height; }
            set
            {
                _Die_Height = value;
                RaisePropertyChanged("Die_Height");
            }
        }
        private double _Die_Height;

        [DisplayName("Die Area(pixel)")]
        [ReadOnly(true)]
        public double Die_Area
        {
            get { return _Die_Area; }
            set
            {
                _Die_Area = value;
                RaisePropertyChanged("Die_Area");
            }
        }
        private double _Die_Area;

        [Spinnable(0.1, 0.9, 0.1, 0.9)]
        [DisplayName("Die Width Ratio(%)")]
        public double Die_Width_Ratio
        {
            get { return _Die_Width_Ratio; }
            set
            {
                _Die_Width_Ratio = value;
                RaisePropertyChanged("Die_Width_Ratio");
            }
        }
        private double _Die_Width_Ratio;

        [Spinnable(0.1, 0.9, 0.1, 0.9)]
        [DisplayName("Die Height Ratio(%)")]
        public double Die_Height_Ratio
        {
            get { return _Die_Height_Ratio; }
            set
            {
                _Die_Height_Ratio = value;
                RaisePropertyChanged("Die_Height_Ratio");
            }
        }
        private double _Die_Height_Ratio;

        [DisplayName("Die Area Ratio(%)")]
        public double Die_Area_Ratio
        {
            get { return _Die_Area_Ratio; }
            set
            {
                _Die_Area_Ratio = value;
                RaisePropertyChanged("Die_Area_Ratio");
            }
        }
        private double _Die_Area_Ratio;

        [DisplayName("X_Base")]
        [ReadOnly(true)]
        public double Xbase
        {
            get { return _Xbase; }
            set
            {
                _Xbase = value;
                RaisePropertyChanged("Xbase");
            }
        }
        private double _Xbase;

        [DisplayName("Y_Base")]
        [ReadOnly(true)]
        public double Ybase
        {
            get { return _Ybase; }
            set
            {
                _Ybase = value;
                RaisePropertyChanged("Ybase");
            }
        }
        private double _Ybase;

        
        [Spinnable(0, 0.5, 0, 0.5)]
        [DisplayName("Tolerance Angle")]
        public double Tolearance_Angle
        {
            get { return _Tolerance_Angle; }
            set
            {
                _Tolerance_Angle = value;
                RaisePropertyChanged("Tolearance_Angle");
            }
        }
        private double _Tolerance_Angle;

        [DisplayName("Picker Count")]
        [ReadOnly(true)]
        public int Grab_Count
        {
            get { return _Grab_Count; }
            set
            {
                _Grab_Count = value;
                RaisePropertyChanged("Grab_Count");
            }
        }
        private int _Grab_Count;

        [Spinnable(1, 2, 0, 9)]
        [DisplayName("Display Picker Num")]
        public int Display_Picker_Num
        {
            get { return _Display_Picker_Num; }
            set
            {
                _Display_Picker_Num = value;
                RaisePropertyChanged("Display_Picker_Num");
            }
        }
        private int _Display_Picker_Num;

        [Spinnable(1, 128, 1, 128)]
        [DisplayName("Threshold Value")]
        public int Threshold
        {
            get { return _Threshold; }
            set
            {
                _Threshold = value;
                RaisePropertyChanged("Threshold");
            }
        }
        private int _Threshold;

        [Spinnable(1, 128, 1, 128)]
        [DisplayName("Canny Threshold LOW")]
        public int CannyThresholdLow
        {
            get { return _CannyThresholdLow; }
            set
            {
                _CannyThresholdLow = value;
                RaisePropertyChanged("CannyThresholdLow");
            }
        }
        private int _CannyThresholdLow;

        [Spinnable(1, 128, 1, 128)]
        [DisplayName("Canny Threshold HIGH")]
        public int CannyThresholdHigh
        {
            get { return _CannyThresholdHigh; }
            set
            {
                _CannyThresholdHigh = value;
                RaisePropertyChanged("CannyThresholdHigh");
            }
        }
        private int _CannyThresholdHigh;

        [DisplayName("Use Mophology(Dilate)")]
        [CheckableItems("Mophology")]
        public bool Mophology
        {
            get { return _Mophology; }
            set
            {
                _Mophology = value;
                RaisePropertyChanged("Mophology");
            }
        }
        private bool _Mophology;

        [DisplayName("Camera Grab Debug")]
        [CheckableItems("DebugCheck")]
        public bool DebugCheck
        {
            get { return _DebugCheck; }
            set
            {
                _DebugCheck = value;
                RaisePropertyChanged("DebugCheck");
            }
        }
        private bool _DebugCheck;

        [DisplayName("Origin Image Save")]
        [CheckableItems("OriginImg")]
        public bool OriginImg
        {
            get { return _OriginImg; }
            set
            {
                _OriginImg = value;
                RaisePropertyChanged("OriginImg");
            }
        }
        private bool _OriginImg;

        [DisplayName("Inspection Image Save")]
        [CheckableItems("CrobImg")]
        public bool CrobImg
        {
            get { return _CrobImg; }
            set
            {
                _CrobImg = value;
                RaisePropertyChanged("CrobImg");
            }
        }
        private bool _CrobImg;  // Result Crob Image

        [DisplayName("Debug Image Save")]
        [CheckableItems("DebugImgSave")]
        public bool DebugImgSave
        {
            get { return _DebugImgSave; }
            set
            {
                _DebugImgSave = value;
                RaisePropertyChanged("DebugImgSave");
            }
        }
        private bool _DebugImgSave;

        // 02.28 Insert Start
        public enum CalibrationBase { Screen = 0, Picker = 1 }
        [Category("CalibrationBase")]
        public CalibrationBase CalSelector
        {
            get
            { return _CalSelector; }
            set
            {
                _CalSelector = value;
                RaisePropertyChanged("CalSelector");
            }
        }
        private CalibrationBase _CalSelector;
        // 02.28 Insert End

        // 03.04 Insert Start
        [Browsable(false)]
        public int InitCalSelector
        {
            get { return _InitCalSelector; }
            set
            {
                _InitCalSelector = value;
                RaisePropertyChanged("InitCalSelector");
            }
        }
        private int _InitCalSelector;
        // 03.04 Insert End


        public string SequenceName { get; set; }
        public string ActionName { get; set; }
        public string OwnerName { get; set; }


        public BottomInspectionParam(object owner, int algIndex, int modelIndex) : base(owner) {
            AlgIndex = algIndex;
            ModelIndex = modelIndex;

            //model finder
            Module = new ModelFinderView(ModuleModel);
            Module.RegisterNewButtonClick(Model_NewButton_Click);
            Module.RegisterEditButtonClick(Model_EditButton_Click);
            Module.RegisterLoadButtonClick(Model_LoadButton_Click);
        }
        
        public bool CvTemplateMatching(Mat source, Mat masterImage, System.Windows.Rect roi, double minScore, out double foundX, out double foundY, out double foundScore) {
            bool bFound = false;
            minScore = 0;
            foundX = 0;
            foundY = 0;
            foundScore = 0;

            if (source == null || masterImage == null) return false;

            Mat roiImage = null;
            Mat resultImage = new Mat();
            try {
                OpenCvSharp.Rect cvRoi;
                OpenCvSharp.Point minLoc;
                OpenCvSharp.Point maxLoc;
                minScore /= 100.0f; //주어진 score는 100단위 이므로 

                cvRoi.X = (int)roi.X;
                cvRoi.Y = (int)roi.Y;
                cvRoi.Width = (int)roi.Width;
                cvRoi.Height = (int)roi.Height;
                roiImage = source.SubMat(cvRoi);

                Cv2.MatchTemplate(roiImage, masterImage, resultImage, TemplateMatchModes.CCoeffNormed); //maxLoc
                Cv2.MinMaxLoc(resultImage, out double minVal, out double maxVal, out minLoc, out maxLoc);

                foundScore = maxVal * 100;
                if (maxVal >= minScore) {
                    bFound = true;
                    foundX = maxLoc.X;
                    foundY = maxLoc.Y;
                    foundX += cvRoi.X;
                    foundY += cvRoi.Y;
                }
                else {
                    bFound = false;
                    foundX = 0;
                    foundY = 0;
                }

            }
            catch(Exception e) {
                bFound = false;
                foundX = 0;
                foundY = 0;
                //occurs error 
                Logging.PrintErrLog((int)ELogType.Error, string.Format("Exception ReturnCode:{0}", "Template matching Error", e.ToString()));
            }
            finally {
                if (roiImage != null) roiImage.Dispose();
                if (resultImage != null) resultImage.Dispose();
            }
            
            return bFound;
        }

        public bool LoadModuleExternalFile()
        {
            if (File.Exists(ModuleModel.ModelFile) && (Path.GetExtension(ModuleModel.ModelFile) == DeviceHandler.EXTENSION_MODEL))
            {
                if (ALLIGATOR_ALG_MIL.agtAM_LoadModel_CS(AlgIndex, ModelIndex, ModuleModel.ModelFile) != 0)
                {
                    return false;
                }
            }
            else return false;
            return true;
        }

        //module exist
        void Model_NewButton_Click(object sender, RoutedEventArgs e)
        {
            //지정된 영역으로 새 모델 생성
            ALLIGATOR_ALG_MIL.agtAM_SetModel_CS(AlgIndex, ModelIndex, (int)ModuleModel.Master.Left, (int)ModuleModel.Master.Right, (int)ModuleModel.Master.Top, (int)ModuleModel.Master.Bottom);
            if (ALLIGATOR_ALG_MIL.agtAM_FindConfigDialog(AlgIndex, ModelIndex) != 0)
            {
                CustomMessageBox.Show("Fail to New Model", string.Format("{0} - Model Finder, New Model Fail. AlgIndex : {1}, ModelIndex : {2}, File : {3}", this.GetType().Name, AlgIndex, ModelIndex, ModuleModel.ModelFile), MessageBoxImage.Error);
            }
        }

        void Model_EditButton_Click(object sender, RoutedEventArgs e)
        {
            //모델 파일이 있다면 로드한 후, edit 창을 연다.
            if (File.Exists(ModuleModel.ModelFile))
            {
                if (LoadModuleExternalFile() == false)
                {
                    CustomMessageBox.Show("Fail to Load Model", string.Format("{0} - Model Finder, Load Fail. AlgIndex : {1}, ModelIndex : {2}, File : {3}", this.GetType().Name, AlgIndex, ModelIndex, ModuleModel.ModelFile), MessageBoxImage.Error);
                    return;
                }

                if (ALLIGATOR_ALG_MIL.agtAM_FindConfigDialog(AlgIndex, ModelIndex) != 0)
                {
                    CustomMessageBox.Show("Fail to Open Model Dialog", string.Format("{0} - Model Finder, Dialog Open Fail. AlgIndex : {1}, ModelIndex : {2}, File : {3}", this.GetType().Name, AlgIndex, ModelIndex, ModuleModel.ModelFile), MessageBoxImage.Error);
                    return;
                }
                return;
            }

            //없으면 지정된 영역으로 생성
            Model_NewButton_Click(null, null);
        }

        void Model_LoadButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog();
            //dialog.FileName = "Document"; 
            dialog.DefaultExt = DeviceHandler.EXTENSION_MODEL;
            dialog.Filter = DeviceHandler.FILTER_MODEL;
            dialog.InitialDirectory = Path.GetDirectoryName(ModuleModel.ModelFile);

            // Show open file dialog box
            bool? result = dialog.ShowDialog();
            if (result == true)
            {
                ModuleModel.ModelFile = dialog.FileName;
                if (LoadModuleExternalFile() == false)
                {
                    CustomMessageBox.Show("Fail to Load Model", string.Format("{0} - Model Finder, Load Fail. AlgIndex : {1}, ModelIndex : {2}, File : {3}", this.GetType().Name, AlgIndex, ModelIndex, ModuleModel.ModelFile), MessageBoxImage.Error);
                }
            }
        }


        public override bool Load(IniFile loadFile, string group)
        {
            bool result = base.Load(loadFile, group);

            //load pattern
            if (LoadModuleExternalFile() == false)
            {
                CustomMessageBox.Show("Fail to Load Pattern", string.Format("{0} - Model Finder, Load Fail. AlgIndex : {1}, ModelIndex : {2}, File : {3}", this.GetType().Name, AlgIndex, ModelIndex, ModuleModel.ModelFile), MessageBoxImage.Error);
                result = false;
            }

            // 03.05 Insert Start
            CalSelector = (BottomInspectionParam.CalibrationBase)InitCalSelector;
            InitCalSelector = (int)CalSelector;
            // 03.05 Insert End

            return result;
        }

        public override bool Save(IniFile saveFile, string group)
        {
            //model file은 항상 recipe directory 내부에 저장한다. (외부 파일 사용 시, 파일 유실로 인한 recipe 손실이 발생할 수 있으므로)
            ModuleModel.ModelFile = GetExternalFilePath(EExternalFileType.Model, nameof(ModuleModel));
            if (ALLIGATOR_ALG_MIL.agtAM_SaveModel_CS(AlgIndex, ModelIndex, ModuleModel.ModelFile) != 0)
            {
                CustomMessageBox.Show("Fail to Save Model", string.Format("{0} - Model Finder, Save Fail. AlgIndex : {1}, ModelIndex : {2}, File : {3}", this.GetType().Name, AlgIndex, ModelIndex, ModuleModel.ModelFile), MessageBoxImage.Error);
            }

            // 03.05 Insert Start
            InitCalSelector = (int)CalSelector;
            // 03.05 Insert End

            return base.Save(saveFile, group);
        }


        public override void PutImage(Mat image) {
            if (image == null) return;

            if (GrayImage == null || GrayImage.IsDisposed) {
                GrayImage = new Mat(image.Size(), MatType.CV_8UC1);
            }
            else if ((GrayImage.Width != image.Width) || (GrayImage.Height != image.Height)) {
                GrayImage = new Mat(image.Size(), MatType.CV_8UC1);
            }
            Cv2.CvtColor(image, GrayImage, ColorConversionCodes.BGR2GRAY);

            int result = ALLIGATOR_ALG_MIL.agtAM_PutImage(AlgIndex, GrayImage.Data);
            if (result != 0) {
                CustomMessageBox.Show("Fail to PutImage", string.Format("PutImage from Camera : {0}, AlgIndex : {1}", DeviceName, AlgIndex), MessageBoxImage.Error);
            }
        }

        public override bool CopyTo(ParamBase param) {
            if (base.CopyTo(param) == false) return false;

            if (param is BottomInspectionParam)
            {
                BottomInspectionParam BIP = param as BottomInspectionParam;

                BIP.ROI = ROI;

                BIP.ModuleModel.Master = ModuleModel.Master;

                if (File.Exists(ModuleModel.ModelFile) && (Path.GetExtension(ModuleModel.ModelFile) == DeviceHandler.EXTENSION_MODEL))
                {
                    BIP.ModuleModel.ModelFile = BIP.GetExternalFilePath(EExternalFileType.Model, nameof(BIP.ModuleModel));
                    File.Copy(ModuleModel.ModelFile, BIP.ModuleModel.ModelFile, true);
                    if (BIP.LoadModuleExternalFile() == false)
                    {
                        CustomMessageBox.Show("Fail to Load Model", string.Format("{0} - Model Finder, Load Fail. AlgIndex : {1}, ModelIndex : {2}, File : {3}",
                            BIP.ToString(), BIP.AlgIndex, BIP.ModelIndex, BIP.ModuleModel.ModelFile), MessageBoxImage.Error);
                        return false;
                    }
                }

                BIP.Die_Width = Die_Width;
                BIP.Die_Height = Die_Height;
                BIP.Die_Area = Die_Area;
                BIP.Die_Area_Ratio = Die_Area_Ratio;
                BIP.Tolearance_Angle = Tolearance_Angle;
                BIP.Display_Picker_Num = Display_Picker_Num;
                BIP.Threshold = Threshold;
                BIP.DebugCheck = DebugCheck;
                BIP.DebugImgSave = DebugImgSave;

                BIP.PixelToUM_Offset = PixelToUM_Offset;
                BIP.LightLevel = LightLevel;
                BIP.PropertyArray = PropertyArray;

                BIP.CalSelector = CalSelector;          // Insert 03.04
                BIP.InitCalSelector = InitCalSelector;  // Insert 03.04

                return true;
            }

            return true;
        }

        public override double ConvertPixelToMM(double pixel) {
            return base.ConvertPixelToMM(pixel);
        }
    }

    // 이미지 처리를 위한 클래스 정의
    // Grab 된 이미지를 thread로 처리
    class ImageProcessor
    {
        //private Mat GrayImage = null;
        private int result = -1;
        private int AlgIndexForThread;
        public Mat GrayImage = null;

        public void RotationMatrix(double angle, double X, double Y, ref double RX, ref double RY)
        {
            double radian = angle * (float)(Math.PI / 180);
            RX = Math.Cos(radian) * (X) - Math.Sin(radian) * (Y);
            RY = Math.Sin(radian) * (X) + Math.Cos(radian) * (Y);
        }

        public bool ProcessImage(int ImageGrabIndex, Mat ImageBuffer, BottomInspectionContext pMyContext, BottomInspectionParam pMyParam)
        {
            if (ImageGrabIndex >= pMyParam.Grab_Count)
            {
                Logging.PrintErrLog((int)ELogType.Error, "ProcessImage input Index is Out Of Range: {0}", ImageGrabIndex.ToString());
                return false;
            }

            string strName = (pMyParam.AlgIndex == SequenceHandler.FrontBottom_Alg_Index ? "Front Bottom" : "Rear Bottom");
            AlgIndexForThread = pMyParam.AlgIndex * 10 + ImageGrabIndex;
            Logging.PrintLog((int)ELogType.Trace, "{0} : Image Grabbed {1}", strName, (ImageGrabIndex + 1).ToString());
            Mat img = ImageBuffer;

            if (img == null)
            {
                Logging.PrintErrLog((int)ELogType.Error, "ProcessImage Input Image is Null ");
                return false;
            }

            if (ImageGrabIndex == pMyParam.Display_Picker_Num)   // 01.03 MainView 에 선택된 Picker 의 이미지를 표시. 모든 Picker 를 실시간으로 표시하는것은 안됨.
            {
                pMyContext.ResultImage = ImageBuffer.Clone();
                pMyContext.PickerNum = pMyParam.Display_Picker_Num;
            }

            // Origin Image Save
            // 02.16 Origin Image 저장유무에 대한 parameter 변수 추가.
            if (pMyParam.OriginImg == true)
            {
                string strMapPath = Path.Combine(SystemHandler.Handle.Setting.ImageSavePath,
                        String.Format("{0:0000}{1:00}{2:00}/{3}", DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, pMyParam.ProcessName + "_Bottom_Origin"));
                if (Directory.Exists(strMapPath) == false)
                    Directory.CreateDirectory(strMapPath);
                string strFileName = Path.Combine(strMapPath, String.Format($"{DateTime.Now.Hour}_{DateTime.Now.Minute}_{DateTime.Now.Second}_{ImageGrabIndex}Image.bmp", ImageGrabIndex));
                Cv2.ImWrite(strFileName, img);
            }

            double dResolution = pMyParam.PixelToUM_Offset; // 1 pixel size (um) 02.14
            
            BottomDieInfo temp = new BottomDieInfo();
            Mat CrobImg = new Mat();    // 이미지에서 관심 영역만 잘라서 결과 이미지 저장.
            Mat DrawImg = new Mat(img.Size(), MatType.CV_8UC1);

            //Debug.WriteLine($"Image chanel:{ImageBuffer.Channels()}");

            if (ImageBuffer.Channels() != 1)
            {
                Cv2.CvtColor(ImageBuffer, DrawImg, ColorConversionCodes.BGR2GRAY);
            }
            else
                DrawImg = ImageBuffer.Clone();  // DrawImg = ImageBuffer; 에서 Clone() 추가하여 수정 01.04

            Cv2.BitwiseAnd(DrawImg, pMyContext.mask, DrawImg);      // 01.11 Mask  ROI 영역 이외에는 Black 처리.

            if(pMyParam.DebugImgSave == true)
            {
                // Binary 처리 전 ROI 영역을 제외한 영역은 모두 Black 처리된 이미지 저장.
                string MaskImgPath = Path.Combine(SystemHandler.Handle.Setting.ImageSavePath,
                    String.Format("{0:0000}{1:00}{2:00}/{3}", DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, pMyParam.ProcessName + "_Masking"));

                if (Directory.Exists(MaskImgPath) == false)
                    Directory.CreateDirectory(MaskImgPath);

                string MaskImgFileName = Path.Combine(MaskImgPath, String.Format($"{DateTime.Now.Hour}_{DateTime.Now.Minute}_{DateTime.Now.Second}_{ImageGrabIndex}_Masking.bmp", ImageGrabIndex));
                Cv2.ImWrite(MaskImgFileName, DrawImg); // Origin
            }



            Mat binary = new Mat(DrawImg.Size(),MatType.CV_8UC1);     // binary Image
            // Origin 03.05
            Cv2.Threshold(DrawImg, binary, pMyParam.Threshold, 255, ThresholdTypes.Binary);   // Normal Threshold

            if (pMyParam.Mophology == true)
            {
                // Die 바이너리 이미지에서 소량의 픽셀이 경계점에서 빠지는 경우 채워 넣기 위해 사용함. (사용 유/무 옵션 처리)
                Mat element = Cv2.GetStructuringElement(MorphShapes.Cross, new OpenCvSharp.Size(3, 3));
                Cv2.Dilate(binary, binary, element, new OpenCvSharp.Point(2, 2), 2);    // 01.11 Dilate insert (contours의 개수가 맞지 않아 추가함. 조명 및 exposure time, gain 조정 후 사용 여부 확인 필요)
            }

            if (pMyParam.DebugImgSave == true)
            {
                // 1차 마스킹된 이미지에 대해 Binary 처리 및 Dilate 처리된 이미지 저장.
                string BinaryImgPath = Path.Combine(SystemHandler.Handle.Setting.ImageSavePath,
                    String.Format("{0:0000}{1:00}{2:00}/{3}", DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, pMyParam.ProcessName + "_Binary"));

                if (Directory.Exists(BinaryImgPath) == false)
                    Directory.CreateDirectory(BinaryImgPath);

                string BinaryImgFileName = Path.Combine(BinaryImgPath, String.Format($"{DateTime.Now.Hour}_{DateTime.Now.Minute}_{DateTime.Now.Second}_{ImageGrabIndex}_Binary.bmp", ImageGrabIndex));
                Cv2.ImWrite(BinaryImgFileName, binary);
            }

            #region Die LineAngle Start

            //put image
            // 01.11 ROI 영역에 대한 Masking 된 이미지를 넘겨 기울어진 각을 구하게 함. 현재 OrientSearch 함수에 ROI 입력 처리가 되어 있지 않음.
            result = ALLIGATOR_ALG_MIL.agtAM_PutImage(AlgIndexForThread, binary.Data);
            if (result != 0)
            {
                Logging.PrintLog((int)ELogType.Error, "Failed {0} in {1} ReturnCode:{2}", "agtAM_PutImage", pMyContext.strID, result);
                pMyContext.InspectResultArray[ImageGrabIndex] = EVisionResultType.NG;
                return false;
            }

            // Line의 Angle을 찾는다
            int nAngleCnt = 0;
            float[] fAngle = new float[10];
            float[] fAngleScr = new float[10];
            float fGetAngle = 0;
            float fGetAngleScr = 0;
            float calAngle = 0;
            ALLIGATOR_ALG_MIL.agtAM_OrientSearch(AlgIndexForThread, 10, true);
            ALLIGATOR_ALG_MIL.agtAM_OrientResult(AlgIndexForThread, out nAngleCnt, fAngle, fAngleScr);
            int nMax = 0;
            for (int i = 0; i < nAngleCnt; i++)
            {
                if (fAngleScr[i] > nMax)
                {
                    nMax = (int)fAngleScr[i];
                    fGetAngleScr = fAngleScr[i];
                    fGetAngle = fAngle[i];
                }
            }
            // Die의 기울어진 각도를 찾아 수평을 맞추기 위해 회전하여야 하는 값을 계산. 
            // 반시계 방향이 Positive 방향, 시계 방향이 Negative 방향.
            if (fGetAngle < 45.0) calAngle = -fGetAngle;
            // 반시계 방향이 Negative방향, 시계 방향이 Positive방향.
            //if (fGetAngle < 45.0) calAngle = fGetAngle;
            else if (fGetAngle >= 45.0 && fGetAngle < 135.0) calAngle = 90 - fGetAngle;
            else if (fGetAngle > 135) calAngle = 180 - fGetAngle;

            // Picker 에서 회전해야 하는 방향의 각도 (정방향은 시계 반대 방향)
            //pMyContext.dAngleArray[ImageGrabIndex] = calAngle;      // OrientSearch Algorithm Angle 

            // Picker 에서 회전해야 하는 방향의 각도 (정방향은 시계 방향)
            calAngle = (-1) * calAngle;   // 01.17
            pMyContext.dAngleArray[ImageGrabIndex] = calAngle;      // OrientSearch Algorithm Angle

            #endregion Die LineAngle End


            #region Contour Start

            Cv2.CvtColor(DrawImg, DrawImg, ColorConversionCodes.GRAY2BGR);  // Contours Drawing Image

            OpenCvSharp.Point[][] contours;
            HierarchyIndex[] hierarchy;
            Cv2.FindContours(binary, out contours, out hierarchy, RetrievalModes.List, ContourApproximationModes.ApproxNone);
            //Debug.WriteLine($"Picker NUm:{ImageGrabIndex}, Contour Length:{contours.Length}");

            double maxArea = 0;
            int maxIndex = -1;
            for (int j = 0; j < contours.Length; j++)
            {
                double area = Cv2.ContourArea(contours[j]);
                if (area > maxArea)
                {
                    maxArea = area;
                    maxIndex = j;
                }
            }

            // 02.05 Insert Start
            if(maxIndex == -1)
            {
                // Contour object를 못찾은 경우, Exception 발생하여 데이터 처리 필요.
                Logging.PrintErrLog((int)ELogType.Error, $"{ImageGrabIndex} - Not Found Inspection Die Image:{maxIndex}");

                temp.Judgment = false;
                Cv2.PutText(DrawImg, "Judgment: BAD", new OpenCvSharp.Point(DrawImg.Width / 2, DrawImg.Height / 2),
                    HersheyFonts.HersheyComplex, 1, Scalar.Red, 1, LineTypes.AntiAlias);

                CrobImg = DrawImg.SubMat(pMyContext.ROI_Rect);  // Bottom Die Center 좌표를 기준으로 ROI 영역만 저장.
                temp.image = CrobImg;

                // Exception 12.05
                try
                {
                    pMyContext.BottomDie.Add(ImageGrabIndex, temp);
                }
                catch (Exception e)
                {
                    Logging.PrintErrLog((int)ELogType.Error, string.Format("BottomInspection Exception : {0})", "BottomDie.Add", e.Message));
                    return false;
                }

                pMyContext.InspectResultArray[ImageGrabIndex] = EVisionResultType.NG;
                return true;
            }
            // 02.05 Insert End

            string str_area = null;
            try
            {
                //Debug.WriteLine($"515-Bottom Index: {ImageGrabIndex}, Angle:{calAngle}");
                str_area = string.Format("{0:F1}", Cv2.ContourArea(contours[maxIndex]).ToString());
            }
            catch(Exception e)
            {
                Logging.PrintErrLog((int)ELogType.Error, $"{e.Message}");
            }

            string Shapes = "Polygon";

            string str_RTRect_Angle = string.Format("{0:F3}", calAngle);        // OrientSearch Algorithm Angle

            //Debug.WriteLine($"518-Bottom Index: {ImageGrabIndex}, RTRect=> Center_X:{RTRect.Center.X}, Center_Y:{RTRect.Center.Y}, Size:{RTRect.Size}, Angle:{RTRect.Angle}, AngleString:{str_RTRect_Angle}");
            Moments moments = Cv2.Moments(contours[maxIndex], true);
            OpenCvSharp.Point[] approx = Cv2.ApproxPolyDP(contours[maxIndex], Cv2.ArcLength(contours[maxIndex], true) * 0.005, true);   // Epsilon accuracy 0.02  -> 0.005
            bool convex = Cv2.IsContourConvex(contours[maxIndex]);
            if (convex == true) Shapes = "Polygon";
            else
                Shapes = "RECT";

            double objectCenter_X = 0;
            double objectCenter_Y = 0;
            pMyContext.DieCenter_XArray[ImageGrabIndex] = objectCenter_X = moments.M10 / moments.M00;
            pMyContext.DieCenter_YArray[ImageGrabIndex] = objectCenter_Y = moments.M01 / moments.M00;

            Debug.WriteLine($"Bottom Index: {ImageGrabIndex}, Center Position=> X:{objectCenter_X}, Y:{objectCenter_Y}");

            // Draw Object Center
            Cv2.Circle(DrawImg, (int)(moments.M10 / moments.M00), (int)(moments.M01 / moments.M00), 10, Scalar.Red, -1);

            // Draw Contours Line
            Cv2.DrawContours(DrawImg, contours, -1, new Scalar(0, 0, 255), 2, LineTypes.AntiAlias, null, 1);

            // Inspection Infomation
            Cv2.PutText(DrawImg, "PickerNum:" + ImageGrabIndex.ToString(), new OpenCvSharp.Point(objectCenter_X + 20, objectCenter_Y - 50),
                HersheyFonts.HersheyComplex, 1, Scalar.Black, 2, LineTypes.AntiAlias);
            Cv2.PutText(DrawImg, "Contours cnt:" + contours.Length.ToString(), new OpenCvSharp.Point(objectCenter_X + 20, objectCenter_Y),
                HersheyFonts.HersheyComplex, 1, Scalar.Black, 1, LineTypes.AntiAlias);
            Cv2.PutText(DrawImg, "Area:" + str_area, new OpenCvSharp.Point(objectCenter_X + 20, objectCenter_Y + 25),
                HersheyFonts.HersheyComplex, 1, Scalar.Black, 1, LineTypes.AntiAlias);
            //Cv2.PutText(DrawImg, "Shapes:" + Shapes, new OpenCvSharp.Point(objectCenter_X + 20, objectCenter_Y + 50),
            Cv2.PutText(DrawImg, "Angle:" + str_RTRect_Angle, new OpenCvSharp.Point(objectCenter_X + 20, objectCenter_Y + 50),
                HersheyFonts.HersheyComplex, 1, Scalar.Black, 1, LineTypes.AntiAlias);
            Cv2.PutText(DrawImg, "Apex Count:" + approx.Length.ToString(), new OpenCvSharp.Point(objectCenter_X + 20, objectCenter_Y + 75),
                HersheyFonts.HersheyComplex, 1, Scalar.Black, 1, LineTypes.AntiAlias);

            Debug.WriteLine($"Bottom Index: {ImageGrabIndex}, Object Info => Contours Count:{contours.Length}, Area:{str_area}, Shape:{Shapes}, Apex Count:{approx.Length}, Angle:{str_RTRect_Angle}");


            temp.DieCenter_X = objectCenter_X;
            temp.DieCenter_Y = objectCenter_Y;
            temp.DieAngle = calAngle;           // OrientSearch Algorithm Angle
            temp.ContourCount = contours.Length;
            temp.ContourArea = Cv2.ContourArea(contours[maxIndex]);
            temp.ApexCount = approx.Length;

            #endregion Contour End


            // Screen Center 기준으로 Die Center 의 Rotation matrix and Translation matrix 계산에 따른 이동 값.
            double Pos_X = 0;
            double Pos_Y = 0;

            // 02.29 Insert Start
            if (pMyParam.CalSelector == BottomInspectionParam.CalibrationBase.Picker)
            {
                pMyParam.Xbase = pMyContext.X_Picker_CalValue[ImageGrabIndex];  // 03.14 Insert
                pMyParam.Ybase = pMyContext.Y_Picker_CalValue[ImageGrabIndex];  // 03.14 Insert
                BottomInspectionAction.Translation(objectCenter_X, objectCenter_Y, calAngle, pMyContext.X_Picker_CalValue[ImageGrabIndex], pMyContext.Y_Picker_CalValue[ImageGrabIndex], ref Pos_X, ref Pos_Y);
            }
            else
            {
                pMyParam.Xbase = DrawImg.Width / 2;     // 03.14 Insert
                pMyParam.Ybase = DrawImg.Height / 2;    // 03.14 Insert
                BottomInspectionAction.Translation(objectCenter_X, objectCenter_Y, calAngle, DrawImg.Width / 2, DrawImg.Height / 2, ref Pos_X, ref Pos_Y);
            }
            // 02.29 Insert End

            pMyParam.InitCalSelector = (int)pMyParam.CalSelector;   // 03.04 Insert 

            pMyContext.CenterOffsetXmmArray[ImageGrabIndex] = temp.CenterOffsetXmm = Pos_X * dResolution / 1000;       // um to mm
            pMyContext.CenterOffsetYmmArray[ImageGrabIndex] = temp.CenterOffsetYmm = Pos_Y * dResolution / 1000;       // um to mm


            //if ((contours.Length == 1) && (convex == false) && (approx.Length == 4) && ((calAngle < 0.5) && (calAngle > -0.5)))
            //if ((contours.Length == 1) && (convex == false) && (approx.Length == 4) && ((calAngle < pMyParam.Tolearance_Angle) && (calAngle > -pMyParam.Tolearance_Angle))) // 01.04 주석 처리
            if ((contours.Length == 1) && (convex == false) && (approx.Length == 4) && 
                ((temp.ContourArea >= pMyParam.Die_Area * pMyParam.Die_Area_Ratio) && (temp.ContourArea <= pMyParam.Die_Area * (2 - pMyParam.Die_Area_Ratio))) && 
                ((calAngle < pMyParam.Tolearance_Angle) && (calAngle > -pMyParam.Tolearance_Angle)))
            {

                // 03.14 Insert Start
                // Bottom Die 의 중심 값 기준으로 Die Width/Height Ratio 만큼 축소된 위치의 좌표 산출.
                double X1 = (objectCenter_X - ((pMyParam.Die_Width / pMyParam.PixelToUM_Offset) * pMyParam.Die_Width_Ratio) / 2);
                double X2 = (objectCenter_X + ((pMyParam.Die_Width / pMyParam.PixelToUM_Offset) * pMyParam.Die_Width_Ratio) / 2);
                double Y1 = (objectCenter_Y - ((pMyParam.Die_Height / pMyParam.PixelToUM_Offset) * pMyParam.Die_Height_Ratio) / 2);
                double Y2 = (objectCenter_Y + ((pMyParam.Die_Height / pMyParam.PixelToUM_Offset) * pMyParam.Die_Height_Ratio) / 2);

                pMyContext.Inner_ROI_Rect.X = (int)X1;
                pMyContext.Inner_ROI_Rect.Y = (int)Y1;
                pMyContext.Inner_ROI_Rect.Width = (int)(X2 - X1);
                pMyContext.Inner_ROI_Rect.Height = (int)(Y2 - Y1);

                #region ImageRoatation Start
                Mat RotationImg = ImageBuffer.Clone();

                Mat Matrix = Cv2.GetRotationMatrix2D(new Point2f((float)objectCenter_X, (float)objectCenter_Y), -calAngle, 1.0);
                Cv2.WarpAffine(RotationImg, RotationImg, Matrix, RotationImg.Size(), InterpolationFlags.Linear);

                if (pMyParam.DebugImgSave == true)
                {
                    Mat DrawInnerROIRectImage = RotationImg.Clone();

                    if(DrawInnerROIRectImage.Channels() != 3)
                        Cv2.CvtColor(DrawInnerROIRectImage, DrawInnerROIRectImage, ColorConversionCodes.GRAY2BGR);

                    Cv2.Circle(DrawInnerROIRectImage, (int)(moments.M10 / moments.M00), (int)(moments.M01 / moments.M00), 10, Scalar.Red, -1);
                    Cv2.Rectangle(DrawInnerROIRectImage, pMyContext.Inner_ROI_Rect, Scalar.Red, 1);

                    string DieInnerImgPath = Path.Combine(SystemHandler.Handle.Setting.ImageSavePath,
                        String.Format("{0:0000}{1:00}{2:00}/{3}", 
                        DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, pMyParam.ProcessName + "_InnerRect"));

                    if (Directory.Exists(DieInnerImgPath) == false)
                        Directory.CreateDirectory(DieInnerImgPath);

                    string DieInnerImgFileName = Path.Combine(DieInnerImgPath, 
                        String.Format($"{DateTime.Now.Hour}_{DateTime.Now.Minute}_{DateTime.Now.Second}_{ImageGrabIndex}_InnerRect.bmp", ImageGrabIndex));

                    Cv2.ImWrite(DieInnerImgFileName, DrawInnerROIRectImage);
                }
                #endregion ImageRoatation End

                Mat InnerImg = RotationImg.SubMat(pMyContext.Inner_ROI_Rect);   // 03.14 Insert (Canny Eadge Processing 을 위한 이미지)

                // 03.18 Insert Start
                if (InnerImg.Channels() != 1)
                    Cv2.CvtColor(InnerImg, InnerImg, ColorConversionCodes.BGR2GRAY);

                //Cv2.ImWrite("./InnerImg.bmp", InnerImg);
                // 03.18 Insert End

                #region Histogram Start
                /*
                // 03.15 Histogram Calculate Start
                //Mat HistResult = Mat.Ones(new OpenCvSharp.Size(InnerImg.Width, InnerImg.Height), MatType.CV_8UC1);
                Mat HistResult = Mat.Ones(new OpenCvSharp.Size(256, InnerImg.Height), MatType.CV_8UC1);
                //Mat HistResult = Mat.Ones(new OpenCvSharp.Size(300, 300), MatType.CV_8UC1);
                Mat gray = new Mat();
                Mat hist = new Mat();
                Cv2.CalcHist(new Mat[] { InnerImg}, new int[] { 0 }, null, hist, 1, new int[] { 256 }, new Rangef[] { new Rangef(0, 256)});
                Cv2.Normalize(hist, hist, 0, 255, NormTypes.MinMax);

                for(int i=0; i<hist.Rows; i++)
                {
                    Cv2.Line(HistResult, new OpenCvSharp.Point(i, InnerImg.Height), new OpenCvSharp.Point(i, InnerImg.Height - hist.Get<float>(i)), Scalar.White);
                }

                Cv2.ImWrite("./HitogramImage.bmp", HistResult);
                // 03.15 Histogram Calculate End
                */
                #endregion Histogram end

                Mat CannyContourImg = new Mat(InnerImg.Size(), MatType.CV_8UC3);
                Cv2.CvtColor(InnerImg, CannyContourImg, ColorConversionCodes.GRAY2BGR);
                Cv2.Canny(InnerImg, InnerImg, pMyParam.CannyThresholdLow, pMyParam.CannyThresholdHigh);
                //Cv2.ImWrite("./CannyEdge.bmp", InnerImg);

                #region MophologyEx Start
                /*
                Mat element = Cv2.GetStructuringElement(MorphShapes.Ellipse, new OpenCvSharp.Size(3, 3));  // Origin 2, 2

                Cv2.Dilate(InnerImg, InnerImg, element, new OpenCvSharp.Point(2, 2), 2);
                Cv2.Erode(InnerImg, InnerImg, element, new OpenCvSharp.Point(2, 2), 2);

                Cv2.MorphologyEx(InnerImg, InnerImg, MorphTypes.Close, element);
                Cv2.ImWrite("./CannyEdgetoMopholy.bmp", InnerImg);
                */
                #endregion MophologyEx End

                OpenCvSharp.Point[][] Cannycontours;
                HierarchyIndex[] Cannyhierarchy;
                Cv2.FindContours(InnerImg, out Cannycontours, out Cannyhierarchy, RetrievalModes.List, ContourApproximationModes.ApproxNone);
                Debug.WriteLine($"Picker NUm:{ImageGrabIndex}, Contour Length:{Cannycontours.Length}");

                Cv2.DrawContours(CannyContourImg, Cannycontours, -1, new Scalar(0, 0, 255), 2, LineTypes.AntiAlias, null, 1);
                //Cv2.ImWrite("./CannyEdgecontour.bmp", CannyContourImg);
                // 03.14 Insert End

                if (Cannycontours.Length == 0)
                {
                    temp.Judgment = true;
                    Cv2.PutText(DrawImg, "Judgment: GOOD", new OpenCvSharp.Point(objectCenter_X + 20, objectCenter_Y + 100),
                        HersheyFonts.HersheyComplex, 1, Scalar.Blue, 1, LineTypes.AntiAlias);

                    CrobImg = DrawImg.SubMat(pMyContext.ROI_Rect);  // Bottom Die Center 좌표를 기준으로 ROI 영역만 저장.
                    temp.image = CrobImg;

                    // Exception    12.05
                    try
                    {
                        pMyContext.BottomDie.Add(ImageGrabIndex, temp);
                    }
                    catch (Exception e)
                    {
                        Logging.PrintErrLog((int)ELogType.Error, string.Format("BottomInspection Exception : {0})", "BottomDie.Add", e.Message));
                        return false;
                    }

                    pMyContext.InspectResultArray[ImageGrabIndex] = EVisionResultType.OK;
                }
                else
                {
                    temp.Judgment = false;

                    Cv2.PutText(CannyContourImg, "Fail - Crack CNT:" + Cannycontours.Length.ToString(), new OpenCvSharp.Point(CannyContourImg.Width / 4, CannyContourImg.Height / 2),
                    HersheyFonts.HersheyComplex, 1, Scalar.Red, 1, LineTypes.AntiAlias);

                    temp.image = CrobImg = CannyContourImg.Clone();

                    try
                    {
                        pMyContext.BottomDie.Add(ImageGrabIndex, temp);
                    }
                    catch (Exception e)
                    {
                        Logging.PrintErrLog((int)ELogType.Error, string.Format("BottomInspection Exception : {0})", "BottomDie.Add", e.Message));
                        return false;
                    }

                    pMyContext.InspectResultArray[ImageGrabIndex] = EVisionResultType.NG;
                }

                if (pMyParam.CrobImg == true)
                {
                    string strMapPath = Path.Combine(SystemHandler.Handle.Setting.ImageSavePath,
                            String.Format("{0:0000}{1:00}{2:00}/{3}", DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, pMyParam.ProcessName + "_Bottom_Inspection"));
                    if (Directory.Exists(strMapPath) == false)
                        Directory.CreateDirectory(strMapPath);
                    string strFileName = Path.Combine(strMapPath, String.Format($"{DateTime.Now.Hour}_{DateTime.Now.Minute}_{DateTime.Now.Second}_{ImageGrabIndex}Image.bmp", ImageGrabIndex));
                    Cv2.ImWrite(strFileName, CrobImg);
                }

                return true;
            }
            else
            {
                temp.Judgment = false;
                Cv2.PutText(DrawImg, "Judgment: BAD", new OpenCvSharp.Point(objectCenter_X + 20, objectCenter_Y + 100),
                    HersheyFonts.HersheyComplex, 1, Scalar.Red, 1, LineTypes.AntiAlias);

                CrobImg = DrawImg.SubMat(pMyContext.ROI_Rect);  // Bottom Die Center 좌표를 기준으로 ROI 영역만 저장.
                temp.image = CrobImg;

                // 02.16 Insert Start
                if (pMyParam.CrobImg == true)
                {
                    string strMapPath = Path.Combine(SystemHandler.Handle.Setting.ImageSavePath,
                            String.Format("{0:0000}{1:00}{2:00}/{3}", DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, pMyParam.ProcessName + "_Bottom_Inspection"));
                    if (Directory.Exists(strMapPath) == false)
                        Directory.CreateDirectory(strMapPath);
                    string strFileName = Path.Combine(strMapPath, String.Format($"{DateTime.Now.Hour}_{DateTime.Now.Minute}_{DateTime.Now.Second}_{ImageGrabIndex}Image.bmp", ImageGrabIndex));
                    Cv2.ImWrite(strFileName, CrobImg);
                }
                // 02.16 Insert End

                // Exception 12.05
                try
                {
                    pMyContext.BottomDie.Add(ImageGrabIndex, temp);
                }
                catch (Exception e)
                {
                    Logging.PrintErrLog((int)ELogType.Error, string.Format("BottomInspection Exception : {0})", "BottomDie.Add", e.Message));
                    return false;
                }

                pMyContext.InspectResultArray[ImageGrabIndex] = EVisionResultType.NG;
                return true;
            }
        }
    }

    public class BottomInspectionAction : ActionBase {
        private readonly int AlgIndex;
        private readonly int ModelIndex;

        private BottomInspectionContext pMyContext;
        private BottomInspectionParam pMyParam;

        private const int COUNT_IMAGE_GRAB = 10;
        private VirtualCamera pCamera;
        private Mat GrayImage = null;

        private int result = -1;

        //HW multi trigger
        private int ImageGrabIndex = 0;
        private Mat[] ImageBuffer = new Mat[COUNT_IMAGE_GRAB];
        public Mat GrabImage = new Mat();


        // Grab 된 이미지 Thread로 처리
        private ImageProcessor[] imageProcessor = new ImageProcessor[10];
        private CountdownEvent countdownEvent;
        List<bool> processingResults = new List<bool>();

        public enum EStep {
            Init = 0,
            TriggerReady = 1,
            ProcessGrabImage = 2,
            Grab = 3,
            GrabImage = 4,
            End = 5,
        }

        public BottomInspectionAction(EAction id, string name, int algIndex, int modelIndex) : base(id, name) {
            
            AlgIndex = algIndex;
            ModelIndex = modelIndex;
            
            Context = new BottomInspectionContext(this);
            pMyContext = Context as BottomInspectionContext;

            Param = new BottomInspectionParam(this, algIndex, ModelIndex);
            pMyParam = Param as BottomInspectionParam;

            ImageGrabIndex = 0;

            for(int i=0; i<10; i++)
            {
                imageProcessor[i] = new ImageProcessor();
            }
            
            processingResults.Clear();
            //GrabImg.Clear();
        }
        
        public override void OnCreate() {
            base.OnCreate();
        }

        public override void OnLoad() {
            //camera property setting
            pCamera = SystemHandler.Handle.Devices[pMyParam.DeviceName];
            if (pCamera != null) {
                if (pCamera.Properties == null) {
                    CustomMessageBox.Show(pCamera.Name+ " Camera Not Open!", "Camera is not open. Please check your connection status.", MessageBoxImage.Error);
                    return;
                }
                if (!pCamera.Properties.ApplyFromParam(pMyParam)) {
                    CustomMessageBox.Show(pCamera.Name + " Camera Property Set Fail!", "Check camera settings. or camera state.", MessageBoxImage.Error);
                }
                if (!pCamera.SetTriggerMode(ETriggerSource.Hardware_Line0))
                {
                    CustomMessageBox.Show(pCamera.Name + " Camera hardware trigger mode Set Fail!", "Check camera settings. or camera state.", MessageBoxImage.Error);
                }
            }
            else {
                CustomMessageBox.Show(pMyParam.DeviceName + " Camera Not Open!", "Camera is not open. Please check your connection status.", MessageBoxImage.Error);
                return;
            }

            // 01.04 insert start
            //pMyParam.Die_Area = ((pMyParam.Die_Width / pMyParam.PixelToMM_Offset) * (pMyParam.Die_Height / pMyParam.PixelToMM_Offset)); // Pixel (Contours Area 와 비교하기 위해 Pixel 값으로 저장)
            pMyParam.Die_Area = ((pMyParam.Die_Width / pMyParam.PixelToUM_Offset) * (pMyParam.Die_Height / pMyParam.PixelToUM_Offset)); // Pixel (Contours Area 와 비교하기 위해 Pixel 값으로 저장) 02.14

            // 01.16 Insert
            pMyParam.Grab_Count = COUNT_IMAGE_GRAB;

            pMyParam.CalSelector = (BottomInspectionParam.CalibrationBase)pMyParam.InitCalSelector; // Insert 03.04


            base.OnLoad();
        }

        public override void OnBegin(SequenceContext prevResult = null) {
            ImageGrabIndex = 0;
			for (int i = 0; i < COUNT_IMAGE_GRAB; i++)
            {
                if ((ImageBuffer[i] != null) && (ImageBuffer[i].IsDisposed == false))
                {
                    ImageBuffer[i].Dispose();
                    ImageBuffer[i] = null;
                }
            }

            // 01.08 Home Insert Start
            pMyContext.SeqName = SystemHandler.Handle.Sequences[ParentID].ToString();   // 현재 실행하고 있는 Sequence name 가져오기

            if (pMyContext.SeqName == "FRONT_BOTTOM")
            {
                BottomCalibrationParam FCalParam = SystemHandler.Handle.Sequences[ParentID][EAction.FrontBottom_Calibration].Param as BottomCalibrationParam;
                pMyParam.Xbase =  pMyContext.Cal_XBase = FCalParam.BaseX;
                pMyParam.Ybase =  pMyContext.Cal_YBase = FCalParam.BaseY;
                /*
                for(int i=0; i<10; i++)
                {
                    pMyContext.X_Picker_CalValue[i] = FCalParam.X_Picker[i];
                    pMyContext.Y_Picker_CalValue[i] = FCalParam.Y_Picker[i];
                }
                */
                pMyContext.X_Picker_CalValue[0] = FCalParam.X_Picker_0;
                pMyContext.Y_Picker_CalValue[0] = FCalParam.Y_Picker_0;

                pMyContext.X_Picker_CalValue[1] = FCalParam.X_Picker_1;
                pMyContext.Y_Picker_CalValue[1] = FCalParam.Y_Picker_1;

                pMyContext.X_Picker_CalValue[2] = FCalParam.X_Picker_2;
                pMyContext.Y_Picker_CalValue[2] = FCalParam.Y_Picker_2;

                pMyContext.X_Picker_CalValue[3] = FCalParam.X_Picker_3;
                pMyContext.Y_Picker_CalValue[3] = FCalParam.Y_Picker_3;

                pMyContext.X_Picker_CalValue[4] = FCalParam.X_Picker_4;
                pMyContext.Y_Picker_CalValue[4] = FCalParam.Y_Picker_4;

                pMyContext.X_Picker_CalValue[5] = FCalParam.X_Picker_5;
                pMyContext.Y_Picker_CalValue[5] = FCalParam.Y_Picker_5;

                pMyContext.X_Picker_CalValue[6] = FCalParam.X_Picker_6;
                pMyContext.Y_Picker_CalValue[6] = FCalParam.Y_Picker_6;

                pMyContext.X_Picker_CalValue[7] = FCalParam.X_Picker_7;
                pMyContext.Y_Picker_CalValue[7] = FCalParam.Y_Picker_7;

                pMyContext.X_Picker_CalValue[8] = FCalParam.X_Picker_8;
                pMyContext.Y_Picker_CalValue[8] = FCalParam.Y_Picker_8;

                pMyContext.X_Picker_CalValue[9] = FCalParam.X_Picker_9;
                pMyContext.Y_Picker_CalValue[9] = FCalParam.Y_Picker_9;


                // 02.28 Insert Start
                //BottomCalibrationContext FCalContext =
                //    SystemHandler.Handle.Sequences[ParentID][EAction.FrontBottom_Calibration].Context as BottomCalibrationContext;
                //for (int i = 0; i < 10; i++)
                //{
                //    pMyContext.X_Picker_CalValue[i] = FCalContext.X_Picker[i];
                //    pMyContext.Y_Picker_CalValue[i] = FCalContext.Y_Picker[i];
                //}
                // 02.28 Insert End
            }
            else if (pMyContext.SeqName == "REAR_BOTTOM")
            {
                BottomCalibrationParam RCalParam = SystemHandler.Handle.Sequences[ParentID][EAction.RearBottom_Calibration].Param as BottomCalibrationParam;
                pMyParam.Xbase = pMyContext.Cal_XBase = RCalParam.BaseX;
                pMyParam.Ybase = pMyContext.Cal_YBase = RCalParam.BaseY;

                //for (int i = 0; i < 10; i++)
                //{
                //    pMyContext.X_Picker_CalValue[i] = RCalParam.X_Picker[i];
                //    pMyContext.Y_Picker_CalValue[i] = RCalParam.Y_Picker[i];
                //}

                pMyContext.X_Picker_CalValue[0] = RCalParam.X_Picker_0;
                pMyContext.Y_Picker_CalValue[0] = RCalParam.Y_Picker_0;

                pMyContext.X_Picker_CalValue[1] = RCalParam.X_Picker_1;
                pMyContext.Y_Picker_CalValue[1] = RCalParam.Y_Picker_1;

                pMyContext.X_Picker_CalValue[2] = RCalParam.X_Picker_2;
                pMyContext.Y_Picker_CalValue[2] = RCalParam.Y_Picker_2;

                pMyContext.X_Picker_CalValue[3] = RCalParam.X_Picker_3;
                pMyContext.Y_Picker_CalValue[3] = RCalParam.Y_Picker_3;

                pMyContext.X_Picker_CalValue[4] = RCalParam.X_Picker_4;
                pMyContext.Y_Picker_CalValue[4] = RCalParam.Y_Picker_4;

                pMyContext.X_Picker_CalValue[5] = RCalParam.X_Picker_5;
                pMyContext.Y_Picker_CalValue[5] = RCalParam.Y_Picker_5;

                pMyContext.X_Picker_CalValue[6] = RCalParam.X_Picker_6;
                pMyContext.Y_Picker_CalValue[6] = RCalParam.Y_Picker_6;

                pMyContext.X_Picker_CalValue[7] = RCalParam.X_Picker_7;
                pMyContext.Y_Picker_CalValue[7] = RCalParam.Y_Picker_7;

                pMyContext.X_Picker_CalValue[8] = RCalParam.X_Picker_8;
                pMyContext.Y_Picker_CalValue[8] = RCalParam.Y_Picker_8;

                pMyContext.X_Picker_CalValue[9] = RCalParam.X_Picker_9;
                pMyContext.Y_Picker_CalValue[9] = RCalParam.Y_Picker_9;

                // 02.28 Insert Start
                //BottomCalibrationContext RCalContext = 
                //    SystemHandler.Handle.Sequences[ParentID][EAction.RearBottom_Calibration].Context as BottomCalibrationContext;
                //for(int i=0; i < 10; i++)
                //{
                //    pMyContext.X_Picker_CalValue[i] = RCalContext.X_Picker[i];
                //    pMyContext.Y_Picker_CalValue[i] = RCalContext.Y_Picker[i];
                //}
                // 02.28 Insert End
            }
            else
            {
                pMyParam.Xbase = pMyContext.Cal_XBase = pCamera.Properties.Width / 2;
                pMyParam.Ybase = pMyContext.Cal_YBase = pCamera.Properties.Height / 2;
            }
            // 01.08 Home Insert Start

            // 01.11 Create ROI Mask Image Start
            System.Windows.Point ROI_Top_Left = pMyParam.ROI.TopLeft;               // ROI Area : TopLeft
            System.Windows.Point ROI_Bottom_Right = pMyParam.ROI.BottomRight;       // ROI Area : BottomRight
            int ROI_TopX = (int)ROI_Top_Left.X;
            int ROI_TopY = (int)ROI_Top_Left.Y;
            int ROI_BotX = (int)ROI_Bottom_Right.X;
            int ROI_BotY = (int)ROI_Bottom_Right.Y;

            pMyContext.ROI_Rect.X = ROI_TopX;
            pMyContext.ROI_Rect.Y = ROI_TopY;
            pMyContext.ROI_Rect.Width = ROI_BotX - ROI_TopX;
            pMyContext.ROI_Rect.Height = ROI_BotY - ROI_TopY;

            Cv2.Rectangle(pMyContext.mask, pMyContext.ROI_Rect, new Scalar(255, 255, 255), -1);
            // 01.11 Create ROI Mask Image End

            // 02.06 Insert Start
            pMyContext.ScreenCenter_X = pCamera.Properties.Width / 2;
            pMyContext.ScreenCenter_Y = pCamera.Properties.Height / 2;
            // 02.06 Insert End

            // 02.15 Insert Start
            // 01.24 insert start
            //string ActName = Param.OwnerName;
            string ActName = Param.Parent.Name; //02.16 insert
            if (ActName == "FRONT_BOTTOM")
                pMyContext.ProcessName = pMyParam.ProcessName = "Front";
            else if (ActName == "REAR_BOTTOM")
                pMyContext.ProcessName = pMyParam.ProcessName = "Rear";
            else
                pMyContext.ProcessName = pMyParam.ProcessName = "None";
            // 01.24 insert End
            // 02.15 Insert End

            // 02.29 Insert Start
            //pMyParam.CalSelector = (BottomInspectionParam.CalibrationBase)pMyParam.InitCalSelector;
            // 02.29 Insert End
            pMyContext.CalSelector = (int)pMyParam.CalSelector; // Insert 03.04

            base.OnBegin(prevResult);
        }

        public override void OnEnd() {
            base.OnEnd();
        }

        public override ActionContext Run() {

            switch ((EStep)Step) {
                case EStep.Init:
                    if (pMyParam.DebugCheck)
                    {
                        this.countdownEvent = new CountdownEvent(COUNT_IMAGE_GRAB); // 10장의 이미지를 기다립니다.
                        Step = (int)EStep.Grab;
                        break;
                    }
                    if (pCamera == null) {
                        pCamera = SystemHandler.Handle.Devices[pMyParam.DeviceName];
                        if (pCamera != null) {
                            if (!pCamera.Properties.ApplyFromParam(pMyParam)) {
                                Logging.PrintLog((int)ELogType.Error, "{0} Camera Set Property Fail!", pCamera.Name);
                            }
                            if (!pCamera.SetTriggerMode(ETriggerSource.Hardware_Line0)) { 
                                Logging.PrintLog((int)ELogType.Error, "{0} Camera Set Hardware Trigger mode Fail!", pCamera.Name);
                            }
                        }
                        else {
                            Logging.PrintLog((int)ELogType.Error, "{0} Camera Handle is null!", pMyParam.DeviceName);
                            FinishAction(EContextResult.Error);
                            break;
                        }
                    }
                    if (!pCamera.SetTriggerMode(ETriggerSource.Hardware_Line0))
                    {
                        CustomMessageBox.Show(pCamera.Name + " Camera Hardware trigger mode Set Fail!", "Check camera settings. or camera state.", MessageBoxImage.Error);
                    }
                    ImageGrabIndex = 0;
                    this.countdownEvent = new CountdownEvent(COUNT_IMAGE_GRAB); // 10장의 이미지를 기다립니다.
                    Step++;
                    break;

                case EStep.TriggerReady:
                    try
                    {
                        ImageBuffer[ImageGrabIndex] = pCamera.WaitForTrigger(true, 5000); // Origin 01.09
                        if (ImageGrabIndex >= pMyParam.Grab_Count)
                        {
                            Logging.PrintLog((int)ELogType.Trace, $"{ImageGrabIndex}-Image Grab Index Over");
                            Logging.PrintLog((int)ELogType.Error, "Trigger Image Null:{0} Camera trigger did not occur during the specified time period!", pMyParam.DeviceName);
                            //pMyContext.ModelFinderResult = EVisionResultType.NG;
                            pMyContext.GrabCount = ImageGrabIndex;
                            //PlanB 코드
                            if (!pCamera.SetTriggerMode(ETriggerSource.Hardware_Line0, true))
                            {
                                CustomMessageBox.Show(pCamera.Name + " Camera Hardware trigger mode Set Fail!", "Check camera settings. or camera state.", MessageBoxImage.Error);
                            }
                            FinishAction(EContextResult.Error);
                            break;
                        }
                    }
                    catch(Exception ex)
                    {
                       Logging.PrintLog((int)ELogType.Trace, $"{ImageGrabIndex}-Image Grab Index Over, {ex.ToString()}");
                       Step++;
                       break;
                    }

                    if (ImageBuffer[ImageGrabIndex] == null)
                    {
                        Logging.PrintLog((int)ELogType.Error, "Trigger Image Null:{0} Camera trigger did not occur during the specified time period!", pMyParam.DeviceName);
                        //pMyContext.ModelFinderResult = EVisionResultType.NG;
                        pMyContext.GrabCount = ImageGrabIndex;
                        //PlanB 코드
                        if (!pCamera.SetTriggerMode(ETriggerSource.Hardware_Line0, true))
                        {
                            CustomMessageBox.Show(pCamera.Name + " Camera Hardware trigger mode Set Fail!", "Check camera settings. or camera state.", MessageBoxImage.Error);
                        }
                        FinishAction(EContextResult.Error);
                        break;
                    }
                    else
                    {
                        Mat img = ImageBuffer[ImageGrabIndex];

                        if (GrayImage == null)
                        {
                            GrayImage = new Mat(img.Size(), MatType.CV_8UC1);
                        }
                        else if ((GrayImage.Width != img.Width) || (GrayImage.Height != img.Height))
                        {
                            GrayImage.Dispose();
                            GrayImage = new Mat(img.Size(), MatType.CV_8UC1);
                        }
                        Cv2.CvtColor(img, GrayImage, ColorConversionCodes.BGR2GRAY);

                        Debug.WriteLine($"ImageIndex:{ImageGrabIndex}");
                        /*
                        if(ImageGrabIndex > pMyParam.Grab_Count - 1)    // 9보다 크다면
                        {
                            Logging.PrintLog((int)ELogType.Error, "Out of Index: {0} Bottom inspection occured error during the inspection(Image Grab Count over the setting)!", pMyParam.DeviceName);
                            FinishAction(EContextResult.Error);
                            break;
                        }
                        */
                        Thread processingThread = new Thread((index) =>
                        {
                            Debug.WriteLine($"1169:Thead-ImageGrabIndex:{index}");
                            // 이미지 인덱스를 스레드 내부에서 사용할 수 있음
                            int localIndex = (int)index;
                            pMyContext.strParamBaseName = Param.Parent.Name;
                            pMyContext.strActionBaseName = this.Name;
                            pMyContext.strID = ID.ToString();
                            bool result = imageProcessor[localIndex].ProcessImage(localIndex, ImageBuffer[localIndex], pMyContext, pMyParam);
                            processingResults.Add(result);
                            // 처리가 완료되면 CountdownEvent 신호를 줄입니다.
                            countdownEvent.Signal();
                        });
                        processingThread.Start(ImageGrabIndex);

                        ImageGrabIndex++;

                        if(ImageGrabIndex > pMyParam.Grab_Count - 1)
                        {
                            Step++;
                            break;
                        }
                    }
                    break;

                case EStep.ProcessGrabImage:
                    // 모든 스레드가 종료될 때까지 최대 5초까지 대기합니다.
                    if (countdownEvent.Wait(TimeSpan.FromSeconds(5)))
                    {
                        // 모든 스레드가 작업을 마치면 다음 단계로 진행합니다.
                        for(int i=0; i< ImageGrabIndex; i++)  // Origin 주석 처리 01.09
                        {
                            if(processingResults[i] == false)
                            {
                                Logging.PrintLog((int)ELogType.Error, "{0} Bottom inspection occured error during the inspection!", pMyParam.DeviceName);
                                FinishAction(EContextResult.Error);
                                break;
                            }
                        }
                    }
                    else
                    {
                        // 타임아웃이 발생한 경우 처리할 코드를 작성합니다.
                        Logging.PrintLog((int)ELogType.Error, "{0} Bottom inspection did not finish during the specified time period!", pMyParam.DeviceName);
                        FinishAction(EContextResult.Error);
                        break;
                    }
                    Step = (int)EStep.End;
                    break;

                case EStep.Grab:
                    if (pCamera == null)
                    {
                        pCamera = SystemHandler.Handle.Devices[pMyParam.DeviceName];
                        if (pCamera != null)
                        {
                            if (!pCamera.Properties.ApplyFromParam(pMyParam))
                            {
                                Logging.PrintLog((int)ELogType.Error, "{0} Camera Set Property Fail!", pCamera.Name);
                            }
                            if (!pCamera.SetSoftwareTriggerMode())
                            {
                                Logging.PrintLog((int)ELogType.Error, "{0} Camera Set Trigger mode Fail!", pCamera.Name);
                            }
                        }
                        else
                        {
                            Logging.PrintLog((int)ELogType.Error, "{0} Camera Handle is null!", pMyParam.DeviceName);
                            FinishAction(EContextResult.Error);
                            break;
                        }
                    }

                    Context.ResultImage = pCamera.GrabImage();

                    if(Context.ResultImage == null)
                    {
                        Logging.PrintLog((int)ELogType.Error, "{0} Camera Image Grab Failed!", pMyParam.DeviceName);

                        FinishAction(EContextResult.Error);
                        break;
                    }
                    if(GrayImage == null)
                    {
                        GrayImage = new Mat(Context.ResultImage.Size(), MatType.CV_8UC1);
                    }
                    else if ((GrayImage.Width != Context.ResultImage.Width) || (GrayImage.Height != Context.ResultImage.Height))
                    {
                        GrayImage.Dispose();
                        GrayImage = new Mat(Context.ResultImage.Size(), MatType.CV_8UC1);
                    }

                    Cv2.CvtColor(Context.ResultImage, GrayImage, ColorConversionCodes.BGR2GRAY);

                    if (ALLIGATOR_ALG_MIL.agtAM_PutImage(AlgIndex, GrayImage.Data) != 0)
                    {
                        Logging.PrintLog((int)ELogType.Error, "Failed {0} in {1} ReturnCode:{2}", "agtAM_PutImage", ID.ToString(), result);

                        FinishAction(EContextResult.Error);
                        break;
                    }

                    if (ImageGrabIndex > (pMyParam.Grab_Count-1))
                    {
                        Step++;
                    }
                    else
                    {
                        //GrabImg[ImageGrabIndex] = GrayImage.Clone();
                        ImageBuffer[ImageGrabIndex] = GrayImage.Clone();

                        ////////////////////////////////////////////////////////////////////////////////
                        // 백그라운드 스레드에서 이미지 처리 실행
                        if (ImageGrabIndex > (pMyParam.Grab_Count-1))
                        {
                            Logging.PrintLog((int)ELogType.Error, "{0} Bottom inspection occured error during the inspection(Image Grab Count over the setting)!", pMyParam.DeviceName);
                            FinishAction(EContextResult.Error);
                            break;
                        }

                        Thread processingThread = new Thread((index) =>
                        {
                            // 이미지 인덱스를 스레드 내부에서 사용할 수 있음
                            int localIndex = (int)index;
                            pMyContext.strParamBaseName = Param.Parent.Name;
                            pMyContext.strActionBaseName = this.Name;
                            pMyContext.strID = ID.ToString();
                            bool result = imageProcessor[localIndex].ProcessImage(localIndex, ImageBuffer[localIndex], pMyContext, pMyParam);
                            //processingResults.Add(result);
                            // 처리가 완료되면 CountdownEvent 신호를 줄입니다.
                            countdownEvent.Signal();
                        });
                        processingThread.Start(ImageGrabIndex);

                        ImageGrabIndex++;
                    }

                    break;

                case EStep.GrabImage:
                    // 모든 스레드가 종료될 때까지 최대 5초까지 대기합니다.
                    if (countdownEvent.Wait(TimeSpan.FromSeconds(5)))
                    {
                        Logging.PrintLog((int)ELogType.Trace, "{0} Bottom inspection finish during the specified time period!", pMyParam.DeviceName);
                    }
                    else
                    {
                        // 타임아웃이 발생한 경우 처리할 코드를 작성합니다.
                        Logging.PrintLog((int)ELogType.Error, "{0} Bottom inspection did not finish during the specified time period!", pMyParam.DeviceName);
                        FinishAction(EContextResult.Error);
                        break;
                    }
                    Step = (int)EStep.End;
                    break;
                    
                case EStep.End:

                    Debug.WriteLine($"Die_Area:{pMyParam.Die_Area}");

                    //Param.Parent.State == EContextState.Running;

                    if (Param.Parent.State == EContextState.Running)
                        pMyContext.bFinish = false;
                    else
                        pMyContext.bFinish = true;

                    pMyContext.ProcessName = pMyParam.ProcessName;
                    
                    // ImageGrabIndex 개수 만큼의 이미지 결과 값 중 NG가 있는지 확인.
                    int ErrCount = 0;
                    for (int i = 0; i < ImageGrabIndex; i++)
                    {
                        if(pMyContext.InspectResultArray[i] == EVisionResultType.NG)
                        {
                            ErrCount++;
                        }
                    }

                    if (ErrCount == 0)      // NG가 없다면, Pass
                        FinishAction(EContextResult.Pass);
                    else                    // NG가 하나라도 있다면, Fail
                        FinishAction(EContextResult.Fail);

                    break;
            }
            return base.Run();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="X_pos"> 회전된 Object의 중심 X </param>
        /// <param name="Y_pos"> 회전된 Object의 중심 Y </param>
        /// <param name="Angle"> Object의 회전각: Screen Coordination System </param>
        /// <param name="X_base"> Picker와 Camera Width의 중심 값이 같다는 조건의 원점 X_base </param>
        /// <param name="Y_base"> Picker와 Camera Height의 중심 값이 같다는 조건의 원점 Y_base </param>
        /// <param name="Pos_X"> Picker와 Camera Width의 중심에서 떨어진 X Position </param>
        /// <param name="Pos_Y"> Picker와 Camera Height의 중심에서 떨어진 Y Position </param>
        public static void Translation(double X_pos, double Y_pos, double Angle, double X_base, double Y_base, ref double Pos_X, ref double Pos_Y)
        {
            double Rotation_X = 0;
            double Rotation_Y = 0;
            double Delta_X = 0;
            double Delta_Y = 0;

            double radian = Angle * (float)(Math.PI / 180);       // Screen Coordination system
            //double radian = -Angle * (float)(Math.PI / 180);        // Normal Coordination System

            Rotation_X = Math.Cos(radian) * (X_pos - X_base) - Math.Sin(radian) * (Y_pos - Y_base) + X_base;
            Rotation_Y = Math.Sin(radian) * (X_pos - X_base) + Math.Cos(radian) * (Y_pos - Y_base) + Y_base;

            Debug.WriteLine(string.Format($"Rotation Postion X:{Rotation_X} pixel, Y:{Rotation_Y} pixel"));

            // Translation Delta
            if (Rotation_X >= X_base)                       // X축 (+) 방향은 오른쪽 (Normal or Screen Coordination System 모두 동일)
                Delta_X = X_base - Rotation_X;              // 이동 해야 하는 negative value
            else
                Delta_X = -(Rotation_X - X_base);           // 이동 해야 하는 positive value

            if (Rotation_Y >= Y_base)                       // Y축 (+) 방향은 아래쪽 Screen Coordination System.
                Delta_Y = -(Rotation_Y - Y_base);           // 이동해야 하는 negative value (02.06 부호 변경)
            else
                Delta_Y = -(Rotation_Y - Y_base);            // 이동해야 하는 Positive value

            Debug.WriteLine(string.Format($"Delta Postion X:{Delta_X} pixel, Y:{Delta_Y} pixel"));

            if (Delta_X >= 0)     // X_축 오른쪽으로 이동해야 함.
                Debug.WriteLine(string.Format($"Trans Postion X:{Rotation_X - Delta_X} pixel"));
            else
                Debug.WriteLine(string.Format($"Trans Postion X:{Rotation_X + Delta_X} pixel"));

            if (Delta_Y >= 0)     // Y_축 위쪽으로 이동 해야 함.
                Debug.WriteLine(string.Format($"Trans Postion Y:{Rotation_Y - Delta_Y} pixel"));
            else
                Debug.WriteLine(string.Format($"Trans Postion Y:{Rotation_Y + Delta_Y} pixel"));

            Pos_X = Delta_X;
            Pos_Y = Delta_Y;

            //Pos_X = Delta_X * pMyParam.PixelToMM_Offset / 1000;      // um to mm (전달 할 X value)
            //Pos_Y = Delta_Y * pMyParam.PixelToMM_Offset / 1000;      // um to mm (전달 할 Y value)

            //Debug.WriteLine(string.Format($"Delta Postion X:{Pos_X} mm, Y:{Pos_Y} mm"));

            //Pos_X = Rotation_X;
            //Pos_Y = Rotation_Y;
        }
    }
}
