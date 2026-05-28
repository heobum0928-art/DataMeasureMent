---
phase: 38-v1-1-carryover-cleanup-2026-05-28
plan: 03
subsystem: infra
tags: [startup, profiling, stopwatch, uat, systemhandler]

requires:
  - phase: 38-01
    provides: 측정타입 정리 + PixelResolution 로딩 정규화
  - phase: 38-02
    provides: DatumConfig 각도 UI/ReuseFromShotName 정리
provides:
  - SystemHandler.Initialize() 8단계 [STARTUP] Stopwatch 누적+delta 계측 로그
  - 38-STARTUP-ANALYSIS.md — 시작 지연 주범 2개 식별 + carry-over 분류
  - 38-UAT.md — phase 38 전체 6항목 UAT sign-off (5 PASS / 1 carry-over)
affects: [startup-performance, v1.1-closeout]

tech-stack:
  added: [System.Diagnostics.Stopwatch]
  patterns: [Initialize 단계별 누적+delta 계측 로그 (영구 회귀 감시용)]

key-files:
  created:
    - .planning/phases/38-v1-1-carryover-cleanup-2026-05-28/38-STARTUP-ANALYSIS.md
    - .planning/phases/38-v1-1-carryover-cleanup-2026-05-28/38-UAT.md
  modified:
    - WPF_Example/SystemHandler.cs

key-decisions:
  - "D-14 적용: 시작 지연 저위험 개선 미적용 — 주범 2개(LoginManager/SequenceHandler) 모두 구조적/고위험 → carry-over (회귀 위험 0 우선)"
  - "UAT 5(픽셀분해능 단일소스 UI)는 D-10 범위(로딩 정규화) 달성, 런타임/UI 단일소스 기대분은 CO-38-01 carry-over (사용자 결정)"

patterns-established:
  - "계측 패턴: Logging.PrintLog((int)ELogType.Trace, \"[STARTUP] Step N ...: {0} ms (cumulative), delta {1} ms\", sw.ElapsedMilliseconds, sw.ElapsedMilliseconds - prev)"

requirements-completed: []

duration: ~15min (계측+빌드) + 사용자 UAT
completed: 2026-05-28
---

# Phase 38 / Plan 03: 시작 프로파일링 + 전체 UAT Summary

**SystemHandler.Initialize 단계별 Stopwatch 계측으로 시작 지연 주범 2개(LoginManager 808ms·SequenceHandler 550ms)를 정량 식별하고, phase 38 전체 6항목 UAT 를 5 PASS / 1 carry-over 로 sign-off.**

## Performance

- **Duration:** Task 1 약 15분(계측 작성+msbuild 검증) + Task 2 사용자 SIMUL UAT
- **Completed:** 2026-05-28
- **Tasks:** 2/2 (Task 1 auto, Task 2 checkpoint:human-verify)
- **Files modified:** 1 (SystemHandler.cs) + 2 문서 생성

## Accomplishments

### Task 1 — Initialize() 단계별 Stopwatch 계측 (commit 3c99f66)
- `using System.Diagnostics;` 추가, `Initialize()` 진입부 `Stopwatch.StartNew()` + `long prev=0`.
- 8개 단계 각 완료 시 `[STARTUP] Step N {name}: {cumulative} ms, delta {delta} ms` 로그 + Total 합계 → 총 9줄.
- 모든 계측 라인 `//260528 hbk Phase 38 #11` 마커. 단계 호출 순서/인자 무변경(계측 래핑만).
- D-14 저위험 개선: 실측 결과 주범 2개 모두 구조적 → **미적용, 전부 carry-over**.
- msbuild Debug/x64 PASS, 신규 warning 0 (기존 CS0618/CS0162/MSB3884 만).

### Task 2 — 시작 지연 측정 + UAT (checkpoint:human-verify)
- 사용자 SIMUL_MODE 실측: Total 1509ms, Step5 LoginManager(808ms)+Step2 SequenceHandler(550ms)=약 90%.
- 38-STARTUP-ANALYSIS.md 작성 (원인 2개 + 저위험 개선 미적용 사유 + CO-38-02/03/04).
- 38-UAT.md sign-off: UAT 1~4/6 PASS, UAT 5 → CO-38-01 carry-over.

## Carry-over

- **CO-38-01** — #5 픽셀분해능 단일소스 UI (Shot 단일값 일괄적용 + 항목별 UI 정리) — 신규 phase 후보
- **CO-38-02** — LoginManager 초기화 808ms lazy-load 검토
- **CO-38-03** — SequenceHandler 초기화 550ms 동기 의존성 분석
- **CO-38-04** — 실 하드웨어 [STARTUP] 재측정

## Self-Check: PASSED
- [x] Task 1 계측 커밋(3c99f66), [STARTUP] 9줄, msbuild PASS / 신규 warning 0
- [x] 38-STARTUP-ANALYSIS.md 지연 원인 ≥1개 + 개선/carry-over 분류
- [x] 38-UAT.md 6항목 결과 + sign-off (5 PASS / 1 carry-over)
