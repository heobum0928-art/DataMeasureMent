//260624 hbk Phase 59
namespace ReringProject {

    /// <summary>
    /// Shape matching align 결과 모델. AlignShapeMatchService.Run() 반환값.
    /// Phase 62 TCP 전송 소비(OffsetXmm/OffsetYmm/ThetaDeg). 순수 POCO — 로직/HALCON/IO 없음.
    /// </summary>
    public class AlignResult {

        /// <summary>매칭 성공 여부. false 면 나머지 필드 미사용.</summary>
        public bool Found { get; set; }

        /// <summary>매칭 점수 (0~1).</summary>
        public double Score { get; set; }

        /// <summary>X Offset(mm) = dCol × (EthernetPixelResolution/1000). Col↔X 규약 — UAT 에서 부호 확정.</summary>
        public double OffsetXmm { get; set; }

        /// <summary>Y Offset(mm) = dRow × (EthernetPixelResolution/1000). Row↔Y 규약.</summary>
        public double OffsetYmm { get; set; }

        /// <summary>Theta(deg) = curAngleDeg − refAngleDeg. Tray 모드에서는 0 / HasTheta=false.</summary>
        public double ThetaDeg { get; set; }

        /// <summary>true = Bottom 모드 (ThetaDeg 유효). false = Tray 모드 (ThetaDeg=0 무시).</summary>
        public bool HasTheta { get; set; }
    }
}
