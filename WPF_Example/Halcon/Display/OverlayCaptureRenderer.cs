using System;
using System.Collections.Generic;
using System.Text;
using HalconDotNet;
using ReringProject.Halcon.Models;
using ReringProject.Setting;
using ReringProject.Utility;

namespace ReringProject.Halcon.Display
{
    // off-screen 버퍼 윈도우에서 측정 오버레이가 입혀진 캡쳐 HImage 를 생성하는 렌더러.
    // UI HWND 없이 시퀀스 스레드에서 직접 호출 가능.
    public class OverlayCaptureRenderer
    {
        public OverlayCaptureRenderer()
        {
        }

        /// <summary>
        /// off-screen 버퍼 윈도우에 원본 이미지(disp_obj)와 FAI 오버레이(리전 disp_obj)를 그린 뒤
        /// DumpWindowImage 로 캡쳐 HImage 를 반환한다. 실패 시 null — 검사 스레드 생존 보장, PNG 만 누락.
        /// </summary>
        // dump_window_image 는 DispLine 등 벡터 그래픽을 캡쳐하지 못하고 disp_obj 로 표시한
        // iconic 오브젝트(이미지/리전)만 캡쳐한다. 따라서 오버레이를 리전으로 변환해 disp_obj 로 그린다.
        // 순서: disp_obj(image) → disp_obj(region) → dump. 텍스트(OK/NG) 라벨은 리전화 불가로 제외.
        public HImage RenderToHImage(HImage image, List<EdgeInspectionOverlay> overlays, List<DatumCaptureOverlay> datumOverlays)
        {
            if (image == null) return null;
            HWindow hwin = null;
            try
            {
                image.GetImageSize(out HTuple w, out HTuple h);
                // HALCON 24.11 modern handle mode 대응: HWindow 객체 직접 생성(핸들 number 변환 제거).
                hwin = new HWindow(0, 0, w.I, h.I, 0, "buffer", "");
                // graphics_stack=true 없으면 dump 시 마지막 disp_obj 만 남고 앞서 그린 리전들이 소거된다.
                // 스택을 켜면 image+모든 리전이 draw 순서대로 재생되어 전부 보존된다.
                hwin.SetWindowParam("graphics_stack", "true");
                hwin.SetPart(0, 0, h.I - 1, w.I - 1);
                hwin.DispObj(image);
                // 기준선 길이 = 이미지 대각(전체 가로지름)
                double axisHalf = System.Math.Sqrt((double)w.I * w.I + (double)h.I * h.I);
                DrawDatumRegions(hwin, datumOverlays, axisHalf);
                DrawOverlayRegions(hwin, overlays);
                return hwin.DumpWindowImage(); // 소유권 호출부 이전
            }
            catch (Exception ex)
            {
                Logging.PrintErrLog((int)ELogType.Error, "[OverlayCaptureRenderer] capture render failed: " + ex.Message);
                return null;
            }
            finally
            {
                if (hwin != null) // 버퍼 윈도우 누수 방지
                {
                    try { hwin.CloseWindow(); } catch { }
                }
            }
        }

        // 색상 규칙은 HalconDisplayService.Render 의 FAI 오버레이 색상과 일치: FAI-Edge*(녹/적),
        // FAI-DistLine(청록), FAI-EdgeRaw(노랑 점), 그 외(파랑). FAI-Edge* 검출점 X 마커는 magenta.
        private const double LineThicknessRadius = 2.0; // 에지/마커 리전 두께(dilation 반경)
        private const double DistLineThicknessRadius = 3.1; // 측정 거리선(cyan) 두께(사용자 요청, 2.6→3.1)
        private const double MarkerHalfSize = 8.0; // X 마커 반길이(HalconDisplayService size=8.0 일치)
        private const double DatumRingThickness = 6.0; // datum 검출 원 링 두께(px), pale green 가시성↑
        private const double DatumCircleCenterCrossHalf = 12.0; // 원 중심 십자 반길이(UI L913 일치)
        private const double DatumOriginCrossHalf = 20.0; // 검출 원점 십자 반길이(UI L318 일치)

        // z-order: 두꺼운 cyan 거리선이 마지막에 그려지면 OK(녹) 에지선을 덮으므로 레이어 순서를 명시한다.
        // ①배경(거리선 cyan/blue + raw 노랑점) → ②검출점 X 마커(magenta) → ③FAI-Edge 측정선(녹/적)을 맨 위에.
        private static void DrawOverlayRegions(HWindow hwin, List<EdgeInspectionOverlay> overlays)
        {
            if (overlays == null) return;

            // ① 배경 레이어: 거리선(cyan)/기타(blue)/raw 점(노랑). FAI-Edge 마커/라인은 이후 레이어.
            foreach (var ov in overlays)
            {
                if (ov == null || string.IsNullOrEmpty(ov.RoiId)) continue;
                if (ov.RoiId.StartsWith("FAI-Edge", StringComparison.OrdinalIgnoreCase)) continue;

                if (string.Equals(ov.RoiId, "FAI-DistLine", StringComparison.OrdinalIgnoreCase))
                {
                    DrawLineAsRegion(hwin, ov.LineRow1, ov.LineColumn1, ov.LineRow2, ov.LineColumn2, "cyan", DistLineThicknessRadius);
                }
                else
                {
                    DrawLineAsRegion(hwin, ov.LineRow1, ov.LineColumn1, ov.LineRow2, ov.LineColumn2, "blue", LineThicknessRadius);
                }
            }

            // ② FAI-EdgeRaw 노랑 점 (배경 위)
            foreach (var ov in overlays)
            {
                if (ov == null || ov.RoiId == null) continue;
                if (string.Equals(ov.RoiId, "FAI-EdgeRaw", StringComparison.OrdinalIgnoreCase))
                {
                    DrawPointsAsRegion(hwin, ov.Points, "yellow");
                }
            }

            // ③ FAI-Edge 검출점 X 마커(magenta) → 그 위에 측정선(녹/적). 측정선이 cyan·마커보다 위.
            foreach (var ov in overlays)
            {
                if (ov == null || ov.RoiId == null) continue;
                if (!ov.RoiId.StartsWith("FAI-Edge", StringComparison.OrdinalIgnoreCase)) continue;
                if (string.Equals(ov.RoiId, "FAI-EdgeRaw", StringComparison.OrdinalIgnoreCase)) continue; // Raw 는 ②에서 처리

                if (ov.Points != null && ov.Points.Count > 0)
                {
                    DrawPointsAsRegion(hwin, ov.Points, "magenta"); // 검출점 X 마커
                }
                string lineColor;
                if (ov.RoiId.EndsWith("-NG", StringComparison.OrdinalIgnoreCase)) lineColor = "red";
                else lineColor = "green";
                DrawLineAsRegion(hwin, ov.LineRow1, ov.LineColumn1, ov.LineRow2, ov.LineColumn2, lineColor, LineThicknessRadius); // 측정선 최상위
            }
        }

        // 선분 1개를 리전으로 변환(gen_region_line→dilation)해 disp_obj. radius 로 두께 조절.
        private static void DrawLineAsRegion(HWindow hwin, double r1, double c1, double r2, double c2, string color, double radius)
        {
            HObject region = null, dilated = null;
            try
            {
                HOperatorSet.GenRegionLine(out region, r1, c1, r2, c2);
                HOperatorSet.DilationCircle(region, out dilated, radius);
                hwin.SetColor(color); // 표준 색상명만 사용(비표준명 예외 회피)
                hwin.DispObj(dilated);
            }
            catch { /* 단일 오버레이 렌더 실패는 무시 — 전체 캡쳐 보존 */ }
            finally
            {
                if (region != null) { try { region.Dispose(); } catch { } }
                if (dilated != null) { try { dilated.Dispose(); } catch { } }
            }
        }

        // 검출점들을 X 마커 리전으로 변환해 disp_obj.
        private static void DrawPointsAsRegion(HWindow hwin, List<EdgeInspectionPoint> points, string color)
        {
            if (points == null || points.Count == 0) return;
            try { hwin.SetColor(color); } catch { }
            foreach (var p in points)
            {
                if (p == null) continue;
                HObject l1 = null, l2 = null, u = null, dilated = null;
                try
                {
                    HOperatorSet.GenRegionLine(out l1, p.Row - MarkerHalfSize, p.Column - MarkerHalfSize, p.Row + MarkerHalfSize, p.Column + MarkerHalfSize);
                    HOperatorSet.GenRegionLine(out l2, p.Row - MarkerHalfSize, p.Column + MarkerHalfSize, p.Row + MarkerHalfSize, p.Column - MarkerHalfSize);
                    HOperatorSet.Union2(l1, l2, out u);
                    HOperatorSet.DilationCircle(u, out dilated, LineThicknessRadius);
                    hwin.DispObj(dilated);
                }
                catch { /* 단일 점 실패 무시 */ }
                finally
                {
                    if (l1 != null) { try { l1.Dispose(); } catch { } }
                    if (l2 != null) { try { l2.Dispose(); } catch { } }
                    if (u != null) { try { u.Dispose(); } catch { } }
                    if (dilated != null) { try { dilated.Dispose(); } catch { } }
                }
            }
        }

        // datum 검출 오버레이(녹색 원 + 중심/원점 십자)를 리전으로 표시.
        // UI 의 핵심 검출 결과만 캡쳐(녹색 원 #90EE90 + 노랑 중심 십자 + slate blue 원점 십자).
        // 전체 datum ROI/strip/label/arrow 재현은 범위 외(측정 결과 시각화 목적).
        private static void DrawDatumRegions(HWindow hwin, List<DatumCaptureOverlay> datums, double axisHalfLength)
        {
            if (datums == null) return;
            foreach (var d in datums)
            {
                if (d == null) continue;
                // datum 기준선(축): 원점 통과, 각도 방향, 이미지 대각 길이. UI Line1=yellow, Line2=cyan 일치.
                if (d.HasOrigin && d.HasAxis1)
                {
                    DrawDatumAxisLine(hwin, d.OriginRow, d.OriginCol, d.Axis1AngleRad, axisHalfLength, "yellow"); // 1차 기준선
                }
                if (d.HasOrigin && d.HasAxis2)
                {
                    DrawDatumAxisLine(hwin, d.OriginRow, d.OriginCol, d.Axis2AngleRad, axisHalfLength, "cyan"); // 2차(수직) 기준선
                }
                if (d.HasCircle && d.CircleRadius > 0)
                {
                    DrawCircleRingAsRegion(hwin, d.CircleRow, d.CircleCol, d.CircleRadius, "#90EE90"); // 검출 원(녹색, UI L905 일치)
                    DrawCrossAsRegion(hwin, d.CircleRow, d.CircleCol, DatumCircleCenterCrossHalf, "yellow"); // 원 중심 십자(UI L911 일치)
                }
                if (d.HasOrigin)
                {
                    DrawCrossAsRegion(hwin, d.OriginRow, d.OriginCol, DatumOriginCrossHalf, "slate blue"); // 검출 원점 십자
                }
            }
        }

        // 원점 통과 기준선을 방향벡터(sinφ,cosφ)로 ±half 산출 후 리전 표시 (VisionAlgorithmService.GetDatumAxisLine 동일식).
        private static void DrawDatumAxisLine(HWindow hwin, double originRow, double originCol, double angleRad, double half, string color)
        {
            double dirR = System.Math.Sin(angleRad);
            double dirC = System.Math.Cos(angleRad);
            DrawLineAsRegion(hwin,
                originRow - half * dirR, originCol - half * dirC,
                originRow + half * dirR, originCol + half * dirC,
                color, LineThicknessRadius);
        }

        // 원 외곽선을 링 리전(outer−inner)으로 만들어 disp_obj.
        private static void DrawCircleRingAsRegion(HWindow hwin, double row, double col, double radius, string color)
        {
            HObject outer = null, inner = null, ring = null;
            try
            {
                double innerR = radius - DatumRingThickness;
                if (innerR < 1.0) innerR = 1.0;
                HOperatorSet.GenCircle(out outer, row, col, radius);
                HOperatorSet.GenCircle(out inner, row, col, innerR);
                HOperatorSet.Difference(outer, inner, out ring);
                hwin.SetColor(color);
                hwin.DispObj(ring);
            }
            catch { /* 단일 datum 원 실패 무시 */ }
            finally
            {
                if (outer != null) { try { outer.Dispose(); } catch { } }
                if (inner != null) { try { inner.Dispose(); } catch { } }
                if (ring != null) { try { ring.Dispose(); } catch { } }
            }
        }

        // 축 정렬 십자(＋)를 리전으로 만들어 disp_obj.
        private static void DrawCrossAsRegion(HWindow hwin, double row, double col, double half, string color)
        {
            HObject l1 = null, l2 = null, u = null, dilated = null;
            try
            {
                HOperatorSet.GenRegionLine(out l1, row - half, col, row + half, col);
                HOperatorSet.GenRegionLine(out l2, row, col - half, row, col + half);
                HOperatorSet.Union2(l1, l2, out u);
                HOperatorSet.DilationCircle(u, out dilated, LineThicknessRadius);
                hwin.SetColor(color);
                hwin.DispObj(dilated);
            }
            catch { /* 십자 실패 무시 */ }
            finally
            {
                if (l1 != null) { try { l1.Dispose(); } catch { } }
                if (l2 != null) { try { l2.Dispose(); } catch { } }
                if (u != null) { try { u.Dispose(); } catch { } }
                if (dilated != null) { try { dilated.Dispose(); } catch { } }
            }
        }

        /// <summary>
        /// FAI 오버레이 목록에서 FAI-Edge* 검출점 개수로 측정점 segment 를 구성한다.
        /// 점 0 → "", 점 1 → "P1", 점 2 → "P1P2" 형식.
        /// </summary>
        public static string BuildMeasurePointSegment(List<EdgeInspectionOverlay> overlays)
        {
            if (overlays == null) return "";
            int pointCount = 0;
            foreach (var ov in overlays)
            {
                if (ov == null || ov.Points == null) continue;
                if (string.IsNullOrEmpty(ov.RoiId)) continue;
                if (!ov.RoiId.StartsWith("FAI-Edge", StringComparison.OrdinalIgnoreCase)) continue;
                // 1점=1오버레이 가정(FAI-Edge1/2 각각 별도 오버레이, Points 단일점). 다점 오버레이면 pointCount += ov.Points.Count 로 교체 필요.
                if (ov.Points.Count > 0) pointCount++;
            }
            if (pointCount <= 0) return "";
            var sb = new StringBuilder();
            for (int i = 1; i <= pointCount; i++) { sb.Append("P"); sb.Append(i); }
            return sb.ToString();
        }
    }
}
