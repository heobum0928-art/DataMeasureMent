//260523 hbk Phase 32 — E3 단축 환원 + 교점 기반 알고리즘 재작성
using System.Collections.Generic;
using HalconDotNet;
using PropertyTools.DataAnnotations;
using ReringProject.Halcon.Algorithms;
using ReringProject.Halcon.Models;

namespace ReringProject.Sequence
{
    /// <summary>
    /// Rect ROI 1개 → 공통 컨투어 알고리즘(canny→union_adjacent→LargestRect) → 단축 폭(mm) 측정.
    /// E3(SOP p.50): CompoundShortAxisDistance — 짧은 변 거리, 공차 0.600±0.030.
    /// 알고리즘: 긴변 2개(phi 방향 직선) ↔ 중심 지나는 phi+π/2 측정선 교점 2개의 거리 = 2×length2×pixelResolution.
    /// Datum 비의존 (IDatumOriginConsumer 미구현).
    /// </summary>
    public class CompoundShortAxisDistanceMeasurement : MeasurementBase //260523 hbk Phase 32 — E3 단축 환원
    {
        public override string TypeName { get { return "CompoundShortAxisDistance"; } } //260523 hbk Phase 32 — E3 단축 환원

        // ── Rect ROI ─────────────────────────────────────────────────────────────────
        //260523 hbk Phase 32 — 공통 컨투어 알고리즘 입력 Rect ROI
        [Category("Rect|ROI")] //260523 hbk Phase 32 — E3 단축 환원
        public double Rect_Row { get; set; } //260523 hbk Phase 32 — E3 단축 환원
        public double Rect_Col { get; set; } //260523 hbk Phase 32 — E3 단축 환원
        public double Rect_Phi { get; set; } //260523 hbk Phase 32 — E3 단축 환원
        public double Rect_Length1 { get; set; } //260523 hbk Phase 32 — E3 단축 환원
        public double Rect_Length2 { get; set; } //260523 hbk Phase 32 — E3 단축 환원

        // ── Contour 파라미터 (PropertyGrid 사용자 편집) ───────────────────────────────
        //260523 hbk Phase 32 — 공통 컨투어 알고리즘 파라미터 (PropertyGrid 사용자 편집)
        [Category("Contour")] //260523 hbk Phase 32 — E3 단축 환원
        public double CannyAlpha { get; set; } = 1.0; //260523 hbk Phase 32 — E3 단축 환원: edges_sub_pix canny alpha
        public int CannyLow { get; set; } = 20; //260523 hbk Phase 32 — E3 단축 환원: canny low threshold
        public int CannyHigh { get; set; } = 40; //260523 hbk Phase 32 — E3 단축 환원: canny high threshold
        public double UnionDistance { get; set; } = 700.0; //260523 hbk Phase 32 — E3 단축 환원: union_adjacent_contours_xld 거리

        public CompoundShortAxisDistanceMeasurement(object owner) : base(owner) { } //260523 hbk Phase 32 — E3 단축 환원

        public override bool TryExecute( //260523 hbk Phase 32 — E3 교점 기반 알고리즘
            HImage image,
            HTuple datumTransform,
            double pixelResolution,
            out double resultValue,
            out string error,
            out List<EdgeInspectionOverlay> overlays)
        {
            resultValue = 0;
            error = null;
            overlays = new List<EdgeInspectionOverlay>(); //260523 hbk Phase 32 — E3 교점 기반 알고리즘

            var svc = new VisionAlgorithmService(); //260523 hbk Phase 32 — E3 교점 기반 알고리즘

            // Step 1: 공통 컨투어 알고리즘 → LargestRect 중심/각도/장단축
            double centerRow, centerCol, phi, length1, length2; //260523 hbk Phase 32 — E3 교점 기반 알고리즘
            if (!svc.TryFindLargestContourRect(image,
                Rect_Row, Rect_Col, Rect_Phi, Rect_Length1, Rect_Length2,
                datumTransform,
                CannyAlpha, CannyLow, CannyHigh, UnionDistance,
                out centerRow, out centerCol, out phi, out length1, out length2, out error)) //260523 hbk Phase 32 — E3 교점 기반 알고리즘
            {
                return false;
            }

            // Step 2: 긴 변 2개의 양 끝점 계산.
            // HALCON SmallestRectangle2 컨벤션: phi = 첫번째 축 방향 (length1 = phi 방향 반길이).
            // 긴 변(장축) 방향 = phi, 긴 변의 길이 = 2×length1, 긴 변 간의 수직거리(=단축 폭) = 2×length2.
            double sinPhi = System.Math.Sin(phi); //260523 hbk Phase 32 — E3 교점 기반 알고리즘
            double cosPhi = System.Math.Cos(phi); //260523 hbk Phase 32 — E3 교점 기반 알고리즘
            double sinPerp = System.Math.Sin(phi + System.Math.PI / 2.0); //260523 hbk Phase 32 — E3 교점 기반 알고리즘: 단축 방향 sin (= cosPhi)
            double cosPerp = System.Math.Cos(phi + System.Math.PI / 2.0); //260523 hbk Phase 32 — E3 교점 기반 알고리즘: 단축 방향 cos (= -sinPhi)
            // 긴 변 1 중점 = 중심에서 단축 방향(perp) 으로 -length2
            double edge1MidRow = centerRow - length2 * sinPerp; //260523 hbk Phase 32 — E3 교점 기반 알고리즘
            double edge1MidCol = centerCol - length2 * cosPerp; //260523 hbk Phase 32 — E3 교점 기반 알고리즘
            // 긴 변 2 중점 = 중심에서 단축 방향(perp) 으로 +length2
            double edge2MidRow = centerRow + length2 * sinPerp; //260523 hbk Phase 32 — E3 교점 기반 알고리즘
            double edge2MidCol = centerCol + length2 * cosPerp; //260523 hbk Phase 32 — E3 교점 기반 알고리즘
            // 긴 변 1 양 끝점 = 중점 ± length1 × 장축 방향(phi)
            double edge1aRow = edge1MidRow - length1 * sinPhi; //260523 hbk Phase 32 — E3 교점 기반 알고리즘
            double edge1aCol = edge1MidCol - length1 * cosPhi; //260523 hbk Phase 32 — E3 교점 기반 알고리즘
            double edge1bRow = edge1MidRow + length1 * sinPhi; //260523 hbk Phase 32 — E3 교점 기반 알고리즘
            double edge1bCol = edge1MidCol + length1 * cosPhi; //260523 hbk Phase 32 — E3 교점 기반 알고리즘
            // 긴 변 2 양 끝점
            double edge2aRow = edge2MidRow - length1 * sinPhi; //260523 hbk Phase 32 — E3 교점 기반 알고리즘
            double edge2aCol = edge2MidCol - length1 * cosPhi; //260523 hbk Phase 32 — E3 교점 기반 알고리즘
            double edge2bRow = edge2MidRow + length1 * sinPhi; //260523 hbk Phase 32 — E3 교점 기반 알고리즘
            double edge2bCol = edge2MidCol + length1 * cosPhi; //260523 hbk Phase 32 — E3 교점 기반 알고리즘

            // Step 3: 측정선 = 중심을 지나는 phi+π/2 방향 직선. sweep 길이는 length2 × 1.5 (긴변 너머까지 보장 → 교점 안전).
            double measureExtent = length2 * 1.5; //260523 hbk Phase 32 — E3 교점 기반 알고리즘
            double measureRow1 = centerRow - measureExtent * sinPerp; //260523 hbk Phase 32 — E3 교점 기반 알고리즘
            double measureCol1 = centerCol - measureExtent * cosPerp; //260523 hbk Phase 32 — E3 교점 기반 알고리즘
            double measureRow2 = centerRow + measureExtent * sinPerp; //260523 hbk Phase 32 — E3 교점 기반 알고리즘
            double measureCol2 = centerCol + measureExtent * cosPerp; //260523 hbk Phase 32 — E3 교점 기반 알고리즘

            // Step 4: TryIntersectLines × 2 (측정선 ↔ 긴변1, 측정선 ↔ 긴변2).
            double int1Row, int1Col; //260523 hbk Phase 32 — E3 교점 기반 알고리즘
            if (!VisionAlgorithmService.TryIntersectLines(
                measureRow1, measureCol1, measureRow2, measureCol2,
                edge1aRow, edge1aCol, edge1bRow, edge1bCol,
                out int1Row, out int1Col)) //260523 hbk Phase 32 — E3 교점 기반 알고리즘
            {
                error = "긴변1↔측정선 교점 산출 실패 (평행/근접)"; //260523 hbk Phase 32 — E3 교점 기반 알고리즘
                return false;
            }
            double int2Row, int2Col; //260523 hbk Phase 32 — E3 교점 기반 알고리즘
            if (!VisionAlgorithmService.TryIntersectLines(
                measureRow1, measureCol1, measureRow2, measureCol2,
                edge2aRow, edge2aCol, edge2bRow, edge2bCol,
                out int2Row, out int2Col)) //260523 hbk Phase 32 — E3 교점 기반 알고리즘
            {
                error = "긴변2↔측정선 교점 산출 실패 (평행/근접)"; //260523 hbk Phase 32 — E3 교점 기반 알고리즘
                return false;
            }

            // Step 5: 결과값 = 두 교점 간 euclidean 거리 × pixelResolution.
            // 수학적 등가: distance(int1, int2) = 2 × length2 (긴변 평행 + 측정선이 중심+수직 통과 → 교점이 중심±length2 위치).
            double dRow = int2Row - int1Row; //260523 hbk Phase 32 — E3 교점 기반 알고리즘
            double dCol = int2Col - int1Col; //260523 hbk Phase 32 — E3 교점 기반 알고리즘
            double distPx = System.Math.Sqrt(dRow * dRow + dCol * dCol); //260523 hbk Phase 32 — E3 교점 기반 알고리즘
            resultValue = distPx * pixelResolution; //260523 hbk Phase 32 — E3 교점 기반 알고리즘

            // Step 6: 오버레이 6개 — return true 직전 ADDITIVE, HALCON 재호출 0 (ArcLineIntersect 패턴).
            // 긴변 1 (라인 + 중점 마커)
            overlays.Add(new EdgeInspectionOverlay //260523 hbk Phase 32 — E3 교점 기반 알고리즘
            {
                RoiId = "FAI-LongEdge1", //260523 hbk Phase 32 — E3 교점 기반 알고리즘
                LineRow1 = edge1aRow, LineColumn1 = edge1aCol, //260523 hbk Phase 32 — E3 교점 기반 알고리즘
                LineRow2 = edge1bRow, LineColumn2 = edge1bCol, //260523 hbk Phase 32 — E3 교점 기반 알고리즘
                Points = new List<EdgeInspectionPoint> //260523 hbk Phase 32 — E3 교점 기반 알고리즘
                {
                    new EdgeInspectionPoint { Row = edge1MidRow, Column = edge1MidCol } //260523 hbk Phase 32 — E3 교점 기반 알고리즘
                }
            }); //260523 hbk Phase 32 — E3 교점 기반 알고리즘
            // 긴변 2
            overlays.Add(new EdgeInspectionOverlay //260523 hbk Phase 32 — E3 교점 기반 알고리즘
            {
                RoiId = "FAI-LongEdge2", //260523 hbk Phase 32 — E3 교점 기반 알고리즘
                LineRow1 = edge2aRow, LineColumn1 = edge2aCol, //260523 hbk Phase 32 — E3 교점 기반 알고리즘
                LineRow2 = edge2bRow, LineColumn2 = edge2bCol, //260523 hbk Phase 32 — E3 교점 기반 알고리즘
                Points = new List<EdgeInspectionPoint> //260523 hbk Phase 32 — E3 교점 기반 알고리즘
                {
                    new EdgeInspectionPoint { Row = edge2MidRow, Column = edge2MidCol } //260523 hbk Phase 32 — E3 교점 기반 알고리즘
                }
            }); //260523 hbk Phase 32 — E3 교점 기반 알고리즘
            // 측정선 (중심 지나는 phi+π/2 방향)
            overlays.Add(new EdgeInspectionOverlay //260523 hbk Phase 32 — E3 교점 기반 알고리즘
            {
                RoiId = "FAI-MeasureLine", //260523 hbk Phase 32 — E3 교점 기반 알고리즘
                LineRow1 = measureRow1, LineColumn1 = measureCol1, //260523 hbk Phase 32 — E3 교점 기반 알고리즘
                LineRow2 = measureRow2, LineColumn2 = measureCol2, //260523 hbk Phase 32 — E3 교점 기반 알고리즘
                Points = new List<EdgeInspectionPoint> //260523 hbk Phase 32 — E3 교점 기반 알고리즘
                {
                    new EdgeInspectionPoint { Row = centerRow, Column = centerCol } //260523 hbk Phase 32 — E3 교점 기반 알고리즘
                }
            }); //260523 hbk Phase 32 — E3 교점 기반 알고리즘
            // 교점 1 (점 마커: LineRow1==LineRow2, LineColumn1==LineColumn2)
            overlays.Add(new EdgeInspectionOverlay //260523 hbk Phase 32 — E3 교점 기반 알고리즘
            {
                RoiId = "FAI-Intersection1", //260523 hbk Phase 32 — E3 교점 기반 알고리즘
                LineRow1 = int1Row, LineColumn1 = int1Col, //260523 hbk Phase 32 — E3 교점 기반 알고리즘
                LineRow2 = int1Row, LineColumn2 = int1Col, //260523 hbk Phase 32 — E3 교점 기반 알고리즘 — 점 마커
                Points = new List<EdgeInspectionPoint> //260523 hbk Phase 32 — E3 교점 기반 알고리즘
                {
                    new EdgeInspectionPoint { Row = int1Row, Column = int1Col } //260523 hbk Phase 32 — E3 교점 기반 알고리즘
                }
            }); //260523 hbk Phase 32 — E3 교점 기반 알고리즘
            // 교점 2 (점 마커)
            overlays.Add(new EdgeInspectionOverlay //260523 hbk Phase 32 — E3 교점 기반 알고리즘
            {
                RoiId = "FAI-Intersection2", //260523 hbk Phase 32 — E3 교점 기반 알고리즘
                LineRow1 = int2Row, LineColumn1 = int2Col, //260523 hbk Phase 32 — E3 교점 기반 알고리즘
                LineRow2 = int2Row, LineColumn2 = int2Col, //260523 hbk Phase 32 — E3 교점 기반 알고리즘 — 점 마커
                Points = new List<EdgeInspectionPoint> //260523 hbk Phase 32 — E3 교점 기반 알고리즘
                {
                    new EdgeInspectionPoint { Row = int2Row, Column = int2Col } //260523 hbk Phase 32 — E3 교점 기반 알고리즘
                }
            }); //260523 hbk Phase 32 — E3 교점 기반 알고리즘
            // 측정 거리선 — 교점1 ↔ 교점2 (이 선의 길이 × pixelResolution = resultValue mm)
            overlays.Add(new EdgeInspectionOverlay //260523 hbk Phase 32 — E3 교점 기반 알고리즘
            {
                RoiId = "FAI-DistLine", //260523 hbk Phase 32 — E3 교점 기반 알고리즘
                LineRow1 = int1Row, LineColumn1 = int1Col, //260523 hbk Phase 32 — E3 교점 기반 알고리즘
                LineRow2 = int2Row, LineColumn2 = int2Col, //260523 hbk Phase 32 — E3 교점 기반 알고리즘
                Points = new List<EdgeInspectionPoint> //260523 hbk Phase 32 — E3 교점 기반 알고리즘
                {
                    new EdgeInspectionPoint { Row = int1Row, Column = int1Col }, //260523 hbk Phase 32 — E3 교점 기반 알고리즘
                    new EdgeInspectionPoint { Row = int2Row, Column = int2Col } //260523 hbk Phase 32 — E3 교점 기반 알고리즘
                }
            }); //260523 hbk Phase 32 — E3 교점 기반 알고리즘

            return true;
        }
    }
}
