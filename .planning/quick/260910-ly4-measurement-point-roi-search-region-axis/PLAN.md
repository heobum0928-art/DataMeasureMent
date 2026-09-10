---
task: 260910-ly4-measurement-point-roi-search-region-axis
type: quick
mode: bugfix
autonomous: false
files_modified:
  - WPF_Example/Halcon/Algorithms/VisionAlgorithmService.cs
tasks: 3
commits: 2
must_haves:
  truths:
    - "TryFitLine 이 탐색하는 사각형의 세로 반폭은 roiLength1, 가로 반폭은 roiLength2 다."
    - "탐색 사각형의 높이(bottom-top)는 2*roiLength1, 너비(right-left)는 2*roiLength2 다."
    - "strip 루프 직전에 [FitLine] bounds 로그가 ELogType.Algorithm 으로 남는다."
    - "Debug|x64 빌드 에러 0."
  artifacts:
    - WPF_Example/Halcon/Algorithms/VisionAlgorithmService.cs
  key_links:
    - "MainView.BuildPointRoiDefinitions 의 표시 박스 <-> TryFitLine 의 탐색 박스가 동일 규약"
---

<objective>
`VisionAlgorithmService.TryFitLine` 이 측정 Point ROI 의 `Length1`/`Length2` 를
티칭·표시와 **반대 축**으로 해석해, 중심은 같지만 가로·세로가 뒤바뀐(transpose)
사각형을 탐색하는 버그를 고친다. 그리고 실기에서 탐색 영역을 눈으로 확인할 수
있도록 strip 루프 직전에 bounds 로그를 추가한다.

Purpose: 측정 ROI(파란 박스) 밖에서 에지가 검출되어 NG 가 나거나, ROI 를 조금만
옮기면 측정이 아예 실패하는 문제 제거.
Output: `VisionAlgorithmService.cs` 한 파일 수정 (축 매핑 1곳 + 관측 로그 1개).
</objective>

<verified_facts>
계획 작성 시점(2026-09-10)에 플래너가 **직접 재확인**한 사실. 실행 전 라인 번호가
또 밀렸을 수 있으니, 라인 번호가 아니라 **코드 내용**으로 위치를 찾을 것.

**버그 위치 — `WPF_Example/Halcon/Algorithms/VisionAlgorithmService.cs` :108~113**
(메서드 `public bool TryFitLine(...)` 는 :20 에서 시작, 파일 전체 1081줄)
```
:105  // 단일 MeasurePos 는 측정 축 1개에서만 에지 반환 → ... (기존 주석 3줄)
:108  double halfW = roiLength1;
:109  double halfH = roiLength2;
:110  double top = rRow - halfH;
:111  double bottom = rRow + halfH;
:112  double left = rCol - halfW;
:113  double right = rCol + halfW;
:114  double widthPx = right - left;
:115  double heightPx = bottom - top;
```
`halfW` / `halfH` 는 **이 6줄 안에서만** 쓰인다 (`grep -n "halfW\|halfH"` → :108~:113 뿐).
즉 매핑 한 곳만 바꾸면 `top/bottom/left/right/widthPx/heightPx` 와 strip 루프는
자동으로 맞는다.

**strip 루프 시작 — :130 `if (scanHorizontal)`**
그 앞은:
```
:117  // stripCount: sentinel 0 → 기본 20
:118  int stripCount = 20;
:119  if (sampleCount > 0) stripCount = sampleCount;
:120  if (stripCount < 1) stripCount = 1;
:122  HTuple allRows = new HTuple();
:123  HTuple allCols = new HTuple();
:125  // strip 성공/실패 관측용 카운터 (판정에는 미사용 — 로그 전용)
:126  int okStrips = 0;
:127  int noEdgeStrips = 0;
:128  int failedStrips = 0;
:130  if (scanHorizontal)
```
→ 신규 로그는 **:128 과 :130 사이**(빈 줄 자리)에 넣는다.

**기존 변수 (그대로 재사용, 새로 만들지 말 것)**
- `scanHorizontal` (:68 선언, TtoB/BtoT 일 때 false)
- `pol` (:80~:88, `"positive"` / `"negative"`) — 로그의 polarity 값
- `sigma`, `threshold` — 메서드 파라미터 원본값 (Datum 로그도 원본값을 찍는다)
- `stripCount` (:118~:120)

**따라 쓸 로그 포맷 — `WPF_Example/Halcon/Algorithms/DatumFindingService.cs` :1967~1976**
```csharp
string scanLabel = "vertical";
if (scanHorizontal) scanLabel = "horizontal";

Logging.PrintLog((int)ELogType.Algorithm,
    string.Format("[Datum.{0}] strip-loop(extract): bounds top={1:F1} left={2:F1} bottom={3:F1} right={4:F1}  scan={5}  stripCount={6}  sigma={7:F2} threshold={8} polarity={9}",
        lbl, top, left, bottom, right,
        scanLabel,
        stripCount, sigma, threshold, polarity));
```
`Logging` / `ELogType` 은 `VisionAlgorithmService.cs` 에서 이미 사용 중
(:183, :190) → **using 추가 불필요**.

**정답 규약 재확인 — `Length1 = 행(세로) 반폭`, `Length2 = 열(가로) 반폭`**
- 드래그 완료: `WPF_Example/UI/ContentItem/MainView.xaml.cs` :2806 부근
  `etld.Point_Length1 = mHalfHeight; etld.Point_Length2 = mHalfWidth;`
  (`mHalfHeight = (Row2-Row1)/2`, `mHalfWidth = (Column2-Column1)/2`)
- 리사이즈: 같은 파일 `ApplyPointRoiResize` :951~ `_Length1 = halfR; _Length2 = halfC;`
  (`halfR = (row2-row1)/2`, `halfC = (col2-col1)/2`)
- 표시: 같은 파일 `BuildPointRoiDefinitions` :428~
  `Row1 = X_Row - X_Length1, Column1 = X_Col - X_Length2` (역도 동일)
- 카운트 검증 (플래너가 직접 실행):
  `grep -c "_Length1 = mHalfHeight\|_Length1 = halfR"` → **27**
  `grep -c "_Length2 = mHalfHeight\|_Length2 = halfR"` → **0**
  → 반대 매핑 0곳. 티칭/편집/표시 3경로 전부 일관됨. **UI 쪽은 옳다.**

**대비되는 별개 규약 (읽기 전용, 절대 수정 금지)**
`WPF_Example/Halcon/Algorithms/FAIEdgeMeasurementService.cs` :128~131 에
`ROI_Length1 = phi 방향 반장축 / ROI_Length2 = phi 수직 방향 반장축`
(HALCON `gen_measure_rectangle2` 규약, "swap 금지 — CONTEXT.md D-02 LOCKED") 주석이
이미 있다. **측정 Point ROI 규약과 축이 반대**다. 이 차이를 Task 1 주석에 명시해
같은 실수의 재발을 막는다.
</verified_facts>

<orchestrator_data_corrections>
오케스트레이터가 넘긴 값 중 플래너 재확인 결과 **달라진 것 2가지**. 실행자는 아래를
기준으로 판단할 것.

**1) 호출부 개수 17곳 — 맞음 (단, grep 시 주의)**
`grep -rn "TryFitLine(" WPF_Example/ --include=*.cs` 는 20곳을 잡는데 (VisionAlgorithmService.cs:20 의 메서드 정의 자신 1곳 + RoiLineIntersectionAlgorithm.cs 2곳), 그 중 2곳은
`WPF_Example/Halcon/Algorithms/RoiLineIntersectionAlgorithm.cs` 의 **동명이인**
(`private static bool TryFitLine(HTuple rows, HTuple cols, out LineEquation line)`,
:136 선언 / :70 호출) 이며 이번 버그와 **무관**하다.
`VisionAlgorithmService.TryFitLine` 실호출은 측정 클래스 9종 **17곳**이 맞다:
`ArcEdgeDistanceMeasurement`(1), `ArcLineIntersectDistanceMeasurement`(4),
`DualImageEdgeDistanceMeasurement`(2), `EdgeToLineAngleMeasurement`(1),
`EdgeToLineDistanceMeasurement`(1), `LineToLineAngleMeasurement`(2),
`LineToLineDistanceMeasurement`(2), `PointToLineDistanceMeasurement`(2),
`PointToPointDistanceMeasurement`(2).

**2) 레시피의 C13_P1 좌표가 이미 바뀌었다 (사용자가 ROI 를 옮긴 듯)**
`D:/Data/Recipe/FAI_1/main.ini` :8436 `[SHOT_23_FAI_0_MEAS_0]` (해당 `[SHOT_23]` :8345
`ShotName=SIDE_SHOT_2_C13-14_P1`) 현재 실제 값:
```
Point_Row=1961.00686639642      (오케스트레이터 기록: 1882)
Point_Col=989.976408136146      (오케스트레이터 기록: 967)
Point_Length1=58.99999882882    (= 59, 동일)
Point_Length2=100.000000960683  (= 100, 동일)
EdgeDirection=TtoB  EdgePolarity=DarkToLight  EdgeSelection=First
EdgeSampleCount=20  EdgeTrimCount=10  Sigma=1  EdgeThreshold=15
```
→ **절대 좌표를 하드코딩해서 판정하지 말 것.** 아래 `<expected_bounds>` 의 불변식으로
판정한다.
</orchestrator_data_corrections>

<expected_bounds>
실행자 자가 산술 검증용.

**오케스트레이터가 지정한 기준 케이스** (Row=1882, Col=967, L1=59, L2=100):
- 수정 후(정답): `top=1823  left=867  bottom=1941  right=1067`
- 수정 전(버그): `top=1782  left=908  bottom=1982  right=1026`
  → 위로 41px 더 스캔 + 가로 82px 좁음. `TtoB`+`First` 라서 위쪽 엉뚱한 에지를 먼저
  잡는다 = 사용자가 본 빨간 마크 위치.

**현재 디스크 레시피 값으로 다시 계산** (Row=1961.0, Col=990.0, L1=59, L2=100):
- 수정 후(정답): `top≈1902.0  left≈890.0  bottom≈2020.0  right≈1090.0`

**두 경우 모두 성립하는 불변식 — UAT 판정은 이걸로 한다:**
```
bottom - top   == 2 * Point_Length1  == 118   (세로 118px)
right  - left  == 2 * Point_Length2  == 200   (가로 200px)
```
버그 상태에서는 정확히 반대 (`세로 200 / 가로 118`) 로 찍힌다. 이 한 줄이 수정
성공/실패를 가른다.

**중요 — 절대 좌표는 datum 보정만큼 어긋난다.**
`EdgeToLineDistanceMeasurement` 는 `datumTransform` 을 반드시 넘긴다
(:111~:115 에 `datumTransform == null` 이면 `"Datum not found"` 로 실패시키는 가드
존재). `TryFitLine` :46~:60 에서 `AffineTransPoint2d` 로 중심이 `rRow`/`rCol` 로
이동하고, 로그의 `top/left/bottom/right` 는 **보정 후 값**이다. 해당 레시피의
`DatumAngleRad=0.000540...` 는 미소 회전이지만 원점 이동(`DatumOriginRow=2748.2`,
`DatumOriginCol=13125.7`)이 있어 절대 좌표는 티칭 박스와 정확히 일치하지 않을 수
있다. **폭·높이(118 x 200)가 진짜 판정 기준이고, 절대 좌표는 "티칭 박스 근처"면
정상**이다.
</expected_bounds>

<tasks>

<task id="1" type="auto">
  <name>Task 1: TryFitLine 축 매핑 수정 (halfH = roiLength1, halfW = roiLength2)</name>
  <files>WPF_Example/Halcon/Algorithms/VisionAlgorithmService.cs</files>
  <action>
`TryFitLine` 내부에서 `double halfW = roiLength1; double halfH = roiLength2;` 두 줄을
찾아 `halfH` 가 `roiLength1` 을, `halfW` 가 `roiLength2` 를 받도록 바로잡는다.

허용되는 형태는 둘 중 하나 (실행자 판단):
- 선언 순서를 유지하고 우변만 교환 (`halfW = roiLength2; halfH = roiLength1;`)
- 또는 선언 자체를 `halfH` 먼저로 재배치

**절대 건드리지 말 것 (이 한 곳만 고치면 나머지는 자동으로 맞는다):**
- `top/bottom/left/right/widthPx/heightPx` 계산식 6줄 — 그대로
- `if (scanHorizontal)` 수평/수직 분기, `AppendStrip` 호출 인자, `SortAndTrimPercent`
  호출 — 그대로
- `roiPhi` / `rPhi` / `measurePhi` 취급 — 그대로. 현재 구조는 strip region 을 축 정렬로
  두고 회전을 `measurePhi` 로 흡수하며, 화면 표시(`BuildPointRoiDefinitions`)도 Phi 를
  무시한다. 즉 회전 취급은 표시·탐색이 이미 일관된다. **이번 범위 밖.**
- `datumTransform` 블록 — 그대로

**주석을 반드시 남긴다** (기존 :105~:107 주석 블록 바로 아래, 수정한 두 줄 위):
"왜" 3가지를 간결히 (날짜/이니셜 접두 주석 금지, `<hard_rules>` 참조):
1. 측정 Point ROI 규약은 `Length1 = 행(세로) 반폭`, `Length2 = 열(가로) 반폭` 이다.
2. 티칭 3경로(`MainView` 드래그 완료 / `ApplyPointRoiResize` / `BuildPointRoiDefinitions`)가
   모두 이 매핑으로 쓰고 읽으므로 탐색도 동일해야 한다. 반대로 두면 중심은 같고
   가로·세로만 뒤바뀐 사각형을 탐색하게 된다.
3. 이 규약은 `FAIEdgeMeasurementService` 의 FAI ROI 규약
   (`ROI_Length1 = phi 방향 반장축`, HALCON `gen_measure_rectangle2` 규약,
   CONTEXT.md D-02 LOCKED)과 **축이 반대**다. 두 규약을 혼동하지 말 것.

파일 스타일은 **Allman** brace. 기존 라인 정렬/들여쓰기 유지.
  </action>
  <verify>
```bash
cd /c/code/DataMeasurement
# 1) 매핑이 바뀌었는지
grep -n "halfW\|halfH" WPF_Example/Halcon/Algorithms/VisionAlgorithmService.cs
#    기대: halfH 가 roiLength1 을, halfW 가 roiLength2 를 받는다
#    그리고 halfW/halfH 등장은 여전히 6줄뿐 (top/bottom/left/right 계산식 unchanged)

# 2) strip 루프가 손대지지 않았는지 — diff 는 매핑 2줄 + 주석만
git diff --stat WPF_Example/Halcon/Algorithms/VisionAlgorithmService.cs
git diff -U0 WPF_Example/Halcon/Algorithms/VisionAlgorithmService.cs

# 3) 하드룰 — 이번에 추가한 라인만 검사 (<hard_rules> 의 스크립트 사용)
```
  </verify>
  <done>
- `halfH` 가 `roiLength1`, `halfW` 가 `roiLength2` 를 받는다.
- `top = rRow - halfH`, `left = rCol - halfW` 형태는 그대로 유지되어, 결과적으로
  `bottom - top == 2*roiLength1`, `right - left == 2*roiLength2` 가 성립한다.
- "왜" 주석 3항목이 들어갔고 FAI 규약과의 축 반전 차이가 명시되어 있다.
- `git diff` 상 추가/변경 라인이 매핑 2줄 + 주석 몇 줄 뿐이다.
- 커밋 1개 (`git add` 는 이 파일 하나만).
  </done>
  <commit>fix(quick-260910-ly4): 측정 Point ROI 탐색영역 축 반전 수정 — Length1=행 반폭, Length2=열 반폭</commit>
</task>

<task id="2" type="auto">
  <name>Task 2: strip 루프 직전 [FitLine] bounds 관측 로그 추가</name>
  <files>WPF_Example/Halcon/Algorithms/VisionAlgorithmService.cs</files>
  <action>
`int failedStrips = 0;` 다음, `if (scanHorizontal)` **직전**에 bounds 로그를 추가한다.
현재 `TryFitLine` 에는 bounds 로그가 전혀 없어(기존 `[FitLine]` 로그 3개는 strip 통계·
커버리지 경고·fit residual 뿐) 실기 검증이 불가능했다. UAT 1번이 이 로그에 의존한다.

포맷은 `DatumFindingService.cs` :1972 의 `strip-loop(extract)` 로그와 **필드 순서·구분자
공백까지 동일**하게 맞추되, 태그만 기존 `[FitLine]` 계열로 한다:

```
[FitLine] strip-loop: bounds top={0:F1} left={1:F1} bottom={2:F1} right={3:F1}  scan={4}  stripCount={5}  sigma={6:F2} threshold={7} polarity={8}
```
(`bounds ... right=` 와 `scan=` 사이, `scan=...` 과 `stripCount=` 사이는 Datum 로그와
같이 **공백 2개**)

- 출력 채널: `Logging.PrintLog((int)ELogType.Algorithm, ...)` — 기존 :183 호출과 동일.
  using 추가 불필요.
- 인자: `top`, `left`, `bottom`, `right` (이미 계산되어 있는 로컬), `stripCount`,
  `sigma`, `threshold`, `pol`.
  `pol` 은 이미 `"positive"`/`"negative"` 로 매핑된 기존 로컬이다 (:80~:88). 새로 만들지 말 것.
- `scan` 값은 `scanHorizontal` 로 정한다. **조건 연산자 금지** — 명시적 `if/else` 로
  문자열 로컬을 먼저 정한다. 로컬명은 `szScanLabel` (헝가리언 `sz`). 기본값
  `"vertical"` 을 넣고 `scanHorizontal` 이 참일 때 `"horizontal"` 로 바꾸는 형태
  (Datum 쪽과 동일 패턴). 한 줄 분기라도 **중괄호 필수**.
- 매직넘버 없음, 새 HALCON 객체 생성 없음 → Dispose 대상 없음.

**기존 로컬 이름은 바꾸지 말 것** (`halfW`, `top`, `left`, `pol` 등). 이번에 새로
추가하는 식별자만 헝가리언 규칙을 적용한다. 로그 추가 외의 로직 변경 0.
  </action>
  <verify>
```bash
cd /c/code/DataMeasurement
# 1) 로그가 strip 루프 직전에 들어갔는지 (라인 번호 순서 확인)
grep -n "strip-loop\|if (scanHorizontal)\|int failedStrips" WPF_Example/Halcon/Algorithms/VisionAlgorithmService.cs
#    기대 순서: int failedStrips  <  strip-loop 로그  <  if (scanHorizontal)

# 2) 중괄호 있는 명시 if/else 로 szScanLabel 이 정해졌는지
grep -n -A3 "szScanLabel" WPF_Example/Halcon/Algorithms/VisionAlgorithmService.cs

# 3) 하드룰 — 추가 라인만 검사 (<hard_rules> 스크립트)
```
  </verify>
  <done>
- `int failedStrips = 0;` 와 `if (scanHorizontal)` 사이에 `[FitLine] strip-loop: bounds ...`
  로그가 있다.
- `top/left/bottom/right/stripCount/sigma/threshold/pol` 이 그대로 전달된다.
- `szScanLabel` 이 중괄호 있는 `if` 로 결정된다 (조건 연산자 미사용).
- 로그 추가 외 동작 변경 없음 (`git diff` 로 확인).
- 커밋 1개 (`git add` 는 이 파일 하나만).
  </done>
  <commit>feat(quick-260910-ly4): TryFitLine strip 루프 탐색영역 bounds 로그 추가</commit>
</task>

<task id="3" type="auto">
  <name>Task 3: Debug/x64 빌드 에러 0 확인 + 하드룰 최종 검증 (커밋 없음)</name>
  <files>(없음 — 검증 전용)</files>
  <action>
빌드와 하드룰 grep 을 돌려 회귀가 없음을 확정한다. **이 태스크는 파일을 만들지도
커밋하지도 않는다.** 실패하면 Task 1/2 의 해당 파일을 고치고 그 커밋을 `--amend`
하거나 추가 fix 커밋을 낸다 (단, 수정 대상 파일은 `VisionAlgorithmService.cs` 하나로
유지).
  </action>
  <verify>
```bash
cd /c/code/DataMeasurement

# 1) Debug|x64 빌드 — 에러 0
"C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" \
  WPF_Example/DatumMeasurement.csproj \
  //p:Configuration=Debug //p:Platform=x64 //v:minimal //nologo

# 2) 하드룰 — 이번에 추가한 라인만 검사 (<hard_rules> 스크립트). 전부 0 이어야 함

# 3) 금지 파일 미접촉 확인 — 아래 3개는 출력이 없어야 한다
git status --porcelain WPF_Example/DatumMeasurement.csproj
git status --porcelain WPF_Example/Halcon/Algorithms/FAIEdgeMeasurementService.cs
git status --porcelain WPF_Example/UI/ContentItem/MainView.xaml.cs

# 4) 이번 작업이 만진 파일이 정확히 1개인지
git diff --stat HEAD~2 --name-only
#    기대: WPF_Example/Halcon/Algorithms/VisionAlgorithmService.cs 만
```
  </verify>
  <done>
- MSBuild `Debug|x64` 에러 0 (경고는 기존 수준 유지).
- 하드룰 grep 5종 모두 추가 라인 기준 0.
- `DatumMeasurement.csproj`, `FAIEdgeMeasurementService.cs`, `MainView.xaml.cs` 모두
  변경 없음.
- 이번 작업의 변경 파일이 `VisionAlgorithmService.cs` 단 하나.
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
8. 매직넘버 → 이름 있는 `const`
9. 신규 식별자 헝가리언 접두: `b`(bool) `n`(int) `sz`(string) `d`(double) `hv`(HTuple)
   ※ **기존 로컬(`halfW`, `top`, `pol` 등) 이름은 바꾸지 말 것.** 신규만 적용.
10. `HImage`/`HObject`/`HTuple` Dispose — 이번 수정은 새 HALCON 객체를 만들지 않으므로 해당 없음

기존 파일에 이미 위반이 있으므로 (예: 파일 :247 에 삼항, :182 에 날짜 주석) 파일 전체
grep 은 의미가 없다. **`git diff` 의 추가 라인(`+`)만** 검사한다:

```bash
cd /c/code/DataMeasurement
F=WPF_Example/Halcon/Algorithms/VisionAlgorithmService.cs
# 커밋 전이면 아래 그대로, 커밋 후면 REF 를 HEAD~1 등으로 바꿔 사용
git diff -U0 -- "$F" | grep '^+' | grep -v '^+++' > /tmp/ly4_added.txt

grep -cE '\?[^?]*:'  /tmp/ly4_added.txt   # 삼항       -> 0
grep -cF '??'        /tmp/ly4_added.txt   # null 병합  -> 0
grep -cF '?.'        /tmp/ly4_added.txt   # null 조건  -> 0
grep -cE 'switch.*=>' /tmp/ly4_added.txt  # switch 식  -> 0
grep -cF 'hbk'       /tmp/ly4_added.txt   # 날짜 주석  -> 0
```
(`grep -c` 는 매치 0일 때 exit 1 이라 스크립트가 죽을 수 있으니 개별 실행하거나
`|| true` 를 붙일 것.)
</hard_rules>

<forbidden>
아래는 **절대 수정 금지**. 위반 시 회귀로 간주한다.

1. **`WPF_Example/Halcon/Algorithms/FAIEdgeMeasurementService.cs`**
   FAI ROI 의 `ROI_Length1`/`ROI_Length2` 매핑은 CONTEXT.md **D-02 LOCKED** 이며 이번
   버그와 무관하다 (별개 규약, 별개 티칭 경로). :131 에 "swap 금지" 주석이 이미 있다.
   **읽기만 하고 수정하지 말 것.**

2. **`WPF_Example/UI/ContentItem/MainView.xaml.cs`**
   티칭/편집/표시 3경로 전부 옳다 (27:0 검증 완료). 고칠 이유가 없다. 또한 CLAUDE.md 가
   이 비대한 code-behind(4,400줄+) 에 신규 로직 추가를 금지한다.

3. **`WPF_Example/DatumMeasurement.csproj`**
   이 PC 의 실HW 세팅(`Debug|x64` 에서 `SIMUL_MODE` 제거, 현재 `TRACE;DEBUG`)이 들어
   있다. **스테이징/커밋 절대 금지.**
   → 이 때문에 **신규 `.cs` 파일 생성 불가** (classic MSBuild 는 `<Compile Include>`
   항목 추가가 필요하고, 그러면 csproj 이 더러워진다). 기존 파일 안에서 해결할 것.
   ※ 계획 작성 시점 기준 워킹트리는 clean 이고 csproj 에 미커밋 변경은 없다. 그래도
   가드는 유지한다.

4. **`D:\Data\Recipe\FAI_1\main.ini`** — 운영 레시피. **읽기만, 쓰기 금지.**

5. **`.planning/STATE.md`** — 다른 PC 와 공유되어 병합 충돌이 잦다. **행 추가만** 하고
   기존 행은 건드리지 말 것.
</forbidden>

<git_constraints>
- 이 환경에서는 `git stash` / `git checkout -- <path>` / `git restore` 류의 **작업물
  되돌리기 명령이 차단**되어 있다. 실수로 덮어쓰면 복구가 어려우니 편집 전에 대상
  위치를 grep 으로 정확히 확정하고 최소 범위로 편집할 것.
- **`git add -A` / `git add .` 절대 금지.** 커밋마다 대상 경로를 명시:
  `git add WPF_Example/Halcon/Algorithms/VisionAlgorithmService.cs`
- 커밋 전 `git status --porcelain` 으로 `DatumMeasurement.csproj` 가 스테이지에 없는지
  확인한다.
- 브랜치: `main` (현재 clean). 커밋 총 2개 (Task 1, Task 2). Task 3 은 검증 전용.
</git_constraints>

<impact_scope>
**SUMMARY.md 에 반드시 그대로 옮겨 적을 것.**

`VisionAlgorithmService.TryFitLine` 호출부는 **17곳, 측정 타입 9종**이며 **전부 이 수정의
영향을 받는다**:
`EdgeToLineDistanceMeasurement`(1), `EdgeToLineAngleMeasurement`(1),
`ArcEdgeDistanceMeasurement`(1), `ArcLineIntersectDistanceMeasurement`(4),
`DualImageEdgeDistanceMeasurement`(2), `LineToLineAngleMeasurement`(2),
`LineToLineDistanceMeasurement`(2), `PointToLineDistanceMeasurement`(2),
`PointToPointDistanceMeasurement`(2).
(`RoiLineIntersectionAlgorithm.cs` 의 동명 private static `TryFitLine` 2곳은 **무관** —
집계에서 제외.)

→ **`Length1 != Length2` 인 기존 티칭 항목은 측정값이 달라질 수 있다.** 수정 후 값이
"사용자가 실제로 그린 박스대로 탐색한 값"이므로 이것이 정상이지만, **전 항목 재검증이
필요**하다.
→ **TOP / BOTTOM PC 에도 동일하게 해당**된다.
</impact_scope>

<uat>
실기 UAT — **사용자가 직접 수행.** SUMMARY.md 에 체크 항목으로 남길 것.

- [ ] **1. `[FitLine]` bounds 로그 확인**
      `SIDE_SHOT_2_C13-14_P1` 오프라인 검사 재실행 → `D:\Data\Algorithm\` 의 당일
      `*_Algorithm.log` 에 `[FitLine] strip-loop: bounds ...` 가 찍히는지 확인.
      **판정 기준 (절대 좌표 아님):**
      `bottom - top ≈ 118` (= 2 x `Point_Length1` 59),
      `right - left ≈ 200` (= 2 x `Point_Length2` 100).
      버그 상태였다면 정확히 반대(세로 200 / 가로 118)로 찍힌다.
      절대 좌표는 datum 런타임 보정만큼 티칭 박스에서 이동해 있을 수 있다 —
      "티칭 박스 근처 + 폭·높이 일치"면 정상. (오케스트레이터 기록 좌표
      Row=1882/Col=967 기준이라면 `top=1823 left=867 bottom=1941 right=1067`,
      현재 디스크 레시피 Row=1961/Col=990 기준이라면 `top≈1902 left≈890
      bottom≈2020 right≈1090`.)

- [ ] **2. 빨간 에지 마크 위치**
      같은 검사에서 `C13_P1` (EdgeToLineDistance, DatumRef=Side_Datum_2) 의 빨간 에지
      마크가 파란 박스 **안**에 들어오는지 육안 확인.

- [ ] **3. ROI 이동 내성**
      해당 ROI 를 아래로 조금 이동해도 측정이 유지되는지 확인 (이전에는 실패했음).

- [ ] **4. 회귀 확인**
      기존에 정상이던 다른 측정 항목들이 회귀하지 않는지 확인.
      특히 `Length1 != Length2` 로 티칭된 항목은 값이 바뀔 수 있으므로 공칭/공차 기준
      재확인 필요 (`<impact_scope>` 참조).
</uat>

<success_criteria>
- `TryFitLine` 의 탐색 사각형이 `높이 = 2*roiLength1`, `너비 = 2*roiLength2` 로 산출된다.
- 축 반전 규약 차이(측정 Point ROI vs FAI ROI)가 주석으로 남아 재발을 막는다.
- strip 루프 직전 `[FitLine] strip-loop: bounds ...` 로그가 `ELogType.Algorithm` 으로 남는다.
- MSBuild `Debug|x64` 에러 0.
- 변경 파일 1개(`VisionAlgorithmService.cs`), 커밋 2개, 금지 파일 미접촉.
- 하드룰 grep 5종 모두 추가 라인 기준 0.
- SUMMARY.md 에 영향 범위 17곳/9종 + TOP/BOTTOM PC 동일 적용 + UAT 4항목 기록.
</success_criteria>

<output>
`.planning/quick/260910-ly4-measurement-point-roi-search-region-axis/SUMMARY.md`
</output>
