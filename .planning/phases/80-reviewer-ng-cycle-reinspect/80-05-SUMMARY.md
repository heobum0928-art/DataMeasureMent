---
phase: 80-reviewer-ng-cycle-reinspect
plan: 05
subsystem: reviewer-reinspect
tags: [versioning, audit, uat, halcon, mvvm, reviewer]
dependency graph:
  requires:
    - phase: 80-01
      provides: 리뷰어 버튼 → NG 사진 → 메인 상태 줄/[해제] + 운영 레시피 보호 tracer
    - phase: 80-02
      provides: Local Ref stale 재계산 + 기준 ROI 시험 찾기 + 대화상자 없는 공용 Test Find
    - phase: 80-03
      provides: 같은 자재 Shot·기준점·Z 후보 사진 + 기준점 없음 알림 + 리뷰어 선 겹침 수정
    - phase: 80-04
      provides: 메인 화면 이어받기(리뷰어 최소화, NG 측정 노드 선택, 자동 Test Find, 측정 노드 RUN)
  provides:
    - "버전 1.7.51.0(VersionDefine.cs) — 이 phase 의 기능 7가지 + 리뷰어 선 겹침 수정을 담은 한국어 변경 설명"
    - "phase 시작 대비 누적 감사 PASS(audit-80.txt) — 가독성 0·MVVM·UI 3개 제한·회귀 md5 14개 동일"
    - ".planning/phases/80-reviewer-ng-cycle-reinspect/80-HUMAN-UAT.md — 장비 PC Release UAT U-1~U-11 + A-80-E1~E5 판단 완료 기록"
    - "사용자 승인(2026-09-21, approved) — U-6/U-11 PARTIAL(PLC 자동 검사 실동작 회귀 PENDING), A-80-E3 미확인(PENDING)"
    - "U-9 UAT 중 발견한 결함 수정 — 시험 찾기 실패 문구 잔류/과다 길이 (커밋 837479b8, 장비 미배포)"
  affects:
    - 다음 phase (국부 기준선 재계산 실패 시 저장 동작 개선 후보, 837479b8 재배포)
tech-stack:
  added: []
  patterns:
    - "누적 감사 스크립트(git diff -w -U0 + md5 same() 헬퍼)로 phase 시작 커밋 대비 가독성·회귀를 자동 증명 — Task 1 acceptance_criteria 그대로 재사용 가능"
key-files:
  created:
    - .planning/phases/80-reviewer-ng-cycle-reinspect/80-HUMAN-UAT.md
  modified:
    - WPF_Example/VersionDefine.cs
    - WPF_Example/Custom/Sequence/Inspection/LocalRefTestFindService.cs
    - WPF_Example/UI/ContentItem/MainView.xaml.cs
    - WPF_Example/UI/ControlItem/InspectionListView.xaml.cs
key-decisions:
  - "U-9 에서 나온 UAT 후속 결함(시험 찾기 실패 문구가 트리 선택 변경 후에도 남고 문구가 길어 툴바가 3줄로 밀림)은 Rule 1(버그) 로 즉시 수정·커밋했다 — Release 재배포는 하지 않고 다음 사용자 배포에 포함"
  - "U-8 재계산 후 재실행 시 로그가 다시 안 찍히는 동작은 설계대로의 폴백(저장된 결과 재사용)이라 이번 phase 에서 고치지 않고 '알려진 한계' 6번 + STATE.md 후속 후보로 기록"
requirements-completed: [D-80-00, D-80-01, D-80-02, D-80-03, D-80-04, D-80-05, D-80-06, D-80-07, D-80-08, D-80-09, D-80-10, D-80-11, D-80-12, D-80-13, D-80-14, D-80-15]

coverage:
  - id: D1
    description: "버전 1.7.51.0 + phase 시작 대비 누적 감사(가독성·MVVM·UI 3개 제한·회귀 0) PASS"
    requirement: "D-80-16"
    verification:
      - kind: other
        ref: "audit-80.txt (Task 1 <verify> 자동 스크립트 원문 — 아래 '누적 감사 결과' 절)"
        status: pass
    human_judgment: false
  - id: D2
    description: "80-HUMAN-UAT.md 작성 — U-1~U-11 + A-80-E1~E5 판단 칸 + Release 전용/백업 안내"
    requirement: "D-80-00"
    verification:
      - kind: other
        ref: "80-05-PLAN.md Task 2 <verify> grep 스크립트 (exists/U-*/A-80-E* 전부 >=1)"
        status: pass
    human_judgment: false
  - id: D3
    description: "장비 PC Release 1.7.51.0 실기 UAT U-1~U-11 수행 + A-80-E1~E5 사용자 판단 + 총평"
    verification:
      - kind: manual_procedural
        ref: "80-HUMAN-UAT.md '최종 결과 (2026-09-21)' 절 — U-1~U-5·U-7~U-10 PASS, U-6·U-11 PARTIAL(PLC 자동 검사 PENDING), A-80-E1/E2/E4/E5 수용, A-80-E3 PENDING"
        status: pass
    human_judgment: true
    rationale: "실제 장비 PC 화면·로그·main.ini 로 확인해야 하는 사람 판단 — 사용자가 직접 수행하고 승인함(approved). 일부 항목은 PLC 자동 가동 시점에만 확인 가능해 PENDING 으로 명시 기록."
  - id: D4
    description: "U-9 UAT 중 발견한 시험 찾기 실패 문구 잔류/과다 길이 결함을 즉시 수정"
    verification:
      - kind: other
        ref: "커밋 837479b8 — Debug|x64 빌드 0 오류, 가독성 grep 0"
        status: pass
    human_judgment: false

# Metrics
duration: 25min
completed: 2026-09-21
status: complete
---

# Phase 80 Plan 05: 버전 1.7.51.0 + 누적 감사 + 장비 PC Release UAT Summary

버전을 1.7.51.0 으로 올리고 phase 시작 대비 누적 감사(가독성·MVVM·UI 3개 제한·회귀 0)를 자동 스크립트로 증명한 뒤, 장비 PC Release 빌드로 U-1~U-11 실기 UAT 를 수행해 사용자 승인(approved)을 받았다 — U-9 에서 나온 결함 1건은 UAT 도중 즉시 고쳐 커밋했다(837479b8, 아직 장비 미배포).

## Performance

- **Duration:** ~25 min (Task 1-2) + UAT 세션(장비 PC, 사용자 수행) + 사후 정리(이 SUMMARY)
- **Completed:** 2026-09-21
- **Tasks:** 3 (Task 1: 버전+감사, Task 2: UAT 절차서, Task 3: 사용자 확인 체크포인트 — RESOLVED)
- **Files modified:** VersionDefine.cs, 80-HUMAN-UAT.md, + UAT 후속 수정 3개 파일(LocalRefTestFindService.cs, MainView.xaml.cs, InspectionListView.xaml.cs)

## Accomplishments

- **버전 1.7.51.0**: `VersionDefine.cs` 에 새 `[Version(...)]` 항목 추가 — 이 phase 의 기능 7가지(리뷰어 버튼·같은 자재 사진·기준점 없음 알림·자동 Test Find·레시피 보호/자동 해제·Local Ref 재계산·기준 ROI 시험 찾기) + 리뷰어 선 겹침 수정을 쉬운 한국어로 기술.
- **누적 감사 PASS**: phase 시작 커밋 대비 신규 .cs 4개(`ReviewerReinspectService`/`ReviewerReinspectViewModel`/`DatumTestFindService`/`LocalRefTestFindService`) 전부 가독성 규칙(삼항·null 병합·null 조건·switch 식·날짜주석·물음표·조건 3개 이상) 0, 기존 파일 13개에 더한 줄도 전부 0, `.csproj` 등록 4/4, UI 3개 제한(새 XAML 0·새 Window 0·SystemSetting 변경 0·새 Button 정확히 3개·새 대화상자 1개) 확인, 기존 메서드 14개 md5 완전 동일(회귀 0), `RepeatRunService.cs` 삭제 줄 0, Debug|x64 빌드 오류 0.
- **80-HUMAN-UAT.md 작성**: U-1~U-11 절차서(목적·준비·단계·기대 결과·합격 기준·결과 칸) + A-80-E1~E5 판단 칸 + Release 전용/백업 안내를 79-HUMAN-UAT.md 형식으로 작성·커밋.
- **장비 PC Release UAT 완료 + 사용자 승인**: 2026-09-21 사용자가 Release 1.7.51.0 을 직접 배포·시험하고 `approved` 로 응답(총평 "잘 나오네 잘 만들었다"). U-9 에서 UAT 중 결함 1건 발견 → 즉시 수정·커밋(837479b8).

## Task Commits

Each task was committed atomically:

1. **Task 1: 버전 1.7.51.0 + phase 누적 감사** - `33156fca` (feat)
2. **Task 2: 80-HUMAN-UAT.md 작성** - `0f417a3b` (docs)
   - 세션 체크포인트 기록 - `c3e0892d` (docs)
3. **Task 3: 사용자 확인(장비 PC Release UAT)** - RESOLVED via 사용자 UAT 세션 + UAT 후속 수정 `837479b8` (fix)

**Plan metadata:** (이 커밋 — docs)

_Note: TDD 아님 — 전부 auto 타입 + 1개 checkpoint:human-verify 타입. Task 3 는 코드 커밋이 아니라 체크포인트 해소이며, 그 과정에서 나온 결함 수정만 `837479b8` 로 커밋됨._

## Files Created/Modified

- `WPF_Example/VersionDefine.cs` — `Number = "1.7.51.0"` 항목 + `VERSION`/`BUILD_DATE` 상수.
- `.planning/phases/80-reviewer-ng-cycle-reinspect/80-HUMAN-UAT.md` — UAT 절차서 작성 + 실기 결과·판단·최종 승인 기록.
- `WPF_Example/Custom/Sequence/Inspection/LocalRefTestFindService.cs` — 실패 문구 6개 단축(UAT 후속, 837479b8).
- `WPF_Example/UI/ContentItem/MainView.xaml.cs` — `ClearLocalRefTestFindMessage()` 추가(UAT 후속, 837479b8).
- `WPF_Example/UI/ControlItem/InspectionListView.xaml.cs` — 트리 선택 변경 시 위 메서드 호출 1줄 배선(UAT 후속, 837479b8).

## Decisions Made

- U-9 에서 나온 UAT 후속 결함(시험 찾기 실패 문구가 다른 노드를 골라도 남고, 문구가 길어 툴바 버튼 줄이 3줄로 밀림)은 Rule 1(버그 자동 수정)로 즉시 고치고 커밋했다. Release 재빌드·재배포는 하지 않음(사용자가 직접 수행) — 사용자 배포 exe(1.7.51.0, f9bc32d8 시점)에는 아직 이 수정이 반영되지 않았다.
- U-8 에서 "재계산 실패 결과가 저장되어 같은 설정으로 다시 RUN 하면 재계산 로그가 다시 찍히지 않는" 동작은 설계대로의 폴백(이미 저장된 국부 기준선 재사용)이지 버그가 아니므로 이번 phase 에서 고치지 않고, 로그 문구 보강/실패 시 미저장 방안을 다음 phase 후보로 `80-HUMAN-UAT.md` "알려진 한계" 6번 + `STATE.md` Blockers 에 기록했다.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] 기준 ROI 시험 찾기 실패 문구 잔류 + 과다 길이**
- **Found during:** Task 3 (사용자 UAT, U-9)
- **Issue:** 측정을 고르지 않고 "기준 ROI 시험 찾기"를 눌렀을 때 나오는 빨간 안내 문구가 다른 노드를 골라도 계속 남고, 문구가 길어 툴바 버튼 줄이 3줄로 밀림.
- **Fix:** `MainView.ClearLocalRefTestFindMessage()` 신설 + `InspectionListView` 트리 선택 변경 시 호출 1줄 배선. `LocalRefTestFindService` 의 실패 문구 6개를 짧게 정리.
- **Files modified:** `WPF_Example/Custom/Sequence/Inspection/LocalRefTestFindService.cs`, `WPF_Example/UI/ContentItem/MainView.xaml.cs`, `WPF_Example/UI/ControlItem/InspectionListView.xaml.cs`
- **Verification:** Debug|x64 빌드 0 오류, 가독성 grep 0 재확인.
- **Committed in:** `837479b8`

---

**Total deviations:** 1 auto-fixed (Rule 1 — 버그)
**Impact on plan:** UAT 중 발견된 UX 결함 수정. 계획 범위를 벗어나지 않음(같은 기능의 안내 문구 정리). 이 수정본은 아직 장비에 재배포되지 않았다.

## Issues Encountered

None beyond the deviation above.

## User Setup Required

None - Release 빌드·배포는 사용자가 직접 수행(`D:\Data` OutputPath — 실행자는 빌드/배포하지 않음).

## 누적 감사 결과 (audit-80.txt 원문)

```
build_exit=0 cs_errors=0
NEW ReviewerReinspectService.cs ternary=0 coalesce=0 nullcond=0 switchexpr=0 datesig=0 qmark=0 logic3=0
NEW ReviewerReinspectViewModel.cs ternary=0 coalesce=0 nullcond=0 switchexpr=0 datesig=0 qmark=0 logic3=0
NEW DatumTestFindService.cs ternary=0 coalesce=0 nullcond=0 switchexpr=0 datesig=0 qmark=0 logic3=0
NEW LocalRefTestFindService.cs ternary=0 coalesce=0 nullcond=0 switchexpr=0 datesig=0 qmark=0 logic3=0
OLD RepeatRunService.cs added=192 deleted=0 ternary=0 coalesce=0 nullcond=0 switchexpr=0 datesig=0 qmark=0 logic3=0
OLD Action_FAIMeasurement.cs added=102 deleted=2 ternary=0 coalesce=0 nullcond=0 switchexpr=0 datesig=0 qmark=0 logic3=0
OLD InspectionSequence.cs added=104 deleted=0 ternary=0 coalesce=0 nullcond=0 switchexpr=0 datesig=0 qmark=0 logic3=0
OLD ReviewMeasurementRow.cs added=56 deleted=0 ternary=0 coalesce=0 nullcond=0 switchexpr=0 datesig=0 qmark=0 logic3=0
OLD ReviewerWindow.xaml added=16 deleted=0 ternary=0 coalesce=0 nullcond=0 switchexpr=0 datesig=0 qmark=0 logic3=0
OLD ReviewerWindow.xaml.cs added=25 deleted=15 ternary=0 coalesce=0 nullcond=0 switchexpr=0 datesig=0 qmark=0 logic3=0
OLD MainView.xaml added=49 deleted=0 ternary=0 coalesce=0 nullcond=0 switchexpr=0 datesig=0 qmark=0 logic3=0
OLD MainView.xaml.cs added=43 deleted=0 ternary=0 coalesce=0 nullcond=0 switchexpr=0 datesig=0 qmark=0 logic3=0
OLD MainWindow.xaml.cs added=18 deleted=1 ternary=0 coalesce=0 nullcond=0 switchexpr=0 datesig=0 qmark=0 logic3=0
OLD SystemHandler.cs added=2 deleted=0 ternary=0 coalesce=0 nullcond=0 switchexpr=0 datesig=0 qmark=0 logic3=0
OLD InspectionListView.xaml.cs added=56 deleted=1 ternary=0 coalesce=0 nullcond=0 switchexpr=0 datesig=0 qmark=0 logic3=0
OLD DatumMeasurement.csproj added=4 deleted=0 ternary=0 coalesce=0 nullcond=0 switchexpr=0 datesig=0 qmark=0 logic3=0
OLD VersionDefine.cs added=13 deleted=1 ternary=0 coalesce=0 nullcond=0 switchexpr=0 datesig=0 qmark=0 logic3=0
csproj_new=4
ui_new_xaml=0 ui_new_window=0 ui_setting_changed=0 ui_new_buttons=3
ui_new_dialog_calls=1
h_apply=1 h_alert=1 h_release=1 h_forget=1
same[private void BtnTestFindDatum_Click]=1
same[private HImage AskTestImageSource]=1
same[private void ProcessOneDatum]=1
same[private void InjectLocalRef]=1
same[private void HandleRunStartResetResults]=1
same[private void ComputeLocalRefLinesForDatum]=1
same[public static SavedCycleRerunPlan BuildPlan]=1
same[private static string ValidatePart]=1
same[private void ApplySavedCyclePart]=1
same[private SavedCycleOverrideSnapshot BuildOverrideSnapshot]=1
same[private static void RestoreOverridePathsOnly]=1
same[private bool ResolveRunnableAction]=1
same[public static string ResolveCycleImagePath(CycleResultDto cycle)]=1
same[public static string ResolveRowImagePath]=1
version=1 version_entry=1
```

모든 acceptance_criteria 충족: `build_exit=0 cs_errors=0`, NEW/OLD 가독성 전부 0, `csproj_new=4`, UI 3개 제한 확인, 핸들러 4개 1문장, md5 동일 14/14, `RepeatRunService.cs deleted=0`, `version=1 version_entry=1`.

## U 번호별 최종 결과표

| U | 결과 | 핵심 근거 |
|---|---|---|
| U-1 | PASS | 버튼이 NG 원인 패널 아래 보이고 확인창 없이 즉시 동작 |
| U-2 | PASS | `[ReviewerLoad] 불러옴 —` 로그, 리뷰어 최소화+메인 앞으로, NG 노드 자동 선택 |
| U-3 | PASS | 수동 검사 기록(`IsProtocolDriven=false`, `DatumImages=0`)에서 알림 1개 + Shot 사진만 |
| U-4 | PASS | 측정 노드 RUN 1회로 6개 측정 결과 표시 |
| U-5 | PASS | main.ini 사진 경로 줄 0건 변경, 파라미터만 변경 |
| U-6 | PARTIAL | [해제] 버튼·재시작 PASS / 레시피 변경 미시험(PENDING) / PLC 자동 해제 PENDING(장비 자동 가동 시) |
| U-7 | PASS | JPG·Z 후보 안내 문구 확인, 값 차이 원인(`SaveZRangeCandidateImages=False`) 설명·수용 |
| U-8 | PASS | `[LocalRef] ... 다시 구함` 로그 확인, 폴백 동작 확인(재계산 재실행 시 로그 미출력은 설계상 정상) |
| U-9 | PASS (수정 후) | 결함 발견(문구 잔류·과다 길이) → 837479b8 로 즉시 수정(장비 미배포) |
| U-10 | PASS | 엉뚱한 위치의 선 없음 |
| U-11 | PARTIAL | 정적 회귀 증거(md5 14개 동일, 삭제 0줄) 확보 / PLC 자동 실동작 회귀 PENDING |

## A-80-E1~E5 판단

| # | 판단 |
|---|---|
| A-80-E1 (리뷰어 최소화 방식) | 수용 — 사용자 이의 없음 |
| A-80-E2 (측정 노드 RUN + 오프라인 확인창 생략) | 수용 — U-4 에서 문제 없음 |
| A-80-E3 (정적 두 장짜리 기준점 완전 간주) | 미확인(PENDING) — 해당 케이스 미시험 |
| A-80-E4 (JPG 안내 문구) | 수용 — 사용자가 문구로 상황을 이해함 |
| A-80-E5 (같은 자재 묶기 반응 속도) | 수용 — 체감 지연 없음 |

**사용자 총평:** "잘 나오네 잘 만들었다"

## 사용자 응답 (요약)

2026-09-21, 사용자가 장비 PC 에서 Release 1.7.51.0 배포·실행 후 U-1~U-11 을 순서대로 시험, U-9 에서 결함 1건을 즉시 보고하여 그 자리에서 수정·커밋(837479b8). 최종 응답: **approved**. PLC 자동 검사 실동작 관련 항목(U-6 일부, U-11 항목4)과 A-80-E3 는 "장비 자동 가동 시 확인 예정" 으로 PENDING 명시.

## 배포 상태

장비 PC 실행 exe 는 1.7.51.0(커밋 f9bc32d8 시점 코드, UAT 대상)이다. UAT 후속 수정(`837479b8`)은 **아직 Release 재빌드·배포되지 않았다** — 재배포는 사용자가 직접 수행한다(Release `OutputPath` = `D:\Data`).

## 후속 관찰 (다음 phase 후보, 지금 고치지 않음)

국부 기준선 재계산이 실패(에지 0개)하면 그 실패 결과가 시퀀스에 저장되어, 설정을 바꾸지 않는 한 다음 RUN 에서는 재계산 로그가 다시 찍히지 않는다(U-8). 설계상 맞는 폴백이지만 사용자에겐 "한 번만 되고 마는" 것처럼 보였다. 로그 문구 보강 또는 실패 시 저장하지 않는 방안을 다음 phase 후보로 남긴다.

## Known Stubs

없음.

## Threat Flags

없음 — 이 plan 은 문서·버전·UAT 절차이며 새 위협 표면을 추가하지 않았다. UAT 후속 수정(837479b8)도 UI 문구 정리뿐이라 새 표면 없음.

## Next Phase Readiness

- Phase 80 전체(80-01~05) 완료. 사용자 승인(approved) 획득.
- **남은 PENDING (다음 세션/phase 로 이월):**
  1. PLC 자동 검사 실동작 회귀 확인 (U-6 PLC 경로, U-11 항목4) — 장비 자동 가동 시점에만 확인 가능.
  2. A-80-E3 (정적 두 장짜리 기준점 완전 간주) 미확인 — 해당 케이스 준비 후 재확인.
  3. UAT 후속 수정(837479b8) 재배포 — 사용자가 다음 Release 빌드 시 함께 배포.
  4. 국부 기준선 재계산 실패 저장 동작 관련 로그/UX 개선 후보(위 "후속 관찰").
- 레시피 변경 경로(U-6 항목3)도 이번 UAT 에서 시험되지 않아 다음 기회에 확인 필요.

## Self-Check

- [x] `WPF_Example/VersionDefine.cs` — FOUND
- [x] `.planning/phases/80-reviewer-ng-cycle-reinspect/80-HUMAN-UAT.md` — FOUND
- [x] `WPF_Example/Custom/Sequence/Inspection/LocalRefTestFindService.cs` — FOUND
- [x] `WPF_Example/UI/ContentItem/MainView.xaml.cs` — FOUND
- [x] `WPF_Example/UI/ControlItem/InspectionListView.xaml.cs` — FOUND
- [x] commit `33156fca` — FOUND in git log
- [x] commit `0f417a3b` — FOUND in git log
- [x] commit `c3e0892d` — FOUND in git log
- [x] commit `837479b8` — FOUND in git log

## Self-Check: PASSED

---
*Phase: 80-reviewer-ng-cycle-reinspect*
*Completed: 2026-09-21*
