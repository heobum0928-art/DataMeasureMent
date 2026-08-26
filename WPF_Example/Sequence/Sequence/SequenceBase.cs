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

        //260818 hbk 시퀀스 tact 계측 — Trace 를 시퀀스 흐름 전용으로 정리하면서, 어디서 시간이 가는지
        //  로그만 보고 알 수 있도록 두 층위를 기록한다.
        //   · 액션(Shot) 단위: ExecuteAction 의 첫 tick(OnBegin) → Finish/Error 까지
        //   · 루틴 단위: StartCore(점유 성공) → Finish/Error 까지 = 이 시퀀스 1회 실행 전체
        //  둘 다 원래 있던 실행 경로에 Stopwatch 만 얹은 것이라 동작/판정에는 영향이 없다.
        private readonly System.Diagnostics.Stopwatch _actionTactSw = new System.Diagnostics.Stopwatch();
        private readonly System.Diagnostics.Stopwatch _routineTactSw = new System.Diagnostics.Stopwatch();
        private int _routineActionCount = 0; // 이번 루틴에서 실행 완료한 액션 수

        //260818 hbk 액션(Shot) 시작 — 여기부터 그 Shot 의 단계별 [SEQ] 줄이 이어진다.
        private void LogActionBegin(ActionBase action) {
            try {
                string szActionName;
                if (action != null && action.Name != null) szActionName = action.Name;
                else szActionName = "?";
                Logging.PrintLog((int)ELogType.Trace, "[SEQ] {0} · {1} 시작", Name, szActionName);
            }
            catch { } // 로그 실패가 검사를 막으면 안 된다
        }

        //260818 hbk 액션 1개 완료. 액션명은 Shot 이름과 1:1 이라 어느 자리가 느린지 바로 보인다.
        private void LogActionEnd(ActionBase action, string szResult) {
            try {
                if (!_actionTactSw.IsRunning) return; // OnBegin 을 거치지 않은 경로 방어
                _actionTactSw.Stop();
                _routineActionCount++;
                string szActionName;
                if (action != null && action.Name != null) szActionName = action.Name;
                else szActionName = "?";
                Logging.PrintLog((int)ELogType.Trace, "[SEQ] {0} · {1} 완료 {2} — {3:F2}초",
                    Name, szActionName, szResult, _actionTactSw.Elapsed.TotalSeconds);
            }
            catch { }
        }

        //260818 hbk 루틴(이 시퀀스 1회 실행) 시작/종료. Finish/Error 양쪽에서 종료가 호출된다.
        private void LogRoutineBegin(int nActionCount) {
            try {
                Logging.PrintLog((int)ELogType.Trace, "[SEQ] ── {0} 루틴 시작 (대상 액션 {1}개) ──", Name, nActionCount);
            }
            catch { }
        }

        private void LogRoutineEnd(string szResult) {
            try {
                if (!_routineTactSw.IsRunning) return;
                _routineTactSw.Stop();
                Logging.PrintLog((int)ELogType.Trace, "[SEQ] ── {0} 루틴 종료 {1} — 액션 {2}개, 합계 {3:F2}초 ──",
                    Name, szResult, _routineActionCount, _routineTactSw.Elapsed.TotalSeconds);
            }
            catch { }
        }

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
                //260818 hbk 액션(Shot) 단위 tact 계측 시작 — 이 액션의 첫 tick 에서만 리셋된다.
                _actionTactSw.Restart();
                LogActionBegin(action);
                action.OnBegin(Context);
                IsDoneBegin = true;
            }

            ActionContext actionContext = action.Run();

            if (actionContext.Result == EContextResult.Error) {
                LogActionEnd(action, "ERROR"); //260818 hbk 실패로 끝난 액션도 tact 를 남긴다(느린 실패 추적용)
                CurAction.OnEnd();
                IsDoneBegin = false;

                Context.CopyFrom(actionContext);
                if (OnActionChanged != null) { //260612 hbk Wave5
                    OnActionChanged.Invoke(actionContext);
                }

                Error();
            }
            else if (actionContext.State == EContextState.Finish) {
                LogActionEnd(action, "OK"); //260818 hbk 액션(Shot) 단위 tact
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
            LogThreadMemoryCacheState(); //260814 hbk quick-260814-kx5: 이 스레드가 HALCON을 처음 쓰기 전, 스레드 진입 시 1회만

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
                //260814 hbk TEMP 계측(top-release-2x-slower): 이미 있던 Sleep(5) 의 "실제" 소요를 잰다.
                //  타이머 해상도가 1ms 로 살아있으면 5~6ms, 기본값(15.6ms)으로 회수됐으면 15~16ms 가 나온다 —
                //  NtQueryTimerResolution 은 시스템 전역값이라 프로세스별 실효 상태를 못 보여주므로 이 실측이 유일한 판별법.
                //  Sleep 은 원래 있던 것이라 추가 비용 0.
                var swSleep = System.Diagnostics.Stopwatch.StartNew();
                Thread.Sleep(5);
                LastSleepMs = swSleep.Elapsed.TotalMilliseconds;
            }
        }

        //260814 hbk TEMP 계측(top-release-2x-slower): 직전 루프의 Sleep(5) 실측 소요(ms). FaiTiming 로그에 같이 찍는다.
        public double LastSleepMs { get; private set; }

        //260814 hbk quick-260814-kx5: 스레드 전용(tsp_) 재적용은 제거함 — 오늘 하루 종일 inherited 값이 항상
        //  이미 "idle"로 확인돼(SystemHandler.Initialize() 전역 설정이 이 스레드 생성보다 먼저 실행되므로
        //  정상 상속됨), 재적용이 실제로 뭔가를 바꾼 적이 한 번도 없었다 — 불필요한 방어코드였음이 로그로
        //  증명됨. 이 스레드의 HALCON 캐시 상태를 확인하는 진단 로그만 남긴다.
        private void LogThreadMemoryCacheState() {
            try {
                HTuple mode;
                HOperatorSet.GetSystem("tsp_temporary_mem_cache", out mode);
                Logging.PrintLog((int)ELogType.Trace,
                    "[MemCacheWarmup] seq={0} thread={1} mode={2}",
                    Name, Thread.CurrentThread.ManagedThreadId, mode.S);
            }
            catch (Exception ex) {
                Logging.PrintErrLog((int)ELogType.Error,
                    string.Format("[MemCacheWarmup] seq={0} GetSystem exception: {1}", Name, ex.Message));
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
            int nRoutineActionCount; //260818 hbk 루틴 시작 로그용 — 락 안에서 계산, 락 밖에서 출력
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
                //260818 hbk 루틴 tact 시작 — 점유에 성공한 호출만 여기 도달하므로 경합해도 이중 시작되지 않는다.
                _routineTactSw.Restart();
                _routineActionCount = 0;
                nRoutineActionCount = endActionIndex - actionIndex + 1;
            }
            LogRoutineBegin(nRoutineActionCount); //260818 hbk 락 밖에서 로그(락 보유 중 I/O 최소화)

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

        //260810 hbk reset-datum-clear-race 수정: "State==Idle 이면 X 실행"을 StartCore 와 동일한 _startLock 안에서
        //  원자적으로 수행하는 공용 진입점. 배경: $RESET(InspectionSequence.ResetCycleStateForProtocolReset, 락 없는
        //  _datumTransforms/_failedDatums 등을 건드림) 처럼 "Idle 인 시퀀스에만 안전하게 적용해야 하는" 외부 요청이,
        //  기존엔 State 를 락 밖에서 읽고 그 결과를 근거로 나중에(같은 원자성 없이) 실행해 StartCore 의 Idle→Running
        //  원자 점유와 TOCTOU 로 경합했다(체크 통과 직후 StartCore 가 먼저 점유하면, 실행 중인 시퀀스 스레드와
        //  락 없는 컬렉션을 동시에 건드리게 됨). 해결: 같은 _startLock 을 공유해 두 경로를 상호 배타적으로 만든다.
        //  ▶ OnStart 훅(HandleRunStartResetResults) 패턴과 다른 용도: OnStart 는 "Start 가 성공했을 때"만 발화하는
        //    사이클-시작 훅이고, 이 메서드는 "지금 Idle 인가"를 성공 전제 없이 즉시 판정해야 하는 명시적 외부 요청
        //    (RESET 등) 전용이다 — 서로 대체 관계가 아니라 별도 목적.
        //  ▶ action 은 락 안에서 동기 실행된다 — UI Dispatcher 대기/블로킹 호출을 넣지 말 것(StartCore 가 OnStart 를
        //    락 밖에서 발화하는 이유와 동일한 데드락 회피 원칙). 순수 필드대입/컬렉션 Clear 류에만 사용할 것.
        protected bool TryExecuteIfIdle(Action action) {
            lock (_startLock) {
                if (State != EContextState.Idle) return false; // StartCore 가 이미 점유했거나 다른 사유로 Idle 아님
                action();
                return true;
            }
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

        //260810 hbk bottom-empty-zindex-tcp-delay: z_index 매칭 Shot/Action 이 하나도 없는 "빈" z_index(예:
        //  BOTTOM z=3/z=22 — 레시피에 그 z 를 쓰는 shot 자체가 없는 정상 구성) 전용 즉시-응답 진입점.
        //  StartAll/StartSubset 과 달리 어떤 Action 도 실행하지 않는다 — Action 실행이 필요 없는 이유는
        //  AddResponse()(InspectionSequence.BuildScopedResponse/AggregateIndexFais)가 nZIndex 로 shot 을
        //  필터링해 응답을 조립하므로, 무관한 다른 z 의 shot 을 실행해도(구 StartAll 폴백) 이 응답 내용에는
        //  아무 영향이 없기 때문이다(0-매칭 응답은 Action 실행 여부와 무관하게 항상 동일) — 상세 근거는
        //  Custom/SystemHandler.StartV1Scoped 호출부 주석과 .planning/debug/bottom-empty-zindex-tcp-delay.md 참고.
        //  StartCore 와 동일한 _startLock 원자 점유 계약을 따르되(Idle 아니면 실패), Command=Start 로 워커
        //  스레드(MainExecute)에 넘기지 않고 이 스레드(TCP 처리 스레드, MainRun)에서 곧바로 Finish() 를 호출해
        //  완료 처리한다 — 실행할 Action 이 아예 없으므로 워커 스레드 개입이 불필요하다.
        //  OnStart 는 StartCore 와 동일하게 반드시 발화한다(락 밖에서, 동일한 데드락 회피 원칙) — 최초 설계에서는
        //  "아무것도 시작하지 않으니 생략" 하려 했으나, MainWindow.OnSequenceStart/OnSequenceFinish 가
        //  mainView.SetManualToolsEnabled(false/true) 를 이 인스턴스가 아닌 "모든 시퀀스가 공유하는 단일 UI"에
        //  거는 잠금/해제 쌍이라는 사실을 뒤늦게 확인했다(adversarial 재검증) — OnStart 없이 OnFinish(Finish() 내부)
        //  만 발화하면 이 잠금이 비대칭(해제만 발생)이 되어, 동시에 실행 중인 다른 시퀀스(예: TOP)의 진짜 작업
        //  중에 이 인스턴스의 빈 z_index tick 이 매뉴얼 도구 잠금을 조기 해제해버리는 회귀를 만든다. 그래서 OnStart
        //  도 반드시 짝을 맞춰 발화한다 — HandleRunStartResetResults(fai 결과 재초기화)/HandleFlowLogCycleBegin
        //  (Flow stopwatch 리셋) 도 함께 재실행되지만 이미 모든 z tick 마다 무조건 재실행되는 기존 동작과 동일해
        //  추가 회귀는 없다.
        //  AddResponse()/OnFinish 는 그대로 발화(기존 Finish() 계약 100% 재사용, BuildScopedResponse/
        //  WarnIfEmptyScope/ApplyCycleJudgement 무변경 — 회귀 0 설계).
        public bool StartEmptyScope(TestPacket packet) {
            lock (_startLock) {
                if (State != EContextState.Idle) return false; // StartCore 와 동일 원자 점유 계약

                RequestPacket = packet; // 점유 성공 후에만 기록 (StartCore 와 동일 규약 — 경합 시 덮어쓰기 방지)
                Context.Clear();
                IsFinished = false;
                // Running 전이/Command=Start 없음 — 실행할 Action 이 없어 워커 스레드에 넘기지 않는다.
            }

            if (OnStart != null) { // StartCore 와 동일하게 락 밖에서 발화(Dispatcher 데드락 회피 원칙 동일 적용)
                OnStart.Invoke(Context);
            }

            return Finish(); // Action 실행 없이 즉시 완료 — AddResponse() 가 0-매칭 빈 응답(B 또는 F)을 조립/전송한다.
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

                // 260811 odo: 원시 필드 대신 소유권 API 경유 — 획득→CopyImage→Release 를 원자적으로
                //  수행해 use-after-dispose 레이스를 차단한다(복사 1회, 기존과 동일).
                HImage snapshot = Context.CloneResultImage();
                if (snapshot != null) {
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
            LogRoutineEnd("ERROR"); //260818 hbk 실패로 끝난 루틴도 tact 를 남긴다
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
            LogRoutineEnd("OK"); //260818 hbk 루틴 전체 tact — 응답/이벤트 발화 전에 찍어 흐름 순서를 유지
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

        // 응답은 이 시퀀스가 "다음 요청을 받을 수 있는 상태(Idle)"가 된 뒤에만 내보낸다.
        //  배경: Finish()/Error()/Stop() 은 응답을 큐에 넣고 Command=Stop 만 세운 뒤 돌아가고,
        //  State 가 실제로 Idle 이 되는 것은 MainExecute(5ms tick)가 그 Command 를 처리할 때다.
        //  반면 MainRun(1ms)은 큐를 즉시 비워 보내므로, PLC 는 "끝났다"는 응답을 받고 곧바로 다음
        //  $TEST 를 보낸다 — 그 사이 최대 5ms 동안 StartCore 의 Idle 게이트에 걸려 시작이 거부되고,
        //  그 거부가 F 로 나가 사이클이 통째로 중단된다(실측: 응답 후 7ms 요청 실패 / 8ms 성공,
        //  1ms 차이로 갈리는 경합. 2026-08-26 SIDE 자동검사 z=4·z=8·z=15 등 매번 다른 지점에서 발생).
        //  여기서 Idle 을 기다렸다 내보내면 "응답 도착 = 수락 준비 완료"가 성립해 그 창이 사라진다.
        //  응답은 큐에 남아 다음 MainRun tick(≤1ms)에 다시 시도되므로 유실되지 않는다.
        //  지연은 사이클당 z tick 수 × ≤5ms(SIDE 16 tick 기준 ≤80ms, 사이클 10.9초 대비 무시 가능).
        public TestResultPacket PopResponse() {
            if (State != EContextState.Idle) {
                return null; // 아직 정리 중 — 큐에 그대로 두고 다음 tick 에 재시도
            }
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


