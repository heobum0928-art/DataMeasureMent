using System;
using System.Collections.Generic;
using System.ComponentModel;
using ReringProject.Device;

namespace ReringProject.UI {

    public class LightChannelViewModel : INotifyPropertyChanged{
        private LightHandler pLightHandle;
        private ChannelInfo pChannel;
        private int ChannelNum;

        public LightChannelViewModel(ChannelInfo channel, int channelNum) {
            pLightHandle = LightHandler.Handle;
            pChannel = channel;
            ChannelNum = channelNum;
        }

        public string Name {
            get { return pChannel.Name; }
            set { pChannel.Name = value; }
        }

        public bool On {
            get {
                return pLightHandle.GetOnOff(pChannel.Controller.Index, ChannelNum);
                //return pChannel.On;
            }
            set {
                pLightHandle.SetOnOff(pChannel.Controller.Index, ChannelNum, value);
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("On"));
                //pChannel.On = value;
            }
        }

        public int Level {
            get {
                return pLightHandle.GetLevel(pChannel.Controller.Index, ChannelNum);
                //return pChannel.Level;
            }
            set {
                pLightHandle.SetLevel(pChannel.Controller.Index, ChannelNum, value);
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Level"));
                //pChannel.Level = value;
            }
        }

        public int MaxLevel {
            get { return pChannel.Controller.MaxLevel; }
        }

        public int MinLevel {
            get { return pChannel.Controller.MinLevel; }
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }

    public class LightControllerViewModel {
        private LightHandler pLightHandle;
        private VirtualLightController pController;
        
        public VirtualLightController Controller{
            get{
                return pController;
            }
        }
        
        public int Port {
            get {
                return pController.Port;
            }

            set {
                pController.Port = value;
            }
        }

        public int Baudrate {
            get {
                return pController.Baudrate;
            }

            set {
                pController.Baudrate = value;
            }
        }

        public string Name {
            get {
                return string.Format("Controller {0}", pController.Index+1);
            }
        }

        public string State {
            get {
                return pController.State.ToString();
            }
        }

        public ChannelInfo [] Channels {
            get {
                return pController.Channels;
            }
        }

        public LightControllerViewModel(VirtualLightController controller) {
            pLightHandle = LightHandler.Handle;
            pController = controller;
        }
    }
}
