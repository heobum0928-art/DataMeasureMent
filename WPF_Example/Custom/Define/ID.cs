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
        Side = 2,
        Bottom = 3,
    }

    /// <summary>
    /// 각 시퀀스에 종속되는 action의 ID (쓰레드가 수행할 수 있는 동작 단위)
    /// </summary>
    public enum EAction : int {
        Top_Calibration = 1,
        Top_Inspection = 2,
        Side_Calibration = 3,
        Side_Inspection = 4,
        Bottom_Calibration = 5,
        Bottom_Inspection = 6,

        Unknown = Int32.MaxValue
    }

    
}
