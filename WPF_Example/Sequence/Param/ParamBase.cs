using PropertyTools.DataAnnotations;
using ReringProject.Define;
using ReringProject.Setting;
using ReringProject.UI;
using ReringProject.Utility;
using System.ComponentModel;
using System.Reflection;
using System.Windows;
using System.Collections.Generic;
using System.Linq;
using System;
using System.Diagnostics;

namespace ReringProject.Sequence {
    public enum EExternalFileType {
        Model,
        Calibration,
        Image
    }

    public struct DrawableSource {
        public object Owner;
        public PropertyInfo Property;

        public DrawableSource(object owner, PropertyInfo prop) {
            Owner = owner;
            Property = prop;
        }
    }

    /*
     * Parameter 는 코드상에서 정의되므로 실체가 명확하다. 따라서 load, save 시, new를 사용하여 생성할 필요가 없으며, id를 load할 필요가 없음(이미 id가 할당됨)
    */
    public class ParamBase : INotifyPropertyChanged {

        [PropertyTools.DataAnnotations.Browsable(false)]
        [PropertyTools.DataAnnotations.ReadOnly(true)]
        public object Owner { get; set; } //이클래스를 소유한 action base 또는 Sequence Base 가 된다.

        [PropertyTools.DataAnnotations.Browsable(false)]
        public string OwnerName
        {
            get{
                if (Owner is ActionBase)
                    return (Owner as ActionBase).Name;
                if (Owner is SequenceBase)
                    return (Owner as SequenceBase).Name;
                return null;
            }
        }

        [PropertyTools.DataAnnotations.Browsable(false)]
        [PropertyTools.DataAnnotations.ReadOnly(true)]
        public SequenceBase Parent { get; set; }


        [PropertyTools.DataAnnotations.Browsable(false)]
        protected List<DrawableSource> RectList = new List<DrawableSource>();

        [PropertyTools.DataAnnotations.Browsable(false)]
        protected List<DrawableSource> LineList = new List<DrawableSource>();

        [PropertyTools.DataAnnotations.Browsable(false)]
        protected List<DrawableSource> CircleList = new List<DrawableSource>();

        [PropertyTools.DataAnnotations.Browsable(false)]
        protected List<ModelFinderViewModel> ModelFinderList = new List<ModelFinderViewModel>();

        [PropertyTools.DataAnnotations.Browsable(false)]
        protected List<CalibrationViewModel> CalibrationList = new List<CalibrationViewModel>();

        public ParamBase(object owner) {
            Owner = owner;

            PropertyInfo[] props = GetType().GetProperties();
            foreach (var prop in props) {
                var attrs = prop.GetCustomAttributes(true);
                foreach (var attr in attrs) {
                    //drawing
                    if (attr is RectangleAttribute) {
                        //ROIAttribute roi = attr as ROIAttribute;
                        //Rect roiData = (Rect)prop.GetValue(this);
                        //add roi list
                        RectList.Add(new DrawableSource(this, prop));
                    }
                    else if(attr is LineAttribute) {
                        //LineAttribute line = attr as LineAttribute;
                        Line lineData = (Line)prop.GetValue(this);
                        //add line list
                        LineList.Add(new DrawableSource(this, prop));
                    }
                    else if(attr is CircleAttribute) {
                        Circle circleData = (Circle)prop.GetValue(this);
                        CircleList.Add(new DrawableSource(this, prop));
                    }
                    else if(attr is ModelFinderAttribute) {
                        ModelFinderViewModel view = (ModelFinderViewModel)prop.GetValue(this);
                        ModelFinderList.Add(view);
                        //add master
                        RectList.Add(new DrawableSource(view, view.MasterRectProperty));
                    }
                    else if (attr is CalibrationAttribute)
                    {
                        CalibrationViewModel view = (CalibrationViewModel)prop.GetValue(this);
                        CalibrationList.Add(view);
                        //add master
                        RectList.Add(new DrawableSource(view, view.MasterRectProperty));
                    }
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public void RaisePropertyChanged(string name) {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        public string GetExternalFilePath(EExternalFileType fileType, string fileName) {
            string modelPath = "";
            if (Owner is ActionBase) {
                ActionBase actionOwner = Owner as ActionBase;
                string recipeName = SystemHandler.Handle.Setting.CurrentRecipeName;
                string seqName = SystemHandler.Handle.Sequences[actionOwner.ParentID].Name;
                string actName = actionOwner.Name;
                if (fileType == EExternalFileType.Model) {
                    modelPath = RecipeFiles.Handle.GetModelFilePath(recipeName, seqName, actName, fileName);
                }
                if (fileType == EExternalFileType.Calibration)
                {
                    modelPath = RecipeFiles.Handle.GetCalibrationFilePath(recipeName, seqName, actName, fileName);
                }
                else if (fileType == EExternalFileType.Image) {
                    modelPath = RecipeFiles.Handle.GetPatternImageFilePath(recipeName, seqName, actName, fileName);
                }
            }
            return modelPath;
        }
        
        //ROI
        public int GetRectCount() {
            return RectList.Count;
        }

        public bool ContainsRect(string name) {
            int count = RectList.Count(item => item.Property.Name == name);
            if (count > 0) return true;
            return false;
        }

        public bool GetRect(object owner, string name, out Rect rect) {
            for(int i = 0; i < RectList.Count; i++) {
                if((RectList[i].Owner == owner) && (RectList[i].Property.Name == name)) {
                    rect = (Rect)RectList[i].Property.GetValue(RectList[i].Owner);
                    return true;
                }
            }
            rect = new Rect();
            return false;
        }
       
        public bool GetRect(int i, out Rect rect) {
            if (i >= RectList.Count) {
                rect = new Rect();
                return false;
            }
            rect = (Rect)RectList[i].Property.GetValue(RectList[i].Owner);
            return true;
        }

        public bool SetRect(object owner, string name, Rect rect) {
            for (int i = 0; i < RectList.Count; i++) {
                if ((RectList[i].Owner == owner) && (RectList[i].Property.Name == name)) {
                    RectList[i].Property.SetValue(RectList[i].Owner, rect);
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
                    return true;
                }
            }
            return true;
        }

        public bool GetRectOwner(int i, out object owner) {
            if (i >= RectList.Count) {
                owner = null;
                return false;
            }
            owner = RectList[i].Owner;
            return true;
        }

        public bool GetRectName(int i, out string name) {
            if (i >= RectList.Count) {
                name = "";
                return false;
            }
            name = RectList[i].Property.Name;
            return true;
        }

        //Line
        public int GetLineCount() {
            return LineList.Count;
        }

        public bool ContainsLine(string name) {
            int count = LineList.Count(item => item.Property.Name == name);
            if (count > 0) return true;
            return false;
        }

        public bool GetLine(object owner, string name, out Line rect) {
            for (int i = 0; i < LineList.Count; i++) {
                if ((LineList[i].Owner == owner) && (LineList[i].Property.Name == name)) {
                    rect = (Line)LineList[i].Property.GetValue(LineList[i].Owner);
                    return true;
                }
            }
            rect = new Line();
            return false;
        }


        public bool GetLine(int i, out Line line) {
            if (i >= LineList.Count) {
                line = new Line();
                return false;
            }
            line = (Line)LineList[i].Property.GetValue(LineList[i].Owner);
            return true;
        }

        public bool SetLine(object owner, string name, Line line) {
            for (int i = 0; i < LineList.Count; i++) {
                if ((LineList[i].Owner == owner) && (LineList[i].Property.Name == name)) {
                    LineList[i].Property.SetValue(LineList[i].Owner, line);
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
                    return true;
                }
            }
            return true;
        }

        public bool GetLineOwner(int i, out object owner) {
            if (i >= LineList.Count) {
                owner = null;
                return false;
            }
            owner = LineList[i].Owner;
            return true;
        }

        public bool GetLineName(int i, out string name) {
            if (i >= LineList.Count) {
                name = "";
                return false;
            }
            name = LineList[i].Property.Name;
            return true;
        }

        //Circle
        public bool ContainsCircle(string name) {
            int count = CircleList.Count(item => item.Property.Name == name);
            if (count > 0) return true;
            return false;
        }

        public bool GetCircle(object owner, string name, out Circle circle) {
            for (int i = 0; i < CircleList.Count; i++) {
                if ((CircleList[i].Owner == owner) && (CircleList[i].Property.Name == name)) {
                    circle = (Circle)CircleList[i].Property.GetValue(CircleList[i].Owner);
                    return true;
                }
            }
            circle = new Circle();
            return false;
        }

        public bool GetCircle(int i, out Circle circle) {
            if (i >= CircleList.Count) {
                circle = new Circle();
                return false;
            }
            circle = (Circle)CircleList[i].Property.GetValue(CircleList[i].Owner);
            return true;
        }

        public int GetCircleCount() {
            return CircleList.Count;
        }

        public bool SetCircle(object owner, string name, Circle line) {
            for (int i = 0; i < CircleList.Count; i++) {
                if ((CircleList[i].Owner == owner) && (CircleList[i].Property.Name == name)) {
                    CircleList[i].Property.SetValue(CircleList[i].Owner, line);
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
                    return true;
                }
            }
            return true;
        }

        public bool GetCircleOwner(int i, out object owner) {
            if (i >= CircleList.Count) {
                owner = null;
                return false;
            }
            owner = CircleList[i].Owner;
            return true;
        }

        public bool GetCircleName(int i, out string name) {
            if (i >= CircleList.Count) {
                name = "";
                return false;
            }
            name = CircleList[i].Property.Name;
            return true;
        }

        //Model Finder
        //nothing..

        public virtual bool Save(IniFile saveFile, string group) {
            PropertyInfo[] props = this.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public);
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
                    case "PropertyItem[]":
                        PropertyItem[] propItems = (PropertyItem[])prop.GetValue(this);
                        for (int i = 0; i < propItems.Length; i++) {
                            saveFile[group][propItems[i].Name] = propItems[i].Value;
                        }
                        break;
                    case "ModelFinderViewModel":
                        ModelFinderViewModel modelView = (ModelFinderViewModel)prop.GetValue(this);
                        modelView.Save(saveFile, group, prop.Name);
                        break;
                    default:
                        break;
                }
            }
            return true;
        }

        public virtual bool Load(IniFile loadFile, string group) {
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

                            /*
                            if (sValue == null)
                                return true;
                            else
                                prop.SetValue(this, sValue);
                            */
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
                        case "PropertyItem[]":
                            PropertyItem[] propItems = (PropertyItem[])prop.GetValue(this);
                            for (int i = 0; i < propItems.Length; i++) {
                                propItems[i].Value = loadFile[group][propItems[i].Name].ToDouble();
                            }
                            break;
                        case "ModelFinderViewModel":
                            ModelFinderViewModel modelView = (ModelFinderViewModel)prop.GetValue(this);
                            modelView.Load(loadFile, group, prop.Name);
                            break;
                    }
                }
                catch(Exception e) {
                    Debug.WriteLine($"{name}:{group}:{type}");
                    Logging.PrintErrLog((int)ELogType.Error, e.Message);
                }
            }
            return true;
        }

        public virtual bool CopyTo(ParamBase param) {
            return true;
        }

        public override string ToString() {
            if(Owner != null) {
                if(Owner is ActionBase) {
                    ActionBase action = Owner as ActionBase;
                    return action.ID.ToString();
                }
            }
            return this.Owner.ToString();
        }
    }
}
