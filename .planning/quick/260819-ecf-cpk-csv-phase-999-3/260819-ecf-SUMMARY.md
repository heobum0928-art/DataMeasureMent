---
phase: quick-260819-ecf
plan: 01
subsystem: 통계/Export
tags: [cpk, csv, statistics, export, phase-999.3]
requires:
  - MeasurementHistoryCsvWriter 의 14열 CSV 포맷 (무변경)
  - CpkReportExportService Phase 72 시트 작성 로직 (무변경)
provides:
  - MeasurementHistoryCsvLoader.QueryCycles (CSV → List<CycleResultDto> 사이클 재조립)
  - CpkReportExportService.ExportCpkReport 4-인자 오버로드 (RAW 열 상한)
  - StatisticsWindow [CPK 리포트 export] 버튼
affects:
  - WPF_Example/UI/Statistics/StatisticsWindow.xaml(.cs)
tech-stack:
  added: []
  patterns: [순차 스트리밍 그룹핑, 위임 오버로드, 순수 추출 리팩토링]
key-files:
  created: []
  modified:
    - WPF_Example/Custom/Sequence/Inspection/MeasurementHistoryCsvLoader.cs
    - WPF_Example/Custom/Export/CpkReportExportService.cs
    - WPF_Example/UI/Statistics/StatisticsWindow.xaml
    - WPF_Example/UI/Statistics/StatisticsWindow.xaml.cs
decisions:
  - "사이클 경계는 시간만이 아니라 '측정키 재등장'까지 4조건으로 판정 — 같은 초 겹침이 실데이터에 14건 존재"
  - "RAW 열 상한은 표시에만 적용하고 Cpk 통계는 전체 cycles 로 계산 (D-2)"
  - "3-인자 ExportCpkReport 는 int.MaxValue 위임 wrapper 로 남겨 Phase 72 경로 출력 바이트 동일 유지"
metrics:
  duration: ~35m
  completed: 2026-08-19
---

# Quick 260819-ecf: 양산 CSV → CPK 엑셀 리포트 (Phase 999.3 D-1/D-2/D-3) Summary

통계분석 창의 조회 기간/레시피 그대로 일자별 CSV 이력을 사이클 단위로 재조립해 Phase 72 의 2장짜리 CPK 엑셀 리포트로 출력한다. 시트 작성 로직은 한 줄도 고치지 않았다.

## 커밋

| Task | 내용 | 커밋 |
|------|------|------|
| 1 | `QueryCycles` 사이클 재조립 (순수 추가) | `b9ad5da` |
| 2 | `ExportCpkReport` RAW 열 상한 오버로드 | `a88dbba` |
| 3 | 통계분석 창 [CPK 리포트 export] 버튼 배선 | `5902006` |

`git diff --name-only 56e0195..HEAD` = 정확히 4개 파일. `DatumMeasurement.csproj` / `Action_FAIMeasurement.cs` 는 끝까지 unstaged ` M` 로 보존(커밋 3회 모두 확인).

## ① 실데이터 재조립 검증 (정답지 대조)

`WPF_Example/bin/x64/Debug/Statistics/20260819.csv` (헤더 1 + 데이터 2379행, 레시피 전부 `FAI_1`)

| 항목 | 정답지 | plan 의 awk 참조 구현 | C# 로직 독립 포팅 시뮬레이터 | 판정 |
|------|--------|----------------------|------------------------------|------|
| 재조립 사이클 수 | 39 | **39** | **39** | PASS |
| distinct 측정키 | 61 | **61** | **61** | PASS |
| 사이클당 행 수 편차(≠61) | 0 | **0** | **0** | PASS |

추가로 시뮬레이터가 확인한 것 (D-3 근거):
- 사이클당 Shot 3개 / FAI 37개로 전 사이클 균일 (Shot·FAI get-or-add 가 사이클 경계를 넘지 않음)
- 자재번호 분포 = `1` 13 사이클 / `-1` 26 사이클 → RAW DATA 4행 열 그룹 라벨에 **`자재 1` 과 `미지정` 이 둘 다** 나타난다

시뮬레이터는 `ProcessCycleRow`/`IsNewCycleBoundary` 를 문장 단위로 옮긴 것으로, plan 의 awk 와 독립적으로 같은 값을 냈다. (첫 시도에 40/62/1 이 나온 것은 시뮬레이터가 CSV 의 UTF-8 BOM 때문에 헤더 행을 못 걸렀기 때문이며, C# 은 `File.ReadAllLines(path, Encoding.UTF8)` 이 BOM 을 벗겨 주므로 기존 `Query()` 와 동일하게 헤더가 정상 skip 된다 — 기존 운영 코드로 이미 검증된 경로.)

## ② awk 참조 구현 ↔ `IsNewCycleBoundary` 대응표

| awk 참조 구현 | C# `IsNewCycleBoundary` 의 `return true;` | 의미 |
|---------------|-------------------------------------------|------|
| `if (NR==1) newc=1;` (stmt1) | #1 `state.Current == null` | 첫 행 = 무조건 새 사이클 |
| `else if ($1!=pt \|\| $2!=pr \|\| $3!=pi) newc=1;` (stmt2) | #2 `szTime != LastTime \|\| szRecipe != LastRecipe` | 검사일시 또는 레시피 변경 |
| `else if ($1!=pt \|\| $2!=pr \|\| $3!=pi) newc=1;` (stmt2) | #3 `szIndex != LastIndex` | 자재번호 변경 |
| `else if (seen[key]) newc=1;` (stmt3) | #4 `SeenKeys.Contains(szKey)` | 측정키 재등장 = 같은 초 겹침의 마지막 방어선 |

awk 의 stmt2 는 3항 OR 이므로 C# 의 #2 + #3 두 분기와 합쳐 1:1 대응한다(awk 3개 분기 ↔ C# 4개 `return true;`). 평가 순서도 동일하며, C# 은 경계마다 `SeenKeys/ShotMap/FaiMap` 을 `Clear()` 해 awk 의 `delete seen` 과 같다.

**측정키 재등장 규칙이 필수인 이유:** 겹친 14건 중 `09:50:17/:19/:22/:25/:29` 5건은 두 사이클의 IndexNumber 가 둘 다 `-1` 이라 시간+자재번호만으로는 못 나눈다. 시간만으로 묶으면 39가 아닌 25 사이클이 되어 RAW DATA 열 14개가 조용히 소실된다.

## ③ 3-인자 경로 바이트 동일 근거

1. **시그니처 무변경** — `public static bool ExportCpkReport(List<CycleResultDto> cycles, string recipeName, string outputPath)` 가 그대로 존재하고, 본문은 `return ExportCpkReport(cycles, recipeName, outputPath, int.MaxValue);` 한 줄.
2. **잘림이 발생하지 않음** — `nMax = int.MaxValue` → `TakeRecentCycles` 의 `cycles.Count <= nMax` 가 항상 참 → **원본 List 참조를 그대로 반환**(새 List 생성 없음) → `BuildSampleColumns` 입력이 종전과 동일 객체.
3. **안내 셀이 안 써짐** — 그 결과 `columns.Count == cycles.Count` 이므로 `if (nTotalCycleCount > columns.Count)` 가 거짓 → `Cell(3,3)` 미기록. `WriteRawDataSheet` 의 그 외 본문은 무수정.
4. **시트 작성 헬퍼 무변경** — `git show HEAD` diff 에서 `WriteCpkSheet(` / `AppendChartBlock(` / `BuildRawRows(` / `PadRowTo(` 호출부 삭제 0건, 최종 파일에서 `WriteCpkSheet(wsCpk, rows, statDict);` / `AppendChartBlock(wsCpk, rows, statDict, seriesDict);` / `var rows = BuildRawRows(columns);` 각 1건 잔존.
5. **`wb.Worksheets.Add(` 2건 유지** = 시트 2장 고정.
6. `ReviewerWindow.xaml.cs` / `MeasurementHistoryCsvWriter` / `RepeatRunService` 는 3개 커밋 어디에도 없다.

## ④ 최근 N회 상한은 RAW 열에만 적용된다

- RAW 열: `BuildSampleColumns(TakeRecentCycles(cycles, nMaxRawColumns))` — 잘린 목록
- Cpk 통계: `foreach (var c in cycles) { stats.AddSample(c); }` — **잘리지 않은 원본** `cycles` (grep 로 해당 줄 1건 확인, 수정 없음)
- 따라서 열 수(최대 100)와 Cpk 시트의 N 이 달라 보일 수 있다. 그 사실을 `RAW DATA(1)` 시트 `C3` 셀에 `"전체 N회 중 최근 M회만 표시 (Cpk 통계는 전체 기준)"` 로 남긴다.
- **알려진 한계(설계상 수용):** Cpk 시트의 *행 목록*은 RAW 행(=잘린 최근 N회)에서 나오므로, 최근 N회에 한 번도 등장하지 않은 측정 항목은 Cpk 시트에도 행이 생기지 않는다. 행 식별/스펙 컬럼(도면항목·설계값·공차)이 RAW 행에서만 얻어지기 때문이다. 각 행의 *숫자*는 전체 기준이다. 이 절충을 `TakeRecentCycles` XML doc 에 명시했고 C3 안내로 사용자에게도 노출한다.

## 계획 대비 변경 (deviation)

**1. [Rule 2 - 누락된 필수 처리] `UpdateExportButtonState()` 를 `DoQuery` 의 catch 블록에도 호출**
- 발견: Task 3
- 이유: 조회 도중 예외가 나면 `m_lastResult` 가 갱신되지 않은 채 함수가 빠져나가 export 버튼이 **이전 조회 기준 활성 상태로 남는다**. 사용자가 그 상태로 export 를 누르면 화면과 무관한 데이터가 나온다.
- 조치: `catch` 블록 로깅 직후 `UpdateExportButtonState();` 1줄 추가.
- 영향: plan verify 의 `H` 기대값이 2 → **3** (선언 1 + try 1 + catch 1).
- 커밋: `5902006`

**2. [plan-checker 보정 반영]**
- `git status --porcelain` 판정을 `grep -c '^ M'` == 2 로 수행(plan 디렉터리 untracked 1줄은 정상).
- Task 2 의 diff 기반 `grep -cE '^-.*(WriteCpkSheet|...)'` 는 참고용으로만 쓰고, 최종 파일 상태 grep 으로 판정(위 ③-4).
- XAML 버튼은 `btn_Query` 의 닫는 `/>` 가 있는 **L27 다음, `</StackPanel>` 앞**에 삽입(삽입 결과 육안 확인 완료).

그 외는 plan 대로 실행했다.

## 검증 결과

| 항목 | 결과 |
|------|------|
| Task 1 정적 A~J | A=0(삭제 0줄) B=1/1 C=1 D=1 E=1 F=4 G=1 H=0 I=0, J=`cycles=39 distinct_meas_keys=61 cycles_rowcount_ne_61=0` |
| Task 2 정적 A~J | A=1 B=1 C=1 D=1 E=1/0 F=1 G=1 H=2 J=0, I 목록에 ReviewerWindow 없음 |
| Task 3 정적 A~K | A=1/1 B=1 C=1 D=1 E=1 F=1 G=1 **H=3(deviation 1)** I=0 J=0 K=1 |
| 빌드 (Debug/x64, scratch OutDir ×3회) | **Build succeeded** — 경고 CS0618×10 + CS0162×2 = baseline 12줄 정확히 일치, 신규 CS0219/CS0168/CS0177/CS0165 **0건** |
| 삼항 `?:` | 4개 파일 전부 0 (baseline 유지) |
| 신규 .cs / csproj 수정 | 0 / 무수정 |
| 워킹트리 보호 | `Action_FAIMeasurement.cs`, `DatumMeasurement.csproj` 끝까지 unstaged |

Task 1 빌드에서 `MSB3061`(obj\x64\Debug\DatumMeasurement.exe 잠김) 경고가 1건 났다. 실행 중인 앱 때문이며 컴파일·링크는 성공했다(프로세스 종료 금지 규칙 준수, Task 2/3 빌드에서는 재현되지 않음).

## 실행 UAT (사용자 확인 필요)

> ⚠ **export 전에 반드시 [조회] 를 먼저 누를 것.** export 버튼은 클릭 시점의 DatePicker/레시피 콤보를 다시 읽는다. 조회 없이 기간만 바꾸고 export 하면 화면 테이블과 엑셀 내용이 서로 다른 기간이 된다.

1. 앱 실행 → 통계분석 창 열기 → 기간 `2026-08-19 ~ 2026-08-19`, 레시피 `FAI_1` **조회**
2. [CPK 리포트 export] 클릭 → xlsx 저장
3. `RAW DATA(1)` 시트: 3행 `샘플 수` = **39**, C3 잘림 안내 **없음**(39 < 100), 데이터 행 = **61**, 샘플 열 = G 부터 39열, **4행에 `미지정` / `자재 1` 열 그룹 라벨**이 보인다 (D-3 충족)
4. `1Cav 세부치수_Cpk` 시트: 행 61개, 통계 숫자 채워짐, 시트는 **2장뿐**
5. 회귀: 리뷰어 창에서 폴더 반복검사 후 기존 [CPK 리포트 export] 클릭 → 이전과 동일하게 동작 (3행 `샘플 수` = 반복 횟수, C3 안내 없음)

## Self-Check: PASSED

- 수정 파일 4개 전부 존재 확인
- 커밋 `b9ad5da` / `a88dbba` / `5902006` 3건 `git log` 에서 확인
- `git diff --name-only 56e0195..HEAD` = 대상 4파일만
