---
quick_id: 260819-dvf
status: complete
date: 2026-08-19
base_commit: 0dbb4da
files_changed: 1
---

# Quick 260819-dvf — 완료 요약

## 무엇을 고쳤나

`WPF_Example/Custom/Sequence/Inspection/RepeatMeasurementStats.cs` 한 파일.

```diff
+ //260819 hbk quick-260819-dvf: 표준편차 "0" 판정 임계값. == 0.0 직접 비교 금지
+ //  (근거 주석 9줄 — 관측 잡음 / 실신호 하한 / 양방향 실패 모드)
+ private const double STDDEV_ZERO_EPS = 1e-9;

- if (stddev == 0)
+ if (stddev < STDDEV_ZERO_EPS)   //260819 hbk quick-260819-dvf
```

XML 주석 1줄도 `σ=0 이면` → `σ가 STDDEV_ZERO_EPS 미만이면` 으로 동기화.

## 수정 전/후 동작 대조 (Python 으로 동일 산식 재현해 검증)

| 입력 | stddev | 수정 전 | 수정 후 |
|---|---|---|---|
| 동일값 5개 (`0.849669`) | 1.24e-16 | Cp/Cpk = **80,562,839,281,945** ← 버그 | **∞** (엑셀 `"∞"`) |
| 동일값 5개 (`20.681903`) | 0.0 (정확히) | ∞ | ∞ (변화 없음) |
| 실산포 5개 (편차 8.7e-4) | 8.73e-4 | Cp=11.448 / Cpk=**10.967** | Cp=11.448 / Cpk=**10.967** (완전 동일) |

**정상 데이터는 한 자리도 안 바뀌고**, 잡음 케이스만 ∞ 로 잡힌다.

## 41 대 20 분리가 설명됐다

사용자 리포트에서 61개 중 41개는 거대숫자, 20개는 `∞` 문자열이었다(openpyxl 로 실측 확인).
값에 따라 `mean = Sum()/n` 이 원값으로 정확히 되돌아오기도 하고 1 ULP 어긋나기도 하기 때문이다 —
되돌아오면 stddev 가 정확히 0(→∞ 20개), 어긋나면 1e-16 수준(→거대숫자 41개).
수정 후에는 **61개 전부 ∞** 가 된다(해당 데이터셋 기준).

## 검증

| 항목 | 결과 |
|---|---|
| msbuild Debug/x64 (scratch OutDir) | exit 0, error 0 |
| 경고 | **12줄 (CS0618×10 + CS0162×2)** = baseline 정확 일치, 신규 0 |
| 변경 파일 | `RepeatMeasurementStats.cs` 1개뿐 |
| csproj | 로컬 설정(`D:\Data\`, Release `SIMUL_MODE`) unstaged 유지 — 커밋 안 됨 |
| 산식 검증 | Python 으로 C# 산식 재현, 3케이스 대조(위 표) |

## 파급 (의도된 것)

`MeasurementHistoryCsvLoader.Query()` — **통계분석 창의 날짜 조회에도 자동 반영된다.**
같은 `RepeatMeasurementStats.ComputeAll()` 을 쓰기 때문이며, 그쪽 화면에도 동일 버그가 있었으므로 함께 고쳐진 것이다.

## 남은 것 — 사용자 확인

이 수정으로 **거대 숫자는 사라지지만**, 동일 이미지를 반복하면 여전히 `∞` 가 나온다.
`∞` 는 "산포가 없어 공정능력지수를 정의할 수 없다"는 **정직한 표시**이지 오류가 아니다.

**의미 있는 Cpk 를 보려면 서로 다른 촬영본**(같은 부품을 여러 번 실제로 찍은 이미지)이 필요하다.
동일 파일 복사본으로는 산포가 0 이라 Cpk 계산 자체가 성립하지 않는다.
