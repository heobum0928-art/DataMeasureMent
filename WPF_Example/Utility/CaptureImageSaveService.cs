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

        // 저장 큐 상한. 항목 1건이 Shot 원본 HImage(12MP mono ≈ 12MB)를 refcount 로 붙잡으므로
        // 상한 × 이미지 크기가 곧 상주 메모리 상한이다(50 × 12MB ≈ 600MB). 상한 없이 두면
        // 일괄검사에서 생산(사이클) > 소비(저장, 건당 수백 ms) 불균형이 그대로 누적돼 프로세스가 죽는다.
        private const int MAX_QUEUE_DEPTH = 50;
        private const int BACKPRESSURE_POLL_MS = 20;
        private const int BACKPRESSURE_MAX_WAIT_MS = 30000;   // 워커가 완전히 멈춘 경우에도 검사가 영구 정지하지 않도록 하는 절대 상한
        private const int BACKPRESSURE_LOG_THRESHOLD_MS = 1000; // 이 시간 이상 대기했을 때만 로그(20ms 폴링 노이즈 차단)
        private int _nQueueDepth; // 큐 대기 + 처리중(in-flight) 합계. Interlocked/Volatile 로만 접근.

        /// <summary>현재 저장 대기 + 처리중 항목 수(진단용).</summary>
        public int QueueDepth { get { return Volatile.Read(ref _nQueueDepth); } }

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

            WaitForQueueSpace(); // 상한 초과 시 호출 스레드(시퀀스 스레드) 감속 = 백프레셔
            Interlocked.Increment(ref _nQueueDepth);
            _queue.Enqueue(request);
            _signal.Set();
        }

        // 큐가 상한 이상이면 워커가 자리를 비울 때까지 호출 스레드를 짧게 재운다.
        //  이미지 폐기/스킵은 하지 않는다 — 캡쳐 이미지는 불량 판정의 증거 자료라 유실이 허용되지 않는다.
        //  따라서 이 메서드는 "enqueue 여부"가 아니라 "enqueue 시점"만 늦춘다. 반환 후 enqueue 는 항상 수행된다.
        //  생산자가 여러 시퀀스 스레드일 수 있어 상한은 hard cap 이 아닌 soft cap 이다(초과분 ≤ 동시 생산자 수).
        private void WaitForQueueSpace() {
            if (!_isStarted || _isStopping) {
                return; // 워커가 소비하지 않는 상태에서 기다리면 무의미한 행(hang)이 된다
            }

            int nWaitedMs = 0;
            while (Volatile.Read(ref _nQueueDepth) >= MAX_QUEUE_DEPTH) {
                if (_isStopping || !_workerThread.IsAlive) {
                    break; // 종료 중이거나 워커가 죽었다 → 더 기다려봐야 자리가 나지 않는다
                }
                if (nWaitedMs >= BACKPRESSURE_MAX_WAIT_MS) {
                    Logging.PrintErrLog((int)ELogType.Error, string.Format(
                        "[CaptureImageSaveService] 저장 큐 백프레셔 타임아웃 ({0}ms, depth={1}) — 대기를 포기하고 그대로 저장 큐에 넣습니다(이미지 유실 없음). 저장 경로 속도/워커 상태 확인 필요.",
                        nWaitedMs, Volatile.Read(ref _nQueueDepth)));
                    break; // 유실 금지 — 대기만 포기하고 enqueue 는 반드시 수행한다
                }
                Thread.Sleep(BACKPRESSURE_POLL_MS);
                nWaitedMs += BACKPRESSURE_POLL_MS;
            }

            if (nWaitedMs >= BACKPRESSURE_LOG_THRESHOLD_MS) {
                Logging.PrintLog((int)ELogType.Error, string.Format(
                    "[CaptureImageSaveService] 저장 지연으로 검사 사이클 대기 {0}ms (depth={1}/{2}).",
                    nWaitedMs, Volatile.Read(ref _nQueueDepth), MAX_QUEUE_DEPTH));
            }
        }

        private void WorkLoop() {
            while (!_isStopping) {
                if (_queue.TryDequeue(out CaptureImageSaveRequest request)) {
                    ProcessDequeued(request);
                    continue;
                }

                _signal.WaitOne(100);
            }

            while (_queue.TryDequeue(out CaptureImageSaveRequest pending)) {
                ProcessDequeued(pending);
            }
        }

        // dequeue 1건 = 카운터 -1 을 단일 지점에서 보장(감소 누락 시 큐가 영구 포화되어 검사가 멈춘다).
        //  처리 완료 후 감소시키므로 처리중(in-flight) 1건도 상한에 포함된다 = 카운터가 실제 상주 메모리와 일치.
        private void ProcessDequeued(CaptureImageSaveRequest request) {
            try {
                SaveRequest(request);
            }
            finally {
                Interlocked.Decrement(ref _nQueueDepth);
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

        // 파일명 생성 public static 헬퍼. 호출부(Action_FAIMeasurement)가 동기로 파일명을 만들어 fai 에 write-back.
        //  Single source of truth — origin/capture 둘 다 이 헬퍼로 생성하여 파일명 규칙 일관성 보장.
        //  결과: {prefix}_{시퀀스}_{FAI}[_{측정점}][_{OK|NG}]_{HHmmssfff}.jpg  (segment 빈 경우 생략)
        /// <summary>
        /// FAI별 캡쳐 이미지 파일명 생성. prefix = "origin" 또는 "capture".
        /// 각 segment 는 Path.GetInvalidFileNameChars() 로 sanitize (T-40.2-01 path traversal 차단).
        /// </summary>
        public static string BuildFileName(string prefix, string sequence, string faiName, string measurePointSegment, string judgement, DateTime ts) {
            string seq = SanitizeFilePart(sequence, "SEQ"); // T-40.2-01 traversal 차단
            string fai = SanitizeFilePart(faiName, "FAI");
            string seg = SanitizeFilePart(measurePointSegment, ""); // 빈 segment 허용
            string judge = SanitizeFilePart(judgement, ""); // OK/NG (빈값 허용)
            string time = ts.ToString("HHmmssfff");
            string name = prefix + "_" + seq + "_" + fai;
            if (!string.IsNullOrEmpty(seg)) { name += "_" + seg; }
            if (!string.IsNullOrEmpty(judge)) { name += "_" + judge; }
            return name + "_" + time + ".jpg";
        }

        // 260622 hbk Phase 48 PROTO-01: 자재번호 포함 파일명 오버로드.
        //  nIndexNumber >= 0 이면 _M{번호} 를 FAI 뒤(seg 앞)에 삽입, -1 이면 생략.
        //  결과: prefix_seq_fai[_M{자재번호}][_seg][_judge]_time.jpg
        //  자재번호는 int(Plan 01 에서 비정수→-1 정규화) → traversal 문자 불가 (T-48-10 mitigate).
        //  기존 6-인자 BuildFileName 보존 (다른 호출부 호환, 회귀 0).
        private const int FILENAME_NO_MATERIAL = -1; //260622 hbk Phase 48 PROTO-01: 자재번호 미수신 sentinel (-1). 매직넘버 금지(D-00).
        public static string BuildFileName(string prefix, string sequence, string faiName, string measurePointSegment, string judgement, DateTime ts, int nIndexNumber) {
            string seq   = SanitizeFilePart(sequence, "SEQ");
            string fai   = SanitizeFilePart(faiName, "FAI");
            string seg   = SanitizeFilePart(measurePointSegment, "");
            string judge = SanitizeFilePart(judgement, "");
            string time  = ts.ToString("HHmmssfff");

            string szMat = "";
            bool bHasMaterial = nIndexNumber > FILENAME_NO_MATERIAL;
            if (bHasMaterial)
            {
                szMat = nIndexNumber.ToString();
            }

            string name = prefix + "_" + seq + "_" + fai;
            if (!string.IsNullOrEmpty(szMat))  { name += "_M" + szMat; }
            if (!string.IsNullOrEmpty(seg))    { name += "_" + seg; }
            if (!string.IsNullOrEmpty(judge))  { name += "_" + judge; }
            return name + "_" + time + ".jpg";
        }

        // 저장 디렉토리 계산 단일 소스. SaveRequest 와 write-back 경로가 반드시 일치하도록 공유.
        /// <summary>
        /// 캡쳐 PNG 저장 디렉토리. ResultSavePath\Image\{yyMMdd}\{HHmm}\{original|capture}.
        /// </summary>
        public static string BuildDirectory(bool isCapture, DateTime ts) {
            string subFolder;
            if (isCapture) {
                subFolder = "capture";
            } else {
                subFolder = "original";
            }
            return Path.Combine(
                SystemHandler.Handle.Setting.ResultSavePath, "Image",
                ts.ToString("yyMMdd"),
                ts.ToString("HHmm"),
                subFolder);
        }

        // 엑셀/cycle.json 표기용 절대 경로(디렉토리+파일명). 사용자 요청: 셀에 경로\파일명 표기.
        /// <summary>
        /// 저장될 PNG 의 절대 경로(디렉토리 + sanitize 된 파일명). 실제 저장 위치와 동일.
        /// </summary>
        public static string BuildFilePath(bool isCapture, string fileName, DateTime ts) {
            return Path.Combine(BuildDirectory(isCapture, ts), SanitizeFileName(fileName));
        }

        // RawImageSaveService.cs:95-105 와 동일 복제. 단일 segment sanitize.
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

        // 완성 파일명 전체에 대한 2차 방어 (T-40.2-01). SanitizeFilePart 와 동일 치환 로직.
        private static string SanitizeFileName(string name) {
            if (string.IsNullOrWhiteSpace(name)) {
                return "capture_unknown.jpg";
            }

            foreach (char invalid in Path.GetInvalidFileNameChars()) {
                name = name.Replace(invalid, '_');
            }
            return name;
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
