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
        public double X_Pos { get; set; }       // Pixel Coordination X
        public double Y_Pos { get; set; }       // Pixel Coordination Y
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

        public Found_Die_Info Clone()
        {
            Found_Die_Info newItem = new Found_Die_Info();
            newItem.Angle = Angle;
            newItem.Apex = Apex;
            newItem.Area = Area;
            newItem.ContourCount = ContourCount;
            newItem.Convex = Convex;
            newItem.Die_Image = Die_Image;
            newItem.Height = Height;
            newItem.moments = moments;
            newItem.Width = Width;
            newItem.WX_Pos = WX_Pos;
            newItem.WY_Pos = WY_Pos;
            newItem.X_Pos = X_Pos;
            newItem.Y_Pos = Y_Pos;
            return newItem;
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
        public double dAngle { get; set; }          // Die ?뚯쟾 媛곷룄 (?쒖뼱遺???섍꺼??Wafer媛 蹂댁젙?댁빞 ?섎뒗 媛곷룄)
        public double dRadius { get; set; }     // pixel
        public double dRadmm { get; set; }      // mm

        public bool bEraseChip { get; set; }
        public double dEraseLength { get; set; }
        public double dErasemm { get; set; }



        public EVisionResultType CircleFoundResult { get; set; }
        public EVisionResultType AngleFoundResult { get; set; }
        public EVisionResultType TechingResult { get; set; }  // 05.20 Insert

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
        public double WaferDegree { get; set; }     // Wafer ?ъ엯??Map file???뚯쟾 媛곷룄 ?낅젰 parameter 媛?
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

        // ModelFinder?먯꽌 李얠? Die?먯꽌 MapFile??議댁옱?섎뒗 Die留????
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
        /// WaferScanCalibration?먯꽌 Camera Calibration ?섑뻾 ?섏뿬 ?살? ?곗씠?곕? ??ν븷 蹂??
        /// MMPerPixel_X
        /// MMPerPixel_Y
        /// </summary>
        public double MMPerPixel_X { get; set; }
        public double MMPerPixel_Y { get; set; }

        public double ScreenCenter_Xmm { get; set; }    // 12.17 insert
        public double ScreenCenter_Ymm { get; set; }    // 12.17 insert

        public string MapFileName { get; set; } // 03.20 Insert


        /// <summary>
        /// ChartDirector variable
        /// </summary>
        public XYChart WaferMap = null;
        public PlotArea plot = null;
        public DiscreteHeatMapLayer layer = null;
        public ColorAxis cAxis = null;

        public Boolean TeachingImage { get; set; }  // 07.05 Insert

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

            ScreenCenter_Xmm = 0.0; // 12.17 insert
            ScreenCenter_Ymm = 0.0; // 12.17 Insert

            MapFileName = "None";   // 03.20 insert

            TeachingImage = false;  // 07.05 Insert

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


        //[Line, Converter(typeof(LineConverter))]
        //public ReringProject.UI.Line ToolLine { get; set; }




        //[DisplayName("Line length(um)")]
        //[ReadOnly(true)]
        //public int LineLength
        //{
        //    get { return _LineLength; }
        //    set
        //    {
        //        _LineLength = value;
        //        RaisePropertyChanged("LineLength");
        //    }
        //}

        //private int _LineLength;





        [CheckableItems("Erase Chip", "Erase Chip")]
        [DisplayName("Erase Chip")]
        public bool EraseChip
        {
            get { return _EraseChip; }
            set
            {
                _EraseChip = value;
                RaisePropertyChanged("EraseChip");
            }
        }
        private bool _EraseChip;
        
        [DisplayName("Erase Edge Length(um)")]
        public double EraseEdgeLength
        {
            get
            {
                return _EraseEdgeLength;
            }
            set
            {
                _EraseEdgeLength = value;
                RaisePropertyChanged("EraseEdgeLength");
            }
        }
        private double _EraseEdgeLength;

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
        //[Spinnable(1, 5, 0, 20000)]       // 02.22 二쇱꽍 泥섎━.
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
        //[Spinnable(1, 5, 0, 20000)]   // 02.22 二쇱꽍 泥섎━.
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
        //[Spinnable(0.5, 10, 0, 10)]   // 02.22 二쇱꽍 泥섎━.
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

        //[Spinnable(0.5, 10, 0, 10)]   // 02.22 二쇱꽍 泥섎━.
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

        //[Spinnable(0.1, 2.0, 0.0, 2.0)]   // 02.22 二쇱꽍 泥섎━.
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

        [DisplayName("X-Dist SC To WC(mm)")]
        public double ScreenCenterToCircleCenter_X
        {
            get { return _ScreenCenterToCircleCenter_X; }
            set
            {
                _ScreenCenterToCircleCenter_X = value;
                RaisePropertyChanged("ScreenCenterToCircleCenter_X");
            }
        }
        private double _ScreenCenterToCircleCenter_X;

        [DisplayName("Y-Dist SC To WC(mm)")]
        public double ScreenCenterToCircleCenter_Y
        {
            get { return _ScreenCenterToCircleCenter_Y; }
            set
            {
                _ScreenCenterToCircleCenter_Y = value;
                RaisePropertyChanged("ScreenCenterToCircleCenter_Y");
            }
        }
        private double _ScreenCenterToCircleCenter_Y;

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



        //[Spinnable(1, 100, 10, 100), Browsable(true)]  // Vitual Map???ъ슜??BinNumber濡??ъ슜.
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

        [Browsable(true)]
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

        // 11.22 Insert
        [DisplayName("Wafer Threshold")]
        public int WaferThreshold
        {
            get { return _WaferThreshold; }
            set
            {
                _WaferThreshold = value;
                RaisePropertyChanged("WaferThreshold");
            }
        }
        private int _WaferThreshold;

        // 11.25 Insert
        [CheckableItems("WaferTHViewEnable", "WaferTHViewEnable")]
        [DisplayName("Wafer TH View Enable")]
        public bool WaferTHEnable
        {
            get { return _WaferTHEnable; }
            set
            {
                _WaferTHEnable = value;
                RaisePropertyChanged("WaferTHEnable");
            }
        }
        private bool _WaferTHEnable;


        [Category("Die Inspect")]
        //[Spinnable(0.1, 0.9, 0.1, 0.9)]
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

        //[Spinnable(0.1, 0.9, 0.1, 0.9)]
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

        //[Spinnable(1, 10, 120, 255)]
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

        //[Spinnable(1, 10, 120, 255)]
        [DisplayName("Morphology Threshold")]
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

        [DisplayName("Morphology Kernel")]
        public int Mophology_KernelSize
        {
            get { return _Mophology_KernelSize; }
            set
            {
                _Mophology_KernelSize = value;
                RaisePropertyChanged("Mophology_KernelSize");
            }
        }
        private int _Mophology_KernelSize;

        [DisplayName("Morphology Iteration")]
        public int Mophology_Iteration
        {
            get { return _Mophology_Iteration; }
            set
            {
                _Mophology_Iteration = value;
                RaisePropertyChanged("Mophology_Iteration");
            }
        }
        private int _Mophology_Iteration;

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

        [CheckableItems("UnusedMap", "UnusedMap"), Browsable(true)]
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

        //[Spinnable(1, 10, 0, 10)]
        [DisplayName("Left Shift Col"), Browsable(true)]
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

        //[Spinnable(1, 10, 0, 10)]
        [DisplayName("Up Shift Row"), Browsable(true)]
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


        [Category("Teaching & Debug Condition")]
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

        

        public new string SequenceName { get; set; }
        public new string ActionName { get; set; }
        public new string OwnerName { get; set; }

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



            //ToolLine.RegisterEvent(LineEvent);
        }

        public void LineEvent(double x1, double y1, double x2, double y2)
        {
            double length_px = Math.Sqrt(((x1 - x2) * (x1 - x2)) + ((y1 - y2) * (y1 - y2)));
            //LineLength = (int)(length_px * PixelToUM_Offset);
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
                minScore /= 100.0f; //二쇱뼱吏?score??100?⑥쐞 ?대?濡?

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
            if (CalCheck)   // Calibration file ?ъ슜
            {
                if (ALLIGATOR_ALG_MIL.agtAM_GridCalibrationAssociate(AlgIndex, CalibrationIndex) != 0)
                {
                    CustomMessageBox.Show("Fail to New Model", string.Format("{0} - Grid Calibration Fail. AlgIndex : {1}, ModelIndex : {2}, File : {3}", this.GetType().Name, AlgIndex, ModelIndex, ModuleModel.ModelFile), MessageBoxImage.Error);
                }
            }
            else
            {
                ALLIGATOR_ALG_MIL.agtAM_GridCalibrationDeAssociate(AlgIndex, CalibrationIndex);
                ALLIGATOR_ALG_MIL.agtAM_GridCalibrationModelDeAssociate(AlgIndex, ModelIndex, CalibrationIndex);
            }

            //吏?뺣맂 ?곸뿭?쇰줈 ??紐⑤뜽 ?앹꽦
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
                // 02.20 New Dll ?곸슜 ??Error 諛쒖깮???곕Ⅸ 二쇱꽍 泥섎━.
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

            //紐⑤뜽 ?뚯씪???덈떎硫?濡쒕뱶???? edit 李쎌쓣 ?곕떎.
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

            //?놁쑝硫?吏?뺣맂 ?곸뿭?쇰줈 ?앹꽦
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
            //model file? ??긽 recipe directory ?대?????ν븳?? (?몃? ?뚯씪 ?ъ슜 ?? ?뚯씪 ?좎떎濡??명븳 recipe ?먯떎??諛쒖깮?????덉쑝誘濡?
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

                WaferParam.ScreenCenterToCircleCenter_X = ScreenCenterToCircleCenter_X; // Insert 11.04
                WaferParam.ScreenCenterToCircleCenter_Y = ScreenCenterToCircleCenter_Y; // Insert 11.04

                WaferParam.Min_ArcLength = Min_ArcLength;
                WaferParam.OrientSearch = OrientSearch;

                WaferParam.Die_Width_Ratio = Die_Width_Ratio;
                WaferParam.Die_Height_Ratio = Die_Height_Ratio;
                WaferParam.Die_Area_Ratio = Die_Area_Ratio;
                WaferParam.Binary_Threshold = Binary_Threshold;
                WaferParam.Mophology_Threshold = Mophology_Threshold;
                WaferParam.InspectionROI = InspectionROI;   // Insert 02.28

                WaferParam.Mophology_KernelSize = Mophology_KernelSize; // Insert 08.28
                WaferParam.Mophology_Iteration = Mophology_Iteration;   // Insert 08.28

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

                WaferParam.Die_Grade = Die_Grade;
                WaferParam.Die_GradeS = Die_GradeS;
                WaferParam.VMDieEnabel = VMDieEnabel;

                WaferParam.WaferThreshold = WaferThreshold; // 12.06 Insert
                WaferParam.WaferTHEnable = WaferTHEnable;   // 12.06

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
        private Mat MophologyImage = null; // Binary or Mopology Image 02.28

        private Mat GrabToThresholdImage = null;   // Convert Grab Image to ThresholdImage 11.22

        private int result = -1;
        

        /// <summary>
        /// Maching?쇰줈 Map mapping 蹂?섎뱾
        /// </summary>
        //Wafer map mapping 愿??
        static List<int> xMagicIndices = new List<int>();
        static List<int> yMagicIndices = new List<int>();
        static List<string> strHeaderLines = new List<string>();
        static List<string> strFooterLines = new List<string>();
        // 援ъ“泥??뺤쓽
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
        /// Circle find ??map mapping 蹂?섎뱾
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

            // Center媛 Scribe?먯엳?붿? ?뺤씤
            public bool bScribX;
            public bool bScribY;

            // Center
            public double dCenterX;
            public double dCenterY;
            // Trans??Center
            public double dTCenterX;    // Wafer matrix ?됱쓽 Center X
            public double dTCenterY;    // Wafer matrix ?댁쓽 Center Y


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
        // 援ъ“泥??뺤쓽
        struct FoundPoint

        {
            public double X;
            public double Y;
            public int Status;
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

        Dictionary<int, double> VM_COLSW = new Dictionary<int, double>();        // Virtual Map COLUMS World Coordination
        Dictionary<int, double> VM_ROWSW = new Dictionary<int, double>();        // virtual map ROWS World Coordination
        // 04.02 Insert End


        #region MAPFILE_Variable
        // Map File Analysis Variable
        public Dictionary<int, _ST_MAP_INFO> m_MapInfo = new Dictionary<int, _ST_MAP_INFO>();
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

            // 02.22 insert Start
            // Wafer Line Angle 寃異쒖뿉 OrientSearch  ?⑥닔 ?ъ슜?섏? ?딆쓬.
            pMyParam.OrientSearch = false;
            pMyParam.CalCheck = true;
            // 02.22 insert End

            // 02.28 Insert Start
            if ((pMyParam.InspectionROI != WaferScanInspectionParam.Die_ROI.InnerROI) || (pMyParam.InspectionROI != WaferScanInspectionParam.Die_ROI.OuterROI) || (pMyParam.InspectionROI != WaferScanInspectionParam.Die_ROI.Both))
                pMyParam.InspectionROI = (WaferScanInspectionParam.Die_ROI)pMyParam.InitROI;    // 03.04 肄붾뱶 蹂寃?
            else
                pMyParam.InspectionROI = (WaferScanInspectionParam.Die_ROI)pMyParam.InitROI;    // 03.04 肄붾뱶 蹂寃?
            // 02.28 Insert End

            // 11.04 Insert Start
            if (pMyParam.ScreenCenterToCircleCenter_X <= 0)
                pMyParam.ScreenCenterToCircleCenter_X = 6.0;
            if (pMyParam.ScreenCenterToCircleCenter_Y <= 0)
                pMyParam.ScreenCenterToCircleCenter_Y = 12.0;
            // 11.04 Insert End

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

            // 11.04 Insert Start
            if (pMyParam.ScreenCenterToCircleCenter_X <= 0)
                pMyParam.ScreenCenterToCircleCenter_X = 6.0;
            if (pMyParam.ScreenCenterToCircleCenter_Y <= 0)
                pMyParam.ScreenCenterToCircleCenter_Y = 12.0;
            // 11.04 Insert End

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

            OriginData.dSizeX = pMyParam.DieWidth + (pMyParam.Scrib_X * (pMyContext.MMPerPixel_X * 1000));  // Scrib Line ???ы븿??Die X Size
            OriginData.dSizeY = pMyParam.DieHeight + (pMyParam.Scrib_Y * (pMyContext.MMPerPixel_Y * 1000)); // Scrib Line ???ы븿??Die Y Size

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
                        // ?뺢퇋???⑦꽩???ъ슜?섏뿬 媛믪쓣 異붿텧
                        MatchCollection matches = Regex.Matches(line, @"[A-Z]= \d+");

                        int xValue = 0, yValue = 0, bValue = 0;

                        // 異붿텧??媛?留ㅼ튂瑜?諛섎났?섎㈃??媛믪쓣 ?쎌뼱 ?ㅼ엫
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

            CenterPositionGet(out OriginData.dCenterX, out OriginData.dCenterY);        // Center Position??Scrib Line??議댁옱?섎뒗吏, Die ??議댁옱?섎뒗吏 ?뺤씤.
            Translation_Data(pMyParam.RotateWafer, pMyParam.Die_Grade);

            Debug.WriteLine($"Translation Center Position: {OriginData.dTCenterX} {OriginData.dTCenterY}");
            Debug.WriteLine($"OrinPoint: {OriginPoint.Values}");


            return (dataList);
        }


        // 03.27 Insert - Create Virtual Map Matching Position.
        // 諛섎뱶?? ReadMap怨?Circle Find ?댄썑???대떦 ?⑥닔瑜??몄텧?섏뿬????
        // Circle 以묒떖?먯꽌 遺??Die ?꾩튂?쇨퀬 ?앷컖?섎뒗 遺遺꾩쓽 ?꾩튂瑜?怨꾩궛.
        public void CreateVM()
        {
            VMdataList.Clear();

            double Die_Width_mm = pMyParam.DieWidth / 1000;     // Die width mm
            double Die_Height_mm = pMyParam.DieHeight / 1000;   // die Height mm

            double Die_Half_Width_mm = Die_Width_mm / 2;        // die Half width mm
            double Die_Half_Height_mm = Die_Height_mm / 2;      // die Half Height mm

            double Scrib_X_mm = pMyParam.Scrib_X * pMyContext.MMPerPixel_X; // Scrib mm (Pixel * 1-Pixel per mm)
            double Scrib_Y_mm = pMyParam.Scrib_Y * pMyContext.MMPerPixel_Y; // Scrib mm (Pixel * 1-Pixel per mm)

            // ?붾㈃ 以묒떖???먯젏(0,0)?대?濡? Wafer Circle???붾㈃ 以묒떖源뚯? ?⑥뼱吏?嫄곕━瑜??섑??대ŉ, circle 以묒떖?먯씠 ?붾㈃ 以묒떖?쇰줈 ?대룞?댁빞 ?섎뒗 媛?
            double DistX = pMyContext.CenterOffsetXmm;
            double DistY = pMyContext.CenterOffsetYmm;

            // ?됰젹???쒖옉?먯씠 0?먯꽌 ?쒖옉 ?섎?濡? ?ㅼ젣 ?됯낵 ?댁쓽 媛쒖닔??+1 ?댁빞 ??
            double XCenter = Math.Truncate(OriginData.dTCenterX);   // map file ?됱쓽 以묒떖 - ?뚯닔???쒖쇅
            double YCenter = Math.Truncate(OriginData.dTCenterY);   // map file ?댁쓽 以묒떖 - ?뚯닔???쒖쇅

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

                        // ??以묒떖???쇱そ???덈떎硫?
                        if((OriginData.dTCenterX - i) < 1)  // ?쇱そ 諛붾줈 ??
                        {
                            key = i;

                            X_Value = pMyContext.dFoundCenterXWorld - (Scrib_X_mm/2) - Die_Half_Width_mm;     // Find Circle Center
                        }
                        else
                        {
                            key = i;
                            colNum = (int)Math.Truncate(OriginData.dTCenterX - i);

                            X_Value = pMyContext.dFoundCenterXWorld - (colNum * (Die_Width_mm + Scrib_X_mm)) - (Scrib_X_mm/2) - Die_Half_Width_mm;    // Find Circle Center
                        }
                    }
                    else
                    {
                        if ((OriginData.dTCenterX - i) == 0)
                        {
                            key = i;

                            X_Value = pMyContext.dFoundCenterXWorld;  // Find Circle Center
                        }
                        else
                        {
                            key = i;
                            colNum = (int)Math.Truncate(i - OriginData.dTCenterX);

                            X_Value = pMyContext.dFoundCenterXWorld + (colNum * (Die_Width_mm + Scrib_X_mm)) + (Scrib_X_mm/2) + Die_Half_Width_mm;    // Find Circle Center
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
                    }
                    else
                    {
                        if((i - OriginData.dTCenterX) == 0)
                        {
                            key = i;

                            X_Value = pMyContext.dFoundCenterXWorld;  // Find Circle Center
                        }
                        else
                        {
                            key = i;
                            colNum = (int)Math.Truncate(i - OriginData.dTCenterX);

                            X_Value = pMyContext.dFoundCenterXWorld + (colNum * (Die_Width_mm + Scrib_X_mm)); // Find Circle Center
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
                        // ??以묒떖???꾩そ???덈떎硫?
                        if ((OriginData.dTCenterY - i) < 1)  // ?꾩そ 諛붾줈 ??
                        {
                            key = i;

                            Y_Value = pMyContext.dFoundCenterYWorld - (Scrib_Y_mm / 2) - Die_Half_Height_mm;  // Find Circle Center
                        }
                        else
                        {
                            key = i;
                            rowNum = (int)Math.Truncate(OriginData.dTCenterY - i);

                            Y_Value = pMyContext.dFoundCenterYWorld - (rowNum * (Die_Height_mm + Scrib_Y_mm)) - (Scrib_Y_mm / 2) - Die_Half_Height_mm;        // Find Circle Center
                        }
                    }
                    else
                    {
                        if ((OriginData.dTCenterY - i) == 0)
                        {
                            key = i;

                            Y_Value = pMyContext.dFoundCenterYWorld;  // Find Circle Center
                        }
                        else if ((i - OriginData.dTCenterY) < 1)  // 以묒떖?먯꽌 諛붾줈 ?꾨옒
                        {
                            key = i;
                            Y_Value = pMyContext.dFoundCenterYWorld + (Scrib_Y_mm / 2) + Die_Half_Height_mm;  // Screen Center
                        }
                        else
                        {
                            key = i;
                            rowNum = (int)Math.Truncate(i - OriginData.dTCenterY);

                            Y_Value = pMyContext.dFoundCenterYWorld + (rowNum * (Die_Height_mm + Scrib_Y_mm)) + (Scrib_Y_mm / 2) + Die_Half_Height_mm;    // Find Circle Center
                        }
                    }

                    Matrix_ROWS.Add(key, Y_Value);
                }
                else
                {
                    if ((OriginData.dTCenterY - i) == 0)
                    {
                        key = i;

                        Y_Value = 0;    // Screen Center
                    }
                    else
                    {
                        if(i < OriginData.dTCenterY)
                        {
                            key = i;
                            rowNum = (int)Math.Truncate(OriginData.dTCenterY - i);

                            Y_Value = -(rowNum * (Die_Height_mm + Scrib_Y_mm)); // Screen Center
                        }
                        else
                        {
                            key = i;
                            rowNum = (int)Math.Truncate(i - OriginData.dTCenterY);

                            Y_Value = (rowNum * (Die_Height_mm + Scrib_Y_mm));  // Screen Center
                        }
                    }

                    Matrix_ROWS.Add(key, Y_Value);
                }
            }

            // OriginData.nDieCnt : 留듯뙆?쇱쓽 議댁옱?섎뒗 Die??媛쒖닔 (??* ??
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
            pMyContext.ResultHalconImage.GetImageSize(out HTuple imageWidth, out HTuple imageHeight);
            int nImgWidth = imageWidth.I;
            int nImgHeight = imageHeight.I;
            var xCompensation = (0.5 - Statistics.Mean(xFilteredData)) * dWidth;
            var yCompensation = (0.5 - Statistics.Mean(yFilteredData)) * dHeight;

            // ?꾩튂 蹂댁젙媛믪씠 ?곸슜??Map read
            (xPosIndices, yPosIndices, xMods, yMods, xPoss, yPoss) = ModelPosIndexing(xCompensation, yCompensation);

            // ?몃뜳?ㅽ솕???꾩튂 醫뚰몴 異쒕젰
            xNpMods = xMods.ToArray();
            yNpMods = yMods.ToArray();
            xOutliers = DetectOutliersZScore(xNpMods, threshold: 3);
            yOutliers = DetectOutliersZScore(yNpMods, threshold: 3);
            xFilteredData = RemoveOutliers(xNpMods, xOutliers);
            yFilteredData = RemoveOutliers(yNpMods, yOutliers);

            //////////////////////////////////////////////////////////////////////////
            /// ?대?吏??醫뚰몴 ?먯쓣 洹몃┝
            /// Pos data
            int nMinXPosValue = xPosIndices.Min();
            int nMaxXPosValue = xPosIndices.Max();
            int nMinYPosValue = yPosIndices.Min();
            int nMaxYPosValue = yPosIndices.Max();
            int nXPosWidth = nMaxXPosValue - nMinXPosValue + 1;
            int nYPosWidth = nMaxYPosValue - nMinYPosValue + 1;
            Mat pos_image = new Mat(new OpenCvSharp.Size(nXPosWidth, nYPosWidth), MatType.CV_8U, Scalar.All(0));
            // ?대?吏 醫뚰몴? X, Y 媛믪쓣 ??ν븷 Dictionary ?앹꽦
            Dictionary<(int x, int y), CoordinateData> coordinateMap = new Dictionary<(int x, int y), CoordinateData>();
            for (int i = 0; i < xPosIndices.Count; i++)
            {
                pos_image.Set<byte>(yPosIndices[i] - nMinYPosValue, xPosIndices[i] - nMinXPosValue, 255);
                // ?대?吏 醫뚰몴 (x, y)? ??묓븯??X, Y 媛믪쓣 ???
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
            // ?쒗뵆由?留ㅼ묶 ?섑뻾
            Mat result = new Mat();
            Cv2.MatchTemplate(map_image, pos_image, result, TemplateMatchModes.CCoeffNormed);

            // ?좎궗??留듭뿉??理쒕? ?좎궗???꾩튂 李얘린
            double minVal, maxVal;
            OpenCvSharp.Point minLoc, maxLoc;
            Cv2.MinMaxLoc(result, out minVal, out maxVal, out minLoc, out maxLoc);

            //Die Inspection
            // ?대?吏 醫뚰몴????묓븯??X, Y 媛믪쓣 ?쎄퀬 異쒕젰
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
                    //data.Status = 1;      // ?섏젙
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
                    if (DieInspection(founddie, MatrixPoint, ref ContourCount, ref ContourArea, ref Convex, ref ApexCount, ref moments, ref Die_Image) == false) // 01.22
                    {
                        data.Status = 2;
                    }
                    else
                    {
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

            }

            pMyContext.nMaxCol = nXPosWidth;
            pMyContext.nMaxRow = nYPosWidth;
            pMyContext.nTotalCell = pMyContext.nMaxCol * pMyContext.nMaxRow;

            // 留??뚯씪 ?대떦 ?몃뜳?ㅼ뿉 ?꾩튂 醫뚰몴媛?異붽?
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
                // ?대?吏 醫뚰몴????묓븯??X, Y 媛믪쓣 ?쎄퀬 異쒕젰
                for (int i = 0; i < dataList.Count; i++)
                {
                    //Line
                    string line;
                    line = String.Format("X= {0:0000} Y= {1:0000} B= {2:00} X1= {3:0000} Y1= {4:0000} XP= {5:0.000} YP= {6:0.000} S= {7}",
                        dataList[i].X, dataList[i].Y, dataList[i].BinNum, dataList[i].X1, dataList[i].Y1,
                        dataList[i].XP, dataList[i].YP, dataList[i].Status);

                    writer.WriteLine(line);
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
        /// Center Position 媛?援ы븯??媛쒖닔 蹂寃? ?먮낯? +1 ???놁뿀?쇰굹, ?됯낵 ?댁쓽 媛쒖닔瑜??ㅺ퉼由ъ? ?딄쾶 ?섍린 ?꾪빐 ?듭씪?섏뿬
        /// ?ㅼ씠?쇳꽣?몄? ?ㅽ겕?쇱씠釉??쇳꽣?몄?瑜??뺤씤?섍쾶 蹂寃??? ( CTST?먯꽌 異쒗븯 ???섏젙??)
        /// <returns></returns>
        public void CenterPositionGet(out double dCenterX, out double dCenterY)
        {
            dCenterX = 0;
            dCenterY = 0;

            dCenterX = ((double)OriginData.nMinX + (double)OriginData.nMaxX) / 2;   // MAP File ?꾩껜 ?됰젹 湲곗???X 以묒븰 媛? (醫??곷떒 ?쇰줈 ?몃뜳?ㅼ쓽 ?쒖옉???대룞?섍린 ??
            dCenterY = ((double)OriginData.nMinY + (double)OriginData.nMaxY) / 2;   // MAP File ?꾩껜 ?됰젹 湲곗???Y 以묒븰 媛? (醫??곷떒 ?쇰줈 ?몃뜳?ㅼ쓽 ?쒖옉???대룞?섍린 ??

            // MapFile?먯꽌 ?됰젹??媛쒖닔??0 遺???쒖옉?섍린 ?뚮Ц???됰젹 媛쒖닔??+1???댁빞 媛쒖닔媛 ?뺥솗.
            // MapFile???됯낵 ?댁쓽 理쒕?媛쒖닔?먯꽌 理쒖냼 媛쒖닔瑜?鍮쇨퀬 + 1???섏? ?딅뒗 寃쎌슦, ?됰젹??媛쒖닔媛 ?ㅼ젣 媛쒖닔蹂대떎 -1 媛??묐떎.
            // ?곕씪???됰젹???ㅼ젣 媛쒖닔???됰젹??理쒕??몃뜳??- 理쒖냼?몃뜳??+ 1
            int cntCenterX = OriginData.nMaxX - OriginData.nMinX + 1;                   // + 1;   // 醫??곷떒 ?쇰줈 ?몃뜳?ㅼ쓽 ?쒖옉???대룞. ?몃뜳???쒖옉 ?レ옄??0
            int cntCenterY = OriginData.nMaxY - OriginData.nMinY + 1;                   // + 1;   // 醫??곷떒 ?쇰줈 ?몃뜳?ㅼ쓽 ?쒖옉???대룞. ?몃뜳???쒖옉 ?レ옄??0

            Debug.WriteLine($"Matrix - ROW count:{cntCenterX+1}, COLUMN Count:{cntCenterY+1}");

            if (cntCenterX % 2 != 0)    // ?댁쓽 珥?媛쒖닔媛 吏앹닔媛??쇰㈃, 以묒떖? ?ㅽ겕?쇱씠釉뚯뿉 ?덇퀬, ??섍컻?쇰㈃ 以묒떖? 媛?대뜲 Die 以묒떖.
            {
                OriginData.bScribX = false;   // Origin
            }
            else
            {
                OriginData.bScribX = true;    // Origin
            }

            if (cntCenterY % 2 != 0)    // ?됱쓽 珥?媛쒖닔媛 ????쇰㈃, 以묒떖? ?ㅽ겕?쇱씠釉뚯뿉 ?덇퀬, ??섍컻?쇰㈃ 以묒떖? 媛?대뜲 Die 以묒떖.
            {
                OriginData.bScribY = false;   // Origin
            }
            else
            {
                OriginData.bScribY = true;    // Origin
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


                OriginData.nS.Add(0);       // Original Map File??紐⑤뱺 Die ?꾩튂?????Translation Data 留?留뚮뱾湲??뚮Ц?? State(nS)??紐⑤몢 0. 
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

        /// <summary>
        /// 
        /// </summary>
        /// <param name="O_PosX">Model Finder Die Position World Position </param>
        /// <param name="O_PosY">Model Finder Die Position World Position </param>
        /// <param name="ModelPosX"> Rotation Matrix Position X </param>
        /// <param name="ModelPosY"> Rotation Matrix Position Y </param>
        /// <param name="MatrixPointX"> Die Translation Position X </param>
        /// <param name="MatrixPointY"> Die Translation Position Y </param>
        /// <param name="Status"> Die 議댁옱 ?щ? 媛?(1: 議댁옱, 0: ?놁쓬) </param>
        public void SetModelPosToDictionary(double O_PosX, double O_PosY, double ModelPosX, double ModelPosY, out int MatrixPointX, out int MatrixPointY, out int Status) // minho
        {

            // Model Find濡?李얠? ?꾩튂媛믪쓣 怨꾩궛?섏뿬 Dictionary???ｋ뒗??
            // ???⑥닔瑜??ъ슜?섍린 ?꾩뿉 Dictionary??Trans??醫뚰몴媛믪씠 ?ㅼ뼱媛 ?덉뼱???쒕떎

            System.Drawing.Point poSearchDieIdx = new System.Drawing.Point();
            FoundPoint poMovingDieDist = new FoundPoint();      // jdhan

            //double dResolution = pMyParam.PixelToMM_Offset;     // 1-Pixel (um)
            double dResolution = pMyParam.PixelToUM_Offset;     // 1-Pixel (um) 02.14
            double dCenterX = pMyContext.dFoundCenterX;         // circle CenterX Pixel
            double dCenterY = pMyContext.dFoundCenterY;         // circle CenterY Pixel
            pMyContext.ResultHalconImage.GetImageSize(out HTuple centerWidth, out HTuple centerHeight);
            double dImgCenterX = centerWidth.D / 2.0;      // Grab Image CenterX
            double dImgCenterY = centerHeight.D / 2.0;     // Grab Image CenterY

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

            // Dictionary ?ъ슜
            FoundPoint value = new FoundPoint
            {
                X = poMovingDieDist.X,
                Y = poMovingDieDist.Y,
                Status = 1
            };

            if (OriginPoint.ContainsKey(poSearchDieIdx))    // Key 媛믪씠 Dictionary???덈떎硫?
            {
                OriginPoint[poSearchDieIdx] = value;
                MatrixPointX = poSearchDieIdx.X;
                MatrixPointY = poSearchDieIdx.Y;
                Status = 1;
            }
            else
            {
                // Map File?먮뒗 ?놁?留? ?대?吏?먯꽌 Model Finder??Model 李얜뒗 湲곗???遺?⑺븯???곸뿭???됰젹 ?몃뜳??媛믪쓣 ?ｌ뼱???꾨떖.
                // 留??뚯씪 ?됰젹 ?몃뜳?ㅼ쓽 ????꾩튂 媛믪쓣 李얠? 紐삵븯??寃쎌슦, 留??뚯씪???쒓린 ?섏? ?딅뒗 ?됰젹??媛믪쑝濡??泥댄븯湲??꾪빐 ?ъ슜?섎룄濡??섏젙 ??
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
        private bool DieInspection(Found_Die_Info dieinfo, System.Drawing.Point MatrixPoint, ref int ContourCount, ref double ContourArea, ref bool Convex, ref int ApexCount, ref Moments moments, ref Mat Die_Image)   // 01.22 Die_Image ?낅젰 ?뚮씪硫뷀꽣 異붽?.
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
                    // Scrib Pixel ???낅젰 諛쏆븘 怨꾩궛??媛믪쑝濡?ROI ?곸뿭???곗텧.
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
                    x1 = (int)dieinfo.X_Pos - ((int)dieinfo.Width / 2);   // dieinfo.X_Pos??ModelFinder?먯꽌 李얠? Die 以묒떖媛믪씠誘濡? Die Width???덈컲??鍮쇱쨾???쒖옉??
                    x2 = (int)pMyParam.ModuleModel.Master.Width;
                    y1 = (int)dieinfo.Y_Pos - ((int)dieinfo.Height / 2);   // dieinfo.Y_Pos??ModelFinder?먯꽌 李얠? Die 以묒떖媛믪씠誘濡? Die Height???덈컲??鍮쇱쨾???쒖옉??
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
                RadBoundary = (pMyContext.dRadius / 3) * 2;  // Radious * 2/3    // pixel
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
                    // Wafer Circle Radious ??2/3 蹂대떎 ?щ㈃ Mophology Image瑜?Large Roi濡??ъ슜?섍퀬,
                    // ?묐떎硫?GrayImage(?먮낯)瑜?Large ROI濡??ъ슜?섏뿬 寃?ъ쭊??
                    if(DistDietoCenterPoint <= RadBoundary)
                        LdieRegion = new Mat(GrayImage, Lroi);  // Origin Image 02.27
                    else
                        LdieRegion = new Mat(MophologyImage, Lroi);    // Origin Image 02.27
                    // 03.04 Insert End
                }
                // 02.27 Insert End

                double area_threshold = (pMyParam.DieWidth / (pMyContext.MMPerPixel_X * 1000)) * (pMyParam.DieHeight / (pMyContext.MMPerPixel_Y * 1000));   // Standard Mode Die 硫댁쟻.
                double area_threshold_min = (pMyParam.DieWidth / (pMyContext.MMPerPixel_X * 1000)) * (pMyParam.DieHeight / (pMyContext.MMPerPixel_Y * 1000)) * pMyParam.Die_Area_Ratio; // Die 硫댁쟻??Parameter ratio % (minimum)
                double area_threshold_max = (pMyParam.DieWidth / (pMyContext.MMPerPixel_X * 1000)) * (pMyParam.DieHeight / (pMyContext.MMPerPixel_Y * 1000)) * (2.0 - pMyParam.Die_Area_Ratio); // Die 硫댁쟻??Parameter ratio % (maximum)
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

                        if ((pMyParam.MapCheck == false) && (ContourCount == 1) && (ContourArea >= area_threshold_min) && (Convex == false) && (ApexCount == 4)) // 11.25 Insert
                            resultSBool = true;
                        else
                            resultSBool = false;

                        if (pMyParam.SaveWaferImage == true)    // 05.23 Insert
                        {
                            // Saved Inner Die Image 
                            strMapPath = Path.Combine(SystemHandler.Handle.Setting.ImageSavePath,
                                    String.Format("{0:0000}{1:00}{2:00}/{3}", DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, pMyParam.MapName));
                            if (Directory.Exists(strMapPath) == false)
                                Directory.CreateDirectory(strMapPath);
                            strFileName = Path.Combine(strMapPath, String.Format("{0}_{1}_Image.png", MPoint.X, MPoint.Y)); //BMP -> png
                            Cv2.ImWrite(strFileName, Die_Image);
                        }

                        // 07.12 insert : Map File ?녿뒗 寃쎌슦 臾댁“嫄?true;
                        if (pMyParam.MapCheck == true)
                            resultSBool = true;

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

                        if (pMyParam.SaveWaferImage == true)    // 05.23 Insert
                        {
                            // Saved Outer Die Image
                            strMapPath = Path.Combine(SystemHandler.Handle.Setting.ImageSavePath,
                                    String.Format("{0:0000}{1:00}{2:00}/{3}", DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, pMyParam.MapName));
                            if (Directory.Exists(strMapPath) == false)
                                Directory.CreateDirectory(strMapPath);
                            strFileName = Path.Combine(strMapPath, String.Format("{0}_{1}_Image.png", MPoint.X, MPoint.Y)); // bmp -> png
                            Cv2.ImWrite(strFileName, LdieRegion);
                        }

                        // 07.12 insert : Map File ?녿뒗 寃쎌슦 臾댁“嫄?true;
                        if (pMyParam.MapCheck == true)
                            resultLBool = true;

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

                        if (pMyParam.SaveWaferImage == true)    // 05.23 Insert
                        {
                            // Saved Inner Die Image
                            strMapPath = Path.Combine(SystemHandler.Handle.Setting.ImageSavePath,
                                    String.Format("{0:0000}{1:00}{2:00}/{3}", DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, pMyParam.MapName));
                            if (Directory.Exists(strMapPath) == false)
                                Directory.CreateDirectory(strMapPath);
                            strFileName = Path.Combine(strMapPath, String.Format("{0}_{1}_Image.png", MPoint.X, MPoint.Y)); // bmp -> png
                            Cv2.ImWrite(strFileName, dieRegion);
                        }

                        // 07.12 insert : Map File ?녿뒗 寃쎌슦 臾댁“嫄?true;
                        if (pMyParam.MapCheck == true)
                            BothBool = true;

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

                        if (pMyParam.SaveWaferImage == true)    // 05.23 Insert
                        {
                            // Saved Outer Die Image
                            strMapPath = Path.Combine(SystemHandler.Handle.Setting.ImageSavePath,
                                String.Format("{0:0000}{1:00}{2:00}/{3}", DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, pMyParam.MapName));
                            if (Directory.Exists(strMapPath) == false)
                                Directory.CreateDirectory(strMapPath);
                            strFileName = Path.Combine(strMapPath, String.Format("{0}_{1}_Image.png", MPoint.X, MPoint.Y)); // bmp -> png
                            Cv2.ImWrite(strFileName, LdieRegion);
                        }

                        // 07.12 insert : Map File ?녿뒗 寃쎌슦 臾댁“嫄?true
                        if (pMyParam.MapCheck == true)
                            BothBool = true;

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

                        if (pMyParam.SaveWaferImage == true)    // 05.23 Insert
                        {
                            // Saved Error Die Image
                            strMapPath = Path.Combine(SystemHandler.Handle.Setting.ImageSavePath,
                                    String.Format("{0:0000}{1:00}{2:00}/{3}", DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, pMyParam.MapName));
                            if (Directory.Exists(strMapPath) == false)
                                Directory.CreateDirectory(strMapPath);
                            strFileName = Path.Combine(strMapPath, String.Format("{0}_{1}_Image.png", MPoint.X, MPoint.Y)); // bmp -> png
                            Cv2.ImWrite(strFileName, LdieRegion);
                        }

                        // 07.12 insert : Map File ?녿뒗 寃쎌슦 臾댁“嫄?true
                        if (pMyParam.MapCheck == true)
                            BothBool = true;

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

                return false; // 01.18 insert Exception 諛쒖깮 ?ъ씤??
            }

            double area_threshold = (pMyParam.DieWidth / (pMyContext.MMPerPixel_X * 1000)) * (pMyParam.DieHeight / (pMyContext.MMPerPixel_Y * 1000)); // ?ㅼ젣 Die??硫댁쟻

            double area_threshold_min = (pMyParam.DieWidth / (pMyContext.MMPerPixel_X * 1000)) * (pMyParam.DieHeight / (pMyContext.MMPerPixel_Y * 1000)) * pMyParam.Die_Area_Ratio; // Die 硫댁쟻??70%
            double area_threshold_max = (pMyParam.DieWidth / (pMyContext.MMPerPixel_X * 1000)) * (pMyParam.DieHeight / (pMyContext.MMPerPixel_Y * 1000)) * (2.0 - pMyParam.Die_Area_Ratio); // Die 硫댁쟻??130%
                                                                                                                                                                                            // 02.07 change start
            pMyParam.Die_Area = area_threshold; // Standard Real Die Area

            if (pMyParam.MapCheck)
            {
                area_threshold = pMyParam.ModuleModel.Master.Width * pMyParam.ModuleModel.Master.Height * pMyParam.Die_Area_Ratio;
            }

            double area = 0;
            //Moments moments;  //01.22 DieInspection ?⑥닔???몄옄濡?Moments ref 蹂?섎? ?낅젰 諛쏆븘 泥섎━?섎룄濡??섏젙?섏뿬 二쇱꽍 泥섎━??
            //bool convex = false;
            OpenCvSharp.Point[] approx = new OpenCvSharp.Point[10];

            List<OpenCvSharp.Point[]> new_contours = new List<OpenCvSharp.Point[]>();
            foreach (OpenCvSharp.Point[] p in contours)
            {
                double length = Cv2.ArcLength(p, true);

                // 01.22 Min_ArchLength 媛?湲곗??쇰줈 蹂寃? (OnLoad ?⑥닔?먯꽌 Min_ArcLength 媛믪쓣 怨꾩궛?섎룄濡?異붽???
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

                // 02.20 comment : pMyParam.Min_ArcLength 蹂대떎 ??媛믪씠 ?녿뒗 寃쎌슦, ?대떦 Matrix point???대?吏 泥섎━媛 ?꾩슂??蹂댁엫.
                Logging.PrintErrLog((int)ELogType.Error, $"Not Found New Contours count:{new_contours.Count}, X:{MatrixPoint.X}, Y:{MatrixPoint.Y}");

                ResultContourCount = 0;
                ResultContourArea = 0;
                ResultConvex = false;
                ResultApexCount = 0;
                ResultMoments = null;

                return false; // 01.18 insert Exception 諛쒖깮 ?ъ씤??
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
            ResultConvex = Cv2.IsContourConvex(new_contours.ElementAt(maxIndex));                       // insert jdhan 02.27 二쇱꽍 泥섎━.
            // Epsilon value (洹쇱궗移??뺥솗??媛 ??쓣 ?섎줉 ?뺣??섍쾶 洹쇱궗???섎ŉ, ?믪쓣 ?섎줉 ?뺣??꾧? ??븘 吏?
            // 洹쇱궗移??뺥솗?꾨뒗 ?낅젰???ㅺ컖???ㅺ낸??怨?諛섑솚??洹쇱궗?붾맂 ?ㅺ컖???ъ씠??理쒕? ?몄감 媛꾧꺽???섎?. (洹쇱궗移??뺥솗?꾩쓽 媛믪씠 ??쓣 ?섎줉, 洹쇱궗瑜????곴쾶???먮낯 ?ㅺ낸怨??좎궗)
            approx = Cv2.ApproxPolyDP(new_contours.ElementAt(maxIndex), Cv2.ArcLength(new_contours.ElementAt(maxIndex), false) * 0.010, true);    // insert jdhan 01.22 Epsilong value ?섏젙. (0.002 -> 0.005)
            Cv2.Circle(RoiImage, (int)(ResultMoments.M10 / ResultMoments.M00), (int)(ResultMoments.M01 / ResultMoments.M00), 5, Scalar.Red, -1);       // insert jdhan  10 -> 5
            Cv2.DrawContours(RoiImage, new_contours, maxIndex, new Scalar(0, 0, 255), 1, LineTypes.AntiAlias, null, 1);             // insert jdhan
            #endregion Max_Controu_Length End

            ResultImage = RoiImage.Clone();

            ResultContourArea = area;
            ResultContourCount = new_contours.Count;
            ResultApexCount = approx.Length;

            return true;
        }

        /// <summary>
        /// Wafer Angle ???곕Ⅸ 紐⑤뜽??Rotation Matrix Position valuel
        /// </summary>
        /// <param name="angle">Wafer Angle</param>
        /// <param name="X">Model Finder World X</param>
        /// <param name="Y">Model Finder World Y</param>
        /// <param name="RX">Rotation X</param>
        /// <param name="RY">Rotation Y</param>
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

            var dcenterx = pMyContext.dFoundCenterX;
            var dcentery = pMyContext.dFoundCenterY;
            var radius = pMyContext.dRadius;

            double clength = radius - pMyContext.dEraseLength;

            for (int i = 0; i < ModelResult.nCnt; i++)
            {
                Found_Die_Info temp = new Found_Die_Info();
                System.Drawing.Point MatrixPoint = new System.Drawing.Point();
                int MatrixPointX = 0;
                int MatrixPointY = 0;
                int Status = 0;

                bool bErase = false;









                //if (pMyParam.EraseChip == true)
                //{

                //    double Scrib_X_um = pMyParam.Scrib_X * (pMyContext.MMPerPixel_X * 1000);   // 01.22 Insert
                //    double Scrib_Y_um = pMyParam.Scrib_Y * (pMyContext.MMPerPixel_Y * 1000);   // 01.22 Insert


                //    double ecx = ModelResult.dXPos[i];
                //    double ecy = ModelResult.dYPos[i];



                //    //int x1 = (int)(ecx - (((pMyParam.DieWidth + Scrib_X_um) / (pMyContext.MMPerPixel_X * 1000)) * pMyParam.Die_Width_Ratio / 2));
                //    //int x2 = (int)(ecx + (((pMyParam.DieWidth + Scrib_X_um) / (pMyContext.MMPerPixel_X * 1000)) * pMyParam.Die_Width_Ratio / 2));
                //    //int y1 = (int)(ecy - (((pMyParam.DieHeight + Scrib_Y_um) / (pMyContext.MMPerPixel_Y * 1000)) * pMyParam.Die_Height_Ratio / 2));
                //    //int y2 = (int)(ecy + (((pMyParam.DieHeight + Scrib_Y_um) / (pMyContext.MMPerPixel_Y * 1000)) * pMyParam.Die_Height_Ratio / 2));


                //    //int left_x = x1;
                //    //int top_y = y1;
                //    //int right_x = x2;
                //    //int bottom_y = y2;

                //    //double lt_length = Math.Sqrt(((left_x - dcenterx) * (left_x - dcenterx)) + ((top_y - dcentery) * (top_y - dcentery))); // left-top
                //    //double rt_length = Math.Sqrt(((right_x - dcenterx) * (right_x - dcenterx)) + ((top_y - dcentery) * (top_y - dcentery))); // right-top

                //    //double lb_length = Math.Sqrt(((left_x - dcenterx) * (left_x - dcenterx)) + ((bottom_y - dcentery) * (bottom_y - dcentery))); // left-bottom
                //    //double rb_length = Math.Sqrt(((right_x - dcenterx) * (right_x - dcenterx)) + ((bottom_y - dcentery) * (bottom_y - dcentery))); // right-bottom

                //    //if (clength < lt_length &&
                //    //    clength < rt_length &&
                //    //    clength < lb_length &&
                //    //    clength < rb_length
                //    //    )
                //    //    bErase = true;



                //    double elength = Math.Sqrt(((ecx - dcenterx) * (ecx - dcenterx)) + ((ecy - dcentery) * (ecy - dcentery)));

                //    if (clength < elength)
                //        bErase = true;
                //}




                pMyContext.dFoundModelX[i] = ModelResult.dXPos[i];  // Pixel-X
                pMyContext.dFoundModelY[i] = ModelResult.dYPos[i];  // Pixel-Y

                oSearchX = pMyContext.dFoundModelXWorld[i] = ModelResult.dXWorldPos[i];   // mm-X World 醫뚰몴怨?
                oSearchY = pMyContext.dFoundModelYWorld[i] = ModelResult.dYWorldPos[i];   // mm-Y World 醫뚰몴怨?

                // 01.31 Insert Start
                // Wafer 以묒떖??湲곗??쇰줈 Die 醫뚰몴?ㅼ쓣 怨꾩궛?섍린?뚮Ц?? Circle Finder?먯꽌 李얠? ?⑥씠??以묒떖 媛믪쓣 ?붾㈃ 以묒떖?쇰줈 ?대룞?섏뿬????
                cSearchX = pMyContext.dFoundModelXWorld[i] = ModelResult.dXWorldPos[i] + pMyContext.CenterOffsetXmm;   // mm-X World 醫뚰몴怨?
                cSearchY = pMyContext.dFoundModelYWorld[i] = ModelResult.dYWorldPos[i] + pMyContext.CenterOffsetYmm;   // mm-Y World 醫뚰몴怨?


                RotationMatrix(pMyContext.dAngle, cSearchX, cSearchY, ref dSearchX, ref dSearchY);
                // 01.31 Insert End


                pMyContext.dFoundModelAngle[i] = ModelResult.dAngle[i];
                pMyContext.dFoundModelScore[i] = ModelResult.dScore[i];
                pMyContext.dFoundModelWidth[i] = pMyParam.ModuleModel.Master.Width;         // ModelFinder?먯꽌 吏?뺥븳 Module Recipe??Width
                pMyContext.dFoundModelHeight[i] = pMyParam.ModuleModel.Master.Height;       // dndModelFinder?먯꽌 吏?뺥븳 Module Recipe??Height

                SetModelPosToDictionary(oSearchX, oSearchY, dSearchX, dSearchY, out MatrixPointX, out MatrixPointY, out Status);

                if(bErase != true)
                {
                    // Mapfile?먮뒗 議댁옱?섎뒗 Die ?댁?留? ModelFinder ?먯꽌 李얠? 紐삵븳 Die ?ㅼ쓣 李얠븘??醫뚰몴 ???
                    if (Status != 1)    // Change 04.12
                    {
                        // 04.02 Insert Start
                        // Virtual Map???ъ슜?섍린 ?꾪븳 媛??됯낵 媛??댁뿉 ??????醫뚰몴瑜???? (pixel)
                        if (VM_COLS.ContainsKey(MatrixPointX) == false)     // MatrixPointX ??媛?利??ㅺ? ?ы븿?섏뼱 ?덉? ?딅떎硫?
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



                }



                // 媛쒕퀎 Die???대?吏瑜?crob ?섍린 ?꾪빐 Model Find ?먯꽌 李얠? ?대?吏 ?뺣낫瑜?Found_Die?????
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


                    if (bErase != true)
                    {
                        // 04.02 Insert Start
                        // Virtual Map???ъ슜?섍린 ?꾪븳 ?됯낵 ?댁뿉 ???醫뚰몴瑜????
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
        }


        public int MapfileSave()
        {

            //var dcenterx = pMyContext.CenterOffsetXmm;
            //var dcentery = pMyContext.CenterOffsetYmm;

            var dcenterx = pMyContext.dFoundCenterX;
            var dcentery = pMyContext.dFoundCenterY;


            var radius = pMyContext.dRadius;

            //double clength = (radius - pMyContext.dEraseLength) * pMyParam.PixelToUM_Offset / 1000;  
            double clength = (radius - pMyContext.dEraseLength);// * pMyParam.PixelToUM_Offset / 1000;


            // 留??뚯씪 ?대떦 ?몃뜳?ㅼ뿉 ?꾩튂 醫뚰몴媛?異붽?
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

                for (int i = 0; i < OriginData.nDieCnt; i++)            // Map File ???쒖떆??Die 珥?媛쒖닔
                {
                    string line;
                    poTrans.X = OriginData.nTransX[i];
                    poTrans.Y = OriginData.nTransY[i];


                    if(poTrans.X == 9 && poTrans.Y == 1)
                    {
                        int yyyyy = 0;
                    }

                    try
                    {
                        #region Virtual Map Image Position and Inspection Start

                        // 03.28 insert Start
                        // MapFile??Bin ?곗씠?곌? 18踰덉씠怨? ?됰젹?대룞???몃뜳?ㅼ쓽 Die 寃???곹깭媛 0(Die瑜?紐살갼? 寃쎌슦)?대씪硫?
                        if (((OriginData.nB[i] == pMyParam.Die_Grade) || (OriginData.nB[i] == pMyParam.Die_GradeS)) && (OriginPoint[poTrans].Status == 0))
                        {
                            double ScreenCenterX = GrayImage.Width / 2;     // Screen Center X Pixel
                            double ScreenCenterY = GrayImage.Height / 2;    // Screen Center Y Pixel

                            double VMDie_CenterX = 0;
                            double VMDie_CenterY = 0;

                            double VMDie_CenterXW = 0;  // 08.12 Insert
                            double VMDie_CenterYW = 0;  // 08.12 Insert

                            #region Find VirtualMap Start
                            // 12.09 Test 吏꾪뻾 以?
                            bool vmfoundX = false;  // X 諛⑺뼢 ?????몄젒 ?몃뜳?ㅼ뿉???대떦 媛믪쓣 李얠븯?붿? ?뺤씤?섎뒗 蹂??
                            bool vmfoundY = false;  // Y 諛⑺뼢 醫????몄젒 ?몃뜳?ㅼ뿉???대떦 媛믪쓣 李얠븯?붿? ?뺤씤?섎뒗 蹂??
                            System.Drawing.Point _poTrans = new System.Drawing.Point(); // ?꾩옱 ?몃뜳?ㅼ뿉???????몃뜳??蹂??媛믪쓣 媛?몄삤湲??꾪븳 蹂??
                            Found_Die_Info tempDie = new Found_Die_Info();              // Model Finder ?먯꽌 ?대?吏 李얠? ?????몃뜳?ㅺ? 媛吏??곗씠?곕? 媛?몄삤湲??꾪븳 蹂??

                            int SearchIndex = 1;    // Search Index variable
                            _poTrans.X = poTrans.X;

                            while (SearchIndex < (nMaxRow + 1))
                            {
                                if((poTrans.Y - SearchIndex) >= 0)
                                {
                                    _poTrans.Y = poTrans.Y - SearchIndex;
                                    if((OriginPoint.ContainsKey(_poTrans) == true) && OriginPoint[_poTrans].Status == 1)
                                    {
                                        pMyContext.Found_Die.TryGetValue(_poTrans, out tempDie);
                                        VMDie_CenterX = tempDie.X_Pos;
                                        VMDie_CenterXW = tempDie.WX_Pos;

                                        vmfoundX = true;

                                        break;
                                    }
                                }

                                if((poTrans.Y + SearchIndex) <= nMaxRow)
                                {
                                    _poTrans.Y = poTrans.Y + SearchIndex;
                                    if ((OriginPoint.ContainsKey(_poTrans) == true) && OriginPoint[_poTrans].Status == 1)
                                    {
                                        pMyContext.Found_Die.TryGetValue(_poTrans, out tempDie);
                                        VMDie_CenterX = tempDie.X_Pos;
                                        VMDie_CenterXW = tempDie.WX_Pos;

                                        vmfoundX = true;

                                        break;
                                    }
                                }

                                SearchIndex++;
                            }

                            // VM Found Y Coordination
                            SearchIndex = 1;    // Search Index variable
                            _poTrans.Y = poTrans.Y;

                            while(SearchIndex < (nMaxCol + 1))
                            {
                                if ((poTrans.X - SearchIndex) >= 0)
                                {
                                    _poTrans.X = poTrans.X - SearchIndex;
                                    //if ((VM_COLS.ContainsKey(_poTrans.X) == true) && OriginPoint[_poTrans].Status == 1)
                                    if ((OriginPoint.ContainsKey(_poTrans) == true) && OriginPoint[_poTrans].Status == 1)
                                    {
                                        pMyContext.Found_Die.TryGetValue(_poTrans, out tempDie);
                                        VMDie_CenterY = tempDie.Y_Pos;
                                        VMDie_CenterYW = tempDie.WY_Pos;

                                        vmfoundY = true;

                                        break;
                                    }
                                }

                                if ((poTrans.X + SearchIndex) <= nMaxCol)
                                {
                                    _poTrans.X = poTrans.X + SearchIndex;
                                    if ((OriginPoint.ContainsKey(_poTrans) == true) && OriginPoint[_poTrans].Status == 1)
                                    {
                                        pMyContext.Found_Die.TryGetValue(_poTrans, out tempDie);
                                        VMDie_CenterY = tempDie.Y_Pos;
                                        VMDie_CenterYW = tempDie.WY_Pos;

                                        vmfoundY = true;

                                        break;
                                    }
                                }

                                SearchIndex++;
                            }

                            if (vmfoundX == false)
                            {
                                if (VM_COLS.ContainsKey(poTrans.X) == true) // 12.09 Origin
                                {
                                    // VM Dictionary ???대떦 Key媛 議댁옱?쒕떎硫? Value 媛믪쓣 媛?몄삩??
                                    VM_COLS.TryGetValue(poTrans.X, out VMDie_CenterX);
                                    VM_COLSW.TryGetValue(poTrans.X, out VMDie_CenterXW);
                                }
                                else
                                {

                                    int SearchKey = 1;
                                    while (SearchKey < (nMaxCol + 1))
                                    {
                                        if (((poTrans.X - SearchKey) >= 0) && VM_COLS.ContainsKey(poTrans.X - SearchKey) == true)       // ?꾩옱 ?몃뜳?ㅼ쓽 ?쇱そ 諛⑺뼢 寃??
                                        {
                                            VM_COLS.TryGetValue(poTrans.X - SearchKey, out VMDie_CenterX);
                                            VMDie_CenterX += (((pMyParam.Scrib_X * 2) * (pMyContext.MMPerPixel_X * 1000) + pMyParam.DieWidth) / (pMyContext.MMPerPixel_X * 1000)) * SearchKey;

                                            VM_COLSW.TryGetValue(poTrans.X - SearchKey, out VMDie_CenterXW);
                                            VMDie_CenterXW += ((pMyParam.Scrib_X * 2) * (pMyContext.MMPerPixel_X) + (pMyParam.DieWidth / 1000)) * SearchKey;

                                            break;
                                        }
                                        if (((poTrans.X + SearchKey <= nMaxCol)) && (VM_COLS.ContainsKey(poTrans.X + SearchKey) == true))  // ?꾩옱 ?몃뜳?ㅼ쓽 ?ㅻⅨ履?諛⑺뼢 寃??
                                        {
                                            VM_COLS.TryGetValue(poTrans.X + SearchKey, out VMDie_CenterX);
                                            VMDie_CenterX -= (((pMyParam.Scrib_X * 2) * (pMyContext.MMPerPixel_X * 1000) + pMyParam.DieWidth) / (pMyContext.MMPerPixel_X * 1000)) * SearchKey;

                                            VM_COLSW.TryGetValue(poTrans.X + SearchKey, out VMDie_CenterXW);
                                            VMDie_CenterXW -= ((pMyParam.Scrib_X * 2) * (pMyContext.MMPerPixel_X) + (pMyParam.DieWidth / 1000)) * SearchKey;
                                            break;
                                        }

                                        SearchKey++;
                                    }
                                    //Debug.WriteLine($"X Index:{poTrans.X}, Create Virtual Map X-Postion:{VMDie_CenterX}");
                                }
                            }

                            if (vmfoundY == false)
                            {
                                if (VM_ROWS.ContainsKey(poTrans.Y) == true)
                                {
                                    // VM Dictionary ???대떦 Key媛 議댁옱?쒕떎硫? Value 媛믪쓣 媛?몄삩??
                                    VM_ROWS.TryGetValue(poTrans.Y, out VMDie_CenterY);
                                    VM_ROWSW.TryGetValue(poTrans.Y, out VMDie_CenterYW);

                                }
                                else
                                {
                                    int SearchKey = 1;
                                    while (SearchKey < (nMaxRow + 1))
                                    {
                                        if (((poTrans.Y - SearchKey) >= 0) && VM_ROWS.ContainsKey(poTrans.Y - SearchKey) == true)
                                        {
                                            VM_ROWS.TryGetValue(poTrans.Y - SearchKey, out VMDie_CenterY);
                                            VMDie_CenterY += (((pMyParam.Scrib_Y * 2) * (pMyContext.MMPerPixel_Y * 1000) + pMyParam.DieHeight) / (pMyContext.MMPerPixel_Y * 1000)) * SearchKey;

                                            VM_ROWSW.TryGetValue(poTrans.Y - SearchKey, out VMDie_CenterYW);
                                            VMDie_CenterYW += ((pMyParam.Scrib_Y * 2) * (pMyContext.MMPerPixel_Y) + (pMyParam.DieHeight / 1000)) * SearchKey;

                                            break;
                                        }
                                        if (((poTrans.Y + SearchKey) <= nMaxRow) && VM_ROWS.ContainsKey(poTrans.Y + SearchKey) == true)
                                        {
                                            VM_ROWS.TryGetValue(poTrans.Y + SearchKey, out VMDie_CenterY);
                                            VMDie_CenterY -= (((pMyParam.Scrib_Y * 2) * (pMyContext.MMPerPixel_Y * 1000) + pMyParam.DieHeight) / (pMyContext.MMPerPixel_Y * 1000)) * SearchKey;

                                            VM_ROWSW.TryGetValue(poTrans.Y + SearchKey, out VMDie_CenterYW);
                                            VMDie_CenterYW -= ((pMyParam.Scrib_Y * 2) * (pMyContext.MMPerPixel_Y) + (pMyParam.DieHeight / 1000)) * SearchKey;
                                            break;
                                        }

                                        SearchKey++;
                                    }
                                }
                            }
                            #endregion Find VirtualMap End

                            VMDie_CenterX = Math.Truncate(VMDie_CenterX);
                            VMDie_CenterY = Math.Truncate(VMDie_CenterY);

                            // Scrib Pixel ???낅젰 諛쏆븘 怨꾩궛??媛믪쑝濡?ROI ?곸뿭???곗텧.
                            double Scrib_X_um = pMyParam.Scrib_X * (pMyContext.MMPerPixel_X * 1000);   // 01.22 Insert
                            double Scrib_Y_um = pMyParam.Scrib_Y * (pMyContext.MMPerPixel_Y * 1000);   // 01.22 Insert

                            int x1 = (int)(VMDie_CenterX - (((pMyParam.DieWidth + Scrib_X_um) / (pMyContext.MMPerPixel_X * 1000)) * pMyParam.Die_Width_Ratio / 2));
                            int x2 = (int)(VMDie_CenterX + (((pMyParam.DieWidth + Scrib_X_um) / (pMyContext.MMPerPixel_X * 1000)) * pMyParam.Die_Width_Ratio / 2));
                            int y1 = (int)(VMDie_CenterY - (((pMyParam.DieHeight + Scrib_Y_um) / (pMyContext.MMPerPixel_Y * 1000)) * pMyParam.Die_Height_Ratio / 2));
                            int y2 = (int)(VMDie_CenterY + (((pMyParam.DieHeight + Scrib_Y_um) / (pMyContext.MMPerPixel_Y * 1000)) * pMyParam.Die_Height_Ratio / 2));
                            // 01.22 Insert End


                            // Die 以묒떖 ?꾩튂???붾뱶 醫뚰몴瑜?湲곗??쇰줈 ROI ?곸뿭??援ы븳?? (mm to pixel 蹂???꾩슂)
                            OpenCvSharp.Rect VMROI = new OpenCvSharp.Rect(x1, y1, x2 - x1, y2 - y1);

                            // 怨꾩궛??VMROI ?곸뿭留뚰겮 ?대?吏瑜??섎씪 寃??吏꾪뻾.
                            Mat VMImage = new Mat(GrayImage, VMROI);
                            // 留뚯빟 ?대떦 ?대?吏媛 1梨꾨꼸 洹몃젅???곸긽???꾨땲?쇰㈃, ?대?吏 ???蹂??
                            if (VMImage.Channels() != 1)
                                Cv2.CvtColor(VMImage, VMImage, ColorConversionCodes.BGR2GRAY);

                            Mat TempImage = null;
                            int TempContourCount = 0;
                            double TempContourArea = 0;
                            bool TempConvex = false;
                            int TempApexCount = 0;
                            Moments TempMoments = null;

                            System.Drawing.Point MPoint = poTrans;  // poTrans : ?대룞 蹂?섎맂 ?됱뿴???몃뜳??
                            bool tempReturn = ProcessInspection(VMImage, MPoint, ref TempImage, ref TempContourCount, ref TempContourArea,
                                ref TempConvex, ref TempApexCount, ref TempMoments);

                            Debug.Indent();
                            Debug.WriteLine($"vitual Die Size: Width-{x2 - x1}, Height-{y2 - y1}");
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

                            VM_Die_Info.WX_Pos = value.X = VMDie_CenterXW;   //08.12 Insert World Coordination
                            VM_Die_Info.WY_Pos = value.Y = VMDie_CenterYW;  // 08.12 Insert World Coordination 

                            VM_Die_Info.X_Pos = VMDie_CenterX;      // Pixel-X
                            VM_Die_Info.Y_Pos = VMDie_CenterY;      // Pixel-Y

                            VM_Die_Info.Width = pMyParam.ModuleModel.Master.Width;
                            VM_Die_Info.Height = pMyParam.ModuleModel.Master.Height;
                            VM_Die_Info.Angle = 0;

                            VM_Die_Info.Die_Image = TempImage.Clone();

                            
                            if (tempReturn)
                            {
                                //if ((TempContourCount == 1) && (TempApexCount == 4) && (TempContourArea > ((x2-x1) * (y2 - y1) * 0.4)) && (TempContourArea < ((x2-x1) * (y2-y1) * 0.8)) )
                                if ((TempContourCount == 1) && (TempApexCount == 4) && (TempContourArea > ((x2 - x1) * (y2 - y1) * 0.5))) // Virtual Map?먯꽌 李얠? Die 硫댁쟻??50% ?댁긽?대씪硫??뺤긽?쇰줈 ?먮떒.
                                {
                                    value.Status = 1;   // ?뺤긽 Die ?먮퀎
                                }
                                else
                                {
                                    value.Status = 2;   // 遺덈웾 Die ?먮퀎
                                }

                                OriginPoint[poTrans] = value;

                                string strMapPath;
                                string strFileName;

                                if (pMyParam.SaveWaferImage == true)    // 05.23 Insert
                                {
                                    strMapPath = Path.Combine(SystemHandler.Handle.Setting.ImageSavePath,
                                        String.Format("{0:0000}{1:00}{2:00}/{3}", DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, pMyParam.MapName));
                                    if (Directory.Exists(strMapPath) == false)
                                        Directory.CreateDirectory(strMapPath);
                                    strFileName = Path.Combine(strMapPath, String.Format("{0}_{1}_Image.png", poTrans.X, poTrans.Y)); // bmp -> png
                                    Cv2.ImWrite(strFileName, VM_Die_Info.Die_Image);
                                }
                            }
                            else
                            {
                                if (pMyParam.VMDieEnabel == false)
                                    value.Status = 0;   // 鍮꾩뼱 ?덈뒗 Die ?먮퀎. ?대젃寃??섎㈃ UI ?곸쓽 Valid Die 媛쒖닔媛 利앷??섏?留? ListViewBox???쒖떆 ?섏? ?딆쓬.
                                else
                                    value.Status = 2;   // 遺덈웾 Die ?먮퀎.

                                OriginPoint[poTrans] = value;

                                string strMapPath;
                                string strFileName;

                                if (pMyParam.SaveWaferImage == true)    // 05.23 Insert
                                {
                                    strMapPath = Path.Combine(SystemHandler.Handle.Setting.ImageSavePath,
                                        String.Format("{0:0000}{1:00}{2:00}/{3}", DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, pMyParam.MapName));
                                    if (Directory.Exists(strMapPath) == false)
                                        Directory.CreateDirectory(strMapPath);
                                    strFileName = Path.Combine(strMapPath, String.Format("{0}_{1}_Image.png", poTrans.X, poTrans.Y)); // bmp -> png
                                    Cv2.ImWrite(strFileName, VM_Die_Info.Die_Image);
                                }
                            }

                            //// 2025.05.02 Insert Start
                            

                            //if (pMyParam.EraseChip == true)
                            //{
                            //    double left_x = OriginPoint[poTrans].X;
                            //    double top_y = OriginPoint[poTrans].Y;
                            //    double right_x = OriginPoint[poTrans].X + (pMyParam.DieWidth / 1000.0);
                            //    double bottom_y = OriginPoint[poTrans].Y + (pMyParam.DieHeight / 1000.0);

                            //    double c_length = Math.Sqrt(((OriginPoint[poTrans].X - dcenterx) * (OriginPoint[poTrans].X - dcenterx)) + ((OriginPoint[poTrans].Y - dcentery) * (OriginPoint[poTrans].Y - dcentery))); // left-top

                            //    double lt_length = Math.Sqrt(((left_x - dcenterx) * (left_x - dcenterx)) + ((top_y - dcentery) * (top_y - dcentery))); // left-top
                            //    double rt_length = Math.Sqrt(((right_x - dcenterx) * (right_x - dcenterx)) + ((top_y - dcentery) * (top_y - dcentery))); // right-top

                            //    double lb_length = Math.Sqrt(((left_x - dcenterx) * (left_x - dcenterx)) + ((bottom_y - dcentery) * (bottom_y - dcentery))); // left-bottom
                            //    double rb_length = Math.Sqrt(((right_x - dcenterx) * (right_x - dcenterx)) + ((bottom_y - dcentery) * (bottom_y - dcentery))); // right-bottom

                            //    if (clength < lt_length ||
                            //        clength < rt_length ||
                            //        clength < lb_length ||
                            //        clength < rb_length
                            //        )
                            //    {

                            //        var val = OriginPoint[poTrans];
                            //        val.Status = 2;
                            //        OriginPoint[poTrans] = val;
                            //        bErase = true;
                            //    }
                            //}

                            //// 2025.05.02 Insert End






                            try
                            {
                                Debug.Indent();
                                Debug.WriteLine($"VM Found Die UnAdd Count:{pMyContext.Found_Die.Count}");
                                Debug.Unindent();

                                // Vitual Map???곸슜?섍린 ??Dictionary Key???대떦?섎뒗 ?붿냼 媛믪? ?녾린 ?뚮Ц??false 瑜?諛섑솚?댁빞 ?뺤긽.
                                if ((pMyContext.Found_Die.ContainsKey(poTrans) == false) && (OriginPoint[poTrans].Status != 0))
                                {     
                                    pMyContext.Found_Die.Add(poTrans, VM_Die_Info);
                                }
                                else
                                    Debug.WriteLine($"Virtual MapSearch Function Dictionary Key Error:{poTrans.X},{poTrans.Y}");
                            }
                            catch (Exception e)
                            {
                                Logging.PrintErrLog((int)ELogType.Error, $"Inner-Vitual MapSearch Function FoundDie Key Exception:{e.Message}");
                                Debug.WriteLine($"Inner-Vitual MapSearch Found Die Exception:{e.ToString()}");
                            }

                        }
                        // 03.28 insert end
                        #endregion Virtual Map Image Position and Inspection End
                    }
                    catch (Exception e)
                    {
                        Logging.PrintErrLog((int)ELogType.Error, $"Outer-Vitual MapSearch Function FoundDie Key Exception:{e.Message}");
                        Debug.WriteLine($"Outer-Vitual MapSearch Found Die Exception:{e.ToString()}");
                    }


                    // 2025.05.02 Insert Start
                    if(OriginData.nTransX[i]  == 20 && OriginData.nTransY[i] == 8)
                    {
                        int kkkk = 0;
                    }
                    if (pMyParam.EraseChip == true)
                    {
                        System.Drawing.Point t_poTrans = new System.Drawing.Point();

                        t_poTrans.X = OriginData.nTransX[i];
                        t_poTrans.Y = OriginData.nTransY[i];


                        if(pMyContext.Found_Die.ContainsKey(t_poTrans))
                        {

                            var die = pMyContext.Found_Die[t_poTrans];

                            double left_x = die.X_Pos - die.Width / 2.0;
                            double top_y = die.Y_Pos - die.Height / 2.0;
                            double right_x = die.X_Pos + die.Width / 2.0;
                            double bottom_y = die.Y_Pos + die.Height / 2.0;



                            //double left_x = die.X_Pos - die.Die_Image.Width / 2.0;
                            //double top_y = die.Y_Pos - die.Die_Image.Height / 2.0;
                            //double right_x = die.X_Pos + die.Die_Image.Width / 2.0;
                            //double bottom_y = die.Y_Pos + die.Die_Image.Height / 2.0;




                            double lt_length = Math.Sqrt(((left_x - dcenterx) * (left_x - dcenterx)) + ((top_y - dcentery) * (top_y - dcentery))); // left-top
                            double rt_length = Math.Sqrt(((right_x - dcenterx) * (right_x - dcenterx)) + ((top_y - dcentery) * (top_y - dcentery))); // right-top

                            double lb_length = Math.Sqrt(((left_x - dcenterx) * (left_x - dcenterx)) + ((bottom_y - dcentery) * (bottom_y - dcentery))); // left-bottom
                            double rb_length = Math.Sqrt(((right_x - dcenterx) * (right_x - dcenterx)) + ((bottom_y - dcentery) * (bottom_y - dcentery))); // right-bottom


                            if (clength < lt_length ||
                                clength < rt_length ||
                                clength < lb_length ||
                                clength < rb_length
                                )
                            {

                                var val = OriginPoint[t_poTrans];
                                val.Status = 2;
                                OriginPoint[t_poTrans] = val;
                            }
                        }


                        //double Scrib_X_um = pMyParam.Scrib_X * (pMyContext.MMPerPixel_X * 1000);   // 01.22 Insert
                        //double Scrib_Y_um = pMyParam.Scrib_Y * (pMyContext.MMPerPixel_Y * 1000);   // 01.22 Insert


                        //double x1 = 0.0;
                        //double x2 = 0.0;
                        //double y1 = 0.0;
                        //double y2 = 0.0;


                        //x1 = OriginPoint[poTrans].X;

                        //if(x1 > 0)
                        //    x2 = OriginPoint[poTrans].X + (pMyParam.DieWidth / 1000.0);
                        //else
                        //    x2 = OriginPoint[poTrans].X - (pMyParam.DieWidth / 1000.0);


                        //y1 = OriginPoint[poTrans].Y;

                        //if(y1 > 0)
                        //    y2 = OriginPoint[poTrans].Y + (pMyParam.DieHeight / 1000.0);
                        //else
                        //    y2 = OriginPoint[poTrans].Y - (pMyParam.DieHeight / 1000.0);


                        //double left_x = x1;
                        //double top_y = y1;
                        //double right_x = x2;
                        //double bottom_y = y2;


                        //double c_length = Math.Sqrt(((OriginPoint[poTrans].X - dcenterx) * (OriginPoint[poTrans].X - dcenterx)) + ((OriginPoint[poTrans].Y - dcentery) * (OriginPoint[poTrans].Y - dcentery))); // left-top

                        //double lt_length = Math.Sqrt(((left_x - dcenterx) * (left_x - dcenterx)) + ((top_y - dcentery) * (top_y - dcentery))); // left-top
                        //double rt_length = Math.Sqrt(((right_x - dcenterx) * (right_x - dcenterx)) + ((top_y - dcentery) * (top_y - dcentery))); // right-top

                        //double lb_length = Math.Sqrt(((left_x - dcenterx) * (left_x - dcenterx)) + ((bottom_y - dcentery) * (bottom_y - dcentery))); // left-bottom
                        //double rb_length = Math.Sqrt(((right_x - dcenterx) * (right_x - dcenterx)) + ((bottom_y - dcentery) * (bottom_y - dcentery))); // right-bottom


                        ////double c_length = Math.Sqrt(((dcenterx -OriginPoint[poTrans].X) * (dcenterx - OriginPoint[poTrans].X)) + ((dcentery - OriginPoint[poTrans].Y) * (dcentery - OriginPoint[poTrans].Y))); // left-top

                        ////double lt_length = Math.Sqrt(((dcenterx - left_x) * (dcenterx - left_x)) + ((dcentery - top_y) * (dcentery - top_y))); // left-top
                        ////double rt_length = Math.Sqrt(((dcenterx - right_x) * (dcenterx - right_x)) + ((dcentery - top_y) * (dcentery - top_y))); // right-top

                        ////double lb_length = Math.Sqrt(((dcenterx - left_x) * (dcenterx - left_x)) + ((dcentery - bottom_y) * (dcentery - bottom_y))); // left-bottom
                        ////double rb_length = Math.Sqrt(((dcenterx - right_x) * (dcenterx - right_x)) + ((dcentery - bottom_y) * (dcentery - bottom_y))); // right-bottom


                        //if (clength < lt_length ||
                        //    clength < rt_length ||
                        //    clength < lb_length ||
                        //    clength < rb_length
                        //    )
                        //{

                        //    var val = OriginPoint[poTrans];
                        //    val.Status = 2;
                        //    OriginPoint[poTrans] = val;
                        //}
                    }

                    // 2025.05.02 Insert End

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

                    // DieInspection()?⑥닔?먯꽌 PASS, FAIL ???곗씠??紐⑤몢 MapData??異붽? ?? PASS : 1, FAIL: 2, MapFile??議댁옱?섏? ?딆? Die??0?닿퀬, MapData??異붽? ?섏? ?딆쓬.
                    if ((OriginPoint.ContainsKey(poTrans) == true) && (OriginPoint[poTrans].Status != 0))
                    {
                        {
                            temp.Succ = OriginPoint[poTrans].Status;
                            pMyContext.MapData.Add(i, temp);
                        }
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
                                CustomMessageBox.Show($"Wafer Camera Error", $"{pMyParam.ProcessName}-Camera Set Property Fail", MessageBoxImage.Error, true, false);
                            }
                            if (!pCamera.SetSoftwareTriggerMode())
                            {
                                Logging.PrintLog((int)ELogType.Error, "{0} Camera Set Trigger mode Fail!", pCamera.Name);
                                CustomMessageBox.Show($"Wafer Camera Error", $"{pMyParam.ProcessName}-Camera SetTrigger Mode Fail", MessageBoxImage.Error, true, false);
                            }
                        }
                        else
                        {
                            Logging.PrintLog((int)ELogType.Error, "{0} Camera Handle is null!", pMyParam.DeviceName);
                            CustomMessageBox.Show($"Wafer Camera Error ", $"{pMyParam.ProcessName}-Camera Handle is Null", MessageBoxImage.Error, true, false);
                            FinishAction(EContextResult.Error);
                            break;
                        }
                    }
                    Context.ResultHalconImage?.Dispose();
                    Context.ResultHalconImage = pCamera.GrabHalconImage();
                    if (Context.ResultHalconImage == null)
                    {
                        Logging.PrintLog((int)ELogType.Error, "{0} Camera Image Grab Failed!", pMyParam.DeviceName);
                        CustomMessageBox.Show($"Wafer Camera Error", $"{pMyParam.ProcessName}-Camera Image Grab Fail", MessageBoxImage.Error, true, false);
                        FinishAction(EContextResult.Error);
                        break;
                    }

                    using (Mat grabbedFrame = HalconImageBridge.ToMat(Context.ResultHalconImage))
                    {
                        if (grabbedFrame == null)
                        {
                            Logging.PrintLog((int)ELogType.Error, "{0} Camera Image Grab Failed!", pMyParam.DeviceName);
                            CustomMessageBox.Show($"Wafer Camera Error", $"{pMyParam.ProcessName}-Camera Image Grab Fail", MessageBoxImage.Error, true, false);
                            FinishAction(EContextResult.Error);
                            break;
                        }

                        if ((GrayImage == null) || (MophologyImage == null))
                        {   // change code 12.23 create the BinaryImage
                            GrayImage = new Mat(grabbedFrame.Size(), MatType.CV_8UC1);
                            MophologyImage = new Mat(grabbedFrame.Size(), MatType.CV_8UC1); // 12.23
                            GrabToThresholdImage = new Mat(grabbedFrame.Size(), MatType.CV_8UC1);    // 11.22
                        }
                        else if ((GrayImage.Width != grabbedFrame.Width) || (GrayImage.Height != grabbedFrame.Height))
                        {
                            GrayImage.Dispose();
                            GrayImage = new Mat(grabbedFrame.Size(), MatType.CV_8UC1);

                            MophologyImage.Dispose();      // 12.23    Free memory
                            MophologyImage = new Mat(grabbedFrame.Size(), MatType.CV_8UC1); // 12.23

                            GrabToThresholdImage.Dispose(); // 11.22
                            GrabToThresholdImage = new Mat(grabbedFrame.Size(), MatType.CV_8UC1);    // 11.22
                        }
                        // Convert Camera Image to GrayImage.
                        Cv2.CvtColor(grabbedFrame, GrayImage, ColorConversionCodes.BGR2GRAY);
                    }

                    #region GrabToThresholdBinaryImage Start 11.22
                    if (pMyParam.WaferThreshold != 0)               // 0???꾨땲?쇰㈃ Model Finder?먯꽌 紐⑤뜽??李얘린 ?꾪븳 ?대?吏??WaferThreshold 媛믪씠 ?곸슜???대?吏?ъ슜.
                        Cv2.Threshold(GrayImage, GrabToThresholdImage, pMyParam.WaferThreshold, 255, ThresholdTypes.Binary);  // 11.22
                    else
                        GrabToThresholdImage = GrayImage.Clone();   // WaferThreshold 媛믪씠 0?대㈃, 寃???곗씠?곗뿉 ?섍린???대?吏?곗씠?곕뒗 ?먮낯 ?곗씠?곕줈 ?꾨떖.

                    // 11.22 Insert
                    if(pMyParam.TeachingImage == true)
                        Context.ResultHalconImage?.Dispose();
                        Context.ResultHalconImage = HalconImageBridge.FromMat(GrabToThresholdImage);
                    else
                    {
                        // 11.25 Insert
                        if ((pMyParam.WaferTHEnable == true) && (pMyParam.WaferThreshold != 0) && (pMyParam.Mophology == false))
                        {
                            Context.ResultHalconImage?.Dispose();
                            Context.ResultHalconImage = HalconImageBridge.FromMat(GrabToThresholdImage);
                            pMyContext.CrobImage = GrabToThresholdImage.Clone();        // Lef/Right View??蹂댁뿬吏???대?吏 諛?Crob ?섍린 ?꾪븳 ?대?吏 蹂듭궗
                        }
                        else
                        {
                            Context.ResultHalconImage?.Dispose();
                            Context.ResultHalconImage = HalconImageBridge.FromMat(GrayImage);
                            pMyContext.CrobImage = GrayImage.Clone();       // Crob ?섍린 ?꾪븳 ?대?吏 蹂듭궗
                        }
                    }
                    #endregion

                    #region Morphology start insert date 12.23
                    if (pMyParam.Mophology)
                    {
                        /*
                         Mophology Open ImageProcessing.
                         Model Finder?먯꽌 Die瑜?李얠븘 ?먮퀎?섎뒗 遺遺꾨쭔 Binary Image瑜??ъ슜?섏??쇰ŉ, 
                         Contours Processing?먯꽌???먮낯 grab???대?吏(GrayImage)瑜?洹몃?濡??ъ슜.
                         留뚯빟 Contours Processing?먮룄 Binary Image瑜??ъ슜?섎젮硫? GrayImage = BinaryImage.Clone() ?먮뒗 
                         Cv2.Threashold(GrayImage, GrayImage, 30, 255, ThresholdTypes.Binary) ? 媛숈씠 ?ъ슜?섎㈃?섎ŉ, 
                         ?대븣??BinaryImage媛 ?꾩슂 ?놁쓬.
                        */
                        Cv2.Threshold(GrayImage, MophologyImage, pMyParam.Mophology_Threshold, 255, ThresholdTypes.Binary);        // parameter value  Origin

                        if (pMyParam.Mophology_KernelSize == 0)
                            pMyParam.Mophology_KernelSize = 2;
                        if (pMyParam.Mophology_Iteration == 0)
                            pMyParam.Mophology_Iteration = 1;

                        Mat element = Cv2.GetStructuringElement(MorphShapes.Rect, new OpenCvSharp.Size(pMyParam.Mophology_KernelSize, pMyParam.Mophology_KernelSize));  // Kernel Size 3x3, Insert 08.28
                        Cv2.MorphologyEx(MophologyImage, MophologyImage, MorphTypes.Open, element, null, pMyParam.Mophology_Iteration); // iteration is 3, Insert 08.28

                        // 02.15 Insert Start
                        if ((pMyParam.MophologyImage == true) && (pMyParam.MapCheck == true))   // Mophology瑜??ъ슜?섍퀬, Map?뚯씪???녿뒗 寃쎌슦
                        {
                            string strMapPath = Path.Combine(SystemHandler.Handle.Setting.ImageSavePath,
                                String.Format("{0:0000}{1:00}{2:00}/{3}", DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, pMyParam.ProcessName + "_NoMapMopology"));
                            if (Directory.Exists(strMapPath) == false)
                                Directory.CreateDirectory(strMapPath);
                            string strFileName = Path.Combine(strMapPath, String.Format($"{DateTime.Now.Hour}_{DateTime.Now.Minute}_{DateTime.Now.Second}_Mophology.jpg"));
                            Cv2.ImWrite(strFileName, MophologyImage);
                        }
                        // 02.15 Insert End

                        // Mophology Image ?곸슜???대?吏瑜?MIL Model Finder???꾨떖.
                        if (ALLIGATOR_ALG_MIL.agtAM_PutImage(AlgIndex, MophologyImage.Data) != 0)
                        {   // 12. 23 Change Code : Morphology Open Image (using previously threshold binary image)
                            Logging.PrintLog((int)ELogType.Error, "Wafer MophologyImage Failed {0} in {1} ReturnCode:{2}", "agtAM_PutImage", ID.ToString(), result);

                            FinishAction(EContextResult.Error);
                            break;
                        }

                        //Context.ResultImage = MophologyImage.Clone(); // 08.09 Mophology Image Display
                    }
                    else
                    {
                        // Camera Grab ???대?吏??Gray Scale ?대?吏瑜?MIL Model Finder ???꾨떖.
                        if (ALLIGATOR_ALG_MIL.agtAM_PutImage(AlgIndex, GrabToThresholdImage.Data) != 0)    // 11.22 Change Gray Image -> GrabToThresholdImage
                        {
                            Logging.PrintLog((int)ELogType.Error, "Wafer GrayImage Failed {0} in {1} ReturnCode:{2}", "agtAM_PutImage", ID.ToString(), result);

                            FinishAction(EContextResult.Error);
                            break;
                        }
                    }
                    #endregion Morphology End

                    Thread.Sleep(500);  // 02.05 insert
                    pMyContext.bMapList = false;

                    // 11.25 二쇱꽍 泥섎━?섍퀬, pMyParam.WaferTHEnable ??議곌굔???곕씪 pMyContext.CrobImage??GrayImage ?먮뒗 GrabToThresholdImage 瑜??좏깮?섏뿬 ?ｋ룄濡??섏젙??
                    //pMyContext.CrobImage = GrayImage.Clone();       // Crob ?섍린 ?꾪븳 ?대?吏 蹂듭궗

                    Step++;
                    break;

                case EStep.Calibration:
                    //Grid calibration associate
                    if (pMyParam.CalCheck)  // Calibration Option always True
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

                    if (pMyParam.CalCheck)  // Calibration Option Always True 
                    {
                        #region WaferScanClibrationParam Start: Get Param
                        // WaferScanCalibrationParam ??parameter 媛믪쓣 媛?몄??????    12.11 
                        WaferScanCalibrationParam calibParam = SystemHandler.Handle.Sequences[ParentID][EAction.Wafer_Calibration].Param as WaferScanCalibrationParam;

                        // Modify 06.14 Start
                        if (Param.OwnerName == "Inspect_Left")
                        {
                            pMyContext.MMPerPixel_X = calibParam.MMPerPixel_LX;      
                            pMyContext.MMPerPixel_Y = calibParam.MMPerPixel_LY;     
                        }
                        else if (Param.OwnerName == "Inspect_Right")
                        {
                            pMyContext.MMPerPixel_X = calibParam.MMPerPixel_RX;      
                            pMyContext.MMPerPixel_Y = calibParam.MMPerPixel_RY;      
                        }
                        else
                        {
                            Logging.PrintErrLog((int)ELogType.Error, string.Format("Wafer Camera Resolution Load Fail"));
                            FinishAction(EContextResult.Error);
                            break;
                        }
                        // Modify 06.14 End


                        Debug.WriteLine($"CameraCalibration X:{pMyContext.MMPerPixel_X}, Y:{pMyContext.MMPerPixel_Y}");

                        int Rect_Die_Width = (int)(pMyParam.DieWidth / (pMyContext.MMPerPixel_X * 1000));   // Die Width Size - Unit Pixel 
                        int Rect_Die_Height = (int)(pMyParam.DieHeight / (pMyContext.MMPerPixel_Y * 1000)); // Die Heigh Size - Unit Pixel


                        pMyContext.ScreenCenter_Xmm = (pCamera.Properties.Width / 2) * pMyContext.MMPerPixel_X; // 12.17 Insert
                        pMyContext.ScreenCenter_Ymm = (pCamera.Properties.Height / 2) * pMyContext.MMPerPixel_Y;// 12.17 Insert

                        Debug.WriteLine($"Pixel - Die Rect Size Width:{Rect_Die_Width}Pixel, Die Rect Size Height:{Rect_Die_Height}Pixel");
                        Debug.WriteLine($"mm - Die Rect Size Width:{pMyParam.DieWidth/1000}mm, Die Rect Size Height:{pMyParam.DieHeight/1000}mm");

                        #endregion WaferScanCalibrationParam End
                    }

                    Step++;
                    break;

                case EStep.LineAngle:

                    if (pMyParam.OrientSearch == false)     // OrientSearch ?ъ슜?섏? ?딆쓬.
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
                            // PutImage(Gray Image Or Mophology Image)瑜??댁슜?섏뿬 ?깅줉??Model 議곌굔??留욌뒗 Die瑜?李얜뒗??
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

                        #region Just Find die Average Calculate Start
                        // ?곴린 Median Calculate ?곸슜?쇰줈 二쇱꽍泥섎━. (02.21)
                        // Just Average Calculate
                        // Model Finder?먯꽌 李얠? 紐⑤뜽??Angle ?쒓린 諛⑸쾿?
                        // 1. X異?湲곗??쇰줈 ?꾨옒 諛⑺뼢?쇰줈 湲곗슱?댁쭊 寃쎌슦, 360??媛源뚯슫 媛믪쑝濡??곗궛.
                        // 2. X異?湲곗??쇰줈 ?꾩そ 諛⑺뼢?쇰줈 湲곗슱?댁쭊 寃쎌슦, 0?먯꽌 硫?댁쭊 媛믪쑝濡??곗궛.
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
                            // ModelFinder?먯꽌 李얠? 紐⑤뜽??Angle Positive 諛⑺뼢? 諛섏떆怨?諛⑺뼢.
                            PosCalAngle = PosAngle_sum / PosSum_count;
                        }
                        if (NegSum_count != 0)
                        {
                            // ModelFinder?먯꽌 李얠? 紐⑤뜽??Angle Negative 諛⑺뼢? ?쒓퀎 諛⑺뼢.
                            NegCalAngle = NegAngle_sum / NegSum_count;
                            NegCalAngle = NegCalAngle - 360;   // Wafer媛 ?뚯쟾?댁빞 ?섎뒗 Angle 媛? ?쒓퀎 諛섎? 諛⑺뼢??(-) 諛⑺뼢.
                        }

                        pMyContext.dAngle = calAngle = PosCalAngle + NegCalAngle;
                        #endregion Just Find die Average Calculate End


                        Debug.WriteLine($"Positive Angle:{PosCalAngle}, Negative Angle:{NegCalAngle}, calAngle:{calAngle}, { ID.ToString()}");

                        // 議곌굔???곕Ⅸ ?쒖뼱遺??Error 泥섎━ 諛??뚯쟾 ?뺣젹 ?붿껌 泥섎━ ?꾩슂.
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
                        // Line??Angle??李얜뒗??
                        int nAngleCnt = 0;
                        float[] fAngle = new float[10];
                        float[] fAngleScr = new float[10];

                        float fGetAngle = 0;
                        float fGetAngleScr = 0;

                        float calAngle = 0;

                        ALLIGATOR_ALG_MIL.agtAM_OrientSearch(AlgIndex, 10, true);         // 02.20 二쇱꽍泥섎━ ?? (Origin)
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

                        pMyContext.dAngle = -1 * calAngle;   // ?뺣갑?? ?쒓퀎 諛⑺뼢

                        // 議곌굔???곕Ⅸ ?쒖뼱遺??Error 泥섎━ 諛??뚯쟾 ?뺣젹 ?붿껌 泥섎━ ?꾩슂.
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

                    // 01.18 ?덉떆?쇱뿉 紐⑤뜽 ?깅줉 ??Wafer媛 ?뺣젹???곹깭(Tolerance_Rotate 踰붿쐞?댁뿉 議댁옱?????대?吏瑜???ν븷吏 ?좏깮)?먯꽌 Modelfinder??紐⑤뜽???깅줉???명븯寃??섍린 ?꾪빐 ?대?吏 ????듭뀡 異붽?.
                    // 02.15 Mapfile???ъ슜?섏? ?딄퀬, LineAngle???뺤긽?쇰븣 ?대?吏 ????щ? 異붽?. Start
                    if ((pMyContext.AngleFoundResult == EVisionResultType.OK) && (pMyParam.SaveWaferImage == true) && (pMyParam.MapCheck == true))      // Unsued MapFile 議곌굔
                    {
                        string strMapPath = Path.Combine(SystemHandler.Handle.Setting.ImageSavePath,
                            String.Format("{0:0000}{1:00}{2:00}/{3}", DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, pMyParam.ProcessName + "_NoMapWafer"));
                        if (Directory.Exists(strMapPath) == false)
                            Directory.CreateDirectory(strMapPath);
                        string strFileName = Path.Combine(strMapPath, String.Format($"{DateTime.Now.Hour}_{DateTime.Now.Minute}_{DateTime.Now.Second}_Image.jpg"));

                        // 11.22 議곌굔臾?異붽?
                        if (pMyParam.TeachingImage)
                            using (Mat savedResultImage = HalconImageBridge.ToMat(Context.ResultHalconImage)) {
                                Cv2.ImWrite(strFileName, savedResultImage);
                            }
                        else
                            Cv2.ImWrite(strFileName, GrayImage);
                    }
                    // 02.15 Mapfile???ъ슜?섏? ?딄퀬, LineAngle???뺤긽?쇰븣 ?대?吏 ????щ? 異붽?. End

                    Step++;
                    break;

                case EStep.MapDataRead:
                    // MapFile???녿뒗 ?곹깭?먯꽌 ?쒖뼱遺? ?뚯뒪?몃? ?꾪빐 MapFile read ?щ?瑜?泥댄겕
                    if (pMyParam.MapCheck == false) // Map file ?ъ슜.
                    {
                        try
                        {
                            string mapFile = null;  // MapFile name
                            TestPacket testPacket = Param.Parent.RequestPacket.AsTest();    // Protocol Recieve MapFile (?듭떊?쇰줈 ?곌껐?섏? ?딄퀬 ?대?吏 濡쒕뵫?섏뿬 寃利?吏꾪뻾 ??System.NullReferenceException 諛쒖깮 ?ъ씤??
                            if (testPacket != null)
                            {
                                mapFile = Path.Combine(SystemHandler.Handle.Setting.MapDataLoadPath, testPacket.TestID);    // ?듭떊?쇰줈 ?섏뼱??MapFile name 
                                // 02.19 insert Start
                                FileInfo info = new FileInfo(mapFile);
                                if(info.Exists)
                                {
                                    pMyContext.MapFileName = testPacket.TestID; // 03.20 Insert
                                }
                                else
                                {
                                    //CustomMessageBox.Show("Wafer MapFile Not Exists", $"Not Found {testPacket.TestID}", MessageBoxImage.Error, true, true, 5);    // 11.04 before update

                                    Logging.PrintErrLog((int)ELogType.Error, string.Format($"Wafer Map File Not Found: {testPacket.TestID}"));  // 11.04 Insert
                                    CustomMessageBox.Show("Wafer MapFile Not Exists", $"Not Found {testPacket.TestID}", MessageBoxImage.Error, true, false);    // 11.04 Insert
                                    pMyContext.ModelFinderResult = EVisionResultType.NG;    // 11.04 Insert
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
                                    // ?듭떊?쇰줈 ?꾨떖諛쏆? 留듯뙆???대쫫?쇰줈 ?대?吏 ???
                                    string strFileName = Path.Combine(strMapPath, String.Format($"{DateTime.Now.Hour}_{DateTime.Now.Minute}_{DateTime.Now.Second}_{testPacket.TestID}.jpg"));
                                    using (Mat savedResultImage = HalconImageBridge.ToMat(Context.ResultHalconImage)) {
                                Cv2.ImWrite(strFileName, savedResultImage);
                            }
                                }
                                // 01.29 Insert End

                                // 02.15 Isert Start
                                if ((pMyParam.Mophology == true) && (pMyParam.MophologyImage == true))
                                {
                                    string strMapPath = Path.Combine(SystemHandler.Handle.Setting.ImageSavePath,
                                        String.Format("{0:0000}{1:00}{2:00}/{3}", DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, pMyParam.ProcessName + "_Wafer_Mophology"));
                                    if (Directory.Exists(strMapPath) == false)
                                        Directory.CreateDirectory(strMapPath);
                                    string strFileName = Path.Combine(strMapPath, String.Format($"{DateTime.Now.Hour}_{DateTime.Now.Minute}_{DateTime.Now.Second}_{testPacket.TestID}_Mophology.jpg"));
                                    Cv2.ImWrite(strFileName, MophologyImage);
                                }
                                // 02.15 Isert Start
                            }
                            else
                            {
                                mapFile = Path.Combine(SystemHandler.Handle.Setting.MapDataLoadPath, pMyParam.MapName);     // Debug 吏꾪뻾??paramerter ?꾨떖??mapfile name
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

                    if(pMyParam.TeachingImage == true)
                    {
                        CustomMessageBox.Show("Wafer Model Teaching Mode", $"{ID.ToString()} - {pMyParam.ProcessName}", MessageBoxImage.Information, true, true, 5);
                        pMyContext.TechingResult = EVisionResultType.TECHING;       // 05.20 Insert
                        pMyContext.TeachingImage = true;    //07.05 Insert
                        FinishAction(EContextResult.WaferDieTeaching);
                        break;
                    }
                    else
                    {
                        pMyContext.TechingResult = EVisionResultType.OK;            // 06.20 Insert
                        pMyContext.TeachingImage = false;   // 07.05 Insert
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

                        if ((nGet == 0) && (dGetRansacRadius != 0))       // ?뺤긽?곸쑝濡?Circle??李얠븯?ㅻ㈃, Radius 媛 0???꾨땲?쇰㈃
                        {
                            pMyContext.bFoundCircle = true;
                            pMyContext.dFoundCenterX = dGetRansacCenterX;       // Pixel-X
                            pMyContext.dFoundCenterY = dGetRansacCenterY;       // Pixel-Y
                            pMyContext.dMovingCenterX = dGetRansacCenterX;
                            pMyContext.dMovingCenterY = dGetRansacCenterY;
                            pMyContext.dRadius = dGetRansacRadius;        // pixel
                            pMyContext.dRadmm = dGetRansacRadius * pMyParam.PixelToUM_Offset / 1000;   // mm 02.14


                            pMyContext.bEraseChip = pMyParam.EraseChip;
                            pMyContext.dEraseLength = pMyParam.EraseEdgeLength / pMyParam.PixelToUM_Offset;
                            pMyContext.dErasemm = pMyParam.EraseEdgeLength / 1000.0;   // mm 04.16



                            //pMyContext.dEraseLength = pMyParam.EraseEdgeLength;
                            //pMyContext.dErasemm = pMyParam.EraseEdgeLength * pMyParam.PixelToUM_Offset / 1000;   // mm 04.16



                            nGet = ALLIGATOR_ALG_MIL.agtAM_GridCalibrationTransformCoordinatesToWorld(AlgIndex, CalibrationIndex,
                                dGetRansacCenterX, dGetRansacCenterY,
                                out dCenterXWorld, out dCenterYWorld);
                            pMyContext.dFoundCenterXWorld = dCenterXWorld;
                            pMyContext.dFoundCenterYWorld = dCenterYWorld;

                            #region New_Circle_Move_Offset_Start
                            // 05.23 Circle 以묒떖???붾㈃ 以묒떖?쇰줈 ?대룞?댁빞 ?섎뒗 Offset 媛?
                            // Calibration ?곸슜???대?吏??寃쎌슦, ?붾㈃ 以묒떖??(0,0) ?대ŉ, X異뺤? Negative 諛⑺뼢???쇱そ. Positive 諛⑺뼢???ㅻⅨ履?
                            // Calibration ?곸슜???대?吏??寃쎌슦, ?붾㈃ 以묒떖??(0,0) ?대ŉ, Y異뺤? Negative 諛⑺뼢???꾩そ. Positive 諛⑺뼢???꾨옒履?
                            // Circle 以묒떖 媛?湲곗??쇰줈 Offset 媛믪쓽 遺?몃? 諛섎?濡??곸슜.
                            if (pMyContext.dFoundCenterXWorld < 0)
                                pMyContext.CenterOffsetXmm= -1 * pMyContext.dFoundCenterXWorld; // 10.31
                            else
                                pMyContext.CenterOffsetXmm = -1 * pMyContext.dFoundCenterXWorld;  // Origin

                            if (pMyContext.dFoundCenterYWorld < 0)
                                pMyContext.CenterOffsetYmm = pMyContext.dFoundCenterYWorld;
                            else
                                pMyContext.CenterOffsetYmm = -1 * pMyContext.dFoundCenterYWorld;
                            #endregion New_Circle_Move_Offset_End

                            // Insert 11.04
                            if(Math.Abs(pMyContext.CenterOffsetXmm) > pMyParam.ScreenCenterToCircleCenter_X || Math.Abs(pMyContext.CenterOffsetYmm) > pMyParam.ScreenCenterToCircleCenter_Y)
                            {
                                Logging.PrintErrLog((int)ELogType.Error, $"ScreenCenter To Circle Center Fail! - X_Offset:{Math.Abs(pMyContext.CenterOffsetXmm)} > {pMyParam.ScreenCenterToCircleCenter_X} , Y_Offset:{Math.Abs(pMyContext.CenterOffsetYmm)} > {pMyParam.ScreenCenterToCircleCenter_Y}");

                                CustomMessageBox.Show($"Wafer Center Distance Error", $"{pMyParam.ProcessName}-Wafer Center Distance Error:"+"\n"+ $"X_Offset:{Math.Abs(pMyContext.CenterOffsetXmm)} > {pMyParam.ScreenCenterToCircleCenter_X} , Y_Offset:{Math.Abs(pMyContext.CenterOffsetYmm)} > {pMyParam.ScreenCenterToCircleCenter_Y}", 
                                    MessageBoxImage.Error, true, false);

                                pMyContext.ModelFinderResult = EVisionResultType.NG;
                                FinishAction(EContextResult.Fail);
                                break;
                            }
                        }
                        else
                        {
                            Logging.PrintErrLog((int)ELogType.Error, "Ransac Find Circle Fail ! - {0} in {1} ReturnCode:{2}", "Ransac Circle", ID.ToString(), nGet);

                            // 11.04 Insert Start
                            CustomMessageBox.Show($"Ransac Find Circle Fail", $"{pMyParam.ProcessName}-Ransac Circle Fail", MessageBoxImage.Error, true, false);
                            pMyContext.ModelFinderResult = EVisionResultType.NG;
                            // 11.04 Insert End

                            FinishAction(EContextResult.Fail);
                            break;
                        }
                    }
                    catch (Exception e)
                    {
                        Logging.PrintErrLog((int)ELogType.Error, string.Format("Ransac Circle Exception while {0} in {1}, ReturnCode:{2} ({3})", "Ransac Found Circle", ID.ToString(), result, e.Message));
                        pMyContext.CircleFoundResult = EVisionResultType.NG;

                        CustomMessageBox.Show($"Ransac Find Circle Fail", $"{pMyParam.ProcessName}-Ransac Circle Fail", MessageBoxImage.Error, true, false);
                        pMyContext.ModelFinderResult = EVisionResultType.NG;

                        FinishAction(EContextResult.Error);
                        break;
                    }

                    Step++;
                    break;
                case EStep.ModelFind:
                    if (pMyParam.OrientSearch == true)
                    {
                        // EStep.LineAngle ?먯꽌 ModelFind瑜?吏꾪뻾?섏뿬 湲곗슱湲곕? 李얠쓣?? ?ъ슜?섍린 ?뚮Ц???ш린?쒕뒗 二쇱꽍(??젣)泥섎━.
                        ModelResult.Init();
                        try
                        {
                            result = ALLIGATOR_ALG_MIL.agtAM_FindModel(AlgIndex, ModelIndex, out ModelResult);
                            if (result != 0)
                            {
                                Logging.PrintLog((int)ELogType.Error, "ModelFinder Failed {0} in {1} ReturnCode:{2}", "agtAM_FindModel", ID.ToString(), result);

                                // 11.04 Insert
                                CustomMessageBox.Show($"ModelFinder Error", $"{pMyParam.ProcessName}-Not Found Model:{result}",
                                    MessageBoxImage.Error, true, false);

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
                    // map?뚯씪 ?ъ슜?섏? ?딄퀬 Modelfinder?먯꽌 李얠? Die瑜??뚯씪????? MapFile???녿뒗 ?곹깭?먯꽌 Die ?꾩튂 寃利앹슜?쇰줈 ?붿껌?섏뿬 ?묒꽦 (12.06) START
                    if ((pMyParam.MapCheck == true) && (pMyContext.ModelFinderResult == EVisionResultType.OK))
                    {
                        // ModelFinder???깅줉??紐⑤뜽 ?뺣낫瑜??댁슜?섏뿬 湲곕낯 basic_width? basic_Height瑜??곗텧. 01??17???곸슜.
                        double basic_Width = pMyParam.ModuleModel.Master.Width;
                        double basic_Height = pMyParam.ModuleModel.Master.Height;

                        // Create Wafer Map 01.08 start
                        // Circle ???몄젒?섎뒗 ?됰젹 媛쒖닔 ?곗텧. ?뺥솗??媛쒖닔瑜??뚯닔 ?녾린 ?뚮Ц???щ텇???됯낵 ?댁뿉 異붽? 媛쒖닔瑜??뷀븿.
                        int NoMap_Col = (int)Math.Truncate((pMyContext.dRadmm * 2) / (basic_Width * pMyContext.MMPerPixel_X)) + 5;   // X 諛⑺뼢 異붽? ??媛쒖닔: 5
                        int NoMap_Row = (int)Math.Truncate((pMyContext.dRadmm * 2) / (basic_Height * pMyContext.MMPerPixel_Y)) + 5;  // y 諛⑺뼢 異붽? ??媛쒖닔 : 5


                        nMaxCol = NoMap_Col;
                        nMaxRow = NoMap_Row;

                        bool sCol = false;
                        bool sRow = false;

                        int col = 0;
                        int row = 0;

                        int Col_Max = -1;
                        int Row_Max = -1;
                        int Col_Min = 100;
                        int Row_Min = 100;

                        // Create Wafer Map 01.17 End


                        // ?쒖뼱遺 寃利앹슜?쇰줈 ModelFinder?먯꽌 李얠? 紐⑤뱺 Die???꾩튂 ?뺣낫瑜?Screen Center 湲곗??쇰줈 Die Center? ?⑥뼱吏??ㅼ젣 嫄곕━瑜?mm濡?蹂?섑븯?????
                        // Angle 媛믪? ?섎?媛 ?놁쓬.
                        string FoudDieTestFile = Path.Combine(SystemHandler.Handle.Setting.MapDataSavePath, "FoundDieTest.txt");
                        using (StreamWriter writer = new StreamWriter(FoudDieTestFile))
                        {
                            for (int i = 0; i < ModelResult.nCnt; i++)
                            {
                                string line = null;

                                // 01.16 insert start
                                Found_Die_Info temp = new Found_Die_Info();     // Die ?뺣낫
                                System.Drawing.Point MatrixPoint = new System.Drawing.Point();  // Die ?됰젹
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
                                    // ModeFinder?먯꽌 李얠? Die 以묒떖媛믪씠 ?됰젹???쒖옉 Pixel?먯꽌 ?ㅼ쓬 ?됰젹???쒖옉 Pixel 踰붿쐞 ?덉뿉 ?덈떎硫?
                                    if ((ModelResult.dXPos[i] > (basic_Width * col)) && (ModelResult.dXPos[i] < basic_Width * (col + 1)))
                                    {
                                        sCol = true;

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

                                        tempmap.Pos_X = ModelResult.dXWorldPos[i];  // ListViewer ???쒖떆 ?섏? ?딅뜕 遺遺??섏젙 01.19
                                        tempmap.Pos_Y = ModelResult.dYWorldPos[i];  // ListViewer ???쒖떆 ?섏? ?딅뜕 遺遺??섏젙 01.19

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
                                        if (pMyContext.Found_Die.ContainsKey(MatrixPoint) == false)  // 以묐났??KEY 媛 ?녿떎硫?異붽?.    01.19 異붽?
                                            pMyContext.Found_Die.Add(MatrixPoint, temp);

                                        if (pMyContext.MapData.ContainsKey(i) == false)  // 以묐났??Key媛 ?녿떎硫?異붽?. 01.19??異붽?
                                            pMyContext.MapData.Add(i, tempmap);
                                        // 01.19 insert end
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Logging.PrintErrLog((int)ELogType.Error, string.Format($"Col:{col}, Row:{row}, Exception:{ex.Message}"));
                                }
                                // Create Wafer Map 01.17 End



                                // 2024.01.17 insert (MapFile 湲곗??쇰줈 ?ㅻ뜑 ?뺣낫 諛?Tail ?뺣낫瑜??쒖쇅???숈씪???щ㎎?쇰줈 ?뚯씪 ?댁슜 ???)
                                // X, Y??Matrix Shift ???곗씠?곗씠怨? X1, Y1? Matrix瑜??됯낵 ?댁뿉 ????щ갚 遺遺꾩쓣 Shift ???곗씠??
                                // XP, YP: ModelFinder ?먯꽌 李얠? Die??醫뚰몴 媛?
                                // B: 10 (?꾩쓽??媛?
                                // S: 0 (?꾩쓽??媛? -> 1: ?뺤긽 Die, 2: 議댁옱?섏?留?Die媛 遺덈웾?쇰븣 (01.19蹂寃?
                                line = String.Format($"X= {col} Y= {row}  B= 10, X1 = {MatrixPoint.X} Y1 ={MatrixPoint.Y} " +
                                    $"XP ={pMyContext.dFoundModelXWorld[i]:0.0000} YP ={pMyContext.dFoundModelYWorld[i]:0.0000} S ={tempmap.Succ}");  // 01.18 tempmap.Succ 媛믪쑝濡?S 媛??좊떦.
                                                                                                                                                      //$"XP ={pMyContext.dFoundModelXWorld[i]} YP ={pMyContext.dFoundModelYWorld[i]} S =1"); // 01.18 NoMapFile 紐⑤뜽?????S媛믪쓣 紐⑥“嫄?1濡?湲곗엯?섎뜕嫄??꾩? 媛숈씠 ?섏젙.

                                writer.WriteLine(line);
                            }

                            // 01.18 insert Start
                            /*
                             *  01.18 ?대?吏瑜?泥섏쓬 濡쒕뱶?섏뿬 Die???뺣낫瑜??댁슜??MapFile ?앹꽦??Out of Range Exception??諛쒖깮?섏뿬 
                             *  李얠븘??Col_Min怨?Row_Min???덉떆???곗씠?곗? 媛숈? ?딅떎硫? Wafer Map???ш쾶 ?앹꽦?섏뿬 洹몃━寃??섏젙??
                             *  Shift Col/Row ?뚮씪硫뷀꽣????λ맂 ?덉떆?쇰? ???????濡쒕뱶 猷??ъ슜?섎떎硫? 醫??곷떒???щ텇???놁씠 Map??洹몃━寃???
                             *  ?덉떆?쇰? ??ν븯吏 ?딄퀬 ??寃?ъ떆?먮룄 醫??곷떒???щ텇 ?놁씠 洹몃젮吏吏留? ?꾨줈洹몃옩 ?ъ떆?????숈씪??怨쇱젙??嫄곗튂寃???
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
                                nMaxCol = Col_Max; //- 1;      // 01.17 Wafer Map 理쒕? Col 媛쒖닔. (Minus ?댁쓽 ?ㅻⅨ履??щ갚 ?댁쓽 媛쒖닔)
                                nMaxRow = Row_Max; //- 2;      // 01.17 Wafer Map 理쒕? Row 媛쒖닔. (Minus ?됱쓽 ?꾨옒履??щ갚 ?됱쓽 媛쒖닔)
                            }
                            // 01.18 insert End

                        }
                        pMyContext.bFoundModel = true;        // 01.18 insert
                        Step = (int)EStep.End;
                    }
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

                    pMyContext.dFoundCount = ModelResult.nCnt;          // Model Find ?먯꽌 李얠? Die 媛쒖닔.
                    pMyContext.DieTotal = pMyContext.Found_Die.Count;   // Map File??議댁옱?섎뒗 ?꾩튂????묓븯??Die ??臾??뺤씤 ??Die 媛쒖닔.


                    pMyContext.nMaxCol = nMaxCol;
                    pMyContext.nMaxRow = nMaxRow;


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

                                // 李얠? Die 媛 ?먮낯 map file?먯꽌 媛吏?Bin 媛믪쑝濡?洹몃옒?꾩뿉 ?쒗쁽?섍린 ?꾪빐 ?섏젙.
                                if (item.Value.Succ != 0)       // Pass: 1, FAIL: 2, 議댁옱?섏? ?딅뒗 Die : 0
                                    zData[index] = item.Value.Bin;
                                else
                                    zData[index] = 0;
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

