---
quick_id: 260819-dvf
title: Cpk 표준편차 0 판정을 epsilon 비교로 수정
date: 2026-08-19
base_commit: 0dbb4da
files_modified:
  - WPF_Example/Custom/Sequence/Inspection/RepeatMeasurementStats.cs
must_haves:
  truths:
    - "stddev 가 부동소수점 잡음 수준(≤1e-15)일 때 Cp/UCpk/LCpk/Cpk 가 PositiveInfinity 로 처리되어 엑셀에 '∞' 로 찍힌다"
    - "실제 산포(≥1e-5 mm)가 있는 데이터는 수정 전후 Cpk 값이 완전히 동일하다"
    - "임계값은 이름 있는 const 이며 선정 근거가 주석에 남아 있다"
  artifacts:
    - "STDDEV_ZERO_EPS const 선언 + 근거 주석"
    - "if (stddev < STDDEV_ZERO_EPS) 가드"
---

# Quick 260819-dvf: Cpk 표준편차 0 판정 임계값화

## 증상 (실측)

사용자 리포트 `Z:\DOC_EXPORT\2026-08-19[09.53.48]\cpk_report_20260819_095253.xlsx` 의
`1Cav 세부치수_Cpk` 시트 61개 항목 중 **41개**가 아래처럼 출력:

```
Cp   = 3,250,193,071,480.82
UCPK = 3,152,356,117,722.15
Cpk  = 3,152,356,117,722.15
```

나머지 20개는 정상적으로 `∞` 문자열. **41 대 20 으로 갈린 것이 이 버그의 결정적 단서**였다.

## 근본원인

`RepeatMeasurementStats.cs:190`(수정 전) 가드가 `if (stddev == 0)` 로 **정확히 0** 만 잡았다.

동일 이미지를 반복검사하면 측정값 N개가 수학적으로 동일하지만,
`mean = Sum()/n` → `sumSq += (v-mean)²` → `Math.Sqrt(sumSq/(n-1))` 경로에서
**mean 이 원값으로 1 ULP 오차 없이 되돌아오는지가 값에 따라 갈린다.**

Python 으로 실제 리포트의 측정값 10개를 재현한 결과:

| 측정값 | 계산된 stddev |
|---|---|
| 20.681903 | 0.0 (정확히 0) |
| 19.782355 | 0.0 |
| **0.849669** | **1.2413e-16** ← 0 아님 |
| **3.40555** | **4.9651e-16** ← 0 아님 |

→ 0 이 된 값은 `∞`(20개), 0 이 아닌 값은 가드를 통과해 극소값으로 나눠지며
거대 유한수(41개)가 됐다. **41/20 분리가 정확히 이것으로 설명된다.**

엑셀 R열(Std Dev)이 `0` 으로 보이는 건 `WriteStatCell` 의 6자리 반올림 표시일 뿐 실제 값은 0 이 아니다.

## 수정

`WPF_Example/Custom/Sequence/Inspection/RepeatMeasurementStats.cs` 단일 파일, 2지점:

1. 클래스 필드 영역에 `private const double STDDEV_ZERO_EPS = 1e-9;` + 근거 주석 9줄
2. 가드를 `if (stddev == 0)` → `if (stddev < STDDEV_ZERO_EPS)`
3. (부수) `ComputeAll()` XML 주석의 `σ=0 이면` → `σ가 STDDEV_ZERO_EPS 미만이면`

### 임계값 1e-9 선정 근거

| | 값 | 1e-9 대비 |
|---|---|---|
| 관측된 부동소수점 잡음 | 1.2e-16 ~ 4.9e-16 | **6~7자리 아래** → 확실히 차단 |
| 비전 측정 최소 산포 하한 | ~1e-5 mm | 4자리 위 → 영향 없음 |
| 오늘 실측 이미지 2장 간 편차 | 8.7e-4 mm | **87만 배 위** → 영향 없음 |

임계값을 키우면 진짜 산포를 0 으로 오인해 Cpk 를 ∞ 로 감추는 **불량 은폐 방향**이므로 올리지 말 것.

### 프로젝트 전례

`WPF_Example/Custom/SystemSetting.cs:135` — `WR-03 fix //260624 hbk: == 0.0 → PICKER_CENTER_ZERO_EPS 임계 비교로 통일`
(`public const double PICKER_CENTER_ZERO_EPS = 1e-6;`). 동일 계열 조치이며 명명/주석 스타일을 따랐다.

## 범위

- stddev 로 나누는 지점은 코드베이스 전체에서 `RepeatMeasurementStats.cs` 의 3줄(`/(6*stddev)`, `/(3*stddev)` ×2)뿐이고 가드도 1개뿐임을 grep 으로 확인 → **단일 지점 수정**
- `CpkReportExportService.WriteStatCell`(PositiveInfinity → `"∞"`) **무변경** — 가드가 걸리면 기존 표기가 그대로 작동
- Mean/Range/Min/Max/N/OkCount, 판정 로직(`BuildCpkJudgement`), 엑셀 시트 구조 **무변경**
- ⚠ **파급**: `MeasurementHistoryCsvLoader.Query()`(통계분석 창 날짜 조회)도 같은 `ComputeAll()` 을 쓰므로 자동 반영된다. 그쪽도 동일 버그가 있었으므로 의도된 개선이다.
