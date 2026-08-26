using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReringProject.Define {

    /// <summary>
    /// 시퀀스의 ID(쓰레드 단위 = 카메라)
    /// </summary>
    public enum ESequence : int {
        Top = 1,
        // Side = 2 는 레거시 식별자로만 남긴다 — 더 이상 시퀀스로 등록하지 않는다(SIDE_1~4 로 분리됨).
        // 값 자체를 지우거나 재사용하지 않는 이유: VisionResponsePacket 이 (int)ESequence.Bottom 을
        // 와이어 site 정수와 직접 비교하고 있어 번호 재배치가 프로토콜 회귀를 만든다.
        Side = 2,
        Bottom = 3,
        Side1 = 4,
        Side2 = 5,
        Side3 = 6,
        Side4 = 7,
    }

    /// <summary>
    /// 각 시퀀스에 종속되는 action의 ID (쓰레드가 수행할 수 있는 동작 단위)
    /// </summary>
    public enum EAction : int {
        Top_Inspection = 2,
        Side_Inspection = 4,          // 레거시(ESequence.Side 와 짝) — 등록되지 않음
        Bottom_Inspection = 6,

        Side1_Inspection = 7,
        Side2_Inspection = 8,
        Side3_Inspection = 9,
        Side4_Inspection = 10,

        FAI_Base = 100,

        Unknown = Int32.MaxValue
    }

    
}
