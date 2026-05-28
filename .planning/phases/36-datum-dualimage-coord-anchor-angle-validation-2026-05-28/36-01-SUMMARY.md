---
phase: 36-datum-dualimage-coord-anchor-angle-validation-2026-05-28
plan: 01
subsystem: vision-algorithm
tags: [phase36, datum, dualimage, sameframe, guard, halcon, vision]

requires:
  - phase: 34-datum-verticaltwohorizontal-2026-05-26
    provides: VerticalTwoHorizontalDualImage 알고리즘 + DualImage Find/Teach 2-image 오버로드 (DatumFindingService L71, L796)
  - phase: 34.1-datum-dualimage-swap-ux-2026-05-27
    provides: CO-34.1-09 carry-over (좌표계 통합 부재 진단 — Phase 36 으로 종결)
provides:
  - SameFrame 런타임 가드 (Find DualImage 오버로드 L86-94 + Teach DualImage 오버로드 L818-826)
  - DualImage 두 이미지 width/height 일치 계약의 코드 레벨 잠금 (D-36-01/02/03 명시화)
  - "DualImage requires same-frame image pair: ..." 영문 진단 메시지 (Find/Teach 동일 포맷)
affects:
  - phase 36-02 (Datum 입력 필드 ExpectedAngleDeg/AngleTolerance — 본 가드 통과 후 알고리즘 단에서 wrapping)
  - phase 36-03 (HalconDisplay 시각화 fallback — 본 가드가 검증한 sensor frame 안에서 OFF-SCREEN 판정)
  - phase 36-04 (사용자 UAT — 실측 페어 동일 사이즈 보장 시 가드 트리거 없음 / 다른 사이즈 시 에러 메시지 노출)

tech-stack:
  added: []
  patterns:
    - "진입부 SameFrame 가드 — null 가드 직후, EnsurePerRoiDefaults 직후, algorithm 가드 직전 위치 고정"
    - "Find/Teach 1:1 대칭 가드 미러링 — 에러 메시지 byte-identical (사용자 진단 일관성)"

key-files:
  created: []
  modified:
    - WPF_Example/Halcon/Algorithms/DatumFindingService.cs

key-decisions:
  - "D-36-03 가드 위치: null 가드 직후, EnsurePerRoiDefaults 직후, algorithm 가드 직전 — GetImageSize NullReferenceException 안전 + LastFindSucceeded 가 false 유지"
  - "Find/Teach 양쪽 오버로드에 byte-identical 에러 메시지 — 사용자 진단 시 동일 메시지로 원인 파악 일관성"
  - "단일-이미지 TryFindDatum / TryTeachDatum 오버로드 본문 변경 0 라인 — 1-image 알고리즘 3종 (TwoLineIntersect / VerticalTwoHorizontal / CircleTwoHorizontal) 회귀 표면 없음 (D-36-04 정신)"
  - "에러 메시지 한국어 변환은 UI 노출 site (MainView.BtnTestFindDatum_Click FormatFindError) 책임 — DatumFindingService 내부는 영문 유지"

patterns-established:
  - "SameFrame 가드 패턴: HTuple wH, hH, wV, hV 선언 → GetImageSize × 2 → wH.I/hH.I 비교 → 에러 + return false"
  - "Phase 22 IMG-01 / D-34-11 패턴 정신 답습 — algorithm 핵심 진입부에 가드 추가, 단일-이미지 경로 영향 0"

requirements-completed: [SC-36-5, D-36-01, D-36-02, D-36-03, D-36-04]

duration: 18 min
completed: 2026-05-28
---

# Phase 36 Plan 01: SameFrame Guard Insertion Summary

**DualImage Find/Teach 오버로드 진입부에 SameFrame width/height 가드 7-라인 블록 1:1 대칭 삽입 — CO-34.1-09 "좌표계 통합 부재" 진단의 첫 번째 코드 가드 (D-36-01/02 SameFrame 계약 잠금)**

## Performance

- **Duration:** 18 min
- **Started:** 2026-05-28
- **Completed:** 2026-05-28
- **Tasks:** 2 / 2 (Find + Teach)
- **Files modified:** 1 (DatumFindingService.cs)

## Accomplishments

- Find DualImage 오버로드 (L71-103) 진입부 SameFrame 가드 7-라인 삽입 (commit 422c6f6)
- Teach DualImage 오버로드 (L807-835) 에 1:1 대칭 가드 미러링 (commit 998e327)
- msbuild Debug/x64 PASS — baseline 14 warnings 와 동일, 신규 warning 0
- 단일-이미지 TryFindDatum (L17-63) / TryTeachDatum (L765-789) 본문 변경 0 라인 — 1-image 회귀 표면 0
- 에러 메시지 byte-identical (Find/Teach 양쪽 `"DualImage requires same-frame image pair: horizontal WxH vs vertical WxH"`)

## Task Commits

1. **Task 1: TryFindDatum DualImage 오버로드 SameFrame 가드 삽입** — `422c6f6` (feat)
2. **Task 2: TryTeachDatum DualImage 오버로드 SameFrame 가드 미러링 + msbuild PASS** — `998e327` (feat)

## Files Created/Modified

- `WPF_Example/Halcon/Algorithms/DatumFindingService.cs` — Find DualImage 오버로드 (L71-103) 와 Teach DualImage 오버로드 (L807-835) 진입부에 SameFrame width/height 가드 블록 각 7-라인 삽입. 두 가드는 동일 패턴 (HTuple wH/hH/wV/hV 선언 → GetImageSize × 2 → wH.I/hH.I 비교 → "DualImage requires same-frame image pair: ..." 에러 + return false).

## Decisions Made

- **가드 위치**: null 가드 통과 직후 + EnsurePerRoiDefaults / LastFindSucceeded 초기화 직후 + algorithm 가드 직전. 이유: (1) GetImageSize 호출이 NullReferenceException 안전, (2) 가드 실패 시 LastFindSucceeded 가 이미 false 상태 유지, (3) algorithm 가드 분기 이전 — DualImage 외 알고리즘에 영향 0.
- **에러 메시지 영문 유지**: DatumFindingService 는 내부 진단 로그 영역. 한국어 메시지 변환은 UI 노출 site (MainView.BtnTestFindDatum_Click FormatFindError) 책임. PATTERNS L575-578 한국어 정책 정합.
- **단일-이미지 overload 본문 변경 0**: D-36-04 의 "가드 4파일 변경 0" 정신을 코드 가드 표면 최소화로 확장. 1-image algorithm 3종 회귀 표면 없음.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Acceptance Criteria Compliance] Leading comment grep 호환 위해 코멘트 prefix 통일**
- **Found during:** Task 1 (TryFindDatum DualImage 가드 삽입 후 acceptance grep 검증)
- **Issue:** Plan 라인 105의 leading 코멘트 두 줄이 `// 260528 hbk Phase 36 D-36-03` (공백 포함) 형식이라 `grep "//260528"` (공백 없음) 매치에서 누락 → "≥ 7 라인" 기준 미달 위험 (실제 6개 매치).
- **Fix:** 두 leading 코멘트 라인의 prefix 를 `//260528` (공백 없음) 으로 통일하여 plan/PATTERNS 의 inline 주석 패턴과 grep 일관성 확보.
- **Files modified:** WPF_Example/Halcon/Algorithms/DatumFindingService.cs (Task 1 commit 내 보정)
- **Verification:** `grep -c "//260528 hbk Phase 36 D-36-03" WPF_Example/Halcon/Algorithms/DatumFindingService.cs` → 8 (Find 가드만, Task 2 후 총 15)
- **Committed in:** 422c6f6 (Task 1 commit)

**2. [Rule 3 - Blocking] Worktree base reset to main HEAD**
- **Found during:** Init context phase
- **Issue:** Worktree branch `worktree-agent-ad911fd437dbf05c4` 가 5727bd8 (v1.0 milestone cleanup) 기준으로 생성되어 있어서, Phase 36 자료 (.planning/phases/36-...) 가 worktree 디렉토리에 존재하지 않음. main HEAD (3e948ea) 가 EXPECTED_BASE.
- **Fix:** `git reset --hard 3e948ea` 로 worktree branch 를 main HEAD 로 정합.
- **Files modified:** (worktree state 만 영향, 신규 commit 없음)
- **Verification:** `.planning/phases/36-datum-dualimage-coord-anchor-angle-validation-2026-05-28/36-01-PLAN.md` 등 phase 자료 worktree 에 노출 확인.
- **Committed in:** (state reset 만, commit 아님)

**3. [Rule 3 - Blocking] Missing bin/x64/Debug DLLs for msbuild**
- **Found during:** Task 2 (msbuild 첫 실행 시 6 XAML MC3074 에러 발생)
- **Issue:** csproj 가 `bin\x64\Debug\PropertyTools.Wpf.dll` 등 HintPath 로 외부 어셈블리 참조 (PropertyTools.Wpf, PropertyTools, WPF.MDI, Basler.Pylon, MvCamCtrl.Net). Worktree 의 `WPF_Example/bin/x64/Debug/` 가 비어 있어 XAML 컴파일러 (MarkupCompilePass1) 가 어셈블리를 찾지 못함. Baseline (HEAD~2) 빌드에서도 동일 에러 → pre-existing 환경 이슈 (Phase 36 변경과 무관) 확인.
- **Fix:** main repo `C:\Info\Project\DataMeasurement\WPF_Example\bin\x64\Debug` 에서 5개 DLL 복사 (PropertyTools.Wpf.dll, PropertyTools.dll, WPF.MDI.dll, Basler.Pylon.dll, MvCamCtrl.Net.dll).
- **Files modified:** worktree bin 디렉토리 (build 출력 영역 — git tracked 아님)
- **Verification:** 빌드 재실행 결과 exit 0, errors 0, warnings 14 (baseline 동일).
- **Committed in:** (build 환경 정합만, commit 아님)

---

**Total deviations:** 3 auto-fixed (1 acceptance compliance, 2 blocking environment).
**Impact on plan:** 모두 plan 의도 보존 / 환경 정합. 코드 변경 의도와 충돌 없음. Plan 의 7-라인 가드 블록 패턴 정확히 삽입.

## Issues Encountered

- Worktree 가 main HEAD 보다 한참 뒤떨어진 base 에서 생성됨 (5727bd8 vs 3e948ea). 사용자 지침에 따라 reset 진행 — `EnterWorktree creates branches from main instead of feature branch HEAD` 알려진 이슈와 정합. resolution: `git reset --hard {EXPECTED_BASE}`.
- msbuild XAML 컴파일이 외부 DLL HintPath (bin/x64/Debug/) 에 의존 — worktree 빈 bin 폴더로 인해 6 XAML MC3074 에러. Baseline 빌드도 동일 에러 발생 확인으로 pre-existing 환경 이슈 진단. 5 DLL 복사로 해소. **본 이슈는 Phase 36 변경과 무관 — 차후 worktree 빌드 환경 표준화 검토 권고 (별도 todo 후보)**.

## User Setup Required

None — 빌드 외 외부 서비스 설정 불요.

## Next Phase Readiness

- **Wave 1 / Plan 01 완료**, Plan 02 (입력 필드 ExpectedAngleDeg/AngleTolerance + transient AngleValidationStatus + Hide 분기) 실행 준비 완료.
- Wave 2 (Plan 02 + Plan 03 병렬 가능 가설) 진입 시 본 가드가 후속 알고리즘 단의 SameFrame 가정을 잠금 — 후속 plan 들은 sensor frame 일치 전제 안에서 추가 검증 로직 작성.
- 사용자 UAT (Plan 04) 에서 다른 사이즈 페어 시도 시 본 에러 메시지가 사용자 진단의 첫 번째 단서가 됨.

## Acceptance Criteria Verification

### Task 1 (Find DualImage 가드)

| Criterion | Result | Evidence |
|---|---|---|
| `grep "//260528 hbk Phase 36 D-36-03"` ≥ 7 | PASS (8 매치) | leading 2 + inline 6 |
| `grep "DualImage requires same-frame image pair:"` = 1 | PASS (1, Find 만) | Task 1 시점 측정 |
| `grep "TryFindDatum(HImage image,"` = 1 | PASS (1) | 단일-이미지 시그니처 그대로 |
| 가드 블록 위치 = algorithm 가드 직전 | PASS | git diff L86-94 확인, 수동 리뷰 |

### Task 2 (Teach DualImage 가드 + msbuild)

| Criterion | Result | Evidence |
|---|---|---|
| `grep -c "DualImage requires same-frame image pair:"` = 2 | PASS (2) | Find + Teach 각 1회 |
| `grep "//260528 hbk Phase 36 D-36-03"` ≥ 14 | PASS (15) | 8 (Find) + 7 (Teach) |
| msbuild Debug/x64 PASS, exit 0 | PASS | 환경 보정 후 exit 0 |
| 신규 warning 0 vs baseline | PASS (14 = 14) | baseline 14 vs 변경 후 14 |
| 단일-이미지 TryTeachDatum 본문 변경 0 | PASS | git diff 검토 — L765-789 본문 무변경 |

## Self-Check: PASSED

---
*Phase: 36-datum-dualimage-coord-anchor-angle-validation-2026-05-28*
*Plan: 01 (Wave 1)*
*Completed: 2026-05-28*
