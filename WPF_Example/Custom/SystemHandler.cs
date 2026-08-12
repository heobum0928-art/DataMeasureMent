using HalconDotNet; //260626 hbk HImage 참조 (RunBottomAlign 로컬 변수)
using ReringProject.Device; //260626 hbk Phase 66 D-06 — LightHandler.LIGHT_ALIGN_COAX 참조 (ApplyCoaxLightForSlot)
using ReringProject.Setting;
using ReringProject.Network;
using ReringProject.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using ReringProject.Sequence;
using System.Diagnostics;
using TeachDiag   = ReringProject.Halcon.Algorithms.TeachDiagnostics;   //quick-260812: 표시 전용 헬퍼(별칭 = 이름충돌 회피)
using ETeachGrade = ReringProject.Halcon.Algorithms.ETeachGrade;

namespace ReringProject {
    public sealed partial class SystemHandler {
        //260626 hbk z_index=$PREP 분리: $PREP 수신 시 저장, $TEST 라우팅 시 주입. volatile=MainRun/시퀀스 스레드 안전.
        private volatile int _lastPrepZIndex = 0;

        //project 별, sequence 정의
        private void MainRun() {
            //send test response message
            for (int i = 0; i < Sequences.Count; i++) {
                TestResultPacket response = Sequences[i].PopResponse();
                if (response == null) continue;
                //260810 hbk manual-inspect-null-target-crash: 수동(메뉴얼) 트리거(DebugManualZTrigger)는
                //  실제 TCP 클라이언트가 없어 RequestPacket.Sender가 null로 남고, 그게 Target으로 그대로
                //  echo되어 여기까지 도달한다. Target이 비어있으면 보낼 대상이 없는 정상 케이스이므로
                //  TCP 전송만 스킵한다(응답 생성/영속화는 이미 끝난 뒤라 그대로 유지됨) — 크래시 방지.
                if (string.IsNullOrEmpty(response.Target)) {
                    Logging.PrintLog((int)ELogType.Trace,
                        "[MainRun] TestResultPacket.Target empty — TCP 전송 스킵(수동 트리거 등 응답 대상 없음). site={0}",
                        response.Site);
                    continue;
                }
                if (!Server.SendPacket(response.Target, response)) {
                    //occurs error
                }
            }

            //recv message
            for(int i = 0; i < Server.GetConnectedClientCount(); i++) {
                if(Server.GetRecvPacket(i, out VisionRequestPacket packet) == false) {
                    //no received message
                    continue;
                }
                if(packet == null) {
                    continue;
                }
                //메시지를 받음 (operator 모드로 변경함)
                VisionResponsePacket responsePacket = null;
                switch (packet.RequestType) {
                    case VisionRequestType.Light:
                        responsePacket = ProcessLightSet(packet.AsLight());
                        break;
                    case VisionRequestType.RecipeChange:
                        responsePacket = ProcessRecipeChange(packet.AsRecipeChange());
                        break;
                    case VisionRequestType.RecipeGet:
                        responsePacket = ProcessRecipeGet(packet.AsRecipeGet());
                        break;
                    case VisionRequestType.SiteStatus:
                        responsePacket = ProcessSiteStatus(packet.AsSiteStatus());
                        break;
                    case VisionRequestType.Test:
                        if (Setting.AutoLogoutWhenRecvTest && Login.IsLogin) { Login.LogOut(); }

                        if (!ProcessTest(packet.AsTest())) {
                            Logging.PrintLog((int)ELogType.Error, "Client {0} : Fail to Start Sequence. sender:{1}, identifier:{2}", i, packet.Sender, packet.Identifier);
                            //send fail message
                            responsePacket = SendTestError(packet.AsTest());
                        }
                        break;

                    case VisionRequestType.AlignTest:                                  //260624 hbk Phase 63 AV-09
                        responsePacket = ProcessAlignTest(packet.AsAlignTest());
                        break;
                    case VisionRequestType.AlignCalib:                                 //260624 hbk Phase 63 AV-09
                        responsePacket = ProcessAlignCalib(packet.AsAlignCalib());
                        break;
                    case VisionRequestType.Prep:                                   //260625 hbk Phase 64 LIGHT-01
                        responsePacket = ProcessPrep(packet.AsPrep());
                        break;
                    case VisionRequestType.Alive:                                  //260625 hbk v3.0
                        responsePacket = ProcessAlive(packet.AsAlive());
                        break;
                    case VisionRequestType.Reset:                                  //260807 hbk quick-260807-lh7
                        responsePacket = ProcessReset(packet.AsReset());
                        break;
                    case VisionRequestType.Unknown:
                        //occurs error
                        break;
                }

                //send response
                if (responsePacket == null) {
                    //test 메시지는 곧바로 response를 하지 않음
                }
                else if (!Server.SendPacket(i, responsePacket)) {
                    Logging.PrintLog((int)ELogType.Error, "Client {0} : Fail to Send packet. packetType :{1}", i, responsePacket.ResponseType.ToString());
                }
            }
        }

        //260626 hbk z_index=$PREP 분리: $LIGHT 응답 제거 — 조명 제어는 유지, 응답 null 반환(fire-and-forget).
        //  핸들러는 고정 딜레이 후 $PREP 전송. $PREP_ACK 가 "준비 완료" 확인 역할.
        private LightResultPacket ProcessLightSet(LightPacket packet) {
            if (Sequences[packet.Identifier] != null) {
                SequenceBase seq = Sequences[packet.Identifier];
                if (seq != null) {
                    if (packet.TestType == 0) //off
                    {
                        Lights.SetOnOff(packet.Identifier, packet.On);
                        return null;
                    }
                    int actIndex = seq.GetIndexOf(packet.Identifier2);
                    ActionBase act = seq.GetAction(actIndex);
                    if (act != null) {
                        if (act.Param is CameraSlaveParam) {
                            CameraSlaveParam camParam = act.Param as CameraSlaveParam;
                            Lights.SetLevel(camParam.LightGroupName, camParam.LightLevel);
                            Lights.SetOnOff(camParam.LightGroupName, packet.On);
                        }
                    }
                }
                return null;
            }

            //sequence not have identifier
            Lights.SetOnOff(packet.Identifier, packet.On);
            return null;
        }

        private RecipeChangeResultPacket ProcessRecipeChange(RecipeChangePacket packet) {
            RecipeChangeResultPacket resultPacket = new RecipeChangeResultPacket();

            resultPacket.Target = packet.Sender;
            resultPacket.Site = packet.Site;
            string recipeName = packet.RecipeName;

            if (Recipes.HasRecipe(recipeName) == false)
            {
                resultPacket.Result = EVisionResultType.NG;
            }
            //select
            else if ((Setting.CurrentRecipeName != recipeName) && LoadRecipe(recipeName))
            {
                resultPacket.Result = EVisionResultType.OK;
            }
            // 05.11 Insert  (이미 열려있는 레시피의 경우 NG 처리하던것을 OK처리함)
            else if ((Setting.CurrentRecipeName == recipeName) && LoadRecipe(recipeName))
            {
                resultPacket.Result = EVisionResultType.OK;
            }
            else
            {
                resultPacket.Result = EVisionResultType.NG;
            }

            return resultPacket;
        }

        private RecipeListResultPacket ProcessRecipeGet(RecipeGetPacket packet) {
            RecipeListResultPacket resultPacket = new RecipeListResultPacket();

            resultPacket.Target = packet.Sender;
            resultPacket.Site = packet.Site;
            resultPacket.MaxCount = packet.MaxCount;

            //sorting
            if (packet.Option == 1) {
                Recipes.SortingByCreateDate();
            }
            else if(packet.Option == 2) {
                Recipes.SortingByLastAccessDate();
            }

            //listing
            resultPacket.RecipeList.Clear();
            for (int i = 0; i< Recipes.List.Count; i++) {
                if (i >= packet.MaxCount) break;
                resultPacket.RecipeList.Add(Recipes[i].Name);
            }
            return resultPacket;
        }
        //sequence의 상태를 반환
        private SiteStatusResultPacket ProcessSiteStatus(SiteStatusPacket packet) {
            SiteStatusResultPacket resultPacket = new SiteStatusResultPacket();

            resultPacket.Target = packet.Sender;
            resultPacket.Site = packet.Site;

            EContextState state =  Sequences.GetSequenceState(packet.Identifier);
            switch (state) {
                case EContextState.Idle:
                    resultPacket.Result = EVisionSiteStatusType.Ready;
                    break;
                case EContextState.Error:
                    resultPacket.Result = EVisionSiteStatusType.Error;
                    break;
                case EContextState.Paused:
                case EContextState.Running:
                case EContextState.Finish:
                    resultPacket.Result = EVisionSiteStatusType.Busy;
                    break;
            }
            return resultPacket;
        }

        //260722 hbk Phase 68 D-01a→68-12: z=0(Datum) 판별 매직넘버 상수화 (D-09). Datum 검출(EStep.DatumPhase)은
        //  독립 Shot이 아니라 모든 Action 실행 안에 내장되어 매번 재수행되는 phase다. Plan 12(z=0 낭비 제거)부터
        //  z=0 도 더 이상 무조건 StartAll 하지 않는다 — InspectionSequence.FindZeroIndexDatumTriggerActionIndices()
        //  가 고른 이 시퀀스의 대표 Datum 트리거 Action(들)만 StartSubset 으로 실행한다(DatumConfigs 가 비어있거나
        //  대표 트리거가 하나도 해석 안 되면 그때만 StartAll 로 폴백). 대표 Action(들)도 DatumPhase 종료 후
        //  Grab/Measure 를 건너뛴다(Action_FAIMeasurement.ShouldSkipMeasurementAfterDatumPhase) — 68-10 Task2가
        //  stale 주석을 새 동작에 맞게 다시 쓴 선례와 동일 패턴으로 이 주석도 갱신한다.
        private const int DATUM_TEST_Z_INDEX = 0;

        //260409 hbk Phase 5: IsDynamicFAIMode 분기 (D-03)
        //260615 hbk Phase 43.2: IsRecipeReady guard — 레시피 비동기 로드 완료 전 TEST 수신 시 NG 거부 (D-C)
        //260626 hbk z_index=$PREP 분리: $TEST z_index 필드 제거 → _lastPrepZIndex 주입.
        //260722 hbk Phase 68 D-01/D-01a/D-01b: v1.0 실행(grab) 레벨 z_index 스코프 필터링 배선(StartV1Scoped 위임) —
        //  Phase 49 D-01이 응답 집계 레벨(AggregateIndexFais)에서만 구현되고 실행 레벨에서는 갭이던 것을 닫는다.
        private bool ProcessTest(TestPacket packet) {
            if (!IsRecipeReady) {
                Logging.PrintLog((int)ELogType.Error, "[RECIPE] TEST rejected — recipe not yet loaded (IsRecipeReady=false)");
                return false;
            }
            packet.TestID = _lastPrepZIndex.ToString(); //260626 hbk $PREP z_index 주입
            if (Sequences.IsDynamicFAIMode) {
                string seqName = packet.Identifier;
                SequenceBase seq = Sequences[seqName];
                if (seq == null) return false;
                return StartV1Scoped(seq, packet); //260722 hbk Phase 68 D-01/D-01a: z=0 StartAll / z>=1 StartSubset(+폴백)
            }
            return Sequences.Start(packet);
        }

        //260722 hbk Phase 68 D-01/D-01a/D-01b: v1.0(UseProtocolV1+IsDynamicFAIMode) $TEST 실행 스코프 배선.
        //  _lastPrepZIndex 가 이번 $TEST 의 z_index 단일 소스(packet.TestID 는 방금 이 값으로 대입됐을 뿐 재파싱 안 함).
        //  z_index==0(Datum) → StartAll(D-01a, 회귀 0). z_index>=1 → InspectionSequence.FindActionIndicesByZIndex 로
        //  매핑 Shot(own-ZIndex + 크로스-Z ZIndexA/ZIndexB owning Shot, D-01) 만 StartSubset 실행.
        //  StartSubset 은 매칭 인덱스의 min-max 연속구간만 실행(D-01b, SequenceHandler.RebuildInspectionActions 의
        //  ZIndex 안정 정렬이 same-ZIndex Shot 인접을 보장) — 스파스 크로스-Z 매칭으로 min-max 구간이 확장되어 그 사이
        //  무관 Shot 이 재실행될 가능성은 Plan 05 UAT 시나리오 1의 명시적 PASS/FAIL 게이트로 검증한다(T-68-01 mitigation).
        //  매칭 0건(레시피 ZIndex 미설정 등 운용 오류 또는 의도적으로 shot 이 없는 빈 z_index) → 조용한 무시 금지,
        //  로그는 남기되 Action 은 실행하지 않고 즉시 빈 응답으로 응답한다(StartEmptyScope, 260810 hbk
        //  bottom-empty-zindex-tcp-delay 수정 — 기존 StartAll 폴백은 이 시퀀스의 무관한 다른 z 의 shot 을 전부
        //  재실행했을 뿐 응답 내용에는 전혀 기여하지 않으면서 실기 재현 7,485ms 응답 지연을 유발했다. 상세 근거는
        //  StartEmptyScope 호출부 주석 참고).
        private bool StartV1Scoped(SequenceBase seq, TestPacket packet)
        {
            bool bIsDatumZIndex = _lastPrepZIndex == DATUM_TEST_Z_INDEX;
            if (bIsDatumZIndex)
            {
                //260722 hbk Phase 68 FIX-0(68-GAP-ANALYSIS.md): 크로스-Z 저장소 리셋을 z=0 응답 생성 시점
                //  (InspectionSequence.ResetCycleState, 모든 z=0 Action 실행이 끝난 뒤)에서 여기 z=0 $TEST
                //  수신 즉시(StartAll=z=0 Action 실행 시작 전)로 이동한다 — role A 이미지가 같은 z=0 tick의
                //  응답 단계에서 지워지지 않고 z=1 도착까지 살아남게 하기 위함. 캐스트 실패(방어적, 미도달)면
                //  리셋 없이 StartAll 만 수행.
                InspectionSequence inspDatumSeq = seq as InspectionSequence;
                bool bIsInspSeq = inspDatumSeq != null;
                if (!bIsInspSeq)
                {
                    return seq.StartAll(packet); // 방어적 폴백 — 실제로는 도달하지 않음(IsDynamicFAIMode 경로는 항상 InspectionSequence)
                }
                inspDatumSeq.BeginCrossZImageCycle();
                //260807 hbk quick-260807-fix2: Datum transform 캐시(_datumTransforms/_failedDatums) Clear 를
                //  여기서 직접 호출하던 이전 시도(State==Idle 사전 체크 후 Clear)는 TOCTOU 레이스였다 — 이 체크와
                //  StartSubset/StartAll 호출 사이에 "이전 사이클" 시퀀스 스레드가 (SequenceBase.MainExecute 5ms
                //  polling 으로) State 를 Finish/Error→Idle 로 독립적으로 바꿔버릴 수 있어, "State!=Idle 이라 Clear
                //  건너뜀" 과 "그 직후 StartSubset/StartAll 은 Idle 이라 통과" 가 동시에 성립하는 창이 있었다 — 그
                //  결과 새 부품의 새 사이클이 이전 부품의 스테일 _datumTransforms/_failedDatums 캐시를 그대로 물고
                //  시작할 수 있었다(조용한 오검출, 크래시 없음). Clear 지점을 InspectionSequence.
                //  HandleRunStartResetResults(OnStart 구독)로 옮겼다 — OnStart 는 StartCore 의 _startLock 안에서
                //  State 를 Idle→Running 으로 원자적으로 점유하고 RequestPacket 을 세팅한 "직후"(락 해제 후, Action
                //  실행 전) 같은 스레드 동기 호출로 발화되므로, 그 시점엔 이전 사이클 스레드가 이미 확실히
                //  물러났고(그렇지 않았다면 State!=Idle 이라 이번 StartCore 자체가 실패해 OnStart 가 아예 발화되지
                //  않는다) 다음 사이클 스레드는 아직 첫 Action 도 시작 전이라 두 컬렉션을 아무도 건드릴 수 없다 —
                //  Idle 사전-체크가 필요 없는, 진짜 락-프리 안전 지점.
                //260722 hbk Phase 68(68-12, GAP-3 z=0 낭비 제거): z=0 이 더 이상 무조건 StartAll 전량 실행하지
                //  않는다 — 이 시퀀스의 대표 Datum 트리거 Action(들)만 StartSubset 으로 실행하고, 다른 측정 Shot
                //  의 Grab/Measure 는 EStep.DatumPhase 종료부(ShouldSkipMeasurementAfterDatumPhase)에서 스킵된다.
                //  DatumConfigs 가 비어있거나(엣지 케이스) 대표 트리거가 하나도 해석 안 되면 정상적으로 가능한
                //  레시피 구성(운영 오류 아님)이므로 StartAll 로 안전 폴백한다.
                //  quick-260812: 진단 로그 제거 — 정상 폴백 경로라 운영자에게 알릴 내용이 없다.
                List<int> datumZeroIndices = inspDatumSeq.FindZeroIndexDatumTriggerActionIndices();
                bool bHasDatumZeroTrigger = datumZeroIndices != null && datumZeroIndices.Count > 0;
                if (bHasDatumZeroTrigger)
                {
                    return seq.StartSubset(datumZeroIndices.ToArray(), packet);
                }
                return seq.StartAll(packet);
            }
            InspectionSequence inspSeq = seq as InspectionSequence; //260722 hbk dynamic-FAI 런타임 타입은 항상 InspectionSequence
            bool bIsInspectionSeq = inspSeq != null;
            if (!bIsInspectionSeq)
            {
                return seq.StartAll(packet); // 방어적 폴백 — 실제로는 도달하지 않음(IsDynamicFAIMode 경로는 항상 InspectionSequence)
            }
            List<int> matchedIndices = inspSeq.FindActionIndicesByZIndex(_lastPrepZIndex);
            bool bHasMatch = matchedIndices != null && matchedIndices.Count > 0;
            if (bHasMatch)
            {
                return seq.StartSubset(matchedIndices.ToArray(), packet);
            }
            //260722 hbk Phase 68 GAP-2(68-GAP-ANALYSIS.md 우선순위 2): 빈-매칭이 전부 운용 오류인 것은 아니다 —
            //  오직 크로스-Z Datum 만 쓰는 z_index(예: Side z=1)는 own ZIndex/측정 ZIndexA/B 매칭이 없는 게 정상이다.
            //  StartAll 폴백(무관 Shot 전체 재-grab + 매 사이클 Error 로그) 대신 datum-only 최소 Action 만 실행한다.
            bool bDatumOnly = inspSeq.IsDatumOnlyExecutionIndex(_lastPrepZIndex);
            if (bDatumOnly)
            {
                List<int> datumIndices = inspSeq.FindDatumOnlyActionIndices(_lastPrepZIndex);
                bool bHasDatumTrigger = datumIndices != null && datumIndices.Count > 0;
                if (bHasDatumTrigger)
                {
                    return seq.StartSubset(datumIndices.ToArray(), packet); // Error 로그 없음 — Side z=1 의 설계된 정상 경로
                }
            }
            //260810 hbk bottom-empty-zindex-tcp-delay: 진짜 "매칭 0건"(크로스-Z Datum 전용도 아님) — 기존엔
            //  여기서 StartAll 로 이 시퀀스 전체(다른 모든 z 의 shot)를 재실행했다. 이는 InspectionSequence.
            //  WarnIfEmptyScope 주석(Phase 49 BLOCKER 1)의 "폴백(전체 재검사) 금지 — 경고만" 결정과 정면
            //  모순되는 별도 레이어(Phase 68 실행 스코프)의 안전장치였고, 실기 재현(BOTTOM z=3, main.ini 상
            //  3/22 는 원래부터 BOTTOM 소유 shot 이 0개인 정상 구성)에서 7,485ms 응답 지연을 유발했다 —
            //  StartAll 로 실행된 다른 z 의 shot 은 AggregateIndexFais(nZIndex=_lastPrepZIndex) 필터에 절대
            //  포함되지 않으므로(별개 z), 응답 내용은 Action 미실행 시와 100% 동일(B, FAIResults 0건) — 즉
            //  StartAll 은 응답 정확성에 전혀 기여하지 않고 지연만 유발했다(근본원인 확정,
            //  .planning/debug/bottom-empty-zindex-tcp-delay.md). StartEmptyScope 는 Action 실행 없이
            //  BuildScopedResponse/WarnIfEmptyScope(그대로 경고 로그 유지)만 거쳐 즉시 응답한다(회귀 0 설계 —
            //  응답 산출 로직은 1바이트도 변경하지 않음, 달라지는 것은 오직 "그 사이 무관한 shot 을 재실행하지
            //  않는다"는 점 뿐이다).
            Logging.PrintLog((int)ELogType.Error,
                string.Format("[V1Scope] ZIndex={0} 매칭 Shot 0건(Seq={1}) — Action 미실행, 즉시 빈 응답(B/F). 레시피 ZIndex 설정 확인 필요(의도된 빈 z_index 라면 정상).",
                    _lastPrepZIndex, seq.Name));
            return seq.StartEmptyScope(packet);
        }

        private TestResultPacket SendTestError(TestPacket packet) {
            TestResultPacket resultPacket = new TestResultPacket();
            TestPacket sendPacket = packet.AsTest();

            resultPacket.Target = sendPacket.Sender;
            resultPacket.Site = sendPacket.Site;
            resultPacket.InspectionType = sendPacket.TestType;
            resultPacket.Result = EVisionResultType.NG;

            return resultPacket;
        }

        //260626 hbk Phase 65 Plan 03 AV-08: $ALIGN_TEST 처리 — stub(IsPass=true echo) → 실측 grab+Run+pose 채움.
        //  BOTTOM: AlignFace→슬롯→grab→Matcher.Run→FillAlignPose(OffsetX/OffsetY/Theta)+IsPass=Found (D-06/D-07).
        //  TRAY: grab/Run 미수행 — 기존 echo ack 동작 유지 (회귀 0).
        //  AlignFace 범위 외(음수/6이상): IsPass=false 안전 거부+로그 (T-65-01).
        private AlignResultPacket ProcessAlignTest(AlignTestPacket packet) //260626 hbk 실측 경로 배선 (Phase 65 P03)
        {
            AlignResultPacket resultPacket = new AlignResultPacket();
            if (packet == null)
            {
                return null;
            }
            resultPacket.Target      = packet.Sender;
            resultPacket.AlignTarget = packet.AlignTarget;
            resultPacket.MaterialNo  = packet.MaterialNo;  //260625 hbk v3.0: 자재번호 echo (무변경)
            resultPacket.AlignFace   = packet.AlignFace;   //260626 hbk v3.0: 지그 면 인덱스 echo (무변경)

            bool bIsBottom = packet.AlignTarget == "BOTTOM"; //260626 hbk BOTTOM 전용 슬롯 라우팅
            if (!bIsBottom)
            {
                //260630 hbk TRAY: grab→Run→FillAlignPose (X/Y/Theta 전송, 피커 캘리브 없음)
                bool bTrayPass = RunTrayAlign(resultPacket);
                resultPacket.IsPass = bTrayPass;
                return resultPacket;
            }

            //260626 hbk BOTTOM: AlignFace → 슬롯 매핑 (범위 외 → None → NG 안전 거부, T-65-01)
            EBottomAlignSlot slot = EBottomAlignSlotMap.FromAlignFace(packet.AlignFace);
            bool bSlotValid = (slot != EBottomAlignSlot.None);
            if (!bSlotValid)
            {
                Logging.PrintLog((int)ELogType.Error,
                    "[ALIGN_TEST] AlignFace 범위 외 거부: {0} (유효범위 0~5)", packet.AlignFace);
                FillAlignPoseZero(resultPacket); //260626 hbk WR-01: PLC 필드 수 일관성 — pose=0 채움 후 NG 반환
                resultPacket.IsPass = false; //260626 hbk NG 안전 거부 (T-65-01)
                return resultPacket;
            }

            //260626 hbk 슬롯별 grab+Run+pose 채움 — RunBottomAlign 헬퍼 위임
            bool bPass = RunBottomAlign(slot, resultPacket);
            resultPacket.IsPass = bPass;
            return resultPacket;
        }

        //260626 hbk Phase 65 P03: BOTTOM 슬롯별 grab+Matcher.Run+pose 채움 헬퍼.
        //  미티칭/미연결/grab실패/검출실패 → IsPass=false + 로그. throw 금지 (TCP 스레드 크래시 방지, T-65-06).
        //  HImage/DetectedContourXld 반드시 Dispose (HALCON 핸들 누수 방지, Phase 61.1 WR-01/02 선례).
        private bool RunBottomAlign(EBottomAlignSlot slot, AlignResultPacket pResult) //260626 hbk 슬롯 grab→Run→pose 위임
        {
            try
            {
                //260626 hbk 슬롯 미티칭 가드 — 모델 파일 없으면 Run 불가
                bool bHasTemplate = EthernetVisionHandler.Handle.Matcher.HasTemplate(EEthernetVisionMode.Bottom, slot);
                if (!bHasTemplate)
                {
                    Logging.PrintLog((int)ELogType.Error,
                        "[ALIGN_TEST] Bottom slot={0} 미티칭 — 모델 없음 NG 반환", (int)slot);
                    FillAlignPoseZero(pResult); //260626 hbk PLC 형식 일관성 — pose=0 채움
                    return false;
                }

                //260626 hbk Camera null 가드 — Mode==None 또는 초기화 실패 시 null
                bool bCameraReady = EthernetVisionHandler.Handle.Camera != null;
                if (!bCameraReady)
                {
                    Logging.PrintLog((int)ELogType.Error,
                        "[ALIGN_TEST] 이더넷 카메라 미연결(null) — NG 반환");
                    FillAlignPoseZero(pResult);
                    return false;
                }

                HImage img = null; //260626 hbk finally 에서 Dispose 보장
                AlignResult res = null;
                try
                {
                    //260626 hbk Phase 66 D-06/D-07 — grab 직전 해당 슬롯 동축값 자동 적용(티칭=런타임 조명 일치)
                    ApplyCoaxLightForSlot(slot);
                    //260626 hbk EthernetAlignCamera.Grab() — IsOpen 이면 라이브 grab, 아니면 폴백(D-05, SIMUL 지원)
                    img = EthernetVisionHandler.Handle.Camera.Grab();
                    if (img == null)
                    {
                        Logging.PrintLog((int)ELogType.Error,
                            "[ALIGN_TEST] Bottom slot={0} grab 실패(null) — NG 반환", (int)slot);
                        FillAlignPoseZero(pResult);
                        return false;
                    }

                    //260626 hbk 슬롯 모델로 Run — pose(OffsetXmm/OffsetYmm/ThetaDeg) + Found
                    res = EthernetVisionHandler.Handle.Matcher.Run(img, EEthernetVisionMode.Bottom, slot);
                    if (!res.Found)
                    {
                        Logging.PrintLog((int)ELogType.Error,
                            "[ALIGN_TEST] Bottom slot={0} 검출 실패(Found=false) — NG 반환", (int)slot);
                        FillAlignPoseZero(pResult); //260626 hbk 검출 실패 시 pose=0 (T-65-05: 잘못된 보정값 미전송)
                        return false;
                    }

                    //260626 hbk 검출 성공 — pose Items 채움 (D-07)
                    FillAlignPose(pResult, res);
                    Logging.PrintLog((int)ELogType.Trace,
                        "[ALIGN_TEST] Bottom slot={0} PASS off=({1:0.000},{2:0.000}) theta={3:0.000}",
                        (int)slot, res.OffsetXmm, res.OffsetYmm, res.ThetaDeg);
                    return true;
                }
                finally
                {
                    //260626 hbk HImage Dispose — 호출자가 Dispose 책임(EthernetAlignCamera.Grab 규약)
                    if (img != null)
                    {
                        img.Dispose();
                        img = null;
                    }
                    //260626 hbk DetectedContourXld Dispose — TCP 경로에서 미사용, 누수 방지 (Phase 61.1 WR-01)
                    if (res != null && res.DetectedContourXld != null)
                    {
                        res.DetectedContourXld.Dispose();
                        res.DetectedContourXld = null;
                    }
                    LightHandler.Handle.SetOnOff(LightHandler.LIGHT_ALIGN_COAX, false); //260630 hbk grab 완료 즉시 소등 — 검사와 독립
                }
            }
            catch (Exception ex)
            {
                //260626 hbk 예외 → throw 금지, false 반환 (TCP 스레드 크래시 방지, T-65-06)
                Logging.PrintLog((int)ELogType.Error,
                    "[ALIGN_TEST] RunBottomAlign 예외: {0}", ex.Message);
                FillAlignPoseZero(pResult); //260626 hbk WR-02: 외부 catch — 빈 Items 응답 방지, pose=0 채움
                return false;
            }
        }

        //260630 hbk Tray grab→Matcher.Run→pose 채움 헬퍼. throw 금지 (TCP 스레드 크래시 방지).
        private bool RunTrayAlign(AlignResultPacket pResult)
        {
            try
            {
                bool bHasTemplate = EthernetVisionHandler.Handle.Matcher.HasTemplate(EEthernetVisionMode.Tray);
                if (!bHasTemplate)
                {
                    Logging.PrintLog((int)ELogType.Error, "[ALIGN_TEST] Tray 미티칭 — 모델 없음 NG");
                    FillAlignPoseZero(pResult);
                    return false;
                }

                bool bCameraReady = EthernetVisionHandler.Handle.Camera != null;
                if (!bCameraReady)
                {
                    Logging.PrintLog((int)ELogType.Error, "[ALIGN_TEST] 이더넷 카메라 미연결 — NG");
                    FillAlignPoseZero(pResult);
                    return false;
                }

                HImage img = null;
                AlignResult res = null;
                try
                {
                    ApplyCoaxLightForTray(); //260630 hbk grab 직전 Tray 동축 조명 적용
                    img = EthernetVisionHandler.Handle.Camera.Grab();
                    if (img == null)
                    {
                        Logging.PrintLog((int)ELogType.Error, "[ALIGN_TEST] Tray grab 실패(null) — NG");
                        FillAlignPoseZero(pResult);
                        return false;
                    }

                    res = EthernetVisionHandler.Handle.Matcher.Run(img, EEthernetVisionMode.Tray);
                    if (!res.Found)
                    {
                        Logging.PrintLog((int)ELogType.Error, "[ALIGN_TEST] Tray 검출 실패 — NG");
                        FillAlignPoseZero(pResult);
                        return false;
                    }

                    FillAlignPose(pResult, res);
                    Logging.PrintLog((int)ELogType.Trace,
                        "[ALIGN_TEST] Tray PASS off=({0:0.000},{1:0.000}) theta={2:0.000}",
                        res.OffsetXmm, res.OffsetYmm, res.ThetaDeg);
                    return true;
                }
                finally
                {
                    if (img != null)
                    {
                        img.Dispose();
                        img = null;
                    }
                    if (res != null && res.DetectedContourXld != null)
                    {
                        res.DetectedContourXld.Dispose();
                        res.DetectedContourXld = null;
                    }
                    LightHandler.Handle.SetOnOff(LightHandler.LIGHT_ALIGN_COAX, false); //260630 hbk grab 완료 즉시 소등 — 검사와 독립
                }
            }
            catch (Exception ex)
            {
                Logging.PrintLog((int)ELogType.Error, "[ALIGN_TEST] RunTrayAlign 예외: {0}", ex.Message);
                FillAlignPoseZero(pResult);
                return false;
            }
        }

        //260630 hbk Tray JSON 의 CoaxEnabled/CoaxLevel 을 읽어 LIGHT_ALIGN_COAX 적용.
        //  JSON 없음(미티칭)/null → 동축 off. 예외 → 로그 후 off (throw 금지).
        private void ApplyCoaxLightForTray()
        {
            try
            {
                AlignRefPose refPose = EthernetVisionHandler.Handle.Matcher.GetSlotRefPose(EEthernetVisionMode.Tray, EBottomAlignSlot.None); //260630 hbk Tray 동축값 로드
                bool bEnabled = false;
                int nLevel = 0;
                if (refPose != null)
                {
                    bEnabled = refPose.CoaxEnabled;
                    nLevel = refPose.CoaxLevel;
                }

                if (bEnabled)
                {
                    LightHandler.Handle.SetOnOff(LightHandler.LIGHT_ALIGN_COAX, true);
                    LightHandler.Handle.SetLevel(LightHandler.LIGHT_ALIGN_COAX, nLevel);
                }
                else
                {
                    LightHandler.Handle.SetOnOff(LightHandler.LIGHT_ALIGN_COAX, false);
                }
            }
            catch (Exception ex)
            {
                Logging.PrintLog((int)ELogType.Error,
                    "[ALIGN_TEST] ApplyCoaxLightForTray 예외: {0}", ex.Message);
                LightHandler.Handle.SetOnOff(LightHandler.LIGHT_ALIGN_COAX, false);
            }
        }

        //260626 hbk Phase 66 D-06 — 슬롯 JSON 의 CoaxEnabled/CoaxLevel 을 읽어 LIGHT_ALIGN_COAX 적용.
        //  JSON 없음(미티칭)/null → 동축 off. 예외 → 로그 후 off (throw 금지, TCP 스레드 크래시 방지 T-66-01).
        private void ApplyCoaxLightForSlot(EBottomAlignSlot slot)
        {
            try
            {
                AlignRefPose refPose = EthernetVisionHandler.Handle.Matcher.GetSlotRefPose(EEthernetVisionMode.Bottom, slot);   //260626 hbk 슬롯 동축값 로드
                bool bEnabled = false;   //260626 hbk 기본 off (미티칭/null 안전)
                int nLevel = 0;
                if (refPose != null)
                {
                    bEnabled = refPose.CoaxEnabled;   //260626 hbk 저장 동축 ON/OFF
                    nLevel = refPose.CoaxLevel;       //260626 hbk 저장 동축 밝기
                }

                if (bEnabled)
                {
                    LightHandler.Handle.SetOnOff(LightHandler.LIGHT_ALIGN_COAX, true);   //260626 hbk 동축 ON
                    LightHandler.Handle.SetLevel(LightHandler.LIGHT_ALIGN_COAX, nLevel);   //260626 hbk 동축 밝기
                }
                else
                {
                    LightHandler.Handle.SetOnOff(LightHandler.LIGHT_ALIGN_COAX, false);   //260626 hbk 동축 OFF
                }
            }
            catch (Exception ex)
            {
                Logging.PrintLog((int)ELogType.Error,
                    "[ALIGN_TEST] ApplyCoaxLightForSlot 예외: {0}", ex.Message);   //260626 hbk 로그 후 off
                LightHandler.Handle.SetOnOff(LightHandler.LIGHT_ALIGN_COAX, false);   //260626 hbk 예외 시 안전 off (throw 금지)
            }
        }

        //260626 hbk Phase 65 P03: AlignResultPacket.Items 에 pose(OffsetX/OffsetY/Theta) 채움.
        //  Items 순서: OffsetX → OffsetY → Theta (BuildAlignItems 직렬화 순서, D-08 v3.0 스펙).
        private void FillAlignPose(AlignResultPacket pkt, AlignResult res) //260626 hbk 검출 성공 pose 채움
        {
            pkt.Items.Clear();
            AlignResultItem itemX = new AlignResultItem();
            itemX.ItemName = "OffsetX";
            itemX.Value    = res.OffsetXmm; //260626 hbk mm 단위 (AlignShapeMatchService.Run 산출)
            pkt.Items.Add(itemX);

            AlignResultItem itemY = new AlignResultItem();
            itemY.ItemName = "OffsetY";
            itemY.Value    = res.OffsetYmm; //260626 hbk mm 단위
            pkt.Items.Add(itemY);

            AlignResultItem itemTheta = new AlignResultItem();
            itemTheta.ItemName = "Theta";
            itemTheta.Value    = res.ThetaDeg; //260626 hbk Bottom 모드: res.HasTheta=true (deg 단위)
            pkt.Items.Add(itemTheta);
        }

        //260626 hbk Phase 65 P03: 검출 실패/미티칭/grab 실패 시 pose=0 채움.
        //  PLC 가 형식 일관 수신할 수 있도록 0값 전송 (IsPass=false → PLC 안착 미실행, T-65-05).
        private void FillAlignPoseZero(AlignResultPacket pkt) //260626 hbk NG 시 pose=0 채움 (PLC 형식 일관)
        {
            pkt.Items.Clear();
            AlignResultItem itemX = new AlignResultItem();
            itemX.ItemName = "OffsetX";
            itemX.Value    = 0.0; //260626 hbk NG 시 0 (IsPass=false 가 실제 불량 신호)
            pkt.Items.Add(itemX);

            AlignResultItem itemY = new AlignResultItem();
            itemY.ItemName = "OffsetY";
            itemY.Value    = 0.0; //260626 hbk NG 시 0
            pkt.Items.Add(itemY);

            AlignResultItem itemTheta = new AlignResultItem();
            itemTheta.ItemName = "Theta";
            itemTheta.Value    = 0.0; //260626 hbk NG 시 0
            pkt.Items.Add(itemTheta);
        }

        //quick-260812: TCP 자동경로의 캘 실패를 화면에도 알린다. 로그·판정·응답은 그대로 둔다.
        //  마샬링을 여기(호출 측)에서 하는 이유: 통신 스레드가 라벨을 직접 만지면 안 되고,
        //  기존 뷰어 콜백 2개도 정확히 같은 위치에서 같은 방식으로 넘긴다.
        //  구독자가 없거나(창 미부착) 앱 종료 중이면 조용히 지나간다.
        //  전체를 try 로 감싸는 이유: 여기서 예외가 새면 통신 응답 흐름이 끊긴다.
        private void NotifyAlignCalibError(string szMessage)
        {
            try
            {
                Action<string> errCb = EthernetVisionHandler.Handle.OnCalibError;
                bool bHasSubscriber = errCb != null;
                if (!bHasSubscriber)
                {
                    return;
                }
                System.Windows.Application app = System.Windows.Application.Current;
                bool bHasApp = app != null;
                if (!bHasApp)
                {
                    return;
                }
                app.Dispatcher.Invoke(() => errCb(szMessage));
            }
            catch (Exception ex)
            {
                Logging.PrintLog((int)ELogType.Error, "[ALIGN_CALIB] 화면 알림 전달 실패: {0}", ex.Message);
            }
        }

        //260624 hbk Phase 63 AV-09: $ALIGN_CALIB 처리.
        //260625 hbk v3.0: CmdStr echo 추가. AlignFace 제거됨.
        //260630 hbk Phase 60: 스텁 → 실 구현 (START/STEP/END/ABORT 분기 + PickerCal 연결).
        private AlignCalibResultPacket ProcessAlignCalib(AlignCalibPacket packet)
        {
            AlignCalibResultPacket resultPacket = new AlignCalibResultPacket();
            bool bHasPacket = packet != null;
            if (!bHasPacket)
            {
                return null;
            }
            resultPacket.Target      = packet.Sender;
            resultPacket.AlignTarget = packet.AlignTarget;
            resultPacket.CmdStr      = packet.CmdStr;
            resultPacket.IsPass      = false; // 기본 FAIL — 성공 분기에서 true 덮어씀

            string szCmd = packet.CmdStr;

            int nCmd = 0;                                                    //260807 hbk quick-260807-omy
            bool bCmdIsNumeric = Int32.TryParse(szCmd, out nCmd);             //260807 hbk quick-260807-omy
            if (!bCmdIsNumeric)                                               //260807 hbk quick-260807-omy 비숫자 가드 — 코드 비교보다 반드시 먼저 (0=START 오인식 방지)
            {
                Logging.PrintLog((int)ELogType.Error, "[ALIGN_CALIB] 숫자가 아닌 CmdStr: {0}", szCmd);
                NotifyAlignCalibError("[자동] 명령 형식 오류: " + szCmd);
                return resultPacket;
            }

            bool bIsStart = nCmd == AlignCalibPacket.CMD_CODE_START;
            if (bIsStart)
            {
                EthernetVisionHandler.Handle.PickerCal.Reset();
#if SIMUL_MODE
                //260630 hbk — SIMUL: START 수신 시 이미지 순차 인덱스 리셋
                if (EthernetVisionHandler.Handle.Camera != null) {
                    EthernetVisionHandler.Handle.Camera.ResetSimulIndex();
                }
#endif
                // 모델 로드 시도 (UI 티칭 완료 전제). 실패 시 경고만 — STEP 에서 자연 실패.
                string loadErr;
                bool bLoaded = EthernetVisionHandler.Handle.PickerCal.TryLoadModel(out loadErr);
                if (!bLoaded)
                {
                    Logging.PrintLog((int)ELogType.Error,
                        "[ALIGN_CALIB] START: 모델 로드 실패 ({0})", loadErr);
                    NotifyAlignCalibError("[자동] START 모델 로드 실패: " + loadErr);
                }
                resultPacket.IsPass = true;
                resultPacket.StepNo = 0;    //260810 hbk quick-260810-olh: N=0 고정(제어팀 요청, START 의미)
                Logging.PrintLog((int)ELogType.Trace, "[ALIGN_CALIB] START — 누적 초기화, model={0}", bLoaded);
                return resultPacket;
            }

            bool bIsStep = nCmd == AlignCalibPacket.CMD_CODE_STEP;
            if (bIsStep)
            {
                bool bCameraReady = EthernetVisionHandler.Handle.Camera != null;
                if (!bCameraReady)
                {
                    Logging.PrintLog((int)ELogType.Error, "[ALIGN_CALIB] STEP: 카메라 미연결");
                    NotifyAlignCalibError("[자동] STEP: 카메라 미연결");
                    return resultPacket;
                }

                HImage img = null;
                try
                {
                    // 260724 hbk 임시 진단 — 동기 파일 기록으로 크래시 직전 상황 확실히 남김
                    try { System.IO.File.AppendAllText(@"D:\Data\Camera\crash_diag.log",
                        string.Format("{0} [ALIGN_CALIB] STEP: Camera.Grab() 호출 직전\r\n", DateTime.Now.ToString("HH:mm:ss.fff"))); } catch { }
                    img = EthernetVisionHandler.Handle.Camera.Grab();
                    try { System.IO.File.AppendAllText(@"D:\Data\Camera\crash_diag.log",
                        string.Format("{0} [ALIGN_CALIB] STEP: Camera.Grab() 반환됨\r\n", DateTime.Now.ToString("HH:mm:ss.fff"))); } catch { }
                    bool bGrabOk = img != null;
                    if (!bGrabOk)
                    {
                        Logging.PrintLog((int)ELogType.Error, "[ALIGN_CALIB] STEP: Grab 실패");
                        NotifyAlignCalibError("[자동] STEP: Grab 실패");
                        return resultPacket;
                    }

                    //260630 hbk — 사각형 ROI 전환: Row/Col/Radius→Row1/Col1/Row2/Col2
                    double dSearchRow1 = SystemSetting.Handle.CalibSearchRow1;
                    double dSearchCol1 = SystemSetting.Handle.CalibSearchCol1;
                    double dSearchRow2 = SystemSetting.Handle.CalibSearchRow2;
                    double dSearchCol2 = SystemSetting.Handle.CalibSearchCol2;

                    double dFoundRow, dFoundCol;
                    double dScore;   //quick-260812: 이미 계산된 검색 점수 수신(등급 로그용)
                    string error;
                    bool bOk = EthernetVisionHandler.Handle.PickerCal.TryAddStep(
                        img, dSearchRow1, dSearchCol1, dSearchRow2, dSearchCol2,
                        out dFoundRow, out dFoundCol, out dScore, out error);

                    if (bOk)
                    {
                        resultPacket.StepNo = EthernetVisionHandler.Handle.PickerCal.StepCount;
                        resultPacket.IsPass = true;
                        //260630 hbk — TCP 경로: Grab 이미지 + vizXld 를 뷰어에 표시
                        var viewerCb = EthernetVisionHandler.Handle.OnCalibStepViewer;
                        if (viewerCb != null)
                        {
                            HObject vizXld = EthernetVisionHandler.Handle.PickerCal.GetVisualizationXld();
                            HImage imgRef = img; // finally 전에 캡처
                            System.Windows.Application.Current.Dispatcher.Invoke(() => viewerCb(imgRef, vizXld));
                        }
                        //quick-260812: 이번 Quick 은 로그까지. 화면 노출은 Quick #3(TCP 자동경로) 범위.
                        ETeachGrade calGrade = TeachDiag.ClassifyScore(dScore, PickerCenterCalibrationService.FindMinScore);
                        Logging.PrintLog((int)ELogType.Trace,
                            "[ALIGN_CALIB] STEP {0} OK score={1:F3} grade={2}", resultPacket.StepNo, dScore, calGrade);
                    }
                    else
                    {
                        Logging.PrintLog((int)ELogType.Error, "[ALIGN_CALIB] STEP 실패: {0}", error);
                        NotifyAlignCalibError("[자동] STEP 실패: " + error);
                    }
                }
                finally
                {
                    if (img != null)
                    {
                        img.Dispose();
                    }
                }
                return resultPacket;
            }

            bool bIsEnd = nCmd == AlignCalibPacket.CMD_CODE_END;
            if (bIsEnd)
            {
                double dRow, dCol, dRad;
                string error;
                bool bOk = EthernetVisionHandler.Handle.PickerCal.TryComputePickerCenter(
                    out dRow, out dCol, out dRad, out error);

                if (bOk)
                {
                    resultPacket.IsPass = true;
                    resultPacket.StepNo = 99;    //260810 hbk quick-260810-olh: N=99 고정(제어팀 요청, END=완료 의미)
                    //260630 hbk — END 성공: 피커센터 즉시 저장 (비정상 종료 시 손실 방지)
                    SystemSetting.Handle.Save();
                    var endCb = EthernetVisionHandler.Handle.OnCalibEndViewer;
                    if (endCb != null)
                    {
                        HObject vizXld = EthernetVisionHandler.Handle.PickerCal.GetVisualizationXld();
                        double r = dRow; double c = dCol; double rad = dRad;
                        System.Windows.Application.Current.Dispatcher.Invoke(() => endCb(r, c, rad, vizXld));
                    }
                    Logging.PrintLog((int)ELogType.Trace,
                        "[ALIGN_CALIB] END — 피커센터=({0:F2},{1:F2}) r={2:F2}", dRow, dCol, dRad);
                }
                else
                {
                    Logging.PrintLog((int)ELogType.Error, "[ALIGN_CALIB] END 산출 실패: {0}", error);
                    NotifyAlignCalibError("[자동] END 산출 실패: " + error);
                }
                return resultPacket;
            }

            bool bIsAbort = nCmd == AlignCalibPacket.CMD_CODE_ABORT;
            if (bIsAbort)
            {
                EthernetVisionHandler.Handle.PickerCal.Reset();
                resultPacket.IsPass = true;
                resultPacket.StepNo = 98;    //260810 hbk quick-260810-olh: N=98 고정(제어팀 요청, ABORT=취소 의미)
                Logging.PrintLog((int)ELogType.Trace, "[ALIGN_CALIB] ABORT — 누적 초기화");
                return resultPacket;
            }

            Logging.PrintLog((int)ELogType.Error, "[ALIGN_CALIB] 알 수 없는 CmdStr: {0}", szCmd);
            NotifyAlignCalibError("[자동] 알 수 없는 명령: " + szCmd);
            return resultPacket;
        }

        //260625 hbk v3.0: $ALIVE 처리 — ALIVE:OK 응답.
        private AliveResponsePacket ProcessAlive(AlivePacket packet)
        {
            AliveResponsePacket resultPacket = new AliveResponsePacket();
            bool bHasPacket = packet != null;
            if (!bHasPacket)
            {
                return null;
            }
            resultPacket.Target = packet.Sender;
            return resultPacket;
        }

        //260625 hbk Phase 64 LIGHT-01 (D-12): $PREP 처리.
        //260806 hbk Phase 71: Op 필드 폐기 — $PREP 는 항상 "이 z_index 조명 점등" 단일 의미.
        //  소등은 PLC 요청이 아니라 사이클 P/F 확정 시 InspectionSequence 가 자동 수행(71-02, TryTurnOffLightsOnCycleEnd).
        //  HW 트리거 전환 대비: 조명 점등이 $PREP(준비 단계)에 통합 → $TEST(트리거)는 조명 무관.
        //  Site 필드는 ACK 에 echo만 함. 실제 시퀀스 라우팅은 이 PC 소속 InspectionSequence 전부 대상.
        private PrepAckPacket ProcessPrep(PrepPacket packet)
        {
            PrepAckPacket ackPacket = new PrepAckPacket();
            bool bHasPacket = packet != null;
            if (!bHasPacket)
            {
                return null;
            }
            ackPacket.Target = packet.Sender;
            ackPacket.Site = packet.Site;
            ackPacket.ZIndex = packet.ZIndex;
            ackPacket.IsOk = false; // 기본값 FAIL — 성공 시 true 로 덮어씀

            _lastPrepZIndex = packet.ZIndex; //260626 hbk z_index 저장 → ProcessTest 주입용
            bool bApplied = ApplyPrepToSequences(packet.ZIndex);
            if (bApplied)
            {
                ackPacket.IsOk = true;
            }
            return ackPacket;
        }

        //260807 hbk quick-260807-lh7 (D-LH7-01): $RESET 처리 — "검사 상태 클린 슬레이트 + ACK".
        //  왜 필요한가: $PREP 이 저장한 _lastPrepZIndex 는 다음 $PREP 이 올 때까지 계속 남는데 되돌릴 수단이 앱 재시작뿐이라,
        //  실측 중 상태가 꼬이면 복구가 불가능했다. 그 복구 수단이 이 명령이다.
        //  Site 필드는 ACK 에 echo 만 한다 — 라우팅에 쓰지 않는다. site 로 Top/Bottom 을 구분하는 것이 불가능한 것은
        //  이미 확인된 설계 제약이며, ApplyPrepToSequences($PREP)와 동일하게 "이 PC 의 InspectionSequence 전부"가 대상이다.
        //  IsOk 의미: true = 모든 대상 시퀀스가 실제로 리셋됨 / false = 하나 이상이 실행 중이라 건너뜀(또는 대상 0개).
        //  실패여도 ACK 는 반드시 보낸다 — 무응답은 PLC 를 ACK 무한 대기(라인 정지)시킨다.
        private ResetAckPacket ProcessReset(ResetPacket packet)
        {
            ResetAckPacket ackPacket = new ResetAckPacket();
            bool bHasPacket = packet != null;
            if (!bHasPacket)
            {
                return null;
            }
            ackPacket.Target = packet.Sender;
            ackPacket.Site = packet.Site;

            _lastPrepZIndex = 0; //260807 hbk volatile int — 어느 스레드에서 써도 안전. 0 = "다음 $TEST 는 Datum(z=0)" 보수적 초기값.

            bool bAllReset = ResetSequenceCycleStates();
            ackPacket.IsOk = bAllReset;

            Logging.PrintLog((int)ELogType.Trace,
                "[RESET] site={0} 수신 — _lastPrepZIndex=0, 시퀀스 리셋 결과={1}", packet.Site, bAllReset);
            return ackPacket;
        }

        // [임시 / TEMP] 수동 Z축 트리거 브리지.
        // 존재 이유: Z축을 아직 수동 지그(다이얼)로 맞추는 단계라, 조작자가 맞춘 z_index 로
        //   검사를 돌리도록 알리는 로컬 경로가 없다($PREP+$TEST TCP 패킷만 존재). 이 wrapper 는
        //   그 두 패킷을 코드로 만들어 실제 처리 경로(ProcessPrep→ProcessTest)로 그대로 흘려보낸다.
        // 삭제 조건: POC 장비의 자동 Z축(IAxisController) 실제 구현이 완료되면
        //   이 메서드 전체 + MainView 의 수동 Z 트리거 UI 패널 전체를 삭제할 것.
        // 주의: ProcessPrep/ProcessTest 는 프로덕션 TCP 경로 — 시그니처/로직 변경 금지, 호출만 한다.
        internal bool DebugManualZTrigger(string seqName, int zIndex)
        {
            bool bHasSeqName = !string.IsNullOrEmpty(seqName);
            if (!bHasSeqName)
            {
                Logging.PrintLog((int)ELogType.Error, "[임시 수동Z트리거] 시퀀스 이름이 비어있음 — 트리거 취소");
                return false;
            }

            PrepPacket prepPacket = new PrepPacket();
            prepPacket.ZIndex = zIndex;
            PrepAckPacket ack = ProcessPrep(prepPacket);

            bool bPrepOk = ack != null && ack.IsOk;
            if (!bPrepOk)
            {
                Logging.PrintLog((int)ELogType.Error,
                    "[임시 수동Z트리거] 시퀀스={0} z_index={1} PREP 실패 — TEST 진행하지 않음", seqName, zIndex);
                return false;
            }

            TestPacket testPacket = new TestPacket();
            testPacket.Identifier = seqName;
            bool bTestOk = ProcessTest(testPacket);

            Logging.PrintLog((int)ELogType.Trace,
                "[임시 수동Z트리거] 시퀀스={0} z_index={1} PREP={2} TEST={3}", seqName, zIndex, bPrepOk, bTestOk);

            return bTestOk;
        }

        //260625 hbk Phase 64 LIGHT-01: Sequences 순회 → InspectionSequence 찾기 → ApplyShotLights 호출.
        //  하나라도 성공하면 true 반환. 매칭 InspectionSequence 없으면 false.
        private bool ApplyPrepToSequences(int nZIndex)
        {
            bool bAnyApplied = false;
            int nCount = Sequences.Count;
            for (int i = 0; i < nCount; i++)
            {
                SequenceBase seqBase = Sequences[i];
                InspectionSequence inspSeq = seqBase as InspectionSequence;
                bool bIsInsp = inspSeq != null;
                if (!bIsInsp)
                {
                    continue;
                }
                bool bOk = inspSeq.ApplyShotLights(nZIndex);
                if (bOk)
                {
                    bAnyApplied = true;
                }
            }
            return bAnyApplied;
        }

        //260626 hbk v3.0: 전 InspectionSequence 소등 헬퍼(하나라도 있으면 true).
        //260806 hbk Phase 71: $PREP Op 폐기로 현재 호출자 없음 — CONTEXT locked decision 에 따라 삭제하지 않고 유지한다.
        //  소등 주체가 "PLC 의 명시 요청" → "사이클 P/F 확정 시 자동"(InspectionSequence.TryTurnOffLightsOnCycleEnd, 71-02)으로 이동했을 뿐이며,
        //  향후 "PC 단위 전 시퀀스 강제 소등"(예: 비상정지/레시피 전환)이 필요해지면 이 헬퍼를 그대로 재사용한다.
        private bool TurnOffPrepLights()
        {
            bool bAnyOff = false;
            int nCount = Sequences.Count;
            for (int i = 0; i < nCount; i++)
            {
                SequenceBase seqBase = Sequences[i];
                InspectionSequence inspSeq = seqBase as InspectionSequence;
                bool bIsInsp = inspSeq != null;
                if (!bIsInsp)
                {
                    continue;
                }
                inspSeq.TurnOffShotLights();
                bAnyOff = true;
            }
            return bAnyOff;
        }

        //260807 hbk quick-260807-lh7: Sequences 순회 → InspectionSequence 만 골라 클린 슬레이트 리셋.
        //  260810 hbk reset-datum-clear-race 수정: 이전엔 여기서 State==Idle 을 락 없이 미리 읽고, 그 결과를 근거로
        //  (같은 원자성 없이) ResetCycleStateForProtocolReset() 을 나중에 호출했다 — 그 사이 SequenceBase.StartCore
        //  가 끼어들어 Idle→Running 원자 점유에 성공하면, 이 스레드는 이미 지난 "Idle" 판단을 근거로 그대로 Clear 를
        //  실행해 실행 중인 시퀀스 스레드와 락 없는 _datumTransforms 등을 동시에 건드릴 수 있었다(TOCTOU, 크래시/
        //  무한루프 가능). InspectionSequence.TryResetCycleStateForProtocolReset() 하나로 교체 — 내부에서
        //  SequenceBase.TryExecuteIfIdle 이 StartCore 와 동일한 _startLock 안에서 "체크+실행"을 원자적으로 묶는다.
        //  ▶ Stop() 을 먼저 부르지 않는 이유: SequenceBase.Stop() 은 RequestPacket!=null 이면 AddResponse() 를 호출해
        //    PLC 가 요청하지도 않은 $RESULT 를 한 장 더 내보낸다 — 복구 명령이 프로토콜을 더 꼬는 부작용이라 미채택.
        //  ▶ 반환값: 대상이 1개 이상이고 그 전부를 리셋했을 때만 true(=ACK OK). 부분 리셋은 클린 슬레이트가 아니므로 false.
        private bool ResetSequenceCycleStates()
        {
            int nFound = 0;
            int nReset = 0;
            int nCount = Sequences.Count;
            for (int i = 0; i < nCount; i++)
            {
                SequenceBase seqBase = Sequences[i];
                InspectionSequence inspSeq = seqBase as InspectionSequence;
                bool bIsInsp = inspSeq != null;
                if (!bIsInsp)
                {
                    continue;
                }
                nFound++;

                bool bDidReset = inspSeq.TryResetCycleStateForProtocolReset(); // Idle 체크 + 실행이 _startLock 안에서 원자적
                if (!bDidReset)
                {
                    Logging.PrintLog((int)ELogType.Error,
                        "[RESET] Seq={0} 실행 중(State={1}) — 상태 리셋 건너뜀(스레드 안전). 사이클 종료 후 $RESET 재전송 필요.",
                        inspSeq.Name, inspSeq.State.ToString());
                    continue;
                }

                nReset++;
                Logging.PrintLog((int)ELogType.Trace,
                    "[RESET] Seq={0} 클린 슬레이트 완료", inspSeq.Name);
            }

            bool bAllReset = nFound > 0 && nReset == nFound;
            return bAllReset;
        }

        //260510 hbk Phase 21: BUF-02 channel #1 — recipe change buffer flush wire-up (D-02 / D-03)
        private void WireBufferLifecycle() {
            //260510 hbk Phase 21: OnRecipeChanged subscriber 등록 — Sequences 가 SequenceHandler.Handle 로 초기화된 후 호출되어야 함
            Sequences.OnRecipeChanged += OnRecipeChanged_FlushBuffers;

            //260811 hbk plc-spec-260811-alignment(조명): 이 메서드가 SystemHandler.Initialize() Step 6(root,
            //  READ-ONLY)에서 호출되는 유일한 Custom-측 wiring 지점이라 재사용한다 — Lights(Step 1)/Sequences
            //  (Step 2) 둘 다 이 시점엔 이미 준비돼 있다. 이름은 "BufferLifecycle" 이지만 "기동 시 1회 wiring"
            //  이라는 본질은 동일해 새 이름의 별도 메서드를 추가하고 root 에서 호출부를 새로 추가하는 것보다
            //  이 지점에 얹는 편이 root 파일 무변경 제약을 지키면서 가장 자연스럽다.
            Lights.OnError += OnLightHandlerError;
        }

        //260510 hbk Phase 21: BUF-02 channel #1 — Release 시점 unsubscribe (subscriber lifecycle 보호 — D-04 Claude's Discretion)
        internal void UnwireBufferLifecycle() {
            //260510 hbk Phase 21: 멱등 — 미등록 상태에서도 안전 (delegate -= null 무동작)
            Sequences.OnRecipeChanged -= OnRecipeChanged_FlushBuffers;

            //260811 hbk plc-spec-260811-alignment(조명): WireBufferLifecycle 과 대칭 — root Release()(READ-ONLY)가
            //  이미 이 메서드를 호출하므로 동일한 지점에서 해제한다.
            Lights.OnError -= OnLightHandlerError;
        }

        //260510 hbk Phase 21: BUF-02 channel #1 — recipe change 훅 (wire/unwire lifecycle 유지용)
        private void OnRecipeChanged_FlushBuffers(object sender, RecipeChangedEventArgs args) {
            //260511 hbk Phase 21 hotfix: ClearShots() 제거 — LoadRecipe 완료 후 OnRecipeChanged 발화 시
            //  이 훅이 방금 로드된 Shots 컬렉션을 전부 삭제하는 silent data-loss 유발.
            //  LoadPhase6Format 내부에서 Shots 재구성을 직접 수행하므로 여기서 ClearShots 호출 불필요.
            //  app shutdown 시 buffer dispose 는 Release() 의 channel #3 (SystemHandler.cs:176) 이 담당.
        }

        //260811 hbk plc-spec-260811-alignment(조명 하드웨어 에러 → E, 카메라 절반 커밋 6697fc4 의 나머지 절반):
        //  LightHandler.OnError 핸들러. LightHandler.Execute() 전용 백그라운드 스레드(1ms 폴링)에서, 컨트롤러/
        //  채널 단위 Read/Write 가 FAIL_LIMIT(3) 회 연속 실패했을 때 발화된다 — 이 메서드 자체가 그 스레드에서
        //  실행된다(SystemHandler/MainRun 스레드 아님).
        //
        //  설계 결정 1(비동기-사이클 상관, 사용자 승인 "조명에러도 E로 처리"):
        //  FAIL_LIMIT 누적 지연 때문에 이 이벤트는 실패가 "확정"된 한참 뒤에야 발화된다 — 그 사이 관련 사이클의
        //  $RESULT 가 이미 나갔을 수 있다. 완벽한 상관은 불가능하므로 현실적으로: 이 이벤트가 발화된 "지금"
        //  State==Running 인 시퀀스만 마킹한다(카메라 경로와 동일한 MarkCycleHardwareError 진입점 재사용,
        //  InspectionSequence.cs 주석 참고). 이미 응답이 나가버린 사이클은 소급 정정하지 않는다 — 대신
        //  Error 로그로 명확히 남겨 사후 추적 가능하게 한다(응답 재전송/철회는 시도하지 않음).
        //
        //  설계 결정 2(어느 시퀀스를 마킹할지 — 정밀 채널→시퀀스 역매핑 대신 광범위 마킹 채택):
        //  LightFailEventArgs 는 controllerIndex/channel/channelName 을 담고 있어 정밀 타겟팅을 시도할 수도
        //  있었지만, Custom/TcpServer/ResourceMap.cs 의 조명 자원 매핑(MapPc1Resources/MapPc2Resources/그 외
        //  role)은 PC1/PC2 역할별로 Site→LightGroup 매핑이 서로 다르고(예: ESite.Side 가 PC1 에선 LIGHT_BAR,
        //  PC2 role 에 따라 LIGHT_BACK 또는 LIGHT_BAR), 게다가 물리 컨트롤러/채널 2개(RING7, ALIGN_COAX,
        //  Custom/Device/LightHandler.cs RegisterLightController)는 이 매핑에 아예 등록돼 있지 않다(별도 Align
        //  플로우 전용) — 컨트롤러/채널 인덱스 → Site → InspectionSequence 로의 안전한 역추적이 자명하지 않다.
        //  그래서 이 코드베이스의 기존 원칙("조명/카메라 에러가 정상 NG 판정으로 위장되면 안 된다")에 따라
        //  더 단순하고 안전한 방향(정밀도 낮음, 과잉 마킹 허용)을 택한다: 조명 에러 발생 시 그 순간
        //  State==Running 인 모든 InspectionSequence(Top/Side/Bottom, 최대 3개)를 전부 마킹한다. 사용자
        //  승인 사항("2번 항목은 명확치 않으면 이 단순 대안 채택 가능") 그대로.
        private void OnLightHandlerError(LightFailEventArgs args) {
            Logging.PrintLog((int)ELogType.Error,
                "[Light] 하드웨어 에러 감지(ErrorType={0}, Controller={1}, Channel={2}({3})) — 진행 중인 검사 사이클을 " +
                "$RESULT E 로 마킹 시도(이미 응답이 나간 사이클은 소급 정정 불가).",
                args.ErrorType, args.Index, args.Channel, args.Name);

            int nMarked = 0;
            for (int i = 0; i < Sequences.Count; i++) {
                SequenceBase seq = Sequences[i];
                if (seq == null) continue;
                if (seq.State != EContextState.Running) continue;

                InspectionSequence inspSeq = seq as InspectionSequence;
                if (inspSeq == null) continue;

                inspSeq.MarkCycleHardwareError();
                nMarked++;
                Logging.PrintLog((int)ELogType.Error,
                    "[Light] Seq={0} 진행 중 사이클에 하드웨어 에러 마킹 완료(다음 $RESULT 는 E).", inspSeq.Name);
            }

            if (nMarked == 0) {
                Logging.PrintLog((int)ELogType.Error,
                    "[Light] 조명 에러 발생 시점에 Running 상태인 시퀀스가 없음 — 마킹 대상 없음(이미 응답이 " +
                    "나갔거나, 검사와 무관한 조명 채널의 에러일 수 있음). 실기 조사 필요.");
            }
        }

    }
}
