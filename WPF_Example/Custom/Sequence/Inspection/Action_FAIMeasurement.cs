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

        //260818 hbk 크로스-Z 게이트 상태 — ProcessOneMeasurement 게이트 블록 전용.
        //  프로토콜/비프로토콜 구분(NotMyTick 의 2갈래, HalfPending 의 2갈래)은 멤버로 쪼개지 않고
        //  case 안에서 bNonProtocolCycle if-else 로 처리한다 — 원본 if 구조와 1:1 로 남겨야
        //  리팩토링 전후 대조가 가능하기 때문이다.
        //260819 hbk quick-260819-tcs 문서화 전용(로직 무변경) — NotMyTick/HalfPending 두 case 내부의
        //  bNonProtocolCycle 분기(parentSeq2.IsProtocolDrivenCycle() 로 읽음, 위 주석 참고)까지 합치면
        //  실제 상태 조합은 5개보다 많다. 즉 이 enum 은 완전한 상태표가 아니라 상위(top-level) 분류이며,
        //  두 번째 축(bNonProtocolCycle)은 의도적으로 enum 멤버로 인코딩하지 않는다.
        private enum ECrossZGate {
            Misconfigured,
            NotMyTick,
            CaptureFailed,
            HalfPending,
            BothReady
        }

        // Shot 측정 루프에서 쓰는 값들을 한데 묶은 것 — 예전엔 이 값들을 메서드 사이에 ref 로 넘겼는데,
        //  ref 를 하나만 빠뜨려도 컴파일은 되고 값만 조용히 0 으로 남는 실수가 나기 쉬웠다.
        //  ⚠ 프로퍼티가 아니라 반드시 필드로 선언해야 한다 — 아직 ref 로 받는 곳이 남아있는데
        //    C# 은 프로퍼티를 ref 로 못 넘긴다.
        //  ⚠ 값 중 FaiAllPass/CrossZRoleImage 는 FAI 하나 처리할 때마다 리셋해야 한다 —
        //    나머지는 Shot 전체가 끝날 때까지 유지되는 값이다.
        private class ShotMeasureAccumulator {
            public bool AllPass;
            public int MeasuredCount;
            public int NMeasNg;
            public bool ShotDisplayImageReplaced;
            public bool FaiAllPass;
            public HImage CrossZRoleImage;
        }

        private FAIMeasurementContext pMyContext;

        //260722 hbk Phase 68 D-09: 매직넘버 상수화 — ZIndexA/B 미설정 sentinel + 크로스-Z 저장소 역할(A/B) 키 접미사.
        private const int UNSET_ZINDEX = -1;
        private const string CROSS_Z_ROLE_SUFFIX_A = "_ZA";
        private const string CROSS_Z_ROLE_SUFFIX_B = "_ZB";
        //260722 hbk Phase 68 D-06/D-09: Datum 크로스-Z 저장소 키 접두사 — 측정 키(ShotName|MeasName)와 네임스페이스 구분.
        private const string CROSS_Z_DATUM_KEY_PREFIX = "DATUM|";
        //260819 hbk quick-260819-tcs: 로그 태그 리터럴 17곳 중복 제거 — 문자열 값(태그 뒤 공백 포함) 은 그대로, 표기만 상수 참조로 치환. 공백을 상수 안에 둬서 호출부마다 손으로 입력할 필요를 없앴다.
        private const string LOG_TAG = "[FAIMeasurement] ";

        public ShotConfig ShotParam => Param as ShotConfig;

        public Action_FAIMeasurement(EAction id, string name, ShotConfig shotConfig) : base(id, name) {
            Context = new FAIMeasurementContext(this);
            pMyContext = Context as FAIMeasurementContext;
            Param = shotConfig;
        }

        public override void OnLoad() {
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

        //260819 hbk quick-260819-q9t: Dispose try/catch 반복 제거 — null 이면 그냥 반환, catch 는 기존과 동일하게 예외를 삼킨다.
        private static void SafeDisposeImage(HImage image) {
            if (image == null) return;
            try { image.Dispose(); } catch { }
        }

        //260819 hbk quick-260819-q9t: 측정 이름 미교시(null)면 TypeName 으로 대체 — 5곳 중복 통합.
        private static string GetMeasurementDisplayName(MeasurementBase meas) {
            return meas.MeasurementName ?? meas.TypeName;
        }

        // 이 메서드는 프로그램이 켜져 있는 동안 아주 짧은 간격으로 계속 호출됩니다. 호출될 때마다
        //  지금이 몇 번째 단계인지 보고 그 단계에 맞는 case 하나만 실행하는 식으로 동작합니다.
        //  한 장 촬영하는 데 Init(초기화) → MoveZ(이동) → DatumPhase(기준점) → Grab(촬영) →
        //  Measure(측정) → End(종료) 순서로 단계가 넘어갑니다.
        public override ActionContext Run() {
            switch ((EStep)Step) {
                case EStep.Init:       RunInit();       break;
                case EStep.MoveZ:      RunMoveZ();      break;
                case EStep.DatumPhase: RunDatumPhase(); break;
                case EStep.Grab:       RunGrab();       break;
                case EStep.Measure:    RunMeasure();    break;
                case EStep.End:        RunEnd();        break;
            }
            return Context;
        }

        //260818 hbk [초보자용] 검사 시작 직전 정리 단계 — 지난번 검사 결과가 남아있으면 지우고 바로 다음 단계로 넘어갑니다.
        private void RunInit() {
            // Run 사이클 진입 시 image buffer + FAI results dispose
            if (ShotParam != null) ShotParam.ClearAllResults();
            Step = (int)EStep.MoveZ;
        }

        //260818 hbk [초보자용] 이 Shot이 필요로 하는 높이(Z)로 이동하는 단계입니다. 지금은 실제 축 이동 장치가
        //  아직 없어서(주석의 SIMUL/실장비 분기 참고) 사실상 대기만 하거나 건너뜁니다.
        private void RunMoveZ() {
            //260818 hbk [SEQ] 단계 로그 — 대괄호 안 이름은 코드의 EStep 값과 1:1 이라, 로그에서 본 단계명으로
            //  Action_FAIMeasurement.cs 의 'case EStep.<이름>:' 을 바로 찾아갈 수 있다.
            int nLogZIndex;
            if (ShotParam != null) nLogZIndex = ShotParam.ZIndex;
            else nLogZIndex = -1;
            int nLogDelayMs;
            if (ShotParam != null) nLogDelayMs = ShotParam.DelayMs;
            else nLogDelayMs = 0;
            LogSeqStep("MoveZ", string.Format("Z축 이동 (ZIndex={0}, Delay={1}ms)",
                nLogZIndex, nLogDelayMs));
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
        }

        // 등록된 Datum 전부를 순서대로 검출해서 _datumTransforms 에 쌓는다. 일부가 실패해도 나머지는 계속
        //  진행한다(중단하지 않음).
        // "기준점(Datum)"은 사진에서 측정 위치를 정할 때 기준이 되는 점입니다. 부품이 움직이지 않는 한
        //  기준점도 안 바뀌므로, 이번 검사에서 이미 찾았으면 다시 찾지 않고 재사용합니다(아래 캐시).
        // 이 초기화는 여기서 하지 않는다 — 사이클이 시작될 때 한 번만 한다. 매번 하면 Shot 마다
        //  안 움직이는 기준점까지 매번 다시 찾게 되어 느려진다(실측 회당 0.9~1.4초).
        private void RunDatumPhase() {
            //260818 hbk [SEQ] DatumPhase 단계 tact 측정용 — 아래 "완료 —" 단계 요약 로그가 소비한다.
            var swDatumPhase = Stopwatch.StartNew();
            int nDatumOk = 0, nDatumFail = 0, nDatumCached = 0; //260818 hbk [SEQ] 단계 요약용 집계
            InspectionSequence parentSeq;
            if (ShotParam != null) parentSeq = ShotParam.Parent as InspectionSequence;
            else parentSeq = null;
            int nDatumRegistered;
            if (parentSeq != null) nDatumRegistered = parentSeq.DatumConfigs.Count;
            else nDatumRegistered = 0;
            LogSeqStep("DatumPhase", string.Format("기준점 검출 — 등록 Datum {0}개",
                nDatumRegistered));
            if (parentSeq != null && parentSeq.DatumConfigs.Count > 0) {
                //260618 hbk Phase 54 ALIGN-01 이미지 회전(datumLevelOn/datumLevelAngle) 폐기 (D-03/D-05 warp 0회).
                //  레벨링 이미지회전 → 패턴매칭 ROI 좌표변환으로 대체. 이전 datumLevelOn/datumLevelAngle 지역변수 제거.
                foreach (var datum in parentSeq.DatumConfigs) {
                    ProcessOneDatum(datum, parentSeq, ref nDatumOk, ref nDatumFail, ref nDatumCached);
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
            // Datum 설정이 없으면 그냥 통과(보정 없이 진행) — 중단하지 않는다.
            // 이 Shot이 기준점만 찾으러 실행된 경우(다른 Shot의 기준점을 대신 찾아주는 역할)에는 여기서
            //  멈추고 이 Shot 자신의 촬영/측정은 하지 않는다 — 계속 진행하면 잘못된 위치에서 측정이 실행돼
            //  결과와 저장 이미지가 엉뚱하게 오염된다.
            int nCurZ = 0;
            bool bDatumOnly = false;
            if (parentSeq != null) {
                nCurZ = parentSeq.GetExecutionZIndex();
                bDatumOnly = parentSeq.ShouldSkipMeasurementAfterDatumPhase(nCurZ);
            }
            //260818 hbk [SEQ] DatumPhase 결과 요약 (tact 포함)
            LogDatumPhaseSummary(nDatumOk, nDatumFail, nDatumCached, swDatumPhase);
            if (bDatumOnly) {
                Step = (int)EStep.End;
            } else {
                Step = (int)EStep.Grab; // datum 부분 실패해도 측정 진행
            }
        }

        //260819 hbk quick-260819-rle: DatumPhase 완료 요약 로그 — LogAndTallyAlgorithm 과 대칭되는 소규모 로깅 헬퍼.
        private void LogDatumPhaseSummary(int nDatumOk, int nDatumFail, int nDatumCached, Stopwatch swDatumPhase) {
            LogSeqStep("DatumPhase", string.Format("완료 — 검출성공 {0} / 실패 {1} / 캐시재사용 {2} ({3:F2}초)",
                nDatumOk, nDatumFail, nDatumCached, swDatumPhase.Elapsed.TotalSeconds));
        }

        //260702 hbk Extract Method(Task3): DatumPhase per-datum loop 본문(원본 foreach 내부, 동치 보장, continue->return)
        private void ProcessOneDatum(DatumConfig datum, InspectionSequence parentSeq, ref int nDatumOk, ref int nDatumFail, ref int nDatumCached) {
            if (datum == null) return;
            // 크로스-Z 가 아닌 기준점은 위치가 고정이라, 이번 검사에서 이미 한 번 찾았으면 다시 찾지 않고
            //  캐시를 그대로 쓴다(회당 0.9~1.4초 절약). 전에 실패했던 기준점은 캐시에 안 남으므로
            //  다시 시도한다. 크로스-Z 기준점은 여러 장을 조합해야 해서 이 캐시 대상에서 제외한다 —
            //  매번 다시 실행한다.
            bool bIsCrossZDatum = datum.AlgorithmTypeEnum == EDatumAlgorithm.VerticalTwoHorizontalDualImage
                && !(datum.ZIndexA == UNSET_ZINDEX && datum.ZIndexB == UNSET_ZINDEX);
            // 크로스-Z 로 "설정"된 것과 이번 사이클에 실제로 크로스-Z 경로를 "타는" 것은 다르다.
            //  TryGrabOrLoadDualDatumImages 의 bCrossZEnabled 와 같은 조건 — 프로토콜 사이클이 아니면
            //  크로스-Z 설정 Datum 도 정적 2장(TeachingImagePath[_Vertical]) 경로로 검출된다. 정적 경로는
            //  Shot 과 무관하게 매번 같은 입력/같은 결과라 사이클 내 캐시가 성립한다(캐시는 사이클 시작에
            //  ClearDatumTransforms 로 비워진다). 프로토콜 사이클에서는 z 마다 이미지가 누적되므로 종전대로
            //  캐시 대상에서 제외한다.
            bool bTakesCrossZPath = bIsCrossZDatum && parentSeq.IsProtocolDrivenCycle();
            // 260820 hbk quick-fix: 크로스-Z Datum 은 캐시가 안 되다 보니(위 주석), 이 Shot 과 무관한 크로스-Z
            //  Datum 까지 DatumPhase 가 등록 Datum 전부를 매번 순회하며 같이 재검출하고 있었다 — 반복검사(일괄검사)로
            //  Shot 1개만 계속 돌려도 매 회 무관한 다른 Datum까지 strip-loop 를 다시 도는 게 실측(gray_erosion/
            //  strip-loop 타임스탬프)으로 확인됨. 이 Shot 이 실제로 소유/참조하는 크로스-Z Datum 만 검출하도록 제한.
            //  비-크로스-Z Datum 은 캐시가 있어 재검출 비용이 사실상 0 이라 건드리지 않는다(회귀 위험 최소화).
            if (bIsCrossZDatum && !IsDatumOwnedByCurrentShot(datum)) {
                return; // 이 Shot 과 무관한 크로스-Z Datum — 검출 생략(집계 카운터 미증가, 실패 아님)
            }
            if (!bTakesCrossZPath && parentSeq.HasCachedDatumTransform(datum.DatumName)) {
                nDatumCached++; //260818 hbk [SEQ] 요약: 이번 사이클 이미 검출됨 → 재검출 생략
                return; // 이번 사이클 기 검출 성공 — skip
            }
            // Datum 전용 조명(SourceShotName 상속과 무관) 을 grab 직전에 켠다. 이 grab 이 끝나면
            //  루프 종료 후 ApplyShotLights 로 되돌려야 EStep.Grab 의 측정 grab 이 Shot 조명 아래서 이뤄진다.
            parentSeq.ApplyDatumLights(datum);
            // 조명 명령은 큐잉만 되고 실제 전송은 백그라운드 스레드가 처리 — grab 전에 실제 반영을 기다린다.
            //  (기존엔 수동 UI grab 경로에만 있던 대기를 자동 검사 사이클에도 배선. SIMUL/오프라인처럼
            //  실제로 대기할 쓰기가 없으면 즉시 반환되므로 비용은 무시할 만큼 작다.)
            LightHandler.Handle.WaitForPendingWrites();
            if (datum.AlgorithmTypeEnum == EDatumAlgorithm.VerticalTwoHorizontalDualImage) {
                ProcessDatumDualImage(datum, parentSeq);
            } else { // 1-image datum
                ProcessDatumSingleImage(datum, parentSeq, ref nDatumOk, ref nDatumFail);
            }
        }

        // 260820 hbk quick-fix(리비전): 이 Datum 이 현재 실행 중인 Shot(ShotParam) 과 관련 있는지 판정 — 셋 중
        //  하나면 소유(=검출 진행).
        //  1) datum.SourceShotName 이 이 Shot 을 명시적으로 캡처 담당으로 지정.
        //  2) 이 Shot 의 FAI 측정 중 하나라도 DatumRef 로 이 Datum 을 직접 참조.
        //  3) datum.SourceShotName 이 비어있음(미설정) — InspectionSequence.AddDatumTriggerActionIndex 가 이
        //     경우 "이 시퀀스의 첫 owned Action" 으로 트리거를 폴백시키는데(로그: "SourceShotName 미해결 — 첫
        //     owned Action 을 DatumPhase 트리거로 사용"), 그 폴백이 정확히 어느 Shot으로 갈지는 이 함수가 알 수
        //     없다 — 잘못 스킵하면 크로스-Z 이미지 짝이 영원히 안 채워져 사이클이 멈춘다(실측: SIDE 반복검사 중
        //     무응답 hang 재현, Trace.log 가 크래시/예외 없이 그냥 끊김). 그래서 SourceShotName 미해결 크로스-Z
        //     Datum 은 안전하게 "항상 소유"로 보고 스킵하지 않는다 — 최적화 대상에서 제외, 정확성 우선.
        private bool IsDatumOwnedByCurrentShot(DatumConfig datum) {
            if (ShotParam == null) return true; // 방어적 폴백 — 판정 불가 시 기존 동작(항상 검출) 유지
            bool bSourceShotUnresolved = string.IsNullOrEmpty(datum.SourceShotName);
            if (bSourceShotUnresolved) return true; // 폴백 트리거 대상을 알 수 없음 — 안전하게 항상 검출
            string shotName = ShotParam.ShotName ?? "";
            bool bSourceShotMatches = datum.SourceShotName == shotName;
            if (bSourceShotMatches) return true;
            foreach (var fai in ShotParam.FAIList) {
                if (fai == null) continue;
                foreach (var meas in fai.Measurements) {
                    if (meas != null && meas.DatumRef == datum.DatumName) return true;
                }
            }
            return false;
        }

        //260702 hbk Extract Method(Task3): DatumPhase DUAL(VerticalTwoHorizontalDualImage) 분기 본문 -- 카운터(nDatumOk/nDatumFail) 미접근 구조 고정(D-1)
        private void ProcessDatumDualImage(DatumConfig datum, InspectionSequence parentSeq) {
            //260722 hbk Phase 68 D-05: Datum ZIndexA/B 오설정 게이트 — TryGrabOrLoadDualDatumImages 호출 전
            //  명시적 실패 처리(조용한 static 폴백 금지). 미설정(-1/-1)은 게이트 미해당 → 기존 static 경로.
            bool bDatumZIndexMisconfigured = IsDatumZIndexMisconfigured(datum, parentSeq);
            if (bDatumZIndexMisconfigured) {
                Logging.PrintLog((int)ELogType.Error, LOG_TAG + "Datum '" + datum.DatumName + "' ZIndexA=" + datum.ZIndexA + ", ZIndexB=" + datum.ZIndexB + " 크로스-Z 오설정(동일값/단일설정/존재하지 않는 index, " + SkipReason.ZINDEX_MISCONFIGURED + ")");
                datum.RuntimeDetectFailed = true;
                parentSeq.MarkDatumFailed(datum.DatumName);
                return; // datum skip, abort 안 함
            }
            // 이 검출 루프가 매번 전체 Datum 목록을 다시 검출한다는 전제, 그리고 두 z_index 모두에서 이
            //  Datum 검출이 실행된다는 전제가 크로스-Z 결과 정확성의 바탕이다 — 이 두 동작을 바꿀 때는
            //  크로스-Z 쪽도 반드시 다시 검증해야 한다.
            HImage imgH = null, imgV = null;
            bool bDatumCrossZPending;
            try {
                if (!TryGrabOrLoadDualDatumImages(datum, parentSeq, out imgH, out imgV, out bDatumCrossZPending)) {
                    if (bDatumCrossZPending) {
                        return; // Z1(비완성 index): 캡처만 — 실패 아님(MarkDatumFailed 미설정), 완성 z_index에서 검출(D-02a)
                    }
                    Logging.PrintLog((int)ELogType.Error, LOG_TAG + "Datum '" + datum.DatumName + "' DualImage 취득 실패 (skip)");
                    // 이미지 취득 실패 시 RenderDatumOverlay DETECT FAIL 라벨 분기 조건 충족 (TryRunSingleDatum 미호출 경로)
                    datum.LastFindSucceeded = false;
                    // 티칭 여부 무관 라벨 신호
                    datum.RuntimeDetectFailed = true;
                    // per-FAI gate 신호 기록
                    parentSeq.MarkDatumFailed(datum.DatumName);
                    return; // datum skip, abort 안 함
                }
                RunDatumDualImageDetection(datum, parentSeq, imgH, imgV); //260819 hbk quick-260819-s05: align/detect 분기를 RunDatumDualImageDetection 헬퍼로 추출(측정 TryExecuteMeasurement/TryExecuteCrossZMeasurement 와 동일 패턴)
            } finally {
                SafeDisposeImage(imgH);
                SafeDisposeImage(imgV);
            }
        }

        //260819 hbk quick-260819-s05: ProcessDatumDualImage 의 align/detect 분기 본문 추출 — 측정 쪽 TryExecuteMeasurement/TryExecuteCrossZMeasurement 와
        //  동일한 "오케스트레이터가 같은 모양의 헬퍼를 호출" 패턴. 카운터(nDatumOk/nDatumFail) 미접근 구조 그대로 유지(원본 ProcessDatumDualImage 도 카운터 미사용, D-1).
        private void RunDatumDualImageDetection(DatumConfig datum, InspectionSequence parentSeq, HImage imgH, HImage imgV) {
            //260619 hbk Phase 57 #4 DualImage align 배선 (deferred 게이트 해제, 단일이미지 분기 미러).
            //  enabled → align 단독 경로(imgH 패턴매칭 → 단일 alignRigid 를 imgH/imgV 두 검출에 적용, D-01). disabled → 기존 2-image 검출(off 회귀 0).
            //  패턴 모델은 가로축(imgH/TeachingImagePath) 1세트만 사용 — 세로엔 패턴 없음(D-04).
            if (datum.IsPatternAlignEnabled) {
                string modelPath = InspectionSequence.ResolveDatumModelPath(datum, parentSeq.Name); // 260723 hbk quick-fix: 전역 Shots[0] 폴백 결함 수정 — 소유 시퀀스명 명시 전달 (D-07)
                string alignErr;
                if (!parentSeq.TryComposeAlign(datum, imgH, imgV, modelPath, out alignErr)) {
                    Logging.PrintLog((int)ELogType.Error, LOG_TAG + "Datum '" + datum.DatumName + "' DualImage 패턴매칭 실패 (ALIGN_FAIL, skip): " + alignErr);
                    datum.RuntimeDetectFailed = true;
                    parentSeq.MarkAlignFailed(datum.DatumName); //260619 hbk Phase 57 #5 lenient — NG 강제, abort 안 함
                }
            } else {
                string derr;
                if (!parentSeq.TryRunSingleDatum(datum, imgH, imgV, out derr)) { // 기존 검출 경로 무수정
                    Logging.PrintLog((int)ELogType.Error, LOG_TAG + "Datum '" + datum.DatumName + "' 검출 실패 (skip): " + derr);
                    datum.RuntimeDetectFailed = true;
                    parentSeq.MarkDatumFailed(datum.DatumName);
                }
            }
        }

        //260702 hbk Extract Method(Task3): DatumPhase 1-image 분기 본문
        private void ProcessDatumSingleImage(DatumConfig datum, InspectionSequence parentSeq, ref int nDatumOk, ref int nDatumFail) {
            HImage img = GrabOrLoadDatumImage(datum);
            if (img == null) {
                Logging.PrintLog((int)ELogType.Error, LOG_TAG + "Datum '" + datum.DatumName + "' 이미지 취득 실패 (skip)");
                datum.LastFindSucceeded = false;
                datum.RuntimeDetectFailed = true;
                parentSeq.MarkDatumFailed(datum.DatumName);
                return;
            }
            //260618 hbk Phase 54 ALIGN-01 패턴매칭 위치보정 (D-02/D-04/D-05). 이미지 회전(레벨링 warp) 폐기 (D-03/D-05 warp 0회).
            //  enabled → align 단독 경로(검출 미수행, 이중적용 방지). disabled → 기존 검출 경로 유지(off 회귀 0, D-11).
            try {
                RunDatumSingleImageDetection(datum, parentSeq, img, ref nDatumOk, ref nDatumFail); //260819 hbk quick-260819-s05: align/detect 분기를 RunDatumSingleImageDetection 헬퍼로 추출(측정 TryExecuteMeasurement/TryExecuteCrossZMeasurement 와 동일 패턴)
            } finally {
                img.Dispose();
            }
        }

        //260819 hbk quick-260819-s05: ProcessDatumSingleImage 의 align/detect 분기 본문 추출 — 측정 쪽 TryExecuteMeasurement/TryExecuteCrossZMeasurement 와
        //  동일한 "오케스트레이터가 같은 모양의 헬퍼를 호출" 패턴. ref nDatumOk/nDatumFail 은 이 분기 안에서 실제로 증가되므로 ref 그대로 전달(out 아님).
        private void RunDatumSingleImageDetection(DatumConfig datum, InspectionSequence parentSeq, HImage img, ref int nDatumOk, ref int nDatumFail) {
            if (datum.IsPatternAlignEnabled) {
                string modelPath = InspectionSequence.ResolveDatumModelPath(datum, parentSeq.Name); // 260723 hbk quick-fix: 전역 Shots[0] 폴백 결함 수정 — 소유 시퀀스명 명시 전달 (D-07)
                string alignErr;
                if (!parentSeq.TryComposeAlign(datum, img, modelPath, out alignErr)) {
                    Logging.PrintLog((int)ELogType.Error, LOG_TAG + "Datum '" + datum.DatumName + "' 패턴매칭 실패 (ALIGN_FAIL, skip): " + alignErr);
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
                    Logging.PrintLog((int)ELogType.Error, LOG_TAG + "Datum '" + datum.DatumName + "' 검출 실패 (skip): " + derr);
                    datum.RuntimeDetectFailed = true;
                    parentSeq.MarkDatumFailed(datum.DatumName);
                    nDatumFail++;
                } else {
                    nDatumOk++;
                }
                //260818 hbk [SEQ] 어떤 검출 알고리즘을 탔는지 표시 — 알고리즘 탭에서 상세 확인
                LogSeqAlgo("Datum", datum.DatumName, "TryRunSingleDatum/" + datum.AlgorithmTypeEnum);
            }
        }

        //260818 hbk [초보자용] 실제로 카메라로 사진을 찍는(또는 SIMUL 모드면 저장된 사진을 불러오는) 단계입니다.
        //  이미 이 Shot이 사진을 갖고 있으면(HasImage) 다시 찍지 않고 그대로 다음 단계로 넘어갑니다.
        private void RunGrab() {
            if (ShotParam != null && !ShotParam.HasImage) {
                //260818 hbk [SEQ] Grab 단계 tact 측정용 — 아래 "촬영 완료" 단계 요약 로그가 소비한다.
                var swGrabTotal = Stopwatch.StartNew();
                HImage image = AcquireShotImage();
                if (image != null) {
                    //260618 hbk Phase 54 ALIGN-01 측정 이미지 회전(레벨링 warp) 폐기 (D-03/D-05 warp 0회).
                    //  레벨링 이미지회전 → 패턴매칭 ROI 좌표변환으로 대체. 측정은 보정 전 원본 픽셀에서 수행.
                    ShotParam.SetImage(image); // 측정 소스(데이터 경로) — 표시 설정과 무관하게 항상 설정한다.
                    UpdateViewerCopy(image);
                    image.Dispose(); // 누수 방지 — 조건과 무관하게 항상 수행.
                }
                //260818 hbk [SEQ] Grab 단계 요약 (tact 포함)
                LogSeqStep("Grab", string.Format("검사 이미지 촬영 완료 ({0:F2}초)",
                    swGrabTotal.Elapsed.TotalSeconds));
            }
            Step = (int)EStep.Measure;
        }

        //260819 hbk Extract Method: RunGrab 의 촬영 구역을 그대로 옮긴 것(순수 이동, 동작 무변경).
        //  ⚠ 조건부 컴파일 3줄이 반드시 이 메서드 안에 함께 있어야 한다. 하나라도 경계를 넘으면
        //    한쪽 빌드에서만 조용히 다른 코드가 된다. Debug(SIMUL ON)/비-SIMUL 두 빌드로 검증한다.
        //  ⚠ 실기 grab 여부 플래그는 선언·대입·읽기가 전부 이 메서드 안에 있어야 한다 —
        //    쪼개면 SIMUL 빌드에서 "대입되지만 사용 안 됨" 경고(CS0219 계열)가 새로 생긴다.
        //  ⚠ tact Stopwatch 는 호출부(RunGrab)에 남겼다. 여기로 옮기면 측정 구간이 달라져 [SEQ] 로그 숫자가 바뀐다.
        private HImage AcquireShotImage() {
            HImage image = null;
            bool bIsLiveGrabAttempt = false;   //260811 hbk plc-spec-260811-alignment: 실기 카메라 grab 여부(하드웨어 에러=E 판정용) — SIMUL_MODE/OfflineInspectMode 경로에선 절대 true 로 세팅 안 함
            // ShotParam.SimulImagePath = InspectionImagePath 역할 (검사 사이클 마다 로드). 티칭 기준 이미지는 별도 DatumConfig.TeachingImagePath (셋업 시 1회, INI 보존) 사용 — 역할 분리. Simul 에서 두 경로 동일 파일 가능.
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
            //260811 hbk plc-spec-260811-alignment: 실기 grab 이 null 을 반환한 경우만(SIMUL_MODE/오프라인
            //  경로는 절대 해당 없음) 사이클 하드웨어 에러로 마킹 — $RESULT 응답이 F 대신 E 로 나간다
            //  (제어팀 확정 스펙). Step 은 그대로 Measure 로 진행(기존 lenient 동작 유지, 회귀 0).
            if (image == null && bIsLiveGrabAttempt) {
                InspectionSequence parentSeqForHwErr = ShotParam.Parent as InspectionSequence;
                if (parentSeqForHwErr != null) parentSeqForHwErr.MarkCycleHardwareError();
                Logging.PrintLog((int)ELogType.Error, LOG_TAG + "SHOT '" + (ShotParam.ShotName ?? "") + "' 실기 카메라 grab 실패 — $RESULT E(하드웨어 에러) 처리");
            }
            return image;
        }

        //260819 hbk Extract Method: RunGrab 의 화면표시용 사본 처리를 그대로 옮긴 것(순수 이동, 동작 무변경).
        //  ⚠ 측정 소스 설정(데이터 경로)과 원본 이미지 해제는 호출부에 남겼다 — 여기 들어오면
        //    "조건과 무관하게 항상 수행" 계약이 조기 return 등으로 깨질 여지가 생긴다.
        //  ⚠ 안쪽 null 재확인은 원형 그대로다. 바깥 호출부가 이미 non-null 을 보장하지만,
        //    지우면 순수 이동이 아니게 되므로 중복 방어를 유지한다.
        private void UpdateViewerCopy(HImage image) {
            //260810 hbk quick-260810-egx: 아래는 "표시 전용" 사본(127MP memcpy). 자동검사 중 표시를 끄면 생략한다.
            InspectionSequence parentSeqForView;
            if (ShotParam != null) parentSeqForView = ShotParam.Parent as InspectionSequence;
            else parentSeqForView = null;
            bool bSkipViewer = IsViewerUpdateSkipped(parentSeqForView);
            if (pMyContext.ResultHalconImage != null) pMyContext.ResultHalconImage.Dispose();
            if (bSkipViewer) {
                pMyContext.ResultHalconImage = null; // 표시사본 미생성. HalconImageBridge.Clone(null)==null 이라 뒤쪽은 조용히 no-op.
            } else {
                pMyContext.ResultHalconImage = image.CopyImage();
            }
        }

        //260818 hbk [초보자용] 찍은 사진 위에서 진짜 "측정"을 하는 단계입니다. 이 Shot에 등록된 FAI(검사 그룹)와
        //  그 안의 개별 측정 항목(길이/각도/지름 등)을 하나씩 돌면서 값을 재고, 공차(허용 범위) 안에 드는지
        //  판정합니다. 하나라도 공차를 벗어나면 이 Shot 전체가 불합격(NG)으로 표시됩니다.
        private void RunMeasure() {
            int nFaiCount;
            if (ShotParam != null && ShotParam.FAIList != null) nFaiCount = ShotParam.FAIList.Count;
            else nFaiCount = 0;
            LogSeqStep("Measure", string.Format("측정 시작 — FAI {0}개",
                nFaiCount)); //260818 hbk [SEQ]
            var dctAlgoUsed = new Dictionary<string, int>(); //260818 hbk 이번 Shot 에서 실제로 탄 측정 알고리즘 종류별 횟수
            int nMeasNg = 0;                                  //260818 hbk 공차 벗어난 측정 수
            //260818 hbk [SEQ] Measure 단계 tact 측정용 — 아래 "완료 —" 단계 요약 로그가 소비한다.
            var swMeasureTotal = Stopwatch.StartNew();
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
                MeasureShotFaiList(parentSeq2, overlayAcc, dctAlgoUsed, ref allPass, ref measuredCount, ref nMeasNg, ref bShotDisplayImageReplaced);
            }
            pMyContext.AllPass = allPass;
            pMyContext.MeasuredCount = measuredCount;
            pMyContext.InspectionOverlays = overlayAcc; // overlay 누적 결과 반영
            //260818 hbk [SEQ] Measure 결과 요약 — 어떤 알고리즘을 몇 번 탔는지까지 한 줄에
            var sbAlgo = new StringBuilder();
            foreach (var kv in dctAlgoUsed) {
                if (sbAlgo.Length > 0) sbAlgo.Append(", ");
                sbAlgo.Append(kv.Key).Append(chAlgoMul).Append(kv.Value);
            }
            string szMeasureVerdict;
            if (allPass) szMeasureVerdict = "OK";
            else szMeasureVerdict = "NG";
            string szAlgoSummary;
            if (sbAlgo.Length > 0) szAlgoSummary = sbAlgo.ToString();
            else szAlgoSummary = "없음";
            LogSeqStep("Measure", string.Format("완료 — 측정 {0}개 (공차이탈 {1}개), 판정 {2} ({3:F2}초) │ 알고리즘: {4}",
                measuredCount, nMeasNg, szMeasureVerdict, swMeasureTotal.Elapsed.TotalSeconds,
                szAlgoSummary));
            Step = (int)EStep.End;
        }

        //260818 hbk Extract Method: RunMeasure 의 if(ShotParam != null) 안쪽 전체를 그대로 옮긴 것.
        //  ⚠ 조기 return 을 쓰지 않았다 — 호출부의 if 를 그대로 둬야 ShotParam==null 일 때도
        //    뒤이은 pMyContext.AllPass 기록 / [SEQ] 요약 로그 / Step=End 가 원본대로 실행된다.
        //  ⚠ 값형 4개는 반드시 ref 다(allPass/measuredCount/nMeasNg/bShotDisplayImageReplaced).
        //    ref 를 빠뜨려도 컴파일은 통과하지만 호출자 카운터가 조용히 0 으로 남는다.
        //    참조형 3개(parentSeq2/overlayAcc/dctAlgoUsed)는 재대입이 없어 값 전달로 충분하다.
        //  ⚠ using / try-finally 는 통째로 함께 이동했다. 상대 위치를 바꾸면 260810 round4 누수 수정이 무효화된다.
        private void MeasureShotFaiList(InspectionSequence parentSeq2,
                                        List<EdgeInspectionOverlay> overlayAcc,
                                        Dictionary<string, int> dctAlgoUsed,
                                        ref bool allPass, ref int measuredCount,
                                        ref int nMeasNg, ref bool bShotDisplayImageReplaced) {
            //260819 hbk quick-260819-gf1: 바깥 시그니처(ref 4개)는 RunMeasure 와의 계약이라 그대로 둔다.
            //  안쪽에서만 누적 객체로 바꿔 쓰고, 메서드 끝에서 딱 한 번 되쓴다.
            var acc = new ShotMeasureAccumulator();
            acc.AllPass = allPass;
            acc.MeasuredCount = measuredCount;
            acc.NMeasNg = nMeasNg;
            acc.ShotDisplayImageReplaced = bShotDisplayImageReplaced;
            using (var image = ShotParam.GetImage()) {
                if (image != null) {
                    // Shot당 1회 복사 공유(refcount). 검사 스레드의 FAI별 대용량 CopyImage 제거(throughput).
                    // 한 Shot 의 모든 FAI origin/capture 요청이 이 1개 복사본을 공유. capSaver 없으면 복사 자체 생략.
                    var capSaver = SystemHandler.Handle.CaptureImageSaver;
                    SharedHImage sharedSrc = null;
                    if (capSaver != null) { try { sharedSrc = new SharedHImage(image.CopyImage()); } catch { sharedSrc = null; } }
                    // try 를 sharedSrc 를 만들자마자로 넓혔다 — 그 전에는 사이에서 예외가 나면 이 이미지가
                    //  release 안 되고 새는 버그가 있었다(참조 카운트가 이중으로 새는 경우까지 있었음).
                    try {
                    // datum 검출 오버레이 스냅샷(시퀀스 단위, 전 FAI 공유). 값만 추출해 워커 async race 차단.
                    List<DatumCaptureOverlay> datumSnapshot = BuildDatumCaptureSnapshot(parentSeq2);
                    //260619 hbk per-shot 보정계수 적용 = PixelResolution × CorrectionFactor (단일소스 GetEffectivePixelResolution). PixelResolution 저장값 불변.
                    double pixRes; //260615 hbk Phase 42 D-01 Shot 단일소스
                    if (ShotParam != null) pixRes = ShotParam.GetEffectivePixelResolution();
                    else pixRes = 1.0;
                    // ±2% 를 넘으면 경고 로그를 남기던 걸 뺐다 — 이제 보정계수 자체가 배율까지 포함하는 방식이라
                    //  ±2% 초과가 정상 범위라서, 매번 뜨는 경고가 그냥 노이즈였다(로그만 영향, 검사엔 무관).
                    // 원본 이미지는 이 Shot 의 모든 FAI 가 완전히 같은 걸 보므로, FAI 마다 따로 저장하지 않고
                    //  Shot 당 한 번만 저장 큐에 넣는다(항목별로 매번 저장하면 느려진다).
                    string szSharedOriginPath = QueueSharedShotOrigin(sharedSrc, parentSeq2);
                    foreach (var fai in ShotParam.FAIList) {
                        acc.FaiAllPass = true;
                        var faiOverlays = new List<EdgeInspectionOverlay>(); // per-FAI overlay 누적 (LastOverlays write-back 용, 노드 클릭 재현)
                        //260729 hbk quick-fix(260729-hwb): 이 FAI tick 에서 실제로 캡처된 크로스-Z role 이미지의
                        //  소유 사본(같은 FAI 안에서 첫 캡처가 이김). null 이면 AggregateFaiResult 는 종전과
                        //  동일하게 sharedSrc 를 쓴다(비-크로스-Z 회귀 0). 새 필드 아님 — per-FAI 지역변수.
                        acc.CrossZRoleImage = null;
                        foreach (var meas in fai.Measurements) {
                            ProcessOneMeasurement(meas, parentSeq2, image, pixRes, acc, overlayAcc, faiOverlays, dctAlgoUsed);
                        }
                        FinalizeFaiTick(fai, acc.FaiAllPass, faiOverlays, sharedSrc, datumSnapshot, szSharedOriginPath, parentSeq2, acc);
                    }
                    } finally { // 검사 루프 소유 ref 1 해제(워커 요청들의 ref 와 독립). 마지막 Release 시 공유 이미지 dispose.
                        if (sharedSrc != null) sharedSrc.Release();
                    }
                } else {
                    //260616 hbk simul-shot-cascade: 이미지 미취득(SimulImagePath 무효) SHOT 은 모든 measurement NG 처리.
                    //  과거엔 공유 카메라 캐시 fallback 으로 항상 이미지가 채워져 이 분기가 사실상 미도달 → fallback 제거 후
                    //  무효 경로 SHOT 이 image==null 도달. allPass 가 default true 로 남아 잘못 PASS 되는 것을 차단.
                    acc.AllPass = false;
                    MarkAllMeasurementsNoImage(ref acc.MeasuredCount); //260702 hbk Extract Method(Task2)
                }
            }
            //260819 hbk quick-260819-gf1: 되쓰기 — using 블록 바깥이라 image!=null / image==null 두 경로가
            //  전부 여기로 합류한다. 예외로 탈출하는 경우 되쓰기는 생략되지만 관측 불가하다 —
            //  RunMeasure 에는 try/catch 가 없어(실측) 호출부도 함께 unwind 되고,
            //  ref 로 읽는 지점(원래 RunMeasure 대입문) 이후 문장에 애초에 도달하지 못한다.
            allPass = acc.AllPass;
            measuredCount = acc.MeasuredCount;
            nMeasNg = acc.NMeasNg;
            bShotDisplayImageReplaced = acc.ShotDisplayImageReplaced;
        }

        //260702 hbk Extract Method(Task3): Measure per-measurement 루프 본문(원본 foreach meas 내부, 동치 보장, continue->return)
        private void ProcessOneMeasurement(MeasurementBase meas, InspectionSequence parentSeq2,
                                     HImage image, double pixRes,
                                     ShotMeasureAccumulator acc,
                                     List<EdgeInspectionOverlay> overlayAcc,
                                     List<EdgeInspectionOverlay> faiOverlays,
                                     Dictionary<string, int> dctAlgoUsed) {
            if (!TryGateMeasurement(meas, parentSeq2, acc)) return;
            DualImageEdgeDistanceMeasurement dualMeasForGate;
            bool bHasAnyZIndex;
            if (!EvaluateCrossZGate(meas, parentSeq2, acc, out dualMeasForGate, out bHasAnyZIndex)) return;
            HTuple transform = ResolveDatumTransform(parentSeq2, meas.DatumRef); //260702 hbk Extract Method(Task1)
            InjectDatumOrigin(meas, parentSeq2); //260702 hbk Extract Method(Task1)
            double resultValue;
            string measError;
            List<EdgeInspectionOverlay> measOverlays;
            bool ok;
            var swMeasureExec = Stopwatch.StartNew(); //260818 hbk 알고리즘 로그용 측정 실행시간
            if (bHasAnyZIndex)
            {
                ok = TryExecuteCrossZMeasurement(dualMeasForGate, parentSeq2, transform, pixRes, out resultValue, out measError, out measOverlays); //260722 hbk Phase 68 D-02a: 완성 index 크로스-Z 실행
            }
            else
            {
                ok = TryExecuteMeasurement(meas, image, transform, pixRes, out resultValue, out measError, out measOverlays); //260702 hbk Extract Method(Task1)
            }
            RecordMeasurementResult(meas, bHasAnyZIndex, ok, resultValue, measError, measOverlays, overlayAcc, faiOverlays, dctAlgoUsed, swMeasureExec, acc);
        }

        //260819 hbk quick-260819-hyk: ProcessOneMeasurement 의 초기 게이트 2개를 그대로 옮긴 것.
        //  원본에서 이 자리의 'return;' 2곳은 ProcessOneMeasurement 자체를 빠져나가는 문장이었다.
        //  여기서는 false 를 돌려주고, 호출부가 false 를 받으면 즉시 return 해서 동일한 탈출을 재현한다.
        //  게이트 본문(Mark 호출 / 누적 대입 / 시도회수 통계)은 한 글자도 바뀌지 않았다.
        private bool TryGateMeasurement(MeasurementBase meas, InspectionSequence parentSeq2, ShotMeasureAccumulator acc) {
            // per-FAI gate: 해당 datum 이 검출 실패했으면 측정 skip, NG 누적, 다음 meas 진행.
            // Step=Grab 변경 안 함 (lenient 유지). 본 게이트는 Measure 루프 안에서만 동작.
            // 빈 DatumRef (무보정) 또는 성공 datum 참조는 IsDatumFailed=false → 기존 identity fallback / transform 경로 진행.
            if (parentSeq2 != null && parentSeq2.IsDatumFailed(meas.DatumRef))
            {
                MarkMeasurementDatumSkipped(meas, parentSeq2); //260702 hbk Extract Method(Task1)
                acc.FaiAllPass = false;
                acc.MeasuredCount++; // 시도 회수 통계
                return false; // 다음 measurement 진행 (TryExecute 호출 안 함)
            }
            //260716 hbk DatumRef 참조 불일치 게이트 — 오타/개명/삭제로 실존하지 않는 datum 을 가리키면
            //  검출 시도 자체가 없어 IsDatumFailed 게이트를 우회하고 identity(무보정)로 조용히 측정되던 결함 차단.
            //  '무보정 의도(빈 DatumRef)'와 '참조 깨짐'을 구분해 후자만 NG 로 승격(기존 lenient 구조 유지).
            if (parentSeq2 != null && parentSeq2.IsDatumRefUnresolvable(meas.DatumRef))
            {
                MarkMeasurementDatumRefMissing(meas);
                acc.FaiAllPass = false;
                acc.MeasuredCount++;
                return false;
            }
            return true; // 두 게이트 모두 통과 — 호출부는 측정 실행 경로로 계속 진행
        }

        //260819 hbk quick-260819-hyk: 크로스-Z 게이트 판정 전체를 그대로 옮긴 것.
        //  반환값 계약 — false 는 "이 tick 에 측정을 실행하지 않는다"(설정오류 / 무관tick / 캡처실패 /
        //  짝 미완성 = 4경로), true 는 "공용 실행 경로로 계속 진행한다"(짝 완성 1경로 + 크로스-Z 가
        //  아닌 일반 측정 1경로 = 2경로) 를 뜻한다. 원본에서 각각 return / fall-through 였던 것이다.
        //  case 본문(로그·누적·캡처 호출)은 한 글자도 바뀌지 않았다. 바뀐 것은 각 case 끝의
        //  제어흐름 키워드 1단어뿐이다.
        //  out 2개는 본문 첫 2줄에서 무조건 대입되므로 모든 반환 경로에서 확정 대입이다.
        private bool EvaluateCrossZGate(MeasurementBase meas, InspectionSequence parentSeq2, ShotMeasureAccumulator acc,
                                        out DualImageEdgeDistanceMeasurement dualMeasForGate, out bool bHasAnyZIndex) {
            //260722 hbk Phase 68 D-02a/D-05: 크로스-Z(ZIndexA/B 둘 다 -1 아님) 측정 게이트/캡처.
            //  ZIndexA/B 둘 다 -1(미설정) 이면 이 블록 진입 안 함 → 기존 경로 그대로(D-07 회귀 0).
            dualMeasForGate = meas as DualImageEdgeDistanceMeasurement;
            bHasAnyZIndex = dualMeasForGate != null && (dualMeasForGate.ZIndexA != UNSET_ZINDEX || dualMeasForGate.ZIndexB != UNSET_ZINDEX);
            if (bHasAnyZIndex)
            {
                // 판정을 명시적인 상태값(ECrossZGate)으로 뽑아서 아래 switch 한 곳에서만 처리한다.
                //  실제 캡처/저장을 수행하는 ProcessCrossZCaptureTick 호출은 일부러 switch 밖에 남겨뒀다 —
                //  "설정 오류를 통과한 경우에만 캡처한다"는 순서와 호출 횟수를 그대로 보존하기 위해서다.
                //260819 hbk quick-260819-sgg: ProcessCrossZCaptureTick 4-out → CrossZCaptureTickResult. tickResult 를
                //  all-default 로 미리 선언 — bMisconfigured 분기는 ProcessCrossZCaptureTick 을 안 부르므로 원본 초기값과 동치.
                CrossZCaptureTickResult tickResult = new CrossZCaptureTickResult();
                bool bNonProtocolCycle = false;
                ECrossZGate eGate;
                bool bMisconfigured = IsZIndexMisconfigured(dualMeasForGate, parentSeq2);
                if (bMisconfigured)
                {
                    eGate = ECrossZGate.Misconfigured;
                }
                else
                {
                    tickResult = ProcessCrossZCaptureTick(dualMeasForGate, parentSeq2);
                    // 수동으로 RUN 버튼을 눌러 검사할 때는 한 번만 찍어서 크로스-Z 두 장이 절대 안 모인다 —
                    //  조용히 넘어가면 "안 잰 것도 합격"되는 버그가 있어 NG 처리한다.
                    // 자동(PLC) 촬영은 다음 번에 나머지가 채워지니 그냥 기다린다.
                    bNonProtocolCycle = parentSeq2 == null || !parentSeq2.IsProtocolDrivenCycle();
                    eGate = ResolveCrossZGate(tickResult.Relevant, tickResult.CaptureOk, tickResult.Completed);
                }
                //260818 hbk default: 를 두지 않는다 — 5개 멤버를 전부 다루고 있고, default 를 추가하면
                //  감사되지 않은 6번째 경로가 생긴다. 멤버를 늘릴 일이 생기면 반드시 이 switch 도 함께 고칠 것.
                switch (eGate)
                {
                    case ECrossZGate.Misconfigured:
                        MarkMeasurementZIndexMisconfigured(meas);
                        acc.FaiAllPass = false;
                        acc.MeasuredCount++;
                        return false;
                    case ECrossZGate.NotMyTick:
                        if (bNonProtocolCycle)
                        {
                            MarkMeasurementCrossZIncomplete(meas, false, false, parentSeq2);
                            acc.FaiAllPass = false;
                            acc.MeasuredCount++;
                        }
                        return false; // 프로토콜: 이 tick 은 이 측정과 무관 — 상태변화 없음(안전망, 무변경)
                    case ECrossZGate.CaptureFailed:
                        meas.ClearResult();
                        meas.LastSkipReason = SkipReason.NO_IMAGE;
                        meas.LastJudgement = false;
                        acc.FaiAllPass = false;
                        acc.MeasuredCount++;
                        return false;
                    case ECrossZGate.HalfPending:
                        TakeCrossZRoleImageIfFirst(parentSeq2, tickResult.CaptureOk, tickResult.CapturedRoleKey, ref acc.CrossZRoleImage);
                        MarkCrossZHalfPending(meas, parentSeq2, bNonProtocolCycle, ref acc.FaiAllPass, ref acc.MeasuredCount);
                        return false;
                    case ECrossZGate.BothReady:
                        TakeCrossZRoleImageIfFirst(parentSeq2, tickResult.CaptureOk, tickResult.CapturedRoleKey, ref acc.CrossZRoleImage);
                        return true; // 완성 index — 아래 공용 실행 경로로 계속 진행(transform/InjectDatumOrigin 재사용)
                }
            }
            return true; // 크로스-Z 가 아닌 일반 측정 — 원본에서 if 블록을 건너뛰던 경로와 동치
        }

        //260819 hbk quick-260819-hyk: ProcessOneMeasurement 의 마무리부(판정 / 실패로그 / 오버레이 누적 /
        //  카운터)를 그대로 옮긴 것. 이 구간에는 제어흐름 문장이 애초에 0개라 순수 이동이다.
        //  Stopwatch 를 통째로 받는 이유는 LogAndTallyAlgorithm 과 같다 — 호출부에서 ms 를 미리 읽으면
        //  로그에 찍히는 숫자가 달라진다.
        private void RecordMeasurementResult(MeasurementBase meas, bool bHasAnyZIndex, bool ok,
                                             double resultValue, string measError, List<EdgeInspectionOverlay> measOverlays,
                                             List<EdgeInspectionOverlay> overlayAcc, List<EdgeInspectionOverlay> faiOverlays,
                                             Dictionary<string, int> dctAlgoUsed, Stopwatch swMeasureExec,
                                             ShotMeasureAccumulator acc) {
            LogAndTallyAlgorithm(meas, bHasAnyZIndex, ok, dctAlgoUsed, swMeasureExec);
            if (ok) {
                meas.EvaluateJudgement(resultValue);
                if (!meas.LastJudgement) acc.NMeasNg++; //260818 hbk [SEQ] 요약용 공차이탈 집계
            } else {
                string measName = GetMeasurementDisplayName(meas);
                string measErrorStr = measError;
                measErrorStr = measErrorStr ?? "";
                Logging.PrintLog((int)ELogType.Error, LOG_TAG + "Measurement '" + measName + "' failed: " + measErrorStr);
                meas.ClearResult();
                meas.LastJudgement = false;
            }
            ApplyOverlaySuffixAndAccumulate(meas, measOverlays, overlayAcc, faiOverlays); //260702 hbk Extract Method(Task2)
            if (!meas.LastJudgement) {
                acc.FaiAllPass = false;
            }
            acc.MeasuredCount++;
        }

        // 이름에 Tally 가 붙은 건 로그만 찍는 게 아니라서다 — dctAlgoUsed(Shot 안에서 어떤 알고리즘을
        //  몇 번 썼는지)를 같이 갱신한다. Stopwatch 를 통째로 넘기는 이유는, 시간을 미리 재서 넘기면
        //  로그에 찍히는 숫자가 실제보다 앞당겨지기 때문이다 — 로그를 찍는 시점에 직접 읽어야 한다.
        private void LogAndTallyAlgorithm(MeasurementBase meas, bool bHasAnyZIndex, bool bOk,
                                          Dictionary<string, int> dctAlgoUsed, Stopwatch swMeasureExec) {
            //260818 hbk 어떤 측정 알고리즘을 탔는지 — Shot 요약용 집계 + Algorithm 탭 상세 1줄
            string szAlgoType = meas.TypeName;
            if (string.IsNullOrEmpty(szAlgoType)) szAlgoType = "?";
            string szAlgoEntry;
            if (bHasAnyZIndex) szAlgoEntry = "TryExecuteCrossZMeasurement";
            else szAlgoEntry = "TryExecuteMeasurement";
            if (dctAlgoUsed.ContainsKey(szAlgoType)) dctAlgoUsed[szAlgoType]++;
            else dctAlgoUsed[szAlgoType] = 1;
            string szAlgoShotName;
            if (ShotParam != null) szAlgoShotName = ShotParam.ShotName;
            else szAlgoShotName = "?";
            string szAlgoResult;
            if (bOk) szAlgoResult = "OK";
            else szAlgoResult = "FAIL";
            Logging.PrintLog((int)ELogType.Algorithm, "[ALGO] {0} · {1} type={2} → {3} ({4}) {5}ms",
                szAlgoShotName,
                (meas.MeasurementName ?? szAlgoType), szAlgoType, szAlgoEntry,
                szAlgoResult, swMeasureExec.ElapsedMilliseconds);
        }

        //260818 hbk 크로스-Z 게이트 상태 분류 — 순수 함수다(인자 3개 bool 외에는 아무것도 읽지 않고,
        //  아무것도 쓰지 않는다). 부수효과가 있는 IsZIndexMisconfigured / ProcessCrossZCaptureTick /
        //  IsProtocolDrivenCycle 호출은 일부러 호출부에 남겼다.
        //  판정 순서(bRelevant → bCaptureOk → bCompleted)는 원본 중첩 if 순서와 1:1 이다.
        private ECrossZGate ResolveCrossZGate(bool bRelevant, bool bCaptureOk, bool bCompleted) {
            if (!bRelevant) return ECrossZGate.NotMyTick;
            if (!bCaptureOk) return ECrossZGate.CaptureFailed;
            if (!bCompleted) return ECrossZGate.HalfPending;
            return ECrossZGate.BothReady;
        }

        // 이번 tick 에서 실제로 캡처된 이미지의 사본을 받아둔다(같은 FAI 안에서 먼저
        //  캡처된 쪽이 이긴다) — 화면 표시/저장에 이 사본을 쓴다.
        // 조건식은 원래 코드 그대로 뒀다 — 지금은 항상 참인 항이 있지만, 나중에 코드가
        //  바뀌어도 안전하도록 남겨둔다.
        private void TakeCrossZRoleImageIfFirst(InspectionSequence parentSeq2, bool bCaptureOk, string szCapturedRoleKey, ref HImage crossZRoleImage) {
            if (bCaptureOk && crossZRoleImage == null && !string.IsNullOrEmpty(szCapturedRoleKey) && parentSeq2 != null)
            {
                crossZRoleImage = parentSeq2.TakeCrossZImageCopy(szCapturedRoleKey);
            }
        }

        //260818 hbk HalfPending(A/B 중 한쪽만 모임) case 본문 — 원본 if(!bCompleted){…} 블록의 순수 이동.
        private void MarkCrossZHalfPending(MeasurementBase meas, InspectionSequence parentSeq2, bool bNonProtocolCycle, ref bool faiAllPass, ref int measuredCount) {
            if (bNonProtocolCycle)
            {
                MarkMeasurementCrossZIncomplete(meas, true, false, parentSeq2);
                faiAllPass = false;
            }
            else
            {
                // 자동(PLC) 촬영이든 수동이든, 짝이 아직 안 모인 상태를 "합격"으로 방치하지 않고
                //  명시적으로 미완성 표시한다. 이 표시는 화면/파일명에만 영향을 주고, PLC 로 나가는
                //  최종 응답에는 영향이 없다(미완성 tick 은 애초에 보고 대상에서 빠지기 때문).
                MarkMeasurementCrossZIncomplete(meas, true, true, parentSeq2);
                faiAllPass = false;
            }
            measuredCount++; // 프로토콜 Z1(비완성 index): 캡처만 — NG 아님, 미보고(Task4 index 게이트가 보장)
        }

        //260702 hbk Extract Method(Task3): Measure per-FAI 저장/표시 마무리(원본 meas 루프 뒤 try/finally, 동치 보장)
        private void FinalizeFaiTick(FAIConfig fai, bool faiAllPass,
                                     List<EdgeInspectionOverlay> faiOverlays, SharedHImage sharedSrc,
                                     List<DatumCaptureOverlay> datumSnapshot, string szSharedOriginPath,
                                     InspectionSequence parentSeq2,
                                     ShotMeasureAccumulator acc) {
            try {
                if (acc.CrossZRoleImage == null) {
                    AggregateFaiResult(fai, faiAllPass, faiOverlays, sharedSrc, datumSnapshot, szSharedOriginPath); //260702 hbk Extract Method(Task2)
                } else {
                    // 크로스-Z 측정의 캡처/저장은 고정 사본 대신 실제로 측정에 쓰인 이미지를 쓴다.
                    // 크로스-Z 는 FAI 마다 다른 이미지를 참조할 수 있어서 Shot 공유 저장을 못 쓴다 —
                    //  FAI 마다 따로 저장한다(회귀 없음).
                    SharedHImage crossZSharedSrc = null;
                    try {
                        try { crossZSharedSrc = new SharedHImage(acc.CrossZRoleImage.CopyImage()); } catch { crossZSharedSrc = null; }
                        AggregateFaiResult(fai, faiAllPass, faiOverlays, crossZSharedSrc, datumSnapshot, null);
                    } finally {
                        if (crossZSharedSrc != null) crossZSharedSrc.Release();
                    }
                    //260810 hbk quick-260810-egx: 표시 전용 교체 블록 — 자동검사 중 표시를 끄면 통째로 건너뛴다
                    //  (127MP memcpy 제거). 위 AggregateFaiResult/crossZSharedSrc 저장 경로와 아래 finally 의
                    //  acc.CrossZRoleImage.Dispose() 는 조건과 무관하게 그대로 수행된다.
                    if (!acc.ShotDisplayImageReplaced && !IsViewerUpdateSkipped(parentSeq2)) {
                        //260729 hbk quick-fix(260729-hwb): Shot 전체에서 첫 크로스-Z 캡처가 화면을
                        //  차지한다(결정론적 규칙) — 정적 대표 사진 대신 실제 측정 사진을 표시.
                        if (pMyContext.ResultHalconImage != null) pMyContext.ResultHalconImage.Dispose();
                        pMyContext.ResultHalconImage = acc.CrossZRoleImage.CopyImage();
                        acc.ShotDisplayImageReplaced = true;
                    }
                }
                if (!faiAllPass) acc.AllPass = false;
            } finally {
                SafeDisposeImage(acc.CrossZRoleImage); acc.CrossZRoleImage = null;
            }
        }

        //260818 hbk [초보자용] 이 Shot의 검사가 다 끝났다고 알리는 마지막 단계입니다. Measure 단계에서 하나라도
        //  불합격이 있었으면 전체를 Fail로, 전부 합격이면 Pass로 확정해서 이 Action을 종료합니다.
        private void RunEnd() {
            EContextResult finishResult;
            if (pMyContext.AllPass) finishResult = EContextResult.Pass;
            else finishResult = EContextResult.Fail;
            FinishAction(finishResult);
        }

        // 검사 이미지 경로가 하나뿐이고 실패하면 null + 로그만 남긴다.
        //  예전엔 실패 시 다른 카메라의 캐시 이미지로 몰래 대체했었는데, 그러면 경로가 잘못된 SHOT 이
        //  엉뚱한(또는 방금 전) 이미지로 측정되는 문제가 있었다 — 지금은 그 SHOT 만 측정 skip 하고
        //  다른 SHOT 에는 영향을 주지 않는다.
        private HImage LoadShotInspectionImage() {
            if (ShotParam == null) return null;
            if (!string.IsNullOrEmpty(ShotParam.SimulImagePath) && File.Exists(ShotParam.SimulImagePath)) {
                try {
                    return new HImage(ShotParam.SimulImagePath);
                } catch (Exception ex) {
                    Logging.PrintLog((int)ELogType.Error, LOG_TAG + "SHOT '" + (ShotParam.ShotName ?? "") + "' 검사이미지 로드 실패: " + ex.Message);
                    return null;
                }
            }
            Logging.PrintLog((int)ELogType.Error, LOG_TAG + "SHOT '" + (ShotParam.ShotName ?? "") + "' 검사이미지 경로 비어있음/파일없음 — 이 SHOT 만 측정 skip (공유 카메라 캐시 fallback 차단). 오프라인 모드면 '검사이미지 Grab' 으로 먼저 확보 필요.");
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

        // 자동(PLC) 검사 중 화면표시용 이미지 사본 만들기를 생략할지 정한다 — 생략하면 Shot 당
        //  대용량 복사 2번을 아낀다. 세 조건이 다 맞을 때만 생략한다: ①자동 검사일 것(수동/티칭은
        //  항상 표시 유지) ②설정에서 켜져 있을 것(기본은 꺼짐=기존 동작) ③불량 이미지 저장 설정이
        //  꺼져 있을 것 — 이게 핵심 조건이다. 불량 이미지 저장이 켜져 있으면 이 표시용 사본이
        //  실제 저장 데이터로도 쓰이므로, 생략하면 저장이 조용히 깨진다.
        // 이 설정은 "화면 표시"에만 적용된다 — 결과 이미지 저장 자체는 이 값과 무관하게 항상 된다.
        private bool IsViewerUpdateSkipped(InspectionSequence parentSeq) {
            if (parentSeq == null) return false;
            if (!parentSeq.IsProtocolDrivenCycle()) return false;
            SystemSetting setting = SystemSetting.Handle;
            if (setting == null) return false;
            if (!setting.DisableViewerDuringAutoInspect) return false;
            if (setting.SaveFailImage) return false;
            return true;
        }

        //260820 hbk quick-260820-dfw: 6개 함수(가로/세로 이미지+bPending)를 관통하던 out 3종 조합을
        //  DualDatumImageResult 필드로 교체 — 파일 상단 ShotMeasureAccumulator/CrossZCaptureTickResult 와
        //  동일한 필드(프로퍼티 아님)+K&R 스타일을 따른다. 외부 호출부 1곳(ProcessDatumDualImage)에
        //  보이는 이 함수의 out 시그니처는 그대로 유지 — 내부 5개 함수만 result 객체로 배선.
        private class DualDatumImageResult {
            public HImage Horizontal;
            public HImage Vertical;
            public bool Pending;
        }

        // 양쪽(A/B) 이미지를 동시에 로드한다. 둘 다 설정돼 있으면 크로스-Z 실시간 촬영 경로,
        //  하나도 없으면 기존 고정 이미지 경로다. "대기 중"(bPending) 은 실패가 아니라 다음 촬영을
        //  기다리는 정상 상태다.
        // 수동(RUN/반복/일괄) 검사는 이 기준점의 짝(A/B)이 절대 완성될 수 없는 구조다 — 방치하면
        //  이 기준점이 매번 조용히 건너뛰어지고(실패로 표시되지 않음), 예전에 검출 성공했을 때의
        //  위치(몇 시간~며칠 전 값일 수 있음)가 실패 표시 없이 계속 재사용된다. 자동(PLC) 검사는
        //  이 문제와 무관하며 완전히 그대로 동작한다.
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
            DualDatumImageResult result = new DualDatumImageResult();
            bool bOk;
            if (bCrossZEnabled) {
                bOk = TryGrabOrLoadCrossZDatumImages(datum, parentSeq, result);
            } else {
                bOk = TryLoadStaticDualDatumImages(datum, result);
            }
            imageHorizontal = result.Horizontal;
            imageVertical = result.Vertical;
            bPending = result.Pending;
            return bOk;
        }

        // 기존 고정 이미지 로드 경로 — 가로/세로 각각 교시 이미지가 우선이고, 없거나 로드 실패하면
        //  이 Shot 의 검사 이미지로 대신한다. 교시 이미지가 아직 없어도 통신 테스트가 막히지 않게
        //  하기 위한 폴백이다. 검사 이미지조차 없으면 그대로 실패 처리한다.
        // 두 반환 이미지는 호출부에서 각각 따로 해제(Dispose)하므로, 폴백 때도 서로 다른 인스턴스로
        //  각각 새로 만들어야 한다(같은 인스턴스를 공유하면 안 됨).
        private bool TryLoadStaticDualDatumImages(DatumConfig datum, DualDatumImageResult result) {
            string pathH = datum.TeachingImagePath;
            string pathV = datum.TeachingImagePath_Vertical;

            bool bFallbackH = string.IsNullOrEmpty(pathH) || !File.Exists(pathH);
            bool bFallbackV = string.IsNullOrEmpty(pathV) || !File.Exists(pathV);

            result.Horizontal = LoadDatumImageFromPath(datum, pathH, false); // teachingPath → SimulImagePath 폴백 (grab 없음)
            result.Vertical = LoadDatumImageFromPath(datum, pathV, false);   // 동일 폴백, 별도 인스턴스

            if (result.Horizontal == null) {
                Logging.PrintErrLog((int)ELogType.Error, "[Datum] 가로축 이미지 확보 실패 — TeachingImagePath / ShotParam.SimulImagePath 모두 없음 (DualImage).");
            } else if (bFallbackH) {
                // 폴백이 조용히 일어나면 "티칭 이미지로 검출했다" 고 오해할 수 있어 흔적을 남긴다.
                Logging.PrintLog((int)ELogType.Trace, "[Datum] 가로축 티칭 이미지 부재 — SHOT 검사이미지(SimulImagePath)로 폴백 (DualImage).");
            }
            if (result.Vertical == null) {
                Logging.PrintErrLog((int)ELogType.Error, "[Datum] 세로축 이미지 확보 실패 — TeachingImagePath_Vertical / ShotParam.SimulImagePath 모두 없음 (DualImage).");
            } else if (bFallbackV) {
                Logging.PrintLog((int)ELogType.Trace, "[Datum] 세로축 티칭 이미지 부재 — SHOT 검사이미지(SimulImagePath)로 폴백 (DualImage).");
            }

            if (result.Horizontal == null || result.Vertical == null) {
                SafeDisposeImage(result.Horizontal);
                SafeDisposeImage(result.Vertical);
                result.Horizontal = null;
                result.Vertical = null;
                return false;
            }
            return true;
        }

        //260722 hbk Phase 68 D-06/D-02a: Datum 크로스-Z 라이브 캡처/주입 — 완성 z_index=max(ZIndexA,ZIndexB)
        //  (측정 레벨 TryExecuteCrossZMeasurement 완성 index 정의와 통일). 현재 tick 이 이 datum 의 ZIndexA/B
        //  어느 쪽도 아니면 무관(bPending=true, 상태변화 없음 — ProcessCrossZCaptureTick bRelevant 미러).
        private bool TryGrabOrLoadCrossZDatumImages(DatumConfig datum, InspectionSequence parentSeq, DualDatumImageResult result) {
            if (parentSeq == null) {
                Logging.PrintErrLog((int)ELogType.Error, "[Datum] 크로스-Z: parentSeq null");
                return false;
            }
            int nCurZ = parentSeq.GetExecutionZIndex();
            bool bIsRoleA = nCurZ == datum.ZIndexA;
            bool bIsRoleB = nCurZ == datum.ZIndexB;
            bool bRelevant = bIsRoleA || bIsRoleB;
            if (!bRelevant) {
                // 이 tick 과 무관한 기준점은 여기서 다시 검출하지 않고 건너뛴다 — 매 단계 진입마다
                //  기존 검출 결과가 지워지므로, 이미 두 이미지가 다 모여 있는 기준점만 여기서
                //  다시 검출해 정확한 값을 만든다.
                bool bBothStored = IsCrossZDatumBothStored(datum, parentSeq);
                if (bBothStored) {
                    return TryReDetectCrossZDatumFromStore(datum, parentSeq, result);
                }
                result.Pending = true; // 저장 미완성 + 이 tick 무관 — 상태변화 없음(안전망)
                return false;
            }
            if (!CaptureAndStoreCrossZDatumImage(datum, parentSeq, bIsRoleA)) {
                return false; // 실제 캡처 실패 — 호출부가 MarkDatumFailed(실패 확정)
            }
            return TryTakeCompletedCrossZDatumImages(datum, parentSeq, result);
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
            SafeDisposeImage(capturedImage); // Store 가 CopyImage 로 소유 클론 저장 — 원본은 여기서 즉시 해제
            return true;
        }

        // 양 role(A/B) 저장 완료 여부 판정 — 완성이면 클론 반환(호출부 finally Dispose 계약), 아니면 bPending=true(Z1 캡처만).
        private bool TryTakeCompletedCrossZDatumImages(DatumConfig datum, InspectionSequence parentSeq, DualDatumImageResult result) {
            string keyA, keyB;
            ResolveCrossZDatumRoleKeys(datum, out keyA, out keyB);
            bool bCompleted = parentSeq.HasCrossZImage(keyA) && parentSeq.HasCrossZImage(keyB);
            if (!bCompleted) {
                result.Pending = true; // Z1(비완성 index): 캡처만 — 실패 아님
                return false;
            }
            return TryTakeCrossZImageClones(keyA, keyB, parentSeq, result);
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
        private bool TryReDetectCrossZDatumFromStore(DatumConfig datum, InspectionSequence parentSeq, DualDatumImageResult result) {
            string keyA, keyB;
            ResolveCrossZDatumRoleKeys(datum, out keyA, out keyB);
            return TryTakeCrossZImageClones(keyA, keyB, parentSeq, result);
        }

        // 저장소 키 두 개로부터 클론 취득 공용 로직 — TryTakeCompletedCrossZDatumImages/TryReDetectCrossZDatumFromStore
        //  가 공유(D-09 동일 로직 2회 이상 반복 금지). 한쪽만 취득 성공 시 누수 방지를 위해 양쪽 모두 Dispose.
        private bool TryTakeCrossZImageClones(string keyA, string keyB, InspectionSequence parentSeq, DualDatumImageResult result) {
            result.Horizontal = parentSeq.TakeCrossZImageCopy(keyA);
            result.Vertical = parentSeq.TakeCrossZImageCopy(keyB);
            bool bBothLoaded = result.Horizontal != null && result.Vertical != null;
            if (!bBothLoaded) {
                SafeDisposeImage(result.Horizontal);
                SafeDisposeImage(result.Vertical);
                result.Horizontal = null;
                result.Vertical = null;
                return false; // 완성 index 인데 클론 취득 실패 — 실제 실패
            }
            return true;
        }

        // 이미지 소스 우선순위를 정하는 부분만 따로 뺐다.
        //  예전에 실기 촬영을 최우선으로 두는 버그가 있었다 — 이 현장처럼 매 사이클 이미지를
        //  자동으로 채워두는 운영 모드에서는 그게 항상 참이 되어, 운영자가 직접 지정한 교시
        //  이미지가 매번 무시되는 문제가 있었다(측정값이 안 나오는 증상). 지금 순서: ①운영자가
        //  직접 지정한 경로 ②실기 촬영 ③기본 검사 이미지.
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
                SafeDisposeImage(imageA);
                SafeDisposeImage(imageB);
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

        // Shot 안의 모든 FAI 가 완전히 같은 원본 이미지를 보므로, 원본 저장은 Shot 당 한 번만
        //  큐에 넣는다(항목마다 저장하면 항목 수만큼 느려진다). 파일 경로만 돌려주고 판정 값은
        //  담지 않는다 — 어차피 Shot 전체가 공유하는 파일이라서다.
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

        //260819 hbk quick-260819-rle: 파일명/경로 결정(순수 계산 + fai.Last*ImageFileName 기록)과
        //  큐잉(Enqueue) 부수효과를 분리 — originName == null 이면 공유 origin 재사용(큐잉 불필요).
        private void ResolveFaiCaptureFileNames(FAIConfig fai, List<EdgeInspectionOverlay> faiOverlays, string sequenceName, string szSharedOriginPath, DateTime ts, out string captureName, out string originName) {
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

            captureName = CaptureImageSaveService.BuildFileName("capture", sequenceName, fai.FAIName, seg, judge, ts, nIndexNumber);  //260622 hbk Phase 48 PROTO-01
            // 동기 write-back — BuildDto 가 즉시 읽을 수 있도록 (PNG write 실패와 무관하게 경로는 확정)
            // 엑셀/cycle.json 에 절대 경로(경로\파일명) 표기. 실제 저장 경로와 동일한 BuildFilePath 로 기록.
            fai.LastCaptureImageFileName = CaptureImageSaveService.BuildFilePath(true, captureName, ts);

            bool bUseSharedOrigin = !string.IsNullOrEmpty(szSharedOriginPath);
            if (bUseSharedOrigin) {
                fai.LastOriginImageFileName = szSharedOriginPath; // Shot 공유 origin — 이미 저장됨, 경로만 기록
                originName = null;
            } else {
                // 크로스-Z 등 FAI 마다 원본 내용이 실제로 다를 수 있는 경로 — 기존과 동일하게 FAI 마다 개별 저장.
                originName = CaptureImageSaveService.BuildFileName("origin", sequenceName, fai.FAIName, seg, judge, ts, nIndexNumber);   //260622 hbk Phase 48 PROTO-01: 자재번호 포함 파일명
                fai.LastOriginImageFileName = CaptureImageSaveService.BuildFilePath(false, originName, ts);
            }
        }

        // 원본/캡처 이미지를 비동기 저장 큐에 넣고, 파일명은 즉시(동기) 확정해둔다 — 결과 데이터가
        //  이 파일명을 바로 읽어가므로 미리 정해둬야 한다. 실제 PNG 저장만 백그라운드에서 나중에 한다.
        // 원본이 Shot 당 한 번만 저장된 경우엔 여기서 다시 저장하지 않고 경로만 기록한다 —
        //  크로스-Z(항목마다 다른 이미지일 수 있음)는 항목마다 따로 저장한다.
        private void QueueFaiCapture(FAIConfig fai, SharedHImage sharedSrc, List<EdgeInspectionOverlay> faiOverlays, List<DatumCaptureOverlay> datumSnapshot, string sequenceName, string szSharedOriginPath) {
            if (fai == null) return;
            var saver = SystemHandler.Handle.CaptureImageSaver;
            DateTime ts = DateTime.Now; // origin(개별 저장 시)/capture 동일 timestamp 공유 (쌍)

            string captureName;
            string originName;
            ResolveFaiCaptureFileNames(fai, faiOverlays, sequenceName, szSharedOriginPath, ts, out captureName, out originName);

            if (originName != null && saver != null && sharedSrc != null) {
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
            string measName = GetMeasurementDisplayName(meas);
            string datumRef = meas.DatumRef;
            datumRef = datumRef ?? "";
            Logging.PrintLog((int)ELogType.Error, LOG_TAG + "Measurement '" + measName + "' skipped — datum '" + datumRef + "' 실패 (" + meas.LastSkipReason + ")");
        }

        //260716 hbk DatumRef 가 실존하지 않는 datum 이름을 가리킬 때 skip 처리(MarkMeasurementDatumSkipped 와 동일 규약).
        //  기존엔 이 케이스가 게이트를 우회해 identity(무보정)로 정상 측정처럼 PASS/NG 를 냈다 — 원인이 로그에도 안 남았음.
        private void MarkMeasurementDatumRefMissing(MeasurementBase meas) {
            meas.ClearResult();
            meas.LastSkipReason = SkipReason.DATUM_REF_MISSING;
            meas.LastJudgement = false; // skip 도 NG 강도(기존 규약 동일)
            string measName = GetMeasurementDisplayName(meas);
            string datumRef = meas.DatumRef;
            datumRef = datumRef ?? "";
            Logging.PrintLog((int)ELogType.Error, LOG_TAG + "Measurement '" + measName + "' skipped — DatumRef '" + datumRef + "' 에 해당하는 Datum 이 레시피에 없음 (오타/개명/삭제 확인 필요, " + meas.LastSkipReason + ")");
        }

        //260819 hbk quick-260819-sxj: IsZIndexMisconfigured / IsDatumZIndexMisconfigured 공용 로직 추출.
        //  두 호출부의 전제가 다르다 — EvaluateCrossZGate 는 호출 전 "둘 중 하나라도 설정됨"을
        //  이미 확인했고(둘 다 -1 인 채로 여기 들어올 수 없음), ProcessDatumDualImage 는 모든 Datum에
        //  대해 무조건 호출한다(둘 다 -1 인 일반 Datum이 훨씬 흔함). 그래서 bBothUnset 가드가
        //  반드시 있어야 하며, 이 가드가 있어도 EvaluateCrossZGate 쪽 동작은 바뀌지 않는다(그 경로는
        //  가드에 도달할 수 없는 입력만 들어오기 때문 — 도달 불가능이지 무관한 게 아니다).
        private bool IsCrossZIndexPairMisconfigured(int zIndexA, int zIndexB, InspectionSequence parentSeq)
        {
            bool bAUnset = zIndexA == UNSET_ZINDEX;
            bool bBUnset = zIndexB == UNSET_ZINDEX;
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
            bool bSameValue = zIndexA == zIndexB;
            if (bSameValue)
            {
                return true;
            }
            bool bAExists = parentSeq != null && parentSeq.DoesZIndexExistInRecipe(zIndexA);
            bool bBExists = parentSeq != null && parentSeq.DoesZIndexExistInRecipe(zIndexB);
            bool bBothExist = bAExists && bBExists;
            return !bBothExist;
        }

        //260722 hbk Phase 68 D-05: DualImage 측정의 ZIndexA/ZIndexB 오설정 판정 — 단일설정/동일값/존재하지 않는
        //  z_index 참조 → true. 호출부가 이미 "둘 중 하나라도 설정됨"을 확인한 뒤에만 호출한다(둘 다 -1 미설정인
        //  기존 레시피는 이 검사 자체를 타지 않음 — D-07 회귀 0). 조용한 폴백(ResolveDatumModelPath 의 Shots[0] 류) 금지.
        //260819 hbk quick-260819-sxj: 본문을 IsCrossZIndexPairMisconfigured 로 위임(로직 무변경, 시그니처 무변경).
        private bool IsZIndexMisconfigured(DualImageEdgeDistanceMeasurement dualMeas, InspectionSequence parentSeq2)
        {
            return IsCrossZIndexPairMisconfigured(dualMeas.ZIndexA, dualMeas.ZIndexB, parentSeq2);
        }

        //260722 hbk Phase 68 D-05: MarkMeasurementDatumRefMissing 미러 — 크로스-Z 오설정 명시적 NG(조용한 폴백 금지).
        private void MarkMeasurementZIndexMisconfigured(MeasurementBase meas)
        {
            meas.ClearResult();
            meas.LastSkipReason = SkipReason.ZINDEX_MISCONFIGURED;
            meas.LastJudgement = false;
            string measName = GetMeasurementDisplayName(meas);
            int nZA = UNSET_ZINDEX;
            int nZB = UNSET_ZINDEX;
            var dualMeas = meas as DualImageEdgeDistanceMeasurement;
            if (dualMeas != null)
            {
                nZA = dualMeas.ZIndexA;
                nZB = dualMeas.ZIndexB;
            }
            Logging.PrintLog((int)ELogType.Error, LOG_TAG + "Measurement '" + measName + "' skipped — ZIndexA=" + nZA + ", ZIndexB=" + nZB + " 크로스-Z 오설정(동일값/단일설정/존재하지 않는 index, " + meas.LastSkipReason + ")");
        }

        // 수동(RUN/반복/일괄) 검사에서 크로스-Z 짝이 절대 완성될 수 없는 상황의 처리 — 예전엔
        //  그냥 넘어가서 "한 번도 측정 안 한 항목"이 합격으로 보고되는 안전 문제가 있었다.
        //  지금은 명시적으로 불합격 처리한다.
        // 로그 문구만 상황에 따라 다르게 낸다 — 수동 검사는 기존 안내 문구 그대로, 자동(PLC) 검사
        //  중간 상태는 "고장이 아니라 정상 대기"라는 걸 알 수 있게 별도 문구를 쓴다. 판정 자체는
        //  둘 다 동일하다.
        private void MarkMeasurementCrossZIncomplete(MeasurementBase meas, bool bRelevantTick, bool bProtocolCycle, InspectionSequence parentSeq2)
        {
            meas.ClearResult();
            meas.LastSkipReason = SkipReason.CROSS_Z_INCOMPLETE;
            meas.LastJudgement = false;
            string measName = GetMeasurementDisplayName(meas);
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
                int nCurZ;
                if (parentSeq2 != null) nCurZ = parentSeq2.GetExecutionZIndex();
                else nCurZ = UNSET_ZINDEX;
                Logging.PrintLog((int)ELogType.Error, LOG_TAG + "Measurement '" + measName + "' CROSS_Z_INCOMPLETE — ZIndexA=" + nZA + ", ZIndexB=" + nZB + ", CurrentZ=" + nCurZ + ": 프로토콜 사이클 정상 흐름의 중간 상태(고장 아님) — 짝이 되는 나머지 z 트리거 대기 중, 아직 측정되지 않음(PASS 아님) (" + meas.LastSkipReason + ")");
                return;
            }
            string szCase;
            if (bRelevantTick) szCase = "한쪽 z 이미지만 확보, 나머지 tick 없음";
            else szCase = "z=0 이 ZIndexA/B 어느 쪽도 아님, 캡처 없음";
            Logging.PrintLog((int)ELogType.Error, LOG_TAG + "Measurement '" + measName + "' 미측정 NG — 비프로토콜 실행(RUN 버튼/일괄검사)은 z_index 가 항상 0 이라 ZIndexA=" + nZA + ", ZIndexB=" + nZB + " 크로스-Z 짝을 완성할 수 없음(" + szCase + "). 실제로 측정하려면 PLC/$TEST 또는 수동 Z 트리거로 두 z 위치를 모두 실행할 것 (" + meas.LastSkipReason + ")");
        }

        // 기준점의 A/B 짝 설정 오류 판정 — 하나만 설정됐거나, 같은 값이거나, 존재하지 않는 값을
        //  가리키면 오류다. 단 둘 다 "설정 안 함"(-1)인 경우는 정상이니 먼저 걸러내야 한다 —
        //  안 그러면 -1 과 -1 이 같은 값이라고 오판정한다.
        //260819 hbk quick-260819-sxj: 본문을 IsCrossZIndexPairMisconfigured 로 위임(로직 무변경, 시그니처 무변경).
        private bool IsDatumZIndexMisconfigured(DatumConfig datum, InspectionSequence parentSeq)
        {
            return IsCrossZIndexPairMisconfigured(datum.ZIndexA, datum.ZIndexB, parentSeq);
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
                    SafeDisposeImage(imgA);
                    SafeDisposeImage(imgB);
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

        // 시뮬레이션 모드에서는 Shot 이 사진 한 장만 고정으로 갖고 있어서, 크로스-Z 의 두 촬영(A/B)이
        //  항상 같은 사진이 되어 거리 계산 자체가 성립하지 않았다(실측 확인됨) — 그래서 시뮬레이션
        //  으로는 크로스-Z 를 검증할 방법이 없었다. 이제 A/B 각각 정해둔 교시 이미지가 있으면
        //  그걸 대신 쓴다(경로 안 정해놨으면 기존처럼 실기 촬영으로 폴백, 회귀 없음).
        // 처음엔 시뮬레이션 모드에서만 이 대체를 켰는데, 그러면 실기 빌드에서는 여전히 A/B 가
        //  같은 사진이 되는 버그가 남아있었다(실기에서 재현된 실패 사례 있음) — 지금은 빌드
        //  종류와 무관하게 항상 같은 규칙(경로가 정해져 있으면 그 파일 사용)으로 동작한다.
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

        //260819 hbk quick-260819-sgg: ProcessCrossZCaptureTick 의 4개 out 파라미터를 이름 있는 필드로 교체 —
        //  파일 상단 ShotMeasureAccumulator 와 동일한 필드(프로퍼티 아님)+K&R 스타일을 따른다.
        private class CrossZCaptureTickResult {
            public bool Relevant;
            public bool CaptureOk;
            public bool Completed;
            public string CapturedRoleKey;
        }

        // 크로스-Z 촬영 한 번(tick)을 처리한다 — 이 측정과 무관한지 / 촬영에 실패했는지 / A·B 가
        //  다 모였는지 세 가지로 판정한다. 새로 촬영하지 않고 이미 찍어둔 사진을 재사용하는 게
        //  기본이다(교시 경로가 지정돼 있으면 그 파일을 대신 읽는다).
        // 이번에 실제로 캡처된 이미지가 어느 쪽(A/B)인지 호출부에 알려준다 — 화면/저장이 항상
        //  고정 이미지만 보여주던 문제를 막기 위해, 호출부가 이 정보로 실제 측정 이미지를 표시/저장한다.
        private CrossZCaptureTickResult ProcessCrossZCaptureTick(DualImageEdgeDistanceMeasurement dualMeas, InspectionSequence parentSeq2)
        {
            CrossZCaptureTickResult result = new CrossZCaptureTickResult();
            if (parentSeq2 == null || ShotParam == null)
            {
                return result;
            }
            int nCurZ = parentSeq2.GetExecutionZIndex();
            bool bIsRoleA = nCurZ == dualMeas.ZIndexA;
            bool bIsRoleB = nCurZ == dualMeas.ZIndexB;
            result.Relevant = bIsRoleA || bIsRoleB;
            if (!result.Relevant)
            {
                return result; // 이 tick 은 이 측정의 ZIndexA/B 어느 쪽도 아님 — 상태변화 없음(안전망)
            }
            string baseKey = BuildCrossZMeasurementKey(dualMeas);
            string roleKey;
            if (bIsRoleA) roleKey = baseKey + CROSS_Z_ROLE_SUFFIX_A;
            else roleKey = baseKey + CROSS_Z_ROLE_SUFFIX_B;
            using (HImage capturedImage = LoadCrossZRoleImage(bIsRoleA, dualMeas))
            {
                if (capturedImage == null)
                {
                    return result; // 캡처 실패 — 호출부가 NG 처리
                }
                parentSeq2.StoreCrossZImage(roleKey, capturedImage);
                result.CaptureOk = true;
                result.CapturedRoleKey = roleKey;
            }
            string keyA = baseKey + CROSS_Z_ROLE_SUFFIX_A;
            string keyB = baseKey + CROSS_Z_ROLE_SUFFIX_B;
            result.Completed = parentSeq2.HasCrossZImage(keyA) && parentSeq2.HasCrossZImage(keyB);
            return result;
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
                SafeDisposeImage(imgA);
                SafeDisposeImage(imgB);
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
            shotName = shotName ?? "";
            Logging.PrintLog((int)ELogType.Error, LOG_TAG + "SHOT '" + shotName + "' 검사 이미지 없음 — 모든 measurement NG 처리 (캐스케이드 차단)");
        }
    }
}
