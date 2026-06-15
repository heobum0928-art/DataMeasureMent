using System.Collections.Generic;
using HalconDotNet;
using PropertyTools.DataAnnotations;
using ReringProject.Halcon.Algorithms;
using ReringProject.Halcon.Models;

namespace ReringProject.Sequence
{
    /// <summary>
    /// 기존 FAIEdgeMeasurementService(샘플 스트립 + 라인 피팅)를 래핑한다.
    /// 에지 측정 로직을 재사용하며 결과는 FAIEdgeMeasurementResult.DistanceMm.
    /// </summary>
    public class EdgePairDistanceMeasurement : MeasurementBase
    {
        public override string TypeName { get { return "EdgePairDistance"; } }

        // ROI 필드 제거 — Owner(FAIConfig).ROI_*를 단일 소스로 사용.
        // 자체 ROI_Row/Col/Phi/Length1/Length2 필드는 FAIConfig와 중복되어
        // 동기화 누락 시 표시 ROI와 측정 ROI가 달라지는 버그의 원인이었음. INI에 해당 키가
        // 남아있어도 ParamBase.Load 리플렉션이 미존재 프로퍼티를 무시하므로 하위호환 OK.

        [Category("EdgePair|Edge")]
        public int EdgeThreshold { get; set; } = 10;
        public double Sigma { get; set; } = 1.0;
        [ItemsSourceProperty(nameof(EdgeDirectionList))]
        public string EdgeDirection { get; set; } = "LtoR";
        public string EdgeSelection { get; set; } = "Both";
        public int EdgeSampleCount { get; set; } = 20;
        public int EdgeTrimCount { get; set; } = 10;
        [ItemsSourceProperty(nameof(EdgePolarityList))]
        public string EdgePolarity { get; set; } = "DarkToLight";

        [PropertyTools.DataAnnotations.Browsable(false)]
        public List<string> EdgeDirectionList { get { return EdgeOptionLists.Directions; } }
        [PropertyTools.DataAnnotations.Browsable(false)]
        public List<string> EdgePolarityList { get { return EdgeOptionLists.FAIPolarities; } }

        // INI 호환 잔존 저장용 — D-06 재배선 후 TryExecute 에서 소비 안 함. //260615 hbk Phase 42 D-05
        [PropertyTools.DataAnnotations.Browsable(false)]
        public double PixelResolutionX { get; set; } = 1.0;
        [PropertyTools.DataAnnotations.Browsable(false)]
        public double PixelResolutionY { get; set; } = 1.0;

        public EdgePairDistanceMeasurement(object owner) : base(owner) { }

        public override bool TryExecute(
            HImage image,
            HTuple datumTransform,
            double pixelResolution,
            out double resultValue,
            out string error,
            out List<EdgeInspectionOverlay> overlays)
        {
            resultValue = 0;
            error = null;
            overlays = new List<EdgeInspectionOverlay>();

            if (image == null)
            {
                error = "image is null";
                return false;
            }

            // ROI 단일 소스: Owner(FAIConfig)에서 직접 참조 — 중복 저장 제거
            var ownerFai = Owner as FAIConfig;
            if (ownerFai == null)
            {
                error = "Owner is not FAIConfig";
                return false;
            }

            //260615 hbk Phase 42 D-06 — PixelResolution 은 shot 단일소스(파라미터 경유). self 필드 소비 제거
            var ownerShot = ownerFai.Owner as ShotConfig;
            double resolvedPixelRes = (ownerShot != null) ? ownerShot.PixelResolution : pixelResolution;

            // 래퍼용 임시 FAIConfig 구성 — FAIEdgeMeasurementService 재사용.
            // ROI는 Owner에서, Edge/Calibration 파라미터는 self에서 가져온다.
            var temp = new FAIConfig(Owner)
            {
                ROI_Row = ownerFai.ROI_Row,
                ROI_Col = ownerFai.ROI_Col,
                ROI_Phi = ownerFai.ROI_Phi,
                ROI_Length1 = ownerFai.ROI_Length1,
                ROI_Length2 = ownerFai.ROI_Length2,
                EdgeThreshold = EdgeThreshold,
                Sigma = Sigma,
                EdgeDirection = EdgeDirection,
                EdgeSelection = EdgeSelection,
                EdgeSampleCount = EdgeSampleCount,
                EdgeTrimCount = EdgeTrimCount,
                EdgePolarity = EdgePolarity,
                PixelResolutionX = resolvedPixelRes, //260615 hbk Phase 42 D-06
                PixelResolutionY = resolvedPixelRes, //260615 hbk Phase 42 D-06
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
            if (result.Overlays != null) overlays = result.Overlays;
            return true;
        }
    }
}
