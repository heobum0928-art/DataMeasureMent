using System.Windows;
using System.Windows.Controls;

namespace ReringProject.UI
{
    /// <summary>브러시 마스킹 사이드 패널. 저장·재생성·상태문구는 전부 ViewModel 이 처리한다.</summary>
    public partial class PatternBrushPanel : UserControl
    {
        public PatternBrushMaskViewModel ViewModel { get; private set; }

        public PatternBrushPanel()
        {
            InitializeComponent();
            ViewModel = new PatternBrushMaskViewModel();
            DataContext = ViewModel;
        }

        private void ClearMaskButton_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel == null)
            {
                return;
            }
            ViewModel.ClearMask();
        }
    }
}
