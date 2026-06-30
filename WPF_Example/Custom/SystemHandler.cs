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

        //260409 hbk Phase 5: IsDynamicFAIMode 분기 (D-03)
        //260615 hbk Phase 43.2: IsRecipeReady guard — 레시피 비동기 로드 완료 전 TEST 수신 시 NG 거부 (D-C)
        //260626 hbk z_index=$PREP 분리: $TEST z_index 필드 제거 → _lastPrepZIndex 주입.
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
                    "[ALIGN_TEST] AlignFace 범위 외 거부: {0} (유효범위 0~5) //260626 hbk", packet.AlignFace);
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
                        "[ALIGN_TEST] Bottom slot={0} 미티칭 — 모델 없음 NG 반환 //260626 hbk", (int)slot);
                    FillAlignPoseZero(pResult); //260626 hbk PLC 형식 일관성 — pose=0 채움
                    return false;
                }

                //260626 hbk Camera null 가드 — Mode==None 또는 초기화 실패 시 null
                bool bCameraReady = EthernetVisionHandler.Handle.Camera != null;
                if (!bCameraReady)
                {
                    Logging.PrintLog((int)ELogType.Error,
                        "[ALIGN_TEST] 이더넷 카메라 미연결(null) — NG 반환 //260626 hbk");
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
                            "[ALIGN_TEST] Bottom slot={0} grab 실패(null) — NG 반환 //260626 hbk", (int)slot);
                        FillAlignPoseZero(pResult);
                        return false;
                    }

                    //260626 hbk 슬롯 모델로 Run — pose(OffsetXmm/OffsetYmm/ThetaDeg) + Found
                    res = EthernetVisionHandler.Handle.Matcher.Run(img, EEthernetVisionMode.Bottom, slot);
                    if (!res.Found)
                    {
                        Logging.PrintLog((int)ELogType.Error,
                            "[ALIGN_TEST] Bottom slot={0} 검출 실패(Found=false) — NG 반환 //260626 hbk", (int)slot);
                        FillAlignPoseZero(pResult); //260626 hbk 검출 실패 시 pose=0 (T-65-05: 잘못된 보정값 미전송)
                        return false;
                    }

                    //260626 hbk 검출 성공 — pose Items 채움 (D-07)
                    FillAlignPose(pResult, res);
                    Logging.PrintLog((int)ELogType.Trace,
                        "[ALIGN_TEST] Bottom slot={0} PASS off=({1:0.000},{2:0.000}) theta={3:0.000} //260626 hbk",
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
                    "[ALIGN_TEST] RunBottomAlign 예외: {0} //260626 hbk", ex.Message);
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
                    Logging.PrintLog((int)ELogType.Error, "[ALIGN_TEST] Tray 미티칭 — 모델 없음 NG //260630 hbk");
                    FillAlignPoseZero(pResult);
                    return false;
                }

                bool bCameraReady = EthernetVisionHandler.Handle.Camera != null;
                if (!bCameraReady)
                {
                    Logging.PrintLog((int)ELogType.Error, "[ALIGN_TEST] 이더넷 카메라 미연결 — NG //260630 hbk");
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
                        Logging.PrintLog((int)ELogType.Error, "[ALIGN_TEST] Tray grab 실패(null) — NG //260630 hbk");
                        FillAlignPoseZero(pResult);
                        return false;
                    }

                    res = EthernetVisionHandler.Handle.Matcher.Run(img, EEthernetVisionMode.Tray);
                    if (!res.Found)
                    {
                        Logging.PrintLog((int)ELogType.Error, "[ALIGN_TEST] Tray 검출 실패 — NG //260630 hbk");
                        FillAlignPoseZero(pResult);
                        return false;
                    }

                    FillAlignPose(pResult, res);
                    Logging.PrintLog((int)ELogType.Trace,
                        "[ALIGN_TEST] Tray PASS off=({0:0.000},{1:0.000}) theta={2:0.000} //260630 hbk",
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
                Logging.PrintLog((int)ELogType.Error, "[ALIGN_TEST] RunTrayAlign 예외: {0} //260630 hbk", ex.Message);
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
                    "[ALIGN_TEST] ApplyCoaxLightForTray 예외: {0} //260630 hbk", ex.Message);
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
                    "[ALIGN_TEST] ApplyCoaxLightForSlot 예외: {0} //260626 hbk", ex.Message);   //260626 hbk 로그 후 off
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

            bool bIsStart = string.Equals(szCmd, "START", StringComparison.OrdinalIgnoreCase);
            if (bIsStart)
            {
                EthernetVisionHandler.Handle.PickerCal.Reset();
                // 모델 로드 시도 (UI 티칭 완료 전제). 실패 시 경고만 — STEP 에서 자연 실패.
                string loadErr;
                bool bLoaded = EthernetVisionHandler.Handle.PickerCal.TryLoadModel(out loadErr);
                if (!bLoaded)
                {
                    Logging.PrintLog((int)ELogType.Error,
                        "[ALIGN_CALIB] START: 모델 로드 실패 ({0})", loadErr);
                }
                resultPacket.IsPass = true;
                Logging.PrintLog((int)ELogType.Trace, "[ALIGN_CALIB] START — 누적 초기화, model={0}", bLoaded);
                return resultPacket;
            }

            bool bIsStep = string.Equals(szCmd, "STEP", StringComparison.OrdinalIgnoreCase);
            if (bIsStep)
            {
                bool bCameraReady = EthernetVisionHandler.Handle.Camera != null;
                if (!bCameraReady)
                {
                    Logging.PrintLog((int)ELogType.Error, "[ALIGN_CALIB] STEP: 카메라 미연결");
                    return resultPacket;
                }

                HImage img = null;
                try
                {
                    img = EthernetVisionHandler.Handle.Camera.Grab();
                    bool bGrabOk = img != null;
                    if (!bGrabOk)
                    {
                        Logging.PrintLog((int)ELogType.Error, "[ALIGN_CALIB] STEP: Grab 실패");
                        return resultPacket;
                    }

                    //260630 hbk — 사각형 ROI 전환: Row/Col/Radius→Row1/Col1/Row2/Col2
                    double dSearchRow1 = SystemSetting.Handle.CalibSearchRow1;
                    double dSearchCol1 = SystemSetting.Handle.CalibSearchCol1;
                    double dSearchRow2 = SystemSetting.Handle.CalibSearchRow2;
                    double dSearchCol2 = SystemSetting.Handle.CalibSearchCol2;

                    double dFoundRow, dFoundCol;
                    string error;
                    bool bOk = EthernetVisionHandler.Handle.PickerCal.TryAddStep(
                        img, dSearchRow1, dSearchCol1, dSearchRow2, dSearchCol2,
                        out dFoundRow, out dFoundCol, out error);

                    if (bOk)
                    {
                        resultPacket.StepNo = EthernetVisionHandler.Handle.PickerCal.StepCount;
                        resultPacket.IsPass = true;
                        Logging.PrintLog((int)ELogType.Trace,
                            "[ALIGN_CALIB] STEP {0} OK", resultPacket.StepNo);
                    }
                    else
                    {
                        Logging.PrintLog((int)ELogType.Error, "[ALIGN_CALIB] STEP 실패: {0}", error);
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

            bool bIsEnd = string.Equals(szCmd, "END", StringComparison.OrdinalIgnoreCase);
            if (bIsEnd)
            {
                double dRow, dCol, dRad;
                string error;
                bool bOk = EthernetVisionHandler.Handle.PickerCal.TryComputePickerCenter(
                    out dRow, out dCol, out dRad, out error);

                if (bOk)
                {
                    resultPacket.IsPass = true;
                    Logging.PrintLog((int)ELogType.Trace,
                        "[ALIGN_CALIB] END — 피커센터=({0:F2},{1:F2}) r={2:F2}", dRow, dCol, dRad);
                }
                else
                {
                    Logging.PrintLog((int)ELogType.Error, "[ALIGN_CALIB] END 산출 실패: {0}", error);
                }
                return resultPacket;
            }

            bool bIsAbort = string.Equals(szCmd, "ABORT", StringComparison.OrdinalIgnoreCase);
            if (bIsAbort)
            {
                EthernetVisionHandler.Handle.PickerCal.Reset();
                resultPacket.IsPass = true;
                Logging.PrintLog((int)ELogType.Trace, "[ALIGN_CALIB] ABORT — 누적 초기화");
                return resultPacket;
            }

            Logging.PrintLog((int)ELogType.Error, "[ALIGN_CALIB] 알 수 없는 CmdStr: {0}", szCmd);
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
        //260626 hbk v3.0: Op 분기 — 1=ON(z_index 샷 조명 점등) / 0=OFF(사이클 종료 소등). $LIGHT 폐기 대체.
        //  HW 트리거 전환 대비: 조명 ON/OFF 가 $PREP(준비 단계)에 통합 → $TEST(트리거)는 조명 무관.
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
            ackPacket.Op = packet.Op;          //260626 hbk Op echo (1=ON / 0=OFF)
            ackPacket.IsOk = false; // 기본값 FAIL — 성공 시 true 로 덮어씀

            bool bIsOn = packet.Op != 0;       //260626 hbk Op!=0 → ON (미수신 기본 1=ON)
            if (bIsOn)
            {
                _lastPrepZIndex = packet.ZIndex; //260626 hbk ON 일 때만 z_index 저장 → ProcessTest 주입용
                bool bApplied = ApplyPrepToSequences(packet.ZIndex);
                if (bApplied)
                {
                    ackPacket.IsOk = true;
                }
            }
            else
            {
                bool bOff = TurnOffPrepLights(); //260626 hbk Op==0 → 전 시퀀스 소등
                if (bOff)
                {
                    ackPacket.IsOk = true;
                }
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

        //260626 hbk v3.0: $PREP Op==0(사이클 종료 OFF) 처리 — 전 InspectionSequence 소등.
        //  하나라도 InspectionSequence 가 있으면 true(소등 ACK). $LIGHT OFF 대체.
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
