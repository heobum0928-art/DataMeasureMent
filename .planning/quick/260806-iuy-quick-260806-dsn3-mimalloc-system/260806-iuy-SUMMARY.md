---
phase: quick-260806-iuy
plan: 01
subsystem: startup-init
tags: [halcon, memory, mimalloc, batch-inspection]
requires: []
provides:
  - "SystemHandler.Initialize() 시작 시 HALCON 힙 할당자를 mimalloc → system 으로 전환"
affects:
  - "배치검사 반복 시 프로세스 WorkingSet64 메모리 반환 거동"
tech-stack:
  added: []
  patterns:
    - "HOperatorSet.SetSystem(\"memory_allocator\", \"system\") — 기존 캐시 idle 3줄과 같은 try 블록의 첫 실행문"
key-files:
  created: []
  modified:
    - "WPF_Example/SystemHandler.cs"
decisions:
  - "HALCON 24.11 Windows 기본 할당자 mimalloc → system(Win32 HeapAlloc) 전환. 공식 문서(memory_management 챕터4, Handling Suspected Memory Leaks in HALCON)가 캐시 3줄 적용 후에도 메모리가 안 돌아오면 권장하는 다음 단계."
  - "신규 try/catch 도입하지 않고 기존 catch 블록을 그대로 재사용 — 새 줄이 던지면 캐시 3줄까지 함께 skip되는 구조적 위험을 Task 2의 Error 로그 부재 확인으로 커버(최소 diff 우선)."
metrics:
  duration: "~15분(Task 1만, Task 2는 human checkpoint 대기)"
  completed: 2026-08-06
---

# Phase quick-260806-iuy Plan 01: HALCON mimalloc → system 할당자 전환 Summary

**One-liner:** `SystemHandler.Initialize()`의 기존 HALCON 캐시 설정 try 블록 최상단에 `HOperatorSet.SetSystem("memory_allocator", "system")` 1줄을 추가해 Windows 기본 mimalloc 할당자를 Win32 기본 힙으로 전환 — 격리 하네스 실측으로 8회차부터 영구 고착(mimalloc)하던 메모리가 매회 정상 반환(system)됨을 확인.

## What Was Built

`WPF_Example/SystemHandler.cs`의 `Initialize()` 메서드 — 260806-dsn Part A가 이미 넣어둔 HALCON 캐시 3줄(`global_mem_cache`/`temporary_mem_cache`/`image_cache_capacity`)을 감싸는 기존 try 블록의 **맨 위**(그 3줄보다 먼저)에 새 줄 1개와 "왜" 주석(`quick-260806-dsn3:` 접두)을 추가했다. 기존 3줄, 기존 주석(115~119행, `quick-260806-dsn Part A:`), catch 블록은 글자 하나도 바뀌지 않았다.

```csharp
try {
    // quick-260806-dsn3: 위 3줄(캐시 idle)로도 메모리가 안 돌아오는 경우를 위한 같은 챕터의 다음 단계 —
    //  HALCON 내부 힙 할당자를 Windows 기본값 mimalloc 에서 Win32 기본 힙(system)으로 전환한다.
    //  ...(생략)...
    HOperatorSet.SetSystem("memory_allocator", "system");
    HOperatorSet.SetSystem("global_mem_cache", "idle");
    HOperatorSet.SetSystem("temporary_mem_cache", "idle");
    HOperatorSet.SetSystem("image_cache_capacity", 0);
}
catch (Exception ex) {
    Logging.PrintLog((int)ELogType.Error, "[STARTUP] HALCON SetSystem memory cache config failed: {0}", ex.Message);
}
```

Commit: `715f6e2` — `fix(quick-260806-iuy): HALCON 힙 할당자 mimalloc -> system 전환`

## Why This Was the Last Suspected Root Cause

- 260805-mze/mzf/mzh/ojq + 260806-dsn/dsn-2 로 애플리케이션 레벨 원인(큐 백프레셔·패턴모델 캐시·HObject 누수·저장큐 레이스 재시도 대기열)은 전부 수정되고 **로그로 정상 동작 확인**됐음에도, 실기 배치에서 `Process.WorkingSet64`가 16.7GB+에서 내려오지 않는 증상이 남아있었다 (`project_batch_memory_never_shrinks_260806` 메모리에 미해결로 기록).
- 260806-dsn Part A가 HALCON 자체 캐시 3종을 이미 idle/0으로 껐지만 그것도 해결이 안 됐다 → 남은 계층은 그 아래 **native 할당자**.

### HALCON 24.11 공식 문서 근거

`C:\Program Files\MVTec\HALCON-24.11-Progress-Steady\doc\html\manuals\memory_management\` — 챕터 4 "Handling Suspected Memory Leaks in HALCON"이 캐시 3줄로도 해결이 안 되면 다음 단계로 정확히 이 조치를 권고한다:

> "Switch off mimalloc (under Windows). mimalloc tends to cache memory more aggressively than the Win32 default heap allocator. Therefore, switching to the default allocator can help resolve memory related problems in some cases: `set_system('memory_allocator', 'system')`"

`set_system` 레퍼런스로 파라미터 실재 확인: `'memory_allocator'`는 `'system'`/`'mimalloc'` 값을 가지며 **Windows 기본값은 `'mimalloc'`**(다른 OS는 `'system'`이 기본). 이 설정은 HALCON 내부 할당 경로만 바꾸고 이미지 데이터/알고리즘/수치 결과에는 영향이 없음이 문서에 명시되어 있다(측정값 회귀 위험 없음).

### 격리 하네스 실측 수치 (리포지토리 밖, 이번 plan 실행 범위 아님 — 이전 세션에서 수행됨)

실제 생산 SHOT 이미지 규모인 13376×9528 mono8 HImage(≈121.5MB)를 15회 생성/Dispose 반복:

| 할당자 | 거동 | 최종 WorkingSet64 |
|---|---|---|
| `mimalloc`(변경 전 기본값) | 1~7회차는 Dispose 후 ~30MB로 정상 반환되다가 **8회차부터 ~152MB에 영구 고착**, 이후 끝까지 안 내려감. `GC.Collect()` + `WaitForPendingFinalizers()` 추가해도 효과 0 | **152.1MB** |
| `memory_allocator='system'` | 15회 전부 Dispose 직후 ~30MB로 정상 반환 | **30.0MB** |

→ .NET GC/finalizer 문제가 아니라 **native 할당자 문제**임이 이 실측으로 확정됨(GC.Collect 무효화가 결정적 증거).

## Task 1 Verification (자동, 완료)

Plan의 `<automated>` verify 블록을 그대로 실행:

- `ALLOCATOR_FIRST_OK` — `memory_allocator` 줄이 정확히 1회, 파일의 **첫 번째** `HOperatorSet.SetSystem` 호출이며 총 SetSystem 4개.
- `EXISTING_LINES_INTACT_OK` — 기존 캐시 3줄 + catch 로그 문자열 그대로.
- `NO_DELETIONS_OK` — 삭제 라인 0.
- `CHANGED_FILES=[WPF_Example/SystemHandler.cs]` — 단독 파일.
- `NEW_TRY_CATCH_LINES=0` — 신규 try/catch 없음.
- 빌드: 0 error, 0 warning.

### 빌드 관련 특이사항 (프로세스 강제 종료 금지 규칙 준수)

정식 `//t:Rebuild` 커맨드 실행 시 `obj\x64\Debug\DatumMeasurement.exe`를 `bin\x64\Debug\DatumMeasurement.exe`로 복사하는 마지막 단계에서 **MSB3027/MSB3021 (파일이 다른 프로세스에 의해 잠김 — "Microsoft Visual Studio Insiders (14504), DatumMeasurement (15732)")** 오류가 발생했다. 이는 CoreCompile **이후** 단계(복사)의 실패이므로 소스 컴파일 자체는 이미 성공한 상태였다(`obj\x64\Debug\DatumMeasurement.exe` 타임스탬프가 소스 수정 시각보다 늦음으로 확인).

plan 지시대로 프로세스를 강제 종료하지 않고, scratch `OutputPath`로 컴파일-only fallback 빌드를 실행해 0 error/0 warning으로 재확인했다(`MSBuild ... /t:Build /p:OutputPath=<scratch>/build-iuy/`). 실행 중이던 앱/VS 세션은 그대로 유지된 상태다 — **exe 잠김은 해소되지 않았으며, 사용자가 실행 중인 앱을 직접 정상 종료 후 재빌드해야 정식 `bin\x64\Debug\DatumMeasurement.exe`에 이번 변경이 반영된다** (Task 2의 1번 절차와 일치).

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] 작업 시작 전 이미 존재하던 잘못된 미커밋 변경 수정**
- **Found during:** Task 1 착수 직전 `git status`/`git diff` 확인 시
- **Issue:** 작업 디렉토리에 `WPF_Example/SystemHandler.cs`가 이미 수정된 상태(git status: `M`)였고, 그 내용은 `memory_allocator` 줄을 기존 캐시 3줄 **뒤**(4번째)에 추가한 것이었다. plan의 must-haves("할당자 전환 호출이 기존 캐시 3줄보다 먼저 실행된다")와 정면으로 어긋나고, plan이 요구하는 "왜" 주석(`quick-260806-dsn3:` 블록)도 빠져 있었다.
- **Fix:** 해당 줄을 제거하고, plan의 `<action>` 블록에 명시된 정확한 위치(try 블록 첫 실행문)와 전체 주석 텍스트로 재삽입.
- **Files modified:** `WPF_Example/SystemHandler.cs`
- **Commit:** `715f6e2`

## Task 2: 실기 검증 — PENDING (사람 승인 대기)

**이 항목은 자동 실행자가 수행하지 않았다.** Task 2는 `type="checkpoint:human-verify" gate="blocking"`이며, 코드 추가 수정 없이 사용자에게 절차를 제시하고 응답을 기다려야 한다. 아래는 plan에서 그대로 가져온 검증 지시(오케스트레이터가 사용자에게 전달할 내용)다.

### what-built (검증 대상 설명)

`SystemHandler.Initialize()`의 기존 HALCON 메모리 설정 try 블록 맨 위에 `HOperatorSet.SetSystem("memory_allocator", "system");` 1줄을 추가했다. 이제 앱이 시작될 때 HALCON이 내부적으로 메모리를 잡을 때 쓰는 할당자가 Windows 기본값인 **mimalloc** 대신 **Win32 기본 힙(HeapAlloc)**이 된다. mimalloc은 성능을 위해 해제된 메모리를 자기 안에 계속 쥐고 있어서(공식 문서 표현: "caches memory more aggressively") `Dispose()`를 아무리 정확히 해도 작업 관리자/`WorkingSet64` 상으로는 메모리가 안 줄어드는 것처럼 보인다 — 격리 테스트에서 121MB 이미지 8회차부터 영구 고착이 재현됐고, `system`으로 바꾸니 15회 전부 정상 반환됐다.

자동 검증으로 확인한 것: 빌드 PASS, 새 줄이 정확한 위치(캐시 3줄보다 먼저)에 1줄만 추가됨, 기존 3줄/catch/주석 무변경(삭제 0), 다른 파일 diff 0.

자동으로 검증 **불가능**한 것 3가지 — 실기 확인이 필요하다:
1. 실제 30개 항목 배치에서 메모리가 진짜로 내려오는지(격리 하네스와 실기는 규모/동시성이 다르다),
2. 새 줄이 런타임에 예외를 던지지 않는지 — **던지면 같은 try 안의 기존 캐시 3줄까지 통째로 건너뛰어 상황이 오히려 나빠진다**(Error 로그로만 판별 가능),
3. 할당자를 바꾼 대가로 검사 속도가 눈에 띄게 느려지지 않는지(HALCON 문서상 mimalloc이 더 빠를 수 있다고 명시됨).

### how-to-verify (사용자 절차)

1. 실행 중인 이전 인스턴스가 있으면 **직접 정상 종료**한다(강제 종료 아님). 최신 커밋(`715f6e2`)으로 Debug/x64 재빌드 후 앱을 새로 실행한다.

2. **기동 정상성 확인 (가장 먼저)**
   - 앱이 예외 팝업/크래시 없이 정상 기동하는지 확인한다.
   - 이미지가 정상 표시되는지 확인한다(아무 SHOT 노드 클릭 → 이미지 뜸).
   - **(a) 핵심 확인**: `D:\Data\Error\` 최신 로그를 열어 `[STARTUP] HALCON SetSystem memory cache config failed` 문자열이 **없는지** 확인한다.
     - 없으면 정상(할당자 전환 + 캐시 3줄 전부 적용됨).
     - **있으면 즉시 중단하고 보고**한다 — 새로 추가한 줄이 던져서 기존 캐시 3줄까지 무력화된 상태다(후속 조치: try를 분리해야 함).

3. **메모리 재현 시나리오** — 지난 세션(34~41GB / 16.7GB 고착)과 **동일 조건**으로 맞춘다.
   - PowerShell 별도 창에서 실시간 관찰:
     ```powershell
     while ($true) { $p = Get-Process DatumMeasurement -ErrorAction SilentlyContinue; if ($p) { "{0:HH:mm:ss} WS={1:N0} MB  Priv={2:N0} MB" -f (Get-Date), ($p.WorkingSet64/1MB), ($p.PrivateMemorySize64/1MB) }; Start-Sleep -Seconds 2 }
     ```
   - 트리에서 BOTTOM 시퀀스를 선택하고 지난번과 같이 약 30개 항목(SHOT)을 체크한다.
   - 시작 직후 기준값(baseline MB)을 메모에 적어둔다.

4. **일괄검사 1사이클 실행 → 완료 후 1~2분 관찰**
   - **(b) 확인**: 사이클 완료 후 메모리가 계단식으로 **내려오는지** 확인한다. (260806-dsn-2의 재시도 대기열이 저장 큐를 따라잡으며 순차 정리하므로 즉시가 아니라 1~2분에 걸쳐 내려오는 게 정상 패턴이다.)
   - 지난 세션과의 차이가 핵심이다: 지난번엔 정리 로그(`[BatchImageCleanup] 재시도 성공`)가 다 찍혔는데도 WorkingSet이 16.7GB에서 **안 내려왔다**. 이번엔 내려와야 한다.

5. **반복 누적 확인 (이번 수정의 본 목적)**
   - 일괄검사를 **연속 3~5사이클** 반복 실행한다(각 사이클 사이 1~2분 관찰).
   - **(c) 확인**: 사이클을 거듭해도 최고점이 계속 밀려 올라가지 않고(34~41GB로 향하지 않고) 일정 범위 안에서 **오르내리며 안정화**되는지 확인한다.
   - **(d) 확인**: 반복 중/후에 `halcon.DLL` 크래시가 발생하지 않는지 확인한다.

6. **결과 동일성 확인 (회귀 방지)**
   - **(e) 확인**: 임의의 FAI 측정값 3~5개를 지난 검사 결과(엑셀 Export 또는 화면 값)와 비교해 **동일**한지 확인한다. 할당자 교체는 수치에 영향이 없어야 하므로 값이 달라지면 이상 신호다.
   - 종합 판정(P/F/B)도 동일 자재 기준으로 동일해야 한다.

7. **성능 확인 (트레이드오프)**
   - **(f) 확인**: 1사이클 소요 시간이 지난 세션 대비 눈에 띄게 느려지지 않았는지 확인한다(Flow 로그의 `사이클 종료 ... 소요 N초` 줄로 비교하면 쉽다). HALCON 문서가 mimalloc이 더 빠를 수 있다고 명시하므로 약간의 증가는 예상 범위지만, **체감될 만큼 느려지면 그 수치를 보고**한다(그 경우 메모리 안정 vs 속도 트레이드오프를 사용자가 판단).

### resume-signal

(a)~(f) 전부 정상이면 "approved". 하나라도 문제가 있으면 어떤 항목인지와 실측값을 함께 기술한다 — 특히 (a) Error 로그가 찍힌 경우(→ try 분리 후속 필요), (c) 여전히 누적되는 경우(→ 사이클별 최고/최저 MB 수치), (f) 느려진 경우(→ 변경 전/후 사이클 소요 초).

### 사이클 소요 시간 변경 전/후 수치

미측정 — Task 2 실기 검증에서 사용자가 (f) 항목 수행 시 채워야 함.

### `project_batch_memory_never_shrinks_260806` 상태 갱신 판단

**미해결 → 부분해결(코드 반영 완료, 실기 검증 대기)로 잠정 갱신 권고.** Task 2의 (a)~(f)가 전부 "approved"로 나오면 **해결**로 최종 갱신 가능. 하나라도 실패하면(특히 (a) Error 로그 발생 또는 (c) 여전히 누적) 이 메모리 항목은 계속 미해결 상태를 유지해야 하며, (a) 실패 시 다음 후속 quick에서 try 분리가 필요하다.

## Known Stubs

없음 — 이번 변경은 UI/데이터 흐름에 영향 없는 시작 시 설정 1줄이다.

## Threat Flags

없음 — plan의 threat_model에 이미 등록된 위협(T-260806iuy-01~04) 범위 내에서만 변경했고, 신규 네트워크/인증/파일 경로/스키마 노출이 없다.

## Self-Check: PASSED

- `WPF_Example/SystemHandler.cs` 존재 확인: FOUND
- 커밋 `715f6e2` 존재 확인: FOUND (아래 self-check 절 참고)
