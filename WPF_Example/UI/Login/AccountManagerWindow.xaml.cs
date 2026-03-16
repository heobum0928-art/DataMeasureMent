using ReringProject.Login;
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
    /// AccountManager.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class AccountManagerWindow : Window {
        private LoginManager pLogin;
        private AccountManagerViewModel Model;

        private bool isAdding = true;
        public AccountManagerWindow() {
            InitializeComponent();
            pLogin = SystemHandler.Handle.Login;
            Model = new AccountManagerViewModel(pLogin);
            this.DataContext = Model;

            isAdding = true;
            Model.UpdateList();
        }

        private void Button_new_Click(object sender, RoutedEventArgs e) {
            isAdding = true;
            Model.SelectedIndex = -1;

            textBox_id.IsEnabled = true;
            groupBox_selected.Header = SystemHandler.Handle.Localize["New Account"];
            textBox_id.Text = "";
            textBox_password.Password = "";
            button_delete.IsEnabled = false;
        }

        private void Button_save_Click(object sender, RoutedEventArgs e) {
            string id = textBox_id.Text;
            string gradeStr = comboBox_grade.SelectedItem.ToString();
            string password = textBox_password.Password;
            EAccountGrade grade = (EAccountGrade)Enum.Parse(typeof(EAccountGrade), gradeStr);

            if (isAdding) {    
                if(pLogin.AddAccount(id, grade, password) == false) {
                    //occurs error
                    return;
                }
                CustomMessageBox.Show(SystemHandler.Handle.Localize["Created a New Account"], string.Format(SystemHandler.Handle.Localize["New Account : {0}"], id), MessageBoxImage.Information);
            }
            else {
                if(pLogin.UpdateAccount(id, grade, password) == false) {
                    //occurs error
                    return;
                }
                CustomMessageBox.Show(SystemHandler.Handle.Localize["Updated Selected Account"], string.Format(SystemHandler.Handle.Localize["Updated : {0}"], id), MessageBoxImage.Information);
            }
            Model.UpdateList();
        }

        private void Button_cancel_Click(object sender, RoutedEventArgs e) {
            if(pLogin.Load() == false) {
                //occurs error
                return;
            }
            this.DialogResult = false;
            this.Close();
        }

        private void Button_ok_Click(object sender, RoutedEventArgs e) {
            if(pLogin.Save() == false) {
                //occurs error 
                return;
            }
            this.DialogResult = true;
            this.Close();
        }

        private void ListView_accountList_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            if (Model.SelectedIndex < 0) return;

            isAdding = false;
            textBox_id.IsEnabled = false;
            button_delete.IsEnabled = true;
            int index = Model.SelectedIndex;
            AccountInfo selected = Model.AccountList[index];
            //AccountInfo selected = Model.SelectedAccount;
            textBox_id.Text = selected.ID;
            comboBox_grade.SelectedIndex = comboBox_grade.Items.IndexOf(selected.Grade);
            textBox_password.Password = selected.Password;
            groupBox_selected.Header = selected.ID;
        }

        private void Button_delete_Click(object sender, RoutedEventArgs e) {
            if (Model.SelectedIndex < 0) return;

            string selectedID = Model.SelectedAccount.ID;
            MessageBoxResult result = CustomMessageBox.ShowConfirmation(SystemHandler.Handle.Localize["Confirm Delete Account"], string.Format(SystemHandler.Handle.Localize["Are you sure you want to delete {0} account?"], selectedID), MessageBoxButton.YesNo);
            if(result != MessageBoxResult.Yes) {
                return;
            }

            if(pLogin.DeleteAccount(selectedID) == false) {
                //occurs error
            }
            Model.UpdateList();
        }
    }
}
