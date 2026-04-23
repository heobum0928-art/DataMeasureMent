//260413 hbk Phase 6: 에지 페어 거리 측정 — FAIEdgeMeasurementService 래핑 (D-15, D-19)
using System.Collections.Generic; //260422 hbk Phase 7: List<T> (D-01)
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

        //260417 hbk ROI 필드 제거 — Owner(FAIConfig).ROI_*를 단일 소스로 사용
        // 기존 EdgePair 자체 ROI_Row/Col/Phi/Length1/Length2 필드는 FAIConfig와 중복되어
        // 동기화 누락 시 표시 ROI와 측정 ROI가 달라지는 버그의 원인이었음. INI에 해당 키가
        // 남아있어도 ParamBase.Load 리플렉션이 미존재 프로퍼티를 무시하므로 하위호환 OK.

        [Category("EdgePair|Edge")]
        public int EdgeThreshold { get; set; } = 10;
        public double Sigma { get; set; } = 1.0;
        [ItemsSourceProperty(nameof(EdgeDirectionList))] //260423 hbk WR-RT-02 ComboBox 처리
        public string EdgeDirection { get; set; } = "LtoR";
        public string EdgeSelection { get; set; } = "Both";
        public int EdgeSampleCount { get; set; } = 20;
        public int EdgeTrimCount { get; set; } = 10;
        [ItemsSourceProperty(nameof(EdgePolarityList))] //260423 hbk WR-RT-02 ComboBox 처리
        public string EdgePolarity { get; set; } = "DarkToLight";

        //260423 hbk WR-RT-02 PropertyGrid ComboBox 옵션 래퍼
        [PropertyTools.DataAnnotations.Browsable(false)]
        public List<string> EdgeDirectionList { get { return EdgeOptionLists.Directions; } }
        [PropertyTools.DataAnnotations.Browsable(false)]
        public List<string> EdgePolarityList { get { return EdgeOptionLists.FAIPolarities; } }

        [Category("EdgePair|Calibration")]
        public double PixelResolutionX { get; set; } = 1.0;
        public double PixelResolutionY { get; set; } = 1.0;

        public EdgePairDistanceMeasurement(object owner) : base(owner) { } //260413 hbk

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
            overlays = new List<EdgeInspectionOverlay>(); //260422 hbk Phase 7: 실패 경로에서도 non-null (D-02)

            if (image == null)
            {
                error = "image is null";
                return false;
            }

            //260417 hbk ROI 단일 소스: Owner(FAIConfig)에서 직접 참조 — 중복 저장 제거
            var ownerFai = Owner as FAIConfig;
            if (ownerFai == null)
            {
                error = "Owner is not FAIConfig";
                return false;
            }

            // 래퍼용 임시 FAIConfig 구성 (D-19: FAIEdgeMeasurementService 재사용)
            // ROI는 Owner에서, Edge/Calibration 파라미터는 self에서
            var temp = new FAIConfig(Owner)
            {
                ROI_Row = ownerFai.ROI_Row,       //260417 hbk
                ROI_Col = ownerFai.ROI_Col,       //260417 hbk
                ROI_Phi = ownerFai.ROI_Phi,       //260417 hbk
                ROI_Length1 = ownerFai.ROI_Length1, //260417 hbk
                ROI_Length2 = ownerFai.ROI_Length2, //260417 hbk
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
            if (result.Overlays != null) overlays = result.Overlays; //260422 hbk Phase 7: service 산출 overlay 전달 (D-09, D-10)
            return true;
        }
    }
}
