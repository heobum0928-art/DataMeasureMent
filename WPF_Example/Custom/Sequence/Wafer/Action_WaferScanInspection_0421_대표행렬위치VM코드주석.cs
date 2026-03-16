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
using System.Linq;
using ChartDirector;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using MathNet.Numerics;
using MathNet.Numerics.Statistics;
using System.Diagnostics;
using System.Drawing;
using System.Threading;

namespace ReringProject.Sequence
{

    public class _ST_MAP_INFO
    {
        public int Org_X;
        public int Org_Y;
        public int Bin;
        public int Tgt_X;
        public int Tgt_Y;
        public double Pos_X;
        public double Pos_Y;
        public int Succ;

        /// <summary>
        /// Insert Data Field
        /// </summary>
        public int ContourCount;
        public double Area;
        public bool Convex;
        public int Apex;

        public _ST_MAP_INFO()
        {

        }
        public void Clear()
        {
            Org_X = 0;
            Org_Y = 0;
            Bin = 0;
            Tgt_X = 0;
            Tgt_Y = 0;
            Pos_X = 0;
            Pos_Y = 0;
            Succ = 0;

            ContourCount = 0;
            Area = 0;
            Convex = false;
            Apex = 0;
        }

        public _ST_MAP_INFO Clone()
        {
            _ST_MAP_INFO newItem = new _ST_MAP_INFO();
            newItem.Org_X = Org_X;
            newItem.Org_Y = Org_Y;
            newItem.Bin = Bin;
            newItem.Tgt_X = Tgt_X;
            newItem.Tgt_Y = Tgt_Y;
            newItem.Pos_X = Pos_X;
            newItem.Pos_Y = Pos_Y;
            newItem.Succ = Succ;
            return newItem;
        }
    }
    public class Found_Die_Info
    {
        public double X_Pos { get; set; }
        public double Y_Pos { get; set; }
        public double WX_Pos { get; set; }      // World Coordination X
        public double WY_Pos { get; set; }      // World Coordination Y
        public double Width { get; set; }
        public double Height { get; set; }
        public double Angle { get; set; }

        public int ContourCount { get; set; }
        public double Area { get; set; }
        public bool Convex { get; set; }
        public int Apex { get; set; }

        public Moments moments { get; set; }    // 01.22 insert

        // 01. 20 Insert
        public Mat Die_Image { get; set; }

        public Found_Die_Info()
        {
            X_Pos = 0;
            Y_Pos = 0;

            WX_Pos = 0;
            WY_Pos = 0;

            Width = 0;
            Height = 0;
            Angle = 0;

            ContourCount = 0;
            Area = 0;
            Convex = false;
            Apex = 0;

            moments = null;

            // 01.20 Insert
            Die_Image = null;
        }
    }
    public class WaferScanInspectionActionContext : ActionContext
    {
        /// <summary>
        /// Model find parameters
        /// </summary>
        public bool bFoundModel { get; set; }       // Found ModelFinder model.
        public int dFoundCount { get; set; }        // Found Model Count

        public double[] dFoundModelX = new double[1024];
        public double[] ddFoundModelX = new double[1024];
        public double[] dFoundModelY = new double[1024];
        public double[] ddFoundModelY = new double[1024];

        public double[] dFoundModelXWorld = new double[1024];
        public double[] dFoundModelYWorld = new double[1024];
        public double[] dFoundModelAngle = new double[1024];
        public double[] dFoundModelScore = new double[1024];
        public double[] dFoundModelWidth = new double[1024];
        public double[] dFoundModelHeight = new double[1024];



        public EVisionResultType ModelFinderResult { get; set; }

        /// <summary>
        /// RanSac Circle Found Variable
        /// </summary>
        public bool bFoundCircle { get; set; }      // Found Wafer Circle.
        public double dFoundCenterX { get; set; }
        public double dFoundCenterY { get; set; }
        public double dFoundCenterXWorld { get; set; }
        public double dFoundCenterYWorld { get; set; }
        public double dMovingCenterX { get; set; }
        public double dMovingCenterY { get; set; }
        public double dAngle { get; set; }          // Die 회전 각도 (제어부에 넘겨서 Wafer가 보정해야 하는 각도)
        public double dRadius { get; set; }     // pixel
        public double dRadmm { get; set; }      // mm
        public EVisionResultType CircleFoundResult { get; set; }
        public EVisionResultType AngleFoundResult { get; set; }

        public bool bBoundRatio { get; set; }


        /// <summary>
        /// MapFile Analysis Variable
        /// </summary>
        public int nMaxRow { get; set; }
        public int nMaxCol { get; set; }
        public int nTotalCell { get; set; }
        public double nDie_Width { get; set; }
        public double nDie_Height { get; set; }
        public int Die_Grade { get; set; }          // Die Grade
        public double WaferDegree { get; set; }     // Wafer 투입시 Map file의 회전 각도 입력 parameter 값.
        public int DieTotal { get; set; }

        public Dictionary<int, _ST_MAP_INFO> MapData = new Dictionary<int, _ST_MAP_INFO>();
        // Insert Date: 2023.10.23
        public List<_ST_MAP_INFO> ListMapInfo = new List<_ST_MAP_INFO>();
        public List<double> DiePos = new List<double>();
        public bool bMapList { get; set; }      // Whether Map File Analysis Complete.
        public EVisionResultType MapFileResult { get; set; }

        //Response data
        public double CenterOffsetXmm { get; set; }
        public double CenterOffsetYmm { get; set; }


        //public MapDataOrigin OriginData;                                            // KBH
        // Point.X : Trans-X, Point.Y: Trans-Y, PointF.X: CenterToDieCenter-X, PontF.Y: CenterToDieCenter-Y
        //public Dictionary<System.Drawing.Point, System.Drawing.PointF> OriginPoint = new Dictionary<System.Drawing.Point, PointF>(); // KBH

        // ModelFinder에서 찾은 Die에서 MapFile에 존재하는 Die만 저장.
        public Dictionary<System.Drawing.Point, Found_Die_Info> Found_Die = new Dictionary<System.Drawing.Point, Found_Die_Info>();     // jdhan

        public Mat CrobImage = new Mat();

        /// <summary>
        /// Die Inspection Parameter
        /// </summary>
        public double Die_Width_Ratio { get; set; }
        public double Die_Height_Ratio { get; set; }
        public double Die_Area_Ratio { get; set; }
        public int Binary_Threshold { get; set; }

        public string ProcessName { get; set; }     // Process Name

        /// <summary>
        /// WaferScanCalibration에서 Camera Calibration 수행 하여 얻은 데이터를 저장할 변수
        /// MMPerPixel_X
        /// MMPerPixel_Y
        /// </summary>
        public double MMPerPixel_X { get; set; }
        public double MMPerPixel_Y { get; set; }

        public string MapFileName { get; set; } // 03.20 Insert


        /// <summary>
        /// ChartDirector variable
        /// </summary>
        public XYChart WaferMap = null;
        public PlotArea plot = null;
        public DiscreteHeatMapLayer layer = null;
        public ColorAxis cAxis = null;

        public WaferScanInspectionActionContext(ActionBase source) : base(source)
        {

        }

        public override void Clear()
        {
            bFoundModel = false;
            dFoundCount = 0;
            for (int i = 0; i < 1024; i++)
            {
                dFoundModelX[i] = 0;
                dFoundModelY[i] = 0;

                ddFoundModelX[i] = 0;
                ddFoundModelY[i] = 0;

                dFoundModelXWorld[i] = 0;
                dFoundModelYWorld[i] = 0;

                dFoundModelAngle[i] = 0;
                dFoundModelScore[i] = 0;
                dFoundModelWidth[i] = 0;
                dFoundModelHeight[i] = 0;
            }
            ModelFinderResult = EVisionResultType.NG;

            bFoundCircle = false;
            dFoundCenterX = 0;
            dFoundCenterY = 0;
            dMovingCenterX = 0;
            dMovingCenterY = 0;
            dAngle = 0;
            dRadius = 0;
            dRadmm = 0;

            CircleFoundResult = EVisionResultType.NG;
            AngleFoundResult = EVisionResultType.ANG;

            nMaxCol = 0;
            nMaxRow = 0;
            nTotalCell = 0;
            nDie_Height = 0;
            nDie_Width = 0;
            WaferDegree = 0;
            DieTotal = 0;

            MapData.Clear();
            DiePos.Clear();
            ListMapInfo.Clear();
            bMapList = false;
            MapFileResult = EVisionResultType.NG;

            Found_Die.Clear();

            CrobImage = null;

            Die_Width_Ratio = 0.0;
            Die_Height_Ratio = 0.0;
            Die_Area_Ratio = 0.0;
            Binary_Threshold = 0;

            ProcessName = "None";

            bBoundRatio = false;

            MMPerPixel_X = 0;
            MMPerPixel_X = 0;

            MapFileName = "None";   // 03.20 insert

            base.Clear();
        }
    }

    public class WaferScanInspectionParam : CameraSlaveParam
    {
        public readonly int AlgIndex;
        public readonly int ModelIndex;
        public readonly int CalibrationIndex;
        private Mat GrayImage = null;

        [Category("Circle Finder")]
        [Circle, Converter(typeof(CircleConverter))]
        public Circle InnerCircle { get; set; }



        //[Category("Model Finder")]
        [Category("WAFER Model Finder")]
        [Content]
        public ModelFinderView Module { get; private set; }

        [ModelFinder, Browsable(false)]
        //[ModelFinder, Browsable(true)]
        public ModelFinderViewModel ModuleModel { get; set; } = new ModelFinderViewModel("Module", EAlgorithmType.ModelFinder);

        [DisplayName("Process Name")]
        [ReadOnly(true)]
        public string ProcessName
        {
            get { return _ProccessName; }
            set
            {
                _ProccessName = value;
                RaisePropertyChanged("ProcessName");
            }
        }
        private string _ProccessName;

        //Die width
        //[Spinnable(1, 5, 0, 20000)]       // 02.22 주석 처리.
        [DisplayName("Die Width(um)")]
        public double DieWidth
        {
            get
            {
                return _DieWidth;
            }
            set
            {
                _DieWidth = value;
                RaisePropertyChanged("DieWidth");
            }
        }
        private double _DieWidth;

        //Die height
        //[Spinnable(1, 5, 0, 20000)]   // 02.22 주석 처리.
        [DisplayName("Die Height(um)")]
        public double DieHeight
        {
            get
            {
                return _DieHeight;
            }
            set
            {
                _DieHeight = value;
                RaisePropertyChanged("DieHeight");
            }
        }
        private double _DieHeight;

        // 01.22 Insert Start
        //[Spinnable(0.5, 10, 0, 10)]   // 02.22 주석 처리.
        [DisplayName("Scrib X(Pixel)")]
        public double Scrib_X
        {
            get { return _Scrib_X; }
            set
            {
                _Scrib_X = value;
                RaisePropertyChanged("Scrib_X");
            }
        }
        private double _Scrib_X;

        //[Spinnable(0.5, 10, 0, 10)]   // 02.22 주석 처리.
        [DisplayName("Scrib Y(Pixel)")]

        public double Scrib_Y
        {
            get { return _Scrib_Y; }
            set
            {
                _Scrib_Y = value;
                RaisePropertyChanged("Scrib_Y");
            }
        }
        private double _Scrib_Y;
        // 01.22 Insert End

        [Spinnable(180, 0, 0, 180)]
        [DisplayName("Rotate Wafer")]
        public double RotateWafer
        {
            get
            {
                return _RotateWafer;
            }
            set
            {
                _RotateWafer = value;
                RaisePropertyChanged("RotateWafer");
            }
        }
        private double _RotateWafer;

        //[Spinnable(0.1, 2.0, 0.0, 2.0)]   // 02.22 주석 처리.
        [DisplayName("Tolerance Rotate")]
        public double Tolerance_Rotate
        {
            get { return _Tolerance_Rotate; }
            set
            {
                _Tolerance_Rotate = value;
                RaisePropertyChanged("Tolerance_Rotate");
            }
        }
        private double _Tolerance_Rotate;

        [CheckableItems("Save Wafer", "Save Wafer")]
        [DisplayName("Save Wafer Image")]
        public bool SaveWaferImage
        {
            get { return _SaveWaferImage; }
            set
            {
                _SaveWaferImage = value;
                RaisePropertyChanged("SaveWaferImage");
            }
        }
        private bool _SaveWaferImage;



        //[Spinnable(1, 100, 10, 100), Browsable(true)]  // Vitual Map에 사용할 BinNumber로 사용.
        [DisplayName("VM Die BIN#1")]
        public int Die_Grade
        {
            get
            {
                return _Die_Grade;
            }
            set
            {
                _Die_Grade = value;
                RaisePropertyChanged("Die_Grade");
            }
        }
        private int _Die_Grade;

        [DisplayName("VM Die BIN#2")]
        public int Die_GradeS
        {
            get
            {
                return _Die_GradeS;
            }
            set
            {
                _Die_GradeS = value;
                RaisePropertyChanged("Die_GradeS");
            }
        }
        private int _Die_GradeS;

        [CheckableItems("VMDieEnable", "VMDieEnable")]
        [DisplayName("VM View Enable")]
        public bool VMDieEnabel
        {
            get { return _VMDieEnable; }
            set
            {
                _VMDieEnable = value;
                RaisePropertyChanged("VMDieEnable");
            }
        }
        private bool _VMDieEnable;

        [DisplayName("Model ArcLength")]
        [ReadOnly(true)]
        public double Model_ArcLength
        {
            get
            {
                return _Model_ArcLength;
            }
            set
            {
                _Model_ArcLength = value;
                RaisePropertyChanged("Model_ArcLength");
            }
        }
        private double _Model_ArcLength;

        // 01.22 Insert Start
        [DisplayName("Min ArcLength")]
        public double Min_ArcLength
        {
            get { return _Min_ArcLength; }
            set
            {
                _Min_ArcLength = value;
                RaisePropertyChanged("Min_ArcLength");
            }
        }
        private double _Min_ArcLength;
        // 01.22 Insert End

        // 02.07 Insert Start
        [CheckableItems("OrientSearch", "OrientSearch"), Browsable(false)]      // 02.22 Not Display
        [DisplayName("Use OrientSearch")]                     // 02.22 Not Display
        //[Browsable(false)]                                    // 02.22 Not Display
        public bool OrientSearch
        {
            get { return _OrientSearch; }
            set
            {
                _OrientSearch = value;
                RaisePropertyChanged("OrientSearch");
            }
        }
        private bool _OrientSearch;
        // 02.07 Insert End


        [Category("Die Inspect")]
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

        [Spinnable(1, 10, 120, 255)]
        [DisplayName("Binary Threshold")]
        public int Binary_Threshold
        {
            get { return _Binary_Threshold; }
            set
            {
                _Binary_Threshold = value;
                RaisePropertyChanged("Binary_Threshold");
            }
        }
        private int _Binary_Threshold;

        [Spinnable(1, 10, 120, 255)]
        [DisplayName("Mophology Threshold")]
        public int Mophology_Threshold
        {
            get { return _Mophology_Threshold; }
            set
            {
                _Mophology_Threshold = value;
                RaisePropertyChanged("Mophology_Threshold");
            }
        }
        private int _Mophology_Threshold;

        [DisplayName("Die Area")]
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

        public enum Die_ROI { InnerROI = 0, OuterROI = 1, Both = 2}

        public Die_ROI InspectionROI
        {
            get { return _InspectionROI; }
            set
            {
                _InspectionROI = value;
                RaisePropertyChanged("InspectionROI");
            }
        }
        private Die_ROI _InspectionROI;

        // 03.04 Insert Start
        [Browsable(false)]
        public int InitROI
        {
            get { return _InitROI; }
            set
            {
                _InitROI = value;
                RaisePropertyChanged("InitROI");
            }
        }
        private int _InitROI;
        // 03.04 Insert End

        [CheckableItems("CalCheck", "CalCheck"), Browsable(false)]
        [DisplayName("Calibration Check")]
        public bool CalCheck
        {
            get { return _CalCheck; }
            set
            {
                _CalCheck = value;
                RaisePropertyChanged("CalCheck");
            }
        }
        private bool _CalCheck;

        [CheckableItems("MophologyEnable", "MophologyEnable")]
        [DisplayName("Mophology Enable")]
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

        [CheckableItems("MophologyImage", "MophologyImage")]
        [DisplayName("Mophology Save")]
        public bool MophologyImage
        {
            get { return _MophologyImage; }
            set
            {
                _MophologyImage = value;
                RaisePropertyChanged("MophologyImage");
            }
        }
        private bool _MophologyImage;

        [CheckableItems("UnusedMap", "UnusedMap"), Browsable(false)]
        [DisplayName("Unused MapFile")]
        public bool MapCheck
        {
            get { return _MapCheck; }
            set
            {
                _MapCheck = value;
                RaisePropertyChanged("MapCheck");
            }
        }
        private bool _MapCheck;

        [Spinnable(1, 10, 0, 10)]
        [DisplayName("Left Shift Col"), Browsable(false)]
        public int Shift_Col
        {
            get { return _Shift_Col; }
            set
            {
                _Shift_Col = value;
                RaisePropertyChanged("Shift_Col");
            }
        }
        private int _Shift_Col;

        [Spinnable(1, 10, 0, 10), Browsable(false)]
        [DisplayName("Up Shift Row")]
        public int Shift_Row
        {
            get { return _Shift_Row; }
            set
            {
                _Shift_Row = value;
                RaisePropertyChanged("Shift_Row");
            }
        }
        private int _Shift_Row;


        [Category("Teching & Debug Condition")]
        public string MapName { get; set; }

        [CheckableItems("TeachingImage", "TeachingImage")]
        [DisplayName("TeachingImage")]
        public bool TeachingImage
        {
            get { return _TeachingImage; }
            set
            {
                _TeachingImage = value;
                RaisePropertyChanged("TeachingImage");
            }
        }
        private bool _TeachingImage;

        

        public string SequenceName { get; set; }
        public string ActionName { get; set; }
        public string OwnerName { get; set; }

        // 03.07 Insert Start
        [Category("Device|WAFER OUTER Light")]
        [DisplayName("Outer Lightlevel")]
        public int OuterLevel { get; set; }
        // 03.07 Insert End


        public WaferScanInspectionParam(object owner, int algIndex, int modelIndex, int calIndex) : base(owner)
        {
            AlgIndex = algIndex;
            ModelIndex = modelIndex;
            CalibrationIndex = calIndex;

            //model finder
            Module = new ModelFinderView(ModuleModel);
            Module.RegisterNewButtonClick(Model_NewButton_Click);
            Module.RegisterEditButtonClick(Model_EditButton_Click);
            Module.RegisterLoadButtonClick(Model_LoadButton_Click);
        }

        public bool CvTemplateMatching(Mat source, Mat masterImage, System.Windows.Rect roi, double minScore, out double foundX, out double foundY, out double foundScore)
        {
            bool bFound = false;
            minScore = 0;
            foundX = 0;
            foundY = 0;
            foundScore = 0;

            if (source == null || masterImage == null) return false;

            Mat roiImage = null;
            Mat resultImage = new Mat();
            try
            {
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
                if (maxVal >= minScore)
                {
                    bFound = true;
                    foundX = maxLoc.X;
                    foundY = maxLoc.Y;
                    foundX += cvRoi.X;
                    foundY += cvRoi.Y;
                }
                else
                {
                    bFound = false;
                    foundX = 0;
                    foundY = 0;
                }

            }
            catch (Exception e)
            {
                bFound = false;
                foundX = 0;
                foundY = 0;
                //occurs error 
                Logging.PrintErrLog((int)ELogType.Error, string.Format("Exception ReturnCode:{0}", "Template matching Error", e.ToString()));
            }
            finally
            {
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
            if (CalCheck)   // Calibration file 사용
            {
                if (ALLIGATOR_ALG_MIL.agtAM_GridCalibrationAssociate(AlgIndex, CalibrationIndex) != 0)
                {
                    CustomMessageBox.Show("Fail to New Model", string.Format("{0} - Grid Calibration Fail. AlgIndex : {1}, ModelIndex : {2}, File : {3}", this.GetType().Name, AlgIndex, ModelIndex, ModuleModel.ModelFile), MessageBoxImage.Error);
                }
                // 02.20 New Dll 적용 후 Error 발생에 따른 주석 처리.
                //if (ALLIGATOR_ALG_MIL.agtAM_GridCalibrationModelAssociate(AlgIndex, ModelIndex, CalibrationIndex) != 0)
                //{
                //    CustomMessageBox.Show("Fail to New Model", string.Format("{0} - Grid Calibration Model Fail. AlgIndex : {1}, ModelIndex : {2}, File : {3}", this.GetType().Name, AlgIndex, ModelIndex, ModuleModel.ModelFile), MessageBoxImage.Error);
                //}
            }
            // Calibration file 사용하지 않을 경우.
            else
            {
                ALLIGATOR_ALG_MIL.agtAM_GridCalibrationDeAssociate(AlgIndex, CalibrationIndex);
                ALLIGATOR_ALG_MIL.agtAM_GridCalibrationModelDeAssociate(AlgIndex, ModelIndex, CalibrationIndex);
            }

            //지정된 영역으로 새 모델 생성
            ALLIGATOR_ALG_MIL.agtAM_SetModel_CS(AlgIndex, ModelIndex, (int)ModuleModel.Master.Left, (int)ModuleModel.Master.Right, (int)ModuleModel.Master.Top, (int)ModuleModel.Master.Bottom);
            if (ALLIGATOR_ALG_MIL.agtAM_FindConfigDialog(AlgIndex, ModelIndex) != 0)
            {
                CustomMessageBox.Show("Fail to New Model", string.Format("{0} - Model Finder, New Model Fail. AlgIndex : {1}, ModelIndex : {2}, File : {3}", this.GetType().Name, AlgIndex, ModelIndex, ModuleModel.ModelFile), MessageBoxImage.Error);
            }
        }

        void Model_EditButton_Click(object sender, RoutedEventArgs e)
        {
            if (CalCheck)
            {
                if (ALLIGATOR_ALG_MIL.agtAM_GridCalibrationAssociate(AlgIndex, CalibrationIndex) != 0)
                {
                    CustomMessageBox.Show("Fail to New Model", string.Format("{0} - Grid Calibration Fail. AlgIndex : {1}, ModelIndex : {2}, File : {3}", this.GetType().Name, AlgIndex, ModelIndex, ModuleModel.ModelFile), MessageBoxImage.Error);
                }
                // 02.20 New Dll 적용 후 Error 발생에 따른 주석 처리.
                if (ALLIGATOR_ALG_MIL.agtAM_GridCalibrationModelAssociate(AlgIndex, ModelIndex, CalibrationIndex) != 0)
                {
                    CustomMessageBox.Show("Fail to New Model", string.Format("{0} - Grid Calibration Model Fail. AlgIndex : {1}, ModelIndex : {2}, File : {3}", this.GetType().Name, AlgIndex, ModelIndex, ModuleModel.ModelFile), MessageBoxImage.Error);
                }
            }
            else
            {
                ALLIGATOR_ALG_MIL.agtAM_GridCalibrationDeAssociate(AlgIndex, CalibrationIndex);
                ALLIGATOR_ALG_MIL.agtAM_GridCalibrationModelDeAssociate(AlgIndex, ModelIndex, CalibrationIndex);
            }

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

            //Load calibration file
            string recipeName = SystemHandler.Handle.Setting.CurrentRecipeName;
            string seqName = SystemHandler.Handle.Sequences[AlgIndex].Name;
            string strCalibrationFile = RecipeFiles.Handle.GetCalibrationFilePath(recipeName, seqName, "Calibration", "WaferScan" + (CalibrationIndex + 1).ToString());
            if (ALLIGATOR_ALG_MIL.agtAM_LoadGridCalibration(AlgIndex, CalibrationIndex, strCalibrationFile) != 0)
            {
                CustomMessageBox.Show("Fail to Load Calibration", string.Format("{0} - Grid Calibration, Load Fail. AlgIndex : {1}, File : {2}", this.GetType().Name, AlgIndex, strCalibrationFile), MessageBoxImage.Error);
                result = false;
            }

            // 03.05 Insert Start
            InspectionROI = (WaferScanInspectionParam.Die_ROI)InitROI;
            InitROI = (int)InspectionROI;
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
            InitROI = (int)InspectionROI;
            // 03.05 Insert End

            return base.Save(saveFile, group);
        }

        public override void PutImage(Mat image)
        {
            if (image == null) return;

            if (GrayImage == null || GrayImage.IsDisposed)
            {
                GrayImage = new Mat(image.Size(), MatType.CV_8UC1);
            }
            else if ((GrayImage.Width != image.Width) || (GrayImage.Height != image.Height))
            {
                GrayImage = new Mat(image.Size(), MatType.CV_8UC1);
            }
            Cv2.CvtColor(image, GrayImage, ColorConversionCodes.BGR2GRAY);
            int result = ALLIGATOR_ALG_MIL.agtAM_PutImage(AlgIndex, GrayImage.Data);
            if (result != 0)
            {
                CustomMessageBox.Show("Fail to PutImage", string.Format("PutImage from Camera : {0}, AlgIndex : {1}", DeviceName, AlgIndex), MessageBoxImage.Error);
            }
        }

        public override bool CopyTo(ParamBase param)
        {
            if (base.CopyTo(param) == false) return false;

            // 02.14 insert Start
            if (param is WaferScanInspectionParam)
            {
                WaferScanInspectionParam WaferParam = param as WaferScanInspectionParam;
                WaferParam.InnerCircle = InnerCircle;

                WaferParam.ModuleModel.Master = ModuleModel.Master;

                if (File.Exists(ModuleModel.ModelFile) && (Path.GetExtension(ModuleModel.ModelFile) == DeviceHandler.EXTENSION_MODEL))
                {
                    WaferParam.ModuleModel.ModelFile = WaferParam.GetExternalFilePath(EExternalFileType.Model, nameof(WaferParam.ModuleModel));
                    File.Copy(ModuleModel.ModelFile, WaferParam.ModuleModel.ModelFile, true);
                    if (WaferParam.LoadModuleExternalFile() == false)
                    {
                        CustomMessageBox.Show("Fail to Load Model", string.Format("{0} - Model Finder, Load Fail. AlgIndex : {1}, ModelIndex : {2}, File : {3}",
                            WaferParam.ToString(), WaferParam.AlgIndex, WaferParam.ModelIndex, WaferParam.ModuleModel.ModelFile), MessageBoxImage.Error);
                        return false;
                    }
                }

                WaferParam.DieWidth = DieWidth;
                WaferParam.DieHeight = DieHeight;
                WaferParam.Scrib_X = Scrib_X;
                WaferParam.Scrib_Y = Scrib_Y;
                WaferParam.RotateWafer = RotateWafer;
                WaferParam.Tolerance_Rotate = Tolerance_Rotate;
                WaferParam.Min_ArcLength = Min_ArcLength;
                WaferParam.OrientSearch = OrientSearch;

                WaferParam.Die_Width_Ratio = Die_Width_Ratio;
                WaferParam.Die_Height_Ratio = Die_Height_Ratio;
                WaferParam.Die_Area_Ratio = Die_Area_Ratio;
                WaferParam.Binary_Threshold = Binary_Threshold;
                WaferParam.Mophology_Threshold = Mophology_Threshold;
                WaferParam.InspectionROI = InspectionROI;   // Insert 02.28

                WaferParam.CalCheck = CalCheck;
                WaferParam.Mophology = Mophology;
                WaferParam.MapCheck = MapCheck;
                WaferParam.Shift_Col = Shift_Col;
                WaferParam.Shift_Row = Shift_Row;

                WaferParam.MapName = MapName;

                WaferParam.PixelToUM_Offset = PixelToUM_Offset;
                WaferParam.LightLevel = LightLevel;
                WaferParam.PropertyArray = PropertyArray;

                WaferParam.InitROI = InitROI;   // Insert 03.04

                WaferParam.OuterLevel = OuterLevel; // Light OuterLevel 03.18

                return true;
            }
            //02.14 insert End
            return false;
        }

        public override double ConvertPixelToMM(double pixel)
        {
            return base.ConvertPixelToMM(pixel);
        }
    }

    public class WaferScanInspectionAction : ActionBase
    {
        private readonly int AlgIndex;
        private readonly int ModelIndex;
        private readonly int CalibrationIndex;

        private WaferScanInspectionActionContext pMyContext;
        private WaferScanInspectionParam pMyParam;

        private VirtualCamera pCamera;
        private Mat GrayImage = null;   // Original Gray
        //private Mat BinaryImage = null; // Binary or Mopology Image 12.23
        private Mat MophologyImage = null; // Binary or Mopology Image 02.28

        //private Mat MasterImage = null;
        private int result = -1;
        //private int foundIndex = 0;

        

        /// <summary>
        /// Maching으로 Map mapping 변수들
        /// </summary>
        //Wafer map mapping 관련
        static List<int> xMagicIndices = new List<int>();
        static List<int> yMagicIndices = new List<int>();
        static List<string> strHeaderLines = new List<string>();
        static List<string> strFooterLines = new List<string>();
        // 구조체 정의
        struct Data
        {
            public int X;
            public int Y;
            public int BinNum;
            public int X1;
            public int Y1;
            public double XP;
            public double YP;
            public int Status;
        }
        static List<Data> dataList = new List<Data>();
        // Coordinate Data
        public struct CoordinateData
        {
            public double X { get; set; }
            public double Y { get; set; }

            public CoordinateData(double x, double y)
            {
                X = x;
                Y = y;
            }
        }

        /// <summary>
        /// Circle find 후 map mapping 변수들
        /// </summary>
        struct MapDataOrigin
        {
            public int nDieCnt;

            public List<int> nX;
            public List<int> nY;
            public List<int> nB;

            public List<int> nTransX;
            public List<int> nTransY;
            public List<int> nS;

            public int nMaxX;
            public int nMinX;
            public int nMaxY;
            public int nMinY;

            // Die Size
            public double dSizeX;
            public double dSizeY;

            // Center가 Scribe에있는지 확인
            public bool bScribX;
            public bool bScribY;

            // Center
            public double dCenterX;
            public double dCenterY;
            // Trans한 Center
            public double dTCenterX;    // Wafer matrix 행의 Center X
            public double dTCenterY;    // Wafer matrix 열의 Center Y


            public void Init()
            {
                nDieCnt = 0;
                if (nX != null)
                {
                    nX.Clear();
                }
                else
                {
                    nX = new List<int>();
                }
                if (nY != null)
                {
                    nY.Clear();
                }
                else
                {
                    nY = new List<int>();
                }

                if (nTransX != null)
                {
                    nTransX.Clear();
                }
                else
                {
                    nTransX = new List<int>();
                }
                if (nTransY != null)
                {
                    nTransY.Clear();
                }
                else
                {
                    nTransY = new List<int>();
                }
                if (nB != null)
                {
                    nB.Clear();
                }
                else
                {
                    nB = new List<int>();
                }

                if (nS != null)
                {
                    nS.Clear();
                }
                else
                {
                    nS = new List<int>();
                }



                nMaxX = 0;
                nMinX = 4096;
                nMaxY = 0;
                nMinY = 4096;

                bScribX = false;
                bScribY = false;

                dSizeX = 0;
                dSizeY = 0;
            }
        }
        MapDataOrigin OriginData;
        // 구조체 정의
        struct FoundPoint

        {
            public double X;
            public double Y;
            public int Status;

            /// <summary>
            /// Insert Struct Field
            /// </summary>
            //public int ContourCount;
            //public double Area;
            //public bool Convex;
            //public int Apex;
        }
        Dictionary<System.Drawing.Point, FoundPoint> OriginPoint;

        // 03.27 Insert - Vitual Map Create Data List
        List<Data> VMdataList = new List<Data>();

        private ALLIGATOR_ALG_MIL.sCModelFindResult ModelResult = new ALLIGATOR_ALG_MIL.sCModelFindResult();
        private double dGetRansacCenterX = 0, dGetRansacCenterY = 0, dGetRansacRadius = 0;

        // 04.02 Insert Start
        // Virtual Map Coordination Dictionary Variable
        Dictionary<int, double> VM_COLS = new Dictionary<int, double>();        // Virtual Map COLUMS Pixel Coordination
        Dictionary<int, double> VM_ROWS = new Dictionary<int, double>();        // virtual map ROWS Pixel Coordination

        //public double VM_COLS_AVG = 0.0;    // Colums Die to Die Average
        //public double VM_ROWS_AVG = 0.0;    // Rows Die to Die Average

        Dictionary<int, double> VM_COLSW = new Dictionary<int, double>();        // Virtual Map COLUMS World Coordination
        Dictionary<int, double> VM_ROWSW = new Dictionary<int, double>();        // virtual map ROWS World Coordination
        // 04.02 Insert End



        #region MAPFILE_Variable
        // Map File Analysis Variable
        public Dictionary<int, _ST_MAP_INFO> m_MapInfo = new Dictionary<int, _ST_MAP_INFO>();
        private Dictionary<int, _ST_MAP_INFO> p_MapInfo = null;
        public int nTotalBin = 0;

        public int nMaxRow = -100;
        public int nMinRow = 100;
        public int nMaxCol = -100;
        public int nMinCol = 100;
        public bool die_Detact = false;

        public string sFileName;
        #endregion

        public enum EStep
        {
            Grab = 0,
            Calibration = 1,
            LineAngle = 2,
            MapDataRead = 3,
            CircleFind = 4,
            ModelFind = 5,
            MapMapping = 6,
            End = 7,
        }

        public WaferScanInspectionAction(EAction id, string name, int algIndex, int modelIndex, int calIndex) : base(id, name)
        {

            AlgIndex = algIndex;
            ModelIndex = modelIndex;
            CalibrationIndex = calIndex;

            Context = new WaferScanInspectionActionContext(this);
            pMyContext = Context as WaferScanInspectionActionContext;

            Param = new WaferScanInspectionParam(this, algIndex, ModelIndex, CalibrationIndex);
            pMyParam = Param as WaferScanInspectionParam;

            OriginPoint = new Dictionary<System.Drawing.Point, FoundPoint>();
        }

        public override void OnCreate()
        {
            base.OnCreate();
        }

        public override void OnLoad()
        {
            //camera property setting
            pCamera = SystemHandler.Handle.Devices[pMyParam.DeviceName];
            if (pCamera != null)
            {
                if (pCamera.Properties == null)
                {
                    CustomMessageBox.Show(pCamera.Name + " Camera Not Open!", "Camera is not open. Please check your connection status.", MessageBoxImage.Error);
                    return;
                }
                if (!pCamera.Properties.ApplyFromParam(pMyParam))
                {
                    CustomMessageBox.Show(pCamera.Name + " Camera Property Set Fail!", "Check camera settings. or camera state.", MessageBoxImage.Error);
                }
                if (!pCamera.SetSoftwareTriggerMode())
                {
                    CustomMessageBox.Show(pCamera.Name + " Camera Software trigger mode Set Fail!", "Check camera settings. or camera state.", MessageBoxImage.Error);
                }
            }
            else
            {
                CustomMessageBox.Show(pMyParam.DeviceName + " Camera Not Open!", "Camera is not open. Please check your connection status.", MessageBoxImage.Error);
                return;
            }

            // 01.20 insert start
            try
            {
                if (pMyParam.ModuleModel.Master.IsEmpty)
                {
                    pMyParam.Model_ArcLength = 0;
                    pMyParam.Min_ArcLength = 0;
                }
                else
                {
                    pMyParam.Model_ArcLength = pMyParam.ModuleModel.Master.Size.Width * 2 + pMyParam.ModuleModel.Master.Size.Height * 2;
                    pMyParam.Min_ArcLength = pMyParam.Model_ArcLength / 2;
                }
            }
            catch (Exception e)
            {
                Logging.PrintErrLog((int)ELogType.Error, $"Model ArcLength Exception:{e.Message}");
            }
            // 01.20 insert End

            // 02.06 Insert Start
            // Wafer 조명의 경우, Application이 처음 실행 하고, 첫 동작 시 Light가 켜지지 않는 문제가 발생. (현재는 제어에서 한번 OFF 후 On 시키는 방식으로 임시 조치)
            // Load 될때 한번은 반드시 OFF 시키는 코드 추가. (테스트 필요)
            //Debug.WriteLine($"{SystemHandler.Handle.Lights.GetOnOff("WAFER")}");
            //SystemHandler.Handle.Lights.SetLevel("WAFER", 150);
            //SystemHandler.Handle.Lights.SetOnOff("WAFER", true);        // 02.07 insert
            //SystemHandler.Handle.Lights.SetOnOff("WAFER", false);       // 02.07 insert
            // 02.06 Insert End

            // 03.07 Insert Start (Outer Chanel 추가 시 분리해서 초기화해야 하므로 추가)
            //SystemHandler.Handle.Lights.SetLevel("WAFER_OUTER", 1);
            //SystemHandler.Handle.Lights.SetOnOff("WAFER_OUTER", true);
            //SystemHandler.Handle.Lights.SetOnOff("WAFER_OUTER", false);
            // 03.07 Insert End

            // 02.22 insert Start
            // Wafer Line Angle 검출에 OrientSearch  함수 사용하지 않음.
            pMyParam.OrientSearch = false;
            pMyParam.CalCheck = true;
            // 02.22 insert End

            // 02.28 Insert Start
            if ((pMyParam.InspectionROI != WaferScanInspectionParam.Die_ROI.InnerROI) || (pMyParam.InspectionROI != WaferScanInspectionParam.Die_ROI.OuterROI) || (pMyParam.InspectionROI != WaferScanInspectionParam.Die_ROI.Both))
                //pMyParam.InspectionROI = WaferScanInspectionParam.Die_ROI.InnerROI;   // 03.04 주석 처리.
                pMyParam.InspectionROI = (WaferScanInspectionParam.Die_ROI)pMyParam.InitROI;    // 03.04 코드 변경.
            else
                pMyParam.InspectionROI = (WaferScanInspectionParam.Die_ROI)pMyParam.InitROI;    // 03.04 코드 변경.
            // 02.28 Insert End

            base.OnLoad();
        }

        public override void OnBegin(SequenceContext prevResult = null)
        {
            if (prevResult.TargetCode != null)
                pMyParam.MapName = prevResult.TargetCode;

            // 01.24 insert start
            string ActName = Param.OwnerName;
            if (ActName == "Inspect_Left")
                pMyContext.ProcessName = pMyParam.ProcessName = "Left WAFER";
            else if (ActName == "Inspect_Right")
                pMyContext.ProcessName = pMyParam.ProcessName = "Right WAFER";
            else
                pMyContext.ProcessName = pMyParam.ProcessName = "None";
            // 01.24 insert End

            // 02.28 Insert Start
            if ((pMyParam.Die_Width_Ratio > 1.0) || (pMyParam.Die_Width_Ratio < 0.5))
                pMyParam.Die_Width_Ratio = 0.8;
            if ((pMyParam.Die_Height_Ratio > 1.0) || (pMyParam.Die_Height_Ratio < 0.5))
                pMyParam.Die_Height_Ratio = 0.8;
            // 02.28 Insert End


            base.OnBegin(prevResult);
        }

        public override void OnEnd()
        {
            base.OnEnd();
        }


        /// <summary>
        /// Common methods [ReadMapFile]
        /// </summary>
        private List<Data> ReadMapFile(string mapFile)
        {
            List<Data> dataList = new List<Data>();
            strHeaderLines.Clear();
            strFooterLines.Clear();
            OriginData.Init();
            OriginPoint.Clear();
            bool bEndofData = false;

            // pMyParam.Scrib_X * (pMyContext.MMPerPixel_X * 1000) => Scrib_X Pixel 개수 * 1-Pixel per mm
            // pMyParam.Scrib_Y * (pMyContext.MMPerPixel_Y * 1000) => Scrib_Y Pixel 개수 * 1-Pixel per mm
            OriginData.dSizeX = pMyParam.DieWidth + (pMyParam.Scrib_X * (pMyContext.MMPerPixel_X * 1000));
            OriginData.dSizeY = pMyParam.DieHeight + (pMyParam.Scrib_Y * (pMyContext.MMPerPixel_Y * 1000));

            try
            {
                using (StreamReader file = new StreamReader(mapFile))
                {
                    string line;
                    while ((line = file.ReadLine()) != null)
                    {
                        if (line.Length > 1 && line[0] == '[')
                        {
                            strHeaderLines.Add(line);
                            continue;
                        }
                        else if (line.Length > 1 && line[0] == 'E')
                            bEndofData = true;

                        if (bEndofData == true)
                        {
                            strFooterLines.Add(line);
                            continue;
                        }
                        // 정규식 패턴을 사용하여 값을 추출
                        MatchCollection matches = Regex.Matches(line, @"[A-Z]= \d+");

                        int xValue = 0, yValue = 0, bValue = 0;

                        // 추출된 각 매치를 반복하면서 값을 읽어 들임
                        foreach (Match match in matches)
                        {
                            string[] parts = match.Value.Split('=');
                            if (parts.Length == 2)
                            {
                                string key = parts[0];
                                int value = 0;
                                if (int.TryParse(parts[1], out value))
                                {
                                    if (key == "X")
                                    {
                                        xValue = value;     // Matrix X-index
                                        OriginData.nX.Add(value);
                                        if (value < OriginData.nMinX)
                                            OriginData.nMinX = value;
                                        if (value >= OriginData.nMaxX)
                                            OriginData.nMaxX = value;
                                    }
                                    else if (key == "Y")
                                    {
                                        yValue = value;     // Matrix Y-index
                                        OriginData.nY.Add(value);
                                        if (value < OriginData.nMinY)
                                            OriginData.nMinY = value;
                                        if (value >= OriginData.nMaxY)
                                            OriginData.nMaxY = value;
                                    }
                                    else if (key == "B")
                                    {
                                        bValue = value;
                                        OriginData.nB.Add(value);
                                    }
                                }
                            }
                        }


                        dataList.Add(new Data { X = xValue, Y = yValue, BinNum = bValue, X1 = 0, Y1 = 0, XP = 0, YP = 0, Status = 0 });
                        OriginData.nDieCnt++;
                    }
                }
            }
            catch
            {
                Logging.PrintErrLog((int)ELogType.Error, "Check map file(It could not be existance).");
                return (dataList);
            }

            CenterPositionGet(out OriginData.dCenterX, out OriginData.dCenterY);        // Center Position이 Scrib Line에 존재하는지, Die 에 존재하는지 확인.
            Translation_Data(pMyParam.RotateWafer, pMyParam.Die_Grade);

            Debug.WriteLine($"Translation Center Position: {OriginData.dTCenterX} {OriginData.dTCenterY}");
            Debug.WriteLine($"OrinPoint: {OriginPoint.Values}");


            return (dataList);
        }


        // 03.27 Insert - Create Virtual Map Matching Position.
        // 반드시, ReadMap과 Circle Find 이후에 해당 함수를 호출하여야 함.
        // Circle 중심에서 부터 Die 위치라고 생각되는 부분의 위치를 계산.
        public void CreateVM()
        {
            VMdataList.Clear();

            double Die_Width_mm = pMyParam.DieWidth / 1000;     // Die width mm
            double Die_Height_mm = pMyParam.DieHeight / 1000;   // die Height mm

            double Die_Half_Width_mm = Die_Width_mm / 2;        // die Half width mm
            double Die_Half_Height_mm = Die_Height_mm / 2;      // die Half Height mm

            double Scrib_X_mm = pMyParam.Scrib_X * pMyContext.MMPerPixel_X; // Scrib mm (Pixel * 1-Pixel per mm)
            double Scrib_Y_mm = pMyParam.Scrib_Y * pMyContext.MMPerPixel_Y; // Scrib mm (Pixel * 1-Pixel per mm)

            // 화면 중심이 원점(0,0)이므로, Wafer Circle이 화면 중심까지 떨어진 거리를 나타내며, circle 중심점이 화면 중심으로 이동해야 하는 값.
            double DistX = pMyContext.CenterOffsetXmm;
            double DistY = pMyContext.CenterOffsetYmm;

            // 행렬의 시작점이 0에서 시작 하므로, 실제 행과 열의 개수는 +1 해야 함.
            double XCenter = Math.Truncate(OriginData.dTCenterX);   // map file 행의 중심 - 소수점 제외
            double YCenter = Math.Truncate(OriginData.dTCenterY);   // map file 열의 중심 - 소수점 제외

            Debug.Indent();
            Debug.WriteLine($"Real Colum Count:{XCenter+1}, Real Row Count:{YCenter+1}");
            Debug.WriteLine($"Matrix Center(ABS) : {XCenter}, {YCenter}");
            Debug.WriteLine($"Circle Center X:{pMyContext.dFoundCenterXWorld}, Circle Center Y:{ pMyContext.dFoundCenterYWorld}");
            Debug.WriteLine($"Circle Center X-OffSet:{pMyContext.CenterOffsetXmm}, Circle Center Y-Offset:{ pMyContext.CenterOffsetYmm}");
            Debug.WriteLine($"{Die_Width_mm}, {Die_Height_mm}");
            Debug.WriteLine($"{Die_Half_Width_mm}, {Die_Half_Height_mm}");
            Debug.WriteLine($"{Scrib_X_mm}, {Scrib_Y_mm}");
            Debug.WriteLine($"{OriginData.nDieCnt}");
            Debug.WriteLine($"Column Count:{OriginData.nMaxX - OriginData.nMinX}, Row Count:{OriginData.nMaxY - OriginData.nMinY}");
            Debug.Unindent();


            Dictionary<int, double> Matrix_COLS = new Dictionary<int, double>();        // Virtual Map COLUMS Pixel Coordination
            Dictionary<int, double> Matrix_ROWS = new Dictionary<int, double>();        // virtual map ROWS Pixel Coordination

            Matrix_COLS.Clear();
            Matrix_ROWS.Clear();

            for (int i = 0; i <= (OriginData.nMaxX-OriginData.nMinX); i++)
            {
                int key = 0;
                int colNum = 0;
                double X_Value = 0.0;

                if(OriginData.bScribX)
                {
                    if(i < OriginData.dTCenterX)    
                    {

                        // 열 중심의 왼쪽에 있다면,
                        if((OriginData.dTCenterX - i) < 1)  // 왼쪽 바로 옆
                        {
                            key = i;

                            X_Value = pMyContext.dFoundCenterXWorld - (Scrib_X_mm/2) - Die_Half_Width_mm;     // Find Circle Center
                            //X_Value =  -(Scrib_X_mm/2) - Die_Half_Width_mm; // Screen Center
                        }
                        else
                        {
                            key = i;
                            colNum = (int)Math.Truncate(OriginData.dTCenterX - i);

                            X_Value = pMyContext.dFoundCenterXWorld - (colNum * (Die_Width_mm + Scrib_X_mm)) - (Scrib_X_mm/2) - Die_Half_Width_mm;    // Find Circle Center
                            //X_Value =  -(colNum * (Die_Width_mm + Scrib_X_mm)) - (Scrib_X_mm/2) - Die_Half_Width_mm;    // Screen Center
                        }
                    }
                    else
                    {
                        if ((OriginData.dTCenterX - i) == 0)
                        {
                            key = i;

                            X_Value = pMyContext.dFoundCenterXWorld;  // Find Circle Center
                            //X_Value = 0;    // Screen Center
                        }
                        else
                        {
                            key = i;
                            colNum = (int)Math.Truncate(i - OriginData.dTCenterX);

                            X_Value = pMyContext.dFoundCenterXWorld + (colNum * (Die_Width_mm + Scrib_X_mm)) + (Scrib_X_mm/2) + Die_Half_Width_mm;    // Find Circle Center
                            //X_Value = (colNum * (Die_Width_mm + Scrib_X_mm)) + (Scrib_X_mm/2) + Die_Half_Width_mm;  // Screen Center
                        }
                    }

                    Matrix_COLS.Add(key, X_Value);
                }
                else
                {
                    if(i < OriginData.dTCenterX)
                    {
                        key = i;
                        colNum = (int)Math.Truncate(OriginData.dTCenterX - i);

                        X_Value = pMyContext.dFoundCenterXWorld - (colNum * (Die_Width_mm + Scrib_X_mm)); // Find Circle Center
                        //X_Value =  -(colNum * (Die_Width_mm + Scrib_X_mm));   // Screen Center
                    }
                    else
                    {
                        if((i - OriginData.dTCenterX) == 0)
                        {
                            key = i;

                            X_Value = pMyContext.dFoundCenterXWorld;  // Find Circle Center
                            //X_Value = 0;    // Screen Center
                        }
                        else
                        {
                            key = i;
                            colNum = (int)Math.Truncate(i - OriginData.dTCenterX);

                            X_Value = pMyContext.dFoundCenterXWorld + (colNum * (Die_Width_mm + Scrib_X_mm)); // Find Circle Center
                            //X_Value = (colNum * (Die_Width_mm + Scrib_X_mm));       // Screen Center
                        }
                    }

                    Matrix_COLS.Add(key, X_Value);
                }
            }

            for (int i = 0; i <= (OriginData.nMaxY - OriginData.nMinY); i++)
            {
                int key = 0;
                int rowNum = 0;
                double Y_Value = 0.0;

                if (OriginData.bScribY)
                {
                    if (i < OriginData.dTCenterY)
                    {
                        // 행 중심의 위쪽에 있다면,
                        if ((OriginData.dTCenterY - i) < 1)  // 위쪽 바로 위
                        //if (((OriginData.dTCenterY - i) < 1) && ((OriginData.dTCenterY - i) > 0))  // 위쪽 바로 위
                        {
                            key = i;

                            Y_Value = pMyContext.dFoundCenterYWorld - (Scrib_Y_mm / 2) - Die_Half_Height_mm;  // Find Circle Center
                            //Y_Value =  -(Scrib_Y_mm / 2) - Die_Half_Height_mm;  // Screen Center
                        }
                        else
                        {
                            key = i;
                            rowNum = (int)Math.Truncate(OriginData.dTCenterY - i);
                            //rowNum = (int)Math.Round(OriginData.dTCenterY - i);

                            Y_Value = pMyContext.dFoundCenterYWorld - (rowNum * (Die_Height_mm + Scrib_Y_mm)) - (Scrib_Y_mm / 2) - Die_Half_Height_mm;        // Find Circle Center
                            //Y_Value = -(rowNum * (Die_Height_mm + Scrib_Y_mm)) - (Scrib_Y_mm / 2) - Die_Half_Height_mm;  // Screen Center
                        }

                        //if ((Y_Value < pMyContext.dFoundCenterYWorld) && (pMyContext.CenterOffsetYmm < 0))
                        //    Y_Value -= Math.Abs(pMyContext.CenterOffsetYmm);
                        //else if ((Y_Value < pMyContext.dFoundCenterYWorld) && (pMyContext.CenterOffsetYmm > 0))
                        //    Y_Value += Math.Abs(pMyContext.CenterOffsetYmm);
                        //else if ((Y_Value > pMyContext.dFoundCenterYWorld) && (pMyContext.CenterOffsetYmm < 0))
                        //    Y_Value -= Math.Abs(pMyContext.CenterOffsetYmm);
                        //else if ((Y_Value > pMyContext.dFoundCenterYWorld) && (pMyContext.CenterOffsetYmm > 0))
                        //    Y_Value += Math.Abs(pMyContext.CenterOffsetYmm);
                        //else
                        //    Y_Value += 0;

                        //if (pMyContext.CenterOffsetYmm < 0)
                        //    Y_Value += pMyContext.CenterOffsetYmm;
                        //else
                        //    Y_Value -= pMyContext.CenterOffsetYmm;
                    }
                    else
                    {
                        if ((OriginData.dTCenterY - i) == 0)
                        {
                            key = i;

                            Y_Value = pMyContext.dFoundCenterYWorld;  // Find Circle Center
                            //Y_Value = 0;    // Screen Center
                        }
                        //else if (((i - OriginData.dCenterY) < 1) && ((i - OriginData.dTCenterY) > 0))  // 중심에서 바로 아래
                        else if ((i - OriginData.dTCenterY) < 1)  // 중심에서 바로 아래
                        {
                            key = i;
                            Y_Value = pMyContext.dFoundCenterYWorld + (Scrib_Y_mm / 2) + Die_Half_Height_mm;  // Screen Center
                        }
                        else
                        {
                            key = i;
                            rowNum = (int)Math.Truncate(i - OriginData.dTCenterY);
                            //rowNum = (int)Math.Round(i - OriginData.dTCenterY);

                            Y_Value = pMyContext.dFoundCenterYWorld + (rowNum * (Die_Height_mm + Scrib_Y_mm)) + (Scrib_Y_mm / 2) + Die_Half_Height_mm;    // Find Circle Center
                            //Y_Value = (rowNum * (Die_Height_mm + Scrib_Y_mm)) + (Scrib_Y_mm / 2) + Die_Half_Height_mm;  // Screen Center
                        }

                        //if ((Y_Value < pMyContext.dFoundCenterYWorld) && (pMyContext.CenterOffsetYmm < 0))
                        //    Y_Value -= Math.Abs(pMyContext.CenterOffsetYmm);
                        //else if ((Y_Value < pMyContext.dFoundCenterYWorld) && (pMyContext.CenterOffsetYmm > 0))
                        //    Y_Value += Math.Abs(pMyContext.CenterOffsetYmm);
                        //else if ((Y_Value > pMyContext.dFoundCenterYWorld) && (pMyContext.CenterOffsetYmm < 0))
                        //    Y_Value -= Math.Abs(pMyContext.CenterOffsetYmm);
                        //else if ((Y_Value > pMyContext.dFoundCenterYWorld) && (pMyContext.CenterOffsetYmm > 0))
                        //    Y_Value += Math.Abs(pMyContext.CenterOffsetYmm);
                        //else
                        //    Y_Value += 0;

                        //if (pMyContext.CenterOffsetYmm < 0)
                        //    Y_Value += pMyContext.CenterOffsetYmm;
                        //else
                        //    Y_Value -= pMyContext.CenterOffsetYmm;
                    }

                    Matrix_ROWS.Add(key, Y_Value);
                }
                else
                {
                    if ((OriginData.dTCenterY - i) == 0)
                    {
                        key = i;

                        //Y_Value = pMyContext.dFoundCenterYWorld;  // find Circle Center
                        Y_Value = 0;    // Screen Center
                    }
                    else
                    {
                        if(i < OriginData.dTCenterY)
                        {
                            key = i;
                            rowNum = (int)Math.Truncate(OriginData.dTCenterY - i);

                            //Y_Value = pMyContext.dFoundCenterYWorld - (rowNum * (Die_Height_mm + Scrib_Y_mm));    // Find Circle Center
                            Y_Value = -(rowNum * (Die_Height_mm + Scrib_Y_mm)); // Screen Center
                        }
                        else
                        {
                            key = i;
                            rowNum = (int)Math.Truncate(i - OriginData.dTCenterY);

                            //Y_Value = pMyContext.dFoundCenterYWorld + (rowNum * (Die_Height_mm + Scrib_Y_mm));    // Find Circle Center
                            Y_Value = (rowNum * (Die_Height_mm + Scrib_Y_mm));  // Screen Center
                        }
                    }

                    Matrix_ROWS.Add(key, Y_Value);
                }
            }

            // OriginData.nDieCnt : 맵파일의 존재하는 Die의 개수 (행 * 열)
            for (int i=0; i < OriginData.nDieCnt; i++)
            {
                double XP = 0;
                double YP = 0;

                if (Matrix_COLS.ContainsKey(OriginData.nTransX[i]) == true)
                    Matrix_COLS.TryGetValue(OriginData.nTransX[i], out XP);
                else
                    Debug.WriteLine($"nTransX:{i} is not Found");

                if (Matrix_ROWS.ContainsKey(OriginData.nTransY[i]) == true)
                    Matrix_ROWS.TryGetValue(OriginData.nTransY[i], out YP);
                else
                    Debug.WriteLine($"nTransY:{i} is not Found");


                Debug.WriteLine($"Virtual Map num:{i}, TX:{OriginData.nTransX[i]}, TY:{OriginData.nTransY[i]} XP:{XP}, YP={YP}");

                VMdataList.Add(new Data { X = OriginData.nX[i], Y = OriginData.nY[i], BinNum = OriginData.nB[i], X1 = OriginData.nTransX[i], Y1 = OriginData.nTransY[i], XP = XP, YP = YP, Status = 0 });

                // circle 중심과 Screen 중심의 거리 Offset 추가
                //VMdataList.Add(new Data { X = OriginData.nX[i], Y = OriginData.nY[i], BinNum = OriginData.nB[i], X1 = OriginData.nTransX[i], Y1 = OriginData.nTransY[i],
                    //XP = XP + pMyContext.CenterOffsetXmm, YP = YP + pMyContext.CenterOffsetYmm, Status = 0 });
                    //XP = XP + Math.Abs(pMyContext.CenterOffsetXmm), YP = YP + Math.Abs(pMyContext.CenterOffsetYmm), Status = 0 });
            }
        }


        /// <summary>
        /// Matcing map mapping algorithm methods [DetectOuliersZScore, RemoveOuliers, ModelPosIndexing, WaferMapMapping]
        /// </summary>
        static int[] DetectOutliersZScore(double[] data, double threshold)
        {
            double mean = data.Mean();
            double stdDev = data.StandardDeviation();
            double[] zScores = data.Select(x => Math.Abs((x - mean) / stdDev)).ToArray();
            return Enumerable.Range(0, data.Length).Where(i => zScores[i] > threshold).ToArray();
        }

        static double[] RemoveOutliers(double[] data, int[] outliers)
        {
            return data.Where((value, index) => !outliers.Contains(index)).ToArray();
        }

        public (List<int>, List<int>, List<double>, List<double>, List<double>, List<double>) ModelPosIndexing(double xCompen, double yCompen)
        {
            List<int> xIndices = new List<int>();
            List<int> yIndices = new List<int>();
            List<double> xMods = new List<double>();
            List<double> yMods = new List<double>();
            List<double> xPoss = new List<double>();
            List<double> yPoss = new List<double>();
            double dWidth = pMyParam.DieWidth;
            double dHeight = pMyParam.DieHeight;

            for (int i = 0; i < ModelResult.nCnt; i++)
            {
                int xIndex = (int)((ModelResult.dXPos[i] + xCompen) / dWidth);
                int yIndex = (int)((ModelResult.dYPos[i] + yCompen) / dHeight);
                double xMod = (ModelResult.dXPos[i] + xCompen) % dWidth;
                double yMod = (ModelResult.dYPos[i] + yCompen) % dHeight;

                xIndices.Add(xIndex);
                yIndices.Add(yIndex);
                xMods.Add(xMod / dWidth);
                yMods.Add(yMod / dHeight);
                xPoss.Add(ModelResult.dXPos[i]);
                yPoss.Add(ModelResult.dYPos[i]);
            }

            return (xIndices, yIndices, xMods, yMods, xPoss, yPoss);
        }

        public void WaferMapMapping()
        {
            // Model position indexing
            var (xPosIndices, yPosIndices, xMods, yMods, xPoss, yPoss) = ModelPosIndexing(0, 0);
            var xNpMods = xMods.ToArray();
            var yNpMods = yMods.ToArray();
            var xOutliers = DetectOutliersZScore(xNpMods, threshold: 3);
            var yOutliers = DetectOutliersZScore(yNpMods, threshold: 3);
            var xFilteredData = RemoveOutliers(xNpMods, xOutliers);
            var yFilteredData = RemoveOutliers(yNpMods, yOutliers);
            double dWidth = pMyParam.DieWidth;
            double dHeight = pMyParam.DieHeight;
            //double dResolution = pMyParam.PixelToMM_Offset;
            double dResolution = pMyParam.PixelToUM_Offset; // 02.14
            int nImgWidth = pMyContext.ResultImage.Width;
            int nImgHeight = pMyContext.ResultImage.Height;
            var xCompensation = (0.5 - Statistics.Mean(xFilteredData)) * dWidth;
            var yCompensation = (0.5 - Statistics.Mean(yFilteredData)) * dHeight;

            // 위치 보정값이 적용된 Map read
            (xPosIndices, yPosIndices, xMods, yMods, xPoss, yPoss) = ModelPosIndexing(xCompensation, yCompensation);

            // 인덱스화된 위치 좌표 출력
            xNpMods = xMods.ToArray();
            yNpMods = yMods.ToArray();
            xOutliers = DetectOutliersZScore(xNpMods, threshold: 3);
            yOutliers = DetectOutliersZScore(yNpMods, threshold: 3);
            xFilteredData = RemoveOutliers(xNpMods, xOutliers);
            yFilteredData = RemoveOutliers(yNpMods, yOutliers);

            //////////////////////////////////////////////////////////////////////////
            /// 이미지에 좌표 점을 그림
            /// Pos data
            int nMinXPosValue = xPosIndices.Min();
            int nMaxXPosValue = xPosIndices.Max();
            int nMinYPosValue = yPosIndices.Min();
            int nMaxYPosValue = yPosIndices.Max();
            int nXPosWidth = nMaxXPosValue - nMinXPosValue + 1;
            int nYPosWidth = nMaxYPosValue - nMinYPosValue + 1;
            Mat pos_image = new Mat(new OpenCvSharp.Size(nXPosWidth, nYPosWidth), MatType.CV_8U, Scalar.All(0));
            // 이미지 좌표와 X, Y 값을 저장할 Dictionary 생성
            Dictionary<(int x, int y), CoordinateData> coordinateMap = new Dictionary<(int x, int y), CoordinateData>();
            for (int i = 0; i < xPosIndices.Count; i++)
            {
                pos_image.Set<byte>(yPosIndices[i] - nMinYPosValue, xPosIndices[i] - nMinXPosValue, 255);
                // 이미지 좌표 (x, y)와 대응하는 X, Y 값을 저장
                coordinateMap[(xPosIndices[i] - nMinXPosValue, yPosIndices[i] - nMinYPosValue)] = new CoordinateData(xPoss[i], yPoss[i]);
            }
            //Cv2.ImWrite("D:/Pos_Indices.bmp", pos_image);
            /////////////////////////////////////////////////////////////////////
            /// Map data
            int nMinXMapValue = dataList.Min(data => data.X);
            int nMaxXMapValue = dataList.Max(data => data.X);
            int nMinYMapValue = dataList.Min(data => data.Y);
            int nMaxYMapValue = dataList.Max(data => data.Y);
            int nXMapWidth = nMaxXMapValue - nMinXMapValue + 1;
            int nYMapWidth = nMaxYMapValue - nMinYMapValue + 1;
            Mat map_image = new Mat(new OpenCvSharp.Size(nXMapWidth + 4, nYMapWidth + 4), MatType.CV_8U, Scalar.All(0));
            for (int i = 0; i < dataList.Count; i++)
            {
                if (dataList[i].BinNum == 18 || dataList[i].BinNum == 22)
                {
                    map_image.Set<byte>(dataList[i].Y - nMinYMapValue + 2, dataList[i].X - nMinXMapValue + 2, 255);
                }
            }
            //Cv2.ImWrite("D:/Map_Indices.bmp", map_image);
            ///////////////////////////////////////////////////////////////////////////////////////////
            // Index Drawing
            // Create a scatter plot for wafer indices
            // 템플릿 매칭 수행
            Mat result = new Mat();
            Cv2.MatchTemplate(map_image, pos_image, result, TemplateMatchModes.CCoeffNormed);

            // 유사도 맵에서 최대 유사도 위치 찾기
            double minVal, maxVal;
            OpenCvSharp.Point minLoc, maxLoc;
            Cv2.MinMaxLoc(result, out minVal, out maxVal, out minLoc, out maxLoc);

            //Die Inspection
            // 이미지 좌표에 대응하는 X, Y 값을 읽고 출력
            pMyContext.MapData.Clear();
            nMaxCol = -100;
            nMaxRow = -100;
            nMinCol = 100;
            nMinRow = 100;
            nTotalBin = 0;
            CoordinateData cdata;
            for (int i = 0; i < dataList.Count; i++)
            {
                // Image
                int nXImage = dataList[i].X - nMinXMapValue + 2 - maxLoc.X;
                int nYImage = dataList[i].Y - nMinYMapValue + 2 - maxLoc.Y;
                byte brightness = 0;
                if (nXImage >= 0 && nXImage < nXPosWidth && nYImage >= 0 && nYImage < nYPosWidth)
                    brightness = pos_image.At<byte>(nYImage, nXImage);
                Data data = dataList[i];
                data.X1 = nXImage + (maxLoc.X - 2);
                data.Y1 = nYImage + (maxLoc.Y - 2);

                //Wafer map data add
                _ST_MAP_INFO mapinfp = new _ST_MAP_INFO();
                mapinfp.Pos_X = 0;
                mapinfp.Pos_Y = 0;

                //Compare
                bool bContain = false;
                if (coordinateMap.ContainsKey((nXImage, nYImage)))
                    bContain = true;
                if (brightness == 255 && bContain == true)
                {
                    cdata = coordinateMap[(nXImage, nYImage)];
                    System.Drawing.Point MatrixPoint = new System.Drawing.Point();
                    Found_Die_Info founddie = new Found_Die_Info();
                    //data.Status = 1;      // 수정
                    data.XP = (cdata.X - (nImgWidth / 2)) * dResolution / 1000;
                    data.YP = (cdata.Y - (nImgHeight / 2)) * dResolution / 1000;

                    //found die
                    founddie.X_Pos = cdata.X;
                    founddie.Y_Pos = cdata.Y;
                    founddie.Width = pMyParam.ModuleModel.Master.Width;
                    founddie.Height = pMyParam.ModuleModel.Master.Height;
                    MatrixPoint.X = data.X1;
                    MatrixPoint.Y = data.Y1;

                    //pMyContext.Found_Die.Add(MatrixPoint, founddie);

                    int ContourCount = 0;
                    double ContourArea = 0;
                    bool Convex = false;
                    int ApexCount = 0;

                    Moments moments = null;
                    Mat Die_Image = null;

                    //Die Inspection
                    //if (DieInspection(founddie, MatrixPoint) == false)
                    //if (DieInspection(founddie, MatrixPoint, ref ContourCount, ref ContourArea, ref Convex, ref ApexCount) == false) // 01.20 이전
                    //if (DieInspection(founddie, MatrixPoint, ref ContourCount, ref ContourArea, ref Convex, ref ApexCount, ref Die_Image) == false)   // 01.20
                    if (DieInspection(founddie, MatrixPoint, ref ContourCount, ref ContourArea, ref Convex, ref ApexCount, ref moments, ref Die_Image) == false) // 01.22
                    {
                        //founddie.ContourCount = ContourCount;
                        //founddie.Area = ContourArea;
                        //founddie.Convex = Convex;
                        //founddie.Apex = ApexCount;

                        data.Status = 2;
                    }
                    else
                    {
                        //founddie.ContourCount = ContourCount;
                        //founddie.Area = ContourArea;
                        //founddie.Convex = Convex;
                        //founddie.Apex = ApexCount;

                        data.Status = 1;
                    }

                    founddie.ContourCount = ContourCount;
                    founddie.Area = ContourArea;
                    founddie.Convex = Convex;
                    founddie.Apex = ApexCount;

                    founddie.moments = moments; // 01.22 insert

                    founddie.Die_Image = Die_Image; // 01.20 insert

                    pMyContext.Found_Die.Add(MatrixPoint, founddie);

                    mapinfp.Pos_X = cdata.X;
                    mapinfp.Pos_Y = cdata.Y;
                }
                dataList[i] = data;

                //Wafer map data add
                mapinfp.Org_X = OriginData.nX[i];
                mapinfp.Org_Y = OriginData.nY[i];
                mapinfp.Bin = OriginData.nB[i];
                mapinfp.Tgt_X = OriginData.nTransX[i];
                if (mapinfp.Tgt_X >= nMaxCol) nMaxCol = mapinfp.Tgt_X + 1;
                mapinfp.Tgt_Y = OriginData.nTransY[i];
                if (mapinfp.Tgt_Y >= nMaxRow) nMaxRow = mapinfp.Tgt_Y + 1;
                mapinfp.Succ = data.Status;

                //Wafer map data add
                pMyContext.MapData.Add(i, mapinfp);

                //Debugging
                //Debug.WriteLine($"MapData Count:{pMyContext.MapData.Count}");
            }

            pMyContext.nMaxCol = nXPosWidth;
            pMyContext.nMaxRow = nYPosWidth;
            pMyContext.nTotalCell = pMyContext.nMaxCol * pMyContext.nMaxRow;

            // 맵 파일 해당 인덱스에 위치 좌표값 추가
            string mapIndexFile = Path.Combine(SystemHandler.Handle.Setting.MapDataSavePath, pMyParam.MapName);
            using (StreamWriter writer = new StreamWriter(mapIndexFile))
            {
                // Header
                for (int i = 0; i < strHeaderLines.Count; i++)
                {
                    string line = strHeaderLines[i];
                    writer.WriteLine(line);
                }

                // Body
                // 이미지 좌표에 대응하는 X, Y 값을 읽고 출력
                for (int i = 0; i < dataList.Count; i++)
                {
                    //Line
                    string line;
                    line = String.Format("X= {0:0000} Y= {1:0000} B= {2:00} X1= {3:0000} Y1= {4:0000} XP= {5:0.000} YP= {6:0.000} S= {7}",
                        dataList[i].X, dataList[i].Y, dataList[i].BinNum, dataList[i].X1, dataList[i].Y1,
                        dataList[i].XP, dataList[i].YP, dataList[i].Status);

                    //Console.WriteLine(line);
                    writer.WriteLine(line);

                    //Wafer map data add
                    //pMyContext.MapData.Add(i, temp);  // Origin Map File에 있는 모든 Die의 위치를 표기할 때,

                    //Debugging
                    //Debug.WriteLine($"MapData Count:{pMyContext.MapData.Count}");
                }

                // Footer
                for (int i = 0; i < strFooterLines.Count; i++)
                {
                    string line = strFooterLines[i];
                    writer.WriteLine(line);
                }
            }
        }

        /// <summary>
        /// Circle find map mapping methods
        /// </summary>
        /// <returns></returns>
        public void CenterPositionGet(out double dCenterX, out double dCenterY)
        {
            dCenterX = 0;
            dCenterY = 0;

            dCenterX = ((double)OriginData.nMinX + (double)OriginData.nMaxX) / 2;   // MAP File 전체 행렬 기준의 X 중앙 값. (좌/상단 으로 인덱스의 시작을 이동하기 전)
            dCenterY = ((double)OriginData.nMinY + (double)OriginData.nMaxY) / 2;   // MAP File 전체 행렬 기준의 Y 중앙 값. (좌/상단 으로 인덱스의 시작을 이동하기 전)

            // MapFile에서 행렬의 개수는 0 부터 시작하기 때문에 행렬 개수는 +1을 해야 개수가 정확.
            // MapFile의 행과 열의 최대개수에서 최소 개수를 빼고 + 1을 하지 않는 경우, 행렬의 개수가 실제 개수보다 -1 개 작다.
            // 따라서 행렬의 실제 개수는 행렬의 최대인덱스 - 최소인덱스 + 1
            int cntCenterX = OriginData.nMaxX - OriginData.nMinX + 1;                   // + 1;   // 좌/상단 으로 인덱스의 시작을 이동. 인덱스 시작 숫자는 0
            int cntCenterY = OriginData.nMaxY - OriginData.nMinY + 1;                   // + 1;   // 좌/상단 으로 인덱스의 시작을 이동. 인덱스 시작 숫자는 0

            //double dT = dCenterX % 10;

            Debug.WriteLine($"Matrix - ROW count:{cntCenterX+1}, COLUMN Count:{cntCenterY+1}");

            if (cntCenterX % 2 != 0)    // 열의 총 개수가 짝수개 라면, 중심은 스크라이브에 있고, 홀수개라면 중심은 가운데 Die 중심.
            {
                OriginData.bScribX = false;   // Origin
                //OriginData.bScribX = true;      // 03.26 change
            }
            else
            {
                OriginData.bScribX = true;    // Origin
                //OriginData.bScribX = false;     // 03.26 change
            }

            if (cntCenterY % 2 != 0)    // 행의 총 개수가 홀수 라면, 중심은 스크라이브에 있고, 홀수개라면 중심은 가운데 Die 중심.
            {
                OriginData.bScribY = false;   // Origin
                //OriginData.bScribY = true;      // 03.26 Change
            }
            else
            {
                OriginData.bScribY = true;    // Origin
                //OriginData.bScribY = false;     // 03.26 Change
            }
        }

        public void Translation_Data(double degrees, int nGrade)
        {
            double angle = Math.PI * degrees / 180.0;   // Radian to Degree

            int sinAngle = (int)Math.Sin(angle);
            int cosAngle = (int)Math.Cos(angle);

            System.Drawing.Point poIndex = new System.Drawing.Point();
            PointF poValue = new PointF();

            poValue.X = 0;
            poValue.Y = 0;

            for (int i = 0; i < OriginData.nDieCnt; i++)
            {
                if (degrees != 0)
                {
                    OriginData.nTransX.Add((OriginData.nX[i] * cosAngle) - (OriginData.nY[i] * sinAngle) + OriginData.nMaxX);
                    OriginData.nTransY.Add((OriginData.nX[i] * sinAngle) + (OriginData.nY[i] * cosAngle) + OriginData.nMaxY);


                    poIndex.X = (OriginData.nX[i] * cosAngle) - (OriginData.nY[i] * sinAngle) + OriginData.nMaxX;
                    poIndex.Y = (OriginData.nX[i] * sinAngle) + (OriginData.nY[i] * cosAngle) + OriginData.nMaxY;
                    FoundPoint value = new FoundPoint
                    {
                        X = poValue.X,
                        Y = poValue.Y,
                        Status = 0
                    };
                    OriginPoint.Add(poIndex, value);
                }
                else
                {
                    OriginData.nTransX.Add((OriginData.nX[i] * cosAngle) - (OriginData.nY[i] * sinAngle) - OriginData.nMinX);
                    OriginData.nTransY.Add((OriginData.nX[i] * sinAngle) + (OriginData.nY[i] * cosAngle) - OriginData.nMinY);


                    poIndex.X = (OriginData.nX[i] * cosAngle) - (OriginData.nY[i] * sinAngle) - OriginData.nMinX;
                    poIndex.Y = (OriginData.nX[i] * sinAngle) + (OriginData.nY[i] * cosAngle) - OriginData.nMinY;
                    FoundPoint value = new FoundPoint
                    {
                        X = poValue.X,
                        Y = poValue.Y,
                        Status = 0
                    };
                    OriginPoint.Add(poIndex, value);
                }


                OriginData.nS.Add(0);       // Original Map File의 모든 Die 위치에 대한 Translation Data 만 만들기 때문에, State(nS)는 모두 0. 
            }

            // TransCenter
            if (degrees != 0)
            {
                OriginData.dTCenterX = (OriginData.dCenterX * cosAngle) - (OriginData.dCenterY * sinAngle) + OriginData.nMaxX;
                OriginData.dTCenterY = (OriginData.dCenterX * sinAngle) + (OriginData.dCenterY * cosAngle) + OriginData.nMaxY;
            }
            else
            {
                OriginData.dTCenterX = (OriginData.dCenterX * cosAngle) - (OriginData.dCenterY * sinAngle) - OriginData.nMinX;
                OriginData.dTCenterY = (OriginData.dCenterX * sinAngle) + (OriginData.dCenterY * cosAngle) - OriginData.nMinY;
            }

        }

        //public void SetModelPosToDictionary(double ModelPosX, double ModelPosY)
        /// <summary>
        /// 
        /// </summary>
        /// <param name="O_PosX">Model Finder Die Position World Position </param>
        /// <param name="O_PosY">Model Finder Die Position World Position </param>
        /// <param name="ModelPosX"> Translation Position X </param>
        /// <param name="ModelPosY"> Translation Position Y </param>
        /// <param name="MatrixPointX"> Die Translation Position X </param>
        /// <param name="MatrixPointY"> Die Translation Position Y </param>
        /// <param name="Status"> Die 존재 여부 값 (1: 존재, 0: 없음) </param>
        public void SetModelPosToDictionary(double O_PosX, double O_PosY, double ModelPosX, double ModelPosY, out int MatrixPointX, out int MatrixPointY, out int Status)
        {

            // Model Find로 찾은 위치값을 계산하여 Dictionary에 넣는다
            // 이 함수를 사용하기 전에 Dictionary에 Trans된 좌표값이 들어가 있어야 한다

            System.Drawing.Point poSearchDieIdx = new System.Drawing.Point();
            FoundPoint poMovingDieDist = new FoundPoint();      // jdhan

            //double dResolution = pMyParam.PixelToMM_Offset;     // 1-Pixel (um)
            double dResolution = pMyParam.PixelToUM_Offset;     // 1-Pixel (um) 02.14
            double dCenterX = pMyContext.dFoundCenterX;         // circle CenterX Pixel
            double dCenterY = pMyContext.dFoundCenterY;         // circle CenterY Pixel
            double dImgCenterX = (double)(pMyContext.ResultImage.Width) / 2.0;      // Grab Image CenterX
            double dImgCenterY = (double)(pMyContext.ResultImage.Height) / 2.0;     // Grab Image CenterY

            double Origin_PosX = O_PosX;
            double Origin_PosY = O_PosY;

            double dDistX = ModelPosX * 1000;   // mm to um
            double dDistY = ModelPosY * 1000;   // mm to um
            
            poMovingDieDist.X = Origin_PosX; //  mm ModelPosX is World Coordination 
            poMovingDieDist.Y = Origin_PosY; //  mm ModelPosY is World Coordination

            double dMovingDieCountX = Math.Abs(dDistX) / OriginData.dSizeX;
            double dMovingDieCountY = Math.Abs(dDistY) / OriginData.dSizeY;
            if (dDistX < 0)
            {
                dMovingDieCountX = dMovingDieCountX * -1;
            }
            if (dDistY < 0)
            {
                dMovingDieCountY = dMovingDieCountY * -1;
            }
            poSearchDieIdx.X = (int)Math.Round(OriginData.dTCenterX + dMovingDieCountX, 0);
            poSearchDieIdx.Y = (int)Math.Round(OriginData.dTCenterY + dMovingDieCountY, 0);

            // Dictionary 사용
            FoundPoint value = new FoundPoint
            {
                X = poMovingDieDist.X,
                Y = poMovingDieDist.Y,
                Status = 1
            };

            if (OriginPoint.ContainsKey(poSearchDieIdx))    // Key 값이 Dictionary에 있다면,
            {
                OriginPoint[poSearchDieIdx] = value;
                MatrixPointX = poSearchDieIdx.X;
                MatrixPointY = poSearchDieIdx.Y;
                Status = 1;
            }
            else
            {
                //MatrixPointX = 0;         // Origin 04.12
                //MatrixPointY = 0;         // Origin 04.12

                // Map File에는 없지만, 이미지에서 Model Finder의 Model 찾는 기준에 부합하는 영역의 행렬 인덱스 값을 넣어서 전달.
                // 맵 파일 행렬 인덱스의 대표 위치 값을 찾지 못하는 경우, 맵 파일이 표기 되지 않는 행렬의 값으로 대체하기 위해 사용하도록 수정 함.
                MatrixPointX = poSearchDieIdx.X;    // Change 04.12
                MatrixPointY = poSearchDieIdx.Y;    // change 04.12
                Status = 0;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="dieinfo"></param>
        /// <param name="MatrixPoint"> Translation Matrix index </param>
        /// <param name="ContourCount"></param>
        /// <param name="ContourArea"></param>
        /// <param name="Convex"></param>
        /// <param name="ApexCount"></param>
        /// <param name="moments"> Die Center </param>
        /// <param name="Die_Image"> Die Result Image </param>
        /// <returns></returns>
        private bool DieInspection(Found_Die_Info dieinfo, System.Drawing.Point MatrixPoint, ref int ContourCount, ref double ContourArea, ref bool Convex, ref int ApexCount, ref Moments moments, ref Mat Die_Image)   // 01.22 Die_Image 입력 파라메터 추가.
        {
            try
            {
                pMyContext.Die_Width_Ratio = pMyParam.Die_Width_Ratio;
                pMyContext.Die_Height_Ratio = pMyParam.Die_Height_Ratio;
                pMyContext.Die_Area_Ratio = pMyParam.Die_Area_Ratio;
                pMyContext.Binary_Threshold = pMyParam.Binary_Threshold;

                int x1 = 0;
                int x2 = 0;
                int y1 = 0;
                int y2 = 0;

                // 02.27 Insert Start
                int LX1 = 0;
                int LX2 = 0;
                int LY1 = 0;
                int LY2 = 0;
                // 02.27 Insert END


                double Scrib_X_um = pMyParam.Scrib_X * (pMyContext.MMPerPixel_X * 1000);   // 01.22 Insert
                double Scrib_Y_um = pMyParam.Scrib_Y * (pMyContext.MMPerPixel_Y * 1000);   // 01.22 Insert
                if (pMyParam.MapCheck == false)
                {
                    // 02.27 Insert Start
                    // Scrib Pixel 을 입력 받아 계산된 값으로 ROI 영역을 산출.
                    x1 = (int)(dieinfo.X_Pos - (((pMyParam.DieWidth + Scrib_X_um) / (pMyContext.MMPerPixel_X * 1000)) * pMyParam.Die_Width_Ratio / 2));
                    x2 = (int)(dieinfo.X_Pos + (((pMyParam.DieWidth + Scrib_X_um) / (pMyContext.MMPerPixel_X * 1000)) * pMyParam.Die_Width_Ratio / 2));
                    y1 = (int)(dieinfo.Y_Pos - (((pMyParam.DieHeight + Scrib_Y_um) / (pMyContext.MMPerPixel_Y * 1000)) * pMyParam.Die_Height_Ratio / 2));
                    y2 = (int)(dieinfo.Y_Pos + (((pMyParam.DieHeight + Scrib_Y_um) / (pMyContext.MMPerPixel_Y * 1000)) * pMyParam.Die_Height_Ratio / 2));

                    // Large ROI
                    LX1 = (int)(dieinfo.X_Pos - (((pMyParam.DieWidth + Scrib_X_um) / (pMyContext.MMPerPixel_X * 1000)) * (2 - pMyParam.Die_Width_Ratio) / 2));
                    LX2 = (int)(dieinfo.X_Pos + (((pMyParam.DieWidth + Scrib_X_um) / (pMyContext.MMPerPixel_X * 1000)) * (2 - pMyParam.Die_Width_Ratio) / 2));
                    LY1 = (int)(dieinfo.Y_Pos - (((pMyParam.DieHeight + Scrib_Y_um) / (pMyContext.MMPerPixel_Y * 1000)) * (2 - pMyParam.Die_Height_Ratio) / 2));
                    LY2 = (int)(dieinfo.Y_Pos + (((pMyParam.DieHeight + Scrib_Y_um) / (pMyContext.MMPerPixel_Y * 1000)) * (2 - pMyParam.Die_Height_Ratio) / 2));
                    // 02.27 Insert END
                }
                else
                {
                    x1 = (int)dieinfo.X_Pos - ((int)dieinfo.Width / 2);   // dieinfo.X_Pos는 ModelFinder에서 찾은 Die 중심값이므로, Die Width의 절반을 빼줘야 시작점.
                    x2 = (int)pMyParam.ModuleModel.Master.Width;
                    y1 = (int)dieinfo.Y_Pos - ((int)dieinfo.Height / 2);   // dieinfo.Y_Pos는 ModelFinder에서 찾은 Die 중심값이므로, Die Height의 절반을 빼줘야 시작점.
                    y2 = (int)pMyParam.ModuleModel.Master.Height;
                }

                // 01.18 insert Start
                OpenCvSharp.Rect roi, Lroi;
                if (pMyParam.MapCheck == false)
                {
                    roi = new OpenCvSharp.Rect(x1, y1, x2 - x1, y2 - y1);      // Pixel Coordination Origin
                    Lroi = new OpenCvSharp.Rect(LX1, LY1, LX2 - LX1, LY2 - LY1);      // 02.27 Insert Large ROI Pixel Coordination Origin
                }
                else
                {
                    roi = new OpenCvSharp.Rect(x1, y1, x2, y2);      // Pixel Coordination
                    Lroi = new OpenCvSharp.Rect(x1, y1, x2, y2);      // Pixel Coordination
                }
                // 01.18 insert End

                // 03.04 Insert Start
                double RadBoundary = 0.0;
                //RadBoundary = (pMyContext.dRadmm / 3) * 2;  // Radious * 2/3    // mm
                RadBoundary = (pMyContext.dRadius/ 3) * 2;  // Radious * 2/3    // pixel
                Debug.WriteLine($"{pMyContext.dRadius}, {RadBoundary}");
                double DistWidth = Math.Abs((GrayImage.Width / 2 - dieinfo.X_Pos));
                double DistHeight = Math.Abs((GrayImage.Height/ 2 - dieinfo.Y_Pos));
                double DistDietoCenterPoint = Math.Sqrt(DistWidth * DistWidth + DistHeight * DistHeight);
                Debug.WriteLine($"{DistWidth}, {DistHeight}, {DistDietoCenterPoint}");
                // 03.04 Insert End

                // 02.27 Insert Start
                Mat dieRegion = null;
                Mat LdieRegion = null;

                if (pMyParam.Mophology == false)
                {
                    dieRegion = new Mat(GrayImage, roi);    // Origin Image 02.27
                    LdieRegion = new Mat(GrayImage, Lroi);  // Origin Image 02.27
                }
                else
                {
                    dieRegion = new Mat(MophologyImage, roi);   // Inner Die Areae Mophology 02.27

                    // 03.04 Insert Start
                    // Wafer Circle Radious 의 2/3 보다 크면 Mophology Image를 Large Roi로 사용하고,
                    // 작다면 GrayImage(원본)를 Large ROI로 사용하여 검사진행.
                    if(DistDietoCenterPoint <= RadBoundary)
                        LdieRegion = new Mat(GrayImage, Lroi);  // Origin Image 02.27
                    else
                        LdieRegion = new Mat(MophologyImage, Lroi);    // Origin Image 02.27
                    // 03.04 Insert End
                }
                // 02.27 Insert End

                double area_threshold = (pMyParam.DieWidth / (pMyContext.MMPerPixel_X * 1000)) * (pMyParam.DieHeight / (pMyContext.MMPerPixel_Y * 1000));   // Standard Mode Die 면적.
                double area_threshold_min = (pMyParam.DieWidth / (pMyContext.MMPerPixel_X * 1000)) * (pMyParam.DieHeight / (pMyContext.MMPerPixel_Y * 1000)) * pMyParam.Die_Area_Ratio; // Die 면적의 Parameter ratio % (minimum)
                double area_threshold_max = (pMyParam.DieWidth / (pMyContext.MMPerPixel_X * 1000)) * (pMyParam.DieHeight / (pMyContext.MMPerPixel_Y * 1000)) * (2.0 - pMyParam.Die_Area_Ratio); // Die 면적의 Parameter ratio % (maximum)
                // 02.07 change start
                pMyParam.Die_Area = area_threshold; // Standard Die Area

                Debug.Indent();
                Debug.WriteLine($"Inspection Area Min spec:{area_threshold_min}");
                Debug.WriteLine($"Inspection Area Max spec:{area_threshold_max}");
                Debug.Unindent();


                string strMapPath = null;
                string strFileName = null;
                bool resultSBool = false;
                bool resultLBool = false;
                bool BothBool = false;

                Mat TempImage = null;
                int TempContourCount = 0;
                double TempContourArea = 0;
                bool TempConvex = false;
                int TempApexCount = 0;
                Moments TempMoments = null;

                System.Drawing.Point MPoint = MatrixPoint;

                if(pMyParam.InspectionROI == WaferScanInspectionParam.Die_ROI.InnerROI)
                {
                    pMyParam.InitROI = (int)pMyParam.InspectionROI; // insert 03.04
                    if(ProcessInspection(dieRegion, MPoint, ref TempImage, ref TempContourCount, ref TempContourArea, ref TempConvex, ref TempApexCount, ref TempMoments) == true)
                    {
                        Die_Image = TempImage.Clone();

                        ContourCount = TempContourCount;
                        ContourArea = TempContourArea;
                        Convex = TempConvex;
                        ApexCount = TempApexCount;
                        moments = TempMoments;

                        //if ((pMyParam.MapCheck == false) && (ContourCount == 1) && (ContourArea >= area_threshold_min) && (ContourArea <= area_threshold_max) && (Convex == false) && (ApexCount == 4)) // Delete new_contours.Count  02.27
                        if ((pMyParam.MapCheck == false) && (ContourCount == 1) && (ContourArea >= (roi.Width * roi.Height * 0.7)) && (Convex == false) && (ApexCount == 4)) // Delete new_contours.Count  02.27
                            resultSBool = true;
                        else
                            resultSBool = false;

                        // Saved Inner Die Image 
                        strMapPath = Path.Combine(SystemHandler.Handle.Setting.ImageSavePath,
                                String.Format("{0:0000}{1:00}{2:00}/{3}", DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, pMyParam.MapName));
                        if (Directory.Exists(strMapPath) == false)
                            Directory.CreateDirectory(strMapPath);
                        strFileName = Path.Combine(strMapPath, String.Format("{0}_{1}_Image.png", MPoint.X, MPoint.Y)); //BMP -> png
                        Cv2.ImWrite(strFileName, Die_Image);

                        return resultSBool;
                    }
                    else
                    {
                        Die_Image = TempImage.Clone();

                        ContourCount = TempContourCount;
                        ContourArea = TempContourArea;
                        Convex = TempConvex;
                        ApexCount = TempApexCount;
                        moments = TempMoments;

                        return false;
                    }
                }
                else if (pMyParam.InspectionROI == WaferScanInspectionParam.Die_ROI.OuterROI)
                {
                    pMyParam.InitROI = (int)pMyParam.InspectionROI; // insert 03.04
                    if (ProcessInspection(LdieRegion, MPoint, ref TempImage, ref TempContourCount, ref TempContourArea, ref TempConvex, ref TempApexCount, ref TempMoments) == true)
                    {
                        Die_Image = TempImage.Clone();

                        ContourCount = TempContourCount;
                        ContourArea = TempContourArea;
                        Convex = TempConvex;
                        ApexCount = TempApexCount;
                        moments = TempMoments;

                        if ((pMyParam.MapCheck == false) && (ContourArea >= area_threshold_min) && (ContourArea <= area_threshold_max) && (Convex == false) && (ApexCount == 4)) // Delete new_contours.Count  02.27
                            resultLBool = true;
                        else
                            resultLBool = false;

                        // Saved Outer Die Image
                        strMapPath = Path.Combine(SystemHandler.Handle.Setting.ImageSavePath,
                                String.Format("{0:0000}{1:00}{2:00}/{3}", DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, pMyParam.MapName));
                        if (Directory.Exists(strMapPath) == false)
                            Directory.CreateDirectory(strMapPath);
                        strFileName = Path.Combine(strMapPath, String.Format("{0}_{1}_Image.png", MPoint.X, MPoint.Y)); // bmp -> png
                        Cv2.ImWrite(strFileName, LdieRegion);

                        return resultLBool;
                    }
                    else
                    {
                        Die_Image = TempImage.Clone();

                        ContourCount = TempContourCount;
                        ContourArea = TempContourArea;
                        Convex = TempConvex;
                        ApexCount = TempApexCount;
                        moments = TempMoments;

                        return false;
                    }
                }
                else if (pMyParam.InspectionROI == WaferScanInspectionParam.Die_ROI.Both)    // Both
                {
                    pMyParam.InitROI = (int)pMyParam.InspectionROI; // insert 03.04
                    // fist Small ROI Inspection
                    if(ProcessInspection(dieRegion, MPoint, ref TempImage, ref TempContourCount, ref TempContourArea, ref TempConvex, ref TempApexCount, ref TempMoments) == true)
                    {
                        ContourCount = TempContourCount;
                        ContourArea = TempContourArea;
                        Convex = TempConvex;
                        ApexCount = TempApexCount;
                        moments = TempMoments;

                        if ((pMyParam.MapCheck == false) && (ContourCount == 1) && (ContourArea >= area_threshold_min) && (ContourArea <= area_threshold_max) && (Convex == false) && (ApexCount == 4)) // Delete new_contours.Count  02.27
                            resultSBool = true;
                        else
                            resultSBool = false;
                    }
                    if(resultSBool == false)
                    {
                        BothBool = false;
                        Die_Image = TempImage.Clone();

                        ContourCount = TempContourCount;
                        ContourArea = TempContourArea;
                        Convex = TempConvex;
                        ApexCount = TempApexCount;
                        moments = TempMoments;

                        // Saved Inner Die Image
                        strMapPath = Path.Combine(SystemHandler.Handle.Setting.ImageSavePath,
                                String.Format("{0:0000}{1:00}{2:00}/{3}", DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, pMyParam.MapName));
                        if (Directory.Exists(strMapPath) == false)
                            Directory.CreateDirectory(strMapPath);
                        strFileName = Path.Combine(strMapPath, String.Format("{0}_{1}_Image.png", MPoint.X, MPoint.Y)); // bmp -> png
                        Cv2.ImWrite(strFileName, dieRegion);

                        return BothBool;
                    }
                    // Large ROI Inspection
                    if ((resultSBool == true) && (ProcessInspection(LdieRegion, MPoint, ref TempImage, ref TempContourCount, ref TempContourArea, ref TempConvex, ref TempApexCount, ref TempMoments) == true))
                    {
                        ContourCount = TempContourCount;
                        ContourArea = TempContourArea;
                        Convex = TempConvex;
                        ApexCount = TempApexCount;
                        moments = TempMoments;

                        if ((pMyParam.MapCheck == false) && (ContourArea >= area_threshold_min) && (ContourArea <= area_threshold_max) && (Convex == false) && (ApexCount == 4)) // Delete new_contours.Count  02.27
                            resultLBool = true;
                        else
                            resultLBool = false;
                    }
                    if ((resultSBool == true) && (resultLBool == true))
                    {
                        BothBool = true;
                        Die_Image = TempImage.Clone();

                        ContourCount = TempContourCount;
                        ContourArea = TempContourArea;
                        Convex = TempConvex;
                        ApexCount = TempApexCount;
                        moments = TempMoments;

                        // Saved Outer Die Image
                        strMapPath = Path.Combine(SystemHandler.Handle.Setting.ImageSavePath,
                                String.Format("{0:0000}{1:00}{2:00}/{3}", DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, pMyParam.MapName));
                        if (Directory.Exists(strMapPath) == false)
                            Directory.CreateDirectory(strMapPath);
                        strFileName = Path.Combine(strMapPath, String.Format("{0}_{1}_Image.png", MPoint.X, MPoint.Y)); // bmp -> png
                        Cv2.ImWrite(strFileName, LdieRegion);

                        return BothBool;
                    }
                    else if ((resultSBool == true) && (resultLBool == false))
                    {
                        BothBool = false;
                        Die_Image = TempImage.Clone();

                        ContourCount = TempContourCount;
                        ContourArea = TempContourArea;
                        Convex = TempConvex;
                        ApexCount = TempApexCount;
                        moments = TempMoments;

                        // Saved Error Die Image
                        strMapPath = Path.Combine(SystemHandler.Handle.Setting.ImageSavePath,
                                String.Format("{0:0000}{1:00}{2:00}/{3}", DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, pMyParam.MapName));
                        if (Directory.Exists(strMapPath) == false)
                            Directory.CreateDirectory(strMapPath);
                        strFileName = Path.Combine(strMapPath, String.Format("{0}_{1}_Image.png", MPoint.X, MPoint.Y)); // bmp -> png
                        Cv2.ImWrite(strFileName, LdieRegion);

                        return BothBool;
                    }
                }
                else
                {
                    pMyParam.InitROI = (int)pMyParam.InspectionROI; // insert 03.04
                    Die_Image = null;

                    ContourCount = 0;
                    ContourArea = 0;
                    Convex = false;
                    ApexCount = 0;
                    moments = null;

                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                Logging.PrintErrLog((int)ELogType.Error, $"Die Inspection Error. Exception:{ex.Message}");
                Logging.PrintErrLog((int)ELogType.Error, $"X:{MatrixPoint.X}, Y:{MatrixPoint.Y}");

                Die_Image = null;

                ContourCount = 0;
                ContourArea = 0;
                Convex = false;
                ApexCount = 0;
                moments = null;

                return false;
            }
        }

        /// <summary>
        /// Insert Data : 02.27 Small ROI and Large ROI Inspection
        /// </summary>
        /// <param name="RoiImage"> Die Inner ROI or Die Outer ROI </param>
        /// <param name="MatrixPoint"> Die Index value </param>
        /// <param name="ResultImage"> Inspection Image </param>
        /// <param name="ResultContourCount"> Inspection Contours Count </param>
        /// <param name="ResultContourArea"> Inspection Contours Area </param>
        /// <param name="ResultConvex"> Inspection Convex </param>
        /// <param name="ResultApexCount"> inspection ApexCount</param>
        /// <param name="ResultMoments"> Inspection moments </param>
        /// <returns></returns>
        public bool ProcessInspection(Mat RoiImage, System.Drawing.Point MatrixPoint, ref Mat ResultImage, 
            ref int ResultContourCount, ref double ResultContourArea, ref bool ResultConvex, ref int ResultApexCount, ref Moments ResultMoments)
        {
            Mat binary = new Mat();
            Cv2.Threshold(RoiImage, binary, pMyParam.Binary_Threshold, 255, ThresholdTypes.Binary);
            Cv2.CvtColor(RoiImage, RoiImage, ColorConversionCodes.GRAY2BGR);

            Mat inv = new Mat();
            Cv2.BitwiseNot(binary, inv);
            OpenCvSharp.Point[][] contours;
            HierarchyIndex[] hierarchy;
            Cv2.FindContours(inv, out contours, out hierarchy, RetrievalModes.List, ContourApproximationModes.ApproxNone);

            if (contours.Length == 0)
            {
                Cv2.PutText(RoiImage, "Not Found Contours", new OpenCvSharp.Point(RoiImage.Width / 2, RoiImage.Height / 2), HersheyFonts.HersheyComplex, 0.25, Scalar.Red, 1, LineTypes.AntiAlias);
                ResultImage = RoiImage.Clone(); // 02.21 Insert
                Logging.PrintErrLog((int)ELogType.Error, $"Not Found Controus Length:{contours.Length}, X:{MatrixPoint.X}, Y:{MatrixPoint.Y}");

                ResultContourCount = 0;
                ResultContourArea = 0;
                ResultConvex = false;
                ResultApexCount = 0;
                ResultMoments = null;

                return false; // 01.18 insert Exception 발생 포인트.
            }

            double area_threshold = (pMyParam.DieWidth / (pMyContext.MMPerPixel_X * 1000)) * (pMyParam.DieHeight / (pMyContext.MMPerPixel_Y * 1000)); // 실제 Die의 면적

            double area_threshold_min = (pMyParam.DieWidth / (pMyContext.MMPerPixel_X * 1000)) * (pMyParam.DieHeight / (pMyContext.MMPerPixel_Y * 1000)) * pMyParam.Die_Area_Ratio; // Die 면적의 70%
            double area_threshold_max = (pMyParam.DieWidth / (pMyContext.MMPerPixel_X * 1000)) * (pMyParam.DieHeight / (pMyContext.MMPerPixel_Y * 1000)) * (2.0 - pMyParam.Die_Area_Ratio); // Die 면적의 130%
                                                                                                                                                                                            // 02.07 change start
            pMyParam.Die_Area = area_threshold; // Standard Real Die Area

            if (pMyParam.MapCheck)
            {
                area_threshold = pMyParam.ModuleModel.Master.Width * pMyParam.ModuleModel.Master.Height * pMyParam.Die_Area_Ratio;
            }

            double area = 0;
            //Moments moments;  //01.22 DieInspection 함수의 인자로 Moments ref 변수를 입력 받아 처리하도록 수정하여 주석 처리함.
            bool convex = false;
            OpenCvSharp.Point[] approx = new OpenCvSharp.Point[10];

            List<OpenCvSharp.Point[]> new_contours = new List<OpenCvSharp.Point[]>();
            foreach (OpenCvSharp.Point[] p in contours)
            {
                double length = Cv2.ArcLength(p, true);

                // 01.22 Min_ArchLength 값 기준으로 변경. (OnLoad 함수에서 Min_ArcLength 값을 계산하도록 추가함)
                if (length > pMyParam.Min_ArcLength)
                {
                    new_contours.Add(p);  // origin
                                          //new_contours.Add(Cv2.ApproxPolyDP(p, length * 0.01, true));    // 02.27
                }
            }

            // 01.20 insert Start
            if (new_contours.Count == 0)
            {
                Cv2.PutText(RoiImage, "Not Found Bound Contours", new OpenCvSharp.Point(RoiImage.Width / 2, RoiImage.Height / 2), HersheyFonts.HersheyComplex, 0.25, Scalar.Red, 1, LineTypes.AntiAlias);
                ResultImage = RoiImage.Clone(); // 02.21 Insert

                // 02.20 comment : pMyParam.Min_ArcLength 보다 큰 값이 없는 경우, 해당 Matrix point의 이미지 처리가 필요해 보임.
                Logging.PrintErrLog((int)ELogType.Error, $"Not Found New Contours count:{new_contours.Count}, X:{MatrixPoint.X}, Y:{MatrixPoint.Y}");

                ResultContourCount = 0;
                ResultContourArea = 0;
                ResultConvex = false;
                ResultApexCount = 0;
                ResultMoments = null;

                return false; // 01.18 insert Exception 발생 포인트.
            }

            #region Max_Contours_Length Start
            int maxIndex = -1;
            double maxArea = 0.0;
            for (int i = 0; i < new_contours.Count; i++)
            {
                double Area = Cv2.ContourArea(new_contours.ElementAt(i), false);
                if (Area > maxArea)
                {
                    maxArea = Area;
                    maxIndex = i;
                }
            }
            area = Cv2.ContourArea(new_contours.ElementAt(maxIndex), false);
            ResultMoments = Cv2.Moments(new_contours.ElementAt(maxIndex), false);
            ResultConvex = Cv2.IsContourConvex(new_contours.ElementAt(maxIndex));                       // insert jdhan 02.27 주석 처리.
            // Epsilon value (근사치 정확도)가 낮을 수록 정밀하게 근사화 하며, 높을 수록 정밀도가 낮아 짐.
            // 근사치 정확도는 입력된 다각형(윤곽선)과 반환될 근사화된 다각형 사이의 최대 편차 간격을 의미. (근사치 정확도의 값이 낮을 수록, 근사를 더 적게해 원본 윤곽과 유사)
            approx = Cv2.ApproxPolyDP(new_contours.ElementAt(maxIndex), Cv2.ArcLength(new_contours.ElementAt(maxIndex), false) * 0.010, true);    // insert jdhan 01.22 Epsilong value 수정. (0.002 -> 0.005)
            Cv2.Circle(RoiImage, (int)(ResultMoments.M10 / ResultMoments.M00), (int)(ResultMoments.M01 / ResultMoments.M00), 5, Scalar.Red, -1);       // insert jdhan  10 -> 5
            Cv2.DrawContours(RoiImage, new_contours, maxIndex, new Scalar(0, 0, 255), 1, LineTypes.AntiAlias, null, 1);             // insert jdhan
            #endregion Max_Controu_Length End

            ResultImage = RoiImage.Clone();

            ResultContourArea = area;
            ResultContourCount = new_contours.Count;
            ResultApexCount = approx.Length;

            return true;
        }

        // Rotation Matrix 12.19
        public void RotationMatrix(double angle, double X, double Y, ref double RX, ref double RY)
        {
            double radian = angle * (float)(Math.PI / 180);
            RX = Math.Cos(radian) * (X) - Math.Sin(radian) * (Y);
            RY = Math.Sin(radian) * (X) + Math.Cos(radian) * (Y);
        }

        private void MapSearch()
        {
            double dSearchX = 0, dSearchY = 0;
            double cSearchX = 0, cSearchY = 0;
            double oSearchX = 0, oSearchY = 0;

            VM_COLS.Clear();
            VM_ROWS.Clear();

            VM_COLSW.Clear();
            VM_ROWSW.Clear();

            for (int i = 0; i < ModelResult.nCnt; i++)
            {
                Found_Die_Info temp = new Found_Die_Info();
                System.Drawing.Point MatrixPoint = new System.Drawing.Point();
                int MatrixPointX = 0;
                int MatrixPointY = 0;
                int Status = 0;

                pMyContext.dFoundModelX[i] = ModelResult.dXPos[i];  // Pixel-X
                pMyContext.dFoundModelY[i] = ModelResult.dYPos[i];  // Pixel-Y

                oSearchX = pMyContext.dFoundModelXWorld[i] = ModelResult.dXWorldPos[i];   // mm-X World 좌표계
                oSearchY = pMyContext.dFoundModelYWorld[i] = ModelResult.dYWorldPos[i];   // mm-Y World 좌표계

                // 01.31 Insert Start
                // Wafer 중심을 기준으로 Die 좌표들을 계산하기때문에, Circle Finder에서 찾은 웨이퍼 중심 값을 화면 중심으로 이동하여야 함.
                cSearchX = pMyContext.dFoundModelXWorld[i] = ModelResult.dXWorldPos[i] + pMyContext.CenterOffsetXmm;   // mm-X World 좌표계
                cSearchY = pMyContext.dFoundModelYWorld[i] = ModelResult.dYWorldPos[i] + pMyContext.CenterOffsetYmm;   // mm-Y World 좌표계
                //RotationMatrix(ModelResult.dAngle[i], cSearchX, cSearchY, ref dSearchX, ref dSearchY);
                RotationMatrix(pMyContext.dAngle, cSearchX, cSearchY, ref dSearchX, ref dSearchY);
                // 01.31 Insert End


                pMyContext.dFoundModelAngle[i] = ModelResult.dAngle[i];
                pMyContext.dFoundModelScore[i] = ModelResult.dScore[i];
                pMyContext.dFoundModelWidth[i] = pMyParam.ModuleModel.Master.Width;         // ModelFinder에서 지정한 Module Recipe의 Width
                pMyContext.dFoundModelHeight[i] = pMyParam.ModuleModel.Master.Height;       // ModelFinder에서 지정한 Module Recipe의 Height

                SetModelPosToDictionary(oSearchX, oSearchY, dSearchX, dSearchY, out MatrixPointX, out MatrixPointY, out Status);

                // Mapfile에는 존재하는 Die 이지만, ModelFinder 에서 찾지 못한 Die 들을 찾아서 좌표 저장.
                //if (Status != 0)  // Origin 04.12
                if (Status != 1)    // Change 04.12
                {
                    // 04.02 Insert Start
                    // Virtual Map에 사용하기 위한 각 행과 각 열에 대한 대표 좌표를 저장. (pixel)
                    if (VM_COLS.ContainsKey(MatrixPointX) == false)
                    {
                        VM_COLS.Add(MatrixPointX, ModelResult.dXPos[i]);
                        VM_COLSW.Add(MatrixPointX, ModelResult.dXWorldPos[i]);
                    }
                    if (VM_ROWS.ContainsKey(MatrixPointY) == false)
                    {
                        VM_ROWS.Add(MatrixPointY, ModelResult.dYPos[i]);
                        VM_ROWSW.Add(MatrixPointY, ModelResult.dYWorldPos[i]);
                    }
                    // 04.02 Insert End
                }

                // 04.12 Insert Start
                //else
                //{
                //    if (VM_COLS.ContainsKey(MatrixPointX) == false)
                //    {
                //        VM_COLS.Add(MatrixPointX, ModelResult.dXPos[i]);
                //        VM_COLSW.Add(MatrixPointX, ModelResult.dXWorldPos[i]);
                //    }
                //    if (VM_ROWS.ContainsKey(MatrixPointY) == false)
                //    {
                //        VM_ROWS.Add(MatrixPointY, ModelResult.dYPos[i]);
                //        VM_ROWSW.Add(MatrixPointY, ModelResult.dYWorldPos[i]);
                //    }
                //}
                // 04.12 Insert End
                

                // 개별 Die의 이미지를 crob 하기 위해 Model Find 에서 찾은 이미지 정보를 Found_Die에 저장.
                if (Status == 1)
                {
                    temp.X_Pos = ModelResult.dXPos[i];  // Pixel X Position
                    temp.Y_Pos = ModelResult.dYPos[i];  // Pixel Y Position
                    temp.WX_Pos = ModelResult.dXWorldPos[i]; //World X Position
                    temp.WY_Pos = ModelResult.dYWorldPos[i]; //World Y Position
                    temp.Width = pMyParam.ModuleModel.Master.Width;
                    temp.Height = pMyParam.ModuleModel.Master.Height;
                    temp.Angle = ModelResult.dAngle[i];

                    MatrixPoint.X = MatrixPointX;
                    MatrixPoint.Y = MatrixPointY;

                    // 04.02 Insert Start
                    // Virtual Map에 사용하기 위한 행과 열에 대한 좌표를 저장.
                    if (VM_COLS.ContainsKey(MatrixPointX) == false)
                    {
                        VM_COLS.Add(MatrixPointX, temp.X_Pos);
                        VM_COLSW.Add(MatrixPointX, temp.WX_Pos);
                    }
                    if (VM_ROWS.ContainsKey(MatrixPointY) == false)
                    {
                        VM_ROWS.Add(MatrixPointY, temp.Y_Pos);
                        VM_ROWSW.Add(MatrixPointY, temp.WY_Pos);
                    }
                    // 04.02 Insert End

                    int ContourCount = 0;
                    double ContourArea = 0;
                    bool Convex = false;
                    int ApexCount = 0;

                    Moments moments = null; // 01.22 insert
                    Mat Die_Image = null;   // 01.20 insert

                    //Die Inspection
                    if (DieInspection(temp, MatrixPoint, ref ContourCount, ref ContourArea, ref Convex, ref ApexCount, ref moments, ref Die_Image) == false) // 01.22 insert
                    {
                        FoundPoint value = OriginPoint[MatrixPoint];

                        value.Status = 2;
                        OriginPoint[MatrixPoint] = value;
                    }
                    else
                    {
                        FoundPoint value = OriginPoint[MatrixPoint];

                        value.Status = 1;
                        OriginPoint[MatrixPoint] = value;
                    }

                    temp.ContourCount = ContourCount;
                    temp.Area = ContourArea;
                    temp.Convex = Convex;
                    temp.Apex = ApexCount;

                    temp.moments = moments;     // 01.22 Insert

                    temp.Die_Image = Die_Image; // 01.20 Insert

                    try
                    {
                        if (pMyContext.Found_Die.ContainsKey(MatrixPoint) == false)
                            pMyContext.Found_Die.Add(MatrixPoint, temp);
                        else
                            Debug.WriteLine($"MapSearch Function Dictionary Key Error:{MatrixPoint.X},{MatrixPoint.Y}");
                    }
                    catch (Exception e)
                    {
                        Logging.PrintErrLog((int)ELogType.Error, $"MapSearch Function FoundDie Key Exception:{e.Message}");
                        Debug.WriteLine($"MapSearch Found Die Exception:{e.ToString()}");
                    }
                }
            }
        }


        public int MapfileSave()
        {
            // 맵 파일 해당 인덱스에 위치 좌표값 추가
            string mapIndexFile = Path.Combine(SystemHandler.Handle.Setting.MapDataSavePath, pMyParam.MapName);
            using (StreamWriter writer = new StreamWriter(mapIndexFile))
            {
                // Header
                for (int i = 0; i < strHeaderLines.Count; i++)
                {
                    string line = strHeaderLines[i];
                    writer.WriteLine(line);
                }

                // Body
                System.Drawing.Point poTrans = new System.Drawing.Point();
                pMyContext.MapData.Clear();
                nMaxCol = -100;
                nMaxRow = -100;
                nMinCol = 100;
                nMinRow = 100;
                nTotalBin = 0;
                //for (int i = 0; i < OriginData.nTransX.Count; i++)    // 03.28 주석 처리
                for (int i = 0; i < OriginData.nDieCnt; i++)            // Map File 에 표시된 Die 총 개수
                {
                    string line;
                    poTrans.X = OriginData.nTransX[i];
                    poTrans.Y = OriginData.nTransY[i];

                    #region Virtual Map Image Position and Inspection Start
                    // 03.28 insert Start
                    // MapFile의 Bin 데이터가 18번이고, 행렬이동한 인덱스의 Die 검사 상태가 0(Die를 못찾은 경우)이라면,
                    //if ((OriginData.nB[i] == pMyParam.Die_Grade) && (OriginPoint[poTrans].Status == 0))
                    if (((OriginData.nB[i] == pMyParam.Die_Grade) || (OriginData.nB[i] == pMyParam.Die_GradeS)) && (OriginPoint[poTrans].Status == 0))
                    {
                        double ScreenCenterX = GrayImage.Width / 2;     // Screen Center X Pixel
                        double ScreenCenterY = GrayImage.Height / 2;    // Screen Center Y Pixel

                        // dist_X, dist_Y 는 찾으려고 하는 Die를 Circle의 중심에서 Screen 중심까지 떨어진 거리이며 픽셀 값으로 변환.
                        double dist_X = 0.0;
                        double dist_Y = 0.0;

                        /*
                        // (pMyContext.CenterOffsetXmm & pMyContext.CenterOffsetYmm) 는 Circle의 중심이 ScreenCenter 로 이동해야하는 부호의 거리 값을 가짐.
                        // VMdataList 의 좌표 값들은 Circle Finder 에서 찾은 웨이퍼 중심 부터의 거리로 산출한 값.
                        // 이미지 영상은 Calibration되어 Screen 중심 좌표가 (0, 0)이며, 이미지 그랩을 위해서는 이미지의 중심(Width/2, Height/2) 값 기준으로
                        // VMdataList의 값들이 변환되어야 한다.
                        if (VMdataList[i].XP < 0)       // Die 가 왼쪽에 있는 경우
                        {
                            if(pMyContext.CenterOffsetXmm < 0)  // circle 중심이 오른쪽에 있음.
                                //dist_X = (VMdataList[i].XP - pMyContext.CenterOffsetXmm) / pMyContext.MMPerPixel_X;
                                dist_X = (VMdataList[i].XP - pMyContext.CenterOffsetXmm) / pMyContext.MMPerPixel_X;
                            else
                                dist_X = (VMdataList[i].XP + pMyContext.CenterOffsetXmm) / pMyContext.MMPerPixel_X;
                        }
                        else
                        {
                            if(pMyContext.CenterOffsetXmm < 0)
                                dist_X = (VMdataList[i].XP + pMyContext.CenterOffsetXmm) / pMyContext.MMPerPixel_X;
                            else
                                dist_X = (VMdataList[i].XP - pMyContext.CenterOffsetXmm) / pMyContext.MMPerPixel_X;
                        }

                        // circle 중심의 VMdataList의 Y좌표가 0 보다 작다면,
                        if (VMdataList[i].YP < 0)
                        {
                            // pMycontext.CenterOffsetYmm 는 Circle 중심 Y 좌표가 화면 중심 Y좌표로 이동해야 하는 값을 가진다.(부호 주의)
                            if(pMyContext.CenterOffsetYmm < 0)
                                dist_Y = (VMdataList[i].YP - pMyContext.CenterOffsetYmm)/pMyContext.MMPerPixel_Y;
                            else
                                dist_Y = (VMdataList[i].YP + pMyContext.CenterOffsetYmm)/pMyContext.MMPerPixel_Y;
                        }
                        else
                        {
                            if(pMyContext.CenterOffsetYmm < 0)
                                dist_Y = (VMdataList[i].YP + pMyContext.CenterOffsetYmm)/pMyContext.MMPerPixel_Y;
                            else
                                dist_Y = (VMdataList[i].YP - pMyContext.CenterOffsetYmm)/pMyContext.MMPerPixel_Y;
                        }
                        */



                        //if ((VMdataList[i].XP < pMyContext.dFoundCenterXWorld) && (pMyContext.CenterOffsetXmm < 0))
                        //    dist_X = (VMdataList[i].XP + pMyContext.CenterOffsetXmm) / pMyContext.MMPerPixel_X;
                        //else if ((VMdataList[i].XP > pMyContext.dFoundCenterXWorld) && (pMyContext.CenterOffsetXmm < 0))
                        //    dist_X = (VMdataList[i].XP + pMyContext.CenterOffsetXmm) / pMyContext.MMPerPixel_X;
                        //else if ((VMdataList[i].XP < pMyContext.dFoundCenterXWorld) && (pMyContext.CenterOffsetXmm > 0))
                        //    dist_X = (VMdataList[i].XP + pMyContext.CenterOffsetXmm) / pMyContext.MMPerPixel_X;
                        //else if ((VMdataList[i].XP > pMyContext.dFoundCenterXWorld) && (pMyContext.CenterOffsetXmm > 0))
                        //    dist_X = (VMdataList[i].XP + pMyContext.CenterOffsetXmm) / pMyContext.MMPerPixel_X;

                        //if (VMdataList[i].XP != pMyContext.dFoundCenterXWorld)
                        //    dist_X = (VMdataList[i].XP + pMyContext.CenterOffsetXmm) / pMyContext.MMPerPixel_X;
                        //else
                        //    dist_X = VMdataList[i].XP / pMyContext.MMPerPixel_X;




                        //if ((VMdataList[i].YP < pMyContext.dFoundCenterYWorld) && (pMyContext.CenterOffsetYmm < 0))
                        //    dist_Y = (VMdataList[i].YP + pMyContext.CenterOffsetYmm) / pMyContext.MMPerPixel_Y;
                        //else if ((VMdataList[i].YP > pMyContext.dFoundCenterYWorld) && (pMyContext.CenterOffsetYmm < 0))
                        //    dist_Y = (VMdataList[i].YP - pMyContext.CenterOffsetYmm) / pMyContext.MMPerPixel_Y;
                        //else if ((VMdataList[i].YP < pMyContext.dFoundCenterYWorld) && (pMyContext.CenterOffsetYmm > 0))
                        //    dist_Y = (VMdataList[i].YP - pMyContext.CenterOffsetYmm) / pMyContext.MMPerPixel_Y;
                        //else if ((VMdataList[i].YP > pMyContext.dFoundCenterYWorld) && (pMyContext.CenterOffsetYmm > 0))
                        //    dist_Y = (VMdataList[i].YP - pMyContext.CenterOffsetYmm) / pMyContext.MMPerPixel_Y;

                        //else
                        //    dist_Y = VMdataList[i].YP / pMyContext.MMPerPixel_Y;

                        dist_X = VMdataList[i].XP / pMyContext.MMPerPixel_X;
                        dist_Y = VMdataList[i].YP / pMyContext.MMPerPixel_Y;

                        //if((pMyContext.CenterOffsetYmm < 0) && (VMdataList[i].YP < pMyContext.dFoundCenterYWorld))
                        //    dist_Y = (VMdataList[i].YP + pMyContext.CenterOffsetYmm) / pMyContext.MMPerPixel_Y;
                        //else
                        //    dist_Y = (VMdataList[i].YP - pMyContext.CenterOffsetYmm) / pMyContext.MMPerPixel_Y;




                        double VMDie_CenterX = 0;   // Virtual Die Center X, Pixel Data
                        double VMDie_CenterY = 0;   // Virtual Die Center Y, Pixel Data

                        #region VM_Dictionary Found Die Start
                        /*
                        if (VM_COLS.ContainsKey(poTrans.X) == true)
                        {
                            // VM Dictionary 에 해당 Key가 존재한다면, Value 값을 가져온다.
                            VM_COLS.TryGetValue(poTrans.X, out VMDie_CenterX);
                        }
                        else
                        {
                            // VM Dictionary 에 해당 Key가 존재하지 않는다면,
                            for (int j = 0; j < (OriginData.nMaxX - OriginData.nMinX + 1); j++)
                            {
                                // Colums 방향 VM Key가 있는 어디든 해당 key가 있다면, 
                                if (VM_COLS.ContainsKey(j) == true)
                                {
                                    double temp_X = 0.0;
                                    // 해당 Colum 값을 가져 온다.
                                    VM_COLS.TryGetValue(j, out temp_X);

                                    // 만약 찾고자하는 Colum이 값을 가진 Colum에 해당하는 j 보다 작다면, 왼쪽에 존재 하므로
                                    if (poTrans.X < j)
                                    {
                                        // 값이 있는 Colum 위치와 찾고자 하는 Colum 위치의 차이를 구하고, 차이 값은 Die Width 값을 곱하여 찾은 Colum 값에서 빼주면 해당 위치 값이 된다.
                                        //VMDie_CenterX = temp_X - (Math.Abs(j - poTrans.X) * (pMyParam.DieWidth + pMyParam.Scrib_X * (pMyContext.MMPerPixel_X * 1000)) / (pMyContext.MMPerPixel_X * 1000));

                                        VMDie_CenterX = temp_X - (Math.Abs(j - poTrans.X) * (pMyParam.DieWidth / (pMyContext.MMPerPixel_X * 1000))) - pMyParam.Scrib_X*2;
                                        //VMDie_CenterX = temp_X - (Math.Abs(j - poTrans.X) * (temp_X / Math.Abs(OriginData.dCenterX - j)));
                                        VM_COLS.Add(poTrans.X, VMDie_CenterX);
                                        break;
                                    }
                                    else
                                    {
                                        //VMDie_CenterX = temp_X + (Math.Abs(poTrans.X - j) * (pMyParam.DieWidth + pMyParam.Scrib_X * (pMyContext.MMPerPixel_X * 1000)) / (pMyContext.MMPerPixel_X * 1000));

                                        VMDie_CenterX = temp_X + (Math.Abs(poTrans.X - j) * (pMyParam.DieWidth / (pMyContext.MMPerPixel_X * 1000))) + pMyParam.Scrib_X*2;
                                        //VMDie_CenterX = temp_X + (Math.Abs(poTrans.X - j) * (temp_X / Math.Abs(OriginData.dCenterX - j)));
                                        VM_COLS.Add(poTrans.X, VMDie_CenterX);
                                        break;
                                    }
                                }
                            }

                            if (VM_COLS.ContainsKey(poTrans.X) == false)
                            {
                                // VM Dictionary 에 해당 Key가 존재하지 않는다면, CreateVM 에서 생성한 좌표 기준의 Pixel 값을 가져 온다.
                                VMDie_CenterX = ScreenCenterX + dist_X;
                                Debug.WriteLine($"X Index:{poTrans.X}, Create Virtual Map X-Postion:{VMDie_CenterX}");
                            }
                        }

                        if (VM_ROWS.ContainsKey(poTrans.Y) == true)
                        {
                            VM_ROWS.TryGetValue(poTrans.Y, out VMDie_CenterY);
                        }
                        else
                        {
                            for (int j = 0; j < (OriginData.nMaxY - OriginData.nMinY + 1); j++)
                            {
                                if (VM_ROWS.ContainsKey(j) == true)
                                {
                                    double temp_Y = 0.0;
                                    VM_ROWS.TryGetValue(j, out temp_Y);

                                    if (poTrans.Y < j)
                                    {
                                        //VMDie_CenterY = temp_Y - (Math.Abs(j - poTrans.Y) * (pMyParam.DieHeight + (pMyParam.Scrib_Y * (pMyContext.MMPerPixel_Y * 1000))) / (pMyContext.MMPerPixel_Y * 1000));
                                        VMDie_CenterY = temp_Y - (Math.Abs(j - poTrans.Y) * (pMyParam.DieHeight / (pMyContext.MMPerPixel_Y * 1000))) - pMyParam.Scrib_Y*2;

                                        VM_ROWS.Add(poTrans.Y, VMDie_CenterY);
                                        break;
                                    }
                                    else
                                    {
                                        //VMDie_CenterY = temp_Y + (Math.Abs(j - poTrans.Y) * (pMyParam.DieHeight + (pMyParam.Scrib_Y * (pMyContext.MMPerPixel_Y * 1000))) / (pMyContext.MMPerPixel_Y * 1000));
                                        VMDie_CenterY = temp_Y + (Math.Abs(j - poTrans.Y) * (pMyParam.DieHeight / (pMyContext.MMPerPixel_Y * 1000))) + pMyParam.Scrib_Y*2;
                                        VM_ROWS.Add(poTrans.Y, VMDie_CenterY);
                                        break;
                                    }
                                }
                            }

                            if (VM_ROWS.ContainsKey(poTrans.Y) == false)
                            {
                                VMDie_CenterY = ScreenCenterY + dist_Y;
                                Debug.WriteLine($"Y Index:{poTrans.Y}, Create Virtual Map Y-Postion:{VMDie_CenterY}");
                            }
                        }

                        if ((VMDie_CenterY <= 0) || (VMDie_CenterX <= 0))
                            Debug.WriteLine($"{poTrans.X}, {poTrans.Y}");

                        VMDie_CenterX = Math.Truncate(VMDie_CenterX);
                        VMDie_CenterY = Math.Truncate(VMDie_CenterY);
                        */
                        #endregion VM_Dictionary Found Die End


                        // Unused VM_Dictionary Found Die start
                        VMDie_CenterX = ScreenCenterX + dist_X; // Virtual Die Pixel Center X
                        VMDie_CenterY = ScreenCenterY + dist_Y; // Virtual Die Pixel Center Y



                        /*
                        VMDie_CenterX = ScreenCenterX + (VMdataList[i].XP / pMyContext.MMPerPixel_X); // Virtual Die Pixel Center X
                        VM_COLS[poTrans.X] = VMDie_CenterX;
                        
                        VMDie_CenterY = ScreenCenterY + (VMdataList[i].YP / pMyContext.MMPerPixel_Y); // Virtual Die Pixel Center X
                        VM_ROWS[poTrans.Y] = VMDie_CenterY;

                        VMDie_CenterX = Math.Truncate(VMDie_CenterX);
                        VMDie_CenterY = Math.Truncate(VMDie_CenterY);
                        */


                        double Offset_XPixel = pMyContext.CenterOffsetXmm / pMyContext.MMPerPixel_X;
                        double Offset_YPixel = pMyContext.CenterOffsetYmm / pMyContext.MMPerPixel_Y;

                        //VMDie_CenterX = Math.Truncate(VMDie_CenterX + Offset_XPixel);
                        //VMDie_CenterY = Math.Truncate(VMDie_CenterY + Offset_YPixel);

                        VMDie_CenterX = Math.Round(VMDie_CenterX + Offset_XPixel);  // 반올림
                        VMDie_CenterY = Math.Round(VMDie_CenterY + Offset_YPixel);  // 반올림

                        //VMDie_CenterX = VMDie_CenterX + Offset_XPixel;  // 반올림
                        //VMDie_CenterY = VMDie_CenterY + Offset_YPixel;  // 반올림
                        // Unused VM_Dictionary Found Die End

                        // Scrib Pixel 을 입력 받아 계산된 값으로 ROI 영역을 산출.
                        double Scrib_X_um = pMyParam.Scrib_X * (pMyContext.MMPerPixel_X * 1000);   // 01.22 Insert
                        double Scrib_Y_um = pMyParam.Scrib_Y * (pMyContext.MMPerPixel_Y * 1000);   // 01.22 Insert


                        int x1 = (int)(VMDie_CenterX - (((pMyParam.DieWidth + Scrib_X_um) / (pMyContext.MMPerPixel_X * 1000)) * pMyParam.Die_Width_Ratio / 2));
                        int x2 = (int)(VMDie_CenterX + (((pMyParam.DieWidth + Scrib_X_um) / (pMyContext.MMPerPixel_X * 1000)) * pMyParam.Die_Width_Ratio / 2));
                        int y1 = (int)(VMDie_CenterY - (((pMyParam.DieHeight + Scrib_Y_um) / (pMyContext.MMPerPixel_Y * 1000)) * pMyParam.Die_Height_Ratio / 2));
                        int y2 = (int)(VMDie_CenterY + (((pMyParam.DieHeight + Scrib_Y_um) / (pMyContext.MMPerPixel_Y * 1000)) * pMyParam.Die_Height_Ratio / 2));
                        // 01.22 Insert End


                        // Die 중심 위치의 월드 좌표를 기준으로 ROI 영역을 구한다. (mm to pixel 변환 필요)
                        OpenCvSharp.Rect VMROI = new OpenCvSharp.Rect(x1, y1, x2-x1, y2-y1);

                        // 계산된 VMROI 영역만큼 이미지를 잘라 검사 진행.
                        Mat VMImage = new Mat(GrayImage, VMROI);
                        // 만약 해당 이미지가 1채널 그레이 영상이 아니라면, 이미지 타입 변환.
                        if (VMImage.Channels() != 1)
                            Cv2.CvtColor(VMImage, VMImage, ColorConversionCodes.BGR2GRAY);

                        Mat TempImage = null;
                        int TempContourCount = 0;
                        double TempContourArea = 0;
                        bool TempConvex = false;
                        int TempApexCount = 0;
                        Moments TempMoments = null;

                        System.Drawing.Point MPoint = poTrans;  // poTrans : 이동 변환된 행열의 인덱스
                        bool tempReturn = ProcessInspection(VMImage, MPoint, ref TempImage, ref TempContourCount, ref TempContourArea,
                            ref TempConvex, ref TempApexCount, ref TempMoments);

                        Debug.Indent();
                        Debug.WriteLine($"vitual Die Size: Width-{x2-x1}, Height-{y2-y1}");
                        Debug.WriteLine($"vitual Die Center: X-{VMDie_CenterX}, Y-{VMDie_CenterY}");
                        Debug.WriteLine($"vitual Image Size: Width-{TempImage.Width}, Height-{TempImage.Height}");
                        Debug.WriteLine($"Virtual Map Die Inspection Result: TX-{MPoint.X}, TY-{MPoint.Y}");
                        Debug.WriteLine($"VM Die Contours Count:{TempContourCount}");
                        Debug.WriteLine($"VM Die ContourArea:{TempContourArea}");
                        Debug.WriteLine($"VM Die ApexCount:{TempApexCount}");
                        Debug.Unindent();

                        Found_Die_Info VM_Die_Info = new Found_Die_Info();
                        FoundPoint value = OriginPoint[poTrans];

                        VM_Die_Info.ContourCount = TempContourCount;
                        VM_Die_Info.Area = TempContourArea;
                        VM_Die_Info.Apex = TempApexCount;
                        VM_Die_Info.moments = TempMoments;
                        VM_Die_Info.Convex = TempConvex;
                        VM_Die_Info.WX_Pos = value.X = VMdataList[i].XP;    // World Coordination X
                        VM_Die_Info.WY_Pos = value.Y = VMdataList[i].YP;    // World Coordination Y
                        VM_Die_Info.X_Pos = VMDie_CenterX;  // Pixel Coordination X
                        VM_Die_Info.Y_Pos = VMDie_CenterY;  // Pixel Coordination Y

                        VM_Die_Info.Width = pMyParam.ModuleModel.Master.Width;
                        VM_Die_Info.Height = pMyParam.ModuleModel.Master.Height;
                        VM_Die_Info.Angle = 0;

                        VM_Die_Info.Die_Image = TempImage.Clone();


                        if (tempReturn)
                        {
                            //if ((TempContourCount == 1) && (TempApexCount == 4) && (TempContourArea > ((x2-x1) * (y2 - y1) * 0.4)) && (TempContourArea < ((x2-x1) * (y2-y1) * 0.8)) )
                            if ((TempContourCount == 1) && (TempApexCount == 4) && (TempContourArea > ((x2-x1) * (y2 - y1) * 0.5))) // Virtual Map에서 찾은 Die 면적의 50% 이상이라면 정상으로 판단.
                            {
                                value.Status = 1;   // 정상 Die 판별
                            }
                            else
                            {
                                value.Status = 2;   // 불량 Die 판별
                            }
                            OriginPoint[poTrans] = value;

                            string strMapPath;
                            string strFileName;

                            strMapPath = Path.Combine(SystemHandler.Handle.Setting.ImageSavePath,
                                String.Format("{0:0000}{1:00}{2:00}/{3}", DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, pMyParam.MapName));
                            if (Directory.Exists(strMapPath) == false)
                                Directory.CreateDirectory(strMapPath);
                            strFileName = Path.Combine(strMapPath, String.Format("{0}_{1}_Image.png", poTrans.X, poTrans.Y)); // bmp -> png
                            Cv2.ImWrite(strFileName, VM_Die_Info.Die_Image);
                        }
                        else
                        {
                            if(pMyParam.VMDieEnabel == false)
                                value.Status = 0;   // 비어 있는 Die 판별. 이렇게 하면 UI 상의 Valid Die 개수가 증가하지만, ListViewBox에 표시 되지 않음.
                            else
                                value.Status = 2;   // 불량 Die 판별.

                            OriginPoint[poTrans] = value;

                            string strMapPath;
                            string strFileName;

                            strMapPath = Path.Combine(SystemHandler.Handle.Setting.ImageSavePath,
                                String.Format("{0:0000}{1:00}{2:00}/{3}", DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, pMyParam.MapName));
                            if (Directory.Exists(strMapPath) == false)
                                Directory.CreateDirectory(strMapPath);
                            strFileName = Path.Combine(strMapPath, String.Format("{0}_{1}_Image.png", poTrans.X, poTrans.Y)); // bmp -> png
                            Cv2.ImWrite(strFileName, VM_Die_Info.Die_Image);
                        }

                        try
                        {
                            Debug.Indent();
                            Debug.WriteLine($"VM Found Die UnAdd Count:{pMyContext.Found_Die.Count}");
                            Debug.Unindent();

                            // Vitual Map을 적용하기 전 Dictionary Key에 해당하는 요소 값은 없기 때문에 false 를 반환해야 정상.
                            //if (pMyContext.Found_Die.ContainsKey(poTrans) == false)
                            if ((pMyContext.Found_Die.ContainsKey(poTrans) == false) && (OriginPoint[poTrans].Status != 0))
                                pMyContext.Found_Die.Add(poTrans, VM_Die_Info); // Translation Map File Index Add VM_Die_Info
                            else
                                Debug.WriteLine($"Virtual MapSearch Function Dictionary Key Error:{poTrans.X},{poTrans.Y}");
                        }
                        catch (Exception e)
                        {
                            Logging.PrintErrLog((int)ELogType.Error, $"Vitual MapSearch Function FoundDie Key Exception:{e.Message}");
                            Debug.WriteLine($"Vitual MapSearch Found Die Exception:{e.ToString()}");
                        }

                    }
                    // 03.28 insert end
                    #endregion Virtual Map Image Position and Inspection End

                    

                    line = String.Format("X= {0:0000} Y= {1:0000} B= {2:00} X1= {3:0000} Y1= {4:0000} XP= {5:0.000} YP= {6:0.000} S= {7}",
                        dataList[i].X, dataList[i].Y, dataList[i].BinNum, OriginData.nTransX[i], OriginData.nTransY[i],
                        OriginPoint[poTrans].X, OriginPoint[poTrans].Y, OriginPoint[poTrans].Status);
                    writer.WriteLine(line);

                    //Wafer map data add
                    _ST_MAP_INFO temp = new _ST_MAP_INFO();
                    temp.Org_X = OriginData.nX[i];
                    temp.Org_Y = OriginData.nY[i];
                    temp.Bin = OriginData.nB[i];
                    temp.Tgt_X = poTrans.X = OriginData.nTransX[i];
                    if (temp.Tgt_X >= nMaxCol) nMaxCol = temp.Tgt_X + 1;
                    temp.Tgt_Y = poTrans.Y = OriginData.nTransY[i];
                    if (temp.Tgt_Y >= nMaxRow) nMaxRow = temp.Tgt_Y + 1;
                    temp.Pos_X = OriginPoint[poTrans].X;
                    temp.Pos_Y = OriginPoint[poTrans].Y;

                    // DieInspection()함수에서 PASS, FAIL 된 데이터 모두 MapData에 추가 됨. PASS : 1, FAIL: 2, MapFile에 존재하지 않은 Die는 0이고, MapData에 추가 하지 않음.
                    if ((OriginPoint.ContainsKey(poTrans) == true) && (OriginPoint[poTrans].Status != 0))
                    {
                        temp.Succ = OriginPoint[poTrans].Status;
                        pMyContext.MapData.Add(i, temp);
                    }
                }

                // Footer
                for (int i = 0; i < strFooterLines.Count; i++)
                {
                    string line = strFooterLines[i];
                    writer.WriteLine(line);
                }
            }

            return 0;
        }




        public override ActionContext Run()
        {
            switch ((EStep)Step)
            {
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
                    if (Context.ResultImage == null)
                    {
                        Logging.PrintLog((int)ELogType.Error, "{0} Camera Image Grab Failed!", pMyParam.DeviceName);

                        FinishAction(EContextResult.Error);
                        break;
                    }
                    if ((GrayImage == null) || (MophologyImage == null))
                    {   // change code 12.23 create the BinaryImage
                        GrayImage = new Mat(Context.ResultImage.Size(), MatType.CV_8UC1);
                        MophologyImage = new Mat(Context.ResultImage.Size(), MatType.CV_8UC1); // 12.23
                    }
                    else if ((GrayImage.Width != Context.ResultImage.Width) || (GrayImage.Height != Context.ResultImage.Height))
                    {
                        GrayImage.Dispose();
                        GrayImage = new Mat(Context.ResultImage.Size(), MatType.CV_8UC1);

                        MophologyImage.Dispose();      // 12.23    Free memory
                        MophologyImage = new Mat(Context.ResultImage.Size(), MatType.CV_8UC1); // 12.23
                    }
                    // Convert Camera Image to GrayImage.
                    Cv2.CvtColor(Context.ResultImage, GrayImage, ColorConversionCodes.BGR2GRAY);

                    #region Morphology start insert date 12.23
                    if (pMyParam.Mophology)
                    {
                        /*
                         Mophology Open ImageProcessing.
                         Model Finder에서 Die를 찾아 판별하는 부분만 Binary Image를 사용하였으며, 
                         Contours Processing에서는 원본 grab한 이미지(GrayImage)를 그대로 사용.
                         만약 Contours Processing에도 Binary Image를 사용하려면, GrayImage = BinaryImage.Clone() 또는 
                         Cv2.Threashold(GrayImage, GrayImage, 30, 255, ThresholdTypes.Binary) 와 같이 사용하면되며, 
                         이때는 BinaryImage가 필요 없음.
                        */
                        Cv2.Threshold(GrayImage, MophologyImage, pMyParam.Mophology_Threshold, 255, ThresholdTypes.Binary);        // parameter value  Origin
                        Mat element = Cv2.GetStructuringElement(MorphShapes.Rect, new OpenCvSharp.Size(2, 2));  // Origin 2, 2
                        Cv2.MorphologyEx(MophologyImage, MophologyImage, MorphTypes.Open, element);

                        // 02.15 Insert Start
                        if ((pMyParam.MophologyImage == true) && (pMyParam.MapCheck == true))
                        {
                            string strMapPath = Path.Combine(SystemHandler.Handle.Setting.ImageSavePath,
                                String.Format("{0:0000}{1:00}{2:00}/{3}", DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, pMyParam.ProcessName + "_NoMapMopology"));
                            if (Directory.Exists(strMapPath) == false)
                                Directory.CreateDirectory(strMapPath);
                            string strFileName = Path.Combine(strMapPath, String.Format($"{DateTime.Now.Hour}_{DateTime.Now.Minute}_{DateTime.Now.Second}_Mophology.bmp"));
                            Cv2.ImWrite(strFileName, MophologyImage);
                        }
                        // 02.15 Insert End

                        // Mophology Image 적용된 이미지를 MIL Model Finder에 전달.
                        if (ALLIGATOR_ALG_MIL.agtAM_PutImage(AlgIndex, MophologyImage.Data) != 0)
                        {   // 12. 23 Change Code : Morphology Open Image (using previously threshold binary image)
                            Logging.PrintLog((int)ELogType.Error, "Failed {0} in {1} ReturnCode:{2}", "agtAM_PutImage", ID.ToString(), result);

                            FinishAction(EContextResult.Error);
                            break;
                        }
                    }
                    else
                    {
                        // Camera Grab 된 이미지의 Gray Scale 이미지를 MIL Model Finder 에 전달.
                        if (ALLIGATOR_ALG_MIL.agtAM_PutImage(AlgIndex, GrayImage.Data) != 0)    // Original Code : Gray Image
                        {
                            Logging.PrintLog((int)ELogType.Error, "Failed {0} in {1} ReturnCode:{2}", "agtAM_PutImage", ID.ToString(), result);

                            FinishAction(EContextResult.Error);
                            break;
                        }
                    }
                    #endregion Morphology End

                    Thread.Sleep(500);  // 02.05 insert
                    pMyContext.bMapList = false;

                    pMyContext.CrobImage = GrayImage.Clone();       // Crob 하기 위한 이미지 복사

                    Step++;
                    break;

                case EStep.Calibration:
                    //Grid calibration associate
                    if (pMyParam.CalCheck)
                    {
                        if ((pMyParam.CalCheck) && (ALLIGATOR_ALG_MIL.agtAM_GridCalibrationAssociate(AlgIndex, CalibrationIndex) != 0))
                        {
                            Logging.PrintErrLog((int)ELogType.Error, string.Format("Exception while {0} in {1}, ReturnCode:{2}", "agtAM_GridCalibrationAssociate", ID.ToString(), result));
                            FinishAction(EContextResult.Error);
                            break;
                        }
                    }
                    else
                    {
                        ALLIGATOR_ALG_MIL.agtAM_GridCalibrationDeAssociate(AlgIndex, CalibrationIndex);
                        ALLIGATOR_ALG_MIL.agtAM_GridCalibrationModelDeAssociate(AlgIndex, ModelIndex, CalibrationIndex);
                    }

                    if (pMyParam.CalCheck)
                    {
                        #region WaferScanClibrationParam Start: Get Param
                        // WaferScanCalibrationParam 의 parameter 값을 가져와서 저장.    12.11 
                        WaferScanCalibrationParam calibParam = SystemHandler.Handle.Sequences[ParentID][EAction.Wafer_Calibration].Param as WaferScanCalibrationParam;
                        pMyContext.MMPerPixel_X = calibParam.MMPerPixel_X;     // 1Pixel per mm //0.0484539718412904
                        pMyContext.MMPerPixel_Y = calibParam.MMPerPixel_Y;     // 1Pixel Per mm //0.0488501543569413

                        Debug.WriteLine($"CameraCalibration X:{pMyContext.MMPerPixel_X}, Y:{pMyContext.MMPerPixel_Y}");

                        int Rect_Die_Width = (int)(pMyParam.DieWidth / (pMyContext.MMPerPixel_X * 1000));   // Die Width Size - Unit Pixel 
                        int Rect_Die_Height = (int)(pMyParam.DieHeight / (pMyContext.MMPerPixel_Y * 1000)); // Die Heigh Size - Unit Pixel

                        Debug.WriteLine($"Pixel - Die Rect Size Width:{Rect_Die_Width}Pixel, Die Rect Size Height:{Rect_Die_Height}Pixel");
                        Debug.WriteLine($"mm - Die Rect Size Width:{pMyParam.DieWidth/1000}mm, Die Rect Size Height:{pMyParam.DieHeight/1000}mm");

                        #endregion WaferScanCalibrationParam End
                    }

                    Step++;
                    break;

                case EStep.LineAngle:

                    if (pMyParam.OrientSearch == false)     // OrientSearch 사용하지 않음.
                    {
                        // 01.25 Insert start
                        double PosAngle_sum = 0;
                        int PosSum_count = 0;
                        double PosCalAngle = 0.0;

                        double NegAngle_sum = 0;
                        int NegSum_count = 0;
                        double NegCalAngle = 0.0;

                        double calAngle = 0;

                        ModelResult.Init();
                        try
                        {
                            // PutImage(Gray Image Or Mophology Image)를 이용하여 등록된 Model 조건에 맞는 Die를 찾는다.
                            result = ALLIGATOR_ALG_MIL.agtAM_FindModel(AlgIndex, ModelIndex, out ModelResult);
                            if (result != 0)
                            {
                                Logging.PrintLog((int)ELogType.Error, "Failed {0} in {1} ReturnCode:{2}", "agtAM_FindModel", ID.ToString(), result);
                                pMyContext.ModelFinderResult = EVisionResultType.NG;
                                FinishAction(EContextResult.Error);
                                break;
                            }
                            pMyContext.CrobImage = GrayImage.Clone();       // Crob 하기 위한 이미지 복사
                            pMyContext.ModelFinderResult = EVisionResultType.OK;
                        }
                        catch (Exception e)
                        {
                            Logging.PrintErrLog((int)ELogType.Error, string.Format("Exception while {0} in {1}, ReturnCode:{2}, ({3})", "agtAM_FindModel", ID.ToString(), result, e.Message));
                            pMyContext.ModelFinderResult = EVisionResultType.NG;
                            FinishAction(EContextResult.Error);
                            break;
                        }

                        #region Median LineAngele Calculate Start
                        /*
                        // 02.21 insert Start
                        // Median calculate
                        // Model Finder에서 찾은 모델의 Angle 표기 방법은
                        // 1. X축 기준으로 아래 방향으로 기울어진 경우, 360에 가까운 값으로 연산.
                        // 2. X축 기준으로 위쪽 방향으로 기울어진 경우, 0에서 멀어진 값으로 연산.
                        double[] arrayPosValue = new double[ModelResult.nCnt];  // 02.21 insert
                        double[] arrayNegValue = new double[ModelResult.nCnt];  // 02.21 insert

                        for (int i = 0; i < ModelResult.nCnt; i++)
                        {
                            if (ModelResult.dAngle[i] > 315 && ModelResult.dAngle[i] < 360)
                            {
                                arrayNegValue[NegSum_count] = ModelResult.dAngle[i];    // 02.21 insert

                                NegAngle_sum += ModelResult.dAngle[i];
                                NegSum_count++;
                            }
                            else if (ModelResult.dAngle[i] > 0 && ModelResult.dAngle[i] < 45)
                            {
                                arrayPosValue[PosSum_count] = ModelResult.dAngle[i];    // 02.21 insert

                                PosAngle_sum += ModelResult.dAngle[i];
                                PosSum_count++;
                            }
                            else
                            {
                                Debug.WriteLine($"Other value:{ModelResult.dAngle[i]}");
                            }
                        }

                        
                        double dPosTemp = 0.0;
                        double dPosTemp1 = 0.0;
                        double dNegTemp = 0.0;
                        double dNegTemp1 = 0.0;
                        double[] arrayPosAngle = new double[PosSum_count];
                        double[] arrayPosScore = new double[PosSum_count];
                        double[] arrayNegAngle = new double[NegSum_count];
                        double[] arrayNegScore = new double[NegSum_count];

                        for(int i = 0; i < PosSum_count; i++)
                        {
                            arrayPosAngle[i] = arrayPosValue[i];
                        }

                        for (int i = 0; i < NegSum_count; i++)
                        {
                            arrayNegAngle[i] = arrayNegValue[i];
                        }

                        for (int a = 0; a < PosSum_count - 1; a++)
                        {
                            for (int b = 0; b < PosSum_count - 1 - a; b++)
                            {
                                if (arrayPosAngle[b] > arrayPosAngle[b + 1])
                                {
                                    dPosTemp = arrayPosAngle[b];
                                    arrayPosAngle[b] = arrayPosAngle[b + 1];
                                    arrayPosAngle[b + 1] = dPosTemp;
                                }
                                if (arrayPosScore[b] > arrayPosScore[b + 1])
                                {
                                    dPosTemp1 = arrayPosScore[b];
                                    arrayPosScore[b] = arrayPosScore[b + 1];
                                    arrayPosScore[b + 1] = dPosTemp1;
                                }
                            }
                        }

                        for (int a = 0; a < NegSum_count - 1; a++)
                        {
                            for (int b = 0; b < NegSum_count - 1 - a; b++)
                            {
                                if (arrayNegAngle[b] > arrayNegAngle[b + 1])
                                {
                                    dNegTemp = arrayNegAngle[b];
                                    arrayNegAngle[b] = arrayNegAngle[b + 1];
                                    arrayNegAngle[b + 1] = dNegTemp;
                                }
                                if (arrayNegScore[b] > arrayNegScore[b + 1])
                                {
                                    dNegTemp1 = arrayNegScore[b];
                                    arrayNegScore[b] = arrayNegScore[b + 1];
                                    arrayNegScore[b + 1] = dNegTemp1;
                                }
                            }
                        }

                        if (PosSum_count != 0)
                        {
                            PosCalAngle = (arrayPosAngle[(PosSum_count / 2) - 1] + arrayPosAngle[PosSum_count / 2]) * 1.0 / 2.0;
                            double dPosMeanScore = (arrayPosScore[(PosSum_count / 2) - 1] + arrayPosScore[PosSum_count / 2]) * 1.0 / 2.0;
                        }
                        if (NegSum_count != 0)
                        {
                            NegCalAngle = (arrayNegAngle[(NegSum_count / 2) - 1] + arrayNegAngle[NegSum_count / 2]) * 1.0 / 2.0;
                            NegCalAngle = NegCalAngle - 360;   // Wafer가 회전해야 하는 Angle 값. 시계 반대 방향이 (-) 방향.
                            double dNegMeanScore = (arrayNegScore[(NegSum_count / 2) - 1] + arrayNegScore[NegSum_count / 2]) * 1.0 / 2.0;
                        }

                        pMyContext.dAngle = calAngle = PosCalAngle + NegCalAngle;
                        // 02.21 insert End
                        */
                        #endregion Median LineAngele Calculate End

                        #region Just Find die Average Calculate Start
                        // 상기 Median Calculate 적용으로 주석처리. (02.21)
                        // Just Average Calculate
                        // Model Finder에서 찾은 모델의 Angle 표기 방법은
                        // 1. X축 기준으로 아래 방향으로 기울어진 경우, 360에 가까운 값으로 연산.
                        // 2. X축 기준으로 위쪽 방향으로 기울어진 경우, 0에서 멀어진 값으로 연산.
                        for (int i = 0; i < ModelResult.nCnt; i++)
                        {
                            if (ModelResult.dAngle[i] > 315 && ModelResult.dAngle[i] < 360)
                            {
                                NegAngle_sum += ModelResult.dAngle[i];
                                NegSum_count++;
                            }
                            else if (ModelResult.dAngle[i] > 0 && ModelResult.dAngle[i] < 45)
                            {
                                PosAngle_sum += ModelResult.dAngle[i];
                                PosSum_count++;
                            }
                            else
                            {
                                Debug.WriteLine($"Other value:{ModelResult.dAngle[i]}");
                            }
                        }
                        if (PosSum_count != 0)
                        {
                            // ModelFinder에서 찾은 모델의 Angle Positive 방향은 반시계 방향.
                            PosCalAngle = PosAngle_sum / PosSum_count;
                            //calAngle = PosCalAngle;     // Wafer가 회전해야 하는 Angle 값. 시계 방향이 (+) 방향.
                        }
                        if (NegSum_count != 0)
                        {
                            // ModelFinder에서 찾은 모델의 Angle Negative 방향은 시계 방향.
                            NegCalAngle = NegAngle_sum / NegSum_count;
                            NegCalAngle = NegCalAngle - 360;   // Wafer가 회전해야 하는 Angle 값. 시계 반대 방향이 (-) 방향.
                        }

                        pMyContext.dAngle = calAngle = PosCalAngle + NegCalAngle;
                        #endregion Just Find die Average Calculate End

                        //pMyContext.dAngle = calAngle;

                        Debug.WriteLine($"Positive Angle:{PosCalAngle}, Negative Angle:{NegCalAngle}, calAngle:{calAngle}, { ID.ToString()}");

                        // 조건에 따른 제어부에 Error 처리 및 회전 정렬 요청 처리 필요.
                        if ((calAngle < -pMyParam.Tolerance_Rotate) || (calAngle > pMyParam.Tolerance_Rotate))       // 0.4
                        {
                            pMyContext.bBoundRatio = false;
                            Logging.PrintErrLog((int)ELogType.Error, string.Format("{0} - Tolerance Roate Fail! Rotate Value:{1}", ID.ToString(), calAngle));
                            pMyContext.AngleFoundResult = EVisionResultType.ANG;  // 12.21
                            FinishAction(EContextResult.WaferAngleError);
                            break;
                        }
                        else
                        {
                            pMyContext.AngleFoundResult = EVisionResultType.OK;
                            pMyContext.bBoundRatio = true;
                        }
                        // 01.25 Insert End
                    }
                    else
                    {
                        #region OrientSearch Start
                        // Line의 Angle을 찾는다
                        int nAngleCnt = 0;
                        float[] fAngle = new float[10];
                        float[] fAngleScr = new float[10];

                        float fGetAngle = 0;
                        float fGetAngleScr = 0;

                        float calAngle = 0;

                        //ALLIGATOR_ALG_MIL.agtAM_OrientSearch_4Division(AlgIndex, 10, true); // 02.20 New DLL 적용하면서 적용. (검증 필요: 정확도가 낮아 사용하지 않음.)
                        ALLIGATOR_ALG_MIL.agtAM_OrientSearch(AlgIndex, 10, true);         // 02.20 주석처리 함. (Origin)
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

                        //Debug.WriteLine($"AngleCnt:{nAngleCnt}, Angle value:{fGetAngle}, Angle Score:{fGetAngleScr}, Cal Angle:{calAngle}");

                        //pMyContext.dAngle = calAngle; // 정방향: 시계 반대 방향 일때 12월 21일 변경되어 주석 처리.
                        pMyContext.dAngle = -1 * calAngle;   // 정방향: 시계 방향

                        // 조건에 따른 제어부에 Error 처리 및 회전 정렬 요청 처리 필요.
                        if ((calAngle < -pMyParam.Tolerance_Rotate) || (calAngle > pMyParam.Tolerance_Rotate))       // 0.4
                        {
                            pMyContext.bBoundRatio = false;
                            Logging.PrintErrLog((int)ELogType.Error, string.Format("{0} - Tolerance Roate Fail! Rotate Value:{1}", ID.ToString(), calAngle));
                            pMyContext.AngleFoundResult = EVisionResultType.ANG;  // 12.21
                            FinishAction(EContextResult.WaferAngleError);
                            break;
                        }
                        else
                        {
                            pMyContext.AngleFoundResult = EVisionResultType.OK;
                            pMyContext.bBoundRatio = true;
                        }
                        #endregion OrientSearch End
                    }

                    // 01.18 레시피에 모델 등록 시 Wafer가 정렬된 상태(Tolerance_Rotate 범위내에 존재할 때 이미지를 저장할지 선택)에서 Modelfinder에 모델을 등록을 편하게 하기 위해 이미지 저장 옵션 추가.
                    // 02.15 Mapfile을 사용하지 않고, LineAngle이 정상일때 이미지 저장 여부 추가. Start
                    if ((pMyContext.AngleFoundResult == EVisionResultType.OK) && (pMyParam.SaveWaferImage == true) && (pMyParam.MapCheck == true))      // Unsued MapFile 조건
                    {
                        string strMapPath = Path.Combine(SystemHandler.Handle.Setting.ImageSavePath,
                            String.Format("{0:0000}{1:00}{2:00}/{3}", DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, pMyParam.ProcessName + "_NoMapWafer"));
                        if (Directory.Exists(strMapPath) == false)
                            Directory.CreateDirectory(strMapPath);
                        string strFileName = Path.Combine(strMapPath, String.Format($"{DateTime.Now.Hour}_{DateTime.Now.Minute}_{DateTime.Now.Second}_Image.bmp"));
                        Cv2.ImWrite(strFileName, Context.ResultImage);
                    }
                    // 02.15 Mapfile을 사용하지 않고, LineAngle이 정상일때 이미지 저장 여부 추가. End

                    Step++;
                    break;

                case EStep.MapDataRead:
                    // MapFile이 없는 상태에서 제어부와 테스트를 위해 MapFile read 여부를 체크
                    if (pMyParam.MapCheck == false) // Map file 사용.
                    {
                        try
                        {
                            string mapFile = null;  // MapFile name
                            TestPacket testPacket = Param.Parent.RequestPacket.AsTest();    // Protocol Recieve MapFile (통신으로 연결하지 않고 이미지 로딩하여 검증 진행 시 System.NullReferenceException 발생 포인트)
                            if (testPacket != null)
                            {
                                mapFile = Path.Combine(SystemHandler.Handle.Setting.MapDataLoadPath, testPacket.TestID);    // 통신으로 넘어온 MapFile name 
                                // 02.19 insert Start
                                FileInfo info = new FileInfo(mapFile);
                                if(info.Exists)
                                {
                                    pMyContext.MapFileName = testPacket.TestID; // 03.20 Insert
                                }
                                else
                                {
                                    CustomMessageBox.Show("MapFile Not Exists", $"Not Found {testPacket.TestID}", MessageBoxImage.Error, true, true, 3);
                                    pMyContext.MapFileResult = EVisionResultType.NG;
                                    FinishAction(EContextResult.Error);
                                    break;
                                }
                                // 02.19 insert End

                                // 01.29 Insert start
                                if ((pMyParam.SaveWaferImage == true) && (pMyContext.AngleFoundResult == EVisionResultType.OK))
                                {
                                    string strMapPath = Path.Combine(SystemHandler.Handle.Setting.ImageSavePath,
                                        String.Format("{0:0000}{1:00}{2:00}/{3}", DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, pMyParam.ProcessName + "_Wafer"));
                                    if (Directory.Exists(strMapPath) == false)
                                        Directory.CreateDirectory(strMapPath);
                                    // 통신으로 전달받은 맵파일 이름으로 이미지 저장.
                                    string strFileName = Path.Combine(strMapPath, String.Format($"{DateTime.Now.Hour}_{DateTime.Now.Minute}_{DateTime.Now.Second}_{testPacket.TestID}.bmp"));
                                    Cv2.ImWrite(strFileName, Context.ResultImage);
                                }
                                // 01.29 Insert End

                                // 02.15 Isert Start
                                if ((pMyParam.Mophology == true) && (pMyParam.MophologyImage == true))
                                {
                                    string strMapPath = Path.Combine(SystemHandler.Handle.Setting.ImageSavePath,
                                        String.Format("{0:0000}{1:00}{2:00}/{3}", DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, pMyParam.ProcessName + "_Wafer_Mophology"));
                                    if (Directory.Exists(strMapPath) == false)
                                        Directory.CreateDirectory(strMapPath);
                                    string strFileName = Path.Combine(strMapPath, String.Format($"{DateTime.Now.Hour}_{DateTime.Now.Minute}_{DateTime.Now.Second}_{testPacket.TestID}_Mophology.bmp"));
                                    Cv2.ImWrite(strFileName, MophologyImage);
                                }
                                // 02.15 Isert Start
                            }
                            else
                            {
                                mapFile = Path.Combine(SystemHandler.Handle.Setting.MapDataLoadPath, pMyParam.MapName);     // Debug 진행시 paramerter 전달할 mapfile name
                            }

                            dataList = ReadMapFile(mapFile);
                            sFileName = mapFile;
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"MapFile Exception: {ex.Message}");
                            if (pMyParam.MapName != null)
                            {
                                string mapFile = Path.Combine(SystemHandler.Handle.Setting.MapDataLoadPath, pMyParam.MapName);
                                dataList = ReadMapFile(mapFile);
                                sFileName = mapFile;
                                pMyContext.MapFileName = pMyParam.MapName;  // 03.20 Insert
                            }
                            else
                            {
                                Logging.PrintErrLog((int)ELogType.Error, string.Format("Exception while {0} in {1}, ReturnCode:{2}", "MapFile Load Error", ID.ToString(), result, ex.ToString()));
                                pMyContext.MapFileResult = EVisionResultType.NG;
                                FinishAction(EContextResult.Error);
                                break;
                            }
                        }
                    }

                    if(pMyParam.TeachingImage)
                    {
                        FinishAction(EContextResult.WaferDieTeaching);
                        break;
                    }

                    Step++;
                    break;
                case EStep.CircleFind:
                    try
                    {
                        double dRange, dStep;
                        double dCenterXWorld = 0, dCenterYWorld = 0;
                        int nThresh, nBright, nDistThresh, nNoSam;
                        ALLIGATOR_ALG_MIL.agtAM_GetRansacParam(AlgIndex, out dRange, out dStep, out nThresh, out nBright, out nDistThresh, out nNoSam);
                        ALLIGATOR_ALG_MIL.agtAM_SetRansacParam(AlgIndex, 5, 10, 30, 0, 30, 3);
                        int nGet = ALLIGATOR_ALG_MIL.agtAM_SearchRansacCircle(AlgIndex, true, (int)pMyParam.InnerCircle.CenterX, (int)pMyParam.InnerCircle.CenterY,
                            (int)pMyParam.InnerCircle.Radius, out dGetRansacCenterX, out dGetRansacCenterY, out dGetRansacRadius);

                        if ((nGet == 0) && (dGetRansacRadius != 0))       // 정상적으로 Circle을 찾았다면, Radius 가 0이 아니라면
                        {
                            pMyContext.bFoundCircle = true;
                            pMyContext.dFoundCenterX = dGetRansacCenterX;       // Pixel-X
                            pMyContext.dFoundCenterY = dGetRansacCenterY;       // Pixel-Y
                            pMyContext.dMovingCenterX = dGetRansacCenterX;
                            pMyContext.dMovingCenterY = dGetRansacCenterY;
                            pMyContext.dRadius = dGetRansacRadius;        // pixel
                            //pMyContext.dRadmm = dGetRansacRadius * pMyParam.PixelToMM_Offset / 1000;   // mm
                            pMyContext.dRadmm = dGetRansacRadius * pMyParam.PixelToUM_Offset / 1000;   // mm 02.14

                            nGet = ALLIGATOR_ALG_MIL.agtAM_GridCalibrationTransformCoordinatesToWorld(AlgIndex, CalibrationIndex,
                                dGetRansacCenterX, dGetRansacCenterY,
                                out dCenterXWorld, out dCenterYWorld);
                            pMyContext.dFoundCenterXWorld = dCenterXWorld;
                            pMyContext.dFoundCenterYWorld = dCenterYWorld;

                            // 01.31 Insert Start
                            if (pMyContext.dFoundCenterX < pCamera.CenterX)  // 원의 X 중심이 왼쪽에 있다면, 이동해야 하는 값을 계산하여 저장.
                                //pMyContext.CenterOffsetXmm = pMyContext.dFoundCenterXWorld = -1 * pMyContext.dFoundCenterXWorld;
                                pMyContext.CenterOffsetXmm = -1 * pMyContext.dFoundCenterXWorld;    // Change 04.19
                            else
                                //pMyContext.CenterOffsetXmm = pMyContext.dFoundCenterXWorld; // Origin
                                //pMyContext.CenterOffsetXmm = pMyContext.dFoundCenterXWorld = -1 * pMyContext.dFoundCenterXWorld;    // 02.23 insert
                                pMyContext.CenterOffsetXmm = -1 * pMyContext.dFoundCenterXWorld;    // Change 04.19

                            //pMyContext.CenterOffsetYmm = pMyContext.dFoundCenterYWorld = -1 * pMyContext.dFoundCenterYWorld;
                            pMyContext.CenterOffsetYmm = -1 * pMyContext.dFoundCenterYWorld;    // Change 04.19
                            // 01.31 Insert End

                            // 03.27 Insert - Create Virtual Map Data
                            CreateVM();
                        }
                        else
                        {
                            Logging.PrintErrLog((int)ELogType.Error, "Ransac Find Circle Fail ! - {0} in {1} ReturnCode:{2}", "Ransac Circle", ID.ToString(), nGet);
                            FinishAction(EContextResult.Fail);
                            break;
                        }
                    }
                    catch (Exception e)
                    {
                        Logging.PrintErrLog((int)ELogType.Error, string.Format("Exception while {0} in {1}, ReturnCode:{2} ({3})", "Ransac Found Circle", ID.ToString(), result, e.Message));
                        pMyContext.CircleFoundResult = EVisionResultType.NG;
                        FinishAction(EContextResult.Error);
                        break;
                    }

                    Step++;
                    break;
                case EStep.ModelFind:
                    if (pMyParam.OrientSearch == true)
                    {
                        // EStep.LineAngle 에서 ModelFind를 진행하여 기울기를 찾을때, 사용하기 때문에 여기서는 주석(삭제)처리.
                        ModelResult.Init();
                        try
                        {
                            result = ALLIGATOR_ALG_MIL.agtAM_FindModel(AlgIndex, ModelIndex, out ModelResult);
                            if (result != 0)
                            {
                                Logging.PrintLog((int)ELogType.Error, "Failed {0} in {1} ReturnCode:{2}", "agtAM_FindModel", ID.ToString(), result);
                                pMyContext.ModelFinderResult = EVisionResultType.NG;
                                FinishAction(EContextResult.Error);
                                break;
                            }
                            pMyContext.ModelFinderResult = EVisionResultType.OK;
                        }
                        catch (Exception e)
                        {
                            Logging.PrintErrLog((int)ELogType.Error, string.Format("Exception while {0} in {1}, ReturnCode:{2}, ({3})", "agtAM_FindModel", ID.ToString(), result, e.Message));
                            pMyContext.ModelFinderResult = EVisionResultType.NG;
                            FinishAction(EContextResult.Error);
                            break;
                        }
                    }

                    #region unused mapfile start code
                    // map파일 사용하지 않고 Modelfinder에서 찾은 Die를 파일에 저장. MapFile이 없는 상태에서 Die 위치 검증용으로 요청하여 작성 (12.06) START
                    if ((pMyParam.MapCheck == true) && (pMyContext.ModelFinderResult == EVisionResultType.OK))
                    {
                        // ModelFinder에 등록된 모델 정보를 이용하여 기본 basic_width와 basic_Height를 산출. 01월 17일 적용.
                        double basic_Width = pMyParam.ModuleModel.Master.Width;
                        double basic_Height = pMyParam.ModuleModel.Master.Height;

                        // Create Wafer Map 01.08 start
                        // Circle 에 외접하는 행렬 개수 산출. 정확한 개수를 알수 없기 때문에 여분의 행과 열에 추가 개수를 더함.
                        int NoMap_Col = (int)Math.Truncate((pMyContext.dRadmm * 2) / (basic_Width * pMyContext.MMPerPixel_X)) + 5;   // X 방향 추가 행 개수: 5
                        int NoMap_Row = (int)Math.Truncate((pMyContext.dRadmm * 2) / (basic_Height * pMyContext.MMPerPixel_Y)) + 5;  // y 방향 추가 행 개수 : 5


                        nMaxCol = NoMap_Col;
                        nMaxRow = NoMap_Row;

                        //Debug.WriteLine($"Die col Count: {NoMap_Col}");
                        //Debug.WriteLine($"Die Row Count: {NoMap_Row}");

                        /*
                        // Parameter의 Die Width와 Die Height 의 입력된 값을 이용하여 pixel로 변환하여 기본 basic_width와 basic_Height를 산출. 01월 17일 이전에 사용.
                        double basic_Width = pMyParam.DieWidth / (pMyContext.MMPerPixel_X * 1000) + 10;     // Pixel  + 5
                        double basic_Height = pMyParam.DieHeight / (pMyContext.MMPerPixel_Y * 1000) + 3;    // Pixel  + 2
                        */

                        /*  01.23 위로 이동. NoMap_Col과 NoMax_Row를 구하기 위해 이동
                        // ModelFinder에 등록된 모델 정보를 이용하여 기본 basic_width와 basic_Height를 산출. 01월 17일 적용.
                        double basic_Width = pMyParam.ModuleModel.Master.Width;
                        double basic_Height = pMyParam.ModuleModel.Master.Height;
                        */

                        bool sCol = false;
                        bool sRow = false;

                        int col = 0;
                        int row = 0;

                        int Col_Max = -1;
                        int Row_Max = -1;
                        int Col_Min = 100;
                        int Row_Min = 100;

                        // Create Wafer Map 01.17 End


                        // 제어부 검증용으로 ModelFinder에서 찾은 모든 Die의 위치 정보를 Screen Center 기준으로 Die Center와 떨어진 실제 거리를 mm로 변환하여 저장.
                        // Angle 값은 의미가 없음.
                        string FoudDieTestFile = Path.Combine(SystemHandler.Handle.Setting.MapDataSavePath, "FoundDieTest.txt");
                        using (StreamWriter writer = new StreamWriter(FoudDieTestFile))
                        {
                            for (int i = 0; i < ModelResult.nCnt; i++)
                            {
                                string line = null;

                                // 01.16 insert start
                                Found_Die_Info temp = new Found_Die_Info();     // Die 정보
                                System.Drawing.Point MatrixPoint = new System.Drawing.Point();  // Die 행렬
                                // 01.16 insert end

                                // 01.18 insert start
                                _ST_MAP_INFO tempmap = new _ST_MAP_INFO();
                                // 01.18 insert end

                                pMyContext.dFoundModelX[i] = ModelResult.dXPos[i];
                                pMyContext.dFoundModelY[i] = ModelResult.dYPos[i];
                                pMyContext.dFoundModelXWorld[i] = ModelResult.dXWorldPos[i];
                                pMyContext.dFoundModelYWorld[i] = ModelResult.dYWorldPos[i];
                                pMyContext.dFoundModelAngle[i] = ModelResult.dAngle[i];
                                pMyContext.dFoundModelScore[i] = ModelResult.dScore[i];
                                pMyContext.dFoundModelWidth[i] = pMyParam.ModuleModel.Master.Width;
                                pMyContext.dFoundModelHeight[i] = pMyParam.ModuleModel.Master.Height;


                                for (col = 0; col < NoMap_Col; col++)
                                {
                                    // ModeFinder에서 찾은 Die 중심값이 행렬의 시작 Pixel에서 다음 행렬의 시작 Pixel 범위 안에 있다면
                                    if ((ModelResult.dXPos[i] > (basic_Width * col)) && (ModelResult.dXPos[i] < basic_Width * (col + 1)))
                                    {
                                        sCol = true;
                                        //Debug.WriteLine($"COL Detect:{col}, ModelFinder num:{i}, value:{ModelResult.dXPos[i]}, min:{basic_Width * col}, max:{basic_Width * (col + 1)}");

                                        if (col >= Col_Max) Col_Max = col;
                                        if (col <= Col_Min) Col_Min = col;

                                        break;
                                    }
                                    else
                                        sCol = false;
                                }
                                for (row = 0; row < NoMap_Row; row++)
                                {
                                    if ((ModelResult.dYPos[i] > (basic_Height * row)) && (ModelResult.dYPos[i] < basic_Height * (row + 1) + 150))
                                    {
                                        sRow = true;
                                        //Debug.WriteLine($"ROW Detect:{row}, ModelFinder num:{i}, value:{ModelResult.dYPos[i]}, min:{basic_Height * row}, max:{basic_Height * (row + 1)}");

                                        if (row >= Row_Max) Row_Max = row;
                                        if (row <= Row_Min) Row_Min = row;

                                        break;
                                    }
                                    else
                                        sRow = false;
                                }


                                try
                                {
                                    if ((sCol == true) && (sRow == true))
                                    {
                                        // 01.18 insert start
                                        int ContourCount = 0;
                                        double ContourArea = 0;
                                        bool Convex = false;
                                        int ApexCount = 0;

                                        Moments moments = null;

                                        Mat Die_Image = null;   // 01.20 Insert
                                        // 01.18 insert end

                                        MatrixPoint.X = col - pMyParam.Shift_Col;    // minus Shift Col value 2
                                        MatrixPoint.Y = row - pMyParam.Shift_Row;    // minus Shift Row value 3

                                        temp.X_Pos = ModelResult.dXPos[i];
                                        temp.Y_Pos = ModelResult.dYPos[i];
                                        temp.WX_Pos = ModelResult.dXWorldPos[i];
                                        temp.WY_Pos = ModelResult.dYWorldPos[i];
                                        temp.Width = pMyParam.ModuleModel.Master.Width;
                                        temp.Height = pMyParam.ModuleModel.Master.Height;
                                        temp.Angle = ModelResult.dAngle[i];

                                        // 01.18 insert start
                                        tempmap.Org_X = col;
                                        tempmap.Org_Y = row;
                                        tempmap.Tgt_X = MatrixPoint.X;
                                        tempmap.Tgt_Y = MatrixPoint.Y;
                                        tempmap.Bin = 10;

                                        tempmap.Pos_X = ModelResult.dXWorldPos[i];  // ListViewer 에 표시 되지 않던 부분 수정 01.19
                                        tempmap.Pos_Y = ModelResult.dYWorldPos[i];  // ListViewer 에 표시 되지 않던 부분 수정 01.19

                                        //if (DieInspection(temp, MatrixPoint, ref ContourCount, ref ContourArea, ref Convex, ref ApexCount, ref Die_Image) == false)
                                        if (DieInspection(temp, MatrixPoint, ref ContourCount, ref ContourArea, ref Convex, ref ApexCount, ref moments, ref Die_Image) == false)
                                        {
                                            tempmap.Succ = 2;
                                        }
                                        else
                                        {
                                            tempmap.Succ = 1;
                                        }

                                        tempmap.Apex = ApexCount;
                                        tempmap.Area = ContourArea;
                                        // 01.18 insert end


                                        temp.ContourCount = ContourCount;
                                        temp.Area = ContourArea;
                                        temp.Convex = Convex;
                                        temp.Apex = ApexCount;

                                        temp.moments = moments;     // 01.22 Insert

                                        temp.Die_Image = Die_Image; // 01.20 Insert 

                                        // 01.19 insert Start
                                        if (pMyContext.Found_Die.ContainsKey(MatrixPoint) == false)  // 중복된 KEY 가 없다면 추가.    01.19 추가
                                            pMyContext.Found_Die.Add(MatrixPoint, temp);

                                        if (pMyContext.MapData.ContainsKey(i) == false)  // 중복된 Key가 없다면 추가. 01.19일 추가
                                            pMyContext.MapData.Add(i, tempmap);
                                        // 01.19 insert end
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Logging.PrintErrLog((int)ELogType.Error, string.Format($"Col:{col}, Row:{row}, Exception:{ex.Message}"));
                                    //Debug.WriteLine($"Col:{col}, Row:{row}, Exception:{ex.Message}");
                                    //Debug.WriteLine($"COL Detect:{col}, ModelFinder num:{i}, value:{ModelResult.dXPos[i]}, min:{basic_Width * col}, max:{basic_Width * (col + 1)}");
                                    //Debug.WriteLine($"ROW Detect:{row}, ModelFinder num:{i}, value:{ModelResult.dYPos[i]}, min:{basic_Height * row}, max:{basic_Height * (row + 1)}");
                                }
                                // Create Wafer Map 01.17 End



                                /*
                                // 2024.01.17 주석 처리. (NoMap 상태에서 이미지 기준 MapFile 생성. ModelFinder에서 찾은 위치만 저장.)
                                line = String.Format("num= {0} X= {1:0.000} Y= {2:0.000} Angle= {3:0.000}", i, pMyContext.dFoundModelXWorld[i],
                                    pMyContext.dFoundModelYWorld[i], ModelResult.dAngle[i]);
                                */

                                // 2024.01.17 insert (MapFile 기준으로 헤더 정보 및 Tail 정보를 제외한 동일한 포맷으로 파일 내용 저장.)
                                // X, Y는 Matrix Shift 전 데이터이고, X1, Y1은 Matrix를 행과 열에 대해 여백 부분을 Shift 한 데이터.
                                // XP, YP: ModelFinder 에서 찾은 Die의 좌표 값.
                                // B: 10 (임의의 값)
                                // S: 0 (임의의 값) -> 1: 정상 Die, 2: 존재하지만 Die가 불량일때 (01.19변경)
                                line = String.Format($"X= {col} Y= {row}  B= 10, X1 = {MatrixPoint.X} Y1 ={MatrixPoint.Y} " +
                                    $"XP ={pMyContext.dFoundModelXWorld[i]:0.0000} YP ={pMyContext.dFoundModelYWorld[i]:0.0000} S ={tempmap.Succ}");  // 01.18 tempmap.Succ 값으로 S 값 할당.
                                                                                                                                                      //$"XP ={pMyContext.dFoundModelXWorld[i]} YP ={pMyContext.dFoundModelYWorld[i]} S =1"); // 01.18 NoMapFile 모델에 대해 S값을 모조건 1로 기입하던걸 위와 같이 수정.

                                writer.WriteLine(line);
                            }
                            //Debug.WriteLine($"Min_Col:{Col_Min}, Max_Col:{Col_Max}, Min_Row:{Row_Min}, Max_Row:{Row_Max}");

                            // 01.18 insert Start
                            /*
                             *  01.18 이미지를 처음 로드하여 Die의 정보를 이용한 MapFile 생성시 Out of Range Exception이 발생하여 
                             *  찾아낸 Col_Min과 Row_Min이 레시피 데이터와 같지 않다면, Wafer Map을 크게 생성하여 그리게 수정함.
                             *  Shift Col/Row 파라메터에 저장된 레시피를 저장 후 재 로드 루 사용하다면, 좌/상단의 여분이 없이 Map을 그리게 됨.
                             *  레시피를 저장하지 않고 재 검사시에도 좌/상단의 여분 없이 그려지지만, 프로그램 재시작 시 동일한 과정을 거치게 됨.
                             */
                            if ((pMyParam.Shift_Col != Col_Min) && (pMyParam.Shift_Row != Row_Min))
                            {
                                nMaxCol = NoMap_Col;
                                nMaxRow = NoMap_Row;

                                pMyParam.Shift_Col = Col_Min;
                                pMyParam.Shift_Row = Row_Min;
                            }
                            else
                            {
                                nMaxCol = Col_Max; //- 1;      // 01.17 Wafer Map 최대 Col 개수. (Minus 열의 오른쪽 여백 열의 개수)
                                nMaxRow = Row_Max; //- 2;      // 01.17 Wafer Map 최대 Row 개수. (Minus 행의 아래쪽 여백 행의 개수)
                            }
                            // 01.18 insert End

                        }
                        pMyContext.bFoundModel = true;        // 01.18 insert
                        Step = (int)EStep.End;
                    }
                    // map파일 사용하지 않고 Modelfinder에서 찾은 Die를 파일에 저장. MapFile이 없는 상태에서 Die 위치 검증용으로 요청하여 작성 (12.06) END
                    else
                    {
                        Step++;
                    }
                    #endregion unused mapfile End

                    pMyContext.bFoundModel = true;        // origin
                    break;
                case EStep.MapMapping:
                    if ((pMyContext.bFoundCircle == true) && (pMyContext.bFoundModel == true))   // insert jdhan
                    {
                        MapSearch();
                        MapfileSave();
                    }
                    else if ((pMyContext.bFoundCircle == false) && (pMyContext.bFoundModel == true))     // insert jdhan
                        WaferMapMapping();
                    else        // insert jdhan
                    {
                        Logging.PrintLog((int)ELogType.Result, string.Format("Model Found or Circle Found Fail:{0}", result));
                        pMyContext.ModelFinderResult = EVisionResultType.NG;
                        FinishAction(EContextResult.Fail);
                        break;
                    }
                    Step++;
                    break;
                case EStep.End:

                    pMyContext.nDie_Height = pMyParam.DieHeight;        // insert jdhan
                    pMyContext.nDie_Width = pMyParam.DieWidth;          // insert jdhan
                    pMyContext.WaferDegree = pMyParam.RotateWafer;      // insert jdhan
                    pMyContext.Die_Grade = pMyParam.Die_Grade;          // insert jdhan

                    pMyContext.dFoundCount = ModelResult.nCnt;          // Model Find 에서 찾은 Die 개수.
                    pMyContext.DieTotal = pMyContext.Found_Die.Count;   // Map File에 존재하는 위치에 대응하는 Die 유/무 확인 된 Die 개수.

                    //Debug.WriteLine($"OriginPoint Count:{OriginPoint.Count}");
                    //Debug.WriteLine($"OriginData Count:{OriginData.nS.Count}");

                    pMyContext.nMaxCol = nMaxCol;
                    pMyContext.nMaxRow = nMaxRow;

                    //Debug.WriteLine($"Diameter:{pMyContext.dRadius * 2}, Diameter/WaferDieCol:{pMyContext.dRadius*2 / nMaxCol}, Diameter/WaferDieRow:{pMyContext.dRadius*2 / nMaxRow}");

                    pMyContext.nTotalCell = nMaxCol * nMaxRow;

                    int index = 0;
                    double[] zData = new double[pMyContext.nTotalCell];
                    for (int i = 0; i < pMyContext.nTotalCell; i++)
                    {
                        zData[i] = Chart.NoValue;
                    }
                    try
                    {
                        // NoMap Die Map Create 01.16 insert Start
                        if (pMyParam.MapCheck == true)
                        {
                            foreach (var item in pMyContext.Found_Die)
                            {
                                index = (item.Key.X % pMyContext.nMaxCol) + (item.Key.Y * pMyContext.nMaxCol);
                                zData[index] = 10;
                            }
                        }
                        else
                        {
                            foreach (var item in pMyContext.MapData)
                            {
                                index = (item.Value.Tgt_X % pMyContext.nMaxCol) + (item.Value.Tgt_Y * pMyContext.nMaxCol);
                                //zData[index] = item.Value.Succ;       // 1 or 0 표기
                                //zData[index] = item.Value.Bin;          // MapFile의 BIN 값으로 표기

                                // 찾은 Die 가 원본 map file에서 가진 Bin 값으로 그래프에 표현하기 위해 수정.
                                //if (item.Value.Succ == 1)
                                if (item.Value.Succ != 0)       // Pass: 1, FAIL: 2, 존재하지 않는 Die : 0
                                    zData[index] = item.Value.Bin;
                                else
                                    zData[index] = 0;
                                //zData[index] = Chart.NoValue;
                            }
                        }
                        // NoMap Die Map Create 01.16 insert End

                    }
                    catch (Exception e)
                    {
                        Logging.PrintErrLog((int)ELogType.Error, string.Format("Exception while {0} in {1}, ReturnCode:{2}, ({3})", "agtAM_FindModel", ID.ToString(), result, e.Message));
                        Logging.PrintErrLog((int)ELogType.Error, string.Format($"index num:{index} Out of Range! In Range:{pMyContext.nTotalCell}"));   // 01.18 insert
                        pMyContext.MapFileResult = EVisionResultType.NG;
                        FinishAction(EContextResult.Error);
                        break;
                    }

                    int nCharXSize = ((int)ReringProject.UI.WaferMapView.dNewWidth == 0) ? 620 : (int)ReringProject.UI.WaferMapView.dNewWidth;
                    int nCharYSize = ((int)ReringProject.UI.WaferMapView.dNewHeight == 0) ? 740 : (int)ReringProject.UI.WaferMapView.dNewHeight;

                    pMyContext.WaferMap = new XYChart(nCharXSize, nCharYSize);

                    pMyContext.WaferMap.addTitle("Wafer File" + sFileName, "Arial Bold", 8);
                    pMyContext.plot = pMyContext.WaferMap.setPlotArea(40, 60, nCharXSize - 100, nCharYSize - 100, -1, -1, Chart.Transparent, 0xdddddd, 0xdddddd);
                    pMyContext.layer = pMyContext.WaferMap.addDiscreteHeatMapLayer(zData, pMyContext.nMaxCol);

                    pMyContext.WaferMap.xAxis().setLinearScale(0, pMyContext.nMaxCol, 1);
                    pMyContext.WaferMap.xAxis().setLabelStyle("Arial Bold", 8);
                    pMyContext.WaferMap.xAxis().setColors(Chart.Transparent, Chart.TextColor);
                    pMyContext.WaferMap.xAxis().setLabelOffset(0.5);
                    pMyContext.WaferMap.setXAxisOnTop();

                    pMyContext.WaferMap.yAxis().setLinearScale(0, pMyContext.nMaxRow, 1);
                    pMyContext.WaferMap.yAxis().setLabelStyle("Arial Bold", 8);
                    pMyContext.WaferMap.yAxis().setColors(Chart.Transparent, Chart.TextColor);
                    pMyContext.WaferMap.yAxis().setLabelOffset(0.5);
                    pMyContext.WaferMap.yAxis().setReverse();

                    pMyContext.cAxis = pMyContext.layer.setColorAxis(pMyContext.plot.getRightX() + 20, pMyContext.plot.getTopY(), Chart.TopLeft, pMyContext.plot.getHeight(), Chart.Right);
                    pMyContext.cAxis.setLabelStyle("Arial Bold", 8);

                    pMyContext.bMapList = true;

                    pMyContext.MapFileResult = EVisionResultType.OK;
                    FinishAction(EContextResult.Pass);

                    break;
            }
            return base.Run();
        }

    }
}
