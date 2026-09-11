---
phase: quick-260911-fia
plan: 01
type: execute
mode: quick
wave: 1
depends_on: []
files_modified:
  - WPF_Example/UI/ViewModel/CycleResultDto.cs
  - WPF_Example/Utility/CaptureImageSaveService.cs
  - WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs
  - WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs
  - WPF_Example/Custom/Sequence/Inspection/RepeatRunService.cs
  - WPF_Example/MainWindow.xaml.cs
  - WPF_Example/UI/Statistics/StatisticsWindow.xaml.cs
  - WPF_Example/UI/Statistics/StatisticsWindow.xaml
autonomous: false
requirements:
  - FIA-A-datum-photo-per-cycle
  - FIA-B-rerun-plan-from-saved-cycles
  - FIA-C-rerun-execution-with-restore
  - FIA-D-statistics-window-rerun-ui
  - FIA-E-no-change-guard

must_haves:
  truths:
    - "PLC 자동(프로토콜) 사이클에서 실기 촬영한 기준점 사진이 측정 원본과 같은 original 폴더에 datum_<SEQ>_<기준점이름>[_H|_V]_<HHmmssfff>.<jpg|bmp>(OriginImageFormat 따름) 로 저장되고, 그 tick 의 cycle.json 에 DatumImages(DatumName, Role, Path) 로 기록된다. 기준점 검출이 실패해도 사진은 저장된다."
    - "수동 RUN/반복/일괄, SIMUL 빌드, OfflineInspectMode, 캐시 재사용(그 tick 에 기준점을 다시 안 찍음) tick 에서는 datum_ 사진도 DatumImages 기록도 생기지 않는다. 옛 cycle.json 은 DatumImages 가 빈 목록으로 읽힌다."
    - "통계 창 '저장 사진으로 재검사' 는 선택 기간 + 레시피 + 시퀀스의 자동 cycle.json 만 읽어 기준점 z tick 을 시작으로 부품 단위로 묶고, 기준점 사진이 없는 옛 사이클·사진 파일이 없는 부품·Shot/두 장짜리 측정 사진이 빠진 부품은 제외하며 사유별 개수를 보여준다."
    - "재검사는 부품마다 그 부품의 Shot/기준점/두 장짜리 측정 사진 경로를 메모리에서만 바꿔 수동 StartAll 로 돌리고, 완료·사용자 중단·시퀀스 중단/오류·통계 창 닫기·프로그램 종료 어느 경우든 원래 경로와 원래 OfflineInspectMode 로 되돌린다. 레시피 파일/Setting 파일은 재검사 코드가 저장하지 않는다."
    - "재검사가 도는 동안(부품 사이 Idle 포함) 레시피 저장 시도는 '재검사 중 저장 불가' 안내로 막힌다."
    - "재검사 중 PLC 자동 검사가 들어오면(OfflineInspectMode 강제 OFF 또는 프로토콜 사이클 종료 감지) 재검사가 중단되고 사유가 표시되며, 그 자동 사이클 결과는 재검사 결과에 섞이지 않는다."
    - "재검사가 끝나면 통계 표가 재검사 결과로 바뀐다 — 색/나쁜 순 정렬/벗어난 양/요약은 기존 StatRowPresenter 그대로, 칸 2개 '원래 평균'·'변화'(재검사 평균 − 원래 평균) 추가, 상단에 '재검사 결과 — 부품 N개(제외 M개: 사유)' 와 '원래 통계로' 버튼. CPK export 는 지금 보고 있는 쪽(재검사면 재검사 사이클 목록) 기준."
    - "TCP 응답·판정 로직, 자동 사이클 흐름(기준점 사진 저장 추가 외), 수동 검사 통계 제외 규칙은 바뀌지 않는다. Release|x64 빌드 error CS / error MC 0건."
  artifacts:
    - path: "WPF_Example/UI/ViewModel/CycleResultDto.cs"
      provides: "DatumImageRecordDto(ROLE_SINGLE/H/V) + CycleResultDto.DatumImages"
      contains: "public class DatumImageRecordDto"
    - path: "WPF_Example/Utility/CaptureImageSaveService.cs"
      provides: "BuildDatumFileName — datum_ 파일명(원본 포맷 확장자)"
      contains: "public static string BuildDatumFileName"
    - path: "WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs"
      provides: "tick 단위 기준점 사진 기록 수집/초기화 + 두 프로토콜 BuildDto 지점에서 DatumImages 첨부"
      contains: "public void RecordTickDatumImage"
    - path: "WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs"
      provides: "DatumPhase 실기 grab 기준점 사진 비동기 저장(1장/크로스-Z 역할별)"
      contains: "private void ArchiveDatumImageForCycle"
    - path: "WPF_Example/Custom/Sequence/Inspection/RepeatRunService.cs"
      provides: "SavedCycleRerunPlanner/Plan/Part(부품 묶기) + RepeatRunService.StartFromSavedCycles(경로 스냅샷·교체·복원)"
      contains: "public static class SavedCycleRerunPlanner"
    - path: "WPF_Example/MainWindow.xaml.cs"
      provides: "재검사 중 레시피 저장 차단 가드 + 종료 시 복원 훅(Release 전)"
      contains: "IsSavedCycleRerunActive"
    - path: "WPF_Example/UI/Statistics/StatisticsWindow.xaml.cs"
      provides: "StatisticsRerunViewModel + StatRowPresenter.FillRerunComparison + 배선"
      contains: "public class StatisticsRerunViewModel"
    - path: "WPF_Example/UI/Statistics/StatisticsWindow.xaml"
      provides: "재검사 패널(시퀀스 콤보/재검사/중단/원래 통계로/상태) + 원래 평균·변화 칸"
      contains: "btn_Rerun"
  key_links:
    - from: "Action_FAIMeasurement.ArchiveDatumImageForCycle"
      to: "InspectionSequence.RecordTickDatumImage"
      via: "저장 경로 기록(IsLiveCaptureMode && IsProtocolDrivenCycle 게이트)"
      pattern: "RecordTickDatumImage\\("
    - from: "InspectionSequence.AddResponse / PersistAndEnqueueV1"
      to: "CycleResultDto.DatumImages"
      via: "BuildDto 직후 TakeTickDatumImagesSnapshot 대입, SaveAsync 전"
      pattern: "DatumImages = TakeTickDatumImagesSnapshot"
    - from: "InspectionSequence.HandleRunStartResetResults"
      to: "ClearTickDatumImages"
      via: "OnStart(매 tick) 시작 시 초기화 — early return 앞"
      pattern: "ClearTickDatumImages\\(\\)"
    - from: "StatisticsRerunViewModel"
      to: "SavedCycleRerunPlanner.BuildPlan + RepeatRunService.StartFromSavedCycles"
      via: "백그라운드 계획 → UI 스레드 시작"
      pattern: "StartFromSavedCycles\\("
    - from: "RepeatRunService.EndSavedCycleRerun"
      to: "RestoreSavedCycleOverrides"
      via: "완료/중단/OnStop/OnError/종료 공통 단일 종료 지점(멱등)"
      pattern: "RestoreSavedCycleOverrides\\(\\)"
    - from: "MainWindow.SaveRecipe"
      to: "RepeatRunService.IsSavedCycleRerunActive"
      via: "저장 차단 가드"
      pattern: "IsSavedCycleRerunActive"
    - from: "StatisticsWindow.Btn_CpkExport_Click"
      to: "StatisticsRerunViewModel.GetCyclesForExport"
      via: "보고 있는 쪽 사이클 목록"
      pattern: "GetCyclesForExport\\("
---

<objective>
PLC 자동 검사로 이미 찍어 둔 사진으로, 파라미터를 바꾼 뒤 사이클을 다시 돌려 CPK 를 비교할 수 있게 한다
(예: TOP C1_P1 파라미터 변경 → 오늘 자동 9부품을 새 파라미터로 재검사 → 새 CPK 확인).

현재는 측정 자리 원본만 사이클마다 저장되고 기준점(Datum) 사진은 저장되지 않아(오프라인 폴더에 마지막 1장만 덮어씀)
과거 부품을 그대로 재현할 수 없다. 그래서 A(기준점 사진 사이클마다 저장) → B(저장 사이클을 부품 단위 재검사 계획으로)
→ C(경로를 메모리에서만 바꿔 실행 + 반드시 복원) → D(통계 창 UI) 순으로 만든다. E(무변경 영역) 는 모든 태스크의 가드.

Purpose: 현장 사용자가 파라미터 변경 효과를 "같은 부품·같은 사진" 기준으로 바로 확인.
Output: 8개 기존 파일 수정(새 .cs 파일 없음 — csproj 스테이징 금지 규칙), 실기 UAT 체크리스트.
</objective>

<execution_context>
@$HOME/.claude/get-shit-done/workflows/execute-plan.md
@$HOME/.claude/get-shit-done/templates/summary.md
</execution_context>

<context>
@./CLAUDE.md
@.planning/STATE.md

## 하드룰 (모든 태스크 공통 — 위반은 회귀)
- 삼항 `?:` / `??` / `??=` / `?.` / `?[]` / C# 8 switch 식 금지. 한 줄 분기도 중괄호 필수. `&&`/`||` 3개 이상 조건은 이름 있는 bool 로 선추출.
- 헝가리언(b/n/sz/d/hv), 매직넘버 금지(const), 날짜 주석(hbk 등) 신규 금지 — 주석은 비자명한 "왜" 만. C# 7.2 만.
- 파일별 기존 브레이스 스타일: Action_FAIMeasurement.cs / CaptureImageSaveService.cs / MainWindow.xaml.cs = K&R, RepeatRunService.cs / CycleResultDto.cs / StatisticsWindow.xaml.cs = Allman, InspectionSequence.cs = 수정 지점 인접 메서드 스타일(HandleRunStartResetResults 주변 K&R, PersistAndEnqueueV1 주변 Allman).
- HImage 는 반드시 Dispose(finally 에서 try { x.Dispose(); } catch { }), SharedHImage 는 AddRef/Release 짝.
- 새 UI 로직은 VM/정적 헬퍼, code-behind 는 배선만. MainView.xaml.cs 수정 금지.
- 새 문자열 리터럴·주석에 '?' 문자를 쓰지 않는다(삼항 grep 게이트 오탐 방지).
- 검증 grep(추가 줄 한정, .cs): 삼항 `\?[^\?]*:`, `??`, `?.`, `switch.*=>`, `hbk`, 중괄호 없는 `if (...) x;`/`else x;` 전부 0.
- `git add .`/`-A` 금지 — 태스크별 수정 파일만 명시 스테이징. `WPF_Example/DatumMeasurement.csproj` 는 절대 스테이징 금지(로컬 수정 상태 유지).
- D:\Data\Recipe 운영 레시피 파일 쓰기 금지. 실행 중인 exe 강제종료 금지(MSB3027 복사 실패는 error CS/MC 0 이면 통과).

## 확인된 코드 사실 (계획 근거 — 재탐색 불필요)
- cycle.json: `ResultSavePath\<yyyyMMdd>\<HHmmss>_cycle\cycle.json` (CycleResultSerializer.SaveAsync). Load(jsonPath) 은 실패 시 null.
- 측정 원본: FAIConfig.LastOriginImageFileName = CaptureImageSaveService.BuildFilePath(false, name, ts) → **절대경로**가 FaiResultDto.OriginImageFileName 로 복사됨. Shot 공유 원본(QueueSharedShotOrigin)이 비-크로스-Z FAI 전부에 같은 경로로 기록되고, 크로스-Z FAI(E5)는 FinalizeFaiTick 에서 그 tick 에 실제 캡처한 역할 이미지(A 또는 B)로 FAI 별 원본을 따로 저장한다.
- 매 tick 시작(OnStart → HandleRunStartResetResults)에 이 시퀀스 소유 측정 결과가 전부 ClearResult 된다 → 어떤 tick 의 dto 에서 LastHasResult 이거나 LastSkipReason 이 비어있지 않은 측정은 **그 tick 에 처리된 것**. 반면 FAI 의 OriginImageFileName 은 지워지지 않아 과거 값이 남는다(그래서 "처리된 측정이 있는 FAI" 의 원본만 사진 출처로 쓴다).
- 크로스-Z 두 장짜리 측정(E5, ZIndexA=14 점/ZIndexB=15 선): ZIndexA tick 에서는 HalfPending → LastSkipReason=CROSS_Z_INCOMPLETE + FAI 원본=역할 A 이미지, ZIndexB tick 에서는 BothReady → 측정 결과 + FAI 원본=역할 B 이미지. FillTickSummary 는 CROSS_Z_INCOMPLETE 를 "처리됨" 에서 뺀다(MeasuredShotNames 에 안 들어갈 수 있음).
- 수동 RUN(StartAll(null), RequestPacket==null)에서: AcquireShotImage 는 (비-SIMUL) OfflineInspectMode 일 때만 ShotConfig.SimulImagePath 로드, GrabOrLoadDatumImage 는 OfflineInspectMode 일 때 DatumConfig.TeachingImagePath 로드, 크로스-Z 기준점은 TryLoadStaticDualDatumImages(TeachingImagePath / TeachingImagePath_Vertical), E5 는 NotMyTick+HasStaticDualImages → TryGrabOrLoadFaiDualImages 가 측정의 TeachingImagePath_Horizontal(최우선)/TeachingImagePath_Vertical 로 측정. Release|x64 는 SIMUL_MODE 꺼짐(csproj DefineConstants=TRACE).
- 수동 RUN 시작 시 HandleRunStartResetResults 가 ClearDatumTransforms() 하지만 `_bManualDatumHeld`(Test Find 유지)가 true 면 **비우지 않는다** → 재검사는 부품마다 StartAll 직전에 `seq.ClearDatumTransforms()`(public, 락 보호, $RESET 도 쓰는 진입점)를 직접 호출해야 부품별 기준점을 새로 찾는다.
- MainWindow.SaveRecipe 는 `Sequences.StateAll == Running`(하나라도 Running)일 때만 막는다 → 부품 사이 Idle 틈에는 임시 경로가 저장될 수 있음 → 가드 추가 필요. 다른 저장 경로(MainView 3472/4241, InspectionListView 메뉴)도 전부 MainWindow.SaveRecipe 경유.
- SystemHandler.Release()(앱 종료, MainWindow.Window_Closing 461행) 와 SystemHandler.LoadRecipe 가 Setting.Save() 를 부른다 → 재검사 중 켠 OfflineInspectMode 가 파일에 남을 수 있음. 종료 경로는 Release 직전 복원 훅으로 막고, 레시피 로드/설정창 저장 경로는 잔여 위험으로 수용(PLC $TEST 수신 시 ForceOfflineInspectModeOffForAutoTest 가 자동 검사를 보호).
- 기준점 z: `InspectionSequence.GetDatumZIndex()`(public). 프로토콜 판정: `IsProtocolDrivenCycle()`(RequestPacket != null, OnFinish 시점에도 유효 — HandleManualCyclePersist 가 같은 방식 사용).

<interfaces>
From WPF_Example/UI/ViewModel/CycleResultDto.cs (namespace ReringProject.UI):
```csharp
public class CycleResultDto { DateTime InspectionTime; string RecipeName; int IndexNumber = -1; bool IsProtocolDriven;
  string OverallJudgement; string CycleFolderPath; string TickJudgement; List<string> MeasuredShotNames; int ZIndex = -1;
  List<ShotResultDto> Shots; }
public class ShotResultDto { string ShotName; string OwnerSequenceName; string ResultImagePath; List<FaiResultDto> FAIs; }
public class FaiResultDto { string FAIName; bool IsPass; bool WasDatumSkipped; string OriginImageFileName /*절대경로*/; ... List<MeasurementResultDto> Measurements; }
public class MeasurementResultDto { string MeasurementName /*BuildDto: 빈값이면 TypeName*/; string TypeName; ... bool LastHasResult; string LastSkipReason;
  bool IsDualImage; string HorizontalImagePath; string VerticalImagePath; }
```
From WPF_Example/Custom/Sequence/Inspection/CycleResultSerializer.cs:
```csharp
public static CycleResultDto BuildDto(InspectionRecipeManager rm, EVisionResultType r, DateTime when, string recipeName,
    string ownerSequenceName = null, int nIndexNumber = -1, bool bIsProtocolDriven = false, int nZIndex = -1);
public static void SaveAsync(CycleResultDto dto);   // cycle.json + CSV append(비동기)
public static CycleResultDto Load(string jsonPath); // 실패 시 null
```
From WPF_Example/Utility/CaptureImageSaveService.cs (namespace ReringProject.Utility, K&R):
```csharp
public sealed class SharedHImage { SharedHImage(HImage image) /*ref 1*/; void AddRef(); void Release(); HImage Image { get; } }
public sealed class CaptureImageSaveRequest : IDisposable { SharedHImage Shared; bool NeedsRender; string FileName; bool IsCapture; DateTime Timestamp; ... }
public void Enqueue(CaptureImageSaveRequest request);                 // SystemHandler.Handle.CaptureImageSaver
public static string BuildFilePath(bool isCapture, string fileName, DateTime ts); // ResultSavePath\Image\yyMMdd\HHmm\original|capture\name
private static string SanitizeFilePart(string value, string fallback);
private static string ResolveExtension(string prefix);  // "origin" 만 BMP/JPG — 수정 금지(기존 삼항 줄)
// SaveRequest: !IsCapture 이면 OriginImageFormat(BMP→"bmp", 그 외 "jpeg") 로 WriteImage
```
From WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs:
```csharp
public List<DatumConfig> DatumConfigs { get; }
public int GetDatumZIndex(); public int GetExecutionZIndex(); public bool IsProtocolDrivenCycle();
public void ClearDatumTransforms();   // _bManualDatumHeld=false + 캐시/실패집합 비움(락 보호)
public static bool IsShotOwnedBySequence(ShotConfig shot, string szSeqName);
private void HandleRunStartResetResults(SequenceContext context); // OnStart, 매 tick. try 안 첫 줄 _dtCycleStartUtc=..., 다음 줄 if (Actions == null) return;
protected override void AddResponse();          // v2.6: 255~263 BuildDto → 264 SaveAsync
private void PersistAndEnqueueV1(InspectionRecipeManager rm, TestResultPacket packet); // v1.0: 2349~2357 BuildDto → 2358 SaveAsync
```
From WPF_Example/Sequence/Sequence/SequenceBase.cs:
```csharp
public delegate void EventSequenceStateChanged(SequenceContext context);
public event EventSequenceStateChanged OnStart, OnStop, OnFinish, OnError;
public bool StartAll(TestPacket packet);  public EContextState State { get; }  public string Name;
```
From WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs (K&R, namespace ReringProject.Sequence):
```csharp
private void ProcessDatumSingleImage(DatumConfig datum, InspectionSequence parentSeq, ref int nDatumOk, ref int nDatumFail); // 399~427: img=GrabOrLoadDatumImage; try { ... bool bDetectOk = ...; autofill } finally { img.Dispose(); }
private bool CaptureAndStoreCrossZDatumImage(DatumConfig datum, InspectionSequence parentSeq, bool bIsRoleA); // 1209~1221: capturedImage → StoreCrossZImage → SafeDisposeImage(capturedImage)
private static bool IsLiveCaptureMode();  // SIMUL=false, 그 외 !OfflineInspectMode
private static string EnqueueOfflineImageCopy(HImage src, string szBaseName); // 1464~1480 — 사본 SharedHImage → AddRef/Enqueue → finally Release 패턴(미러 대상)
private static void SafeDisposeImage(HImage image);
private const string LOG_TAG = "[FAIMeasurement] ";
```
From WPF_Example/Custom/Sequence/Inspection/ShotConfig.cs / DatumConfig.cs / DualImageEdgeDistanceMeasurement.cs:
```csharp
ShotConfig { string SimulImagePath; string ShotName; string OwnerSequenceName; int ZIndex; List<FAIConfig> FAIList; }
FAIConfig { string FAIName; List<MeasurementBase> Measurements; }   MeasurementBase { string MeasurementName; string TypeName; }
DatumConfig { string DatumName; string TeachingImagePath; string TeachingImagePath_Vertical; int ZIndexA = -1; int ZIndexB = -1; EDatumAlgorithm AlgorithmTypeEnum; }
DualImageEdgeDistanceMeasurement : MeasurementBase { string TeachingImagePath_Horizontal; string TeachingImagePath_Vertical; int ZIndexA = -1; int ZIndexB = -1; }
EDatumAlgorithm.VerticalTwoHorizontalDualImage  // namespace ReringProject.Sequence
SequenceHandler.SEQ_TOP = "TOP" (빈 OwnerSequenceName 폴백), SystemHandler.Handle.Sequences.RecipeManager.Shots, Sequences.Count / Sequences[i]
```
From WPF_Example/Custom/Sequence/Inspection/RepeatRunService.cs (Allman): 기존 Start/StartFromImages/Stop/ApplyCurrentImage/HandleFinish/TriggerNext
(Idle 대기 → Dispatcher.BeginInvoke(Background) → StartAll(null), 아니면 Task.Delay(50) 재시도). HandleFinish 는 lock(_lock) 안에서
소유 shot 종합판정 → BuildDto(.., seqName, MaterialIndexNumber) → SaveAsync → _collected.Add → 진행/완료 이벤트.
From WPF_Example/Custom/Sequence/Inspection/RepeatMeasurementStats.cs: `AddSample(CycleResultDto)`, `ComputeAll()` → Dictionary<"Shot/FAI/측정명", MeasurementStat>, `GetSeries()`.
From MeasurementHistoryCsvLoader: `StatisticsQueryResult { Stats; Series; RecipeNames; TotalRowCount; }`, `Query(from, to, recipe)`, `QueryCycles(from, to, recipe)` — 키 형식 동일("Shot/FAI/측정명").
From WPF_Example/UI/Statistics/StatisticsWindow.xaml.cs: StatRow(Key/Mean/N/…), StatRowPresenter.BuildRows/SortWorstFirst/BuildSummary/IsProblemRow,
StatisticsWindow { m_lastResult; DoQuery(recipe); GetSelectedRecipeFilter(); GetSelectedRange(out,out); UpdateExportButtonState(); ApplyProblemFilter(); ClearCharts(); Btn_CpkExport_Click }.
</interfaces>
</context>

<tasks>

<task type="auto">
  <name>Task 1: [A] 자동 검사 기준점 사진을 사이클마다 저장하고 cycle.json 에 DatumImages 로 기록</name>
  <files>WPF_Example/UI/ViewModel/CycleResultDto.cs, WPF_Example/Utility/CaptureImageSaveService.cs, WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs, WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs</files>
  <action>
(1) CycleResultDto.cs (Allman): 새 클래스 `DatumImageRecordDto` 추가 — public const string ROLE_SINGLE = "", ROLE_HORIZONTAL = "H", ROLE_VERTICAL = "V"; 프로퍼티 DatumName, Role, Path(전부 string, 요구사항 표기 그대로 "Path"). CycleResultDto 의 ZIndex 뒤에 `public List<DatumImageRecordDto> DatumImages { get; set; } = new List<DatumImageRecordDto>();` + XML 주석("자동 검사 tick 에서 새로 촬영·저장된 기준점 사진. 수동/캐시 재사용 tick/옛 cycle.json 은 빈 목록"). CSV writer/Loader 는 건드리지 않는다.

(2) CaptureImageSaveService.cs (K&R): `public const string DATUM_PREFIX = "datum";` 과 `public static string BuildDatumFileName(string szSequence, string szDatumName, string szRole, DateTime ts)` 추가 — 결과 "datum_<seq>_<datum>[_<role>]_<HHmmssfff><ext>". seq/datum/role 은 기존 private SanitizeFilePart 로 정리(role 빈값이면 세그먼트 생략), 확장자는 새 private static `ResolveOriginImageExtension()`(if/else: OriginImageFormat 이 "BMP"(OrdinalIgnoreCase) 면 ".bmp", 아니면 ".jpg") — SaveRequest 가 !IsCapture 요청을 OriginImageFormat 으로 쓰므로 짝이 맞는다. 기존 ResolveExtension(삼항 포함 기존 줄)은 수정 금지.

(3) InspectionSequence.cs: 필드 `private readonly object _tickDatumImageLock = new object();`, `private readonly List<DatumImageRecordDto> _tickDatumImages = new List<DatumImageRecordDto>();` 추가(using ReringProject.UI 이미 있음). 메서드 3개: `public void RecordTickDatumImage(string szDatumName, string szRole, string szPath)` — 빈 이름/경로면 무시, 락 안에서 새 DatumImageRecordDto Add. `private void ClearTickDatumImages()` — 락 안 Clear. `private List<DatumImageRecordDto> TakeTickDatumImagesSnapshot()` — 락 안에서 새 List 복사본 반환. 배선: HandleRunStartResetResults 의 try 안 `_dtCycleStartUtc = DateTime.UtcNow;` 바로 다음, `if (Actions == null) return;` **앞**에 `ClearTickDatumImages();` (매 tick 시작마다 비움 — 기준점을 다시 안 찍은 tick 은 빈 목록 = "캐시 재사용이면 기록 없음"). AddResponse(v2.6) 의 BuildDto 대입문 다음·SaveAsync 앞, PersistAndEnqueueV1(v1.0) 의 BuildDto 대입문 다음·SaveAsync 앞에 각각 `cycleDto.DatumImages = TakeTickDatumImagesSnapshot();` 한 줄. HandleManualCyclePersist(수동)는 건드리지 않는다. 판정·ResponseQueue·TCP 경로 무변경(per FIA-E).

(4) Action_FAIMeasurement.cs (K&R): private static `EnqueueCycleDatumImageCopy(HImage src, string szFileName, DateTime ts)` → bool — EnqueueOfflineImageCopy 와 같은 수명 규약: saver(SystemHandler.Handle.CaptureImageSaver) null 이거나 src null 이면 false, `new SharedHImage(src.CopyImage())`(실패 시 로그 후 false) → try { AddRef; Enqueue(new CaptureImageSaveRequest { Shared, NeedsRender = false, FileName = szFileName, IsCapture = false, Timestamp = ts }); return true; } finally { shared.Release(); }. DirectoryOverride/FormatOverride 는 쓰지 않는다(original 폴더 + OriginImageFormat). private `ArchiveDatumImageForCycle(DatumConfig datum, InspectionSequence parentSeq, HImage img, string szRole)` — 가드: img/datum/parentSeq null 또는 DatumName 빈값이면 return; `bool bArchiveEnabled = IsLiveCaptureMode() && parentSeq.IsProtocolDrivenCycle();` 거짓이면 return (SIMUL/오프라인/수동 제외). ts=DateTime.Now, 파일명 = CaptureImageSaveService.BuildDatumFileName(parentSeq.Name, datum.DatumName, szRole, ts), 경로 = CaptureImageSaveService.BuildFilePath(false, 파일명, ts), 큐잉 성공 시에만 parentSeq.RecordTickDatumImage(datum.DatumName, szRole, 경로). 전체를 try/catch(Exception ex) 로 감싸 Logging.PrintErrLog((int)ELogType.Error, LOG_TAG + ...) 후 삼킨다 — 검사 흐름·판정에 절대 예외 전파 금지. 호출 지점 2곳: (a) ProcessDatumSingleImage 의 try 안 `bool bDetectOk = nDatumOk > nOkBefore;` 다음 줄(자동채움 앞, finally 의 img.Dispose 전)에 `ArchiveDatumImageForCycle(datum, parentSeq, img, ReringProject.UI.DatumImageRecordDto.ROLE_SINGLE);` — bDetectOk 와 무관하게 호출(검출 실패여도 저장, per 요구 A). (b) CaptureAndStoreCrossZDatumImage 의 `parentSeq.StoreCrossZImage(roleKey, capturedImage);` 다음·`SafeDisposeImage(capturedImage);` 앞에서 bIsRoleA 로 if/else 해 szArchiveRole = ROLE_HORIZONTAL(역할 A=가로, 기존 규약 keyA→Horizontal) / ROLE_VERTICAL 결정 후 ArchiveDatumImageForCycle 호출. TryReDetectCrossZDatumFromStore(재검출)·캐시 skip·정적 두 장 경로(TryLoadStaticDualDatumImages)에서는 호출하지 않는다(새로 찍은 사진이 아님). ReringProject.UI 는 using 추가 대신 정규화 이름으로 참조. 기존 `??` 가 많은 파일이지만 새 줄에는 `??`/삼항 금지.
  </action>
  <verify>
    <automated>cd /c/code/DataMeasurement && D="$(git diff -U0 -- WPF_Example/UI/ViewModel/CycleResultDto.cs WPF_Example/Utility/CaptureImageSaveService.cs WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs | grep '^+' | grep -v '^+++')"; echo "tern=$(echo "$D" | grep -cE '\?[^\?]*:') coal=$(echo "$D" | grep -cF '??') nullc=$(echo "$D" | grep -cF '?.') swx=$(echo "$D" | grep -cE 'switch.*=>') hbk=$(echo "$D" | grep -cF 'hbk') nobrace=$(echo "$D" | grep -cE '^\+\s*(if\s*\(.*\)|else)\s+[^{/ ].*;\s*$')"; echo "attach=$(grep -c 'DatumImages = TakeTickDatumImagesSnapshot' WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs) clear=$(grep -c 'ClearTickDatumImages();' WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs) hooks=$(grep -c 'ArchiveDatumImageForCycle(datum' WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs)"; "C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe" WPF_Example/DatumMeasurement.csproj -p:Configuration=Release -p:Platform=x64 -t:Build -v:m 2>&1 | grep -E 'error (CS|MC)' | wc -l</automated>
  </verify>
  <done>하드룰 grep 6종 전부 0, attach=2, clear=1, hooks=2, 빌드 error CS/MC 0. 자동 사이클 기준점 tick 에서만 datum_ 사진이 original 폴더로 비동기 저장되고 그 tick cycle.json 의 DatumImages 에 역할별로 기록된다. 커밋: 이 4개 파일만 명시 스테이징.</done>
</task>

<task type="auto">
  <name>Task 2: [B] 저장 cycle.json → 부품 단위 재검사 계획(SavedCycleRerunPlanner)</name>
  <files>WPF_Example/Custom/Sequence/Inspection/RepeatRunService.cs</files>
  <action>
RepeatRunService 클래스 **아래**, 같은 파일·같은 namespace(ReringProject.Sequence) 에 Allman 스타일로 추가(새 .cs 금지). 필요한 using(System.IO, System.Linq 불필요 — 루프로 작성) 만 추가.

모델: `SavedCycleDualPhoto`(ShotName, FAIName, MeasurementName, HorizontalPath, VerticalPath), `SavedCycleRerunPart`(StartTime, IndexNumber(기준점 tick dto 의 IndexNumber), List<CycleResultDto> Ticks, Dictionary<string,string> DatumPhotoPaths(키 = 기준점이름 + "|" + Role, 정적 헬퍼 BuildDatumRoleKey), Dictionary<string,string> ShotPhotoPaths(ShotName→경로), List<SavedCycleDualPhoto> DualPhotos), `SavedCycleRerunPlan`(SequenceName, RecipeName, List<SavedCycleRerunPart> Parts, int AutoTickCount, int ExcludedPartCount, Dictionary<string,int> ExclusionCounts, List<string> ExclusionDetails, `public string BuildExclusionSummary()` → "제외 M개: 사유A 3, 사유B 1" / 0개면 "제외 0개").

`public static class SavedCycleRerunPlanner` 의 `public static SavedCycleRerunPlan BuildPlan(DateTime dtFrom, DateTime dtTo, string szRecipeName, InspectionSequence seq, InspectionRecipeManager recipeManager)`: 인자 null/기간 역전이면 빈 계획. 사유 문자열은 const(REASON_NO_DATUM_TICK="기준점 촬영 없이 시작된 사이클", REASON_NO_DATUM_PHOTO="기준점 사진 없음(저장 기능 이전 사이클)", REASON_DATUM_ROLE_MISSING="기준점 사진 일부 없음", REASON_NO_SHOT="측정 사진 없음", REASON_SHOT_MISSING="Shot 사진 누락", REASON_DUAL_MISSING="두 장짜리 측정 사진 없음", REASON_FILE_MISSING="사진 파일 없음"), 폴더 패턴 "*_cycle"/파일명 "cycle.json" 도 const.
① 수집: dtFrom.Date~dtTo.Date 날마다 Path.Combine(SystemHandler.Handle.Setting.ResultSavePath, d.ToString("yyyyMMdd")) 가 있으면 Directory.GetDirectories(.., "*_cycle") 의 cycle.json 을 CycleResultSerializer.Load. 채택 조건(이름 있는 bool 로): dto != null && IsProtocolDriven && ZIndex >= 0 && RecipeName 이 szRecipeName 과 Ordinal 일치 && 이 시퀀스 tick(dto.Shots 중 OwnerSequenceName(빈값→SequenceHandler.SEQ_TOP) 이 seq.Name 과 OrdinalIgnoreCase 일치하는 게 하나라도). 폴더 단위 예외는 격리(bare catch 후 다음). AutoTickCount 기록. InspectionTime 오름차순 정렬(List.Sort + 비교 메서드).
② 부품 묶기(per 요구 B): nDatumZ = seq.GetDatumZIndex(); dto.ZIndex == nDatumZ 인 tick 이 새 부품 시작, 다음 기준점 tick 전까지가 한 부품. 첫 기준점 tick 이전 tick 들은 부품이 아님 → 있으면 ExclusionCounts[REASON_NO_DATUM_TICK] 에 tick 수가 아니라 1건으로 가산하고 상세에 개수 기록.
③ 부품별 사진 채우기: (a) 기준점 — 부품 tick 들의 DatumImages 를 시간순으로 보며 (DatumName, Role) 첫 등장만 채택(기준점 tick 사진 우선, 실패 재시도 tick 사진 무시). (b) Shot — 각 tick 에서 "처리된 Shot"(dto.MeasuredShotNames 가 비어있지 않으면 그 목록, 비어있으면 FillTickSummary 규칙: 측정 중 LastHasResult 또는 (LastSkipReason 비어있지 않고 CROSS_Z_INCOMPLETE 아님)) 만 대상, 그 ShotResultDto 에서 처리된 측정이 있고 IsDualImage=false 인 측정을 가진 첫 FAI 의 OriginImageFileName, 없으면 처리된 측정이 있는 첫 FAI 의 OriginImageFileName. Shot 이름 첫 등장만 채택. (c) 두 장짜리 — 현재 레시피에서 seq 소유 Shot(InspectionSequence.IsShotOwnedBySequence) 의 FAI 측정 중 DualImageEdgeDistanceMeasurement 이고 ZIndexA/B 둘 다 -1 아님인 것마다: 측정 키 이름 = MeasurementName 빈값이면 TypeName(BuildDto 규칙); 부품 안 dto.ZIndex == ZIndexA tick 의 같은 ShotName/FAIName/측정명 dto 측정이 "그 tick 에 처리됨"(LastHasResult 또는 LastSkipReason 비어있지 않음 — CROSS_Z_INCOMPLETE 포함) 이면 그 FAI 의 OriginImageFileName = 가로, ZIndexB tick 에서 같은 방식 = 세로. ZIndexA/B 미설정(정적) 측정은 교체 대상 아님.
④ 검증(부품 제외 규칙, 사유 1개로 집계 + 상세 문자열 기록): DatumPhotoPaths 가 비었으면 REASON_NO_DATUM_PHOTO(옛 사이클 — 기본 제외). 필요 기준점 역할 = seq.DatumConfigs 마다 VerticalTwoHorizontalDualImage 이면서 ZIndexA/B 둘 다 설정 → H,V / VerticalTwoHorizontalDualImage 정적 → 없음(현재 티칭 경로 유지) / 그 외 → ROLE_SINGLE; 하나라도 없으면 REASON_DATUM_ROLE_MISSING. ShotPhotoPaths 비었으면 REASON_NO_SHOT. 기대 Shot 집합 = 모든 후보 부품 ShotPhotoPaths 키의 합집합 — 빠진 Shot 이 있으면 REASON_SHOT_MISSING(부품마다 같은 개수로 비교되게, 누락 Shot 이 옛 사진으로 섞이는 것 방지). ③(c) 대상 측정인데 가로/세로 중 하나라도 없으면 REASON_DUAL_MISSING. 채택된 모든 경로에 File.Exists — 하나라도 없으면 REASON_FILE_MISSING. 통과 부품만 Parts, 나머지 ExcludedPartCount++. 
⑤ 계획 요약을 Logging.PrintLog((int)ELogType.Trace, "[Rerun] ...") 한 줄(시퀀스/레시피/자동 tick 수/부품 수/제외 요약).
재량 결정(기록): 요구 B 의 선택 옵션 "현재 기준점 사진으로 대체(참고용)" 는 넣지 않는다 — 기준점이 부품마다 좌표 원점을 다시 잡는 입력이라 다른 부품 사진과 섞으면 결과가 비교 불가한 숫자가 되고, 옵션 UI/분기가 늘어 과하다. 이 근거를 SUMMARY 에 적는다.
  </action>
  <verify>
    <automated>cd /c/code/DataMeasurement && D="$(git diff -U0 -- WPF_Example/Custom/Sequence/Inspection/RepeatRunService.cs | grep '^+' | grep -v '^+++')"; echo "tern=$(echo "$D" | grep -cE '\?[^\?]*:') coal=$(echo "$D" | grep -cF '??') nullc=$(echo "$D" | grep -cF '?.') swx=$(echo "$D" | grep -cE 'switch.*=>') hbk=$(echo "$D" | grep -cF 'hbk') nobrace=$(echo "$D" | grep -cE '^\+\s*(if\s*\(.*\)|else)\s+[^{/ ].*;\s*$')"; echo "planner=$(grep -c 'public static class SavedCycleRerunPlanner' WPF_Example/Custom/Sequence/Inspection/RepeatRunService.cs) build=$(grep -c 'public static SavedCycleRerunPlan BuildPlan' WPF_Example/Custom/Sequence/Inspection/RepeatRunService.cs) datumz=$(grep -c 'GetDatumZIndex()' WPF_Example/Custom/Sequence/Inspection/RepeatRunService.cs)"; "C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe" WPF_Example/DatumMeasurement.csproj -p:Configuration=Release -p:Platform=x64 -t:Build -v:m 2>&1 | grep -E 'error (CS|MC)' | wc -l</automated>
  </verify>
  <done>하드룰 grep 6종 0, planner=1 build=1 datumz>=1, 빌드 error CS/MC 0. BuildPlan 이 부품별 기준점(역할별)/Shot/두 장짜리(가로=ZIndexA tick, 세로=ZIndexB tick) 사진 경로와 사유별 제외 개수를 돌려준다. 기존 RepeatRunService 동작 무변경. 커밋: 이 파일만.</done>
</task>

<task type="auto">
  <name>Task 3: [C] 재검사 실행 — 메모리 경로 교체 + 모든 종료 경로에서 복원 + 저장 차단/종료 훅</name>
  <files>WPF_Example/Custom/Sequence/Inspection/RepeatRunService.cs, WPF_Example/MainWindow.xaml.cs</files>
  <action>
RepeatRunService 클래스 안에 새 멤버 추가(기존 Start/StartFromImages/Stop/ApplyCurrentImage/TriggerNext 동작 무변경).
(0) 순수 이동 리팩터: HandleFinish 의 "소유 shot 종합판정 → BuildDto" 구간(seqRef 로컬 고정 ~ BuildDto 대입까지)을 `private CycleResultDto BuildRunCycleDto(InspectionRecipeManager recipeManager, InspectionSequence seqRef, int nIndexNumber)` 로 추출하고 HandleFinish 는 MaterialIndexNumber 로 호출 — 로직·로그 문구 한 글자도 바꾸지 않는다(기존 WR-01/WR-02 주석은 함께 이동).
(1) 정적 상태: `private static readonly object s_savedCycleLock = new object(); private static RepeatRunService s_activeSavedCycleRun;` `public static bool IsSavedCycleRerunActive` (락 안에서 != null). `public static void RestoreActiveSavedCycleOverridesForShutdown()` — 활성 인스턴스가 있으면 EndSavedCycleRerun("프로그램 종료") 호출(상태 무관 강제 복원 — Release 의 Setting.Save 전에 OfflineInspectMode 원복 목적).
(2) 이벤트: `public event Action<List<CycleResultDto>, string> OnSavedCycleRerunEnded;` (reason null = 정상 완료, 수집 목록은 복사본). 진행은 기존 OnProgressChanged(완료 부품 수, 전체 부품 수) 재사용. 이벤트 호출은 `?.Invoke` 대신 로컬 복사 + null 체크, **_lock 밖에서** 발화.
(3) 스냅샷(private 중첩 클래스 SavedCycleOverrideSnapshot): seq 소유 ShotConfig → SimulImagePath, seq.DatumConfigs → TeachingImagePath/TeachingImagePath_Vertical, 소유 Shot 의 모든 DualImageEdgeDistanceMeasurement → TeachingImagePath_Horizontal/_Vertical(Dictionary 키 = 객체 참조), bOfflineBefore = SystemSetting.Handle.OfflineInspectMode, bOfflineSetByRerun, szRecipeName = CurrentRecipeName, 소유 Shot 참조 목록(레시피 재로드 감지용).
(4) `public bool StartFromSavedCycles(InspectionSequence seq, SavedCycleRerunPlan plan, out string szError)` — UI 스레드 호출 전제. 거부(szError 채우고 false): IsRunning, 다른 재검사 활성(s_activeSavedCycleRun != null), seq/plan null, plan.Parts 0개, seq.State != Idle("시퀀스가 실행 중"), RecipeManager null. 통과 시: 스냅샷 → OfflineInspectMode 가 false 면 메모리에서만 true + bOfflineSetByRerun=true(Setting.Save 금지) → s_activeSavedCycleRun=this, IsRunning=true, TargetCount=Parts.Count, CompletedCount=0, _collected 새로 → seq.OnFinish/OnStop/OnError 에 새 핸들러 구독(기존 _onFinishHandler 와 별도 필드) → TriggerNextSavedCyclePart().
(5) `TriggerNextSavedCyclePart()` — 기존 TriggerNext 와 같은 Idle 대기 패턴(Idle 이면 Dispatcher.BeginInvoke(Background), 아니면 Task.Delay(SAVED_CYCLE_IDLE_POLL_MS=50 const) 재시도). 디스패처 콜백 안(UI 스레드)에서: 종료됨/중단요청이면 return(중단요청이면 EndSavedCycleRerun(요청 사유)); `FindSavedCycleAbortReason()` 이 사유를 돌려주면 EndSavedCycleRerun(사유) — 사유: OfflineInspectMode 가 false("PLC 자동 검사 수신으로 오프라인 모드가 꺼짐 — 재검사 중단"), CurrentRecipeName 변경 또는 스냅샷 Shot 이 RecipeManager.Shots 에 더는 없음("레시피가 바뀌어 재검사 중단"); 아니면 ApplySavedCyclePart(Parts[CompletedCount]) → seq.ClearDatumTransforms()(Test Find 유지·이전 부품 기준점 캐시 해제 — 부품마다 새로 찾기) → StartAll(null) 이 false 면 EndSavedCycleRerun("시퀀스 시작 실패").
(6) `ApplySavedCyclePart(part)` — 먼저 스냅샷 값으로 전부 되돌린 뒤 part 값을 덮어쓴다: ShotPhotoPaths 의 Shot(이름 일치 소유 Shot).SimulImagePath, DatumPhotoPaths(ROLE_SINGLE/H → TeachingImagePath, V → TeachingImagePath_Vertical), DualPhotos(Shot/FAI/측정명 일치 측정의 TeachingImagePath_Horizontal/_Vertical 을 직접 대입 — ShotConfig.SetDualImagePathForMeasurements 는 SimulImagePath 까지 바꾸므로 쓰지 않는다). 레시피 저장 호출 금지.
(7) `HandleSavedCycleFinish(SequenceContext ctx)` (OnFinish, 시퀀스 스레드): lock(_lock) 안에서 — 이미 종료면 return; seq.IsProtocolDrivenCycle() 이면 결과 버리고 종료 사유 "PLC 자동 검사가 들어와 재검사 중단"; OfflineInspectMode 가 false 여도 결과 버리고 같은 계열 사유로 종료; 중단요청이면 결과 버리고 요청 사유로 종료; 정상이면 BuildRunCycleDto(rm, seq, part.IndexNumber) → dto.Shots 와 dto.MeasuredShotNames 에서 part.ShotPhotoPaths 에 없는 Shot 제거(레시피에만 있고 자동 사이클에 없던 Shot 이 옛 사진 값으로 섞이는 것 방지) → CycleResultSerializer.SaveAsync(dto)(기존과 동일 — CSV 에 '수동' 행, 통계 기본 조회 제외 규칙 그대로, per FIA-E) → _collected.Add → CompletedCount++ → 락 밖에서 진행 이벤트 → 다 끝났으면 EndSavedCycleRerun(null) 아니면 TriggerNextSavedCyclePart(). OnStop/OnError 핸들러는 EndSavedCycleRerun("시퀀스가 중단/오류로 멈춤").
(8) `public void RequestStopSavedCycleRerun(string szReason)` — 중단요청 플래그+사유 기록. seq.State == Idle 이면 즉시 EndSavedCycleRerun(사유), 아니면 실행 중인 부품이 끝날 때 (7) 이 종료한다 + 안전망 `WaitIdleThenEndSavedCycle()`(Task.Delay(SAVED_CYCLE_STOP_POLL_MS=100) 로 Idle 대기, SAVED_CYCLE_STOP_WAIT_MAX_MS=120000 초과 시 로그 남기고 강제 종료) — 실행 중 경로를 되돌려 한 부품 안에서 사진이 섞이는 것을 막기 위함.
(9) `EndSavedCycleRerun(string szReason)` — 멱등(락 안 bEnded 플래그, 두 번째 호출 즉시 return): 이벤트 구독 해제 → RestoreSavedCycleOverrides()(모든 스냅샷 경로 원복; OfflineInspectMode 는 bOfflineSetByRerun 이고 현재 true 일 때만 false 로 — PLC 가 이미 끈 경우·원래 true 였던 경우는 그대로) → IsRunning=false, s_activeSavedCycleRun=null → Trace 로그 "[Rerun] 종료 — 완료 N/M, 사유" → 락 밖에서 OnSavedCycleRerunEnded(복사본, 사유). 복원 코드는 try/catch 로 감싸 로그만 남기고 계속(한 항목 실패가 나머지 복원을 막지 않게).
(10) MainWindow.xaml.cs (K&R, 배선만): SaveRecipe 의 Running 가드 바로 다음에 `if (RepeatRunService.IsSavedCycleRerunActive) { CustomMessageBox.Show("저장 불가", "저장 사진 재검사 중에는 레시피를 저장할 수 없습니다. 재검사가 끝나거나 중단된 뒤 저장하세요.", MessageBoxImage.Warning); return; }` (중괄호 줄 나눔은 파일 스타일). Window_Closing 의 `mSystemHandler.Release();` 바로 앞에 `RepeatRunService.RestoreActiveSavedCycleOverridesForShutdown();`. 다른 로직 추가 금지.
  </action>
  <verify>
    <automated>cd /c/code/DataMeasurement && D="$(git diff -U0 -- WPF_Example/Custom/Sequence/Inspection/RepeatRunService.cs WPF_Example/MainWindow.xaml.cs | grep '^+' | grep -v '^+++')"; echo "tern=$(echo "$D" | grep -cE '\?[^\?]*:') coal=$(echo "$D" | grep -cF '??') nullc=$(echo "$D" | grep -cF '?.') swx=$(echo "$D" | grep -cE 'switch.*=>') hbk=$(echo "$D" | grep -cF 'hbk') nobrace=$(echo "$D" | grep -cE '^\+\s*(if\s*\(.*\)|else)\s+[^{/ ].*;\s*$') settingsave=$(echo "$D" | grep -cE 'Setting\.Save\(|SaveRecipe\(')"; F=WPF_Example/Custom/Sequence/Inspection/RepeatRunService.cs; echo "start=$(grep -c 'public bool StartFromSavedCycles' $F) restore=$(grep -c 'RestoreSavedCycleOverrides()' $F) cleardatum=$(grep -c 'ClearDatumTransforms()' $F) guard=$(grep -c 'IsSavedCycleRerunActive' WPF_Example/MainWindow.xaml.cs) shutdown=$(grep -c 'RestoreActiveSavedCycleOverridesForShutdown' WPF_Example/MainWindow.xaml.cs)"; "C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe" WPF_Example/DatumMeasurement.csproj -p:Configuration=Release -p:Platform=x64 -t:Build -v:m 2>&1 | grep -E 'error (CS|MC)' | wc -l</automated>
  </verify>
  <done>하드룰 grep 6종 0, settingsave=0(재검사 코드가 Setting/레시피를 저장하지 않음), start=1 restore>=1 cleardatum>=1 guard=1 shutdown=1, 빌드 error CS/MC 0. 완료/중단/OnStop/OnError/창 닫기/종료 모두 단일 EndSavedCycleRerun 을 거쳐 원래 경로·OfflineInspectMode 로 복원된다. 기존 폴더 반복검사(StartFromImages)·고정 반복(Start) 동작 무변경. 커밋: 이 2개 파일만.</done>
</task>

<task type="auto">
  <name>Task 4: [D] 통계 창 — "저장 사진으로 재검사" 버튼/진행/중단, 재검사 결과 표(원래 평균·변화), 원래 통계로, CPK export 분기</name>
  <files>WPF_Example/UI/Statistics/StatisticsWindow.xaml.cs, WPF_Example/UI/Statistics/StatisticsWindow.xaml</files>
  <action>
StatisticsWindow.xaml.cs (Allman, 같은 파일에 클래스 추가 — 새 파일 금지):
(1) StatRow 에 OriginalMeanText(string), DeltaText(string), DeltaSortValue(double) 추가. StatRowPresenter 에 `public static void FillRerunComparison(List<StatRow> rows, Dictionary<string, MeasurementStat> originalStats)` — 행 Key 로 원래 통계 조회, 원래 N>0 이고 행 N>0 이면 OriginalMeanText=원래 Mean.ToString(VALUE_FORMAT), 변화 d = 행 Mean − 원래 Mean, DeltaText = 양수면 "+" 접두 + F4, 0/음수는 ToString(F4) 그대로(if/else), DeltaSortValue = Math.Abs(d)(가장 많이 변한 항목 정렬용 — 선택 근거 주석); 아니면 "-" / "-" / NO_ORIGINAL_SORT(-1.0 const). 기존 BuildRows/SortWorstFirst/BuildSummary 재사용(색/정렬/벗어난 양/요약 동일).
(2) `public class StatisticsRerunViewModel : INotifyPropertyChanged` — 바인딩 프로퍼티(변경 알림): SequenceNames(List<string>), SelectedSequenceName, StatusText, IsRerunning, CanStartRerun(= !IsRerunning, 함께 알림), IsShowingRerun. 비바인딩: RerunRows(List<StatRow>), RerunResult(StatisticsQueryResult), RerunSummaryText, RerunCycles(List<CycleResultDto>), RerunRecipeName, 현재 SavedCycleRerunPlan. 이벤트 `public event Action RerunViewReady;`. 메서드:
 - `LoadSequenceNames()` — SystemHandler.Handle.Sequences 를 돌며 InspectionSequence 이고 RecipeManager.Shots 중 IsShotOwnedBySequence 인 Shot 이 1개 이상인 이름만, 첫 항목 선택.
 - `public bool TryStartRerun(DateTime dtFrom, DateTime dtTo, string szRecipeFilter, out string szError)` — 동기 거부: 실행 중, 시퀀스 미선택/미발견, 현재 불러온 레시피(Setting.CurrentRecipeName) 비어있음, szRecipeFilter 가 비어있지 않고 현재 레시피와 다름("재검사는 지금 불러온 레시피 파라미터로 돕니다. 레시피 콤보를 '<현재>' 또는 '전체' 로 두세요"), 시퀀스 State != Idle. 통과 시 IsRerunning=true, StatusText="저장 사이클 읽는 중" → Task.Run 으로 SavedCycleRerunPlanner.BuildPlan(기간, 현재 레시피, seq, RecipeManager) + 원래 통계 MeasurementHistoryCsvLoader.Query(기간, 현재 레시피)(= 재검사 직전 조회 결과, 레시피 '전체' 선택 시 다른 레시피 값이 섞이지 않게 레시피를 명시) → Application.Current.Dispatcher.BeginInvoke 로 UI 스레드 복귀 → 부품 0개면 StatusText="재검사할 부품 없음 — " + plan.BuildExclusionSummary(), IsRerunning=false; 아니면 새 RepeatRunService 생성·OnProgressChanged/OnSavedCycleRerunEnded 구독(둘 다 Dispatcher.BeginInvoke 로 마샬 — Invoke 동기 금지, 데드락 방지) → StartFromSavedCycles(seq, plan, out err) 실패 시 StatusText=err, IsRerunning=false. 진행 중 StatusText = "재검사 중 k/N 부품 (" + 제외 요약 + ")".
 - 종료 처리(OnSavedCycleRerunEnded, UI 스레드): IsRerunning=false. 수집 0개면 StatusText="재검사 결과 없음 — " + 사유, IsShowingRerun 변경 없음. 있으면 RepeatMeasurementStats 로 AddSample 전부 → ComputeAll/GetSeries → RerunResult = new StatisticsQueryResult { Stats, Series, RecipeNames=[레시피], TotalRowCount = 사이클 수 } → rows=BuildRows → FillRerunComparison(rows, 원래 Stats) → SortWorstFirst → RerunSummaryText=BuildSummary(rows) → StatusText = "재검사 결과 — 부품 " + 완료 수 + "개(" + 제외 요약 + ")" + 사유 있으면 " · 중단: " + 사유 → IsShowingRerun=true → RerunViewReady 발화.
 - `RequestStop()` → 서비스.RequestStopSavedCycleRerun("사용자 중단"). `ReturnToOriginal()` → 실행 중이 아니면 IsShowingRerun=false, StatusText="". `OnWindowClosing()` → 실행 중이면 RequestStopSavedCycleRerun("통계 창 닫힘")(서비스가 스스로 끝까지 복원).
 - Export: `List<CycleResultDto> GetCyclesForExport(DateTime, DateTime, string szRecipeFilter)` → IsShowingRerun 이면 RerunCycles, 아니면 MeasurementHistoryCsvLoader.QueryCycles(기존 동작). `string GetExportRecipeName(string szRecipeFilter, string szAllLabel)` → 재검사면 RerunRecipeName, 아니면 기존 규칙(빈값→szAllLabel). `string GetExportFilePrefix()` → 재검사면 "cpk_rerun_", 아니면 "cpk_report_".
(3) code-behind 배선(각 핸들러 1~3줄, 새 계산 로직 금지): 필드 m_rerunVm; 생성자에서 생성 → pnl_Rerun.DataContext = m_rerunVm → RerunViewReady += ApplyRerunView → LoadSequenceNames(). Btn_Rerun_Click: GetSelectedRange/GetSelectedRecipeFilter 후 TryStartRerun, 실패 시 CustomMessageBox.Show("저장 사진으로 재검사", err, MessageBoxImage.Warning). Btn_RerunStop_Click → RequestStop(). Btn_BackToOriginal_Click → ReturnToOriginal(); SetRerunColumnsVisible(false); DoQuery(GetSelectedRecipeFilter()). Btn_Query_Click 에도 조회 전에 ReturnToOriginal(); SetRerunColumnsVisible(false); 추가. ApplyRerunView(): m_lastResult = vm.RerunResult; grid_Stats.ItemsSource = vm.RerunRows; ApplyProblemFilter(); txt_Summary.Text = vm.RerunSummaryText; ClearCharts(); SetRerunColumnsVisible(true); UpdateExportButtonState(); (차트는 m_lastResult.Series 그대로 재사용). SetRerunColumnsVisible(bool) — col_OriginalMean/col_Delta Visibility 만 토글. Window_Closing → m_rerunVm.OnWindowClosing(). Btn_CpkExport_Click 은 QueryCycles 호출/레시피명/파일명 접두 3곳만 VM 메서드로 교체(나머지 흐름·메시지 그대로).
(4) StatisticsWindow.xaml: Window 에 Closing="Window_Closing", Window.Resources 에 BooleanToVisibilityConverter(x:Key="BoolToVis"). Row0 필터바 StackPanel 에 세 번째 가로 StackPanel x:Name="pnl_Rerun"(Margin 0,6,0,0): TextBlock "재검사 시퀀스", ComboBox combo_RerunSeq(ItemsSource=SequenceNames, SelectedItem=SelectedSequenceName Mode=TwoWay, IsEnabled=CanStartRerun, Width 110), Button btn_Rerun "저장 사진으로 재검사"(Click, IsEnabled=CanStartRerun), Button btn_RerunStop "중단"(Click, IsEnabled=IsRerunning), Button btn_BackToOriginal "원래 통계로"(Click, Visibility=IsShowingRerun+BoolToVis), TextBlock txt_RerunStatus(Text=StatusText, SemiBold, 색 #1D4ED8). 기존 버튼 스타일(Padding 14,4 / Margin 8,0,0,0) 따름. DataGrid "평균" 칸 바로 뒤에 `DataGridTextColumn x:Name="col_OriginalMean" Header="원래 평균" Binding=OriginalMeanText Width=80 Visibility="Collapsed"`, `x:Name="col_Delta" Header="변화" Binding=DeltaText SortMemberPath="DeltaSortValue" Width=80 Visibility="Collapsed"`. 기존 칸·RowStyle·안내 문구 무변경.
  </action>
  <verify>
    <automated>cd /c/code/DataMeasurement && D="$(git diff -U0 -- WPF_Example/UI/Statistics/StatisticsWindow.xaml.cs | grep '^+' | grep -v '^+++')"; echo "cs: tern=$(echo "$D" | grep -cE '\?[^\?]*:') coal=$(echo "$D" | grep -cF '??') nullc=$(echo "$D" | grep -cF '?.') swx=$(echo "$D" | grep -cE 'switch.*=>') hbk=$(echo "$D" | grep -cF 'hbk') nobrace=$(echo "$D" | grep -cE '^\+\s*(if\s*\(.*\)|else)\s+[^{/ ].*;\s*$') syncinvoke=$(echo "$D" | grep -cE 'Dispatcher\.Invoke\(')"; X="$(git diff -U0 -- WPF_Example/UI/Statistics/StatisticsWindow.xaml | grep '^+' | grep -v '^+++')"; echo "xaml: hbk=$(echo "$X" | grep -cF 'hbk') rerun=$(echo "$X" | grep -c 'btn_Rerun') cols=$(echo "$X" | grep -cE 'col_OriginalMean|col_Delta') back=$(echo "$X" | grep -c 'btn_BackToOriginal')"; echo "vm=$(grep -c 'public class StatisticsRerunViewModel' WPF_Example/UI/Statistics/StatisticsWindow.xaml.cs) cmp=$(grep -c 'public static void FillRerunComparison' WPF_Example/UI/Statistics/StatisticsWindow.xaml.cs) exp=$(grep -c 'GetCyclesForExport(' WPF_Example/UI/Statistics/StatisticsWindow.xaml.cs)"; "C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe" WPF_Example/DatumMeasurement.csproj -p:Configuration=Release -p:Platform=x64 -t:Build -v:m 2>&1 | grep -E 'error (CS|MC)' | wc -l</automated>
  </verify>
  <done>하드룰 grep 6종 0, syncinvoke=0, xaml rerun>=1 cols=2 back>=1, vm=1 cmp=1 exp>=2, 빌드 error CS/MC 0. 통계 창에서 재검사 시작·진행·중단·결과표(원래 평균/변화)·원래 통계로 복귀·보고 있는 쪽 기준 CPK export 가 동작하도록 배선됨. 기존 조회/차트/필터/색 동작 무변경. 최종 확인: `git diff --name-only` 기준 이번 작업 커밋 파일이 files_modified 8개뿐이고 csproj 미포함. 커밋: 이 2개 파일만.</done>
</task>

<task type="checkpoint:human-verify" gate="non-blocking">
  <name>Task 5: 실기 확인 — 기준점 사진 저장 / 통계 창 재검사 / 원복</name>
  <files>.planning/quick/260911-fia-saved-cycle-rerun/260911-fia-SUMMARY.md</files>
  <action>
이 체크포인트는 실행을 막지 않는다. Task 1~4 코드·빌드·커밋이 끝나면 실행자는 대기하지 않고 아래 how-to-verify 항목을 SUMMARY 의 "실기 UAT 대기" 체크리스트(미체크)로 옮겨 적고, 재량 결정(참고용 대체 옵션 생략 근거, 변화 칸 정렬=절댓값, 원래 평균=같은 기간·현재 레시피 CSV 조회)과 잔여 위험(재검사 중 레시피 로드/설정창 저장 시 OfflineInspectMode ON 이 파일에 남을 수 있음 — PLC $TEST 강제 OFF 가 자동 검사 보호, 같은 초에 두 시퀀스 cycle.json 폴더가 겹치면 한 tick 이 덮여 그 부품이 제외될 수 있음)을 함께 기록한 뒤 반환한다. 코드 파일은 수정하지 않는다.
  </action>
  <what-built>
A: PLC 자동 사이클 기준점 tick 에서 datum_ 사진 저장 + cycle.json DatumImages. B/C: 저장 사이클을 부품 단위로 묶어 사진 경로를 메모리에서만 바꿔 재검사하고 반드시 원복. D: 통계 창 재검사 버튼/진행/중단/결과표(원래 평균·변화)/원래 통계로/CPK export.
  </what-built>
  <how-to-verify>
1. PLC 자동 1사이클(TOP: 기준점 z=1 + Shot 2~4) 후 D:\Data\Result\Image\<yyMMdd>\<HHmm>\original 에 datum_TOP_<기준점이름>_<시각>.jpg(설정이 BMP 면 .bmp) 가 생기는지. 그 z=1 tick 의 D:\Data\Result\<yyyyMMdd>\<HHmmss>_cycle\cycle.json 에 "DatumImages": [{ "DatumName", "Role": "", "Path" }] 가 있고, z=2~4 tick 은 빈 목록인지. BOTTOM(기준점 z=11)도 동일. 자동 검사 tact 가 체감상 그대로인지(로그 [CaptureSave] datum_ 줄 확인).
2. 수동 RUN 한 번 → 새 datum_ 파일이 생기지 않는지.
3. TOP C1_P1 파라미터 변경(레시피 저장은 하지 않아도 됨) → 통계 창 → 기간 오늘, 레시피 전체 또는 FAI_1, 재검사 시퀀스 TOP → "저장 사진으로 재검사". 진행 "재검사 중 k/N 부품", 끝나면 상단 "재검사 결과 — 부품 N개(제외 M개: 사유)". 오늘 이전 사이클(기준점 사진 없음)은 제외 사유로 잡히는지.
4. 결과 표: 색/나쁜 순 정렬/벗어난 양/요약이 기존과 같은 방식이고 "원래 평균"·"변화" 칸이 보이는지, C1_P1 행의 변화 값이 파라미터 변경 방향과 맞는지, 행 클릭 시 차트가 재검사 값으로 그려지는지.
5. 재검사 결과 보는 중 "CPK 리포트 export" → 파일명 cpk_rerun_... 이고 재검사 부품 수만큼 들어가는지. "원래 통계로" → 원래 표로 돌아오고 새 칸이 숨는지, 이때 export 는 cpk_report_... (원래 CSV 기준).
6. 원복: 재검사 후 TOP Shot/기준점 노드의 이미지 경로(SimulImagePath/TeachingImagePath)가 재검사 전과 같은지, 설정의 OfflineInspectMode 가 재검사 전 값인지. 재검사 도중 "중단" → 현재 부품이 끝난 뒤 멈추고 같은 원복이 되는지. 재검사 도중 레시피 저장 → "저장 사진 재검사 중에는 저장할 수 없습니다" 안내가 뜨는지.
7. (가능하면) 재검사 도중 PLC $TEST 가 오면 재검사가 "PLC 자동 검사 수신" 사유로 멈추고 자동 검사는 실물 촬영으로 정상 응답하는지.
8. BOTTOM 재검사: E5(두 장짜리) 가 부품마다 z=14 사진=가로, z=15 사진=세로로 측정되어 값이 나오는지(재검사 결과 표 E5 행 N = 부품 수).
  </how-to-verify>
  <verify>
    <automated>cd /c/code/DataMeasurement && grep -c '실기 UAT' .planning/quick/260911-fia-saved-cycle-rerun/260911-fia-SUMMARY.md</automated>
  </verify>
  <done>SUMMARY 에 8항목 실기 UAT 체크리스트(미체크) + 재량 결정 + 잔여 위험이 기록되어 있고, 실행자는 사용자 응답을 기다리지 않고 완료 반환.</done>
  <resume-signal>비차단 — SUMMARY 에 UAT 대기로 기록 후 종료. 사용자가 장비에서 확인 후 결과를 알려준다.</resume-signal>
</task>

</tasks>

<threat_model>
## Trust Boundaries

| Boundary | Description |
|----------|-------------|
| 디스크 cycle.json → 재검사 계획 | 로컬 결과 폴더의 JSON(과거 실행 산출물)에서 이미지 경로를 읽어 HALCON 이 로드한다 |
| 재검사(메모리 경로 교체) ↔ 레시피/설정 영속화 | 임시 경로·임시 OfflineInspectMode 가 레시피 INI / Setting 파일로 새어 나갈 수 있는 경계 |
| PLC TCP $TEST ↔ 재검사 | 재검사 중 자동 검사 명령이 같은 시퀀스·같은 경로 상태를 공유 |

## STRIDE Threat Register

| Threat ID | Category | Component | Disposition | Mitigation Plan |
|-----------|----------|-----------|-------------|-----------------|
| T-FIA-01 | Tampering | 레시피 INI(임시 경로 저장) | mitigate | MainWindow.SaveRecipe 에 IsSavedCycleRerunActive 가드(부품 사이 Idle 포함 차단), 재검사 코드는 SaveRecipe/Setting.Save 호출 0(Task 3 grep settingsave=0), 모든 종료 경로 단일 EndSavedCycleRerun 에서 스냅샷 원복 |
| T-FIA-02 | Tampering | Setting.OfflineInspectMode 영속화 | mitigate | 메모리에서만 ON, 종료 시 "우리가 켰고 아직 ON 일 때만" OFF 로 원복, 앱 종료는 Release(Setting.Save) 직전 RestoreActiveSavedCycleOverridesForShutdown 호출 |
| T-FIA-03 | Tampering | 재검사 중 LoadRecipe/설정창 저장이 ON 을 기록 | accept | 발생 빈도 낮음. PLC $TEST 수신 시 ForceOfflineInspectModeOffForAutoTest 가 자동 검사를 항상 실물 촬영으로 되돌려 판정 오염 없음. 레시피 변경은 다음 부품 전에 감지해 재검사 중단. SUMMARY 에 잔여 위험 기록 |
| T-FIA-04 | Tampering/Spoofing | 재검사 중 PLC 자동 사이클 결과 혼입 | mitigate | OnFinish 에서 IsProtocolDrivenCycle / OfflineInspectMode OFF 감지 시 그 결과 폐기 + 재검사 중단, 부품 시작 전 FindSavedCycleAbortReason 재확인, 경로 교체는 시퀀스 Idle + UI 스레드에서만 |
| T-FIA-05 | Tampering | 재검사 부품 내 사진 혼합(중단 시) | mitigate | 실행 중 중단 요청은 즉시 원복하지 않고 부품 종료(OnFinish/OnStop/OnError) 또는 Idle 폴링(최대 120 s) 후 원복, 중단 부품 결과는 폐기 |
| T-FIA-06 | Tampering | cycle.json 의 이미지 경로 | accept | 로컬 전용 산출물(외부 입력 아님). File.Exists 확인 후 기존 HImage 로더 경로(OfflineInspectMode 로드)만 사용, 경로를 어디에도 영속 저장하지 않음. Load 는 TypeNameHandling.None 유지 |
| T-FIA-07 | Denial of Service | 자동 검사 tact(기준점 사본 저장) | mitigate | 기준점 grab 1회당 CopyImage 1회 + 기존 비동기 CaptureImageSaveService 큐(백프레셔 기존 규칙) 재사용, 예외는 삼키고 로그만 — 판정/응답 경로 무영향 |
| T-FIA-08 | Denial of Service | 재검사 중 시퀀스 점유로 PLC $TEST 시작 실패 | accept | 기존 반복검사와 동일한 알려진 제약(STATE 이월 WR-03). 재검사는 PLC 수신 감지 즉시 중단 |
| T-FIA-09 | Tampering | 기준점 사진 HImage 누수 | mitigate | 사본은 SharedHImage(ref) 로만 보유, finally Release, 원본 img 는 기존 finally Dispose 경로 그대로 |
</threat_model>

<verification>
- 태스크별 automated verify 전부 통과(하드룰 grep 6종 0, 구조 grep 기대값, Release|x64 error CS/MC 0).
- 최종: `git diff --name-only <작업 시작 HEAD>..HEAD` = files_modified 8개(+ .planning 문서), WPF_Example/DatumMeasurement.csproj 미포함. 추가 줄 전체에 대해 grep 6종 재실행 0.
- 소스 커버리지: A→Task 1, B→Task 2, C→Task 3, D→Task 4, E→Task 1/3/4 가드(TCP·판정·ResponseQueue 무수정, HandleManualCyclePersist·CSV Writer/Loader 무수정, 기존 StartFromImages/Start 무변경), 실기 확인→Task 5.
</verification>

<success_criteria>
- 자동 사이클 기준점 tick 마다 datum_ 사진 + DatumImages 기록(수동/SIMUL/오프라인/캐시 tick 은 없음).
- 통계 창에서 한 번의 클릭으로 오늘 자동 부품을 새 파라미터로 재검사, 부품 수/제외 수·사유, 원래 평균·변화, CPK export(보고 있는 쪽) 확인 가능.
- 재검사 전후 레시피 경로·OfflineInspectMode 동일(모든 종료 경로), 재검사 중 레시피 저장 차단.
- 빌드 통과, 하드룰 위반 0, 새 .cs 파일 0.
</success_criteria>

<output>
Create `.planning/quick/260911-fia-saved-cycle-rerun/260911-fia-SUMMARY.md` when done (실기 UAT 대기 체크리스트 + 재량 결정 + 잔여 위험 포함). STATE.md 의 quick 표에 260911-fia 행 추가.
</output>
