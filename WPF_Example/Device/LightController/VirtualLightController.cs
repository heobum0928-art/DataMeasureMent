using PropertyTools;
using PropertyTools.DataAnnotations;
using ReringProject.Setting;
using ReringProject.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ReringProject.Device {

    public enum ELightControllerState {
        Idle,
        Reading,
        Writing,
    }

    public class ChannelInfo : Observable{
        private VirtualLightController pController;

        public VirtualLightController Controller { get => pController; }

        private string name;
        public string Name {
            get { return name; }
            set { SetValue(ref name, value);}
        }

        private bool on;
        public bool On {
            get { return on; }
            set { SetValue(ref on, value); }
        }

        private int level;
        public int Level {
            get { return level; }
            set { SetValue(ref this.level, value); } }
        
        public ChannelInfo(VirtualLightController controller, string name) {
            pController = controller;
            Name = name;
        }
    }

    public class VirtualLightController {
        public int Index { get; private set; }

        public int Port { get; set; } = 3;

        public int Baudrate { get; set; } = 9600;

        public int MaxChannel { get; private set; } = LightHandler.CHANNEL_LIMIT;

        public int MinLevel { get; protected set; } = 0;

        public int MaxLevel { get; protected set; } = 255;

        public ELightControllerState State { get; protected set; }
        
        public ChannelInfo[] Channels { get; private set; }

        public VirtualLightController(int index, int maxChannel=LightHandler.CHANNEL_LIMIT) {
            Index = index;
            MaxChannel = maxChannel;
            Channels = new ChannelInfo[MaxChannel];

            //set default name
            for (int i = 0; i < Channels.Length; i++) {
                Channels[i] = new ChannelInfo(this, "Channel " + i.ToString());
            }
        }

        public VirtualLightController SetChannelName(int index, string name) {
            if (index >= MaxChannel) return this;
            Channels[index].Name = name;
            return this;
        }

        public VirtualLightController SetChannelNames(params string [] names) {
            for(int i = 0; i < names.Length; i++) {
                if (i >= MaxChannel) break;
                Channels[i].Name = names[i];
            }
            return this;
        }
        
        public int ChannelCount { get => Channels.Length; }

        public ChannelInfo this[int index] {
            get {
                if (index >= MaxChannel) return null;
                return Channels[index];
            }
        }

        public ChannelInfo this[string name] {
            get {
                for(int i = 0; i < Channels.Length; i++) {
                    if (Channels[i].Name == name) return Channels[i];
                }
                return null;
            }
        }
        
        public bool IsOpen { get; protected set; }

        public virtual bool Open() {
            State = ELightControllerState.Idle;
            IsOpen = true;
            return true;
        }

        public virtual void Close() {
            IsOpen = false;
            State = ELightControllerState.Idle;
        }

        public virtual bool GetOnOff(int channel) {
            if (channel >= MaxChannel) return false;
            return Channels[channel].On;
        }

        public virtual bool SetOnOff(int channel, bool onOff) {
            if (channel >= MaxChannel) return false;
            return Channels[channel].On = onOff;
        }
        
        public virtual bool WriteOnOff(int channel, bool state) {
            if (channel >= MaxChannel) return false;
            State = ELightControllerState.Writing;
            Thread.Sleep(10);
            Channels[channel].On = state;
            State = ELightControllerState.Idle;
            Logging.PrintLog((int)ELogType.LightController, "{0} {1} : Write OnOff => {2}", Index.ToString(), channel.ToString(), state.ToString());
            return true;
        }

        public virtual bool ReadOnOff(int channel) {
            State = ELightControllerState.Reading;
            //Channels[channel].On = true;
            Thread.Sleep(10);
            State = ELightControllerState.Idle;
            Logging.PrintLog((int)ELogType.LightController, "{0} {1} : Read OnOff", Index.ToString(), channel.ToString());
            return true;
        }

        /// <summary>
        /// 연결된 통신으로 파라메타 channel을 level값으로 설정.
        /// </summary>
        /// <param name="channel"></param>
        /// <param name="level"></param>
        /// <returns></returns>
        public virtual bool WriteLevel(int channel, int level) {
            if (channel >= MaxChannel) return false;
            State = ELightControllerState.Writing;
            Thread.Sleep(10);
            Channels[channel].Level = level;
            State = ELightControllerState.Idle;
            Logging.PrintLog((int)ELogType.LightController, "{0} {1} : Write Level => {2}", Index.ToString(), channel.ToString(), level.ToString());
            return true;
        }

        /// <summary>
        /// 연결된 통신으로 현재 level값을 reading함.
        /// </summary>
        /// <param name="channel"></param>
        /// <returns></returns>
        public virtual bool ReadLevel(int channel) {
            if (channel >= MaxChannel) return false;
            State = ELightControllerState.Reading;
            //Channels[channel].Level = 0;
            Thread.Sleep(10);
            State = ELightControllerState.Idle;
            Logging.PrintLog((int)ELogType.LightController, "{0} {1} : Read Level", Index.ToString(), channel.ToString());
            return true;
        }

        /// <summary>
        /// 내부 버퍼에 저장된 (이전에 reading된) 값을 반환
        /// </summary>
        /// <param name="channel"></param>
        /// <returns></returns>
        public virtual int GetLevel(int channel) {
            if (channel >= MaxChannel) return 0;
            return Channels[channel].Level; 
        }

        public virtual bool SetLevel(int channel, int level) {
            if (channel >= MaxChannel) return false;
            Channels[channel].Level = level;
            return true;
        }
    }
}
