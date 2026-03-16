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

namespace ReringProject.Sequence {

    public class BottomCalibrationContext : ActionContext {

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

        // 02.29 insert Start
        // Each Picker Calibration value
        public double[] X_Picker = new double[10];
        public double[] Y_Picker = new double[10];
        // 02.29 insert End

        public sCCircleFindResult CircleResult;

        
        public EVisionResultType CalibrationResult{ get; set; }

        public BottomCalibrationContext(ActionBase source) : base(source) {

        }

        public override void Clear() {

            bFoundCircle = false;
            CircleCenter_X = 0;
            CircleCenter_Y = 0;
            Radius = 0;
            dRadmm = 0;

            dDistX = 0;
            dDistY = 0;

            CenterOffsetXmm = 0;
            CenterOffsetYmm = 0;

            CalibrationResult = EVisionResultType.NG;

            // 02.29 insert Start
            for(int i=0; i < 10; i++)
            {
                X_Picker[i] = 0;
                Y_Picker[i] = 0;
            }
            // 02.29 insert End
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

        // 02.28 Insert Start
        public enum PickerNum { Picker_0 = 0, Picker_1 = 1, Picker_2, Picker_3, Picker_4, Picker_5, Picker_6, Picker_7, Picker_8, Picker_9 };
        [Category("Picker Selection")]
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
    
    public class BottomCalibrationAction : ActionBase {
        private readonly int AlgIndex;
        private readonly int ModelIndex;

        private BottomCalibrationContext pMyContext;
        private BottomCalibrationParam pMyParam;

        private VirtualCamera pCamera;
        private Mat GrayImage = null;

        //private Mat MasterImage = null; //12.22 주석 처리.
        private int result = -1;

        private string SeqName = null;
        
        public enum EStep {
            Grab = 0,
            FindCircle = 1,
            End = 2,
        }

        public BottomCalibrationAction(EAction id, string name, int algIndex, int modelIndex) : base(id, name) {
            
            AlgIndex = algIndex;
            ModelIndex = modelIndex;
            
            Context = new BottomCalibrationContext(this);
            pMyContext = Context as BottomCalibrationContext;

            Param = new BottomCalibrationParam(this, algIndex, ModelIndex);
            pMyParam = Param as BottomCalibrationParam;
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
            
            base.OnLoad();
        }

        public override void OnBegin(SequenceContext prevResult = null) {
            base.OnBegin(prevResult);
        }

        public override void OnEnd() {
            base.OnEnd();
        }
        
        public override ActionContext Run() {
            switch ((EStep)Step) {
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
                    Step++;
                    break;
                case EStep.FindCircle:
                    // Screen ROI Coordination
                    System.Windows.Point Top_Left = pMyParam.ROI.TopLeft;
                    System.Windows.Point Bottom_Right = pMyParam.ROI.BottomRight;

                    int TopX = (int)Top_Left.X;
                    int TopY = (int)Top_Left.Y;
                    int BotX = (int)Bottom_Right.X;
                    int BotY = (int)Bottom_Right.Y;

                    int cnt = 0;

                    // 여러개의 Circle을 찾았을 때, pMyContext.CircleResult에는 Score 값이 제일 높고, 반지름 값이 제일 큰 순서가 제일 먼저 저장이 된다.
                    //cnt = agtAM_FindCircle_ROI_CS(AlgIndex, 150, TopX, BotX, TopY, BotY, out pMyContext.CircleResult);
                    cnt = agtAM_FindCircle_ROI_CS(AlgIndex, 30, TopX, BotX, TopY, BotY, out pMyContext.CircleResult);
                    Debug.WriteLine($"FindCircle Cnt:{cnt}");

                    if (cnt != 0)
                    {
                        pMyContext.bFoundCircle = false;
                        pMyContext.CalibrationResult = EVisionResultType.NG;
                        FinishAction(EContextResult.Fail);
                    }
                    else
                    {
                        pMyContext.bFoundCircle = true;
                        pMyContext.CalibrationResult = EVisionResultType.OK;
                    }

                    Step++;
                    break;
                    
                case EStep.End:
                    Debug.WriteLine($"Circle Center-X :{pMyContext.CircleResult.dXPos[0]}, Circle Center-Y :{pMyContext.CircleResult.dYPos[0]}");
                    Debug.WriteLine($"Circle Radius :{pMyContext.CircleResult.dRadius[0]}, Circle Score :{pMyContext.CircleResult.dRadius[0]}");
                    Debug.WriteLine($"Circle Cnt :{pMyContext.CircleResult.nCnt}, Circle Score :{pMyContext.CircleResult.dScore[0]}");

                    SeqName = SystemHandler.Handle.Sequences[ParentID].ToString();   // 현재 실행하고 있는 Sequence name 가져오기

                    pMyContext.CircleCenter_X = pMyContext.CircleResult.dXPos[0];
                    pMyContext.CircleCenter_Y = pMyContext.CircleResult.dYPos[0];
                    pMyContext.Radius = pMyContext.CircleResult.dRadius[0];

                    // 01.08 insert start
                    pMyParam.BaseX = pMyContext.CircleCenter_X;
                    pMyParam.BaseY = pMyContext.CircleCenter_Y;
                    // 01.08 insert End

                    // 02.28 Insert Start
                    switch (pMyParam.CalbrationPicker)
                    {
                        case BottomCalibrationParam.PickerNum.Picker_0:
                            pMyParam.X_Picker[0] = pMyContext.X_Picker[0] = pMyParam.X_Picker_0 = pMyParam.BaseX = pMyContext.CircleCenter_X = pMyContext.CircleResult.dXPos[0];
                            pMyParam.Y_Picker[0] = pMyContext.Y_Picker[0] = pMyParam.Y_Picker_0 = pMyParam.BaseY = pMyContext.CircleCenter_Y = pMyContext.CircleResult.dYPos[0];
                            break;
                        case BottomCalibrationParam.PickerNum.Picker_1:
                            pMyParam.X_Picker[1] = pMyContext.X_Picker[1] = pMyParam.X_Picker_1 = pMyParam.BaseX = pMyContext.CircleCenter_X = pMyContext.CircleResult.dXPos[0];
                            pMyParam.Y_Picker[1] = pMyContext.Y_Picker[1] = pMyParam.Y_Picker_1 = pMyParam.BaseY = pMyContext.CircleCenter_Y = pMyContext.CircleResult.dYPos[0];
                            break;
                        case BottomCalibrationParam.PickerNum.Picker_2:
                            pMyParam.X_Picker[2] = pMyContext.X_Picker[2] = pMyParam.X_Picker_2 = pMyParam.BaseX = pMyContext.CircleCenter_X = pMyContext.CircleResult.dXPos[0];
                            pMyParam.Y_Picker[2] = pMyContext.Y_Picker[2] = pMyParam.Y_Picker_2 = pMyParam.BaseY = pMyContext.CircleCenter_Y = pMyContext.CircleResult.dYPos[0];
                            break;
                        case BottomCalibrationParam.PickerNum.Picker_3:
                            pMyParam.X_Picker[3] = pMyContext.X_Picker[3] = pMyParam.X_Picker_3 = pMyParam.BaseX = pMyContext.CircleCenter_X = pMyContext.CircleResult.dXPos[0];
                            pMyParam.Y_Picker[3] = pMyContext.Y_Picker[3] = pMyParam.Y_Picker_3 = pMyParam.BaseY = pMyContext.CircleCenter_Y = pMyContext.CircleResult.dYPos[0];
                            break;
                        case BottomCalibrationParam.PickerNum.Picker_4:
                            pMyParam.X_Picker[4] = pMyContext.X_Picker[4] = pMyParam.X_Picker_4 = pMyParam.BaseX = pMyContext.CircleCenter_X = pMyContext.CircleResult.dXPos[0];
                            pMyParam.Y_Picker[4] = pMyContext.Y_Picker[4] = pMyParam.Y_Picker_4 = pMyParam.BaseY = pMyContext.CircleCenter_Y = pMyContext.CircleResult.dYPos[0];
                            break;
                        case BottomCalibrationParam.PickerNum.Picker_5:
                            pMyParam.X_Picker[5] = pMyContext.X_Picker[5] = pMyParam.X_Picker_5 = pMyParam.BaseX = pMyContext.CircleCenter_X = pMyContext.CircleResult.dXPos[0];
                            pMyParam.Y_Picker[5] = pMyContext.Y_Picker[5] = pMyParam.Y_Picker_5 = pMyParam.BaseY = pMyContext.CircleCenter_Y = pMyContext.CircleResult.dYPos[0];
                            break;
                        case BottomCalibrationParam.PickerNum.Picker_6:
                            pMyParam.X_Picker[6] = pMyContext.X_Picker[6] = pMyParam.X_Picker_6 = pMyParam.BaseX = pMyContext.CircleCenter_X = pMyContext.CircleResult.dXPos[0];
                            pMyParam.Y_Picker[6] = pMyContext.Y_Picker[6] = pMyParam.Y_Picker_6 = pMyParam.BaseY = pMyContext.CircleCenter_Y = pMyContext.CircleResult.dYPos[0];
                            break;
                        case BottomCalibrationParam.PickerNum.Picker_7:
                            pMyParam.X_Picker[7] = pMyContext.X_Picker[7] = pMyParam.X_Picker_7 = pMyParam.BaseX = pMyContext.CircleCenter_X = pMyContext.CircleResult.dXPos[0];
                            pMyParam.Y_Picker[7] = pMyContext.Y_Picker[7] = pMyParam.Y_Picker_7 = pMyParam.BaseY = pMyContext.CircleCenter_Y = pMyContext.CircleResult.dYPos[0];
                            break;
                        case BottomCalibrationParam.PickerNum.Picker_8:
                            pMyParam.X_Picker[8] = pMyContext.X_Picker[8] = pMyParam.X_Picker_8 = pMyParam.BaseX = pMyContext.CircleCenter_X = pMyContext.CircleResult.dXPos[0];
                            pMyParam.Y_Picker[8] = pMyContext.Y_Picker[8] = pMyParam.Y_Picker_8 = pMyParam.BaseY = pMyContext.CircleCenter_Y = pMyContext.CircleResult.dYPos[0];
                            break;
                        case BottomCalibrationParam.PickerNum.Picker_9:
                            pMyParam.X_Picker[9] = pMyContext.X_Picker[9] = pMyParam.X_Picker_9 = pMyParam.BaseX = pMyContext.CircleCenter_X = pMyContext.CircleResult.dXPos[0];
                            pMyParam.Y_Picker[9] = pMyContext.Y_Picker[9] = pMyParam.Y_Picker_9 = pMyParam.BaseY = pMyContext.CircleCenter_Y = pMyContext.CircleResult.dYPos[0];
                            break;
                    }
                    // 02.28 Insert End

                    //double dResolution = pMyParam.PixelToMM_Offset;
                    double dResolution = pMyParam.PixelToUM_Offset; // 02.14

                    Debug.WriteLine($"Width:{pCamera.Properties.Width}, Height:{pCamera.Properties.Height}");

                    double dImgCenterX = (double)(pCamera.Properties.Width) / 2.0;          // Camera Property Width / 2
                    double dImgCenterY = (double)(pCamera.Properties.Height) / 2.0;         // Camera Property Height / 2


                    if (dImgCenterX > pMyContext.CircleCenter_X)        // Circle X-Center 가 화면 좌표의 왼쪽에 있다면, 이동해야 하는 방향은 + 방향(오른쪽)
                        pMyContext.dDistX = pMyContext.CenterOffsetXmm = (dImgCenterX - pMyContext.CircleCenter_X) * dResolution / 1000;    // Circle X-Center 와 화면 중심과의 거리에 + 방향으로 이동해야 화면 중심에 도착.
                    else                                                // Circle X-Center 가 화면 좌표의오른쪽에 있다면, 이동해야 하는 방향은 - 방향 (왼쪽)
                        //pMyContext.dDistX = pMyContext.CenterOffsetXmm = (pMyContext.CircleCenter_X - dImgCenterX) * dResolution / 1000;
                        pMyContext.dDistX = pMyContext.CenterOffsetXmm = (dImgCenterX - pMyContext.CircleCenter_X) * dResolution / 1000;

                    if (dImgCenterY > pMyContext.CircleCenter_Y)        // Circle Y-Center 가 화면 좌표의 위쪽에 있다면, 이동해야 하는 방향은 + 방향 (아래쪽)
                        pMyContext.dDistY = pMyContext.CenterOffsetYmm = (dImgCenterY - pMyContext.CircleCenter_Y) * dResolution / 1000;
                    else                                                // Circle Y-Center 가 화면 좌표의 아래쪽에 있다면, 이동해야 하는 방향은 - 방향 (위쪽)
                        pMyContext.dDistY = pMyContext.CenterOffsetYmm = -(pMyContext.CircleCenter_Y - dImgCenterY) * dResolution / 1000;

                    pMyContext.dRadmm = pMyContext.Radius * dResolution / 1000;    // Radius per mm

                    pMyParam.Circle_X = pMyContext.dDistX;
                    pMyParam.Circle_Y = pMyContext.dDistY;
                    pMyParam.Radius = pMyContext.dRadmm;

                    Debug.WriteLine($"dResolution:{dResolution}");
                    Debug.WriteLine($"dDistX:{pMyContext.CenterOffsetXmm}");
                    Debug.WriteLine($"dDistY:{pMyContext.CenterOffsetYmm}");

                    

                    FinishAction(EContextResult.Pass);
                    break;
            }
            return base.Run();
        }

    }
}
