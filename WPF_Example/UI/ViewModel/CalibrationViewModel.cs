
using PropertyTools.DataAnnotations;
using ReringProject.Device;
using ReringProject.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace ReringProject.UI {

    public enum ECalibrationType {
        Calibration,
    }

    

    public class CalibrationViewModel : PropertyTools.Observable, IFormattable
    {
        [ReadOnly(true)]
        public ECalibrationType CalType { get; private set; }

        public string Name { get; private set; }

        [InputFilePath(DeviceHandler.EXTENSION_MODEL, DeviceHandler.FILTER_MODEL)]
        [AutoUpdateText]
        public string ModelFile
        {
            get
            {
                return modelFile;
            }
            set
            {
                this.SetValue(ref this.modelFile, value);
            }
        }
        private string modelFile;


        [Rectangle, Converter(typeof(RectConverter))]
        public Rect Master
        {
            get
            {
                return masterRect;
            }
            set
            {
                this.SetValue(ref this.masterRect, value);
            }
        }
        private Rect masterRect;

        public CalibrationViewModel(string name, ECalibrationType calType)
        {
            Name = name;
            CalType = calType;
        }

        public PropertyInfo MasterRectProperty
        {
            get
            {
                return this.GetType().GetProperty(nameof(Master));
            }
        }

        public string ToString(string format, IFormatProvider formatProvider)
        {
            return string.Format(format, formatProvider);
        }

        public override string ToString()
        {
            return Name;
        }

    }
}
