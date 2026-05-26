using ReringProject.Define;
using ReringProject.Device;
using ReringProject.Network;
using ReringProject.UI;
using System.Windows;
using System.Windows.Media;

namespace ReringProject.Sequence {
    public class BottomSequenceContext : SequenceContext {
        public double dAngle { get; set; }
        public bool bfinish { get; set; }
        public string ProcessName { get; set; }
        public EVisionResultType ResultInfo { get; set; }
        public System.Collections.Generic.Dictionary<int, BottomDieInfo> BottomDie = new System.Collections.Generic.Dictionary<int, BottomDieInfo>();
        public System.Windows.Rect ROI_Rect { get; set; }
        public double[] CenterOffsetXmmArray { get; set; } = new double[10];
        public double[] CenterOffsetYmmArray { get; set; } = new double[10];
        public double[] dAngleArray { get; set; } = new double[10];
        public double[] DieCenter_XArray { get; set; } = new double[10];
        public double[] DieCenter_YArray { get; set; } = new double[10];
        public int PickerNum { get; set; }
        public string strActionBaseName { get; set; }
        public double[] X_Picker = new double[10];
        public double[] Y_Picker = new double[10];
        public int CalSelector;
        public double Cal_Xbase { get; set; }
        public double Cal_Ybase { get; set; }
        public double ScreenCenter_X { get; set; }
        public double ScreenCenter_Y { get; set; }
        public BottomSequenceContext(BottomSequence source) : base(source) { }

        public override void Clear() {
            dAngle = 0;
            bfinish = false;
            ProcessName = "None";
            strActionBaseName = "None";
            ResultInfo = EVisionResultType.NG;
            BottomDie.Clear();
            PickerNum = 0;
            for (var i = 0; i < 10; i++) {
                CenterOffsetXmmArray[i] = 0;
                CenterOffsetYmmArray[i] = 0;
                dAngleArray[i] = 0;
                DieCenter_XArray[i] = 0;
                DieCenter_YArray[i] = 0;
                X_Picker[i] = 0;
                Y_Picker[i] = 0;
            }
            base.Clear();
        }

        public override void RenderResult(DrawingContext dc) {
            base.RenderResult(dc);
            if (bfinish) {
                var pen = new Pen(Brushes.Lime, 3);
                dc.DrawLine(pen, new System.Windows.Point(DieCenter_XArray[PickerNum] - 40, DieCenter_YArray[PickerNum]), new System.Windows.Point(DieCenter_XArray[PickerNum] + 40, DieCenter_YArray[PickerNum]));
                dc.DrawLine(pen, new System.Windows.Point(DieCenter_XArray[PickerNum], DieCenter_YArray[PickerNum] - 40), new System.Windows.Point(DieCenter_XArray[PickerNum], DieCenter_YArray[PickerNum] + 40));
            }
        }

        public override void CopyFrom(ActionContext actionContext) {
            base.CopyFrom(actionContext);
            Result = actionContext.Result;
            if (actionContext is BottomInspectionContext inspection) {
                BottomDie = inspection.BottomDie;
                ResultInfo = inspection.InspectResult;
                bfinish = inspection.bFinish;
                ProcessName = inspection.ProcessName;
                ROI_Rect = inspection.ROI_Rect;
                PickerNum = inspection.PickerNum;
                Cal_Xbase = inspection.Cal_XBase;
                Cal_Ybase = inspection.Cal_YBase;
                ScreenCenter_X = inspection.ScreenCenter_X;
                ScreenCenter_Y = inspection.ScreenCenter_Y;
                CalSelector = inspection.CalSelector;
                for (var i = 0; i < 10; i++) {
                    CenterOffsetXmmArray[i] = inspection.CenterOffsetXmmArray[i];
                    CenterOffsetYmmArray[i] = inspection.CenterOffsetYmmArray[i];
                    dAngleArray[i] = inspection.dAngleArray[i];
                    DieCenter_XArray[i] = inspection.DieCenter_XArray[i];
                    DieCenter_YArray[i] = inspection.DieCenter_YArray[i];
                    X_Picker[i] = inspection.X_Picker_CalValue[i];
                    Y_Picker[i] = inspection.Y_Picker_CalValue[i];
                }
            }
        }
    }

    //260526 hbk Phase 33 — InspectionSequence 로 마이그레이션됨 (D-05)
    [System.Obsolete("Phase 33 — InspectionSequence/Action_FAIMeasurement 로 마이그레이션됨", false)]
    public class BottomSequence : SequenceBase {
        private readonly DeviceHandler pDevs;
        private readonly BottomSequenceContext pMyContext;
        private readonly CameraMasterParam pMyParam;
        private readonly string DefaultCamera;
        private readonly string DefaultLight;

        public BottomSequence(ESequence seqID, string name, int algIndex, string defaultCamera, string defaultLight) : base(seqID, name) {
            pDevs = SystemHandler.Handle.Devices;
            Context = new BottomSequenceContext(this);
            pMyContext = Context as BottomSequenceContext;
            Param = new CameraMasterParam(this);
            pMyParam = Param as CameraMasterParam;
            DefaultLight = defaultLight;
            DefaultCamera = defaultCamera;
        }

        protected override void AddResponse() {
            if (RequestPacket == null) return;

            var responsePacket = new TestResultPacket {
                Target = RequestPacket.Sender,
                Site = RequestPacket.Site,
                InspectionType = RequestPacket.TestType,
            };

            if (responsePacket.InspectionType == (int)ETestType.Inspection) {
                var index = 0;
                foreach (var item in pMyContext.BottomDie) {
                    if (index >= TestResultPacket.MaxListCount) break;
                    responsePacket.visionResults[index].X = item.Value.CenterOffsetXmm;
                    responsePacket.visionResults[index].Y = item.Value.CenterOffsetYmm;
                    responsePacket.visionResults[index].Angle = item.Value.DieAngle;
                    responsePacket.visionResults[index].Result = item.Value.Judgment ? EVisionResultType.OK : (item.Value.newJudgment == 0 ? EVisionResultType.NotExist : EVisionResultType.NG);
                    index++;
                }
                for (; index < TestResultPacket.MaxListCount; index++) {
                    responsePacket.visionResults[index].Result = EVisionResultType.NotExist;
                }
            }

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
