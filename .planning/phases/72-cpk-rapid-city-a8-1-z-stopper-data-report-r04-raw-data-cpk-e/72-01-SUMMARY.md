---
phase: 72-cpk-rapid-city-a8-1-z-stopper-data-report-r04-raw-data-cpk-e
plan: 01
subsystem: inspection-statistics
tags: [cpk, statistics, export, repeat-measurement]
requires: []
provides:
  - "MeasurementStat.Cp / UCpk / LCpk / MinValue / MaxValue"
  - "RepeatMeasurementStats.GetSeries() — 측정키별 원시 측정값 복사본"
affects:
  - "WPF_Example/Custom/Sequence/Inspection/RepeatMeasurementStats.cs"
tech-stack:
  added: []
  patterns:
    - "지역변수 승격(promote-local-to-result) — 계산 후 버려지던 min/max/cpkUpper/cpkLower 를 DTO 필드로 노출"
    - "방어적 복사(defensive copy) — 내부 List<double> 을 복사해서 반환"
key-files:
  created: []
  modified:
    - "WPF_Example/Custom/Sequence/Inspection/RepeatMeasurementStats.cs"
decisions:
  - "stddev == 0 가드를 Cpk 와 분리하지 않고 동일 블록에서 Cp/UCpk/LCpk/Cpk 를 함께 PositiveInfinity 로 대입 (Pitfall 3 회피)"
  - "stddev == 0 비교를 엡실론 비교로 바꾸지 않음 — 기존 동작 보존(회귀 0) 우선"
  - "신규 필드는 기존 필드 사이가 아니라 Cpk 뒤에 append — 기존 object initializer 참조 코드 보호"
  - "GetSeries() 는 회차 인덱스를 보존하지 않음(DATUM_FAIL/NO_IMAGE 미누적) — RAW DATA 열 정렬 용도 금지를 XML 주석으로 명시"
metrics:
  duration: "약 6분"
  completed: "2026-08-18"
  tasks: 2
  files: 1
---

# Phase 72 Plan 01: RepeatMeasurementStats 확장 Summary

`MeasurementStat` 에 Cpk 상세 시트용 5개 통계 필드(Cp/UCpk/LCpk/MinValue/MaxValue)를 추가하고, 차트 렌더용 원시 측정값 접근자 `GetSeries()` 를 신설했다.

## What Was Built

### Task 1 — MeasurementStat 5필드 추가 + 지역변수 승격 (`ec9906a`)

`ComputeAll()` 이 계산해 놓고 버리던 값들을 결과 DTO 로 내보낸다.

- `MeasurementStat` 에 `Cp` / `UCpk` / `LCpk` / `MinValue` / `MaxValue` 추가. 기존 필드 순서는 건드리지 않고 `Cpk` 바로 뒤에 append 했다 — 다른 export 코드가 object initializer 로 참조 중이기 때문.
- `ComputeAll()` 내부 지역변수 `minVal` / `maxVal` / `cpkUpper` / `cpkLower` 를 `minValOut` / `maxValOut` / `ucpk` / `lcpk` 로 승격.
- `Cp = (LastTolPlus + |LastTolMinus|) / (6 * stddev)` 신규 계산.
- `stddev == 0` 가드 블록 안에서 Cp/UCpk/LCpk/Cpk 를 모두 `double.PositiveInfinity` 로 대입. 가드를 갈라놓지 않아 4개 값의 정의가 항상 일관된다.
- `usl` / `lsl` 선언과 `stddev == 0` 정확 비교는 그대로 유지 — 기존 Cpk 값 회귀 0.

### Task 2 — GetSeries() 원시값 접근자 (`cbf5512`)

- `public Dictionary<string, List<double>> GetSeries()` 신설. 측정키별로 내부 `KeyData.Values` 의 **복사본** `List<double>` 을 담아 반환한다.
- `ComputeAll()` 시그니처 무변경 — 기존 호출부(`RepeatExcelExportService.ExportInternal`) 영향 없음.
- XML 주석에 함정 명시: DATUM_FAIL / NO_IMAGE 회차는 애초에 누적되지 않으므로 이 리스트로는 "몇 번째 회차 값인지" 알 수 없다 → RAW DATA 열 정렬 용도 금지.

## Verification

| 항목 | 결과 |
|------|------|
| Task 1 acceptance grep (10건) | 전부 기대값 일치 |
| Task 2 acceptance grep (4건) | 전부 기대값 일치 |
| 삼항 연산자 `[^?]\? .+ : ` | 0 matches |
| msbuild Debug/x64 (Task 1 후) | exit 0, 에러 0, 경고 12줄 baseline |
| msbuild Debug/x64 (Task 2 후) | exit 0, 에러 0, 경고 12줄 baseline |
| `RepeatExcelExportService.cs` 무수정 | `git diff --name-only HEAD~2 HEAD` = 해당 1파일만 |
| 파일 삭제 없음 | `--diff-filter=D` 결과 없음 |

경고 12줄은 CS0618×10 + CS0162×2 로, 프로젝트의 기존 baseline 과 동일하다(신규 경고 0).

## Coding Rules 준수

- 삼항 연산자 미사용 (전부 if-else)
- 신규 지역변수는 파일 기존 스타일(무접두 `mean`/`stddev`/`cpk`)에 맞춰 헝가리언 접두 없이 작성 — 파일 내 스타일 혼용 방지
- Allman 브레이스, C# 7.2 문법만 사용 (switch expression / record / nullable reference type 없음)

## Deviations from Plan

None - plan executed exactly as written.

## Known Stubs

None.

## Self-Check: PASSED

- `WPF_Example/Custom/Sequence/Inspection/RepeatMeasurementStats.cs` — FOUND
- `.planning/phases/72-cpk-rapid-city-a8-1-z-stopper-data-report-r04-raw-data-cpk-e/72-01-SUMMARY.md` — FOUND
- commit `ec9906a` — FOUND
- commit `cbf5512` — FOUND

## Next Plan 참고사항

- **`stat.N > 0` 확인 필수:** `AddSample` 이 DATUM_FAIL/NO_IMAGE 만 있는 측정키에도 `KeyData` 를 만들기 때문에 `ComputeAll()` 결과에 N==0 엔트리가 남는다. 이 경우 Mean/StdDev/Cp/Cpk 는 전부 0 이다. `stat != null` 만으로 판단하면 안 된다.
- **PositiveInfinity 렌더:** stddev==0 이면 Cp/UCpk/LCpk/Cpk 가 `double.PositiveInfinity` 다. 엑셀 셀에 그대로 쓰면 `∞` 또는 오류가 되므로 소비 측에서 표기 규칙을 정해야 한다.
