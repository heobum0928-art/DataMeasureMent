using System;
using System.Collections.Generic;
using System.Text;
using HalconDotNet;
using ReringProject.Halcon.Models;
using ReringProject.Setting;
using ReringProject.Utility;

namespace ReringProject.Halcon.Display
{
    //260610 hbk Phase 40.2 — off-screen 버퍼 윈도우에서 HalconDisplayService.Render 를 헤드리스 재사용하여
    //  측정 오버레이가 입혀진 캡쳐 HImage 를 생성하는 렌더러.
    //  UI HWND 없이 시퀀스 스레드에서 직접 호출 가능.
    public class OverlayCaptureRenderer
    {
        //260610 hbk Phase 40.2 — 스레드 안전성: stateful→지역new
        //  HalconDisplayService 는 _isFontInitialized/_normalFontName 인스턴스 필드를 보유하며
        //  Render() 에서 EnsureFontInitialized(window) 를 통해 인스턴스 상태를 변경한다(stateful).
        //  RenderToHImage 는 호출마다 지역 new HalconDisplayService() 를 사용하여 인스턴스 상태 공유를 방지.
        //  (단일 시퀀스 스레드에서 FAI 루프가 직렬 호출되므로 즉시 손상 위험은 낮으나 방어적 설계 적용.)

        public OverlayCaptureRenderer() //260610 hbk Phase 40.2
        {
        }

        /// <summary>
        /// off-screen 버퍼 윈도우에 원본 이미지(disp_obj)와 FAI 오버레이(리전 disp_obj)를 그린 뒤
        /// DumpWindowImage 로 캡쳐 HImage 를 반환한다. 실패 시 null — 검사 스레드 생존 보장, PNG 만 누락.
        /// </summary>
        //260610 hbk Phase 40.2 hotfix CO-40.2-06 — 오버레이 캡쳐 누락 근본 수정.
        //  dump_window_image 는 DispLine 등 "벡터 그래픽"을 캡쳐하지 못하고 disp_obj 로 표시한
        //  iconic 오브젝트(이미지/리전)만 캡쳐한다(도메인 가이드). 따라서 HalconDisplayService.Render(DispLine 기반)
        //  재사용을 폐기하고, 오버레이를 리전으로 변환해 disp_obj 로 그린다. 순서: disp_obj(image) → disp_obj(region) → dump.
        //  텍스트(OK/NG) 라벨은 리전화 불가로 현 단계 제외(차기 burn-in 검토).
        public HImage RenderToHImage(HImage image, List<EdgeInspectionOverlay> overlays) //260610 hbk Phase 40.2
        {
            if (image == null) return null;
            HWindow hwin = null; //260610 hbk Phase 40.2 hotfix CO-40.2-01
            try
            {
                image.GetImageSize(out HTuple w, out HTuple h);
                //260610 hbk Phase 40.2 hotfix CO-40.2-01 — HALCON 24.11 modern handle mode 대응: HWindow 객체 직접 생성(핸들 number 변환 제거).
                hwin = new HWindow(0, 0, w.I, h.I, 0, "buffer", ""); //260610 hbk Phase 40.2 hotfix — off-screen 버퍼 HWindow
                //260610 hbk Phase 40.2 hotfix CO-40.2-07 — graphics_stack=true 없으면 dump 시 마지막 disp_obj 만 남고
                //  앞서 그린 리전들이 소거된다(=마지막 cyan DistLine 만 보임). 스택을 켜면 image+모든 리전이
                //  draw 순서대로 재생되어 전부 보존된다.
                hwin.SetWindowParam("graphics_stack", "true"); //260610 hbk Phase 40.2 hotfix CO-40.2-07
                hwin.SetPart(0, 0, h.I - 1, w.I - 1); //260610 hbk Phase 40.2 — 전체 이미지 매핑
                hwin.DispObj(image); //260610 hbk Phase 40.2 hotfix CO-40.2-06 — 배경 이미지(iconic) 먼저 표시
                DrawOverlayRegions(hwin, overlays); //260610 hbk Phase 40.2 hotfix CO-40.2-06 — 오버레이를 리전(disp_obj)로 표시
                return hwin.DumpWindowImage(); //260610 hbk Phase 40.2 — 윈도우 내용을 HImage 로 덤프 (소유권 호출부 이전)
            }
            catch (Exception ex)
            {
                Logging.PrintErrLog((int)ELogType.Error, "[OverlayCaptureRenderer] capture render failed: " + ex.Message); //260610 hbk Phase 40.2 — 캡쳐 실패 = PNG 누락만, 검사 계속
                return null;
            }
            finally
            {
                if (hwin != null) //260610 hbk Phase 40.2 — 버퍼 윈도우 누수 방지 (필수)
                {
                    try { hwin.CloseWindow(); } catch { }
                }
            }
        }

        //260610 hbk Phase 40.2 hotfix CO-40.2-06 — 오버레이를 리전으로 변환해 disp_obj 로 표시(dump 캡쳐 가능).
        //  색상 규칙은 HalconDisplayService.Render 의 FAI 오버레이 색상과 일치: FAI-Edge*(녹/적), FAI-DistLine(청록),
        //  FAI-EdgeRaw(노랑 점), 그 외(파랑). FAI-Edge* 검출점 X 마커는 magenta.
        private const double LineThicknessRadius = 2.0; //260610 hbk Phase 40.2 hotfix CO-40.2-06 — 에지/마커 리전 두께(dilation 반경)
        private const double DistLineThicknessRadius = 3.1; //260610 hbk Phase 40.2 hotfix CO-40.2-09 — 측정 거리선(cyan) 추가 20%↑(사용자 요청, 2.6→3.1)
        private const double MarkerHalfSize = 8.0; //260610 hbk Phase 40.2 hotfix CO-40.2-06 — X 마커 반길이(HalconDisplayService size=8.0 일치)

        //260610 hbk Phase 40.2 hotfix CO-40.2-10 — z-order 재설계로 OK(녹) 에지선 가림 해결.
        //  원인: 오버레이 리스트 순서가 [Edge1, Edge2, DistLine] → cyan 거리선이 마지막(맨 위)에 그려져
        //  두꺼운 cyan(3.1)이 OK 에지선(녹)을 덮었다. (NG 적색선은 cyan 밖으로 더 길게 뻗어 보였음.)
        //  해결: 레이어 순서를 명시 — ①배경(거리선 cyan/blue + raw 노랑점) → ②검출점 X 마커(magenta)
        //  → ③FAI-Edge 측정선(녹/적)을 맨 위에. 측정 OK/NG 색이 항상 보이도록.
        private static void DrawOverlayRegions(HWindow hwin, List<EdgeInspectionOverlay> overlays) //260610 hbk Phase 40.2 hotfix CO-40.2-06
        {
            if (overlays == null) return;

            // ① 배경 레이어: 거리선(cyan)/기타(blue)/raw 점(노랑). FAI-Edge 마커/라인은 이후 레이어.
            foreach (var ov in overlays)
            {
                if (ov == null || string.IsNullOrEmpty(ov.RoiId)) continue;
                if (ov.RoiId.StartsWith("FAI-Edge", StringComparison.OrdinalIgnoreCase)) continue; //260610 hbk Phase 40.2 hotfix CO-40.2-10 — FAI-Edge(및 FAI-EdgeRaw)는 별도 레이어

                if (string.Equals(ov.RoiId, "FAI-DistLine", StringComparison.OrdinalIgnoreCase))
                {
                    DrawLineAsRegion(hwin, ov.LineRow1, ov.LineColumn1, ov.LineRow2, ov.LineColumn2, "cyan", DistLineThicknessRadius); //260610 hbk Phase 40.2 hotfix CO-40.2-10
                }
                else
                {
                    DrawLineAsRegion(hwin, ov.LineRow1, ov.LineColumn1, ov.LineRow2, ov.LineColumn2, "blue", LineThicknessRadius); //260610 hbk Phase 40.2 hotfix CO-40.2-10
                }
            }

            // ② FAI-EdgeRaw 노랑 점 (배경 위)
            foreach (var ov in overlays)
            {
                if (ov == null || ov.RoiId == null) continue;
                if (string.Equals(ov.RoiId, "FAI-EdgeRaw", StringComparison.OrdinalIgnoreCase))
                {
                    DrawPointsAsRegion(hwin, ov.Points, "yellow"); //260610 hbk Phase 40.2 hotfix CO-40.2-10
                }
            }

            // ③ FAI-Edge 검출점 X 마커(magenta) → 그 위에 측정선(녹/적). 측정선이 cyan·마커보다 위.
            foreach (var ov in overlays)
            {
                if (ov == null || ov.RoiId == null) continue;
                if (!ov.RoiId.StartsWith("FAI-Edge", StringComparison.OrdinalIgnoreCase)) continue;
                if (string.Equals(ov.RoiId, "FAI-EdgeRaw", StringComparison.OrdinalIgnoreCase)) continue; //260610 hbk Phase 40.2 hotfix CO-40.2-10 — Raw 는 ②에서 처리

                if (ov.Points != null && ov.Points.Count > 0)
                {
                    DrawPointsAsRegion(hwin, ov.Points, "magenta"); //260610 hbk Phase 40.2 hotfix CO-40.2-10 — 검출점 X 마커
                }
                string lineColor = ov.RoiId.EndsWith("-NG", StringComparison.OrdinalIgnoreCase) ? "red" : "green"; //260610 hbk Phase 40.2 hotfix CO-40.2-10
                DrawLineAsRegion(hwin, ov.LineRow1, ov.LineColumn1, ov.LineRow2, ov.LineColumn2, lineColor, LineThicknessRadius); //260610 hbk Phase 40.2 hotfix CO-40.2-10 — 측정선 최상위
            }
        }

        //260610 hbk Phase 40.2 hotfix CO-40.2-06/07 — 선분 1개를 리전으로 변환(gen_region_line→dilation)해 disp_obj. radius 로 두께 조절.
        private static void DrawLineAsRegion(HWindow hwin, double r1, double c1, double r2, double c2, string color, double radius)
        {
            HObject region = null, dilated = null;
            try
            {
                HOperatorSet.GenRegionLine(out region, r1, c1, r2, c2);
                HOperatorSet.DilationCircle(region, out dilated, radius);
                hwin.SetColor(color); //260610 hbk Phase 40.2 hotfix CO-40.2-06 — 표준 색상명만 사용(비표준명 예외 회피)
                hwin.DispObj(dilated);
            }
            catch { /* 단일 오버레이 렌더 실패는 무시 — 전체 캡쳐 보존 */ }
            finally
            {
                if (region != null) { try { region.Dispose(); } catch { } }
                if (dilated != null) { try { dilated.Dispose(); } catch { } }
            }
        }

        //260610 hbk Phase 40.2 hotfix CO-40.2-06 — 검출점들을 X 마커 리전으로 변환해 disp_obj.
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

        /// <summary>
        /// FAI 오버레이 목록에서 FAI-Edge* 검출점 개수로 측정점 segment 를 구성한다.
        /// 점 0 → "", 점 1 → "P1", 점 2 → "P1P2" 형식.
        /// </summary>
        public static string BuildMeasurePointSegment(List<EdgeInspectionOverlay> overlays) //260610 hbk Phase 40.2
        {
            if (overlays == null) return "";
            int pointCount = 0;
            foreach (var ov in overlays)
            {
                if (ov == null || ov.Points == null) continue;
                if (string.IsNullOrEmpty(ov.RoiId)) continue;
                if (!ov.RoiId.StartsWith("FAI-Edge", StringComparison.OrdinalIgnoreCase)) continue;
                if (ov.Points.Count > 0) pointCount++; //260610 hbk Phase 40.2 — 1점=1오버레이 가정 (FAIEdgeMeasurementService BuildOverlaysBoth/Single 확인: FAI-Edge1/2 각각 별도 오버레이, Points 단일점). 다점 오버레이면 pointCount += ov.Points.Count 로 교체 필요
            }
            if (pointCount <= 0) return "";
            var sb = new StringBuilder();
            for (int i = 1; i <= pointCount; i++) { sb.Append("P"); sb.Append(i); } //260610 hbk Phase 40.2 — 1→"P1", 2→"P1P2"
            return sb.ToString();
        }
    }
}
