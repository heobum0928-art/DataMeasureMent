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
    /// LightControllerView.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class LightControllerView : UserControl {
        private LightControllerViewModel pModel;
        public LightControllerView(LightControllerViewModel model) {
            pModel = model;
            InitializeComponent();
            this.DataContext = model;

            for(int i = 0; i < pModel.Channels.Length; i++) {
                LightChannelViewModel channelModel = new LightChannelViewModel(pModel.Channels[i], i);
                LightChannelView channelView = new LightChannelView(channelModel);
                stackPanel_content.Children.Add(channelView);

                /*
                HeaderedEntrySlider slider = new HeaderedEntrySlider();
                slider.Margin = new Thickness(2, 2, 2, 2);

                CheckBox checkBox = new CheckBox();
                checkBox.Margin = new Thickness(2, 2, 2, 2);
                
                slider.Maximum = pModel.Controller.MaxLevel;
                slider.Minimum = pModel.Controller.MinLevel;
                slider.Value = pModel.Channels[i].Level;

                checkBox.Content = pModel.Channels[i].Name;
                checkBox.IsChecked = pModel.Channels[i].On;
                
                stackPanel_content.Children.Add(checkBox);
                stackPanel_content.Children.Add(slider);
                */
            }
            
        }

        public void UpdateBindingTarget() {
            BindingExpression binding = textBlock_state.GetBindingExpression(TextBlock.TextProperty);
            binding.UpdateTarget();

            for(int i = 0; i < stackPanel_content.Children.Count; i++) {
                UIElement uiElement = stackPanel_content.Children[i];

                if(uiElement is LightChannelView) {
                    LightChannelView view = uiElement as LightChannelView;
                    view.UpdateBindingTarget();
                }
            }
        }

        private void Button_close_Click(object sender, RoutedEventArgs e) {
            pModel.Controller.Close();
        }

        private void Button_open_Click(object sender, RoutedEventArgs e) {
            if(pModel.Controller.Open() == false) {
                //Message box 
            }
        }
    }
}
