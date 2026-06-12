using HalconDotNet;
using ReringProject.Halcon;
using ReringProject.Halcon.Models;
using ReringProject.Define;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows.Media;

namespace ReringProject.Sequence {

#region Enums
    public enum EContextResult {
        None,
        Pass,
        Fail,
        Error,
        WaferAngleError,
        WaferDieTeaching,
    }

    public enum EContextState {
        Idle,
        Paused,
        Running,
        Finish,
        Error,
    }
#endregion

    public class ActionContext {
        public ActionBase Source { get; private set; }

        public HImage ResultHalconImage { get; set; }

        public string ResultImagePath { get; set; }

        public List<EdgeInspectionOverlay> InspectionOverlays { get; set; } = new List<EdgeInspectionOverlay>();

        public List<string> DisplayMessages { get; set; } = new List<string>(); //260409 hbk

        public EContextState State { get; set; }

        public EContextResult Result { get; set; }

        public int CurrentStep { get; set; }

        public Stopwatch Timer { get; private set; } = new Stopwatch();

        public ActionContext(ActionBase source) {
            Source = source;
        }

        public virtual void Clear() {
            State = EContextState.Idle;
            Result = EContextResult.None;
            CurrentStep = 0;
            if (ResultHalconImage != null) { //260612 hbk Wave5
                ResultHalconImage.Dispose();
            }
            ResultHalconImage = null;
            ResultImagePath = null;
            InspectionOverlays.Clear();
            DisplayMessages.Clear(); //260409 hbk
        }

        public string GetStateString => Enum.GetName(typeof(EContextState), State);

        public string GetResultString => Enum.GetName(typeof(EContextResult), Result);

        public void CopyFrom(SequenceContext seqContext) {
            if (seqContext == null) return;
            if (ResultHalconImage != null) { //260612 hbk Wave5
                ResultHalconImage.Dispose();
            }
            ResultHalconImage = HalconImageBridge.Clone(seqContext.ResultHalconImage);
            ResultImagePath = seqContext.ResultImagePath;
            if (seqContext.InspectionOverlays == null) { //260612 hbk Wave5
                InspectionOverlays = new List<EdgeInspectionOverlay>();
            } else {
                InspectionOverlays = seqContext.InspectionOverlays.Select(overlay => overlay.Clone()).ToList();
            }
            if (seqContext.DisplayMessages == null) { //260612 hbk Wave5
                DisplayMessages = new List<string>();
            } else {
                DisplayMessages = new List<string>(seqContext.DisplayMessages); //260409 hbk
            }
        }
    }

    public class SequenceContext {
        public SequenceBase Source { get; private set; }

        public ParamBase ActionParam { get; set; }

        public HImage ResultHalconImage { get; set; }

        public string ResultImagePath { get; set; }

        public List<EdgeInspectionOverlay> InspectionOverlays { get; set; } = new List<EdgeInspectionOverlay>();

        public List<string> DisplayMessages { get; set; } = new List<string>(); //260409 hbk

        public string TargetCode { get; set; }

        public string ResultImageFileName { get; set; }

        private EContextState _state;

        public EContextState State {
            get {
                if (Source != null && !Source.IsInitialized) {
                    return EContextState.Error;
                }
                return _state;
            }
            set {
                _state = value;
            }
        }

        public EContextResult Result { get; set; }

        public string ResultStr { get; set; }

        public Stopwatch Timer { get; private set; } = new Stopwatch();

        protected static Brush OkColor = Brushes.Lime;
        protected static Brush NgColor = Brushes.Red;
        protected static Pen OkPen = new Pen(OkColor, 3);
        protected static Pen NgPen = new Pen(NgColor, 3);
        protected static Typeface TextDrawFont = new Typeface("Arial");
        protected static int FontSize = 60;

        public SequenceContext(SequenceBase source) {
            Source = source;
        }

        public virtual void RenderResult(DrawingContext dc) {
        }

        public string StateString => Enum.GetName(typeof(EContextState), State);

        public string ResultString => Enum.GetName(typeof(EContextResult), Result);

        public virtual void Clear() {
            for (int i = 0; i < Source.ActionCount; i++) {
                ActionBase act = Source.GetAction(i);
                act.Context.Clear();
            }

            ResultImageFileName = string.Empty;
            if (ResultHalconImage != null) { //260612 hbk Wave5
                ResultHalconImage.Dispose();
            }
            ResultHalconImage = null;
            ResultImagePath = null;
            InspectionOverlays.Clear();
            DisplayMessages.Clear(); //260409 hbk

            Timer.Restart();
            State = EContextState.Idle;
            Result = EContextResult.None;
        }

        public virtual void CopyFrom(ActionContext actionContext) {
            if (actionContext == null) return;
            if (ResultHalconImage != null) { //260612 hbk Wave5
                ResultHalconImage.Dispose();
            }
            ResultHalconImage = HalconImageBridge.Clone(actionContext.ResultHalconImage);
            ResultImagePath = actionContext.ResultImagePath;
            if (actionContext.InspectionOverlays == null) { //260612 hbk Wave5
                InspectionOverlays = new List<EdgeInspectionOverlay>();
            } else {
                InspectionOverlays = actionContext.InspectionOverlays.Select(overlay => overlay.Clone()).ToList();
            }
            if (actionContext.DisplayMessages == null) { //260612 hbk Wave5
                DisplayMessages = new List<string>();
            } else {
                DisplayMessages = new List<string>(actionContext.DisplayMessages); //260409 hbk
            }
        }
    }
}
