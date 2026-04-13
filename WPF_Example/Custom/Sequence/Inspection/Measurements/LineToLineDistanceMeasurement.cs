//260413 hbk Phase 6: 선-선 거리 측정 (D-15)
using HalconDotNet;
using PropertyTools.DataAnnotations;
using ReringProject.Halcon.Algorithms;

namespace ReringProject.Sequence
{
    /// <summary>
    /// 두 Line ROI에서 직선을 피팅하고 Line1 중점에서 Line2까지의 수직 거리(mm)를 반환한다.
    /// </summary>
    public class LineToLineDistanceMeasurement : MeasurementBase //260413 hbk
    {
        public override string TypeName { get { return "LineToLineDistance"; } }

        [Category("Line1|ROI")]
        public double Line1_Row { get; set; }
        public double Line1_Col { get; set; }
        public double Line1_Phi { get; set; }
        public double Line1_Length1 { get; set; }
        public double Line1_Length2 { get; set; }

        [Category("Line2|ROI")]
        public double Line2_Row { get; set; }
        public double Line2_Col { get; set; }
        public double Line2_Phi { get; set; }
        public double Line2_Length1 { get; set; }
        public double Line2_Length2 { get; set; }

        [Category("Edge")]
        public int EdgeThreshold { get; set; } = 10;
        public double Sigma { get; set; } = 1.0;
        public int EdgeSampleCount { get; set; } = 20;
        public int EdgeTrimCount { get; set; } = 10;
        public string EdgePolarity { get; set; } = "DarkToLight";
        public string EdgeDirection { get; set; } = "LtoR";

        public LineToLineDistanceMeasurement(object owner) : base(owner) { } //260413 hbk

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

            double l1r1, l1c1, l1r2, l1c2;
            if (!svc.TryFitLine(image,
                Line1_Row, Line1_Col, Line1_Phi, Line1_Length1, Line1_Length2,
                datumTransform,
                EdgeSampleCount, EdgeTrimCount, Sigma, EdgeThreshold,
                EdgeDirection, EdgePolarity,
                out l1r1, out l1c1, out l1r2, out l1c2, out error))
            {
                return false;
            }

            double l2r1, l2c1, l2r2, l2c2;
            if (!svc.TryFitLine(image,
                Line2_Row, Line2_Col, Line2_Phi, Line2_Length1, Line2_Length2,
                datumTransform,
                EdgeSampleCount, EdgeTrimCount, Sigma, EdgeThreshold,
                EdgeDirection, EdgePolarity,
                out l2r1, out l2c1, out l2r2, out l2c2, out error))
            {
                return false;
            }

            double midRow = (l1r1 + l1r2) / 2.0;
            double midCol = (l1c1 + l1c2) / 2.0;
            double distPixel = VisionAlgorithmService.DistancePointToLine(midRow, midCol, l2r1, l2c1, l2r2, l2c2);
            resultValue = distPixel * pixelResolution;
            return true;
        }
    }
}
