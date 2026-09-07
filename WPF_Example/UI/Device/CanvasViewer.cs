using ReringProject.Device;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ReringProject.UI {
    public class CanvasViewer : Canvas{
        private const double DEFAULT_VIEW_SCALE = 1.0;

        private VirtualCamera pCamera;

        private object mInterlock = new object();

        public CanvasViewer() : base() {

        }

        public void SetDevice(VirtualCamera camera) {
            lock (mInterlock) {
                pCamera = camera;
            }
        }

        protected override void OnRender(DrawingContext dc) {
            base.OnRender(dc);

            lock (mInterlock) {
                if (pCamera == null) {
                    return;
                }

                // 이 미리보기는 배율이 두 번 곱해지는 구조다: Background(ImageBrush) 는 캔버스 레이아웃 크기
                // (카메라 해상도 × DrawScale) 에 맞춰 그려지고, 그 위에 RenderTransform(DrawScale) 이 한 번 더 걸린다.
                // 그래서 이미지 픽셀 p 는 화면상 p × DrawScale² 에 놓인다(실기 확인: 0.32 배율에서 13376px 이미지가
                // 약 1370px 로 표시). 십자를 배경과 같은 자리에 두려면 여기서도 DrawScale 을 한 번 더 push 해야 한다.
                // 선 두께는 push 후 좌표에서 그리므로 화면상 DrawScale² 배가 된다 → 그만큼 나눠 화면 두께를 고정.
                double dViewScale = DEFAULT_VIEW_SCALE;
                ScaleTransform scale = this.RenderTransform as ScaleTransform;
                if (scale != null) {
                    dViewScale = scale.ScaleX;
                }

                dc.PushTransform(new ScaleTransform(dViewScale, dViewScale));
                pCamera.RenderCenterLine(dc, dViewScale * dViewScale);
                dc.Pop();
            }
        }
    }

    // 카메라 창 미리보기 위에 겹쳐서, 스크롤/확대와 무관하게 "지금 보이는 영역"의 정중앙에 십자를 그린다.
    // ScrollViewer 바깥(같은 Grid 셀)에 놓이므로 캔버스 배율/스크롤의 영향을 받지 않는다.
    // 이미지 중심 십자(CanvasViewer, 자홍색)와 구분되도록 연두색을 쓴다.
    public class ViewCenterOverlay : FrameworkElement {
        private const double LINE_THICKNESS = 2.0;
        private static readonly Pen VIEW_CENTER_PEN = CreatePen();

        private static Pen CreatePen() {
            Pen pen = new Pen(Brushes.Lime, LINE_THICKNESS);
            pen.Freeze();
            return pen;
        }

        protected override void OnRender(DrawingContext dc) {
            base.OnRender(dc);
            bool bNoArea = ActualWidth <= 0 || ActualHeight <= 0;
            if (bNoArea) {
                return;
            }
            double dCx = ActualWidth / 2;
            double dCy = ActualHeight / 2;
            dc.DrawLine(VIEW_CENTER_PEN, new Point(0, dCy), new Point(ActualWidth, dCy));
            dc.DrawLine(VIEW_CENTER_PEN, new Point(dCx, 0), new Point(dCx, ActualHeight));
        }

        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo) {
            base.OnRenderSizeChanged(sizeInfo);
            InvalidateVisual();
        }
    }
}
