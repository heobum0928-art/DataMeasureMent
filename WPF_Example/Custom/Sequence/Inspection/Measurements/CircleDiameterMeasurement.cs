//260413 hbk Phase 6: 원 직경 측정 (D-15)
using System.Collections.Generic; //260422 hbk Phase 7: List<T> (D-01)
using HalconDotNet;
using PropertyTools.DataAnnotations;
using ReringProject.Halcon.Algorithms;
using ReringProject.Halcon.Models; //260422 hbk Phase 7: EdgeInspectionOverlay (D-01)

namespace ReringProject.Sequence
{
    /// <summary>
    /// 원형 탐색 영역에서 에지를 검출하고 FitCircleContourXld로 원을 피팅한 뒤
    /// 직경(mm) = radius * 2 * pixelResolution을 반환한다.
    /// </summary>
    public class CircleDiameterMeasurement : MeasurementBase //260413 hbk
    {
        public override string TypeName { get { return "CircleDiameter"; } }

        [Category("Circle|ROI")]
        public double Circle_Row { get; set; }
        public double Circle_Col { get; set; }
        public double Circle_Radius { get; set; }

        [Category("Edge")]
        public int EdgeThreshold { get; set; } = 10;
        public double Sigma { get; set; } = 1.0;
        [ItemsSourceProperty(nameof(EdgePolarityList))] //260423 hbk WR-RT-02 ComboBox 처리
        public string EdgePolarity { get; set; } = "DarkToLight";

        [ItemsSourceProperty(nameof(Circle_RadialDirectionList))] //260508 hbk Phase 28 REQ-28-01 (D-06/D-07 — Edge category 2옵션 콤보)
        public string Circle_RadialDirection { get; set; } = ""; //260508 hbk Phase 28 REQ-28-01/REQ-28-04 (default "" → fit 경로 → INI 하위호환)

        //260423 hbk WR-RT-02 PropertyGrid ComboBox 옵션 래퍼
        [PropertyTools.DataAnnotations.Browsable(false)]
        public List<string> EdgePolarityList { get { return EdgeOptionLists.FAIPolarities; } }

        //260508 hbk Phase 28 REQ-28-01 — RadialDirection 콤보 옵션 래퍼 (Phase 17 D-02 단일 소스 EdgeOptionLists.RadialDirections 직접 참조)
        [PropertyTools.DataAnnotations.Browsable(false)] //260508 hbk Phase 28
        public List<string> Circle_RadialDirectionList { get { return EdgeOptionLists.RadialDirections; } } //260508 hbk Phase 28

        public CircleDiameterMeasurement(object owner) : base(owner) { } //260413 hbk

        public override bool TryExecute( //260413 hbk //260422 hbk Phase 7: out overlays 추가 (D-01)
            HImage image,
            HTuple datumTransform,
            double pixelResolution,
            out double resultValue,
            out string error,
            out List<EdgeInspectionOverlay> overlays)
        {
            resultValue = 0;
            error = null;
            overlays = new List<EdgeInspectionOverlay>(); //260422 hbk Phase 7: 5종 overlay 미구현 — 빈 리스트 반환 (D-03)

            var svc = new VisionAlgorithmService();
            double foundRow, foundCol, foundRadius;
            if (!svc.TryFindCircle(image,
                Circle_Row, Circle_Col, Circle_Radius,
                datumTransform,
                Sigma, EdgeThreshold, EdgePolarity,
                out foundRow, out foundCol, out foundRadius, out error))
            {
                return false;
            }

            resultValue = foundRadius * 2.0 * pixelResolution;
            return true;
        }
    }
}
