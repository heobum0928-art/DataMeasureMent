using ReringProject.Setting;
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
using System.Windows.Shapes;
using WPF.MDI;

namespace ReringProject.UI
{
    /// <summary>
    /// LogViewWindow.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class LogViewWindow : Window
    {
        private string _OriginalTitle;

        private Dictionary<ELogType, MdiChild> ChildList = new Dictionary<ELogType, MdiChild>();

        public LogViewWindow()
        {
            InitializeComponent();
            _OriginalTitle = Title;
            Container.Children.CollectionChanged += (o, e) => UpdateWindowsMenu();
            Container.MdiChildTitleChanged += Container_MdiChildTitleChanged;

            //init add menu
            string[] logTypes = Enum.GetNames(typeof(ELogType));
            for (int i = 0; i < logTypes.Length; i++) {
                MenuItem menu_add = new MenuItem();
                menu_add.Header = logTypes[i];
                menu_add.Click += AddNewLogView;
                menuItem_add.Items.Add(menu_add);
            }
            ChildList.Clear();
        }

        private void UpdateWindowsMenu() {
            menuItem_windows.Items.Clear();
            MenuItem mi;
            for (int i = 0; i < Container.Children.Count; i++) {
                MdiChild child = Container.Children[i];
                mi = new MenuItem { Header = child.Title };
                mi.Click += (o, e) => child.Focus();
                menuItem_windows.Items.Add(mi);
            }
            menuItem_windows.Items.Add(new Separator());
            menuItem_windows.Items.Add(mi = new MenuItem { Header = "Cascade" });
            mi.Click += (o, e) => Container.MdiLayout = MdiLayout.Cascade;
            menuItem_windows.Items.Add(mi = new MenuItem { Header = "Horizontally" });
            mi.Click += (o, e) => Container.MdiLayout = MdiLayout.TileHorizontal;
            menuItem_windows.Items.Add(mi = new MenuItem { Header = "Vertically" });
            mi.Click += (o, e) => Container.MdiLayout = MdiLayout.TileVertical;

            menuItem_windows.Items.Add(new Separator());
            menuItem_windows.Items.Add(mi = new MenuItem { Header = "Close all" });
            mi.Click += (o, e) => Container.Children.Clear();
        }

        private void AddNewLogView(object sender, RoutedEventArgs e) {
            MenuItem selected = (MenuItem)sender as MenuItem;
            if (selected == null) return;
            ELogType logType = (ELogType)Enum.Parse(typeof(ELogType), selected.Header.ToString());
            if (ChildList.ContainsKey(logType)) {
                ChildList[logType].Focus();
                return;
            }

            LogViewChildWindow child = new LogViewChildWindow(logType);
            ChildList.Add(logType, child);
            this.Container.Children.Add(child);

            Container.Theme = ThemeType.Luna;
        }

        private void Container_MdiChildTitleChanged(object sender, RoutedEventArgs e) {
            if (Container.ActiveMdiChild != null && Container.ActiveMdiChild.WindowState == WindowState.Maximized)
                Title = _OriginalTitle + " - [" + Container.ActiveMdiChild.Title + "]";
            else
                Title = _OriginalTitle;

        }

    }

    
}
