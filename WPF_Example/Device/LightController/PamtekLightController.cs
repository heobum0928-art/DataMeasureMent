using ReringProject.Setting;
using ReringProject.Utility;
using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReringProject.Device {
    public class PamtekLightController : VirtualLightController{

        private SerialPort mPort = new SerialPort();

        public PamtekLightController(int index, int maxChannel= LightHandler.CHANNEL_LIMIT) : base(index, maxChannel) {

        }

        public override bool Open() {
            try {
                mPort.PortName = "COM" + Port.ToString();
                mPort.BaudRate = Baudrate;
                mPort.StopBits = StopBits.One;
                mPort.DataBits = 8;
                mPort.Parity = Parity.None;

                mPort.Open();
                Logging.PrintLog((int)ELogType.LightController, "Light Controller {0} Open Success", Index);
            }
            catch(Exception e) {
                Logging.PrintLog((int)ELogType.LightController, "Light Controller {0} Open Fail : {1}", Index, e.Message);
                return false;
            }
            return base.Open();
        }

        public override void Close() {
            if (IsOpen) mPort.Close();

            base.Close();
        }

        public override bool ReadOnOff(int channel) {
            return base.ReadOnOff(channel);
        }

        public bool WriteAmperaLimit(int ampere) {
            if (IsOpen == false) return false;
            try {
                mPort.DiscardOutBuffer();
                mPort.WriteLine(string.Format("#I{0:0000}&", ampere));
            }
            catch(Exception e) {
                Logging.PrintLog((int)ELogType.LightController, "Light Controller {0} Write Ampere to {1} Fail : {2}", Index, ampere, e.Message);
                return false;
            }
            return true;
        }

        public override bool WriteOnOff(int channel, bool state) {
            if (IsOpen == false) return false;

            try {
                SetOnOff(channel, state);

                int stateNum = Convert.ToInt32(state);
                mPort.DiscardInBuffer();
                if (state) {
                    mPort.WriteLine(string.Format("#A{0}{1:000}&", channel+1, GetLevel(channel)));
                }
                else
                {
                    mPort.WriteLine(string.Format("#A{0}{1:000}&", channel+1, stateNum));
                }

            }
            catch(Exception e) {
                Logging.PrintLog((int)ELogType.LightController, "Light Controller {0} Write OnOff to {1} Fail : {2}", Index, state.ToString(), e.Message);
                return false;
            }

            return true;
            //return base.WriteOnOff(channel, state);
        }

        public override bool ReadLevel(int channel) {
            return base.ReadLevel(channel);
        }

        public override bool WriteLevel(int channel, int level) {
            SetLevel(channel, level);
            return WriteOnOff(channel, GetOnOff(channel));
        }
    }
}
