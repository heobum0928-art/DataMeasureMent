using ReringProject.Setting;
using ReringProject.Utility;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Input;
using WPF.MDI;

namespace ReringProject.UI
{
    public class LogViewChildWindow : MdiChild {
     
        public ELogType LogType { get; private set; }

        private ListBox LogListBox = new ListBox();

        public LogViewChildWindow(ELogType type) {
            LogType = type;
            this.Title = type.ToString();
            this.Resizable = true;
            this.CloseBox = true;
            this.MaximizeBox = true;
            this.MinimizeBox = true;
            this.ShowIcon = true;
            
            
            DockPanel pnl = new DockPanel();
            pnl.Children.Add(LogListBox);

            Logging.SetListControl((int)type, LogListBox);

            this.Content = pnl;
        }

   

    }
}
