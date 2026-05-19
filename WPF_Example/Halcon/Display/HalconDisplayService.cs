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
        //260519 hbk #6-b — 전역 폰트 문자열 캐시 (DrawRoiLabelAt 축소 폰트 원복용)
        private string _normalFontName;
        private static readonly HTuple MessageTextParamNames = new HTuple("box");
        private static readonly HTuple MessageTextParamValues = new HTuple("false");

        //260519 hbk #6-a — ROI 가 현재 선택 대상인지 판정한다.
        //  exact Id 매칭에 더해, FAI 노드 선택(selectedRoiId=FAIName) 시 그 FAI 의 모든 자식 ROI
        //  (Id="FAIName_<측정명>")도 선택으로 간주한다 — FAI 에 rect ROI 가 없고 측정별 Point ROI 만
        //  있을 때 FAI 노드 클릭/결과행 선택이 아무 ROI 도 하이라이트 못 하던 문제 해결.
        //  "_" 경계 덕분에 FAI_0 선택이 FAI_01 의 ROI 를 잘못 매칭하지 않는다.
        private static bool IsRoiSelected(RoiDefinition roi, string selectedRoiId)
        {
            if (roi == null || string.IsNullOrEmpty(selectedRoiId) || string.IsNullOrEmpty(roi.Id))
            {
                return false;
            }
            if (roi.Id == selectedRoiId)
            {
                return true;
            }
            return roi.Id.StartsWith(selectedRoiId + "_", StringComparison.Ordinal);
        }

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
                    //260509 hbk Phase 20 — ?: → if/else (D-01)
                    string roiColor;
                    int roiWidth;
                    if (IsRoiSelected(roi, selectedRoiId)) //260519 hbk #6-a — exact + FAI 접두사 매칭
                    {
                        roiColor = "yellow";
                        roiWidth = 3;
                    }
                    else
                    {
                        roiColor = "green";
                        roiWidth = 2;
                    }
                    window.SetColor(roiColor);
                    window.SetLineWidth(roiWidth);

                    //260423 hbk Phase 11 D-19 — Circle ROI 렌더링 (명시 Shape이 Polygon 감지보다 우선)
                    if (roi.Shape == RoiShape.Circle)
                    {
                        //260509 hbk Phase 20 — ?: → if/else (D-01)
                        string circleColor;
                        int circleWidth;
                        if (IsRoiSelected(roi, selectedRoiId)) //260519 hbk #6-a — exact + FAI 접두사 매칭
                        {
                            circleColor = "yellow";
                            circleWidth = 3;
                        }
                        else
                        {
                            circleColor = "lime green";
                            circleWidth = 2;
                        }
                        window.SetColor(circleColor);
                        window.SetLineWidth(circleWidth);
                        HOperatorSet.DispCircle(window, roi.CenterRow, roi.CenterCol, roi.Radius);

                        // Center cross marker (6px, red) — UI-SPEC Circle ROI center marker
                        window.SetColor("red");
                        window.SetLineWidth(2);
                        window.DispLine(roi.CenterRow - 6, roi.CenterCol, roi.CenterRow + 6, roi.CenterCol);
                        window.DispLine(roi.CenterRow, roi.CenterCol - 6, roi.CenterRow, roi.CenterCol + 6);
                        //260518 hbk #6 — Circle ROI 명칭 라벨 (원 상단 외곽)
                        if (!string.IsNullOrEmpty(roi.Name))
                            DrawRoiLabelAt(window, roi.CenterRow - roi.Radius - 22, roi.CenterCol, roi.Name);
                        continue;
                    }

                    //260408 hbk Polygon ROI 렌더링 지원
                    if (!string.IsNullOrEmpty(roi.PolygonPoints))
                    {
                        var pts = ParsePolygonPoints(roi.PolygonPoints);
                        if (pts != null && pts.Count >= 3)
                            RenderPolygon(window, pts, roiColor, roiWidth);
                        //260518 hbk #6 — Polygon ROI 명칭 라벨 (첫 점 기준 위쪽)
                        if (!string.IsNullOrEmpty(roi.Name) && pts != null && pts.Count > 0)
                            DrawRoiLabelAt(window, pts[0].Y - 22, pts[0].X, roi.Name);
                    }
                    else if (roi.Row1 != 0 || roi.Column1 != 0 || roi.Row2 != 0 || roi.Column2 != 0)
                    {
                        DrawRectangleOutline(window, roi.Row1, roi.Column1, roi.Row2, roi.Column2);
                        //260518 hbk #6 — Rectangle ROI 명칭 라벨 (좌상단 외곽 위쪽)
                        if (!string.IsNullOrEmpty(roi.Name))
                            DrawRoiLabelAt(window, roi.Row1 - 22, roi.Column1, roi.Name);
                    }

                    if (IsRoiSelected(roi, selectedRoiId) && roi.IsTaught) //260519 hbk #6-a — exact + FAI 접두사 매칭
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
                        //260509 hbk Phase 20 — ?: → if/else (D-01)
                        if (isNG)
                        {
                            window.SetColor("red");
                        }
                        else
                        {
                            window.SetColor("green");
                        }
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

        //260503 hbk Phase 17 D-13 — RenderDatumFindResult 본문 교체 (orange→purple, RefOrigin→DetectedOrigin, +DetectedRefAngle 화살표)
        //  Phase 13 D-07 본문 (orange + RefOriginRow/Col) 폐기 → DetectedOrigin transient 기반 시각화로 전환.
        //  LastFindSucceeded gate: TryFindDatum 성공 분기에서만 렌더 (catch/조기 return 시 자동 미렌더).
        //  Z-stack: RenderDatumOverlay 의 LastTeachSucceeded 분기 마지막에 호출 — purple 십자가 가장 위.
        public void RenderDatumFindResult(HWindow window, DatumConfig datum)
        {
            if (window == null || datum == null) return;
            if (!datum.LastFindSucceeded) return; //260503 hbk Phase 17 D-13 — find 성공 분기에서만 렌더
            try
            {
                //260503 hbk Phase 17 D-13 — purple DispCross size=14 lineWidth=2 (UI-SPEC LOCKED)
                HOperatorSet.SetColor(window, "purple");
                HOperatorSet.SetLineWidth(window, 2);
                const double crossHalf = 14.0; //260503 hbk Phase 17 D-13
                HOperatorSet.DispLine(window,
                    datum.DetectedOriginRow - crossHalf, datum.DetectedOriginCol,
                    datum.DetectedOriginRow + crossHalf, datum.DetectedOriginCol); //260503 hbk Phase 17 D-13
                HOperatorSet.DispLine(window,
                    datum.DetectedOriginRow, datum.DetectedOriginCol - crossHalf,
                    datum.DetectedOriginRow, datum.DetectedOriginCol + crossHalf); //260503 hbk Phase 17 D-13

                //260503 hbk Phase 17 D-13 — 좌표 텍스트 "Find (row, col)"
                EnsureFontInitialized(window);
                HOperatorSet.SetTposition(window,
                    datum.DetectedOriginRow - crossHalf - 22,
                    datum.DetectedOriginCol + crossHalf + 4); //260503 hbk Phase 17 D-13
                HOperatorSet.WriteString(window,
                    "Find (" + datum.DetectedOriginRow.ToString("F1") + ", "
                             + datum.DetectedOriginCol.ToString("F1") + ")"); //260503 hbk Phase 17 D-13

                //260503 hbk Phase 17 D-13 — DetectedRefAngle 방향 화살표 (DrawDirectionArrow 패턴 inlined, length=20, head=5)
                double angle  = datum.DetectedRefAngle; //260503 hbk Phase 17 D-13
                double aLen   = 20.0; //260503 hbk Phase 17 D-13
                double headLn = 5.0; //260503 hbk Phase 17 D-13
                double endRow = datum.DetectedOriginRow + aLen * System.Math.Sin(angle); //260503 hbk Phase 17 D-13
                double endCol = datum.DetectedOriginCol + aLen * System.Math.Cos(angle); //260503 hbk Phase 17 D-13
                HOperatorSet.DispLine(window, datum.DetectedOriginRow, datum.DetectedOriginCol, endRow, endCol); //260503 hbk Phase 17 D-13
                double a1 = angle + 2.5, a2 = angle - 2.5; //260503 hbk Phase 17 D-13
                HOperatorSet.DispLine(window, endRow, endCol,
                    endRow + headLn * System.Math.Sin(a1), endCol + headLn * System.Math.Cos(a1)); //260503 hbk Phase 17 D-13
                HOperatorSet.DispLine(window, endRow, endCol,
                    endRow + headLn * System.Math.Sin(a2), endCol + headLn * System.Math.Cos(a2)); //260503 hbk Phase 17 D-13
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
                //260509 hbk Phase 20 — ?: → if/else (D-01)
                HTuple font;
                if (fonts.TupleLength() > 0)
                {
                    font = fonts.TupleSelect(0) + "-18";
                }
                else
                {
                    font = new HTuple("mono-18");
                }
                window.SetFont(font);
                //260519 hbk #6-b — 전역 폰트 문자열 캐시 저장 (DrawRoiLabelAt 원복용)
                _normalFontName = font.S;
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

        //260429 hbk Phase 16 D-01 — 원 ROI 그린 직후 알고리즘이 사용할 strip 사각형을 정적으로 시각화.
        //  VisionAlgorithmService.TryFindCircleByPolarSampling 의 strip 생성 식 (Phase 14-04 D-13 보존) 을 그대로 미러링.
        //  알고리즘 canonical (VisionAlgorithmService.cs line 282-285):
        //    rectRow = CircleROI_Row - Radius * Sin(thetaRad)   (화면 CCW 좌표계 — Phase 14-04 D-13 코멘트 참조)
        //    rectCol = CircleROI_Col + Radius * Cos(thetaRad)
        //    rectPhi = thetaRad
        //  length1 = Radius * RectL1Ratio (반경 방향), length2 = Radius * RectL2Ratio (접선 방향). fill 없음 — DispLine 외곽선만.
        //260503 hbk Phase 17 hotfix#6 — Phase 17 D-01 (strip 1개) 정책 폐기. stepCount 만큼 360° 전부 표시 → Circle 검출 디버깅 시 어느 각도에서 실패하는지 사용자가 시각적으로 확인 가능. UAT Test 1 spec 변경 (carry-over).
        private static void RenderCircleStripOverlay(HWindow window, DatumConfig datum)
        {
            if (datum == null) return;
            if (datum.CircleROI_Radius <= 0) return;
            double stepDeg = datum.Circle_PolarStepDeg;
            //260429 hbk Phase 16 D-01 — 0/음수 division 방지 + CONTEXT D-01: 1°~30° 범위 가드 (데이터 모델 보존)
            if (stepDeg < 1.0) stepDeg = 1.0;
            if (stepDeg > 30.0) stepDeg = 30.0;
            //260503 hbk Phase 17 hotfix#6 — stepCount = 360 / stepDeg (기본 36개 @ 10°). 알고리즘 canonical 식 미러.
            int stepCount = (int)Math.Round(360.0 / stepDeg);
            if (stepCount < 1) stepCount = 1;
            double stepRad = (2.0 * Math.PI) / stepCount;

            double radius  = datum.CircleROI_Radius;
            double centerR = datum.CircleROI_Row;
            double centerC = datum.CircleROI_Col;
            //260429 hbk Phase 16 D-01 — 반경 방향 / 접선 방향 길이
            //260430 hbk Quick 260430-hox — 12px cap (VisionAlgorithmService.TryFindCircleByPolarSampling 와 동일). Phase 16 UAT FAIL root cause: 큰 radius/ratio → strip 화면 폭만큼 거대 → 시각화 의미 상실 + 알고리즘 fail.
            double length1 = Math.Min(radius * datum.Circle_RectL1Ratio, 12.0);
            double length2 = Math.Min(radius * datum.Circle_RectL2Ratio, 12.0);
            //260429 hbk Phase 16 — 1px 미만이면 시각화 의미 없음, 자동 floor
            if (length1 < 1.0) length1 = 1.0;
            if (length2 < 1.0) length2 = 1.0;

            try
            {
                //260505 hbk Phase 18 CO-05 — per-strip 성공/실패 색상 분기 (D-15)
                bool[] successes = datum.CircleStripSuccesses; //260505 hbk Phase 18 CO-05
                HOperatorSet.SetLineWidth(window, 1);
                //260503 hbk Phase 17 hotfix#6 — stepCount 만큼 0°, stepDeg°, 2*stepDeg°, ... 360° (한 바퀴) 모두 그림.
                for (int i = 0; i < stepCount; i++)
                {
                    //260505 hbk Phase 18 CO-05 — green=성공, red=실패, gray=데이터 없음(fallback)
                    string stripColor = "gray"; //260505 hbk Phase 18 CO-05
                    //260505 hbk Phase 18 CO-05
                    //260505 hbk Phase 18 CO-05
                    //260509 hbk Phase 20 — ?: → if/else (D-01, Phase 18 CO-05 의미 보존)
                    if (successes != null && i < successes.Length)
                    {
                        if (successes[i])
                        {
                            stripColor = "green";
                        }
                        else
                        {
                            stripColor = "red";
                        }
                    }
                    HOperatorSet.SetColor(window, stripColor); //260505 hbk Phase 18 CO-05
                    double thetaRad = i * stepRad;
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
                //260505 hbk Phase 18 CO-05 — 루프 완료 후 SetColor 상태 복원 (Halcon Window 전역 상태 오염 방지)
                HOperatorSet.SetColor(window, "gray"); //260505 hbk Phase 18 CO-05
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

            //260509 hbk Phase 20 — ?: → if/else (D-01)
            string color;
            int lineWidth;
            if (isSelected)
            {
                color = "cyan";
                lineWidth = 3;
            }
            else
            {
                color = "blue";
                lineWidth = 2;
            }

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

                    //260425 hbk Phase 13 D-VIZ-05 — 5 ROI raw 검출 에지점 (있을 때만) — ROI 별 색상 구분
                    //260429 hbk Phase 16 D-08 — z-order 정렬: Raw edge points 먼저 그린 후 검출 원 + center cross (top) 로 center 가 가려지지 않게.
                    RenderRawEdgePoints(window, datum.Line1_DetectedEdgeRows,        datum.Line1_DetectedEdgeCols,        "cyan");
                    RenderRawEdgePoints(window, datum.Line2_DetectedEdgeRows,        datum.Line2_DetectedEdgeCols,        "magenta");
                    //260425 hbk Phase 13 D-VIZ-05 — Circle raw 검출 에지점 (yellow size=6) — Phase 16 D-07 로 회색 size=4 변경
                    //260429 hbk Phase 16 D-07 — Circle raw points = 회색 작은 십자가 size=4 (검출 trace 용, Phase 13-05 의 yellow 와 시각 구분)
                    RenderRawEdgePoints(window, datum.Circle_DetectedEdgeRows,       datum.Circle_DetectedEdgeCols,       "gray", 4.0);
                    RenderRawEdgePoints(window, datum.Horizontal_A_DetectedEdgeRows, datum.Horizontal_A_DetectedEdgeCols, "green");
                    RenderRawEdgePoints(window, datum.Horizontal_B_DetectedEdgeRows, datum.Horizontal_B_DetectedEdgeCols, "lime green");
                    //260426 hbk Phase 14-03 Req 3 — Vertical 그룹 raw 점 (Line1 cyan 과 시각 구분: orange 신규 — 미사용 색상)
                    RenderRawEdgePoints(window, datum.Vertical_DetectedEdgeRows,     datum.Vertical_DetectedEdgeCols,     "orange");

                    //260424 hbk Phase 12 D-13 — CircleTwoHorizontal 검출 원 오버레이 (노란 원 + 빨간 중심 십자) — 색상 / 사이즈 Phase 16 재정의
                    //260429 hbk Phase 16 D-05 — 검출 원 색상 yellow → "light green" (검출 성공 = 녹색 컨벤션)
                    //260429 hbk Phase 16 D-06 — Center cross 색상 red → "yellow", 6px → 12px (정밀 원 center 가 최종 목적, 가장 두드러지게)
                    //260429 hbk Phase 16 D-08 — z-order: 검출 원 그린 후 center cross (top) — center 가 가려지지 않게.
                    if (datum.AlgorithmTypeEnum == EDatumAlgorithm.CircleTwoHorizontal
                        && datum.CircleDetected_Radius > 0)
                    {
                        //260429 hbk Phase 16 D-05 — 검출 원 = 녹색 (light green)
                        //260430 hbk Quick 260430 — "light green" 비표준 색상명 → HALCON SetColor 예외 → catch swallow → 검출 원 + center cross 둘 다 미표시 결함. hex "#90EE90" 으로 교체 (사용자 의도 보존).
                        HOperatorSet.SetColor(window, "#90EE90");
                        HOperatorSet.SetLineWidth(window, 2);
                        HOperatorSet.DispCircle(window,
                            datum.CircleCenter_Row, datum.CircleCenter_Col, datum.CircleDetected_Radius);

                        //260429 hbk Phase 16 D-06 — Center cross = 노란색 + size=12 + line width 3 (굵기 강조)
                        HOperatorSet.SetColor(window, "yellow");
                        HOperatorSet.SetLineWidth(window, 3);
                        const double circleCenterCrossHalf = 12.0; //260429 hbk Phase 16 D-06 — 6 → 12px (size=12, CONTEXT D-06)
                        HOperatorSet.DispLine(window,
                            datum.CircleCenter_Row - circleCenterCrossHalf, datum.CircleCenter_Col,
                            datum.CircleCenter_Row + circleCenterCrossHalf, datum.CircleCenter_Col);
                        HOperatorSet.DispLine(window,
                            datum.CircleCenter_Row, datum.CircleCenter_Col - circleCenterCrossHalf,
                            datum.CircleCenter_Row, datum.CircleCenter_Col + circleCenterCrossHalf);
                    }

                    //260503 hbk Phase 17 D-13 — z-stack last: DetectedOrigin purple cross 가 가장 위에 그려짐 (LastFindSucceeded gate 는 RenderDatumFindResult 내부)
                    RenderDatumFindResult(window, datum); //260503 hbk Phase 17 D-13
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
        //260519 hbk #6-b — 라벨 폰트 30% 축소 (전역 폰트 영향 0: 렌더 후 원복)
        private void DrawRoiLabelAt(HWindow window, double row, double col, string label)
        {
            try
            {
                EnsureFontInitialized(window);
                //260519 hbk #6-b — 라벨 전용 축소 폰트 (~70% of 18 = 13): "-18" → "-13" 치환
                if (!string.IsNullOrEmpty(_normalFontName))
                {
                    string smallFont = _normalFontName.Replace("-18", "-13");
                    HOperatorSet.SetFont(window, smallFont);
                }
                HOperatorSet.SetColor(window, "yellow");
                HOperatorSet.SetTposition(window, row, col);
                HOperatorSet.WriteString(window, label);
                //260519 hbk #6-b — 전역 폰트 원복 (좌표/메시지 텍스트 회귀 방지)
                if (!string.IsNullOrEmpty(_normalFontName))
                {
                    HOperatorSet.SetFont(window, _normalFontName);
                }
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














