
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PropertyTools.DataAnnotations;
using ReringProject.UI;
using ReringProject.Utility;
using Newtonsoft.Json;
using System.Reflection;
using ReringProject.Device;
using ReringProject.Properties;

namespace ReringProject.Setting {
    
    public enum ELogType : int {
        Trace = 0,
        Camera = 1,
        LightController = 2,
        TcpConnection = 3,
        Result = 4,
        Image = 5,
        Error = 6,
    }
    
    public partial class SystemSetting {
        public static SystemSetting Handle { get; } = new SystemSetting();
        //setting
        private string SettingIniFile { get; set; } = AppDomain.CurrentDomain.BaseDirectory + @"Setting.ini";
        private string SettingJsonFile { get; set; } = AppDomain.CurrentDomain.BaseDirectory + @"Setting.json";

        //server
        //260623 hbk Phase 49: v2.6 레거시 포트 — UI 숨김(혼란 방지). v1.0 전환 완료로 ServerPortV1 만 노출.
        //  INI 직렬화는 유지(v2.6 폴백 시 값 보존). UseProtocolV1=false 일 때만 사용되는 포트.
        [Browsable(false)]
        [Category("Connection|Server")]
        public int ServerPort { get; set; } = 2505;

        //recipe
        [Category("Path|Recipe")]
        [DirectoryPath]
        [AutoUpdateText]
        public string RecipeSavePath { get; set; } = @"D:\Data\Recipe";

        [AutoUpdateText]
        public string CurrentRecipeName { get; set; } = "A";

        //calibration
        [Category("Path|Calibration")]
        [DirectoryPath]
        [AutoUpdateText]
        public string CalibrationSavePath { get; set; } = AppDomain.CurrentDomain.BaseDirectory + @"Calibration";

        //log
        [Category("Path|Log")]
        [DirectoryPath]
        [AutoUpdateText]
        public string TraceLogSavePath { get; set; } = AppDomain.CurrentDomain.BaseDirectory + @"Trace";
        [DirectoryPath]
        [AutoUpdateText]
        public string ImageSavePath { get; set; } = AppDomain.CurrentDomain.BaseDirectory + @"Image";
        [DirectoryPath]
        [AutoUpdateText]
        public string ResultSavePath { get; set; } = AppDomain.CurrentDomain.BaseDirectory + @"Result";

        [DirectoryPath]
        [AutoUpdateText]
        public string ErrorSavePath { get; set; } = AppDomain.CurrentDomain.BaseDirectory + @"Error";

        [DirectoryPath]
        [AutoUpdateText]
        public string CameraLogSavePath { get; set; } = AppDomain.CurrentDomain.BaseDirectory + @"Camera";

        [DirectoryPath]
        [AutoUpdateText]
        public string LightControllerPath { get; set; } = AppDomain.CurrentDomain.BaseDirectory + @"LightController";

        //light config (light.ini — 물리 채널 배선/포트 설정 파일 위치). 다른 D:\Data\* 데이터 경로와 동일하게
        //  bin 폴더 밖으로 분리 — light.ini 는 로그가 아니라 Recipe/Calibration 과 같은 영속 설정이라 별도 그룹.
        [Category("Path|Light")]
        [DirectoryPath]
        [AutoUpdateText]
        public string LightConfigPath { get; set; } = @"D:\Data\Light";

        [DirectoryPath]
        [AutoUpdateText]
        public string TcpConnectionPath { get; set; } = AppDomain.CurrentDomain.BaseDirectory + @"TcpConnection";

        public int LogDeleteDay { get; set; } = 30;

        //data statistics                                              //260707 hbk STAT-01 D-01: 양산 이력 통계 CSV 저장 경로
        [Category("Path|Statistics")]                                   //260707 hbk STAT-01 D-01: 자체 그룹(뒤 MapData 가 리셋 → 그룹 누출 0)
        [DirectoryPath]                                                 //260707 hbk STAT-01 D-01
        [AutoUpdateText]                                                //260707 hbk STAT-01 D-01
        public string StatisticsSavePath { get; set; } = AppDomain.CurrentDomain.BaseDirectory + @"Statistics";   //260707 hbk STAT-01 D-01

        //data path
        [Category("Path|MapData")]
        [DirectoryPath]
        [AutoUpdateText]
        public string MapDataLoadPath { get; set; } = AppDomain.CurrentDomain.BaseDirectory + @"Load";
        [DirectoryPath]
        [AutoUpdateText]
        public string MapDataSavePath { get; set; } = AppDomain.CurrentDomain.BaseDirectory + @"Save";


        //260622 hbk Phase 48
        // PROTO-01: v1.0 프로토콜 활성화 플래그 (D-06 v2.6/v1.0 공존). 기본 false → 구 INI 0 로드돼도 v2.6 유지.
        [Category("Connection|Protocol")]
        public bool UseProtocolV1 { get; set; } = false;

        //260722 hbk Phase 68 GAP-3(68-10, 지침 #7): 크로스-Z Datum(2위치, 완성 index>=1) 실패 시 완성 index 에서
        // 즉시 F 를 보낼지 게이팅. 기본 false — Vision-Protocol-v1.0.md 는 Datum(Idx0) 단일위치만 명시하므로
        // z>=1 F 를 PLC 가 올바르게 해석하는지 제어팀 합의 전까지 OFF. bool 기본 false → INI 누락 시 자동 OFF
        // (Load 오버라이드 불필요, UseProtocolV1 과 동일 계약).
        [Category("Connection|Protocol")]
        public bool EnableCrossZDatumImmediateFail { get; set; } = false;

        //260622 hbk Phase 48
        // PROTO-01: PC 역할 (D-03 빌드 상수 대신 설정 지정). 1=PC1(TOP/BOTTOM), 2=PC2(SIDE_1/SIDE_2).
        [Category("Connection|Server")]
        public int PcRole { get; set; } = 1;

        //260622 hbk Phase 48
        // PROTO-01: v1.0 전용 포트 (엑셀 규격 7701). ServerPort(2505) 는 v2.6 호환 유지 — v1.0 포트는 별도 속성으로 분리.
        [Category("Connection|Server")]
        public int ServerPortV1 { get; set; } = 7701;

        //config

        [Category("System|Enviroment")]
        public int TestTimeOut { get; set; } = 2000;

        public bool AutoLogoutWhenRecvTest { get; set; } = true;

        public bool SaveFailImage { get; set; } = false;

        // 수동/오프라인 검사 모드. ON 이면 실 카메라 빌드에서도 라이브 grab 대신 노드별 저장 이미지로 검사한다.
        //  (Z 모터 없는 메뉴얼 지그: 사람이 datum/shot Z 를 맞춰 미리 이미지를 확보 후 그 이미지로 검사.)
        //  누락 INI 키 → false 로드(ToBool 기본) 이므로 기존 레시피는 자동으로 라이브 grab 유지.
        [Category("System|Enviroment")]
        public bool OfflineInspectMode { get; set; } = false;


        [Category("System|Localize")]
        [ItemsSourceProperty("LanguageList")]
        public string Language {
            get {
                return _Language;
            }
            set {
                if (value != _Language) {
                    _Language = value;
                    LocalizationResource localRes = App.Current.Resources["DR"] as LocalizationResource;
                    localRes.ChangeLanguage(_Language);
                }
                
            }
        }
        private string _Language = Properties.LocalizationResource.LanguageCodes[0];

        [Browsable(false)]
        public string[] LanguageList { get; } = Properties.LocalizationResource.LanguageCodes;
        
        
        private SystemSetting() {
            Load();
        }

        public string GetCameraImageSavePath(string camName) {
            string filePath = GetLogSavePath(ELogType.Image, DateTime.Now.ToShortDateString());
            if (Directory.Exists(filePath) == false) {
                Directory.CreateDirectory(filePath);
            }
            filePath += string.Format(@"\{0}_{1}{2}", camName, DateTime.Now.ToString("hhmmssff"), DeviceHandler.EXTENSION_SAVE_IMAGE);
            return filePath;
        }

        public string GetResultImageSavePath(string seqName, string actionName) {
            string filePath = SystemHandler.Handle.Setting.GetLogSavePath(ELogType.Image, DateTime.Now.ToShortDateString());
            if (Directory.Exists(filePath) == false) {
                Directory.CreateDirectory(filePath);
            }
            filePath += string.Format(@"\{0}_{1}_{2}{3}", seqName, actionName, DateTime.Now.ToString("hhmmssff"), DeviceHandler.EXTENSION_SAVE_IMAGE);
            return filePath;
        }

        public string GetLogSavePath(ELogType type, params string [] subDirs) {
            string basePath = AppDomain.CurrentDomain.BaseDirectory;
            switch (type) {
                case ELogType.Trace:
                    basePath = TraceLogSavePath;
                    break;
                case ELogType.Camera:
                    basePath = CameraLogSavePath;
                    break;
                case ELogType.Result:
                    basePath = ResultSavePath;
                    break;
                case ELogType.Image:
                    basePath = ImageSavePath;
                    break;
                case ELogType.Error:
                    basePath = ErrorSavePath;
                    break;
                case ELogType.LightController:
                    basePath = LightControllerPath;
                    break;
                case ELogType.TcpConnection:
                    basePath = TcpConnectionPath;
                    break;
            }

            string finalPath = basePath;
            if (subDirs != null) {
                finalPath = Path.Combine(basePath, Path.Combine(subDirs));
            }
            return finalPath;
        }
        
        public void Load() {
            IniFile loadFile = new IniFile();
            if (File.Exists(SettingIniFile)) {
                loadFile.Load(SettingIniFile);
            }
            string group = "Default";
            bool isDirectory = false;
            PropertyInfo[] props = GetType().GetProperties();
            foreach (var prop in props) {
                string name = prop.Name;
                string type = prop.PropertyType.Name;

                //set group Name 
                Attribute attr = prop.GetCustomAttribute(typeof(CategoryAttribute));
                if (attr != null) {
                    CategoryAttribute catAttr = attr as CategoryAttribute;
                    group = catAttr.Category;
                }
                //directory check
                attr = prop.GetCustomAttribute(typeof(DirectoryPathAttribute));
                if (attr != null) {
                    isDirectory = true;
                }
                else {
                    isDirectory = false;
                }
                try {
                    switch (type) {
                        case "Int32":
                            int iValue = loadFile[group][name].ToInt();
                            prop.SetValue(this, iValue);
                            break;
                        case "Double":
                            double dValue = loadFile[group][name].ToDouble();
                            prop.SetValue(this, dValue);
                            break;
                        case "String":
                            string sValue = loadFile[group][name].ToString();
                            if(isDirectory) {
                                if (Directory.Exists(sValue) == false) Directory.CreateDirectory(sValue);
                            }
                            prop.SetValue(this, sValue);
                            break;
                        case "Boolean":
                            bool bValue = loadFile[group][name].ToBool();
                            prop.SetValue(this, bValue);
                            break;
                        case "Rect":
                            System.Windows.Rect rectVal = loadFile[group][name].ToRect();
                            prop.SetValue(this, rectVal);
                            break;
                        case "Line":
                            Line lineVal = loadFile[group][name].ToLine();
                            prop.SetValue(this, lineVal);
                            break;
                        case "Circle":
                            Circle circleVal = loadFile[group][name].ToCircle();
                            prop.SetValue(this, circleVal);
                            break;
                        case "PropertyItem[]":
                            Sequence.PropertyItem[] propItems = (Sequence.PropertyItem[])prop.GetValue(this);
                            for (int i = 0; i < propItems.Length; i++) {
                                propItems[i].Value = loadFile[group][propItems[i].Name].ToDouble();
                            }
                            break;
                        case "ModelFinderViewModel":
                            ModelFinderViewModel modelView = (ModelFinderViewModel)prop.GetValue(this);
                            modelView.Load(loadFile, group, prop.Name);
                            break;
                    }
                }
                catch (Exception e) {
                    Logging.PrintErrLog((int)ELogType.Error, e.Message);
                }
            }
            //260622 hbk Phase 48
            // PROTO-01: INI 로드 후 Custom partial 가드 호출 — PcRole 등 기본값≠0 항목 복원.
            AfterLoad();
        }

        //260622 hbk Phase 48
        // PROTO-01: Load 후처리 partial 후크 — Custom/SystemSetting.cs 에서 구현.
        partial void AfterLoad();

        public void Save() {
            IniFile saveFile = new IniFile();
            string group = "Default";
            PropertyInfo[] props = GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public);
            foreach (var prop in props) {
                string name = prop.Name;
                string type = prop.PropertyType.Name;
                
                //set group Name 
                Attribute attr = prop.GetCustomAttribute(typeof(CategoryAttribute));
                if(attr != null) {
                    CategoryAttribute catAttr = attr as CategoryAttribute;
                    group = catAttr.Category;
                }

                switch (type) {
                    case "Int32":
                        saveFile[group][name] = (int)prop.GetValue(this);
                        break;
                    case "Double":
                        saveFile[group][name] = (double)prop.GetValue(this);
                        break;
                    case "String":
                        saveFile[group][name] = (string)prop.GetValue(this);
                        break;
                    case "Boolean":
                        saveFile[group][name] = (bool)prop.GetValue(this);
                        break;
                    case "Rect":
                        saveFile[group][name] = (System.Windows.Rect)prop.GetValue(this);
                        break;
                    case "Line":
                        saveFile[group][name] = (Line)prop.GetValue(this);
                        break;
                    case "Circle":
                        saveFile[group][name] = (Circle)prop.GetValue(this);
                        break;
                    case "PropertyItem[]":
                        Sequence.PropertyItem[] propItems = (Sequence.PropertyItem[])prop.GetValue(this);
                        for (int i = 0; i < propItems.Length; i++) {
                            saveFile[group][propItems[i].Name] = propItems[i].Value;
                        }
                        break;
                    case "ModelFinderViewModel":
                        ModelFinderViewModel modelView = (ModelFinderViewModel)prop.GetValue(this);
                        modelView.Save(saveFile, group, prop.Name);
                        break;
                    default:
                        break;
                }
            }
            saveFile.Save(SettingIniFile);
        }
        /*
        public void SaveToJson() {
            string json = JsonConvert.SerializeObject(this);
            StreamWriter saveFile = File.CreateText(SettingJsonFile);
            saveFile.Write(json);
            saveFile.Flush();
            saveFile.Close();
        }

        public void LoadFromJson() {
            if (File.Exists(SettingJsonFile) == false) return;
            
            StreamReader loadFile = File.OpenText(SettingJsonFile);
            string json = loadFile.ReadToEnd();
            JsonConvert.PopulateObject(json, this);
            loadFile.Close();
        }
        */
    }
}



