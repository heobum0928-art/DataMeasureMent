//260413 hbk Phase 6: 점-선 거리 측정 (D-15)
using HalconDotNet;
using PropertyTools.DataAnnotations;
using ReringProject.Halcon.Algorithms;

namespace ReringProject.Sequence
{
    /// <summary>
    /// Point ROI에서 에지 라인을 피팅하여 중점을 점으로 사용하고,
    /// Line ROI에서 에지 라인을 피팅한 뒤 두 대상 간 수직 거리를 산출한다.
    /// 결과 단위: mm (pixelResolution 적용).
    /// </summary>
    public class PointToLineDistanceMeasurement : MeasurementBase //260413 hbk
    {
        public override string TypeName { get { return "PointToLineDistance"; } }

        [Category("Point|ROI")]
        public double Point_Row { get; set; }
        public double Point_Col { get; set; }
        public double Point_Phi { get; set; }
        public double Point_Length1 { get; set; }
        public double Point_Length2 { get; set; }

        [Category("Line|ROI")]
        public double Line_Row { get; set; }
        public double Line_Col { get; set; }
        public double Line_Phi { get; set; }
        public double Line_Length1 { get; set; }
        public double Line_Length2 { get; set; }

        [Category("Edge")]
        public int EdgeThreshold { get; set; } = 10;
        public double Sigma { get; set; } = 1.0;
        public int EdgeSampleCount { get; set; } = 20;
        public int EdgeTrimCount { get; set; } = 10;
        public string EdgePolarity { get; set; } = "DarkToLight";
        public string EdgeDirection { get; set; } = "LtoR";

        public PointToLineDistanceMeasurement(object owner) : base(owner) { } //260413 hbk

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

            double pr1, pc1, pr2, pc2;
            if (!svc.TryFitLine(image,
                Point_Row, Point_Col, Point_Phi, Point_Length1, Point_Length2,
                datumTransform,
                EdgeSampleCount, EdgeTrimCount, Sigma, EdgeThreshold,
                EdgeDirection, EdgePolarity,
                out pr1, out pc1, out pr2, out pc2, out error))
            {
                return false;
            }
            double pRow = (pr1 + pr2) / 2.0;
            double pCol = (pc1 + pc2) / 2.0;

            double lr1, lc1, lr2, lc2;
            if (!svc.TryFitLine(image,
                Line_Row, Line_Col, Line_Phi, Line_Length1, Line_Length2,
                datumTransform,
                EdgeSampleCount, EdgeTrimCount, Sigma, EdgeThreshold,
                EdgeDirection, EdgePolarity,
                out lr1, out lc1, out lr2, out lc2, out error))
            {
                return false;
            }

            double distPixel = VisionAlgorithmService.DistancePointToLine(pRow, pCol, lr1, lc1, lr2, lc2);
            resultValue = distPixel * pixelResolution;
            return true;
        }
    }
}
