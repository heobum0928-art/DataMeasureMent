using ReringProject.Setting;
using ReringProject.Utility;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ReringProject.Network {
    public class MessageEventArgs : EventArgs {
        public string Target { get; }
        public string Message { get; }

        public MessageEventArgs(string target, string message) {
            Target = target;
            Message = message;
        }

        public override string ToString() {
            return string.Format("{0} - {1} - {2}", DateTime.Now.ToLongTimeString(), Target, Message);
        }
    }

    public class AlarmEventArgs : EventArgs {
        public enum AlarmEventType {
            OnConnected,
            OnDisconnected,
            OnAcceptFail,

            OnRecvTimeOut,
            OnSendFail,

            OnRecvMessageParsingFail,
            OnSendMessageParsingFail,
        }

        public AlarmEventType AlarmType { get; }

        public string Target { get; }
        public string Message { get; }

        public AlarmEventArgs(AlarmEventType type, string target, string message) {
            AlarmType = type;
            Target = target;
            Message = message;
        }
    }
    
    //event
        public delegate void MessageEventHandler(object sender, MessageEventArgs e);
        public delegate void AlarmEventHandler(object sender, AlarmEventArgs e);

    
    public class TcpServer : IDisposable {
        //def
        public const int TIMEOUT_CONNECT = 2000;
        public const int TIMEOUT_RECV = 2000;

        public const int SIZE_RECV_BUFFER = 1024;
        public const int SIZE_SEND_BUFFER = 1024;

        public const int MAX_CONNECTION_COUNT = 1;  //client 최대 연결 갯수

        //options
        public enum MessageEncodingType {
            Default,
            Ascii,
            Utf8,
        }
        private static MessageEncodingType EncodingType { get; set; } = MessageEncodingType.Default;

        //client data
        public class ConnectedClient : IDisposable {
            private TcpServer Parent { get; }

            private TcpClient mClient;
            private NetworkStream mStream;

            private byte[] mSendBuffer = new byte[SIZE_SEND_BUFFER];
            private byte[] mRecvBuffer = new byte[SIZE_RECV_BUFFER];

            private int RecvCount = 0;
            
            private ConcurrentQueue<string> SendQueue = new ConcurrentQueue<string>();
            private ConcurrentQueue<string> RecvQueue = new ConcurrentQueue<string>();

            private Thread mCommunicationThread;
            private bool IsTerminated = false;

            public override string ToString() {
                return GetIpAddress();
            }

            public ConnectedClient(TcpServer parent, TcpClient pClient) {
                Parent = parent;

                mClient = pClient;
                mClient.ReceiveTimeout = TIMEOUT_RECV;
                
                mStream = mClient.GetStream();

                mCommunicationThread = new Thread(Execute);
                mCommunicationThread.Name = GetIpAddress();
                mCommunicationThread.Start();

            }

            public string GetIpAddress() {
                if (mClient == null) return null;
                return mClient.Client.RemoteEndPoint.ToString();
            }

            public bool IsConnected() { 
                if (mClient == null) return false;

                ipProperties = IPGlobalProperties.GetIPGlobalProperties();
                try {
                    tcpConnections = ipProperties.GetActiveTcpConnections().Where(x => x.LocalEndPoint.Equals(mClient.Client.LocalEndPoint) && x.RemoteEndPoint.Equals(mClient.Client.RemoteEndPoint)).ToArray();
                }
                catch (Exception ex) {
                    // Exception 처리 -> 여기서는 Disconnected된 것으로 보면 된다.
                    Disconnect();
                    Logging.PrintLog((int)ELogType.TcpConnection, $"{GetIpAddress()} : Client Disconnected. ({ex.Message})");
                    return false;
                }

                if (tcpConnections != null && tcpConnections.Length > 0) {
                    TcpState stateOfConnection = tcpConnections.First().State;
                    if (stateOfConnection == TcpState.Established) { //Connected
                        return true;
                    }
                }
                //연결 해제되었으므로 disconnect 처리
                Disconnect();
                return false;
            }
            

            public void Dispose() {
                Disconnect();
            }

            private byte[] ConvertMessage(string msg) {
                try {
                    switch (EncodingType) {
                        case MessageEncodingType.Ascii:
                            return Encoding.ASCII.GetBytes(msg);
                        case MessageEncodingType.Default:
                            return Encoding.Default.GetBytes(msg);
                        case MessageEncodingType.Utf8:
                            return Encoding.UTF8.GetBytes(msg);
                    }
                }
                catch(Exception e) {
                    Logging.PrintLog((int)ELogType.TcpConnection, $"{GetIpAddress()} : Send Message Convert Fail. ({e.Message})");

                    Parent.OnAlarm?.Invoke(this, new AlarmEventArgs(AlarmEventArgs.AlarmEventType.OnSendMessageParsingFail, GetIpAddress(), e.Message));
                }
                return null;
            }

            private string ConvertMessage(byte [] msg) {
                try {
                    switch (EncodingType) {
                        case MessageEncodingType.Ascii:
                            return Encoding.ASCII.GetString(msg);
                        case MessageEncodingType.Default:
                            return Encoding.Default.GetString(msg);
                        case MessageEncodingType.Utf8:
                            return Encoding.UTF8.GetString(msg);
                    }
                }
                catch (Exception e) {
                    Logging.PrintLog((int)ELogType.TcpConnection, $"{GetIpAddress()} : Recv Message Convert Fail. ({e.Message})");

                    Parent.OnAlarm?.Invoke(this, new AlarmEventArgs(AlarmEventArgs.AlarmEventType.OnSendMessageParsingFail, GetIpAddress(), e.Message));
                }
                return null;
            }

            protected void Execute() {
                while (!IsTerminated) {
                    if(IsConnected() == false) {
                        Thread.Sleep(10);
                        continue;
                    }
                    //Read Message
                    if(Recv(out string recvMsg, out int recvCount)) {
                        RecvQueue.Enqueue(recvMsg);
                    }
                    
                    //Write Message
                    if(SendQueue.TryDequeue(out string msg)) {
                        Send(msg);
                    }

                    Thread.Sleep(1);
                }

                //Disconnect
                Parent.OnAlarm?.Invoke(this, new AlarmEventArgs(AlarmEventArgs.AlarmEventType.OnDisconnected, GetIpAddress(), "Disconnected."));
                mStream.Flush();
                mStream.Close();
                mClient.Close();

                mStream = null;
                mClient = null;
            }

            private bool Send(string msg) {
                if (IsConnected() == false) return false;
                
                byte[] bytes = ConvertMessage(msg);
                if (bytes == null) {
                    return false;
                }

                try {
                    Array.Copy(bytes, mSendBuffer, bytes.Length);
                    mStream.Write(mSendBuffer, 0, bytes.Length);
                    Logging.PrintLog((int)ELogType.TcpConnection, $"{GetIpAddress()} : Send Message : {msg}");
                    Parent.OnSendMessage?.Invoke(this, new MessageEventArgs(GetIpAddress(), msg));
                }
                catch (Exception e) {
                    Logging.PrintLog((int)ELogType.TcpConnection, $"{GetIpAddress()} : Send Fail. ({e.Message})");
                    Parent.OnAlarm?.Invoke(this, new AlarmEventArgs(AlarmEventArgs.AlarmEventType.OnSendFail, GetIpAddress(), e.Message));
                }
                
                return true;
            }
            
            
            private bool Recv(out string recvMsg, out int recvCount) {
                recvMsg = null;
                recvCount = 0;

                if (IsConnected() == false) return false;
                if (mStream.DataAvailable == false) return false;
                
                try {
                    Array.Clear(mRecvBuffer, 0, mRecvBuffer.Length);
                    while (mStream.DataAvailable) {
                        byte recvByte = (byte)mStream.ReadByte();

                        if (recvByte == Header) {
                            RecvCount = 0;
                        }
                        mRecvBuffer[RecvCount++] = recvByte;

                        if (recvByte == Trailer) {
                            //convert
                            string msg = ConvertMessage(mRecvBuffer).Trim('\0');
                            if (msg == null) {
                                return false;
                            }
                            recvCount = RecvCount;
                            recvMsg = msg;

                            RecvCount = 0;

                            //occurs event
                            Logging.PrintLog((int)ELogType.TcpConnection, $"{GetIpAddress()} : Recv Message : {msg}");
                            Parent.OnRecvMessage?.Invoke(this, new MessageEventArgs(GetIpAddress(), msg));
                            return true;
                        }
                    }
                }
                catch (Exception e) {
                    Logging.PrintLog((int)ELogType.TcpConnection, $"{GetIpAddress()} : Recv Fail. ({e.Message})");
                    Parent.OnAlarm?.Invoke(this, new AlarmEventArgs(AlarmEventArgs.AlarmEventType.OnSendFail, GetIpAddress(), e.Message));
                }
                
                return false;
            }

            public void Disconnect() {
                if (mClient == null) return;
                
                IsTerminated = true;
                mCommunicationThread.Join(1000);
            }

            public void SendMessage(string msg) {
                if (msg[0] != (char)Header) {
                    msg = (char)Header + msg;
                }
                if (msg[msg.Length - 1] != (char)Trailer) {
                    msg = msg + (char)Trailer;
                }
                SendQueue.Enqueue(msg);
            }

            public bool GetRecvMessage(out string msg) {
                if(!RecvQueue.TryDequeue(out msg)) {
                    return false;
                }
                return true;
            }

            public bool IsReadable {
                get {
                    if (mStream == null) return false;
                    return mStream.DataAvailable;
                }
            }
        }

        //handler
        private TcpListener mListener;
        
        private List<ConnectedClient> mConnectedClientList = new List<ConnectedClient>(MAX_CONNECTION_COUNT);
        private object mListInterlock = new object();
        protected int PortNum = 9000;
         
        private bool IsTerminated = false;

        private Thread mConnectionThread;

        private static IPGlobalProperties ipProperties;
        private static TcpConnectionInformation [] tcpConnections;

        //timer
        private Stopwatch mReceiveTimer = new Stopwatch();
        
        public event MessageEventHandler OnRecvMessage;
        public event MessageEventHandler OnSendMessage;
        public event AlarmEventHandler OnAlarm;

        //protocol
        public static byte Header { get; set; } = (byte)'$';
        public static byte Trailer { get; set; }  = (byte)'@';

        //method
        public TcpServer() {
            PortNum = SystemSetting.Handle.ServerPort;

            mListener = new TcpListener(IPAddress.Any, PortNum);
            mListener.Start();
            
            OnAlarm += OnAlarmProcess;

            mConnectionThread = new Thread(ConnectionExecute);
            mConnectionThread.IsBackground = true;
            mConnectionThread.Name = "TcpServer";
            mConnectionThread.Start();
        }

        protected virtual void PerformOnRecvMessage(MessageEventArgs e) {
            OnRecvMessage?.Invoke(this, e);
        }

        protected virtual void PerformOnSendMessage(MessageEventArgs e) {
            OnSendMessage?.Invoke(this, e);
        }

        protected virtual void PerformOnAlarm(AlarmEventArgs e) {
            OnAlarm?.Invoke(this, e);
        }
        
        private void OnAlarmProcess(object sender, AlarmEventArgs args) {
            switch (args.AlarmType) {
                case AlarmEventArgs.AlarmEventType.OnConnected:
                    break;
                case AlarmEventArgs.AlarmEventType.OnDisconnected: 
                    //리스트 삭제 처리
                    ConnectedClient client = GetClient(args.Target);
                    if(client != null) {
                        lock (mListInterlock) {
                            mConnectedClientList.Remove(client);
                            client.Dispose();
                        }
                    }
                    break;
            }
        }

        public void Dispose() {
            mListener.Stop();
            IsTerminated = true;
            
            mConnectionThread.Join(1000);
        }

        private void DisconnectAll() {
            lock (mListInterlock) {
                for (int i = 0; i < mConnectedClientList.Count; i++) {
                    if (mConnectedClientList[i].IsConnected()) {
                        mConnectedClientList[i].Disconnect();
                        mConnectedClientList[i].Dispose();
                    }
                }
                mConnectedClientList.Clear();
            }
        }

        public static string GetLocalIpAddress() {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList) {
                if (ip.AddressFamily == AddressFamily.InterNetwork) {
                    return ip.ToString();
                }
            }
            return "localhost";
        }
        
        //client 가 하나이상 연결되어 있으면 true 
        public bool IsConnected() {
            lock (mListInterlock) {
                for (int i = 0; i < mConnectedClientList.Count; i++) {
                    if (mConnectedClientList[i].IsConnected()) return true;
                }
            }
            return false;
        }

        public int GetConnectedClientCount() {
            return mConnectedClientList.Count;
        }

        public string GetClientIpAddress(int i) {
            if (i >= mConnectedClientList.Count) return null;
            lock (mListInterlock) {
                return mConnectedClientList[i].GetIpAddress();
            }
        }
        
        //연결을 처리하는 쓰레드
        private void ConnectionExecute() {
            while (!IsTerminated) {
                try {
                    TcpClient clientHandle = mListener.AcceptTcpClient();
                    ConnectedClient newClient = new ConnectedClient(this, clientHandle);
                    lock (mListInterlock) {
                        mConnectedClientList.Add(newClient);
                    }
                    Logging.PrintLog((int)ELogType.TcpConnection, $"{GetLocalIpAddress()} - New Client Accepted. : {newClient.GetIpAddress()}");
                    OnAlarm?.Invoke(this, new AlarmEventArgs(AlarmEventArgs.AlarmEventType.OnConnected, newClient.GetIpAddress(), "New Client Accepted."));
                    
                }
                catch(SocketException se) {
                    Logging.PrintLog((int)ELogType.TcpConnection, $"{GetLocalIpAddress()} - Socket Has Closed. : {se.Message}");
                    OnAlarm?.Invoke(this, new AlarmEventArgs(AlarmEventArgs.AlarmEventType.OnAcceptFail, GetLocalIpAddress(), $"Socket Has Closed. : {se.Message}"));
                }
                catch(Exception e) {
                    Logging.PrintLog((int)ELogType.TcpConnection, $"{GetLocalIpAddress()} - Client Accept Fail. : {e.Message}");
                    OnAlarm?.Invoke(this, new AlarmEventArgs(AlarmEventArgs.AlarmEventType.OnAcceptFail, GetLocalIpAddress(), $"Client Accept Fail. : {e.Message}"));
                }
                Thread.Sleep(500);
            }
            DisconnectAll();
        }
        public ConnectedClient GetClient(string ipAddress) {
            lock (mListInterlock) {
                for (int i = 0; i < mConnectedClientList.Count; i++) {
                    if (mConnectedClientList[i].GetIpAddress().Contains(ipAddress)) return mConnectedClientList[i];
                }
            }
            return null;
        }

        public ConnectedClient GetClient(int index) {
            lock (mListInterlock) {
                return mConnectedClientList[index];
            }
        }

        public bool SendMessage(string targetIp, string msg) {
            ConnectedClient client = GetClient(targetIp);
            if (client == null) return false;
            if (!client.IsConnected()) return false;
            
            client.SendMessage(msg);
            return true;
        }

        public bool SendMessage(int targetIndex, string msg) {
            lock (mListInterlock) {
                if (targetIndex >= mConnectedClientList.Count) return false;
                if (!mConnectedClientList[targetIndex].IsConnected()) return false;
                
                mConnectedClientList[targetIndex].SendMessage(msg);
            }
            return true;
        }

        public bool IsReadable(int targetIndex) {
            return mConnectedClientList[targetIndex].IsReadable;
        }

        public bool GetRecvMessage(int targetIndex, out string msg) {
            msg = null;
            lock (mListInterlock) {
                if (targetIndex >= mConnectedClientList.Count) return false;
                if (!mConnectedClientList[targetIndex].IsConnected()) return false;

                mConnectedClientList[targetIndex].GetRecvMessage(out msg);
            }
            return true;
        }

        public bool GetRecvMessage(string targetIp, out string msg) {
            msg = null;
            ConnectedClient client = GetClient(targetIp);
            if (client == null) return false;
            if (!client.IsConnected()) return false;

            client.GetRecvMessage(out msg);
            return true;
        }
        
    }
}
