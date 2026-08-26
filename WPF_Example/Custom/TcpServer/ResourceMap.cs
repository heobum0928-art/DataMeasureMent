using ReringProject.Device;
using ReringProject.Sequence;
using ReringProject.Setting;
using ReringProject.Utility;
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
            Add(EResource.Light, ESite.Top,    LightHandler.LIGHT_RING); //260625 hbk Phase 64 LIGHT-01
            Add(EResource.Light, ESite.Side,   LightHandler.LIGHT_BAR);  //260625 hbk Phase 64 LIGHT-01
            Add(EResource.Light, ESite.Bottom, LightHandler.LIGHT_BACK); //260625 hbk Phase 64 LIGHT-01

            //sequence
            Add(EResource.Sequence, ESite.Top, SequenceHandler.SEQ_TOP);
            Add(EResource.Sequence, ESite.Side, SequenceHandler.SEQ_SIDE_1);
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
            Add(EResource.Light,    ESite.Top,  LightHandler.LIGHT_RING); //260625 hbk Phase 64 LIGHT-01
            Add(EResource.Light,    ESite.Side, LightHandler.LIGHT_BACK); //260625 hbk Phase 64 LIGHT-01
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
            Add(EResource.Light,    ESite.Top,  LightHandler.LIGHT_BAR); //260625 hbk Phase 64 LIGHT-01
            Add(EResource.Light,    ESite.Side, LightHandler.LIGHT_BAR); //260625 hbk Phase 64 LIGHT-01
            Add(EResource.Sequence, ESite.Top,  SequenceHandler.SEQ_SIDE_1);
            Add(EResource.Sequence, ESite.Side, SequenceHandler.SEQ_SIDE_1);
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

        // 260811 hbk plc-spec-260811-alignment: 제어팀 확정 스펙 — 6지그, Type 0~5 1:1 매핑.
        //  0=3D지그1(Top, 예비) / 1=3D지그2(Bottom, 예비) / 2="Top+Side1" 2D지그(Top) /
        //  3="Bottom+Side2" 2D지그(Bottom) / 4="Side3" 2D지그(물리 Side 카메라 1대뿐 — Top 슬롯 폴백 무해) /
        //  5="Side4" 2D지그(Type 4 와 동일 사유로 Top 슬롯 폴백).
        private const int TYPE_CODE_TOP = 0;              // 3D지그1 (Top, 예비)
        private const int TYPE_CODE_BOTTOM = 1;            // 3D지그2 (Bottom, 예비)
        private const int TYPE_CODE_TOP_SIDE1 = 2;         // SIDE_1 지그 (PC2). 상수 이름은 구 스펙 잔재 — 이름/값 변경 금지(외부 참조 회귀 방지)
        private const int TYPE_CODE_BOTTOM_SIDE2 = 3;      // SIDE_2 지그 (PC2). PC1 에서는 여전히 Bottom 슬롯 폴백
        private const int TYPE_CODE_SIDE3 = 4;             // SIDE_3 지그 (PC2)
        private const int TYPE_CODE_SIDE4 = 5;             // SIDE_4 지그 (PC2)

        //260807 hbk quick-260807-omy v-next PROTO-Type: Type 필드가 텍스트 토큰에서 숫자 코드로 전환됨. 인식 실패 시 false 반환(호출부가 Site 폴백).
        //260811 hbk plc-spec-260811-alignment: Type=3("Bottom+Side2" 지그)이 기존엔 Type 2~5 단일 분기에 묶여
        //  Top 슬롯으로 잘못 라우팅되던 버그 수정 — Type=1,3 만 Bottom 슬롯(ESite.Side, PC1 에서 CAMERA_BOTTOM
        //  매핑), 나머지(0,2,4,5)는 Top 슬롯. 근거: MapPc1Resources 에서 Add(Camera, ESite.Side, CAMERA_BOTTOM)
        //  확정, 사용자 물리 지그 배치 대조 확인 완료(.planning/debug/plc-spec-260811-alignment.md).
        //  경고: Int32.TryParse 실패 시 out 값이 0(=TOP 코드)이 되므로, 비숫자 가드를 반드시 코드 비교보다 먼저 배치할 것.
        // T-63-08/T-63-09 mitigation: 등록된 ESite.Top/Side 슬롯만 산출 → KeyNotFoundException/오라우팅 회피.
        private bool TryResolveSlotByType(string szType, out ESite eSlot)
        {
            eSlot = ESite.Top;
            bool bHasType = !string.IsNullOrEmpty(szType);
            if (!bHasType)
            {
                return false;
            }
            int nCode = 0;                                                //260807 hbk quick-260807-omy
            bool bIsNumeric = Int32.TryParse(szType, out nCode);           //260807 hbk quick-260807-omy
            if (!bIsNumeric)                                               //260807 hbk quick-260807-omy 비숫자 가드 — 코드 비교보다 반드시 먼저
            {
                return false;
            }
            bool bIsBottomSlot = nCode == TYPE_CODE_BOTTOM || nCode == TYPE_CODE_BOTTOM_SIDE2;   //260811 hbk plc-spec-260811-alignment
            if (bIsBottomSlot)
            {
                eSlot = ESite.Side;
                return true;
            }
            bool bIsTopSlot = nCode == TYPE_CODE_TOP || nCode == TYPE_CODE_TOP_SIDE1              //260811 hbk plc-spec-260811-alignment
                || nCode == TYPE_CODE_SIDE3 || nCode == TYPE_CODE_SIDE4;
            if (bIsTopSlot)
            {
                eSlot = ESite.Top;
                return true;
            }
            return false;
        }

        // Type(검사 대상 코드) → 시퀀스 이름 직접 해석. ESite 는 3슬롯이라 SIDE_1~4 를 담을 수 없어
        // 시퀀스 라우팅만 이 경로로 분리한다(카메라/조명 슬롯 매핑은 기존 ESite 경로 그대로).
        // PcRole 별 의미:
        //   PC2(Side)  : 2~5 = SIDE_1~4. 0/1 은 이 메서드에서 false 를 돌려주지만, 호출부가
        //     TryResolveSlotByType 폴백을 태우고 PC2 자원맵은 ESite.Top/Side 를 둘 다 SEQ_SIDE_1 로
        //     매핑하므로 결과적으로 SIDE_1 으로 흘러간다(오설정 시 Error 로그는 남는다).
        //     의도된 동작이다 — 여기서 FAIL 로 막으면 $PREP 과 $TEST 의 라우팅이 갈려
        //     "조명 켠 시퀀스 ≠ 검사 도는 시퀀스" 가 되기 때문. $TEST 와 결과를 일치시키는 쪽을 택했다.
        //   PC1(TopBottom): Phase 73 이전 라우팅을 그대로 보존한다(회귀 0). 0,2,4,5 → TOP / 1,3 → BOTTOM.
        //     ⚠ 제어 스펙상 2~5 는 SIDE 이므로 PC1 이 이 값을 받을 일은 없다. 값이 실제로 오면
        //     기존 동작(폴백)을 유지하되 로그로 남긴다 — 조용한 라우팅 변경이 더 위험하다.
        private bool TryResolveSequenceNameByType(string szType, out string szSeqName)
        {
            szSeqName = null;
            bool bHasType = !string.IsNullOrEmpty(szType);
            if (!bHasType)
            {
                return false;
            }
            int nCode = 0;
            bool bIsNumeric = Int32.TryParse(szType, out nCode);
            if (!bIsNumeric)
            {
                return false;
            }
            int nPcRole = (int)SystemSetting.Handle.PcRole;
            bool bIsPc2Side = nPcRole == (int)EPcRole.PC2_Side;
            if (bIsPc2Side)
            {
                switch (nCode)
                {
                    case TYPE_CODE_TOP_SIDE1:    szSeqName = SequenceHandler.SEQ_SIDE_1; return true;
                    case TYPE_CODE_BOTTOM_SIDE2: szSeqName = SequenceHandler.SEQ_SIDE_2; return true;
                    case TYPE_CODE_SIDE3:        szSeqName = SequenceHandler.SEQ_SIDE_3; return true;
                    case TYPE_CODE_SIDE4:        szSeqName = SequenceHandler.SEQ_SIDE_4; return true;
                    default:
                        Logging.PrintLog((int)ELogType.Error,
                            "[ROUTE] PC2(Side)에서 지원하지 않는 Type={0} 수신 — 시퀀스 해석 실패", nCode);
                        return false;
                }
            }
            // PC1(TopBottom). $PREP 에는 $TEST 같은 ESite 슬롯 폴백이 없으므로 여기서 해석하지 않으면
            //  TOP/BOTTOM 조명 예열이 통째로 사라진다(FAIL ACK 만 나감) — phase 목표 "TOP/BOTTOM 포함
            //  전 대상 Type 적용" 과 정면 충돌하는 회귀다.
            //  Type 0/1 은 제어 스펙상 TOP/BOTTOM 으로 확정이라 그대로 해석한다.
            //  Type 2~5(SIDE)는 PC1 대상이 아니다 — 해석하지 않고 false 를 돌려 기존 ESite 슬롯 폴백
            //  ($TEST 의 TryResolveSlotByType 경로, Phase 73 이전 라우팅)을 그대로 쓰게 둔다(회귀 0).
            switch (nCode)
            {
                case TYPE_CODE_TOP:    szSeqName = SequenceHandler.SEQ_TOP;    return true;
                case TYPE_CODE_BOTTOM: szSeqName = SequenceHandler.SEQ_BOTTOM; return true;
                default:
                    Logging.PrintLog((int)ELogType.Error,
                        "[ROUTE] PC1(TopBottom)에서 SIDE 계열 Type={0} 수신 — 이 PC 대상이 아니다. 기존 슬롯 폴백 사용", nCode);
                    return false;
            }
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
                // ⚠ 알려진 제약(Phase 73): SIDE 가 SIDE_1~4 로 분리됐지만 이 조회는 ESite 슬롯 1개만 본다 —
                //  PC2 에서는 SEQ_SIDE_1 상태만 보고된다(SIDE_2~4 가 검사 중이어도 Idle 로 보일 수 있다).
                //  $SITE_STATUS 에 Type 필드가 없어 대상을 특정할 수 없는 것이 근본 원인이다. 제어와 재협의 필요.
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
                        string szSeqByType;
                        bool bSeqResolved = TryResolveSequenceNameByType(testPacket.Type, out szSeqByType);
                        ESite eSlot;                                                                       //260624 hbk Phase 63
                        bool bTypeResolved = TryResolveSlotByType(testPacket.Type, out eSlot);             //260624 hbk Phase 63
                        if (!bTypeResolved)                                                                //260624 hbk Phase 63
                        {
                            eSlot = ResolveSiteSlot(testPacket.Site);                                     //260624 hbk Phase 63 폴백
                        }
                        if (bSeqResolved)
                        {
                            testPacket.Identifier = szSeqByType;          // PC2: Type 이 시퀀스를 직접 정한다
                        }
                        else
                        {
                            testPacket.Identifier = Find(EResource.Sequence, eSlot);
                        }
                        // v1.0 은 항상 Inspection action 매핑 (TestType 명시 필드 없음 — D-01 SUMMARY 기록).
                        testPacket.Identifier2 = Find(EResource.Action, eSlot, ETestType.Inspection);
                    }
                    else
                    {
                        testPacket.Identifier  = Find(EResource.Sequence, (ESite)testPacket.Site);
                        testPacket.Identifier2 = Find(EResource.Action, (ESite)testPacket.Site, (ETestType)testPacket.TestType);
                    }
                    break;
                case VisionRequestType.Prep:
                    PrepPacket prepPacket = packet.AsPrep();
                    string szPrepSeqName;
                    bool bPrepSeqResolved = TryResolveSequenceNameByType(prepPacket.Type, out szPrepSeqName);
                    if (bPrepSeqResolved)
                    {
                        prepPacket.Identifier = szPrepSeqName;
                    }
                    else
                    {
                        // [B-2] Type 해석 실패 시의 폴백은 **알려진 Type(0~5)에 한정**한다.
                        //  ResolveSiteSlot(packet.Site) 로 무조건 폴백하면 미지 Type(예: 9)도 슬롯을 얻어
                        //  PC2 에서 SEQ_SIDE_1 으로 흘러들어가 조용히 OK 가 나가고, 남의 지그 z_index 까지 세팅된다.
                        //  TryResolveSlotByType 은 Type 0~5 에서만 true 이므로(그 밖 / 비숫자 / 빈값 → false)
                        //  이 게이트 하나로 "미지 Type → Identifier 미설정 → FAIL ACK" 가 성립한다.
                        //  동시에 $TEST 와 같은 슬롯 규칙을 쓰므로 PREP/TEST 라우팅이 어긋나지 않는다.
                        ESite ePrepSlot;
                        bool bPrepSlotResolved = TryResolveSlotByType(prepPacket.Type, out ePrepSlot);
                        if (bPrepSlotResolved)
                        {
                            prepPacket.Identifier = Find(EResource.Sequence, ePrepSlot);
                        }
                        // 미지 Type 은 Identifier 를 세팅하지 않는다 → ProcessPrep 이 FAIL ACK 회신.
                    }
                    break;
                case VisionRequestType.Unknown:
                    break;
            }

            return true;
        }
    }


}
