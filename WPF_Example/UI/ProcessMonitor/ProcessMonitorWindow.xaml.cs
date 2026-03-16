using ReringProject.Sequence;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Threading;

namespace ReringProject.UI {
    /// <summary>
    /// ProcessMonitorWindow.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class ProcessMonitorWindow : Window {
        public ProcessMonitorModel Model { get; }
        public ProcessMonitorWindow() {
            InitializeComponent();

            Model = new ProcessMonitorModel();
            this.DataContext = Model;
        }
    }


    public class ProcessMonitorModel {

        public class SequenceStateModel : INotifyPropertyChanged {
            private SequenceBase pSeq;
            public string Name { get; private set; }

            public string State { get; private set; }

            public string Type { get; private set; }

            public string Action { get; private set; }

            public string Time { get; private set; }

            public SequenceStateModel(SequenceBase seq) {
                pSeq = seq;
                
                Type = "Sequence";
                Name = pSeq.Name;
                
                //PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Name"));
                //PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Type"));
            }

            public event PropertyChangedEventHandler PropertyChanged;

            public void UpdateState() {
                State = pSeq.State.ToString();
                Action = pSeq.CurActionName;
                string timeStr = "";
                if (pSeq.Context.Timer.Elapsed.Hours > 0) {
                    timeStr = string.Format("{0}h ", pSeq.Context.Timer.Elapsed.Hours);
                }
                if (pSeq.Context.Timer.Elapsed.Minutes > 0) {
                    timeStr = string.Format("{0}m ", pSeq.Context.Timer.Elapsed.Minutes);
                }
                timeStr += string.Format("{0}s", pSeq.Context.Timer.Elapsed.Seconds);

                Time = timeStr;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("State"));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Action"));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Time"));
            }
        }
        
        public List<SequenceStateModel> StateList { get; } = new List<SequenceStateModel>();
        private SequenceHandler pSeq;

        public ProcessMonitorModel() {
            pSeq = SystemHandler.Handle.Sequences;
            for (int i = 0; i < pSeq.Count; i++) {
                SequenceStateModel model = new SequenceStateModel(pSeq[i]);
                StateList.Add(model);
            }
            
        }
        public void UpdateState() {
            foreach(SequenceStateModel model in StateList) {
                model.UpdateState();
            }
        }
    }
}
