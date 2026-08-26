using System;
using ReringProject.Setting;
using ReringProject.Utility;

namespace ReringProject.Network {
    public enum VisionRequestType {
        RecipeChange,
        RecipeGet,
        SiteStatus,
        Light,
        Test,
        AlignTest,      //260624 hbk Phase 63 AV-09: $ALIGN_TEST 수신 타입
        AlignCalib,     //260624 hbk Phase 63 AV-09: $ALIGN_CALIB 수신 타입
        Prep,           //260625 hbk Phase 64 LIGHT-01: $PREP 수신 타입
        Alive,          //260625 hbk v3.0: $ALIVE heartbeat 수신 타입
        Reset,          //260807 hbk quick-260807-lh7: $RESET 수신 타입 (z_index/시퀀스 상태 복구용)

        Unknown = 999
    }

    public class VisionRequestPacket : IDisposable {

        //Recv
        public const string CMD_RECV_RECIPE_CHANGE = "RECIPE";
        public const string CMD_RECV_RECIPE_GET = "GET_RECIPE";
        public const string CMD_RECV_SITE_STATUS = "SITE_STATUS";
        public const string CMD_RECV_LIGHT = "LIGHT";
        public const string CMD_RECV_TEST = "TEST";
        public const string CMD_RECV_ALIGN_TEST = "ALIGN_TEST";   //260624 hbk Phase 63 AV-09
        public const string CMD_RECV_ALIGN_CALIB = "ALIGN_CALIB"; //260624 hbk Phase 63 AV-09
        public const string CMD_RECV_PREP = "PREP";               //260625 hbk Phase 64 LIGHT-01: $PREP 수신 커맨드
        public const string CMD_RECV_ALIVE = "ALIVE";             //260625 hbk v3.0: $ALIVE heartbeat 수신 커맨드
        public const string CMD_RECV_RESET = "RESET";             //260807 hbk quick-260807-lh7: $RESET 수신 커맨드

        //260622 hbk Phase 48
        // PROTO-01: v1.0 TEST 유연 파서 상수 ($TEST:site,MaterialNumber,null,z_index@).
        // D-00 매직넘버 금지 — 모든 필드 인덱스/sentinel 을 명명 상수로 선언.
        public const int SENTINEL_NO_MATERIAL = -1;          // 자재번호 미수신 sentinel
        public const string SENTINEL_Z_INDEX_STR = "-1";     // z_index 미수신 sentinel
        public const string TEST_NULL_PLACEHOLDER = "null";  // 예약 'null' 문자열
        private const int TEST_FIELD_SITE = 0;               // 필드 인덱스: site
        //260624 hbk Phase 63 PROTO-Type: Type 필드 삽입 → 자재번호/z_index 인덱스 +1 시프트 (V1 한정).
        private const int TEST_FIELD_TYPE = 1;               //260624 hbk Phase 63 필드 인덱스: Type (TOP/BOTTOM/SIDE_1~4)
        private const int TEST_FIELD_MATERIAL = 2;           //260624 hbk Phase 63 필드 인덱스: 자재번호 (1→2 시프트)
        private const int TEST_MIN_FIELD_SITE = 1;           // site 만 있으면 파싱 시작
        private const int TEST_MIN_FIELD_TYPE = 2;           //260624 hbk Phase 63 Type 필드 존재 최소 길이
        private const int TEST_MIN_FIELD_MATERIAL = 3;       //260624 hbk Phase 63 자재번호 필드 존재 최소 길이 (2→3 시프트)
        // TEST_FIELD_ZINDEX/TEST_MIN_FIELD_ZINDEX 제거 //260626 hbk z_index=$PREP 분리 → $TEST에서 z_index 삭제

        // $PREP 필드 인덱스 — 제어 협의 확정 포맷 $PREP:site,Type,z_index@ 전용(3필드 고정).
        // 구버전 펌웨어는 존재하지 않는다(제어와 동시 교체) — 필드 개수 분기 없음.
        private const int PREP_FIELD_SITE = 0;
        private const int PREP_FIELD_TYPE = 1;
        private const int PREP_FIELD_ZINDEX = 2;
        private const int PREP_FIELD_COUNT = 3;

        public VisionRequestType RequestType { get; }

        public string Sender { get; set; }

        public string Identifier { get; set; }

        //public int Zone { get; set; }

        public int Site { get; set; }

        public VisionRequestPacket(VisionRequestType type) {
            RequestType = type;
        }


        public void Dispose() {
        }

        public override string ToString() {
            return Convert(this);
        }

        //응답 패킷을 string 으로 변환
        public static string Convert(VisionRequestPacket packet) {
            string msg = "";
            switch (packet.RequestType) {
                case VisionRequestType.RecipeChange:
                    RecipeChangePacket recipePacket = packet.AsRecipeChange();
                    msg += CMD_RECV_RECIPE_CHANGE;
                    msg += VisionServer.MSG_CMD_SEPERATOR;
                    //msg += recipePacket.Zone.ToString();
                    //msg += VisionServer.MSG_CONTENTS_SEPERATOR;
                    msg += recipePacket.Site.ToString();
                    msg += VisionServer.MSG_CONTENTS_SEPERATOR;
                    msg += recipePacket.RecipeName;

                    break;
                case VisionRequestType.RecipeGet:
                    RecipeGetPacket getPacket = packet.AsRecipeGet();
                    msg += CMD_RECV_RECIPE_GET;
                    msg += VisionServer.MSG_CMD_SEPERATOR;
                    //msg += getPacket.Zone.ToString();
                    //msg += VisionServer.MSG_CONTENTS_SEPERATOR;
                    msg += getPacket.Site.ToString();
                    msg += VisionServer.MSG_CONTENTS_SEPERATOR;
                    msg += getPacket.MaxCount.ToString();
                    msg += VisionServer.MSG_CONTENTS_SEPERATOR;
                    msg += getPacket.Option.ToString();

                    break;
                case VisionRequestType.SiteStatus:
                    SiteStatusPacket sitePacket = packet.AsSiteStatus();
                    msg += CMD_RECV_SITE_STATUS;
                    msg += VisionServer.MSG_CMD_SEPERATOR;
                    //msg += sitePacket.Zone.ToString();
                    //msg += VisionServer.MSG_CONTENTS_SEPERATOR;
                    msg += sitePacket.Site.ToString();
                    
                    break;
                case VisionRequestType.Test:
                    TestPacket testPacket = packet.AsTest();
                    msg += CMD_RECV_TEST;
                    msg += VisionServer.MSG_CMD_SEPERATOR;
                    //msg += testPacket.Zone.ToString();
                    //msg += VisionServer.MSG_CONTENTS_SEPERATOR;
                    msg += testPacket.Site.ToString();
                    msg += VisionServer.MSG_CONTENTS_SEPERATOR;
                    msg += (int)testPacket.TestType;

                    break;
                case VisionRequestType.Light:
                    LightPacket lightPacket = packet.AsLight();
                    msg += CMD_RECV_LIGHT;
                    msg += VisionServer.MSG_CMD_SEPERATOR;
                    //msg += lightPacket.Zone.ToString();
                    //msg += VisionServer.MSG_CONTENTS_SEPERATOR;
                    msg += lightPacket.Site.ToString();
                    msg += VisionServer.MSG_CONTENTS_SEPERATOR;
                    msg += lightPacket.TestType.ToString();     // 02.06 insert
                    msg += VisionServer.MSG_CONTENTS_SEPERATOR; // 02.06 insert
                    msg += lightPacket.GetOnString();
                    
                    break;
                case VisionRequestType.Unknown:
                    break;
            }
            return msg;
        }

        //string을 패킷형태로 변환
        public static VisionRequestPacket Convert(string msg) {
            if (msg == null) return null;
            
                //header 제거
                int index = msg.IndexOf(VisionServer.MSG_STX);
                if (index < 0) return null;
                msg = msg.Remove(index, 1);

                //trailer 제거
                index = msg.IndexOf(VisionServer.MSG_ETX);
                if (index < 0) return null;
                msg = msg.Remove(index, 1);

                //명령어 분리
                var msgList = msg.Split(VisionServer.MSG_CMD_SEPERATOR);
                if (msgList == null || msgList.Length < 1) return null;

                //260625 hbk v3.0: ALIVE는 내용 필드 없이 '$ALIVE@' 형식 허용
                if (msgList[0] == CMD_RECV_ALIVE) { return new AlivePacket(); }

                //260807 hbk quick-260807-lh7: '$RESET@'(site 없음)도 null 로 떨어뜨리지 않는다 — null 이면 응답 자체가
                //  안 나가 PLC 가 ACK 무한 대기(라인 정지). 413-416번째 줄 PREP 하위호환 주석과 동일한 위험.
                if (msgList[0] == CMD_RECV_RESET && msgList.Length < 2) { return new ResetPacket(); }

                if (msgList.Length < 2) return null;

            //cmd 구분
            VisionRequestPacket packet = null;

            string[] dataList;
            //int zoneNum = 0;
            int siteNum = 0;
            int testKind = 0;
            // testID 로컬 변수 제거 — v1.0/v2.6 파서 메서드로 이전하여 불필요 //260622 hbk Phase 48
            switch (msgList[0]) { //cmd
                case CMD_RECV_RECIPE_CHANGE: //recipe change
                    packet = new RecipeChangePacket();
                    RecipeChangePacket recipePacket = packet.AsRecipeChange();

                    dataList = msgList[1].Split(VisionServer.MSG_CONTENTS_SEPERATOR);
                    if (dataList.Length < 2) return null;
                    //site
                    if (Int32.TryParse(dataList[0], out siteNum) == false) {
                        return null;
                    }
                    recipePacket.Site = siteNum;

                    //recipe name
                    recipePacket.RecipeName = dataList[1];

                    break;
                case CMD_RECV_RECIPE_GET: //get recipe
                    packet = new RecipeGetPacket();
                    RecipeGetPacket recipeGetPacket = packet.AsRecipeGet();

                    dataList = msgList[1].Split(VisionServer.MSG_CONTENTS_SEPERATOR);
                    if (dataList.Length < 3) return null;
                    //site
                    if (Int32.TryParse(dataList[0], out siteNum) == false) {
                        return null;
                    }
                    recipeGetPacket.Site = siteNum;

                    if (Int32.TryParse(dataList[1], out int count) == false) {
                        return null;
                    }
                    recipeGetPacket.MaxCount = count;

                    if (Int32.TryParse(dataList[2], out int option) == false) {
                        return null;
                    }
                    recipeGetPacket.Option = option;

                    break;
                case CMD_RECV_SITE_STATUS: //site status
                    packet = new SiteStatusPacket();
                    SiteStatusPacket sitePacket = packet.AsSiteStatus();

                    dataList = msgList[1].Split(VisionServer.MSG_CONTENTS_SEPERATOR);
                    if (dataList.Length < 1) return null;
                    //site
                    if (Int32.TryParse(dataList[0], out siteNum) == false) {
                        return null;
                    }
                    sitePacket.Site = siteNum;

                    break;
                case CMD_RECV_LIGHT: //light
                    packet = new LightPacket();
                    LightPacket lightPacket = packet.AsLight();

                    dataList = msgList[1].Split(VisionServer.MSG_CONTENTS_SEPERATOR);
                    if (dataList.Length < 2) return null;
                    //site
                    if (Int32.TryParse(dataList[0], out siteNum) == false) {
                        return null;
                    }
                    lightPacket.Site = siteNum;

                    //type          12.21 주석 처리되어 있어 packet.Testype에 데이터 전달이 되지 않았음. 
                    if (Int32.TryParse(dataList[1], out testKind) == false)
                    {
                        return null;
                    }
                    lightPacket.TestType = testKind;
                    if (testKind == 0)
                    {
                        lightPacket.On = false;
                        break;
                    }

                    //state
                    int state;
                    //if (Int32.TryParse(dataList[1], out state) == false)  // Origin $LIGHT:Site,ON/OFF@
                    if (Int32.TryParse(dataList[2], out state) == false)    // 01.12 $LIGHT:Site,Type,ON/OFF@
                    {
                        return null;
                    }

                    if (state == 1) lightPacket.On = true;
                    else lightPacket.On = false;

                    break;
                case CMD_RECV_TEST: //test
                    //260622 hbk Phase 48
                    // PROTO-01: v2.6/v1.0 분기 (D-06). UseProtocolV1=true → 유연 V1 파서, false → 레거시 V2.6 파서.
                    packet = new TestPacket();
                    TestPacket testPacket = packet.AsTest();

                    dataList = msgList[1].Split(VisionServer.MSG_CONTENTS_SEPERATOR);

                    bool bUseV1 = ReringProject.Setting.SystemSetting.Handle.UseProtocolV1; //260622 hbk Phase 48
                    if (bUseV1)
                    {
                        bool bParseV1Ok = TryParseTestFieldsV1(dataList, testPacket); //260622 hbk Phase 48
                        if (!bParseV1Ok) { return null; }
                    }
                    else
                    {
                        bool bParseV26Ok = TryParseTestFieldsV26(dataList, testPacket); //260622 hbk Phase 48
                        if (!bParseV26Ok) { return null; }
                    }

                    break;
                case CMD_RECV_ALIGN_TEST: //260624 hbk Phase 63 AV-09 Align 검사 요청
                    packet = new AlignTestPacket();
                    AlignTestPacket alignTestPacket = packet.AsAlignTest();
                    dataList = msgList[1].Split(VisionServer.MSG_CONTENTS_SEPERATOR);
                    bool bAlignTestOk = TryParseAlignTestFields(dataList, alignTestPacket); //260624 hbk Phase 63
                    if (!bAlignTestOk) { return null; }

                    break;
                case CMD_RECV_ALIGN_CALIB: //260624 hbk Phase 63 AV-09 Align 캘리브레이션 요청
                    packet = new AlignCalibPacket();
                    AlignCalibPacket alignCalibPacket = packet.AsAlignCalib();
                    dataList = msgList[1].Split(VisionServer.MSG_CONTENTS_SEPERATOR);
                    bool bAlignCalibOk = TryParseAlignCalibFields(dataList, alignCalibPacket); //260624 hbk Phase 63
                    if (!bAlignCalibOk) { return null; }

                    break;
                case CMD_RECV_PREP: //260625 hbk Phase 64 LIGHT-01
                    packet = new PrepPacket();
                    PrepPacket prepPacket = packet.AsPrep();
                    dataList = msgList[1].Split(VisionServer.MSG_CONTENTS_SEPERATOR);
                    TryParsePrepFields(dataList, prepPacket);   // 반환값 무시 — 이 파서는 항상 true(파서 주석 참고)
                    break;
                case CMD_RECV_RESET: //260807 hbk quick-260807-lh7
                    packet = new ResetPacket();
                    ResetPacket resetPacket = packet.AsReset();
                    dataList = msgList[1].Split(VisionServer.MSG_CONTENTS_SEPERATOR);
                    TryParseResetFields(dataList, resetPacket);   // 반환값 무시 — 이 파서는 항상 true (아래 주석 참고)
                    break;
            }

            return packet;
        }

        //260622 hbk Phase 48
        // PROTO-01: v2.6 레거시 TEST 파서 — 기존 고정 인덱스 로직 그대로 보존 (D-06 회귀 0).
        // 기존 CMD_RECV_TEST 블록(lines 259-288)의 로직을 byte-identical 추출.
        private static bool TryParseTestFieldsV26(string[] dataList, TestPacket testPacket)
        {
            if (dataList.Length < 3) { return false; }

            int nSiteNum = 0;
            if (Int32.TryParse(dataList[0], out nSiteNum) == false) { return false; }
            testPacket.Site = nSiteNum;

            int nTestKind = 0;
            if (Int32.TryParse(dataList[1], out nTestKind) == false) { return false; }
            testPacket.TestType = nTestKind;

            testPacket.TestID = dataList[2];
            return true;
        }

        //260622 hbk Phase 48
        // PROTO-01: v1.0 유연 TEST 파서. 고정 매직 인덱스 의존 탈피 — 필드 누락 시 sentinel 폴백.
        //  향후 필드 추가/순서 변경 시 이 메서드(+상수)만 수정. D-02.
        // D-00 준수: 헝가리언 + if/else + 조건 bool 변수화 + 30줄 한도(자재번호 헬퍼 분리).
        //260626 hbk z_index=$PREP 분리: $TEST에서 z_index 필드 제거. TestID는 SystemHandler가 _lastPrepZIndex로 주입.
        private static bool TryParseTestFieldsV1(string[] dataList, TestPacket testPacket)
        {
            bool bHasSite = dataList.Length >= TEST_MIN_FIELD_SITE;
            if (!bHasSite) { return false; }

            int nSiteNum = 0;
            bool bSiteValid = Int32.TryParse(dataList[TEST_FIELD_SITE], out nSiteNum);
            if (!bSiteValid) { return false; }
            testPacket.Site = nSiteNum;

            testPacket.Type = ParseTypeField(dataList);        //260624 hbk Phase 63 PROTO-Type
            testPacket.IndexNumber = ParseMaterialField(dataList);
            testPacket.TestID = SENTINEL_Z_INDEX_STR;          //260626 hbk $PREP 분리 — z_index는 SystemHandler._lastPrepZIndex 주입
            return true;
        }

        //260624 hbk Phase 63 PROTO-Type: Type 필드 파싱. 누락/'null'/빈값 → "" 폴백.
        private static string ParseTypeField(string[] dataList)
        {
            bool bHasType = dataList.Length >= TEST_MIN_FIELD_TYPE;
            if (!bHasType) { return ""; }
            string szRaw = dataList[TEST_FIELD_TYPE];
            bool bIsNullPlaceholder = string.IsNullOrEmpty(szRaw) || szRaw == TEST_NULL_PLACEHOLDER;
            if (bIsNullPlaceholder) { return ""; }
            return szRaw;
        }

        //260622 hbk Phase 48
        // PROTO-01: 자재번호 필드 파싱. 누락/'null'/비정수 → SENTINEL_NO_MATERIAL.
        private static int ParseMaterialField(string[] dataList)
        {
            bool bHasMaterial = dataList.Length >= TEST_MIN_FIELD_MATERIAL;
            if (!bHasMaterial) { return SENTINEL_NO_MATERIAL; }

            string szRaw = dataList[TEST_FIELD_MATERIAL];
            bool bIsNullPlaceholder = string.IsNullOrEmpty(szRaw) || szRaw == TEST_NULL_PLACEHOLDER;
            if (bIsNullPlaceholder) { return SENTINEL_NO_MATERIAL; }

            int nMaterial = 0;
            bool bMaterialValid = Int32.TryParse(szRaw, out nMaterial);
            if (!bMaterialValid) { return SENTINEL_NO_MATERIAL; }
            return nMaterial;
        }

        // ParseZIndexField 제거 //260626 hbk z_index=$PREP 분리 — $TEST z_index 파싱 불필요

        //260626 hbk v3.0: ALIGN_TEST 수신 파서.
        //  dataList[0]=AlignTarget(TRAY/BOTTOM), [1]=MaterialNo(int), [2]=모드(skip).
        //  BOTTOM이면 [3]=AlignFace(int: 0=G1_TOP/1=G1_BOT/2=G2_TOP/3=G2_BOT/4=G2_SIDE1/5=G2_SIDE2). TRAY는 AlignFace 없음.
        private static bool TryParseAlignTestFields(string[] dataList, AlignTestPacket alignPacket)
        {
            bool bHasBase = dataList != null && dataList.Length >= 2;
            if (!bHasBase) { return false; }
            alignPacket.AlignTarget = dataList[0];

            int nMaterialNo = 0;
            bool bMaterialOk = Int32.TryParse(dataList[1], out nMaterialNo);
            if (!bMaterialOk) { return false; }
            alignPacket.MaterialNo = nMaterialNo;

            // dataList[2]=모드(skip)

            bool bIsBottom = alignPacket.AlignTarget == "BOTTOM";
            if (bIsBottom)
            {
                bool bHasFace = dataList.Length >= 4;
                if (!bHasFace) { return false; }
                int nAlignFace = -1;
                Int32.TryParse(dataList[3], out nAlignFace); //260626 hbk 0=G1_TOP/1=G1_BOT/2=G2_TOP/3=G2_BOT/4=G2_SIDE1/5=G2_SIDE2
                alignPacket.AlignFace = nAlignFace;
            }
            return true;
        }

        //260625 hbk v3.0: ALIGN_CALIB 수신 파서.
        //260807 hbk quick-260807-omy v-next: dataList[0]=BOTTOM(고정), [1]=CmdStr(숫자 코드 "0"~"3" = START/STEP/END/ABORT). AlignFace 제거.
        private static bool TryParseAlignCalibFields(string[] dataList, AlignCalibPacket alignPacket)
        {
            bool bHasFields = dataList != null && dataList.Length >= 2;
            if (!bHasFields) { return false; }
            alignPacket.AlignTarget = dataList[0];  // BOTTOM (고정)
            alignPacket.CmdStr = dataList[1];       //260807 hbk quick-260807-omy 숫자 코드 문자열(0=START/1=STEP/2=END/3=ABORT)
            return true;
        }

        // $PREP 수신 파서 — $PREP:site,Type,z_index@ (3필드 고정).
        //  절대 false 를 반환하지 않는다. 규격 위반 입력은 IsRequestValid=false 로 표시해서 넘기고,
        //  ProcessPrep 이 $PREP_ACK ... FAIL 을 회신한다. false 를 반환하면 호출부가 null 을 돌려
        //  응답 자체가 사라지고 PLC 가 ACK 무한 대기(라인 정지)에 빠진다.
        // ⚠ 알려진 제약(Phase 73): 구 펌웨어의 $PREP:site,z_index,Op@ 도 3필드라 개수로는 구분되지 않는다.
        //  그 패킷이 오면 Type=z_index / z_index=Op 로 조용히 오파싱된다(예외·FAIL 없음).
        //  제어와 동시 교체를 전제로 수용한 위험이다 — 구 펌웨어와 혼용하지 말 것.
        private static bool TryParsePrepFields(string[] dataList, PrepPacket prepPacket)
        {
            prepPacket.IsRequestValid = false;   // 아래 전 항목을 통과해야만 true 로 승격
            prepPacket.Site = 0;
            prepPacket.Type = "";
            prepPacket.ZIndex = 0;

            bool bHasExactFields = dataList != null && dataList.Length == PREP_FIELD_COUNT;
            if (!bHasExactFields)
            {
                int nGotLength = 0;
                if (dataList != null) { nGotLength = dataList.Length; }
                Logging.PrintLog((int)ELogType.Error,
                    "[PREP] 규격 위반 — 필드 {0}개 수신(기대 {1}개: site,Type,z_index). FAIL ACK 회신.",
                    nGotLength, PREP_FIELD_COUNT);
                return true;
            }

            int nSite = 0;
            bool bSiteOk = Int32.TryParse(dataList[PREP_FIELD_SITE], out nSite);
            if (!bSiteOk)
            {
                Logging.PrintLog((int)ELogType.Error, "[PREP] site 파싱 실패 — 원본='{0}'. FAIL ACK 회신.", dataList[PREP_FIELD_SITE]);
                return true;
            }

            int nZIndex = 0;
            bool bZIndexOk = Int32.TryParse(dataList[PREP_FIELD_ZINDEX], out nZIndex);
            if (!bZIndexOk)
            {
                Logging.PrintLog((int)ELogType.Error, "[PREP] z_index 파싱 실패 — 원본='{0}'. FAIL ACK 회신.", dataList[PREP_FIELD_ZINDEX]);
                return true;
            }

            string szType = dataList[PREP_FIELD_TYPE];
            if (szType == null) { szType = ""; }
            szType = szType.Trim();
            bool bHasType = szType.Length > 0;
            if (!bHasType)
            {
                Logging.PrintLog((int)ELogType.Error, "[PREP] Type 필드 비어 있음. FAIL ACK 회신.");
                return true;
            }

            prepPacket.Site = nSite;
            prepPacket.Type = szType;
            prepPacket.ZIndex = nZIndex;
            prepPacket.IsRequestValid = true;
            return true;
        }

        //260807 hbk quick-260807-lh7: $RESET 수신 파서. dataList[0]=site(echo 전용).
        //  절대 false 를 반환하지 않는다 — false 면 호출부가 null 을 반환해 응답이 안 나가고 PLC 가 ACK 를
        //  무한 대기(라인 정지)한다. TryParsePrepFields(413-416번째 줄) 하위호환 주석이 기록한 그 위험을
        //  $RESET 에서는 아예 구조적으로 제거한다: site 파싱 실패는 0 폴백, 필드 초과는 무시.
        //  site 는 ACK echo 에만 쓰이고 라우팅에는 관여하지 않으므로 0 폴백이 오동작을 만들지 않는다.
        private static bool TryParseResetFields(string[] dataList, ResetPacket resetPacket)
        {
            int nSite = 0;
            bool bHasField = dataList != null && dataList.Length >= 1;
            if (bHasField)
            {
                Int32.TryParse(dataList[0], out nSite);   // 실패 시 nSite=0 유지 (out 규약)
            }
            resetPacket.Site = nSite;
            return true;
        }

        public RecipeChangePacket AsRecipeChange() {
            if (RequestType != VisionRequestType.RecipeChange) return null;
            RecipeChangePacket recipePacket = this as RecipeChangePacket;
            return recipePacket;
        }

        public SiteStatusPacket AsSiteStatus() {
            if (RequestType != VisionRequestType.SiteStatus) return null;
            SiteStatusPacket sitePacket = this as SiteStatusPacket;
            return sitePacket;
        }

        public LightPacket AsLight() {
            if (RequestType != VisionRequestType.Light) return null;
            LightPacket lightPacket = this as LightPacket;
            return lightPacket;
        }

        public TestPacket AsTest() {
            if (RequestType != VisionRequestType.Test) return null;
            TestPacket testPacket = this as TestPacket;
            return testPacket;
        }

        public RecipeGetPacket AsRecipeGet() {
            if (RequestType != VisionRequestType.RecipeGet) return null;
            RecipeGetPacket recipeGetPacket = this as RecipeGetPacket;
            return recipeGetPacket;
        }

        //260624 hbk Phase 63 AV-09
        public AlignTestPacket AsAlignTest() {
            if (RequestType != VisionRequestType.AlignTest) return null;
            return this as AlignTestPacket;
        }

        //260624 hbk Phase 63 AV-09
        public AlignCalibPacket AsAlignCalib() {
            if (RequestType != VisionRequestType.AlignCalib) return null;
            return this as AlignCalibPacket;
        }

        //260625 hbk Phase 64 LIGHT-01
        public PrepPacket AsPrep() {
            if (RequestType != VisionRequestType.Prep) return null;
            return this as PrepPacket;
        }

        //260625 hbk v3.0: $ALIVE heartbeat 다운캐스트 헬퍼
        public AlivePacket AsAlive() {
            if (RequestType != VisionRequestType.Alive) return null;
            return this as AlivePacket;
        }

        //260807 hbk quick-260807-lh7
        public ResetPacket AsReset() {
            if (RequestType != VisionRequestType.Reset) return null;
            return this as ResetPacket;
        }

    }



    public class RecipeChangePacket : VisionRequestPacket {

        public string RecipeName { get; set; }

        public RecipeChangePacket() : base(VisionRequestType.RecipeChange) {
        }
    }

    public class SiteStatusPacket : VisionRequestPacket {

        public SiteStatusPacket(VisionRequestPacket packet) : base(VisionRequestType.SiteStatus) {
            Site = packet.Site;
        }
        public SiteStatusPacket() : base(VisionRequestType.SiteStatus) {
        }
    }

    public class LightPacket : VisionRequestPacket {
        public string Identifier2 { get; set; }

        public int TestType { get; set; }
        public bool On { get; set; }

        public LightPacket() : base(VisionRequestType.Light) {
        }

        public string GetOnString() {
            if (On) return "1";
            return "0";
        }
    }

    public class TestPacket : VisionRequestPacket {
        public int TestType { get; set; }
        public string TestID { get; set; }

        public string Identifier2 { get; set; }

        //260622 hbk Phase 48
        // PROTO-01: 자재번호 (v1.0 $TEST 두 번째 필드). 미수신/null/파싱실패 → SENTINEL_NO_MATERIAL(-1).
        // 자재번호 전파 체인의 출발점 — Wave 2 Plan 04 가 소비.
        public int IndexNumber { get; set; } = SENTINEL_NO_MATERIAL;

        //260624 hbk Phase 63 PROTO-Type: 검사 대상 토큰 (TOP/BOTTOM/SIDE_1~4). 미수신/null/빈값 → "" (INI/미수신 안전).
        public string Type { get; set; } = "";

        public TestPacket() : base(VisionRequestType.Test) {
        }
    }

    //260624 hbk Phase 63 AV-09: $ALIGN_TEST 수신 패킷.
    //260625 hbk v3.0: MaterialNo 추가([1]=자재번호). AlignFace 는 BOTTOM 전용([3]).
    //260626 hbk v3.0: AlignFace int 0~5 (6지그) — 0=G1_TOP/1=G1_BOT/2=G2_TOP/3=G2_BOT/4=G2_SIDE1/5=G2_SIDE2. TRAY=비사용(-1).
    public class AlignTestPacket : VisionRequestPacket {
        public string AlignTarget { get; set; } = "";   //260624 hbk 라우팅 대상(TRAY/BOTTOM)
        public int    MaterialNo  { get; set; } = -1;   //260625 hbk v3.0: 자재번호 echo용
        public int    AlignFace   { get; set; } = -1;   //260626 hbk BOTTOM 전용: 지그 면 인덱스(0=G1_TOP/1=G1_BOT/2=G2_TOP/3=G2_BOT/4=G2_SIDE1/5=G2_SIDE2). TRAY=-1

        public AlignTestPacket() : base(VisionRequestType.AlignTest) {
        }
    }

    //260624 hbk Phase 63 AV-09: $ALIGN_CALIB 수신 패킷.
    //260625 hbk v3.0: CmdStr 추가([1]=START/STEP/END/ABORT). AlignFace 제거.
    //260807 hbk quick-260807-omy v-next: CmdStr 값이 텍스트에서 숫자 코드로 전환. CMD_CODE_* 가 단일 진실 원천 —
    //  Custom/SystemHandler.cs 와 VisionResponsePacket.cs 두 파일이 이 상수를 참조한다(값 복제 금지).
    public class AlignCalibPacket : VisionRequestPacket {
        public const int CMD_CODE_START = 0;   //260807 hbk quick-260807-omy
        public const int CMD_CODE_STEP  = 1;   //260807 hbk quick-260807-omy
        public const int CMD_CODE_END   = 2;   //260807 hbk quick-260807-omy
        public const int CMD_CODE_ABORT = 3;   //260807 hbk quick-260807-omy

        public string AlignTarget { get; set; } = "";   //260624 hbk 라우팅 대상(BOTTOM 고정)
        public string CmdStr      { get; set; } = "";   //260807 hbk quick-260807-omy v-next: 숫자 코드 문자열("0"~"3") — CMD_CODE_START/STEP/END/ABORT

        public AlignCalibPacket() : base(VisionRequestType.AlignCalib) {
        }
    }

    //260625 hbk v3.0: $ALIVE heartbeat 수신 패킷. 내용 필드 없음.
    public class AlivePacket : VisionRequestPacket {
        public AlivePacket() : base(VisionRequestType.Alive) {
        }
    }

    //260625 hbk Phase 64 LIGHT-01: $PREP 수신 패킷. ZIndex = 조명 세팅 대상 Shot z_index.
    // Op 프로퍼티 제거 //260806 hbk Phase 71: $PREP 는 항상 점등 의미 — 소등은 사이클 P/F 확정 시 자동
    public class PrepPacket : VisionRequestPacket {
        public int ZIndex { get; set; }

        // 검사 대상 코드(0=TOP / 1=BOTTOM / 2~5=SIDE_1~4). 라우팅 해석은 ResourceMap 이 담당한다.
        public string Type { get; set; } = "";

        // 수신 필드가 규격($PREP:site,Type,z_index@)을 만족했는지. false 면 ProcessPrep 이 FAIL ACK 를 회신한다.
        // 파서가 실패를 이 플래그로 넘기는 이유: 파서가 false 를 반환하면 호출부가 null 을 반환해
        // 응답 자체가 안 나가고 PLC 가 ACK 를 무한 대기(라인 정지)한다. TryParseResetFields 와 같은 설계.
        public bool IsRequestValid { get; set; } = true;

        public PrepPacket() : base(VisionRequestType.Prep) {
        }
    }

    //260807 hbk quick-260807-lh7: $RESET 수신 패킷. 고유 필드 없음 — site 는 베이스 VisionRequestPacket.Site 사용(ACK echo 전용).
    public class ResetPacket : VisionRequestPacket {
        public ResetPacket() : base(VisionRequestType.Reset) {
        }
    }

    public class RecipeGetPacket : VisionRequestPacket {
        public int MaxCount { get; set; }
        public int Option { get; set; }

        public RecipeGetPacket() : base(VisionRequestType.RecipeGet) {
        }
    }

}
