---
phase: 72-cpk-rapid-city-a8-1-z-stopper-data-report-r04-raw-data-cpk-e
plan: 05
subsystem: cpk-report-export
tags: [cpk, export, closedxml, raw-data, pivot]
requires:
  - "72-01 (MeasurementStat 확장 — 이번 plan 은 코드 의존 없음, 72-06 조인 계약만 공유)"
  - "72-04 (빌드 직렬화 선행 조건)"
  - "CycleResultDto / ShotResultDto / FaiResultDto / MeasurementResultDto"
provides:
  - "CpkReportExportService.ExportCpkReport(cycles, recipeName, outputPath)"
  - "RAW DATA(1) 가로형 매트릭스 시트 (1행=측정항목, 1열=검사 회차)"
  - "SampleColumn / RawRow 내부 pivot 모델 (72-06 이 재사용)"
affects:
  - "WPF_Example/Custom/Export/CpkReportExportService.cs"
  - "WPF_Example/DatumMeasurement.csproj"
tech-stack:
  added: []
  patterns:
    - "index-aligned pivot — cycle 인덱스 기준으로 열을 만들고 PadRowTo 로 뒤늦게 등장한 측정키의 앞쪽 열을 메워 열 밀림 방지"
    - "stable secondary sort — OrderBy(IndexNumber).ThenBy(원래 인덱스) 로 자재 구간을 만들되 자재 내 회차 순서 보존"
    - "sentinel constant — MATERIAL_NOT_SET(-1) 비교로 미지정 라벨 분기 (인라인 -1 금지)"
    - "presence flag over value — 0.0 도 정상값이므로 LastHasResult 로만 미측정 판별 (CO-23-01)"
key-files:
  created:
    - "WPF_Example/Custom/Export/CpkReportExportService.cs"
  modified:
    - "WPF_Example/DatumMeasurement.csproj"
decisions:
  - "열 축 = 검사 1회차 (자재번호는 4행 열 그룹 라벨). D-03 의 100회+ 반복 데이터를 표현하려면 회차가 열 축이어야 하고, 자재 오름차순 정렬로 같은 자재 회차가 인접 구간에 모여 D-04/D-05 목적도 동시 충족"
  - "RepeatMeasurementStats.GetSeries() 를 RAW DATA 열 정렬에 쓰지 않고 BuildRawRows 로 직접 pivot — GetSeries 는 DATUM_FAIL/NO_IMAGE 회차를 누락시켜 회차 인덱스가 밀린다"
  - "측정키 문자열을 RepeatMeasurementStats.AddSample 과 글자 그대로 동일하게 구성 — 72-06 통계 조인이 이 문자열 동일성에 의존"
  - "공차/설계값은 마지막으로 관측된 cycle 값으로 갱신(최신 레시피 우선) — RepeatMeasurementStats 와 동일 정책"
  - "미측정 칸은 0 이 아니라 문자열 \"-\" — 0 을 쓰면 통계/그래프에서 실측값과 구분 불가"
  - "MATERIAL_NOT_SET 비교를 `>= 0` 대신 `== MATERIAL_NOT_SET` 로 작성 — 상수가 실제로 참조되어 죽은 상수가 되지 않게 함"
metrics:
  duration: "약 8분"
  completed: "2026-08-18"
  tasks: 2
  files: 2
---

# Phase 72 Plan 05: CpkReportExportService — RAW DATA(1) 가로형 시트 Summary

고객 참고양식 대조용 CPK 리포트 export 서비스를 신설하고, 기존 세로형(1행 = 1측정×1회차) 포맷으로는 표현 불가능한 `RAW DATA(1)` 가로형 매트릭스(1행 = 측정 항목, 1열 = 검사 회차)를 구현했다.

## What Was Built

### Task 1 — 서비스 골격 + pivot 빌더 + csproj 등록 (`bc0566b`)

`WPF_Example/Custom/Export/CpkReportExportService.cs` 신규 생성 (`namespace ReringProject.Export`, `public static class`).

- **상수 8종**: `RAW_SHEET_NAME = "RAW DATA(1)"`, 레이아웃 좌표 4종(`RAW_MATERIAL_ROW`/`RAW_HEADER_ROW`/`RAW_FIRST_DATA_ROW`/`RAW_FIRST_SAMPLE_COLUMN`), 표기 상수 3종(`NO_VALUE_TEXT`/`MATERIAL_UNSET_LABEL`/`MATERIAL_NOT_SET`). 시트 좌표 매직넘버 인라인 0건.
- **내부 모델 2개** (`SampleColumn`, `RawRow`) — `RepeatExcelExportService.AlgoAggData` 관례대로 프로퍼티가 아닌 public 필드.
- **`ExportCpkReport`** 진입점: 가드 3종 → `BuildSampleColumns` → `BuildRawRows` → `using (XLWorkbook)` → `Worksheets.Add(RAW_SHEET_NAME)` 1회 → `SaveAs` → `true`. 예외는 `catch (Exception ex)` 안에서 `Logging.PrintErrLog` 를 다시 bare `catch { }` 로 감싸고 `false` 반환 (throw 금지, 기존 export 관례 동일).
- **`BuildSampleColumns`**: cycle 1개 = 열 1개. `OrderBy(IndexNumber).ThenBy(입력 인덱스)` 로 자재 오름차순 + 자재 내 원래 순서 유지. `GroupBy` 로 시트를 나누는 폐기 설계(RESEARCH Pattern 4)는 도입하지 않았다.
- **`BuildRawRows`**: 4중 루프 + 레벨별 `continue` 널 가드. 키는 `(shot.ShotName ?? "") + "/" + (fai.FAIName ?? "") + "/" + (m.MeasurementName ?? "")` — `RepeatMeasurementStats.AddSample` 과 글자 단위로 동일.
- **`PadRowTo`**: 열 처리 직전과 마지막에 각 행을 목표 길이까지 "값 없음"으로 패딩. 나중 회차에 처음 등장한 측정키가 앞쪽 열에 밀려 들어가는 것을 막는다.

`DatumMeasurement.csproj` 에 `<Compile Include="Custom\Export\CpkReportExportService.cs" />` 를 `ChartImageCapture` 뒤 / `ExcelExportSmokeTest` 앞에 알파벳 순 삽입.

### Task 2 — WriteRawDataSheet 시트 기록 (`9da6cec`)

| 위치 | 내용 |
|---|---|
| A1~A3 / B1~B3 | 모델명 / 측정일시 / 샘플 수 |
| 4행 G~ | 자재 라벨 (`자재 3` 또는 `미지정`) |
| 5행 A~F | Number / 도면항목설명 / 측정방식 / 설계값 / 상한 공차 / 하한 공차 |
| 5행 G~ | `#1`, `#2`, ... |
| 6행~ | FAIName / MeasurementName / TypeName / Nominal / Tol+ / Tol- / 측정값 또는 `-` |

- 측정값은 `Math.Round(x, 6)`, 미측정은 `NO_VALUE_TEXT`("-"). 판별은 `HasValues[i]` (값이 아니라 플래그).
- 헤더행 Bold + `FreezeRows(RAW_HEADER_ROW)` + `FreezeColumns(RAW_FIRST_SAMPLE_COLUMN - 1)` + `Columns().AdjustToContents()`.
- 셀 기록은 전부 `.Value` 고정값 — `FormulaA1` 0건 (D-02 준수).

## Verification

| 항목 | 결과 |
|------|------|
| Task 1 acceptance grep (10건) | 전부 기대값 일치 → `TASK1_OK` |
| Task 2 acceptance grep (8건) | 전부 기대값 일치 |
| `Worksheets.Add` 호출 수 | 1 (RAW 1장만 — 72-06 이 2번째 추가 예정) |
| 금지 시트명(`검사성적서`/`2Cav`/`RAW DATA(2)`/`안내사항`) | 0건 |
| `FormulaA1` | 0건 |
| 삼항 `[^?]\? .+ : ` | 0 matches |
| msbuild Debug/x64 (scratch OutDir) | exit 0, CS 에러 0 |
| 빌드 경고 | 12줄 (CS0618×10 + CS0162×2) = baseline, 신규 경고 0 |
| csproj diff 범위 | `<Compile Include>` 1줄 추가만 — `OutputPath`/`DefineConstants` 변경 없음 |
| 파일 삭제 | 두 커밋 모두 `--diff-filter=D` 결과 없음 |

### must_haves 대응

| Truth | 근거 |
|-------|------|
| `RAW DATA(1)` 시트 정확히 1장 | `Worksheets.Add` grep == 1, 시트명은 `private const` |
| 1행 = FAI 측정 항목, 열 = 샘플 매트릭스 | `WriteRawDataSheet` 의 `foreach (var row in rows)` × `for (i < columns.Count)` 2중 구조 |
| 자재번호 오름차순 정렬 + 4행 자재 라벨 | `OrderBy(p => p.Value.IndexNumber).ThenBy(p => p.Key)`, `ws.Cell(RAW_MATERIAL_ROW, nCol).Value = MaterialLabel` |
| 미측정 칸 `-` 표기 | `ws.Cell(nRow, nCol).Value = NO_VALUE_TEXT;` (else 분기) |
| 금지 시트 4종 미생성 | grep 0건, `Worksheets.Add` 1회 |

## Coding Rules 준수

- 삼항 연산자 0건 (전부 if-else) — 특히 `cycle.IndexNumber >= 0 ? ... : ...` 형태는 도입하지 않았다
- 헝가리언: `nCol` / `nRow` / `nTargetCount` / `bHas` / `szMaterial`
- Allman 브레이스 (신규 파일 전체 통일)
- C# 7.2 문법만 사용 — switch expression / record / nullable reference type 없음, `out row` 는 `TryGetValue` 사전 선언 변수
- 신규 .cs 파일 csproj 수동 등록 완료
- 엑셀 셀은 `.Value` 고정값만 (D-02)

## Threat Model 대응

- **T-72-07 (Tampering, 시트명 injection)** — mitigate 적용됨. 시트명은 `private const string RAW_SHEET_NAME` 하드코딩뿐이고 사용자 입력(recipeName / IndexNumber)은 셀 값으로만 흐른다.
- **T-72-08 (DoS, 열 폭증)** — accept 그대로. 열 상한 초과 시 ClosedXML 예외를 `ExportCpkReport` 의 try/catch 가 흡수해 `false` 반환하고 앱은 유지된다.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 2 - 규칙 준수] `MATERIAL_NOT_SET` 을 실제 참조하도록 조건식 변경**
- **Found during:** Task 1
- **Issue:** plan 예시 코드는 `if (cycle.IndexNumber >= 0)` 로 분기해 `MATERIAL_NOT_SET` 상수를 어디서도 참조하지 않았다. 그대로 두면 plan-checker 가 지적한 대로 죽은 상수(그리고 CS0414 유발 가능성)가 된다.
- **Fix:** `if (cycle.IndexNumber == MATERIAL_NOT_SET) { szMaterial = MATERIAL_UNSET_LABEL; } else { szMaterial = "자재 " + cycle.IndexNumber; }` 로 if/else 분기를 뒤집어 상수를 실제로 사용. 동작은 `-1` 만이 미지정이라는 DTO 계약상 동일하다.
- **Files modified:** `WPF_Example/Custom/Export/CpkReportExportService.cs`
- **Commit:** `bc0566b`

### 검증 절차 조정 (코드 변경 아님)

plan 의 acceptance grep 중 `grep -c 'Custom\\Export\\CpkReportExportService.cs' WPF_Example/DatumMeasurement.csproj` 가 Git Bash(MSYS)의 인자 경로 변환 때문에 항상 0 을 반환한다. 실제 파일에는 해당 줄이 존재하며 `grep -cF 'Custom\Export\CpkReportExportService.cs'` 로는 1 이 나온다. 검증만 `-F` 로 바꿔 수행했고 소스/csproj 내용은 plan 그대로다. 72-06 이후 plan 에서 같은 grep 을 쓸 경우 동일 조정이 필요하다.

## Known Stubs

없음. 단, `ExportCpkReport` 는 아직 **어떤 호출자도 없다** — UI 연결(Export 버튼)은 이 plan 범위 밖이며 후속 plan 소관이다. 두 번째 시트(`1Cav 세부치수_Cpk`)도 72-06 이 `ExportCpkReport` 안에 추가한다. 둘 다 plan 이 명시한 의도된 미완 지점이다.

## Threat Flags

없음 — 신규 네트워크 엔드포인트/인증 경로/스키마 변경 없음. 파일 쓰기는 상위 호출자가 정한 `outputPath` 1건뿐이고 threat register 에 이미 등재돼 있다.

## Self-Check: PASSED

- `WPF_Example/Custom/Export/CpkReportExportService.cs` — FOUND
- `WPF_Example/DatumMeasurement.csproj` — FOUND
- commit `bc0566b` — FOUND
- commit `9da6cec` — FOUND

## 다음 plan(72-06) 참고사항

- `Worksheets.Add` 는 현재 1회다. 72-06 은 `ExportCpkReport` 안 `wsRaw` 기록 직후에 2번째 시트를 추가하면 되고, 그 시점에 grep 기대값이 2 로 바뀐다.
- `SampleColumn` / `RawRow` / `PadRowTo` 는 `private` 이지만 같은 클래스 안이라 72-06 이 그대로 재사용할 수 있다. 통계 시트가 RAW 와 같은 행 순서를 쓰려면 `BuildRawRows` 결과 리스트를 넘겨 받는 편이 안전하다.
- 통계 조인 키는 `RawRow.Key` 이며 `RepeatMeasurementStats.ComputeAll()` 의 딕셔너리 키와 동일 규칙이다. 조인 시 `stat.N > 0` 확인 필수(72-01 경고).
- `stddev == 0` 인 측정키는 Cp/UCpk/LCpk/Cpk 가 `double.PositiveInfinity` 다. 셀에 그대로 쓰면 안 되므로 72-06 에서 표기 규칙(예: `NO_VALUE_TEXT` 재사용)을 정해야 한다.
