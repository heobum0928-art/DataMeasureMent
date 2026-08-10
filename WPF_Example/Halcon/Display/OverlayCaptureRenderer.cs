using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using HalconDotNet;
using ReringProject.Halcon.Models;
using ReringProject.Setting;
using ReringProject.Utility;

namespace ReringProject.Halcon.Display
{
    // FAI 마다 오버레이가 입혀진 캡쳐 HImage 를 생성하는 렌더러. UI HWND 없이 시퀀스/저장 워커 스레드에서
    //  직접 호출 가능.
    // 260810 hbk quick-debug(capture-render-per-fai-slow) round7: HWindow(disp_obj+DumpWindowImage) 기반
    //  구현을 완전히 폐기했다. 창을 재사용해도(round1) 매 FAI 마다 127MP 전체를 그리고(disp_obj) 다시
    //  통째로 읽어오는(DumpWindowImage) 왕복 자체가 실제 비용이었다(실기 300-660ms/FAI, 파일 용량(3MB)과
    //  무관 — 스크래치 벤치마크로 확정). 대신 R/G/B 채널로 분해해 각 채널에 overpaint_region(리전 크기만큼만
    //  비용 발생하는 in-place 픽셀 페인트, 127MP 왕복 없음)으로 오버레이를 찍은 뒤 재합성한다 — 127MP 기준
    //  실측 85-130ms/FAI(기존 대비 4-6배). 이 클래스는 이제 인스턴스 필드를 전혀 갖지 않는(캐시 없음)
    //  완전한 무상태(stateless) 렌더러라 여러 스레드가 같은 인스턴스를 동시에 호출해도 안전하다 — 다만
    //  CaptureImageSaveService 는 round3 에서 도입한 워커별 독립 인스턴스 배열(_renderers[])을 구조적
    //  위험 없이 그대로 유지한다(이 클래스가 stateless 로 바뀌었어도 워커별 인스턴스 자체는 여전히 정확하게
    //  동작함, 굳이 공유 인스턴스로 되돌릴 필요 없음).
    //
    // *** 매우 중요: overpaint_region 은 HALCON 이 공식적으로 "입력을 수정하지 않는다"는 일반 규칙의
    //  예외 3개 중 하나다(호출자가 넘긴 이미지를 in-place 로 직접 변형) — 보통의 copy-on-write 안전망이
    //  전혀 적용되지 않는다. 반드시 아래 순서를 지켜야 한다(스크래치 하네스로 실측 확인된 함정, 순서를
    //  어기면 예외 없이 조용히 다른 채널/다른 FAI/원본 이미지까지 오염될 수 있다):
    //   1) 소스(공유 sharedSrc 등) 전체를 CopyImage() 로 독립 복사한 뒤에만 decompose3 한다 — 원본 보호.
    //   2) decompose3 가 반환한 채널 3개(r0/g0/b0)는 서로 내부 데이터를 공유할 수 있다(1번을 지켜도
    //      마찬가지 — 실측: perFaiCopy 로 원본은 보호돼도 r0/g0/b0 를 그대로 overpaint 하면 채널끼리
    //      서로 오염됨/R=G=B=마지막 칠한 값). decompose3 직후 r0/g0/b0 각각을 다시 CopyImage() 로 독립
    //      복사한 뒤에만 overpaint_region 을 호출해야 한다.
    //   3) overpaint_region 에 3채널 컬러를 HTuple 하나로(new HTuple(new int[]{R,G,B})) 한 번에 넘기면
    //      안 된다 — 실측 확인된 결함: 튜플의 마지막 값이 전체 채널에 다 덮어써진다(예: magenta
    //      (255,0,255) 를 넘기면 결과가 흰색 (255,255,255)로 나옴). 반드시 채널을 나눠 각각 단일
    //      스칼라 grayval 로 3번 호출해야 한다.
    public class OverlayCaptureRenderer
    {
        public OverlayCaptureRenderer()
        {
        }

        /// <summary>
        /// R/G/B 채널로 분해 → 각 채널에 FAI 오버레이(리전)를 overpaint_region 으로 in-place 페인트 →
        /// 재합성한 캡쳐 HImage 를 반환한다. 실패 시 null — 검사/저장 워커 스레드 생존 보장, 캡쳐 이미지만 누락.
        /// </summary>
        public HImage RenderToHImage(HImage image, List<EdgeInspectionOverlay> overlays, List<DatumCaptureOverlay> datumOverlays)
        {
            if (image == null) return null;

            var swTotal = Stopwatch.StartNew();
            var swStage = new Stopwatch();
            long msPrep = 0, msDecompose = 0, msChannelCopy = 0, msPaint = 0, msCompose = 0;

            // 아래 지역변수는 전부 finally 에서 일괄 정리한다(성공/실패 무관 — 반환하는 result 만 예외적으로
            //  finally 직전에 null 로 바꿔 소유권을 호출부로 넘긴다).
            HObject composedMono = null;   // channelCount==1(실카메라 전부 Gray8) 일 때만 생성 — mono -> RGB 복제본
            HImage baseRgbWrap = null;     // composedMono 를 감싸는 wrapper, 또는 이미 3채널이면 image 자체(별도 dispose 불필요)
            bool ownsBaseRgbWrap = false;  // baseRgbWrap 이 이번 호출에서 새로 만든 객체라 우리가 dispose 책임을 지는지
            HImage perFaiCopy = null;      // 1) 소스 전체 독립 복사(원본 보호)
            HObject r0 = null, g0 = null, b0 = null;
            HImage r0Wrap = null, g0Wrap = null, b0Wrap = null;
            HImage r = null, g = null, b = null; // 2) 채널별 독립 복사 — 실제로 overpaint 되는 버퍼
            HObject paintedObj = null;
            HImage result = null;

            try
            {
                int channelCount = image.CountChannels().I;

                swStage.Restart();
                if (channelCount == 1)
                {
                    // 실카메라는 전부 Gray8/mono(Custom/Device/DeviceHandler.cs RegisterCxpCamera 확인) —
                    //  컬러 오버레이를 입히려면 R=G=B 로 3채널 복제해야 한다.
                    HOperatorSet.Compose3(image, image, image, out composedMono);
                    baseRgbWrap = new HImage(composedMono);
                    ownsBaseRgbWrap = true;
                }
                else if (channelCount == 3)
                {
                    baseRgbWrap = image; // 이미 3채널 — image 는 호출자 소유이므로 별도 dispose 하지 않음
                }
                else
                {
                    throw new NotSupportedException(string.Format("capture render: unsupported channel count {0}", channelCount));
                }

                perFaiCopy = baseRgbWrap.CopyImage(); // 1) 원본(공유 sharedSrc 또는 위에서 만든 composedMono) 보호
                msPrep = swStage.ElapsedMilliseconds;

                swStage.Restart();
                HOperatorSet.Decompose3(perFaiCopy, out r0, out g0, out b0);
                msDecompose = swStage.ElapsedMilliseconds;

                swStage.Restart();
                // 2) 채널별 독립 복사 — decompose3 결과끼리 내부 데이터 공유 가능성 차단(필수, 클래스 상단 주석 참고).
                r0Wrap = new HImage(r0);
                g0Wrap = new HImage(g0);
                b0Wrap = new HImage(b0);
                r = r0Wrap.CopyImage();
                g = g0Wrap.CopyImage();
                b = b0Wrap.CopyImage();
                msChannelCopy = swStage.ElapsedMilliseconds;

                swStage.Restart();
                image.GetImageSize(out HTuple w, out HTuple h);
                // 기준선 길이 = 이미지 대각(전체 가로지름) — 기존 window 방식과 동일 계산.
                double axisHalf = System.Math.Sqrt((double)w.I * w.I + (double)h.I * h.I);
                DrawDatumRegions(r, g, b, datumOverlays, axisHalf);
                DrawOverlayRegions(r, g, b, overlays);
                msPaint = swStage.ElapsedMilliseconds;

                swStage.Restart();
                HOperatorSet.Compose3(r, g, b, out paintedObj); // 3) 합쳐서 최종 캡쳐 이미지
                result = new HImage(paintedObj);
                msCompose = swStage.ElapsedMilliseconds;

                Logging.PrintLog((int)ELogType.Trace,
                    "[CaptureRender] prep={0}ms decompose={1}ms channelCopy={2}ms paint={3}ms compose={4}ms total={5}ms",
                    msPrep, msDecompose, msChannelCopy, msPaint, msCompose, swTotal.ElapsedMilliseconds);

                HImage success = result;
                result = null; // finally 에서 dispose 되지 않도록 — 호출부로 소유권 이전
                return success;
            }
            catch (Exception ex)
            {
                Logging.PrintErrLog((int)ELogType.Error, "[OverlayCaptureRenderer] capture render failed: " + ex.Message);
                return null;
            }
            finally
            {
                // 260810 round7: 스크래치 하네스로 실측 확인된 누수 함정 — 아래 중간 산출물(특히 decompose3
                //  결과를 감싸는 wrapper, r0Wrap/g0Wrap/b0Wrap)을 명시적으로 dispose 하지 않으면 FAI 마다
                //  ~490MB 씩 네이티브 메모리가 새어 200회 반복 시 수십 GB 까지 치솟는다(.NET GC 가 뒤늦게
                //  finalizer 로 회수할 때까지 방치됨, 400회 스트레스 테스트로 확인) — 반드시 이름 붙은
                //  변수로 받아 전부 명시적으로 dispose 한다(익명 임시 wrapper 로 남겨두지 않음).
                if (result != null) { try { result.Dispose(); } catch { } }
                if (paintedObj != null) { try { paintedObj.Dispose(); } catch { } }
                if (r != null) { try { r.Dispose(); } catch { } }
                if (g != null) { try { g.Dispose(); } catch { } }
                if (b != null) { try { b.Dispose(); } catch { } }
                if (r0Wrap != null) { try { r0Wrap.Dispose(); } catch { } }
                if (g0Wrap != null) { try { g0Wrap.Dispose(); } catch { } }
                if (b0Wrap != null) { try { b0Wrap.Dispose(); } catch { } }
                if (r0 != null) { try { r0.Dispose(); } catch { } }
                if (g0 != null) { try { g0.Dispose(); } catch { } }
                if (b0 != null) { try { b0.Dispose(); } catch { } }
                if (perFaiCopy != null) { try { perFaiCopy.Dispose(); } catch { } }
                if (ownsBaseRgbWrap && baseRgbWrap != null) { try { baseRgbWrap.Dispose(); } catch { } }
                if (composedMono != null) { try { composedMono.Dispose(); } catch { } }
            }
        }

        /// <summary>
        /// 서비스 종료 시 호출 — 이 렌더러는 이제 인스턴스 상태(캐시된 창 등)를 전혀 갖지 않으므로 정리할
        /// 자원이 없다(no-op). CaptureImageSaveService.Dispose() 의 워커별 정리 호출부는 변경하지 않기
        /// 위해(다른 파일 구조 변경 최소화) 메서드 시그니처는 그대로 유지한다.
        /// </summary>
        public void Dispose()
        {
        }

        // 색상 규칙은 HalconDisplayService.Render 의 FAI 오버레이 색상과 일치: FAI-Edge*(녹/적),
        // FAI-DistLine(청록), FAI-EdgeRaw(노랑 점), 그 외(파랑). FAI-Edge* 검출점 X 마커는 magenta.
        private const double LineThicknessRadius = 2.0; // 에지/마커 리전 두께(dilation 반경)
        private const double DistLineThicknessRadius = 3.1; // 측정 거리선(cyan) 두께(사용자 요청, 2.6→3.1)
        private const double MarkerHalfSize = 8.0; // X 마커 반길이(HalconDisplayService size=8.0 일치)
        private const double DatumRingThickness = 6.0; // datum 검출 원 링 두께(px), pale green 가시성↑
        private const double DatumCircleCenterCrossHalf = 12.0; // 원 중심 십자 반길이(UI L913 일치)
        private const double DatumOriginCrossHalf = 20.0; // 검출 원점 십자 반길이(UI L318 일치)

        // SetColor(colorName) 로 렌더링했을 때와 동일한 RGB 를 재현하기 위해, 실제 HALCON 렌더링 결과에서
        //  직접 추출한 값(HWindow.SetColor+DispObj+DumpWindowImage 왕복 측정, 260810)을 사용한다 — 임의
        //  추정값이 아니다. 이 파일이 실제로 쓰는 8개 색상명만 지원하며, 그 외 이름은 예외를 던져 호출부의
        //  개별 try/catch 가 해당 오버레이 1건만 스킵하게 한다(기존 HWindow.SetColor 의 "비표준 색상명 →
        //  예외 → catch swallow → 미표시" 동작과 동일한 안전 성격 유지).
        private static void ResolveColorRgb(string colorName, out int r, out int g, out int b)
        {
            switch (colorName)
            {
                case "cyan": r = 0; g = 255; b = 255; break;
                case "blue": r = 0; g = 0; b = 255; break;
                case "yellow": r = 255; g = 255; b = 0; break;
                case "magenta": r = 255; g = 0; b = 255; break;
                case "red": r = 255; g = 0; b = 0; break;
                case "green": r = 0; g = 255; b = 0; break;
                case "#90EE90": r = 144; g = 238; b = 144; break; // light green
                case "slate blue": r = 106; g = 90; b = 205; break;
                default:
                    throw new ArgumentException("Unknown color name for capture overlay render: " + colorName);
            }
        }

        // 리전을 R/G/B 채널 이미지 각각에 overpaint_region(단일 스칼라)으로 in-place 페인트한다. r/g/b 는
        //  반드시 decompose3 직후 채널별로 CopyImage() 된 독립 버퍼여야 한다(호출부 RenderToHImage 가 보장,
        //  클래스 상단 주석 참고). "fill" 모드 사용 — 새로 만든 off-screen buffer 창(SetDraw 미호출)의
        //  기본 disp_obj 렌더링이 리전을 100% 채워 그린다는 것을 스크래치 하네스로 실측 확인(margin 아님).
        private static void PaintRegionColor(HImage r, HImage g, HImage b, HObject region, string colorName)
        {
            ResolveColorRgb(colorName, out int cr, out int cg, out int cb);
            HOperatorSet.OverpaintRegion(r, region, new HTuple(cr), "fill");
            HOperatorSet.OverpaintRegion(g, region, new HTuple(cg), "fill");
            HOperatorSet.OverpaintRegion(b, region, new HTuple(cb), "fill");
        }

        // z-order: 두꺼운 cyan 거리선이 마지막에 그려지면 OK(녹) 에지선을 덮으므로 레이어 순서를 명시한다.
        // ①배경(거리선 cyan/blue + raw 노랑점) → ②검출점 X 마커(magenta) → ③FAI-Edge 측정선(녹/적)을 맨 위에.
        // overpaint_region 도 disp_obj 와 동일하게 나중 호출이 겹치는 픽셀을 덮어쓰므로(painter's algorithm),
        // 호출 순서만 그대로 유지하면 기존 window 방식과 동일한 z-order 결과가 재현된다.
        private static void DrawOverlayRegions(HImage r, HImage g, HImage b, List<EdgeInspectionOverlay> overlays)
        {
            if (overlays == null) return;

            // ① 배경 레이어: 거리선(cyan)/기타(blue)/raw 점(노랑). FAI-Edge 마커/라인은 이후 레이어.
            foreach (var ov in overlays)
            {
                if (ov == null || string.IsNullOrEmpty(ov.RoiId)) continue;
                if (ov.RoiId.StartsWith("FAI-Edge", StringComparison.OrdinalIgnoreCase)) continue;

                if (string.Equals(ov.RoiId, "FAI-DistLine", StringComparison.OrdinalIgnoreCase))
                {
                    DrawLineAsRegion(r, g, b, ov.LineRow1, ov.LineColumn1, ov.LineRow2, ov.LineColumn2, "cyan", DistLineThicknessRadius);
                }
                else
                {
                    DrawLineAsRegion(r, g, b, ov.LineRow1, ov.LineColumn1, ov.LineRow2, ov.LineColumn2, "blue", LineThicknessRadius);
                }
            }

            // ② FAI-EdgeRaw 노랑 점 (배경 위)
            foreach (var ov in overlays)
            {
                if (ov == null || ov.RoiId == null) continue;
                if (string.Equals(ov.RoiId, "FAI-EdgeRaw", StringComparison.OrdinalIgnoreCase))
                {
                    DrawPointsAsRegion(r, g, b, ov.Points, "yellow");
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
                    DrawPointsAsRegion(r, g, b, ov.Points, "magenta"); // 검출점 X 마커
                }
                string lineColor;
                if (ov.RoiId.EndsWith("-NG", StringComparison.OrdinalIgnoreCase)) lineColor = "red";
                else lineColor = "green";
                DrawLineAsRegion(r, g, b, ov.LineRow1, ov.LineColumn1, ov.LineRow2, ov.LineColumn2, lineColor, LineThicknessRadius); // 측정선 최상위
            }
        }

        // 선분 1개를 리전으로 변환(gen_region_line→dilation)해 페인트. radius 로 두께 조절.
        private static void DrawLineAsRegion(HImage r, HImage g, HImage b, double r1, double c1, double r2, double c2, string color, double radius)
        {
            HObject region = null, dilated = null;
            try
            {
                HOperatorSet.GenRegionLine(out region, r1, c1, r2, c2);
                HOperatorSet.DilationCircle(region, out dilated, radius);
                PaintRegionColor(r, g, b, dilated, color);
            }
            catch { /* 단일 오버레이 렌더 실패는 무시 — 전체 캡쳐 보존 */ }
            finally
            {
                if (region != null) { try { region.Dispose(); } catch { } }
                if (dilated != null) { try { dilated.Dispose(); } catch { } }
            }
        }

        // 검출점들을 X 마커 리전으로 변환해 페인트.
        private static void DrawPointsAsRegion(HImage r, HImage g, HImage b, List<EdgeInspectionPoint> points, string color)
        {
            if (points == null || points.Count == 0) return;
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
                    PaintRegionColor(r, g, b, dilated, color);
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
        private static void DrawDatumRegions(HImage r, HImage g, HImage b, List<DatumCaptureOverlay> datums, double axisHalfLength)
        {
            if (datums == null) return;
            foreach (var d in datums)
            {
                if (d == null) continue;
                // datum 기준선(축): 원점 통과, 각도 방향, 이미지 대각 길이. UI Line1=yellow, Line2=cyan 일치.
                if (d.HasOrigin && d.HasAxis1)
                {
                    DrawDatumAxisLine(r, g, b, d.OriginRow, d.OriginCol, d.Axis1AngleRad, axisHalfLength, "yellow"); // 1차 기준선
                }
                if (d.HasOrigin && d.HasAxis2)
                {
                    DrawDatumAxisLine(r, g, b, d.OriginRow, d.OriginCol, d.Axis2AngleRad, axisHalfLength, "cyan"); // 2차(수직) 기준선
                }
                if (d.HasCircle && d.CircleRadius > 0)
                {
                    DrawCircleRingAsRegion(r, g, b, d.CircleRow, d.CircleCol, d.CircleRadius, "#90EE90"); // 검출 원(녹색, UI L905 일치)
                    DrawCrossAsRegion(r, g, b, d.CircleRow, d.CircleCol, DatumCircleCenterCrossHalf, "yellow"); // 원 중심 십자(UI L911 일치)
                }
                if (d.HasOrigin)
                {
                    DrawCrossAsRegion(r, g, b, d.OriginRow, d.OriginCol, DatumOriginCrossHalf, "slate blue"); // 검출 원점 십자
                }
            }
        }

        // 원점 통과 기준선을 방향벡터(sinφ,cosφ)로 ±half 산출 후 리전 표시 (VisionAlgorithmService.GetDatumAxisLine 동일식).
        private static void DrawDatumAxisLine(HImage r, HImage g, HImage b, double originRow, double originCol, double angleRad, double half, string color)
        {
            double dirR = System.Math.Sin(angleRad);
            double dirC = System.Math.Cos(angleRad);
            DrawLineAsRegion(r, g, b,
                originRow - half * dirR, originCol - half * dirC,
                originRow + half * dirR, originCol + half * dirC,
                color, LineThicknessRadius);
        }

        // 원 외곽선을 링 리전(outer−inner)으로 만들어 페인트.
        private static void DrawCircleRingAsRegion(HImage r, HImage g, HImage b, double row, double col, double radius, string color)
        {
            HObject outer = null, inner = null, ring = null;
            try
            {
                double innerR = radius - DatumRingThickness;
                if (innerR < 1.0) innerR = 1.0;
                HOperatorSet.GenCircle(out outer, row, col, radius);
                HOperatorSet.GenCircle(out inner, row, col, innerR);
                HOperatorSet.Difference(outer, inner, out ring);
                PaintRegionColor(r, g, b, ring, color);
            }
            catch { /* 단일 datum 원 실패 무시 */ }
            finally
            {
                if (outer != null) { try { outer.Dispose(); } catch { } }
                if (inner != null) { try { inner.Dispose(); } catch { } }
                if (ring != null) { try { ring.Dispose(); } catch { } }
            }
        }

        // 축 정렬 십자(＋)를 리전으로 만들어 페인트.
        private static void DrawCrossAsRegion(HImage r, HImage g, HImage b, double row, double col, double half, string color)
        {
            HObject l1 = null, l2 = null, u = null, dilated = null;
            try
            {
                HOperatorSet.GenRegionLine(out l1, row - half, col, row + half, col);
                HOperatorSet.GenRegionLine(out l2, row, col - half, row, col + half);
                HOperatorSet.Union2(l1, l2, out u);
                HOperatorSet.DilationCircle(u, out dilated, LineThicknessRadius);
                PaintRegionColor(r, g, b, dilated, color);
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
