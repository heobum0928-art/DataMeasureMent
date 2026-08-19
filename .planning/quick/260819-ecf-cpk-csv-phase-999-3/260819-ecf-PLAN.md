---
phase: quick-260819-ecf
plan: 01
type: execute
wave: 1
depends_on: []
autonomous: true
requirements: [PHASE-999.3-D1, PHASE-999.3-D2, PHASE-999.3-D3]
files_modified:
  - WPF_Example/Custom/Sequence/Inspection/MeasurementHistoryCsvLoader.cs
  - WPF_Example/Custom/Export/CpkReportExportService.cs
  - WPF_Example/UI/Statistics/StatisticsWindow.xaml
  - WPF_Example/UI/Statistics/StatisticsWindow.xaml.cs

must_haves:
  truths:
    - "통계분석 창에서 기간/레시피를 조회한 뒤 [CPK 리포트 export] 버튼으로 xlsx 를 저장할 수 있다"
    - "CSV 이력이 사이클 단위로 재조립된다 — 실데이터 20260819.csv 기준 39 사이클 / 61 측정키 / 사이클당 61행"
    - "같은 초(second)에 겹친 서로 다른 사이클이 하나로 합쳐지지 않는다 (실데이터에 14건 존재)"
    - "자재번호가 RAW DATA 4행 열 그룹 라벨('자재 1' / '미지정')로 나타난다"
    - "RAW DATA 열은 최근 100회로 제한되지만 Cpk 통계 시트 숫자는 조회 범위 전체로 계산된다"
    - "리뷰어 창의 기존 CPK 리포트 export(폴더 반복검사)와 통계분석 창 화면 표시가 회귀 없이 그대로 동작한다"
  artifacts:
    - path: "WPF_Example/Custom/Sequence/Inspection/MeasurementHistoryCsvLoader.cs"
      provides: "QueryCycles(dtFrom, dtTo, szRecipeFilter) → List<CycleResultDto> 사이클 재조립"
      contains: "public static List<CycleResultDto> QueryCycles"
    - path: "WPF_Example/Custom/Export/CpkReportExportService.cs"
      provides: "4-인자 ExportCpkReport 오버로드 (RAW 열 상한) + DEFAULT_MAX_RAW_COLUMNS"
      contains: "int nMaxRawColumns"
    - path: "WPF_Example/UI/Statistics/StatisticsWindow.xaml"
      provides: "btn_CpkExport 버튼"
      contains: "btn_CpkExport"
    - path: "WPF_Example/UI/Statistics/StatisticsWindow.xaml.cs"
      provides: "Btn_CpkExport_Click 핸들러 + 조회결과 연동 활성화"
      contains: "Btn_CpkExport_Click"
  key_links:
    - from: "WPF_Example/UI/Statistics/StatisticsWindow.xaml.cs"
      to: "MeasurementHistoryCsvLoader.QueryCycles"
      via: "Btn_CpkExport_Click 내부 직접 호출"
      pattern: "QueryCycles\\("
    - from: "WPF_Example/UI/Statistics/StatisticsWindow.xaml.cs"
      to: "CpkReportExportService.ExportCpkReport (4-인자)"
      via: "SaveFileDialog 확정 후 호출, 상한=DEFAULT_MAX_RAW_COLUMNS"
      pattern: "DEFAULT_MAX_RAW_COLUMNS"
    - from: "CpkReportExportService.ExportCpkReport (3-인자)"
      to: "CpkReportExportService.ExportCpkReport (4-인자)"
      via: "int.MaxValue 위임 — ReviewerWindow 호출부 무변경"
      pattern: "int\\.MaxValue"
---

<objective>
자동(PLC/TCP) 운전으로 일자별 CSV 에 쌓인 양산 이력을, 통계분석 창의 날짜 범위 그대로 골라
Phase 72 의 2장짜리 CPK 엑셀 리포트로 출력한다 (백로그 Phase 999.3, D-1/D-2/D-3).

핵심 설계: **CSV → `List<CycleResultDto>` 로 재조립한 뒤 기존 `ExportCpkReport` 를 그대로 호출한다.**
시트 작성 로직(`BuildSampleColumns`/`BuildRawRows`/`WriteRawDataSheet`/`WriteCpkSheet`/`AppendChartBlock`)은
한 줄도 고치지 않는다 → 어제 검증된 Phase 72 경로의 회귀 위험이 0 이다.
자재번호(D-3)는 `BuildSampleColumns` 가 이미 `CycleResultDto.IndexNumber` 를 읽으므로 재조립만으로 자동 해결된다.

Purpose: 현장에서 실제 공정능력을 제출하려면 양산 CSV → CPK 엑셀 경로가 필요하다.
        (폴더 반복검사는 이미지 1장을 모든 Shot 에 먹이므로 다중 Shot 레시피에서 양산 통계용으로 쓸 수 없다.)
Output: 통계분석 창 [CPK 리포트 export] 버튼 + 로더의 사이클 재조립 API + 엑셀기의 RAW 열 상한 오버로드.
</objective>

<execution_context>
@$HOME/.claude/get-shit-done/workflows/execute-plan.md
@$HOME/.claude/get-shit-done/templates/summary.md
</execution_context>

<context>
@CLAUDE.md
@.planning/STATE.md

편집 대상 4파일 (전부 이미 `WPF_Example/DatumMeasurement.csproj` 에 등록되어 있다 —
**신규 .cs 파일을 만들지 말 것. csproj 를 건드리지 말 것.**):
@WPF_Example/Custom/Sequence/Inspection/MeasurementHistoryCsvLoader.cs
@WPF_Example/Custom/Export/CpkReportExportService.cs
@WPF_Example/UI/Statistics/StatisticsWindow.xaml
@WPF_Example/UI/Statistics/StatisticsWindow.xaml.cs

읽기 전용 참조 (수정 금지):
@WPF_Example/Custom/Sequence/Inspection/MeasurementHistoryCsvWriter.cs
@WPF_Example/UI/ViewModel/CycleResultDto.cs
@WPF_Example/UI/Reviewer/ReviewerWindow.xaml.cs

<interfaces>
<!-- 플래너가 코드에서 직접 추출한 계약. executor 는 코드베이스 탐색 없이 이대로 쓴다. -->

CSV 컬럼 계약 (MeasurementHistoryCsvWriter.CSV_HEADER, 14열, 0-based):
```
0 검사일시("yyyy-MM-dd HH:mm:ss")   7  NominalValue(F4)
1 RecipeName                        8  TolerancePlus(F4)
2 IndexNumber        ← COL_INDEX    9  ToleranceMinus(F4)
3 ShotName                          10 MeasuredValue(F4)
4 FAIName                           11 Judgement
5 MeasurementName                   12 HasResult
6 TypeName                          13 OverallCycleResult("P"/"F"/"N")  ← COL_OVERALL
```

MeasurementHistoryCsvLoader.cs 에 이미 있는 것 (전부 재사용, 수정 금지):
```csharp
private const string CSV_EXT = ".csv";
private const string HEADER_FIRST_TOKEN = "검사일시";
private const int COLUMN_COUNT = 14;
private const int COL_TIME = 0;      private const int COL_RECIPE = 1;
private const int COL_SHOT = 3;      private const int COL_FAI = 4;
private const int COL_MEASNAME = 5;  private const int COL_TYPE = 6;
private const int COL_NOMINAL = 7;   private const int COL_TOLPLUS = 8;
private const int COL_TOLMINUS = 9;  private const int COL_MEASURED = 10;
private const int COL_JUDGE = 11;

public  static StatisticsQueryResult Query(DateTime dtFrom, DateTime dtTo, string szRecipeFilter)   // ★ 무변경
private static MeasurementResultDto  BuildMeasFromRow(List<string> fields)   // ★ 반드시 재사용 — 새로 만들지 말 것
private static double                ParseDouble(string sz)                  // ★ 재사용
private static List<string>          ParseCsvLine(string szLine)             // ★ 재사용
```
`BuildMeasFromRow` 는 Judgement 5분기(DATUM_FAIL / NO_IMAGE / NO_RESULT / OK / NG)를
`LastSkipReason`/`LastHasResult`/`LastJudgement`/`LastMeasuredValue` 로 정확히 되돌린다.

MeasurementHistoryCsvLoader.cs 상단 using (전부 이미 있음 — **새 using 불필요**):
`System`, `System.Collections.Generic`, `System.Globalization`, `System.IO`, `System.Text`,
`ReringProject.UI`, `ReringProject.Utility`, `ReringProject.Setting`
→ `HashSet<string>`, `Dictionary<,>`, `int.TryParse`, `DateTime.TryParseExact`, `CultureInfo` 전부 사용 가능.
→ `CycleResultDto`/`ShotResultDto`/`FaiResultDto` 는 `ReringProject.UI` 에 있고 이미 using 되어 있다.

DTO 계층 (WPF_Example/UI/ViewModel/CycleResultDto.cs):
```csharp
public class CycleResultDto {
    public DateTime InspectionTime { get; set; }
    public string RecipeName { get; set; }
    public int IndexNumber { get; set; } = -1;          // -1 = 미지정 sentinel
    public string OverallJudgement { get; set; }        // "OK" / "NG" / "DETECT_FAIL"
    public string CycleFolderPath { get; set; }
    public List<ShotResultDto> Shots { get; set; } = new List<ShotResultDto>();
}
public class ShotResultDto { public string ShotName { get; set; } ... public List<FaiResultDto> FAIs { get; set; } }
public class FaiResultDto  { public string FAIName  { get; set; } ... public List<MeasurementResultDto> Measurements { get; set; } }
```

CpkReportExportService.cs 현재 진입점 (Phase 72 검증 완료 — 시트 작성부 무변경):
```csharp
public static bool ExportCpkReport(List<CycleResultDto> cycles, string recipeName, string outputPath)
// 내부: columns = BuildSampleColumns(cycles) → rows = BuildRawRows(columns)
//       WriteRawDataSheet(wsRaw, columns, rows, recipeName)
//       stats.AddSample(c) for each c in cycles → statDict
//       WriteCpkSheet(wsCpk, rows, statDict) → AppendChartBlock(...)
private const int MATERIAL_NOT_SET = -1;
private const string MATERIAL_UNSET_LABEL = "미지정";
private static List<SampleColumn> BuildSampleColumns(List<CycleResultDto> cycles)
    // → IndexNumber 오름차순 정렬 후 MaterialLabel = "자재 N" 또는 "미지정", HeaderLabel = "#1","#2"...
private static void WriteRawDataSheet(IXLWorksheet ws, List<SampleColumn> columns, List<RawRow> rows, string recipeName)
    // → ws.Cell(3,1)="샘플 수", ws.Cell(3,2)=columns.Count   ← Task 2 의 truncation note 앵커
```

ReviewerWindow 의 기존 CPK export 관례 (Task 3 이 그대로 따를 것):
```csharp
string initialDir = SystemHandler.Handle.Setting.ResultSavePath;
var dlg = new Microsoft.Win32.SaveFileDialog {
    Filter = "Excel 파일 (*.xlsx)|*.xlsx", FileName = "cpk_report_...xlsx", InitialDirectory = initialDir };
if (dlg.ShowDialog() == true) { bool ok = ...ExportCpkReport(...);
    string msg; if (ok) { msg = "저장 완료:\n" + dlg.FileName; } else { msg = "export 실패 (로그 확인)"; }
    MessageBoxImage icon; if (ok) { icon = MessageBoxImage.Information; } else { icon = MessageBoxImage.Error; }
    CustomMessageBox.Show("CPK 리포트 export", msg, icon); }
```
버튼 문구는 리뷰어 창과 동일한 `CPK 리포트 export` (ReviewerWindow.xaml L57 `btn_cpkReportExport` 실측).
`CustomMessageBox` 는 `ReringProject.UI` — StatisticsWindow 와 같은 네임스페이스라 using 불필요.
`SystemHandler` 는 `ReringProject` 루트라 enclosing namespace 로 해석된다 — using 불필요.
</interfaces>
</context>

<ground_rules>

### G-1. 착수 baseline (실측 확인 완료)
- HEAD = `56e0195`
- 워킹트리 미커밋 2건 — **둘 다 이번 작업과 무관, 절대 스테이징 금지**:
  - `M WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs`  (주석에서 `⚠` 2개 제거)
  - `M WPF_Example/DatumMeasurement.csproj`  (커밋 금지 로컬 설정: Debug OutputPath=`D:\Data\`, Release `SIMUL_MODE`)
- **`git add -A` / `git commit -a` 금지.** 커밋마다 대상 파일 경로만 명시 스테이징.
- 커밋 후 매번 `git status --porcelain` 이 위 2줄을 **여전히 ` M` 로 유지**하는지 확인.

### G-2. 코딩 컨벤션 (절대)
- **삼항연산자 `?:` 금지** — if-else 만. 편집 대상 3개 .cs 파일의 현재 삼항 개수는 **전부 0** (실측). 작업 후에도 0.
  - 단, `??` / `?.` 는 허용(기존 코드에 `fai.FAIName ?? ""` 존재).
- 헝가리언: `bXxx` / `nXxx` / `szXxx` / `dXxx`.
- **C# 7.2 only** — switch expression, pattern-matching switch, NRT, record, 로컬함수 남용 금지.
- 브레이스 스타일: 편집 대상 3개 .cs 파일 **전부 Allman** (실측). Allman 유지.
- 주석은 비자명한 "왜"만. 날짜 주석(`//YYMMDD hbk`) 규칙은 폐기됨 — 새로 달지 말 것.
- **신규 .cs 파일 금지.** 4개 대상 파일 전부 csproj 에 이미 등록되어 있다(실측: L259/L268/L408/L545).

### G-3. 하드 제약 (건드리면 실패)
- `MeasurementHistoryCsvLoader.Query()` 와 그 헬퍼 `LoadFile`/`ProcessRow` — **기존 줄 삭제/수정 0**. 순수 추가만.
- `CpkReportExportService` 의 `WriteRawDataSheet` 를 제외한 시트 작성 로직 — 무변경.
  `WriteRawDataSheet` 도 **3-인자 경로에서는 출력이 바이트 동일**해야 한다(Task 2 참조).
- `MeasurementHistoryCsvWriter` / `RepeatRunService` / `ReviewerWindow.*` — 무변경.
- 시트는 여전히 **2장 고정**(`RAW DATA(1)`, `1Cav 세부치수_Cpk`). 늘리면 D-04 위반.

### G-4. 빌드 규칙
- 앱이 `D:\Data\` 에서 실행 중일 수 있다 → **프로세스 강제종료 절대 금지.** 스크래치 `OutputPath` 로 컴파일만 검증.
- **`-p:` 사용(`//p:` 금지).** `-p:OutputPath="$SCR\\xxx\\"` 의 **후행 백슬래시는 반드시 `\\`** —
  `\"` 로 쓰면 bash 가 unexpected EOF 로 죽어 빌드가 아예 안 돈다(직전 plan 에서 실제 발생한 blocker).
- **경고 baseline = 12줄 (CS0618×10 + CS0162×2).** "경고 0" 을 통과 기준으로 쓰면 항상 거짓 실패.
  통과 기준 = **경고 총 12줄 이하 & 신규 CS0219/CS0168/CS0177/CS0165 0건**.
- 파일 잠김으로 실패하면 OutputPath 이름만 바꿔 재시도. 그래도 안 되면 **죽이지 말고 사용자에게 보고.**

```bash
MSB="/c/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/MSBuild.exe"
SCR="C:\\Users\\tech\\AppData\\Local\\Temp\\claude\\C--Info-Project-DataMeasurement\\9d3a7b4d-2314-4b14-8686-52fd6346a1f9\\scratchpad"
"$MSB" 'C:\Info\Project\DataMeasurement\WPF_Example\DatumMeasurement.csproj' \
  -p:Configuration=Debug -p:Platform=x64 -p:OutputPath="$SCR\\ecf-t1\\" \
  -t:Rebuild -v:minimal -nologo
```

### G-5. 셸 변수는 호출 사이에 살아남지 않는다
Bash 호출마다 셸이 새로 뜬다. `$L` / `$X` / `$W` / `$C` / `$MSB` / `$SCR` / `$BASE` 를 쓰는
**모든 블록의 첫 줄에서 다시 정의**할 것:
```bash
cd /c/Info/Project/DataMeasurement
L=WPF_Example/Custom/Sequence/Inspection/MeasurementHistoryCsvLoader.cs
X=WPF_Example/Custom/Export/CpkReportExportService.cs
W=WPF_Example/UI/Statistics/StatisticsWindow.xaml
C=WPF_Example/UI/Statistics/StatisticsWindow.xaml.cs
CSV=WPF_Example/bin/x64/Debug/Statistics/20260819.csv
BASE=56e0195
```
정의 없이 실행하면 경로가 빈 문자열이 되어 **조용히 오탐**한다.

### G-6. Grep 규칙 (자기모순 방지 — 최근 3개 plan 이 연속으로 여기 걸렸다)
- **모든 grep 에 대상 파일 경로 명시** (없으면 stdin 대기로 멈춘다).
- 개수 기준은 **정확한 선언 줄 앵커**(`^        public static ...$`)로 좁힌다.
  느슨한 식별자 카운트는 XML doc 주석·호출부까지 세어져 영구 실패한다.
- `sed -n '/앵커/,/…/p'` 를 쓰기 전에 **그 앵커의 파일 내 출현 횟수를 먼저 `grep -c` 로 1 인지 확인**하고,
  검증식 자체에도 그 유일성 가드를 함께 넣는다.
- **삼항 검출은 줄 단위**: `grep -nE '\?[^?:]*:' <path> | grep -vE '\?\?|\?\.' | wc -l`.
  `-o`(매치 단위)로 바꾸면 문자열 리터럴에서 오탐이 난다.
- 백슬래시 윈도우 경로 grep 에는 `-F`.

### G-7. 실데이터 기대값 (플래너가 awk 로 직접 산출 — Task 1 의 정답지)
파일: `WPF_Example/bin/x64/Debug/Statistics/20260819.csv` (헤더 1 + 데이터 2379행, 레시피 전부 `FAI_1`)

| 항목 | 기대값 |
|------|--------|
| 재조립 사이클 수 | **39** |
| distinct 측정키 (Shot/FAI/측정명) | **61** |
| 사이클당 행 수 | **전부 61** (편차 0) |
| distinct 타임스탬프 | 25 |
| **같은 초에 2사이클이 겹친 타임스탬프** | **14건** (122행 = 61×2) |
| distinct IndexNumber | `-1`, `1` |

⚠ **같은 초 충돌은 이론이 아니라 실데이터에 존재한다.** 시간만으로 그룹핑하면 39가 아니라 25 사이클이 되어
RAW DATA 열이 14개 소실된다. 게다가 `09:50:17`~`09:50:29` 5건은 **겹친 두 사이클의 IndexNumber 가 둘 다 -1** 이므로
IndexNumber 변화만으로도 못 나눈다 → **"측정키 재등장" 규칙이 반드시 필요하다.**
겹친 블록은 항상 연속(첫 61행 = 사이클 A, 다음 61행 = 사이클 B)임을 실측 확인했다.

</ground_rules>

<tasks>

<task type="auto">
  <name>Task 1: MeasurementHistoryCsvLoader 에 QueryCycles 사이클 재조립 추가 (순수 추가)</name>
  <files>WPF_Example/Custom/Sequence/Inspection/MeasurementHistoryCsvLoader.cs</files>
  <action>
`MeasurementHistoryCsvLoader` 에 **기존 코드를 한 줄도 지우거나 고치지 않고** 다음을 추가한다.

**1) 상수 2개 추가** (기존 const 블록 끝, `COL_JUDGE = 11;` 다음 줄):
```csharp
        private const int COL_INDEX = 2;
        private const int COL_OVERALL = 13;
```

**2) 그룹핑 상태 컨테이너** (private nested class, `MeasurementHistoryCsvLoader` 내부):
```csharp
        /// <summary>
        /// QueryCycles 의 사이클 경계 판정 상태. CSV 는 사이클 단위 append 라 행이 항상 연속이므로
        /// 전체를 메모리에 모으지 않고 직전 행과의 비교만으로 경계를 찾는다.
        /// </summary>
        private class CycleGroupState
        {
            public List<CycleResultDto> Cycles = new List<CycleResultDto>();
            public CycleResultDto Current;
            public string LastTime;
            public string LastRecipe;
            public string LastIndex;
            public HashSet<string> SeenKeys = new HashSet<string>();
            public Dictionary<string, ShotResultDto> ShotMap = new Dictionary<string, ShotResultDto>();
            public Dictionary<string, FaiResultDto> FaiMap = new Dictionary<string, FaiResultDto>();
        }
```

**3) 공개 진입점** — 기존 `Query()` 의 파일 순회 골격을 **같은 방식으로** 복제한다
(디렉터리 빈값 방어 → `dtTo.Date < dtFrom.Date` 방어 → `for (DateTime d = dtFrom.Date; d <= dtTo.Date; d = d.AddDays(1))`
→ `Path.Combine(szDir, d.ToString("yyyyMMdd") + CSV_EXT)` → `File.Exists` skip → 파일 단위 try/catch 격리):
```csharp
        /// <summary>
        /// dtFrom~dtTo 기간의 일자별 CSV 를 읽어 검사 사이클 단위 DTO 목록으로 재조립한다.
        /// CPK 리포트 export 전용 — 화면 통계는 Query() 를 쓴다(무변경).
        /// 반환 순서는 시간 오름차순(오래된 것 → 최신)이며, CSV 의 append 순서를 그대로 따른다.
        /// </summary>
        public static List<CycleResultDto> QueryCycles(DateTime dtFrom, DateTime dtTo, string szRecipeFilter)
```
- 전체를 `try/catch (Exception ex)` 로 감싸고 실패 시 `Logging.PrintErrLog((int)ELogType.Error, "[MeasurementHistoryCsvLoader] QueryCycles failed: " + ex.Message)`
  후 **빈 리스트 반환**(기존 `Query()` 의 방어 관례 동일). throw 금지.
- 파일 단위 헬퍼 `LoadCyclesFromFile(string szPath, string szRecipeFilter, CycleGroupState state)` 를 별도 private 로 만들고
  그 안에서 `File.ReadAllLines(szPath, Encoding.UTF8)` → 빈줄 skip → `ParseCsvLine` →
  `fields.Count < COLUMN_COUNT` skip → `fields[COL_TIME] == HEADER_FIRST_TOKEN` skip → 행 처리.
  파일 단위 try/catch 로 손상 파일 1개가 전체를 중단시키지 않게 격리(기존 `LoadFile` 동일 패턴).
- ⚠ **정렬하지 말 것.** 일자 루프가 오름차순이고 파일 내 append 순서가 곧 시간순이다.
  초 단위 타임스탬프로 정렬하면 같은 초 사이클들의 순서가 흐트러진다.

**4) 사이클 경계 판정** — 아래 시그니처를 **그대로** 쓴다(검증식이 이 줄을 앵커로 잡는다):
```csharp
        /// <summary>
        /// 새 사이클이 시작되는 행인지 판정한다. CSV 타임스탬프가 초 단위라 서로 다른 사이클이
        /// 같은 초에 겹치는 일이 실제로 발생하므로(실데이터 14건) 시간만으로는 나눌 수 없다.
        /// 측정키 재등장을 마지막 방어선으로 둔다 — 한 사이클 안에서 같은 Shot/FAI/측정명은 한 번뿐이다.
        /// </summary>
        private static bool IsNewCycleBoundary(CycleGroupState state, string szTime, string szRecipe, string szIndex, string szKey)
        {
            if (state.Current == null)
            {
                return true;
            }

            if (szTime != state.LastTime || szRecipe != state.LastRecipe)
            {
                return true;
            }

            if (szIndex != state.LastIndex)
            {
                return true;
            }

            if (state.SeenKeys.Contains(szKey))
            {
                return true;
            }

            return false;
        }
```
→ 이 메서드 본문의 `return true;` 는 **정확히 4개**여야 한다(검증식이 4를 요구한다).

**5) 행 처리 로직** (private 헬퍼):
- `szKey = fields[COL_SHOT] + "/" + fields[COL_FAI] + "/" + fields[COL_MEASNAME]`
- 레시피 필터: `if (!string.IsNullOrEmpty(szRecipeFilter) && fields[COL_RECIPE] != szRecipeFilter) { return; }`
  (⚠ `Query()` 와 달리 distinct 레시피 수집은 하지 않는다 — 드롭다운은 `Query()` 가 이미 채운다)
- `IsNewCycleBoundary(...)` 가 true 면 새 `CycleResultDto` 생성 후 `state.Cycles.Add(...)`,
  `state.Current` 교체, `state.SeenKeys.Clear()` / `state.ShotMap.Clear()` / `state.FaiMap.Clear()`.
  새 DTO 필드:
  - `InspectionTime` = `ParseInspectionTime(fields[COL_TIME])`
  - `RecipeName` = `fields[COL_RECIPE]`
  - `IndexNumber` = `ParseIndexNumber(fields[COL_INDEX])`
  - `OverallJudgement` = `MapOverallBack(fields[COL_OVERALL])`
- Shot 은 `state.ShotMap` 에서 ShotName 으로 get-or-add(없으면 `new ShotResultDto { ShotName = ... }` 후 `Current.Shots.Add`).
- FAI 는 `state.FaiMap` 에서 `ShotName + "/" + FAIName` 키로 get-or-add(없으면 `new FaiResultDto { FAIName = ... }` 후 `shot.FAIs.Add`).
  ⚠ FAI 맵 키에 ShotName 을 반드시 포함할 것 — 다른 Shot 에 동명 FAI 가 있으면 섞인다.
- 측정은 **`BuildMeasFromRow(fields)` 를 그대로 호출**해 `fai.Measurements.Add(...)`. **절대 새로 만들지 말 것.**
- 마지막에 `state.SeenKeys.Add(szKey)` 와 `state.LastTime/LastRecipe/LastIndex` 갱신.

**6) 파싱 헬퍼 3개** (전부 private static, 예외 없이 폴백):
```csharp
        /// <summary>"yyyy-MM-dd HH:mm:ss" 파싱. 실패 시 DateTime.MinValue — 그룹핑은 원본 문자열로 하므로 영향 없다.</summary>
        private static DateTime ParseInspectionTime(string sz)
        /// <summary>자재번호 파싱. 공백/실패는 -1(CycleResultDto.IndexNumber 의 미지정 sentinel).</summary>
        private static int ParseIndexNumber(string sz)
        /// <summary>CSV 의 P/F/N 을 CycleResultDto.OverallJudgement 값으로 되돌린다(Writer.MapOverall 의 역함수).</summary>
        private static string MapOverallBack(string sz)   // "P"→"OK", "F"→"NG", 그 외→"DETECT_FAIL"
```
`ParseInspectionTime` 은 `DateTime.TryParseExact(sz, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out dt)`,
`ParseIndexNumber` 는 `int.TryParse(sz, NumberStyles.Integer, CultureInfo.InvariantCulture, out n)`.
⚠ `DateTimeStyles` 는 `System.Globalization` — 이미 using 되어 있다. **새 using 추가 금지.**

**금지 사항**: `Query()` / `LoadFile` / `ProcessRow` / `BuildMeasFromRow` / `ParseDouble` / `ParseCsvLine`
및 기존 상수는 **한 글자도 수정하지 말 것.** 이 task 의 diff 는 순수 추가(삭제 0줄)여야 한다.
  </action>
  <verify>
    <automated>
```bash
cd /c/Info/Project/DataMeasurement
L=WPF_Example/Custom/Sequence/Inspection/MeasurementHistoryCsvLoader.cs
CSV=WPF_Example/bin/x64/Debug/Statistics/20260819.csv

echo "== A. 순수 추가(기존 줄 삭제/수정 0) — 기대 0 =="
git diff HEAD -- "$L" | grep -E '^-' | grep -v '^---' | wc -l

echo "== B. 신규 상수 (각 1) =="
grep -c '^        private const int COL_INDEX = 2;$' "$L"
grep -c '^        private const int COL_OVERALL = 13;$' "$L"

echo "== C. 공개 진입점 시그니처 (1) =="
grep -c '^        public static List<CycleResultDto> QueryCycles(DateTime dtFrom, DateTime dtTo, string szRecipeFilter)$' "$L"

echo "== D. 기존 Query() 시그니처 그대로 살아있음 (1) =="
grep -c '^        public static StatisticsQueryResult Query(DateTime dtFrom, DateTime dtTo, string szRecipeFilter)$' "$L"

echo "== E. 경계 판정 메서드 선언 유일성 (1) — sed 앵커 가드 =="
grep -c '^        private static bool IsNewCycleBoundary(' "$L"

echo "== F. 경계 조건 4개 = 본문 return true; 개수 (4) =="
sed -n '/^        private static bool IsNewCycleBoundary(/,/^        }$/p' "$L" | grep -c 'return true;'

echo "== G. BuildMeasFromRow 재사용(중복 구현 금지) — 선언 1 =="
grep -c '^        private static MeasurementResultDto BuildMeasFromRow(List<string> fields)$' "$L"

echo "== H. 새 using 0 =="
git diff HEAD -- "$L" | grep -cE '^\+using ' || true

echo "== I. 삼항 0 =="
grep -nE '\?[^?:]*:' "$L" | grep -vE '\?\?|\?\.' | wc -l

echo "== J. 실데이터 정답지 (G-7 표와 대조: cycles=39 keys=61 bad=0) =="
tail -n +2 "$CSV" | awk -F, '
{ key=$4"|"$5"|"$6; newc=0;
  if (NR==1) newc=1;
  else if ($1!=pt || $2!=pr || $3!=pi) newc=1;
  else if (seen[key]) newc=1;
  if (newc) { cyc++; delete seen; }
  seen[key]=1; rows[cyc]++; pt=$1; pr=$2; pi=$3; allkeys[key]=1; }
END{ n=0; for(k in allkeys) n++;
     bad=0; for(i=1;i<=cyc;i++) if(rows[i]!=61) bad++;
     print "cycles="cyc" distinct_meas_keys="n" cycles_rowcount_ne_61="bad; }'
```
→ A=0, B=1/1, C=1, D=1, E=1, F=4, G=1, H=0, I=0,
   J = `cycles=39 distinct_meas_keys=61 cycles_rowcount_ne_61=0`

**규칙 동치 대조 (수동, SUMMARY 에 기록)**: 위 awk 는 C# 과 동일한 4조건
(첫 행 / (time,recipe) 변경 / index 변경 / 측정키 재등장)을 구현한다.
`IsNewCycleBoundary` 의 4개 `return true;` 가 awk 의 3개 `newc=1` 분기 + `NR==1` 과 1:1 대응하는지
줄 단위로 대조하고 그 매핑을 SUMMARY 에 표로 남길 것. 어긋나면 C# 을 고친다(awk 를 고치지 말 것).

```bash
cd /c/Info/Project/DataMeasurement
MSB="/c/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/MSBuild.exe"
SCR="C:\\Users\\tech\\AppData\\Local\\Temp\\claude\\C--Info-Project-DataMeasurement\\9d3a7b4d-2314-4b14-8686-52fd6346a1f9\\scratchpad"
"$MSB" 'C:\Info\Project\DataMeasurement\WPF_Example\DatumMeasurement.csproj' \
  -p:Configuration=Debug -p:Platform=x64 -p:OutputPath="$SCR\\ecf-t1\\" \
  -t:Rebuild -v:minimal -nologo 2>&1 | tail -25
```
→ `Build succeeded`, 경고 총 12줄 이하, 신규 CS0219/CS0168/CS0177/CS0165 0건.

커밋 (**대상 파일 1개만 명시 스테이징**):
```bash
cd /c/Info/Project/DataMeasurement
git add WPF_Example/Custom/Sequence/Inspection/MeasurementHistoryCsvLoader.cs
git commit -m "feat(quick-260819-ecf): CSV 이력을 사이클 단위 DTO 로 재조립하는 QueryCycles 추가"
git status --porcelain
```
→ `git status --porcelain` 이 `Action_FAIMeasurement.cs` 와 `DatumMeasurement.csproj` 2줄만 ` M` 로 남긴다.
    </automated>
  </verify>
  <done>
`QueryCycles` 가 존재하고 순수 추가 diff 이며, 경계 판정 4조건이 실데이터 정답지(39/61/0)의 awk 규칙과
1:1 대응한다. 기존 `Query()`/`BuildMeasFromRow` 무변경. 빌드 PASS, 삼항 0, 새 using 0.
  </done>
</task>

<task type="auto">
  <name>Task 2: ExportCpkReport 에 RAW 열 상한 오버로드 추가 (D-2) — 3-인자 경로 출력 바이트 동일 유지</name>
  <files>WPF_Example/Custom/Export/CpkReportExportService.cs</files>
  <action>
D-2: **RAW DATA 시트는 최근 N회만, Cpk 통계 시트 숫자는 전체 데이터로 계산.**

**1) 상수 추가** (상단 상수 블록, `RAW_FIRST_SAMPLE_COLUMN` 근처):
```csharp
        /// <summary>날짜 범위 export 의 RAW DATA 열 기본 상한. 양산 수천 건이면 열이 수천 개가 되어 엑셀이 무거워진다.</summary>
        public const int DEFAULT_MAX_RAW_COLUMNS = 100;
```
(`public` 인 이유: StatisticsWindow 가 이 값을 인자로 넘긴다.)

**2) 기존 3-인자 메서드를 위임 wrapper 로 축소.** 기존 본문 전체를 새 4-인자 메서드로 옮기고,
3-인자는 아래 한 줄만 남긴다(**시그니처 줄은 글자 하나도 바꾸지 말 것** — ReviewerWindow 호출부 무변경):
```csharp
        public static bool ExportCpkReport(List<CycleResultDto> cycles, string recipeName, string outputPath)
        {
            return ExportCpkReport(cycles, recipeName, outputPath, int.MaxValue);
        }
```
기존 XML doc 주석은 3-인자 위에 그대로 둔다.

**3) 신규 4-인자 메서드** (기존 본문을 옮긴 것 + 3곳만 변경):
```csharp
        /// <summary>
        /// nMaxRawColumns 로 RAW DATA 시트 열 수를 제한한 CPK 리포트 export.
        /// Cpk 통계 시트 숫자는 제한 없이 cycles 전체로 계산한다(D-2) — 표시만 줄이고 숫자는 안 버린다.
        /// </summary>
        public static bool ExportCpkReport(List<CycleResultDto> cycles, string recipeName, string outputPath, int nMaxRawColumns)
```
변경점 정확히 3곳:
- (a) `var columns = BuildSampleColumns(cycles);` → `var columns = BuildSampleColumns(TakeRecentCycles(cycles, nMaxRawColumns));`
- (b) `WriteRawDataSheet(wsRaw, columns, rows, recipeName);` → `WriteRawDataSheet(wsRaw, columns, rows, recipeName, cycles.Count);`
- (c) 그 외 전부 그대로. **특히 `foreach (var c in cycles) { stats.AddSample(c); }` 는 잘리지 않은 원본 `cycles`
  를 계속 돌아야 한다** — 이게 D-2 의 핵심이다. 절대 잘린 목록으로 바꾸지 말 것.
- 가드(`cycles == null || cycles.Count == 0 || string.IsNullOrEmpty(outputPath)`)와 catch 블록은 그대로 옮긴다.

**4) 잘라내기 헬퍼**:
```csharp
        /// <summary>
        /// 시간 오름차순 목록에서 뒤쪽(최신) nMax 개만 남긴다. 상한 이하면 원본을 그대로 돌려준다.
        /// 주의: 잘라낸 뒤 등장하지 않는 측정 항목은 Cpk 시트 행에서도 빠진다 —
        /// 행 식별/스펙 컬럼이 RAW 행 목록에서 나오기 때문이다. 그래서 잘린 사실을 시트에 남긴다(WriteRawDataSheet).
        /// </summary>
        private static List<CycleResultDto> TakeRecentCycles(List<CycleResultDto> cycles, int nMax)
        {
            if (nMax <= 0)
            {
                return cycles;
            }

            if (cycles.Count <= nMax)
            {
                return cycles;
            }

            return cycles.GetRange(cycles.Count - nMax, nMax);
        }
```

**5) `WriteRawDataSheet` 에 파라미터 1개 추가 + 잘림 안내 1줄.**
시그니처를 `..., string recipeName, int nTotalCycleCount)` 로 바꾸고,
기존 `ws.Cell(3, 2).Value = columns.Count;` **바로 다음**에만 아래를 추가한다(그 외 본문 무변경):
```csharp
            if (nTotalCycleCount > columns.Count)
            {
                // 잘린 사실을 리포트 안에 남긴다 — Cpk 숫자는 전체 기준이라 열 수와 안 맞아 보일 수 있다(D-2).
                ws.Cell(3, 3).Value = "전체 " + nTotalCycleCount + "회 중 최근 " + columns.Count + "회만 표시 (Cpk 통계는 전체 기준)";
            }
```
⚠ 이 `if` 가드 덕분에 **3-인자 경로(nMaxRawColumns = int.MaxValue → 절대 잘리지 않음)에서는
`nTotalCycleCount == columns.Count` 라 셀이 안 써지고 출력이 바이트 동일**하다. 가드를 빼면 하드 제약 위반이다.

**금지**: `BuildSampleColumns` / `BuildRawRows` / `WriteCpkSheet` / `AppendChartBlock` / `WriteStatCell` /
`BuildCpkJudgement` / `BuildToleranceTypeText` / `PadRowTo` 본문 수정 금지. 시트 2장 고정 유지.
`ReviewerWindow.xaml.cs` 는 손대지 말 것.
  </action>
  <verify>
    <automated>
```bash
cd /c/Info/Project/DataMeasurement
X=WPF_Example/Custom/Export/CpkReportExportService.cs

echo "== A. 기존 3-인자 시그니처 유지 (1) =="
grep -c '^        public static bool ExportCpkReport(List<CycleResultDto> cycles, string recipeName, string outputPath)$' "$X"

echo "== B. 신규 4-인자 시그니처 (1) =="
grep -c '^        public static bool ExportCpkReport(List<CycleResultDto> cycles, string recipeName, string outputPath, int nMaxRawColumns)$' "$X"

echo "== C. 3-인자 → int.MaxValue 위임 (1) =="
grep -cF 'return ExportCpkReport(cycles, recipeName, outputPath, int.MaxValue);' "$X"

echo "== D. 공개 기본 상한 상수 (1) =="
grep -c '^        public const int DEFAULT_MAX_RAW_COLUMNS = 100;$' "$X"

echo "== E. RAW 열은 잘린 목록 (1), 원본 직결 호출 잔존 0 =="
grep -cF 'BuildSampleColumns(TakeRecentCycles(cycles, nMaxRawColumns))' "$X"
grep -cF 'BuildSampleColumns(cycles)' "$X"

echo "== F. Cpk 통계는 전체 cycles (1) =="
grep -c '^                    foreach (var c in cycles)$' "$X"

echo "== G. 잘림 안내는 조건부 (1) — 3-인자 출력 바이트 동일 보장 =="
grep -cF 'if (nTotalCycleCount > columns.Count)' "$X"

echo "== H. 시트 2장 고정 (Worksheets.Add 2회) =="
grep -c 'wb.Worksheets.Add(' "$X"

echo "== I. ReviewerWindow 무변경 — 아래 목록에 ReviewerWindow 가 없어야 한다 =="
git diff HEAD --name-only

echo "== J. 삼항 0 =="
grep -nE '\?[^?:]*:' "$X" | grep -vE '\?\?|\?\.' | wc -l
```
→ A=1, B=1, C=1, D=1, E=1 그리고 0, F=1, G=1, H=2, J=0.
   I 의 목록에 `ReviewerWindow` / `MeasurementHistoryCsvWriter` / `RepeatRunService` 가 **없어야** 한다.

```bash
cd /c/Info/Project/DataMeasurement
MSB="/c/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/MSBuild.exe"
SCR="C:\\Users\\tech\\AppData\\Local\\Temp\\claude\\C--Info-Project-DataMeasurement\\9d3a7b4d-2314-4b14-8686-52fd6346a1f9\\scratchpad"
"$MSB" 'C:\Info\Project\DataMeasurement\WPF_Example\DatumMeasurement.csproj' \
  -p:Configuration=Debug -p:Platform=x64 -p:OutputPath="$SCR\\ecf-t2\\" \
  -t:Rebuild -v:minimal -nologo 2>&1 | tail -25
```
→ `Build succeeded`, 경고 12줄 이하, 신규 CS0219/CS0168/CS0177/CS0165 0건.

커밋:
```bash
cd /c/Info/Project/DataMeasurement
git add WPF_Example/Custom/Export/CpkReportExportService.cs
git commit -m "feat(quick-260819-ecf): ExportCpkReport 에 RAW 열 상한 오버로드 추가 (Cpk 통계는 전체 유지)"
git status --porcelain
```
→ 2줄(`Action_FAIMeasurement.cs`, `DatumMeasurement.csproj`)만 ` M` 로 남는다.

**커밋 이후** 3-인자 경로 무해성 재확인:
```bash
cd /c/Info/Project/DataMeasurement
git show --stat HEAD
git show HEAD -- WPF_Example/Custom/Export/CpkReportExportService.cs | grep -cE '^\-.*(WriteCpkSheet|AppendChartBlock|BuildRawRows|PadRowTo)\(' 
```
→ `git show --stat` 에 `CpkReportExportService.cs` 1개 파일만. 마지막 grep = **0**
   (시트 작성 헬퍼 호출부가 삭제되지 않았다 = 로직 이동 없음).
    </automated>
  </verify>
  <done>
3-인자 `ExportCpkReport` 시그니처가 그대로 살아 있고 4-인자로 `int.MaxValue` 위임한다.
RAW 열만 최근 N회로 잘리고 `stats.AddSample` 은 전체 `cycles` 를 돈다. 잘림 안내는 조건부라
3-인자 경로 출력이 바이트 동일하다. 시트 2장 고정. 빌드 PASS, 삼항 0, ReviewerWindow 무변경.
  </done>
</task>

<task type="auto">
  <name>Task 3: StatisticsWindow 에 [CPK 리포트 export] 버튼 배선 (D-1)</name>
  <files>WPF_Example/UI/Statistics/StatisticsWindow.xaml, WPF_Example/UI/Statistics/StatisticsWindow.xaml.cs</files>
  <action>
**1) XAML** — `StatisticsWindow.xaml` 필터바의 `btn_Query` **바로 다음 줄**에 추가:
```xml
                <Button x:Name="btn_CpkExport" Content="CPK 리포트 export" Click="Btn_CpkExport_Click"
                        IsEnabled="False"
                        Padding="14,4" Margin="8,0,0,0" VerticalAlignment="Center"/>
```
문구는 리뷰어 창 `btn_cpkReportExport` 와 동일하게 `CPK 리포트 export`. 초기 상태는 비활성.

**2) code-behind** — `StatisticsWindow.xaml.cs` 에 아래를 추가한다. **새 using 추가 금지**
(`CycleResultDto` 는 같은 `ReringProject.UI`, `CustomMessageBox` 도 같은 네임스페이스,
`SystemHandler` 는 루트 네임스페이스로 해석, `Microsoft.Win32.SaveFileDialog` / `ReringProject.Export.CpkReportExportService` 는 완전수식으로 쓴다).

(2-a) 순수 추출 헬퍼 2개 — 기존 로직을 **의미 변경 없이** 뽑아낸다:
```csharp
        /// <summary>레시피 콤보 현재 선택 → 필터 문자열. "전체" 또는 미선택이면 빈 문자열(=필터 없음).</summary>
        private string GetSelectedRecipeFilter()
        {
            string szRecipe = "";
            if (combo_Recipe.SelectedItem != null)
            {
                string szSel = combo_Recipe.SelectedItem.ToString();
                if (szSel != RECIPE_ALL)
                {
                    szRecipe = szSel;
                }
            }

            return szRecipe;
        }

        /// <summary>DatePicker 두 개 → 조회 기간. 미선택이면 오늘로 폴백(기존 DoQuery 동작 동일).</summary>
        private void GetSelectedRange(out DateTime dtFrom, out DateTime dtTo)
        {
            dtFrom = DateTime.Today;
            if (dp_From.SelectedDate.HasValue)
            {
                dtFrom = dp_From.SelectedDate.Value;
            }

            dtTo = DateTime.Today;
            if (dp_To.SelectedDate.HasValue)
            {
                dtTo = dp_To.SelectedDate.Value;
            }
        }
```
- `Btn_Query_Click` 본문의 콤보 읽기 5줄을 `string szRecipe = GetSelectedRecipeFilter();` 로 교체.
- `DoQuery` 의 날짜 읽기 10줄을 `DateTime dtFrom; DateTime dtTo; GetSelectedRange(out dtFrom, out dtTo);` 로 교체.
  → 둘 다 **순수 이동**이다. 조건/폴백을 바꾸지 말 것.

(2-b) 버튼 활성화 — `DoQuery` 의 `ClearCharts();` **바로 다음 줄**에 `UpdateExportButtonState();` 추가하고:
```csharp
        /// <summary>조회 결과가 있을 때만 export 버튼을 연다. 조회 전/0건이면 비활성.</summary>
        private void UpdateExportButtonState()
        {
            bool bEnable = false;
            if (m_lastResult != null && m_lastResult.TotalRowCount > 0)
            {
                bEnable = true;
            }

            btn_CpkExport.IsEnabled = bEnable;
        }
```
(생성자에서 `InitializeComponent()` 후 `DoQuery("")` 를 부르므로 `btn_CpkExport` 는 이미 존재한다.)

(2-c) 클릭 핸들러 — ReviewerWindow 관례를 그대로 따른다:
```csharp
        /// <summary>
        /// 현재 조회 조건(기간/레시피)으로 CSV 이력을 사이클 단위로 재조립해 CPK 리포트 xlsx 를 저장한다.
        /// 화면 통계와 달리 사이클 재구성이 필요하므로 Query() 가 아니라 QueryCycles() 를 쓴다.
        /// </summary>
        private void Btn_CpkExport_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                DateTime dtFrom;
                DateTime dtTo;
                GetSelectedRange(out dtFrom, out dtTo);
                string szRecipeFilter = GetSelectedRecipeFilter();

                List<CycleResultDto> cycles = MeasurementHistoryCsvLoader.QueryCycles(dtFrom, dtTo, szRecipeFilter);
                if (cycles == null || cycles.Count == 0)
                {
                    CustomMessageBox.Show("CPK 리포트 export", "해당 기간에 데이터가 없습니다.", MessageBoxImage.Warning);
                    return;
                }

                string szRecipeName = szRecipeFilter;
                if (string.IsNullOrEmpty(szRecipeName))
                {
                    szRecipeName = RECIPE_ALL;
                }

                var dlg = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "Excel 파일 (*.xlsx)|*.xlsx",
                    FileName = "cpk_report_" + dtFrom.ToString("yyyyMMdd") + "_" + dtTo.ToString("yyyyMMdd") + ".xlsx",
                    InitialDirectory = SystemHandler.Handle.Setting.ResultSavePath
                };

                if (dlg.ShowDialog() == true)
                {
                    bool bOk = ReringProject.Export.CpkReportExportService.ExportCpkReport(
                        cycles, szRecipeName, dlg.FileName,
                        ReringProject.Export.CpkReportExportService.DEFAULT_MAX_RAW_COLUMNS);

                    string szMsg;
                    if (bOk)
                    {
                        szMsg = "저장 완료:\n" + dlg.FileName;
                    }
                    else
                    {
                        szMsg = "export 실패 (로그 확인)";
                    }

                    MessageBoxImage icon;
                    if (bOk)
                    {
                        icon = MessageBoxImage.Information;
                    }
                    else
                    {
                        icon = MessageBoxImage.Error;
                    }

                    CustomMessageBox.Show("CPK 리포트 export", szMsg, icon);
                }
            }
            catch (Exception ex)
            {
                try { Logging.PrintErrLog((int)ELogType.Error, "[StatisticsWindow] Btn_CpkExport_Click: " + ex.Message); } catch { }
                CustomMessageBox.Show("CPK 리포트 export", "export 중 오류가 발생했습니다 (로그 확인)", MessageBoxImage.Error);
            }
        }
```

**금지**: `BuildRows` / `CpkToText` / `YieldRateToText` / 차트 렌더 메서드 수정 금지.
`MeasurementHistoryCsvLoader.Query` 호출부(`DoQuery` L104)는 그대로 둘 것 — 화면 통계 경로 무변경.
  </action>
  <verify>
    <automated>
```bash
cd /c/Info/Project/DataMeasurement
W=WPF_Example/UI/Statistics/StatisticsWindow.xaml
C=WPF_Example/UI/Statistics/StatisticsWindow.xaml.cs

echo "== A. 버튼 정의 (1) + 문구 (1) =="
grep -cF 'x:Name="btn_CpkExport"' "$W"
grep -cF 'Content="CPK 리포트 export"' "$W"

echo "== B. 초기 비활성 (1) =="
grep -c 'IsEnabled="False"' "$W"

echo "== C. 핸들러 선언 (1) =="
grep -c '^        private void Btn_CpkExport_Click(object sender, RoutedEventArgs e)$' "$C"

echo "== D. QueryCycles 배선 (1) =="
grep -cF 'MeasurementHistoryCsvLoader.QueryCycles(dtFrom, dtTo, szRecipeFilter)' "$C"

echo "== E. 4-인자 상한 배선 (1) =="
grep -cF 'CpkReportExportService.DEFAULT_MAX_RAW_COLUMNS' "$C"

echo "== F. 화면 통계 경로 무변경 — 기존 Query 호출 그대로 (1) =="
grep -cF 'm_lastResult = MeasurementHistoryCsvLoader.Query(dtFrom, dtTo, szRecipeFilter);' "$C"

echo "== G. 0건 안내 (1) =="
grep -cF '"해당 기간에 데이터가 없습니다."' "$C"

echo "== H. 활성화 배선: 선언 1 + DoQuery 내 호출 1 = 2 =="
grep -c 'UpdateExportButtonState()' "$C"

echo "== I. 새 using 0 =="
git diff HEAD -- "$C" | grep -cE '^\+using ' || true

echo "== J. 삼항 0 (두 파일 합산) =="
grep -nE '\?[^?:]*:' "$C" | grep -vE '\?\?|\?\.' | wc -l

echo "== K. XAML 이벤트 이름 = code-behind 핸들러 이름 (양쪽 1/1) =="
grep -cF 'Click="Btn_CpkExport_Click"' "$W"
```
→ A=1/1, B=1, C=1, D=1, E=1, F=1, G=1, H=2, I=0, J=0, K=1.

```bash
cd /c/Info/Project/DataMeasurement
MSB="/c/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/MSBuild.exe"
SCR="C:\\Users\\tech\\AppData\\Local\\Temp\\claude\\C--Info-Project-DataMeasurement\\9d3a7b4d-2314-4b14-8686-52fd6346a1f9\\scratchpad"
"$MSB" 'C:\Info\Project\DataMeasurement\WPF_Example\DatumMeasurement.csproj' \
  -p:Configuration=Debug -p:Platform=x64 -p:OutputPath="$SCR\\ecf-t3\\" \
  -t:Rebuild -v:minimal -nologo 2>&1 | tail -25
```
→ `Build succeeded` (XAML 이벤트 미배선이면 여기서 CS1061 로 잡힌다), 경고 12줄 이하,
   신규 CS0219/CS0168/CS0177/CS0165 0건.

커밋 (**XAML + cs 2개만 명시 스테이징**):
```bash
cd /c/Info/Project/DataMeasurement
git add WPF_Example/UI/Statistics/StatisticsWindow.xaml WPF_Example/UI/Statistics/StatisticsWindow.xaml.cs
git commit -m "feat(quick-260819-ecf): 통계분석 창에 날짜 범위 CPK 리포트 export 버튼 추가"
git status --porcelain
```
→ 2줄(`Action_FAIMeasurement.cs`, `DatumMeasurement.csproj`)만 ` M` 로 남는다.

**커밋 이후** 3개 커밋 전체 파일 스코프 최종 확인:
```bash
cd /c/Info/Project/DataMeasurement
BASE=56e0195
git diff --name-only $BASE..HEAD
git log --oneline $BASE..HEAD
```
→ 파일 목록이 **정확히 4개**:
   `WPF_Example/Custom/Export/CpkReportExportService.cs`
   `WPF_Example/Custom/Sequence/Inspection/MeasurementHistoryCsvLoader.cs`
   `WPF_Example/UI/Statistics/StatisticsWindow.xaml`
   `WPF_Example/UI/Statistics/StatisticsWindow.xaml.cs`
   → `DatumMeasurement.csproj` / `Action_FAIMeasurement.cs` / `ReviewerWindow.*` 가 **있으면 실패**.
   커밋 3개.
    </automated>
  </verify>
  <done>
통계분석 창 필터바에 [CPK 리포트 export] 버튼이 있고 조회 결과가 있을 때만 활성화된다.
클릭 시 현재 기간/레시피로 `QueryCycles` → SaveFileDialog → 4-인자 `ExportCpkReport(…, 100)` 가 호출된다.
0건이면 안내 후 중단. 화면 통계 `Query()` 경로 무변경. 빌드 PASS, 삼항 0, 새 using 0,
$BASE..HEAD 변경 파일이 정확히 4개.
  </done>
</task>

</tasks>

<verification>

### 정적 (자동)
- 3개 task 의 verify 블록 전부 통과.
- `git diff --name-only 56e0195..HEAD` = 정확히 4개 파일, 커밋 3개.
- 삼항 0 (4파일 전부, baseline 0 유지).
- msbuild Debug/x64 PASS, 경고 12줄 이하, 신규 CS0219/CS0168/CS0177/CS0165 0건.
- `WPF_Example/DatumMeasurement.csproj` 와 `Action_FAIMeasurement.cs` 는 여전히 unstaged ` M`.

### 실데이터 규칙 동치 (Task 1 verify J + 수동 대조)
`20260819.csv` → **39 사이클 / 61 측정키 / 사이클당 61행** 이 정답지다.
`IsNewCycleBoundary` 의 4개 `return true;` 와 awk 참조 구현의 분기 대응표를 SUMMARY 에 남긴다.

### 실행 UAT (사용자 몫 — SUMMARY 에 안내로 기록, 이 plan 은 여기까지 자동화하지 않는다)
1. 앱 실행 → 통계분석 창 열기 → 기간 `2026-08-19 ~ 2026-08-19`, 레시피 `FAI_1` 조회
2. [CPK 리포트 export] 클릭 → xlsx 저장
3. `RAW DATA(1)` 시트: 3행 `샘플 수` = **39**, C3 잘림 안내 **없음**(39 < 100), 데이터 행 = **61**,
   샘플 열 = G..(39열), **4행에 `미지정` / `자재 1` 열 그룹 라벨**이 보인다 (D-3 충족)
4. `1Cav 세부치수_Cpk` 시트: 행 61개, 통계 숫자 채워짐, 시트는 **2장뿐**
5. 회귀: 리뷰어 창에서 폴더 반복검사 후 기존 [CPK 리포트 export] 클릭 → 이전과 동일하게 동작
   (3행 `샘플 수` = 반복 횟수, C3 안내 없음)

</verification>

<success_criteria>
- 통계분석 창에서 날짜 범위 CPK 리포트 xlsx 를 뽑을 수 있다 (D-1)
- RAW DATA 열은 최근 100회 상한, Cpk 통계 숫자는 조회 범위 전체 기준 (D-2)
- 자재번호가 RAW DATA 4행 열 그룹 라벨로 나온다 (D-3)
- 같은 초에 겹친 사이클이 합쳐지지 않는다 (실데이터 39 사이클 재조립)
- 회귀 0: `Query()` 무변경, 3-인자 `ExportCpkReport` 출력 바이트 동일, ReviewerWindow/Writer/RepeatRunService 무변경
- 신규 .cs 0개 / csproj 무수정 / 삼항 0 / 빌드 PASS(경고 12줄 baseline)
</success_criteria>

<output>
완료 후 `.planning/quick/260819-ecf-cpk-csv-phase-999-3/260819-ecf-SUMMARY.md` 생성.
반드시 포함할 것:
- `IsNewCycleBoundary` 4조건 ↔ awk 참조 구현 분기 **대응표**
- 실데이터 정답지 실측값 (cycles / distinct_meas_keys / rowcount 편차)
- 잘림 시 Cpk 시트 행 누락 케이스의 처리 방식(최근 N회 기준 행 + C3 안내)과 그 근거
- 위 "실행 UAT" 5단계를 사용자 확인 항목으로 그대로 전재
</output>
</content>
</invoke>
