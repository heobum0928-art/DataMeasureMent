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
        [Category("Datum|Line1 ROI")]
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
        [Category("Datum|Line1 Edge")]
        public int    Line1_EdgeThreshold   { get; set; } = 0;
        public double Line1_Sigma           { get; set; } = 0;
        [ItemsSourceProperty(nameof(Line1_EdgeDirectionList))]
        public string Line1_EdgeDirection   { get; set; } = "";
        public int    Line1_EdgeSampleCount { get; set; } = 0;
        public int    Line1_EdgeTrimCount   { get; set; } = 0;
        [ItemsSourceProperty(nameof(Line1_EdgePolarityList))]
        public string Line1_EdgePolarity    { get; set; } = "";

        [PropertyTools.DataAnnotations.Browsable(false)]
        public List<string> Line1_EdgeDirectionList { get { return EdgeOptionLists.Directions; } }
        [PropertyTools.DataAnnotations.Browsable(false)]
        public List<string> Line1_EdgePolarityList  { get { return EdgeOptionLists.DatumPolarities; } }

        //260409 hbk Phase 4: Line2 ROI (기준 Y축 방향 에지 라인, 기본값 PI/2 = 수직)
        [Category("Datum|Line2 ROI")]
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
        [Category("Datum|Line2 Edge")]
        public int    Line2_EdgeThreshold   { get; set; } = 0;
        public double Line2_Sigma           { get; set; } = 0;
        [ItemsSourceProperty(nameof(Line2_EdgeDirectionList))]
        public string Line2_EdgeDirection   { get; set; } = "";
        public int    Line2_EdgeSampleCount { get; set; } = 0;
        public int    Line2_EdgeTrimCount   { get; set; } = 0;
        [ItemsSourceProperty(nameof(Line2_EdgePolarityList))]
        public string Line2_EdgePolarity    { get; set; } = "";

        [PropertyTools.DataAnnotations.Browsable(false)]
        public List<string> Line2_EdgeDirectionList { get { return EdgeOptionLists.Directions; } }
        [PropertyTools.DataAnnotations.Browsable(false)]
        public List<string> Line2_EdgePolarityList  { get { return EdgeOptionLists.DatumPolarities; } }

        //260423 hbk Phase 12 D-10 — Circle ROI (CircleTwoHorizontal 전용 검색 영역)
        //260423 hbk  CircleROI_Radius > 0 이 ROI 설정 완료 판정 기준.
        [Category("Datum|Circle ROI")]
        public double CircleROI_Row    { get; set; } = 0; //260423 hbk Phase 12 D-10
        public double CircleROI_Col    { get; set; } = 0; //260423 hbk Phase 12 D-10
        public double CircleROI_Radius { get; set; } = 0; //260423 hbk Phase 12 D-10

        //260425 hbk Phase 13 D-PRP-02 — Circle ROI 전용 에지 파라미터 (per-ROI 독립 튜닝)
        [Category("Datum|Circle Edge")]
        public int    Circle_EdgeThreshold   { get; set; } = 0;
        public double Circle_Sigma           { get; set; } = 0;
        [ItemsSourceProperty(nameof(Circle_EdgeDirectionList))]
        public string Circle_EdgeDirection   { get; set; } = "";
        public int    Circle_EdgeSampleCount { get; set; } = 0;
        public int    Circle_EdgeTrimCount   { get; set; } = 0;
        [ItemsSourceProperty(nameof(Circle_EdgePolarityList))]
        public string Circle_EdgePolarity    { get; set; } = "";

        [PropertyTools.DataAnnotations.Browsable(false)]
        public List<string> Circle_EdgeDirectionList { get { return EdgeOptionLists.Directions; } }
        [PropertyTools.DataAnnotations.Browsable(false)]
        public List<string> Circle_EdgePolarityList  { get { return EdgeOptionLists.DatumPolarities; } }

        //260423 hbk Phase 12 D-11 — 수평 A ROI (CircleTwoHorizontal + VerticalTwoHorizontal 공용)
        //260423 hbk  A/B 순서 의존성 없음 — concat + FitLineContourXld 이므로 교환 대칭.
        //260423 hbk  Length1 > 0 && Length2 > 0 이 ROI 설정 완료 판정 기준.
        [Category("Datum|Horizontal A ROI")]
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
        [Category("Datum|Horizontal A Edge")]
        public int    Horizontal_A_EdgeThreshold   { get; set; } = 0;
        public double Horizontal_A_Sigma           { get; set; } = 0;
        [ItemsSourceProperty(nameof(Horizontal_A_EdgeDirectionList))]
        public string Horizontal_A_EdgeDirection   { get; set; } = "";
        public int    Horizontal_A_EdgeSampleCount { get; set; } = 0;
        public int    Horizontal_A_EdgeTrimCount   { get; set; } = 0;
        [ItemsSourceProperty(nameof(Horizontal_A_EdgePolarityList))]
        public string Horizontal_A_EdgePolarity    { get; set; } = "";

        [PropertyTools.DataAnnotations.Browsable(false)]
        public List<string> Horizontal_A_EdgeDirectionList { get { return EdgeOptionLists.Directions; } }
        [PropertyTools.DataAnnotations.Browsable(false)]
        public List<string> Horizontal_A_EdgePolarityList  { get { return EdgeOptionLists.DatumPolarities; } }

        //260423 hbk Phase 12 D-11 — 수평 B ROI
        [Category("Datum|Horizontal B ROI")]
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
        [Category("Datum|Horizontal B Edge")]
        public int    Horizontal_B_EdgeThreshold   { get; set; } = 0;
        public double Horizontal_B_Sigma           { get; set; } = 0;
        [ItemsSourceProperty(nameof(Horizontal_B_EdgeDirectionList))]
        public string Horizontal_B_EdgeDirection   { get; set; } = "";
        public int    Horizontal_B_EdgeSampleCount { get; set; } = 0;
        public int    Horizontal_B_EdgeTrimCount   { get; set; } = 0;
        [ItemsSourceProperty(nameof(Horizontal_B_EdgePolarityList))]
        public string Horizontal_B_EdgePolarity    { get; set; } = "";

        [PropertyTools.DataAnnotations.Browsable(false)]
        public List<string> Horizontal_B_EdgeDirectionList { get { return EdgeOptionLists.Directions; } }
        [PropertyTools.DataAnnotations.Browsable(false)]
        public List<string> Horizontal_B_EdgePolarityList  { get { return EdgeOptionLists.DatumPolarities; } }

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

        //260425 hbk Phase 13 D-PRP-03 — 최초 로드 시 sentinel(per-ROI EdgeThreshold==0) 검출 → 글로벌 값 5 ROI 일괄 복제 (idempotent).
        //  DatumFindingService.TryTeach* / TryFindDatum 진입부에서 1회 호출.
        //  sentinel 가 아니면 (사용자가 per-ROI 값을 명시한 경우) 그대로 유지 — 멱등성 보장.
        public void EnsurePerRoiDefaults() {
            // Hardcoded fallback (legacy 글로벌이 모두 0/"" 인 극단 케이스)
            int    fbThreshold   = EdgeThreshold > 0 ? EdgeThreshold : 20;
            double fbSigma       = Sigma > 0 ? Sigma : 1.0;
            string fbDirection   = "LtoR"; // legacy 글로벌에 EdgeDirection 없음
            int    fbSampleCount = 20;
            int    fbTrimCount   = 10;
            string fbPolarity    = !string.IsNullOrEmpty(EdgePolarity) ? EdgePolarity : "all";

            // Line1
            if (Line1_EdgeThreshold == 0)        Line1_EdgeThreshold = fbThreshold;
            if (Line1_Sigma == 0)                Line1_Sigma = fbSigma;
            if (string.IsNullOrEmpty(Line1_EdgeDirection)) Line1_EdgeDirection = fbDirection;
            if (Line1_EdgeSampleCount == 0)      Line1_EdgeSampleCount = fbSampleCount;
            if (Line1_EdgeTrimCount == 0)        Line1_EdgeTrimCount = fbTrimCount;
            if (string.IsNullOrEmpty(Line1_EdgePolarity)) Line1_EdgePolarity = fbPolarity;

            // Line2
            if (Line2_EdgeThreshold == 0)        Line2_EdgeThreshold = fbThreshold;
            if (Line2_Sigma == 0)                Line2_Sigma = fbSigma;
            if (string.IsNullOrEmpty(Line2_EdgeDirection)) Line2_EdgeDirection = fbDirection;
            if (Line2_EdgeSampleCount == 0)      Line2_EdgeSampleCount = fbSampleCount;
            if (Line2_EdgeTrimCount == 0)        Line2_EdgeTrimCount = fbTrimCount;
            if (string.IsNullOrEmpty(Line2_EdgePolarity)) Line2_EdgePolarity = fbPolarity;

            // Circle
            if (Circle_EdgeThreshold == 0)       Circle_EdgeThreshold = fbThreshold;
            if (Circle_Sigma == 0)               Circle_Sigma = fbSigma;
            if (string.IsNullOrEmpty(Circle_EdgeDirection)) Circle_EdgeDirection = fbDirection;
            if (Circle_EdgeSampleCount == 0)     Circle_EdgeSampleCount = fbSampleCount;
            if (Circle_EdgeTrimCount == 0)       Circle_EdgeTrimCount = fbTrimCount;
            if (string.IsNullOrEmpty(Circle_EdgePolarity)) Circle_EdgePolarity = fbPolarity;

            // Horizontal_A
            if (Horizontal_A_EdgeThreshold == 0)   Horizontal_A_EdgeThreshold = fbThreshold;
            if (Horizontal_A_Sigma == 0)           Horizontal_A_Sigma = fbSigma;
            if (string.IsNullOrEmpty(Horizontal_A_EdgeDirection)) Horizontal_A_EdgeDirection = fbDirection;
            if (Horizontal_A_EdgeSampleCount == 0) Horizontal_A_EdgeSampleCount = fbSampleCount;
            if (Horizontal_A_EdgeTrimCount == 0)   Horizontal_A_EdgeTrimCount = fbTrimCount;
            if (string.IsNullOrEmpty(Horizontal_A_EdgePolarity)) Horizontal_A_EdgePolarity = fbPolarity;

            // Horizontal_B
            if (Horizontal_B_EdgeThreshold == 0)   Horizontal_B_EdgeThreshold = fbThreshold;
            if (Horizontal_B_Sigma == 0)           Horizontal_B_Sigma = fbSigma;
            if (string.IsNullOrEmpty(Horizontal_B_EdgeDirection)) Horizontal_B_EdgeDirection = fbDirection;
            if (Horizontal_B_EdgeSampleCount == 0) Horizontal_B_EdgeSampleCount = fbSampleCount;
            if (Horizontal_B_EdgeTrimCount == 0)   Horizontal_B_EdgeTrimCount = fbTrimCount;
            if (string.IsNullOrEmpty(Horizontal_B_EdgePolarity)) Horizontal_B_EdgePolarity = fbPolarity;
        }

        public DatumConfig(object owner) : base(owner) {
        }
    }
}
