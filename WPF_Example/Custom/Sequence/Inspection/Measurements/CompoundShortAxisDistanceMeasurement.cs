using System.Collections.Generic;
using HalconDotNet;
using PropertyTools.DataAnnotations;
using ReringProject.Halcon.Algorithms;
using ReringProject.Halcon.Models;

namespace ReringProject.Sequence
{
    /// <summary>
    /// Rect ROI 1개 → 공통 컨투어 알고리즘(canny→union_adjacent→LargestRect XLD) → 긴변 fit_line → 단축 폭(mm) 측정.
    /// CompoundShortAxisDistance — 짧은 변 거리, 공차 0.600±0.030.
    /// 알고리즘: get_contour_xld 코너 → Edge1Len vs Edge2Len 비교로 긴변 선택 → fit_line_contour_xld('tukey') 로 refined Phi →
    /// 중심 통과 phi+π/2 측정선 → intersection_contours_xld(measureLine, LargestRect, 'all') → 교점 2개 거리 = 결과값.
    /// Datum 비의존 (IDatumOriginConsumer 미구현).
    /// </summary>
    public class CompoundShortAxisDistanceMeasurement : MeasurementBase
    {
        public override string TypeName { get { return "CompoundShortAxisDistance"; } }

        // ── Rect ROI ─────────────────────────────────────────────────────────────────
        // 공통 컨투어 알고리즘 입력 Rect ROI
        [Category("Rect|ROI")]
        public double Rect_Row { get; set; }
        public double Rect_Col { get; set; }
        public double Rect_Phi { get; set; }
        public double Rect_Length1 { get; set; }
        public double Rect_Length2 { get; set; }

        // ── Contour 파라미터 (PropertyGrid 사용자 편집) ───────────────────────────────
        [Category("Contour")]
        public double CannyAlpha { get; set; } = 1.0; // edges_sub_pix canny alpha
        public int CannyLow { get; set; } = 20; // canny low threshold
        public int CannyHigh { get; set; } = 40; // canny high threshold
        public double UnionDistance { get; set; } = 700.0; // union_adjacent_contours_xld 거리

        // ── Measure (PhiPerp 측정선 sweep 반길이) ────────────────────────────────────
        // 중심 통과 측정선 양 끝까지 거리 (사각형 너머까지 보장 → 교점 산출 안전)
        [Category("Measure")]
        public double CrossLen { get; set; } = 500.0; // 측정선 반길이 px

        public CompoundShortAxisDistanceMeasurement(object owner) : base(owner) { }

        public override bool TryExecute(
            HImage image,
            HTuple datumTransform,
            double pixelResolution,
            out double resultValue,
            out string error,
            out List<EdgeInspectionOverlay> overlays)
        {
            resultValue = 0;
            error = null;
            overlays = new List<EdgeInspectionOverlay>();

            var svc = new VisionAlgorithmService();

            // VisionAlgorithmService.TryFindShortAxisIntersections 로 단일 호출 위임:
            //   - canny → union → LargestRect XLD → get_contour_xld → 긴변 선택 → fit_line refined Phi → 측정선 → contour intersection
            //   - HObject 라이프사이클은 서비스 내부에서 try/finally 로 모두 처리
            double centerRow, centerCol, phi, length1, length2;
            double e1ar, e1ac, e1br, e1bc; // 긴변 1 양 끝
            double e2ar, e2ac, e2br, e2bc; // 긴변 2 양 끝
            double mRow1, mCol1, mRow2, mCol2; // 측정선 양 끝
            double int1Row, int1Col, int2Row, int2Col; // 교점 2개

            if (!svc.TryFindShortAxisIntersections(image,
                Rect_Row, Rect_Col, Rect_Phi, Rect_Length1, Rect_Length2,
                datumTransform,
                CannyAlpha, CannyLow, CannyHigh, UnionDistance,
                CrossLen,
                out centerRow, out centerCol,
                out phi, out length1, out length2,
                out e1ar, out e1ac, out e1br, out e1bc,
                out e2ar, out e2ac, out e2br, out e2bc,
                out mRow1, out mCol1, out mRow2, out mCol2,
                out int1Row, out int1Col, out int2Row, out int2Col,
                out error))
            {
                return false;
            }

            // 결과값 = 두 교점 간 euclidean 거리 × pixelResolution (≈ 2 × length2 × pixelResolution, contour subpixel 정확도)
            double dRow = int2Row - int1Row;
            double dCol = int2Col - int1Col;
            double distPx = System.Math.Sqrt(dRow * dRow + dCol * dCol);
            resultValue = distPx * pixelResolution;

            // 오버레이 6개 — return true 직전 ADDITIVE, HALCON 재호출 0 (ArcLineIntersect 패턴)
            // 긴변 1 (라인 + 중점 마커)
            double edge1MidRow = (e1ar + e1br) / 2.0;
            double edge1MidCol = (e1ac + e1bc) / 2.0;
            overlays.Add(new EdgeInspectionOverlay
            {
                RoiId = "FAI-LongEdge1",
                LineRow1 = e1ar, LineColumn1 = e1ac,
                LineRow2 = e1br, LineColumn2 = e1bc,
                Points = new List<EdgeInspectionPoint>
                {
                    new EdgeInspectionPoint { Row = edge1MidRow, Column = edge1MidCol }
                }
            });
            // 긴변 2
            double edge2MidRow = (e2ar + e2br) / 2.0;
            double edge2MidCol = (e2ac + e2bc) / 2.0;
            overlays.Add(new EdgeInspectionOverlay
            {
                RoiId = "FAI-LongEdge2",
                LineRow1 = e2ar, LineColumn1 = e2ac,
                LineRow2 = e2br, LineColumn2 = e2bc,
                Points = new List<EdgeInspectionPoint>
                {
                    new EdgeInspectionPoint { Row = edge2MidRow, Column = edge2MidCol }
                }
            });
            // 측정선 (중심 지나는 phi+π/2 방향)
            overlays.Add(new EdgeInspectionOverlay
            {
                RoiId = "FAI-MeasureLine",
                LineRow1 = mRow1, LineColumn1 = mCol1,
                LineRow2 = mRow2, LineColumn2 = mCol2,
                Points = new List<EdgeInspectionPoint>
                {
                    new EdgeInspectionPoint { Row = centerRow, Column = centerCol }
                }
            });
            // 교점 1 (점 마커)
            overlays.Add(new EdgeInspectionOverlay
            {
                RoiId = "FAI-Intersection1",
                LineRow1 = int1Row, LineColumn1 = int1Col,
                LineRow2 = int1Row, LineColumn2 = int1Col, // 점 마커
                Points = new List<EdgeInspectionPoint>
                {
                    new EdgeInspectionPoint { Row = int1Row, Column = int1Col }
                }
            });
            // 교점 2 (점 마커)
            overlays.Add(new EdgeInspectionOverlay
            {
                RoiId = "FAI-Intersection2",
                LineRow1 = int2Row, LineColumn1 = int2Col,
                LineRow2 = int2Row, LineColumn2 = int2Col, // 점 마커
                Points = new List<EdgeInspectionPoint>
                {
                    new EdgeInspectionPoint { Row = int2Row, Column = int2Col }
                }
            });
            // 측정 거리선 — 교점1 ↔ 교점2
            overlays.Add(new EdgeInspectionOverlay
            {
                RoiId = "FAI-DistLine",
                LineRow1 = int1Row, LineColumn1 = int1Col,
                LineRow2 = int2Row, LineColumn2 = int2Col,
                Points = new List<EdgeInspectionPoint>
                {
                    new EdgeInspectionPoint { Row = int1Row, Column = int1Col },
                    new EdgeInspectionPoint { Row = int2Row, Column = int2Col }
                }
            });

            return true;
        }
    }
}
