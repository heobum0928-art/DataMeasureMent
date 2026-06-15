using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using ReringProject.Setting;
using ReringProject.Define;
using System.Windows.Threading;
using ReringProject.Utility;
using ReringProject.Sequence;
using ReringProject.UI;
using ReringProject.Device;
using ReringProject.Login;
using System.Windows.Controls;
using ReringProject.Properties;

namespace ReringProject {

    public enum EPageType {
        Ready,
        Recipe,
        Log,
        Setting,
        Camera,
        Light,
        Connect,
        Login,
        ProcessMonitor,
        Reviewer,   // Phase 40 OUT-01 D-08 — 결과 리뷰어 비모달 창
    }

    /// <summary>
    /// MainWindow.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class MainWindow : Window {

        [DllImport("user32.dll")]
        static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wp, IntPtr lp);
        [DllImport("user32.dll")]
        static extern IntPtr GetSystemMenu(IntPtr hWnd, bool bRevert);
        [DllImport("user32.dll")]
        static extern int TrackPopupMenu(IntPtr hMenu, uint uFlags, int x, int y, int nReserved, IntPtr hWnd, IntPtr prcRect);

        private Point startPos;
        //private PageType CurrentPage = PageType.Ready;

        private SystemHandler mSystemHandler;


        private bool _IsEditable;
        public bool IsEditable {
            get {
                return _IsEditable;
            }

            set {
                this.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Normal, new Action(() => {
                    mainView.IsEditable = value;
                    inspectionList.IsEditable = value;
                    menuBar.IsEditable = value;
                    _IsEditable = value;
                }));
            }
        }

        //UI
        private Window mModalWindow;
        private ProcessMonitorWindow mProcMonitorWindow;
        private UI.ReviewerWindow mReviewerWindow;
        private DispatcherTimer mTimer = new DispatcherTimer();



        public MainWindow() {
            Logging.PrintLog((int)ELogType.Trace, "[STARTUP-WHITE] (b) MainWindow ctor entry: {0} ms", App.StartupWatch.ElapsedMilliseconds); //260615 hbk Phase 43.1
            //initialize
            mSystemHandler = SystemHandler.Handle;
            mSystemHandler.Initialize();
            Logging.PrintLog((int)ELogType.Trace, "[STARTUP-WHITE] (c) after Initialize: {0} ms", App.StartupWatch.ElapsedMilliseconds); //260615 hbk Phase 43.1

            //update ui
            InitializeComponent();
            Logging.PrintLog((int)ELogType.Trace, "[STARTUP-WHITE] (d) after InitializeComponent: {0} ms", App.StartupWatch.ElapsedMilliseconds); //260615 hbk Phase 43.1
            //ComboLanguage.ItemsSource = Enum.GetValues(typeof(ELanguageType)).Cast<ELanguageType>();

            mSystemHandler.Sequences.OnRecipeChanged += this.OnLoadRecipe;

            mSystemHandler.Login.OnLoginStateChanged += this.OnLoginChanged;

            for (int i = 0; i < mSystemHandler.Sequences.Count; i++) {
                mSystemHandler.Sequences[i].OnStart += OnSequenceStart;
                mSystemHandler.Sequences[i].OnStop += OnSequenceStop;
                mSystemHandler.Sequences[i].OnError += OnSequenceError;
                mSystemHandler.Sequences[i].OnFinish += OnSequenceFinish;
                mSystemHandler.Sequences[i].OnActionChanged += OnActionChanged;
            }
            mTimer.Dispatcher.Thread.Priority = System.Threading.ThreadPriority.AboveNormal;
            mTimer.Interval = TimeSpan.FromMilliseconds(constantInterval);
            mTimer.Tick += TimerTick;

        }
        const int constantInterval = 100;
        private int timerCallCount = 0;
        const int statusUpdateInterval = 1000;
        private void TimerTick(object sender, EventArgs args) {

            var now = DateTime.Now;
            //var nowMilliseconds = (int)now.TimeOfDay.TotalMilliseconds;
            //var timerInterval = constantInterval - nowMilliseconds % constantInterval + 5;//5: sometimes the tick comes few millisecs early
            //mTimer.Interval = TimeSpan.FromMilliseconds(timerInterval);

            if (this.IsVisible) {
                menuBar.UpdateState();
                if ((timerCallCount * constantInterval) >= statusUpdateInterval) {
                    statusBar.Model.UpdateResourceInfo();
                    timerCallCount = 0;
                }
                if(mProcMonitorWindow != null) {
                    if (mProcMonitorWindow.IsVisible) {
                        mProcMonitorWindow.Model.UpdateState();
                    }
                }
            }
            else {
                mTimer.Stop();
            }
            timerCallCount++;
        }

        //언어 설정이 변경될 때 호출합니다.
        private void LanguageChanged(object sender, string e) {

        }

        public void SelectedDrawingItemChanged(object sender, SelectionChangedCallbackArg arg) {
            //mParentWindow.SelectParam()
        }

        private void OnSequenceStart(SequenceContext context) {
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Normal, new Action(() => {
                mainView.SetManualToolsEnabled(false);
                statusBar.Model.SetText(string.Format("[DIAG] Start fired: {0}", context.Source.Name));
            }));
        }

        // SetManualToolsEnabled(true) + 상태바 갱신을 단일 BeginInvoke 로 통합.
        //  기존: SetManualToolsEnabled(true)는 BeginInvoke, statusBar.SetText는 시퀀스 스레드 직접 호출 — 분리.
        //  수정: 모두 BeginInvoke 내부에서 순서대로 실행 — 잠금 해제와 상태바 갱신이 UI 스레드에서 원자적으로 처리됨.
        private void OnSequenceStop(SequenceContext context) {
            Logging.PrintLog((int)ELogType.Result, context.ToString());
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Normal, new Action(() => {
                mainView.SetManualToolsEnabled(true);
                statusBar.Model.SetText(string.Format("{0} Stop.({1},{2}ms)", context.Source.Name, context.ResultString, context.Timer.ElapsedMilliseconds.ToString()));
            }));
        }

        // SetManualToolsEnabled(true) + DisplaySequenceContext + 상태바 갱신을 단일 BeginInvoke 로 통합.
        //  기존: SetManualToolsEnabled(true)는 BeginInvoke, DisplaySequenceContext/SetText는 시퀀스 스레드 직접 호출.
        //  수정: 모두 BeginInvoke 내부에서 순서대로 실행 — 잠금 해제 → 결과 표시 순서 보장.
        private void OnSequenceError(SequenceContext context) {
            Logging.PrintLog((int)ELogType.Result, context.ToString());
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Normal, new Action(() => {
                mainView.SetManualToolsEnabled(true);
                mainView.DisplaySequenceContext(context);
                statusBar.Model.SetText(string.Format("{0} Error.({1},{2}ms)", context.Source.Name, context.ResultString, context.Timer.ElapsedMilliseconds.ToString()));
            }));
        }

        private void OnActionChanged(ActionContext context) {
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Normal, new Action(() => {
                mainView.DisplayActionContext(context);
                string actName;
                if (context.Source != null) {
                    actName = context.Source.Name;
                }
                else {
                    actName = "?";
                }
                statusBar.Model.SetText(string.Format("[DIAG] Action done: {0} result={1}", actName, context.Result));
            }));
        }

        // SetManualToolsEnabled(true) + DisplaySequenceContext + 로그 + 상태바 갱신을 단일 BeginInvoke 로 통합.
        //  기존: SetManualToolsEnabled(true)는 BeginInvoke, DisplaySequenceContext/SetText는 시퀀스 스레드 직접 호출.
        //  수정: 모두 BeginInvoke 내부에서 순서대로 실행 — 잠금 해제 → 결과 표시 순서 보장.
        //  로그(Logging.PrintLog)만 시퀀스 스레드에서 유지 (스레드 안전, 즉시 기록 의도).
        private void OnSequenceFinish(SequenceContext context) {
            Logging.PrintLog((int)ELogType.Result, "Sequence {0} Final Result: {1} ({2}ms)",
                context.Source.Name, context.ResultString, context.Timer.ElapsedMilliseconds);
            Logging.PrintLog((int)ELogType.Result, context.ToString());
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Normal, new Action(() => {
                mainView.SetManualToolsEnabled(true);
                mainView.DisplaySequenceContext(context);
                statusBar.Model.SetText(string.Format("{0} Finished.({1},{2}ms)", context.Source.Name, context.ResultString, context.Timer.ElapsedMilliseconds.ToString()));
            }));
        }

        private void Title_MouseDown(object sender, MouseButtonEventArgs e) {
            if (e.ChangedButton == MouseButton.Left) {
                if (e.ClickCount >= 2) {
                    if (this.WindowState == WindowState.Normal) {
                        this.WindowState = WindowState.Maximized;
                    }
                    else {
                        this.WindowState = WindowState.Normal;
                    }
                }
                else {
                    startPos = e.GetPosition(null);
                }
            }
            else if (e.ChangedButton == MouseButton.Right) {
                var pos = PointToScreen(e.GetPosition(this));
                IntPtr hWnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
                IntPtr hMenu = GetSystemMenu(hWnd, false);
                int cmd = TrackPopupMenu(hMenu, 0x100, (int)pos.X, (int)pos.Y, 0, hWnd, IntPtr.Zero);
                if (cmd > 0) SendMessage(hWnd, 0x112, (IntPtr)cmd, IntPtr.Zero);
            }
        }

        private void Title_MouseMove(object sender, MouseEventArgs e) {
            if (e.LeftButton == MouseButtonState.Pressed) {
                if (this.WindowState == WindowState.Maximized && Math.Abs(startPos.Y - e.GetPosition(null).Y) > 2) {
                    var point = PointToScreen(e.GetPosition(null));

                    this.WindowState = WindowState.Normal;

                    this.Left = point.X - this.ActualWidth / 2;
                    this.Top = point.Y - TitleLabel.ActualHeight / 2;
                }
                DragMove();
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e) {
            Close();
        }

        public void StartSequence(ESequence seqID, EAction actID= EAction.Unknown) {
            mSystemHandler.Sequences.Start(seqID, actID);
        }

        public void StartSequence(string seqName, int actNo = 0) {

        }

        public void OnLoginChanged(object sender, LoginEventArgs args) {
            this.IsEditable = args.IsLogined;
            menuBar.UpdateLoginID(mSystemHandler.Login.LoginID);
        }

        public void OnLoadRecipe(object sender, RecipeChangedEventArgs args) {
            //args.RecipeName;
            inspectionList.OnLoadRecipe(args.RecipeName);
        }

        public void SaveRecipe(string name=null) {
            if(mSystemHandler.Sequences.StateAll == EContextState.Running) {
                CustomMessageBox.Show("Error", SystemHandler.Handle.Localize["System Is Running"], MessageBoxImage.Error);
                return;
            }
            if(!mSystemHandler.Sequences.SaveRecipe(name, ERecipeFileType.Ini)) {
                CustomMessageBox.Show("Error", SystemHandler.Handle.Localize["fail to save recipe"], MessageBoxImage.Error);
                return;
            }
            CustomMessageBox.Show(SystemHandler.Handle.Localize["Save Recipe Success"], string.Format("Recipe : {0} Saved!", name), MessageBoxImage.Information, false);
        }

        //main view 위에 표시한다.
        public void PopupView(EPageType page) {
            if(mModalWindow != null) {
                //closing window or else
            }
            switch (page) {
                case EPageType.Login:
                    mModalWindow = new LoginWindow();
                    mModalWindow.Owner = this;
                    if(mModalWindow.ShowDialog() == true) {

                    }
                    break;
                case EPageType.Camera:
                    if(mSystemHandler.Sequences.IsIdle == false) {
                        CustomMessageBox.Show(mSystemHandler.Localize["Sequence is Running"], mSystemHandler.Localize["window cannot be displayed.\nWait until the sequence is completed."], MessageBoxImage.Error);
                        return;
                    }
                    string selectedDevName = null;
                    if(inspectionList.SelectedParam != null) {
                        ICameraParam param = inspectionList.SelectedParam as ICameraParam;
                        if(param != null) {
                            selectedDevName = param.DeviceName;
                        }
                    }
                    mModalWindow = new DeviceSelector(selectedDevName);
                    mModalWindow.Owner = this;
                    if(mModalWindow.ShowDialog() == true) {
                        DeviceSelector devWindow = mModalWindow as DeviceSelector;
                        if (mainView.DrawScale != devWindow.SelectedDisplayConfig.DrawScale) {
                            mainView.DrawScale = devWindow.SelectedDisplayConfig.DrawScale;
                        }

                    }
                    break;
                case EPageType.Connect:
                    mModalWindow = new TcpServerWindow();
                    mModalWindow.Owner = this;
                    if(mModalWindow.ShowDialog() == true){

                    }
                    break;
                case EPageType.Light:
                    mModalWindow = new LightHandlerWindow();
                    mModalWindow.Owner = this;
                    if(mModalWindow.ShowDialog() == true) {

                    }
                    break;
                case EPageType.Log:
                    break;
                case EPageType.Ready:
                    break;
                case EPageType.Recipe:
                    mModalWindow = new OpenRecipeWindow();
                    mModalWindow.Owner = this;
                    if (mModalWindow.ShowDialog() == true) {
                        string selectedName = (mModalWindow as OpenRecipeWindow).SelectedRecipeName;
                        if (!mSystemHandler.LoadRecipe(selectedName)) {
                            CustomMessageBox.Show("Error", mSystemHandler.Localize["fail to load recipe"], MessageBoxImage.Error);
                        }
                    }
                    break;
                case EPageType.Setting:
                    mModalWindow = new SettingWindow();
                    mModalWindow.Owner = this;
                    mModalWindow.ShowDialog();
                    break;
                case EPageType.ProcessMonitor:
                    if(mProcMonitorWindow != null) {
                        if (mProcMonitorWindow.IsLoaded) {
                            mProcMonitorWindow.Show();
                            return;
                        }
                    }
                    mProcMonitorWindow = new ProcessMonitorWindow();
                    mProcMonitorWindow.Owner = this;
                    mProcMonitorWindow.Show();
                    break;
                case EPageType.Reviewer:   // Phase 40 OUT-01 D-08 — 비모달 Show() (ShowDialog 아님, 라이브 검사 방해 안 함)
                    if (mReviewerWindow != null && mReviewerWindow.IsLoaded) {
                        mReviewerWindow.Show();
                        return;
                    }
                    mReviewerWindow = new UI.ReviewerWindow();
                    mReviewerWindow.Owner = this;
                    mReviewerWindow.Show();   // 비모달 — 라이브 MainView 와 동시 사용 가능 (D-08)
                    break;
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e) {
            CustomMessageBox.Parent = this;
            mTimer.Start();

            //register custom ui
            RegisterCustomUI();

            //load recipe

            if (mSystemHandler.Setting.CurrentRecipeName != null)
            {
                mSystemHandler.LoadRecipe(mSystemHandler.Setting.CurrentRecipeName);
            }

            IsEditable = false;
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e) {
            MessageBoxResult result = CustomMessageBox.ShowConfirmation("Confirm", SystemHandler.Handle.Localize["Are you sure you want to exit program?"], MessageBoxButton.YesNo);
            if(result == MessageBoxResult.No) {
                e.Cancel = true;
                return;
            }

            //register
            for (int i = 0; i < mSystemHandler.Sequences.Count; i++) {
                mSystemHandler.Sequences[i].OnStart -= OnSequenceStart;
                mSystemHandler.Sequences[i].OnStop -= OnSequenceStop;
                mSystemHandler.Sequences[i].OnError -= OnSequenceError;
                mSystemHandler.Sequences[i].OnFinish -= OnSequenceFinish;
                mSystemHandler.Sequences[i].OnActionChanged -= OnActionChanged;
            }
            mSystemHandler.Release();
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e) {
            this.WindowState = WindowState.Minimized;
        }

        public void AddCustomMainView(string name, UserControl control) {
            mainView.AddCustomControl(name, control);
        }
    }
}
