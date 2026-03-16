using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReringProject.Device {
    /// <summary>
    /// 시스템에서 사용되는 조명 장치를 설정합니다.
    /// Sequece name 과 동일하게 맞춰줘야 한다.
    /// </summary>
    public sealed partial class LightHandler {
        public const string LIGHT_TOP = "TOP";
        public const string LIGHT_SIDE = "SIDE";
        public const string LIGHT_BOTTOM = "BOTTOM";
        

        /// <summary>
        /// 사용되는 조명 컨트롤러 및 조명 그룹 (제어 단위) 을 설정합니다.
        /// </summary>
        public void RegisterLightController() {
            Controllers.Add(new JPFLightController(0).SetChannelNames(LIGHT_TOP, LIGHT_SIDE, LIGHT_BOTTOM));

            Groups.Add(new LightGroup(LIGHT_TOP).AddChannel(LIGHT_TOP));
            Groups.Add(new LightGroup(LIGHT_SIDE).AddChannel(LIGHT_SIDE));
            Groups.Add(new LightGroup(LIGHT_BOTTOM).AddChannel(LIGHT_BOTTOM));
        }
    }
}
