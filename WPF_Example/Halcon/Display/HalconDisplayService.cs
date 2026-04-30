using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using HalconDotNet;
using ReringProject.Halcon.Models;
using ReringProject.Sequence;

namespace ReringProject.Halcon.Display
{
    public class HalconDisplayService
    {
        private bool _isFontInitialized;
        private static readonly HTuple MessageTextParamNames = new HTuple("box");
        private static readonly HTuple MessageTextParamValues = new HTuple("false");

        public void Render(
            HWindow window,
            HImage image,
            IEnumerable<RoiDefinition> rois,
            string selectedRoiId,
            RoiDefinition draftRoi = null,
            IEnumerable<EdgeInspectionOverlay> inspectionOverlays = null,
            IEnumerable<string> displayMessages = null)
        {
            if (window == null)
            {
                return;
            }

            EnsureFontInitialized(window);
            window.ClearWindow();
            window.SetDraw("margin");
            window.SetLineWidth(2);

            if (image != null)
            {
                window.DispObj(image);
            }

            if (rois != null)
            {
                foreach (var roi in rois)
                {
                    string roiColor = roi.Id == selectedRoiId ? "yellow" : "green";
                    int roiWidth = roi.Id == selectedRoiId ? 3 : 2;
                    window.SetColor(roiColor);
                    window.SetLineWidth(roiWidth);

                    //260423 hbk Phase 11 D-19 — Circle ROI 렌더링 (명시 Shape이 Polygon 감지보다 우선)
                    if (roi.Shape == RoiShape.Circle)
                    {
                        string circleColor = roi.Id == selectedRoiId ? "yellow" : "lime green";
                        int circleWidth = roi.Id == selectedRoiId ? 3 : 2;
                        window.SetColor(circleColor);
                        window.SetLineWidth(circleWidth);
                        HOperatorSet.DispCircle(window, roi.CenterRow, roi.CenterCol, roi.Radius);

                        // Center cross marker (6px, red) — UI-SPEC Circle ROI center marker
                        window.SetColor("red");
                        window.SetLineWidth(2);
                        window.DispLine(roi.CenterRow - 6, roi.CenterCol, roi.CenterRow + 6, roi.CenterCol);
                        window.DispLine(roi.CenterRow, roi.CenterCol - 6, roi.CenterRow, roi.CenterCol + 6);
                        continue;
                    }

                    //260408 hbk Polygon ROI 렌더링 지원
                    if (!string.IsNullOrEmpty(roi.PolygonPoints))
                    {
                        var pts = ParsePolygonPoints(roi.PolygonPoints);
                        if (pts != null && pts.Count >= 3)
                            RenderPolygon(window, pts, roiColor, roiWidth);
                    }
                    else if (roi.Row1 != 0 || roi.Column1 != 0 || roi.Row2 != 0 || roi.Column2 != 0)
                    {
                        DrawRectangleOutline(window, roi.Row1, roi.Column1, roi.Row2, roi.Column2);
                    }

                    if (roi.Id == selectedRoiId && roi.IsTaught)
                    {
                        DrawDirectionArrow(window, roi);
                    }
                }
            }

            if (draftRoi != null)
            {
                window.SetColor("red");
                window.SetLineWidth(3);
                DrawRectangleOutline(window, draftRoi.Row1, draftRoi.Column1, draftRoi.Row2, draftRoi.Column2);
            }

            if (inspectionOverlays != null)
            {
                foreach (var overlay in inspectionOverlays)
                {
                    if (string.Equals(overlay.RoiId, "Group-H", StringComparison.OrdinalIgnoreCase))
                    {
                        window.SetColor("cyan");
                        window.SetLineWidth(3);
                    }
                    else if (string.Equals(overlay.RoiId, "Group-V", StringComparison.OrdinalIgnoreCase))
                    {
                        window.SetColor("lime green");
                        window.SetLineWidth(3);
                    }
                    else if (string.Equals(overlay.RoiId, "Cross-H-Link", StringComparison.OrdinalIgnoreCase))
                    {
                        window.SetColor("cyan");
                        window.SetLineWidth(4);
                    }
                    else if (string.Equals(overlay.RoiId, "Cross-V-Link", StringComparison.OrdinalIgnoreCase))
                    {
                        window.SetColor("lime green");
                        window.SetLineWidth(4);
                    }
                    else if (string.Equals(overlay.RoiId, "Cross", StringComparison.OrdinalIgnoreCase))
                    {
                        window.SetColor("orange");
                        window.SetLineWidth(4);
                    }
                    else if (string.Equals(overlay.RoiId, "Main-Crosshair-H", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(overlay.RoiId, "Main-Crosshair-V", StringComparison.OrdinalIgnoreCase))
                    {
                        window.SetColor("yellow");
                        window.SetLineWidth(1);
                    }
                    else if (string.Equals(overlay.RoiId, "ManualMeasure-Line", StringComparison.OrdinalIgnoreCase))
                    {
                        window.SetColor("orange");
                        window.SetLineWidth(3);
                    }
                    else if (string.Equals(overlay.RoiId, "ManualMeasure-Start", StringComparison.OrdinalIgnoreCase))
                    {
                        window.SetColor("green");
                        window.SetLineWidth(3);
                    }
                    else if (string.Equals(overlay.RoiId, "ManualMeasure-End", StringComparison.OrdinalIgnoreCase))
                    {
                        window.SetColor("red");
                        window.SetLineWidth(3);
                    }
                    //260409 hbk Phase 3: FAI edge measurement result overlay colors
                    else if (overlay.RoiId != null && overlay.RoiId.StartsWith("FAI-Edge", StringComparison.OrdinalIgnoreCase))
                    {
                        bool isNG = overlay.RoiId.EndsWith("-NG", StringComparison.OrdinalIgnoreCase);
                        window.SetColor(isNG ? "red" : "green");
                        window.SetLineWidth(2);
                    }
                    else if (string.Equals(overlay.RoiId, "FAI-DistLine", StringComparison.OrdinalIgnoreCase))
                    {
                        window.SetColor("cyan");
                        window.SetLineWidth(1);
                    }
                    else
                    {
                        window.SetColor("blue");
                        window.SetLineWidth(2);
                    }

                    window.DispLine(overlay.LineRow1, overlay.LineColumn1, overlay.LineRow2, overlay.LineColumn2);
                    if (overlay.Points == null)
                    {
                        continue;
                    }

                    foreach (var point in overlay.Points)
                    {
                        const double size = 8.0;
                        window.DispLine(point.Row - size, point.Column - size, point.Row + size, point.Column + size);
                        window.DispLine(point.Row - size, point.Column + size, point.Row + size, point.Column - size);
                    }
                }
            }

            if (displayMessages == null)
            {
                return;
            }

            var line = 0;
            foreach (var message in displayMessages)
            {
                if (string.IsNullOrWhiteSpace(message))
                {
                    continue;
                }

                window.DispText(message, "window", 12 + (line * 28), 12, "yellow", MessageTextParamNames, MessageTextParamValues);
                line++;
            }
        }

        //260423 hbk Phase 11 D-14 — Circle 드래그 미리보기 (rubber-band, 빨강)
        public void RenderCircleDraft(HWindow window, double centerRow, double centerCol, double radius)
        {
            if (window == null || radius <= 0) return;
            try
            {
                window.SetColor("red");
                window.SetLineWidth(3);
                HOperatorSet.DispCircle(window, centerRow, centerCol, radius);
                // draft center cross (6px, red)
                window.DispLine(centerRow - 6, centerCol, centerRow + 6, centerCol);
                window.DispLine(centerRow, centerCol - 6, centerRow, centerCol + 6);
            }
            catch
            {
                // suppress display errors per existing HalconDisplayService pattern
            }
        }

        //260424 hbk Phase 13 D-07 — 런타임 TryFindDatum 성공 시 검출 RefOrigin 주황 3px 십자 + 좌표 WriteString 오버레이
        //  기존 RenderDatumOverlay teach-success 빨간 2px 십자 (L350+) 와 시각적으로 구분되는 팔레트.
        //  datum.RefOriginRow/Col 은 TryFindDatum 성공 시 DatumFindingService 에서 curRow/curCol 로 업데이트된 값.
        public void RenderDatumFindResult(HWindow window, DatumConfig datum)
        {
            if (window == null || datum == null) return;

            try
            {
                // 주황 십자 (20px half-length, 3px 굵기 — teach 빨강 2px 와 차별화)
                HOperatorSet.SetColor(window, "orange");
                HOperatorSet.SetLineWidth(window, 3);
                const double crossHalf = 20.0;
                HOperatorSet.DispLine(window,
                    datum.RefOriginRow - crossHalf, datum.RefOriginCol,
                    datum.RefOriginRow + crossHalf, datum.RefOriginCol);
                HOperatorSet.DispLine(window,
                    datum.RefOriginRow, datum.RefOriginCol - crossHalf,
                    datum.RefOriginRow, datum.RefOriginCol + crossHalf);

                // 좌표 텍스트 (십자 위쪽 외곽 — teach yellow 라벨 규약과 동일한 상단 offset)
                EnsureFontInitialized(window);
                HOperatorSet.SetColor(window, "orange");
                HOperatorSet.SetTposition(window, datum.RefOriginRow - crossHalf - 22, datum.RefOriginCol + crossHalf + 4);
                HOperatorSet.WriteString(window,
                    "TryFind (" + datum.RefOriginRow.ToString("F1") + ", " + datum.RefOriginCol.ToString("F1") + ")");
            }
            catch
            {
                // Suppress display errors (기존 RenderDatumOverlay / RenderCircleDraft catch 관습 유지)
            }
        }

        private void EnsureFontInitialized(HWindow window)
        {
            if (_isFontInitialized)
            {
                return;
            }

            try
            {
                HTuple fonts;
                HOperatorSet.QueryFont(window, out fonts);
                var font = fonts.TupleLength() > 0 ? fonts.TupleSelect(0) + "-18" : new HTuple("mono-18");
                window.SetFont(font);
                _isFontInitialized = true;
            }
            catch
            {
            }
        }

        private static void DrawRectangleOutline(HWindow window, double row1, double col1, double row2, double col2)
        {
            window.DispLine(row1, col1, row1, col2);
            window.DispLine(row1, col2, row2, col2);
            window.DispLine(row2, col2, row2, col1);
            window.DispLine(row2, col1, row1, col1);
        }

        //260408 hbk RenderPolygon 추가 (Polygon ROI 렌더링)
        /// <summary>Renders a polygon outline on the HWindow. Points as (col, row) pairs (X=col, Y=row).</summary>
        public void RenderPolygon(HWindow window, IList<Point> points, string color, int lineWidth)
        {
            if (window == null || points == null || points.Count < 3) return;
            window.SetColor(color);
            window.SetLineWidth(lineWidth);
            for (int i = 0; i < points.Count; i++)
            {
                int next = (i + 1) % points.Count;
                window.DispLine(points[i].Y, points[i].X, points[next].Y, points[next].X);
            }
        }

        //260408 hbk RenderPolygonPoints — 큰 + 표시 + 점 사이 라인
        /// <summary>Renders polygon draft points (large cross marks + connecting lines during drawing).</summary>
        public void RenderPolygonPoints(HWindow window, IList<Point> points, string color)
        {
            if (window == null || points == null) return;
            int crossSize = 12;

            // 점 사이 연결선
            if (points.Count >= 2)
            {
                window.SetColor("cyan");
                window.SetLineWidth(1);
                for (int i = 0; i < points.Count - 1; i++)
                {
                    window.DispLine(points[i].Y, points[i].X, points[i + 1].Y, points[i + 1].X);
                }
            }

            // 각 점에 큰 + 표시
            window.SetColor(color);
            window.SetLineWidth(2);
            foreach (var pt in points)
            {
                window.DispLine(pt.Y - crossSize, pt.X, pt.Y + crossSize, pt.X);
                window.DispLine(pt.Y, pt.X - crossSize, pt.Y, pt.X + crossSize);
            }
        }

        //260408 hbk Calibration 십자 + 라인 렌더링
        /// <summary>Renders calibration crosshairs and connecting line.</summary>
        public void RenderCalibrationOverlay(HWindow window, IList<Point> points)
        {
            if (window == null || points == null || points.Count == 0) return;
            int crossSize = 20;

            window.SetColor("yellow");
            window.SetLineWidth(2);
            // 첫 번째 점 십자
            var p1 = points[0];
            window.DispLine(p1.Y - crossSize, p1.X, p1.Y + crossSize, p1.X);
            window.DispLine(p1.Y, p1.X - crossSize, p1.Y, p1.X + crossSize);

            if (points.Count >= 2)
            {
                // 두 번째 점 십자
                var p2 = points[1];
                window.DispLine(p2.Y - crossSize, p2.X, p2.Y + crossSize, p2.X);
                window.DispLine(p2.Y, p2.X - crossSize, p2.Y, p2.X + crossSize);

                // 두 점 사이 연결선
                window.SetColor("green");
                window.SetLineWidth(1);
                window.DispLine(p1.Y, p1.X, p2.Y, p2.X);
            }
        }

        //260408 hbk DrawDirectionArrow 추가 (에지 방향 화살표)
        /// <summary>Draws edge search direction arrow at ROI center (per D-02). White, 2px line + arrowhead.</summary>
        private static void DrawDirectionArrow(HWindow window, RoiDefinition roi)
        {
            double centerRow = (roi.Row1 + roi.Row2) / 2.0;
            double centerCol = (roi.Column1 + roi.Column2) / 2.0;
            double arrowLength = 15.0;

            // Determine arrow direction from EdgeDirection
            double dRow = 0, dCol = 0;
            switch (roi.EdgeDirection)
            {
                case "LtoR": dCol = arrowLength; break;
                case "RtoL": dCol = -arrowLength; break;
                case "TtoB": dRow = arrowLength; break;
                case "BtoT": dRow = -arrowLength; break;
                default: dCol = arrowLength; break;
            }

            window.SetColor("white");
            window.SetLineWidth(2);
            // Main line
            window.DispLine(centerRow - dRow / 2, centerCol - dCol / 2,
                             centerRow + dRow / 2, centerCol + dCol / 2);
            // Arrowhead (two short lines at 30-degree angle)
            double headLen = 5.0;
            double endRow = centerRow + dRow / 2;
            double endCol = centerCol + dCol / 2;
            double angle = Math.Atan2(dRow, dCol);
            double a1 = angle + 2.5;  // ~143 degrees
            double a2 = angle - 2.5;  // ~-143 degrees
            window.DispLine(endRow, endCol,
                             endRow + headLen * Math.Sin(a1), endCol + headLen * Math.Cos(a1));
            window.DispLine(endRow, endCol,
                             endRow + headLen * Math.Sin(a2), endCol + headLen * Math.Cos(a2));
        }

        //260425 hbk Phase 13 D-VIZ-04 — 검출 라인 외삽 거리 (HALCON DispLine 자동 클리핑 활용; 30K~50K 이미지에서도 충분)
        private const double EXTEND_PX = 10000.0;

        //260425 hbk Phase 13 D-VIZ-04 — 두 점 (r1,c1)-(r2,c2) 를 unit-vector × EXTEND_PX 로 양쪽 외삽 후 DispLine
        //  HALCON 은 화면 밖 좌표를 자동 클리핑하므로 이미지 width/height 조회 불필요.
        //  두 점이 동일하면 (lenSq=0) DispLine 호출 자체를 skip — divide by zero 방지.
        private static void DrawExtendedLine(HWindow window, double r1, double c1, double r2, double c2)
        {
            double dr = r2 - r1;
            double dc = c2 - c1;
            double lenSq = dr * dr + dc * dc;
            if (lenSq < 1e-9) return; // degenerate
            double len = Math.Sqrt(lenSq);
            double ur = dr / len;
            double uc = dc / len;
            double exR1 = r1 - ur * EXTEND_PX;
            double exC1 = c1 - uc * EXTEND_PX;
            double exR2 = r2 + ur * EXTEND_PX;
            double exC2 = c2 + uc * EXTEND_PX;
            try
            {
                HOperatorSet.DispLine(window, exR1, exC1, exR2, exC2);
            }
            catch
            {
                // suppress display errors per RenderDatumOverlay 관습
            }
        }

        //260425 hbk Phase 13 D-VIZ-05 — raw 검출 에지점들을 작은 cross 마커로 일괄 렌더
        //  rows/cols 가 null 이거나 length 0 이면 no-op (안전).
        //  size 기본 6 px, line width 1. HALCON DispCross batch: rows/cols HTuple 일괄 처리.
        //260429 hbk Phase 16 D-07 — size 인자 추가 (default 6.0, 기존 호출 시그니처 하위호환). Circle 호출처에서만 4.0 + "gray" override.
        private static void RenderRawEdgePoints(HWindow window, HTuple rows, HTuple cols, string color, double size = 6.0)
        {
            if (rows == null || cols == null) return;
            int n = rows.TupleLength();
            if (n == 0 || cols.TupleLength() != n) return;
            try
            {
                HOperatorSet.SetColor(window, color);
                HOperatorSet.SetLineWidth(window, 1);
                //260429 hbk Phase 16 D-07 — 6.0 하드코딩 → size 인자
                HOperatorSet.DispCross(window, rows, cols, size, 0.0);
            }
            catch
            {
                // Suppress display errors (RenderDatumOverlay catch 관습)
            }
        }

        //260429 hbk Phase 16 D-01 — 원 ROI 그린 직후 알고리즘이 사용할 strip 사각형 stepCount 개를 정적으로 시각화.
        //  VisionAlgorithmService.TryFindCircleByPolarSampling 의 strip 생성 식 (Phase 14-04 D-13 보존) 을 그대로 미러링.
        //  알고리즘 canonical (VisionAlgorithmService.cs line 282-285):
        //    rectRow = CircleROI_Row - Radius * Sin(thetaRad)   (화면 CCW 좌표계 — Phase 14-04 D-13 코멘트 참조)
        //    rectCol = CircleROI_Col + Radius * Cos(thetaRad)
        //    rectPhi = thetaRad
        //  Plan 16-01 <interfaces> 96-103 의 sin/cos 부호는 plan-text error (반대 부호) — 본 구현은 알고리즘 코드 미러링 (D-22:
        //  알고리즘 diff 0 보존 + 시각화는 알고리즘 식 따라간다 라는 plan 의 stated intent 에 충실).
        //  length1 = Radius * RectL1Ratio (반경 방향), length2 = Radius * RectL2Ratio (접선 방향). fill 없음 — DispLine 외곽선만.
        private static void RenderCircleStripOverlay(HWindow window, DatumConfig datum)
        {
            if (datum == null) return;
            if (datum.CircleROI_Radius <= 0) return;
            double stepDeg = datum.Circle_PolarStepDeg;
            //260429 hbk Phase 16 D-01 — 0/음수 division 방지 + CONTEXT D-01: 1°~30° 범위 가드
            if (stepDeg < 1.0) stepDeg = 1.0;
            if (stepDeg > 30.0) stepDeg = 30.0;
            int stepCount = (int)Math.Round(360.0 / stepDeg);
            if (stepCount <= 0) return;

            double radius  = datum.CircleROI_Radius;
            double centerR = datum.CircleROI_Row;
            double centerC = datum.CircleROI_Col;
            //260429 hbk Phase 16 D-01 — 반경 방향 / 접선 방향 길이
            double length1 = radius * datum.Circle_RectL1Ratio;
            double length2 = radius * datum.Circle_RectL2Ratio;
            //260429 hbk Phase 16 — 1px 미만이면 시각화 의미 없음, 자동 floor
            if (length1 < 1.0) length1 = 1.0;
            if (length2 < 1.0) length2 = 1.0;

            try
            {
                //260429 hbk Phase 16 D-02 — Strip 색상: 회색 thin line, fill 없음 (cyan/magenta/yellow 와 충돌 회피)
                HOperatorSet.SetColor(window, "gray");
                HOperatorSet.SetLineWidth(window, 1);
                double stepRad = stepDeg * Math.PI / 180.0;
                for (int k = 0; k < stepCount; k++)
                {
                    double thetaRad = k * stepRad;
                    //260429 hbk Phase 16 D-01 — 알고리즘 canonical 식 미러 (VisionAlgorithmService line 282-285, -sin/+cos)
                    double rectRow = centerR - radius * Math.Sin(thetaRad);
                    double rectCol = centerC + radius * Math.Cos(thetaRad);
                    double rectPhi = thetaRad;
                    //260429 hbk Phase 16 D-02 — fill 없는 외곽선만: 4 corner 좌표 직접 계산 후 DispLine 4 회 (DispObj GenRectangle2 는 fill 됨)
                    double cosP = Math.Cos(rectPhi);
                    double sinP = Math.Sin(rectPhi);
                    //  로컬 4 corner: (-l1,-l2), (-l1,+l2), (+l1,+l2), (+l1,-l2) → 회전 변환 (rectPhi)
                    double r1 = rectRow + (-length1) * cosP - (-length2) * sinP;
                    double c1 = rectCol + (-length1) * sinP + (-length2) * cosP;
                    double r2 = rectRow + (-length1) * cosP - ( length2) * sinP;
                    double c2 = rectCol + (-length1) * sinP + ( length2) * cosP;
                    double r3 = rectRow + ( length1) * cosP - ( length2) * sinP;
                    double c3 = rectCol + ( length1) * sinP + ( length2) * cosP;
                    double r4 = rectRow + ( length1) * cosP - (-length2) * sinP;
                    double c4 = rectCol + ( length1) * sinP + (-length2) * cosP;
                    HOperatorSet.DispLine(window, r1, c1, r2, c2);
                    HOperatorSet.DispLine(window, r2, c2, r3, c3);
                    HOperatorSet.DispLine(window, r3, c3, r4, c4);
                    HOperatorSet.DispLine(window, r4, c4, r1, c1);
                }
            }
            catch
            {
                //260429 hbk Phase 16 — RenderDatumOverlay 의 catch 컨벤션 유지 (display 에러 무시)
            }
        }

        //260409 hbk Phase 4: render Datum Line ROI overlays on canvas (D-12)
        /// <summary>Renders Datum Line1/Line2 ROI rectangles and reference origin cross on HWindow.</summary>
        public void RenderDatumOverlay(HWindow window, DatumConfig datum, bool isSelected)
        {
            if (datum == null) return;

            string color = isSelected ? "cyan" : "blue";
            int lineWidth = isSelected ? 3 : 2;

            try
            {
                HOperatorSet.SetColor(window, color);
                HOperatorSet.SetLineWidth(window, lineWidth);

                //260428 hbk W4-A 후속 — RenderDatumOverlay 슬롯 분기 수정
                //  Phase 14-03 W4-A 에서 VerticalTwoHorizontal 의 수직 검색 ROI 를 Line1_* → Vertical_* 슬롯으로 이동했으나
                //  렌더 경로가 갱신되지 않아 사용자가 Vertical ROI 를 그려도 사각형이 표시되지 않는 버그가 있었음.
                //  AlgorithmType 별로 그릴 슬롯을 분기:
                //    TwoLineIntersect       → Line1_*  (기존 동작 보존, "L1" 라벨)
                //    VerticalTwoHorizontal  → Vertical_* (신규, "Vert" 라벨)
                //    CircleTwoHorizontal    → 둘 다 미사용 (legacy INI 의 Line1_* 잔류값이 더 이상 잘못 렌더되지 않음)
                if (datum.AlgorithmTypeEnum == EDatumAlgorithm.TwoLineIntersect)
                {
                    if (datum.Line1_Length1 > 0 && datum.Line1_Length2 > 0)
                    {
                        HOperatorSet.DispRectangle2(window,
                            datum.Line1_Row, datum.Line1_Col, datum.Line1_Phi,
                            datum.Line1_Length1, datum.Line1_Length2);

                        DrawRoiLabel(window, datum.Line1_Row, datum.Line1_Col, datum.Line1_Phi,
                            datum.Line1_Length1, datum.Line1_Length2, "L1");
                    }
                }
                else if (datum.AlgorithmTypeEnum == EDatumAlgorithm.VerticalTwoHorizontal)
                {
                    if (datum.Vertical_Length1 > 0 && datum.Vertical_Length2 > 0)
                    {
                        HOperatorSet.DispRectangle2(window,
                            datum.Vertical_Row, datum.Vertical_Col, datum.Vertical_Phi,
                            datum.Vertical_Length1, datum.Vertical_Length2);

                        DrawRoiLabel(window, datum.Vertical_Row, datum.Vertical_Col, datum.Vertical_Phi,
                            datum.Vertical_Length1, datum.Vertical_Length2, "Vert");
                    }
                }
                // CircleTwoHorizontal: Line1/Vertical 모두 렌더하지 않음 (의도적). Horizontal A/B + Circle 만 아래 블록에서 그림.

                //260424 hbk Phase 12 D-13 — Line2 Rectangle2 는 TwoLineIntersect 에서만 렌더 (Circle/Vertical-TwoHorizontal 은 Line2 미사용)
                if (datum.AlgorithmTypeEnum == EDatumAlgorithm.TwoLineIntersect
                    && datum.Line2_Length1 > 0 && datum.Line2_Length2 > 0)
                {
                    HOperatorSet.DispRectangle2(window,
                        datum.Line2_Row, datum.Line2_Col, datum.Line2_Phi,
                        datum.Line2_Length1, datum.Line2_Length2);

                    //260424 hbk Phase 12 Gap-2 — "L2" 라벨
                    DrawRoiLabel(window, datum.Line2_Row, datum.Line2_Col, datum.Line2_Phi,
                        datum.Line2_Length1, datum.Line2_Length2, "L2");
                }

                //260424 hbk Phase 12 D-10 — Circle ROI 검색 영역 (CircleTwoHorizontal 일 때만 렌더, Line1/Line2 와 동일 색)
                if (datum.AlgorithmTypeEnum == EDatumAlgorithm.CircleTwoHorizontal
                    && datum.CircleROI_Radius > 0)
                {
                    HOperatorSet.SetColor(window, color);
                    HOperatorSet.SetLineWidth(window, lineWidth);
                    HOperatorSet.DispCircle(window,
                        datum.CircleROI_Row, datum.CircleROI_Col, datum.CircleROI_Radius);

                    //260424 hbk Phase 12 Gap-2 — "Circle" 라벨 (원 위쪽 외곽 바로 바깥)
                    DrawRoiLabelAt(window,
                        datum.CircleROI_Row - datum.CircleROI_Radius - 22,
                        datum.CircleROI_Col - datum.CircleROI_Radius,
                        "Circle");

                    //260429 hbk Phase 16 D-01/D-02/D-08 — pre-teach Strip 사각형 stepCount 개 정적 시각화 (z-order: ROI 경계 위)
                    RenderCircleStripOverlay(window, datum);
                }

                //260424 hbk Phase 12 D-11 — Horizontal A/B ROI Rectangle2 (CircleTwoHorizontal + VerticalTwoHorizontal 공용)
                if (datum.AlgorithmTypeEnum != EDatumAlgorithm.TwoLineIntersect)
                {
                    HOperatorSet.SetColor(window, color);
                    HOperatorSet.SetLineWidth(window, lineWidth);
                    if (datum.Horizontal_A_Length1 > 0 && datum.Horizontal_A_Length2 > 0)
                    {
                        HOperatorSet.DispRectangle2(window,
                            datum.Horizontal_A_Row, datum.Horizontal_A_Col, datum.Horizontal_A_Phi,
                            datum.Horizontal_A_Length1, datum.Horizontal_A_Length2);

                        //260424 hbk Phase 12 Gap-2 — "H-A" 라벨
                        DrawRoiLabel(window, datum.Horizontal_A_Row, datum.Horizontal_A_Col,
                            datum.Horizontal_A_Phi, datum.Horizontal_A_Length1, datum.Horizontal_A_Length2, "H-A");
                    }
                    if (datum.Horizontal_B_Length1 > 0 && datum.Horizontal_B_Length2 > 0)
                    {
                        HOperatorSet.DispRectangle2(window,
                            datum.Horizontal_B_Row, datum.Horizontal_B_Col, datum.Horizontal_B_Phi,
                            datum.Horizontal_B_Length1, datum.Horizontal_B_Length2);

                        //260424 hbk Phase 12 Gap-2 — "H-B" 라벨
                        DrawRoiLabel(window, datum.Horizontal_B_Row, datum.Horizontal_B_Col,
                            datum.Horizontal_B_Phi, datum.Horizontal_B_Length1, datum.Horizontal_B_Length2, "H-B");
                    }
                }

                // Draw reference origin cross if configured
                if (datum.IsConfigured)
                {
                    HOperatorSet.SetColor(window, "magenta");
                    HOperatorSet.SetLineWidth(window, 2);
                    double crossSize = 15;
                    // Horizontal line
                    HOperatorSet.DispLine(window,
                        datum.RefOriginRow, datum.RefOriginCol - crossSize,
                        datum.RefOriginRow, datum.RefOriginCol + crossSize);
                    // Vertical line
                    HOperatorSet.DispLine(window,
                        datum.RefOriginRow - crossSize, datum.RefOriginCol,
                        datum.RefOriginRow + crossSize, datum.RefOriginCol);
                    // Label
                    HOperatorSet.SetColor(window, "magenta");
                    EnsureFontInitialized(window);
                    HOperatorSet.SetTposition(window, datum.RefOriginRow - crossSize - 15, datum.RefOriginCol + 5);
                    HOperatorSet.WriteString(window, "Datum Origin");
                }

                //260423 hbk Phase 11 D-11 — 검출 라인 2개 + 교점 오버레이 (TryTeachDatum 성공 시에만, 기존 cyan/blue/magenta 팔레트는 건드리지 않음)
                if (datum.LastTeachSucceeded)
                {
                    //260425 hbk Phase 13 D-VIZ-04 — Line1 detected 외삽 (yellow)
                    HOperatorSet.SetColor(window, "yellow");
                    HOperatorSet.SetLineWidth(window, 2);
                    DrawExtendedLine(window,
                        datum.Line1Detected_RBegin, datum.Line1Detected_CBegin,
                        datum.Line1Detected_REnd,   datum.Line1Detected_CEnd);

                    //260425 hbk Phase 13 D-VIZ-04 — Line2 detected 외삽 (cyan)
                    HOperatorSet.SetColor(window, "cyan");
                    DrawExtendedLine(window,
                        datum.Line2Detected_RBegin, datum.Line2Detected_CBegin,
                        datum.Line2Detected_REnd,   datum.Line2Detected_CEnd);

                    // Intersection cross (red, 20px half-length, line width 2) — UI-SPEC Datum overlay palette
                    HOperatorSet.SetColor(window, "red");
                    HOperatorSet.SetLineWidth(window, 2);
                    const double crossHalf = 20.0;
                    HOperatorSet.DispLine(window, datum.RefOriginRow - crossHalf, datum.RefOriginCol,
                                                  datum.RefOriginRow + crossHalf, datum.RefOriginCol);
                    HOperatorSet.DispLine(window, datum.RefOriginRow, datum.RefOriginCol - crossHalf,
                                                  datum.RefOriginRow, datum.RefOriginCol + crossHalf);

                    //260424 hbk Phase 12 D-13 — CircleTwoHorizontal 검출 원 오버레이 (노란 원 + 빨간 중심 십자)
                    if (datum.AlgorithmTypeEnum == EDatumAlgorithm.CircleTwoHorizontal
                        && datum.CircleDetected_Radius > 0)
                    {
                        HOperatorSet.SetColor(window, "yellow");
                        HOperatorSet.SetLineWidth(window, 2);
                        HOperatorSet.DispCircle(window,
                            datum.CircleCenter_Row, datum.CircleCenter_Col, datum.CircleDetected_Radius);

                        HOperatorSet.SetColor(window, "red");
                        HOperatorSet.SetLineWidth(window, 2);
                        const double circleCenterCrossHalf = 6.0; //260424 hbk Phase 12 — 원 중심 십자 6px
                        HOperatorSet.DispLine(window,
                            datum.CircleCenter_Row - circleCenterCrossHalf, datum.CircleCenter_Col,
                            datum.CircleCenter_Row + circleCenterCrossHalf, datum.CircleCenter_Col);
                        HOperatorSet.DispLine(window,
                            datum.CircleCenter_Row, datum.CircleCenter_Col - circleCenterCrossHalf,
                            datum.CircleCenter_Row, datum.CircleCenter_Col + circleCenterCrossHalf);
                    }

                    //260425 hbk Phase 13 D-VIZ-05 — 5 ROI raw 검출 에지점 (있을 때만) — ROI 별 색상 구분
                    RenderRawEdgePoints(window, datum.Line1_DetectedEdgeRows,        datum.Line1_DetectedEdgeCols,        "cyan");
                    RenderRawEdgePoints(window, datum.Line2_DetectedEdgeRows,        datum.Line2_DetectedEdgeCols,        "magenta");
                    RenderRawEdgePoints(window, datum.Circle_DetectedEdgeRows,       datum.Circle_DetectedEdgeCols,       "yellow");
                    RenderRawEdgePoints(window, datum.Horizontal_A_DetectedEdgeRows, datum.Horizontal_A_DetectedEdgeCols, "green");
                    RenderRawEdgePoints(window, datum.Horizontal_B_DetectedEdgeRows, datum.Horizontal_B_DetectedEdgeCols, "lime green");
                    //260426 hbk Phase 14-03 Req 3 — Vertical 그룹 raw 점 (Line1 cyan 과 시각 구분: orange 신규 — 미사용 색상)
                    RenderRawEdgePoints(window, datum.Vertical_DetectedEdgeRows,     datum.Vertical_DetectedEdgeCols,     "orange");
                }
            }
            catch
            {
                // Suppress display errors
            }
        }

        //260424 hbk Phase 12 Gap-2 — Datum ROI 라벨 그리기 (수직/수평/라인 구분 가시화)
        // Rectangle2 (row, col, phi, length1, length2) 외곽 위쪽 바로 바깥에 yellow 텍스트로 ROI 식별자 렌더.
        //  phi=0 이면 (row-length1-22, col-length2) 가 좌상단 외곽. phi≠0 이어도 회전 중심 기준 상대 오프셋이므로 가독성 확보됨.
        private void DrawRoiLabel(HWindow window, double row, double col, double phi,
            double length1, double length2, string label)
        {
            // 외곽 상단 좌표 (회전 고려): ROI 로컬 (-length1, -length2) → 이미지 좌표 변환
            // 로컬 (-L1, -L2) 를 phi 만큼 회전 후 (row, col) 에 더함
            double cosP = Math.Cos(phi);
            double sinP = Math.Sin(phi);
            double labelRow = row + (-length1) * cosP - (-length2) * sinP - 22; // 외곽 위쪽 22px 바깥
            double labelCol = col + (-length1) * sinP + (-length2) * cosP;
            DrawRoiLabelAt(window, labelRow, labelCol, label);
        }

        //260424 hbk Phase 12 Gap-2 — 주어진 (row, col) 에 yellow 텍스트 라벨 렌더 (Circle ROI 등 비-Rectangle 용)
        private void DrawRoiLabelAt(HWindow window, double row, double col, string label)
        {
            try
            {
                EnsureFontInitialized(window);
                HOperatorSet.SetColor(window, "yellow");
                HOperatorSet.SetTposition(window, row, col);
                HOperatorSet.WriteString(window, label);
            }
            catch
            {
                // Suppress display errors (기존 RenderDatumOverlay catch 관습 유지)
            }
        }

        //260408 hbk PolygonPoints 문자열 파싱 ("x1,y1;x2,y2;..." → List<Point>)
        private static IList<Point> ParsePolygonPoints(string polygonPoints)
        {
            if (string.IsNullOrEmpty(polygonPoints)) return null;
            var result = new List<Point>();
            var pairs = polygonPoints.Split(';');
            foreach (var pair in pairs)
            {
                var parts = pair.Split(',');
                if (parts.Length >= 2 &&
                    double.TryParse(parts[0], out double x) &&
                    double.TryParse(parts[1], out double y))
                {
                    result.Add(new Point(x, y));
                }
            }
            return result;
        }
    }
}














