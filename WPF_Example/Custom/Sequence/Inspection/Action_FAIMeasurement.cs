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
            //260619 hbk Phase 57 #6 leveling 제거 — EStep.Level 폐기 (D-13/D-14), MoveZ→DatumPhase 직결
            Init,
            MoveZ,
            DatumPhase,
            Grab,
            Measure,
            End
        }

        private FAIMeasurementContext pMyContext;
        private VirtualCamera pCamera;

        //260722 hbk Phase 68 D-09: 매직넘버 상수화 — ZIndexA/B 미설정 sentinel + 크로스-Z 저장소 역할(A/B) 키 접미사.
        private const int UNSET_ZINDEX = -1;
        private const string CROSS_Z_ROLE_SUFFIX_A = "_ZA";
        private const string CROSS_Z_ROLE_SUFFIX_B = "_ZB";
        //260722 hbk Phase 68 D-06/D-09: Datum 크로스-Z 저장소 키 접두사 — 측정 키(ShotName|MeasName)와 네임스페이스 구분.
        private const string CROSS_Z_DATUM_KEY_PREFIX = "DATUM|";

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
                    //260619 hbk Phase 57 #6 leveling 제거 — MoveZ→DatumPhase 직결 (EStep.Level 폐기, D-13/D-14)
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
                        //260618 hbk Phase 54 ALIGN-01 이미지 회전(datumLevelOn/datumLevelAngle) 폐기 (D-03/D-05 warp 0회).
                        //  레벨링 이미지회전 → 패턴매칭 ROI 좌표변환으로 대체. 이전 datumLevelOn/datumLevelAngle 지역변수 제거.
                        foreach (var datum in parentSeq.DatumConfigs) {
                            if (datum == null) continue;
                            // Datum 전용 조명(SourceShotName 상속과 무관) 을 grab 직전에 켠다. 이 grab 이 끝나면
                            //  루프 종료 후 ApplyShotLights 로 되돌려야 EStep.Grab 의 측정 grab 이 Shot 조명 아래서 이뤄진다.
                            parentSeq.ApplyDatumLights(datum);
                            // 조명 명령은 큐잉만 되고 실제 전송은 백그라운드 스레드가 처리 — grab 전에 실제 반영을 기다린다.
                            //  (기존엔 수동 UI grab 경로에만 있던 대기를 자동 검사 사이클에도 배선. SIMUL/오프라인처럼
                            //  실제로 대기할 쓰기가 없으면 즉시 반환되므로 비용은 무시할 만큼 작다.)
                            LightHandler.Handle.WaitForPendingWrites();
                            if (datum.AlgorithmTypeEnum == EDatumAlgorithm.VerticalTwoHorizontalDualImage) {
                                //260722 hbk Phase 68 D-05: Datum ZIndexA/B 오설정 게이트 — TryGrabOrLoadDualDatumImages 호출 전
                                //  명시적 실패 처리(조용한 static 폴백 금지). 미설정(-1/-1)은 게이트 미해당 → 기존 static 경로.
                                bool bDatumZIndexMisconfigured = IsDatumZIndexMisconfigured(datum, parentSeq);
                                if (bDatumZIndexMisconfigured) {
                                    string misName = datum.DatumName;
                                    if (misName == null) misName = "";
                                    Logging.PrintLog((int)ELogType.Error, "[FAIMeasurement] Datum '" + misName + "' ZIndexA=" + datum.ZIndexA + ", ZIndexB=" + datum.ZIndexB + " 크로스-Z 오설정(동일값/단일설정/존재하지 않는 index, " + SkipReason.ZINDEX_MISCONFIGURED + ")");
                                    datum.RuntimeDetectFailed = true;
                                    parentSeq.MarkDatumFailed(datum.DatumName);
                                    continue; // datum skip, abort 안 함
                                }
                                //260722 hbk Phase 68 D-06 (WARNING 2): Datum 크로스-Z 는 별도 z_index→Datum 매핑 조회를
                                //  추가하지 않는다 — (a) 바로 이 루프가 매 실행 Action 마다 시퀀스 DatumConfigs 전체를
                                //  재검출하고 (b) Plan 02 ProcessTest 의 빈-매칭→StartAll 폴백에 의존해, 두 z_index
                                //  모두에서 이 Datum 검출이 실행된다는 사실이 크로스-Z 정정성의 전제다. 이 두 동작(전체
                                //  재검출 / 빈-매칭 폴백)을 향후 변경할 때는 Datum 크로스-Z 재검증이 반드시 필요하다.
                                HImage imgH = null, imgV = null;
                                bool bDatumCrossZPending;
                                try {
                                    if (!TryGrabOrLoadDualDatumImages(datum, parentSeq, out imgH, out imgV, out bDatumCrossZPending)) {
                                        if (bDatumCrossZPending) {
                                            continue; // Z1(비완성 index): 캡처만 — 실패 아님(MarkDatumFailed 미설정), 완성 z_index에서 검출(D-02a)
                                        }
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
                                    //260619 hbk Phase 57 #4 DualImage align 배선 (deferred 게이트 해제, 단일이미지 분기 미러).
                                    //  enabled → align 단독 경로(imgH 패턴매칭 → 단일 alignRigid 를 imgH/imgV 두 검출에 적용, D-01). disabled → 기존 2-image 검출(off 회귀 0).
                                    //  패턴 모델은 가로축(imgH/TeachingImagePath) 1세트만 사용 — 세로엔 패턴 없음(D-04).
                                    if (datum.IsPatternAlignEnabled) {
                                        string modelPath = InspectionSequence.ResolveDatumModelPath(datum); // 티칭과 동일 키 헬퍼 (D-07)
                                        string alignErr;
                                        if (!parentSeq.TryComposeAlign(datum, imgH, imgV, modelPath, out alignErr)) {
                                            string dn = datum.DatumName;
                                            if (dn == null) dn = "";
                                            string ae = alignErr;
                                            if (ae == null) ae = "";
                                            Logging.PrintLog((int)ELogType.Error, "[FAIMeasurement] Datum '" + dn + "' DualImage 패턴매칭 실패 (ALIGN_FAIL, skip): " + ae);
                                            datum.RuntimeDetectFailed = true;
                                            parentSeq.MarkAlignFailed(datum.DatumName); //260619 hbk Phase 57 #5 lenient — NG 강제, abort 안 함
                                        }
                                    } else {
                                        string derr;
                                        if (!parentSeq.TryRunSingleDatum(datum, imgH, imgV, out derr)) { // 기존 검출 경로 무수정
                                            string datumName = datum.DatumName;
                                            if (datumName == null) datumName = "";
                                            string derrStr = derr;
                                            if (derrStr == null) derrStr = "";
                                            Logging.PrintLog((int)ELogType.Error, "[FAIMeasurement] Datum '" + datumName + "' 검출 실패 (skip): " + derrStr);
                                            datum.RuntimeDetectFailed = true;
                                            parentSeq.MarkDatumFailed(datum.DatumName);
                                        }
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
                                //260618 hbk Phase 54 ALIGN-01 패턴매칭 위치보정 (D-02/D-04/D-05). 이미지 회전(레벨링 warp) 폐기 (D-03/D-05 warp 0회).
                                //  enabled → align 단독 경로(검출 미수행, 이중적용 방지). disabled → 기존 검출 경로 유지(off 회귀 0, D-11).
                                try {
                                    if (datum.IsPatternAlignEnabled) {
                                        string modelPath = InspectionSequence.ResolveDatumModelPath(datum); // 54-05 티칭과 동일 키 헬퍼 (D-07)
                                        string alignErr;
                                        if (!parentSeq.TryComposeAlign(datum, img, modelPath, out alignErr)) {
                                            string dn = datum.DatumName;
                                            if (dn == null) dn = "";
                                            string ae = alignErr;
                                            if (ae == null) ae = "";
                                            Logging.PrintLog((int)ELogType.Error, "[FAIMeasurement] Datum '" + dn + "' 패턴매칭 실패 (ALIGN_FAIL, skip): " + ae);
                                            datum.RuntimeDetectFailed = true;
                                            parentSeq.MarkAlignFailed(datum.DatumName); // D-10 lenient — 측정 NG(ALIGN_FAIL) 강제, abort 안 함
                                        }
                                    } else {
                                        string derr;
                                        if (!parentSeq.TryRunSingleDatum(datum, img, null, out derr)) { // 기존 검출 경로 무수정
                                            string datumName = datum.DatumName;
                                            if (datumName == null) datumName = "";
                                            string derrStr = derr;
                                            if (derrStr == null) derrStr = "";
                                            Logging.PrintLog((int)ELogType.Error, "[FAIMeasurement] Datum '" + datumName + "' 검출 실패 (skip): " + derrStr);
                                            datum.RuntimeDetectFailed = true;
                                            parentSeq.MarkDatumFailed(datum.DatumName);
                                        }
                                    }
                                } finally {
                                    img.Dispose();
                                }
                            }
                        }
                        // Datum grab 동안 켜져 있던 datum 전용 조명을 이 Shot 본연의 조명으로 되돌린다.
                        //  이후 EStep.Grab 의 측정 grab 이 datum 조명이 아니라 $PREP 로 세팅된 Shot 조명 아래서 이뤄져야 한다.
                        if (ShotParam != null) {
                            parentSeq.ApplyShotLights(ShotParam.ZIndex);
                            // EStep.Grab 의 실제 촬영은 다음 Run() 호출(시퀀스 스레드 다음 tick)에서 이뤄지므로,
                            //  여기서 큐가 비워질 때까지 동기 대기해두면 그 사이 조명 복귀가 실제로 반영된다.
                            LightHandler.Handle.WaitForPendingWrites();
                        }
                    }
                    // DatumConfigs 비어있으면 무보정 pass-through — abort 없음 (lenient)
                    //260722 hbk Phase 68 GAP-2(AdvanceAfterDatumPhase, 68-GAP-ANALYSIS.md 우선순위 2)→68-12: datum-only
                    //  index(예: Side z=1, 오직 크로스-Z Datum 만 씀) 뿐 아니라 z=0(이 시퀀스의 대표 Datum 트리거
                    //  실행)에서도 이 Action 은 DatumPhase(Datum 캡처+검출)를 트리거하려고만 실행됐다 — 이 Shot 의
                    //  Grab/Measure 를 그대로 진행하면 이 Shot의 일반 측정이 잘못된 물리 Z(z=1 은 Datum 위치, z=0 은
                    //  아직 이 Shot 차례가 아님)에서 재실행되어 cycle.json/저장이미지/화면표시가 오염된다(GAP-2 남은
                    //  리스크). 판정은 이제 InspectionSequence.ShouldSkipMeasurementAfterDatumPhase 단일 소스(기존
                    //  IsDatumOnlyExecutionIndex z>=1 경로 + 신규 z=0 대표트리거 경로 OR 결합)로 통합됐다.
                    int nCurZ = 0;
                    bool bDatumOnly = false;
                    if (parentSeq != null) {
                        nCurZ = parentSeq.GetExecutionZIndex();
                        bDatumOnly = parentSeq.ShouldSkipMeasurementAfterDatumPhase(nCurZ);
                    }
                    if (bDatumOnly) {
                        Step = (int)EStep.End;
                    } else {
                        Step = (int)EStep.Grab; // datum 부분 실패해도 측정 진행
                    }
                    break;
                }

                case EStep.Grab:
                    if (ShotParam != null && !ShotParam.HasImage) {
                        HImage image = null;
                        // ShotParam.SimulImagePath = InspectionImagePath 역할 (검사 사이클 마다 로드). 티칭 기준 이미지는 별도 DatumConfig.TeachingImagePath (셋업 시 1회, INI 보존) 사용 — 역할 분리. Simul 에서 두 경로 동일 파일 가능.
                        #if SIMUL_MODE
                        image = LoadShotInspectionImage(); // SIMUL: 항상 저장 이미지(ShotParam.SimulImagePath) 로드
                        #else
                        if (SystemSetting.Handle.OfflineInspectMode) {
                            // 오프라인(수동 지그): 라이브 grab 대신 노드 저장 이미지 로드. 각 SHOT 이 자기 Z 이미지라 정합 성립.
                            image = LoadShotInspectionImage();
                        } else {
                            image = SystemHandler.Handle.Devices.GrabHalconImage(ShotParam);
                        }
                        #endif
                        if (image != null) {
                            //260618 hbk Phase 54 ALIGN-01 측정 이미지 회전(레벨링 warp) 폐기 (D-03/D-05 warp 0회).
                            //  레벨링 이미지회전 → 패턴매칭 ROI 좌표변환으로 대체. 측정은 보정 전 원본 픽셀에서 수행.
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
                                //260619 hbk per-shot 보정계수 적용 = PixelResolution × CorrectionFactor (단일소스 GetEffectivePixelResolution). PixelResolution 저장값 불변.
                                double pixRes = ShotParam != null ? ShotParam.GetEffectivePixelResolution() : 1.0; //260615 hbk Phase 42 D-01 Shot 단일소스
                                // (구) ±2% 가드레일 경고 제거 — CorrectionFactor 를 배율 보정(예: 0.72)까지 포함한 단일 보정 knob 으로
                                //  운용하기로 결정. ±2% 초과가 정상 사용이 되어 매 검사 Error 로그를 헛되이 채우던 노이즈였음(로그 전용, 검사 영향 0).
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
                                            MarkMeasurementDatumSkipped(meas, parentSeq2); //260702 hbk Extract Method(Task1)
                                            faiAllPass = false;
                                            measuredCount++; // 시도 회수 통계
                                            continue; // 다음 measurement 진행 (TryExecute 호출 안 함)
                                        }
                                        //260716 hbk DatumRef 참조 불일치 게이트 — 오타/개명/삭제로 실존하지 않는 datum 을 가리키면
                                        //  검출 시도 자체가 없어 IsDatumFailed 게이트를 우회하고 identity(무보정)로 조용히 측정되던 결함 차단.
                                        //  '무보정 의도(빈 DatumRef)'와 '참조 깨짐'을 구분해 후자만 NG 로 승격(기존 lenient 구조 유지).
                                        if (parentSeq2 != null && parentSeq2.IsDatumRefUnresolvable(meas.DatumRef))
                                        {
                                            MarkMeasurementDatumRefMissing(meas);
                                            faiAllPass = false;
                                            measuredCount++;
                                            continue;
                                        }
                                        //260722 hbk Phase 68 D-02a/D-05: 크로스-Z(ZIndexA/B 둘 다 -1 아님) 측정 게이트/캡처.
                                        //  ZIndexA/B 둘 다 -1(미설정) 이면 이 블록 진입 안 함 → 기존 경로 그대로(D-07 회귀 0).
                                        var dualMeasForGate = meas as DualImageEdgeDistanceMeasurement;
                                        bool bHasAnyZIndex = dualMeasForGate != null && (dualMeasForGate.ZIndexA != UNSET_ZINDEX || dualMeasForGate.ZIndexB != UNSET_ZINDEX);
                                        if (bHasAnyZIndex)
                                        {
                                            bool bMisconfigured = IsZIndexMisconfigured(dualMeasForGate, parentSeq2);
                                            if (bMisconfigured)
                                            {
                                                MarkMeasurementZIndexMisconfigured(meas);
                                                faiAllPass = false;
                                                measuredCount++;
                                                continue;
                                            }
                                            bool bRelevant, bCaptureOk, bCompleted;
                                            ProcessCrossZCaptureTick(dualMeasForGate, parentSeq2, out bRelevant, out bCaptureOk, out bCompleted);
                                            if (!bRelevant)
                                            {
                                                continue; // 이 tick 은 이 측정과 무관 — 상태변화 없음(안전망)
                                            }
                                            if (!bCaptureOk)
                                            {
                                                meas.ClearResult();
                                                meas.LastSkipReason = SkipReason.NO_IMAGE;
                                                meas.LastJudgement = false;
                                                faiAllPass = false;
                                                measuredCount++;
                                                continue;
                                            }
                                            if (!bCompleted)
                                            {
                                                measuredCount++; // Z1(비완성 index): 캡처만 — NG 아님, 미보고(Task4 index 게이트가 보장)
                                                continue;
                                            }
                                            // 완성 index — 아래 공용 실행 경로로 계속 진행(transform/InjectDatumOrigin 재사용)
                                        }
                                        HTuple transform = ResolveDatumTransform(parentSeq2, meas.DatumRef); //260702 hbk Extract Method(Task1)
                                        InjectDatumOrigin(meas, parentSeq2); //260702 hbk Extract Method(Task1)
                                        double resultValue;
                                        string measError;
                                        List<EdgeInspectionOverlay> measOverlays;
                                        bool ok;
                                        if (bHasAnyZIndex)
                                        {
                                            ok = TryExecuteCrossZMeasurement(dualMeasForGate, parentSeq2, transform, pixRes, out resultValue, out measError, out measOverlays); //260722 hbk Phase 68 D-02a: 완성 index 크로스-Z 실행
                                        }
                                        else
                                        {
                                            ok = TryExecuteMeasurement(meas, image, transform, pixRes, out resultValue, out measError, out measOverlays); //260702 hbk Extract Method(Task1)
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
                                        ApplyOverlaySuffixAndAccumulate(meas, measOverlays, overlayAcc, faiOverlays); //260702 hbk Extract Method(Task2)
                                        if (!meas.LastJudgement) {
                                            faiAllPass = false;
                                        }
                                        measuredCount++;
                                    }
                                    AggregateFaiResult(fai, faiAllPass, faiOverlays, sharedSrc, datumSnapshot); //260702 hbk Extract Method(Task2)
                                    if (!faiAllPass) allPass = false;
                                }
                                } finally { // 검사 루프 소유 ref 1 해제(워커 요청들의 ref 와 독립). 마지막 Release 시 공유 이미지 dispose.
                                    if (sharedSrc != null) sharedSrc.Release();
                                }
                            } else {
                                //260616 hbk simul-shot-cascade: 이미지 미취득(SimulImagePath 무효) SHOT 은 모든 measurement NG 처리.
                                //  과거엔 공유 카메라 캐시 fallback 으로 항상 이미지가 채워져 이 분기가 사실상 미도달 → fallback 제거 후
                                //  무효 경로 SHOT 이 image==null 도달. allPass 가 default true 로 남아 잘못 PASS 되는 것을 차단.
                                allPass = false;
                                MarkAllMeasurementsNoImage(ref measuredCount); //260702 hbk Extract Method(Task2)
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

        // SIMUL / 오프라인 공용: SHOT 검사 이미지는 ShotParam.SimulImagePath 단일소스. 실패 시 null + 로그.
        //  경로 무효/로드 실패 시 공유 VirtualCamera 캐시(BackgroundImagePath 하드코딩 / stale LastGrabHalconImage)로
        //  silent fallback 하지 않는다 — fallback 은 시퀀스 전 SHOT 공유 캐시의 임의 이미지를 반환해 경로 오류 SHOT 이
        //  엉뚱/직전 이미지로 측정되는 캐스케이드(번짐)를 유발했었다. 무효 경로 SHOT 은 null 로 유지 → Measure 단계
        //  GetImage()==null 로 해당 SHOT 만 측정 skip, SHOT 간 전파 차단.
        private HImage LoadShotInspectionImage() {
            if (ShotParam == null) return null;
            if (!string.IsNullOrEmpty(ShotParam.SimulImagePath) && File.Exists(ShotParam.SimulImagePath)) {
                try {
                    return new HImage(ShotParam.SimulImagePath);
                } catch (Exception ex) {
                    Logging.PrintLog((int)ELogType.Error, "[FAIMeasurement] SHOT '" + (ShotParam.ShotName ?? "") + "' 검사이미지 로드 실패: " + ex.Message);
                    return null;
                }
            }
            Logging.PrintLog((int)ELogType.Error, "[FAIMeasurement] SHOT '" + (ShotParam.ShotName ?? "") + "' 검사이미지 경로 비어있음/파일없음 — 이 SHOT 만 측정 skip (공유 카메라 캐시 fallback 차단). 오프라인 모드면 '검사이미지 Grab' 으로 먼저 확보 필요.");
            return null;
        }

        // per-datum 1-image 로드 (TeachingImagePath → SimulImagePath 폴백 → grab).
        private HImage GrabOrLoadDatumImage(DatumConfig datum) {
            if (ShotParam == null) return null;
            HImage image = null;
            string teachingPath;
            if (datum != null) teachingPath = datum.TeachingImagePath;
            else teachingPath = null;
            #if SIMUL_MODE
            image = LoadDatumImageFromPath(datum, teachingPath, true); // SIMUL: 저장 datum 이미지, 폴백 shot, 폴백 grab
            #else
            if (SystemSetting.Handle.OfflineInspectMode) {
                // 오프라인(수동 지그): datum 저장 이미지 로드. datum 은 초점 맞는 자기 이미지라 정합 성립. grab 폴백 없음(잘못된 Z 은폐 금지).
                image = LoadDatumImageFromPath(datum, teachingPath, false);
            } else {
                image = SystemHandler.Handle.Devices.GrabHalconImage(ShotParam);
            }
            #endif
            return image;
        }

        // datum 저장 이미지 로드: teachingPath → ShotParam.SimulImagePath 폴백. allowGrabFallback=true 면 둘 다 실패 시 라이브 grab.
        //  오프라인 모드(allowGrabFallback=false)에선 저장 이미지 부재 시 null 반환 → datum Find 실패로 명확히 드러남.
        private HImage LoadDatumImageFromPath(DatumConfig datum, string teachingPath, bool allowGrabFallback) {
            HImage image = null;
            if (!string.IsNullOrEmpty(teachingPath) && File.Exists(teachingPath)) {
                try { image = new HImage(teachingPath); } catch { image = null; }
            }
            if (image == null && ShotParam != null && !string.IsNullOrEmpty(ShotParam.SimulImagePath) && File.Exists(ShotParam.SimulImagePath)) { // 폴백
                try { image = new HImage(ShotParam.SimulImagePath); } catch { image = null; }
            }
            if (image == null) {
                if (allowGrabFallback) {
                    image = SystemHandler.Handle.Devices.GrabHalconImage(ShotParam);
                } else {
                    string dName = "";
                    if (datum != null) dName = datum.DatumName ?? "";
                    Logging.PrintLog((int)ELogType.Error, "[Datum] '" + dName + "' 오프라인 datum 이미지 없음 (TeachingImagePath/SimulImagePath) — datum Find skip. '검사이미지 Grab' 으로 먼저 확보 필요.");
                }
            }
            return image;
        }

        // DualImage 변형용 두 이미지 동시 로드 (per-datum).
        //  ZIndexA/ZIndexB 둘 다 설정(-1 아님) → 크로스-Z 라이브 캡처 경로(D-06). 미설정(-1/-1) → 기존 static 경로(D-07 회귀 0).
        //  bPending=true 는 "실패 아님, 다음 z_index 대기"(D-02a) — 호출부가 MarkDatumFailed 를 걸지 않는 근거.
        private bool TryGrabOrLoadDualDatumImages(DatumConfig datum, InspectionSequence parentSeq, out HImage imageHorizontal, out HImage imageVertical, out bool bPending) {
            imageHorizontal = null;
            imageVertical = null;
            bPending = false;
            if (datum == null) {
                Logging.PrintErrLog((int)ELogType.Error, "[Datum] DualImage: datum 이 null 입니다.");
                return false;
            }
            bool bCrossZEnabled = datum.ZIndexA != UNSET_ZINDEX && datum.ZIndexB != UNSET_ZINDEX;
            if (bCrossZEnabled) {
                return TryGrabOrLoadCrossZDatumImages(datum, parentSeq, out imageHorizontal, out imageVertical, out bPending);
            }
            return TryLoadStaticDualDatumImages(datum, out imageHorizontal, out imageVertical);
        }

        // 기존 static teaching 파일 로드 경로 (ZIndexA/B 미설정, D-07 회귀 0) — 원본 TryGrabOrLoadDualDatumImages 로직 그대로.
        //  imageHorizontal: datum.TeachingImagePath 에서 로드 (가로축 ROI 검출용)
        //  imageVertical:   datum.TeachingImagePath_Vertical 에서 로드 (세로축 ROI 검출용)
        //  빈 경로 또는 파일 없음 / HImage 생성 실패 시 false + 로그.
        private bool TryLoadStaticDualDatumImages(DatumConfig datum, out HImage imageHorizontal, out HImage imageVertical) {
            imageHorizontal = null;
            imageVertical = null;
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

        //260722 hbk Phase 68 D-06/D-02a: Datum 크로스-Z 라이브 캡처/주입 — 완성 z_index=max(ZIndexA,ZIndexB)
        //  (측정 레벨 TryExecuteCrossZMeasurement 완성 index 정의와 통일). 현재 tick 이 이 datum 의 ZIndexA/B
        //  어느 쪽도 아니면 무관(bPending=true, 상태변화 없음 — ProcessCrossZCaptureTick bRelevant 미러).
        private bool TryGrabOrLoadCrossZDatumImages(DatumConfig datum, InspectionSequence parentSeq, out HImage imageHorizontal, out HImage imageVertical, out bool bPending) {
            imageHorizontal = null;
            imageVertical = null;
            bPending = false;
            if (parentSeq == null) {
                Logging.PrintErrLog((int)ELogType.Error, "[Datum] 크로스-Z: parentSeq null");
                return false;
            }
            int nCurZ = parentSeq.GetExecutionZIndex();
            bool bIsRoleA = nCurZ == datum.ZIndexA;
            bool bIsRoleB = nCurZ == datum.ZIndexB;
            bool bRelevant = bIsRoleA || bIsRoleB;
            if (!bRelevant) {
                //260722 hbk Phase 68 CROSS-1(68-GAP-ANALYSIS.md): 소비 index(자기 ZIndexA/B 아님, 예 z=2 측정)
                //  — ClearDatumTransforms 가 매 Action 의 EStep.DatumPhase 진입마다 _datumTransforms 를 비우므로,
                //  여기서 재검출 없이 그냥 skip 하면 ResolveDatumTransform 이 identity 로 조용히 폴백해 무보정
                //  측정이 정상 측정처럼 P/F 를 낸다. 양 role 이 저장소에 이미 있으면 결정론적으로 재검출한다.
                bool bBothStored = IsCrossZDatumBothStored(datum, parentSeq);
                if (bBothStored) {
                    return TryReDetectCrossZDatumFromStore(datum, parentSeq, out imageHorizontal, out imageVertical);
                }
                bPending = true; // 저장 미완성 + 이 tick 무관 — 상태변화 없음(안전망)
                return false;
            }
            if (!CaptureAndStoreCrossZDatumImage(datum, parentSeq, bIsRoleA)) {
                return false; // 실제 캡처 실패 — 호출부가 MarkDatumFailed(실패 확정)
            }
            return TryTakeCompletedCrossZDatumImages(datum, parentSeq, out imageHorizontal, out imageVertical, out bPending);
        }

        // 현재 tick 이미지를 라이브 grab(기존 Shot 라이브 grab 선례 GrabOrLoadDatumImage 재사용, 새 메커니즘 금지 D-01)해
        //  크로스-Z 저장소에 역할(A/B) 키로 저장. GrabSyncLock 은 호출부(EStep.DatumPhase)가 이미 보유 — 새 lock 없음.
        private bool CaptureAndStoreCrossZDatumImage(DatumConfig datum, InspectionSequence parentSeq, bool bIsRoleA) {
            HImage capturedImage = GrabOrLoadDatumImage(datum);
            if (capturedImage == null) {
                return false;
            }
            string baseKey = BuildCrossZDatumKey(datum);
            string roleKey;
            if (bIsRoleA) roleKey = baseKey + CROSS_Z_ROLE_SUFFIX_A;
            else roleKey = baseKey + CROSS_Z_ROLE_SUFFIX_B;
            parentSeq.StoreCrossZImage(roleKey, capturedImage);
            try { capturedImage.Dispose(); } catch { } // Store 가 CopyImage 로 소유 클론 저장 — 원본은 여기서 즉시 해제
            return true;
        }

        // 양 role(A/B) 저장 완료 여부 판정 — 완성이면 클론 반환(호출부 finally Dispose 계약), 아니면 bPending=true(Z1 캡처만).
        private bool TryTakeCompletedCrossZDatumImages(DatumConfig datum, InspectionSequence parentSeq, out HImage imageHorizontal, out HImage imageVertical, out bool bPending) {
            imageHorizontal = null;
            imageVertical = null;
            bPending = false;
            string keyA, keyB;
            ResolveCrossZDatumRoleKeys(datum, out keyA, out keyB);
            bool bCompleted = parentSeq.HasCrossZImage(keyA) && parentSeq.HasCrossZImage(keyB);
            if (!bCompleted) {
                bPending = true; // Z1(비완성 index): 캡처만 — 실패 아님
                return false;
            }
            return TryTakeCrossZImageClones(keyA, keyB, parentSeq, out imageHorizontal, out imageVertical);
        }

        // 크로스-Z 저장소 키 = "DATUM|" 접두사 + DatumName — 측정 키(ShotName|MeasName)와 네임스페이스 구분(충돌 방지).
        private string BuildCrossZDatumKey(DatumConfig datum) {
            string datumName = "";
            if (datum != null && datum.DatumName != null) {
                datumName = datum.DatumName;
            }
            return CROSS_Z_DATUM_KEY_PREFIX + datumName;
        }

        // BuildCrossZDatumKey 단일 소스로부터 role(A/B) 키 두 개를 도출 — 키 도출 중복 순회 금지(D-09).
        private void ResolveCrossZDatumRoleKeys(DatumConfig datum, out string keyA, out string keyB) {
            string baseKey = BuildCrossZDatumKey(datum);
            keyA = baseKey + CROSS_Z_ROLE_SUFFIX_A;
            keyB = baseKey + CROSS_Z_ROLE_SUFFIX_B;
        }

        // 양 role(A/B) 저장 완료 여부만 판정(클론 미취득) — TryGrabOrLoadCrossZDatumImages 의 !bRelevant(소비 index)
        //  분기가 재검출 여부를 게이트하는 데 사용(CROSS-1).
        private bool IsCrossZDatumBothStored(DatumConfig datum, InspectionSequence parentSeq) {
            string keyA, keyB;
            ResolveCrossZDatumRoleKeys(datum, out keyA, out keyB);
            return parentSeq.HasCrossZImage(keyA) && parentSeq.HasCrossZImage(keyB);
        }

        //260722 hbk Phase 68 CROSS-1: 크로스-Z Datum 소비 index(자기 ZIndexA/B 아님) 결정론적 재검출 —
        //  양 role 이미지가 저장소에 이미 있을 때 클론을 반환해 호출부(EStep.DatumPhase)가 TryRunSingleDatum/
        //  TryComposeAlign 을 그대로 재실행하도록 한다. 클론 소유권은 호출부 finally Dispose 계약(기존과 동일).
        private bool TryReDetectCrossZDatumFromStore(DatumConfig datum, InspectionSequence parentSeq, out HImage imageHorizontal, out HImage imageVertical) {
            string keyA, keyB;
            ResolveCrossZDatumRoleKeys(datum, out keyA, out keyB);
            return TryTakeCrossZImageClones(keyA, keyB, parentSeq, out imageHorizontal, out imageVertical);
        }

        // 저장소 키 두 개로부터 클론 취득 공용 로직 — TryTakeCompletedCrossZDatumImages/TryReDetectCrossZDatumFromStore
        //  가 공유(D-09 동일 로직 2회 이상 반복 금지). 한쪽만 취득 성공 시 누수 방지를 위해 양쪽 모두 Dispose.
        private bool TryTakeCrossZImageClones(string keyA, string keyB, InspectionSequence parentSeq, out HImage imageHorizontal, out HImage imageVertical) {
            imageHorizontal = parentSeq.TakeCrossZImageCopy(keyA);
            imageVertical = parentSeq.TakeCrossZImageCopy(keyB);
            bool bBothLoaded = imageHorizontal != null && imageVertical != null;
            if (!bBothLoaded) {
                if (imageHorizontal != null) { try { imageHorizontal.Dispose(); } catch { } }
                if (imageVertical != null) { try { imageVertical.Dispose(); } catch { } }
                imageHorizontal = null;
                imageVertical = null;
                return false; // 완성 index 인데 클론 취득 실패 — 실제 실패
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
            //260722 hbk Phase 68 D-08 버그수정: pathA 산출 우선순위를 3단 if/else-if/else 로 재정렬(D-09).
            //  (1) ShotParam.HasImage(라이브 grab) 최우선 — GetImage() 클론을 imageA 로 직접 사용, 파일 재로드 스킵.
            //  (2) TeachingImagePath_Horizontal 명시 경로  (3) ShotParam.SimulImagePath 폴백.
            //  기존 버그: (1)을 전혀 확인하지 않아 이미 라이브로 확보된 이미지를 무시하고 매번 파일에서 재로드했음.
            bool bHasLiveImage = ShotParam.HasImage;
            string pathA = null;
            if (bHasLiveImage) {
                imageA = ShotParam.GetImage(); // 라이브 클론 — 소유권은 호출부(TryExecuteMeasurement finally)가 Dispose
            }
            else if (!string.IsNullOrEmpty(dualMeas.TeachingImagePath_Horizontal) && File.Exists(dualMeas.TeachingImagePath_Horizontal)) {
                pathA = dualMeas.TeachingImagePath_Horizontal; // Measurement 명시 경로
            }
            else {
                pathA = ShotParam.SimulImagePath; // fallback
            }
            string pathB = dualMeas.TeachingImagePath_Vertical; // LineROI 이미지 = meas 별도 경로 (무변경)

            bool bPathALoadNeeded = !bHasLiveImage; // 라이브로 이미 확보했으면 파일 경로 검증/로드 스킵
            bool bPathAInvalid = bPathALoadNeeded && (string.IsNullOrEmpty(pathA) || !File.Exists(pathA));
            if (bPathAInvalid) {
                Logging.PrintErrLog((int)ELogType.Error, "[FAI DualImage] PointROI 이미지 경로 비어 있거나 파일 없음 (SimulImagePath)");
                return false;
            }
            if (string.IsNullOrEmpty(pathB) || !File.Exists(pathB)) {
                Logging.PrintErrLog((int)ELogType.Error, "[FAI DualImage] LineROI 이미지 경로 비어 있거나 파일 없음 (TeachingImagePath_Vertical)");
                return false;
            }
            if (bPathALoadNeeded) {
                try { imageA = new HImage(pathA); } catch (Exception ex) { Logging.PrintErrLog((int)ELogType.Error, "[FAI DualImage] PointROI 이미지 로드 실패: " + ex.Message); imageA = null; }
            }
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

            //260622 hbk Phase 48 PROTO-01: 자재번호 추출 — 부모 시퀀스 RequestPacket(TCP TEST 패킷). null 이면 -1 폴백.
            //  부모 시퀀스가 없거나 InspectionSequence 아닌 경우에도 -1(생략), 회귀 0.
            int nIndexNumber = -1;
            InspectionSequence parentSeq;
            if (ShotParam != null)
            {
                parentSeq = ShotParam.Parent as InspectionSequence;
            }
            else
            {
                parentSeq = null;
            }
            bool bHasRequest = parentSeq != null && parentSeq.RequestPacket != null;
            if (bHasRequest)
            {
                nIndexNumber = parentSeq.RequestPacket.IndexNumber;
            }

            string originName = CaptureImageSaveService.BuildFileName("origin", sequenceName, fai.FAIName, seg, judge, ts, nIndexNumber);   //260622 hbk Phase 48 PROTO-01: 자재번호 포함 파일명
            string captureName = CaptureImageSaveService.BuildFileName("capture", sequenceName, fai.FAIName, seg, judge, ts, nIndexNumber);  //260622 hbk Phase 48 PROTO-01
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

        //260702 hbk Extract Method(Task1): datum/align 실패로 인한 measurement skip 처리 (원본 case EStep.Measure 인라인 이식, 동치 보장)
        private void MarkMeasurementDatumSkipped(MeasurementBase meas, InspectionSequence parentSeq2) {
            meas.ClearResult();
            //260618 hbk Phase 54 ALIGN-01 align 실패와 검출 실패 구분 표기 (D-10) — Excel/UI 식별.
            //260702 hbk 기존 삼항(?:) → if-else 로 전개(동치 유지, 신규 삼항 미도입)
            if (parentSeq2.IsAlignFailed(meas.DatumRef)) meas.LastSkipReason = SkipReason.ALIGN_FAIL; //260710 hbk 상수화
            else meas.LastSkipReason = SkipReason.DATUM_FAIL; //260710 hbk 상수화
            meas.LastJudgement = false; // skip 도 NG 강도
            string measName = meas.MeasurementName;
            if (measName == null) measName = meas.TypeName;
            string datumRef = meas.DatumRef;
            if (datumRef == null) datumRef = "";
            Logging.PrintLog((int)ELogType.Error, "[FAIMeasurement] Measurement '" + measName + "' skipped — datum '" + datumRef + "' 실패 (" + meas.LastSkipReason + ")");
        }

        //260716 hbk DatumRef 가 실존하지 않는 datum 이름을 가리킬 때 skip 처리(MarkMeasurementDatumSkipped 와 동일 규약).
        //  기존엔 이 케이스가 게이트를 우회해 identity(무보정)로 정상 측정처럼 PASS/NG 를 냈다 — 원인이 로그에도 안 남았음.
        private void MarkMeasurementDatumRefMissing(MeasurementBase meas) {
            meas.ClearResult();
            meas.LastSkipReason = SkipReason.DATUM_REF_MISSING;
            meas.LastJudgement = false; // skip 도 NG 강도(기존 규약 동일)
            string measName = meas.MeasurementName;
            if (measName == null) measName = meas.TypeName;
            string datumRef = meas.DatumRef;
            if (datumRef == null) datumRef = "";
            Logging.PrintLog((int)ELogType.Error, "[FAIMeasurement] Measurement '" + measName + "' skipped — DatumRef '" + datumRef + "' 에 해당하는 Datum 이 레시피에 없음 (오타/개명/삭제 확인 필요, " + meas.LastSkipReason + ")");
        }

        //260722 hbk Phase 68 D-05: DualImage 측정의 ZIndexA/ZIndexB 오설정 판정 — 단일설정/동일값/존재하지 않는
        //  z_index 참조 → true. 호출부가 이미 "둘 중 하나라도 설정됨"을 확인한 뒤에만 호출한다(둘 다 -1 미설정인
        //  기존 레시피는 이 검사 자체를 타지 않음 — D-07 회귀 0). 조용한 폴백(ResolveDatumModelPath 의 Shots[0] 류) 금지.
        private bool IsZIndexMisconfigured(DualImageEdgeDistanceMeasurement dualMeas, InspectionSequence parentSeq2)
        {
            bool bAUnset = dualMeas.ZIndexA == UNSET_ZINDEX;
            bool bBUnset = dualMeas.ZIndexB == UNSET_ZINDEX;
            bool bSingleSet = bAUnset != bBUnset;
            if (bSingleSet)
            {
                return true;
            }
            bool bSameValue = dualMeas.ZIndexA == dualMeas.ZIndexB;
            if (bSameValue)
            {
                return true;
            }
            bool bAExists = parentSeq2 != null && parentSeq2.DoesZIndexExistInRecipe(dualMeas.ZIndexA);
            bool bBExists = parentSeq2 != null && parentSeq2.DoesZIndexExistInRecipe(dualMeas.ZIndexB);
            bool bBothExist = bAExists && bBExists;
            return !bBothExist;
        }

        //260722 hbk Phase 68 D-05: MarkMeasurementDatumRefMissing 미러 — 크로스-Z 오설정 명시적 NG(조용한 폴백 금지).
        private void MarkMeasurementZIndexMisconfigured(MeasurementBase meas)
        {
            meas.ClearResult();
            meas.LastSkipReason = SkipReason.ZINDEX_MISCONFIGURED;
            meas.LastJudgement = false;
            string measName = meas.MeasurementName;
            if (measName == null) measName = meas.TypeName;
            int nZA = UNSET_ZINDEX;
            int nZB = UNSET_ZINDEX;
            var dualMeas = meas as DualImageEdgeDistanceMeasurement;
            if (dualMeas != null)
            {
                nZA = dualMeas.ZIndexA;
                nZB = dualMeas.ZIndexB;
            }
            Logging.PrintLog((int)ELogType.Error, "[FAIMeasurement] Measurement '" + measName + "' skipped — ZIndexA=" + nZA + ", ZIndexB=" + nZB + " 크로스-Z 오설정(동일값/단일설정/존재하지 않는 index, " + meas.LastSkipReason + ")");
        }

        //260722 hbk Phase 68 D-05: Datum(VerticalTwoHorizontalDualImage) ZIndexA/ZIndexB 오설정 판정 — 단일설정/동일값/
        //  존재하지 않는 z_index 참조 → true. 측정 레벨 IsZIndexMisconfigured 와 달리 호출부가 "하나라도 설정됨"을
        //  미리 걸러주지 않고 이 알고리즘의 모든 datum 에 대해 무조건 호출되므로(호출부: EStep.DatumPhase 진입 직후),
        //  둘 다 -1(미설정)인 경우를 별도로 먼저 통과시켜야 한다 — 그렇지 않으면 -1==-1 이 bSameValue 로 오판정된다
        //  (D-07 회귀 0 보장). 조용한 폴백(ResolveDatumModelPath 의 Shots[0] 류) 금지.
        private bool IsDatumZIndexMisconfigured(DatumConfig datum, InspectionSequence parentSeq)
        {
            bool bAUnset = datum.ZIndexA == UNSET_ZINDEX;
            bool bBUnset = datum.ZIndexB == UNSET_ZINDEX;
            bool bSingleSet = bAUnset != bBUnset;
            if (bSingleSet)
            {
                return true;
            }
            bool bBothUnset = bAUnset && bBUnset;
            if (bBothUnset)
            {
                return false; // 미설정(-1/-1) — 게이트 미해당, 기존 static 경로(D-07)
            }
            bool bSameValue = datum.ZIndexA == datum.ZIndexB;
            if (bSameValue)
            {
                return true;
            }
            bool bAExists = parentSeq != null && parentSeq.DoesZIndexExistInRecipe(datum.ZIndexA);
            bool bBExists = parentSeq != null && parentSeq.DoesZIndexExistInRecipe(datum.ZIndexB);
            bool bBothExist = bAExists && bBExists;
            return !bBothExist;
        }

        //260702 hbk Extract Method(Task1): datum transform 해석 (fixture 미존재/미지정 시 identity fallback), 원본 인라인 이식
        private HTuple ResolveDatumTransform(InspectionSequence parentSeq2, string datumRef) {
            HTuple transform;
            if (parentSeq2 == null || !parentSeq2.TryGetDatumTransform(datumRef, out transform)) {
                // Fixture 미존재 또는 미지정 DatumRef → identity fallback
                try {
                    HOperatorSet.HomMat2dIdentity(out transform);
                } catch {
                    transform = new HTuple();
                }
            }
            return transform;
        }

        //260702 hbk Extract Method(Task1): IDatumOriginConsumer 에 검출 datum origin/각도/원중심 주입, 원본 인라인 이식(Allman 블록 배치 보존)
        private void InjectDatumOrigin(MeasurementBase meas, InspectionSequence parentSeq2) {
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
        }

        //260702 hbk Extract Method(Task1): measurement 1건 실행 (DualImage/1-image 분기 포함), 원본 인라인 이식(finally 순서 보존)
        private bool TryExecuteMeasurement(MeasurementBase meas, HImage image, HTuple transform, double pixRes, out double resultValue, out string measError, out List<EdgeInspectionOverlay> measOverlays) {
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
            return ok;
        }

        //260722 hbk Phase 68 D-02a: 크로스-Z 저장소 키 = Shot 이름 + 측정 식별자(사이클 내 안정 문자열).
        //  Shot 이름을 포함해 서로 다른 Shot 의 동명 측정이 같은 저장소를 공유하는 충돌을 방지한다.
        private string BuildCrossZMeasurementKey(MeasurementBase meas)
        {
            string shotName = "";
            if (ShotParam != null && ShotParam.ShotName != null)
            {
                shotName = ShotParam.ShotName;
            }
            string measName = meas.MeasurementName;
            if (string.IsNullOrEmpty(measName))
            {
                measName = meas.TypeName;
            }
            return shotName + "|" + measName;
        }

        //260722 hbk Phase 68 D-02a: 크로스-Z 캡처 tick 처리 — 이 측정과 무관(bRelevant=false) / 캡처 실패
        //  (bCaptureOk=false) / 완성 여부(bCompleted) 3-state 판정. 새 grab 호출 없음 — EStep.DatumPhase(GrabSyncLock
        //  안)에서 이미 확보된 ShotParam.GetImage() 클론만 재사용(lock 위반 없음, shared-lighthandler-race 준수).
        //  완성(bCompleted=true) 이면 호출부가 TryExecuteCrossZMeasurement 로 이어간다.
        private void ProcessCrossZCaptureTick(DualImageEdgeDistanceMeasurement dualMeas, InspectionSequence parentSeq2, out bool bRelevant, out bool bCaptureOk, out bool bCompleted)
        {
            bRelevant = false;
            bCaptureOk = false;
            bCompleted = false;
            if (parentSeq2 == null || ShotParam == null)
            {
                return;
            }
            int nCurZ = parentSeq2.GetExecutionZIndex();
            bool bIsRoleA = nCurZ == dualMeas.ZIndexA;
            bool bIsRoleB = nCurZ == dualMeas.ZIndexB;
            bRelevant = bIsRoleA || bIsRoleB;
            if (!bRelevant)
            {
                return; // 이 tick 은 이 측정의 ZIndexA/B 어느 쪽도 아님 — 상태변화 없음(안전망)
            }
            string baseKey = BuildCrossZMeasurementKey(dualMeas);
            string roleKey;
            if (bIsRoleA) roleKey = baseKey + CROSS_Z_ROLE_SUFFIX_A;
            else roleKey = baseKey + CROSS_Z_ROLE_SUFFIX_B;
            using (HImage capturedImage = ShotParam.GetImage())
            {
                if (capturedImage == null)
                {
                    return; // 캡처 실패 — 호출부가 NG 처리
                }
                parentSeq2.StoreCrossZImage(roleKey, capturedImage);
                bCaptureOk = true;
            }
            string keyA = baseKey + CROSS_Z_ROLE_SUFFIX_A;
            string keyB = baseKey + CROSS_Z_ROLE_SUFFIX_B;
            bCompleted = parentSeq2.HasCrossZImage(keyA) && parentSeq2.HasCrossZImage(keyB);
        }

        //260722 hbk Phase 68 D-02a: 완성 index 실행 — 저장소의 A/B 클론을 RuntimeImageA/B 에 주입해 기존 TryExecute
        //  를 변경 없이 1회 호출한다(알고리즘 무변경). TryExecuteMeasurement 의 DualImage 분기(주입+finally Dispose)
        //  와 동일 소유권 계약 — 주입 이미지는 항상 저장소의 클론(TakeCrossZImageCopy)이라 finally 에서 안전히 Dispose.
        private bool TryExecuteCrossZMeasurement(DualImageEdgeDistanceMeasurement dualMeas, InspectionSequence parentSeq2, HTuple transform, double pixRes, out double resultValue, out string measError, out List<EdgeInspectionOverlay> measOverlays)
        {
            resultValue = 0;
            measError = null;
            measOverlays = null;
            if (parentSeq2 == null)
            {
                measError = "크로스-Z: parentSeq null";
                return false;
            }
            string baseKey = BuildCrossZMeasurementKey(dualMeas);
            HImage imgA = parentSeq2.TakeCrossZImageCopy(baseKey + CROSS_Z_ROLE_SUFFIX_A);
            HImage imgB = parentSeq2.TakeCrossZImageCopy(baseKey + CROSS_Z_ROLE_SUFFIX_B);
            bool ok;
            try
            {
                dualMeas.RuntimeImageA = imgA;
                dualMeas.RuntimeImageB = imgB;
                try
                {
                    ok = dualMeas.TryExecute(null, transform, pixRes, out resultValue, out measError, out measOverlays);
                }
                catch (Exception ex)
                {
                    ok = false; resultValue = 0; measError = ex.Message; measOverlays = null;
                }
            }
            finally
            {
                if (imgA != null) { try { imgA.Dispose(); } catch { } }
                if (imgB != null) { try { imgB.Dispose(); } catch { } }
                dualMeas.RuntimeImageA = null;
                dualMeas.RuntimeImageB = null;
            }
            return ok;
        }

        //260702 hbk Extract Method(Task2): FAI-Edge* overlay 판정 suffix 부여 + Shot/FAI overlay 누적, 원본 인라인 이식
        private void ApplyOverlaySuffixAndAccumulate(MeasurementBase meas, List<EdgeInspectionOverlay> measOverlays, List<EdgeInspectionOverlay> overlayAcc, List<EdgeInspectionOverlay> faiOverlays) {
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
        }

        //260702 hbk Extract Method(Task2): FAIConfig legacy 필드(IsPass/MeasuredValue) 집계 + QueueFaiCapture 호출, 원본 인라인 이식
        private void AggregateFaiResult(FAIConfig fai, bool faiAllPass, List<EdgeInspectionOverlay> faiOverlays, SharedHImage sharedSrc, List<DatumCaptureOverlay> datumSnapshot) {
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
                    //260618 hbk Phase 54 ALIGN-01 ALIGN_FAIL 도 skip 통계에 포함 (D-10, skip 통계 누락 방지)
                    if (m != null && (m.LastSkipReason == SkipReason.DATUM_FAIL || m.LastSkipReason == SkipReason.ALIGN_FAIL)) { wasSkip = true; break; } //260710 hbk 상수화
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
        }

        //260702 hbk Extract Method(Task2): image==null NO_IMAGE 캐스케이드 처리 (faiHadMeas 분기 보존), 원본 인라인 이식
        private void MarkAllMeasurementsNoImage(ref int measuredCount) {
            foreach (var fai in ShotParam.FAIList) {
                bool faiHadMeas = fai.Measurements.Count > 0;
                foreach (var meas in fai.Measurements) {
                    meas.ClearResult();
                    meas.LastSkipReason = SkipReason.NO_IMAGE; //260710 hbk 상수화 // UI 'DETECT FAIL' 류 + Excel export 분기 신호
                    meas.LastJudgement = false;
                    measuredCount++;
                }
                if (faiHadMeas) {
                    fai.IsPass = false;
                    fai.MeasuredValue = 0;
                    if (fai.LastOverlays != null) fai.LastOverlays.Clear();
                } else {
                    fai.ClearResult();
                    if (fai.LastOverlays != null) fai.LastOverlays.Clear();
                }
            }
            string shotName = ShotParam.ShotName;
            if (shotName == null) shotName = "";
            Logging.PrintLog((int)ELogType.Error, "[FAIMeasurement] SHOT '" + shotName + "' 검사 이미지 없음 — 모든 measurement NG 처리 (캐스케이드 차단)");
        }
    }
}
