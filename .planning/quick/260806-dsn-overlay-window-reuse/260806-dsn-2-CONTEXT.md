# Quick Task 260806-dsn-2: 배치 정리 로직의 비동기 저장 큐 레이스 수정 - Context

**Gathered:** 2026-08-06 (260806-dsn의 Task 4 human-verify에서 발견된 실패를 원인 확정 후 이어서 수정)

**Status:** Ready for planning (원인 확정 — 독립 에이전트가 코드추적 + 실제 파일 타임스탬프 대조로 실증 완료, 재조사 불필요)

<domain>
## Task Boundary

**선행 작업**: 260806-dsn(커밋 `3a5f4b4`/`8c327c5`/`534c742`)이 배치 사이클 완료 시 비표시 SHOT의 이미지 캐시(`ShotConfig._image`/`ActionContext.ResultHalconImage`)를 정리하는 로직을 추가했으나, 사용자 실기 검증(Task 4)에서 **"메모리가 30초 후에도 안 떨어짐, 계속 누르면 조금씩 더 올라감"**으로 실패함.

**확정된 원인(독립 에이전트가 코드추적 + 실제 파일 시각 대조로 실증)**:

`ShotConfig.ResolveFallbackImagePath()`(`ShotConfig.cs:426-434`)는 정리 전에 "재클릭 시 디스크에서 재현 가능한가"를 `File.Exists()`로 확인하는 안전장치인데, 이 확인이 도는 시점(`OnBatchComplete`, 배치 사이클 완료 직후)과 실제로 그 파일이 디스크에 써지는 시점 사이에 구조적인 시간차가 있다:

1. `Action_FAIMeasurement.QueueFaiCapture`(`:847-909`)는 FAI마다 origin/capture 2건을 **비동기 큐에 넣기만** 하고(`CaptureImageSaveService.Enqueue`, `:114-128`) 즉시 반환한다 — 실제 `WriteImage()`(`:212`)는 단일 `BelowNormal` 우선순위 워커 스레드가 나중에 처리한다.
2. 사이클의 마지막 FAI가 큐에 들어간 직후 `EStep.End`→`FinishAction()`→`SequenceBase.Finish()`→`OnFinish`(전부 순수 인메모리, I/O 없음)를 거쳐 `BatchRunService.HandleFinish`→`OnBatchComplete`→`CleanupBatchImageMemoryAfterCycle`까지가 **밀리초 단위**로 끝난다 — 저장 큐를 전혀 기다리지 않는다.
3. **실측 증거**: 오늘 실제 저장된 파일의 "파일명에 박힌 큐 투입 시각" vs "실제 파일시스템 기록 시각"을 직접 대조 — `origin_BOTTOM_FAI_E5_NG_122116660.jpg`는 12:21:16.660에 큐 투입, **12:21:27.354에 실제 기록(10.69초 지연)**. 세션이 진행될수록 지연이 더 늘어나 12:33경엔 **15.96초 지연**까지 관측됨. 앱 자체 Error 로그에도 `[CaptureImageSaveService] 저장 지연으로 검사 사이클 대기 1040ms (depth=49/50)`이 실제로 찍혀있어 큐가 상한(50)에 근접해 상시 포화 상태였음이 확인됨.
4. `ResolveFallbackImagePath()`는 SHOT 안의 FAI 중 **하나라도** 파일이 있으면 통과하도록 되어 있어(관대한 설계), 배치 앞쪽에서 처리된 SHOT은 그 시점 파일이 이미 써져 있어 대부분 정리에 성공한다. 문제는 **배치 뒤쪽(약 1/3~1/2 구간, 30초 사이클에 10~16초 지연이면 그 정도 비율)** — 이 SHOT들의 파일은 사이클 종료 시점에 아직 큐에 남아있어 **매 사이클, 확정적으로** 정리가 skip된다. 이게 누적되어 "계속 누르면 조금씩 올라감"으로 나타남.

**참고(범위 밖으로 확인됨)**: 같은 조사에서 `OverlayCaptureRenderer.RenderToHImage`의 `#6001 open_window` 에러가 오늘 이른 시각 로그에도 남아있음을 발견했으나, 이 항목은 **이미 사용자가 직접 코드를 주석처리+재빌드해서 재현한 결과 폭증의 주범이 아님이 확인된 가설**이다(오늘 앞선 대화 참고) — 재조사 금지, 무시할 것.

</domain>

<decisions>
## Implementation Decisions (LOCKED)

### 해결 방향: "즉시 실패 시 포기"를 "나중에 큐가 비면 재시도"로 전환 (동기 대기 없이)

**금지**: `CleanupBatchImageMemoryAfterCycle` 안에서 큐가 빌 때까지 **동기적으로(블로킹) 대기**하는 방식은 채택하지 않는다 — 실측 지연이 10~16초이고 세션이 진행될수록 더 늘어나는 특성상, 매 사이클마다 UI/시퀀스 스레드를 그만큼 멈추는 건 받아들일 수 없다(현재 `OnBatchComplete`는 `Dispatcher.Invoke` 안에서 동기 실행되며 이게 반환할 때까지 시퀀스 스레드가 블로킹된다는 걸 기억할 것).

**채택**: 즉시 정리에 실패한(폴백 파일이 아직 없는) SHOT을 그냥 버리지 않고 **"나중에 다시 시도할 목록"에 넣어두고, 별도의 가벼운 재시도 루프(`DispatcherTimer`, UI 스레드에서 짧은 주기로 실행 — 새 스레드/Task 도입 불필요, `File.Exists()`는 가벼운 호출이라 UI 스레드에서 짧은 목록을 주기적으로 훑어도 체감 지연 없음)가 몇 초 간격으로 재확인**한다.

구체 요구사항(플래너가 정확한 구현 세부는 재량, 아래는 확정 요구사항):
1. **즉시 시도 경로는 그대로 유지**: `CleanupBatchImageMemoryAfterCycle`이 사이클 완료 직후 기존처럼 한 번 훑어서, 그 시점에 폴백이 이미 있는 SHOT은 지금처럼 즉시 정리한다(무변경, 이미 잘 동작함 — 배치 앞쪽 SHOT 커버).
2. **실패한 SHOT은 재시도 대기열에 추가**: 그 시점에 폴백이 없어 정리를 skip한 SHOT들을 인스턴스 필드(예: `List<ShotConfig> _pendingImageCleanup` 또는 동등한 자료구조)에 추가한다.
3. **재시도 루프 신설**: `DispatcherTimer` 하나를 두고(간격은 플래너 재량이나 예: 3~5초 — 실측 지연이 10초대이므로 너무 짧게 폴링해 낭비하지 않되, 너무 길게 잡아 정리가 체감되게 늦어지지도 않게), 대기열이 비어있지 않은 동안 주기적으로 순회하며:
   - 각 SHOT에 대해 `ResolveCurrentlyDisplayedShot()`을 **그 시점 기준으로 다시** 호출해 현재 표시 중인 노드인지 재확인한다(사이클 종료 시점과 재시도 시점 사이에 사용자가 다른 노드를 클릭했을 수 있으므로, 최초 판별을 재사용하지 말고 반드시 재조회할 것). 현재 표시 중이면 정리하지 않고 대기열에서 제거한다(더 이상 재시도 불필요 — 사용자가 보고 있음).
   - 현재 표시 중이 아니면 `ResolveFallbackImagePath()`를 다시 확인한다. 폴백이 이제 존재하면 기존과 동일한 정리(`ShotConfig.ClearImage()` + 대응 `ActionContext.ResultHalconImage` Dispose)를 수행하고 대기열에서 제거한다. 여전히 없으면 대기열에 남겨 다음 주기에 다시 시도한다.
4. **무한 재시도 방지**: 저장이 영구적으로 실패하는 예외적 케이스(디스크 오류 등)에서 대기열이 무한히 안 비워지는 걸 막기 위해, SHOT별로 대기열에 들어간 후 경과 시간 또는 재시도 횟수에 합리적인 상한(플래너 재량, 예: 2분 또는 20회)을 두고 초과 시 그 SHOT은 조용히 포기(대기열에서 제거, 메모리 절감 실패를 감수 — 회귀/예외 없이).
5. **대기열이 비면 타이머 정지(또는 항상 켜두되 빈 목록이면 즉시 반환)** — 리소스 낭비 방지. 재사용 시(다음 배치 사이클 완료로 대기열에 새 항목 추가) 다시 시작.
6. **동시성**: 이 재시도 루프는 `DispatcherTimer`라 UI 스레드에서만 실행되므로, `CleanupBatchImageMemoryAfterCycle`(역시 UI 스레드, `Dispatcher.Invoke` 콜백)과 동일 스레드 친화성을 가져 별도 락이 불필요하다 — 단, 같은 SHOT이 대기열에 중복으로 들어가지 않도록(예: 이미 대기열에 있는 SHOT을 또 추가하지 않음) 확인할 것.

### TEMP DIAG 로그 제거
이번 조사를 위해 `InspectionListView.xaml.cs`의 `CleanupBatchImageMemoryAfterCycle`에 임시로 추가한 `[DIAG-260806]` 진단 로그 3곳(시작/SKIP/종료, 커밋 안 된 상태로 현재 작업 트리에 있음)을 이번 수정에서 제거하거나, 새 재시도 로직에 맞게 정리된 형태로 대체한다(완전 제거해도 되고, 유용하다고 판단되면 재시도 성공/최종 포기 시점에 맞춰 Trace 로그로 남겨도 됨 — Claude's Discretion, 단 "TEMP DIAG"라는 임시 표시 문구는 제거).

### 범위 밖
- `CaptureImageSaveService.cs`의 `MAX_QUEUE_DEPTH=50`나 워커 스레드 우선순위/개수 자체를 바꾸는 것 — 이번 범위 밖(근본적으로 저장 처리량을 늘리는 건 별도 최적화 과제, Claude's Discretion으로 언급만 하고 이번엔 무수정).
- Part A(HALCON SetSystem 3줄)와 나머지 Part B 로직(디스크 폴백 헬퍼, 크로스-Z 정리, `DisplayShotImage` 폴백 분기) — 이미 완료·검증됨, 무수정.
- `OverlayCaptureRenderer.cs` — 반증된 가설, 무수정.

</decisions>

<specifics>
## Specific Ideas

- 대상 파일: `WPF_Example\UI\ControlItem\InspectionListView.xaml.cs` — `CleanupBatchImageMemoryAfterCycle`/`ResolveCurrentlyDisplayedShot` 주변에 재시도 대기열 필드 + `DispatcherTimer` + 재시도 콜백 메서드 신설.
- 재사용: `ShotConfig.ResolveFallbackImagePath()`, `ShotConfig.ClearImage()`, `InspectionSequence.ClearCrossZImagesAfterBatchCycle()` — 전부 무변경, 재시도 루프에서도 그대로 재사용.
- 검증: 오늘 실기 재현했던 정확히 그 시나리오(Bottom, 30개 항목, 일괄검사) — 이번엔 **사이클 종료 직후 30초가 아니라, 저장 큐가 완전히 비워질 때까지(로그의 QueueDepth=0 확인 또는 충분히 긴 대기, 예 1~2분) 관찰**해야 한다. 재시도 루프가 실제로 뒤쪽 SHOT들을 나중에 정리하는지 확인.
- 세션이 진행될수록 지연이 늘어나는 특성(캡처 저장 처리량 자체가 부족)이 있으므로, 이 수정 하나로 "완벽하게 즉시 감소"까지는 보장 못 할 수 있음 — 대신 "결국엔(큐가 따라잡으면) 정리된다"가 목표. 검증 시 이 점을 사용자에게 미리 안내할 것.

</specifics>

<canonical_refs>
## Canonical References

- 독립 에이전트 실측 증거: `origin_BOTTOM_FAI_E5_NG_122116660.jpg` 큐투입 12:21:16.660 vs 실제기록 12:21:27.354(10.69초), `origin_BOTTOM_FAI_E5_NG_123253183.jpg` 12:32:53.183 vs 12:33:09.145(15.96초). `D:\Data\Error\2026-08-06_Error.log` `12:31:26:0,[CaptureImageSaveService] 저장 지연으로 검사 사이클 대기 1040ms (depth=49/50)`.
- 코드 경로 전수 추적: `Action_FAIMeasurement.cs:296-468,847-909,1322`, `CaptureImageSaveService.cs:90,100-104,114-128,212`, `SequenceBase.cs:227-243,274,459-475`, `BatchRunService.cs:78-147`, `InspectionListView.xaml.cs:605-620`, `ShotConfig.cs:426-434`.

</canonical_refs>
