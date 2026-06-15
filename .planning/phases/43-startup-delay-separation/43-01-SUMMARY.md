---
phase: 43-startup-delay-separation
plan: 01
subsystem: startup
tags: [loginmanager, threading, background-preload, startup-performance, halcon]

# Dependency graph
requires:
  - phase: 38-carry-over-cleanup
    provides: "[STARTUP] 9줄 Stopwatch 계측 베이스라인 (Step 1~8 + Total)"
provides:
  - "LoginManager.Preload() / EnsureLoaded() / PreloadWorker() 백그라운드 프리로드 인프라"
  - "SystemHandler.Initialize() Step 5 동기 Load 제거 → 백그라운드 thread 기동"
  - "[STARTUP] READY 마커 — Step 7 직후 첫 $TEST 수용 가능 시점 단일 기준 지표"
  - "LoginWindow.ctor EnsureLoaded readiness wait — half-loaded AccountList race 차단"
affects: [phase-44-startup-hw-remeasure, phase-43-context]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "volatile bool ready flag + Thread.Join() EnsureLoaded 패턴 (RawImageSaveService 워커 패턴 차용)"
    - "[STARTUP] READY 마커로 '첫 $TEST 수용 가능 시점'을 단일 로그 지표화"

key-files:
  created: []
  modified:
    - WPF_Example/Login/LoginManager.cs
    - WPF_Example/SystemHandler.cs
    - WPF_Example/UI/Login/LoginWindow.xaml.cs

key-decisions:
  - "D-03: LoginManager 생성자 동기 Load() 제거 → Preload() 백그라운드 thread 기동 (RawImageSaveService 패턴 차용)"
  - "D-04/D-10: EnsureLoaded() = 완료 시 즉시 반환, 미완 시 Join, 미기동 시 동기 폴백 — half-loaded AccountList race 불가"
  - "D-01/D-02: [STARTUP] READY = Step 7 CollectRecipe 완료 직후 단일 기준 지표 (Before/After 30% 비교)"
  - "D-06/D-07: SequenceHandler(Step 2)는 측정 임계 경로 — 이동하지 않음. LoginManager(Step 5)만 분리"
  - "CO-43-01(carry-over): 18~20s 흰 화면은 Initialize() 외부(MainWindow ctor cold JIT + native DLL 로딩) — 별도 phase 신설 결정"

patterns-established:
  - "volatile bool _isPreloaded + Thread.IsAlive 이중 guard로 재기동 방지 (Preload 메서드)"
  - "EnsureLoaded 3-way: _isPreloaded=true → 즉시 반환 / IsAlive=true → Join / 둘 다 false → 동기 폴백"
  - "UI 모달 차단 컨텍스트에서 EnsureLoaded Join 허용 (LoginWindow ShowDialog 선례)"

requirements-completed: [CO-38-02, CO-38-03]

# Metrics
duration: 계측 기반 (Task 1~3 코드 약 45분 + Task 4 UAT 런타임 승인)
completed: 2026-06-15
---

# Phase 43 Plan 01: 시작지연 분리 Summary

**LoginManager.Load() 를 백그라운드 thread 로 이동하여 [STARTUP] READY 시간 55% 단축 (567~628ms, Before ≈ 1285ms) + half-loaded AccountList race 차단 (EnsureLoaded Join)**

## Performance

- **Duration:** Task 1~3 코드 구현 + Task 4 런타임 UAT (사용자 승인 포함)
- **Started:** 2026-06-15T(계측값 없음 — continuation agent)
- **Completed:** 2026-06-15
- **Tasks:** 4 (Task 1~3 자동 + Task 4 human-verify 승인 완료)
- **Files modified:** 3

## Accomplishments

- SystemHandler.Initialize() Step 5 LoginManager 동기 Load(808ms) 를 백그라운드 thread 로 이동 — READY 마커 기준 55% 단축 (Before ≈ 1285ms → After avg 578ms, 목표 ≥30% PASS)
- [STARTUP] READY 마커 신규 삽입(Step 7 CollectRecipe 직후) — 첫 $TEST 수용 가능 시점을 단일 로그 지표로 정형화, CO-38-02/CO-38-03 공식 종결
- LoginWindow.ctor 에 EnsureLoaded() 선행 호출 — half-loaded AccountList race(T-43-01) 구조적 차단, 프리로드 미완 시 Join / 완료 시 즉시 반환 / 미기동 시 동기 폴백 3-way 안전 경로

## Task Commits

각 태스크를 독립적으로 커밋:

1. **Task 1: LoginManager 백그라운드 프리로드 인프라** - `6d75145` (feat)
2. **Task 2: SystemHandler Step 5 교체 + [STARTUP] READY 마커** - `81c1686` (feat)
3. **Task 3: LoginWindow EnsureLoaded readiness wait + 통합 빌드** - `700355e` (feat)
4. **Task 4: human-verify** — 코드 커밋 없음 (런타임 UAT 사용자 승인)

## Files Created/Modified

- `WPF_Example/Login/LoginManager.cs` — volatile bool _isPreloaded + Thread _preloadThread + Preload()/PreloadWorker()/EnsureLoaded() 추가; 생성자 동기 Load() 제거; Load() 본문 무수정
- `WPF_Example/SystemHandler.cs` — Step 5 LoginManager.Handle.Preload() 교체 + [STARTUP] READY 마커(Step 7 직후·Step 8 이전) 삽입; Step 2 SequenceHandler 무수정
- `WPF_Example/UI/Login/LoginWindow.xaml.cs` — ctor 에 Login.EnsureLoaded() 한 줄 삽입 (comboBox_id.ItemsSource 호출 이전)

## Decisions Made

- **D-03**: 생성자 동기 Load() 제거 → `Preload()` 백그라운드 thread 기동(RawImageSaveService volatile flag + Thread 패턴 그대로 차용). Load() 본문 자체는 한 줄도 수정하지 않아 인증 동작/계정 모델 불변.
- **D-04/D-10**: `EnsureLoaded()` 3-way 방어 경로 — (a) _isPreloaded=true → 즉시 반환, (b) thread.IsAlive → Join, (c) 미기동 → 동기 Load() 폴백. UI 모달 컨텍스트에서 Join 차단 허용(LoginWindow는 ShowDialog 모달 자체가 차단).
- **D-01/D-02**: READY 마커 = Step 7 직후. recipe 스캔 완료 + SystemThread(Step 4) alive + Sequences(Step 2) 구성 완료가 모두 이 시점에 만족됨.
- **D-06/D-07**: SequenceHandler(Step 2)는 측정 임계 경로이므로 이번 phase에서 이동하지 않음. CO-38-03의 "불필요하게 끼어든 동기 의존성만 분리" 해석 = LoginManager 하나만 분리.
- **Btn_edit_Click L89 (의도적 배제)**: LoginWindow.Btn_edit_Click 핸들러의 GetIDList() 호출 이전에는 EnsureLoaded() 를 추가하지 않음 — 생성자에서 이미 Join 완료된 이후에만 실행되는 경로이므로 중복 호출 불필요(plan-checker WARNING 문서화 완료 c2c6713).

## Deviations from Plan

계획 범위를 벗어난 자동 수정 없음. 플랜 그대로 실행.

단, 런타임 UAT(Task 4) 에서 **범위 외 발견 사항** 1건 기록:

### CO-43-01 (Carry-over) — 18~20s 흰 화면 (Initialize() 외부, 별도 phase 신설 결정)

- **발견 시점:** Task 4 사용자 UAT
- **내용:** 앱 프로세스 기동 후 화면이 나타나기까지 18~20s 흰 화면 관찰. 근본 원인은 Initialize() 내부가 아님 — Initialize() 자체는 After 기준 ≈ 579ms. 흰 화면 구간은 cold JIT(Debug 빌드) + Halcon/OpenCV/카메라 네이티브 DLL 로딩 + MainWindow InitializeComponent() XAML inflation 이 모두 MainWindow.ctor 안에서 view.Show() 이전에 실행되기 때문.
- **원인 위치:** MainWindow.xaml.cs:78 Initialize → :81 InitializeComponent → 이후 Show() — pre-existing, 본 phase 변경과 무관.
- **조치:** Phase 43 목표(≥30% READY 단축)는 달성됨. 흰 화면은 별도 신규 phase 에서 해결하기로 사용자 결정. CO-43-01 로 추적.
- **제안 carry-over 제목:** CO-43-01 — 앱 기동 흰 화면(paint-before-Initialize 패턴 검토, MainWindow ctor 구조 분리)

---

**Total deviations:** 0 auto-fixed. CO-43-01 carry-over 1건(범위 외 발견, 별도 phase 이연).

## Issues Encountered

없음. Task 1~3 msbuild Debug/x64 PASS (0 errors, 0 신규 warning, Phase 42 베이스라인 6 warning 유지). Task 4 UAT 3회 평균 578ms READY, 55% 단축 PASS.

## UAT 결과 요약 (Task 4 — 사용자 승인)

로그 파일 `D:\Data\Trace\2026-06-15_Trace.log` 기준:

| 구분 | 수치 |
|------|------|
| Before (pre-change, 12:36) Step 5 delta | 804ms |
| Before READY 상당 시점 | ≈ 1285ms |
| After 3회 READY (13:27/13:28/13:29) | 567ms / 540ms / 628ms |
| After 평균 READY | ≈ 578ms |
| 단축률 | ≈ 55% (목표 ≥30% **PASS**) |
| [LOGIN] Preload complete | READY 후 ≈ 1s (임계 경로 외부, race 없음) |
| 회귀 (a) 첫 로그인 | 사용자 수용 (Preload complete 로그 확인) |
| 회귀 (b) 첫 $TEST | 사용자 수용 (명시 오류 없음) |

## Known Stubs

없음.

## Threat Flags

없음. 신규 네트워크 엔드포인트 / 인증 변경 / 신규 외부 입력 없음. T-43-01(race)/T-43-02(auth bypass)/T-43-03(DoS hang) 모두 계획대로 mitigate/accept 처리 완료.

## Next Phase Readiness

- Phase 43 완료 → CO-38-02/CO-38-03 공식 종결
- 다음: Phase 44 (실HW [STARTUP] 재측정, CO-38-04) — HW 도착 시 착수
- CO-43-01 (흰 화면) 신규 phase 신설 필요 — 우선순위 별도 결정

## Self-Check

커밋 존재 확인:
- `6d75145` — LoginManager 백그라운드 프리로드 인프라
- `81c1686` — SystemHandler Step 5 교체 + READY 마커
- `700355e` — LoginWindow EnsureLoaded + 통합 빌드

파일 존재 확인:
- `WPF_Example/Login/LoginManager.cs` — 존재 (수정됨)
- `WPF_Example/SystemHandler.cs` — 존재 (수정됨)
- `WPF_Example/UI/Login/LoginWindow.xaml.cs` — 존재 (수정됨)

## Self-Check: PASSED

---
*Phase: 43-startup-delay-separation*
*Completed: 2026-06-15*
