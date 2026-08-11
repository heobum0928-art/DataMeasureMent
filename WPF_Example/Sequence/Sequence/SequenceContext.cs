using HalconDotNet;
using ReringProject.Halcon;
using ReringProject.Halcon.Models;
using ReringProject.Define;
using ReringProject.Utility;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
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
            // 260811 odo: seqContext.ResultHalconImage 원시 필드가 제거됐으므로 소유권 API(CloneResultImage)
            //  경유로 교체 — 획득→CopyImage→Release 를 원자적으로 수행해 use-after-dispose 레이스 차단.
            //  ActionContext 자기 필드(이 클래스) 처리는 범위 밖이라 무변경(이전 것 Dispose 후 대입).
            ResultHalconImage = seqContext.CloneResultImage();
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

    // 260811 odo: 결과 이미지 소유권 계약.
    //  SequenceContext 는 결과 이미지를 refcount(SharedHImage)로 소유하고, 원시 HImage 참조를
    //  클래스 밖으로 노출하지 않는다. 외부(다른 스레드)는 아래 두 방식 중 하나만 써야 한다:
    //    (1) AcquireResultImage() 로 획득 성공 시에만 그 구간 안에서 읽고 반드시 finally 에서 Release() — 원시 필드 접근 금지.
    //    (2) CloneResultImage() 로 소유권 있는 사본을 받아 그 사본을 자유롭게 소유.
    //  이유: 시퀀스/MainRun 스레드(해제자)와 UI 스레드(독자)가 원시 HImage 를 공유하면, 해제자가
    //  Dispose() 한 직후 독자가 그 힙을 읽는 use-after-dispose 레이스가 성립한다. HALCON 의
    //  AccessViolationException 은 Corrupted State Exception 이라 .NET try/catch(Exception) 로 잡히지
    //  않으므로, 유일한 해법은 "해제된 메모리를 읽는 경로 자체를 없애는 것"이다. refcount 는 마지막
    //  참조자가 해제를 수행하게 해 그 경로를 원천 차단한다(TryAddRef 가 "이미 해제됨"을 원자적으로
    //  알려주므로 획득 자체가 실패하거나, 획득에 성공하면 그 구간에서는 절대 해제되지 않는다).
    public class SequenceContext {
        public SequenceBase Source { get; private set; }

        public ParamBase ActionParam { get; set; }

        // 260811 odo: 원시 HImage 공개 프로퍼티 제거 — AcquireResultImage/SetResultImageOwned/CloneResultImage 로 대체.
        //  Volatile.Read/Interlocked.Exchange 로 스레드 간 가시성/원자적 교체를 보장한다.
        private SharedHImage _resultShared;

        /// <summary>
        /// 결과 이미지 획득 시도. non-null 반환 시 반드시 finally 에서 Release() 해야 한다(안 하면 refcount 누수).
        /// 획득 구간 안에서는 반환된 SharedHImage.Image 가 해제되지 않음이 보장된다. 이미 해제됐거나
        /// 애초에 결과 이미지가 없으면 null 을 반환한다(둘은 호출자 입장에서 동일하게 취급 — "표시할 이미지 없음").
        /// </summary>
        public SharedHImage AcquireResultImage() {
            SharedHImage shared = Volatile.Read(ref _resultShared);
            if (shared == null) { return null; }
            if (!shared.TryAddRef()) { return null; } // 이미 마지막 Release 로 해제됨 — 이미지 없음과 동일 취급
            return shared;
        }

        /// <summary>
        /// 결과 이미지 소유권을 이 컨텍스트로 이전한다. 호출자는 이 호출 이후 image 를 직접 만지지 않는다
        /// (Dispose 포함 — 소유권이 넘어갔다). image 가 null 이면 기존 결과 이미지를 해제만 한다.
        /// 이전 값은 여기서 즉시 Dispose 하지 않고 Release() 한다 — 실제 해제는 마지막 참조자(획득 중인
        /// UI 스레드일 수 있음)가 수행하므로 이 호출은 항상 O(1)이고 절대 블로킹하지 않는다.
        /// </summary>
        public void SetResultImageOwned(HImage image) {
            SharedHImage next = image != null ? new SharedHImage(image) : null;
            SharedHImage prev = Interlocked.Exchange(ref _resultShared, next);
            if (prev != null) { prev.Release(); }
        }

        /// <summary>
        /// 결과 이미지의 소유권 있는 사본을 반환한다(없으면 null). 획득→CopyImage()→finally Release() 를
        /// 한 번에 수행 — 시퀀스 측 독자(SaveResultImage, ActionContext.CopyFrom)가 사용.
        /// </summary>
        public HImage CloneResultImage() {
            SharedHImage shared = AcquireResultImage();
            if (shared == null) { return null; }
            try {
                return shared.Image.CopyImage();
            }
            finally {
                shared.Release();
            }
        }

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
            SetResultImageOwned(null); // 260811 odo: 원자적 소유권 API 경유(주 해제자) — 이전 값은 O(1) Release, 실제 Dispose 는 마지막 참조자가 수행
            ResultImagePath = null;
            InspectionOverlays.Clear();
            DisplayMessages.Clear(); //260409 hbk

            Timer.Restart();
            State = EContextState.Idle;
            Result = EContextResult.None;
        }

        public virtual void CopyFrom(ActionContext actionContext) {
            if (actionContext == null) return;
            // 260811 odo: clone 은 기존과 동일하게 시퀀스 스레드에서 1회 수행(복사 횟수 불변). 소유권
            //  이전은 SetResultImageOwned 가 O(1) Interlocked.Exchange + 이전 값 Release 로 처리(부차 해제자).
            SetResultImageOwned(HalconImageBridge.Clone(actionContext.ResultHalconImage));
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
