using System.Collections.Generic;
using System.Windows;

namespace ReringProject.UI {
    /// <summary>
    /// 옵션이 정해진 입력용 모달 콤보 다이얼로그. 자유 텍스트는 TextInputBox 사용.
    /// 사용 예: 측정 타입 선택 (InspectionListView.AddMeasurementToFAI).
    /// </summary>
    public static class ComboInputBox {
        private static ComboInputBoxWindow _ComboBox;

        private static void Close() {
            if (_ComboBox != null) {
                if (_ComboBox.IsVisible) {
                    _ComboBox.Close();
                    _ComboBox = null;
                }
            }
        }

        public static Window Parent { get; set; } = null;

        /// <summary>
        /// 콤보박스 입력 다이얼로그를 띄운다.
        /// </summary>
        /// <param name="title">다이얼로그 제목/안내</param>
        /// <param name="items">선택 옵션 목록</param>
        /// <param name="initialSelection">초기 선택값 (items 내 미존재 시 첫 번째 항목)</param>
        /// <param name="selectedText">사용자가 선택한 옵션 (Cancel 시 null)</param>
        /// <returns>true: Ok, false: Cancel 또는 옵션 없음</returns>
        public static bool Show(string title, IEnumerable<string> items, string initialSelection, out string selectedText) {
            Close();
            selectedText = null;
            _ComboBox = new ComboInputBoxWindow(title, items, initialSelection);
            _ComboBox.Owner = Parent;
            _ComboBox.Topmost = true;

            if (Parent == null) {
                _ComboBox.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            }
            bool dialogResult = (bool)_ComboBox.ShowDialog();
            selectedText = _ComboBox.SelectedText;
            return dialogResult;
        }
    }
}
