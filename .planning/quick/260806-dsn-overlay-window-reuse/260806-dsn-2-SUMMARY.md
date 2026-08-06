---
phase: quick-260806-dsn-2
plan: 01
subsystem: infra

# Dependency graph
requires:
  - phase: quick-260806-dsn
    provides: "CleanupBatchImageMemoryAfterCycle 즉시 정리 경로(534c742) + ShotConfig.ResolveFallbackImagePath/InspectionSequence.ClearCrossZImagesAfterBatchCycle/MainView.DisplayShotImage 디스크 폴백(8c327c5)"
provides:
  - "InspectionListView._pendingImageCleanup + DispatcherTimer 기반 재시도 정리 큐 — 저장 큐가 나중에 따라잡으면 배치 뒤쪽 SHOT도 결국 메모리에서 정리됨"
  - "ClearShotImageCache 공유 헬퍼 — 즉시/재시도 두 경로가 동일 dispose 로직 재사용"
affects: [batch-inspection, memory-management, image-display]
subsystem: infra

tech-stack:
  added: []
  patterns:
    - "즉시 실패 시 영구 포기 대신 인스턴스 필드 재시도 대기열 + DispatcherTimer(UI 스레드, 지연 생성, 대기열 빌 때 자동 Stop)로 전환 — 동기 블로킹 대기 없이 비동기 저장 큐를 따라잡음"
    - "재시도 틱마다 '현재 표시 중 SHOT'과 '재실행 중(EContextState.Running) 여부'를 매번 재조회하여 사이클 종료 시점 스냅샷을 재사용하지 않음"
    - "SHOT별 재시도 횟수 상한(Dictionary<ShotConfig,int>)으로 영구 실패 케이스의 무한 누적 방지, 초과 시 조용히 포기(예외/알림 없음)"

key-files:
  created: []
  modified:
    - WPF_Example/UI/ControlItem/InspectionListView.xaml.cs

key-decisions:
  - "CONTEXT.md LOCKED 결정 그대로 채택: 동기 대기(Thread.Sleep/.Wait()/.Result/Task.Delay) 도입 절대 금지 — DispatcherTimer 5초 간격 폴링으로만 재시도"
  - "즉시 정리 경로(폴백 이미 존재 SHOT)는 무변경 유지 — 534c742 로직을 ClearShotImageCache 헬퍼로 이동만 하고 동작은 동일"
  - "재시도 틱에서 대상 SHOT의 소속 시퀀스가 EContextState.Running이면 정리를 건너뛰고 재시도 횟수도 소모하지 않음 — 새 배치/단일 RUN이 같은 SHOT을 재실행 중일 때 ActionContext.ResultHalconImage 동시 접근 레이스 방지"

requirements-completed: [BATCH-MEM-02]

# Metrics
duration: ~15min (Task 1 자동 실행 구간)
completed: 2026-08-06
---

# Quick Task 260806-dsn-2: 배치 정리 로직의 비동기 저장 큐 레이스 수정 Summary

**CleanupBatchImageMemoryAfterCycle을 "즉시 실패 시 영구 포기"에서 "재시도 대기열 + DispatcherTimer로 나중에 따라잡기"로 재작성 — Task 1(코드/빌드/구조검증) 완료, Task 2(실기 human-verify)는 미실행 상태로 남음**

## Performance

- **Duration:** ~15 min (Task 1만 해당)
- **Completed (Task 1):** 2026-08-06
- **Tasks:** 1 of 2 (Task 2는 `checkpoint:human-verify` — 실행자가 자동 수행 불가, 아래 "남은 작업" 참고)
- **Files modified:** 1

## Accomplishments

- `CleanupBatchImageMemoryAfterCycle`을 재작성: 사이클 완료 직후 폴백 파일이 이미 있는 SHOT은 기존과 동일하게 즉시 정리(회귀 없음), 폴백이 아직 없는 SHOT은 버리지 않고 `_pendingImageCleanup` 대기열에 추가(중복 가드 포함).
- `ClearShotImageCache(shot)` 공유 헬퍼 신설 — 기존 534c742의 `ShotConfig.ClearImage()` + `ActionContext.ResultHalconImage` Dispose 로직을 그대로 옮겨 즉시 경로와 재시도 경로가 함께 재사용(중복 구현 없음).
- `EnsurePendingImageCleanupTimer()`(지연 생성) + `PendingImageCleanupTimer_Tick`(5초 간격) 신설: 매 틱마다 `ResolveCurrentlyDisplayedShot()`을 재조회(사이클 종료 시점 값 재사용 안 함)해 현재 표시 중 SHOT은 정리 없이 대기열에서만 제거, 소속 시퀀스가 `EContextState.Running`이면 재시도 횟수 소모 없이 다음 틱까지 보류, 폴백이 생기면 `ClearShotImageCache`로 정리, SHOT별 24회(약 2분) 초과 시 조용히 포기.
- 대기열이 비면 타이머 자동 `Stop()`, 다음 사이클에서 대기열에 새 항목이 생기면 `Start()` 재사용(no-op 안전).
- 작업 트리에 남아있던 `TEMP DIAG (260806)`/`[DIAG-260806]` 임시 진단 로그 3곳(시작/SKIP/종료)을 전량 제거(재작성 과정에서 자연 소거, 별도 조치 불필요).

## Task Commits

Each task was committed atomically:

1. **Task 1: 저장 큐 레이스 수정 — 재시도 대기열 + DispatcherTimer 도입, TEMP DIAG 제거 (InspectionListView.xaml.cs)** - `b133c32` (fix)

**Plan metadata:** 본 SUMMARY.md 및 STATE.md/ROADMAP.md는 오케스트레이터가 별도 커밋 (실행자는 커밋하지 않음 — 지시사항에 따름).

_Note: 이 quick task는 TDD 대상이 아님(순수 리소스 라이프사이클 수정) — RED/GREEN 게이트 해당 없음._

## Files Created/Modified

- `WPF_Example/UI/ControlItem/InspectionListView.xaml.cs` — 필드 4개 추가(`_pendingImageCleanup`, `_pendingImageCleanupRetryCount`, `_pendingImageCleanupTimer`, 상수 2개) + `CleanupBatchImageMemoryAfterCycle` 재작성 + `ClearShotImageCache`/`EnsurePendingImageCleanupTimer`/`PendingImageCleanupTimer_Tick` 신설 + TEMP DIAG 로그 3곳 제거.

## Verification Results

자동 검증(`<automated>` 커맨드) 결과:

- **빌드**: Debug/x64 MSBuild — error 0, `DatumMeasurement.exe` 정상 생성 (사전 존재 경고만 있음, 이번 변경과 무관).
- **DIAG_REMOVED_OK**: `TEMP DIAG`/`DIAG-260806` 마커 0건 확인.
- **RETRY_MECHANISM_WIRED_OK**: `_pendingImageCleanup`/`_pendingImageCleanupTimer`/`PendingImageCleanupTimer_Tick`(2회 이상)/`ClearShotImageCache`(3회 이상)/`EContextState.Running`(1회 이상) 전부 존재 및 배선 확인.
- **NO_SYNC_WAIT_OK**: `Thread.Sleep`/`.Wait()`/`.Result`/`Task.Delay` 0건 확인.
- **단일 호출 지점**: `grep -c "CleanupBatchImageMemoryAfterCycle"` 결과가 plan 예상값(N=2)과 달리 **N=3**으로 나왔으나, 3번째 매치는 `PendingImageCleanupTimer_Tick` 위 doc-comment 안에서 이 메서드명을 프로즈로 언급한 것(plan이 제공한 코드에 그대로 포함된 문구, Task 1 action 블록 원문 그대로 옮김)이었다. 실제 호출 표현식은 `Grep`으로 직접 확인 시 `OnBatchComplete` 안 1곳뿐(line 627)이고 나머지는 정의(line 641)와 주석(line 708)이다 — plan의 "호출부는 OnBatchComplete 1곳뿐" 요구사항은 실질적으로 충족됨. 이 verify 커맨드의 grep은 주석 텍스트도 함께 세므로 plan 자체의 예상 카운트가 살짝 부정확했던 것으로 판단(코드는 plan 원문 그대로 유지, 별도 수정 없음).
- **파일 스코프**: `ShotConfig.ResolveFallbackImagePath`(ShotConfig.cs:426)/`InspectionSequence.ClearCrossZImagesAfterBatchCycle`(InspectionSequence.cs:881)/`MainView.DisplayShotImage`의 `shot.ResolveFallbackImagePath()` 호출(MainView.xaml.cs:209) 전부 그대로 존재 — 무변경 확인.
- `git status --short` / `git diff --diff-filter=D` — `InspectionListView.xaml.cs` 1개 파일만 수정, 의도치 않은 삭제 없음.

## Decisions Made

- 플랜에 명시된 대로 실행 — 별도 아키텍처 결정 없음(CONTEXT.md에서 이미 LOCKED 확정, 독립 plan-check가 사전에 블로커 없음 확인).

## Deviations from Plan

None - Task 1 액션을 plan 원문 그대로 실행함(content-anchor 기준 필드 삽입 + `OnBatchComplete`~`ResolveCurrentlyDisplayedShot` 블록 전체 교체). 위 "Verification Results"의 단일 호출 지점 카운트 불일치는 plan 자체가 제공한 코드(doc-comment 내 메서드명 언급)로 인한 verify 스크립트의 텍스트 매칭 부작용이며, 코드 로직이나 plan 내용을 임의로 바꾼 것이 아니므로 Rule 1~4 해당 없음(문서화만).

## Issues Encountered

없음 — 빌드/구조 검증 전부 1차 시도에서 통과.

## User Setup Required

None - 외부 서비스 설정 불필요.

## 남은 작업 — Task 2 (checkpoint:human-verify, 실행자가 자동 수행 불가)

Task 1의 자동 검증(빌드 PASS, TEMP DIAG 제거, 재시도 메커니즘 배선, 동기 대기 없음, 단일 트리거)은 모두 통과했으나, **실제로 배치 뒤쪽 SHOT이 결국(1~2분 이내) 정리되는지, 그리고 재시도 대기/정리된 SHOT을 조작해도 안전한지는 자동 검증이 불가능**하다. 아래 절차를 사용자가 직접 수행해야 한다(plan Task 2 `<how-to-verify>` 원문):

1. 실행 중인 이전 인스턴스가 있으면 완전히 종료한다. 최신 커밋(`b133c32`) 기준으로 Debug/x64 재빌드 후 앱을 새로 실행한다.
2. PowerShell에서 메모리를 실시간 관찰할 준비를 한다(별도 창):
   ```powershell
   while ($true) { $p = Get-Process DatumMeasurement -ErrorAction SilentlyContinue; if ($p) { "{0:HH:mm:ss} {1:N0} MB" -f (Get-Date), ($p.WorkingSet64/1MB) }; Start-Sleep -Seconds 2 }
   ```
3. 트리에서 BOTTOM 시퀀스를 선택하고, 오늘 재현 때와 동일하게 약 30개 측정 항목(SHOT)을 체크한다.
4. 임의의 SHOT/FAI/Measurement 노드 하나를 클릭해 화면에 이미지가 표시된 상태로 둔다(이 노드가 "현재 표시 중인 노드" — 재시도 도중에도 이 노드만은 예외 없이 정리되면 안 된다).
5. "일괄검사" 버튼을 눌러 1사이클을 실행하고 완료를 기다린다.
6. 사이클 완료 후 **아무것도 클릭하지 말고 1~2분 이상 대기**하며 PowerShell 메모리 로그를 관찰한다(저장 큐 지연이 세션 진행에 따라 10초대~15초대 이상까지 늘어나는 특성이 있었으므로, 필요하면 더 길게 기다린다 — `D:\Data\Error\...` 로그의 `[CaptureImageSaveService] ... depth=`가 0 근처로 내려가면 큐가 거의 비워진 신호다).
   - **(a) 확인**: 사이클 완료 직후엔 메모리가 즉시 다 떨어지지 않을 수 있지만, 1~2분 이내에 **추가로 계단식으로 더 감소**하는지 확인한다(재시도 타이머가 뒤쪽 SHOT들을 순차적으로 정리하는 신호).
   - **(b) 확인**: 4번에서 표시해뒀던 노드를 **다시 한 번 클릭**해서 이미지가 정상적으로 보이는지 확인한다(사이클 종료 시 뷰어가 다른 SHOT으로 자동 갱신되는 기존 무관 동작이 있으므로 재클릭해서 확인할 것).
7. 대기 중간(예: 사이클 완료 후 10~20초 시점, 아직 재시도가 덜 끝났을 시점)에 트리에서 4번과 **다른** SHOT/FAI/Measurement 노드 여러 개를 순서대로 클릭한다.
   - **(c) 확인**: 아직 재시도 대기열에 있는 SHOT을 클릭해도 예외/크래시 없이 동작하는지 확인한다 — 메모리에 아직 이미지가 남아있으면 정상 표시, 이미 재시도로 정리됐다면 디스크 폴백으로 표시, 정말 아무것도 없는 극히 드문 경우엔 "NO Image"만 뜨고(빈 화면 크래시 아님) 앱이 죽지 않아야 한다.
8. 1~2분 대기 후(재시도가 끝난 뒤), 7번에서 클릭했던 SHOT 중 하나를 선택한 채로 단일 RUN(또는 그 SHOT만 다시 일괄검사)으로 재실행한다.
   - **(d) 확인**: 크래시나 예외 팝업 없이 정상적으로 재검사가 수행되고 결과/이미지가 갱신되는지 확인한다.
9. (선택) 8번 재실행 직후 몇 초 이내에 다시 일괄검사를 시작해, 이전 사이클의 재시도 대기열이 아직 안 비워진 상태에서 새 사이클이 겹쳐도 앱이 죽거나 멈추지 않는지 확인한다.

**Resume-signal:** (a)(b)(c)(d) 모두 정상이면 "approved". 하나라도 문제가 있으면 어떤 단계에서 무엇이 잘못됐는지(메모리가 1~2분 후에도 안 줄어듦 / 특정 노드 빈 화면·크래시 / 재실행 시 예외 / 새 사이클 겹칠 때 멈춤 등) 구체적으로 기술해서 재개.

## Next Phase Readiness

- Task 1(코드/빌드/구조 검증)은 완료 상태 — 추가 코드 작업 불필요.
- Task 2(실기 human-verify)가 완료되어야 이 quick task가 최종 승인(signed-off)된다. 승인 전까지 STATE.md는 "PARTIAL — Task 2 실기 검증 대기"로 표기 권장.

---
*Phase: quick-260806-dsn-2*
*Completed (Task 1): 2026-08-06*

## Self-Check: PASSED

- FOUND: WPF_Example/UI/ControlItem/InspectionListView.xaml.cs
- FOUND commit: b133c32 (Task 1)
