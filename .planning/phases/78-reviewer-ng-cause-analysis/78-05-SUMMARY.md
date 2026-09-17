---
phase: 78-reviewer-ng-cause-analysis
plan: 05
subsystem: ui
tags: [wpf, halcon, reviewer, closedxml, excel-export, ng-cause-analysis]

requires:
  - phase: 78-reviewer-ng-cause-analysis (78-01, 78-02)
    provides: "NgCauseHistory/NgCauseAnalyzer.IsNgMeasurement/Analyze — 화면과 같은 원인 판정 엔진"
  - phase: 78-reviewer-ng-cause-analysis (78-04)
    provides: "리뷰어 왼쪽 버튼 스택 순서(날짜 폴더 열기 바로 아래에 새 버튼 삽입), ExcelExportService.BuildJudgementText 공용 헬퍼 유지"
provides:
  - "NgAccumulationExportService.AppendDateFolder — 날짜 폴더의 NG 만 NG_분석_누적.xlsx 'NG 누적' 시트 20열에 중복 없이 누적(D-78-02)"
  - "리뷰어 'NG 누적 엑셀 저장' 버튼 — 마지막으로 연 날짜 폴더를 서비스에 넘기고 안내 메시지만 표시"
affects: [78-06, 78-07]

tech-stack:
  added: []
  patterns:
    - "기존 xlsx 열기(경로 있으면 new XLWorkbook(path))/새로 만들기(new XLWorkbook()) 분기 + 같은 폴더 임시 xlsx SaveAs → File.Replace/Move 원자적 쓰기 — 이 프로젝트에서 처음으로 '기존 파일 열어서 계속 쓰기' 패턴"
    - "중복 판정은 기존 행 키를 먼저 전부 HashSet 에 읽어 오고, 쓰는 동안 같은 HashSet 에 계속 추가 — 같은 실행 안 중복과 파일에 이미 있는 중복을 한 자료구조로 처리"

key-files:
  created: []
  modified:
    - WPF_Example/Custom/Export/ExcelExportService.cs
    - WPF_Example/UI/Reviewer/ReviewerWindow.xaml
    - WPF_Example/UI/Reviewer/ReviewerWindow.xaml.cs

key-decisions: []

patterns-established: []

requirements-completed: [NGA-03, NGA-04]

coverage:
  - id: D1
    description: "날짜 폴더를 연 뒤 'NG 누적 엑셀 저장' 을 누르면 그 날짜 폴더 NG 만 NG_분석_누적.xlsx 'NG 누적' 시트에 20열(규격표 그대로)로 추가되고, 엑셀의 추정 원인·근거·확인할 일·함께 의심·원인 코드는 같은 날짜 폴더를 연 리뷰어 화면 패널과 글자까지 같다"
    requirement: "NGA-03"
    verification:
      - kind: other
        ref: "NgCauseProbe.exe <binDir> xlsx/xlsxequal (repo 밖 probe) — 20260916 새 파일: status=Added added=36 dup=0 filerows=36 uniquekeys=36. xlsxequal rows=36 mismatch=0 unmatched=0(화면 ReviewMeasurementRow.Cause 와 14~18열 전부 일치, 한글 xlsx 왕복 확인)"
        status: pass
    human_judgment: false
  - id: D2
    description: "다른 날짜 폴더를 열고 누르면 같은 파일 아래에 계속 쌓이고(20260916 36행 뒤에 20260601 140행이 붙어 176행), 같은 날짜로 두 번 누르면 두 번째는 추가 0(NothingNew)이며 행 수·파일 내용이 그대로다"
    requirement: "NGA-03/NGA-04"
    verification:
      - kind: other
        ref: "NgCauseProbe.exe xlsx — 20260916 두 번째 실행: status=NothingNew added=0 dup=36 filerows=36(그대로). 20260601 이어서 실행: status=Added added=140 filerows=176 uniquekeys=176"
        status: pass
    human_judgment: false
  - id: D3
    description: "엑셀 파일이 다른 프로그램에 열려 있으면 안내 후 중단하고 파일은 바이트 단위로 그대로, 임시 파일도 남지 않는다. 날짜 폴더를 안 열었거나 없는 폴더/cycle.json 없음/NG 0 인 경우 전부 엑셀 파일을 만들거나 건드리지 않는다. 손상된 cycle.json 은 '읽지 못한 결과 N건'으로 세고 건너뛴다"
    requirement: "NGA-03"
    verification:
      - kind: other
        ref: "NgCauseProbe.exe xlsxlock — status=FileLocked unchanged=1(SHA256 전후 동일) templeft=0. xlsxempty — nofolder=PASS missing=PASS nocycles=PASS nong=PASS created=0(4경우 전부 대상 xlsx 미생성). xlsxpartial — status=Added, unreadable=1(broken cycle.json 1건 건너뜀)"
        status: pass
    human_judgment: false
  - id: D4
    description: "리뷰어에 'NG 누적 엑셀 저장' 버튼이 '날짜 폴더 열기' 바로 아래에 생기고, 연 날짜 폴더를 서비스에 넘겨 안내 창만 띄우는 배선이 분기 없이(if/switch/for/foreach/while 0개) 커밋되었다"
    requirement: "NGA-04"
    verification:
      - kind: other
        ref: "grep: button=1 content=1 after_load=1(버튼 위치), handler=1 call=1 assign=1 field=1(배선), branch_kw=0(추가 줄 분기 0), codelines=6(핸들러 본문 3문장 + 필드 대입 1줄 규모). 회귀: rows 20260916 ng=36 exceptions=0, xlsx status=Added added=36(Task 1 판정 회귀 없음)"
        status: pass
    human_judgment: true
    rationale: "probe 는 저장소 밖 스크래치 코드로 커밋되지 않아 CI 에서 재실행되지 않는다. 버튼을 실제로 눌러 화면에 안내 창이 뜨고 파일이 열리는지는 78-07 UAT U-3 에서 사람이 확인한다"

duration: 약 12분
completed: 2026-09-17
status: complete
---

# Phase 78 Plan 05: NG 누적 엑셀 Summary

**리뷰어가 연 날짜 폴더의 NG 측정만 화면과 같은 원인 문구로 NG_분석_누적.xlsx 한 파일에 중복 없이 계속 쌓는 서비스(NgAccumulationExportService)와 리뷰어 버튼 배선을 추가한 2커밋**

## Performance

- **Duration:** 약 12분(base 18:45:20 → Task 2 커밋 18:57:09, commit timestamp 기준)
- **Started:** 2026-09-17(78-04 마지막 커밋 직후)
- **Completed:** 2026-09-17
- **Tasks:** 2 (Task 1 tdd, Task 2 auto)
- **Files modified:** 3 (ExcelExportService.cs, ReviewerWindow.xaml, ReviewerWindow.xaml.cs)

## Base / 커밋 해시

- **base-78-05 (편집 전 HEAD):** `5a4674c8d9b396da5337cec4a16a0a41cb470795`
- **Task 1 커밋:** `4b54c4d0` — feat(78-05): NG 누적 엑셀 서비스(중복 제외·잠금 안전)
- **Task 2 커밋:** `ff019e36` — feat(78-05): 리뷰어 NG 누적 엑셀 저장 버튼

## 빌드 결과

Debug|x64 (Release 금지, D:\Data 읽기만) — 두 커밋 시점 모두 `msbuild_exit=0`, errors 0.

## Probe xlsx 계열 출력 원문 (Task 1, 전체 시퀀스)

```
xlsx status=Added added=36 dup=0 unreadable=0 filerows=36 uniquekeys=36
xlsx status=NothingNew added=0 dup=36 unreadable=0 filerows=36 uniquekeys=36
xlsxequal rows=36 mismatch=0 unmatched=0
xlsx status=Added added=140 dup=0 unreadable=0 filerows=176 uniquekeys=176
xlsxlock status=FileLocked unchanged=1 templeft=0
xlsxempty nofolder=PASS missing=PASS nocycles=PASS nong=PASS created=0
xlsxpartial status=Added added=6 unreadable=1
```

순서: 20260916 새 파일(36건) → 같은 명령 재실행(0건, idempotency) → xlsxequal(화면=엑셀 14~18열 비교) → 20260601 이어 붙이기(140건, 누적 176행) → 잠금 상태에서 20260916 재시도(FileLocked, 파일 불변) → 빈/없는/NG0 폴더 4경우(파일 미생성) → 손상 cycle.json 섞인 폴더(1건 건너뛰고 나머지 추가).

## Probe rows/xlsx 회귀 출력 원문 (Task 2, 20260916 재실행)

```
date=20260916 cycles=24 loadnull=0 rows=216 ng=36 codes=R0:0,R1:0,R2:0,R3:0,R4:0,R5:0,R6:36,R7:0,R8:0,R9:0,RX:0 suspects=R5:3,R6:0,R7:0,R8:22,R9:0 newfields=0 exceptions=0
xlsx status=Added added=36 dup=0 unreadable=0 filerows=36 uniquekeys=36
```

버튼 배선 커밋이 78-01/02 의 원인 규칙 판정(R6:36)과 Task 1 의 xlsx 누적(added=36)에 영향을 주지 않았다.

## 엑셀 머리글 20개 (실제 문자열, HEADER_TEXTS 그대로)

| # | 머리글 |
|---|---|
| 1 | 검사시각 |
| 2 | 검사구분 |
| 3 | 레시피 |
| 4 | 자재번호 |
| 5 | Shot |
| 6 | FAI |
| 7 | 측정명 |
| 8 | 측정값 |
| 9 | 공칭 |
| 10 | 공차+ |
| 11 | 공차- |
| 12 | 판정 |
| 13 | 사용 Z |
| 14 | 추정 원인 |
| 15 | 근거 |
| 16 | 확인할 일 |
| 17 | 함께 의심 |
| 18 | 원인 코드 |
| 19 | 사진 경로 |
| 20 | 사이클 폴더 |

## 메시지 문구 const 목록 (BuildOutcome 이 조립하는 한국어 안내, PLAN 규격과 1:1)

| const | 문구 | 상태/아이콘 |
|---|---|---|
| `MSG_NO_FOLDER` | 먼저 '날짜 폴더 열기' 로 날짜 폴더를 여세요. | NoFolder / Warning |
| `MSG_NO_CYCLES` | 이 폴더에는 검사 결과(cycle.json)가 없습니다. 날짜 폴더(예: 20260916)를 여세요. | NoCycles / Warning |
| `MSG_NO_NG` | 이 날짜 폴더에는 NG 가 없습니다 (추가 0건). | NoNg / Information |
| `MSG_NOTHING_NEW_FORMAT` | 새로 추가할 NG 가 없습니다 — 이미 들어 있는 {0}건은 건너뛰었습니다. | NothingNew / Information |
| `MSG_ADDED_FORMAT` | NG {0}건을 추가했습니다 (이미 있던 {1}건 건너뜀). | Added / Information |
| `MSG_FILE_LOCKED` | NG 누적 엑셀 파일이 열려 있어 저장하지 못했습니다. 엑셀을 닫고 다시 누르세요. | FileLocked / Warning |
| `MSG_FAILED` | NG 누적 엑셀 저장에 실패했습니다 (Error 로그 확인). | Failed / Error |
| `MSG_UNREADABLE_FORMAT` | 읽지 못한 검사 결과 {0}건 — 저장 중이었거나 손상된 파일입니다. 잠시 후 다시 누르면 추가됩니다. | (Added/NothingNew/NoNg 에 unreadable>0 이면 덧붙는 줄) |
| `MSG_LARGE_FILE_FORMAT` | 파일이 커졌습니다({0}행). 파일 이름을 바꿔 보관하면 다음부터 새 파일로 시작합니다. | (Added 이고 100000행 이상이면 덧붙는 줄) |

## Verify 판정 숫자 (acceptance criteria 그대로)

### Task 1

| 지표 | 값 | 기준 |
|---|---|---|
| `msbuild_exit` | 0 | = 0 |
| xlsx 1번째 | `status=Added added=36 dup=0 unreadable=0 filerows=36 uniquekeys=36` | 동일 |
| xlsx 2번째 | `status=NothingNew added=0 dup=36 filerows=36` | 동일(idempotency) |
| `xlsxequal` | `rows=36 mismatch=0 unmatched=0` | 동일(화면=엑셀) |
| xlsx 3번째(20260601) | `status=Added added=140 filerows=176 uniquekeys=176` | 동일(날짜 넘어 누적) |
| `xlsxlock` | `status=FileLocked unchanged=1 templeft=0` | 동일 |
| `xlsxempty` | `nofolder=PASS missing=PASS nocycles=PASS nong=PASS created=0` | 동일 |
| `xlsxpartial` | `status=Added unreadable=1` | 동일 |
| `svc` | 5 | = 5 |
| `consts` | 3 | = 3 |
| `replace` | 1 | = 1 |
| `sharenone` | 2 | ≥ 1 |
| `analyze` | 1 | = 1 |
| `writes` | 1 | = 1(TryDeleteTempFile 의 File.Delete 1곳만) |
| `dataroot` | 0 | = 0 |
| 추가 줄 하드룰(ternary/coalesce/nullcond/switchexpr/datesig/logic3/lambda) | 전부 0 | = 0 |
| `deleted` | 0 | ≤ 1 |
| `commit_files` | `WPF_Example/Custom/Export/ExcelExportService.cs` 1개뿐 | 동일 |

### Task 2

| 지표 | 값 | 기준 |
|---|---|---|
| `msbuild_exit` | 0 | = 0 |
| 회귀(20260916) | `ng=36 exceptions=0`, xlsx `status=Added added=36` | 동일 |
| `button` / `content` / `after_load` | 1 / 1 / 1 | 전부 1 |
| `handler` / `call` / `assign` / `field` | 1 / 1 / 1 / 1 | 전부 1 |
| `branch_kw` | 0 | = 0 |
| `codelines` | 6 | ≤ 6 |
| 추가 줄 하드룰(ternary/coalesce/nullcond/datesig) | 전부 0 | = 0 |
| `deleted` | 0 | = 0 |
| `untouched`(MainView.xaml.cs·csproj) | 0 | = 0 |
| `commit_files` | ReviewerWindow.xaml·ReviewerWindow.xaml.cs 2개뿐 | 동일 |

## 규격과 다르게 구현한 점

없음 — PLAN 의 'NG 누적 엑셀 규격' 표·흐름(1~9단계)·메시지 문구·XAML 삽입 위치·핸들러 3문장을 그대로 구현했다. 실행자 재량으로 남겨진 지점(BuildOutcome 의 상태별 if/else 순서, WriteRows 의 using 블록 경계 — PLAN 은 "using" 이라고만 명시)은 CLAUDE.md 하드룰(로직 3개 이상 연산자 금지 등)을 지키는 선에서 채웠다.

## 새 심볼 목록 (Artifacts 표 78-05 행과 대조)

| 심볼 | 파일 | Artifacts 표 대조 |
|---|---|---|
| `ENgAccumExportStatus`(Added/NothingNew/NoNg/NoCycles/NoFolder/FileLocked/Failed) | ExcelExportService.cs | 일치 |
| `NgAccumExportOutcome`(Status/AddedCount/SkippedDuplicateCount/UnreadableCycleCount/TotalDataRows/Message/Icon) | ExcelExportService.cs | 일치(PLAN 프로퍼티 목록 그대로) |
| `NgAccumulationExportService`(`OUTPUT_FILE_NAME`="NG_분석_누적.xlsx", `SHEET_NAME`="NG 누적", `MESSAGE_TITLE`="NG 누적 엑셀", `BuildOutputPath`, `AppendDateFolder` + private 헬퍼 15개) | ExcelExportService.cs | 일치 |
| `_loadedDateFolder`, `Button_NgAccumExport_Click`, XAML `btn_ngAccumExport`("NG 누적 엑셀 저장") | ReviewerWindow.xaml(.cs) | 일치 |

## Task Commits

1. **Task 1 (tdd): NgAccumulationExportService — 날짜 폴더 NG 를 한 xlsx 에 중복 없이 추가(잠금·손상·빈 폴더 안전)** - `4b54c4d0` (feat)
2. **Task 2: 리뷰어 'NG 누적 엑셀 저장' 버튼 배선** - `ff019e36` (feat)

**Plan metadata:** (다음 커밋) - `docs(78-05): complete NG 누적 엑셀 plan`

## Files Created/Modified

- `WPF_Example/Custom/Export/ExcelExportService.cs` - `ENgAccumExportStatus`/`NgAccumExportOutcome`/`NgAccumulationExportService` 추가(`using System.Windows;` 1줄 포함)
- `WPF_Example/UI/Reviewer/ReviewerWindow.xaml` - `btn_ngAccumExport`("NG 누적 엑셀 저장") 버튼을 `btn_loadFolder` 바로 다음에 추가
- `WPF_Example/UI/Reviewer/ReviewerWindow.xaml.cs` - `_loadedDateFolder` 필드, `LoadCycleFolders` 첫 문장 대입, `Button_NgAccumExport_Click` 핸들러(3문장) 추가

## Decisions Made

없음 — PLAN 이 흐름·상수·메시지·삽입 위치를 정확히 지정했고 그대로 구현했다.

## Deviations from Plan

None - 계획대로 실행. 모든 acceptance criteria(수치 포함)가 첫 실행에서 그대로 통과했다.

## Issues Encountered

- probe(`NgCauseProbe.cs`, 저장소 밖 `C:/Users/admin/AppData/Local/Temp/p78-probe/`)에 `xlsx`/`xlsxlock`/`xlsxempty`/`xlsxpartial`/`xlsxequal` 5개 모드를 처음 작성할 때 `NgAccumExportOutcome`/`ENgAccumExportStatus` 를 `ReringProject.UI` 네임스페이스로 잘못 참조해 컴파일 에러(CS0234)가 났다 — 실제로는 `ExcelExportService.cs` 가 속한 `ReringProject.Export` 네임스페이스다. `sed` 로 일괄 치환 후 재컴파일해 바로 해결(probe 자체 수정, 저장소 코드 아님, 별도 커밋 없음).

## User Setup Required

None - 외부 서비스 설정 불필요.

## Next Phase Readiness

- 78-06(VersionDefine 변경 이력)이 이 plan 이 추가한 `NgAccumulationExportService`/`btn_ngAccumExport` 를 변경 이력에 기록할 수 있다.
- 78-07 UAT U-3 에서 사람이 실제로 버튼을 눌러 파일이 생기고 내용이 화면과 같은지 확인해야 한다(A-78-E1 계열 — probe 는 저장소 밖 스크래치 코드라 CI 재실행 안 됨).
- `NG_분석_누적.xlsx` 파일명·시트명·20열 순서는 운영자가 쌓기 시작하면 바꾸기 어렵다(reversibility: costly) — 사용자가 실제로 파일을 만들어 쌓기 시작하기 전에 UAT 로 규격을 최종 확인하는 것이 좋다.
- 블로커 없음.

---
*Phase: 78-reviewer-ng-cause-analysis*
*Completed: 2026-09-17*

## Self-Check: PASSED

- FOUND: `.planning/phases/78-reviewer-ng-cause-analysis/78-05-SUMMARY.md`
- FOUND commit: `4b54c4d0` (Task 1)
- FOUND commit: `ff019e36` (Task 2)
- FOUND: `WPF_Example/Custom/Export/ExcelExportService.cs`
- FOUND: `WPF_Example/UI/Reviewer/ReviewerWindow.xaml`
- FOUND: `WPF_Example/UI/Reviewer/ReviewerWindow.xaml.cs`
