using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReringProject.Device {

    public class LightGroupItem {
        public string Name { get; private set; }
        public int Index { get; private set; }
        public int Channel { get; private set; }

        public LightGroupItem(string name, int index, int channel) {
            Name = name;
            Index = index;
            Channel = channel;
        }
    }

    public class LightGroup {
        private LightHandler pHandler;
        public string Name { get; set; }

        private List<LightGroupItem> Items = new List<LightGroupItem>();

        public LightGroup(string name) {
            pHandler = LightHandler.Handle;
            Name = name;
        }

        public int MinLevel {
            get {
                return pHandler.GetLevelMin(Items[0].Index);
            }
        }

        public LightGroup AddChannel(VirtualLightController controller) {
            for(int i = 0; i < controller.ChannelCount; i++) {
                LightGroupItem item = new LightGroupItem(controller[i].Name, controller.Index, i);
                Items.Add(item);
            }
            return this;
        }

        public LightGroup AddChannel(params string [] names) {
            for (int i = 0; i < pHandler.ControllerCount; i++) {
                VirtualLightController con = pHandler.Controllers[i];
                for (int j = 0; j < con.ChannelCount; j++) {
                    if (names.Contains(con[j].Name)) {
                        LightGroupItem item = new LightGroupItem(con[j].Name, con.Index, j);
                        Items.Add(item);
                    }
                }
            }
            return this;
        }

        public int Count { get => Items.Count; }

        public LightGroupItem this[int index] {
            get {
                if (index >= Items.Count) return null;
                return Items[index];
            }
        }

        public LightGroupItem this[string name] {
            get {
                foreach(LightGroupItem info in Items) {
                    if (info.Name == name) return info;
                }
                return null;
            }
        }

    }
}
