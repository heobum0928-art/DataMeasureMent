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

namespace ReringProject.UI {
    /// <summary>
    /// TextInputBox.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class TextInputBoxWinidow : Window {
        public string Text { get; private set; }

        public TextInputBoxWinidow(string message, string initialText) {
            InitializeComponent();
            label_title.Content = message;
            textBox_text.Text = initialText;
        }

        private void Button_ok_Click(object sender, RoutedEventArgs e) {
            Text = textBox_text.Text;
            this.DialogResult = true;
            this.Close();
        }

        private void Button_cancel_Click(object sender, RoutedEventArgs e) {
            this.DialogResult = false;
            this.Close();
        }

        private void Window_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e) {
            if (this.IsVisible) {
                textBox_text.SelectAll();
            }
        }

        private void TextBox_text_KeyUp(object sender, KeyEventArgs e) {
            if(e.Key == Key.Return) {
                Button_ok_Click(this, null);
            }
        }
    }
}
