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
                if (pCamera == null) return;
                dc.PushTransform(this.RenderTransform);
                pCamera.RenderCenterLine(dc);
            }
        }
    }
}
