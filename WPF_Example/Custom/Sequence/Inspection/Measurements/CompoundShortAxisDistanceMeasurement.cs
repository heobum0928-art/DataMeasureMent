//260523 hbk Phase 32 — E3 단축 환원 + 교점 기반 알고리즘 재작성
//260523 hbk Phase 32 — E3 reference algorithm: VisionAlgorithmService.TryFindShortAxisIntersections 로 위임 (HALCON 호출 전면 캡슐화)
using System.Collections.Generic;
using HalconDotNet;
using PropertyTools.DataAnnotations;
using ReringProject.Halcon.Algorithms;
using ReringProject.Halcon.Models;

namespace ReringProject.Sequence
{
    /// <summary>
    /// Rect ROI 1개 → 공통 컨투어 알고리즘(canny→union_adjacent→LargestRect XLD) → 긴변 fit_line → 단축 폭(mm) 측정.
    /// E3(SOP p.50): CompoundShortAxisDistance — 짧은 변 거리, 공차 0.600±0.030.
    /// 알고리즘(사용자 reference): get_contour_xld 코너 → Edge1Len vs Edge2Len 비교로 긴변 선택 → fit_line_contour_xld('tukey') 로 refined Phi →
    /// 중심 통과 phi+π/2 측정선 → intersection_contours_xld(measureLine, LargestRect, 'all') → 교점 2개 거리 = 결과값.
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

        // ── Measure (PhiPerp 측정선 sweep 반길이) ────────────────────────────────────
        //260523 hbk Phase 32 — E3 reference algorithm: 중심 통과 측정선 양 끝까지 거리 (사각형 너머까지 보장 → 교점 산출 안전)
        [Category("Measure")] //260523 hbk Phase 32 — E3 reference algorithm
        public double CrossLen { get; set; } = 500.0; //260523 hbk Phase 32 — E3 reference algorithm: 측정선 반길이 px (사용자 reference 기본값)

        public CompoundShortAxisDistanceMeasurement(object owner) : base(owner) { } //260523 hbk Phase 32 — E3 단축 환원

        public override bool TryExecute( //260523 hbk Phase 32 — E3 reference algorithm
            HImage image,
            HTuple datumTransform,
            double pixelResolution,
            out double resultValue,
            out string error,
            out List<EdgeInspectionOverlay> overlays)
        {
            resultValue = 0;
            error = null;
            overlays = new List<EdgeInspectionOverlay>(); //260523 hbk Phase 32 — E3 reference algorithm

            var svc = new VisionAlgorithmService(); //260523 hbk Phase 32 — E3 reference algorithm

            // VisionAlgorithmService.TryFindShortAxisIntersections 로 단일 호출 위임:
            //   - canny → union → LargestRect XLD → get_contour_xld → 긴변 선택 → fit_line refined Phi → 측정선 → contour intersection
            //   - HObject 라이프사이클은 서비스 내부에서 try/finally 로 모두 처리
            double centerRow, centerCol, phi, length1, length2; //260523 hbk Phase 32 — E3 reference algorithm
            double e1ar, e1ac, e1br, e1bc; //260523 hbk Phase 32 — E3 reference algorithm: 긴변 1 양 끝
            double e2ar, e2ac, e2br, e2bc; //260523 hbk Phase 32 — E3 reference algorithm: 긴변 2 양 끝
            double mRow1, mCol1, mRow2, mCol2; //260523 hbk Phase 32 — E3 reference algorithm: 측정선 양 끝
            double int1Row, int1Col, int2Row, int2Col; //260523 hbk Phase 32 — E3 reference algorithm: 교점 2개

            if (!svc.TryFindShortAxisIntersections(image, //260523 hbk Phase 32 — E3 reference algorithm
                Rect_Row, Rect_Col, Rect_Phi, Rect_Length1, Rect_Length2, //260523 hbk Phase 32 — E3 reference algorithm
                datumTransform, //260523 hbk Phase 32 — E3 reference algorithm
                CannyAlpha, CannyLow, CannyHigh, UnionDistance, //260523 hbk Phase 32 — E3 reference algorithm
                CrossLen, //260523 hbk Phase 32 — E3 reference algorithm
                out centerRow, out centerCol, //260523 hbk Phase 32 — E3 reference algorithm
                out phi, out length1, out length2, //260523 hbk Phase 32 — E3 reference algorithm
                out e1ar, out e1ac, out e1br, out e1bc, //260523 hbk Phase 32 — E3 reference algorithm
                out e2ar, out e2ac, out e2br, out e2bc, //260523 hbk Phase 32 — E3 reference algorithm
                out mRow1, out mCol1, out mRow2, out mCol2, //260523 hbk Phase 32 — E3 reference algorithm
                out int1Row, out int1Col, out int2Row, out int2Col, //260523 hbk Phase 32 — E3 reference algorithm
                out error)) //260523 hbk Phase 32 — E3 reference algorithm
            {
                return false;
            }

            // 결과값 = 두 교점 간 euclidean 거리 × pixelResolution (≈ 2 × length2 × pixelResolution, contour subpixel 정확도)
            double dRow = int2Row - int1Row; //260523 hbk Phase 32 — E3 reference algorithm
            double dCol = int2Col - int1Col; //260523 hbk Phase 32 — E3 reference algorithm
            double distPx = System.Math.Sqrt(dRow * dRow + dCol * dCol); //260523 hbk Phase 32 — E3 reference algorithm
            resultValue = distPx * pixelResolution; //260523 hbk Phase 32 — E3 reference algorithm

            // 오버레이 6개 — return true 직전 ADDITIVE, HALCON 재호출 0 (ArcLineIntersect 패턴)
            // 긴변 1 (라인 + 중점 마커)
            double edge1MidRow = (e1ar + e1br) / 2.0; //260523 hbk Phase 32 — E3 reference algorithm
            double edge1MidCol = (e1ac + e1bc) / 2.0; //260523 hbk Phase 32 — E3 reference algorithm
            overlays.Add(new EdgeInspectionOverlay //260523 hbk Phase 32 — E3 reference algorithm
            {
                RoiId = "FAI-LongEdge1", //260523 hbk Phase 32 — E3 reference algorithm
                LineRow1 = e1ar, LineColumn1 = e1ac, //260523 hbk Phase 32 — E3 reference algorithm
                LineRow2 = e1br, LineColumn2 = e1bc, //260523 hbk Phase 32 — E3 reference algorithm
                Points = new List<EdgeInspectionPoint> //260523 hbk Phase 32 — E3 reference algorithm
                {
                    new EdgeInspectionPoint { Row = edge1MidRow, Column = edge1MidCol } //260523 hbk Phase 32 — E3 reference algorithm
                }
            }); //260523 hbk Phase 32 — E3 reference algorithm
            // 긴변 2
            double edge2MidRow = (e2ar + e2br) / 2.0; //260523 hbk Phase 32 — E3 reference algorithm
            double edge2MidCol = (e2ac + e2bc) / 2.0; //260523 hbk Phase 32 — E3 reference algorithm
            overlays.Add(new EdgeInspectionOverlay //260523 hbk Phase 32 — E3 reference algorithm
            {
                RoiId = "FAI-LongEdge2", //260523 hbk Phase 32 — E3 reference algorithm
                LineRow1 = e2ar, LineColumn1 = e2ac, //260523 hbk Phase 32 — E3 reference algorithm
                LineRow2 = e2br, LineColumn2 = e2bc, //260523 hbk Phase 32 — E3 reference algorithm
                Points = new List<EdgeInspectionPoint> //260523 hbk Phase 32 — E3 reference algorithm
                {
                    new EdgeInspectionPoint { Row = edge2MidRow, Column = edge2MidCol } //260523 hbk Phase 32 — E3 reference algorithm
                }
            }); //260523 hbk Phase 32 — E3 reference algorithm
            // 측정선 (중심 지나는 phi+π/2 방향)
            overlays.Add(new EdgeInspectionOverlay //260523 hbk Phase 32 — E3 reference algorithm
            {
                RoiId = "FAI-MeasureLine", //260523 hbk Phase 32 — E3 reference algorithm
                LineRow1 = mRow1, LineColumn1 = mCol1, //260523 hbk Phase 32 — E3 reference algorithm
                LineRow2 = mRow2, LineColumn2 = mCol2, //260523 hbk Phase 32 — E3 reference algorithm
                Points = new List<EdgeInspectionPoint> //260523 hbk Phase 32 — E3 reference algorithm
                {
                    new EdgeInspectionPoint { Row = centerRow, Column = centerCol } //260523 hbk Phase 32 — E3 reference algorithm
                }
            }); //260523 hbk Phase 32 — E3 reference algorithm
            // 교점 1 (점 마커)
            overlays.Add(new EdgeInspectionOverlay //260523 hbk Phase 32 — E3 reference algorithm
            {
                RoiId = "FAI-Intersection1", //260523 hbk Phase 32 — E3 reference algorithm
                LineRow1 = int1Row, LineColumn1 = int1Col, //260523 hbk Phase 32 — E3 reference algorithm
                LineRow2 = int1Row, LineColumn2 = int1Col, //260523 hbk Phase 32 — E3 reference algorithm: 점 마커
                Points = new List<EdgeInspectionPoint> //260523 hbk Phase 32 — E3 reference algorithm
                {
                    new EdgeInspectionPoint { Row = int1Row, Column = int1Col } //260523 hbk Phase 32 — E3 reference algorithm
                }
            }); //260523 hbk Phase 32 — E3 reference algorithm
            // 교점 2 (점 마커)
            overlays.Add(new EdgeInspectionOverlay //260523 hbk Phase 32 — E3 reference algorithm
            {
                RoiId = "FAI-Intersection2", //260523 hbk Phase 32 — E3 reference algorithm
                LineRow1 = int2Row, LineColumn1 = int2Col, //260523 hbk Phase 32 — E3 reference algorithm
                LineRow2 = int2Row, LineColumn2 = int2Col, //260523 hbk Phase 32 — E3 reference algorithm: 점 마커
                Points = new List<EdgeInspectionPoint> //260523 hbk Phase 32 — E3 reference algorithm
                {
                    new EdgeInspectionPoint { Row = int2Row, Column = int2Col } //260523 hbk Phase 32 — E3 reference algorithm
                }
            }); //260523 hbk Phase 32 — E3 reference algorithm
            // 측정 거리선 — 교점1 ↔ 교점2
            overlays.Add(new EdgeInspectionOverlay //260523 hbk Phase 32 — E3 reference algorithm
            {
                RoiId = "FAI-DistLine", //260523 hbk Phase 32 — E3 reference algorithm
                LineRow1 = int1Row, LineColumn1 = int1Col, //260523 hbk Phase 32 — E3 reference algorithm
                LineRow2 = int2Row, LineColumn2 = int2Col, //260523 hbk Phase 32 — E3 reference algorithm
                Points = new List<EdgeInspectionPoint> //260523 hbk Phase 32 — E3 reference algorithm
                {
                    new EdgeInspectionPoint { Row = int1Row, Column = int1Col }, //260523 hbk Phase 32 — E3 reference algorithm
                    new EdgeInspectionPoint { Row = int2Row, Column = int2Col } //260523 hbk Phase 32 — E3 reference algorithm
                }
            }); //260523 hbk Phase 32 — E3 reference algorithm

            return true;
        }
    }
}
