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
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace ReringProject.Utility {
    // Shot 단위 공유 이미지(refcount).
    //  한 Shot 의 모든 FAI origin/capture 요청이 동일 이미지 1개를 공유 → 검사 스레드의 FAI별 대용량 CopyImage 제거(throughput).
    //  생성자가 ref 1 보유(검사 루프 소유). 요청마다 AddRef, 처리 후 Release. ref 0 도달 시 Dispose.
    //  260810 round3: 저장 워커가 여러 개(WORKER_COUNT)로 늘어나 같은 Shot 의 서로 다른 FAI 요청을
    //  서로 다른 워커가 동시에 처리할 수 있다 — 즉 동일 HImage 를 여러 워커 스레드가 동시에 "읽을" 수
    //  있다. HALCON 공식 문서는 동일 객체에 대한 동시 접근의 스레드 안전성을 보장하지 않는다(오히려
    //  호출자 측 수동 동기화를 권고) — 여기서의 "안전" 결론은 문서 보장이 아니라, 실제 운영 해상도
    //  (13376x9528, 127MP)에서 워커 2개가 동일 원본 이미지를 동시에 읽어 렌더링하는 것을 150회 반복
    //  실측(음성대조군 포함 하네스)한 결과 오염/손상 0건이었다는 실측 근거에 기반한다(260810 round4).
    //  lock 은 ref 카운트 보호용(픽셀 접근과 무관).
    public sealed class SharedHImage {
        private HImage _image;
        private int _ref;
        private readonly object _lock = new object();

        public SharedHImage(HImage image) {
            _image = image;
            _ref = 1; // 생성자(검사 루프)가 1 보유
        }

        /// <summary>
        /// 읽기 전용 소스. 워커(여러 스레드 가능, 읽기 전용 접근만 허용)가 참조.
        /// 260811 odo: 이 게터는 <see cref="TryAddRef"/> 가 true 를 반환한 시점부터 대응하는
        /// <see cref="Release"/> 호출 전까지만 유효하다 — 그 구간에서는 참조 카운트가 1 이상으로
        /// 유지되므로 내부 이미지가 해제될 수 없다(다른 스레드가 이미 해제된 힙을 읽는 use-after-dispose
        /// 를 구조적으로 차단). 이 계약 밖에서 읽으면 다시 레이스가 성립한다.
        /// </summary>
        public HImage Image { get { return _image; } }

        // 260811 odo: 성공 여부를 알려주지 않던 기존 AddRef() 를 TryAddRef() 로 위임한다.
        //  기존 호출부(CaptureImageSaveRequest 등)는 이미 살아있음이 보장된 상태에서만 AddRef() 를
        //  호출하므로 동작은 완전히 동일하다(회귀 0) — 차이는 "이미 해제됨"을 원자적으로 알려주는
        //  새 API(TryAddRef)가 SequenceContext 소유권 모델(AcquireResultImage)에 필요하다는 것뿐이다.
        public void AddRef() {
            TryAddRef();
        }

        /// <summary>
        /// 획득 시도. 내부 이미지가 이미 해제(마지막 Release 로 null)됐으면 카운트를 올리지 않고 false 를
        /// 반환한다. 살아 있으면 카운트를 올리고 true 를 반환한다 — "해제 여부"를 원자적으로 호출자에게
        /// 알려준다는 점이 유일한 차이(락 안에서 판정 + 증가가 함께 일어나 TOCTOU 없음).
        /// </summary>
        public bool TryAddRef() {
            lock (_lock) {
                if (_image == null) { return false; }
                _ref++;
                return true;
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
        // 260810 round4 fix: Shared 를 필드로 두고 Dispose() 를 Interlocked.Exchange 로 idempotent/스레드
        //  세이프하게 만들었다. 이유: Enqueue() 가 셧다운 레이스(_signal.Set() 이 ObjectDisposedException)로
        //  이 요청을 조기 Dispose() 할 수 있는데, 그 시점에 Join(1000) 타임아웃으로 아직 살아있는 워커가
        //  드레인 루프에서 같은 요청을 뒤늦게 dequeue 해 SaveRequest 의 finally 에서도 Dispose() 를 호출할
        //  가능성이 있다 — 두 스레드가 동시에 호출해도 Release() 가 정확히 1회만 실행되도록 보장해 이중
        //  Release(공유 이미지 조기 dispose→다른 스레드가 이미 disposed 된 HImage 읽기 크래시) 를 막는다.
        private SharedHImage _shared;
        /// <summary>Shot 단위 공유 소스 이미지(refcount). origin write 및 capture 렌더의 읽기 소스.</summary>
        public SharedHImage Shared { get { return _shared; } set { _shared = value; } }
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
            // 요청 1건당 ref 1 해제 (마지막 해제 시 공유 이미지 dispose). Interlocked.Exchange 로 동시
            //  호출돼도 Release() 는 정확히 1회만 수행됨(먼저 교체에 성공한 스레드만 non-null 을 받는다).
            var shared = Interlocked.Exchange(ref _shared, null);
            if (shared != null) {
                shared.Release();
            }
        }
    }

    // RawImageSaveService 패턴 복제 비동기 캡쳐 저장 워커.
    public sealed class CaptureImageSaveService : IDisposable {
        private readonly ConcurrentQueue<CaptureImageSaveRequest> _queue = new ConcurrentQueue<CaptureImageSaveRequest>();
        // 260810 hbk quick-debug(capture-render-per-fai-slow) round2: 워커가 1개뿐이면 소비(저장/렌더,
        //  건당 ~220-500ms) 속도가 생산(측정) 속도를 못 따라가 MAX_QUEUE_DEPTH 가 한 번 포화된 뒤 다시
        //  안 비워진다 — 실기 로그(captureEnqueue=)로 확정됨. 검사 스레드가 WaitForQueueSpace() 에서
        //  저장 워커 속도에 강제 동기화되던 것이 tact 지연의 실제 원인이었다(round1 의 렌더 1건 비용
        //  최적화만으로는 부족). AutoResetEvent 1개를 여러 워커가 공유 — Set() 은 대기자 1명만 깨우지만
        //  각 워커의 WaitOne(100) 타임아웃이 안전망이라 늦어도 100ms 안에 모든 워커가 큐를 재확인한다
        //  (렌더 1건 처리시간 220ms+ 보다 훨씬 작아 실질적 영향 없음).
        private readonly AutoResetEvent _signal = new AutoResetEvent(false);
        private readonly Thread[] _workerThreads;
        // 워커 전용 렌더러 — 워커별 독립 인스턴스. 260810 round7: OverlayCaptureRenderer 가 HWindow 캐시를
        //  완전히 폐기하고(overpaint_region 기반 재작성) 인스턴스 필드가 없는 무상태(stateless) 클래스로
        //  바뀌어, 이제는 워커끼리 인스턴스를 공유해도 안전하다. 다만 워커별 배열 구조 자체를 되돌리는 것은
        //  이번 라운드의 수정 범위가 아니고(이미 정상 동작 중인 구조를 굳이 건드릴 이유가 없음), 그대로 둔다.
        private readonly OverlayCaptureRenderer[] _renderers;
        private volatile bool _isStopping;
        private volatile bool _isStarted;

        // 저장 워커 개수. 260810 round3: 사용자 요청대로 무작정 늘리지 않고 2개부터 보수적으로 시작.
        //  round6: worker=2 실기 재검증(round3-검증)에서 tact 개선 효과 미미 확정 — 사용자 명시적 지시로
        //  2→3 증설. round7 로 render(HWindow 왕복) 병목이 해소된 뒤, 새 지배적 비용인 [CaptureSave]
        //  write=502-532ms(JPEG 인코딩) 를 round8 스크래치 벤치마크(FullPipelineConcurrency.cs, 실제
        //  프로덕션 DatumMeasurement.exe 직접 참조, render+WriteImage 워커별 동시 실행 재현)로 분석한
        //  결과: 물리 디스크 I/O 가 아니라 JPEG 인코더의 코어 과다경합(oversubscription)이 원인이었다
        //  (worker=3: solo 대비 write 시간 2배+ 로 늘면서 throughput 은 solo 와 거의 동일 = 늘린 워커가
        //  전혀 도움이 안 되는 구간이었음). worker=6 으로 늘리면 개별 호출 지연은 다소 늘어도(코어 경합
        //  자체는 여전히 존재) 전체 저장 처리량이 solo 대비 2.7배(오염 없는 조건)/1.4-1.5배(실기 100
        //  사이클 자동반복이 동시에 도는 오염된 조건, 2회 반복 재검증) 로 뚜렷하게 증가함을 확인해 3→6
        //  증설. (조정 필요 시 이 상수만 변경). CPU 코어 경합은 round1 벤치마크로 render 자체는 무시
        //  가능 수준(+10%, 24코어/32논리프로세서 환경) 이었으나, JPEG 인코딩은 round8 로 훨씬 큰 경합
        //  효과(+100%대)를 보임 — 다만 워커 수를 충분히 늘리면(6개) 그 경합을 상쇄하고도 남는 처리량
        //  이득이 있음을 실측으로 확인. 메모리: 워커 N개 = 최대 N개의 대용량(127-380MB급) 렌더 버퍼가
        //  동시에 생존할 수 있음(N=3 이전 벤치마크로 피크 ~7GB, N=6 은 대략 2배인 ~14GB 로 추정 — 시스템
        //  총 RAM 127GB(wmic 확인) 대비 충분한 여유). MAX_QUEUE_DEPTH 는 워커 수와 무관하게 그대로
        //  유지되어 전체 상주 메모리 상한 역할은 동일하다.
        private const int WORKER_COUNT = 6;

        // 저장 큐 상한. 항목 1건이 Shot 원본 HImage(실제 운영 해상도 13376x9528, ~127MP mono ≈ 127MB)를
        // refcount 로 붙잡으므로 상한 × 이미지 크기가 곧 상주 메모리 상한이다(50 × 127MB ≈ 6.3GB, 이론상
        // 최대치 — 같은 Shot 의 FAI 들은 sharedSrc 를 공유해 실제 동시 상주량은 이보다 훨씬 적다). 상한
        // 없이 두면 일괄검사에서 생산(사이클) > 소비(저장, 건당 수백 ms) 불균형이 그대로 누적돼 프로세스가
        // 죽는다. (260810 round4: 기존 주석의 12MP/12MB 수치는 실측과 다른 오기였음 — 127MB로 정정.)
        private const int MAX_QUEUE_DEPTH = 50;
        private const int BACKPRESSURE_POLL_MS = 20;
        private const int BACKPRESSURE_MAX_WAIT_MS = 30000;   // 워커가 완전히 멈춘 경우에도 검사가 영구 정지하지 않도록 하는 절대 상한
        private const int BACKPRESSURE_LOG_THRESHOLD_MS = 1000; // 이 시간 이상 대기했을 때만 로그(20ms 폴링 노이즈 차단)
        private int _nQueueDepth; // 큐 대기 + 처리중(in-flight) 합계. Interlocked/Volatile 로만 접근.

        /// <summary>현재 저장 대기 + 처리중 항목 수(진단용).</summary>
        public int QueueDepth { get { return Volatile.Read(ref _nQueueDepth); } }

        public CaptureImageSaveService() {
            _workerThreads = new Thread[WORKER_COUNT];
            _renderers = new OverlayCaptureRenderer[WORKER_COUNT];
            for (int i = 0; i < WORKER_COUNT; i++) {
                int workerIndex = i; // 클로저 캡처 — 루프 변수 직접 캡처 금지(C# 5+ 는 안전하나 명시적으로 고정)
                _renderers[i] = new OverlayCaptureRenderer();
                _workerThreads[i] = new Thread(() => WorkLoop(workerIndex)) {
                    IsBackground = true,
                    Name = "CaptureImageSaveService#" + workerIndex,
                    Priority = ThreadPriority.BelowNormal // 검사 throughput 보호
                };
            }
        }

        public void Start() {
            if (!_isStarted) {
                for (int i = 0; i < _workerThreads.Length; i++) {
                    _workerThreads[i].Start();
                }
                _isStarted = true;
            }
        }

        // 워커 중 하나라도 살아있으면 true. WaitForQueueSpace 가 "소비자가 전멸했는지" 판정하는 데 사용.
        private bool AnyWorkerAlive() {
            for (int i = 0; i < _workerThreads.Length; i++) {
                if (_workerThreads[i].IsAlive) { return true; }
            }
            return false;
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
            try {
                _signal.Set();
            } catch (ObjectDisposedException) {
                // 260810 hbk quick-debug(capture-render-per-fai-slow) round4 fix: 이 Enqueue 가 서비스
                //  Dispose() 완료 시점과 겹치면 _signal 이 이미 Dispose 되어 Set() 이 예외를 던질 수 있다.
                //  이 요청은 이미 위에서 _queue 에 들어갔지만, 그 시점에 모든 워커가 이미 종료된 상태라면
                //  아무도 dequeue 하지 않는 미아가 되어 request.Shared 의 ref(호출부가 미리 AddRef 해둔
                //  1개)가 영원히 release 안 되고 샌다 — 여기서 즉시 Dispose() 해 ref 를 해제한다. 큐에
                //  남은 request 객체 자체는 이후 Shared==null 이라 워커가 뒤늦게 dequeue 하더라도
                //  SaveRequest 에서 조용히 no-op 로 스킵되고, finally 의 재-Dispose() 도 Shared==null
                //  가드로 이중 Release 없이 안전하다.
                request.Dispose();
            }
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
                if (_isStopping || !AnyWorkerAlive()) {
                    break; // 종료 중이거나 워커가 전부 죽었다 → 더 기다려봐야 자리가 나지 않는다
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

        // workerIndex: 이 루프를 실행하는 워커의 인덱스 — 자기 소유의 렌더러(_renderers[workerIndex])만 사용.
        private void WorkLoop(int workerIndex) {
            OverlayCaptureRenderer renderer = _renderers[workerIndex];
            while (!_isStopping) {
                if (_queue.TryDequeue(out CaptureImageSaveRequest request)) {
                    ProcessDequeued(request, renderer);
                    continue;
                }

                _signal.WaitOne(100);
            }

            while (_queue.TryDequeue(out CaptureImageSaveRequest pending)) {
                ProcessDequeued(pending, renderer);
            }
        }

        // dequeue 1건 = 카운터 -1 을 단일 지점에서 보장(감소 누락 시 큐가 영구 포화되어 검사가 멈춘다).
        //  처리 완료 후 감소시키므로 처리중(in-flight) 1건도 상한에 포함된다 = 카운터가 실제 상주 메모리와 일치.
        //  ConcurrentQueue.TryDequeue/Interlocked 카운터는 멀티 컨슈머(여러 워커) 세이프 — 코드 변경 불필요.
        private void ProcessDequeued(CaptureImageSaveRequest request, OverlayCaptureRenderer renderer) {
            try {
                SaveRequest(request, renderer);
            }
            finally {
                Interlocked.Decrement(ref _nQueueDepth);
            }
        }

        private static void SaveRequest(CaptureImageSaveRequest request, OverlayCaptureRenderer renderer) {
            HImage rendered = null; // NeedsRender 시 워커가 생성하는 일시 이미지
            var swTotal = Stopwatch.StartNew();
            try {
                // 공유 소스(읽기 전용). 여러 워커가 같은 Shot 의 서로 다른 FAI 요청을 동시에 처리하며
                //  동일 HImage 를 동시에 읽을 수 있다(픽셀 변형 없음, DispObj/GetImageSize 만 호출,
                //  SharedHImage.Image 는 읽기 전용 참조만 노출). HALCON 공식 문서는 이 동시 읽기의
                //  스레드 안전성을 보장하지 않으므로(수동 동기화 권고) — 여기서는 실측(하네스, 127MP
                //  실해상도, 워커 2개 동시읽기 150회 반복, 오염 0건)으로 안전성을 확인했다(260810 round4).
                HImage src = null;
                if (request.Shared != null) {
                    src = request.Shared.Image;
                }
                if (src == null) { return; } // 이미 해제(방어)
                // capture 렌더를 워커 스레드에서 수행(검사 throughput 보호). renderer 는 이 워커 전용 인스턴스.
                HImage toWrite;
                if (request.NeedsRender) {
                    rendered = renderer.RenderToHImage(src, request.Overlays, request.DatumOverlays); // datum 오버레이 포함
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

                // 260810 hbk quick-debug(capture-render-per-fai-slow) round5 계측: WriteImage(JPEG 인코딩+
                //  디스크 쓰기)만 별도로 시간을 잰다. 지금까지 [CaptureRender] 는 렌더(HWindow dump)까지만
                //  계측했고 그 다음 단계(디스크 쓰기)는 완전한 계측 공백이었다 — worker=2 증설이 실기에서
                //  거의 효과가 없었던 이유가 렌더가 아니라 디스크 쓰기 병목일 가능성을 확인하기 위함
                //  (디스크 I/O 는 워커 수를 늘려도 물리 디스크 대역폭에 막히면 그대로 병목일 수 있다).
                var swWrite = Stopwatch.StartNew();
                toWrite.WriteImage("jpeg", 0, filePath);
                long msWrite = swWrite.ElapsedMilliseconds;

                Logging.PrintLog((int)ELogType.Trace,
                    "[CaptureSave] file={0} needsRender={1} write={2}ms total={3}ms",
                    fileName, request.NeedsRender, msWrite, swTotal.ElapsedMilliseconds);
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
            _signal.Set(); // AutoResetEvent 는 대기자 1명만 깨우지만, 각 워커의 WaitOne(100) 타임아웃이
                            //  안전망이라 늦어도 100ms 안에 모든 워커가 _isStopping 을 확인하고 종료한다.
            if (_isStarted) {
                for (int i = 0; i < _workerThreads.Length; i++) {
                    if (_workerThreads[i].IsAlive) {
                        _workerThreads[i].Join(1000);
                    }
                }
            }
            // 260810 round3: 워커별로 완전히 죽은 경우에만 그 워커 소유의 렌더러를 정리(Join 타임아웃으로
            //  특정 워커만 아직 살아있다면 그 워커의 렌더러는 건드리지 않는다 — 동시 접근 시 새 동시성
            //  버그가 되는 것을 방지). round7: OverlayCaptureRenderer.Dispose() 는 이제 정리할 캐시 자원이
            //  없어 no-op 이지만(HWindow 캐시 폐기), 호출부 구조/판정 로직은 변경 없이 그대로 유지한다.
            for (int i = 0; i < _workerThreads.Length; i++) {
                if (!_workerThreads[i].IsAlive) {
                    _renderers[i].Dispose();
                }
            }
            _signal.Dispose();
        }
    }
}
