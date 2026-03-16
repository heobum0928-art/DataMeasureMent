using PropertyTools.DataAnnotations;
using ReringProject.Define;
using ReringProject.Setting;
using ReringProject.UI;
using ReringProject.Utility;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace ReringProject.Device {
    /// <summary>
    /// 이미지 옵션
    /// </summary>
    public class DisplayConfig : INotifyPropertyChanged {

        [PropertyTools.DataAnnotations.Browsable(false)]
        private string DisplayConfigIniFile = AppDomain.CurrentDomain.BaseDirectory + @"DisplayConfig.ini";

        [PropertyTools.DataAnnotations.Browsable(false)]
        public const double DrawScaleLowLimit = 0.25f;

        [PropertyTools.DataAnnotations.Browsable(false)]
        public const double DrawScaleHighLimit = 2.0f;

        [PropertyTools.DataAnnotations.Browsable(false)]
        public const ushort TriggerModeTimeOutLowLimit = 100;

        [PropertyTools.DataAnnotations.Browsable(false)]
        public const ushort GrabTimeOutLowLimit = 100;

        private double drawScale = 1.0f;   //이미지를 표시할때의 스케일 (0.1 ~ 2.0)
        
        [PropertyTools.DataAnnotations.Category("Camera|Display")]
        [Slidable(DrawScaleLowLimit, DrawScaleHighLimit, TickFrequency = 0.25)]
        [FormatString("0.00")]
        public double DrawScale {
            get {
                return drawScale;
            }
            set {
                if ((value >= DrawScaleLowLimit) && (value <= DrawScaleHighLimit)) drawScale = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("DrawScale"));
            }
        }
        
        private ushort grabTimeOut = 1000;       // 그랩 시 타임아웃

        //[Spinnable(10, 10), Width(100)]
        [PropertyTools.DataAnnotations.Browsable(false)]
        public ushort GrabTimeOut {
            get {
                return grabTimeOut;
            }
            set {
                if (value >= GrabTimeOutLowLimit) grabTimeOut = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("GrabTimeOut"));
            }
        }
        
        private ushort triggerModeTimeOut = 500;    //트리거모드 전환할 때의 타임아웃

        //[Spinnable(10, 10), Width(100)]
        [PropertyTools.DataAnnotations.Browsable(false)]
        public ushort TriggerModeTimeOut {
            get {
                return triggerModeTimeOut;
            }
            set {
                if (value >= TriggerModeTimeOutLowLimit) triggerModeTimeOut = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("TriggerModeTimeOut"));
            }
        }

        private bool drawCenterLine = false;
        public bool DrawCenterLine {
            get { return drawCenterLine; }
            set {
                drawCenterLine = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("DrawCenterLine"));
            }
        }

        private bool drawCenterRect = false;
        public bool DrawCenterRect {
            get { return drawCenterRect; }
            set {
                drawCenterRect = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("DrawCenterRect"));
            }
        }
        
        private int centerRectWidth;

        [Spinnable(10, 10), Width(100)]
        public int CenterRectWidth {
            get { return centerRectWidth; }
            set {
                centerRectWidth = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("CenterRectWidth"));
            }
        }

        private int centerRectHeight;

        [Spinnable(10, 10), Width(100)]
        public int CenterRectHeight {
            get { return centerRectHeight; }
            set {
                centerRectHeight = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("CenterRectHeight"));
            }
        }

        private bool drawCenterCircle = false;
        public bool DrawCenterCircle {
            get { return drawCenterCircle; }
            set {
                drawCenterCircle = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("DrawCenterCircle"));
            }
        }

        private int centerCircleRadius;

        [Spinnable(10, 10), Width(100)]
        public int CenterCircleRadius {
            get { return centerCircleRadius; }
            set {
                centerCircleRadius = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("CenterCircleRadius"));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;


        public DisplayConfig() {
            Load();
        }

        public void Save() {
            IniFile saveFile = new IniFile();
            //saveFile["ImageProvider"]["DrawScale"] = DrawScale;
            //saveFile["ImageProvider"]["GrabTimeOut"] = GrabTimeOut;
            //saveFile["ImageProvider"]["TriggerModeTimeOut"] = TriggerModeTimeOut;
            //saveFile.Save(DisplayConfigIniFile);
            string group = this.GetType().Name;
            PropertyInfo[] props = GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public);
            foreach (var prop in props) {
                string name = prop.Name;
                string type = prop.PropertyType.Name;

                switch (type) {
                    case "Int32":
                        saveFile[group][name] = (int)prop.GetValue(this);
                        break;
                    case "Double":
                        saveFile[group][name] = (double)prop.GetValue(this);
                        break;
                    case "String":
                        saveFile[group][name] = (string)prop.GetValue(this);
                        break;
                    case "Boolean":
                        saveFile[group][name] = (bool)prop.GetValue(this);
                        break;
                    case "Rect":
                        saveFile[group][name] = (Rect)prop.GetValue(this);
                        break;
                    case "Line":
                        saveFile[group][name] = (Line)prop.GetValue(this);
                        break;
                    case "Circle":
                        saveFile[group][name] = (Circle)prop.GetValue(this);
                        break;
                    case "ModelFinderViewModel":
                        ModelFinderViewModel modelView = (ModelFinderViewModel)prop.GetValue(this);
                        modelView.Save(saveFile, group, prop.Name);
                        break;
                    default:
                        break;
                }
            }
            saveFile.Save(DisplayConfigIniFile);
        }

        public void Load() {
            IniFile loadFile = new IniFile();
            if (File.Exists(DisplayConfigIniFile)) loadFile.Load(DisplayConfigIniFile);
            string group = this.GetType().Name;

            PropertyInfo[] props = GetType().GetProperties();
            foreach (var prop in props) {
                string name = prop.Name;
                string type = prop.PropertyType.Name;

                try {
                    switch (type) {
                        case "Int32":
                            int iValue = loadFile[group][name].ToInt();
                            prop.SetValue(this, iValue);
                            break;
                        case "Double":
                            double dValue = loadFile[group][name].ToDouble();
                            prop.SetValue(this, dValue);
                            break;
                        case "String":
                            string sValue = loadFile[group][name].ToString();
                            prop.SetValue(this, sValue);
                            break;
                        case "Boolean":
                            bool bValue = loadFile[group][name].ToBool();
                            prop.SetValue(this, bValue);
                            break;
                        case "Rect":
                            System.Windows.Rect rectVal = loadFile[group][name].ToRect();
                            prop.SetValue(this, rectVal);
                            break;
                        case "Line":
                            Line lineVal = loadFile[group][name].ToLine();
                            prop.SetValue(this, lineVal);
                            break;
                        case "Circle":
                            Circle circleVal = loadFile[group][name].ToCircle();
                            prop.SetValue(this, circleVal);
                            break;
                        case "ModelFinderViewModel":
                            ModelFinderViewModel modelView = (ModelFinderViewModel)prop.GetValue(this);
                            modelView.Load(loadFile, group, prop.Name);
                            break;
                    }
                }
                catch (Exception e) {
                    Logging.PrintErrLog((int)ELogType.Error, e.Message);
                }
            }
        }
    }
}
