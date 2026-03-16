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
using System.Threading;
using System.Collections.Generic;

namespace ReringProject.Sequence {

    public class BottomCalibrationContext : ActionContext {

        /*
        public bool bFoundCircle { get; set; }
        public double CircleCenter_X { get; set; }
        public double CircleCenter_Y { get; set; }
        public double Radius { get; set; }
        public double dRadmm { get; set; }

        public double dDistX { get; set; }
        public double dDistY { get; set; }

        //Response data
        public double CenterOffsetXmm { get; set; }
        public double CenterOffsetYmm { get; set; }
        */

        // 03.11 Insert Start
        public bool[] bFoundCircle = new bool[10];
        public double[] CircleCenter_X = new double[10];
        public double[] CircleCenter_Y = new double[10];
        public double[] Radius = new double[10];
        public double[] dRadmm = new double[10];
        public double[] dDistX = new double[10];
        public double[] dDistY = new double[10];
        public double[] CenterOffsetXmm = new double[10];
        public double[] CenterOffsetYmm = new double[10];

        public int PickerNum { get; set; }
        // 03.11 Insert End

        // 02.29 insert Start
        // Each Picker Calibration value
        public double[] X_Picker = new double[10];
        public double[] Y_Picker = new double[10];
        // 02.29 insert End

        public sCCircleFindResult CircleResult;
        public sCModelFindResult FindResult;        // 03.13 Insert


        public EVisionResultType CalibrationResult{ get; set; }

        // 03.11 Insert Start
        public OpenCvSharp.Rect ROI_Rect = new OpenCvSharp.Rect();
        public Mat mask = new Mat(2048, 2448, MatType.CV_8UC1);     // created Black Mask Image 03.11

        public EVisionResultType[] CalibrationResultArray { get; set; } = new EVisionResultType[10];

        public string strID { get; set; }
        public string strParamBaseName { get; set; }
        public string strActionBaseName { get; set; }

        public int GrabCount { get; set; }
        // 03.11 Insert End

        public BottomCalibrationContext(ActionBase source) : base(source) {

        }

        public override void Clear() {

            //bFoundCircle = false;

            //CircleCenter_X = 0;
            //CircleCenter_Y = 0;
            //Radius = 0;
            //dRadmm = 0;

            //dDistX = 0;
            //dDistY = 0;

            //CenterOffsetXmm = 0;
            //CenterOffsetYmm = 0;

            CalibrationResult = EVisionResultType.NG;

            // 02.29 insert Start
            for(int i=0; i < 10; i++)
            {
                X_Picker[i] = 0;
                Y_Picker[i] = 0;

                Radius[i] = 0;
                dRadmm[i] = 0;
                dDistX[i] = 0;
                dDistY[i] = 0;
                CenterOffsetXmm[i] = 0;
                CenterOffsetYmm[i] = 0;
                CircleCenter_X[i] = 0;
                CircleCenter_Y[i] = 0;

                bFoundCircle[i] = false;
            }
            // 02.29 insert End

            PickerNum = 0;

            base.Clear();
        }
    }

    //[Serializable]
    public class BottomCalibrationParam : CameraSlaveParam {
        public readonly int AlgIndex;
        public readonly int ModelIndex;
        private Mat GrayImage = null;

        //view model은 반드시 생성자 이전에 생성해야 한다.
        [Category("Bottom Calibration")]
        [Rectangle, Converter(typeof(UI.RectConverter))]
        public System.Windows.Rect ROI { get; set; }
        //view는 반드시 생성자에서 생성해야 한다.
        [Content]
        public ModelFinderView Module { get; private set; }

        [ModelFinder, Browsable(false)]
        public ModelFinderViewModel ModuleModel { get; set; } = new ModelFinderViewModel("Module", EAlgorithmType.ModelFinder);



        // 03.11 Insert Start
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

        [DisplayName("Circle Radius")]
        public double CircleRadius
        {
            get { return _CircleRadius; }
            set
            {
                _CircleRadius = value;
                RaisePropertyChanged("CircleRadius");
            }
        }
        private double _CircleRadius;
        // 03.11 Insert End

        //[Spinnable(1, 5, 0, 100)]
        [DisplayName("Circle X")]
        [ReadOnly(true)]
        public double Circle_X 
        {
            get
            {
                return _Circle_X;
            }
            set
            {
                _Circle_X = value;
                RaisePropertyChanged("Circle_X");
            }
        }
        private double _Circle_X;

        [DisplayName("Circle Y")]
        [ReadOnly(true)]
        public double Circle_Y
        {
            get
            {
                return _Circle_Y;
            }
            set
            {
                _Circle_Y = value;
                RaisePropertyChanged("Circle_Y");
            }
        }
        private double _Circle_Y;

        [DisplayName("Radius")]
        [ReadOnly(true)]
        public double Radius
        {
            get
            {
                return _Radius;
            }
            set
            {
                _Radius = value;
                RaisePropertyChanged("Radius");
            }
        }
        private double _Radius;

        [DisplayName("BaseX")]
        [ReadOnly(true)]
        public double BaseX
        {
            get
            {
                return _BaseX;
            }
            set
            {
                _BaseX = value;
                RaisePropertyChanged("BaseX");
            }
        }
        private double _BaseX;

        [DisplayName("BaseY")]
        [ReadOnly(true)]
        public double BaseY
        {
            get
            {
                return _BaseY;
            }
            set
            {
                _BaseY = value;
                RaisePropertyChanged("BaseY");
            }
        }
        private double _BaseY;

        // 03.11 Insert Start
        [DisplayName("Picker Count")]
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
        [DisplayName("Display Picker Num"), Browsable(false)]   // 05.03 Drop Down number 에서 처리하기 때문에 보이지 않게 처리.
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

        [DisplayName("Origin Image Save")]
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

        [DisplayName("Camera Grab Debug")]
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
        // 03.11 Insert End



        // 02.28 Insert Start

        public enum PickerNum { Picker_0 = 0, Picker_1 = 1, Picker_2, Picker_3, Picker_4, Picker_5, Picker_6, Picker_7, Picker_8, Picker_9 };
        [Category("Picker Selection")]

        [DisplayName("World Calibration"), Browsable(false)]    // Display 되지 않게 수정 05.03
        [CheckableItems("WorldCalibration")]
        public bool WorldCalibration
        {
            get { return _WorldCalibration; }
            set
            {
                _WorldCalibration = value;
                RaisePropertyChanged("WorldCalibration");
            }
        }
        private bool _WorldCalibration;
        public PickerNum CalbrationPicker { get; set; }




        //[Browsable(false)]
        public double[] X_Picker = new double[10];
        //[Browsable(false)]
        public double[] Y_Picker = new double[10];

        #region Picker number parameter start
        [ReadOnly(true)]
        public double X_Picker_0
        {
            get { return _X_Picker_0; }
            set
            {
                _X_Picker_0 = value;
                RaisePropertyChanged("X_Picker_0");
            }
        }
        private double _X_Picker_0;
        [ReadOnly(true)]
        public double X_Picker_1
        {
            get { return _X_Picker_1; }
            set
            {
                _X_Picker_1 = value;
                RaisePropertyChanged("X_Picker_1");
            }
        }
        private double _X_Picker_1;
        [ReadOnly(true)]
        public double X_Picker_2
        {
            get { return _X_Picker_2; }
            set
            {
                _X_Picker_2 = value;
                RaisePropertyChanged("X_Picker_2");
            }
        }
        private double _X_Picker_2;
        [ReadOnly(true)]
        public double X_Picker_3
        {
            get { return _X_Picker_3; }
            set
            {
                _X_Picker_3 = value;
                RaisePropertyChanged("X_Picker_3");
            }
        }
        private double _X_Picker_3;

        [ReadOnly(true)]
        public double X_Picker_4
        {
            get { return _X_Picker_4; }
            set
            {
                _X_Picker_4 = value;
                RaisePropertyChanged("X_Picker_4");
            }
        }
        private double _X_Picker_4;

        [ReadOnly(true)]
        public double X_Picker_5
        {
            get { return _X_Picker_5; }
            set
            {
                _X_Picker_5 = value;
                RaisePropertyChanged("X_Picker_5");
            }
        }
        private double _X_Picker_5;

        [ReadOnly(true)]
        public double X_Picker_6
        {
            get { return _X_Picker_6; }
            set
            {
                _X_Picker_6 = value;
                RaisePropertyChanged("X_Picker_6");
            }
        }
        private double _X_Picker_6;
        [ReadOnly(true)]
        public double X_Picker_7
        {
            get { return _X_Picker_7; }
            set
            {
                _X_Picker_7 = value;
                RaisePropertyChanged("X_Picker_7");
            }
        }
        private double _X_Picker_7;
        [ReadOnly(true)]
        public double X_Picker_8
        {
            get { return _X_Picker_8; }
            set
            {
                _X_Picker_8 = value;
                RaisePropertyChanged("X_Picker_8");
            }
        }
        private double _X_Picker_8;
        [ReadOnly(true)]
        public double X_Picker_9
        {
            get { return _X_Picker_9; }
            set
            {
                _X_Picker_9 = value;
                RaisePropertyChanged("X_Picker_9");
            }
        }
        private double _X_Picker_9;

        [ReadOnly(true)]
        public double Y_Picker_0
        {
            get { return _Y_Picker_0; }
            set
            {
                _Y_Picker_0 = value;
                RaisePropertyChanged("Y_Picker_0");
            }
        }
        private double _Y_Picker_0;
        [ReadOnly(true)]
        public double Y_Picker_1
        {
            get { return _Y_Picker_1; }
            set
            {
                _Y_Picker_1 = value;
                RaisePropertyChanged("Y_Picker_1");
            }
        }
        private double _Y_Picker_1;
        [ReadOnly(true)]
        public double Y_Picker_2
        {
            get { return _Y_Picker_2; }
            set
            {
                _Y_Picker_2 = value;
                RaisePropertyChanged("Y_Picker_2");
            }
        }
        private double _Y_Picker_2;
        [ReadOnly(true)]
        public double Y_Picker_3
        {
            get { return _Y_Picker_3; }
            set
            {
                _Y_Picker_3 = value;
                RaisePropertyChanged("Y_Picker_3");
            }
        }
        private double _Y_Picker_3;

        [ReadOnly(true)]
        public double Y_Picker_4
        {
            get { return _Y_Picker_4; }
            set
            {
                _Y_Picker_4 = value;
                RaisePropertyChanged("Y_Picker_4");
            }
        }
        private double _Y_Picker_4;
        [ReadOnly(true)]
        public double Y_Picker_5
        {
            get { return _Y_Picker_5; }
            set
            {
                _Y_Picker_5 = value;
                RaisePropertyChanged("Y_Picker_5");
            }
        }
        private double _Y_Picker_5;
        [ReadOnly(true)]
        public double Y_Picker_6
        {
            get { return _Y_Picker_6; }
            set
            {
                _Y_Picker_6 = value;
                RaisePropertyChanged("Y_Picker_6");
            }
        }
        private double _Y_Picker_6;
        [ReadOnly(true)]
        public double Y_Picker_7
        {
            get { return _Y_Picker_7; }
            set
            {
                _Y_Picker_7 = value;
                RaisePropertyChanged("Y_Picker_7");
            }
        }
        private double _Y_Picker_7;
        [ReadOnly(true)]
        public double Y_Picker_8
        {
            get { return _Y_Picker_8; }
            set
            {
                _Y_Picker_8 = value;
                RaisePropertyChanged("Y_Picker_8");
            }
        }
        private double _Y_Picker_8;
        [ReadOnly(true)]
        public double Y_Picker_9
        {
            get { return _Y_Picker_9; }
            set
            {
                _Y_Picker_9 = value;
                RaisePropertyChanged("Y_Picker_9");
            }
        }
        private double _Y_Picker_9;
        #endregion Picker number parameter end

        // 02.28 Insert End

        public string SequenceName { get; set; }
        public string ActionName { get; set; }
        public string OwnerName { get; set; }

        
        public BottomCalibrationParam(object owner, int algIndex, int modelIndex) : base(owner) {
            AlgIndex = algIndex;
            ModelIndex = modelIndex;

            //pattern
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
            //
            ////없으면 지정된 영역으로 생성
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
                //CustomMessageBox.Show("Fail to Load Pattern", string.Format("{0} - Model Finder, Load Fail. AlgIndex : {1}, ModelIndex : {2}, File : {3}", this.GetType().Name, AlgIndex, ModelIndex, ModuleModel.ModelFile), MessageBoxImage.Error);
                result = false;
            }

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
            return base.Save(saveFile, group);
            //return false;
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

            return true;
        }

        public override double ConvertPixelToMM(double pixel) {
            return base.ConvertPixelToMM(pixel);
        }
    }


    #region CalImageProcessor
    // 이미지 처리를 위한 클래스 정의
    // Grab 된 이미지를 thread로 처리
    class CalImageProcessor
    {
        //private Mat GrayImage = null;
        private int result = -1;
        private int AlgIndexForThread;
        public Mat GrayImage = null;
        private ALLIGATOR_ALG_MIL.sCModelFindResult ModelResult = new ALLIGATOR_ALG_MIL.sCModelFindResult();

        public bool CalibrationImage(int ImageGrabIndex, Mat ImageBuffer, BottomCalibrationContext pMyContext, BottomCalibrationParam pMyParam)
        {
            if (ImageGrabIndex >= pMyParam.Grab_Count)
            {
                Logging.PrintErrLog((int)ELogType.Error, "Calibration Image input Index is Out Of Range: {0}", ImageGrabIndex.ToString());
                return false;
            }
            string strName = (pMyParam.AlgIndex == SequenceHandler.FrontBottom_Alg_Index ? "Front Bottom" : "Rear Bottom");
            AlgIndexForThread = pMyParam.AlgIndex * 10 + ImageGrabIndex;
            Logging.PrintLog((int)ELogType.Trace, "{0} Calibration : Image Grabbed {1}", strName, (ImageGrabIndex + 1).ToString());
            Mat img = ImageBuffer;

            if (img == null)
            {
                Logging.PrintErrLog((int)ELogType.Error, "Calibration Input Image is Null ");
                return false;
            }

            // Origin Image Save
            // 02.16 Origin Image 저장유무에 대한 parameter 변수 추가.
            if (pMyParam.OriginImg == true)
            {
                string strMapPath = Path.Combine(SystemHandler.Handle.Setting.ImageSavePath,
                        String.Format("{0:0000}{1:00}{2:00}/{3}", DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, pMyParam.ProcessName + "_BTM_Cal_Origin"));
                if (Directory.Exists(strMapPath) == false)
                    Directory.CreateDirectory(strMapPath);
                string strFileName = Path.Combine(strMapPath, String.Format($"{DateTime.Now.Hour}_{DateTime.Now.Minute}_{DateTime.Now.Second}_{ImageGrabIndex}Image.bmp", ImageGrabIndex));
                Cv2.ImWrite(strFileName, img);
            }

            double dResolution = pMyParam.PixelToUM_Offset; // 1 pixel size (um) 02.14

            Mat CrobImg = new Mat();    // 이미지에서 관심 영역만 잘라서 결과 이미지 저장.
            Mat DrawImg = new Mat(img.Size(), MatType.CV_8UC1);

            Debug.WriteLine($"Image chanel:{ImageBuffer.Channels()}");

            if (ImageBuffer.Channels() != 1)
            {
                Cv2.CvtColor(ImageBuffer, DrawImg, ColorConversionCodes.BGR2GRAY);
            }
            else
                DrawImg = ImageBuffer.Clone();  // DrawImg = ImageBuffer; 에서 Clone() 추가하여 수정 01.04

            Cv2.BitwiseAnd(DrawImg, pMyContext.mask, DrawImg);      // 01.11 Mask  ROI 영역 이외에는 Black 처리.

            if (pMyParam.DebugImgSave == true)
            {
                // Binary 처리 전 ROI 영역을 제외한 영역은 모두 Black 처리된 이미지 저장.
                string MaskImgPath = Path.Combine(SystemHandler.Handle.Setting.ImageSavePath,
                    String.Format("{0:0000}{1:00}{2:00}/{3}", DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, pMyParam.ProcessName + "BTM_Cal_Masking"));

                if (Directory.Exists(MaskImgPath) == false)
                    Directory.CreateDirectory(MaskImgPath);

                string MaskImgFileName = Path.Combine(MaskImgPath, String.Format($"{DateTime.Now.Hour}_{DateTime.Now.Minute}_{DateTime.Now.Second}_{ImageGrabIndex}_Masking.bmp", ImageGrabIndex));
                Cv2.ImWrite(MaskImgFileName, DrawImg); // Origin
            }

            Mat binary = new Mat(DrawImg.Size(), MatType.CV_8UC1);     // binary Image
            Cv2.Threshold(DrawImg, binary, pMyParam.Threshold, 255, ThresholdTypes.Binary);   // Normal Threshold



            if (pMyParam.DebugImgSave == true)
            {
                // 1차 마스킹된 이미지에 대해 Binary 처리 및 Dilate 처리된 이미지 저장.
                string BinaryImgPath = Path.Combine(SystemHandler.Handle.Setting.ImageSavePath,
                    String.Format("{0:0000}{1:00}{2:00}/{3}", DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, pMyParam.ProcessName + "BTM_Cal_Binary"));

                if (Directory.Exists(BinaryImgPath) == false)
                    Directory.CreateDirectory(BinaryImgPath);

                string BinaryImgFileName = Path.Combine(BinaryImgPath, String.Format($"{DateTime.Now.Hour}_{DateTime.Now.Minute}_{DateTime.Now.Second}_{ImageGrabIndex}_Binary.bmp", ImageGrabIndex));
                Cv2.ImWrite(BinaryImgFileName, binary);
            }
            //Cv2.CvtColor(DrawImg, DrawImg, ColorConversionCodes.GRAY2BGR);

            // Display_Picker_NUM 에 표시된 숫자의 PICKER의 이미지를 Main 화면에 이미지를 표시.
            if(ImageGrabIndex == pMyParam.Display_Picker_Num)
            {
                pMyContext.ResultImage = ImageBuffer.Clone();
                pMyContext.PickerNum = pMyParam.Display_Picker_Num;
            }

            //put image
            // 03.11 ROI 영역에 대한 Masking 된 이미지를 넘겨 circle radius, Center Coordination 구하게 함.
            //result = ALLIGATOR_ALG_MIL.agtAM_PutImage(AlgIndexForThread, binary.Data);
            result = ALLIGATOR_ALG_MIL.agtAM_PutImage(AlgIndexForThread, DrawImg.Data);
            if (result != 0)
            {
                Logging.PrintLog((int)ELogType.Error, "Failed {0} in {1} ReturnCode:{2}", "agtAM_PutImage", pMyContext.strID, result);
                pMyContext.CalibrationResultArray[ImageGrabIndex] = EVisionResultType.NG;
                return false;
            }



            // Screen ROI Coordination
            System.Windows.Point Top_Left = pMyParam.ROI.TopLeft;
            System.Windows.Point Bottom_Right = pMyParam.ROI.BottomRight;

            int TopX = (int)Top_Left.X;
            int TopY = (int)Top_Left.Y;
            int BotX = (int)Bottom_Right.X;
            int BotY = (int)Bottom_Right.Y;

            int cnt = 0;

            /*
            #region Modelfind Start
            // Image 모델을 등록해서 Picker의 중심을 찾을 때 사용.
            ModelResult.Init();
            //cnt = agtAM_FindModel_ROI_CS(AlgIndexForThread, 0, TopX, BotX, TopY, BotY, out ModelResult);
            cnt = ALLIGATOR_ALG_MIL.agtAM_FindModel(AlgIndexForThread, 0, out ModelResult);
            if (cnt != 0)
                Debug.WriteLine($"{cnt} is Not Found Model");
            else
            {
                Debug.WriteLine($"--{ImageGrabIndex}, X:{ModelResult.dXPos}, Y:{ModelResult.dYPos}, Score:{ModelResult.dScore}");
            }
            #endregion Modelfind End
            */

            #region CircleFind Start

            // 여러개의 Circle을 찾았을 때, pMyContext.CircleResult에는 Score 값이 제일 높고, 반지름 값이 제일 큰 순서가 제일 먼저 저장이 된다.
            cnt = agtAM_FindCircle_ROI_CS(AlgIndexForThread, pMyParam.CircleRadius, TopX, BotX, TopY, BotY, out pMyContext.CircleResult);   // Defalt Circle Radius is 30
            Debug.WriteLine($"FindCircle Cnt:{cnt}");

            if (cnt != 0)
            {
                pMyContext.bFoundCircle[ImageGrabIndex] = false;
                pMyContext.CalibrationResultArray[ImageGrabIndex] = EVisionResultType.NG;
                return true;
            }
            else
            {
                pMyContext.bFoundCircle[ImageGrabIndex] = true;
                pMyContext.CalibrationResultArray[ImageGrabIndex] = EVisionResultType.OK;

                pMyParam.X_Picker[ImageGrabIndex] = pMyContext.X_Picker[ImageGrabIndex] = pMyContext.CircleCenter_X[ImageGrabIndex] = pMyParam.BaseX = pMyContext.CircleResult.dXPos[0];    // Pixel value
                pMyParam.Y_Picker[ImageGrabIndex] = pMyContext.Y_Picker[ImageGrabIndex] = pMyContext.CircleCenter_Y[ImageGrabIndex] = pMyParam.BaseY = pMyContext.CircleResult.dYPos[0];    // Pixel value
                pMyContext.Radius[ImageGrabIndex] = pMyContext.CircleResult.dRadius[0];

                double dImgCenterX = (double)(img.Width) / 2.0;          // Image Width / 2
                double dImgCenterY = (double)(img.Height) / 2.0;         // Image Height / 2

                if (dImgCenterX > pMyContext.CircleCenter_X[ImageGrabIndex])        // Circle X-Center 가 화면 좌표의 왼쪽에 있다면, 이동해야 하는 방향은 + 방향(오른쪽)
                    pMyContext.dDistX[ImageGrabIndex] = pMyContext.CenterOffsetXmm[ImageGrabIndex] = (dImgCenterX - pMyContext.CircleCenter_X[ImageGrabIndex]) * dResolution / 1000;    // Circle X-Center 와 화면 중심과의 거리에 + 방향으로 이동해야 화면 중심에 도착.
                else                                                // Circle X-Center 가 화면 좌표의오른쪽에 있다면, 이동해야 하는 방향은 - 방향 (왼쪽)
                    //pMyContext.dDistX = pMyContext.CenterOffsetXmm = (pMyContext.CircleCenter_X - dImgCenterX) * dResolution / 1000;
                    pMyContext.dDistX[ImageGrabIndex] = pMyContext.CenterOffsetXmm[ImageGrabIndex] = (dImgCenterX - pMyContext.CircleCenter_X[ImageGrabIndex]) * dResolution / 1000;

                if (dImgCenterY > pMyContext.CircleCenter_Y[ImageGrabIndex])        // Circle Y-Center 가 화면 좌표의 위쪽에 있다면, 이동해야 하는 방향은 + 방향 (아래쪽)
                    pMyContext.dDistY[ImageGrabIndex] = pMyContext.CenterOffsetYmm[ImageGrabIndex] = (dImgCenterY - pMyContext.CircleCenter_Y[ImageGrabIndex]) * dResolution / 1000;
                else                                                // Circle Y-Center 가 화면 좌표의 아래쪽에 있다면, 이동해야 하는 방향은 - 방향 (위쪽)
                    pMyContext.dDistY[ImageGrabIndex] = pMyContext.CenterOffsetYmm[ImageGrabIndex] = -(pMyContext.CircleCenter_Y[ImageGrabIndex] - dImgCenterY) * dResolution / 1000;

                pMyContext.dRadmm[ImageGrabIndex] = pMyContext.Radius[ImageGrabIndex] * dResolution / 1000;    // Radius per mm

                pMyParam.Circle_X = pMyContext.dDistX[ImageGrabIndex];
                pMyParam.Circle_Y = pMyContext.dDistY[ImageGrabIndex];
                pMyParam.Radius = pMyContext.dRadmm[ImageGrabIndex];

                //pMyContext.PickerNum = pMyParam.Display_Picker_Num;

                // Argument로 넘어온 ImageGrabIndex
                switch (ImageGrabIndex)
                {
                    case 0:
                        // pMyParam.WorldCalibration이 체크되었다면, UI에 표시되는 값이 화면 중심에서 Picker 중심까지
                        // offset 을 mm 값으로 이동해야 하는 수치를 표시.
                        if (pMyParam.WorldCalibration)
                        {
                            pMyParam.X_Picker_0 = pMyContext.CenterOffsetXmm[ImageGrabIndex];
                            pMyParam.Y_Picker_0 = pMyContext.CenterOffsetYmm[ImageGrabIndex];
                        }
                        else
                        {
                            pMyParam.X_Picker_0 = pMyContext.CircleResult.dXPos[0];
                            pMyParam.Y_Picker_0 = pMyContext.CircleResult.dYPos[0];
                        }
                        break;
                    case 1:
                        if (pMyParam.WorldCalibration)
                        {
                            pMyParam.X_Picker_1 = pMyContext.CenterOffsetXmm[ImageGrabIndex];
                            pMyParam.Y_Picker_1 = pMyContext.CenterOffsetYmm[ImageGrabIndex];
                        }
                        else
                        {
                            pMyParam.X_Picker_1 = pMyContext.CircleResult.dXPos[0];
                            pMyParam.Y_Picker_1 = pMyContext.CircleResult.dYPos[0];
                        }
                        break;
                    case 2:
                        if(pMyParam.WorldCalibration)
                        {
                            pMyParam.X_Picker_2 = pMyContext.CenterOffsetXmm[ImageGrabIndex];
                            pMyParam.Y_Picker_2 = pMyContext.CenterOffsetYmm[ImageGrabIndex];
                        }
                        else
                        {
                            pMyParam.X_Picker_2 = pMyContext.CircleResult.dXPos[0];
                            pMyParam.Y_Picker_2 = pMyContext.CircleResult.dYPos[0];
                        }
                        break;
                    case 3:
                        if (pMyParam.WorldCalibration)
                        {
                            pMyParam.X_Picker_3 = pMyContext.CenterOffsetXmm[ImageGrabIndex];
                            pMyParam.Y_Picker_3 = pMyContext.CenterOffsetYmm[ImageGrabIndex];
                        }
                        else
                        {
                            pMyParam.X_Picker_3 = pMyContext.CircleResult.dXPos[0];
                            pMyParam.Y_Picker_3 = pMyContext.CircleResult.dYPos[0];
                        }
                        break;
                    case 4:
                        if (pMyParam.WorldCalibration)
                        {
                            pMyParam.X_Picker_4 = pMyContext.CenterOffsetXmm[ImageGrabIndex];
                            pMyParam.Y_Picker_4 = pMyContext.CenterOffsetYmm[ImageGrabIndex];
                        }
                        else
                        {
                            pMyParam.X_Picker_4 = pMyContext.CircleResult.dXPos[0];
                            pMyParam.Y_Picker_4 = pMyContext.CircleResult.dYPos[0];
                        }
                        break;
                    case 5:
                        if (pMyParam.WorldCalibration)
                        {
                            pMyParam.X_Picker_5 = pMyContext.CenterOffsetXmm[ImageGrabIndex];
                            pMyParam.Y_Picker_5 = pMyContext.CenterOffsetYmm[ImageGrabIndex];
                        }
                        else
                        {
                            pMyParam.X_Picker_5 = pMyContext.CircleResult.dXPos[0];
                            pMyParam.Y_Picker_5 = pMyContext.CircleResult.dYPos[0];
                        }
                        break;
                    case 6:
                        if (pMyParam.WorldCalibration)
                        {
                            pMyParam.X_Picker_6 = pMyContext.CenterOffsetXmm[ImageGrabIndex];
                            pMyParam.Y_Picker_6 = pMyContext.CenterOffsetYmm[ImageGrabIndex];
                        }
                        else
                        {
                            pMyParam.X_Picker_6 = pMyContext.CircleResult.dXPos[0];
                            pMyParam.Y_Picker_6 = pMyContext.CircleResult.dYPos[0];
                        }
                        break;
                    case 7:
                        if (pMyParam.WorldCalibration)
                        {
                            pMyParam.X_Picker_7 = pMyContext.CenterOffsetXmm[ImageGrabIndex];
                            pMyParam.Y_Picker_7 = pMyContext.CenterOffsetYmm[ImageGrabIndex];
                        }
                        else
                        {
                            pMyParam.X_Picker_7 = pMyContext.CircleResult.dXPos[0];
                            pMyParam.Y_Picker_7 = pMyContext.CircleResult.dYPos[0];
                        }
                        break;
                    case 8:
                        if (pMyParam.WorldCalibration)
                        {
                            pMyParam.X_Picker_8 = pMyContext.CenterOffsetXmm[ImageGrabIndex];
                            pMyParam.Y_Picker_8 = pMyContext.CenterOffsetYmm[ImageGrabIndex];
                        }
                        else
                        {
                            pMyParam.X_Picker_8 = pMyContext.CircleResult.dXPos[0];
                            pMyParam.Y_Picker_8 = pMyContext.CircleResult.dYPos[0];
                        }
                        break;
                    case 9:
                        if (pMyParam.WorldCalibration)
                        {
                            pMyParam.X_Picker_9 = pMyContext.CenterOffsetXmm[ImageGrabIndex];
                            pMyParam.Y_Picker_9 = pMyContext.CenterOffsetYmm[ImageGrabIndex];
                        }
                        else
                        {
                            pMyParam.X_Picker_9 = pMyContext.CircleResult.dXPos[0];
                            pMyParam.Y_Picker_9 = pMyContext.CircleResult.dYPos[0];
                        }
                        break;
                }

                return true;
            }
            #endregion CircleFind End
        }
    }
    #endregion CalImageProcessor




    public class BottomCalibrationAction : ActionBase {
        private readonly int AlgIndex;
        private readonly int ModelIndex;

        private BottomCalibrationContext pMyContext;
        private BottomCalibrationParam pMyParam;

        private VirtualCamera pCamera;
        private Mat GrayImage = null;

        private int result = -1;

        private string SeqName = null;

        // 03.11 insert Start
        private const int COUNT_IMAGE_GRAB = 10;

        //HW multi trigger
        private int ImageGrabIndex = 0;
        private Mat[] ImageBuffer = new Mat[COUNT_IMAGE_GRAB];
        public Mat GrabImage = new Mat();

        // Grab 된 이미지 Thread로 처리
        private CalImageProcessor[] imageProcessor = new CalImageProcessor[10];
        private CountdownEvent countdownEvent;
        List<bool> processingResults = new List<bool>();
        // 03.11 insert End

        public enum EStep {
            // 03.11 Insert Start
            Init = 0,
            TriggerReady = 1,
            CalibrationGrabImage = 2,
            Grab = 3,
            FindCircle = 4,
            GrabImage = 5,  // New Insert 05.03 (FlyingCal Debug Mode 사용시 사용)
            End = 6,    // 5
        }

        public BottomCalibrationAction(EAction id, string name, int algIndex, int modelIndex) : base(id, name) {
            
            AlgIndex = algIndex;
            ModelIndex = modelIndex;
            
            Context = new BottomCalibrationContext(this);
            pMyContext = Context as BottomCalibrationContext;

            Param = new BottomCalibrationParam(this, algIndex, ModelIndex);
            pMyParam = Param as BottomCalibrationParam;

            ImageGrabIndex = 0;
            for(int i=0; i<10; i++)
            {
                imageProcessor[i] = new CalImageProcessor();
            }
            processingResults.Clear();
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
                if (!pCamera.SetSoftwareTriggerMode()) {
                    CustomMessageBox.Show(pCamera.Name + " Camera Software trigger mode Set Fail!", "Check camera settings. or camera state.", MessageBoxImage.Error);
                }
            }
            else {
                CustomMessageBox.Show(pMyParam.DeviceName + " Camera Not Open!", "Camera is not open. Please check your connection status.", MessageBoxImage.Error);
                return;
            }

            pMyParam.Grab_Count = COUNT_IMAGE_GRAB;
            
            base.OnLoad();
        }

        public override void OnBegin(SequenceContext prevResult = null) {

            ImageGrabIndex = 0;
            for(int i = 0; i < COUNT_IMAGE_GRAB; i++)
            {
                if((ImageBuffer[i] != null) && (ImageBuffer[i].IsDisposed == false))
                {
                    ImageBuffer[i].Dispose();
                    ImageBuffer[i] = null;
                }
            }

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

            string ActName = Param.Parent.Name; //02.16 insert
            if (ActName == "FRONT_BOTTOM")
                pMyParam.ProcessName = "Front";
            else if (ActName == "REAR_BOTTOM")
                pMyParam.ProcessName = "Rear";
            else
                pMyParam.ProcessName = "None";

            base.OnBegin(prevResult);
        }

        public override void OnEnd() {
            base.OnEnd();
        }
        
        public override ActionContext Run() {
            switch ((EStep)Step) {

                case EStep.Init:

                    pMyContext.strID = ID.ToString();           // FrontBottom_Calibration or RearBottom_Calibration
                    pMyContext.strActionBaseName = this.Name;   // Calibration

                    if (pMyParam.DebugCheck)
                    {
                        #region FlyingCal Insert Code Start
                        // 삭제: Debug 모드로 한장씩 calibration 진행 시 같은 이미지의 같은 값으로 되기 때문에 삭제.
                        //this.countdownEvent = new CountdownEvent(COUNT_IMAGE_GRAB); // 10장의 이미지를 기다립니다.   
                        #endregion FlyingCal Insert Code End

                        Step = (int)EStep.Grab;
                        break;
                    }
                    if (pCamera == null)
                    {
                        pCamera = SystemHandler.Handle.Devices[pMyParam.DeviceName];
                        if (pCamera != null)
                        {
                            if (!pCamera.Properties.ApplyFromParam(pMyParam))
                            {
                                Logging.PrintLog((int)ELogType.Error, "{0} Camera Set Property Fail!", pCamera.Name);
                            }
                            if (!pCamera.SetTriggerMode(ETriggerSource.Hardware_Line0))
                            {
                                Logging.PrintLog((int)ELogType.Error, "{0} Camera Set Hardware Trigger mode Fail!", pCamera.Name);
                            }
                        }
                        else
                        {
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
                            Logging.PrintLog((int)ELogType.Error, $"{ImageGrabIndex}-Image Grab Index Over");
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
                    catch (Exception ex)
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

                        Thread processingThread = new Thread((index) =>
                        {
                            // 이미지 인덱스를 스레드 내부에서 사용할 수 있음
                            int localIndex = (int)index;
                            pMyContext.strParamBaseName = Param.Parent.Name;
                            pMyContext.strActionBaseName = this.Name;
                            pMyContext.strID = ID.ToString();
                            bool result = imageProcessor[localIndex].CalibrationImage(localIndex, ImageBuffer[localIndex], pMyContext, pMyParam);
                            processingResults.Add(result);
                            // 처리가 완료되면 CountdownEvent 신호를 줄입니다.
                            countdownEvent.Signal();
                        });
                        processingThread.Start(ImageGrabIndex);

                        ImageGrabIndex++;

                        if (ImageGrabIndex > pMyParam.Grab_Count - 1)
                        {
                            Step++;
                            break;
                        }
                    }
                    break;

                case EStep.CalibrationGrabImage:
                    // 모든 스레드가 종료될 때까지 최대 5초까지 대기합니다.
                    if (countdownEvent.Wait(TimeSpan.FromSeconds(5)))
                    {
                        // 모든 스레드가 작업을 마치면 다음 단계로 진행합니다.
                        for (int i = 0; i < ImageGrabIndex; i++)  // Origin 주석 처리 01.09
                        {
                            if (processingResults[i] == false)
                            {
                                Logging.PrintLog((int)ELogType.Error, "{0} Bottom Calibration occured error during the inspection!", pMyParam.DeviceName);
                                FinishAction(EContextResult.Error);
                                break;
                            }
                        }
                    }
                    else
                    {
                        // 타임아웃이 발생한 경우 처리할 코드를 작성합니다.
                        Logging.PrintLog((int)ELogType.Error, "{0} Bottom Calibration did not finish during the specified time period!", pMyParam.DeviceName);
                        FinishAction(EContextResult.Error);
                        break;
                    }
                    Step = (int)EStep.End;
                    break;

                case EStep.Grab:

                    //SeqName = SystemHandler.Handle.Sequences[ParentID].ToString();   // 현재 실행하고 있는 Sequence name 가져오기

                    if (pCamera == null) {
                        pCamera = SystemHandler.Handle.Devices[pMyParam.DeviceName];
                        if (pCamera != null) {
                            if (!pCamera.Properties.ApplyFromParam(pMyParam)) {
                                Logging.PrintLog((int)ELogType.Error, "{0} Camera Set Property Fail!", pCamera.Name);
                            }
                            if (!pCamera.SetSoftwareTriggerMode()) {
                                Logging.PrintLog((int)ELogType.Error, "{0} Camera Set Trigger mode Fail!", pCamera.Name);
                            }
                        }
                        else {
                            Logging.PrintLog((int)ELogType.Error, "{0} Camera Handle is null!", pMyParam.DeviceName);
                            FinishAction(EContextResult.Error);
                            break;
                        }
                    }
                    Context.ResultImage = pCamera.GrabImage();
                    if (Context.ResultImage == null) {
                        Logging.PrintLog((int)ELogType.Error, "{0} Camera Image Grab Failed!", pMyParam.DeviceName);
                        FinishAction(EContextResult.Error);
                        break;
                    }
                    if (GrayImage == null) {
                        GrayImage = new Mat(Context.ResultImage.Size(), MatType.CV_8UC1);
                    }
                    else if ((GrayImage.Width != Context.ResultImage.Width) || (GrayImage.Height != Context.ResultImage.Height)) {
                        GrayImage.Dispose();
                        GrayImage = new Mat(Context.ResultImage.Size(), MatType.CV_8UC1);
                    }
                    Cv2.CvtColor(Context.ResultImage, GrayImage, ColorConversionCodes.BGR2GRAY);

                    if (ALLIGATOR_ALG_MIL.agtAM_PutImage(AlgIndex, GrayImage.Data) != 0) {
                        Logging.PrintLog((int)ELogType.Error, "Failed {0} in {1} ReturnCode:{2}", "agtAM_PutImage", ID.ToString(), result);
                        FinishAction(EContextResult.Error);
                        break;
                    }
                    //Step++;   // 05.03 Test code 진행 시 주석 처리 필요.
                    Step = (int)EStep.FindCircle;   // 05.03 Insert


                    #region FlyingCal Insert Code Start
                    /*
                    // 05.03 Insert Test code Start
                    if (ImageGrabIndex > (pMyParam.Grab_Count - 1))
                    {
                        Step++;
                    }
                    else
                    {
                        ImageBuffer[ImageGrabIndex] = GrayImage.Clone();

                        if (ImageGrabIndex > (pMyParam.Grab_Count - 1))
                        {
                            Logging.PrintLog((int)ELogType.Error, "{0} Bottom inspection occured error during the inspection(Image Grab Count over the setting)!", pMyParam.DeviceName);
                            FinishAction(EContextResult.Error);
                            break;
                        }

                        Thread processingThread = new Thread((index) =>
                        {
                            int localIndex = (int)index;
                            pMyContext.strParamBaseName = Param.Parent.Name;
                            pMyContext.strActionBaseName = this.Name;
                            pMyContext.strID = ID.ToString();
                            bool result = imageProcessor[localIndex].CalibrationImage(localIndex, ImageBuffer[localIndex], pMyContext, pMyParam);
                            countdownEvent.Signal();
                        });
                        processingThread.Start(ImageGrabIndex);

                        ImageGrabIndex++;
                    }
                    // 05.03 Insert Test code End
                    */
                    #endregion FlyingCal Insert End

                    break;

                #region FlyingCal Insert Sequence Start
                // 05.03 Insert Test Sequence start
                /*
                case EStep.GrabImage:
                    // 모든 스레드가 종료될 때까지 최대 5초까지 대기.
                    if (countdownEvent.Wait(TimeSpan.FromSeconds(5)))
                    {
                        Logging.PrintLog((int)ELogType.Trace, "{0} Bottom Calibration finish during the specified time period!", pMyParam.DeviceName);
                    }
                    else
                    {
                        // 타임아웃이 발생한 경우 처리할 코드를 작성.
                        Logging.PrintLog((int)ELogType.Error, "{0} Bottom Calibration did not finish during the specified time period!", pMyParam.DeviceName);
                        FinishAction(EContextResult.Error);
                        break;
                    }
                    Step = (int)EStep.End;
                    break;
                */
                // 05.03 Insert Test Sequence End
                #endregion FlyingCal Insert Sequence End

                case EStep.FindCircle:
                    // Screen ROI Coordination
                    System.Windows.Point Top_Left = pMyParam.ROI.TopLeft;
                    System.Windows.Point Bottom_Right = pMyParam.ROI.BottomRight;

                    int TopX = (int)Top_Left.X;
                    int TopY = (int)Top_Left.Y;
                    int BotX = (int)Bottom_Right.X;
                    int BotY = (int)Bottom_Right.Y;

                    int cnt = 0;

                    #region ModelFinder Start
                    //ALLIGATOR_ALG_MIL.sCModelFindResult ModelResult = new ALLIGATOR_ALG_MIL.sCModelFindResult();
                    //ModelResult.Init();
                    //cnt = agtAM_FindModel_ROI_CS(AlgIndexForThread, 0, TopX, BotX, TopY, BotY, out ModelResult);
                    //cnt = ALLIGATOR_ALG_MIL.agtAM_FindModel(AlgIndex, 0, out ModelResult);
                    //if (cnt != 0)
                    //    Debug.WriteLine($"{cnt} is Not Found Model");
                    //else
                    //{
                    //    Debug.WriteLine($"--{ImageGrabIndex}, X:{ModelResult.dXPos}, Y:{ModelResult.dYPos}, Score:{ModelResult.dScore}");
                    //}
                    #endregion ModelFinder end

                    // 여러개의 Circle을 찾았을 때, pMyContext.CircleResult에는 Score 값이 제일 높고, 반지름 값이 제일 큰 순서가 제일 먼저 저장이 된다.
                    cnt = agtAM_FindCircle_ROI_CS(AlgIndex, pMyParam.CircleRadius, TopX, BotX, TopY, BotY, out pMyContext.CircleResult);
                    Debug.WriteLine($"FindCircle Cnt:{cnt}");

                    if (cnt != 0)
                    {
                        pMyContext.bFoundCircle[(int)pMyParam.CalbrationPicker] = false;
                        pMyContext.CalibrationResult = EVisionResultType.NG;
                        FinishAction(EContextResult.Fail);
                    }
                    else
                    {
                        pMyContext.bFoundCircle[(int)pMyParam.CalbrationPicker] = true;
                        pMyContext.CalibrationResult = EVisionResultType.OK;
                    }

                    //Step++;
                    Step = (int)EStep.End;
                    break;
                    
                case EStep.End:

                    if (pMyParam.DebugCheck)    // 개별 이미지를 이용해 Calibration 진행시 사용.
                    {
                        pMyContext.PickerNum = (int)pMyParam.CalbrationPicker;  // Debug 상태에서는 CalibrationPicker Radio box 번호를 Picker Number 로 사용.

                        Debug.WriteLine($"Circle Center-X :{pMyContext.CircleResult.dXPos[0]}, Circle Center-Y :{pMyContext.CircleResult.dYPos[0]}");
                        Debug.WriteLine($"Circle Radius :{pMyContext.CircleResult.dRadius[0]}, Circle Score :{pMyContext.CircleResult.dRadius[0]}");
                        Debug.WriteLine($"Circle Cnt :{pMyContext.CircleResult.nCnt}, Circle Score :{pMyContext.CircleResult.dScore[0]}");

                        SeqName = SystemHandler.Handle.Sequences[ParentID].ToString();   // 현재 실행하고 있는 Sequence name 가져오기

                        pMyContext.CircleCenter_X[(int)pMyParam.CalbrationPicker] = pMyContext.CircleResult.dXPos[0];
                        pMyContext.CircleCenter_Y[(int)pMyParam.CalbrationPicker] = pMyContext.CircleResult.dYPos[0];
                        pMyContext.Radius[(int)pMyParam.CalbrationPicker] = pMyContext.CircleResult.dRadius[0];

                        // 01.08 insert start
                        pMyParam.BaseX = pMyContext.CircleCenter_X[(int)pMyParam.CalbrationPicker];
                        pMyParam.BaseY = pMyContext.CircleCenter_Y[(int)pMyParam.CalbrationPicker];
                        // 01.08 insert End

                        // 04.27 Move Code start
                        //double dResolution = pMyParam.PixelToMM_Offset;
                        double dResolution = pMyParam.PixelToUM_Offset; // 02.14

                        Debug.WriteLine($"Width:{pCamera.Properties.Width}, Height:{pCamera.Properties.Height}");

                        double dImgCenterX = (double)(pCamera.Properties.Width) / 2.0;          // Camera Property Width / 2
                        double dImgCenterY = (double)(pCamera.Properties.Height) / 2.0;         // Camera Property Height / 2


                        if (dImgCenterX > pMyContext.CircleCenter_X[(int)pMyParam.CalbrationPicker])        // Circle X-Center 가 화면 좌표의 왼쪽에 있다면, 이동해야 하는 방향은 + 방향(오른쪽)
                            pMyContext.dDistX[(int)pMyParam.CalbrationPicker] = pMyContext.CenterOffsetXmm[(int)pMyParam.CalbrationPicker] = (dImgCenterX - pMyContext.CircleCenter_X[(int)pMyParam.CalbrationPicker]) * dResolution / 1000;    // Circle X-Center 와 화면 중심과의 거리에 + 방향으로 이동해야 화면 중심에 도착.
                        else                                                // Circle X-Center 가 화면 좌표의오른쪽에 있다면, 이동해야 하는 방향은 - 방향 (왼쪽)
                                                                            //pMyContext.dDistX = pMyContext.CenterOffsetXmm = (pMyContext.CircleCenter_X - dImgCenterX) * dResolution / 1000;
                            pMyContext.dDistX[(int)pMyParam.CalbrationPicker] = pMyContext.CenterOffsetXmm[(int)pMyParam.CalbrationPicker] = (dImgCenterX - pMyContext.CircleCenter_X[(int)pMyParam.CalbrationPicker]) * dResolution / 1000;

                        if (dImgCenterY > pMyContext.CircleCenter_Y[(int)pMyParam.CalbrationPicker])        // Circle Y-Center 가 화면 좌표의 위쪽에 있다면, 이동해야 하는 방향은 + 방향 (아래쪽)
                            pMyContext.dDistY[(int)pMyParam.CalbrationPicker] = pMyContext.CenterOffsetYmm[(int)pMyParam.CalbrationPicker] = (dImgCenterY - pMyContext.CircleCenter_Y[(int)pMyParam.CalbrationPicker]) * dResolution / 1000;
                        else                                                // Circle Y-Center 가 화면 좌표의 아래쪽에 있다면, 이동해야 하는 방향은 - 방향 (위쪽)
                            pMyContext.dDistY[(int)pMyParam.CalbrationPicker] = pMyContext.CenterOffsetYmm[(int)pMyParam.CalbrationPicker] = -(pMyContext.CircleCenter_Y[(int)pMyParam.CalbrationPicker] - dImgCenterY) * dResolution / 1000;

                        pMyContext.dRadmm[(int)pMyParam.CalbrationPicker] = pMyContext.Radius[(int)pMyParam.CalbrationPicker] * dResolution / 1000;    // Radius per mm

                        pMyParam.Circle_X = pMyContext.dDistX[(int)pMyParam.CalbrationPicker];
                        pMyParam.Circle_Y = pMyContext.dDistY[(int)pMyParam.CalbrationPicker];
                        pMyParam.Radius = pMyContext.dRadmm[(int)pMyParam.CalbrationPicker];

                        Debug.WriteLine($"dResolution:{dResolution}");
                        Debug.WriteLine($"dDistX:{pMyContext.CenterOffsetXmm[(int)pMyParam.CalbrationPicker]}");
                        Debug.WriteLine($"dDistY:{pMyContext.CenterOffsetYmm[(int)pMyParam.CalbrationPicker]}");
                        // 04.27 Move Code End

                        // 02.28 Insert Start
                        switch (pMyParam.CalbrationPicker)
                        {
                            case BottomCalibrationParam.PickerNum.Picker_0:
                                pMyParam.X_Picker[0] = pMyContext.X_Picker[0] = pMyParam.X_Picker_0 = pMyParam.BaseX = pMyContext.CircleCenter_X[(int)pMyParam.CalbrationPicker] = pMyContext.CircleResult.dXPos[0];
                                pMyParam.Y_Picker[0] = pMyContext.Y_Picker[0] = pMyParam.Y_Picker_0 = pMyParam.BaseY = pMyContext.CircleCenter_Y[(int)pMyParam.CalbrationPicker] = pMyContext.CircleResult.dYPos[0];

                                if (pMyParam.WorldCalibration)
                                {
                                    pMyParam.X_Picker_0 = pMyContext.CenterOffsetXmm[0];
                                    pMyParam.Y_Picker_0 = pMyContext.CenterOffsetYmm[0];
                                }

                                break;
                            case BottomCalibrationParam.PickerNum.Picker_1:
                                pMyParam.X_Picker[1] = pMyContext.X_Picker[1] = pMyParam.X_Picker_1 = pMyParam.BaseX = pMyContext.CircleCenter_X[(int)pMyParam.CalbrationPicker] = pMyContext.CircleResult.dXPos[0];
                                pMyParam.Y_Picker[1] = pMyContext.Y_Picker[1] = pMyParam.Y_Picker_1 = pMyParam.BaseY = pMyContext.CircleCenter_Y[(int)pMyParam.CalbrationPicker] = pMyContext.CircleResult.dYPos[0];

                                if (pMyParam.WorldCalibration)
                                {
                                    pMyParam.X_Picker_1 = pMyContext.CenterOffsetXmm[1];
                                    pMyParam.Y_Picker_1 = pMyContext.CenterOffsetYmm[1];
                                }

                                break;
                            case BottomCalibrationParam.PickerNum.Picker_2:
                                pMyParam.X_Picker[2] = pMyContext.X_Picker[2] = pMyParam.X_Picker_2 = pMyParam.BaseX = pMyContext.CircleCenter_X[(int)pMyParam.CalbrationPicker] = pMyContext.CircleResult.dXPos[0];
                                pMyParam.Y_Picker[2] = pMyContext.Y_Picker[2] = pMyParam.Y_Picker_2 = pMyParam.BaseY = pMyContext.CircleCenter_Y[(int)pMyParam.CalbrationPicker] = pMyContext.CircleResult.dYPos[0];

                                if (pMyParam.WorldCalibration)
                                {
                                    pMyParam.X_Picker_2 = pMyContext.CenterOffsetXmm[2];
                                    pMyParam.Y_Picker_2 = pMyContext.CenterOffsetYmm[2];
                                }

                                break;
                            case BottomCalibrationParam.PickerNum.Picker_3:
                                pMyParam.X_Picker[3] = pMyContext.X_Picker[3] = pMyParam.X_Picker_3 = pMyParam.BaseX = pMyContext.CircleCenter_X[(int)pMyParam.CalbrationPicker] = pMyContext.CircleResult.dXPos[0];
                                pMyParam.Y_Picker[3] = pMyContext.Y_Picker[3] = pMyParam.Y_Picker_3 = pMyParam.BaseY = pMyContext.CircleCenter_Y[(int)pMyParam.CalbrationPicker] = pMyContext.CircleResult.dYPos[0];

                                if (pMyParam.WorldCalibration)
                                {
                                    pMyParam.X_Picker_3 = pMyContext.CenterOffsetXmm[3];
                                    pMyParam.Y_Picker_3 = pMyContext.CenterOffsetYmm[3];
                                }

                                break;
                            case BottomCalibrationParam.PickerNum.Picker_4:
                                pMyParam.X_Picker[4] = pMyContext.X_Picker[4] = pMyParam.X_Picker_4 = pMyParam.BaseX = pMyContext.CircleCenter_X[(int)pMyParam.CalbrationPicker] = pMyContext.CircleResult.dXPos[0];
                                pMyParam.Y_Picker[4] = pMyContext.Y_Picker[4] = pMyParam.Y_Picker_4 = pMyParam.BaseY = pMyContext.CircleCenter_Y[(int)pMyParam.CalbrationPicker] = pMyContext.CircleResult.dYPos[0];

                                if (pMyParam.WorldCalibration)
                                {
                                    pMyParam.X_Picker_4 = pMyContext.CenterOffsetXmm[4];
                                    pMyParam.Y_Picker_4 = pMyContext.CenterOffsetYmm[4];
                                }

                                break;
                            case BottomCalibrationParam.PickerNum.Picker_5:
                                pMyParam.X_Picker[5] = pMyContext.X_Picker[5] = pMyParam.X_Picker_5 = pMyParam.BaseX = pMyContext.CircleCenter_X[(int)pMyParam.CalbrationPicker] = pMyContext.CircleResult.dXPos[0];
                                pMyParam.Y_Picker[5] = pMyContext.Y_Picker[5] = pMyParam.Y_Picker_5 = pMyParam.BaseY = pMyContext.CircleCenter_Y[(int)pMyParam.CalbrationPicker] = pMyContext.CircleResult.dYPos[0];

                                if (pMyParam.WorldCalibration)
                                {
                                    pMyParam.X_Picker_5 = pMyContext.CenterOffsetXmm[5];
                                    pMyParam.Y_Picker_5 = pMyContext.CenterOffsetYmm[5];
                                }

                                break;
                            case BottomCalibrationParam.PickerNum.Picker_6:
                                pMyParam.X_Picker[6] = pMyContext.X_Picker[6] = pMyParam.X_Picker_6 = pMyParam.BaseX = pMyContext.CircleCenter_X[(int)pMyParam.CalbrationPicker] = pMyContext.CircleResult.dXPos[0];
                                pMyParam.Y_Picker[6] = pMyContext.Y_Picker[6] = pMyParam.Y_Picker_6 = pMyParam.BaseY = pMyContext.CircleCenter_Y[(int)pMyParam.CalbrationPicker] = pMyContext.CircleResult.dYPos[0];

                                if (pMyParam.WorldCalibration)
                                {
                                    pMyParam.X_Picker_6 = pMyContext.CenterOffsetXmm[6];
                                    pMyParam.Y_Picker_6 = pMyContext.CenterOffsetYmm[6];
                                }

                                break;
                            case BottomCalibrationParam.PickerNum.Picker_7:
                                pMyParam.X_Picker[7] = pMyContext.X_Picker[7] = pMyParam.X_Picker_7 = pMyParam.BaseX = pMyContext.CircleCenter_X[(int)pMyParam.CalbrationPicker] = pMyContext.CircleResult.dXPos[0];
                                pMyParam.Y_Picker[7] = pMyContext.Y_Picker[7] = pMyParam.Y_Picker_7 = pMyParam.BaseY = pMyContext.CircleCenter_Y[(int)pMyParam.CalbrationPicker] = pMyContext.CircleResult.dYPos[0];

                                if (pMyParam.WorldCalibration)
                                {
                                    pMyParam.X_Picker_7 = pMyContext.CenterOffsetXmm[7];
                                    pMyParam.Y_Picker_7 = pMyContext.CenterOffsetYmm[7];
                                }

                                break;
                            case BottomCalibrationParam.PickerNum.Picker_8:
                                pMyParam.X_Picker[8] = pMyContext.X_Picker[8] = pMyParam.X_Picker_8 = pMyParam.BaseX = pMyContext.CircleCenter_X[(int)pMyParam.CalbrationPicker] = pMyContext.CircleResult.dXPos[0];
                                pMyParam.Y_Picker[8] = pMyContext.Y_Picker[8] = pMyParam.Y_Picker_8 = pMyParam.BaseY = pMyContext.CircleCenter_Y[(int)pMyParam.CalbrationPicker] = pMyContext.CircleResult.dYPos[0];

                                if (pMyParam.WorldCalibration)
                                {
                                    pMyParam.X_Picker_8 = pMyContext.CenterOffsetXmm[8];
                                    pMyParam.Y_Picker_8 = pMyContext.CenterOffsetYmm[8];
                                }
                                break;
                            case BottomCalibrationParam.PickerNum.Picker_9:
                                pMyParam.X_Picker[9] = pMyContext.X_Picker[9] = pMyParam.X_Picker_9 = pMyParam.BaseX = pMyContext.CircleCenter_X[(int)pMyParam.CalbrationPicker] = pMyContext.CircleResult.dXPos[0];
                                pMyParam.Y_Picker[9] = pMyContext.Y_Picker[9] = pMyParam.Y_Picker_9 = pMyParam.BaseY = pMyContext.CircleCenter_Y[(int)pMyParam.CalbrationPicker] = pMyContext.CircleResult.dYPos[0];

                                if (pMyParam.WorldCalibration)
                                {
                                    pMyParam.X_Picker_9 = pMyContext.CenterOffsetXmm[9];
                                    pMyParam.Y_Picker_9 = pMyContext.CenterOffsetYmm[9];
                                }

                                break;
                        }
                        // 02.28 Insert End

                        // 05.03 Insert Code Start
                        string BotCalFile = Path.Combine(SystemHandler.Handle.Setting.CalibrationSavePath, $"{pMyContext.strID}_{10 - (int)pMyParam.CalbrationPicker}.txt");   // 05.03 Insert 
                        string line = null;
                        using (StreamWriter writer = new StreamWriter(BotCalFile))  // 05.03 Insert
                        {
                            line = String.Format($"X{10 - (int)pMyParam.CalbrationPicker}:{pMyContext.CenterOffsetXmm[(int)pMyParam.CalbrationPicker]:0.000}, " +
                                $"Y{10 - (int)pMyParam.CalbrationPicker}:{pMyContext.CenterOffsetYmm[(int)pMyParam.CalbrationPicker]:0.000}" +
                                $"\t (PIXEL) X{(int)pMyParam.CalbrationPicker}:{pMyParam.X_Picker[(int)pMyParam.CalbrationPicker]:0.000}, " +
                                $"Y{(int)pMyParam.CalbrationPicker}:{pMyParam.Y_Picker[(int)pMyParam.CalbrationPicker]:0.000}");   // 05.03 Insert 제어 Ordering
                            writer.WriteLine(line); // 05.03 Insert
                        }

                        System.Diagnostics.Process.Start("Notepad.exe", BotCalFile);  // 05.03 Insert Notepad 를 이용하여 Calibration 파일 열기.
                        // 05.03 Insert Code end

                    }
                    else
                    {
                        int ErrCount = 0;

                        //string BotCalFile = Path.Combine(@"D:\Data\Calibration\", "BottomCalMM.txt");   // 05.03 Insert 
                        string BotCalFile = Path.Combine(SystemHandler.Handle.Setting.CalibrationSavePath, $"{pMyContext.strID}.txt");   // 05.03 Insert 
                        using (StreamWriter writer = new StreamWriter(BotCalFile))  // 05.03 Insert
                        {
                            for (int i = 0; i < ImageGrabIndex; i++)
                            {
                                string line = null;

                                if (pMyContext.CalibrationResultArray[i] == EVisionResultType.NG)
                                {
                                    ErrCount++;
                                    Logging.PrintErrLog((int)ELogType.Error, string.Format("BottomCalibration Error, PickerNum:{0})", i));
                                }

                                Debug.WriteLine($"Index:{i} X:{pMyContext.dDistX[i]}, Y:{pMyContext.dDistY[i]}");

                                //line = String.Format($"X{i}:{pMyContext.CenterOffsetXmm[i]:0.000}, Y{i}:{pMyContext.CenterOffsetYmm[i]:0.000}");   // 05.03 Insert   Vision Ordering
                                line = String.Format($"X{10 -i}:{pMyContext.CenterOffsetXmm[i]:0.000}, Y{10 -i}:{pMyContext.CenterOffsetYmm[i]:0.000}\t" +  // 05.03 Insert 제어 Ordering
                                    $" (PIXEL) X{i}:{pMyParam.X_Picker[i]:0.000}, Y{i}:{pMyParam.Y_Picker[i]:0.000}");   // 05.03 Insert Vision Ordering
                                writer.WriteLine(line); // 05.03 Insert
                            }
                        }

                        if (ErrCount == 0)      // NG가 없다면, Pass
                        {
                            System.Diagnostics.Process.Start("Notepad.exe", BotCalFile);  // 05.03 Insert Notepad 를 이용하여 Calibration 파일 열기.

                            FinishAction(EContextResult.Pass);
                            break;
                        }
                        else                    // NG가 하나라도 있다면, Fail
                        {
                            FinishAction(EContextResult.Fail);
                            break;
                        }
                    }

                    #region FlyingCal Test Code Start
                    // 해당 영역을 사용하기 위해서는 위 EStep.End 시작 부분에서 바로 위 부분까지 주석 처리 필요.
                    // FlyingCal 관련 region 부분 모두 실행 가능하게 해야 함.
                    /*
                    int ErrCount = 0;

                    //string BotCalFile = Path.Combine(@"D:\Data\Calibration\", "BottomCalMM.txt");   // 05.03 Insert 
                    string BotCalFile = Path.Combine(SystemHandler.Handle.Setting.CalibrationSavePath, $"{pMyContext.strID}.txt");   // 05.03 Insert 
                    using (StreamWriter writer = new StreamWriter(BotCalFile))  // 05.03 Insert
                    {
                        for (int i = 0; i < ImageGrabIndex; i++)
                        {
                            string line = null;

                            if (pMyContext.CalibrationResultArray[i] == EVisionResultType.NG)
                            {
                                ErrCount++;
                                Logging.PrintErrLog((int)ELogType.Error, string.Format("BottomCalibration Error, PickerNum:{0})", i));
                            }

                            Debug.WriteLine($"Index:{i} X:{pMyContext.dDistX[i]}, Y:{pMyContext.dDistY[i]}");

                            //line = String.Format($"X{i}:{pMyContext.CenterOffsetXmm[i]:0.000}, Y{i}:{pMyContext.CenterOffsetYmm[i]:0.000}");   // 05.03 Insert   Vision Ordering
                            line = String.Format($"X{10 - i}:{pMyContext.CenterOffsetXmm[i]:0.000}, Y{10 - i}:{pMyContext.CenterOffsetYmm[i]:0.000}");   // 05.03 Insert 제어 Ordering
                            writer.WriteLine(line); // 05.03 Insert
                        }
                    }

                    if (ErrCount == 0)      // NG가 없다면, Pass
                    {
                        System.Diagnostics.Process.Start("Notepad.exe", BotCalFile);  // 05.03 Insert Notepad 를 이용하여 Calibration 파일 열기.

                        FinishAction(EContextResult.Pass);
                        break;
                    }
                    else                    // NG가 하나라도 있다면, Fail
                    {
                        FinishAction(EContextResult.Fail);
                        break;
                    }
                    */
                    #endregion FlyingCal Test Code End


                    FinishAction(EContextResult.Pass);
                    break;
            }
            return base.Run();
        }

    }
}
