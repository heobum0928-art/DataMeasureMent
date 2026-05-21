//260521 hbk Phase 32 E3 — E3: 공통 컨투어 알고리즘(canny→union→LargestRect) → 장축 폭 측정
//260521 hbk Phase 32 UAT — 단축→장축 정정: CompoundShortAxisDistance → CompoundLongAxisDistance
using System.Collections.Generic;
using HalconDotNet;
using PropertyTools.DataAnnotations;
using ReringProject.Halcon.Algorithms;
using ReringProject.Halcon.Models;

namespace ReringProject.Sequence
{
    /// <summary>
    /// Rect ROI 1개 → 공통 컨투어 알고리즘(canny→union_adjacent→LargestRect) → 장축 폭(mm) 측정.
    /// E3(SOP p.50): CompoundLongAxisDistance — La/Lb 2직선 거리, 공차 0.600±0.030.
    /// LargestRect 장축 폭 = smallest_rectangle2_xld 의 length1/length2 중 큰 쪽 × 2 × pixelResolution.
    /// 장축 = 2 * max(length1, length2). (260521 hbk Phase 32 UAT — min→max 정정)
    /// Datum 비의존: 장축 폭은 사각형 자체 기하이므로 IDatumOriginConsumer 미구현.
    /// VisionAlgorithmService.TryFindLargestContourRect 공용 컨투어 서비스 호출.
    /// </summary>
    public class CompoundLongAxisDistanceMeasurement : MeasurementBase //260521 hbk Phase 32 UAT — Short→Long 정정
    {
        public override string TypeName { get { return "CompoundLongAxisDistance"; } } //260521 hbk Phase 32 UAT — CompoundShortAxisDistance → CompoundLongAxisDistance

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

        public CompoundLongAxisDistanceMeasurement(object owner) : base(owner) { } //260521 hbk Phase 32 UAT — 생성자명 정정

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

            // 장축 폭 = smallest_rectangle2_xld 의 긴 변.
            // length1/length2 는 사각형 장축/단축 반길이 → 장축 폭 = 2 * max(length1, length2).
            //260521 hbk Phase 32 UAT — min → max 정정 (단축→장축 SOP 요구사항 정정)
            // TryFindLargestContourRect 가 스칼라만 반환(사각형 XLD 미반환)하므로
            // intersection_contours_xld 대신 직접 계산 채택 — 수학적으로 등가, 교점 0개 위험 없음.
            double longHalf = System.Math.Max(length1, length2); //260521 hbk Phase 32 UAT — Max(장축 반길이)
            double longAxisWidthPx = 2.0 * longHalf; //260521 hbk Phase 32 UAT — 사각형 긴 변 폭 (px)

            resultValue = longAxisWidthPx * pixelResolution; //260521 hbk Phase 32 UAT — mm 변환 (SOP La↔Lb 간격 등가)

            // overlay — 장축 폭 세그먼트. TryFindLargestContourRect 결과 변수 재사용. HALCON 재호출 없음. //260521 hbk Phase 32 E3-overlay
            // 장축 방향각: phi (rad). 장축 세그먼트 양 끝점 = 중심 ± longHalf × 장축방향
            //260521 hbk Phase 32 UAT — FAI-ShortAxis → FAI-LongAxis, phiPerp 제거, phi(장축 방향) 직접 사용
            double sinPhi = System.Math.Sin(phi); //260521 hbk Phase 32 UAT — 장축 방향 sin
            double cosPhi = System.Math.Cos(phi); //260521 hbk Phase 32 UAT — 장축 방향 cos
            // 장축 세그먼트 양 끝점 = 중심 ± longHalf × 장축방향
            double lEnd1Row = centerRow - longHalf * sinPhi; //260521 hbk Phase 32 UAT
            double lEnd1Col = centerCol - longHalf * cosPhi; //260521 hbk Phase 32 UAT
            double lEnd2Row = centerRow + longHalf * sinPhi; //260521 hbk Phase 32 UAT
            double lEnd2Col = centerCol + longHalf * cosPhi; //260521 hbk Phase 32 UAT
            // FAI-LongAxis = 장축 폭 세그먼트 (양 끝 X마커 포함)
            overlays.Add(new EdgeInspectionOverlay //260521 hbk Phase 32 E3-overlay
            {
                RoiId = "FAI-LongAxis", //260521 hbk Phase 32 UAT — FAI-ShortAxis → FAI-LongAxis
                LineRow1 = lEnd1Row, LineColumn1 = lEnd1Col, //260521 hbk Phase 32 UAT — 장축 끝점 1
                LineRow2 = lEnd2Row, LineColumn2 = lEnd2Col, //260521 hbk Phase 32 UAT — 장축 끝점 2
                Points = new List<EdgeInspectionPoint> //260521 hbk Phase 32 E3-overlay — 양 끝점 X마커
                {
                    new EdgeInspectionPoint { Row = lEnd1Row, Column = lEnd1Col }, //260521 hbk Phase 32 UAT
                    new EdgeInspectionPoint { Row = lEnd2Row, Column = lEnd2Col } //260521 hbk Phase 32 UAT
                }
            }); //260521 hbk Phase 32 E3-overlay

            return true;
        }
    }
}
