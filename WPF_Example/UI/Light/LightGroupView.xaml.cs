using ReringProject.Device;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using PropertyTools.Wpf;
using System.Windows.Data;

namespace ReringProject.UI {
    /// <summary>
    /// UserControl1.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class LightGroupView : UserControl {
        private LightHandler pHandle;
        private LightGroupViewModel pModel;

        public LightGroupView(LightGroupViewModel groupModel) {
            pHandle = LightHandler.Handle;
            pModel = groupModel;

            InitializeComponent();
            this.DataContext = groupModel;
        }

        public void UpdateBindingTarget() {
            BindingExpression binding = checkBox_on.GetBindingExpression(CheckBox.IsCheckedProperty);
            binding.UpdateTarget();

            binding = slider_value.GetBindingExpression(HeaderedEntrySlider.ValueProperty);
            binding.UpdateTarget();
        }
    }
}
