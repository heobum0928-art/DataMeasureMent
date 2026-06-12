using ReringProject.Define;
using ReringProject.Device;
using ReringProject.Network;
using ReringProject.UI;
using System.Windows;

namespace ReringProject.Sequence {
    public enum ETopActionType {
        Carrier,
        Socket,
    }

    public class TopSequenceContext : SequenceContext {
        public double CenterOffsetXmm { get; set; }
        public double CenterOffsetYmm { get; set; }
        public double dAngle { get; set; }
        public EVisionResultType ResultInfo { get; set; }

        public TopSequenceContext(TopSequence source) : base(source) { }

        public override void Clear() {
            CenterOffsetXmm = 0;
            CenterOffsetYmm = 0;
            dAngle = 0;
            ResultInfo = EVisionResultType.NG;
            base.Clear();
        }

        public override void CopyFrom(ActionContext actionContext) {
            base.CopyFrom(actionContext);
            Result = actionContext.Result;
            if (actionContext is TopInspectionContext inspection) {
                CenterOffsetXmm = inspection.CenterOffsetXmm;
                CenterOffsetYmm = inspection.CenterOffsetYmm;
                dAngle = inspection.AngleDeg;
                ResultInfo = inspection.InspectResult;
            }
        }
    }

    [System.Obsolete("Phase 33 — InspectionSequence/Action_FAIMeasurement 로 마이그레이션됨", false)]
    public class TopSequence : SequenceBase {
        private readonly DeviceHandler pDevs;
        private readonly TopSequenceContext pMyContext;
        private readonly CameraMasterParam pMyParam;
        private readonly string DefaultCamera;
        private readonly string DefaultLight;

        public TopSequence(ESequence seqID, string name, int algIndex, string defaultCamera, string defaultLight) : base(seqID, name) {
            pDevs = SystemHandler.Handle.Devices;
            Context = new TopSequenceContext(this);
            pMyContext = Context as TopSequenceContext;
            Param = new CameraMasterParam(this);
            pMyParam = Param as CameraMasterParam;
            DefaultCamera = defaultCamera;
            DefaultLight = defaultLight;
        }

        protected override void AddResponse() {
            if (RequestPacket == null) return;

            var responsePacket = new TestResultPacket {
                Target = RequestPacket.Sender,
                Site = RequestPacket.Site,
                InspectionType = RequestPacket.TestType,
                Result = pMyContext.ResultInfo,
                Angle = pMyContext.dAngle,
                X = pMyContext.CenterOffsetXmm,
                Y = pMyContext.CenterOffsetYmm,
            };
            ResponseQueue.Enqueue(responsePacket);
        }

        public override void OnCreate() {
            pMyParam.LightGroupName = DefaultLight;
            pMyParam.DeviceName = DefaultCamera;

            var camera = pDevs[pMyParam.DeviceName];
            if (camera == null || camera.Properties == null) {
                CustomMessageBox.Show("Error", $"Camera {pMyParam.DeviceName} - Initialize Fail", MessageBoxImage.Error);
                IsInitialized = false;
                Context.State = EContextState.Error;
                return;
            }

            IsInitialized = true;
            base.OnCreate();
        }

        public override void OnLoad() {
            SystemHandler.Handle.Lights.ApplyLight(pMyParam);
            base.OnLoad();
        }

        public override void OnRelease() {
            IsInitialized = false;
            base.OnRelease();
        }
    }
}
