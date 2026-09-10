---
task: 260910-md3-compound-measurement-rect-roi-genrectang
type: quick
mode: bugfix
status: code-complete-uat-pending
files_modified:
  - WPF_Example/Halcon/Algorithms/VisionAlgorithmService.cs
commits: 2
---

# 260910-md3: Compound 측정 Rect ROI 탐색영역 축 반전 수정 Summary

`VisionAlgorithmService.TryFindLargestContourRect` / `TryFindShortAxisIntersections` 가
측정 `Rect_*` ROI 의 `Length1`/`Length2` 를 HALCON `gen_rectangle2` 규약에 그대로 밀어넣어,
중심은 같고 가로·세로가 뒤바뀐(transpose) 사각형을 `ReduceDomain` 대상으로 삼던 버그를
고쳤다. `260910-ly4` 가 측정 `Point_*` ROI(`TryFitLine`)에서 고친 것과 같은 클래스의 버그이며,
이번은 측정 `Rect_*` ROI(Compound 4종) 경로다. 그리고 `GenRectangle2` 직후에
`[ContourRect]`/`[ShortAxis]` 관측 로그를 추가해 실기에서 "그린 박스대로 탐색하는가"를
로그 한 줄로 판정할 수 있게 했다.

## 상태

- **코드 작업: 완료** (Task 1, 2, 3 모두 완료 — 커밋 2개, 빌드 확인 1회)
- **실기 UAT: 미완료 — 사용자/오케스트레이터가 실제 하드웨어에서 수행해야 함**
  (본 실행자는 SIMUL_MODE 없이 실카메라를 구동할 수 없어 코드 수정·빌드까지만 담당했다.)

## 변경 내용

### Task 1 — GenRectangle2 인자 순서 교정 2곳 (커밋 `bc7af209`)

두 메서드(`TryFindLargestContourRect` 원 `:837`, `TryFindShortAxisIntersections` 원 `:965`)
모두 동일한 버그 한 줄을 갖고 있었다:

```csharp
HOperatorSet.GenRectangle2(out rect, cRow, cCol, cPhi, roiLength1, roiLength2);
```

측정 `Rect_*` ROI 규약은 `Length1 = 행(세로) 반폭`, `Length2 = 열(가로) 반폭`인데(`MainView`
티칭 드래그완료/리사이즈/표시 3경로가 전부 이 매핑), HALCON `gen_rectangle2` 는
`Length1 = Phi 방향 반장축`이라 `cPhi=0`일 때 Phi 방향은 열(가로)이다 — 즉 축이 반대다.
두 곳 각각 "왜" 주석(우리 Rect 규약 / HALCON 규약과 축 반대 + 강체 회전이므로 `cPhi != 0`
에서도 대응 유지 / 혼동 금지 대상 — `TryFindCircleByPolarSampling`·`PatternMatchService`·
`FAIEdgeMeasurementService`는 별개 경로)을 추가하고, 이름 있는 로컬 2개로 뒤집었다:

```csharp
double dGenLen1 = roiLength2; // HALCON Length1 = Phi 방향 반장축 -> 열(가로) 반폭
double dGenLen2 = roiLength1; // HALCON Length2 = Phi 수직 방향 반장축 -> 행(세로) 반폭
HOperatorSet.GenRectangle2(out rect, cRow, cCol, cPhi, dGenLen1, dGenLen2);
```

**최종 `GenRectangle2` 호출 라인 (두 메서드 동일):**
```csharp
HOperatorSet.GenRectangle2(out rect, cRow, cCol, cPhi, dGenLen1, dGenLen2);
```

`dGenLen1`/`dGenLen2` 라는 이름 있는 로컬을 남긴 것은 의도적 결정이다 — 인라인으로
`roiLength2, roiLength1` 만 뒤집으면 Task 2 의 로그가 실제로 넘긴 값을 재사용할 수 없어
버그를 감지하지 못한다(수정 전후 로그 출력이 같아짐).

### Task 2 — GenRectangle2 직후 관측 로그 추가 (커밋 `6c14573a`)

각 메서드의 `GenRectangle2` 바로 다음 줄, `ReduceDomain` 앞에 로그를 추가했다.
`height`/`width` 는 **`dGenLen2`/`dGenLen1`(=HALCON 에 실제로 넘긴 값)에서 계산**해,
`roiLength1`/`roiLength2`(티칭 필드 원본)와 나란히 찍음으로써 로그 한 줄 안에서
`height == 2*halfRow`, `width == 2*halfCol` 불변식을 판정할 수 있게 했다.

`TryFindLargestContourRect` — `[ContourRect]`:
```csharp
double dHeightPx = dGenLen2 + dGenLen2; // 행(세로) 전체 길이
double dWidthPx = dGenLen1 + dGenLen1; // 열(가로) 전체 길이
Logging.PrintLog((int)ELogType.Algorithm,
    string.Format("[ContourRect] roi: row={0:F1} col={1:F1} phi={2:F4}  halfRow={3:F1} halfCol={4:F1}  gen_rectangle2: height={5:F1} width={6:F1}",
        cRow, cCol, cPhi, roiLength1, roiLength2, dHeightPx, dWidthPx));
```

`TryFindShortAxisIntersections` — `[ShortAxis]`: 동일 포맷, 태그만 `[ShortAxis]`로 교체.

`ELogType.Algorithm` 채널, `using` 추가 없음(`ReringProject.Setting`/`ReringProject.Utility`
기존 사용). 로그 추가 외 동작 변경 없음.

### Task 3 — 빌드 및 하드룰 최종 검증 (커밋 없음, 검증 전용)

- MSBuild `Debug|x64` — **에러 0** (경고는 기존 수준의 `CS0618`/`CS0169` 뿐, 이번 변경과 무관)
- 하드룰 grep 5종(삼항/null 병합/null 조건/switch 식/날짜 주석) — 추가 라인 기준 **전부 0**
- 금지 파일 5개(`DatumMeasurement.csproj`, `FAIEdgeMeasurementService.cs`,
  `PatternMatchService.cs`, `MainView.xaml.cs`, `Custom/.../Measurements/`) — **미접촉 확인**
- 이번 작업이 만진 파일 — `VisionAlgorithmService.cs` **단 1개** 확인 (`git diff --name-only HEAD~2 HEAD`)
- 같은 파일 내 `TryFitLine`/`TryFindCircleByPolarSampling`/`GenMeasureRectangle2` 관련
  코드 변경 0건(`TryFindCircleByPolarSampling` 언급은 혼동 금지 주석 1줄뿐, 로직 무변경)

## 영향 범위 (반드시 남겨야 하는 항목)

**측정 타입 4종 / 서비스 메서드 2개 / 운영 레시피 항목 6개**가 이 수정의 영향을 받는다.

서비스 메서드 2개와 호출부:
- `TryFindLargestContourRect` ← `CompoundAngleMeasurement`(1) / `CompoundCenterBDistanceMeasurement`(1) /
  `CompoundCenterCDistanceMeasurement`(1) — 3곳
- `TryFindShortAxisIntersections` ← `CompoundShortAxisDistanceMeasurement`(1곳)

운영 레시피(`D:/Data/Recipe/FAI_1/main.ini`) 실제 존재 항목 6개 — **6개 전부 `Rect_Length1 != Rect_Length2`**,
`Rect_Phi` 는 전부 0:

| 섹션 | Type | Rect_Length1 | Rect_Length2 |
|---|---|---|---|
| `[SHOT_7_FAI_1_MEAS_0]` | `CompoundAngle` | 189 | 199 |
| `[SHOT_7_FAI_2_MEAS_0]` | `CompoundShortAxisDistance` | 207 | 214 |
| `[SHOT_7_FAI_5_MEAS_0]` | `CompoundCenterCDistance` | 177 | 181.5 |
| `[SHOT_7_FAI_6_MEAS_0]` | `CompoundCenterBDistance` | 169.5 | 172 |
| `[SHOT_14_FAI_0_MEAS_0]` | `CompoundCenterCDistance` | 213.5 | 241.5 |
| `[SHOT_15_FAI_0_MEAS_0]` | `CompoundCenterBDistance` | 198.5 | 211.5 |

→ **기존 티칭 항목의 측정값이 달라질 수 있다.** 달라진 값이 "사용자가 실제로 그린 박스대로
탐색한 값"이므로 이것이 정상이지만, **전 항목 재검증이 필요**하다. 차이가 5~13% 라
증상이 극적이지 않았을 뿐 탐색 영역은 조용히 어긋나 있었다.

→ **TOP / BOTTOM PC 에도 동일하게 해당된다.** 두 PC 모두 이번 커밋 pull → 리빌드 → 재검증해야 한다.

→ `260910-ly4`(측정 `Point_*` ROI, `TryFitLine`, 17곳/9종)와 **합쳐서** 재검증할 것.

## Phi != 0 관련 알아둘 것 (범위 밖, 기록용)

플랜의 `<phi_analysis>` 분석 결과: swap 은 `Rect_Phi` 값과 무관하게 항상 옳다
(`TransformMeasurementGeometry` 가 `Rect_Length1`/`Rect_Length2` 를 보존한 채 강체 회전만
적용하므로 대응 관계가 회전 불변). 다만 **표시(`BuildPointRoiDefinitions`)는 Phi 를 완전히
무시하고 항상 축정렬 AABB 를 그린다** — 따라서 `Rect_Phi != 0` 인 항목이 생기면 화면의
파란 박스(축정렬)와 실제 탐색 사각형(회전)이 **회전만큼** 어긋난다. 이는 `260910-ly4` 가
Point ROI 에서 명시적으로 범위 밖으로 둔 것과 동일한 **기존 표시 한계**이며 이번 수정
대상이 아니다. 현재 운영 레시피 6개는 전부 `Rect_Phi=0` 이라 육안 비교도 유효하지만,
캘리브/지오메트리 전체적용(`TransformMeasurementGeometry`) 경로로 `Rect_Phi != 0` 인
레시피가 앞으로 생길 수 있으므로 기록해 둔다.

## 실기 UAT — 사용자가 직접 수행 (미완료)

- [ ] **1. `[ContourRect]` / `[ShortAxis]` bounds 로그 불변식 확인 (핵심 판정)**
      Compound 측정이 포함된 Shot(예: `SHOT_7`, `SHOT_14`, `SHOT_15`)을 오프라인 검사 →
      `D:\Data\Algorithm\` 의 당일 `*_Algorithm.log` 에서 해당 줄을 찾는다.
      **판정은 절대 좌표가 아니라 로그 한 줄 안의 불변식으로 한다:**
      ```
      height == 2 * halfRow      (halfRow = Rect_Length1, 행/세로)
      width  == 2 * halfCol      (halfCol = Rect_Length2, 열/가로)
      ```
      예) `[SHOT_7_FAI_1_MEAS_0]`(CompoundAngle, L1=189 L2=199) →
      `halfRow=189.0 halfCol=199.0  gen_rectangle2: height=378.0 width=398.0`.
      **버그 상태면 정확히 반대**(`height=398.0 width=378.0`)로 찍힌다. 이 한 줄이 성공/실패를 가른다.
      ※ `row`/`col` 절대 좌표는 datum 런타임 보정만큼 티칭 값에서 이동해 있으며, 사용자가
      ROI 를 옮겼을 수도 있다. 절대 좌표로 판정하지 말 것.

- [ ] **2. 검출 결과가 그린 박스 안인지 육안 확인**
      해당 Compound 측정의 검출 결과(LargestRect / 교점 / 측정선)가 화면의 파란 박스 **안**에
      들어오는지 확인. (현재 6개 항목은 전부 `Rect_Phi=0` 이라 화면 박스와 탐색 박스의 회전이
      일치하므로 육안 비교가 유효하다.)

- [ ] **3. Compound 4종 측정값 공차 확인**
      `CompoundAngle` / `CompoundCenterBDistance` / `CompoundCenterCDistance` /
      `CompoundShortAxisDistance` 6개 항목의 측정값이 공차 안에 드는지 확인.
      **수정 전후 값이 달라질 수 있으며 그것이 정상**이다 (위 영향 범위 참조).
      공칭/공차 기준 재확인이 필요하면 재티칭한다.

- [ ] **4. `260910-ly4` 회귀 확인**
      `EdgeToLineDistance` 등 `TryFitLine` 계열(측정 타입 9종, 17곳)이 회귀하지 않았는지 확인.
      `[FitLine] strip-loop: bounds ...` 로그의 `bottom-top == 2*Point_Length1`,
      `right-left == 2*Point_Length2` 불변식도 함께 재확인.

## 자가 검증 (self-check)

- `git log -2 --oneline` → `bc7af209`, `6c14573a` 두 커밋 확인됨
- `WPF_Example/Halcon/Algorithms/VisionAlgorithmService.cs` 존재 확인됨
- 빌드 산출물 `C:\code\DataMeasurement\WPF_Example\bin\x64\Debug\DatumMeasurement.exe` 생성됨

## Deviations from Plan

None — plan executed exactly as written. Task 1/2 는 계획된 형태(이름 있는 로컬
`dGenLen1`/`dGenLen2` + 그 로컬에서 파생된 `height`/`width` 로그) 그대로 적용했고,
Task 3 검증에서 재수정이 필요한 항목이 없었다.

## Self-Check: PASSED
