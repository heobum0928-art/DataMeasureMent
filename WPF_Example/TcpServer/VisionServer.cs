using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReringProject.Network {
    public partial class VisionServer : TcpServer {
        public const char MSG_STX = '$';
        public const char MSG_ETX = '@';
        public const char MSG_CONTENTS_SEPERATOR = ',';
        public const char MSG_CMD_SEPERATOR = ':';

        //Message Identifier
        public ResourceMap ResourceIdentifier { get; private set; } = new ResourceMap();


        public VisionServer() : base() {
            Header = (byte)MSG_STX;
            Trailer = (byte)MSG_ETX;
        }

        protected override void PerformOnAlarm(AlarmEventArgs e) {
            base.PerformOnAlarm(e);
        }

        protected override void PerformOnRecvMessage(MessageEventArgs e) {
            base.PerformOnRecvMessage(e);
        }

        protected override void PerformOnSendMessage(MessageEventArgs e) {
            base.PerformOnSendMessage(e);
        }

        

        public bool GetRecvPacket(int index, out VisionRequestPacket packet) {
            packet = null;
            try {
                if (GetRecvMessage(index, out string msg)) {
                    string sender = GetClientIpAddress(index);
                    packet = VisionRequestPacket.Convert(msg);
                    if (packet != null) {
                        packet.Sender = sender;
                        ResourceIdentifier.SetIdentifier(ref packet);
                        return true;
                    }
                }
            }
            catch (ArgumentOutOfRangeException argumentException) {
                PerformOnAlarm(new AlarmEventArgs(AlarmEventArgs.AlarmEventType.OnRecvMessageParsingFail, GetClientIpAddress(index), argumentException.Message));
            }
            catch (IndexOutOfRangeException indexException) {
                PerformOnAlarm(new AlarmEventArgs(AlarmEventArgs.AlarmEventType.OnRecvMessageParsingFail, GetClientIpAddress(index), indexException.Message));
            }
            return false;
        }

        public bool GetRecvPacket(string ipAddress, out VisionRequestPacket packet) {
            packet = null;
            try {
                if (GetRecvMessage(ipAddress, out string msg)) {
                    packet = VisionRequestPacket.Convert(msg);
                    if (packet != null) {
                        packet.Sender = ipAddress;
                        ResourceIdentifier.SetIdentifier(ref packet);
                        return true;
                    }
                }
            }
            catch (ArgumentOutOfRangeException argumentException) {
                PerformOnAlarm(new AlarmEventArgs(AlarmEventArgs.AlarmEventType.OnRecvMessageParsingFail, ipAddress, argumentException.Message));
            }
            catch (IndexOutOfRangeException indexException) {
                PerformOnAlarm(new AlarmEventArgs(AlarmEventArgs.AlarmEventType.OnRecvMessageParsingFail, ipAddress, indexException.Message));
            }
            return false;
        }

        public bool SendPacket(int index, VisionResponsePacket packet) {
            return SendMessage(index, packet.ToString());
        }

        public bool SendPacket(string ipAddress, VisionResponsePacket packet) {
            return SendMessage(ipAddress, packet.ToString());
        }


    }
}
