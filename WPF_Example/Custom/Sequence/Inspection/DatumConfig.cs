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
    //260503 hbk Phase 17 D-09 — PropertyGrid 동적 노출 (AlgorithmType 별 필터). ParamBase INI 직렬화는 GetType().GetProperties() Reflection 경로 사용 → ICustomTypeDescriptor 영향 0 (확인: ParamBase.cs L75/L325/L370).
    public class DatumConfig : ParamBase, System.ComponentModel.ICustomTypeDescriptor, IOfflineImageParam { //260518 hbk #3 — IOfflineImageParam 추가

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

        //260511 hbk Phase 22 IMG-01 — Datum 티칭 시 사용한 기준 이미지 경로. INI 직렬화는 ParamBase reflection 이 자동 처리 (ParamBase.cs L325-339 Save 의 case "String", L385-395 Load 의 case "String"). 검사 실행 시 이미지는 별도 ShotConfig.SimulImagePath 사용 — 역할 분리 유지. 키 미존재 → EnsurePerRoiDefaults 에서 "" 정규화 (T2).
        [Category("Datum|ImageSource")]
        public string TeachingImagePath { get; set; } = "";

        //260518 hbk #3 IOfflineImageParam — Datum 노드 Load 버튼이 선택 경로를 TeachingImagePath 에 기록.
        //  Shot 노드(ShotConfig)는 SimulImagePath, Datum 노드는 TeachingImagePath 로 역할 분리.
        /// <summary>
        /// Datum 노드 오프라인 이미지 경로 게터 — TeachingImagePath 를 backing 으로 사용한다.
        /// </summary>
        public string GetLatestImagePath()
        {
            return TeachingImagePath;
        }

        /// <summary>
        /// Datum 노드 오프라인 이미지 경로 세터 — 선택한 경로를 TeachingImagePath 에 기록한다.
        /// </summary>
        public void SetLatestImagePath(string imagePath)
        {
            TeachingImagePath = imagePath;
        }

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

        //260426 hbk Phase 14-02 Req 2 — TwoLineIntersect 두 라인 직각성 게이트 임계각 (도)
        //  0  = 게이트 off (어떤 각도여도 PASS)
        //  10 = default (90°±10° 허용)
        //  range hint: 0~45°. INI 미존재 시 default 10° (값이 0 이면 명시 off 의도로 간주).
        //  ParamBase reflection 자동 직렬화 — 별도 Save/Load 코드 불필요.
        [Category("Datum|Algorithm")]
        public double TwoLineAngleToleranceDeg { get; set; } = 10.0;

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
        //260426 hbk Phase 14-03 D-08 — Category prefix labeling (TLI=TwoLineIntersect)
        [Category("Datum|Line1 (TLI) ROI")]
        public double Line1_Row { get; set; } = 0;
        public double Line1_Col { get; set; } = 0;
        //260426 hbk Phase 13 D-PRP-PHIFIX — radian 원본은 PropertyGrid 에서 숨기고 PhiDeg wrapper 노출 (사용자는 도 단위로 입력)
        [PropertyTools.DataAnnotations.Browsable(false)]
        public double Line1_Phi { get; set; } = 0;
        //260426 hbk Phase 13 D-PRP-PHIFIX — Phi 를 도(degree) 단위로 노출 (Line1_Phi = PhiDeg * PI / 180).
        //  수평 에지 검출 시 90, 수직 에지 검출 시 0 (기본). MeasurePos 가 ROI 의 긴 축 방향으로 traverse 하며 수직 그라디언트 측정.
        public double Line1_PhiDeg
        {
            get { return Line1_Phi * 180.0 / System.Math.PI; }
            set { Line1_Phi = value * System.Math.PI / 180.0; }
        }
        public double Line1_Length1 { get; set; } = 0;
        public double Line1_Length2 { get; set; } = 0;

        //260425 hbk Phase 13 D-PRP-02 — Line1 ROI 전용 에지 파라미터 (per-ROI 독립 튜닝)
        //  sentinel 0/"" 일 때 EnsurePerRoiDefaults() 가 legacy 글로벌 값으로 복제.
        //260426 hbk Phase 14-03 D-08 — Category prefix labeling (TLI)
        [Category("Datum|Line1 (TLI) Edge")]
        public int    Line1_EdgeThreshold   { get; set; } = 0;
        public double Line1_Sigma           { get; set; } = 0;
        [System.ComponentModel.Description("일반적으로 수평 방향 ROI 에는 LtoR 또는 RtoL 을 권장합니다.")] //260503 hbk Phase 17 D-04
        [ItemsSourceProperty(nameof(Line1_EdgeDirectionList))]
        public string Line1_EdgeDirection   { get; set; } = "";
        public int    Line1_EdgeSampleCount { get; set; } = 0;
        public int    Line1_EdgeTrimCount   { get; set; } = 0;
        [ItemsSourceProperty(nameof(Line1_EdgePolarityList))]
        public string Line1_EdgePolarity    { get; set; } = "";
        [ItemsSourceProperty(nameof(Line1_EdgeSelectionList))] //260429 hbk Phase 15 — EdgeSelection 명시 처리
        public string Line1_EdgeSelection   { get; set; } = ""; //260429 hbk Phase 15

        [PropertyTools.DataAnnotations.Browsable(false)]
        public List<string> Line1_EdgeDirectionList { get { return EdgeOptionLists.Directions; } }
        [PropertyTools.DataAnnotations.Browsable(false)]
        public List<string> Line1_EdgePolarityList  { get { return EdgeOptionLists.DatumPolarities; } }
        [PropertyTools.DataAnnotations.Browsable(false)]
        public List<string> Line1_EdgeSelectionList { get { return EdgeOptionLists.Selections; } } //260429 hbk Phase 15

        //260426 hbk Phase 14-03 Req 3 — Vertical ROI 그룹 (VerticalTwoHorizontal 전용 수직 에지 라인 — 의미상 별도)
        //  D-08 Category prefix labeling — "(VTH)" 태그로 알고리즘 가시 구분 (PropertyGrid 동적 숨김 fallback)
        [Category("Datum|Vertical (VTH) ROI")]
        public double Vertical_Row { get; set; } = 0;
        public double Vertical_Col { get; set; } = 0;
        [PropertyTools.DataAnnotations.Browsable(false)]
        public double Vertical_Phi { get; set; } = 0;
        public double Vertical_PhiDeg
        {
            get { return Vertical_Phi * 180.0 / System.Math.PI; }
            set { Vertical_Phi = value * System.Math.PI / 180.0; }
        }
        public double Vertical_Length1 { get; set; } = 0;
        public double Vertical_Length2 { get; set; } = 0;

        //260426 hbk Phase 14-03 Req 3 — Vertical ROI 전용 에지 파라미터 (per-ROI 독립 튜닝)
        [Category("Datum|Vertical (VTH) Edge")]
        public int    Vertical_EdgeThreshold   { get; set; } = 0;
        public double Vertical_Sigma           { get; set; } = 0;
        [System.ComponentModel.Description("일반적으로 수평 방향 ROI 에는 LtoR 또는 RtoL 을 권장합니다.")] //260503 hbk Phase 17 D-04
        [ItemsSourceProperty(nameof(Vertical_EdgeDirectionList))]
        public string Vertical_EdgeDirection   { get; set; } = "";
        public int    Vertical_EdgeSampleCount { get; set; } = 0;
        public int    Vertical_EdgeTrimCount   { get; set; } = 0;
        [ItemsSourceProperty(nameof(Vertical_EdgePolarityList))]
        public string Vertical_EdgePolarity    { get; set; } = "";
        [ItemsSourceProperty(nameof(Vertical_EdgeSelectionList))] //260429 hbk Phase 15 — EdgeSelection 명시 처리
        public string Vertical_EdgeSelection   { get; set; } = ""; //260429 hbk Phase 15

        [PropertyTools.DataAnnotations.Browsable(false)]
        public List<string> Vertical_EdgeDirectionList { get { return EdgeOptionLists.Directions; } }
        [PropertyTools.DataAnnotations.Browsable(false)]
        public List<string> Vertical_EdgePolarityList  { get { return EdgeOptionLists.DatumPolarities; } }
        [PropertyTools.DataAnnotations.Browsable(false)]
        public List<string> Vertical_EdgeSelectionList { get { return EdgeOptionLists.Selections; } } //260429 hbk Phase 15

        //260409 hbk Phase 4: Line2 ROI (기준 Y축 방향 에지 라인, 기본값 PI/2 = 수직)
        //260426 hbk Phase 14-03 D-08 — Category prefix labeling (TLI)
        [Category("Datum|Line2 (TLI) ROI")]
        public double Line2_Row { get; set; } = 0;
        public double Line2_Col { get; set; } = 0;
        //260426 hbk Phase 13 D-PRP-PHIFIX — radian 원본은 PropertyGrid 에서 숨기고 PhiDeg wrapper 노출 (사용자는 도 단위로 입력)
        [PropertyTools.DataAnnotations.Browsable(false)]
        public double Line2_Phi { get; set; } = 0;
        //260426 hbk Phase 13 D-PRP-PHIFIX — Phi 를 도(degree) 단위로 노출 (Line2_Phi = PhiDeg * PI / 180).
        //  수평 에지 검출 시 90, 수직 에지 검출 시 0 (기본).
        public double Line2_PhiDeg
        {
            get { return Line2_Phi * 180.0 / System.Math.PI; }
            set { Line2_Phi = value * System.Math.PI / 180.0; }
        }
        public double Line2_Length1 { get; set; } = 0;
        public double Line2_Length2 { get; set; } = 0;

        //260425 hbk Phase 13 D-PRP-02 — Line2 ROI 전용 에지 파라미터 (per-ROI 독립 튜닝)
        //260426 hbk Phase 14-03 D-08 — Category prefix labeling (TLI)
        [Category("Datum|Line2 (TLI) Edge")]
        public int    Line2_EdgeThreshold   { get; set; } = 0;
        public double Line2_Sigma           { get; set; } = 0;
        [System.ComponentModel.Description("일반적으로 수평 방향 ROI 에는 LtoR 또는 RtoL 을 권장합니다.")] //260503 hbk Phase 17 D-04
        [ItemsSourceProperty(nameof(Line2_EdgeDirectionList))]
        public string Line2_EdgeDirection   { get; set; } = "";
        public int    Line2_EdgeSampleCount { get; set; } = 0;
        public int    Line2_EdgeTrimCount   { get; set; } = 0;
        [ItemsSourceProperty(nameof(Line2_EdgePolarityList))]
        public string Line2_EdgePolarity    { get; set; } = "";
        [ItemsSourceProperty(nameof(Line2_EdgeSelectionList))] //260429 hbk Phase 15 — EdgeSelection 명시 처리
        public string Line2_EdgeSelection   { get; set; } = ""; //260429 hbk Phase 15

        [PropertyTools.DataAnnotations.Browsable(false)]
        public List<string> Line2_EdgeDirectionList { get { return EdgeOptionLists.Directions; } }
        [PropertyTools.DataAnnotations.Browsable(false)]
        public List<string> Line2_EdgePolarityList  { get { return EdgeOptionLists.DatumPolarities; } }
        [PropertyTools.DataAnnotations.Browsable(false)]
        public List<string> Line2_EdgeSelectionList { get { return EdgeOptionLists.Selections; } } //260429 hbk Phase 15

        //260423 hbk Phase 12 D-10 — Circle ROI (CircleTwoHorizontal 전용 검색 영역)
        //260423 hbk  CircleROI_Radius > 0 이 ROI 설정 완료 판정 기준.
        //260426 hbk Phase 14-03 D-08 — Category prefix labeling (CTH=CircleTwoHorizontal)
        [Category("Datum|Circle (CTH) ROI")]
        public double CircleROI_Row    { get; set; } = 0; //260423 hbk Phase 12 D-10
        public double CircleROI_Col    { get; set; } = 0; //260423 hbk Phase 12 D-10
        public double CircleROI_Radius { get; set; } = 0; //260423 hbk Phase 12 D-10

        //260426 hbk Phase 14-04 Req 4 — Circle polar-sampling 알고리즘 파라미터 (TryFindCircleByPolarSampling 전용)
        //  Circle_PolarStepDeg: 회전 각도 step (1~30°, 360/step = 점 개수)
        //  Circle_RectL1Ratio:  사각형 ROI 의 length1 = radius × ratio (반경 방향)
        //  Circle_RectL2Ratio:  사각형 ROI 의 length2 = radius × ratio (접선 방향)
        //  default 10° / 0.05 / 0.05. INI 미존재 시 자동 fallback (회귀 0).
        //  사용자 0/음수 입력 시 TryFindCircleByPolarSampling 진입부에서 sanity clamp 으로 default 복원.
        //260427 hbk Phase 14 fix — Circle polar 파라미터 PropertyGrid 노출 추가 (Category 명시)
        [Category("Datum|Circle (CTH) Polar")]
        public double Circle_PolarStepDeg  { get; set; } = 10.0;
        //260430 hbk Quick 260430-hox — default 0.05 → 0.02 (radius 200 → strip 8px). Phase 16 UAT FAIL root cause: 큰 strip → MeasurePos edge 노이즈 → "insufficient polar samples (1)".
        public double Circle_RectL1Ratio   { get; set; } = 0.02;
        public double Circle_RectL2Ratio   { get; set; } = 0.02;

        //260425 hbk Phase 13 D-PRP-02 — Circle ROI 전용 에지 파라미터 (per-ROI 독립 튜닝)
        //260426 hbk Phase 14-03 D-08 — Category prefix labeling (CTH)
        [Category("Datum|Circle (CTH) Edge")]
        public int    Circle_EdgeThreshold   { get; set; } = 0;
        public double Circle_Sigma           { get; set; } = 0;
        //260507 hbk Phase 18 CO-01 — Circle 알고리즘은 EdgeDirection 대신 RadialDirection (Inward/Outward) 사용 → 영구 hide.
        //  Phase 17 D-03 의 IsHiddenForAlgorithm CTH 분기 hide 가 dynamic enumeration 경로 차이로 불안정하게 동작 → 정적 Browsable(false) 로 확정.
        [PropertyTools.DataAnnotations.Browsable(false)] //260507 hbk Phase 18 CO-01
        [System.ComponentModel.Description("일반적으로 수평 방향 ROI 에는 LtoR 또는 RtoL 을 권장합니다.")] //260503 hbk Phase 17 D-04
        [ItemsSourceProperty(nameof(Circle_EdgeDirectionList))]
        public string Circle_EdgeDirection   { get; set; } = "";
        public int    Circle_EdgeSampleCount { get; set; } = 0;
        public int    Circle_EdgeTrimCount   { get; set; } = 0;
        [ItemsSourceProperty(nameof(Circle_EdgePolarityList))]
        public string Circle_EdgePolarity    { get; set; } = "";
        [ItemsSourceProperty(nameof(Circle_EdgeSelectionList))] //260429 hbk Phase 15 — EdgeSelection 명시 처리
        public string Circle_EdgeSelection   { get; set; } = ""; //260429 hbk Phase 15

        [ItemsSourceProperty(nameof(Circle_RadialDirectionList))] //260503 hbk Phase 17 D-02
        public string Circle_RadialDirection { get; set; } = ""; //260503 hbk Phase 17 D-02 — 안→밖(Inward, polarity=positive) / 밖→안(Outward, polarity=negative)

        [PropertyTools.DataAnnotations.Browsable(false)]
        public List<string> Circle_EdgeDirectionList { get { return EdgeOptionLists.Directions; } }
        [PropertyTools.DataAnnotations.Browsable(false)]
        public List<string> Circle_EdgePolarityList  { get { return EdgeOptionLists.DatumPolarities; } }
        [PropertyTools.DataAnnotations.Browsable(false)]
        public List<string> Circle_EdgeSelectionList { get { return EdgeOptionLists.Selections; } } //260429 hbk Phase 15
        [PropertyTools.DataAnnotations.Browsable(false)] //260503 hbk Phase 17 D-02
        public List<string> Circle_RadialDirectionList { get { return EdgeOptionLists.RadialDirections; } } //260503 hbk Phase 17 D-02

        //260423 hbk Phase 12 D-11 — 수평 A ROI (CircleTwoHorizontal + VerticalTwoHorizontal 공용)
        //260423 hbk  A/B 순서 의존성 없음 — concat + FitLineContourXld 이므로 교환 대칭.
        //260423 hbk  Length1 > 0 && Length2 > 0 이 ROI 설정 완료 판정 기준.
        //260426 hbk Phase 14-03 D-08 — Category prefix labeling (CTH+VTH 공용)
        [Category("Datum|Horizontal_A (CTH/VTH) ROI")]
        public double Horizontal_A_Row     { get; set; } = 0; //260423 hbk Phase 12 D-11
        public double Horizontal_A_Col     { get; set; } = 0; //260423 hbk Phase 12 D-11
        //260426 hbk Phase 13 D-PRP-PHIFIX — radian 원본은 PropertyGrid 에서 숨기고 PhiDeg wrapper 노출 (사용자는 도 단위로 입력)
        [PropertyTools.DataAnnotations.Browsable(false)]
        public double Horizontal_A_Phi     { get; set; } = 0; //260423 hbk Phase 12 D-11
        //260426 hbk Phase 13 D-PRP-PHIFIX — Phi 를 도(degree) 단위로 노출 (Horizontal_A_Phi = PhiDeg * PI / 180).
        //  수평 에지 검출 시 90, 수직 에지 검출 시 0 (기본).
        public double Horizontal_A_PhiDeg
        {
            get { return Horizontal_A_Phi * 180.0 / System.Math.PI; }
            set { Horizontal_A_Phi = value * System.Math.PI / 180.0; }
        }
        public double Horizontal_A_Length1 { get; set; } = 0; //260423 hbk Phase 12 D-11
        public double Horizontal_A_Length2 { get; set; } = 0; //260423 hbk Phase 12 D-11

        //260425 hbk Phase 13 D-PRP-02 — Horizontal A ROI 전용 에지 파라미터 (per-ROI 독립 튜닝)
        //260426 hbk Phase 14-03 D-08 — Category prefix labeling
        [Category("Datum|Horizontal_A (CTH/VTH) Edge")]
        public int    Horizontal_A_EdgeThreshold   { get; set; } = 0;
        public double Horizontal_A_Sigma           { get; set; } = 0;
        [System.ComponentModel.Description("일반적으로 수평 방향 ROI 에는 LtoR 또는 RtoL 을 권장합니다.")] //260503 hbk Phase 17 D-04
        [ItemsSourceProperty(nameof(Horizontal_A_EdgeDirectionList))]
        public string Horizontal_A_EdgeDirection   { get; set; } = "";
        public int    Horizontal_A_EdgeSampleCount { get; set; } = 0;
        public int    Horizontal_A_EdgeTrimCount   { get; set; } = 0;
        [ItemsSourceProperty(nameof(Horizontal_A_EdgePolarityList))]
        public string Horizontal_A_EdgePolarity    { get; set; } = "";
        [ItemsSourceProperty(nameof(Horizontal_A_EdgeSelectionList))] //260429 hbk Phase 15 — EdgeSelection 명시 처리
        public string Horizontal_A_EdgeSelection   { get; set; } = ""; //260429 hbk Phase 15

        [PropertyTools.DataAnnotations.Browsable(false)]
        public List<string> Horizontal_A_EdgeDirectionList { get { return EdgeOptionLists.Directions; } }
        [PropertyTools.DataAnnotations.Browsable(false)]
        public List<string> Horizontal_A_EdgePolarityList  { get { return EdgeOptionLists.DatumPolarities; } }
        [PropertyTools.DataAnnotations.Browsable(false)]
        public List<string> Horizontal_A_EdgeSelectionList { get { return EdgeOptionLists.Selections; } } //260429 hbk Phase 15

        //260423 hbk Phase 12 D-11 — 수평 B ROI
        //260426 hbk Phase 14-03 D-08 — Category prefix labeling
        [Category("Datum|Horizontal_B (CTH/VTH) ROI")]
        public double Horizontal_B_Row     { get; set; } = 0; //260423 hbk Phase 12 D-11
        public double Horizontal_B_Col     { get; set; } = 0; //260423 hbk Phase 12 D-11
        //260426 hbk Phase 13 D-PRP-PHIFIX — radian 원본은 PropertyGrid 에서 숨기고 PhiDeg wrapper 노출 (사용자는 도 단위로 입력)
        [PropertyTools.DataAnnotations.Browsable(false)]
        public double Horizontal_B_Phi     { get; set; } = 0; //260423 hbk Phase 12 D-11
        //260426 hbk Phase 13 D-PRP-PHIFIX — Phi 를 도(degree) 단위로 노출 (Horizontal_B_Phi = PhiDeg * PI / 180).
        //  수평 에지 검출 시 90, 수직 에지 검출 시 0 (기본).
        public double Horizontal_B_PhiDeg
        {
            get { return Horizontal_B_Phi * 180.0 / System.Math.PI; }
            set { Horizontal_B_Phi = value * System.Math.PI / 180.0; }
        }
        public double Horizontal_B_Length1 { get; set; } = 0; //260423 hbk Phase 12 D-11
        public double Horizontal_B_Length2 { get; set; } = 0; //260423 hbk Phase 12 D-11

        //260425 hbk Phase 13 D-PRP-02 — Horizontal B ROI 전용 에지 파라미터 (per-ROI 독립 튜닝)
        //260426 hbk Phase 14-03 D-08 — Category prefix labeling
        [Category("Datum|Horizontal_B (CTH/VTH) Edge")]
        public int    Horizontal_B_EdgeThreshold   { get; set; } = 0;
        public double Horizontal_B_Sigma           { get; set; } = 0;
        [System.ComponentModel.Description("일반적으로 수평 방향 ROI 에는 LtoR 또는 RtoL 을 권장합니다.")] //260503 hbk Phase 17 D-04
        [ItemsSourceProperty(nameof(Horizontal_B_EdgeDirectionList))]
        public string Horizontal_B_EdgeDirection   { get; set; } = "";
        public int    Horizontal_B_EdgeSampleCount { get; set; } = 0;
        public int    Horizontal_B_EdgeTrimCount   { get; set; } = 0;
        [ItemsSourceProperty(nameof(Horizontal_B_EdgePolarityList))]
        public string Horizontal_B_EdgePolarity    { get; set; } = "";
        [ItemsSourceProperty(nameof(Horizontal_B_EdgeSelectionList))] //260429 hbk Phase 15 — EdgeSelection 명시 처리
        public string Horizontal_B_EdgeSelection   { get; set; } = ""; //260429 hbk Phase 15

        [PropertyTools.DataAnnotations.Browsable(false)]
        public List<string> Horizontal_B_EdgeDirectionList { get { return EdgeOptionLists.Directions; } }
        [PropertyTools.DataAnnotations.Browsable(false)]
        public List<string> Horizontal_B_EdgePolarityList  { get { return EdgeOptionLists.DatumPolarities; } }
        [PropertyTools.DataAnnotations.Browsable(false)]
        public List<string> Horizontal_B_EdgeSelectionList { get { return EdgeOptionLists.Selections; } } //260429 hbk Phase 15

        //260409 hbk Phase 4: 기준 원점 및 각도 (티칭 시 저장)
        [Category("Datum|Reference")]
        public double RefOriginRow { get; set; } = 0;
        public double RefOriginCol { get; set; } = 0;
        public double RefAngleRad { get; set; } = 0;

        //260425 hbk Phase 13 D-PRP-01 — Legacy 글로벌 에지 파라미터 (INI 하위호환 유지, PropertyGrid 노출 안 함)
        //  per-ROI 30 필드로 대체 (Line1_*/Line2_*/Circle_*/Horizontal_A_*/Horizontal_B_*).
        //  EnsurePerRoiDefaults() 가 최초 1회 글로벌 → per-ROI 복제.
        [Category("Datum|Edge Detection (legacy)")]
        [PropertyTools.DataAnnotations.Browsable(false)]
        public int EdgeThreshold { get; set; } = 20;
        [PropertyTools.DataAnnotations.Browsable(false)]
        public double Sigma { get; set; } = 1.0;
        [PropertyTools.DataAnnotations.Browsable(false)]
        public string EdgePolarity { get; set; } = "all"; // Halcon MeasurePos polarity: "all", "positive", "negative"

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

        //260425 hbk Phase 13 D-VIZ-01 — raw 검출 에지점 (TryFindLine / TryExtractEdgePoints / TryFindCircle 직후 write-back)
        //  HTuple 은 ParamBase 직렬화 미지원 (Phase 4 D-11 패턴 동일) → INI 영향 0, runtime 전용.
        //  RenderDatumOverlay 가 LastTeachSucceeded 분기에서 ROI 별 색상으로 점 마커 렌더.
        [PropertyTools.DataAnnotations.Browsable(false)]
        public HTuple Line1_DetectedEdgeRows { get; set; }
        [PropertyTools.DataAnnotations.Browsable(false)]
        public HTuple Line1_DetectedEdgeCols { get; set; }

        [PropertyTools.DataAnnotations.Browsable(false)]
        public HTuple Line2_DetectedEdgeRows { get; set; }
        [PropertyTools.DataAnnotations.Browsable(false)]
        public HTuple Line2_DetectedEdgeCols { get; set; }

        [PropertyTools.DataAnnotations.Browsable(false)]
        public HTuple Circle_DetectedEdgeRows { get; set; }
        [PropertyTools.DataAnnotations.Browsable(false)]
        public HTuple Circle_DetectedEdgeCols { get; set; }

        [PropertyTools.DataAnnotations.Browsable(false)]
        public HTuple Horizontal_A_DetectedEdgeRows { get; set; }
        [PropertyTools.DataAnnotations.Browsable(false)]
        public HTuple Horizontal_A_DetectedEdgeCols { get; set; }

        [PropertyTools.DataAnnotations.Browsable(false)]
        public HTuple Horizontal_B_DetectedEdgeRows { get; set; }
        [PropertyTools.DataAnnotations.Browsable(false)]
        public HTuple Horizontal_B_DetectedEdgeCols { get; set; }

        //260426 hbk Phase 14-03 Req 3 — Vertical raw 검출 에지점 (volatile, INI 영향 0 — Phase 13-05 D-VIZ-01 패턴)
        [PropertyTools.DataAnnotations.Browsable(false)]
        public HTuple Vertical_DetectedEdgeRows { get; set; }
        [PropertyTools.DataAnnotations.Browsable(false)]
        public HTuple Vertical_DetectedEdgeCols { get; set; }

        //260503 hbk Phase 17 D-13 — DetectedOrigin transient (TryFindDatum write-back, INI 0 영향 — ParamBase double 직렬화하나 [Browsable(false)] 로 PropertyGrid 미표시)
        [System.ComponentModel.Browsable(false)] //260503 hbk Phase 17 D-13
        [PropertyTools.DataAnnotations.Browsable(false)] //260503 hbk Phase 17 D-13
        [Newtonsoft.Json.JsonIgnore] //260503 hbk Phase 17 D-13
        public double DetectedOriginRow { get; set; } //260503 hbk Phase 17 D-13
        [System.ComponentModel.Browsable(false)] //260503 hbk Phase 17 D-13
        [PropertyTools.DataAnnotations.Browsable(false)] //260503 hbk Phase 17 D-13
        [Newtonsoft.Json.JsonIgnore] //260503 hbk Phase 17 D-13
        public double DetectedOriginCol { get; set; } //260503 hbk Phase 17 D-13
        [System.ComponentModel.Browsable(false)] //260503 hbk Phase 17 D-13
        [PropertyTools.DataAnnotations.Browsable(false)] //260503 hbk Phase 17 D-13
        [Newtonsoft.Json.JsonIgnore] //260503 hbk Phase 17 D-13
        public double DetectedRefAngle { get; set; } //260503 hbk Phase 17 D-13

        //260519 hbk Phase 31 hotfix#3 — datum 2차(수직) 기준선 각도(rad). DetectedRefAngle 은 1차(수평) 각도.
        //  X축 측정이 Line1+90° 가 아닌 실제 datum 수직선을 기준하도록 DatumFindingService 가 write-back.
        [System.ComponentModel.Browsable(false)] //260519 hbk Phase 31 hotfix#3
        [PropertyTools.DataAnnotations.Browsable(false)] //260519 hbk Phase 31 hotfix#3
        [Newtonsoft.Json.JsonIgnore] //260519 hbk Phase 31 hotfix#3
        public double DetectedRefAngle2 { get; set; } //260519 hbk Phase 31 hotfix#3

        //260505 hbk Phase 18 CO-05 — Circle polar strip 별 검출 성공 여부 (TryTeachCircleTwoHorizontal write-back).
        //  INI/JSON 직렬화 제외 (transient). RenderCircleStripOverlay 소비. bool[] 크기 = stepCount.
        [System.ComponentModel.Browsable(false)] //260505 hbk Phase 18 CO-05
        [PropertyTools.DataAnnotations.Browsable(false)] //260505 hbk Phase 18 CO-05
        [Newtonsoft.Json.JsonIgnore] //260505 hbk Phase 18 CO-05
        public bool[] CircleStripSuccesses { get; set; } //260505 hbk Phase 18 CO-05

        //260503 hbk Phase 17 D-16 — 결과 메트릭 PropertyGrid 노출 (ReadOnly, 사용자가 검출 품질 즉시 확인)
        //  PropertyTools.Wpf 가 지원하는 ReadOnly attribute 는 PropertyTools.DataAnnotations.ReadOnly (ParamBase.cs L37/L53 confirmed canonical).
        //  System.ComponentModel.ReadOnly 도 함께 부착 — ICustomTypeDescriptor.GetProperties 경로의 안전판.
        [Category("Datum|Result")] //260503 hbk Phase 17 D-16
        [System.ComponentModel.ReadOnly(true)] //260503 hbk Phase 17 D-16
        [PropertyTools.DataAnnotations.ReadOnly(true)] //260503 hbk Phase 17 D-16
        public int DetectedEdgeCount { get; set; } //260503 hbk Phase 17 D-16

        [Category("Datum|Result")] //260503 hbk Phase 17 D-16
        [System.ComponentModel.ReadOnly(true)] //260503 hbk Phase 17 D-16
        [PropertyTools.DataAnnotations.ReadOnly(true)] //260503 hbk Phase 17 D-16
        public double DetectedFitRMSE { get; set; } //260503 hbk Phase 17 D-16

        [Category("Datum|Result")] //260503 hbk Phase 17 D-16
        [System.ComponentModel.ReadOnly(true)] //260503 hbk Phase 17 D-16
        [PropertyTools.DataAnnotations.ReadOnly(true)] //260503 hbk Phase 17 D-16
        public double DetectedAngleDeg { get; set; } //260503 hbk Phase 17 D-16

        //260425 hbk Phase 13 D-PRP-03 — 최초 로드 시 sentinel(per-ROI EdgeThreshold==0) 검출 → 글로벌 값 5 ROI 일괄 복제 (idempotent).
        //  DatumFindingService.TryTeach* / TryFindDatum 진입부에서 1회 호출.
        //  sentinel 가 아니면 (사용자가 per-ROI 값을 명시한 경우) 그대로 유지 — 멱등성 보장.
        public void EnsurePerRoiDefaults() {
            // Hardcoded fallback (legacy 글로벌이 모두 0/"" 인 극단 케이스)
            int    fbThreshold; //260509 hbk Phase 20 — ternary expanded
            if (EdgeThreshold > 0) fbThreshold = EdgeThreshold;
            else                   fbThreshold = 20;
            double fbSigma; //260509 hbk Phase 20 — ternary expanded
            if (Sigma > 0) fbSigma = Sigma;
            else           fbSigma = 1.0;
            string fbDirection   = "LtoR"; // legacy 글로벌에 EdgeDirection 없음
            int    fbSampleCount = 20;
            int    fbTrimCount   = 10;
            string fbPolarity; //260509 hbk Phase 20 — ternary expanded
            if (!string.IsNullOrEmpty(EdgePolarity)) fbPolarity = EdgePolarity;
            else                                     fbPolarity = "all";
            string fbSelection   = "First"; //260429 hbk Phase 15 — EdgeSelection 기본값 (Halcon MeasurePos "first" 와 매핑)

            // Line1
            if (Line1_EdgeThreshold == 0)        Line1_EdgeThreshold = fbThreshold;
            if (Line1_Sigma == 0)                Line1_Sigma = fbSigma;
            if (string.IsNullOrEmpty(Line1_EdgeDirection)) Line1_EdgeDirection = fbDirection;
            if (Line1_EdgeSampleCount == 0)      Line1_EdgeSampleCount = fbSampleCount;
            if (Line1_EdgeTrimCount == 0)        Line1_EdgeTrimCount = fbTrimCount;
            if (string.IsNullOrEmpty(Line1_EdgePolarity)) Line1_EdgePolarity = fbPolarity;
            if (string.IsNullOrEmpty(Line1_EdgeSelection)) Line1_EdgeSelection = fbSelection; //260429 hbk Phase 15

            // Line2
            if (Line2_EdgeThreshold == 0)        Line2_EdgeThreshold = fbThreshold;
            if (Line2_Sigma == 0)                Line2_Sigma = fbSigma;
            if (string.IsNullOrEmpty(Line2_EdgeDirection)) Line2_EdgeDirection = fbDirection;
            if (Line2_EdgeSampleCount == 0)      Line2_EdgeSampleCount = fbSampleCount;
            if (Line2_EdgeTrimCount == 0)        Line2_EdgeTrimCount = fbTrimCount;
            if (string.IsNullOrEmpty(Line2_EdgePolarity)) Line2_EdgePolarity = fbPolarity;
            if (string.IsNullOrEmpty(Line2_EdgeSelection)) Line2_EdgeSelection = fbSelection; //260429 hbk Phase 15

            // Circle
            if (Circle_EdgeThreshold == 0)       Circle_EdgeThreshold = fbThreshold;
            if (Circle_Sigma == 0)               Circle_Sigma = fbSigma;
            if (string.IsNullOrEmpty(Circle_EdgeDirection)) Circle_EdgeDirection = fbDirection;
            if (Circle_EdgeSampleCount == 0)     Circle_EdgeSampleCount = fbSampleCount;
            if (Circle_EdgeTrimCount == 0)       Circle_EdgeTrimCount = fbTrimCount;
            if (string.IsNullOrEmpty(Circle_EdgePolarity)) Circle_EdgePolarity = fbPolarity;
            if (string.IsNullOrEmpty(Circle_EdgeSelection)) Circle_EdgeSelection = fbSelection; //260429 hbk Phase 15
            if (string.IsNullOrEmpty(Circle_RadialDirection)) Circle_RadialDirection = "Inward"; //260503 hbk Phase 17 D-02 — sentinel "" → "Inward" fallback (INI 하위호환)
            if (TeachingImagePath == null) TeachingImagePath = ""; //260511 hbk Phase 22 IMG-01 — INI 키 미존재 시 null 가드 (ParamBase.Load String case 가 IniValue.Default → null 로 SetValue 가능 → 소비처 string.IsNullOrEmpty 가드 보완). 멱등성 보장 — 빈 문자열 아닌 사용자 셋업 값은 보존.

            // Horizontal_A
            if (Horizontal_A_EdgeThreshold == 0)   Horizontal_A_EdgeThreshold = fbThreshold;
            if (Horizontal_A_Sigma == 0)           Horizontal_A_Sigma = fbSigma;
            if (string.IsNullOrEmpty(Horizontal_A_EdgeDirection)) Horizontal_A_EdgeDirection = fbDirection;
            if (Horizontal_A_EdgeSampleCount == 0) Horizontal_A_EdgeSampleCount = fbSampleCount;
            if (Horizontal_A_EdgeTrimCount == 0)   Horizontal_A_EdgeTrimCount = fbTrimCount;
            if (string.IsNullOrEmpty(Horizontal_A_EdgePolarity)) Horizontal_A_EdgePolarity = fbPolarity;
            if (string.IsNullOrEmpty(Horizontal_A_EdgeSelection)) Horizontal_A_EdgeSelection = fbSelection; //260429 hbk Phase 15

            // Horizontal_B
            if (Horizontal_B_EdgeThreshold == 0)   Horizontal_B_EdgeThreshold = fbThreshold;
            if (Horizontal_B_Sigma == 0)           Horizontal_B_Sigma = fbSigma;
            if (string.IsNullOrEmpty(Horizontal_B_EdgeDirection)) Horizontal_B_EdgeDirection = fbDirection;
            if (Horizontal_B_EdgeSampleCount == 0) Horizontal_B_EdgeSampleCount = fbSampleCount;
            if (Horizontal_B_EdgeTrimCount == 0)   Horizontal_B_EdgeTrimCount = fbTrimCount;
            if (string.IsNullOrEmpty(Horizontal_B_EdgePolarity)) Horizontal_B_EdgePolarity = fbPolarity;
            if (string.IsNullOrEmpty(Horizontal_B_EdgeSelection)) Horizontal_B_EdgeSelection = fbSelection; //260429 hbk Phase 15

            //260426 hbk Phase 14-03 D-05/D-06 — Vertical 그룹 INI 하위호환 마이그레이션
            //  기존 INI 의 Line1_* 값을 Vertical_* sentinel(==0/"") 일 때 1회 복사.
            //  Line1_* zero-out 안 함 (회귀 위험 0, 사용자가 알고리즘을 TwoLineIntersect 로 다시 바꿔도 Line1_* 즉시 사용 가능).
            //  idempotent: 두 번째 호출 시 Vertical_* 가 모두 의미값 → 분기 미진입.
            //260509 hbk Phase 20 — Vertical_* migration ternaries expanded to nested if/else
            if (Vertical_EdgeThreshold == 0) {
                if (Line1_EdgeThreshold > 0) Vertical_EdgeThreshold = Line1_EdgeThreshold;
                else                         Vertical_EdgeThreshold = fbThreshold;
            }
            if (Vertical_Sigma == 0) {
                if (Line1_Sigma > 0) Vertical_Sigma = Line1_Sigma;
                else                 Vertical_Sigma = fbSigma;
            }
            if (string.IsNullOrEmpty(Vertical_EdgeDirection)) {
                if (!string.IsNullOrEmpty(Line1_EdgeDirection)) Vertical_EdgeDirection = Line1_EdgeDirection;
                else                                            Vertical_EdgeDirection = fbDirection;
            }
            if (Vertical_EdgeSampleCount == 0) {
                if (Line1_EdgeSampleCount > 0) Vertical_EdgeSampleCount = Line1_EdgeSampleCount;
                else                           Vertical_EdgeSampleCount = fbSampleCount;
            }
            if (Vertical_EdgeTrimCount == 0) {
                if (Line1_EdgeTrimCount > 0) Vertical_EdgeTrimCount = Line1_EdgeTrimCount;
                else                         Vertical_EdgeTrimCount = fbTrimCount;
            }
            if (string.IsNullOrEmpty(Vertical_EdgePolarity)) {
                if (!string.IsNullOrEmpty(Line1_EdgePolarity)) Vertical_EdgePolarity = Line1_EdgePolarity;
                else                                           Vertical_EdgePolarity = fbPolarity;
            }
            if (string.IsNullOrEmpty(Vertical_EdgeSelection))   Vertical_EdgeSelection   = fbSelection; //260429 hbk Phase 15

            //260426 hbk Phase 14-03 D-05 — Geometry 1회 복사 (사용자가 Vertical 그룹만 보고 알고리즘 운용 가능하도록)
            //   sentinel: Vertical_Length1 == 0
            if (Vertical_Length1 == 0 && Line1_Length1 > 0) {
                Vertical_Row     = Line1_Row;
                Vertical_Col     = Line1_Col;
                Vertical_Phi     = Line1_Phi;
                Vertical_Length1 = Line1_Length1;
                Vertical_Length2 = Line1_Length2;
            }
        }

        //260503 hbk Phase 17 D-09 — PropertyGrid 동적 노출 (AlgorithmType 별 필터)
        //260508 hbk Phase 19 fix — PropertyTools.Wpf PropertyGrid 는 GetProperties() 무인자 오버로드만 호출 (TypeDescriptor.GetProperties(object) 단일 인자 → ICustomTypeDescriptor.GetProperties() 무인자로 위임).
        //  Phase 17~19 의 GetProperties(Attribute[]) 본문은 호출되지 않는 dead code 였음. 무인자 오버로드로 hide 로직 이전.
        //  ParamBase INI 직렬화는 GetType().GetProperties() System.Reflection 경로 사용 — ICustomTypeDescriptor 영향 0 (ParamBase.cs L75/L325/L370).
        //  GetProperties(Attribute[]) 는 외부 사용처(LiveBinding 등) 안전판 — 동일 본문으로 유지.
        public System.ComponentModel.PropertyDescriptorCollection GetProperties(System.Attribute[] attributes) { //260508 hbk Phase 19 fix
            return BuildFilteredProperties(attributes); //260508 hbk Phase 19 fix
        }
        public System.ComponentModel.PropertyDescriptorCollection GetProperties() { //260508 hbk Phase 19 fix — PropertyGrid 가 호출하는 진짜 진입점
            return BuildFilteredProperties(null); //260508 hbk Phase 19 fix
        }
        private System.ComponentModel.PropertyDescriptorCollection BuildFilteredProperties(System.Attribute[] attrs) { //260508 hbk Phase 19 fix
            var alg = AlgorithmTypeEnum; //260508 hbk Phase 19 fix
            var sourceNames = new System.Collections.Generic.HashSet<string> { //260507 hbk Phase 18 CO-01 패턴 유지
                nameof(AlgorithmTypeList),
                nameof(Circle_EdgeDirectionList), nameof(Circle_EdgePolarityList),
                nameof(Circle_EdgeSelectionList), nameof(Circle_RadialDirectionList),
                nameof(Horizontal_A_EdgeDirectionList), nameof(Horizontal_A_EdgePolarityList),
                nameof(Horizontal_A_EdgeSelectionList),
                nameof(Horizontal_B_EdgeDirectionList), nameof(Horizontal_B_EdgePolarityList),
                nameof(Horizontal_B_EdgeSelectionList),
                nameof(Line1_EdgeDirectionList), nameof(Line1_EdgePolarityList), nameof(Line1_EdgeSelectionList),
                nameof(Line2_EdgeDirectionList), nameof(Line2_EdgePolarityList), nameof(Line2_EdgeSelectionList),
                nameof(Vertical_EdgeDirectionList), nameof(Vertical_EdgePolarityList), nameof(Vertical_EdgeSelectionList),
            };
            return DynamicPropertyHelper.FilterProperties(this, attrs, name => IsHiddenForAlgorithm(name, alg), sourceNames); //260508 hbk Phase 19 fix
        }
        public System.ComponentModel.AttributeCollection GetAttributes() { return System.ComponentModel.TypeDescriptor.GetAttributes(this, true); }
        public string GetClassName() { return System.ComponentModel.TypeDescriptor.GetClassName(this, true); }
        public string GetComponentName() { return System.ComponentModel.TypeDescriptor.GetComponentName(this, true); }
        public System.ComponentModel.TypeConverter GetConverter() { return System.ComponentModel.TypeDescriptor.GetConverter(this, true); }
        public System.ComponentModel.EventDescriptor GetDefaultEvent() { return System.ComponentModel.TypeDescriptor.GetDefaultEvent(this, true); }
        public System.ComponentModel.PropertyDescriptor GetDefaultProperty() { return System.ComponentModel.TypeDescriptor.GetDefaultProperty(this, true); }
        public object GetEditor(System.Type editorBaseType) { return System.ComponentModel.TypeDescriptor.GetEditor(this, editorBaseType, true); }
        public System.ComponentModel.EventDescriptorCollection GetEvents(System.Attribute[] attributes) { return System.ComponentModel.TypeDescriptor.GetEvents(this, attributes, true); }
        public System.ComponentModel.EventDescriptorCollection GetEvents() { return System.ComponentModel.TypeDescriptor.GetEvents(this, true); }
        public object GetPropertyOwner(System.ComponentModel.PropertyDescriptor pd) { return this; }

        //260503 hbk Phase 17 D-08/D-09 — AlgorithmType 별 노출 그룹 (UI-SPEC 표):
        //   TLI: Line1_*/Line2_* 노출 — Circle_*/CircleROI_*/CircleCenter_*/CircleDetected_*, Vertical_*, Horizontal_A_*/Horizontal_B_* 숨김
        //   CTH: Circle_* (RadialDirection 포함) + Horizontal_A_*/Horizontal_B_* 노출 — Line1_*/Line2_*, Vertical_*, Circle_EdgeDirection (D-03) 숨김
        //   VTH: Vertical_* + Horizontal_A_*/Horizontal_B_* 노출 — Line1_*/Line2_*, Circle_* 숨김
        private static bool IsHiddenForAlgorithm(string name, EDatumAlgorithm alg) {
            switch (alg) {
                case EDatumAlgorithm.TwoLineIntersect:
                    if (name.StartsWith("Circle_") || name.StartsWith("CircleROI_") || name.StartsWith("CircleCenter_") || name.StartsWith("CircleDetected_")) return true;
                    if (name.StartsWith("Vertical_")) return true;
                    if (name.StartsWith("Horizontal_A_") || name.StartsWith("Horizontal_B_")) return true;
                    return false;
                case EDatumAlgorithm.CircleTwoHorizontal:
                    if (name.StartsWith("Line1_") || name.StartsWith("Line1Detected_")) return true;
                    if (name.StartsWith("Line2_") || name.StartsWith("Line2Detected_")) return true;
                    if (name.StartsWith("Vertical_")) return true;
                    if (name == "Circle_EdgeDirection") return true; //260503 hbk Phase 17 D-03 — Circle 분기에서 EdgeDirection 동적 hide
                    return false;
                case EDatumAlgorithm.VerticalTwoHorizontal:
                    if (name.StartsWith("Line1_") || name.StartsWith("Line1Detected_")) return true;
                    if (name.StartsWith("Line2_") || name.StartsWith("Line2Detected_")) return true;
                    if (name.StartsWith("Circle_") || name.StartsWith("CircleROI_") || name.StartsWith("CircleCenter_") || name.StartsWith("CircleDetected_")) return true;
                    return false;
            }
            return false;
        }

        public DatumConfig(object owner) : base(owner) {
        }
    }
}
