---
phase: quick-260908-jzs
plan: 01
type: execute
wave: 1
depends_on: []
files_modified:
  - WPF_Example/Custom/Sequence/Inspection/SkipReason.cs
  - WPF_Example/Custom/Sequence/Inspection/MeasurementBase.cs
  - WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs
  - WPF_Example/Custom/Sequence/Inspection/CycleResultSerializer.cs
  - WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs
  - WPF_Example/UI/ViewModel/CycleResultDto.cs
  - WPF_Example/UI/ViewModel/ReviewMeasurementRow.cs
  - WPF_Example/Custom/Export/ExcelExportService.cs
  - WPF_Example/UI/Reviewer/ReviewerWindow.xaml
  - WPF_Example/UI/Reviewer/ReviewerWindow.xaml.cs
autonomous: false
requirements: [A, B, C, D, E]

must_haves:
  truths:
    - "측정 실패(에지 0개 등) 항목이 리뷰어 표/엑셀에서 '측정실패'로 보이고 '—'(미측정)로 보이지 않는다"
    - "리뷰어 좌측 목록 한 줄만 보고 어느 Shot·어느 z·무슨 항목이 왜 NG 인지 알 수 있다"
    - "SHOT_I5 처럼 tick 자체는 OK 인데 사이클 종합이 NG 인 경우 목록에 'OK ... · 종합 NG' 로 구분 표시된다"
    - "새 필드가 없는 옛 cycle.json 을 열어도 크래시 없이 기존 형식으로 표시된다"
    - "'불량만 보기' 체크 시 좌측 cycle 목록도 불량 tick 만 남는다"
  artifacts:
    - path: "WPF_Example/UI/ViewModel/CycleResultDto.cs"
      provides: "TickJudgement/MeasuredShotNames/ZIndex/LastErrorMessage 필드 + ReviewerListLabelBuilder"
      contains: "class ReviewerListLabelBuilder"
    - path: "WPF_Example/Custom/Sequence/Inspection/SkipReason.cs"
      provides: "MEASURE_FAIL 상수"
      contains: "MEASURE_FAIL"
  key_links:
    - from: "Action_FAIMeasurement.RecordMeasurementResult"
      to: "MeasurementBase.LastSkipReason / LastErrorMessage"
      via: "실패 분기 대입"
      pattern: "SkipReason\\.MEASURE_FAIL"
    - from: "CycleResultSerializer.BuildDto"
      to: "CycleResultDto.TickJudgement"
      via: "빌드 후 정적 헬퍼 계산"
      pattern: "TickJudgement"
    - from: "ReviewerWindow.LoadCycleFolders"
      to: "ReviewerListLabelBuilder.Build"
      via: "목록 항목 DisplayText 생성"
      pattern: "ReviewerListLabelBuilder"
---

<objective>
결과 리뷰어(ReviewerWindow) 좌측 cycle 목록이 "HH:mm:ss  OK/NG" 만 보여줘서 어느 자리(Shot)·어느 항목이 왜 NG 인지 알 수 없는 문제를 해결하고,
측정 실패(TryExecute false, 예: "insufficient edge points (0)")가 리뷰어/엑셀에서 "—"(미측정)로 보이던 기록 버그를 고친다.

Purpose: 2026-09-08 14:14 BOTTOM 자동 사이클에서 (a) 14:14:36 SHOT_F2_P2 측정 실패 tick 이 목록에 OK 로,
(b) 14:14:45 SHOT_I5(자체 OK) tick 이 NG 로 보여 현장 판독이 불가능했다. tick 자체 판정과 사이클 종합 판정을 분리해 둘 다 보여준다.
Output: SkipReason.MEASURE_FAIL 기록 경로, CycleResultDto 의 tick 메타 필드(TickJudgement/MeasuredShotNames/ZIndex), 리뷰어 목록 라벨/색/필터.

무변경(회귀 금지): TCP 응답 규약, 판정 로직(P/F/B), 자동 사이클 흐름, cycle.json 의 기존 필드 이름/의미, OverallJudgement 계산.
</objective>

<execution_context>
@$HOME/.claude/get-shit-done/workflows/execute-plan.md
</execution_context>

<context>
@CLAUDE.md

실기 근거 데이터: D:\Data\Result\20260908\1414xx_cycle\cycle.json (BOTTOM 자동 사이클)

<interfaces>
확인된 현재 코드 사실 (추측 아님, 실제 읽음):

- `SkipReason` (Custom/Sequence/Inspection/SkipReason.cs): DATUM_FAIL / ALIGN_FAIL / NO_IMAGE / DATUM_REF_MISSING / ZINDEX_MISCONFIGURED / CROSS_Z_INCOMPLETE 상수 6개. 값은 와이어/CSV/로그에 그대로 나가므로 기존 값 변경 금지.
- `MeasurementBase` (같은 폴더): `LastHasResult`(105), `LastSkipReason`(110), `ClearResult()`(169: LastMeasuredValue=0 / LastJudgement=false / LastHasResult=false / LastSkipReason=null), `_copyExclude` HashSet(205~).
- `ParamBase.Save`(WPF_Example/Sequence/Param/ParamBase.cs:318~)는 `GetProperties(Instance|Public)` 로 **프로퍼티만** 순회해 INI 에 쓴다. → **public 필드는 INI 직렬화·CopyPublicPropertiesTo 대상이 아니다.**
- `Action_FAIMeasurement.RecordMeasurementResult`(767~): 실패 분기가 `Logging.PrintLog(... failed: measErrorStr)` 후 `meas.ClearResult(); meas.LastJudgement = false;` 만 한다. (이 파일은 K&R 스타일 — 여는 중괄호 같은 줄)
- `CycleResultSerializer.BuildDto`(35~): 시그니처 끝 optional 파라미터 `nIndexNumber = -1`, `bIsProtocolDriven = false`. measDto 복사부 120~131.
- BuildDto 호출부 5곳: InspectionSequence.cs:248(AddResponse, 프로토콜), :276(HandleManualCyclePersist, 수동), :2336(PersistAndEnqueueV1, 프로토콜 v1.0), BatchRunService.cs:168(수동), RepeatRunService.cs:244(수동).
- `InspectionSequence.GetExecutionZIndex()`(1551) = ParseCurrentZIndex(). `IsProtocolDrivenCycle()`(1565) = RequestPacket != null. 수동이면 ParseCurrentZIndex 가 0 을 반환하므로 **0 과 "z 없음"을 구별하려면 IsProtocolDrivenCycle() 게이트가 필요**하다.
- `MeasurementResultDto`(UI/ViewModel/CycleResultDto.cs:76~): MeasurementName/TypeName/Nominal/Tol±/LastMeasuredValue/LastJudgement/LastHasResult/LastSkipReason/IsDualImage/Horizontal·VerticalImagePath.
- `ReviewMeasurementRow`(UI/ViewModel/ReviewMeasurementRow.cs) 생성자 끝 JudgeText 분기: DATUM_FAIL→"DETECT FAIL", NO_IMAGE→"NO IMAGE", CROSS_Z_INCOMPLETE→"CROSS-Z INCOMPLETE", LastHasResult→OK/NG, else "—".
- `ExcelExportService.BuildJudgementText(MeasurementResultDto)`(Custom/Export/ExcelExportService.cs:~319): 위와 동일 분기(문자열 동일해야 함).
- `ReviewerWindow.xaml.cs`: LoadCycleFolders(53~90, DisplayText 생성 73행), CycleList_SelectionChanged(92), DisplayCycle(109), ApplyRowFilter(175~203, `chk_failOnly.IsChecked` → JudgeText 화이트리스트), ChkFailOnly_Changed(205).
- `ReviewerWindow.xaml`: listBox_cycles(79~87, ItemTemplate 은 `<TextBlock Text="{Binding DisplayText}" TextWrapping="Wrap" Padding="1"/>`), dataGrid_measurements RowStyle DataTrigger(JudgeText = NG/DETECT FAIL/OK 배경색, 168~187), chk_failOnly(119).
- `CycleListItem`(ReviewerWindow.xaml.cs:616): FolderPath / DisplayText / ToString().
</interfaces>

<hard_rules>
CLAUDE.md 하드룰 — 이 계획의 모든 태스크에 예외 없이 적용:
- 삼항 `?:` 금지, `??`/`??=` 금지, `?.`/`?[]` 금지, C# 8 switch 식(`=>`) 금지 → 전통 if/else, switch 문
- 한 줄 분기라도 중괄호 생략 금지 (단, **이미 존재하는 줄은 건드리지 않는다** — 새로 쓰는 줄에만 적용)
- 헝가리언 접두사: b(bool) n(int) sz(string) d(double)
- 매직넘버·매직문자열 금지 → 이름 있는 `const`
- 날짜주석(`//YYMMDD hbk`) 신규 금지. "왜" 만 최소한으로
- C# 7.2 (nullable 참조형식/record/신 패턴매칭 금지)
- 파일별 기존 브레이스 스타일 유지 (Action_FAIMeasurement=K&R, CycleResultDto/ReviewerWindow=Allman)
- 새 .cs 파일 생성 금지(classic csproj). 새 클래스는 기존 파일 안에 둔다
- 새 UI 로직은 DTO/ViewModel 쪽. ReviewerWindow code-behind 에는 호출·바인딩만
</hard_rules>

<verification_greps>
수정한 각 .cs 파일에 대해 아래가 전부 0 이어야 한다(주석 줄 제외 후 카운트):
```bash
for f in <수정파일들>; do
  echo "== $f"
  grep -v '^\s*//' "$f" | grep -cE '\?[^\?]*:'
  grep -v '^\s*//' "$f" | grep -cF '??'
  grep -v '^\s*//' "$f" | grep -cF '?.'
  grep -v '^\s*//' "$f" | grep -cE 'switch.*=>'
  grep -cF 'hbk' <(git diff -U0 -- "$f" | grep '^+')
done
```
주의: 위 카운트는 **이번에 추가/수정한 줄**(`git diff -U0 | grep '^+'`) 기준으로 판단한다 — 기존 줄에 이미 있는 패턴은 손대지 않는다.
</verification_greps>
</context>

<tasks>

<task type="auto">
  <name>Task 1: 측정 실패를 결과에 기록하고 '측정실패'로 표시 (요구 A)</name>
  <files>WPF_Example/Custom/Sequence/Inspection/SkipReason.cs, WPF_Example/Custom/Sequence/Inspection/MeasurementBase.cs, WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs, WPF_Example/UI/ViewModel/CycleResultDto.cs, WPF_Example/Custom/Sequence/Inspection/CycleResultSerializer.cs, WPF_Example/UI/ViewModel/ReviewMeasurementRow.cs, WPF_Example/Custom/Export/ExcelExportService.cs</files>
  <action>
1) `SkipReason.cs`: 기존 상수 뒤에 `public const string MEASURE_FAIL = "MEASURE_FAIL";` 추가. 한 줄 주석으로 의미만("측정 알고리즘 실행 실패 — 에지 부족 등"). 기존 상수 값은 절대 변경하지 않는다.

2) `MeasurementBase.cs`: `LastSkipReason`(110) 바로 아래에 실패 사유 원문 보관용
   `public string LastErrorMessage;` 를 **프로퍼티가 아니라 public 필드로** 선언한다.
   이유(주석으로 남길 것): ParamBase.Save/Load(ParamBase.cs:318, 364)는 `GetProperties` 로 프로퍼티만 순회하므로
   필드로 두면 INI 레시피에 실패 문자열이 새어 들어가지 않는다(에러 문자열에 '='/개행이 섞이면 INI 파손 위험).
   `CopyPublicPropertiesTo` 도 프로퍼티 기반이라 `_copyExclude` 수정이 불필요하다.
   `ClearResult()`(169) 마지막에 `LastErrorMessage = null;` 추가 — 이전 사이클 잔재 방지.

3) `Action_FAIMeasurement.cs` `RecordMeasurementResult`(767~) 실패(else) 분기: 기존 `meas.ClearResult(); meas.LastJudgement = false;` **뒤에**(ClearResult 가 방금 비우므로 순서 필수)
   `meas.LastSkipReason = SkipReason.MEASURE_FAIL;` 과 `meas.LastErrorMessage = <정제된 measErrorStr>;` 를 추가한다.
   정제: 파일 상단 상수 영역에 `private const int MEASURE_ERROR_MAX_LEN = 200;` 를 두고,
   개행(\r,\n)을 공백으로 치환한 뒤 길이가 MEASURE_ERROR_MAX_LEN 초과면 Substring 으로 자른다. 빈 문자열이면 대입하지 않고 null 로 남긴다.
   `measErrorStr = measErrorStr ?? "";` 같은 기존 줄은 건드리지 않는다(신규 줄만 하드룰 적용).
   이 파일은 K&R 브레이스 스타일이므로 새 if 블록도 K&R 로 쓴다.
   ClearResult 는 acc/판정 흐름과 무관 — `acc.FaiAllPass=false`, `acc.MeasuredCount++` 등 기존 집계 로직은 한 줄도 바꾸지 않는다.

4) `CycleResultDto.cs` `MeasurementResultDto`: `LastSkipReason` 아래에 `public string LastErrorMessage { get; set; }` 추가(요약 주석 1줄).

5) `CycleResultSerializer.BuildDto`(120~131) measDto 초기화자에 `LastErrorMessage = meas.LastErrorMessage` 한 줄 추가. 다른 필드 순서/의미 변경 금지.

6) 라벨 표시 — 두 곳의 분기 문자열이 반드시 같아야 한다:
   - `ReviewMeasurementRow` 생성자 JudgeText 분기에 `else if (m.LastSkipReason == SkipReason.MEASURE_FAIL) { JudgeText = JUDGE_MEASURE_FAIL; }` 를 CROSS_Z_INCOMPLETE 분기 뒤·`LastHasResult` 분기 앞에 삽입. 라벨은 클래스 내부 `public const string JUDGE_MEASURE_FAIL = "측정실패";` 로 정의(ReviewerWindow 필터가 재사용).
   - `ExcelExportService.BuildJudgementText` 에 같은 위치·같은 문자열(`ReviewMeasurementRow.JUDGE_MEASURE_FAIL` 참조)로 분기 추가. XML doc 주석의 우선순위 설명 문장도 MEASURE_FAIL 포함하도록 갱신.
  </action>
  <verify>
    <automated>"C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" WPF_Example/DatumMeasurement.csproj -p:Configuration=Release -p:Platform=x64 -t:Build 2>&amp;1 | grep -c "error CS"  # 0 이어야 함 (MSB3027 파일 복사 실패는 무시 — D:\Data 의 exe 실행 중. 프로세스 강제종료 금지)</automated>
    <automated>grep -c "MEASURE_FAIL" WPF_Example/Custom/Sequence/Inspection/SkipReason.cs WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs WPF_Example/UI/ViewModel/ReviewMeasurementRow.cs WPF_Example/Custom/Export/ExcelExportService.cs  # 각 1 이상</automated>
    <automated>context 의 verification_greps 블록을 이 태스크 수정 파일에 실행 — 전부 0</automated>
  </verify>
  <done>측정 실패 항목이 LastSkipReason=MEASURE_FAIL + LastErrorMessage 원문으로 cycle.json 에 기록되고, 리뷰어 표와 엑셀 판정 칸이 "측정실패" 로 표시된다. INI 레시피에는 새 키가 생기지 않는다.</done>
</task>

<task type="auto">
  <name>Task 2: tick 자체 판정 / 측정 Shot / z 번호를 cycle.json 에 추가 (요구 B)</name>
  <files>WPF_Example/UI/ViewModel/CycleResultDto.cs, WPF_Example/Custom/Sequence/Inspection/CycleResultSerializer.cs, WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs</files>
  <action>
1) `CycleResultDto` 에 필드 3개 추가(기존 필드는 손대지 않는다 — 하위 호환):
   - `public string TickJudgement { get; set; }` — 이 tick 에서 **실제 측정된 항목만** 본 판정. "OK"/"NG", 측정된 항목이 없으면 null. 옛 JSON 은 키가 없어 null → 폴백 신호.
   - `public List<string> MeasuredShotNames { get; set; } = new List<string>();` — 이 tick 에서 결과가 있는 Shot 이름.
   - `public int ZIndex { get; set; } = -1;` — 이 tick 의 z 번호. -1 = 없음/수동. Newtonsoft 는 없는 키를 건드리지 않으므로 옛 JSON 은 -1 유지.
   판정 상수는 `CycleResultDto` 내부 `public const string TICK_OK = "OK";`, `public const string TICK_NG = "NG";` 로 둔다(매직문자열 금지).

2) `CycleResultSerializer.BuildDto`: 시그니처 맨 끝에 optional `int nZIndex = -1` 추가(기존 5개 호출부 무수정 컴파일 보장). dto 생성 시 `ZIndex = nZIndex`.
   `dto.Shots` 를 다 채운 **뒤** `return dto;` 직전에 private static 헬퍼 `FillTickSummary(CycleResultDto dto)` 호출.
   `FillTickSummary` 규칙(전통 if/else 만 사용):
   - 각 MeasurementResultDto 에 대해 "이 tick 에서 다뤄진 항목"인지 판정:
     `bool bHasResult = m.LastHasResult;`
     `bool bHasReason = !string.IsNullOrEmpty(m.LastSkipReason) && m.LastSkipReason != SkipReason.CROSS_Z_INCOMPLETE;`
     둘 중 하나라도 true 면 "다뤄진 항목"(CROSS_Z_INCOMPLETE 는 다른 z 대기 상태이므로 제외).
   - NG 조건: `(bHasResult && !m.LastJudgement)` 또는 `bHasReason`.
   - 다뤄진 항목이 하나도 없으면 `TickJudgement = null`(대입하지 않음), 있으면 NG 하나라도 있으면 TICK_NG, 아니면 TICK_OK.
   - `MeasuredShotNames` 에는 다뤄진 항목이 1개 이상인 Shot 의 ShotName 을 중복 없이(이미 Contains 인 것만 건너뜀) 순서대로 담는다. 빈 이름은 넣지 않는다.
   - `OverallJudgement` 및 `MapJudgement` 는 절대 수정하지 않는다.

3) z 전파 — **프로토콜 경로 2곳만**(수동 경로는 기본 -1 유지):
   - `InspectionSequence.cs:248` AddResponse 의 BuildDto 호출: 호출 직전에
     `int nCycleZIndex = -1;` 선언 후 `if (IsProtocolDrivenCycle()) { nCycleZIndex = GetExecutionZIndex(); }` — 수동 RUN 은 ParseCurrentZIndex 가 0 을 돌려줘 "진짜 z=0" 과 구별이 안 되므로 게이트 필수. 인자로 `nCycleZIndex` 전달.
     주의: BuildDto 인자 순서는 (recipeManager, result, when, recipeName, Name, nIndexNumber, IsProtocolDrivenCycle(), nCycleZIndex).
   - `InspectionSequence.cs:2336` PersistAndEnqueueV1 도 동일 패턴으로 추가.
   - HandleManualCyclePersist(:276), BatchRunService.cs:168, RepeatRunService.cs:244 는 **무수정**(기본값 -1 = 수동).
  </action>
  <verify>
    <automated>"C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" WPF_Example/DatumMeasurement.csproj -p:Configuration=Release -p:Platform=x64 -t:Build 2>&amp;1 | grep -c "error CS"  # 0</automated>
    <automated>grep -c "TickJudgement\|MeasuredShotNames\|ZIndex = nZIndex" WPF_Example/UI/ViewModel/CycleResultDto.cs WPF_Example/Custom/Sequence/Inspection/CycleResultSerializer.cs  # 각 1 이상</automated>
    <automated>grep -n "MapJudgement\|OverallJudgement" WPF_Example/Custom/Sequence/Inspection/CycleResultSerializer.cs | wc -l 후 git diff 로 해당 줄 무변경 확인</automated>
    <automated>verification_greps 실행 — 전부 0</automated>
  </verify>
  <done>새 cycle.json 에 TickJudgement/MeasuredShotNames/ZIndex 가 기록되고(자동 사이클은 z 실값, 수동은 -1), 기존 필드와 TCP 응답/판정 흐름은 바이트 단위로 동일하다.</done>
</task>

<task type="auto">
  <name>Task 3: 리뷰어 목록 라벨·색상·'불량만 보기' 확장 (요구 C, D)</name>
  <files>WPF_Example/UI/ViewModel/CycleResultDto.cs, WPF_Example/UI/Reviewer/ReviewerWindow.xaml.cs, WPF_Example/UI/Reviewer/ReviewerWindow.xaml</files>
  <action>
1) `CycleResultDto.cs` 파일 **안에**(새 파일 금지) `public static class ReviewerListLabelBuilder` 추가 — 순수 로직, UI 타입 참조 금지.
   `public static string Build(CycleResultDto dto)`:
   - dto == null 이면 빈 문자열 반환(호출부가 폴더명 폴백을 이미 처리).
   - 기본형: `HH:mm:ss` + 두 칸 + (ZIndex >= 0 일 때만 `z=NN` + 두 칸; `dto.ZIndex.ToString("D2")`) + tick 판정 + 두 칸 + Shot 이름들(", " 조인).
   - tick 판정: `TickJudgement` 가 비어 있지 않으면 그 값, 비어 있으면(옛 JSON) `OverallJudgement` 를 쓴다.
   - Shot 이름: `MeasuredShotNames` 가 있으면 그것, 비어 있으면(옛 JSON) `dto.Shots` 의 ShotName 들로 폴백. 최대 개수 상수 없이 전부 조인하되 빈 이름 제외.
   - NG(위에서 고른 tick 판정이 "NG")면 ` ← ` + 불량 항목 요약. 항목 1건 = `측정명(사유)`.
     사유 문자열은 `ReviewMeasurementRow` 와 동일 규칙: DATUM_FAIL→"DETECT FAIL", NO_IMAGE→"NO IMAGE", MEASURE_FAIL→"측정실패"(ReviewMeasurementRow.JUDGE_MEASURE_FAIL 재사용), 그 외 공차 이탈(LastHasResult && !LastJudgement)→"공차이탈".
     CROSS_Z_INCOMPLETE 는 불량으로 세지 않는다(Task 2 규칙과 동일).
     최대 `MAX_NG_ITEMS = 3` 개까지 ", " 로 나열하고 초과분은 ` 외 N` 을 덧붙인다(상수 사용).
   - 사이클 종합 덧붙이기: `TickJudgement` 가 비어 있지 않고 `OverallJudgement` 가 비어 있지 않으며 둘이 다를 때만 ` · 종합 ` + OverallJudgement 를 끝에 붙인다.
     기대 결과 예시(반드시 이 형태):
       `14:14:45  z=32  OK  SHOT_I5 · 종합 NG`
       `14:14:36  z=24  NG  SHOT_F2_P2 ← F2_P2(측정실패)`
       옛 JSON: `14:14:45  NG` (z/Shot 정보 없으면 생략, TickJudgement 없으니 종합 덧붙임도 없음)
   - `public static bool IsFailTick(CycleResultDto dto)` 도 함께 제공: tick 판정이 NG 이거나 OverallJudgement 가 "OK" 가 아니면 true(필터용). dto==null 이면 false.
   - 구분자/라벨은 전부 `private const string` (예: SEP = "  ", ARROW = " ← ", OVERALL_PREFIX = " · 종합 ").

2) `ReviewerWindow.xaml.cs`:
   - `CycleListItem` 에 `public bool IsNg { get; set; }` 추가.
   - `LoadCycleFolders`(53~90): DisplayText 계산 73행을 `display = ReviewerListLabelBuilder.Build(dto);` 로 교체하고, 빈 문자열이면 폴더명 폴백을 유지. `IsNg = ReviewerListLabelBuilder.IsFailTick(dto)` 대입.
     만든 목록을 새 필드 `private List<CycleListItem> _allCycleItems = new List<CycleListItem>();` 에 보관하고, ItemsSource 는 새 메서드 `ApplyCycleListFilter()` 가 설정한다.
   - `ApplyCycleListFilter()`: `chk_failOnly.IsChecked == true` 면 `_allCycleItems.Where(i => i.IsNg)`, 아니면 전체. **선택 항목 자동 변경 금지**(SelectionChanged 로 이미지가 튀지 않게), ItemsSource 대입만.
   - `ChkFailOnly_Changed`(205): 기존 `ApplyRowFilter();` 앞에 `ApplyCycleListFilter();` 호출 추가 — 같은 체크박스가 목록·표 둘 다에 걸린다.
   - `ApplyRowFilter`(186, 192) 의 불량 화이트리스트에 `ReviewMeasurementRow.JUDGE_MEASURE_FAIL` 을 추가한다(측정실패 행이 '불량만 보기'에서 사라지지 않도록). 두 곳 모두.
   - code-behind 에는 호출·대입만 둔다. 라벨 문자열 조립 로직은 절대 여기에 쓰지 않는다.

3) `ReviewerWindow.xaml` listBox_cycles(79~87) ItemTemplate 의 TextBlock 에 `IsNg` DataTrigger 로 글자색 지정(Style 안에 `<DataTrigger Binding="{Binding IsNg}" Value="True"><Setter Property="Foreground" Value="#C62828"/></DataTrigger>`). 기본색은 지정하지 않는다(테마 유지).
   dataGrid_measurements RowStyle(168~187) 에 `<DataTrigger Binding="{Binding JudgeText}" Value="측정실패"><Setter Property="Background" Value="#FFD6D6"/></DataTrigger>` 를 NG 트리거 옆에 추가하고, IsSelected Trigger 는 반드시 **맨 뒤**로 유지한다.
  </action>
  <verify>
    <automated>"C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" WPF_Example/DatumMeasurement.csproj -p:Configuration=Release -p:Platform=x64 -t:Build 2>&amp;1 | grep -c "error CS"  # 0 (XAML 오류도 error MC/XDG 로 0 인지 함께 확인)</automated>
    <automated>grep -c "ReviewerListLabelBuilder" WPF_Example/UI/ViewModel/CycleResultDto.cs WPF_Example/UI/Reviewer/ReviewerWindow.xaml.cs  # 각 1 이상</automated>
    <automated>grep -n "DisplayText = \|IsNg" WPF_Example/UI/Reviewer/ReviewerWindow.xaml.cs  # 라벨 조립 로직이 code-behind 에 없는지 육안 확인</automated>
    <automated>verification_greps 실행 — 전부 0</automated>
  </verify>
  <done>리뷰어 목록이 `HH:mm:ss  z=NN  OK|NG  SHOT... [← 항목(사유)] [· 종합 NG]` 로 표시되고, NG 는 붉은 글자, '불량만 보기'가 목록과 표에 동시에 적용되며, 옛 cycle.json 은 기존 형식으로 크래시 없이 표시된다.</done>
</task>

<task type="checkpoint:human-verify" gate="blocking">
  <name>Task 4: 실기 확인 (자동 사이클 1회 + 옛 결과 폴백)</name>
  <what-built>측정 실패 기록(MEASURE_FAIL + 사유 원문), tick 판정/z/Shot 메타 필드, 리뷰어 목록 라벨·색·필터 확장. TCP 규약·판정 로직·자동 사이클 흐름은 무변경.</what-built>
  <how-to-verify>
1) 새 빌드로 프로그램 실행 → BOTTOM 자동 사이클 1회 수행.
2) 결과 리뷰어 → "날짜 폴더 열기" → 오늘 날짜 폴더 선택.
   - 좌측 목록의 각 줄에 `시각  z=NN  OK/NG  SHOT_이름` 이 보이는가?
   - NG 줄에 `← 항목명(사유)` 가 붙고 글자가 빨간가?
   - 마지막 tick 이 tick 자체는 OK 인데 사이클 종합이 NG 라면 `· 종합 NG` 가 보이는가?
3) 에지 검출이 안 되는 항목이 있는 tick 을 선택 → 우측 표 판정 칸이 "—" 가 아니라 "측정실패" 로 나오는가? 엑셀 export 판정 칸도 동일한가?
4) "불량만 보기" 체크 → 좌측 목록도 불량 tick 만 남는가? 해제 시 전부 복귀하는가?
5) 예전 날짜 폴더(2026-09-08 이전, 새 필드 없는 cycle.json)를 열어 → 예전처럼 `시각  OK/NG` 로 뜨고 크래시/빈 목록이 없는가?
6) 자동 사이클 판정/TCP 응답이 이전과 동일한가(핸들러 쪽 OK/NG 수신 변화 없음).
  </how-to-verify>
  <resume-signal>"승인" 또는 문제점을 알려주세요</resume-signal>
</task>

</tasks>

<threat_model>
## Trust Boundaries

| Boundary | Description |
|----------|-------------|
| cycle.json → 리뷰어 | 디스크의 과거 결과 파일(구버전/손상 가능)이 UI 로 들어온다 |
| 측정 알고리즘 오류 문자열 → 저장 계층 | Halcon 예외 메시지가 INI/JSON/엑셀로 전파될 수 있다 |

## STRIDE Threat Register

| Threat ID | Category | Component | Disposition | Mitigation Plan |
|-----------|----------|-----------|-------------|-----------------|
| T-jzs-01 | Tampering | 레시피 INI 오염 | mitigate | LastErrorMessage 를 프로퍼티가 아닌 public 필드로 선언 — ParamBase.Save 의 GetProperties 순회 대상에서 제외 |
| T-jzs-02 | Denial of Service | ReviewerListLabelBuilder(옛/손상 JSON) | mitigate | null/빈 컬렉션 전 구간 방어, dto==null → 폴더명 폴백, ZIndex 기본 -1 로 "없음" 판별 |
| T-jzs-03 | Information Disclosure | 에러 원문 길이/개행 | mitigate | MEASURE_ERROR_MAX_LEN(200) 절단 + 개행 치환 |
| T-jzs-SC | Tampering | 패키지 설치 | accept | 신규 패키지 설치 없음 — 기존 파일 수정만 |
</threat_model>

<verification>
- `error CS` 0 (MSB3027 파일 복사 실패는 D:\Data 의 exe 실행 중이라 발생 가능 — 컴파일 통과로 간주, 프로세스 강제종료 금지)
- 수정한 각 파일의 신규 diff 줄에 대해 verification_greps 전부 0
- `git status` 로 `WPF_Example/DatumMeasurement.csproj` 가 스테이징되지 않았는지 확인. `git add .` / `git add -A` 금지 — 수정 파일만 개별 스테이징
- 새 .cs 파일 0개 (`git status --porcelain | grep '^??'` 에 .cs 없음)
</verification>

<success_criteria>
- 측정 실패 항목이 리뷰어/엑셀에서 "측정실패" 로 보인다(더 이상 "—" 아님)
- 리뷰어 목록 한 줄로 시각/z/tick 판정/Shot/불량 항목·사유/사이클 종합을 알 수 있다
- 옛 cycle.json 폴백 정상, 크래시 0
- '불량만 보기'가 목록·표에 동시 적용
- TCP 응답 규약·판정 로직·자동 사이클 흐름·cycle.json 기존 필드 무변경
</success_criteria>

<output>
완료 시 `.planning/quick/260908-jzs-reviewer-ng-label/260908-jzs-SUMMARY.md` 작성
</output>
