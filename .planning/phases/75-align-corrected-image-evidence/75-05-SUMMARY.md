---
phase: 75-align-corrected-image-evidence
plan: 05
status: complete
date: 2026-08-27
---

# 75-05 SUMMARY — Align 정합 조회 화면

## 만든 것

| 파일 | 내용 |
|---|---|
| `Custom/EthernetVision/AlignVerifyCsvLoader.cs` (신규) | CSV 조회/집계 + 편차 헬퍼 2종 |
| `UI/Reviewer/AlignVerifyViewModel.cs` (신규) | 화면 상태 + **표시 문자열 생성** (INPC) |
| `UI/Reviewer/AlignVerifyWindow.xaml` (신규) | 조회 창 (MVVM 바인딩만) |
| `UI/Reviewer/AlignVerifyWindow.xaml.cs` (신규) | **배선만** — 세미콜론 6줄 |
| `UI/Reviewer/ReviewerWindow.xaml` | `btn_alignVerify` 버튼 1개 — **순수 삽입** |
| `UI/Reviewer/ReviewerWindow.xaml.cs` | `Button_AlignVerify_Click` 핸들러 — **순수 삽입** |
| `DatumMeasurement.csproj` | Compile 2 + Page 1 — **커밋 안 함** |

## 공개 계약

```csharp
// 조회 (namespace ReringProject)
public static AlignVerifyQueryResult AlignVerifyCsvLoader.Query(
    DateTime dtFrom, DateTime dtTo, int nMaterialNo, int nRecentCount);

// 편차 계산 단일 지점 — 화면이 다시 계산하지 않는다
public static double AlignVerifyCsvLoader.ComputeAlignDistanceMm(AlignVerifyRecord rec);
public static bool   AlignVerifyCsvLoader.TryComputeSeatDeviation(
    AlignVerifyRecord rec, out double outPx, out double outMm);   // false = mm 환산 불가

// 창 (namespace ReringProject.UI)
public AlignVerifyWindow();
public void SetInitialMaterial(string szMaterialNo);
```

## 🔴 임계 게이트 — 이 phase 의 핵심 안전장치

`AlignVerifyResidualLimitMm` / `AlignVerifySeatLimitMm` 기본값은 **0 = 미설정**이다.

| 상태 | 화면 |
|---|---|
| 임계 `0` | 판정 칸에 `"(판정 기준 미설정)"`. **정상/벗어남을 절대 표시하지 않는다** |
| 임계 둘 다 `> 0` | `"정상"` / `"벗어남"` + 결론 문구 |
| 임계 하나라도 `0` | 결론: `"판정 기준이 설정되지 않아 결론을 내지 않습니다. (설정 → Path\|AlignVerify)"` + 안내 문구 |

**실측 산포 없이 임계를 넣으면 정상품을 버린다.** 1차 배포는 숫자만 보여준다.

## 결론 문구 (임계 둘 다 설정된 경우에만)

| ① | ② | 문구 |
|---|---|---|
| 정상 | 정상 | `정상` |
| 정상 | 벗어남 | `비전은 맞게 줬는데 놓는 위치가 틀어졌습니다 (피커 쪽 점검)` |
| 벗어남 | (무관) | `Align 계산 자체가 기준에 못 미칩니다 (비전 쪽 점검)` |
| 데이터 없음 | | `해당 자재번호의 기록이 없습니다` |

## 해상도 0 처리 (75-04 가 넘긴 요건)

`PixelResolutionMmPerPx <= 0` 이면 **mm 로 환산하지 않는다.**
- 자재 요약: `"환산 불가(px 만) — 해상도 미상"`, 판정은 `"-"`
- 상세 행: `"환산 불가(px 만): {px:F2}px"`
- 시퀀스별 집계: `HasResolution = false` + `평균px`/`최대px` 컬럼 별도 제공

0 을 곱해 `0mm` 로 보여주면 **"편차 없음" 으로 오독된다.**

## 알려진 한계 — 화면 하단 고정 (조건부 아님)

```
※ SIDE 는 측면 촬영이라 앞뒤(깊이) 방향은 검증되지 않습니다. 좌우·높이만 확인됩니다.
※ ① 은 검출·강체변환의 자기일관성만 검증합니다. 피커센터 기준 재표현(부호 규약)은 ②로 확인합니다.
```

## 진입 경로

결과 리뷰어 좌측 버튼 스택 → **[Align 정합 조회]** (`차트 이미지 캡처 점검` 바로 아래).
`txt_materialIndex` 값을 초기 자재번호로 **복사**해 넘긴다 — 그 입력란의 기존 용도
("이미지 폴더 반복 검사")는 그대로 둔다.

## 검증 결과

| 빌드 | 에러 | 경고 | 코드 종류 |
|---|---|---|---|
| SIMUL-ON | **0** | **18줄** | `CS0162`/`CS0618` ✅ |
| SIMUL-OFF | **0** | **16줄** | `CS0618` ✅ |

XAML 오타는 컴파일 에러로 잡히므로 이 빌드가 창 XAML 의 실질 검증이다
(`Page Include` 누락 시 `InitializeComponent` 미정의 CS0103 으로 즉시 드러난다).

| acceptance | 기대 | 실측 |
|---|---|---|
| `COL_` 상수 | 20 | **20** ✅ |
| `COLUMN_COUNT = 20` | 1 | **1** ✅ |
| `Query` 메서드 | 1 | **1** ✅ |
| `TryComputeSeatDeviation` | ≥2 | **4** ✅ |
| `Split(',')` 직접 파싱 | 0 | **0** ✅ (RFC4180 파서 복제) |
| Loader `throw` 문 | 0 | **0** ✅ |
| `INotifyPropertyChanged` | ≥1 | **1** ✅ |
| 임계 게이트(`bResidualLimitSet`/`bSeatLimitSet`) | ≥2 | **7** ✅ |
| `판정 기준 미설정` | ≥1 | **1** ✅ |
| `AlignVerifyCsvLoader.Query` 호출 | 1 | **1** ✅ |
| code-behind 세미콜론 줄 | 적을수록 | **6** ✅ |
| code-behind 금지 심볼(`File.`/`Math.`/`Directory.`/Loader) | 0 | **0** ✅ |
| `KnownLimitText` 바인딩 | ≥1 | **1** ✅ |
| `ReviewerWindow.xaml(.cs)` 삭제 줄 | 0 | **0 / 0** ✅ |
| 기존 통계 계층 워킹트리 | 0줄 | **0줄** ✅ |
| 신규 3 `.cs` 의 `?:` / `??` / `?.` | 0 | **전부 0** ✅ |

## Deviations

없음. 계획대로 구현했다.

## 커밋

신규 4파일 + `ReviewerWindow.xaml` + `ReviewerWindow.xaml.cs`. csproj 는 **커밋하지 않음**.

## Self-Check: PASSED

- 조회 로직이 `MeasurementHistoryCsvLoader` 의 RFC4180 파서를 복제해 쓴다 (Datum 이름에 콤마가 있어도 컬럼이 안 밀린다) ✅
- 편차 계산이 Loader 단일 지점에 있고 화면은 재계산하지 않는다 ✅
- 임계 0 이면 판정 문구가 나오지 않는다 ✅
- 해상도 0 이 `0mm` 로 표시되지 않는다 ✅
- 한계 문구가 조건 없이 항상 보인다 ✅
- code-behind 는 배선만 (MVVM) ✅
