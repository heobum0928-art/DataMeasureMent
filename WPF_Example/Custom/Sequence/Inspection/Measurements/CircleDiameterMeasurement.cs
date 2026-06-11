using System.Collections.Generic;
using HalconDotNet;
using PropertyTools.DataAnnotations;
using ReringProject.Halcon.Algorithms;
using ReringProject.Halcon.Models;

namespace ReringProject.Sequence
{
    /// <summary>
    /// 원형 탐색 영역에서 에지를 검출하고 FitCircleContourXld로 원을 피팅한 뒤
    /// 직경(mm) = radius * 2 * pixelResolution을 반환한다.
    /// </summary>
    // ICustomTypeDescriptor: Circle_RadialDirection 빈값 시 polar 4 필드 PropertyGrid hide.
    public class CircleDiameterMeasurement : MeasurementBase, System.ComponentModel.ICustomTypeDescriptor
    {
        public override string TypeName { get { return "CircleDiameter"; } }

        [Category("Circle|ROI")]
        public double Circle_Row { get; set; }
        public double Circle_Col { get; set; }
        public double Circle_Radius { get; set; }

        [Category("Edge")]
        public int EdgeThreshold { get; set; } = 10;
        public double Sigma { get; set; } = 1.0;
        [ItemsSourceProperty(nameof(EdgePolarityList))]
        public string EdgePolarity { get; set; } = "DarkToLight";

        [ItemsSourceProperty(nameof(Circle_RadialDirectionList))]
        public string Circle_RadialDirection { get; set; } = ""; // default "" → fit 경로 → INI 하위호환

        // polar 4 필드 인스턴스화 (PropertyGrid 노출, ICustomTypeDescriptor 가 Circle_RadialDirection 빈값 시 hide).
        //  default = EdgeOptionLists.FaiCircle* 상수 byte-identical → Datum CTH 동등성 회귀 0.
        [Category("Edge")]
        public double Circle_PolarStepDeg { get; set; } = 10.0; // default = EdgeOptionLists.FaiCirclePolarStepDeg

        [Category("Edge")]
        public double Circle_RectL1Ratio { get; set; } = 0.02; // default = EdgeOptionLists.FaiCircleRectL1Ratio

        [Category("Edge")]
        public double Circle_RectL2Ratio { get; set; } = 0.02; // default = EdgeOptionLists.FaiCircleRectL2Ratio

        [Category("Edge")]
        [ItemsSourceProperty(nameof(Circle_PolarEdgeSelectionList))]
        public string Circle_PolarEdgeSelection { get; set; } = "First"; // default = EdgeOptionLists.FaiCircleEdgeSelection

        // PropertyGrid ComboBox 옵션 래퍼
        [PropertyTools.DataAnnotations.Browsable(false)]
        public List<string> EdgePolarityList { get { return EdgeOptionLists.FAIPolarities; } }

        // RadialDirection 콤보 옵션 래퍼 (단일 소스 EdgeOptionLists.RadialDirections 직접 참조)
        [PropertyTools.DataAnnotations.Browsable(false)]
        public List<string> Circle_RadialDirectionList { get { return EdgeOptionLists.RadialDirections; } }

        // Selections 옵션 래퍼 (EdgeOptionLists 단일 소스)
        [PropertyTools.DataAnnotations.Browsable(false)]
        public List<string> Circle_PolarEdgeSelectionList { get { return EdgeOptionLists.Selections; } }

        // idempotent migration: 기존 INI 로드 후 sentinel (0/"") 감지 시 EdgeOptionLists 단일 소스에서 default 채움.
        public CircleDiameterMeasurement(object owner) : base(owner)
        {
            if (Circle_PolarStepDeg == 0.0) Circle_PolarStepDeg = EdgeOptionLists.FaiCirclePolarStepDeg;
            if (Circle_RectL1Ratio  == 0.0) Circle_RectL1Ratio  = EdgeOptionLists.FaiCircleRectL1Ratio;
            if (Circle_RectL2Ratio  == 0.0) Circle_RectL2Ratio  = EdgeOptionLists.FaiCircleRectL2Ratio;
            if (string.IsNullOrEmpty(Circle_PolarEdgeSelection)) Circle_PolarEdgeSelection = EdgeOptionLists.FaiCircleEdgeSelection;
        }

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
            overlays = new List<EdgeInspectionOverlay>(); // 아래에서 채움

            var svc = new VisionAlgorithmService();
            double foundRow, foundCol, foundRadius;
            HTuple edgeRowsAcc = new HTuple(); // 검출 에지 누적 (폴라 경로 only)
            HTuple edgeColsAcc = new HTuple();

            // Circle_RadialDirection 빈값=fit / Inward,Outward=polar 분기
            if (string.IsNullOrEmpty(Circle_RadialDirection))
            {
                // 기존 fit 경로 (인자 순서/EdgePolarity 사용 규칙 v1.0 동일 → INI 하위호환 회귀 0)
                if (!svc.TryFindCircle(image,
                    Circle_Row, Circle_Col, Circle_Radius,
                    datumTransform,
                    Sigma, EdgeThreshold, EdgePolarity,
                    out foundRow, out foundCol, out foundRadius, out error))
                {
                    return false;
                }
            }
            else
            {
                // polar 경로:
                //  polarity = MapRadialDirectionToHalconPolarity(Circle_RadialDirection) (단일 소스, EdgePolarity 무시)
                //  step/L1/L2/selection = 인스턴스 필드 (default = EdgeOptionLists.FaiCircle*, Datum CTH default 와 동일 → 동등성 결정적)
                string polarity = EdgeOptionLists.MapRadialDirectionToHalconPolarity(Circle_RadialDirection);
                bool[] unusedStrips;
                // edgeRowsAcc/edgeColsAcc 로 캡처 (overlay 시각화 용)
                if (!svc.TryFindCircleByPolarSampling(
                    image,
                    Circle_Row, Circle_Col, Circle_Radius,
                    Circle_PolarStepDeg,
                    Circle_RectL1Ratio,
                    Circle_RectL2Ratio,
                    Sigma, EdgeThreshold, polarity,
                    Circle_PolarEdgeSelection,
                    datumTransform,
                    out foundRow, out foundCol, out foundRadius,
                    out edgeRowsAcc, out edgeColsAcc, out unusedStrips,
                    out error))
                {
                    return false;
                }
            }

            // 검사 후 시각화 overlay 2종 (검출 원 근사 + 지름 라인).
            //  사용자 요구: 테스트 시 원만 표시, edit 시 사각형 표시. Strip 사각형은 FAI 노드 선택 preview 경로
            //  (HalconDisplayService.RenderFaiCircleStripPreview + MainResultViewerControl) 로 이동. Edge X 마커 제거.
            AppendCircleResultOverlays(overlays, foundRow, foundCol, foundRadius);

            resultValue = foundRadius * 2.0 * pixelResolution;
            return true;
        }

        // 검사 결과 overlay 2종 (X 마커 / Strip 사각형 제거).
        //  1) 검출 원 근사 (72-point cloud as line segments, 양 경로 공통).
        //  2) 지름 라인 (수평, FAI-DistLine 청록).
        private static void AppendCircleResultOverlays(
            List<EdgeInspectionOverlay> overlays,
            double foundRow, double foundCol, double foundRadius)
        {
            // 검출 원 근사: 72-point cloud 를 line segment 로 연결 (X 마커 없이 라인만)
            //  Points 없이 LineRow1/Col1 → LineRow2/Col2 segment * 72 (FAI-Edge1 → 녹/적 suffix + DispLine 만 호출, X 마커 분기 미진입).
            if (foundRadius > 0)
            {
                const int circleSampleCount = 72;
                double prevR = foundRow - foundRadius * System.Math.Sin(0);
                double prevC = foundCol + foundRadius * System.Math.Cos(0);
                for (int i = 1; i <= circleSampleCount; i++)
                {
                    double t = (2.0 * System.Math.PI * i) / circleSampleCount;
                    double nextR = foundRow - foundRadius * System.Math.Sin(t);
                    double nextC = foundCol + foundRadius * System.Math.Cos(t);
                    overlays.Add(new EdgeInspectionOverlay
                    {
                        RoiId = "FAI-Edge1", // HalconDisplayService 녹/적 분기 + Action_FAIMeasurement -OK/-NG suffix
                        LineRow1 = prevR, LineColumn1 = prevC,
                        LineRow2 = nextR, LineColumn2 = nextC
                        // Points 명시 안 함 → null → HalconDisplayService Points X 마커 분기 미진입 (라인만 그림)
                    });
                    prevR = nextR; prevC = nextC;
                }
            }

            // 지름 라인 (수평, FAI-DistLine 청록). Points X 마커 제거.
            if (foundRadius > 0)
            {
                overlays.Add(new EdgeInspectionOverlay
                {
                    RoiId = "FAI-DistLine",
                    LineRow1 = foundRow, LineColumn1 = foundCol - foundRadius,
                    LineRow2 = foundRow, LineColumn2 = foundCol + foundRadius
                    // Points 명시 안 함 → null → X 마커 분기 미진입
                });
            }
        }

        // PropertyGrid 동적 노출. FAIConfig 패턴 동형.
        public System.ComponentModel.PropertyDescriptorCollection GetProperties(System.Attribute[] attributes) { return BuildFilteredProperties(attributes); }
        public System.ComponentModel.PropertyDescriptorCollection GetProperties() { return BuildFilteredProperties(null); }
        private System.ComponentModel.PropertyDescriptorCollection BuildFilteredProperties(System.Attribute[] attrs) {
            var sourceNames = new System.Collections.Generic.HashSet<string> {
                nameof(EdgePolarityList),
                nameof(Circle_RadialDirectionList),
                nameof(Circle_PolarEdgeSelectionList),
            };
            return DynamicPropertyHelper.FilterProperties(this, attrs,
                name => IsHiddenForRadialDirection(name, Circle_RadialDirection),
                sourceNames);
        }
        // Circle_RadialDirection 빈값 (fit 경로) 시 polar 4 필드 + List hide
        private static bool IsHiddenForRadialDirection(string name, string radialDir) {
            if (string.IsNullOrEmpty(radialDir)) {
                if (name == "Circle_PolarStepDeg") return true;
                if (name == "Circle_RectL1Ratio")  return true;
                if (name == "Circle_RectL2Ratio")  return true;
                if (name == "Circle_PolarEdgeSelection") return true;
                if (name == "Circle_PolarEdgeSelectionList") return true;
            }
            return false;
        }
        public System.ComponentModel.AttributeCollection GetAttributes() { return System.ComponentModel.TypeDescriptor.GetAttributes(this, true); }
        public string GetClassName() { return System.ComponentModel.TypeDescriptor.GetClassName(this, true); }
        public string GetComponentName() { return System.ComponentModel.TypeDescriptor.GetComponentName(this, true); }
        public System.ComponentModel.TypeConverter GetConverter() { return System.ComponentModel.TypeDescriptor.GetConverter(this, true); }
        public System.ComponentModel.EventDescriptor GetDefaultEvent() { return System.ComponentModel.TypeDescriptor.GetDefaultEvent(this, true); }
        public System.ComponentModel.PropertyDescriptor GetDefaultProperty() { return System.ComponentModel.TypeDescriptor.GetDefaultProperty(this, true); }
        public object GetEditor(System.Type editorBaseType) { return System.ComponentModel.TypeDescriptor.GetEditor(this, editorBaseType, true); }
        public System.ComponentModel.EventDescriptorCollection GetEvents(System.Attribute[] attributes) { return System.ComponentModel.TypeDescriptor.GetEvents(this, attributes, true); }
        public System.ComponentModel.EventDescriptorCollection GetEvents() { return System.ComponentModel.TypeDescriptor.GetEvents(this, true); }
        public object GetPropertyOwner(System.ComponentModel.PropertyDescriptor pd) { return this; }
    }
}
