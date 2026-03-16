using PropertyTools.DataAnnotations;
using ReringProject.Define;
using ReringProject.Sequence;
using ReringProject.UI;
using ReringProject.Utility;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;

namespace ReringProject.Device {
    /// <summary>
    /// 공통 카메라 프로퍼티 타입
    /// </summary>
    public enum ECameraPropertyType : int {
        Exposure,           //노출

        //Focus,              //포커스
        //Brightness,         //밝기
        //Whitebalance,       //화이트발란스
        //Contrast,           //대비
        //Saturation,         //채도

        Gamma,          //감마
        Gain,               //게인
        
        //BalanceRatioRed,        //Red 발란스
        //BalanceRatioGreen,      //Green 발란스
        //BalanceRatioBlue,        //Blue 발란스
       
    }

    

    public class PropertyValueChangedEvent : EventArgs {
        public ECameraPropertyType PropertyType { get; private set; }
        public object Value { get; private set; }

        public PropertyValueChangedEvent(ECameraPropertyType propType, object value) {
            PropertyType = propType;
            Value = value;
        }
    }

    public delegate void OnPropertyValueChanged(PropertyValueChangedEvent args);

    public class PropertyItem {
        public decimal Value;
        public decimal Min;
        public decimal Max;
        public decimal Increment;

        public PropertyItem(decimal value, decimal min = 0, decimal max= 0, decimal increment=0) {
            Value = value;
            Min = min;
            Max = max;
            Increment = increment;
        }
    }

    public class VirtualCameraProperty : INotifyPropertyChanged {

        [PropertyTools.DataAnnotations.ReadOnly(true)]
        public int Width {
            get { return _Width; }
            set {
                _Width = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Width"));
            }
        }
        private int _Width;

        [PropertyTools.DataAnnotations.ReadOnly(true)]
        public int Height {
            get {
                return _Height;
            }
            set {
                _Height = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Height"));
            }
        }
        private int _Height;

        [PropertyTools.DataAnnotations.Browsable(false)]
        [PropertyTools.DataAnnotations.ReadOnly(true)]
        protected Dictionary<ECameraPropertyType, PropertyItem> Values = new Dictionary<ECameraPropertyType, PropertyItem>();
        
        public event PropertyChangedEventHandler PropertyChanged;

        public void RaisePropertyChanged(string name) {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        public VirtualCameraProperty() {
            //collect my parameter
            PropertyInfo[] _props = this.GetType().GetProperties();

            foreach (PropertyInfo prop in _props) {
                object[] attrs = prop.GetCustomAttributes(true);
                ECameraPropertyType propType = ECameraPropertyType.Exposure;
                foreach (object attr in attrs) {
                    if (attr is PropertyItemAttribute) {
                        PropertyItemAttribute propAttr = attr as PropertyItemAttribute;
                        //attribute로부터 지정된 enum type을 뽑아낸다.
                        propType = propAttr.PropertyType;
                        if (Values.ContainsKey(propType)) {
                            Logging.PrintLog((int)Setting.ELogType.Error, "{0} type has already exist.", propType);
                            continue;
                        }
                        //add to values
                        decimal value = 0;
                        switch (prop.GetValue(this).GetType().ToString()) {
                            case "System.Int32":
                                int intVal = (int)prop.GetValue(this);
                                value = intVal;
                                Values.Add(propType, new PropertyItem(value));
                                break;
                            case "System.Double":
                                double doubleVal = (double)prop.GetValue(this);
                                value = (decimal)doubleVal;
                                Values.Add(propType, new PropertyItem(value));
                                break;
                            case "System.Boolean":
                                bool boolVal = (bool)prop.GetValue(this);
                                value = Convert.ToDecimal(boolVal);
                                Values.Add(propType, new PropertyItem(value));
                                break;
                        }
                        break;
                    }
                }
                
            }
        }

        public virtual bool ApplyFromParam(ICameraParam cameraParam) {
            for (int i = 0; i < cameraParam.PropertyArray.Length; i++) {
                ECameraPropertyType propType = cameraParam.PropertyArray[i].GetPropertyType();
                WriteProperty(propType, (decimal)cameraParam.PropertyArray[i].Value);
            }
            return true;
        }
        

        public virtual void Update() {
            //read all
            for (int i = 0; i < Values.Count; i++) {
                ECameraPropertyType key = Values.ElementAt(i).Key;
                ReadProperty(key, out decimal value);
            }
        }

        [PropertyTools.DataAnnotations.Browsable(false)]
        [PropertyTools.DataAnnotations.ReadOnly(true)]
        public int Count { get => Values.Count; }

        public decimal this[ECameraPropertyType type] {
            get {
                ReadProperty(type, out decimal value);
                return value;
                //if (Values.ContainsKey(type) == false) return 0;
                //return Values[type].Value;
            }
            set {
                WriteProperty(type, value);
                //if (Values.ContainsKey(type) == false) return;
                //Values[type].Value = value;
            }
        }

        public decimal this[string name] {
            get {
                var type = (ECameraPropertyType)Enum.Parse(typeof(ECameraPropertyType), name);
                return this[type];
            }

            set {
                var type = (ECameraPropertyType)Enum.Parse(typeof(ECameraPropertyType), name);
                this[type] = value;
            }
        }

        public ECameraPropertyType GetPropType(int index) {
            if (index >= Values.Count) return ECameraPropertyType.Exposure;
            return Values.ElementAt(index).Key;
        }

        public decimal this[int index] {
            get {
                if (index >= Values.Count) index = Values.Count - 1;
                else if (index < 0) index = 0;
                return this[(ECameraPropertyType)index];
                //return Values.ElementAt(index).Value.Value;
            }

            set {
                if (index >= Values.Count) index = Values.Count - 1;
                else if (index < 0) index = 0;
                ECameraPropertyType key = Values.ElementAt(index).Key;
                this[key] = value;
                //Values[key].Value = value;
            }
        }

        protected virtual bool ReadProperty(ECameraPropertyType type, out decimal value) {
            value = Values[type].Value;
            return true;
        }

        protected virtual bool WriteProperty(ECameraPropertyType type, decimal value) {
            if (Values.ContainsKey(type) == false) return false;
            Values[type].Value = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(type.ToString()));
            return true;
        }

        public virtual void Load(IniFile loadFile, string group) {
            throw new NotImplementedException();
        }

        public virtual void Save(IniFile saveFile, string group) {
            throw new NotImplementedException();
        }

    }
}
