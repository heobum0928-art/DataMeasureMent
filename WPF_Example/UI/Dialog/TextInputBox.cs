using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace ReringProject.UI {
    public static class TextInputBox {
        private static TextInputBoxWinidow _InputBox;

        private static void Close() {
            if (_InputBox != null) {
                if (_InputBox.IsVisible) {
                    _InputBox.Close();
                    _InputBox = null;
                }
            }
        }

        public static Window Parent { get; set; } = null;

        public static bool Show(string title, string initialText, out string inputText) {
            Close();
            inputText = null;
            _InputBox = new TextInputBoxWinidow(title, initialText);
            _InputBox.Owner = Parent;
            _InputBox.Topmost = true;

            bool dialogResult = false;
            if (Parent == null) {
                _InputBox.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            }
            dialogResult = (bool)_InputBox.ShowDialog();
            inputText = _InputBox.Text;
            return dialogResult;
        }
    }
}
