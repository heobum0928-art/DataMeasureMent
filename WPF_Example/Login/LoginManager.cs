using Newtonsoft.Json;
using ReringProject.Setting;
using ReringProject.Utility;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace ReringProject.Login {
    public enum EAccountGrade {
        Admin,
        Engineer
    }

    public class AccountInfo : INotifyPropertyChanged{

        private string _ID;
        public string ID {
            get { return _ID; }
            set {
                _ID = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("ID"));
            }
        }

        private EAccountGrade _Grade;
        public EAccountGrade Grade {
            get {
                return _Grade;
            }
            set {
                _Grade = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Grade"));
            }
        }

        public string GradeStr {
            get {
                return _Grade.ToString();
            }
            set {
                _Grade = (EAccountGrade)Enum.Parse(typeof(EAccountGrade), value);
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Grade"));
            }
        }

        public string Password { get; set; }

        public AccountInfo() {

        }

        public AccountInfo(string id, EAccountGrade grade, string password) {
            ID = id;
            Grade = grade;
            Password = password;
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }

    public class LoginEventArgs : EventArgs {
        public string LoginID { get; private set; } = "Operator";
        public bool IsLogined { get; private set; } = false;

        public LoginEventArgs(bool isLogin, string id) {
            LoginID = id;
            IsLogined = isLogin;
        }
    }

    public delegate void LoginStateChanged(object sender, LoginEventArgs args);

    public sealed class LoginManager {
        private const string DEFAULT_ADMIN_ID = "admin";
        private const string DEFAULT_ADMIN_PASSWORD = "admin";

        private const string DEFAULT_OPERATOR_ID = "operator";

        private readonly string ACCOUNT_FILE;
        private static string PASSWORD = "1Alg!Young!Min22"; //16자 이상
        private static readonly string KEY = PASSWORD.Substring(0, 128 / 8); //8bit단위로 나눔

        public static LoginManager Handle { get; } = new LoginManager();

        public AccountInfo LoginAccount { get; private set; }

        public bool IsLogin { get; private set; }

        public ObservableCollection<AccountInfo> AccountList { get; private set; } = new ObservableCollection<AccountInfo>();

        public event LoginStateChanged OnLoginStateChanged;

        private LoginManager() {
            ACCOUNT_FILE = AppDomain.CurrentDomain.BaseDirectory + @"account.db";
            if (!Load()) {
                //occurs error or nothing
            }
        }

        public string LoginID {
            get {
                if (IsLogin == false) return DEFAULT_OPERATOR_ID;
                return LoginAccount.ID;
            }
        }

        public string [] GetIDList() {
            string[] idList = new string[AccountList.Count];
            for(int i = 0;i < AccountList.Count; i++) {
                idList[i] = AccountList[i].ID;
            }
            return idList;
        }

        public bool AddAccount(string id, EAccountGrade grade, string password) {
            int contains = AccountList.Count(ai => ai.ID == id);
            if (contains > 0) return false;

            AccountInfo newAccount = new AccountInfo(id, grade, password);
            AccountList.Add(newAccount);

            return true;
        }

        public bool UpdateAccount(string id, EAccountGrade grade, string password) {
            AccountInfo info = AccountList.First(ai => ai.ID == id);
            if (info == null) return false;
            info.Grade = grade;
            info.Password = password;
            return true;
        }

        public bool DeleteAccount(string id) {
            AccountInfo info = AccountList.First(ai => ai.ID == id);
            if (info == null) return false;
            return AccountList.Remove(info);
        }

        public bool RemoveAccount(string id) {
            if(IsLogin == false) {
                return false;
            }
            if (LoginAccount == null) {
                return false;
            }
            //현재 login 계정이 admin일 때만 계정 삭제 가능.
            if(LoginAccount.Grade != EAccountGrade.Admin) {
                return false;
            }
            for (int i = 0; i < AccountList.Count; i++) {
                if(AccountList[i].ID == id) {
                    AccountList.RemoveAt(i);
                    return true;
                }
            }
            return false;
        }

        public bool Load() {
            //read from file
            if (File.Exists(ACCOUNT_FILE) == false) {
                if (CountOf(EAccountGrade.Admin) == 0) {
                    AccountList.Add(new AccountInfo(DEFAULT_ADMIN_ID, EAccountGrade.Admin, DEFAULT_ADMIN_PASSWORD));
                }
                return false;
            }

            using (FileStream fs = new FileStream(ACCOUNT_FILE, FileMode.Open)) {
                byte[] buff = new byte[fs.Length];

                int offset = 0;
                long readLength = fs.Length > Int32.MaxValue ? Int32.MaxValue : fs.Length;
                int readCount = 0;

                if (readLength == 0) {
                    if (CountOf(EAccountGrade.Admin) == 0) {
                        AccountList.Add(new AccountInfo(DEFAULT_ADMIN_ID, EAccountGrade.Admin, DEFAULT_ADMIN_PASSWORD));
                    }
                    return true; //파일은 있으나 내용이 비었음
                }
                while (readCount < fs.Length) {
                    readCount += fs.Read(buff, offset, (int)readLength);
                    offset = readCount;
                    readLength = fs.Length - readCount;
                    if (readLength > Int32.MaxValue) {
                        readLength = Int32.MaxValue;
                    }
                }
                //decrypt and encoding
                byte[] decrypted = Decrypt(buff, PASSWORD);
                string decryptedStr = Encoding.UTF8.GetString(decrypted);

                //json to list
                AccountList = (ObservableCollection<AccountInfo>)JsonConvert.DeserializeObject(decryptedStr, typeof(ObservableCollection<AccountInfo>));
            }

            //if not account
            if(CountOf(EAccountGrade.Admin) == 0) {
                AccountList.Add(new AccountInfo(DEFAULT_ADMIN_ID, EAccountGrade.Admin, DEFAULT_ADMIN_PASSWORD));
            }
            
            return true;
        }

        public int CountOf(EAccountGrade grade) {
           return AccountList.Count(ai => ai.Grade == grade);
        }

        public bool Save() {
            string jsonStr = JsonConvert.SerializeObject(AccountList);
            byte[] rawData = Encoding.UTF8.GetBytes(jsonStr);
            byte[] encrypted = Encrypt(rawData, PASSWORD);
            using (FileStream fs = new FileStream(ACCOUNT_FILE, FileMode.Create)) {
                int writeCount = 0;
                int offset = 0;
                int writeLength = encrypted.Length > Int32.MaxValue ? Int32.MaxValue : encrypted.Length;

                while(writeCount < encrypted.Length) {
                    fs.Write(encrypted, offset, writeLength);
                    writeCount += writeLength;
                    offset = writeCount;

                    writeLength = encrypted.Length - writeCount;
                    if(writeLength > Int32.MaxValue) {
                        writeLength = Int32.MaxValue;
                    }
                }   
            }
            return true;
        }

        public void LogOut() {
            IsLogin = false;
            LoginAccount = null;
            Logging.PrintLog((int)ELogType.Trace, "[EVENT] Admin Log Out");
            OnLoginStateChanged?.Invoke(this, new LoginEventArgs(IsLogin, null));
        }

        public bool Login(string id, string password) {
            //reading file
            for(int i = 0; i < AccountList.Count; i++) {
                if((AccountList[i].ID == id) && (AccountList[i].Password == password)) {
                    LoginAccount = AccountList[i];
                    IsLogin = true;
                    Logging.PrintLog((int)ELogType.Trace, "[EVENT] {0} Log in", id);
                    OnLoginStateChanged?.Invoke(this, new LoginEventArgs(IsLogin, id));
                    return true;
                }
            }
            return false;
        }

        private static Rfc2898DeriveBytes CreateKey(string password) {
            byte[] keyBytes = Encoding.UTF8.GetBytes(password);         //키값 생성
            byte[] saltBytes = SHA512.Create().ComputeHash(keyBytes);   //솔트값(원본 키값을 알기 어렵게 하는 값)

            Rfc2898DeriveBytes result = new Rfc2898DeriveBytes(keyBytes, saltBytes, 100000);    //키값에 솔트값을 사용해 새로운 키 생성, 마지막에 들어가는 수는 해시 생성의 반복 횟수이다.

            return result;  //키값 반환
        }

        private static Rfc2898DeriveBytes CreateVector(string vector) {
            byte[] vectorBytes = Encoding.UTF8.GetBytes(vector);        //벡터 생성
            byte[] saltBytes = SHA512.Create().ComputeHash(vectorBytes);   //솔트값(원본 벡터를 알기 어렵게 하는 값)

            Rfc2898DeriveBytes result = new Rfc2898DeriveBytes(vectorBytes, saltBytes, 100000);    //벡터에 솔트값을 사용해 새로운 키 생성, 마지막에 들어가는 수는 해시 생성의 반복 횟수이다.

            return result;  //벡터 반환
        }

        private static byte[] Encrypt(byte[] origin, string password) {
            RijndaelManaged aes = new RijndaelManaged();       //AES 알고리즘
            Rfc2898DeriveBytes key = CreateKey(password);            //키값 생성
            Rfc2898DeriveBytes vector = CreateVector("ttEVbAqjGa9WTVYeexersfrvjc1nu7Cm");   //벡터 생성 

            aes.BlockSize = 128;            //AES의 블록 크기는 128 고정이다.
            aes.KeySize = 256;              //AES의 키 크기는 128, 192, 256을 지원한다.
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            aes.Key = key.GetBytes(32);     //AES-256을 사용하므로 키값의 길이는 32여야 한다.
            aes.IV = vector.GetBytes(16);   //초기화 벡터는 언제나 길이가 16이어야 한다.

            //키값과 초기화 벡터를 기반으로 암호화 작업을 하는 클래스 변수를 생성
            ICryptoTransform encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
            
            using (MemoryStream ms = new MemoryStream()) {
                //encryptor 변수에서 암호화된 데이터를 결과에 쓰는 스트림
                using (CryptoStream cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write)) {
                    cs.Write(origin, 0, origin.Length);
                }
                return ms.ToArray();    //암호화된 바이트 배열 반환
            }
        }

        private static byte[] Decrypt(byte[] origin, string password) {
            RijndaelManaged aes = new RijndaelManaged();       //AES 알고리즘
            Rfc2898DeriveBytes key = CreateKey(password);            //키값 생성
            Rfc2898DeriveBytes vector = CreateVector("ttEVbAqjGa9WTVYeexersfrvjc1nu7Cm");   //벡터 생성 

            aes.BlockSize = 128;            //AES의 블록 크기는 128 고정이다.
            aes.KeySize = 256;              //AES의 키 크기는 128, 192, 256을 지원한다.
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            aes.Key = key.GetBytes(32);     //AES-256을 사용하므로 키값의 길이는 32여야 한다.
            aes.IV = vector.GetBytes(16);   //초기화 벡터는 언제나 길이가 16이어야 한다.

            //키값과 초기화 벡터를 기반으로 복호화 작업을 하는 클래스 변수를 생성
            ICryptoTransform decryptor = aes.CreateDecryptor(aes.Key, aes.IV);

            //using블록으로 변수를 사용하면 블록에서 나올때 자동으로 변수가 가비지컬렉팅 된다. 
            using (MemoryStream ms = new MemoryStream()) //결과를 담을 스트림 
            {
                //encryptor 변수에서 복호화된 데이터를 결과에 쓰는 스트림
                using (CryptoStream cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Write)) {
                    cs.Write(origin, 0, origin.Length);
                }
                return ms.ToArray();    //복호화된 바이트 배열 반환
            }
        }
    }
}
