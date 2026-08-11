---
phase: quick-260811-odo
plan: 01
type: execute
wave: 1
depends_on: []
files_modified:
  - WPF_Example/Utility/CaptureImageSaveService.cs
  - WPF_Example/Sequence/Sequence/SequenceContext.cs
  - WPF_Example/Sequence/Sequence/SequenceBase.cs
  - WPF_Example/UI/ContentItem/MainView.xaml.cs
  - WPF_Example/Custom/Sequence/Inspection/RepeatRunService.cs
  - WPF_Example/UI/ControlItem/InspectionListView.xaml.cs
autonomous: true
requirements: [ODO-01, ODO-02, ODO-03, ODO-04]
must_haves:
  truths:
    - "UI 스레드가 이미 해제된 HImage 를 읽는 것이 구조적으로 불가능하다 — 원시 HImage 참조를 UI 로 넘기는 경로가 코드상 존재하지 않는다"
    - "시퀀스 스레드(ThreadPriority.Highest)는 UI 의 이미지 복사 완료를 어떤 경로에서도 대기하지 않는다 — 소유권 연산은 전부 O(1)"
    - "사이클당 127MP 이미지 복사 횟수가 수정 전과 정확히 동일하다(증가 0) — 택트 회귀 없음"
    - "주 해제자(StartCore→Context.Clear)와 부차 해제자(ExecuteAction→Context.CopyFrom) 양쪽 모두 동일 소유권 모델을 경유한다"
    - "DisableViewerDuringAutoInspect=true 현장 회피책 경로가 수정 전과 동일하게 동작한다 (MainWindow.xaml.cs 무수정)"
    - "트리 노드 클릭 경로(InspectionListView→SetParam→DisplayParam→DisplayContextToViewer)도 동일 모델로 보호된다"
    - "동시 swap/release ↔ acquire/clone 부하에서 sentinel 무결성이 깨지지 않음이 자동 하네스로 실증된다"
    - "Debug/x64 MSBuild 빌드 에러 0"
  artifacts:
    - path: "WPF_Example/Utility/CaptureImageSaveService.cs"
      provides: "SharedHImage.TryAddRef() — 해제 여부를 원자적으로 알려주는 획득 시도 API"
      contains: "TryAddRef"
    - path: "WPF_Example/Sequence/Sequence/SequenceContext.cs"
      provides: "SequenceContext 결과 이미지 소유권 모델(private SharedHImage + Acquire/SetOwned/Clone)"
      contains: "AcquireResultImage"
    - path: "WPF_Example/Sequence/Sequence/SequenceBase.cs"
      provides: "SaveResultImage 가 원시 필드 대신 소유권 API 로 스냅샷 획득"
      contains: "CloneResultImage"
    - path: "WPF_Example/UI/ContentItem/MainView.xaml.cs"
      provides: "DisplayContextToViewer 가 refcount 획득 구간 안에서만 이미지를 읽음(확정 크래시 지점)"
      contains: "AcquireResultImage"
    - path: "scratchpad/odo-harness/RefRaceHarness.cs"
      provides: "동시 swap/release ↔ acquire/clone 레이스 + sentinel 무결성 자동 검증 하네스"
      contains: "SENTINEL"
  key_links:
    - from: "SequenceBase.StartCore / SequenceBase.ExecuteAction"
      to: "SequenceContext.SetResultImageOwned"
      via: "Interlocked.Exchange + 이전 소유권 Release (O(1), 논블로킹)"
      pattern: "SetResultImageOwned"
    - from: "MainView.DisplayContextToViewer"
      to: "SharedHImage.TryAddRef"
      via: "SequenceContext.AcquireResultImage() → 성공 시에만 .Image 접근, finally Release"
      pattern: "AcquireResultImage"
    - from: "SequenceBase.SaveResultImage"
      to: "SequenceContext.CloneResultImage"
      via: "획득→CopyImage→finally Release"
      pattern: "CloneResultImage"
---

<objective>
`SequenceContext.ResultHalconImage` 에 **소유권 모델이 없어서** 발생하는 use-after-dispose 레이스를 구조적으로 제거한다. 실기 오토검사 중 `HImage.CopyImage()` 에서 `AccessViolationException` 으로 프로세스가 즉사하며, AVE 는 Corrupted State Exception 이라 기존 try/catch 방어망이 전부 무력하다.

Purpose: 해제된 힙을 memcpy 하는 코드 경로 자체를 없앤다. 가용성(프로세스 즉사)보다 **정확성 리스크(조용한 메모리 오염 → 잘못된 측정값이 정상 판정으로 PLC 전송)** 가 더 큰 이유로 우선순위가 높다.
Output: refcount 기반 소유권 모델(저장소에 이미 있는 `SharedHImage` 관용구 재사용) + 레이스 자동 검증 하네스 + 잔여 리스크 문서화.

**이 플랜은 방어적 try/catch 를 근본 수정의 대체로 쓰지 않는다.** AVE 는 .NET 4+ 에서 `catch (Exception)` 에 잡히지 않고, 저장소에 `[HandleProcessCorruptedStateExceptions]` 도 없다. 유일한 해법은 "해제된 메모리를 읽는 경로를 없애는 것"이다.
</objective>

<execution_context>
@$HOME/.claude/gsd-core/workflows/execute-plan.md
@$HOME/.claude/gsd-core/templates/summary.md
</execution_context>

<context>
@./CLAUDE.md
@.planning/STATE.md
@WPF_Example/Sequence/Sequence/SequenceContext.cs
@WPF_Example/Sequence/Sequence/SequenceBase.cs
@WPF_Example/Utility/CaptureImageSaveService.cs
@WPF_Example/UI/ContentItem/MainView.xaml.cs
</context>

---

## 확정 사실 (코드 직접 확인 완료 — 재조사 불필요)

### 크래시 콜스택
```
HalconDotNet.HImage.CopyImage()
  <- HalconImageBridge.Clone(HImage)                 HalconImageBridge.cs:18
  <- MainResultViewerControl.LoadImage(HImage)       MainResultViewerControl.xaml.cs:223 or 235
  <- MainView.DisplayContextToViewer                 MainView.xaml.cs:1671
  <- MainView.DisplaySequenceContext                 MainView.xaml.cs:1571-1574
  <- MainWindow.OnSequenceFinish (Dispatcher lambda) MainWindow.xaml.cs:221-223
```

### 해제자 3개 (시퀀스 스레드 또는 MainRun 스레드)
| # | 위치 | 트리거 | 스레드 |
|---|------|--------|--------|
| 주 | `SequenceContext.cs:154-157` (`SequenceContext.Clear`) | `StartCore` (`SequenceBase.cs:348`) — 다음 `$TEST` | MainRun (1ms 폴링) |
| 부차 | `SequenceContext.cs:169-171` (`SequenceContext.CopyFrom`) | `ExecuteAction` (`SequenceBase.cs:220,231`) — 액션 완료마다 | 시퀀스 |
| 부차 | `SequenceContext.cs:147-151` (`Clear` 안의 `act.Context.Clear()` 루프) | 동일 | MainRun |

`Dispose()` 가 `= null` 대입보다 **먼저** 실행되므로 `MainView.xaml.cs:1669` 의 null 체크는 원리적으로 무력하다.

### 독자(reader) 4개 — 이번 조사에서 **UI 독자가 하나 더 발견됨**
| # | 위치 | 스레드 | 비고 |
|---|------|--------|------|
| R1 | `MainView.xaml.cs:1669-1671` <- `DisplaySequenceContext` | **UI** | 확정 크래시 경로 (OnFinish/OnError) |
| R2 | `MainView.xaml.cs:1539` <- `DisplayParam` <- `SetParam` <- `InspectionListView.xaml.cs:882` (트리 노드 클릭) | **UI** | **같은 `DisplayContextToViewer` 를 타는 두 번째 UI 독자.** 검사 중 트리를 클릭하면 동일 크래시. 기존 분석에 없던 경로 |
| R3 | `SequenceBase.cs:468` (`SaveResultImage` → `CopyImage`) | 시퀀스 | `.planning/debug/manual-tools-locked-stuck.md:144` 가 의심했던 바로 그 지점 |
| R4 | `SequenceContext.cs:77` (`ActionContext.CopyFrom(seqContext)`) <- `ActionBase.OnBegin` (`ActionBase.cs:49`) | 시퀀스 | 액션 시작마다 127MP clone |

R1 과 R2 가 **같은 `DisplayContextToViewer` 를 통과**하므로 그 한 지점만 고치면 UI 측 독자가 전부 닫힌다.

### 소비자 전수조사 결과 (grep 완료 — 이것이 전부)
`SequenceContext.ResultHalconImage` 를 참조하는 코드는 위 R1~R4 + 해제자 3곳뿐. XAML 바인딩 0건. 다른 `OnFinish`/`OnError` 구독자(`InspectionSequence.HandleManualCyclePersist`, `HandleFlowLogCycleEnd`, `HandleAbnormalCycleLightOff`, `BatchRunService`, `RepeatRunService`)는 이미지를 전혀 읽지 않는다.
`Action_TopInspection` / `Action_BottomInspection` / `Action_FAIMeasurement` / `InspectionListView` 의 `ResultHalconImage` 접근은 전부 **`ActionContext` 쪽(다른 클래스)** 이다 — 이번 범위 밖(아래 잔여 리스크 참고).

### 이벤트 발화 순서 (설계 전제)
- `Finish()`: `AddResponse()`(PLC 해방, :522) → `SaveResultImage`(:525, Fail 시) → `OnFinish.Invoke`(:528)
- `Error()`: `AddResponse()`(:506) → `SaveResultImage`(:508) → `OnError.Invoke`(:509)
- 구독 순서상 `MainWindow.OnSequenceFinish` 는 `InspectionSequence` 자체 훅 **다음**, `RepeatRunService`/`BatchRunService` 훅 **이전** 에 실행된다.
- `StartEmptyScope`(`SequenceBase.cs:442`)는 **MainRun 스레드에서** `Finish()` 를 직접 호출한다 — `OnFinish` 는 시퀀스 스레드 전용이 아니다.

---

## 설계 결정: 3안 평가 (요구사항 1)

### 채택 — (c) `SharedHImage` refcount 재사용 + `TryAddRef()` 보강

`WPF_Example/Utility/CaptureImageSaveService.cs:27-56` 에 이미 존재하는 저장소 관용구다(260810 round4 에서 `Interlocked.Exchange` 이중해제 방어까지 하드닝 완료). `SequenceContext` 가 원시 `HImage` 대신 `SharedHImage` 를 소유하게 한다.

**채택 근거 4가지 (전부 코드 확인 기반):**

1. **독자 4개를 하나의 모델로 전부 덮는다.** 특히 R2(트리 클릭)는 훔치기(steal) 모델로는 깔끔하게 못 덮는다(아래 (a) 탈락 사유 1). refcount 는 독자 수와 무관하게 성립한다.
2. **시퀀스 스레드 블로킹 0.** 해제자는 `Interlocked.Exchange` + `Release()`(정수 감소 1회짜리 `lock` 구간)뿐이다. UI 가 127MB 를 복사하는 동안에도 시퀀스 스레드는 즉시 반환하고, 실제 `Dispose()` 는 마지막 참조자(=UI)가 수행한다.
3. **복사 횟수 불변(하드 제약 충족).** 수정 전후 모두 사이클당 ①액션 사본 ②`SequenceContext.CopyFrom` clone ③뷰어 내부 clone. refcount 는 복사를 추가하지 않는다.
4. **동시 read 안전성에 실측 근거가 있다.** `CaptureImageSaveService.cs:17-26` 주석에 기록된 260810 round4 실측: 운영 해상도 13376x9528(127MP)에서 워커 2개가 **동일 HImage 를 동시에 읽어** 렌더링하는 것을 150회 반복 — 오염/손상 0건. 본 수정에서 동시 read 가 생길 수 있는 조합(UI clone ↔ 시퀀스 `SaveResultImage`/`ActionContext.CopyFrom` clone)이 정확히 그 검증된 케이스다. Task 2 하네스가 sentinel 검증으로 재확인한다.

### 탈락 — (a) 소유권 이전 (`Interlocked.Exchange` 로 UI 에 단독 이관)

**포기하는 이점(정직하게 기록):** 뷰어가 clone 대신 넘겨받은 인스턴스를 그대로 채택하면 UI 스레드의 **127MB memcpy 1회를 사이클마다 제거**할 수 있다. 실제 성능 이득이며, 이번 수정에서는 포기한다(별건으로 재검토 가능).

**탈락 사유 3가지:**
1. **R2(트리 클릭)를 못 덮는다.** `DisplayParam` 도 훔치게 하면 "노드 한 번 클릭 후 컨텍스트에서 이미지가 사라져" 이후 선택이 디스크 폴백/무갱신으로 떨어지는 **동작 변경**이 생긴다. 훔치지 않고 원시 필드를 읽게 두면 **레이스가 그대로 남는다** — 정확히 `RepeatRunService` 가 저지른 "절반만 고친 버그"의 반복이다(요구사항 4가 경고하는 패턴).
2. **`MainWindow.xaml.cs` 수정이 필수가 된다.** 시퀀스 스레드에서 떼어내 Dispatcher 람다로 실어 보내야 하므로 `OnSequenceFinish`/`OnSequenceError` 의 수명관리가 바뀐다. 요구사항 3(현장 회피책 `DisableViewerDuringAutoInspect` 경로 보존)의 회귀 표면이 커진다. **채택안은 `MainWindow.xaml.cs` 를 한 줄도 건드리지 않는다.**
3. **핀(pin) 누적 메모리 리스크.** 표시 신선도를 보장하려면 OnFinish 시점에 이미지를 붙잡아 Dispatcher 큐에 실어야 하는데, UI 가 밀리면 큐 길이만큼 127MB 가 누적된다. 이 프로젝트는 메모리 폭증(34~41GB) 이력이 있어(`STATE.md`) 감수할 수 없다.

### 탈락 — (b) 락 보호 (`ShotConfig._imageLock` 미러링)

1. **하드 제약 직접 위반.** `ThreadPriority.Highest`(`SequenceBase.cs:80`) 시퀀스 스레드가 UI 의 127MB 복사(수십 ms)를 락 대기하게 된다 → 택트 직접 회귀.
2. **데드락 표면 증가.** 이 경로는 이미 `mDrawInterlock`(`MainView.xaml.cs:1537,1572`)과 `_startLock`(`SequenceBase.cs:326`)을 쓰고 있고, `MainView.xaml.cs:1200-1204,1350-1361` 주석은 **실제로 겪은 데드락**을 기록하고 있다. 세 번째 락을 교차 스레드로 추가하는 것은 같은 사고를 다시 부른다.

---

## 제약이 설계를 바꾼 지점 (반드시 인지)

**`DatumMeasurement.csproj` 수정 금지 = 신규 파일 생성 금지.**
클래식(non-SDK) csproj 라 모든 `.cs` 는 `<Compile Include=...>` 등록이 필요하다(현재 187개 명시). 따라서:
- `SharedHImage` 를 범용 프리미티브로 별도 파일 분리하지 **않는다** — `Utility/CaptureImageSaveService.cs` 안에 그대로 두고 메서드만 추가한다.
- 단위테스트 프로젝트 추가 **불가** → Task 2 는 저장소 밖(scratchpad) 독립 하네스를 `csc.exe` 로 직접 컴파일해 자동 검증한다(`STATE.md` 의 `ojq-verify` 하네스 선례 재사용).

**`SystemSetting.cs` 수정 금지** → 이 수정에 on/off 스위치를 두지 않는다. 소유권 모델은 무조건 적용된다(스위치가 있으면 "꺼진 상태 = 크래시하는 상태"가 남으므로 어차피 부적절하다).

**`Action_TopInspection.cs` / `Action_FAIMeasurement.cs` 수정 금지** → `ActionContext.ResultHalconImage`(SequenceContext 와 **다른 클래스**)의 원시 필드 접근은 이번에 손대지 않는다. 잔여 리스크로 문서화(Task 3).

### 커밋 주의 (플랜 작성 시점 워킹트리 실측 — 반드시 확인하고 시작할 것)

**금지 4개 파일은 전부 이미 미커밋 변경 상태다** (`git status` 실측: `SystemSetting.cs`, `Action_FAIMeasurement.cs`, `Action_TopInspection.cs`, `DatumMeasurement.csproj` 4개 모두 `M`). 따라서:

- **`git add -A` / `git add .` / `git commit -a` 절대 금지.** 이 작업으로 바뀐 파일만 경로로 명시해 `git add` 할 것.
- 금지 4개 파일의 워킹트리 내용은 **바이트 단위로 그대로 보존**되어야 한다. Task 1 게이트가 플랜 작성 시점의 `git hash-object` 값과 대조해 이를 강제한다. 값이 다르면 이 작업이 남의 변경을 건드린 것이므로 즉시 중단하고 복구한다.
- `MainWindow.xaml.cs` 는 현재 깨끗한 상태이며 이 작업 후에도 깨끗해야 한다(diff 0줄).

---

## 수용하는 동작 변경 (근거 있음)

**표시 신선도:** 채택안에서 UI 는 그리기 직전에 획득을 시도하므로, 다음 `$TEST` 가 매우 빨리 들어와 이미 `Clear()` 로 소유권이 교체된 뒤라면 `AcquireResultImage()` 가 null 을 반환하고 기존 디스크 폴백/오버레이 갱신 경로로 떨어진다 — **드물게 마지막 프레임이 다시 그려지지 않는다.**

수용 근거: (1) 현재는 그 경우 크래시하거나 오염된 픽셀을 그린다 — 어느 쪽이든 명백히 열등하다. (2) 사용자는 자동검사 중 실시간 표시를 **통째로 끄는 설정**(`DisableViewerDuringAutoInspect`)을 이미 도입해 운용 중이다 — 자동검사 중 표시 신선도가 필수 요구가 아님을 스스로 확인해 준 셈이다. (3) 수동/반복검사 경로는 `RepeatRunService` 의 `DispatcherPriority.Background` 순서 보장 덕에 영향이 사실상 없다.

---

> **게이트 방향 주의:** 아래 식별자들은 이 작업이 **새로 만드는 API 이름**이라 `<action>` 에 정확한 철자로 등장해야 한다(개념 서술로 대체하면 실행자가 다른 이름을 지어내 게이트와 어긋난다). 이 플랜의 검증 게이트는 전부 "존재 개수 >= N" 형태의 **양성 게이트**이며 부재(== 0) 게이트가 아니다.

<!-- planner-discipline-allow: AcquireResultImage -->
<!-- planner-discipline-allow: SetResultImageOwned -->
<!-- planner-discipline-allow: CloneResultImage -->
<!-- planner-discipline-allow: TryAddRef -->
<!-- planner-discipline-allow: _resultShared -->
<!-- planner-discipline-allow: Interlocked.Exchange -->
<!-- planner-discipline-allow: DispatcherPriority.Background -->

<tasks>

<task type="tracer">
  <name>Task 1: 결과 이미지 소유권 모델 end-to-end 배선 (생산자 → 소유자 → 소비자 관통)</name>
  <files>WPF_Example/Utility/CaptureImageSaveService.cs, WPF_Example/Sequence/Sequence/SequenceContext.cs, WPF_Example/Sequence/Sequence/SequenceBase.cs, WPF_Example/UI/ContentItem/MainView.xaml.cs</files>
  <reversibility rating="costly">공개 API 제거를 포함하므로 되돌리려면 4개 파일을 함께 revert 해야 한다. 단일 커밋 revert 로 완전 복구 가능하고 레시피/데이터 포맷 변경은 없다.</reversibility>
  <read_first>
    - WPF_Example/Utility/CaptureImageSaveService.cs:17-56 (SharedHImage 원본 + refcount 계약 주석)
    - WPF_Example/Sequence/Sequence/SequenceContext.cs:32-90 (ActionContext 전체)
    - WPF_Example/Sequence/Sequence/SequenceContext.cs:92-185 (SequenceContext 전체)
    - WPF_Example/Sequence/Sequence/SequenceBase.cs:457-496 (SaveResultImage)
    - WPF_Example/UI/ContentItem/MainView.xaml.cs:1660-1695 (DisplayContextToViewer)
  </read_first>
  <action>
**(1) `CaptureImageSaveService.cs` — `SharedHImage` 에 획득-시도 API 추가.**
`public bool TryAddRef()` 를 추가한다. 기존 `AddRef()` 와의 유일한 차이는 **성공 여부를 호출자에게 알려준다는 것**이다: `lock (_lock)` 안에서 내부 이미지가 이미 null(마지막 Release 로 해제 완료)이면 false 를 반환하고 카운트를 올리지 않으며, 살아 있으면 카운트를 올리고 true 를 반환한다. 기존 `AddRef()` 는 `TryAddRef()` 호출로 위임해 **동작을 완전히 동일하게 유지**한다(기존 `CaptureImageSaveService` 호출부 회귀 0).
`Image` 게터 위에 계약 주석을 추가한다: 이 게터는 `TryAddRef()` 가 true 를 반환한 시점부터 대응하는 `Release()` 전까지만 유효하며, 그 구간에서는 참조 카운트가 1 이상이라 내부 이미지가 해제될 수 없다는 사실을 명시한다.

**(2) `SequenceContext.cs` — `SequenceContext` 결과 이미지에 소유권 모델 도입.**
`SequenceContext` 의 원시 `HImage` 공개 프로퍼티를 제거하고 `private SharedHImage _resultShared;` 백킹 필드로 대체한다. `using System.Threading;` 과 `using ReringProject.Utility;` 를 추가한다(`ReringProject.Halcon` 은 이미 있음). **`ActionContext` 클래스의 동명 프로퍼티는 그대로 둔다** — 다른 클래스이고, 그 writer 들이 수정 금지 파일에 있다.

공개 API 3개를 추가한다:
- `public SharedHImage AcquireResultImage()` — `Volatile.Read` 로 필드를 읽고, null 이면 null 반환. non-null 이면 `TryAddRef()` 를 호출해 false 면 null 반환(이미 해제됨 = 이미지 없음과 동일 취급), true 면 그 인스턴스 반환. **반환값이 non-null 이면 호출자는 반드시 `finally` 에서 `Release()` 해야 한다**는 계약을 XML doc 으로 명시.
- `public void SetResultImageOwned(HImage image)` — 인자의 소유권을 컨텍스트로 이전한다. image 가 null 이면 next 는 null, 아니면 `new SharedHImage(image)`. `Interlocked.Exchange` 로 필드를 교체하고 이전 값이 non-null 이면 `Release()` 한다. **여기서 `Dispose()` 를 직접 호출하지 않는 것이 이 수정의 핵심** — 실제 해제는 마지막 참조자가 수행한다. 호출자는 이 메서드 호출 이후 인자를 만지지 않는다는 계약을 doc 에 명시.
- `public HImage CloneResultImage()` — 획득 → `CopyImage()` → `finally Release()` 를 한 번에 수행하고 소유권 있는 사본을 반환(없으면 null). 시퀀스 측 독자 2곳이 이것을 쓴다.

해제자 2곳을 소유권 API 로 교체한다:
- `SequenceContext.Clear()`: 결과 이미지 해제 부분을 `SetResultImageOwned(null)` 한 줄로 대체한다. `ResultImageFileName`/`ResultImagePath` 초기화, `InspectionOverlays`/`DisplayMessages` Clear, `act.Context.Clear()` 루프, Timer/State/Result 처리는 **전부 그대로 둔다.**
- `SequenceContext.CopyFrom(ActionContext)`: 기존의 "이전 것 Dispose → `HalconImageBridge.Clone(actionContext.ResultHalconImage)` 대입" 2단계를 `SetResultImageOwned(HalconImageBridge.Clone(actionContext.ResultHalconImage))` 한 번으로 대체한다. **clone 은 기존과 동일하게 시퀀스 스레드에서 1회 수행**(복사 횟수 불변). 오버레이/메시지/경로 처리는 무변경. 파생 override(`InspectionSequenceContext.CopyFrom`, `Sequence_Top`, `Sequence_Bottom`)는 base 호출만 하므로 손대지 않는다.

시퀀스 측 독자 1곳:
- `ActionContext.CopyFrom(SequenceContext seqContext)`: `HalconImageBridge.Clone(seqContext.ResultHalconImage)` 를 `seqContext.CloneResultImage()` 로 교체한다. 이 메서드의 `ActionContext` 자기 필드 처리(이전 것 Dispose 후 대입)는 **범위 밖이므로 무변경.**

`SequenceContext` 클래스 상단에 소유권 계약 헤더 주석을 남긴다: 결과 이미지는 컨텍스트가 소유하고 refcount 로 수명을 관리하며, 원시 참조를 클래스 밖으로 노출하지 않는다 — 외부는 획득 구간 내 읽기 또는 사본 소유, 둘 중 하나만 쓴다는 규칙과 그 이유(교차 스레드 use-after-dispose 차단, AVE 는 catch 불가).

**(3) `SequenceBase.cs` — `SaveResultImage` 를 소유권 API 로 전환.**
`Context.ResultHalconImage != null` 검사 후 `CopyImage()` 하던 부분을 `HImage snapshot = Context.CloneResultImage();` 로 바꾸고 `snapshot != null` 일 때만 기존 `Task.Factory.StartNew` 저장 로직에 그대로 넘긴다. **워커 람다 내부(파일명 결정, `Rgb1ToGray`, `WriteImage`, `finally` 의 `resultImage.Dispose()`)는 한 줄도 바꾸지 않는다.** 바깥의 260520 hbk `try/catch` 격리(잠금 영구화 방지)도 그대로 유지한다. `SaveFailImage == false` 조기 반환 분기도 무변경.

**(4) `MainView.xaml.cs` — 확정 크래시 지점을 획득 구간으로 감싼다.**
`DisplayContextToViewer` 에서 컨텍스트의 원시 이미지를 읽던 분기를 다음으로 교체한다: `context.AcquireResultImage()` 로 획득을 시도하고, non-null 이면 `try` 안에서 `halconViewer.LoadImage(shared.Image)` + `UpdateDisplayState(roiList, context.InspectionOverlays, context.DisplayMessages)` 를 수행하고 `true` 를 반환하며, 예외는 기존과 동일하게 `Logging.PrintErrLog` 로 삼키고, `finally` 에서 반드시 `Release()` 한다. 획득 실패(null)면 아래의 기존 디스크 폴백(`ResultImagePath`) → 최종 `UpdateDisplayState` 경로로 **기존 그대로** 흘러간다. `roiList` 계산과 두 폴백 블록은 무변경.
이 한 지점 수정으로 UI 독자 2개(`DisplaySequenceContext` 경로와 `DisplayParam` 경로)가 동시에 닫힌다는 사실, 그리고 `LoadImage` 가 내부에서 clone 하므로 획득 구간을 벗어난 뒤에는 뷰어가 자기 사본만 들고 있다는 사실을 주석으로 남긴다.

**금지사항 재확인:** `MainWindow.xaml.cs`, `SystemSetting.cs`, `Action_FAIMeasurement.cs`, `Action_TopInspection.cs`, `DatumMeasurement.csproj` 는 이 태스크에서 열지도 말 것. 신규 파일 생성 금지.
  </action>
  <verify>
    <automated>bash -c 'set -e; cd /c/code/DataMeasurement; S=WPF_Example/Sequence/Sequence/SequenceContext.cs; test $(grep -v "^\s*//" $S | grep -c "AcquireResultImage") -ge 2; test $(grep -v "^\s*//" $S | grep -c "SetResultImageOwned") -ge 3; test $(grep -v "^\s*//" $S | grep -c "CloneResultImage") -ge 2; test $(grep -v "^\s*//" $S | grep -c "Interlocked.Exchange") -ge 1; test $(grep -v "^\s*//" $S | grep -c "_resultShared") -ge 3; test $(grep -v "^\s*//" WPF_Example/Utility/CaptureImageSaveService.cs | grep -c "TryAddRef") -ge 2; test $(grep -v "^\s*//" WPF_Example/Sequence/Sequence/SequenceBase.cs | grep -c "CloneResultImage") -ge 1; test $(grep -v "^\s*//" WPF_Example/UI/ContentItem/MainView.xaml.cs | grep -c "AcquireResultImage") -ge 1; test $(git diff --name-only -- WPF_Example/MainWindow.xaml.cs | wc -l) -eq 0; test "$(git hash-object WPF_Example/Setting/SystemSetting.cs)" = "26fc59d32321fadf015b0777ed1fbb3a355aeb59"; test "$(git hash-object WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs)" = "83046e712243f88ecba98673b8db4288dd5713c4"; test "$(git hash-object WPF_Example/Custom/Sequence/Top/Action_TopInspection.cs)" = "09f778b2952bdad3b24fb3e44742aa2092db0dc9"; test "$(git hash-object WPF_Example/DatumMeasurement.csproj)" = "d4a455d8dc5cabfd8aecdee2fb28b0ee9912e300"; test $(git diff --cached --name-only -- WPF_Example/Setting/SystemSetting.cs WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs WPF_Example/Custom/Sequence/Top/Action_TopInspection.cs WPF_Example/DatumMeasurement.csproj | wc -l) -eq 0; echo GATES_OK'</automated>
    <automated>"C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" WPF_Example/DatumMeasurement.csproj -p:Configuration=Debug -p:Platform=x64 -v:m -nologo</automated>
  </verify>
  <done>
Debug/x64 빌드 에러 0. `SequenceContext` 가 원시 결과 이미지 참조를 외부에 노출하지 않으며, 해제자 2곳과 독자 3곳이 전부 소유권 API 를 경유한다. `MainWindow.xaml.cs` diff 0줄(게이트로 확인).

**빌드가 완전성 게이트다:** 원시 프로퍼티를 제거했으므로 미이관 소비자가 하나라도 남으면 컴파일이 실패한다. 빌드 성공 = 전수 이관 증명.

**exe 잠김 대응(프로세스 강제 종료 절대 금지 — 프로젝트 하드룰):** `DatumMeasurement.exe` 가 실행 중이라 복사 단계에서 실패하면 동일 명령에 `-p:OutDir=C:/Users/admin/AppData/Local/Temp/claude/C--code-DataMeasurement/0da39e39-7e39-40eb-8182-41eca9b2accd/scratchpad/odo-build/` (마지막 `/` 필수)를 추가해 컴파일만 검증한다. 그 경우 SUMMARY 에 "OutDir 우회로 컴파일 검증"이라고 명시한다.
  </done>
</task>

<task type="auto">
  <name>Task 2: 동시 swap/release ↔ acquire/clone 레이스 자동 검증 하네스 (sentinel 무결성 포함)</name>
  <files>C:/Users/admin/AppData/Local/Temp/claude/C--code-DataMeasurement/0da39e39-7e39-40eb-8182-41eca9b2accd/scratchpad/odo-harness/RefRaceHarness.cs, C:/Users/admin/AppData/Local/Temp/claude/C--code-DataMeasurement/0da39e39-7e39-40eb-8182-41eca9b2accd/scratchpad/odo-harness/build-and-run.sh</files>
  <precondition>HALCON 24.11 네이티브 DLL 디렉터리(`C:\Program Files\MVTec\HALCON-24.11-Progress-Steady\bin\x64-win64`)가 존재해야 한다. 하네스 스크립트가 실행 시 PATH 앞에 직접 붙이므로 시스템 PATH 설정 여부는 무관하나, 디렉터리 자체가 없으면 하네스는 실행 불가다(그 경우 태스크를 SKIP 하지 말고 즉시 중단 보고).</precondition>
  <read_first>
    - WPF_Example/Utility/CaptureImageSaveService.cs:27-56 (하네스가 소스를 추출해 갈 대상 클래스)
    - WPF_Example/Sequence/Sequence/SequenceContext.cs (Task 1 에서 추가한 Acquire/SetOwned 본문 — 하네스가 동일 로직을 재현한다)
  </read_first>
  <action>
저장소 밖(scratchpad)에 독립 콘솔 하네스를 만들어 **소유권 프리미티브가 실제 HALCON 이미지로 레이스 상황에서 안전한지**를 자동 검증한다. 저장소에 파일을 추가하지 않으므로 csproj 수정 금지 제약과 충돌하지 않는다.

**`build-and-run.sh` 가 할 일:**
1. 저장소의 `WPF_Example/Utility/CaptureImageSaveService.cs` 에서 `SharedHImage` 클래스 본문을 `awk` 로 추출한다(클래스 선언 줄부터, 4칸 들여쓰기 닫는 중괄호 줄까지). 추출한 본문을 `using HalconDotNet; using System; using System.Threading;` + 네임스페이스로 감싸 `SharedHImage.gen.cs` 로 저장한다. **복붙이 아니라 실제 소스를 추출한다는 점이 중요하다** — 하네스가 검증하는 것이 프로덕션 코드와 동일하다는 보장이 된다. 추출 실패 시 컴파일이 깨져 즉시 드러난다.
2. `C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe` 로 `SharedHImage.gen.cs` + `RefRaceHarness.cs` 를 컴파일한다. 참조는 `C:/Program Files/MVTec/HALCON-24.11-Progress-Steady/bin/dotnet35/halcondotnet.dll`, 플랫폼은 x64, 출력은 콘솔 exe.
3. PATH 앞에 HALCON `bin/x64-win64` 를 붙여 exe 를 실행하고 종료 코드를 그대로 전파한다.

**`RefRaceHarness.cs` 가 할 일:**
- `Owner` 클래스: Task 1 에서 `SequenceContext` 에 추가한 3개 메서드와 **동일한 본문**을 갖는다(private `SharedHImage` 필드, `Volatile.Read` + `TryAddRef` 획득, `Interlocked.Exchange` + `Release` 교체). 검증 대상은 이 소유권 프로토콜이다.
- 쓰기 스레드 1개(시퀀스/MainRun 역할): 루프마다 세대 번호를 증가시키고 sentinel 값을 1~200 범위로 정한다. `GenImageConst` 로 byte 이미지를 만든 뒤 `ScaleImage(0, sentinel)` 로 **전 픽셀을 sentinel 값으로 균일하게** 채워 `SetOwned` 로 넘긴다. 주기적으로(예: 세대 17배수) `SetOwned(null)` 도 섞어 "해제 직후 획득" 창을 인위적으로 넓힌다. 스레드 우선순위를 `Highest` 로 설정해 실제 시퀀스 스레드 조건을 흉내낸다.
- 읽기 스레드 2개(UI 표시 역할 + 시퀀스 `SaveResultImage`/`ActionContext.CopyFrom` 역할): 루프마다 `Acquire()` 를 호출한다. null 이면 miss 카운트만 올리고 계속(정상 — 이미지 없음). non-null 이면 `try` 안에서 `CopyImage()` 로 사본을 만들고, 사본의 도메인에 대해 `Intensity` 로 평균과 표준편차를 구해 **표준편차 0 이고 평균이 1~200 범위의 정수**인지 검사한다(= 해제된/재사용된 메모리를 읽지 않았다는 증명). 어긋나면 실패 카운트를 올리고 즉시 상세를 출력한다. `finally` 에서 반드시 `Release()` 하고 사본을 dispose 한다.
- 두 페이즈로 실행한다: 페이즈 A 는 1024x1024(작고 빠름, 반복 횟수 최대화로 레이스 창 타격 확률 극대화), 페이즈 B 는 4000x4000(실기 급 대용량에서 복사 시간이 길어 획득 구간이 실제로 겹치는지 확인). 전체 실행 시간이 **50초를 넘지 않도록** 각 페이즈를 시간 기반으로 종료한다.
- 종료 시 세대 수, 획득 성공/miss 수, 무결성 실패 수, 예외 수를 출력한다. 실패 0 이면 `PASS` 를 출력하고 종료 코드 0, 아니면 `FAIL` 과 종료 코드 1. `HalconException` 은 실패로 집계한다.
- **AVE 가 발생하면 프로세스가 즉사하며 종료 코드가 0 이 아니게 되므로, 그것 자체가 유효한 실패 신호다**(catch 로 잡히지 않는다는 사실이 여기서는 오히려 검출 장치가 된다).

하네스가 검증하는 범위를 정직하게 기록할 것: 이것은 **소유권 프로토콜과 `SharedHImage` 원본 소스**를 검증한다. `SequenceContext`/`MainView` 배선 자체는 Task 1 의 빌드 게이트와 코드 리뷰로 검증되며, 실기 재현 부재 확인은 사용자 UAT 몫이다.
  </action>
  <verify>
    <automated>bash C:/Users/admin/AppData/Local/Temp/claude/C--code-DataMeasurement/0da39e39-7e39-40eb-8182-41eca9b2accd/scratchpad/odo-harness/build-and-run.sh</automated>
  </verify>
  <done>하네스가 종료 코드 0 으로 `PASS` 를 출력한다. 출력에 두 페이즈의 세대 수 / 획득 성공 수 / miss 수 / 무결성 실패 0 / 예외 0 이 표시되고, 획득 성공 수가 0 이 아니다(레이스 창을 실제로 통과했다는 증거 — 성공 수 0 이면 검증이 성립하지 않으므로 FAIL 로 간주하고 반복 횟수를 늘려 재실행). SUMMARY 에 하네스 출력 원문을 인용한다.</done>
</task>

<task type="auto">
  <name>Task 3: 잔여 경로 전수 스윕 + 기존 부분수정과의 관계 문서화 + 최종 빌드</name>
  <files>WPF_Example/Custom/Sequence/Inspection/RepeatRunService.cs, WPF_Example/UI/ControlItem/InspectionListView.xaml.cs</files>
  <read_first>
    - WPF_Example/Custom/Sequence/Inspection/RepeatRunService.cs:259-296 (TriggerNext, 기존 Background 우선순위 우회)
    - WPF_Example/UI/ControlItem/InspectionListView.xaml.cs:680-745 (ClearShotImageCache + Running 가드 재시도 루프)
  </read_first>
  <action>
**(1) `RepeatRunService.TriggerNext` — 코드는 그대로 두고 주석만 갱신한다 (요구사항 4).**
`DispatcherPriority.Background` 우회는 **제거하지 않는다.** 이유를 주석에 명시한다: 소유권 모델 도입으로 이 우회의 **안전(크래시 방지) 역할은 사라졌지만**, "표시가 먼저 그려진 뒤 다음 사이클이 시작된다"는 **순서 보장 역할은 그대로 유효**하며, 그 순서 보장이 수동 반복검사에서 표시 신선도를 지켜준다. 즉 두 수정은 모순되지 않고 역할이 분리된다. 제거하면 반복검사 표시가 드물게 스킵되는 회귀만 얻으므로 의도적으로 유지한다는 판단을 남긴다. 기존 주석에서 "경합이 해소된다"고 단정하는 문장은, 이제 경합 자체가 상위 계층에서 구조적으로 제거되었음을 반영하도록 고쳐 쓴다(과거 서술이 미래 독자를 오도하지 않게).

**(2) `InspectionListView.ClearShotImageCache` — 잔여 위험 마커를 남긴다 (수정 금지 파일 제약의 결과).**
`ActionContext` 쪽 결과 이미지에는 이번 소유권 모델이 적용되지 않았다는 사실과 그 정확한 이유/조건을 주석으로 기록한다: (a) 이 UI 정리 경로와 `SequenceContext.Clear()` 안의 액션 컨텍스트 정리 루프가 **둘 다 해제자**이고, 후자는 `StartCore` 에서 State 가 Running 으로 점유되기 **직전** 에 돌기 때문에 기존 `EContextState.Running` 가드가 그 창을 막지 못한다(이중 해제 잔여 창). (b) 근본 수정은 `ActionContext` 에도 동일한 원자적 소유권 모델을 적용하는 것인데, writer 가 수정 금지 파일(`Action_TopInspection.cs`, `Action_FAIMeasurement.cs`, `Action_BottomInspection.cs`)에 있어 **절반만 고치면 오히려 위험**하므로(비원자적 writer 가 남으면 use-after-dispose 가 그대로 성립) 이번 범위에서 손대지 않았다. (c) 다음에 이 영역을 만지는 사람이 반복 조사하지 않도록, 필요한 후속 작업 형태를 한 줄로 적는다.
**코드 동작은 변경하지 않는다 — 주석만 추가한다.**

**(3) 전수 스윕.**
저장소 전체에서 `SequenceContext` 쪽 결과 이미지를 원시로 만지는 코드가 하나도 남지 않았음을 grep 으로 확인한다. 남은 `ResultHalconImage` 참조는 전부 `ActionContext` 계열(`pMyContext.` / `act.Context.` / `actionContext.`)이어야 한다. 예외가 하나라도 나오면 Task 1 로 되돌아가 이관한다.

**(4) 최종 확인 빌드** 를 Debug/x64 로 수행한다(Task 1 이후 두 파일이 더 바뀌었으므로 재빌드).
  </action>
  <verify>
    <automated>bash -c 'set -e; cd /c/code/DataMeasurement; R=$(grep -rn "ResultHalconImage" WPF_Example --include=*.cs | grep -v "WPF_Example/Sequence/Sequence/SequenceContext.cs" | grep -v "pMyContext\." | grep -v "act\.Context\." | grep -vE "^[^:]+:[0-9]+:[[:space:]]*//" | wc -l); echo "raw_seqcontext_refs=$R"; test "$R" -eq 0; test $(grep -c "DispatcherPriority.Background" WPF_Example/Custom/Sequence/Inspection/RepeatRunService.cs) -ge 1; echo SWEEP_OK'</automated>
    <automated>"C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" WPF_Example/DatumMeasurement.csproj -p:Configuration=Debug -p:Platform=x64 -v:m -nologo</automated>
  </verify>
  <done>스윕 게이트 통과(원시 `SequenceContext` 결과 이미지 참조 0건, 남은 참조는 전부 `ActionContext` 계열). `RepeatRunService` 의 `DispatcherPriority.Background` 는 **코드 그대로 유지**되고 주석만 갱신됨. `InspectionListView` 에 `ActionContext` 잔여 위험 마커 추가(동작 변경 0). Debug/x64 재빌드 에러 0.</done>
</task>

</tasks>

<threat_model>
## Trust Boundaries

| Boundary | Description |
|----------|-------------|
| 시퀀스/MainRun 스레드 → UI 스레드 | 가변 공유 객체(`SequenceContext`)와 네이티브 이미지 핸들이 스레드 경계를 넘는다. 소유권 계약이 없으면 해제된 네이티브 메모리를 읽는다 |
| 비전 시스템 → PLC/호스트 (TCP) | 측정값·판정이 외부 장비로 나간다. 오염된 픽셀로 계산된 값이 정상 판정으로 전송되면 검출 불가능한 오출하가 된다 |

## STRIDE Threat Register

| Threat ID | Category | Component | Severity | Disposition | Mitigation Plan |
|-----------|----------|-----------|----------|-------------|-----------------|
| T-odo-01 | Tampering (메모리 무결성) | `SequenceContext.ResultHalconImage` ↔ `MainView.DisplayContextToViewer` | critical | mitigate | 해제된 힙 memcpy → 조용한 오염 → 잘못된 측정값이 Pass 로 PLC 전송. Task 1 의 refcount 소유권 모델이 "획득 성공 구간에서만 읽기"를 강제해 원인을 제거. Task 2 하네스가 sentinel(균일 픽셀값 + 표준편차 0) 검사로 오염 검출력을 실증 |
| T-odo-02 | Denial of Service | 프로세스 전체 | high | mitigate | AVE 로 검사 PC 즉사 → 라인 정지. 동일 수정으로 제거. AVE 는 catch 불가이므로 방어적 예외처리는 완화책이 될 수 없음(설계 원칙으로 명시) |
| T-odo-03 | Denial of Service (자원) | refcount 지연 해제 | medium | accept | 마지막 참조자가 UI 스레드가 되어 127MB 해제가 UI 드레인까지 지연될 수 있음. 지연 폭은 Dispatcher 1틱 수준이고 인스턴스 수는 시퀀스당 1개로 유한. 핀 누적형 설계((a)안)를 탈락시킨 이유가 이 리스크의 상한을 낮추기 위함 |
| T-odo-04 | Tampering (이중 해제) | `ActionContext.ResultHalconImage` (UI 정리 ↔ MainRun `Clear`) | medium | accept | 이번 범위 밖(writer 가 수정 금지 파일). Task 3 에서 조건·경로·필요 후속작업을 코드 주석으로 명시해 다음 조사자가 재조사하지 않도록 함. 기존 `EContextState.Running` 가드가 부분적으로 완화 중 |
| T-odo-SC | Tampering (공급망) | 패키지 설치 | n/a | n/a | 이 작업은 신규 패키지를 설치하지 않는다(npm/pip/cargo 미사용, csproj 수정 금지로 참조 추가 자체가 불가) |
</threat_model>

<verification>
1. **정적 게이트** — Task 1/3 의 grep 매트릭스: 소유권 API 배선 확인 + 원시 참조 0건 + 수정 금지 파일 무변경.
2. **컴파일 완전성 증명** — 원시 프로퍼티 제거로 미이관 소비자가 남으면 빌드가 깨진다. Debug/x64 빌드 에러 0 = 전수 이관 완료.
3. **동적 레이스 검증** — Task 2 하네스: 실제 HALCON 이미지로 쓰기 1 + 읽기 2 스레드 경합, sentinel 무결성 실패 0, 예외 0, 종료 코드 0.
4. **범위 밖(사용자 UAT)** — 실기 자동 연속 반복으로 크래시 미재현 확인, 표시 동작 육안 확인, `DisableViewerDuringAutoInspect=true/false` 양쪽 확인. **이 플랜의 완료 조건에 포함하지 않는다.**
</verification>

<success_criteria>
- Debug/x64 MSBuild 에러 0 (exe 잠김 시 OutDir 우회 컴파일 검증, 프로세스 강제 종료 없음)
- `SequenceContext` 결과 이미지의 원시 참조가 저장소 전체에 0건
- 해제자 2곳(`Clear`, `CopyFrom`) + 독자 3곳(`SaveResultImage`, `ActionContext.CopyFrom`, `DisplayContextToViewer`)이 전부 소유권 API 경유
- `MainWindow.xaml.cs` / `SystemSetting.cs` / `Action_FAIMeasurement.cs` / `Action_TopInspection.cs` / `DatumMeasurement.csproj` diff 0줄
- 신규 파일 0개(저장소 기준)
- 127MP 복사 횟수 수정 전과 동일(액션 사본 1 + `CopyFrom` clone 1 + 뷰어 clone 1)
- 시퀀스 스레드가 UI 복사를 대기하는 코드 경로 0개
- Task 2 하네스 PASS(종료 코드 0, 무결성 실패 0, 획득 성공 수 > 0)
- `RepeatRunService` 의 `DispatcherPriority.Background` 코드 유지 + 관계 주석 갱신
</success_criteria>

<output>
완료 시 `.planning/quick/260811-odo-resulthalconimage-use-after-dispose-acce/260811-odo-SUMMARY.md` 작성.

SUMMARY 에 반드시 포함할 것:
- 채택안((c) refcount)과 탈락 2안의 사유 요약, 특히 **(a)안이 제거할 수 있었던 UI 127MB memcpy 1회를 이번에 포기했다는 사실**(후속 최적화 후보로 기록)
- 새로 발견된 두 번째 UI 독자(트리 노드 클릭 → `DisplayParam`)가 같은 수정으로 함께 닫혔다는 사실
- 표시 신선도 트레이드오프(빠른 연속 사이클에서 마지막 프레임 재표시가 드물게 생략될 수 있음)와 수용 근거
- Task 2 하네스 출력 원문
- 잔여 리스크: `ActionContext.ResultHalconImage` 이중 해제 창(수정 금지 파일 제약), 그리고 그 근본 수정에 필요한 작업 형태
- 사용자 실기 UAT 항목 목록(이 플랜 범위 밖임을 명시)
</output>
