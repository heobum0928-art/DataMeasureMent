//260413 hbk Phase 6: 원 직경 측정 (D-15)
using HalconDotNet;
using PropertyTools.DataAnnotations;
using ReringProject.Halcon.Algorithms;

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
        public string EdgePolarity { get; set; } = "DarkToLight";

        public CircleDiameterMeasurement(object owner) : base(owner) { } //260413 hbk

        public override bool TryExecute( //260413 hbk
            HImage image,
            HTuple datumTransform,
            double pixelResolution,
            out double resultValue,
            out string error)
        {
            resultValue = 0;
            error = null;

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
