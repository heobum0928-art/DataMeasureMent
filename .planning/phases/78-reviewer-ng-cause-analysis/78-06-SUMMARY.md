---
phase: 78-reviewer-ng-cause-analysis
plan: 06
subsystem: release
tags: [versiondefine, changelog, audit, regression, ng-cause-analysis]

requires:
  - phase: 78-reviewer-ng-cause-analysis (78-01, 78-02, 78-03, 78-04, 78-05)
    provides: "12개 파일에 걸친 NG 원인 추정 패널·R5~R9 규칙·진단 값 기록·실제 촬영 사진·NG 누적 엑셀 전체 구현"
provides:
  - "VersionDefine.cs [Version(Number=\"1.7.49.0\")] changelog 항목 (1.7.48.0 유지, 삭제 0) + VERSION/BUILD_DATE 갱신"
  - "Phase 78 전체 누적 감사 증거 — 변경 파일 범위 정확 일치, 하드룰 위반 0, 삽입 전용 파일 삭제 0, probe 전체 회귀 PASS"
affects: [78-07]

tech-stack:
  added: []
  patterns:
    - "changelog 항목은 실제 필드/서비스/클래스 이름(OriginImageFileName·DatumDiagnostics·ZCandidateScores·NG_분석_누적.xlsx)을 한국어 설명에 괄호로 병기 — 이전 1.7.47.0/1.7.48.0 항목과 동일 관례"
    - "누적 감사는 base-78-01(phase 시작 HEAD)부터 현재 HEAD까지 diff 범위로 계산 — plan 단위가 아니라 phase 단위 회귀 증거"

key-files:
  created: []
  modified:
    - WPF_Example/VersionDefine.cs

key-decisions: []

patterns-established: []

requirements-completed: [NGA-05]

coverage:
  - id: D1
    description: "VersionDefine.VERSION 이 1.7.49.0 이고, 1.7.48.0 항목 아래에 Phase 78 변경(NG 원인 추정 패널·NG 누적 엑셀·실제 촬영 사진·진단 값 기록·버튼 2개 삭제)을 쉬운 한국어로 적은 [Version] 항목이 하나 더 쌓인다(기존 항목 삭제 0)"
    requirement: "NGA-05"
    verification:
      - kind: other
        ref: "grep: number=1 version=1 prev_kept=1 order=1, content=6(다섯 키워드 이상), deleted=2(VERSION/BUILD_DATE 두 줄만), qmark=0 datesig=0, commit_files=WPF_Example/VersionDefine.cs 뿐, msbuild_exit=0"
        status: pass
    human_judgment: false
  - id: D2
    description: "phase 78 전체 누적 감사: 78-01 시작 커밋 이후 바뀐 코드 파일은 계획한 12개뿐이고, 새 .cs·csproj·MainView.xaml.cs·통계 CSV writer/loader·SkipReason.cs 변경 0, 추가 줄 하드룰 grep 0, 삽입 전용 파일 삭제 줄 0"
    requirement: "NGA-05"
    verification:
      - kind: other
        ref: "scope_match=1 newcs=0 forbidden=0, hardrule_violations=0(11개 .cs 전부 ternary/coalesce/nullcond/switchexpr/datesig/logic3=0), 삽입 전용 6개 파일(MeasurementBase/Action_FAIMeasurement/CycleResultSerializer/DatumConfig/InspectionSequence/ReviewMeasurementRow) deleted=0, 나머지 파일 삭제 상한 이내(CycleResultDto=0≤40, ReviewerWindow.xaml.cs=65≤81, ExcelExportService=116≤126, ChartImageCapture=46≤50, VersionDefine=2=2)"
        status: pass
    human_judgment: false
  - id: D3
    description: "누적 probe 회귀가 한 번에 통과한다 — 5개 날짜 rows 예외 0, 20260916 R6 36/36·C13_P1 함께 의심 R8, 20260915 C13_P3 R5, 옛 JSON R5·R8 0, synthetic 실패 0, json·image·xlsx 계열 PASS"
    requirement: "NGA-01~07 회귀"
    verification:
      - kind: other
        ref: "probe_exit=0, exceptions_nonzero=0 synthetic_fail=0 imgcase_fail=0 fails=0, d16_r6=36 n16=R6|R8 n15=R5 old_r8r5=0, json roundtrip=PASS, datummap fresh/stale/never/null/roundtrip=PASS, image date=20260916 row_origin=216 chosen_missing=0, xlsx 3회(added=36/added=0/added=140 filerows=176), xlsxequal rows=36 mismatch=0, xlsxlock status=FileLocked unchanged=1 templeft=0, xlsxempty created=0, xlsxpartial status=Added unreadable=1"
        status: pass
    human_judgment: false
  - id: D4
    description: "[edge NGA-05 concurrency] 검사가 계속 쓰는 중인 20260917 폴더를 포함한 probe 에서 예외 0 (쓰는 도중 cycle.json 은 loadnull 로만 집계)"
    requirement: "NGA-05"
    verification:
      - kind: other
        ref: "rows 20260917 줄: cycles=328 loadnull=0 rows=2952 ng=789 exceptions=0"
        status: pass
    human_judgment: false

duration: 약 13분
completed: 2026-09-17
status: complete
---

# Phase 78 Plan 06: 버전 1.7.49.0 기록 + Phase 78 전체 누적 감사 Summary

**VersionDefine 에 1.7.49.0 changelog 항목을 쌓고(코드 수정 없음), 78-01~05 전체 변경(12개 파일)을 한 번에 감사해 범위·하드룰·삭제 범위·probe 회귀 전부 실패 0으로 확인한 1커밋**

## Performance

- **Duration:** 약 13분
- **Started:** 2026-09-17 (78-05 마지막 커밋 직후)
- **Completed:** 2026-09-17
- **Tasks:** 2 (Task 1 auto — 코드 변경, Task 2 auto — 감사만, 코드 변경 없음)
- **Files modified:** 1 (WPF_Example/VersionDefine.cs)

## Base / 커밋 해시

- **base-78-01 (phase 78 시작 전 HEAD):** `90ba5ac2d02b4dce3e1640532e2c92c60c90f86f`
- **base-78-06 (이 plan 편집 전 HEAD):** `3977062b2a248a34a5979eb888b0f177b8a75a11`
- **Task 1 커밋:** `e6f0266e` — chore(78-06): 버전 1.7.49.0 — 리뷰어 NG 원인 분석
- **Task 2:** 코드 변경 없음(감사만, 이 SUMMARY 가 증거 기록)

## 빌드 결과

Debug|x64 (Release 금지, D:\Data 읽기만) — Task 1 커밋 전/후, Task 2 감사 시점 모두 `msbuild_exit=0`, errors 0.

## Task 1 — VersionDefine 1.7.49.0 verify 판정 (acceptance criteria 그대로)

| 지표 | 값 | 기준 |
|---|---|---|
| `msbuild_exit` | 0 | = 0 |
| `number` / `version` / `prev_kept` / `order` | 1 / 1 / 1 / 1 | 전부 1 |
| `content`(키워드 수: NG_분석_누적.xlsx·OriginImageFileName·DatumDiagnostics·ZCandidateScores·추정 원인) | 6 | ≥ 5 |
| `deleted`(VERSION·BUILD_DATE 두 줄만) | 2 | = 2 |
| `qmark` / `datesig` | 0 / 0 | 전부 0 |
| `commit_files` | `WPF_Example/VersionDefine.cs` | 동일 |

첫 번째 초안에서 `content=3`(DatumDiagnostics/ZCandidateScores/OriginImageFileName 세 식별자를 한국어 설명으로만 풀어 쓰고 원문 이름을 병기하지 않아 grep 미달)이었다. 이전 항목(1.7.47.0 의 ZIndexEnd, 1.7.46.0 의 IsVerticalLineDisabled)과 같은 관례로 세 이름을 괄호로 병기해 `content=6` 으로 맞추고 재검증했다(코드 수정 아님 — changelog 문구만 보강, 별도 커밋 분리 없음, 위반 문구는 커밋 이력에 남지 않음).

## Task 2 — Phase 78 전체 누적 감사 verify 판정 (acceptance criteria 그대로)

### 변경 범위

| 지표 | 값 | 기준 |
|---|---|---|
| `scope_match` | 1 | = 1 (12개 파일 정확 일치) |
| `newcs` | 0 | = 0 |
| `forbidden`(csproj·MainView.xaml.cs·CSV writer/loader·SkipReason.cs·SystemHandler.cs) | 0 | = 0 |

`got` (base-78-01 → HEAD, `git diff --name-only -- WPF_Example` 정렬):
```
WPF_Example/Custom/Export/ChartImageCapture.cs
WPF_Example/Custom/Export/ExcelExportService.cs
WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs
WPF_Example/Custom/Sequence/Inspection/CycleResultSerializer.cs
WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs
WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs
WPF_Example/Custom/Sequence/Inspection/MeasurementBase.cs
WPF_Example/UI/Reviewer/ReviewerWindow.xaml
WPF_Example/UI/Reviewer/ReviewerWindow.xaml.cs
WPF_Example/UI/ViewModel/CycleResultDto.cs
WPF_Example/UI/ViewModel/ReviewMeasurementRow.cs
WPF_Example/VersionDefine.cs
```
12개, 계획 원장의 EXPECT 목록과 정확히 일치.

### 하드룰 + 파일별 삭제 수 (11개 .cs, base-78-01 → HEAD 누적)

| 파일 | deleted | 상한 | ternary/coalesce/nullcond/switchexpr/datesig/logic3 |
|---|---|---|---|
| ChartImageCapture.cs | 46 | ≤50 | 전부 0 |
| ExcelExportService.cs | 116 | ≤126 | 전부 0 |
| Action_FAIMeasurement.cs | 0 | =0(삽입 전용) | 전부 0 |
| CycleResultSerializer.cs | 0 | =0(삽입 전용) | 전부 0 |
| DatumConfig.cs | 0 | =0(삽입 전용) | 전부 0 |
| InspectionSequence.cs | 0 | =0(삽입 전용) | 전부 0 |
| MeasurementBase.cs | 0 | =0(삽입 전용) | 전부 0 |
| ReviewerWindow.xaml.cs | 65 | ≤81 | 전부 0 |
| CycleResultDto.cs | 0 | ≤40 | 전부 0 |
| ReviewMeasurementRow.cs | 0 | =0(삽입 전용) | 전부 0 |
| VersionDefine.cs | 2 | =2 | 전부 0 |

`hardrule_violations=0` (11개 파일 전부 0으로 위반 0건).

### 빌드 + probe 전체 재실행

`msbuild_exit=0`, `probe_exit=0`.

rows (5개 날짜, base-78-01 이후 누적 코드 기준):
```
date=20260601 cycles=8 loadnull=0 rows=322 ng=140 codes=R0:136,R1:0,R2:0,R3:0,R4:0,R5:0,R6:0,R7:4,R8:0,R9:0,RX:0 suspects=R5:0,R6:0,R7:0,R8:0,R9:0 newfields=0 exceptions=0
date=20260811 cycles=411 loadnull=0 rows=10275 ng=338 codes=R0:0,R1:0,R2:338,R3:0,R4:0,R5:0,R6:0,R7:0,R8:0,R9:0,RX:0 suspects=R5:0,R6:0,R7:0,R8:0,R9:0 newfields=0 exceptions=0
date=20260915 cycles=1112 loadnull=0 rows=10008 ng=1992 codes=R0:32,R1:0,R2:159,R3:33,R4:0,R5:2,R6:1765,R7:0,R8:1,R9:0,RX:0 suspects=R5:32,R6:0,R7:0,R8:475,R9:0 newfields=0 exceptions=0
date=20260916 cycles=24 loadnull=0 rows=216 ng=36 codes=R0:0,R1:0,R2:0,R3:0,R4:0,R5:0,R6:36,R7:0,R8:0,R9:0,RX:0 suspects=R5:3,R6:0,R7:0,R8:22,R9:0 newfields=0 exceptions=0
date=20260917 cycles=328 loadnull=0 rows=2952 ng=789 codes=R0:0,R1:0,R2:0,R3:5,R4:0,R5:0,R6:784,R7:0,R8:0,R9:0,RX:0 suspects=R5:30,R6:0,R7:0,R8:52,R9:0 newfields=0 exceptions=0
```
(20260917 은 검사가 계속 쓰는 중인 오늘 날짜 폴더 — edge NGA-05 concurrency, `exceptions=0` 확인, `loadnull=0` 은 이번 실행 시점에 쓰는 중인 cycle.json 이 없었다는 뜻이며 동시성 자체는 rows 모드가 로드 실패를 loadnull 로만 세고 계속 진행하는 78-02 구현으로 이미 보장된다.)

synthetic 44사례 전부 PASS (`synthetic_fail=0`) — 13(78-01) + 20(78-02 R9/R8/R7/R6) + 11(78-02 R5) 누적.

json/datummap:
```
json roundtrip=PASS
json old zstart=-1 zend=-1 cand=0 datum=0
json meas_field=1 meas_prop=0 meas_jsonignore=1
datummap fresh=PASS
datummap stale=PASS
datummap never=PASS
datummap null=PASS
datummap roundtrip=PASS
json datum_fields=4 datum_jsonignore=4
```

image (합성 9사례 + 실데이터 2개 날짜) 전부 PASS, `imgcase_fail=0`:
```
image date=20260601 cycles=8 cycle_origin=0 cycle_fallback=0 cycle_none=8 rows=322 row_origin=0 row_fallback=0 row_none=322 chosen_missing=0
image date=20260916 cycles=24 cycle_origin=8 cycle_fallback=16 cycle_none=0 rows=216 row_origin=216 row_fallback=0 row_none=0 chosen_missing=0
```

xlsx 계열(신규→재실행→비교→다른날짜 이어붙이기→잠금→빈폴더4종→손상 파일 섞임):
```
xlsx status=Added added=36 dup=0 unreadable=0 filerows=36 uniquekeys=36
xlsx status=NothingNew added=0 dup=36 unreadable=0 filerows=36 uniquekeys=36
xlsxequal rows=36 mismatch=0 unmatched=0
xlsx status=Added added=140 dup=0 unreadable=0 filerows=176 uniquekeys=176
xlsxlock status=FileLocked unchanged=1 templeft=0
xlsxempty nofolder=PASS missing=PASS nocycles=PASS nong=PASS created=0
xlsxpartial status=Added added=6 unreadable=1
```

### 판정 숫자 요약 (acceptance criteria 그대로)

| 지표 | 값 | 기준 |
|---|---|---|
| `exceptions_nonzero` | 0 | = 0 |
| `synthetic_fail` | 0 | = 0 |
| `imgcase_fail` | 0 | = 0 |
| `fails`(문자열 `FAIL` 검색) | 0 | = 0 |
| `d16_r6`(20260916 IsNg=1 중 CauseCode=R6) | 36 | = 36 |
| `n16`(164530363_cycle C13_P1 CauseCode\|SuspectCodes) | `R6\|R8` | = `R6\|R8` |
| `n15`(135946717_cycle C13_P3 CauseCode) | `R5` | = `R5` |
| `old_r8r5`(20260601+20260811 IsNg=1 중 R8·R5 발동) | 0 | = 0 |

모든 지표가 계획이 명시한 값과 정확히 일치했다 — 실패 0건, 수정 없이 통과.

## Files Created/Modified

- `WPF_Example/VersionDefine.cs` — `[Version(Number = "1.7.49.0", ...)]` changelog 항목 추가(기존 항목 삭제 0), `VERSION`/`BUILD_DATE` 를 `"1.7.49.0"`/`"2026-09-17"` 로 갱신

## Task Commits

1. **Task 1: 버전 1.7.49.0 기록** - `e6f0266e` (chore)
2. **Task 2: Phase 78 누적 회귀 감사** - 코드 변경 없음(감사만, 위 표가 증거)

**Plan metadata:** (이 커밋) - `docs(78-06): complete 버전 1.7.49.0 + 누적 감사 plan`

## Decisions Made

없음 — Task 1 changelog 키워드 병기는 계획이 명시한 문체 관례(이전 버전 항목이 실제 필드명을 괄호로 병기)를 그대로 따른 것이고, Task 2 는 계획이 지정한 verify 스크립트를 그대로 실행했다.

## Deviations from Plan

None - 계획대로 실행. Task 1 초안에서 `content=3`(키워드 5개 미만)이 나온 것은 코드 결함이 아니라 changelog 문구 보강이 필요했던 것으로, 커밋 전에 발견·수정해 커밋 이력에 미달 문구가 남지 않았다(위 "Task 1 verify 판정" 절에 기록).

## Issues Encountered

None — Task 2 감사는 계획이 지정한 verify 스크립트를 수정 없이 그대로 실행해 첫 실행에서 전부 통과했다. 실패가 있었다면 고치지 않고 항목·원인 후보를 이 SUMMARY 에 기록하고 멈췄을 것이나, 그런 경우는 없었다.

## User Setup Required

None - 외부 서비스 설정 불필요.

## Next Phase Readiness

- Phase 78 코드 구현(78-01~06)이 버전 1.7.49.0 으로 기록되고 전체 누적 감사(변경 범위·하드룰·삭제 범위·probe 회귀)를 한 번에 통과했다.
- 배포(Release 빌드, D:\Data 덮어쓰기)는 이 plan 범위 밖 — 사용자 승인 후 별도 진행.
- 78-07(UAT)이 실제 화면에서 확인해야 할 항목: A-78-E1(패널 가독성), U-1~U-7(리뷰어 패널·사진·NG 누적 엑셀·동시성 backstop 등) — 78-01~05 SUMMARY 에 이미 예고됨.
- 블로커 없음.

---
*Phase: 78-reviewer-ng-cause-analysis*
*Completed: 2026-09-17*

## Self-Check: PASSED

- FOUND: `.planning/phases/78-reviewer-ng-cause-analysis/78-06-SUMMARY.md`
- FOUND commit: `e6f0266e` (Task 1)
- FOUND commit: `ee455ef4` (plan metadata)
- FOUND: `WPF_Example/VersionDefine.cs` (VERSION/changelog 1.7.49.0 확인)
