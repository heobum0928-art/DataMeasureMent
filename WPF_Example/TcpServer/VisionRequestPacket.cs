using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReringProject.Network {
    public enum VisionRequestType {
        RecipeChange,
        RecipeGet,
        SiteStatus,
        Light,
        Test,
        AlignTest,      //260624 hbk Phase 63 AV-09: $ALIGN_TEST 수신 타입
        AlignCalib,     //260624 hbk Phase 63 AV-09: $ALIGN_CALIB 수신 타입

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
        private const int TEST_FIELD_ZINDEX = 4;             //260624 hbk Phase 63 필드 인덱스: z_index (3→4 시프트)
        private const int TEST_MIN_FIELD_SITE = 1;           // site 만 있으면 파싱 시작
        private const int TEST_MIN_FIELD_TYPE = 2;           //260624 hbk Phase 63 Type 필드 존재 최소 길이
        private const int TEST_MIN_FIELD_MATERIAL = 3;       //260624 hbk Phase 63 자재번호 필드 존재 최소 길이 (2→3 시프트)
        private const int TEST_MIN_FIELD_ZINDEX = 5;         //260624 hbk Phase 63 z_index 필드 존재 최소 길이 (4→5 시프트)

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
                if (msgList == null || msgList.Length < 2) return null;

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
                    /*
                    //zone
                    if(Int32.TryParse(dataList[0], out zoneNum) == false) {
                        return null;
                    }
                    recipePacket.Zone = zoneNum;
                    */
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
                    /*
                    //zone
                    if (Int32.TryParse(dataList[0], out zoneNum) == false) {
                        return null;
                    }
                    recipeGetPacket.Zone = zoneNum;
                    */
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
                    /*
                    //zone
                    if (Int32.TryParse(dataList[0], out zoneNum) == false) {
                        return null;
                    }
                    sitePacket.Zone = zoneNum;
                    */
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
                    /*
                    //zone
                    if (Int32.TryParse(dataList[0], out zoneNum) == false) {
                        return null;
                    }
                    lightPacket.Zone = zoneNum;
                    */
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
        // D-00 준수: 헝가리언 + if/else + 조건 bool 변수화 + 30줄 한도(자재번호/z_index 헬퍼 분리).
        private static bool TryParseTestFieldsV1(string[] dataList, TestPacket testPacket)
        {
            bool bHasSite = dataList.Length >= TEST_MIN_FIELD_SITE;
            if (!bHasSite) { return false; }

            int nSiteNum = 0;
            bool bSiteValid = Int32.TryParse(dataList[TEST_FIELD_SITE], out nSiteNum);
            if (!bSiteValid) { return false; }
            testPacket.Site = nSiteNum;

            testPacket.Type = ParseTypeField(dataList);   //260624 hbk Phase 63 PROTO-Type
            testPacket.IndexNumber = ParseMaterialField(dataList);
            testPacket.TestID = ParseZIndexField(dataList);
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

        //260622 hbk Phase 48
        // PROTO-01: z_index 필드 파싱. 누락/비정수 → SENTINEL_Z_INDEX_STR.
        private static string ParseZIndexField(string[] dataList)
        {
            bool bHasZIndex = dataList.Length >= TEST_MIN_FIELD_ZINDEX;
            if (!bHasZIndex) { return SENTINEL_Z_INDEX_STR; }

            int nZIndex = 0;
            bool bZValid = Int32.TryParse(dataList[TEST_FIELD_ZINDEX], out nZIndex);
            if (!bZValid) { return SENTINEL_Z_INDEX_STR; }
            return nZIndex.ToString();
        }

        //260624 hbk Phase 63 PROTO-Type/AV-09: ALIGN_TEST 수신 파서. [가정] 페이로드 = target 토큰(TRAY/BOTTOM 등).
        //  Phase 62(Align 결과 모델) 미확정 → 라우팅용 Target 만 추출, 상세 필드는 향후 확장.
        private static bool TryParseAlignTestFields(string[] dataList, AlignTestPacket alignPacket)
        {
            bool bHasTarget = dataList != null && dataList.Length >= 1;
            if (!bHasTarget) { return false; }
            alignPacket.AlignTarget = dataList[0];
            return true;
        }

        //260624 hbk Phase 63 PROTO-Type/AV-09: ALIGN_CALIB 수신 파서. [가정] 페이로드 = target 토큰(TRAY/BOTTOM 등).
        //  Phase 62(Align 결과 모델) 미확정 → 라우팅용 Target 만 추출, 상세 필드는 향후 확장.
        private static bool TryParseAlignCalibFields(string[] dataList, AlignCalibPacket alignPacket)
        {
            bool bHasTarget = dataList != null && dataList.Length >= 1;
            if (!bHasTarget) { return false; }
            alignPacket.AlignTarget = dataList[0];
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

    //260624 hbk Phase 63 AV-09: $ALIGN_TEST 수신 패킷. AlignTarget = 라우팅 대상(TRAY/BOTTOM).
    public class AlignTestPacket : VisionRequestPacket {
        public string AlignTarget { get; set; } = "";   //260624 hbk Phase 63 라우팅 대상(TRAY/BOTTOM)

        public AlignTestPacket() : base(VisionRequestType.AlignTest) {
        }
    }

    //260624 hbk Phase 63 AV-09: $ALIGN_CALIB 수신 패킷. AlignTarget = 라우팅 대상(TRAY/BOTTOM).
    public class AlignCalibPacket : VisionRequestPacket {
        public string AlignTarget { get; set; } = "";   //260624 hbk Phase 63 라우팅 대상(TRAY/BOTTOM)

        public AlignCalibPacket() : base(VisionRequestType.AlignCalib) {
        }
    }

    public class RecipeGetPacket : VisionRequestPacket {
        public int MaxCount { get; set; }
        public int Option { get; set; }

        public RecipeGetPacket() : base(VisionRequestType.RecipeGet) {
        }
    }

}
