using ReringProject.Setting;
using ReringProject.Utility;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ReringProject.Device
{
    public class JPFLightController : VirtualLightController
    {
        private SerialPort mPort = new SerialPort();

        public JPFLightController(int index, int maxChannel = LightHandler.CHANNEL_LIMIT) : base(index, maxChannel)
        {
        }

        public override bool Open()
        {
            try
            {
                mPort.PortName = "COM" + Port.ToString();
                mPort.BaudRate = Baudrate;
                mPort.StopBits = StopBits.One;
                mPort.DataBits = 8;
                mPort.Parity = Parity.None;

                mPort.Open();
                Logging.PrintLog((int)ELogType.LightController, "Light Controller {0} Open Success", Index);

                //mPort.WriteLine(string.Format("#O11,1,1,1&"));    // 모든 채널(1,2,3,4)을 동시에 ON
                mPort.WriteLine(string.Format("#Oa1&"));          // 모든 채널(1,2,3,4)을 동시에 ON
                Thread.Sleep(50);
                mPort.WriteLine(string.Format("#Aa150&"));        // 모든 채널의 레벨 값을 150 적용.
                Thread.Sleep(50);
                mPort.WriteLine(string.Format("#Aa000&"));        // 모든 채널의 레벨 값을 0 적용.
                Thread.Sleep(50);
            }
            catch (Exception e)
            {
                Logging.PrintLog((int)ELogType.LightController, "Light Controller {0} Open Fail : {1}", Index, e.Message);
                return false;
            }
            return base.Open();
        }

        public override void Close()
        {

            if (IsOpen)
            {
                mPort.WriteLine(string.Format("#Oa0&"));        // 모든 채널을 동시에 OFF
                mPort.Close();
            }

            base.Close();
        }

        public override bool ReadOnOff(int channel)
        {
            return base.ReadOnOff(channel);
        }

        public bool WriteAmperaLimit(int ampere)
        {
            if (IsOpen == false) return false;
            try
            {
                mPort.DiscardOutBuffer();
                mPort.WriteLine(string.Format("#I{0:0000}&", ampere));
            }
            catch (Exception e)
            {
                Logging.PrintLog((int)ELogType.LightController, "Light Controller {0} Write Ampere to {1} Fail : {2}", Index, ampere, e.Message);
                return false;
            }
            return true;
        }

        public override bool WriteOnOff(int channel, bool state)
        {
            if (IsOpen == false) return false;

            try
            {
                SetOnOff(channel, state);

                //int stateNum = Convert.ToInt32(state);
                //mPort.DiscardInBuffer();
                //if (state)
                //{
                //    mPort.WriteLine(string.Format("#A{0}{1:000}&", channel + 1, GetLevel(channel)));
                //    // 11.11 Test Code Start
                //    string LightStr = string.Format("#A{0}{1:000}&", channel + 1, GetLevel(channel));
                //    Debug.WriteLine($"Light Debug:{LightStr}");
                //    // 11.11 Test Code End
                //}
                //else
                //{
                //    mPort.WriteLine(string.Format("#A{0}{1:000}&", channel + 1, 0));
                //}
            }
            catch (Exception e)
            {
                Logging.PrintLog((int)ELogType.LightController, "Light Controller {0} Write OnOff to {1} Fail : {2}", Index, state.ToString(), e.Message);
                return false;
            }

            return true;
        }

        public override bool ReadLevel(int channel)
        {
            return base.ReadLevel(channel);
        }

        public override bool WriteLevel(int channel, int level)
        {
            SetLevel(channel, level);
            if (IsOpen == false) return false;

            try
            {
                bool bstate = GetOnOff(channel);// SetOnOff(channel, state);

                //int stateNum = Convert.ToInt32(state);
                mPort.DiscardInBuffer();
                if (bstate)
                {
                    mPort.WriteLine(string.Format("#A{0}{1:000}&", channel + 1, GetLevel(channel)));
                    // 11.11 Test Code Start
                    string LightStr = string.Format("#A{0}{1:000}&", channel + 1, GetLevel(channel));
                    Debug.WriteLine($"Light Debug:{LightStr}");
                    // 11.11 Test Code End
                }
                else
                {
                    mPort.WriteLine(string.Format("#A{0}{1:000}&", channel + 1, 0));
                }
            }
            catch (Exception e)
            {
                Logging.PrintLog((int)ELogType.LightController, "Light Controller {0} Write OnOff to {1} Fail : {2}", Index, GetOnOff(channel).ToString(), e.Message);
                return false;
            }
            //return WriteOnOff(channel, GetOnOff(channel));
            return true;
        }
    }
}
