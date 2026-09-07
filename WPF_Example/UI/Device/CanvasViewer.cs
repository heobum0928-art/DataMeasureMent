using ReringProject.Device;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
}
