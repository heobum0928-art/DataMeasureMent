//260519 hbk Phase 31 D-11 — E2: 다단계 기하 체인(CL1~CL3 + La/Lb) → Datum B 기준 각도
using System.Collections.Generic;
using HalconDotNet;
using PropertyTools.DataAnnotations;
using ReringProject.Halcon.Algorithms;
using ReringProject.Halcon.Models;

namespace ReringProject.Sequence
{
    /// <summary>
    /// CL1~CL3 원 피팅 + La/Lb 라인 피팅 → midline Lc → Pa=Lc∩CL2, Pb=Lc∩CL3
    /// → Pc=midpoint(Pa,Pb) → line Ld=(CL1 중심 Pd, Pc) → AngleLineLine(Ld, Datum B 기준선).
    /// E2(SOP): CompoundAngle — 다단계 기하 체인을 통해 Datum B 기준 각도(degree)를 산출.
    /// D-09: 중간 산출물(Pc, Pa, Pb 등) 내부 계산, 사용자 ROI 티칭만 노출.
    /// D-11: E2(각도) = 별도 타입. E9/E10(거리)는 CompoundCenterC/BDistanceMeasurement 참조.
    /// </summary>
    public class CompoundAngleMeasurement : MeasurementBase, IDatumOriginConsumer //260519 hbk Phase 31 D-11
    {
        public override string TypeName { get { return "CompoundAngle"; } } //260519 hbk Phase 31 D-11

        // ── CL1 ROI (원중심 Pd 추출용) ────────────────────────────────────────────────
        [Category("CL1|ROI")] //260519 hbk Phase 31 D-11
        public double Cl1_Row { get; set; } //260519 hbk Phase 31 D-11
        public double Cl1_Col { get; set; } //260519 hbk Phase 31 D-11
        public double Cl1_Radius { get; set; } //260519 hbk Phase 31 D-11

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
        //260519 hbk Phase 31 hotfix#3 — IDatumOriginConsumer 2차 각도 (CompoundAngle 은 각도 측정 — 1차선 기준 유지, 속성만 구현)
        [System.ComponentModel.Browsable(false)] //260519 hbk Phase 31 hotfix#3
        [PropertyTools.DataAnnotations.Browsable(false)] //260519 hbk Phase 31 hotfix#3
        [Newtonsoft.Json.JsonIgnore] //260519 hbk Phase 31 hotfix#3
        public double DatumAngle2Rad { get; set; } //260519 hbk Phase 31 hotfix#3

        public CompoundAngleMeasurement(object owner) : base(owner) { } //260519 hbk Phase 31 D-11

        /// <summary>
        /// CL2/CL3 + La/Lb 기하 체인으로 중점 Pc 를 계산한다.
        /// midline Lc = La+Lb 방향 평균 + 두 라인 4점의 중점.
        /// Pa = Lc∩CL2, Pb = Lc∩CL3, Pc = midpoint(Pa, Pb).
        /// T-31-08 mitigation: 각 단계 실패 시 즉시 return false + error 전파.
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

            var svc = new VisionAlgorithmService();

            // (1) CL1 TryFindCircle → 원중심 Pd
            //260519 hbk Phase 31 D-11 — CL1 은 E2 전용 (E9/E10 에는 없음)
            double cl1R, cl1C, cl1Rad;
            if (!svc.TryFindCircle(image, Cl1_Row, Cl1_Col, Cl1_Radius, datumTransform,
                Sigma, EdgeThreshold, EdgePolarity,
                out cl1R, out cl1C, out cl1Rad, out error))
            {
                return false;
            }

            // (2) TryComputeChainPoint → Pc
            //260519 hbk Phase 31 D-09 — 기하 체인 내부 계산 캡슐화 (T-31-08 mitigation)
            double pcRow, pcCol;
            if (!TryComputeChainPoint(image, datumTransform, out pcRow, out pcCol, out error))
            {
                return false;
            }

            // (3) E2 전용: Datum B 기준선 2점 구성
            //260519 hbk Phase 31 D-11 — DatumOriginRow/Col 중심 ±200px, 방향 = DatumAngleRad (EdgeToLineAngleMeasurement 동일 패턴)
            double sinT = System.Math.Sin(DatumAngleRad);
            double cosT = System.Math.Cos(DatumAngleRad);
            double daR1 = DatumOriginRow - 200.0 * sinT; //260519 hbk Phase 31 D-11
            double daC1 = DatumOriginCol - 200.0 * cosT; //260519 hbk Phase 31 D-11
            double daR2 = DatumOriginRow + 200.0 * sinT; //260519 hbk Phase 31 D-11
            double daC2 = DatumOriginCol + 200.0 * cosT; //260519 hbk Phase 31 D-11

            // (4) AngleLineLine(Ld vs Datum B 기준선): line Ld = (Pd=cl1Center → Pc)
            //260519 hbk Phase 31 D-11 — 각도(degree), pixelResolution 미적용 (각도 타입)
            resultValue = VisionAlgorithmService.AngleLineLine(
                cl1R, cl1C, pcRow, pcCol, //260519 hbk Phase 31 D-11 — line Ld = Pd → Pc
                daR1, daC1, daR2, daC2); //260519 hbk Phase 31 D-11 — Datum B 기준선

            return true;
        }
    }
}
