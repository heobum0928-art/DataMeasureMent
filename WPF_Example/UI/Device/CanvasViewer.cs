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

                // this.RenderTransform(scaleTransform) 은 WPF 가 이 Visual 전체(자기 OnRender 결과 포함)에
                // 이미 한 번 적용하므로, 여기서 변환을 또 push 하면 배율이 두 번 곱해져 십자가 어긋난다.
                double dViewScale = DEFAULT_VIEW_SCALE;
                ScaleTransform scale = this.RenderTransform as ScaleTransform;
                if (scale != null) {
                    dViewScale = scale.ScaleX;
                }

                pCamera.RenderCenterLine(dc, dViewScale);
            }
        }
    }
}
