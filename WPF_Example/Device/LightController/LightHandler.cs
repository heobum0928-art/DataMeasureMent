
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
            if (group == null) {
                //260716 hbk 무음 실패 경로 로그화 — 그룹 자체가 없으면 조명이 안 켜져도 아무 신호가 없었다.
                Logging.PrintLog((int)ELogType.Error, "[Light] 그룹 '{0}' 없음 — SetOnOff 무동작 (light.ini 그룹/채널명 확인)", groupName);
                return false;
            }
            //260716 hbk 그룹은 존재하나 비어있음(RebindChannels 가 이름 못 찾은 아이템을 제거) — 기존엔 for 가 0회 돌고
            //  '성공' 로그까지 남겨 켜진 것처럼 보였다(가장 기만적인 경로). 재배선 오타/누락을 여기서 드러낸다.
            if (group.Count == 0) {
                Logging.PrintLog((int)ELogType.Error, "[Light] 그룹 '{0}' 이 비어있음(채널 매핑 소실) — SetOnOff 무동작. light.ini ChannelNames 오타/누락 확인", groupName);
                return false;
            }

            for (int i = 0; i< group.Count; i++) {
                LightGroupItem item = group[i];
                SetOnOff(item.Index, item.Channel, onOff);
            }

            Logging.PrintLog((int)ELogType.LightController, "{0} - Set On : {1}", groupName, onOff);
            return true;
        }

        public bool SetLevel(string groupName, int level) {
            LightGroup group = GetGroup(groupName);
            if (group == null) {
                //260716 hbk 무음 실패 경로 로그화 (SetOnOff 와 동일 규약)
                Logging.PrintLog((int)ELogType.Error, "[Light] 그룹 '{0}' 없음 — SetLevel 무동작 (light.ini 그룹/채널명 확인)", groupName);
                return false;
            }
            if (group.Count == 0) {
                Logging.PrintLog((int)ELogType.Error, "[Light] 그룹 '{0}' 이 비어있음(채널 매핑 소실) — SetLevel 무동작. light.ini ChannelNames 오타/누락 확인", groupName);
                return false;
            }

            for (int i = 0; i < group.Count; i++) {
                LightGroupItem item = group[i];
                SetLevel(item.Index, item.Channel, level);
            }
            Logging.PrintLog((int)ELogType.LightController, "{0} - Set Level : {1}", groupName, level);
            return true;
        }

        // 채널명 → (controllerIndex, channel) 조회. RegisterLightController 의 SetChannelNames 등록명을 그대로 검색한다.
        //  인덱스 하드코딩 시 Bar(Controller B ch1~4) 를 ch0 부터 쓰면 백라이트(ch0)를 오작동시키므로 이름 기반 조회를 강제한다.
        public bool TryFindChannel(string channelName, out int index, out int channel) {
            index = -1;
            channel = -1;
            for (int i = 0; i < Controllers.Count; i++) {
                VirtualLightController con = Controllers[i];
                for (int j = 0; j < con.ChannelCount; j++) {
                    if (con[j].Name == channelName) {
                        index = con.Index;
                        channel = j;
                        return true;
                    }
                }
            }
            return false;
        }

        // 채널명 기반 개별 On/Off — 기존 그룹 오버로드 SetOnOff(string groupName, bool) 와 시그니처가 겹치므로 별도 이름으로 신설.
        public bool SetChannelOnOff(string channelName, bool onOff) {
            int index, channel;
            if (!TryFindChannel(channelName, out index, out channel)) {
                //260716 hbk 무음 실패 경로 로그화 — 이름 미매핑(재배선 오타/누락) 시 조명이 안 켜져도 로그가 전혀 없었고,
                //  호출부(InspectionSequence.ApplyChannelLight)도 반환값을 버려 추적이 불가능했다.
                Logging.PrintLog((int)ELogType.Error, "[Light] 채널명 '{0}' 을 찾을 수 없음 — SetOnOff 무동작. light.ini ChannelNames 오타/누락 확인", channelName);
                return false;
            }
            SetOnOff(index, channel, onOff);
            Logging.PrintLog((int)ELogType.LightController, "{0} - Set On : {1}", channelName, onOff);
            return true;
        }

        // 채널명 기반 개별 밝기 — 기존 그룹 오버로드 SetLevel(string groupName, int) 와 시그니처가 겹치므로 별도 이름으로 신설.
        public bool SetChannelLevel(string channelName, int level) {
            int index, channel;
            if (!TryFindChannel(channelName, out index, out channel)) {
                //260716 hbk 무음 실패 경로 로그화 (SetChannelOnOff 와 동일 규약)
                Logging.PrintLog((int)ELogType.Error, "[Light] 채널명 '{0}' 을 찾을 수 없음 — SetLevel 무동작. light.ini ChannelNames 오타/누락 확인", channelName);
                return false;
            }
            SetLevel(index, channel, level);
            Logging.PrintLog((int)ELogType.LightController, "{0} - Set Level : {1}", channelName, level);
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

        // SetOnOff/SetLevel(및 SetChannelOnOff/SetChannelLevel)은 CmdTable 에 플래그만 세팅하고 즉시 반환한다 —
        //  실제 시리얼 전송은 Execute() 백그라운드 루프(1ms 폴링)가 처리한다. 조명 적용 직후 곧바로 grab 하면
        //  아직 전송 전이라 조명이 안 켜진 채로 촬영되는 문제가 있어(수동 UI 토글은 클릭↔다음 동작 사이 자연 지연이
        //  있어 우연히 문제가 없었을 뿐) — grab 직전에 이 메서드로 큐가 실제로 비워질 때까지 동기 대기한다.
        //  타임아웃은 안전장치(하드웨어 응답 없음 등으로 큐가 영영 안 비는 상황에서도 grab 자체는 진행되게).
        public void WaitForPendingWrites(int timeoutMs = 500) {
            Stopwatch sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < timeoutMs) {
                bool anyPending = false;
                for (int i = 0; i < Controllers.Count && !anyPending; i++) {
                    // 미연결/미오픈 컨트롤러(Execute() 의 IsOpen==false continue 로 영구 skip 됨)의 채널은
                    // 큐에 남아있어도 절대 처리되지 않는다 — 검사 대상에서 제외해야 매 grab 마다 불필요하게
                    // 타임아웃(500ms)을 다 채우는 것을 막을 수 있다.
                    if (Controllers[i].IsOpen == false) continue;
                    for (int j = 0; j < CHANNEL_LIMIT; j++) {
                        if (CmdTable[i, j].IsWriteState || CmdTable[i, j].IsWriteValue) {
                            anyPending = true;
                            break;
                        }
                    }
                }
                if (!anyPending) return;
                Thread.Sleep(2);
            }
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
        

        // light.ini 경로. bin 폴더는 재빌드/재배포 시 지워질 수 있는 산출물 폴더라, Recipe/Calibration 과
        //  동일하게 SystemSetting(LightConfigPath, 기본값 D:\Data\Light)으로 분리해 영속시킨다.
        private static string GetLightIniPath() {
            return Path.Combine(SystemHandler.Handle.Setting.LightConfigPath, "light.ini");
        }

        public bool Load() {
            string loadPath = GetLightIniPath();
            if (File.Exists(loadPath) == false) return false;

            IniFile loadFile = new IniFile();
            loadFile.Load(loadPath);

            for(int i = 0; i < Controllers.Count; i++) {
                string groupName = "Controller" + i.ToString();
                Controllers[i].Port = loadFile[groupName]["Port"].ToInt();
                Controllers[i].Baudrate = loadFile[groupName]["Baudrate"].ToInt();

                // 물리 채널 ↔ 논리 이름(RING_CH1/BACK/ALIGN_COAX 등) 재배선을 코드 수정 없이 하기 위한 override.
                //  RegisterLightController() 의 하드코딩 SetChannelNames 는 그대로 두고(구조 변경 금지, 260713-nse
                //  계획서 예고: "임시 배선은 light.ini 로 대응"), 여기서 키가 있을 때만 이름을 덮어쓴다.
                //  키 부재 시 RegisterLightController 가 부여한 기본 이름 그대로 — 하위호환/회귀 0.
                string channelNamesRaw = loadFile[groupName]["ChannelNames"].ToString();
                if (!string.IsNullOrEmpty(channelNamesRaw)) {
                    string[] names = channelNamesRaw.Split(',');
                    for (int j = 0; j < names.Length && j < Controllers[i].ChannelCount; j++) {
                        string trimmed = names[j].Trim();
                        if (trimmed.Length == 0) continue; // 빈 항목은 기본 이름 유지(부분 override 허용)
                        Controllers[i].SetChannelName(j, trimmed);
                    }
                }
            }

            // ChannelNames override 로 채널 이름이 바뀌었을 수 있으므로, Groups(RING/BACK/BAR/RING7/ALIGN_COAX 등
            //  그룹 API 가 쓰는 고정 인덱스)를 이름 기준으로 다시 찾아 갱신한다. RegisterLightController() 는 Load()
            //  보다 먼저 실행되어 이 시점 이전 채널 배치로 그룹 인덱스가 고정돼 있었다 — 재배선 미반영 결함 수정.
            for (int i = 0; i < Groups.Count; i++) {
                Groups[i].RebindChannels();
            }

            WarnOnDuplicateChannelNames();
            return true;
        }

        // 같은 이름을 가진 채널이 둘 이상이면 TryFindChannel/GetGroup 이 항상 첫 매치(낮은 Controller Index)만 반환해
        //  나머지는 이름으로 조회 불가능해진다(무음 오작동 위험). 앱 기동을 막지는 않되 로그로 즉시 알린다.
        private void WarnOnDuplicateChannelNames() {
            var seen = new Dictionary<string, string>();
            for (int i = 0; i < Controllers.Count; i++) {
                VirtualLightController con = Controllers[i];
                for (int j = 0; j < con.ChannelCount; j++) {
                    string name = con[j].Name;
                    if (string.IsNullOrEmpty(name)) continue;
                    string loc = "Controller" + i + "[" + j + "]";
                    string prevLoc;
                    if (seen.TryGetValue(name, out prevLoc)) {
                        Logging.PrintLog((int)ELogType.LightController,
                            "[light.ini] 채널명 중복: '{0}' 이 {1} 와 {2} 모두에 등록됨 — 이름 조회 시 {1} 만 사용됨(먼저 등록된 쪽)", name, prevLoc, loc);
                    }
                    else {
                        seen[name] = loc;
                    }
                }
            }
        }

        public bool Save() {
            IniFile saveFile = new IniFile();
            string savePath = GetLightIniPath();
            string saveDir = Path.GetDirectoryName(savePath);
            if (!string.IsNullOrEmpty(saveDir) && Directory.Exists(saveDir) == false) Directory.CreateDirectory(saveDir);

            for (int i = 0; i < Controllers.Count; i++) {
                string groupName = "Controller" + i.ToString();
                saveFile[groupName]["Port"] = Controllers[i].Port;
                saveFile[groupName]["Baudrate"] = Controllers[i].Baudrate;

                // 현재 채널명(기본값이든 Load 에서 override 된 값이든)을 그대로 되저장 — 저장 후 재로드해도
                //  override 가 유실되지 않도록 라운드트립 보장(이게 없으면 LightHandlerWindow 저장 시 Port/Baudrate 만
                //  다시 쓰여 ChannelNames 커스텀 값이 지워짐).
                VirtualLightController con = Controllers[i];
                string[] names = new string[con.ChannelCount];
                for (int j = 0; j < con.ChannelCount; j++) {
                    names[j] = con[j].Name;
                }
                saveFile[groupName]["ChannelNames"] = string.Join(",", names);
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
