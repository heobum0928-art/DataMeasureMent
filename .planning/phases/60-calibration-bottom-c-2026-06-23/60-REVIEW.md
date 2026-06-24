---
phase: 60-calibration-bottom-c-2026-06-23
reviewed: 2026-06-24T00:00:00Z
depth: standard
files_reviewed: 5
files_reviewed_list:
  - WPF_Example/Custom/EthernetVision/PickerCenterCalibrationService.cs
  - WPF_Example/Custom/EthernetVision/AlignShapeMatchService.cs
  - WPF_Example/Custom/EthernetVision/EthernetVisionHandler.cs
  - WPF_Example/Custom/SystemSetting.cs
  - WPF_Example/DatumMeasurement.csproj
findings:
  critical: 0
  warning: 3
  info: 2
  total: 5
status: clean
---

# Phase 60: Code Review Report

**Reviewed:** 2026-06-24
**Depth:** standard
**Files Reviewed:** 5
**Status:** issues_found

## Summary

Phase 60은 `PickerCenterCalibrationService` 신규 파일(200줄)을 중심으로 `AlignShapeMatchService.ApplyPickerCenterCorrection`, `EthernetVisionHandler.PickerCal`, `SystemSetting.PickerCenterRow/Col` 4개 파일을 수정한다. HALCON 리소스 수명 관리와 실패 격리는 전반적으로 올바르다. `finally` 블록이 모든 HObject/HTuple을 포괄하며, 조기 반환 경로도 `finally`가 보장한다. `Save()` 호출은 실제 INI 기록 메서드다.

아래에서 지적하는 사항은 1건의 잠재적 데이터 부패(BLOCKER급 아님, HIGH) — 36-스텝 피팅 결과 저장 전에 올바른 접근이지만 실행 시 예외가 발생하면 Setting이 부분 저장될 수 있다 — 과, 2건의 중요도 경고(WARNING), 2건의 정보성 사항이다.

---

## Warnings

### WR-01: `TryComputePickerCenter` — 반경 가드가 예외 경로에서 `out` 값을 0으로 반환한 후 호출자가 `Save()`를 건너뛰는 경로 부재 (논리 순서 위험)

**File:** `WPF_Example/Custom/EthernetVision/PickerCenterCalibrationService.cs:161-175`

**Issue:** `bRadiusBad` 체크(line 161-165)는 올바르다. 그러나 체크 통과 후 `row = fittedRow; col = fittedCol; radius = fittedRad;`를 먼저 out 파라미터에 기록하고(line 168-170), 그 다음에 `SystemSetting.Handle.PickerCenterRow = fittedRow; ... Save();`를 호출한다(line 173-175). 이 순서는 문제 없다. **그러나** `Save()` 자체가 예외를 던질 경우(디스크 가득 찬 경우 등) `catch`가 이를 잡아 `false`를 반환하는데, `out row/col/radius`는 이미 피팅 값으로 채워져 반환된다. 즉 **호출자 입장에서 `false`를 받아도 `out` 값은 유효한 것처럼 보이는 불일치**가 발생한다. 호출자(Phase 61 UI)가 반환값 `false`를 무시하고 `out row/col`를 읽으면 저장되지 않은 값으로 계속 진행하게 된다.

**Fix:** `Save()` 실패 시 `out` 파라미터를 0으로 리셋하거나(강제), 아니면 `Save()` 이전에 out 파라미터를 할당하지 않고 성공 시에만 할당한다.

```csharp
// 권장 패턴: Save 성공 확인 후 out 할당
SystemSetting.Handle.PickerCenterRow = fittedRow;
SystemSetting.Handle.PickerCenterCol = fittedCol;
SystemSetting.Handle.Save();   // 예외 → catch → false, out은 0 상태

// Save 예외가 발생하지 않은 경우에만 여기 도달
row = fittedRow;
col = fittedCol;
radius = fittedRad;
```

---

### WR-02: `TryAddStep` — `fitRow.Length == 0` 조기 반환 후 `fitRow[0].D` (log 라인)가 `return true` 경로에서 안전하나 `fitRad` 인덱스 접근이 `fitRow` 길이 검사 후에만 보호됨

**File:** `WPF_Example/Custom/EthernetVision/PickerCenterCalibrationService.cs:89-101`

**Issue:** `FitCircleContourXld`는 결과 튜플이 복수(다수 원 발견)인 경우 `fitRow.Length > 1`이 될 수 있다. 현재 코드는 `fitRow.Length == 0` 만 검사하고 `fitRow[0]`, `fitCol[0]`, `fitRad[0]`를 직접 접근한다. `Length >= 1`이면 `[0]`은 안전하다. 그러나 `FitCircleContourXld`의 결과 배열이 `fitRow.Length > 0`이지만 `fitRad.Length == 0`일 수 있는지 — HALCON 명세상 결과 배열 길이는 항상 동일하게 출력되므로 실제 배열 불일치 가능성은 없다.

**진짜 문제:** 단일 호출에서 여러 원이 검출되면 `fitRow[0]`만 사용하며 나머지는 무시된다. 이는 `atukey` 플래그와 `-1` (전체 컨투어 단위 처리) 인자 조합에서 일반적으로 단일 결과를 돌려주므로 실용적으로 문제가 없다. 그러나 명시적인 `fitRow.Length != 1` 경고 로그가 없어 디버깅 시 복수 원 검출을 감지할 수 없다.

**Fix:** 복수 결과 시 경고 로그를 추가한다:

```csharp
if (fitRow.Length == 0) {
    error = "지그 원 피팅 실패 (에지/원 없음)";
    return false;
}
if (fitRow.Length > 1) {
    Logging.PrintLog((int)ELogType.Error,
        "[PICKER_CAL] TryAddStep: 복수 원 검출({0}) — [0] 사용", fitRow.Length);
}
```

---

### WR-03: `RestorePickerCenterDefault` — 완전한 no-op이지만 분기 조건이 `_rows` 빈 `List<double>` 와는 무관, 그러나 INI 저장 값 0.0 을 `bUncalibrated` 로 판정하는 `PICKER_CENTER_ZERO_EPS` 임계가 `AlignShapeMatchService`와 `SystemSetting`에서 각각 독립 선언됨

**File:** `WPF_Example/Custom/EthernetVision/AlignShapeMatchService.cs:48`, `WPF_Example/Custom/SystemSetting.cs:71`

**Issue:** 미캘 판정 임계(1e-6)가 `AlignShapeMatchService`의 `PICKER_CENTER_ZERO_EPS` 상수에만 있고, `SystemSetting.RestorePickerCenterDefault`는 정확히 `0.0`과 비교한다(line 71). 이 두 곳이 각각 독립적인 기준을 사용한다. 만약 향후 피커센터 캘 결과가 부동소수점 수치적으로 0에 가까운 매우 작은 값(예: `1e-9`)으로 저장되면, `SystemSetting`은 미캘로 복원하지 않지만 `AlignShapeMatchService`는 올바르게 미캘로 간주한다. 반대로 `AlignShapeMatchService`가 미캘 폴백을 사용하면서 `SystemSetting`은 캘된 것으로 유지하는 불일치가 없다. 그러나 `RestorePickerCenterDefault`에서 `== 0.0` 비교는 IEEE 754 double 정밀도에서 저장/로드 라운드트립을 거치면 위험할 수 있다.

**Fix:** 두 판정 기준을 동일한 상수로 일치시킨다. `SystemSetting.RestorePickerCenterDefault`에서도 `PICKER_CENTER_ZERO_EPS`에 해당하는 동일한 임계를 사용하거나, 공유 const를 `SystemSetting.cs` 에 정의하고 `AlignShapeMatchService`가 참조하도록 한다.

```csharp
// SystemSetting.cs
private const double PICKER_CENTER_ZERO_EPS = 1e-6;

private void RestorePickerCenterDefault()
{
    bool bPickerCenterUncalibrated =
        (Math.Abs(PickerCenterRow) <= PICKER_CENTER_ZERO_EPS)
        && (Math.Abs(PickerCenterCol) <= PICKER_CENTER_ZERO_EPS);
    if (bPickerCenterUncalibrated)
    {
        // 미캘 상태 유지 — 별도 복원 없음
    }
}
```

---

## Info

### IN-01: `ELogType.Camera` 를 스텝 로그에 사용 — 의미론적 불일치

**File:** `WPF_Example/Custom/EthernetVision/PickerCenterCalibrationService.cs:99, 177`

**Issue:** 스텝별 지그 원 피팅 결과(`[PICKER_CAL] step ...`)와 최종 피커센터 산출 결과를 `ELogType.Camera` 채널에 기록한다. 이 정보는 카메라 소통 로그보다 캘리브레이션 이벤트에 가깝다. 다른 알고리즘 서비스(`VisionAlgorithmService`, `FAIEdgeMeasurementService`)는 측정 결과를 `ELogType.Trace` 또는 `ELogType.Result`에 기록한다. 실제 버그는 아니지만 로그 파일 분리 정책과 불일치하여 캘 결과를 카메라 로그 디렉터리에서 찾아야 한다.

**Fix:** 피커캘 로그를 `ELogType.Trace`로 변경하거나, 별도 캘리브레이션 로그 타입이 추가될 때 교체할 수 있도록 주석으로 명시한다.

---

### IN-02: `RestorePickerCenterDefault` — 빈 `if` 블록

**File:** `WPF_Example/Custom/SystemSetting.cs:70-76`

**Issue:** 의도 명시 주석이 있어 이유는 이해되나, `if (bPickerCenterUncalibrated) { // 미캘 상태 유지 }` 구조는 정적 분석 도구 경고 대상이다. 코드가 실제로 수행하는 동작이 없으므로 메서드 전체를 주석으로 대체하거나 조건 없이 주석만 남기는 편이 명확하다.

**Fix:**
```csharp
private void RestorePickerCenterDefault()
{
    // PickerCenterRow/Col 기본값 0 = 미캘 상태(정상 초기값).
    // reflection Load 가 누락 키를 0 으로 로드하는 것이 곧 올바른 의미이므로 복원 불필요.
    // 향후 비-0 머신 기본값 도입 시 WR-03 수정과 함께 복원 로직 추가.
}
```

---

## HALCON 리소스 수명 검증 결과 (정상)

- `TryAddStep` (36회 반복 경로): `circleRegion`, `imageReduced`, `edges`, `fitRow/Col/Rad`, `startPhi`, `endPhi`, `pointOrder` 9개 HObject/HTuple 모두 `finally`에서 null 가드 포함 해제. 조기 반환(`fitRow.Length == 0`, line 89-92)도 `finally`가 실행되므로 누수 없음. **정상.**
- `TryComputePickerCenter`: `contour`, `allRows`, `allCols`, `fitRow/Col/Rad`, `startPhi`, `endPhi`, `pointOrder` 9개 모두 `finally`에서 해제. `allRows`/`allCols`는 `try` 블록 내부에서 생성되므로 `GenContourPolygonXld` 이전 예외 시 null 가드가 보호함. **정상.**
- `ApplyPickerCenterCorrection`: `homMat`, `rotMat`, `pointRow`, `pointCol`, `outRow`, `outCol` 6개 HTuple 모두 `finally`에서 해제. 미캘 조기 반환 경로(line 455-456)는 HTuple 미생성 상태이므로 누수 없음. **정상.**

## 실패 격리 검증 결과 (정상)

- `Reset`: throw 경로 없음. **정상.**
- `TryAddStep`: null 가드 → try-catch → false. `HOperatorSet` 예외 포함 전체 포획. **정상.**
- `TryComputePickerCenter`: `MIN_STEPS` 가드(line 129-132) → try-catch → false. **정상.**
- `ApplyPickerCenterCorrection`: void, out 파라미터 기본값 설정 → try-catch → 폴백. throw 없음. **정상.**

## 누적기 정확성 검증 결과 (정상)

- `_rows`/`_cols`는 인스턴스 필드(`readonly List<double>`) — 호출 간 누적 유지. **정상.**
- `Reset()`은 두 리스트 모두 Clear(). **정상.**
- `GenContourPolygonXld(out contour, allRows, allCols)` — HALCON 관례상 (Rows, Cols) 순서. 기존 코드베이스의 `VisionAlgorithmService.cs:496`, `DatumFindingService.cs:323` 등 동일 순서 확인. **정상.**

## 지속성 검증 결과 (정상)

- `SystemSetting.Handle.Save()` — base `SystemSetting.cs:281`에 존재, INI 직렬화 수행. `PickerCenterRow/Col`은 `Double` 타입이므로 `case "Double":` 분기에서 저장됨. **정상.**

---

_Reviewed: 2026-06-24_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
