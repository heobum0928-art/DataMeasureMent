---
phase: quick-260517-ijg
plan: 01
type: execute
wave: 1
depends_on: []
files_modified:
  - WPF_Example/Halcon/Algorithms/VisionAlgorithmService.cs
autonomous: false
requirements:
  - CO-23-01

must_haves:
  truths:
    - "TryFitLine 이 ROI 를 strip 으로 분할해 strip 마다 MeasurePos 를 돌려 에지점을 누적한다"
    - "단일 ROI 한 변에서 2개 이상(보통 stripCount 개)의 에지점이 모여 FitLineContourXld 가 직선을 피팅한다"
    - "EdgeToLineDistance / PointToLine / PointToPoint / LineToLineAngle / LineToLineDistance 측정값이 '—' 가 아니라 mm 값으로 표시된다"
    - "datumTransform 변환 로직과 rPhi 회전 보정이 보존된다 (Datum 런타임 보정 회귀 0)"
    - "TryFitLine 시그니처가 무변경이라 5개 caller 무수정으로 동일 strip-loop 개선을 받는다"
  artifacts:
    - path: "WPF_Example/Halcon/Algorithms/VisionAlgorithmService.cs"
      provides: "strip-loop MeasurePos 누적으로 재작성된 TryFitLine + private strip 헬퍼"
      contains: "AppendStrip"
  key_links:
    - from: "TryFitLine"
      to: "private strip 헬퍼 (AppendStrip)"
      via: "stripCount 회 호출 + ref HTuple 누적"
      pattern: "AppendStrip"
    - from: "TryFitLine"
      to: "FitLineContourXld"
      via: "GenContourPolygonXld(누적 allRows/allCols)"
      pattern: "GenContourPolygonXld"
---

<objective>
`VisionAlgorithmService.TryFitLine` 을 strip-loop MeasurePos 누적 패턴으로 재작성한다.

**근본 원인:** 현재 `TryFitLine` 은 `GenMeasureRectangle2` + `MeasurePos` 를 ROI 전체에 대해 **단 1회만** 호출한다. 단일 MeasurePos 는 측정 축 1개를 따라서만 에지를 반환하므로 `FitLineContourXld` 입력 점이 1~2개뿐이거나 collinear → "insufficient edge points (1)" → `TryFitLine` false → `TryExecute` false → EdgeToLineDistance 등 측정값 컬럼이 '—' 로 표시된다.

라인 피팅은 에지를 따라 퍼진 점이 필요하다. `DatumFindingService.TryFindLine` (검증된 CANONICAL 구현)처럼 ROI 를 strip 으로 쪼개 strip 마다 MeasurePos 를 돌려 에지점을 누적해야 한다.

Purpose: UAT 에서 발견된 측정값 미표시('—')의 구조적 차단을 제거한다 (CO-23-01).
Output: strip-loop 로 재작성된 `TryFitLine` + VisionAlgorithmService 내부 private strip 헬퍼. 시그니처 무변경.
</objective>

<execution_context>
@$HOME/.claude/get-shit-done/workflows/execute-plan.md
@$HOME/.claude/get-shit-done/templates/summary.md
</execution_context>

<context>
@.planning/STATE.md

# 수정 대상 — TryFitLine 현재 단일 MeasurePos 구조
@WPF_Example/Halcon/Algorithms/VisionAlgorithmService.cs

# CANONICAL 레퍼런스 — strip-loop 검증된 구현 (복사 기준)
@WPF_Example/Halcon/Algorithms/DatumFindingService.cs

# TryFitLine caller (시그니처 무변경 → 무수정. 동작 변화만 확인용)
@WPF_Example/Custom/Sequence/Inspection/Measurements/EdgeToLineDistanceMeasurement.cs

<interfaces>
<!-- 실행자가 알아야 할 핵심 계약. 코드베이스에서 추출 — 추가 탐색 불필요. -->

TryFitLine 시그니처 (VisionAlgorithmService.cs:18-27) — 이 시그니처는 절대 변경하지 않는다:
```csharp
public bool TryFitLine(
    HImage image,
    double roiRow, double roiCol, double roiPhi,
    double roiLength1, double roiLength2,
    HTuple datumTransform,
    int sampleCount, int trimCount, double sigma, int threshold,
    string direction, string polarity,
    out double row1, out double col1, out double row2, out double col2,
    out string error,
    string selection = "all")
```

CANONICAL 패턴 — AppendEdgePointsFromStrip (DatumFindingService.cs:1419-1496):
- GenRectangle1(stripRegion, row1, col1, row2, col2)
- SmallestRectangle2 → rr, rc, rp, rh, rw  (rp 자동 phi 는 사용 안 함)
- direction → measurePhi 4-way 명시 매핑: TtoB=-π/2, BtoT=+π/2, RtoL=π, LtoR=0
- selection (PascalCase) → Halcon lower: First→"first", Last→"last", All→"all"
- GenMeasureRectangle2(rr, rc, measurePhi, rh, rw, imgW, imgH, "nearest_neighbor")
- MeasurePos(image, handle, sigma, threshold, polarity, selectionLower) → edgeRows, edgeCols
- TupleConcat 으로 allRows/allCols 누적; strip 실패(빈 결과/예외)는 swallow
- finally 에서 CloseMeasure + stripRegion.Dispose

CANONICAL 패턴 — TryFindLine strip-loop 본문 (DatumFindingService.cs:1156-1278):
- bounding box: top=roiRow-halfH, bottom=roiRow+halfH, left=roiCol-halfW, right=roiCol+halfW (halfW=Length1, halfH=Length2)
- scanHorizontal = (direction != "TtoB" && direction != "BtoT")
- stripCount = sampleCount (sentinel 0 또는 음수 → 기본 20), 최소 1
- scanHorizontal 이면 row 분할(r1..r2, 전체 폭 left..right), 아니면 col 분할(c1..c2, 전체 높이 top..bottom)
- trimCount: 누적 점 양 끝 제거 (edgeCount > 2*trimCount+1 일 때만)
- edgeCount < 2 면 error 설정 후 false
- GenContourPolygonXld(allRows, allCols) + FitLineContourXld(contour, "tukey", -1, 0, 5, 2, ...)
</interfaces>

<polarity_convention_warning>
중요 — 두 서비스의 polarity 규약이 다르다:
- `TryFitLine` (현재): caller 가 "LightToDark"/"DarkToLight" 전달 → 내부에서 LightToDark→"negative", else→"positive" 매핑
- `AppendEdgePointsFromStrip` (CANONICAL): Halcon polarity 문자열("positive"/"negative"/"all")을 그대로 MeasurePos 에 넘김

→ TryFitLine 안에 작성할 strip 헬퍼는 **이미 매핑된 Halcon polarity 문자열**("positive"/"negative")을 인자로 받아야 한다. 기존 `pol` 변수 매핑 로직(L78-86)을 strip-loop 진입 전에 그대로 유지하고, 그 결과 `pol` 을 strip 헬퍼에 넘긴다. CANONICAL 의 polarity clamp(`string.IsNullOrEmpty(polarity)→"all"`)는 도입하지 말 것 — caller 규약이 달라 회귀 위험.
</polarity_convention_warning>
</context>

<tasks>

<task type="auto" tdd="false">
  <name>Task 1: TryFitLine 을 strip-loop MeasurePos 누적으로 재작성 + VisionAlgorithmService 내부 private strip 헬퍼 추가</name>
  <files>WPF_Example/Halcon/Algorithms/VisionAlgorithmService.cs</files>

  <action>
**목표:** `TryFitLine` (VisionAlgorithmService.cs:18-139) 내부의 단일 `GenMeasureRectangle2` + `MeasurePos` 호출(L88-118)을 strip-loop 누적으로 교체한다. 시그니처는 절대 변경하지 않는다.

**보존할 부분 (수정 금지):**
1. L29-36: 진입부 null 가드 (`row1=col1=row2=col2=0; error=null; if (image==null)...`)
2. L42-59: datumTransform 변환 블록 — `rRow/rCol/rPhi` 산출. 그대로 유지.
3. L61-62: `image.GetImageSize(out imageWidth, out imageHeight)`.
4. L64-75: direction → `measurePhi` 4-way 매핑 + `measurePhi += (rPhi - roiPhi)` rPhi 회전 보정. 그대로 유지.
5. L78-86: polarity → `pol` 매핑 ("LightToDark"→"negative", else→"positive"). 그대로 유지. **CANONICAL 의 polarity clamp 는 도입하지 말 것** (polarity_convention_warning 참조).
6. L93-106: selection → `measureSel` 매핑 ("First"→"first", "Last"→"last", else→"all"). 그대로 유지.
7. L129-138: catch + finally 블록. 단 finally 의 `measureHandle` 해제는 strip 헬퍼가 strip 별로 처리하므로 메서드 레벨 `measureHandle` 변수는 제거 (아래 참조).

**교체할 부분 — L88-127 (단일 MeasurePos + FitLine 블록) 을 strip-loop 로 교체:**

a. `measurePhi` 산출 직후, **rPhi 회전 strip 의 bounding box** 를 계산한다. CANONICAL TryFindLine 은 Phi=0 가정이지만 TryFitLine 은 datum 회전으로 `rPhi != 0` 가능하다. **strip 분할은 회전을 따로 다루지 않고 회전 비회전 strip(축 정렬 bounding box)으로 처리하되, 이미 산출된 `measurePhi`(rPhi 보정 포함)를 GenMeasureRectangle2 에 넘겨 측정 축 자체를 회전시킨다.** 즉:
   - bounding box 는 `rRow/rCol` 중심의 축 정렬 박스: `halfW=roiLength1, halfH=roiLength2, top=rRow-halfH, bottom=rRow+halfH, left=rCol-halfW, right=rCol+halfW`.
   - strip region 은 `GenRectangle1` (축 정렬)으로 분할.
   - strip 마다 `SmallestRectangle2` 로 strip 중심 `rr/rc` 와 half-size `rh/rw` 추출.
   - `GenMeasureRectangle2(rr, rc, measurePhi, rh, rw, ...)` — phi 인자로 `measurePhi`(rPhi 회전 보정 + direction 매핑 합산값)를 넘긴다. rPhi 회전은 측정 축 phi 로 흡수된다.
   - 이 방식의 근거를 주석으로 명시: `//260517 hbk — rPhi 회전은 strip region 회전 대신 measurePhi 로 흡수 (축 정렬 strip + 회전된 측정 축). datum 회전 ROI 도 동일 경로.`

b. `scanHorizontal` 은 이미 L65-73 에서 산출됨 (TtoB/BtoT 면 false, 그 외 true). 이 값을 strip 분할 방향 결정에 재사용한다.

c. `stripCount` 산출 — CANONICAL 패턴 그대로:
   ```
   int stripCount = 20;
   if (sampleCount > 0) stripCount = sampleCount;
   if (stripCount < 1) stripCount = 1;
   ```

d. strip-loop 누적:
   ```
   HTuple allRows = new HTuple();
   HTuple allCols = new HTuple();
   double widthPx  = right - left;
   double heightPx = bottom - top;
   if (scanHorizontal)
   {
       for (int i = 0; i < stripCount; i++)
       {
           double r1 = top + (i * heightPx / stripCount);
           double r2 = top + ((i + 1) * heightPx / stripCount);
           AppendStrip(image, r1, left, r2, right, imageWidth, imageHeight,
               Math.Max(0.4, sigma), Math.Max(1, threshold), pol, measurePhi, measureSel,
               ref allRows, ref allCols);
       }
   }
   else
   {
       for (int i = 0; i < stripCount; i++)
       {
           double c1 = left + (i * widthPx / stripCount);
           double c2 = left + ((i + 1) * widthPx / stripCount);
           AppendStrip(image, top, c1, bottom, c2, imageWidth, imageHeight,
               Math.Max(0.4, sigma), Math.Max(1, threshold), pol, measurePhi, measureSel,
               ref allRows, ref allCols);
       }
   }
   ```
   주의: 기존 단일 MeasurePos 가 `Math.Max(0.4, sigma)` / `Math.Max(1, threshold)` clamp 를 썼으므로 동일하게 유지 (위 호출에 반영됨).

e. trimCount 적용 — CANONICAL 패턴 그대로:
   ```
   int edgeCount = allRows.TupleLength();
   if (trimCount > 0 && edgeCount > 2 * trimCount + 1)
   {
       HTuple trimmedR = allRows.TupleSelectRange(trimCount, edgeCount - trimCount - 1);
       HTuple trimmedC = allCols.TupleSelectRange(trimCount, edgeCount - trimCount - 1);
       allRows = trimmedR;
       allCols = trimmedC;
       edgeCount = allRows.TupleLength();
   }
   ```

f. edge 개수 게이트 — 기존 `error` 문구 스타일 유지 (edgeCount 노출):
   ```
   if (edgeCount < 2)
   {
       error = "insufficient edge points (" + edgeCount + ") across " + stripCount + " strips";
       return false;
   }
   ```

g. FitLineContourXld — 기존 L120-127 로직 그대로 누적된 `allRows/allCols` 에 적용:
   ```
   HOperatorSet.GenContourPolygonXld(out contour, allRows, allCols);
   HTuple lr1, lc1, lr2, lc2, nr, nc, df;
   HOperatorSet.FitLineContourXld(contour, "tukey", -1, 0, 5, 2,
       out lr1, out lc1, out lr2, out lc2, out nr, out nc, out df);
   row1 = lr1.D; col1 = lc1.D;
   row2 = lr2.D; col2 = lc2.D;
   return true;
   ```

**메서드 레벨 변수 정리:**
- `HTuple measureHandle = null;` (L38) 제거 — strip 헬퍼가 strip 별로 measure handle 을 열고 닫는다.
- finally 블록 (L134-138) 에서 `measureHandle` 해제 라인 제거. `contour` 해제는 유지.
- `HObject contour = null;` (L39) 유지.

**private strip 헬퍼 추가 — `AppendStrip`:**
`TryFitLine` 메서드 바로 아래(또는 클래스 내 적절한 위치)에 `private void AppendStrip(...)` 를 추가한다. **DatumFindingService.AppendEdgePointsFromStrip 의 동등 구현** 이지만 polarity 규약 차이 때문에 별도 작성한다 (헬퍼 위치 결정: 옵션 (a) — VisionAlgorithmService 내부 private 헬퍼. 단순함 우선, 공유 추출 없음):

```csharp
//260517 hbk — 단일 strip 에서 MeasurePos 실행 후 edge 점 누적.
//  CANONICAL: DatumFindingService.AppendEdgePointsFromStrip. DatumFindingService 의 헬퍼는 private 라 직접 호출 불가 → 동등 구현을 VisionAlgorithmService 내부에 작성.
//  polarity 차이: 이 헬퍼는 Halcon polarity 문자열("positive"/"negative")을 그대로 받는다 (caller 에서 매핑 완료).
//  measurePhi 차이: caller 가 direction 매핑 + rPhi 회전 보정을 합산한 값을 전달 → 헬퍼는 SmallestRectangle2 자동 phi(rp) 를 쓰지 않고 전달받은 measurePhi 만 사용.
//  strip 실패(빈 결과 / 예외)는 swallow — 한 strip 실패가 전체 ROI 를 중단시키지 않음.
private void AppendStrip(
    HImage image,
    double row1, double col1, double row2, double col2,
    HTuple imageWidth, HTuple imageHeight,
    double sigma, int threshold, string polarity,
    double measurePhi, string selection,
    ref HTuple allRows, ref HTuple allCols)
{
    HObject stripRegion = null;
    HTuple measureHandle = null;
    try
    {
        HOperatorSet.GenRectangle1(out stripRegion, row1, col1, row2, col2);
        HTuple rr, rc, rp, rh, rw;
        HOperatorSet.SmallestRectangle2(stripRegion, out rr, out rc, out rp, out rh, out rw);
        HOperatorSet.GenMeasureRectangle2(
            rr, rc, measurePhi, rh, rw,
            imageWidth, imageHeight, "nearest_neighbor",
            out measureHandle);
        HTuple edgeRows, edgeCols, amp, dist;
        HOperatorSet.MeasurePos(
            image, measureHandle, sigma, threshold,
            polarity, selection,
            out edgeRows, out edgeCols, out amp, out dist);
        if (edgeRows.TupleLength() <= 0 || edgeCols.TupleLength() <= 0)
        {
            return;
        }
        HOperatorSet.TupleConcat(allRows, edgeRows, out allRows);
        HOperatorSet.TupleConcat(allCols, edgeCols, out allCols);
    }
    catch
    {
    }
    finally
    {
        if (measureHandle != null) { try { HOperatorSet.CloseMeasure(measureHandle); } catch { } }
        if (stripRegion != null) { try { stripRegion.Dispose(); } catch { } }
    }
}
```

**제약:**
- .NET Framework 4.8 / C# 7.2 — switch expression, nullable reference type, expression-bodied member 신규 도입 금지.
- 수정/추가하는 모든 라인에 `//260517 hbk` 마커. 기존 hbk 마커(`//260413 hbk`, `//260509 hbk Phase 20`, `//260512 hbk Phase 23 ALG-01`)는 절대 제거하지 말 것 — Phase 20 D-12 stacking 패턴(기존 마커 보존 + 위/옆에 누적).
- VisionAlgorithmService.cs 기존 brace 스타일(Allman — 여는 중괄호 새 줄) 준수.
- `using HalconDotNet;` 외 신규 using 추가 불필요 (모든 타입이 HalconDotNet / System 범위).
  </action>

  <verify>
    <automated>cd C:\Info\Project\DataMeasurement\WPF_Example && msbuild DatumMeasurement.csproj /p:Configuration=Debug /p:Platform=x64 /t:Rebuild /v:minimal /nologo</automated>
  </verify>

  <done>
- msbuild Debug/x64 Rebuild 가 0 errors 로 통과한다.
- 신규 warning 0 (Phase 23 baseline warning 개수와 동일 — STATE 기준 6 warnings).
- `TryFitLine` 시그니처가 변경되지 않았다 (5개 caller 무수정 컴파일 통과로 증명).
- `TryFitLine` 본문이 strip-loop 구조를 가진다: `stripCount` 회 루프 + `AppendStrip` 호출 + `GenContourPolygonXld(allRows, allCols)`.
- datumTransform 변환 블록과 `measurePhi += (rPhi - roiPhi)` 회전 보정이 보존되었다.
- `private void AppendStrip(...)` 헬퍼가 VisionAlgorithmService 클래스 내부에 존재한다.
  </done>
</task>

<task type="checkpoint:human-verify" gate="blocking">
  <name>Task 2: SIMUL_MODE 측정값 표시 육안 검증</name>
  <what-built>
`VisionAlgorithmService.TryFitLine` 을 단일 MeasurePos → strip-loop MeasurePos 누적으로 재작성했다.
ROI 한 변을 stripCount(기본 20)개 strip 으로 쪼개 strip 마다 MeasurePos 를 돌려 에지점을 누적 →
FitLineContourXld 가 충분한 점으로 직선을 피팅한다. TryFitLine 을 쓰는 5개 측정 알고리즘
(EdgeToLineDistance / PointToLineDistance / PointToPointDistance / LineToLineAngle / LineToLineDistance)이
모두 동일하게 개선된다 (시그니처 무변경).
  </what-built>
  <how-to-verify>
1. Debug/x64 SIMUL_MODE 로 DatumMeasurement.exe 실행.
2. EdgeToLineDistance(또는 PointToLine/LineToLine 계열) 측정 항목이 포함된 레시피를 로드한다.
3. Datum 티칭/검증이 정상인 SHOT 의 검사를 1회 실행한다.
4. 검사 결과 그리드에서 해당 측정 항목의 측정값 컬럼을 확인한다:
   - 기대: 측정값이 '—' 가 아니라 mm 숫자 값으로 표시된다.
   - 기대: Trace 로그에 "insufficient edge points (1)" 류 오류가 더 이상 나타나지 않는다.
5. (회귀 확인) Datum 보정이 적용된 측정값이 datum-relative 좌표 기준으로 합리적인 범위에 있는지 확인한다 (이전 정상 측정 대비 급격한 부호/스케일 변화 없음).
  </how-to-verify>
  <resume-signal>측정값이 mm 로 표시되면 "approved", 여전히 '—' 이거나 값이 비정상이면 증상(측정 항목명 + Trace 로그 발췌)을 알려주세요.</resume-signal>
</task>

</tasks>

<verification>
- msbuild Debug/x64 Rebuild: 0 errors, 신규 warning 0.
- `TryFitLine` 시그니처 무변경 → 5개 caller (EdgeToLineDistance / PointToLineDistance / PointToPointDistance ×1 / LineToLineAngle ×2 / LineToLineDistance ×2) 무수정 컴파일 통과.
- strip-loop 구조 존재: `AppendStrip` 헬퍼 + stripCount 루프 + 누적 `allRows/allCols` → FitLineContourXld.
- datumTransform 변환 + rPhi 회전 보정 보존.
- SIMUL_MODE 측정값 '—' → mm 값 전환 (Task 2 육안 검증).
</verification>

<success_criteria>
- `TryFitLine` 이 ROI 를 strip 으로 분할해 strip 마다 MeasurePos 를 돌려 에지점을 누적한다.
- 단일 ROI 한 변에서 2개 이상의 에지점이 모여 FitLineContourXld 가 직선을 피팅한다.
- "insufficient edge points (1)" 오류가 더 이상 발생하지 않는다.
- EdgeToLineDistance 등 5개 측정 알고리즘의 측정값이 '—' 가 아니라 mm 로 표시된다.
- datumTransform 변환 + rPhi 회전 보정 회귀 0.
- msbuild Debug/x64 PASS, 신규 warning 0, `//260517 hbk` 마커 부여, 기존 hbk 마커 보존.
</success_criteria>

<scope_notes>
**범위에 포함하지 않는 것 (의도적):**
- **D-08 "All" 하드코딩 (EdgeToLineDistanceMeasurement.cs L85-91):** 이번 범위에서 손대지 않는다. 근거: strip-loop 도입 후에도 `selection="All"` 은 안전한 동작이다 — strip 당 MeasurePos 가 selection 을 그대로 받으며, "All" 이면 strip 마다 검출된 모든 에지점을 누적하므로 라인 피팅 입력이 가장 풍부하다. D-08 은 Phase 23.1-01 에서 "First/Last 가 단일점만 반환 → 라인 피팅 2점 미충족" 을 막으려는 구조적 가드였고, strip-loop 에서도 strip 당 1점씩만 모이는 First/Last 보다 All 이 여전히 더 안전하다. D-08 가드를 제거하면 레거시 INI 의 `EdgeSelection=First` 가 살아나 strip 당 1점 → trimCount 적용 후 점 부족 위험이 생긴다. 따라서 D-08 하드코딩은 **유지가 정답** 이며 이번 quick 범위 밖. (시그니처 무변경이라 caller 무수정 원칙과도 일치.)
- **5개 caller 파일 (PointToLine/PointToPoint/LineToLineAngle/LineToLineDistance/EdgeToLineDistance):** 시그니처 무변경이라 무수정. 전부 동일 strip-loop 개선을 자동으로 받는 것이 의도된 설계.
- **DatumFindingService.cs:** 무수정. CANONICAL 레퍼런스로만 참조. `AppendEdgePointsFromStrip` 을 공유 헬퍼로 추출하지 않고 VisionAlgorithmService 내부에 동등 헬퍼(`AppendStrip`)를 별도 작성한다 — 헬퍼 위치 결정 옵션 (a) 채택. 근거: (1) 단순함 우선 — 공유 추출은 polarity 규약 차이(Halcon 문자열 vs LightToDark 매핑)를 인자로 흡수하는 추가 설계가 필요해 quick 범위에 과하다. (2) DatumFindingService 를 건드리면 Datum 티칭 경로 회귀 위험이 생긴다. (3) 두 헬퍼는 향후 독립 진화 가능.
</scope_notes>

<output>
완료 후 `.planning/quick/260517-ijg-rewrite-tryfitline-to-strip-loop-measure/260517-ijg-SUMMARY.md` 를 생성한다.
</output>
