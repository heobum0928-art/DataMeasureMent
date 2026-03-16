using ReringProject.Utility;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using ReringProject.Device;
using ReringProject.Define;
using ReringProject.Sequence;
using Newtonsoft.Json;
using ReringProject.Setting;
using PropertyTools.DataAnnotations;
using System.Diagnostics;
using System.Collections.ObjectModel;

namespace ReringProject.Utility {

    public enum ERecipeFileType {
        Ini,
        Json,
    }

    public class RecipeFileInfo {
        public string Name { get; set; }

        public string FilePath { get; set; }

        public DateTime CreateDate { get; set; }
        public string CreateDateString { get => CreateDate.ToString(); }

        public DateTime LastOpenDate { get; set; }

        public string LastOpenDateString { get => LastOpenDate.ToString(); }

        public string ThumbnailPath {
            get {
                if (File.Exists(FilePath)) {
                    string dir = Path.GetDirectoryName(FilePath);
                    return Path.Combine(dir, RecipeFiles.FILE_THUMBNAIL);
                }
                return Path.Combine(SystemHandler.Handle.Setting.RecipeSavePath, Name, RecipeFiles.FILE_THUMBNAIL);
            }
        }

        public string SummaryPath {
            get {
                if (File.Exists(FilePath)) {
                    string dir = Path.GetDirectoryName(FilePath);
                    return Path.Combine(dir, RecipeFiles.FILE_SUMMARY);
                }
                return Path.Combine(SystemHandler.Handle.Setting.RecipeSavePath, Name, RecipeFiles.FILE_SUMMARY);
            }
        }

    }
    
    public class RecipeFiles {
        public static RecipeFiles Handle { get; } = new RecipeFiles();

        //static recipe List
        private const string FILE_RECIPE = "main";
        private const string EXT_RECIPE = ".ini";

        public const string FILE_THUMBNAIL = "thumb.jpg";
        public const string FILE_SUMMARY = "summary.txt";

        public const string DEFAULT_THUMBNAIL = "/Resource/error.png";

        [Browsable(false)]
        public ObservableCollection<RecipeFileInfo> List { get; private set; } = new ObservableCollection<RecipeFileInfo>();
    
        //public string CurrentSequenceName { get; set; }
        //public string CurrentActionName { get; set; }
    
        private RecipeFiles() {
        }

        public RecipeFileInfo this[int index] {
            get {
                return List[index];
            }
        }

        public string GetModelFilePath(string recipeName, string seqName, string actName, string propertyName) {
            string saveFile = Path.Combine(SystemHandler.Handle.Setting.RecipeSavePath, recipeName, seqName, actName);
            saveFile += propertyName + DeviceHandler.EXTENSION_MODEL;

            string savePath = Path.GetDirectoryName(saveFile);
            if (Directory.Exists(savePath) == false) Directory.CreateDirectory(savePath);
            return saveFile;
        }

        public string GetCalibrationFilePath(string recipeName, string seqName, string actName, string propertyName)
        {
            string saveFile = Path.Combine(SystemHandler.Handle.Setting.CalibrationSavePath, propertyName + DeviceHandler.EXTENSION_CALIBRATION);

            string savePath = Path.GetDirectoryName(saveFile);
            if (Directory.Exists(savePath) == false) Directory.CreateDirectory(savePath);
            return saveFile;
        }

        public string GetPatternImageFilePath(string recipeName, string seqName, string actName, string propertyName){
            string saveFile = Path.Combine(SystemHandler.Handle.Setting.RecipeSavePath, recipeName, seqName, actName);
            saveFile += propertyName + DeviceHandler.EXTENSION_IMAGE;

            string savePath = Path.GetDirectoryName(saveFile);
            if (Directory.Exists(savePath) == false) Directory.CreateDirectory(savePath);
            return saveFile;
        }
        
        public bool Delete(string recipeName) {
            string dirPath = Path.Combine(SystemHandler.Handle.Setting.RecipeSavePath, recipeName);
            if (Directory.Exists(dirPath)) {
                Directory.Delete(dirPath, true);
                return true;
            }
            return false;
        }

        public bool Copy(string prevName, string newName, bool forceCopy = false) {
            //check already exist recipe as newName 
            //get recipe save path
            string prevDirPath = Path.Combine(SystemHandler.Handle.Setting.RecipeSavePath, prevName);
            string newDirPath = Path.Combine(SystemHandler.Handle.Setting.RecipeSavePath, newName);
            
            //해당 dir이 이미 존재함
            if (Directory.Exists(newDirPath) && (forceCopy == false)) {
                return false;
            }

            //폴더 통째로 복사하여 이름바꾼 후에 저장
            CopyFilesRecursively(prevDirPath, newDirPath);
            
            return true;
        }

        private static void CopyFilesRecursively(string sourcePath, string targetPath) {
            //Now Create all of the directories
            foreach (string dirPath in Directory.GetDirectories(sourcePath, "*", SearchOption.AllDirectories)) {
                Directory.CreateDirectory(dirPath.Replace(sourcePath, targetPath));
            }

            //Copy all the files & Replaces any files with the same name
            foreach (string newPath in Directory.GetFiles(sourcePath, "*.*", SearchOption.AllDirectories)) {
                File.Copy(newPath, newPath.Replace(sourcePath, targetPath), true);
            }
        }

        public void SortingByCreateDate(bool descending=false) {

        }

        public void SortingByLastAccessDate(bool descending = false) {

        }

        public string GetVersion() {
            System.Reflection.Assembly assembly = System.Reflection.Assembly.GetExecutingAssembly();
            FileVersionInfo fvi = FileVersionInfo.GetVersionInfo(assembly.Location);
            return fvi.FileVersion;
        }

        public string GetDLLVersion() {
            FileVersionInfo dllInfo = FileVersionInfo.GetVersionInfo(AppDomain.CurrentDomain.BaseDirectory + "\\AlligatorAlgMil.dll");
            return dllInfo.FileVersion;
        }

        public int CollectRecipe() {
            SystemHandler pSys = SystemHandler.Handle;
            string recipePath = pSys.Setting.RecipeSavePath;  //load from default path
            
            List.Clear();

            string [] recipeDirList = Directory.GetDirectories(recipePath, "*");
            foreach(string recipeDir in recipeDirList) {

                string recipeName = new DirectoryInfo(recipeDir).Name;
                
                string filePath = Path.Combine(recipeDir, FILE_RECIPE + EXT_RECIPE);
                if (File.Exists(filePath)) {
                    DateTime creationDate = File.GetCreationTime(filePath);
                    DateTime lastOpenDate = File.GetLastAccessTime(filePath);
                    
                    List.Add(new RecipeFileInfo { Name = recipeName, CreateDate = creationDate, LastOpenDate = lastOpenDate, FilePath = filePath });
                }
            }
            return List.Count;
        }

        public string GetRecipeFilePath(string name) {
            string recipeSavePath = SystemHandler.Handle.Setting.RecipeSavePath;
            //recipe > name > name.vrcp
            recipeSavePath = Path.Combine(recipeSavePath, name);
            string recipeFile = Path.Combine(recipeSavePath, FILE_RECIPE + EXT_RECIPE);
            return recipeFile;
        }
        public bool HasRecipe(string name) {
            if (List.Count(info => info.Name == name) > 0) return true;
            return false;
        }

    }
}
