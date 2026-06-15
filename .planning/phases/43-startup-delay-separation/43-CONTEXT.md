# Phase 43: 시작지연 분리 (LoginManager + SequenceHandler) - Context

**Gathered:** 2026-06-15
**Status:** Ready for planning

<domain>
## Phase Boundary

앱 기동 시 동기적으로 수행되는 무거운 초기화를 지연/분리하여 **"측정 가능 시점"까지의 시간을 ≥30% 단축**한다. 대상은 `SystemHandler.Initialize()`의 Step 5 LoginManager(808ms delta)와 Step 2 SequenceHandler(550ms delta). Phase 38이 추가한 `[STARTUP]` Stopwatch 계측(SystemHandler.cs:110~176, Step1~8 + Total)을 기준선으로 사용한다.

**고정 범위(ROADMAP CO-38-02/03):** 기존 LoginManager 인증 동작/계정 모델 변경 없음. OAuth/인증 고도화 OUT-OF-SCOPE. 실HW [STARTUP] 재측정은 Phase 44(HW 의존). 본 phase는 SIMUL 기준 검증.

</domain>

<analysis>
## 핵심 분석 (논의 진입 전 공유)

- **808ms(LoginManager) / 550ms(SequenceHandler) delta는 실제 데이터 작업이 아니라 첫-접근 JIT/crypto warmup 의혹이 큼.** LoginManager.Load()는 작은 `account.db`(AES decrypt + Newtonsoft.Json deserialize)만 읽음 → 808ms는 RijndaelManaged/JSON 첫 JIT 비용 추정. SequenceHandler Step 2(550ms)는 레시피 데이터 로딩이 아니라 `SequenceHandler.Handle` 생성자(RegisterSequences/RegisterActions/InitializeSequences)의 첫-접근 JIT.
- **그래서 lazy-load 는 비용을 "제거"하는 게 아니라 "측정 가능 경로" 밖으로 이동**시키는 것. → '측정 가능 시점' 정의가 30% 목표의 핵심.
- **임계 경로 구분:** 첫 `$TEST` 수용·검사에는 SystemThread(Step 4) + 시퀀스 ExecOnCreate(Step 6) + recipe 가 필요. **LoginManager(Step 5)는 측정 임계 경로 밖**(login은 운영자/UI 행위) → 순수 이동 이득. **SequenceHandler는 측정 임계 경로의 일부**라 밖으로 옮길 수 없음.
- 현재 순서상 LoginManager(Step 5, 808ms)가 Step 6 ExecOnCreate / Step 7 CollectRecipe **앞에** 위치 → 백그라운드로 빼면 후속 측정 준비 단계가 ~808ms 앞당겨짐.

</analysis>

<decisions>
## Implementation Decisions

### "측정 가능 시점" 정의 (30% 단축 계측 기준점) — GA-1
- **D-01:** 30% 단축의 계측 기준점 = **첫 `$TEST` 수용 가능 시점**(recipe 로드 완료 + 시퀀스 thread alive → 핸들러가 TEST 보내면 검사 가능). Initialize() 반환 시점이나 UI 표시 시점이 아님.
- **D-02:** 그 시점에 `[STARTUP] READY` 마커를 신규 추가하고, Before/After 비교의 단일 기준 지표로 삼는다. (정확한 삽입 위치 = recipe ready + seq thread alive 가 모두 충족되는 지점. planner/research 가 코드상 위치 확정.)

### LoginManager lazy-load 방식 — GA-2
- **D-03:** **기동 직후 백그라운드 프리로드.** `Initialize()`에서 `LoginManager.Handle` 동기 접근(Step 5)을 제거하고, 시스템 thread 기동 후 백그라운드 Task로 LoginManager를 프리로드한다.
- **D-04:** 측정 임계 경로에서 808ms 제거 + 첫 로그인 지연 0(프리로드가 로그인 다이얼로그보다 먼저 완료 목표). account.db 는 작고 단일 read 라 백그라운드 thread-safe (로그인 UI는 프리로드 완료를 대기/확인).
- **D-05:** lazy-on-first-login(첫 로그인에 ~800ms 1회 노출), UI idle 로드(UI thread 점유 우려)는 기각.

### SequenceHandler 비동기 분리 전략 — GA-3
- **D-06:** **SequenceHandler는 측정 임계 경로라 밖으로 옮기지 않는다.** 첫 TEST 전 준비 필수. 본 phase의 30% 단축은 주로 **LoginManager 이동**으로 달성.
- **D-07:** CO-38-03("SequenceHandler 동기 의존성 제거")의 해석 = 시퀀스 자체가 아니라 **시퀀스 준비에 불필요하면서 동기적으로 끼어 있는 의존성**만 분리 검토. SequenceHandler 구간은 계측 유지하고, 안전한 범위에서만 손댄다.
- **D-08:** 독립 init 병렬화(option B)와 JIT 프리워밍(option C)은 본 phase에서 채택하지 않음(race 복잡도 ↑ / 효과 불확실). 필요 시 후속 phase 후보로 deferred.

### 검증 방법 + 회귀 안전 범위 — GA-4
- **D-09:** 30% 입증 = `[STARTUP]` 로그 **3~5회 평균**으로 JIT 첫-실행 편차 흡수, `READY` 마커 Before/After 비교. 1회 측정 기각.
- **D-10:** 회귀 필수 확인(SIMUL): (a) 첫 로그인 흐름 정상(백그라운드 프리로드 미완 상태에서 로그인 시도해도 정상), (b) 첫 `$TEST` 검사 정상(readiness race 없음 — recipe/seq 미준비 상태에서 TEST 수신 시 안전 처리).

### Claude's Discretion
- `[STARTUP] READY` 마커의 정확한 코드 삽입 위치 (D-02 정의를 만족하는 지점).
- 백그라운드 프리로드 구현 디테일(Task vs Thread, 완료 신호 방식) — D-03/D-04 계약을 만족하는 한 planner/executor 재량.
- 로그인 UI가 프리로드 완료를 대기/확인하는 구체적 동기화 메커니즘.

</decisions>

<specifics>
## Specific Ideas

- 측정 기준점을 산업 비전 서버의 실질 의미("핸들러가 TEST 보내면 검사됨")에 맞춘다 — 사용자 명시 선호(GA-1).
- Phase 38의 `[STARTUP]` 계측 라인 형식(`Step N ...: {cumulative} ms, delta {delta} ms`)을 유지/확장한다.

</specifics>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### 시작지연 계측/대상 코드
- `WPF_Example/SystemHandler.cs` §Initialize() (L109~177) — `[STARTUP]` Step1~8 + Total 계측. Step 2 SequenceHandler(L124), Step 5 LoginManager(L150)가 본 phase 핵심 대상. READY 마커 삽입 후보 구간.
- `WPF_Example/Login/LoginManager.cs` — 싱글턴 `Handle`(L88), 생성자(L97~103) → `Load()`(L164~) account.db AES decrypt + JSON deserialize. 백그라운드 프리로드 대상.
- `WPF_Example/Sequence/SequenceHandler.cs` §생성자(L47~54) — RegisterSequences/RegisterActions/InitializeSequences. 측정 임계 경로(이동 금지).
- `WPF_Example/Custom/Sequence/SequenceHandler.cs` §InitializeSequences(L76~93) — 역할 활성 시퀀스 생성/등록.

### 요구사항 출처
- `.planning/REQUIREMENTS.md` — CO-38-02(LoginManager 분리), CO-38-03(SequenceHandler 분리), CO-38-04(실HW 재측정 = Phase 44).
- Phase 38 progress(메모리 `project_phase38_progress`) — SIMUL 1회 실측 Total 1509ms / LoginManager 808ms / SequenceHandler 550ms ≈ 90%.

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- Phase 38 `[STARTUP]` Stopwatch 계측 인프라(SystemHandler.cs) — READY 마커만 추가하면 Before/After 비교 그대로 활용.
- `Logging.PrintLog((int)ELogType.Trace, ...)` — 계측 로그 출력 경로 확립됨.

### Established Patterns
- 싱글턴 `Handle` getter 패턴(LoginManager/SequenceHandler) — 첫 접근 시 생성자 실행. 백그라운드 프리로드는 이 첫 접근을 별도 thread에서 트리거하는 형태.
- 백그라운드 워커 선례: `RawImageSaveService.Start()`(Step 3), `CaptureImageSaveService.Start()`(Phase 40.2) — volatile flag + thread 패턴 참고 가능.
- `mSystemThread`(Step 4, ThreadPriority.Highest)가 SystemProcess→MainRun 루프 — TEST 처리 주체. READY 판정의 한 축.

### Integration Points
- `SystemHandler.Initialize()` Step 순서 재배치(LoginManager 동기 접근 제거 → 백그라운드).
- 로그인 UI 진입점(MainWindow/로그인 다이얼로그)이 프리로드 완료를 대기/확인.
- 첫 `$TEST` 수신 경로(MainRun 디스패치)가 recipe/seq readiness를 안전 처리(미준비 시 거부 또는 대기).

</code_context>

<deferred>
## Deferred Ideas

- **독립 init 병렬화 + readiness barrier** (GA-3 option B) — Lights/Server/RawImageSaver 등을 SequenceHandler와 병렬 스레드로 돌려 wall-clock 추가 단축. race 복잡도 ↑ → 효과 입증되면 후속 phase 후보.
- **시퀀스/Halcon 스택 JIT 프리워밍** (GA-3 option C) — 효과 불확실, 보수적 보류.
- **실HW [STARTUP] 재측정** — Phase 44 (CO-38-04, HW 도착 의존).

</deferred>
