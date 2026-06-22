using ReringProject.Device;
using ReringProject.Sequence;
using ReringProject.Setting;
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

    //260622 hbk Phase 48 PROTO-01: PC 역할 (D-03). SystemSetting.PcRole 값과 일치 (1=PC1, 2=PC2).
    public enum EPcRole : int {
        PC1_TopBottom = 1,
        PC2_Side = 2,
    }

    /// <summary>
    /// 통신 프로토콜의 zone, site, type 등의 정보를 시스템의 sequence, action, light 이름 등으로 치환하기 위한 map을 구성합니다.
    /// </summary>
    public partial class ResourceMap {
        //260622 hbk Phase 48 PROTO-01: v1.0/v2.6 분기 — UseProtocolV1 플래그로 결정 (D-06).
        public void Initialize()
        {
            bool bUseV1 = SystemSetting.Handle.UseProtocolV1;
            if (bUseV1)
            {
                InitializeV1();
            }
            else
            {
                InitializeV26();
            }
        }

        // v2.6 보존 — 현행 Initialize 본문 그대로 (Site1=Top/2=Side/3=Bottom). 회귀 0.
        private void InitializeV26()
        {
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

        //260622 hbk Phase 48 PROTO-01: v1.0 2-PC 매핑 (D-04). Site1=ESite.Top 슬롯, Site2=ESite.Side 슬롯.
        //  PcRole 로 슬롯이 가리키는 실제 자원(TOP/BOTTOM vs SIDE/SIDE)을 결정. framework ESite 키 재사용(시그니처 호환).
        private void InitializeV1()
        {
            int nPcRole = (int)SystemSetting.Handle.PcRole;
            bool bIsPc1 = nPcRole == (int)EPcRole.PC1_TopBottom;
            if (bIsPc1)
            {
                MapPc1Resources();
            }
            else
            {
                MapPc2Resources();
            }
        }

        // PC1: Site1(ESite.Top 슬롯)=TOP, Site2(ESite.Side 슬롯)=BOTTOM.
        private void MapPc1Resources()
        {
            Add(EResource.Camera,   ESite.Top,  DeviceHandler.CAMERA_TOP);
            Add(EResource.Camera,   ESite.Side, DeviceHandler.CAMERA_BOTTOM);
            Add(EResource.Light,    ESite.Top,  LightHandler.LIGHT_TOP);
            Add(EResource.Light,    ESite.Side, LightHandler.LIGHT_BOTTOM);
            Add(EResource.Sequence, ESite.Top,  SequenceHandler.SEQ_TOP);
            Add(EResource.Sequence, ESite.Side, SequenceHandler.SEQ_BOTTOM);
            Add(EResource.Action,   ESite.Top,  ETestType.Inspection, SequenceHandler.ACT_INSPECT);
            Add(EResource.Action,   ESite.Side, ETestType.Inspection, SequenceHandler.ACT_INSPECT);
        }

        // PC2: Site1·Site2 모두 SIDE 자원 (물리 SIDE 1종 공유).
        private void MapPc2Resources()
        {
            Add(EResource.Camera,   ESite.Top,  DeviceHandler.CAMERA_SIDE);
            Add(EResource.Camera,   ESite.Side, DeviceHandler.CAMERA_SIDE);
            Add(EResource.Light,    ESite.Top,  LightHandler.LIGHT_SIDE);
            Add(EResource.Light,    ESite.Side, LightHandler.LIGHT_SIDE);
            Add(EResource.Sequence, ESite.Top,  SequenceHandler.SEQ_SIDE);
            Add(EResource.Sequence, ESite.Side, SequenceHandler.SEQ_SIDE);
            Add(EResource.Action,   ESite.Top,  ETestType.Inspection, SequenceHandler.ACT_INSPECT);
            Add(EResource.Action,   ESite.Side, ETestType.Inspection, SequenceHandler.ACT_INSPECT);
        }

        //260622 hbk Phase 48 PROTO-01: v1.0 Site 정수 → ESite 슬롯 변환 (Site1=Top, Site2=Side). 범위 밖 → Top 폴백.
        // T-48-04 mitigation: 범위 밖 Site(0, 3, 음수) 도 안전한 기본 슬롯으로 처리 (KeyNotFoundException 회피).
        private ESite ResolveSiteSlot(int nSite)
        {
            bool bIsSite2 = nSite == 2;
            if (bIsSite2)
            {
                return ESite.Side;
            }
            return ESite.Top;
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
                    //260622 hbk Phase 48 PROTO-01: Calibration 체크 — bool 변수화 (D-00 조건 변수화).
                    bool bIsCalibration = (ETestType)testPacket.TestType == ETestType.Calibration;
                    if (bIsCalibration)
                    {
                        testPacket.Identifier = null;
                        testPacket.Identifier2 = null;
                        return false;
                    }

                    //260622 hbk Phase 48 PROTO-01: v1.0 분기 — Site 정수를 ESite 슬롯으로 변환 후 Find. v2.6 캐스팅 보존.
                    bool bUseV1 = SystemSetting.Handle.UseProtocolV1;
                    if (bUseV1)
                    {
                        ESite eSlot = ResolveSiteSlot(testPacket.Site);
                        testPacket.Identifier  = Find(EResource.Sequence, eSlot);
                        // v1.0 은 항상 Inspection action 매핑 (TestType 명시 필드 없음 — D-01 SUMMARY 기록).
                        testPacket.Identifier2 = Find(EResource.Action, eSlot, ETestType.Inspection);
                    }
                    else
                    {
                        testPacket.Identifier  = Find(EResource.Sequence, (ESite)testPacket.Site);
                        testPacket.Identifier2 = Find(EResource.Action, (ESite)testPacket.Site, (ETestType)testPacket.TestType);
                    }
                    break;
                case VisionRequestType.Unknown:
                    break;
            }

            return true;
        }
    }


}
