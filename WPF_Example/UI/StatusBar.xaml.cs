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
    /// StatusBar.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class StatusBar : UserControl {
        private MainWindow mParentWindow;
        public StatusBarModel Model { get; }
        public StatusBar() {
            InitializeComponent();
            Model = new StatusBarModel();
            DataContext = Model;
        }

        private void StatusBar_Loaded(object sender, RoutedEventArgs e) {
            mParentWindow = (MainWindow)Window.GetWindow(this);
        }
    }
}
