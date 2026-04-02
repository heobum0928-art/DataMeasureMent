using ReringProject.Device;
using ReringProject.Sequence;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReringProject.Network {
    public enum ESite : int {
        Top = 1,
        Side = 2,
        Bottom = 3,
    }

    public enum ETestType : int {
        Default = 0,

        Calibration = 1,
        Inspection = 2,

        InspectionLeft = 3,
        InspectionRight = 4,

        Unknown = 999
    }


    /// <summary>
    /// 통신 프로토콜의 zone, site, type 등의 정보를 시스템의 sequence, action, light 이름 등으로 치환하기 위한 map을 구성합니다.
    /// </summary>
    public partial class ResourceMap {
        public void Initialize() {
            //camera
            Add(EResource.Camera, ESite.Top, DeviceHandler.CAMERA_TOP);
            Add(EResource.Camera, ESite.Side, DeviceHandler.CAMERA_SIDE);
            Add(EResource.Camera, ESite.Bottom, DeviceHandler.CAMERA_BOTTOM);

            //light
            Add(EResource.Light, ESite.Top, LightHandler.LIGHT_TOP);
            Add(EResource.Light, ESite.Side, LightHandler.LIGHT_SIDE);
            Add(EResource.Light, ESite.Bottom, LightHandler.LIGHT_BOTTOM);

            //sequence
            Add(EResource.Sequence, ESite.Top, SequenceHandler.SEQ_TOP);
            Add(EResource.Sequence, ESite.Side, SequenceHandler.SEQ_SIDE);
            Add(EResource.Sequence, ESite.Bottom, SequenceHandler.SEQ_BOTTOM);


            Add(EResource.Action, ESite.Top, ETestType.Inspection, SequenceHandler.ACT_INSPECT);

            Add(EResource.Action, ESite.Side, ETestType.Inspection, SequenceHandler.ACT_INSPECT);

            Add(EResource.Action, ESite.Bottom, ETestType.Inspection, SequenceHandler.ACT_INSPECT);
        }

        public bool SetIdentifier(ref VisionRequestPacket packet) {
            switch (packet.RequestType) {
                case VisionRequestType.Light:
                    LightPacket lightPacket = packet.AsLight();
                    lightPacket.Identifier = Find(EResource.Light, (ESite)lightPacket.Site);
                    //lightPacket.Identifier = Find(EResource.Light, (ESite)lightPacket.Site, (ETestType)lightPacket.TestType);
                    if (lightPacket.On) {
                        lightPacket.Identifier2 = Find(EResource.Action, (ESite)lightPacket.Site, (ETestType)lightPacket.TestType);
                    }
                    // 01.12 else 구문 추가.
                    else
                        lightPacket.Identifier2 = Find(EResource.Action, (ESite)lightPacket.Site, (ETestType)lightPacket.TestType);
                    break;
                case VisionRequestType.RecipeChange:
                case VisionRequestType.RecipeGet:
                    //no identifier
                    break;
                case VisionRequestType.SiteStatus:
                    packet.Identifier = Find(EResource.Sequence, (ESite)packet.Site);
                    break;
                case VisionRequestType.Test:
                    TestPacket testPacket = packet.AsTest();
                    if ((ETestType)testPacket.TestType == ETestType.Calibration) {
                        testPacket.Identifier = null;
                        testPacket.Identifier2 = null;
                        return false;
                    }
                    testPacket.Identifier = Find(EResource.Sequence, (ESite)testPacket.Site);
                    testPacket.Identifier2 = Find(EResource.Action, (ESite)testPacket.Site, (ETestType)testPacket.TestType);
                    break;
                case VisionRequestType.Unknown:
                    break;
            }

            return true;
        }
    }

    
}
