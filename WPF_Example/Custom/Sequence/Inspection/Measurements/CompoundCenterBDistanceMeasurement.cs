//260521 hbk Phase 32 — E10: 공통 컨투어 알고리즘(canny→union→LargestRect) → Datum B Y 거리
using System.Collections.Generic;
using HalconDotNet;
using PropertyTools.DataAnnotations;
using ReringProject.Halcon.Algorithms;
using ReringProject.Halcon.Models;

namespace ReringProject.Sequence
{
    /// <summary>
    /// Rect ROI 1개 → 공통 컨투어 알고리즘(canny→union_adjacent→LargestRect) → LargestRect 중심 추출.
    /// E10(SOP): CompoundCenterBDistance — LargestRect 중심 → Datum B Y 방향 거리(mm).
    /// Phase 32 재작성: CL2/CL3 원피팅 + La/Lb 라인 + midline 교점 체인 폐기.
    /// CompoundCenterCDistanceMeasurement 와 구조 완전 동일.
    /// 차이: TypeName="CompoundCenterBDistance", MeasureAxis 기본값 "Y" (Datum B Y 방향).
    /// </summary>
    public class CompoundCenterBDistanceMeasurement : MeasurementBase, IDatumOriginConsumer //260521 hbk Phase 32
    {
        public override string TypeName { get { return "CompoundCenterBDistance"; } } //260521 hbk Phase 32

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
        //260521 hbk Phase 32 — E10 = Datum B Y 방향이므로 기본값 "Y" (Pitfall 8 방지)
        [Category("Edge")] //260521 hbk Phase 32
        [ItemsSourceProperty(nameof(MeasureAxisList))] //260521 hbk Phase 32
        public string MeasureAxis { get; set; } = "Y"; //260521 hbk Phase 32 (E10 = Datum B Y 방향)
        [PropertyTools.DataAnnotations.Browsable(false)] //260521 hbk Phase 32
        public List<string> MeasureAxisList { get { return new List<string> { "Y", "X" }; } } //260521 hbk Phase 32

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

        public CompoundCenterBDistanceMeasurement(object owner) : base(owner) { } //260521 hbk Phase 32

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
            double measureLineAngle = (MeasureAxis == "X") ? DatumAngle2Rad : DatumAngleRad; //260521 hbk Phase 32 E10-overlay — 유지
            double footRow, footCol; //260521 hbk Phase 32 E10-overlay — FAI-DistLine 수선의 발 좌표
            bool footOk; //260521 hbk Phase 32 E10-overlay
            resultValue = VisionAlgorithmService.ComputeProjectionDistance(
                centerRow, centerCol,
                DatumOriginRow, DatumOriginCol, measureLineAngle,
                pixelResolution, MeasureAxis,
                out footRow, out footCol, out footOk); //260521 hbk Phase 32 E10-overlay — foot 반환 오버로드로 교체 (수치 결과 동일)

            // overlay — 이미 계산한 변수만 재사용. HALCON 재호출 없음. //260521 hbk Phase 32 E10-overlay
            // 1) FAI-Edge1 = LargestRect 중심 점 마커
            overlays.Add(new EdgeInspectionOverlay //260521 hbk Phase 32 E10-overlay
            {
                RoiId = "FAI-Edge1", //260521 hbk Phase 32 E10-overlay — HalconDisplayService 녹/적 분기 + Action_FAIMeasurement suffix
                LineRow1 = centerRow, LineColumn1 = centerCol, //260521 hbk Phase 32 E10-overlay
                LineRow2 = centerRow, LineColumn2 = centerCol, //260521 hbk Phase 32 E10-overlay — 점 마커
                Points = new List<EdgeInspectionPoint> //260521 hbk Phase 32 E10-overlay
                {
                    new EdgeInspectionPoint { Row = centerRow, Column = centerCol } //260521 hbk Phase 32 E10-overlay
                }
            }); //260521 hbk Phase 32 E10-overlay
            // 2) FAI-DistLine = LargestRect 중심 → datum 기준선 수선의 발 (수직 드롭선, cyan)
            if (footOk) //260521 hbk Phase 32 E10-overlay — projection 실패 시 수치 0 이지만 라인은 skip
            {
                overlays.Add(new EdgeInspectionOverlay //260521 hbk Phase 32 E10-overlay
                {
                    RoiId = "FAI-DistLine", //260521 hbk Phase 32 E10-overlay — HalconDisplayService cyan 분기
                    LineRow1 = footRow, LineColumn1 = footCol, //260521 hbk Phase 32 E10-overlay — 수선의 발 (datum 기준선 위)
                    LineRow2 = centerRow, LineColumn2 = centerCol, //260521 hbk Phase 32 E10-overlay — LargestRect 중심
                    Points = new List<EdgeInspectionPoint> //260521 hbk Phase 32 E10-overlay — 양 끝점 X마커
                    {
                        new EdgeInspectionPoint { Row = footRow, Column = footCol }, //260521 hbk Phase 32 E10-overlay
                        new EdgeInspectionPoint { Row = centerRow, Column = centerCol } //260521 hbk Phase 32 E10-overlay
                    }
                }); //260521 hbk Phase 32 E10-overlay
            } //260521 hbk Phase 32 E10-overlay

            return true;
        }
    }
}
