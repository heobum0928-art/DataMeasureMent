---
phase: quick-260814-dxy
plan: 01
type: execute
wave: 1
depends_on: []
files_modified:
  - WPF_Example/SystemHandler.cs
  - WPF_Example/Custom/SystemHandler.cs
  - WPF_Example/MainWindow.xaml.cs
  - WPF_Example/UI/ControlItem/InspectionListView.xaml.cs
autonomous: false
requirements: [MEASURE-WARMUP-01]

must_haves:
  truths:
    - "앱 시작(레시피 로드 완료 직후) 시 측정 파이프라인 워밍업이 백그라운드 스레드에서 자동 실행되고, UI 스레드를 블로킹하지 않는다"
    - "워밍업이 끝나기 전에는 TCP $TEST 요청이 거부된다(IsRecipeReady 게이트와 동일한 방식으로 추가 게이트)"
    - "워밍업이 끝나기 전에는 UI RUN(Btn_start_Click)/일괄검사(Btn_batchRun_Click) 클릭이 안내 메시지와 함께 거부된다"
    - "워밍업 중 예외가 발생해도 앱 기동이 막히지 않고, 게이트는 반드시(성공/실패 무관) 열린다(fail-open)"
    - "레시피가 없는 기동(CurrentRecipeName==null)에서도 게이트가 영원히 닫혀 있지 않고 즉시 열린다"
    - "워밍업 호출은 meas.TryExecute() 의 out 파라미터만 사용하고 EvaluateJudgement/ClearResult 를 호출하지 않아, 실제 판정 로직/화면 표시(LastMeasuredValue/LastJudgement)에 어떤 영향도 주지 않는다"
    - "워밍업은 실제 프로덕션과 동일한 meas.TryExecute() 호출 경로(GenMeasureRectangle2/GenMeasureArc + MeasurePos/MeasurePairs)를 태운다 — 별도의 단순화된 가짜 경로가 아니다"
  artifacts:
    - path: "WPF_Example/SystemHandler.cs"
      provides: "IsMeasureWarmupComplete 게이트 플래그 (IsRecipeReady 와 동일 패턴)"
      contains: "public bool IsMeasureWarmupComplete"
    - path: "WPF_Example/Custom/SystemHandler.cs"
      provides: "StartMeasureWarmupAsync/RunMeasureWarmup/TryWarmupOneMeasurement/FindMeasureWarmupShot 워밍업 서비스 + ProcessTest 게이트 체크"
      contains: "StartMeasureWarmupAsync"
    - path: "WPF_Example/MainWindow.xaml.cs"
      provides: "Window_ContentRendered_LoadRecipe 에서 레시피 로드 직후(양쪽 분기 모두) 워밍업 기동"
      contains: "StartMeasureWarmupAsync()"
    - path: "WPF_Example/UI/ControlItem/InspectionListView.xaml.cs"
      provides: "Btn_start_Click/Btn_batchRun_Click 워밍업 완료 게이트"
      contains: "IsMeasureWarmupComplete"
  key_links:
    - from: "WPF_Example/MainWindow.xaml.cs Window_ContentRendered_LoadRecipe"
      to: "WPF_Example/Custom/SystemHandler.cs StartMeasureWarmupAsync"
      via: "mSystemHandler.StartMeasureWarmupAsync() 호출 (레시피 있음/없음 두 분기 모두)"
      pattern: "StartMeasureWarmupAsync\\(\\)"
    - from: "WPF_Example/Custom/SystemHandler.cs StartMeasureWarmupAsync (Task.Run 내부, finally)"
      to: "WPF_Example/SystemHandler.cs IsMeasureWarmupComplete"
      via: "IsMeasureWarmupComplete = true 대입 — 성공/예외 무관 항상 실행"
      pattern: "IsMeasureWarmupComplete = true;"
    - from: "WPF_Example/Custom/SystemHandler.cs ProcessTest"
      to: "WPF_Example/SystemHandler.cs IsMeasureWarmupComplete"
      via: "TEST 패킷 처리 진입부 게이트 체크 (IsRecipeReady 체크 바로 다음)"
      pattern: "if \\(!IsMeasureWarmupComplete\\)"
    - from: "WPF_Example/UI/ControlItem/InspectionListView.xaml.cs Btn_start_Click / Btn_batchRun_Click"
      to: "WPF_Example/SystemHandler.cs IsMeasureWarmupComplete"
      via: "SystemHandler.Handle.IsMeasureWarmupComplete 체크 후 CustomMessageBox 안내 + return"
      pattern: "SystemHandler\\.Handle\\.IsMeasureWarmupComplete"
---

<objective>
Release 빌드 콜드스타트 시 HALCON measureExec(`MeasurePos`/`MeasurePairs`)가 정체불명의 원인으로 수 배~10배
느려지는 문제(`.planning/debug/top-release-2x-slower.md`, status: `root_cause_narrowed_workaround_pending`)를
**완전히 해결하는 게 아니라, 그 비용을 실제 검사 사이클이 아니라 앱 기동 시점에 미리 확정적으로 치르게 만드는**
임시 완화책을 추가한다.

**핵심 설계:** 앱이 레시피를 로드한 직후, 실제 프로덕션과 동일한 `MeasurementBase.TryExecute()` 호출 경로를
현재 레시피의 대표 Shot 하나로 15회 반복 실행해(결과는 전부 버림) HALCON 내부 캐시/코드페이지를 미리 데운다.
UI 스레드를 막지 않도록 백그라운드 스레드(`Task.Run`)에서 실행하고, 완료 전까지는 TCP `$TEST` 와 UI
RUN/일괄검사를 새 게이트 플래그(`IsMeasureWarmupComplete`)로 막는다 — 기존 `IsRecipeReady` TCP 게이트와
정확히 동일한 패턴(볼륨/네이밍/파일 위치)을 그대로 재사용한다.

**원인 불명 — 반드시 유의:** 디버그 세션에서 근본원인을 확정하지 못했다(mimalloc/temporary_mem_cache 워밍업
후보, 또는 AV 커널모드 first-touch 개입 후보 둘 다 미확증). 관측된 워밍업 문턱도 7회~36회+(전혀 하락 안 함
포함)로 완전히 들쭉날쭉했다. 따라서 이 워밍업이 문제를 **완전히 해소한다는 보장은 없다** — "임시 완화 시도"이지
"해결"이 아니다. SUMMARY 에 반드시 이 뉘앙스를 그대로 남길 것.

Purpose: 사용자가 사내 IT 팀에 요청 중인 ESET 성능 예외(KB7833) 승인을 기다리는 동안, 코드만으로 시도할 수
있는 완화책을 먼저 적용해 둔다.
Output: `IsMeasureWarmupComplete` 게이트 플래그 + 측정 파이프라인 워밍업 서비스 + TCP/UI 양쪽 게이트 배선.
</objective>

<execution_context>
@$HOME/.claude/get-shit-done/workflows/execute-plan.md
@$HOME/.claude/get-shit-done/templates/summary.md
</execution_context>

<context>
@./CLAUDE.md
@.planning/debug/top-release-2x-slower.md

**코딩 규칙 (이 프로젝트 상시 규칙 — 위반 시 리뷰 반려):**
- 삼항연산자 `?:` **금지** → 반드시 `if / else`
- C# 7.2 (`nullable`, `switch expression`, `record` 등 8.0+ 문법 금지), .NET Framework 4.8
- 헝가리언 표기 점진 적용 — 로컬 `bool` 은 `b` 접두, `int` 는 `n` 접두 (신규 코드에 한함, 기존 코드 소급 변경 금지)
- 신규 주석은 `260814 hbk quick-260814-dxy:` 접두. `//YYMMDD hbk` 날짜 주석 "필수" 규칙은 2026-06-11 부로
  폐기됨 — 비자명한 "왜"가 있을 때만 최소한으로 남긴다
- 함수 본문은 대략 30~40줄 내외 유지 (하드 제한 아님)

---

## 절대 건들면 안 되는 파일 (열지도 말 것)

`git status --porcelain` 기준 baseline (작업 시작 시점):

| 파일 | 상태 | 지침 |
|------|------|------|
| `WPF_Example/DatumMeasurement.csproj` | 사용자의 별도 진행 중인 로컬 실험(Release 에 SIMUL_MODE 임시 추가 등) | **절대 열지도, 건들지도 말 것.** 이번 작업은 새 `.cs` 파일을 만들지 않으므로 csproj 편집이 애초에 필요 없다(기존 4개 파일만 수정) |
| `WPF_Example/Custom/EthernetVision/PickerCenterCalibrationService.cs` | 사용자의 별도 진행 중인 로컬 실험(탐색범위 조정) | **절대 열지도, 건들지도 말 것.** 이번 작업과 무관 |

baseline diff 해시 (작업 후에도 동일해야 함):
```
DatumMeasurement.csproj              : 3daa3bef520786d331716fb77bc93e2eb632b966
PickerCenterCalibrationService.cs    : 86d1071909389cdb13b4ff8f3032489aff26e2fe
```

**참고:** `WPF_Example/SystemHandler.cs` 도 현재 `git status` 상 `M`(수정됨) 상태다 — 다만 이건 L128 근처
빈 줄 1개가 늘어난 사소한 기존 변경(사용자 실험 아님, 이번 작업과 무관)이고, 이 파일은 애초에 이번 계획의
**수정 대상**이므로 그대로 두고 그 위에 이어서 편집하면 된다. 되돌리거나 신경 쓸 필요 없음.

`git add .` / `git add -A` / `git commit -a` 는 금지 — 반드시 수정한 4개 파일만 명시적으로 `git add`.

---

## 새 파일을 만들지 않는 이유

`DatumMeasurement.csproj` 는 classic-style(`packages.config`) MSBuild 프로젝트라 `<Compile Include="...">`
로 파일을 명시적으로 나열하는 구조일 가능성이 높다 — 그런데 csproj 는 절대 건들면 안 되는 파일이다. 그래서
새 `.cs` 파일(예: 별도 `MeasurePipelineWarmupService.cs`)을 만드는 대신, **워밍업 로직 전부를 기존에 이미
컴파일 대상인 `WPF_Example/Custom/SystemHandler.cs` 안에 메서드로 추가**한다. 이 파일은 이미
`using System.Threading.Tasks;` / `using ReringProject.Sequence;` / `using HalconDotNet;` /
`using System.Diagnostics;` 를 갖고 있어 추가로 필요한 using 은 `System.IO`(`File.Exists`)와
`ReringProject.Halcon.Models`(`EdgeInspectionOverlay`) 딱 2개뿐이다.

---

## 조사 결과 — 이 계획의 근거 (실행자는 아래를 다시 확인할 필요 없다)

### (1) 실행 순서 — 레시피 로드는 언제 끝나는가

```
MainWindow ctor → mSystemHandler.Initialize()  (Sequences 생성 등, 레시피는 아직 안 실림)
   ↓
Window_Loaded → this.ContentRendered += Window_ContentRendered_LoadRecipe
   ↓ (첫 페인트 후)
Window_ContentRendered_LoadRecipe:
   - CurrentRecipeName == null  → IsRecipeReady=true 후 즉시 return (레시피 없음)
   - 아니면 Dispatcher.BeginInvoke(Background, () => {
         mSystemHandler.LoadRecipe(...);   // 여기서 Sequences.RecipeManager.Shots 가 채워짐
         mSystemHandler.IsRecipeReady = true;
     })
```
`Sequences.RecipeManager.Shots`(`List<ShotConfig>`)는 `LoadRecipe()` 호출이 끝난 **직후**부터 유효하다 —
그래서 워밍업 기동 호출은 반드시 `IsRecipeReady = true;` 대입과 같은 지점(레시피 있음/없음 두 분기 모두)에
추가한다.

### (2) 워밍업이 실제로 실행하는 것 — `Action_FAIMeasurement.TryExecuteMeasurement` 와 동일 호출 경로

프로덕션 코드(`Action_FAIMeasurement.cs` EStep.Measure)는 각 측정에 대해 정확히 이렇게 호출한다:
```csharp
// DualImage 타입이면 RuntimeImageA/B 를 먼저 주입
ok = meas.TryExecute(image, transform, pixRes, out resultValue, out measError, out measOverlays);
if (ok) meas.EvaluateJudgement(resultValue);
else { meas.ClearResult(); meas.LastJudgement = false; }
```
`meas.TryExecute()` 내부가 바로 `GenMeasureRectangle2`/`GenMeasureArc` + `MeasurePos`/`MeasurePairs` 를
호출하는 지점이다(디버그 세션이 `[FaiTiming]` 계측으로 병목을 확정한 바로 그 지점). 워밍업은 **이 호출만**
반복하고 `EvaluateJudgement`/`ClearResult` 는 호출하지 않는다 — `TryExecute` 구현체(예:
`EdgeToLineDistanceMeasurement.cs`, `DualImageEdgeDistanceMeasurement.cs`)를 직접 확인한 결과 결과값은
전부 `out` 파라미터로만 반환되고 측정 객체의 `Last*` 필드를 직접 쓰지 않는다 — 그래서 워밍업이 화면
표시/판정에 어떤 흔적도 남기지 않는다.

`datumTransform` 인자는 `null` 을 넘긴다 — `MeasurementBase.TryExecute` 의 계약상 "null/empty 면 identity"
이고(`EdgeToLineDistanceMeasurement.cs:111` `if (datumTransform == null || datumTransform.Length == 0)` 로
이미 이 널 체크가 기존 관례임을 확인함), 워밍업은 결과 정확도가 필요 없으므로 identity 로 충분하다.

### (3) 더미 이미지 — 카메라 미연결 상태에서도 안전해야 한다

카메라를 그랩하지 않는다(하드웨어 의존 0). 대신:
1. 우선순위 1: 현재 레시피의 Shot 들 중 `SimulImagePath` 파일이 실제로 존재하는 **첫 Shot** 을 찾아 그
   이미지를 로드한다 — SIMUL_MODE/오프라인 검사가 애초에 이 값을 전제로 동작하므로(이번 디버그 세션도 Top
   SIMUL_MODE 오프라인 배치로 재현했다), 실제 운용 레시피라면 거의 항상 존재한다. 이게 가장 신뢰도 높은
   경로(진짜 ROI 좌표 + 진짜 이미지 크기가 맞아서 실제로 에지가 검출될 가능성이 높음).
2. 우선순위 2(폴백): 측정 항목이 있는 Shot 은 있는데 `SimulImagePath` 파일이 하나도 없으면(신규 미티칭
   레시피 등), `HOperatorSet.GenImageConst("byte", 2048, 2048)` 합성 이미지로 대체한다. 이 경로는 실제
   에지 검출 성공을 보장하지 않지만(ROI 가 캔버스 밖일 수 있음), 캐시 워밍이 목적이므로 검출 실패해도
   상관없다 — 실패해도 `TryExecute` 는 예외를 던지지 않고 `false` 를 반환하도록 이미 구현돼 있다(프로젝트
   컨벤션: `HOperatorSet.*` 호출은 항상 `try{}catch{return false;}`).
3. 측정 항목이 있는 Shot 자체가 하나도 없으면(빈 레시피) 워밍업을 스킵하고 즉시 게이트를 연다.

### (4) 반복 횟수 — 하드코딩 상수로 결정 (discretion)

관측된 워밍업 문턱이 7회~36회+(심지어 전혀 하락 안 함)로 완전히 들쭉날쭉했으므로, Setting.ini 로 노출해도
"정답값"을 사용자가 알 수 없다 — 오히려 설정 UI 만 늘어나고 실효는 없다. `MEASURE_WARMUP_ITERATIONS = 15`
(관측된 여러 문턱의 중간값 근사)를 하드코딩 상수로 둔다. 이 판단은 코드 주석에 근거와 함께 남긴다.

<interfaces>
<!-- 실행자가 코드베이스를 탐색할 필요가 없도록 편집 대상 지점의 현재 코드를 그대로 옮겨둔다. -->

**`WPF_Example/SystemHandler.cs` (L73~76, 현재 상태 — K&R 브레이스, 헝가리언 없음, 이 스타일 그대로 유지):**
```csharp
        //260615 hbk Phase 43.2: 레시피 비동기 로드 완료 신호 — ProcessTest guard 참조용 (D-B)
        private volatile bool _isRecipeReady = false;
        public bool IsRecipeReady { get { return _isRecipeReady; } set { _isRecipeReady = value; } }
```
이 블록 **바로 다음 줄**(그 다음이 빈 줄 + `private SystemHandler() {` 생성자)에 새 플래그를 추가한다.

**`WPF_Example/Custom/SystemHandler.cs` — 현재 using 목록 (파일 최상단, 그대로):**
```csharp
using HalconDotNet;
using ReringProject.Device;
using ReringProject.Setting;
using ReringProject.Network;
using ReringProject.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using ReringProject.Sequence;
using System.Diagnostics;
using TeachDiag   = ReringProject.Halcon.Algorithms.TeachDiagnostics;
using ETeachGrade = ReringProject.Halcon.Algorithms.ETeachGrade;
```
`System.IO`(File.Exists) 와 `ReringProject.Halcon.Models`(EdgeInspectionOverlay) 딱 2개만 추가하면 된다.

**`ProcessTest` 현재 전체 (K&R 스타일 — 이 메서드 편집은 이 스타일 그대로 유지):**
```csharp
        private bool ProcessTest(TestPacket packet) {
            if (!IsRecipeReady) {
                Logging.PrintLog((int)ELogType.Error, "[RECIPE] TEST rejected — recipe not yet loaded (IsRecipeReady=false)");
                return false;
            }
            packet.TestID = _lastPrepZIndex.ToString(); //260626 hbk $PREP z_index 주입
            if (Sequences.IsDynamicFAIMode) {
                string seqName = packet.Identifier;
                SequenceBase seq = Sequences[seqName];
                if (seq == null) return false;
                return StartV1Scoped(seq, packet);
            }
            return Sequences.Start(packet);
        }
```

**`SendTestError` 의 끝 + 그 다음 메서드(`ProcessAlignTest`) 시작부 — 새 워밍업 메서드들을 이 사이에 삽입
(이 지점부터 파일은 Allman 브레이스 + 헝가리언 bool(`b` 접두) 스타일로 전환된다 — 새 메서드는 이 스타일을
따른다):**
```csharp
        private TestResultPacket SendTestError(TestPacket packet) {
            TestResultPacket resultPacket = new TestResultPacket();
            TestPacket sendPacket = packet.AsTest();

            resultPacket.Target = sendPacket.Sender;
            resultPacket.Site = sendPacket.Site;
            resultPacket.InspectionType = sendPacket.TestType;
            resultPacket.Result = EVisionResultType.NG;

            return resultPacket;
        }

        //260626 hbk Phase 65 Plan 03 AV-08: $ALIGN_TEST 처리 — stub(IsPass=true echo) → 실측 grab+Run+pose 채움.
        //  BOTTOM: AlignFace→슬롯→grab→Matcher.Run→FillAlignPose(OffsetX/OffsetY/Theta)+IsPass=Found (D-06/D-07).
        //  TRAY: grab/Run 미수행 — 기존 echo ack 동작 유지 (회귀 0).
        //  AlignFace 범위 외(음수/6이상): IsPass=false 안전 거부+로그 (T-65-01).
        private AlignResultPacket ProcessAlignTest(AlignTestPacket packet) //260626 hbk 실측 경로 배선 (Phase 65 P03)
```

**핵심 타입 시그니처 (전부 이미 존재, 신규 아님 — `ReringProject.Sequence` 네임스페이스, 이미 using 됨):**
```csharp
// SequenceHandler.cs
public InspectionRecipeManager RecipeManager { get; } = new InspectionRecipeManager(Handle);

// InspectionRecipeManager.cs
public List<ShotConfig> Shots { get; private set; } = new List<ShotConfig>();
public int ShotCount => Shots.Count;

// ShotConfig.cs
public string SimulImagePath { get; set; } = "";
public List<FAIConfig> FAIList { get; private set; } = new List<FAIConfig>();
public string ShotName { get; set; }

// FAIConfig.cs
public List<MeasurementBase> Measurements { get; private set; } = new List<MeasurementBase>();

// MeasurementBase.cs
public abstract bool TryExecute(
    HImage image, HTuple datumTransform, double pixelResolution,
    out double resultValue, out string error, out List<EdgeInspectionOverlay> overlays);

// DualImageEdgeDistanceMeasurement.cs (MeasurementBase 파생, RuntimeImageA/B 주입 필요)
public HImage RuntimeImageA { get; set; }
public HImage RuntimeImageB { get; set; }
```

**`WPF_Example/MainWindow.xaml.cs` — `Window_ContentRendered_LoadRecipe` 현재 전체 (편집 대상 메서드 전문):**
```csharp
        private void Window_ContentRendered_LoadRecipe(object sender, EventArgs e) {
            this.ContentRendered -= Window_ContentRendered_LoadRecipe; // 1회 실행 후 구독 해제
            if (mSystemHandler.Setting.CurrentRecipeName == null) {
                mSystemHandler.IsRecipeReady = true; // 레시피 없어도 guard 해제
                return;
            }
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background, new Action(() => {
                mSystemHandler.LoadRecipe(mSystemHandler.Setting.CurrentRecipeName);
                mSystemHandler.IsRecipeReady = true; //260615 hbk Phase 43.2: 로드 완료(성공/실패 무관) → TCP guard 해제 (D-B)
            }));
        }
```

**`WPF_Example/UI/ControlItem/InspectionListView.xaml.cs` — 편집 대상 두 메서드 시작부(그대로):**
```csharp
        private void Btn_start_Click(object sender, RoutedEventArgs e) {
            if (treeListBox_sequence.SelectedIndex < 0) return;
            ...
```
```csharp
        private void Btn_batchRun_Click(object sender, RoutedEventArgs e) {
            var root = treeListBox_sequence.Items.Count > 0 ? treeListBox_sequence.Items[0] as NodeViewModel : null;
            ...
```
(이 파일은 이미 `SystemHandler`/`CustomMessageBox`/`MessageBoxImage` 를 다른 곳에서 쓰고 있으므로 신규
using 불필요. `CustomMessageBox.Show(string title, string message, MessageBoxImage image)` 3-인자 오버로드는
같은 파일의 `Btn_batchRun_Click` 안에 이미 실사용 예가 있다: `CustomMessageBox.Show("일괄 검사", "검사할
SHOT 을 체크하세요.", MessageBoxImage.Warning);`. 위 `var root = ... ? ... : ...;` 삼항연산자는 기존
코드다 — 이번 작업 범위가 아니므로 건들지 않는다.)

빌드 환경(planner 실측 확인, 이 머신 기준):
- MSBuild: `C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe`
- Git Bash 에서는 `-p:` 대시 프리픽스를 쓴다(`//p:` 는 깨짐)
- 빌드에 1~2분 걸릴 수 있으니 Bash 툴 타임아웃을 300000 으로 준다
- 실행 중인 프로세스가 산출물을 잠그고 있으면(MSB3021/3026/3027/3030) **프로세스를 절대 죽이지 말 것**(이
  프로젝트 하드 규칙) — 스크래치 `-p:OutDir=<scratchpad>/build-verify/` 로 컴파일만 재검증하고 SUMMARY 에
  "산출물 잠김으로 스크래치 컴파일 검증" 이라고 남긴다
- Debug/x64 빌드 warning 기존 baseline = 정확히 12줄(`CS0618`×10 + `CS0162`×2). "0경고" 를 기준으로 삼지
  말 것 — 이 baseline 은 이번 작업과 무관한 기존 경고다. 목표는 **신규 warning 0 / 신규 error 0**.
</interfaces>
</context>

<tasks>

<task type="auto">
  <name>Task 1: 워밍업 게이트 플래그 + 워밍업 서비스 + TCP 게이트 배선</name>
  <files>WPF_Example/SystemHandler.cs, WPF_Example/Custom/SystemHandler.cs</files>
  <action>
**1) `WPF_Example/SystemHandler.cs`** — `IsRecipeReady` 블록(위 interfaces 참고) 바로 다음에 추가:
```csharp
        //260814 hbk quick-260814-dxy: 측정 파이프라인 워밍업(Custom/SystemHandler.cs StartMeasureWarmupAsync)
        //  완료 신호. IsRecipeReady 와 별도 플래그인 이유: 레시피 로드는 끝나도 워밍업은 아직 진행 중일 수
        //  있어서다. ProcessTest / Btn_start_Click / Btn_batchRun_Click 게이트로 쓰인다.
        private volatile bool _isMeasureWarmupComplete = false;
        public bool IsMeasureWarmupComplete { get { return _isMeasureWarmupComplete; } set { _isMeasureWarmupComplete = value; } }
```

**2) `WPF_Example/Custom/SystemHandler.cs`** — using 2개 추가(파일 최상단 using 목록 아무 곳):
```csharp
using System.IO;
using ReringProject.Halcon.Models;
```

**3) 같은 파일 — `ProcessTest` 안, `if (!IsRecipeReady) {...}` 블록 바로 다음, `packet.TestID = ...` 줄
바로 앞에 게이트 추가(K&R 스타일 유지, ProcessTest 메서드 자체는 이 삽입 외에 손대지 않는다):**
```csharp
            //260814 hbk quick-260814-dxy: 측정 파이프라인 워밍업 완료 전에는 TEST 거부 — Release 콜드스타트
            //  임시 완화(StartMeasureWarmupAsync). "완전 해결"이 아니라 "완화 시도" — top-release-2x-slower.md.
            if (!IsMeasureWarmupComplete) {
                Logging.PrintLog((int)ELogType.Error, "[MeasureWarmup] TEST rejected — 측정 파이프라인 워밍업 진행 중(IsMeasureWarmupComplete=false)");
                return false;
            }
```

**4) 같은 파일 — `SendTestError` 메서드의 닫는 `}` 직후, `ProcessAlignTest` 메서드 시작 전에 아래 5개
메서드를 통째로 삽입한다(Allman 브레이스 + 헝가리언 `b`/`n` 로컬 접두 — 이 지점부터 파일의 최신 컨벤션):**
```csharp
        //260814 hbk quick-260814-dxy: Release 콜드스타트 measureExec(MeasurePos/MeasurePairs) 수 배~10배
        //  저하(.planning/debug/top-release-2x-slower.md) 원인 불명 임시 완화. 그 비용을 실제 검사 사이클이
        //  아니라 기동 시점에 미리 확정적으로 치르게 한다 — "완전 해결"이 아니라 "완화 시도"임을 명심할 것.
        //  레시피 로드 직후 MainWindow.Window_ContentRendered_LoadRecipe 가 호출한다.
        private const int MEASURE_WARMUP_ITERATIONS = 15; // 관측된 워밍업 문턱(7~36회+, 들쭉날쭉)의 중간값 근사치
        private const int MEASURE_WARMUP_SYNTHETIC_IMAGE_SIZE = 2048; // 저장 이미지가 전혀 없을 때만 쓰는 최후 폴백

        //260814 hbk 워밍업 진입점 — UI 스레드를 블로킹하지 않도록 Task.Run 으로 던진다.
        //  Shot 이 하나도 없으면(레시피 없음/구형식) 즉시 게이트를 연다 — 영원히 막히면 안 된다.
        public void StartMeasureWarmupAsync()
        {
            bool bHasShots = Sequences != null && Sequences.RecipeManager != null && Sequences.RecipeManager.ShotCount > 0;
            if (!bHasShots)
            {
                Logging.PrintLog((int)ELogType.Trace, "[MeasureWarmup] 대상 Shot 없음 — 워밍업 스킵, 즉시 게이트 개방");
                IsMeasureWarmupComplete = true;
                return;
            }
            Task.Run(() =>
            {
                try
                {
                    RunMeasureWarmup();
                }
                catch (Exception ex)
                {
                    Logging.PrintLog((int)ELogType.Error, "[MeasureWarmup] 예외 — 워밍업 실패, 정상 기동 계속: {0}", ex.Message);
                }
                finally
                {
                    IsMeasureWarmupComplete = true;
                }
            });
        }

        //260814 hbk 대표 Shot 하나를 골라 그 FAI/Measurement 를 N회 반복 실행(TryExecuteMeasurement 와
        //  동일한 meas.TryExecute 호출 경로). EvaluateJudgement/ClearResult 는 호출하지 않는다 — 결과를
        //  완전히 버려서 실제 판정 로직/화면 표시에 어떤 영향도 주지 않는다.
        private void RunMeasureWarmup()
        {
            Stopwatch sw = Stopwatch.StartNew();
            HImage img = null;
            try
            {
                bool bIsSynthetic;
                ShotConfig shot = FindMeasureWarmupShot(out img, out bIsSynthetic);
                if (shot == null || img == null)
                {
                    Logging.PrintLog((int)ELogType.Trace, "[MeasureWarmup] 측정 항목 있는 Shot 없음 — 워밍업 스킵");
                    return;
                }

                int nSuccessCount = 0;
                int nFailCount = 0;
                for (int i = 0; i < MEASURE_WARMUP_ITERATIONS; i++)
                {
                    foreach (FAIConfig fai in shot.FAIList)
                    {
                        foreach (MeasurementBase meas in fai.Measurements)
                        {
                            bool bOk = TryWarmupOneMeasurement(meas, img);
                            if (bOk) nSuccessCount++;
                            else nFailCount++;
                        }
                    }
                }

                Logging.PrintLog((int)ELogType.Trace,
                    "[MeasureWarmup] 완료 shot={0} iterations={1} synthetic={2} success={3} fail={4} elapsed={5}ms",
                    shot.ShotName, MEASURE_WARMUP_ITERATIONS, bIsSynthetic, nSuccessCount, nFailCount, sw.ElapsedMilliseconds);
            }
            finally
            {
                if (img != null) img.Dispose();
            }
        }

        //260814 hbk 단일 측정 1회 실행 — 실제 TryExecuteMeasurement(Action_FAIMeasurement.cs) 의 DualImage
        //  주입 패턴을 그대로 미러링한다. datumTransform=null 은 MeasurementBase.TryExecute 계약상 identity
        //  와 동일(EdgeToLineDistanceMeasurement 등에서 이미 null 체크로 identity 처리하는 기존 관례).
        private bool TryWarmupOneMeasurement(MeasurementBase meas, HImage img)
        {
            DualImageEdgeDistanceMeasurement dualMeas = meas as DualImageEdgeDistanceMeasurement;
            bool bIsDual = dualMeas != null;
            if (bIsDual)
            {
                dualMeas.RuntimeImageA = img;
                dualMeas.RuntimeImageB = img;
            }
            try
            {
                double resultValue;
                string error;
                List<EdgeInspectionOverlay> overlays;
                return meas.TryExecute(img, null, 1.0, out resultValue, out error, out overlays);
            }
            catch
            {
                return false;
            }
            finally
            {
                if (bIsDual)
                {
                    dualMeas.RuntimeImageA = null;
                    dualMeas.RuntimeImageB = null;
                }
            }
        }

        //260814 hbk 워밍업용 Shot+더미이미지 선택. 우선순위: (1) 측정 있는 Shot 중 SimulImagePath 파일이
        //  실존하는 첫 Shot(진짜 코드 경로 재현, 가장 신뢰도 높음) → (2) 그런 Shot 이 하나도 없으면 측정
        //  있는 첫 Shot + 합성 이미지(GenImageConst, 캐시 워밍 목적이라 검출 성공 여부 무관).
        private ShotConfig FindMeasureWarmupShot(out HImage img, out bool bIsSynthetic)
        {
            img = null;
            bIsSynthetic = false;
            ShotConfig shotWithMeasurements = null;

            foreach (ShotConfig shot in Sequences.RecipeManager.Shots)
            {
                bool bHasMeasurements = ShotHasAnyMeasurement(shot);
                if (!bHasMeasurements) continue;
                if (shotWithMeasurements == null) shotWithMeasurements = shot;

                bool bHasValidImage = !string.IsNullOrEmpty(shot.SimulImagePath) && File.Exists(shot.SimulImagePath);
                if (!bHasValidImage) continue;
                try
                {
                    img = new HImage(shot.SimulImagePath);
                    return shot;
                }
                catch
                {
                    img = null; // 이 Shot 은 포기, 다음 Shot 계속 탐색
                }
            }

            if (shotWithMeasurements == null) return null;

            try
            {
                HObject hobjConst;
                HOperatorSet.GenImageConst(out hobjConst, "byte", MEASURE_WARMUP_SYNTHETIC_IMAGE_SIZE, MEASURE_WARMUP_SYNTHETIC_IMAGE_SIZE);
                img = new HImage(hobjConst);
                bIsSynthetic = true;
                return shotWithMeasurements;
            }
            catch
            {
                img = null;
                return null;
            }
        }

        private bool ShotHasAnyMeasurement(ShotConfig shot)
        {
            if (shot.FAIList == null) return false;
            foreach (FAIConfig fai in shot.FAIList)
            {
                if (fai.Measurements != null && fai.Measurements.Count > 0) return true;
            }
            return false;
        }
```

**절대 하지 말 것:**
- `WPF_Example/DatumMeasurement.csproj`, `WPF_Example/Custom/EthernetVision/PickerCenterCalibrationService.cs`
  — 열지도 말 것.
- 삼항연산자 금지, TryExecute 성공 시 `EvaluateJudgement`/`ClearResult` 호출 금지(판정 로직 오염 방지).
- 새 `.cs` 파일 생성 금지(csproj 편집 불필요하게 만드는 게 이 설계의 핵심).
  </action>
  <verify>
    <automated>F1=WPF_Example/SystemHandler.cs && F2=WPF_Example/Custom/SystemHandler.cs && echo "=== [1] IsMeasureWarmupComplete 프로퍼티 : 1 기대 ===" && grep -c "public bool IsMeasureWarmupComplete" "$F1" && echo "=== [2] StartMeasureWarmupAsync 정의 : 1 기대 ===" && grep -c "public void StartMeasureWarmupAsync" "$F2" && echo "=== [3] 신규 메서드 4종 정의 : 각 1 기대 ===" && for m in RunMeasureWarmup TryWarmupOneMeasurement FindMeasureWarmupShot ShotHasAnyMeasurement; do grep -c "private .*$m(" "$F2"; done && echo "=== [4] ProcessTest 게이트 : 1 기대 ===" && grep -c "if (!IsMeasureWarmupComplete)" "$F2" && echo "=== [5] EvaluateJudgement/ClearResult 미호출 확인(0 기대) ===" && awk '/StartMeasureWarmupAsync/,/private bool ShotHasAnyMeasurement/' "$F2" | grep -c "EvaluateJudgement\|ClearResult" && echo "=== [6] using 2개 추가 확인 : 각 1 기대 ===" && grep -c "^using System.IO;" "$F2" && grep -c "^using ReringProject.Halcon.Models;" "$F2" && echo "=== [7] 금지 파일 무변경(해시 baseline 과 동일해야 함) ===" && git diff -- WPF_Example/DatumMeasurement.csproj | git hash-object --stdin && git diff -- WPF_Example/Custom/EthernetVision/PickerCenterCalibrationService.cs | git hash-object --stdin && echo "=== [8] Debug/x64 빌드 ===" && "/c/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/MSBuild.exe" "WPF_Example/DatumMeasurement.csproj" -p:Configuration=Debug -p:Platform=x64 -v:minimal -nologo 2>&1 | grep -iE "error CS|error MSB|warning CS|Build succeeded"</automated>
  </verify>
  <done>
- [1]~[4], [6] 전부 정확히 `1`.
- [3] 4개 메서드 모두 `1`.
- [5] `0` (경고: awk 범위 특성상 `RunMeasureWarmup` 시작 직전 마지막 줄도 일부 포함될 수 있으니, 만약 0이
  아니면 반드시 실제 위치를 눈으로 확인해 새로 추가한 코드 안에서의 호출인지 판별할 것 — 새 코드 안에서는
  0이어야 정상).
- [7] 두 해시가 각각 `3daa3bef520786d331716fb77bc93e2eb632b966` / `86d1071909389cdb13b4ff8f3032489aff26e2fe`
  와 동일 (baseline 과 완전 일치, 이번 작업으로 변경 없음).
- [8] `Build succeeded`, 신규 `error CS`/`error MSB` 0건. 산출물 잠김이면 스크래치 OutDir 컴파일 성공으로
  대체하고 SUMMARY 에 기록.
  </done>
</task>

<task type="auto">
  <name>Task 2: 앱 시작 워밍업 기동 배선 + UI RUN/일괄검사 게이트</name>
  <files>WPF_Example/MainWindow.xaml.cs, WPF_Example/UI/ControlItem/InspectionListView.xaml.cs</files>
  <action>
**1) `WPF_Example/MainWindow.xaml.cs`** — `Window_ContentRendered_LoadRecipe` 전체를 아래로 교체(두 분기
모두에 `StartMeasureWarmupAsync()` 호출 추가, 그 외 로직은 100% 동일):
```csharp
        private void Window_ContentRendered_LoadRecipe(object sender, EventArgs e) {
            this.ContentRendered -= Window_ContentRendered_LoadRecipe; // 1회 실행 후 구독 해제
            if (mSystemHandler.Setting.CurrentRecipeName == null) {
                mSystemHandler.IsRecipeReady = true; // 레시피 없어도 guard 해제
                mSystemHandler.StartMeasureWarmupAsync(); //260814 hbk quick-260814-dxy: 레시피 없음 — 내부에서 즉시 게이트 개방
                return;
            }
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background, new Action(() => {
                mSystemHandler.LoadRecipe(mSystemHandler.Setting.CurrentRecipeName);
                mSystemHandler.IsRecipeReady = true; //260615 hbk Phase 43.2: 로드 완료(성공/실패 무관) → TCP guard 해제 (D-B)
                mSystemHandler.StartMeasureWarmupAsync(); //260814 hbk quick-260814-dxy: 레시피 로드 직후 측정 파이프라인 워밍업 백그라운드 시작(UI 스레드 논블로킹)
            }));
        }
```

**2) `WPF_Example/UI/ControlItem/InspectionListView.xaml.cs`** — `Btn_start_Click` 메서드 첫 줄(`if
(treeListBox_sequence.SelectedIndex < 0) return;` 바로 앞)에 게이트 삽입:
```csharp
        private void Btn_start_Click(object sender, RoutedEventArgs e) {
            //260814 hbk quick-260814-dxy: 측정 파이프라인 워밍업 완료 전에는 수동 RUN 도 막는다(TCP $TEST 와 동일 게이트).
            if (!SystemHandler.Handle.IsMeasureWarmupComplete) {
                CustomMessageBox.Show("측정 파이프라인 준비 중",
                    "앱이 측정 파이프라인을 준비하는 중입니다. 잠시 후 다시 시도하세요.",
                    MessageBoxImage.Warning);
                return;
            }
            if (treeListBox_sequence.SelectedIndex < 0) return;
```
그 아래 나머지 본문은 전혀 손대지 않는다.

**3) 같은 파일** — `Btn_batchRun_Click` 메서드 첫 줄(`var root = ...` 바로 앞)에 동일 패턴 게이트 삽입:
```csharp
        private void Btn_batchRun_Click(object sender, RoutedEventArgs e) {
            //260814 hbk quick-260814-dxy: 측정 파이프라인 워밍업 완료 전에는 일괄검사도 막는다.
            if (!SystemHandler.Handle.IsMeasureWarmupComplete) {
                CustomMessageBox.Show("측정 파이프라인 준비 중",
                    "앱이 측정 파이프라인을 준비하는 중입니다. 잠시 후 다시 시도하세요.",
                    MessageBoxImage.Warning);
                return;
            }
            var root = treeListBox_sequence.Items.Count > 0 ? treeListBox_sequence.Items[0] as NodeViewModel : null;
```
`var root = ... ? ... : ...;` 줄의 기존 삼항연산자는 이번 작업 범위 밖이므로 그대로 둔다(건들지 않는다).

**절대 하지 말 것:**
- `Btn_start_Click`/`Btn_batchRun_Click` 본문의 나머지 로직(선택 검증, 오프라인 모드 확인 팝업 등) 변경 금지.
- `WPF_Example/DatumMeasurement.csproj`, `PickerCenterCalibrationService.cs` — 열지도 말 것.
  </action>
  <verify>
    <automated>F1=WPF_Example/MainWindow.xaml.cs && F2=WPF_Example/UI/ControlItem/InspectionListView.xaml.cs && echo "=== [1] MainWindow StartMeasureWarmupAsync 호출 : 2 기대 ===" && grep -c "StartMeasureWarmupAsync()" "$F1" && echo "=== [2] InspectionListView 게이트 체크 : 2 기대 ===" && grep -c "SystemHandler.Handle.IsMeasureWarmupComplete" "$F2" && echo "=== [3] Btn_start_Click/Btn_batchRun_Click 존재 : 각 1 기대(무변경 확인) ===" && grep -c "private void Btn_start_Click" "$F2" && grep -c "private void Btn_batchRun_Click" "$F2" && echo "=== [4] 금지 파일 무변경(해시 baseline 과 동일해야 함) ===" && git diff -- WPF_Example/DatumMeasurement.csproj | git hash-object --stdin && git diff -- WPF_Example/Custom/EthernetVision/PickerCenterCalibrationService.cs | git hash-object --stdin && echo "=== [5] Debug/x64 빌드 ===" && "/c/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/MSBuild.exe" "WPF_Example/DatumMeasurement.csproj" -p:Configuration=Debug -p:Platform=x64 -v:minimal -nologo 2>&1 | grep -iE "error CS|error MSB|warning CS|Build succeeded"</automated>
  </verify>
  <done>
- [1] `2` — `Window_ContentRendered_LoadRecipe`의 두 분기(레시피 없음/있음) 모두에서 `StartMeasureWarmupAsync()` 호출.
- [2] `2` — `Btn_start_Click`/`Btn_batchRun_Click` 각각에 `SystemHandler.Handle.IsMeasureWarmupComplete` 게이트 1회씩.
- [3] 둘 다 `1` — 두 메서드가 중복 생성되거나 깨지지 않고 정확히 하나씩 존재.
- [4] 두 해시가 각각 `3daa3bef520786d331716fb77bc93e2eb632b966` / `86d1071909389cdb13b4ff8f3032489aff26e2fe`와 동일 (baseline과 완전 일치, 이번 작업으로 변경 없음).
- [5] `Build succeeded`, 신규 `error CS`/`error MSB` 0건, warning은 기존 baseline 12줄(`CS0618`×10 + `CS0162`×2)과 정확히 동일(신규 warning 0). 산출물 잠김이면 스크래치 OutDir 컴파일 성공으로 대체하고 SUMMARY에 기록.
  </done>
</task>

</tasks>