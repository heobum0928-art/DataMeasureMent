using AlligatorAlgMil;
using OpenCvSharp;
using ReringProject.Define;
using ReringProject.Device;
using ReringProject.Network;
using ReringProject.Sequence;
using ReringProject.UI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace ReringProject.Sequence {

    public class BottomSequenceContext : SequenceContext {

        //Response data
        public double CenterOffsetXmm { get; set; }
        public double CenterOffsetYmm { get; set; }
        public double dAngle { get; set; }

        //ActionContext pActionContext;

        public bool bFoundCircle { get; set; }
        public double dCenterX { get; set; }
        public double dCenterY { get; set; }
        public double dRad { get; set; }

        public double dRadmm { get; set; }

        public double dDistX { get; set; }
        public double dDistY { get; set; }

        public bool bfinish { get; set; }
        public string ProcessName { get; set; }     // Process Name

        public EVisionResultType ResultInfo { get; set; }

        public Dictionary<int, BottomDieInfo> BottomDie = new Dictionary<int, BottomDieInfo>();

        BottomCalibrationContext BottomCalibrationContext { get; set; }

        BottomInspectionContext BottomInspectionContext { get; set; }

        //public OpenCvSharp.Rect ROI_Rect = new OpenCvSharp.Rect();
        public OpenCvSharp.Rect ROI_Rect { get; set; }

        // 01.03 insert start
        public double[] CenterOffsetXmmArray { get; set; } = new double[10];
        public double[] CenterOffsetYmmArray { get; set; } = new double[10];
        public double[] dAngleArray { get; set; } = new double[10];
        public double[] DieCenter_XArray { get; set; } = new double[10];   // int -> double
        public double[] DieCenter_YArray { get; set; } = new double[10];   // int -> double
        public int PickerNum { get; set; }
        // 01.03 Insert End

        // 02.29 Insert Start
        public double[] X_Picker = new double[10];
        public double[] Y_Picker = new double[10];

        public int CalSelector;
        // 02.29 Insert End

        public double Cal_Xbase { get; set; }
        public double Cal_Ybase { get; set; }

        // 02.06 Insert Start
        public double ScreenCenter_X { get; set; }      // mm  = pCamera.Width/2 * 1PixelResolution.
        public double ScreenCenter_Y { get; set; }      // mm  = pCamera.Height/2 * 1PiexelResolution.
        // 02.06 Insert End

        public BottomSequenceContext(BottomSequence source) : base(source) {
            //생성자에서 clear를 호출하지 말것
        }
        
        // 비전 결과 데이터를 저장할 List 생성          // 11.20 insert 
        // VisionResponseListData 구조체 변경 요청.    // 11.20 Request by YJK
        public const int MaxListCount = 10;
        //public VisionResponseListData[] visionResults = new VisionResponseListData[MaxListCount];

        public override void Clear() {

            bFoundCircle = false;
            dCenterX = 0;
            dCenterY = 0;
            dRad = 0;

            dRadmm = 0;     // Cicle Radius per mm

            dDistX = 0;
            dDistY = 0;

            // Bottom Calibration variable Init
            CenterOffsetXmm = 0;
            CenterOffsetYmm = 0;
            dAngle = 0;

            bfinish = false;

            ProcessName = "None";

            ResultInfo = EVisionResultType.NG;

            BottomDie.Clear();

            for(int i=0; i<10; i++)
            {
                // Bottom Inspection variable Init
                CenterOffsetXmmArray[i] = 0;
                CenterOffsetYmmArray[i] = 0;
                dAngleArray[i] = 0;

                DieCenter_XArray[i] = 0;
                DieCenter_YArray[i] = 0;

                X_Picker[i] = 0;
                Y_Picker[i] = 0;
            }
            PickerNum = 0;

            Cal_Xbase = 0;
            Cal_Ybase = 0;

            ScreenCenter_X = 0;
            ScreenCenter_Y = 0;

            // 02.29 Insert Start
            CalSelector = 0;
            // 02.29 Insert End 

            base.Clear();
        }

        public override void RenderResult(DrawingContext dc) {
            base.RenderResult(dc);

            Pen drawPen = null;
            Brush drawBrush = null;

            if ((Result == EContextResult.Pass) || (Result == EContextResult.Fail))
            {
                drawPen = OkPen;
                drawBrush = OkColor;
            }
            else
            {
                drawPen = NgPen;
                drawBrush = NgColor;
            }

            //protected static Brush NgColor = Brushes.Red;
            //protected static Pen OkPen = new Pen(OkColor, 3);

            // 01.05 Base Coordination Center Cross Line Pen & Brush color
            Brush BaseCrossColor = Brushes.Blue;
            Pen BaseCrossPen = new Pen(BaseCrossColor, 8);

            // 01.03 late insert start
            if(bfinish)     // Inspection Rendering
            {
                // Base Coordination Center Cross Line (01.05 insert)
                //dc.DrawLine(BaseCrossPen, new System.Windows.Point(BottomInspectionContext.Cal_XBase - 50, BottomInspectionContext.Cal_YBase), new System.Windows.Point(BottomInspectionContext.Cal_XBase + 50, BottomInspectionContext.Cal_YBase));
                //dc.DrawLine(BaseCrossPen, new System.Windows.Point(BottomInspectionContext.Cal_XBase, BottomInspectionContext.Cal_YBase - 50), new System.Windows.Point(BottomInspectionContext.Cal_XBase, BottomInspectionContext.Cal_YBase + 50));

                /*
                // 02.29 주석 처리.
                dc.DrawLine(BaseCrossPen, new System.Windows.Point(BottomInspectionContext.ScreenCenter_X - 50, BottomInspectionContext.ScreenCenter_Y), new System.Windows.Point(BottomInspectionContext.ScreenCenter_X + 50, BottomInspectionContext.ScreenCenter_Y));
                dc.DrawLine(BaseCrossPen, new System.Windows.Point(BottomInspectionContext.ScreenCenter_X, BottomInspectionContext.ScreenCenter_Y - 50), new System.Windows.Point(BottomInspectionContext.ScreenCenter_X, BottomInspectionContext.ScreenCenter_Y + 50));
                */

                // 02.29 Insert Start
                if (CalSelector == (int)BottomInspectionParam.CalibrationBase.Picker)
                {
                    dc.DrawLine(BaseCrossPen, new System.Windows.Point(X_Picker[PickerNum]- 50, Y_Picker[PickerNum]), new System.Windows.Point(X_Picker[PickerNum] + 50, Y_Picker[PickerNum]));
                    dc.DrawLine(BaseCrossPen, new System.Windows.Point(X_Picker[PickerNum], Y_Picker[PickerNum] - 50), new System.Windows.Point(X_Picker[PickerNum], Y_Picker[PickerNum] + 50));
                }
                else
                {
                    dc.DrawLine(BaseCrossPen, new System.Windows.Point(BottomInspectionContext.ScreenCenter_X - 50, BottomInspectionContext.ScreenCenter_Y), new System.Windows.Point(BottomInspectionContext.ScreenCenter_X + 50, BottomInspectionContext.ScreenCenter_Y));
                    dc.DrawLine(BaseCrossPen, new System.Windows.Point(BottomInspectionContext.ScreenCenter_X, BottomInspectionContext.ScreenCenter_Y - 50), new System.Windows.Point(BottomInspectionContext.ScreenCenter_X, BottomInspectionContext.ScreenCenter_Y + 50));
                }
                // 02.29 Insert End

                // Die Center cross Line
                dc.DrawLine(drawPen, new System.Windows.Point(DieCenter_XArray[PickerNum] - 50, DieCenter_YArray[PickerNum]), new System.Windows.Point(DieCenter_XArray[PickerNum] + 50, DieCenter_YArray[PickerNum]));
                dc.DrawLine(drawPen, new System.Windows.Point(DieCenter_XArray[PickerNum], DieCenter_YArray[PickerNum] - 50), new System.Windows.Point(DieCenter_XArray[PickerNum], DieCenter_YArray[PickerNum] + 50));
                // 01.04 Picker Number 표기
                string pickerString = string.Format("PickerNum:{0}", PickerNum.ToString());
                FormattedText formatText = new FormattedText(pickerString, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, TextDrawFont, 50, drawBrush, 1.0); // Default FontSize is 60
                dc.DrawText(formatText, new System.Windows.Point(DieCenter_XArray[PickerNum] + 60, DieCenter_YArray[PickerNum] - 120));
                // 01.04 Die 중심값 및 Angle 값 표기
                pickerString = string.Format("X:{0:F03}, Y:{1:F03}", CenterOffsetXmmArray[PickerNum], CenterOffsetYmmArray[PickerNum]);
                formatText = new FormattedText(pickerString, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, TextDrawFont, 50, drawBrush, 1.0); // Default FontSize is 60
                dc.DrawText(formatText, new System.Windows.Point(DieCenter_XArray[PickerNum] + 60, DieCenter_YArray[PickerNum] - 60));

                pickerString = string.Format("Angle:{0:F03}", dAngleArray[PickerNum]);
                formatText = new FormattedText(pickerString, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, TextDrawFont, 50, drawBrush, 1.0); // Default FontSize is 60
                dc.DrawText(formatText, new System.Windows.Point(DieCenter_XArray[PickerNum] + 60, DieCenter_YArray[PickerNum] - 0));
            }
            // 01.03 late insert end

            if (bFoundCircle)   // Calibration Rendering.
            {
                // draw Circle
                Circle cirResultCircle = new Circle(dCenterX, dCenterY, dRad);
                dc.DrawEllipse(Brushes.Transparent, drawPen, new System.Windows.Point(dCenterX, dCenterY), dRad, dRad);

                //cross
                dc.DrawLine(drawPen, new System.Windows.Point(dCenterX - 50, dCenterY), new System.Windows.Point(dCenterX + 50, dCenterY));
                dc.DrawLine(drawPen, new System.Windows.Point(dCenterX, dCenterY - 50), new System.Windows.Point(dCenterX, dCenterY + 50));

                //string valueStr1 = string.Format("X : {0:0.000}, Y : {1:0.000}", dMovingCenterX, dMovingCenterY);
                //string valueStr1 = string.Format("X : {0:0.000}, Y : {1:0.000}", dCenterX, dCenterY);
                string valueStr1 = string.Format("X : {0:0.000}, Y : {1:0.000}", dDistX, dDistY);
                FormattedText formattedText1 = new FormattedText(valueStr1, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, TextDrawFont, FontSize, drawBrush, 1.0);
                dc.DrawText(formattedText1, new System.Windows.Point(dCenterX + 10, dCenterY + 60));

                //valueStr1 = string.Format("Radius: {0:0.000}, Angle: {1:0.000}", dRad, dAngle);
                //valueStr1 = string.Format("Radius: {0:0.000}", dRad);   // Radius pixel
                valueStr1 = string.Format("Radius:{0:0.000}", dRadmm);  // Radisu mm
                formattedText1 = new FormattedText(valueStr1, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, TextDrawFont, FontSize, drawBrush, 1.0);
                dc.DrawText(formattedText1, new System.Windows.Point(dCenterX + 10, dCenterY + 120));

            }
            else
            {
                // 못 찾았을 경우
                FormattedText formattedText1 = new FormattedText(ResultInfo.ToString(), CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, TextDrawFont, FontSize, drawBrush, 1.0);
                dc.DrawText(formattedText1, new System.Windows.Point(dCenterX + 10, dCenterY));
            }
        }

        public override void CopyFrom(ActionContext actionContext) {
            base.CopyFrom(actionContext);

            //pActionContext = actionContext;
            if (actionContext is BottomCalibrationContext)
            {
                BottomCalibrationContext = actionContext as BottomCalibrationContext;

                Result = actionContext.Result;

                bFoundCircle = BottomCalibrationContext.bFoundCircle;
                dCenterX = BottomCalibrationContext.CircleCenter_X;
                dCenterY = BottomCalibrationContext.CircleCenter_Y;
                dRad = BottomCalibrationContext.Radius;
                dRadmm = BottomCalibrationContext.dRadmm;

                /*
                dDistX = BottomCalibrationContext.dDistX;
                dDistY = BottomCalibrationContext.dDistY;
                */

                ResultInfo = BottomCalibrationContext.CalibrationResult;
                dDistX = CenterOffsetXmm = BottomCalibrationContext.CenterOffsetXmm;
                dDistY = CenterOffsetYmm = BottomCalibrationContext.CenterOffsetYmm;

            }
            else if(actionContext is BottomInspectionContext)
            {
                BottomInspectionContext = actionContext as BottomInspectionContext;

                Result = actionContext.Result;

                BottomDie = BottomInspectionContext.BottomDie;
                ResultInfo = BottomInspectionContext.InspectResult;

                bfinish = BottomInspectionContext.bFinish;
                ProcessName = BottomInspectionContext.ProcessName;

                ROI_Rect = BottomInspectionContext.ROI_Rect;

                // 01.03 insert
                for(int i=0; i<10; i++)
                {
                    CenterOffsetXmmArray[i] = BottomInspectionContext.CenterOffsetXmmArray[i];
                    CenterOffsetYmmArray[i] = BottomInspectionContext.CenterOffsetYmmArray[i];
                    dAngleArray[i] = BottomInspectionContext.dAngleArray[i];

                    DieCenter_XArray[i] = BottomInspectionContext.DieCenter_XArray[i];
                    DieCenter_YArray[i] = BottomInspectionContext.DieCenter_YArray[i];

                    // 02.29 Insert Start
                    X_Picker[i] = BottomInspectionContext.X_Picker_CalValue[i];
                    Y_Picker[i] = BottomInspectionContext.Y_Picker_CalValue[i];
                    // 02.29 Insert End
                }
                PickerNum = BottomInspectionContext.PickerNum;

                Cal_Xbase = BottomInspectionContext.Cal_XBase;
                Cal_Ybase = BottomInspectionContext.Cal_YBase;

                // 02.06 Insert Start
                ScreenCenter_X = BottomInspectionContext.ScreenCenter_X;
                ScreenCenter_Y = BottomInspectionContext.ScreenCenter_Y;
                // 02.06 Insert End

                // 02.29 Insert Start
                CalSelector = BottomInspectionContext.CalSelector;
                // 02.29 Insert End
            }
        }

        public override string ToString() {
            return null;
        }
    }

    public class BottomSequence : SequenceBase {
        private DeviceHandler pDevs;
        private VirtualCamera pCam;

        private readonly int AlgIndex;

        private BottomSequenceContext pMyContext;
        private CameraMasterParam pMyParam;
        
        private readonly string DefaultCamera;
        private readonly string DefaultLight;

        public BottomSequence(ESequence seqID, string name, int algIndex, string defaultCamera, string defaultLight) : base(seqID, name) {
            pDevs = SystemHandler.Handle.Devices;

            AlgIndex = algIndex;

            Context = new BottomSequenceContext(this);
            pMyContext = Context as BottomSequenceContext;

            Param = new CameraMasterParam(this);
            pMyParam = Param as CameraMasterParam;

            DefaultLight = defaultLight;
            DefaultCamera = defaultCamera;
        }

        protected override void AddResponse() {
            if (RequestPacket == null) return;

            TestResultPacket ResponsePacket = new TestResultPacket();

            ResponsePacket.Target = RequestPacket.Sender;
            ResponsePacket.Site = RequestPacket.Site;
            ResponsePacket.InspectionType = RequestPacket.TestType;

            
            if (Context is BottomSequenceContext) {
                BottomSequenceContext botContext = Context as BottomSequenceContext;

                if (ResponsePacket.InspectionType == (int)ETestType.Calibration)  // When type is Calibration,
                {
                    ResponsePacket.Result = botContext.ResultInfo;
                    ResponsePacket.Angle = botContext.dAngle;
                    ResponsePacket.X = botContext.CenterOffsetXmm;
                    ResponsePacket.Y = botContext.CenterOffsetYmm;
                }
                else if(ResponsePacket.InspectionType == (int)ETestType.Inspection) // When type is Inspection,
                {
                    //for(int i=0; i<10; i++)
                    //    ResponsePacket.visionResults[i] = botContext.visionResults[i]; 

                    Debug.WriteLine($"Sequence_Bottom Inspection Type: {ResponsePacket.InspectionType}");

                    int index = 0;
                    foreach (var items in botContext.BottomDie)
                    {
                        ResponsePacket.visionResults[index].X = items.Value.CenterOffsetXmm;
                        ResponsePacket.visionResults[index].Y = items.Value.CenterOffsetYmm;
                        ResponsePacket.visionResults[index].Angle = items.Value.DieAngle;

                        if (items.Value.Judgment == false)
                            ResponsePacket.visionResults[index].Result = EVisionResultType.NG;
                        else
                            ResponsePacket.visionResults[index].Result = EVisionResultType.OK;

                        index++;
                    }
                }
            }
            
            ResponseQueue.Enqueue(ResponsePacket);
        }

        public override void OnCreate() {
            pMyParam.LightGroupName = DefaultLight;
            pMyParam.DeviceName = DefaultCamera;

            pCam = pDevs[pMyParam.DeviceName];
            if (pCam == null) {
                //occurs error
                CustomMessageBox.Show("Error", string.Format("Camera {0} - Initialize Fail", pMyParam.DeviceName), System.Windows.MessageBoxImage.Error);
                IsInitialized = false;
                Context.State = EContextState.Error;
                return;
            }
            if (pCam.Properties == null) {
                //occurs error
                CustomMessageBox.Show("Error", string.Format("Camera Property {0} - Initialize Fail", pMyParam.DeviceName), System.Windows.MessageBoxImage.Error);
                IsInitialized = false;
                Context.State = EContextState.Error;
                return;
            }
            //initialize algorithm
            try {
                if (ALLIGATOR_ALG_MIL.agtAM_Init(AlgIndex, false, pCam.Properties.Width, pCam.Properties.Height) == false) {
                    CustomMessageBox.Show("Error", "Alligator algorithm MIL - Initialize Fail", System.Windows.MessageBoxImage.Error);
                    IsInitialized = false;
                }
                else {
                    IsInitialized = true;
                }

                for (int i = 0; i < 10; i++)
                {
                    if (ALLIGATOR_ALG_MIL.agtAM_Init(AlgIndex * 10 + i, false, pCam.Properties.Width, pCam.Properties.Height) == false)
                    {
                        CustomMessageBox.Show("Error", "Alligator algorithm MIL - Initialize Fail", System.Windows.MessageBoxImage.Error);
                        IsInitialized = false;
                    }
                }
            }
            catch (Exception e) {
                CustomMessageBox.Show("Error", string.Format("Alligator algorithm MIL - Error : {0}", e.Message), System.Windows.MessageBoxImage.Error);
            }

            base.OnCreate();
        }

        public override void OnLoad() {
            //light setting
            if (!SystemHandler.Handle.Lights.ApplyLight(pMyParam)) {
                //occurs error
            }
            base.OnLoad();
        }

        public override void OnRelease() {
            if (IsInitialized) {
                ALLIGATOR_ALG_MIL.agtAM_Free(AlgIndex);
                IsInitialized = false;
            }
            base.OnRelease();
        }
    }
}
