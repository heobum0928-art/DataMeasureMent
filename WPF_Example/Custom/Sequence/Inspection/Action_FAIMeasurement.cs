using System;
using System.Collections.Generic;
using System.IO;
using HalconDotNet;
using ReringProject.Define;
using ReringProject.Device;
using ReringProject.Halcon.Algorithms;
using ReringProject.Halcon.Models;
using ReringProject.Setting; //260409 hbk Phase 4: ELogType for Datum error logging
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
            MoveZ,    //260409 hbk Phase 5: Z축 이동 스텝 (D-08)
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
                    Step = (int)EStep.MoveZ;
                    break;

                //260409 hbk Phase 5: Z축 이동 + SIMUL 이미지 준비 (D-08, D-10, D-11)
                case EStep.MoveZ:
                    #if SIMUL_MODE
                    // SIMUL: Z축 이동 건너뜀, DelayMs 무시
                    #else
                    // 실 장비: IAxisController 구현 후 연동 예정
                    if (ShotParam != null && ShotParam.DelayMs > 0) {
                        System.Threading.Thread.Sleep(ShotParam.DelayMs);
                    }
                    #endif
                    Step = (int)EStep.Grab;
                    break;

                case EStep.Grab:
                    if (ShotParam != null && !ShotParam.HasImage) {
                        HImage image = null;
                        //260409 hbk Phase 5: SimulImagePath 이미지 로드 (D-10)
                        #if SIMUL_MODE
                        if (!string.IsNullOrEmpty(ShotParam.SimulImagePath) && File.Exists(ShotParam.SimulImagePath)) {
                            try {
                                image = new HImage(ShotParam.SimulImagePath);
                            } catch {
                                image = null;
                            }
                        }
                        if (image == null) {
                            image = SystemHandler.Handle.Devices.GrabHalconImage(ShotParam);
                        }
                        #else
                        image = SystemHandler.Handle.Devices.GrabHalconImage(ShotParam);
                        #endif
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
                    //260409 hbk Phase 3: FAIEdgeMeasurementService로 실제 에지 측정
                    if (ShotParam != null) {
                        var service = new FAIEdgeMeasurementService();
                        bool allPass = true;
                        var overlays = new List<EdgeInspectionOverlay>();
                        //260409 hbk Phase 4: Datum fail flag for early exit (D-17)
                        bool datumFailed = false;
                        using (var image = ShotParam.GetImage()) {
                            if (image != null) {
                                //260413 hbk Phase 6: Datum 실행은 InspectionSequence.TryRunDatumPhase로 이전 (D-04, D-09).
                                // Plan 03에서 부모 Fixture의 Datum transform dictionary를 주입하도록 재설계한다.
                                // 현재는 identity transform fallback으로 기존 측정 흐름 유지.
                                HTuple datumTransform;
                                HOperatorSet.HomMat2dIdentity(out datumTransform);

                                if (!datumFailed) {
                                foreach (var fai in ShotParam.FAIList) {
                                    FAIEdgeMeasurementResult r;
                                    //260409 hbk Phase 4: pass datumTransform to TryMeasure (D-07, D-08)
                                    if (service.TryMeasure(image, fai, datumTransform, out r)) {
                                        fai.SetResult(r.DistanceMm);
                                        //260409 hbk Phase 3: overlay RoiId에 OK/NG 접미사 추가 (HalconDisplayService 색상 분기용)
                                        string suffix = fai.IsPass ? "-OK" : "-NG";
                                        foreach (var ov in r.Overlays) {
                                            if (ov.RoiId != null && ov.RoiId.StartsWith("FAI-Edge"))
                                                ov.RoiId = ov.RoiId + suffix;
                                        }
                                        overlays.AddRange(r.Overlays);
                                    } else {
                                        fai.ClearResult();
                                    }
                                    if (!fai.IsPass) allPass = false;
                                }
                                } //260409 hbk end if (!datumFailed)
                            }
                        }
                        pMyContext.AllPass = allPass;
                        pMyContext.MeasuredCount = ShotParam.FAIList.Count;
                        pMyContext.InspectionOverlays = overlays;
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
