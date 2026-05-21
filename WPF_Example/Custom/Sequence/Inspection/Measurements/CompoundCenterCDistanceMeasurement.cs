//260521 hbk Phase 32 — E9: 공통 컨투어 알고리즘(canny→union→LargestRect) → Datum C X 거리
using System.Collections.Generic;
using HalconDotNet;
using PropertyTools.DataAnnotations;
using ReringProject.Halcon.Algorithms;
using ReringProject.Halcon.Models;

namespace ReringProject.Sequence
{
    /// <summary>
    /// Rect ROI 1개 → 공통 컨투어 알고리즘(canny→union_adjacent→LargestRect) → LargestRect 중심 추출.
    /// E9(SOP): CompoundCenterCDistance — LargestRect 중심 → Datum C X 방향 거리(mm).
    /// Phase 32 재작성: CL2/CL3 원피팅 + La/Lb 라인 + midline 교점 체인 폐기.
    /// VisionAlgorithmService.TryFindLargestContourRect 공용 컨투어 서비스 호출.
    /// MeasureAxis 기본값 "X" (Datum C X 방향).
    /// </summary>
    public class CompoundCenterCDistanceMeasurement : MeasurementBase, IDatumOriginConsumer //260521 hbk Phase 32
    {
        public override string TypeName { get { return "CompoundCenterCDistance"; } } //260521 hbk Phase 32

        // ── Rect ROI ─────────────────────────────────────────────────────────────────
        //260521 hbk Phase 32 — 공통 컨투어 알고리즘 입력 Rect ROI
        [Category("Rect|ROI")] //260521 hbk Phase 32
        public double Rect_Row { get; set; } //260521 hbk Phase 32
        public double Rect_Col { get; set; } //260521 hbk Phase 32
        public double Rect_Phi { get; set; } //260521 hbk Phase 32
        public double Rect_Length1 { get; set; } //260521 hbk Phase 32
        public double Rect_Length2 { get; set; } //260521 hbk Phase 32

        // ── Edge 파라미터 ─────────────────────────────────────────────────────────────
        [Category("Edge")] //260521 hbk Phase 32
        public int EdgeThreshold { get; set; } = 10; //260521 hbk Phase 32
        public double Sigma { get; set; } = 1.0; //260521 hbk Phase 32
        public int EdgeSampleCount { get; set; } = 20; //260521 hbk Phase 32
        public int EdgeTrimCount { get; set; } = 10; //260521 hbk Phase 32
        [ItemsSourceProperty(nameof(EdgePolarityList))] //260521 hbk Phase 32
        public string EdgePolarity { get; set; } = "DarkToLight"; //260521 hbk Phase 32
        [ItemsSourceProperty(nameof(EdgeDirectionList))] //260521 hbk Phase 32
        public string EdgeDirection { get; set; } = "TtoB"; //260521 hbk Phase 32

        //260521 hbk Phase 32 — PropertyGrid ComboBox 옵션 래퍼
        [PropertyTools.DataAnnotations.Browsable(false)] //260521 hbk Phase 32
        public List<string> EdgeDirectionList { get { return EdgeOptionLists.Directions; } } //260521 hbk Phase 32
        [PropertyTools.DataAnnotations.Browsable(false)] //260521 hbk Phase 32
        public List<string> EdgePolarityList { get { return EdgeOptionLists.FAIPolarities; } } //260521 hbk Phase 32

        // ── Contour 파라미터 (PropertyGrid 사용자 편집) ───────────────────────────────
        //260521 hbk Phase 32 — 공통 컨투어 알고리즘 파라미터 (PropertyGrid 사용자 편집)
        [Category("Contour")] //260521 hbk Phase 32
        public double CannyAlpha { get; set; } = 1.0; //260521 hbk Phase 32 — edges_sub_pix canny alpha
        public int CannyLow { get; set; } = 20; //260521 hbk Phase 32 — canny low threshold
        public int CannyHigh { get; set; } = 40; //260521 hbk Phase 32 — canny high threshold
        public double UnionDistance { get; set; } = 700.0; //260521 hbk Phase 32 — union_adjacent_contours_xld 거리

        // ── MeasureAxis ───────────────────────────────────────────────────────────────
        //260521 hbk Phase 32 — E9 = Datum C X 방향이므로 기본값 "X"
        [Category("Edge")] //260521 hbk Phase 32
        [ItemsSourceProperty(nameof(MeasureAxisList))] //260521 hbk Phase 32
        public string MeasureAxis { get; set; } = "X"; //260521 hbk Phase 32 (E9 = Datum C X 방향)
        [PropertyTools.DataAnnotations.Browsable(false)] //260521 hbk Phase 32
        public List<string> MeasureAxisList { get { return new List<string> { "X", "Y" }; } } //260521 hbk Phase 32

        // ── IDatumOriginConsumer transient 필드 ──────────────────────────────────────
        //260521 hbk Phase 32 — datum 교점 좌표 runtime 주입 전용. ArcEdgeDistanceMeasurement 동일 패턴.
        [System.ComponentModel.Browsable(false)] //260521 hbk Phase 32
        [PropertyTools.DataAnnotations.Browsable(false)] //260521 hbk Phase 32
        [Newtonsoft.Json.JsonIgnore] //260521 hbk Phase 32
        public double DatumOriginRow { get; set; } //260521 hbk Phase 32
        [System.ComponentModel.Browsable(false)] //260521 hbk Phase 32
        [PropertyTools.DataAnnotations.Browsable(false)] //260521 hbk Phase 32
        [Newtonsoft.Json.JsonIgnore] //260521 hbk Phase 32
        public double DatumOriginCol { get; set; } //260521 hbk Phase 32
        [System.ComponentModel.Browsable(false)] //260521 hbk Phase 32
        [PropertyTools.DataAnnotations.Browsable(false)] //260521 hbk Phase 32
        [Newtonsoft.Json.JsonIgnore] //260521 hbk Phase 32
        public double DatumAngleRad { get; set; } //260521 hbk Phase 32 — datum 1차(수평) 기준선 각도
        [System.ComponentModel.Browsable(false)] //260521 hbk Phase 32
        [PropertyTools.DataAnnotations.Browsable(false)] //260521 hbk Phase 32
        [Newtonsoft.Json.JsonIgnore] //260521 hbk Phase 32
        public double DatumAngle2Rad { get; set; } //260521 hbk Phase 32 — datum 2차(수직) 기준선 각도. X축 측정 기준.
        //260521 hbk Phase 32 — IDatumOriginConsumer 확장. CompoundCenter 미사용 (E2 전용) — 주입만 받음.
        [System.ComponentModel.Browsable(false)] //260521 hbk Phase 32
        [PropertyTools.DataAnnotations.Browsable(false)] //260521 hbk Phase 32
        [Newtonsoft.Json.JsonIgnore] //260521 hbk Phase 32
        public double DatumDetectedCircleRow { get; set; } //260521 hbk Phase 32
        [System.ComponentModel.Browsable(false)] //260521 hbk Phase 32
        [PropertyTools.DataAnnotations.Browsable(false)] //260521 hbk Phase 32
        [Newtonsoft.Json.JsonIgnore] //260521 hbk Phase 32
        public double DatumDetectedCircleCol { get; set; } //260521 hbk Phase 32

        public CompoundCenterCDistanceMeasurement(object owner) : base(owner) { } //260521 hbk Phase 32

        public override bool TryExecute( //260521 hbk Phase 32
            HImage image,
            HTuple datumTransform,
            double pixelResolution,
            out double resultValue,
            out string error,
            out List<EdgeInspectionOverlay> overlays)
        {
            resultValue = 0;
            error = null;
            overlays = new List<EdgeInspectionOverlay>(); //260521 hbk Phase 32

            var svc = new VisionAlgorithmService(); //260521 hbk Phase 32

            // (1) 공통 컨투어 알고리즘 → LargestRect 중심
            double centerRow, centerCol, phi, len1, len2; //260521 hbk Phase 32
            if (!svc.TryFindLargestContourRect(image,
                Rect_Row, Rect_Col, Rect_Phi, Rect_Length1, Rect_Length2,
                datumTransform,
                CannyAlpha, CannyLow, CannyHigh, UnionDistance,
                out centerRow, out centerCol, out phi, out len1, out len2, out error)) //260521 hbk Phase 32
            {
                return false;
            }

            // (2) LargestRect 중심 → Datum 거리. X측정=2차(수직)선, Y측정=1차(수평)선
            double measureLineAngle = (MeasureAxis == "X") ? DatumAngle2Rad : DatumAngleRad; //260521 hbk Phase 32
            resultValue = VisionAlgorithmService.ComputeProjectionDistance(
                centerRow, centerCol,
                DatumOriginRow, DatumOriginCol, measureLineAngle,
                pixelResolution, MeasureAxis); //260521 hbk Phase 32

            return true;
        }
    }
}
