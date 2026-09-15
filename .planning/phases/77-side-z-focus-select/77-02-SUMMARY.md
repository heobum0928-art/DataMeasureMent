---
phase: 77-side-z-focus-select
plan: 02
subsystem: vision-inspection
tags: [halcon, edge-measurement, z-focus-select, tcp-plc, side-camera]

# Dependency graph
requires:
  - phase: 77-side-z-focus-select/77-01
    provides: "Z 범위 tracer 뼈대(ShotConfig.ZIndexEnd, EdgeStrengthScore, MeasurementBase.LastFitScore/LastSelectedZIndex, InspectionSequence.m_dicZRangeImages, Action_FAIMeasurement.TryHandleZRangeMeasurement)"
provides:
  - "Action_FAIMeasurement.PickZFocusResult 3% 기준 Z 동점 규칙 — 기준 Z 채택 시 기준 Z 자신의 Value/Error/Overlays 그대로 재사용(재계산 없음)"
  - "EdgeToLineAngleMeasurement 에지 강도 점수 배선 — EdgeToLineDistance 와 동일 규칙으로 범위 Shot 후보 선택 지원"
  - "Action_FAIMeasurement.ExecuteZRangeBaseImageMeasurement — 에지 강도 선택 미지원 타입은 기준 Z 사진 1회 측정"
  - "Action_FAIMeasurement.ApplyZRangeBaseImageForDisplay — ZIndexEnd tick 화면·원본 사진을 기준 Z 사진으로 교체"
  - "Action_FAIMeasurement.LogZRangeMissingCandidatesIfAny — 중간 z 누락 시 Error 로그 후 도착한 사진으로 계속"
  - "InspectionSequence.IsZIndexReservedOutsideZRange — 다른 Shot·Datum·다른 범위 Shot 이 쓰는 z 를 후보에서 제외(라우팅·저장·후보 공통 가드)"
  - "InspectionSequence.FindShotByZIndex 3패스 — $PREP 조명이 범위 z 에서도 그 범위를 소유한 Shot 조명으로 켜짐"
  - "ShotConfig.WarnZIndexEndChanged/IsZRangeMisconfigured/BuildZRangeMisconfigText — ZIndexEnd 편집 즉시 한국어 경고(로드·붙여넣기는 억제)"
  - "Action_FAIMeasurement.LogZRangeMisconfigIfNeeded — 오설정 범위 Shot 은 tick 마다 Error/Algorithm 로그로 노출"
affects: [77-03-side-z-focus-select, 77-04-side-z-focus-select, 77-05-side-z-focus-select, 77-06-side-z-focus-select]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "동점 규칙을 별도 헬퍼(FindZFocusResultByZ/ApplyBaseZTieRule)로 분리해 PickZFocusResult/LogZFocusSelection 양쪽에서 기준 Z 결과를 재사용 — 가드 절만으로 0 나눗셈·null 케이스를 방어"
    - "77-01 의 DoesShotOwnZRangeIndex 마지막 return 앞에 겹침 제외 가드를 삽입해 라우팅($TEST)·조명($PREP)·저장·후보 목록이 단일 판정 함수를 공유(회귀 0 보장 구조 유지)"
    - "DatumConfig.WarnDatumZIndexChanged 의 _suppressUserEditWarning 관용구를 ShotConfig 에 재사용 — 편집(PropertyGrid)만 경고, INI 로드·붙여넣기는 억제"

key-files:
  created: []
  modified:
    - WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs
    - WPF_Example/Custom/Sequence/Inspection/Measurements/EdgeToLineAngleMeasurement.cs
    - WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs
    - WPF_Example/Custom/Sequence/Inspection/ShotConfig.cs

key-decisions:
  - "동점 규칙: 최고 점수 z 와 기준 Z(ZIndex) 점수 차가 최고 점수의 3.0%(Z_FOCUS_TIE_PERCENT, 화면 미노출 내부 const) 이하(경계 포함)면 기준 Z 를 채택, 초과면 최고 점수 z 를 채택(O-3, D-77-07 ③)"
  - "동점 규칙으로 기준 Z 가 채택되면 기준 Z 후보 자신의 Value/Error/Overlays 를 그대로 반환 — 다른 후보와 섞지 않고 재계산도 하지 않는다(D-77-02, 리서치 Open Question 2 해소)"
  - "최고 점수가 ZFOCUS_FAILED_SCORE(0.0) 이하면 비율 계산 없이 곧바로 기준 Z 채택 — 0 나눗셈 회피(모든 strip 에지 없음 케이스)"
  - "미지원 측정 타입(EdgeToLine* 외)은 후보 반복 없이 기준 Z 사진으로 TryExecuteMeasurement 1회만 실행 — 재선택 없음(D-77-07 ②)"
  - "ZIndexEnd tick 의 화면·원본(측정 소스) 사진은 기준 Z 사진으로 교체된다 — 기준 Z 사진이 없으면 현재 z 사진을 쓰고 Error 로그(O-7)"
  - "겹침 제외 판정(IsZIndexReservedOutsideZRange)은 DoesShotOwnZRangeIndex 내부에서만 호출되고 그 반대는 호출하지 않는다(재귀 방지) — 우선순위: 기준점(GetDatumZIndex) → 크로스-Z Datum → 다른 Shot own ZIndex → 다른 Shot 크로스-Z → 다른 범위 Shot 의 원시 범위(IsZIndexInsideRawZRange)"
  - "ZIndexEnd 편집 경고는 오입력(ZIndex 미설정/역순/상한 초과)을 먼저 확인하고, 없으면 겹침(BuildZRangeConflictText)을 확인 — INI 로드·붙여넣기는 _suppressUserEditWarning 으로 억제, 저장은 막지 않는다(D-77-07 ⑤)"

patterns-established:
  - "Pattern: 범위 후보 선택 로직(점수 비교·동점 처리)은 항상 '먼저 best 를 고르고, 기준 Z 후보를 찾아, 가드 절 체인으로 최종 채택자를 정한다' 순서를 따른다 — PickZFocusResult/ApplyBaseZTieRule 이 이 순서의 참조 구현"

requirements-completed: [SZF-01, SZF-02, SZF-03, SZF-05]

coverage:
  - id: D1
    description: "EdgeToLineAngle 도 EdgeToLineDistance 와 같은 에지 강도 점수로 범위 Shot 후보 선택을 지원, 3% 기준 Z 동점 규칙으로 흔들림 없이 선택되며 기준 Z 채택 시 기준 Z 결과가 재계산 없이 그대로 쓰인다(SZF-03)"
    requirement: "SZF-03"
    verification:
      - kind: unit
        ref: "grep eawire=3 tieconst=1 tieuse=2 pickcall=1 (77-02-PLAN.md Task 1 자동검증 스크립트)"
        status: pass
    human_judgment: true
    rationale: "동점 근처 후보에서 실제로 기준 Z 가 반복 재현성 있게 선택되는지는 SIMUL TCP 반복 사이클의 [ZFocus] 로그를 사람이 눈으로 대조해야 한다 — 77-06 UAT."
  - id: D2
    description: "에지 강도 선택 미지원 타입은 기준 Z 사진으로 1회 측정되고, ZIndexEnd tick 의 화면·원본 사진은 기준 Z 사진으로 교체되며, 중간 z 누락은 Error 로그 후 사이클이 계속된다(SZF-02/SZF-03)"
    requirement: "SZF-02"
    verification:
      - kind: unit
        ref: "grep display=1 baseexec=1 basesel=1 missing=1 baselog=1 (77-02-PLAN.md Task 1 자동검증 스크립트)"
        status: pass
    human_judgment: true
    rationale: "화면 표시가 실제로 기준 Z 사진으로 바뀌어 보이는지, 미지원 타입 로그가 실제 SIMUL 사이클에서 찍히는지는 런타임 증거가 필요하다 — 이 코드베이스에는 앱을 자동 기동하는 테스트 하네스가 없다(77-01 SUMMARY 와 동일 사유) — 77-06 UAT."
  - id: D3
    description: "다른 Shot·Datum·다른 범위 Shot 이 쓰는 z 는 범위 Shot 의 라우팅($TEST)·조명($PREP)·저장·후보 목록에서 전부 제외되고, 기준 Z 자신은 항상 후보로 남는다(SZF-02)"
    requirement: "SZF-02"
    verification:
      - kind: unit
        ref: "grep reserveguard=1 baseorder=1 norecurse=0 prep3=1 preplog=1 declared=1 conflict=1 (77-02-PLAN.md Task 2 자동검증 스크립트)"
        status: pass
    human_judgment: true
    rationale: "겹침 제외가 실제 다중 Shot/Datum 레시피에서 올바른 사진을 골라내는지는 사람이 실제 레시피로 확인해야 한다 — 77-06 UAT U-3."
  - id: D4
    description: "ZIndexEnd 오입력·겹침은 PropertyGrid 편집 즉시 한국어 한 줄 경고(로드·붙여넣기는 억제), 런타임 오설정은 tick 마다 Error/Algorithm 로그로 드러난다(SZF-01/SZF-05)"
    requirement: "SZF-05"
    verification:
      - kind: unit
        ref: "grep warnfn=1 warncall=1 suppress=4 conflictcall=1 misfn=2 misconflog=1 (77-02-PLAN.md Task 2 자동검증 스크립트)"
        status: pass
    human_judgment: true
    rationale: "경고창이 실제 PropertyGrid 편집 시 뜨는지(WPF UI 상호작용)는 사람이 직접 확인해야 한다 — 77-06 UAT U-5."

# Metrics
duration: ~55min
completed: 2026-09-15
status: complete
---

# Phase 77 Plan 02: SIDE Z 범위 선택 정책 완성 — 동점 규칙·겹침 제외·편집 경고 Summary

**77-01 tracer 뼈대에 3% 기준 Z 동점 규칙(재계산 없이 결과 재사용)·EdgeToLineAngle 지원·미지원 타입 기준 Z 사진 측정·화면 사진 기준 Z 교체·중간 z 누락 경고를 얹고, InspectionSequence 겹침 제외 가드로 다른 Shot·Datum 이 쓰는 z 를 후보에서 빼며, ShotConfig.ZIndexEnd 편집 즉시 한국어 경고와 런타임 오설정 로그로 운영 실수를 드러냄**

## Performance

- **Duration:** ~55 min
- **Completed:** 2026-09-15T12:55:40+09:00
- **Tasks:** 2
- **Files modified:** 4

## Accomplishments

- `EdgeToLineAngleMeasurement` 에 `EdgeToLineDistance` 와 동일한 에지 강도 점수 배선(`SupportsEdgeStrengthScore()`, `EdgeScore`, `LastFitScore`) 추가 — 범위 Shot 의 두 지원 측정 타입 모두 자동 선택 대상이 된다(D-77-07 ②)
- `Action_FAIMeasurement.PickZFocusResult` 에 3% 기준 Z 동점 규칙(`Z_FOCUS_TIE_PERCENT` 내부 const, 화면 미노출) 추가 — `FindZFocusResultByZ`/`ApplyBaseZTieRule` 가 가드 절만으로 0 나눗셈 없이 판정하고, 기준 Z 채택 시 기준 Z 자신의 측정 결과를 재계산 없이 그대로 씀(D-77-02)
- `LogZFocusSelection` 로그에 기준 Z 점수·허용치 정보(` 기준 z<N>=<점수> 허용 3.0%` 또는 ` 기준 z<N> 후보 없음`) 추가 — 점수 상세는 이 로그에만 유지(D-77-07 ⑥)
- `EnsureZRangeCandidatesLoaded`(`LogZRangeMissingCandidatesIfAny`) — 기대 z 목록 대비 실제 도착한 후보를 비교해 누락된 z 를 Error 로그 한 줄로 남기고 도착한 사진만으로 선택을 계속함(O-4)
- `RunGrab`(`ApplyZRangeBaseImageForDisplay`) — 완성(ZIndexEnd) tick 에서 화면·원본(측정 소스) 사진을 기준 Z 사진으로 교체, 기준 Z 사진이 없으면 현재 z 사진 유지 + Error 로그(O-7)
- `TryHandleZRangeMeasurement`(`ExecuteZRangeBaseImageMeasurement`) — 에지 강도 선택 미지원 타입은 후보 반복 없이 기준 Z 사진으로 공용 실행 경로를 1회만 태우고 `[ZFocus] 기준 Z 사진 사용` 로그를 남김
- `InspectionSequence.DoesShotOwnZRangeIndex` 마지막 `return true` 앞에 `IsZIndexReservedOutsideZRange` 가드 추가 — 기준 Z(자기 자신)는 항상 소유, 다른 Shot own ZIndex·다른 Shot 크로스-Z·다른 범위 Shot 의 원시 범위·이 시퀀스 기준점·크로스-Z Datum 이 쓰는 z 는 후보에서 제외(T-77-07, D-77-07 ⑤) — `IsZIndexInsideRawZRange` 는 재귀 방지용 원시 판정만 함
- `FindShotByZIndex` 3패스 추가 — `$PREP` 조명이 범위 z 에서도 그 범위를 소유한 Shot 의 조명으로 켜지고 `[PREP ZRange]` 로그를 남김(기존 정확일치·크로스-Z 매칭이 우선, 범위 매칭은 3순위)
- `AddShotDeclaredZIndices` 가 범위 z 도 "레시피에 존재하는 z" 선언 유니버스에 포함 — 크로스-Z 오설정(존재하지 않는 z_index 참조) 오탐 방지
- `BuildZRangeConflictText` — 범위 안에서 다른 Shot·기준점과 겹치는 z 목록을 한국어 한 줄로 조립, `ShotConfig.WarnZIndexEndChanged`(편집 즉시)와 `Action_FAIMeasurement.LogZRangeMisconfigIfNeeded`(런타임 tick)가 공유
- `ShotConfig.WarnZIndexEndChanged`/`IsZRangeMisconfigured`/`BuildZRangeMisconfigText` — `ZIndexEnd` 를 PropertyGrid 에서 직접 바꾸면 즉시 한국어 한 줄 경고(오입력 우선, 없으면 겹침), INI 로드·붙여넣기는 `_suppressUserEditWarning` 으로 억제, 저장은 막지 않음(DatumConfig.WarnDatumZIndexChanged 관용구 재사용)
- `Action_FAIMeasurement.LogZRangeMisconfigIfNeeded` — 오설정으로 조용히 꺼진 범위 Shot 은 Measure 단계마다(사이클당 1회) Error(오입력) 또는 Algorithm(겹침) 로그로 이유를 남김(리서치 Pitfall 4)

## Task Commits

Each task was committed atomically:

1. **Task 1: 선택 정책 완성 — EdgeToLineAngle 점수, 기준 Z 동점 규칙(결과 재사용), 미지원 타입 기준 Z 측정, 화면 사진 기준 Z 교체, 누락 z 경고** - `d60b3e7b` (feat)
2. **Task 2: z 소유 규칙 강화와 운영 실수 경고 — 겹치는 z 제외, $PREP 조명 3패스, ZIndexEnd 편집 즉시 경고, 오설정 런타임 로그** - `a712d3b5` (feat)

**Plan metadata:** (this commit) `docs(77-02): complete SIDE Z 범위 선택 정책 plan`

_두 태스크 모두 `type="auto"` — RED/GREEN 분리 없음(TDD 플랜 아님)._

## Files Created/Modified

- `WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs` - `Z_FOCUS_TIE_PERCENT`/`PERCENT_SCALE` const, `_nZRangeDisplayZIndex`/`_bZRangeMisconfigLogged` 필드, `FindZFocusResultByZ`/`ApplyBaseZTieRule`(PickZFocusResult 확장), `LogZFocusSelection` 기준 Z 정보 추가, `LogZRangeMissingCandidatesIfAny`(EnsureZRangeCandidatesLoaded 확장), `ApplyZRangeBaseImageForDisplay`(RunGrab 호출), `ExecuteZRangeBaseImageMeasurement`(TryHandleZRangeMeasurement 미지원 분기 교체), `LogZRangeMisconfigIfNeeded`(RunMeasure 호출)
- `WPF_Example/Custom/Sequence/Inspection/Measurements/EdgeToLineAngleMeasurement.cs` - `SupportsEdgeStrengthScore()` override(true), `EdgeStrengthScore` 연결, `LastFitScore` 기록(EdgeToLineDistance 배선 그대로 미러링)
- `WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs` - `IsZIndexInsideRawZRange`(재귀방지 원시 판정), `IsZIndexReservedOutsideZRange`(겹침 판정), `BuildZRangeConflictText`, `DoesShotOwnZRangeIndex` 겹침 가드 2줄, `FindShotByZIndex` 3패스 + `[PREP ZRange]` 로그, `AddShotDeclaredZIndices` 범위 z 포함
- `WPF_Example/Custom/Sequence/Inspection/ShotConfig.cs` - `_suppressUserEditWarning` 필드, `IsZRangeMisconfigured()`, `BuildZRangeMisconfigText()`, `WarnZIndexEndChanged()`(ZIndexEnd setter 호출), `Load`/`CopyTo` 억제 플래그 토글

## Decisions Made

계획 단계 결정(77-02-PLAN.md must_haves/threat_model 표에서 이 plan 이 구현한 범위):

- 동점 허용치 3.0%(`Z_FOCUS_TIE_PERCENT`)는 화면에 노출하지 않는 내부 const — 최고 점수와 기준 Z 점수 차가 이 값 이하(경계 포함)면 기준 Z 채택(O-3, D-77-07 ③)
- 기준 Z 채택 시 기준 Z 후보 자신의 Value/Error/Overlays 를 그대로 반환 — 다른 후보와 섞이지 않고 재계산 없음(D-77-02)
- 최고 점수가 `ZFOCUS_FAILED_SCORE`(0.0) 이하면 비율 계산을 건너뛰고 곧바로 기준 Z 채택 — 0 나눗셈 방지
- 겹침 제외 우선순위: 기준점(`GetDatumZIndex()`) → 크로스-Z Datum → 다른 Shot own ZIndex → 다른 Shot 크로스-Z → 다른 범위 Shot 의 원시 범위. 기준 Z(자기 자신)는 이 판정보다 먼저 항상 true
- `$PREP` 조명 3패스 우선순위: 1패스(own ZIndex 정확일치) → 2패스(크로스-Z) → 3패스(범위 소유) — 기존 매칭 결과 불변
- ZIndexEnd 편집 경고는 오입력(ZIndex 미설정/역순/상한 초과)을 먼저 확인하고, 없으면 겹침을 확인 — INI 로드·붙여넣기는 `_suppressUserEditWarning` 으로 억제, 저장은 막지 않음

**경고 문구 원문(3종 + 겹침):**

1. ZIndex 미설정(`< MIN_Z_RANGE_BASE_INDEX`): `"ZIndex 가 1 이상이어야 Z 범위를 쓸 수 있어 Z 범위 기능이 꺼집니다."`
2. 역순(`ZIndexEnd <= ZIndex`): `"Z 범위 끝(N)이 ZIndex(M) 이하라 Z 범위 기능이 꺼집니다."`
3. 상한 초과(`개수 > MAX_Z_RANGE_COUNT`): `"Z 범위가 N개로 최대 10개를 넘어 Z 범위 기능이 꺼집니다."`
4. 겹침(`BuildZRangeConflictText`): `"Z 범위 안의 z3, z5 은 다른 Shot·기준점이 쓰는 번호라 후보에서 빠집니다."`(예시)

**동점 규칙 경계 확인 메모:** `ApplyBaseZTieRule` 의 `dGapPercent <= Z_FOCUS_TIE_PERCENT` 비교는 `<=`(이하, 경계 포함) — 정확히 3.0% 차이면 기준 Z 가 채택된다. 반올림 없이 `double` 원값을 그대로 비교하며, `best.Score`(분모)가 0 이하이면 이 비교식 자체에 도달하지 않고 그 전에 기준 Z 로 조기 반환한다.

## Deviations from Plan

None - plan executed exactly as written. `git diff -w` 로 BASE(77-01 커밋 이전, `b4218e95`)와 비교한 4개 파일 모두 `deleted_since_plan=0`(삭제된 줄 없음). 77-01 커밋(`69d5ba72`) 대비로는 `Action_FAIMeasurement.cs` 에서 4줄이 수정되었는데, 전부 77-01 이 이 plan 을 위해 확장 지점으로 남겨둔 ZFocus 관련 줄이다:
- `TryHandleZRangeMeasurement` 의 미지원 타입 분기 `return false;` → `ExecuteZRangeBaseImageMeasurement(...); return true;` 로 교체
- `PickZFocusResult` 의 `if (best != null) { return best; }` → `if (best == null) { return lstResults[0]; }` 로 반전 후 동점 규칙 호출 추가
- `LogZFocusSelection` 의 `Logging.PrintLog(...)` 마지막 줄 → 기준 Z 정보(`szBaseInfo`)를 덧붙인 버전으로 교체

InspectionSequence.cs/ShotConfig.cs/EdgeToLineAngleMeasurement.cs 는 77-01 대비로도 삭제 0(순수 삽입).

## Issues Encountered

None. Task 1/Task 2 각각 편집 직후 Debug|x64 빌드를 실행해 총 4회 시도 모두 에러 0 으로 통과했다(Release|x64 빌드는 규칙에 따라 시도하지 않음 — OutputPath 가 실행 중인 D:\Data\DatumMeasurement.exe 를 덮어쓸 위험).

## New Symbols (Artifacts 표 77-02 행 대조)

이 목록은 77-03/04/05 가 확장할 대상이다:

- `Action_FAIMeasurement`: `Z_FOCUS_TIE_PERCENT`, `PERCENT_SCALE`, `_nZRangeDisplayZIndex`, `_bZRangeMisconfigLogged`, `FindZFocusResultByZ`, `ApplyBaseZTieRule`, `LogZRangeMissingCandidatesIfAny`, `ApplyZRangeBaseImageForDisplay`, `ExecuteZRangeBaseImageMeasurement`, `LogZRangeMisconfigIfNeeded`, 로그 `[ZFocus] 기준 Z 사진 사용` / `[ZFocus] 기준 Z 사진 없음` / `[ZFocus] 후보 누락` / `[ZFocus] 설정 확인`
- `EdgeToLineAngleMeasurement`: `SupportsEdgeStrengthScore()` override(true)
- `InspectionSequence`: `IsZIndexInsideRawZRange`, `IsZIndexReservedOutsideZRange`, `BuildZRangeConflictText`, 로그 `[PREP ZRange]`
- `ShotConfig`: `_suppressUserEditWarning`, `IsZRangeMisconfigured()`, `BuildZRangeMisconfigText()`, `WarnZIndexEndChanged()`

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- 77-03(CSV/화면 표시)이 쓸 `meas.LastSelectedZIndex`/`LastFitScore` 는 동점 규칙 적용 후에도 채택된 결과 그대로 채워짐(기준 Z 채택 시 `chosen.ZIndex`=기준 Z, `ExecuteZRangeBaseImageMeasurement` 는 `_nZRangeDisplayZIndex` 로 채움) — 별도 작업 불필요
- 77-04(수동/오프라인/재검사)는 `EZRangeMode.ManualSingle`/`OfflineSelect` 분기가 여전히 자리만 비워둔 상태(현재는 둘 다 Off 폴백) — 이 plan 은 그 분기를 건드리지 않음
- **런타임 end-to-end 증거(SIMUL TCP 사이클의 동점 규칙 실제 선택, 겹침 제외로 다른 Shot 사진이 실제로 빠지는지, PropertyGrid 편집 경고창)는 아직 확인되지 않음** — 이 코드베이스에는 앱을 자동 기동하는 테스트 하네스가 없어 77-06 UAT(U-2 동점, U-3 겹침, U-5 경고)에서 사람이 직접 확인해야 한다
- 77-05 누적 감사가 BASE(77-01 이전) 기준 4개 파일 전부 삭제 0 을 재확인할 예정 — 이번 plan 자체 검증에서는 이미 0 으로 확인됨(위 Deviations 참조)

## Self-Check: PASSED

- 4개 소스 파일 + SUMMARY.md 전부 FOUND
- 커밋 2개(`d60b3e7b` feat, `a712d3b5` feat) 전부 FOUND in `git log --oneline --all`

---
*Phase: 77-side-z-focus-select*
*Completed: 2026-09-15*
