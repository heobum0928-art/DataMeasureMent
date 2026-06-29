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

        //260618 hbk Phase 54 ALIGN-01 패턴매칭(align) 실패 datum set — 검출 실패(_failedDatums)와 구분하여 측정 게이트가 LastSkipReason=ALIGN_FAIL 표기 (D-10).
        private readonly HashSet<string> _alignFailedDatums = new HashSet<string>();

        //260623 hbk Phase 49 PROTO-03/05 (D-02): 멀티샷 사이클 누적 상태 — 신규 클래스 미도입, _failedDatums 와 동일 lifecycle 멤버.
        //  ClearDatumTransforms() 와 별도로 Index 0 수신 시 ResetCycleState() 로 리셋(D-08).
        //  49-02 가 AddResponseV1Cycle 에서 read 연결 → CS0414(미사용) 해소, #pragma 제거함.
        private const int DATUM_Z_INDEX = 0;         //260623 hbk Phase 49 (D-08): Index 0 = Datum 샷 (매직넘버 상수화, D-10)
        private bool m_bCycleHasNG = false;          // 사이클 중 NG 1건이라도 발견 → 마지막 Index 종합 F (D-02)
        private bool m_bCycleDatumFailed = false;    // Index 0 Datum 검출 실패 → 즉시 F 마킹 (D-04/D-05)
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

        //260625 hbk Phase 64 LIGHT-01 (D-10): ShotConfig 4종 조명 → LightHandler 5종 그룹 적용.
        //  RingLight → RING / BackLight → BACK / CoaxLight → ALIGN_COAX / SideLight → BAR / Ring7Light → RING7   //260626 hbk Phase 66 Ring7 추가
        //  Enabled=true: SetOnOff(true) 먼저, 이후 SetLevel (ApplyLight 순서 동일).
        //  Enabled=false: SetOnOff(false) 만 호출.
        private void ApplyShotLightsInternal(ShotConfig shot)
        {
            if (shot.RingLight_Enabled)
            {
                LightHandler.Handle.SetOnOff(LightHandler.LIGHT_RING, true);
                LightHandler.Handle.SetLevel(LightHandler.LIGHT_RING, shot.RingLight_Brightness);
            }
            else
            {
                LightHandler.Handle.SetOnOff(LightHandler.LIGHT_RING, false);
            }

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

            if (shot.SideLight_Enabled)
            {
                LightHandler.Handle.SetOnOff(LightHandler.LIGHT_BAR, true);
                LightHandler.Handle.SetLevel(LightHandler.LIGHT_BAR, shot.SideLight_Brightness);
            }
            else
            {
                LightHandler.Handle.SetOnOff(LightHandler.LIGHT_BAR, false);
            }

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

        //260623 hbk Phase 49 PROTO-05 (D-08): Index 0(Datum 샷) 수신 = 사이클 시작 → 누적 상태 클린 슬레이트.
        //  비정상 종료(중단 F) 후에도 다음 사이클 시작이 항상 깨끗 — 마지막 Index 후 리셋(누락 위험) 미채택.
        private void ResetCycleState()
        {
            m_bCycleHasNG = false;
            m_bCycleDatumFailed = false;
            m_nCurrentZIndex = 0;
            m_nLastZIndex = 0;     // 호출 후 반드시 m_nLastZIndex = ComputeLastZIndex(recipeManager) 재산출 필요 — 호출부 의무 //260623 hbk
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
            pMyContext.ResultInfo = packet.Result;
            return packet;
        }

        //260623 hbk Phase 49 (D-01): nZIndex 매칭 Shot 의 FAI 만 packet.FAIResults 에 집계. 매칭 Shot 수 반환.
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
                bool bInScope = bOwnedByThisSeq && bZMatch;
                if (!bInScope)
                {
                    continue;
                }
                nMatchedShots = nMatchedShots + 1;
                foreach (var fai in shot.FAIList)
                {
                    AddFaiResult(packet, fai);
                }
            }
            return nMatchedShots;
        }

        //260629 hbk FAI 단위 1항목 → 측정 단위 N항목 전환. P2 등 다측정 불량값 은폐 제거 (ETI-RESULT-PER-MEASUREMENT).
        private void AddFaiResult(TestResultPacket packet, FAIConfig fai)
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

        //260629 hbk 측정 단위 3-state 분류 — datum/align-skip('N')·측정 NG('F')는 m_bCycleHasNG 누적. 그 외 OK('P'). (ClassifyFai 측정 단위 복제)
        private EVisionResultType ClassifyMeasurement(MeasurementBase meas)
        {
            string szSkip = meas.LastSkipReason; //260629 hbk 측정 단위 skip 사유
            bool bDatumSkipped = (szSkip == "DATUM_FAIL") || (szSkip == "ALIGN_FAIL"); //260629 hbk datum/align 검출 실패 skip = 'N'
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

        //260623 hbk Phase 49 (D-02): FAI 3-state 분류 — 검출실패('N')/NG('F')는 m_bCycleHasNG 누적. 그 외 OK('P').
        private EVisionResultType ClassifyFai(FAIConfig fai)
        {
            bool bDatumSkipped = fai.WasDatumSkipped;
            if (bDatumSkipped)
            {
                m_bCycleHasNG = true;   // 검출실패도 사이클 NG 로 누적
                return EVisionResultType.NotExist;
            }
            bool bNotPass = !fai.IsPass;
            if (bNotPass)
            {
                m_bCycleHasNG = true;
                return EVisionResultType.NG;
            }
            return EVisionResultType.OK;
        }

        //260623 hbk Phase 49 BLOCKER 1 (D-01 정합): ZIndex 매칭 0건(빈 결과 + 매칭 Shot 0)이면 PrintErrLog 경고.
        //  레시피 ZIndex 미설정(전부 0) + 측정 Index 수신 = 운용 오류. 폴백(전체 재검사) 금지 — 경고만.
        //  WR-01 fix: 중간 Index 면 빈 B 유지, 마지막 Index 면 ApplyCycleJudgement 가 F 강제(false-PASS 차단).
        private void WarnIfEmptyScope(TestResultPacket packet, int nMatchedShots, int nZIndex)
        {
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

        // Datum phase 실행 — 모든 DatumConfig 순회
        public bool TryRunDatumPhase(HImage image, out string error) {
            error = null;
            _datumTransforms.Clear();

            if (DatumConfigs.Count == 0) {
                return true; // Datum 미설정 Fixture는 무보정 pass-through
            }

            if (image == null) {
                error = "image is null";
                return false;
            }

            var service = new DatumFindingService();
            foreach (var datum in DatumConfigs) {
                HTuple transform;
                string datumError;
                if (!service.TryFindDatum(image, datum, out transform, out datumError)) { // datum 실패 시 abort 안 함, 성공만 저장
                    datum.LastFindSucceeded = false;
                    string datumName = datum.DatumName;
                    if (datumName == null) datumName = "";
                    string derr = datumError;
                    if (derr == null) derr = "";
                    Logging.PrintLog((int)ELogType.Error, "[DatumPhase] Datum '" + datumName + "' find 실패 (skip): " + derr);
                    continue; // 다음 datum 계속, 이 datum 은 _datumTransforms 미저장
                }
                datum.LastFindSucceeded = true;
                datum.CurrentTransform = transform;
                string datumKey = datum.DatumName;
                if (datumKey == null) datumKey = "";
                _datumTransforms[datumKey] = transform;
            }
            return true;
        }
        // VerticalTwoHorizontalDualImage 전용 2-image 오버로드. image1=가로축, image2=세로축.
        //  _datumTransforms 채움 규약은 1-image 오버로드와 동일.
        public bool TryRunDatumPhase(HImage image1, HImage image2, out string error) {
            error = null;
            _datumTransforms.Clear();
            if (DatumConfigs.Count == 0) return true;
            if (image1 == null || image2 == null) { error = "image1 or image2 is null"; return false; }
            var service = new DatumFindingService();
            foreach (var datum in DatumConfigs) {
                HTuple transform; string datumError; bool ok; // per-datum DualImage 판단 유지 (DatumConfigs[0] 단일 판단 아님)
                if (datum.AlgorithmTypeEnum == EDatumAlgorithm.VerticalTwoHorizontalDualImage) ok = service.TryFindDatum(image1, image2, datum, out transform, out datumError);
                else ok = service.TryFindDatum(image1, datum, out transform, out datumError);
                if (!ok) {
                    datum.LastFindSucceeded = false;
                    string datumName = datum.DatumName;
                    if (datumName == null) datumName = "";
                    string derr = datumError;
                    if (derr == null) derr = "";
                    Logging.PrintLog((int)ELogType.Error, "[DatumPhase] Datum '" + datumName + "' find 실패 (skip): " + derr);
                    continue; // abort 제거, skip
                }
                datum.LastFindSucceeded = true;
                datum.CurrentTransform = transform;
                string datumKey = datum.DatumName;
                if (datumKey == null) datumKey = "";
                _datumTransforms[datumKey] = transform;
            }
            return true;
        }

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
