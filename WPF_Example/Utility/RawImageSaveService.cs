using HalconDotNet;
using ReringProject.Network;
using ReringProject.Setting;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;

namespace ReringProject.Utility {
    public sealed class RawImageSaveRequest : IDisposable {
        public HImage Image { get; set; }
        public string SequenceName { get; set; }
        public string ActionName { get; set; }
        public string TestId { get; set; }
        public string TargetCode { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.Now;

        public void Dispose() {
            Image?.Dispose();
            Image = null;
        }
    }

    public sealed class RawImageSaveService : IDisposable {
        private readonly ConcurrentQueue<RawImageSaveRequest> _queue = new ConcurrentQueue<RawImageSaveRequest>();
        private readonly AutoResetEvent _signal = new AutoResetEvent(false);
        private readonly Thread _workerThread;
        private volatile bool _isStopping;
        private volatile bool _isStarted;

        public RawImageSaveService() {
            _workerThread = new Thread(WorkLoop) {
                IsBackground = true,
                Name = "RawImageSaveService",
                Priority = ThreadPriority.BelowNormal
            };
        }

        public void Start() {
            if (!_isStarted) {
                _workerThread.Start();
                _isStarted = true;
            }
        }

        public void Enqueue(RawImageSaveRequest request) {
            if (request == null || request.Image == null) {
                request?.Dispose();
                return;
            }

            _queue.Enqueue(request);
            _signal.Set();
        }

        private void WorkLoop() {
            while (!_isStopping) {
                if (_queue.TryDequeue(out RawImageSaveRequest request)) {
                    SaveRequest(request);
                    continue;
                }

                _signal.WaitOne(100);
            }

            while (_queue.TryDequeue(out RawImageSaveRequest pending)) {
                SaveRequest(pending);
            }
        }

        private static void SaveRequest(RawImageSaveRequest request) {
            try {
                string baseDirectory = SystemHandler.Handle.Setting.GetLogSavePath(ELogType.Image, "Raw", request.Timestamp.ToString("yyyyMMdd"));
                Directory.CreateDirectory(baseDirectory);

                string sequence = SanitizeFilePart(request.SequenceName, "SEQ");
                string action = SanitizeFilePart(request.ActionName, "ACT");
                string testId = SanitizeFilePart(request.TestId, "NOID");
                string target = SanitizeFilePart(request.TargetCode, null);
                string fileName = string.IsNullOrWhiteSpace(target)
                    ? string.Format("{0}_{1}_{2}_{3}.png", request.Timestamp.ToString("HHmmssfff"), sequence, action, testId)
                    : string.Format("{0}_{1}_{2}_{3}_{4}.png", request.Timestamp.ToString("HHmmssfff"), sequence, action, testId, target);
                string filePath = Path.Combine(baseDirectory, fileName);

                request.Image.WriteImage("png", 0, filePath);
            }
            catch (Exception ex) {
                Logging.PrintErrLog((int)ELogType.Error, string.Format("Raw image save failed: {0}", ex.Message));
            }
            finally {
                request.Dispose();
            }
        }

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

        public void Dispose() {
            _isStopping = true;
            _signal.Set();
            if (_isStarted && _workerThread.IsAlive) {
                _workerThread.Join(1000);
            }
            _signal.Dispose();
        }
    }
}
