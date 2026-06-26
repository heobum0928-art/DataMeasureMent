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

namespace ReringProject {
    public sealed partial class SystemHandler {
        //project 별, sequence 정의
        private void MainRun() {
            //send test response message
            for (int i = 0; i < Sequences.Count; i++) {
                TestResultPacket response = Sequences[i].PopResponse();
                if (response == null) continue;
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

        private LightResultPacket ProcessLightSet(LightPacket packet) {
            LightResultPacket resultPacket = new LightResultPacket();

            resultPacket.Target = packet.Sender;
            resultPacket.Site = packet.Site;
            Debug.WriteLine($"Packet.TestType:{packet}");
            resultPacket.TestType = packet.TestType;

            if (Sequences[packet.Identifier] != null) {
                SequenceBase seq = Sequences[packet.Identifier];
                if (seq != null) {
                    if(packet.TestType == 0) //off
                    {
                        if (Lights.SetOnOff(packet.Identifier, packet.On) == false) {
                            Thread.Sleep(50);
                            resultPacket.On = !packet.On;
                        }
                        else {
                            Thread.Sleep(50);
                            resultPacket.On = packet.On;
                        }
                        return resultPacket;
                    }
                    int actIndex = seq.GetIndexOf(packet.Identifier2);
                    ActionBase act = seq.GetAction(actIndex);
                    if (act != null) {
                        if(act.Param is CameraSlaveParam) {
                            CameraSlaveParam camParam = act.Param as CameraSlaveParam;

                            /*
                            // 03.19 주석처리
                            if (Lights.SetLevel(camParam.LightGroupName, camParam.LightLevel) == false) {
                                //response false
                                Thread.Sleep(50);
                                resultPacket.On = !packet.On;
                            }
                            if (Lights.SetOnOff(camParam.LightGroupName, packet.On) == false) {
                                //response false
                                Thread.Sleep(50);
                                resultPacket.On = !packet.On;
                            }
                            else {
                                Thread.Sleep(50);
                                resultPacket.On = packet.On;
                            }
                            */

                            if (Lights.SetLevel(camParam.LightGroupName, camParam.LightLevel) == false)
                            {
                                resultPacket.On = !packet.On;
                            }
                            else
                            {
                                resultPacket.On = packet.On;
                            }

                            if (Lights.SetOnOff(camParam.LightGroupName, packet.On) == false)
                            {
                                resultPacket.On = !packet.On;
                            }
                            else
                            {
                                resultPacket.On = packet.On;
                            }
                        }
                    }
                }
                return resultPacket;
            }

            //sequence not have identifier
            if(Lights.SetOnOff(packet.Identifier, packet.On) == false) {
                resultPacket.On = !packet.On;
            }
            else {
                resultPacket.On = packet.On;
            }

            return resultPacket;
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

        //260409 hbk Phase 5: IsDynamicFAIMode 분기 (D-03)
        //260615 hbk Phase 43.2: IsRecipeReady guard — 레시피 비동기 로드 완료 전 TEST 수신 시 NG 거부 (D-C)
        //  IsInitializeFail 패턴과 동일. false 반환 → MainRun 에서 SendTestError(NG) 응답.
        private bool ProcessTest(TestPacket packet) {
            if (!IsRecipeReady) {
                Logging.PrintLog((int)ELogType.Error, "[RECIPE] TEST rejected — recipe not yet loaded (IsRecipeReady=false)");
                return false;
            }
            if (Sequences.IsDynamicFAIMode) {
                string seqName = packet.Identifier;
                SequenceBase seq = Sequences[seqName];
                if (seq == null) return false;
                return seq.StartAll(packet);
            }
            return Sequences.Start(packet);
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

        //260624 hbk Phase 63 AV-09: $ALIGN_TEST 처리.
        //260625 hbk Phase 64 ALIGN-FACE: AlignFace 로그 추가. 알고리즘 면별 라우팅은 향후 phase에서 확장.
        private AlignResultPacket ProcessAlignTest(AlignTestPacket packet)
        {
            AlignResultPacket resultPacket = new AlignResultPacket();
            bool bHasPacket = packet != null;
            if (!bHasPacket)
            {
                return null;
            }
            resultPacket.Target = packet.Sender;
            resultPacket.AlignTarget = packet.AlignTarget;
            resultPacket.MaterialNo = packet.MaterialNo;  //260625 hbk v3.0: 자재번호 echo
            resultPacket.AlignFace = packet.AlignFace;    //260626 hbk v3.0: 지그 면 인덱스 echo(0=TOP/1=BOTTOM/2=SIDE_1/3=SIDE_2)
            resultPacket.IsPass = true;   // [가정] 측정 연계 전까지 ack

            bool bIsBottom = packet.AlignTarget == "BOTTOM";
            if (bIsBottom)
            {
                Logging.PrintLog((int)ELogType.Trace,
                    "[ALIGN_TEST] Target=BOTTOM, Face={0}(0=TOP/1=BOTTOM/2=SIDE_1/3=SIDE_2) //260626 hbk", packet.AlignFace);
            }
            return resultPacket;
        }

        //260624 hbk Phase 63 AV-09: $ALIGN_CALIB 처리 — 캘리브 ack 응답.
        //260625 hbk v3.0: CmdStr echo 추가. AlignFace 제거됨.
        private AlignCalibResultPacket ProcessAlignCalib(AlignCalibPacket packet)
        {
            AlignCalibResultPacket resultPacket = new AlignCalibResultPacket();
            bool bHasPacket = packet != null;
            if (!bHasPacket)
            {
                return null;
            }
            resultPacket.Target = packet.Sender;
            resultPacket.AlignTarget = packet.AlignTarget;
            resultPacket.CmdStr = packet.CmdStr;    //260625 hbk v3.0: echo back
            resultPacket.IsPass = true;
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
        //  z_index → 이 PC InspectionSequence 찾기 → ApplyShotLights() 호출 → PrepAck 반환.
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

            bool bApplied = ApplyPrepToSequences(packet.ZIndex);
            if (bApplied)
            {
                ackPacket.IsOk = true;
            }
            return ackPacket;
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

        //260510 hbk Phase 21: BUF-02 channel #1 — recipe change buffer flush wire-up (D-02 / D-03)
        private void WireBufferLifecycle() {
            //260510 hbk Phase 21: OnRecipeChanged subscriber 등록 — Sequences 가 SequenceHandler.Handle 로 초기화된 후 호출되어야 함
            Sequences.OnRecipeChanged += OnRecipeChanged_FlushBuffers;
        }

        //260510 hbk Phase 21: BUF-02 channel #1 — Release 시점 unsubscribe (subscriber lifecycle 보호 — D-04 Claude's Discretion)
        internal void UnwireBufferLifecycle() {
            //260510 hbk Phase 21: 멱등 — 미등록 상태에서도 안전 (delegate -= null 무동작)
            Sequences.OnRecipeChanged -= OnRecipeChanged_FlushBuffers;
        }

        //260510 hbk Phase 21: BUF-02 channel #1 — recipe change 훅 (wire/unwire lifecycle 유지용)
        private void OnRecipeChanged_FlushBuffers(object sender, RecipeChangedEventArgs args) {
            //260511 hbk Phase 21 hotfix: ClearShots() 제거 — LoadRecipe 완료 후 OnRecipeChanged 발화 시
            //  이 훅이 방금 로드된 Shots 컬렉션을 전부 삭제하는 silent data-loss 유발.
            //  LoadPhase6Format 내부에서 Shots 재구성을 직접 수행하므로 여기서 ClearShots 호출 불필요.
            //  app shutdown 시 buffer dispose 는 Release() 의 channel #3 (SystemHandler.cs:176) 이 담당.
        }

    }
}
