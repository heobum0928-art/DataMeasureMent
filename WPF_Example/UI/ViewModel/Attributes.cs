using ReringProject.Device;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReringProject.UI {

    [AttributeUsage(AttributeTargets.Property)]
    public class PropertyItemAttribute : Attribute {
        public ECameraPropertyType PropertyType { get; private set; }
        public PropertyItemAttribute(ECameraPropertyType type) {
            PropertyType = type;
        }
    }

    [AttributeUsage(AttributeTargets.Property)]
    public class RectangleAttribute : Attribute {
    }

    [AttributeUsage(AttributeTargets.Property)]
    public class LineAttribute : Attribute {
    }

    [AttributeUsage(AttributeTargets.Property)]
    public class CircleAttribute : Attribute {

    }

    [AttributeUsage(AttributeTargets.Property)]
    public class ModelFinderAttribute : Attribute {

    }

    [AttributeUsage(AttributeTargets.Property)]
    public class CalibrationAttribute : Attribute
    {

    }
}
