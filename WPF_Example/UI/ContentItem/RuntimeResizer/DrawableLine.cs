using ReringProject.Define;
using ReringProject.Device;
using ReringProject.Sequence;
using ReringProject.Utility;
using System;
using System.Globalization;
using System.Reflection;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace ReringProject.UI {

    public delegate void LineEvent(double x1, double y1, double x2, double y2);

    public struct Line : IEquatable<Line>, IFormattable {

        public event LineEvent lineEvent;

        public const double OFFSET_BODY = 5.0f;

        public double X1 { get; set; }
        public double Y1 { get; set; }

        public double X2 { get; set; }
        public double Y2 { get; set; }

        public double Distance {
            get {
                return GetDistance(X1, Y1, X2, Y2);
            }
        }

        public static double GetDistance(double x1, double y1, double x2, double y2) {
            return Math.Sqrt(Math.Pow(x1 - x2, 2) + Math.Pow(y1 - y2, 2));
        }

        public bool IsInLine(double x, double y, double offset) {
            double totalDist = GetDistance(x, y, X1, Y1) + GetDistance(x, y, X2, Y2);
            if(Math.Abs(Distance - totalDist) < offset) {

                Console.WriteLine(string.Format($"x1 : {X1}, x2 : {Y1}, y1 : {Y1}, y2 : {Y2}"));
                if(lineEvent != null)
                {
                    lineEvent(X1, Y1, X2, Y2);
                }
                return true;
            }
            return false;
        }
        
        public Line(double x1, double y1, double x2, double y2) {
            X1 = x1;
            Y1 = y1;
            X2 = x2;
            Y2 = y2;

            lineEvent = null;
        }

        public Line(Point pt1, Point pt2) {
            X1 = pt1.X;
            Y1 = pt1.Y;

            X2 = pt2.X;
            Y2 = pt2.Y;

            lineEvent = null;
        }

        public bool Equals(Line other) {
            if(X1 == other.X1 && Y1 == other.Y1 && X2 == other.X2 && Y2 == other.Y2) {
                return true;
            }
            return false;
        }

        public string ToString(string format, IFormatProvider formatProvider) {
            if (format == null) return ToString();
            return string.Format(formatProvider, format, X1, Y1, X2, Y2);
        }

        public override string ToString() {
            return string.Format("{0:0.00},{1:0.00},{2:0.00},{3:0.00}", X1, Y1, X2, Y2);
        }

        public void RegisterEvent(LineEvent lineEvent)
        {
            this.lineEvent += lineEvent;
        }

        public void RaisePosition(double x1, double y1, double x2, double y2)
        {
            if(lineEvent != null)
            {
                lineEvent(x1, y1, x2, y2);
            }
        }
    }

    [ValueConversion(typeof(Line), typeof(string))]
    public class LineConverter : IValueConverter {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            if(value is Line) {
                var c = (Line)value;

                c.RaisePosition(c.X1, c.Y1, c.X2, c.Y2);



                if (targetType == typeof(string)) {
                    return c.ToString();
                }
            }
            return DependencyProperty.UnsetValue;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            if(value is string) {
                var c = (string)value;
                if(targetType == typeof(Line)) {
                    try {
                        //c = c.Trim("()".ToCharArray());
                        int i = c.IndexOf(",", StringComparison.InvariantCultureIgnoreCase);
                        string[] ptList = c.Split(',');
                        if (ptList != null && ptList.Length >= 4) {
                            var sx1 = ptList[0];
                            var sy1 = ptList[1];
                            var sx2 = ptList[2];
                            var sy2 = ptList[3];

                            var x1 = double.Parse(sx1, culture);
                            var y1 = double.Parse(sy1, culture);
                            var x2 = double.Parse(sx2, culture);
                            var y2 = double.Parse(sy2, culture);

                            return new Line(x1, y1, x2, y2);
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

    //abstract class
    public class DrawableLine : IDrawableItem, IDisposable {
        //const
        public const int PickerCount = 2;
        public const int PickerSize = 40;
        public const int DefaultFontSize = 50;

        //data
        public object Owner { get; private set; }
        public string Name { get; private set; }

        public Line OriginalLine;
        
        public Line GetLine() { return OriginalLine; }

        private Picker[] Pickers;

        //draw vars
        public static Pen OkPen = new Pen(Brushes.Lime, 4);
        public static Pen NgPen = new Pen(Brushes.Red, 4);
        public static Pen GuidePen = new Pen(Brushes.Fuchsia, 4);

        private Pen DrawPen;
        private Pen PickerDrawPen;

        private Typeface TextDrawFont;     
        private Brush TextDrawBrush;
        private int FontSize;

        private ParamBase Param;

        public DrawableLine(ParamBase param, object owner, string linePropertyName) {
            Param = param;
            Owner = owner;
            Name = linePropertyName;

            param.GetLine(owner, linePropertyName, out Line line);
        
            if (line.X1 < 0) line.X1 = 0;
            if (line.Y1 < 0) line.Y1 = 0;
            if (line.X1 > DeviceHandler.MAX_WIDTH) line.X1 = DeviceHandler.MAX_WIDTH;
            if (line.Y1 > DeviceHandler.MAX_HEIGHT) line.Y1 = DeviceHandler.MAX_HEIGHT;
            
            if (line.X2 < 0) line.X2 = 0;
            if (line.Y2 < 0) line.Y2 = 0;
            if (line.X2 > DeviceHandler.MAX_WIDTH) line.X2 = DeviceHandler.MAX_WIDTH;
            if (line.Y2 > DeviceHandler.MAX_HEIGHT) line.Y2 = DeviceHandler.MAX_HEIGHT;

            if (line.Distance <= 0) {
                line.X2 = 50;
                line.Y2 = 50;
            }
            
            OriginalLine = line;
            
            Pickers = new Picker[PickerCount];
            Pickers[0] = new Picker(EPickerPosition.Point1);
            Pickers[1] = new Picker(EPickerPosition.Point2);

            //set colors
            SetDrawColor(Brushes.Cyan, 1);
            SetPickerDrawColor(Brushes.Cyan, 1);
            SetTextFont("맑은 고딕", DefaultFontSize, Brushes.Cyan);
            
            UpdatePicker();
        }

        public EDrawType DrawType { get { return EDrawType.Line; } }

        public void SetDrawColor(Brush color, float width=1) {
            DrawPen = new Pen(color, width);
        }

        public void SetPickerDrawColor(Brush color, float width=1) {
            PickerDrawPen = new Pen(color, width);
        }

        public void SetTextFont(string fontName, int size, Brush brush) {
            TextDrawFont = new Typeface(fontName);
            FontSize = size;
            TextDrawBrush = brush;
        }

        public void Render(DrawingContext drawingContext) {
            //draw rect 
            drawingContext.DrawLine(DrawPen, new Point(OriginalLine.X1, OriginalLine.Y1), new Point(OriginalLine.X2, OriginalLine.Y2));

            //draw text
            string name = Name;
            if (!(Owner is ParamBase))
            {
                name = Owner.ToString();
            }
            FormattedText formattedText = new FormattedText(name, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, TextDrawFont, FontSize, TextDrawBrush, 1.0);
            drawingContext.DrawText(formattedText, new Point(X1, Y1));
        }

        public void RenderPicker(DrawingContext drawingContext) {
            foreach (Picker picker in Pickers) {
                drawingContext.DrawRectangle(PickerDrawPen.Brush, PickerDrawPen, picker.Rect);
            }
        }

        public EPickerPosition GetMouseIsEnter(int x, int y) {
            
            //is in picker
            foreach (Picker picker in Pickers) {
                if ((x > picker.Rect.Left) && (y > picker.Rect.Top) && (x < picker.Rect.Left + picker.Rect.Width) && (y < picker.Rect.Top + picker.Rect.Height)) {
                    return picker.Position;
                }
            }
            //is in body
            if (OriginalLine.IsInLine(x, y, Line.OFFSET_BODY)) {
                return EPickerPosition.Body;
            }
            return EPickerPosition.None;
        }

        public int X1 {
            get { return (int)OriginalLine.X1; }
            set { OriginalLine.X1 = value; }
        }

        public int Y1 {
            get { return (int)OriginalLine.Y1; }
            set { OriginalLine.Y1 = value; }
        }

        public int X2 {
            get { return (int)OriginalLine.X2; }
            set { OriginalLine.X2 = value; }
        }

        public int Y2 {
            get { return (int)OriginalLine.Y2; }
            set { OriginalLine.Y2 = value; }
        }

        public void UpdatePicker() {
            //left
            Pickers[0].Rect.X = OriginalLine.X1 - (PickerSize /2);
            Pickers[0].Rect.Y = OriginalLine.Y1 - (PickerSize /2);

            //right
            Pickers[1].Rect.X = OriginalLine.X2 - (PickerSize / 2);
            Pickers[1].Rect.Y = OriginalLine.Y2 - (PickerSize / 2);
            
        }

        public virtual void ExecMove(int moveX, int moveY) {
            this.OriginalLine.X1 += moveX;
            this.OriginalLine.Y1 += moveY;

            this.OriginalLine.X2 += moveX;
            this.OriginalLine.Y2 += moveY;

            UpdatePicker();
        }

        public virtual void ExecResize(int resizeX1, int resizeY1, int resizeX2, int resizeY2) {
            if ((OriginalLine.X1 + resizeX1) > 0) this.OriginalLine.X1 += resizeX1;
            if ((OriginalLine.Y1 + resizeY1) > 0) this.OriginalLine.Y1 += resizeY1;

            if ((OriginalLine.X2 + resizeX2) > 0) this.OriginalLine.X2 += resizeX2;
            if ((OriginalLine.Y2 + resizeY2) > 0) this.OriginalLine.Y2 += resizeY2;

            UpdatePicker();
        }

        public virtual void CheckAvailable(EPickerPosition moveType) {
            
            if (OriginalLine.X1 < 0) OriginalLine.X1 = 0;
            if (OriginalLine.Y1 < 0) OriginalLine.Y1 = 0;
            /*
            if (OriginalLine.X1 > DeviceHandler.MAX_WIDTH) OriginalLine.X1 = DeviceHandler.MAX_WIDTH;
            if (OriginalLine.Y1 > DeviceHandler.MAX_HEIGHT) OriginalLine.Y1 = DeviceHandler.MAX_HEIGHT;

            if (OriginalLine.X2 < 0) OriginalLine.X2 = 0;
            if (OriginalLine.Y2 < 0) OriginalLine.Y2 = 0;
            if (OriginalLine.X2 > DeviceHandler.MAX_WIDTH) OriginalLine.X2 = DeviceHandler.MAX_WIDTH;
            if (OriginalLine.Y2 > DeviceHandler.MAX_HEIGHT) OriginalLine.Y2 = DeviceHandler.MAX_HEIGHT;
            */

            UpdatePicker();

            //update source
            Param.SetLine(Owner, Name, OriginalLine);
        }
        
        public void Dispose() {
            
        }
    }
}
