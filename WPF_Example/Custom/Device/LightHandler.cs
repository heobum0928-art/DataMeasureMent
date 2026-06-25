using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReringProject.Device {
    /// <summary>
    /// 시스템에서 사용되는 조명 장치를 설정합니다.
    /// Phase 64 LIGHT-01: JPF-1208 2대, 13채널 구성.
    /// </summary>
    public sealed partial class LightHandler {
        //260625 hbk Phase 64 LIGHT-01: Controller A — Ring 6분할 물리 채널명
        public const string LIGHT_RING_CH1  = "RING_CH1";
        public const string LIGHT_RING_CH2  = "RING_CH2";
        public const string LIGHT_RING_CH3  = "RING_CH3";
        public const string LIGHT_RING_CH4  = "RING_CH4";
        public const string LIGHT_RING_CH5  = "RING_CH5";
        public const string LIGHT_RING_CH6  = "RING_CH6";
        public const string LIGHT_ALIGN_COAX = "ALIGN_COAX"; //Align 동축 조명

        //260625 hbk Phase 64 LIGHT-01: Controller B — Back, Bar×4, Ring7
        public const string LIGHT_BACK  = "BACK";
        public const string LIGHT_BAR_1 = "BAR_1";
        public const string LIGHT_BAR_2 = "BAR_2";
        public const string LIGHT_BAR_3 = "BAR_3";
        public const string LIGHT_BAR_4 = "BAR_4";
        public const string LIGHT_RING7 = "RING7";

        //260625 hbk Phase 64 LIGHT-01: 그룹 이름 상수 (ApplyShotLights 소비)
        public const string LIGHT_RING = "RING";   // RING_CH1~CH6 통합 그룹
        public const string LIGHT_BAR  = "BAR";    // BAR_1~BAR_4 통합 그룹

        /// <summary>
        /// 사용되는 조명 컨트롤러 및 조명 그룹 (제어 단위) 을 설정합니다.
        /// D-06: Controller A (Index=0) Ring CH1~CH6 + AlignCoax = 7채널
        /// D-07: Controller B (Index=1) Back + Bar×4 + Ring7 = 6채널
        /// D-08: Ring 6채널은 RING 통합 그룹으로 동시 제어
        /// D-09: LightGroup 5종 — RING/BACK/BAR/RING7/ALIGN_COAX
        /// </summary>
        public void RegisterLightController() {
            //260625 hbk Phase 64 LIGHT-01: Controller A — Ring CH1~CH6 + Align 동축
            Controllers.Add(new JPFLightController(0, 7)
                .SetChannelNames(
                    LIGHT_RING_CH1, LIGHT_RING_CH2, LIGHT_RING_CH3,
                    LIGHT_RING_CH4, LIGHT_RING_CH5, LIGHT_RING_CH6,
                    LIGHT_ALIGN_COAX));

            //260625 hbk Phase 64 LIGHT-01: Controller B — Back + Bar×4 + Ring7
            Controllers.Add(new JPFLightController(1, 6)
                .SetChannelNames(
                    LIGHT_BACK, LIGHT_BAR_1, LIGHT_BAR_2,
                    LIGHT_BAR_3, LIGHT_BAR_4, LIGHT_RING7));

            //260625 hbk Phase 64 LIGHT-01: LightGroup 5종 등록
            // RING: Ring CH1~CH6 6채널 통합 — ShotConfig.RingLight_* 소비
            Groups.Add(new LightGroup(LIGHT_RING).AddChannel(
                LIGHT_RING_CH1, LIGHT_RING_CH2, LIGHT_RING_CH3,
                LIGHT_RING_CH4, LIGHT_RING_CH5, LIGHT_RING_CH6));

            // BACK: 백라이트 단일 채널 — ShotConfig.BackLight_* 소비
            Groups.Add(new LightGroup(LIGHT_BACK).AddChannel(LIGHT_BACK));

            // BAR: Bar 4채널 통합 — ShotConfig.SideLight_* 소비
            Groups.Add(new LightGroup(LIGHT_BAR).AddChannel(
                LIGHT_BAR_1, LIGHT_BAR_2, LIGHT_BAR_3, LIGHT_BAR_4));

            // RING7: Ring7 단일 채널 (독립 제어용)
            Groups.Add(new LightGroup(LIGHT_RING7).AddChannel(LIGHT_RING7));

            // ALIGN_COAX: Align 동축 단일 채널 — ShotConfig.CoaxLight_* 소비
            Groups.Add(new LightGroup(LIGHT_ALIGN_COAX).AddChannel(LIGHT_ALIGN_COAX));
        }
    }
}
