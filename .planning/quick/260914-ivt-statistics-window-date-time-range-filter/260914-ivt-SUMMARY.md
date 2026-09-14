---
phase: quick-260914-ivt
plan: 01
subsystem: ui
tags: [wpf, statistics, csv, datetime-filter, mvvm]

requires: []
provides:
  - "StatisticsTimeRange 값 객체(날짜+시:분 기간, 반열린 구간 [FromInclusive, ToExclusive))"
  - "MeasurementHistoryCsvLoader.Query/QueryCycles 기간 오버로드 + QueryDirectory/QueryCyclesDirectory(경로 주입)"
  - "RepeatMeasurementStats.MeasurementStat.RecordCount(항목별 기록 틱 수)"
  - "StatisticsWindow: 시/분 ComboBox 기간 선택, 결과 없음(보라) 행 상태, 기록(틱) 칸, 요약 확장"
  - "SavedCycleRerunPlanner.BuildPlan 기간(StatisticsTimeRange) 오버로드"
affects: [statistics-window, saved-cycle-rerun, cpk-export]

tech-stack:
  added: []
  patterns:
    - "값 객체(StatisticsTimeRange)로 날짜+시:분 기간을 캡슐화하고 Query/QueryCycles/BuildPlan 세 경로가 같은 객체를 공유"
    - "경로 주입 오버로드(QueryDirectory/QueryCyclesDirectory)로 SystemHandler 의존을 제거해 스크래치 하네스에서 실데이터로 검증 가능하게 함"

key-files:
  created: []
  modified:
    - WPF_Example/Custom/Sequence/Inspection/MeasurementHistoryCsvLoader.cs
    - WPF_Example/Custom/Sequence/Inspection/RepeatMeasurementStats.cs
    - WPF_Example/Custom/Sequence/Inspection/RepeatRunService.cs
    - WPF_Example/UI/Statistics/StatisticsWindow.xaml
    - WPF_Example/UI/Statistics/StatisticsWindow.xaml.cs

key-decisions:
  - "부품 수 칸은 넣지 않음 — CSV 에 z 열이 없어 부품 경계를 신뢰성 있게 셀 수 없음(근거는 본문 표 참고)"
  - "결과 없음 행의 평균/표준편차/범위는 0.0000 표시가 유지된다(double 바인딩, 이번 범위 밖)"
  - "레시피 콤보 목록은 기간 안 줄 기준"
  - "재검사 결과 export 파일명은 재검사를 시작한 기간을 따른다(화면 시각 변경 무관)"
  - "XAML 에는 삼항 grep 을 적용하지 않는다(C# 조건 연산자 규칙 대상 아님)"

requirements-completed: [R1-period-date-and-time, R2-row-level-time-filter, R3-same-period-rerun-and-export, R4-data-presence-at-a-glance]

metrics:
  duration: "커밋 구간 8분(14:11~14:18, 세션 전체 계획 확인·편집·검증 시간은 별도 기록 없음)"
  completed: 2026-09-14
status: complete
---

# Quick Task 260914-ivt: 통계 창 기간을 날짜+시:분으로 Summary

**통계 창 조회 기간을 날짜+시:분(기본 00:00~23:59)으로 넓히고, 행 단위 검사일시로 필터하며, 같은 기간을 저장 사진 재검사·CPK export 에도 적용. 결과 없음 항목은 사라지지 않고 보라색으로 표시되며 기록(틱) 칸과 확장 요약으로 "데이터가 실제로 나왔는지"를 한눈에 보여준다.**

## 실행 정보

- BASE 커밋: `93c800becce230f50d3f765739d316fdc2420364`
- 완료: 2026-09-14
- 태스크 3개, 코드 커밋 3개(모두 `main`)

## Task Commits

1. **Task 1 (tracer): 기간 값 객체 + 로더 행 시각 필터 + 기간 ViewModel/시·분 ComboBox + DoQuery 배선** — `25da3d94` (feat)
   - `WPF_Example/Custom/Sequence/Inspection/MeasurementHistoryCsvLoader.cs`, `WPF_Example/UI/Statistics/StatisticsWindow.xaml.cs`, `WPF_Example/UI/Statistics/StatisticsWindow.xaml`
2. **Task 2: "데이터가 나왔나" 한눈에 — RecordCount, 기간 밖 항목 보존, 결과 없음 상태(보라)·정렬·필터, 기록(틱) 칸·헤더 툴팁, 요약 개수** — `8268db30` (feat)
   - `WPF_Example/Custom/Sequence/Inspection/RepeatMeasurementStats.cs`, `WPF_Example/Custom/Sequence/Inspection/MeasurementHistoryCsvLoader.cs`, `WPF_Example/UI/Statistics/StatisticsWindow.xaml.cs`, `WPF_Example/UI/Statistics/StatisticsWindow.xaml`
3. **Task 3: 같은 기간을 저장 사진 재검사·CPK export 에 적용 — QueryCycles 행 필터, BuildPlan InspectionTime 필터, export 파일명 시각 포함** — `f923c7e5` (feat)
   - `WPF_Example/Custom/Sequence/Inspection/MeasurementHistoryCsvLoader.cs`, `WPF_Example/Custom/Sequence/Inspection/RepeatRunService.cs`, `WPF_Example/UI/Statistics/StatisticsWindow.xaml.cs`

## 지표 정의와 근거 (R4)

| 표시 | 정의 | 근거 / 오해 방지 |
|------|------|------------------|
| N (기존) | 기간 안 OK+NG 줄 수 = 측정값이 나온 횟수 | 정의 불변. 헤더 툴팁만 추가 |
| 기록(틱) (신규) | 기간 안 그 항목 줄 수(자동·레시피 필터 통과) = `MeasurementStat.RecordCount` | CSV writer 는 틱마다 그 시퀀스의 모든 측정을 1줄씩 쓴다 → 항목 줄 수 = 그 시퀀스의 검사 틱 수. **N 의 기대값이 아니다**: 한 부품을 여러 z 틱으로 나눠 찍고 항목은 한 틱에서만 잰다(실측 부품당 4틱). 그래서 "누락 수"·"결과 비율" 칸은 만들지 않는다. 쓸모: N=0 일 때 "틱은 40번 돌았는데 결과 0"(09:50) 과 "기간 안 기록 자체 0"(시퀀스가 안 돔) 을 구분 |
| 검출실패 (기존) | DATUM_FAIL + NO_IMAGE 줄 수(RepeatMeasurementStats 규칙 그대로) | CSV 에 항목 단위로 남는 명시적 실패는 이 둘뿐. DETECT_FAIL 은 사이클 종합(OverallCycleResult=N)에만 있음. MEASURE_FAIL/ALIGN_FAIL/DATUM_REF_MISSING 등은 `ClearResult()` 로 HasResult=false 가 되어 writer 가 NO_RESULT 로 기록 → 구조적 빈 줄과 구분 불가. 헤더 툴팁에 이 한계를 적는다 |
| 결과 없음 (신규 상태, 보라) | N == 0 (이때 NG 도 0) | 기존에는 N=0 행이 흰색 "정상" 이었다. 불량 다음 순서로 정렬, "문제 항목만 보기" 에 포함 |
| 요약 "(기록 없음 X)" | 결과 없음 중 RecordCount == 0 인 항목 수 | 선택 날짜 파일에는 줄이 있으나 기간 안에는 줄이 없는 항목 |
| 부품 수 | **넣지 않음** | CSV 에 z 열 없음. 부품 경계는 cycle.json 의 ZIndex == 기준점 z 로만 알 수 있고(SavedCycleRerunPlanner), 이는 결과 폴더 저장 여부·기준점 사진·레시피 z 구성에 따라 달라지는 다른 소스·다른 필터라 CSV 기반 N 옆에 두면 오해를 부른다 |

항목 목록 범위: 선택 날짜 파일에서 레시피·자동 필터를 통과한 항목은 기간 안 줄이 없어도 표에 남긴다(기록 0 · N 0 · 결과 없음). 공차는 그 항목의 마지막 줄 값을 쓴다. 레시피 콤보 목록은 기간 안 줄에서만 만든다. 기본값(하루 전체)에서는 모든 줄이 기간 안이라 추가 항목이 0개이고 결과가 기존과 같다.

## Task 별 검증 결과 (실측 숫자)

### Task 1 — 빌드/하드룰/실데이터

- `build_errors=0`, `exe_fresh=1`, 두 .cs 파일 하드룰 grep 6종 전부 0(`rule_fail=0`), `xaml_hbk=0`.
- **`wholeday_identical=1`**: `D:\Data\Statistics` 12개 CSV 전체(구 14컬럼 8/2~8/11 포함, 8/24 검출실패 11줄 포함, 2026-09-14 828줄 포함)에서 하루 단위 항목별 N·검출실패·총 행 수가 awk 독립 계산과 **한 줄도 다르지 않음** — "시간을 안 건드리면 결과 불변" 확인.
  - (하네스 산출 CRLF vs awk LF 개행 차이로 1차 diff 가 264줄 전부 다르게 나온 이슈 발견 → 하네스 출력을 LF 로 통일해 재검증, Deviations 절 참고.)
- 2026-09-14 기간별 실측(항목 9개 = SIDE_SHOT_1_C13-14 계열 6개 + SIDE_SHOT_1_F9 계열 3개):

  | 기간 | N12 | N13 | N10 | TOTAL | RECIPES | WHOLE |
  |---|---|---|---|---|---|---|
  | 하루 전체(r1) | 6 | 3 | - | 828 | FAI_1 | True |
  | 09:50~09:59(r2) | - | - | - | 360(N0=9) | | False |
  | 13:20~13:29(r3) | - | - | 9 | 360 | | |
  | 10:00~12:59(r4, 데이터 없는 구간) | - | - | - | 0(keys=0) | | |
  | 13:30~13:20(r5, From>To 역전) | - | - | - | 0(keys=0, EMPTY=True) | | |

### Task 2 — 결과 없음/기록(틱)/요약

- `build_errors=0`, `rule_fail=0`, `xaml_hbk=0`, `noresult_trigger=1`, `rec_col=1`.
- `wholeday_identical=1` 재확인(기간 밖 항목 보존이 하루 전체 결과에 영향 없음).
- r1(하루 전체): `rec92bad=9`(9개 항목 전부 기록 92·상태 Bad), 요약 `SUMMARY|전체 9항목 · 불량 9 · 결과 없음 0 · 주의 0 · 정상 0` 일치.
- r2(09:50~09:59): `rec40none=9`(9개 항목 전부 N=0·기록 40·상태 NoResult·Cpk "-"), 요약 `전체 9항목 · 불량 0 · 결과 없음 9 · 주의 0 · 정상 0` 일치.
- r3(13:20~13:29): `rec40bad=9`(N=10·기록 40·상태 Bad), 요약 `전체 9항목 · 불량 9 · 결과 없음 0 · 주의 0 · 정상 0` 일치.
- r4(10:00~12:59, 데이터 없는 구간): `rec0none=9`(N=0·기록 0·상태 NoResult), `TOTAL|0`, `RECIPES|`(빈 목록), 요약 `전체 9항목 · 불량 0 · 결과 없음 9(기록 없음 9) · 주의 0 · 정상 0` 일치.
- r5(From>To): keys=0, 요약 `전체 0항목 · 불량 0 · 결과 없음 0 · 주의 0 · 정상 0` 일치.

### Task 3 — 재검사·export 기간 배선

- `build_errors=0`, `rule_fail=0`, `leftover_refs=0`(GetSelectedRange/구 ParseInspectionTime 완전 제거), `range_calls=3`(DoQuery·Btn_Rerun_Click·Btn_CpkExport_Click 모두 `m_periodVm.BuildRange()` 사용).
- `changed_files`(누적) = 계획한 5개 파일뿐(`MeasurementHistoryCsvLoader.cs`, `RepeatMeasurementStats.cs`, `RepeatRunService.cs`, `StatisticsWindow.xaml`, `StatisticsWindow.xaml.cs`). Phase 76 파일·`ReviewerWindow`·`MeasurementHistoryCsvWriter.cs`·csproj 없음.
- `wholeday_identical=1` 재확인.
- CYCLES(QueryCyclesDirectory 사이클 수) / TICKS(cycle.json 기준 IsProtocolDriven && 기간 안 직접 카운트) / STAMP(export 파일명 스탬프):
  - r1(하루 전체): `CYCLES|92 TICKS|92 STAMP|20260914_20260914`
  - r2(09:50~09:59): `CYCLES|40 TICKS|40 STAMP|20260914_0950_20260914_0959`
  - r3(13:20~13:29): `CYCLES|40 TICKS|40 STAMP|20260914_1320_20260914_1329`
  - r4(10:00~12:59): `CYCLES|0 TICKS|0 STAMP|20260914_1000_20260914_1259`
  - r5(From>To): `CYCLES|0 TICKS|0 STAMP|20260914_1330_20260914_1320`
  - TICKS 가 계획 시점 폴더명 기준 사전 계수(40/40/92)와 정확히 일치 — 별도 조사 불필요.
- `contains` 모드(경계값 9줄) 전부 일치(`contains_ok=1`): 13:20:00 포함, 13:19:59 제외, 13:29:59 포함, 13:29:59.999 포함, 13:30:00 제외, 하루 전체 00:00:00/23:59:59.999 포함·다음날 00:00:00 제외, ComboBox 미선택(-1,-1) 폴백이 `2026-09-14 00:00 ~ 2026-09-14 23:59, IsWholeDays=True`.
- `stamps` 모드(4줄) 전부 일치(`stamps_ok=1`): 하루 전체는 기존 형식(`20260914_20260914`) 그대로, 시:분 지정 시 `yyyyMMdd_HHmm_yyyyMMdd_HHmm` 형식.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] 스크래치 하네스 개행 문자 불일치로 wholeday 회귀 diff 오탐**
- **발견 시점:** Task 1 검증(wholeday 비교)
- **문제:** C# `StringBuilder.AppendLine`이 `Environment.NewLine`(Windows CRLF)을 쓰는 반면 awk/bash 산출물은 LF만 써서, 내용이 완전히 같은 264줄이 `diff`에서 전부 다르게 잡힘(라인마다 `\r` 유무 차이).
- **수정:** 하네스에 `W(sb, line)` 헬퍼를 추가해 `sb.Append(line).Append('\n')`으로 통일(모든 `AppendLine` 호출을 교체). 앱 코드는 무관 — 하네스(커밋 대상 아님)만의 문제.
- **영향 파일:** `SP/IvtHarness.cs`(스크래치, 커밋 안 됨)
- **검증:** 수정 후 `wholeday_identical=1`(diff 완전 일치) 확인.
- **커밋:** 해당 없음(스크래치 파일은 하드룰·커밋 대상 아님)

**2. [Rule 4 판단 후 적용 안 함 — 정정] 날짜 꼬리 주석 잔존 방지**
- **발견 시점:** Task 1 hard-rule grep(`hbk` 3건)
- **문제:** `QueryDirectory`/새 `Query(range,...)`/`ProcessRow`로 로직을 옮기면서 기존 `//260707 hbk ...` 주석 3줄이 diff 상 "추가 줄"로 잡힘(H4 위반).
- **수정:** 해당 3줄에서 날짜 꼬리(`260707 hbk`)만 제거하고 "왜" 설명은 유지(예: `// from>to 방어`, `// D-11 필터 전에 distinct 수집(드롭다운용, 기간 안 줄 기준)`).
- **영향 파일:** `WPF_Example/Custom/Sequence/Inspection/MeasurementHistoryCsvLoader.cs`
- **검증:** grep 재실행 `hbk=0` 확인.
- **커밋:** `25da3d94`(Task 1 커밋에 포함)

---

**Total deviations:** 2 (모두 Rule 1 — 하드룰/검증 스크립트 정합성 보정, 앱 동작 로직 변경 아님)
**Impact on plan:** 계획 범위·동작에 영향 없음. 실데이터 비교 결과는 계획서 기재값과 정확히 일치.

## 후속 제안 (범위 밖, 기록만)

MEASURE_FAIL/ALIGN_FAIL/DATUM_REF_MISSING 등 항목 단위 측정 실패는 현재 CSV 에 `NO_RESULT`(구조적 빈 줄과 구분 불가)로만 남는다. 이를 CSV Judgement 값으로 명시적으로 남기려면 `MeasurementHistoryCsvWriter.cs`(이번 범위 밖, H11 로 수정 금지)의 기록 로직과 `MeasurementHistoryCsvLoader.BuildMeasFromRow`의 "그 외=NG" 분기를 함께 바꿔야 한다. 이번 작업에서는 헤더 툴팁으로 이 한계만 명시했다.

## Release 빌드

수행하지 않음(H9) — Release 빌드·`D:\Data\DatumMeasurement.exe` 배포는 오케스트레이터가 사용자 확인 후 처리.

## 실기 UAT 대기 체크리스트 (미체크, 사용자가 장비에서 수행)

- [ ] 1. 통계 창을 열면 기간이 오늘 00:00 ~ 오늘 23:59 이고, 표 숫자·요약이 이전 버전과 같다(20260914 기준 C13·C14 계열 N=12, F9 계열 N=13, 요약 "전체 9항목 · 불량 9 · 결과 없음 0 · 주의 0 · 정상 0").
- [ ] 2. From 09:50 · To 09:59 로 조회하면 SIDE_1 9개 항목이 모두 보라색 "결과 없음", N 0, 기록(틱) 40, Cpk "-". 요약 "결과 없음 9".
- [ ] 3. From 13:20 · To 13:29 로 조회하면 항목당 N 10, 기록(틱) 40. 행 클릭 시 히스토그램/추이가 10개 값으로 그려진다.
- [ ] 4. From 10:00 · To 12:59 로 조회하면 9개 항목이 결과 없음(기록 0), 요약에 "(기록 없음 9)" 가 보이고 CPK export 버튼이 비활성이다.
- [ ] 5. From 13:30 · To 13:20 처럼 거꾸로 고르면 요약 줄이 "기간 오류: 시작이 끝보다 늦습니다", 재검사 버튼은 같은 문구 메시지 박스로 거부된다.
- [ ] 6. "문제 항목만 보기" 체크 시 보라 행이 남고, 정렬이 불량 → 결과 없음 → 주의 → 정상 순이다. 기록(틱)/N/검출실패 헤더에 마우스를 올리면 설명 툴팁이 보인다.
- [ ] 7. 기간 13:20~13:29 로 "저장 사진으로 재검사"를 실행하면 진행 표시의 부품 수가 그 구간 부품만이다(하루 전체로 했을 때보다 적다). 결과 표의 원래 평균이 같은 구간 통계와 맞는다.
- [ ] 8. 기간 13:20~13:29 에서 CPK export 를 누르면 기본 파일명이 `cpk_report_20260914_1320_20260914_1329.xlsx` 이고, 파일에 그 구간 사이클만 담긴다. 하루 전체면 `cpk_report_20260914_20260914.xlsx`(기존과 같음). 재검사 결과를 본 상태에서 시간을 바꾼 뒤 export 해도 파일명은 재검사한 기간이다.
- [ ] 9. 레시피 콤보, 행 선택 차트, 원래 통계로 버튼 등 기존 기능이 그대로 동작한다.

## Self-Check: PASSED

- 5개 코드 파일 + SUMMARY.md 전부 FOUND.
- 커밋 3개(`25da3d94`, `8268db30`, `f923c7e5`) 전부 FOUND(git log).
