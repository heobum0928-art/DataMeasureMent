
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ReringProject.Sequence;
using ReringProject.Utility;
using ReringProject.Device;
using ReringProject.Setting;

namespace ReringProject.Device {
    
    public enum ELightErrorType {
        OpenFail,
        Disconnected,
        ReadFail,
        WriteFail,
    }

    public class LightFailEventArgs : EventArgs {

        public ELightErrorType ErrorType { get; private set; }

        public string Name { get; private set; }
        public int Index { get; private set; }

        public int Channel { get; private set; }

        public LightFailEventArgs(ELightErrorType type, int devIndex, int channel, string name) {
            ErrorType = type;
            Index = devIndex;
            Channel = channel;
            Name = name;
        }
    }

    public delegate void LightFailEvent(LightFailEventArgs args);
    
    
    public sealed partial class LightHandler {
        private struct LightCommandData {
            public bool IsReadState;
            public bool IsWriteState;
            public bool WriteState;

            public bool IsReadValue;
            public bool IsWriteValue;
            public int WriteValue;
        }

        public static LightHandler Handle { get; } = new LightHandler();

        public const int CHANNEL_LIMIT = 8; //260625 hbk Phase 64 LIGHT-01: 1 controller 당 채널 갯수 (JPF-1208 8CH 대응)
        public const int FAIL_LIMIT = 3;

        public const int TIMEOUT_READ = 2000;

        public List<VirtualLightController> Controllers { get; private set; } = new List<VirtualLightController>();

        private Thread mThread;
        private bool IsTerminated = false;
        
        private LightCommandData[,] CmdTable;
        private int[] FailControllerTable;

        //group
        public List<LightGroup> Groups { get; private set; } = new List<LightGroup>();

        //event
        public event LightFailEvent OnError;

        private LightHandler() {
        }

        public bool Initialize() {
            RegisterLightController();
            
            CmdTable = new LightCommandData[Controllers.Count, CHANNEL_LIMIT];
            for (int i = 0; i < Controllers.Count; i++) {
                for (int j = 0; j < CHANNEL_LIMIT; j++) {
                    CmdTable[i, j] = new LightCommandData();
                }
            }
            FailControllerTable = new int[Controllers.Count];

            Load();
            bool openResult = OpenAll();

            mThread = new Thread(Execute);
            mThread.Name = "LightHandler";
            mThread.Priority = ThreadPriority.Lowest;
            mThread.Start();

            return openResult;
        }
        
        public void Release() {
            CloseAll();
            IsTerminated = true;
            if(mThread != null) {
                mThread.Join(1000);
                mThread = null;
            }
        }

        public LightGroup this[int i] {
            get {
                return Groups[i];
            }
        }

        public LightGroup this[string groupName] {
            get {
                for(int i = 0; i < Groups.Count; i++) {
                    if (Groups[i].Name == groupName) return Groups[i];
                }
                return null;
            }
        }

        public bool ApplyLight(ICameraParam param, bool bOn = false) {
            if (bOn) {
                if (!SetOnOff(param.LightGroupName, true)) return false;
            }
            return SetLevel(param.LightGroupName, param.LightLevel);
        }

        public bool ApplyLight(CameraMasterParam param, bool bOn = false) {
            if (bOn) {
                if (!SetOnOff(param.LightGroupName, true)) return false;
            }
            return true;
        }
        

        public bool OpenAll() {
            bool state = true;
            foreach (VirtualLightController con in Controllers)
            {
                if (con.Open() == false) state = false;
            }
            return state;
        }

        public void CloseAll() {
            foreach(VirtualLightController con in Controllers) {
                con.Close();
            }
        }

        public LightGroup GetGroup(string groupName) {
            foreach(LightGroup group in Groups) {
                if (group.Name == groupName) return group;
            }
            return null;
        }

        public bool GetOnOff(string groupName) {
            LightGroup group = GetGroup(groupName);
            if (group == null) return false;
            bool onOff = true;
            for (int i = 0; i < group.Count; i++) {
                LightGroupItem item = group[i];
                bool singleState = GetOnOff(item.Index, item.Channel);
                if (singleState == false) onOff = false;
            }

            return onOff;
        }

        public bool SetOnOff(string groupName, bool onOff) {
            LightGroup group = GetGroup(groupName);
            if (group == null) return false;

            for (int i = 0; i< group.Count; i++) {
                LightGroupItem item = group[i];
                SetOnOff(item.Index, item.Channel, onOff);
            }

            Logging.PrintLog((int)ELogType.LightController, "{0} - Set On : {1}", groupName, onOff);
            return true;
        }

        public bool SetLevel(string groupName, int level) {
            LightGroup group = GetGroup(groupName);
            if (group == null) return false;

            for (int i = 0; i < group.Count; i++) {
                LightGroupItem item = group[i];
                SetLevel(item.Index, item.Channel, level);
            }
            Logging.PrintLog((int)ELogType.LightController, "{0} - Set Level : {1}", groupName, level);
            return true;
        }

        public int GetLevelMin(int index) {
            return Controllers[index].MinLevel;
        }

        public int GetLevelMax(int index) {
            return Controllers[index].MaxLevel;
        }

        public int GetLevelMin(string groupName) {
            int singleMin = 0;
            int minOfMin = 9999;
            LightGroup group = GetGroup(groupName);
            if (group == null) return 0;

            for (int i = 0; i < group.Count; i++) {
                LightGroupItem item = group[i];
                singleMin = GetLevelMin(item.Index);
                if (singleMin < minOfMin) minOfMin = singleMin;
            }
            return minOfMin;
        }

        public int GetLevelMax(string groupName) {
            int singleMax = 0;
            int maxOfMax = 0;
            LightGroup group = GetGroup(groupName);
            if (group == null) return 0;

            for (int i = 0; i < group.Count; i++) {
                LightGroupItem item = group[i];
                singleMax = GetLevelMax(item.Index);
                if (singleMax > maxOfMax) maxOfMax = singleMax;
            }
            return maxOfMax;
        }

        public int GetLevel(string groupName) {
            int singleLevel = 0;
            LightGroup group = GetGroup(groupName);
            if (group == null) return 0;

            for (int i = 0; i < group.Count; i++) {
                LightGroupItem item = group[i];
                singleLevel = GetLevel(item.Index, item.Channel);
                if (singleLevel > 0) break;
            }
            return singleLevel;
        }

        public bool IsSameLevel(string groupName, int compareLevel) {
            LightGroup group = GetGroup(groupName);
            if (group == null) return false;

            for (int i = 0; i < group.Count; i++) {
                LightGroupItem item = group[i];
                int value = GetLevel(item.Index, item.Channel);
                if (value != compareLevel) return false;
            }
            return true;
        }

        public bool GetOnOff(int index, int channel) {
            if (index >= Controllers.Count) return false;
            return Controllers[index].GetOnOff(channel);
        }

        public void SetReadOnOff(int index, int channel) {
            if (index >= Controllers.Count) return;
            CmdTable[index, channel].IsReadState = true;
        }

        public void SetOnOff(int index, int channel, bool on) {
            if (index >= Controllers.Count) return;
            CmdTable[index, channel].IsWriteState = true;
            CmdTable[index, channel].WriteState = on;
        }

        public async Task<bool> ReadOnOffAsync(int index, int channel) {
            CmdTable[index, channel].IsReadState = true;

            //비동기로 while문을 looping (reading 성공할 때 까지)
            Task t = new Task(() => {
                Stopwatch stopWatch = new Stopwatch();
                stopWatch.Start();
                while (CmdTable[index, channel].IsReadState) {
                    if (stopWatch.ElapsedMilliseconds >= TIMEOUT_READ) {
                        return;
                    }
                }
            });

            await t;
            
            return Controllers[index].GetOnOff(channel);
        }

        public void SetLevel(int index, int channel, int level) {
            if (index >= Controllers.Count) return;
            CmdTable[index, channel].IsWriteValue = true;
            CmdTable[index, channel].WriteValue = level;
        }

        /// <summary>
        /// 내부 버퍼에 저장된 각 채널 별 level값을 반환.
        /// </summary>
        /// <param name="index"></param>
        /// <param name="channel"></param>
        /// <param name="level"></param>
        /// <returns></returns>
        public int GetLevel(int index, int channel) {
            if (index >= Controllers.Count) return 0;
            return Controllers[index].GetLevel(channel);
        }

        /// <summary>
        /// 장치 index 및 channel의 level를 reading하도록 명령한다.
        /// </summary>
        /// <param name="index"></param>
        /// <param name="channel"></param>
        /// <param name="continousReading"></param>
        /// <returns></returns>
        public bool SetReadLevel(int index, int channel, bool continousReading=false) {
            if (index >= Controllers.Count) return false;
            if (channel >= Controllers[index].MaxChannel) return false;

            CmdTable[index, channel].IsReadValue = true;
            return true;
        }
        
        public async Task<int> ReadLevelAsync(int index, int channel) {
            CmdTable[index, channel].IsReadValue = true;

            //비동기로 while문을 looping (reading 성공할 때 까지)
            Task t = new Task(() => {
                Stopwatch stopWatch = new Stopwatch();
                stopWatch.Start();
                while (CmdTable[index, channel].IsReadValue) {
                    if(stopWatch.ElapsedMilliseconds >= TIMEOUT_READ) {
                        return;
                    }
                }
            });

            await t;

            return Controllers[index].GetLevel(channel);
        }

        public int ControllerCount { get => Controllers.Count; }
        

        public bool Load() {
            string loadPath = AppDomain.CurrentDomain.BaseDirectory + @"light.ini";
            if (File.Exists(loadPath) == false) return false;

            IniFile loadFile = new IniFile();
            loadFile.Load(loadPath);

            for(int i = 0; i < Controllers.Count; i++) {
                string groupName = "Controller" + i.ToString();
                Controllers[i].Port = loadFile[groupName]["Port"].ToInt();
                Controllers[i].Baudrate = loadFile[groupName]["Baudrate"].ToInt();
                
            }
            
            return true;
        }

        public bool Save() {
            IniFile saveFile = new IniFile();
            string savePath = AppDomain.CurrentDomain.BaseDirectory + @"light.ini";

            for (int i = 0; i < Controllers.Count; i++) {
                string groupName = "Controller" + i.ToString();
                saveFile[groupName]["Port"] = Controllers[i].Port;
                saveFile[groupName]["Baudrate"] = Controllers[i].Baudrate;
            }

            saveFile.Save(savePath);
            return true;
        }

        private void Execute() {
            while (!IsTerminated) {
                for(int i = 0; i < Controllers.Count; i++) {
                    if (Controllers[i].IsOpen == false) continue;

                    for (int j = 0; j < CHANNEL_LIMIT; j++) {
                        if (j >= Controllers[i].MaxChannel) continue; //최대 채널을 넘지 않도록 한다.
                        //read OnOff
                        if (CmdTable[i,j].IsReadState == true) {
                            Thread.Sleep(2);
                            if (Controllers[i].ReadOnOff(j) == false) {
                                FailControllerTable[i]++;
                                if (FailControllerTable[i] > FAIL_LIMIT) {
                                    if (OnError != null) OnError(new LightFailEventArgs(ELightErrorType.ReadFail, i, j, Controllers[i][j].Name));
                                    FailControllerTable[i] = 0;
                                }
                            }
                            else {
                                CmdTable[i, j].IsReadState = false;
                            }
                        }

                        //read Level
                        if (CmdTable[i, j].IsReadValue == true) {
                            Thread.Sleep(2);

                            if (Controllers[i].ReadLevel(j) == false) {
                                FailControllerTable[i]++;
                                if (FailControllerTable[i] > FAIL_LIMIT) {
                                    if (OnError != null) OnError(new LightFailEventArgs(ELightErrorType.ReadFail, i, j, Controllers[i][j].Name));
                                    FailControllerTable[i] = 0;
                                }
                            }
                            else {
                                CmdTable[i, j].IsReadValue = false;
                            }
                        }

                        //Write onOff
                        if (CmdTable[i, j].IsWriteState) {
                            Thread.Sleep(2);

                            if (Controllers[i].WriteOnOff(j, CmdTable[i, j].WriteState) == false) {
                                FailControllerTable[i]++;
                                if (FailControllerTable[i] > FAIL_LIMIT) {
                                    if (OnError != null) OnError(new LightFailEventArgs(ELightErrorType.WriteFail, i, j, Controllers[i][j].Name));
                                    FailControllerTable[i] = 0;
                                }
                            }
                            else {
                                CmdTable[i, j].IsWriteState = false;
                                CmdTable[i, j].IsWriteValue = true;
                                //write에 성공하면 이후에 1회 read한다.
                                //CmdTable[i, j].IsReadState = true;
                            }
                        }

                        //Write level
                        if (CmdTable[i, j].IsWriteValue) {
                            Thread.Sleep(2);

                            if (Controllers[i].WriteLevel(j, CmdTable[i, j].WriteValue) == false) {
                                FailControllerTable[i]++;
                                if (FailControllerTable[i] > FAIL_LIMIT) {
                                    if (OnError != null) OnError(new LightFailEventArgs(ELightErrorType.WriteFail, i, j, Controllers[i][j].Name));
                                    FailControllerTable[i] = 0;
                                }
                            }
                            else {
                                CmdTable[i, j].IsWriteValue = false;

                                //write에 성공하면 이후에 1회 read한다.
                                //CmdTable[i, j].IsReadValue = true;
                            }
                        }
                    }
                }
                Thread.Sleep(1);
            }
        }
    }
}
