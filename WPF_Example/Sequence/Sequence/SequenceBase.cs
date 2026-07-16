using ReringProject.Define;
using System;
using System.Collections.Generic;
using System.Threading;
using ReringProject.Setting;
using ReringProject.Utility;
using ReringProject.Network;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using OpenCvSharp;
using HalconDotNet;
using ReringProject.Halcon;
using System.IO;
using ReringProject.Device;
using ReringProject.UI;

namespace ReringProject.Sequence {

    #region delegates
    public delegate void EventSequenceStateChanged(SequenceContext context);
    public delegate void EventActionChanged(ActionContext context);
    #endregion

    #region enums

    public enum ESequenceCommmand {
        Stop,
        Start,
        Pause,
        Resume,
    }

    #endregion

    public partial class SequenceBase {
        public bool IsInitialized { get; protected set; }

        public ESequence ID { get; private set; }

        public string Name { get; private set; }

        public string TargetID { get; set; }    //Sequence target id, wafer id, barcode ....

        public int CurrentActionIndex { get; protected set; }
        public int EndActionIndex { get; protected set; }

        protected ActionBase [] Actions;
        protected ActionBase CurAction = null;

        public ParamBase Param { get; protected set; }

        public string CurActionName {
            get {
                if (CurAction == null) return EContextState.Idle.ToString();
                return CurAction.Name;
            }
        }

        private Thread MainThread;
        private bool IsTerminated = false;

        protected bool bCreated { get; private set; } = false;
        public SequenceContext Context { get; protected set; } = null;
        protected bool IsDoneBegin = false;
        public bool IsFinished { get; protected set; } = false;

        public ESequenceCommmand Command { get; protected set; }

        public TestPacket RequestPacket { get; private set; } = null;
        public ConcurrentQueue<TestResultPacket> ResponseQueue { get; private set; } = new ConcurrentQueue<TestResultPacket>();

        public SequenceBase(ESequence id, string name) {
            ID = id;
            Name = name;

            CurrentActionIndex = 0;

            IsTerminated = false;
            MainThread = new Thread(MainExecute);
            MainThread.Priority = ThreadPriority.Highest;
            MainThread.Name = Name;
            MainThread.Start();
        }

        public SequenceBase(ESequence id, params ActionBase [] actions) {
            ID = id;
            Name = Enum.GetName(typeof(ESequence), id);
            Actions = actions;

            CurrentActionIndex = 0;

            Context = new SequenceContext(this);

            IsTerminated = false;
            MainThread = new Thread(MainExecute);
            MainThread.Priority = ThreadPriority.Normal;
            MainThread.Name = Name;
            MainThread.Start();
        }

        public void AddAction(params ActionBase[] actions) {
            Actions = actions;
            Param.Parent = this;
            foreach (ActionBase act in Actions) {
                if ((Param != null) && (Param is CameraMasterParam)) {
                    CameraMasterParam masterParam = Param as CameraMasterParam;
                    if (act.Param is CameraSlaveParam) {
                        CameraSlaveParam camParam = act.Param as CameraSlaveParam;
                        if (string.IsNullOrEmpty(camParam.DeviceName)) {
                            camParam.DeviceName = masterParam.DeviceName;
                        }
                        masterParam.AddChild(camParam);
                    }
                }
                act.Param.Parent = this;
            }


        }

        public EContextState State {
            get { return Context.State; }
            private set { Context.State = value; }
        }

        public EContextResult Result {
            get { return Context.Result; }
            private set { Context.Result = value; }
        }

        public void SequenceCheck() {
            if (Actions.Length == 0) throw new InvalidOperationException("Action list is Empty.");
        }

        public virtual void OnCreate() {
            if (Context == null) Context = new SequenceContext(this);

            foreach(ActionBase action in Actions) {
                action.OnCreate();
            }
            bCreated = true;
        }

        public virtual void OnRelease() {

        }

        public void Release() {
            foreach (ActionBase action in Actions) {
                action.Release();
            }

            IsTerminated = true;
            MainThread.Join(1000);
        }

        public int ActionCount { get => Actions.Length; }

        public ActionBase this[int index] {
            get {
                if (index >= Actions.Length) return null;
                return Actions[index];
            }
        }

        public ActionBase this[EAction id] {
            get {
                foreach (ActionBase act in Actions) {
                    if (act.ID == id) return act;
                }
                return null;
            }
        }

        public ActionBase this[string name] {
            get {
                EAction id = (EAction)Enum.Parse(typeof(EAction), name);
                return this[id];
            }
        }

        public ActionBase GetAction(int index) {
            if (index >= Actions.Length) return null;
            return Actions[index];
        }

        public int GetIndexOf(string name) {
            for (int i = 0; i < Actions.Length; i++) {
                if (Actions[i].Name == name) {
                    return i;
                }
            }
            return -1;
        }

        public int GetIndexOf(EAction actionID) {
            for(int i = 0; i < Actions.Length; i++) {
                if(Actions[i].ID == actionID) {
                    return i;
                }
            }
            return -1;
        }

        private void ExecuteAction(ActionBase action) {
            if (IsDoneBegin == false) {
                Context.ActionParam = action.Param;
                if (TargetID != null)
                    Context.TargetCode = TargetID;
                action.OnBegin(Context);
                IsDoneBegin = true;
            }

            ActionContext actionContext = action.Run();

            if (actionContext.Result == EContextResult.Error) {
                CurAction.OnEnd();
                IsDoneBegin = false;

                Context.CopyFrom(actionContext);
                if (OnActionChanged != null) { //260612 hbk Wave5
                    OnActionChanged.Invoke(actionContext);
                }

                Error();
            }
            else if (actionContext.State == EContextState.Finish) {
                CurAction.OnEnd();
                IsDoneBegin = false;

                Context.CopyFrom(actionContext);
                if (OnActionChanged != null) { //260612 hbk Wave5
                    OnActionChanged.Invoke(actionContext);
                }

                if (CurrentActionIndex >= EndActionIndex) {
                    Context.Result = actionContext.Result;
                    Finish();
                } else {
                    CurrentActionIndex++;
                    CurAction = Actions[CurrentActionIndex];
                }
            }
        }

        private void MainExecute() {
            while(IsTerminated == false) {
                if (bCreated == false) {
                    Thread.Sleep(1000);
                    continue;
                }

                try { // 처리되지 않은 예외로 인한 스레드 종료 방지 — 예외 발생 시 Error()로 잠금 해제 보장
                    switch (Command) {
                        case ESequenceCommmand.Stop:
                            State = EContextState.Idle;
                            break;
                        case ESequenceCommmand.Pause:
                            State = EContextState.Paused;
                            break;
                        case ESequenceCommmand.Start:
                            State = EContextState.Running;
                            CurAction = Actions[CurrentActionIndex];
                            ExecuteAction(CurAction);
                            break;
                    }
                }
                catch (Exception ex) { //260517 hbk 예외 캐치 → Error() 호출로 OnError 이벤트 보장 (잠금 미해제 방지)
                    Logging.PrintErrLog((int)ELogType.Error,
                        string.Format("[MainExecute] Unhandled exception in sequence '{0}': {1}", Name, ex.Message));
                    IsDoneBegin = false;
                    try { Error(); } catch { } //260517 hbk Error() 내 2차 예외도 무시 (로그 스레드 재진입 방지)
                }
                Thread.Sleep(5);
            }
        }
        public virtual void OnLoad() {
            foreach(ActionBase act in Actions) {
                act.OnLoad();
            }
        }

        /// <summary>
        /// 요청 패킷으로 sequence 수행
        /// </summary>
        /// <param name="packet"></param>
        /// <returns></returns>
        public bool Start(TestPacket packet) {
            if (State != EContextState.Idle) return false; // 빠른 사전 컷(원자 점유는 StartCore 에서)

            //260716 hbk RequestPacket 대입을 점유(StartCore) 이후로 이동 — 기존엔 인덱스 조회 실패/점유 경합 시에도
            //  RequestPacket 을 먼저 덮어써, 다른 스레드가 진행 중인 요청의 응답 대상을 날릴 수 있었다(TCP 응답 유실).
            string actName = packet.Identifier2;
            int actionIndex = GetIndexOf(actName);
            if(actionIndex == -1) {

                return false;
            }
            return StartCore(actionIndex, actionIndex, packet);
        }

        /// <summary>
        /// 이벤트로 수행되는 start
        /// </summary>
        /// <param name="actionID"></param>
        /// <returns></returns>
        public bool Start(EAction actionID) {
            if (State != EContextState.Idle) return false; // 빠른 사전 컷(원자 점유는 StartCore 에서)

            //id가 없으면 첫 번째 action 수행
            if (actionID == EAction.Unknown) {
                return Start(0);
            }
            int i = GetIndexOf(actionID);
            if(i == -1) {
                return false;
            }
            return Start(i);
        }

        protected bool Start(int actionIndex = 0) {
            return StartCore(actionIndex, actionIndex, null);
        }

        //260716 hbk 시작 경합(TOCTOU) 차단용 락. Start 계열 전 경로(Start/StartAll/StartSubset)가 공유한다.
        private readonly object _startLock = new object();

        //260716 hbk Start 계열 공통 원자 점유 경로.
        //  배경: 기존 4개 진입점이 모두 "State != Idle 체크 → 필드 대입 → Command=Start" 였는데, State 는 시퀀스 스레드가
        //  Command 를 처리(최대 5ms tick)해야 Running 이 되므로, UI RUN 버튼(UI 스레드)과 TCP $TEST(MainRun 스레드)가
        //  그 사이에 겹치면 둘 다 체크를 통과했다. 결과: RequestPacket/CurrentActionIndex/EndActionIndex 가 나중 호출자
        //  값으로 조용히 덮어써져 (a) TCP 요청이 응답을 영원히 못 받거나 (b) 의도와 다른 액션 범위가 실행됨 — 로그/예외 없음.
        //  해결: 락 안에서 State 를 즉시 Running 으로 점유해 두 번째 호출자가 State 체크에서 확실히 탈락하게 한다.
        //  (MainExecute 가 Command=Start 를 보고 State=Running 을 다시 세팅하지만 동일 값이라 idempotent — 회귀 없음.
        //   Command 는 소비 후에도 Start 로 유지되는 구조라 가드로 쓸 수 없어 State 점유 방식을 택했다.)
        //  OnStart.Invoke 는 락 밖에서 호출 — 구독자(MainWindow.OnSequenceStart)가 Dispatcher 로 UI 에 접근하므로,
        //  락을 쥔 채 UI 를 기다리면 UI 스레드가 같은 락을 요청할 때 데드락 위험이 있다(오늘 grab 데드락과 동일 클래스).
        private bool StartCore(int actionIndex, int endActionIndex, TestPacket packet) {
            lock (_startLock) {
                if (State != EContextState.Idle) return false; // 원자 점유 — 여기서만 승자가 결정된다
                if (Actions == null || Actions.Length == 0) return false;
                if (actionIndex < 0 || endActionIndex >= Actions.Length || actionIndex > endActionIndex) return false;

                RequestPacket = packet; // 점유 성공 후에만 기록 (경합 시 남의 요청을 덮어쓰지 않음)
                CurrentActionIndex = actionIndex;
                EndActionIndex = endActionIndex;

                Context.Clear();
                IsFinished = false;
                State = EContextState.Running; // 즉시 점유 → 이후 호출자는 위 State 체크에서 탈락
            }

            //260517 hbk OnStart 이벤트를 Command=Start 이전에 발화한다.
            //  이로써 Dispatcher 큐에 SetManualToolsEnabled(false) [잠금] 이 먼저 등록되고,
            //  이후 완료 이벤트(OnFinish/OnStop/OnError)의 SetManualToolsEnabled(true) [해제]가
            //  항상 뒤에 처리되어 잠금 순서가 보장된다 (race condition 차단).
            if (OnStart != null) { //260612 hbk Wave5
                OnStart.Invoke(Context);
            }
            Command = ESequenceCommmand.Start; //260517 hbk OnStart 발화 이후 Command 설정 (순서 보장)
            return true;
        }

        //260409 hbk Phase 5: 모든 Action 순차 실행 (D-01)
        public bool StartAll(TestPacket packet) {
            if (State != EContextState.Idle) return false; // 빠른 사전 컷(원자 점유는 StartCore 에서)
            if (Actions == null || Actions.Length == 0) return false;

            //260716 hbk StartCore 로 통일 — 원자 점유 + RequestPacket 을 점유 후에만 기록(경합 시 덮어쓰기 방지)
            return StartCore(0, Actions.Length - 1, packet);
        }

        //260616 hbk Phase 51: 선택 Action(SHOT) 인덱스 집합으로 부분 실행 (D-01 SHOT 다중 선택, StartAll 변형)
        public bool StartSubset(int[] actionIndices, TestPacket packet) {
            if (State != EContextState.Idle) return false; // 빠른 사전 컷(원자 점유는 StartCore 에서)
            if (Actions == null || Actions.Length == 0) return false;
            if (actionIndices == null || actionIndices.Length == 0) return false;

            Array.Sort(actionIndices);
            int first = actionIndices[0];
            int last = actionIndices[actionIndices.Length - 1];
            if (first < 0 || last >= Actions.Length) return false;

            //260716 hbk StartCore 로 통일 (StartAll 과 동일 규약)
            return StartCore(first, last, packet);
        }

        public bool Stop() {
            if (State == EContextState.Idle) return false;
            Context.Timer.Restart();
            Command = ESequenceCommmand.Stop;
            if(RequestPacket != null) AddResponse();

            if (OnStop != null) { //260612 hbk Wave5
                OnStop.Invoke(Context);
            }
            return true;
        }

        //260520 hbk Manual Tools Locked root cause — 본체 전체 try/catch 추가.
        //  배경: DatumPhase 실패 등으로 Grab 단계 미실행 시 Context.ResultHalconImage 가 disposed/잘못된 핸들 → CopyImage() 동기 예외 →
        //  Error()/Finish() 의 OnError/OnFinish?.Invoke() 까지 도달 못함 → MainWindow.SetManualToolsEnabled(true) 미호출 → 잠금 영구화.
        //  격리 원칙: 이미지 저장 실패가 시퀀스 완료 이벤트 발화를 막아서는 안 됨.
        protected void SaveResultImage(string actionName) {
            try { //260520 hbk
                if (SystemHandler.Handle.Setting.SaveFailImage == false) {
                    Context.ResultImageFileName = null;
                    return;
                }

                if (Context.ResultHalconImage != null) {
                    HImage snapshot = Context.ResultHalconImage.CopyImage();
                    Task.Factory.StartNew((object obj) => {
                        HImage resultImage = obj as HImage;
                        try {
                            string filePath = SystemHandler.Handle.Setting.GetResultImageSavePath(Name, actionName);
                            Context.ResultImageFileName = Path.GetFileName(filePath);

                            using (HImage grayImage = resultImage.CountChannels().I == 1 ? resultImage.CopyImage() : resultImage.Rgb1ToGray()) {
                                string format = Path.GetExtension(filePath).TrimStart('.');
                                grayImage.WriteImage(format, 0, filePath);
                            }
                        }
                        catch (Exception ex) {
                            CustomMessageBox.Show("Fail to Save Image", "Image Save Fail : " + ex.Message, System.Windows.MessageBoxImage.Error);
                        }
                        finally {
                            resultImage.Dispose();
                        }
                    }, snapshot);
                    return;
                }
            } //260520 hbk
            catch (Exception ex) { //260520 hbk — CopyImage 등 동기 예외 격리 (OnError/OnFinish 발화 보장)
                try { Logging.PrintErrLog((int)ELogType.Error, "[SaveResultImage] Sync exception swallowed (lock-release path protected): " + ex.Message); } catch { } //260520 hbk
                Context.ResultImageFileName = null; //260520 hbk
                return; //260520 hbk
            }
        }

        protected bool Error() {
            Context.State = EContextState.Error;
            Context.Result = EContextResult.Error;

            IsFinished = true;

            Context.Timer.Restart();
            Command = ESequenceCommmand.Stop;
            if (RequestPacket != null) AddResponse();

            SaveResultImage(CurActionName);
            if (OnError != null) { //260612 hbk Wave5
                OnError.Invoke(Context);
            }

            return true;
        }

        protected bool Finish() {
            Context.State = EContextState.Finish;
            IsFinished = true;

            Context.Timer.Restart();
            Command = ESequenceCommmand.Stop;
            if (RequestPacket != null) AddResponse();

            if (Context.Result == EContextResult.Fail) {
                SaveResultImage(CurActionName);
            }

            if (OnFinish != null) { //260612 hbk Wave5
                OnFinish.Invoke(Context);
            }
            return true;
        }

        public bool Pause() {
            if (State != EContextState.Running) return false;

            if(CurAction != null) {
                CurAction.OnPaused();
                Context.Timer.Restart();
            }
            Command = ESequenceCommmand.Pause;
            if (OnPaused != null) { //260612 hbk Wave5
                OnPaused.Invoke(Context);
            }
            return true;
        }

        public bool Resume() {
            if (State != EContextState.Paused) return false;

            if(CurAction != null) {
                CurAction.OnResume();
                Context.Timer.Restart();
            }
            Command = ESequenceCommmand.Resume;
            if (OnResume != null) { //260612 hbk Wave5
                OnResume.Invoke(Context);
            }
            return true;
        }

        protected virtual void AddResponse() {
        }

        public int IsResponseReady {
            get {
                return ResponseQueue.Count;
            }
        }

        public TestResultPacket PopResponse() {
            if(IsResponseReady > 0) {
                if(ResponseQueue.TryDequeue(out TestResultPacket respInfo)) {
                    return respInfo;
                }
                return null;
            }
            return null;
        }

        public override string ToString() {
            return Name;
        }

        public event EventSequenceStateChanged OnStart;
        public event EventSequenceStateChanged OnStop;
        public event EventSequenceStateChanged OnFinish;
        public event EventSequenceStateChanged OnPaused;
        public event EventSequenceStateChanged OnResume;
        public event EventSequenceStateChanged OnError;
        public event EventActionChanged OnActionChanged;

    }
}


