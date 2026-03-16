using ReringProject.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace ReringProject.UI {
    /// <summary>
    /// LogView.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class LogView : UserControl {
        public LogView() {
            InitializeComponent();
            for (int i = 0; i < Logging.GetLogCount(); i++) {
                string name = Logging.GetLogNameByIndex(i);
                TabItem tab = new TabItem();
                ListBox lb = new ListBox();
                Logging.SetListControlByIndex(i, lb);

                tab.Header = name;
                tab.Content = lb;
                tabControl_logging.Items.Add(tab);
            }
        }
    }
}
