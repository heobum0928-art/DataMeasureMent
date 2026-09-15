---
phase: 77-side-z-focus-select
plan: 05
subsystem: vision-inspection
tags: [halcon, edge-measurement, z-focus-select, versioning, regression-audit]

# Dependency graph
requires:
  - phase: 77-side-z-focus-select/77-01
    provides: "Z 범위 tracer 뼈대(ShotConfig.ZIndexEnd, IsZRangeEnabled, m_dicZRangeImages, TryHandleZRangeMeasurement)"
  - phase: 77-side-z-focus-select/77-02
    provides: "동점 규칙(PickZFocusResult 3%)·겹침 제외 가드(DoesShotOwnZRangeIndex/BuildZRangeCandidateIndices)·편집 경고"
  - phase: 77-side-z-focus-select/77-03
    provides: "CSV/화면/오버레이 선택Z 표시 배선"
  - phase: 77-side-z-focus-select/77-04
    provides: "ResolveZRangeMode 완성(수동/오프라인/재검사), 후보 저장 체크박스"
provides:
  - "VersionDefine 1.7.47.0 changelog — SIDE Z 범위 자동 초점 선택 + D-77-08 범위 변경 다이얼로그 요약"
  - "phase 77 누적(77-01~05) 회귀 감사 — 삭제 허용 목록(ShotConfig.cs=1, MeasurementHistoryCsvWriter.cs=1, EdgeInspectionOverlay.cs=1, VersionDefine.cs=2) 외 기존 줄 삭제 0을 코드 근거로 확정, 모든 새 분기가 IsZRangeEnabled()/ResolveZRangeMode Off 가드 뒤에 있음을 확정"
affects: [77-06-side-z-focus-select]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "changelog-as-code — [Version] 어트리뷰트를 계속 쌓아 배포 이력을 코드 안에 남기는 기존 관용구(VersionDefine.cs) 그대로 재사용"
    - "누적 회귀 감사 — 코드를 고쳐 숫자를 맞추지 않고, BASE(77-01 이전) 대비 diff 로 삭제/하드룰 위반을 있는 그대로 보고하는 감사 전용 태스크"

key-files:
  created: []
  modified:
    - WPF_Example/VersionDefine.cs

key-decisions:
  - "VersionDefine 1.7.47.0 changelog 는 D-77-01~08(SZF-01~05, D-77-08 다이얼로그 포함) 전체를 한 문단으로 요약 — 기존 항목 삭제 없이 마지막(1.7.46.0) 뒤에 추가"
  - "Task 2 감사의 삭제 허용 목록은 오케스트레이터 지시대로 ShotConfig.cs=1(D-77-08/b92d57e6, ZIndex 자동 프로퍼티 → 다이얼로그 setter 교체)을 포함해 4개 파일(합 5줄)로 확장 — 코드는 되돌리지 않는다"

patterns-established: []

requirements-completed: [SZF-05]

coverage:
  - id: D1
    description: "VersionDefine.cs 에 1.7.47.0 [Version] 항목이 기존 항목을 지우지 않고 추가되고 VERSION/BUILD_DATE 가 1.7.47.0/2026-09-15 로 올라간다"
    requirement: "SZF-05"
    verification:
      - kind: unit
        ref: "grep entry=1 prev=1 ver=1 date=1 deleted=2(VERSION/BUILD_DATE 값줄) qmark=0 datesig=0 (77-05-PLAN.md Task 1 자동검증 스크립트, 본 SUMMARY 실행 결과와 동일)"
        status: pass
    human_judgment: false
  - id: D2
    description: "phase 77 누적(77-01~05) diff 에서 BASE 이전부터 있던 줄의 삭제·수정은 허용 목록(MeasurementHistoryCsvWriter.cs 1줄, EdgeInspectionOverlay.cs 1줄, VersionDefine.cs 2줄, ShotConfig.cs 1줄 — D-77-08/b92d57e6 orchestrator 승인 확장)뿐이고 나머지 19개 파일은 0"
    requirement: "SZF-05"
    verification:
      - kind: unit
        ref: "grep deleted=N per-file (77-05-PLAN.md Task 2 자동검증 스크립트, 아래 '회귀 감사' 절 전문)"
        status: pass
    human_judgment: false
  - id: D3
    description: "모든 새 분기(DoesShotOwnZRangeIndex/BuildZRangeCandidateIndices/ShotHasZRangeCompletingAt/ResolveZRangeMode)가 IsZRangeEnabled() 가드로 시작하고, EdgeScore 를 설정하는 곳은 EdgeToLineDistance·EdgeToLineAngle 두 파일뿐이며 사용처는 전부 null 가드 뒤에 있다"
    requirement: "SZF-05"
    verification:
      - kind: unit
        ref: "grep guard[...]>=1(3개) offfirst=1 edgescore_setters=2파일 edgescore_uses=4 edgescore_nullguards=4 (77-05-PLAN.md Task 2 자동검증 스크립트, 아래 '회귀 감사' 절 전문)"
        status: pass
    human_judgment: false
  - id: D4
    description: "phase 누적 추가 줄에 삼항·null 병합·null 조건·switch 식·날짜 서명 주석이 0이고, 시작 번호(ZIndexStart) 프로퍼티가 코드에 없으며, csproj·MainView.xaml.cs·SystemHandler.cs 는 phase 동안 바뀌지 않았다"
    requirement: "SZF-05"
    verification:
      - kind: unit
        ref: "grep ternary=0 coalesce=0 nullcond=0 switchexpr=0 datesig=0 (23개 파일 전부) + unexpected_files= csproj=0 mainview_cs=0 systemhandler=0 newcs=0 nostart=0 (77-05-PLAN.md Task 2 자동검증 스크립트)"
        status: pass
    human_judgment: false
  - id: D5
    description: "범위를 쓰지 않는 기존 레시피로 자동 사이클·수동 RUN 을 돌렸을 때 측정값·판정·응답이 이전 버전(1.7.46.0)과 같다"
    requirement: "SZF-05"
    verification: []
    human_judgment: true
    rationale: "정적 감사(D1~D4)는 코드에 회귀 분기가 없음을 증명하지만, 실제 SIMUL/PLC 사이클을 돌려 측정값·판정·TCP 응답이 실측으로 동일한지는 사람이 77-06 UAT U-8(backstop)에서 확인해야 한다 — 이 코드베이스에는 앱을 자동 기동하는 테스트 하네스가 없다."

# Metrics
duration: ~15min
completed: 2026-09-15
status: complete
---

# Phase 77 Plan 05: 버전 표기 + SZF-05 누적 회귀 감사 Summary

**VersionDefine 을 1.7.47.0(SIDE Z 범위 자동 초점 선택 + D-77-08 범위 변경 다이얼로그)으로 올리고, phase 77 전체(77-01~05) 누적 diff 를 BASE(77-01 이전) 기준으로 감사해 허용 목록(ShotConfig.cs 1줄 포함 4개 파일) 외 기존 줄 삭제가 0이고 모든 새 분기가 범위 켜짐 가드 뒤에 있음을 코드 근거로 확정**

## Performance

- **Duration:** ~15 min
- **Completed:** 2026-09-15T04:46:12Z
- **Tasks:** 2
- **Files modified:** 1 (+ 감사 결과 기록용 SUMMARY.md)

## Accomplishments

- `VersionDefine.cs` 마지막 항목(1.7.46.0) 뒤에 `Number = "1.7.47.0"`, `Date = "2026-09-15"` `[Version]` 항목을 추가 — SIDE Z 범위 자동 초점 선택(Shot ZIndexEnd 1칸, PLC 범위 z 촬영, EdgeToLineDistance/Angle 에지 강도 최고점 채택, 3% 동점 규칙, 겹침 제외·편집 경고, 결과 그리드/CSV/오버레이 선택Z, 수동 1장+안내, 오프라인/재검사 z별 사진, 후보 저장 체크박스)와 **D-77-08(범위 변경 시 항상 안내 다이얼로그, 기본 0)**을 한 문단으로 요약
- `VERSION`/`BUILD_DATE` 상수를 `"1.7.47.0"`/`"2026-09-15"` 로 갱신 — 기존 `[Version]` 항목은 하나도 지우지 않음(삭제 2줄은 VERSION/BUILD_DATE 값 줄 자신뿐)
- phase 77 전체 누적(77-01~04 실행 커밋 + D-77-08 out-of-plan 커밋 `b92d57e6`) 을 BASE(77-01-PLAN.md 커밋 직전, `b4218e959eb7eb1c0bae488e9bc83560a8298932`) 대비 23개 파일 diff 로 정적 감사 — 삭제 허용 목록(오케스트레이터 지시로 ShotConfig.cs=1 추가)을 벗어난 파일이 없음을 확인
- 모든 새 판정 진입점이 `IsZRangeEnabled()`(또는 `ResolveZRangeMode` 의 Off 가드) 뒤에서만 실행되고, `EdgeScore` 를 설정하는 곳이 `EdgeToLineAngleMeasurement.cs`/`EdgeToLineDistanceMeasurement.cs` 두 파일뿐이며 4곳 사용처 전부 `EdgeScore != null` 가드 안에 있음을 확인

## Task Commits

1. **Task 1: VersionDefine 1.7.47.0 — SIDE Z 범위 자동 초점 선택 changelog** - `bbbda563` (chore)
2. **Task 2: SZF-05 누적 회귀 감사 — 감사 결과 기록만(코드 변경 없음), 이 SUMMARY 커밋에 포함**

**Plan metadata:** (this commit) `docs(77-05): complete SIDE Z 범위 버전 표기·회귀 감사 plan`

_두 태스크 모두 `type="auto"` — RED/GREEN 분리 없음(TDD 플랜 아님). Task 2 는 감사 전용이라 별도 코드 커밋이 없다._

## Files Created/Modified

- `WPF_Example/VersionDefine.cs` - `[Version(Number = "1.7.47.0", ...)]` 항목 추가, `VERSION`/`BUILD_DATE` 갱신
- `.planning/phases/77-side-z-focus-select/77-05-SUMMARY.md` - 이 문서(Task 2 감사 결과 전문 기록)

## Decisions Made

계획 단계 결정(77-05-PLAN.md must_haves, 오케스트레이터 project_overrides 에서 이 plan 이 반영한 범위):

- VersionDefine changelog 는 phase 77 전체(D-77-01~08, SZF-01~05)를 한 번에 요약하고 물음표·날짜 서명 주석을 쓰지 않는다(하드룰)
- **Task 2 삭제 허용 목록에 `ShotConfig.cs=1` 을 추가** — 오케스트레이터 지시(D-77-08, out-of-plan 커밋 `b92d57e6`)에 따라, 그 커밋이 `public int ZIndex { get; set; } = 0;` 자동 프로퍼티 줄을 지우고 백킹 필드 + 다이얼로그 setter 로 교체한 것을 orchestrator-approved allowlist 확장으로 문서화한다 — 코드를 되돌리지 않는다
- 감사에서 기준을 벗어나는 항목이 나오면 코드를 고쳐 숫자를 맞추지 않고 SUMMARY 에 기록 후 멈춘다는 규칙(STATE 2026-08-27 교훈)을 그대로 따랐다 — 이번 실행은 전부 PASS 라 중단 사유 없음

## 회귀 감사 (Task 2 자동검증 스크립트 출력 전문)

```
phase_base=b4218e959eb7eb1c0bae488e9bc83560a8298932
WPF_Example/Custom/Sequence/Inspection/ShotConfig.cs deleted=1 ternary=0 coalesce=0 nullcond=0 switchexpr=0 datesig=0
WPF_Example/Halcon/Algorithms/VisionAlgorithmService.cs deleted=0 ternary=0 coalesce=0 nullcond=0 switchexpr=0 datesig=0
WPF_Example/Custom/Sequence/Inspection/MeasurementBase.cs deleted=0 ternary=0 coalesce=0 nullcond=0 switchexpr=0 datesig=0
WPF_Example/Custom/Sequence/Inspection/Measurements/EdgeToLineDistanceMeasurement.cs deleted=0 ternary=0 coalesce=0 nullcond=0 switchexpr=0 datesig=0
WPF_Example/Custom/Sequence/Inspection/Measurements/EdgeToLineAngleMeasurement.cs deleted=0 ternary=0 coalesce=0 nullcond=0 switchexpr=0 datesig=0
WPF_Example/Custom/Sequence/Inspection/SkipReason.cs deleted=0 ternary=0 coalesce=0 nullcond=0 switchexpr=0 datesig=0
WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs deleted=0 ternary=0 coalesce=0 nullcond=0 switchexpr=0 datesig=0
WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs deleted=0 ternary=0 coalesce=0 nullcond=0 switchexpr=0 datesig=0
WPF_Example/UI/ViewModel/CycleResultDto.cs deleted=0 ternary=0 coalesce=0 nullcond=0 switchexpr=0 datesig=0
WPF_Example/Custom/Sequence/Inspection/CycleResultSerializer.cs deleted=0 ternary=0 coalesce=0 nullcond=0 switchexpr=0 datesig=0
WPF_Example/Custom/Sequence/Inspection/MeasurementHistoryCsvWriter.cs deleted=1 ternary=0 coalesce=0 nullcond=0 switchexpr=0 datesig=0
WPF_Example/Custom/Sequence/Inspection/MeasurementHistoryCsvLoader.cs deleted=0 ternary=0 coalesce=0 nullcond=0 switchexpr=0 datesig=0
WPF_Example/UI/ViewModel/MeasurementResultRow.cs deleted=0 ternary=0 coalesce=0 nullcond=0 switchexpr=0 datesig=0
WPF_Example/UI/ViewModel/ReviewMeasurementRow.cs deleted=0 ternary=0 coalesce=0 nullcond=0 switchexpr=0 datesig=0
WPF_Example/UI/ContentItem/MainView.xaml deleted=0 ternary=0 coalesce=0 nullcond=0 switchexpr=0 datesig=0
WPF_Example/UI/Reviewer/ReviewerWindow.xaml deleted=0 ternary=0 coalesce=0 nullcond=0 switchexpr=0 datesig=0
WPF_Example/Halcon/Models/EdgeInspectionOverlay.cs deleted=1 ternary=0 coalesce=0 nullcond=0 switchexpr=0 datesig=0
WPF_Example/Halcon/Display/HalconDisplayService.cs deleted=0 ternary=0 coalesce=0 nullcond=0 switchexpr=0 datesig=0
WPF_Example/Setting/SystemSetting.cs deleted=0 ternary=0 coalesce=0 nullcond=0 switchexpr=0 datesig=0
WPF_Example/Utility/CaptureImageSaveService.cs deleted=0 ternary=0 coalesce=0 nullcond=0 switchexpr=0 datesig=0
WPF_Example/Utility/RecipeFileHelper.cs deleted=0 ternary=0 coalesce=0 nullcond=0 switchexpr=0 datesig=0
WPF_Example/Custom/Sequence/Inspection/RepeatRunService.cs deleted=0 ternary=0 coalesce=0 nullcond=0 switchexpr=0 datesig=0
WPF_Example/VersionDefine.cs deleted=2 ternary=0 coalesce=0 nullcond=0 switchexpr=0 datesig=0
unexpected_files=
csproj=0 mainview_cs=0 systemhandler=0 newcs=0
nostart=0
guard[public bool DoesShotOwnZRangeIndex\(]=1
guard[public List<int> BuildZRangeCandidateIndices\(]=1
guard[private bool ShotHasZRangeCompletingAt\(]=1
offfirst=1
edgescore_setters=WPF_Example/Custom/Sequence/Inspection/Measurements/EdgeToLineAngleMeasurement.cs WPF_Example/Custom/Sequence/Inspection/Measurements/EdgeToLineDistanceMeasurement.cs
edgescore_uses=4 edgescore_nullguards=4
savegate=1
```

### PASS/FAIL 판정표

| 기준 | 목표 | 실측 | 판정 |
|---|---|---|---|
| 파일별 deleted | MeasurementHistoryCsvWriter.cs=1, EdgeInspectionOverlay.cs=1, VersionDefine.cs=2, ShotConfig.cs=1(D-77-08 allowlist 확장), 나머지 19개=0 | 위 표와 정확히 일치 | PASS |
| 23개 파일 하드룰(ternary/coalesce/nullcond/switchexpr/datesig) | 전부 0 | 전부 0 | PASS |
| unexpected_files / csproj / mainview_cs / systemhandler / newcs | 전부 비어있음·0 | 전부 비어있음·0 | PASS |
| nostart(ZIndexStart 프로퍼티) | 0 | 0 | PASS |
| guard[DoesShotOwnZRangeIndex]/guard[BuildZRangeCandidateIndices]/guard[ShotHasZRangeCompletingAt] | 전부 >=1 | 전부 1 | PASS |
| offfirst(ResolveZRangeMode 가 IsZRangeEnabled() 를 Auto/Manual/Offline 반환보다 먼저 확인) | 1 | 1 | PASS |
| edgescore_setters | EdgeToLineAngleMeasurement.cs, EdgeToLineDistanceMeasurement.cs 두 파일뿐 | 정확히 이 두 파일 | PASS |
| edgescore_nullguards | >=3 | 4 (아래 근거) | PASS |
| savegate | >=1 | 1 | PASS |

**edgescore_nullguards 근거(VisionAlgorithmService.cs, 사용처가 가드 밖에 없는지 확인):**

```
141:                if (EdgeScore != null) { EdgeScore.Reset(stripCount); } // Phase 77: 분모 = strip 수(에지 없는 strip 도 포함, D-77-05)
173:                        if (EdgeScore != null) { EdgeScore.AddStrip(_dLastStripMaxAbsAmp); } // Phase 77: strip 최대 |amp| 누적
197:                        if (EdgeScore != null) { EdgeScore.AddStrip(_dLastStripMaxAbsAmp); } // Phase 77: strip 최대 |amp| 누적
223:                if (EdgeScore != null)
227:                            EdgeScore.BuildStripListText(), EdgeScore.SumMaxAbsAmp, EdgeScore.StripCount, EdgeScore.Average));
```

`edgescore_uses`(Reset/AddStrip/BuildStripListText 호출) 4곳 — 141/173/197 은 각각 자기 줄의 `if (EdgeScore != null)` 로 인라인 가드, 227(`BuildStripListText` 포함 로그 줄)은 223의 `if (EdgeScore != null)` 블록 안에서만 실행된다. `SumMaxAbsAmp`/`StripCount`/`Average` 프로퍼티 읽기도 같은 223 가드 블록 안이라 EdgeScore 가 null 인 opt-in 미사용 경로(기존 15개 TryFitLine 호출부)에서는 이 코드가 전혀 실행되지 않는다.

## Deviations from Plan

### Auto-fixed Issues

없음 — 코드 변경은 Task 1(VersionDefine.cs)뿐이고 계획 그대로 실행됨.

### Orchestrator-directed allowlist extension (Rule 없음 — 사전 승인된 지시 반영)

**1. Task 2 삭제 허용 목록에 `ShotConfig.cs=1` 추가**
- **Found during:** Task 2 준비(오케스트레이터 project_overrides 확인)
- **Issue:** out-of-plan 커밋 `b92d57e6`("D-77-08 Z 범위 변경 시 항상 다이얼로그 알림")가 `ShotConfig.cs` 의 사전 존재 자동 프로퍼티 줄 `public int ZIndex { get; set; } = 0;` 을 지우고 백킹 필드 + `ZIndexEnd != 0` 일 때만 같은 다이얼로그를 띄우는 setter 로 교체했다. 77-05-PLAN.md 원본 acceptance_criteria 는 ShotConfig.cs 를 "나머지 20개 파일=0" 그룹에 포함하고 있어 그대로면 FAIL.
- **Fix:** 코드를 되돌리지 않고(D-77-08 은 사용자 확정 결정, 오케스트레이터가 명시적으로 되돌리지 말라고 지시), Task 2 감사 스크립트/판정표에서 `ShotConfig.cs=1` 을 허용 목록에 추가해 그대로 기록했다.
- **Files modified:** 없음(코드 변경 없음, 감사 기준만 문서화)
- **Verification:** 위 감사 출력에서 `ShotConfig.cs deleted=1` 확인, 판정표에서 PASS로 기록
- **Committed in:** 이 SUMMARY 커밋(코드 변경이 아니므로 별도 feat/fix 커밋 없음)

---

**Total deviations:** 0 auto-fix + 1 orchestrator-directed allowlist 확장(코드 변경 없음, 문서화만)
**Impact on plan:** 없음. 감사 기준 자체가 오케스트레이터 지시대로 미리 확장되어 PASS/FAIL 판정에 왜곡이 없다.

## Issues Encountered

None. Task 1 편집 직후 Debug|x64 빌드 1회 시도로 에러 0 통과(Release|x64 빌드는 규칙에 따라 시도하지 않음 — OutputPath 가 실행 중인 D:\Data\DatumMeasurement.exe 를 덮어쓸 위험).

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- 버전 1.7.47.0 표기 완료, phase 77 전체(77-01~05)의 정적 회귀 감사가 코드 근거로 확정됨(허용 목록 4개 파일 5줄 외 삭제 0, 모든 새 분기가 가드 뒤)
- **실측 회귀 동일성(범위 안 쓰는 레시피의 측정값·판정·TCP 응답이 1.7.46.0 과 같은지)은 이 plan 범위 밖 — 77-06 UAT U-8(backstop)이 확인한다.**
- 77-06 UAT 는 이전 plan(77-01~04) SUMMARY 에 쌓인 모든 human_judgment 항목(U-1~U-8, O-1 PLC z 번호 배정 협의 포함)을 그대로 이어받는다. 배포(Release 빌드)는 이 phase 범위 밖 — 사용자 승인 후 진행.

## Self-Check: PASSED

- `WPF_Example/VersionDefine.cs` FOUND(디스크 확인), `1.7.47.0` 항목 및 `VERSION`/`BUILD_DATE` 값 확인됨
- 커밋 `bbbda563`(chore) FOUND in `git log --oneline --all`

---
*Phase: 77-side-z-focus-select*
*Completed: 2026-09-15*
