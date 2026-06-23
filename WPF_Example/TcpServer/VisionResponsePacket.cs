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

        public const string RESULT_OK = "OK";
        public const string RESULT_NG = "NG";

        public const string TEST_RESULT_PASS = "P";
        public const string TEST_RESULT_FAIL = "F";
        public const string TEST_RESULT_NOTEXIST = "N";
        public const string TEST_RESULT_ANGLE_FAIL = "A";           //12.21
        public const string TEST_RESULT_TEACHING = "T";             //05.20 Insert

        public const string SITE_STATUS_READY = "READY";
        public const string SITE_STATUS_BUSY = "BUSY";
        public const string SITE_STATUS_ERROR = "ERROR";

        // 260622 hbk Phase 48 PROTO-02: v1.0 RESULT 3단 구분자 ($RESULT:site;P|F|B;count;id=val=OK|NG,...@).
        public const char MSG_RESULT_HEADER_SEP = ';';   // 헤더 구분자 (site/판정/count 사이)
        public const char MSG_RESULT_ITEM_SEP   = ',';   // 항목 간 구분자
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

        public static VisionResponsePacket Convert(string msg) {
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
            VisionResponsePacket packet = null;
            string[] dataList;
            int siteNum = 0;
            //int zoneNum = 0;
            switch (msgList[0]) {
                case CMD_SEND_GRAB_STATUS:
                    packet = new GrabStatusResultPacket();
                    GrabStatusResultPacket grabPacket = packet.AsGrabStatusResult();

                    dataList = msgList[1].Split(VisionServer.MSG_CONTENTS_SEPERATOR);
                    if (dataList.Length < 3) return null;
                    /*
                    //zone
                    if(Int32.TryParse(dataList[0], out zoneNum) == false) {
                        return null;
                    }
                    grabPacket.Zone = zoneNum;
                    */
                    //site
                    if(Int32.TryParse(dataList[0], out siteNum) == false) {
                        return null;
                    }
                    grabPacket.Site = siteNum;
                    grabPacket.SetResultFromString(dataList[1]);

                    break;
                case CMD_SEND_LIGHT:
                    packet = new LightResultPacket();
                    LightResultPacket lightPacket = packet.AsLightResult();

                    dataList = msgList[1].Split(VisionServer.MSG_CONTENTS_SEPERATOR);
                    if (dataList.Length < 3) return null;
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
                    lightPacket.SetOnFromString(dataList[1]);

                    break;
                case CMD_SEND_RECIPE_CHANGE:
                    packet = new RecipeChangeResultPacket();
                    RecipeChangeResultPacket recipePacket = packet.AsRecipeChangeResult();

                    dataList = msgList[1].Split(VisionServer.MSG_CONTENTS_SEPERATOR);
                    if (dataList.Length < 2) return null;
                    /*
                    //zone
                    if (Int32.TryParse(dataList[0], out zoneNum) == false) {
                        return null;
                    }
                    recipePacket.Zone = zoneNum;
                    */
                    //result
                    recipePacket.SetResultFromString(dataList[0]);

                    break;
                case CMD_SEND_RECIPE_GET:
                    packet = new RecipeListResultPacket();
                    RecipeListResultPacket recipeListPacket = packet.AsRecipeListResult();

                    dataList = msgList[1].Split(VisionServer.MSG_CONTENTS_SEPERATOR);
                    if (dataList.Length < 3) return null;
                    /*
                    //zone
                    if (Int32.TryParse(dataList[0], out zoneNum) == false) {
                        return null;
                    }
                    recipeListPacket.Zone = zoneNum;
                    */
                    //site
                    if (Int32.TryParse(dataList[0], out siteNum) == false) {
                        return null;
                    }
                    recipeListPacket.Site = siteNum;

                    //count
                    if(Int32.TryParse(dataList[1], out int count) == false) {
                        return null;
                    }
                    recipeListPacket.MaxCount = count;

                    //names
                    for(int i = 0; i < count; i++) {
                        recipeListPacket.RecipeList.Add(dataList[i+3]);
                    }
                    break;
                case CMD_SEND_SITE_STATUS:
                    packet = new SiteStatusResultPacket();
                    SiteStatusResultPacket sitePacket = packet.AsSiteStatusResult();

                    dataList = msgList[1].Split(VisionServer.MSG_CONTENTS_SEPERATOR);
                    if (dataList.Length < 3) return null;
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

                    //status
                    sitePacket.SetSiteStatusFromString(dataList[1]);
                    break;
                case CMD_SEND_TEST:
                    packet = new TestResultPacket();
                    TestResultPacket testPacket = packet.AsTestResult();

                    dataList = msgList[1].Split(VisionServer.MSG_CONTENTS_SEPERATOR);
                    if (dataList.Length < 7) return null;
                    /*
                    //zone
                    if (Int32.TryParse(dataList[0], out zoneNum) == false) {
                        return null;
                    }
                    testPacket.Zone = zoneNum;
                    */
                    //site
                    if (Int32.TryParse(dataList[0], out siteNum) == false) {
                        return null;
                    }
                    testPacket.Site = siteNum;

                    //type
                    if(Int32.TryParse(dataList[1], out int testKind) == false) {
                        return null;
                    }
                    testPacket.InspectionType = testKind;

                    //result
                    testPacket.SetResultFromString(dataList[2]);

                    //angle
                    if(Double.TryParse(dataList[3], out double angle) == false) {
                        return null;
                    }
                    testPacket.Angle = angle;

                    //x
                    if(Double.TryParse(dataList[4], out double x) == false) {
                        return null;
                    }
                    testPacket.X = x;

                    //y
                    if(Double.TryParse(dataList[5], out double y) == false) {
                        return null;
                    }
                    testPacket.Y = y;

                    break;
            }
            return packet;
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
                case EVisionResponseType.Unknown:
                    return null;
            }
            return msg;
        }

        // 260622 hbk Phase 48 PROTO-02: v1.0 RESULT 직렬화. $RESULT:site;P|F|B;count;id=val=judge,...@ (STX/ETX 는 TcpServer 부착).
        // FAICount=0(Datum 샷)이면 BuildFaiItemsV1 가 빈 문자열 반환 → 'RESULT:{site};B;0;' (마지막 ';' 뒤 항목 없음).
        private static string BuildResultMessageV1(TestResultPacket testPacket)
        {
            string szMsg = "";
            szMsg += CMD_SEND_TEST;                       // "RESULT"
            szMsg += VisionServer.MSG_CMD_SEPERATOR;      // ':'
            szMsg += testPacket.Site.ToString();
            szMsg += MSG_RESULT_HEADER_SEP;               // ';'
            szMsg += MapCycleJudgement(testPacket);       // P|F|B
            szMsg += MSG_RESULT_HEADER_SEP;               // ';'
            szMsg += testPacket.FAICount.ToString();      // count
            szMsg += MSG_RESULT_HEADER_SEP;               // ';'
            szMsg += BuildFaiItemsV1(testPacket);         // id=val=judge,...  (count=0 이면 빈 문자열)
            return szMsg;
        }

        // 260622 hbk Phase 48 PROTO-02: cycle 종합 판정 → P/F/B 매핑. IsBuffer 최우선(진행 중), OK=P, 그 외=F.
        // 판정 '결정' 로직은 Phase 49. 여기선 이미 확정된 Result/IsBuffer 를 문자로 변환만.
        private static string MapCycleJudgement(TestResultPacket testPacket)
        {
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

        // 260622 hbk Phase 48 PROTO-02: FAI 항목 판정 → OK|NG. (cycle 판정과 별개 — 항목 단위.)
        private static string MapFaiJudgement(FAIResultData faiData)
        {
            bool bIsOk = faiData.Result == EVisionResultType.OK;
            if (bIsOk)
            {
                return RESULT_OK;   // "OK"
            }
            return RESULT_NG;       // "NG"
        }

        // 260622 hbk Phase 48 PROTO-02: FAI 항목들을 id=val=judge,... 로 직렬화 (항목 간 ',').
        private static string BuildFaiItemsV1(TestResultPacket testPacket)
        {
            string szItems = "";
            int nCount = testPacket.FAICount;
            for (int i = 0; i < nCount; i++)
            {
                FAIResultData faiData = testPacket.FAIResults[i];
                bool bNeedsSeparator = i > 0;
                if (bNeedsSeparator)
                {
                    szItems += MSG_RESULT_ITEM_SEP;               // ','
                }
                szItems += faiData.FAIName;                       // id
                szItems += MSG_RESULT_INNER_SEP;                  // '='
                szItems += faiData.DistanceMm.ToString("0.000");  // val
                szItems += MSG_RESULT_INNER_SEP;                  // '='
                szItems += MapFaiJudgement(faiData);              // OK|NG
            }
            return szItems;
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

        public void SetSiteStatusFromString(string siteStr) {
            if (siteStr == SITE_STATUS_READY) Result = EVisionSiteStatusType.Ready;
            else if (siteStr == SITE_STATUS_BUSY) Result = EVisionSiteStatusType.Busy;
            else if (siteStr == SITE_STATUS_ERROR) Result = EVisionSiteStatusType.Error;
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

    public class LightResultPacket : VisionResponsePacket {

        public int TestType { get; set; }
        public bool On { get; set; }

        public LightResultPacket() : base(EVisionResponseType.Light) {
        }

        public string GetOnString() {
            if (On) return "1";
            return "0";
        }

        public void SetOnFromString(string onStr) {
            if (onStr == "1") On = true;
            else On = false;
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
