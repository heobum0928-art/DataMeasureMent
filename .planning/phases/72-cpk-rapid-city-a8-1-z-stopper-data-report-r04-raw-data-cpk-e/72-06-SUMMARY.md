---
phase: 72-cpk-rapid-city-a8-1-z-stopper-data-report-r04-raw-data-cpk-e
plan: 06
subsystem: cpk-report-export
tags: [cpk, export, closedxml, statistics, judgement]
requires:
  - "72-01 (MeasurementStat.Cp/UCpk/LCpk/MinValue/MaxValue)"
  - "72-05 (CpkReportExportService 골격 + RawRow pivot 모델)"
provides:
  - "CpkReportExportService.WriteCpkSheet — '1Cav 세부치수_Cpk' 통계 시트"
  - "WriteStatCell / BuildCpkJudgement / BuildToleranceTypeText 헬퍼"
  - "ExportCpkReport 가 시트 2장(RAW DATA(1) + 1Cav 세부치수_Cpk) 워크북을 저장"
affects:
  - "WPF_Example/Custom/Export/CpkReportExportService.cs"
tech-stack:
  added: []
  patterns:
    - "infinity-to-text — StdDev==0 으로 PositiveInfinity 가 된 지표는 '∞' 텍스트로 치환(StatisticsWindow.CpkToText 미러)"
    - "presence-with-count guard — stat != null 이 아니라 stat.N > 0 으로 표본 유무 판별(N==0 엔트리 0 클로버 방지)"
    - "row-order reuse — RAW DATA 시트와 동일한 BuildRawRows 결과 리스트를 통계 시트에도 그대로 사용해 두 시트 행 순서 일치"
key-files:
  created: []
  modified:
    - "WPF_Example/Custom/Export/CpkReportExportService.cs"
decisions:
  - "USL/LSL 산식을 RepeatMeasurementStats.ComputeAll(cs:188-189)과 글자 그대로 동일하게 작성 — 통계 시트의 명시 컬럼과 Cpk 내부 계산이 어긋나지 않게 함"
  - "판정은 NG > Cpk경고(1.33) > OK 3단계 단일 규칙. 참고 원본 엑셀의 IF 참/거짓 동일값 오류는 재현하지 않음"
  - "Cpk == PositiveInfinity 는 < 1.33 이 아니므로 자동으로 'O K' — 의도된 동작(산포 0 = 최고 능력)"
  - "E열(Datum 유형)/Q열(#1 Target Std Dev)은 DTO/시스템 미보유 항목이라 항상 '-' 로 두되 참고양식 열 배치를 유지"
  - "N==0(DATUM_FAIL/NO_IMAGE 만 있는 측정키) 항목은 통계 9칸을 루프로 일괄 '-' 처리 — 판정 열과 표기 일관"
metrics:
  duration: "약 9분"
  completed: "2026-08-18"
  tasks: 2
  files: 1
---

# Phase 72 Plan 06: 1Cav 세부치수_Cpk 통계 시트 Summary

`CpkReportExportService` 에 두 번째(그리고 마지막) 시트인 `1Cav 세부치수_Cpk` 를 구현해, Phase 51 에서 제거됐던 Cpk export 를 복원하고 Cp/UCPK/LCPK/USL/LSL 명시 컬럼과 3단계 판정·상단 요약을 추가했다.

## What Was Built

### Task 1 — 표시/판정 헬퍼 3개 + 레이아웃 상수 (`affd046`)

- 상수 추가: `CPK_SHEET_NAME = "1Cav 세부치수_Cpk"`, `CPK_WARN_THRESHOLD = 1.33`, 행 좌표 5종(`CPK_SUMMARY_OK_ROW`/`CPK_SUMMARY_NG_ROW`/`CPK_SUMMARY_LIST_ROW`/`CPK_HEADER_ROW`/`CPK_FIRST_DATA_ROW`), 열 좌표 3종(`CPK_LEFT_FIRST_COLUMN=2`/`CPK_RIGHT_FIRST_COLUMN=14`/`CPK_JUDGE_COLUMN=24`), 판정 라벨 3종(`N G`/`Cpk`/`O K`), `INFINITY_TEXT = "∞"`, `STAT_DECIMALS = 6`.
- `WriteStatCell`: `PositiveInfinity → "∞"`, `NegativeInfinity`/`NaN → "-"`, 그 외 `Math.Round(x, 6)`. raw `double.PositiveInfinity` 를 셀에 넣어 파일이 깨지는 것을 막는다.
- `BuildCpkJudgement`: `stat == null || stat.N <= 0` → `"-"`, `Min < LSL || Max > USL` → `"N G"`, `Cpk < 1.33` → `"Cpk"`, 그 외 `"O K"`.
- `BuildToleranceTypeText`: 상/하한 공차 존재 조합으로 `양측`/`상한`/`하한`/`없음`.

### Task 2 — WriteCpkSheet + ExportCpkReport 연결 (`3bd6943`)

| 위치 | 내용 |
|---|---|
| A1/B1, A2/B2, A3/B3 | `OK / Total`, `NG / Total`, `NG FAI# 항목`(중복 제거 `, ` 연결, 없으면 `-`) |
| 5행 B~L | SPC / FAI# / 측정 방식 / Datum 유형 / 공차 유형 / 기준 치수 / + 공차 / - 공차 / 검사 방법 / USL / LSL |
| 5행 N~V | Maximum / Minimum / Mean / #1 Target Std Dev / Std Dev / Cp / UCPK / LCPK / Cpk |
| 5행 X | Judgment |
| 6행~ | RAW DATA 시트와 **동일 행 순서**의 측정 항목별 데이터 |

- 조인: `statDict.TryGetValue(row.Key, out stat)` — `RawRow.Key` 가 `RepeatMeasurementStats` 딕셔너리 키와 동일 규칙.
- `bool bHasStat = stat != null && stat.N > 0;` — false 면 N~V 9칸을 루프로 전부 `"-"`.
- `ExportCpkReport` 안에서 `RepeatMeasurementStats` 를 새로 만들어 `AddSample` 누적 후 `ComputeAll()`, `wb.Worksheets.Add(CPK_SHEET_NAME)` → `WriteCpkSheet`. `Worksheets.Add` 총 2회.
- 헤더행 Bold + `FreezeRows(CPK_HEADER_ROW)` + `Columns().AdjustToContents()`.
- 셀 기록은 전부 `.Value` 고정값 — `FormulaA1` 0건 (D-02).

## Verification

| 항목 | 결과 |
|------|------|
| Task 1 acceptance (9건) | 전부 기대값 일치 |
| Task 2 acceptance (13건) | 전부 기대값 일치 |
| `Worksheets.Add` 호출 수 | **2** (D-04 시트 2장 고정 준수) |
| 금지 시트명 4종(코드+주석) | **0건** |
| `FormulaA1` | 0건 |
| 삼항 `[^?]\? .+ : ` | 0 matches |
| msbuild Debug/x64 (scratch OutDir, Task 1 후) | exit 0, 에러 0 |
| msbuild Debug/x64 (scratch OutDir, Task 2 후) | exit 0, **에러 0, 경고 12줄 = baseline**(CS0618×10 + CS0162×2, 신규 0) |
| USL/LSL 산식 육안 대조 | `RepeatMeasurementStats.cs:188-189` (`LastNominal + LastTolPlus` / `LastNominal - Math.Abs(LastTolMinus)`)와 구조 동일 |
| 파일 삭제 | 두 커밋 모두 `--diff-filter=D` 결과 없음 |
| 스테이징 범위 | 두 커밋 모두 `CpkReportExportService.cs` 1파일 (`1 file changed`) |

### must_haves 대응

| Truth | 근거 |
|-------|------|
| `1Cav 세부치수_Cpk` 시트 1장 추가(총 2장) | `Worksheets.Add` grep == 2, 시트명은 `private const` |
| Cp/UCPK/LCPK/Cpk 참고양식 산식대로 기록 | `stat.Cp`/`UCpk`/`LCpk`/`Cpk`(72-01 이 `ComputeAll` 에서 계산)를 N~V 열에 기록 |
| USL/LSL 명시 컬럼 | K(11)/L(12) 열, `Nominal ± 공차` C# 계산 후 `.Value` |
| 판정 3단계 일관 적용 | `BuildCpkJudgement` 단일 함수 — 원본 엑셀의 IF 오류 미재현 |
| 상단 OK/NG/NG 목록 요약 | A1~B3 |
| 무한대는 `∞` 텍스트 | `WriteStatCell` 의 `double.IsPositiveInfinity` 분기 |
| N==0 항목 통계 9칸 `-` | `bHasStat` else 분기 `for (nCol = 14; nCol <= 22)` |

## Coding Rules 준수

- 삼항 연산자 0건 (전부 if-else)
- 헝가리언: `nRow`/`nSpc`/`nOk`/`nNg`/`nTotal`/`nCol`/`dUsl`/`dLsl`/`dValue`/`szJudge`/`bHasStat`/`bHasUpper`
- Allman 브레이스 (파일 전체 통일 유지)
- C# 7.2 문법만 — switch expression / record / nullable reference type 없음, `out stat` 는 사전 선언
- 엑셀 셀은 `.Value` 고정값만 (D-02)
- 신규 파일 없음 → csproj 무수정 (72-05 에서 이미 등록)

## Threat Model 대응

- **T-72-09 (Tampering, 셀 수식 injection)** — mitigate 적용됨. FAI 명/측정명은 `.Value` 문자열로만 기록되고 `FormulaA1` 사용 0건이라 `=`/`+` 로 시작하는 이름도 수식으로 평가되지 않는다.
- **T-72-10 (Information Disclosure)** — accept 그대로. 기존 export 와 동일 노출 수준.

## Deviations from Plan

None - plan executed exactly as written.

### 검증 절차 조정 (코드 변경 아님)

acceptance grep 중 특수문자(`[`, `#`, `(`)를 포함한 패턴은 Git Bash 에서 오탐/거짓 0 을 피하려고 `-F`(고정 문자열)로 실행했다. 72-05 가 기록한 MSYS 경로 변환 함정과 같은 계열의 검증 조정이며, 소스 내용은 plan 그대로다.

## Known Stubs

- E열 `Datum 유형`, Q열 `#1 Target Std Dev` 는 **항상 `-`** 다. 각각 `CycleResultDto` 에 datum 유형 필드가 없고, `#1 Target Std Dev` 는 참고양식 고유의 목표치라 우리 시스템이 보유하지 않는다. plan 이 명시한 의도된 양식 유지용 열이며 72-07 UAT 체크리스트에도 "두 열이 전부 `-` 인지" 확인 항목으로 남아 있다.
- `ExportCpkReport` 는 **여전히 호출자가 없다**. UI Export 버튼 연결은 72-07 소관(72-05 부터 이어진 의도된 미완).

## Threat Flags

없음 — 신규 네트워크 엔드포인트/인증 경로/스키마 변경 없음. 파일 쓰기는 상위 호출자가 정한 `outputPath` 1건뿐이고 threat register 에 이미 등재돼 있다.

## Self-Check: PASSED

- `WPF_Example/Custom/Export/CpkReportExportService.cs` — FOUND
- `.planning/phases/72-.../72-06-SUMMARY.md` — FOUND
- commit `affd046` — FOUND
- commit `3bd6943` — FOUND

## 다음 plan(72-07) 참고사항

- 워크북은 이제 시트 2장으로 완성됐다. 72-07 이 시트를 더 추가하면 D-04 위반이다 — 차트/이미지는 기존 2장 안에 배치할 것.
- `ExportCpkReport(cycles, recipeName, outputPath)` 를 UI 버튼에서 호출하면 되고, 반환은 `bool`(실패 시 false + 에러 로그, throw 없음)이다.
- 72-04 의 `ChartImageCapture` 오프스크린 캡처는 실패해도 예외 없이 **백지 PNG** 가 나온다 — UAT 최초에 리뷰어 창 "차트 이미지 캡처 점검" 버튼부터 눌러 확인할 것.
