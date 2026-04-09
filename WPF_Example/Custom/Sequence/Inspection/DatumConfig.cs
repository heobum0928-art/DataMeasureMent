//260409 hbk Phase 4: Datum 데이터 모델 — D-01, D-04, D-05, D-11
using System.ComponentModel;
using HalconDotNet;
using PropertyTools.DataAnnotations;
using ReringProject.Utility;

namespace ReringProject.Sequence {

    /// <summary>
    /// Datum 찾기를 위한 2-라인 ROI 설정 및 기준 원점/각도 저장 모델.
    /// ParamBase 상속으로 INI 자동 직렬화(double/int/string/bool).
    /// HTuple 필드는 ParamBase switch-case에 없으므로 런타임 전용으로 사용된다 (D-11).
    /// </summary>
    public class DatumConfig : ParamBase {

        //260409 hbk Phase 4: Line1 ROI (기준 X축 방향 에지 라인)
        [Category("Datum|Line1 ROI")]
        public double Line1_Row { get; set; } = 0;
        public double Line1_Col { get; set; } = 0;
        public double Line1_Phi { get; set; } = 0;
        public double Line1_Length1 { get; set; } = 100;
        public double Line1_Length2 { get; set; } = 10;

        //260409 hbk Phase 4: Line2 ROI (기준 Y축 방향 에지 라인, 기본값 PI/2 = 수직)
        [Category("Datum|Line2 ROI")]
        public double Line2_Row { get; set; } = 0;
        public double Line2_Col { get; set; } = 0;
        public double Line2_Phi { get; set; } = 1.5708; // Math.PI / 2 — 기본 수직 방향
        public double Line2_Length1 { get; set; } = 100;
        public double Line2_Length2 { get; set; } = 10;

        //260409 hbk Phase 4: 기준 원점 및 각도 (티칭 시 저장)
        [Category("Datum|Reference")]
        public double RefOriginRow { get; set; } = 0;
        public double RefOriginCol { get; set; } = 0;
        public double RefAngleRad { get; set; } = 0;

        //260409 hbk Phase 4: 에지 검출 파라미터 (FAIConfig와 동일 패턴)
        [Category("Datum|Edge Detection")]
        public int EdgeThreshold { get; set; } = 20;
        public double Sigma { get; set; } = 1.0;
        public string EdgePolarity { get; set; } = "all"; // Halcon MeasurePos polarity: "all", "positive", "negative"

        //260409 hbk Phase 4: 설정 완료 플래그 — 티칭 후 true, 기본값 false
        [Category("Datum|Status")]
        public bool IsConfigured { get; set; } = false;

        //260409 hbk Phase 4: 런타임 전용 (HTuple은 ParamBase 직렬화 대상 아님)
        [PropertyTools.DataAnnotations.Browsable(false)]
        public HTuple CurrentTransform { get; set; }

        [PropertyTools.DataAnnotations.Browsable(false)]
        public bool LastFindSucceeded { get; set; }

        public DatumConfig(object owner) : base(owner) {
        }
    }
}
