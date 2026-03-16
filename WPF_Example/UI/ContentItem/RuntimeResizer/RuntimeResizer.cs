using System;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows;
using System.Windows.Input;
using System.Collections.Generic;
using ReringProject.Sequence;
using ReringProject.Device;
using System.ComponentModel;

namespace ReringProject.UI {
    public class SelectionChangedCallbackArg : EventArgs {
        public IDrawableItem SelectedItem { get; private set; }

        public SelectionChangedCallbackArg(IDrawableItem selected) {
            SelectedItem = selected;
        }
    }

    public delegate void OnDrawingItemSelectionChanged(object sender, SelectionChangedCallbackArg args);

    public class RuntimeResizer : Canvas, INotifyPropertyChanged {
        //operation
        private EPickerPosition CurrentActionMode = EPickerPosition.None;
        
        public ScaleTransform _ScaleTransform { get; set; }

        //translateTransform
        //public TranslateTransform _TranslateTransform { get; set; }
        public ScrollViewer ParentScrollViewer { get; set; }

        //spec
        private ParamBase pParam;

        //drawable
        public IDrawableItem SelectedItem { get; private set; }
        private List<IDrawableItem> DrawableList = new List<IDrawableItem>();
        private object mDrawInterlock = new object();
        //event
        public event OnDrawingItemSelectionChanged OnSelectionItemChanged;
        public event PropertyChangedEventHandler PropertyChanged;

        private SequenceContext SequenceResult;

        private bool _IsEditable = false;
        public bool IsEditable {
            get { return _IsEditable; }
            set {
                if (_IsEditable != value) {
                    _IsEditable = value;
                    SelectedItem = null;
                    InvalidateVisual();
                }
            }
        }

        private Point DownPosition;
        private Point ScrollStartPos;
        private Point ScrollEndPos;
        private Point CurrentPos;

        private string _CurrentPosDisplay;
        public string CurrentPosDisplay {
            get {
                return _CurrentPosDisplay;
            }
            set {
                _CurrentPosDisplay = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("CurrentPosDisplay"));
            }
        }
            
        
        public RuntimeResizer() : base() {
            this.MouseDown += this.OnMouseDown;
            this.MouseMove += this.OnMouseMove;
            this.MouseUp += this.OnMouseUp;
        }

        public void SetParam(ICameraParam param) {
            if(param is ParamBase) {
                ParamBase pb = param as ParamBase;
                SetParam(pb);
            }
        }

        public void SetParam(ParamBase param) {
            lock (mDrawInterlock) {
                
                pParam = param;
                SequenceResult = null;

                //ROI attribute를 검색해서 drawable list를 구축한다.
                DrawableList.Clear();
                if (pParam != null) {
                    for (int i = 0; i < pParam.GetRectCount(); i++) {
                        //pParam.GetROI(i, out Rect item);
                        pParam.GetRectName(i, out string name);
                        pParam.GetRectOwner(i, out object owner);
                        DrawableList.Add(new DrawableRectangle(pParam, owner, name));
                    }
                    for(int i = 0; i < pParam.GetLineCount(); i++) {
                        pParam.GetLineName(i, out string name);
                        pParam.GetLineOwner(i, out object owner);
                        DrawableList.Add(new DrawableLine(pParam, owner, name));
                    }
                    for(int i = 0; i < pParam.GetCircleCount(); i++) {
                        pParam.GetCircleName(i, out string name);
                        pParam.GetCircleOwner(i, out object owner);
                        DrawableList.Add(new DrawableCircle(pParam, owner, name));
                    }
                }
            }
            this.InvalidateVisual();
        }
        /*
        public void OnKeyUp(object sender, KeyEventArgs e) {
            
            if (IsEditable == false) return;
            if (SelectedItem == null) {
                e.Handled = true;
                return;
            }
            bool shift = Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift);
            bool ctrl = Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl);
            switch(e.Key) {
                case Key.Up:
                    if (shift) { //size up
                        SelectedItem.ExecResize(0, (int)-(10 / _ScaleTransform.ScaleY), 0, (int)(10 / _ScaleTransform.ScaleY));
                    }
                    else if(ctrl) { //move
                        SelectedItem.ExecMove(0, (int)(-10 / _ScaleTransform.ScaleY));
                    }
                    break;
                case Key.Down:
                    if(shift) { //size up
                        SelectedItem.ExecResize(0, (int)(10 / _ScaleTransform.ScaleY), 0, (int)-(10 / _ScaleTransform.ScaleY));
                    }
                    else if (ctrl) { //move
                        SelectedItem.ExecMove(0, (int)(10 / _ScaleTransform.ScaleY));
                    }
                    break;
                case Key.Left:
                    break;
                case Key.Right:
                    break;
            }
            SelectedItem.CheckAvailable(CurrentActionMode);
            this.InvalidateVisual();
        }
        */

        public void OnMouseDown(object sender, MouseEventArgs e) {
            if (IsEditable == false) {
                this.CaptureMouse();
                ScrollStartPos = e.GetPosition(ParentScrollViewer);
                return;
            }

            bool IsSelected = false;

            //down 위치를 저장함.
            DownPosition.X = e.GetPosition(this).X;
            DownPosition.Y = e.GetPosition(this).Y;

            //이미 선택되어 있다면
            if (SelectedItem != null) {
                CurrentActionMode = SelectedItem.GetMouseIsEnter((int)(e.GetPosition(this).X / _ScaleTransform.ScaleX), (int)(e.GetPosition(this).Y / _ScaleTransform.ScaleY));
                if (CurrentActionMode == EPickerPosition.None) {
                    SelectedItem = null;
                    OnSelectionItemChanged?.Invoke(this, new SelectionChangedCallbackArg(SelectedItem));
                }
                else if(e.RightButton == MouseButtonState.Pressed) {
                    //다음 item 선택 (선택 전환)
                    int curIndex = DrawableList.IndexOf(SelectedItem);
                    foreach (IDrawableItem item in DrawableList) {
                        if (item == SelectedItem) continue;
                        if (item.GetMouseIsEnter((int)(e.GetPosition(this).X / _ScaleTransform.ScaleX), (int)(e.GetPosition(this).Y / _ScaleTransform.ScaleY)) == EPickerPosition.Body) {
                            SelectedItem = item;
                            OnSelectionItemChanged?.Invoke(this, new SelectionChangedCallbackArg(SelectedItem));
                            return;
                        }
                    }
                    return;
                }
                else return;
            }

            //선택 작업
            foreach (IDrawableItem item in DrawableList) {
                //roi의 자식 item이 선택되었는지를 확인
                switch (item.GetMouseIsEnter((int)(e.GetPosition(this).X / _ScaleTransform.ScaleX), (int)(e.GetPosition(this).Y / _ScaleTransform.ScaleY))) {
                    case EPickerPosition.Body:
                        SelectedItem = item;
                        IsSelected = true;
                        OnSelectionItemChanged?.Invoke(this, new SelectionChangedCallbackArg(SelectedItem));
                        break;
                    case EPickerPosition.LeftBottom:
                    case EPickerPosition.LeftTop:
                    case EPickerPosition.RightBottom:
                    case EPickerPosition.RightTop:
                    case EPickerPosition.Left:
                    case EPickerPosition.Right:
                    case EPickerPosition.Top:
                    case EPickerPosition.Bottom:
                        if (SelectedItem != null) IsSelected = true;
                        break;
                    case EPickerPosition.Point1:
                    case EPickerPosition.Point2:
                        if (SelectedItem != null) IsSelected = true;
                        break;
                }

                //아무 자식도 선택되지 않았으면.. ROI 자신을 선택
                if (!IsSelected) {
                    if (item.GetMouseIsEnter((int)(e.GetPosition(this).X / _ScaleTransform.ScaleX), (int)(e.GetPosition(this).Y / _ScaleTransform.ScaleY)) == EPickerPosition.Body) {
                        //이미 추가된 항목을 추가하지 않도록 중복 체크함.
                        SelectedItem = item;
                        IsSelected = true;
                        OnSelectionItemChanged?.Invoke(this, new SelectionChangedCallbackArg(SelectedItem));
                        break;
                    }
                }
            }
            if (!IsSelected) {
                this.CaptureMouse();
                ScrollStartPos = e.GetPosition(ParentScrollViewer);
            }
            this.InvalidateVisual();
        }

        public void OnMouseMove(object sender, MouseEventArgs e) {
            
            CurrentPos = e.GetPosition(this);
            CurrentPosDisplay = string.Format("X:{0:0.0}, Y:{1:0.0}", CurrentPos.X / _ScaleTransform.ScaleX, CurrentPos.Y / _ScaleTransform.ScaleY);

            if (this.IsMouseCaptured) {
                ScrollEndPos = e.GetPosition(ParentScrollViewer);
                double x = ScrollEndPos.X - ScrollStartPos.X;
                double y = ScrollEndPos.Y - ScrollStartPos.Y;
                ParentScrollViewer.ScrollToHorizontalOffset(ParentScrollViewer.HorizontalOffset - x);
                ParentScrollViewer.ScrollToVerticalOffset(ParentScrollViewer.VerticalOffset - y);
                ScrollStartPos = ScrollEndPos;

                if (IsEditable == false) return;
            }

            else if (SelectedItem != null) {
                if (e.LeftButton == MouseButtonState.Pressed) {
                    switch (CurrentActionMode) {
                        case EPickerPosition.Body:
                            Cursor = Cursors.SizeAll;
                            SelectedItem.ExecMove(
                                (int)((CurrentPos.X - DownPosition.X) / _ScaleTransform.ScaleX),
                                (int)((CurrentPos.Y - DownPosition.Y) / _ScaleTransform.ScaleY));

                            DownPosition.X = CurrentPos.X;
                            DownPosition.Y = CurrentPos.Y;
                            break;
                        case EPickerPosition.Left:
                            Cursor = Cursors.SizeWE;
                            SelectedItem.ExecResize(
                                (int)((CurrentPos.X - DownPosition.X) / _ScaleTransform.ScaleX),
                                0,
                                (int)-((CurrentPos.X - DownPosition.X) / _ScaleTransform.ScaleX),
                                0);

                            DownPosition.X = CurrentPos.X;
                            DownPosition.Y = CurrentPos.Y;
                            break;
                        case EPickerPosition.Right:
                            Cursor = Cursors.SizeWE;
                            SelectedItem.ExecResize(
                                0,
                                0,
                                (int)((CurrentPos.X - DownPosition.X) / _ScaleTransform.ScaleX),
                                0);

                            DownPosition.X = CurrentPos.X;
                            DownPosition.Y = CurrentPos.Y;
                            break;
                        case EPickerPosition.Top:
                            Cursor = Cursors.SizeNS;
                            SelectedItem.ExecResize(
                                0,
                                (int)((CurrentPos.Y - DownPosition.Y) / _ScaleTransform.ScaleY),
                                0,
                                (int)-((CurrentPos.Y - DownPosition.Y) / _ScaleTransform.ScaleY));

                            DownPosition.X = CurrentPos.X;
                            DownPosition.Y = CurrentPos.Y;
                            break;
                        case EPickerPosition.Bottom:
                            Cursor = Cursors.SizeNS;
                            SelectedItem.ExecResize(
                                0,
                                0,
                                0,
                                (int)((CurrentPos.Y - DownPosition.Y) / _ScaleTransform.ScaleY));

                            DownPosition.X = CurrentPos.X;
                            DownPosition.Y = CurrentPos.Y;
                            break;
                        case EPickerPosition.LeftBottom:
                            Cursor = Cursors.SizeNESW;
                            SelectedItem.ExecResize(
                                (int)((CurrentPos.X - DownPosition.X) / _ScaleTransform.ScaleX),
                                0,
                                (int)-((CurrentPos.X - DownPosition.X) / _ScaleTransform.ScaleX),
                                (int)((CurrentPos.Y - DownPosition.Y) / _ScaleTransform.ScaleY));

                            DownPosition.X = CurrentPos.X;
                            DownPosition.Y = CurrentPos.Y;
                            break;
                        case EPickerPosition.LeftTop:
                            Cursor = Cursors.SizeNWSE;
                            SelectedItem.ExecResize(
                                (int)((CurrentPos.X - DownPosition.X) / _ScaleTransform.ScaleX),
                                (int)((CurrentPos.Y - DownPosition.Y) / _ScaleTransform.ScaleY),
                                (int)-((CurrentPos.X - DownPosition.X) / _ScaleTransform.ScaleX),
                                (int)-((CurrentPos.Y - DownPosition.Y) / _ScaleTransform.ScaleY));

                            DownPosition.X = CurrentPos.X;
                            DownPosition.Y = CurrentPos.Y;
                            break;
                        case EPickerPosition.RightBottom:
                            Cursor = Cursors.SizeNWSE;
                            SelectedItem.ExecResize(
                                0,
                                0,
                                (int)((CurrentPos.X - DownPosition.X) / _ScaleTransform.ScaleX),
                                (int)((CurrentPos.Y - DownPosition.Y) / _ScaleTransform.ScaleY));
                            DownPosition.X = CurrentPos.X;
                            DownPosition.Y = CurrentPos.Y;
                            break;
                        case EPickerPosition.RightTop:
                            Cursor = Cursors.SizeNESW;
                            SelectedItem.ExecResize(
                                0,
                                (int)((CurrentPos.Y - DownPosition.Y) / _ScaleTransform.ScaleY),
                                (int)((CurrentPos.X - DownPosition.X) / _ScaleTransform.ScaleX),
                                (int)-((CurrentPos.Y - DownPosition.Y) / _ScaleTransform.ScaleY));

                            DownPosition.X = CurrentPos.X;
                            DownPosition.Y = CurrentPos.Y;
                            break;
                        case EPickerPosition.Point1:
                            Cursor = Cursors.SizeWE;
                            SelectedItem.ExecResize(
                                (int)((CurrentPos.X - DownPosition.X) / _ScaleTransform.ScaleX),
                                (int)((CurrentPos.Y - DownPosition.Y) / _ScaleTransform.ScaleY),
                                0,
                                0);
                            DownPosition.X = CurrentPos.X;
                            DownPosition.Y = CurrentPos.Y;
                            break;
                        case EPickerPosition.Point2:
                            Cursor = Cursors.SizeWE;
                            SelectedItem.ExecResize(
                                0,
                                0,
                                (int)((CurrentPos.X - DownPosition.X) / _ScaleTransform.ScaleX),
                                (int)((CurrentPos.Y - DownPosition.Y) / _ScaleTransform.ScaleY));
                            DownPosition.X = CurrentPos.X;
                            DownPosition.Y = CurrentPos.Y;
                            break;
                        case EPickerPosition.None:
                            Cursor = Cursors.Arrow;
                            break;

                    }
                    this.InvalidateVisual();
                }
                else {
                    switch (SelectedItem.GetMouseIsEnter((int)(CurrentPos.X / _ScaleTransform.ScaleX), (int)(CurrentPos.Y / _ScaleTransform.ScaleY))) {
                        case EPickerPosition.Body:
                            Cursor = Cursors.SizeAll;
                            break;
                        case EPickerPosition.Left:
                            Cursor = Cursors.SizeWE;
                            break;
                        case EPickerPosition.Right:
                            Cursor = Cursors.SizeWE;
                            break;
                        case EPickerPosition.Top:
                            Cursor = Cursors.SizeNS;
                            break;
                        case EPickerPosition.Bottom:
                            Cursor = Cursors.SizeNS;
                            break;
                        case EPickerPosition.LeftBottom:
                            Cursor = Cursors.SizeNESW;
                            break;
                        case EPickerPosition.LeftTop:
                            Cursor = Cursors.SizeNWSE;
                            break;
                        case EPickerPosition.RightBottom:
                            Cursor = Cursors.SizeNWSE;
                            break;
                        case EPickerPosition.RightTop:
                            Cursor = Cursors.SizeNESW;
                            break;
                        case EPickerPosition.None:
                            Cursor = Cursors.Arrow;
                            break;
                        case EPickerPosition.Point1:
                        case EPickerPosition.Point2:
                            Cursor = Cursors.SizeWE;
                            break;
                    }
                }
            }
            else {
                Cursor = Cursors.Arrow;
            }
        }

        public void OnMouseUp(object sender, MouseEventArgs e) {
            this.ReleaseMouseCapture();

            if (IsEditable == false) return;
            if (SelectedItem != null) {
                SelectedItem.CheckAvailable(CurrentActionMode);
                this.InvalidateVisual();
            }
        }

        public void SetContext(SequenceContext context) {
            lock (mDrawInterlock) {
                SequenceResult = context;
                DrawableList.Clear();
                if ((SequenceResult != null) && (SequenceResult.ActionParam != null)) {
                    pParam = SequenceResult.ActionParam;
                    
                    for (int i = 0; i < pParam.GetRectCount(); i++) {
                        //pParam.GetROI(i, out Rect item);
                        pParam.GetRectName(i, out string name);
                        pParam.GetRectOwner(i, out object owner);
                        DrawableList.Add(new DrawableRectangle(pParam, owner, name));
                    }
                    for (int i = 0; i < pParam.GetLineCount(); i++) {
                        pParam.GetLineName(i, out string name);
                        pParam.GetLineOwner(i, out object owner);
                        DrawableList.Add(new DrawableLine(pParam, owner, name));
                    }
                    for (int i = 0; i < pParam.GetCircleCount(); i++) {
                        pParam.GetCircleName(i, out string name);
                        pParam.GetCircleOwner(i, out object owner);
                        DrawableList.Add(new DrawableCircle(pParam, owner, name));
                    }
                }
            }
        }

        protected override void OnRender(DrawingContext dc) {
            base.OnRender(dc);
            lock (mDrawInterlock) {
                dc.PushTransform(this._ScaleTransform);

                //draw center lines
                if (SequenceResult != null) {
                    SequenceResult.RenderResult(dc);
                    if(SequenceResult.Source.Param is CameraMasterParam) {
                        CameraMasterParam camParam = SequenceResult.Source.Param as CameraMasterParam;
                        VirtualCamera cam = SystemHandler.Handle.Devices[camParam.DeviceName];
                        if (cam != null) {
                            cam.RenderCenterLine(dc);
                        }
                    }
                }
                else if (pParam != null) {
                    if(pParam is ICameraParam) {
                        ICameraParam camParam = pParam as ICameraParam;
                        VirtualCamera cam = SystemHandler.Handle.Devices[camParam.DeviceName];
                        if(cam != null) {
                            cam.RenderCenterLine(dc);
                        }
                    }
                }

                if (IsEditable == false) return;
                foreach (IDrawableItem item in DrawableList) {
                    item.Render(dc);

                    if (IsEditable && (item == SelectedItem)) {
                        item.RenderPicker(dc);
                    }
                }
            }
        }
    }

}
