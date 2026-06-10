using System;
using System.Collections.Generic;
using System.Text;
using HalconDotNet;
using ReringProject.Halcon.Models;
using ReringProject.Network;
using ReringProject.Setting;

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
        /// off-screen 버퍼 윈도우에 원본 이미지와 FAI 오버레이를 렌더한 후 DumpWindowImage 로 캡쳐 HImage 를 반환한다.
        /// 실패 시 null 반환 — 검사 스레드 생존 보장, PNG 만 누락.
        /// </summary>
        public HImage RenderToHImage(HImage image, List<EdgeInspectionOverlay> overlays) //260610 hbk Phase 40.2
        {
            if (image == null) return null;
            HTuple win = null;
            try
            {
                image.GetImageSize(out HTuple w, out HTuple h);
                HOperatorSet.OpenWindow(0, 0, w, h, 0, "buffer", "", out win); //260610 hbk Phase 40.2 — off-screen 버퍼 윈도우 (UI HWND 불필요)
                HWindow hwin = new HWindow(win.IP); //260610 hbk Phase 40.2 — HalconDisplayService.Render 가 HWindow 타입 요구
                HOperatorSet.SetPart(win, 0, 0, h - 1, w - 1); //260610 hbk Phase 40.2 — 전체 이미지 매핑
                //260610 hbk Phase 40.2 — stateful→지역new: 인스턴스 필드 공유 방지 (EnsureFontInitialized 가 window 별 초기화를 인스턴스 필드에 기록)
                new HalconDisplayService().Render(hwin, image, null, null, null, overlays, null); //260610 hbk Phase 40.2 — 기존 오버레이 렌더 재사용 (순서: image→overlays)
                HObject dumped;
                HOperatorSet.DumpWindowImage(out dumped, win); //260610 hbk Phase 40.2 — 윈도우 내용을 HImage 로 덤프
                return new HImage(dumped); //260610 hbk Phase 40.2 — 호출부로 소유권 이전
            }
            catch (Exception ex)
            {
                Logging.PrintErrLog((int)ELogType.Error, "[OverlayCaptureRenderer] capture render failed: " + ex.Message); //260610 hbk Phase 40.2 — 캡쳐 실패 = PNG 누락만, 검사 계속
                return null;
            }
            finally
            {
                if (win != null) //260610 hbk Phase 40.2 — 버퍼 윈도우 누수 방지 (필수)
                {
                    try { HOperatorSet.CloseWindow(win); } catch { }
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
