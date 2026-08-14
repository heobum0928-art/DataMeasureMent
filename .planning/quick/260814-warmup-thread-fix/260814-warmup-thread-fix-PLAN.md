---
phase: quick-260814-warmup-thread-fix
plan: 01
type: execute
wave: 1
depends_on: []
files_modified:
  - WPF_Example/Sequence/Sequence/SequenceBase.cs
  - WPF_Example/Custom/SystemHandler.cs
autonomous: true
requirements: [MEASURE-WARMUP-01]

must_haves:
  truths:
    - "측정 파이프라인 워밍업 콜백이 Task.Run(스레드풀 임의 스레드)이 아니라, 실제 검사가 도는 그 SequenceBase.MainThread 위에서 실행된다"
    - "IsMeasureWarmupComplete 게이트는 등록된 모든 시퀀스의 워밍업 콜백이 완료되거나 타임아웃(30초)될 때까지 닫혀 있다가 그 이후 열린다 — 이전처럼 Task.Run 진입 즉시 열리지 않는다"
    - "워밍업 대상 시퀀스가 없거나 콜백이 끝내 실행되지 않아도(타임아웃) 앱 기동은 막히지 않는다(fail-open 유지)"
  artifacts:
    - path: "WPF_Example/Sequence/Sequence/SequenceBase.cs"
      provides: "CallbackQueue(ConcurrentQueue<Action>) + EnqueueCallback + MainExecute 매 iteration 드레인"
      contains: "EnqueueCallback"
    - path: "WPF_Example/Custom/SystemHandler.cs"
      provides: "시퀀스별 워밍업 콜백 enqueue + Interlocked 카운트다운 + 타임아웃 감시자 + RunMeasureWarmup/FindMeasureWarmupShot sequenceName 파라미터화"
      contains: "targetSeq.EnqueueCallback"
  key_links:
    - from: "SystemHandler.StartMeasureWarmupAsync"
      to: "SequenceBase.CallbackQueue"
      via: "targetSeq.EnqueueCallback(() => RunMeasureWarmup(sequenceName))"
      pattern: "targetSeq\\.EnqueueCallback"
    - from: "SequenceBase.MainExecute"
      to: "SequenceBase.DrainCallbackQueue"
      via: "매 while 루프 iteration 무조건 호출"
      pattern: "DrainCallbackQueue\\(\\);"
    - from: "RunMeasureWarmup(sequenceName)"
      to: "FindMeasureWarmupShot(sequenceName, ...)"
      via: "OwnerSequenceName 일치 Shot 우선 선택"
      pattern: "FindMeasureWarmupShot\\(sequenceName"
---

<objective>
quick-260814-dxy(커밋 2fbbe94/79974f6) + quick-260814-warmup-transform-fix(커밋 1860bd5)가 만든 측정 파이프라인
워밍업은 `Task.Run(() => RunMeasureWarmup())` 로 **.NET 스레드풀의 임의 스레드**에서 실행된다. 그런데 실제
Top/Side/Bottom 검사는 각 `SequenceBase` 생성자가 만드는 **시퀀스 전용 `Thread`**(`MainThread = new
Thread(MainExecute); MainThread.Start();`)에서 돈다. MVTec 공식 "HALCON Memory Management" 기술노트에 따르면
HALCON 의 temp-mem 캐시는 **스레드별로 관리되는 per-thread proc-handle** 에 붙는다 — 다른 스레드가 캐시를
공유하지 않는다. 즉 워밍업이 데운 스레드와 실제 측정이 도는 스레드가 완전히 다르므로, warmup-transform-fix 로
`measure_pos` 가 실제로 성공(`success=735 fail=150`)하게 만든 뒤에도 그 다음 실제 Top 사이클 속도(3.5~5.1초)가
전혀 개선되지 않았다 — 실측으로 확인된 사실.

**수정 방향:** `SequenceBase` 에 스레드-안전 콜백 큐(`ConcurrentQueue<Action>`)를 추가하고, `MainExecute()` 루프가
매 iteration 마다(Command 상태 무관) 큐를 드레인해서 자기 스레드 위에서 콜백을 실행하게 만든다.
`StartMeasureWarmupAsync()` 는 `Task.Run` 으로 워밍업 로직 자체를 돌리는 대신, 등록된 각 시퀀스(SequenceHandler
— 이 PC 의 PcRole/CameraRole 로 이미 필터링된 실제 활성 시퀀스만)에 워밍업 콜백을 `EnqueueCallback` 으로 넣는다.
게이트(`IsMeasureWarmupComplete`)는 모든 시퀀스의 콜백이 완료되거나(Interlocked 카운트다운) 타임아웃(30초, 감시자
자체는 HALCON 을 안 건드리므로 스레드풀에서 폴링해도 무방)되면 열린다 — fail-open 원칙 유지.

Purpose: 워밍업이 실제로 프로덕션 검사가 도는 그 스레드의 HALCON per-thread 캐시를 데워서, quick-260814-dxy 가
원래 의도했던 "Release 콜드스타트 저하 완화" 효과가 실제로 발휘되게 한다. (여전히 "완전 해결" 보장은 아니다 —
스레드 문제를 고쳐도 다른 요인이 남아있을 수 있다.)
Output: `SequenceBase.cs` 에 재사용 가능한 콜백 큐 인프라, `Custom/SystemHandler.cs` 에 시퀀스별 워밍업 배선 +
카운팅 게이트.
</objective>

<execution_context>
@$HOME/.claude/get-shit-done/workflows/execute-plan.md
@$HOME/.claude/get-shit-done/templates/summary.md
</execution_context>

<context>
@./CLAUDE.md

**배경 (이전 세션 결과물, 참고용 — 이번 수정과 직접 관련된 부분만):**
@.planning/quick/260814-warmup-transform-fix/260814-warmup-transform-fix-SUMMARY.md

**코딩 규칙 (이 프로젝트 상시 규칙):**
- 삼항연산자 `?:` 금지 → 반드시 `if / else`
- C# 7.2, .NET Framework 4.8 (8.0+ 문법 금지)
- 헝가리언 표기 — 로컬 `bool` 은 `b` 접두, `int` 는 `n` 접두 (신규 코드 한정)
- `SequenceBase.cs` 는 K&R 브레이스(여는 중괄호 같은 줄) — 그대로 유지. `Custom/SystemHandler.cs` 의 워밍업
  블록은 Allman 브레이스 — 그대로 유지. 파일별 기존 스타일 유지, 섞지 말 것.
- 신규 주석은 `260814 hbk quick-260814-warmup-thread-fix:` 접두, 비자명한 "왜"만 최소한으로

---

## 절대 건들면 안 되는 파일 (열지도 말 것)

| 파일 | 상태 | 지침 |
|------|------|------|
| `WPF_Example/DatumMeasurement.csproj` | 사용자의 별도 진행 중인 로컬 실험 | **절대 열지도, 건들지도 말 것.** 새 `.cs` 파일을 만들지 않으므로 이 파일을 편집할 필요 자체가 없다 |
| `WPF_Example/Custom/EthernetVision/PickerCenterCalibrationService.cs` | 사용자의 별도 진행 중인 로컬 실험 | **절대 열지도, 건들지도 말 것.** 이번 작업과 무관 |

baseline blob 해시 (`git hash-object <path>` 로 지금 시점 측정, 작업 후에도 동일해야 함 — 이 두 파일은 현재
git status 상 이미 사용자의 별도 미커밋 실험으로 modified 상태이며, 이번 작업이 그 상태를 조금이라도 더 바꾸면
안 된다는 뜻):
```
DatumMeasurement.csproj              : 761141c36ad80d58483248b8507c02e5ee0188a1
PickerCenterCalibrationService.cs    : 9f82579dc560e821b58d1d5f481639019adf52f3
```

`git add .` / `git add -A` / `git commit -a` 는 금지 — 반드시 수정한 2개 파일만 명시적으로 `git add`.

새 `.cs` 파일도 만들지 않는다 — csproj 는 classic-style(`<Compile Include>`)이라 편집이 필요해지는데, 이 파일은
절대 건들면 안 되는 파일이다.

<interfaces>
<!-- 실행자가 코드베이스를 탐색할 필요가 없도록 편집 대상 지점의 현재 코드와 교체할 코드를 그대로 옮겨둔다. -->

### 참고 — 이미 존재하는 관련 타입 (수정 안 함, 시그니처만 참고)

```csharp
// WPF_Example/Sequence/SequenceHandler.cs (SequenceHandler.this[int]/.Count — 이미 SystemHandler.cs 에서
// 동일 패턴 사용 중, 예: Custom/SystemHandler.cs MainRun() 의 `Sequences[i].PopResponse()`)
public int Count { get => Sequences.Count; }
public SequenceBase this[int index] { get { return Sequences.ElementAtOrDefault(index).Value; } }

// WPF_Example/Custom/Sequence/Inspection/ShotConfig.cs
public string OwnerSequenceName { get; set; } = ""; // 로드 시 비어있으면 SEQ_TOP("TOP")으로 폴백(SequenceHandler.cs)
// InspectionSequence(ESequence seqID, string name, ...) : base(seqID, name) 로 SequenceBase.Name 이 "TOP"/"SIDE"/
// "BOTTOM" (SEQ_TOP/SEQ_SIDE/SEQ_BOTTOM 상수) 로 설정됨 — ShotConfig.OwnerSequenceName 과 값이 정확히 대응.

// Custom/SystemHandler.cs 상단 using 목록에 이미 System.Threading, System.Threading.Tasks, ReringProject.Sequence
// 전부 포함돼 있음(라인 10~12) — 이번 수정에 새 using 추가 불필요.
```

### 1) `WPF_Example/Sequence/Sequence/SequenceBase.cs` — 콜백 큐 인프라 신규 추가

**현재 코드 (라인 67~72, `ResponseQueue` 필드 직후 ~ 생성자 직전):**
```csharp
        public ESequenceCommmand Command { get; protected set; }

        public TestPacket RequestPacket { get; private set; } = null;
        public ConcurrentQueue<TestResultPacket> ResponseQueue { get; private set; } = new ConcurrentQueue<TestResultPacket>();

        public SequenceBase(ESequence id, string name) {
```

**교체 후:**
```csharp
        public ESequenceCommmand Command { get; protected set; }

        public TestPacket RequestPacket { get; private set; } = null;
        public ConcurrentQueue<TestResultPacket> ResponseQueue { get; private set; } = new ConcurrentQueue<TestResultPacket>();

        //260814 hbk quick-260814-warmup-thread-fix: 이 시퀀스의 MainThread 위에서 외부 콜백을 실행하기 위한 큐.
        //  HALCON temp-mem 캐시는 스레드별로 관리되므로, "실제 검사가 도는 그 스레드"에서 실행돼야 의미 있는
        //  작업(예: 측정 파이프라인 워밍업)을 Task.Run(스레드풀) 대신 여기로 넘긴다. MainExecute 가 매
        //  iteration 드레인한다.
        public ConcurrentQueue<Action> CallbackQueue { get; private set; } = new ConcurrentQueue<Action>();

        public void EnqueueCallback(Action callback) {
            if (callback == null) return;
            CallbackQueue.Enqueue(callback);
        }

        public SequenceBase(ESequence id, string name) {
```

**현재 코드 (라인 246~276, `MainExecute()` 전체):**
```csharp
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
```

**교체 후 (드레인 호출 추가 + `DrainCallbackQueue` 신규 메서드 추가):**
```csharp
        private void MainExecute() {
            while(IsTerminated == false) {
                DrainCallbackQueue(); //260814 hbk quick-260814-warmup-thread-fix: Command/bCreated 상태 무관하게 매 iteration 드레인

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

        //260814 hbk quick-260814-warmup-thread-fix: CallbackQueue 에 쌓인 콜백을 이 스레드(MainThread) 위에서
        //  순서대로 실행한다. 콜백 예외는 이 시퀀스 스레드 자체를 죽이면 안 되므로 개별적으로 흡수한다.
        private void DrainCallbackQueue() {
            Action callback;
            while (CallbackQueue.TryDequeue(out callback)) {
                try {
                    callback();
                }
                catch (Exception ex) {
                    Logging.PrintErrLog((int)ELogType.Error,
                        string.Format("[MainExecute] CallbackQueue exception in sequence '{0}': {1}", Name, ex.Message));
                }
            }
        }
```

(`Action`, `ConcurrentQueue<T>` 모두 이미 파일 상단 `using System;` / `using System.Collections.Concurrent;` 로
임포트돼 있음 — 신규 using 불필요.)

### 2) `WPF_Example/Custom/SystemHandler.cs` — 시퀀스별 워밍업 배선

**현재 코드 (라인 364~365, 상수 2개):**
```csharp
        private const int MEASURE_WARMUP_ITERATIONS = 15; // 관측된 워밍업 문턱(7~36회+, 들쭉날쭉)의 중간값 근사치
        private const int MEASURE_WARMUP_SYNTHETIC_IMAGE_SIZE = 2048; // 저장 이미지가 전혀 없을 때만 쓰는 최후 폴백
```

**교체 후 (타임아웃 상수 추가):**
```csharp
        private const int MEASURE_WARMUP_ITERATIONS = 15; // 관측된 워밍업 문턱(7~36회+, 들쭉날쭉)의 중간값 근사치
        private const int MEASURE_WARMUP_SYNTHETIC_IMAGE_SIZE = 2048; // 저장 이미지가 전혀 없을 때만 쓰는 최후 폴백
        private const int MEASURE_WARMUP_TIMEOUT_MS = 30000; //260814 hbk quick-260814-warmup-thread-fix: fail-open 타임아웃 — 콜백이 끝내 안 돌아도 결국 게이트를 연다
```

**현재 코드 (라인 367~393, `StartMeasureWarmupAsync` 주석+본문 전체):**
```csharp
        //260814 hbk 워밍업 진입점 — UI 스레드를 블로킹하지 않도록 Task.Run 으로 던진다.
        //  Shot 이 하나도 없으면(레시피 없음/구형식) 즉시 게이트를 연다 — 영원히 막히면 안 된다.
        public void StartMeasureWarmupAsync()
        {
            bool bHasShots = Sequences != null && Sequences.RecipeManager != null && Sequences.RecipeManager.ShotCount > 0;
            if (!bHasShots)
            {
                Logging.PrintLog((int)ELogType.Trace, "[MeasureWarmup] 대상 Shot 없음 — 워밍업 스킵, 즉시 게이트 개방");
                IsMeasureWarmupComplete = true;
                return;
            }
            Task.Run(() =>
            {
                try
                {
                    RunMeasureWarmup();
                }
                catch (Exception ex)
                {
                    Logging.PrintLog((int)ELogType.Error, "[MeasureWarmup] 예외 — 워밍업 실패, 정상 기동 계속: {0}", ex.Message);
                }
                finally
                {
                    IsMeasureWarmupComplete = true;
                }
            });
        }
```

**교체 후 (핵심 변경 — Task.Run 으로 워밍업 로직을 직접 돌리는 대신, 각 시퀀스 스레드에 콜백을 넣고
카운트다운으로 완료를 추적한다):**
```csharp
        //260814 hbk quick-260814-dxy: Release 콜드스타트 measureExec(MeasurePos/MeasurePairs) 수 배~10배
        //  저하(.planning/debug/top-release-2x-slower.md) 원인 불명 임시 완화. 그 비용을 실제 검사 사이클이
        //  아니라 기동 시점에 미리 확정적으로 치르게 한다 — "완전 해결"이 아니라 "완화 시도"임을 명심할 것.
        //  레시피 로드 직후 MainWindow.Window_ContentRendered_LoadRecipe 가 호출한다.
        //260814 hbk quick-260814-warmup-thread-fix(root cause fix): HALCON temp-mem 캐시는 스레드별로 관리되는
        //  per-thread proc-handle 에 붙는다(MVTec 공식 "HALCON Memory Management" 기술노트). Task.Run(스레드풀
        //  임의 스레드)에서 워밍업을 돌리면 실제 검사가 도는 SequenceBase.MainThread(시퀀스별 전용 Thread)를
        //  전혀 데우지 못해 헛수고였다 — 대상 시퀀스 각각의 MainThread 위에서 워밍업 콜백이 실행되도록 바꾼다.
        //  등록된 모든 시퀀스(SequenceHandler — 이 PC 의 PcRole/CameraRole 로 이미 필터링된 실제 활성 시퀀스만)에
        //  콜백을 넣고, 전부 완료되거나 타임아웃(30s)되면 게이트를 연다(fail-open 유지).
        public void StartMeasureWarmupAsync()
        {
            bool bHasShots = Sequences != null && Sequences.RecipeManager != null && Sequences.RecipeManager.ShotCount > 0;
            if (!bHasShots)
            {
                Logging.PrintLog((int)ELogType.Trace, "[MeasureWarmup] 대상 Shot 없음 — 워밍업 스킵, 즉시 게이트 개방");
                IsMeasureWarmupComplete = true;
                return;
            }

            int nSequenceCount = Sequences.Count;
            if (nSequenceCount == 0)
            {
                Logging.PrintLog((int)ELogType.Trace, "[MeasureWarmup] 등록된 시퀀스 없음 — 워밍업 스킵, 즉시 게이트 개방");
                IsMeasureWarmupComplete = true;
                return;
            }

            int nPendingCount = nSequenceCount; //260814 hbk quick-260814-warmup-thread-fix: 클로저로 캡처되어 각 시퀀스 콜백/감시자가 공유하는 카운트다운
            for (int i = 0; i < nSequenceCount; i++)
            {
                SequenceBase targetSeq = Sequences[i];
                string sequenceName = targetSeq.Name; //260814 hbk iteration 별 로컬 캡처 — 클로저 버그 방지
                targetSeq.EnqueueCallback(() =>
                {
                    try
                    {
                        RunMeasureWarmup(sequenceName);
                    }
                    catch (Exception ex)
                    {
                        Logging.PrintLog((int)ELogType.Error, "[MeasureWarmup] 예외(seq={0}) — 워밍업 실패, 정상 진행: {1}", sequenceName, ex.Message);
                    }
                    finally
                    {
                        Interlocked.Decrement(ref nPendingCount);
                    }
                });
            }

            //260814 hbk quick-260814-warmup-thread-fix: 각 시퀀스 스레드 콜백이 다 돌 때까지 기다리는 감시자.
            //  이 감시자 자체는 폴링만 할 뿐 HALCON 코드를 건드리지 않으므로 스레드풀(Task.Run)에서 돌아도 무방하다.
            //  fail-open: 타임아웃이면 일부 미완료여도 게이트를 강제로 연다.
            Task.Run(() =>
            {
                int nWaitedMs = 0;
                const int POLL_INTERVAL_MS = 200;
                while (Volatile.Read(ref nPendingCount) > 0 && nWaitedMs < MEASURE_WARMUP_TIMEOUT_MS)
                {
                    Thread.Sleep(POLL_INTERVAL_MS);
                    nWaitedMs += POLL_INTERVAL_MS;
                }
                int nRemaining = Volatile.Read(ref nPendingCount);
                if (nRemaining > 0)
                {
                    Logging.PrintLog((int)ELogType.Error,
                        "[MeasureWarmup] 타임아웃({0}ms) — 시퀀스 {1}/{2} 워밍업 미완료, fail-open 게이트 개방",
                        MEASURE_WARMUP_TIMEOUT_MS, nRemaining, nSequenceCount);
                }
                IsMeasureWarmupComplete = true;
            });
        }
```

**현재 코드 (라인 395~464, `RunMeasureWarmup` 주석+본문 전체):**
```csharp
        //260814 hbk 대표 Shot 하나를 골라 그 FAI/Measurement 를 N회 반복 실행(TryExecuteMeasurement 와
        //  동일한 meas.TryExecute 호출 경로). EvaluateJudgement/ClearResult 는 호출하지 않는다 — 결과를
        //  완전히 버려서 실제 판정 로직/화면 표시에 어떤 영향도 주지 않는다.
        private void RunMeasureWarmup()
        {
            Stopwatch sw = Stopwatch.StartNew();
            HImage img = null;
            try
            {
                bool bIsSynthetic;
                ShotConfig shot = FindMeasureWarmupShot(out img, out bIsSynthetic);
                if (shot == null || img == null)
                {
                    Logging.PrintLog((int)ELogType.Trace, "[MeasureWarmup] 측정 항목 있는 Shot 없음 — 워밍업 스킵");
                    return;
                }

                //260814 hbk quick-260814-warmup-transform-fix(root cause fix): null 대신 identity HTuple 을
                //  넘긴다. EdgeToLineDistanceMeasurement.TryExecute 는 datumTransform==null 이면 진입부에서
                //  즉시 "Datum not found" 로 false 반환한다(HALCON measure_pos 자체가 호출 안 됨) — 이전
                //  quick-260814-dxy 코드의 "null=identity 로 처리되는 기존 관례" 가정은 VisionAlgorithmService.
                //  TryFitLine 내부에만 해당하고, 그 앞단의 이 가드를 놓쳤던 것이 근본원인이다. identity 를 쓰는
                //  이유: Point_Row/Col(ROI 정의)은 교시 시점 절대 이미지 좌표이고, datumTransform 은 그 위에
                //  얹는 "교시→현재 사이클" 미세 보정 델타일 뿐이다(DatumFindingService.TryFindTwoLineIntersect
                //  참고). 워밍업은 라이브 검출이 없어 그 델타를 알 수 없으므로, 프로덕션 ResolveDatumTransform
                //  이 "Fixture 미존재/미지정" 상황에 쓰는 것과 동일한 identity(무보정)로 대체한다 — 워밍업이
                //  재생하는 이미지가 SimulImagePath(=실제 검사에도 쓰이는 정적 이미지)라 무보정으로도 ROI 는
                //  교시된 실제 위치를 가리킨다.
                HTuple identityTransform;
                try
                {
                    HOperatorSet.HomMat2dIdentity(out identityTransform);
                }
                catch
                {
                    Logging.PrintLog((int)ELogType.Error, "[MeasureWarmup] identity transform 생성 실패 — 워밍업 스킵");
                    return;
                }

                int nSuccessCount = 0;
                int nFailCount = 0;
                int nSkipCount = 0; //260814 hbk quick-260814-warmup-transform-fix: Datum 참조는 있는데 한 번도
                                     //  검출 성공한 적 없는 측정 — identity 강제 실행 시 즉시실패만 반복하므로 skip.
                for (int i = 0; i < MEASURE_WARMUP_ITERATIONS; i++)
                {
                    foreach (FAIConfig fai in shot.FAIList)
                    {
                        foreach (MeasurementBase meas in fai.Measurements)
                        {
                            if (IsWarmupSkipTarget(meas))
                            {
                                nSkipCount++;
                                continue;
                            }
                            bool bOk = TryWarmupOneMeasurement(meas, img, identityTransform);
                            if (bOk) nSuccessCount++;
                            else nFailCount++;
                        }
                    }
                }

                Logging.PrintLog((int)ELogType.Trace,
                    "[MeasureWarmup] 완료 shot={0} iterations={1} synthetic={2} success={3} fail={4} skip={5} elapsed={6}ms",
                    shot.ShotName, MEASURE_WARMUP_ITERATIONS, bIsSynthetic, nSuccessCount, nFailCount, nSkipCount, sw.ElapsedMilliseconds);
            }
            finally
            {
                if (img != null) img.Dispose();
            }
        }
```

**교체 후 (`sequenceName` 파라미터 추가 + `FindMeasureWarmupShot` 호출/로그에 반영 — identity/skip 로직은 무변경):**
```csharp
        //260814 hbk 대표 Shot 하나를 골라 그 FAI/Measurement 를 N회 반복 실행(TryExecuteMeasurement 와
        //  동일한 meas.TryExecute 호출 경로). EvaluateJudgement/ClearResult 는 호출하지 않는다 — 결과를
        //  완전히 버려서 실제 판정 로직/화면 표시에 어떤 영향도 주지 않는다.
        //260814 hbk quick-260814-warmup-thread-fix: sequenceName 파라미터 추가 — 이 메서드는 이제 그 시퀀스의
        //  MainThread 콜백(EnqueueCallback)으로 호출되므로, FindMeasureWarmupShot 이 가능하면 그 시퀀스가
        //  실제로 소유한 Shot 을 골라 프로덕션과 최대한 가깝게 재현한다.
        private void RunMeasureWarmup(string sequenceName)
        {
            Stopwatch sw = Stopwatch.StartNew();
            HImage img = null;
            try
            {
                bool bIsSynthetic;
                ShotConfig shot = FindMeasureWarmupShot(sequenceName, out img, out bIsSynthetic);
                if (shot == null || img == null)
                {
                    Logging.PrintLog((int)ELogType.Trace, "[MeasureWarmup] seq={0} 측정 항목 있는 Shot 없음 — 워밍업 스킵", sequenceName);
                    return;
                }

                //260814 hbk quick-260814-warmup-transform-fix(root cause fix): null 대신 identity HTuple 을
                //  넘긴다. EdgeToLineDistanceMeasurement.TryExecute 는 datumTransform==null 이면 진입부에서
                //  즉시 "Datum not found" 로 false 반환한다(HALCON measure_pos 자체가 호출 안 됨) — 이전
                //  quick-260814-dxy 코드의 "null=identity 로 처리되는 기존 관례" 가정은 VisionAlgorithmService.
                //  TryFitLine 내부에만 해당하고, 그 앞단의 이 가드를 놓쳤던 것이 근본원인이다. identity 를 쓰는
                //  이유: Point_Row/Col(ROI 정의)은 교시 시점 절대 이미지 좌표이고, datumTransform 은 그 위에
                //  얹는 "교시→현재 사이클" 미세 보정 델타일 뿐이다(DatumFindingService.TryFindTwoLineIntersect
                //  참고). 워밍업은 라이브 검출이 없어 그 델타를 알 수 없으므로, 프로덕션 ResolveDatumTransform
                //  이 "Fixture 미존재/미지정" 상황에 쓰는 것과 동일한 identity(무보정)로 대체한다 — 워밍업이
                //  재생하는 이미지가 SimulImagePath(=실제 검사에도 쓰이는 정적 이미지)라 무보정으로도 ROI 는
                //  교시된 실제 위치를 가리킨다.
                HTuple identityTransform;
                try
                {
                    HOperatorSet.HomMat2dIdentity(out identityTransform);
                }
                catch
                {
                    Logging.PrintLog((int)ELogType.Error, "[MeasureWarmup] seq={0} identity transform 생성 실패 — 워밍업 스킵", sequenceName);
                    return;
                }

                int nSuccessCount = 0;
                int nFailCount = 0;
                int nSkipCount = 0; //260814 hbk quick-260814-warmup-transform-fix: Datum 참조는 있는데 한 번도
                                     //  검출 성공한 적 없는 측정 — identity 강제 실행 시 즉시실패만 반복하므로 skip.
                for (int i = 0; i < MEASURE_WARMUP_ITERATIONS; i++)
                {
                    foreach (FAIConfig fai in shot.FAIList)
                    {
                        foreach (MeasurementBase meas in fai.Measurements)
                        {
                            if (IsWarmupSkipTarget(meas))
                            {
                                nSkipCount++;
                                continue;
                            }
                            bool bOk = TryWarmupOneMeasurement(meas, img, identityTransform);
                            if (bOk) nSuccessCount++;
                            else nFailCount++;
                        }
                    }
                }

                Logging.PrintLog((int)ELogType.Trace,
                    "[MeasureWarmup] 완료 seq={0} shot={1} iterations={2} synthetic={3} success={4} fail={5} skip={6} elapsed={7}ms",
                    sequenceName, shot.ShotName, MEASURE_WARMUP_ITERATIONS, bIsSynthetic, nSuccessCount, nFailCount, nSkipCount, sw.ElapsedMilliseconds);
            }
            finally
            {
                if (img != null) img.Dispose();
            }
        }
```

(`IsWarmupSkipTarget`/`TryWarmupOneMeasurement` 두 메서드는 이 사이에 그대로 있고 — **손대지 않는다**, 이번
수정과 무관.)

**현재 코드 (라인 516~559, `FindMeasureWarmupShot` 주석+본문 전체):**
```csharp
        //260814 hbk 워밍업용 Shot+더미이미지 선택. 우선순위: (1) 측정 있는 Shot 중 SimulImagePath 파일이
        //  실존하는 첫 Shot(진짜 코드 경로 재현, 가장 신뢰도 높음) → (2) 그런 Shot 이 하나도 없으면 측정
        //  있는 첫 Shot + 합성 이미지(GenImageConst, 캐시 워밍 목적이라 검출 성공 여부 무관).
        private ShotConfig FindMeasureWarmupShot(out HImage img, out bool bIsSynthetic)
        {
            img = null;
            bIsSynthetic = false;
            ShotConfig shotWithMeasurements = null;

            foreach (ShotConfig shot in Sequences.RecipeManager.Shots)
            {
                bool bHasMeasurements = ShotHasAnyMeasurement(shot);
                if (!bHasMeasurements) continue;
                if (shotWithMeasurements == null) shotWithMeasurements = shot;

                bool bHasValidImage = !string.IsNullOrEmpty(shot.SimulImagePath) && File.Exists(shot.SimulImagePath);
                if (!bHasValidImage) continue;
                try
                {
                    img = new HImage(shot.SimulImagePath);
                    return shot;
                }
                catch
                {
                    img = null; // 이 Shot 은 포기, 다음 Shot 계속 탐색
                }
            }

            if (shotWithMeasurements == null) return null;

            try
            {
                HObject hobjConst;
                HOperatorSet.GenImageConst(out hobjConst, "byte", MEASURE_WARMUP_SYNTHETIC_IMAGE_SIZE, MEASURE_WARMUP_SYNTHETIC_IMAGE_SIZE);
                img = new HImage(hobjConst);
                bIsSynthetic = true;
                return shotWithMeasurements;
            }
            catch
            {
                img = null;
                return null;
            }
        }
```

**교체 후 (`sequenceName` 파라미터 추가, owner-일치 Shot 우선 선택):**
```csharp
        //260814 hbk 워밍업용 Shot+더미이미지 선택.
        //260814 hbk quick-260814-warmup-thread-fix: sequenceName 파라미터 추가. 우선순위:
        //  (1) sequenceName 소유 Shot 중 SimulImagePath 파일이 실존하는 첫 Shot(가장 신뢰도 높음, 그 시퀀스의
        //      실제 프로덕션 경로를 최대한 재현) → (2) 소유 Shot 이미지가 없으면 소유 여부 무관 아무 Shot 이나
        //      실제 이미지로 폴백(quick-260814-dxy 원래 동작, 완전 스킵보다 낫다) → (3) 그마저 없으면 합성
        //      이미지(GenImageConst, 소유 Shot 우선 → 아무 Shot). 측정 있는 Shot 자체가 하나도 없으면 null.
        private ShotConfig FindMeasureWarmupShot(string sequenceName, out HImage img, out bool bIsSynthetic)
        {
            img = null;
            bIsSynthetic = false;

            ShotConfig ownedShot = null;
            ShotConfig anyShot = null;

            foreach (ShotConfig shot in Sequences.RecipeManager.Shots)
            {
                if (!ShotHasAnyMeasurement(shot)) continue;
                if (anyShot == null) anyShot = shot;

                bool bIsOwned = string.Equals(shot.OwnerSequenceName, sequenceName, StringComparison.OrdinalIgnoreCase);
                if (!bIsOwned) continue;
                if (ownedShot == null) ownedShot = shot;

                bool bHasValidImage = !string.IsNullOrEmpty(shot.SimulImagePath) && File.Exists(shot.SimulImagePath);
                if (!bHasValidImage) continue;
                try
                {
                    img = new HImage(shot.SimulImagePath);
                    return shot;
                }
                catch
                {
                    img = null; // 이 Shot 은 포기, 다음 후보 계속 탐색
                }
            }

            foreach (ShotConfig shot in Sequences.RecipeManager.Shots)
            {
                if (!ShotHasAnyMeasurement(shot)) continue;
                bool bHasValidImage = !string.IsNullOrEmpty(shot.SimulImagePath) && File.Exists(shot.SimulImagePath);
                if (!bHasValidImage) continue;
                try
                {
                    img = new HImage(shot.SimulImagePath);
                    return shot;
                }
                catch
                {
                    img = null;
                }
            }

            ShotConfig fallbackShot = ownedShot;
            if (fallbackShot == null) fallbackShot = anyShot;
            if (fallbackShot == null) return null;

            try
            {
                HObject hobjConst;
                HOperatorSet.GenImageConst(out hobjConst, "byte", MEASURE_WARMUP_SYNTHETIC_IMAGE_SIZE, MEASURE_WARMUP_SYNTHETIC_IMAGE_SIZE);
                img = new HImage(hobjConst);
                bIsSynthetic = true;
                return fallbackShot;
            }
            catch
            {
                img = null;
                return null;
            }
        }
```

(`ShotHasAnyMeasurement` 는 이 바로 뒤에 이어짐 — **손대지 않는다**.)

빌드 환경(2026-08-14, 이전 계획과 동일):
- MSBuild: `C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe`
- Git Bash 에서는 `-p:` 대시 프리픽스를 쓴다(`//p:` 는 깨짐)
- 빌드에 1~2분 걸릴 수 있으니 Bash 툴 타임아웃을 300000 으로 준다
- 실행 중인 프로세스가 산출물을 잠그고 있으면(MSB3021/3026/3027/3030) **프로세스를 절대 죽이지 말 것**(프로젝트
  하드 규칙) — 스크래치 `-p:OutDir=<scratchpad>/build-verify/` 로 컴파일만 재검증하고 SUMMARY 에 기록
- Debug/x64 빌드 warning 기존 baseline = 정확히 12줄(`CS0618`×10 + `CS0162`×2). "0경고" 를 기준으로 삼지 말 것
  — 목표는 **신규 warning 0 / 신규 error 0**.
</interfaces>
</context>

<tasks>

<task type="auto">
  <name>Task 1: SequenceBase 에 시퀀스 전용 콜백 큐 인프라 추가</name>
  <files>WPF_Example/Sequence/Sequence/SequenceBase.cs</files>
  <action>
위 interfaces 블록의 "1) SequenceBase.cs" 섹션에 명시된 두 지점을 정확히 그대로 교체한다:
1. `ResponseQueue` 필드 직후에 `CallbackQueue`(`ConcurrentQueue&lt;Action&gt;`) 필드 + `EnqueueCallback(Action)`
   public 메서드 추가.
2. `MainExecute()` 루프 맨 앞에 `DrainCallbackQueue();` 호출 추가(Command/bCreated 상태와 무관하게 매 iteration
   실행 — `if (bCreated == false) { ... continue; }` 체크보다 앞에 위치), 그리고 `MainExecute()` 메서드 바로
   뒤에 `DrainCallbackQueue()` private 메서드 신규 추가(큐를 비우며 각 콜백을 개별 try/catch 로 감싸 예외가
   시퀀스 스레드를 죽이지 않도록 함).

이 파일의 나머지 부분(생성자, ExecuteAction, Start/Stop/Pause 등)은 전혀 건들지 않는다. K&R 브레이스 스타일
(여는 중괄호 같은 줄) 그대로 유지.
  </action>
  <verify>
    <automated>F=WPF_Example/Sequence/Sequence/SequenceBase.cs && echo "=== [1] CallbackQueue 필드 정의 : 1 기대 ===" && grep -c "public ConcurrentQueue<Action> CallbackQueue" "$F" && echo "=== [2] EnqueueCallback 메서드 : 1 기대 ===" && grep -c "public void EnqueueCallback(Action callback)" "$F" && echo "=== [3] DrainCallbackQueue 메서드 정의 : 1 기대 ===" && grep -c "private void DrainCallbackQueue" "$F" && echo "=== [4] MainExecute 루프 안 호출 : 1 기대 ===" && grep -c "DrainCallbackQueue();" "$F" && echo "=== [5] 금지 파일 무변경 ===" && git hash-object WPF_Example/DatumMeasurement.csproj && git hash-object WPF_Example/Custom/EthernetVision/PickerCenterCalibrationService.cs</automated>
  </verify>
  <done>
- [1]~[4] 전부 정확히 `1`.
- [5] 두 해시가 각각 `761141c36ad80d58483248b8507c02e5ee0188a1` / `9f82579dc560e821b58d1d5f481639019adf52f3`
  와 동일 (baseline 과 완전 일치, 이번 작업으로 변경 없음).
  </done>
</task>

<task type="auto">
  <name>Task 2: SystemHandler 워밍업을 시퀀스별 콜백 큐로 재배선 + 완료 카운팅 게이트</name>
  <files>WPF_Example/Custom/SystemHandler.cs</files>
  <action>
위 interfaces 블록의 "2) Custom/SystemHandler.cs" 섹션에 명시된 4개 지점을 정확히 그대로 교체한다(Task 1 이
먼저 완료돼 `SequenceBase.EnqueueCallback` 이 존재해야 이 Task 가 컴파일된다):
1. 상수 블록에 `MEASURE_WARMUP_TIMEOUT_MS = 30000` 추가.
2. `StartMeasureWarmupAsync()` 전체 교체 — `Task.Run(() => RunMeasureWarmup())` 단일 호출을, 등록된 각 시퀀스에
   대해 `targetSeq.EnqueueCallback(() => RunMeasureWarmup(sequenceName))` 로 넣는 반복문 + `Interlocked` 카운트다운
   + 타임아웃(30초) 감시자(`Task.Run` 유지 — 이 감시자는 폴링만 하고 HALCON 을 직접 안 건드리므로 스레드풀이어도
   무방)로 교체.
3. `RunMeasureWarmup()` → `RunMeasureWarmup(string sequenceName)` 로 시그니처 변경, 내부 `FindMeasureWarmupShot`
   호출과 로그 메시지에 `sequenceName` 반영. identity transform 생성/skip 판단/반복 실행 로직 자체는 절대 바꾸지
   않는다(quick-260814-warmup-transform-fix 가 이미 검증한 로직).
4. `FindMeasureWarmupShot(out HImage img, out bool bIsSynthetic)` → `FindMeasureWarmupShot(string sequenceName,
   out HImage img, out bool bIsSynthetic)` 로 시그니처 변경 — `sequenceName` 소유 Shot 우선, 없으면 기존처럼 아무
   Shot 이나 폴백(2-패스 구조, interfaces 블록의 "교체 후" 코드 그대로).

**절대 하지 말 것:**
- `WPF_Example/DatumMeasurement.csproj`, `WPF_Example/Custom/EthernetVision/PickerCenterCalibrationService.cs`
  — 열지도 말 것.
- 삼항연산자 금지, `EvaluateJudgement`/`ClearResult` 호출 금지(판정 로직 오염 방지 — 기존 방침 유지).
- 새 `.cs` 파일 생성 금지.
- `IsWarmupSkipTarget`/`TryWarmupOneMeasurement`/`ShotHasAnyMeasurement` 세 메서드 — 이번 수정과 무관, 손대지
  않는다.
- `DatumConfig`/`InspectionSequence`/`Action_FAIMeasurement`/`DatumFindingService` — 이 4개 파일도 열지도,
  수정하지도 않는다.
  </action>
  <verify>
    <automated>F=WPF_Example/Custom/SystemHandler.cs && echo "=== [1] 타임아웃 상수 : 1 기대 ===" && grep -c "MEASURE_WARMUP_TIMEOUT_MS = 30000" "$F" && echo "=== [2] RunMeasureWarmup(string sequenceName) 신규 시그니처 : 1 기대 ===" && grep -c "private void RunMeasureWarmup(string sequenceName)" "$F" && echo "=== [3] FindMeasureWarmupShot(string sequenceName, ...) 신규 시그니처 : 1 기대 ===" && grep -c "private ShotConfig FindMeasureWarmupShot(string sequenceName" "$F" && echo "=== [4] 시퀀스별 EnqueueCallback 호출 : 1 기대 ===" && grep -c "targetSeq.EnqueueCallback" "$F" && echo "=== [5] Interlocked 카운트다운 : 1 기대 ===" && grep -c "Interlocked.Decrement(ref nPendingCount)" "$F" && echo "=== [6] Volatile.Read 폴링 : 2 기대 ===" && grep -c "Volatile.Read(ref nPendingCount)" "$F" && echo "=== [7] 옛 무인자 RunMeasureWarmup 호출 완전 제거 : 0 기대 ===" && grep -c "RunMeasureWarmup();" "$F" && echo "=== [8] 옛 2-인자 FindMeasureWarmupShot 시그니처 제거 : 0 기대 ===" && grep -c "FindMeasureWarmupShot(out HImage img, out bool bIsSynthetic)" "$F" && echo "=== [9] EvaluateJudgement/ClearResult 미호출 유지(0 기대, 워밍업 블록 한정) ===" && awk '/private void RunMeasureWarmup/,/private bool ShotHasAnyMeasurement/' "$F" | grep -c "EvaluateJudgement\|ClearResult" && echo "=== [10] 금지 파일 무변경 ===" && git hash-object WPF_Example/DatumMeasurement.csproj && git hash-object WPF_Example/Custom/EthernetVision/PickerCenterCalibrationService.cs && echo "=== [11] Debug/x64 빌드 ===" && "/c/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/MSBuild.exe" "WPF_Example/DatumMeasurement.csproj" -p:Configuration=Debug -p:Platform=x64 -v:minimal -nologo 2>&1 | grep -iE "error CS|error MSB|warning CS|Build succeeded"</automated>
  </verify>
  <done>
- [1]~[6] 전부 정확히 기대값(`1`,`1`,`1`,`1`,`1`,`2`).
- [7]~[8] 전부 `0` — 옛 무인자/2-인자 시그니처가 파일 안에 더 이상 존재하지 않는다(완전 교체 확인).
- [9] `0` — 워밍업 블록(`RunMeasureWarmup` ~ `ShotHasAnyMeasurement` 시작 전까지, `IsWarmupSkipTarget`/
  `TryWarmupOneMeasurement`/`FindMeasureWarmupShot` 포함) 안에서 `EvaluateJudgement`/`ClearResult` 호출 없음
  (판정/화면 오염 없음 유지 확인 — Task 2 는 이 로직 자체를 바꾸지 않았으므로 회귀 없어야 함).
- [10] 두 해시가 각각 `761141c36ad80d58483248b8507c02e5ee0188a1` / `9f82579dc560e821b58d1d5f481639019adf52f3`
  와 동일 (baseline 과 완전 일치, 이번 작업으로 변경 없음).
- [11] `Build succeeded`, 신규 `error CS`/`error MSB` 0건, warning 은 기존 baseline 12줄(`CS0618`×10 +
  `CS0162`×2)과 정확히 동일(신규 warning 0). 산출물 잠김(MSB3021/3026/3027/3030)이면 프로세스를 죽이지 말고
  스크래치 `-p:OutDir=<scratchpad>/build-verify/` 컴파일 성공으로 대체하고 SUMMARY 에 기록.
  </done>
</task>

</tasks>

<threat_model>
## Trust Boundaries

이번 수정은 신규 트러스트 바운더리를 만들지 않는다 — 순수 내부 스레드 스케줄링 리팩토링(워밍업 콜백이
어느 스레드에서 도는지)이며, 신규 네트워크/파일/사용자 입력 표면이 없다. TCP(`$TEST` 등)/UI 게이트 체크
지점(`ProcessTest`, `Btn_start_Click`, `Btn_batchRun_Click`)은 이번 수정으로 전혀 건드리지 않는다(여전히
동일한 `IsMeasureWarmupComplete` 프로퍼티를 읽기만 한다).

## STRIDE Threat Register

| Threat ID | Category | Component | Disposition | Mitigation Plan |
|-----------|----------|-----------|-------------|-----------------|
| T-warmup-thread-01 | D (Denial of Service) | `SequenceBase.CallbackQueue`/`DrainCallbackQueue` | mitigate | 콜백 예외를 `DrainCallbackQueue` 자체 try/catch + 워밍업 콜백 자체 try/catch/finally 이중으로 흡수 — 워밍업 콜백 예외가 시퀀스 MainThread(실제 검사 스레드)를 죽이거나 무한정 멈추게 하지 않는다 |
| T-warmup-thread-02 | D (Denial of Service) | `StartMeasureWarmupAsync` 카운트다운 감시자 | mitigate | `MEASURE_WARMUP_TIMEOUT_MS`(30초) 타임아웃으로 fail-open — 시퀀스 부재/콜백 미실행 등 어떤 예외 상황에서도 `IsMeasureWarmupComplete` 가 결국 `true` 가 되어 TCP `$TEST`/UI RUN 이 영구 봉쇄되지 않는다 |
| T-warmup-thread-03 | T (Tampering, 경합) | `nPendingCount`(클로저 캡처 로컬) | accept | 다중 스레드(각 시퀀스 콜백 N개 + 감시자 1개)가 동시에 읽기/쓰기 — `Interlocked.Decrement`(쓰기)와 `Volatile.Read`(읽기)만 사용해 데이터 레이스 없이 정확한 가시성 보장, 별도 락 불필요 |

</threat_model>

<verification>
- Task 1/Task 2 의 `<verify>` grep 체크 전부 통과 + Debug/x64 빌드(또는 스크래치 OutDir 컴파일) 성공.
- 두 파일(`SequenceBase.cs`, `Custom/SystemHandler.cs`) 외 다른 파일은 git diff 상 전혀 나타나지 않아야 한다
  (`DatumMeasurement.csproj`/`PickerCenterCalibrationService.cs` 포함 — baseline 해시 그대로).
- **런타임 검증(실제 스레드 워밍업 효과)은 이 세션 범위 밖이다.** 앱을 재시작해:
  1. `D:\Data\Trace` 최신 로그에서 `[MeasureWarmup] 완료 seq=... shot=...` 라인이 등록된 시퀀스 수만큼(예:
     Side 전용 PC 라면 1줄, Top+Bottom PC 라면 2줄) 나오는지 확인 — 이전(스레드풀 단일 실행)과 달리 시퀀스별로
     각각 로그가 남아야 정상.
  2. 워밍업 완료 직후부터 실제 Top/Side/Bottom 검사 사이클(RUN 버튼 또는 TCP `$TEST`) 속도가 이전(3.5~5.1초)보다
     개선되는지 — **이건 사용자가 직접 확인해야 한다.** 스레드 문제를 고쳤어도 100% 개선을 보장하지 않는다(다른
     요인이 남아있을 수 있음) — 과장 금지, SUMMARY 에 정확히 "확인 필요/개선 정도"만 기록할 것.
</verification>

<success_criteria>
- `SystemHandler.StartMeasureWarmupAsync()` 가 더 이상 `Task.Run` 으로 워밍업 로직 자체를 실행하지 않고, 등록된
  각 시퀀스의 `MainThread`(콜백 큐 경유)에서 실행한다.
- `IsMeasureWarmupComplete` 게이트는 모든 대상 시퀀스의 워밍업 콜백 완료 또는 30초 타임아웃 중 먼저 오는 조건에
  열린다 — fail-open 원칙 100% 유지(어떤 예외/누락 상황에도 영구 봉쇄 없음).
- Debug/x64 빌드 성공, 신규 error/warning 0건.
- 금지 파일(`DatumMeasurement.csproj`, `PickerCenterCalibrationService.cs`) 무변경 유지.
- SUMMARY 에 "재시작 후 시퀀스별 `[MeasureWarmup]` 로그 개수 확인 + 실제 사이클 속도 개선 여부 확인은 사용자
  몫"이라는 문구가 명시적으로 남는다.
</success_criteria>

<output>
After completion, create `.planning/quick/260814-warmup-thread-fix/260814-warmup-thread-fix-SUMMARY.md`
</output>
