---
phase: quick-260806-dsn
plan: 01
subsystem: infra
tags: [halcon, memory-management, wpf, batch-inspection, image-lifecycle]

# Dependency graph
requires:
  - phase: quick-260805-ojq
    provides: "PatternMatchService static 모델 캐시 (별도 근본원인, 병행 수정 완료)"
provides:
  - "HALCON 24.11 공식 권장 SetSystem 3줄 (global_mem_cache/temporary_mem_cache/image_cache_capacity idle) — SystemHandler.Initialize() 최초 실행문"
  - "ShotConfig.ResolveFallbackImagePath() — FAI 원본 캡쳐 파일 디스크 폴백 조회"
  - "InspectionSequence.ClearCrossZImagesAfterBatchCycle() — 배치 사이클 완료 전용 크로스-Z 정리 진입점"
  - "MainView.DisplayShotImage 디스크 폴백 분기 — _image 정리 후에도 재클릭 시 빈 화면 없이 재현"
  - "InspectionListView.CleanupBatchImageMemoryAfterCycle + ResolveCurrentlyDisplayedShot — OnBatchComplete 훅 배선"
affects: [batch-inspection, memory-management, image-display]

tech-stack:
  added: []
  patterns:
    - "사이클 종료 후 정리 시 '현재 표시 중인 노드' 예외 처리 + 디스크 폴백 검증 후에만 정리(회귀보다 안전 우선)"
    - "Dispatcher.Invoke 델리게이트 내부에서 동기 실행되는 정리 로직은 시퀀스 스레드가 그 Invoke 호출에서 블로킹 대기 중이므로 별도 락 없이 안전"

key-files:
  created: []
  modified:
    - WPF_Example/SystemHandler.cs
    - WPF_Example/Custom/Sequence/Inspection/ShotConfig.cs
    - WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs
    - WPF_Example/UI/ContentItem/MainView.xaml.cs
    - WPF_Example/UI/ControlItem/InspectionListView.xaml.cs

key-decisions:
  - "Part A(HALCON 캐시 idle)와 Part B(앱 자체 이미지 보존 해제)를 동일 quick task 내 2계층으로 함께 수정 (CONTEXT.md 확정 결정)"
  - "CONTEXT.md가 가정한 기존 DisplayContextToViewer 디스크 폴백은 이번 시나리오(DisplayShotImage 경로, Action_FAIMeasurement가 ResultImagePath 미설정)에 적용되지 않음을 코드 추적으로 확인 → FAIConfig.LastOriginImageFileName 기반 신규 폴백을 DisplayShotImage에 직접 구현"
  - "정리 대상 판별은 SelectedParam 역추적(Shot/FAI/Measurement 3단계) — 디스크 폴백 경로가 없는 SHOT은 정리를 skip하여 메모리 절감보다 빈 화면 회귀 방지를 우선"

requirements-completed: [BATCH-MEM-01]

# Metrics
duration: ~20min (Task 1~3 자동 실행 구간)
completed: 2026-08-06
---

# Quick Task 260806-dsn: 일괄검사 메모리 폭증 근본원인 수정 (Part A/B) Summary

**HALCON SetSystem 캐시 idle 설정 3줄 + 배치 사이클 완료 시 비표시 SHOT 이미지 캐시 즉시 Dispose(디스크 폴백 안전망 포함) — Task 1~3 자동 실행 완료, Task 4(실기 human-verify)는 미실행 상태로 남음**

## Performance

- **Duration:** ~20 min (Task 1~3만 해당, Task 4 제외)
- **Started:** 2026-08-06 (session 진입, 정확한 시각 미기록)
- **Completed (Task 1~3):** 2026-08-06T03:15:23Z
- **Tasks:** 3 of 4 (Task 4는 `checkpoint:human-verify` — 실행자가 자동 수행 불가, 아래 "남은 작업" 참고)
- **Files modified:** 5

## Accomplishments
- Part A: `SystemHandler.Initialize()` 진입부에 HALCON 24.11 공식 문서 권장 `SetSystem` 3줄(`global_mem_cache`/`temporary_mem_cache` idle, `image_cache_capacity` 0)을 최초 실행문으로 추가. try/catch로 감싸 실패해도 앱 시작을 막지 않음.
- Part B 기반: `ShotConfig.ResolveFallbackImagePath()` 신설 — FAI별 `LastOriginImageFileName`(overlay 미포함 원본 캡쳐 파일) 중 실제 존재하는 첫 경로를 반환. `InspectionSequence.ClearCrossZImagesAfterBatchCycle()` 신설 — 기존 `ClearCrossZImages()`를 배치 사이클 완료 전용 별도 진입점으로 재노출(프로토콜 z=0 계약과 분리). `MainView.DisplayShotImage`의 else 분기에 이 디스크 폴백을 실제로 연결 — `_image`가 비어도 재클릭 시 원본 재로드를 시도한 뒤에만 "NO Image" 표시.
- Part B 트리거: `InspectionListView.OnBatchComplete`(`BatchRunService.OnBatchComplete`, 사이클당 정확히 1회 발화) 안에서 `CleanupBatchImageMemoryAfterCycle(_batchShots)`를 호출 — 현재 화면 표시 중인 SHOT(`ResolveCurrentlyDisplayedShot`으로 `SelectedParam`을 Shot/FAI/Measurement 3단계로 역추적)을 제외한 나머지 SHOT의 `ShotConfig._image` + 대응 `ActionContext.ResultHalconImage`를 Dispose하고, 크로스-Z 이미지 저장소도 함께 정리. 디스크 폴백 경로가 없는 SHOT은 정리를 skip(회귀 방지 우선).

## Task Commits

Each task was committed atomically:

1. **Task 1: Part A — HALCON 메모리 캐시 idle 설정 (SystemHandler.cs)** - `3a5f4b4` (feat)
2. **Task 2: Part B 기반 — 디스크 폴백 헬퍼 + 크로스-Z 정리 진입점 + Shot 재클릭 안전망** - `8c327c5` (feat)
3. **Task 3: Part B 정리 트리거 — 배치 사이클 완료 시 비표시 Shot 이미지 캐시 해제** - `534c742` (feat)

**Plan metadata:** 본 SUMMARY.md 및 STATE.md/ROADMAP.md는 오케스트레이터가 별도 커밋 (실행자는 커밋하지 않음 — 지시사항에 따름).

_Note: 이 quick task는 TDD 대상이 아님(순수 리소스 라이프사이클 수정) — RED/GREEN 게이트 해당 없음._

## Files Created/Modified
- `WPF_Example/SystemHandler.cs` - `using HalconDotNet;` 추가 + `Initialize()` 진입부에 HALCON 캐시 idle 설정 3줄(try/catch)
- `WPF_Example/Custom/Sequence/Inspection/ShotConfig.cs` - `ResolveFallbackImagePath()` 신설
- `WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs` - `ClearCrossZImagesAfterBatchCycle()` 신설(기존 private `ClearCrossZImages()` 재사용)
- `WPF_Example/UI/ContentItem/MainView.xaml.cs` - `DisplayShotImage`의 else 분기에 디스크 폴백 추가(if 분기 무변경)
- `WPF_Example/UI/ControlItem/InspectionListView.xaml.cs` - `OnBatchComplete`에 정리 호출 1줄 추가 + `CleanupBatchImageMemoryAfterCycle`/`ResolveCurrentlyDisplayedShot` 신설

## Decisions Made
- 플랜에 명시된 대로 실행 — 별도 아키텍처 결정 없음(CONTEXT.md에서 이미 LOCKED 확정).
- **plan-check 경미 지적사항 2건을 코드/체크포인트 텍스트에 반영**(아래 "적용된 정정 사항" 참고) — 기능 로직 변경 없음, 문서/주석/체크리스트 정확도만 개선.

## 적용된 정정 사항 (plan-check 경미 지적 반영)

**1. [`InspectionListView.xaml.cs` — 동시성 안전 근거 주석 정정]**
plan 원문 주석은 "`_seq.State == Idle`(OnBatchComplete 시점에 항상 보장됨)"을 안전 근거로 제시했으나, 실제 이 시점의 시퀀스 상태는 `EContextState.Finish`이며 `Idle`은 다음 `MainExecute()` tick에야 설정됨(오정정). **실제 안전 근거는 시퀀스 스레드가 `Dispatcher.Invoke` 안에서 동기적으로 블로킹되어 이 콜백이 반환할 때까지 대기한다는 점**이다. 커밋된 코드의 주석은 이 정확한 메커니즘으로 작성했다(`CleanupBatchImageMemoryAfterCycle` 위 주석, `534c742`). 기능 코드(정리 로직 자체)는 plan 그대로이며 변경 없음 — 주석 텍스트만 정정.

**2. [Task 4 checkpoint 6(b) 절차 정정]**
plan 원문 6(b)는 "4번 노드의 이미지가 여전히 표시되는지"를 그냥 관찰하도록 지시했으나, `MainWindow.OnSequenceFinish`가 사이클 종료 직후 **트리 선택과 무관하게 배치에서 마지막 처리된 SHOT으로 뷰어를 자동 갱신**하는 기존(무관) 동작이 있어, 화면에 떠 있는 것을 그냥 보면 다른(하지만 유효한) SHOT을 보고 있으면서 4번 노드를 확인했다고 착각할 위험이 있다. 아래 "남은 작업 — Task 4" 섹션의 6(b)에 **재클릭 지시 + 이 사전 존재 quirk 안내 문구**를 반영했다.

## Deviations from Plan

None - plan의 Task 1~3 액션을 정확히 그대로 실행함(내용 anchor 기준 삽입, 문자 단위 일치 확인 후 교체). 위 "적용된 정정 사항" 2건은 오케스트레이터가 사전에 지시한 리팩토링이며 plan의 기능 로직을 바꾸지 않음(문서/주석 정정으로 분류, Rule 1~3 해당 없음).

## Issues Encountered

**out-of-scope 발견 (미수정, deferred): `WPF_Example/App.xaml.cs`에 사용자의 사전 실험적 미커밋 변경이 존재**
- `App.xaml.cs`의 `Application_Startup`에 이번 plan과 무관한, 이미 이 세션 시작 전부터 uncommitted 상태였던 HALCON `SetSystem` 실험 코드가 있음: `parallelize_operators=false`, `reentrant=true`, `global_mem_cache="exclusive"`, `temporary_mem_cache="true"`, `mmx_enable="false"` (그리고 이번 plan과 동일한 idle 3줄이 주석 처리된 채 남아있음, line 50-52).
- 호출 순서 확인: `Application_Startup`(App.xaml.cs) → `new MainWindow()` 생성자 → `SystemHandler.Initialize()`. 즉 이번 plan이 추가한 idle 설정(`SystemHandler.Initialize()`)이 App.xaml.cs의 실험적 `exclusive`/`true` 설정보다 **항상 나중에 실행되어 값을 덮어쓴다** — 기능적 충돌은 없으나, App.xaml.cs의 실험값은 사실상 무의미해진다.
- 이 파일은 plan의 `files_modified` 목록에 없고 사용자의 별도 실험(진행 중일 가능성)이라 판단하여 **손대지 않음**(scope boundary 원칙). 사용자가 이 실험을 유지/정리할지 확인 필요 — Task 4 실기 검증 시 이 값들이 최종 결과(메모리 감소량)에 영향을 줄 수 있으므로 참고.

## User Setup Required

None - 외부 서비스 설정 불필요.

## 남은 작업 — Task 4 (checkpoint:human-verify, 실행자가 자동 수행 불가)

Task 1~3의 자동 검증(빌드 PASS, anchor/순서 확인, 단일 트리거 지점 확인)은 모두 통과했으나, **실제 메모리 감소량과 화면 재현 정확성은 실기 하드웨어/SIMUL 이미지로만 검증 가능**하다. 아래 절차를 사용자가 직접 수행해야 한다(리팩토링된 6(b) 반영):

1. 실행 중인 이전 인스턴스가 있으면 완전히 종료한다. 최신 커밋(`534c742`) 기준으로 Debug/x64 재빌드 후 앱을 새로 실행한다.
   - 참고: 위 "Issues Encountered"의 `App.xaml.cs` 실험값이 여전히 uncommitted 상태로 남아있으면 그 상태 그대로 빌드에 포함된다(이번 plan이 그 파일을 건드리지 않았으므로).
2. PowerShell에서 메모리를 실시간 관찰할 준비를 한다(별도 창):
   ```powershell
   while ($true) { $p = Get-Process DatumMeasurement -ErrorAction SilentlyContinue; if ($p) { "{0:HH:mm:ss} {1:N0} MB" -f (Get-Date), ($p.WorkingSet64/1MB) }; Start-Sleep -Seconds 2 }
   ```
3. 트리에서 BOTTOM 시퀀스를 선택하고, 오늘 재현 때와 동일하게 약 30개 측정 항목(SHOT)을 체크한다.
4. 임의의 SHOT/FAI/Measurement 노드 하나를 클릭해 화면에 이미지가 표시된 상태로 둔다(이 노드가 "현재 표시 중인 노드"가 된다 — 정리 후에도 이 노드만은 예외 없이 그대로 보여야 한다).
5. "일괄검사" 버튼을 눌러 1사이클을 실행하고 완료를 기다린다.
6. 사이클 완료 후 **아무것도 클릭하지 말고 30초 이상 대기**하며 PowerShell 메모리 로그를 관찰한다.
   - **(a) 확인**: 메모리가 사이클 진행 중 올라갔다가, 완료 후 수 GB대가 아니라 **수백 MB대**로 떨어지는지 확인한다(오늘 재현 시 1→2.7→8.3→...→12.4GB로 계단식 증가 후 미감소였던 것과 대비).
   - **(b) 확인 (절차 수정됨 — 그냥 보지 말고 재클릭할 것)**: 4번에서 표시해뒀던 그 노드를 **다시 한 번 클릭**해서 이미지가 정상적으로 보이는지(빈 화면/깨짐 없음) 확인한다.
     - **주의(사전 존재 quirk, 이번 수정과 무관)**: 사이클이 끝나는 순간 `MainWindow.OnSequenceFinish`가 트리 선택과 무관하게 배치에서 **마지막으로 처리된 SHOT**으로 뷰어 화면을 자동 갱신하는 기존 동작이 있다. 따라서 사이클 종료 직후 "화면에 떠 있는 것"이 4번에서 선택한 그 노드가 아닐 수 있다 — 이는 이번 수정이 만든 회귀가 아니라 이전부터 있던 무관한 동작이다. 반드시 4번 노드를 **재클릭**해서 확인해야 정확한 테스트가 된다.
   - 30초간 정리 로직 자체가 화면을 강제로 바꾸지는 않으므로, 재클릭 시 이미지가 안 보이거나 깨져 있으면 그때만 회귀로 간주한다.
7. 트리에서 4번과 **다른** SHOT/FAI/Measurement 노드 여러 개를 순서대로 클릭한다.
   - **(c) 확인**: 각 노드마다 이미지 + 측정 overlay(에지 표시 등)가 **정상적으로 표시되는지** 확인한다(디스크 폴백 경로 — 클릭 즉시 표시되면 정상, 빈 화면/"NO Image"가 뜨면 회귀).
8. 7번에서 클릭했던 SHOT 중 하나를 선택한 채로 단일 RUN(또는 그 SHOT만 다시 일괄검사)으로 재실행한다.
   - **(d) 확인**: 크래시나 예외 팝업 없이 정상적으로 재검사가 수행되고 결과/이미지가 갱신되는지 확인한다.

**Resume-signal:** (a)(b)(c)(d) 모두 정상이면 "approved". 하나라도 문제가 있으면 어떤 단계에서 무엇이 잘못됐는지(메모리가 여전히 안 줄어듦 / 특정 노드 빈 화면 / 재실행 시 예외 등) 구체적으로 기술해서 재개.

## Next Phase Readiness
- Task 1~3(코드/빌드/구조 검증)은 완료 상태 — 추가 코드 작업 불필요.
- Task 4(실기 human-verify)가 완료되어야 이 quick task가 최종 승인(signed-off)된다. 승인 전까지 STATE.md는 "PARTIAL — Task 4 실기 검증 대기"로 표기 권장.
- App.xaml.cs의 uncommitted 실험적 변경은 이번 plan 범위 밖이므로 사용자가 별도로 정리/커밋 여부를 결정해야 함.

---
*Phase: quick-260806-dsn*
*Completed (Task 1~3): 2026-08-06*

## Self-Check: PASSED

- FOUND: WPF_Example/SystemHandler.cs
- FOUND: WPF_Example/Custom/Sequence/Inspection/ShotConfig.cs
- FOUND: WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs
- FOUND: WPF_Example/UI/ContentItem/MainView.xaml.cs
- FOUND: WPF_Example/UI/ControlItem/InspectionListView.xaml.cs
- FOUND: .planning/quick/260806-dsn-overlay-window-reuse/260806-dsn-SUMMARY.md
- FOUND commit: 3a5f4b4 (Task 1)
- FOUND commit: 8c327c5 (Task 2)
- FOUND commit: 534c742 (Task 3)
