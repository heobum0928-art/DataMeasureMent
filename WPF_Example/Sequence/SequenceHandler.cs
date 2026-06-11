using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using PropertyTools.DataAnnotations;
using ReringProject.Define;
using ReringProject.Network;
using ReringProject.Setting;
using ReringProject.Utility;

namespace ReringProject.Sequence {
    
    public class RecipeChangedEventArgs : EventArgs {
        public string RecipeName { get; private set; }

        public RecipeChangedEventArgs(string name) {
            RecipeName = name;
        }
    }

    public delegate void OnRecipeChangedEvent(object sender, RecipeChangedEventArgs arg);

    public sealed partial class SequenceHandler : IDisposable {
        [Browsable(false)]
        public static SequenceHandler Handle { get; } = new SequenceHandler();

        public string ModelName { get; set; }

        [ReadOnly(true)]
        public string Version {
            get {
                return SystemHandler.Handle.Recipes.GetVersion();
            }
        }
        
        [Browsable(false)]
        private readonly Dictionary<ESequence, SequenceBase> Sequences = new Dictionary<ESequence, SequenceBase>();

        [Browsable(false)]
        private SystemSetting pSetting;

        public event OnRecipeChangedEvent OnRecipeChanged;

        private SequenceHandler() {
            RegisterSequences();
            RegisterActions();
            InitializeSequences();
            SequenceBuilder.Free();

            pSetting = SystemHandler.Handle.Setting;
        }

        public void Dispose() {
            ExecOnRelease();
            for(int i = 0; i < Sequences.Count; i++) {
                SequenceBase seq = Sequences.ElementAt(i).Value;
                seq.Release();
            }
        }

        public void RegisterSequence(SequenceBuilder sb) {
            SequenceBase seq = sb.Publish();
            Sequences.Add(seq.ID, seq);
        }
        
        [Browsable(false)]
        [ReadOnly(true)]
        public int Count {
            get => Sequences.Count;
        }

        [Browsable(false)]
        [ReadOnly(true)]
        public string StateSequenceName {
            get {
                return _StateSeqName;
            }
        }

        private string _StateSeqName;
        [Browsable(false)]
        [ReadOnly(true)]
        public EContextState StateAll {
            get {
                EContextState allState = EContextState.Idle;
                _StateSeqName = "All Sequences";

                for (int i = 0; i < Sequences.Count; i++) {
                    SequenceBase seq = Sequences.ElementAt(i).Value;
                    if (seq.State == EContextState.Running) {
                        allState = EContextState.Running;
                        _StateSeqName = seq.Name;
                        return EContextState.Running;
                    }
                    else if(seq.State != EContextState.Idle) {
                        _StateSeqName = seq.Name;
                        allState = seq.State;
                    }
                }
                return allState;
            }
        }

        public bool IsIdle {
            get {
                return StateAll == EContextState.Idle;
            }
        }

        public Dictionary<string, SequenceContext> GetContextDictionary() {
            Dictionary<string, SequenceContext> dict = new Dictionary<string, SequenceContext>();
            for(int i = 0; i < Sequences.Count; i++) {
                SequenceBase seq = Sequences.ElementAt(i).Value;

                dict.Add(seq.Name, seq.Context);
            }
            return dict;
        }
        [Browsable(false)]
        [ReadOnly(true)]
        public SequenceBase this[int index] {
            get { return Sequences.ElementAtOrDefault(index).Value; }
        }
        [Browsable(false)]
        [ReadOnly(true)]
        public SequenceBase this[ESequence id] {
            get {
                if (Sequences.ContainsKey(id) == false) return null;
                return Sequences[id];
            }
        }
        [Browsable(false)]
        [ReadOnly(true)]
        public SequenceBase this[string name] {
            get {
                for(int i = 0; i < Sequences.Count; i++) {
                    SequenceBase seq = Sequences.ElementAt(i).Value;
                    if (seq.Name == name) {
                        return seq;
                    }
                }
                return null;
                //ESequence seqID = (ESequence)Enum.Parse(typeof(ESequence), name);
                //return this[seqID];
            }
        }

        public bool LoadRecipe(string name, ERecipeFileType fileType = ERecipeFileType.Ini) {
            bool result = true;
            switch (fileType) {
                case ERecipeFileType.Ini:
                    result = LoadFromIni(name);
                    break;
                case ERecipeFileType.Json:
                    result = LoadFromJson(name);
                    break;
            }
            OnRecipeChanged?.Invoke(this, new RecipeChangedEventArgs(name));

            if (result) ExecOnLoad(name);
            
            return result;
        }

        public bool SaveRecipe(string name, ERecipeFileType fileType = ERecipeFileType.Ini) {
            bool result = true;
            switch (fileType) {
                case ERecipeFileType.Ini:
                    result = SaveToIni(name);
                    break;
                case ERecipeFileType.Json:
                    result = SaveToJson(name);
                    break;
            }
            return result;
        }

        private bool LoadFromIni(string name) {
            if (string.IsNullOrEmpty(name)) return false;
            string recipeFile = SystemHandler.Handle.Recipes.GetRecipeFilePath(name);
            if (File.Exists(recipeFile) == false) return false;

            IniFile loadFile = new IniFile(recipeFile);
            ModelName = loadFile["Info"]["ModelName"].ToString();
            string Version = loadFile["Info"]["Version"].ToString();

            //version check
            if(Version != SystemHandler.Handle.Recipes.GetVersion()) {
                //not matched version
                
            }
            // 신규 SHOTS 포맷 감지 시 동적 로드
            if (TryLoadNewFormat(loadFile)) {
                pSetting.CurrentRecipeName = name;
                return true;
            }

            // 기존 Param0~N 방식
            int m = 0;
            for (int i = 0; i < Sequences.Count; i++) {
                for (int j = 0; j < this[i].ActionCount; j++) {
                    ParamBase param = this[i][j].Param;
                    param.Load(loadFile, "Param" + m.ToString());
                    m++;
                }
            }

            pSetting.CurrentRecipeName = name;

            return true;
        }


        private bool SaveToIni(string name) {
            if (name != null) ModelName = name;
            string recipeFile = SystemHandler.Handle.Recipes.GetRecipeFilePath(ModelName);

            string recipeDir = Path.GetDirectoryName(recipeFile);
            if (Directory.Exists(recipeDir) == false) {
                Directory.CreateDirectory(recipeDir);
            }

            //260611 hbk 덮어쓰기 전 기존 레시피를 읽어 둔다 — 현재 CameraRole 에 비활성인 시퀀스의
            //  FIXTURE Datum 을 보존하기 위함 (Side↔TopBottom 전환 시 타 시퀀스 Datum 소실 버그 수정)
            IniFile existingFile = File.Exists(recipeFile) ? new IniFile(recipeFile) : null;

            IniFile saveFile = new IniFile();
            saveFile["Info"]["ModelName"] = ModelName;
            saveFile["Info"]["Version"] = Version;

            // 동적 FAI 모드면 신규 포맷으로 저장
            if (IsDynamicFAIMode) {
                SaveNewFormat(saveFile, existingFile); //260611 hbk existingFile 전달
            }

            // 기존 Param0~N 방식도 항상 저장 (하위 호환)
            int m = 0;
            for (int i = 0; i < Sequences.Count; i++) {
                for (int j = 0; j < this[i].ActionCount; j++) {
                    ParamBase param = this[i][j].Param;
                    param.Save(saveFile, "Param" + m.ToString());
                    m++;
                }
            }

            saveFile.Save(recipeFile);
            return true;
        }

        private bool LoadFromJson(string name) {
            try {
                string recipeFile = SystemHandler.Handle.Recipes.GetRecipeFilePath(name);
                if (File.Exists(recipeFile) == false) return false;

                using (StreamReader loadFile = File.OpenText(recipeFile)) {
                    string json = loadFile.ReadLine();
                    JsonConvert.PopulateObject(json, this);

                    for (int i = 0; i < Sequences.Count; i++) {
                        for (int j = 0; j < this[i].ActionCount; j++) {
                            ParamBase param = this[i][j].Param;
                            json = loadFile.ReadLine();
                            JsonConvert.PopulateObject(json, param);
                        }
                    }
                }

                pSetting.CurrentRecipeName = name;
            }
            catch (Exception e) {
                Logging.PrintErrLog((int)ELogType.Error, string.Format("Exception ReturnCode:{0}", "LoadFromJson Exception", e.ToString()));
                return false;
            }
            return true;
        }

        private bool SaveToJson(string name = null) {
            if (name != null) ModelName = name;
            try {
                string recipeFile = SystemHandler.Handle.Recipes.GetRecipeFilePath(ModelName);
                string recipeDir = Path.GetDirectoryName(recipeFile);
                if (Directory.Exists(recipeDir) == false) {
                    Directory.CreateDirectory(recipeDir);
                }

                string json = JsonConvert.SerializeObject(this);
                StreamWriter saveFile = File.CreateText(recipeFile);
                saveFile.Write(json);
                saveFile.WriteLine();

                //mmf or ect files
                for (int i = 0; i < Sequences.Count; i++) {
                    for (int j = 0; j < this[i].ActionCount; j++) {
                        ParamBase param = this[i][j].Param;
                        json = JsonConvert.SerializeObject(param);
                        saveFile.Write(json);
                        saveFile.WriteLine();
                    }
                }
                saveFile.Flush();
                saveFile.Close();
            }
            catch (Exception e) {
                Logging.PrintErrLog((int)ELogType.Error, string.Format("Exception ReturnCode:{0}", "SaveToJson Exception", e.ToString()));
                return false;
            }
            return true;
        }

        public void ExecOnRelease() {
            for (int i = 0; i < Sequences.Count; i++) {
                SequenceBase seq = this[i];
                seq.OnRelease();
            }
        }

        public void ExecOnCreate() {
            for(int i = 0; i < Sequences.Count; i++) {
                SequenceBase seq = this[i];
                seq.OnCreate();
            }
        }

        public void ExecOnLoad(string name) {
            for(int i = 0; i < Sequences.Count; i++) {
                SequenceBase seq = this[i];
                seq.OnLoad();
            }
        }
        

        public bool Start(TestPacket packet) {
            if (packet == null) return false;
            if ((ETestType)packet.TestType == ETestType.Calibration) {
                Logging.PrintLog((int)ELogType.Trace, "Calibration test requests are blocked from automatic sequence execution.");
                return false;
            }
            string seqName = packet.Identifier;
            SequenceBase seq = this[seqName];
            if (seq == null) return false;
            return seq.Start(packet);
        }

        public bool Start(ESequence seqID, EAction beginActionID) {
            if (Sequences.ContainsKey(seqID) == false) return false;
            return Sequences[seqID].Start(beginActionID);
        }

        public bool Stop(ESequence id) {
            if (Sequences.ContainsKey(id) == false) return false;
            return Sequences[id].Stop();
        }

        public bool Pause(ESequence id) {
            if (Sequences.ContainsKey(id) == false) return false;
            return Sequences[id].Pause();
        }

        public EContextState GetSequenceState(ESequence id) {
            if (Sequences.ContainsKey(id) == false) return EContextState.Idle;
            return Sequences[id].State;
        }

        public EContextState GetSequenceState(string name) {
            SequenceBase seq = this[name];
            if (seq == null) return EContextState.Idle;
            return seq.State;
        }
        
    }
}
