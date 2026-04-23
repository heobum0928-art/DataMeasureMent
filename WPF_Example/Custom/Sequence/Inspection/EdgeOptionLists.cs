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
    }
}
