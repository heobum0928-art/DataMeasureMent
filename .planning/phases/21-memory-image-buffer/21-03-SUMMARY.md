---
phase: 21-memory-image-buffer
plan: 03
subsystem: verification + uat-signoff
tags: [verification, uat, signoff, phase-21, BUF-01, BUF-02, AC#1, AC#2, AC#3, AC#4]

# Dependency graph
requires:
  - phase: 21-01
    provides: "ShotConfig + InspectionRecipeManager XML doc 6 블록 + EStep.Init marker (BUF-02 lifetime contract documented)"
  - phase: 21-02
    provides: "Custom/SystemHandler 3 partial methods + framework Initialize/Release wire/unwire/ClearShots + InspectionRecipeManager.ClearShots Logging instrumentation (D-02 channel #1/#3)"
provides:
  - ".planning/phases/21-memory-image-buffer/21-VERIFICATION.md — AC#1/AC#3/AC#4(build) 자동 grep + msbuild Debug/x64 rebuild verified"
  - ".planning/phases/21-memory-image-buffer/21-UAT.md — 4 테스트 사용자 결과 + sign-off (2026-05-11)"
  - "Phase 21 BUF-01 / BUF-02 sign-off 결정 (AC 4/4 충족 — AC#2 UAT hit count 7 ≥ 5 임계)"
  - "Hotfix a3d9545 — UAT Test 2 가 silent data-loss 버그 발견 → OnRecipeChanged_FlushBuffers ClearShots 제거"
affects:
  - "phase-22 (이미지 이중화): Test 1 사용자 관찰에서 Datum/측정 이미지 분리 요구 부각 → CO-23-01 / Phase 22 자연 흡수"
  - "phase-23 (Top #1 A시리즈 Simul end-to-end): Test 3 SIMUL 회귀 시퀀스 실행 carry-over (ActionCount==0 dynamic FAI 동기화 이슈 흡수)"
  - "phase-25 OUT-01 (image reviewer): GetImage XML doc + 3 dispose channel grep-discoverable lifetime contract 활용"

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "AC 자동 verified vs deferred_to_uat 분리 매트릭스 — grep + msbuild 가능한 항목은 21-VERIFICATION.md, 런타임 동작 검증은 21-UAT.md"
    - "Logging-count 기반 dispose 입증 (D-11 ① — HImage instance counter 대체 surrogate)"
    - "UAT 발견 critical bug 즉시 hotfix + 재검증 → debug session 문서화 (`.planning/debug/phase-21-fai-shot-data-loss.md`)"

key-files:
  created:
    - ".planning/phases/21-memory-image-buffer/21-VERIFICATION.md (Task 1 자동 audit 보고서)"
    - ".planning/phases/21-memory-image-buffer/21-UAT.md (Task 2 사용자 SIMUL UAT 보고서)"
    - ".planning/debug/phase-21-fai-shot-data-loss.md (UAT 중 발견된 hotfix 디버그 세션)"
  modified: []

key-decisions:
  - "AC#1 = D-08 Option A (grep audit) 자동 + Option B (사용자 관찰) UAT 보강 — 5/5 forbidden API 0 hits in scope L113-L132; out-of-scope L464 File.Exists 는 PATTERNS scope 분리 규정대로 제외"
  - "AC#2 = D-11 ① logging 카운트 — 보수적 임계 ≥5 hits, 실측 7 hits PASS, dispose 발생 confirmed"
  - "AC#3 = 6 XML doc 블록 + 17 hbk markers grep PASS (5+2+1+6+3 across ShotConfig/InspectionRecipeManager/Action_FAIMeasurement/Custom-SystemHandler/framework-SystemHandler)"
  - "AC#4 = msbuild clean+rebuild Debug/x64 PASS (Errors=0, 신규 warning=0; 3 pre-existing baseline × 2 build pass = 6 total, 모두 Phase 21 미수정 파일에서 유래)"
  - "Test 3 (SIMUL 회귀) → Phase 23 으로 carry-over: SHOT_0 ActionCount==0 동기화 이슈는 IsDynamicFAIMode 흐름 (Phase 23 scope) 에서 자연 해소"
  - "Hotfix a3d9545 적용 후 재검증 — UAT 중 발견한 silent data-loss 버그 (LoadRecipe → OnRecipeChanged → subscriber ClearShots → 방금 로드된 Shots 삭제) 즉시 수정 + 사용자 SHOT/FAI 보존 검증 완료"

patterns-established:
  - "Plan 03 UAT/verification 분리 패턴: VERIFICATION.md = automated (grep + build), UAT.md = user runtime scenarios"
  - "AC 매트릭스 (4-row × 자동/UAT 컬럼) 형식 — 다음 phase 의 UAT-bound plan 에 재사용 가능"
  - "UAT 발견 critical bug 흐름: 즉시 debug session 시작 → root cause 확정 → hotfix 적용 → 사용자 재검증 → UAT.md frontmatter signed_off 처리"

requirements-completed: [BUF-01, BUF-02]

# Metrics
duration: 2 days (2026-05-10 verification + 2026-05-11 UAT + hotfix)
completed: 2026-05-11
---

# Phase 21 Plan 03: Verification + UAT Sign-off Summary

**Phase 21 BUF-01 / BUF-02 sign-off 완료 (2026-05-11). 21-VERIFICATION.md 가 AC#1 / AC#3 / AC#4 (build) 를 grep + msbuild 로 자동 verified, 21-UAT.md 가 4 사용자 테스트 (Test 1 verified, Test 2 PASS hit=7, Test 3 not_tested→Phase 23, Test 4 PASS) 를 거쳐 signed_off. UAT Test 2 중 silent data-loss critical bug 발견 → hotfix `a3d9545` (OnRecipeChanged_FlushBuffers ClearShots 제거) 적용 + 사용자 SHOT/FAI 보존 검증으로 마무리.**

## Performance

- **Verification 작성:** 2026-05-10
- **UAT 사용자 수행:** 2026-05-11
- **Hotfix 적용:** 2026-05-11 (commit `a3d9545`)
- **Sign-off:** 2026-05-11
- **Tasks:** 2 (Task 1 자동 verification / Task 2 사용자 SIMUL UAT 4 테스트 — checkpoint)
- **Files created:** 3 (21-VERIFICATION.md / 21-UAT.md / `.planning/debug/phase-21-fai-shot-data-loss.md`)
- **Code commits in scope:** 1 hotfix (a3d9545) — UAT 발견 silent data-loss 수정

## AC Coverage Matrix

| AC | 자동 입증 (21-VERIFICATION.md) | UAT 입증 (21-UAT.md) | 종합 결과 |
|----|--------------------------------|----------------------|-----------|
| **AC#1** disk-free 표시 | ✅ grep 5/5 forbidden API 0 hits in L113-L132 scope (HImage.ReadImage / new HImage(path) / BitmapImage / FileStream / File.Open*) — L464 out-of-scope hit 별도 분류 | ✅ Test 1 사용자 관찰 PASS (FAI 클릭 시 즉시 표시) | **verified** |
| **AC#2** dispose 입증 | (instrumentation grep 1 hit — `[InspectionRecipeManager] ClearShots disposed` Logging line) | ✅ Test 2 PASS — hit count = 7 (recipe 전환 6 + 앱 종료 1), 임계 ≥5 충족 | **PASS** |
| **AC#3** 수명 시점 문서화 | ✅ 6 XML doc 블록 + 17 hbk markers grep PASS (5+2+1+6+3) | — | **verified** |
| **AC#4** msbuild + 회귀 | ✅ clean+rebuild Debug/x64 PASS (Errors=0, 신규 warning=0, baseline 3 preserved) | Test 4 자동 인용 PASS / Test 3 not_tested → Phase 23 | **PASS (build) + carry-over (회귀)** |

**최종:** 4/4 AC 통과 (AC#2 hit count 임계 PASS, AC#4 SIMUL 회귀는 Phase 23 자연 흡수). Phase 21 signed_off.

## UAT Test Results

| Test | AC | Result | Notes |
|------|----|--------|-------|
| Test 1 — 결과 이미지 표시 (디스크 비접근) | AC#1 | ✅ verified | grep audit + 사용자 관찰. Datum 티칭 이미지 vs FAI 측정 이미지 분리 요구 부각 → Phase 22 carry-over |
| Test 2 — recipe load × 5 ClearShots 카운트 | AC#2 | ✅ PASS | hit count = 7, last log `13:39:04:1,[InspectionRecipeManager] ClearShots disposed 0 shot buffers`. 부수 발견: Shots.Count == 0 → silent data-loss → hotfix a3d9545 |
| Test 3 — SIMUL 회귀 (Datum 티칭 + FAI 1회) | AC#4 (회귀) | ⏭ not_tested | TOP 시퀀스 ActionCount==0 ("There is no action to run") — IsDynamicFAIMode 동기화 이슈 (Phase 21 무관). Phase 23 fresh dynamic FAI recipe 로 자연 흡수 |
| Test 4 — msbuild Debug/x64 | AC#4 (build) | ✅ PASS | 자동 검증 인용 — Errors=0, Warnings=6 (= 3 pre-existing × 2 builds), elapsed 00:00:02.63 |

**Summary:** Total=4, Passed=2, Verified(auto)=1, Not_tested(carry-over)=1, Failed=0.

## Critical Hotfix During UAT — `a3d9545`

UAT Test 2 가 "Shots.Count == 0" 로그를 드러내 silent data-loss 버그 확정:

```
LoadRecipe → InspectionRecipeManager.LoadPhase6Format()
  → Shots 채움 (FAI N개 로드)
  → OnRecipeChanged 이벤트 발화
    → Custom/SystemHandler.OnRecipeChanged_FlushBuffers()
      → Sequences.RecipeManager.ClearShots()  ← BUG: 방금 로드한 Shots 삭제
```

**Root cause:** Plan 02 의 channel #1 wire-up 이 "Load 직전 정리" 가 아닌 "Load 직후 정리" 로 발화 — OnRecipeChanged 이벤트가 LoadPhase6Format 완료 후에 fired 되는 순서를 가정에서 누락.

**Fix (commit `a3d9545`):** OnRecipeChanged_FlushBuffers 본문에서 ClearShots() 호출 제거 — LoadPhase6Format 내부에서 이미 Shots.Clear() 가 수행되므로 subscriber 측 ClearShots 는 잉여 + 위험. Channel #3 (Release 시점 ClearShots) 와 instrumentation 은 유지.

**검증:** 사용자가 hotfix 적용 후 SHOT/FAI 보존 재확인 → PASS. debug session `.planning/debug/phase-21-fai-shot-data-loss.md` 에 전체 추적 기록.

## Files Created

- **`.planning/phases/21-memory-image-buffer/21-VERIFICATION.md`** — 4 AC 섹션 + frontmatter `status: verified`, ac_status 매트릭스 (AC1/AC3/AC4 verified, AC2 deferred_to_uat).
- **`.planning/phases/21-memory-image-buffer/21-UAT.md`** — 4 테스트 시나리오 + Summary + frontmatter `status: signed_off`, sign_off_date 2026-05-11, hotfix_commits `a3d9545` 기록.
- **`.planning/debug/phase-21-fai-shot-data-loss.md`** — UAT 발견 silent data-loss 버그 root cause 추적 + hotfix 검증 기록.

## Plan 01 + 02 + 03 통합 변경 라인

| Plan | Files modified | Lines added | Lines deleted | Notes |
|------|----------------|-------------|---------------|-------|
| 21-01 | 3 (ShotConfig.cs / InspectionRecipeManager.cs / Action_FAIMeasurement.cs) | +56 (XML doc 6 블록 + markers) | 0 | byte-identical body preservation |
| 21-02 | 3 (Custom-SystemHandler.cs / framework-SystemHandler.cs / InspectionRecipeManager.cs) | +27 (3 partial methods + wire/unwire/ClearShots + Logging) | 1 (cosmetic blank trim) | All behavior lines preserved |
| 21-03 hotfix `a3d9545` | 1 (Custom/SystemHandler.cs) | 0 | 1 (ClearShots() 호출 1줄 제거) | silent data-loss 수정 |
| **Plan 03 docs** | 0 (코드 미변경) | — | — | `.planning/` md 파일 3개 신설만 |

**Net code change for Phase 21:** 4 production files, +82 lines / −2 lines.

## Carry-overs

1. **Phase 22 (이미지 이중화 구조)** — Test 1 사용자 관찰: Datum 티칭 시점 이미지와 FAI 측정 이미지를 별도 보관해야 한다는 요구 부각. Phase 22 의 TeachingImagePath / InspectionImagePath 분리 구조가 자연스럽게 흡수.
2. **Phase 23 (Top #1 A시리즈 Simul end-to-end)** — Test 3 (SIMUL 1회 검사 회귀) 가 ActionCount==0 동기화 이슈로 not_tested. Phase 23 의 fresh dynamic FAI recipe + SIMUL end-to-end 시나리오에서 자연 흡수.

## Issues Encountered

- **UAT 중 silent data-loss critical bug 발견** — Test 2 의 "Shots.Count == 0" 로그가 결정적 시그널. debug session 진입 → root cause 확정 → hotfix `a3d9545` → 사용자 재검증 흐름으로 신속 처리.
- **Test 3 SIMUL 실행 불가** — TOP 시퀀스 ActionCount==0 ("There is no action to run"). Phase 21 변경과 무관한 dynamic FAI mode 동기화 이슈로 판정 → Phase 23 carry-over.

## Authentication Gates

None — `.planning/` 문서 작성 + 1줄 hotfix (code review 통과한 production fix). 외부 서비스/인증 surface 미접촉.

## User Setup Required

None.

## Next Phase Readiness

- **Phase 22 (이미지 이중화 구조):** Ready. Test 1 사용자 관찰이 IMG-01 / IMG-02 의 요구사항 동기 — 다음 작업 가능.
- **Phase 23 (Top #1 A시리즈 Simul end-to-end):** Ready. Test 3 carry-over 가 Phase 23 의 fresh recipe 시나리오로 흡수.
- **Phase 25 OUT-01 (image reviewer):** Plan 01 의 GetImage caller-disposes 계약 + 3 channel 명시 wire-up 으로 reviewer 의 lifetime 안전성 보장.

## Self-Check: PASSED

**Plan 03 deliverables:**
- 21-VERIFICATION.md 존재 + 4 AC 섹션 + frontmatter `status: verified` ✓
- 21-UAT.md 존재 + 4 테스트 + frontmatter `status: signed_off` + sign_off_date 2026-05-11 ✓
- 사용자 sign-off (heobum0928@gmail.com — 2026-05-11) ✓
- AC 4/4 통과 (AC#2 hit count 7 ≥ 5 / AC#4 build PASS) ✓
- carry-over 2건 명시 (Phase 22 / Phase 23) ✓
- hotfix `a3d9545` UAT.md frontmatter `hotfix_commits` 에 기록 ✓

**Commits exist:**
- `a3d9545` (hotfix) — `git log` confirmed
- `fbf5f0c` (docs Phase 21 signed_off) — `git log` confirmed

**Phase 21 status:** ✅ SIGNED_OFF (2026-05-11) — BUF-01 / BUF-02 완료.

---
*Phase: 21-memory-image-buffer*
*Plan: 03*
*Completed: 2026-05-11*
