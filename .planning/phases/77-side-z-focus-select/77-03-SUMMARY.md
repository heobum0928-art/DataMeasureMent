---
phase: 77-side-z-focus-select
plan: 03
subsystem: vision-inspection
tags: [halcon, edge-measurement, z-focus-select, csv-export, reviewer-ui]

# Dependency graph
requires:
  - phase: 77-side-z-focus-select/77-01
    provides: "MeasurementBase.LastSelectedZIndex/FormatSelectedZ/ParseSelectedZ 단일 표시 규칙, SkipReason.Z_RANGE_PENDING"
  - phase: 77-side-z-focus-select/77-02
    provides: "동점 규칙 적용 후에도 LastSelectedZIndex/LastFitScore 가 채택된 결과 그대로 채워짐"
provides:
  - "CycleResultDto.MeasurementResultDto.SelectedZIndex(cycle.json 에 기록되는 선택 Z, 기본 -1)"
  - "MeasurementHistoryCsvWriter/Loader 의 CSV '선택Z' 열(맨 끝, 인덱스 15, COLUMN_COUNT 14 유지)"
  - "MeasurementResultRow.SelectedZText / ReviewMeasurementRow.SelectedZText — 결과·리뷰어 그리드 선택Z 칸"
  - "ReviewMeasurementRow.JUDGE_Z_RANGE_PENDING(\"Z 범위 대기\") — 중간 z tick 이 NG 로 안 보임"
  - "EdgeInspectionOverlay.SelectedZLabel — 오버레이 강조 라벨 뒤 ' z5' 표시 자리(값 채우기는 77-04)"
affects: [77-04-side-z-focus-select, 77-05-side-z-focus-select, 77-06-side-z-focus-select]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "단일 포맷 소스 재사용 — CSV/화면/오버레이 어디서도 선택 Z 형식을 새로 정의하지 않고 MeasurementBase.FormatSelectedZ/ParseSelectedZ 만 호출"
    - "CSV trailing 컬럼 하위호환 — COLUMN_COUNT 불변, fields.Count > COL_SELECTED_Z 가드로 옵션 열 파싱(기존 검사구분 컬럼 선례 재사용)"
    - "표시 전용 필드 분리 — EdgeInspectionOverlay.SelectedZLabel 을 MeasurementName 과 분리해 HighlightMeasurementName 문자열 비교 불변"

key-files:
  created: []
  modified:
    - WPF_Example/UI/ViewModel/CycleResultDto.cs
    - WPF_Example/Custom/Sequence/Inspection/CycleResultSerializer.cs
    - WPF_Example/Custom/Sequence/Inspection/MeasurementHistoryCsvWriter.cs
    - WPF_Example/Custom/Sequence/Inspection/MeasurementHistoryCsvLoader.cs
    - WPF_Example/UI/ViewModel/MeasurementResultRow.cs
    - WPF_Example/UI/ViewModel/ReviewMeasurementRow.cs
    - WPF_Example/UI/ContentItem/MainView.xaml
    - WPF_Example/UI/Reviewer/ReviewerWindow.xaml
    - WPF_Example/Halcon/Models/EdgeInspectionOverlay.cs
    - WPF_Example/Halcon/Display/HalconDisplayService.cs

key-decisions:
  - "R-1: CSV 열 이름은 한국어 '선택Z', 값은 'z5'/빈칸 — 기존 '검사구분' 표기 관습과 통일(D-77-07 ⑥)"
  - "R-2: 결과 그리드는 MeasurementResultRow VM 에 프로퍼티만 추가, XAML 은 열 한 줄 삽입 — MainView.xaml.cs 무수정(CLAUDE.md MVVM 규칙)"
  - "R-3: 오버레이 라벨은 MeasurementName 에 섞지 않고 별도 SelectedZLabel — HighlightMeasurementName 문자열 비교 보존"
  - "점수(LastFitScore) 는 화면·CSV·cycle.json 어디에도 기록하지 않는다 — Algorithm 로그([ZFocus])에만 유지(D-77-07 ⑥)"

patterns-established:
  - "Pattern: 새 표시/기록 필드는 항상 MeasurementBase.FormatSelectedZ/ParseSelectedZ 한 곳만 호출한다 — 77-04/77-05 가 값을 채우는 지점(Action_FAIMeasurement)에서도 이 규칙을 그대로 따를 것"

requirements-completed: [SZF-04, SZF-05]

coverage:
  - id: D1
    description: "cycle.json 의 측정 항목에 SelectedZIndex(int, 기본 -1)가 기록되고, CycleResultSerializer.BuildDto 가 meas.LastSelectedZIndex 를 그대로 복사한다(SZF-04)"
    requirement: "SZF-04"
    verification:
      - kind: unit
        ref: "grep dtoprop=1 sercopy=1 (77-03-PLAN.md Task 1 자동검증 스크립트, 이 세션 재실행 결과와 동일)"
        status: pass
    human_judgment: false
  - id: D2
    description: "일자별 CSV 맨 끝(검사구분 뒤, 인덱스 15)에 '선택Z' 열이 붙고, COLUMN_COUNT=14 유지로 옛 14/15컬럼 CSV 도 손상 행으로 걸러지지 않는다(SZF-04 edge adjacency/empty/ordering)"
    requirement: "SZF-04"
    verification:
      - kind: unit
        ref: "grep header=1 mapfn=1 mapadd=1 fmtreuse=1 colconst=1 colcount=1 parseguard=1 parsecall=1 (Task 1 자동검증 스크립트)"
        status: pass
    human_judgment: true
    rationale: "실제로 이미 누적된 현장 CSV(14/15컬럼 혼재)를 로더가 손상 없이 파싱하는지는 그 CSV 파일로 사람이 직접 조회해봐야 한다 — 77-06 UAT U-6."
  - id: D3
    description: "결과 그리드·리뷰어 그리드에 '선택Z' 열이 보이고 값은 'z5'/빈칸이며, 리뷰어 판정 칸은 Z_RANGE_PENDING 측정에 'Z 범위 대기' 로 표시된다. code-behind 무수정(SZF-04)"
    requirement: "SZF-04"
    verification:
      - kind: unit
        ref: "grep mrprop=1 mrraise=1 mrfmt=1 rrprop=1 rrassign=1 rrlabel=1 mxcol=1 rxcol=1 codebehind=0 (Task 2 자동검증 스크립트)"
        status: pass
    human_judgment: true
    rationale: "WPF DataGrid 열이 실제 화면에 잘리지 않고 보이는지, 값이 시각적으로 올바른 위치에 표시되는지는 사람이 앱을 띄워 확인해야 한다 — 77-06 UAT U-6."
  - id: D4
    description: "Z_RANGE_PENDING 측정은 tick 요약(FillTickSummary)과 리뷰어 목록 라벨의 '처리됨'·NG 집계에서 CROSS_Z_INCOMPLETE 와 같이 제외된다"
    requirement: "SZF-04"
    verification:
      - kind: unit
        ref: "grep dtopending=1 serpending=1 (Task 1 자동검증 스크립트)"
        status: pass
    human_judgment: true
    rationale: "중간 z tick 이 실제 SIMUL TCP 사이클에서 리뷰어 목록에 NG 로 뜨지 않는지는 런타임 로그·화면을 사람이 대조해야 한다(테스트 하네스 부재, 77-01/02 SUMMARY 와 동일 사유) — 77-06 UAT."
  - id: D5
    description: "측정 오버레이 라벨(강조 대상)에 선택 Z 가 있으면 이름 뒤에 ' z5' 가 붙고, HighlightMeasurementName 비교는 기존 MeasurementName 그대로라 깨지지 않는다"
    requirement: "SZF-05"
    verification:
      - kind: unit
        ref: "grep prop=1 clone=1 label=1 highlight=1 (Task 3 자동검증 스크립트)"
        status: pass
    human_judgment: true
    rationale: "SelectedZLabel 값 채우기는 이 plan 범위 밖(77-04, Action 소유)이라 실제 화면에 ' z5' 문구가 뜨는지는 77-04 완료 후에나 확인 가능 — 그 전까지는 배선만 검증됨."

# Metrics
duration: ~6min
completed: 2026-09-15
status: complete
---

# Phase 77 Plan 03: SIDE Z 범위 선택 결과 기록·표시 Summary

**측정별 선택 Z 를 cycle.json(SelectedZIndex)·일자별 CSV(선택Z 열)에 한 칸으로 기록하고, 결과 그리드·리뷰어 그리드·측정 오버레이 라벨에 같은 값을 표시하며, 중간 z 대기 측정을 tick 판정·리뷰어 NG 집계에서 제외한다 — 점수 상세는 어디에도 남기지 않는다**

## Performance

- **Duration:** ~6 min (커밋 간격 기준; 파일 읽기/검증 포함 세션 전체는 더 길었음)
- **Completed:** 2026-09-15T13:02:38+09:00
- **Tasks:** 3
- **Files modified:** 10

## Accomplishments

- `CycleResultDto.MeasurementResultDto.SelectedZIndex`(기본 `MeasurementBase.SELECTED_Z_NONE`=-1) 추가 — `CycleResultSerializer.BuildDto` 가 `meas.LastSelectedZIndex` 를 그대로 복사해 cycle.json 에 기록(D-77-07 ⑥)
- `ReviewerListLabelBuilder.IsHandled`/`CycleResultSerializer.FillTickSummary` 양쪽에 `SkipReason.Z_RANGE_PENDING` 제외 분기 추가 — 중간 z tick 이 `CROSS_Z_INCOMPLETE` 와 같은 방식으로 tick 판정·리뷰어 목록 NG 집계에서 빠짐
- `MeasurementHistoryCsvWriter`: `CSV_HEADER` 맨 끝(검사구분 뒤)에 `선택Z` 열 추가, `MapSelectedZ` 가 `MeasurementBase.FormatSelectedZ` 를 그대로 호출(형식 규칙 복제 없음)
- `MeasurementHistoryCsvLoader`: `COL_SELECTED_Z`=15 옵션 열 상수 + `ParseSelectedZIndex`(`fields.Count > COL_SELECTED_Z` 가드, `MeasurementBase.ParseSelectedZ` 재사용) — `COLUMN_COUNT`=14 그대로 유지해 옛 14/15컬럼 CSV 도 손상 행 취급 없음
- `MeasurementResultRow.SelectedZText`/`ReviewMeasurementRow.SelectedZText` — 결과 그리드·리뷰어 그리드에 동일한 `FormatSelectedZ` 출력을 바인딩, `MainView.xaml`/`ReviewerWindow.xaml` 에 "선택Z" 열 한 줄씩 삽입(code-behind 무수정)
- `ReviewMeasurementRow.JUDGE_Z_RANGE_PENDING`="Z 범위 대기" — `CROSS_Z_INCOMPLETE` 분기와 `MEASURE_FAIL` 분기 사이에 새 판정 분기 삽입
- `EdgeInspectionOverlay.SelectedZLabel` 프로퍼티 + `Clone()` 배선(구 cycle.json 은 null 로 자연 폴백) — `HalconDisplayService.RenderMeasurementNameLabel` 이 `LABEL_TEXT_SEPARATOR`(" ")로 이름 뒤에 이어 씀, `HighlightMeasurementName` 비교는 `overlay.MeasurementName` 그대로 유지

## Task Commits

Each task was committed atomically:

1. **Task 1: 기록 — cycle.json SelectedZIndex, CSV '선택Z' 열 쓰기·읽기, 대기 상태 tick 집계 제외** - `49f1a29e` (feat)
2. **Task 2: 표시 — 결과 그리드·리뷰어 그리드 '선택Z' 열, 리뷰어 판정 'Z 범위 대기'** - `8fd236f9` (feat)
3. **Task 3: 오버레이 라벨 — EdgeInspectionOverlay.SelectedZLabel 과 강조 라벨 뒤 ' z5' 출력** - `ac74d1ce` (feat)

**Plan metadata:** (this commit) `docs(77-03): complete SIDE Z 범위 선택 결과 기록·표시 plan`

_세 태스크 모두 `type="auto"` — RED/GREEN 분리 없음(TDD 플랜 아님)._

## Files Created/Modified

- `WPF_Example/UI/ViewModel/CycleResultDto.cs` - `MeasurementResultDto.SelectedZIndex`, `ReviewerListLabelBuilder.IsHandled` Z_RANGE_PENDING 제외
- `WPF_Example/Custom/Sequence/Inspection/CycleResultSerializer.cs` - `measDto.SelectedZIndex` 대입, `FillTickSummary` Z_RANGE_PENDING 제외
- `WPF_Example/Custom/Sequence/Inspection/MeasurementHistoryCsvWriter.cs` - `CSV_HEADER` 끝 `,선택Z`, `MapSelectedZ(MeasurementResultDto)`
- `WPF_Example/Custom/Sequence/Inspection/MeasurementHistoryCsvLoader.cs` - `COL_SELECTED_Z`=15, `ParseSelectedZIndex(List<string>)`
- `WPF_Example/UI/ViewModel/MeasurementResultRow.cs` - `SelectedZText`, `Refresh()` 알림 추가
- `WPF_Example/UI/ViewModel/ReviewMeasurementRow.cs` - `SelectedZText`, `JUDGE_Z_RANGE_PENDING`, 판정 분기
- `WPF_Example/UI/ContentItem/MainView.xaml` - "선택Z" DataGridTextColumn
- `WPF_Example/UI/Reviewer/ReviewerWindow.xaml` - "선택Z" DataGridTextColumn
- `WPF_Example/Halcon/Models/EdgeInspectionOverlay.cs` - `SelectedZLabel` 프로퍼티 + `Clone()` 배선
- `WPF_Example/Halcon/Display/HalconDisplayService.cs` - `LABEL_TEXT_SEPARATOR`, `RenderMeasurementNameLabel` 이어쓰기

## Decisions Made

계획 단계 결정(77-03-PLAN.md "계획 단계 결정" 표에서 이 plan 이 구현한 범위):

- **R-1** CSV 열 이름 한국어 `선택Z`, 값 `z5`/빈칸 — 기존 `검사구분` 열 표기 관습과 통일(D-77-07 ⑥)
- **R-2** 결과 그리드는 `MeasurementResultRow` VM 프로퍼티 추가 + XAML 열 한 줄만 — `MainView.xaml.cs` 무수정(CLAUDE.md MVVM 규칙)
- **R-3** 오버레이 라벨은 `MeasurementName` 과 분리된 `SelectedZLabel` — `HighlightMeasurementName` 문자열 비교를 깨지 않기 위해

## Deviations from Plan

None - plan executed exactly as written. `git diff -w` 로 BASE(77-02 커밋 이후, 77-03-PLAN.md 커밋 직전) 대비 비교한 10개 파일 모두 계획에서 허용한 삭제 수와 정확히 일치:
- `MeasurementHistoryCsvWriter.cs` deleted=1(CSV_HEADER 줄)
- `EdgeInspectionOverlay.cs` deleted=1(Clone 초기화 `LineColumn2 = LineColumn2,` 쉼표 추가)
- 나머지 8개 파일 deleted=0

CLAUDE.md 가독성 규칙 grep 5종(삼항/`??`/`?.`/switch식/`hbk` 날짜주석)은 전부 0이었으나, 1회 자체 위반이 있었다:

**1. [자체 발견 - 가독성 규칙] MeasurementHistoryCsvLoader.cs 새 주석에 `hbk` 서명 포함**
- **Found during:** Task 1 자동검증(`datesig` grep)
- **Issue:** `COL_SELECTED_Z` 상수 위 주석에 실수로 `//260915 hbk` 날짜·이니셜 서명 형식을 그대로 썼다(CLAUDE.md 2026-06-11 정책 전환 이후 신규 금지)
- **Fix:** 주석에서 `260915 hbk` 를 제거하고 `// Phase 77 SZF-04: ...` 형식으로 재작성
- **Files modified:** WPF_Example/Custom/Sequence/Inspection/MeasurementHistoryCsvLoader.cs
- **Verification:** 재빌드 exit=0, `datesig=0` 재확인
- **Committed in:** `49f1a29e` (Task 1 커밋에 포함, 별도 커밋 없음 — 커밋 전에 수정 완료)

---

**Total deviations:** 1 자체 발견·즉시 수정 (신규 코드 스타일 위반, 커밋 전 수정 완료 — 별도 Rule 분류 대상 아님)
**Impact on plan:** 없음. 계획 그대로 실행, 커밋 파일 개수/삭제 줄 수 전부 acceptance_criteria 와 일치.

## Issues Encountered

None. Task 1/2/3 각각 편집 직후 Debug|x64 빌드를 실행해 총 4회 시도(1회는 hbk 수정 후 재빌드) 모두 에러 0으로 통과했다(Release|x64 빌드는 규칙에 따라 시도하지 않음 — OutputPath 가 실행 중인 D:\Data\DatumMeasurement.exe 를 덮어쓸 위험).

## New CSV Header (전문)

```
검사일시,RecipeName,IndexNumber,ShotName,FAIName,MeasurementName,TypeName,NominalValue,TolerancePlus,ToleranceMinus,MeasuredValue,Judgement,HasResult,OverallCycleResult,검사구분,선택Z
```

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- 77-04(수동/오프라인/재검사, 후보 사진 저장)가 이 plan 의 배선을 그대로 씀: `MeasurementBase.LastSelectedZIndex` 만 채우면 cycle.json/CSV/그리드가 자동으로 값을 반영한다. 추가 배선 불필요.
- **오버레이 `SelectedZLabel` 값 채우기는 이 plan 범위 밖 — 77-04(Action_FAIMeasurement, Action 소유)가 해야 한다.** 지금은 배선만 있고 값이 항상 null 이라 화면에 ' z5' 문구가 아직 뜨지 않는다. `overlay.SelectedZLabel = MeasurementBase.FormatSelectedZ(meas.LastSelectedZIndex)` 형태로 채우면 즉시 동작한다.
- 77-06 UAT 항목 후보: (a) 실제 현장 CSV(14/15컬럼 혼재)로 로더 파싱 확인(U-6), (b) 결과·리뷰어 그리드 "선택Z" 열이 실제 화면에서 잘리지 않고 값이 맞게 보이는지, (c) 중간 z 대기가 리뷰어 목록에서 정말 NG 로 안 보이는지 SIMUL TCP 사이클 로그·화면 대조.
- 77-05 누적 감사가 BASE(77-02 커밋 이후) 기준 10개 파일 전부 계획대로의 삭제 수(2/10 파일 1줄씩, 나머지 0)를 재확인할 예정 — 이번 plan 자체 검증에서는 이미 확인됨(위 Deviations 참조).

## Self-Check: PASSED

- 10개 소스 파일 전부 FOUND(디스크 확인)
- 커밋 3개(`49f1a29e`, `8fd236f9`, `ac74d1ce`) 전부 FOUND in `git log --oneline --all`

---
*Phase: 77-side-z-focus-select*
*Completed: 2026-09-15*
