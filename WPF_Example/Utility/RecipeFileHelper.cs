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

        //260618 hbk Phase 54 ALIGN-01 패턴 모델 경로 이름 기반 재계산 (D-07/D-07a/D-07b) — 절대경로 저장 안 함
        /// <summary>
        /// HALCON shape/ncc 패턴 모델 파일 경로를 이름 기반으로 재계산한다.
        /// engine = "NCC" → .ncm, 그 외(Shape) → .shm.
        /// 레시피 폴더(RecipeSavePath/recipe/seq/act) 하위 저장이므로 Copy/Delete 시 자동 동반된다.
        /// DatumConfig 에 절대경로를 저장하지 않는다(D-07).
        /// </summary>
        public string GetPatternModelFilePath(string recipeName, string seqName, string actName, string propertyName, string engine)
        {
            string ext;
            if (string.Equals(engine, "NCC", System.StringComparison.OrdinalIgnoreCase))
                ext = DeviceHandler.EXTENSION_NCC_MODEL;
            else
                ext = DeviceHandler.EXTENSION_SHAPE_MODEL;
            string saveFile = Path.Combine(SystemHandler.Handle.Setting.RecipeSavePath, recipeName, seqName, actName);
            saveFile += propertyName + ext;
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

            //260716 hbk 복사된 레시피의 '구 레시피명이 박힌' 오프라인 검사이미지 경로를 비운다.
            //  배경: 검사Grab 저장 경로는 <ImageSavePath>\OfflineInspect\<레시피명>\... 로 레시피명이 절대경로에 박히는데,
            //  이 경로 문자열은 레시피 INI 에 그대로 직렬화된다. 레시피 폴더만 복사하면 신규 레시피가 여전히 구 레시피
            //  이미지를 가리키고, 그 파일이 실제로 존재하므로 File.Exists 를 통과해 OfflineInspectMode 검사가
            //  '구 물건 이미지'로 조용히 수행된다(로그도 없음). 경로를 비워두면 신규 레시피는 '이미지 없음' → 명시적
            //  NG/로그로 드러나고 운영자가 검사Grab 을 다시 하도록 강제된다.
            //  ※ OfflineInspect 규약 경로만 대상 — 사용자가 수동 지정한 외부 경로(예: C:\Info\Doc\...)는 레시피명과
            //    무관하므로 복사해도 유효하다. 그것까지 지우면 오히려 멀쩡한 설정을 날리므로 건드리지 않는다.
            ClearCopiedOfflineImagePaths(newDirPath, prevName);

            return true;
        }

        //260716 hbk 복사본 INI 의 구-레시피 OfflineInspect 이미지 경로 제거. 실패해도 복사 자체는 성공으로 유지(로그만).
        private static void ClearCopiedOfflineImagePaths(string newDirPath, string prevName) {
            try {
                // 구 레시피의 오프라인 이미지 폴더 절대경로 — 이 경로로 시작하는 값만 초기화 대상.
                string prevOfflineDir = Path.Combine(
                    Path.Combine(SystemHandler.Handle.Setting.ImageSavePath, "OfflineInspect"), prevName);

                string[] iniFiles = Directory.GetFiles(newDirPath, "*.ini", SearchOption.AllDirectories);
                foreach (string iniPath in iniFiles) {
                    IniFile ini = new IniFile();
                    ini.Load(iniPath);
                    bool changed = false;
                    foreach (var sectionPair in ini) {
                        IniSection section = sectionPair.Value;
                        if (section == null) continue;
                        foreach (string key in section.Keys.ToList()) {
                            if (key != "SimulImagePath" && key != "TeachingImagePath" && key != "TeachingImagePath_Vertical") continue;
                            string val = section[key].ToString();
                            if (string.IsNullOrEmpty(val)) continue;
                            // 구 레시피 OfflineInspect 폴더를 가리키는 경로만 비운다(외부 수동 경로는 보존).
                            if (val.StartsWith(prevOfflineDir, StringComparison.OrdinalIgnoreCase)) {
                                section[key] = "";
                                changed = true;
                            }
                        }
                    }
                    if (changed) {
                        ini.Save(iniPath);
                        Logging.PrintLog((int)ELogType.Trace, "[Recipe] 복사본 '{0}' 의 구-레시피 오프라인 이미지 경로 초기화 — 신규 레시피는 검사Grab 재실행 필요", iniPath);
                    }
                }
            }
            catch (Exception ex) {
                Logging.PrintErrLog((int)ELogType.Error, "[Recipe] 복사본 오프라인 이미지 경로 초기화 실패(복사 자체는 완료): " + ex.Message);
            }
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
            //260710 hbk 버전 단일 소스(VersionDefine.VERSION) 직접 반환으로 교체
            return ReringProject.VersionDefine.VERSION;
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
