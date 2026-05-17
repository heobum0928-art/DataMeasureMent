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

        //260413 hbk Phase 6: DatumPhase 스텝 추가 — Fixture TryRunDatumPhase 실행 위치 (D-09)
        private enum EStep {
            Init,
            MoveZ,       //260409 hbk Phase 5: Z축 이동 스텝 (D-08)
            DatumPhase,  //260413 hbk Phase 6: Multi-Datum 실행 (D-09)
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
                    //260510 hbk Phase 21: BUF-02 channel #2 — sequence reset 트리거 (Run 사이클 진입 시 image buffer + FAI results dispose)
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
                    Step = (int)EStep.DatumPhase;
                    break;

                //260413 hbk Phase 6: Fixture Multi-Datum 실행 단계 (D-04, D-09, D-10)
                case EStep.DatumPhase: {
                    var parentSeq = ShotParam != null ? ShotParam.Parent as InspectionSequence : null;
                    if (parentSeq != null && parentSeq.DatumConfigs.Count > 0) {
                        HImage datumImage = GrabOrLoadDatumImage(parentSeq);
                        if (datumImage == null) {
                            Logging.PrintLog((int)ELogType.Error, "[FAIMeasurement] Datum image acquisition failed");
                            pMyContext.AllPass = false;
                            FinishAction(EContextResult.Error);
                            break;
                        }
                        try {
                            string datumError;
                            if (!parentSeq.TryRunDatumPhase(datumImage, out datumError)) {
                                Logging.PrintLog((int)ELogType.Error, "[FAIMeasurement] Datum failed: " + datumError);
                                pMyContext.AllPass = false;
                                FinishAction(EContextResult.Error);
                                break;
                            }
                        } finally {
                            datumImage.Dispose();
                        }
                    }
                    // DatumConfigs 비어있으면 → 무보정 pass-through (D-10)
                    Step = (int)EStep.Grab;
                    break;
                }

                case EStep.Grab:
                    if (ShotParam != null && !ShotParam.HasImage) {
                        HImage image = null;
                        //260511 hbk Phase 22 IMG-02 — ShotParam.SimulImagePath = InspectionImagePath 역할 (검사 사이클 마다 로드). 티칭 기준 이미지는 별도 DatumConfig.TeachingImagePath (셋업 시 1회, INI 보존) 사용 — 역할 분리. Simul 에서 두 경로 동일 파일 가능 (UAT Test 2).
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

                //260413 hbk Phase 6: FAI 루프 → Measurement 루프로 재설계 (D-09, D-10, D-20)
                //260422 hbk Phase 7: overlay 누적 + 판정 suffix 부여 (D-04 ~ D-08)
                case EStep.Measure: {
                    var parentSeq2 = ShotParam != null ? ShotParam.Parent as InspectionSequence : null;
                    bool allPass = true;
                    int measuredCount = 0;
                    var overlayAcc = new List<EdgeInspectionOverlay>(); //260422 hbk Phase 7: Shot 단위 overlay 누적 (D-04, D-05)
                    if (ShotParam != null) {
                        using (var image = ShotParam.GetImage()) {
                            if (image != null) {
                                foreach (var fai in ShotParam.FAIList) {
                                    bool faiAllPass = true;
                                    foreach (var meas in fai.Measurements) {
                                        HTuple transform;
                                        if (parentSeq2 == null || !parentSeq2.TryGetDatumTransform(meas.DatumRef, out transform)) {
                                            //260413 hbk Fixture 미존재 또는 미지정 DatumRef → identity fallback
                                            try {
                                                HOperatorSet.HomMat2dIdentity(out transform);
                                            } catch {
                                                transform = new HTuple();
                                            }
                                        }
                                        //260517 hbk — EdgeToLineDistance 는 datum 교점(절대 좌표) 기준 Y거리 측정.
                                        //  TryFindDatum 의 transform 은 part-drift 보정 델타라 좌표 변환에 못 씀 →
                                        //  DatumConfig.DetectedOrigin* (IntersectionLl 결과) 를 측정 객체에 직접 주입.
                                        //  EStep.DatumPhase 가 EStep.Measure 보다 먼저 실행되므로 DetectedOrigin* 는 채워져 있음.
                                        var etld = meas as EdgeToLineDistanceMeasurement; //260517 hbk
                                        if (etld != null) //260517 hbk
                                        {
                                            DatumConfig dc = null; //260517 hbk
                                            if (parentSeq2 != null && parentSeq2.DatumConfigs != null //260517 hbk
                                                && !string.IsNullOrEmpty(meas.DatumRef)) //260517 hbk
                                            {
                                                foreach (var d in parentSeq2.DatumConfigs) //260517 hbk
                                                {
                                                    if (d != null && d.DatumName == meas.DatumRef) { dc = d; break; } //260517 hbk
                                                }
                                            }
                                            if (dc != null) //260517 hbk
                                            {
                                                etld.DatumOriginRow = dc.DetectedOriginRow; //260517 hbk
                                                etld.DatumOriginCol = dc.DetectedOriginCol; //260517 hbk
                                                etld.DatumAngleRad  = dc.DetectedRefAngle; //260517 hbk
                                            }
                                            else //260517 hbk — DatumRef 미지정 또는 매칭 Datum 없음 → 미주입 (측정은 폴백 경로 사용)
                                            {
                                                etld.DatumOriginRow = 0.0; //260517 hbk
                                                etld.DatumOriginCol = 0.0; //260517 hbk
                                                etld.DatumAngleRad  = 0.0; //260517 hbk
                                            }
                                        }
                                        double resultValue;
                                        string measError;
                                        List<EdgeInspectionOverlay> measOverlays; //260422 hbk Phase 7: (D-01)
                                        bool ok = false;
                                        try {
                                            ok = meas.TryExecute(image, transform, fai.PixelResolutionX, out resultValue, out measError, out measOverlays); //260422 hbk Phase 7: 6-param (D-01)
                                        } catch (Exception ex) {
                                            ok = false;
                                            resultValue = 0;
                                            measError = ex.Message;
                                            measOverlays = null; //260422 hbk Phase 7: 예외 경로 null-safe (D-02)
                                        }
                                        if (ok) {
                                            meas.EvaluateJudgement(resultValue);
                                        } else {
                                            Logging.PrintLog((int)ELogType.Error, "[FAIMeasurement] Measurement '" + (meas.MeasurementName ?? meas.TypeName) + "' failed: " + (measError ?? ""));
                                            meas.ClearResult();
                                            meas.LastJudgement = false;
                                        }
                                        //260422 hbk Phase 7: FAI-Edge* overlay에 판정 suffix 부여 (D-06, D-07, D-08)
                                        if (measOverlays != null) {
                                            string suffix = meas.LastJudgement ? "-OK" : "-NG";
                                            foreach (var ov in measOverlays) {
                                                if (ov == null) continue;
                                                if (string.IsNullOrEmpty(ov.RoiId)) continue;
                                                if (ov.RoiId.StartsWith("FAI-Edge", StringComparison.OrdinalIgnoreCase)) {
                                                    ov.RoiId = ov.RoiId + suffix;
                                                }
                                                //260422 hbk FAI-DistLine 등은 suffix 미부여 — 청록 고정 (D-07)
                                            }
                                            overlayAcc.AddRange(measOverlays); //260422 hbk Phase 7: Shot 단위 누적 (D-04)
                                        }
                                        if (!meas.LastJudgement) {
                                            faiAllPass = false;
                                        }
                                        measuredCount++;
                                    }
                                    //260413 hbk FAIConfig legacy 필드(IsPass/MeasuredValue)에 대표 결과 집계 —
                                    // 첫 Measurement 결과를 사용해 UI/TCP 호환 유지
                                    if (fai.Measurements.Count > 0) {
                                        fai.IsPass = faiAllPass;
                                        fai.MeasuredValue = fai.Measurements[0].LastMeasuredValue;
                                    } else {
                                        fai.ClearResult();
                                    }
                                    if (!faiAllPass) allPass = false;
                                }
                            }
                        }
                    }
                    pMyContext.AllPass = allPass;
                    pMyContext.MeasuredCount = measuredCount;
                    pMyContext.InspectionOverlays = overlayAcc; //260422 hbk Phase 7: 초기화 라인 교체 — overlay 누적 결과 반영 (D-04, Gap I1)
                    Step = (int)EStep.End;
                    break;
                }

                case EStep.End:
                    FinishAction(pMyContext.AllPass ? EContextResult.Pass : EContextResult.Fail);
                    break;
            }
            return Context;
        }

        //260413 hbk Phase 6: Datum 이미지 취득 — Dedicated만 우선 지원 (D-07, D-08)
        // ReuseFromShot 모드는 향후 Plan 04 UI 작업과 함께 구현.
        private HImage GrabOrLoadDatumImage(InspectionSequence parentSeq) {
            if (ShotParam == null) return null;
            HImage image = null;
            //260512 hbk Phase 23 ALG-01 — D-04 TeachingImagePath 자동 로드 (Phase 22 carry-over). 비어있지 않으면 우선, 비어있으면 SimulImagePath 폴백.
            //260511 hbk Phase 22 IMG-02 — Datum 찾기 단계의 이미지 = InspectionImagePath (= ShotParam.SimulImagePath) 사용. TeachingImagePath (DatumConfig 보존) 는 본 메서드에서 미참조 — 재티칭/UI 셋업 경로에서만 참조 (Phase 23 carry-over 가능).
            string teachingPath = null; //260512 hbk Phase 23 ALG-01
            if (parentSeq != null && parentSeq.DatumConfigs != null && parentSeq.DatumConfigs.Count > 0) { //260512 hbk Phase 23 ALG-01
                teachingPath = parentSeq.DatumConfigs[0].TeachingImagePath;
            }
            #if SIMUL_MODE
            if (!string.IsNullOrEmpty(teachingPath) && File.Exists(teachingPath)) { //260512 hbk Phase 23 ALG-01 — TeachingImagePath 우선 (Pitfall 3 - 2-step 가드)
                try {
                    image = new HImage(teachingPath);
                } catch {
                    image = null;
                }
            }
            if (image == null && !string.IsNullOrEmpty(ShotParam.SimulImagePath) && File.Exists(ShotParam.SimulImagePath)) { //260512 hbk Phase 23 ALG-01 — SimulImagePath 폴백 (회귀 0)
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
            return image;
        }
    }
}
