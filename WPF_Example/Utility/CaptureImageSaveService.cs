//260610 hbk Phase 40.2 — FAI별 캡쳐 이미지 비동기 저장 서비스. RawImageSaveService 패턴 복제.
//  ResultSavePath\Image\{yyMMdd}\{HHmm}\original|capture 경로 + origin_/capture_ 파일명 규칙.
//  파일명은 호출 스레드(Action_FAIMeasurement)에서 동기 생성(BuildFileName), PNG write 만 워커가 비동기 수행.
using HalconDotNet;
using ReringProject.Network;
using ReringProject.Setting;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;

namespace ReringProject.Utility {
    //260610 hbk Phase 40.2 — 캡쳐 저장 요청 DTO. HImage 소유권을 request 가 갖고 저장 후 Dispose.
    public sealed class CaptureImageSaveRequest : IDisposable {
        /// <summary>워커가 저장 후 Dispose 할 소유 객체.</summary>
        public HImage Image { get; set; }
        /// <summary>동기 결정된 완성 파일명 (origin_... 또는 capture_...)</summary>
        public string FileName { get; set; } //260610 hbk Phase 40.2
        /// <summary>true=capture 폴더, false=original 폴더.</summary>
        public bool IsCapture { get; set; } //260610 hbk Phase 40.2
        /// <summary>yyMMdd/HHmm 폴더 계산용. 기본값 = 생성 시각.</summary>
        public DateTime Timestamp { get; set; } = DateTime.Now; //260610 hbk Phase 40.2

        public void Dispose() {
            Image?.Dispose();
            Image = null;
        }
    }

    //260610 hbk Phase 40.2 — RawImageSaveService 패턴 복제 비동기 캡쳐 저장 워커.
    public sealed class CaptureImageSaveService : IDisposable {
        private readonly ConcurrentQueue<CaptureImageSaveRequest> _queue = new ConcurrentQueue<CaptureImageSaveRequest>(); //260610 hbk Phase 40.2
        private readonly AutoResetEvent _signal = new AutoResetEvent(false); //260610 hbk Phase 40.2
        private readonly Thread _workerThread; //260610 hbk Phase 40.2
        private volatile bool _isStopping; //260610 hbk Phase 40.2
        private volatile bool _isStarted; //260610 hbk Phase 40.2

        public CaptureImageSaveService() { //260610 hbk Phase 40.2
            _workerThread = new Thread(WorkLoop) {
                IsBackground = true,
                Name = "CaptureImageSaveService",
                Priority = ThreadPriority.BelowNormal //260610 hbk Phase 40.2 — 검사 throughput 보호
            };
        }

        public void Start() { //260610 hbk Phase 40.2
            if (!_isStarted) {
                _workerThread.Start();
                _isStarted = true;
            }
        }

        public void Enqueue(CaptureImageSaveRequest request) { //260610 hbk Phase 40.2
            if (request == null || request.Image == null) {
                request?.Dispose();
                return;
            }

            _queue.Enqueue(request);
            _signal.Set();
        }

        private void WorkLoop() { //260610 hbk Phase 40.2
            while (!_isStopping) {
                if (_queue.TryDequeue(out CaptureImageSaveRequest request)) {
                    SaveRequest(request);
                    continue;
                }

                _signal.WaitOne(100);
            }

            while (_queue.TryDequeue(out CaptureImageSaveRequest pending)) {
                SaveRequest(pending);
            }
        }

        private static void SaveRequest(CaptureImageSaveRequest request) { //260610 hbk Phase 40.2
            try {
                string baseDirectory = BuildDirectory(request.IsCapture, request.Timestamp); //260610 hbk Phase 40.2 hotfix CO-40.2-02 — 디렉토리 계산 단일 소스화
                Directory.CreateDirectory(baseDirectory);
                string fileName = SanitizeFileName(request.FileName); //260610 hbk Phase 40.2 — 완성 파일명 2차 방어
                string filePath = Path.Combine(baseDirectory, fileName);
                request.Image.WriteImage("png", 0, filePath); //260610 hbk Phase 40.2 — RawImageSaveService.cs:85 동일 HImage→PNG API
            }
            catch (Exception ex) {
                Logging.PrintErrLog((int)ELogType.Error, string.Format("Capture image save failed: {0}", ex.Message)); //260610 hbk Phase 40.2
            }
            finally {
                request.Dispose();
            }
        }

        //260610 hbk Phase 40.2 — 파일명 생성 public static 헬퍼. 호출부(Action_FAIMeasurement)가 동기로 파일명을 만들어 fai 에 write-back.
        //  Single source of truth — origin/capture 둘 다 이 헬퍼로 생성하여 파일명 규칙 일관성 보장.
        //  결과: {prefix}_{시퀀스}_{FAI}_{측정점}_{HHmmssfff}.png  (segment 빈 경우 생략)
        /// <summary>
        /// FAI별 캡쳐 이미지 파일명 생성. prefix = "origin" 또는 "capture".
        /// 각 segment 는 Path.GetInvalidFileNameChars() 로 sanitize (T-40.2-01 path traversal 차단).
        /// </summary>
        public static string BuildFileName(string prefix, string sequence, string faiName, string measurePointSegment, DateTime ts) { //260610 hbk Phase 40.2
            string seq = SanitizeFilePart(sequence, "SEQ"); //260610 hbk Phase 40.2 — T-40.2-01 traversal 차단
            string fai = SanitizeFilePart(faiName, "FAI"); //260610 hbk Phase 40.2
            string seg = SanitizeFilePart(measurePointSegment, ""); //260610 hbk Phase 40.2 — 빈 segment 허용
            string time = ts.ToString("HHmmssfff"); //260610 hbk Phase 40.2
            if (string.IsNullOrEmpty(seg)) {
                return string.Format("{0}_{1}_{2}_{3}.png", prefix, seq, fai, time); //260610 hbk Phase 40.2
            }
            return string.Format("{0}_{1}_{2}_{3}_{4}.png", prefix, seq, fai, seg, time); //260610 hbk Phase 40.2
        }

        //260610 hbk Phase 40.2 hotfix CO-40.2-02 — 저장 디렉토리 계산 단일 소스. SaveRequest 와 write-back 경로가 반드시 일치하도록 공유.
        /// <summary>
        /// 캡쳐 PNG 저장 디렉토리. ResultSavePath\Image\{yyMMdd}\{HHmm}\{original|capture}.
        /// </summary>
        public static string BuildDirectory(bool isCapture, DateTime ts) { //260610 hbk Phase 40.2 hotfix CO-40.2-02
            return Path.Combine(
                SystemHandler.Handle.Setting.ResultSavePath, "Image",
                ts.ToString("yyMMdd"),
                ts.ToString("HHmm"),
                isCapture ? "capture" : "original");
        }

        //260610 hbk Phase 40.2 hotfix CO-40.2-02 — 엑셀/cycle.json 표기용 절대 경로(디렉토리+파일명). 사용자 요청: 셀에 경로\파일명 표기.
        /// <summary>
        /// 저장될 PNG 의 절대 경로(디렉토리 + sanitize 된 파일명). 실제 저장 위치와 동일.
        /// </summary>
        public static string BuildFilePath(bool isCapture, string fileName, DateTime ts) { //260610 hbk Phase 40.2 hotfix CO-40.2-02
            return Path.Combine(BuildDirectory(isCapture, ts), SanitizeFileName(fileName));
        }

        //260610 hbk Phase 40.2 — RawImageSaveService.cs:95-105 와 동일 복제. 단일 segment sanitize.
        private static string SanitizeFilePart(string value, string fallback) {
            string text = string.IsNullOrWhiteSpace(value) ? fallback : value;
            if (string.IsNullOrWhiteSpace(text)) {
                return string.Empty;
            }

            foreach (char invalid in Path.GetInvalidFileNameChars()) {
                text = text.Replace(invalid, '_');
            }
            return text;
        }

        //260610 hbk Phase 40.2 — 완성 파일명 전체에 대한 2차 방어 (T-40.2-01). SanitizeFilePart 와 동일 치환 로직.
        private static string SanitizeFileName(string name) {
            if (string.IsNullOrWhiteSpace(name)) {
                return "capture_unknown.png";
            }

            foreach (char invalid in Path.GetInvalidFileNameChars()) {
                name = name.Replace(invalid, '_');
            }
            return name;
        }

        public void Dispose() { //260610 hbk Phase 40.2
            _isStopping = true;
            _signal.Set();
            if (_isStarted && _workerThread.IsAlive) {
                _workerThread.Join(1000);
            }
            _signal.Dispose();
        }
    }
}
