//260413 hbk Phase 6: 점-점 거리 측정 (D-15)
using System.Collections.Generic; //260422 hbk Phase 7: List<T> (D-01)
using HalconDotNet;
using PropertyTools.DataAnnotations;
using ReringProject.Halcon.Algorithms;
using ReringProject.Halcon.Models; //260422 hbk Phase 7: EdgeInspectionOverlay (D-01)

namespace ReringProject.Sequence
{
    /// <summary>
    /// 두 Point ROI에서 각각 에지 라인을 피팅해 중점을 구하고,
    /// 두 점 사이의 유클리드 거리(mm)를 반환한다.
    /// </summary>
    public class PointToPointDistanceMeasurement : MeasurementBase //260413 hbk
    {
        public override string TypeName { get { return "PointToPointDistance"; } }

        [Category("Point1|ROI")]
        public double Point1_Row { get; set; }
        public double Point1_Col { get; set; }
        public double Point1_Phi { get; set; }
        public double Point1_Length1 { get; set; }
        public double Point1_Length2 { get; set; }

        [Category("Point2|ROI")]
        public double Point2_Row { get; set; }
        public double Point2_Col { get; set; }
        public double Point2_Phi { get; set; }
        public double Point2_Length1 { get; set; }
        public double Point2_Length2 { get; set; }

        [Category("Edge")]
        public int EdgeThreshold { get; set; } = 10;
        public double Sigma { get; set; } = 1.0;
        public int EdgeSampleCount { get; set; } = 20;
        public int EdgeTrimCount { get; set; } = 10;
        [ItemsSourceProperty(nameof(EdgePolarityList))] //260423 hbk WR-RT-02 ComboBox 처리
        public string EdgePolarity { get; set; } = "DarkToLight";
        [ItemsSourceProperty(nameof(EdgeDirectionList))] //260423 hbk WR-RT-02 ComboBox 처리
        public string EdgeDirection { get; set; } = "LtoR";

        //260423 hbk WR-RT-02 PropertyGrid ComboBox 옵션 래퍼
        [PropertyTools.DataAnnotations.Browsable(false)]
        public List<string> EdgeDirectionList { get { return EdgeOptionLists.Directions; } }
        [PropertyTools.DataAnnotations.Browsable(false)]
        public List<string> EdgePolarityList { get { return EdgeOptionLists.FAIPolarities; } }

        public PointToPointDistanceMeasurement(object owner) : base(owner) { } //260413 hbk

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

            double a1r, a1c, a2r, a2c;
            if (!svc.TryFitLine(image,
                Point1_Row, Point1_Col, Point1_Phi, Point1_Length1, Point1_Length2,
                datumTransform,
                EdgeSampleCount, EdgeTrimCount, Sigma, EdgeThreshold,
                EdgeDirection, EdgePolarity,
                out a1r, out a1c, out a2r, out a2c, out error))
            {
                return false;
            }
            double p1Row = (a1r + a2r) / 2.0;
            double p1Col = (a1c + a2c) / 2.0;

            double b1r, b1c, b2r, b2c;
            if (!svc.TryFitLine(image,
                Point2_Row, Point2_Col, Point2_Phi, Point2_Length1, Point2_Length2,
                datumTransform,
                EdgeSampleCount, EdgeTrimCount, Sigma, EdgeThreshold,
                EdgeDirection, EdgePolarity,
                out b1r, out b1c, out b2r, out b2c, out error))
            {
                return false;
            }
            double p2Row = (b1r + b2r) / 2.0;
            double p2Col = (b1c + b2c) / 2.0;

            double distPixel = VisionAlgorithmService.DistancePointToPoint(p1Row, p1Col, p2Row, p2Col);
            resultValue = distPixel * pixelResolution;
            return true;
        }
    }
}
