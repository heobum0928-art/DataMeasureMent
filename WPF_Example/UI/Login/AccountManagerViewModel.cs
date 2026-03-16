using ReringProject.Login;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReringProject.UI
{
    public class AccountManagerViewModel : INotifyPropertyChanged {
        public event PropertyChangedEventHandler PropertyChanged;

        private LoginManager Handle;

        private ObservableCollection<AccountInfo> _AccountManager;
        public ObservableCollection<AccountInfo> AccountList {
            get {
                return _AccountManager;
            }
            set {
                _AccountManager = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("AccountList"));
            }
        }

        private AccountInfo _SelectedAccount;
        public AccountInfo SelectedAccount {
            get {
                return _SelectedAccount;
            }
            set {
                _SelectedAccount = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("SelectedAccount"));
            }
        }

        private int _SelectedIndex;
        public int SelectedIndex {
            get { return _SelectedIndex; }
            set {
                _SelectedIndex = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("SelectedIndex"));
            }
        }

        public AccountManagerViewModel(LoginManager handle) {
            Handle = handle;
        }

        public void UpdateList() {
            AccountList = Handle.AccountList;

        }
    }
}
