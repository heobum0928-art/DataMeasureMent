using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Media.Imaging;
using ReringProject.Setting;
using ReringProject.Utility;

namespace ReringProject.UI {

    public class ImageLoaderConverter : IValueConverter {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            string imagePath = value as string;

            if (string.IsNullOrEmpty(imagePath) || !File.Exists(imagePath)) {
                return null;
            }

            BitmapImage image = new BitmapImage();

            using (var stream = new FileStream(imagePath, FileMode.Open, FileAccess.Read)) {
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.StreamSource = stream;
                image.EndInit();
            }

            image.Freeze(); // 이미지가 스레드에서 안전하게 사용되도록 함
            return image;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            throw new NotImplementedException();
        }
    }

    public class RecipeListViewModel : INotifyPropertyChanged{
        private ObservableCollection<RecipeFileInfo> _Items;
        public ObservableCollection<RecipeFileInfo> Items {
            get {
                return _Items;
            }

            set {
                _Items = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Items"));
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

        private RecipeFileInfo _SelectedItem;
        public RecipeFileInfo SelectedItem {
            get { return _SelectedItem; }
            set {
                _SelectedItem = value;
                if(_SelectedItem != null) {
                    ThumbnailPath = SelectedItem.ThumbnailPath;
                    SummaryPath = SelectedItem.SummaryPath;
                }
                else {
                    ThumbnailPath = null;
                    SummaryPath = null;
                }
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("SelectedItem"));
            }
        }

        private string _ThumbnailPath;
        public string ThumbnailPath {
            get {
                if (SelectedItem == null || (File.Exists(_ThumbnailPath) == false)) return RecipeFiles.DEFAULT_THUMBNAIL;
                return SelectedItem.ThumbnailPath;
            }

            set {
                _ThumbnailPath = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("ThumbnailPath"));
            }
        }

        private string _SummaryPath;
        public string SummaryPath {
            get { return _SummaryPath; }
            set {
                _SummaryPath = value;
                if (File.Exists(_SummaryPath)) {
                    //reading text
                    FileStream fs = null;
                    StreamReader sr = null;
                    try {
                        fs = new FileStream(_SummaryPath, FileMode.Open);
                        sr = new StreamReader(fs);
                        SummaryText = sr.ReadToEnd();
                    }
                    catch(Exception e) {
                        Logging.PrintLog((int)ELogType.Error, string.Format(SystemHandler.Handle.Localize["Fail to Summary file read. Path : {0} ({1})"], SummaryPath, e.Message));
                    }
                    finally {
                        if (sr != null) sr.Close();
                        if (fs != null) fs.Close();
                    }
                }
                else {
                    SummaryText = "input text";
                }
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("SelectedItem"));
            }
        }

        private string _SummaryText;
        public string SummaryText {
            get {
                return _SummaryText;
            }
            set {
                _SummaryText = value;
                PropertyChanged.Invoke(this, new PropertyChangedEventArgs("SummaryText"));
            }
        }

        public RecipeListViewModel() {
            Items = SystemHandler.Handle.Recipes.List;
        }

        public bool SaveSummaryText() {
            bool result = false;
            if (SelectedItem == null) return false;
            string savePath = SummaryPath;

            FileStream fs = null;
            StreamWriter sw = null;
            try {
                fs = new FileStream(savePath, FileMode.Create);
                sw = new StreamWriter(fs);
                sw.WriteLine(SummaryText);
                result = true;
            }
            catch(Exception e) {
                Logging.PrintLog((int)ELogType.Error, string.Format(SystemHandler.Handle.Localize["Fail to Summary file write. Path : {0} ({1})"], SummaryPath, e.Message));
                result = false;
            }
            finally {
                if(sw != null) sw.Close();
                if(fs != null) fs.Close();
            }
            return result;
        }


        public event PropertyChangedEventHandler PropertyChanged;
    }
}
