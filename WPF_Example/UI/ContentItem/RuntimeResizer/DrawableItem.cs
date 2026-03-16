using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace ReringProject.UI {
    public enum EDrawType {
        Rectangle,
        Line,
        Circle,
        Ellipse,
    }
    
    public interface IDrawableItem {
        object Owner { get; }

        string Name { get; }

        EDrawType DrawType { get; }

        EPickerPosition GetMouseIsEnter(int x, int y);

        void Render(DrawingContext drawingContext);

        void RenderPicker(DrawingContext drawingContext);

        void ExecMove(int moveX, int moveY);

        void ExecResize(int resizeX, int resizeY, int resizeWidth, int resizeHeight);

        void CheckAvailable(EPickerPosition moveType);
        
    }

    public enum EPickerPosition {
        None,
        Body,

        //rect
        Left,
        Top,
        Right,
        Bottom,
        LeftTop,
        LeftBottom,
        RightTop,
        RightBottom,

        //line
        Point1,
        Point2,
    }

    public class Picker {
        public EPickerPosition Position = EPickerPosition.None;
        public Rect Rect;

        public Picker(EPickerPosition pos) {
            Position = pos;
            Rect.Width = DrawableRectangle.PickerSize;
            Rect.Height = DrawableRectangle.PickerSize;
        }
    }
}
