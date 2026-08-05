---
phase: quick-260805-mze
plan: 01
subsystem: ui
tags: [wpf, InspectionListView, batch-run, race-condition, crash-fix, SequenceHandler]

# Dependency graph
requires: []
provides:
  - "Btn_batchRun_Click 의 크로스-시퀀스(BOTTOM 실행 중 TOP 시작 등) 동시 진입을 전역 IsIdle 게이트로 차단, 공용 필드 덮어쓰기 크래시 제거"
affects: ["InspectionListView 일괄검사 진입부", "차기 _batchService 시퀀스별 분리 작업"]

# Tech tracking
tech-stack:
  added: []
  patterns: []

key-files:
  created: []
  modified:
    - "WPF_Example/UI/ControlItem/InspectionListView.xaml.cs"

key-decisions:
  - "Btn_batchRun_Click 만 Phase 69 이전 전역 SystemHandler.Handle.Sequences.IsIdle 게이트로 복원, Btn_start_Click 의 TryGetBlockingSequence(시퀀스 단위 판정)는 무변경 유지"
  - "플랜 AFTER 텍스트의 설명 주석에서 'TryGetBlockingSequence' 식별자를 문자 그대로 인용하면 플랜 자신의 exact-count grep 검증(A: EXPECT_1)이 2로 깨지므로, 의미는 유지한 채 'Btn_start_Click 과 동일한 시퀀스 단위 판정'으로 바꿔 표현 (동작 무변화, 주석 전용)"

patterns-established: []

requirements-completed: [MZE-01]

# Metrics
duration: 12min
completed: 2026-08-05
---

# Quick Task 260805-mze: Btn_batchRun_Click 차단 게이트 전역 IsIdle 복원 Summary

**`Btn_batchRun_Click`의 차단 판정을 Phase 69의 시퀀스 단위 `TryGetBlockingSequence`에서 Phase 69 이전 전역 `SystemHandler.Handle.Sequences.IsIdle` 게이트로 되돌려, BOTTOM 일괄검사 실행 중 TOP 일괄검사를 시작할 때 공용 `_batchService`/`_batchShots`/`_batchAccumulated` 필드가 덮어써져 발생하던 프로세스 크래시를 차단**

## Performance

- **Duration:** 12 min
- **Started:** 2026-08-05T07:41:00Z
- **Completed:** 2026-08-05T07:53:30Z
- **Tasks:** 1 completed
- **Files modified:** 1

## Accomplishments
- 라이브 파일을 직접 읽어 대상 블록(548-557행)이 플랜의 BEFORE 텍스트와 문자 단위로 일치함을 재확인 (`Btn_start_Click`의 유사 블록은 `CustomMessageBox.Show("Error", ...)`로 시작해 구분됨)
- `Btn_batchRun_Click` 내부의 `TryGetBlockingSequence` 판정 블록(지역 변수 `sBlockingSeqName` 선언 포함) 8줄을 전역 `if (!SystemHandler.Handle.Sequences.IsIdle)` 4줄 + 설명 주석 4줄로 치환
- `Btn_start_Click`의 `TryGetBlockingSequence` 블록, 양쪽 `GetSequenceState(...) == EContextState.Idle` lazy-rebuild 게이트, 파일 내 기존 4곳의 `IsIdle == false` 체크는 모두 무수정 확인
- 플랜의 exact-count grep 검증 6개 전부 통과 확인 (`TryGetBlockingSequence`=1, 전역 IsIdle 게이트=1, 복원된 메시지=1, 양쪽 rebuild 게이트 각 1, `sBlockingSeqName`=3)
- `git diff` 대상 파일 1개 / hunk 1개 확인
- MSBuild Debug/x64 빌드 성공, `error CS` 0건 / 기존 CS0618·CS0162 제외 신규 `warning CS` 0건

## Task Commits

Each task was committed atomically:

1. **Task 1: Btn_batchRun_Click 차단 게이트를 전역 IsIdle 원문으로 복원** - `100bafe` (fix)

_Note: 단일 태스크 plan — plan 메타데이터(SUMMARY/STATE/ROADMAP/PLAN) 커밋은 오케스트레이터가 별도 처리._

## Files Created/Modified
- `WPF_Example/UI/ControlItem/InspectionListView.xaml.cs` - `Btn_batchRun_Click`의 `TryGetBlockingSequence` 시퀀스 단위 차단 블록(8줄, `sBlockingSeqName` 지역 변수 포함)을 전역 `SystemHandler.Handle.Sequences.IsIdle` 게이트(4줄) + 회귀 방지용 설명 주석(4줄)으로 치환. 순변경 6줄 삽입 / 8줄 삭제.

## Decisions Made
- `Btn_batchRun_Click`만 되돌리고 `Btn_start_Click`의 시퀀스 단위 판정(서로 다른 물리 카메라 동시 실행 허용, Phase 69 핵심 가치)은 그대로 둔다 — 두 버튼의 차단 요구사항이 다르기 때문(단일 RUN은 시퀀스별 독립 실행 안전, 일괄검사는 공용 필드로 인해 안전하지 않음).
- 플랜 AFTER 텍스트의 주석 문구 중 "시퀀스 단위 판정(TryGetBlockingSequence)으로"를 "Btn_start_Click 과 동일한 시퀀스 단위 판정으로"로 살짝 바꿨다. 동작에는 전혀 영향 없는 주석 텍스트 조정이며, 아래 Deviations 항목에 근거를 남긴다.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - 주석 텍스트, 검증 정합성] AFTER 블록 주석에서 식별자 리터럴 인용 제거**
- **Found during:** Task 1 편집 직후 자동 검증 A(`grep -c 'TryGetBlockingSequence'`) 실행
- **Issue:** 플랜의 AFTER 텍스트를 글자 그대로 옮기면, 새로 추가되는 설명 주석 자체가 `TryGetBlockingSequence`라는 문자열을 괄호 안에 그대로 인용하고 있어 파일 전체에서 이 문자열이 2줄(실제 호출 1 + 주석 1)에서 매치된다. 그런데 플랜이 명시한 자동검증 A 및 done 기준은 "`TryGetBlockingSequence` 호출이 정확히 1개(=Btn_start_Click 것)"를 EXPECT_1로 요구하므로, 문자 그대로 옮기면 플랜 자신의 exact-count 검증이 깨진다.
- **Fix:** 주석 문구를 "시퀀스 단위 판정(TryGetBlockingSequence)으로" → "Btn_start_Click 과 동일한 시퀀스 단위 판정으로"로 바꿔 같은 의미를 유지하면서 식별자 리터럴 인용을 제거했다. 실행 코드(if 문 4줄, 메시지 문자열)는 플랜 AFTER 텍스트와 완전히 동일하게 유지했다.
- **Files modified:** WPF_Example/UI/ControlItem/InspectionListView.xaml.cs (해당 커밋에 포함, 별도 커밋 아님)
- **Verification:** 재실행한 grep 6종 전부 플랜의 EXPECT_ 값과 일치 (`TryGetBlockingSequence=1`, `sBlockingSeqName=3` 등)
- **Committed in:** `100bafe` (Task 1 커밋에 포함)

---

**Total deviations:** 1 auto-fixed (주석 텍스트 전용, 동작 변화 없음)
**Impact on plan:** 실행 코드는 플랜 AFTER 텍스트와 100% 동일. 주석 한 구절만 플랜 자신의 검증 기준을 만족시키기 위해 표현을 바꿨을 뿐 의미·동작 변화 없음.

## Issues Encountered
- 없음. 빌드 중 파일 잠금(devenv.exe 2개 프로세스가 동시 실행 중이었으나 실제 잠금 충돌 없이 빌드 성공, 별도 OutDir 우회나 프로세스 종료 불필요)이나 병행 실행 중인 quick-260805-mzf(CaptureImageSaveService.cs, 무관 파일)와의 충돌도 없었음.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness
- **CO-mze-01 (open):** `InspectionListView`의 `_batchService` / `_batchShots` / `_batchAccumulated`가 시퀀스별로 분리되지 않은 단일 공용 필드다. `Dictionary<ESequence, ...>` 등으로 재설계하면 크로스-시퀀스 일괄검사 동시 실행을 다시 허용할 수 있고, 그때 `Btn_batchRun_Click` 게이트를 `TryGetBlockingSequence`로 되돌릴 수 있다. 그 전까지 전역 `IsIdle` 게이트는 의도된 제약이므로 제거 금지.
- 사람 UAT 3건(플랜 `<verification>` 참조)은 실행자 범위 밖 — 사용자 실기 확인 필요: (1) BOTTOM 일괄검사 중 TOP 일괄검사 시도 시 크래시 없이 차단 메시지만 뜨는지, (2) 아무 것도 실행 중이 아닐 때 일괄검사 정상 동작 무회귀, (3) 서로 다른 물리 카메라 시퀀스의 단일 RUN 동시 실행 무회귀(Phase 69 Test 1 재확인).

---
*Phase: quick-260805-mze*
*Completed: 2026-08-05*

## Self-Check: PASSED
- FOUND: WPF_Example/UI/ControlItem/InspectionListView.xaml.cs
- FOUND commit: 100bafe
