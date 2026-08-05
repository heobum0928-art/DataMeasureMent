---
phase: quick-260805-mzf
plan: 01
type: execute
wave: 1
depends_on: []
files_modified:
  - WPF_Example/Utility/CaptureImageSaveService.cs
autonomous: false
requirements: [QUICK-260805-mzf]

must_haves:
  truths:
    - "일괄검사를 연속 수행해도 저장 대기 항목 수가 상한 부근에서 유지되고 무한 증가하지 않는다"
    - "저장이 생산 속도를 못 따라가면 검사 사이클(생산측)이 느려진다 — 이미지를 버리거나 건너뛰지 않는다"
    - "일괄검사 N회 후 저장된 origin/capture 파일 수 = FAI수 × 2 × N — 누락 0장"
    - "enqueue 대기는 항상 유한 시간 내에 종료된다(서비스 미시작/종료중/워커 사망/타임아웃 시 즉시 진행)"
    - "큐 깊이 카운터가 enqueue 1회당 +1, 처리 완료 1건당 -1 로 정확히 상쇄되어 드리프트가 없다"
  artifacts:
    - path: "WPF_Example/Utility/CaptureImageSaveService.cs"
      provides: "유계 큐 + 생산측 백프레셔(대기) + 큐 깊이 카운터"
      contains: "MAX_QUEUE_DEPTH"
  key_links:
    - from: "CaptureImageSaveService.Enqueue"
      to: "WaitForQueueSpace"
      via: "_queue.Enqueue 직전 호출 (상한 도달 시 호출 스레드 대기)"
      pattern: "WaitForQueueSpace\\(\\)"
    - from: "CaptureImageSaveService.WorkLoop (TryDequeue 2곳 전부)"
      to: "ProcessDequeued -> Interlocked.Decrement(_nQueueDepth)"
      via: "처리 완료 시 finally 에서 깊이 감소"
      pattern: "Interlocked\\.Decrement\\(ref _nQueueDepth\\)"
---

<objective>
`CaptureImageSaveService` 의 무제한 저장 큐에 상한을 두고, 상한 도달 시 enqueue 하는 검사 스레드가 잠시 대기하도록(백프레셔) 만든다.

Purpose: 일괄검사 중 저장 워커(단일 스레드, BelowNormal, 건당 수백 ms)가 생산 속도를 못 따라가 큐에 요청이 무한 적체 → 각 항목이 Shot 원본 HImage(약 12MB)를 refcount 로 붙잡음 → 프로세스 메모리 58.3GB 도달 후 강제 종료되는 실측 크래시를 구조적으로 차단한다. 원인 조사 완료(CONTEXT.md) — 재조사 불필요, Dispose 로직 결함 아님(속도 불균형 문제).

Output: `WPF_Example/Utility/CaptureImageSaveService.cs` 1개 파일 수정 — 큐 깊이 카운터 + 상한 상수 + 대기 헬퍼 + dequeue 경로 카운터 감소.
</objective>

<execution_context>
@$HOME/.claude/get-shit-done/workflows/execute-plan.md
@$HOME/.claude/get-shit-done/templates/summary.md
</execution_context>

<context>
@.planning/quick/260805-mzf-captureimagesaveservice/260805-mzf-CONTEXT.md
@CLAUDE.md
@WPF_Example/Utility/CaptureImageSaveService.cs

<interfaces>
<!-- 실행자가 코드베이스를 탐색하지 않아도 되도록 필요한 계약을 여기 전부 제공한다. -->

현재 `CaptureImageSaveService` 의 관련 멤버 (WPF_Example/Utility/CaptureImageSaveService.cs):
```csharp
private readonly ConcurrentQueue<CaptureImageSaveRequest> _queue = new ConcurrentQueue<CaptureImageSaveRequest>();
private readonly AutoResetEvent _signal = new AutoResetEvent(false);
private readonly Thread _workerThread;      // 단일 워커, IsBackground=true, Priority=BelowNormal
private volatile bool _isStopping;
private volatile bool _isStarted;

public void Start();                        // _workerThread.Start(); _isStarted = true;
public void Enqueue(CaptureImageSaveRequest request);   // null/Shared 누락 시 request.Dispose() 후 return
private void WorkLoop();                    // TryDequeue 2곳: 본 루프 1곳 + 종료 후 drain 루프 1곳
private static void SaveRequest(CaptureImageSaveRequest request);  // 내부 try/catch/finally 로 절대 throw 안 함
public void Dispose();                      // _isStopping = true; _signal.Set(); Join(1000); _signal.Dispose();
```

호출부 (수정 대상 아님 — 시그니처 불변이므로 무수정으로 동작):
```csharp
// WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs:883-908 (QueueFaiCapture)
sharedSrc.AddRef();  saver.Enqueue(new CaptureImageSaveRequest { ... NeedsRender = false ... });  // origin
sharedSrc.AddRef();  saver.Enqueue(new CaptureImageSaveRequest { ... NeedsRender = true  ... });  // capture
```
`QueueFaiCapture` 는 `Action_FAIMeasurement` 의 `EStep.Measure` 에서 호출된다 = **시퀀스 스레드**(UI 스레드 아님).
따라서 여기서 대기시키면 검사 사이클이 느려질 뿐 UI 프리즈는 발생하지 않는다 — 이것이 의도된 백프레셔 지점이다.

로깅 (WPF_Example/Utility/Logging.cs, WPF_Example/Setting/SystemSetting.cs):
```csharp
Logging.PrintLog(int id, string msg);
Logging.PrintErrLog(int id, string msg);
// SystemHandler 에서 실제 등록된 ELogType: Trace, Camera, TcpConnection, Result, Error, LightController, Flow
// ELogType.Image 는 SetLog 등록이 없어 사용 금지 → 본 작업의 경고는 (int)ELogType.Error 사용
```
</interfaces>
</context>

<tasks>

<task type="auto">
  <name>Task 1: CaptureImageSaveService 큐 상한 + 생산측 백프레셔 구현</name>
  <files>WPF_Example/Utility/CaptureImageSaveService.cs</files>
  <action>
`CaptureImageSaveService` 클래스에만 변경을 가한다. `SharedHImage`, `CaptureImageSaveRequest`, `SaveRequest`, `BuildFileName`, `BuildDirectory`, `BuildFilePath`, `Sanitize*` 는 **일체 수정 금지**(회귀 0).

**(1) 상수 + 깊이 카운터 필드 추가** — `_renderer` 필드 아래에 배치:

```csharp
// 저장 큐 상한. 항목 1건이 Shot 원본 HImage(12MP mono ≈ 12MB)를 refcount 로 붙잡으므로
// 상한 × 이미지 크기가 곧 상주 메모리 상한이다(50 × 12MB ≈ 600MB). 상한 없이 두면
// 일괄검사에서 생산(사이클) > 소비(저장, 건당 수백 ms) 불균형이 그대로 누적돼 프로세스가 죽는다.
private const int MAX_QUEUE_DEPTH = 50;
private const int BACKPRESSURE_POLL_MS = 20;
private const int BACKPRESSURE_MAX_WAIT_MS = 30000;   // 워커가 완전히 멈춘 경우에도 검사가 영구 정지하지 않도록 하는 절대 상한
private const int BACKPRESSURE_LOG_THRESHOLD_MS = 1000; // 이 시간 이상 대기했을 때만 로그(20ms 폴링 노이즈 차단)
private int _nQueueDepth; // 큐 대기 + 처리중(in-flight) 합계. Interlocked/Volatile 로만 접근.
```

**(2) 관측용 읽기 전용 프로퍼티 추가**(향후 UI/진단용, 부작용 없음):

```csharp
/// <summary>현재 저장 대기 + 처리중 항목 수(진단용).</summary>
public int QueueDepth { get { return Volatile.Read(ref _nQueueDepth); } }
```

**(3) `Enqueue` 수정** — 기존 null/Shared 누락 방어(early return)는 그대로 두고, 그 아래 `_queue.Enqueue(request); _signal.Set();` 부분만 다음으로 교체:

```csharp
            WaitForQueueSpace(); // 상한 초과 시 호출 스레드(시퀀스 스레드) 감속 = 백프레셔
            Interlocked.Increment(ref _nQueueDepth);
            _queue.Enqueue(request);
            _signal.Set();
```

**(4) `WaitForQueueSpace()` private 메서드 신규 추가** — `Enqueue` 바로 아래에 배치:

```csharp
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
```

**(5) `WorkLoop` 의 TryDequeue 2곳을 전부 `ProcessDequeued` 경유로 교체** — `SaveRequest(request)` 직접 호출을 남겨두면 카운터가 감소하지 않아 큐가 영구히 "가득 참" 상태로 굳는다(치명적). 본 루프와 종료 후 drain 루프 **둘 다** 교체할 것:

```csharp
                if (_queue.TryDequeue(out CaptureImageSaveRequest request)) {
                    ProcessDequeued(request);
                    continue;
                }
...
            while (_queue.TryDequeue(out CaptureImageSaveRequest pending)) {
                ProcessDequeued(pending);
            }
```

**(6) `ProcessDequeued` private 메서드 신규 추가** — `WorkLoop` 바로 아래:

```csharp
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
```

**코딩 규약 (필수 준수)**
- 삼항 연산자 `?:` 금지 → `if`/`else` 로 전개.
- 헝가리언 표기: 신규 지역/필드는 int=`n`, bool=`b`, string=`sz` 접두사(`nWaitedMs`, `_nQueueDepth`).
- C# 7.2 한정 — switch expression / nullable reference types / `record` 사용 금지.
- 이 파일은 K&R 스타일(여는 중괄호 같은 줄)이다. 그대로 따를 것.
- 주석은 "왜"만 최소로. `//YYMMDD hbk` 날짜 접두 주석 규칙은 폐기됐으므로 신규 주석에 붙이지 말 것.
- `using System.Threading;` 은 이미 존재(Interlocked/Volatile/Thread 추가 using 불필요). `System.Threading.Volatile` 은 .NET 4.8 에서 사용 가능.
- 새 `AutoResetEvent`/`SemaphoreSlim` 등 disposable 동기화 객체를 추가하지 말 것 — 기존 `Dispose()` 가 `_signal.Dispose()` 를 워커 종료 보장 없이 호출하므로, 핸들을 늘리면 `ObjectDisposedException` 표면적만 커진다. 폴링 방식이면 새 핸들이 필요 없다.
- `Dispose()`, `Start()`, `SaveRequest` 및 파일명/경로 헬퍼는 무수정.
  </action>
  <verify>
    <automated>"/c/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/MSBuild.exe" "C:/Info/Project/DataMeasurement/WPF_Example/DatumMeasurement.csproj" //p:Configuration=Debug //p:Platform=x64 //v:minimal //nologo 2>&1 | tail -20</automated>
    <automated>cd "C:/Info/Project/DataMeasurement" && echo "--- increment(1 expected) ---" && grep -c "Interlocked.Increment(ref _nQueueDepth)" WPF_Example/Utility/CaptureImageSaveService.cs && echo "--- decrement(1 expected) ---" && grep -c "Interlocked.Decrement(ref _nQueueDepth)" WPF_Example/Utility/CaptureImageSaveService.cs && echo "--- ProcessDequeued calls(2 expected: TryDequeue 2 sites) ---" && grep -c "ProcessDequeued(" WPF_Example/Utility/CaptureImageSaveService.cs && echo "--- bare SaveRequest calls outside ProcessDequeued(1 expected: inside ProcessDequeued only) ---" && grep -c "SaveRequest(" WPF_Example/Utility/CaptureImageSaveService.cs && echo "--- drop/skip 금지 확인: TryDequeue 후 폐기 코드 없음 ---" && grep -n "TryDequeue" WPF_Example/Utility/CaptureImageSaveService.cs</automated>
  </verify>
  <done>
- Debug/x64 빌드 성공, 신규 에러/경고 0건.
- `Interlocked.Increment(ref _nQueueDepth)` 1곳(Enqueue), `Interlocked.Decrement(ref _nQueueDepth)` 1곳(ProcessDequeued finally).
- `ProcessDequeued(` 호출 2곳 = WorkLoop 본 루프 + 종료 drain 루프. `SaveRequest(` 호출은 `ProcessDequeued` 내부 1곳(+정의 1)만 남는다.
- `Enqueue` 는 어떤 경로로도 request 를 폐기(drop)하지 않는다 — 기존 null/Shared-누락 방어 외에 새로운 skip 분기가 없다.
- 대기 루프에 `_isStopping` / `!_workerThread.IsAlive` / `BACKPRESSURE_MAX_WAIT_MS` 3중 탈출 조건이 모두 존재한다(무한 대기 불가).
- 신규 코드에 삼항 연산자 없음, 헝가리언 표기 준수.
  </done>
</task>

<task type="checkpoint:human-verify" gate="blocking">
  <name>Task 2: 일괄검사 실측 — 메모리 안정 + 이미지 무유실 확인</name>
  <files>(검증 전용 — 코드 변경 없음)</files>
  <what-built>
`CaptureImageSaveService` 저장 큐에 상한(50)을 걸고, 상한 도달 시 검사(시퀀스) 스레드가 20ms 단위로 최대 30초까지 대기했다가 반드시 enqueue 하도록 백프레셔를 넣었다. 이미지는 어떤 경우에도 버려지지 않으며, 대기가 1초를 넘으면 Error 로그에 대기 시간과 큐 깊이가 남는다.
  </what-built>
  <action>
사용자가 실기에서 일괄검사를 돌려 (a) 메모리가 더 이상 단조 증가하지 않는지, (b) 캡쳐 이미지가 1장도 누락되지 않았는지 확인한다. 아래 how-to-verify 절차를 그대로 안내할 것.
  </action>
  <how-to-verify>
1. 앱을 재빌드(Debug/x64)한 뒤 실행한다.
2. 작업 관리자(Ctrl+Shift+Esc) → 세부 정보 탭에서 `DatumMeasurement.exe` 의 메모리를 볼 수 있게 띄워 둔다. 시작 시점 메모리를 메모한다.
3. 일괄검사를 **최소 20회 이상**(가능하면 크래시 재현 때와 같은 회차) 연속 실행한다.
4. 실행 중 메모리 관찰:
   - 기대: 초기값 대비 수백 MB 범위 안에서 오르내리고, 계속 우상향으로만 증가하지 않는다.
   - 실패 신호: GB 단위로 멈추지 않고 계속 증가한다.
5. 일괄검사 완료 후 저장 폴더 확인: `{ResultSavePath}\Image\{yyMMdd}\{HHmm}\original` 과 `...\capture`.
   - 기대 파일 수: `original` = `capture` = (검사한 FAI 수 × 회차 수). 두 폴더 개수가 서로 같아야 한다.
   - 파일 개수 세기(경로는 실제 ResultSavePath 로 치환):
     `find "{ResultSavePath}/Image/$(date +%y%m%d)" -name 'capture_*.jpg' | wc -l`
     `find "{ResultSavePath}/Image/$(date +%y%m%d)" -name 'origin_*.jpg' | wc -l`
6. Error 로그 확인:
   - `저장 지연으로 검사 사이클 대기 ...ms` 가 보이면 → 백프레셔가 정상 동작한 것(정상). 검사가 조금 느려진 대신 메모리를 지킨 것이다.
   - `저장 큐 백프레셔 타임아웃` 이 보이면 → 저장이 30초 동안 1건도 못 빠져나간 것. 저장 경로(네트워크 드라이브 여부)/디스크 속도를 알려주세요.
7. 검사 사이클 체감 속도가 실사용에 문제될 정도로 느려졌는지 알려주세요(상한 50 값 조정 근거로 사용).
  </how-to-verify>
  <verify>사용자 실측 승인. 보조 확인: original/capture 파일 개수가 동일하고 예상 개수와 일치.</verify>
  <done>메모리가 유한 범위에서 안정되고(GB 단위 단조 증가 없음), 캡쳐/원본 이미지 누락 0장이 사용자에 의해 확인됨.</done>
  <resume-signal>"승인" 또는 관찰된 문제(메모리 수치 / 누락 파일 수 / 사이클 체감 속도)를 알려주세요</resume-signal>
</task>

</tasks>

<threat_model>
## Trust Boundaries

| Boundary | Description |
|----------|-------------|
| 시퀀스 스레드(생산자) → 저장 큐 | 검사 스레드가 무제한으로 요청을 밀어 넣는 지점. 속도 계약이 없음 = 자원 고갈 진입점 |
| 저장 큐 → 워커 스레드(소비자) | 단일 스레드 · BelowNormal · 건당 수백 ms. 공유 상태(`_nQueueDepth`, `SharedHImage` refcount) 교차점 |
| 워커 스레드 → 파일 시스템(ResultSavePath) | 느린/네트워크 디스크가 소비 속도를 좌우 → 백프레셔 발동 빈도를 결정 |
| Dispose/종료 경로 → 실행 중인 생산자·소비자 | 종료 중 대기하던 생산자가 영원히 못 깨어날 수 있는 지점 |

## STRIDE Threat Register

| Threat ID | Category | Component | Disposition | Mitigation Plan |
|-----------|----------|-----------|-------------|-----------------|
| T-mzf-01 | D (자원 고갈) | `_queue` 무제한 + 항목당 HImage 12MB 보유 | mitigate | `MAX_QUEUE_DEPTH = 50` 상한 + `WaitForQueueSpace()` 로 생산측 감속. 상주 메모리 상한이 큐 상한 × 이미지 크기로 유계화(≈600MB) |
| T-mzf-02 | D (서비스 정지/행) | `WaitForQueueSpace` 무한 대기 시 검사 스레드 영구 블록 | mitigate | 3중 탈출: `_isStopping`, `!_workerThread.IsAlive`, `BACKPRESSURE_MAX_WAIT_MS(30s)` 절대 상한. 락을 보유한 채 대기하지 않음(`SharedHImage._lock` 밖에서만 대기) |
| T-mzf-03 | T (증거 자료 변조/유실) | drop-oldest 또는 skip 방식 채택 시 캡쳐 이미지 누락 | mitigate | 폐기 로직 자체를 만들지 않음. `WaitForQueueSpace` 는 enqueue 여부가 아니라 시점만 늦춤 — 타임아웃 시에도 반드시 enqueue. verify 에 "TryDequeue 후 폐기 코드 없음" 정적 확인 포함 |
| T-mzf-04 | T (상태 무결성 손상) | `_nQueueDepth` 를 여러 스레드가 비원자적으로 갱신 | mitigate | 갱신은 `Interlocked.Increment/Decrement`, 읽기는 `Volatile.Read` 만 사용. 감소는 `ProcessDequeued` 의 `finally` 단일 지점 → 예외 시에도 드리프트 없음 |
| T-mzf-05 | D (영구 포화 = 자기 유발 교착) | dequeue 경로 중 한 곳(종료 drain 루프)에서 감소 누락 | mitigate | TryDequeue 2곳을 모두 `ProcessDequeued` 경유로 강제 + verify 에서 호출 개수(2)와 `SaveRequest` 잔여 직접 호출 개수를 grep 으로 검증 |
| T-mzf-06 | D (종료 시 행) | `Dispose()` 중 생산자가 대기 상태로 남음 | mitigate | `_isStopping` 은 `volatile` 이라 대기 루프가 즉시 관측 → 다음 폴링(≤20ms)에 탈출. 대기 진입 전에도 `_isStarted/_isStopping` 선검사 |
| T-mzf-07 | R (원인 추적 불가) | 검사가 느려졌는데 이유가 기록되지 않아 운영자가 오진 | mitigate | 대기 ≥1초 시 대기 시간·큐 깊이를 Error 로그에 기록, 타임아웃 시 별도 경고. 20ms 폴링 노이즈는 임계값으로 차단 |
| T-mzf-08 | D (2차 영향) | 백프레셔로 검사 사이클이 느려져 TCP 핸들러 응답이 지연 | accept | 사용자 LOCKED 결정("저장이 못 따라가면 검사 사이클 자체가 자연스럽게 느려지는 방향"). 58.3GB 크래시 대비 명백히 우월. 대기는 큐가 이미 50건일 때만, 자리 1개 나면 즉시 해제(≈수백 ms) |
| T-mzf-09 | D (소프트 캡 초과) | 생산자 다중(Top/Side/Bottom 시퀀스 스레드)일 때 검사-후-증가 사이 경합으로 상한 초과 | accept | 초과분 ≤ 동시 생산자 수(≈3~4건 = 약 48MB)로 유계. 하드 세마포어 도입은 새 disposable 핸들 + `ObjectDisposedException` 표면적을 늘려 T-mzf-06 을 악화시키므로 비채택 |
| T-mzf-10 | I / E / S | 정보 노출 · 권한 상승 · 스푸핑 | accept | 프로세스 내부 스레드 간 큐 제어 변경으로 신뢰 경계 밖 노출면 변화 없음. 파일명 sanitize(T-40.2-01) 경로 무수정 |
</threat_model>

<verification>
1. `msbuild WPF_Example/DatumMeasurement.csproj /p:Configuration=Debug /p:Platform=x64` 성공, 신규 경고 0.
2. 정적 확인 — `Interlocked.Increment` 1 / `Interlocked.Decrement` 1 / `ProcessDequeued(` 호출 2 / `SaveRequest` 직접 호출은 `ProcessDequeued` 내부 1곳뿐.
3. 정적 확인 — `WaitForQueueSpace` 의 탈출 조건 3종(`_isStopping`, `!_workerThread.IsAlive`, 타임아웃) 존재, 무한 루프 불가.
4. 정적 확인 — enqueue 경로에 이미지 폐기/skip 분기 신규 추가 없음(유실 0 보장).
5. 실측(Task 2) — 일괄검사 20회 이상에서 메모리 유한 안정 + original/capture 파일 수 일치·누락 0.
</verification>

<success_criteria>
- 저장 큐 대기 항목이 `MAX_QUEUE_DEPTH`(+동시 생산자 수) 이상으로 늘지 않아 상주 메모리가 유계가 된다.
- 저장이 느릴 때 검사 사이클이 느려지되, 캡쳐/원본 이미지는 단 1장도 유실되지 않는다.
- 어떤 상황(서비스 미시작 / 종료 중 / 워커 사망 / 극단적 저장 지연)에서도 enqueue 호출이 30초 이내에 반환한다.
- Debug/x64 빌드 PASS, 기존 저장 경로·파일명 규칙 회귀 0(`Action_FAIMeasurement` 등 호출부 무수정).
</success_criteria>

<output>
완료 후 `.planning/quick/260805-mzf-captureimagesaveservice/260805-mzf-SUMMARY.md` 생성.
</output>
</content>
</invoke>
