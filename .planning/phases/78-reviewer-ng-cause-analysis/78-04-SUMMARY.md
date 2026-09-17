---
phase: 78-reviewer-ng-cause-analysis
plan: 04
subsystem: ui
tags: [wpf, halcon, reviewer, cycle-json, ng-cause-analysis, export-cleanup]

requires:
  - phase: 78-reviewer-ng-cause-analysis (78-01)
    provides: "ReviewMeasurementRow.cs 골격(78-01이 만든 파일에 ReviewerImagePathResolver를 추가)"
provides:
  - "ReviewerImagePathResolver (ResolveRowImagePath/ResolveCycleImagePath/IsUsableImageFile/MIN_IMAGE_FILE_BYTES) — 리뷰어 사진 = 실제 촬영 원본 우선(D-78-07)"
  - "리뷰어 왼쪽 버튼 스택에서 사이클 1건 엑셀 export·차트 이미지 캡처 점검 삭제(D-78-06), 반복검사 묶음·Align 정합 조회·공용 export 헬퍼 유지"
affects: [78-05, 78-06, 78-07]

tech-stack:
  added: []
  patterns:
    - "사진 경로 해석은 순수 정적 클래스(ReviewerImagePathResolver) — 파일 존재·크기 확인만, code-behind 는 결과 문자열을 LoadImage 에 넘기는 배선 2줄뿐"
    - "삭제 전 grep -rl 로 호출부가 리뷰어 한 곳뿐인지 먼저 확인한 뒤 메서드·버튼·const 를 통째로 지운다(78-01 RESEARCH Pitfall 1 그대로 적용)"

key-files:
  created: []
  modified:
    - WPF_Example/UI/ViewModel/ReviewMeasurementRow.cs
    - WPF_Example/UI/Reviewer/ReviewerWindow.xaml.cs
    - WPF_Example/UI/Reviewer/ReviewerWindow.xaml
    - WPF_Example/Custom/Export/ExcelExportService.cs
    - WPF_Example/Custom/Export/ChartImageCapture.cs

key-decisions: []

patterns-established: []

requirements-completed: [NGA-06, NGA-04]

coverage:
  - id: D1
    description: "리뷰어에서 측정 행을 누르면 그 FAI 의 실제 촬영 원본 사진(OriginImageFileName)이 뜨고, 없을 때만 기존 Shot ResultImagePath 로 폴백한다. 사이클 전체 보기도 같은 규칙(측정 결과 있는 첫 FAI 원본 우선)을 따른다"
    requirement: "NGA-06"
    verification:
      - kind: other
        ref: "NgCauseProbe.exe <binDir> image <scratchDir> <dateDir>... (repo 밖 probe) — 합성 9사례 전부 PASS, 20260916 실데이터 216행 전부 원본(row_origin=216 row_fallback=0 chosen_missing=0), 20260601 은 원본 0(row_origin=0)"
        status: pass
    human_judgment: true
    rationale: "probe 는 저장소 밖 스크래치 코드로 커밋되지 않아 CI 에서 재실행되지 않는다. 화면에 실제로 실제 촬영 사진이 뜨는지는 78-07 UAT U-1/U-2 에서 사람이 확인한다"
  - id: D2
    description: "리뷰어 왼쪽 버튼에서 '사이클 1건 엑셀 export' 와 '차트 이미지 캡처 점검' 버튼·Click 핸들러·xlsx 생성 메서드·스모크 PNG 메서드가 전부 사라지고, 반복도·CPK export 가 쓰는 공용 헬퍼(BuildJudgementText/LoadCaptureImageBytes/TryInsertCaptureImage/ApplyCaptureColumnWidth)와 차트 렌더(RenderHistogramPng/RenderTrendPng/TryInsertChartPicture)는 그대로 남아 빌드가 통과한다"
    requirement: "NGA-04"
    verification:
      - kind: other
        ref: "Debug|x64 빌드 msbuild_exit=0 errors=0. grep: gone_xaml=0 gone_method=0, keep_helpers=4 keep_charts=3 keep_callers=4, keep_xaml=9 keep_handlers=5 order=1(왼쪽 스택 순서 고정), NgCauseProbe.exe rows/image 20260916 재실행 회귀 0(ng=36 exceptions=0, row_origin=216 chosen_missing=0)"
        status: pass
    human_judgment: false

duration: 약 7분
completed: 2026-09-17
status: complete
---

# Phase 78 Plan 04: 리뷰어 실제 촬영 사진 + 필요 없는 기능 삭제 Summary

**리뷰어 사진을 시뮬레이션 경로가 아닌 그 검사의 실제 촬영 원본(OriginImageFileName)으로 바꾸고(D-78-07), 사이클 1건 엑셀 export·차트 이미지 캡처 점검 버튼과 그 코드만 삭제한(D-78-06) 2커밋**

## Performance

- **Duration:** 약 7분(base 커밋 18:35:55 → Task 2 커밋 18:43:20, commit timestamp 기준)
- **Started:** 2026-09-17 (78-03 마지막 커밋 직후)
- **Completed:** 2026-09-17
- **Tasks:** 2 (Task 1 tdd, Task 2 auto)
- **Files modified:** 5 (ReviewMeasurementRow.cs, ReviewerWindow.xaml.cs, ReviewerWindow.xaml, ExcelExportService.cs, ChartImageCapture.cs)

## Base / 커밋 해시

- **base-78-04 (편집 전 HEAD):** `d4bdfc2761e251296adf3040fe7115ab8ea3c03a`
- **Task 1 커밋:** `9ca082ab` — feat(78-04): 리뷰어 사진을 실제 촬영 원본으로
- **Task 2 커밋:** `693a1424` — refactor(78-04): 리뷰어 사이클 1건 엑셀·차트 캡처 점검 기능 삭제 (D-78-06)

## 빌드 결과

Debug|x64 (Release 금지, D:\Data 읽기만) — 두 커밋 시점 모두 `msbuild_exit=0`, `errors=0`.

## 파일별 `git diff` 통계 (base → HEAD, 전체 plan 누적)

| 파일 | added | deleted |
|---|---|---|
| WPF_Example/UI/ViewModel/ReviewMeasurementRow.cs | 153 | 0 |
| WPF_Example/UI/Reviewer/ReviewerWindow.xaml.cs | 5 | 67 |
| WPF_Example/UI/Reviewer/ReviewerWindow.xaml | 0 | 7 |
| WPF_Example/Custom/Export/ExcelExportService.cs | 1 | 116 |
| WPF_Example/Custom/Export/ChartImageCapture.cs | 0 | 46 |

지운 const 2개(ExcelExportService.cs): `CAPTURE_IMAGE_COLUMN`(캡쳐이미지 컬럼 번호, Export 메서드 삭제로 참조 0), `CAPTURE_WAIT_BUDGET_MS`(export 1회 대기 예산 상한, 같은 이유로 참조 0). 나머지 const(`CAPTURE_WAIT_TIMEOUT_MS`/`CAPTURE_WAIT_POLL_MS`/`CAPTURE_BOX_WIDTH_PX`/`CAPTURE_BOX_HEIGHT_PX`/`EXCEL_PIXELS_PER_WIDTH_UNIT`/`EXCEL_POINTS_PER_PIXEL`/`JPEG_MIN_BYTES`)는 유지 메서드(`WaitForCaptureImage`/`TryInsertCaptureImage`/`ApplyCaptureColumnWidth`)가 계속 쓰므로 그대로 두었다.

## 삭제 전 호출부 grep 결과 (Task 2, 계획 단계 확인 재확인)

```
Button_ExportExcel_Click  → WPF_Example/UI/Reviewer/ReviewerWindow.xaml(Click 속성) + ReviewerWindow.xaml.cs(핸들러 정의) 뿐
Button_ChartSmoke_Click   → 동일하게 ReviewerWindow.xaml(.cs) 뿐
ExcelExportService.Export(  → ReviewerWindow.xaml.cs:365(호출부) 뿐 — RepeatExcelExportService.Export 는 이름만 겹치는 별개 메서드
TrySaveSmokePng            → ChartImageCapture.cs(정의) + ReviewerWindow.xaml.cs(호출부) 뿐
```

삭제 전 확인대로 전부 리뷰어 한 곳에서만 호출되었고, 반복도·CPK export(`RepeatExcelExportService.cs`, `CpkReportExportService.cs`)는 그 이름이 겹치는 별개 메서드를 갖고 있을 뿐 삭제 대상을 호출하지 않았다.

## Probe image 출력 원문 (Task 1, 합성 9사례 + 실데이터)

```
imgcase=row_origin_exists PASS
imgcase=row_origin_missing PASS
imgcase=row_origin_relative PASS
imgcase=row_origin_zero_bytes PASS
imgcase=row_both_missing PASS
imgcase=row_null_inputs PASS
imgcase=cycle_first_measured PASS
imgcase=cycle_pending_only PASS
imgcase=cycle_null PASS
image date=20260601 cycles=8 cycle_origin=0 cycle_fallback=0 cycle_none=8 rows=322 row_origin=0 row_fallback=0 row_none=322 chosen_missing=0
image date=20260916 cycles=24 cycle_origin=8 cycle_fallback=16 cycle_none=0 rows=216 row_origin=216 row_fallback=0 row_none=0 chosen_missing=0
imgcase_fail=0
```

20260601(옛 cycle.json, OriginImageFileName 전부 빈 문자열)은 행 216개 중 원본 해석 0건 — 폴백 규칙이 실데이터로도 확인됨. 20260916 은 216행 전부 원본으로 해석되고 선택된 경로가 전부 실제 파일이다(`chosen_missing=0`).

## Probe rows/image 회귀 출력 원문 (Task 2 커밋 후, 20260916 재실행)

```
date=20260916 cycles=24 loadnull=0 rows=216 ng=36 codes=R0:0,R1:0,R2:0,R3:0,R4:0,R5:0,R6:36,R7:0,R8:0,R9:0,RX:0 suspects=R5:3,R6:0,R7:0,R8:22,R9:0 newfields=0 exceptions=0
image date=20260916 cycles=24 cycle_origin=8 cycle_fallback=16 cycle_none=0 rows=216 row_origin=216 row_fallback=0 row_none=0 chosen_missing=0
```

D-78-06 삭제가 78-01/78-02 의 원인 규칙 판정(R6:36)과 78-04 Task 1 의 사진 해석(row_origin=216)에 영향을 주지 않았다.

## Verify 판정 숫자 (acceptance criteria 그대로)

### Task 1

| 지표 | 값 | 기준 |
|---|---|---|
| `msbuild_exit` | 0 | = 0 |
| 합성 9사례 | 전부 PASS | `imgcase_fail=0` |
| `image date=20260916` | `rows=216 row_origin=216 row_fallback=0 chosen_missing=0`, `cycle_origin=8` | rows=216/row_origin=216/row_fallback=0/chosen_missing=0, cycle_origin≥1 |
| `image date=20260601` | `row_origin=0 chosen_missing=0` | row_origin=0/chosen_missing=0 |
| `resolver` | 4 | = 4 |
| `wire` | 2 | = 2 |
| `oldload` | 0 | = 0 |
| `branch_kw` | 2 | = 2(교체된 null 가드 if 2개뿐) |
| ReviewMeasurementRow.cs `deleted` | 0 | = 0 |
| ReviewerWindow.xaml.cs `deleted` | 8 | 1~13 |
| 하드룰(ternary/coalesce/nullcond/switchexpr/datesig/logic3/qmark) | 전부 0 | = 0 |
| `commit_files` | 2개 경로뿐 | 2개 경로뿐 |

### Task 2

| 지표 | 값 | 기준 |
|---|---|---|
| `msbuild_exit` / `errors` | 0 / 0 | = 0 / 0 |
| probe 회귀(20260916) | `ng=36 exceptions=0`, `row_origin=216 chosen_missing=0` | 동일 |
| `gone_xaml` | 0 | = 0 |
| `gone_refs` | 1 (아래 각주) | = 0(각주 참고) |
| `gone_method` | 0 | = 0 |
| `keep_helpers` | 4 | = 4 |
| `keep_charts` | 3 | ≥ 3 |
| `keep_callers` | 4 | = 4 |
| `keep_xaml` | 9 | = 9 |
| `keep_handlers` | 5 | = 5 |
| `order` | 1 | = 1 |
| `deleted_xaml/xc/ex/ch` | 7 / 67 / 116 / 46 | ≤8 / ≤80 / ≤125 / ≤50 |
| `added_task2` | 1 | ≤ 3 |
| `datesig` / `qmark` | 0 / 0 | = 0 / 0 |
| `untouched` | 0 | = 0 |
| `commit_files` | 4개 경로뿐 | 4개 경로뿐 |

**각주(`gone_refs=1`):** 계획의 검증 정규식 `ExcelExportService\.Export\(` 이 클래스 경계를 앵커링하지 않아, 정상적으로 유지되는 `ReringProject.Export.RepeatExcelExportService.Export(` 호출(`ReviewerWindow.xaml.cs:495`, 반복도 엑셀 export 버튼 핸들러)까지 부분 문자열로 매치되어 기대값 0 대신 1 이 나왔다. 실제 삭제 대상인 `ExcelExportService.Export(`(대문자 E 로 시작하는 정확한 클래스명, 접두사 `Repeat` 없음) 호출은 `gone_method=0`과 별도의 `grep -rn 'ExcelExportService\.Export\('` 로 재확인한 결과 0건이었다 — 코드 결함이 아니라 계획 검증 스크립트의 정규식이 겹친 것으로 판단해 grep 재확인 후 진행했다.

## 새 심볼 목록 (Artifacts 표 78-04 행과 대조)

| 심볼 | 파일 | Artifacts 표 대조 |
|---|---|---|
| `ReviewerImagePathResolver`(`MIN_IMAGE_FILE_BYTES`, `IsUsableImageFile`, `IsExistingFile`(private), `FaiHasMeasuredResult`(private), `ResolveRowImagePath`, `ResolveCycleImagePath`, `FindMeasuredOriginInShot`(private)) | ReviewMeasurementRow.cs | 일치 |
| ReviewerWindow.xaml.cs 배선 2곳(`ResolveCycleImagePath(cycle)`, `ResolveRowImagePath(row.OwnerShot, row.OwnerFai)`) | ReviewerWindow.xaml.cs | 일치 |
| 삭제: `btn_exportExcel`/`btn_chartSmoke`(XAML), `Button_ExportExcel_Click`/`Button_ChartSmoke_Click`(핸들러), `ExcelExportService.Export(CycleResultDto, string)`, `ChartImageCapture.TrySaveSmokePng` | ReviewerWindow.xaml(.cs), ExcelExportService.cs, ChartImageCapture.cs | 일치 |

## Task Commits

1. **Task 1 (tdd): 리뷰어 사진 = 실제 촬영 원본(원본 우선, 없으면 기존 경로)** - `9ca082ab` (feat)
2. **Task 2: 필요 없는 리뷰어 기능 삭제 — 사이클 1건 엑셀 export·차트 이미지 캡처 점검** - `693a1424` (refactor)

**Plan metadata:** (다음 커밋) - `docs(78-04): complete 리뷰어 실제 촬영 사진·불필요 기능 삭제 plan`

## Files Created/Modified

- `WPF_Example/UI/ViewModel/ReviewMeasurementRow.cs` - `ReviewerImagePathResolver` 정적 클래스 추가(원본 우선 사진 경로 해석)
- `WPF_Example/UI/Reviewer/ReviewerWindow.xaml.cs` - 사진 로드 2곳을 해석기 호출로 교체, `btn_exportExcel.IsEnabled` 배선·`Button_ExportExcel_Click`·`Button_ChartSmoke_Click` 삭제
- `WPF_Example/UI/Reviewer/ReviewerWindow.xaml` - "엑셀 export"·"차트 이미지 캡처 점검" Button 요소 2개 삭제
- `WPF_Example/Custom/Export/ExcelExportService.cs` - `Export(CycleResultDto, string)` 삭제, 참조 0 이 된 const 2개 삭제, 클래스 요약 주석을 공용 헬퍼 설명으로 교체
- `WPF_Example/Custom/Export/ChartImageCapture.cs` - `TrySaveSmokePng` 삭제

## Decisions Made

없음 — 계획(78-04-PLAN.md)이 삽입/삭제 위치·해석기 메서드 시그니처·삭제 범위를 정확히 지정했고 그대로 구현했다.

## Deviations from Plan

None - 계획대로 실행. 유일한 차이는 위 "Verify 판정 숫자" 각주에 적은 `gone_refs` 검증 정규식의 클래스명 부분 문자열 매치(계획 검증 스크립트의 한계, 코드 변경 아님) — 실제 삭제 대상 참조는 별도 grep 으로 0건임을 재확인했다.

## Issues Encountered

None — probe(`NgCauseProbe.cs`, 저장소 밖 `C:/Users/admin/AppData/Local/Temp/p78-probe/`)에 `image` 모드(합성 9사례 + 날짜 폴더별 원본/폴백/누락 집계)를 신설했고, Task 1·Task 2 모두 1회 컴파일·실행으로 통과했다.

## User Setup Required

None - 외부 서비스 설정 불필요.

## Next Phase Readiness

- 78-05(NG 누적 엑셀)가 새로 만들 `NgAccumulationExportService`는 이 plan 이 유지한 `ExcelExportService` 공용 헬퍼(`BuildJudgementText`/`LoadCaptureImageBytes`/`TryInsertCaptureImage`/`ApplyCaptureColumnWidth`)를 그대로 재사용할 수 있다.
- 리뷰어 왼쪽 버튼 순서(날짜 폴더 열기 → 자재번호 → 이전 결과에 누적 → 이미지 폴더 반복 검사 → 진행 표시 → 반복도 엑셀 export → CPK 리포트 export → Align 정합 조회 → 중간 단계도 보기)가 고정되어 78-05 가 추가할 "NG 누적 엑셀 저장" 버튼의 삽입 위치를 그대로 참고할 수 있다.
- 화면에 실제 촬영 사진이 뜨는지, 쓰는 도중인 파일을 열 때의 동작(A-78-A3)은 78-07 UAT U-1/U-2 대기.
- 블로커 없음.

---
*Phase: 78-reviewer-ng-cause-analysis*
*Completed: 2026-09-17*

## Self-Check: PASSED

- FOUND: `.planning/phases/78-reviewer-ng-cause-analysis/78-04-SUMMARY.md`
- FOUND commit: `9ca082ab` (Task 1)
- FOUND commit: `693a1424` (Task 2)
- FOUND: 5개 수정 파일 전부(ReviewMeasurementRow.cs, ReviewerWindow.xaml.cs, ReviewerWindow.xaml, ExcelExportService.cs, ChartImageCapture.cs)
