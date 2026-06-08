---
phase: 38-v1-1-carryover-cleanup-2026-05-28
plan: 03
type: execute
wave: 2
depends_on: ["38-01", "38-02"]
files_modified:
  - WPF_Example/SystemHandler.cs
autonomous: false

must_haves:
  truths:
    - "프로그램 시작 초기화 구간이 단계별로 계측되어 로그에 시간이 기록된다"
    - "시작 지연 원인이 1개 이상 식별되고 문서화된다"
    - "명백한 저위험 개선은 이번 phase 에 적용되고, 구조적/고위험 개선은 carry-over 로 명시된다"
    - "전체 phase 38 의 5개 성공기준이 UAT 로 확인된다"
  artifacts:
    - path: "WPF_Example/SystemHandler.cs"
      provides: "Initialize() 단계별 Stopwatch 계측 로그"
      contains: "Stopwatch"
    - path: ".planning/phases/38-v1-1-carryover-cleanup-2026-05-28/38-STARTUP-ANALYSIS.md"
      provides: "시작 지연 원인 식별 + 개선/carry-over 분류 문서"
      contains: "원인"
  key_links:
    - from: "SystemHandler.Initialize()"
      to: "Logging.PrintLog 단계별 경과시간"
      via: "Stopwatch 계측"
      pattern: "Stopwatch|ElapsedMilliseconds"
---

<objective>
v1.1 정리 항목 #11(프로그램 시작 지연 분석)을 구현하고, phase 38 전체(38-01 + 38-02 결과 포함)에 대한 UAT 를 수행한다. 시작 초기화 구간을 단계별로 계측해 지연 원인을 최소 1개 식별·문서화하고, 명백한 저위험 개선만 적용한다.

Purpose: 시작 지연의 정량적 근거를 확보(성공기준 #4)하고, v1.1 종결 전 전체 정리 항목의 회귀 0 을 사람이 확인한다.
Output: SystemHandler.Initialize 단계별 Stopwatch 계측 + 38-STARTUP-ANALYSIS.md 원인 문서 + 38-UAT.md sign-off.
</objective>

<execution_context>
@$HOME/.claude/get-shit-done/workflows/execute-plan.md
@$HOME/.claude/get-shit-done/templates/summary.md
</execution_context>

<context>
@.planning/PROJECT.md
@.planning/ROADMAP.md
@.planning/STATE.md
@.planning/phases/38-v1-1-carryover-cleanup-2026-05-28/38-CONTEXT.md

<interfaces>
<!-- 시작 초기화 진입점 (CLAUDE.md Entry Points + 코드 확인) -->

엔트리 체인: App.xaml.cs Application_Startup → MainWindow → SystemHandler.Handle.Initialize()

SystemHandler.cs:103-149 Initialize() — 8개 명시 단계 (계측 단위):
  1) Lights.Initialize()              (L104-109, 조명 컨트롤러 open — COM 포트, 지연 후보 #1)
  2) Sequences = SequenceHandler.Handle (L110-112)
  3) Server = new VisionServer()       (L114-116, TCP 서버)
     RawImageSaver.Start()             (L118-120, 저장 워커 스레드)
  4) mSystemThread.Start()             (L122-127, 시스템 루프 스레드)
  5) Login = LoginManager.Handle       (L129-130)
  6) Sequences.ExecOnCreate()          (L132-134, 시퀀스 생성 콜백 — 카메라/리소스 init, 지연 후보 #2)
     WireBufferLifecycle()             (L136-137)
  7) Recipes.CollectRecipe()           (L139-141, 레시피 디렉터리 스캔 — 디스크 I/O, 지연 후보 #3)
  8) Localize = ... (L143-145)
  Logging.PrintLog (L148)

기존 로깅: Logging.PrintLog((int)ELogType.Trace, "...") 패턴. ELogType using 이미 존재.
System.Diagnostics.Stopwatch — .NET Framework 4.8 표준, using System.Diagnostics 추가 가능.
</interfaces>
</context>

<tasks>

<task type="auto">
  <name>Task 1: Initialize() 단계별 Stopwatch 계측 + 저위험 개선 (#11, D-14)</name>
  <files>WPF_Example/SystemHandler.cs</files>
  <read_first>
    - WPF_Example/SystemHandler.cs:103-149 (Initialize 8단계 전체)
    - WPF_Example/App.xaml.cs (Application_Startup → MainWindow 생성 타이밍 — 계측 범위 상한 확인)
    - WPF_Example/MainWindow.xaml.cs 의 Initialize() 호출 지점 (Loaded vs ctor — 계측 시작점 확인)
  </read_first>
  <action>
    (a) 계측: SystemHandler.Initialize() (L103) 진입부에 `System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();` 를 추가하고, 8개 단계 각각의 끝에서 경과시간을 로그한다. 단계 사이 누적 경과를 찍는 방식(각 단계 종료 시 `Logging.PrintLog((int)ELogType.Trace, "[STARTUP] Step N {name}: {ms} ms (cumulative)", sw.ElapsedMilliseconds);`)으로, 어느 단계가 지연 주범인지 delta 가 드러나게 한다. 직전 단계 시각을 변수(long prev)로 보관해 단계별 delta 도 함께 로그하면 더 명확하다.
    각 계측 라인에 //260528 hbk Phase 38 #11 마커 부착. 단계 로직 자체(Lights.Initialize 등 호출 순서/인자)는 이 (a) 에서 변경하지 않는다 — 계측 래핑만.

    (b) 저위험 개선(D-14): (a) 계측 후 실데이터 로그를 근거로 명백한 저위험 개선만 적용한다. 다음 유형만 허용:
        - 중복 초기화 제거(같은 객체를 두 번 init 하는 게 grep/로그로 확인되는 경우)
        - 명백히 lazy load 가능한 비필수 리소스 지연(첫 사용 시점으로 미룸)
        고위험/구조적 변경(스레드 모델 변경, 카메라 init 순서 재배치, 비동기화 등)은 적용하지 말고 38-STARTUP-ANALYSIS.md 에 carry-over 로 명시한다.
        주의: 이 Task 의 (b) 는 계측 결과에 따라 "적용할 저위험 개선 없음 — 전부 carry-over" 가 정당한 결론일 수 있다. 회귀 위험 0 이 우선이며, 확신이 없으면 개선을 적용하지 않고 carry-over 로 문서화한다.

    msbuild Debug/x64 가 0 신규 warning 으로 빌드되어야 한다(using System.Diagnostics 추가 시 unused 경고 없도록).
  </action>
  <verify>
    <automated>msbuild WPF_Example/DatumMeasurement.csproj /p:Configuration=Debug /p:Platform=x64 — exit 0, 신규 warning 0; Grep "STARTUP" WPF_Example/SystemHandler.cs — 단계별 계측 로그 ≥4 매치</automated>
  </verify>
  <acceptance_criteria>
    - SystemHandler.Initialize() 에 `Stopwatch` 또는 `ElapsedMilliseconds` 사용 계측 코드 존재
    - 8단계 중 최소 4개 단계에 `[STARTUP]` 경과시간 로그가 부착됨 (grep "[STARTUP]" ≥4)
    - 단계 호출 순서/인자는 계측 외 변경 없음 (저위험 개선 미적용 시) 또는 적용된 개선이 중복제거/lazy-load 유형으로 한정
    - msbuild Debug/x64 exit 0, 신규 warning 0
  </acceptance_criteria>
  <done>Initialize 단계별 계측 로그 부착, 저위험 개선 적용 또는 carry-over 분류 완료, 빌드 PASS</done>
</task>

<task type="checkpoint:human-verify" gate="blocking">
  <name>Task 2: 시작 지연 측정 + 38-STARTUP-ANALYSIS.md 작성 + phase 38 UAT</name>
  <files>.planning/phases/38-v1-1-carryover-cleanup-2026-05-28/38-STARTUP-ANALYSIS.md, .planning/phases/38-v1-1-carryover-cleanup-2026-05-28/38-UAT.md</files>
  <read_first>
    - .planning/phases/38-v1-1-carryover-cleanup-2026-05-28/38-CONTEXT.md (성공기준 + D-14)
    - .planning/phases/38-v1-1-carryover-cleanup-2026-05-28/38-01-SUMMARY.md (38-01 결과)
    - .planning/phases/38-v1-1-carryover-cleanup-2026-05-28/38-02-SUMMARY.md (38-02 결과)
  </read_first>
  <action>
    이 Task 는 checkpoint:human-verify 다 — Task 1 의 계측 코드를 사람이 SIMUL_MODE 로 실행해 시작 지연을 측정하고, phase 38 전체(38-01/02/03) 의 5개 성공기준을 육안 확인한다.
    executor 가 수행할 자동 작업: 사람이 보고한 [STARTUP] 측정값을 받아 38-STARTUP-ANALYSIS.md 를 작성(지연 원인 ≥1개 + 적용한 저위험 개선 + carry-over 항목 분류)하고, UAT 결과를 38-UAT.md 로 sign-off 한다. 아래 <how-to-verify> 의 6항목을 사람에게 제시하고 PASS/FAIL 을 수집한다.
  </action>
  <what-built>
    - 38-01: MeasurementFactory GetTypeNames 미사용 5종 숨김 + PixelResolution 카메라 단일화 마이그레이션
    - 38-02: AngleTolerance 기본 OFF + TwoLineAngleToleranceDeg PropertyGrid 숨김 + ReuseFromShotName 제거 + 주석 정리
    - 38-03 Task 1: SystemHandler.Initialize 단계별 Stopwatch 계측 로그
  </what-built>
  <how-to-verify>
    실행은 SIMUL_MODE(Debug/x64) 로 한다. 카메라 하드웨어 없이도 검증 가능한 항목 위주.

    1. 시작 지연 (성공기준 #4):
       - DatumMeasurement.exe 를 실행하고 Trace 로그에서 `[STARTUP] Step N ...: {ms} ms` 라인들을 확인한다.
       - 가장 큰 delta 를 보이는 단계(지연 주범 1개 이상)를 기록한다.
       - executor 가 이 측정값을 받아 `.planning/phases/38-v1-1-carryover-cleanup-2026-05-28/38-STARTUP-ANALYSIS.md` 를 작성한다(원인 ≥1개 + 적용한 저위험 개선 + carry-over 항목).

    2. #1 측정타입 정리 (성공기준 #1):
       - FAI 노드에서 Measurement 추가 → Type ComboBox 에 EdgePairDistance/PointToLineDistance/PointToPointDistance/LineToLineAngle/LineToLineDistance 가 보이지 않음을 확인.
       - EdgeToLineAngle, CircleDiameter 는 여전히 보임을 확인.

    3. #6 각도 UI (성공기준 #2):
       - Datum 노드(DualImage 알고리즘) 새로 추가 시 각도 NG 배지가 기본으로 뜨지 않음(AngleTolerance=0.0 OFF) 확인.
       - PropertyGrid 에서 Datum 노드 선택 시 TwoLineAngleToleranceDeg 항목이 보이지 않음을 확인.
       - TwoLineIntersect datum 의 직각 게이트(잘못된 직각 datum 거부)는 여전히 동작함을 확인.

    4. INI 하위호환 (성공기준 #5):
       - 기존(Phase 6 포맷) 레시피 INI 를 로드 → 파싱 오류 없이 정상 로드되는지 확인.
       - (가능하면) ReuseFromShotName / AngleTolerance / PixelResolutionX/Y 키를 가진 구 레시피로 로드 무오류 확인.

    5. #5 픽셀분해능 (성공기준 #3):
       - 레시피 로드 후 측정 실행 시 mm 값이 카메라 단일값 기준으로 계산되는지 확인.
       - 정규화 전후 mm 변화가 있으면 의도적 보정(D-10)으로 38-STARTUP-ANALYSIS.md 또는 SUMMARY 에 기록되었는지 확인.

    6. 빌드: msbuild Debug/x64 PASS, 신규 warning 0 (자동).
  </how-to-verify>
  <verify>
    <automated>msbuild WPF_Example/DatumMeasurement.csproj /p:Configuration=Debug /p:Platform=x64 — exit 0, 신규 warning 0 (사람 육안 UAT 6항목은 수동)</automated>
  </verify>
  <acceptance_criteria>
    - 38-STARTUP-ANALYSIS.md 에 시작 지연 원인 ≥1개 식별 + 개선/carry-over 분류 존재
    - 38-UAT.md 에 6 UAT 항목 결과 기록 + sign-off 상태
    - UAT 6항목 모두 PASS (또는 FAIL 항목이 carry-over 로 명시)
  </acceptance_criteria>
  <done>시작 지연 원인 문서화 + phase 38 5개 성공기준 UAT 확인 + 38-UAT.md sign-off</done>
  <resume-signal>각 항목 PASS/FAIL 을 알려주세요. 모두 PASS 면 "approved" — 38-UAT.md sign-off 처리 + 38-STARTUP-ANALYSIS.md 확정. FAIL 있으면 항목별 증상 기술 → 수정 또는 carry-over.</resume-signal>
</task>

</tasks>

<threat_model>
## Trust Boundaries

| Boundary | Description |
|----------|-------------|
| (없음) | 이 plan 은 계측 로그 추가 + 문서/UAT 만 — 신규 입력 경계 도입 없음 |

## STRIDE Threat Register

오프라인 Windows 산업용 데스크톱 앱 — 신규 외부/네트워크 인터페이스 없음. auth/injection 위협 표면 없음(명시).

| Threat ID | Category | Component | Disposition | Mitigation Plan |
|-----------|----------|-----------|-------------|-----------------|
| T-38-05 | Information Disclosure | #11 [STARTUP] 계측 로그 | accept | Trace 로그에 경과시간(ms)만 기록 — 민감정보 없음. 로컬 로그 파일, 외부 전송 없음. |
</threat_model>

<verification>
- msbuild Debug/x64 PASS, 신규 warning 0 (성공기준 #1)
- [STARTUP] 단계별 계측 로그 부착 (grep)
- 38-STARTUP-ANALYSIS.md 에 지연 원인 ≥1개 식별 + 개선/carry-over 분류 (성공기준 #4)
- UAT 6항목 사람 확인 (성공기준 #1~#5 전체 커버)
</verification>

<success_criteria>
- 시작 지연 원인 1개 이상 식별(개선 또는 carry-over 명시) (Success Criteria #4)
- phase 38 전체 5개 성공기준이 UAT 로 확인됨
- INI 하위호환 유지 (Success Criteria #5)
</success_criteria>

<output>
완료 후 생성:
- `.planning/phases/38-v1-1-carryover-cleanup-2026-05-28/38-03-SUMMARY.md`
- `.planning/phases/38-v1-1-carryover-cleanup-2026-05-28/38-STARTUP-ANALYSIS.md` (지연 원인 + 개선/carry-over)
- `.planning/phases/38-v1-1-carryover-cleanup-2026-05-28/38-UAT.md` (sign-off)
</output>
