---
phase: 77-side-z-focus-select
plan: 01
subsystem: vision-inspection
tags: [halcon, edge-measurement, z-focus-select, tcp-plc, side-camera]

# Dependency graph
requires: []
provides:
  - "ShotConfig.ZIndexEnd 1칸 + IsZRangeEnabled() 가드 — Shot 단위 Z 범위(ZIndex~ZIndexEnd) 온오프 단일 소스"
  - "VisionAlgorithmService.EdgeStrengthScore opt-in 점수 수집기 — TryFitLine 시그니처 무변경, strip 별 최대 |amp| 누적"
  - "MeasurementBase.LastFitScore/LastSelectedZIndex 필드 + FormatSelectedZ/ParseSelectedZ 표시 단일 규칙"
  - "InspectionSequence.m_dicZRangeImages 저장소 + z 라우팅·완성 index·마지막 index·빈 응답 억제 확장"
  - "Action_FAIMeasurement.TryHandleZRangeMeasurement — 후보 누적·중간 z 대기 표시·ZIndexEnd 후보별 실행·최고 점수 채택"
affects: [77-02-side-z-focus-select, 77-03-side-z-focus-select, 77-04-side-z-focus-select]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "opt-in 점수 수집기(EdgeScore=null 이면 no-op) — 기존 15개 TryFitLine 호출부 무변경으로 기능 확장"
    - "부수효과 public 필드(LastFitScore/LastSelectedZIndex)로 측정 결과 노출 — TryExecute 추상 시그니처 불변, INI/복사 오염 없음(LastErrorMessage 필드 선례 재사용)"
    - "Phase 68 크로스-Z 저장소/게이트 패턴을 Shot 단위 N-후보로 일반화 — 별도 사전(m_dicZRangeImages) + 락 재사용, 재계산 없는 후보별 실행·최고점수 채택"

key-files:
  created: []
  modified:
    - WPF_Example/Custom/Sequence/Inspection/ShotConfig.cs
    - WPF_Example/Halcon/Algorithms/VisionAlgorithmService.cs
    - WPF_Example/Custom/Sequence/Inspection/MeasurementBase.cs
    - WPF_Example/Custom/Sequence/Inspection/Measurements/EdgeToLineDistanceMeasurement.cs
    - WPF_Example/Custom/Sequence/Inspection/SkipReason.cs
    - WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs
    - WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs

key-decisions:
  - "P-1: 입력 칸은 ShotConfig.ZIndexEnd 1개(표시명 'Z 범위 끝', 0=꺼짐). 범위=ZIndex~ZIndexEnd, 기준 Z=ZIndex. 시작 번호 칸 없음(D-77-07 ①⑧, D-77-03 대체)"
  - "P-2: IsZRangeEnabled() = ZIndexEnd≠0 이고 ZIndex≥1 이고 ZIndexEnd>ZIndex 이고 z개수≤MAX_Z_RANGE_COUNT(10). 아니면 꺼짐(기존 동작)"
  - "P-3: strip 강도=strip 안 최대 |amp| 1개. 점수=Σ÷실제 strip 수(EdgeSampleCount), 에지 없는 strip=0(D-77-02, D-77-05)"
  - "P-4: TryFitLine 시그니처 불변 — VisionAlgorithmService.EdgeScore opt-in 프로퍼티(null이면 계산·로그 0)"
  - "P-5: MeasurementBase.LastFitScore/LastSelectedZIndex 는 필드(프로퍼티 아님) — INI 저장·붙여넣기 복사 대상 제외"
  - "P-6: 중간 z 대기 = SkipReason.Z_RANGE_PENDING, LastJudgement=false, 응답은 완성 index 게이트로 제외"
  - "P-7: 평가 시점 = ZIndexEnd tick 1회. 후보는 그 시점에 소유권째 꺼내(추가 복사 없음) 측정 직후 Dispose"
  - "P-10: 수동 RUN·수동 트리거(Sender 없음)·오프라인은 이 plan 에서 기존 단일 사진 경로 그대로(선택 없음) — 안내 문구·오프라인 선택은 77-04"
  - "flagged assumption A-77-E1(SZF-02 ordering): PLC 가 ZIndexEnd 를 두 번 보내면 첫 평가 후 후보가 Dispose 되어 두 번째는 ZIndexEnd 사진 1장으로 재선택, 결과·CSV 행이 한 번 더 생김. ZIndexEnd 뒤 늦게 온 중간 z 는 평가되지 않고 다음 사이클 시작에 비워짐 — 77-06 UAT U-4 에서 사용자 확인"
  - "flagged assumption A-77-O1/O8/O9: O-1(PLC z 번호 배정) 미결 → SIDE 실기 UAT 는 77-06 체크포인트에서 협의 후. O-8 사이클 타임, O-9 Z 이동 중 XY 흔들림은 실측 전 미지수"

patterns-established:
  - "Pattern: Z 범위 온/오프 단일 진실원(ShotConfig.IsZRangeEnabled())을 모든 새 분기의 첫 가드로 사용 — 옛 레시피/TOP/BOTTOM은 항상 첫 가드에서 false로 빠져 회귀 0을 보장"

requirements-completed: [SZF-01, SZF-02, SZF-03, SZF-05]

coverage:
  - id: D1
    description: "ShotConfig.ZIndexEnd 1칸으로 범위 온오프, 키 없는 옛 레시피는 꺼짐(SZF-01)"
    requirement: "SZF-01"
    verification:
      - kind: unit
        ref: "grep zend=1 desc=1 enabled=1 nostart=0 (77-01-PLAN.md 자동검증 스크립트)"
        status: pass
    human_judgment: false
  - id: D2
    description: "PLC 자동 사이클에서 범위 Shot 이 ZIndex~ZIndexEnd z 마다 사진을 m_dicZRangeImages 에 누적, 범위 밖 z 는 저장 안 함(SZF-02)"
    requirement: "SZF-02"
    verification:
      - kind: unit
        ref: "grep storecall=1 route=1 lifecycle=3 (77-01-PLAN.md 자동검증 스크립트)"
        status: pass
    human_judgment: true
    rationale: "실제 PLC/SIMUL TCP 사이클에서의 z 누적·저장 동작은 런타임 증거(로그)가 필요하다 — 이 코드베이스에는 앱을 자동 기동하는 테스트 하네스가 없어 77-06 UAT U-1 에서 확인한다."
  - id: D3
    description: "ZIndexEnd tick 에서 EdgeToLineDistance 후보별 실제 측정 후 에지 강도 최고 점수 z 를 재계산 없이 채택, 완성 index·마지막 Index 에 반영(SZF-03)"
    requirement: "SZF-03"
    verification:
      - kind: unit
        ref: "grep gate=1 selz=1 completion=1 aggregate=1 lastidx=1 edwire=3 (77-01-PLAN.md 자동검증 스크립트)"
        status: pass
    human_judgment: true
    rationale: "점수 계산·선택 로직의 실제 정확도(가장 선명한 z 가 정말 채택되는지)는 SIMUL TCP 모의 사이클의 [ZFocus]/[FitLine] 로그를 사람이 눈으로 확인해야 한다 — 77-06 UAT U-1."
  - id: D4
    description: "범위 꺼짐 Shot·TOP/BOTTOM·수동 경로는 기존 줄 삭제 0, 새 분기 전부 IsZRangeEnabled() 가드 뒤(SZF-05)"
    requirement: "SZF-05"
    verification:
      - kind: unit
        ref: "grep deleted=0 (7개 파일) + offfirst=1 ownguard=1 (77-01-PLAN.md 자동검증 스크립트)"
        status: pass
    human_judgment: false

# Metrics
duration: ~35min
completed: 2026-09-15
status: complete
---

# Phase 77 Plan 01: SIDE Z 범위 자동 초점 선택 — Tracer Summary

**SIDE 범위 Shot 1개(ZIndex~ZIndexEnd) × EdgeToLineDistance 측정 1종을 PLC 자동 사이클 경로로 끝까지 관통 — z 마다 사진 누적, ZIndexEnd tick 에서 후보별 실측·에지 강도(strip 최대 |amp| 평균) 최고 z 채택, 완성 index 응답까지 회귀 0으로 연결**

## Performance

- **Duration:** ~35 min
- **Completed:** 2026-09-15T12:42:32+09:00
- **Tasks:** 1 (tracer)
- **Files modified:** 7

## Accomplishments

- `ShotConfig.ZIndexEnd`(표시명 "Z 범위 끝") 1칸 + `IsZRangeEnabled()` 단일 가드로 Z 범위 온/오프를 표현 — 시작 번호 칸 없이 기존 `ZIndex` 를 범위 시작으로 재사용(D-77-07 ①⑧)
- `VisionAlgorithmService.EdgeStrengthScore` opt-in 점수 수집기 — `TryFitLine` 시그니처를 바꾸지 않고 strip 별 최대 |amp| 를 모아 Σ÷strip수 점수를 계산, `[FitLine] edge-strength` 로그로 strip 상세를 남김(D-77-02/D-77-05)
- `MeasurementBase.LastFitScore`/`LastSelectedZIndex` 필드(프로퍼티 아님, INI/복사 제외) + `FormatSelectedZ`/`ParseSelectedZ` 단일 표시 규칙(77-03이 CSV/화면에서 재사용)
- `InspectionSequence.m_dicZRangeImages` 후보 저장소(크로스-Z 사전과 별도, 락 재사용) + `DoesShotOwnZRangeIndex`/`BuildZRangeCandidateIndices`/z 라우팅·완성 index(`ZIndexEnd`)·마지막 index·빈 응답 억제 확장
- `Action_FAIMeasurement.TryHandleZRangeMeasurement` — 중간 z 는 `SkipReason.Z_RANGE_PENDING` 대기 표시, `ZIndexEnd` tick 은 후보 사진마다 `TryExecuteMeasurement` 를 실제로 돌려 재계산 없이 최고 점수 결과를 그대로 채택하고 `[ZFocus]` 로그로 후보·점수·선택을 남김
- 범위 꺼짐 Shot·TOP/BOTTOM·수동(RUN/수동 트리거)/오프라인 경로는 기존 줄 삭제 0으로 완전히 동일 — 모든 새 분기가 `IsZRangeEnabled()`(또는 `ResolveZRangeMode()` 의 Off 가드)뒤에서만 실행

## Task Commits

1. **Task 1 (tracer): 범위 Shot 1개 × EdgeToLineDistance — z 마다 사진 누적, ZIndexEnd 에서 후보별 측정·점수 선택, 완성 index 응답까지 한 경로** - `69d5ba72` (feat)

**Plan metadata:** (this commit) `docs(77-01): complete SIDE Z 범위 tracer plan`

_단일 tracer 태스크 — RED/GREEN 분리 없음(TDD 플랜 아님)._

## Files Created/Modified

- `WPF_Example/Custom/Sequence/Inspection/ShotConfig.cs` - `ZIndexEnd` 프로퍼티, `Z_RANGE_OFF`/`MIN_Z_RANGE_BASE_INDEX`/`MAX_Z_RANGE_COUNT` const, `IsZRangeEnabled()`
- `WPF_Example/Halcon/Algorithms/VisionAlgorithmService.cs` - `EdgeScore` opt-in 프로퍼티, `_dLastStripMaxAbsAmp`/`ComputeStripMaxAbsAmp`, `[FitLine] edge-strength` 로그, `EdgeStrengthScore` 클래스(파일 하단, 같은 namespace)
- `WPF_Example/Custom/Sequence/Inspection/MeasurementBase.cs` - `LastFitScore`/`LastSelectedZIndex` 필드, `SELECTED_Z_NONE`/`SELECTED_Z_PREFIX`, `SupportsEdgeStrengthScore()`, `FormatSelectedZ`/`ParseSelectedZ`
- `WPF_Example/Custom/Sequence/Inspection/Measurements/EdgeToLineDistanceMeasurement.cs` - `SupportsEdgeStrengthScore()` override(true), `EdgeStrengthScore` 연결, `LastFitScore` 기록
- `WPF_Example/Custom/Sequence/Inspection/SkipReason.cs` - `Z_RANGE_PENDING` 상수
- `WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs` - `m_dicZRangeImages` 저장소(Store/Take/Has/TakeAll/Clear), `DoesShotOwnZRangeIndex`/`BuildZRangeCandidateIndices`/`IsZIndexUsedByZRangeCapture`/`ShotHasZRangeCompletingAt`/`MaxZRangeCompletionZIndex`/`IsManualTriggerCycle`, z 라우팅·완성 index·마지막 index·빈 응답 억제 확장 4곳
- `WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs` - `EZRangeMode`/`ZFocusRunResult`, `ResolveZRangeMode`/`StoreZRangeCandidateImage`/`TryHandleZRangeMeasurement`/`MarkMeasurementZRangePending`/`EnsureZRangeCandidatesLoaded`/`ReleaseZRangeCandidates`/`ExecuteZRangeSelection`/`RunZFocusCandidates`/`PickZFocusResult`/`LogZFocusSelection`, RunInit/RunGrab/RunMeasure/ProcessOneMeasurement 삽입 4곳

## Decisions Made

계획 단계 결정(77-01-PLAN.md "계획 단계 결정" 표, phase 전체 P-1~P-13 중 이 plan 이 구현한 범위):

- **P-1** 입력 칸: `ShotConfig.ZIndexEnd` 1개(0=꺼짐). 범위=`ZIndex`~`ZIndexEnd`, 기준 Z=`ZIndex`. 시작 번호 칸 없음(D-77-07 ①⑧, D-77-03 대체)
- **P-2** 켜짐 규칙: `IsZRangeEnabled()` = `ZIndexEnd`≠0 & `ZIndex`≥1 & `ZIndexEnd`>`ZIndex` & z개수≤`MAX_Z_RANGE_COUNT`(10). 아니면 꺼짐(O-2, 메모리 127~152MB/장 가드)
- **P-3** 점수: strip 강도=strip 안 최대 |amp| 1개(모든 EdgeSelection 공통). 점수=Σ÷실제 strip 수(`EdgeSampleCount`), 에지 없는 strip=0(D-77-02, D-77-05)
- **P-4** 점수 전달: `TryFitLine` 시그니처 불변, opt-in `EdgeScore` 프로퍼티(기본 null → 기존 15개 호출부 무변경)
- **P-5** 측정 결과 필드: `LastFitScore`/`LastSelectedZIndex` 는 필드(프로퍼티 아님) — INI 저장·붙여넣기 복사 제외
- **P-6** 대기 표시: 중간 z = `SkipReason.Z_RANGE_PENDING`, `LastJudgement=false`, `FaiAllPass=false`. 응답은 완성 index 게이트로 제외
- **P-7** 평가 시점: `ZIndexEnd` tick 1회. 후보는 그 시점에 소유권째 꺼내(추가 복사 없음) 측정 직후 Dispose(O-5)
- **P-10** 수동·오프라인: 라이브 수동(RUN, 수동 트리거)=1장·선택 없음, 오프라인/재검사=이 plan 에서는 전부 기존 경로(Off). 구현은 77-04(D-77-06)

**Flagged assumptions (자동 해소 금지, 문서 그대로 옮김):**

- **A-77-E1** (SZF-02 ordering): PLC 가 `ZIndexEnd` 를 두 번 보내면 첫 평가 후 후보가 이미 Dispose 되어 두 번째 평가는 `ZIndexEnd` 사진 1장으로 다시 선택하고 결과·CSV 행이 한 번 더 생긴다. `ZIndexEnd` 뒤에 늦게 온 중간 z 사진은 평가되지 않고 다음 사이클 시작에 비워진다. O-5(즉시 해제)를 우선한 결과이며 77-06 UAT U-4 에서 사용자 확인.
- **A-77-O1/O8/O9**: O-1(PLC z 번호 배정) 미결 → SIDE 실기 UAT 는 77-06 체크포인트에서 협의 후. O-8 사이클 타임, O-9 Z 이동 중 XY 흔들림은 실측 전 미지수 — 로그의 측정 시간(ms)과 UAT 로 확인.

## Deviations from Plan

None - plan executed exactly as written. 모든 삽입은 기존 줄 삭제 0(`git diff -w` 7개 파일 전부 `deleted=0`), CLAUDE.md 가독성 규칙 grep 5종(삼항/`??`/`?.`/switch식/`hbk` 날짜주석) 전부 0.

## Issues Encountered

None. Debug|x64 빌드 1회 시도 만에 에러 0으로 통과했다(Release|x64 빌드는 규칙에 따라 시도하지 않음 — OutputPath가 실행 중인 D:\Data\DatumMeasurement.exe 를 덮어쓸 위험).

## New Symbols (Artifacts 표 77-01 행 대조)

이 목록은 77-02/03/04 가 확장할 대상이다(`ResolveZRangeMode`·`PickZFocusResult`·`EnsureZRangeCandidatesLoaded`·`FormatSelectedZ` 등):

- `ShotConfig`: `Z_RANGE_OFF`, `MIN_Z_RANGE_BASE_INDEX`, `MAX_Z_RANGE_COUNT`, `ZIndexEnd`, `IsZRangeEnabled()`
- `VisionAlgorithmService`: `EdgeScore`, `_dLastStripMaxAbsAmp`, `NO_EDGE_STRIP_AMP`, `ComputeStripMaxAbsAmp`, `[FitLine] edge-strength` 로그, `EdgeStrengthScore` 클래스(Reset/AddStrip/StripCount/SumMaxAbsAmp/Average/BuildStripListText)
- `MeasurementBase`: `SELECTED_Z_NONE`, `SELECTED_Z_PREFIX`, `MIN_SELECTED_Z_INDEX`, `LastFitScore`, `LastSelectedZIndex`, `SupportsEdgeStrengthScore()`, `FormatSelectedZ(int)`, `ParseSelectedZ(string)`
- `EdgeToLineDistanceMeasurement`: `SupportsEdgeStrengthScore()` override(true)
- `SkipReason`: `Z_RANGE_PENDING`
- `InspectionSequence`: `m_dicZRangeImages`, `ZRANGE_KEY_SEPARATOR`, `BuildZRangeImageKey`, `StoreZRangeImage`, `TakeZRangeImageCopy`, `HasZRangeImage`, `TakeZRangeImages`, `ClearZRangeImages`, `DoesShotOwnZRangeIndex`, `BuildZRangeCandidateIndices`, `IsZIndexUsedByZRangeCapture`, `ShotHasZRangeCompletingAt`, `MaxZRangeCompletionZIndex`, `IsManualTriggerCycle`
- `Action_FAIMeasurement`: `EZRangeMode`, `ZFOCUS_LOG_TAG`, `ZFOCUS_SCORE_FORMAT`, `ZFOCUS_FAILED_SCORE`, `_lstZRangeCandidates`, `_bZRangeCandidatesLoaded`, `ZFocusRunResult`, `ResolveZRangeMode`, `StoreZRangeCandidateImage`, `TryHandleZRangeMeasurement`, `MarkMeasurementZRangePending`, `EnsureZRangeCandidatesLoaded`, `ReleaseZRangeCandidates`, `ExecuteZRangeSelection`, `RunZFocusCandidates`, `PickZFocusResult`, `LogZFocusSelection`, `[ZFocus]` 로그(대기/후보 저장/후보 없음/선택)

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- 77-02(겹침 제외·동점 규칙·EdgeToLineAngle·편집 경고)가 붙을 뼈대 완성: `DoesShotOwnZRangeIndex` 마지막 return 앞에 겹침 가드 추가 지점, `PickZFocusResult` 에 동점 3% 규칙 추가 지점, `TryHandleZRangeMeasurement` 의 `bScoreSupported=false` 분기(미지원 타입 기준 Z 사진 경로) 확장 지점이 모두 준비됨
- 77-03(CSV/화면 표시)은 `MeasurementBase.FormatSelectedZ`/`ParseSelectedZ` 를 그대로 재사용 가능
- 77-04(수동/오프라인/재검사, 후보 사진 저장)는 `EZRangeMode.ManualSingle`/`OfflineSelect` 분기가 `ResolveZRangeMode`/`TryHandleZRangeMeasurement` 에 이미 자리만 비워둔 상태(현재는 둘 다 Off로 폴백)
- **런타임 end-to-end 증거(SIMUL TCP 사이클의 `[ZFocus] 대기`·`[ZFocus] 선택`·`[FitLine] edge-strength` 로그와 PLC 응답 B/P/F)는 아직 확인되지 않음** — 이 코드베이스에는 앱을 자동 기동하는 테스트 하네스가 없어 77-06 UAT U-1 에서 사람이 직접 확인해야 한다. O-1(PLC z 번호 배정)도 제어팀 협의 전이라 SIDE 실기 UAT 는 77-06 체크포인트 이후로 남아있다.

---
*Phase: 77-side-z-focus-select*
*Completed: 2026-09-15*
