using AlligatorAlgMil;
using ChartDirector;
using OpenCvSharp;
using ReringProject.Define;
using ReringProject.Device;
using ReringProject.Network;
using ReringProject.Sequence;
using ReringProject.Setting;
using ReringProject.UI;
using ReringProject.Utility;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

using System.Runtime.Serialization.Formatters.Binary;
using System.IO;

namespace ReringProject.Sequence {

     public class WaferSequenceContext : SequenceContext {
        public bool bCalibrated { get; set; }   // 05.20 Insert Calibration Status
        WaferScanCalibrationActionContext WaferCalibrationContext { get; set; } // 05.20 Insert Calibration Instance

        //Response 정보
        public double CenterOffsetXmm { get; set; }
        public double CenterOffsetYmm { get; set; }
        public EVisionResultType ModelFinderResult { get; set; }
        public EVisionResultType AngleFoundResult { get; set; } // 12.21
        public EVisionResultType TeachingResult { get; set; }   // 05.20 Insert

        //wafer 정보
        public double X;
        public double Y;

        public int nMaxRow;
        public int nMaxCol;
        public int nTotalCell;

        /// <summary>
        /// Ransac Circle Found Variable
        /// </summary>
        public bool bFoundCircle { get; set; }  // Wafer Circle Found
        public double dCenterX { get; set; }
        public double dCenterY { get; set; }
        public double dMovingCenterX { get; set; }
        public double dMovingCenterY { get; set; }
        public double dRad { get; set; }
        public double dRadmm { get; set; }
        public double dAngle { get; set; }

        public bool bEraseChip { get; set; }
        public double dEraseLength { get; set; }
        public double dErasemm { get; set; }




        public int dFoundCount { get; set; }
        public int DieTotal { get; set; }
        public EVisionResultType ResultInfo { get; set; }

        public double nDie_Height { get; set; }
        public double nDie_Width { get; set; }
        public double WaferDegree { get; set; }
        public double WaferAngle { get; set; }

        /// <summary>
        /// Die Inspection Parameter
        /// </summary>
        public double Die_Width_Ratio { get; set; }
        public double Die_Height_Ratio { get; set; }
        public double Die_Area_Ratio { get; set; }
        public int Binary_Threshold { get; set; }

        public string ProcessName { get; set; }     // Process Name

        public string MapFileName { get; set; }     // MapFileName

        public double ScreenCenter_Xmm { get; set; }    // 12.17 Insert
        public double ScreenCenter_Ymm { get; set; }    // 12.17 Insert
        public double MMPerPixel_X { get; set; }        // 12.17 Insert
        public double MMPerPixel_Y { get; set; }        // 12.17 Insert


        WaferScanInspectionActionContext WaferContext { get; set; }
        public Dictionary<int, _ST_MAP_INFO> LeftWaferMap = new Dictionary<int, _ST_MAP_INFO>();
        public Dictionary<int, _ST_MAP_INFO> WaferMap = new Dictionary<int, _ST_MAP_INFO>();

        // Insert Date: 2023.10.31
        public List<_ST_MAP_INFO> LeftListMapInfo = new List<_ST_MAP_INFO>();
        public List<_ST_MAP_INFO> ListMapInfo = new List<_ST_MAP_INFO>();

        public Dictionary<System.Drawing.Point, Found_Die_Info> Found_Die = new Dictionary<System.Drawing.Point, Found_Die_Info>();   // Origin
        public Dictionary<System.Drawing.Point, Found_Die_Info> Left_Found_Die = new Dictionary<System.Drawing.Point, Found_Die_Info>();   // Origin

        public Mat CrobImage = new Mat();
        public Mat DrawImage = new Mat();       // 11.23
        public Mat LeftDrawImage = new Mat();       // 05.13

        /// <summary>
        /// ChartDirector Variable : MapFile
        /// </summary>
        public XYChart WaferChart;
        public PlotArea plot;
        public DiscreteHeatMapLayer layer;
        public ColorAxis cAxis;

        public bool bMapList { get; set; }

        public bool TeachingImage { get; set; } // 07.05 Insert

        public WaferSequenceContext(WaferSequence source) : base(source) {
            //생성자에서 clear를 호출하지 말것
        }

        public override void Clear() {
            X = 0;
            Y = 0;
            nMaxRow = 0;
            nMaxCol = 0;
            nTotalCell = 0;

            WaferMap.Clear();

            WaferChart = null;
            plot = null;
            layer = null;
            cAxis = null;


            bFoundCircle = false;
            dCenterX = 0;
            dCenterY = 0;
            dMovingCenterX = 0;
            dMovingCenterY = 0;
            dRad = 0;
            dRadmm = 0;
            dAngle = 0;

            bMapList = false;

            nDie_Height = 0;
            nDie_Width = 0;
            WaferDegree = 0;

            WaferAngle = 0;

            Die_Width_Ratio = 0;
            Die_Height_Ratio = 0;
            Die_Area_Ratio = 0;
            Binary_Threshold = 0;

            ProcessName = "NONE";
            MapFileName = "NONE";   // 03,.20

            TeachingImage = false;  // 07.05 Insert

            ScreenCenter_Xmm = 0.0; // 12.17 Insert
            ScreenCenter_Ymm = 0.0; // 12.17 Insert
            MMPerPixel_X = 0.0;
            MMPerPixel_Y = 0.0;

            base.Clear();
        }

        public override void RenderResult(DrawingContext dc)
        {
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

            if (bFoundCircle)
            {
                // draw Circle
                Circle cirResultCircle = new Circle(dCenterX, dCenterY, dRad);
                dc.DrawEllipse(Brushes.Transparent, drawPen, new System.Windows.Point(dCenterX, dCenterY), dRad, dRad);

                // 2025.04.16 minho
                if(bEraseChip == true)
                {
                    Pen pen2 = new Pen(Brushes.Red, 15);

                    dc.DrawEllipse(Brushes.Transparent, pen2, new System.Windows.Point(dCenterX, dCenterY), 
                        dRad - dEraseLength, dRad - dEraseLength);
                }

                //cross
                dc.DrawLine(drawPen, new System.Windows.Point(dCenterX - 50, dCenterY), new System.Windows.Point(dCenterX + 50, dCenterY));
                dc.DrawLine(drawPen, new System.Windows.Point(dCenterX, dCenterY - 50), new System.Windows.Point(dCenterX, dCenterY + 50));

                string valueStr1 = string.Format("X : {0:0.00}, Y : {1:0.00}", dMovingCenterX, dMovingCenterY);
                FormattedText formattedText1 = new FormattedText(valueStr1, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, TextDrawFont, FontSize, drawBrush, 1.0);
                dc.DrawText(formattedText1, new System.Windows.Point(dCenterX + 10, dCenterY + 60));

                valueStr1 = string.Format("Radius: {0:0.00}, Angle: {1:0.00}", dRadmm, dAngle);
                formattedText1 = new FormattedText(valueStr1, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, TextDrawFont, FontSize, drawBrush, 1.0);
                dc.DrawText(formattedText1, new System.Windows.Point(dCenterX + 10, dCenterY + 120));
            }
            else
            {
                // 못 찾았을 경우
                FormattedText formattedText1 = new FormattedText(ResultInfo.ToString(), CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, TextDrawFont, FontSize, drawBrush, 1.0);
                dc.DrawText(formattedText1, new System.Windows.Point(dCenterX + 10, dCenterY));
            }

            // Draw Rect
            if ((WaferContext != null) && (WaferContext.bFoundModel == true))
            {
                for (int j = 0; j < WaferContext.dFoundCount; )
                {
                    RotatedRect rotRect = new RotatedRect(new Point2f((float)WaferContext.dFoundModelX[j], (float)WaferContext.dFoundModelY[j]),
                        new Size2f(WaferContext.dFoundModelWidth[j], WaferContext.dFoundModelHeight[j]), (float)-WaferContext.dFoundModelAngle[j]);
                    Point2f[] pts = rotRect.Points();
                    for (int i = 0; i < 4; i++)
                    {
                        dc.DrawLine(drawPen, new System.Windows.Point(pts[i].X, pts[i].Y), new System.Windows.Point(pts[(i + 1) % 4].X, pts[(i + 1) % 4].Y));
                    }

                    //result text
                    FormattedText formattedText = new FormattedText(Result.ToString(), CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, TextDrawFont, FontSize, drawBrush, 1.0);
                    dc.DrawText(formattedText, new System.Windows.Point(WaferContext.dFoundModelX[j] + 10, WaferContext.dFoundModelY[j] + 10));

                    string valueStr = string.Format("X : {0:0.000}, Y : {1:0.000}", WaferContext.dFoundModelXWorld[j], WaferContext.dFoundModelYWorld[j]);
                    formattedText = new FormattedText(valueStr, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, TextDrawFont, FontSize, drawBrush, 1.0);
                    dc.DrawText(formattedText, new System.Windows.Point(WaferContext.dFoundModelX[j] + 10, WaferContext.dFoundModelY[j] + 60));

                    valueStr = string.Format("Score: {0:0.000}, Angle: {1:0.000}", WaferContext.dFoundModelScore[j], WaferContext.dFoundModelAngle[j]);
                    formattedText = new FormattedText(valueStr, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, TextDrawFont, FontSize, drawBrush, 1.0);
                    dc.DrawText(formattedText, new System.Windows.Point(WaferContext.dFoundModelX[j] + 10, WaferContext.dFoundModelY[j] + 120));

                    j = j + 1;      // Debuging 진행 시 화면에 표시되는 영역 사각형과 값들이 너무 많을 때 상수값을 변경하면서 진행.

                }
            }
        }

        public override void CopyFrom(ActionContext actionContext) {
            base.CopyFrom(actionContext);
            //action이 가지고 있는 결과 데이터를 자신한테 복사, action context 포인터를 가지고 있어야 함

            // 05.20 Insert WaferScanCalibrationActionContext Start
            if (actionContext is WaferScanCalibrationActionContext)
            {
                WaferCalibrationContext = actionContext as WaferScanCalibrationActionContext;
                bCalibrated = WaferCalibrationContext.bCalibrated;
            }
            // 05.20 Insert WaferScanCalibrationActionContext End
            if (actionContext is WaferScanInspectionActionContext)
            {
                //WaferContext = actionContext as WaferScanActionContext;
                WaferContext = actionContext as WaferScanInspectionActionContext;

                Result = actionContext.Result;

                // Ransac Result
                bFoundCircle = WaferContext.bFoundCircle;
                dCenterX = WaferContext.dFoundCenterX;
                dCenterY = WaferContext.dFoundCenterY;

                // 05.23 Wafer Circle의 중심이 화면 중심으로 이동해야 하는 값이 필요할때 사용.
                dMovingCenterX = WaferContext.CenterOffsetXmm;     // World Coordination X
                dMovingCenterY = WaferContext.CenterOffsetYmm;     // World Coordination Y 

                dRad = WaferContext.dRadius;
                dRadmm = WaferContext.dRadmm;

                bEraseChip = WaferContext.bEraseChip;
                dEraseLength = WaferContext.dEraseLength;
                dErasemm = WaferContext.dErasemm;

                //Orientation Result
                dAngle = WaferContext.dAngle;

                dFoundCount = WaferContext.dFoundCount;     // Model Finder 에서 찾은 Die 개수
                DieTotal = WaferContext.DieTotal;           // 찾은 Die 들 중 존재 유무가 확인된 Die의 개수.

                nMaxRow = WaferContext.nMaxRow;
                nMaxCol = WaferContext.nMaxCol;
                nTotalCell = WaferContext.nTotalCell;

                TeachingImage = WaferContext.TeachingImage;

                // Left 또는 Right zone에서 Grab 한 Wafer 이미지에 대한 결과를 깊은 복사를 통해 저장.
                // 해당 class 내에 Clone 메소드를 구성.
                // UI 에서 Left 와 Right 전환 후 ListView의 아이템을 클릭 시 응답하지 않는 문제 해결.
                if (WaferContext.ProcessName == "Left WAFER")
                {
                    LeftWaferMap = WaferContext.MapData.ToDictionary(entry => entry.Key, entry => (_ST_MAP_INFO)entry.Value.Clone());
                }
                else
                {
                    WaferMap = WaferContext.MapData.ToDictionary(entry => entry.Key, entry => (_ST_MAP_INFO)entry.Value.Clone());
                }

                // ChartDirector Variable Result.
                WaferChart = WaferContext.WaferMap;
                plot = WaferContext.plot;
                layer = WaferContext.layer;
                cAxis = WaferContext.cAxis;

                if (WaferContext.ProcessName == "Left WAFER")
                {
                    LeftListMapInfo.Clear();    // 새로운 Wafer Grab 후 Clear 되지 않으면, 계속 추가되는 현상이 발생.
                    // WaferView ListView Data  11.02
                    for (int idx = 0; idx < LeftWaferMap.Count; idx++)
                    {
                        _ST_MAP_INFO temp = new _ST_MAP_INFO();

                        temp.Org_X = LeftWaferMap.ElementAt(idx).Value.Org_X;
                        temp.Org_Y = LeftWaferMap.ElementAt(idx).Value.Org_Y;
                        temp.Bin = LeftWaferMap.ElementAt(idx).Value.Bin;
                        temp.Pos_X = LeftWaferMap.ElementAt(idx).Value.Pos_X;
                        temp.Pos_Y = LeftWaferMap.ElementAt(idx).Value.Pos_Y;
                        temp.Succ = LeftWaferMap.ElementAt(idx).Value.Succ;
                        temp.Tgt_X = LeftWaferMap.ElementAt(idx).Value.Tgt_X;
                        temp.Tgt_Y = LeftWaferMap.ElementAt(idx).Value.Tgt_Y;

                        // insert Data Field
                        temp.ContourCount = LeftWaferMap.ElementAt(idx).Value.ContourCount;
                        temp.Area = LeftWaferMap.ElementAt(idx).Value.Area;
                        temp.Convex = LeftWaferMap.ElementAt(idx).Value.Convex;
                        temp.Apex = LeftWaferMap.ElementAt(idx).Value.Apex;

                        LeftListMapInfo.Add(temp);
                    }
                }
                else
                {
                    ListMapInfo.Clear();    // 새로운 Wafer Grab 후 Clear 되지 않으면, 계속 추가되는 현상이 발생.
                    // WaferView ListView Data  11.02
                    for (int idx = 0; idx < WaferMap.Count; idx++)
                    {
                        _ST_MAP_INFO temp = new _ST_MAP_INFO();

                        temp.Org_X = WaferMap.ElementAt(idx).Value.Org_X;
                        temp.Org_Y = WaferMap.ElementAt(idx).Value.Org_Y;
                        temp.Bin = WaferMap.ElementAt(idx).Value.Bin;
                        temp.Pos_X = WaferMap.ElementAt(idx).Value.Pos_X;
                        temp.Pos_Y = WaferMap.ElementAt(idx).Value.Pos_Y;
                        temp.Succ = WaferMap.ElementAt(idx).Value.Succ;
                        temp.Tgt_X = WaferMap.ElementAt(idx).Value.Tgt_X;
                        temp.Tgt_Y = WaferMap.ElementAt(idx).Value.Tgt_Y;

                        // insert Data Field
                        temp.ContourCount = WaferMap.ElementAt(idx).Value.ContourCount;
                        temp.Area = WaferMap.ElementAt(idx).Value.Area;
                        temp.Convex = WaferMap.ElementAt(idx).Value.Convex;
                        temp.Apex = WaferMap.ElementAt(idx).Value.Apex;

                        ListMapInfo.Add(temp);
                    }
                }

                bMapList = WaferContext.bMapList;

                if (WaferContext.ProcessName == "Left WAFER")
                {
                    Left_Found_Die = WaferContext.Found_Die.ToDictionary(entry => entry.Key, entry => (Found_Die_Info)entry.Value.Clone());
                }
                else
                {
                    Found_Die = WaferContext.Found_Die.ToDictionary(entry => entry.Key, entry => (Found_Die_Info)entry.Value.Clone());
                }
                
                // Exception
                try
                {
                    CrobImage = WaferContext.CrobImage.Clone();     // Die Crob image   11.23

                    if (WaferContext.ProcessName == "Left WAFER")
                    {
                        LeftDrawImage = WaferContext.CrobImage.Clone();     // Die Crob image   11.23
                        Cv2.CvtColor(LeftDrawImage, LeftDrawImage, ColorConversionCodes.GRAY2BGR);  // 12.17
                    }
                    else
                    {
                        DrawImage = WaferContext.CrobImage.Clone();     // Die Crob image   11.23
                        Cv2.CvtColor(DrawImage, DrawImage, ColorConversionCodes.GRAY2BGR);  // 12.17
                    }
                }
                catch (Exception e)
                {
                    Logging.PrintErrLog((int)ELogType.Error, string.Format("Sequence Wafer Image Clone Exception : {0})", "Sequence_Wafer", e.Message));
                }

                if (bFoundCircle)   // Waferview 이미지에 Corss Line, Circle Center Cross Line, Circle Center X, Y, Radius 그리기.
                {
                    if (WaferContext.ProcessName == "Left WAFER")
                    {
                        // 11.23 insert by jdhan
                        Cv2.Circle(LeftDrawImage, (int)dCenterX, (int)dCenterY, (int)dRad, new Scalar(255, 0, 255), 20);


                        // 2025.04.16 insert by minho
                        if (bEraseChip)
                            Cv2.Circle(LeftDrawImage, (int)dCenterX, (int)dCenterY, (int)(dRad - dEraseLength), new Scalar(0, 0, 255), 10);

                        Cv2.Line(LeftDrawImage, new OpenCvSharp.Point(0, (7000 / 2) - 1), new OpenCvSharp.Point(7008 - 1, (7000 / 2) - 1), new Scalar(255, 0, 255), 20, LineTypes.AntiAlias);   // 12.17 Insert
                        Cv2.Line(LeftDrawImage, new OpenCvSharp.Point((7008 / 2) - 1, 0), new OpenCvSharp.Point((7008 / 2)-1, 7000 - 1), new Scalar(255, 0, 255), 20, LineTypes.AntiAlias);     // 12.17 Insert

                        // Circel Center Cross Line
                        Cv2.Line(LeftDrawImage, new OpenCvSharp.Point(dCenterX - 50, dCenterY), new OpenCvSharp.Point(dCenterX + 50, dCenterY), new Scalar(255, 0, 0), 20, LineTypes.AntiAlias);
                        Cv2.Line(LeftDrawImage, new OpenCvSharp.Point(dCenterX, dCenterY - 50), new OpenCvSharp.Point(dCenterX, dCenterY + 50), new Scalar(255, 0, 0), 20, LineTypes.AntiAlias);

                        string Coord = string.Format("X : {0:0.00}, Y : {1:0.00}", dMovingCenterX, dMovingCenterY);
                        Cv2.PutText(LeftDrawImage, Coord, new OpenCvSharp.Point(dCenterX + 150, dCenterY + 100),
                            HersheyFonts.HersheyTriplex, 3, Scalar.Blue, 2, LineTypes.AntiAlias);

                        string Rad_Angle = string.Format("Radius: {0:0.00}, Angle: {1:0.00}", dRadmm, dAngle);
                        Cv2.PutText(LeftDrawImage, Rad_Angle, new OpenCvSharp.Point(dCenterX + 150, dCenterY + 200),
                            HersheyFonts.HersheyTriplex, 3, Scalar.Blue, 2, LineTypes.AntiAlias);
                    }
                    else
                    {

                        // 11.23 insert by jdhan
                        Cv2.Circle(DrawImage, (int)dCenterX, (int)dCenterY, (int)dRad, new Scalar(255, 0, 255), 20);

                        // 2025.04.16 insert by minho
                        if (bEraseChip)
                            Cv2.Circle(DrawImage, (int)dCenterX, (int)dCenterY, (int)(dRad - dEraseLength), new Scalar(0, 0, 255), 10);

                        Cv2.Line(DrawImage, new OpenCvSharp.Point(0, (7000 / 2) - 1), new OpenCvSharp.Point(7008 - 1, (7000 / 2) -1), new Scalar(255, 0, 255), 20, LineTypes.AntiAlias);
                        Cv2.Line(DrawImage, new OpenCvSharp.Point((7008 / 2) - 1, 0), new OpenCvSharp.Point((7008 / 2) - 1, 7000 - 1), new Scalar(255, 0, 255), 20, LineTypes.AntiAlias);

                        // Circel Center Cross Line
                        Cv2.Line(DrawImage, new OpenCvSharp.Point(dCenterX - 50, dCenterY), new OpenCvSharp.Point(dCenterX + 50, dCenterY), new Scalar(255, 0, 0), 20, LineTypes.AntiAlias);
                        Cv2.Line(DrawImage, new OpenCvSharp.Point(dCenterX, dCenterY - 50), new OpenCvSharp.Point(dCenterX, dCenterY + 50), new Scalar(255, 0, 0), 20, LineTypes.AntiAlias);

                        string Coord = string.Format("X : {0:0.00}, Y : {1:0.00}", dMovingCenterX, dMovingCenterY);
                        Cv2.PutText(DrawImage, Coord, new OpenCvSharp.Point(dCenterX + 150, dCenterY + 100),
                            HersheyFonts.HersheyTriplex, 3, Scalar.Blue, 2, LineTypes.AntiAlias);

                        string Rad_Angle = string.Format("Radius: {0:0.00}, Angle: {1:0.00}", dRadmm, dAngle);
                        Cv2.PutText(DrawImage, Rad_Angle, new OpenCvSharp.Point(dCenterX + 150, dCenterY + 200),
                            HersheyFonts.HersheyTriplex, 3, Scalar.Blue, 2, LineTypes.AntiAlias);

                    }
                }

                string Jugment = null;
                if(WaferContext.ModelFinderResult == EVisionResultType.OK)
                {
                    Jugment = string.Format("Die Model Found!");
                    if(WaferContext.ProcessName == "Left WAFER")
                        Cv2.PutText(LeftDrawImage, Jugment, new OpenCvSharp.Point(dCenterX + 150, dCenterY + 350 ), HersheyFonts.HersheyTriplex, 3, Scalar.BlueViolet, 2, LineTypes.AntiAlias);
                    else
                        Cv2.PutText(DrawImage, Jugment, new OpenCvSharp.Point(dCenterX + 150, dCenterY + 350 ), HersheyFonts.HersheyTriplex, 3, Scalar.BlueViolet, 2, LineTypes.AntiAlias);
                }
                else
                {
                    Jugment = string.Format("Error: Die Model Not Found!");
                    if((WaferContext.ProcessName == "Left WAFER") && (LeftDrawImage != null))   // 01.24 insert DrawImage가 Null 상태에서는 Exception 발생하므로, 조거문 추가.
                        Cv2.PutText(LeftDrawImage, Jugment, new OpenCvSharp.Point(dCenterX + 150, dCenterY + 350 ), HersheyFonts.HersheyTriplex, 3, Scalar.Red, 2, LineTypes.AntiAlias);
                    else if((WaferContext.ProcessName != "Left WAFER") && (DrawImage != null))   // 01.24 insert DrawImage가 Null 상태에서는 Exception 발생하므로, 조거문 추가.
                        Cv2.PutText(DrawImage, Jugment, new OpenCvSharp.Point(dCenterX + 150, dCenterY + 350 ), HersheyFonts.HersheyTriplex, 3, Scalar.Red, 2, LineTypes.AntiAlias);

                }

                // WaferView에 결과 이미지 표시 12.08
                if ((WaferContext != null) && (WaferContext.bFoundModel == true))
                {
                    // ModelFinder에서 찾은 Die 모두를 표시
                    for (int j = 0; j < WaferContext.dFoundCount; j++)
                    {
                        RotatedRect rotRect = new RotatedRect(new Point2f((float)WaferContext.dFoundModelX[j], (float)WaferContext.dFoundModelY[j]),
                                                new Size2f(WaferContext.dFoundModelWidth[j], WaferContext.dFoundModelHeight[j]), (float)-WaferContext.dFoundModelAngle[j]);
                        Point2f[] pts = rotRect.Points();
                        for (int i = 0; i < 4; i++)
                        {
                            if(WaferContext.ProcessName == "Left WAFER")
                                Cv2.Line(LeftDrawImage, new OpenCvSharp.Point(pts[i].X, pts[i].Y), new OpenCvSharp.Point(pts[(i + 1) % 4].X, pts[(i + 1) % 4].Y), new Scalar(76, 153, 0), 5, LineTypes.AntiAlias);
                            else
                                Cv2.Line(DrawImage, new OpenCvSharp.Point(pts[i].X, pts[i].Y), new OpenCvSharp.Point(pts[(i + 1) % 4].X, pts[(i + 1) % 4].Y), new Scalar(76, 153, 0), 5, LineTypes.AntiAlias);

                        }
                    }

                    if (WaferContext.ProcessName == "Left WAFER")
                    {
                                
                        // Found_Die는 ModelFinder에서 찾은 Die 중 MapFile에 존재하는 Die 정보를 저장하고 있는 데이터.
                        foreach (var Items in Left_Found_Die)
                        {
                            // minho     
                            if (bEraseChip == true)
                            {
                                double left_x = Items.Value.X_Pos - Items.Value.Width / 2.0;
                                double top_y = Items.Value.Y_Pos - Items.Value.Height / 2.0;
                                double right_x = Items.Value.X_Pos + Items.Value.Width / 2.0;
                                double bottom_y = Items.Value.Y_Pos + Items.Value.Height / 2.0;

                                double lt_length = Math.Sqrt(((left_x - dCenterX) * (left_x - dCenterX)) + ((top_y - dCenterY) * (top_y - dCenterY))); // left-top
                                double rt_length = Math.Sqrt(((right_x - dCenterX) * (right_x - dCenterX)) + ((top_y - dCenterY) * (top_y - dCenterY))); // right-top

                                double lb_length = Math.Sqrt(((left_x - dCenterX) * (left_x - dCenterX)) + ((bottom_y - dCenterY) * (bottom_y - dCenterY))); // left-bottom
                                double rb_length = Math.Sqrt(((right_x - dCenterX) * (right_x - dCenterX)) + ((bottom_y - dCenterY) * (bottom_y - dCenterY))); // right-bottom

                                var clength = dRad - dEraseLength;
                                if (clength < lt_length ||
                                    clength < rt_length ||
                                    clength < lb_length ||
                                    clength < rb_length
                                    )
                                {
                                    continue;
                                }
                            }

                            
                            RotatedRect rotRect = new RotatedRect(new Point2f((float)Items.Value.X_Pos, (float)Items.Value.Y_Pos),
                                                    new Size2f(Items.Value.Width, Items.Value.Height), (float)Items.Value.Angle);

                            Point2f[] pts = rotRect.Points();

                            for (int i = 0; i < 4; i++)
                            {
                                //11.23 insert by jdhan
                                //Found Die 외곽을 감싸는 사각 Line 그리기.
                                Cv2.Line(LeftDrawImage, new OpenCvSharp.Point(pts[i].X, pts[i].Y), new OpenCvSharp.Point(pts[(i + 1) % 4].X, pts[(i + 1) % 4].Y), new Scalar(0, 255, 255), 5, LineTypes.AntiAlias);
                            }

                            // Found Die의 중심에 Small Cross Line 그리기.
                            Cv2.Line(LeftDrawImage, new OpenCvSharp.Point(Items.Value.X_Pos - 15, Items.Value.Y_Pos), new OpenCvSharp.Point(Items.Value.X_Pos + 15, Items.Value.Y_Pos), new Scalar(255, 255, 0), 20, LineTypes.AntiAlias);
                            Cv2.Line(LeftDrawImage, new OpenCvSharp.Point(Items.Value.X_Pos, Items.Value.Y_Pos - 15), new OpenCvSharp.Point(Items.Value.X_Pos, Items.Value.Y_Pos + 15), new Scalar(255, 255, 0), 20, LineTypes.AntiAlias);
                        }
                    }
                    else
                    {
                        // Found_Die는 ModelFinder에서 찾은 Die 중 MapFile에 존재하는 Die 정보를 저장하고 있는 데이터.
                        foreach (var Items in Found_Die)
                        {
                            // minho     
                            if (bEraseChip == true)
                            {
                                double left_x = Items.Value.X_Pos - Items.Value.Width / 2.0;
                                double top_y = Items.Value.Y_Pos - Items.Value.Height / 2.0;
                                double right_x = Items.Value.X_Pos + Items.Value.Width / 2.0;
                                double bottom_y = Items.Value.Y_Pos + Items.Value.Height / 2.0;
                                                               
                                double lt_length = Math.Sqrt(((left_x - dCenterX) * (left_x - dCenterX)) + ((top_y - dCenterY) * (top_y - dCenterY))); // left-top
                                double rt_length = Math.Sqrt(((right_x - dCenterX) * (right_x - dCenterX)) + ((top_y - dCenterY) * (top_y - dCenterY))); // right-top

                                double lb_length = Math.Sqrt(((left_x - dCenterX) * (left_x - dCenterX)) + ((bottom_y - dCenterY) * (bottom_y - dCenterY))); // left-bottom
                                double rb_length = Math.Sqrt(((right_x - dCenterX) * (right_x - dCenterX)) + ((bottom_y - dCenterY) * (bottom_y - dCenterY))); // right-bottom

                                var clength = dRad - dEraseLength;
                                if (clength < lt_length ||
                                    clength < rt_length ||
                                    clength < lb_length ||
                                    clength < rb_length
                                    )
                                {
                                    continue;
                                }
                            }
                            RotatedRect rotRect = new RotatedRect(new Point2f((float)Items.Value.X_Pos, (float)Items.Value.Y_Pos),
                                new Size2f(Items.Value.Width, Items.Value.Height), (float)Items.Value.Angle);

                            Point2f[] pts = rotRect.Points();

                            for (int i = 0; i < 4; i++)
                            {
                                //11.23 insert by jdhan
                                //Found Die 외곽을 감싸는 사각 Line 그리기.
                                Cv2.Line(DrawImage, new OpenCvSharp.Point(pts[i].X, pts[i].Y), new OpenCvSharp.Point(pts[(i + 1) % 4].X, pts[(i + 1) % 4].Y), new Scalar(0, 255, 255), 5, LineTypes.AntiAlias);
                            }

                            // Found Die의 중심에 Small Cross Line 그리기.
                            Cv2.Line(DrawImage, new OpenCvSharp.Point(Items.Value.X_Pos - 15, Items.Value.Y_Pos), new OpenCvSharp.Point(Items.Value.X_Pos + 15, Items.Value.Y_Pos), new Scalar(255, 255, 0), 20, LineTypes.AntiAlias);
                            Cv2.Line(DrawImage, new OpenCvSharp.Point(Items.Value.X_Pos, Items.Value.Y_Pos - 15), new OpenCvSharp.Point(Items.Value.X_Pos, Items.Value.Y_Pos + 15), new Scalar(255, 255, 0), 20, LineTypes.AntiAlias);
                        }
                    }
                }

                WaferDegree = WaferContext.WaferDegree; // Wafer Rotate 
                nDie_Width = WaferContext.nDie_Width;   // Die Width
                nDie_Height = WaferContext.nDie_Height; // Die Height

                //Response data
                ModelFinderResult = WaferContext.ModelFinderResult;
                AngleFoundResult = WaferContext.AngleFoundResult;

                TeachingResult = WaferContext.TechingResult;    // 05.20 Insert

                CenterOffsetXmm = WaferContext.CenterOffsetXmm;
                CenterOffsetYmm = WaferContext.CenterOffsetYmm;

                WaferAngle = WaferContext.dAngle;       // Wafer Angle

                Die_Width_Ratio = WaferContext.Die_Width_Ratio;
                Die_Height_Ratio = WaferContext.Die_Height_Ratio;
                Die_Area_Ratio = WaferContext.Die_Area_Ratio;
                Binary_Threshold = WaferContext.Binary_Threshold;

                ProcessName = WaferContext.ProcessName;     // Process Name

                MapFileName = WaferContext.MapFileName;     // 03.20 Insert

                ScreenCenter_Xmm = WaferContext.ScreenCenter_Xmm;   // 12.17 Insert
                ScreenCenter_Ymm = WaferContext.ScreenCenter_Ymm;   // 12.17 Insert
                MMPerPixel_X = WaferContext.MMPerPixel_X;           // 12.17 Insert
                MMPerPixel_Y = WaferContext.MMPerPixel_Y;           // 12.17 Insert

                // WaferImage Tab에 Out Of Range Angle 발생시 표시. 01.02
                Jugment = null;
                if(AngleFoundResult == EVisionResultType.ANG)
                {
                    Jugment = string.Format("Angle Out of Range - Angle: {0:0.00}", dAngle);
                    if((WaferContext.ProcessName == "Left WAFER") && (LeftDrawImage != null))   // 01.24 insert DrawImage가 Null 상태에서는 Exception 발생하므로, 조거문 추가.
                        Cv2.PutText(LeftDrawImage, Jugment, new OpenCvSharp.Point(150, 550), HersheyFonts.HersheyTriplex, 3, Scalar.Red, 2, LineTypes.AntiAlias);
                    else if((WaferContext.ProcessName != "Left WAFER") && (DrawImage != null))   // 01.24 insert DrawImage가 Null 상태에서는 Exception 발생하므로, 조거문 추가.
                        Cv2.PutText(DrawImage, Jugment, new OpenCvSharp.Point(150, 550), HersheyFonts.HersheyTriplex, 3, Scalar.Red, 2, LineTypes.AntiAlias);
                }
            }
        }

        public override string ToString() {
            return null;
        }

     }

    public class WaferSequence : SequenceBase {
        private DeviceHandler pDevs;
        private VirtualCamera pCam;

        private readonly int AlgIndex;

        private WaferSequenceContext pMyContext;
        private CameraMasterParam pMyParam;
        
        private readonly string DefaultCamera;
        private readonly string DefaultLight;

        public WaferSequence(ESequence seqID, string name, int algIndex, string defaultCamera, string defaultLight) : base(seqID, name) {
            pDevs = SystemHandler.Handle.Devices;

            AlgIndex = algIndex;

            Context = new WaferSequenceContext(this);
            pMyContext = Context as WaferSequenceContext;

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

            
            if (Context is WaferSequenceContext) {
                WaferSequenceContext waferContext = Context as WaferSequenceContext;

                // 05.20 Calibration ResponsePacket Insert Start
                if (ResponsePacket.InspectionType == (int)ETestType.Calibration)
                {
                    if (bCreated == true)
                        ResponsePacket.Result = EVisionResultType.OK;
                    else
                        ResponsePacket.Result = EVisionResultType.NG;

                    ResponsePacket.Angle = 0;
                    ResponsePacket.X = 0;
                    ResponsePacket.Y = 0;
                }
                // 05.20 Calibration ResponsePacket Insert End
                else
                {
                    // Wafer 회전각이 설정된 값을 벗어 났을 때, 보정 요청을 위해 추가.
                    if (waferContext.AngleFoundResult == EVisionResultType.ANG)      // 12.21
                        ResponsePacket.Result = waferContext.AngleFoundResult;
                    else if (waferContext.TeachingResult == EVisionResultType.TECHING)  // 05.20   Insert
                        ResponsePacket.Result = waferContext.TeachingResult;
                    else
                        ResponsePacket.Result = waferContext.ModelFinderResult;

                    ResponsePacket.Angle = waferContext.dAngle;         // Wafer Angle
                    ResponsePacket.X = waferContext.CenterOffsetXmm;    // Wafer Center X
                    ResponsePacket.Y = waferContext.CenterOffsetYmm;    // Wafer Center Y
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
                //ALLIGATOR_ALG_MIL.rmrjs (AlgIndex);
                IsInitialized = false;
            }
            base.OnRelease();
        }
    }
}
