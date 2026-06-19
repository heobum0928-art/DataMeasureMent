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
        // 전역 폰트 문자열 캐시 (DrawRoiLabelAt 축소 폰트 원복용)
        private string _normalFontName;
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
                    string roiColor;
                    int roiWidth;
                    if (roi.Id == selectedRoiId)
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

                    // Circle ROI 렌더링 (명시 Shape이 Polygon 감지보다 우선)
                    if (roi.Shape == RoiShape.Circle)
                    {
                        string circleColor;
                        int circleWidth;
                        if (roi.Id == selectedRoiId)
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

                        // polar strip 시각화 (CircleCenterDistance polar 모드일 때만 StepDeg > 0)
                        // 파라미터(StepDeg/RectL1/L2Ratio) 수정 → ToRoiDefinition 재생성 → 여기서 즉시 반영.
                        if (roi.CirclePolarStepDeg > 0)
                        {
                            RenderCircleStrips(window, roi.CenterRow, roi.CenterCol, roi.Radius,
                                roi.CirclePolarStepDeg, roi.CircleRectL1Ratio, roi.CircleRectL2Ratio, null);
                            window.SetColor(circleColor); // strip 후 색상 복원
                            window.SetLineWidth(circleWidth);
                        }

                        // Center cross marker (6px, red) — UI-SPEC Circle ROI center marker
                        window.SetColor("red");
                        window.SetLineWidth(2);
                        window.DispLine(roi.CenterRow - 6, roi.CenterCol, roi.CenterRow + 6, roi.CenterCol);
                        window.DispLine(roi.CenterRow, roi.CenterCol - 6, roi.CenterRow, roi.CenterCol + 6);
                        // Circle ROI 명칭 라벨 (원 상단 외곽)
                        if (!string.IsNullOrEmpty(roi.Name))
                            DrawRoiLabelAt(window, roi.CenterRow - roi.Radius - 22, roi.CenterCol, roi.Name);
                        continue;
                    }

                    if (!string.IsNullOrEmpty(roi.PolygonPoints))
                    {
                        var pts = ParsePolygonPoints(roi.PolygonPoints);
                        if (pts != null && pts.Count >= 3)
                            RenderPolygon(window, pts, roiColor, roiWidth);
                        // Polygon ROI 명칭 라벨 (첫 점 기준 위쪽)
                        if (!string.IsNullOrEmpty(roi.Name) && pts != null && pts.Count > 0)
                            DrawRoiLabelAt(window, pts[0].Y - 22, pts[0].X, roi.Name);
                    }
                    else if (roi.Row1 != 0 || roi.Column1 != 0 || roi.Row2 != 0 || roi.Column2 != 0)
                    {
                        DrawRectangleOutline(window, roi.Row1, roi.Column1, roi.Row2, roi.Column2);
                        // Rectangle ROI 명칭 라벨 (좌상단 외곽 위쪽)
                        if (!string.IsNullOrEmpty(roi.Name))
                            DrawRoiLabelAt(window, roi.Row1 - 22, roi.Column1, roi.Name);
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
                    // FAI-EdgeRaw: strip-loop 누적 raw 에지점 일괄 가시화 (노랑 작은 +).
                    //  반드시 "FAI-Edge" StartsWith 분기보다 먼저 평가되어야 함 — "FAI-EdgeRaw" 도 prefix 매칭.
                    //  렌더 후 continue → 기본 DispLine + 큰 X 마커 loop 모두 skip.
                    else if (string.Equals(overlay.RoiId, "FAI-EdgeRaw", StringComparison.OrdinalIgnoreCase))
                    {
                        if (overlay.Points != null && overlay.Points.Count > 0)
                        {
                            try
                            {
                                HTuple rRows = new HTuple();
                                HTuple rCols = new HTuple();
                                foreach (var p in overlay.Points)
                                {
                                    rRows = rRows.TupleConcat(p.Row);
                                    rCols = rCols.TupleConcat(p.Column);
                                }
                                HOperatorSet.SetColor(window, "yellow");
                                HOperatorSet.SetLineWidth(window, 1);
                                HOperatorSet.DispCross(window, rRows, rCols, 4.0, 0.0);
                            }
                            catch
                            {
                                // RenderRawEdgePoints 관습 — display 예외 swallow
                            }
                        }
                        continue;
                    }
                    // X 마커 색 분리용: FAI-Edge* 라인은 녹/적(OK/NG), X 는 white 로 구분.
                    bool isFaiEdgeLine = false;
                    // FAI edge measurement result overlay colors
                    if (overlay.RoiId != null && overlay.RoiId.StartsWith("FAI-Edge", StringComparison.OrdinalIgnoreCase))
                    {
                        bool isNG = overlay.RoiId.EndsWith("-NG", StringComparison.OrdinalIgnoreCase);
                        if (isNG)
                        {
                            window.SetColor("red");
                        }
                        else
                        {
                            window.SetColor("green");
                        }
                        window.SetLineWidth(2);
                        isFaiEdgeLine = true;
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

                    // FAI-Edge* 의 X 마커만 라인과 분리된 색상으로 (검출된 점 위치를 라인과 시각적으로 분리)
                    if (isFaiEdgeLine)
                    {
                        window.SetColor("magenta"); // (was: white)
                        window.SetLineWidth(2);
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

        // Circle 드래그 미리보기 (rubber-band, 빨강)
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

        // RenderDatumFindResult: DetectedOrigin transient 기반 시각화 (검출 origin 십자 + DetectedRefAngle 화살표).
        //  LastFindSucceeded gate: TryFindDatum 성공 분기에서만 렌더 (catch/조기 return 시 자동 미렌더).
        //  Z-stack: RenderDatumOverlay 의 LastTeachSucceeded 분기 마지막에 호출 — 십자가 가장 위.
        public void RenderDatumFindResult(HWindow window, DatumConfig datum)
        {
            if (window == null || datum == null) return;
            //260619 hbk Phase 56 — LastFindSucceeded OR 유효 DetectedOrigin 이면 렌더. 결과화면 datum 은 검사시 검출좌표는 유효한데
            //  LastFind 플래그가 false 인 경우 있음(복원/재티칭 경로 게이트) → 좌표 유효성으로 게이트 완화. (0,0)=휘발/미검출만 skip.
            if (!datum.LastFindSucceeded && datum.DetectedOriginRow == 0.0 && datum.DetectedOriginCol == 0.0) return;
            try
            {
                // 검출 origin 십자. RenderDatumOverlay 의 RefOrigin 십자(고정 15~20px)와 동일 방식.
                //  "purple" 는 HALCON 유효 색상명 아님 → SetColor 예외 → catch swallow → 십자 전체 미표시. "slate blue" 로 교체.
                HOperatorSet.SetColor(window, "slate blue");
                HOperatorSet.SetLineWidth(window, 2);
                const double crossHalf = 20.0; // teach 오버레이와 동일 고정 크기
                HOperatorSet.DispLine(window,
                    datum.DetectedOriginRow - crossHalf, datum.DetectedOriginCol,
                    datum.DetectedOriginRow + crossHalf, datum.DetectedOriginCol);
                HOperatorSet.DispLine(window,
                    datum.DetectedOriginRow, datum.DetectedOriginCol - crossHalf,
                    datum.DetectedOriginRow, datum.DetectedOriginCol + crossHalf);

                // 좌표 텍스트 "Find (row, col)"
                EnsureFontInitialized(window);
                HOperatorSet.SetTposition(window,
                    datum.DetectedOriginRow - crossHalf - 15,
                    datum.DetectedOriginCol + 5); // teach "Datum Origin" 라벨과 동일 offset
                HOperatorSet.WriteString(window,
                    "Find (" + datum.DetectedOriginRow.ToString("F1") + ", "
                             + datum.DetectedOriginCol.ToString("F1") + ")");

                // DetectedRefAngle 방향 화살표 (고정 크기)
                double angle  = datum.DetectedRefAngle;
                double aLen   = 30.0;
                double headLn = 8.0;
                double endRow = datum.DetectedOriginRow + aLen * System.Math.Sin(angle);
                double endCol = datum.DetectedOriginCol + aLen * System.Math.Cos(angle);
                HOperatorSet.DispLine(window, datum.DetectedOriginRow, datum.DetectedOriginCol, endRow, endCol);
                double a1 = angle + 2.5, a2 = angle - 2.5;
                HOperatorSet.DispLine(window, endRow, endCol,
                    endRow + headLn * System.Math.Sin(a1), endCol + headLn * System.Math.Cos(a1));
                HOperatorSet.DispLine(window, endRow, endCol,
                    endRow + headLn * System.Math.Sin(a2), endCol + headLn * System.Math.Cos(a2));

                //260619 hbk Phase 56 — datum 기준선 slate blue 굵게(측정 ROI cyan/라임과 구분). 방향규약 = EdgeToLineDistance 측정축과 동일.
                //  길이 = 이미지 전체(세로 0~높이, 가로 0~너비) 걸치도록(사용자 요청 2026-06-19): GetPart 로 표시 이미지 대각선 산출
                //  → ±대각선(어느 origin 위치/각도든 전 이미지 관통, DispLine 창밖 자동클립). 직전 ±7000px 는 14208px 이미지서 원중심(~8600px)까지 못 닿음.
                //260619 hbk Phase 57 #3 datum 색상 slate blue 통일 (magenta→slate blue recolor, 길이/좌표 무변경 D-10)
                HOperatorSet.SetColor(window, "slate blue");
                HOperatorSet.SetLineWidth(window, 3);
                double datumLineHalf = 20000.0; // GetPart 실패 시 폴백(현 이미지 대각선 17750 초과)
                try
                {
                    HTuple gpR1, gpC1, gpR2, gpC2;
                    HOperatorSet.GetPart(window, out gpR1, out gpC1, out gpR2, out gpC2);
                    double partH = System.Math.Abs(gpR2.D - gpR1.D) + 1.0;
                    double partW = System.Math.Abs(gpC2.D - gpC1.D) + 1.0;
                    double partDiag = System.Math.Sqrt(partH * partH + partW * partW);
                    if (partDiag > 1.0) datumLineHalf = partDiag;
                }
                catch { /* GetPart 실패 → 폴백 길이 유지 */ }
                // 수평 기준선 = DetectedRefAngle 방향(부품 틸트 반영), 교점 통과.
                double hSin = System.Math.Sin(datum.DetectedRefAngle), hCos = System.Math.Cos(datum.DetectedRefAngle);
                HOperatorSet.DispLine(window,
                    datum.DetectedOriginRow - datumLineHalf * hSin, datum.DetectedOriginCol - datumLineHalf * hCos,
                    datum.DetectedOriginRow + datumLineHalf * hSin, datum.DetectedOriginCol + datumLineHalf * hCos);
                //260619 hbk Phase 56 — 수직 기준선: CTH(원검출 datum)는 교점(DetectedOrigin)↔원중심(DetectedCircle) 잇는 직선 → 교점·원중심 둘 다 확실히 통과(사용자 요구).
                //  그 외 datum 은 검출 수직 기준각(DetectedRefAngle2) 방향. 둘 다 교점 피벗·이미지 전체 길이. ※'RefAngle+90°+원중심피벗' 은 교점 빗나가 회귀 → 금지.
                double vDirRow, vDirCol;
                if (datum.DetectedCircleRow != 0.0 || datum.DetectedCircleCol != 0.0)
                {
                    vDirRow = datum.DetectedCircleRow - datum.DetectedOriginRow;
                    vDirCol = datum.DetectedCircleCol - datum.DetectedOriginCol;
                }
                else
                {
                    vDirRow = System.Math.Sin(datum.DetectedRefAngle2);
                    vDirCol = System.Math.Cos(datum.DetectedRefAngle2);
                }
                double vDirLen = System.Math.Sqrt(vDirRow * vDirRow + vDirCol * vDirCol);
                if (vDirLen > 1e-6)
                {
                    double vur = vDirRow / vDirLen, vuc = vDirCol / vDirLen;
                    HOperatorSet.DispLine(window,
                        datum.DetectedOriginRow - datumLineHalf * vur, datum.DetectedOriginCol - datumLineHalf * vuc,
                        datum.DetectedOriginRow + datumLineHalf * vur, datum.DetectedOriginCol + datumLineHalf * vuc);
                }

                // ExpectedAngleDeg 점선 화살표 (AngleTolerance > 0 sentinel 활성 시에만).
                //  status==None 일 때는 호출 안 함 → 점선 화살표 미표시.
                if (datum.AngleTolerance > 0.0)
                {
                    DrawExpectedAngleArrow(window, datum.DetectedOriginRow, datum.DetectedOriginCol,
                                           datum.ExpectedAngleDeg * System.Math.PI / 180.0, // deg → rad
                                           datum.AngleValidationStatus);
                }
            }
            catch
            {
                // Suppress display errors (기존 RenderDatumOverlay / RenderCircleDraft catch 관습 유지)
            }
        }

        // Expected angle 점선 화살표 (DetectedRefAngle 실선 화살표와 시각 구분).
        //  PASS = 두 화살표 시각적 일치 (green) / FAIL = 시각적 어긋남 (red). status==None 일 때는 본 메서드 호출 안 됨 (호출자 게이트).
        //  Halcon 점선 = HOperatorSet.SetLineStyle(window, new HTuple(10, 5)). 호출 직후 빈 HTuple 로 즉시 해제 (다른 렌더 영향 0).
        private void DrawExpectedAngleArrow(HWindow window, double originRow, double originCol, double expectedAngleRad, ReringProject.Sequence.EAngleValidationStatus status)
        {
            try
            {
                string color;
                if (status == ReringProject.Sequence.EAngleValidationStatus.Pass) color = "green";
                else                                                              color = "red";
                HOperatorSet.SetColor(window, color);
                HOperatorSet.SetLineWidth(window, 2);
                HOperatorSet.SetLineStyle(window, new HTuple(10, 5)); // 점선 (10px on, 5px off)
                double aLen = 45.0; // 검출 실선(30px) 보다 길게 (고정 크기)
                double endRow = originRow + aLen * System.Math.Sin(expectedAngleRad);
                double endCol = originCol + aLen * System.Math.Cos(expectedAngleRad);
                HOperatorSet.DispLine(window, originRow, originCol, endRow, endCol);
                // arrow head (검출 화살표와 동일 패턴)
                double a1 = expectedAngleRad + 2.5;
                double a2 = expectedAngleRad - 2.5;
                double headLn = 10.0;
                HOperatorSet.DispLine(window, endRow, endCol, endRow + headLn * System.Math.Sin(a1), endCol + headLn * System.Math.Cos(a1));
                HOperatorSet.DispLine(window, endRow, endCol, endRow + headLn * System.Math.Sin(a2), endCol + headLn * System.Math.Cos(a2));
                HOperatorSet.SetLineStyle(window, new HTuple()); // 점선 해제 (다른 렌더 영향 0)
            }
            catch { /* Suppress display errors */ }
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
                // 전역 폰트 문자열 캐시 저장 (DrawRoiLabelAt 원복용)
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

        //260619 hbk Phase 56 Wave 2 — 결과 화면 보정(회전) ROI 박스 표시 전용 렌더. 편집 채널(_rois)과 무관 → 드래그/write-back 없음.
        //  측정과 100% 동일하게 HALCON rectangle2 로 그림(코너 수동계산 시 회전 규약 어긋나 반대로 보이던 문제 제거).
        //  rect 인자 = {row, col, phi, length1, length2} (TryFitLine 의 gen_measure_rectangle2 와 동일 순서/규약).
        public void RenderResultRoiBoxes(HWindow window, IList<double[]> rects, string color, int lineWidth)
        {
            if (window == null || rects == null) return;
            try
            {
                HOperatorSet.SetColor(window, color);
                HOperatorSet.SetLineWidth(window, lineWidth);
                HOperatorSet.SetDraw(window, "margin"); // 외곽선 (datum ROI rectangle2 렌더와 동일)
                foreach (double[] r in rects)
                {
                    if (r == null) continue;
                    if (r.Length == 3) HOperatorSet.DispCircle(window, r[0], r[1], r[2]);                       // {row,col,radius} 원 ROI
                    else if (r.Length >= 5) HOperatorSet.DispRectangle2(window, r[0], r[1], r[2], r[3], r[4]); // {row,col,phi,l1,l2} 사각 ROI
                }
            }
            catch { /* suppress display errors (기존 렌더 catch 관습 유지) */ }
        }

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

        // 검출 라인 외삽 거리 (HALCON DispLine 자동 클리핑 활용; 30K~50K 이미지에서도 충분)
        private const double EXTEND_PX = 10000.0;

        // 두 점 (r1,c1)-(r2,c2) 를 unit-vector × EXTEND_PX 로 양쪽 외삽 후 DispLine
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

        // raw 검출 에지점들을 작은 cross 마커로 일괄 렌더
        //  rows/cols 가 null 이거나 length 0 이면 no-op (안전).
        //  size 기본 6 px, line width 1. HALCON DispCross batch: rows/cols HTuple 일괄 처리.
        //  Circle 호출처에서만 4.0 + "gray" override.
        private static void RenderRawEdgePoints(HWindow window, HTuple rows, HTuple cols, string color, double size = 6.0)
        {
            if (rows == null || cols == null) return;
            int n = rows.TupleLength();
            if (n == 0 || cols.TupleLength() != n) return;
            try
            {
                HOperatorSet.SetColor(window, color);
                HOperatorSet.SetLineWidth(window, 1);
                HOperatorSet.DispCross(window, rows, cols, size, 0.0);
            }
            catch
            {
                // Suppress display errors (RenderDatumOverlay catch 관습)
            }
        }

        // 원 ROI 그린 직후 알고리즘이 사용할 strip 사각형을 정적으로 시각화.
        //  VisionAlgorithmService.TryFindCircleByPolarSampling 의 strip 생성 식을 그대로 미러링.
        //  알고리즘 canonical (VisionAlgorithmService.cs line 282-285):
        //    rectRow = CircleROI_Row - Radius * Sin(thetaRad)   (화면 CCW 좌표계)
        //    rectCol = CircleROI_Col + Radius * Cos(thetaRad)
        //    rectPhi = thetaRad
        //  length1 = Radius * RectL1Ratio (반경 방향), length2 = Radius * RectL2Ratio (접선 방향). fill 없음 — DispLine 외곽선만.
        //  stepCount 만큼 360° 전부 표시 → Circle 검출 디버깅 시 어느 각도에서 실패하는지 시각적으로 확인 가능.
        private static void RenderCircleStripOverlay(HWindow window, DatumConfig datum)
        {
            if (datum == null) return;
            if (datum.CircleROI_Radius <= 0) return;
            // primitive 공용 렌더러로 위임 (Datum/FAI circle 공유)
            RenderCircleStrips(window,
                datum.CircleROI_Row, datum.CircleROI_Col, datum.CircleROI_Radius,
                datum.Circle_PolarStepDeg, datum.Circle_RectL1Ratio, datum.Circle_RectL2Ratio,
                datum.CircleStripSuccesses);
        }

        // FAI CircleDiameter Strip preview (Edit 모드 = FAI 노드 선택 시).
        //  테스트 시에는 원만 표시, edit 시 사각형 표시 — strip 사각형은 검사 overlay 가 아닌 preview 경로.
        //  successes=null → 회색 strip (parameter preview, 검출 데이터 없음).
        //  Polar 경로 (Circle_RadialDirection != "") 한정 호출 — fit 경로는 strip 미사용.
        public void RenderFaiCircleStripPreview(HWindow window,
            double centerR, double centerC, double radius,
            double stepDeg, double l1Ratio, double l2Ratio,
            HTuple datumTransform)
        {
            if (window == null) return;
            if (radius <= 0) return;
            // datum transform 적용 (TryFindCircleByPolarSampling 내부 변환 미러)
            double tR = centerR, tC = centerC;
            if (datumTransform != null && datumTransform.Length > 0)
            {
                try
                {
                    HTuple rT, cT;
                    HOperatorSet.AffineTransPoint2d(datumTransform, centerR, centerC, out rT, out cT);
                    tR = rT.D; tC = cT.D;
                }
                catch { /* identity fallback */ }
            }
            RenderCircleStrips(window, tR, tC, radius, stepDeg, l1Ratio, l2Ratio, null /* preview = gray */);
        }

        // primitive 파라미터 strip 렌더러 (Datum CircleConfig / FAI CircleCenterDistance 공용).
        //  successes != null 이면 per-strip green/red, null 이면 전부 gray (정적 preview — 파라미터 수정 시 즉시 반영).
        //  strip 생성 식은 VisionAlgorithmService.TryFindCircleByPolarSampling canonical 미러 (-sin/+cos, 화면 CCW).
        private static void RenderCircleStrips(HWindow window,
            double centerR, double centerC, double radius,
            double stepDeg, double l1Ratio, double l2Ratio, bool[] successes)
        {
            if (radius <= 0) return;
            // 0/음수 division 방지 + 1°~30° 범위 가드
            if (stepDeg < 1.0) stepDeg = 1.0;
            if (stepDeg > 30.0) stepDeg = 30.0;
            int stepCount = (int)Math.Round(360.0 / stepDeg);
            if (stepCount < 1) stepCount = 1;
            double stepRad = (2.0 * Math.PI) / stepCount;

            // strip half-extent cap (VisionAlgorithmService 와 공유 — WYSIWYG)
            double length1 = Math.Min(radius * l1Ratio, ReringProject.Halcon.Algorithms.VisionAlgorithmService.CircleStripHalfExtentCapPx);
            double length2 = Math.Min(radius * l2Ratio, ReringProject.Halcon.Algorithms.VisionAlgorithmService.CircleStripHalfExtentCapPx);
            if (length1 < 1.0) length1 = 1.0;
            if (length2 < 1.0) length2 = 1.0;

            try
            {
                HOperatorSet.SetLineWidth(window, 1);
                for (int i = 0; i < stepCount; i++)
                {
                    // green=성공, red=실패, gray=데이터 없음(fallback)
                    string stripColor = "gray";
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
                    HOperatorSet.SetColor(window, stripColor);
                    double thetaRad = i * stepRad;
                    // 알고리즘 canonical 식 미러 (VisionAlgorithmService line 282-285, -sin/+cos)
                    double rectRow = centerR - radius * Math.Sin(thetaRad);
                    double rectCol = centerC + radius * Math.Cos(thetaRad);
                    double rectPhi = thetaRad;
                    // fill 없는 외곽선만: 4 corner 좌표 직접 계산 후 DispLine 4 회 (DispObj GenRectangle2 는 fill 됨)
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
                // 루프 완료 후 SetColor 상태 복원 (Halcon Window 전역 상태 오염 방지)
                HOperatorSet.SetColor(window, "gray");
            }
            catch
            {
                // RenderDatumOverlay 의 catch 컨벤션 유지 (display 에러 무시)
            }
        }

        /// <summary>Renders Datum Line1/Line2 ROI rectangles and reference origin cross on HWindow.</summary>
        // Datum CTH Edit 모드 분리: isEditMode 옵션 인자. 기본값 false 로 기존 호출자 호환.
        public void RenderDatumOverlay(HWindow window, DatumConfig datum, bool isSelected, bool isEditMode = false)
        {
            if (datum == null) return;

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

                // CTH 평소 모드 (LastTeachSucceeded + !isEditMode) 시 Horizontal_A/B + Circle ROI 사각형 핸들 hide.
                //  fitting 원 + DetectedOrigin 십자는 LastTeachSucceeded 블록 + RefOrigin 블록에서 별도 그림 — 본 가드 영향 없음.
                //  TwoLineIntersect / VerticalTwoHorizontal 등 다른 algorithm 에는 영향 0 (CircleTwoHorizontal 한정).
                bool cthHideRois = (datum.AlgorithmTypeEnum == EDatumAlgorithm.CircleTwoHorizontal)
                                && datum.LastTeachSucceeded
                                && !isEditMode;

                // RenderDatumOverlay 슬롯 분기: AlgorithmType 별로 그릴 슬롯을 분기.
                //    TwoLineIntersect       → Line1_*  ("L1" 라벨)
                //    VerticalTwoHorizontal  → Vertical_* ("Vert" 라벨)
                //    CircleTwoHorizontal    → 둘 다 미사용 (legacy INI 의 Line1_* 잔류값이 잘못 렌더되지 않음)
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
                // DualImage 도 Vertical 슬롯 렌더 필요.
                else if (datum.AlgorithmTypeEnum == EDatumAlgorithm.VerticalTwoHorizontal
                      || datum.AlgorithmTypeEnum == EDatumAlgorithm.VerticalTwoHorizontalDualImage)
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

                // Line2 Rectangle2 는 TwoLineIntersect 에서만 렌더 (Circle/Vertical-TwoHorizontal 은 Line2 미사용)
                if (datum.AlgorithmTypeEnum == EDatumAlgorithm.TwoLineIntersect
                    && datum.Line2_Length1 > 0 && datum.Line2_Length2 > 0)
                {
                    HOperatorSet.DispRectangle2(window,
                        datum.Line2_Row, datum.Line2_Col, datum.Line2_Phi,
                        datum.Line2_Length1, datum.Line2_Length2);

                    // "L2" 라벨
                    DrawRoiLabel(window, datum.Line2_Row, datum.Line2_Col, datum.Line2_Phi,
                        datum.Line2_Length1, datum.Line2_Length2, "L2");
                }

                // Circle ROI 검색 영역 (CircleTwoHorizontal 일 때만 렌더, Line1/Line2 와 동일 색)
                //  cthHideRois 가드: CTH Edit 모드 OFF + 티칭 완료 시 Circle ROI + Strip 시각화 hide
                if (datum.AlgorithmTypeEnum == EDatumAlgorithm.CircleTwoHorizontal
                    && datum.CircleROI_Radius > 0
                    && !cthHideRois)
                {
                    HOperatorSet.SetColor(window, color);
                    HOperatorSet.SetLineWidth(window, lineWidth);
                    HOperatorSet.DispCircle(window,
                        datum.CircleROI_Row, datum.CircleROI_Col, datum.CircleROI_Radius);

                    // "Circle" 라벨 (원 위쪽 외곽 바로 바깥)
                    DrawRoiLabelAt(window,
                        datum.CircleROI_Row - datum.CircleROI_Radius - 22,
                        datum.CircleROI_Col - datum.CircleROI_Radius,
                        "Circle");

                    // pre-teach Strip 사각형 stepCount 개 정적 시각화 (z-order: ROI 경계 위)
                    RenderCircleStripOverlay(window, datum);
                }

                // Horizontal A/B ROI Rectangle2 (CircleTwoHorizontal + VerticalTwoHorizontal 공용)
                //  cthHideRois 가드: CTH Edit 모드 OFF + 티칭 완료 시 Horizontal_A/B hide. VTH 는 cthHideRois=false → 영향 0.
                if (datum.AlgorithmTypeEnum != EDatumAlgorithm.TwoLineIntersect
                    && !cthHideRois)
                {
                    HOperatorSet.SetColor(window, color);
                    HOperatorSet.SetLineWidth(window, lineWidth);
                    if (datum.Horizontal_A_Length1 > 0 && datum.Horizontal_A_Length2 > 0)
                    {
                        HOperatorSet.DispRectangle2(window,
                            datum.Horizontal_A_Row, datum.Horizontal_A_Col, datum.Horizontal_A_Phi,
                            datum.Horizontal_A_Length1, datum.Horizontal_A_Length2);

                        // "H-A" 라벨
                        DrawRoiLabel(window, datum.Horizontal_A_Row, datum.Horizontal_A_Col,
                            datum.Horizontal_A_Phi, datum.Horizontal_A_Length1, datum.Horizontal_A_Length2, "H-A");
                    }
                    if (datum.Horizontal_B_Length1 > 0 && datum.Horizontal_B_Length2 > 0)
                    {
                        HOperatorSet.DispRectangle2(window,
                            datum.Horizontal_B_Row, datum.Horizontal_B_Col, datum.Horizontal_B_Phi,
                            datum.Horizontal_B_Length1, datum.Horizontal_B_Length2);

                        // "H-B" 라벨
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

                // 검출 라인 2개 + 교점 오버레이 (TryTeachDatum 성공 시에만, 기존 cyan/blue/magenta 팔레트는 건드리지 않음)
                if (datum.LastTeachSucceeded)
                {
                    // Line1 detected 외삽 (yellow)
                    HOperatorSet.SetColor(window, "yellow");
                    HOperatorSet.SetLineWidth(window, 2);
                    DrawExtendedLine(window,
                        datum.Line1Detected_RBegin, datum.Line1Detected_CBegin,
                        datum.Line1Detected_REnd,   datum.Line1Detected_CEnd);

                    // Line2 detected 외삽 (cyan)
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

                    // 5 ROI raw 검출 에지점 (있을 때만) — ROI 별 색상 구분
                    //  z-order 정렬: Raw edge points 먼저 그린 후 검출 원 + center cross (top) 로 center 가 가려지지 않게.
                    RenderRawEdgePoints(window, datum.Line1_DetectedEdgeRows,        datum.Line1_DetectedEdgeCols,        "cyan");
                    RenderRawEdgePoints(window, datum.Line2_DetectedEdgeRows,        datum.Line2_DetectedEdgeCols,        "magenta");
                    // Circle raw points = 회색 작은 십자가 size=4 (검출 trace 용, yellow 와 시각 구분)
                    RenderRawEdgePoints(window, datum.Circle_DetectedEdgeRows,       datum.Circle_DetectedEdgeCols,       "gray", 4.0);
                    RenderRawEdgePoints(window, datum.Horizontal_A_DetectedEdgeRows, datum.Horizontal_A_DetectedEdgeCols, "green");
                    RenderRawEdgePoints(window, datum.Horizontal_B_DetectedEdgeRows, datum.Horizontal_B_DetectedEdgeCols, "lime green");
                    // Vertical 그룹 raw 점 (Line1 cyan 과 시각 구분: orange)
                    RenderRawEdgePoints(window, datum.Vertical_DetectedEdgeRows,     datum.Vertical_DetectedEdgeCols,     "orange");

                    // CircleTwoHorizontal 검출 원 오버레이 (녹색 원 + 노란 중심 십자)
                    //  z-order: 검출 원 그린 후 center cross (top) — center 가 가려지지 않게.
                    if (datum.AlgorithmTypeEnum == EDatumAlgorithm.CircleTwoHorizontal
                        && datum.CircleDetected_Radius > 0)
                    {
                        // 검출 원 = 녹색. "light green" 비표준 색상명 → HALCON SetColor 예외 → catch swallow → 미표시 결함. hex "#90EE90" 으로 교체.
                        HOperatorSet.SetColor(window, "#90EE90");
                        HOperatorSet.SetLineWidth(window, 2);
                        HOperatorSet.DispCircle(window,
                            datum.CircleCenter_Row, datum.CircleCenter_Col, datum.CircleDetected_Radius);

                        // Center cross = 노란색 + size=12 + line width 3 (굵기 강조)
                        HOperatorSet.SetColor(window, "yellow");
                        HOperatorSet.SetLineWidth(window, 3);
                        const double circleCenterCrossHalf = 12.0;
                        HOperatorSet.DispLine(window,
                            datum.CircleCenter_Row - circleCenterCrossHalf, datum.CircleCenter_Col,
                            datum.CircleCenter_Row + circleCenterCrossHalf, datum.CircleCenter_Col);
                        HOperatorSet.DispLine(window,
                            datum.CircleCenter_Row, datum.CircleCenter_Col - circleCenterCrossHalf,
                            datum.CircleCenter_Row, datum.CircleCenter_Col + circleCenterCrossHalf);
                    }

                }

                // RenderDatumFindResult 를 LastTeachSucceeded 블록 밖에서 호출.
                //  검출 십자는 자체 LastFindSucceeded 게이트(메서드 내부)만 따르면 충분 → 레시피 로드/swap 후(teach 미수행) Test Find 결과도 표시. z-stack last 유지.
                RenderDatumFindResult(window, datum);

                // Datum 검출 실패 시 'DETECT FAIL' 적색 라벨 렌더.
                //  분기: RuntimeDetectFailed (게이트 발동) OR (IsConfigured && !LastFindSucceeded) (티칭 한 경우 fallback).
                //  색상: "red" 표준명 (비표준명은 SetColor catch swallow 로 silent 미표시 위험).
                if (datum.RuntimeDetectFailed || (datum.IsConfigured && !datum.LastFindSucceeded))
                {
                    try
                    {
                        EnsureFontInitialized(window);
                        HOperatorSet.SetColor(window, "red");
                        // 위치: 이미지 오른쪽 상단 (GetPart 로 현재 표시 영역 좌표 얻기).
                        //  datum 이름 hash 기반 row stagger 로 여러 datum 동시 실패 시 라벨 겹침 회피 (6단계 25px 간격).
                        HTuple partRow1, partCol1, partRow2, partCol2;
                        HOperatorSet.GetPart(window, out partRow1, out partCol1, out partRow2, out partCol2);
                        string datumNameKey = datum.DatumName;
                        if (datumNameKey == null) datumNameKey = "";
                        int hashStagger = System.Math.Abs((datumNameKey.GetHashCode()) % 6) * 25; // 0/25/50/75/100/125 중 하나
                        double labelRow = (double)partRow1.D + 20.0 + hashStagger; // 상단 20px + stagger
                        double labelCol = (double)partCol2.D - 280.0; // 오른쪽 가장자리에서 280px 안쪽 (라벨 길이 고려)
                        HOperatorSet.SetTposition(window, labelRow, labelCol);
                        string datumLabel = datum.DatumName;
                        if (datumLabel == null) datumLabel = "Datum";
                        HOperatorSet.WriteString(window, "DETECT FAIL: " + datumLabel);
                    }
                    catch
                    {
                        // Suppress display errors (기존 RenderDatumOverlay catch 컨벤션)
                    }
                }
            }
            catch
            {
                // Suppress display errors
            }
        }

        // Datum ROI 라벨 그리기 (수직/수평/라인 구분 가시화)
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

        // 주어진 (row, col) 에 yellow 텍스트 라벨 렌더 (Circle ROI 등 비-Rectangle 용)
        //  라벨 폰트 30% 축소 (전역 폰트 영향 0: 렌더 후 원복)
        private void DrawRoiLabelAt(HWindow window, double row, double col, string label)
        {
            try
            {
                EnsureFontInitialized(window);
                // 라벨 전용 축소 폰트 (~70% of 18 = 13): "-18" → "-13" 치환
                if (!string.IsNullOrEmpty(_normalFontName))
                {
                    string smallFont = _normalFontName.Replace("-18", "-13");
                    HOperatorSet.SetFont(window, smallFont);
                }
                HOperatorSet.SetColor(window, "yellow");
                HOperatorSet.SetTposition(window, row, col);
                HOperatorSet.WriteString(window, label);
                // 전역 폰트 원복 (좌표/메시지 텍스트 회귀 방지)
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

        // PolygonPoints 문자열 파싱 ("x1,y1;x2,y2;..." → List<Point>)
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














