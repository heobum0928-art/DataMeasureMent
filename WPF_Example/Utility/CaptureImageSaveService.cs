// FAI별 캡쳐 이미지 비동기 저장 서비스. RawImageSaveService 패턴 복제.
//  ResultSavePath\Image\{yyMMdd}\{HHmm}\original|capture 경로 + origin_/capture_ 파일명 규칙.
//  파일명은 호출 스레드(Action_FAIMeasurement)에서 동기 생성(BuildFileName), PNG write 만 워커가 비동기 수행.
using HalconDotNet;
using ReringProject.Halcon.Display;
using ReringProject.Halcon.Models;
using ReringProject.Network;
using ReringProject.Setting;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace ReringProject.Utility {
    // Shot 단위 공유 이미지(refcount).
    //  한 Shot 의 모든 FAI origin/capture 요청이 동일 이미지 1개를 공유 → 검사 스레드의 FAI별 대용량 CopyImage 제거(throughput).
    //  생성자가 ref 1 보유(검사 루프 소유). 요청마다 AddRef, 처리 후 Release. ref 0 도달 시 Dispose.
    //  단일 워커 스레드가 읽기 전용으로만 접근하므로 동시 픽셀 접근 없음(lock 은 ref 카운트 보호용).
    public sealed class SharedHImage {
        private HImage _image;
        private int _ref;
        private readonly object _lock = new object();

        public SharedHImage(HImage image) {
            _image = image;
            _ref = 1; // 생성자(검사 루프)가 1 보유
        }

        /// <summary>읽기 전용 소스. 워커(단일 스레드)만 접근.</summary>
        public HImage Image { get { return _image; } }

        public void AddRef() {
            lock (_lock) {
                if (_image != null) { _ref++; }
            }
        }

        public void Release() {
            lock (_lock) {
                if (_image == null) { return; }
                _ref--;
                if (_ref <= 0) {
                    try { _image.Dispose(); } catch { }
                    _image = null;
                }
            }
        }
    }

    // 캡쳐 저장 요청 DTO. Shot 단위 공유 이미지(SharedHImage)를 참조. 요청 1건 = ref 1, Dispose 가 정확히 1회 Release.
    public sealed class CaptureImageSaveRequest : IDisposable {
        /// <summary>Shot 단위 공유 소스 이미지(refcount). origin write 및 capture 렌더의 읽기 소스.</summary>
        public SharedHImage Shared { get; set; }
        /// <summary>true 면 워커가 Shared.Image+Overlays 로 오버레이 캡쳐 렌더 후 저장. false 면 Shared.Image 직접 write(원본).</summary>
        public bool NeedsRender { get; set; }
        /// <summary>NeedsRender 시 입힐 오버레이 스냅샷.</summary>
        public List<EdgeInspectionOverlay> Overlays { get; set; }
        /// <summary>NeedsRender 시 입힐 datum 검출 오버레이 스냅샷(녹색 원 등). null 허용.</summary>
        public List<DatumCaptureOverlay> DatumOverlays { get; set; }
        /// <summary>동기 결정된 완성 파일명 (origin_... 또는 capture_...)</summary>
        public string FileName { get; set; }
        /// <summary>true=capture 폴더, false=original 폴더.</summary>
        public bool IsCapture { get; set; }
        /// <summary>yyMMdd/HHmm 폴더 계산용. 기본값 = 생성 시각.</summary>
        public DateTime Timestamp { get; set; } = DateTime.Now;

        public void Dispose() {
            // 요청 1건당 ref 1 해제 (마지막 해제 시 공유 이미지 dispose)
            if (Shared != null) {
                Shared.Release();
                Shared = null;
            }
        }
    }

    // RawImageSaveService 패턴 복제 비동기 캡쳐 저장 워커.
    public sealed class CaptureImageSaveService : IDisposable {
        private readonly ConcurrentQueue<CaptureImageSaveRequest> _queue = new ConcurrentQueue<CaptureImageSaveRequest>();
        private readonly AutoResetEvent _signal = new AutoResetEvent(false);
        private readonly Thread _workerThread;
        private volatile bool _isStopping;
        private volatile bool _isStarted;
        // 워커 전용 렌더러. 단일 워커 스레드에서만 사용(직렬) → 버퍼윈도우 경합 없음.
        private static readonly OverlayCaptureRenderer _renderer = new OverlayCaptureRenderer();

        public CaptureImageSaveService() {
            _workerThread = new Thread(WorkLoop) {
                IsBackground = true,
                Name = "CaptureImageSaveService",
                Priority = ThreadPriority.BelowNormal // 검사 throughput 보호
            };
        }

        public void Start() {
            if (!_isStarted) {
                _workerThread.Start();
                _isStarted = true;
            }
        }

        public void Enqueue(CaptureImageSaveRequest request) {
            if (request == null) {
                return;
            }
            // Shared 소스 필수. 누락 시 Dispose(=Release) 로 ref 균형 유지.
            if (request.Shared == null || request.Shared.Image == null) {
                request.Dispose();
                return;
            }

            _queue.Enqueue(request);
            _signal.Set();
        }

        private void WorkLoop() {
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

        private static void SaveRequest(CaptureImageSaveRequest request) {
            HImage rendered = null; // NeedsRender 시 워커가 생성하는 일시 이미지
            try {
                // 공유 소스(읽기 전용). 단일 워커 스레드라 동시 접근 없음.
                HImage src = null;
                if (request.Shared != null) {
                    src = request.Shared.Image;
                }
                if (src == null) { return; } // 이미 해제(방어)
                // capture 렌더를 워커 스레드에서 수행(검사 throughput 보호).
                HImage toWrite;
                if (request.NeedsRender) {
                    rendered = _renderer.RenderToHImage(src, request.Overlays, request.DatumOverlays); // datum 오버레이 포함
                    if (rendered == null) {
                        return; // 렌더 실패(렌더러가 로깅) → PNG 만 누락, 워커 계속
                    }
                    toWrite = rendered;
                } else {
                    toWrite = src; // 원본: 공유 이미지 직접 write
                }

                string baseDirectory = BuildDirectory(request.IsCapture, request.Timestamp);
                Directory.CreateDirectory(baseDirectory);
                string fileName = SanitizeFileName(request.FileName); // 완성 파일명 2차 방어
                string filePath = Path.Combine(baseDirectory, fileName);
                toWrite.WriteImage("jpeg", 0, filePath);
            }
            catch (Exception ex) {
                Logging.PrintErrLog((int)ELogType.Error, string.Format("Capture image save failed: {0}", ex.Message));
            }
            finally {
                if (rendered != null) { try { rendered.Dispose(); } catch { } }
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
        //260610 hbk Phase 40.2 hotfix CO-40.2-08 — judgement(OK/NG) 추가 + 확장자 .png→.jpg(사용자 요청).
        //  결과: {prefix}_{시퀀스}_{FAI}[_{측정점}][_{OK|NG}]_{HHmmssfff}.jpg
        public static string BuildFileName(string prefix, string sequence, string faiName, string measurePointSegment, string judgement, DateTime ts) { //260610 hbk Phase 40.2 hotfix CO-40.2-08
            string seq = SanitizeFilePart(sequence, "SEQ"); //260610 hbk Phase 40.2 — T-40.2-01 traversal 차단
            string fai = SanitizeFilePart(faiName, "FAI"); //260610 hbk Phase 40.2
            string seg = SanitizeFilePart(measurePointSegment, ""); //260610 hbk Phase 40.2 — 빈 segment 허용
            string judge = SanitizeFilePart(judgement, ""); //260610 hbk Phase 40.2 hotfix CO-40.2-08 — OK/NG (빈값 허용)
            string time = ts.ToString("HHmmssfff"); //260610 hbk Phase 40.2
            string name = prefix + "_" + seq + "_" + fai; //260610 hbk Phase 40.2 hotfix CO-40.2-08
            if (!string.IsNullOrEmpty(seg)) { name += "_" + seg; } //260610 hbk Phase 40.2 hotfix CO-40.2-08
            if (!string.IsNullOrEmpty(judge)) { name += "_" + judge; } //260610 hbk Phase 40.2 hotfix CO-40.2-08
            return name + "_" + time + ".jpg"; //260610 hbk Phase 40.2 hotfix CO-40.2-08
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
                return "capture_unknown.jpg"; //260610 hbk Phase 40.2 hotfix CO-40.2-08
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
