//260521 hbk Phase 32 — E2: 공통 컨투어 알고리즘(canny→union→LargestRect) + DatumC 검출 원중심 → DatumB 기준 각도
using System.Collections.Generic;
using HalconDotNet;
using PropertyTools.DataAnnotations;
using ReringProject.Halcon.Algorithms;
using ReringProject.Halcon.Models;

namespace ReringProject.Sequence
{
    /// <summary>
    /// Rect ROI 1개 → 공통 컨투어 알고리즘(canny→union_adjacent→LargestRect) → LargestRect 중심 추출.
    /// DatumC 티칭 시 검출되는 원(B1 홀) 중심을 DatumDetectedCircleRow/Col 로 주입받음.
    /// E2(SOP): CompoundAngle — 대각선(LargestRect 중심 ↔ DatumC 검출 원중심) vs DatumB 기준선 각도(degree).
    /// Phase 32 재작성: CL1~CL3 원피팅 + La/Lb 라인 + midline 교점 체인 전면 폐기.
    /// DatumC 검출 원중심 미주입(0,0) 시 명시적 error 반환 — 안전 종결.
    /// </summary>
    public class CompoundAngleMeasurement : MeasurementBase, IDatumOriginConsumer //260521 hbk Phase 32
    {
        public override string TypeName { get { return "CompoundAngle"; } } //260521 hbk Phase 32

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
        //260521 hbk Phase 32 — IDatumOriginConsumer 2차 각도. CompoundAngle 은 각도 측정 — 1차선 기준 유지, 속성만 구현.
        [System.ComponentModel.Browsable(false)] //260521 hbk Phase 32
        [PropertyTools.DataAnnotations.Browsable(false)] //260521 hbk Phase 32
        [Newtonsoft.Json.JsonIgnore] //260521 hbk Phase 32
        public double DatumAngle2Rad { get; set; } //260521 hbk Phase 32
        //260521 hbk Phase 32 — IDatumOriginConsumer 확장. E2 는 DatumC 검출 원(B1 홀) 중심을 실제로 사용.
        [System.ComponentModel.Browsable(false)] //260521 hbk Phase 32
        [PropertyTools.DataAnnotations.Browsable(false)] //260521 hbk Phase 32
        [Newtonsoft.Json.JsonIgnore] //260521 hbk Phase 32
        public double DatumDetectedCircleRow { get; set; } //260521 hbk Phase 32
        [System.ComponentModel.Browsable(false)] //260521 hbk Phase 32
        [PropertyTools.DataAnnotations.Browsable(false)] //260521 hbk Phase 32
        [Newtonsoft.Json.JsonIgnore] //260521 hbk Phase 32
        public double DatumDetectedCircleCol { get; set; } //260521 hbk Phase 32

        public CompoundAngleMeasurement(object owner) : base(owner) { } //260521 hbk Phase 32

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

            // (2) DatumC 검출 원중심 주입값 검증 — E2 는 CircleTwoHorizontal datum 전제
            if (DatumDetectedCircleRow == 0.0 && DatumDetectedCircleCol == 0.0) //260521 hbk Phase 32
            {
                error = "DatumC detected circle center not injected (requires CircleTwoHorizontal datum)"; //260521 hbk Phase 32
                return false;
            }

            // (3) DatumB 기준선 2점 (DatumOrigin 중심 ±200px, 방향 = DatumAngleRad)
            double sinT = System.Math.Sin(DatumAngleRad); //260521 hbk Phase 32
            double cosT = System.Math.Cos(DatumAngleRad); //260521 hbk Phase 32
            double daR1 = DatumOriginRow - 200.0 * sinT; //260521 hbk Phase 32
            double daC1 = DatumOriginCol - 200.0 * cosT; //260521 hbk Phase 32
            double daR2 = DatumOriginRow + 200.0 * sinT; //260521 hbk Phase 32
            double daC2 = DatumOriginCol + 200.0 * cosT; //260521 hbk Phase 32

            // (4) 대각선 라인 (LargestRect 중심 ↔ DatumC 검출 원중심) vs DatumB 기준선 각도 1개
            resultValue = VisionAlgorithmService.AngleLineLine(
                centerRow, centerCol, DatumDetectedCircleRow, DatumDetectedCircleCol, //260521 hbk Phase 32 — 대각선
                daR1, daC1, daR2, daC2); //260521 hbk Phase 32 — DatumB 기준선

            // overlay — TryFindLargestContourRect/DatumDetectedCircle* 가 이미 계산한 변수 재사용. //260521 hbk Phase 32 E2-overlay
            // 1) FAI-Edge1 = LargestRect 중심 마커 (점 마커: LineRow1==LineRow2)
            overlays.Add(new EdgeInspectionOverlay //260521 hbk Phase 32 E2-overlay
            {
                RoiId = "FAI-Edge1", //260521 hbk Phase 32 E2-overlay — HalconDisplayService 녹/적 분기 + Action_FAIMeasurement suffix
                LineRow1 = centerRow, LineColumn1 = centerCol, //260521 hbk Phase 32 E2-overlay
                LineRow2 = centerRow, LineColumn2 = centerCol, //260521 hbk Phase 32 E2-overlay — 점 마커
                Points = new List<EdgeInspectionPoint> //260521 hbk Phase 32 E2-overlay
                {
                    new EdgeInspectionPoint { Row = centerRow, Column = centerCol } //260521 hbk Phase 32 E2-overlay
                }
            }); //260521 hbk Phase 32 E2-overlay
            // 2) FAI-DiagLine = 대각선 (LargestRect 중심 ↔ DatumC 검출 원중심). AngleLineLine 의 첫 번째 라인과 동일.
            overlays.Add(new EdgeInspectionOverlay //260521 hbk Phase 32 E2-overlay
            {
                RoiId = "FAI-DiagLine", //260521 hbk Phase 32 E2-overlay
                LineRow1 = centerRow, LineColumn1 = centerCol, //260521 hbk Phase 32 E2-overlay — LargestRect 중심
                LineRow2 = DatumDetectedCircleRow, LineColumn2 = DatumDetectedCircleCol, //260521 hbk Phase 32 E2-overlay — DatumC 검출 원중심
                Points = new List<EdgeInspectionPoint> //260521 hbk Phase 32 E2-overlay — 양 끝점 X마커
                {
                    new EdgeInspectionPoint { Row = centerRow, Column = centerCol }, //260521 hbk Phase 32 E2-overlay
                    new EdgeInspectionPoint { Row = DatumDetectedCircleRow, Column = DatumDetectedCircleCol } //260521 hbk Phase 32 E2-overlay
                }
            }); //260521 hbk Phase 32 E2-overlay

            return true;
        }
    }
}
