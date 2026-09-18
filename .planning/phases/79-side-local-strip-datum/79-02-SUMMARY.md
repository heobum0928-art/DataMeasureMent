---
phase: 79-side-local-strip-datum
plan: 02
subsystem: vision-measurement
tags: [halcon, cycle-json, csv-history, mvvm, datagrid, csharp7.2]

requires:
  - phase: 79-01
    provides: "MeasurementBase.REF_SOURCE_LOCAL/REF_SOURCE_FALLBACK, LastRefSource(옵션 켠 측정만 채워짐, ClearResult 에서 null)"
provides:
  - "MeasurementBase.FormatRefSource/ParseRefSource 단일 표시·파싱 규칙(국부/국부실패→전역/빈칸)"
  - "cycle.json MeasurementResultDto.RefSource — 옛 파일은 null 로 역직렬화, 새 파일은 3값 왕복"
  - "일자별 CSV 끝 열(인덱스16) 사용기준 — COLUMN_COUNT 14 유지, 옛 15·16칸 행은 null 로 읽힘"
  - "결과 그리드(MainView)·리뷰어(ReviewerWindow) '기준' 열 — VM 바인딩, code-behind 무수정"
affects: [79-03-roi-wiring-overlay-color, 79-04-version-audit, 79-05-realab-uat]

tech-stack:
  added: []
  patterns:
    - "표시·파싱 단일 소스 정적 메서드(MeasurementBase.FormatX/ParseX) — Phase 77 SZF-04 선례를 그대로 재사용, CSV writer/loader/두 VM 이 규칙을 복제하지 않고 이 메서드만 호출"
    - "CSV 하위호환: 신규 컬럼은 항상 맨 끝에만 추가, COLUMN_COUNT 는 올리지 않고 fields.Count > COL_X 로만 존재 여부 판정"

key-files:
  created: []
  modified:
    - WPF_Example/Custom/Sequence/Inspection/MeasurementBase.cs
    - WPF_Example/UI/ViewModel/CycleResultDto.cs
    - WPF_Example/Custom/Sequence/Inspection/CycleResultSerializer.cs
    - WPF_Example/Custom/Sequence/Inspection/MeasurementHistoryCsvWriter.cs
    - WPF_Example/Custom/Sequence/Inspection/MeasurementHistoryCsvLoader.cs
    - WPF_Example/UI/ViewModel/MeasurementResultRow.cs
    - WPF_Example/UI/ContentItem/MainView.xaml
    - WPF_Example/UI/ViewModel/ReviewMeasurementRow.cs
    - WPF_Example/UI/Reviewer/ReviewerWindow.xaml

key-decisions:
  - "표시 문자열은 REF_SOURCE_LOCAL_TEXT(\"국부\")/REF_SOURCE_FALLBACK_TEXT(\"국부실패→전역\") 상수 2개 + FormatRefSource/ParseRefSource 두 메서드로만 구현 — Phase 77 FormatSelectedZ/ParseSelectedZ 위치(ParseSelectedZ 바로 다음)에 그대로 이어붙임"
  - "CSV 끝 열은 선택Z(인덱스15) 다음 인덱스16에만 추가 — MeasurementHistoryCsvWriter.cs 는 헤더 문자열 1줄만 수정, 나머지는 삽입뿐"
  - "MainView.xaml.cs·ReviewerWindow.xaml.cs 는 손대지 않음 — 표시 문자열은 VM(RefSourceText getter/property)에서 만들고 XAML 은 바인딩 1줄만 추가(CLAUDE.md MVVM 하드룰)"

requirements-completed: [LSR-04, LSR-05]

coverage:
  - id: D1
    description: "cycle.json 의 측정마다 RefSource 가 기록되고(옵션 켠 측정 Local/LocalFallback, 옵션 꺼짐 null), 일자별 CSV 끝 열(인덱스16, 헤더 '사용기준')에도 같은 값이 기록된다. 옛 cycle.json·옛 15/16칸 CSV 행은 예외 없이 RefSource null 로 읽힌다."
    requirement: "LSR-04"
    verification:
      - kind: integration
        ref: "LocalRefProbe.exe records — case=fmt_*(6)·parse_*(6)·json_roundtrip·json_old1·json_old2·csv_buildline·csv_query, records_fail=0"
        status: pass
    human_judgment: false
  - id: D2
    description: "결과 그리드(MainView)와 리뷰어 측정표에 '기준' 열이 선택Z/사용 Z 바로 뒤에 추가되고, 두 화면의 RefSourceText 는 같은 MeasurementBase.FormatRefSource 한 곳에서 만들어진다. 옵션 꺼짐 측정은 빈칸이고 값·판정·기존 CSV 16칸은 회귀 0."
    requirement: "LSR-05"
    verification:
      - kind: integration
        ref: "LocalRefProbe.exe display — case=display_review_*(3)·display_main_*(3), display_fail=0"
        status: pass
      - kind: integration
        ref: "OffRegressProbe.exe(편집 전 exe API 기준) — regress-base.txt vs regress-new-02a.txt/02b.txt cmp -s, regress_diff=0"
        status: pass
    human_judgment: false

duration: 약 25min
completed: 2026-09-18
status: complete
---

# Phase 79 Plan 02: 국부(핀 옆 띠) 기준선 — 기록·표시 Summary

**79-01 이 남긴 MeasurementBase.LastRefSource 를 cycle.json/일자별 CSV 끝 열에 기록하고 결과 그리드·리뷰어 '기준' 열에 표시 — 옵션 꺼짐·옛 파일은 빈칸/null 로 회귀 0.**

## Performance

- **Duration:** 약 25분 (base-79-02 기록 → Task 1 빌드·probe·커밋 → Task 2 빌드·probe·커밋)
- **Completed:** 2026-09-18T05:14:26Z
- **Tasks:** 2 (Task 1 기록, Task 2 표시)
- **Files modified:** 9 (MeasurementBase.cs, CycleResultDto.cs, CycleResultSerializer.cs, MeasurementHistoryCsvWriter.cs, MeasurementHistoryCsvLoader.cs, MeasurementResultRow.cs, MainView.xaml, ReviewMeasurementRow.cs, ReviewerWindow.xaml)

## Accomplishments

- `MeasurementBase.FormatRefSource`/`ParseRefSource` 단일 표시·파싱 규칙 추가(Phase 77 FormatSelectedZ/ParseSelectedZ 바로 다음 위치) — 국부="국부", 국부실패→전역="국부실패→전역", 그 외(null·빈 문자열·모르는 값·대소문자 다른 값)는 빈칸/null
- `MeasurementResultDto.RefSource` 필드 추가, `CycleResultSerializer.BuildDto` 가 `SelectedZIndex` 대입 바로 다음에 `LastRefSource` 를 복사 — 새 cycle.json 은 3값(null/Local/LocalFallback) 왕복, 옛 cycle.json 2개는 예외 없이 전부 null 로 역직렬화됨을 확인
- 일자별 CSV 헤더에 `사용기준` 열을 선택Z 뒤 맨 끝(인덱스16)에 추가, `MapRefSource` 로 값 기록. `COLUMN_COUNT` 는 14 그대로 유지하고 `COL_REF_SOURCE`(16)·`ParseRefSourceColumn` 은 칸이 있을 때만 읽어 옛 15·16칸 행이 손상 행 취급되지 않음을 확인
- `MeasurementResultRow.RefSourceText`(결과 그리드)·`ReviewMeasurementRow.RefSourceText`(리뷰어)가 같은 `MeasurementBase.FormatRefSource` 한 곳만 호출, `MainView.xaml`·`ReviewerWindow.xaml` 에 '기준' 열을 선택Z/사용 Z 바로 뒤에 추가(code-behind 무수정, CLAUDE.md MVVM 준수)
- 옵션 켠 측정 값 계산 코드는 한 줄도 바꾸지 않았음을 `OffRegressProbe`(편집 전 exe API 기준, 합성 6 + main-snapshot.ini EdgeToLineDistance 96섹션 × 실사진 2장 = 198경우)로 확인 — Task 1/Task 2 모두 `regress_diff=0`

## Task Commits

Each task was committed atomically:

1. **Task 1: 기록 — cycle.json RefSource + 일자별 CSV 끝 열 '사용기준' + FormatRefSource/ParseRefSource** - `0332bbc3` (feat)
2. **Task 2: 화면 — 결과 그리드·리뷰어 측정표에 '기준' 열** - `49e8c685` (feat)

**편집 전 기준(base-79-02):** `06ee8db23c0c09aa6342c1058bd68f895bb7c420` (docs(phase-79): update tracking after wave 1 (79-01 complete))

_TDD 프레임워크 없음 — 두 태스크 모두 probe(records/display) + OffRegressProbe 기반 automated verify. Task 1 frontmatter 는 tdd="true" 로 표기되어 있으나 실행은 계획서의 probe verify 절차를 그대로 따랐다(RED/GREEN 커밋 분리 없음) — 값 계산 코드 변경이 없는 순수 기록/삽입 작업이라 probe 선-실패/후-통과 확인으로 대체._

## Files Created/Modified

- `WPF_Example/Custom/Sequence/Inspection/MeasurementBase.cs` - `REF_SOURCE_LOCAL_TEXT`/`REF_SOURCE_FALLBACK_TEXT`, `FormatRefSource`, `ParseRefSource`
- `WPF_Example/UI/ViewModel/CycleResultDto.cs` - `MeasurementResultDto.RefSource` 필드
- `WPF_Example/Custom/Sequence/Inspection/CycleResultSerializer.cs` - `BuildDto` 에서 `measDto.RefSource = meas.LastRefSource;` 1줄
- `WPF_Example/Custom/Sequence/Inspection/MeasurementHistoryCsvWriter.cs` - `CSV_HEADER` 끝 `,사용기준` 추가(유일한 기존 줄 수정), `MapRefSource`, `BuildLine` 에 필드 추가
- `WPF_Example/Custom/Sequence/Inspection/MeasurementHistoryCsvLoader.cs` - `COL_REF_SOURCE`(16), `ParseRefSourceColumn`, `BuildMeasFromRow` 에서 호출
- `WPF_Example/UI/ViewModel/MeasurementResultRow.cs` - `RefSourceText` 프로퍼티, `Refresh()` 에 `RaisePropertyChanged` 추가
- `WPF_Example/UI/ContentItem/MainView.xaml` - 결과 DataGrid '기준' 열(선택Z 다음)
- `WPF_Example/UI/ViewModel/ReviewMeasurementRow.cs` - `RefSourceText` 프로퍼티, 3인자 생성자에서 대입
- `WPF_Example/UI/Reviewer/ReviewerWindow.xaml` - 측정표 '기준' 열(사용 Z 다음)

## Decisions Made

- 표시 문자열은 상수 2개(`REF_SOURCE_LOCAL_TEXT`="국부", `REF_SOURCE_FALLBACK_TEXT`="국부실패→전역") + `FormatRefSource`/`ParseRefSource` 두 메서드로만 구현하고, CSV writer/loader·두 VM 은 이 메서드만 호출한다(규칙 복제 없음) — Phase 77 SZF-04 `FormatSelectedZ`/`ParseSelectedZ` 선례 그대로 재사용
- CSV 신규 열은 항상 맨 끝(인덱스16)에만 추가, `COLUMN_COUNT` 는 14 유지 — 옛 15·16칸 파일이 "손상 행"으로 걸러지지 않도록 함(로더는 `fields.Count > COL_REF_SOURCE` 로만 존재 여부 판정)
- `MainView.xaml.cs`/`ReviewerWindow.xaml.cs` 는 이 plan 에서 전혀 수정하지 않음 — 표시 문자열은 VM 에서 만들고 XAML 은 바인딩 1줄만 추가(CLAUDE.md MVVM 하드룰), `codebehind=0` 으로 확인

## Deviations from Plan

None - 계획대로 실행. Task 1/Task 2 모두 probe·regress·grep 검증 1회에 통과.

## Issues Encountered

- 없음. (환경 메모, 79-01 인계사항과 동일) Git Bash 에서 probe 실행 시 출력/입력 경로 인자는 `C:/...` 형식(Windows 표기)으로 넘겨야 한다 — MSYS `/c/...` 경로는 예외를 던진다. 이번 실행도 전부 `$W`(Windows 표기) 변수를 사용해 문제 없었다.
- 콘솔 출력의 한글(국부/국부실패→전역)이 codepage 문제로 `����` 로 표시되지만, 비교는 `string.Equals(..., StringComparison.Ordinal)` 로 내부 UTF-16 값끼리 하므로 실제 판정에는 영향이 없다(79-01 SUMMARY 와 동일한 현상).

## Probe 출력 원문

### records (17사례, `records_fail=0`)

```
case=fmt_null actual=[-] expected=[-] result=PASS
case=fmt_empty actual=[-] expected=[-] result=PASS
case=fmt_local actual=[국부] expected=[국부] result=PASS
case=fmt_fallback actual=[국부실패→전역] expected=[국부실패→전역] result=PASS
case=fmt_lowercase actual=[-] expected=[-] result=PASS
case=fmt_unknown actual=[-] expected=[-] result=PASS
case=parse_local actual=[Local] expected=[Local] result=PASS
case=parse_fallback actual=[LocalFallback] expected=[LocalFallback] result=PASS
case=parse_empty actual=[-] expected=[-] result=PASS
case=parse_null actual=[-] expected=[-] result=PASS
case=parse_leadingspace actual=[-] expected=[-] result=PASS
case=parse_unknown actual=[-] expected=[-] result=PASS
case=json_roundtrip loadok=1 null=- local=Local fallback=LocalFallback result=PASS
case=json_old1 loadok=1 allnull=1 exceptions=0 result=PASS
case=json_old2 loadok=1 allnull=1 exceptions=0 result=PASS
case=csv_buildline columncount=1 lastfield=1 result=PASS
case=csv_query count=5 r0=- r1=Local r2=LocalFallback r3=- r4=- result=PASS
records_fail=0
```
(콘솔 원문의 국부/국부실패→전역 표시는 codepage 문제로 이 문서에서 한글로 보정했다 — 내부 비교는 UTF-16 그대로라 영향 없음. `json_old1`=`20260916/164530363_cycle/cycle.json`, `json_old2`=`20260601/163944_cycle/cycle.json`)

### display (6사례, `display_fail=0`)

```
case=display_review_null actual=[-] expected=[-] result=PASS
case=display_review_local actual=[국부] expected=[국부] result=PASS
case=display_review_fallback actual=[국부실패→전역] expected=[국부실패→전역] result=PASS
case=display_main_null actual=[-] expected=[-] result=PASS
case=display_main_local actual=[국부] expected=[국부] result=PASS
case=display_main_fallback actual=[국부실패→전역] expected=[국부실패→전역] result=PASS
display_fail=0
```

### regress (OffRegressProbe, 편집 전 exe API 기준)

- `regress-base.txt`(79-01 산출, total=198: 합성 6 + main-snapshot.ini `TypeName=EdgeToLineDistance` 96섹션 × 실사진 2장)
- Task 1 후: `regress-new-02a.txt` vs `regress-base.txt` → `regress_diff=0`
- Task 2 후: `regress-new-02b.txt` vs `regress-base.txt` → `regress_diff=0`

### 빌드·grep 검증 요약

- `msbuild_exit=0` (Task 1, Task 2 각 1회)
- Task 1: `fmt=4 dto=1 map=1`, `header=1 addfield=1 colconst=1 colcount14=1 parse=1`, 5개 파일 `ternary=0 coalesce=0 nullcond=0 switchexpr=0 datesig=0 logic3=0 qmark=0`, `MeasurementHistoryCsvWriter.cs deleted=1`(헤더 줄), 나머지 4개 `deleted=0`
- Task 2: `vm_main=1 raise=1 vm_review=1`, `col_main=1 col_review=1`, `codebehind=0`, `MeasurementResultRow.cs`·`ReviewMeasurementRow.cs` `deleted=0 ternary=0 coalesce=0 nullcond=0 switchexpr=0 datesig=0 qmark=0`, `MainView.xaml`·`ReviewerWindow.xaml` `deleted=0 added=1 qmark=0`

## New Symbols (Artifacts 표 79-02 행 대조)

| 심볼 | 파일 | 확인 |
|---|---|---|
| `REF_SOURCE_LOCAL_TEXT`("국부")/`REF_SOURCE_FALLBACK_TEXT`("국부실패→전역") | MeasurementBase.cs | O |
| `FormatRefSource(string)`/`ParseRefSource(string)` | MeasurementBase.cs | O |
| `MeasurementResultDto.RefSource` | CycleResultDto.cs | O |
| `measDto.RefSource = meas.LastRefSource;` | CycleResultSerializer.cs | O |
| CSV 헤더 끝 `,사용기준`, `MapRefSource` | MeasurementHistoryCsvWriter.cs | O |
| `COL_REF_SOURCE`(16), `ParseRefSourceColumn` | MeasurementHistoryCsvLoader.cs | O |
| `MeasurementResultRow.RefSourceText` | MeasurementResultRow.cs | O |
| `ReviewMeasurementRow.RefSourceText` | ReviewMeasurementRow.cs | O |
| DataGridTextColumn Header="기준" (선택Z 다음) | MainView.xaml | O |
| DataGridTextColumn Header="기준" (사용 Z 다음) | ReviewerWindow.xaml | O |
| LocalRefProbe `records`·`display` 모드 | LocalRefProbe.cs (probe, 저장소 밖) | O |

79-03(ROI 배선·주황 오버레이 색)·79-04(버전 감사)·79-05(realab UAT)는 이 plan 범위 밖.

## User Setup Required

None - 외부 서비스 설정 불필요.

## Next Phase Readiness

- 79-03(ROI 배선·오버레이 색)은 이 plan 이 만든 표시 경로와 독립 — `LOCAL_REF_ROI_SUBKEY`/`LOCAL_REF_OVERLAY_ROI_ID`(79-01 산출)를 그대로 이어받는다
- 79-04(버전 감사)는 이 plan 이 수정한 9개 파일을 포함해 회귀 점검 대상에 넣어야 한다
- 79-05(realab UAT)의 U-4(옵션 꺼짐 '기준' 칸 빈칸 확인, A-79-A4)는 이번 plan 의 `display_main_null`/`display_review_null` PASS 로 코드 경로가 이미 증명됨 — 실제 화면 확인만 남음
- cycle.json·CSV 하위호환은 옛 파일 2개(20260916/164530363, 20260601/163944)로 실측 확인됨 — 추가 마이그레이션 불필요

## Self-Check: PASSED

모든 수정 파일 9개 존재 확인, 커밋 해시 2개(`0332bbc3`, `49e8c685`) `git log --oneline --all` 에서 확인됨.

---
*Phase: 79-side-local-strip-datum*
*Completed: 2026-09-18*
