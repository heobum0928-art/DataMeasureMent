using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ReringProject.Login;

namespace ReringProject.UI {
    /// <summary>
    /// LoginWindow.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class LoginWindow : Window {
        private LoginManager Login;

        public LoginWindow() {
            InitializeComponent();
            Login = SystemHandler.Handle.Login;

            //init combobox 
            comboBox_id.ItemsSource = Login.GetIDList();
        }

        //login
        private void Btn_ok_Click(object sender, RoutedEventArgs e) {
            //attemp login
            string id = comboBox_id.SelectedValue.ToString();
            string password = passwordBox.Password;
            if(Login.Login(id, password)) {
                DialogResult = true;
                this.Close();
                return;
            }
            //login fail
            CustomMessageBox.Show(SystemHandler.Handle.Localize["Login Fail"], SystemHandler.Handle.Localize["password does not match."], MessageBoxImage.Information);
            passwordBox.Password = "";
        }

        private void ComboBox_id_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            passwordBox.Password = "";
            passwordBox.Focus();

            string selectedID = comboBox_id.SelectedItem.ToString();
            if (selectedID == Login.LoginID) {
                btn_ok.IsEnabled = false;
            }
            else {
                btn_ok.IsEnabled = true;
            }
        }

        private void Button_logOut_Click(object sender, RoutedEventArgs e) {
            Login.LogOut();

            this.DialogResult = true;
            this.Close();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e) {
            //show current login id
            if (Login.IsLogin && (Login.LoginAccount != null)) {
                label_logined.Content = Login.LoginAccount.ID;
                label_logined.Foreground = Brushes.Blue;
                button_logOut.IsEnabled = true;
            }
            else {
                label_logined.Content = SystemHandler.Handle.Localize["Not Login"];
                label_logined.Foreground = Brushes.Red;
                button_logOut.IsEnabled = false;
            }
            if(Login.IsLogin && (Login.LoginAccount.Grade == EAccountGrade.Admin)) {
                btn_edit.IsEnabled = true;
            }
            else {
                btn_edit.IsEnabled = false;
            }

            if (comboBox_id.Items.Count > 0) {
                comboBox_id.SelectedIndex = 0;
            }
        }

        private void Btn_edit_Click(object sender, RoutedEventArgs e) {
            AccountManagerWindow accountWindow = new AccountManagerWindow();
            accountWindow.Owner = this;
            if(accountWindow.ShowDialog() == true) {
                comboBox_id.ItemsSource = Login.GetIDList();
            }
        }

        private void PasswordBox_KeyUp(object sender, System.Windows.Input.KeyEventArgs e) {
            if(e.Key == System.Windows.Input.Key.Return) {
                Btn_ok_Click(this, null);
            }
        }
    }
}
