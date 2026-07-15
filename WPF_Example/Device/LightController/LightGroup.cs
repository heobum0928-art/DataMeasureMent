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

        // AddChannel(names) 은 등록 시점의 채널 이름 배치를 스캔해 (Index,Channel) 을 그 자리에서 확정 저장한다.
        //  LightHandler.Load() 가 light.ini 의 ChannelNames 로 물리 채널명을 재배선해도(예: ALIGN_COAX→BACK),
        //  RegisterLightController() 는 Load() 보다 먼저 실행되므로 이 그룹의 아이템들은 재배선 이전 위치에 고정된 채
        //  남는다 — TryFindChannel(개별 채널 API 가 매 호출마다 이름을 새로 찾는 것)과 달리 그룹 API(SetOnOff(string,..))
        //  는 이 고정 인덱스를 그대로 쓰므로 재배선이 반영되지 않는 결함이 있었다. Load() 가 재배선 직후 각 그룹에 대해
        //  본 메서드를 호출해 아이템 이름 기준으로 현재 채널 위치를 다시 찾는다.
        //  이름을 더 이상 못 찾으면(예: ALIGN_COAX 를 BACK 으로 통째로 재명명해 "ALIGN_COAX" 라는 이름 자체가 사라진
        //  경우) 예전 위치를 그대로 두지 않고 아이템을 제거한다 — 예전 위치를 유지하면 그 자리가 다른 논리 채널
        //  (BACK)로 재배선된 상태이므로, "ALIGN_COAX" 그룹에 대한 SetOnOff/SetLevel 호출이 실제로는 BACK 과 동일한
        //  물리 채널에 명령을 보내 서로 덮어써 버리는 버그(예: Back On 직후 Coax 기본값 Off 가 같은 채널을 꺼버림)가
        //  있었다. 아이템 제거 후에는 group.Count==0 이 되어 SetOnOff/SetLevel 이 안전하게 아무 것도 하지 않는다.
        public void RebindChannels() {
            List<LightGroupItem> rebound = new List<LightGroupItem>();
            for (int i = 0; i < Items.Count; i++) {
                LightGroupItem old = Items[i];
                int index, channel;
                if (pHandler.TryFindChannel(old.Name, out index, out channel)) {
                    rebound.Add(new LightGroupItem(old.Name, index, channel));
                }
                // else: 이름을 더 이상 찾을 수 없음 — 이 아이템은 버리고(제거) 다른 채널의 물리 위치를 침범하지 않는다.
            }
            Items = rebound;
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
