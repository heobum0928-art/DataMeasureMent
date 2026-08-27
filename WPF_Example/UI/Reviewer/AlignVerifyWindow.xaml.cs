using System.Windows;

namespace ReringProject.UI
{
    /// <summary>
    /// Align 정합 조회 창. 계산/포맷/파일 IO 는 전부 AlignVerifyViewModel 에 있다.
    /// 이 code-behind 는 배선만 한다.
    /// </summary>
    public partial class AlignVerifyWindow : Window
    {
        private readonly AlignVerifyViewModel _vm = new AlignVerifyViewModel();

        public AlignVerifyWindow()
        {
            InitializeComponent();
            DataContext = _vm;
        }

        /// <summary>자재번호 초기값 주입(리뷰어의 입력란 값 복사).</summary>
        public void SetInitialMaterial(string szMaterialNo)
        {
            _vm.MaterialNoText = szMaterialNo;
        }

        private void Button_Query_Click(object sender, RoutedEventArgs e)
        {
            _vm.ExecuteQuery();
        }
    }
}
