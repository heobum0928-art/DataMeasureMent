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
                //260528 hbk Phase 37 D-37-02/04/05 — DatumConfigs[0] 단일 분기 제거. DatumConfigs 전체를 per-datum loop 하여 각자 자기 이미지로 검출, _datumTransforms 누적. datum 부분 실패는 skip+log (lenient, abort 없음 — D-37-03).
                case EStep.DatumPhase: {
                    var parentSeq = ShotParam != null ? ShotParam.Parent as InspectionSequence : null; //260528 hbk Phase 37
                    if (parentSeq != null && parentSeq.DatumConfigs.Count > 0) { //260528 hbk Phase 37
                        parentSeq.ClearDatumTransforms(); //260528 hbk Phase 37 D-37-05 — loop 전 1회 초기화
                        foreach (var datum in parentSeq.DatumConfigs) { //260528 hbk Phase 37 D-37-04/05 — per-datum loop, mixed algorithm 허용
                            if (datum == null) continue; //260528 hbk Phase 37
                            if (datum.AlgorithmTypeEnum == EDatumAlgorithm.VerticalTwoHorizontalDualImage) { //260528 hbk Phase 37 D-37-04 — datum별 판단
                                HImage imgH = null, imgV = null; //260528 hbk Phase 37
                                try {
                                    if (!TryGrabOrLoadDualDatumImages(datum, out imgH, out imgV)) { //260528 hbk Phase 37 D-37-02 — per-datum
                                        Logging.PrintLog((int)ELogType.Error, "[FAIMeasurement] Datum '" + (datum.DatumName ?? "") + "' DualImage 취득 실패 (skip)"); //260528 hbk Phase 37 D-37-03
                                        datum.LastFindSucceeded = false; //260529 hbk Phase 39 hotfix CO-39-01 — 이미지 취득 실패 시 RenderDatumOverlay DETECT FAIL 라벨 분기 조건 충족 (TryRunSingleDatum 미호출 경로)
                                        parentSeq.MarkDatumFailed(datum.DatumName); //260529 hbk Phase 39 WF-01 D-01 — per-FAI gate 신호 기록
                                        continue; //260528 hbk Phase 37 D-37-03 — datum skip, abort 안 함
                                    }
                                    string derr; //260528 hbk Phase 37
                                    if (!parentSeq.TryRunSingleDatum(datum, imgH, imgV, out derr)) { //260528 hbk Phase 37 D-37-05
                                        Logging.PrintLog((int)ELogType.Error, "[FAIMeasurement] Datum '" + (datum.DatumName ?? "") + "' 검출 실패 (skip): " + (derr ?? "")); //260528 hbk Phase 37 D-37-03
                                        parentSeq.MarkDatumFailed(datum.DatumName); //260529 hbk Phase 39 WF-01 D-01 — per-FAI gate 신호 기록
                                    }
                                } finally {
                                    if (imgH != null) { try { imgH.Dispose(); } catch { } } //260528 hbk Phase 37
                                    if (imgV != null) { try { imgV.Dispose(); } catch { } } //260528 hbk Phase 37
                                }
                            } else { //260528 hbk Phase 37 — 1-image datum
                                HImage img = GrabOrLoadDatumImage(datum); //260528 hbk Phase 37 D-37-02 — per-datum 오버로드
                                if (img == null) { //260528 hbk Phase 37
                                    Logging.PrintLog((int)ELogType.Error, "[FAIMeasurement] Datum '" + (datum.DatumName ?? "") + "' 이미지 취득 실패 (skip)"); //260528 hbk Phase 37 D-37-03
                                    datum.LastFindSucceeded = false; //260529 hbk Phase 39 hotfix CO-39-01 — 이미지 취득 실패 시 RenderDatumOverlay DETECT FAIL 라벨 분기 조건 충족 (TryRunSingleDatum 미호출 경로)
                                    parentSeq.MarkDatumFailed(datum.DatumName); //260529 hbk Phase 39 WF-01 D-01 — per-FAI gate 신호 기록
                                    continue; //260528 hbk Phase 37
                                }
                                try {
                                    string derr; //260528 hbk Phase 37
                                    if (!parentSeq.TryRunSingleDatum(datum, img, null, out derr)) { //260528 hbk Phase 37 D-37-05
                                        Logging.PrintLog((int)ELogType.Error, "[FAIMeasurement] Datum '" + (datum.DatumName ?? "") + "' 검출 실패 (skip): " + (derr ?? "")); //260528 hbk Phase 37 D-37-03
                                        parentSeq.MarkDatumFailed(datum.DatumName); //260529 hbk Phase 39 WF-01 D-01 — per-FAI gate 신호 기록
                                    }
                                } finally {
                                    img.Dispose(); //260528 hbk Phase 37
                                }
                            }
                        }
                    }
                    // DatumConfigs 비어있으면 무보정 pass-through (D-10) — abort 없음 (D-37-03 lenient)
                    Step = (int)EStep.Grab; //260528 hbk Phase 37 — datum 부분 실패해도 측정 진행
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
                                        //260529 hbk Phase 39 WF-01 D-01 — per-FAI gate: 해당 datum 이 검출 실패했으면 측정 skip, NG 누적, 다음 meas 진행.
                                        //  L119 Step=Grab 변경 안 함 (Phase 37 D-37-03 lenient 유지). 본 게이트는 Measure 루프 안에서만 동작.
                                        //  빈 DatumRef (무보정) 또는 성공 datum 참조는 IsDatumFailed=false → 기존 identity fallback / transform 경로 진행.
                                        if (parentSeq2 != null && parentSeq2.IsDatumFailed(meas.DatumRef)) //260529 hbk Phase 39 WF-01 D-01
                                        {
                                            meas.ClearResult(); //260529 hbk Phase 39 WF-01 D-01 — runtime 결과 클리어
                                            meas.LastSkipReason = "DATUM_FAIL"; //260529 hbk Phase 39 WF-01 D-02 — UI 'DETECT FAIL' 라벨 + Excel export 분기 신호
                                            meas.LastJudgement = false; //260529 hbk Phase 39 WF-01 D-01 — faiAllPass=false 누적 (skip 도 NG 강도)
                                            Logging.PrintLog((int)ELogType.Error, "[FAIMeasurement] Measurement '" + (meas.MeasurementName ?? meas.TypeName) + "' skipped — datum '" + (meas.DatumRef ?? "") + "' 검출 실패 (D-01)"); //260529 hbk Phase 39 WF-01 D-01
                                            faiAllPass = false; //260529 hbk Phase 39 WF-01 D-01 — 외부 if(!meas.LastJudgement) 와 의미 동일이지만 명시
                                            measuredCount++; //260529 hbk Phase 39 WF-01 D-01 — measuredCount 도 증가 (시도 회수 통계)
                                            continue; //260529 hbk Phase 39 WF-01 D-01 — 다음 measurement 진행 (TryExecute 호출 안 함)
                                        }
                                        HTuple transform;
                                        if (parentSeq2 == null || !parentSeq2.TryGetDatumTransform(meas.DatumRef, out transform)) {
                                            //260413 hbk Fixture 미존재 또는 미지정 DatumRef → identity fallback
                                            try {
                                                HOperatorSet.HomMat2dIdentity(out transform);
                                            } catch {
                                                transform = new HTuple();
                                            }
                                        }
                                        //260519 hbk Phase 31 D-03 removed — EdgeToLineDistanceMeasurement 하드코딩 제거
                                        //260519 hbk Phase 31 D-03 — IDatumOriginConsumer 일반화 (기존 EdgeToLineDistanceMeasurement 하드코딩 제거)
                                        //  EStep.DatumPhase 가 EStep.Measure 보다 먼저 실행되므로 DetectedOrigin* 는 채워져 있음.
                                        var consumer = meas as IDatumOriginConsumer; //260519 hbk Phase 31 D-03
                                        if (consumer != null) //260519 hbk Phase 31 D-03
                                        {
                                            DatumConfig dc = null; //260519 hbk Phase 31 D-03
                                            if (parentSeq2 != null && parentSeq2.DatumConfigs != null //260519 hbk Phase 31 D-03
                                                && !string.IsNullOrEmpty(meas.DatumRef)) //260519 hbk Phase 31 D-03
                                            {
                                                foreach (var d in parentSeq2.DatumConfigs) //260519 hbk Phase 31 D-03
                                                {
                                                    if (d != null && d.DatumName == meas.DatumRef) { dc = d; break; } //260519 hbk Phase 31 D-03
                                                }
                                            }
                                            if (dc != null) //260519 hbk Phase 31 D-03
                                            {
                                                consumer.DatumOriginRow = dc.DetectedOriginRow; //260519 hbk Phase 31 D-03
                                                consumer.DatumOriginCol = dc.DetectedOriginCol; //260519 hbk Phase 31 D-03
                                                consumer.DatumAngleRad  = dc.DetectedRefAngle;  //260519 hbk Phase 31 D-03
                                                consumer.DatumAngle2Rad = dc.DetectedRefAngle2; //260519 hbk Phase 31 hotfix#3 — 수직 기준선 각도
                                                consumer.DatumDetectedCircleRow = dc.DetectedCircleRow; //260521 hbk Phase 32 — E2 CompoundAngle 원중심 주입
                                                consumer.DatumDetectedCircleCol = dc.DetectedCircleCol; //260521 hbk Phase 32
                                            }
                                            else //260519 hbk Phase 31 D-03 — DatumRef 미지정 또는 매칭 Datum 없음 → 미주입
                                            {
                                                consumer.DatumOriginRow = 0.0; //260519 hbk Phase 31 D-03
                                                consumer.DatumOriginCol = 0.0; //260519 hbk Phase 31 D-03
                                                consumer.DatumAngleRad  = 0.0; //260519 hbk Phase 31 D-03
                                                consumer.DatumAngle2Rad = 0.0; //260519 hbk Phase 31 hotfix#3
                                                consumer.DatumDetectedCircleRow = 0.0; //260521 hbk Phase 32
                                                consumer.DatumDetectedCircleCol = 0.0; //260521 hbk Phase 32
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
                                        //260529 hbk Phase 39 WF-01 D-02 — fai 하위 measurement 중 1건이라도 DATUM_FAIL 이면 fai 도 datum-skip 마크.
                                        //  Plan 02 AddResponse 가 anyDatumSkip 누적용으로 사용 (cycle = NotExist 분기). System.Linq 도입 회피 — for-loop.
                                        bool wasSkip = false; //260529 hbk Phase 39 WF-01 D-02
                                        foreach (var m in fai.Measurements) //260529 hbk Phase 39 WF-01 D-02
                                        {
                                            if (m != null && m.LastSkipReason == "DATUM_FAIL") { wasSkip = true; break; } //260529 hbk Phase 39 WF-01 D-02
                                        }
                                        fai.WasDatumSkipped = wasSkip; //260529 hbk Phase 39 WF-01 D-02
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

        //260528 hbk Phase 37 D-37-02 — per-datum 1-image 로드 (TeachingImagePath → SimulImagePath 폴백 → grab). datum 인자 명시.
        private HImage GrabOrLoadDatumImage(DatumConfig datum) {
            if (ShotParam == null) return null; //260528 hbk Phase 37
            HImage image = null; //260528 hbk Phase 37
            string teachingPath = (datum != null) ? datum.TeachingImagePath : null; //260528 hbk Phase 37 D-37-02
            #if SIMUL_MODE
            if (!string.IsNullOrEmpty(teachingPath) && File.Exists(teachingPath)) { //260528 hbk Phase 37
                try { image = new HImage(teachingPath); } catch { image = null; } //260528 hbk Phase 37
            }
            if (image == null && !string.IsNullOrEmpty(ShotParam.SimulImagePath) && File.Exists(ShotParam.SimulImagePath)) { //260528 hbk Phase 37 — 회귀 0 폴백
                try { image = new HImage(ShotParam.SimulImagePath); } catch { image = null; } //260528 hbk Phase 37
            }
            if (image == null) { image = SystemHandler.Handle.Devices.GrabHalconImage(ShotParam); } //260528 hbk Phase 37
            #else
            image = SystemHandler.Handle.Devices.GrabHalconImage(ShotParam); //260528 hbk Phase 37
            #endif
            return image; //260528 hbk Phase 37
        }

        //260527 hbk Phase 34 D-34-13 — DualImage 변형용 두 이미지 동시 로드.
        //260528 hbk Phase 37 D-37-02 — DatumConfigs[0] 한정 제거, datum 인자 명시 (per-datum 로드).
        //  imageHorizontal: datum.TeachingImagePath 에서 로드 (가로축 ROI 검출용)
        //  imageVertical:   datum.TeachingImagePath_Vertical 에서 로드 (세로축 ROI 검출용)
        //  빈 경로 또는 파일 없음 / HImage 생성 실패 시 false + 로그.
        private bool TryGrabOrLoadDualDatumImages(DatumConfig datum, out HImage imageHorizontal, out HImage imageVertical) { //260528 hbk Phase 37 D-37-02 — DatumConfigs[0] 한정 제거, datum 인자
            imageHorizontal = null; //260527 hbk Phase 34
            imageVertical = null; //260527 hbk Phase 34
            if (datum == null) { //260528 hbk Phase 37 D-37-02 — datum null 가드
                Logging.PrintErrLog((int)ELogType.Error, "[Datum] DualImage: datum 이 null 입니다."); //260528 hbk Phase 37 D-37-02
                return false; //260528 hbk Phase 37 D-37-02
            }
            string pathH = datum.TeachingImagePath; //260528 hbk Phase 37 D-37-02 — 인자 datum 에서 읽음
            string pathV = datum.TeachingImagePath_Vertical; //260528 hbk Phase 37 D-37-02 — 인자 datum 에서 읽음

            if (string.IsNullOrEmpty(pathH) || !File.Exists(pathH)) { //260527 hbk Phase 34 D-34-09
                Logging.PrintErrLog((int)ELogType.Error, "[Datum] 가로축 티칭 이미지 경로가 비어 있거나 파일이 없습니다 (DualImage)."); //260527 hbk Phase 34 D-34-10
                return false; //260527 hbk Phase 34
            }
            if (string.IsNullOrEmpty(pathV) || !File.Exists(pathV)) { //260527 hbk Phase 34 D-34-09
                Logging.PrintErrLog((int)ELogType.Error, "[Datum] 세로축 티칭 이미지 경로가 비어 있거나 파일이 없습니다 (DualImage)."); //260527 hbk Phase 34 D-34-10
                return false; //260527 hbk Phase 34
            }

            try { imageHorizontal = new HImage(pathH); } catch (Exception ex) { Logging.PrintErrLog((int)ELogType.Error, "[Datum] 가로축 이미지 로드 실패: " + ex.Message); imageHorizontal = null; } //260527 hbk Phase 34
            try { imageVertical = new HImage(pathV); } catch (Exception ex) { Logging.PrintErrLog((int)ELogType.Error, "[Datum] 세로축 이미지 로드 실패: " + ex.Message); imageVertical = null; } //260527 hbk Phase 34

            if (imageHorizontal == null || imageVertical == null) { //260527 hbk Phase 34
                if (imageHorizontal != null) { try { imageHorizontal.Dispose(); } catch { } } //260527 hbk Phase 34
                if (imageVertical != null) { try { imageVertical.Dispose(); } catch { } } //260527 hbk Phase 34
                imageHorizontal = null; //260527 hbk Phase 34
                imageVertical = null; //260527 hbk Phase 34
                return false; //260527 hbk Phase 34
            }
            return true; //260527 hbk Phase 34
        }
    }
}
