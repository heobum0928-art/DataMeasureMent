using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using ReringProject.Network;


namespace ReringProject.UI {
    /// <summary>
    /// TcpServerWindow.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class TcpServerWindow : Window, INotifyPropertyChanged {
        VisionServer pServer;

        public ObservableCollection<string> ConnectedList { get; set; } = new ObservableCollection<string>();

        public ObservableCollection<string> MessageList { get; set; } = new ObservableCollection<string>();

        private int _SelectedIndex = -1;
        public int SelectedIndex {
            get { return _SelectedIndex; }
            set {
                _SelectedIndex = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("SelectedIndex"));
                comboBox_connectedClient.GetBindingExpression(ComboBox.SelectedIndexProperty).UpdateTarget();
            }
        }

        public string SendText { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;

        public TcpServerWindow() {
            InitializeComponent();

            pServer = SystemHandler.Handle.Server;
            this.DataContext = this;

            pServer.OnAlarm += OnVisionAlarm;
            pServer.OnRecvMessage += OnVisionRecv;
            pServer.OnSendMessage += OnVisionSend;
        }

        private void UpdateClientList() {
            ConnectedList.Clear();
            ConnectedList.Add("All");
            for (int i = 0; i < pServer.GetConnectedClientCount(); i++) {
                string clientIp = pServer.GetClient(i).ToString();
                ConnectedList.Add(clientIp);
            }
            SelectedIndex = 0;
        }

        private void OnVisionRecv(object sender, MessageEventArgs e) {
            if (SelectedIndex < 0) return;
            if (ConnectedList.Count <= SelectedIndex) return;

            if ((SelectedIndex == 0) || (ConnectedList[SelectedIndex] == e.Target)) {
                this.Dispatcher.Invoke(() => {
                    MessageList.Add("[RECV] - " + e.ToString());
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("MessageList"));
                    BindingExpression expr = textBox_send.GetBindingExpression(ListBox.ItemsSourceProperty);
                    if(expr != null){
                        expr.UpdateTarget();
                    }
                });
            }
        }

        private void OnVisionSend(object sender, MessageEventArgs e) {
            if (SelectedIndex < 0) return;
            if (ConnectedList.Count <= SelectedIndex) return;

            if ((SelectedIndex == 0) || (ConnectedList[SelectedIndex] == e.Target)) {
                this.Dispatcher.Invoke(() => {
                    MessageList.Add("[SEND] - " + e.ToString());
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("MessageList"));
                    BindingExpression expr = textBox_send.GetBindingExpression(ListBox.ItemsSourceProperty);
                    if (expr != null) expr.UpdateTarget();
                });
            }
        }

        private void OnVisionAlarm(object sender, AlarmEventArgs args) {
            try {
                switch (args.AlarmType) {
                    case AlarmEventArgs.AlarmEventType.OnConnected:
                        this.Dispatcher.Invoke(() => {
                            ConnectedList.Add(args.Target);
                            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("ConnectedList"));
                            BindingExpression expr = comboBox_connectedClient.GetBindingExpression(ComboBox.ItemsSourceProperty);
                            if(expr != null) {
                                expr.UpdateTarget();
                            }
                        });
                        break;
                    case AlarmEventArgs.AlarmEventType.OnDisconnected:
                        this.Dispatcher.Invoke(() => {
                            int index = ConnectedList.IndexOf(args.Target);
                            if (index >= ConnectedList.Count - 1) {
                                index--;
                                if (index < 0) index = 0;
                                if (ConnectedList.Count == 1) index = -1;
                            }
                            ConnectedList.Remove(args.Target);
                            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("ConnectedList"));
                            BindingExpression expr = comboBox_connectedClient.GetBindingExpression(ComboBox.ItemsSourceProperty);
                            if(expr != null) {
                                expr.UpdateTarget();
                            }

                            SelectedIndex = index;
                            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("SelectedIndex"));
                            expr = comboBox_connectedClient.GetBindingExpression(ComboBox.SelectedIndexProperty);
                            if(expr != null) {
                                expr.UpdateTarget();
                            }
                        });
                        
                        break;
                    case AlarmEventArgs.AlarmEventType.OnSendMessageParsingFail:
                        break;
                    case AlarmEventArgs.AlarmEventType.OnRecvMessageParsingFail:
                        break;
                    case AlarmEventArgs.AlarmEventType.OnRecvTimeOut:
                        break;
                    case AlarmEventArgs.AlarmEventType.OnSendFail:
                        break;
                }
            }
            catch (Exception e) {
                Debug.WriteLine($"[139-TcpServerWindow.xaml.cs] Exception:{e.ToString()}");
            }
        }

        private void Button_send_Click(object sender, RoutedEventArgs e) {
            if(SelectedIndex < 0) {
                return;
            }
            //All 이므로 모두에게 전송한다.
            if(SelectedIndex == 0) {
                for (int i = 0; i < ConnectedList.Count; i++) {
                    pServer.SendMessage(i, SendText);
                }
                return;
            }
            pServer.SendMessage(SelectedIndex-1, SendText);
        }

        private void ComboBox_connectedClient_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            //if (SelectedIndex != 0) 
            {
                this.Dispatcher.Invoke(() => {
                    MessageList.Clear();
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("MessageList"));
                });
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e) {
            UpdateClientList();
            if(ConnectedList.Count > 0) {
                SelectedIndex = 0;
            }
        }

        private void Button_close_Click(object sender, RoutedEventArgs e) {
            this.Close();
        }

        private void Window_Closed(object sender, EventArgs e) {
            pServer.OnAlarm -= OnVisionAlarm;
            pServer.OnRecvMessage -= OnVisionRecv;
            pServer.OnSendMessage -= OnVisionSend;
        }

        private void Button_send_recipe_set_Click(object sender, RoutedEventArgs e) {

        }

        private void Button_send_recipe_list_Click(object sender, RoutedEventArgs e) {

        }

        private void Button_send_light_set_Click(object sender, RoutedEventArgs e) {

        }

        private void Button_send_status_Click(object sender, RoutedEventArgs e) {

        }

        private void Button_grab_status_Click(object sender, RoutedEventArgs e) {

        }

        private void Button_test_result_Click(object sender, RoutedEventArgs e) {

        }
    }

}
