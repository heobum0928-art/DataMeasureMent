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
using PropertyTools.Wpf;

namespace ReringProject.UI {
    /// <summary>
    /// LightChannelView.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class LightChannelView : UserControl {
        public LightChannelView(LightChannelViewModel model) {
            InitializeComponent();
            this.DataContext = model;
        }

        public void UpdateBindingTarget() {
            BindingExpression binding = checkBox_on.GetBindingExpression(CheckBox.IsCheckedProperty);
            binding.UpdateTarget();

            binding = slider_level.GetBindingExpression(HeaderedEntrySlider.ValueProperty);
            binding.UpdateTarget();
        }
    }
}
