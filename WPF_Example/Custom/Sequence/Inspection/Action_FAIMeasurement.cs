using System;
using HalconDotNet;
using ReringProject.Define;
using ReringProject.Device;
using ReringProject.Utility;

namespace ReringProject.Sequence {

    public class FAIMeasurementContext : ActionContext {
        public bool AllPass { get; set; }
        public int MeasuredCount { get; set; }

        public FAIMeasurementContext(ActionBase source) : base(source) { }

        public override void Clear() {
            AllPass = false;
            MeasuredCount = 0;
            base.Clear();
        }
    }

    public class Action_FAIMeasurement : ActionBase {

        private enum EStep {
            Init,
            Grab,
            Measure,
            End
        }

        private FAIMeasurementContext pMyContext;
        private VirtualCamera pCamera;

        public ShotConfig ShotParam => Param as ShotConfig;

        public Action_FAIMeasurement(EAction id, string name, ShotConfig shotConfig) : base(id, name) {
            Context = new FAIMeasurementContext(this);
            pMyContext = Context as FAIMeasurementContext;
            Param = shotConfig;
        }

        public override void OnLoad() {
            if (ShotParam != null) {
                pCamera = SystemHandler.Handle.Devices[ShotParam.DeviceName];
            }
            base.OnLoad();
        }

        public override ActionContext Run() {
            switch ((EStep)Step) {
                case EStep.Init:
                    ShotParam?.ClearAllResults();
                    Step = (int)EStep.Grab;
                    break;

                case EStep.Grab:
                    if (ShotParam != null && !ShotParam.HasImage) {
                        HImage image = SystemHandler.Handle.Devices.GrabHalconImage(ShotParam);
                        if (image != null) {
                            ShotParam.SetImage(image);
                            pMyContext.ResultHalconImage?.Dispose();
                            pMyContext.ResultHalconImage = image.CopyImage();
                            image.Dispose();
                        }
                    }
                    Step = (int)EStep.Measure;
                    break;

                case EStep.Measure:
                    // Phase 8: Halcon edge measurement will be implemented here
                    // For now, mark all FAIs as measured with stub values
                    if (ShotParam != null) {
                        pMyContext.MeasuredCount = ShotParam.FAIList.Count;
                        pMyContext.AllPass = true;
                        foreach (var fai in ShotParam.FAIList) {
                            fai.SetResult(fai.NominalValue); // stub: nominal = pass
                            if (!fai.IsPass) pMyContext.AllPass = false;
                        }
                    }
                    Step = (int)EStep.End;
                    break;

                case EStep.End:
                    FinishAction(pMyContext.AllPass ? EContextResult.Pass : EContextResult.Fail);
                    break;
            }
            return Context;
        }
    }
}
