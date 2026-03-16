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
    /// CalibrationControl.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class CalibrationView : UserControl {
        public CalibrationViewModel Calibration { get; set; }

        public CalibrationView(CalibrationViewModel calibrationView) {
            InitializeComponent();
            Calibration = calibrationView;
            DataContext = Calibration;

            switch (Calibration.CalType) {
                case ECalibrationType.Calibration:
                    
                    break;
            }
        }

        public void RegisterApplyButtonClick(RoutedEventHandler applyEvent){
            button_apply.Click += applyEvent;
        }      
    }
}
