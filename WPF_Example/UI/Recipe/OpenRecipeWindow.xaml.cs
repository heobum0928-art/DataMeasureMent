
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using ReringProject.Device;
using ReringProject.Utility;

namespace ReringProject.UI {
    /// <summary>
    /// OpenRecipe.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class OpenRecipeWindow : Window {
        private RecipeListViewModel Model;
        public OpenRecipeWindow() {
            SystemHandler.Handle.Recipes.CollectRecipe();
            InitializeComponent();
            
            Model = new RecipeListViewModel();
            this.DataContext = Model;
        }

        public string SelectedRecipeName {
            get {
                if (Model.SelectedItem == null) return null;
                return Model.SelectedItem.Name;
            }
        }

        private void Btn_Ok_Click(object sender, RoutedEventArgs e) {

            DialogResult = true;
            Close();
        }

        private void Btn_Cancel_Click(object sender, RoutedEventArgs e) {
            DialogResult = false;
            Close();
        }

        //open event
        private void Window_Loaded(object sender, RoutedEventArgs e) {
            
        }

        private void FreeImage() {
            BitmapImage bi = new BitmapImage();
            bi.BeginInit();
            bi.CacheOption = BitmapCacheOption.OnLoad;
            bi.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
            bi.UriSource = new Uri("pack://application:,,,/Resource/error.png", UriKind.RelativeOrAbsolute);
            bi.EndInit();
            img_Selected.Source = bi;
        }
        
        private void Btn_Copy_Click(object sender, RoutedEventArgs e) {
            if (SelectedRecipeName == null) return; 
            if((SystemHandler.Handle.Login.IsLogin == false) || (SystemHandler.Handle.Login.LoginAccount.Grade < Login.EAccountGrade.Admin)) {
                CustomMessageBox.Show(SystemHandler.Handle.Localize["Permission denied"], SystemHandler.Handle.Localize["Requires admin privileges."], MessageBoxImage.Error);
                return;
            }

            bool dlgResult = TextInputBox.Show(SystemHandler.Handle.Localize["Enter the name of new recipe to copy."], SelectedRecipeName, out string inputText);

            if (dlgResult == false) {
                return;
            }
            string newName = inputText;
            if(newName == SelectedRecipeName) {
                CustomMessageBox.Show("Error", SystemHandler.Handle.Localize["Recipe name to be copied must be different."], MessageBoxImage.Error);
                return;
            }
            if (RecipeFiles.Handle.HasRecipe(newName)) {
                if(CustomMessageBox.ShowConfirmation(newName + SystemHandler.Handle.Localize[" Has Already Exists."], SystemHandler.Handle.Localize["Are you sure you want to overwrite the existing directory?"], MessageBoxButton.YesNo) != MessageBoxResult.Yes) {
                    return;
                }
            }
            try {
                if(RecipeFiles.Handle.Copy(SelectedRecipeName, newName) == false) {
                    CustomMessageBox.Show(SystemHandler.Handle.Localize["Fail to copy recipe"], string.Format(SystemHandler.Handle.Localize["copy fail recipe {0} to {1}."], SelectedRecipeName, newName), MessageBoxImage.Error);
                }
                SystemHandler.Handle.Recipes.CollectRecipe();
                Model.Items = SystemHandler.Handle.Recipes.List;
                
            }
            catch(Exception ex) {
                CustomMessageBox.Show(SystemHandler.Handle.Localize["Fail to copy recipe"], string.Format(SystemHandler.Handle.Localize["copy fail recipe {0} to {1}. ({2})"], SelectedRecipeName, newName, ex.Message), MessageBoxImage.Error);
            }
        }

        private void Btn_Delete_Click(object sender, RoutedEventArgs e) {
            if (SelectedRecipeName == null) return;
            if ((SystemHandler.Handle.Login.IsLogin == false) || (SystemHandler.Handle.Login.LoginAccount.Grade < Login.EAccountGrade.Admin)) {
                CustomMessageBox.Show(SystemHandler.Handle.Localize["Permission denied"], SystemHandler.Handle.Localize["Requires admin privileges."], MessageBoxImage.Error);
                return;
            }
            //confirm message
            if (CustomMessageBox.ShowConfirmation(SystemHandler.Handle.Localize["Delete recipe"], SystemHandler.Handle.Localize["Are you sure you want to delete the recipe directory?\nCancellation is not possible."], MessageBoxButton.OKCancel) != MessageBoxResult.OK) {
                return;
            }
            try {
                //delete
                string removeName = SelectedRecipeName;
                Model.SelectedIndex = -1;
                FreeImage();

                if (RecipeFiles.Handle.Delete(removeName) == false) {
                    throw new Exception("recipe directory delete fail.");
                }
                SystemHandler.Handle.Recipes.CollectRecipe();
                Model.Items = SystemHandler.Handle.Recipes.List;
            }
            catch(Exception ex) {
                CustomMessageBox.Show(SystemHandler.Handle.Localize["Fail to delete recipe"], string.Format(SystemHandler.Handle.Localize["delete fail recipe {0}. ({1})"], SelectedRecipeName, ex.Message), MessageBoxImage.Error);
            }
        }

        private void ListView_Recipe_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e) {
            //display summary image 

            //display summary text
        }

        private void Btn_openDir_Click(object sender, RoutedEventArgs e) {
            System.Diagnostics.Process.Start(SystemHandler.Handle.Setting.RecipeSavePath);
        }

        private void Button_save_Click(object sender, RoutedEventArgs e) {
            if (Model.SelectedItem == null) return;
            if (Model.SaveSummaryText()) {
                CustomMessageBox.Show(SystemHandler.Handle.Localize["saved Summary Text"], string.Format(SystemHandler.Handle.Localize["Saved : {0}"], Model.SummaryPath), MessageBoxImage.Information);
            }
            else {
                CustomMessageBox.Show(SystemHandler.Handle.Localize["fail to save summary"], string.Format(SystemHandler.Handle.Localize["save Fail : {0}"], Model.SummaryPath), MessageBoxImage.Error);
            }
        }

        private void Button_importThumbnail_Click(object sender, RoutedEventArgs e) {
            if (Model.SelectedItem == null) return;
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.DefaultExt = DeviceHandler.EXTENSION_SAVE_IMAGE;
            openFileDialog.Filter = DeviceHandler.FILTER_SAVE_IMAGE;
            openFileDialog.InitialDirectory = SystemHandler.Handle.Setting.ImageSavePath;
            bool result = (bool)openFileDialog.ShowDialog();
            if (result == false) return;

            try{
                //file copy from my recipe path
                string sourceFile = openFileDialog.FileName;
                string targetFile = Model.SelectedItem.ThumbnailPath;
                File.Copy(sourceFile, targetFile);

                Model.ThumbnailPath = targetFile;
            }
            catch(Exception ex) {
                CustomMessageBox.Show(SystemHandler.Handle.Localize["fail to load thumbnail"], string.Format(SystemHandler.Handle.Localize["load Fail : {0}, ({1})"], Model.ThumbnailPath, ex.Message), MessageBoxImage.Error);
            }
        }

        private void Img_Selected_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e) {

        }
    }
}
