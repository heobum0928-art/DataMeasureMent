# Quick Task 260805-mzf: CaptureImageSaveService 큐 백프레셔 추가 — 일괄검사 메모리 폭증 방지 - Context

**Gathered:** 2026-08-05
**Status:** Ready for planning (원인 조사 완료 — 재조사 불필요)

<domain>
## Task Boundary

사용자가 일괄검사(Batch)를 돌리는 동안 프로세스 메모리가 58.3GB까지 지속 증가(감소 없음)하는 것을 실측(스크린샷)으로 확인했고, 결국 프로세스가 종료(크래시)됨을 보고함. 조사 결과 이건 "Dispose 누락" 류의 고전적 누수가 아니라 **`CaptureImageSaveService`의 저장 큐가 무제한(unbounded)이고, 백프레셔가 전혀 없어서 생산 속도(측정 사이클)가 소비 속도(이미지 저장)를 크게 앞지르면 큐에 이미지가 무한정 쌓이는 구조적 문제**임이 확정됨.

**확정된 메커니즘**:
- `Utility/CaptureImageSaveService.cs`의 `_queue`는 상한 없는 `ConcurrentQueue`이고, 처리 워커는 **단일 스레드 + `BelowNormal` 우선순위**(:88-92).
- 요청 1건(FAI 1개당 origin+capture 2건, `Action_FAIMeasurement.cs:883,898`)마다 `OverlayCaptureRenderer.RenderToHImage`가 원본 해상도 HALCON 버퍼 윈도우 생성 → DispObj → DumpWindowImage → JPEG write(`CaptureImageSaveService.cs:143-156`)를 수행 — 건당 수백 ms.
- **일괄검사**는 `BatchRunService.TriggerNext`(:149-181)가 시퀀스가 Idle이 되는 즉시 다음 사이클을 던져, 저장 워커가 큐를 비울 유휴 시간이 사실상 0이다. 단발 RUN은 사람이 다음 조작까지 시간이 걸려 그 사이 큐가 비워지므로 표면화되지 않았다.
- 각 큐 항목이 Shot 원본 HImage(refcount로 보유, 12MP mono 기준 약 12MB)를 참조하므로, 미처리 항목 수 × 이미지 크기가 그대로 상주 메모리로 누적된다. 수천 건 적체 시 수십 GB에 도달 가능 — 실측치(58.3GB)와 일치.
- Dispose 자체(`Release`/`request.Dispose()`)는 정확하다 — 문제는 소비가 생산을 못 따라가는 속도 불균형이지 메모리 해제 로직의 결함이 아니다.

</domain>

<decisions>
## Implementation Decisions

### 해결 방향: 큐에 상한을 두고 백프레셔를 건다 (LOCKED)
- 큐 깊이에 상한(`private const int MAX_QUEUE_DEPTH`, 구체 수치는 재량이나 예: 50~200 사이 합리적 값 — 이미지 크기/저장 속도를 고려해 "몇 초~몇십 초치 버퍼"에 해당하는 수준으로 설계)을 둔다.
- 상한 초과 시 **enqueue 하려는 쪽(검사 사이클 진행 스레드)이 짧게 대기**하도록 한다(백프레셔) — 저장이 못 따라가면 검사 사이클 자체가 자연스럽게 느려지는 방향. 이미지 저장을 조용히 건너뛰거나 가장 오래된 항목을 버리는 방식(데이터 유실)은 **채택하지 않는다** — 산업용 검사 시스템에서 캡쳐 이미지는 불량 판정의 증거 자료이므로 유실은 허용 안 됨.
- 대기 방식은 블로킹(예: `SemaphoreSlim` 기반 유계 큐, 또는 스핀 대기+짧은 `Thread.Sleep`)이든 무엇이든 재량 — 단, **무한 대기(deadlock 위험)는 안 되고, 반드시 상한 있는 타임아웃 또는 폴링 방식**으로 구현해야 하며, 타임아웃 시에도 데이터를 유실시키지 않고(예: 로그 경고 후 그래도 enqueue 하거나, 상한을 일시적으로 유연하게 풀어주는 등) 검사 자체가 무한정 멈추지는 않게 설계한다.
- **저장 스레드 우선순위를 올리거나 병렬화하는 방향은 이번 범위에서 우선순위 낮음** — 백프레셔(생산측 감속)가 가장 안전하고 예측 가능한 1차 해결책이며, 처리량 자체를 늘리는 건 별도 최적화로 남겨도 된다(단, 플래너가 판단하기에 더 낫다면 채택 가능 — Claude's Discretion).

### Claude's Discretion
- 정확한 큐 상한값, 백프레셔 구현 방식(세마포어/폴링/타임아웃 값), 관련 로깅(예: 큐가 상한에 걸려 대기 중임을 알리는 경고 로그) 형태는 플래너/실행자 재량.
- 기존 `_queue` 필드 타입을 `ConcurrentQueue`에서 유계 컬렉션(예: `BlockingCollection<T>` with bounded capacity)으로 교체하는 것도 검토 가능 — 기존 enqueue/dequeue 호출부와의 호환성을 고려해 판단.

</decisions>

<specifics>
## Specific Ideas

- 관련 파일: `WPF_Example\Utility\CaptureImageSaveService.cs`(큐 본체, 워커 스레드), `WPF_Example\Custom\Sequence\Inspection\Action_FAIMeasurement.cs`(:883,898 근처 `QueueFaiCapture` — enqueue 호출부), `WPF_Example\Custom\Sequence\Inspection\BatchRunService.cs`(:149-181 `TriggerNext` — 사이클을 얼마나 빨리 재트리거하는지의 맥락).
- 이 문제는 **일괄검사에 국한되지 않는다** — TCP `$TEST`로 매우 빠르게 연속 트리거되는 실제 생산 라인에서도 동일한 적체가 발생할 수 있으므로, 수정은 "일괄검사만 느리게"가 아니라 "큐 자체에 상한을 두는" 근본적인 해결이어야 한다.

</specifics>

<canonical_refs>
## Canonical References

No external specs — 에이전트 조사(Explore)로 원인 확정. CLAUDE.md의 "HImage 객체는 반드시 Dispose" 규칙과는 별개 문제(Dispose는 이미 정확함, 처리 속도 문제).

</canonical_refs>
