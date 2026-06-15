using System;
using System.Collections.Generic;
using System.IO;
using HalconDotNet;
using ReringProject.Define;
using ReringProject.Device;
using ReringProject.Halcon.Algorithms;
using ReringProject.Halcon.Display;
using ReringProject.Halcon.Models;
using ReringProject.Setting;
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
            MoveZ,
            DatumPhase,
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
                    // Run 사이클 진입 시 image buffer + FAI results dispose
                    if (ShotParam != null) ShotParam.ClearAllResults();
                    Step = (int)EStep.MoveZ;
                    break;

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

                // DatumConfigs 전체를 per-datum loop 하여 각자 자기 이미지로 검출, _datumTransforms 누적.
                // datum 부분 실패는 skip+log (lenient, abort 없음).
                case EStep.DatumPhase: {
                    InspectionSequence parentSeq;
                    if (ShotParam != null) parentSeq = ShotParam.Parent as InspectionSequence;
                    else parentSeq = null;
                    if (parentSeq != null && parentSeq.DatumConfigs.Count > 0) {
                        parentSeq.ClearDatumTransforms();
                        foreach (var datum in parentSeq.DatumConfigs) {
                            if (datum == null) continue;
                            if (datum.AlgorithmTypeEnum == EDatumAlgorithm.VerticalTwoHorizontalDualImage) {
                                HImage imgH = null, imgV = null;
                                try {
                                    if (!TryGrabOrLoadDualDatumImages(datum, out imgH, out imgV)) {
                                        string datumName = datum.DatumName;
                                        if (datumName == null) datumName = "";
                                        Logging.PrintLog((int)ELogType.Error, "[FAIMeasurement] Datum '" + datumName + "' DualImage 취득 실패 (skip)");
                                        // 이미지 취득 실패 시 RenderDatumOverlay DETECT FAIL 라벨 분기 조건 충족 (TryRunSingleDatum 미호출 경로)
                                        datum.LastFindSucceeded = false;
                                        // 티칭 여부 무관 라벨 신호
                                        datum.RuntimeDetectFailed = true;
                                        // per-FAI gate 신호 기록
                                        parentSeq.MarkDatumFailed(datum.DatumName);
                                        continue; // datum skip, abort 안 함
                                    }
                                    string derr;
                                    if (!parentSeq.TryRunSingleDatum(datum, imgH, imgV, out derr)) {
                                        string datumName = datum.DatumName;
                                        if (datumName == null) datumName = "";
                                        string derrStr = derr;
                                        if (derrStr == null) derrStr = "";
                                        Logging.PrintLog((int)ELogType.Error, "[FAIMeasurement] Datum '" + datumName + "' 검출 실패 (skip): " + derrStr);
                                        datum.RuntimeDetectFailed = true;
                                        parentSeq.MarkDatumFailed(datum.DatumName);
                                    }
                                } finally {
                                    if (imgH != null) { try { imgH.Dispose(); } catch { } }
                                    if (imgV != null) { try { imgV.Dispose(); } catch { } }
                                }
                            } else { // 1-image datum
                                HImage img = GrabOrLoadDatumImage(datum);
                                if (img == null) {
                                    string datumName = datum.DatumName;
                                    if (datumName == null) datumName = "";
                                    Logging.PrintLog((int)ELogType.Error, "[FAIMeasurement] Datum '" + datumName + "' 이미지 취득 실패 (skip)");
                                    datum.LastFindSucceeded = false;
                                    datum.RuntimeDetectFailed = true;
                                    parentSeq.MarkDatumFailed(datum.DatumName);
                                    continue;
                                }
                                try {
                                    string derr;
                                    if (!parentSeq.TryRunSingleDatum(datum, img, null, out derr)) {
                                        string datumName = datum.DatumName;
                                        if (datumName == null) datumName = "";
                                        string derrStr = derr;
                                        if (derrStr == null) derrStr = "";
                                        Logging.PrintLog((int)ELogType.Error, "[FAIMeasurement] Datum '" + datumName + "' 검출 실패 (skip): " + derrStr);
                                        datum.RuntimeDetectFailed = true;
                                        parentSeq.MarkDatumFailed(datum.DatumName);
                                    }
                                } finally {
                                    img.Dispose();
                                }
                            }
                        }
                    }
                    // DatumConfigs 비어있으면 무보정 pass-through — abort 없음 (lenient)
                    Step = (int)EStep.Grab; // datum 부분 실패해도 측정 진행
                    break;
                }

                case EStep.Grab:
                    if (ShotParam != null && !ShotParam.HasImage) {
                        HImage image = null;
                        // ShotParam.SimulImagePath = InspectionImagePath 역할 (검사 사이클 마다 로드). 티칭 기준 이미지는 별도 DatumConfig.TeachingImagePath (셋업 시 1회, INI 보존) 사용 — 역할 분리. Simul 에서 두 경로 동일 파일 가능.
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
                            if (pMyContext.ResultHalconImage != null) pMyContext.ResultHalconImage.Dispose();
                            pMyContext.ResultHalconImage = image.CopyImage();
                            image.Dispose();
                        }
                    }
                    Step = (int)EStep.Measure;
                    break;

                case EStep.Measure: {
                    InspectionSequence parentSeq2;
                    if (ShotParam != null) parentSeq2 = ShotParam.Parent as InspectionSequence;
                    else parentSeq2 = null;
                    bool allPass = true;
                    int measuredCount = 0;
                    var overlayAcc = new List<EdgeInspectionOverlay>(); // Shot 단위 overlay 누적
                    if (ShotParam != null) {
                        using (var image = ShotParam.GetImage()) {
                            if (image != null) {
                                // Shot당 1회 복사 공유(refcount). 검사 스레드의 FAI별 대용량 CopyImage 제거(throughput).
                                // 한 Shot 의 모든 FAI origin/capture 요청이 이 1개 복사본을 공유. capSaver 없으면 복사 자체 생략.
                                var capSaver = SystemHandler.Handle.CaptureImageSaver;
                                SharedHImage sharedSrc = null;
                                if (capSaver != null) { try { sharedSrc = new SharedHImage(image.CopyImage()); } catch { sharedSrc = null; } }
                                // datum 검출 오버레이 스냅샷(시퀀스 단위, 전 FAI 공유). 값만 추출해 워커 async race 차단.
                                List<DatumCaptureOverlay> datumSnapshot = BuildDatumCaptureSnapshot(parentSeq2);
                                double pixRes = ShotParam != null ? ShotParam.PixelResolution : 1.0; //260615 hbk Phase 42 D-01 Shot 단일소스
                                try {
                                foreach (var fai in ShotParam.FAIList) {
                                    bool faiAllPass = true;
                                    var faiOverlays = new List<EdgeInspectionOverlay>(); // per-FAI overlay 누적 (LastOverlays write-back 용, 노드 클릭 재현)
                                    foreach (var meas in fai.Measurements) {
                                        // per-FAI gate: 해당 datum 이 검출 실패했으면 측정 skip, NG 누적, 다음 meas 진행.
                                        // Step=Grab 변경 안 함 (lenient 유지). 본 게이트는 Measure 루프 안에서만 동작.
                                        // 빈 DatumRef (무보정) 또는 성공 datum 참조는 IsDatumFailed=false → 기존 identity fallback / transform 경로 진행.
                                        if (parentSeq2 != null && parentSeq2.IsDatumFailed(meas.DatumRef))
                                        {
                                            meas.ClearResult();
                                            meas.LastSkipReason = "DATUM_FAIL"; // UI 'DETECT FAIL' 라벨 + Excel export 분기 신호
                                            meas.LastJudgement = false; // skip 도 NG 강도
                                            string measName = meas.MeasurementName;
                                            if (measName == null) measName = meas.TypeName;
                                            string datumRef = meas.DatumRef;
                                            if (datumRef == null) datumRef = "";
                                            Logging.PrintLog((int)ELogType.Error, "[FAIMeasurement] Measurement '" + measName + "' skipped — datum '" + datumRef + "' 검출 실패 (D-01)");
                                            faiAllPass = false;
                                            measuredCount++; // 시도 회수 통계
                                            continue; // 다음 measurement 진행 (TryExecute 호출 안 함)
                                        }
                                        HTuple transform;
                                        if (parentSeq2 == null || !parentSeq2.TryGetDatumTransform(meas.DatumRef, out transform)) {
                                            // Fixture 미존재 또는 미지정 DatumRef → identity fallback
                                            try {
                                                HOperatorSet.HomMat2dIdentity(out transform);
                                            } catch {
                                                transform = new HTuple();
                                            }
                                        }
                                        // IDatumOriginConsumer 일반화. EStep.DatumPhase 가 EStep.Measure 보다 먼저 실행되므로 DetectedOrigin* 는 채워져 있음.
                                        var consumer = meas as IDatumOriginConsumer;
                                        if (consumer != null)
                                        {
                                            DatumConfig dc = null;
                                            if (parentSeq2 != null && parentSeq2.DatumConfigs != null
                                                && !string.IsNullOrEmpty(meas.DatumRef))
                                            {
                                                foreach (var d in parentSeq2.DatumConfigs)
                                                {
                                                    if (d != null && d.DatumName == meas.DatumRef) { dc = d; break; }
                                                }
                                            }
                                            if (dc != null)
                                            {
                                                consumer.DatumOriginRow = dc.DetectedOriginRow;
                                                consumer.DatumOriginCol = dc.DetectedOriginCol;
                                                consumer.DatumAngleRad  = dc.DetectedRefAngle;
                                                consumer.DatumAngle2Rad = dc.DetectedRefAngle2; // 수직 기준선 각도
                                                consumer.DatumDetectedCircleRow = dc.DetectedCircleRow; // CompoundAngle 원중심 주입
                                                consumer.DatumDetectedCircleCol = dc.DetectedCircleCol;
                                            }
                                            else // DatumRef 미지정 또는 매칭 Datum 없음 → 미주입
                                            {
                                                consumer.DatumOriginRow = 0.0;
                                                consumer.DatumOriginCol = 0.0;
                                                consumer.DatumAngleRad  = 0.0;
                                                consumer.DatumAngle2Rad = 0.0;
                                                consumer.DatumDetectedCircleRow = 0.0;
                                                consumer.DatumDetectedCircleCol = 0.0;
                                            }
                                        }
                                        double resultValue;
                                        string measError;
                                        List<EdgeInspectionOverlay> measOverlays;
                                        bool ok = false;
                                        // DualImage 타입 분기: 양 이미지 별도 로드 → RuntimeImageA/B 주입 → TryExecute → dispose
                                        if (meas is DualImageEdgeDistanceMeasurement dualMeas) {
                                            HImage imgA = null, imgB = null;
                                            try {
                                                if (TryGrabOrLoadFaiDualImages(meas, out imgA, out imgB)) {
                                                    dualMeas.RuntimeImageA = imgA; // transient property, TryExecute 가 image 인자 무시
                                                    dualMeas.RuntimeImageB = imgB;
                                                    try {
                                                        ok = meas.TryExecute(image, transform, pixRes, out resultValue, out measError, out measOverlays); //260615 hbk Phase 42 D-01
                                                    } catch (Exception ex) {
                                                        ok = false; resultValue = 0; measError = ex.Message; measOverlays = null;
                                                    }
                                                } else {
                                                    ok = false; resultValue = 0; measError = "DualImage 이미지 로드 실패"; measOverlays = null;
                                                }
                                            } finally {
                                                if (imgA != null) { try { imgA.Dispose(); } catch { } }
                                                if (imgB != null) { try { imgB.Dispose(); } catch { } }
                                                dualMeas.RuntimeImageA = null;
                                                dualMeas.RuntimeImageB = null;
                                            }
                                        } else { // 기존 1-image 경로
                                            try {
                                                ok = meas.TryExecute(image, transform, pixRes, out resultValue, out measError, out measOverlays); //260615 hbk Phase 42 D-01
                                            } catch (Exception ex) {
                                                ok = false;
                                                resultValue = 0;
                                                measError = ex.Message;
                                                measOverlays = null; // 예외 경로 null-safe
                                            }
                                        }
                                        if (ok) {
                                            meas.EvaluateJudgement(resultValue);
                                        } else {
                                            string measName = meas.MeasurementName;
                                            if (measName == null) measName = meas.TypeName;
                                            string measErrorStr = measError;
                                            if (measErrorStr == null) measErrorStr = "";
                                            Logging.PrintLog((int)ELogType.Error, "[FAIMeasurement] Measurement '" + measName + "' failed: " + measErrorStr);
                                            meas.ClearResult();
                                            meas.LastJudgement = false;
                                        }
                                        // FAI-Edge* overlay에 판정 suffix 부여
                                        if (measOverlays != null) {
                                            string suffix;
                                            if (meas.LastJudgement) suffix = "-OK";
                                            else suffix = "-NG";
                                            foreach (var ov in measOverlays) {
                                                if (ov == null) continue;
                                                if (string.IsNullOrEmpty(ov.RoiId)) continue;
                                                if (ov.RoiId.StartsWith("FAI-Edge", StringComparison.OrdinalIgnoreCase)) {
                                                    ov.RoiId = ov.RoiId + suffix;
                                                }
                                                // FAI-DistLine 등은 suffix 미부여 — 청록 고정
                                            }
                                            overlayAcc.AddRange(measOverlays); // Shot 단위 누적
                                            faiOverlays.AddRange(measOverlays); // per-FAI 누적 (노드 클릭 재현용)
                                        }
                                        if (!meas.LastJudgement) {
                                            faiAllPass = false;
                                        }
                                        measuredCount++;
                                    }
                                    // FAIConfig legacy 필드(IsPass/MeasuredValue)에 대표 결과 집계 —
                                    // 첫 Measurement 결과를 사용해 UI/TCP 호환 유지
                                    if (fai.Measurements.Count > 0) {
                                        fai.IsPass = faiAllPass;
                                        fai.MeasuredValue = fai.Measurements[0].LastMeasuredValue;
                                        // fai 하위 measurement 중 1건이라도 DATUM_FAIL 이면 fai 도 datum-skip 마크.
                                        // AddResponse 가 anyDatumSkip 누적용으로 사용 (cycle = NotExist 분기). System.Linq 도입 회피 — for-loop.
                                        bool wasSkip = false;
                                        foreach (var m in fai.Measurements)
                                        {
                                            if (m != null && m.LastSkipReason == "DATUM_FAIL") { wasSkip = true; break; }
                                        }
                                        fai.WasDatumSkipped = wasSkip;
                                        fai.LastOverlays = faiOverlays; // per-FAI overlay 저장 (노드 클릭 시 재현)
                                        // FAI별 origin/capture 캡쳐 enqueue + 파일명 write-back (오버레이+소스 이미지 확정 시점)
                                        string ownerSeqName;
                                        if (ShotParam != null) ownerSeqName = ShotParam.OwnerSequenceName;
                                        else ownerSeqName = "";
                                        QueueFaiCapture(fai, sharedSrc, faiOverlays, datumSnapshot, ownerSeqName);
                                    } else {
                                        fai.ClearResult();
                                        if (fai.LastOverlays != null) fai.LastOverlays.Clear(); // Measurements 0 케이스 명시적 클리어
                                    }
                                    if (!faiAllPass) allPass = false;
                                }
                                } finally { // 검사 루프 소유 ref 1 해제(워커 요청들의 ref 와 독립). 마지막 Release 시 공유 이미지 dispose.
                                    if (sharedSrc != null) sharedSrc.Release();
                                }
                            }
                        }
                    }
                    pMyContext.AllPass = allPass;
                    pMyContext.MeasuredCount = measuredCount;
                    pMyContext.InspectionOverlays = overlayAcc; // overlay 누적 결과 반영
                    Step = (int)EStep.End;
                    break;
                }

                case EStep.End:
                    EContextResult finishResult;
                    if (pMyContext.AllPass) finishResult = EContextResult.Pass;
                    else finishResult = EContextResult.Fail;
                    FinishAction(finishResult);
                    break;
            }
            return Context;
        }

        // per-datum 1-image 로드 (TeachingImagePath → SimulImagePath 폴백 → grab).
        private HImage GrabOrLoadDatumImage(DatumConfig datum) {
            if (ShotParam == null) return null;
            HImage image = null;
            string teachingPath;
            if (datum != null) teachingPath = datum.TeachingImagePath;
            else teachingPath = null;
            #if SIMUL_MODE
            if (!string.IsNullOrEmpty(teachingPath) && File.Exists(teachingPath)) {
                try { image = new HImage(teachingPath); } catch { image = null; }
            }
            if (image == null && !string.IsNullOrEmpty(ShotParam.SimulImagePath) && File.Exists(ShotParam.SimulImagePath)) { // 폴백
                try { image = new HImage(ShotParam.SimulImagePath); } catch { image = null; }
            }
            if (image == null) { image = SystemHandler.Handle.Devices.GrabHalconImage(ShotParam); }
            #else
            image = SystemHandler.Handle.Devices.GrabHalconImage(ShotParam);
            #endif
            return image;
        }

        // DualImage 변형용 두 이미지 동시 로드 (per-datum).
        //  imageHorizontal: datum.TeachingImagePath 에서 로드 (가로축 ROI 검출용)
        //  imageVertical:   datum.TeachingImagePath_Vertical 에서 로드 (세로축 ROI 검출용)
        //  빈 경로 또는 파일 없음 / HImage 생성 실패 시 false + 로그.
        private bool TryGrabOrLoadDualDatumImages(DatumConfig datum, out HImage imageHorizontal, out HImage imageVertical) {
            imageHorizontal = null;
            imageVertical = null;
            if (datum == null) {
                Logging.PrintErrLog((int)ELogType.Error, "[Datum] DualImage: datum 이 null 입니다.");
                return false;
            }
            string pathH = datum.TeachingImagePath;
            string pathV = datum.TeachingImagePath_Vertical;

            if (string.IsNullOrEmpty(pathH) || !File.Exists(pathH)) {
                Logging.PrintErrLog((int)ELogType.Error, "[Datum] 가로축 티칭 이미지 경로가 비어 있거나 파일이 없습니다 (DualImage).");
                return false;
            }
            if (string.IsNullOrEmpty(pathV) || !File.Exists(pathV)) {
                Logging.PrintErrLog((int)ELogType.Error, "[Datum] 세로축 티칭 이미지 경로가 비어 있거나 파일이 없습니다 (DualImage).");
                return false;
            }

            try { imageHorizontal = new HImage(pathH); } catch (Exception ex) { Logging.PrintErrLog((int)ELogType.Error, "[Datum] 가로축 이미지 로드 실패: " + ex.Message); imageHorizontal = null; }
            try { imageVertical = new HImage(pathV); } catch (Exception ex) { Logging.PrintErrLog((int)ELogType.Error, "[Datum] 세로축 이미지 로드 실패: " + ex.Message); imageVertical = null; }

            if (imageHorizontal == null || imageVertical == null) {
                if (imageHorizontal != null) { try { imageHorizontal.Dispose(); } catch { } }
                if (imageVertical != null) { try { imageVertical.Dispose(); } catch { } }
                imageHorizontal = null;
                imageVertical = null;
                return false;
            }
            return true;
        }

        // DualImageEdgeDistanceMeasurement 측정용 양 이미지 로드.
        //  imageA: ShotParam.SimulImagePath (1차) — PointROI 검출용 (FAI 1차 이미지 = Shot 검사 이미지 재사용).
        //  imageB: meas.TeachingImagePath_Vertical (2차) — LineROI 검출용.
        //  경로 빈/파일없음 → false + 로그. HImage 한쪽 생성 실패 시 양쪽 Dispose + false (메모리 누수 방지).
        private bool TryGrabOrLoadFaiDualImages(MeasurementBase meas, out HImage imageA, out HImage imageB) {
            imageA = null;
            imageB = null;
            if (ShotParam == null) {
                Logging.PrintErrLog((int)ELogType.Error, "[FAI DualImage] ShotParam null");
                return false;
            }
            var dualMeas = meas as DualImageEdgeDistanceMeasurement;
            if (dualMeas == null) {
                Logging.PrintErrLog((int)ELogType.Error, "[FAI DualImage] meas 가 DualImageEdgeDistanceMeasurement 가 아닙니다");
                return false;
            }
            // PointROI 이미지 = Measurement 명시 경로 우선, 빈/파일 부재 시 ShotConfig.SimulImagePath fallback. ternary 회피 — 명시적 if/else 로 회귀 표면 최소화.
            string pathA;
            if (!string.IsNullOrEmpty(dualMeas.TeachingImagePath_Horizontal) && File.Exists(dualMeas.TeachingImagePath_Horizontal)) {
                pathA = dualMeas.TeachingImagePath_Horizontal; // Measurement 명시 경로
            }
            else {
                pathA = ShotParam.SimulImagePath; // fallback
            }
            string pathB = dualMeas.TeachingImagePath_Vertical; // LineROI 이미지 = meas 별도 경로

            if (string.IsNullOrEmpty(pathA) || !File.Exists(pathA)) {
                Logging.PrintErrLog((int)ELogType.Error, "[FAI DualImage] PointROI 이미지 경로 비어 있거나 파일 없음 (SimulImagePath)");
                return false;
            }
            if (string.IsNullOrEmpty(pathB) || !File.Exists(pathB)) {
                Logging.PrintErrLog((int)ELogType.Error, "[FAI DualImage] LineROI 이미지 경로 비어 있거나 파일 없음 (TeachingImagePath_Vertical)");
                return false;
            }
            try { imageA = new HImage(pathA); } catch (Exception ex) { Logging.PrintErrLog((int)ELogType.Error, "[FAI DualImage] PointROI 이미지 로드 실패: " + ex.Message); imageA = null; }
            try { imageB = new HImage(pathB); } catch (Exception ex) { Logging.PrintErrLog((int)ELogType.Error, "[FAI DualImage] LineROI 이미지 로드 실패: " + ex.Message); imageB = null; }
            if (imageA == null || imageB == null) {
                if (imageA != null) { try { imageA.Dispose(); } catch { } }
                if (imageB != null) { try { imageB.Dispose(); } catch { } }
                imageA = null; imageB = null;
                return false;
            }
            return true;
        }

        // 검출 성공 datum 의 녹색 원 + 원점 십자만 추출. 값만 복사해 워커 async race 차단.
        private List<DatumCaptureOverlay> BuildDatumCaptureSnapshot(InspectionSequence parentSeq) {
            if (parentSeq == null || parentSeq.DatumConfigs == null) return null;
            List<DatumCaptureOverlay> list = null;
            foreach (var dc in parentSeq.DatumConfigs) {
                if (dc == null) continue;
                var cap = new DatumCaptureOverlay();
                if (dc.CircleDetected_Radius > 0) { // 검출 원(녹색)
                    cap.HasCircle = true;
                    // 중심 fallback: CircleCenter 0(런타임 미갱신) 이면 DetectedOrigin 사용(원중심≈원점).
                    if (dc.CircleCenter_Row != 0.0) cap.CircleRow = dc.CircleCenter_Row;
                    else cap.CircleRow = dc.DetectedOriginRow;
                    if (dc.CircleCenter_Col != 0.0) cap.CircleCol = dc.CircleCenter_Col;
                    else cap.CircleCol = dc.DetectedOriginCol;
                    cap.CircleRadius = dc.CircleDetected_Radius;
                }
                if (dc.LastFindSucceeded && (dc.DetectedOriginRow != 0.0 || dc.DetectedOriginCol != 0.0)) { // 검출 원점 십자
                    cap.HasOrigin = true;
                    cap.OriginRow = dc.DetectedOriginRow;
                    cap.OriginCol = dc.DetectedOriginCol;
                    // 검출 기준선(축). 1차=DetectedRefAngle(각도 0 도 유효), 2차=DetectedRefAngle2(0 이면 단일축 datum → 미표시).
                    cap.HasAxis1 = true;
                    cap.Axis1AngleRad = dc.DetectedRefAngle;
                    if (dc.DetectedRefAngle2 != 0.0) {
                        cap.HasAxis2 = true;
                        cap.Axis2AngleRad = dc.DetectedRefAngle2;
                    }
                }
                if (cap.HasCircle || cap.HasOrigin) {
                    if (list == null) list = new List<DatumCaptureOverlay>();
                    list.Add(cap);
                }
            }
            return list;
        }

        // FAI별 원본/캡쳐 이미지를 비동기 저장 큐에 넣고, 파일명을 fai 에 동기 write-back.
        //  파일명은 BuildDto(AddResponse) 가 읽으므로 enqueue 전에 동기 확정. PNG write 만 워커가 비동기 수행.
        //  origin/capture 가 동일 timestamp·segment 쌍을 유지한다.
        //  sharedSrc(Shot당 1회 복사 공유)를 받아 origin/capture 요청에 ref 공유. 파일명 write-back 은 saver/공유 유무와 무관하게 항상 수행.
        private void QueueFaiCapture(FAIConfig fai, SharedHImage sharedSrc, List<EdgeInspectionOverlay> faiOverlays, List<DatumCaptureOverlay> datumSnapshot, string sequenceName) {
            if (fai == null) return;
            var saver = SystemHandler.Handle.CaptureImageSaver;
            DateTime ts = DateTime.Now; // origin/capture 동일 timestamp 공유 (쌍)
            string seg = OverlayCaptureRenderer.BuildMeasurePointSegment(faiOverlays); // P1/P1P2/빈값
            string judge;
            if (fai.IsPass) judge = "OK";
            else judge = "NG"; // 캡쳐/원본 파일명에 OK/NG 삽입. origin/capture 쌍 동일.
            string originName = CaptureImageSaveService.BuildFileName("origin", sequenceName, fai.FAIName, seg, judge, ts);
            string captureName = CaptureImageSaveService.BuildFileName("capture", sequenceName, fai.FAIName, seg, judge, ts);
            // 동기 write-back — BuildDto 가 즉시 읽을 수 있도록 (PNG write 실패와 무관하게 경로는 확정)
            // 엑셀/cycle.json 에 절대 경로(경로\파일명) 표기. 실제 저장 경로와 동일한 BuildFilePath 로 기록.
            fai.LastOriginImageFileName = CaptureImageSaveService.BuildFilePath(false, originName, ts);
            fai.LastCaptureImageFileName = CaptureImageSaveService.BuildFilePath(true, captureName, ts);
            if (saver == null || sharedSrc == null) return; // 서비스/공유 미존재 시 파일명만 기록, PNG skip

            // 원본 enqueue — 공유 이미지 직접 write (FAI별 복사 없음). 요청 1건당 ref 1 추가.
            sharedSrc.AddRef();
            saver.Enqueue(new CaptureImageSaveRequest
            {
                Shared = sharedSrc,
                NeedsRender = false,
                FileName = originName,
                IsCapture = false,
                Timestamp = ts
            });

            // capture 렌더(리전 disp_obj)는 워커 스레드가 공유 이미지 + 오버레이 스냅샷으로 수행.
            //  오버레이는 새 List 로 스냅샷 — fai.LastOverlays 와 참조 공유로 인한 후속 변형 위험 차단.
            List<EdgeInspectionOverlay> overlaySnapshot;
            if (faiOverlays != null) overlaySnapshot = new List<EdgeInspectionOverlay>(faiOverlays);
            else overlaySnapshot = null;
            sharedSrc.AddRef();
            saver.Enqueue(new CaptureImageSaveRequest
            {
                Shared = sharedSrc,
                NeedsRender = true,
                Overlays = overlaySnapshot,
                DatumOverlays = datumSnapshot, // datum 검출 오버레이(녹색 원) 포함
                FileName = captureName,
                IsCapture = true,
                Timestamp = ts
            });
        }
    }
}
