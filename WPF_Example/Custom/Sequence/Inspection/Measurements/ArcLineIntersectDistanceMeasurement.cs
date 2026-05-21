//260521 hbk Phase 32 — I9/I10 SOP 재정합: 3점 호 피팅 폐기 → 2직선 교점 + Datum 거리
using System.Collections.Generic;
using HalconDotNet;
using PropertyTools.DataAnnotations;
using ReringProject.Halcon.Algorithms;
using ReringProject.Halcon.Models;

namespace ReringProject.Sequence
{
    /// <summary>
    /// EdgeA ROI(수직 에지)와 EdgeB ROI(수평 에지) 각각에서 직선을 피팅하고,
    /// 두 직선의 교점을 VisionAlgorithmService.TryIntersectLines(HALCON intersection_lines 래퍼)로 산출한다.
    /// 교점을 Datum 기준선까지의 거리(mm)로 환산한다.
    /// I9/I10(SOP): 2직선 교점 1개 → Datum C X 방향 거리. 기본 MeasureAxis="X".
    /// 직선이 평행/근접일 때 false 를 반환하여 측정값 '—' 표시 (T-32-04 mitigation).
    /// SOP 적용: P4∩P1, P5∩P3, p6∩p9, p8∩p10 (각 측정 = EdgeA/EdgeB ROI 한 쌍).
    /// </summary>
    public class ArcLineIntersectDistanceMeasurement : MeasurementBase, IDatumOriginConsumer //260521 hbk Phase 32
    {
        public override string TypeName { get { return "ArcLineIntersectDistance"; } } //260521 hbk Phase 32

        // ── EdgeA ROI (수직 에지) ──────────────────────────────────────────────────────
        //260521 hbk Phase 32 — 수직 에지 ROI (SOP P4/P5/p6/p8 측). EdgeA Phi 로 스캔 방향 지정.
        [Category("EdgeA|ROI")] //260521 hbk Phase 32
        public double EdgeA_Row { get; set; } //260521 hbk Phase 32
        public double EdgeA_Col { get; set; } //260521 hbk Phase 32
        public double EdgeA_Phi { get; set; } //260521 hbk Phase 32
        public double EdgeA_Length1 { get; set; } //260521 hbk Phase 32
        public double EdgeA_Length2 { get; set; } //260521 hbk Phase 32

        // ── EdgeB ROI (수평 에지) ──────────────────────────────────────────────────────
        //260521 hbk Phase 32 — 수평 에지 ROI (SOP P1/P3/p9/p10 측).
        [Category("EdgeB|ROI")] //260521 hbk Phase 32
        public double EdgeB_Row { get; set; } //260521 hbk Phase 32
        public double EdgeB_Col { get; set; } //260521 hbk Phase 32
        public double EdgeB_Phi { get; set; } //260521 hbk Phase 32
        public double EdgeB_Length1 { get; set; } //260521 hbk Phase 32
        public double EdgeB_Length2 { get; set; } //260521 hbk Phase 32

        // ── EdgeA Edge 파라미터 (수직 에지 — 수평 스캔 방향) ─────────────────────────
        //260521 hbk Phase 32 UAT — 공용 Edge 블록을 EdgeA/EdgeB 독립 그룹으로 분리.
        //  EdgeA = 수직 에지 검출 → 스캔 방향 기본값 "LtoR" (수평 스캔).
        [Category("EdgeA|Edge")] //260521 hbk Phase 32 UAT
        public int EdgeA_Threshold { get; set; } = 10; //260521 hbk Phase 32 UAT
        public double EdgeA_Sigma { get; set; } = 1.0; //260521 hbk Phase 32 UAT
        public int EdgeA_SampleCount { get; set; } = 20; //260521 hbk Phase 32 UAT
        public int EdgeA_TrimCount { get; set; } = 10; //260521 hbk Phase 32 UAT
        [ItemsSourceProperty(nameof(EdgePolarityList))] //260521 hbk Phase 32 UAT
        public string EdgeA_Polarity { get; set; } = "DarkToLight"; //260521 hbk Phase 32 UAT
        [ItemsSourceProperty(nameof(EdgeDirectionList))] //260521 hbk Phase 32 UAT
        public string EdgeA_Direction { get; set; } = "LtoR"; //260521 hbk Phase 32 UAT — 수직 에지 → 수평 스캔
        [ItemsSourceProperty(nameof(EdgeSelectionList))] //260521 hbk Phase 32 UAT
        public string EdgeA_Selection { get; set; } = "All"; //260521 hbk Phase 32 UAT — EdgeA 에지 선택 (First/Last/All)

        // ── EdgeB Edge 파라미터 (수평 에지 — 수직 스캔 방향) ─────────────────────────
        //260521 hbk Phase 32 UAT — EdgeB = 수평 에지 검출 → 스캔 방향 기본값 "TtoB" (수직 스캔).
        [Category("EdgeB|Edge")] //260521 hbk Phase 32 UAT
        public int EdgeB_Threshold { get; set; } = 10; //260521 hbk Phase 32 UAT
        public double EdgeB_Sigma { get; set; } = 1.0; //260521 hbk Phase 32 UAT
        public int EdgeB_SampleCount { get; set; } = 20; //260521 hbk Phase 32 UAT
        public int EdgeB_TrimCount { get; set; } = 10; //260521 hbk Phase 32 UAT
        [ItemsSourceProperty(nameof(EdgePolarityList))] //260521 hbk Phase 32 UAT
        public string EdgeB_Polarity { get; set; } = "DarkToLight"; //260521 hbk Phase 32 UAT
        [ItemsSourceProperty(nameof(EdgeDirectionList))] //260521 hbk Phase 32 UAT
        public string EdgeB_Direction { get; set; } = "TtoB"; //260521 hbk Phase 32 UAT — 수평 에지 → 수직 스캔
        [ItemsSourceProperty(nameof(EdgeSelectionList))] //260521 hbk Phase 32 UAT
        public string EdgeB_Selection { get; set; } = "All"; //260521 hbk Phase 32 UAT — EdgeB 에지 선택 (First/Last/All)

        //260521 hbk Phase 32 — PropertyGrid ComboBox 옵션 래퍼 (EdgeA/EdgeB 두 그룹에서 공유)
        [PropertyTools.DataAnnotations.Browsable(false)] //260521 hbk Phase 32
        public List<string> EdgeDirectionList { get { return EdgeOptionLists.Directions; } } //260521 hbk Phase 32
        [PropertyTools.DataAnnotations.Browsable(false)] //260521 hbk Phase 32
        public List<string> EdgePolarityList { get { return EdgeOptionLists.FAIPolarities; } } //260521 hbk Phase 32
        [PropertyTools.DataAnnotations.Browsable(false)] //260521 hbk Phase 32 UAT
        public List<string> EdgeSelectionList { get { return EdgeOptionLists.Selections; } } //260521 hbk Phase 32 UAT — First/Last/All

        // ── MeasureAxis ───────────────────────────────────────────────────────────────
        //260521 hbk Phase 32 — I9/I10 = Datum C X 방향이므로 기본값 "X". 독립 카테고리로 이동.
        [Category("Measurement|Measure")] //260521 hbk Phase 32 UAT — 기존 "Edge" 에서 "Measure" 로 분리; Tab 프리픽스 추가 (클래스명 기본탭 제거)
        [ItemsSourceProperty(nameof(MeasureAxisList))] //260521 hbk Phase 32
        public string MeasureAxis { get; set; } = "X"; //260521 hbk Phase 32 (I9/I10 = Datum C X 방향)
        [PropertyTools.DataAnnotations.Browsable(false)] //260521 hbk Phase 32
        public List<string> MeasureAxisList { get { return new List<string> { "X", "Y" }; } } //260521 hbk Phase 32

        // ── IDatumOriginConsumer transient 필드 ──────────────────────────────────────
        //260521 hbk Phase 32 — datum 교점 좌표 runtime 주입 전용. PropertyGrid 미표시, JSON 직렬화 제외.
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
        //260521 hbk Phase 32 — IDatumOriginConsumer 확장. ArcLineIntersect 미사용 (E2 전용) — 주입만 받음.
        [System.ComponentModel.Browsable(false)] //260521 hbk Phase 32
        [PropertyTools.DataAnnotations.Browsable(false)] //260521 hbk Phase 32
        [Newtonsoft.Json.JsonIgnore] //260521 hbk Phase 32
        public double DatumDetectedCircleRow { get; set; } //260521 hbk Phase 32
        [System.ComponentModel.Browsable(false)] //260521 hbk Phase 32
        [PropertyTools.DataAnnotations.Browsable(false)] //260521 hbk Phase 32
        [Newtonsoft.Json.JsonIgnore] //260521 hbk Phase 32
        public double DatumDetectedCircleCol { get; set; } //260521 hbk Phase 32

        public ArcLineIntersectDistanceMeasurement(object owner) : base(owner) { } //260521 hbk Phase 32

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

            // (1) EdgeA ROI — 수직 에지 직선 피팅. EdgeSelection "All" 고정 (memory feedback — 단일 MeasurePos 금지)
            //260521 hbk Phase 32 UAT — EdgeA_* 독립 파라미터 사용 (수직 에지, 수평 스캔)
            double a1r1, a1c1, a1r2, a1c2; //260521 hbk Phase 32
            if (!svc.TryFitLine(image,
                EdgeA_Row, EdgeA_Col, EdgeA_Phi, EdgeA_Length1, EdgeA_Length2,
                datumTransform,
                EdgeA_SampleCount, EdgeA_TrimCount, EdgeA_Sigma, EdgeA_Threshold, //260521 hbk Phase 32 UAT
                EdgeA_Direction, EdgeA_Polarity, //260521 hbk Phase 32 UAT
                out a1r1, out a1c1, out a1r2, out a1c2, out error,
                EdgeA_Selection)) //260521 hbk Phase 32 UAT — 사용자 선택값 (기본값 "All")
            {
                return false;
            }

            // (2) EdgeB ROI — 수평 에지 직선 피팅
            //260521 hbk Phase 32 UAT — EdgeB_* 독립 파라미터 사용 (수평 에지, 수직 스캔)
            double b1r1, b1c1, b1r2, b1c2; //260521 hbk Phase 32
            if (!svc.TryFitLine(image,
                EdgeB_Row, EdgeB_Col, EdgeB_Phi, EdgeB_Length1, EdgeB_Length2,
                datumTransform,
                EdgeB_SampleCount, EdgeB_TrimCount, EdgeB_Sigma, EdgeB_Threshold, //260521 hbk Phase 32 UAT
                EdgeB_Direction, EdgeB_Polarity, //260521 hbk Phase 32 UAT
                out b1r1, out b1c1, out b1r2, out b1c2, out error,
                EdgeB_Selection)) //260521 hbk Phase 32 UAT — 사용자 선택값 (기본값 "All")
            {
                return false;
            }

            // (3) 두 직선 교점 — intersection_lines 래퍼. 평행/근접 시 false (측정값 '—', T-32-04 mitigation)
            double intRow, intCol; //260521 hbk Phase 32
            if (!VisionAlgorithmService.TryIntersectLines(
                a1r1, a1c1, a1r2, a1c2,
                b1r1, b1c1, b1r2, b1c2,
                out intRow, out intCol)) //260521 hbk Phase 32
            {
                error = "Line intersection failed (parallel or near-parallel edges)"; //260521 hbk Phase 32
                return false;
            }

            // (4) 교점 → Datum 기준선 투영 거리. X측정=2차(수직)선, Y측정=1차(수평)선
            double measureLineAngle = (MeasureAxis == "X") ? DatumAngle2Rad : DatumAngleRad; //260521 hbk Phase 32
            resultValue = VisionAlgorithmService.ComputeProjectionDistance(
                intRow, intCol,
                DatumOriginRow, DatumOriginCol, measureLineAngle,
                pixelResolution, MeasureAxis); //260521 hbk Phase 32

            // overlay — 알고리즘이 이미 계산한 변수만 재사용. HALCON 재호출 없음. //260521 hbk Phase 32 I9/I10-overlay
            // 1) FAI-Edge1 = EdgeA 피팅 라인 (HalconDisplayService 녹/적 분기 + Action_FAIMeasurement -OK/-NG suffix)
            overlays.Add(new EdgeInspectionOverlay //260521 hbk Phase 32 I9/I10-overlay
            {
                RoiId = "FAI-Edge1", //260521 hbk Phase 32 I9/I10-overlay
                LineRow1 = a1r1, LineColumn1 = a1c1, //260521 hbk Phase 32 I9/I10-overlay
                LineRow2 = a1r2, LineColumn2 = a1c2, //260521 hbk Phase 32 I9/I10-overlay
                Points = new List<EdgeInspectionPoint> //260521 hbk Phase 32 I9/I10-overlay
                {
                    new EdgeInspectionPoint { Row = (a1r1 + a1r2) / 2.0, Column = (a1c1 + a1c2) / 2.0 } //260521 hbk Phase 32 I9/I10-overlay — EdgeA 중점
                }
            }); //260521 hbk Phase 32 I9/I10-overlay
            // 2) FAI-Edge2 = EdgeB 피팅 라인 (FAI-Edge 로 시작하므로 동일 색상 분기 적용)
            overlays.Add(new EdgeInspectionOverlay //260521 hbk Phase 32 I9/I10-overlay
            {
                RoiId = "FAI-Edge2", //260521 hbk Phase 32 I9/I10-overlay
                LineRow1 = b1r1, LineColumn1 = b1c1, //260521 hbk Phase 32 I9/I10-overlay
                LineRow2 = b1r2, LineColumn2 = b1c2, //260521 hbk Phase 32 I9/I10-overlay
                Points = new List<EdgeInspectionPoint> //260521 hbk Phase 32 I9/I10-overlay
                {
                    new EdgeInspectionPoint { Row = (b1r1 + b1r2) / 2.0, Column = (b1c1 + b1c2) / 2.0 } //260521 hbk Phase 32 I9/I10-overlay — EdgeB 중점
                }
            }); //260521 hbk Phase 32 I9/I10-overlay
            // 3) FAI-Intersection = 교점 마커 (점 마커: LineRow1==LineRow2, LineColumn1==LineColumn2)
            overlays.Add(new EdgeInspectionOverlay //260521 hbk Phase 32 I9/I10-overlay
            {
                RoiId = "FAI-Intersection", //260521 hbk Phase 32 I9/I10-overlay
                LineRow1 = intRow, LineColumn1 = intCol, //260521 hbk Phase 32 I9/I10-overlay
                LineRow2 = intRow, LineColumn2 = intCol, //260521 hbk Phase 32 I9/I10-overlay — 점 마커 (길이 0 라인)
                Points = new List<EdgeInspectionPoint> //260521 hbk Phase 32 I9/I10-overlay
                {
                    new EdgeInspectionPoint { Row = intRow, Column = intCol } //260521 hbk Phase 32 I9/I10-overlay — 교점 X마커
                }
            }); //260521 hbk Phase 32 I9/I10-overlay

            return true;
        }
    }
}
