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

        Unknown = 999
    }

    public class VisionRequestPacket : IDisposable {

        //Recv
        public const string CMD_RECV_RECIPE_CHANGE = "RECIPE";
        public const string CMD_RECV_RECIPE_GET = "GET_RECIPE";
        public const string CMD_RECV_SITE_STATUS = "SITE_STATUS";
        public const string CMD_RECV_LIGHT = "LIGHT";
        public const string CMD_RECV_TEST = "TEST";

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
            string testID = "";
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
                    packet = new TestPacket();
                    TestPacket testPacket = packet.AsTest();

                    dataList = msgList[1].Split(VisionServer.MSG_CONTENTS_SEPERATOR);
                    if (dataList.Length < 3) return null;
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

                    //test kind
                    if (Int32.TryParse(dataList[1], out testKind) == false) {
                        return null;
                    }
                    testPacket.TestType = testKind;

                    //test ID
                    testID = dataList[2];
                    testPacket.TestID = testID;

                    break;
            }

            return packet;
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

        public TestPacket() : base(VisionRequestType.Test) {
        }
    }

    public class RecipeGetPacket : VisionRequestPacket {
        public int MaxCount { get; set; }
        public int Option { get; set; }

        public RecipeGetPacket() : base(VisionRequestType.RecipeGet) {
        }
    }

}
