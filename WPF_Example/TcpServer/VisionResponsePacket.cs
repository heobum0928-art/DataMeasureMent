using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ReringProject.Define;
using ReringProject.Sequence;
using ReringProject.Setting; //260622 hbk Phase 48

namespace ReringProject.Network {
    public enum EVisionResponseType {
        RecipeChange,
        RecipeGet,
        SiteStatus,
        Light,
        Test,
        GrabStatus,

        AlignResult,   //260624 hbk Phase 63 AV-09: Align 결과 응답 ($ALIGN_RESULT)
        AlignCalib,    //260624 hbk Phase 63 AV-09: Align 캘리브 ack 응답 ($ALIGN_CALIB)
        PrepAck,       //260625 hbk Phase 64 LIGHT-01: $PREP_ACK 응답
        Alive,         //260625 hbk v3.0: $ALIVE heartbeat 응답
        ResetAck,      //260807 hbk quick-260807-lh7: $RESET_ACK 응답

        Unknown = 999
    }

    public enum EVisionResultType : int{
        NG = 0,
        OK = 1,
        NotExist = 2,
        ANG = 3,            // 12.21
        TECHING = 4,        // 05.20 Insert
    }

    //260623 hbk Phase 49 PROTO-05: 멀티샷 사이클 판정 결과 (D-07). enum 신설 범위 = 이 1개만.
    //  Buffer = 중간 Index 진행 중(NG 포함 가능) / Pass = 마지막 Index 전체 OK / Fail = 마지막 Index NG 있음 or Datum 실패.
    //  라이프사이클 상태는 멤버 bool(m_bCycleHasNG 등)로 표현 — A-2(멤버 상태) 결정과 일관. CycleState 라이프사이클 enum 미도입.
    public enum ECycleResult : int {
        Buffer = 0,
        Pass = 1,
        Fail = 2,
    }

    public enum EVisionSiteStatusType : int {
        Ready = 0,
        Busy = 1,
        Error = 2,
    }

    public class VisionResponsePacket : IDisposable {
        //Send
        public const string CMD_SEND_RECIPE_CHANGE = "SETTING";
        public const string CMD_SEND_RECIPE_GET = "RECIPE_LIST";
        public const string CMD_SEND_SITE_STATUS = "SITE_STATUS";
        public const string CMD_SEND_LIGHT = "LIGHT";
        public const string CMD_SEND_TEST = "RESULT";
        public const string CMD_SEND_GRAB_STATUS = "GRAB_STATUS";
        public const string CMD_SEND_ALIGN_RESULT = "ALIGN_RESULT";   //260624 hbk Phase 63 AV-09: Align 결과 송신 커맨드
        public const string CMD_SEND_ALIGN_CALIB = "ALIGN_CALIB";     //260624 hbk Phase 63 AV-09: Align 캘리브 ack 송신 커맨드
        public const string CMD_SEND_PREP_ACK = "PREP_ACK";           //260625 hbk Phase 64 LIGHT-01: $PREP_ACK 송신 커맨드
        public const string CMD_SEND_ALIVE = "ALIVE";                  //260625 hbk v3.0: $ALIVE heartbeat 송신 커맨드
        public const string CMD_SEND_RESET_ACK = "RESET_ACK";          //260807 hbk quick-260807-lh7: $RESET_ACK 송신 커맨드

        public const string RESULT_OK = "OK";
        public const string RESULT_NG = "NG";
        public const int ALIGN_CALIB_NG_STEP_NO = 97;   //260810 hbk quick-260810-olh: ALIGN_CALIB 실패(NG) 시 명령 종류 무관 고정 N값(제어팀 요청)
        public const string ALIGN_CALIB_NG_CMD = "4";   //260811 hbk plc-spec-260811-alignment: ALIGN_CALIB 실패(NG) 시 명령 종류 무관 고정 CMD값(제어팀 확정 스펙, 엑셀 R179행)

        public const string TEST_RESULT_PASS = "P";
        public const string TEST_RESULT_FAIL = "F";
        public const string TEST_RESULT_NOTEXIST = "N";
        public const string TEST_RESULT_ANGLE_FAIL = "A";           //12.21
        public const string TEST_RESULT_TEACHING = "T";             //05.20 Insert
        public const string TEST_RESULT_ERROR = "E";       //260811 hbk plc-spec-260811-alignment: 카메라/조명 하드웨어 에러 전용(측정은 됐으나 공차 불합격=F 와 구분, 엑셀 63행)

        public const string SITE_STATUS_READY = "READY";
        public const string SITE_STATUS_BUSY = "BUSY";
        public const string SITE_STATUS_ERROR = "ERROR";

        // 260622 hbk Phase 48 PROTO-02: v1.0 RESULT 3단 구분자 ($RESULT:site;P|F|B;count;id=val=OK|NG,...@).
        public const char MSG_RESULT_HEADER_SEP = ';';   // 헤더 구분자 (site/판정/count 사이)
        public const char MSG_RESULT_ITEM_SEP   = ',';   //260807 hbk quick-260807-omy v-next $RESULT 항목목록 폐기로 현재 미사용 (선언은 유지)
        public const char MSG_RESULT_INNER_SEP  = '=';   // 항목 내부 구분자 (id=val=judge)
        public const string TEST_RESULT_BUFFER  = "B";   // cycle 진행 중(Buffer) 판정
        
        public EVisionResponseType ResponseType { get; }

        public string Target { get; set; }

        public string Identifier { get; set; }

        //public int Zone { get; set; }

        public int Site { get; set; }

        public VisionResponsePacket(EVisionResponseType type) {
            ResponseType = type;
        }

        public override string ToString() {
            return Convert(this);
        }

        public static string Convert(VisionResponsePacket packet) {
            string msg = "";//VisionServer.MSG_STX.ToString();
            switch (packet.ResponseType) {
                case EVisionResponseType.GrabStatus:
                    GrabStatusResultPacket grabPacket = packet.AsGrabStatusResult();
                    msg += CMD_SEND_GRAB_STATUS;
                    msg += VisionServer.MSG_CMD_SEPERATOR;
                    //msg += packet.Zone.ToString();
                    //msg += VisionServer.MSG_CONTENTS_SEPERATOR;
                    msg += grabPacket.Site.ToString();
                    msg += VisionServer.MSG_CONTENTS_SEPERATOR;
                    msg += grabPacket.GetResultString();
                    
                    break;
                case EVisionResponseType.Light:
                    LightResultPacket lightPacket = packet.AsLightResult();
                    msg += CMD_SEND_LIGHT;
                    msg += VisionServer.MSG_CMD_SEPERATOR;
                    //msg += lightPacket.Zone.ToString();
                    //msg += VisionServer.MSG_CONTENTS_SEPERATOR;
                    msg += lightPacket.Site.ToString();
                    msg += VisionServer.MSG_CONTENTS_SEPERATOR;
                    if (lightPacket.On == false) {
                        msg += lightPacket.GetOnString();
                    }
                    else {
                        //msg += lightPacket.TestType.ToString();
                        //msg += VisionServer.MSG_CONTENTS_SEPERATOR;
                        msg += lightPacket.GetOnString();
                    }
                    break;
                case EVisionResponseType.RecipeChange:
                    RecipeChangeResultPacket recipeChangePacket = packet.AsRecipeChangeResult();
                    msg += CMD_SEND_RECIPE_CHANGE;
                    msg += VisionServer.MSG_CMD_SEPERATOR;
                    //msg += recipeChangePacket.Zone.ToString();
                    //msg += VisionServer.MSG_CONTENTS_SEPERATOR;
                    msg += recipeChangePacket.GetResultString();

                    break;
                case EVisionResponseType.RecipeGet:
                    RecipeListResultPacket recipeGetPacket = packet.AsRecipeListResult();

                    msg += CMD_SEND_RECIPE_GET;
                    msg += VisionServer.MSG_CMD_SEPERATOR;
                    //msg += recipeGetPacket.Zone.ToString();
                    //msg += VisionServer.MSG_CONTENTS_SEPERATOR;
                    msg += recipeGetPacket.Site.ToString();
                    msg += VisionServer.MSG_CONTENTS_SEPERATOR;
                    if (recipeGetPacket.MaxCount > recipeGetPacket.RecipeList.Count)
                        recipeGetPacket.MaxCount = recipeGetPacket.RecipeList.Count;
                    msg += recipeGetPacket.MaxCount.ToString();
                    msg += VisionServer.MSG_CONTENTS_SEPERATOR;

                    //recipe list
                    for (int i = 0; i < recipeGetPacket.MaxCount; i++) {
                        if (i >= recipeGetPacket.RecipeList.Count)
                            break;
                        msg += recipeGetPacket.RecipeList[i];
                        if (i >= recipeGetPacket.RecipeList.Count - 1)
                            break;
                        if (i <= recipeGetPacket.MaxCount - 1) {
                            msg += VisionServer.MSG_CONTENTS_SEPERATOR;
                        }
                    }

                    break;
                case EVisionResponseType.SiteStatus:
                    SiteStatusResultPacket sitePacket = packet.AsSiteStatusResult();

                    msg += CMD_SEND_SITE_STATUS;
                    msg += VisionServer.MSG_CMD_SEPERATOR;
                    //msg += sitePacket.Zone.ToString();
                    //msg += VisionServer.MSG_CONTENTS_SEPERATOR;
                    msg += sitePacket.Site.ToString();
                    msg += VisionServer.MSG_CONTENTS_SEPERATOR;
                    msg += sitePacket.Result;

                    break;
                case EVisionResponseType.Test:
                    TestResultPacket testPacket = packet.AsTestResult();

                    // 260622 hbk Phase 48 PROTO-02: v1.0 분기 — 3단 구분자 RESULT. v2.6 면 기존 누적 블록 보존(회귀 0).
                    bool bUseV1 = SystemSetting.Handle.UseProtocolV1;
                    if (bUseV1)
                    {
                        msg += BuildResultMessageV1(testPacket);
                        break;
                    }

                    // ↓↓↓ 이하 기존 v2.6 블록 그대로 (회귀 0) ↓↓↓
                    msg += CMD_SEND_TEST;
                    msg += VisionServer.MSG_CMD_SEPERATOR;

                    //msg += testPacket.Zone.ToString();
                    //msg += VisionServer.MSG_CONTENTS_SEPERATOR;

                    msg += testPacket.Site.ToString();
                    msg += VisionServer.MSG_CONTENTS_SEPERATOR;

                    msg += testPacket.InspectionType.ToString();
                    msg += VisionServer.MSG_CONTENTS_SEPERATOR;

                    // 동적 FAI 모드 직렬화
                    if (testPacket.IsDynamicFAI) {
                        msg += testPacket.GetResultString(); // cycle Result, NotExist→'N' 매핑은 GetResultString 이미 보유
                        msg += VisionServer.MSG_CONTENTS_SEPERATOR;
                        msg += testPacket.FAICount.ToString();
                        for (int i = 0; i < testPacket.FAICount; i++) {
                            msg += VisionServer.MSG_CONTENTS_SEPERATOR;
                            var faiData = testPacket.FAIResults[i];
                            // 3-state: P (OK) / N (NotExist, datum-skip) / F (NG 및 fallback — ANG/TECHING 도 'F' 로 매핑, wire 호환)
                            string faiCode;
                            if (faiData.Result == EVisionResultType.OK) faiCode = TEST_RESULT_PASS;
                            else if (faiData.Result == EVisionResultType.NotExist) faiCode = TEST_RESULT_NOTEXIST;
                            else faiCode = TEST_RESULT_FAIL;
                            msg += faiCode;
                            msg += VisionServer.MSG_CONTENTS_SEPERATOR;
                            msg += faiData.DistanceMm.ToString("0.000");
                        }
                    }
                    else if ((testPacket.InspectionType == (int)ETestType.Inspection) && testPacket.Site == (int)ESequence.Bottom)
                    {
                        for (int i = 0; i < 10; i++)
                        {
                            if(testPacket.visionResults[i] == null)
                            {
                                testPacket.visionResults[i] = new VisionResponseListData();
                            }
                            msg += testPacket.GetListResultString(i);   // Bottom 검사 유형에 따른 숫자를 문자로 변경하는 함수 호출.
                            msg += VisionServer.MSG_CONTENTS_SEPERATOR;

                            msg += testPacket.visionResults[i].Angle.ToString("0.000"); //angle
                            msg += VisionServer.MSG_CONTENTS_SEPERATOR;
                            msg += testPacket.visionResults[i].X.ToString("0.000"); //x
                            msg += VisionServer.MSG_CONTENTS_SEPERATOR;
                            msg += testPacket.visionResults[i].Y.ToString("0.000"); //y
                            if (i < 9)
                                msg += VisionServer.MSG_CONTENTS_SEPERATOR;
                        }
                    }
                    else
                    {
                        msg += testPacket.GetResultString();
                        msg += VisionServer.MSG_CONTENTS_SEPERATOR;

                        msg += testPacket.Angle.ToString("0.000"); //angle
                        msg += VisionServer.MSG_CONTENTS_SEPERATOR;
                        msg += testPacket.X.ToString("0.000"); //x
                        msg += VisionServer.MSG_CONTENTS_SEPERATOR;
                        msg += testPacket.Y.ToString("0.000"); //y
                    }

                    //msg += VisionServer.MSG_CONTENTS_SEPERATOR;

                    break;
                case EVisionResponseType.AlignResult:
                    msg += BuildAlignResultMessage(packet.AsAlignResult());   //260624 hbk Phase 63
                    break;
                case EVisionResponseType.AlignCalib:
                    msg += BuildAlignCalibMessage(packet.AsAlignCalib());     //260624 hbk Phase 63
                    break;
                case EVisionResponseType.PrepAck:
                    msg += BuildPrepAckMessage(packet.AsPrepAck()); //260625 hbk Phase 64 LIGHT-01
                    break;
                case EVisionResponseType.Alive:
                    msg += BuildAliveMessage(packet.AsAlive()); //260625 hbk v3.0
                    break;
                case EVisionResponseType.ResetAck:
                    msg += BuildResetAckMessage(packet.AsResetAck()); //260807 hbk quick-260807-lh7
                    break;
                case EVisionResponseType.Unknown:
                    return null;
            }
            return msg;
        }

        // 260622 hbk Phase 48 PROTO-02: v1.0 RESULT 직렬화.
        //260807 hbk quick-260807-omy v-next: $RESULT:site;Type;P|F|B@ (STX/ETX 는 TcpServer 부착).
        //  count/개별 FAI 항목목록은 v-next 에서 와이어에서만 제거되었으며, 내부 FAICount/FAIResults 는 그대로 살아있다(UI·엑셀export 계속 소비).
        //  Datum 샷(FAICount=0)일 때 옛 형식이 만들던 trailing ';' 도 함께 사라진다.
        //260624 hbk Phase 63 PROTO-Type: site 뒤 ;Type; echo 삽입 (Type 빈값이면 ;; 자리 보존).
        private static string BuildResultMessageV1(TestResultPacket testPacket)
        {
            string szMsg = "";
            szMsg += CMD_SEND_TEST;                       // "RESULT"
            szMsg += VisionServer.MSG_CMD_SEPERATOR;      // ':'
            szMsg += testPacket.Site.ToString();
            szMsg += MSG_RESULT_HEADER_SEP;               // ';'
            szMsg += testPacket.Type;                     //260624 hbk Phase 63 Type echo (빈값이면 빈 토큰)
            szMsg += MSG_RESULT_HEADER_SEP;               // ';'  //260624 hbk Phase 63
            szMsg += MapCycleJudgement(testPacket);       // P|F|B
            return szMsg;
        }

        // 260622 hbk Phase 48 PROTO-02: cycle 종합 판정 → P/F/B 매핑. IsBuffer 최우선(진행 중), OK=P, 그 외=F.
        // 판정 '결정' 로직은 Phase 49. 여기선 이미 확정된 Result/IsBuffer 를 문자로 변환만.
        //260811 hbk plc-spec-260811-alignment: HasHardwareError 를 IsBuffer 보다도 먼저 확인 — 카메라
        //  하드웨어 grab 실패는 "측정을 아예 못 했다"는 뜻이라 진행 중(B)이든 완료(P/F)든 사이클 상태와
        //  무관하게 무조건 E 로 나가야 한다(제어팀 확정 스펙, 엑셀 63행). 이 필드는 두 신호가 OR 로 합쳐진
        //  결과다 — (1) 카메라: Action_FAIMeasurement 의 실기 grab 실패 감지(EStep.Grab/GrabOrLoadDatumImage),
        //  (2) 조명(260811 후속): Custom/SystemHandler.OnLightHandlerError(LightHandler.OnError 구독) 가
        //  진행 중(State==Running)인 시퀀스에 대해 동일한 InspectionSequence.MarkCycleHardwareError() 를
        //  호출 — 둘 다 이 한 필드로 수렴하므로 이 메서드 자체는 신호의 출처를 구분하지 않는다.
        private static string MapCycleJudgement(TestResultPacket testPacket)
        {
            bool bIsHardwareError = testPacket.HasHardwareError;
            if (bIsHardwareError)
            {
                return TEST_RESULT_ERROR;
            }

            bool bIsBuffer = testPacket.IsBuffer;
            if (bIsBuffer)
            {
                return TEST_RESULT_BUFFER;
            }

            bool bIsPass = testPacket.Result == EVisionResultType.OK;
            if (bIsPass)
            {
                return TEST_RESULT_PASS;
            }

            return TEST_RESULT_FAIL;
        }

        //260626 hbk v3.0: $ALIGN_RESULT 직렬화.
        //  TRAY:   $ALIGN_RESULT:TRAY,MaterialNo,OK|NG,OffsetX=val,OffsetY=val@
        //  BOTTOM: $ALIGN_RESULT:BOTTOM,MaterialNo,AlignFace(0~3),OK|NG,OffsetX=val,OffsetY=val,Theta=val@
        //  AlignFace(0~5, 6지그): 0=G1_TOP / 1=G1_BOT / 2=G2_TOP / 3=G2_BOT / 4=G2_SIDE1 / 5=G2_SIDE2
        private static string BuildAlignResultMessage(AlignResultPacket packet)
        {
            string szMsg = "";
            szMsg += CMD_SEND_ALIGN_RESULT;
            szMsg += VisionServer.MSG_CMD_SEPERATOR;        // ':'
            szMsg += packet.AlignTarget;
            szMsg += VisionServer.MSG_CONTENTS_SEPERATOR;   // ','
            szMsg += packet.MaterialNo.ToString();
            szMsg += VisionServer.MSG_CONTENTS_SEPERATOR;   // ','
            bool bIsBottom = packet.AlignTarget == "BOTTOM";
            if (bIsBottom)
            {
                szMsg += packet.AlignFace.ToString();       // 0=G1_TOP/1=G1_BOT/2=G2_TOP/3=G2_BOT/4=G2_SIDE1/5=G2_SIDE2
                szMsg += VisionServer.MSG_CONTENTS_SEPERATOR; // ','
            }
            bool bIsPass = packet.IsPass;
            if (bIsPass)
            {
                szMsg += RESULT_OK;                         // "OK"
            }
            else
            {
                szMsg += RESULT_NG;                         // "NG"
            }
            szMsg += VisionServer.MSG_CONTENTS_SEPERATOR;   // ','
            szMsg += BuildAlignItems(packet);               // OffsetX=val,OffsetY=val[,Theta=val]
            return szMsg;
        }

        //260625 hbk v3.0: Align 항목들을 Name=val,... 로 직렬화. per-item 판정 제거.
        private static string BuildAlignItems(AlignResultPacket packet)
        {
            string szItems = "";
            int nCount = packet.Items.Count;
            for (int i = 0; i < nCount; i++)
            {
                AlignResultItem item = packet.Items[i];
                bool bNeedsSeparator = i > 0;
                if (bNeedsSeparator)
                {
                    szItems += VisionServer.MSG_CONTENTS_SEPERATOR; // ','
                }
                szItems += item.ItemName;                   // OffsetX / OffsetY / Theta
                szItems += MSG_RESULT_INNER_SEP;            // '='
                szItems += item.Value.ToString("+0.000;-0.000;+0.000");    // val //260810 hbk quick-260810-cgl: 고정폭 파싱 위해 양수/0 도 '+' 부호 고정 (펨텍 PLC팀 요청)
            }
            return szItems;
        }

        //260810 hbk quick-260810-olh: 제어팀 요청 — N(현재 스텝 번호) 필드를 명령 종류(START/STEP/END/ABORT) 무관하게
        //  항상 출력한다. 성공 시 의미는 ProcessAlignCalib 가 세팅한 packet.StepNo 그대로(START=0/STEP=1~36/END=99/
        //  ABORT=98), 실패(NG) 시엔 명령 종류 무관하게 항상 ALIGN_CALIB_NG_STEP_NO(97) — 이 실패 sentinel 결정을
        //  여기 한 곳으로 중앙화해 ProcessAlignCalib 의 여러 실패 반환 지점(5곳)이 개별로 StepNo=97 을 챙길 필요가
        //  없도록 한다(빠뜨림 방지, 근거: .planning/quick/260810-olh-align-calib-stepno-all-commands/).
        //260811 hbk plc-spec-260811-alignment: 제어팀 확정 스펙(엑셀 R179행) — CMD 필드도 N 값과 동일 원칙으로
        //  실패(NG) 시엔 수신 명령 종류(0~3) 무관하게 항상 ALIGN_CALIB_NG_CMD("4") 고정. 성공 시엔 기존대로
        //  packet.CmdStr echo 유지(회귀 0). CMD/N 두 값 모두 이 한 곳(bIsPass 분기)에서만 결정한다.
        private static string BuildAlignCalibMessage(AlignCalibResultPacket packet)
        {
            string szMsg = "";
            szMsg += CMD_SEND_ALIGN_CALIB;
            szMsg += VisionServer.MSG_CMD_SEPERATOR;        // ':'
            szMsg += packet.AlignTarget;                    // BOTTOM

            bool bIsPass = packet.IsPass;
            string szOutCmd = bIsPass ? packet.CmdStr : ALIGN_CALIB_NG_CMD;
            int nOutStepNo = bIsPass ? packet.StepNo : ALIGN_CALIB_NG_STEP_NO;

            szMsg += VisionServer.MSG_CONTENTS_SEPERATOR;   // ','
            szMsg += szOutCmd;                              // CMD: 성공=수신 숫자 코드 echo(0=START/1=STEP/2=END/3=ABORT), 실패=4 고정

            szMsg += VisionServer.MSG_CONTENTS_SEPERATOR;   // ','
            szMsg += nOutStepNo.ToString();                 // N

            szMsg += VisionServer.MSG_CONTENTS_SEPERATOR;   // ','
            if (bIsPass)
            {
                szMsg += RESULT_OK;                         // "OK"
            }
            else
            {
                szMsg += RESULT_NG;                         // "NG"
            }
            return szMsg;
        }

        //260625 hbk v3.0: $ALIVE:OK@ 직렬화.
        private static string BuildAliveMessage(AliveResponsePacket packet)
        {
            string szMsg = CMD_SEND_ALIVE;
            szMsg += VisionServer.MSG_CMD_SEPERATOR;        // ':'
            szMsg += RESULT_OK;                             // "OK"
            return szMsg;
        }

        //260626 hbk v3.0: $PREP_ACK 직렬화.
        //260806 hbk Phase 71: Op echo 제거 → $PREP_ACK:site,z_index,OK|FAIL@ (구분자 2개).
        //  IsOk=true → OK, false → FAIL. 헝가리언 + if-else.
        private static string BuildPrepAckMessage(PrepAckPacket packet)
        {
            string szMsg = "";
            szMsg += CMD_SEND_PREP_ACK;
            szMsg += VisionServer.MSG_CMD_SEPERATOR;       // ':'
            szMsg += packet.Site.ToString();
            szMsg += VisionServer.MSG_CONTENTS_SEPERATOR;  // ','
            szMsg += packet.ZIndex.ToString();
            szMsg += VisionServer.MSG_CONTENTS_SEPERATOR;  // ','
            bool bIsOk = packet.IsOk;
            if (bIsOk)
            {
                szMsg += "OK";
            }
            else
            {
                szMsg += "FAIL";
            }
            return szMsg;
        }

        //260807 hbk quick-260807-lh7: $RESET_ACK 직렬화 → $RESET_ACK:site,OK|FAIL@ (STX/ETX 는 TcpServer 부착).
        //  PREP_ACK 와 동일 스타일이되 z_index 필드가 없어 구분자가 1개 적다. site 는 요청 echo.
        //  IsOk=false 는 "리셋을 못 했다"(예: 시퀀스가 검사 실행 중이라 건너뜀)는 뜻 — 통신 실패가 아니다.
        private static string BuildResetAckMessage(ResetAckPacket packet)
        {
            string szMsg = "";
            szMsg += CMD_SEND_RESET_ACK;
            szMsg += VisionServer.MSG_CMD_SEPERATOR;       // ':'
            szMsg += packet.Site.ToString();
            szMsg += VisionServer.MSG_CONTENTS_SEPERATOR;  // ','
            bool bIsOk = packet.IsOk;
            if (bIsOk)
            {
                szMsg += "OK";
            }
            else
            {
                szMsg += "FAIL";
            }
            return szMsg;
        }

        public void Dispose() {
        }

        public SiteStatusResultPacket AsSiteStatusResult() {
            if (this.ResponseType != EVisionResponseType.SiteStatus) return null;
            return this as SiteStatusResultPacket;
        }

        public RecipeListResultPacket AsRecipeListResult() {
            if (this.ResponseType != EVisionResponseType.RecipeGet) return null;
            return this as RecipeListResultPacket;
        }

        public RecipeChangeResultPacket AsRecipeChangeResult() {
            if (this.ResponseType != EVisionResponseType.RecipeChange) return null;
            return this as RecipeChangeResultPacket;
        }

        public TestResultPacket AsTestResult() {
            if (this.ResponseType != EVisionResponseType.Test) return null;
            return this as TestResultPacket;
        }

        public LightResultPacket AsLightResult() {
            if (ResponseType != EVisionResponseType.Light) return null;
            return this as LightResultPacket;
        }

        public GrabStatusResultPacket AsGrabStatusResult() {
            if (ResponseType != EVisionResponseType.GrabStatus) return null;
            return this as GrabStatusResultPacket;
        }

        //260624 hbk Phase 63 AV-09: Align 응답 다운캐스트 헬퍼 (As* 패턴).
        public AlignResultPacket AsAlignResult() {
            if (ResponseType != EVisionResponseType.AlignResult) return null;
            return this as AlignResultPacket;
        }

        //260624 hbk Phase 63 AV-09
        public AlignCalibResultPacket AsAlignCalib() {
            if (ResponseType != EVisionResponseType.AlignCalib) return null;
            return this as AlignCalibResultPacket;
        }

        //260625 hbk Phase 64 LIGHT-01
        public PrepAckPacket AsPrepAck() {
            if (ResponseType != EVisionResponseType.PrepAck) return null;
            return this as PrepAckPacket;
        }

        //260625 hbk v3.0: $ALIVE 응답 다운캐스트 헬퍼
        public AliveResponsePacket AsAlive() {
            if (ResponseType != EVisionResponseType.Alive) return null;
            return this as AliveResponsePacket;
        }

        //260807 hbk quick-260807-lh7
        public ResetAckPacket AsResetAck() {
            if (ResponseType != EVisionResponseType.ResetAck) return null;
            return this as ResetAckPacket;
        }
    }

    public class SiteStatusResultPacket : VisionResponsePacket {
        public EVisionSiteStatusType Result { get; set; }

        public SiteStatusResultPacket() : base(EVisionResponseType.SiteStatus){
        }

        public string GetSiteStatusString() {
            switch (Result) {
                case EVisionSiteStatusType.Ready:
                    return SITE_STATUS_READY;
                case EVisionSiteStatusType.Busy:
                    return SITE_STATUS_BUSY;
                case EVisionSiteStatusType.Error:
                    return SITE_STATUS_ERROR;
            }
            return SITE_STATUS_ERROR;
        }

    }
    
    public class RecipeListResultPacket : VisionResponsePacket {
        public int MaxCount { get; set; }
        public List<string> RecipeList { get; } = new List<string>();

        public RecipeListResultPacket() : base(EVisionResponseType.RecipeGet) {
        }
    }

    public class RecipeChangeResultPacket : VisionResponsePacket {
        public EVisionResultType Result { get; set; }

        public RecipeChangeResultPacket() : base(EVisionResponseType.RecipeChange) {
        }

        public string GetResultString() {
            if (Result == EVisionResultType.NG) return RESULT_NG;
            else if (Result == EVisionResultType.OK) return RESULT_OK;
            return "-";
        }

        public void SetResultFromString(string result) {
            if (result == RESULT_OK) Result = EVisionResultType.OK;
            else if (result == RESULT_NG) Result = EVisionResultType.NG;
        }
    }

    // 비전 결과 데이터 클래스
    public class VisionResponseListData
    {
        public EVisionResultType Result { get; set; }
        public double Angle { get; set; }
        public double X { get; set; }
        public double Y { get; set; }

        public VisionResponseListData()
        {
            Result = EVisionResultType.NG;
            Angle = 0;
            X = 0;
            Y = 0;
        }
    }


    // FAI별 개별 측정 결과
    public class FAIResultData {
        public string FAIName { get; set; }
        public EVisionResultType Result { get; set; }
        public double DistanceMm { get; set; }

        public FAIResultData() {
            Result = EVisionResultType.NG;
            DistanceMm = 0;
        }

        public FAIResultData(string name, bool isPass, double distMm) {
            FAIName = name;
            if (isPass) {
                Result = EVisionResultType.OK;
            } else {
                Result = EVisionResultType.NG;
            }
            DistanceMm = distMm;
        }

        // 3-state (P/F/N) 직접 전달용 ctor. 기존 bool ctor 는 호환 유지.
        //  InspectionSequence.AddResponse 가 fai.WasDatumSkipped → NotExist, !fai.IsPass → NG, 그 외 → OK 로 분기 후 호출.
        public FAIResultData(string name, EVisionResultType result, double distMm)
        {
            FAIName = name;
            Result = result;
            DistanceMm = distMm;
        }
    }

    public class TestResultPacket : VisionResponsePacket {
        public int InspectionType { get; set; }
        public string Type { get; set; } = "";   //260624 hbk Phase 63 PROTO-Type: 검사 대상 echo (TOP/BOTTOM/SIDE_1~4)
        public EVisionResultType Result { get; set; }
        public double Angle { get; set; }
        public double X { get; set; }
        public double Y { get; set; }

        // FAI별 동적 결과 리스트
        public List<FAIResultData> FAIResults { get; set; } = new List<FAIResultData>();
        public int FAICount => FAIResults.Count;
        public bool IsDynamicFAI { get; set; } = false;

        // 260622 hbk Phase 48 PROTO-02: v1.0 B(Buffer) 상태 플래그. 직렬화가 P/F 보다 우선 평가. 판정 엔진(Phase 49)이 set.
        public bool IsBuffer { get; set; } = false;

        // 260811 hbk plc-spec-260811-alignment: 카메라 하드웨어 grab 실패 플래그. 직렬화가 IsBuffer 보다도
        //  우선 평가되어 'E' 로 나간다(MapCycleJudgement). InspectionSequence 가 사이클 스코프로 set —
        //  공차 불합격(NG/F)과는 별개로, "측정 자체가 불가능했다"는 뜻이라 무조건 최우선.
        public bool HasHardwareError { get; set; } = false;

        // 비전 결과 데이터를 저장할 List 생성
        public const int MaxListCount = 10;
        public VisionResponseListData[] visionResults = new VisionResponseListData[MaxListCount];

        public TestResultPacket() : base(EVisionResponseType.Test) {
            for (int i = 0; i < MaxListCount; i++)
            {
                if(visionResults[i] == null)
                    visionResults[i] = new VisionResponseListData();
            }
        }
        
        public void SetResultFromString(string result) {
            if (result == TEST_RESULT_PASS) Result = EVisionResultType.OK;
            else if (result == TEST_RESULT_FAIL) Result = EVisionResultType.NG;
            else if (result == TEST_RESULT_NOTEXIST) Result = EVisionResultType.NotExist;
            else if (result == TEST_RESULT_ANGLE_FAIL) Result = EVisionResultType.ANG;      // 12.21
            else if (result == TEST_RESULT_TEACHING) Result = EVisionResultType.TECHING;    // 05.20
        }

        public string GetResultString() {
            switch (Result) {
                case EVisionResultType.NG:
                    return TEST_RESULT_FAIL;
                case EVisionResultType.NotExist:
                    return TEST_RESULT_NOTEXIST;
                case EVisionResultType.ANG:         // 12.21
                    return TEST_RESULT_ANGLE_FAIL; // 12.21
                case EVisionResultType.TECHING:     // 05.20  Insert
                    return TEST_RESULT_TEACHING;    // 05.20  Insert
                case EVisionResultType.OK:
                    return TEST_RESULT_PASS;
            }
            return TEST_RESULT_NOTEXIST;
        }

        public string GetListResultString(int index)
        {
            if (index > MaxListCount - 1)
                return TEST_RESULT_NOTEXIST;
            switch (visionResults[index].Result)
            {
                case EVisionResultType.NG:
                    return TEST_RESULT_FAIL;
                case EVisionResultType.NotExist:
                    return TEST_RESULT_NOTEXIST;
                case EVisionResultType.ANG:
                    return TEST_RESULT_ANGLE_FAIL;
                case EVisionResultType.TECHING: // 05.20 Insert
                    return TEST_RESULT_TEACHING;// 05.20 Insert
                case EVisionResultType.OK:
                    return TEST_RESULT_PASS;
            }
            return TEST_RESULT_NOTEXIST;
        }
    }

    //260624 hbk Phase 63 AV-09: Align 결과 항목 (OffsetX/Y/Theta 공용). id=val=judge 직렬화.
    public class AlignResultItem {
        public string ItemName { get; set; } = "";   // OffsetX / OffsetY / Theta
        public double Value { get; set; }
        public bool IsPass { get; set; } = true;
    }

    //260624 hbk Phase 63 AV-09: $ALIGN_RESULT 응답 패킷. 가변 Items 로 Tray(2)/Bottom(3) 모두 수용.
    //260625 hbk v3.0: MaterialNo 추가 — 자재번호 echo.
    //260626 hbk v3.0: AlignFace int 0~5 echo (6지그) — 0=G1_TOP/1=G1_BOT/2=G2_TOP/3=G2_BOT/4=G2_SIDE1/5=G2_SIDE2. TRAY=-1.
    public class AlignResultPacket : VisionResponsePacket {
        public string AlignTarget { get; set; } = "";   // TRAY / BOTTOM
        public int    MaterialNo  { get; set; } = -1;   //260625 hbk v3.0: 자재번호 echo
        public int    AlignFace   { get; set; } = -1;   //260626 hbk v3.0: BOTTOM 전용 지그 면 인덱스 echo(0=G1_TOP/1=G1_BOT/2=G2_TOP/3=G2_BOT/4=G2_SIDE1/5=G2_SIDE2)
        public bool IsPass { get; set; } = true;
        public List<AlignResultItem> Items { get; set; } = new List<AlignResultItem>();

        public AlignResultPacket() : base(EVisionResponseType.AlignResult) {
        }
    }

    //260624 hbk Phase 63 AV-09: $ALIGN_CALIB 캘리브 ack 응답 패킷.
    //260624 hbk Phase 63: AlignCalibResultPacket 으로 개명 — 수신측 AlignCalibPacket(VisionRequestPacket 파생)과 동명 충돌 회피
    //260625 hbk v3.0: CmdStr/StepNo 추가. AlignTarget=BOTTOM 고정.
    public class AlignCalibResultPacket : VisionResponsePacket {
        public string AlignTarget { get; set; } = "";
        public string CmdStr      { get; set; } = "";   //260625 hbk v3.0: START/STEP/END/ABORT echo
        public int    StepNo      { get; set; } = 0;    //260625 hbk v3.0: STEP 커맨드 시 단계 번호
        public bool IsPass { get; set; } = true;

        public AlignCalibResultPacket() : base(EVisionResponseType.AlignCalib) {
        }
    }

    //260625 hbk v3.0: $ALIVE:OK@ 응답 패킷.
    public class AliveResponsePacket : VisionResponsePacket {
        public AliveResponsePacket() : base(EVisionResponseType.Alive) {
        }
    }

    //260625 hbk Phase 64 LIGHT-01: $PREP_ACK 응답 패킷. IsOk=true → $PREP_ACK:site,z_index,OK@ / IsOk=false → $PREP_ACK:site,z_index,FAIL@
    // Op 프로퍼티 제거 //260806 hbk Phase 71: $PREP Op 필드 폐기 → echo 대상 없음
    public class PrepAckPacket : VisionResponsePacket {
        public int ZIndex { get; set; }
        public bool IsOk { get; set; }

        public PrepAckPacket() : base(EVisionResponseType.PrepAck) {
        }
    }

    //260807 hbk quick-260807-lh7: $RESET_ACK 응답 패킷. IsOk=true → $RESET_ACK:site,OK@ / false → $RESET_ACK:site,FAIL@
    //  site 는 베이스 VisionResponsePacket.Site 사용(요청 echo 전용).
    public class ResetAckPacket : VisionResponsePacket {
        public bool IsOk { get; set; }

        public ResetAckPacket() : base(EVisionResponseType.ResetAck) {
        }
    }

    public class LightResultPacket : VisionResponsePacket {

        public int TestType { get; set; }
        public bool On { get; set; }

        public LightResultPacket() : base(EVisionResponseType.Light) {
        }

        public string GetOnString() {
            if (On) return "1";
            return "0";
        }

    }

    public class GrabStatusResultPacket : VisionResponsePacket {
        public EVisionResultType Result { get; set; }

        public GrabStatusResultPacket() : base(EVisionResponseType.GrabStatus) {
        }

        public string GetResultString() {
            if (Result == EVisionResultType.NG) return RESULT_NG;
            else if (Result == EVisionResultType.OK) return RESULT_OK;
            return "-";
        }

        public void SetResultFromString(string resultStr) {
            if (resultStr == RESULT_NG) Result = EVisionResultType.NG;
            else if (resultStr == RESULT_OK) Result = EVisionResultType.OK;
        }
    }

}
