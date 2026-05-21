//260519 hbk Phase 31 D-11 — E10: 다단계 기하 체인(CL2/CL3 + La/Lb) → Datum B Y 거리
using System.Collections.Generic;
using HalconDotNet;
using PropertyTools.DataAnnotations;
using ReringProject.Halcon.Algorithms;
using ReringProject.Halcon.Models;

namespace ReringProject.Sequence
{
    /// <summary>
    /// CL2/CL3 원 피팅 + La/Lb 라인 피팅 → midline Lc → Pa=Lc∩CL2, Pb=Lc∩CL3
    /// → Pc=midpoint(Pa,Pb) → ComputeProjectionDistance(Pc, DatumB).
    /// E10(SOP): CompoundCenterBDistance — 기하 체인으로 Pc 산출 후 Datum B Y 방향 거리(mm).
    /// CompoundCenterCDistanceMeasurement 와 구조 완전 동일.
    /// 차이: TypeName="CompoundCenterBDistance", MeasureAxis 기본값 "Y" (Datum B Y 방향, D-07).
    /// D-11 타입 분리 근거: E10 거리 단위 mm, 기준축 Datum B Y (E9 와 다른 참조축).
    /// </summary>
    public class CompoundCenterBDistanceMeasurement : MeasurementBase, IDatumOriginConsumer //260519 hbk Phase 31 D-11
    {
        public override string TypeName { get { return "CompoundCenterBDistance"; } } //260519 hbk Phase 31 D-11

        // ── CL2 ROI ───────────────────────────────────────────────────────────────────
        [Category("CL2|ROI")] //260519 hbk Phase 31 D-11
        public double Cl2_Row { get; set; } //260519 hbk Phase 31 D-11
        public double Cl2_Col { get; set; } //260519 hbk Phase 31 D-11
        public double Cl2_Radius { get; set; } //260519 hbk Phase 31 D-11

        // ── CL3 ROI ───────────────────────────────────────────────────────────────────
        [Category("CL3|ROI")] //260519 hbk Phase 31 D-11
        public double Cl3_Row { get; set; } //260519 hbk Phase 31 D-11
        public double Cl3_Col { get; set; } //260519 hbk Phase 31 D-11
        public double Cl3_Radius { get; set; } //260519 hbk Phase 31 D-11

        // ── La ROI ────────────────────────────────────────────────────────────────────
        [Category("La|ROI")] //260519 hbk Phase 31 D-11
        public double La_Row { get; set; } //260519 hbk Phase 31 D-11
        public double La_Col { get; set; } //260519 hbk Phase 31 D-11
        public double La_Phi { get; set; } //260519 hbk Phase 31 D-11
        public double La_Length1 { get; set; } //260519 hbk Phase 31 D-11
        public double La_Length2 { get; set; } //260519 hbk Phase 31 D-11

        // ── Lb ROI ────────────────────────────────────────────────────────────────────
        [Category("Lb|ROI")] //260519 hbk Phase 31 D-11
        public double Lb_Row { get; set; } //260519 hbk Phase 31 D-11
        public double Lb_Col { get; set; } //260519 hbk Phase 31 D-11
        public double Lb_Phi { get; set; } //260519 hbk Phase 31 D-11
        public double Lb_Length1 { get; set; } //260519 hbk Phase 31 D-11
        public double Lb_Length2 { get; set; } //260519 hbk Phase 31 D-11

        // ── Edge 파라미터 ─────────────────────────────────────────────────────────────
        [Category("Edge")] //260519 hbk Phase 31 D-11
        public int EdgeThreshold { get; set; } = 10; //260519 hbk Phase 31 D-11
        public double Sigma { get; set; } = 1.0; //260519 hbk Phase 31 D-11
        public int EdgeSampleCount { get; set; } = 20; //260519 hbk Phase 31 D-11
        public int EdgeTrimCount { get; set; } = 10; //260519 hbk Phase 31 D-11
        [ItemsSourceProperty(nameof(EdgePolarityList))] //260519 hbk Phase 31 D-11
        public string EdgePolarity { get; set; } = "DarkToLight"; //260519 hbk Phase 31 D-11
        [ItemsSourceProperty(nameof(EdgeDirectionList))] //260519 hbk Phase 31 D-11
        public string EdgeDirection { get; set; } = "TtoB"; //260519 hbk Phase 31 D-11

        //260519 hbk Phase 31 D-11 — PropertyGrid ComboBox 옵션 래퍼
        [PropertyTools.DataAnnotations.Browsable(false)] //260519 hbk Phase 31 D-11
        public List<string> EdgeDirectionList { get { return EdgeOptionLists.Directions; } } //260519 hbk Phase 31 D-11
        [PropertyTools.DataAnnotations.Browsable(false)] //260519 hbk Phase 31 D-11
        public List<string> EdgePolarityList { get { return EdgeOptionLists.FAIPolarities; } } //260519 hbk Phase 31 D-11

        // ── MeasureAxis ───────────────────────────────────────────────────────────────
        //260519 hbk Phase 31 D-11 — E10 = Datum B Y 방향이므로 기본값 "Y" (D-07/D-08 확정, Pitfall 8 방지)
        [Category("Edge")] //260519 hbk Phase 31 D-11
        [ItemsSourceProperty(nameof(MeasureAxisList))] //260519 hbk Phase 31 D-11
        public string MeasureAxis { get; set; } = "Y"; //260519 hbk Phase 31 D-11 (E10 = Datum B Y 방향)
        [PropertyTools.DataAnnotations.Browsable(false)] //260519 hbk Phase 31 D-11
        public List<string> MeasureAxisList { get { return new List<string> { "Y", "X" }; } } //260519 hbk Phase 31 D-11

        // ── IDatumOriginConsumer 3 transient 필드 ────────────────────────────────────
        //260519 hbk Phase 31 D-11 — datum 교점 좌표 runtime 주입 전용. EdgeToLineDistanceMeasurement.cs L69~80 동일 패턴.
        [System.ComponentModel.Browsable(false)] //260519 hbk Phase 31 D-11
        [PropertyTools.DataAnnotations.Browsable(false)] //260519 hbk Phase 31 D-11
        [Newtonsoft.Json.JsonIgnore] //260519 hbk Phase 31 D-11
        public double DatumOriginRow { get; set; } //260519 hbk Phase 31 D-11
        [System.ComponentModel.Browsable(false)] //260519 hbk Phase 31 D-11
        [PropertyTools.DataAnnotations.Browsable(false)] //260519 hbk Phase 31 D-11
        [Newtonsoft.Json.JsonIgnore] //260519 hbk Phase 31 D-11
        public double DatumOriginCol { get; set; } //260519 hbk Phase 31 D-11
        [System.ComponentModel.Browsable(false)] //260519 hbk Phase 31 D-11
        [PropertyTools.DataAnnotations.Browsable(false)] //260519 hbk Phase 31 D-11
        [Newtonsoft.Json.JsonIgnore] //260519 hbk Phase 31 D-11
        public double DatumAngleRad { get; set; } //260519 hbk Phase 31 D-11 — datum 1차(수평) 기준선 각도
        [System.ComponentModel.Browsable(false)] //260519 hbk Phase 31 hotfix#3
        [PropertyTools.DataAnnotations.Browsable(false)] //260519 hbk Phase 31 hotfix#3
        [Newtonsoft.Json.JsonIgnore] //260519 hbk Phase 31 hotfix#3
        public double DatumAngle2Rad { get; set; } //260519 hbk Phase 31 hotfix#3 — datum 2차(수직) 기준선 각도. X축 측정 기준.
        //260521 hbk Phase 32 — IDatumOriginConsumer 확장 stub. Plan 04 재작성 시 교체.
        [System.ComponentModel.Browsable(false)] //260521 hbk Phase 32
        [PropertyTools.DataAnnotations.Browsable(false)] //260521 hbk Phase 32
        [Newtonsoft.Json.JsonIgnore] //260521 hbk Phase 32
        public double DatumDetectedCircleRow { get; set; } //260521 hbk Phase 32
        [System.ComponentModel.Browsable(false)] //260521 hbk Phase 32
        [PropertyTools.DataAnnotations.Browsable(false)] //260521 hbk Phase 32
        [Newtonsoft.Json.JsonIgnore] //260521 hbk Phase 32
        public double DatumDetectedCircleCol { get; set; } //260521 hbk Phase 32

        public CompoundCenterBDistanceMeasurement(object owner) : base(owner) { } //260519 hbk Phase 31 D-11

        /// <summary>
        /// CL2/CL3 + La/Lb 기하 체인으로 중점 Pc 를 계산한다.
        /// midline Lc = La+Lb 방향 평균 + 두 라인 4점의 중점.
        /// Pa = Lc∩CL2, Pb = Lc∩CL3, Pc = midpoint(Pa, Pb).
        /// T-31-08 mitigation: 각 단계 실패 시 즉시 return false + error 전파.
        /// D-09: 각 타입이 독립 헬퍼 보유 (ROI 공유 없음).
        /// </summary>
        private bool TryComputeChainPoint( //260519 hbk Phase 31 D-09 — compound 기하 체인 내부 계산
            HImage image, HTuple datumTransform,
            out double pcRow, out double pcCol, out string error)
        {
            pcRow = pcCol = 0;
            error = null;
            var svc = new VisionAlgorithmService();

            // CL2 피팅
            double cl2R, cl2C, cl2Rad;
            if (!svc.TryFindCircle(image, Cl2_Row, Cl2_Col, Cl2_Radius, datumTransform,
                Sigma, EdgeThreshold, EdgePolarity, out cl2R, out cl2C, out cl2Rad, out error))
                return false;

            // CL3 피팅
            double cl3R, cl3C, cl3Rad;
            if (!svc.TryFindCircle(image, Cl3_Row, Cl3_Col, Cl3_Radius, datumTransform,
                Sigma, EdgeThreshold, EdgePolarity, out cl3R, out cl3C, out cl3Rad, out error))
                return false;

            // La 라인 피팅
            double la1R, la1C, la2R, la2C;
            if (!svc.TryFitLine(image, La_Row, La_Col, La_Phi, La_Length1, La_Length2,
                datumTransform, EdgeSampleCount, EdgeTrimCount, Sigma, EdgeThreshold,
                EdgeDirection, EdgePolarity,
                out la1R, out la1C, out la2R, out la2C, out error,
                "All")) //260519 hbk Phase 31 D-09 — EdgeSelection "All" 고정 (memory feedback)
                return false;

            // Lb 라인 피팅
            double lb1R, lb1C, lb2R, lb2C;
            if (!svc.TryFitLine(image, Lb_Row, Lb_Col, Lb_Phi, Lb_Length1, Lb_Length2,
                datumTransform, EdgeSampleCount, EdgeTrimCount, Sigma, EdgeThreshold,
                EdgeDirection, EdgePolarity,
                out lb1R, out lb1C, out lb2R, out lb2C, out error,
                "All")) //260519 hbk Phase 31 D-09
                return false;

            // midline Lc: 중점 = La/Lb 4점 평균, 방향 = La+Lb 단위벡터 합
            //260519 hbk Phase 31 D-09 — Pitfall 4 방지: 방향벡터 단위벡터 합으로 정규화
            double lcMidR = (la1R + la2R + lb1R + lb2R) / 4.0; //260519 hbk Phase 31 D-09
            double lcMidC = (la1C + la2C + lb1C + lb2C) / 4.0; //260519 hbk Phase 31 D-09
            double laLen = System.Math.Sqrt((la2R - la1R) * (la2R - la1R) + (la2C - la1C) * (la2C - la1C));
            double lbLen = System.Math.Sqrt((lb2R - lb1R) * (lb2R - lb1R) + (lb2C - lb1C) * (lb2C - lb1C));
            double laLenInv = 1.0 / System.Math.Max(1e-9, laLen); //260519 hbk Phase 31 D-09
            double lbLenInv = 1.0 / System.Math.Max(1e-9, lbLen); //260519 hbk Phase 31 D-09
            double dirR = (la2R - la1R) * laLenInv + (lb2R - lb1R) * lbLenInv; //260519 hbk Phase 31 D-09
            double dirC = (la2C - la1C) * laLenInv + (lb2C - lb1C) * lbLenInv; //260519 hbk Phase 31 D-09
            double lcR1 = lcMidR - 200.0 * dirR;
            double lcC1 = lcMidC - 200.0 * dirC;
            double lcR2 = lcMidR + 200.0 * dirR;
            double lcC2 = lcMidC + 200.0 * dirC;

            // Pa = Lc ∩ CL2
            double paR, paC;
            if (!VisionAlgorithmService.TryIntersectCircleLine(
                cl2R, cl2C, cl2Rad, lcR1, lcC1, lcR2, lcC2,
                Cl2_Row, Cl2_Col, //260519 hbk Phase 31 D-10 — CL2 ROI 중심 = 교점 선택 기준
                out paR, out paC))
            {
                error = "Lc∩CL2 intersection failed"; //260519 hbk Phase 31 D-09
                return false;
            }

            // Pb = Lc ∩ CL3
            double pbR, pbC;
            if (!VisionAlgorithmService.TryIntersectCircleLine(
                cl3R, cl3C, cl3Rad, lcR1, lcC1, lcR2, lcC2,
                Cl3_Row, Cl3_Col, //260519 hbk Phase 31 D-10 — CL3 ROI 중심 = 교점 선택 기준
                out pbR, out pbC))
            {
                error = "Lc∩CL3 intersection failed"; //260519 hbk Phase 31 D-09
                return false;
            }

            // Pc = midpoint(Pa, Pb)
            pcRow = (paR + pbR) / 2.0; //260519 hbk Phase 31 D-09
            pcCol = (paC + pbC) / 2.0; //260519 hbk Phase 31 D-09
            return true;
        }

        public override bool TryExecute( //260519 hbk Phase 31 D-11
            HImage image,
            HTuple datumTransform,
            double pixelResolution,
            out double resultValue,
            out string error,
            out List<EdgeInspectionOverlay> overlays)
        {
            resultValue = 0;
            error = null;
            overlays = new List<EdgeInspectionOverlay>(); //260519 hbk Phase 31 D-11

            // TryComputeChainPoint → Pc
            //260519 hbk Phase 31 D-09 — 기하 체인 내부 계산 캡슐화 (T-31-08 mitigation)
            double pcRow, pcCol;
            if (!TryComputeChainPoint(image, datumTransform, out pcRow, out pcCol, out error))
            {
                return false;
            }

            // Pc → Datum B Y 방향 거리(mm)
            //260519 hbk Phase 31 D-04 — ComputeProjectionDistance 공용 헬퍼 호출 (D-07: E10 = Datum B Y 거리)
            //260519 hbk Phase 31 hotfix#3 — X축 측정은 2차(수직) 기준선, Y축은 1차(수평) 기준선
            double measureLineAngle = (MeasureAxis == "X") ? DatumAngle2Rad : DatumAngleRad; //260519 hbk Phase 31 hotfix#3
            resultValue = VisionAlgorithmService.ComputeProjectionDistance(
                pcRow, pcCol,
                DatumOriginRow, DatumOriginCol, measureLineAngle,
                pixelResolution, MeasureAxis); //260519 hbk Phase 31 D-11

            return true;
        }
    }
}
