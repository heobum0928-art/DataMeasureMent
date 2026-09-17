---
phase: 78-reviewer-ng-cause-analysis
plan: 01
subsystem: ui
tags: [wpf, halcon, reviewer, cycle-json, ng-cause-analysis, rule-engine]

requires: []
provides:
  - "NgCauseResult / NgHistorySample / NgCauseHistory / NgCauseAnalyzer (CycleResultDto.cs) — R6(치우침)·R1~R4(사유)·R0(미분류 이탈)·RX(모르는 사유) 순수 규칙 엔진"
  - "ReviewMeasurementRow 5인자 생성자(Cause/CausePanelText) — cycle+history 를 받아 원인 판정까지 채움"
  - "ReviewerWindow.xaml 측정표 아래 NG 원인 패널(txt_ngCausePanel, SelectedItem.CausePanelText 바인딩)"
  - "ShotResultDto.ZRangeStartIndex/ZRangeEndIndex, MeasurementResultDto.ZCandidateScores, CycleResultDto.DatumDiagnostics — NGA-07 DTO 계약(78-02/03 이 소비)"
affects: [78-02, 78-03, 78-04, 78-05, 78-06, 78-07]

tech-stack:
  added: []
  patterns:
    - "규칙 엔진은 CycleResultDto/NgCauseHistory 만 입력받는 순수 정적 클래스(파일 I/O·전역 싱글턴·레시피 참조 0) — ReviewerListLabelBuilder 와 동일 원칙"
    - "리뷰어가 이미 로드한 날짜 폴더 cycle.json 전체를 NgCauseHistory 로 1회 구성해 클릭마다 재스캔하지 않음(Pitfall 3 회피)"

key-files:
  created: []
  modified:
    - WPF_Example/UI/ViewModel/CycleResultDto.cs
    - WPF_Example/UI/ViewModel/ReviewMeasurementRow.cs
    - WPF_Example/UI/Reviewer/ReviewerWindow.xaml
    - WPF_Example/UI/Reviewer/ReviewerWindow.xaml.cs

key-decisions:
  - "R6 창=최근 5개(3개 미만이면 뒤로 채움), 전부 공칭(NominalValue) 대비 엄격히 한쪽 + 평균이 허용 범위 밖(엄격)일 때만 발동"
  - "CROSS_Z_INCOMPLETE 는 NG 집계에서 빠지지만(IsNg=false) 원인 패널에는 R4 설명이 보인다(HasCause=true) — Analyze 에서 IsNgMeasurement 체크보다 먼저 분기"
  - "NGA-07 DTO 필드는 이 plan 에서 선언만 하고 채우는 코드는 78-03, 읽는 규칙(R5/R8)은 78-02 — interface-first"

patterns-established:
  - "패널 문구는 반드시 '추정 원인 / 근거 / 확인할 일' 3줄 접두사로 시작 — 추정을 사실처럼 보여주지 않는다(PR-1)"

requirements-completed: [NGA-01, NGA-02, NGA-05, NGA-07]

coverage:
  - id: D1
    description: "R6(치우침) 규칙이 cycle.json→날짜 폴더 이력→규칙 엔진→행 VM→리뷰어 패널까지 관통하고, 20260916 실데이터 NG 36행 전부 R6 으로 판정된다"
    requirement: "NGA-01"
    verification:
      - kind: other
        ref: "NgCauseProbe.exe <binDir> rows <outDir> D:/Data/Result/20260916 (repo 밖 probe, 실데이터 1회성 검증 — 회귀 테스트로 반복 실행되지 않음)"
        status: pass
    human_judgment: true
    rationale: "probe 는 저장소 밖 스크래치 코드로 커밋되지 않아 CI 에서 재실행되지 않는다 — 화면에 실제로 보이는지는 78-07 UAT U-1 에서 사람이 확인한다"
  - id: D2
    description: "NG 행 선택 시 원인/근거/확인할 일 3줄이 패널에 바인딩으로 표시되고, code-behind 추가 줄에는 분기·반복문이 없다(배선만)"
    requirement: "NGA-02"
    verification:
      - kind: other
        ref: "grep -cE '\\b(if|switch|for|foreach|while)\\b' ReviewerWindow.xaml.cs 추가 줄 == 0"
        status: pass
    human_judgment: true
    rationale: "패널 가독성(비전 초보 기준)은 사람 판단 — A-78-E1, 78-07 UAT U-1"
  - id: D3
    description: "옛 cycle.json(20260601/20260811/20260916, 신규 필드 없음)이 예외 없이 로드되고 새 DTO 필드는 기본값(ZRangeStartIndex/EndIndex=-1, ZCandidateScores/DatumDiagnostics 빈 목록)으로 채워진다"
    requirement: "NGA-05"
    verification:
      - kind: other
        ref: "NgCauseProbe.exe rows 실행 — exceptions=0, loadnull=0, newfields=0 (3개 날짜 전부)"
        status: pass
    human_judgment: false
  - id: D4
    description: "NGA-07 DTO 계약(ZCandidateScoreDto/DatumDiagnosticDto/ZRangeStartIndex/ZRangeEndIndex/ZCandidateScores/DatumDiagnostics) 선언 완료, 78-02/03 이 그대로 쓸 수 있다"
    requirement: "NGA-07"
    verification:
      - kind: other
        ref: "Debug|x64 빌드 PASS + grep dto=6 datumprops=15 required=0"
        status: pass
    human_judgment: false

duration: 25min
completed: 2026-09-17
status: complete
---

# Phase 78 Plan 01: NG 원인 분석 tracer + 사유 규칙 + DTO 계약 Summary

**R6(보정값/티칭 치우침) 규칙을 cycle.json→날짜 폴더 이력→NgCauseAnalyzer→ReviewMeasurementRow→리뷰어 패널까지 끝까지 연결한 tracer, R1~R4 사유 규칙, NGA-07 DTO 계약 3커밋**

## Performance

- **Duration:** 약 25분
- **Started:** 2026-09-17T08:44:00Z (추정 — PLAN_START_TIME 미기록, 편집 전 빌드 로그 시각 기준)
- **Completed:** 2026-09-17T08:57:30Z
- **Tasks:** 3 (Task 1 tracer, Task 2 auto+tdd, Task 3 auto)
- **Files modified:** 4 (CycleResultDto.cs, ReviewMeasurementRow.cs, ReviewerWindow.xaml, ReviewerWindow.xaml.cs)

## Base / 커밋 해시

- **base-78-01 (편집 전 HEAD):** `90ba5ac2d02b4dce3e1640532e2c92c60c90f86f`
- **Task 1 커밋:** `a2b1d62b` — feat(78-01): NG 원인 tracer — R6 치우침 규칙 cycle.json→리뷰어 패널
- **Task 2 커밋:** `cd5b05c0` — feat(78-01): NG 원인 사유 규칙 R1~R4
- **Task 3 커밋:** `2543b9f1` — feat(78-01): NGA-07 cycle.json 진단 필드 계약

## 빌드 결과

Debug|x64 (Release 금지, D:\Data 읽기만) — 3개 커밋 시점 모두:

| 시점 | msbuild_exit | errors |
|---|---|---|
| 편집 전(baseline) | 0 | 0 |
| Task 1 후 | 0 | 0 |
| Task 2 후 | 0 | 0 |
| Task 3 후 | 0 | 0 |

## 파일별 `git diff -w` 삭제 수 (base → HEAD, 전체 plan 누적)

| 파일 | added | deleted |
|---|---|---|
| WPF_Example/UI/ViewModel/CycleResultDto.cs | 591 | 0 |
| WPF_Example/UI/ViewModel/ReviewMeasurementRow.cs | 13 | 0 |
| WPF_Example/UI/Reviewer/ReviewerWindow.xaml | 16 | 2 |
| WPF_Example/UI/Reviewer/ReviewerWindow.xaml.cs | 8 | 1 |

ReviewerWindow.xaml 의 삭제 2줄은 DataGrid 의 `Grid.Row="1" Grid.Column="4"` 속성을 `Grid.Row="0"` 으로 바꾸며 발생한 것(플랜에서 예고된 유일한 기존 줄 수정). ReviewerWindow.xaml.cs 의 삭제 1줄은 `rows.Add(new ReviewMeasurementRow(shot, fai, m));` → 5인자 생성자 호출로 바뀐 것. 그 외 모든 변경은 기존 줄 사이 삽입이다.

## Probe rows 출력 원문 (날짜별)

Task 1 (20260601/20260811/20260916, 사유 규칙 전 — 사유 있는 행은 전부 RX):
```
date=20260601 cycles=8 loadnull=0 rows=322 ng=140 codes=R0:140,R1:0,R2:0,R3:0,R4:0,R5:0,R6:0,R7:0,R8:0,R9:0,RX:0 suspects=R5:0,R6:0,R7:0,R8:0,R9:0 newfields=0 exceptions=0
date=20260811 cycles=411 loadnull=0 rows=10275 ng=338 codes=R0:0,R1:0,R2:0,R3:0,R4:0,R5:0,R6:0,R7:0,R8:0,R9:0,RX:338 suspects=R5:0,R6:0,R7:0,R8:0,R9:0 newfields=0 exceptions=0
date=20260916 cycles=24 loadnull=0 rows=216 ng=36 codes=R0:0,R1:0,R2:0,R3:0,R4:0,R5:0,R6:36,R7:0,R8:0,R9:0,RX:0 suspects=R5:0,R6:0,R7:0,R8:0,R9:0 newfields=0 exceptions=0
```

named row (20260916/164530363_cycle, C13_P1):
```
20260916/164530363_cycle	SIDE_SHOT_1_C13-14	FAI_C13-14	C13_P1	NG	1	R6	-	추정 원인: 보정값 또는 티칭이 한쪽으로 치우쳐 있습니다 / 근거: 최근 4회 평균 2.2920 · 기준 2.0500 (허용 2.0200 ~ 2.0800) · 4회 모두 큰 쪽 / 확인할 일: Shot 보정값과 이 측정의 티칭 위치를 확인하세요
```

Task 2 (사유 규칙 R1~R4 추가 후, 20260811/20260915):
```
date=20260811 cycles=411 loadnull=0 rows=10275 ng=338 codes=R0:0,R1:0,R2:338,R3:0,R4:0,R5:0,R6:0,R7:0,R8:0,R9:0,RX:0 suspects=R5:0,R6:0,R7:0,R8:0,R9:0 newfields=0 exceptions=0
date=20260915 cycles=1112 loadnull=0 rows=10008 ng=1992 codes=R0:35,R1:0,R2:159,R3:33,R4:0,R5:0,R6:1765,R7:0,R8:0,R9:0,RX:0 suspects=R5:0,R6:0,R7:0,R8:0,R9:0 newfields=0 exceptions=0
```
(20260915 의 R6:1765/R0:35 는 이미 Task 1 이 만든 값-이탈 규칙이 계속 평가된 결과 — Task 2 범위는 R2:159/R3:33/RX:0 확인.)

Task 3 (NGA-07 DTO 필드 선언 후, 20260601/20260811/20260916 — 아직 기록 코드가 없어 newfields=0 이어야 함):
```
date=20260601 cycles=8 loadnull=0 rows=322 ng=140 codes=R0:140,R1:0,R2:0,R3:0,R4:0,R5:0,R6:0,R7:0,R8:0,R9:0,RX:0 suspects=R5:0,R6:0,R7:0,R8:0,R9:0 newfields=0 exceptions=0
date=20260811 cycles=411 loadnull=0 rows=10275 ng=338 codes=R0:0,R1:0,R2:338,R3:0,R4:0,R5:0,R6:0,R7:0,R8:0,R9:0,RX:0 suspects=R5:0,R6:0,R7:0,R8:0,R9:0 newfields=0 exceptions=0
date=20260916 cycles=24 loadnull=0 rows=216 ng=36 codes=R0:0,R1:0,R2:0,R3:0,R4:0,R5:0,R6:36,R7:0,R8:0,R9:0,RX:0 suspects=R5:0,R6:0,R7:0,R8:0,R9:0 newfields=0 exceptions=0
```

## Probe synthetic 출력 원문 (Task 2, 13사례)

```
case=r1_no_image expect=True/True/R1 got=True/True/R1 PASS
case=r2_datum_fail expect=True/True/R2 got=True/True/R2 PASS
case=r2_align_fail expect=True/True/R2 got=True/True/R2 PASS
case=r2_ref_missing expect=True/True/R2 got=True/True/R2 PASS
case=r3_measure_fail expect=True/True/R3 got=True/True/R3 PASS
case=r3_long_error expect=True/True/R3 got=True/True/R3 PASS
case=r4_zindex expect=True/True/R4 got=True/True/R4 PASS
case=r4_cross_z_not_ng expect=False/True/R4 got=False/True/R4 PASS
case=rx_unknown expect=True/True/RX got=True/True/RX PASS
case=pending_not_ng expect=False/False/ got=False/False/ PASS
case=ok_not_ng expect=False/False/ got=False/False/ PASS
case=null_measurement expect=False/False/ got=False/False/ PASS
case=null_cycle expect=False/False/ got=False/False/ PASS
synthetic_fail=0
```

## 새 심볼 목록 (Artifacts 표 78-01 행과 대조)

| 심볼 | 파일 | Artifacts 표 대조 |
|---|---|---|
| `NgCauseResult`(IsNg/HasCause/CauseCode/CauseText/EvidenceText/ActionText/SuspectCodes/SuspectText, `Empty()`, `NotNg()`) | CycleResultDto.cs | 일치 |
| `NgHistorySample`(InspectionTime/CycleKey/Value/IsOk) | CycleResultDto.cs | 일치 |
| `NgCauseHistory`(`AddCycle`/`ResolveCycleKey`/`SampleCount`/`GetSamples`/`KEY_SEPARATOR`/`CYCLE_KEY_TIME_PREFIX`) | CycleResultDto.cs | 일치 |
| `NgCauseAnalyzer`(`IsNgMeasurement`/`Analyze`/`BuildPanelText`/CODE_R0~R4·R6·RX·NONE, R6_WINDOW_SIZE=5/R6_MIN_SAMPLES=3, R0/R1~R4/R6/RX 문구 const, `NOT_NG_TEXT`, 패널 접두 const) | CycleResultDto.cs | 계획된 R5/R7/R8/R9 및 관련 const(R9_BOUNDARY_RATIO 등)는 78-02 범위 — 이 plan 범위(R0/R1~R4/R6/RX)는 전부 구현 |
| `ZCandidateScoreDto`(ZIndex/Ok/Score), `DatumDiagnosticDto`(15개 필드), `ShotResultDto.Z_RANGE_NONE`/`ZRangeStartIndex`/`ZRangeEndIndex`, `MeasurementResultDto.ZCandidateScores`, `CycleResultDto.DatumDiagnostics` | CycleResultDto.cs | 일치 |
| `ReviewMeasurementRow(shot, fai, m, cycle, history)` 생성자, `Cause`, `CausePanelText` | ReviewMeasurementRow.cs | 일치 |
| XAML `border_ngCause`, `txt_ngCauseHeader`("NG 원인 분석 (추정)"), `txt_ngCausePanel` | ReviewerWindow.xaml | 일치 |
| `_ngCauseHistory` 필드 | ReviewerWindow.xaml.cs | 일치 |

## 계획 단계 결정 요약 (P-1~P-17)

- **P-1 NG 범위:** `IsNgMeasurement` — 측정 null→false, Z_RANGE_PENDING·CROSS_Z_INCOMPLETE→false, 그 밖 사유 있으면 true, 사유 없으면 LastHasResult && !LastJudgement 일 때만 true.
- **P-2 규칙 입력:** `Analyze(cycle, shot, fai, m, history)` 순수 함수 — 파일 I/O·전역 싱글턴·레시피 조회 없음(probe `pure=0` 로 확인).
- **P-3 이력 키·정렬:** 측정 키=Shot+U+001F+FAI+U+001F+측정명, 사이클 키=CycleFolderPath(끝 구분자 제거) 또는 `T`+Ticks. 정렬=시각 오름차순→사이클 키 ordinal. 같은 사이클 키 재추가는 무시.
- **P-4 대표 원인 순서:** 이 plan 은 R6/R1~R4/R0/RX 만 구현(단독 규칙들). R5/R9/R8/R7 발동 목록·대표/함께 의심 조립은 78-02 범위.
- **P-5 R6:** 창=최근 5개(3개 미만이면 뒤로 채움), 3개 이상+전부 공칭 대비 엄격히 한쪽+평균이 허용 범위 밖(엄격) — 20260916 NG 36/36 전부 R6 실데이터로 확인.
- **P-6~P-9 (R9/R5/R8/R7):** 78-02 범위 — 이 plan 은 손대지 않음.
- **P-10 문구·숫자:** const 문자열 + `string.Format`, 접두 `추정 원인: `/`근거: `/`확인할 일: `/`함께 의심: `, 값 F4. 판정은 double 원값 비교(반올림 없음).
- **P-11 화면:** 오른쪽 열(Grid.Column 4) 안에 Grid, DataGrid 아래 패널. `txt_ngCausePanel.Text` 바인딩만, code-behind 코드 없음(`branch_kw=0` 확인).
- **P-12~P-14 (엑셀/사진):** 78-05/78-04 범위 — 이 plan 밖.
- **P-15 진단 기록:** `DatumDiagnostics`/`ZCandidateScores`/`ZRangeStartIndex`/`ZRangeEndIndex` 필드 선언 완료(78-01 Task 3). 값을 채우는 코드는 78-03.
- **P-16 점수 노출:** 이 plan 은 값 기록 안 함(선언만) — 표시 정책은 78-02/03 에서 적용.
- **P-17 삭제 범위:** 78-04 범위 — 이 plan 밖.

## Flagged Assumptions (그대로 옮겨 적음 — 자동 해소 금지)

- **A-78-E1 (NGA-02 unclassified):** 원인 3줄(+함께 의심)이 폭 430px 오른쪽 열에서 비전 초보에게 읽히는지는 사람 판단이다. 78-07 UAT U-1 에서 사용자가 판단하고, 문구 수정은 const 만 바꾸면 된다.
- **A-78-A1:** R5/R6/R9 임계값(0.15·2.0 / 5·3 / 0.10)은 20260915~17 SIDE_1 표본으로만 맞췄다. TOP/BOTTOM·다른 제품에서 과다/과소 판정 여부는 UAT 와 운영 중 확인한다.
- **A-78-A2:** R5 는 "같은 FAI = 같은 기준선" 으로 묶는다(cycle.json 측정에 DatumRef 가 없다). FAI 하나에 기준선이 여럿인 레시피는 묶음 조건(같은 부호·비슷한 크기)에서 대부분 걸러지지만 확인 필요.
- **A-78-A3 (NGA-06 concurrency):** 원본 사진은 검사 직후 워커가 비동기로 쓴다. 쓰는 도중(크기 > 0, 미완성)인 파일을 리뷰어가 열면 기존 전역 예외 창이 뜰 수 있다 — 빈도 낮음, 다시 누르면 정상. 78-07 U-2 에서 확인.

## Task Commits

1. **Task 1 (tracer): R6 치우침 1개 규칙** - `a2b1d62b` (feat)
2. **Task 2: 사유 규칙 R1~R4** - `cd5b05c0` (feat, tdd — synthetic 13 사례로 RED 대신 실데이터/합성 사례 assertion)
3. **Task 3: NGA-07 DTO 계약** - `2543b9f1` (feat)

**Plan metadata:** (이 커밋) - `docs(78-01): complete NG 원인 tracer plan`

## Files Created/Modified

- `WPF_Example/UI/ViewModel/CycleResultDto.cs` - NgCauseResult/NgHistorySample/NgCauseHistory/NgCauseAnalyzer(R0/R1~R4/R6/RX) + NGA-07 DTO(ZCandidateScoreDto/DatumDiagnosticDto/ZRangeStartIndex/ZRangeEndIndex/ZCandidateScores/DatumDiagnostics)
- `WPF_Example/UI/ViewModel/ReviewMeasurementRow.cs` - Cause/CausePanelText 프로퍼티 + 5인자 생성자
- `WPF_Example/UI/Reviewer/ReviewerWindow.xaml` - 측정표 아래 NG 원인 패널(border_ngCause)
- `WPF_Example/UI/Reviewer/ReviewerWindow.xaml.cs` - _ngCauseHistory 필드, LoadCycleFolders/DisplayCycle 배선(분기 없음)

## Decisions Made

- R6 판정의 "공칭 중심"은 `(lower+upper)/2` 가 아니라 `m.NominalValue` 를 직접 사용한다 — 공차가 비대칭(TolerancePlus≠ToleranceMinus)이면 두 값이 다르기 때문(계획 단계 미명시, 구현 중 발견해 정정 — Rule 1 성격의 자기 발견·수정, 커밋 전 반영되어 별도 deviation 으로 분리 기록하지 않음).
- CROSS_Z_INCOMPLETE 분기는 `Analyze` 최상단(cycle/m null 체크 다음, `IsNgMeasurement` 체크 이전)에 둔다 — `IsNgMeasurement` 가 이미 이 사유를 false 로 취급하므로, 순서를 지키지 않으면 NotNg() 로 빠져 R4 설명이 보이지 않는다.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] 삼항 연산자(`shot != null ? shot.ShotName : null`) 하드룰 위반 자체 수정**
- **Found during:** Task 1 acceptance-criteria grep 검증(`ternary=2`)
- **Issue:** `BuildOutOfToleranceResult` 안에서 이력 키 조립 시 삼항 연산자를 사용해 CLAUDE.md 가독성 하드룰(삼항 금지)을 위반했다.
- **Fix:** `szShotName`/`szFaiName` 지역 변수를 명시적 `if/else` 로 대입하도록 변경.
- **Files modified:** WPF_Example/UI/ViewModel/CycleResultDto.cs
- **Verification:** 재빌드 PASS, `ternary=0 coalesce=0 nullcond=0 switchexpr=0 datesig=0 logic3=0` 확인 후 커밋.
- **Committed in:** a2b1d62b (Task 1 커밋, 커밋 전에 수정 완료 — 위반 코드는 git 이력에 남지 않음)

---

**Total deviations:** 1 auto-fixed (Rule 1 — 하드룰 위반 자체 발견·수정)
**Impact on plan:** 계획 범위·설계를 바꾸지 않음. 커밋 전에 발견해 커밋 이력에 위반 코드가 남지 않았다.

## Issues Encountered

None — probe(NgCauseProbe.cs, 저장소 밖 `C:/Users/admin/AppData/Local/Temp/p78-probe/`)는 각 Task 마다 계획대로 확장(rows→+synthetic→newfields 계산 갱신)되었고 전부 1회 컴파일로 통과했다.

## User Setup Required

None - 외부 서비스 설정 불필요.

## Next Phase Readiness

- 78-02(R5/R7/R8/R9 + 함께 의심 조립)가 쓸 `NgCauseAnalyzer` 골격(R0/R1~R4/R6/RX)과 `NgCauseHistory` 가 준비됨.
- 78-03(진단 값 기록)이 채울 `ZCandidateScoreDto`/`DatumDiagnosticDto`/`ZRangeStartIndex`/`ZRangeEndIndex`/`ZCandidateScores`/`DatumDiagnostics` 필드가 기본값과 함께 선언됨 — 옛 cycle.json 호환 확인됨(newfields=0, exceptions=0).
- 화면에 패널이 실제로 보이는지, 문구가 비전 초보에게 읽히는지는 78-07 UAT U-1 대기(A-78-E1).
- 블로커 없음.

---
*Phase: 78-reviewer-ng-cause-analysis*
*Completed: 2026-09-17*
