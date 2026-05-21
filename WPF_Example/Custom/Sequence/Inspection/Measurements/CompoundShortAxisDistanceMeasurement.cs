//260521 hbk Phase 32 E3 — E3: 공통 컨투어 알고리즘(canny→union→LargestRect) → 단축 폭 측정
using System.Collections.Generic;
using HalconDotNet;
using PropertyTools.DataAnnotations;
using ReringProject.Halcon.Algorithms;
using ReringProject.Halcon.Models;

namespace ReringProject.Sequence
{
    /// <summary>
    /// Rect ROI 1개 → 공통 컨투어 알고리즘(canny→union_adjacent→LargestRect) → 단축 폭(mm) 측정.
    /// E3(SOP p.50): CompoundShortAxisDistance — La/Lb 2직선 거리, 공차 0.600±0.030.
    /// LargestRect 단축 폭 = smallest_rectangle2_xld 의 length1/length2 중 작은 쪽 × 2 × pixelResolution.
    /// Datum 비의존: 단축 폭은 사각형 자체 기하이므로 IDatumOriginConsumer 미구현.
    /// VisionAlgorithmService.TryFindLargestContourRect 공용 컨투어 서비스 호출.
    /// </summary>
    public class CompoundShortAxisDistanceMeasurement : MeasurementBase //260521 hbk Phase 32 E3
    {
        public override string TypeName { get { return "CompoundShortAxisDistance"; } } //260521 hbk Phase 32 E3

        // ── Rect ROI ─────────────────────────────────────────────────────────────────
        //260521 hbk Phase 32 E3 — 공통 컨투어 알고리즘 입력 Rect ROI
        [Category("Rect|ROI")] //260521 hbk Phase 32 E3
        public double Rect_Row { get; set; } //260521 hbk Phase 32 E3
        public double Rect_Col { get; set; } //260521 hbk Phase 32 E3
        public double Rect_Phi { get; set; } //260521 hbk Phase 32 E3
        public double Rect_Length1 { get; set; } //260521 hbk Phase 32 E3
        public double Rect_Length2 { get; set; } //260521 hbk Phase 32 E3

        // ── Contour 파라미터 (PropertyGrid 사용자 편집) ───────────────────────────────
        //260521 hbk Phase 32 E3 — 공통 컨투어 알고리즘 파라미터 (PropertyGrid 사용자 편집)
        [Category("Contour")] //260521 hbk Phase 32 E3
        public double CannyAlpha { get; set; } = 1.0; //260521 hbk Phase 32 E3 — edges_sub_pix canny alpha
        public int CannyLow { get; set; } = 20; //260521 hbk Phase 32 E3 — canny low threshold
        public int CannyHigh { get; set; } = 40; //260521 hbk Phase 32 E3 — canny high threshold
        public double UnionDistance { get; set; } = 700.0; //260521 hbk Phase 32 E3 — union_adjacent_contours_xld 거리

        public CompoundShortAxisDistanceMeasurement(object owner) : base(owner) { } //260521 hbk Phase 32 E3

        public override bool TryExecute( //260521 hbk Phase 32 E3
            HImage image,
            HTuple datumTransform,
            double pixelResolution,
            out double resultValue,
            out string error,
            out List<EdgeInspectionOverlay> overlays)
        {
            resultValue = 0;
            error = null;
            overlays = new List<EdgeInspectionOverlay>(); //260521 hbk Phase 32 E3

            var svc = new VisionAlgorithmService(); //260521 hbk Phase 32 E3

            // 공통 컨투어 알고리즘 → LargestRect 중심/각도/장단축
            double centerRow, centerCol, phi, length1, length2; //260521 hbk Phase 32 E3
            if (!svc.TryFindLargestContourRect(image,
                Rect_Row, Rect_Col, Rect_Phi, Rect_Length1, Rect_Length2,
                datumTransform,
                CannyAlpha, CannyLow, CannyHigh, UnionDistance,
                out centerRow, out centerCol, out phi, out length1, out length2, out error)) //260521 hbk Phase 32 E3
            {
                return false;
            }

            // 단축 폭 = smallest_rectangle2_xld 의 짧은 변.
            // length1/length2 는 사각형 장축/단축 반길이 → 단축 폭 = 2 * min(length1, length2).
            // TryFindLargestContourRect 가 스칼라만 반환(사각형 XLD 미반환)하므로
            // intersection_contours_xld 대신 직접 계산 채택 — 수학적으로 등가, 교점 0개 위험 없음.
            double shortHalf = System.Math.Min(length1, length2); //260521 hbk Phase 32 E3
            double shortAxisWidthPx = 2.0 * shortHalf; //260521 hbk Phase 32 E3 — 사각형 짧은 변 폭 (px)

            resultValue = shortAxisWidthPx * pixelResolution; //260521 hbk Phase 32 E3 — mm 변환 (SOP La↔Lb 간격 등가)

            // overlay — 단축 폭 세그먼트. TryFindLargestContourRect 결과 변수 재사용. HALCON 재호출 없음. //260521 hbk Phase 32 E3-overlay
            // 단축 방향 법선각도: phi 는 장축 방향(rad), 단축은 phi + π/2
            double phiPerp = phi + System.Math.PI / 2.0; //260521 hbk Phase 32 E3-overlay — 단축 방향 각도
            double sinPerp = System.Math.Sin(phiPerp); //260521 hbk Phase 32 E3-overlay
            double cosPerp = System.Math.Cos(phiPerp); //260521 hbk Phase 32 E3-overlay
            // 단축 세그먼트 양 끝점 = 중심 ± shortHalf × 단축방향
            double sEnd1Row = centerRow - shortHalf * sinPerp; //260521 hbk Phase 32 E3-overlay
            double sEnd1Col = centerCol - shortHalf * cosPerp; //260521 hbk Phase 32 E3-overlay
            double sEnd2Row = centerRow + shortHalf * sinPerp; //260521 hbk Phase 32 E3-overlay
            double sEnd2Col = centerCol + shortHalf * cosPerp; //260521 hbk Phase 32 E3-overlay
            // FAI-ShortAxis = 단축 폭 세그먼트 (양 끝 X마커 포함)
            overlays.Add(new EdgeInspectionOverlay //260521 hbk Phase 32 E3-overlay
            {
                RoiId = "FAI-ShortAxis", //260521 hbk Phase 32 E3-overlay — 기본 HalconDisplayService 분기 (라인 + 점 마커)
                LineRow1 = sEnd1Row, LineColumn1 = sEnd1Col, //260521 hbk Phase 32 E3-overlay — 단축 끝점 1
                LineRow2 = sEnd2Row, LineColumn2 = sEnd2Col, //260521 hbk Phase 32 E3-overlay — 단축 끝점 2
                Points = new List<EdgeInspectionPoint> //260521 hbk Phase 32 E3-overlay — 양 끝점 X마커
                {
                    new EdgeInspectionPoint { Row = sEnd1Row, Column = sEnd1Col }, //260521 hbk Phase 32 E3-overlay
                    new EdgeInspectionPoint { Row = sEnd2Row, Column = sEnd2Col } //260521 hbk Phase 32 E3-overlay
                }
            }); //260521 hbk Phase 32 E3-overlay

            return true;
        }
    }
}
