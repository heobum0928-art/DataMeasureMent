//260519 hbk Phase 31 D-01 — E8: 원중심 → Datum B Y 거리
using System.Collections.Generic;
using HalconDotNet;
using PropertyTools.DataAnnotations;
using ReringProject.Halcon.Algorithms;
using ReringProject.Halcon.Models;

namespace ReringProject.Sequence
{
    /// <summary>
    /// 원형 ROI 에서 TryFindCircle 로 원중심을 검출하고,
    /// Datum 기준선까지의 거리(mm)를 VisionAlgorithmService.ComputeProjectionDistance 로 산출한다.
    /// MeasureAxis="Y": Datum B 수평선(x축)까지 수직거리 — E8 기본 방향.
    /// MeasureAxis="X": Datum 수직선(y축)까지 거리.
    /// </summary>
    public class CircleCenterDistanceMeasurement : MeasurementBase, IDatumOriginConsumer //260519 hbk Phase 31 D-01
    {
        public override string TypeName { get { return "CircleCenterDistance"; } } //260519 hbk Phase 31 D-01

        [Category("Circle|ROI")] //260519 hbk Phase 31 D-01
        public double Circle_Row { get; set; } //260519 hbk Phase 31 D-01
        public double Circle_Col { get; set; } //260519 hbk Phase 31 D-01
        public double Circle_Radius { get; set; } //260519 hbk Phase 31 D-01

        [Category("Edge")] //260519 hbk Phase 31 D-01
        public int EdgeThreshold { get; set; } = 10; //260519 hbk Phase 31 D-01
        public double Sigma { get; set; } = 1.0; //260519 hbk Phase 31 D-01
        [ItemsSourceProperty(nameof(EdgePolarityList))] //260519 hbk Phase 31 D-01
        public string EdgePolarity { get; set; } = "DarkToLight"; //260519 hbk Phase 31 D-01

        //260519 hbk Phase 31 CO-23.1-02 — Datum 원 알고리즘 (polar sampling) 사용: Inward/Outward 방사 방향.
        //  CircleDiameterMeasurement Phase 28 REQ-28-01 패턴 동일. Phase 31 신규 타입 → INI 하위호환 불필요, 기본값 "Inward".
        [ItemsSourceProperty(nameof(Circle_RadialDirectionList))] //260519 hbk Phase 31 CO-23.1-02
        public string Circle_RadialDirection { get; set; } = "Inward"; //260519 hbk Phase 31 CO-23.1-02

        //260519 hbk Phase 31 D-01 — PropertyGrid ComboBox 옵션 래퍼
        [PropertyTools.DataAnnotations.Browsable(false)] //260519 hbk Phase 31 D-01
        public List<string> EdgePolarityList { get { return EdgeOptionLists.FAIPolarities; } } //260519 hbk Phase 31 D-01
        [PropertyTools.DataAnnotations.Browsable(false)] //260519 hbk Phase 31 CO-23.1-02
        public List<string> Circle_RadialDirectionList { get { return EdgeOptionLists.RadialDirections; } } //260519 hbk Phase 31 CO-23.1-02

        //260519 hbk Phase 31 D-01 — 측정 거리 축 선택: E8 = Datum B Y 방향이므로 기본값 "Y"
        [Category("Edge")] //260519 hbk Phase 31 D-01
        [ItemsSourceProperty(nameof(MeasureAxisList))] //260519 hbk Phase 31 D-01
        public string MeasureAxis { get; set; } = "Y"; //260519 hbk Phase 31 D-01 (E8 = Datum B Y 방향)
        [PropertyTools.DataAnnotations.Browsable(false)] //260519 hbk Phase 31 D-01
        public List<string> MeasureAxisList { get { return new List<string> { "Y", "X" }; } } //260519 hbk Phase 31 D-01

        //260519 hbk Phase 31 D-01 — datum 교점 좌표 runtime 주입 전용 (Action_FAIMeasurement IDatumOriginConsumer 경로).
        //  transient: PropertyGrid 미표시, JSON 직렬화 제외. EdgeToLineDistanceMeasurement.cs L69~80 동일 패턴.
        [System.ComponentModel.Browsable(false)] //260519 hbk Phase 31 D-01
        [PropertyTools.DataAnnotations.Browsable(false)] //260519 hbk Phase 31 D-01
        [Newtonsoft.Json.JsonIgnore] //260519 hbk Phase 31 D-01
        public double DatumOriginRow { get; set; } //260519 hbk Phase 31 D-01 — datum 교점 row (image 좌표). 미주입 시 0.
        [System.ComponentModel.Browsable(false)] //260519 hbk Phase 31 D-01
        [PropertyTools.DataAnnotations.Browsable(false)] //260519 hbk Phase 31 D-01
        [Newtonsoft.Json.JsonIgnore] //260519 hbk Phase 31 D-01
        public double DatumOriginCol { get; set; } //260519 hbk Phase 31 D-01 — datum 교점 col (image 좌표). 미주입 시 0.
        [System.ComponentModel.Browsable(false)] //260519 hbk Phase 31 D-01
        [PropertyTools.DataAnnotations.Browsable(false)] //260519 hbk Phase 31 D-01
        [Newtonsoft.Json.JsonIgnore] //260519 hbk Phase 31 D-01
        public double DatumAngleRad { get; set; } //260519 hbk Phase 31 D-01 — datum 기준선 각도(rad). 미주입 시 0.

        public CircleCenterDistanceMeasurement(object owner) : base(owner) { } //260519 hbk Phase 31 D-01

        public override bool TryExecute( //260519 hbk Phase 31 D-01
            HImage image,
            HTuple datumTransform,
            double pixelResolution,
            out double resultValue,
            out string error,
            out List<EdgeInspectionOverlay> overlays)
        {
            resultValue = 0;
            error = null;
            overlays = new List<EdgeInspectionOverlay>(); //260519 hbk Phase 31 D-01

            var svc = new VisionAlgorithmService();
            double foundRow, foundCol, foundRadius;

            //260519 hbk Phase 31 CO-23.1-02 — Circle_RadialDirection 빈값=legacy fit / Inward,Outward=Datum polar sampling 분기
            //  CircleDiameterMeasurement Phase 28 REQ-28-02 패턴 동일.
            if (string.IsNullOrEmpty(Circle_RadialDirection)) //260519 hbk Phase 31 CO-23.1-02
            {
                //260519 hbk Phase 31 CO-23.1-02 — legacy fit 경로 (EdgePolarity 사용)
                if (!svc.TryFindCircle(image,
                    Circle_Row, Circle_Col, Circle_Radius,
                    datumTransform,
                    Sigma, EdgeThreshold, EdgePolarity,
                    out foundRow, out foundCol, out foundRadius, out error))
                {
                    return false;
                }
            }
            else //260519 hbk Phase 31 CO-23.1-02
            {
                //260519 hbk Phase 31 CO-23.1-02 — Datum 원 알고리즘 (polar sampling):
                //  polarity = MapRadialDirectionToHalconPolarity(Circle_RadialDirection) (EdgePolarity 무시)
                //  step/L1/L2/selection = EdgeOptionLists.FaiCircle* defaults (Datum CTH default 와 동일)
                string polarity = EdgeOptionLists.MapRadialDirectionToHalconPolarity(Circle_RadialDirection); //260519 hbk Phase 31 CO-23.1-02
                HTuple unusedRows, unusedCols; //260519 hbk Phase 31 CO-23.1-02
                bool[] unusedStrips; //260519 hbk Phase 31 CO-23.1-02
                if (!svc.TryFindCircleByPolarSampling(
                    image,
                    Circle_Row, Circle_Col, Circle_Radius,
                    EdgeOptionLists.FaiCirclePolarStepDeg,
                    EdgeOptionLists.FaiCircleRectL1Ratio,
                    EdgeOptionLists.FaiCircleRectL2Ratio,
                    Sigma, EdgeThreshold, polarity,
                    EdgeOptionLists.FaiCircleEdgeSelection,
                    datumTransform,
                    out foundRow, out foundCol, out foundRadius,
                    out unusedRows, out unusedCols, out unusedStrips,
                    out error)) //260519 hbk Phase 31 CO-23.1-02
                {
                    return false; //260519 hbk Phase 31 CO-23.1-02
                }
            }

            //260519 hbk Phase 31 D-01 — D-04 공용 헬퍼 호출: 원중심 → datum 기준선 투영 거리
            resultValue = VisionAlgorithmService.ComputeProjectionDistance(
                foundRow, foundCol,
                DatumOriginRow, DatumOriginCol, DatumAngleRad,
                pixelResolution, MeasureAxis);

            return true;
        }
    }
}
