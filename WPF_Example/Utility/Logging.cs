/*
 * INT Base Library
 * Author : 김영민
 * Version : 1.0
 * Release Date : 2021.03.26
 */

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Controls;

namespace ReringProject.Utility {
    //public delegate void ListLoggerDelegate(string msg, string seperator, Control listView);

    /// <summary>
    /// 로그를 처리하는 클래스
    /// </summary>
    public static class Logging {
        public const string DEFAULT_LOG_EXT = ".log";
        public const int DEFAULT_LOG_CLEAR_COUNT = 500;
        public const string DEFAULT_MSG_SEPERATOR = ",";

        public const string DEFAULT_TRACE_NAME = "Trace";

        private class LogInfo : IDisposable {
            //Identifier
            //public LoggingType LogType;
            public string LogName;
            public string LogPath;

            //Writer
            public Queue<string> Messages;      //메시지 큐
            public StreamWriter LogWriter;

            //날짜 변경 체크용 버퍼
            public DateTime DayChangeChecker;
            public int StartHour;

            //UI 
            public Control pListView;


            //Option
            /// <summary>
            /// LOG 파일 확장자
            /// </summary>
            public string FileExt { get; set; } = DEFAULT_LOG_EXT;                        //확장자

            /// <summary>
            /// 메시지 앞에 날짜 및 시간을 포함시킨다.
            /// </summary>
            public bool MessagePrefixDateTime { get; set; } = true;               //날짜를 앞에 붙일지

            /// <summary>
            /// 메시지 앞에 포함시킬 날짜 및 시간의 포맷 (ex. yyyy-MM-dd hh:mm:ss)
            /// </summary>
            public string MessageDateTimeFormat { get; set; } = "HH:mm:ss:f";             //날짜 포맷

            /// <summary>
            /// 시간과 메시지 구분자
            /// </summary>
            public string MessageSeperator { get; set; } = DEFAULT_MSG_SEPERATOR;

            public LogInfo() {
                Messages = new Queue<string>();
            }

            public void Close() {
                Messages.Clear();
                if (LogWriter != null) LogWriter.Close();
            }

            public void CreateFileWriter() {
                if (!Directory.Exists(LogPath)) {
                    Directory.CreateDirectory(LogPath);
                }
                string path = GetTodaySavePath();
                LogWriter = new StreamWriter(path, true);
            }

            //저장 경로와 파일명을 포함한 전체 경로를 반환한다.
            public string GetTodaySavePath() {
                return string.Format("{0}\\{1:yyyy-MM-dd}_{2}{3}", LogPath, DateTime.Now, LogName, FileExt);
            }

            public bool IsChagedDate(DateTime date) {
                if (DayChangeChecker.Day != date.Day) {
                    DayChangeChecker = DateTime.Now;
                    return true;
                }
                return false;
            }

            public bool IsChangedHour(int nowHour) {
                if (StartHour != nowHour) {
                    StartHour = nowHour;
                    return true;
                }
                return false;
            }

            public void Dispose() {
                Close();
            }
        }

        private static object lockObject = new object();
        private static Dictionary<int, LogInfo> LogList = new Dictionary<int, LogInfo>();
        private static Thread LogWriterThread;
        private static bool IsTerminated;
        private static EventWaitHandle WaitObject = null;
        
        //private static event ListLoggerDelegate ListLogger;

        
        /// <summary>
        /// ListView에 표시할 최대 LOG 갯수 (메모리 부족 방지)
        /// </summary>
        public static int ListControlDisplayCount { get; set; } = DEFAULT_LOG_CLEAR_COUNT;

        /// <summary>
        /// 새 LOG를 지정한다.
        /// </summary>
        /// <param name="id">LOG의 ID</param>
        /// <param name="name">LOG의 이름</param>
        /// <param name="savePath">LOG의 저장 경로</param>
        /// <param name="listView">LOG를 실시간 표시할 ListView 컨트롤</param>
        /// <returns>이미 동일한 ID가 존재하는 경우 false, 그외는 true</returns>
        public static bool SetLog(int id, string name, string savePath = null, Control listView = null) {
            if (LogList.ContainsKey(id)) return false;

            LogInfo newLog = new LogInfo();
            newLog.LogName = name;
            if (savePath == null) {
                savePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, name);
            }
            newLog.LogPath = savePath;
            newLog.StartHour = DateTime.Now.Hour;

            LogList.Add(id, newLog);
            if (listView != null) SetListControl(id, listView);

            return true;
        }
        /// <summary>
        /// 개별 LOG 파일의 확장자를 설정
        /// </summary>
        /// <param name="id">변경할 LOG ID</param>
        /// <param name="fileExt">확장자 (.log)</param>
        /// <returns>해당 ID의 LOG가 없으면 false 반환</returns>
        public static bool SetLogFileExt(int id, string fileExt) {
            if (!LogList.ContainsKey(id)) return false;
            LogInfo info = LogList[id];
            info.FileExt = fileExt;

            return true;
        }

        /// <summary>
        /// 개별 LOG 의 메시지 앞에 날짜 및 시간을 추가
        /// </summary>
        /// <param name="id">변경할 LOG ID</param>
        /// <param name="prefixDateTime">메시지 앞에 DateTime을 추가</param>
        /// <param name="dateTimeFormat">DateTime 포맷 (yyyy-MM-dd hh:mm:ss)</param>
        /// <returns>해당 ID의 LOG가 없으면 false 반환</returns>
        public static bool SetLogPrefixDateTime(int id, bool prefixDateTime, string dateTimeFormat) {
            if (!LogList.ContainsKey(id)) return false;
            LogInfo info = LogList[id];
            info.MessagePrefixDateTime = prefixDateTime;
            info.MessageDateTimeFormat = dateTimeFormat;

            return true;
        }

        /// <summary>
        /// 개별 LOG의 시간과 메시지 구분자를 변경한다.
        /// </summary>
        /// <param name="id">변경할 LOG ID</param>
        /// <param name="seperator">시간과 메시지의 구분 문자 (기본 : ,)</param>
        /// <returns>해당 ID의 LOG가 없으면 false를 반환</returns>
        public static bool SetLogSeperator(int id, string seperator) {
            if (!LogList.ContainsKey(id)) return false;
            LogInfo info = LogList[id];
            info.MessageSeperator = seperator;
            return true;
        }

        /// <summary>
        /// 해당 로그의 저장 경로를 반환.
        /// </summary>
        /// <param name="id">반환할 로그의 ID</param>
        /// <returns>저장 경로</returns>
        public static string GetLogSavePath(int id) { return LogList[id].GetTodaySavePath(); }

        /// <summary>
        /// 관리 LOG의 전체 갯수를 반환
        /// </summary>
        /// <returns>전체 LOG 갯수</returns>
        public static int GetLogCount() { return LogList.Count; }

        /// <summary>
        /// 해당 순번의 LOG ID를 반환
        /// </summary>
        /// <param name="index">반환할 Index</param>
        /// <returns>LOG ID</returns>
        public static int GetLogID(int index) { return LogList.ElementAt(index).Key; }

        /// <summary>
        /// 해당 ID의 로그명을 반환
        /// </summary>
        /// <param name="id">반환할 ID</param>
        /// <returns>LOG 이름</returns>
        public static string GetLogName(int id) { return LogList[id].LogName; }

        /// <summary>
        /// 해당 Index의 로그명을 반환
        /// </summary>
        /// <param name="index">반환할 Index</param>
        /// <returns>LOG 이름</returns>
        public static string GetLogNameByIndex(int index) { return LogList.ElementAt(index).Value.LogName; }

        /// <summary>
        /// 해당 ID의 로그 목록을 표시할 컨트롤을 지정한다.
        /// </summary>
        /// <param name="logID">표시할 로그 ID</param>
        /// <param name="listView">로그를 출력할 List Control (ListBox, ListView, TextBox 중 택)</param>
        public static void SetListControl(int logID, Control listView) {
            lock (lockObject) {
                if (LogList.ContainsKey(logID)) {
                    LogInfo info = LogList[logID];
                    info.pListView = listView;
                }
            }
        }

        public static void SetListControlByIndex(int index, Control listView) {
            if (index > LogList.Count) return;
            lock (lockObject) {
                LogInfo info = LogList.ElementAt(index).Value;
                if (info != null) {
                    info.pListView = listView;
                }
            }
        }

        /// <summary>
        /// 해당 ID의 LOG를 출력한다.
        /// </summary>
        /// <param name="logID">출력할 LOG ID</param>
        /// <param name="format">출력할 텍스트 포맷</param>
        /// <param name="args">포맷에 적용될 파라메타</param>
        /// <returns>성공하면 true 반환</returns>
        public static bool PrintLog(int logID, string format, params object[] args) {
            bool res = true;
            string msg = string.Format(format, args);
            res = PrintLog(logID, msg);
            
            return res;
        }

        /// <summary>
        /// 해당 ID의 LOG를 출력한다.
        /// </summary>
        /// <param name="logID">출력할 LOG ID</param>
        /// <param name="msg">출력할 텍스트</param>
        /// <returns>성공하면 true 반환</returns>
        public static bool PrintLog(int logID, string msg) {
            
            if (IsTerminated) return false;
            if (!LogList.ContainsKey(logID)) return false;

            LogInfo info = LogList[logID];
            Debug.Assert(info.Messages != null);
            if (info.Messages == null) return false;

            lock (lockObject) {
                if (info.MessagePrefixDateTime) {
                    string finalMsg = string.Format("{0}{1}{2}", DateTime.Now.ToString(info.MessageDateTimeFormat), info.MessageSeperator, msg);
                    info.Messages.Enqueue(finalMsg);
                }
                else {
                    info.Messages.Enqueue(msg);
                }
            }

            WaitObject.Set();
            
            return true;
        }
        /// <summary>
        /// 마지막 발생한 win32 Error 정보를 출력한다. 
        /// </summary>
        /// <param name="logID">출력할 LOG ID</param>
        /// <param name="message">출력할 메시지</param>
        /// <param name="memberName">호출된 함수명</param>
        /// <param name="sourceFilePath">호출된 파일명</param>
        /// <param name="sourceLineNumber">라인 번호</param>
        public static void PrintErrLog(int logID, string message = "",
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string sourceFilePath = "",
            [CallerLineNumber] int sourceLineNumber = 0) {

            int errno = Marshal.GetLastWin32Error();
            string errorMessage = new Win32Exception(errno).Message;

            if (message.Length > 0) PrintLog(logID, string.Format("ERROR [{0}, errno({1}) msg: {2}] : {3}", memberName, errno, errorMessage, message));
            else PrintLog(logID, string.Format("ERROR [{0}, errno({1}) msg: {2}]", memberName, errno, errorMessage));
        }

        public static bool PrintLogToCSV(int logID, params string[] data) {
            bool res = true;
            if (IsTerminated) return false;
            if (!LogList.ContainsKey(logID)) return false;

            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < data.Length; i++) {
                sb.Append(data[i]);
                if(i < data.Length-1) sb.Append(LogList[logID].MessageSeperator);
            }
            res = PrintLog(logID, sb.ToString());
            sb.Clear();
            
            return res;
        }

        /// <summary>
        /// 로깅 쓰레드를 시작한다.
        /// </summary>
        public static void Start() {
            IsTerminated = false;

            try {
                
                int logCount = LogList.Count;
                for(int i = 0; i < logCount; i++) {
                    LogInfo info = LogList.ElementAt(i).Value;

                    //start date
                    info.DayChangeChecker = DateTime.Now;

                    //Create Directory
                    info.CreateFileWriter();
                }
                WaitObject = new EventWaitHandle(false, EventResetMode.AutoReset);
                LogWriterThread = new Thread(Execute);
                LogWriterThread.IsBackground = true;
                LogWriterThread.Name = "Logging";
                LogWriterThread.Start();
            }catch(Exception e) {
                for(int i = 0; i < LogList.Count; i++) {
                    LogInfo info = LogList.ElementAt(i).Value;
                    if(info.LogWriter != null){
                        info.LogWriter.Close();
                        info.LogWriter.Dispose();
                        info.LogWriter = null;
                    }
                }
                Debug.Assert(true, e.Message);
            }
            
        }

        /// <summary>
        /// 로깅 쓰레드를 정지시킨다.
        /// </summary>
        public static void Stop() {
            IsTerminated = true;
            if (WaitObject != null) WaitObject.Set();

            if (LogWriterThread != null) {
                LogWriterThread.Join(1000);
                LogWriterThread = null;
            }
        }

        static private void AddMsgToListView(string msg, string seperator, Control listView) {
            listView.Dispatcher.BeginInvoke( System.Windows.Threading.DispatcherPriority.Background, new Action(()=>{
                if (listView is ListBox) {
                    ListBox lb = listView as ListBox;
                    lb.BeginInit();

                    if (lb.Items.Count > ListControlDisplayCount) {
                        lb.Items.RemoveAt(0);
                    }

                    lb.Items.Add(msg);
                    lb.SelectedIndex = lb.Items.Count - 1;

                    lb.EndInit();
                    lb.SelectedIndex = lb.Items.Count - 1;
                    lb.ScrollIntoView(lb.SelectedItem);
                }
                else if (listView is ListView) {
                    ListView lv = listView as ListView;
                    lv.BeginInit();

                    if (lv.Items.Count > ListControlDisplayCount) {
                        lv.Items.RemoveAt(0);
                    }
                    string[] msgList = msg.Split(new string[] { seperator }, StringSplitOptions.None);
                    if (msgList != null) {
                        lv.ItemsSource = msgList;
                    }
                    lv.EndInit();

                    lv.SelectedIndex = lv.Items.Count - 1;
                    lv.ScrollIntoView(lv.SelectedItem);
                }
            }));
        }

        //쓰레드 동작 함수
        private static void Execute() {
            while (!IsTerminated) {
                WaitObject.WaitOne();

                lock (lockObject) {
                    for(int i = 0; i< LogList.Count; i++) {
                        LogInfo info = LogList.ElementAt(i).Value;

                        if (info.LogWriter == null) continue;
                        if (info.Messages.Count == 0) continue;

                        while(info.Messages.Count > 0) {
                            string msg = info.Messages.Dequeue();

                            //날짜 변경 체크 (변경된 경우 파일 새로 생성)
                            if (info.IsChagedDate(DateTime.Now)) {
                                if(info.LogWriter != null) {
                                    info.LogWriter.Flush();
                                    info.LogWriter.Close();
                                    info.LogWriter.Dispose();
                                }
                                info.CreateFileWriter();
                            }
                            info.LogWriter.WriteLine(msg);
                            info.LogWriter.Flush();

                            if (info.pListView != null) {
                                AddMsgToListView(msg, info.MessageSeperator, info.pListView);
                            }
                            Thread.Sleep(1);
                        }

                        if (info.IsChangedHour(DateTime.Now.Hour)) {
                            DeleteLogByDay(SystemHandler.Handle.Setting.LogDeleteDay, info.LogPath);
                        }
                    }
                }
            }

            for(int i = 0; i < LogList.Count; i++) {
                LogInfo info = LogList.ElementAt(i).Value;
                info.Close();
            }
            
        }

        //main window에서 호출한다.
        internal static void DeleteLogByDay(int nKeepDay, string strFilePath) {
            try {
                // 폴더 경로 확인
                string strFolderPath = strFilePath;
                DirectoryInfo logFolder = new DirectoryInfo(strFolderPath);
                foreach (DirectoryInfo dir in logFolder.GetDirectories()) {
                    foreach (FileInfo file in dir.GetFiles()) {
                        // 확장자 확인(이미지 파일도 삭제해야 함)
                        if ((file.Extension != ".log") && (file.Extension != ".jpg"))
                            continue;

                        // Reference : 수정한 날짜
                        //if (file.LastWriteTime < DateTime.Now.AddDays(-(nKeepDay)))
                        //    file.Delete();

                        // Reference : 생성한 날짜
                        if (file.CreationTime < DateTime.Now.AddDays(-(nKeepDay)))
                            file.Delete();
                    }
                }
                logFolder = null;
            }
            catch (Exception ex) {
                Trace.WriteLine(ex.Message);
            }
        }
    }
}
