using System;
using System.Collections.Generic;
using HalconDotNet;
using ReringProject.Define;
using ReringProject.Device;
using ReringProject.Halcon.Algorithms;
using ReringProject.Halcon.Models;
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
                    //260409 hbk Phase 3: FAIEdgeMeasurementService로 실제 에지 측정
                    if (ShotParam != null) {
                        var service = new FAIEdgeMeasurementService();
                        bool allPass = true;
                        var overlays = new List<EdgeInspectionOverlay>();
                        using (var image = ShotParam.GetImage()) {
                            if (image != null) {
                                //260409 hbk Phase 4: FindDatum before FAI measurement loop (D-02, D-07, D-17)
                                HTuple datumTransform = null;
                                if (ShotParam.Datum != null && ShotParam.Datum.IsConfigured) {
                                    var datumService = new DatumFindingService();
                                    string datumError;
                                    if (!datumService.TryFindDatum(image, ShotParam.Datum, out datumTransform, out datumError)) {
                                        //260409 hbk Phase 4: Datum fail -> all FAI NG (D-17)
                                        Logging.PrintLog((int)ELogType.Error, "Datum find failed: " + datumError);
                                        foreach (var fai in ShotParam.FAIList) {
                                            fai.ClearResult();
                                        }
                                        pMyContext.AllPass = false;
                                        pMyContext.MeasuredCount = ShotParam.FAIList.Count;
                                        Step = (int)EStep.End;
                                        break;
                                    }
                                    ShotParam.Datum.LastFindSucceeded = true;
                                    ShotParam.Datum.CurrentTransform = datumTransform;
                                }

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
