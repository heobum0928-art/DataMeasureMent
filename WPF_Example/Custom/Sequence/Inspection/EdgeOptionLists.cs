//260423 hbk WR-RT-02 PropertyGrid ComboBox 옵션 단일 소스 — EdgeDirection / EdgePolarity
using System.Collections.Generic;

namespace ReringProject.Sequence
{
    /// <summary>
    /// PropertyTools PropertyGrid의 <c>[ItemsSourceProperty]</c>가 참조하는 공용 옵션 리스트.
    /// 문자열 값은 기존 INI 레시피 / Halcon 파라미터와 일치시켜 하위호환을 유지한다.
    /// </summary>
    public static class EdgeOptionLists
    {
        // MeasurementAlgorithm.cs 및 FAIEdgeMeasurementService.cs 가 참조하는 방향 코드
        public static readonly List<string> Directions = new List<string> { "LtoR", "RtoL", "TtoB", "BtoT" };

        // FAIConfig / Measurement 레이어 — Halcon "positive"/"negative"로 변환됨
        public static readonly List<string> FAIPolarities = new List<string> { "DarkToLight", "LightToDark" };

        // DatumConfig — Halcon MeasurePos transition 파라미터 원시 값
        public static readonly List<string> DatumPolarities = new List<string> { "all", "positive", "negative" };

        // MeasurePos 'first'/'last'/'all' 의 Datum UI 표기. //260429 hbk Phase 15 — DatumConfig.*_EdgeSelection ItemsSource 단일 소스
        public static readonly List<string> Selections = new List<string> { "First", "Last", "All" }; //260429 hbk Phase 15

        //260503 hbk Phase 17 D-02 — Datum Circle ROI 안→밖 / 밖→안 그라디언트 polarity (CTH only).
        //  DatumConfig.Circle_RadialDirection ItemsSource 단일 소스. Caller (DatumFindingService.TryTeachCircleTwoHorizontal)
        //  가 "Inward" → "positive", "Outward" → "negative" 로 매핑하여 TryFindCircleByPolarSampling 의 polarity 인자에 override 전달.
        public static readonly List<string> RadialDirections = new List<string> { "Inward", "Outward" }; //260503 hbk Phase 17 D-02

        //260508 hbk Phase 28 D-04 — FAI CircleDiameter polar 경로 default 상수 (Datum CTH default 와 동일 → REQ-28-03 동등성 결정적 보장)
        public const double FaiCirclePolarStepDeg   = 10.0;     //260508 hbk Phase 28
        public const double FaiCircleRectL1Ratio    = 0.02;     //260508 hbk Phase 28
        public const double FaiCircleRectL2Ratio    = 0.02;     //260508 hbk Phase 28
        public const string FaiCircleEdgeSelection  = "First";  //260508 hbk Phase 28

        //260508 hbk Phase 28 D-02/D-03 — RadialDirection ("Inward"/"Outward") → Halcon polarity ("positive"/"negative") 단일 매핑.
        //  Datum CTH (DatumFindingService.cs:200, :730) 의 inline `string.Equals(..., "Outward", OrdinalIgnoreCase) ? "negative" : "positive"` 와
        //  byte-identical 결과를 보장한다 (null/empty/Inward → "positive", Outward(대소문자무관) → "negative").
        public static string MapRadialDirectionToHalconPolarity(string radial) //260508 hbk Phase 28
        {
            if (string.Equals(radial, "Outward", System.StringComparison.OrdinalIgnoreCase)) //260508 hbk Phase 28
            {
                return "negative"; //260508 hbk Phase 28
            }
            return "positive"; //260508 hbk Phase 28
        }
    }
}
