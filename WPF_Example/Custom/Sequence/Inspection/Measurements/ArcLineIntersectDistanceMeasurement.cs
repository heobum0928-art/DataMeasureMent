//260519 hbk Phase 31 D-01 — I9/I10: 3점 호 피팅 + 라인 피팅 + 원-직선 교점 → Datum 거리
using System.Collections.Generic;
using HalconDotNet;
using PropertyTools.DataAnnotations;
using ReringProject.Halcon.Algorithms;
using ReringProject.Halcon.Models;

namespace ReringProject.Sequence
{
    /// <summary>
    /// Arc P1/P2/P3 각 ROI 에서 에지점을 추출해 3점 호를 피팅하고,
    /// Line ROI 에서 직선을 피팅한 뒤, 원과 직선의 교점(D-10: ROI 내부 해 선택)을
    /// Datum 기준선까지의 거리(mm)로 환산한다.
    /// I9/I10(SOP): 호∩라인 교점 1개 → Datum C X 방향 거리. 기본 MeasureAxis="X".
    /// 교점이 2개일 때 Line ROI 중심에 가장 가까운 해를 선택한다 (D-10).
    /// 각 P1/P2/P3 는 독립 ROI — TryFitLine("All") 으로 에지점 1개(라인 중점) 추출 후 FitCircleContourXld.
    /// </summary>
    public class ArcLineIntersectDistanceMeasurement : MeasurementBase, IDatumOriginConsumer //260519 hbk Phase 31 D-01
    {
        public override string TypeName { get { return "ArcLineIntersectDistance"; } } //260519 hbk Phase 31 D-01

        // ── Arc 3점 ROI ──────────────────────────────────────────────────────────────
        [Category("Arc|ROI")] //260519 hbk Phase 31 D-01
        public double Arc_P1_Row { get; set; } //260519 hbk Phase 31 D-01
        public double Arc_P1_Col { get; set; } //260519 hbk Phase 31 D-01
        public double Arc_P1_Phi { get; set; } //260519 hbk Phase 31 D-01
        public double Arc_P1_Length1 { get; set; } //260519 hbk Phase 31 D-01
        public double Arc_P1_Length2 { get; set; } //260519 hbk Phase 31 D-01

        public double Arc_P2_Row { get; set; } //260519 hbk Phase 31 D-01
        public double Arc_P2_Col { get; set; } //260519 hbk Phase 31 D-01
        public double Arc_P2_Phi { get; set; } //260519 hbk Phase 31 D-01
        public double Arc_P2_Length1 { get; set; } //260519 hbk Phase 31 D-01
        public double Arc_P2_Length2 { get; set; } //260519 hbk Phase 31 D-01

        public double Arc_P3_Row { get; set; } //260519 hbk Phase 31 D-01
        public double Arc_P3_Col { get; set; } //260519 hbk Phase 31 D-01
        public double Arc_P3_Phi { get; set; } //260519 hbk Phase 31 D-01
        public double Arc_P3_Length1 { get; set; } //260519 hbk Phase 31 D-01
        public double Arc_P3_Length2 { get; set; } //260519 hbk Phase 31 D-01

        // ── Line ROI ─────────────────────────────────────────────────────────────────
        [Category("Line|ROI")] //260519 hbk Phase 31 D-01
        public double Line_Row { get; set; } //260519 hbk Phase 31 D-01
        public double Line_Col { get; set; } //260519 hbk Phase 31 D-01
        public double Line_Phi { get; set; } //260519 hbk Phase 31 D-01
        public double Line_Length1 { get; set; } //260519 hbk Phase 31 D-01
        public double Line_Length2 { get; set; } //260519 hbk Phase 31 D-01

        // ── Edge 파라미터 ─────────────────────────────────────────────────────────────
        [Category("Edge")] //260519 hbk Phase 31 D-01
        public int EdgeThreshold { get; set; } = 10; //260519 hbk Phase 31 D-01
        public double Sigma { get; set; } = 1.0; //260519 hbk Phase 31 D-01
        public int EdgeSampleCount { get; set; } = 20; //260519 hbk Phase 31 D-01
        public int EdgeTrimCount { get; set; } = 10; //260519 hbk Phase 31 D-01
        [ItemsSourceProperty(nameof(EdgePolarityList))] //260519 hbk Phase 31 D-01
        public string EdgePolarity { get; set; } = "DarkToLight"; //260519 hbk Phase 31 D-01
        [ItemsSourceProperty(nameof(EdgeDirectionList))] //260519 hbk Phase 31 D-01
        public string EdgeDirection { get; set; } = "TtoB"; //260519 hbk Phase 31 D-01

        //260519 hbk Phase 31 D-01 — PropertyGrid ComboBox 옵션 래퍼
        [PropertyTools.DataAnnotations.Browsable(false)] //260519 hbk Phase 31 D-01
        public List<string> EdgeDirectionList { get { return EdgeOptionLists.Directions; } } //260519 hbk Phase 31 D-01
        [PropertyTools.DataAnnotations.Browsable(false)] //260519 hbk Phase 31 D-01
        public List<string> EdgePolarityList { get { return EdgeOptionLists.FAIPolarities; } } //260519 hbk Phase 31 D-01

        // ── MeasureAxis ───────────────────────────────────────────────────────────────
        //260519 hbk Phase 31 D-01 — I9/I10 = Datum C X 방향이므로 기본값 "X"
        [Category("Edge")] //260519 hbk Phase 31 D-01
        [ItemsSourceProperty(nameof(MeasureAxisList))] //260519 hbk Phase 31 D-01
        public string MeasureAxis { get; set; } = "X"; //260519 hbk Phase 31 D-01 (I9/I10 = Datum C X 방향)
        [PropertyTools.DataAnnotations.Browsable(false)] //260519 hbk Phase 31 D-01
        public List<string> MeasureAxisList { get { return new List<string> { "X", "Y" }; } } //260519 hbk Phase 31 D-01

        // ── IDatumOriginConsumer 3 transient 필드 ────────────────────────────────────
        //260519 hbk Phase 31 D-01 — datum 교점 좌표 runtime 주입 전용. EdgeToLineDistanceMeasurement.cs L69~80 동일 패턴.
        [System.ComponentModel.Browsable(false)] //260519 hbk Phase 31 D-01
        [PropertyTools.DataAnnotations.Browsable(false)] //260519 hbk Phase 31 D-01
        [Newtonsoft.Json.JsonIgnore] //260519 hbk Phase 31 D-01
        public double DatumOriginRow { get; set; } //260519 hbk Phase 31 D-01
        [System.ComponentModel.Browsable(false)] //260519 hbk Phase 31 D-01
        [PropertyTools.DataAnnotations.Browsable(false)] //260519 hbk Phase 31 D-01
        [Newtonsoft.Json.JsonIgnore] //260519 hbk Phase 31 D-01
        public double DatumOriginCol { get; set; } //260519 hbk Phase 31 D-01
        [System.ComponentModel.Browsable(false)] //260519 hbk Phase 31 D-01
        [PropertyTools.DataAnnotations.Browsable(false)] //260519 hbk Phase 31 D-01
        [Newtonsoft.Json.JsonIgnore] //260519 hbk Phase 31 D-01
        public double DatumAngleRad { get; set; } //260519 hbk Phase 31 D-01 — datum 1차(수평) 기준선 각도
        [System.ComponentModel.Browsable(false)] //260519 hbk Phase 31 hotfix#3
        [PropertyTools.DataAnnotations.Browsable(false)] //260519 hbk Phase 31 hotfix#3
        [Newtonsoft.Json.JsonIgnore] //260519 hbk Phase 31 hotfix#3
        public double DatumAngle2Rad { get; set; } //260519 hbk Phase 31 hotfix#3 — datum 2차(수직) 기준선 각도. X축 측정 기준.

        public ArcLineIntersectDistanceMeasurement(object owner) : base(owner) { } //260519 hbk Phase 31 D-01

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

            // ── (1) Arc 3점 에지 추출: 각 P1/P2/P3 ROI 에서 TryFitLine → 라인 중점을 arc 포인트로 사용 ──
            //260519 hbk Phase 31 D-01 — 3점 arc ROI 에서 각각 TryFitLine("All") → 중점(arcRow, arcCol)
            double ap1r1, ap1c1, ap1r2, ap1c2;
            if (!svc.TryFitLine(image,
                Arc_P1_Row, Arc_P1_Col, Arc_P1_Phi, Arc_P1_Length1, Arc_P1_Length2,
                datumTransform,
                EdgeSampleCount, EdgeTrimCount, Sigma, EdgeThreshold,
                EdgeDirection, EdgePolarity,
                out ap1r1, out ap1c1, out ap1r2, out ap1c2, out error,
                "All")) //260519 hbk Phase 31 D-01 — EdgeSelection "All" 고정 (memory feedback)
            {
                return false;
            }
            double a1Row = (ap1r1 + ap1r2) / 2.0; //260519 hbk Phase 31 D-01 — P1 에지 중점
            double a1Col = (ap1c1 + ap1c2) / 2.0; //260519 hbk Phase 31 D-01

            double ap2r1, ap2c1, ap2r2, ap2c2;
            if (!svc.TryFitLine(image,
                Arc_P2_Row, Arc_P2_Col, Arc_P2_Phi, Arc_P2_Length1, Arc_P2_Length2,
                datumTransform,
                EdgeSampleCount, EdgeTrimCount, Sigma, EdgeThreshold,
                EdgeDirection, EdgePolarity,
                out ap2r1, out ap2c1, out ap2r2, out ap2c2, out error,
                "All")) //260519 hbk Phase 31 D-01
            {
                return false;
            }
            double a2Row = (ap2r1 + ap2r2) / 2.0; //260519 hbk Phase 31 D-01 — P2 에지 중점
            double a2Col = (ap2c1 + ap2c2) / 2.0; //260519 hbk Phase 31 D-01

            double ap3r1, ap3c1, ap3r2, ap3c2;
            if (!svc.TryFitLine(image,
                Arc_P3_Row, Arc_P3_Col, Arc_P3_Phi, Arc_P3_Length1, Arc_P3_Length2,
                datumTransform,
                EdgeSampleCount, EdgeTrimCount, Sigma, EdgeThreshold,
                EdgeDirection, EdgePolarity,
                out ap3r1, out ap3c1, out ap3r2, out ap3c2, out error,
                "All")) //260519 hbk Phase 31 D-01
            {
                return false;
            }
            double a3Row = (ap3r1 + ap3r2) / 2.0; //260519 hbk Phase 31 D-01 — P3 에지 중점
            double a3Col = (ap3c1 + ap3c2) / 2.0; //260519 hbk Phase 31 D-01

            // ── (2) 3점 호 피팅: GenContourPolygonXld(3점) → FitCircleContourXld → 원중심+반지름 ──
            //260519 hbk Phase 31 D-01 — TryFitArc: T-31-09 mitigation (try/catch → false)
            double arcRow, arcCol, arcRadius;
            if (!svc.TryFitArc(
                a1Row, a1Col, a2Row, a2Col, a3Row, a3Col,
                out arcRow, out arcCol, out arcRadius, out error))
            {
                return false;
            }

            // ── (3) 라인 ROI 에서 TryFitLine → 직선 2점 추출 ──
            //260519 hbk Phase 31 D-01 — EdgeSelection "All" 고정 (memory feedback)
            double lr1, lc1, lr2, lc2;
            if (!svc.TryFitLine(image,
                Line_Row, Line_Col, Line_Phi, Line_Length1, Line_Length2,
                datumTransform,
                EdgeSampleCount, EdgeTrimCount, Sigma, EdgeThreshold,
                EdgeDirection, EdgePolarity,
                out lr1, out lc1, out lr2, out lc2, out error,
                "All")) //260519 hbk Phase 31 D-01
            {
                return false;
            }

            // ── (4) 원-직선 교점: D-10 — Line ROI 중심이 교점 선택 기준점 ──
            //260519 hbk Phase 31 D-10 — TryIntersectCircleLine: T-31-10 mitigation (disc<0 → false)
            double intRow, intCol;
            if (!VisionAlgorithmService.TryIntersectCircleLine(
                arcRow, arcCol, arcRadius,
                lr1, lc1, lr2, lc2,
                Line_Row, Line_Col, //260519 hbk Phase 31 D-10 — 라인 ROI 중심 = 교점 선택 기준
                out intRow, out intCol))
            {
                error = "Arc-line intersection failed (no intersection or degenerate line)"; //260519 hbk Phase 31 D-01
                return false;
            }

            // ── (5) 교점 → Datum 기준선 투영 거리 ──
            //260519 hbk Phase 31 D-04 — ComputeProjectionDistance 공용 헬퍼 호출
            //260519 hbk Phase 31 hotfix#3 — X축 측정은 2차(수직) 기준선, Y축은 1차(수평) 기준선
            double measureLineAngle = (MeasureAxis == "X") ? DatumAngle2Rad : DatumAngleRad; //260519 hbk Phase 31 hotfix#3
            resultValue = VisionAlgorithmService.ComputeProjectionDistance(
                intRow, intCol,
                DatumOriginRow, DatumOriginCol, measureLineAngle,
                pixelResolution, MeasureAxis); //260519 hbk Phase 31 D-01

            return true;
        }
    }
}
