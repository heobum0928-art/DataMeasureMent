//260413 hbk Phase 6: 선-선 각도 측정 (D-15)
using System.Collections.Generic; //260422 hbk Phase 7: List<T> (D-01)
using HalconDotNet;
using PropertyTools.DataAnnotations;
using ReringProject.Halcon.Algorithms;
using ReringProject.Halcon.Models; //260422 hbk Phase 7: EdgeInspectionOverlay (D-01)

namespace ReringProject.Sequence
{
    /// <summary>
    /// 두 Line ROI에서 직선을 피팅하고 두 직선이 이루는 각도(degree)를 반환한다.
    /// 단위는 degree이므로 pixelResolution 미적용.
    /// </summary>
    public class LineToLineAngleMeasurement : MeasurementBase //260413 hbk
    {
        public override string TypeName { get { return "LineToLineAngle"; } }

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

        public LineToLineAngleMeasurement(object owner) : base(owner) { } //260413 hbk

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

            resultValue = VisionAlgorithmService.AngleLineLine(
                l1r1, l1c1, l1r2, l1c2,
                l2r1, l2c1, l2r2, l2c2);
            return true;
        }
    }
}
