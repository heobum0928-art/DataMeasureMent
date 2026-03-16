using ReringProject.Define;
using ReringProject.Device;
using ReringProject.Sequence;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace ReringProject.UI {
    public struct Circle : IEquatable<Circle>, IFormattable {

        public double CenterX { get; set; }
        public double CenterY { get; set; }

        public double Radius { get; set; }

        public Circle(double x, double y, double radius) {
            CenterX = x;
            CenterY = y;
            Radius = radius;
        }

        public bool IsInCircle(double x, double y) {
            double distance = Line.GetDistance(x, y, CenterX, CenterY);
            if (distance <= Radius) return true;
            return false;
        }

        public bool Equals(Circle other) {
            if((CenterX == other.CenterX) && (CenterY == other.CenterY) && (Radius == other.Radius)) {
                return true;
            }
            return false;
        }

        public string ToString(string format, IFormatProvider formatProvider) {
            if (format == null) return ToString();
            return string.Format(formatProvider, format, CenterX, CenterY, Radius);
        }

        public override string ToString() {
            return string.Format("{0:0.00},{1:0.00},{2:0.00}", CenterX, CenterY, Radius);
        }

    }

    [ValueConversion(typeof(Circle), typeof(string))]
    public class CircleConverter : IValueConverter {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            if(value is Circle) {
                var c = (Circle)value;
                if(targetType == typeof(string)) {
                    return c.ToString();
                }
            }
            return DependencyProperty.UnsetValue;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            if(value is string) {
                var c = (string)value;
                if(targetType == typeof(Circle)) {
                    try {
                        int i = c.IndexOf(",", StringComparison.InvariantCultureIgnoreCase);
                        string[] ptList = c.Split(',');
                        if(ptList != null && ptList.Length >= 3) {
                            var sx = ptList[0];
                            var sy = ptList[1];
                            var sradius = ptList[2];

                            var x = double.Parse(sx, culture);
                            var y = double.Parse(sy, culture);
                            var radius = double.Parse(sradius, culture);

                            return new Circle(x, y, radius);
                        }
                    }
                    catch {
                        return DependencyProperty.UnsetValue;
                    }
                }
            }
            return DependencyProperty.UnsetValue;
        }
    }

    public class DrawableCircle : IDrawableItem, IDisposable {
        public const int PickerCount = 4;
        public const int PickerSize = 40;
        public const int DefaultFontSize = 50;

        public object Owner { get; private set; }
        public string Name { get; private set; }

        public Circle OriginalCircle;
        
        public Circle GetCircle() { return OriginalCircle; }

        private Picker[] Pickers;

        private ParamBase Param;

        private Pen DrawPen;
        private Pen PickerDrawPen;
        private Typeface TextDrawFont;
        private Brush TextDrawBrush;
        private int FontSize;

        public EDrawType DrawType { get => EDrawType.Circle; }

        public DrawableCircle(ParamBase param, object owner, string circlePropertyName) {
            Param = param;
            Owner = owner;
            Name = circlePropertyName;

            param.GetCircle(owner, circlePropertyName, out Circle circle);

            if (circle.Radius < DeviceHandler.MIN_CIRCLE_RADIUS) circle.Radius = DeviceHandler.MIN_CIRCLE_RADIUS;
            else if (circle.Radius > DeviceHandler.MAX_CIRCLE_RADIUS) circle.Radius = DeviceHandler.MAX_CIRCLE_RADIUS;

            if ((circle.CenterX - circle.Radius) < 0) circle.CenterX = circle.Radius;
            if ((circle.CenterY - circle.Radius) < 0) circle.CenterY = circle.Radius;

            if ((circle.CenterX + circle.Radius) > DeviceHandler.MAX_WIDTH) circle.CenterX = DeviceHandler.MAX_WIDTH - circle.Radius;
            if ((circle.CenterY + circle.Radius) > DeviceHandler.MAX_HEIGHT) circle.CenterY = DeviceHandler.MAX_HEIGHT - circle.Radius;

            OriginalCircle = circle;

            Pickers = new Picker[PickerCount];
            Pickers[0] = new Picker(EPickerPosition.Left);
            Pickers[1] = new Picker(EPickerPosition.Right);
            Pickers[2] = new Picker(EPickerPosition.Top);
            Pickers[3] = new Picker(EPickerPosition.Bottom);

            //set colors
            SetDrawColor(Brushes.Cyan, 4);
            SetPickerDrawColor(Brushes.Cyan, 4);
            SetTextFont("맑은 고딕", DefaultFontSize, Brushes.Cyan);
            
            UpdatePicker();
        }

        public void SetDrawColor(Brush color, float width = 1) {
            DrawPen = new Pen(color, width);
        }

        public void SetPickerDrawColor(Brush color, float width = 1) {
            PickerDrawPen = new Pen(color, width);
        }

        public void SetTextFont(string fontName, int size, Brush brush) {
            TextDrawFont = new Typeface(fontName);
            FontSize = size;
            TextDrawBrush = brush;
        }

        public void Render(DrawingContext drawingContext) {
            drawingContext.DrawEllipse(Brushes.Transparent, DrawPen, new Point(OriginalCircle.CenterX, OriginalCircle.CenterY), OriginalCircle.Radius, OriginalCircle.Radius);

            //drawText
            string name = Name;
            if (!(Owner is ParamBase))
            {
                name = Owner.ToString();
            }
            FormattedText formattedText = new FormattedText(name, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, TextDrawFont, FontSize, TextDrawBrush, 1.0);
            drawingContext.DrawText(formattedText, new Point(CenterX - (Math.Cos(120) * Radius), CenterY - (Math.Sin(120) * Radius)));
            
        }

        public void RenderPicker(DrawingContext drawingContext) {
            foreach(Picker picker in Pickers) {
                drawingContext.DrawRectangle(PickerDrawPen.Brush, PickerDrawPen, picker.Rect);
            }
        }

        public void CheckAvailable(EPickerPosition moveType) {
       
            /*
            if (OriginalCircle.Radius < DeviceHandler.MIN_CIRCLE_RADIUS) OriginalCircle.Radius = DeviceHandler.MIN_CIRCLE_RADIUS;
            else if (OriginalCircle.Radius > DeviceHandler.MAX_CIRCLE_RADIUS) OriginalCircle.Radius = DeviceHandler.MAX_CIRCLE_RADIUS;

            if ((OriginalCircle.CenterX - OriginalCircle.Radius) < 0) OriginalCircle.CenterX = OriginalCircle.Radius;
            if ((OriginalCircle.CenterY - OriginalCircle.Radius) < 0) OriginalCircle.CenterY = OriginalCircle.Radius;

            if ((OriginalCircle.CenterX + OriginalCircle.Radius) > DeviceHandler.MAX_WIDTH) OriginalCircle.CenterX = DeviceHandler.MAX_WIDTH - OriginalCircle.Radius;
            if ((OriginalCircle.CenterY + OriginalCircle.Radius) > DeviceHandler.MAX_HEIGHT) OriginalCircle.CenterY = DeviceHandler.MAX_HEIGHT - OriginalCircle.Radius;
            */

            UpdatePicker();

            Param.SetCircle(Owner, Name, OriginalCircle);
        }

        public void Dispose() {

        }

        public void ExecMove(int moveX, int moveY) {
            this.OriginalCircle.CenterX += moveX;
            this.OriginalCircle.CenterY += moveY;

            UpdatePicker();
        }

        public void ExecResize(int resizeX, int resizeY, int resizeWidth, int resizeHeight) {
            if (OriginalCircle.Radius < 0) return;


            this.OriginalCircle.Radius += resizeWidth;
            this.OriginalCircle.Radius += resizeHeight;
            
            UpdatePicker();
        }

        public EPickerPosition GetMouseIsEnter(int x, int y) {
            //is in picker
            foreach (Picker picker in Pickers) {
                if ((x > picker.Rect.Left) && (y > picker.Rect.Top) && (x < picker.Rect.Left + picker.Rect.Width) && (y < picker.Rect.Top + picker.Rect.Height)) {
                    return picker.Position;
                }
            }

            //is in body
            if(OriginalCircle.IsInCircle(x, y)) {
                return EPickerPosition.Body;
            }
            return EPickerPosition.None;
        }
   

        public int CenterX {
            get { return (int)OriginalCircle.CenterX; }
            set { OriginalCircle.CenterX = value; }
        }

        public int CenterY {
            get { return (int)OriginalCircle.CenterY; }
            set { OriginalCircle.CenterY = value; }
        }

        public int Radius {
            get { return (int)OriginalCircle.Radius; }
            set { OriginalCircle.Radius = value; }
        }

        public void UpdatePicker() {
            //left 
            Pickers[0].Rect.X = (OriginalCircle.CenterX - Radius) - (PickerSize/2);
            Pickers[0].Rect.Y = OriginalCircle.CenterY - (PickerSize/2);

            //right
            Pickers[1].Rect.X = (OriginalCircle.CenterX + Radius) - (PickerSize / 2);
            Pickers[1].Rect.Y = OriginalCircle.CenterY - (PickerSize / 2);

            //top
            Pickers[2].Rect.X = OriginalCircle.CenterX - (PickerSize / 2);
            Pickers[2].Rect.Y = (OriginalCircle.CenterY - Radius) - (PickerSize/2);

            //bottom
            Pickers[3].Rect.X = OriginalCircle.CenterX - (PickerSize /2);
            Pickers[3].Rect.Y = (OriginalCircle.CenterY + Radius) - (PickerSize / 2);
        }
    }
}
