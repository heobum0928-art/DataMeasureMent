using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;

namespace ReringProject.UI {
    /// <summary>
    /// ComboInputBoxWindow.xaml에 대한 상호 작용 논리.
    /// 옵션이 정해진 입력에 사용 (예: 측정 타입). 자유 텍스트 입력은 TextInputBoxWinidow 사용.
    /// </summary>
    public partial class ComboInputBoxWindow : Window {

        public string SelectedText { get; private set; }

        public ComboInputBoxWindow(string message, IEnumerable<string> items, string initialSelection) {
            InitializeComponent();
            label_title.Content = message;
            if (items != null) {
                foreach (var item in items) {
                    combo_items.Items.Add(item);
                }
            }
            if (combo_items.Items.Count > 0) {
                if (!string.IsNullOrEmpty(initialSelection) && combo_items.Items.Contains(initialSelection)) {
                    combo_items.SelectedItem = initialSelection;
                }
                else {
                    combo_items.SelectedIndex = 0; // fallback: 첫 번째 옵션
                }
            }
        }

        private void Button_ok_Click(object sender, RoutedEventArgs e) {
            // SelectedItem 이 null(빈 옵션)이면 다이얼로그가 의미 없으니 cancel 처리
            if (combo_items.SelectedItem == null) {
                this.DialogResult = false;
                this.Close();
                return;
            }
            SelectedText = combo_items.SelectedItem.ToString();
            this.DialogResult = true;
            this.Close();
        }

        private void Button_cancel_Click(object sender, RoutedEventArgs e) {
            this.DialogResult = false;
            this.Close();
        }

        private void Window_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e) {
            if (this.IsVisible) {
                combo_items.Focus();
            }
        }

        private void Combo_items_KeyUp(object sender, KeyEventArgs e) {
            if (e.Key == Key.Return) {
                Button_ok_Click(this, null);
            }
        }
    }
}
