using System;
using ReringProject.Define;
using ReringProject.UI;
using ReringProject.Utility;
using PropertyTools.DataAnnotations;
using ReringProject.Device;
using AlligatorAlgMil;
using System.IO;
using OpenCvSharp;
using HalconDotNet;
using ReringProject.Halcon;
using System.Windows;
using ReringProject.Network;
using ReringProject.Setting;
using System.Diagnostics;
using System.Threading;

namespace ReringProject.Sequence {

    public class WaferScanCalibrationActionContext : ActionContext {
        public double dAngle { get; set; }          // Die ?åÏ†Ñ Í∞ÅÎèÑ (?úÏñ¥Î∂Ä???òÍ≤®??WaferÍ∞Ä Î≥¥Ï†ï?¥Ïïº ?òÎäî Í∞ÅÎèÑ)
        public bool bCalibrated { get; set; }       // Found ModelFinder model.
        public int CalZoneNum { get; set; }         // Calibration Zone Number. 05.29 Insert
        

        public double MMPerPixel_LX { get; set; }    // Calibration ÏßÑÌñâ ??X Ï∂?1Pixel ??mm
        public double MMPerPixel_LY { get; set; }    // Calibration ÏßÑÌñâ ??Y Ï∂?1Pixel ??mm
        public double MMPerPixel_RX { get; set; }    // Calibration ÏßÑÌñâ ??X Ï∂?1Pixel ??mm
        public double MMPerPixel_RY { get; set; }    // Calibration ÏßÑÌñâ ??Y Ï∂?1Pixel ??mm
        public WaferScanCalibrationActionContext(ActionBase source) : base(source) {

        }

        public override void Clear() {
            dAngle = 0;
            bCalibrated = false;
            CalZoneNum = 0;
            base.Clear();
        }
    }
    
    public class WaferScanCalibrationParam : CameraSlaveParam {
        public readonly int AlgIndex;
        public readonly int ModelIndex;
        public readonly int CalibrationIndex;
        private Mat GrayImage = null;

        [Category("Model Finder")]
        [Content]
        public ModelFinderView Module { get; private set; }

        [ModelFinder, Browsable(false)]
        public ModelFinderViewModel ModuleModel { get; set; } = new ModelFinderViewModel("Module", EAlgorithmType.ModelFinder);

        [Category("Grid Calibration")]
        [DisplayName("Calibration Enable")]
        public bool CalEnable
        {
            get { return _CalEnable; }
            set
            {
                _CalEnable = value;
                RaisePropertyChanged("CalEnable");
            }
        }
        private bool _CalEnable;

        //Die width
        [Spinnable(1, 5, 0, 30)]
        [DisplayName("Grid Column Number")]
        public int GridColumnNumber
        {
            get
            {
                return _GridColumnNumber;
            }
            set
            {
                _GridColumnNumber = value;
                RaisePropertyChanged("GridColumnNumber");
            }
        }
        private int _GridColumnNumber;

        //Die height
        [Spinnable(1, 5, 0, 30)]
        [DisplayName("Grid Row Number")]
        public int GridRowNumber
        {
            get
            {
                return _GridRowNumber;
            }
            set
            {
                _GridRowNumber = value;
                RaisePropertyChanged("GridRowNumber");
            }
        }
        private int _GridRowNumber;

        // 06.12 Insert Checker Size Property Start
        [DisplayName("RECT ROW-Size(mm)")]
        public double RowSpacing
        {
            get
            {
                return _RowSpacing;
            }
            set
            {
                _RowSpacing = value;
                RaisePropertyChanged("RowSpecing");
            }
        }
        private double _RowSpacing;

        [DisplayName("RECT COL-Size(mm)")]
        public double ColumSpacing
        {
            get
            {
                return _ColumSpacing;
            }
            set
            {
                _ColumSpacing = value;
                RaisePropertyChanged("ColumSpacing");
            }
        }
        private double _ColumSpacing;
        // 06.12 Insert Checker Size Property End

        [DisplayName("mmPerPixel-LX")]
        [ReadOnly(true)]
        public double MMPerPixel_LX
        {
            get
            {
                return _MMPerPixel_LX;
            }
            set
            {
                _MMPerPixel_LX = value;
                RaisePropertyChanged("MMPerPixel_LX");
            }
        }
        private double _MMPerPixel_LX;

        [DisplayName("mmPerPixel-LY")]
        [ReadOnly(true)]
        public double MMPerPixel_LY
        {
            get
            {
                return _MMPixel_LY;
            }
            set
            {
                _MMPixel_LY = value;
                RaisePropertyChanged("MMPerPixel_LY");
            }
        }
        private double _MMPixel_LY;

        [DisplayName("mmPerPixel-RX")]
        [ReadOnly(true)]
        public double MMPerPixel_RX
        {
            get
            {
                return _MMPerPixel_RX;
            }
            set
            {
                _MMPerPixel_RX = value;
                RaisePropertyChanged("MMPerPixel_RX");
            }
        }
        private double _MMPerPixel_RX;

        [DisplayName("mmPerPixel-RY")]
        [ReadOnly(true)]
        public double MMPerPixel_RY
        {
            get
            {
                return _MMPixel_RY;
            }
            set
            {
                _MMPixel_RY = value;
                RaisePropertyChanged("MMPerPixel_RY");
            }
        }
        private double _MMPixel_RY;

        // 02.26 Insert Start
        public enum ZoneNum { Left = 1, Right = 2};

        [Category("Calibration Zone Selector")]
        [DisplayName("Manual Enable")]
        public bool Manual 
        {
            get { return _Manual; }
            set
            {
                _Manual = value;
                RaisePropertyChanged("Manual");
            }
        }
        private bool _Manual;
        public ZoneNum ZoneSelector { get; set; }
        // 02.26 Insert End
        
        [Browsable(false)]  // 02.26 Insert 
        public new string SequenceName { get; set; }
        public new string ActionName { get; set; }
        public new string OwnerName { get; set; }

        // 03.07 Insert Start
        [Category("Device|WAFER OUTER Light")]
        [DisplayName("Outer Lightlevel")]
        public int OuterLevel { get; set; }
        // 03.07 Insert End

        public WaferScanCalibrationParam(object owner, int algIndex, int modelIndex) : base(owner) {
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
                minScore /= 100.0f; //Ï£ºÏñ¥Ïß?score??100?®ÏúÑ ?¥Î?Î°?

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


        public bool LoadModuleExternalFile() {
            if (File.Exists(ModuleModel.ModelFile) && (Path.GetExtension(ModuleModel.ModelFile) == DeviceHandler.EXTENSION_MODEL)) {
                if (ALLIGATOR_ALG_MIL.agtAM_LoadModel_CS(AlgIndex, ModelIndex, ModuleModel.ModelFile) != 0) {
                    return false;
                }
            }
            else return false;
            return true;
        }

        //module exist
        void Model_NewButton_Click(object sender, RoutedEventArgs e) {
            //ÏßÄ?ïÎêú ?ÅÏó≠?ºÎ°ú ??Î™®Îç∏ ?ùÏÑ±
            ALLIGATOR_ALG_MIL.agtAM_SetModel_CS(AlgIndex, ModelIndex, (int)ModuleModel.Master.Left, (int)ModuleModel.Master.Right, (int)ModuleModel.Master.Top, (int)ModuleModel.Master.Bottom);
            if (ALLIGATOR_ALG_MIL.agtAM_FindConfigDialog(AlgIndex, ModelIndex) != 0) {
                CustomMessageBox.Show("Fail to New Model", string.Format("{0} - Model Finder, New Model Fail. AlgIndex : {1}, ModelIndex : {2}, File : {3}", this.GetType().Name, AlgIndex, ModelIndex, ModuleModel.ModelFile), MessageBoxImage.Error);
            }
        }

        void Model_EditButton_Click(object sender, RoutedEventArgs e) {
            //Î™®Îç∏ ?åÏùº???àÎã§Î©?Î°úÎìú???? edit Ï∞ΩÏùÑ ?∞Îã§.
            if (File.Exists(ModuleModel.ModelFile)) {
                if (LoadModuleExternalFile() == false) {
                    CustomMessageBox.Show("Fail to Load Model", string.Format("{0} - Model Finder, Load Fail. AlgIndex : {1}, ModelIndex : {2}, File : {3}", this.GetType().Name, AlgIndex, ModelIndex, ModuleModel.ModelFile), MessageBoxImage.Error);
                    return;
                }

                if (ALLIGATOR_ALG_MIL.agtAM_FindConfigDialog(AlgIndex, ModelIndex) != 0) {
                    CustomMessageBox.Show("Fail to Open Model Dialog", string.Format("{0} - Model Finder, Dialog Open Fail. AlgIndex : {1}, ModelIndex : {2}, File : {3}", this.GetType().Name, AlgIndex, ModelIndex, ModuleModel.ModelFile), MessageBoxImage.Error);
                    return;
                }
                return;
            }

            //?ÜÏúºÎ©?ÏßÄ?ïÎêú ?ÅÏó≠?ºÎ°ú ?ùÏÑ±
            Model_NewButton_Click(null, null);
        }

        void Model_LoadButton_Click(object sender, RoutedEventArgs e) {
            var dialog = new Microsoft.Win32.OpenFileDialog();
            //dialog.FileName = "Document"; 
            dialog.DefaultExt = DeviceHandler.EXTENSION_MODEL;
            dialog.Filter = DeviceHandler.FILTER_MODEL;
            dialog.InitialDirectory = Path.GetDirectoryName(ModuleModel.ModelFile);

            // Show open file dialog box
            bool? result = dialog.ShowDialog();
            if (result == true) {
                ModuleModel.ModelFile = dialog.FileName;
                if (LoadModuleExternalFile() == false) {
                    CustomMessageBox.Show("Fail to Load Model", string.Format("{0} - Model Finder, Load Fail. AlgIndex : {1}, ModelIndex : {2}, File : {3}", this.GetType().Name, AlgIndex, ModelIndex, ModuleModel.ModelFile), MessageBoxImage.Error);
                }
            }
        }


        public override bool Load(IniFile loadFile, string group) {
            bool result = base.Load(loadFile, group);
            
            //load pattern
            if(LoadModuleExternalFile() == false) {
                CustomMessageBox.Show("Fail to Load Pattern", string.Format("{0} - Model Finder, Load Fail. AlgIndex : {1}, ModelIndex : {2}, File : {3}", this.GetType().Name, AlgIndex, ModelIndex, ModuleModel.ModelFile), MessageBoxImage.Error);
                result = false;
            }
            
            return result;
        }

        public override bool Save(IniFile saveFile, string group)
        {
            //model file?Ä ??ÉÅ recipe directory ?¥Î????Ä?•Ìïú?? (?∏Î? ?åÏùº ?¨Ïö© ?? ?åÏùº ?†Ïã§Î°??∏Ìïú recipe ?êÏã§??Î∞úÏÉù?????àÏúºÎØÄÎ°?
            ModuleModel.ModelFile = GetExternalFilePath(EExternalFileType.Model, nameof(ModuleModel));
            if (ALLIGATOR_ALG_MIL.agtAM_SaveModel_CS(AlgIndex, ModelIndex, ModuleModel.ModelFile) != 0)
            {
                CustomMessageBox.Show("Fail to Save Model", string.Format("{0} - Model Finder, Save Fail. AlgIndex : {1}, ModelIndex : {2}, File : {3}", this.GetType().Name, AlgIndex, ModelIndex, ModuleModel.ModelFile), MessageBoxImage.Error);
            }
            return base.Save(saveFile, group);
        }

        public override void PutImage(HImage image) {
            if (image == null) return;
            HImage grayImage = null;
            try {
                grayImage = image.CountChannels().I == 1 ? image.CopyImage() : image.Rgb1ToGray();
                string imageType;
                int width;
                int height;
                IntPtr pointer = grayImage.GetImagePointer1(out imageType, out width, out height);
                int result = ALLIGATOR_ALG_MIL.agtAM_PutImage(AlgIndex, pointer);
                if (result != 0) {
                    CustomMessageBox.Show("Fail to PutImage", string.Format("PutImage from Camera : {0}, AlgIndex : {1}", DeviceName, AlgIndex), MessageBoxImage.Error);
                }
            }
            finally {
                if (grayImage != null) {
                    grayImage.Dispose();
                }
            }
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

            if(param is BottomInspectionParam) {
                
                return true;
            }
            return false;
        }

        public override double ConvertPixelToMM(double pixel) {
            return base.ConvertPixelToMM(pixel);
        }
    }
    
    public class WaferScanCalibrationAction : ActionBase {
        private readonly int AlgIndex;
        private readonly int ModelIndex;
        private int CalibrationIndex;

        private WaferScanCalibrationActionContext pMyContext;
        private WaferScanCalibrationParam pMyParam;

        private VirtualCamera pCamera;
        private Mat GrayImage = null;

        private Mat MasterImage = null;
        private int result = -1;
        private int foundIndex = 0;

        public double dDistanceX = 0, dDistanceY = 0;
        public double dCalibrationAngle = 0;

        public enum EStep {
            Grab = 0,
            LineAngle = 1,
            ModelFind = 2,
            Calibration = 3,
            End = 4,
        }

        public WaferScanCalibrationAction(EAction id, string name, int algIndex, int modelIndex) : base(id, name) {
            
            AlgIndex = algIndex;
            ModelIndex = modelIndex;

            Context = new WaferScanCalibrationActionContext(this);
            pMyContext = Context as WaferScanCalibrationActionContext;

            Param = new WaferScanCalibrationParam(this, algIndex, ModelIndex);
            pMyParam = Param as WaferScanCalibrationParam;
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

            // 02.26 Insert start - Application OnLoad (ZoneSelector Initialize)
            if ((pMyParam.ZoneSelector != WaferScanCalibrationParam.ZoneNum.Left) && (pMyParam.ZoneSelector != WaferScanCalibrationParam.ZoneNum.Right))
                pMyParam.ZoneSelector = WaferScanCalibrationParam.ZoneNum.Left;
            // 02.26 Insert End
            
            base.OnLoad();
        }

        public override void OnBegin(SequenceContext prevResult = null) {
            if ((prevResult.TargetCode != null) && (pMyParam.Manual == false))
            {
                string upperstr = null;
                upperstr = prevResult.TargetCode.ToUpper();  // ?ÄÎ¨∏Ïûê Î≥Ä??

                if ((upperstr == "LEFT") || (prevResult.TargetCode == "1"))
                {
                    pMyParam.ZoneSelector = WaferScanCalibrationParam.ZoneNum.Left;
                    pMyContext.CalZoneNum = 1;
                    prevResult.TargetCode = null;
                }
                else if ((upperstr == "RIGHT") || (prevResult.TargetCode == "2"))
                {
                    pMyParam.ZoneSelector = WaferScanCalibrationParam.ZoneNum.Right;
                    pMyContext.CalZoneNum = 2;
                    prevResult.TargetCode = null;
                }
                else
                {
                    Logging.PrintLog((int)ELogType.Error, "Error Wafer Calibration! TargetCode is Not match");
                    pMyContext.CalZoneNum = 0;
                }
            }
            else
            {
                pMyContext.CalZoneNum = 0;
            }

            base.OnBegin(prevResult);
        }

        public override void OnEnd() {
            base.OnEnd();
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
                    Context.ResultHalconImage?.Dispose();
                    Context.ResultHalconImage = pCamera.GrabHalconImage();
                    Context.ResultHalconImage?.Dispose();
                    Context.ResultHalconImage = pCamera.GrabHalconImage();
                    if (Context.ResultHalconImage == null) {
                        FinishAction(EContextResult.Error);
                        break;
                    }
                    pMyParam.PutImage(Context.ResultHalconImage);
                    Step++;
                    break;

                case EStep.LineAngle:

                    // Line??Angle??Ï∞æÎäî??
                    int nAngleCnt = 0;
                    float[] fAngle = new float[10];
                    float[] fAngleScr = new float[10];

                    float fGetAngle = 0;
                    float fGetAngleScr = 0;

                    float calAngle = 0;

                    ALLIGATOR_ALG_MIL.agtAM_OrientSearch(AlgIndex, 10, true);
                    ALLIGATOR_ALG_MIL.agtAM_OrientResult(AlgIndex, out nAngleCnt, fAngle, fAngleScr);


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

                    if (fGetAngle < 45.0) calAngle = -fGetAngle;
                    else if (fGetAngle >= 45.0 && fGetAngle < 135.0) calAngle = 90 - fGetAngle;
                    else if (fGetAngle > 135) calAngle = 180 - fGetAngle;

                    Debug.WriteLine($"AngleCnt:{nAngleCnt}, Angle value:{fGetAngle}, Angle Score:{fGetAngleScr}, Cal Angle:{calAngle}");

                    pMyContext.dAngle = calAngle;

                    // Ï°∞Í±¥???∞Î•∏ ?úÏñ¥Î∂Ä??Error Ï≤òÎ¶¨ Î∞??åÏ†Ñ ?ïÎ†¨ ?îÏ≤≠ Ï≤òÎ¶¨ ?ÑÏöî.

                    string str = string.Format("Angle Score: {0:0.000}, Angle: {1:0.000}", fGetAngleScr, calAngle);
                    //Step++;
                    if (pMyParam.CalEnable)
                    {
                        Logging.PrintLog((int)ELogType.Result, str);
                        Step = (int)EStep.Calibration;
                    }
                    else
                    {
                        Logging.PrintLog((int)ELogType.Result, str);
                        Cv2.PutText(calibrationImage, str, new OpenCvSharp.Point(7008 / 2 + 150, 7000 / 2 + 200), HersheyFonts.HersheyTriplex, 3, Scalar.Red, 2, LineTypes.AntiAlias);
                        Context.ResultHalconImage?.Dispose();
                        Context.ResultHalconImage = HalconImageBridge.FromMat(calibrationImage);
                        Step = (int)EStep.End;
                    }
                    break;

                case EStep.ModelFind:
                    Step++;
                    break;

                case EStep.Calibration:

                    try
                    {
                        double dGridOffsetX = 0.0, dGridOffsetY = 0.0, dGridOffsetZ = 0.0;
                        int nRowNumber = pMyParam.GridRowNumber, nColumnNumber = pMyParam.GridColumnNumber;
                        double dRowSpacing = pMyParam.RowSpacing, dColumnSpacing = pMyParam.ColumSpacing;       // 06.12 Modify
                        int nOperation = 0, nGridType = 2;

                        if (pMyParam.Manual == true)
                            CalibrationIndex = (int)pMyParam.ZoneSelector;    // 02.26 Insert 1: LeftZone, 2: RightZone
                        else
                            CalibrationIndex = pMyContext.CalZoneNum;

                        result = ALLIGATOR_ALG_MIL.agtAM_GridCalibration(AlgIndex, CalibrationIndex,
                            dGridOffsetX, dGridOffsetY, dGridOffsetZ,
                            nRowNumber, nColumnNumber,
                            dRowSpacing, dColumnSpacing,
                            nOperation, nGridType,
                            out dDistanceX, out dDistanceY, out dCalibrationAngle);    // pixel per mm (dDistancex, dDistanceY)
                        if (result != 0)
                        {
                            Logging.PrintLog((int)ELogType.Error, "Failed {0} in {1} ReturnCode:{2}", "agtAM_GridCalibration", ID.ToString(), result);
                            pMyContext.bCalibrated = false;
                            FinishAction(EContextResult.Error);
                            break;
                        }

                        if (CalibrationIndex == 1)
                        {
                            pMyParam.MMPerPixel_LX = pMyContext.MMPerPixel_LX = dDistanceX;     // insert 06.14 jdhan
                            pMyParam.MMPerPixel_LY = pMyContext.MMPerPixel_LY = dDistanceY;     // insert 06.14 jdhan
                        }
                        else
                        {
                            pMyParam.MMPerPixel_RX = pMyContext.MMPerPixel_RX = dDistanceX;     // insert 06.14 jdhan
                            pMyParam.MMPerPixel_RY = pMyContext.MMPerPixel_RY = dDistanceY;     // insert 06.14 jdhan
                        }

                        //Pixel Í±∞Î¶¨ Update here (dDistanceX, dDistanceY)
                        Logging.PrintLog((int)ELogType.Result, "Calibrated {0} in {1} ReturnCode:{2}, X Pixel_MM:{3}, Y Pixel_MM:{4}, Cal Angle:{5}", 
                            "agtAM_GridCalibration", ID.ToString(), result, dDistanceX, dDistanceY, dCalibrationAngle);
                    }
                    catch (Exception e)
                    {
                        Logging.PrintErrLog((int)ELogType.Error, string.Format("Exception while {0} in {1}, ReturnCode:{2}, ({3})", "agtAM_GridCalibration", ID.ToString(), result, e.Message));
                        pMyContext.bCalibrated = false;
                        FinishAction(EContextResult.Error);
                        break;
                    }
                    pMyContext.bCalibrated = true;

                    //Save calibration file
                    string strCalibrationFile = pMyParam.GetExternalFilePath(EExternalFileType.Calibration, "WaferScan"+ (int)pMyParam.ZoneSelector);   // 02.26 Insert
                    if (ALLIGATOR_ALG_MIL.agtAM_SaveGridCalibration(AlgIndex, CalibrationIndex, strCalibrationFile) != 0)
                    {
                        Logging.PrintErrLog((int)ELogType.Error, string.Format("Exception while {0} in {1}, ReturnCode:{2}", "agtAM_SaveGridCalibration", ID.ToString(), result));
                        pMyContext.bCalibrated = false;
                        FinishAction(EContextResult.Error);
                        break;
                    }
                    
                    Step++;
                    break;

                case EStep.End:
                    FinishAction(EContextResult.Pass);
                    break;
            }
            return base.Run();
        }
    }
}
