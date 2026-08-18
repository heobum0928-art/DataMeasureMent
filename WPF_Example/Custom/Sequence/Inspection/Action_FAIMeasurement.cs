using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text; //260818 hbk [SEQ] Measure 요약의 알고리즘 종류별 집계 문자열 조립(StringBuilder)
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

        //260818 hbk 시퀀스 흐름 로그(Trace [SEQ]) 헬퍼.
        //  목적: 문제가 생겼을 때 로그만 보고 "어느 단계에서 무슨 일이 있었는지"를 재구성하고, 그 단계명으로
        //  코드(이 파일의 'case EStep.<단계명>:')를 바로 찾아갈 수 있게 하는 것. 단계명은 EStep 값과 1:1 로 맞춘다.
        //  로그 실패가 검사를 막으면 안 되므로 예외는 전부 삼킨다.
        private const char chAlgoMul = '×'; // '×' — 알고리즘 종류별 횟수 표기용

        private void LogSeqStep(string szStepName, string szDetail) {
            try {
                string szSeqName = "?";
                string szShotName = "?";
                if (ShotParam != null) {
                    if (ShotParam.ShotName != null) szShotName = ShotParam.ShotName;
                    SequenceBase seq = ShotParam.Parent as SequenceBase;
                    if (seq != null && seq.Name != null) szSeqName = seq.Name;
                }
                Logging.PrintLog((int)ELogType.Trace, "[SEQ]   {0} · {1} · [{2}] {3}",
                    szSeqName, szShotName, szStepName, szDetail);
            }
            catch { }
        }

        //260818 hbk 어떤 알고리즘 함수를 탔는지 한 줄 — 상세 수치는 Algorithm 탭, 여기는 "경로" 확인용.
        private void LogSeqAlgo(string szStepName, string szTargetName, string szAlgoPath) {
            try {
                string szTarget = szTargetName;
                if (string.IsNullOrEmpty(szTarget)) szTarget = "?";
                LogSeqStep(szStepName, string.Format("'{0}' 알고리즘 → {1}", szTarget, szAlgoPath));
            }
            catch { }
        }

        //260818 hbk [초보자용 개요] 이 Run() 메서드는 프로그램이 살아있는 동안 아주 짧은 간격(수 ms)으로 계속
        //  반복 호출됩니다. 매번 호출될 때마다 지금이 몇 번째 "단계"(Step)인지 보고, 그 단계에 해당하는
        //  case 블록 하나만 실행합니다 — 이런 구조를 "상태 머신(state machine)"이라고 부릅니다.
        //  한 Shot(사진 한 장 분량의 검사)이 끝나기까지 아래 순서로 단계가 넘어갑니다:
        //    Init(초기화) → MoveZ(높이 이동) → DatumPhase(기준점 찾기) → Grab(촬영) → Measure(측정) → End(종료)
        //  각 case 는 자기 할 일을 끝내면 `Step = (int)EStep.다음단계;` 로 다음 단계를 예약하고 `break;`로
        //  빠져나갑니다. 다음 호출 때 그 다음 단계 case 가 실행되는 식입니다. 그래서 이 메서드 안에서
        //  "루프"를 직접 도는 게 아니라, 밖에서(SequenceBase) 반복 호출해주는 걸 받아서 한 걸음씩 전진합니다.
        public override ActionContext Run() {
            switch ((EStep)Step) {
                //260818 hbk [초보자용] 검사 시작 직전 정리 단계 — 지난번 검사 결과가 남아있으면 지우고 바로 다음 단계로 넘어갑니다.
                case EStep.Init:
                    // Run 사이클 진입 시 image buffer + FAI results dispose
                    if (ShotParam != null) ShotParam.ClearAllResults();
                    Step = (int)EStep.MoveZ;
                    break;

                //260818 hbk [초보자용] 이 Shot이 필요로 하는 높이(Z)로 이동하는 단계입니다. 지금은 실제 축 이동 장치가
                //  아직 없어서(주석의 SIMUL/실장비 분기 참고) 사실상 대기만 하거나 건너뜁니다.
                case EStep.MoveZ:
                    //260818 hbk [SEQ] 단계 로그 — 대괄호 안 이름은 코드의 EStep 값과 1:1 이라, 로그에서 본 단계명으로
                    //  Action_FAIMeasurement.cs 의 'case EStep.<이름>:' 을 바로 찾아갈 수 있다.
                    LogSeqStep("MoveZ", string.Format("Z축 이동 (ZIndex={0}, Delay={1}ms)",
                        (ShotParam != null ? ShotParam.ZIndex : -1), (ShotParam != null ? ShotParam.DelayMs : 0)));
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
                //260807 hbk quick-260807: ClearDatumTransforms() 를 여기(매 Action 의 DatumPhase 진입마다)에서
                //  더 이상 호출하지 않는다 — 사이클 경계(InspectionSequence.HandleRunStartResetResults 수동 RUN /
                //  SystemHandler.StartV1Scoped z=0 프로토콜 진입, $RESET 은 기존 ResetCycleStateForProtocolReset
                //  그대로)로 이동했다. 이유: 이 case 는 시퀀스의 Shot(=Z) 수만큼 반복 실행되는데, 매번 무조건
                //  비우면 물리적으로 고정된(Z 무관) Datum 기준물까지 Shot 마다 재조명+재grab+재정렬+재검출(실측
                //  0.9~1.4초/회)하게 된다 — 아래 루프의 캐시-재사용 스킵과 짝을 이루는 변경.
                //260818 hbk [초보자용] "기준점(Datum)"을 찾는 단계입니다. 사진에서 측정을 하려면 먼저 "여기가 기준이다"라는
                //  위치를 알아야 하는데, 그걸 찾는 게 이 단계입니다. 부품이 움직이지 않는 한 기준점 위치도 안 바뀌므로,
                //  이번 검사(사이클)에서 이미 한 번 찾았으면 다시 찾지 않고 그 결과를 재사용합니다(아래 캐시 로직).
                case EStep.DatumPhase: {
                    //TEMP 계측(top-release-2x-slower 조사용, 원인 확인 후 제거): [FaiTiming]이 Grab/Measure만 재고
                    //  이 DatumPhase 단계는 안 재고 있어서, Grab+Measure 합계와 실제 사이클 전체 시간 사이의 차이가
                    //  이 단계에서 나는지 확인하기 위한 임시 로그.
                    var swDatumPhase = Stopwatch.StartNew();
                    int nDatumOk = 0, nDatumFail = 0, nDatumCached = 0; //260818 hbk [SEQ] 단계 요약용 집계
                    InspectionSequence parentSeq;
                    if (ShotParam != null) parentSeq = ShotParam.Parent as InspectionSequence;
                    else parentSeq = null;
                    LogSeqStep("DatumPhase", string.Format("기준점 검출 — 등록 Datum {0}개",
                        (parentSeq != null ? parentSeq.DatumConfigs.Count : 0)));
                    if (parentSeq != null && parentSeq.DatumConfigs.Count > 0) {
                        //260618 hbk Phase 54 ALIGN-01 이미지 회전(datumLevelOn/datumLevelAngle) 폐기 (D-03/D-05 warp 0회).
                        //  레벨링 이미지회전 → 패턴매칭 ROI 좌표변환으로 대체. 이전 datumLevelOn/datumLevelAngle 지역변수 제거.
                        foreach (var datum in parentSeq.DatumConfigs) {
                            if (datum == null) continue;
                            //260807 hbk quick-260807: 크로스-Z(ZIndexA/B 중 하나라도 설정된 DualImage) 가 아닌 Datum 은
                            //  고정 기준물이라 Z 가 바뀌어도 위치가 바뀌지 않는다 — 이번 사이클에서 이미 검출 성공해
                            //  _datumTransforms 에 값이 있으면(성공시에만 저장됨) 이 Shot 에서는 재조명/재grab/재정렬/
                            //  재검출을 전부 건너뛰고 캐시를 그대로 쓴다(실측 0.9~1.4초/회 절감 × 같은 시퀀스의 남은 Shot 수).
                            //  이전 Z 에서 검출이 "실패"했던 datum 은 캐시에 안 남으므로 여기서 그대로 다시 시도된다
                            //  (lenient 원칙 유지, D-04 요구사항). 크로스-Z 는 여러 Z 의 이미지를 조합해야 하므로
                            //  이 스킵 대상에서 완전히 제외 — 아래 분기는 기존과 동일하게 매번 재실행된다.
                            bool bIsCrossZDatum = datum.AlgorithmTypeEnum == EDatumAlgorithm.VerticalTwoHorizontalDualImage
                                && !(datum.ZIndexA == UNSET_ZINDEX && datum.ZIndexB == UNSET_ZINDEX);
                            if (!bIsCrossZDatum && parentSeq.HasCachedDatumTransform(datum.DatumName)) {
                                nDatumCached++; //260818 hbk [SEQ] 요약: 이번 사이클 이미 검출됨 → 재검출 생략
                                continue; // 이번 사이클 기 검출 성공 — skip
                            }
                            // Datum 전용 조명(SourceShotName 상속과 무관) 을 grab 직전에 켠다. 이 grab 이 끝나면
                            //  루프 종료 후 ApplyShotLights 로 되돌려야 EStep.Grab 의 측정 grab 이 Shot 조명 아래서 이뤄진다.
                            var swDatumLight = Stopwatch.StartNew(); //TEMP 계측(top-release-2x-slower 조사용, 원인 확인 후 제거)
                            parentSeq.ApplyDatumLights(datum);
                            long msApplyDatumLights = swDatumLight.ElapsedMilliseconds; //TEMP 계측
                            // 조명 명령은 큐잉만 되고 실제 전송은 백그라운드 스레드가 처리 — grab 전에 실제 반영을 기다린다.
                            //  (기존엔 수동 UI grab 경로에만 있던 대기를 자동 검사 사이클에도 배선. SIMUL/오프라인처럼
                            //  실제로 대기할 쓰기가 없으면 즉시 반환되므로 비용은 무시할 만큼 작다.)
                            var swDatumLightWait = Stopwatch.StartNew(); //TEMP 계측
                            LightHandler.Handle.WaitForPendingWrites();
                            long msWaitForPendingWrites = swDatumLightWait.ElapsedMilliseconds; //TEMP 계측
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
                                        string modelPath = InspectionSequence.ResolveDatumModelPath(datum, parentSeq.Name); // 260723 hbk quick-fix: 전역 Shots[0] 폴백 결함 수정 — 소유 시퀀스명 명시 전달 (D-07)
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
                                var swDatumGrab = Stopwatch.StartNew(); //TEMP 계측(top-release-2x-slower 조사용, 원인 확인 후 제거)
                                HImage img = GrabOrLoadDatumImage(datum);
                                long msDatumGrab = swDatumGrab.ElapsedMilliseconds; //TEMP 계측
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
                                var swDatumDetect = Stopwatch.StartNew(); //TEMP 계측(top-release-2x-slower 조사용, 원인 확인 후 제거)
                                try {
                                    if (datum.IsPatternAlignEnabled) {
                                        string modelPath = InspectionSequence.ResolveDatumModelPath(datum, parentSeq.Name); // 260723 hbk quick-fix: 전역 Shots[0] 폴백 결함 수정 — 소유 시퀀스명 명시 전달 (D-07)
                                        string alignErr;
                                        if (!parentSeq.TryComposeAlign(datum, img, modelPath, out alignErr)) {
                                            string dn = datum.DatumName;
                                            if (dn == null) dn = "";
                                            string ae = alignErr;
                                            if (ae == null) ae = "";
                                            Logging.PrintLog((int)ELogType.Error, "[FAIMeasurement] Datum '" + dn + "' 패턴매칭 실패 (ALIGN_FAIL, skip): " + ae);
                                            datum.RuntimeDetectFailed = true;
                                            parentSeq.MarkAlignFailed(datum.DatumName); // D-10 lenient — 측정 NG(ALIGN_FAIL) 강제, abort 안 함
                                            nDatumFail++;
                                        } else {
                                            nDatumOk++;
                                        }
                                        //260818 hbk [SEQ] 패턴매칭 경로를 탔음을 표시
                                        LogSeqAlgo("Datum", datum.DatumName, "TryComposeAlign(패턴매칭)");
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
                                            nDatumFail++;
                                        } else {
                                            nDatumOk++;
                                        }
                                        //260818 hbk [SEQ] 어떤 검출 알고리즘을 탔는지 표시 — 알고리즘 탭에서 상세 확인
                                        LogSeqAlgo("Datum", datum.DatumName, "TryRunSingleDatum/" + datum.AlgorithmTypeEnum);
                                    }
                                    //TEMP 계측(top-release-2x-slower 조사용, 원인 확인 후 제거): thread=MemCacheWarmup의 seq 스레드ID와 대조용, dbg=디버거 부착 여부 직접 확인
                                    Logging.PrintLog((int)ELogType.Algorithm, "[FaiTiming] datum={0} stage=DatumDetail light={1}ms lightWait={2}ms grab={3}ms detect={4}ms thread={5} dbg={6}",
                                        (datum.DatumName != null ? datum.DatumName : "?"), msApplyDatumLights, msWaitForPendingWrites, msDatumGrab, swDatumDetect.ElapsedMilliseconds,
                                        System.Threading.Thread.CurrentThread.ManagedThreadId, System.Diagnostics.Debugger.IsAttached);
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
                    //260818 hbk [SEQ] DatumPhase 결과 요약 (tact 포함)
                    LogSeqStep("DatumPhase", string.Format("완료 — 검출성공 {0} / 실패 {1} / 캐시재사용 {2} ({3:F2}초)",
                        nDatumOk, nDatumFail, nDatumCached, swDatumPhase.Elapsed.TotalSeconds));
                    //TEMP 계측(top-release-2x-slower 조사용, 원인 확인 후 제거)
                    Logging.PrintLog((int)ELogType.Algorithm, "[FaiTiming] shot={0} stage=Datum total={1}ms",
                        (ShotParam != null ? ShotParam.ShotName : "?"), swDatumPhase.ElapsedMilliseconds);
                    if (bDatumOnly) {
                        Step = (int)EStep.End;
                    } else {
                        Step = (int)EStep.Grab; // datum 부분 실패해도 측정 진행
                    }
                    break;
                }

                //260818 hbk [초보자용] 실제로 카메라로 사진을 찍는(또는 SIMUL 모드면 저장된 사진을 불러오는) 단계입니다.
                //  이미 이 Shot이 사진을 갖고 있으면(HasImage) 다시 찍지 않고 그대로 다음 단계로 넘어갑니다.
                case EStep.Grab:
                    if (ShotParam != null && !ShotParam.HasImage) {
                        //TEMP 계측(top-release-2x-slower 조사용, 원인 확인 후 제거): grab/load 순수시간 vs 표시사본 복사시간 분리
                        var swGrabTotal = Stopwatch.StartNew();
                        long msAcquire = 0;
                        long msDisplayCopy = 0;
                        HImage image = null;
                        bool bIsLiveGrabAttempt = false;   //260811 hbk plc-spec-260811-alignment: 실기 카메라 grab 여부(하드웨어 에러=E 판정용) — SIMUL_MODE/OfflineInspectMode 경로에선 절대 true 로 세팅 안 함
                        // ShotParam.SimulImagePath = InspectionImagePath 역할 (검사 사이클 마다 로드). 티칭 기준 이미지는 별도 DatumConfig.TeachingImagePath (셋업 시 1회, INI 보존) 사용 — 역할 분리. Simul 에서 두 경로 동일 파일 가능.
                        var swAcquire = Stopwatch.StartNew();
                        #if SIMUL_MODE
                        image = LoadShotInspectionImage(); // SIMUL: 항상 저장 이미지(ShotParam.SimulImagePath) 로드
                        #else
                        if (SystemSetting.Handle.OfflineInspectMode) {
                            // 오프라인(수동 지그): 라이브 grab 대신 노드 저장 이미지 로드. 각 SHOT 이 자기 Z 이미지라 정합 성립.
                            image = LoadShotInspectionImage();
                        } else {
                            bIsLiveGrabAttempt = true;
                            // quick-260813-jnh: Shot 검사이미지 grab — 참조 DatumRef 로 소유 Datum 을 역추적해 MIL 미러 방향을 결정한다.
                            InspectionSequence parentSeqForMirror = ShotParam.Parent as InspectionSequence;   // :283 에 동일 선례
                            bool bShotMirrorX = false;
                            bool bShotMirrorY = false;
                            if (parentSeqForMirror != null) parentSeqForMirror.ResolveShotGrabMirror(ShotParam, out bShotMirrorX, out bShotMirrorY);
                            string szShotRoleId = DeviceHandler.BuildGrabRoleIdentifier(ShotParam.DeviceName, bShotMirrorX, bShotMirrorY);
                            image = SystemHandler.Handle.Devices.GrabHalconImage(ShotParam, szShotRoleId);
                        }
                        #endif
                        msAcquire = swAcquire.ElapsedMilliseconds;
                        //260811 hbk plc-spec-260811-alignment: 실기 grab 이 null 을 반환한 경우만(SIMUL_MODE/오프라인
                        //  경로는 절대 해당 없음) 사이클 하드웨어 에러로 마킹 — $RESULT 응답이 F 대신 E 로 나간다
                        //  (제어팀 확정 스펙). Step 은 그대로 Measure 로 진행(기존 lenient 동작 유지, 회귀 0).
                        if (image == null && bIsLiveGrabAttempt) {
                            InspectionSequence parentSeqForHwErr = ShotParam.Parent as InspectionSequence;
                            if (parentSeqForHwErr != null) parentSeqForHwErr.MarkCycleHardwareError();
                            Logging.PrintLog((int)ELogType.Error, "[FAIMeasurement] SHOT '" + (ShotParam.ShotName ?? "") + "' 실기 카메라 grab 실패 — $RESULT E(하드웨어 에러) 처리");
                        }
                        if (image != null) {
                            //260618 hbk Phase 54 ALIGN-01 측정 이미지 회전(레벨링 warp) 폐기 (D-03/D-05 warp 0회).
                            //  레벨링 이미지회전 → 패턴매칭 ROI 좌표변환으로 대체. 측정은 보정 전 원본 픽셀에서 수행.
                            ShotParam.SetImage(image); // 측정 소스(데이터 경로) — 표시 설정과 무관하게 항상 설정한다.
                            //260810 hbk quick-260810-egx: 아래는 "표시 전용" 사본(127MP memcpy). 자동검사 중 표시를 끄면 생략한다.
                            InspectionSequence parentSeqForView;
                            if (ShotParam != null) parentSeqForView = ShotParam.Parent as InspectionSequence;
                            else parentSeqForView = null;
                            bool bSkipViewer = IsViewerUpdateSkipped(parentSeqForView);
                            if (pMyContext.ResultHalconImage != null) pMyContext.ResultHalconImage.Dispose();
                            var swDisplayCopy = Stopwatch.StartNew();
                            if (bSkipViewer) {
                                pMyContext.ResultHalconImage = null; // 표시사본 미생성. HalconImageBridge.Clone(null)==null 이라 뒤쪽은 조용히 no-op.
                            } else {
                                pMyContext.ResultHalconImage = image.CopyImage();
                            }
                            msDisplayCopy = swDisplayCopy.ElapsedMilliseconds;
                            image.Dispose(); // 누수 방지 — 조건과 무관하게 항상 수행.
                        }
                        Logging.PrintLog((int)ELogType.Algorithm,
                            "[FaiTiming] shot={0} stage=Grab acquire={1}ms displayCopy={2}ms total={3}ms",
                            ShotParam.ShotName ?? "", msAcquire, msDisplayCopy, swGrabTotal.ElapsedMilliseconds);
                        //260818 hbk [SEQ] Grab 단계 요약 (tact 포함)
                        LogSeqStep("Grab", string.Format("검사 이미지 촬영 완료 ({0:F2}초)",
                            swGrabTotal.Elapsed.TotalSeconds));
                    }
                    Step = (int)EStep.Measure;
                    break;

                //260818 hbk [초보자용] 찍은 사진 위에서 진짜 "측정"을 하는 단계입니다. 이 Shot에 등록된 FAI(검사 그룹)와
                //  그 안의 개별 측정 항목(길이/각도/지름 등)을 하나씩 돌면서 값을 재고, 공차(허용 범위) 안에 드는지
                //  판정합니다. 하나라도 공차를 벗어나면 이 Shot 전체가 불합격(NG)으로 표시됩니다.
                case EStep.Measure: {
                    LogSeqStep("Measure", string.Format("측정 시작 — FAI {0}개",
                        (ShotParam != null && ShotParam.FAIList != null ? ShotParam.FAIList.Count : 0))); //260818 hbk [SEQ]
                    var dctAlgoUsed = new Dictionary<string, int>(); //260818 hbk 이번 Shot 에서 실제로 탄 측정 알고리즘 종류별 횟수
                    int nMeasNg = 0;                                  //260818 hbk 공차 벗어난 측정 수
                    //TEMP 계측(top-release-2x-slower 조사용, 원인 확인 후 제거): Shot 단위 Measure 단계 breakdown
                    var swMeasureTotal = Stopwatch.StartNew();
                    long msMeasureExec = 0;  // TryExecuteMeasurement/TryExecuteCrossZMeasurement 순수 실행시간 누적
                    long msSaveQueue = 0;    // AggregateFaiResult(결과이미지 저장 큐 enqueue 포함) 누적
                    InspectionSequence parentSeq2;
                    if (ShotParam != null) parentSeq2 = ShotParam.Parent as InspectionSequence;
                    else parentSeq2 = null;
                    bool allPass = true;
                    int measuredCount = 0;
                    var overlayAcc = new List<EdgeInspectionOverlay>(); // Shot 단위 overlay 누적
                    //260729 hbk quick-fix(260729-hwb): Shot 전체에서 표시 이미지(pMyContext.ResultHalconImage)를
                    //  크로스-Z role 이미지로 이미 교체했는지 여부 — 첫 크로스-Z 캡처가 화면을 차지한다(결정론적 규칙).
                    bool bShotDisplayImageReplaced = false;
                    if (ShotParam != null) {
                        using (var image = ShotParam.GetImage()) {
                            if (image != null) {
                                // Shot당 1회 복사 공유(refcount). 검사 스레드의 FAI별 대용량 CopyImage 제거(throughput).
                                // 한 Shot 의 모든 FAI origin/capture 요청이 이 1개 복사본을 공유. capSaver 없으면 복사 자체 생략.
                                var capSaver = SystemHandler.Handle.CaptureImageSaver;
                                SharedHImage sharedSrc = null;
                                if (capSaver != null) { try { sharedSrc = new SharedHImage(image.CopyImage()); } catch { sharedSrc = null; } }
                                //260810 hbk quick-debug(capture-render-per-fai-slow) round4 fix: try/finally 를 sharedSrc
                                //  생성 직후로 넓혔다(기존엔 BuildDatumCaptureSnapshot/GetEffectivePixelResolution/
                                //  QueueSharedShotOrigin 이 try 밖에 있어, 이 구간에서 예외가 나면 sharedSrc(ref 1, 검사
                                //  루프 소유)가 release 안 되고 새는 결함이 있었다 — QueueSharedShotOrigin 이 이미
                                //  AddRef 까지 해둔 뒤 예외가 나면 ref 2개가 동시에 샐 수 있었다).
                                try {
                                // datum 검출 오버레이 스냅샷(시퀀스 단위, 전 FAI 공유). 값만 추출해 워커 async race 차단.
                                List<DatumCaptureOverlay> datumSnapshot = BuildDatumCaptureSnapshot(parentSeq2);
                                //260619 hbk per-shot 보정계수 적용 = PixelResolution × CorrectionFactor (단일소스 GetEffectivePixelResolution). PixelResolution 저장값 불변.
                                double pixRes = ShotParam != null ? ShotParam.GetEffectivePixelResolution() : 1.0; //260615 hbk Phase 42 D-01 Shot 단일소스
                                // (구) ±2% 가드레일 경고 제거 — CorrectionFactor 를 배율 보정(예: 0.72)까지 포함한 단일 보정 knob 으로
                                //  운용하기로 결정. ±2% 초과가 정상 사용이 되어 매 검사 Error 로그를 헛되이 채우던 노이즈였음(로그 전용, 검사 영향 0).
                                //260807 hbk quick-debug(top-z1-measure-8sec-slow) fix: 원본(origin) 이미지는 이 Shot 의 모든 FAI 가
                                //  sharedSrc 를 통해 완전히 동일한 내용을 참조한다 — FAI 마다 반복 저장(127MP 기준 실측 ~1초/장)하지
                                //  않고 Shot 당 1회만 큐에 넣는다. 큐 상한(50=FAI 25개분)을 넘는 FAI 부터 저장 대기가 걸리던 원인.
                                string szSharedOriginPath = QueueSharedShotOrigin(sharedSrc, parentSeq2);
                                foreach (var fai in ShotParam.FAIList) {
                                    bool faiAllPass = true;
                                    var faiOverlays = new List<EdgeInspectionOverlay>(); // per-FAI overlay 누적 (LastOverlays write-back 용, 노드 클릭 재현)
                                    //260729 hbk quick-fix(260729-hwb): 이 FAI tick 에서 실제로 캡처된 크로스-Z role 이미지의
                                    //  소유 사본(같은 FAI 안에서 첫 캡처가 이김). null 이면 AggregateFaiResult 는 종전과
                                    //  동일하게 sharedSrc 를 쓴다(비-크로스-Z 회귀 0). 새 필드 아님 — per-FAI 지역변수.
                                    HImage crossZRoleImage = null;
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
                                            string szCapturedRoleKey;
                                            ProcessCrossZCaptureTick(dualMeasForGate, parentSeq2, out bRelevant, out bCaptureOk, out bCompleted, out szCapturedRoleKey);
                                            //260729 hbk quick-fix(260729-e9q): 비프로토콜 사이클(RUN 버튼/일괄검사,
                                            //  RequestPacket==null)은 이 Shot 의 EStep.Measure 가 이번 tick 단 한 번뿐이고
                                            //  GetExecutionZIndex() 도 항상 0 이라 크로스-Z 짝이 절대 완성되지 않는다 —
                                            //  조용히 continue 하면 측정한 적 없는 항목이 PASS 로 집계된다(안전 결함).
                                            //  프로토콜 사이클(RequestPacket!=null)은 다음 z tick 에서 짝이 완성되므로
                                            //  기존 defer 동작을 그대로 유지한다(회귀 0 하드 요구).
                                            //  parentSeq2==null 은 여기 도달 불가(IsZIndexMisconfigured 가 먼저 걸러냄)이나,
                                            //  도달하더라도 짝 완성 경로가 없으므로 비프로토콜과 동일하게 NG 로 본다.
                                            bool bNonProtocolCycle = parentSeq2 == null || !parentSeq2.IsProtocolDrivenCycle();
                                            if (!bRelevant)
                                            {
                                                if (bNonProtocolCycle)
                                                {
                                                    MarkMeasurementCrossZIncomplete(meas, false, false, parentSeq2);
                                                    faiAllPass = false;
                                                    measuredCount++;
                                                }
                                                continue; // 프로토콜: 이 tick 은 이 측정과 무관 — 상태변화 없음(안전망, 무변경)
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
                                            if (bCaptureOk && crossZRoleImage == null && !string.IsNullOrEmpty(szCapturedRoleKey) && parentSeq2 != null)
                                            {
                                                //260729 hbk quick-fix(260729-hwb): 이번 tick 에서 실제로 캡처된 role 이미지의
                                                //  소유 사본을 받아둔다(같은 FAI 안에서 첫 캡처가 결정론적으로 이긴다).
                                                //  AggregateFaiResult 의 표시/저장 소스로 sharedSrc 대신 사용된다(아래).
                                                crossZRoleImage = parentSeq2.TakeCrossZImageCopy(szCapturedRoleKey);
                                            }
                                            if (!bCompleted)
                                            {
                                                if (bNonProtocolCycle)
                                                {
                                                    MarkMeasurementCrossZIncomplete(meas, true, false, parentSeq2);
                                                    faiAllPass = false;
                                                }
                                                else
                                                {
                                                    //260729 hbk quick-fix(260729-hwb): 프로토콜 사이클(수동 Z트리거 포함)도
                                                    //  짝이 아직 미완성인 tick 에서 faiAllPass 기본값 true 로 방치하지 않고
                                                    //  CROSS_Z_INCOMPLETE 로 명시 표시한다(T-HWB-01). AddFaiResult 의 완성
                                                    //  index 게이트가 미완성 index 를 애초에 보고 대상에서 제외하므로 PLC
                                                    //  응답은 오염되지 않는다 — 영향 범위는 화면/캡처 파일명/cycle.json 뿐.
                                                    MarkMeasurementCrossZIncomplete(meas, true, true, parentSeq2);
                                                    faiAllPass = false;
                                                }
                                                measuredCount++; // 프로토콜 Z1(비완성 index): 캡처만 — NG 아님, 미보고(Task4 index 게이트가 보장)
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
                                        var swMeasureExec = Stopwatch.StartNew(); //TEMP 계측(top-release-2x-slower): HALCON 측정 실행 순수시간
                                        if (bHasAnyZIndex)
                                        {
                                            ok = TryExecuteCrossZMeasurement(dualMeasForGate, parentSeq2, transform, pixRes, out resultValue, out measError, out measOverlays); //260722 hbk Phase 68 D-02a: 완성 index 크로스-Z 실행
                                        }
                                        else
                                        {
                                            ok = TryExecuteMeasurement(meas, image, transform, pixRes, out resultValue, out measError, out measOverlays); //260702 hbk Extract Method(Task1)
                                        }
                                        msMeasureExec += swMeasureExec.ElapsedMilliseconds;
                                        //260818 hbk 어떤 측정 알고리즘을 탔는지 — Shot 요약용 집계 + Algorithm 탭 상세 1줄
                                        string szAlgoType = meas.TypeName;
                                        if (string.IsNullOrEmpty(szAlgoType)) szAlgoType = "?";
                                        string szAlgoEntry;
                                        if (bHasAnyZIndex) szAlgoEntry = "TryExecuteCrossZMeasurement";
                                        else szAlgoEntry = "TryExecuteMeasurement";
                                        if (dctAlgoUsed.ContainsKey(szAlgoType)) dctAlgoUsed[szAlgoType]++;
                                        else dctAlgoUsed[szAlgoType] = 1;
                                        Logging.PrintLog((int)ELogType.Algorithm, "[ALGO] {0} · {1} type={2} → {3} ({4}) {5}ms",
                                            (ShotParam != null ? ShotParam.ShotName : "?"),
                                            (meas.MeasurementName ?? szAlgoType), szAlgoType, szAlgoEntry,
                                            (ok ? "OK" : "FAIL"), swMeasureExec.ElapsedMilliseconds);
                                        if (ok) {
                                            meas.EvaluateJudgement(resultValue);
                                            if (!meas.LastJudgement) nMeasNg++; //260818 hbk [SEQ] 요약용 공차이탈 집계
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
                                    try {
                                        if (crossZRoleImage == null) {
                                            var swSaveQueue = Stopwatch.StartNew(); //TEMP 계측(top-release-2x-slower): 결과이미지 저장 큐 enqueue 시간
                                            AggregateFaiResult(fai, faiAllPass, faiOverlays, sharedSrc, datumSnapshot, szSharedOriginPath); //260702 hbk Extract Method(Task2)
                                            msSaveQueue += swSaveQueue.ElapsedMilliseconds;
                                        } else {
                                            //260729 hbk quick-fix(260729-hwb): 크로스-Z tick 의 캡처/저장 소스를 정적 sharedSrc
                                            //  대신 실제 측정에 쓰인 role 이미지로 교체한다(T-HWB-02). sharedSrc 의 기존 소유권
                                            //  계약(L284-285/L443-445, AddRef/Release, 워커 요청과 독립)을 그대로 미러한다.
                                            //260807 hbk quick-debug(top-z1-measure-8sec-slow) fix: 크로스-Z 는 FAI 마다 실제로
                                            //  다른 role 이미지를 참조할 수 있어 Shot 공유 origin 재사용 대상이 아니다 — origin path
                                            //  null 로 넘겨 기존과 동일하게 FAI 마다 개별 저장(회귀 0).
                                            SharedHImage crossZSharedSrc = null;
                                            try {
                                                try { crossZSharedSrc = new SharedHImage(crossZRoleImage.CopyImage()); } catch { crossZSharedSrc = null; }
                                                var swSaveQueueCrossZ = Stopwatch.StartNew(); //TEMP 계측(top-release-2x-slower): 결과이미지 저장 큐 enqueue 시간
                                                AggregateFaiResult(fai, faiAllPass, faiOverlays, crossZSharedSrc, datumSnapshot, null);
                                                msSaveQueue += swSaveQueueCrossZ.ElapsedMilliseconds;
                                            } finally {
                                                if (crossZSharedSrc != null) crossZSharedSrc.Release();
                                            }
                                            //260810 hbk quick-260810-egx: 표시 전용 교체 블록 — 자동검사 중 표시를 끄면 통째로 건너뛴다
                                            //  (127MP memcpy 제거). 위 AggregateFaiResult/crossZSharedSrc 저장 경로와 아래 finally 의
                                            //  crossZRoleImage.Dispose() 는 조건과 무관하게 그대로 수행된다.
                                            if (!bShotDisplayImageReplaced && !IsViewerUpdateSkipped(parentSeq2)) {
                                                //260729 hbk quick-fix(260729-hwb): Shot 전체에서 첫 크로스-Z 캡처가 화면을
                                                //  차지한다(결정론적 규칙) — 정적 대표 사진 대신 실제 측정 사진을 표시.
                                                if (pMyContext.ResultHalconImage != null) pMyContext.ResultHalconImage.Dispose();
                                                pMyContext.ResultHalconImage = crossZRoleImage.CopyImage();
                                                bShotDisplayImageReplaced = true;
                                            }
                                        }
                                        if (!faiAllPass) allPass = false;
                                    } finally {
                                        if (crossZRoleImage != null) { try { crossZRoleImage.Dispose(); } catch { } crossZRoleImage = null; }
                                    }
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
                    //TEMP 계측(top-release-2x-slower 조사용, 원인 확인 후 제거): Shot 단위 Measure 단계 breakdown 로그
                    string szMeasureShotName;
                    if (ShotParam != null) szMeasureShotName = ShotParam.ShotName ?? "";
                    else szMeasureShotName = "";
                    //TEMP 계측: sleep5=시퀀스 루프의 Sleep(5) 실측치 — 타이머 해상도 실효 상태 판별(1ms 살아있으면 ~5-6ms, 회수됐으면 ~15-16ms)
                    double dLastSleepMs;
                    SequenceBase measureSeq = null;
                    if (ShotParam != null) measureSeq = ShotParam.Parent as SequenceBase;
                    if (measureSeq != null) dLastSleepMs = measureSeq.LastSleepMs;
                    else dLastSleepMs = -1.0;
                    //260818 hbk [SEQ] Measure 결과 요약 — 어떤 알고리즘을 몇 번 탔는지까지 한 줄에
                    var sbAlgo = new StringBuilder();
                    foreach (var kv in dctAlgoUsed) {
                        if (sbAlgo.Length > 0) sbAlgo.Append(", ");
                        sbAlgo.Append(kv.Key).Append(chAlgoMul).Append(kv.Value);
                    }
                    LogSeqStep("Measure", string.Format("완료 — 측정 {0}개 (공차이탈 {1}개), 판정 {2} ({3:F2}초) │ 알고리즘: {4}",
                        measuredCount, nMeasNg, (allPass ? "OK" : "NG"), swMeasureTotal.Elapsed.TotalSeconds,
                        (sbAlgo.Length > 0 ? sbAlgo.ToString() : "없음")));
                    Logging.PrintLog((int)ELogType.Algorithm,
                        "[FaiTiming] shot={0} stage=Measure measuredCount={1} measureExec={2}ms saveQueueEnqueue={3}ms total={4}ms thread={5} dbg={6} sleep5={7:F1}ms",
                        szMeasureShotName, measuredCount, msMeasureExec, msSaveQueue, swMeasureTotal.ElapsedMilliseconds,
                        System.Threading.Thread.CurrentThread.ManagedThreadId, System.Diagnostics.Debugger.IsAttached, dLastSleepMs); //TEMP 계측(top-release-2x-slower 조사용, 원인 확인 후 제거)
                    Step = (int)EStep.End;
                    break;
                }

                //260818 hbk [초보자용] 이 Shot의 검사가 다 끝났다고 알리는 마지막 단계입니다. Measure 단계에서 하나라도
                //  불합격이 있었으면 전체를 Fail로, 전부 합격이면 Pass로 확정해서 이 Action을 종료합니다.
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
                // quick-260813-jnh: Datum 검출 grab — datum 객체를 손에 쥐고 있으므로 MirrorX/Y 를 직접 읽는다(역추적 불필요).
                bool bDatumMirrorX = false;
                bool bDatumMirrorY = false;
                if (datum != null) {
                    bDatumMirrorX = datum.MirrorX;
                    bDatumMirrorY = datum.MirrorY;
                }
                string szDatumRoleId = DeviceHandler.BuildGrabRoleIdentifier(ShotParam.DeviceName, bDatumMirrorX, bDatumMirrorY);
                image = SystemHandler.Handle.Devices.GrabHalconImage(ShotParam, szDatumRoleId);
                //260811 hbk plc-spec-260811-alignment: 이 분기는 실기 grab 만 도달(SIMUL_MODE/오프라인 배제) —
                //  null 이면 카메라 하드웨어 에러로 마킹, $RESULT 응답이 F 대신 E 로 나간다(제어팀 확정 스펙).
                if (image == null) {
                    InspectionSequence parentSeqForHwErr = ShotParam.Parent as InspectionSequence;
                    if (parentSeqForHwErr != null) parentSeqForHwErr.MarkCycleHardwareError();
                    string dNameForLog = "";
                    if (datum != null) dNameForLog = datum.DatumName ?? "";
                    Logging.PrintLog((int)ELogType.Error, "[Datum] '" + dNameForLog + "' 실기 카메라 grab 실패 — $RESULT E(하드웨어 에러) 처리");
                }
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

        //260810 hbk quick-260810-egx: 자동검사(TCP $PREP/$TEST) 사이클에서 "표시 전용" 127MP 사본 생성을 생략할지 판단.
        //  true 면 pMyContext.ResultHalconImage 를 만들지 않는다(Shot 당 memcpy 2회 제거: 여기 + SequenceContext.CopyFrom clone).
        //  세 조건 AND:
        //   1) 프로토콜(자동) 사이클 — 수동 RUN / 티칭 / 일괄검사(RepeatRun/BatchRun)는 RequestPacket==null 이라 항상 false → 표시 유지.
        //   2) 설정 DisableViewerDuringAutoInspect 가 ON (opt-in, 기본 false = 기존 동작).
        //   3) SaveFailImage 가 OFF — 이게 핵심 가드다. SaveFailImage 가 ON 이면 SequenceBase.SaveResultImage 가
        //      Context.ResultHalconImage 를 실제 "저장 소스"(데이터 경로)로 사용하므로, 표시사본을 없애면 결과이미지 저장이
        //      조용히 깨진다. 이 가드로 데이터 경로 영향 0 을 코드로 보장한다.
        //  Setting 이 null 인 극단 상황은 false(=기존 동작 유지) 로 폴백한다.
        //  주의: 이 판단은 "화면 표시"에만 적용된다 — capture/original 저장(QueueFaiCapture/CaptureImageSaveService)은 무관하며
        //        OK/NG 전부 기존과 동일하게 저장된다.
        private bool IsViewerUpdateSkipped(InspectionSequence parentSeq) {
            if (parentSeq == null) return false;
            if (!parentSeq.IsProtocolDrivenCycle()) return false;
            SystemSetting setting = SystemSetting.Handle;
            if (setting == null) return false;
            if (!setting.DisableViewerDuringAutoInspect) return false;
            if (setting.SaveFailImage) return false;
            return true;
        }

        // DualImage 변형용 두 이미지 동시 로드 (per-datum).
        //  ZIndexA/ZIndexB 둘 다 설정(-1 아님) → 크로스-Z 라이브 캡처 경로(D-06). 미설정(-1/-1) → 기존 static 경로(D-07 회귀 0).
        //  bPending=true 는 "실패 아님, 다음 z_index 대기"(D-02a) — 호출부가 MarkDatumFailed 를 걸지 않는 근거.
        //260724 hbk quick-fix(재발, SIDE_SHOT_F9/Side_Datum_4): 수동(RUN/Repeat/Batch) 경로는 RequestPacket==null
        //  이라 GetExecutionZIndex() 가 항상 0 을 반환(D-08 안전 폴백)해 이 datum의 ZIndexA/ZIndexB(예: 7/8, 11/12)와
        //  결코 일치할 수 없다 — 크로스-Z tick 상태기계가 구조적으로 완주 불가능. 방치 시 datum 이 매 RUN 마다 조용히
        //  skip(bPending, MarkDatumFailed 미설정)되어 DatumConfig.DetectedOriginRow/Col/RefAngle/RefAngle2(직전 성공
        //  검출 잔여값 — 몇 시간/며칠 전일 수 있음)이 InjectDatumOrigin 을 통해 아무 실패 표시 없이 재사용된다
        //  (m_dicCrossZImages 케이스와 동일 계열의 stale 재사용, 계층만 다름 — 오늘자 HandleRunStartResetResults 의
        //  BeginCrossZImageCycle() 클린 슬레이트 조치로는 커버되지 않는다: 저장소가 비어있어도 nCurZ 가 애초에
        //  ZIndexA/B 와 매칭될 수 없으므로 여전히 bPending=true 로 매번 skip 된다). 프로토콜 $TEST/DebugManualZTrigger
        //  (Custom/SystemHandler.cs, RequestPacket!=null, 실제 z_index 스텝)는 완전히 무변경 — 수동 경로만 tick 게이트를
        //  우회해 기존 static 경로(TeachingImagePath/_Vertical, 무 z 의존)로 동기 취득한다. TeachingImagePath/_Vertical 이
        //  비어 있으면 SHOT 검사이미지(ShotParam.SimulImagePath)로 폴백하고(1-image datum 경로와 동일 패턴), 그것마저
        //  없을 때만 TryLoadStaticDualDatumImages 가 false 를 반환해 명시적 DETECT FAIL/NG 로 이어진다(조용한 stale 재사용 금지).
        private bool TryGrabOrLoadDualDatumImages(DatumConfig datum, InspectionSequence parentSeq, out HImage imageHorizontal, out HImage imageVertical, out bool bPending) {
            imageHorizontal = null;
            imageVertical = null;
            bPending = false;
            if (datum == null) {
                Logging.PrintErrLog((int)ELogType.Error, "[Datum] DualImage: datum 이 null 입니다.");
                return false;
            }
            bool bIsProtocolDriven = parentSeq != null && parentSeq.IsProtocolDrivenCycle();
            bool bCrossZEnabled = bIsProtocolDriven && datum.ZIndexA != UNSET_ZINDEX && datum.ZIndexB != UNSET_ZINDEX;
            if (bCrossZEnabled) {
                return TryGrabOrLoadCrossZDatumImages(datum, parentSeq, out imageHorizontal, out imageVertical, out bPending);
            }
            return TryLoadStaticDualDatumImages(datum, out imageHorizontal, out imageVertical);
        }

        // 기존 static teaching 파일 로드 경로 (ZIndexA/B 미설정, D-07 회귀 0).
        //  imageHorizontal: datum.TeachingImagePath  → 부재/로드실패 시 ShotParam.SimulImagePath 폴백
        //  imageVertical:   datum.TeachingImagePath_Vertical → 부재/로드실패 시 동일 ShotParam.SimulImagePath 폴백
        //  폴백은 1-image datum 경로(LoadDatumImageFromPath)가 이미 쓰던 것과 동일한 것을 재사용한다 — SIMUL/오프라인
        //  통신테스트에서 datum 티칭 이미지 미확보만으로 $TEST 가 즉시 F 로 끝나 프로토콜 왕복 검증 자체가 막히던
        //  문제를 닫기 위함. 두 축이 같은 SHOT 검사이미지 한 장을 쓰는 것은 271줄 "Simul 에서 두 경로 동일 파일 가능"
        //  전제와 동일. SimulImagePath 조차 없으면 종전대로 false(명시적 DETECT FAIL/NG).
        //  주의: 두 out 파라미터는 호출부 finally 에서 각각 Dispose 되므로 폴백 시에도 반드시 서로 다른 HImage
        //  인스턴스여야 한다 — LoadDatumImageFromPath 를 두 번 호출해 각각 새로 생성한다(참조 공유 금지).
        private bool TryLoadStaticDualDatumImages(DatumConfig datum, out HImage imageHorizontal, out HImage imageVertical) {
            imageHorizontal = null;
            imageVertical = null;
            string pathH = datum.TeachingImagePath;
            string pathV = datum.TeachingImagePath_Vertical;

            bool bFallbackH = string.IsNullOrEmpty(pathH) || !File.Exists(pathH);
            bool bFallbackV = string.IsNullOrEmpty(pathV) || !File.Exists(pathV);

            imageHorizontal = LoadDatumImageFromPath(datum, pathH, false); // teachingPath → SimulImagePath 폴백 (grab 없음)
            imageVertical = LoadDatumImageFromPath(datum, pathV, false);   // 동일 폴백, 별도 인스턴스

            if (imageHorizontal == null) {
                Logging.PrintErrLog((int)ELogType.Error, "[Datum] 가로축 이미지 확보 실패 — TeachingImagePath / ShotParam.SimulImagePath 모두 없음 (DualImage).");
            } else if (bFallbackH) {
                // 폴백이 조용히 일어나면 "티칭 이미지로 검출했다" 고 오해할 수 있어 흔적을 남긴다.
                Logging.PrintLog((int)ELogType.Trace, "[Datum] 가로축 티칭 이미지 부재 — SHOT 검사이미지(SimulImagePath)로 폴백 (DualImage).");
            }
            if (imageVertical == null) {
                Logging.PrintErrLog((int)ELogType.Error, "[Datum] 세로축 이미지 확보 실패 — TeachingImagePath_Vertical / ShotParam.SimulImagePath 모두 없음 (DualImage).");
            } else if (bFallbackV) {
                Logging.PrintLog((int)ELogType.Trace, "[Datum] 세로축 티칭 이미지 부재 — SHOT 검사이미지(SimulImagePath)로 폴백 (DualImage).");
            }

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

        //260722 hbk Phase 68 D-08 회귀수정(D-09): pathA(PointROI) 소스 우선순위 결정을 별도 함수로 추출(30줄 초과 방지).
        //  회귀 원인: b28beca 가 ShotParam.HasImage 를 최우선으로 승격했으나, SIMUL_MODE/OfflineInspectMode(이 사이트의
        //  실제 운영 모드, Z모터 없는 수동지그) 에서는 EStep.Grab 이 매 사이클 LoadShotInspectionImage()+SetImage() 를
        //  거쳐 HasImage 가 Measure 시점엔 항상 true 임 → 라이브 분기를 최우선에 두면 TeachingImagePath_Horizontal 이
        //  도달 불가능한 죽은 코드가 되어, 운영자가 명시 지정한 PointROI 교시 이미지가 매번 무시됨
        //  (DualImage 측정값 미산출 증상). 올바른 우선순위:
        //  (1) TeachingImagePath_Horizontal 명시 경로 — 운영자 명시 설정은 절대 자동 override 되면 안 됨.
        //  (2) ShotParam.HasImage(라이브 grab) — 명시 경로 없을 때만, 불필요한 파일 재로드 회피(D-08 원 의도 보존).
        //  (3) ShotParam.SimulImagePath 폴백.
        private void ResolveFaiImageASource(DualImageEdgeDistanceMeasurement dualMeas, out string pathA, out HImage liveImageA, out bool bPathALoadNeeded) {
            bool bHasExplicitTeachingPath = !string.IsNullOrEmpty(dualMeas.TeachingImagePath_Horizontal) && File.Exists(dualMeas.TeachingImagePath_Horizontal);
            bool bHasLiveImage = ShotParam.HasImage;
            pathA = null;
            liveImageA = null;
            if (bHasExplicitTeachingPath) {
                pathA = dualMeas.TeachingImagePath_Horizontal; // Measurement 명시 경로 — 최우선
            }
            else if (bHasLiveImage) {
                liveImageA = ShotParam.GetImage(); // 라이브 클론 — 소유권은 호출부(TryExecuteMeasurement finally)가 Dispose
            }
            else {
                pathA = ShotParam.SimulImagePath; // fallback
            }
            bool bLiveImageBranchTaken = bHasLiveImage && !bHasExplicitTeachingPath; // imageA 가 이미 라이브 클론으로 채워졌는지
            bPathALoadNeeded = !bLiveImageBranchTaken; // 라이브 클론 미사용 시에만 pathA 파일 검증/로드
        }

        // DualImageEdgeDistanceMeasurement 측정용 양 이미지 로드.
        //  imageA: ResolveFaiImageASource 우선순위 결과 (명시 경로 > 라이브 grab > SimulImagePath 폴백).
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
            string pathA;
            HImage liveImageA;
            bool bPathALoadNeeded;
            ResolveFaiImageASource(dualMeas, out pathA, out liveImageA, out bPathALoadNeeded);
            imageA = liveImageA;
            string pathB = dualMeas.TeachingImagePath_Vertical; // LineROI 이미지 = meas 별도 경로 (무변경)

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

        //260807 hbk quick-debug(top-z1-measure-8sec-slow) fix: Shot 안의 모든(비-크로스-Z) FAI 가 sharedSrc 를 통해
        //  완전히 동일한 원본 이미지를 참조하므로, origin 저장은 Shot 당 1회만 큐에 넣는다(기존: FAI 마다 매번,
        //  127MP 기준 실측 ~1초/장 — FAICount=35 면 35회 중복 저장). 반환값은 모든 FAI 가 공유할 파일 경로(파일명만
        //  Shot 단위로 결정 — 개별 FAI 판정/측정점 세그먼트는 담지 않음, 애초에 Shot 전체가 공유하는 파일이므로).
        //  saver/sharedSrc 가 없으면 enqueue 없이 경로만 반환(호출부가 기존과 동일하게 파일명 메타를 채울 수 있도록).
        private string QueueSharedShotOrigin(SharedHImage sharedSrc, InspectionSequence parentSeq) {
            if (ShotParam == null) return "";
            DateTime ts = DateTime.Now;
            string sequenceName = ShotParam.OwnerSequenceName ?? "";
            int nIndexNumber = -1;
            bool bHasRequest = parentSeq != null && parentSeq.RequestPacket != null;
            if (bHasRequest) nIndexNumber = parentSeq.RequestPacket.IndexNumber;

            string originName = CaptureImageSaveService.BuildFileName("origin", sequenceName, ShotParam.ShotName, "", "", ts, nIndexNumber);
            string originPath = CaptureImageSaveService.BuildFilePath(false, originName, ts);

            var saver = SystemHandler.Handle.CaptureImageSaver;
            if (saver != null && sharedSrc != null) {
                sharedSrc.AddRef();
                saver.Enqueue(new CaptureImageSaveRequest {
                    Shared = sharedSrc,
                    NeedsRender = false,
                    FileName = originName,
                    IsCapture = false,
                    Timestamp = ts
                });
            }
            return originPath;
        }

        // FAI별 원본/캡쳐 이미지를 비동기 저장 큐에 넣고, 파일명을 fai 에 동기 write-back.
        //  파일명은 BuildDto(AddResponse) 가 읽으므로 enqueue 전에 동기 확정. PNG write 만 워커가 비동기 수행.
        //  origin/capture 가 동일 timestamp·segment 쌍을 유지한다.
        //  sharedSrc(Shot당 1회 복사 공유)를 받아 capture 요청에 ref 공유. 파일명 write-back 은 saver/공유 유무와 무관하게 항상 수행.
        //260807 hbk quick-debug(top-z1-measure-8sec-slow) fix: szSharedOriginPath 가 있으면(비-크로스-Z, 일반 경로)
        //  origin 은 이미 QueueSharedShotOrigin 으로 Shot 당 1회 저장됐으므로 여기선 경로만 기록하고 재저장하지 않는다.
        //  null 이면(크로스-Z — FAI 마다 실제로 다른 role 이미지 가능) 기존과 동일하게 FAI 마다 개별 저장(회귀 0).
        private void QueueFaiCapture(FAIConfig fai, SharedHImage sharedSrc, List<EdgeInspectionOverlay> faiOverlays, List<DatumCaptureOverlay> datumSnapshot, string sequenceName, string szSharedOriginPath) {
            if (fai == null) return;
            var saver = SystemHandler.Handle.CaptureImageSaver;
            DateTime ts = DateTime.Now; // origin(개별 저장 시)/capture 동일 timestamp 공유 (쌍)

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

            string captureName = CaptureImageSaveService.BuildFileName("capture", sequenceName, fai.FAIName, seg, judge, ts, nIndexNumber);  //260622 hbk Phase 48 PROTO-01
            // 동기 write-back — BuildDto 가 즉시 읽을 수 있도록 (PNG write 실패와 무관하게 경로는 확정)
            // 엑셀/cycle.json 에 절대 경로(경로\파일명) 표기. 실제 저장 경로와 동일한 BuildFilePath 로 기록.
            fai.LastCaptureImageFileName = CaptureImageSaveService.BuildFilePath(true, captureName, ts);

            bool bUseSharedOrigin = !string.IsNullOrEmpty(szSharedOriginPath);
            if (bUseSharedOrigin) {
                fai.LastOriginImageFileName = szSharedOriginPath; // Shot 공유 origin — 이미 저장됨, 경로만 기록
            } else {
                // 크로스-Z 등 FAI 마다 원본 내용이 실제로 다를 수 있는 경로 — 기존과 동일하게 FAI 마다 개별 저장.
                string originName = CaptureImageSaveService.BuildFileName("origin", sequenceName, fai.FAIName, seg, judge, ts, nIndexNumber);   //260622 hbk Phase 48 PROTO-01: 자재번호 포함 파일명
                fai.LastOriginImageFileName = CaptureImageSaveService.BuildFilePath(false, originName, ts);
                if (saver != null && sharedSrc != null) {
                    sharedSrc.AddRef();
                    saver.Enqueue(new CaptureImageSaveRequest
                    {
                        Shared = sharedSrc,
                        NeedsRender = false,
                        FileName = originName,
                        IsCapture = false,
                        Timestamp = ts
                    });
                }
            }

            if (saver == null || sharedSrc == null) return; // 서비스/공유 미존재 시 파일명만 기록, PNG skip

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

        //260729 hbk quick-fix(260729-e9q): MarkMeasurementZIndexMisconfigured 미러 — 비프로토콜 실행(RUN 버튼/
        //  RepeatRunService·BatchRunService 일괄검사)에서 크로스-Z A/B 짝이 구조적으로 완성 불가일 때의 명시적
        //  미측정 NG. GetExecutionZIndex() 가 항상 0(RequestPacket==null, D-08 안전 폴백)이므로 ZIndexA!=ZIndexB
        //  중 최대 한쪽만 매칭될 수 있고, 이 Shot 의 EStep.Measure 는 이번 tick 한 번뿐이라 뒤이어 완성될 tick 이
        //  존재하지 않는다. 기존엔 그대로 continue 해 faiAllPass 가 true 로 남았고 AggregateFaiResult 가
        //  fai.IsPass=true 로 확정 → 한 번도 측정하지 않은 항목이 작업자/외부 핸들러에 PASS 로 보고됐다(안전 결함).
        //  bRelevantTick=true = 한쪽 role 이미지만 캡처됨 / false = z=0 이 A·B 어느 쪽도 아니라 캡처조차 없음.
        //260729 hbk quick-fix(260729-hwb): bProtocolCycle 파라미터 추가 — 상태 마킹부(ClearResult/LastSkipReason/
        //  LastJudgement)는 e9q 와 동일하게 유지하고, 로그 문구만 분기한다. bProtocolCycle=false 는 e9q 문구를
        //  한 글자도 바꾸지 않고 그대로 출력(비프로토콜 실행 안내). bProtocolCycle=true 는 프로토콜 사이클(수동
        //  Z트리거 포함)의 정상 흐름 중간 상태임을 명시해 운영자가 고장으로 오인하지 않게 별도 문구를 출력한다.
        //  parentSeq2 는 bProtocolCycle=true 일 때만 현재 tick 의 z(GetExecutionZIndex) 를 로그에 남기는 데 쓴다.
        private void MarkMeasurementCrossZIncomplete(MeasurementBase meas, bool bRelevantTick, bool bProtocolCycle, InspectionSequence parentSeq2)
        {
            meas.ClearResult();
            meas.LastSkipReason = SkipReason.CROSS_Z_INCOMPLETE;
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
            if (bProtocolCycle)
            {
                int nCurZ = parentSeq2 != null ? parentSeq2.GetExecutionZIndex() : UNSET_ZINDEX;
                Logging.PrintLog((int)ELogType.Error, "[FAIMeasurement] Measurement '" + measName + "' CROSS_Z_INCOMPLETE — ZIndexA=" + nZA + ", ZIndexB=" + nZB + ", CurrentZ=" + nCurZ + ": 프로토콜 사이클 정상 흐름의 중간 상태(고장 아님) — 짝이 되는 나머지 z 트리거 대기 중, 아직 측정되지 않음(PASS 아님) (" + meas.LastSkipReason + ")");
                return;
            }
            string szCase;
            if (bRelevantTick) szCase = "한쪽 z 이미지만 확보, 나머지 tick 없음";
            else szCase = "z=0 이 ZIndexA/B 어느 쪽도 아님, 캡처 없음";
            Logging.PrintLog((int)ELogType.Error, "[FAIMeasurement] Measurement '" + measName + "' 미측정 NG — 비프로토콜 실행(RUN 버튼/일괄검사)은 z_index 가 항상 0 이라 ZIndexA=" + nZA + ", ZIndexB=" + nZB + " 크로스-Z 짝을 완성할 수 없음(" + szCase + "). 실제로 측정하려면 PLC/$TEST 또는 수동 Z 트리거로 두 z 위치를 모두 실행할 것 (" + meas.LastSkipReason + ")");
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

        //260722 hbk Phase 68 GAP-4(68-GAP-ANALYSIS.md 사후발견): 크로스-Z role별 이미지 대체.
        //  SIMUL_MODE 에서 Shot 은 SimulImagePath 단일 고정 이미지만 가져(1-Shot-1-file) role A(ZIndexA tick)/
        //  role B(ZIndexB tick) 가 매 tick ShotParam.GetImage() 를 호출해도 항상 동일 이미지를 반환한다 — 크로스-Z
        //  거리계산 전제(서로 다른 두 Z 위치 이미지)가 SIMUL 에서 구조적으로 성립 불가능해 UAT 검증이 막혀 있었다
        //  (사용자 디버거 실측: TakeCrossZImageCopy 로 취득한 imgA/imgB 가 동일 사진이었음).
        //  대체 소스는 기존 static DualImage 경로가 이미 쓰는 두 필드를 재사용(신규 필드 도입 없음):
        //  role A(ZIndexA tick) → TeachingImagePath_Horizontal, role B(ZIndexB tick) → TeachingImagePath_Vertical.
        //  경로 미설정/파일없음 → 기존 ShotParam.GetImage() 라이브 폴백(이 두 경로를 설정 안 한 기존 레시피는 회귀 0).
        //260729 hbk quick-fix(260729-jdi): #if SIMUL_MODE 게이트 제거. SIMUL_MODE 가 정의되지 않은 빌드
        //  구성(Debug|x64)에서는 #else 가 무조건 ShotParam.GetImage() 를 반환해 role A/B 가 항상 같은 이미지를
        //  받았다 — 가로 이미지 기준 PointROI 가 세로 이미지를 보게 되어 실기 크로스-Z 측정이 에지 0개로
        //  실패했다(BOTTOM SHOT_E5 E5_P1/E5_P2 재현). 비-크로스-Z 경로 ResolveFaiImageASource/
        //  TryGrabOrLoadFaiDualImages 는 애초에 컴파일 게이트 없이 "경로가 설정되어 있으면 항상 그 파일 사용"
        //  정책이므로, 크로스-Z 도 같은 정책으로 통일해 빌드 구성에 관계없이 동일하게 동작시킨다. 경로 미설정
        //  레시피는 여전히 라이브(ShotParam.GetImage()) 폴백이라 회귀 0.
        private HImage LoadCrossZRoleImage(bool bIsRoleA, DualImageEdgeDistanceMeasurement dualMeas)
        {
            string szRoleTeachingPath;
            if (bIsRoleA) szRoleTeachingPath = dualMeas.TeachingImagePath_Horizontal;
            else szRoleTeachingPath = dualMeas.TeachingImagePath_Vertical;
            bool bRoleTeachingPathValid = !string.IsNullOrEmpty(szRoleTeachingPath) && File.Exists(szRoleTeachingPath);
            if (!bRoleTeachingPathValid)
            {
                string szRoleLabel;
                if (bIsRoleA) szRoleLabel = "A(Horizontal)";
                else szRoleLabel = "B(Vertical)";
                Logging.PrintLog((int)ELogType.Trace, "[FAI CrossZ] role " + szRoleLabel + " 교시 이미지 미설정 — 라이브 이미지로 폴백");
                return ShotParam.GetImage();
            }
            try
            {
                return new HImage(szRoleTeachingPath);
            }
            catch (Exception ex)
            {
                Logging.PrintErrLog((int)ELogType.Error, "[FAI CrossZ] role 교시 이미지 로드 실패(" + szRoleTeachingPath + "): " + ex.Message + " — 라이브 이미지로 폴백");
                return ShotParam.GetImage();
            }
        }

        //260722 hbk Phase 68 D-02a: 크로스-Z 캡처 tick 처리 — 이 측정과 무관(bRelevant=false) / 캡처 실패
        //  (bCaptureOk=false) / 완성 여부(bCompleted) 3-state 판정. 새 라이브 grab 호출 없음 — EStep.DatumPhase
        //  (GrabSyncLock 안)에서 이미 확보된 ShotParam.GetImage() 클론 재사용이 기본(lock 위반 없음, shared-
        //  lighthandler-race 준수). role별 교시 경로가 설정돼 있으면 LoadCrossZRoleImage(GAP-4)
        //  가 대신 그 파일에서 로드 — 파일 읽기이므로 GrabSyncLock/라이브 캡처와 무관, lock 계약 영향 없음.
        //  완성(bCompleted=true) 이면 호출부가 TryExecuteCrossZMeasurement 로 이어간다.
        //260729 hbk quick-fix(260729-hwb): out szCapturedRoleKey 추가 — 이번 tick 에서 실제로 캡처된 role 저장소
        //  키를 호출부에 알려준다. 캡처 성공(bCaptureOk=true) 이외의 모든 조기 return 경로는 null 로 나간다.
        //  화면/저장 표시가 항상 정적 SimulImagePath 였던 결함(T-HWB-02) 을 닫기 위해 호출부가 이 키로
        //  TakeCrossZImageCopy 사본을 받아 표시/저장 소스로 쓴다.
        private void ProcessCrossZCaptureTick(DualImageEdgeDistanceMeasurement dualMeas, InspectionSequence parentSeq2, out bool bRelevant, out bool bCaptureOk, out bool bCompleted, out string szCapturedRoleKey)
        {
            bRelevant = false;
            bCaptureOk = false;
            bCompleted = false;
            szCapturedRoleKey = null;
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
            using (HImage capturedImage = LoadCrossZRoleImage(bIsRoleA, dualMeas))
            {
                if (capturedImage == null)
                {
                    return; // 캡처 실패 — 호출부가 NG 처리
                }
                parentSeq2.StoreCrossZImage(roleKey, capturedImage);
                bCaptureOk = true;
                szCapturedRoleKey = roleKey;
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
        private void AggregateFaiResult(FAIConfig fai, bool faiAllPass, List<EdgeInspectionOverlay> faiOverlays, SharedHImage sharedSrc, List<DatumCaptureOverlay> datumSnapshot, string szSharedOriginPath) {
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
                QueueFaiCapture(fai, sharedSrc, faiOverlays, datumSnapshot, ownerSeqName, szSharedOriginPath);
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
