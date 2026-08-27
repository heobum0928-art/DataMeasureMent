---
phase: 75-align-corrected-image-evidence
plan: 01
status: complete
date: 2026-08-27
---

# 75-01 SUMMARY — ① 보정 후 재매칭 엔진

## 만든 것

| 파일 | 내용 |
|---|---|
| `WPF_Example/Custom/EthernetVision/AlignVerifyResult.cs` (신규) | ① 결과 POCO 7 프로퍼티 |
| `WPF_Example/Custom/EthernetVision/AlignShapeMatchService.Verify.cs` (신규) | `RunCorrectedRecheck` 오버로드 2개 |
| `WPF_Example/Custom/EthernetVision/AlignShapeMatchService.cs` | `class` → `partial class` **한 단어만** (`git diff --numstat` = `1 1`) |
| `WPF_Example/DatumMeasurement.csproj` | Compile 항목 2개 추가 — **커밋 안 함(unstaged 유지)** |

## 확정 시그니처 (75-03 이 호출한다)

```csharp
// (A) 자체 검출판 — 1차 검출 직접 수행(TryFindPose 4회). 오프라인/단독 호출용.
public AlignVerifyResult RunCorrectedRecheck(
    HImage img, EEthernetVisionMode mode, EBottomAlignSlot slot, out HImage correctedImage);

// (B) 검출 재사용판 — Run() 의 1차 검출을 받아 그 2회를 건너뛴다(TryFindPose 2회).  ★ 75-03 은 이걸 쓴다
public AlignVerifyResult RunCorrectedRecheck(
    HImage img, EEthernetVisionMode mode, EBottomAlignSlot slot,
    bool bHasDetection, double dDet1Row, double dDet1Col, double dDet2Row, double dDet2Col,
    out HImage correctedImage);
```

(A) 는 (B) 에 `false, 0.0, 0.0, 0.0, 0.0` 을 넘기는 **한 줄 위임**이다. 구현 본체는 (B) 하나뿐.

## `AlignVerifyResult` 프로퍼티 (75-02 CSV 컬럼 / 75-05 UI 가 소비)

| 프로퍼티 | 타입 | 의미 |
|---|---|---|
| `Verified` | bool | 재매칭까지 전부 성공 = 아래 수치 유효 |
| `ResidualOffsetXmm` | double | 보정 후 남은 X(mm). **Col 축** |
| `ResidualOffsetYmm` | double | 보정 후 남은 Y(mm). **Row 축** |
| `ResidualThetaDeg` | double | 재baseline − 기준 baseline (deg) |
| `ResidualDistanceMm` | double | `sqrt(X²+Y²)` |
| `Score` | double | 재매칭 두 점수 중 **작은 값** |
| `FailReason` | string | 실패 사유. 성공 시 `""` |

## 🔴 `correctedImage` 소유권 규약

- **호출자가 Dispose 책임을 진다.**
- **재매칭 실패 시에도 non-null 일 수 있다** — 실패한 보정 이미지가 곧 NG 증거이므로 버리지 않고 넘긴다.
- 예외 경로에서만 이 메서드가 직접 Dispose 하고 `null` 로 만든다.

## 실패 사유 문자열 (75-05 가 화면에 그대로 띄운다)

`입력 이미지 없음` / `모드 None` / `경로 미설정` / `기준 pose 없음` /
`1차 검출 실패[1]` / `1차 검출 실패[2]` / `baseline 산출 실패` /
`재검출 실패[1]` / `재검출 실패[2]` / `재baseline 산출 실패` / `예외: {메시지}`

## 로그 (75-06 UAT 가 이 줄을 본다)

```
[ALIGN_VERIFY] recheck ({mode}/{slot}) verified={..} residual=({..},{..})mm dist={..}mm
               theta={..} score={..} reused={..} elapsed={..}ms reason={..}
```

`ELogType.Algorithm`. `finally` 에서 성공/실패/예외 **모든 경로에 1회** 나간다.
`reused=True` = Run() 검출 재사용(정상), `False` = 자체검출 폴백.
`elapsed` 는 PLC 응답 경로에 얹은 실제 지연 — **100ms 초과면 75-06 U-6 에서 보고 대상.**

## 검증 결과

**빌드 (SIMUL-ON, `Debug|x64`, 스크래치 OutDir):** 에러 **0**, 경고 **18줄**, 코드 종류 `CS0162`/`CS0618` **2종뿐** = baseline 유지.

| acceptance | 기대 | 실측 |
|---|---|---|
| `AlignShapeMatchService.cs` numstat | `1 1` | **1 1** ✅ |
| `partial class` | 1 | **1** ✅ |
| `AlignVerifyResult` 프로퍼티 | ≥7 | **7** ✅ |
| csproj `AlignVerifyResult.cs` / `Verify.cs` | 1 / 1 | **1 / 1** ✅ |
| 오버로드 선언 | 2 | **2** ✅ |
| `_matcher.TryFindPose` | 4 | **4** ✅ |
| 폴백 2회가 `bHasDetection==true` 분기 밖 | 0 | **0** ✅ |
| `AffineTransImage(..., "bilinear", "false")` | 1 | **1** ✅ |
| `ApplyPickerCenterCorrection(` | 0 | **0** ✅ |
| `throw ` | 0 | **0** ✅ |
| `?:` / `??` / `?.` / switch식 | 0 | **0 / 0 / 0 / 0** ✅ |
| `Dispose` | ≥3 | **5** ✅ |
| `EthernetPixelResolution / UM_PER_MM` | 1 | **1** ✅ |
| `Stopwatch` / `ElapsedMilliseconds` | ≥1 / 1 | **1 / 1** ✅ |
| `elapsed=` / `reused=` | 1 / 1 | **1 / 1** ✅ |
| `hbk` 날짜 주석 | 0 | **0** ✅ |

## Deviations

**[Rule 3 - 계획 결함] 75-01-PLAN.md acceptance 2건이 줄-단위 grep 이라 실측과 불일치**

- 발견: Task 3 검증 중
- 내용:
  1. `awk '/if \(bHasDetection == true\)/,/^            else$/'` — 이 파일은 K&R 이라 실제로는
     `                else {` 다. 종료 패턴이 절대 매치되지 않았다.
  2. `grep -c 'Stopwatch' >= 2` — `System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();`
     는 **한 줄**이라 `Stopwatch` 가 2회 나와도 `grep -c` 는 1 이다.
- 조치: **코드가 아니라 계획의 기준을 고쳤다**(기준을 맞추려 코드를 바꾸지 않았다).
  각각 실제 스타일에 맞는 awk 패턴 / `ElapsedMilliseconds` 병행 검사로 교체하고,
  "왜 줄-단위 grep 이 함정인지" 를 계획에 주석으로 남겼다.
- 이 두 기준은 **어제 quick-260827-hdf 에서 내가 직접 작성한 것**이며, 같은 세션에서 지적했던
  L-3(줄 vs 파일 혼동)과 동일한 실수 유형이다.

**[Rule 1 - 계획 준수] 명명 상수 대신 리터럴 유지**

`AffineTransImage` 의 `"bilinear"`/`"false"` 를 명명 상수로 뽑았다가 되돌렸다.
계획이 리터럴 호출을 acceptance 앵커로 지정했고, 상수화하면 검증 앵커가 깨진다.
"왜 false 인가" 는 인접 주석으로 남겼다.

## 커밋

- `AlignVerifyResult.cs` / `AlignShapeMatchService.Verify.cs` / `AlignShapeMatchService.cs` — 아래 커밋
- `DatumMeasurement.csproj` — **의도적으로 커밋하지 않음**(unstaged 유지)

## Self-Check: PASSED

플랜 `<verification>` 5항목 전부 통과:
1. SIMUL-ON 빌드 에러 0, 새 경고 코드 0건 ✅
2. `AlignShapeMatchService.cs` 변경이 `1 1` (partial 한 단어) — `Run()` 무변경 ✅
3. `RunCorrectedRecheck` 안에 `throw` 0건, `?:`/`??`/`?.` 0건 ✅
4. `AffineTransImage` 의 `AdaptImageSize` 인자가 `"false"` ✅
5. csproj 미커밋 ✅
