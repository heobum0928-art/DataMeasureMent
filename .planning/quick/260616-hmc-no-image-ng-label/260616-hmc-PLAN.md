---
phase: quick-260616-hmc
plan: 01
type: execute
wave: 1
depends_on: []
files_modified:
  - WPF_Example/UI/ViewModel/ReviewMeasurementRow.cs
  - WPF_Example/Custom/Export/ExcelExportService.cs
  - WPF_Example/UI/Reviewer/ReviewerWindow.xaml.cs
  - WPF_Example/Custom/Sequence/Inspection/RepeatMeasurementStats.cs
autonomous: true
requirements: [QUICK-NO-IMAGE-LABEL]
must_haves:
  truths:
    - "무효 SimulImagePath SHOT의 NO_IMAGE measurement가 결과 그리드에서 'NO IMAGE'로 표시된다 (— dash 아님)"
    - "Excel export 8번 컬럼에 NO_IMAGE measurement가 'NO IMAGE'로 기록된다"
    - "리뷰어 '불량만 보기' 필터/첫 불량 자동 포커스가 NO IMAGE 행을 불량으로 포함한다"
    - "반복도 통계에서 NO_IMAGE measurement는 값 목록에서 제외되고 DetectFail로 카운트된다"
    - "Debug|x64 (SIMUL_MODE) 빌드가 성공한다"
  artifacts:
    - path: "WPF_Example/UI/ViewModel/ReviewMeasurementRow.cs"
      provides: "JudgeText NO_IMAGE 분기"
      contains: "NO IMAGE"
    - path: "WPF_Example/Custom/Export/ExcelExportService.cs"
      provides: "Excel 8컬럼 NO_IMAGE 분기"
      contains: "NO IMAGE"
    - path: "WPF_Example/UI/Reviewer/ReviewerWindow.xaml.cs"
      provides: "불량 필터/포커스 NO IMAGE 포함"
      contains: "NO IMAGE"
    - path: "WPF_Example/Custom/Sequence/Inspection/RepeatMeasurementStats.cs"
      provides: "반복도 통계 NO_IMAGE = DetectFail 취급"
      contains: "NO_IMAGE"
  key_links:
    - from: "Action_FAIMeasurement.cs (LastSkipReason=\"NO_IMAGE\")"
      to: "ReviewMeasurementRow.JudgeText"
      via: "LastSkipReason 문자열 매칭"
      pattern: "NO_IMAGE.*NO IMAGE"
---

<objective>
무효 SimulImagePath SHOT의 NO_IMAGE measurement를 UI/Export 전반에 명확한 NG 라벨("NO IMAGE")로 일관 표기한다.

Purpose: 선행 작업(de7773f)에서 NO_IMAGE NG 판정 로직(allPass=false + LastSkipReason="NO_IMAGE" + LastJudgement=false)은 이미 동작하지만, JudgeText/판정 라벨 렌더 4곳이 "DATUM_FAIL"만 인식하고 "NO_IMAGE"는 누락하여 결과 그리드/Excel에서 명확한 NG가 아니라 "—"(미측정 dash)로 보이는 갭을 해소한다.

Output: 4개 파일에 DATUM_FAIL 분기 옆 NO_IMAGE 분기 추가 (DATUM_FAIL 직후, HasResult 분기 앞 우선순위 유지).
</objective>

<execution_context>
@$HOME/.claude/get-shit-done/workflows/execute-plan.md
@$HOME/.claude/get-shit-done/templates/summary.md
</execution_context>

<context>
@./CLAUDE.md

<background>
선행 작업(commit de7773f): 무효 SimulImagePath SHOT을 image=null로 두고 Action_FAIMeasurement.cs EStep.Measure else 분기에서 allPass=false + 모든 measurement에 LastSkipReason="NO_IMAGE" + LastJudgement=false로 NG 처리하는 로직이 이미 들어감. NG 판정 자체는 동작함.

남은 갭: JudgeText/판정 라벨 렌더 4곳이 "DATUM_FAIL"만 인식 → "NO_IMAGE"는 "—" dash로 표시. 사용자 결정 = "명확한 NG + 이미지 없음 라벨". JudgeText 토큰은 기존 영문(OK/NG/DETECT FAIL) 일관성 위해 "NO IMAGE" 사용 (밑줄 없는 공백 표기).

확인됨 (코드 변경 불필요): CycleResultSerializer.cs 126행이 `LastSkipReason = meas.LastSkipReason`로 verbatim 복사 → "NO_IMAGE" DTO 전달은 이미 동작.
</background>

<interfaces>
<!-- 매칭 키: measurement.LastSkipReason (string). Action_FAIMeasurement가 NO_IMAGE 시 "NO_IMAGE" 설정. -->
<!-- JudgeText 토큰 규칙: "OK" / "NG" / "DETECT FAIL"(=DATUM_FAIL) / "NO IMAGE"(=NO_IMAGE) / "—"(미측정) -->
<!-- 주의: LastSkipReason은 밑줄 "NO_IMAGE", 표시 토큰은 공백 "NO IMAGE" -->
</interfaces>
</context>

<tasks>

<task type="auto">
  <name>Task 1: UI/Export 라벨 렌더 3곳 NO_IMAGE 분기 추가</name>
  <files>WPF_Example/UI/ViewModel/ReviewMeasurementRow.cs, WPF_Example/Custom/Export/ExcelExportService.cs, WPF_Example/UI/Reviewer/ReviewerWindow.xaml.cs</files>
  <action>
3개 파일에 NO_IMAGE 라벨 분기를 추가한다. 모든 추가/수정 라인에 `//260616 hbk NO_IMAGE 라벨` 주석을 단다.

1. **ReviewMeasurementRow.cs** (83~94행 3분기, Allman 스타일):
   `if (m.LastSkipReason == "DATUM_FAIL") { JudgeText = "DETECT FAIL"; }` 블록 직후,
   `else if (m.LastHasResult)` 앞에 다음 분기를 삽입:
   ```csharp
   else if (m.LastSkipReason == "NO_IMAGE") //260616 hbk NO_IMAGE 라벨
   {
       JudgeText = "NO IMAGE";
   }
   ```

2. **ExcelExportService.cs** (80~91행 8번 컬럼 3분기, Allman 스타일):
   `if (m.LastSkipReason == "DATUM_FAIL") { ws.Cell(row, 8).Value = "DETECT FAIL"; }` 블록 직후,
   `else if (m.LastHasResult)` 앞에 다음 분기를 삽입:
   ```csharp
   else if (m.LastSkipReason == "NO_IMAGE") //260616 hbk NO_IMAGE 라벨
   {
       ws.Cell(row, 8).Value = "NO IMAGE";
   }
   ```

3. **ReviewerWindow.xaml.cs** (185행 '불량만 보기' 필터, 191행 첫 불량 자동 포커스):
   두 곳의 `r.JudgeText == "NG" || r.JudgeText == "DETECT FAIL"` 조건 끝에 `|| r.JudgeText == "NO IMAGE"`를 추가:
   - 185행: `visible = _allRows.Where(r => r.JudgeText == "NG" || r.JudgeText == "DETECT FAIL" || r.JudgeText == "NO IMAGE").ToList(); //260616 hbk NO_IMAGE 불량 포함`
   - 191행: `var firstFail = visible.FirstOrDefault(r => r.JudgeText == "NG" || r.JudgeText == "DETECT FAIL" || r.JudgeText == "NO IMAGE"); //260616 hbk NO_IMAGE 불량 포함`

   주의: ReviewerWindow는 비교 토큰이 JudgeText("NO IMAGE" 공백)이고, 다른 두 파일은 매칭 키가 LastSkipReason("NO_IMAGE" 밑줄)임. 혼동 금지.

C# 7.2 제약: switch expression/range 금지, if/else-if 체인 유지. 각 파일 기존 brace 스타일 유지.
  </action>
  <verify>
    <automated>grep -c "NO IMAGE" "WPF_Example/UI/ViewModel/ReviewMeasurementRow.cs" "WPF_Example/Custom/Export/ExcelExportService.cs" && grep -c "NO IMAGE" "WPF_Example/UI/Reviewer/ReviewerWindow.xaml.cs"</automated>
  </verify>
  <done>ReviewMeasurementRow/ExcelExportService에 NO_IMAGE→"NO IMAGE" 분기가 DATUM_FAIL 직후 추가됨. ReviewerWindow 필터/포커스 2곳에 `|| r.JudgeText == "NO IMAGE"` 추가됨. 모든 라인에 //260616 hbk 주석.</done>
</task>

<task type="auto">
  <name>Task 2: 반복도 통계 NO_IMAGE = DetectFail 취급 + 빌드 검증</name>
  <files>WPF_Example/Custom/Sequence/Inspection/RepeatMeasurementStats.cs</files>
  <action>
**RepeatMeasurementStats.cs** (108행 부근, 실제 메서드 코드 확인 완료):
현재 코드는
```csharp
if (m.LastSkipReason == "DATUM_FAIL")
{
    d.DetectFailCount++;
}
else if (m.LastHasResult) { ... }
```
DATUM_FAIL 조건을 NO_IMAGE도 포함하도록 확장하여, NO_IMAGE measurement가 `d.Values`(값 목록)에서 제외되고 `d.DetectFailCount`만 증가하도록 한다:
```csharp
if (m.LastSkipReason == "DATUM_FAIL" || m.LastSkipReason == "NO_IMAGE") //260616 hbk NO_IMAGE DetectFail 취급
{
    d.DetectFailCount++;
}
```
(else if 이하 값 누적/Ok/Ng 카운트 분기는 그대로 유지 — NO_IMAGE는 LastHasResult=false이므로 어차피 값에 들어가지 않지만, DetectFailCount로 명시 집계되도록 조건에 포함시키는 것이 목적.)

추가 라인/수정 라인에 //260616 hbk 주석. C# 7.2 if/else-if 유지.

이후 전체 솔루션을 빌드한다 (SIMUL_MODE 활성 Debug|x64):
MSBuild으로 `WPF_Example/DatumMeasurement.csproj`를 Debug|x64 구성으로 빌드. 빌드 성공(Build succeeded, 0 errors) 확인.
  </action>
  <verify>
    <automated>"/c/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/MSBuild.exe" "WPF_Example/DatumMeasurement.csproj" -p:Configuration=Debug -p:Platform=x64 -t:Build -v:m 2>&1 | grep -iE "Build succeeded|error"</automated>
  </verify>
  <done>RepeatMeasurementStats.cs 조건이 `DATUM_FAIL || NO_IMAGE`로 확장됨. NO_IMAGE는 값 제외 + DetectFailCount 증가. Debug|x64 빌드 0 errors.</done>
</task>

</tasks>

<verification>
정적 일관성 확인:
1. 4곳 분기 모두 DATUM_FAIL 직후 + HasResult 분기 앞에 배치되어 우선순위 유지.
2. LastSkipReason 매칭 키는 밑줄 "NO_IMAGE", 표시/JudgeText 토큰은 공백 "NO IMAGE".
3. ReviewerWindow 비교는 JudgeText("NO IMAGE"), 나머지는 LastSkipReason("NO_IMAGE").
4. 모든 추가/수정 라인에 //260616 hbk 주석.
5. CycleResultSerializer.cs는 변경 없음 (LastSkipReason verbatim 복사로 이미 동작 — 확인만).
6. MSBuild Debug|x64 (SIMUL_MODE) 빌드 성공.
</verification>

<success_criteria>
- ReviewMeasurementRow / ExcelExportService에 NO_IMAGE → "NO IMAGE" 분기 추가
- ReviewerWindow 필터/포커스 2곳에 NO IMAGE 불량 포함
- RepeatMeasurementStats가 NO_IMAGE를 DetectFail로 집계 (값 제외)
- Debug|x64 빌드 0 errors
- 모든 변경 라인에 //260616 hbk 주석
</success_criteria>

<output>
After completion, create `.planning/quick/260616-hmc-no-image-ng-label/260616-hmc-SUMMARY.md`
</output>
