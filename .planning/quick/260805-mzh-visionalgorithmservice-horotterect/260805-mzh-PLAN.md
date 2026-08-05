---
phase: quick-260805-mzh
plan: 01
type: execute
wave: 1
depends_on: []
files_modified:
  - WPF_Example/Halcon/Algorithms/VisionAlgorithmService.cs
autonomous: true
requirements: [MZH-01]

must_haves:
  truths:
    - "polar sweep 루프가 매 step 마다 사용하지 않는 HRegion 을 생성하지 않는다"
    - "원(Circle) 측정/Datum 검출을 반복 실행해도 해당 지점에서 HALCON region 이 누적되지 않는다"
    - "TryFindCircleByPolarSampling 의 측정 결과(원 중심/반경/strip 성공 여부)가 수정 전과 동일하다"
    - "Debug/x64 빌드가 신규 에러 0건으로 성공한다"
  artifacts:
    - path: "WPF_Example/Halcon/Algorithms/VisionAlgorithmService.cs"
      provides: "horotteRect 데드 할당이 제거된 polar sweep 루프"
      contains: "HOperatorSet.GenMeasureRectangle2"
  key_links:
    - from: "VisionAlgorithmService.TryFindCircleByPolarSampling"
      to: "HOperatorSet.GenMeasureRectangle2"
      via: "rectRow/rectCol/rectPhi/halfL1/halfL2 원시 값 직접 전달 (HObject 경유 없음)"
      pattern: "GenMeasureRectangle2\\(\\s*rectRow, rectCol, rectPhi, halfL1, halfL2"
---

<objective>
`VisionAlgorithmService.TryFindCircleByPolarSampling` 의 polar sweep 루프(:492-545) 안에서 생성 직후 한 번도 참조되지 않고 Dispose 도 되지 않는 `horotteRect` HObject 할당 2줄(:501-502)을 삭제한다.

Purpose: 원(Circle) 관련 측정/Datum 검출이 포함된 레시피에서 사이클당 (Circle 개수 x 최대 36) 개의 HALCON region 이 확정적으로 누수되고 있다. STATE.md 에 기록된 "HALCON region 누수" carry-only 항목 중 하나를 종결한다.
Output: 데드 할당이 제거된 `VisionAlgorithmService.cs` (동작 변경 0).
</objective>

<execution_context>
@$HOME/.claude/get-shit-done/workflows/execute-plan.md
@$HOME/.claude/get-shit-done/templates/summary.md
</execution_context>

<context>
@.planning/quick/260805-mzh-visionalgorithmservice-horotterect/260805-mzh-CONTEXT.md
@CLAUDE.md

<interfaces>
<!-- 현재 코드 (VisionAlgorithmService.cs:497-511). 실행자는 코드베이스 추가 탐색 불필요. -->

```csharp
                    double rectRow = cRow - radius * Math.Sin(thetaRad);
                    double rectCol = cCol + radius * Math.Cos(thetaRad);
                    double rectPhi = thetaRad; // 반경 방향 = rect length1 축

                    HObject horotteRect;                                                              // <-- 삭제 대상 (:501)
                    HOperatorSet.GenRectangle2(out horotteRect, rectRow, rectCol, rectPhi, halfL1, halfL2);  // <-- 삭제 대상 (:502)

                    HTuple measureHandle = null;
                    try
                    {
                        HOperatorSet.GenMeasureRectangle2(
                            rectRow, rectCol, rectPhi, halfL1, halfL2,
                            imageWidth, imageHeight, "nearest_neighbor",
                            out measureHandle);
```

핵심: `GenMeasureRectangle2` 는 `rectRow/rectCol/rectPhi/halfL1/halfL2` **원시 double 값**을 직접 받는다. `horotteRect` 객체를 전혀 참조하지 않는다.
</interfaces>

<repo_wide_grep_evidence>
`grep -rn "horotteRect" C:\Info\Project\DataMeasurement` 결과 (planning 문서 제외):
- `WPF_Example\Halcon\Algorithms\VisionAlgorithmService.cs:501`
- `WPF_Example\Halcon\Algorithms\VisionAlgorithmService.cs:502`

→ 리포지토리 전체에서 이 2줄이 유일한 출현. 다른 파일/다른 메서드에서 재사용되는 이름이 아님. 완전한 데드 코드로 확정.
</repo_wide_grep_evidence>
</context>

<tasks>

<task type="auto">
  <name>Task 1: horotteRect 데드 할당 2줄 삭제</name>
  <files>WPF_Example/Halcon/Algorithms/VisionAlgorithmService.cs</files>
  <action>
사전 재확인 (삭제 전 필수):
1. `grep -n "horotteRect" WPF_Example/Halcon/Algorithms/VisionAlgorithmService.cs` 실행 → 정확히 2건(:501, :502)만 나오는지 확인.
2. 리포지토리 전체 `grep -rn "horotteRect" WPF_Example/` 실행 → 위 2건 외 0건인지 확인.
3. 만약 3건 이상 나오거나 다른 파일에서 참조되면 **삭제하지 말고 즉시 중단**하고 사용자에게 보고한다.

삭제 대상 (D-01: Dispose 추가가 아니라 코드 자체 삭제 — 애초에 불필요한 객체이므로 try/finally 로 감싸지 않고 아예 만들지 않는다):
```csharp
                    HObject horotteRect;
                    HOperatorSet.GenRectangle2(out horotteRect, rectRow, rectCol, rectPhi, halfL1, halfL2);
```
위 2줄과, 그 뒤에 남는 빈 줄 1개를 제거하여 `double rectPhi = thetaRad;` 다음에 빈 줄 1개를 두고 바로 `HTuple measureHandle = null;` 이 오도록 한다.

변경 금지 (D-02):
- `strips` 배열 채우기(`strips[i] = true;`), `allRows`/`allCols` TupleConcat 누적 로직
- `try/catch/finally` 구조 및 `measureHandle` CloseMeasure 패턴
- `GenMeasureRectangle2` 호출 인자, `MeasurePos` 호출, selectionLower 분기
- 루프 밖의 halfL1/halfL2 cap 계산, FitCircleContourXld 이후 로직
정확히 이 2줄(+빈 줄 정리)만 제거한다.

코딩 규약: C# 7.2 한정. 신규 코드 추가 없음(순수 삭제)이므로 추가 규약 적용 대상 없음. 주석 추가하지 않는다(비자명한 "왜"가 아님 — 그냥 사라진 코드).
  </action>
  <verify>
    <automated>cd C:/Info/Project/DataMeasurement && grep -c "horotteRect" WPF_Example/Halcon/Algorithms/VisionAlgorithmService.cs; echo "expect: 0 (grep exit 1)"</automated>
    <automated>cd C:/Info/Project/DataMeasurement && grep -n "GenMeasureRectangle2" WPF_Example/Halcon/Algorithms/VisionAlgorithmService.cs</automated>
    <automated>cd C:/Info/Project/DataMeasurement && git diff --stat WPF_Example/Halcon/Algorithms/VisionAlgorithmService.cs</automated>
    <automated>cd C:/Info/Project/DataMeasurement && "/c/Program Files (x86)/Microsoft Visual Studio/2019/Community/MSBuild/Current/Bin/MSBuild.exe" WPF_Example/DatumMeasurement.csproj //p:Configuration=Debug //p:Platform=x64 //v:minimal 2>&1 | tail -15</automated>
  </verify>
  <done>
- `horotteRect` 가 `VisionAlgorithmService.cs` 및 `WPF_Example/` 전체에서 0건.
- `GenMeasureRectangle2` 호출이 그대로 존재하고 인자(rectRow, rectCol, rectPhi, halfL1, halfL2, imageWidth, imageHeight, "nearest_neighbor") 무변경.
- `git diff --stat` 이 1 file changed, 0 insertions, 3 deletions (2줄 + 빈 줄) 수준 — 삽입 0줄.
- Debug/x64 MSBuild 빌드 성공, 신규 에러/경고 0건.
  </done>
</task>

</tasks>

<verification>
1. `git diff WPF_Example/Halcon/Algorithms/VisionAlgorithmService.cs` 전체 확인 — 삭제 라인만 존재하고 추가 라인 0개.
2. polar sweep 루프의 `strips`/`allRows`/`allCols`/`measureHandle` finally 블록이 diff 에 나타나지 않음.
3. MSBuild Debug/x64 PASS.
</verification>

<success_criteria>
- 리포지토리 전체에서 `horotteRect` 심볼 0건.
- `TryFindCircleByPolarSampling` 의 측정 경로(GenMeasureRectangle2 → MeasurePos → FitCircleContourXld)가 코드상 완전히 동일.
- 빌드 PASS, 삽입 라인 0.
</success_criteria>

<output>
완료 후 `.planning/quick/260805-mzh-visionalgorithmservice-horotterect/260805-mzh-SUMMARY.md` 생성.
</output>
