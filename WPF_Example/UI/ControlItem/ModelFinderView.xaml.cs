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
using PropertyTools.DataAnnotations;
using ReringProject.Device;

namespace ReringProject.UI {
    /// <summary>
    /// ModelFinderControl.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class ModelFinderView : UserControl {
        public ModelFinderViewModel Model { get; set; }

        public ModelFinderView(ModelFinderViewModel modelView) {
            InitializeComponent();
            Model = modelView;
            DataContext = Model;

            switch (Model.AlgType) {
                case EAlgorithmType.ModelFinder:
                    
                    break;
                case EAlgorithmType.PatternMatch:
                    button_edit.Content = "Show";
                    button_new.Content = "Set Master";
                    
                    break;
            }
        }

        public void RegisterNewButtonClick(RoutedEventHandler newEvent){
            button_new.Click += newEvent;
        }

        public void RegisterEditButtonClick(RoutedEventHandler editEvent) {
            button_edit.Click += editEvent;
        }

        public void RegisterLoadButtonClick(RoutedEventHandler loadEvent) {
            button_load.Click += loadEvent;
        }       
    }
}
