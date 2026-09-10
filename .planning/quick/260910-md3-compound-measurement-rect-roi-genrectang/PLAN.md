---
task: 260910-md3-compound-measurement-rect-roi-genrectang
type: quick
mode: bugfix
autonomous: false
follows: 260910-ly4-measurement-point-roi-search-region-axis
files_modified:
  - WPF_Example/Halcon/Algorithms/VisionAlgorithmService.cs
tasks: 3
commits: 2
must_haves:
  truths:
    - "TryFindLargestContourRect 가 만드는 탐색 사각형의 행(세로) 반폭은 Rect_Length1, 열(가로) 반폭은 Rect_Length2 다."
    - "TryFindShortAxisIntersections 도 동일하다."
    - "GenRectangle2 직후 [ContourRect] / [ShortAxis] bounds 로그가 ELogType.Algorithm 으로 남는다."
    - "로그 한 줄 안에서 height == 2*halfRow, width == 2*halfCol 가 성립한다 (버그 상태면 정확히 반대로 찍힌다)."
    - "Debug|x64 빌드 에러 0."
  artifacts:
    - WPF_Example/Halcon/Algorithms/VisionAlgorithmService.cs
  key_links:
    - "MainView.BuildPointRoiDefinitions 의 표시 박스 <-> TryFindLargestContourRect / TryFindShortAxisIntersections 의 탐색 박스가 동일 규약"
---

<objective>
Compound 측정 4종이 쓰는 `VisionAlgorithmService.TryFindLargestContourRect` 와
`TryFindShortAxisIntersections` 가 측정 `Rect_*` ROI 의 `Length1`/`Length2` 를
HALCON `gen_rectangle2` 규약에 그대로 밀어넣어, **중심은 같고 가로·세로가 뒤바뀐
(transpose)** 사각형을 `ReduceDomain` 대상으로 삼는 버그를 고친다.
그리고 실기에서 "그린 박스대로 탐색하는가"를 사용자가 로그 한 줄로 판정할 수 있도록
`GenRectangle2` 직후에 관측 로그를 추가한다.

`260910-ly4` 가 측정 `Point_*` ROI(`TryFitLine`)에서 고친 것과 **같은 클래스의 버그**이며,
이번은 측정 `Rect_*` ROI(Compound 4종) 경로다.

Purpose: 사용자가 그린 박스와 실제 탐색 영역이 어긋난 채 조용히 측정되던 상태 제거.
Output: `VisionAlgorithmService.cs` 한 파일 수정 (인자 순서 2곳 + 관측 로그 2곳).
</objective>

<verified_facts>
계획 작성 시점(2026-09-10, `main` @ `e72a08e9`)에 플래너가 **직접 재확인**한 사실.
`260910-ly4` 커밋 2개가 이미 반영된 워킹트리 기준이며, 아래 라인 번호는 **현재 정확**하다.
그래도 실행 전에 라인 번호가 아니라 **코드 내용(grep)** 으로 위치를 확정할 것.

## 1) 이 코드베이스에는 Length1/Length2 규약이 **두 개** 공존한다

`Length1`/`Length2` 라는 이름은 HALCON `gen_rectangle2` / `gen_measure_rectangle2` 에서
왔고, HALCON 규약은 **`Length1` = Phi 방향 반장축, `Length2` = Phi 수직 방향 반장축** 이다.
즉 **이름만으로는 가로/세로가 정해지지 않는다.** 어느 축인지는 그 필드에 값을 써넣는
티칭 코드를 봐야만 알 수 있다. 플래너가 전수 재확인한 결과:

| ROI 종류 | 티칭이 Length1 에 쓰는 값 | 탐색 쪽 해석 | 상태 |
|---|---|---|---|
| Datum `PatternRoi_*` | `MainView.xaml.cs:4024` `PatternRoi_Length1 = halfW; // X축 절반` (`:4025` `Length2 = halfH; // Y축 절반`) | `PatternMatchService.cs:173` `GenRectangle2(..., roiLen1, roiLen2)` | HALCON 규약끼리 일치 — **정상. 건드리지 말 것** |
| FAI `ROI_*` | 드래그 bbox X절반 → `ROI_Length1` | `FAIEdgeMeasurementService.cs:127~136` cos/sin AABB | 별개 규약, **CONTEXT.md D-02 LOCKED** (`:131` 에 "swap 금지" 주석) — **읽기만** |
| 측정 `Point_*` 등 | `mHalfHeight` (**세로**) | `VisionAlgorithmService.TryFitLine` | **260910-ly4 에서 수정 완료. 재수정 금지** |
| **측정 `Rect_*` (Compound 4종)** | **`mHalfHeight` (세로)** | **`GenRectangle2(..., roiLength1, roiLength2)`** | **← 이번에 고칠 버그** |

## 2) 티칭 — `Rect_Length1 = 행(세로) 반폭`, `Rect_Length2 = 열(가로) 반폭`

`WPF_Example/UI/ContentItem/MainView.xaml.cs`

- **드래그 완료 (`:2803~2819`)**
  ```
  :2803  double mHalfHeight = (measRoi.Row2 - measRoi.Row1) / 2.0;
  :2804  double mHalfWidth  = (measRoi.Column2 - measRoi.Column1) / 2.0;
  :2813  cAngle.Rect_Phi = 0.0;   cAngle.Rect_Length1 = mHalfHeight;   cAngle.Rect_Length2 = mHalfWidth;
  :2815  cCenterC.Rect_Phi = 0.0; cCenterC.Rect_Length1 = mHalfHeight; cCenterC.Rect_Length2 = mHalfWidth;
  :2817  cCenterB.Rect_Phi = 0.0; cCenterB.Rect_Length1 = mHalfHeight; cCenterB.Rect_Length2 = mHalfWidth;
  :2819  cShort.Rect_Phi = 0.0;   cShort.Rect_Length1 = mHalfHeight;   cShort.Rect_Length2 = mHalfWidth;
  ```
  → **`Rect_Phi` 는 티칭 시 항상 `0.0` 으로 리셋된다** (요청받은 확인 항목 1, 확인됨).
- **리사이즈 (`:979~985`)** — `Rect_Length1 = halfR; Rect_Length2 = halfC;` (행/열, 동일 매핑)
- **클리어 (`:1011~1017`)** — 전부 0
- **표시 (`BuildPointRoiDefinitions`, `:507~523`)**
  ```
  :507  if (cAngle != null)   { pRow = cAngle.Rect_Row;   pCol = cAngle.Rect_Col;   pLen1 = cAngle.Rect_Length1;   pLen2 = cAngle.Rect_Length2; }
  :509  cCenterC / :511 cCenterB / :513 cShort — 동일
  :518  Row1    = pRow - pLen1,
  :519  Column1 = pCol - pLen2,
  :520  Row2    = pRow + pLen1,
  :521  Column2 = pCol + pLen2
  ```
  → 표시도 `Length1 = 행`, `Length2 = 열`. **티칭과 표시는 서로 일치한다. UI 쪽은 옳다.**

## 3) 버그 — `VisionAlgorithmService.cs` 2곳 (파일 총 1102줄)

두 곳 모두 **완전히 동일한 한 줄**이며, 들여쓰기는 공백 16칸, 파일 스타일은 **Allman**:

```
:837   HOperatorSet.GenRectangle2(out rect, cRow, cCol, cPhi, roiLength1, roiLength2);   // TryFindLargestContourRect (메서드 :796, 파라미터 :798)
:965   HOperatorSet.GenRectangle2(out rect, cRow, cCol, cPhi, roiLength1, roiLength2);   // TryFindShortAxisIntersections (메서드 :911, 파라미터 :913)
```

`grep -n "GenRectangle2\|GenMeasureRectangle2" WPF_Example/Halcon/Algorithms/VisionAlgorithmService.cs`
→ `310`, `525`(둘 다 `GenMeasureRectangle2`, 무관), `837`, `965`. **고칠 대상은 837 / 965 딱 2곳.**

HALCON 규약상 4번째 인자(`Length1`)는 **Phi 방향** 반장축이다. `cPhi = 0` 이면
Phi 방향 = **열(가로)** 축이므로, 우리 `roiLength1`(세로 값)이 가로로 들어간다.

## 4) 호출부 4곳 — 전부 `Rect_Length1, Rect_Length2` 를 그대로 넘긴다

| 파일 | 라인 | 호출 |
|---|---|---|
| `Custom/Sequence/Inspection/Measurements/CompoundAngleMeasurement.cs` | `:116~117` | `TryFindLargestContourRect` |
| `Custom/Sequence/Inspection/Measurements/CompoundCenterBDistanceMeasurement.cs` | `:115~116` | `TryFindLargestContourRect` |
| `Custom/Sequence/Inspection/Measurements/CompoundCenterCDistanceMeasurement.cs` | **`:115~116`** | `TryFindLargestContourRect` |
| `Custom/Sequence/Inspection/Measurements/CompoundShortAxisDistanceMeasurement.cs` | `:66~67` | `TryFindShortAxisIntersections` |

(요청받은 확인 항목 3: `CompoundCenterCDistanceMeasurement.cs` 호출부는 **`:115`**,
`Rect_*` 인자 줄은 `:116`. CenterB 와 동일한 라인 번호다.)
**호출부는 이번에 수정하지 않는다** — 서비스 내부에서 규약 변환을 끝낸다.

## 5) 운영 레시피 실측 (`D:/Data/Recipe/FAI_1/main.ini`, 읽기 전용)

`grep -c "Rect_Phi"` → **6**, 그리고 `grep "Rect_Phi" | sort | uniq -c` → **`6 Rect_Phi=0`**
(요청받은 확인 항목 1의 후반부: 현재 레시피의 `Rect_Phi` 는 **전부 0**, 확인됨).

플래너가 섹션별 `Type=` 까지 대조한 6개 항목 (**6개 전부 `Length1 != Length2`**):

| 섹션 | Type | Rect_Length1 (행/세로 반폭) | Rect_Length2 (열/가로 반폭) |
|---|---|---|---|
| `[SHOT_7_FAI_1_MEAS_0]` | `CompoundAngle` | 189 | 199 |
| `[SHOT_7_FAI_2_MEAS_0]` | `CompoundShortAxisDistance` | 207 | 214 |
| `[SHOT_7_FAI_5_MEAS_0]` | `CompoundCenterCDistance` | 177 | 181.5 |
| `[SHOT_7_FAI_6_MEAS_0]` | `CompoundCenterBDistance` | 169.5 | 172 |
| `[SHOT_14_FAI_0_MEAS_0]` | `CompoundCenterCDistance` | 213.5 | 241.5 |
| `[SHOT_15_FAI_0_MEAS_0]` | `CompoundCenterBDistance` | 198.5 | 211.5 |

타입별 집계: `CompoundAngle` 1 / `CompoundShortAxisDistance` 1 /
`CompoundCenterCDistance` 2 / `CompoundCenterBDistance` 2 = **총 6개**
(오케스트레이터 집계와 일치. 값-타입 대응만 위 표 기준으로 정정).

차이가 5~13% 라 증상이 극적이지 않아 지금까지 드러나지 않았을 뿐, 탐색 영역은
조용히 어긋난 상태다.

## 6) 로깅 인프라 — `using` 추가 불필요

`VisionAlgorithmService.cs` 상단:
```
:4  using ReringProject.Setting;   // ELogType
:5  using ReringProject.Utility;   // Logging
```
기존 `Logging.PrintLog((int)ELogType.Algorithm, ...)` 호출: `:145`, `:203`, `:235`.
`ELogType.Algorithm` 저장 경로: `SystemSetting.AlgorithmLogSavePath` 기본값 `D:\Data\Algorithm`
(`Setting/SystemSetting.cs:105`, 등록은 `SystemHandler.cs:97`).

**선례 포맷 — `260910-ly4` 가 추가한 `VisionAlgorithmService.cs:138~149`:**
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
(원 선례는 `DatumFindingService.cs` 의 `[Datum.*] strip-loop(extract)` 로그.)
</verified_facts>

<phi_analysis>
**요청받은 확인 항목 2 — "Phi 가 0 이 아닌 값이 들어오면 swap 이 여전히 옳은가?"**

## Phi 가 0 이 아니게 될 수 있는 경로가 실제로 존재한다

`MainView.xaml.cs:1037` `TransformMeasurementGeometry(MeasurementBase m, HTuple T)` —
캘리브/지오메트리 전체적용 경로 (`:1031` `RotAngleOf(T)` = `Atan2(-T[1], T[0])`):
```
:1063  cAngle.Rect_Phi   = cAngle.Rect_Phi   + rot;
:1065  cCenterC.Rect_Phi = cCenterC.Rect_Phi + rot;
:1067  cCenterB.Rect_Phi = cCenterB.Rect_Phi + rot;
:1069  cShort.Rect_Phi   = cShort.Rect_Phi   + rot;
```
중심은 `TransformPointInPlace` 로 옮기고 **`Rect_Length1`/`Rect_Length2` 는 보존**한다
(강체 회전). 따라서 `Rect_Phi != 0` 인 레시피가 앞으로 생길 수 있다.
(다만 **현재 운영 레시피 6개는 전부 `Rect_Phi=0`** — 위 `<verified_facts>` 5 참조.)

## 결론: swap 은 Phi 와 무관하게 항상 옳다

우리 규약은 **ROI 자기 좌표계(Phi=0 기준)** 에서 `Length1 = 행 반폭`, `Length2 = 열 반폭` 이다.
HALCON `gen_rectangle2(row, col, phi, len1, len2)` 는
`len1 = Phi 방향 반장축`, `len2 = Phi 수직 방향 반장축` 이다.

- `phi = 0` 일 때: Phi 방향 = **+열(가로)** 축, 수직 방향 = **행(세로)** 축.
  → 4번째 인자에 우리 `Length2`(열 반폭), 5번째 인자에 우리 `Length1`(행 반폭).
- `phi = θ` 로 회전할 때: `TransformMeasurementGeometry` 는 박스를 **강체 회전** 시키고
  `Length1`/`Length2` 를 보존한다. 즉 "열 방향이었던 축"이 그대로 Phi 방향으로 따라 돌고,
  그 축의 반장축은 여전히 `Length2` 다. **대응 관계가 회전 불변으로 유지된다.**

→ **`GenRectangle2(out rect, cRow, cCol, cPhi, roiLength2, roiLength1)` 는 모든 `phi` 에 대해 옳다.**
`datumTransform` 이 `cPhi = roiPhi + rotAngle` 로 런타임 회전을 더하는 경우
(`:822~832`, `:949~959`)에도 같은 논리로 유지된다.

## 알아둘 것 — Phi != 0 이면 "화면 박스"와 "탐색 박스"는 회전만큼 달라진다 (이번 범위 밖)

표시(`BuildPointRoiDefinitions:518~521`)는 **Phi 를 완전히 무시**하고 항상 축정렬 AABB 를
그린다. 따라서 `Rect_Phi != 0` 인 항목은 파란 박스(축정렬)와 실제 탐색 사각형(회전)이
**회전만큼** 어긋난다. 이는 `260910-ly4` 가 Point ROI 에서 명시적으로 범위 밖으로 둔 것과
동일한 **기존 표시 한계**이며 이번 수정 대상이 아니다.
→ 그래서 UAT 판정 기준을 "화면 박스와 눈으로 비교"가 아니라 **로그 한 줄 안의 불변식**으로
잡는다 (`<uat>` 1번). 현재 6개 항목은 전부 `Rect_Phi=0` 이라 육안 비교도 유효하다.
</phi_analysis>

<design_note>
**왜 인자만 뒤집지 않고 이름 있는 로컬 2개를 두는가 (의도적 결정 — 실행자는 그대로 따를 것)**

Task 1 을 `GenRectangle2(..., roiLength2, roiLength1)` 한 줄로 끝내면 Task 2 의 로그가
**버그를 감지할 수 없다.** 로그가 `roiLength1`/`roiLength2` 필드에서 직접 폭·높이를
계산하면 수정 전후 출력이 똑같아서 UAT 1번이 무의미해진다.
(`260910-ly4` 는 `top/left/bottom/right` 가 버그 변수 `halfW`/`halfH` 에서 파생돼 있어
로그가 자동으로 버그를 드러냈지만, 여기는 그 구조가 아니다.)

그래서 **HALCON 에 실제로 넘기는 값을 이름 있는 로컬로 뽑고**, 로그의 `height`/`width` 는
**그 로컬(=실제 인자)에서** 계산한다. 그러면 로그 한 줄 안에
- `halfRow`/`halfCol` = 티칭 필드 원본값
- `height`/`width`   = 실제로 넘긴 인자에서 나온 값

두 계열이 함께 찍혀서 **`height == 2*halfRow && width == 2*halfCol`** 라는 자기완결적
불변식이 성립한다. 누군가 나중에 swap 을 되돌리면 로그가 즉시 뒤집힌다.
</design_note>

<tasks>

<task id="1" type="auto">
  <name>Task 1: GenRectangle2 인자 순서 교정 2곳 (Rect ROI 규약 -> HALCON 규약 변환)</name>
  <files>WPF_Example/Halcon/Algorithms/VisionAlgorithmService.cs</files>
  <action>
`VisionAlgorithmService.cs` 에서 아래 한 줄이 나오는 **2곳**(`TryFindLargestContourRect`
`:837`, `TryFindShortAxisIntersections` `:965`)을 찾는다. 라인 번호가 아니라 grep 으로 확정할 것:

    grep -n "GenRectangle2" WPF_Example/Halcon/Algorithms/VisionAlgorithmService.cs

두 곳 **각각**에서, 기존 한 줄
`HOperatorSet.GenRectangle2(out rect, cRow, cCol, cPhi, roiLength1, roiLength2);`
를 다음 형태로 교체한다 (두 곳 동일하게, 들여쓰기 공백 16칸 유지, Allman 스타일):

- 먼저 "왜" 주석 블록을 넣는다 (아래 주석 요건 참조).
- 이어서 HALCON 에 넘길 값을 이름 있는 `double` 로컬 2개로 뽑는다.
  로컬명은 헝가리언 `d` 접두를 붙여 `dGenLen1`, `dGenLen2` 로 한다.
  - `dGenLen1` 에는 `roiLength2`(열/가로 반폭) 를 대입 — HALCON `Length1` = Phi 방향 반장축.
  - `dGenLen2` 에는 `roiLength1`(행/세로 반폭) 를 대입 — HALCON `Length2` = Phi 수직 방향 반장축.
  - 각 대입 줄 끝에 어느 축인지 한 마디 주석을 단다.
- 그리고 `HOperatorSet.GenRectangle2(out rect, cRow, cCol, cPhi, dGenLen1, dGenLen2);` 로 호출한다.

`dGenLen1`/`dGenLen2` 는 **Task 2 의 로그에서 재사용**되므로 반드시 이 이름의 로컬로 남길 것
(`<design_note>` 참조). 인라인으로 `roiLength2, roiLength1` 만 뒤집어 쓰지 말 것.

**반드시 남길 "왜" 주석** — 날짜/이니셜 접두 주석 금지, 비자명한 "왜"만 간결히.
두 곳 모두에 넣되 두 번째는 짧게 줄여도 된다. 아래 3항목을 담을 것:
1. 측정 `Rect_*` ROI 규약은 `Length1 = 행(세로) 반폭`, `Length2 = 열(가로) 반폭` 이다
   (티칭 `MainView` 드래그 완료 / 리사이즈 / 표시 3경로가 전부 이 매핑).
2. HALCON `gen_rectangle2` 는 `Length1 = Phi 방향 반장축` 이라 **축이 반대**다.
   그래서 넘길 때 뒤집는다. 강체 회전이므로 `cPhi != 0` 에서도 이 대응은 유지된다.
3. **혼동 금지 대상**: 같은 파일의 `TryFindCircleByPolarSampling` (`halfL1/halfL2` 를
   `radius * ratio` 로 직접 계산하고 `rectPhi = thetaRad`(반경 방향) — 티칭 필드를 안 쓴다),
   그리고 `PatternMatchService`(Datum `PatternRoi_*`) / `FAIEdgeMeasurementService`
   (FAI `ROI_*`, CONTEXT.md **D-02 LOCKED**) 는 **HALCON 규약을 그대로 쓰는 별개 경로**다.
   이 뒤집기를 그쪽에 옮겨 붙이지 말 것.

**절대 건드리지 말 것 (이 한 줄 외 로직 변경 0):**
- 위쪽 `datumTransform` 블록 (`cRow`/`cCol`/`cPhi` 산출, `:822~832` / `:949~959`) — 그대로
- 이후 파이프라인 `ReduceDomain` → `EdgesSubPix` → `UnionAdjacentContoursXld` →
  `ShapeTransXld` → `AreaCenterXld` → `TupleMax/TupleFind` → `SelectObj` 이하 전부 — 그대로
- `finally` 의 Dispose 블록 — 그대로 (새 HALCON 객체를 만들지 않으므로 추가 Dispose 대상 없음)
- 메서드 시그니처 / 파라미터 이름 (`roiLength1`, `roiLength2`) — 그대로.
  호출부 4곳도 수정하지 않는다.
  </action>
  <verify>
```bash
cd /c/code/DataMeasurement
F=WPF_Example/Halcon/Algorithms/VisionAlgorithmService.cs

# 1) 두 곳 모두 dGenLen1/dGenLen2 로 호출하는지 (roiLength1/roiLength2 직접 전달이 0건이어야 함)
grep -n "GenRectangle2" "$F"
grep -c "GenRectangle2(out rect, cRow, cCol, cPhi, dGenLen1, dGenLen2);" "$F" || true   # 기대: 2
grep -c "GenRectangle2(out rect, cRow, cCol, cPhi, roiLength1, roiLength2);" "$F" || true # 기대: 0

# 2) 로컬 대입이 올바른 축인지 (dGenLen1 <- roiLength2, dGenLen2 <- roiLength1) 각 2건
grep -n "dGenLen1\|dGenLen2" "$F"

# 3) GenMeasureRectangle2(:310, :525) 는 미변경인지
grep -n "GenMeasureRectangle2" "$F"   # 기대: 310, 525 두 줄 그대로

# 4) diff 가 최소 범위인지
git diff --stat -- "$F"
git diff -U0 -- "$F"

# 5) 하드룰 — 추가 라인만 검사 (<hard_rules> 스크립트)
```
  </verify>
  <done>
- `:837` / `:965` 두 곳 모두 `GenRectangle2(out rect, cRow, cCol, cPhi, dGenLen1, dGenLen2)` 형태다.
- 두 곳 모두 `dGenLen1 = roiLength2`(열/가로), `dGenLen2 = roiLength1`(행/세로) 로 대입한다.
- 결과적으로 생성 사각형은 `phi=0` 기준 `높이 = 2*roiLength1`, `너비 = 2*roiLength2` 다.
- "왜" 주석 3항목(우리 Rect 규약 / HALCON 규약과 축 반대 + 회전 불변 / 혼동 금지 3경로)이 들어갔다.
- `GenMeasureRectangle2` 2곳, `datumTransform` 블록, 이후 파이프라인, 메서드 시그니처, 호출부 4곳 — 전부 무변경.
- 변경 파일 1개. 커밋 1개 (`git add` 는 이 파일 하나만, 경로 명시).
  </done>
  <commit>fix(quick-260910-md3): Compound Rect ROI 탐색영역 축 반전 수정 — GenRectangle2 인자 순서 교정 2곳</commit>
</task>

<task id="2" type="auto">
  <name>Task 2: GenRectangle2 직후 [ContourRect] / [ShortAxis] 관측 로그 추가 (2곳)</name>
  <files>WPF_Example/Halcon/Algorithms/VisionAlgorithmService.cs</files>
  <action>
Task 1 에서 고친 **두 곳 각각**, `HOperatorSet.GenRectangle2(...)` **바로 다음 줄**(
`HOperatorSet.ReduceDomain(...)` 앞)에 관측 로그를 추가한다. 두 메서드에는 현재
bounds 성격의 로그가 전혀 없어 실기 검증이 불가능하다. UAT 1번이 이 로그에 의존한다.

**태그는 메서드별로 구분한다:**
- `TryFindLargestContourRect` → `[ContourRect]`
- `TryFindShortAxisIntersections` → `[ShortAxis]`

**포맷** (두 곳 태그만 다르고 나머지는 동일. `260910-ly4` 의 `[FitLine] strip-loop:` /
`DatumFindingService` 의 `strip-loop(extract)` 와 같은 결로, 논리 그룹 사이는 **공백 2칸**):

```
[ContourRect] roi: row={0:F1} col={1:F1} phi={2:F4}  halfRow={3:F1} halfCol={4:F1}  gen_rectangle2: height={5:F1} width={6:F1}
```

**인자 매핑 — 여기가 이 태스크의 핵심이다:**
- `{0} {1} {2}` ← `cRow`, `cCol`, `cPhi` (datum 보정이 적용된 실제 사용값. 기존 로컬 재사용)
- `{3} halfRow` ← `roiLength1` (티칭 필드 원본 = 행/세로 반폭)
- `{4} halfCol` ← `roiLength2` (티칭 필드 원본 = 열/가로 반폭)
- `{5} height`  ← **`dGenLen2` 로부터** 계산한 전체 높이
- `{6} width`   ← **`dGenLen1` 로부터** 계산한 전체 너비

`height`/`width` 는 **반드시 Task 1 에서 만든 `dGenLen2`/`dGenLen1`(= HALCON 에 실제로 넘긴 값)
에서 계산**한다. `roiLength1`/`roiLength2` 에서 직접 계산하면 로그가 버그를 감지하지 못해
UAT 1번이 무의미해진다 (`<design_note>` 참조).

전체 길이는 매직넘버를 피하기 위해 **덧셈 형태**로 이름 있는 로컬에 담는다:
```csharp
double dHeightPx = dGenLen2 + dGenLen2;   // 행(세로) 전체 길이
double dWidthPx  = dGenLen1 + dGenLen1;   // 열(가로) 전체 길이
```
(리터럴 `2.0` 을 쓰지 않으므로 매직넘버 논쟁 없음. 로컬명은 헝가리언 `d` 접두.)

- 출력 채널: `Logging.PrintLog((int)ELogType.Algorithm, string.Format(...))` — 기존 `:145` 호출과 동일.
  `using` 추가 **불필요** (`:4` `ReringProject.Setting`, `:5` `ReringProject.Utility` 이미 존재).
- 로그 위에 한 줄 주석: 이 로그로 "그린 박스대로 탐색하는가"를 실기에서 판정한다는 취지와,
  판정 불변식 `height == 2*halfRow`, `width == 2*halfCol` 를 적는다.
- 조건 분기 없음 → 조건 연산자 이슈 없음. 새 HALCON 객체 없음 → Dispose 대상 없음.
- **기존 로컬 이름은 바꾸지 말 것** (`cRow`, `cCol`, `cPhi`, `rect` 등). 신규 식별자만 헝가리언 적용.
- 로그 추가 외 **로직 변경 0**. `try`/`finally` 구조 손대지 말 것.
  </action>
  <verify>
```bash
cd /c/code/DataMeasurement
F=WPF_Example/Halcon/Algorithms/VisionAlgorithmService.cs

# 1) 두 태그가 각각 1건씩 있는지
grep -c "\[ContourRect\] roi:" "$F" || true   # 기대: 1
grep -c "\[ShortAxis\] roi:"   "$F" || true   # 기대: 1

# 2) 로그가 GenRectangle2 직후 / ReduceDomain 직전인지 (라인 번호 순서 확인)
grep -n "GenRectangle2\|ContourRect\|ShortAxis\] roi\|ReduceDomain\|dHeightPx\|dWidthPx" "$F"
#    기대 순서(각 메서드마다): GenRectangle2 < dHeightPx/dWidthPx < 로그 < ReduceDomain

# 3) height/width 가 dGenLen2/dGenLen1 에서 나오는지 (roiLength* 직접 사용 금지)
grep -n "dHeightPx\s*=\|dWidthPx\s*=" "$F"
#    기대: dHeightPx = dGenLen2 + dGenLen2 / dWidthPx = dGenLen1 + dGenLen1  (각 2건)

# 4) 로그 추가 외 로직 변경 없는지
git diff -U0 -- "$F"

# 5) 하드룰 — 추가 라인만 검사 (<hard_rules> 스크립트)
```
  </verify>
  <done>
- 두 메서드 모두 `GenRectangle2` 직후 / `ReduceDomain` 직전에 로그가 있다.
- 태그가 `[ContourRect]` 와 `[ShortAxis]` 로 구분된다.
- 한 줄 안에 `row/col/phi`, `halfRow`(=`roiLength1`), `halfCol`(=`roiLength2`),
  `height`(=`dGenLen2` 파생), `width`(=`dGenLen1` 파생) 가 모두 찍힌다.
- `dHeightPx`/`dWidthPx` 가 `dGenLen2`/`dGenLen1` 에서 계산된다 (`roiLength*` 직접 사용 아님).
- `ELogType.Algorithm` 채널, `using` 추가 없음.
- 로그 추가 외 동작 변경 없음 (`git diff -U0` 로 확인).
- 커밋 1개 (`git add` 는 이 파일 하나만, 경로 명시).
  </done>
  <commit>feat(quick-260910-md3): Compound Rect ROI 탐색영역 bounds 관측 로그 추가 ([ContourRect]/[ShortAxis])</commit>
</task>

<task id="3" type="auto">
  <name>Task 3: Debug/x64 빌드 에러 0 확인 + 하드룰·금지파일 최종 검증 (커밋 없음)</name>
  <files>(없음 — 검증 전용)</files>
  <action>
빌드와 하드룰 grep, 금지 파일 미접촉을 확인해 회귀가 없음을 확정한다.
**이 태스크는 파일을 만들지도 커밋하지도 않는다.** 실패하면 `VisionAlgorithmService.cs`
를 고쳐 Task 1/2 커밋을 `--amend` 하거나 추가 fix 커밋을 낸다
(수정 대상 파일은 `VisionAlgorithmService.cs` 하나로 유지).
  </action>
  <verify>
```bash
cd /c/code/DataMeasurement

# 1) Debug|x64 빌드 — 에러 0
"C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" \
  WPF_Example/DatumMeasurement.csproj \
  //p:Configuration=Debug //p:Platform=x64 //v:minimal //nologo

# 2) 하드룰 — 이번에 추가한 라인만 검사 (<hard_rules> 스크립트). 전부 0 이어야 함

# 3) 금지 파일 미접촉 확인 — 아래 5개는 출력이 없어야 한다
git status --porcelain WPF_Example/DatumMeasurement.csproj
git status --porcelain WPF_Example/Halcon/Algorithms/FAIEdgeMeasurementService.cs
git status --porcelain WPF_Example/Halcon/Algorithms/PatternMatchService.cs
git status --porcelain WPF_Example/UI/ContentItem/MainView.xaml.cs
git status --porcelain WPF_Example/Custom/Sequence/Inspection/Measurements/

# 4) 이번 작업이 만진 소스 파일이 정확히 1개인지
git diff --name-only HEAD~2 HEAD
#    기대: WPF_Example/Halcon/Algorithms/VisionAlgorithmService.cs 만

# 5) 같은 파일 내 무관 지점 미변경 확인
git diff -U0 HEAD~2 HEAD -- WPF_Example/Halcon/Algorithms/VisionAlgorithmService.cs | grep '^[-+]' | grep -v '^[-+][-+]'
#    기대: GenRectangle2 2곳 + dGenLen*/dHeightPx/dWidthPx + 로그 + 주석만.
#    TryFitLine(:20~), TryFindCircleByPolarSampling(:435~), GenMeasureRectangle2(:310/:525) 관련 라인 0건
```
  </verify>
  <done>
- MSBuild `Debug|x64` 에러 0 (경고는 기존 수준 `CS0618`/`CS0169` 유지).
- 하드룰 grep 5종 모두 추가 라인 기준 0.
- `DatumMeasurement.csproj`, `FAIEdgeMeasurementService.cs`, `PatternMatchService.cs`,
  `MainView.xaml.cs`, `Custom/.../Measurements/` 전부 변경 없음.
- 이번 작업의 변경 소스 파일이 `VisionAlgorithmService.cs` 단 하나, 커밋 2개.
- 같은 파일의 `TryFitLine` / `TryFindCircleByPolarSampling` / `GenMeasureRectangle2` 는 무변경.
  </done>
</task>

</tasks>

<hard_rules>
CLAUDE.md 강제 규칙. **위반 = 회귀.** 이번에 추가·수정한 라인 기준으로 전부 0 이어야 한다.

금지 항목 (신규/수정 라인에서):
1. 삼항 조건 연산자 → 반드시 `if` / `else`
2. null 병합 연산자 (2문자 물음표 형태, 대입형 포함) → 명시적 null 분기
3. null 조건 접근 연산자 (물음표+점, 물음표+대괄호) → 명시적 null 체크
4. C# 8.0 switch 식 (화살표 형태) → 전통 `switch` 문, `case` 마다 `break`
5. 날짜+이니셜 접두 주석 (`//YYMMDD` + 작성자 이니셜 형태) → 신규 금지. 비자명한 "왜"만
6. C# 8.0+ 문법 전반 (nullable 참조형식, `record`, 신 패턴매칭) → C# 7.2 만
7. 중괄호 생략 → 한 줄 분기라도 필수
8. 매직넘버 → 이름 있는 `const`. (이번 작업은 리터럴 숫자를 아예 쓰지 않는다 —
   전체 길이는 `dGenLen2 + dGenLen2` 덧셈 형태로 만든다.)
9. 신규 식별자 헝가리언 접두: `b`(bool) `n`(int) `sz`(string) `d`(double) `hv`(HTuple)
   → 이번 신규 로컬은 `dGenLen1`, `dGenLen2`, `dHeightPx`, `dWidthPx` 전부 `d` 접두.
   ※ **기존 로컬(`cRow`, `cCol`, `cPhi`, `rect` 등) 이름은 바꾸지 말 것.** 신규만 적용.
10. `HImage`/`HObject`/`HTuple` Dispose — 이번 수정은 새 HALCON 객체를 만들지 않으므로 해당 없음
11. 파일 스타일은 **Allman** brace. 들여쓰기 공백 16칸(메서드 `try` 블록 내부) 유지.

**전체 파일 grep 금지 — 이 파일에는 기존 위반이 있어 항상 실패한다.**
(`260910-ly4` 가 확인한 대로 파일 내에 기존 삼항·날짜 주석이 존재한다.)
반드시 `git diff` 의 **추가 라인(`+`)만** 검사한다:

```bash
cd /c/code/DataMeasurement
F=WPF_Example/Halcon/Algorithms/VisionAlgorithmService.cs
OUT="$TMPDIR/md3_added.txt"; [ -z "$TMPDIR" ] && OUT=./md3_added.txt

# 커밋 전이면 아래 그대로. 커밋 후면 `git diff -U0 HEAD~2 HEAD -- "$F"` 로 바꿔 사용.
git diff -U0 -- "$F" | grep '^+' | grep -v '^+++' > "$OUT"

# grep -c 는 0건일 때 exit 1 이라 스크립트가 죽는다 → 반드시 `|| true` 를 붙일 것
grep -cE '\?[^?]*:'   "$OUT" || true   # 삼항       -> 0
grep -cF '??'         "$OUT" || true   # null 병합  -> 0
grep -cF '?.'         "$OUT" || true   # null 조건  -> 0
grep -cE 'switch.*=>' "$OUT" || true   # switch 식  -> 0
grep -cF 'hbk'        "$OUT" || true   # 날짜 주석  -> 0

rm -f "$OUT"   # 임시 파일은 저장소에 남기지 말 것
```
(임시 파일을 저장소 루트에 만들었다면 **반드시 삭제**하고, 절대 스테이징하지 말 것.)
</hard_rules>

<forbidden>
아래는 **절대 수정 금지**. 위반 시 회귀로 간주한다.

1. **`WPF_Example/Halcon/Algorithms/FAIEdgeMeasurementService.cs`**
   FAI `ROI_Length1`/`ROI_Length2` 는 **CONTEXT.md D-02 LOCKED** 이며 별개 규약·별개 티칭
   경로다. `:131` 에 "swap 금지" 주석이 이미 있다. **읽기만 하고 수정하지 말 것.**

2. **`WPF_Example/Halcon/Algorithms/PatternMatchService.cs`**
   Datum `PatternRoi_*` 는 티칭(`MainView:4024` `Length1 = halfW; // X축 절반`)과
   `:173 GenRectangle2(..., roiLen1, roiLen2)` 가 **HALCON 규약끼리 이미 일치**한다.
   정상이다. **읽기만 하고 수정하지 말 것.**

3. **`VisionAlgorithmService.cs` 의 `TryFindCircleByPolarSampling`** (`:435~`,
   `GenMeasureRectangle2` at `:525`) — `halfL1`/`halfL2` 를 `radius * ratio` 로 직접 계산하고
   `rectPhi = thetaRad`(반경 방향)라 **티칭 필드를 쓰지 않는다.** 정상. **건드리지 말 것.**

4. **`VisionAlgorithmService.cs` 의 `TryFitLine` / `AppendStrip`** (`:20~`) —
   `260910-ly4` 에서 이미 고쳤다. **재수정 금지.** `:310` 의 `GenMeasureRectangle2` 도 무관.

5. **`WPF_Example/UI/ContentItem/MainView.xaml.cs`**
   티칭(`:2813~2819`)·리사이즈(`:979~985`)·표시(`:507~521`) 3경로 전부 옳다 (플래너 재확인 완료).
   고칠 이유가 없다. 또한 CLAUDE.md 가 이 비대한 code-behind(4,400줄+)에 신규 로직 추가를 금지한다.

6. **`Custom/Sequence/Inspection/Measurements/Compound*.cs` 4개** —
   호출부는 `Rect_Length1, Rect_Length2` 를 그대로 넘기는 게 맞다. 규약 변환은 서비스 내부에서
   끝낸다. **수정하지 말 것.**

7. **`WPF_Example/DatumMeasurement.csproj`**
   이 PC 의 실HW 세팅이 들어 있다. **스테이징/커밋 절대 금지.**
   → 이 때문에 **신규 `.cs` 파일 생성 불가** (classic MSBuild 는 `<Compile Include>` 추가가
   필요하고 그러면 csproj 이 더러워진다). 기존 파일 안에서 해결할 것.
   ※ 계획 작성 시점 기준 csproj 에 미커밋 변경 없음(`git status --porcelain` 확인). 가드는 유지.

8. **`D:\Data\Recipe\FAI_1\main.ini`** — 운영 레시피. **읽기만, 쓰기 금지.**

9. **`.planning/STATE.md`** — 다른 PC 와 공유되어 병합 충돌이 잦다. **행 추가만** 하고
   기존 행은 건드리지 말 것.
</forbidden>

<git_constraints>
- 이 환경에서는 `git stash` / `git checkout -- <path>` / `git restore` 류의 **작업물 되돌리기
  명령이 차단**되어 있다. 실수로 덮어쓰면 복구가 어려우니 편집 전에 대상 위치를 grep 으로
  정확히 확정하고 최소 범위로 편집할 것.
- **`git add -A` / `git add .` 절대 금지.** 커밋마다 대상 경로를 명시:
  `git add WPF_Example/Halcon/Algorithms/VisionAlgorithmService.cs`
- 커밋 전 `git status --porcelain` 으로 `DatumMeasurement.csproj` 가 스테이지에 없는지 확인한다.
- 브랜치: `main`. 계획 작성 시점 워킹트리에는 `260910-ly4` 의 미추적 문서 2개
  (`PLAN.md`, `VERIFICATION.md`)만 있고 **소스 변경은 없다**. 이 2개는 이번 커밋에 넣지 말 것.
- 커밋 총 2개 (Task 1, Task 2). Task 3 은 검증 전용.
</git_constraints>

<impact_scope>
**SUMMARY.md 에 반드시 그대로 옮겨 적을 것.**

**측정 타입 4종 / 서비스 메서드 2개 / 운영 레시피 항목 6개**가 이 수정의 영향을 받는다.

서비스 메서드 2개와 호출부:
- `TryFindLargestContourRect` ← `CompoundAngleMeasurement:116`,
  `CompoundCenterBDistanceMeasurement:115`, `CompoundCenterCDistanceMeasurement:115` (3곳)
- `TryFindShortAxisIntersections` ← `CompoundShortAxisDistanceMeasurement:66` (1곳)

운영 레시피(`D:/Data/Recipe/FAI_1/main.ini`) 실제 존재 항목 6개 — **6개 전부 `Length1 != Length2`**,
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

→ **TOP / BOTTOM PC 에도 동일하게 해당된다.** 두 PC 모두 이번 커밋 pull → 리빌드 → 재검증.

→ `260910-ly4`(측정 `Point_*` ROI, `TryFitLine`, 17곳/9종)와 **합쳐서** 재검증할 것.
</impact_scope>

<uat>
실기 UAT — **사용자가 직접 수행.** SUMMARY.md 에 체크 항목으로 남길 것.

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
      ※ `row`/`col` 절대 좌표는 **datum 런타임 보정만큼 티칭 값에서 이동**해 있으며,
      사용자가 ROI 를 옮겼을 수도 있다. 절대 좌표로 판정하지 말 것.

- [ ] **2. 검출 결과가 그린 박스 안인지 육안 확인**
      해당 Compound 측정의 검출 결과(LargestRect / 교점 / 측정선)가 화면의 파란 박스 **안**에
      들어오는지 확인. (현재 6개 항목은 전부 `Rect_Phi=0` 이라 화면 박스와 탐색 박스의 회전이
      일치하므로 육안 비교가 유효하다.)

- [ ] **3. Compound 4종 측정값 공차 확인**
      `CompoundAngle` / `CompoundCenterBDistance` / `CompoundCenterCDistance` /
      `CompoundShortAxisDistance` 6개 항목의 측정값이 공차 안에 드는지 확인.
      **수정 전후 값이 달라질 수 있으며 그것이 정상**이다 (`<impact_scope>` 참조).
      공칭/공차 기준 재확인이 필요하면 재티칭한다.

- [ ] **4. `260910-ly4` 회귀 확인**
      `EdgeToLineDistance` 등 `TryFitLine` 계열(측정 타입 9종, 17곳)이 회귀하지 않았는지 확인.
      `[FitLine] strip-loop: bounds ...` 로그의 `bottom-top == 2*Point_Length1`,
      `right-left == 2*Point_Length2` 불변식도 함께 재확인.
</uat>

<success_criteria>
- `TryFindLargestContourRect` / `TryFindShortAxisIntersections` 의 탐색 사각형이
  `높이 = 2*Rect_Length1`(행), `너비 = 2*Rect_Length2`(열) 로 생성된다.
- 축 반전 규약 차이(측정 `Rect_*`/`Point_*` vs Datum `PatternRoi_*` / FAI `ROI_*` /
  `TryFindCircleByPolarSampling`)가 주석으로 남아 재발과 오적용을 막는다.
- `GenRectangle2` 직후 `[ContourRect]` / `[ShortAxis]` 로그가 `ELogType.Algorithm` 으로 남고,
  `height`/`width` 가 **실제로 넘긴 인자(`dGenLen2`/`dGenLen1`)** 에서 계산되어
  로그 한 줄만으로 `height == 2*halfRow`, `width == 2*halfCol` 를 판정할 수 있다.
- MSBuild `Debug|x64` 에러 0.
- 변경 파일 1개(`VisionAlgorithmService.cs`), 커밋 2개, 금지 파일 9종 미접촉.
- 하드룰 grep 5종 모두 추가 라인 기준 0.
- SUMMARY.md 에 영향 범위(측정 타입 4종 / 메서드 2개 / 레시피 항목 6개 표 + TOP/BOTTOM PC
  동일 적용) 와 UAT 4항목 기록.
</success_criteria>

<output>
`.planning/quick/260910-md3-compound-measurement-rect-roi-genrectang/SUMMARY.md`
</output>
