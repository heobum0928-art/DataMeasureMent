---
task: 260910-mr6-log-actual-halcon-region-extents-via-sma
type: quick
mode: observability
autonomous: false
follows: 260910-md3-compound-measurement-rect-roi-genrectang
files_modified:
  - WPF_Example/Halcon/Algorithms/VisionAlgorithmService.cs
tasks: 3
commits: 2
must_haves:
  truths:
    - "[ContourRect]/[ShortAxis] 로그의 height/width 가 HALCON 이 실제로 만든 region 에서 나온다 (코드 가정에서 파생되지 않는다)."
    - "region 조회 코드가 자체 try/catch 로 격리되어 있어, 거기서 예외가 나도 바깥 catch 로 새지 않는다."
    - "region 조회가 실패해도 로그 한 줄은 반드시 찍힌다 (센티넬 -1.0)."
    - "측정 로직(dGenLen1/dGenLen2 계산, GenRectangle2 호출, ReduceDomain 이후 파이프라인)은 한 줄도 바뀌지 않는다."
    - "순환 로그였던 dHeightPx/dWidthPx 는 제거되어 파일에 남아 있지 않다."
    - "Debug|x64 빌드 에러 0."
  artifacts:
    - WPF_Example/Halcon/Algorithms/VisionAlgorithmService.cs
  key_links:
    - "gen_rectangle2 가 만든 rect region <-> smallest_rectangle1/2 로 되읽은 실측 폭·높이"
    - "티칭 원본(roiLength1/roiLength2) <-> 실측값 — 같은 로그 한 줄 안의 불변식"
---

<objective>
`260910-md3` 가 `VisionAlgorithmService` 두 지점에 넣은 `[ContourRect]` / `[ShortAxis]`
관측 로그는 **자기 가정을 반증할 수 없는 순환 로그**다. `height`/`width` 를
`dGenLen2`/`dGenLen1` 에서 계산하는데, 그 값들은 "HALCON `gen_rectangle2` 의 4번째 인자 =
Phi 방향 반장축" 이라는 **이 코드의 가정**에서 나온 것이기 때문이다. 가정이 틀렸어도
로그는 기대한 값을 그대로 찍는다.

이번 작업은 사용자 제안대로 **`gen_rectangle2` 가 실제로 만들어낸 region 을 HALCON 에게
되물어** (`smallest_rectangle1` + `smallest_rectangle2`) 실측 폭·높이를 찍게 바꾼다.
그러면 로그가 이 코드의 가정과 **독립적인 근거**가 된다.

사용자 지시: **"에이전트 논의 후 넣어 이거 잘못 넣으면 프로그램 실측값 엉망돼"**
→ 이 작업의 1순위 요구는 "관측성 향상"이 아니라 **"로그가 측정 결과에 절대 영향을 주지
않는다"** 이다. 설계·검증 전부 이 요구를 중심으로 잡았다 (`<safety_design>` 참조).

Purpose: 순환 로그 제거. 로그를 코드 가정과 독립적인 실측 근거로 승격.
Output: `VisionAlgorithmService.cs` 한 파일, 로그 블록 2곳 교체. **측정 로직 변경 0.**
</objective>

<verified_facts>
계획 작성 시점(2026-09-10, `main` @ `a74347ae`)에 플래너가 **직접 재확인**한 사실.
`260910-ly4` 3커밋 + `260910-md3` 3커밋이 전부 반영된 워킹트리 기준이며,
아래 라인 번호는 **현재 정확**하다 (md3 이후 `+31`줄 시프트: 파일 1102 → 1133줄).
그래도 실행 전에 라인 번호가 아니라 **코드 내용(grep)** 으로 위치를 확정할 것.

## 1) 현재 코드 — 순환 로그가 있는 두 지점

`WPF_Example/Halcon/Algorithms/VisionAlgorithmService.cs` (총 **1133**줄, **Allman**, 들여쓰기 **공백 16칸**)

```
grep -n "GenRectangle2\|GenMeasureRectangle2" WPF_Example/Halcon/Algorithms/VisionAlgorithmService.cs
  310  GenMeasureRectangle2   <- AppendStrip. 무관
  525  GenMeasureRectangle2   <- TryFindCircleByPolarSampling. 무관
  846  GenRectangle2          <- TryFindLargestContourRect      (수정 대상 로그는 :848~854)
  987  GenRectangle2          <- TryFindShortAxisIntersections  (수정 대상 로그는 :989~995)
```

| 지점 | 메서드 | `GenRectangle2` | 교체할 로그 블록 | 직후 |
|---|---|---|---|---|
| A | `TryFindLargestContourRect` | `:846` | `:848~854` (앞 `:847` 은 빈 줄) | `:856 ReduceDomain` |
| B | `TryFindShortAxisIntersections` | `:987` | `:989~995` (앞 `:988` 은 빈 줄) | `:997 ReduceDomain` |

**교체 대상 블록 — 두 곳이 태그(`[ContourRect]`/`[ShortAxis]`)만 다르고 나머지는 동일:**

```
                // 탐색 영역 관측 로그 — 실기에서 "그린 박스대로 탐색하는가"를 판정한다.
                // 불변식: height == 2*halfRow, width == 2*halfCol (버그 상태면 정확히 반대로 찍힌다).
                double dHeightPx = dGenLen2 + dGenLen2; // 행(세로) 전체 길이
                double dWidthPx = dGenLen1 + dGenLen1; // 열(가로) 전체 길이
                Logging.PrintLog((int)ELogType.Algorithm,
                    string.Format("[ContourRect] roi: row={0:F1} col={1:F1} phi={2:F4}  halfRow={3:F1} halfCol={4:F1}  gen_rectangle2: height={5:F1} width={6:F1}",
                        cRow, cCol, cPhi, roiLength1, roiLength2, dHeightPx, dWidthPx));
```

**보존 대상 — 바로 위 3줄은 손대지 않는다 (`:844~846` / `:985~987`):**
```
                double dGenLen1 = roiLength2; // HALCON Length1 = Phi 방향 반장축 -> 열(가로) 반폭
                double dGenLen2 = roiLength1; // HALCON Length2 = Phi 수직 방향 반장축 -> 행(세로) 반폭
                HOperatorSet.GenRectangle2(out rect, cRow, cCol, cPhi, dGenLen1, dGenLen2);
```
그 위의 md3 "왜" 주석 블록(`:836~843`, B 지점은 `:981~984`)도 **그대로 둔다.**

## 2) 안전 요구 검증 — `smallest_rectangle1/2` 는 입력 region 을 읽기만 한다

HALCON 24.11 공식 레퍼런스(로컬 설치본
`C:/Program Files/MVTec/HALCON-24.11-Progress-Steady/doc/html/reference/operators/`)에서
플래너가 직접 확인:

- `smallest_rectangle1 ( Regions : : : Row1, Column1, Row2, Column2 )`
  - `Regions` 는 **`input_object`** 로 명시. C 시그니처는 **`const Hobject Regions`**.
  - .NET: `static void HOperatorSet.SmallestRectangle1(HObject regions, out HTuple row1, out HTuple column1, out HTuple row2, out HTuple column2)`
    → 첫 인자만 `HObject`(값), 나머지는 전부 `out`. **region 을 변형·소비하지 않는다.**
- `smallest_rectangle2 ( Regions : : : Row, Column, Phi, Length1, Length2 )` — 동일하게 `input_object` / `const Hobject`.

`halcondotnet.xml` 멤버 확인 (`bin/dotnet35/halcondotnet.xml:44118`, `:44127`):
두 메서드 모두 `M:HalconDotNet.HOperatorSet.SmallestRectangle{1,2}(HalconDotNet.HObject, HalconDotNet.HTuple@ ...)`
— 첫 파라미터에 `@`(by-ref) 가 **없다**. 24.11 halcondotnet 에 존재하며 시그니처가 맞다.

→ 로그 직후의 `HOperatorSet.ReduceDomain(image, rect, out imageReduced)` 는 **아무 영향을 받지 않는다.**
   (프로젝트 참조는 non-XL `halcondotnet.dll` — `DatumMeasurement.csproj:207~208` 확인.)

## 3) HALCON 반환 의미 — 이번 설계의 핵심 근거

**`smallest_rectangle1` (축정렬 bounding box)**
- 문서: *"The surrounding rectangle is described by the coordinates of the corner pixels (Row1, Column1, Row2, Column2). **The calculation of the rectangle is based on the center coordinates of the region pixels.**"*
- 반환 타입: **정수** (`Hlong*` / Python `Sequence[int]` / `HRegion.SmallestRectangle1(out int row1, ...)`)
- 빈 region: **예외가 아니라 전부 0** *(단, `set_system` 으로 다르게 설정되지 않은 경우)*.
  이 코드베이스는 `HOperatorSet.SetSystem` 을 `SystemHandler.cs:208~215` 에서 메모리 관련
  4개만 호출하며 `empty_region_result` 는 설정하지 않는다 → 기본 동작(0 반환).
  그래도 격리 try/catch 는 유지한다 (아래 `<safety_design>`).

**`smallest_rectangle2` (임의 방향 최소 외접 사각형)**
- 문서: *"determines the smallest surrounding rectangle of a region, i.e., the rectangle with the smallest area of all rectangles containing the region... **The parameters are chosen in such a way that they can be used directly as input for disp_rectangle2 and gen_rectangle2.**"*
  → `gen_rectangle2` 의 **정확한 역연산**이다. "우리가 넘긴 4번째 인자가 실제로 무슨 축이었나"에
  HALCON 자기 어휘로 답한다.
- 반환 타입: **실수**(`double`) → **`.D`**.
- **⚠ 결정적 제약 — 문서 Assertion:**
  - `Length2 >= 0.0 && **Length2 <= Length1**` → HALCON 은 **항상 `Length1` 을 긴 쪽(major)** 으로 정규화해 돌려준다.
  - `-pi/2 < Phi && Phi <= pi/2`
  → **`Length1`/`Length2` 값만으로는 가로/세로를 구분할 수 없다.** 구분자는 **`Phi`** 다.
    (`Phi≈0` → 긴 축이 열/가로, `Phi≈±1.5708` → 긴 축이 행/세로)
  → 이 제약 때문에 `smallest_rectangle2` **단독으로는 이번 목적에 부적합**하다.
    자세한 판단은 `<design_decision>` 참조.

## 3b) `HTuple` 요소 접근은 **전부 `.D`** 로 한다 (`.I` 금지) — 타입 실패 모드 제거

`halcondotnet.xml` 의 프로퍼티 설명을 플래너가 직접 확인:

| 접근자 | 문서 설명 (`halcondotnet.xml:64423`, `:64447`) | 허용 범위 |
|---|---|---|
| `.I` | *"Get the value of this element as a 32-bit integer. The element **must represent integer data** (32-bit or 64-bit)."* | 정수 전용 — 실수면 예외 |
| `.D` | *"Get the value of this element as a double. The element **must represent numeric data**."* | **정수·실수 모두 허용** |

→ `smallest_rectangle1` 은 정수를 돌려주므로 `.I` 도 맞지만, **`.D` 는 정수·실수 양쪽에서
  무조건 안전**하다. 이번 작업의 1순위 요구가 "로그가 측정을 깨뜨릴 수 없게 한다" 이므로
  **실패 모드 자체를 없애는 `.D` 를 택한다.** 두 연산자 9개 출력 전부 `.D` 로 읽어
  코드도 균일해진다.
→ 따라서 aabb delta 는 `double` 이고 로컬명은 헝가리언 `d` 접두, 포맷은 `{:F1}` 이다.

`HTuple.D` / `HTuple.I` 는 단일 요소 편의 접근자로 둘 다 존재한다
(`halcondotnet.xml:58588`, `:58594` — *"Convenience accessor for tuple[0].D"*).
이 파일도 이미 `tRow.D`(`:828`) 처럼 인덱스 없이 `.D` 를 쓴다.

## 4) 로깅 인프라 — `using` 추가 불필요, 포맷 파싱 위험 없음

```
:3  using HalconDotNet;
:4  using ReringProject.Setting;   // ELogType
:5  using ReringProject.Utility;   // Logging
```
- `ELogType.Algorithm = 8` (`Setting/SystemSetting.cs:30`), 저장 경로 `SystemSetting.AlgorithmLogSavePath`
  기본값 `D:\Data\Algorithm` (`Setting/SystemSetting.cs:102`, `:286`).
- `Logging.PrintLog` 오버로드 2개 (`Utility/Logging.cs:261`, `:275`):
  - `PrintLog(int logID, string format, params object[] args)` → 내부에서 `string.Format` 수행
  - `PrintLog(int logID, string msg)` → **`string.Format` 을 하지 않고 그대로 enqueue**
  인자 1개(문자열)로 호출하면 C# 오버로드 해석이 non-`params` 쪽(`:275`)을 고른다 →
  중괄호가 든 문자열을 넘겨도 포맷 파싱 예외가 나지 않는다. 기존 호출도 전부
  `PrintLog((int)ELogType.Algorithm, string.Format(...))` 형태로 `:275` 를 쓴다. **동일 패턴 유지.**
- `PrintLog` 는 `lock` 후 `Queue.Enqueue` + `WaitObject.Set()` 만 한다 → 검사 스레드를 블록하지 않는다.

## 5) 격리 패턴의 **파일 내 선례** — 새 발명이 아니다

바로 위 `datumTransform` 블록(`:823~834`, B 지점 `:967~978`)이 **이미 동일한 패턴**이다:
바깥 `try { ... } catch { return false; }` 안에서, 실패해도 측정을 중단시키면 안 되는
부분을 **중첩 `try { ... } catch { }`** 로 감싸고 있다.

```
                if (datumTransform != null && datumTransform.Length > 0)
                {
                    try
                    {
                        ... AffineTransPoint2d ...
                    }
                    catch { }
                }
```
→ 이번 로그 격리는 **같은 메서드 안의 기존 관행을 그대로 따르는 것**이다.
CLAUDE.md 도 "비핵심 정리 작업의 bare `catch { }` 묵인은 기존 패턴상 허용"으로 규정한다.

## 6) 운영 레시피 6개 (`D:/Data/Recipe/FAI_1/main.ini`, 읽기 전용 — md3 에서 확인, 값 그대로 승계)

`Rect_Phi` 는 6건 전부 **0**. 그리고 **6건 전부 `Rect_Length2 > Rect_Length1`** (박스가 세로보다 가로가 길다).
이 사실이 `<expected_log_output>` 판정의 근거다.
</verified_facts>

<design_decision>
**설계 후보 A / B 중 무엇을 쓸 것인가 — 플래너 판단과 근거**

## 결론: **A 를 주 판정 지표로, B 를 회전 대응 보조 지표로 — 한 줄에 둘 다 찍는다.**

## 왜 B 단독이면 안 되는가 (오케스트레이터 초안에서 바뀐 지점)

오케스트레이터는 "B 가 사용자의 질문(가로인지 세로인지)에 가장 직접 답한다"고 봤지만,
플래너가 HALCON 24.11 공식 문서를 확인한 결과 **그렇지 않다.**

`smallest_rectangle2` 는 Assertion 으로 **`Length2 <= Length1`** 을 보장한다 —
즉 항상 **긴 쪽을 `Length1`** 에 넣어 정규화해서 돌려준다. 우리 6개 항목에 대입하면:

| 상태 | region 실제 모양 | `smallest_rectangle2` 반환 |
|---|---|---|
| 수정 후(정상) | 가로 398 × 세로 378 | `Phi≈0.0000`, `Length1≈199`, `Length2≈189` |
| 만약 버그 상태 | 가로 378 × 세로 398 | `Phi≈1.5708`, `Length1≈199`, `Length2≈189` |

→ **`Length1`/`Length2` 숫자가 두 경우에 완전히 동일하다.** 차이는 오직 `Phi` 뿐이다.
그런데 로그 한 줄에는 입력 `cPhi`(=0.0000)도 나란히 찍히므로, `phi` 두 개가 붙어 있으면
사람이 오독하기 쉽다. **"잘못 읽으면 실측값 엉망"이라는 사용자 우려와 정확히 같은 종류의 위험**이다.

## 왜 A 가 주 지표인가

`smallest_rectangle1` 은 `Row1/Column1/Row2/Column2` 를 돌려준다 —
**파라미터 이름 자체에 행/열이 박혀 있어 해석의 여지가 0이다.**
`Row2-Row1` 과 `Col2-Col1` 이 그대로 세로/가로다. 버그 상태면 두 숫자가 **눈에 띄게 서로 뒤바뀐다**
(예: `height=377.0 width=397.0` ↔ `height=397.0 width=377.0`). 판정에 산수도 해석도 필요 없다.

그리고 이것이 **사용자가 원문에서 말한 바로 그것**이다 —
*"꼭지점 4점을 구해서 사이즈별로 확인하면 가로인지 세로인지 알 수 있지 않을까"*.
`smallest_rectangle1` 의 `(Row1,Col1)`/`(Row2,Col2)` 가 정확히 그 축정렬 4점이다.

A 의 알려진 한계: `cPhi != 0` 이면 회전 사각형을 감싸는 AABB 라 값이 부풀어
`height == 2*halfRow` 불변식이 깨진다. **현재 운영 레시피 6건은 전부 `Rect_Phi=0`** 이라
현장에서는 정확하지만, `TransformMeasurementGeometry`(캘리브 전체적용) 경로로 언젠가
`Rect_Phi != 0` 이 생길 수 있다 (md3 `<phi_analysis>` 에서 확인된 사실).

## 그래서 B 를 보조로 함께 남긴다

`cPhi != 0` 일 때 A 의 AABB 는 부풀지만, B 는 **회전을 따라가며 진짜 변 길이**를 준다.
즉 두 지표는 서로의 사각지대를 덮는다:

| | `cPhi == 0` (현재 전부) | `cPhi != 0` (미래) |
|---|---|---|
| **A** `region(aabb)` | 정확. **주 판정 지표** | AABB 라 부풀어 오름 (해석 주의) |
| **B** `region(rect2)` | `phi≈0` 확인용 교차검증 | **회전 불변. 여기서는 B 가 주 지표** |

혼동을 원천 차단하기 위해 **B 의 출력 라벨을 `len1`/`len2` 가 아니라 `major`/`minor` 로 찍는다.**
HALCON 이 실제로 보장하는 것(`Length1 >= Length2`)을 이름으로 못 박아,
"len1 이 행이냐 열이냐"라는 잘못된 질문 자체가 성립하지 않게 한다.

## 기존 `dHeightPx`/`dWidthPx` — **대체(제거)** 한다. 병기하지 않는다.

1. **순환이다.** `dGenLen1`/`dGenLen2` 에서 파생되므로, 검증하려는 가정을 그대로 재출력한다.
2. **실측값이 이를 완전히 포섭한다.** 누군가 `dGenLen1 = roiLength2` swap 을 되돌리면
   HALCON 이 만드는 region 자체가 뒤집히므로 **실측값도 그대로 뒤집힌다.**
   md3 `<design_note>` 이 확보하려던 "swap 회귀 감지" 능력이 손실 없이 유지된다.
3. **한 줄에 `height=`/`width=` 가 두 쌍 있으면 오독을 부른다.** 이번 작업의 1순위 요구에 반한다.

→ `dHeightPx` / `dWidthPx` 두 로컬은 **삭제**한다. 티칭 원본(`roiLength1`/`roiLength2`)은
`teach:` 그룹으로 **계속 찍는다** — 그래야 로그 한 줄이 자기완결적 불변식을 갖는다.
</design_decision>

<safety_design>
**"로그가 측정에 영향을 주지 않는다"를 보장하는 5중 장치. 실행자는 5개 전부 지킬 것.**

### 1. 중첩 `try { ... } catch { }` 로 격리 (필수)
삽입 지점은 바깥 `try { ... } catch (Exception ex) { error = ex.Message; return false; }` **내부**다.
로그 코드에서 예외가 나면 바깥 catch 가 삼켜 **측정이 조용히 실패**한다 — 사용자가 우려한
"실측값 엉망"이 정확히 이 시나리오다.
→ HALCON 조회 2건을 **자체 `try { ... } catch { }`** 로 감싼다. 파일 내 선례는
`<verified_facts>` 5 (`datumTransform` 블록).

### 2. `Logging.PrintLog` 호출은 격리 블록 **바깥**에 둔다
조회 결과를 **사전 선언된 센티넬 로컬**에 담고, `PrintLog` 는 격리 try 를 빠져나온 뒤 호출한다.
→ 조회가 실패해도 **로그 한 줄은 반드시 찍힌다** (`height=-1 width=-1` 형태).
   "로그가 아예 안 나온다"는 진단 불가 상태를 만들지 않는다.
→ `PrintLog` 자신은 `lock` + `Enqueue` 뿐이라 사실상 던지지 않지만,
   던지더라도 이미 조회는 끝났고 측정 상태는 변경되지 않은 시점이다.

### 3. `rect` region 은 읽기 전용으로만 다룬다
`smallest_rectangle1/2` 는 `input_object`/`const Hobject` — 변형·소비하지 않는다
(`<verified_facts>` 2 에서 문서·시그니처로 확인). 직후의 `ReduceDomain(image, rect, ...)` 무영향.
**`rect` 를 재할당하거나 Dispose 하지 말 것** — `finally` 블록이 이미 담당한다.

### 4. 측정 로직은 단 한 줄도 바꾸지 않는다
`dGenLen1`/`dGenLen2` 계산, `GenRectangle2` 호출, md3 "왜" 주석 블록,
`datumTransform` 블록, `ReduceDomain` 이후 파이프라인 전부, `finally` Dispose 블록,
메서드 시그니처, 호출부 4곳 — **전부 불변**. 이번 변경은 **관측 전용**이다.

### 5. `HTuple` 요소 접근은 전부 `.D` — 타입 실패 모드를 아예 없앤다
`.I` 는 "정수 데이터여야 한다"는 제약이 있고 `.D` 는 "숫자 데이터면 된다" 이다
(`<verified_facts>` 3b). 두 연산자 출력 9개 전부 `.D` 로 읽어 `HTupleAccessException`
경로를 제거한다. **`.I` 를 쓰지 말 것.**

### 그래도 센티넬이 찍히는 경우의 의미
위 5중 장치에도 불구하고 `height=-1.0 width=-1.0` 이 보인다면, 격리 catch 가 무언가를 받은
것이다 — **측정값에는 영향이 없다.** 원인(빈 region, HALCON 내부 오류 등)을 별건으로 보고한다.
(UAT 항목 5에 이 분기를 넣어 두었다.)
</safety_design>

<htuple_dispose_decision>
**요구받은 결정 사항 — 신규 out-`HTuple` 9개를 Dispose 할 것인가?**

## 결론: **Dispose 하지 않는다 (파일·코드베이스 관행을 따른다).**

## 근거

1. **관행이 예외 없이 일관된다 (전수 확인).** 이 코드베이스에서 HALCON 연산자의 out-`HTuple`
   을 Dispose 하는 곳은 **단 한 군데도 없다.** 같은 파일만 봐도
   `AppendStrip` 의 `rr/rc/rp/rh/rw`(`:308`), `GetImageSize` 의 `imageWidth/imageHeight`,
   `SmallestRectangle2Xld` 의 `cRowT/cColT/phiT/len1T/len2T`(`:881`, `:1019`),
   `AreaCenterXld` 의 `area/rowC/colC/ptOrder`, `GetContourXld` 의 `rows/cols` —
   전부 미해제다. 다른 파일도 동일(`DatumFindingService.cs:2197`,
   `FAIEdgeMeasurementService.cs:212/:395`, `MeasurementAlgorithm.cs:171`).
   CLAUDE.md 자신이 **"Use the style of the file/module you are editing"** 을 규정한다.
   한 지점에서만 해제를 도입하면 오히려 일관성이 깨지고, 읽는 사람에게
   "여기만 왜 다르지?"라는 잘못된 신호를 준다.

2. **`finally` 를 추가하면 1순위 안전 요구와 충돌한다.** 격리 블록에 `finally` 를 붙이면
   예외가 빠져나갈 수 있는 경로가 늘어난다(해제 중 예외 → 전파). 이번 작업의 최우선 요구는
   **"어떤 경우에도 바깥 catch 로 새지 않는다"** 이므로, 격리 블록은 `try`/`catch { }` 2개
   구성으로 **최대한 단순하게** 유지하는 편이 안전하다.

3. **해제 대상이 순수 수치 제어 튜플이다.** 여기 9개는 전부 `double`/`int` 만 담는 control
   튜플로, 아이코닉 객체나 HALCON 핸들을 들고 있지 않다 — CLAUDE.md 규칙이 겨냥하는
   `HImage`/`HObject`(네이티브 이미지·리전 메모리) 누수와 성격이 다르다.
   *(이 3번은 보조 근거다. 결정의 무게는 1·2번에 있다.)*

## 규칙 문자와의 관계 — 숨기지 않고 기록한다

CLAUDE.md 5)의 문자 그대로는 "`HTuple` 은 반드시 Dispose" 다. 이번 결정은 그 문자와
어긋나며, **의도적 예외**임을 여기와 SUMMARY.md 에 명시한다. 되돌리려면 이 파일(더 나아가
`DatumFindingService`/`FAIEdgeMeasurementService`/`MeasurementAlgorithm`)의 out-`HTuple`
전체를 한 번에 정리하는 별도 작업이어야 하며, **관측 로그를 넣는 이번 작업에서 부분 도입할
사안이 아니다.** (헝가리언 전면 리팩토링이 `Phase 26` 으로 따로 잡혀 있는 것과 같은 성격.)
</htuple_dispose_decision>

<reference_implementation>
**실행자는 이 형태를 그대로 따를 것.** (플래너가 확정한 최종 코드 형태.
`<action>` 은 이 절을 참조한다. A 지점 기준이며, B 지점은 태그만 `[ShortAxis]` 로 바꾼다.)

교체 전 (`:848~854`):
```csharp
                // 탐색 영역 관측 로그 — 실기에서 "그린 박스대로 탐색하는가"를 판정한다.
                // 불변식: height == 2*halfRow, width == 2*halfCol (버그 상태면 정확히 반대로 찍힌다).
                double dHeightPx = dGenLen2 + dGenLen2; // 행(세로) 전체 길이
                double dWidthPx = dGenLen1 + dGenLen1; // 열(가로) 전체 길이
                Logging.PrintLog((int)ELogType.Algorithm,
                    string.Format("[ContourRect] roi: row={0:F1} col={1:F1} phi={2:F4}  halfRow={3:F1} halfCol={4:F1}  gen_rectangle2: height={5:F1} width={6:F1}",
                        cRow, cCol, cPhi, roiLength1, roiLength2, dHeightPx, dWidthPx));
```

교체 후:
```csharp
                // 탐색 영역 실측 관측 로그 — HALCON 이 실제로 만든 region 을 되읽어 폭/높이를 찍는다.
                // 인자에서 역산하면 "우리 가정"을 그대로 재출력하는 순환 로그가 되므로 그렇게 하지 않는다.
                // 이 블록은 순수 관측용이다. 여기서 예외가 나면 바깥 catch 가 삼켜 측정이 조용히
                // 실패하므로, region 조회는 자체 try/catch 로 격리하고 실패 시 센티넬만 남긴다.
                // region(aabb) 는 축정렬 bounding box — cPhi=0 이면 사각형과 정확히 일치한다
                // (cPhi != 0 이면 회전 사각형을 감싸는 AABB 라 값이 커진다).
                // region(rect2) 는 HALCON 이 같은 region 을 gen_rectangle2 규약으로 되읽은 값.
                // HALCON 이 Length1 >= Length2 로 정규화하므로 major/minor 로 표기했고,
                // 가로/세로 구분자는 길이가 아니라 phi 다 (0 이면 긴 축이 열, ±pi/2 면 행).
                const double PROBE_FAIL = -1.0;
                double dAabbHeight = PROBE_FAIL;
                double dAabbWidth = PROBE_FAIL;
                double dFitPhi = PROBE_FAIL;
                double dMajorLen = PROBE_FAIL;
                double dMinorLen = PROBE_FAIL;
                try
                {
                    HTuple hvBoxRow1, hvBoxCol1, hvBoxRow2, hvBoxCol2;
                    HOperatorSet.SmallestRectangle1(rect, out hvBoxRow1, out hvBoxCol1, out hvBoxRow2, out hvBoxCol2);
                    dAabbHeight = hvBoxRow2.D - hvBoxRow1.D;
                    dAabbWidth = hvBoxCol2.D - hvBoxCol1.D;

                    HTuple hvFitRow, hvFitCol, hvFitPhi, hvFitLen1, hvFitLen2;
                    HOperatorSet.SmallestRectangle2(rect, out hvFitRow, out hvFitCol, out hvFitPhi, out hvFitLen1, out hvFitLen2);
                    dFitPhi = hvFitPhi.D;
                    dMajorLen = hvFitLen1.D;
                    dMinorLen = hvFitLen2.D;
                }
                catch { }

                Logging.PrintLog((int)ELogType.Algorithm,
                    string.Format("[ContourRect] roi: row={0:F1} col={1:F1} phi={2:F4}  teach: halfRow={3:F1} halfCol={4:F1}  region(aabb): height={5:F1} width={6:F1}  region(rect2): phi={7:F4} major={8:F1} minor={9:F1}",
                        cRow, cCol, cPhi, roiLength1, roiLength2,
                        dAabbHeight, dAabbWidth,
                        dFitPhi, dMajorLen, dMinorLen));
```

**형태 요건 체크리스트:**
- 들여쓰기 **공백 16칸**(`try`/`catch` 는 16칸, 그 안은 20칸). 파일 스타일 **Allman**.
- 신규 식별자 헝가리언: `d`(double) `hv`(HTuple), 상수는 UPPER_SNAKE_CASE. (int 로컬은 없다)
- 매직넘버 없음 — `-1.0` 은 `const double PROBE_FAIL` 로 이름을 줬다.
- **`HTuple` 요소 접근은 전부 `.D`** — `.I` 를 쓰지 말 것 (`<verified_facts>` 3b).
- 조건 연산자(`?:` / `??` / `?.`) 0개, `switch` 식 0개, 날짜 주석 0개.
- 중괄호 생략 없음.
- 기존 로컬(`cRow`, `cCol`, `cPhi`, `rect`, `roiLength1`, `roiLength2`, `dGenLen1`, `dGenLen2`)
  **이름 변경 금지**. 신규 식별자에만 헝가리언 적용.
- 신규 로컬명 9+5개는 두 메서드의 기존 로컬(`cRowT/cColT/phiT/len1T/len2T`, `area/rowC/colC/ptOrder`,
  `rows/cols`, `r0~r3`, `c0~c3`, `edge1Len/edge2Len`)과 **충돌하지 않음** — 플래너 확인 완료.
- **B 지점 차이는 단 하나: 포맷 문자열의 `[ContourRect]` → `[ShortAxis]`.** 그 외 전부 동일.
</reference_implementation>

<expected_log_output>
**실행자와 사용자가 실기에서 대조할 기대값. 플래너가 운영 레시피 6건에서 직접 산출.**

## 산출 규칙 (HALCON 문서의 "region 픽셀 중심 기준" 정의에서 유도)

`gen_rectangle2(center=r, phi=0, len1=halfCol, len2=halfRow)` 로 만든 region 에 대해:

- `smallest_rectangle1` → `Row2-Row1 = 2*halfRow` **또는** `2*halfRow - 1`
  (`Col2-Col1` 도 동일하게 `2*halfCol` 또는 `2*halfCol - 1`)
  ※ ±1 은 래스터화 때문이다 — ROI 중심이 픽셀 중심에 정확히 놓이면 `2*half`,
    반픽셀 어긋나면 `2*half - 1`. `datumTransform` 이 중심을 소수 좌표로 옮기므로
    현장에서는 두 값이 섞여 나온다. **±1 은 정상이며 실패가 아니다.**
- `smallest_rectangle2` → `major = (Col2-Col1)/2`, `minor = (Row2-Row1)/2`, `phi ≈ 0`
  (6건 전부 `halfCol > halfRow` 이므로 긴 축은 열/가로 → `phi≈0`)

## 6건 기대 로그 (현재 코드 = md3 수정 반영 상태 = **정상**)

| 섹션 | Type | 태그 | `teach: halfRow` | `teach: halfCol` | `aabb height` | `aabb width` | `rect2 phi` | `major` | `minor` |
|---|---|---|---|---|---|---|---|---|---|
| `[SHOT_7_FAI_1_MEAS_0]`  | `CompoundAngle`             | `[ContourRect]` | 189.0 | 199.0 | **377.0~378.0** | **397.0~398.0** | ≈0.0000 | 198.5~199.0 | 188.5~189.0 |
| `[SHOT_7_FAI_2_MEAS_0]`  | `CompoundShortAxisDistance` | `[ShortAxis]`   | 207.0 | 214.0 | **413.0~414.0** | **427.0~428.0** | ≈0.0000 | 213.5~214.0 | 206.5~207.0 |
| `[SHOT_7_FAI_5_MEAS_0]`  | `CompoundCenterCDistance`   | `[ContourRect]` | 177.0 | 181.5 | **353.0~354.0** | **362.0~363.0** | ≈0.0000 | 181.0~181.5 | 176.5~177.0 |
| `[SHOT_7_FAI_6_MEAS_0]`  | `CompoundCenterBDistance`   | `[ContourRect]` | 169.5 | 172.0 | **338.0~339.0** | **343.0~344.0** | ≈0.0000 | 171.5~172.0 | 169.0~169.5 |
| `[SHOT_14_FAI_0_MEAS_0]` | `CompoundCenterCDistance`   | `[ContourRect]` | 213.5 | 241.5 | **426.0~427.0** | **482.0~483.0** | ≈0.0000 | 241.0~241.5 | 213.0~213.5 |
| `[SHOT_15_FAI_0_MEAS_0]` | `CompoundCenterBDistance`   | `[ContourRect]` | 198.5 | 211.5 | **396.0~397.0** | **422.0~423.0** | ≈0.0000 | 211.0~211.5 | 198.0~198.5 |

한 줄 예시 (`[SHOT_7_FAI_1_MEAS_0]`, `row`/`col` 은 datum 보정으로 이동하므로 예시값):
```
[ContourRect] roi: row=1204.3 col=1508.7 phi=0.0000  teach: halfRow=189.0 halfCol=199.0  region(aabb): height=378.0 width=398.0  region(rect2): phi=0.0000 major=199.0 minor=189.0
```

## 만약 축 반전 회귀가 생기면 이렇게 찍힌다 (판정 기준)

같은 항목이 이렇게 나오면 **회귀**다:
```
[ContourRect] roi: ...  teach: halfRow=189.0 halfCol=199.0  region(aabb): height=398.0 width=378.0  region(rect2): phi=1.5708 major=199.0 minor=189.0
```
→ `aabb` 의 `height`/`width` 가 **서로 뒤바뀌고**, `rect2 phi` 가 **0 → ±1.5708** 로 튄다.
→ **`major`/`minor` 숫자는 두 경우에 동일하다** (HALCON 이 `Length1 >= Length2` 로 정규화하기 때문).
   **`major`/`minor` 만 보고 판정하지 말 것.**

## 예외 상황 판독표

| 로그에 보이는 것 | 의미 | 조치 |
|---|---|---|
| `height=-1.0 width=-1.0` + `phi=-1.0 major=-1.0 minor=-1.0` | region 조회가 예외를 던졌다 (격리 catch 가 받음) | **측정에는 영향 없음.** 원인 조사는 별건으로 보고 |
| `height=0.0 width=0.0` | region 이 비어 있다 (`gen_rectangle2` 에 0 이 들어감 = 미티칭 ROI) | 해당 항목 재티칭 필요. 로그가 새 문제를 잡아낸 것 |
| `aabb` 가 `teach` 값보다 **작게** 나옴 | HALCON region clipping 의심 (`set_system('clip_region')`) | 로그가 새 문제를 드러낸 것. 측정 파이프라인도 같은 region 을 쓰고 있었다는 뜻 |
| `rect2 phi` 가 0 도 ±1.5708 도 아님 | `Rect_Phi != 0` 로 티칭·변환된 항목 | 정상. 이때는 **A(aabb) 대신 B(rect2)** 로 판정할 것 |
| `major == minor` | 정사각형 ROI | `phi` 가 임의값이 되어 방향 판정 불가. 현재 6건에는 해당 없음 |
</expected_log_output>

<tasks>

<task id="1" type="auto">
  <name>Task 1: [ContourRect]/[ShortAxis] 로그를 HALCON 실측 region 기반으로 교체 (2곳)</name>
  <files>WPF_Example/Halcon/Algorithms/VisionAlgorithmService.cs</files>
  <precondition>HALCON 24.11 Progress Steady 가 `C:\Program Files\MVTec\HALCON-24.11-Progress-Steady\` 에 설치되어 있고 `bin/dotnet35/halcondotnet.dll` 이 존재한다 (csproj `:207~208` HintPath).</precondition>
  <action>
`VisionAlgorithmService.cs` 의 **로그 블록 2곳만** 교체한다. 최종 코드 형태는
`<reference_implementation>` 절에 확정되어 있으니 **그대로 따를 것.**
설계 근거는 `<design_decision>`, 안전 요구 4중 장치는 `<safety_design>` 에 있다.

**위치 확정 — 라인 번호를 믿지 말고 grep 으로 잡을 것:**

    grep -n "ContourRect\|ShortAxis\] roi\|dHeightPx\|dWidthPx\|GenRectangle2\|ReduceDomain" WPF_Example/Halcon/Algorithms/VisionAlgorithmService.cs

두 지점 모두 `HOperatorSet.GenRectangle2(out rect, cRow, cCol, cPhi, dGenLen1, dGenLen2);`
바로 아래, `HOperatorSet.ReduceDomain(image, rect, out imageReduced);` 바로 위에 있다.
- A 지점 = `TryFindLargestContourRect`, 태그 `[ContourRect]` (현재 `:848~854`)
- B 지점 = `TryFindShortAxisIntersections`, 태그 `[ShortAxis]` (현재 `:989~995`)

**교체 내용 요약 (상세 형태는 `<reference_implementation>`):**

1. 순환 로그였던 두 로컬을 **삭제**한다 — `<reference_implementation>` 의 "교체 전" 블록에서
   `dGenLen2 + dGenLen2` / `dGenLen1 + dGenLen1` 로 전체 길이를 만들던 `double` 로컬 2개.
   이 값들은 검증하려는 가정에서 파생된 값이라 가정을 반증할 수 없다.
2. **센티넬 로컬 5개(전부 `double`) + 이름 있는 실패 상수 1개(`const double`)** 를 로그 앞에
   선언한다. 매직넘버 금지 규칙 때문에 실패값에 이름을 준다.
3. **자체 `try { ... } catch { }` 블록** 안에서 HALCON 에 region 을 되묻는다.
   **두 연산자 출력 9개 모두 `HTuple` 요소를 `.D` 로 읽는다 — `.I` 를 쓰지 말 것**
   (`.I` 는 정수 데이터만 허용, `.D` 는 숫자 데이터면 허용 → 타입 실패 모드 제거.
   근거는 `<verified_facts>` 3b):
   - `HOperatorSet.SmallestRectangle1(rect, out ...4개...)` — 축정렬 bounding box.
     행 delta / 열 delta 를 뺄셈으로 구한다.
   - `HOperatorSet.SmallestRectangle2(rect, out ...5개...)` — HALCON 이 같은 region 을
     `gen_rectangle2` 규약으로 되읽은 값. `phi` 와 두 반장축을 센티넬 로컬에 담는다.
   - **`catch { }` 는 비워 둔다** — 파일 내 선례는 바로 위 `datumTransform` 블록(`:823~834`).
     여기서 예외가 바깥 catch 로 새면 측정이 조용히 실패한다.
4. **`Logging.PrintLog` 는 격리 블록을 빠져나온 뒤 호출한다.** 조회가 실패해도 로그 한 줄은
   반드시 남아야 한다(센티넬이 그대로 찍힌다).
5. 포맷 문자열은 `<reference_implementation>` 의 것을 **문자 그대로** 사용한다.
   그룹 구분은 공백 2칸, 그룹 라벨은 `teach:` / `region(aabb):` / `region(rect2):`.
   B 지점은 선두 태그만 다르다.
6. **"왜" 주석**을 로그 블록 앞에 남긴다 (`<reference_implementation>` 의 주석 8줄).
   담아야 할 내용: (a) 인자에서 역산하면 순환 로그가 된다는 점, (b) 격리 try/catch 의 이유,
   (c) `region(aabb)` 는 `cPhi=0` 에서만 사각형과 일치한다는 한계,
   (d) `region(rect2)` 는 HALCON 이 긴 쪽을 앞에 놓도록 정규화하므로 가로/세로 구분자는
   길이가 아니라 `phi` 라는 점. B 지점은 짧게 줄여도 되나 (b)(d) 는 반드시 남긴다.

**절대 건드리지 말 것 — 측정 로직 변경 0:**
- 로그 블록 **바로 위 3줄**(`dGenLen1`/`dGenLen2` 대입 + `GenRectangle2` 호출) — 그대로
- 그 위 md3 "왜" 주석 블록(A `:836~843`, B `:981~984`) — 그대로
- `datumTransform` 블록(A `:823~834`, B `:967~978`) — 그대로
- `ReduceDomain` → `EdgesSubPix` → `UnionAdjacentContoursXld` → `ShapeTransXld` →
  `AreaCenterXld` → `TupleMax/TupleFind` → `SelectObj` → `SmallestRectangle2Xld` 이하 전부 — 그대로
- `finally` 의 Dispose 블록 — 그대로. **새 HALCON 아이코닉 객체를 만들지 않으므로 추가 대상 없음.**
  신규 out-`HTuple` 은 Dispose 하지 않는다 (`<htuple_dispose_decision>` 참조 — 의도적 결정).
- 메서드 시그니처 / 파라미터 이름(`roiLength1`, `roiLength2`), 호출부 4곳 — 그대로
- `:310` / `:525` 의 `GenMeasureRectangle2` 2곳 — 무관, 그대로
- `TryFitLine` / `AppendStrip` (`260910-ly4` 완료분) — 재수정 금지
  </action>
  <verify>
```bash
cd /c/code/DataMeasurement
F=WPF_Example/Halcon/Algorithms/VisionAlgorithmService.cs

# 1) 순환 로컬이 완전히 사라졌는가 (기대: 둘 다 0)
grep -c 'dHeightPx' "$F" || true
grep -c 'dWidthPx'  "$F" || true

# 2) 옛 포맷 잔재가 없는가 (기대: 0)
grep -cF 'gen_rectangle2: height=' "$F" || true

# 3) 실측 조회가 두 지점에 각각 1건씩 (기대: 각 2)
grep -c 'HOperatorSet.SmallestRectangle1(rect,' "$F" || true
grep -c 'HOperatorSet.SmallestRectangle2(rect,' "$F" || true

# 4) 새 로그 태그가 각각 1건씩 (기대: 각 1)
grep -c '\[ContourRect\] roi:' "$F" || true
grep -c '\[ShortAxis\] roi:'   "$F" || true

# 5) 새 포맷 그룹 라벨 3종이 각각 2건씩 (기대: 각 2)
grep -c 'teach: halfRow='   "$F" || true
grep -c 'region(aabb):'     "$F" || true
grep -c 'region(rect2):'    "$F" || true

# 6) 센티넬 상수/로컬 (기대: PROBE_FAIL 은 각 지점 선언1+참조5 = 총 12)
grep -c 'PROBE_FAIL' "$F" || true
grep -n 'dAabbHeight\|dAabbWidth\|dFitPhi\|dMajorLen\|dMinorLen' "$F"

# 6b) HTuple 요소 접근이 전부 .D 인지 — 신규 hv* 로컬에 .I 가 0건이어야 한다 (기대: 0)
grep -cE 'hv(Box|Fit)[A-Za-z0-9]*\.I\b' "$F" || true

# 7) 측정 로직 미변경 — 이 3종은 그대로 남아 있어야 한다 (기대: 각 2, 2, 2)
grep -c 'double dGenLen1 = roiLength2;' "$F" || true
grep -c 'double dGenLen2 = roiLength1;' "$F" || true
grep -c 'GenRectangle2(out rect, cRow, cCol, cPhi, dGenLen1, dGenLen2);' "$F" || true

# 8) GenMeasureRectangle2 는 미변경 (기대: 310, 525 두 줄 그대로)
grep -n 'GenMeasureRectangle2' "$F"

# 9) 순서 확인 — 각 메서드마다 GenRectangle2 < SmallestRectangle1 < SmallestRectangle2 < PrintLog < ReduceDomain
grep -n 'GenRectangle2(out rect\|SmallestRectangle1(rect\|SmallestRectangle2(rect\|ContourRect\] roi\|ShortAxis\] roi\|ReduceDomain(image' "$F"

# 10) rect 를 재할당/Dispose 하지 않았는가
#     기대: `HObject rect = null;` 2건(메서드 선두) + `rect.Dispose()` 2건(finally) — 총 4줄, 그 외 0.
#     로그 블록에서 rect 에 대입하거나 Dispose 하면 여기서 줄 수가 늘어난다.
grep -n 'rect = \|rect.Dispose' "$F"

# 11) diff 최소 범위
git diff --stat -- "$F"
git diff -U0 -- "$F"

# 12) 하드룰 — 추가 라인만 검사 (<hard_rules> 스크립트 실행)
```
  </verify>
  <done>
- 두 지점 모두 `SmallestRectangle1(rect, ...)` + `SmallestRectangle2(rect, ...)` 를 **자체 `try`/`catch { }`** 안에서 호출한다.
- `Logging.PrintLog` 는 그 격리 블록 **밖**에 있어, 조회 실패 시에도 센티넬 값으로 한 줄이 찍힌다.
- 로그 한 줄에 `teach:`(티칭 원본) / `region(aabb):`(축정렬 실측) / `region(rect2):`(HALCON 규약 실측) 3그룹이 모두 있다.
- `region(rect2)` 의 두 길이는 `major`/`minor` 로 라벨링되어 있다 (HALCON 이 긴 쪽을 앞에 놓기 때문).
- 순환 로컬 2개가 파일에서 완전히 제거되었다 (grep 0건).
- `dGenLen1`/`dGenLen2` 대입, `GenRectangle2` 호출, md3 주석, `datumTransform`, `ReduceDomain` 이후
  파이프라인, `finally` Dispose, 메서드 시그니처, 호출부 4곳 — **전부 무변경**.
- 신규 식별자는 헝가리언(`n`/`d`/`hv`) + 상수는 UPPER_SNAKE_CASE. 매직넘버 0.
- 변경 파일 1개. 커밋 1개 (`git add` 는 이 파일 하나만, 경로 명시).
  </done>
  <commit>refactor(quick-260910-mr6): [ContourRect]/[ShortAxis] 로그를 HALCON 실측 region 기반으로 교체 — 순환 로그 제거</commit>
</task>

<task id="2" type="auto">
  <name>Task 2: Debug/x64 빌드 에러 0 + 하드룰·금지파일 최종 검증 (커밋 없음)</name>
  <files>(없음 — 검증 전용)</files>
  <action>
빌드와 하드룰 grep, 금지 파일 미접촉을 확인해 회귀가 없음을 확정한다.
**이 태스크는 파일을 만들지도 커밋하지도 않는다.** 실패하면 `VisionAlgorithmService.cs`
를 고쳐 Task 1 커밋을 `--amend` 하거나 추가 fix 커밋을 낸다
(수정 대상 파일은 `VisionAlgorithmService.cs` 하나로 유지).

빌드 경고에 주의할 것: 센티넬 로컬을 선언만 하고 안 쓰면 `CS0219`(assigned but never used)
가 새로 뜬다. 5개 전부 `string.Format` 인자로 소비되므로 정상 구현이면 안 뜬다.
**이번 변경으로 새로 생긴 경고가 있으면 그것도 회귀로 간주하고 고칠 것.**
  </action>
  <verify>
```bash
cd /c/code/DataMeasurement

# 1) Debug|x64 빌드 — 에러 0
"C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" \
  WPF_Example/DatumMeasurement.csproj \
  //p:Configuration=Debug //p:Platform=x64 //v:minimal //nologo

# 1b) 이번 파일에서 새 경고가 생기지 않았는지 (기대: VisionAlgorithmService.cs 관련 신규 경고 0)
#     특히 CS0219 / CS0168 / CS0164

# 2) 하드룰 — 이번에 추가한 라인만 검사 (<hard_rules> 스크립트). 전부 0 이어야 함

# 3) 금지 파일 미접촉 확인 — 아래 6개는 출력이 없어야 한다
git status --porcelain WPF_Example/DatumMeasurement.csproj
git status --porcelain WPF_Example/Halcon/Algorithms/FAIEdgeMeasurementService.cs
git status --porcelain WPF_Example/Halcon/Algorithms/PatternMatchService.cs
git status --porcelain WPF_Example/Halcon/Algorithms/DatumFindingService.cs
git status --porcelain WPF_Example/UI/ContentItem/MainView.xaml.cs
git status --porcelain WPF_Example/Custom/Sequence/Inspection/Measurements/

# 4) 이번 작업이 만진 소스 파일이 정확히 1개인지
git diff --name-only HEAD~1 HEAD
#    기대: WPF_Example/Halcon/Algorithms/VisionAlgorithmService.cs 만

# 5) 같은 파일 내 무관 지점 미변경 확인
git diff -U0 HEAD~1 HEAD -- WPF_Example/Halcon/Algorithms/VisionAlgorithmService.cs | grep '^[-+]' | grep -v '^[-+][-+]'
#    기대: 로그 블록 2곳(주석 + 센티넬 + 격리 try/catch + PrintLog)만.
#    TryFitLine / AppendStrip / TryFindCircleByPolarSampling / GenMeasureRectangle2 /
#    dGenLen* 대입 / GenRectangle2 호출 / ReduceDomain 이하 관련 라인 0건
```
  </verify>
  <done>
- MSBuild `Debug|x64` 에러 0 (경고는 기존 수준 `CS0618`/`CS0169` 유지, 신규 경고 0).
- 하드룰 grep 6종 모두 추가 라인 기준 0.
- 금지 파일 6개(`DatumMeasurement.csproj`, `FAIEdgeMeasurementService.cs`, `PatternMatchService.cs`,
  `DatumFindingService.cs`, `MainView.xaml.cs`, `Custom/.../Measurements/`) 전부 변경 없음.
- 이번 작업의 변경 소스 파일이 `VisionAlgorithmService.cs` 단 하나, 코드 커밋 1개.
- 같은 파일의 `TryFitLine` / `AppendStrip` / `TryFindCircleByPolarSampling` / `GenMeasureRectangle2` /
  `dGenLen*` / `GenRectangle2` 호출 / `ReduceDomain` 이하 — 전부 무변경.
  </done>
</task>

<task id="3" type="auto">
  <name>Task 3: SUMMARY 작성 + STATE 행 추가 + 문서 커밋 (미추적 ly4 VERIFICATION.md 포함)</name>
  <files>
.planning/quick/260910-mr6-log-actual-halcon-region-extents-via-sma/SUMMARY.md
.planning/quick/260910-mr6-log-actual-halcon-region-extents-via-sma/PLAN.md
.planning/quick/260910-ly4-measurement-point-roi-search-region-axis/VERIFICATION.md
.planning/STATE.md
  </files>
  <precondition>`.planning/quick/260910-ly4-measurement-point-roi-search-region-axis/VERIFICATION.md` 가 워킹트리에 미추적(`??`) 상태로 존재한다 — 계획 작성 시점 `git status --porcelain` 확인 완료.</precondition>
  <action>
SUMMARY.md 를 작성하고 STATE.md 에 행을 추가한 뒤, 문서 일체를 **한 커밋**으로 묶는다.

**SUMMARY.md 에 반드시 담을 것:**
1. 무엇을 왜 바꿨는가 — 순환 로그 → HALCON 실측 로그. 사용자 원문 인용
   ("regionfeature 함수를 써서 꼭지점 4점을 구해서 사이즈별로 확인하면...").
2. **최종 로그 포맷 한 줄**과 각 필드의 출처(티칭 원본 vs 실측).
3. **`<expected_log_output>` 의 6건 기대값 표를 그대로 옮겨 적을 것** — 사용자가 실기 로그와
   1:1 대조할 수 있어야 한다. "회귀면 이렇게 찍힌다" 대비표와 예외 판독표도 함께.
4. **`<design_decision>` 의 A/B 선택 근거 요약** — 특히 `smallest_rectangle2` 가
   `Length1 >= Length2` 로 정규화하므로 `major`/`minor` 숫자만으로는 가로/세로를 구분할 수
   없고 `phi` 가 구분자라는 점. 오케스트레이터 초안(B 우선)에서 바뀐 지점임을 명시.
5. **`<safety_design>` 4중 장치**와, `<htuple_dispose_decision>` 의 의도적 규칙 예외 기록.
6. **영향 범위** — 이번 변경은 **관측 전용**이므로 측정값은 변하지 않아야 한다.
   md3 로 이미 바뀐 측정값 재검증은 여전히 유효(md3 SUMMARY 참조). TOP/BOTTOM PC 동일 적용.
7. **md3 SUMMARY 의 UAT 1번 예시 문구가 이번 포맷 변경으로 낡았다는 사실**을 명시.
   라벨이 `gen_rectangle2:` → `region(aabb):` 로 바뀌었고 `region(rect2):` 그룹이 새로 붙는다.
   **숫자는 우연히 같다** — 현재 코드가 옳기 때문이며, 값의 출처가 "인자에서 역산"에서
   "HALCON region 실측"으로 바뀐 것이 이번 변경의 본질이다.
   md3 SUMMARY 파일 자체는 **수정하지 않는다** — 사후 정정은 이 SUMMARY 에서 한다.
8. `<uat>` 5항목을 체크박스로 그대로 옮길 것.

**STATE.md** — `260910-md3` 행(`:715`) **바로 아래에 1행만 추가**한다. 기존 행 수정 금지.
컬럼 형식은 기존과 동일: `| id | date | 설명 | commits | verdict |`.

**커밋 대상 4개를 경로 명시로 add 한다** (미추적 `ly4/VERIFICATION.md` 포함 — 지시받은 항목):
- `.planning/quick/260910-mr6-log-actual-halcon-region-extents-via-sma/PLAN.md`
- `.planning/quick/260910-mr6-log-actual-halcon-region-extents-via-sma/SUMMARY.md`
- `.planning/quick/260910-ly4-measurement-point-roi-search-region-axis/VERIFICATION.md`
- `.planning/STATE.md`

`git add -A` / `git add .` **절대 금지.**
  </action>
  <verify>
```bash
cd /c/code/DataMeasurement

# 1) 4개 파일이 정확히 스테이지에 올랐는지 (기대: 정확히 이 4줄)
git add .planning/quick/260910-mr6-log-actual-halcon-region-extents-via-sma/PLAN.md \
        .planning/quick/260910-mr6-log-actual-halcon-region-extents-via-sma/SUMMARY.md \
        .planning/quick/260910-ly4-measurement-point-roi-search-region-axis/VERIFICATION.md \
        .planning/STATE.md
git status --porcelain --untracked-files=no

# 2) 소스 파일이 스테이지에 섞이지 않았는지 (기대: 출력 없음)
git diff --cached --name-only | grep 'WPF_Example/' || true

# 3) csproj 가 스테이지에 없는지 (기대: 출력 없음)
git diff --cached --name-only | grep 'DatumMeasurement.csproj' || true

# 4) ly4 VERIFICATION.md 가 실제로 포함됐는지 (기대: 1줄)
git diff --cached --name-only | grep -c 'ly4.*VERIFICATION.md' || true

# 5) STATE.md 는 1행 추가만인지 (기대: + 1줄, - 0줄)
git diff --cached --numstat -- .planning/STATE.md

# 6) 커밋 후 최종 확인 — 이번 작업 커밋 2개
git log --oneline -3
git status --porcelain
```
  </verify>
  <done>
- SUMMARY.md 에 6건 기대값 표 / 회귀 시 대비표 / 예외 판독표 / A·B 선택 근거 /
  안전 5중 장치 / HTuple Dispose 예외 기록 / md3 UAT 문구 사후 정정 / UAT 5항목이 모두 있다.
- STATE.md 에 `260910-mr6` 행이 **1행만** 추가되었다 (`--numstat` 이 `1 0`).
- 커밋에 4개 파일이 정확히 들어갔고, 그중 하나가 미추적이던 `ly4/VERIFICATION.md` 다.
- 스테이지에 `WPF_Example/` 파일이 하나도 없다.
- 이번 작업 총 커밋 2개(Task 1 코드 1 + Task 3 문서 1), 워킹트리 clean.
  </done>
  <commit>docs(quick-260910-mr6): SUMMARY + STATE 행 추가 — 실측 region 로그 전환, ly4 VERIFICATION 동봉</commit>
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
7. 중괄호 생략 → 한 줄 분기라도 필수. **빈 `catch { }` 도 중괄호를 쓴다**
8. 매직넘버 → 이름 있는 `const`. 이번 실패 센티넬 `-1.0` 은 `const double PROBE_FAIL` 로 이름을 준다
9. 신규 식별자 헝가리언 접두: `b`(bool) `n`(int) `sz`(string) `d`(double) `hv`(HTuple),
   상수는 UPPER_SNAKE_CASE.
   ※ **기존 로컬(`cRow`, `cCol`, `cPhi`, `rect`, `roiLength1/2`, `dGenLen1/2`) 이름은 바꾸지 말 것.**
10. `HImage`/`HObject`/`HTuple` Dispose — 신규 아이코닉 객체 없음. 신규 out-`HTuple` 은
    **의도적으로 미해제**(`<htuple_dispose_decision>` 에 근거 기록). 이 결정을 조용히 뒤집지 말 것
11. 파일 스타일은 **Allman** brace. 들여쓰기 공백 16칸(메서드 `try` 블록 내부), 격리 블록 내부는 20칸

**검증 grep 을 자기무효화하지 않도록 — 신규 주석에 아래 문자열을 쓰지 말 것:**
`dHeightPx`, `dWidthPx`, `gen_rectangle2: height=`
(Task 1 verify 가 이 3개를 0건으로 기대한다. 주석에 언급하면 grep 이 세어 실패한다.
 주석에서 그 개념을 말해야 한다면 "인자에서 역산한 값" 처럼 **이름을 쓰지 말고 서술**할 것.)

**전체 파일 grep 금지 — 이 파일에는 기존 위반이 있어 항상 실패한다.**
반드시 `git diff` 의 **추가 라인(`+`)만** 검사한다:

```bash
cd /c/code/DataMeasurement
F=WPF_Example/Halcon/Algorithms/VisionAlgorithmService.cs
OUT="$TMPDIR/mr6_added.txt"; [ -z "$TMPDIR" ] && OUT=./mr6_added.txt

# 커밋 전이면 아래 그대로. 커밋 후면 `git diff -U0 HEAD~1 HEAD -- "$F"` 로 바꿔 사용.
git diff -U0 -- "$F" | grep '^+' | grep -v '^+++' > "$OUT"

# grep -c 는 0건일 때 exit 1 이라 스크립트가 죽는다 → 반드시 `|| true` 를 붙일 것
grep -cE '\?[^?]*:'   "$OUT" || true   # 삼항       -> 0
grep -cF '??'         "$OUT" || true   # null 병합  -> 0
grep -cF '?.'         "$OUT" || true   # null 조건  -> 0
grep -cE 'switch.*=>' "$OUT" || true   # switch 식  -> 0
grep -cF 'hbk'        "$OUT" || true   # 날짜 주석  -> 0

# 6종째 — 매직넘버. 리터럴 -1 은 const 정의 줄에만 존재해야 한다.
grep -cF 'const double PROBE_FAIL = -1.0;' "$OUT" || true             # -> 2 (지점당 1줄)
grep -F -- '-1' "$OUT" | grep -vF 'const double PROBE_FAIL' || true    # -> 출력 없음

rm -f "$OUT"   # 임시 파일은 저장소에 남기지 말 것
```
(임시 파일을 저장소 루트에 만들었다면 **반드시 삭제**하고, 절대 스테이징하지 말 것.)
</hard_rules>

<forbidden>
아래는 **절대 수정 금지**. 위반 시 회귀로 간주한다.

1. **`VisionAlgorithmService.cs` 의 측정 로직 전체** — 이번은 **관측 전용** 변경이다.
   `dGenLen1`/`dGenLen2` 대입, `GenRectangle2` 호출, md3 "왜" 주석 블록, `datumTransform` 블록,
   `ReduceDomain` 이후 파이프라인, `finally` Dispose, 메서드 시그니처 — **전부 불변.**
   로그 블록 2곳(A `:848~854`, B `:989~995`) **외에는 한 줄도 바꾸지 않는다.**

2. **`VisionAlgorithmService.cs` 의 `TryFitLine` / `AppendStrip`** (`:20~`, `GenMeasureRectangle2` `:310`) —
   `260910-ly4` 에서 이미 고쳤다. 그 `[FitLine] strip-loop: bounds` 로그는 `top/left/bottom/right`
   가 그대로 `GenRectangle1(row1, col1, row2, col2)` 로 넘어가고 `GenRectangle1` 은 인자 의미가
   row/col 로 명시적이라 **해석의 여지가 없다 — 순환이 아니다. 이번 작업 범위 밖. 건드리지 말 것.**

3. **`VisionAlgorithmService.cs` 의 `TryFindCircleByPolarSampling`** (`:435~`, `GenMeasureRectangle2` `:525`) —
   `halfL1`/`halfL2` 를 `radius * ratio` 로 직접 계산하고 `rectPhi = thetaRad`(반경 방향)라
   **티칭 필드를 쓰지 않는다.** 정상. **건드리지 말 것.**

4. **`WPF_Example/Halcon/Algorithms/FAIEdgeMeasurementService.cs`** — FAI `ROI_Length1/2` 는
   **CONTEXT.md D-02 LOCKED**, `:131` 에 "swap 금지" 주석 존재. **읽기만.**

5. **`WPF_Example/Halcon/Algorithms/PatternMatchService.cs`** — Datum `PatternRoi_*` 는
   티칭과 `:173 GenRectangle2` 가 HALCON 규약끼리 이미 일치. 정상. **읽기만.**

6. **`WPF_Example/Halcon/Algorithms/DatumFindingService.cs`** — `:2197` 의
   `SmallestRectangle2(stripRegion, ...)` 는 이번 작업의 **선례로 참조만** 한다. **수정 금지.**

7. **`WPF_Example/UI/ContentItem/MainView.xaml.cs`** — 티칭/리사이즈/표시 3경로 전부 옳다
   (md3 에서 재확인 완료). CLAUDE.md 가 이 비대한 code-behind(4,400줄+)에 신규 로직 추가를 금지한다.

8. **`Custom/Sequence/Inspection/Measurements/Compound*.cs` 4개** — 호출부는 그대로가 맞다. **수정 금지.**

9. **`WPF_Example/DatumMeasurement.csproj`** — 이 PC 의 실HW 세팅이 들어 있다.
   **스테이징/커밋 절대 금지.** → **신규 `.cs` 파일 생성 불가** (classic MSBuild 는
   `<Compile Include>` 추가가 필요하고 그러면 csproj 이 더러워진다). 기존 파일 안에서 해결할 것.
   ※ 계획 작성 시점 기준 csproj 에 미커밋 변경 없음 확인. 가드는 유지.

10. **`D:\Data\Recipe\FAI_1\main.ini`** — 운영 레시피. **읽기만, 쓰기 금지.**

11. **`.planning/quick/260910-md3-.../SUMMARY.md`** — 이미 커밋된 기록이다. 포맷 변경에 따른
    사후 정정은 **이번 SUMMARY 에서** 한다. md3 파일 자체는 **수정하지 말 것.**

12. **`.planning/STATE.md`** — 다른 PC 와 공유되어 병합 충돌이 잦다. **행 추가만** 하고
    기존 행은 건드리지 말 것.
</forbidden>

<git_constraints>
- 이 환경에서는 `git stash` / `git checkout -- <path>` / `git restore` 류의 **작업물 되돌리기
  명령이 차단**되어 있다. 실수로 덮어쓰면 복구가 어려우니 편집 전에 대상 위치를 grep 으로
  정확히 확정하고 최소 범위로 편집할 것.
- **`git add -A` / `git add .` 절대 금지.** 커밋마다 대상 경로를 명시할 것.
- 커밋 전 `git status --porcelain` 으로 `DatumMeasurement.csproj` 가 스테이지에 없는지 확인한다.
- 브랜치: `main`. 계획 작성 시점 워킹트리는 미추적 파일 1개
  (`.planning/quick/260910-ly4-.../VERIFICATION.md`) 외에는 clean 하며 **소스 변경은 없다.**
  그 미추적 파일은 **Task 3 문서 커밋에 반드시 포함**한다 (지시받은 항목).
- 커밋 총 2개: Task 1(코드), Task 3(문서). Task 2 는 검증 전용.
</git_constraints>

<impact_scope>
**SUMMARY.md 에 반드시 그대로 옮겨 적을 것.**

## 이번 변경 자체의 영향 — **관측 전용. 측정값은 변하지 않아야 한다.**

이것이 이번 작업의 **안전성 회귀 테스트**다. `dGenLen1`/`dGenLen2` 계산과 `GenRectangle2`
호출을 건드리지 않았으므로, Compound 4종 측정값은 **md3 수정 직후와 정확히 동일**해야 한다.
값이 변했다면 로그가 측정에 영향을 준 것이고 — 사용자가 우려한 바로 그 상황이다 → 즉시 롤백.

## 로그를 읽는 대상 — 측정 타입 4종 / 서비스 메서드 2개 / 운영 레시피 항목 6개

- `TryFindLargestContourRect` → `[ContourRect]` ← `CompoundAngleMeasurement`(1) /
  `CompoundCenterBDistanceMeasurement`(2) / `CompoundCenterCDistanceMeasurement`(2) — 레시피 5건
- `TryFindShortAxisIntersections` → `[ShortAxis]` ← `CompoundShortAxisDistanceMeasurement`(1) — 레시피 1건

기대 로그값 6건은 `<expected_log_output>` 표 참조.

## 낡아진 문서 — 사후 정정 대상

`260910-md3` SUMMARY.md 의 UAT 1번 예시 문구
(`halfRow=189.0 halfCol=199.0  gen_rectangle2: height=378.0 width=398.0`)는
이번 포맷 변경으로 **낡았다.** 새 포맷은
`teach: halfRow=189.0 halfCol=199.0  region(aabb): height=378.0 width=398.0  region(rect2): phi=0.0000 major=199.0 minor=189.0` 다.

**숫자가 같은 것은 우연이 아니라 현재 코드가 옳다는 뜻이다.** 바뀐 것은 두 가지다:
(1) 라벨 — `gen_rectangle2:` → `region(aabb):`, 그리고 `region(rect2):` 그룹 신설,
(2) **값의 출처** — "우리가 넘긴 인자에서 역산"에서 **"HALCON 이 만든 region 을 실측"** 으로.
(2) 가 이번 작업의 본질이다. 앞으로 축이 뒤집히면 **이제는 로그가 실제로 뒤집힌다.**

md3 SUMMARY 파일은 수정하지 않고, 이번 SUMMARY 에 사후 정정으로 기록한다.

## TOP / BOTTOM PC

로그 포맷 변경이므로 두 PC 모두 pull → 리빌드해야 같은 로그를 본다.
측정 동작은 변하지 않으므로 재티칭은 불필요하다 (md3 로 인한 재검증은 별건으로 계속 유효).
</impact_scope>

<uat>
실기 UAT — **사용자가 직접 수행.** SUMMARY.md 에 체크 항목으로 남길 것.

- [ ] **1. 실측 로그 불변식 확인 (핵심 판정)**
      Compound 측정이 포함된 Shot(`SHOT_7`, `SHOT_14`, `SHOT_15`)을 오프라인 검사 →
      `D:\Data\Algorithm\` 의 당일 `*_Algorithm.log` 에서 `[ContourRect]` / `[ShortAxis]` 줄을 찾는다.
      **판정은 절대 좌표가 아니라 로그 한 줄 안의 불변식으로 한다:**
      ```
      region(aabb): height ≈ 2 * teach.halfRow      (±1 px 는 래스터화, 정상)
      region(aabb): width  ≈ 2 * teach.halfCol      (±1 px 는 래스터화, 정상)
      region(rect2): phi ≈ 0.0000                   (현재 6건 전부 Rect_Phi=0)
      ```
      6건 기대값은 `<expected_log_output>` 표와 1:1 대조할 것.
      **⚠ `major`/`minor` 숫자만 보고 판정하지 말 것** — HALCON 이 긴 쪽을 앞에 놓도록
      정규화하므로 정상일 때와 축이 뒤집혔을 때 **두 숫자가 동일**하다. 구분자는 `aabb` 와 `phi` 다.

- [ ] **2. 측정값 무변화 확인 (안전성 회귀 테스트 — 이번 작업의 핵심)**
      같은 검사에서 Compound 4종 6개 항목의 측정값이 **md3 수정 직후와 동일**한지 확인.
      이번 변경은 관측 전용이므로 **값이 변하면 안 된다.** 변했다면 즉시 보고 → 롤백.

- [ ] **3. `260910-ly4` / 기타 측정 회귀 확인**
      `EdgeToLineDistance` 등 `TryFitLine` 계열(측정 타입 9종, 17곳)이 회귀하지 않았는지 확인.
      `[FitLine] strip-loop: bounds ...` 로그는 이번에 손대지 않았으므로 **포맷·값 모두 그대로**여야 한다.

- [ ] **4. 검출 결과가 그린 박스 안인지 육안 확인**
      Compound 측정의 검출 결과(LargestRect / 교점 / 측정선)가 화면 파란 박스 **안**에 들어오는지 확인.
      (현재 6건은 전부 `Rect_Phi=0` 이라 화면 박스와 탐색 박스의 회전이 일치해 육안 비교가 유효하다.)

- [ ] **5. 예외 값이 찍히지 않았는지 확인**
      로그에 `height=-1.0 width=-1.0` 또는 `height=0.0 width=0.0` 이 보이는지 확인.
      - `-1.0` → region 조회가 예외를 던졌다. **측정에는 영향 없음.** 원인 조사는 별건으로 보고.
      - `0.0` → region 이 비어 있다(미티칭 ROI). 해당 항목 재티칭.
      - `aabb` 가 `teach` 값보다 작게 나옴 → HALCON region clipping 의심. 별건으로 보고.
      전체 판독표는 `<expected_log_output>` 의 "예외 상황 판독표" 참조.
</uat>

<success_criteria>
- `[ContourRect]` / `[ShortAxis]` 로그의 폭·높이가 **HALCON 이 실제로 만든 region 을 되읽어**
  나온 값이며, 코드의 `gen_rectangle2` 인자 해석 가정에서 파생되지 않는다 (순환 로그 제거).
- 로그 한 줄에 `teach:`(티칭 원본) / `region(aabb):`(축정렬 실측) / `region(rect2):`(HALCON 규약 실측)
  3그룹이 함께 찍혀, 한 줄만으로 `aabb.height ≈ 2*teach.halfRow`, `aabb.width ≈ 2*teach.halfCol`
  불변식을 판정할 수 있다.
- `region(rect2)` 의 두 길이는 `major`/`minor` 로 라벨링되어, HALCON 정규화(`Length1 >= Length2`)로
  인한 오독 위험이 이름 차원에서 차단된다.
- region 조회가 **자체 `try`/`catch { }` 로 격리**되어 어떤 경우에도 바깥
  `catch { return false; }` 로 새지 않는다 — **로그가 측정 결과에 영향을 줄 수 없다.**
- 조회가 실패해도 **로그 한 줄은 반드시 찍힌다** (센티넬 `-1.0`) — 진단 불가 상태를 만들지 않는다.
- `rect` region 은 읽기 전용으로만 다뤄지며(`input_object`/`const Hobject` 문서 근거),
  직후의 `ReduceDomain` 이 영향을 받지 않는다.
- **측정 로직 diff 0** — `dGenLen*` 대입, `GenRectangle2` 호출, `datumTransform`, `ReduceDomain`
  이하 파이프라인, `finally` Dispose, 시그니처, 호출부 4곳 전부 무변경.
- MSBuild `Debug|x64` 에러 0, 신규 경고 0.
- 변경 소스 파일 1개(`VisionAlgorithmService.cs`), 커밋 2개(코드 1 + 문서 1), 금지 항목 12종 미접촉.
- 하드룰 grep 6종 모두 추가 라인 기준 0.
- SUMMARY.md 에 6건 기대값 표 / 회귀 대비표 / 예외 판독표 / A·B 선택 근거 /
  안전 5중 장치 / `HTuple` Dispose 의도적 예외 / md3 UAT 문구 사후 정정 / UAT 5항목 기록.
- 문서 커밋에 미추적이던 `.planning/quick/260910-ly4-.../VERIFICATION.md` 가 포함되었다.
</success_criteria>

<output>
`.planning/quick/260910-mr6-log-actual-halcon-region-extents-via-sma/SUMMARY.md`
</output>
