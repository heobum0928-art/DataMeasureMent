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
    //260529 hbk Phase 39.1 G2-04 — ICustomTypeDescriptor 추가: Circle_RadialDirection 빈값 시 polar 4 필드 PropertyGrid hide.
    public class CircleDiameterMeasurement : MeasurementBase, System.ComponentModel.ICustomTypeDescriptor //260529 hbk Phase 39.1 G2-04
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

        //260529 hbk Phase 39.1 G2-01 — polar 4 필드 인스턴스화 (PropertyGrid 노출, ICustomTypeDescriptor 가 Circle_RadialDirection 빈값 시 hide).
        //  default = EdgeOptionLists.FaiCircle* 상수 byte-identical → Datum CTH 동등성 회귀 0 (Phase 28 D-04).
        [Category("Edge")] //260529 hbk Phase 39.1 G2-01 — Edge 카테고리
        public double Circle_PolarStepDeg { get; set; } = 10.0; //260529 hbk Phase 39.1 G2-01 — default = EdgeOptionLists.FaiCirclePolarStepDeg

        [Category("Edge")] //260529 hbk Phase 39.1 G2-01
        public double Circle_RectL1Ratio { get; set; } = 0.02; //260529 hbk Phase 39.1 G2-01 — default = EdgeOptionLists.FaiCircleRectL1Ratio

        [Category("Edge")] //260529 hbk Phase 39.1 G2-01
        public double Circle_RectL2Ratio { get; set; } = 0.02; //260529 hbk Phase 39.1 G2-01 — default = EdgeOptionLists.FaiCircleRectL2Ratio

        [Category("Edge")] //260529 hbk Phase 39.1 G2-01
        [ItemsSourceProperty(nameof(Circle_PolarEdgeSelectionList))] //260529 hbk Phase 39.1 — Circle_RadialDirectionList 패턴 동형
        public string Circle_PolarEdgeSelection { get; set; } = "First"; //260529 hbk Phase 39.1 G2-01 — default = EdgeOptionLists.FaiCircleEdgeSelection

        //260423 hbk WR-RT-02 PropertyGrid ComboBox 옵션 래퍼
        [PropertyTools.DataAnnotations.Browsable(false)]
        public List<string> EdgePolarityList { get { return EdgeOptionLists.FAIPolarities; } }

        //260508 hbk Phase 28 REQ-28-01 — RadialDirection 콤보 옵션 래퍼 (Phase 17 D-02 단일 소스 EdgeOptionLists.RadialDirections 직접 참조)
        [PropertyTools.DataAnnotations.Browsable(false)] //260508 hbk Phase 28
        public List<string> Circle_RadialDirectionList { get { return EdgeOptionLists.RadialDirections; } } //260508 hbk Phase 28

        //260529 hbk Phase 39.1 G2-01 — Selections 옵션 래퍼 (EdgeOptionLists 단일 소스)
        [PropertyTools.DataAnnotations.Browsable(false)] //260529 hbk Phase 39.1
        public List<string> Circle_PolarEdgeSelectionList { get { return EdgeOptionLists.Selections; } } //260529 hbk Phase 39.1

        //260529 hbk Phase 39.1 G2-03 — idempotent migration: 기존 INI 로드 후 sentinel (0/"") 감지 시 EdgeOptionLists 단일 소스에서 default 채움.
        public CircleDiameterMeasurement(object owner) : base(owner) //260529 hbk Phase 39.1 G2-03
        {
            if (Circle_PolarStepDeg == 0.0) Circle_PolarStepDeg = EdgeOptionLists.FaiCirclePolarStepDeg; //260529 hbk Phase 39.1 G2-03
            if (Circle_RectL1Ratio  == 0.0) Circle_RectL1Ratio  = EdgeOptionLists.FaiCircleRectL1Ratio;  //260529 hbk Phase 39.1 G2-03
            if (Circle_RectL2Ratio  == 0.0) Circle_RectL2Ratio  = EdgeOptionLists.FaiCircleRectL2Ratio;  //260529 hbk Phase 39.1 G2-03
            if (string.IsNullOrEmpty(Circle_PolarEdgeSelection)) Circle_PolarEdgeSelection = EdgeOptionLists.FaiCircleEdgeSelection; //260529 hbk Phase 39.1 G2-03
        }

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
            overlays = new List<EdgeInspectionOverlay>(); //260422 hbk Phase 7: 5종 overlay 미구현 — 빈 리스트 반환 (D-03) //260529 hbk Phase 39.1 CO-39.1-01 — 아래에서 채움

            var svc = new VisionAlgorithmService();
            double foundRow, foundCol, foundRadius;
            HTuple edgeRowsAcc = new HTuple(); //260529 hbk CO-39.1-01 — 검출 에지 누적 (폴라 경로 only)
            HTuple edgeColsAcc = new HTuple(); //260529 hbk CO-39.1-01

            //260508 hbk Phase 28 REQ-28-02 (D-01) — Circle_RadialDirection 빈값=fit / Inward,Outward=polar 분기
            if (string.IsNullOrEmpty(Circle_RadialDirection)) //260508 hbk Phase 28
            {
                //260508 hbk Phase 28 REQ-28-04 — 기존 fit 경로 (인자 순서/EdgePolarity 사용 규칙 v1.0 동일 → INI 하위호환 회귀 0)
                if (!svc.TryFindCircle(image,
                    Circle_Row, Circle_Col, Circle_Radius,
                    datumTransform,
                    Sigma, EdgeThreshold, EdgePolarity,
                    out foundRow, out foundCol, out foundRadius, out error))
                {
                    return false;
                }
            }
            else //260508 hbk Phase 28
            {
                //260508 hbk Phase 28 REQ-28-02/REQ-28-03 (D-02, D-04, D-08) — polar 경로:
                //  polarity = MapRadialDirectionToHalconPolarity(Circle_RadialDirection) (단일 소스, EdgePolarity 무시)
                //  step/L1/L2/selection = EdgeOptionLists.FaiCircle* defaults (Datum CTH default 와 동일 → 동등성 결정적)
                //260529 hbk Phase 39.1 G2-01 — 4 정적 상수 참조를 4 인스턴스 필드로 교체 (PropertyGrid override 가능). default = EdgeOptionLists.FaiCircle* byte-identical → INI 회귀 0.
                string polarity = EdgeOptionLists.MapRadialDirectionToHalconPolarity(Circle_RadialDirection); //260508 hbk Phase 28
                bool[] unusedStrips; //260508 hbk Phase 28
                //260529 hbk CO-39.1-01 — was unusedRows/unusedCols → edgeRowsAcc/edgeColsAcc 로 캡처 (overlay 시각화 용)
                if (!svc.TryFindCircleByPolarSampling(
                    image,
                    Circle_Row, Circle_Col, Circle_Radius,
                    Circle_PolarStepDeg,    //260529 hbk Phase 39.1 G2-01 — was EdgeOptionLists.FaiCirclePolarStepDeg
                    Circle_RectL1Ratio,     //260529 hbk Phase 39.1 G2-01 — was EdgeOptionLists.FaiCircleRectL1Ratio
                    Circle_RectL2Ratio,     //260529 hbk Phase 39.1 G2-01 — was EdgeOptionLists.FaiCircleRectL2Ratio
                    Sigma, EdgeThreshold, polarity,
                    Circle_PolarEdgeSelection, //260529 hbk Phase 39.1 G2-01 — was EdgeOptionLists.FaiCircleEdgeSelection
                    datumTransform,
                    out foundRow, out foundCol, out foundRadius,
                    out edgeRowsAcc, out edgeColsAcc, out unusedStrips, //260529 hbk CO-39.1-01
                    out error)) //260508 hbk Phase 28
                {
                    return false; //260508 hbk Phase 28
                }
            }

            //260529 hbk CO-39.1-01 — UAT FAIL #1 후속: 검사 후 시각화 overlay 4종 추가 (Strip 사각형 + 검출 에지 점 + 검출 원 근사 + 지름 라인).
            //  사용자 요구: "에지나 원이 보이고 지름 라인이 나왔으면 함" + "작은 사각형도 파라미터 변하면 보일수있게".
            //  Phase 7 D-03 빈 리스트 정책 의도적 뒤집기 (Phase 23.1 EdgeToLineDistance 260517-ja8 패턴 동형).
            AppendCircleVisualizationOverlays(overlays, datumTransform,
                Circle_Row, Circle_Col, Circle_Radius,
                Circle_PolarStepDeg, Circle_RectL1Ratio, Circle_RectL2Ratio,
                Circle_RadialDirection, edgeRowsAcc, edgeColsAcc,
                foundRow, foundCol, foundRadius);

            resultValue = foundRadius * 2.0 * pixelResolution;
            return true;
        }

        //260529 hbk CO-39.1-01 — 검사 시점 시각화 overlay 생성. Phase 7 D-03 빈 리스트 정책 의도적 뒤집기.
        //  1) Strip 사각형 (4-line, 폴라 경로 only — fit 경로는 strip 미사용) — stepDeg/L1Ratio/L2Ratio 변경 즉시 반영.
        //  2) 검출 에지 점 (폴라 경로 only, FAI-Edge1 녹/적).
        //  3) 검출 원 근사 (72 point cloud, 양 경로 공통, FAI-Edge1).
        //  4) 지름 라인 (수평, 양 경로 공통, FAI-DistLine 청록).
        private static void AppendCircleVisualizationOverlays(
            List<EdgeInspectionOverlay> overlays, HTuple datumTransform,
            double searchRow, double searchCol, double searchRadius,
            double stepDeg, double l1Ratio, double l2Ratio,
            string radialDirection,
            HTuple edgeRows, HTuple edgeCols,
            double foundRow, double foundCol, double foundRadius)
        {
            //260529 hbk CO-39.1-01 — Strip 시각화는 폴라 경로(Inward/Outward) 에서만 의미. fit 경로는 strip 사용 안 함.
            bool isPolar = !string.IsNullOrEmpty(radialDirection);

            //260529 hbk CO-39.1-01 — Strip 그리려면 search center 를 image 좌표로 변환 필요 (TryFindCircleByPolarSampling 내부 변환 미러).
            double tRow = searchRow, tCol = searchCol;
            if (datumTransform != null && datumTransform.Length > 0)
            {
                try
                {
                    HTuple rT, cT;
                    HOperatorSet.AffineTransPoint2d(datumTransform, searchRow, searchCol, out rT, out cT);
                    tRow = rT.D; tCol = cT.D;
                }
                catch { /* identity fallback */ }
            }

            //260529 hbk CO-39.1-01 — (1) Strip 사각형 (polar only) — HalconDisplayService.RenderCircleStrips canonical 식 미러
            if (isPolar && searchRadius > 0)
            {
                double sd = stepDeg;
                if (sd < 1.0) sd = 1.0;
                if (sd > 30.0) sd = 30.0;
                int stepCount = (int)System.Math.Round(360.0 / sd);
                if (stepCount < 1) stepCount = 1;
                double stepRad = (2.0 * System.Math.PI) / stepCount;
                //  strip half-extent cap (VisionAlgorithmService.CircleStripHalfExtentCapPx 와 WYSIWYG)
                double length1 = System.Math.Min(searchRadius * l1Ratio, VisionAlgorithmService.CircleStripHalfExtentCapPx);
                double length2 = System.Math.Min(searchRadius * l2Ratio, VisionAlgorithmService.CircleStripHalfExtentCapPx);
                if (length1 < 1.0) length1 = 1.0;
                if (length2 < 1.0) length2 = 1.0;

                for (int i = 0; i < stepCount; i++)
                {
                    double thetaRad = i * stepRad;
                    double rectRow = tRow - searchRadius * System.Math.Sin(thetaRad);
                    double rectCol = tCol + searchRadius * System.Math.Cos(thetaRad);
                    double cosP = System.Math.Cos(thetaRad);
                    double sinP = System.Math.Sin(thetaRad);
                    double r1 = rectRow + (-length1) * cosP - (-length2) * sinP;
                    double c1 = rectCol + (-length1) * sinP + (-length2) * cosP;
                    double r2 = rectRow + (-length1) * cosP - ( length2) * sinP;
                    double c2 = rectCol + (-length1) * sinP + ( length2) * cosP;
                    double r3 = rectRow + ( length1) * cosP - ( length2) * sinP;
                    double c3 = rectCol + ( length1) * sinP + ( length2) * cosP;
                    double r4 = rectRow + ( length1) * cosP - (-length2) * sinP;
                    double c4 = rectCol + ( length1) * sinP + (-length2) * cosP;
                    //  Strip 1개 = 4 line overlay (RoiId="FAI-Strip" → HalconDisplayService else 분기 blue 렌더, suffix 무관)
                    overlays.Add(new EdgeInspectionOverlay { RoiId = "FAI-Strip", LineRow1 = r1, LineColumn1 = c1, LineRow2 = r2, LineColumn2 = c2 });
                    overlays.Add(new EdgeInspectionOverlay { RoiId = "FAI-Strip", LineRow1 = r2, LineColumn1 = c2, LineRow2 = r3, LineColumn2 = c3 });
                    overlays.Add(new EdgeInspectionOverlay { RoiId = "FAI-Strip", LineRow1 = r3, LineColumn1 = c3, LineRow2 = r4, LineColumn2 = c4 });
                    overlays.Add(new EdgeInspectionOverlay { RoiId = "FAI-Strip", LineRow1 = r4, LineColumn1 = c4, LineRow2 = r1, LineColumn2 = c1 });
                }
            }

            //260529 hbk CO-39.1-01 — (2) 검출 에지 점 (폴라 경로 only — fit 경로는 edge 점 미반환)
            if (edgeRows != null && edgeCols != null && edgeRows.Length > 0 && edgeRows.Length == edgeCols.Length)
            {
                var edgePoints = new List<EdgeInspectionPoint>();
                for (int i = 0; i < edgeRows.Length; i++)
                {
                    edgePoints.Add(new EdgeInspectionPoint { Row = edgeRows[i].D, Column = edgeCols[i].D });
                }
                overlays.Add(new EdgeInspectionOverlay
                {
                    RoiId = "FAI-Edge1", //  HalconDisplayService 녹/적 분기 + Action_FAIMeasurement -OK/-NG suffix
                    LineRow1 = foundRow, LineColumn1 = foundCol, LineRow2 = foundRow, LineColumn2 = foundCol,
                    Points = edgePoints
                });
            }

            //260529 hbk CO-39.1-01 — (3) 검출 원 근사 (72 point cloud, 양 경로 공통) — 사용자 "원이 보이고" 요구 충족
            if (foundRadius > 0)
            {
                const int circleSampleCount = 72;
                var circlePoints = new List<EdgeInspectionPoint>();
                for (int i = 0; i < circleSampleCount; i++)
                {
                    double t = (2.0 * System.Math.PI * i) / circleSampleCount;
                    circlePoints.Add(new EdgeInspectionPoint {
                        Row = foundRow - foundRadius * System.Math.Sin(t),
                        Column = foundCol + foundRadius * System.Math.Cos(t)
                    });
                }
                overlays.Add(new EdgeInspectionOverlay
                {
                    RoiId = "FAI-Edge1",
                    LineRow1 = foundRow, LineColumn1 = foundCol, LineRow2 = foundRow, LineColumn2 = foundCol,
                    Points = circlePoints
                });
            }

            //260529 hbk CO-39.1-01 — (4) 지름 라인 (수평, 양 경로 공통) — FAI-DistLine 청록
            if (foundRadius > 0)
            {
                overlays.Add(new EdgeInspectionOverlay
                {
                    RoiId = "FAI-DistLine",
                    LineRow1 = foundRow, LineColumn1 = foundCol - foundRadius,
                    LineRow2 = foundRow, LineColumn2 = foundCol + foundRadius,
                    Points = new List<EdgeInspectionPoint>
                    {
                        new EdgeInspectionPoint { Row = foundRow, Column = foundCol - foundRadius },
                        new EdgeInspectionPoint { Row = foundRow, Column = foundCol + foundRadius }
                    }
                });
            }
        }

        //260529 hbk Phase 39.1 G2-04 — PropertyGrid 동적 노출. FAIConfig L267-296 패턴 동형.
        public System.ComponentModel.PropertyDescriptorCollection GetProperties(System.Attribute[] attributes) { return BuildFilteredProperties(attributes); } //260529 hbk Phase 39.1 G2-04
        public System.ComponentModel.PropertyDescriptorCollection GetProperties() { return BuildFilteredProperties(null); } //260529 hbk Phase 39.1 G2-04
        private System.ComponentModel.PropertyDescriptorCollection BuildFilteredProperties(System.Attribute[] attrs) { //260529 hbk Phase 39.1 G2-04
            var sourceNames = new System.Collections.Generic.HashSet<string> {
                nameof(EdgePolarityList),
                nameof(Circle_RadialDirectionList),
                nameof(Circle_PolarEdgeSelectionList), //260529 hbk Phase 39.1 — 신규 List 화이트리스트
            };
            return DynamicPropertyHelper.FilterProperties(this, attrs,
                name => IsHiddenForRadialDirection(name, Circle_RadialDirection),
                sourceNames);
        }
        //260529 hbk Phase 39.1 G2-04 — Circle_RadialDirection 빈값 (fit 경로) 시 polar 4 필드 + List hide
        private static bool IsHiddenForRadialDirection(string name, string radialDir) { //260529 hbk Phase 39.1 G2-04
            if (string.IsNullOrEmpty(radialDir)) {
                if (name == "Circle_PolarStepDeg") return true;
                if (name == "Circle_RectL1Ratio")  return true;
                if (name == "Circle_RectL2Ratio")  return true;
                if (name == "Circle_PolarEdgeSelection") return true;
                if (name == "Circle_PolarEdgeSelectionList") return true;
            }
            return false;
        }
        public System.ComponentModel.AttributeCollection GetAttributes() { return System.ComponentModel.TypeDescriptor.GetAttributes(this, true); } //260529 hbk Phase 39.1 G2-04
        public string GetClassName() { return System.ComponentModel.TypeDescriptor.GetClassName(this, true); } //260529 hbk Phase 39.1 G2-04
        public string GetComponentName() { return System.ComponentModel.TypeDescriptor.GetComponentName(this, true); } //260529 hbk Phase 39.1 G2-04
        public System.ComponentModel.TypeConverter GetConverter() { return System.ComponentModel.TypeDescriptor.GetConverter(this, true); } //260529 hbk Phase 39.1 G2-04
        public System.ComponentModel.EventDescriptor GetDefaultEvent() { return System.ComponentModel.TypeDescriptor.GetDefaultEvent(this, true); } //260529 hbk Phase 39.1 G2-04
        public System.ComponentModel.PropertyDescriptor GetDefaultProperty() { return System.ComponentModel.TypeDescriptor.GetDefaultProperty(this, true); } //260529 hbk Phase 39.1 G2-04
        public object GetEditor(System.Type editorBaseType) { return System.ComponentModel.TypeDescriptor.GetEditor(this, editorBaseType, true); } //260529 hbk Phase 39.1 G2-04
        public System.ComponentModel.EventDescriptorCollection GetEvents(System.Attribute[] attributes) { return System.ComponentModel.TypeDescriptor.GetEvents(this, attributes, true); } //260529 hbk Phase 39.1 G2-04
        public System.ComponentModel.EventDescriptorCollection GetEvents() { return System.ComponentModel.TypeDescriptor.GetEvents(this, true); } //260529 hbk Phase 39.1 G2-04
        public object GetPropertyOwner(System.ComponentModel.PropertyDescriptor pd) { return this; } //260529 hbk Phase 39.1 G2-04
    }
}
