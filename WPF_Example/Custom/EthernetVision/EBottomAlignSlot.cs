namespace ReringProject.Setting
{
    //260626 hbk Bottom 6슬롯 면별 Align 슬롯 enum.
    // 6슬롯 2그룹 (3D 그룹 2개 + 2D 그룹 4개). None=-1 은 폴백(기존 Bottom 단일 경로).
    // 정수값을 AlignFace 0~7 와 1:1 정렬.
    //261124 hbk 뒤집은 면 2슬롯(Top_Flip=6, Bottom_Flip=7) 추가 → 총 8슬롯.
    public enum EBottomAlignSlot
    {
        None         = -1,  //260626 hbk 슬롯 미지정 → 기존 Bottom_1/2.shm 단일 경로 폴백
        Slot3DTop    =  0,  //260626 hbk 3D_Top (AlignFace 0 = G1_TOP)
        Slot3DBottom =  1,  //260626 hbk 3D_Bottom (AlignFace 1 = G1_BOT)
        Slot2DTop    =  2,  //260626 hbk 2D_TOP (AlignFace 2 = G2_TOP)
        Slot2DBottom =  3,  //260626 hbk 2D_BOTTOM (AlignFace 3 = G2_BOT)
        Slot2DSide1  =  4,  //260626 hbk 2D_SIDE_1 (AlignFace 4 = G2_SIDE1)
        Slot2DSide2  =  5,  //260626 hbk 2D_SIDE_2 (AlignFace 5 = G2_SIDE2)
        SlotTopFlip    = 6, //261124 hbk Top_Flip (AlignFace 6)
        SlotBottomFlip = 7  //261124 hbk Bottom_Flip (AlignFace 7)
    }

    //260626 hbk Bottom 슬롯 매퍼 헬퍼.
    // ToFileToken: 파일명 토큰 반환 (Bottom_{token}_1/2.shm). None → "" (폴백 신호).
    // FromAlignFace: TCP AlignFace 0~7 → 슬롯 (범위 외 → None, OOB 방지).
    // ToDisplayLabel: UI 라벨 (Plan 02 소비). None → "(단일)".
    public static class EBottomAlignSlotMap
    {
        /// <summary>
        /// 슬롯에 대응하는 파일명 토큰을 반환한다.
        /// None → "" (폴백 신호 = 기존 Bottom 단일 경로).
        /// Bottom_{token}_1/2.shm 파일명 조합에 사용.
        /// </summary>
        public static string ToFileToken(EBottomAlignSlot slot) //260626 hbk 슬롯→파일 토큰 매핑
        {
            if (slot == EBottomAlignSlot.Slot3DTop)
            {
                return "3D_Top";    //260626 hbk G1_TOP
            }
            if (slot == EBottomAlignSlot.Slot3DBottom)
            {
                return "3D_Bottom"; //260626 hbk G1_BOT
            }
            if (slot == EBottomAlignSlot.Slot2DTop)
            {
                return "2D_TOP";    //260626 hbk G2_TOP
            }
            if (slot == EBottomAlignSlot.Slot2DBottom)
            {
                return "2D_BOTTOM"; //260626 hbk G2_BOT
            }
            if (slot == EBottomAlignSlot.Slot2DSide1)
            {
                return "2D_SIDE_1"; //260626 hbk G2_SIDE1
            }
            if (slot == EBottomAlignSlot.Slot2DSide2)
            {
                return "2D_SIDE_2"; //260626 hbk G2_SIDE2
            }
            if (slot == EBottomAlignSlot.SlotTopFlip)
            {
                return "Top_Flip";
            }
            if (slot == EBottomAlignSlot.SlotBottomFlip)
            {
                return "Bottom_Flip";
            }
            return ""; //260626 hbk None(폴백) 및 미지정 → 빈 문자열 = 기존 Bottom 단일 경로 신호
        }

        /// <summary>
        /// TCP AlignFace 정수(0~7) → EBottomAlignSlot 으로 변환한다.
        /// 0~7 범위 외(음수/8 이상)는 None 반환 → OOB 불가.
        /// 0=Slot3DTop/1=Slot3DBottom/2=Slot2DTop/3=Slot2DBottom/4=Slot2DSide1/5=Slot2DSide2/6=SlotTopFlip/7=SlotBottomFlip.
        /// </summary>
        public static EBottomAlignSlot FromAlignFace(int alignFace) //260626 hbk TCP AlignFace 0~7 → 슬롯
        {
            if (alignFace == 0)
            {
                return EBottomAlignSlot.Slot3DTop;    //260626 hbk G1_TOP
            }
            if (alignFace == 1)
            {
                return EBottomAlignSlot.Slot3DBottom; //260626 hbk G1_BOT
            }
            if (alignFace == 2)
            {
                return EBottomAlignSlot.Slot2DTop;    //260626 hbk G2_TOP
            }
            if (alignFace == 3)
            {
                return EBottomAlignSlot.Slot2DBottom; //260626 hbk G2_BOT
            }
            if (alignFace == 4)
            {
                return EBottomAlignSlot.Slot2DSide1;  //260626 hbk G2_SIDE1
            }
            if (alignFace == 5)
            {
                return EBottomAlignSlot.Slot2DSide2;  //260626 hbk G2_SIDE2
            }
            if (alignFace == 6)
            {
                return EBottomAlignSlot.SlotTopFlip;
            }
            if (alignFace == 7)
            {
                return EBottomAlignSlot.SlotBottomFlip;
            }
            return EBottomAlignSlot.None; //260626 hbk 범위 외(음수/8 이상) → None 안전 거부
        }

        /// <summary>
        /// UI 표시용 라벨을 반환한다 (Plan 02 소비).
        /// None → "(단일)".
        /// </summary>
        public static string ToDisplayLabel(EBottomAlignSlot slot) //260626 hbk UI 라벨
        {
            if (slot == EBottomAlignSlot.Slot3DTop)
            {
                return "3D_Top";    //260626 hbk G1_TOP 라벨
            }
            if (slot == EBottomAlignSlot.Slot3DBottom)
            {
                return "3D_Bottom"; //260626 hbk G1_BOT 라벨
            }
            if (slot == EBottomAlignSlot.Slot2DTop)
            {
                return "2D_TOP";    //260626 hbk G2_TOP 라벨
            }
            if (slot == EBottomAlignSlot.Slot2DBottom)
            {
                return "2D_BOTTOM"; //260626 hbk G2_BOT 라벨
            }
            if (slot == EBottomAlignSlot.Slot2DSide1)
            {
                return "2D_SIDE_1"; //260626 hbk G2_SIDE1 라벨
            }
            if (slot == EBottomAlignSlot.Slot2DSide2)
            {
                return "2D_SIDE_2"; //260626 hbk G2_SIDE2 라벨
            }
            if (slot == EBottomAlignSlot.SlotTopFlip)
            {
                return "Top_Flip";
            }
            if (slot == EBottomAlignSlot.SlotBottomFlip)
            {
                return "Bottom_Flip";
            }
            return "(단일)"; //260626 hbk None → 기존 단일 Bottom 경로 표시
        }
    }
}
