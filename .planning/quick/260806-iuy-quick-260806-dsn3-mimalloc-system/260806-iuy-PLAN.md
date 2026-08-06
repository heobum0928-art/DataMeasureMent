---
phase: quick-260806-iuy
plan: 01
type: execute
wave: 1
depends_on: []
files_modified:
  - WPF_Example/SystemHandler.cs
autonomous: false
requirements: [BATCH-MEM-03]

must_haves:
  truths:
    - "앱 시작 시 HALCON 내부 힙 할당자가 Windows 기본값(mimalloc)이 아니라 Win32 기본 힙(system)으로 설정된다"
    - "할당자 전환 호출이 기존 캐시 3줄보다 먼저 실행된다(같은 try 블록의 첫 실행문)"
    - "기존 캐시 3줄(global_mem_cache/temporary_mem_cache/image_cache_capacity)과 catch 블록은 글자 하나도 바뀌지 않는다(삭제 라인 0)"
    - "이번 변경의 diff는 WPF_Example/SystemHandler.cs 단 1개 파일에만 존재한다"
    - "앱이 정상 기동하고 Error 로그에 '[STARTUP] HALCON SetSystem memory cache config failed' 가 남지 않는다(남으면 새 줄이 던져서 기존 3줄까지 함께 무력화된 것)"
    - "배치 검사 사이클 정리 후 Process.WorkingSet64 가 실제로 감소하고, 반복 배치에서 34~41GB로 누적되지 않는다"
    - "측정값/판정 결과가 변경 전과 동일하다(할당자 교체는 수치에 영향 없음)"
    - "반복 배치 중 halcon.DLL 크래시가 재현되지 않는다"
  artifacts:
    - path: "WPF_Example/SystemHandler.cs"
      provides: "Initialize() 시작 지점에서 HALCON 힙 할당자를 mimalloc → system 으로 전환하는 1줄(기존 캐시 설정 try 블록 내부 최상단)"
      contains: "memory_allocator"
  key_links:
    - from: "SystemHandler.Initialize() 진입"
      to: "HALCON 내부 힙 할당자(mimalloc → Win32 HeapAlloc)"
      via: "기존 캐시 설정 try 블록의 첫 실행문으로 HOperatorSet.SetSystem 호출"
      pattern: "HOperatorSet\\.SetSystem\\(\"memory_allocator\", \"system\"\\)"
    - from: "memory_allocator 설정 실패(예외)"
      to: "Error 로그 '[STARTUP] HALCON SetSystem memory cache config failed'"
      via: "기존 catch 블록(무변경) — 실패 시 기존 캐시 3줄도 함께 건너뛰게 되므로 이 로그의 부재가 검증 항목이다"
      pattern: "HALCON SetSystem memory cache config failed"
---

<objective>
배치검사 메모리 미반환(34~41GB 폭증 + halcon.DLL 크래시)의 마지막 남은 근본원인을 제거한다: HALCON 이 Windows 에서 기본으로 쓰는 내부 힙 할당자 **mimalloc** 을 **`system`(Win32 기본 힙)** 으로 전환한다.

**왜 이게 남은 원인인가 (이번 세션 확정):**
- 260805-mze/mzf/mzh/ojq, 260806-dsn/dsn-2 로 애플리케이션 레벨 원인(큐 백프레셔·패턴모델 캐시·HObject 누수·저장큐 레이스 재시도 대기열)은 전부 수정 완료됐고, **로그로 정리 로직이 정상 동작함이 확인**됐다(`[BatchImageCleanup] 재시도 성공` 이 SHOT 마다 출력됨). 그런데도 실기 배치에서 `Process.WorkingSet64` 가 16.7GB+ 에서 내려오지 않는다 → 원인이 앱 레이어보다 **아래**에 있다.
- 260806-dsn Part A 가 이미 HALCON 자체 캐시 3종(`global_mem_cache`/`temporary_mem_cache`/`image_cache_capacity`)을 idle/0 으로 껐다 → 그것도 아니다. 남은 건 그 아래 계층인 **native 할당자**.
- HALCON 24.11 공식 문서 `memory_management` 챕터 4 "Handling Suspected Memory Leaks in HALCON" 이 정확히 이 순서를 권고한다 — 캐시 3줄을 껐는데도 해결이 안 되면: *"Switch off mimalloc (under Windows). mimalloc tends to cache memory more aggressively than the Win32 default heap allocator. Therefore, switching to the default allocator can help resolve memory related problems in some cases: `set_system('memory_allocator', 'system')`"*. (`set_system` 레퍼런스 확인: `'memory_allocator'` 는 실재하는 파라미터이고 값 `'system'`/`'mimalloc'` 중 Windows 기본값이 `'mimalloc'`.)
- 격리 하네스 실측(리포지토리 밖, 이번 plan 범위 아님): 실제 생산 SHOT 이미지 규모인 13376x9528 mono8 HImage(≈121.5MB)를 15회 생성/Dispose 반복 —
  - **mimalloc(현재 기본값)**: 1~7회차는 Dispose 후 ~30MB 로 정상 반환되다가 **8회차부터 WorkingSet64 가 ~152MB 에 영구 고착**, 이후 끝까지 안 내려감. `GC.Collect()` + `WaitForPendingFinalizers()` 를 넣어도 효과 0 → .NET GC/finalizer 문제가 아니라 **native 할당자 문제**임이 확정.
  - **`memory_allocator='system'`**: 15회 전부 Dispose 직후 ~30MB 로 정상 반환. 최종 WorkingSet 30.0MB vs mimalloc 152.1MB.

Purpose: "Dispose 는 되는데 OS 메모리가 안 돌아온다"(project_batch_memory_never_shrinks_260806 메모리에 미해결로 기록된 상태)의 종결.
Output: `WPF_Example/SystemHandler.cs` **1줄 추가**(+ 그 위 한국어 "왜" 주석). Debug/x64 빌드 PASS. 실기 배치 재현 시나리오로 checkpoint 검증.

**범위 밖(건드리지 않음):** 다른 모든 파일, 기존 catch 블록, 기존 캐시 3줄, 기존 주석 블록(115~119행), `GetSystem("memory_allocators_supported")` 같은 진단 코드 추가, 신규 try/catch, 로깅 추가. 순수 1줄 추가다.
</objective>

<execution_context>
@$HOME/.claude/get-shit-done/workflows/execute-plan.md
@$HOME/.claude/get-shit-done/templates/summary.md
</execution_context>

<context>
@.planning/STATE.md
@./CLAUDE.md
@.planning/quick/260806-dsn-overlay-window-reuse/260806-dsn-CONTEXT.md
@.planning/quick/260806-dsn-overlay-window-reuse/260806-dsn-2-PLAN.md

<style_rules>
프로젝트 규칙 (예외 없음):
- 삼항 연산자 `?:` 절대 금지 → if-else 만 사용. (이번 변경엔 분기 자체가 없다.)
- 날짜 프리픽스 주석(`//YYMMDD hbk`) 정책은 폐기됨 — 새 주석은 `// quick-260806-dsn3: ...` 형태로 출처만 짧게 표기하고 비자명한 "왜"를 한국어로 설명한다. **기존 주석은 재작성 금지**(115~119행 `quick-260806-dsn Part A:` 블록은 한 글자도 건드리지 않는다).
- C# 7.2 / .NET Framework 4.8. 이번 변경은 기존 `HOperatorSet.SetSystem(string, string)` 호출 1개 추가일 뿐이므로 신규 언어기능/using 추가 없음(`HalconDotNet` 은 이 파일 7행에 이미 있음).
- 브레이스 스타일: `SystemHandler.cs` 는 **K&R**(여는 브레이스 같은 줄) — 이번엔 블록 자체를 만들지 않으므로 해당 없음.
- 들여쓰기: `try {` 는 12칸, try 내부 실행문은 **16칸**(기존 121~123행과 정확히 동일하게 맞출 것).
- 한 줄에 한 문장.
</style_rules>

<interfaces>
<!-- 코드베이스/HALCON 문서에서 확인된 기존 계약 — 그대로 사용, 추가 탐색 불필요. -->

From HalconDotNet (기존, 이 파일에서 이미 3회 사용 중):
```csharp
public static void SetSystem(HTuple systemParameter, HTuple value); // string → HTuple 암시적 변환
```

HALCON 24.11 `set_system` 레퍼런스 발췌 (검증 완료, 문서 원문):
```
'memory_allocator' : Set the memory allocator used to manage HALCON's heap.
  'system'   : Use the system's default heap allocator. On Windows this is Win32's HeapAlloc.
  'mimalloc' : Use the mimalloc heap allocator.
  Default: 'mimalloc' on Windows systems, 'system' otherwise.
  Note: this setting does neither replace nor divert the system's standard heap allocator,
  it only changes which function HALCON calls to allocate memory internally.
```
→ 즉 이 설정은 **HALCON 내부 할당 경로만** 바꾼다. 이미지 데이터/알고리즘/수치 결과에는 영향이 없다(측정값 회귀 위험 없음).
</interfaces>

<edit_anchors>
절대 라인 번호를 신뢰하지 말 것 — 아래 content-anchor 로 위치를 찾아 편집한다.

- 대상 메서드: `public void Initialize() {` (파일 내 유일)
- 편집 위치: 그 바로 아래 `// quick-260806-dsn Part A:` 로 시작하는 5줄짜리 주석 블록 **다음**의 `try {` 바로 다음 줄
- 앵커 문자열: `HOperatorSet.SetSystem("global_mem_cache", "idle");` — **이 줄 바로 위**에 새 주석 + 새 줄을 삽입한다
- 이 파일에서 `HOperatorSet.SetSystem` 은 현재 3곳뿐이며 전부 이 try 블록 안에 있다(작업 후 4곳)
</edit_anchors>
</context>

<tasks>

<task type="auto" tdd="false">
  <name>Task 1: HALCON 힙 할당자를 mimalloc → system 으로 전환 (1줄 추가)</name>
  <files>WPF_Example/SystemHandler.cs</files>
  <action>
`Initialize()` 의 기존 try 블록 **첫 실행문**으로 할당자 전환 1줄과 그 위 "왜" 주석을 삽입한다. 작업 후 해당 블록은 정확히 아래와 같아야 한다(115~119행 기존 주석 블록과 `catch` 블록은 **그대로**, 121~123행 기존 3줄도 **그대로** — 삽입만 한다):

```csharp
        // Call after constructor to fully initialize runtime components.
        public void Initialize() {
            // quick-260806-dsn Part A: HALCON 자체 캐시(mimalloc, HALCON 24.11 Windows 기본 할당자)가 해제된
            //  메모리를 OS에 즉시 반환하지 않고 계속 쌓아두는 문제의 공식 완화책(memory_management 챕터,
            //  "Handling Suspected Memory Leaks in HALCON" 권장 3줄, 앱 시작 시 1회). 캐시 정책만 바꿀 뿐
            //  기능/정확성에는 영향 없다. Devices/Sequences 등 이후의 모든 Halcon 이미지 처리에 적용되도록
            //  이 메서드의 첫 실행문으로 둔다. 실패해도(캐시 힌트 실패일 뿐) 앱 시작을 막지 않는다.
            try {
                // quick-260806-dsn3: 위 3줄(캐시 idle)로도 메모리가 안 돌아오는 경우를 위한 같은 챕터의 다음 단계 —
                //  HALCON 내부 힙 할당자를 Windows 기본값 mimalloc 에서 Win32 기본 힙(system)으로 전환한다.
                //  문서 원문: "mimalloc tends to cache memory more aggressively than the Win32 default heap
                //  allocator ... set_system('memory_allocator', 'system')". 격리 하네스 실측에서 121MB HImage를
                //  생성/Dispose 반복 시 mimalloc 은 8회차부터 WorkingSet 이 ~152MB 에 영구 고착(GC.Collect 무효)한 반면
                //  'system' 은 15회 전부 ~30MB 로 반환됐다. 할당자 종류 설정이므로 다른 SetSystem 보다 먼저 둔다.
                //  HALCON 내부 할당 경로만 바꾸므로 이미지 데이터/측정 수치에는 영향이 없다.
                HOperatorSet.SetSystem("memory_allocator", "system");
                HOperatorSet.SetSystem("global_mem_cache", "idle");
                HOperatorSet.SetSystem("temporary_mem_cache", "idle");
                HOperatorSet.SetSystem("image_cache_capacity", 0);
            }
            catch (Exception ex) {
                Logging.PrintLog((int)ELogType.Error, "[STARTUP] HALCON SetSystem memory cache config failed: {0}", ex.Message);
            }
```

**중요 — 스코프 경계(회귀 방지, 자체 확인할 것):**
- **삭제 라인 0**: 기존 코드/주석을 단 한 줄도 지우거나 고치지 않는다. `git diff --numstat` 의 삭제 카운트가 0 이어야 한다. (기존 주석의 "권장 3줄" 표현은 새 주석이 "같은 챕터의 다음 단계"라고 이어 설명하므로 그대로 두면 된다.)
- **신규 try/catch 금지**: 기존 try 블록 안에 넣는다. 새 줄이 예외를 던지면 뒤의 캐시 3줄까지 건너뛰게 되는 구조적 부작용이 있는데, 이는 Task 2 에서 Error 로그 부재로 검증한다(발생 시 후속 quick 에서 try 분리).
- **로깅/진단 코드 추가 금지**: `GetSystem("memory_allocators_supported")` 같은 확인 호출, 성공 로그 등 어떤 것도 추가하지 않는다.
- **다른 파일 수정 금지**: `git diff --name-only` 결과가 `WPF_Example/SystemHandler.cs` 하나여야 한다.
- 삼항 연산자 신규 도입 0건(이번 변경엔 분기 없음).

**빌드 시 주의(프로젝트 규칙):** 앱이나 VS 디버그 세션이 `DatumMeasurement.exe` 를 점유해 빌드가 실패하면 **절대 프로세스를 강제 종료하지 말 것**. 그 경우 아래 fallback 으로 컴파일만 검증하고, 잠김 사실을 그대로 보고한다:
`"/c/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/MSBuild.exe" "WPF_Example/DatumMeasurement.csproj" //t:Build //p:Configuration=Debug //p:Platform=x64 //p:OutputPath="C:/Users/tech/AppData/Local/Temp/claude/C--Info-Project-DataMeasurement/6daecb8f-c376-47ac-89d1-018d55afefc3/scratchpad/build-iuy/" //v:minimal`
  </action>
  <verify>
    <automated>cd "C:/Info/Project/DataMeasurement" && "/c/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/MSBuild.exe" "WPF_Example/DatumMeasurement.csproj" //t:Rebuild //p:Configuration=Debug //p:Platform=x64 //v:minimal 2>&1 | grep -iE "error|Build succeeded"; F="WPF_Example/SystemHandler.cs"; ALLOC=$(grep -c 'HOperatorSet.SetSystem("memory_allocator", "system");' "$F"); FIRST=$(grep -n "HOperatorSet.SetSystem" "$F" | head -1 | grep -c "memory_allocator"); TOTAL=$(grep -c "HOperatorSet.SetSystem" "$F"); GMC=$(grep -c '"global_mem_cache", "idle"' "$F"); TMC=$(grep -c '"temporary_mem_cache", "idle"' "$F"); ICC=$(grep -c '"image_cache_capacity", 0' "$F"); CATCHMSG=$(grep -c "HALCON SetSystem memory cache config failed" "$F"); if [ "$ALLOC" -eq 1 ] && [ "$FIRST" -eq 1 ] && [ "$TOTAL" -eq 4 ]; then echo "ALLOCATOR_FIRST_OK"; else echo "ALLOCATOR_PLACEMENT_BAD ALLOC=$ALLOC FIRST=$FIRST TOTAL=$TOTAL"; fi; if [ "$GMC" -eq 1 ] && [ "$TMC" -eq 1 ] && [ "$ICC" -eq 1 ] && [ "$CATCHMSG" -eq 1 ]; then echo "EXISTING_LINES_INTACT_OK"; else echo "EXISTING_LINES_CHANGED GMC=$GMC TMC=$TMC ICC=$ICC CATCH=$CATCHMSG"; fi; DEL=$(git diff --numstat -- "$F" | awk '{print $2}'); if [ "${DEL:-0}" -eq 0 ]; then echo "NO_DELETIONS_OK"; else echo "UNEXPECTED_DELETIONS del=$DEL"; fi; CHANGED=$(git diff --name-only | tr '\n' ' '); echo "CHANGED_FILES=[$CHANGED]"; NEWTRY=$(git diff -U0 -- "$F" | grep "^+" | grep -cE "try \{|catch \("); echo "NEW_TRY_CATCH_LINES=$NEWTRY"</automated>
  </verify>
  <done>`HOperatorSet.SetSystem("memory_allocator", "system");` 가 정확히 1회 존재하고 이 파일의 **첫 번째** `HOperatorSet.SetSystem` 이며 총 SetSystem 은 4개다(ALLOCATOR_FIRST_OK). 기존 캐시 3줄과 catch 로그 문자열이 그대로다(EXISTING_LINES_INTACT_OK). 삭제 라인 0(NO_DELETIONS_OK). 변경 파일은 `WPF_Example/SystemHandler.cs` 하나뿐이다(CHANGED_FILES). 신규 try/catch 0줄(NEW_TRY_CATCH_LINES=0). MSBuild Debug/x64 error 0("Build succeeded"). exe 잠김으로 정식 빌드가 막힌 경우 scratch OutputPath 컴파일-only PASS + 잠김 사실을 SUMMARY 에 명시(프로세스 강제 종료 금지).</done>
</task>

<task type="checkpoint:human-verify" gate="blocking">
  <name>Task 2: 실기 검증 — 배치 반복 시 메모리 반환/크래시 없음/측정값 동일/속도 회귀 확인</name>
  <action>이 태스크는 사용자 실기 검증 checkpoint 이다 — 실행자는 코드를 추가 수정하지 말고, 아래 &lt;how-to-verify&gt; 절차를 그대로 사용자에게 제시한 뒤 응답을 기다린다. 사용자가 직접 앱을 종료/재실행하며, 어떠한 경우에도 실행자가 프로세스를 강제 종료하지 않는다.</action>
  <what-built>
`SystemHandler.Initialize()` 의 기존 HALCON 메모리 설정 try 블록 맨 위에 `HOperatorSet.SetSystem("memory_allocator", "system");` 1줄을 추가했다. 이제 앱이 시작될 때 HALCON 이 내부적으로 메모리를 잡을 때 쓰는 할당자가 Windows 기본값인 **mimalloc** 대신 **Win32 기본 힙(HeapAlloc)** 이 된다. mimalloc 은 성능을 위해 해제된 메모리를 자기 안에 계속 쥐고 있어서(공식 문서 표현: "caches memory more aggressively") `Dispose()` 를 아무리 정확히 해도 작업 관리자/`WorkingSet64` 상으로는 메모리가 안 줄어드는 것처럼 보인다 — 격리 테스트에서 121MB 이미지 8회차부터 영구 고착이 재현됐고, `system` 으로 바꾸니 15회 전부 정상 반환됐다.

자동 검증으로 확인한 것: 빌드 PASS, 새 줄이 정확한 위치(캐시 3줄보다 먼저)에 1줄만 추가됨, 기존 3줄/catch/주석 무변경(삭제 0), 다른 파일 diff 0.

자동으로 검증 **불가능**한 것 3가지 — 실기 확인이 필요하다:
(1) 실제 30개 항목 배치에서 메모리가 진짜로 내려오는지(격리 하네스와 실기는 규모/동시성이 다르다),
(2) 새 줄이 런타임에 예외를 던지지 않는지 — **던지면 같은 try 안의 기존 캐시 3줄까지 통째로 건너뛰어 상황이 오히려 나빠진다**(Error 로그로만 판별 가능),
(3) 할당자를 바꾼 대가로 검사 속도가 눈에 띄게 느려지지 않는지(HALCON 문서상 mimalloc 이 더 빠를 수 있다고 명시됨).
  </what-built>
  <how-to-verify>
1. 실행 중인 이전 인스턴스가 있으면 **직접 정상 종료**한다(강제 종료 아님). 최신 커밋으로 Debug/x64 재빌드 후 앱을 새로 실행한다.

2. **기동 정상성 확인 (가장 먼저)**
   - 앱이 예외 팝업/크래시 없이 정상 기동하는지 확인한다.
   - 이미지가 정상 표시되는지 확인한다(아무 SHOT 노드 클릭 → 이미지 뜸).
   - **(a) 핵심 확인**: `D:\Data\Error\` 최신 로그를 열어 `[STARTUP] HALCON SetSystem memory cache config failed` 문자열이 **없는지** 확인한다.
     - 없으면 정상(할당자 전환 + 캐시 3줄 전부 적용됨).
     - **있으면 즉시 중단하고 보고**한다 — 새로 추가한 줄이 던져서 기존 캐시 3줄까지 무력화된 상태다(후속 조치: try 를 분리해야 함).

3. **메모리 재현 시나리오** — 지난 세션(34~41GB / 16.7GB 고착)과 **동일 조건**으로 맞춘다.
   - PowerShell 별도 창에서 실시간 관찰:
     ```powershell
     while ($true) { $p = Get-Process DatumMeasurement -ErrorAction SilentlyContinue; if ($p) { "{0:HH:mm:ss} WS={1:N0} MB  Priv={2:N0} MB" -f (Get-Date), ($p.WorkingSet64/1MB), ($p.PrivateMemorySize64/1MB) }; Start-Sleep -Seconds 2 }
     ```
   - 트리에서 BOTTOM 시퀀스를 선택하고 지난번과 같이 약 30개 항목(SHOT)을 체크한다.
   - 시작 직후 기준값(baseline MB)을 메모에 적어둔다.

4. **일괄검사 1사이클 실행 → 완료 후 1~2분 관찰**
   - **(b) 확인**: 사이클 완료 후 메모리가 계단식으로 **내려오는지** 확인한다. (260806-dsn-2 의 재시도 대기열이 저장 큐를 따라잡으며 순차 정리하므로 즉시가 아니라 1~2분에 걸쳐 내려오는 게 정상 패턴이다.)
   - 지난 세션과의 차이가 핵심이다: 지난번엔 정리 로그(`[BatchImageCleanup] 재시도 성공`)가 다 찍혔는데도 WorkingSet 이 16.7GB 에서 **안 내려왔다**. 이번엔 내려와야 한다.

5. **반복 누적 확인 (이번 수정의 본 목적)**
   - 일괄검사를 **연속 3~5사이클** 반복 실행한다(각 사이클 사이 1~2분 관찰).
   - **(c) 확인**: 사이클을 거듭해도 최고점이 계속 밀려 올라가지 않고(34~41GB 로 향하지 않고) 일정 범위 안에서 **오르내리며 안정화**되는지 확인한다.
   - **(d) 확인**: 반복 중/후에 `halcon.DLL` 크래시가 발생하지 않는지 확인한다.

6. **결과 동일성 확인 (회귀 방지)**
   - **(e) 확인**: 임의의 FAI 측정값 3~5개를 지난 검사 결과(엑셀 Export 또는 화면 값)와 비교해 **동일**한지 확인한다. 할당자 교체는 수치에 영향이 없어야 하므로 값이 달라지면 이상 신호다.
   - 종합 판정(P/F/B)도 동일 자재 기준으로 동일해야 한다.

7. **성능 확인 (트레이드오프)**
   - **(f) 확인**: 1사이클 소요 시간이 지난 세션 대비 눈에 띄게 느려지지 않았는지 확인한다(Flow 로그의 `사이클 종료 ... 소요 N초` 줄로 비교하면 쉽다). HALCON 문서가 mimalloc 이 더 빠를 수 있다고 명시하므로 약간의 증가는 예상 범위지만, **체감될 만큼 느려지면 그 수치를 보고**한다(그 경우 메모리 안정 vs 속도 트레이드오프를 사용자가 판단).
  </how-to-verify>
  <resume-signal>(a)~(f) 전부 정상이면 "approved". 하나라도 문제가 있으면 어떤 항목인지와 실측값을 함께 기술한다 — 특히 (a) Error 로그가 찍힌 경우(→ try 분리 후속 필요), (c) 여전히 누적되는 경우(→ 사이클별 최고/최저 MB 수치), (f) 느려진 경우(→ 변경 전/후 사이클 소요 초).</resume-signal>
</task>

</tasks>

<threat_model>
## Trust Boundaries

| Boundary | Description |
|----------|-------------|
| 없음 (신규 trust boundary 미도입) | 이번 변경은 프로세스 내부 native 힙 할당자 선택을 바꾸는 시작 시 설정 1줄이다. 외부 입력·네트워크 노출·파일 경로·사용자 입력 경로가 전혀 추가되지 않는다. |

## STRIDE Threat Register

| Threat ID | Category | Component | Disposition | Mitigation Plan |
|-----------|----------|-----------|-------------|-----------------|
| T-260806iuy-01 | D (Denial of Service — 시작 설정 실패의 연쇄) | `Initialize()` 의 공유 try 블록 | mitigate | 새 줄을 기존 try 최상단에 두므로, 만약 이 줄이 던지면 뒤의 캐시 3줄이 통째로 건너뛰어져 메모리 상황이 오히려 악화된다. 신규 try/catch 를 추가하지 않는 대신(요구된 최소 diff), Task 2 (a) 에서 Error 로그 `[STARTUP] HALCON SetSystem memory cache config failed` 의 **부재**를 필수 검증 항목으로 못박고, 발생 시 즉시 중단·보고 → 후속 quick 에서 try 분리로 처리. |
| T-260806iuy-02 | D (성능 저하로 인한 처리량 감소) | HALCON 전역 할당 경로 | accept | HALCON 문서가 명시하듯 `'system'` 이외 할당자는 성능상 유리할 수 있다 → `'system'` 전환은 할당 집약 구간에서 느려질 수 있다. 메모리 폭증/크래시가 현재 가동 자체를 막고 있으므로 감수하되, Task 2 (f) 에서 사이클 소요 시간을 실측해 사용자에게 트레이드오프 판단 근거를 제공한다. |
| T-260806iuy-03 | T (Tampering — 런타임 중 할당자 전환으로 인한 힙 불일치) | HALCON 내부 힙 | mitigate | 이 프로젝트에선 생성자에서 `DeviceHandler.Initialize()` 가 먼저 돌기 때문에 `Initialize()` 시점엔 이미 일부 HALCON 할당이 있었을 수 있다. HALCON 문서는 `set_system('memory_allocator', ...)` 에 "최초 호출 이전에만 가능" 같은 제약을 명시하지 않으며(다른 파라미터와 달리 순서 제약 문구 없음) 할당자 추적은 HALCON 내부 책임이다. 그래도 잔여 위험이므로 Task 2 의 2번(기동 정상성: 예외 없음 + 이미지 정상 표시)과 (e)(측정값 동일)로 실기 확인한다. |
| T-260806iuy-04 | I (Information Disclosure) | 해당 없음 | accept | 새 코드가 로그/파일/네트워크로 아무것도 내보내지 않는다(로깅 추가 금지가 명시적 스코프). |
</threat_model>

<verification>
- Task 1 `<automated>`: "Build succeeded" + error 0, ALLOCATOR_FIRST_OK, EXISTING_LINES_INTACT_OK, NO_DELETIONS_OK, CHANGED_FILES 가 `WPF_Example/SystemHandler.cs` 단독, NEW_TRY_CATCH_LINES=0.
- 260806-dsn Part A 의 캐시 3줄과 260806-dsn-2 의 재시도 대기열 로직은 이번 plan 에서 전혀 수정되지 않는다(전자는 같은 try 안에 그대로, 후자는 `InspectionListView.xaml.cs` 로 `files_modified` 에 아예 없음).
- Task 2 checkpoint 의 (a)~(f) 전부 승인.
</verification>

<success_criteria>
- 앱 기동 시 HALCON 힙 할당자가 `system` 으로 전환되고, Error 로그에 SetSystem 실패 기록이 없다(= 캐시 3줄도 함께 정상 적용).
- 30개 항목 배치검사 반복 시 `WorkingSet64` 가 사이클 정리 후 실제로 감소하며, 34~41GB 로 누적되지 않고 안정 범위에서 오르내린다.
- 반복 배치 중 `halcon.DLL` 크래시가 재현되지 않는다.
- 측정값/판정 결과가 변경 전과 동일하다(회귀 0).
- diff 는 `WPF_Example/SystemHandler.cs` 1개 파일, 추가만 있고 삭제 0줄이다.
</success_criteria>

<output>
After completion, create `.planning/quick/260806-iuy-quick-260806-dsn3-mimalloc-system/260806-iuy-SUMMARY.md`

SUMMARY 에 반드시 포함할 것:
- 격리 하네스 실측 수치(mimalloc 152.1MB 고착 vs system 30.0MB)와 HALCON 공식 문서 근거(memory_management 챕터 4) — 다음 세션이 이 결정의 배경을 재조사하지 않도록.
- 실기 checkpoint 의 (a)~(f) 결과와 사이클 소요 시간 변경 전/후 수치.
- `project_batch_memory_never_shrinks_260806` 메모리 항목의 "미해결" 상태를 갱신할 수 있는지(해결/부분해결/미해결) 판단 결과.
</output>
