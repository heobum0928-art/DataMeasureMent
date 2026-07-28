---
phase: 260728-l2r
plan: 01
subsystem: inspection-teaching (datum pattern-align baseline)

tags: [halcon, datum, pattern-match, align, refmatch, baseline, teach, wpf]

# Dependency graph
requires: []
provides:
  - "RefreshPatternRefPoseAfterTeach (재티칭 직후) 와 InvokeCreatePatternModel (모델 생성 직후)
    의 ref pose 기록 4곳(패턴1×2, 패턴2×2) 이 전체이미지 검색 PatternMatchService.TryFindRefPose
    대신 InspectionSequence.TryComposeAlign 라이브 매칭과 byte-identical 인자로
    PatternMatchService.TryFindPose(ROI±PatternSearchMarginPx, PatternMinScore,
    downsampleFactor=1.0) 를 사용"
  - "RefreshPatternRefPoseAfterTeach 진입부에 datum.EnsurePerRoiDefaults() 멱등 폴백 추가
    (sentinel 0 margin/minScore 로 인한 ref/live 조건 재분기 차단, TryComposeAlign 진입부 미러)"
  - "패턴1 ROI 미확보(Length1/2 <= 0) 시 조용히 return 하는 가드 추가 — 갱신 시도 없이 기존
    RefMatch 보존, 티칭 성공 자체는 취소하지 않음"
affects: [datum-align-baseline-accuracy, pattern-ref-pose-consistency, circle-detection-after-teach]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "ref pose 기록과 라이브 검사가 항상 동일한 검색 파라미터(ROI±margin, minScore,
      downsampleFactor=1.0)를 쓰도록 통일 — ref/live 검색범위 불일치로 인한 baseline 각도
      오차 버그 재발 방지 패턴"

key-files:
  created: []
  modified:
    - "WPF_Example/UI/ContentItem/MainView.xaml.cs - RefreshPatternRefPoseAfterTeach
      (패턴1/2 ref pose 기록 교체 + EnsurePerRoiDefaults + ROI 부재 가드 추가),
      InvokeCreatePatternModel (패턴1/2 ref pose 기록 교체만, 기존 EnsurePerRoiDefaults/가드
      재사용)"

key-decisions:
  - "각도 검색범위(-Math.PI, 2.0*Math.PI)와 downsampleFactor 는 TryFindRefPose/TryFindPose
    양쪽이 이미 동일함을 플래너가 사전 대조 확인 — 이번 수정에서 각도 관련 인자는 손대지 않음.
    유일한 실질 차이였던 검색영역(전체 이미지 vs ROI±margin)만 교체."
  - "EnsurePerRoiDefaults() 를 RefreshPatternRefPoseAfterTeach 에도 추가 — 기존에는
    InvokeCreatePatternModel 만 호출해서 재티칭 경로가 sentinel 0 margin/minScore 로
    검색해 라이브 경로와 조건이 갈릴 수 있었음. 멱등이라 매 호출 안전."
  - "패턴1 ROI 미확보 시 갱신 자체를 건너뛰는 가드를 추가 — 범위제한 검색은 ROI 가 0이면
    좌상단 구석의 작은 박스로 무너져 무조건 no-match. 라이브 TryComposeAlign 도 같은
    조건에서 실패하므로 새로운 실패 모드를 만들지 않음."

requirements-completed: [L2R-01]  # Task 2 human-verify 사용자 승인("pass") 완료 — 2026-07-28

# Metrics
duration: ~20min (Task 1 only; Task 2 은 사용자 실기 검증 소요시간 별도)
completed: 2026-07-28
---

# Quick Task 260728-l2r: 2-패턴 baseline ref pose 범위통일 Summary

**MainView.xaml.cs 의 ref pose 기록 4곳(패턴1/2 × 재티칭/모델생성)을 전체이미지 검색 `TryFindRefPose` 에서 라이브 검사와 동일한 범위제한 `TryFindPose`(ROI±margin, minScore, downsample 1.0)로 교체 — baseline 회전각 1.4~2° 오차와 그로 인한 "Circle: insufficient polar samples (0)" 실패의 근본 원인 제거.**

## Plan Status: TASK 1 + TASK 2 COMPLETE

이 계획(`260728-l2r-PLAN.md`)은 태스크 2개로 구성되어 있고, **모두 완료됐다**:

- **Task 1 (`type="auto"`)** — 코드 수정 + 빌드 검증. **완료, 커밋됨.**
- **Task 2 (`type="checkpoint:human-verify"`, `gate="blocking"`)** — 실행 중인 앱을 사용자가 직접 재시작해 재티칭→Test Find→Trace 로그 확인을 수행하는 실사용 검증. **사용자 승인 완료("pass", 2026-07-28).** 이 검증은 뒤이은 260728-n7b 수정(TryComposeAlign 패턴2 modelPath2 owner 버그)과 함께 최종 통합 테스트로 수행됨 — 아래 "Human Verification Required (Task 2)" 섹션은 참고용으로 원문 보존.

`L2R-01` 요구사항 완료 처리됨. 단, 개별 항목(4·5·6·7번)의 세부 로그 숫자값은 사용자가 요약 승인("pass")만 전달해 이 SUMMARY에 개별 기록되지 않았다 — 필요 시 사용자에게 재요청 가능.

## Performance (Task 1 only)

- **Duration:** ~20 min
- **Completed:** 2026-07-28T06:51:31Z
- **Tasks:** 1/2 (Task 2 = blocking checkpoint, awaiting user)
- **Files modified:** 1

## Accomplishments (Task 1)
- `RefreshPatternRefPoseAfterTeach` 패턴1 호출 (구 `svc.TryFindRefPose(patternImage, ..., modelPath, datum.PatternMinScore, ...)`) → `svc.TryFindPose(patternImage, ..., modelPath, datum.PatternRoi_Row, datum.PatternRoi_Col, datum.PatternRoi_Length1, datum.PatternRoi_Length2, datum.PatternSearchMarginPx, datum.PatternMinScore, /*downsampleFactor*/ 1.0, ...)` 로 교체.
- `RefreshPatternRefPoseAfterTeach` 패턴2 호출 → 동일하게 교체하되 `datum.PatternRoi2_*` 필드 사용(패턴1과 교차오염 없음, 게이트로 확인).
- `RefreshPatternRefPoseAfterTeach` 진입부에 `datum.EnsurePerRoiDefaults();` + `if (datum.PatternRoi_Length1 <= 0.0 || datum.PatternRoi_Length2 <= 0.0) return;` 2줄 추가(각각 `//260728 hbk quick-fix(260728-l2r)` 주석).
- `InvokeCreatePatternModel` 패턴1/패턴2 ref pose 호출도 동일한 방식으로 `TryFindPose` 로 교체 (이 메서드는 이미 `EnsurePerRoiDefaults()`/ROI 부재 가드를 갖고 있어 추가 가드는 넣지 않음 — 계획 지시대로).
- `PatternMatchService.cs` / `InspectionSequence.cs` / `AlignShapeMatchService.cs` 는 **한 글자도 수정하지 않음** (git diff 빈 출력으로 확인).
- 260728-kd2 커밋분(`FormatTeachError`/`FormatFindError` 의 `RadialDirection(Inward/Outward)` 힌트)은 그대로 보존.

## Task Commits

1. **Task 1: ref pose 기록 4곳을 범위제한 TryFindPose 로 통일 (MainView.xaml.cs 단일 파일)** - `20c8ba6` (fix)

_No plan metadata commit — orchestrator handles the docs commit in a later step per this run's constraints._

## Files Created/Modified
- `WPF_Example/UI/ContentItem/MainView.xaml.cs` - `RefreshPatternRefPoseAfterTeach` (라인 ~3643-3678) 및 `InvokeCreatePatternModel` (라인 ~3798 이하, 패턴1/2 ref pose 기록 부분만): 4개 `TryFindRefPose` 호출 전부 `TryFindPose` 로 교체 + 신규 2줄(defaults/guard). 1 file changed, 19 insertions(+), 5 deletions(-).

## Automated Gate Results (want vs actual)

모두 계획서의 `(want N)` 값과 정확히 일치했다 (수정 후 1회 통과, 재시도 없음):

| Gate | Want | Actual |
|------|------|--------|
| refpose_calls_gone | 0 (was 4) | 0 |
| findpose_calls | 4 (was 0) | 4 |
| p1_roi_args | 2 (was 0) | 2 |
| p2_roi_args | 2 (was 0) | 2 |
| margin_minscore_ds | 4 (was 0) | 4 |
| ensure_defaults | 2 (was 1) | 2 |
| helper_p1_guard | 1 (was 0) | 1 |
| helper_sig | 1 | 1 |
| teach_ok_hook | 2 | 2 |
| gate_align | 1 | 1 |
| gate_p2 | 2 | 2 |
| w2_modal_guard | 1 | 1 |
| refpose_fail_modal | 1 | 1 |
| p2_fallback_msg | 1 | 1 |
| align_refresh_log | 1 | 1 |
| kd2_hunk_intact | 2 | 2 |
| svc_refpose_intact (PatternMatchService.cs) | 1 | 1 |
| align_svc_refpose_users (AlignShapeMatchService.cs) | 2 | 2 |
| no_csharp8_added | 0 | 0 |
| `git diff --name-only` scope | 1 file (MainView.xaml.cs only) | 1 file (confirmed) |

Additional file-level checks:
- `git diff -- WPF_Example/Halcon/Algorithms/PatternMatchService.cs` → empty (confirmed unchanged).
- `git diff -- WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs` → empty (confirmed unchanged).
- `git diff -- WPF_Example/Custom/EthernetVision/AlignShapeMatchService.cs` → empty (confirmed unchanged).
- Debug/x64 build: **0 `error CS`, 0 new `warning CS`** (only pre-existing `CS0618` Phase-33-migration obsolete-API warnings present, explicitly excluded by the gate's own filter).
- `WPF_Example/bin/x64/Debug/DatumMeasurement.exe` was confirmed freshly rebuilt (timestamp `2026-07-28 15:50:49` vs. build-run wall clock `15:51:04`, no MSB3021/3026/3027 lock — `DatumMeasurement.exe` was not running during this build).

## Decisions Made
- See frontmatter `key-decisions`. In short: only the search-region argument set changed (ROI±margin vs whole-image); angle range and downsample were already identical between the two service methods per the planner's pre-verified interface note, so this execution did not touch them.

## Deviations from Plan

None — plan executed exactly as written. All automated gate token counts matched the plan's expected values exactly on first application; no auto-fixes (Rule 1/2/3) were needed, no architectural questions (Rule 4) arose.

## Issues Encountered

None. `DatumMeasurement.exe` was not running at build time, so no MSB3021/3026/3027 file-lock issue occurred (unlike the prior 260728-kd2 session) — the build's copy-to-bin step succeeded cleanly on the first attempt.

## User Setup Required

None - no external service configuration required.

## Human Verification Required (Task 2) — BLOCKING, NOT YET PERFORMED

Per this run's constraints, Task 2 (`type="checkpoint:human-verify"`, `gate="blocking"`) was intentionally **not** executed or faked. It requires closing the currently-running app, using the freshly rebuilt `DatumMeasurement.exe`, and performing a specific button sequence with numeric log-value checks that only a human operating the UI can do. Reproduced verbatim below from the plan.

### What was built (plain-language, from plan `<what-built>`)

기준값(ref)을 만들 때와 실제로 검사할 때가 서로 다른 방식으로 사진을 뒤지던 것을 하나로 맞췄습니다.

전에는 기준값을 만들 때 사진 전체를 훑어서 제일 비슷한 곳을 골랐고, 실제 검사할 때는 지정한 네모 근처만 훑었습니다. 패턴이 흐릿해서 점수가 낮으면(예: 패턴2가 0.74) 두 방식이 같은 사진에서도 살짝 다른 지점을 집을 수 있었고, 그러면 부품이 전혀 안 움직였는데도 각도가 1.4~2도 틀어진 것처럼 계산됐습니다. 두 패턴 사이가 1만 픽셀쯤 떨어져 있어서 그 작은 각도가 수백 픽셀 위치 오차로 커졌고, 그래서 원(Circle) 검출이 "샘플 0개"로 실패했던 겁니다.

이제 기준값도 실제 검사와 똑같이 "지정한 네모 근처만" 훑습니다. 그래서 방금 다시 티칭한 사진에서는 두 값이 반드시 같아지고, 부품이 진짜로 움직였을 때만 각도가 나옵니다.

### How to verify (verbatim from plan `<how-to-verify>`)

아래 순서대로 확인해 주세요.

0. **먼저 이게 제일 중요합니다 — 새 프로그램으로 테스트하는지 확인.**
   지금 켜져 있는 DatumMeasurement 프로그램을 **완전히 닫고**(Visual Studio 로 디버깅 중이면 그것도 정지), 새로 빌드된 것을 다시 실행해 주세요. 안 닫으면 파일이 잠겨서 **예전 프로그램이 그대로 실행**되고, 고친 게 하나도 반영되지 않은 상태로 테스트하게 됩니다.
1. **문제가 났던 그 Datum** 을 트리에서 고릅니다. 속성창에서 `Is pattern align enabled` 를 **켭니다(ON)** — 지난번에 이걸 끄고 확인했던 그 항목입니다.
2. `[패턴 모델 생성]` 버튼을 누릅니다. 완료 창에 나오는 **패턴1 점수와 패턴2 점수**를 적어 주세요. (패턴2가 0.74 근처로 나와도 정상입니다.)
   - 저장할지 물어보면 **예(Recipe Save)** 를 누릅니다.
3. `[Datum 티칭]` 을 눌러 재티칭을 완료합니다.
4. `[Test Find]` 를 누릅니다.
   - **기대**: 예전처럼 "Circle: insufficient polar samples (0)" 같은 실패가 나지 않고 정상 검출된다. 파란(Find) 선이 노란(Teach) 선과 거의 겹친다.
5. Trace 로그를 열어 방금 Test Find 의 `[ALIGN2]` 줄을 찾습니다.
   - **기대**: `thetaDeg` 값이 **0에 매우 가깝다**(대략 ±0.05도 이내). 예전처럼 1.4~2도가 나오면 실패입니다.
   - `[ALIGN-REFRESH]` 줄도 함께 남아 있는지 봐 주세요.
6. **회귀 확인(중요)**: `Is pattern align enabled` 가 **꺼져 있는** 다른 Datum 하나를 골라 티칭 → Test Find 를 해 봅니다.
   - **기대**: 예전과 똑같이 동작한다(아무 변화 없음).
7. **여유 있으면**: 부품을 실제로 살짝 돌려서 다시 Test Find 를 해 봅니다.
   - **기대**: 이때는 `thetaDeg` 가 0이 아니라 실제 돌린 만큼 나오고, 검출도 성공한다. (보정이 죽은 게 아니라 살아 있다는 확인)

문제가 있으면 `[ALIGN2]` / `[ALIGN-REFRESH]` 로그 줄과 화면 상태를 알려 주세요.

**Resume signal:** "approved" 라고 입력하거나, 안 된 항목의 번호와 로그를 알려 주세요.

### 패턴2 점수 0.74대에서도 thetaDeg≈0 이 나왔는지

**PENDING — Task 2 미수행.** 사용자가 위 절차를 실행해야 알 수 있는 값이며, 이 실행에서는 확인할 수 없다(자동화 불가 항목, 계획서 `<verify><automated>MISSING</automated></verify>` 참조). Task 2 승인 시 이어지는 에이전트가 이 필드를 실측값으로 채우고 SUMMARY 를 갱신해야 한다.

### 행동 변화 경계 (반드시 사용자에게 그대로 전달)

패턴이 티칭 위치에서 `PatternSearchMarginPx`(기본 100px) 를 넘어 벗어난 이미지로 재티칭하면, 이제 ref 갱신이 실패해 **기존 RefMatch 가 유지**된다(예전엔 전체검색이라 어떻게든 갱신됐다). 이건 의도된 변화이며, 그런 경우 사용자는 패턴 ROI 를 새 위치로 다시 그려야 한다.

## Next Phase Readiness
- Task 1 코드 변경은 완료·커밋되어 있고 빌드도 깨끗하다. Task 2 인간 검증만 남았다.
- Task 2 결과가 "approved" 이면: 이어지는 에이전트가 이 SUMMARY 의 "PENDING" 항목(패턴1/2 점수, thetaDeg 실측값)을 채우고, `requirements-completed: [L2R-01]` 로 갱신 후 STATE.md/ROADMAP.md 등 문서 커밋을 진행해야 한다.
- Task 2 결과가 실패 항목을 포함하면: 그 로그(`[ALIGN2]`/`[ALIGN-REFRESH]`)를 근거로 원인 분석 후, **새 코드 수정 전에 사용자 승인**을 받아야 한다(계획 Task 2 `<action>` 명시 사항).

## Self-Check: PASSED

- FOUND: `WPF_Example/UI/ContentItem/MainView.xaml.cs`
- FOUND: commit `20c8ba6`
- All 20 automated gate tokens matched expected `(want N)` values exactly (see table above).
- `git diff --name-only` shows exactly one modified source file: `WPF_Example/UI/ContentItem/MainView.xaml.cs`.
- Build (`MSBuild //t:Build //p:Configuration=Debug //p:Platform=x64`) produced zero `error CS` lines and zero new `warning CS` lines.
- `PatternMatchService.cs`, `InspectionSequence.cs`, `AlignShapeMatchService.cs` all confirmed unchanged (`git diff` empty for each).

---
*Quick task: 260728-l2r*
*Task 1 completed: 2026-07-28 — Task 2 (blocking human-verify) PENDING*
