---
phase: quick-260806-dsn
plan: 01
type: execute
wave: 1
depends_on: []
files_modified:
  - WPF_Example/SystemHandler.cs
  - WPF_Example/Custom/Sequence/Inspection/ShotConfig.cs
  - WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs
  - WPF_Example/UI/ContentItem/MainView.xaml.cs
  - WPF_Example/UI/ControlItem/InspectionListView.xaml.cs
autonomous: false
requirements: [BATCH-MEM-01]

must_haves:
  truths:
    - "앱 시작 시 HALCON 캐시(global_mem_cache/temporary_mem_cache/image_cache_capacity)가 idle/0 으로 설정된다"
    - "BOTTOM 시퀀스 30개 항목 일괄검사 1사이클 완료 후 앱이 Idle 상태에서 메모리가 수 GB대가 아니라 수백 MB대로 감소한다"
    - "사이클 종료 직후 현재 화면에 표시 중이던 노드는 이미지가 그대로 보인다 (회귀 없음)"
    - "사이클 종료 후 다른 Shot/FAI/Measurement 노드를 클릭하면 디스크 폴백을 통해 이미지+overlay가 정상 표시된다"
    - "정리된 Shot을 다시 검사(재실행)해도 NullReferenceException/크래시 없이 정상 동작한다"
    - "단일 RUN(Btn_start_Click)과 사이클 진행 도중에는 어떤 이미지도 조기에 Dispose 되지 않는다"
  artifacts:
    - path: "WPF_Example/SystemHandler.cs"
      provides: "Initialize() 진입부 HALCON SetSystem 캐시 설정 3줄"
      contains: "global_mem_cache"
    - path: "WPF_Example/Custom/Sequence/Inspection/ShotConfig.cs"
      provides: "ResolveFallbackImagePath() — _image 정리 후 디스크 재현 가능 여부 판단"
      contains: "ResolveFallbackImagePath"
    - path: "WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs"
      provides: "ClearCrossZImagesAfterBatchCycle() — 사이클 종료 후 크로스-Z 저장소 정리 진입점"
      contains: "ClearCrossZImagesAfterBatchCycle"
    - path: "WPF_Example/UI/ContentItem/MainView.xaml.cs"
      provides: "DisplayShotImage 디스크 폴백 분기 — 정리된 Shot 재클릭 시 안전망"
      contains: "ResolveFallbackImagePath"
    - path: "WPF_Example/UI/ControlItem/InspectionListView.xaml.cs"
      provides: "CleanupBatchImageMemoryAfterCycle + ResolveCurrentlyDisplayedShot — 배치 완료 훅"
      contains: "CleanupBatchImageMemoryAfterCycle"
  key_links:
    - from: "SystemHandler.Initialize() 진입부"
      to: "HOperatorSet.SetSystem 3콜"
      via: "try/catch (Sequences/Devices 초기화보다 먼저 실행)"
      pattern: "SetSystem\\(\"global_mem_cache\""
    - from: "BatchRunService.OnBatchComplete 이벤트"
      to: "InspectionListView.OnBatchComplete 핸들러"
      via: "기존 이벤트 구독 (Btn_batchInspect_Click, 무변경)"
      pattern: "_batchService.OnBatchComplete \\+= OnBatchComplete"
    - from: "InspectionListView.OnBatchComplete"
      to: "CleanupBatchImageMemoryAfterCycle(_batchShots)"
      via: "Dispatcher.Invoke 내부 호출 (UI 스레드, 사이클당 1회)"
      pattern: "CleanupBatchImageMemoryAfterCycle\\(_batchShots\\)"
    - from: "CleanupBatchImageMemoryAfterCycle"
      to: "ShotConfig.ClearImage / ResolveFallbackImagePath / InspectionSequence.ClearCrossZImagesAfterBatchCycle"
      via: "기존/신규 헬퍼 재사용 (락 우회 없음)"
      pattern: "shot\\.ClearImage\\(\\)"
    - from: "MainView.DisplayShotImage (else 분기)"
      to: "ShotConfig.ResolveFallbackImagePath"
      via: "디스크 재로드 폴백 (FAI 원본 캡쳐 파일)"
      pattern: "shot\\.ResolveFallbackImagePath\\(\\)"
---

<objective>
일괄검사(BatchRunService) 1사이클이 끝난 뒤에도 메모리가 30GB+까지 누적되고 절대 줄어들지 않는 근본원인을 2계층으로 수정한다.

**Part A (HALCON 자체 캐시)**: `SystemHandler.Initialize()` 진입부에 HALCON 24.11 공식 문서가 권장하는 `SetSystem` 3줄을 추가해, HALCON의 mimalloc 기반 메모리 캐싱(해제된 메모리를 OS에 반환하지 않는 동작)을 완화한다. 기능/정확성 영향 없음(캐시 정책만 변경).

**Part B (앱 자체의 사이클-종료-후 이미지 보존)**: `ShotConfig._image`(Shot당 ~127MB clone), `ActionContext.ResultHalconImage`(Action당 ~127MB clone), 크로스-Z 이미지 저장소가 사이클이 끝나도 전혀 해제되지 않고 "그 Shot/Action이 다음에 다시 실행될 때"까지 살아있다. 사용자 확정 설계대로, **일괄검사 사이클이 완전히 끝나는 시점(`BatchRunService.OnBatchComplete`, 사이클당 정확히 1회 발화)**에 **현재 화면에 표시 중인 노드를 제외한 나머지 전부**의 이미지 캐시를 즉시 Dispose한다.

**CONTEXT.md 가정에 대한 중요한 정정 (코드 직접 추적으로 확인됨)**: CONTEXT.md는 "`MainView.DisplayContextToViewer`(1645-1680)에 디스크 재로드 폴백이 이미 있으니 `ResultHalconImage`만 null로 정리하면 된다"고 가정했다. 그러나 실제 코드 추적 결과, Shot/FAI/Measurement/Action 트리 노드 클릭 시 **최종적으로 화면에 보이는 이미지를 결정하는 경로는 `DisplayContextToViewer`가 아니라 `MainView.DisplayShotImage(ShotConfig shot)`이다** (`RenderInspectionResultForNode` → `DisplayFAIImage`/`DisplayMeasurementImage` → `DisplayShotImage`, 그리고 Action 노드는 `InspectionListView.xaml.cs`에서 직접 호출). `DisplayShotImage`는 오직 `shot.GetImage()`(=`ShotConfig._image`)만 사용하며 **디스크 폴백이 전혀 없다** — `_image`가 비면 곧바로 "이미지 로드 실패"/"NO Image"를 표시한다. 게다가 `Action_FAIMeasurement.cs`는 `ActionContext.ResultImagePath`를 **한 번도 설정하지 않는다**(grep 확인 — `ResultImagePath =` 대입은 `Action_TopInspection.cs`에만 있고, 이 프로젝트의 동적 FAI 검사(Top/Side/Bottom 전부)는 `Action_FAIMeasurement`를 쓴다). 즉 CONTEXT.md가 가정한 "이미 있는 폴백"은 이번 시나리오(BOTTOM 동적 FAI 배치검사)에서 **작동하지 않는다** — 그대로 구현했다면 정리 후 다른 노드를 클릭할 때마다 빈 화면이 뜨는 회귀가 발생했을 것이다.

대신, 이미 신뢰성 있게 채워지는 기존 디스크 자산인 **`FAIConfig.LastOriginImageFileName`**(Phase 40.2 `CaptureImageSaveService`가 `AggregateFaiResult`→`QueueFaiCapture`에서 FAI마다 overlay 없는 원본을 항상 저장하고 기록하는 필드, `Action_FAIMeasurement.cs`)을 폴백 소스로 사용하고, `DisplayShotImage`에 실제로 폴백 분기를 추가한다. overlay는 이 폴백과 무관하게 `FAIConfig.LastOverlays`를 통해 항상 별도로 재현되므로(`RenderStoredOverlaysForFai`) 원본(overlay 미포함) 이미지만 있으면 충분하다.

Purpose: 실측 재현(30개 항목 Bottom 배치, 1GB→12.4GB 계단식 증가, 사이클 종료 후에도 미감소)의 근본원인 제거. 화면 표시 회귀(빈 화면) 없이 메모리만 회수.
Output: 5개 파일 수정. Debug/x64 빌드 PASS. checkpoint:human-verify로 실제 재현 시나리오 재검증.

**범위 밖 (건드리지 않음, CONTEXT.md 확정)**: `OverlayCaptureRenderer.cs`(반증된 가설), `PatternMatchService.cs`(전날 완료), `CaptureImageSaveService.cs`의 `MAX_QUEUE_DEPTH` 값, DualImage 티칭 이미지 로드/해제(이미 정상), `RepeatRunService`(별도 기능, 이번 재현/검증 범위 아님 — Gage R&R 반복실행의 유사 누적은 이번 quick task 범위 밖이며 별도 확인이 필요하면 후속 작업으로 분리).
</objective>

<execution_context>
@$HOME/.claude/get-shit-done/workflows/execute-plan.md
@$HOME/.claude/get-shit-done/templates/summary.md
</execution_context>

<context>
@.planning/STATE.md
@./CLAUDE.md
@.planning/quick/260806-dsn-overlay-window-reuse/260806-dsn-CONTEXT.md

<style_rules>
프로젝트 규칙 (예외 없음):
- 삼항 연산자 `?:` 절대 금지 → if-else 만 사용.
- **날짜 프리픽스 주석(`//YYMMDD hbk`) 정책은 폐기됨(2026-06-11)** — 새로 추가/수정하는 라인에 날짜 주석을 달지 않는다. 대신 `// quick-260806-dsn: ...` 형태로 이 quick task 출처만 짧게 표기하고, 비자명한 "왜"를 한국어로 설명한다.
- C# 7.2 / .NET Framework 4.8. 로컬함수·`is` 타입 패턴(`x is Type y`)은 이미 코드베이스 전역에서 사용 중이므로 허용. switch expression / nullable ref / record / range 연산자(`..`) 금지.
- 브레이스 스타일은 파일별로 다르다 — 반드시 그 파일의 기존 스타일을 따를 것:
  - `SystemHandler.cs`: K&R (여는 브레이스 같은 줄).
  - `ShotConfig.cs`: K&R.
  - `InspectionSequence.cs`: Allman (여는 브레이스 다음 줄).
  - `MainView.xaml.cs`: K&R.
  - `InspectionListView.xaml.cs`: K&R.
- 헝가리안 표기는 점진 적용 — 기존 변수명 스타일(camelCase 지역변수, `sw`/`prev` 같은 기존 관례)을 그대로 따르고 불필요한 광범위 개명 금지.
- `HImage` Dispose는 반드시 null 대입과 짝을 이룬다(이 파일들의 기존 패턴 — `ShotConfig.ClearImage`, `ActionContext.Clear` 참고).
</style_rules>

<interfaces>
<!-- 코드베이스에서 확인된 기존 계약 — executor는 아래를 그대로 재사용한다. 코드 탐색 불필요. -->

From WPF_Example/Custom/Sequence/Inspection/ShotConfig.cs (기존, 무변경 — 그대로 재사용):
```csharp
private readonly object _imageLock = new object();
private HImage _image;
public bool HasImage { get { lock (_imageLock) { return _image != null; } } }
public void SetImage(HImage image) { /* lock(_imageLock) 내부에서 기존 dispose 후 CopyImage() 저장 */ }
public HImage GetImage() { /* lock(_imageLock) 내부에서 CopyImage() 반환, 호출자 Dispose 책임 */ }
public void ClearImage() { /* lock(_imageLock) 내부에서 dispose + null. 멱등. 이번 정리에 그대로 재사용 */ }
public List<FAIConfig> FAIList { get; private set; }
```

From WPF_Example/Custom/Sequence/Inspection/FAIConfig.cs (기존, 무변경):
```csharp
public List<MeasurementBase> Measurements { get; private set; }
public object Owner { get; set; }                    // ShotConfig (ParamBase.Owner 상속)
public List<EdgeInspectionOverlay> LastOverlays { get; set; }   // overlay는 이미지와 별도로 항상 재현됨(RenderStoredOverlaysForFai)
public string LastOriginImageFileName { get; set; } = "";       // Action_FAIMeasurement.QueueFaiCapture 가 매 FAI마다 overlay 없는 원본을 기록 (폴백 소스로 사용)
public string LastCaptureImageFileName { get; set; } = "";      // overlay 렌더 완료본 (이번 정리에서는 사용 안 함 — 원본만 필요)
```

From WPF_Example/Sequence/Sequence/SequenceContext.cs (기존, 무변경):
```csharp
public class ActionContext {
    public HImage ResultHalconImage { get; set; }   // Action_FAIMeasurement.cs 에서 image.CopyImage() 로 세팅됨 (EStep.Grab, 크로스-Z 대표사진 교체)
    public string ResultImagePath { get; set; }      // Action_FAIMeasurement.cs 에서는 절대 세팅 안 됨(grep 확인) — 이번 정리에서 의존 금지
}
```

From WPF_Example/Sequence/Action/ActionBase.cs (기존, 무변경):
```csharp
public ParamBase Param { get; protected set; }     // Action_FAIMeasurement 는 ShotConfig 를 Param 으로 가짐 (1 Shot = 1 Action)
public ActionContext Context { get; protected set; }
```

From WPF_Example/Sequence/Sequence/SequenceBase.cs (기존, 무변경):
```csharp
public int ActionCount { get => Actions.Length; }
public ActionBase GetAction(int index) { ... }
```

From WPF_Example/Sequence/Param/ParamBase.cs (기존, 무변경):
```csharp
public SequenceBase Parent { get; set; }   // ShotConfig.Parent as InspectionSequence 로 소속 시퀀스 역추적 가능
```

From WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs (기존, 무변경 — 재사용 대상):
```csharp
private void ClearCrossZImages() { /* _crossZImageLock 안에서 전 엔트리 dispose + Dictionary.Clear() */ }
public void BeginCrossZImageCycle() { ClearCrossZImages(); }   // 다음 z=0 $TEST 수신용 — 이번 신규 메서드가 같은 private 헬퍼를 별도 진입점으로 재사용한다
```

From WPF_Example/Custom/Sequence/Inspection/BatchRunService.cs (기존, 무변경):
```csharp
public event Action<List<CycleResultDto>> OnBatchComplete;  // TargetCount=1(수동 일괄검사) 이므로 사이클당 정확히 1회만 발화
```

From WPF_Example/UI/ControlItem/InspectionListView.xaml.cs (기존, 무변경 — 이번 정리가 사용):
```csharp
public ParamBase SelectedParam { get; private set; }        // 트리 마지막 선택 노드(기존 필드, 신규 추적 메커니즘 도입 금지)
private List<ShotConfig> _batchShots;                       // 방금 실행한 배치의 SHOT 목록 (이번 사이클 범위, 재사용)
private void OnBatchComplete(List<CycleResultDto> cycles) { /* Dispatcher.Invoke 내부, UI 스레드 */ }
```
</interfaces>

<edit_anchors>
절대 라인 번호를 신뢰하지 말 것(코드가 드리프트됨) — 아래 content-anchor로 위치를 찾아 편집한다.

- `WPF_Example/SystemHandler.cs`: anchor = `public void Initialize() {` 메서드 시작. 이 라인의 여는 브레이스 바로 다음, `Stopwatch sw = Stopwatch.StartNew();` 라인보다 먼저 삽입.
- `WPF_Example/Custom/Sequence/Inspection/ShotConfig.cs`: anchor = `public void ClearImage() {` 메서드의 닫는 `}` 바로 다음(그 다음이 `// 빈값 폴백...` 주석 / `ApplyShotDefaults()` 메서드 시작 지점). 그 사이에 신규 메서드 삽입.
- `WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs`: anchor = `public void BeginCrossZImageCycle()` 메서드 블록. 그 닫는 `}` 바로 다음에 신규 메서드 삽입.
- `WPF_Example/UI/ContentItem/MainView.xaml.cs`: anchor = `public void DisplayShotImage(ShotConfig shot) {` 메서드 전체 — else 분기만 교체(Task 2 액션에 완성된 대체 본문 제공).
- `WPF_Example/UI/ControlItem/InspectionListView.xaml.cs`: anchor = `private void OnBatchComplete(List<CycleResultDto> cycles) {` 메서드. 본문 끝에 1줄 추가 + 그 메서드 뒤에 신규 private 메서드 2개 추가.
</edit_anchors>
</context>

<tasks>

<task type="auto">
  <name>Task 1: Part A — HALCON 메모리 캐시 idle 설정 (SystemHandler.cs)</name>
  <files>WPF_Example/SystemHandler.cs</files>
  <action>
HALCON 24.11 공식 문서(`C:\Program Files\MVTec\HALCON-24.11-Progress-Steady\doc\html\manuals\memory_management\`, Chapter 4 "Handling Suspected Memory Leaks in HALCON")가 권장하는 3줄을 앱 시작 시 1회, 가능한 가장 이른 시점에 실행한다.

**(1) using 추가**: 파일 상단 `using System.Threading;` 다음 줄에 `using HalconDotNet;` 추가.

**(2) `Initialize()` 진입부에 삽입** — anchor: `public void Initialize() {` 바로 다음, 기존 `Stopwatch sw = Stopwatch.StartNew();` 보다 먼저:

```csharp
        // Call after constructor to fully initialize runtime components.
        public void Initialize() {
            // quick-260806-dsn Part A: HALCON 자체 캐시(mimalloc, HALCON 24.11 Windows 기본 할당자)가 해제된
            //  메모리를 OS에 즉시 반환하지 않고 계속 쌓아두는 문제의 공식 완화책(memory_management 챕터,
            //  "Handling Suspected Memory Leaks in HALCON" 권장 3줄, 앱 시작 시 1회). 캐시 정책만 바꿀 뿐
            //  기능/정확성에는 영향 없다. Devices/Sequences 등 이후의 모든 Halcon 이미지 처리에 적용되도록
            //  이 메서드의 첫 실행문으로 둔다. 실패해도(캐시 힌트 실패일 뿐) 앱 시작을 막지 않는다.
            try {
                HOperatorSet.SetSystem("global_mem_cache", "idle");
                HOperatorSet.SetSystem("temporary_mem_cache", "idle");
                HOperatorSet.SetSystem("image_cache_capacity", 0);
            }
            catch (Exception ex) {
                Logging.PrintLog((int)ELogType.Error, "[STARTUP] HALCON SetSystem memory cache config failed: {0}", ex.Message);
            }

            Stopwatch sw = Stopwatch.StartNew(); //260528 hbk Phase 38 #11
```

나머지 `Initialize()` 본문(Step 1~8, EthernetVisionHandler 초기화 포함)은 전혀 건드리지 않는다 — 그 앞에 이 블록만 추가한다. `SystemHandler`의 생성자(`Devices.Initialize()`가 있는 곳)는 `Initialize()` 호출보다 항상 먼저 실행되지만, 카메라 SDK(Basler/HIK) 초기화 자체는 HALCON 연산을 호출하지 않으므로 무관하다 — 실제 대량 Halcon 이미지 처리(Grab/Measure)는 `Initialize()` 완료 후 사용자 조작/검사 시점에 비로소 시작되므로 이 위치가 요구사항("다른 시퀀스/디바이스 초기화보다 먼저")을 충족한다.
  </action>
  <verify>
    <automated>cd "C:/Info/Project/DataMeasurement" && "/c/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/MSBuild.exe" "WPF_Example/DatumMeasurement.csproj" //t:Build //p:Configuration=Debug //p:Platform=x64 //v:minimal 2>&1 | grep -iE "error|Build succeeded"; A=$(grep -n "global_mem_cache" WPF_Example/SystemHandler.cs | head -1 | cut -d: -f1); B=$(grep -n "Stopwatch sw = Stopwatch.StartNew" WPF_Example/SystemHandler.cs | head -1 | cut -d: -f1); if [ -n "$A" ] && [ -n "$B" ] && [ "$A" -lt "$B" ]; then echo "ORDER_OK"; else echo "ORDER_FAIL A=$A B=$B"; fi</automated>
  </verify>
  <done>3줄이 `Initialize()` 첫 실행문으로 존재하고 try/catch로 감싸져 있으며, 빌드가 error 0 으로 통과하고 `global_mem_cache` 라인이 `Stopwatch sw = Stopwatch.StartNew()` 라인보다 앞에 있다(ORDER_OK).</done>
</task>

<task type="auto">
  <name>Task 2: Part B 기반 — 디스크 폴백 헬퍼 + 크로스-Z 정리 진입점 + Shot 재클릭 안전망</name>
  <files>WPF_Example/Custom/Sequence/Inspection/ShotConfig.cs, WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs, WPF_Example/UI/ContentItem/MainView.xaml.cs</files>
  <action>
Task 3(실제 이미지 정리 트리거)이 안전하게 동작하려면, 먼저 "정리된 Shot을 다시 클릭했을 때 빈 화면이 뜨지 않게 하는" 안전망부터 만든다(Interface-First — 정리 로직보다 먼저 폴백을 만들어 회귀 창을 없앤다).

**(1) `ShotConfig.cs`** — anchor: `public void ClearImage() {` 메서드의 닫는 `}` 바로 다음에 신규 메서드 삽입(K&R 스타일):

```csharp
        /// <summary>
        /// quick-260806-dsn Part B: 배치 사이클 완료 후 메모리 정리로 _image 가 비워진 뒤에도 화면 재현이 가능한지
        /// 판단하기 위한 디스크 폴백 경로 조회. FAIList 의 각 FAI가 보유한 원본 캡쳐 파일
        /// (FAIConfig.LastOriginImageFileName — CaptureImageSaveService 가 매 검사마다 overlay 없이 저장하는 원본,
        /// Action_FAIMeasurement.QueueFaiCapture 가 기록) 중 실제 존재하는 첫 경로를 반환한다.
        /// overlay 는 이 경로와 별개로 FAIConfig.LastOverlays 를 통해 항상 재현되므로(RenderStoredOverlaysForFai),
        /// 여기서는 원본(overlay 미포함) 이미지 경로만 반환하면 된다.
        /// 반환값이 null/빈 문자열이면 호출자는 _image 정리를 건너뛰어야 한다(재클릭 시 빈 화면 회귀 방지).
        /// </summary>
        public string ResolveFallbackImagePath() {
            if (FAIList == null) return null;
            foreach (var fai in FAIList) {
                if (fai == null) continue;
                string path = fai.LastOriginImageFileName;
                if (!string.IsNullOrEmpty(path) && System.IO.File.Exists(path)) return path;
            }
            return null;
        }
```

**(2) `InspectionSequence.cs`** — anchor: `public void BeginCrossZImageCycle()` 메서드 블록의 닫는 `}` 바로 다음에 신규 메서드 삽입(Allman 스타일, 이 파일 기존 컨벤션):

```csharp
        // quick-260806-dsn Part B: 배치 사이클 "완료" 후 메모리 정리 전용 진입점. ClearCrossZImages 를 그대로
        //  재사용하지만, BeginCrossZImageCycle(다음 z=0 $TEST 수신 시 프로토콜 계약상 "유일한 진입점" — 위 주석
        //  참고)과는 호출 시점이 전혀 다르다(이번 사이클 완료 직후, UI 레벨 정리) — 그 주석이 가리키는 프로토콜
        //  계약을 깨지 않도록 이름을 분리해 별도 진입점으로 노출한다.
        public void ClearCrossZImagesAfterBatchCycle()
        {
            ClearCrossZImages();
        }
```

**(3) `MainView.xaml.cs`** — anchor: `public void DisplayShotImage(ShotConfig shot) {` 메서드 전체를 아래로 교체(else 분기에 디스크 폴백 추가, if 분기는 무변경):

```csharp
        /// <summary>Displays the image stored in the given ShotConfig on the canvas.</summary>
        public void DisplayShotImage(ShotConfig shot) {
            if (shot != null && shot.HasImage) {
                if (ReferenceEquals(shot, _lastDisplayedImageShot)) {
                    // 같은 Shot 재선택 — 이미지 내용 불변, 재로드를 건너뛰어 현재 확대/이동 상태를 그대로 유지한다.
                    label_message.Visibility = Visibility.Collapsed;
                    return;
                }
                HImage img = null;
                try {
                    img = shot.GetImage();
                    if (img != null) {
                        halconViewer.LoadImage(img);
                        _lastDisplayedImageShot = shot;
                        label_message.Visibility = Visibility.Collapsed;
                    } else {
                        label_message.Content = "이미지 로드 실패";
                        label_message.Visibility = Visibility.Visible;
                    }
                } finally {
                    if (img != null) img.Dispose();
                }
            } else {
                // quick-260806-dsn Part B: 배치 사이클 종료 후 메모리 정리(InspectionListView.CleanupBatchImageMemoryAfterCycle)로
                //  shot._image 가 비워졌을 수 있다 — FAI 원본 캡쳐 파일(overlay 미포함, RenderStoredOverlaysForFai 가
                //  overlay 는 별도로 그림)로 재로드를 시도한 뒤에도 없으면 기존과 동일하게 "NO Image" 표시.
                string fallbackPath = null;
                if (shot != null) fallbackPath = shot.ResolveFallbackImagePath();
                if (!string.IsNullOrEmpty(fallbackPath) && File.Exists(fallbackPath)) {
                    try {
                        halconViewer.LoadImage(fallbackPath);
                        _lastDisplayedImageShot = shot;
                        label_message.Visibility = Visibility.Collapsed;
                        return;
                    } catch (Exception ex) {
                        Logging.PrintErrLog((int)ELogType.Error, ex.Message);
                    }
                }
                _lastDisplayedImageShot = null;
                label_message.Content = "NO Image";
                label_message.Visibility = Visibility.Visible;
            }
        }
```

`File.Exists`는 이 파일에 이미 `System.IO` using이 있어(line 1665 부근 `DisplayContextToViewer`가 이미 사용) 추가 using 불필요. `Logging.PrintErrLog`도 이미 이 파일 전역에서 사용 중이므로 그대로 사용.
  </action>
  <verify>
    <automated>cd "C:/Info/Project/DataMeasurement" && "/c/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/MSBuild.exe" "WPF_Example/DatumMeasurement.csproj" //t:Build //p:Configuration=Debug //p:Platform=x64 //v:minimal 2>&1 | grep -iE "error|Build succeeded"; grep -n "ResolveFallbackImagePath" WPF_Example/Custom/Sequence/Inspection/ShotConfig.cs WPF_Example/UI/ContentItem/MainView.xaml.cs; grep -n "ClearCrossZImagesAfterBatchCycle" WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs</automated>
  </verify>
  <done>`ShotConfig.ResolveFallbackImagePath()`가 존재하고 FAIList를 순회하며 실제 존재하는 첫 원본 파일 경로를 반환한다. `InspectionSequence.ClearCrossZImagesAfterBatchCycle()`이 존재하고 기존 `ClearCrossZImages()`를 재사용한다(private 헬퍼 자체는 무변경). `MainView.DisplayShotImage`의 else 분기가 `shot.ResolveFallbackImagePath()`로 디스크 재로드를 시도한 뒤에도 실패하면 기존과 동일하게 "NO Image"를 표시한다. 빌드 error 0.</done>
</task>

<task type="auto">
  <name>Task 3: Part B 정리 트리거 — 배치 사이클 완료 시 비표시 Shot 이미지 캐시 해제 (InspectionListView.xaml.cs)</name>
  <files>WPF_Example/UI/ControlItem/InspectionListView.xaml.cs</files>
  <action>
Task 2에서 만든 안전망(디스크 폴백)을 전제로, 실제 "사이클 완료 시 정리" 트리거를 배선한다. 사이클이 완전히 끝나는 시점은 `BatchRunService.OnBatchComplete`(`TargetCount=1`인 수동 일괄검사이므로 정확히 1회만 발화)이며, 이미 `InspectionListView.xaml.cs`가 이 이벤트를 구독해 `OnBatchComplete(List<CycleResultDto> cycles)` 핸들러를 갖고 있다 — 새 이벤트 구독을 만들지 않고 이 기존 핸들러 안에 정리 호출을 추가한다.

**anchor**: `private void OnBatchComplete(List<CycleResultDto> cycles) {` 메서드. 본문(`Dispatcher.Invoke` 델리게이트) 마지막 줄에 호출 1줄을 추가하고, 이 메서드 바로 다음에 신규 private 메서드 2개(`CleanupBatchImageMemoryAfterCycle`, `ResolveCurrentlyDisplayedShot`)를 추가한다:

```csharp
        //260616 hbk Phase 51 BATCH-01: 일괄 검사 1사이클 완료 → 누적 + Export 버튼 활성 (D-04 append, D-05 수동 Export)
        private void OnBatchComplete(List<CycleResultDto> cycles) {
            Dispatcher.Invoke(new Action(delegate {
                if (cycles != null) {
                    _batchAccumulated.AddRange(cycles);
                }
                btn_batchExport.IsEnabled = (_batchAccumulated.Count > 0);
                //260617 hbk Quick 260617-cq2: 검사한 체크 SHOT 전체 측정 결과를 그리드에 펼쳐 표시.
                //  행이 live 측정 객체를 감싸므로 LastMeasuredValue/판정이 즉시 반영됨.
                if (_inspectionVm != null && _batchShots != null) {
                    _inspectionVm.ShowMeasurementsForShots(_batchShots);
                }
                // quick-260806-dsn Part B: 배치 사이클이 완전히 끝나는 유일한 시점(이 콜백, 사이클당 1회) —
                //  현재 화면에 표시 중인 노드를 제외한 나머지 SHOT의 대용량 이미지 캐시를 즉시 해제한다.
                CleanupBatchImageMemoryAfterCycle(_batchShots);
            }));
        }

        // quick-260806-dsn Part B: 사이클 완료 후 메모리 정리. 현재 트리 선택(SelectedParam)이 가리키는 SHOT은
        //  제외하고, 각 SHOT의 이미지 캐시(ShotConfig._image)와 대응 Action의 표시용 클론
        //  (ActionContext.ResultHalconImage)을 Dispose한다. 재클릭 시 재현할 디스크 폴백
        //  (ShotConfig.ResolveFallbackImagePath)이 없는 SHOT은 정리를 건너뛴다 — 메모리 절감보다 빈 화면 회귀
        //  방지가 우선(사용자 확인 요구사항). 크로스-Z 저장소도 같은 시점에 함께 정리한다.
        //  단일 RUN(Btn_start_Click)이나 사이클 도중에는 이 메서드가 호출되지 않는다(OnBatchComplete 전용 경로).
        private void CleanupBatchImageMemoryAfterCycle(List<ShotConfig> shots) {
            if (shots == null || shots.Count == 0) return;

            ShotConfig currentShot = ResolveCurrentlyDisplayedShot();

            foreach (ShotConfig shot in shots) {
                if (shot == null) continue;
                if (ReferenceEquals(shot, currentShot)) continue; // 현재 표시 중인 노드는 보존

                string fallbackPath = shot.ResolveFallbackImagePath();
                if (string.IsNullOrEmpty(fallbackPath)) continue; // 재현 불가 SHOT은 정리 skip(회귀 방지 우선)

                shot.ClearImage(); // ShotConfig.cs 기존 _imageLock 보호 dispose+null 재사용

                InspectionSequence shotSeq = shot.Parent as InspectionSequence;
                if (shotSeq == null) continue;
                for (int i = 0; i < shotSeq.ActionCount; i++) {
                    ActionBase act = shotSeq.GetAction(i);
                    if (act != null && ReferenceEquals(act.Param, shot)) {
                        if (act.Context != null && act.Context.ResultHalconImage != null) {
                            act.Context.ResultHalconImage.Dispose();
                            act.Context.ResultHalconImage = null;
                        }
                        break; // SHOT 당 Action 1개 (1:1)
                    }
                }
            }

            // 일괄검사는 단일 시퀀스 내 SHOT만 허용(Btn_batchInspect_Click 검증) — shots[0]의 소속 시퀀스로 충분.
            InspectionSequence batchSeq = shots[0].Parent as InspectionSequence;
            if (batchSeq != null) {
                batchSeq.ClearCrossZImagesAfterBatchCycle();
            }
        }

        // quick-260806-dsn Part B: 현재 트리에서 선택된 노드(SelectedParam)가 속한 ShotConfig를 역추적한다.
        //  MainView.GetCurrentShotContext()와 동일 목적이나 그 메서드는 private이라 직접 참조할 수 없어
        //  _batchShots 스코프 내에서 자체 해석한다(측정 노드는 이 배치에 속한 SHOT 안에서만 검색하면 충분).
        private ShotConfig ResolveCurrentlyDisplayedShot() {
            ParamBase sel = SelectedParam;
            if (sel == null) return null;
            if (sel is ShotConfig shotSel) return shotSel;
            if (sel is FAIConfig faiSel) return faiSel.Owner as ShotConfig;
            if (sel is MeasurementBase measSel && _batchShots != null) {
                foreach (ShotConfig shot in _batchShots) {
                    if (shot == null || shot.FAIList == null) continue;
                    foreach (FAIConfig fai in shot.FAIList) {
                        if (fai.Measurements != null && fai.Measurements.Contains(measSel)) return shot;
                    }
                }
            }
            return null;
        }
```

**중요 — 스코프 경계 확인(회귀 방지, executor가 반드시 grep으로 자체 확인할 것):**
- `CleanupBatchImageMemoryAfterCycle` 호출부는 위 `OnBatchComplete` 안 **1곳뿐**이어야 한다. `Btn_start_Click`(단일 RUN) 이나 `TriggerNext`/`HandleFinish`(사이클 도중) 어디에도 추가하지 않는다.
- `shot.Parent as InspectionSequence`가 null이면(비정상 상태) 해당 SHOT은 조용히 skip — 예외를 던지지 않는다.
- `ActionCount`/`GetAction` 순회는 `_seq.State == Idle`(OnBatchComplete 시점에 항상 보장됨 — `BatchRunService.HandleFinish`가 `Stop()` 후 발화)에서만 실행되므로 시퀀스 스레드와의 동시 접근 위험이 없다.
  </action>
  <verify>
    <automated>cd "C:/Info/Project/DataMeasurement" && "/c/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/MSBuild.exe" "WPF_Example/DatumMeasurement.csproj" //t:Build //p:Configuration=Debug //p:Platform=x64 //v:minimal 2>&1 | grep -iE "error|Build succeeded"; N=$(grep -c "CleanupBatchImageMemoryAfterCycle" WPF_Example/UI/ControlItem/InspectionListView.xaml.cs); if [ "$N" -eq 2 ]; then echo "SINGLE_TRIGGER_OK (def+1 call)"; else echo "TRIGGER_COUNT_UNEXPECTED N=$N"; fi; grep -n "private void Btn_start_Click" -A 80 WPF_Example/UI/ControlItem/InspectionListView.xaml.cs | grep -c "CleanupBatchImageMemoryAfterCycle" | xargs -I{} echo "matches_in_Btn_start_Click={}"</automated>
  </verify>
  <done>`CleanupBatchImageMemoryAfterCycle`과 `ResolveCurrentlyDisplayedShot`이 추가되고, `OnBatchComplete`의 `Dispatcher.Invoke` 델리게이트 마지막 줄에서 정확히 1회 호출된다(정의 포함 총 2회 매치). `Btn_start_Click` 본문에는 이 호출이 없다(matches_in_Btn_start_Click=0). 빌드 error 0.</done>
</task>

<task type="checkpoint:human-verify" gate="blocking">
  <what-built>
Part A: `SystemHandler.Initialize()` 진입부에 HALCON 공식 권장 캐시 idle 설정 3줄 추가.
Part B: 일괄검사 1사이클이 완전히 끝나는 시점(`BatchRunService.OnBatchComplete`)에, 현재 화면에 표시 중인 노드를 제외한 나머지 SHOT의 `ShotConfig._image`/`ActionContext.ResultHalconImage`를 Dispose하고 크로스-Z 이미지 저장소를 정리한다. 정리된 SHOT을 나중에 다시 클릭하면 `MainView.DisplayShotImage`가 디스크에 저장된 FAI 원본 캡쳐 파일로 자동 재로드한다(overlay는 항상 별도로 재현됨).
자동 검증(Task 1~3)으로 빌드 PASS + 구조적 배선(단일 트리거 지점, 헬퍼 존재, 순서)을 확인했지만, **실제 메모리 감소량과 화면 재현 정확성은 자동 검증이 불가능하다** — 오늘 실기 재현했던 정확히 그 시나리오로 재확인이 필요하다.
  </what-built>
  <how-to-verify>
1. 실행 중인 이전 인스턴스가 있으면 완전히 종료한다. 최신 커밋 기준으로 Debug/x64 재빌드 후 앱을 새로 실행한다.
2. PowerShell에서 메모리를 실시간 관찰할 준비를 한다(별도 창):
   ```powershell
   while ($true) { $p = Get-Process DatumMeasurement -ErrorAction SilentlyContinue; if ($p) { "{0:HH:mm:ss} {1:N0} MB" -f (Get-Date), ($p.WorkingSet64/1MB) }; Start-Sleep -Seconds 2 }
   ```
3. 트리에서 BOTTOM 시퀀스를 선택하고, 오늘 재현 때와 동일하게 약 30개 측정 항목(SHOT)을 체크한다.
4. 임의의 SHOT/FAI/Measurement 노드 하나를 클릭해 화면에 이미지가 표시된 상태로 둔다(이 노드가 "현재 표시 중인 노드"가 된다 — 정리 후에도 이 노드만은 예외 없이 그대로 보여야 한다).
5. "일괄검사" 버튼을 눌러 1사이클을 실행하고 완료를 기다린다.
6. 사이클 완료 후 **아무것도 클릭하지 말고 30초 이상 대기**하며 PowerShell 메모리 로그를 관찰한다.
   - **(a) 확인**: 메모리가 사이클 진행 중 올라갔다가, 완료 후 수 GB대가 아니라 **수백 MB대**로 떨어지는지 확인한다(오늘 재현 시 1→2.7→8.3→...→12.4GB로 계단식 증가 후 미감소였던 것과 대비).
   - **(b) 확인**: 4번에서 표시해뒀던 그 노드의 이미지가 **여전히 정상적으로 보이는지**(빈 화면/깨짐 없음) 확인한다.
7. 트리에서 4번과 **다른** SHOT/FAI/Measurement 노드 여러 개를 순서대로 클릭한다.
   - **(c) 확인**: 각 노드마다 이미지 + 측정 overlay(에지 표시 등)가 **정상적으로 표시되는지** 확인한다(디스크 폴백 경로 — 클릭 즉시 표시되면 정상, 빈 화면/"NO Image"가 뜨면 회귀).
8. 7번에서 클릭했던 SHOT 중 하나를 선택한 채로 단일 RUN(또는 그 SHOT만 다시 일괄검사)으로 재실행한다.
   - **(d) 확인**: 크래시나 예외 팝업 없이 정상적으로 재검사가 수행되고 결과/이미지가 갱신되는지 확인한다.
  </how-to-verify>
  <resume-signal>(a)(b)(c)(d) 모두 정상이면 "approved". 하나라도 문제가 있으면 어떤 단계에서 무엇이 잘못됐는지(메모리가 여전히 안 줄어듦 / 특정 노드 빈 화면 / 재실행 시 예외 등) 구체적으로 기술.</resume-signal>
</task>

</tasks>

<threat_model>
## Trust Boundaries

| Boundary | Description |
|----------|-------------|
| 없음 (신규 trust boundary 미도입) | 이번 변경은 순수 내부 메모리 라이프사이클 관리(HImage dispose 시점 조정 + 기존에 이미 디스크에 저장되던 파일을 읽는 것)이다. 새로운 외부 입력, 네트워크 노출, 사용자 입력 경로가 추가되지 않는다. |

## STRIDE Threat Register

| Threat ID | Category | Component | Disposition | Mitigation Plan |
|-----------|----------|-----------|-------------|-----------------|
| T-260806dsn-01 | D (Denial of Service — 자기 자신 UI 기능 저하) | `CleanupBatchImageMemoryAfterCycle` | mitigate | 디스크 폴백 경로가 검증되지 않은 SHOT은 정리를 skip(회귀보다 안전 우선) — 잘못된 정리로 인한 UI 기능 저하(빈 화면)를 원천 차단. |
| T-260806dsn-02 | I (정보 무결성 — 잘못된 이미지 표시) | `MainView.DisplayShotImage` 폴백 분기 | accept | 폴백은 `FAIConfig.LastOriginImageFileName`(같은 Shot이 마지막으로 캡처한 원본)만 사용하고 존재 여부를 `File.Exists`로 확인한다 — 타 Shot/구버전 파일이 섞일 경로 없음(FAI가 자기 소유 Shot 이미지만 기록). 잔여 리스크는 낮음(로컬 파일시스템, 외부 변조 불가 경로). |
| T-260806dsn-03 | E (권한 상승) | 해당 없음 | accept | 이번 변경은 권한/인증 경계를 다루지 않는다(로컬 산업용 비전 앱, 네트워크 신규 노출 없음). |
</threat_model>

<verification>
- Task 1~3의 각 `<automated>` 커맨드가 전부 "Build succeeded" + error 0, 그리고 각 구조적 체크(ORDER_OK, 헬퍼 존재, SINGLE_TRIGGER_OK, Btn_start_Click 0매치)가 기대값과 일치.
- `CleanupBatchImageMemoryAfterCycle`은 `OnBatchComplete` 1곳에서만 호출되고, `Btn_start_Click`/사이클 진행 중 어디에도 배선되지 않는다.
- `ShotConfig.ClearImage()`/`_imageLock`은 무변경 재사용(락 우회 없음), `InspectionSequence.ClearCrossZImages()`(private)도 무변경 재사용.
- Task 4(checkpoint)의 실기 재검증 (a)(b)(c)(d) 전부 승인.
</verification>

<success_criteria>
- Part A: HALCON SetSystem 3줄이 `Initialize()` 최초 실행문으로 존재, 빌드 PASS.
- Part B: 일괄검사 1사이클 완료 후 메모리가 수백 MB대로 감소(사용자 실측 확인).
- 사이클 종료 직후 표시 중이던 노드와, 이후 클릭하는 다른 노드 모두 이미지+overlay가 정상 표시(회귀 0).
- 정리된 SHOT의 재실행이 예외 없이 정상 동작.
- 단일 RUN과 사이클 진행 도중에는 어떤 이미지도 조기 Dispose되지 않음.
</success_criteria>

<output>
After completion, create `.planning/quick/260806-dsn-overlay-window-reuse/260806-dsn-SUMMARY.md`
</output>
