using System;
using System.Collections.Generic;
using System.Windows;
using HalconDotNet;
using PropertyTools.DataAnnotations;
using ReringProject.Define;
using ReringProject.Device;
using ReringProject.Halcon.Algorithms;
using ReringProject.Network;
using ReringProject.Setting;
using ReringProject.UI;
using ReringProject.Utility;

namespace ReringProject.Sequence {

    public class InspectionSequenceContext : SequenceContext {
        public EVisionResultType ResultInfo { get; set; }

        public InspectionSequenceContext(InspectionSequence source) : base(source) { }

        public override void Clear() {
            ResultInfo = EVisionResultType.NG;
            base.Clear();
        }

        public override void CopyFrom(ActionContext actionContext) {
            base.CopyFrom(actionContext);
            Result = actionContext.Result;
            if (actionContext is FAIMeasurementContext faiContext) {
                // 각 Action의 AllPass가 false면 NG
                if (!faiContext.AllPass) {
                    ResultInfo = EVisionResultType.NG;
                }
            }
        }
    }

    public class InspectionSequence : SequenceBase {
        private readonly DeviceHandler pDevs;
        private readonly InspectionSequenceContext pMyContext;
        private readonly CameraMasterParam pMyParam;
        private readonly string DefaultCamera;
        private readonly string DefaultLight;

        // Fixture DisplayName — 사용자 편집 가능
        public string DisplayName { get; set; } = "";

        //260619 hbk Phase 57 #6 leveling 제거 — LevelingEnabled 토글 폐기 (ALIGN 대체, D-12/D-13)

        // Multi-Datum — Fixture 레벨 Datum 소유
        [PropertyTools.DataAnnotations.Browsable(false)]
        public List<DatumConfig> DatumConfigs { get; private set; } = new List<DatumConfig>();

        // 런타임 transform 캐시
        private readonly Dictionary<string, HTuple> _datumTransforms = new Dictionary<string, HTuple>();

        // datum 검출 실패 datum 이름 집합 (per-FAI gate 신호). _datumTransforms 와 동일 lifecycle.
        private readonly HashSet<string> _failedDatums = new HashSet<string>();

        //260722 hbk Phase 68 D-02/D-02a: 크로스-Z 측정 이미지 저장소 — 계산값이 아니라 캡처 이미지(HImage) 보관.
        //  키 = Action_FAIMeasurement.BuildCrossZMeasurementKey(Shot명+측정식별자)+역할(A/B) 접미사. 소유 클론 계약
        //  (ShotConfig._image/SetImage/GetImage 패턴 미러) — Store 는 CopyImage 저장, Take 는 CopyImage 반환(호출부
        //  소유, finally Dispose 책임). z_index=0 수신 시 ResetCycleState() 에서 Dispose+Clear(D-03, 누수 0).
        private readonly object _crossZImageLock = new object();
        private readonly Dictionary<string, HImage> m_dicCrossZImages = new Dictionary<string, HImage>();

        //260618 hbk Phase 54 ALIGN-01 패턴매칭(align) 실패 datum set — 검출 실패(_failedDatums)와 구분하여 측정 게이트가 LastSkipReason=ALIGN_FAIL 표기 (D-10).
        private readonly HashSet<string> _alignFailedDatums = new HashSet<string>();

        //260623 hbk Phase 49 PROTO-03/05 (D-02): 멀티샷 사이클 누적 상태 — 신규 클래스 미도입, _failedDatums 와 동일 lifecycle 멤버.
        //  ClearDatumTransforms() 와 별도로 Index 0 수신 시 ResetCycleState() 로 리셋(D-08).
        //  49-02 가 AddResponseV1Cycle 에서 read 연결 → CS0414(미사용) 해소, #pragma 제거함.
        private const int DATUM_Z_INDEX = 0;         //260623 hbk Phase 49 (D-08): Index 0 = Datum 샷 (매직넘버 상수화, D-10)
        private const int CROSS_Z_UNSET = -1;        //260722 hbk Phase 68 D-09: ZIndexA/B 미설정 sentinel (매직넘버 상수화)
        private bool m_bCycleHasNG = false;          // 사이클 중 NG 1건이라도 발견 → 마지막 Index 종합 F (D-02)
        private bool m_bCycleDatumFailed = false;    // Index 0 Datum 검출 실패 → 즉시 F 마킹 (D-04/D-05)
        //260722 hbk Phase 68 GAP-3(68-10, 지침 #6): 한 사이클에 즉시-F 가 최대 1회만 나가도록 하는 latch.
        //  z=0 즉시-F 분기(BuildDatumShotResponse)와 완성 index 재평가(TryApplyCrossZDatumImmediateFail) 양쪽
        //  모두 세팅 — 어느 한쪽만 세팅하면 한 사이클에 F 가 2번 나가는 중복-F blocking 회귀 발생(T-68-11).
        //  ResetCycleState(z=0 리셋)에서 초기화되어 다음 부품으로 누수되지 않는다(T-68-13).
        private bool m_bImmediateFailSent = false;
        private int m_nCurrentZIndex = 0;            // 이번 $TEST z_index (RequestPacket.TestID 파싱 결과)
        private int m_nLastZIndex = 0;               // 레시피 Shot z_index 최댓값 = 마지막 Index (D-03, ComputeLastZIndex 산출)

        //260619 hbk Phase 57 #6 leveling 제거 — 레벨링 각도 캐시 멤버/메서드 폐기 (ALIGN 대체, D-12/D-13)

        public InspectionSequence(ESequence seqID, string name, int algIndex, string defaultCamera, string defaultLight) : base(seqID, name) {
            pDevs = SystemHandler.Handle.Devices;
            Context = new InspectionSequenceContext(this);
            pMyContext = Context as InspectionSequenceContext;
            Param = new InspectionMasterParam(this);
            pMyParam = Param as CameraMasterParam;
            DefaultCamera = defaultCamera;
            DefaultLight = defaultLight;
            // 수동(UI) 검사 완료 시에도 cycle.json 영속화.
            //  AddResponse() 는 RequestPacket(TCP/host) 경로에서만 호출 → Start(EAction) 수동 검사(RequestPacket=null)는 미저장이었음.
            //  OnFinish 는 트리거 무관 발화 → 핸들러에서 RequestPacket==null(수동) 일 때만 저장(TCP 경로 중복 방지).
            OnFinish += HandleManualCyclePersist;
            // 런 시작 시 이 시퀀스 shot 의 모든 측정 결과 초기화 (안 돈 측정 stale 방지).
            //  OnStart 는 단일 Start(int) + StartAll 모두에서 발화 → 단일/전체 모두 런 시작에 일괄 초기화.
            OnStart += HandleRunStartResetResults;
        }

        // 종합 판정 + FAI별 결과 TCP 전송. 3-state cycle hierarchy + FAIResults P/F/N 분기.
        //  계층: 검출실패(NotExist 'N') > NG ('X') > OK ('O'). datum-skip 1건이라도 있으면 cycle = NotExist.
        //  TestResultPacket 신규 필드 추가 안 함 — FAIResults[i] 표현 레벨에서 끝남, wire 호환 100%.
        //  EVisionResultType.NotExist (기존 v2.6 enum) 재사용.
        protected override void AddResponse() {
            if (RequestPacket == null) return;

            //260623 hbk Phase 49 PROTO-03/04/05: v1.0 활성 시에만 z_index 사이클 엔진 적용. v2.6 면 기존 전체-Shot 경로(아래)로 폴백(회귀 0).
            bool bUseV1Cycle = SystemHandler.Handle.Setting.UseProtocolV1;
            if (bUseV1Cycle)
            {
                AddResponseV1Cycle();
                return;
            }
            // ↓ 이하 기존 v2.6 전체-Shot 집계 블록 그대로 (회귀 0) — 무변경 보존.

            var recipeManager = SystemHandler.Handle.Sequences.RecipeManager;

            // 종합 판정: 3-state hierarchy
            bool anyDatumSkip = false; // fai.WasDatumSkipped 1건이라도 있으면 true → cycle=NotExist
            bool allPass = true;
            var responsePacket = new TestResultPacket {
                Target = RequestPacket.Sender,
                Site = RequestPacket.Site,
                InspectionType = RequestPacket.TestType,
                IsDynamicFAI = true,
                Type = RequestPacket.Type,   //260624 hbk Phase 63 PROTO-Type: 수신 Type echo
            };

            foreach (var shot in recipeManager.Shots) {
                foreach (var fai in shot.FAIList) {
                    // FAIResults[i] 3-state (P/F/N) 분기. datum-skip 시 fai.WasDatumSkipped=true + IsPass=false 동시 설정.
                    //  계층 우선순위: WasDatumSkipped → NotExist, !IsPass → NG, 그 외 → OK.
                    EVisionResultType faiResultCode;
                    if (fai.WasDatumSkipped)
                    {
                        faiResultCode = EVisionResultType.NotExist; // 'N'
                        anyDatumSkip = true; // cycle aggregation
                    }
                    else if (!fai.IsPass)
                    {
                        faiResultCode = EVisionResultType.NG; // 'F'
                        allPass = false;
                    }
                    else
                    {
                        faiResultCode = EVisionResultType.OK; // 'P'
                    }
                    string faiNameArg = fai.FAIName;
                    if (faiNameArg == null) faiNameArg = "FAI";
                    responsePacket.FAIResults.Add(new FAIResultData(
                        faiNameArg,
                        faiResultCode,
                        fai.MeasuredValue
                    ));
                }
            }

            // cycle 계층 판정: 검출실패 > NG > OK.
            if (anyDatumSkip) responsePacket.Result = EVisionResultType.NotExist; // 'N' (TestResultPacket.GetResultString 가 자동 매핑)
            else if (!allPass) responsePacket.Result = EVisionResultType.NG; // 'X'
            else responsePacket.Result = EVisionResultType.OK; // 'O'
            pMyContext.ResultInfo = responsePacket.Result;

            // cycle 완료 → 구조화 JSON 영속화 (리뷰어/xlsx 공통 토대).
            //  BuildDto 는 동기 스냅샷, SaveAsync 가 비동기 파일 쓰기 — AddResponse 스레드 블로킹 최소화.
            //  직렬화 예외가 TCP 응답(ResponseQueue.Enqueue)을 차단하지 않도록 try/catch 격리.
            try
            {
                //260622 hbk Phase 48 PROTO-01: 자재번호 추출 (RequestPacket = 이번 TCP TEST 패킷). null 이면 -1 폴백.
                int nIndexNumber = -1;
                bool bHasRequest = RequestPacket != null;
                if (bHasRequest)
                {
                    nIndexNumber = RequestPacket.IndexNumber;
                }
                var cycleDto = CycleResultSerializer.BuildDto(
                    recipeManager,
                    responsePacket.Result,
                    System.DateTime.Now,
                    SystemHandler.Handle.Setting.CurrentRecipeName,
                    Name,           // 이 시퀀스 소유 shot 만 cycle 에 포함
                    nIndexNumber);  //260622 hbk Phase 48 PROTO-01: 자재번호 전파
                CycleResultSerializer.SaveAsync(cycleDto);
            }
            catch (Exception ex)
            {
                try { Logging.PrintErrLog((int)ELogType.Error, "[Phase40] cycle 직렬화 실패(무시): " + ex.Message); } catch { }
            }

            ResponseQueue.Enqueue(responsePacket);
        }

        // 수동(UI) 검사 완료 핸들러 (OnFinish 구독, 생성자에서 1회 등록).
        //  TCP 경로(RequestPacket!=null)는 AddResponse 에서 이미 저장하므로 여기선 수동 경로만 처리 → 중복 저장 방지.
        //  종합판정은 ComputeOverallResult (AddResponse 의 anyDatumSkip>NG>OK 계층과 동일) 로 재집계 후 BuildDto/SaveAsync.
        private void HandleManualCyclePersist(SequenceContext context) {
            if (RequestPacket != null) return; // TCP 경로는 AddResponse 에서 저장됨
            try
            {
                var recipeManager = SystemHandler.Handle.Sequences.RecipeManager;
                EVisionResultType result = ComputeOverallResult(recipeManager);
                var cycleDto = CycleResultSerializer.BuildDto(
                    recipeManager,
                    result,
                    System.DateTime.Now,
                    SystemHandler.Handle.Setting.CurrentRecipeName,
                    Name); // 이 시퀀스 소유 shot 만 cycle 에 포함
                CycleResultSerializer.SaveAsync(cycleDto);
            }
            catch (Exception ex)
            {
                try { Logging.PrintErrLog((int)ELogType.Error, "[Phase40] 수동 cycle 직렬화 실패(무시): " + ex.Message); } catch { }
            }
        }

        // 런 시작 시 이 시퀀스 소유 shot 의 모든 측정 결과를 초기화한다.
        //  배경: 기존 per-shot EStep.Init 의 ShotParam.ClearAllResults() 는 '실행되는 shot' 만 초기화 + FAIConfig.ClearResult() 가
        //        하위 Measurement 의 LastHasResult 를 안 지운다 → 단일 shot 실행 시 나머지 shot 의 측정이 이전 런 잔여값(stale)을 유지,
        //        cycle.json/트리/리뷰어에 '안 돈 측정'이 OK/NG 로 찍히던 문제.
        //  조치: 런 시작에 이 시퀀스의 Actions(=이 시퀀스 shot)의 모든 Measurement 를 ClearResult → 안 돈 측정은 미측정('—')으로 표시.
        //  범위: 이 시퀀스 shot 만 (Actions 순회) → Top/Bottom/Side 병렬 실행 간섭 없음. 실행되는 shot 은 Measure 루프가 다시 채운다.
        private void HandleRunStartResetResults(SequenceContext context) {
            try {
                if (Actions == null) return;
                foreach (var act in Actions) {
                    var faiAct = act as Action_FAIMeasurement;
                    if (faiAct == null) continue;
                    var shot = faiAct.ShotParam;
                    if (shot == null) continue;
                    foreach (var fai in shot.FAIList) {
                        if (fai == null) continue;
                        if (fai.Measurements != null) {
                            foreach (var m in fai.Measurements) {
                                if (m != null) m.ClearResult(); // LastHasResult=false → 리뷰어 '—'
                            }
                        }
                        fai.ClearResult();
                        if (fai.LastOverlays != null) fai.LastOverlays.Clear();
                    }
                }
            } catch (Exception ex) {
                try { Logging.PrintErrLog((int)ELogType.Error, "[Phase40] run-start 결과 초기화 실패(무시): " + ex.Message); } catch { }
            }
        }

        // 종합판정 집계 (read-only, 패킷 미생성). AddResponse 의 3-state 계층과 동일 우선순위.
        private EVisionResultType ComputeOverallResult(InspectionRecipeManager recipeManager) {
            bool anyDatumSkip = false;
            bool allPass = true;
            if (recipeManager != null)
            {
                foreach (var shot in recipeManager.Shots)
                {
                    foreach (var fai in shot.FAIList)
                    {
                        if (fai.WasDatumSkipped) anyDatumSkip = true;
                        else if (!fai.IsPass) allPass = false;
                    }
                }
            }
            if (anyDatumSkip) return EVisionResultType.NotExist; // 검출실패 최우선
            if (!allPass) return EVisionResultType.NG;
            return EVisionResultType.OK;
        }

        //260623 hbk Phase 49 PROTO-03 (D-03): 레시피 Shot 들의 z_index 최댓값 = "마지막 Index".
        //  별도 설정/플래그 불필요 — 레시피 단일 진실원. PLC Index Table = 레시피 Shot 구성 일치 전제(D-03 코드 주석 고정).
        //  이 시퀀스 소유 Shot 만 대상(Name == OwnerSequenceName) — Top/Bottom/Side 병렬 간섭 차단.
        //260722 hbk Phase 68 CROSS-2(68-GAP-ANALYSIS.md 교차이슈): 소유 shot.ZIndex 최댓값만으로는 크로스-Z 완성
        //  index(GetMeasurementCompletionZIndex/GetDatumCompletionZIndex)가 그 최댓값을 넘는 경우를 놓친다 — 넘으면
        //  "마지막 Index"를 조기 오판정해 한 사이클에 P/F 가 2회 송신될 위험(T-68-10). 소유 shot.ZIndex 최댓값 산출 뒤
        //  MaxCrossZCompletionZIndex 로 크로스-Z 완성 index 까지 반영해 최종 max 를 반환한다. 크로스-Z 미설정 레시피는
        //  MaxCrossZCompletionZIndex==0(또는 이하) → 기존 max(shot.ZIndex)와 동치(D-07 회귀 0).
        private int ComputeLastZIndex(InspectionRecipeManager recipeManager)
        {
            int nMax = 0;
            bool bHasManager = recipeManager != null;
            if (!bHasManager)
            {
                return nMax;
            }
            foreach (var shot in recipeManager.Shots)
            {
                bool bIsNull = shot == null;
                if (bIsNull)
                {
                    continue;
                }
                bool bOwnedByThisSeq = shot.OwnerSequenceName == Name;
                if (!bOwnedByThisSeq)
                {
                    continue;
                }
                bool bIsLarger = shot.ZIndex > nMax;
                if (bIsLarger)
                {
                    nMax = shot.ZIndex;
                }
            }
            nMax = System.Math.Max(nMax, MaxCrossZCompletionZIndex(recipeManager));
            return nMax;
        }

        //260722 hbk Phase 68 CROSS-2 (D-09): ComputeLastZIndex 의 sub-헬퍼(함수 30줄 가드) — 이 시퀀스 소유 Shot 의
        //  크로스-Z 측정 완성 index(MaxShotCrossZMeasurementCompletionZIndex) 최댓값과, 이 시퀀스 DatumConfigs 의
        //  크로스-Z Datum 완성 index(GetDatumCompletionZIndex) 최댓값 중 더 큰 값을 반환한다. 크로스-Z 가 전혀 없으면
        //  두 최댓값 모두 0 → 반환 0(D-07: ComputeLastZIndex 의 Math.Max(nMax, 0) 이 기존값을 그대로 보존).
        private int MaxCrossZCompletionZIndex(InspectionRecipeManager recipeManager)
        {
            int nMax = 0;
            bool bHasManager = recipeManager != null;
            if (bHasManager)
            {
                foreach (var shot in recipeManager.Shots)
                {
                    nMax = System.Math.Max(nMax, MaxShotCrossZMeasurementCompletionZIndex(shot));
                }
            }
            foreach (var datum in DatumConfigs)
            {
                int nDatumCompletion = GetDatumCompletionZIndex(datum);
                bool bDatumHasCompletion = nDatumCompletion != CROSS_Z_UNSET;
                if (bDatumHasCompletion)
                {
                    nMax = System.Math.Max(nMax, nDatumCompletion);
                }
            }
            return nMax;
        }

        //260722 hbk Phase 68 CROSS-2 (D-09): MaxCrossZCompletionZIndex 의 sub-헬퍼(함수 30줄 가드) — 이 시퀀스
        //  소유(OwnerSequenceName==Name) shot 이 소유한 DualImageEdgeDistanceMeasurement 중 크로스-Z(ZIndexA/B 둘 다
        //  설정)인 것들의 완성 index(GetMeasurementCompletionZIndex, ShotHasCrossZMeasurementCompletingAt 와 동일
        //  bIsCrossZ 게이트) 최댓값. 미소유 shot/비-크로스-Z 측정은 0 기여(D-07 회귀 가드).
        private int MaxShotCrossZMeasurementCompletionZIndex(ShotConfig shot)
        {
            int nMax = 0;
            bool bShotNull = shot == null;
            if (bShotNull)
            {
                return nMax;
            }
            bool bOwnedByThisSeq = shot.OwnerSequenceName == Name;
            if (!bOwnedByThisSeq)
            {
                return nMax;
            }
            foreach (var fai in shot.FAIList)
            {
                bool bFaiNull = fai == null;
                if (bFaiNull)
                {
                    continue;
                }
                foreach (var meas in fai.Measurements)
                {
                    var dualMeas = meas as DualImageEdgeDistanceMeasurement;
                    bool bIsCrossZ = dualMeas != null && dualMeas.ZIndexA != CROSS_Z_UNSET && dualMeas.ZIndexB != CROSS_Z_UNSET;
                    if (!bIsCrossZ)
                    {
                        continue;
                    }
                    int nCompletion = GetMeasurementCompletionZIndex(meas, shot);
                    nMax = System.Math.Max(nMax, nCompletion);
                }
            }
            return nMax;
        }

        //260625 hbk Phase 64 LIGHT-01 (D-12): z_index + OwnerSequenceName 기반 ShotConfig 조회.
        //  이 시퀀스 소유 Shot 중 첫 번째 매칭을 반환. 없으면 null.
        //  ComputeLastZIndex 의 순회 패턴 동일 — foreach + OwnerSequenceName + ZIndex 이중 조건.
        private ShotConfig FindShotByZIndex(int nZIndex)
        {
            var recipeManager = SystemHandler.Handle.Sequences.RecipeManager;
            bool bHasManager = recipeManager != null;
            if (!bHasManager)
            {
                return null;
            }
            foreach (var shot in recipeManager.Shots)
            {
                bool bIsNull = shot == null;
                if (bIsNull)
                {
                    continue;
                }
                bool bOwnedByThisSeq = shot.OwnerSequenceName == Name;
                bool bZMatch = shot.ZIndex == nZIndex;
                bool bInScope = bOwnedByThisSeq && bZMatch;
                if (bInScope)
                {
                    return shot;
                }
            }
            return null;
        }

        //260722 hbk Phase 68 D-01/D-05: shot이 소유한 FAIList의 DualImageEdgeDistanceMeasurement 중 하나라도
        //  ZIndexA 또는 ZIndexB == nZIndex 이면 true. FindActionIndicesByZIndex 의 크로스-Z 매칭 sub-헬퍼(D-09: 함수 분리).
        //  -1(미설정) sentinel은 어떤 실제 z_index(실행 시 항상 >=1)와도 매칭되지 않아 자연 배제됨 — 별도 가드 불필요.
        private bool DoesShotOwnCrossZIndex(ShotConfig shot, int nZIndex)
        {
            bool bHasShot = shot != null;
            if (!bHasShot)
            {
                return false;
            }
            foreach (var fai in shot.FAIList)
            {
                bool bFaiNull = fai == null;
                if (bFaiNull)
                {
                    continue;
                }
                foreach (var meas in fai.Measurements)
                {
                    var dualMeas = meas as DualImageEdgeDistanceMeasurement;
                    bool bIsDualImage = dualMeas != null;
                    if (!bIsDualImage)
                    {
                        continue;
                    }
                    bool bMatchA = dualMeas.ZIndexA == nZIndex;
                    bool bMatchB = dualMeas.ZIndexB == nZIndex;
                    bool bCrossZMatch = bMatchA || bMatchB;
                    if (bCrossZMatch)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        //260722 hbk Phase 68 D-01/D-01b: z_index → Actions[] 인덱스 다중매칭 헬퍼 (ShotConfig.ZIndex + owned ZIndexA/ZIndexB aware).
        //  사용자 확정 설계: 크로스-Z 측정의 owning Shot은 별도 Shot으로 쪼개지 않고, own-ZIndex 위치뿐 아니라 자신이 소유한
        //  DualImageEdgeDistanceMeasurement의 ZIndexA/ZIndexB 위치에서도 실행되어야 한다 — 그래서 한 Shot이 여러 z_index에
        //  다중 매칭될 수 있다(반환 List<int>). FindShotByZIndex(단일매칭, ShotConfig 반환)와 달리 Actions[] 인덱스를 반환해야
        //  SequenceBase.StartSubset(int[], TestPacket)에 그대로 전달 가능하다.
        //  public: Custom/SystemHandler.ProcessTest(Task 3, 다른 클래스)가 직접 호출해야 하므로 ApplyShotLights/TurnOffShotLights와
        //  동일하게 public 노출(같은 파일의 기존 cross-class 호출 컨벤션 — private로는 다른 클래스에서 호출 불가, 컴파일 불가).
        public List<int> FindActionIndicesByZIndex(int nZIndex)
        {
            var matchedIndices = new List<int>();
            bool bHasActions = Actions != null;
            if (!bHasActions)
            {
                return matchedIndices;
            }
            for (int i = 0; i < Actions.Length; i++)
            {
                var faiAct = Actions[i] as Action_FAIMeasurement;
                bool bIsFaiAction = faiAct != null;
                if (!bIsFaiAction)
                {
                    continue;
                }
                ShotConfig shot = faiAct.ShotParam;
                bool bHasShot = shot != null;
                if (!bHasShot)
                {
                    continue;
                }
                bool bOwnedByThisSeq = shot.OwnerSequenceName == Name;
                if (!bOwnedByThisSeq)
                {
                    continue;
                }
                bool bOwnZIndexMatch = shot.ZIndex == nZIndex;
                bool bCrossZMatch = DoesShotOwnCrossZIndex(shot, nZIndex);
                bool bMatch = bOwnZIndexMatch || bCrossZMatch;
                if (bMatch)
                {
                    matchedIndices.Add(i);
                }
            }
            return matchedIndices;
        }

        //260625 hbk Phase 64 LIGHT-01 (D-10/D-12): $PREP 수신 → z_index Shot 조회 → 조명 세팅.
        //  ProcessPrep() 이 호출하는 public 진입점. Shot 없으면 false.
        //  CoaxLight_* 는 ALIGN_COAX 그룹으로 매핑 (D-11: INI 키 이름 보존, 그룹명만 변경).
        public bool ApplyShotLights(int nZIndex)
        {
            ShotConfig shot = FindShotByZIndex(nZIndex);
            bool bHasShot = shot != null;
            if (!bHasShot)
            {
                Logging.PrintLog((int)ELogType.LightController,
                    "[PREP] Shot not found for ZIndex={0}, Seq={1} //260625 hbk Phase 64", nZIndex, Name);
                return false;
            }
            ApplyShotLightsInternal(shot);
            return true;
        }

        // 티칭 Grab 등 $PREP/z_index 프로토콜 밖에서 이미 ShotConfig 객체를 들고 있을 때 쓰는 진입점.
        //  ApplyShotLights(int) 처럼 FindShotByZIndex 조회를 거치지 않고 바로 적용 — ApplyDatumLights(DatumConfig) 와 동일 패턴.
        public void ApplyShotLightsDirect(ShotConfig shot)
        {
            if (shot == null) return;
            ApplyShotLightsInternal(shot);
        }

        //260626 hbk v3.0: $PREP Op==0(사이클 종료) → 전 조명 그룹 소등. ApplyShotLightsInternal 의 4그룹 OFF.
        //  $LIGHT OFF 명령 폐기 대체. HW 트리거 전환 시에도 $PREP 가 OFF 담당.
        public void TurnOffShotLights()
        {
            LightHandler.Handle.SetOnOff(LightHandler.LIGHT_RING, false);
            LightHandler.Handle.SetOnOff(LightHandler.LIGHT_BACK, false);
            LightHandler.Handle.SetOnOff(LightHandler.LIGHT_ALIGN_COAX, false);
            LightHandler.Handle.SetOnOff(LightHandler.LIGHT_BAR, false);
            LightHandler.Handle.SetOnOff(LightHandler.LIGHT_RING7, false);   //260626 hbk Phase 66: Ring7 소등 정합 — 점등(ApplyShotLightsInternal)/소등 대칭
        }

        //260625 hbk Phase 64 LIGHT-01 (D-10): ShotConfig 조명 → LightHandler 적용.
        //  Ring → RING_CH1~6 채널별 개별 적용 / Bar → BAR_1~4 채널별 개별 적용 (quick-260713-nse: 그룹→채널 개별 전환)
        //  Back → BACK 그룹 / Coax → ALIGN_COAX 그룹 / Ring7 → RING7 그룹 (1채널이라 그룹 API 그대로 유지)
        //  Enabled=true: SetOnOff(true) 먼저, 이후 SetLevel (ApplyLight 순서 동일).
        //  Enabled=false: SetOnOff(false) 만 호출.
        private void ApplyShotLightsInternal(ShotConfig shot)
        {
            ApplyChannelLight(LightHandler.LIGHT_RING_CH1, shot.RingLight_Enabled_1, shot.RingLight_Brightness_1);
            ApplyChannelLight(LightHandler.LIGHT_RING_CH2, shot.RingLight_Enabled_2, shot.RingLight_Brightness_2);
            ApplyChannelLight(LightHandler.LIGHT_RING_CH3, shot.RingLight_Enabled_3, shot.RingLight_Brightness_3);
            ApplyChannelLight(LightHandler.LIGHT_RING_CH4, shot.RingLight_Enabled_4, shot.RingLight_Brightness_4);
            ApplyChannelLight(LightHandler.LIGHT_RING_CH5, shot.RingLight_Enabled_5, shot.RingLight_Brightness_5);
            ApplyChannelLight(LightHandler.LIGHT_RING_CH6, shot.RingLight_Enabled_6, shot.RingLight_Brightness_6);

            if (shot.BackLight_Enabled)
            {
                LightHandler.Handle.SetOnOff(LightHandler.LIGHT_BACK, true);
                LightHandler.Handle.SetLevel(LightHandler.LIGHT_BACK, shot.BackLight_Brightness);
            }
            else
            {
                LightHandler.Handle.SetOnOff(LightHandler.LIGHT_BACK, false);
            }

            if (shot.CoaxLight_Enabled)
            {
                LightHandler.Handle.SetOnOff(LightHandler.LIGHT_ALIGN_COAX, true);
                LightHandler.Handle.SetLevel(LightHandler.LIGHT_ALIGN_COAX, shot.CoaxLight_Brightness);
            }
            else
            {
                LightHandler.Handle.SetOnOff(LightHandler.LIGHT_ALIGN_COAX, false);
            }

            ApplyChannelLight(LightHandler.LIGHT_BAR_1, shot.SideLight_Enabled_1, shot.SideLight_Brightness_1);
            ApplyChannelLight(LightHandler.LIGHT_BAR_2, shot.SideLight_Enabled_2, shot.SideLight_Brightness_2);
            ApplyChannelLight(LightHandler.LIGHT_BAR_3, shot.SideLight_Enabled_3, shot.SideLight_Brightness_3);
            ApplyChannelLight(LightHandler.LIGHT_BAR_4, shot.SideLight_Enabled_4, shot.SideLight_Brightness_4);

            if (shot.Ring7Light_Enabled)   //260626 hbk Phase 66 D-02: Ring7Light → LIGHT_RING7 매핑 추가(검사 조명 정합)
            {
                LightHandler.Handle.SetOnOff(LightHandler.LIGHT_RING7, true);   //260626 hbk Ring7 ON
                LightHandler.Handle.SetLevel(LightHandler.LIGHT_RING7, shot.Ring7Light_Brightness);   //260626 hbk Ring7 밝기
            }
            else
            {
                LightHandler.Handle.SetOnOff(LightHandler.LIGHT_RING7, false);   //260626 hbk Ring7 OFF
            }
        }

        // Datum 전용 조명 적용 — Action_FAIMeasurement.EStep.DatumPhase 가 datum grab 직전 호출한다.
        //  ShotConfig 와 달리 Datum 은 $PREP/z_index 로 세팅되지 않으므로(Shot 소속 무관 순수 datum grab) 별도 진입점.
        public void ApplyDatumLights(DatumConfig datum)
        {
            if (datum == null) return;
            ApplyDatumLightsInternal(datum);
        }

        // ApplyShotLightsInternal 과 완전히 동일한 채널 매핑(Ring 6개별/Bar 4개별/Back·Coax·Ring7 그룹) — Datum 소스만 다르다.
        private void ApplyDatumLightsInternal(DatumConfig datum)
        {
            ApplyChannelLight(LightHandler.LIGHT_RING_CH1, datum.RingLight_Enabled_1, datum.RingLight_Brightness_1);
            ApplyChannelLight(LightHandler.LIGHT_RING_CH2, datum.RingLight_Enabled_2, datum.RingLight_Brightness_2);
            ApplyChannelLight(LightHandler.LIGHT_RING_CH3, datum.RingLight_Enabled_3, datum.RingLight_Brightness_3);
            ApplyChannelLight(LightHandler.LIGHT_RING_CH4, datum.RingLight_Enabled_4, datum.RingLight_Brightness_4);
            ApplyChannelLight(LightHandler.LIGHT_RING_CH5, datum.RingLight_Enabled_5, datum.RingLight_Brightness_5);
            ApplyChannelLight(LightHandler.LIGHT_RING_CH6, datum.RingLight_Enabled_6, datum.RingLight_Brightness_6);

            if (datum.BackLight_Enabled)
            {
                LightHandler.Handle.SetOnOff(LightHandler.LIGHT_BACK, true);
                LightHandler.Handle.SetLevel(LightHandler.LIGHT_BACK, datum.BackLight_Brightness);
            }
            else
            {
                LightHandler.Handle.SetOnOff(LightHandler.LIGHT_BACK, false);
            }

            if (datum.CoaxLight_Enabled)
            {
                LightHandler.Handle.SetOnOff(LightHandler.LIGHT_ALIGN_COAX, true);
                LightHandler.Handle.SetLevel(LightHandler.LIGHT_ALIGN_COAX, datum.CoaxLight_Brightness);
            }
            else
            {
                LightHandler.Handle.SetOnOff(LightHandler.LIGHT_ALIGN_COAX, false);
            }

            ApplyChannelLight(LightHandler.LIGHT_BAR_1, datum.SideLight_Enabled_1, datum.SideLight_Brightness_1);
            ApplyChannelLight(LightHandler.LIGHT_BAR_2, datum.SideLight_Enabled_2, datum.SideLight_Brightness_2);
            ApplyChannelLight(LightHandler.LIGHT_BAR_3, datum.SideLight_Enabled_3, datum.SideLight_Brightness_3);
            ApplyChannelLight(LightHandler.LIGHT_BAR_4, datum.SideLight_Enabled_4, datum.SideLight_Brightness_4);

            if (datum.Ring7Light_Enabled)
            {
                LightHandler.Handle.SetOnOff(LightHandler.LIGHT_RING7, true);
                LightHandler.Handle.SetLevel(LightHandler.LIGHT_RING7, datum.Ring7Light_Brightness);
            }
            else
            {
                LightHandler.Handle.SetOnOff(LightHandler.LIGHT_RING7, false);
            }
        }

        // 채널 하나의 On/Off + 밝기 적용. Enabled=true 면 On 후 SetLevel, false 면 Off 만 (기존 그룹 로직과 순서 동일).
        //  Ring/Bar 채널별 반복 호출 지점(ApplyShotLightsInternal) 코드 중복 축소용 헬퍼.
        private void ApplyChannelLight(string channelName, bool bEnabled, int nBrightness)
        {
            if (bEnabled)
            {
                LightHandler.Handle.SetChannelOnOff(channelName, true);
                LightHandler.Handle.SetChannelLevel(channelName, nBrightness);
            }
            else
            {
                LightHandler.Handle.SetChannelOnOff(channelName, false);
            }
        }

        //260623 hbk Phase 49 PROTO-05 (D-08): Index 0(Datum 샷) 수신 = 사이클 시작 → 누적 상태 클린 슬레이트.
        //  비정상 종료(중단 F) 후에도 다음 사이클 시작이 항상 깨끗 — 마지막 Index 후 리셋(누락 위험) 미채택.
        //260722 hbk Phase 68 FIX-0(68-GAP-ANALYSIS.md): 크로스-Z 저장소 clear 는 여기서 더 이상 호출하지 않는다 —
        //  BeginCrossZImageCycle()(z=0 $TEST 수신 즉시, Action 실행 전)으로 이동됨. 이 메서드는 z=0 응답 생성
        //  시점(HandleDatumIndexResponse, 모든 z=0 Action 실행이 끝난 뒤)에 호출되므로, 여기서 크로스-Z를 clear하면
        //  같은 z=0 tick에서 방금 StoreCrossZImage로 저장한 role A 이미지가 z=1 도착 전에 지워져 크로스-Z Datum
        //  검출이 구조적으로 불가능해진다(FIX-0 버그). m_bCycleHasNG/m_bCycleDatumFailed 는 응답 생성 시점에만
        //  write 되므로(ClassifyMeasurement/DetectDatumFailure) 여기 리셋은 그대로 안전하다.
        private void ResetCycleState()
        {
            m_bCycleHasNG = false;
            m_bCycleDatumFailed = false;
            m_bImmediateFailSent = false;   //260722 hbk Phase 68 GAP-3(68-10, T-68-13): latch 도 사이클 시작에 초기화 — 다음 부품으로 누수 방지.
            m_nCurrentZIndex = 0;
            m_nLastZIndex = 0;     // 호출 후 반드시 m_nLastZIndex = ComputeLastZIndex(recipeManager) 재산출 필요 — 호출부 의무 //260623 hbk
        }

        //260722 hbk Phase 68 D-02a: 기존 키에 이미지가 있으면 Dispose 후 image.CopyImage() 를 저장(소유 클론,
        //  ShotConfig.SetImage 계약 미러). public: Action_FAIMeasurement(다른 클래스)가 크로스-Z 캡처 tick 에서
        //  직접 호출 — FindActionIndicesByZIndex 와 동일 cross-class 노출 컨벤션(Plan 02 선례).
        public void StoreCrossZImage(string szKey, HImage image)
        {
            bool bHasKey = !string.IsNullOrEmpty(szKey);
            if (!bHasKey)
            {
                return;
            }
            lock (_crossZImageLock)
            {
                HImage existing;
                bool bHasExisting = m_dicCrossZImages.TryGetValue(szKey, out existing);
                if (bHasExisting && existing != null)
                {
                    try { existing.Dispose(); } catch { }
                }
                if (image != null)
                {
                    m_dicCrossZImages[szKey] = image.CopyImage();
                }
                else
                {
                    m_dicCrossZImages[szKey] = null;
                }
            }
        }

        //260722 hbk Phase 68 D-02a: 저장된 이미지의 CopyImage() 를 반환한다(호출부 소유, finally Dispose 책임 —
        //  ShotConfig.GetImage 계약 미러). 키 없음/이미지 없음 → null.
        public HImage TakeCrossZImageCopy(string szKey)
        {
            bool bHasKey = !string.IsNullOrEmpty(szKey);
            if (!bHasKey)
            {
                return null;
            }
            lock (_crossZImageLock)
            {
                HImage existing;
                bool bHasExisting = m_dicCrossZImages.TryGetValue(szKey, out existing);
                bool bValid = bHasExisting && existing != null;
                if (bValid)
                {
                    return existing.CopyImage();
                }
                return null;
            }
        }

        //260722 hbk Phase 68 D-02a: 크로스-Z 저장소에 해당 키의 이미지가 존재하는지 여부(완성 tick 판정에 사용).
        public bool HasCrossZImage(string szKey)
        {
            bool bHasKey = !string.IsNullOrEmpty(szKey);
            if (!bHasKey)
            {
                return false;
            }
            lock (_crossZImageLock)
            {
                HImage existing;
                bool bHasExisting = m_dicCrossZImages.TryGetValue(szKey, out existing);
                return bHasExisting && existing != null;
            }
        }

        //260722 hbk Phase 68 D-03: 사이클 시작(z=0, BeginCrossZImageCycle) 전용 리셋 — 전 엔트리 Dispose 후 Clear.
        //  Z2 미도달(PLC 중단/스킵)해도 다음 부품의 z=0 도착 시 자동 정리되어 누수 없음(T-68-05 mitigation).
        private void ClearCrossZImages()
        {
            lock (_crossZImageLock)
            {
                foreach (var kvp in m_dicCrossZImages)
                {
                    HImage img = kvp.Value;
                    if (img != null)
                    {
                        try { img.Dispose(); } catch { }
                    }
                }
                m_dicCrossZImages.Clear();
            }
        }

        //260722 hbk Phase 68 FIX-0(68-GAP-ANALYSIS.md): 크로스-Z 저장소의 유일한 clear 진입점 — z=0 $TEST 수신
        //  즉시(그 tick의 Action 실행 시작 전, Custom/SystemHandler.StartV1Scoped z=0 분기)에 1회 호출된다.
        //  왜 여기서(응답 생성 시점 아님): z=0 DatumPhase 가 role A 를 저장하기 전에 저장소를 클린 슬레이트로
        //  만들어야 하고, 그렇게 저장된 role A 는 z=0 응답 시점의 ResetCycleState() 에 더 이상 영향받지 않고
        //  z=1(role B) 도착까지 살아남아야 한다. public: Custom/SystemHandler.StartV1Scoped(다른 클래스)가 직접
        //  호출 — ApplyShotLights/FindActionIndicesByZIndex 와 동일 cross-class 노출 컨벤션(Plan 02 선례).
        public void BeginCrossZImageCycle()
        {
            ClearCrossZImages();
        }

        //260722 hbk Phase 68 D-02a: Action_FAIMeasurement(다른 클래스)가 크로스-Z 캡처 tick 판정 시 현재 $TEST
        //  z_index 를 조회해야 하므로 ParseCurrentZIndex(private) 를 public 래퍼로 노출. RequestPacket 은
        //  SequenceBase.StartCore 에서 Run() 진입 전에 이미 세팅되므로 실행 시점에도 정확한 값을 반환한다.
        public int GetExecutionZIndex()
        {
            return ParseCurrentZIndex();
        }

        //260722 hbk Phase 68 REGR-1(68-GAP-ANALYSIS.md): "이번 실행이 PLC/$TEST 프로토콜 사이클인가"의 단일
        //  진실원 — SequenceBase.RequestPacket(protected 세터, 이 서브클래스에서 직접 읽기 가능)이 null 이 아니면
        //  프로토콜 구동. 수동(UI) RUN(SequenceBase.Start(EAction) → StartCore(.., null))과 RepeatRunService
        //  배치런(StartAll(null)) 은 항상 packet==null 이라 여기서 false — GetExecutionZIndex()==0 과는
        //  구분되는 별도 신호가 필요한 이유: ParseCurrentZIndex 는 packet==null 일 때도 0 을 반환해(D-08 안전
        //  폴백) 진짜 프로토콜 z=0 과 "프로토콜 자체가 없음"을 구별하지 못한다.
        public bool IsProtocolDrivenCycle()
        {
            bool bHasRequestPacket = RequestPacket != null;
            return bHasRequestPacket;
        }

        //260722 hbk Phase 68 GAP-1/GAP-2 (68-GAP-ANALYSIS.md, D-09): "이 z_index 가 크로스-Z Datum 에 쓰이는가"의
        //  단일 소스 헬퍼 — DatumConfigs 순회하여 ZIndexA/ZIndexB(CROSS_Z_UNSET 아닌 것만) 를 set 에 모은다.
        //  BuildDeclaredZIndexSet(GAP-1 유니버스)과 IsDatumOnlyExecutionIndex(GAP-2 실행스코프)가 이 하나를 공유 —
        //  유사 순회 헬퍼 난립 방지(지침 #4).
        private HashSet<int> BuildCrossZDatumIndexSet()
        {
            var crossZSet = new HashSet<int>();
            foreach (var datum in DatumConfigs)
            {
                bool bDatumNull = datum == null;
                if (bDatumNull)
                {
                    continue;
                }
                bool bHasA = datum.ZIndexA != CROSS_Z_UNSET;
                if (bHasA)
                {
                    crossZSet.Add(datum.ZIndexA);
                }
                bool bHasB = datum.ZIndexB != CROSS_Z_UNSET;
                if (bHasB)
                {
                    crossZSet.Add(datum.ZIndexB);
                }
            }
            return crossZSet;
        }

        //260722 hbk Phase 68 GAP-1/GAP-2 (D-09): BuildCrossZDatumIndexSet 의 membership 질의 wrapper.
        private bool IsZIndexUsedByCrossZDatum(int nZIndex)
        {
            return BuildCrossZDatumIndexSet().Contains(nZIndex);
        }

        //260723 hbk Phase 68 GAP-2-ext(68-11 UAT 재검증 중 발견): "이 z_index 가 크로스-Z 측정(Measurement)의
        //  ZIndexA/B(capture-only role 포함) 로 쓰이는가" — BuildCrossZDatumIndexSet 과 대칭 구조(지침 #4 재사용
        //  원칙, AddFaiDeclaredZIndices sub-헬퍼 재사용). WarnIfEmptyScope 가 기존엔 크로스-Z Datum-only index 만
        //  스퓨리어스 에러를 억제했고, 크로스-Z 측정(예: SHOT_E5 ZIndexA=1, own ZIndex=0)의 비완성 capture tick 은
        //  놓쳐서 매 사이클 "[V1Cycle] 매칭 0건" 에러가 찍혔다(AggregateIndexFais 가 own ZIndex 로만 집계하므로
        //  capture-only tick 은 항상 매칭 0건이 정상).
        private HashSet<int> BuildCrossZMeasurementIndexSet()
        {
            var crossZSet = new HashSet<int>();
            var recipeManager = SystemHandler.Handle.Sequences.RecipeManager;
            bool bHasManager = recipeManager != null;
            if (!bHasManager)
            {
                return crossZSet;
            }
            foreach (var shot in recipeManager.Shots)
            {
                bool bShotNull = shot == null;
                if (bShotNull)
                {
                    continue;
                }
                bool bOwnedByThisSeq = shot.OwnerSequenceName == Name;
                if (!bOwnedByThisSeq)
                {
                    continue;
                }
                foreach (var fai in shot.FAIList)
                {
                    AddFaiDeclaredZIndices(fai, crossZSet);
                }
            }
            return crossZSet;
        }

        //260723 hbk Phase 68 GAP-2-ext: BuildCrossZMeasurementIndexSet 의 membership 질의 wrapper.
        private bool IsZIndexUsedByCrossZMeasurement(int nZIndex)
        {
            return BuildCrossZMeasurementIndexSet().Contains(nZIndex);
        }

        //260722 hbk Phase 68 GAP-1 (D-09): 한 FAI 의 Measurements 중 DualImageEdgeDistanceMeasurement 의
        //  ZIndexA/ZIndexB(CROSS_Z_UNSET 아닌 것만) 를 declaredSet 에 추가. BuildDeclaredZIndexSet 의 sub-헬퍼(함수 30줄 가드).
        private void AddFaiDeclaredZIndices(FAIConfig fai, HashSet<int> declaredSet)
        {
            bool bFaiNull = fai == null;
            if (bFaiNull)
            {
                return;
            }
            foreach (var meas in fai.Measurements)
            {
                var dualMeas = meas as DualImageEdgeDistanceMeasurement;
                bool bIsDualImage = dualMeas != null;
                if (!bIsDualImage)
                {
                    continue;
                }
                bool bHasA = dualMeas.ZIndexA != CROSS_Z_UNSET;
                if (bHasA)
                {
                    declaredSet.Add(dualMeas.ZIndexA);
                }
                bool bHasB = dualMeas.ZIndexB != CROSS_Z_UNSET;
                if (bHasB)
                {
                    declaredSet.Add(dualMeas.ZIndexB);
                }
            }
        }

        //260722 hbk Phase 68 GAP-1 (D-09): 이 시퀀스 소유 Shot 1개의 own ZIndex + 소유 측정 ZIndexA/B 를
        //  declaredSet 에 추가. BuildDeclaredZIndexSet 의 sub-헬퍼(함수 30줄 가드).
        private void AddShotDeclaredZIndices(ShotConfig shot, HashSet<int> declaredSet)
        {
            bool bShotNull = shot == null;
            if (bShotNull)
            {
                return;
            }
            bool bOwnedByThisSeq = shot.OwnerSequenceName == Name;
            if (!bOwnedByThisSeq)
            {
                return;
            }
            declaredSet.Add(shot.ZIndex);
            foreach (var fai in shot.FAIList)
            {
                AddFaiDeclaredZIndices(fai, declaredSet);
            }
        }

        //260722 hbk Phase 68 GAP-1(68-GAP-ANALYSIS.md 우선순위 1): "선언된 z_index 유니버스" — 이 시퀀스 소유
        //  Shot own ZIndex + 그 Shot 이 소유한 측정 ZIndexA/B + Datum ZIndexA/B(BuildCrossZDatumIndexSet) 합집합.
        //  ★ 위험 규칙 미포함(지침 #3): "완성 index(max(ZIndexA,ZIndexB))가 최대 shot.ZIndex 초과 시 오설정" 규칙은
        //  Plan 03 BLOCKER(완성 index는 shot.ZIndex와 독립)를 다시 깨뜨리므로 절대 추가하지 않는다 — 여기선 순수 membership 만.
        //  D-07: 전 add 는 != CROSS_Z_UNSET 게이트 하에서만 — ZIndexA/B 미설정 기존 레시피는 유니버스=Shot own ZIndex 집합으로 회귀 0.
        private HashSet<int> BuildDeclaredZIndexSet()
        {
            var declaredSet = new HashSet<int>();
            var recipeManager = SystemHandler.Handle.Sequences.RecipeManager;
            bool bHasManager = recipeManager != null;
            if (bHasManager)
            {
                foreach (var shot in recipeManager.Shots)
                {
                    AddShotDeclaredZIndices(shot, declaredSet);
                }
            }
            declaredSet.UnionWith(BuildCrossZDatumIndexSet());
            return declaredSet;
        }

        //260722 hbk Phase 68 GAP-1: Action_FAIMeasurement(다른 클래스)가 크로스-Z 오설정(존재하지 않는 z_index
        //  참조) 판정에 사용 — FindShotByZIndex(shot.ZIndex 단독) 위임을 제거하고 BuildDeclaredZIndexSet(선언 유니버스)
        //  membership 으로 재작성. Side(z=1, Datum ZIndexB=1) / SHOT_E5(own ZIndex=0, 측정 ZIndexA=1) 모두 이 유니버스에 포함된다.
        public bool DoesZIndexExistInRecipe(int nZIndex)
        {
            return BuildDeclaredZIndexSet().Contains(nZIndex);
        }

        //260722 hbk Phase 68 GAP-2(68-GAP-ANALYSIS.md 우선순위 2): 오직 크로스-Z Datum 만 쓰는 z_index(예: Side z=1) 도착 시,
        //  실행 스코프(FindActionIndicesByZIndex)가 매칭 0건이라도 StartAll 폴백 대신 datum-only 최소 실행으로 라우팅해야 함을 판정.
        //  ★ 필수 가드(지침 #5): nZIndex==DATUM_Z_INDEX(z=0) 이면 최상단에서 무조건 false — D-01a(z=0 StartAll 전량 실행)가
        //  datum-only 오판정으로 무너지는 것을 방지. 그 다음: 크로스-Z Datum 이 쓰는 index 이면서 동시에 일반 실행 매칭(own ZIndex/측정 ZIndexA/B)이
        //  없는 경우만 true.
        public bool IsDatumOnlyExecutionIndex(int nZIndex)
        {
            bool bIsDatumTestZIndex = nZIndex == DATUM_Z_INDEX;
            if (bIsDatumTestZIndex)
            {
                return false;
            }
            bool bUsedByCrossZDatum = IsZIndexUsedByCrossZDatum(nZIndex);
            bool bHasRegularExec = FindActionIndicesByZIndex(nZIndex).Count > 0;
            return bUsedByCrossZDatum && !bHasRegularExec;
        }

        //260722 hbk Phase 68 GAP-2: datum-only index(IsDatumOnlyExecutionIndex==true) 에서 DatumPhase 만 트리거할
        //  최소 Action 인덱스 목록 — DatumConfigs 중 ZIndexA/B==nZIndex 인 datum 의 SourceShotName 을 Actions[] 에서 역추적.
        //  SourceShotName 미해결 datum 은 이 시퀀스 소유 첫 Action(가장 작은 owned index)을 트리거로 add(로그 명시) — DatumPhase 는
        //  실행된 Action 하나에서 DatumConfigs 전체를 재검출하므로 '데이터' 오귀속(D-05 안티패턴)이 아니라 트리거용 Action 선택일 뿐이다.
        public List<int> FindDatumOnlyActionIndices(int nZIndex)
        {
            var triggerIndices = new HashSet<int>();
            bool bHasActions = Actions != null;
            if (!bHasActions)
            {
                return new List<int>();
            }
            foreach (var datum in DatumConfigs)
            {
                bool bMatchesThisZIndex = datum != null && (datum.ZIndexA == nZIndex || datum.ZIndexB == nZIndex);
                if (bMatchesThisZIndex)
                {
                    AddDatumTriggerActionIndex(datum, triggerIndices);
                }
            }
            return new List<int>(triggerIndices);
        }

        //260722 hbk Phase 68 GAP-2: FindDatumOnlyActionIndices 의 sub-헬퍼(함수 30줄 가드) — 단일 datum 의 트리거 Action 인덱스 해석.
        private void AddDatumTriggerActionIndex(DatumConfig datum, HashSet<int> triggerIndices)
        {
            int nFirstOwnedIndex;
            int nResolvedIndex = FindOwnedActionIndexForSourceShot(datum, out nFirstOwnedIndex);
            bool bResolved = nResolvedIndex != -1;
            if (bResolved)
            {
                triggerIndices.Add(nResolvedIndex);
                return;
            }
            AddFallbackTriggerWithLog(datum, nFirstOwnedIndex, triggerIndices);
        }

        //260722 hbk Phase 68 GAP-2: AddDatumTriggerActionIndex 의 sub-헬퍼(함수 30줄 가드) — Actions[] 순회하여
        //  SourceShotName 매칭 인덱스를 찾는다. 매칭 없으면 -1 반환하되, 이 시퀀스 소유 첫 Action index 는 out 으로 남긴다(폴백용).
        private int FindOwnedActionIndexForSourceShot(DatumConfig datum, out int nFirstOwnedIndex)
        {
            nFirstOwnedIndex = -1;
            for (int i = 0; i < Actions.Length; i++)
            {
                var faiAct = Actions[i] as Action_FAIMeasurement;
                ShotConfig shot = null;
                if (faiAct != null)
                {
                    shot = faiAct.ShotParam;
                }
                bool bOwnedByThisSeq = shot != null && shot.OwnerSequenceName == Name;
                if (!bOwnedByThisSeq)
                {
                    continue;
                }
                if (nFirstOwnedIndex == -1)
                {
                    nFirstOwnedIndex = i;
                }
                bool bSourceShotMatch = !string.IsNullOrEmpty(datum.SourceShotName) && shot.ShotName == datum.SourceShotName;
                if (bSourceShotMatch)
                {
                    return i;
                }
            }
            return -1;
        }

        //260722 hbk Phase 68 GAP-2: AddDatumTriggerActionIndex 의 sub-헬퍼(함수 30줄 가드) — SourceShotName 미해결 시
        //  이 시퀀스 소유 첫 Action 을 트리거로 add 하고 로그로 명시(D-05 조용한 Shots[0] 안티패턴과 구분).
        private void AddFallbackTriggerWithLog(DatumConfig datum, int nFirstOwnedIndex, HashSet<int> triggerIndices)
        {
            bool bHasFallback = nFirstOwnedIndex != -1;
            if (!bHasFallback)
            {
                return;
            }
            string datumName = datum.DatumName;
            if (datumName == null)
            {
                datumName = "";
            }
            Logging.PrintLog((int)ELogType.Error,
                "[V1Scope] Datum '" + datumName + "' SourceShotName 미해결 — 첫 owned Action(index=" + nFirstOwnedIndex + ")을 DatumPhase 트리거로 사용. //260722 hbk");
            triggerIndices.Add(nFirstOwnedIndex);
        }

        //260722 hbk Phase 68(68-12, 68-GAP-ANALYSIS.md 후속): z=0(Datum) 자신에서 이 시퀀스의 대표 Datum
        //  트리거 Action(들)을 해석 — FindDatumOnlyActionIndices(z>=1 크로스-Z 전용)와 달리 ZIndexA/ZIndexB
        //  필터를 두지 않는다. 일반(비-크로스-Z) Datum 은 ZIndexA/B 개념 자체가 없어(-1/-1) 그 필터로는 절대
        //  매칭되지 않지만, z=0 이 곧 그 Datum 의 검출 시점이라는 사실만으로 대표 Action 이 필요하기 때문이다.
        //  AddDatumTriggerActionIndex(SourceShotName 역추적 + 미해결시 로그폴백)를 그대로 재사용해 로직 중복을 피한다(D-09).
        public List<int> FindZeroIndexDatumTriggerActionIndices()
        {
            var triggerIndices = new HashSet<int>();
            bool bHasActions = Actions != null;
            if (!bHasActions)
            {
                return new List<int>();
            }
            foreach (var datum in DatumConfigs)
            {
                bool bDatumNull = datum == null;
                if (bDatumNull)
                {
                    continue;
                }
                AddDatumTriggerActionIndex(datum, triggerIndices);
            }
            return new List<int>(triggerIndices);
        }

        //260722 hbk Phase 68(68-12): z=0 대표 트리거 실행 후 이 Action 의 Grab/Measure 를 건너뛸지 판정하는
        //  단일 진입점 — 기존 IsDatumOnlyExecutionIndex(z>=1 크로스-Z 전용 경로, 무변경)와 신규 z=0 대표트리거
        //  경로를 OR 결합한다. IsDatumOnlyExecutionIndex 를 직접 수정하지 않는 이유: 그 술어의 계약("크로스-Z
        //  Datum 만 쓰고 일반 실행 매칭 없음")은 일반(비-크로스-Z) Datum 에는 애초에 성립하지 않는 개념이며,
        //  IsZIndexUsedByCrossZDatum 은 WarnIfEmptyScope/GAP-1 BuildDeclaredZIndexSet 도 소비하므로 억지로
        //  의미를 넓히면 그 소비처들의 의미까지 오염시킬 위험이 있다(68-12-PLAN.md investigation_findings) —
        //  그래서 이 메서드가 병행 경로로 OR 만 담당한다.
        //260722 hbk Phase 68 REGR-1(68-GAP-ANALYSIS.md): z=0 대표트리거 스킵(FindZeroIndexDatumTriggerActionIndices
        //  경로)은 IsProtocolDrivenCycle()==true 일 때만 적용한다 — 프로토콜 $TEST(z=0) 는 이 Shot 의 진짜 측정이
        //  나중 z_index 에 다시 트리거되므로 이번 z=0 측정을 건너뛰어도 안전하지만, 수동(UI) RUN 과
        //  RepeatRunService 배치런은 packet==null 이라 GetExecutionZIndex()==0 으로 동일하게 관측되면서도 이후
        //  z_index 트리거가 전혀 오지 않는다 — 여기서 스킵하면 그 Shot 은 이번 사이클에 영구히 미측정('—')이
        //  된다(이 사이트의 실제 생산 워크플로인 수동 지그 RUN 버튼 회귀, 260722 확인). IsDatumOnlyExecutionIndex
        //  (z>=1 경로) 는 이 가드가 필요 없다 — packet==null 이면 ParseCurrentZIndex 가 항상 0 을 반환하므로
        //  nZIndex 는 여기 도달할 때 이미 0 이고, 그 함수 최상단의 nZIndex==DATUM_Z_INDEX 가드가 이미 false 를
        //  강제한다(호출부 GetExecutionZIndex() 도달 경로 분석으로 확인, 별도 가드 중복 불필요).
        public bool ShouldSkipMeasurementAfterDatumPhase(int nZIndex)
        {
            bool bIsCrossZDatumOnly = IsDatumOnlyExecutionIndex(nZIndex);
            if (bIsCrossZDatumOnly)
            {
                return true;
            }
            bool bIsZeroIndex = nZIndex == DATUM_Z_INDEX;
            if (!bIsZeroIndex)
            {
                return false;
            }
            bool bIsProtocolDriven = IsProtocolDrivenCycle();
            if (!bIsProtocolDriven)
            {
                return false;
            }
            return FindZeroIndexDatumTriggerActionIndices().Count > 0;
        }

        //260623 hbk Phase 49 PROTO-03 (D-08): RequestPacket.TestID(=z_index 문자열, "-1"=미수신)를 정수 파싱.
        //  파싱 실패/미수신/음수 → 0(Datum/Idx0 폴백) 으로 안전 정규화 (T-49-03 mitigation).
        private int ParseCurrentZIndex()
        {
            int nZ = 0;
            bool bHasRequest = RequestPacket != null;
            if (!bHasRequest)
            {
                return nZ;
            }
            string szTestId = RequestPacket.TestID;
            bool bHasId = !string.IsNullOrEmpty(szTestId);
            if (!bHasId)
            {
                return nZ;
            }
            int nParsed = 0;
            bool bValid = int.TryParse(szTestId, out nParsed);
            bool bNonNegative = nParsed >= 0;
            if (bValid && bNonNegative)
            {
                nZ = nParsed;
            }
            return nZ;
        }

        //260623 hbk Phase 49 PROTO-04 (D-04/D-06): Index 0(Datum 샷) 응답 생성.
        //  정상 → 빈 응답 RESULT:site;B;0; (IsBuffer=true, FAIResults 비움). 실패 → 즉시 F (UseProtocolV1 분기에서만).
        //  m_bCycleDatumFailed = 호출 전 DetectDatumFailure() 로 설정됨 (HandleDatumIndexResponse).
        private TestResultPacket BuildDatumShotResponse()
        {
            var packet = new TestResultPacket
            {
                Target = RequestPacket.Sender,
                Site = RequestPacket.Site,
                InspectionType = RequestPacket.TestType,
                IsDynamicFAI = true,
                Type = RequestPacket.Type,   //260624 hbk Phase 63 PROTO-Type: 수신 Type echo
            };
            bool bUseV1 = SystemHandler.Handle.Setting.UseProtocolV1;
            bool bDatumFailed = m_bCycleDatumFailed;
            bool bImmediateFail = bUseV1 && bDatumFailed;
            if (bImmediateFail)
            {
                // 즉시 F — 후속 Index skip 은 핸들러 주도(D-05). IsBuffer=false + Result=NG → 직렬화 'F'.
                packet.IsBuffer = false;
                packet.Result = EVisionResultType.NG;
                m_bImmediateFailSent = true;   //260722 hbk Phase 68 GAP-3(68-10, 지침 #6/T-68-11): z=0 즉시-F 도 latch 세팅 —
                                                //  완성 index 재평가(TryApplyCrossZDatumImmediateFail)가 중복 F 를 또 보내지 않도록.
                return packet;
            }
            // 정상 Datum 샷 → 빈 응답 RESULT:site;B;0; (FAIResults 비어있음 → FAICount=0).
            packet.IsBuffer = true;
            packet.Result = EVisionResultType.OK;
            return packet;
        }

        //260623 hbk Phase 49 PROTO-04: 이 시퀀스 소유 Datum 중 검출 실패(IsDatumFailed)가 1건이라도 있으면 true.
        //  Action_FAIMeasurement.DatumPhase 가 MarkDatumFailed → _failedDatums 에 누적. 빈/누락 datum 은 skip.
        private bool DetectDatumFailure()
        {
            bool bAnyFailed = false;
            foreach (var datum in DatumConfigs)
            {
                bool bIsNull = datum == null;
                if (bIsNull)
                {
                    continue;
                }
                bool bFailed = IsDatumFailed(datum.DatumName);
                if (bFailed)
                {
                    bAnyFailed = true;
                }
            }
            return bAnyFailed;
        }

        //260623 hbk Phase 49 PROTO-03/04/05: v1.0 z_index 멀티샷 사이클 판정 엔진 진입점.
        //  Index 0 = 사이클 시작(리셋 D-08) + Datum 빈응답/즉시F(D-04/D-06) → HandleDatumIndexResponse() 위임(본문 30줄 유지).
        //  중간 Index = 해당 z_index Shot 집계(D-01) + NG 누적(D-02) + B(IsBuffer=true).
        //  마지막 Index(z_index 최댓값 D-03) = 집계 + m_bCycleHasNG 반영 종합 P/F.
        private void AddResponseV1Cycle()
        {
            var recipeManager = SystemHandler.Handle.Sequences.RecipeManager;
            m_nCurrentZIndex = ParseCurrentZIndex();
            bool bIsDatumShot = m_nCurrentZIndex == DATUM_Z_INDEX;
            if (bIsDatumShot)
            {
                HandleDatumIndexResponse(recipeManager);   // D-08 리셋 + 재산출 + Datum 응답 + 영속화 (ComputeLastZIndex 1회)
                return;
            }
            // 측정 Index (중간 or 마지막) — m_nLastZIndex 는 이 경로에서 1회만 산출(이중 호출 제거, BLOCKER 2-a).
            m_nLastZIndex = ComputeLastZIndex(recipeManager);
            bool bIsLastIndex = m_nCurrentZIndex >= m_nLastZIndex;
            TestResultPacket packet = BuildScopedResponse(recipeManager, m_nCurrentZIndex, bIsLastIndex);
            PersistAndEnqueueV1(recipeManager, packet);
        }

        //260623 hbk Phase 49 PROTO-04/05 (D-08): Index 0(Datum 샷) 처리 — 사이클 리셋 + Datum 검출 감지 + 응답 + 영속화.
        //  ResetCycleState() 가 m_nLastZIndex=0 으로 덮으므로 직후 단 1회 재산출(BLOCKER 2-a, 측정 경로는 자체 산출).
        private void HandleDatumIndexResponse(InspectionRecipeManager recipeManager)
        {
            ResetCycleState();                                  // D-08: 사이클 시작 = 클린 슬레이트
            m_nCurrentZIndex = DATUM_Z_INDEX;
            m_nLastZIndex = ComputeLastZIndex(recipeManager);   // 리셋 직후 재산출(호출부 의무, ResetCycleState 주석)
            m_bCycleDatumFailed = DetectDatumFailure();         // D-04: Datum 검출 실패 감지
            TestResultPacket datumPacket = BuildDatumShotResponse();
            PersistAndEnqueueV1(recipeManager, datumPacket);
        }

        //260623 hbk Phase 49 PROTO-03 (D-01/D-02/D-03): Index-scoped 집계 + B/P/F 응답 조립.
        //  이 시퀀스 소유 AND shot.ZIndex == nZIndex 인 Shot 의 FAI 만 집계(전체 재검사 금지 D-01).
        //  중간 Index → B(IsBuffer=true, NG 있어도 B). 마지막 Index → 종합 P/F(m_bCycleHasNG||Datum실패).
        //  불변식: NG 발견돼도 마지막 Index 까지 측정 진행(측정은 Action_FAIMeasurement, 여기는 집계만). 종료 판정은 마지막에서만.
        private TestResultPacket BuildScopedResponse(InspectionRecipeManager recipeManager, int nZIndex, bool bIsLastIndex)
        {
            var packet = new TestResultPacket
            {
                Target = RequestPacket.Sender,
                Site = RequestPacket.Site,
                InspectionType = RequestPacket.TestType,
                IsDynamicFAI = true,
                Type = RequestPacket.Type,   //260624 hbk Phase 63 PROTO-Type: 수신 Type echo
            };
            int nMatchedShots = AggregateIndexFais(recipeManager, nZIndex, packet);
            WarnIfEmptyScope(packet, nMatchedShots, nZIndex);   // BLOCKER 1: ZIndex 매칭 0건 경고(조용한 빈 B 금지)
            ApplyCycleJudgement(packet, bIsLastIndex, nMatchedShots);   // B vs 종합 P/F (D-03/불변식), WR-01: 매칭 0건 전달
            TryApplyCrossZDatumImmediateFail(packet, nZIndex);   //260722 hbk Phase 68 GAP-3(68-10): 완성 index 크로스-Z Datum 실패 재평가(게이팅, 기본 OFF no-op)
            pMyContext.ResultInfo = packet.Result;
            return packet;
        }

        //260722 hbk Phase 68 Task4(BLOCKER): 측정 완성 응답 index 산출 — 크로스-Z 측정(ZIndexA/B 둘 다 설정)은
        //  max(ZIndexA,ZIndexB)(Action_FAIMeasurement.TryExecuteCrossZMeasurement 완성 index 정의와 동일),
        //  그 외(기존 non-cross-Z)는 shot.ZIndex(기존 own-index 의미 그대로 보존 — D-07 회귀 0).
        private int GetMeasurementCompletionZIndex(MeasurementBase meas, ShotConfig shot)
        {
            var dualMeas = meas as DualImageEdgeDistanceMeasurement;
            bool bIsCrossZ = dualMeas != null && dualMeas.ZIndexA != CROSS_Z_UNSET && dualMeas.ZIndexB != CROSS_Z_UNSET;
            if (bIsCrossZ)
            {
                return System.Math.Max(dualMeas.ZIndexA, dualMeas.ZIndexB);
            }
            return shot.ZIndex;
        }

        //260722 hbk Phase 68 CROSS-2 (68-GAP-ANALYSIS.md 교차이슈, D-09): Datum 완성 index 단일 소스 —
        //  GetMeasurementCompletionZIndex(측정)와 대칭. 크로스-Z Datum(ZIndexA/B 둘 다 설정)만 완성 index 개념이
        //  있음 → max(ZIndexA,ZIndexB) 반환. 비-크로스-Z Datum(ZIndexA 또는 ZIndexB 미설정, 기존 정적 이미지 경로)은
        //  완성 index 개념 자체가 없음 → CROSS_Z_UNSET 반환(D-07 게이트, 호출부가 != CROSS_Z_UNSET 으로 필터링).
        //  MaxCrossZCompletionZIndex(이 파일, CROSS-2)와 GAP-3(68-10, Datum 완성 index 즉시-F 재평가)의 단일 소스.
        private int GetDatumCompletionZIndex(DatumConfig datum)
        {
            bool bIsCrossZ = datum != null && datum.ZIndexA != CROSS_Z_UNSET && datum.ZIndexB != CROSS_Z_UNSET;
            if (bIsCrossZ)
            {
                return System.Math.Max(datum.ZIndexA, datum.ZIndexB);
            }
            return CROSS_Z_UNSET;
        }

        //260722 hbk Phase 68 GAP-3(68-10, 지침 #6/#7, 68-GAP-ANALYSIS.md): 완성 index(GetDatumCompletionZIndex,
        //  Side 는 z=1) 응답 생성 시점에 크로스-Z Datum 실패를 재평가 — z=0 에서만 산출되던 m_bCycleDatumFailed 가
        //  크로스-Z Datum(실제 검출은 완성 index 에서 일어남)엔 "즉시 F" 계약을 이행 못 하던 GAP-3 근본원인 수정.
        //  게이팅(T-68-12): EnableCrossZDatumImmediateFail(체크포인트 결정 enable-after-agreement, 기본 true) —
        //  Vision-Protocol-v1.0.md 판정표 F 행 "PLC 동작"은 index 무관 "NG 처리" 단일 규정(PLC 는 B vs P/F 만
        //  분기, index 로 분기하지 않음) → z>=1 F 도 index 0 과 동일하게 처리되어 제어팀 합의 근거로 ON 확정.
        //  latch(T-68-11): m_bImmediateFailSent 이미 세팅(z=0 즉시-F 분기에서)이면 재평가 없이 즉시 return —
        //  한 사이클 최대 1회 F 만 나가도록 보장(중복-F 방지).
        private void TryApplyCrossZDatumImmediateFail(TestResultPacket packet, int nZIndex)
        {
            bool bFlagOn = SystemHandler.Handle.Setting.EnableCrossZDatumImmediateFail && SystemHandler.Handle.Setting.UseProtocolV1;
            if (!bFlagOn)
            {
                return;
            }
            if (m_bImmediateFailSent)
            {
                return;
            }
            foreach (var datum in DatumConfigs)
            {
                bool bDatumNull = datum == null;
                if (bDatumNull)
                {
                    continue;
                }
                int nCompletionZIndex = GetDatumCompletionZIndex(datum);
                bool bIsCrossZDatum = nCompletionZIndex != CROSS_Z_UNSET;
                bool bCompletesHere = bIsCrossZDatum && nCompletionZIndex == nZIndex;
                bool bFailed = IsDatumFailed(datum.DatumName);
                bool bImmediateFailHere = bCompletesHere && bFailed;
                if (bImmediateFailHere)
                {
                    packet.IsBuffer = false;
                    packet.Result = EVisionResultType.NG;
                    m_bImmediateFailSent = true;
                    return;
                }
            }
        }

        //260722 hbk Phase 68 Task4(BLOCKER): shot 이 소유한 측정 중 크로스-Z 이고 완성 index==nZIndex 인 것이
        //  하나라도 있으면 true. AggregateIndexFais 의 in-scope 조건을 shot.ZIndex 우연값과 무관하게 확장해,
        //  크로스-Z 측정의 owning Shot 이 완성 index 응답 집계에 반드시 포함되도록 한다(BLOCKER 핵심 — inclusion).
        private bool ShotHasCrossZMeasurementCompletingAt(ShotConfig shot, int nZIndex)
        {
            bool bHasShot = shot != null;
            if (!bHasShot)
            {
                return false;
            }
            foreach (var fai in shot.FAIList)
            {
                bool bFaiNull = fai == null;
                if (bFaiNull)
                {
                    continue;
                }
                foreach (var meas in fai.Measurements)
                {
                    var dualMeas = meas as DualImageEdgeDistanceMeasurement;
                    bool bIsCrossZ = dualMeas != null && dualMeas.ZIndexA != CROSS_Z_UNSET && dualMeas.ZIndexB != CROSS_Z_UNSET;
                    if (!bIsCrossZ)
                    {
                        continue;
                    }
                    bool bCompletesHere = GetMeasurementCompletionZIndex(meas, shot) == nZIndex;
                    if (bCompletesHere)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        //260623 hbk Phase 49 (D-01): nZIndex 매칭 Shot 의 FAI 만 packet.FAIResults 에 집계. 매칭 Shot 수 반환.
        //260722 hbk Phase 68 Task4(BLOCKER): in-scope 조건에 bCrossZCompletesHere 추가 — 크로스-Z 측정의 owning
        //  Shot 은 자신의 own-ZIndex 와 다른 완성 index 응답에도 포함되어야 한다(shot.ZIndex 우연값 무관, 코드레벨 보장).
        private int AggregateIndexFais(InspectionRecipeManager recipeManager, int nZIndex, TestResultPacket packet)
        {
            int nMatchedShots = 0;
            bool bHasManager = recipeManager != null;
            if (!bHasManager)
            {
                return nMatchedShots;
            }
            foreach (var shot in recipeManager.Shots)
            {
                bool bIsNull = shot == null;
                if (bIsNull)
                {
                    continue;
                }
                bool bOwnedByThisSeq = shot.OwnerSequenceName == Name;
                bool bZMatch = shot.ZIndex == nZIndex;
                bool bCrossZCompletesHere = ShotHasCrossZMeasurementCompletingAt(shot, nZIndex);
                bool bInScope = bOwnedByThisSeq && (bZMatch || bCrossZCompletesHere);
                if (!bInScope)
                {
                    continue;
                }
                nMatchedShots = nMatchedShots + 1;
                foreach (var fai in shot.FAIList)
                {
                    AddFaiResult(packet, fai, shot, nZIndex);
                }
            }
            return nMatchedShots;
        }

        //260629 hbk FAI 단위 1항목 → 측정 단위 N항목 전환. P2 등 다측정 불량값 은폐 제거 (ETI-RESULT-PER-MEASUREMENT).
        //260722 hbk Phase 68 Task4(BLOCKER): shot/nZIndex 추가 — 측정별 GetMeasurementCompletionZIndex 게이트로
        //  완성 index 에서만 담기고(exclusion) 비완성 index 에서는 own-index 로 in-scope 여도 제외된다.
        private void AddFaiResult(TestResultPacket packet, FAIConfig fai, ShotConfig shot, int nZIndex)
        {
            bool bIsNull = fai == null; //260629 hbk null 가드 유지
            if (bIsNull)
            {
                return;
            }
            string szFaiName = fai.FAIName; //260629 hbk FAI 이름 폴백 준비 (헝가리언)
            if (string.IsNullOrEmpty(szFaiName))
            {
                szFaiName = "FAI"; //260629 hbk FAIName null/빈 문자열 → "FAI" 폴백
            }
            int nMeasCount = fai.Measurements.Count; //260629 hbk 측정 개수 (0 = Datum 샷 → 루프 0회 = 항목 0개)
            for (int i = 0; i < nMeasCount; i++) //260629 hbk 측정마다 항목 1개 추가
            {
                MeasurementBase meas = fai.Measurements[i]; //260629 hbk 측정 단위 순회
                if (meas == null) //260629 hbk null 측정 방어
                {
                    continue;
                }
                bool bReportHere = GetMeasurementCompletionZIndex(meas, shot) == nZIndex; //260722 hbk Phase 68 Task4: 완성 index 게이트
                if (!bReportHere)
                {
                    continue;
                }
                string szItemId; //260629 hbk 측정 단위 id 네이밍 (삼항 금지 → if-else)
                if (nMeasCount > 1)
                {
                    szItemId = szFaiName + "_P" + (i + 1).ToString(); //260629 hbk 다측정 FAI: FAIName_P{1-based}
                }
                else
                {
                    szItemId = szFaiName; //260629 hbk 단측정 FAI: suffix 없이 FAIName 그대로
                }
                EVisionResultType eCode = ClassifyMeasurement(meas); //260629 hbk 측정 단위 3-state 분류
                double dVal = meas.LastMeasuredValue; //260629 hbk 측정값(mm) — fai.MeasuredValue 더 이상 사용 안 함
                packet.FAIResults.Add(new FAIResultData(szItemId, eCode, dVal)); //260629 hbk 측정 1건 → 와이어 항목 1개
            }
        }

        //260629 hbk 측정 단위 3-state 분류 — datum/align-skip('N')·측정 NG('F')는 m_bCycleHasNG 누적. 그 외 OK('P'). //260710 hbk 죽은 FAI 단위 분류 메서드 제거로 문구 정리
        private EVisionResultType ClassifyMeasurement(MeasurementBase meas)
        {
            string szSkip = meas.LastSkipReason; //260629 hbk 측정 단위 skip 사유
            bool bDatumSkipped = (szSkip == SkipReason.DATUM_FAIL) || (szSkip == SkipReason.ALIGN_FAIL); //260629 hbk datum/align 검출 실패 skip = 'N' //260710 hbk 상수화
            if (bDatumSkipped)
            {
                m_bCycleHasNG = true; //260629 hbk 검출 실패도 사이클 NG 누적
                return EVisionResultType.NotExist;
            }
            bool bNotPass = !meas.LastJudgement; //260629 hbk LastJudgement true=OK
            if (bNotPass)
            {
                m_bCycleHasNG = true; //260629 hbk 측정 NG → 사이클 NG 누적
                return EVisionResultType.NG;
            }
            return EVisionResultType.OK; //260629 hbk 정상 측정
        }

        //260623 hbk Phase 49 BLOCKER 1 (D-01 정합): ZIndex 매칭 0건(빈 결과 + 매칭 Shot 0)이면 PrintErrLog 경고.
        //  레시피 ZIndex 미설정(전부 0) + 측정 Index 수신 = 운용 오류. 폴백(전체 재검사) 금지 — 경고만.
        //  WR-01 fix: 중간 Index 면 빈 B 유지, 마지막 Index 면 ApplyCycleJudgement 가 F 강제(false-PASS 차단).
        //260722 hbk Phase 68 GAP-2(f): datum-only index(예: Side z=1, 오직 크로스-Z Datum 만 씀)는 측정 항목이
        //  적법하게 0건(완성 index 아님)이므로 이 억제 없이는 매 사이클 스퓨리어스 Error 로그가 발생한다.
        //260723 hbk Phase 68 GAP-2-ext(68-11 UAT 재검증 중 발견): 크로스-Z 측정(Measurement)의 비완성 capture
        //  role(예: SHOT_E5 ZIndexA=1, own ZIndex=0)도 같은 이유로 매칭 0건이 적법 — Datum 케이스만 억제하던
        //  기존 가드를 측정 케이스까지 확장.
        private void WarnIfEmptyScope(TestResultPacket packet, int nMatchedShots, int nZIndex)
        {
            bool bDatumOnlyIndex = IsDatumOnlyExecutionIndex(nZIndex);
            bool bCrossZMeasurementCaptureIndex = IsZIndexUsedByCrossZMeasurement(nZIndex);
            bool bSuppressWarning = bDatumOnlyIndex || bCrossZMeasurementCaptureIndex;
            if (bSuppressWarning)
            {
                return;
            }
            bool bNoResults = packet.FAIResults.Count == 0;
            bool bNoMatch = nMatchedShots == 0;
            bool bEmptyScope = bNoResults && bNoMatch;
            if (bEmptyScope)
            {
                Logging.PrintErrLog((int)ELogType.Error,
                    "[V1Cycle] BuildScopedResponse 빈 결과: ZIndex 매칭 0건 (Seq=" + Name + ", z=" + nZIndex + ", last=" + m_nLastZIndex + "). 레시피 ZIndex 설정 확인 필요. //260623 hbk");
            }
            else
            {
                // 정상 집계 — 추가 처리 없음.
            }
        }

        //260623 hbk Phase 49 (D-03/불변식): 중간 Index → B(IsBuffer=true). 마지막 Index → 종합 P/F(IsBuffer=false).
        //260623 hbk Phase 49 WR-01 fix: 마지막 Index 인데 매칭 Shot 0건이면 P 금지 → F 강제(fail-safe).
        //  ZIndex 미설정 레시피서 측정 Index(z>=1) 수신 시 ComputeLastZIndex=0 → 1>=0 으로 마지막 오인 → 매칭 0건 → 종합 P 송신(검사 0건 합격 통보) silent false-PASS 차단.
        private void ApplyCycleJudgement(TestResultPacket packet, bool bIsLastIndex, int nMatchedShots)
        {
            if (!bIsLastIndex)
            {
                // 중간 Index — NG 있어도 B (종료 판정은 마지막 Index 에서만).
                packet.IsBuffer = true;
                packet.Result = EVisionResultType.OK;   // 직렬화 IsBuffer=true 최우선 → 'B'
                return;
            }
            // 마지막 Index — 사이클 누적 NG/Datum실패 반영 종합 P/F.
            packet.IsBuffer = false;
            bool bEmptyLastScope = nMatchedShots == 0;   // WR-01: 마지막 Index 매칭 0건 = false-PASS 위험 → F 강제
            bool bCycleFail = m_bCycleHasNG || m_bCycleDatumFailed || bEmptyLastScope;
            if (bCycleFail)
            {
                packet.Result = EVisionResultType.NG;   // 직렬화 'F'
            }
            else
            {
                packet.Result = EVisionResultType.OK;   // 직렬화 'P'
            }
        }

        //260623 hbk Phase 49: v1.0 경로 영속화+Enqueue — 기존 v2.6 try/catch CycleResultSerializer 블록 동일 이식.
        //  매 Index 스냅샷 저장(마지막 Index 가 덮음) — 직렬화 예외 격리로 Enqueue 차단 방지(기존 패턴 보존).
        private void PersistAndEnqueueV1(InspectionRecipeManager recipeManager, TestResultPacket packet)
        {
            try
            {
                int nIndexNumber = -1;
                bool bHasRequest = RequestPacket != null;
                if (bHasRequest)
                {
                    nIndexNumber = RequestPacket.IndexNumber;
                }
                var cycleDto = CycleResultSerializer.BuildDto(
                    recipeManager,
                    packet.Result,
                    System.DateTime.Now,
                    SystemHandler.Handle.Setting.CurrentRecipeName,
                    Name,
                    nIndexNumber);
                CycleResultSerializer.SaveAsync(cycleDto);
            }
            catch (Exception ex)
            {
                try { Logging.PrintErrLog((int)ELogType.Error, "[V1Cycle] cycle 직렬화 실패(무시): " + ex.Message); } catch { }
            }
            ResponseQueue.Enqueue(packet);
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

        // Fixture API — DisplayName 폴백
        public string GetDisplayName() {
            if (string.IsNullOrEmpty(DisplayName)) return Name;
            return DisplayName;
        }

        // Multi-Datum add
        public DatumConfig AddDatum(string name = null) {
            string datumName;
            if (string.IsNullOrEmpty(name)) datumName = $"Datum_{DatumConfigs.Count + 1}";
            else datumName = name;
            var datum = new DatumConfig(this);
            datum.DatumName = datumName;
            DatumConfigs.Add(datum);
            return datum;
        }

        public bool RemoveDatum(int index) {
            if (index < 0 || index >= DatumConfigs.Count) return false;
            DatumConfigs.RemoveAt(index);
            return true;
        }

        //260710 hbk 죽은 코드 제거: 미사용 Datum phase 실행 오버로드 2종(1-image/2-image) 호출부 0건 확인 후 삭제 (quick 260710-di7)

        // DatumPhase loop 시작 전 1회 호출 (per-datum 누적 전 초기화)
        public void ClearDatumTransforms() {
            _datumTransforms.Clear();
            _failedDatums.Clear(); // _datumTransforms 와 동일 lifecycle
            //260618 hbk Phase 54 ALIGN-01 align 실패 set 도 동일 lifecycle 리셋 (D-10)
            _alignFailedDatums.Clear();
            // RuntimeDetectFailed 도 일괄 리셋 (이전 사이클 잔여 신호 제거)
            foreach (var d in DatumConfigs)
            {
                if (d != null) d.RuntimeDetectFailed = false;
            }
            //260619 hbk Phase 57 #6 leveling 제거 — ResetLeveling() 호출 폐기 (ALIGN 대체, D-12/D-13)
        }

        // 검출 실패 datum 기록. Action_FAIMeasurement.EStep.DatumPhase 실패 분기에서 호출.
        //  _failedDatums 단일 set 에 idempotent add.
        public void MarkDatumFailed(string datumName)
        {
            if (!string.IsNullOrEmpty(datumName)) _failedDatums.Add(datumName);
        }

        // per-FAI gate 조회. Measurement.DatumRef 가 실패 datum 이름과 일치하면 true.
        //  빈 DatumRef = 무보정 의도이므로 게이트 무관 (false 반환 — TryGetDatumTransform identity fallback 경로로 진행).
        public bool IsDatumFailed(string datumRef)
        {
            return !string.IsNullOrEmpty(datumRef) && _failedDatums.Contains(datumRef);
        }

        //260716 hbk DatumRef 참조 불일치 감지 — datumRef 가 비어있지 않은데 현재 DatumConfigs 어디에도 그 이름이 없는 경우 true.
        //  배경: DatumPhase 루프는 실존 DatumConfigs 만 순회하므로, 오타/개명/삭제된 이름은 애초에 검출 시도조차 안 되고
        //  _failedDatums 에도 안 들어간다 → IsDatumFailed 게이트를 그대로 우회하고 ResolveDatumTransform 의 identity 폴백으로
        //  '무보정 측정'이 정상 측정처럼 PASS/NG 를 내던 결함(조용한 오검). Datum 개명/삭제 시 참조 갱신 로직이 없어
        //  정상적인 레시피 편집만으로도 발생 가능. 빈 DatumRef(무보정 의도)는 false — 기존 identity 폴백 경로 유지.
        public bool IsDatumRefUnresolvable(string datumRef)
        {
            if (string.IsNullOrEmpty(datumRef)) return false; // 무보정 의도 — 불일치 아님
            if (DatumConfigs == null) return true;
            foreach (var d in DatumConfigs)
            {
                if (d != null && d.DatumName == datumRef) return false; // 실존
            }
            return true; // 이름은 지정됐는데 대응 DatumConfig 없음
        }

        //260618 hbk Phase 54 ALIGN-01 패턴매칭 실패 datum 기록 (D-10 lenient — 측정 NG(ALIGN_FAIL) 강제, abort 안 함).
        //  _failedDatums 에도 add 하여 기존 IsDatumFailed 게이트가 NG 를 강제하도록 한다.
        public void MarkAlignFailed(string datumName)
        {
            if (!string.IsNullOrEmpty(datumName))
            {
                _alignFailedDatums.Add(datumName);
                MarkDatumFailed(datumName); // 측정 NG 강제 위해 기존 게이트 set 에도 add
            }
        }

        //260618 hbk Phase 54 ALIGN-01 align 실패 datum 여부 조회 — 게이트가 ALIGN_FAIL vs DATUM_FAIL 표기 구분에 사용 (D-10).
        public bool IsAlignFailed(string datumRef)
        {
            return !string.IsNullOrEmpty(datumRef) && _alignFailedDatums.Contains(datumRef);
        }

        //260618 hbk Phase 54 ALIGN-01 datum 모델 경로 단일 소스 (D-07) — 54-04(런타임 load)·54-05(티칭 save) 공유.
        //  키 도출 로직 단일화 → 경로 불일치 구조적 차단. SourceShotName → ShotConfig.OwnerSequenceName 역추적 (InspectionListView.ResolveDatumCameraParam 선례).
        public static string ResolveDatumModelPath(DatumConfig datum)
        {
            if (datum == null) return null;
            string recipeName = SystemHandler.Handle.Setting.CurrentRecipeName;
            if (recipeName == null) recipeName = "";
            // datum.SourceShotName 으로 ShotConfig 역추적 → OwnerSequenceName (InspectionListView.ResolveDatumCameraParam 선례)
            string seqName = "TOP"; // 미매칭/빈값 폴백 (ShotConfig 정책 일치)
            var shots = SystemHandler.Handle.Sequences.RecipeManager.Shots;
            if (shots != null && shots.Count > 0)
            {
                ShotConfig matched = null;
                if (!string.IsNullOrEmpty(datum.SourceShotName))
                {
                    foreach (var s in shots) { if (s != null && s.ShotName == datum.SourceShotName) { matched = s; break; } }
                }
                if (matched == null) matched = shots[0];
                if (matched != null && !string.IsNullOrEmpty(matched.OwnerSequenceName)) seqName = matched.OwnerSequenceName;
            }
            string actName = "Datum"; // 결정적 상수 — propertyName=DatumName 이 유일성 보장
            string propertyName = datum.DatumName;
            if (propertyName == null) propertyName = "";
            return SystemHandler.Handle.Recipes.GetPatternModelFilePath(recipeName, seqName, actName, propertyName, datum.PatternEngine);
        }

        //260619 hbk Phase 55 ALIGN-02 패턴2 모델 경로 — ResolveDatumModelPath 미러, propertyName 에 "_2" 접미사 → 별도 .shm.
        //  (working ResolveDatumModelPath 무변경: 경로불일치=ALIGN_FAIL 위험이라 리팩토링보다 복제 선택, 안전 우선.)
        public static string ResolveDatumModelPath2(DatumConfig datum)
        {
            if (datum == null) return null;
            string recipeName = SystemHandler.Handle.Setting.CurrentRecipeName;
            if (recipeName == null) recipeName = "";
            string seqName = "TOP";
            var shots = SystemHandler.Handle.Sequences.RecipeManager.Shots;
            if (shots != null && shots.Count > 0)
            {
                ShotConfig matched = null;
                if (!string.IsNullOrEmpty(datum.SourceShotName))
                {
                    foreach (var s in shots) { if (s != null && s.ShotName == datum.SourceShotName) { matched = s; break; } }
                }
                if (matched == null) matched = shots[0];
                if (matched != null && !string.IsNullOrEmpty(matched.OwnerSequenceName)) seqName = matched.OwnerSequenceName;
            }
            string actName = "Datum";
            string propertyName = (datum.DatumName ?? "") + "_2";
            return SystemHandler.Handle.Recipes.GetPatternModelFilePath(recipeName, seqName, actName, propertyName, datum.PatternEngine);
        }

        //260618 hbk Phase 54 ALIGN-01 패턴매칭 보정 합성 (D-02 ①②③④).
        //  refImage = 보정 전 원본 grab 이미지(D-05). modelPath = 호출부가 ResolveDatumModelPath 로 산출 전달.
        //  ① 원본 매칭 → curRow,curCol  ② (curRow-RefMatchRow,curCol-RefMatchCol) 이동 line-fit θ  ③ rigid  ④ _datumTransforms 합성.
        //  합성 순서: HomMat2dCompose(alignRigid, existing) = "기존 검출 transform 을 먼저 적용 후 align 보정" (right-to-left 적용 규약).
        //   ※TryComposeAlign 은 DatumPhase 에서 TryRunSingleDatum(검출) 호출 없이 align 단독 경로로 호출됨(Task 2 ②) → 통상 existing 없음 → alignRigid 단독 저장. existing 존재 시(혼용)만 1회 합성 — align 1회 적용 보장(W4).
        //  실패(매칭 score 미달/모델 로드 실패/rigid 실패) → false (호출부가 MarkAlignFailed, lenient D-10). _datumTransforms 미변경.
        public bool TryComposeAlign(DatumConfig datum, HImage refImage, string modelPath, out string error)
        {
            //260619 hbk Phase 57 #4 DualImage align — 기존 단일이미지 호출처 무변경 보장 위해 5-arg 오버로드에 위임(refImageVertical=null).
            return TryComposeAlign(datum, refImage, null, modelPath, out error);
        }

        //260619 hbk Phase 57 #4 DualImage align — 세로축 이미지(refImageVertical) 를 받는 5-arg 오버로드.
        //  refImageVertical != null + DualImage datum 이면 ④단계가 2-image TryFindDatum 으로 분기하여 가로/세로 ROI 에 동일 alignRigid 적용(D-01).
        //  패턴 매칭(① TryFindPose)은 가로축(refImage) 에서만 수행 — 세로엔 패턴 모델 없음(D-04).
        public bool TryComposeAlign(DatumConfig datum, HImage refImage, HImage refImageVertical, string modelPath, out string error)
        {
            error = null;
            if (datum == null) { error = "datum null"; return false; }
            if (refImage == null) { error = "refImage null"; return false; }
            //260618 hbk Phase 54 ALIGN-01 hotfix: align-enabled Datum 은 검출 경로(EnsurePerRoiDefaults 호출처)를 건너뛰므로
            //  여기서 명시 호출하지 않으면 PatternSearchMarginPx/PatternMinScore 가 sentinel 0 → 검색영역 ROI 에 밀착 + minScore 0
            //  → 회전/이동으로 패턴이 이동하면 매칭 실패(ALIGN_FAIL). 멱등 폴백이므로 매 호출 안전.
            datum.EnsurePerRoiDefaults();
            var svc = new PatternMatchService();
            double curRow, curCol, curAngleDeg, curScore;
            // ① 매칭 (보정 전 원본) → x,y
            if (!svc.TryFindPose(refImage, datum.PatternEngine, modelPath,
                    datum.PatternRoi_Row, datum.PatternRoi_Col, datum.PatternRoi_Length1, datum.PatternRoi_Length2,
                    //260618 hbk Phase 54 ALIGN-01 hotfix(CO-54-02): downsample 비활성(1.0). shape/ncc 모델은 스케일 불변이 아니라
                    //  검색 이미지를 0.5배로 줄이면 패턴이 50% 크기 → find_shape_model "no match"(티칭 score 1.0 인데 런타임 0건).
                    //  속도 다운샘플은 모델 자체 피라미드(NumLevels)가 담당 — 검색 이미지 물리 축소는 금지.
                    datum.PatternSearchMarginPx, datum.PatternMinScore, /*downsampleFactor*/ 1.0,
                    out curRow, out curCol, out curAngleDeg, out curScore, out error))
            {
                return false; // ALIGN_FAIL — 호출부 MarkAlignFailed
            }
            // ② tilt(θ): 패턴 매칭 각도 사용. find_shape_model 이 부품 회전을 직접·강건하게 반환하고 부호도 일관.
            //   (직선 ROI θ 는 atan2 규약이라 패턴과 부호가 반대 + 멀리 있어 자주 실패 → 미사용. CO-54-04)
            double dRow = curRow - datum.RefMatchRow;
            double dCol = curCol - datum.RefMatchCol;
            double thetaRad = (curAngleDeg - datum.RefMatchAngleDeg) * System.Math.PI / 180.0;
            //260618 hbk Phase 54 ALIGN-01 진단 로그 — 매칭/θ 수치 확인용 (CO-54-04)
            Logging.PrintLog((int)ELogType.Trace, "[ALIGN] " + (datum.DatumName ?? "")
                + " cur=(" + curRow.ToString("F1") + "," + curCol.ToString("F1") + ")"
                + " d=(" + dRow.ToString("F1") + "," + dCol.ToString("F1") + ")"
                + " patAngDeg=" + curAngleDeg.ToString("F3") + " refPatAngDeg=" + datum.RefMatchAngleDeg.ToString("F3")
                + " thetaDeg=" + (thetaRad * 180.0 / System.Math.PI).ToString("F3") + " src=pattern"
                + " score=" + curScore.ToString("F3") + " angleExtentDeg=" + datum.PatternAngleExtentDeg.ToString("F1"));
            // ②-2 Phase 55 ALIGN-02 — 패턴2 설정 시 θ 를 "두 점 baseline 각" 으로 교체(단일 패턴 각도 정밀도 한계 보완).
            //  각 패턴 자체 회전각 미사용 — 두 매칭 중심점만 사용. baseline 각 = atan2(-dRow, dCol) (CCW-visual, hom_mat2d_rotate 규약 일치). 부호 SIMUL 검증.
            //  점2 미설정(Length=0) 또는 매칭 실패 → 단일 패턴 θ 유지(폴백) + 경고.
            if (datum.PatternRoi2_Length1 > 0.0 && datum.PatternRoi2_Length2 > 0.0)
            {
                string modelPath2 = ResolveDatumModelPath2(datum);
                double cur2Row, cur2Col, cur2AngleDeg, cur2Score; string err2;
                if (svc.TryFindPose(refImage, datum.PatternEngine, modelPath2,
                        datum.PatternRoi2_Row, datum.PatternRoi2_Col, datum.PatternRoi2_Length1, datum.PatternRoi2_Length2,
                        datum.PatternSearchMarginPx, datum.PatternMinScore, /*downsampleFactor*/ 1.0,
                        out cur2Row, out cur2Col, out cur2AngleDeg, out cur2Score, out err2))
                {
                    double refBaseline = System.Math.Atan2(-(datum.RefMatch2Row - datum.RefMatchRow), datum.RefMatch2Col - datum.RefMatchCol);
                    double curBaseline = System.Math.Atan2(-(cur2Row - curRow), cur2Col - curCol);
                    thetaRad = curBaseline - refBaseline;
                    Logging.PrintLog((int)ELogType.Trace, "[ALIGN2] " + (datum.DatumName ?? "")
                        + " p2cur=(" + cur2Row.ToString("F1") + "," + cur2Col.ToString("F1") + ")"
                        + " refBaseDeg=" + (refBaseline * 180.0 / System.Math.PI).ToString("F3")
                        + " curBaseDeg=" + (curBaseline * 180.0 / System.Math.PI).ToString("F3")
                        + " thetaDeg=" + (thetaRad * 180.0 / System.Math.PI).ToString("F3")
                        + " score2=" + cur2Score.ToString("F3") + " (baseline θ)");
                }
                else
                {
                    Logging.PrintLog((int)ELogType.Error, "[ALIGN2] " + (datum.DatumName ?? "")
                        + " 패턴2 매칭 실패 → 단일 패턴 θ 폴백: " + (err2 ?? ""));
                }
            }
            // ③ transform 산출 (사용자 레시피): identity → rotate(θ, RefMatch 중심) → translate(dRow,dCol).
            //  회전 중심은 무관(rotate 후 translate 로 x,y 보정) — RefMatch 위치 사용. θ 부호 = 측정−Ref.
            HTuple alignRigid;
            try
            {
                HTuple hId, hRot;
                HOperatorSet.HomMat2dIdentity(out hId);
                HOperatorSet.HomMat2dRotate(hId, thetaRad, datum.RefMatchRow, datum.RefMatchCol, out hRot);
                HOperatorSet.HomMat2dTranslate(hRot, dRow, dCol, out alignRigid);
            }
            catch (Exception exr)
            {
                error = exr.Message;
                return false;
            }
            // ④ 보정 transform(T)을 datum 검출 ROI 에 적용하여 datum 을 검출(생성)한다 (사용자 설계, CO-54-04).
            //  T(rotate θ+translate x,y)로 검출 ROI(Circle/Horizontal/Line)가 틀어진 부품의 실제 datum 에지를 덮음
            //  → 진짜 DetectedOrigin/angle + datum transform 생성. 측정은 이 datum transform 을 사용 → nominal 불변.
            var detectSvc = new DatumFindingService();
            detectSvc.AlignPreTransform = alignRigid; // 단일 인스턴스 1회 set → 두 이미지 검출(Vertical=TryFindLine, Horizontal=TryExtractEdgePoints) 모두 적용 (Task 1 으로 TryFindLine 도 소비)
            HTuple datumTransform;
            string detErr;
            //260619 hbk Phase 57 #4 DualImage align — DualImage datum 이면 2-image 오버로드로 분기(가로/세로 ROI 동일 transform, D-01). 그 외엔 기존 1-image.
            bool detectOk;
            if (datum.AlgorithmTypeEnum == EDatumAlgorithm.VerticalTwoHorizontalDualImage && refImageVertical != null)
            {
                detectOk = detectSvc.TryFindDatum(refImage, refImageVertical, datum, out datumTransform, out detErr);
            }
            else
            {
                detectOk = detectSvc.TryFindDatum(refImage, datum, out datumTransform, out detErr);
            }
            if (!detectOk)
            {
                error = "datum 검출 실패(패턴 보정 후): " + (detErr ?? "");
                return false; // ALIGN_FAIL — 호출부 MarkAlignFailed
            }
            datum.CurrentTransform = datumTransform;
            string datumKey = datum.DatumName;
            if (datumKey == null) datumKey = "";
            //260618 hbk Phase 54 ALIGN-01 carry-over#1 확증로그: datum 검출각(수평 결합선) 회전분 vs 패턴 θ.
            //  strip θ회전 적용 후 datumDetectRotDeg 가 patternThetaDeg 로 수렴(편차~0)하면 datum 검출각 정확 = 먼 측정점 정상.
            //  축정렬 strip 가설은 0.1-0.2° 편차. UAT 1회로 메커니즘 확증.
            double datumDetectRotDeg = datum.DetectedAngleDeg - (datum.RefAngleRad * 180.0 / System.Math.PI);
            Logging.PrintLog((int)ELogType.Trace, "[ALIGN] " + datumKey
                + " datumDetectAngleDeg=" + datum.DetectedAngleDeg.ToString("F3")
                + " datumDetectRotDeg=" + datumDetectRotDeg.ToString("F3")
                + " vs patternThetaDeg=" + (thetaRad * 180.0 / System.Math.PI).ToString("F3")
                + " (strip θ-rot applied)");
            //260619 hbk Phase 54 ALIGN-01 — 측정 transform 을 검출-datum 대신 패턴 pose(alignRigid)로 전환(단일 패턴 검증 단계).
            //  검출-datum transform 은 tilt 서 패턴(정답) 대비 ~130px 어긋남(먼 측정 ROI lever-arm) → EDGE_FAIL "—"(quick 260618-o2m [ALIGN-CHK] 확인).
            //  패턴 pose 는 부품 회전/이동을 강건히 반영 → 먼 측정점 ROI 정상 위치. datum 검출 origin(DetectedOrigin*)은 거리 기준으로 계속 사용(nominal 보존).
            //  2-패턴 baseline(양 대각 끝 ROI 2개 → 두 점 각도) 정밀화는 후속 phase. 직선 ROI(AlignLineRoi) 각도 경로는 폐기 확정(CO-54-04).
            _datumTransforms[datumKey] = alignRigid; // 측정 ROI 위치 = 패턴 pose (검출-datum 130px 오차 회피)
            //260619 hbk Phase 56 Wave 2 — 보정 ROI 표시도 측정과 동일 transform(alignRigid) 사용해야 일치.
            //  line 542 는 검출-datum transform(datumTransform)을 CurrentTransform 에 넣지만, 측정은 alignRigid 사용(먼 ROI 130px 오차 회피).
            //  CurrentTransform 소비처 = 보정 ROI 표시 전용(MainView)뿐 → alignRigid 로 덮어써 측정과 박스 위치 일치시킴.
            datum.CurrentTransform = alignRigid;
            datum.LastFindSucceeded = true;
            return true;
        }

        // datum 1개 검출 후 _datumTransforms 누적 저장 (Clear 안 함). 실패 시 false 반환하되 호출부는 abort 안 함 (lenient).
        public bool TryRunSingleDatum(DatumConfig datum, HImage imageH, HImage imageV, out string error) {
            error = null;
            if (datum == null) { error = "datum is null"; return false; }
            var service = new DatumFindingService();
            HTuple transform;
            string datumError;
            bool ok;
            if (datum.AlgorithmTypeEnum == EDatumAlgorithm.VerticalTwoHorizontalDualImage) { // datum별 DualImage 판단 (mixed 허용)
                if (imageH == null || imageV == null) { error = "DualImage 이미지 null"; datum.LastFindSucceeded = false; return false; }
                ok = service.TryFindDatum(imageH, imageV, datum, out transform, out datumError);
            } else { // 1-image datum
                if (imageH == null) { error = "이미지 null"; datum.LastFindSucceeded = false; return false; }
                ok = service.TryFindDatum(imageH, datum, out transform, out datumError);
            }
            if (!ok) { datum.LastFindSucceeded = false; error = datumError; return false; }
            //260716 hbk 미교시 Datum 런타임 사용 감지 — DatumFindingService.TryFindDatum 은 IsConfigured=false 면
            //  identity 를 세팅한 채 true 를 반환한다(D-08 pass-through: FAI ROI 사전 배치 편의를 위한 의도된 설계).
            //  문제는 그 결과가 '검출 성공'과 완전히 동일하게 취급돼(로그 없음, DETECT FAIL 배지 조건도 IsConfigured=true 를
            //  요구해 절대 안 뜸) 교시를 깜빡한 Datum 이 영구히 '무보정 측정'으로 조용히 퇴화한다는 것. pass-through 자체는
            //  유지하되(회귀 0), 런타임 검사에서 실제로 쓰이면 로그 + RuntimeDetectFailed(티칭 무관 라벨 신호)로 드러낸다.
            if (!datum.IsConfigured)
            {
                string unconfName = datum.DatumName;
                if (unconfName == null) unconfName = "";
                datum.RuntimeDetectFailed = true; // 티칭 여부 무관 라벨 신호 → 트리 배지/오버레이에 노출
                Logging.PrintLog((int)ELogType.Error, "[Datum] '" + unconfName + "' 미교시(IsConfigured=false) 상태로 검사에 사용됨 — 무보정(identity) 적용. 티칭 필요.");
            }
            datum.LastFindSucceeded = true;
            datum.CurrentTransform = transform;
            string datumKey = datum.DatumName;
            if (datumKey == null) datumKey = "";
            _datumTransforms[datumKey] = transform; // 누적 저장
            return true;
        }

        // DatumRef → transform 조회
        public bool TryGetDatumTransform(string datumRef, out HTuple transform) {
            if (string.IsNullOrEmpty(datumRef)) {
                HOperatorSet.HomMat2dIdentity(out transform);
                return true;
            }
            return _datumTransforms.TryGetValue(datumRef, out transform);
        }

        //260619 hbk Phase 57 #6 leveling 제거 — TryComputeLevelingAngle 메서드 폐기 (ALIGN 위치/tilt 보정으로 대체, D-12/D-13)
    }
}
