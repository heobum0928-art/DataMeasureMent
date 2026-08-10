---
phase: quick-260810-egx
plan: 01
type: execute
wave: 1
depends_on: []
files_modified:
  - WPF_Example/Setting/SystemSetting.cs
  - WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs
  - WPF_Example/MainWindow.xaml.cs
autonomous: true
requirements: [EGX-01, EGX-02, EGX-03, EGX-04]
must_haves:
  truths:
    - "자동검사(TCP $PREP/$TEST) 사이클에서는 검사 중 화면 이미지/오버레이/결과행 갱신이 수행되지 않는다"
    - "수동 RUN 버튼 / 티칭 / 일괄검사(RepeatRunService·BatchRunService) 는 지금과 동일하게 실시간 표시된다"
    - "저장되는 capture 이미지(오버레이 렌더 포함)는 OK/NG 전부 기존과 완전히 동일하다"
    - "판정·측정값·엑셀/cycle.json export·TCP 응답은 전혀 바뀌지 않는다"
    - "설정 DisableViewerDuringAutoInspect=false 면 기존 동작 그대로다(회귀 0)"
  artifacts:
    - path: "WPF_Example/Setting/SystemSetting.cs"
      provides: "DisableViewerDuringAutoInspect bool 설정(기본 false = 기존 동작)"
      contains: "DisableViewerDuringAutoInspect"
    - path: "WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs"
      provides: "자동검사 시 표시용 127MP CopyImage 생략"
      contains: "DisableViewerDuringAutoInspect"
    - path: "WPF_Example/MainWindow.xaml.cs"
      provides: "자동검사 시 DisplayActionContext/DisplaySequenceContext 호출 억제"
      contains: "IsProtocolDrivenCycle"
  key_links:
    - from: "Action_FAIMeasurement.EStep.Grab"
      to: "InspectionSequence.IsProtocolDrivenCycle"
      via: "ShotParam.Parent as InspectionSequence"
      pattern: "IsProtocolDrivenCycle"
    - from: "MainWindow.OnSequenceFinish"
      to: "MainView.DisplaySequenceContext"
      via: "게이트 후 조건부 호출"
      pattern: "DisplaySequenceContext"
---

<objective>
자동검사(TCP `$PREP`/`$TEST`) 사이클에서 **화면 실시간 표시를 끄는 모드**를 만든다. 사용자 발언: "아냐 캡쳐화면은 남겨야돼 실시간으로 보여지는것만", "오토검사때 이미지뷰어 끄는 모드 하면 더 빨라질듯".

**앞서 계획했던 'NG 만 렌더링 저장' 방향은 완전히 취소됨.** `QueueFaiCapture` / `CaptureImageSaveService` / `NeedsRender` 관련 로직은 **한 줄도 건드리지 않는다.** 저장되는 캡쳐 이미지는 OK/NG 전부 지금 그대로 남는다.

Purpose: 검사 스레드에서 발생하는 **표시 전용 127MP 이미지 복사 2회/Shot**(Action 사본 + SequenceContext clone)과 UI 스레드의 대용량 뷰어 로드를 자동검사 중에만 제거한다.
Output: SystemSetting bool 1개 + Action_FAIMeasurement 표시사본 게이트 + MainWindow 표시 호출 게이트.

**효과에 대한 정직한 전제(근거 없는 주장 금지):**
오케스트레이터 로그 분석의 유력 가설은 "저장 워커의 오버레이 렌더(127MP, 실측 ~1초/장)가 CPU 를 점유해 측정 스레드를 굶긴다"였다. **이번 변경은 그 저장 렌더를 그대로 남기므로, 1.2~1.3초 측정 간 공백이 이 변경만으로 해소된다는 보장은 없다.** 이번 변경이 확실히 제거하는 것은 "표시 목적의 127MP 메모리 복사 2회/Shot + 뷰어 로드"뿐이다. Task 4 에서 Trace 로그의 측정 간 간격으로 실제 효과를 재측정하고, **효과가 없으면 병목이 저장 렌더 쪽이라는 뜻**이므로 그 사실을 SUMMARY 에 명시한다.
</objective>

<execution_context>
@$HOME/.claude/get-shit-done/workflows/execute-plan.md
@$HOME/.claude/get-shit-done/templates/summary.md
</execution_context>

<context>
@./CLAUDE.md
@.planning/STATE.md
@WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs
@WPF_Example/MainWindow.xaml.cs
@WPF_Example/UI/ContentItem/MainView.xaml.cs
@WPF_Example/Sequence/Sequence/SequenceContext.cs
@WPF_Example/Setting/SystemSetting.cs

<pre_verified_findings>
플래너가 코드로 직접 확인한 사실이다. 재조사하지 말고 그대로 사용할 것.

### 1. 표시 경로 전체 지도 (확정)

```
Action_FAIMeasurement.EStep.Grab (검사 스레드)
  ShotParam.SetImage(image)                      ← 측정 소스. 데이터 경로. 절대 건드리지 말 것.
  pMyContext.ResultHalconImage = image.CopyImage()   ← 표시용 사본 #1 (127MP memcpy, 281행)
        ↓ SequenceBase.ExecuteAction:220/231
  Context.CopyFrom(actionContext)
        ↓ SequenceContext.CopyFrom:172
  ResultHalconImage = HalconImageBridge.Clone(...)    ← 표시용 사본 #2 (또 127MP memcpy, 검사 스레드!)
        ↓ OnActionChanged / OnFinish 이벤트
MainWindow.OnActionChanged:172-184  → BeginInvoke → mainView.DisplayActionContext → RefreshFAIResultRows()
MainWindow.OnSequenceFinish:190-199 → BeginInvoke → mainView.DisplaySequenceContext → DisplayContextToViewer → halconViewer.LoadImage(127MP)
MainWindow.OnSequenceError:163-170  → 동일
```

**즉 Shot 하나마다 표시 목적으로 127MP 복사가 2회 일어나고, 두 번째(SequenceContext.CopyFrom)는 검사 스레드에서 동기 실행된다.** 표시를 끄면 이 두 복사가 사라진다.

MainWindow 의 4개 핸들러는 전부 `Dispatcher.BeginInvoke`(비동기) 라 **UI 작업 자체가 검사 스레드를 블로킹하지는 않는다.** 따라서 UI 쪽 이득은 "UI 스레드/HALCON 윈도우 자원 경합 완화"이지 직접적 검사 스레드 대기 제거가 아니다 — 과장 금지.

### 2. `ResultHalconImage` 소비처 전수 확인 (grep 완료, 이게 전부)

| 위치 | 성격 |
|------|------|
| `MainView.DisplayContextToViewer:1669-1673` | **순수 표시** |
| `SequenceBase.SaveResultImage:430-431` | 이미지 저장 — 단, **`SaveFailImage == false` 면 425행에서 조기 return** (기본값 false) |
| `InspectionListView.xaml.cs:690-692` | 배치 정리용 Dispose (null 안전) |
| `SequenceContext.Clear/CopyFrom`, `ActionContext.Clear/CopyFrom` | 생명주기 관리 |

**판정·측정값·엑셀/cycle.json/TCP 응답은 `ResultHalconImage` 를 전혀 읽지 않는다.**
- 엑셀/cycle.json 의 이미지 경로는 `FAIConfig.LastOriginImageFileName` / `LastCaptureImageFileName`(CaptureImageSaveService 경로) 과 `ShotConfig.GetLatestImagePath()` 를 쓴다 — 이번 변경과 무관.
- 판정/측정값은 `ShotParam.GetImage()`(=`SetImage` 로 넣은 원본)로 수행된다 — 이번 변경과 무관.

→ **단, `SaveFailImage == true` 인 경우에는 `ResultHalconImage` 가 데이터(저장) 경로가 된다.** 그래서 skip 조건에 `!SaveFailImage` 가드를 반드시 포함한다(아래 Task 2).

`HalconImageBridge.Clone(null)` 은 `null` 반환(HalconImageBridge.cs:13-16) → 표시사본을 안 만들면 clone 도 자동으로 null 이 되어 뒤쪽이 조용히 no-op 이 된다. `DisplayContextToViewer` 는 이미지가 null 이면 `ResultImagePath` 폴백을 시도하고, 그것도 없으면 false 반환(예외 없음).

### 3. 자동 vs 수동 판별 — `IsProtocolDrivenCycle()` 이 정확히 맞다 (재확인 완료)

`InspectionSequence.cs:1163-1167`: `return RequestPacket != null;`
같은 파일 1157-1162행 주석이 계약을 명시 — 수동 UI RUN(`Start(EAction)` → `StartCore(.., null)`), `RepeatRunService` 배치런(`StartAll(null)`) 은 **항상 packet==null → false**. `$PREP`/`$TEST` 프로토콜 사이클만 true. 이미 코드 3곳(`InspectionSequence:301,1478`, `Action_FAIMeasurement:373,606`)이 같은 용도로 쓰고 있다. **새 판별 로직을 만들지 말 것.**

접근 경로:
- `Action_FAIMeasurement` 안: `ShotParam.Parent as InspectionSequence` (기존 290행 패턴 그대로)
- `MainWindow.OnSequenceFinish/Error/Stop`: `context.Source as InspectionSequence` (SequenceContext.Source 는 SequenceBase)
- `MainWindow.OnActionChanged`: `context.Source` 는 `ActionBase` → `context.Source.Param.Parent as InspectionSequence` (`ActionBase.Param` = ActionBase.cs:20, `ParamBase.Parent` = ParamBase.cs:54)

**경합 주의:** MainWindow 핸들러는 `Dispatcher.BeginInvoke` 로 UI 스레드에 지연 실행된다. UI 스레드가 도는 시점엔 `RequestPacket` 이 이미 클리어됐을 수 있으므로, **판별은 반드시 핸들러 진입 직후(=시퀀스 스레드)에서 계산해 로컬 bool 로 캡처한 뒤 람다 안에서 그 로컬을 쓴다.** 람다 안에서 `IsProtocolDrivenCycle()` 을 호출하면 안 된다.

### 4. INI 로드 기본값 함정 (설정 극성 결정 근거)

`SystemSetting.Load()` (SystemSetting.cs:294-297) 는 INI 에 키가 없으면 `ToBool()` → **false** 를 강제 SetValue 한다. 즉 C# 프로퍼티 초기화값 `= true` 는 기존 `Setting.ini` 사용자에게 **무조건 무시되고 false** 가 된다.
→ 어떤 이름을 쓰든 기존 설치본의 실효 기본값은 false 다. 그러므로 **false = 기존 동작(표시 유지)** 이 되도록 극성을 잡는다: `DisableViewerDuringAutoInspect = false`. 사용자가 설정창에서 켜서 쓴다. (부수 효과로 "화면이 안 보이는" 눈에 띄는 동작 변경이 opt-in 이 되어 안전하다.)
</pre_verified_findings>
</context>

<tasks>

<task type="auto">
  <name>Task 1: SystemSetting 에 DisableViewerDuringAutoInspect 추가</name>
  <files>WPF_Example/Setting/SystemSetting.cs</files>
  <action>
`OfflineInspectMode`(169행) 인근 `[Category("System|Enviroment")]` 블록에 bool 프로퍼티 `DisableViewerDuringAutoInspect` 를 추가한다. 기본값 `= false`.

의미: **true = 자동검사(TCP $PREP/$TEST) 사이클 동안 화면 실시간 표시를 끈다(tact 우선). false = 기존과 동일하게 표시.** 수동 RUN/티칭/일괄검사는 이 설정과 무관하게 항상 표시된다.

주석으로 남길 것(파일의 기존 한국어 주석 스타일 유지):
- 왜 존재하는지: 표시 목적의 127MP 이미지 복사 2회/Shot + 뷰어 로드를 자동검사 중에만 제거
- 저장되는 캡쳐 이미지는 이 설정과 무관하게 항상 그대로 저장된다는 것(오해 방지)
- 누락 INI 키 → false 로드(SystemSetting.Load 294-297행) 라 기존 설치본은 자동으로 기존 동작 유지

`[Category("System|Enviroment")]` 를 붙여 설정창(PropertyTools)에 노출한다. 다른 프로퍼티는 손대지 않는다.
  </action>
  <verify>
    <automated>grep -v '^\s*//' WPF_Example/Setting/SystemSetting.cs | grep -c 'public bool DisableViewerDuringAutoInspect { get; set; } = false;'  # 1 이어야 함</automated>
  </verify>
  <done>`DisableViewerDuringAutoInspect` bool 이 기본값 false, Category 어트리뷰트, 의미 주석과 함께 존재.</done>
</task>

<task type="auto">
  <name>Task 2: 자동검사 시 표시용 127MP 사본 생성 생략</name>
  <files>WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs</files>
  <action>
`Action_FAIMeasurement` 에 판별 헬퍼를 하나 추가한다(이름 예: `IsViewerUpdateSkipped(InspectionSequence parentSeq)`). 반환 true 조건은 **세 가지 AND**:
1. `parentSeq != null && parentSeq.IsProtocolDrivenCycle()` — 자동(프로토콜) 사이클
2. `SystemHandler.Handle.Setting.DisableViewerDuringAutoInspect == true`
3. `SystemHandler.Handle.Setting.SaveFailImage == false`

3번 가드가 핵심이다: `SaveFailImage` 가 켜져 있으면 `SequenceBase.SaveResultImage`(425-431행)가 `Context.ResultHalconImage` 를 실제 저장 소스로 쓴다 → 표시사본을 없애면 결과이미지 저장이 조용히 깨진다. 이 가드로 "데이터 경로 영향 0" 을 코드로 보장한다. 근거를 주석으로 남길 것. `Setting` 이 null 인 극단 상황은 false(=기존 동작) 로 폴백한다. 삼항 신규 도입 금지, if/else 로 전개, 변수명 `bXxx` 관례 유지.

적용 지점 2곳:

**(a) `EStep.Grab` 280-282행** — `ShotParam.SetImage(image);` 는 **측정 소스이므로 절대 건드리지 않는다.** 그 다음의 표시사본만 게이트한다:
- skip 인 경우: 기존 `pMyContext.ResultHalconImage` 가 있으면 Dispose 하고 `null` 로 둔 뒤, `image.CopyImage()` 를 **하지 않는다**(127MP memcpy 제거). `image.Dispose()` 는 기존대로 반드시 수행 — 누수 금지.
- skip 아닌 경우: 기존 코드 그대로.
- 여기서 부모 시퀀스는 `ShotParam.Parent as InspectionSequence` 로 얻는다(290행 기존 패턴 재사용).

**(b) 크로스-Z 표시 교체 블록 478-486행** — `bShotDisplayImageReplaced` 로 첫 크로스-Z 이미지를 화면용으로 채택하는 블록. skip 인 경우 이 블록 전체(Dispose + `crossZRoleImage.CopyImage()` + Trace 로그)를 건너뛴다. **주의: `crossZRoleImage` 자체의 Dispose(490행 finally)와 `crossZSharedSrc.Release()`(476행 finally)는 조건과 무관하게 그대로 유지** — 저장 경로(`AggregateFaiResult`/`QueueFaiCapture`)는 이번 변경 대상이 아니므로 **한 줄도 수정하지 말 것.**

`//260810 hbk quick-260810-egx:` 접두사 주석으로 의도를 남긴다. 파일의 기존 K&R 스타일 유지. `QueueFaiCapture`, `AggregateFaiResult`, `SharedHImage`/`CaptureImageSaveRequest` 관련 코드는 **전혀 건드리지 않는다.**
  </action>
  <verify>
    <automated>grep -v '^\s*//' WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs | grep -c 'DisableViewerDuringAutoInspect'  # >=1</automated>
  </verify>
  <done>자동검사 + 설정 ON + SaveFailImage OFF 일 때 `pMyContext.ResultHalconImage` 가 null 로 유지되고 표시용 CopyImage 2곳이 실행되지 않는다. `ShotParam.SetImage`/`image.Dispose`/저장 경로는 무변경.</done>
</task>

<task type="auto">
  <name>Task 3: 자동검사 시 MainWindow 표시 호출 억제</name>
  <files>WPF_Example/MainWindow.xaml.cs</files>
  <action>
표시 이벤트 핸들러 3곳에서 `mainView.Display*` 호출만 조건부로 만든다. **`SetManualToolsEnabled(true)` 와 `statusBar.Model.SetText(...)` 와 `Logging.PrintLog` 는 절대 건드리지 않는다** — 잠금 해제 누락은 과거 실사고(SequenceBase.cs:419-422 주석 참고) 이력이 있는 지점이다.

공통 규칙: 판별 bool 을 **핸들러 진입 직후(시퀀스 스레드)** 에서 계산해 로컬에 캡처하고, `Dispatcher.BeginInvoke` 람다 안에서는 그 로컬만 읽는다(람다 안에서 `IsProtocolDrivenCycle()` 재호출 금지 — UI 스레드 도달 시점엔 RequestPacket 이 이미 클리어됐을 수 있음).

판별식: `SystemHandler.Handle.Setting.DisableViewerDuringAutoInspect == true` **그리고** 해당 시퀀스가 `InspectionSequence` 이고 `IsProtocolDrivenCycle()` 이 true. (Setting null 또는 캐스팅 실패 시 false = 표시 유지.) 중복을 피하려면 `private bool ShouldSkipViewerUpdate(SequenceBase seq)` 같은 private 헬퍼 하나로 뽑는다.

- `OnSequenceFinish`(190행) / `OnSequenceError`(163행): 시퀀스는 `context.Source`. skip 이면 `mainView.DisplaySequenceContext(context);` 만 건너뛴다.
- `OnActionChanged`(172행): 시퀀스는 `context.Source?.Param?.Parent as InspectionSequence`(ActionBase.Param → ParamBase.Parent). skip 이면 `mainView.DisplayActionContext(context);` 만 건너뛴다(= 검사 중 결과행 실시간 갱신 중단).
- `OnSequenceStop`(152행) 은 애초에 Display 호출이 없으므로 무수정.

주의: 자동검사 사이클 **종료 후** 사용자가 트리/노드를 클릭해 결과를 보는 경로(`MainView.DisplayParam` 등)는 이 게이트와 무관하며 그대로 동작해야 한다 — 그 경로는 건드리지 않는다.

`//260810 hbk quick-260810-egx:` 접두사 주석으로 의도를 남긴다. 파일 기존 스타일 유지.
  </action>
  <verify>
    <automated>grep -v '^\s*//' WPF_Example/MainWindow.xaml.cs | grep -c 'DisableViewerDuringAutoInspect'  # >=1</automated>
  </verify>
  <done>자동검사 중 `DisplaySequenceContext`/`DisplayActionContext` 가 호출되지 않는다. `SetManualToolsEnabled`/상태바/로그는 모든 경로에서 기존대로 호출된다. 수동 경로는 무변경.</done>
</task>

<task type="auto">
  <name>Task 4: 빌드 + 정적 회귀 검증 + tact 효과 측정 절차 문서화</name>
  <files>(빌드/검증 전용, 소스 수정 없음)</files>
  <action>
1. Debug|x64 빌드로 `error CS` 0건 확인. 앱 실행 중이면 exe 잠김으로 마지막 복사 단계만 MSB3027/MSB3021 로 실패할 수 있다 — **코드 실패가 아니므로 사용자 프로세스를 절대 죽이지 말고**, scratchpad 하위 `-p:OutputPath=...` 로 재빌드해 `error CS` 0건을 확인한다.

2. 정적 회귀 확인(git diff 로 직접 확인하고 SUMMARY 에 기록):
   - `QueueFaiCapture` / `CaptureImageSaveService.cs` / `NeedsRender` / `SharedHImage` 관련 코드 **변경 0줄** (이번 요구사항의 핵심 제약)
   - `ShotParam.SetImage(image)` 및 `image.Dispose()` 무변경 — 측정 소스/누수 방지
   - `SetManualToolsEnabled(true)` 가 Finish/Error/Stop 3경로에서 여전히 무조건 호출됨
   - `SaveFailImage` 가드가 skip 조건에 포함되어 있음

3. SUMMARY 에 **실기 tact 측정 절차와 해석 규칙**을 명시한다(사용자가 수행):
   - 설정 OFF 상태로 자동검사 1사이클 → Trace 로그에서 측정(FitLine) 간 최대 간격과 사이클 총 시간 기록
   - 설정 ON 으로 바꾸고 동일 조건 1사이클 → 같은 지표 기록
   - **간격이 여전히 1.2~1.3초로 남으면: 이번 변경은 병목이 아니었다는 뜻이며, 원인은 저장 워커의 `OverlayCaptureRenderer.RenderToHImage`(오버레이 렌더) CPU 경합 쪽이다.** 그 경우 다음 후보는 (a) 저장 워커 우선순위/스로틀 조정, (b) 렌더 해상도 축소(원본 대신 축소본에 오버레이), (c) 저장 렌더 자체를 사이클 종료 후로 미루기 — 어느 것도 이번 변경으로는 다뤄지지 않았음을 명시한다.
   - 근거 없는 효과 주장 금지. 측정값 없이 "빨라졌다"고 쓰지 말 것.

**워킹트리의 기존 미커밋 로컬 변경 2건은 절대 건드리거나 커밋하지 말 것:** `WPF_Example/DatumMeasurement.csproj`(SIMUL_MODE 제거 — 실HW PC 표식), `WPF_Example/SystemHandler.cs`(memory_allocator 주석처리 — 사용자 실험).
  </action>
  <verify>
    <automated>"C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/amd64/MSBuild.exe" "C:/code/DataMeasurement/WPF_Example/DatumMeasurement.csproj" -p:Configuration=Debug -p:Platform=x64 -v:m 2>&1 | grep -c "error CS"  # 0 이어야 함</automated>
  </verify>
  <done>빌드 error CS 0건. 정적 회귀 4건 확인 완료(특히 저장 경로 변경 0줄). tact 측정 절차와 "효과 없을 경우의 해석"이 SUMMARY 에 기록됨. 미커밋 로컬 변경 2건 무접촉.</done>
</task>

</tasks>

<threat_model>
## Trust Boundaries

| Boundary | Description |
|----------|-------------|
| 검사(시퀀스) 스레드 → UI 스레드 | Dispatcher.BeginInvoke 로 컨텍스트 전달, RequestPacket 수명이 그 사이 바뀔 수 있음 |
| 표시 경로 ↔ 데이터 경로 | `ResultHalconImage` 가 `SaveFailImage` ON 일 때만 데이터(저장) 경로가 됨 |

## STRIDE Threat Register

| Threat ID | Category | Component | Disposition | Mitigation Plan |
|-----------|----------|-----------|-------------|-----------------|
| T-EGX-01 | Tampering | `SaveFailImage` 결과이미지 저장 | mitigate | skip 조건에 `!SaveFailImage` 가드 포함(Task 2) — 저장이 켜져 있으면 표시사본을 유지 |
| T-EGX-02 | Denial of Service | 수동툴 잠금(SetManualToolsEnabled) | mitigate | Display 호출만 게이트, 잠금해제/상태바/로그는 무조건 실행(Task 3). 과거 영구잠금 실사고 이력 |
| T-EGX-03 | Repudiation | 검사 중 육안 관찰 불가 | accept | opt-in 설정(기본 false), 저장 캡쳐 이미지는 전량 보존되어 사후 검증 가능 |
| T-EGX-04 | Tampering | RequestPacket 수명 경합 | mitigate | 판별 bool 을 시퀀스 스레드에서 캡처 후 람다에서 로컬만 사용(Task 3) |
| T-EGX-05 | Tampering | 패키지 설치 | accept | 신규 패키지 없음 — 기존 소스 3파일만 수정 |
</threat_model>

<verification>
정적:
- Debug|x64 빌드 `error CS` 0건
- `git diff` 상 `CaptureImageSaveService.cs` 변경 없음, `QueueFaiCapture`/`NeedsRender`/`SharedHImage` 변경 0줄
- `ShotParam.SetImage(image)` / `image.Dispose()` / `crossZSharedSrc.Release()` / `crossZRoleImage.Dispose()` 무변경
- skip 조건 3항(프로토콜 사이클 AND 설정 ON AND `!SaveFailImage`) 모두 존재
- `SetManualToolsEnabled(true)` 3경로 무조건 호출 유지

실기 UAT(사용자 수행):
1. 설정 OFF + 자동검사 → 지금과 동일하게 화면 갱신됨(회귀 0)
2. 설정 ON + 자동검사 → 검사 중 화면 갱신 없음. 그러나 TCP 응답 P/F, 측정값, 엑셀/cycle.json, capture/original 폴더 파일 수·이름이 OFF 때와 동일
3. 설정 ON + **수동 RUN 버튼 / 티칭 / 일괄검사** → 여전히 실시간 표시됨
4. 설정 ON + 자동검사 종료 후 트리/노드 클릭 → 결과 이미지·오버레이 정상 표시
5. `SaveFailImage` ON + 설정 ON + 자동검사 → 결과이미지 저장이 기존대로 동작(가드 검증)
6. tact: OFF/ON A/B 로 Trace 로그 측정 간 최대 간격 비교 — 개선 없으면 병목은 저장 렌더 쪽(objective 의 정직한 전제 참고)
</verification>

<success_criteria>
- 자동검사 사이클에서 실시간 화면 표시가 꺼지고, 표시용 127MP 사본 2회가 생성되지 않는다
- 저장되는 capture/original 이미지는 OK/NG 전부 기존과 동일(저장 코드 변경 0줄)
- 판정·측정값·엑셀/cycle.json·TCP 응답 무영향
- 수동 RUN/티칭/일괄검사 표시 무영향
- 설정 false 로 완전 복귀 가능
- Debug|x64 빌드 error CS 0건
- tact 효과를 측정값으로 기록(효과 없을 경우의 해석까지 문서화)
</success_criteria>

<output>
Create `.planning/quick/260810-egx-tcp-fai-tact-origin-on-off/260810-egx-SUMMARY.md` when done
</output>
</content>
</invoke>
