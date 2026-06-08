---
phase: 37-side-multi-datum-dualimage-2026-05-28
plan: 03
subsystem: inspection-ui
tags: [datum, dualimage, multi-datum, multi-shot, uat, simul]
requires:
  - phase: 37-01
    provides: lenient TryRunDatumPhase (부분 성공 = true)
  - phase: 37-02
    provides: EStep.DatumPhase per-datum loop + ClearDatumTransforms/TryRunSingleDatum 누적 경로
provides:
  - 기존 UI 흐름으로 4-datum DualImage 생성/티칭 가능 확인 (코드 변경 0, D-37-07)
  - SIMUL UAT 4/4 PASS — 다중 datum 독립 실행 + 측정 ≠ datum 이미지 + 예외 robustness
  - multi-Shot 측정 이미지/ROI 해석 객체참조 전환 (2 hotfix)
affects:
  - 이후 Side 검사 실행/시각화 (multi-Shot 동일 FAI명 충돌 해소 토대)
tech-stack:
  added: []
  patterns:
    - "측정 노드 이미지/ROI 해석: FAI 이름 round-trip → 측정 객체 참조(ReferenceEquals) — multi-Shot 동일 FAI명 충돌 회피"
    - "FindFAIContainingMeasurement: RecipeManager.Shots(동적 FAI 단일 소스) 우선 탐색, pSeq fallback — 세션 중 신규 Shot 즉시 반영"
key-files:
  created:
    - .planning/phases/37-side-multi-datum-dualimage-2026-05-28/37-03-SUMMARY.md
    - .planning/phases/37-side-multi-datum-dualimage-2026-05-28/37-UAT.md
  modified:
    - WPF_Example/UI/ContentItem/MainView.xaml.cs
key-decisions:
  - "D-37-07: 신규 UI 최소화 — 기존 AddDatum/PropertyGrid 흐름으로 4-datum DualImage 생성/티칭 가능 확인, InspectionListView.xaml.cs 코드 변경 0"
  - "UAT hotfix A: Measurement 노드 이미지/ROI 해석을 FAI 이름 round-trip → 측정 객체 참조(FindFAIContainingMeasurement, ReferenceEquals) — multi-Shot 동일 FAI명(기본 FAI_0) 충돌 직접 원인 해소"
  - "UAT hotfix B: FindFAIContainingMeasurement 가 RecipeManager.Shots 우선 탐색 — 세션 중 추가한 새 Shot 측정 이미지/ROI 미추종 해소 (AddShotToSequence 지연 동기화 갭)"
patterns-established:
  - "이름 기반 round-trip 보다 객체 참조 / 동적 단일 소스 탐색 우선 — multi-Shot 시나리오 충돌 방지"
requirements-completed: []
duration: 1일 (checkpoint UAT 포함)
completed: 2026-05-28
---

# Phase 37 Plan 03: 다중 Datum 생성/티칭 확인 + SIMUL UAT Summary

**기존 Datum 추가 UI 흐름으로 Side fixture 에 4-datum DualImage 생성/티칭 가능함을 코드로 확인(변경 0, D-37-07)하고, SIMUL UAT 4항목 전부 PASS — UAT 중 발견된 multi-Shot 동일 FAI명 충돌(A) + 세션 중 새 Shot 미추종(B) 2건을 객체참조/단일소스 탐색으로 수정.**

## Performance

- **Tasks:** 2 (Task 1 코드 검증, Task 2 human-verify UAT)
- **Files modified:** 1 (MainView.xaml.cs — 2 hotfix)
- **Completed:** 2026-05-28

## What Was Verified

- **Task 1 — datum 4개 DualImage 생성/티칭 흐름 (코드 변경 0, D-37-07)**
  - `AddDatumToSequence` → `inspSeq.AddDatum(datumName)` 호출 경로가 호출마다 새 `DatumConfig` 를 추가 — 4회 반복으로 datum 4개 생성 가능함을 코드 인용으로 확인.
  - `DatumConfig` ICustomTypeDescriptor 가 `VerticalTwoHorizontalDualImage` 선택 시 `TeachingImagePath` + `TeachingImagePath_Vertical` 를 둘 다 노출함을 확인.
  - 막힘 없음 → **InspectionListView.xaml.cs 코드 변경 0** (신규 UI 최소화 D-37-07). 기존 흐름만으로 4-datum DualImage 생성/티칭 가능 (commit fd5bce1).

- **Task 2 — SIMUL UAT 4항목 (사용자 확인)**
  - 항목 1 (datum 독립 실행): **PASS** — Datum 4개 + Shot 에서 side datum 1 참조 측정 정상.
  - 항목 2 (4-datum mixed 개별 검출): **PASS** — datum 4개 각자 자기 이미지로 검출, DatumConfigs[0] 단일 묶임 없음.
  - 항목 3 (측정 이미지 ≠ datum 이미지, D-37-06): **PASS** — Shot 이미지 로드 시 measurement 가 해당 이미지를 따라감.
  - 항목 4 (예외 robustness, T-37-06): **PASS** — 빈/잘못된 datum 티칭 경로 시 crash 없이 skip+log.

## Files Created/Modified

- `WPF_Example/UI/ContentItem/MainView.xaml.cs` — UAT 중 2 hotfix (측정 노드 이미지/ROI 해석 객체참조 전환 + RecipeManager.Shots 우선 탐색)
- `.planning/phases/37-side-multi-datum-dualimage-2026-05-28/37-UAT.md` — UAT 결과 (signed_off, 4/4 PASS)

## Decisions Made

- **D-37-07**: 신규 UI 최소화. 기존 AddDatum/PropertyGrid 흐름으로 4-datum DualImage 생성/티칭 가능 → Task 1 코드 변경 0.
- **UAT hotfix A**: 측정 노드 이미지/ROI 해석을 이름 round-trip → 객체 참조로. multi-Shot 동일 FAI명 충돌의 직접 원인 제거.
- **UAT hotfix B**: `FindFAIContainingMeasurement` 가 `RecipeManager.Shots`(동적 FAI 단일 소스) 우선 탐색. 세션 중 신규 Shot 즉시 반영, `pSeq` 는 fallback.

## Deviations from Plan

본 plan 은 Task 1 = "막힘 없으면 코드 변경 0" 을 명시했고 실제로 변경 0 으로 확정됨 (plan 대로). UAT(Task 2) 중 다음 2건의 잠복 결함이 표면화되어 동일 세션 내 자동 수정 — Phase 37 의 multi-Shot/다중 datum 구조 도입으로 드러난 기존 결함이며 plan 목표(측정 이미지 ≠ datum 이미지, D-37-06) 달성에 필수적인 수정.

### Auto-fixed Issues

**1. [Rule 1 - Bug] multi-Shot 동일 FAI명 충돌 → Shot2 측정 선택 시 Shot1 이미지 표시**
- **Found during:** Task 2 UAT (항목 3 측정 이미지 ≠ datum 이미지 검증)
- **Issue:** Measurement 노드 이미지/ROI 해석이 FAI 이름 round-trip(`FindFAIByName`)에 의존. 여러 Shot 의 FAI 이름이 같으면(기본 `FAI_0`) 첫 Shot 의 FAI 가 반환되어 Shot2 측정 선택 시 Shot1 이미지가 표시됨.
- **Fix:** 이름 round-trip 대신 측정 객체 참조(`FindFAIContainingMeasurement`, `ReferenceEquals`)로 변경. `HighlightSelectedRoi` 의 `anchorFai` 해석도 동일 원인 함께 수정.
- **Files modified:** WPF_Example/UI/ContentItem/MainView.xaml.cs
- **Verification:** 사용자 SIMUL 재검증 PASS
- **Committed in:** 1c11c35

**2. [Rule 1 - Bug] 세션 중 추가한 새 Shot 측정 이미지/ROI 미추종**
- **Found during:** Task 2 UAT (hotfix A 적용 후 동일 항목 재검증, 세션 중 신규 Shot 추가 시나리오)
- **Issue:** `AddShotToSequence` 가 새 Shot 을 `RecipeManager` 에만 추가하고 라이브 Action(`pSeq`)은 실행 시 지연 동기화 → 세션 중 새 Shot 의 측정을 `pSeq` 에서 못 찾아 이전 Shot 이미지/ROI 가 남음(재시작 후엔 정상).
- **Fix:** `FindFAIContainingMeasurement` 가 `RecipeManager.Shots`(동적 FAI 단일 소스, 신규 Shot 즉시 반영)를 우선 탐색하고 `pSeq` 는 fallback.
- **Files modified:** WPF_Example/UI/ContentItem/MainView.xaml.cs
- **Verification:** msbuild Debug/x64 PASS, 신규 warning 0. 사용자 SIMUL 재검증 PASS
- **Committed in:** c6576e5

---

**Total deviations:** 2 auto-fixed (2 Rule 1 bugs)
**Impact on plan:** 두 hotfix 모두 D-37-06(측정 이미지 ≠ datum 이미지) 달성에 필수. multi-Shot 구조에서 표면화된 기존 잠복 결함 해소로, scope creep 없음.

## Issues Encountered

UAT 중 발견·수정한 2 hotfix 외 추가 이슈 없음. 상세는 Deviations 및 37-UAT.md Gaps 참조.

## User Setup Required

None - 추가 외부 서비스 설정 없음.

## Next Phase Readiness

- Phase 37 코드 3 plan(01/02/03) 전부 머지 + UAT signed_off. CO-36-06 / CO-36-07 (Side 4-datum DualImage 구조) 종결.
- multi-Shot 동일 FAI명 충돌 해소로 다중 Shot 검사 시각화 토대 마련.

## Commits

- fd5bce1: docs(37-03) Task 1 완료(코드 변경 0, D-37-07) + Task 2 checkpoint 위치 기록
- 1c11c35: fix(37) Measurement 노드 이미지/ROI 해석을 이름 대신 객체 참조로 (multi-Shot 동일 FAI명 충돌)
- c6576e5: fix(37) 신규 Shot 측정 이미지/ROI 해석을 RecipeManager.Shots 우선 탐색

## Self-Check: PASSED

- FOUND: .planning/phases/37-side-multi-datum-dualimage-2026-05-28/37-03-SUMMARY.md
- FOUND: .planning/phases/37-side-multi-datum-dualimage-2026-05-28/37-UAT.md
- FOUND: WPF_Example/UI/ContentItem/MainView.xaml.cs
- FOUND: commit fd5bce1
- FOUND: commit 1c11c35
- FOUND: commit c6576e5

---
*Phase: 37-side-multi-datum-dualimage-2026-05-28*
*Completed: 2026-05-28*
