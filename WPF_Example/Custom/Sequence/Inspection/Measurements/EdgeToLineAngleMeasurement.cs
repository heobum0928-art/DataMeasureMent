//260519 hbk Phase 31 D-05 — D1/H5: Datum A 기준 직선 각도
using System.Collections.Generic;
using HalconDotNet;
using PropertyTools.DataAnnotations;
using ReringProject.Halcon.Algorithms;
using ReringProject.Halcon.Models;

namespace ReringProject.Sequence
{
    /// <summary>
    /// Point ROI 에서 에지 라인을 피팅하고, Datum A 기준선과의 각도(degree)를 반환한다.
    /// EdgeToLineDistanceMeasurement 의 "ROI 1개 + Datum" 패턴을 거리 대신 각도로 연장한 구조.
    /// 단위는 degree 이므로 pixelResolution 미적용.
    /// D-05: D1/H5(SOP) = 신규 타입 EdgeToLineAngle (ROI 1개 + Datum A 기준선).
    /// </summary>
    public class EdgeToLineAngleMeasurement : MeasurementBase, IDatumOriginConsumer //260519 hbk Phase 31 D-05
    {
        public override string TypeName { get { return "EdgeToLineAngle"; } } //260519 hbk Phase 31 D-05

        [Category("Point|ROI")] //260519 hbk Phase 31 D-05
        public double Point_Row { get; set; } //260519 hbk Phase 31 D-05
        public double Point_Col { get; set; } //260519 hbk Phase 31 D-05
        public double Point_Phi { get; set; } //260519 hbk Phase 31 D-05
        public double Point_Length1 { get; set; } //260519 hbk Phase 31 D-05
        public double Point_Length2 { get; set; } //260519 hbk Phase 31 D-05

        [Category("Edge")] //260519 hbk Phase 31 D-05
        public int EdgeThreshold { get; set; } = 10; //260519 hbk Phase 31 D-05
        public double Sigma { get; set; } = 1.0; //260519 hbk Phase 31 D-05
        public int EdgeSampleCount { get; set; } = 20; //260519 hbk Phase 31 D-05
        public int EdgeTrimCount { get; set; } = 10; //260519 hbk Phase 31 D-05
        [ItemsSourceProperty(nameof(EdgePolarityList))] //260519 hbk Phase 31 D-05
        public string EdgePolarity { get; set; } = "DarkToLight"; //260519 hbk Phase 31 D-05
        [ItemsSourceProperty(nameof(EdgeDirectionList))] //260519 hbk Phase 31 D-05
        public string EdgeDirection { get; set; } = "TtoB"; //260519 hbk Phase 31 D-05

        //260519 hbk Phase 31 D-05 — PropertyGrid ComboBox 옵션 래퍼
        [PropertyTools.DataAnnotations.Browsable(false)] //260519 hbk Phase 31 D-05
        public List<string> EdgeDirectionList { get { return EdgeOptionLists.Directions; } } //260519 hbk Phase 31 D-05
        [PropertyTools.DataAnnotations.Browsable(false)] //260519 hbk Phase 31 D-05
        public List<string> EdgePolarityList { get { return EdgeOptionLists.FAIPolarities; } } //260519 hbk Phase 31 D-05

        //260519 hbk Phase 31 D-05 — datum 교점 좌표 runtime 주입 전용 (Action_FAIMeasurement IDatumOriginConsumer 경로).
        //  transient: PropertyGrid 미표시, JSON 직렬화 제외. EdgeToLineDistanceMeasurement.cs L69~80 동일 패턴.
        [System.ComponentModel.Browsable(false)] //260519 hbk Phase 31 D-05
        [PropertyTools.DataAnnotations.Browsable(false)] //260519 hbk Phase 31 D-05
        [Newtonsoft.Json.JsonIgnore] //260519 hbk Phase 31 D-05
        public double DatumOriginRow { get; set; } //260519 hbk Phase 31 D-05 — datum 교점 row (image 좌표). 미주입 시 0.
        [System.ComponentModel.Browsable(false)] //260519 hbk Phase 31 D-05
        [PropertyTools.DataAnnotations.Browsable(false)] //260519 hbk Phase 31 D-05
        [Newtonsoft.Json.JsonIgnore] //260519 hbk Phase 31 D-05
        public double DatumOriginCol { get; set; } //260519 hbk Phase 31 D-05 — datum 교점 col (image 좌표). 미주입 시 0.
        [System.ComponentModel.Browsable(false)] //260519 hbk Phase 31 D-05
        [PropertyTools.DataAnnotations.Browsable(false)] //260519 hbk Phase 31 D-05
        [Newtonsoft.Json.JsonIgnore] //260519 hbk Phase 31 D-05
        public double DatumAngleRad { get; set; } //260519 hbk Phase 31 D-05 — datum 1차(수평) 기준선 각도(rad). 미주입 시 0.
        //260519 hbk Phase 31 hotfix#3 — IDatumOriginConsumer 2차 각도 (EdgeToLineAngle 은 각도 측정 — 1차선 기준 유지, 속성만 구현)
        [System.ComponentModel.Browsable(false)] //260519 hbk Phase 31 hotfix#3
        [PropertyTools.DataAnnotations.Browsable(false)] //260519 hbk Phase 31 hotfix#3
        [Newtonsoft.Json.JsonIgnore] //260519 hbk Phase 31 hotfix#3
        public double DatumAngle2Rad { get; set; } //260519 hbk Phase 31 hotfix#3

        public EdgeToLineAngleMeasurement(object owner) : base(owner) { } //260519 hbk Phase 31 D-05

        public override bool TryExecute( //260519 hbk Phase 31 D-05
            HImage image,
            HTuple datumTransform,
            double pixelResolution,
            out double resultValue,
            out string error,
            out List<EdgeInspectionOverlay> overlays)
        {
            resultValue = 0;
            error = null;
            overlays = new List<EdgeInspectionOverlay>(); //260519 hbk Phase 31 D-05

            var svc = new VisionAlgorithmService();
            double pr1, pc1, pr2, pc2;

            //260519 hbk Phase 31 D-05 — TryFitLine "All" 고정 (memory feedback — EdgeSelection 명시 필수, CO-23-01 구조적 차단)
            if (!svc.TryFitLine(image,
                Point_Row, Point_Col, Point_Phi, Point_Length1, Point_Length2,
                datumTransform,
                EdgeSampleCount, EdgeTrimCount, Sigma, EdgeThreshold,
                EdgeDirection, EdgePolarity,
                out pr1, out pc1, out pr2, out pc2, out error,
                "All")) //260519 hbk Phase 31 D-05 — EdgeSelection "All" 고정
            {
                return false;
            }

            //260519 hbk Phase 31 D-05 — Datum A 기준선 2점 구성: DatumOriginRow/Col 중심 ±200px, 방향 = DatumAngleRad
            double sinT = System.Math.Sin(DatumAngleRad); //260519 hbk Phase 31 D-05
            double cosT = System.Math.Cos(DatumAngleRad); //260519 hbk Phase 31 D-05
            double daR1 = DatumOriginRow - 200.0 * sinT; //260519 hbk Phase 31 D-05
            double daC1 = DatumOriginCol - 200.0 * cosT; //260519 hbk Phase 31 D-05
            double daR2 = DatumOriginRow + 200.0 * sinT; //260519 hbk Phase 31 D-05
            double daC2 = DatumOriginCol + 200.0 * cosT; //260519 hbk Phase 31 D-05

            //260519 hbk Phase 31 D-05 — AngleLineLine: 에지 피팅선 vs Datum A 기준선 각도(degree, 0~180)
            resultValue = VisionAlgorithmService.AngleLineLine(
                pr1, pc1, pr2, pc2,
                daR1, daC1, daR2, daC2); //260519 hbk Phase 31 D-05
            return true;
        }
    }
}
