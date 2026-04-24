//260409 hbk Phase 4: Datum 데이터 모델 — D-01, D-04, D-05, D-11
using System.Collections.Generic; //260423 hbk WR-RT-02 ComboBox 옵션 리스트
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

        //260413 hbk Phase 6: DatumName — 사용자가 지정하는 식별자 (D-06)
        [Category("Datum|Identity")]
        public string DatumName { get; set; } = "Datum_1";

        //260413 hbk Phase 6: 이미지 소스 모드 — "Dedicated" 또는 "ReuseFromShot" (D-07, D-08)
        [Category("Datum|ImageSource")]
        public string ImageSourceMode { get; set; } = "Dedicated";

        //260413 hbk Phase 6: ReuseFromShot 모드일 때 재사용할 Shot 이름 (D-07)
        [Category("Datum|ImageSource")]
        public string ReuseFromShotName { get; set; } = "";

        //260423 hbk Phase 11 D-08 — 카메라/Z/조명을 상속할 Shot 이름 (빈 문자열이면 Sequence 첫 Shot fallback)
        [Category("Datum|ImageSource")]
        public string SourceShotName { get; set; } = "";

        //260423 hbk Phase 12 D-09 — Datum 알고리즘 선택자 (PropertyGrid enum 자동 드롭다운)
        //260423 hbk  저장 타입: string (ParamBase.Save/Load switch가 enum 미지원 — ParamBase.cs:330-363)
        //260423 hbk  유효값: "TwoLineIntersect" | "CircleTwoHorizontal" | "VerticalTwoHorizontal"
        //260423 hbk  미존재/미지원 문자열 로드 시 AlgorithmTypeEnum 게터가 TwoLineIntersect로 폴백 (Phase 4/11 INI 하위호환)
        [Category("Datum|Algorithm")]
        [ItemsSourceProperty(nameof(AlgorithmTypeList))] //260423 hbk Phase 12 D-09 — PropertyGrid 드롭다운 목록
        public string AlgorithmType { get; set; } = "TwoLineIntersect"; //260423 hbk Phase 12 D-09

        //260423 hbk Phase 12 D-09 — AlgorithmType 드롭다운 옵션 (enum의 string 투영)
        [PropertyTools.DataAnnotations.Browsable(false)]
        public List<string> AlgorithmTypeList
        {
            get
            {
                return new List<string>
                {
                    EDatumAlgorithm.TwoLineIntersect.ToString(),
                    EDatumAlgorithm.CircleTwoHorizontal.ToString(),
                    EDatumAlgorithm.VerticalTwoHorizontal.ToString(),
                };
            }
        }

        //260423 hbk Phase 12 D-09 — DatumFindingService가 사용하는 enum 게터 (문자열 → enum 파싱, 실패 시 TwoLineIntersect)
        [PropertyTools.DataAnnotations.Browsable(false)]
        public EDatumAlgorithm AlgorithmTypeEnum
        {
            get
            {
                EDatumAlgorithm parsed;
                if (System.Enum.TryParse(AlgorithmType, out parsed)) return parsed;
                return EDatumAlgorithm.TwoLineIntersect; //260423 hbk Phase 12 — 미지원 문자열 폴백 (하위호환)
            }
        }

        //260423 hbk Phase 12 D-12 — Line1 ROI 시맨틱스는 AlgorithmType 에 따라 달라진다:
        //260423 hbk   TwoLineIntersect:         1st 라인 ROI (기준 X축 방향 에지 라인)
        //260423 hbk   VerticalTwoHorizontal:    수직 ROI (수직 에지 라인)
        //260423 hbk   CircleTwoHorizontal:      미사용 (기본값 0 유지)
        /// <summary>
        /// Line1 ROI — 알고리즘별 의미:
        ///   TwoLineIntersect: 1st 라인 ROI (기준 X축 방향)
        ///   VerticalTwoHorizontal: 수직 ROI (수직 에지 라인)
        ///   CircleTwoHorizontal: 미사용 (기본값 0 유지)
        /// </summary>
        //260409 hbk Phase 4: Line1 ROI (기준 X축 방향 에지 라인)
        [Category("Datum|Line1 ROI")]
        public double Line1_Row { get; set; } = 0;
        public double Line1_Col { get; set; } = 0;
        public double Line1_Phi { get; set; } = 0;
        public double Line1_Length1 { get; set; } = 0;
        public double Line1_Length2 { get; set; } = 0;

        //260409 hbk Phase 4: Line2 ROI (기준 Y축 방향 에지 라인, 기본값 PI/2 = 수직)
        [Category("Datum|Line2 ROI")]
        public double Line2_Row { get; set; } = 0;
        public double Line2_Col { get; set; } = 0;
        public double Line2_Phi { get; set; } = 0;
        public double Line2_Length1 { get; set; } = 0;
        public double Line2_Length2 { get; set; } = 0;

        //260423 hbk Phase 12 D-10 — Circle ROI (CircleTwoHorizontal 전용 검색 영역)
        //260423 hbk  CircleROI_Radius > 0 이 ROI 설정 완료 판정 기준.
        [Category("Datum|Circle ROI")]
        public double CircleROI_Row    { get; set; } = 0; //260423 hbk Phase 12 D-10
        public double CircleROI_Col    { get; set; } = 0; //260423 hbk Phase 12 D-10
        public double CircleROI_Radius { get; set; } = 0; //260423 hbk Phase 12 D-10

        //260423 hbk Phase 12 D-11 — 수평 A ROI (CircleTwoHorizontal + VerticalTwoHorizontal 공용)
        //260423 hbk  A/B 순서 의존성 없음 — concat + FitLineContourXld 이므로 교환 대칭.
        //260423 hbk  Length1 > 0 && Length2 > 0 이 ROI 설정 완료 판정 기준.
        [Category("Datum|Horizontal A ROI")]
        public double Horizontal_A_Row     { get; set; } = 0; //260423 hbk Phase 12 D-11
        public double Horizontal_A_Col     { get; set; } = 0; //260423 hbk Phase 12 D-11
        public double Horizontal_A_Phi     { get; set; } = 0; //260423 hbk Phase 12 D-11
        public double Horizontal_A_Length1 { get; set; } = 0; //260423 hbk Phase 12 D-11
        public double Horizontal_A_Length2 { get; set; } = 0; //260423 hbk Phase 12 D-11

        //260423 hbk Phase 12 D-11 — 수평 B ROI
        [Category("Datum|Horizontal B ROI")]
        public double Horizontal_B_Row     { get; set; } = 0; //260423 hbk Phase 12 D-11
        public double Horizontal_B_Col     { get; set; } = 0; //260423 hbk Phase 12 D-11
        public double Horizontal_B_Phi     { get; set; } = 0; //260423 hbk Phase 12 D-11
        public double Horizontal_B_Length1 { get; set; } = 0; //260423 hbk Phase 12 D-11
        public double Horizontal_B_Length2 { get; set; } = 0; //260423 hbk Phase 12 D-11

        //260409 hbk Phase 4: 기준 원점 및 각도 (티칭 시 저장)
        [Category("Datum|Reference")]
        public double RefOriginRow { get; set; } = 0;
        public double RefOriginCol { get; set; } = 0;
        public double RefAngleRad { get; set; } = 0;

        //260409 hbk Phase 4: 에지 검출 파라미터 (FAIConfig와 동일 패턴)
        [Category("Datum|Edge Detection")]
        public int EdgeThreshold { get; set; } = 20;
        public double Sigma { get; set; } = 1.0;
        [ItemsSourceProperty(nameof(EdgePolarityList))] //260423 hbk WR-RT-02 ComboBox 처리
        public string EdgePolarity { get; set; } = "all"; // Halcon MeasurePos polarity: "all", "positive", "negative"

        //260423 hbk WR-RT-02 PropertyGrid ComboBox 옵션 래퍼 — Datum은 Halcon 원시 값 사용
        [PropertyTools.DataAnnotations.Browsable(false)]
        public List<string> EdgePolarityList { get { return EdgeOptionLists.DatumPolarities; } }

        //260409 hbk Phase 4: 설정 완료 플래그 — 티칭 후 true, 기본값 false
        [Category("Datum|Status")]
        public bool IsConfigured { get; set; } = false;

        //260409 hbk Phase 4: 런타임 전용 (HTuple은 ParamBase 직렬화 대상 아님)
        [PropertyTools.DataAnnotations.Browsable(false)]
        public HTuple CurrentTransform { get; set; }

        [PropertyTools.DataAnnotations.Browsable(false)]
        public bool LastFindSucceeded { get; set; }

        //260423 hbk Phase 11 D-11 — 검출 라인 오버레이용 휘발성 필드 (TryTeachDatum 성공 시 DatumFindingService가 기록)
        [PropertyTools.DataAnnotations.Browsable(false)]
        public double Line1Detected_RBegin { get; set; }
        [PropertyTools.DataAnnotations.Browsable(false)]
        public double Line1Detected_CBegin { get; set; }
        [PropertyTools.DataAnnotations.Browsable(false)]
        public double Line1Detected_REnd { get; set; }
        [PropertyTools.DataAnnotations.Browsable(false)]
        public double Line1Detected_CEnd { get; set; }
        [PropertyTools.DataAnnotations.Browsable(false)]
        public double Line2Detected_RBegin { get; set; }
        [PropertyTools.DataAnnotations.Browsable(false)]
        public double Line2Detected_CBegin { get; set; }
        [PropertyTools.DataAnnotations.Browsable(false)]
        public double Line2Detected_REnd { get; set; }
        [PropertyTools.DataAnnotations.Browsable(false)]
        public double Line2Detected_CEnd { get; set; }
        [PropertyTools.DataAnnotations.Browsable(false)]
        public bool LastTeachSucceeded { get; set; }

        //260423 hbk Phase 12 D-10 — Circle 검출 결과 휘발성 (TryTeachDatum 성공 시 DatumFindingService가 기록)
        //260423 hbk  ParamBase reflection이 Browsable 무시하고 public double 직렬화 — INI 에 0 으로 기록됨 (Phase 11 Line*Detected_* 수용 패턴과 동일)
        [PropertyTools.DataAnnotations.Browsable(false)]
        public double CircleCenter_Row { get; set; } //260423 hbk Phase 12 D-10
        [PropertyTools.DataAnnotations.Browsable(false)]
        public double CircleCenter_Col { get; set; } //260423 hbk Phase 12 D-10
        [PropertyTools.DataAnnotations.Browsable(false)]
        public double CircleDetected_Radius { get; set; } //260423 hbk Phase 12 D-10

        public DatumConfig(object owner) : base(owner) {
        }
    }
}
