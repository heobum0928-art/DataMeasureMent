---
task: 260910-ly4-measurement-point-roi-search-region-axis
type: quick
mode: bugfix
status: code-complete-uat-pending
files_modified:
  - WPF_Example/Halcon/Algorithms/VisionAlgorithmService.cs
commits: 2
---

# 260910-ly4: 측정 Point ROI 탐색영역 축 반전 수정 Summary

`VisionAlgorithmService.TryFitLine`이 측정 Point ROI의 `Length1`/`Length2`를 티칭·표시와
반대 축으로 해석해, 중심은 같지만 가로·세로가 뒤바뀐(transpose) 사각형을 탐색하던 버그를
고쳤다. 그리고 strip 루프 직전에 `[FitLine] strip-loop: bounds ...` 관측 로그를 추가해
실기에서 탐색 영역을 눈으로 확인할 수 있게 했다.

## 상태

- **코드 작업: 완료** (Task 1, 2, 3 모두 완료 — 커밋 2개, 빌드 확인 1회)
- **실기 UAT: 미완료 — 사용자/오케스트레이터가 실제 하드웨어에서 수행해야 함**
  (본 실행자는 SIMUL_MODE 없이 실카메라를 구동할 수 없어 코드 수정·빌드까지만 담당했다.)

## 변경 내용

### Task 1 — 축 매핑 수정 (커밋 `006ee5ad`)

`double halfW = roiLength1; double halfH = roiLength2;` → 다음으로 교정:

```csharp
double halfH = roiLength1;
double halfW = roiLength2;
```

`top = rRow - halfH`, `bottom = rRow + halfH`, `left = rCol - halfW`, `right = rCol + halfW`
계산식 6줄은 그대로 두었으므로(요구사항대로), 결과적으로:

```
bottom - top == 2 * roiLength1   (세로)
right - left == 2 * roiLength2   (가로)
```

가 성립하도록 바로잡았다. 수정한 두 줄 바로 위에 "왜" 주석 3항목(측정 Point ROI 규약,
MainView 티칭 3경로와의 일관성, FAIEdgeMeasurementService FAI ROI 규약과의 축 반전 차이)을
추가했다.

### Task 2 — strip 루프 직전 bounds 관측 로그 추가 (커밋 `c46deabd`)

`int failedStrips = 0;` 과 `if (scanHorizontal)`(strip 루프 시작) 사이에 추가:

```csharp
// 탐색 영역 관측 로그 — 실기에서 탐색 사각형을 눈으로 확인할 수 있도록 strip 루프 직전에 남긴다.
string szScanLabel = "vertical";
if (scanHorizontal)
{
    szScanLabel = "horizontal";
}

Logging.PrintLog((int)ELogType.Algorithm,
    string.Format("[FitLine] strip-loop: bounds top={0:F1} left={1:F1} bottom={2:F1} right={3:F1}  scan={4}  stripCount={5}  sigma={6:F2} threshold={7} polarity={8}",
        top, left, bottom, right,
        szScanLabel,
        stripCount, sigma, threshold, pol));
```

`DatumFindingService.cs`의 `strip-loop(extract)` 로그와 필드 순서·구분자 공백을 동일하게
맞추었고(태그만 `[FitLine]` 계열), `ELogType.Algorithm` 채널로 출력한다. 로직 변경은 없다
(관측 로그 추가뿐).

### Task 3 — 빌드 및 하드룰 최종 검증 (커밋 없음, 검증 전용)

- MSBuild `Debug|x64` — **에러 0** (경고는 기존 수준의 `CS0618`/`CS0169` 뿐, 이번 변경과 무관)
- 하드룰 grep 5종(삼항/null 병합/null 조건/switch 식/날짜 주석) — 추가 라인 기준 **전부 0**
- 금지 파일 3개(`DatumMeasurement.csproj`, `FAIEdgeMeasurementService.cs`,
  `MainView.xaml.cs`) — **미접촉 확인**
- 이번 작업이 만진 파일 — `VisionAlgorithmService.cs` **단 1개** 확인

## 영향 범위 (반드시 남겨야 하는 항목)

`VisionAlgorithmService.TryFitLine` 실호출부는 **17곳, 측정 타입 9종**이며 **전부 이 수정의
영향을 받는다**:

- `EdgeToLineDistanceMeasurement` (1)
- `EdgeToLineAngleMeasurement` (1)
- `ArcEdgeDistanceMeasurement` (1)
- `ArcLineIntersectDistanceMeasurement` (4)
- `DualImageEdgeDistanceMeasurement` (2)
- `LineToLineAngleMeasurement` (2)
- `LineToLineDistanceMeasurement` (2)
- `PointToLineDistanceMeasurement` (2)
- `PointToPointDistanceMeasurement` (2)

(`RoiLineIntersectionAlgorithm.cs`의 동명 private static `TryFitLine` 2곳은 무관 — 별개
메서드, 집계에서 제외.)

→ **`Length1 != Length2`인 기존 티칭 항목은 측정값이 달라질 수 있다.** 수정 후 값이
"사용자가 실제로 그린 박스대로 탐색한 값"이므로 이것이 정상이지만, **전 항목 재검증이
필요**하다.

→ **TOP / BOTTOM PC 에도 동일하게 해당된다.** 두 PC 모두 이번 커밋을 pull → 리빌드 →
재검증해야 한다.

## 실기 UAT — 사용자가 직접 수행 (미완료)

- [ ] **1. `[FitLine]` bounds 로그 확인**
      `SIDE_SHOT_2_C13-14_P1` 오프라인 검사 재실행 → `D:\Data\Algorithm\`의 당일
      `*_Algorithm.log`에 `[FitLine] strip-loop: bounds ...`가 찍히는지 확인.
      **판정 기준은 절대 좌표가 아니라 불변식이다** — 사용자가 조사 이후 ROI를 이동했고,
      로그의 좌표는 datum 런타임 보정(회전+원점 이동)이 적용된 값이라 절대 좌표로는
      판정할 수 없다:
      ```
      bottom - top == 2 * Point_Length1   (현재 레시피 기준 118 = 2*59, 세로)
      right  - left == 2 * Point_Length2   (현재 레시피 기준 200 = 2*100, 가로)
      ```
      버그 상태였다면 정확히 반대(세로 200 / 가로 118)로 찍혔다. "티칭 박스 근처 +
      폭·높이 일치"면 정상.

- [ ] **2. 빨간 에지 마크 위치**
      같은 검사에서 `C13_P1`(EdgeToLineDistance, DatumRef=Side_Datum_2)의 빨간 에지
      마크가 파란 박스 **안**에 들어오는지 육안 확인.

- [ ] **3. ROI 이동 내성**
      해당 ROI를 아래로 조금 이동해도 측정이 유지되는지 확인 (이전에는 실패했음).

- [ ] **4. 회귀 확인**
      기존에 정상이던 다른 측정 항목들이 회귀하지 않는지 확인. 특히 `Length1 != Length2`로
      티칭된 항목은 값이 바뀔 수 있으므로 공칭/공차 기준 재확인 필요 (위 영향 범위 참조).

## 자가 검증 (self-check)

- `git log -2 --oneline` → `006ee5ad`, `c46deabd` 두 커밋 확인됨
- `WPF_Example/Halcon/Algorithms/VisionAlgorithmService.cs` 존재 확인됨
- 빌드 산출물 `C:\code\DataMeasurement\WPF_Example\bin\x64\Debug\DatumMeasurement.exe` 생성됨

## Deviations from Plan

None — plan executed exactly as written. Task 1/2는 계획된 형태 그대로 적용했고, Task 3
검증에서 재수정이 필요한 항목이 없었다.

## Self-Check: PASSED
