using OpenCvSharp;
using OpenCvSharp.Extensions;
using ReringProject.Define;
using ReringProject.Halcon;
using ReringProject.Sequence;
using ReringProject.Setting;
using ReringProject.UI;
using ReringProject.Utility;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;

using ImageGlass;

namespace ReringProject.UI
{
    /// <summary>
    /// WaferMapRightView.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class WaferMapRightView : UserControl, IMainView
    {
        private GridViewColumnHeader listViewSortCol = null;
        private SortAdorner listViewSortAdorner = null;

        public Dictionary<string, SequenceContext> ContextList { get; set; }
        private object Interlock = new object();

        public ImageBoxEx IGWaferImage { get => iGWaferImage; set => iGWaferImage = value; }
        private ImageBoxEx iGWaferImage;   // ImageGlass Bottom Object

        public ImageBoxEx IGSelectedDieImage { get => iGSelectedDieImage; set => iGSelectedDieImage = value; }  // 01.24 Insert
        private ImageBoxEx iGSelectedDieImage;  // 01.24 Insert


        public ImageBoxEx IGBotImage { get => iGBotImage; set => iGBotImage = value; }
        private ImageBoxEx iGBotImage;   // ImageGlass Bottom Object


        System.Windows.Forms.Label MouseWheel_Label = new System.Windows.Forms.Label();
        private readonly HashSet<ImageBoxEx> DraggingImageBoxes = new HashSet<ImageBoxEx>();

        static public double dNewWidth { get; set; }
        static public double dNewHeight { get; set; }

        Mat[] FrontBotImg = null;
        Mat[] RearBotImg = null;

        private Mat PImage = null;   // 08.05 Insert PImage는 MouseMove에 따라 Wafer 이미지의 픽셀 값을 가져오기 위한 이미지.


        // Multiview의 ListView에 Die 정보를 추가하기 위한 Data Class
        public class MAP_Die_info : INotifyPropertyChanged
        {
            public event PropertyChangedEventHandler PropertyChanged;
            private void NotifyPropertyChanged(int prop)
            {
                PropertyChangedEventHandler handler = PropertyChanged;
            }

            private void NotifyPropertyChanged(string prop)
            {
                PropertyChangedEventHandler handler = PropertyChanged;
            }

            public int NUM
            {
                get { return _NUM; }
                set
                {
                    _NUM = value;
                    NotifyPropertyChanged(NUM);
                }
            }
            private int _NUM;

            public int OX
            {
                get { return _OX; }
                set
                {
                    _OX = value;
                    NotifyPropertyChanged(OX);
                }
            }
            private int _OX;

            public int OY
            {
                get { return _OY;  }
                set
                {
                    _OY = value;
                    NotifyPropertyChanged(_OY);
                }
            }
            private int _OY;

            public int BIN
            {
                get { return _BIN; }
                set
                {
                    _BIN = value;
                    NotifyPropertyChanged(_OY);
                }
            }
            private int _BIN;

            public string TX
            {
                get { return _TX;  }
                set
                {
                    _TX = value;
                    NotifyPropertyChanged(_TX);
                }
            }
            private string _TX;

            public string TY
            {
                get { return _TY; }
                set
                {
                    _TY = value;
                    NotifyPropertyChanged(_TX);
                }
            }
            private string _TY;

            public string ST
            {
                get { return _ST; }
                set
                {
                    _ST = value;
                    NotifyPropertyChanged(_ST);
                }
            }
            private string _ST;

            private static List<MAP_Die_info> instance; // List View에 저장하는 객체.

            public static List<MAP_Die_info> GetInstance()
            {
                if (instance == null)
                    instance = new List<MAP_Die_info>();
                return instance;
            }

            public MAP_Die_info()
            {
                NUM = 0;
                OX = 0;
                OY = 0;
                BIN = 0;
                TX = null;
                TY = null;

                ST = null;
            }
        }

        // Bottom List View Data class
        public class Bottom_List_Data : INotifyPropertyChanged
        {
            public event PropertyChangedEventHandler PropertyChanged;

            private void NotifyPropertyChanged(int prop)
            {
                PropertyChangedEventHandler handler = PropertyChanged;
            }

            private void NotifyPropertyChanged(string prop)
            {
                PropertyChangedEventHandler handler = PropertyChanged;
            }

            public int NUM
            {
                get { return _NUM; }
                set
                {
                    _NUM = value;
                    NotifyPropertyChanged(NUM);
                }
            }
            private int _NUM;

            public string X_POS
            {
                get { return _X_POS; }
                set
                {
                    _X_POS = value;
                    NotifyPropertyChanged(X_POS);
                }
            }
            private string _X_POS;

            public string Y_POS
            {
                get { return _Y_POS; }
                set
                {
                    _Y_POS = value;
                    NotifyPropertyChanged(Y_POS);
                }
            }
            private string _Y_POS;

            public string ROT
            {
                get { return _ROT; }
                set
                {
                    _ROT = value;
                    NotifyPropertyChanged(ROT);
                }
            }
            private string _ROT;

            public string ST
            {
                get { return _ST; }
                set
                {
                    _ST = value;
                    NotifyPropertyChanged(ST);
                }
            }
            private string _ST;

            public Bottom_List_Data()
            {
                NUM = 0;
                X_POS = null;
                Y_POS = null;
                ROT = null;
                ST = null;
            }
        }


        public WaferMapRightView()
        {
            InitializeComponent();

            PSName_Label.Content = "WAFER MAP";
            Info_Label.Content = "WAFER Information";
            Die_Info.Content = "Die Information";

            dNewWidth = 0;
            dNewHeight = 0;
            
            // WaferMapView MapFile HeatMap Default Scale
            MapSacleTrans.ScaleX = 1.0;
            MapSacleTrans.ScaleY = 1.0;

            // Bottom Inspection Image Default Scale
            ScaleImg0.ScaleX = 0.05;
            ScaleImg0.ScaleY = 0.05;

            ScaleImg1.ScaleX = 0.05;
            ScaleImg1.ScaleY = 0.05;


            // ImageGlass Object LeftMouse button: ZOOM IN, RightMouse button: ZOOM OUT
            IGBotImage = new ImageBoxEx();
            IGBotImage.AllowClickZoom = true;
            IGBotImage.ShowPixelGrid = true;
            IGBotImage.HorizontalScrollBarStyle = ImageBoxScrollBarStyle.Hide;
            IGBotImage.VerticalScrollBarStyle = ImageBoxScrollBarStyle.Hide;
            RegisterImageBoxCursorEvents(IGBotImage);
            IGBotImage.Zoom = 5.0;


            MouseWheel_Label.Text = "WAFER_IMAGE_LABEL";
            WaferImage.Child = MouseWheel_Label;


            // ImageGlass Object
            IGWaferImage = new ImageBoxEx();
            IGWaferImage.Name = "Wafer";
            IGWaferImage.AllowClickZoom = true;
            IGWaferImage.ShowPixelGrid = true;
            IGWaferImage.AutoCenter = true;
            IGWaferImage.AutoSize = true;
            IGWaferImage.HorizontalScrollBarStyle = ImageBoxScrollBarStyle.Hide;
            IGWaferImage.VerticalScrollBarStyle = ImageBoxScrollBarStyle.Hide;
            IGWaferImage.MouseWheel += new System.Windows.Forms.MouseEventHandler(MouseWheel);  // ImageGlass의 MouseWheel Event 등록.     12.13

            IGWaferImage.MouseMove += MouseMove;
            RegisterImageBoxCursorEvents(IGWaferImage);

            IGWaferImage.Zoom = 10.0;
            IGWaferImage.ZoomToFit();

            // 01.24 Insert start
            IGSelectedDieImage = new ImageBoxEx();
            IGSelectedDieImage.Name = "SelectedDie";
            IGSelectedDieImage.AllowClickZoom = true;
            IGSelectedDieImage.ShowPixelGrid = true;
            IGSelectedDieImage.AutoCenter = true;
            IGSelectedDieImage.AutoSize = true;
            IGSelectedDieImage.HorizontalScrollBarStyle = ImageBoxScrollBarStyle.Hide;
            IGSelectedDieImage.VerticalScrollBarStyle = ImageBoxScrollBarStyle.Hide;
            IGSelectedDieImage.MouseWheel += new System.Windows.Forms.MouseEventHandler(MouseWheel);
            RegisterImageBoxCursorEvents(IGSelectedDieImage);
            IGSelectedDieImage.Zoom = 50.0;
            IGSelectedDieImage.ZoomToFit();
            // 01.24 Insert End


        }

        private void RegisterImageBoxCursorEvents(ImageBoxEx imageBox)
        {
            imageBox.MouseEnter += ImageBox_MouseEnter;
            imageBox.MouseLeave += ImageBox_MouseLeave;
            imageBox.MouseDown += ImageBox_MouseDown;
            imageBox.MouseUp += ImageBox_MouseUp;
            imageBox.MouseWheel += ImageBox_MouseWheel;
        }

        private void ImageBox_MouseEnter(object sender, EventArgs e)
        {
            UpdateImageBoxCursor(sender as ImageBoxEx);
        }

        private void ImageBox_MouseLeave(object sender, EventArgs e)
        {
            var imageBox = sender as ImageBoxEx;
            if (imageBox == null)
                return;

            DraggingImageBoxes.Remove(imageBox);
            imageBox.Cursor = System.Windows.Forms.Cursors.Default;
        }

        private void ImageBox_MouseDown(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            var imageBox = sender as ImageBoxEx;
            if (imageBox == null)
                return;

            if (e.Button == System.Windows.Forms.MouseButtons.Left && CanPanImageBox(imageBox))
                DraggingImageBoxes.Add(imageBox);

            UpdateImageBoxCursor(imageBox);
        }

        private void ImageBox_MouseUp(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            var imageBox = sender as ImageBoxEx;
            if (imageBox == null)
                return;

            DraggingImageBoxes.Remove(imageBox);
            UpdateImageBoxCursor(imageBox);
        }

        private void ImageBox_MouseWheel(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            UpdateImageBoxCursor(sender as ImageBoxEx);
        }

        private void UpdateImageBoxCursor(ImageBoxEx imageBox)
        {
            if (imageBox == null)
                return;

            if (DraggingImageBoxes.Contains(imageBox) && CanPanImageBox(imageBox))
            {
                imageBox.Cursor = System.Windows.Forms.Cursors.SizeAll;
                return;
            }

            imageBox.Cursor = CanPanImageBox(imageBox)
                ? System.Windows.Forms.Cursors.Hand
                : System.Windows.Forms.Cursors.Default;
        }

        private bool CanPanImageBox(ImageBoxEx imageBox)
        {
            if (imageBox == null || imageBox.Image == null)
                return false;

            if (imageBox.ClientSize.Width <= 0 || imageBox.ClientSize.Height <= 0)
                return false;

            var zoomedWidth = imageBox.Image.Width * imageBox.ZoomFactor;
            var zoomedHeight = imageBox.Image.Height * imageBox.ZoomFactor;

            return zoomedWidth > imageBox.ClientSize.Width || zoomedHeight > imageBox.ClientSize.Height;
        }

        /// <summary>
        /// Converter to ImageGlass Image
        /// </summary>
        /// <param name="bitmapsource"></param>
        /// <returns></returns>
        public System.Drawing.Bitmap BitmapFromSource(BitmapSource bitmapsource)
        {
            System.Drawing.Bitmap bitmap;
            using (MemoryStream outStream = new MemoryStream())
            {
                BitmapEncoder enc = new BmpBitmapEncoder();
                enc.Frames.Add(BitmapFrame.Create(bitmapsource));
                enc.Save(outStream);
                bitmap = new System.Drawing.Bitmap(outStream);
            }
            return bitmap;
        }

        private static BitmapFrame CreateBitmapFrameFromMat(Mat img)
        {
            using (var backgroundImageStream = new MemoryStream())
            {
                img.WriteToStream(backgroundImageStream, ".bmp");
                backgroundImageStream.Seek(0, SeekOrigin.Begin);
                return BitmapFrame.Create(backgroundImageStream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            }
        }

        public bool Display(string name, string result, Brush resultBrush, object param = null)
        {
            if (ContextList == null || !ContextList.ContainsKey(name))
            {
                return true;
            }
            SequenceContext context = ContextList[name];
            using (Mat img = context == null || context.ResultHalconImage == null ? null : HalconImageBridge.ToMat(context.ResultHalconImage))
            {
                return RenderDisplay(name, img, result, resultBrush, param);
            }
        }

        private bool RenderDisplay(string name, Mat img, string result, Brush resultBrush, object param = null)
        {
            SequenceContext cont = ContextList[name];

            string actName = null;
            if (param is string) actName = (string)param;

            try
            {
                lock (Interlock)
                {
                    if (img != null && (img.Empty() == false))
                    {
                        {
                            BitmapFrame frame = CreateBitmapFrameFromMat(img);

                            switch (cont.Source.ID)
                            {
                                case ESequence.Wafer:
                                    WaferSequenceContext waferContext = ContextList[name] as WaferSequenceContext;

                                    if ((waferContext.bMapList == true) && (waferContext.ProcessName == "Right WAFER"))
                                    {
                                        // Run Initial UI Start
                                        textBox_waferDegree.Text = "0";
                                        textBox_centerX.Text = "0";
                                        textBox_centerY.Text = "0";
                                        textBox_dieTotal.Text = "0";
                                        textBox_valuedDies.Text = "0";

                                        textBox_Name.Text = "NONE";   // 03.20 Insert

                                        textBox_MaxCol.Text = "0";
                                        textBox_MaxRow.Text = "0";

                                        listView_dieInfo.ItemsSource = null;
                                        listView_dieInfo.Items.Clear();
                                        listView_dieInfo.Items.Refresh();

                                        // 현재 실행 중인 쓰레드의 Dispatcher 를 가져와 빈 Delegate를 호출하여 UI 갱신.
                                        System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(System.Windows.Threading.DispatcherPriority.Background,
                                        new System.Threading.ThreadStart(delegate { }));
                                        // Run Initial UI End

                                        // ProcessName String을 가져와 왼쪽 또는 오른쪽 Wafer 검사인지 표시.
                                        PSName_Label.Content = Info_Label.Content = Die_Info.Content = waferContext.ProcessName;
                                        PSName_Label.Content += " MAP";         // Wafer Map (Left or Right)
                                        Info_Label.Content += " Information";   // Wafer Information (Left or Right)
                                        Die_Info.Content += " Die Information"; // Die Information (Left or Right)

                                        if ((IGWaferImage.ZoomFactor < 0.1) || (IGWaferImage.ZoomFactor > 0.15))
                                        {
                                            IGWaferImage.BeginUpdate();
                                            IGWaferImage.Zoom = 10.0;

                                            IGWaferImage.AllowClickZoom = true;
                                            IGWaferImage.ShowPixelGrid = true;

                                            IGWaferImage.VerticalScrollBarStyle = ImageBoxScrollBarStyle.Hide;
                                            IGWaferImage.HorizontalScrollBarStyle = ImageBoxScrollBarStyle.Hide;

                                            IGWaferImage.AutoCenter = true;
                                            IGWaferImage.AutoSize = true;
                                            IGWaferImage.ZoomToFit();

                                            IGWaferImage.Image = BitmapFromSource(OpenCvSharp.WpfExtensions.WriteableBitmapConverter.ToWriteableBitmap(waferContext.DrawImage));
                                            IGWaferImage.CenterToImage();
                                            IGWaferImage.EndUpdate();

                                            WaferImage.Child = IGWaferImage;

                                            // 08.05 Insert
                                            if (waferContext.TeachingImage == true)
                                                PImage = OpenCvSharp.WpfExtensions.BitmapSourceConverter.ToMat(frame);
                                            else
                                                PImage = waferContext.DrawImage;    // 08.05 Insert
                                        }
                                        else
                                        {
                                            IGWaferImage.BeginUpdate();
                                            IGWaferImage.Zoom = 10.0;

                                            IGWaferImage.AutoCenter = true;
                                            IGWaferImage.AutoSize = true;
                                            IGWaferImage.ZoomToFit();
                                            IGWaferImage.Image = BitmapFromSource(OpenCvSharp.WpfExtensions.WriteableBitmapConverter.ToWriteableBitmap(waferContext.DrawImage));
                                            IGWaferImage.CenterToImage();
                                            IGWaferImage.EndUpdate();
                                            WaferImage.Child = IGWaferImage;

                                            // 08.05 Insert
                                            if (waferContext.TeachingImage == true)
                                                PImage = OpenCvSharp.WpfExtensions.BitmapSourceConverter.ToMat(frame);
                                            else
                                                PImage = waferContext.DrawImage;    // 08.05 Insert
                                        }

                                        //Wafer ChartDirector HeatMap Data  
                                        chart_waferMap.updateDisplay(true);                     // ChartDirector HeatMapLayer update, Update the Display
                                        chart_waferMap.Chart = waferContext.WaferChart;
                                        chart_waferMap.ImageMap = waferContext.WaferChart.getHTMLImageMap("clickable", "", "title='<*cdml*>({xLabel}, {yLabel}) = {z|2}'");

                                        // Wafer info
                                        textBox_waferDegree.Text = String.Format("{0:F2}", waferContext.WaferAngle);
                                        textBox_centerX.Text = String.Format("{0:F2}", waferContext.dMovingCenterX);
                                        textBox_centerY.Text = String.Format("{0:F2}", waferContext.dMovingCenterY);
                                        textBox_dieTotal.Text = String.Format("{0}", waferContext.dFoundCount);
                                        textBox_valuedDies.Text = String.Format("{0}", waferContext.DieTotal);

                                        textBox_Name.Text = waferContext.MapFileName;   // 03.20 Insert

                                        textBox_MaxCol.Text = String.Format("{0}", waferContext.nMaxCol);
                                        textBox_MaxRow.Text = String.Format("{0}", waferContext.nMaxRow);

                                        listView_dieInfo.Items.Refresh();
                                        ObservableCollection<MAP_Die_info> items = new ObservableCollection<MAP_Die_info>();
                                        items.Clear();
                                        listView_dieInfo.ItemsSource = items;

                                        listView_dieInfo.Items.Refresh();

                                        for (int i = 0; i < waferContext.ListMapInfo.Count; i++)
                                        {
                                            MAP_Die_info temp = new MAP_Die_info();

                                            temp.NUM = i;
                                            temp.OX = waferContext.ListMapInfo[i].Tgt_X;
                                            temp.OY = waferContext.ListMapInfo[i].Tgt_Y;
                                            temp.BIN = waferContext.ListMapInfo[i].Bin;
                                            temp.TX = string.Format("{0:F2}", waferContext.ListMapInfo[i].Pos_X);
                                            temp.TY = string.Format("{0:F2}", waferContext.ListMapInfo[i].Pos_Y);
                                            temp.ST = waferContext.ListMapInfo[i].Succ == 1 ? "GOOD" : "BAD";

                                            items.Add(temp);
                                            listView_dieInfo.ItemsSource = items;
                                            listView_dieInfo.Items.Refresh();

                                        }
                                    }
                                    else
                                    {
                                        if ((waferContext.bMapList == false) && (actName == "Inspect_Right"))
                                        {
                                            if ((IGWaferImage.ZoomFactor < 0.1) || (IGWaferImage.ZoomFactor > 0.15))
                                            {
                                                IGWaferImage.BeginUpdate();
                                                IGWaferImage.Zoom = 10.0;

                                                IGWaferImage.AllowClickZoom = true;
                                                IGWaferImage.ShowPixelGrid = true;

                                                IGWaferImage.VerticalScrollBarStyle = ImageBoxScrollBarStyle.Hide;
                                                IGWaferImage.HorizontalScrollBarStyle = ImageBoxScrollBarStyle.Hide;

                                                IGWaferImage.AutoCenter = true;
                                                IGWaferImage.AutoSize = true;
                                                IGWaferImage.ZoomToFit();

                                                IGWaferImage.Image = BitmapFromSource(frame); // Oringin Image
                                                IGWaferImage.CenterToImage();
                                                IGWaferImage.EndUpdate();

                                                WaferImage.Child = IGWaferImage;
                                            }
                                            else
                                            {
                                                IGWaferImage.BeginUpdate();
                                                IGWaferImage.Zoom = 10.0;

                                                IGWaferImage.AutoCenter = true;
                                                IGWaferImage.AutoSize = true;
                                                IGWaferImage.ZoomToFit();

                                                IGWaferImage.Image = BitmapFromSource(frame); // Oringin Image
                                                IGWaferImage.CenterToImage();
                                                IGWaferImage.EndUpdate();

                                                WaferImage.Child = IGWaferImage;
                                            }

                                            // 08.05 Insert
                                            PImage = OpenCvSharp.WpfExtensions.BitmapSourceConverter.ToMat(frame);  // 08.05 Insert
                                            Debug.WriteLine($"Before Image CH: {PImage.Channels()}");

                                            PSName_Label.Content = "NONE Wafer";

                                            textBox_waferDegree.Text = null;
                                            textBox_centerX.Text = null;
                                            textBox_centerY.Text = null;
                                            textBox_dieTotal.Text = null;
                                            textBox_valuedDies.Text = null;

                                            textBox_Name.Text = "NONE";   // 03.20 Insert

                                            textBox_MaxCol.Text = null;
                                            textBox_MaxRow.Text = null;

                                            listView_dieInfo.ItemsSource = null;
                                            listView_dieInfo.Items.Clear();
                                            listView_dieInfo.Items.Refresh();
                                        }
                                    }
                                    break;

                                case ESequence.FrontTop:
                                    break;
                                case ESequence.RearTop:
                                    break;

                                case ESequence.FrontBottom:
                                case ESequence.RearBottom:
                                    BottomSequenceContext BotContext = ContextList[name] as BottomSequenceContext;

                                    string SourceName = BotContext.Source.Name;
                                    Bottom_Inspection.Content = "Bottom Inspection";

                                    if (BotContext.bfinish == true)
                                    {
                                        ObservableCollection<Bottom_List_Data> items = new ObservableCollection<Bottom_List_Data>();

                                        if (SourceName == "FRONT_BOTTOM")
                                        {
                                            FrontBotImg = new Mat[BotContext.BottomDie.Count];           // 전역 데이터 필드 사용

                                            listView_FrontBotInfo.Items.Refresh();
                                            items.Clear();
                                            listView_FrontBotInfo.ItemsSource = items;
                                            listView_FrontBotInfo.Items.Refresh();
                                        }
                                        else if(SourceName == "REAR_BOTTOM")
                                        {
                                            RearBotImg = new Mat[BotContext.BottomDie.Count];           // 전역 데이터 필드 사용

                                            listView_RearBotInfo.Items.Refresh();
                                            items.Clear();
                                            listView_RearBotInfo.ItemsSource = items;
                                            listView_RearBotInfo.Items.Refresh();
                                        }

                                        BottomDieInfo temp = new BottomDieInfo();
                                        for (int i = 0; i < BotContext.BottomDie.Count; i++)
                                        {
                                            Bottom_List_Data BotListData = new Bottom_List_Data();

                                            try
                                            {
                                                BotContext.BottomDie.TryGetValue(i, out temp);

                                                if (SourceName == "FRONT_BOTTOM")
                                                    FrontBotImg[i] = temp.image;
                                                else if(SourceName == "REAR_BOTTOM")
                                                    RearBotImg[i] = temp.image;

                                                BotListData.NUM = i;
                                                BotListData.X_POS = string.Format("{0:F2}", temp.CenterOffsetXmm); // DieCenter_X
                                                BotListData.Y_POS = string.Format("{0:F2}", temp.CenterOffsetXmm); // DieCenter_Y
                                                BotListData.ROT = string.Format("{0:F2}", temp.DieAngle);

                                                // 10.10 Insert 
                                                if (temp.Judgment == false && temp.newJudgment == -1)
                                                    BotListData.ST = "BAD";
                                                else if (temp.Judgment == false && temp.newJudgment == 0)
                                                    BotListData.ST = "Empty";
                                                else
                                                    BotListData.ST = "GOOD";

                                                items.Add(BotListData);

                                                if (SourceName == "FRONT_BOTTOM")
                                                {
                                                    listView_FrontBotInfo.ItemsSource = items;
                                                    listView_FrontBotInfo.Items.Refresh();
                                                }
                                                else if(SourceName == "REAR_BOTTOM")
                                                {
                                                    listView_RearBotInfo.ItemsSource = items;
                                                    listView_RearBotInfo.Items.Refresh();
                                                }
                                            }
                                            catch (Exception e)
                                            {
                                                FrontBotImg[i] = null;
                                                RearBotImg[i] = null;

                                                Logging.PrintErrLog((int)ELogType.Error, string.Format("Exception {0}, ReturnCode:{1}", e.ToString(), result));
                                            }
                                        }

                                        // 01.03 추가 시작 부분
                                        if (SourceName == "FRONT_BOTTOM")
                                        {
                                            image_Front_bottom.BeginInit();
                                            image_Front_bottom.Source = frame;        // Grab Image
                                            image_Front_bottom.EndInit();
                                        }
                                        else if (SourceName == "REAR_BOTTOM")
                                        {
                                            image_Rear_bottom.BeginInit();
                                            image_Rear_bottom.Source = frame;
                                            image_Rear_bottom.EndInit();
                                        }
                                    }
                                    else
                                    {
                                        if (SourceName == "FRONT_BOTTOM")
                                        {
                                            Mat frameImage = null;
                                            frameImage = OpenCvSharp.WpfExtensions.BitmapSourceConverter.ToMat(frame);
                                            Mat cutImage = null;

                                            if (BotContext.ROI_Rect.X == 0 && BotContext.ROI_Rect.Y == 0)
                                            {
                                                OpenCvSharp.Rect InitRect = new OpenCvSharp.Rect();
                                                InitRect.X = frameImage.Width / 4;
                                                InitRect.Y = frameImage.Height / 4;
                                                InitRect.Width = frameImage.Width / 4 * 2;
                                                InitRect.Height = frameImage.Height / 4 * 2;
                                                cutImage = frameImage.SubMat(InitRect);
                                            }
                                            else
                                            {
                                                cutImage = frameImage.SubMat(BotContext.ROI_Rect);
                                            }

                                            image_Front_bottom.BeginInit();
                                            image_Front_bottom.Source = OpenCvSharp.WpfExtensions.WriteableBitmapConverter.ToWriteableBitmap(cutImage);
                                            image_Front_bottom.EndInit();

                                            listView_FrontBotInfo.ItemsSource = null;
                                            listView_FrontBotInfo.Items.Clear();
                                            listView_FrontBotInfo.Items.Refresh();
                                        }
                                        else if (SourceName == "REAR_BOTTOM")
                                        {
                                            Mat frameImage = null;
                                            frameImage = OpenCvSharp.WpfExtensions.BitmapSourceConverter.ToMat(frame);
                                            Mat cutImage = null;

                                            if (BotContext.ROI_Rect.X == 0 && BotContext.ROI_Rect.Y == 0)
                                            {
                                                OpenCvSharp.Rect InitRect = new OpenCvSharp.Rect();
                                                InitRect.X = frameImage.Width / 4;
                                                InitRect.Y = frameImage.Height / 4;
                                                InitRect.Width = frameImage.Width / 4 * 2;
                                                InitRect.Height = frameImage.Height / 4 * 2;
                                                cutImage = frameImage.SubMat(InitRect);
                                            }
                                            else
                                            {
                                                cutImage = frameImage.SubMat(BotContext.ROI_Rect);
                                            }

                                            image_Rear_bottom.BeginInit();
                                            image_Rear_bottom.Source = OpenCvSharp.WpfExtensions.WriteableBitmapConverter.ToWriteableBitmap(cutImage);
                                            image_Rear_bottom.EndInit();

                                            listView_RearBotInfo.ItemsSource = null;
                                            listView_RearBotInfo.Items.Clear();
                                            listView_RearBotInfo.Items.Refresh();
                                        }
                                    }
                                    break;

                            }
                        }
                    }
                }
            }
            catch(Exception ex)
            {
                Logging.PrintErrLog((int)ELogType.Error, string.Format("Error while DisplayResult in MultiResultView ({0})", ex.Message));
                return false;
            }

            return true;
        }

        /// <summary>
        /// Board Size change event
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void chartBorder_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            // border의 새로운 너비와 높이를 얻기
            dNewWidth = e.NewSize.Width;
            dNewHeight = e.NewSize.Height;
        }

        private void ListView_dieInfo_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            SequenceContext cont = ContextList["WAFER"];
            WaferSequenceContext waferContext = ContextList["WAFER"] as WaferSequenceContext;

            // ListView에서 선택한 item의 Tgt_X, Tgt_Y 에 대한 Key 값 저장 변수.
            System.Drawing.Point Coord = new System.Drawing.Point();


            // Select Die 에 보여질 Crob Image 생성.

            int MatchingDieCnt = waferContext.Found_Die.Count;

            // Select Item Index 초기화. 0이면 처음것 선택된 상태로 진행되기 때문에 -1로 초기화 필요.
            int selectedIndex = -1;

            // ListView Items 의 데이터들은 MAP_Die_info 형식으로 되어 있기 때문에 선택한 아이템의 데이터를 저장할 변수 선언.
            // 선택된 한개의 데이터 처리만 필요하기 때문에 List collection  사용하지 않음.
            MAP_Die_info SelectDie = new MAP_Die_info();

            // 리스트뷰에서 한개의 아이템을 선택하였다면,
            if (listView_dieInfo.SelectedItems.Count == 1)
            {
                // 선택한 리스트의 인덱스 번호를 가져오고,
                selectedIndex = listView_dieInfo.SelectedIndex;

                // ListView 형식을 MAP_Die_info 형식으로 캐스팅 필요.
                SelectDie = (MAP_Die_info)listView_dieInfo.Items[selectedIndex];

                Coord.X = SelectDie.OX;
                Coord.Y = SelectDie.OY;

                for (int i = 0; i < MatchingDieCnt; i++)
                {
                    Found_Die_Info temp = new Found_Die_Info();

                    Coord.X = SelectDie.OX;
                    Coord.Y = SelectDie.OY;

                    if (waferContext.Found_Die.ContainsKey(Coord))
                    {
                        // 01.20 Insert and Change Start
                        waferContext.Found_Die.TryGetValue(Coord, out temp);

                        // 01.22 Insert Start
                        if (temp.Die_Image != null)
                        {
                            Cv2.PutText(temp.Die_Image, "Contours cnt:" + temp.ContourCount.ToString(), new OpenCvSharp.Point(20, 20), HersheyFonts.HersheyComplex, 0.25, Scalar.White, 1, LineTypes.AntiAlias);
                            Cv2.PutText(temp.Die_Image, "Area:" + temp.Area.ToString(), new OpenCvSharp.Point(20, 30), HersheyFonts.HersheyComplex, 0.25, Scalar.White, 1, LineTypes.AntiAlias);
                            Cv2.PutText(temp.Die_Image, "Apex Count:" + temp.Apex.ToString(), new OpenCvSharp.Point(20, 40), HersheyFonts.HersheyComplex, 0.25, Scalar.White, 1, LineTypes.AntiAlias);
                        }
                        // 01.22 Insert End
                        // 02.21 insert Start
                        else
                        {
                            Logging.PrintErrLog((int)ELogType.Error, $"Wafer Map View(R) - Not Found Image: X:{Coord.X}, Y:{Coord.Y}");
                            break;
                        }
                        // 02.21 insert End

                        // 01.22 Insert Start
                        if ((IGSelectedDieImage.ZoomFactor > 1.0) || (IGSelectedDieImage.ZoomFactor < 0.5))
                        {
                            IGSelectedDieImage.BeginUpdate();
                            IGSelectedDieImage.Zoom = 100.0;
                            IGSelectedDieImage.AllowClickZoom = true;
                            IGSelectedDieImage.ShowPixelGrid = true;
                            IGSelectedDieImage.VerticalScrollBarStyle = ImageBoxScrollBarStyle.Hide;
                            IGSelectedDieImage.HorizontalScrollBarStyle = ImageBoxScrollBarStyle.Hide;
                            IGSelectedDieImage.AutoSize = true;
                            IGSelectedDieImage.AutoCenter = true;
                            IGSelectedDieImage.ZoomToFit();
                            IGSelectedDieImage.Image = BitmapFromSource(OpenCvSharp.WpfExtensions.WriteableBitmapConverter.ToWriteableBitmap(temp.Die_Image));
                            IGSelectedDieImage.CenterToImage();

                            IGSelectedDieImage.EndUpdate();

                            SelectedDie.Child = IGSelectedDieImage;

                        }
                        else
                        {
                            IGSelectedDieImage.BeginUpdate();
                            IGSelectedDieImage.Zoom = 100.0;

                            IGSelectedDieImage.AutoSize = true;
                            IGSelectedDieImage.AutoCenter = true;
                            IGSelectedDieImage.ZoomToFit();

                            IGSelectedDieImage.Image = BitmapFromSource(OpenCvSharp.WpfExtensions.WriteableBitmapConverter.ToWriteableBitmap(temp.Die_Image));
                            IGSelectedDieImage.CenterToImage();
                            IGSelectedDieImage.EndUpdate();

                            SelectedDie.Child = IGSelectedDieImage;
                        }



                        // 12.08 Insert WaferImage Select Die Rect Drawing 

                        Mat DrawClone = waferContext.DrawImage.Clone(); // listView에서 선택한 영역만 그리기 위해 이미지 복사 생성.

                        RotatedRect rotRect = new RotatedRect(new Point2f((float)waferContext.Found_Die[Coord].X_Pos, (float)waferContext.Found_Die[Coord].Y_Pos),
                                                new Size2f(waferContext.Found_Die[Coord].Width, waferContext.Found_Die[Coord].Height), (float)waferContext.Found_Die[Coord].Angle);
                        Point2f[] pts = rotRect.Points();

                        for (int k = 0; k < 4; k++)
                        {
                            Cv2.Line(DrawClone, new OpenCvSharp.Point(pts[k].X, pts[k].Y), new OpenCvSharp.Point(pts[(k + 1) % 4].X, pts[(k + 1) % 4].Y), new Scalar(0, 0, 255), 15, LineTypes.AntiAlias);
                        }

                        if ((IGWaferImage.ZoomFactor < 0.1) || (IGWaferImage.ZoomFactor > 0.15))
                        {
                            IGWaferImage.BeginUpdate();
                            IGWaferImage.Zoom = 10.0;

                            IGWaferImage.AllowClickZoom = true;
                            IGWaferImage.ShowPixelGrid = true;

                            IGWaferImage.VerticalScrollBarStyle = ImageBoxScrollBarStyle.Hide;
                            IGWaferImage.HorizontalScrollBarStyle = ImageBoxScrollBarStyle.Hide;

                            IGWaferImage.AutoCenter = true;
                            IGWaferImage.AutoSize = true;
                            IGWaferImage.ZoomToFit();

                            IGWaferImage.Image = BitmapFromSource(OpenCvSharp.WpfExtensions.WriteableBitmapConverter.ToWriteableBitmap(DrawClone)); // 복사 생성한 이미지 표시
                            IGWaferImage.CenterToImage();
                            IGWaferImage.EndUpdate();

                            WaferImage.Child = IGWaferImage;
                        }
                        else
                        {
                            IGWaferImage.BeginUpdate();
                            IGWaferImage.Zoom = 10.0;

                            IGWaferImage.AutoCenter = true;
                            IGWaferImage.AutoSize = true;
                            IGWaferImage.ZoomToFit();

                            IGWaferImage.Image = BitmapFromSource(OpenCvSharp.WpfExtensions.WriteableBitmapConverter.ToWriteableBitmap(DrawClone)); // 복사 생성한 이미지 표시
                            IGWaferImage.CenterToImage();

                            IGWaferImage.EndUpdate();

                            WaferImage.Child = IGWaferImage;
                        }

                        break;
                    }
                    else
                    {
                        break;
                    }
                }
            }
        }

        private void GridViewColumnHeader_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            GridViewColumnHeader column = (sender as GridViewColumnHeader);
            string sortBy = column.Tag.ToString();  // OX, OY, BIN, ST, TX, TY

            if(listViewSortCol != null)
            {
                AdornerLayer.GetAdornerLayer(listViewSortCol).Remove(listViewSortAdorner);
                listView_dieInfo.Items.SortDescriptions.Clear();
            }

            ListSortDirection newDir = ListSortDirection.Ascending;

            if (listViewSortCol == column && listViewSortAdorner.Direction == newDir)
                newDir = ListSortDirection.Descending;

            listViewSortCol = column;
            listViewSortAdorner = new SortAdorner(listViewSortCol, newDir);
            AdornerLayer.GetAdornerLayer(listViewSortCol).Add(listViewSortAdorner);

            listView_dieInfo.Items.SortDescriptions.Add(new SortDescription(sortBy, newDir));
        }

        public class SortAdorner : Adorner
        {
            private static Geometry ascGeometry =
                    Geometry.Parse("M 0 4 L 3.5 0 L 7 4 Z");

            private static Geometry descGeometry =
                    Geometry.Parse("M 0 0 L 3.5 4 L 7 0 Z");

            public ListSortDirection Direction { get; private set; }

            public SortAdorner(UIElement element, ListSortDirection dir)
                    : base(element)
            {
                this.Direction = dir;
            }

            protected override void OnRender(DrawingContext drawingContext)
            {
                base.OnRender(drawingContext);

                if (AdornedElement.RenderSize.Width < 20)
                    return;

                TranslateTransform transform = new TranslateTransform
                        (
                                AdornedElement.RenderSize.Width - 15,
                                (AdornedElement.RenderSize.Height - 5) / 2
                        );
                drawingContext.PushTransform(transform);

                Geometry geometry = ascGeometry;
                if (this.Direction == ListSortDirection.Descending)
                    geometry = descGeometry;
                drawingContext.DrawGeometry(Brushes.Black, null, geometry);

                drawingContext.Pop();
            }
        }



        // WaferView Mouse Event insert by jdhan
        public System.Windows.Point scrollMousePoint = new System.Windows.Point();
        public System.Windows.Point lastDragPoint;
        double hOff = 4;    // Origin 2

        private void ScrollViewer_PreviewMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
        {
            ScrollViewer ChoiceScrollViewer = (sender as ScrollViewer);

            double zoom = e.Delta > 0 ? .05 : -.05;

            switch(ChoiceScrollViewer.Name)
            {
                case "WaferMapScroll":
                    MapSacleTrans.ScaleX += zoom;
                    MapSacleTrans.ScaleY += zoom;

                    if ((MapSacleTrans.ScaleX <= 0.5) || (MapSacleTrans.ScaleY <= 0.5))
                    {
                        MapSacleTrans.ScaleX = 1.0;
                        MapSacleTrans.ScaleY = 1.0;
                    }
                    break;

                case "ScrollImage0":
                    ScaleImg0.ScaleX += zoom;
                    ScaleImg0.ScaleY += zoom;

                    if ((ScaleImg0.ScaleX <= 0.1) || (ScaleImg0.ScaleY <= 0.1))
                    {
                        ScaleImg0.ScaleX = 0.2;
                        ScaleImg0.ScaleY = 0.2;
                    }
                    break;

                case "ScrollImage1":
                    ScaleImg1.ScaleX += zoom;
                    ScaleImg1.ScaleY += zoom;

                    if ((ScaleImg1.ScaleX <= 0.1) || (ScaleImg1.ScaleY <= 0.1))
                    {
                        ScaleImg1.ScaleX = 0.2;
                        ScaleImg1.ScaleY = 0.2;
                    }
                    break;

            }
            e.Handled = true;
        }
        
        private void ScrollView_PreviewMouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if(WaferMapScroll.IsMouseCaptured)
                WaferMapScroll.ReleaseMouseCapture();
            else if (ScrollImage0.IsMouseCaptured)
                ScrollImage0.ReleaseMouseCapture();
            else if (ScrollImage1.IsMouseCaptured)
                ScrollImage1.ReleaseMouseCapture();
        }

        private void ScrollViewer_PreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if(WaferMapScroll.IsMouseCaptured)
            {
                if (lastDragPoint.Y < e.GetPosition(WaferMapScroll).Y)
                    WaferMapScroll.ScrollToVerticalOffset(WaferMapScroll.VerticalOffset - hOff);
                if (lastDragPoint.Y > e.GetPosition(WaferMapScroll).Y)
                    WaferMapScroll.ScrollToVerticalOffset(WaferMapScroll.VerticalOffset + hOff);

                if (lastDragPoint.X < e.GetPosition(WaferMapScroll).X)
                    WaferMapScroll.ScrollToHorizontalOffset(WaferMapScroll.HorizontalOffset - hOff);
                if (lastDragPoint.X > e.GetPosition(WaferMapScroll).X)
                    WaferMapScroll.ScrollToHorizontalOffset(WaferMapScroll.HorizontalOffset + hOff);
            }
            else if (ScrollImage0.IsMouseCaptured)
            {
                if (lastDragPoint.Y < e.GetPosition(ScrollImage0).Y)
                    ScrollImage0.ScrollToVerticalOffset(ScrollImage0.VerticalOffset - hOff);
                if (lastDragPoint.Y > e.GetPosition(ScrollImage0).Y)
                    ScrollImage0.ScrollToVerticalOffset(ScrollImage0.VerticalOffset + hOff);

                if (lastDragPoint.X < e.GetPosition(ScrollImage0).X)
                    ScrollImage0.ScrollToHorizontalOffset(ScrollImage0.HorizontalOffset - hOff);
                if (lastDragPoint.X > e.GetPosition(ScrollImage0).X)
                    ScrollImage0.ScrollToHorizontalOffset(ScrollImage0.HorizontalOffset + hOff);
            }
            else if (ScrollImage1.IsMouseCaptured)
            {
                if (lastDragPoint.Y < e.GetPosition(ScrollImage1).Y)
                    ScrollImage1.ScrollToVerticalOffset(ScrollImage1.VerticalOffset - hOff);
                if (lastDragPoint.Y > e.GetPosition(ScrollImage1).Y)
                    ScrollImage1.ScrollToVerticalOffset(ScrollImage1.VerticalOffset + hOff);

                if (lastDragPoint.X < e.GetPosition(ScrollImage1).X)
                    ScrollImage1.ScrollToHorizontalOffset(ScrollImage1.HorizontalOffset - hOff);
                if (lastDragPoint.X > e.GetPosition(ScrollImage1).X)
                    ScrollImage1.ScrollToHorizontalOffset(ScrollImage1.HorizontalOffset + hOff);
            }

        }

        private void ScrollViewer_PreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            ScrollViewer ChoiceScrollViewer = (sender as ScrollViewer);

            switch (ChoiceScrollViewer.Name)
            {
                case "WaferMapScroll":
                    scrollMousePoint = e.GetPosition(WaferMapScroll);
                    if (scrollMousePoint.X <= WaferMapScroll.ViewportWidth && scrollMousePoint.Y <= WaferMapScroll.ViewportHeight)
                    {
                        lastDragPoint = scrollMousePoint;
                    }

                    WaferMapScroll.CaptureMouse();
                    break;

                case "ScrollImage0":
                    scrollMousePoint = e.GetPosition(ScrollImage0);
                    if (scrollMousePoint.X <= ScrollImage0.ViewportWidth && scrollMousePoint.Y <= ScrollImage0.ViewportHeight)
                    {
                        lastDragPoint = scrollMousePoint;
                        ScrollImage0.Cursor = System.Windows.Input.Cursors.SizeAll;
                    }
                    ScrollImage0.CaptureMouse();
                    break;

                case "ScrollImage1":
                    scrollMousePoint = e.GetPosition(ScrollImage1);
                    if (scrollMousePoint.X <= ScrollImage1.ViewportWidth && scrollMousePoint.Y <= ScrollImage1.ViewportHeight)
                    {
                        lastDragPoint = scrollMousePoint;
                        ScrollImage1.Cursor = System.Windows.Input.Cursors.SizeAll;
                    }
                    ScrollImage1.CaptureMouse();
                    break;
            }
        }

        private void ListView_BotInfo_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            ListView ChoiceListView = (sender as ListView);

            int selectedIndex = -1;

            // 리스트뷰에서 한개의 아이템을 선택하였다면,
            if ((ChoiceListView.Name == "listView_FrontBotInfo") &&(listView_FrontBotInfo.SelectedItems.Count == 1))
            {
                selectedIndex = listView_FrontBotInfo.SelectedIndex;

                image_Front_bottom.BeginInit();
                image_Front_bottom.Source = OpenCvSharp.WpfExtensions.WriteableBitmapConverter.ToWriteableBitmap(FrontBotImg[selectedIndex]);
                image_Front_bottom.EndInit();
            }
            else if ((ChoiceListView.Name == "listView_RearBotInfo") &&(listView_RearBotInfo.SelectedItems.Count == 1))
            {
                selectedIndex = listView_RearBotInfo.SelectedIndex;

                image_Rear_bottom.BeginInit();
                image_Rear_bottom.Source = OpenCvSharp.WpfExtensions.WriteableBitmapConverter.ToWriteableBitmap(RearBotImg[selectedIndex]);
                image_Rear_bottom.EndInit();
            }
        }

        public new void MouseWheel(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            ImageBoxEx ImageBox = (sender as ImageBoxEx);       // 01.27 어떤 ImageBoxEx 객체에서 발생하였는지 확인하기 위한 객체.

            System.Windows.Point wPT = new System.Windows.Point();
            wPT.X = e.X;
            wPT.Y = e.Y;

            System.Drawing.Point dPT = new System.Drawing.Point((int)wPT.X, (int)wPT.Y);
            // Wafer or SelectedDie 객체 중 선택하여 이벤트 적용.
            if (ImageBox.Name == "Wafer")
                IGWaferImage.ZoomWithMouseWheel(e.Delta, dPT);
            else if (ImageBox.Name == "SelectedDie")
                IGSelectedDieImage.ZoomWithMouseWheel(e.Delta, dPT);
        }

        public new void MouseMove(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            // 08.05 Modify Start
            SequenceContext cont = ContextList["WAFER"];
            WaferSequenceContext waferContext = ContextList["WAFER"] as WaferSequenceContext;

            ImageBoxEx IGbox = (sender as ImageBoxEx);

            System.Drawing.Point wPT = new System.Drawing.Point();  // wPT is Client Postion. System.Windows.Point -> System.Drawing.Point
            wPT.X = e.X;
            wPT.Y = e.Y;

            System.Drawing.Point iPT = new System.Drawing.Point();  // iPT is Image Position. System.Windows.Point -> System.Drawing.Point
            iPT = IGbox.PointToImage(wPT, true);

            string str = "";
            try
            {
                var pt = PImage.At<Vec3b>(iPT.Y, iPT.X);
            

                str = "Pixel Point:" + "(" + iPT.X.ToString() + "," + iPT.Y.ToString() + ")" + "\r\n"
                    + "(R,G,B:" + pt.Item2.ToString() + "," + pt.Item1.ToString() + "," + pt.Item0.ToString() + ")";
            }
            catch (Exception ex)
            {
                Logging.PrintErrLog((int)ELogType.Error, $"Right-WaferView Exception:{ex.Message}");
                str = $"Right-WaferView Exception:{ex.Message}";
            }

            IGbox.TextAlign = System.Drawing.ContentAlignment.TopLeft;  // 11.01
            IGbox.TextDisplayMode = ImageBoxGridDisplayMode.Image;
            IGbox.TextBackColor = System.Drawing.Color.Beige;

            IGbox.Text = str;
            IGbox.Refresh();
            // 08.05 Modify End
        }
    }
}
