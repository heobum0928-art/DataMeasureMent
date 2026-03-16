using ReringProject.Define;
using ReringProject.Device;
using ReringProject.Sequence;
using ReringProject.Utility;
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace ReringProject.UI {
    [ValueConversion(typeof(Rect), typeof(string))]
    public class RectConverter : IValueConverter {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            if (value is Rect) {
                var c = (Rect)value;
                if (targetType == typeof(string)) {
                    return string.Format("{0:0.00},{1:0.00},{2:0.00},{3:0.00}", c.X, c.Y, c.Width, c.Height);
                }
            }
            return DependencyProperty.UnsetValue;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            if (value is string) {
                var c = (string)value;
                if (targetType == typeof(Rect)) {
                    try {
                        int i = c.IndexOf(",", StringComparison.InvariantCultureIgnoreCase);
                        string[] ptList = c.Split(',');
                        if (ptList != null && ptList.Length >= 4) {
                            var sx = ptList[0];
                            var sy = ptList[1];
                            var sw = ptList[2];
                            var sh = ptList[3];

                            var x = double.Parse(sx, culture);
                            var y = double.Parse(sy, culture);
                            var w = double.Parse(sw, culture);
                            var h = double.Parse(sh, culture);

                            return new Rect(x, y, w, h);
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
    public class DrawableRectangle : IDrawableItem, IDisposable {
        //const
        public const int PickerCount = 8;
        public const int PickerSize = 40;
        public const int DefaultFontSize = 50;

        //data
        public string Name { get; private set; }
        public object Owner { get; private set; }

        private Rect OriginalRect;
        
        private Picker[] Pickers;

        //draw vars
        public static Pen OkPen = new Pen(Brushes.Lime, 2);
        public static Pen NgPen = new Pen(Brushes.Red, 2);
        public static Pen GuidePen = new Pen(Brushes.Fuchsia, 2);

        private Pen DrawPen;
        private Pen PickerDrawPen;

        private Typeface TextDrawFont;     
        private Brush TextDrawBrush;
        private int FontSize;

        private ParamBase Param;

        public DrawableRectangle(ParamBase param, object owner, string roiPropertyName) {
            Param = param;
            Owner = owner;
            Name = roiPropertyName;
            
            param.GetRect(owner, roiPropertyName, out Rect rect);
        
            if (rect.X < 0) rect.X = 0;
            if (rect.Y < 0) rect.Y = 0;
            if (rect.Width < DeviceHandler.MIN_ROI_WIDTH) rect.Width = DeviceHandler.MIN_ROI_WIDTH;
            else if (rect.Width > DeviceHandler.MAX_ROI_WIDTH) rect.Width = DeviceHandler.MAX_ROI_WIDTH;

            if (rect.Height < DeviceHandler.MIN_ROI_HEIGHT) rect.Height = DeviceHandler.MIN_ROI_HEIGHT;
            else if (rect.Height > DeviceHandler.MAX_ROI_HEIGHT) rect.Height = DeviceHandler.MAX_ROI_HEIGHT;

            OriginalRect = rect;
            
            Pickers = new Picker[PickerCount];
            Pickers[0] = new Picker(EPickerPosition.Left);
            Pickers[1] = new Picker(EPickerPosition.Right);
            Pickers[2] = new Picker(EPickerPosition.Top);
            Pickers[3] = new Picker(EPickerPosition.Bottom);

            Pickers[4] = new Picker(EPickerPosition.LeftTop);
            Pickers[5] = new Picker(EPickerPosition.RightTop);
            Pickers[6] = new Picker(EPickerPosition.LeftBottom);
            Pickers[7] = new Picker(EPickerPosition.RightBottom);

            //set colors
            SetDrawColor(Brushes.Cyan, 4);
            SetPickerDrawColor(Brushes.Cyan, 4);
            SetTextFont("맑은 고딕", DefaultFontSize, Brushes.Cyan);
            
            UpdatePicker();
        }

        public EDrawType DrawType { get { return EDrawType.Rectangle; } }

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
            drawingContext.DrawRectangle(Brushes.Transparent, DrawPen, OriginalRect);

            //draw text
            string name = Name;
            if (!(Owner is ParamBase))
            {
                name = Owner.ToString();
            }
            FormattedText formattedText = new FormattedText(name, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, TextDrawFont, FontSize, TextDrawBrush, 1.0);
            drawingContext.DrawRectangle(Brushes.Black, new Pen(Brushes.Black, 1), new Rect(X, Y - DefaultFontSize-10, formattedText.Width, formattedText.Height-14));
            drawingContext.DrawText(formattedText, new Point(X, Y-DefaultFontSize-20));
            
        }

        public void RenderPicker(DrawingContext drawingContext) {
            foreach (Picker picker in Pickers) {
                drawingContext.DrawRectangle(PickerDrawPen.Brush, PickerDrawPen, picker.Rect);
            }
        }

        public EPickerPosition GetMouseIsEnter(int x, int y) {
            //is in body
            if ((x > OriginalRect.Left) && (y > OriginalRect.Top) && (x < OriginalRect.Left + OriginalRect.Width) && (y < OriginalRect.Top + OriginalRect.Height)) {
                return EPickerPosition.Body;
            }
            //is in picker
            foreach (Picker picker in Pickers) {
                if ((x > picker.Rect.Left) && (y > picker.Rect.Top) && (x < picker.Rect.Left + picker.Rect.Width) && (y < picker.Rect.Top + picker.Rect.Height)) {
                    return picker.Position;
                }
            }
            return EPickerPosition.None;
        }

        public int X {
            get {
                return (int)OriginalRect.X;
            }
            set { OriginalRect.X = value; }
        }

        public int Y {
            get { return (int)OriginalRect.Y; }
            set { OriginalRect.Y = value; }
        }

        public int Width {
            get { return (int)OriginalRect.Width; }
            set { OriginalRect.Width = value; }
        }

        public int Height {
            get { return (int)OriginalRect.Height; }
            set { OriginalRect.Height = value; }
        }

        public void UpdatePicker() {
            //left
            Pickers[0].Rect.X = OriginalRect.Left - PickerSize;
            Pickers[0].Rect.Y = OriginalRect.Top + (OriginalRect.Height / 2) - (PickerSize / 2);

            //right
            Pickers[1].Rect.X = OriginalRect.Left + OriginalRect.Width;
            Pickers[1].Rect.Y = OriginalRect.Top + (OriginalRect.Height / 2) - (PickerSize / 2);

            //top
            Pickers[2].Rect.X = OriginalRect.Left + (OriginalRect.Width / 2) - (PickerSize / 2);
            Pickers[2].Rect.Y = OriginalRect.Top - PickerSize;

            //bottom
            Pickers[3].Rect.X = OriginalRect.Left + (OriginalRect.Width / 2) - (PickerSize / 2);
            Pickers[3].Rect.Y = OriginalRect.Top + OriginalRect.Height;

            //left top
            Pickers[4].Rect.X = OriginalRect.Left - PickerSize;
            Pickers[4].Rect.Y = OriginalRect.Top - PickerSize;

            //right top
            Pickers[5].Rect.X = OriginalRect.Left + OriginalRect.Width;
            Pickers[5].Rect.Y = OriginalRect.Top - PickerSize;

            //left bottom
            Pickers[6].Rect.X = OriginalRect.Left - PickerSize;
            Pickers[6].Rect.Y = OriginalRect.Top + OriginalRect.Height;

            //right bottom
            Pickers[7].Rect.X = OriginalRect.Left + OriginalRect.Width;
            Pickers[7].Rect.Y = OriginalRect.Top + OriginalRect.Height;
            
        }

        public virtual void ExecMove(int moveX, int moveY) {
            this.OriginalRect.X += moveX;
            this.OriginalRect.Y += moveY;

            UpdatePicker();
        }

        public virtual void ExecResize(int resizeX, int resizeY, int resizeWidth, int resizeHeight) {
            if ((OriginalRect.X + resizeX) > 0) this.OriginalRect.X += resizeX;
            if ((OriginalRect.Y + resizeY) > 0) this.OriginalRect.Y += resizeY;
            if ((OriginalRect.Width + resizeWidth) > 0) this.OriginalRect.Width += resizeWidth;
            if ((OriginalRect.Height + resizeHeight) > 0) this.OriginalRect.Height += resizeHeight;

            UpdatePicker();
        }

        public virtual void CheckAvailable(EPickerPosition moveType) {
            if (OriginalRect.X < 0) OriginalRect.X = 0;
            if (OriginalRect.Y < 0) OriginalRect.Y = 0;
            //if (OriginalRect.X + OriginalRect.Width > DeviceHandler.MAX_WIDTH) OriginalRect.Width = DeviceHandler.MAX_ROI_WIDTH;
            //if (OriginalRect.Y + OriginalRect.Height > DeviceHandler.MAX_HEIGHT) OriginalRect.Height = DeviceHandler.MAX_ROI_HEIGHT;
            //if (OriginalRect.Width < DeviceHandler.MIN_ROI_WIDTH) OriginalRect.Width = DeviceHandler.MIN_ROI_WIDTH;
            //if (OriginalRect.Height < DeviceHandler.MIN_ROI_HEIGHT) OriginalRect.Height = DeviceHandler.MIN_ROI_HEIGHT;
            
            UpdatePicker();

            //update source
            Param.SetRect(Owner, Name, OriginalRect);
        }
        
        public void Dispose() {
            
        }
    }
}
