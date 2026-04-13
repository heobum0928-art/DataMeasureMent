//260413 hbk Phase 6: 에지 페어 거리 측정 — FAIEdgeMeasurementService 래핑 (D-15, D-19)
using HalconDotNet;
using PropertyTools.DataAnnotations;
using ReringProject.Halcon.Algorithms;
using ReringProject.Halcon.Models;

namespace ReringProject.Sequence
{
    /// <summary>
    /// 기존 FAIEdgeMeasurementService(샘플 스트립 + 라인 피팅)를 래핑한다.
    /// Phase 3 에지 측정 로직을 재사용하며 결과는 FAIEdgeMeasurementResult.DistanceMm.
    /// </summary>
    public class EdgePairDistanceMeasurement : MeasurementBase //260413 hbk
    {
        public override string TypeName { get { return "EdgePairDistance"; } }

        [Category("EdgePair|ROI")]
        public double ROI_Row { get; set; }
        public double ROI_Col { get; set; }
        public double ROI_Phi { get; set; }
        public double ROI_Length1 { get; set; }
        public double ROI_Length2 { get; set; }

        [Category("EdgePair|Edge")]
        public int EdgeThreshold { get; set; } = 10;
        public double Sigma { get; set; } = 1.0;
        public string EdgeDirection { get; set; } = "LtoR";
        public string EdgeSelection { get; set; } = "Both";
        public int EdgeSampleCount { get; set; } = 20;
        public int EdgeTrimCount { get; set; } = 10;
        public string EdgePolarity { get; set; } = "DarkToLight";

        [Category("EdgePair|Calibration")]
        public double PixelResolutionX { get; set; } = 1.0;
        public double PixelResolutionY { get; set; } = 1.0;

        public EdgePairDistanceMeasurement(object owner) : base(owner) { } //260413 hbk

        public override bool TryExecute( //260413 hbk
            HImage image,
            HTuple datumTransform,
            double pixelResolution,
            out double resultValue,
            out string error)
        {
            resultValue = 0;
            error = null;

            if (image == null)
            {
                error = "image is null";
                return false;
            }

            // 래퍼용 임시 FAIConfig 구성 (D-19: FAIEdgeMeasurementService 재사용)
            var temp = new FAIConfig(Owner)
            {
                ROI_Row = ROI_Row,
                ROI_Col = ROI_Col,
                ROI_Phi = ROI_Phi,
                ROI_Length1 = ROI_Length1,
                ROI_Length2 = ROI_Length2,
                EdgeThreshold = EdgeThreshold,
                Sigma = Sigma,
                EdgeDirection = EdgeDirection,
                EdgeSelection = EdgeSelection,
                EdgeSampleCount = EdgeSampleCount,
                EdgeTrimCount = EdgeTrimCount,
                EdgePolarity = EdgePolarity,
                PixelResolutionX = PixelResolutionX,
                PixelResolutionY = PixelResolutionY,
                FAIName = MeasurementName
            };

            var service = new FAIEdgeMeasurementService();
            FAIEdgeMeasurementResult result;
            if (!service.TryMeasure(image, temp, datumTransform, out result))
            {
                error = "FAIEdgeMeasurementService.TryMeasure failed";
                return false;
            }

            resultValue = result.DistanceMm;
            return true;
        }
    }
}
