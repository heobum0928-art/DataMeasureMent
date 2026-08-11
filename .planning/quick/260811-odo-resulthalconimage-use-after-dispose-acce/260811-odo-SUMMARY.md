---
phase: quick-260811-odo
plan: 01
subsystem: infra
tags: [halcon, threading, refcount, memory-safety, wpf]

requires:
  - phase: quick-260810-hbk (capture-render-per-fai-slow)
    provides: "SharedHImage refcount 원본 관용구(WPF_Example/Utility/CaptureImageSaveService.cs) — 이번 작업이 재사용"
provides:
  - "SequenceContext 결과 이미지 refcount 소유권 모델(AcquireResultImage/SetResultImageOwned/CloneResultImage)"
  - "SharedHImage.TryAddRef() — 해제 여부를 원자적으로 알려주는 획득 시도 API"
  - "동시 swap/release <-> acquire/clone 레이스 자동 검증 하네스(scratchpad, sentinel 무결성 기반)"
affects: [sequence-engine, main-view, inspection-list-view, repeat-run-service]

tech-stack:
  added: []
  patterns:
    - "결과 이미지 소유권: refcount(SharedHImage) + Volatile.Read/Interlocked.Exchange, 원시 HImage 참조를 클래스 밖으로 노출하지 않는다"

key-files:
  created: []
  modified:
    - WPF_Example/Utility/CaptureImageSaveService.cs
    - WPF_Example/Sequence/Sequence/SequenceContext.cs
    - WPF_Example/Sequence/Sequence/SequenceBase.cs
    - WPF_Example/UI/ContentItem/MainView.xaml.cs
    - WPF_Example/Custom/Sequence/Inspection/RepeatRunService.cs
    - WPF_Example/UI/ControlItem/InspectionListView.xaml.cs

key-decisions:
  - "SharedHImage refcount 재사용((c)안) 채택 — 독자 4개(UI 2개+시퀀스 2개)를 하나의 모델로 덮고, 시퀀스 스레드 블로킹 0, 복사 횟수 불변을 모두 만족하는 유일한 안"
  - "소유권 이전((a)안) 탈락 — UI 127MB memcpy 1회를 사이클마다 제거할 수 있었으나, 트리 클릭 독자(R2)를 못 덮고 MainWindow.xaml.cs 수정이 강제되며 핀 누적 메모리 리스크가 있어 포기(후속 최적화 후보로 기록)"
  - "락 보호((b)안) 탈락 — ThreadPriority.Highest 시퀀스 스레드가 UI 127MB 복사를 대기하게 되어 하드 제약(택트 회귀 없음) 직접 위반"
  - "ActionContext.ResultHalconImage(다른 클래스, writer가 수정 금지 파일)는 이번 범위에서 제외 — 비원자적 writer가 남으면 절반만 고치는 것이 오히려 위험"

requirements-completed: [ODO-01, ODO-02, ODO-03, ODO-04]

coverage:
  - id: D1
    description: "SequenceContext 결과 이미지에 refcount 소유권 모델 도입 — 원시 HImage 참조를 클래스 밖으로 노출하지 않음"
    requirement: "ODO-01"
    verification:
      - kind: unit
        ref: "grep 게이트: AcquireResultImage>=2, SetResultImageOwned>=3, CloneResultImage>=2, Interlocked.Exchange>=1, _resultShared>=3 (SequenceContext.cs 내)"
        status: pass
      - kind: integration
        ref: "MSBuild Debug/x64 — 원시 프로퍼티 제거로 미이관 소비자가 남으면 컴파일 실패(완전성 증명)"
        status: pass
    human_judgment: false
  - id: D2
    description: "확정 크래시 지점(MainView.DisplayContextToViewer)이 획득 구간으로 보호되어 UI 독자 2개(DisplaySequenceContext + 트리클릭 DisplayParam)가 동시에 닫힘"
    requirement: "ODO-02"
    verification:
      - kind: unit
        ref: "grep 게이트: MainView.xaml.cs 내 AcquireResultImage>=1"
        status: pass
    human_judgment: true
    rationale: "실기 자동 연속 반복으로 AVE 미재현 확인은 사용자 UAT 몫(이 플랜의 완료 조건에 명시적으로 미포함) — 정적 게이트와 하네스는 프로토콜 안전성만 증명"
  - id: D3
    description: "동시 swap/release <-> acquire/clone 레이스에서 sentinel(균일 픽셀값) 무결성이 깨지지 않음을 실제 HALCON 이미지로 자동 검증"
    requirement: "ODO-03"
    verification:
      - kind: integration
        ref: "scratchpad/odo-harness/build-and-run.sh (RefRaceHarness.exe) — Phase A/B 전체 출력: generations=135815 acquireSuccess=117074 acquireMiss=55025587 integrityFailures=0 exceptions=0, 종료코드 0, PASS"
        status: pass
    human_judgment: false
  - id: D4
    description: "잔여 경로(RepeatRunService Background 우회, InspectionListView ActionContext 잔여 위험)를 문서화하고 기존 부분수정과의 관계를 명시(코드 동작 변경 없음)"
    requirement: "ODO-04"
    verification:
      - kind: unit
        ref: "grep 게이트: RepeatRunService.cs 내 DispatcherPriority.Background>=1(코드 유지 확인) + 저장소 전체 스윕(raw_seqcontext_refs=0)"
        status: pass
    human_judgment: false

duration: 25min
completed: 2026-08-11
status: complete
---

# Quick Task 260811-odo: SequenceContext 결과 이미지 use-after-dispose 근본 수정 Summary

**해제된 힙을 memcpy 하던 `SequenceContext.ResultHalconImage` 원시 참조를 refcount(SharedHImage) 소유권 모델로 대체해, 확정 재현된 `AccessViolationException`(catch 불가한 Corrupted State Exception) 크래시 경로를 구조적으로 제거했다. 새로 발견된 두 번째 UI 독자(트리 노드 클릭)까지 동일 지점 수정으로 동시에 닫혔고, 저장소 밖 독립 하네스가 실제 HALCON 127MP급 이미지로 쓰기 1 + 읽기 2 스레드 경합을 sentinel 무결성 검사로 자동 검증(PASS, 무결성 실패 0, 예외 0)했다.**

## Performance

- **Duration:** 약 25분 (탐색/읽기 포함)
- **Tasks:** 3/3 완료
- **Files modified:** 6 (저장소 내) + 저장소 밖 scratchpad 하네스 2개

## Accomplishments

- `SequenceContext`(결과 이미지 소유자)와 `SharedHImage`(refcount 프리미티브, 260810 라운드에서 이미 하드닝 완료)를 결합해 해제자 2곳(`Clear`, `CopyFrom(ActionContext)`)과 독자 3곳(`SaveResultImage`, `ActionContext.CopyFrom(SequenceContext)`, `MainView.DisplayContextToViewer`)을 전부 원자적 소유권 API 경유로 전환. 원시 `HImage` 프로퍼티 완전 제거 — 컴파일 성공이 전수 이관의 증명.
- 확정 크래시 콜스택(`HImage.CopyImage → HalconImageBridge.Clone → MainResultViewerControl.LoadImage → MainView.DisplayContextToViewer → MainView.DisplaySequenceContext → MainWindow.OnSequenceFinish`)의 진입점을 `AcquireResultImage()` 획득 구간으로 감쌌다. 이번 조사에서 **두 번째 UI 독자**(`InspectionListView` 트리 노드 클릭 → `SetParam` → `DisplayParam` → 동일한 `DisplayContextToViewer`)를 새로 발견했고, 같은 한 지점 수정으로 함께 닫혔다.
- 저장소 밖(scratchpad) 독립 콘솔 하네스가 저장소의 실제 `SharedHImage` 소스를 추출(복붙이 아님)해 `csc.exe` 로 컴파일, 실제 HALCON 이미지(1024x1024 / 4000x4000 두 페이즈)로 쓰기 1 + 읽기 2 스레드 경합을 실행. Phase A/B 합산 세대 135,815회, 획득 성공 117,074회, sentinel 무결성 실패 0건, 예외 0건, 종료 코드 0 — `PASS`.
- 기존 부분수정(`RepeatRunService.TriggerNext` 의 `DispatcherPriority.Background` 우회)이 이번 수정과 모순되지 않고 역할이 분리됨을 문서화(코드 유지, 주석 갱신). `InspectionListView.ClearShotImageCache` 에는 `ActionContext.ResultHalconImage`(수정 금지 파일이 writer인 다른 클래스)의 잔여 이중 해제 창을 조건과 함께 코드 주석으로 남김(동작 변경 없음).

## Task Commits

Each task was committed atomically:

1. **Task 1: 결과 이미지 소유권 모델 end-to-end 배선** - `8c06ce0` (feat)
2. **Task 2: 동시 레이스 자동 검증 하네스** - 저장소 파일 변경 없음(scratchpad 전용, 커밋 대상 아님)
3. **Task 3: 잔여 경로 문서화 + 최종 빌드** - `27cc255` (docs)

_TDD 아님 — `type="tracer"`(Task 1) + `type="auto"`(Task 2, 3)._

## Files Created/Modified

- `WPF_Example/Utility/CaptureImageSaveService.cs` - `SharedHImage.TryAddRef()` 추가(성공 여부를 원자적으로 알려주는 획득 시도 API), 기존 `AddRef()`는 이 메서드로 위임(회귀 0), `Image` 게터에 계약 주석 추가
- `WPF_Example/Sequence/Sequence/SequenceContext.cs` - 원시 `HImage` 공개 프로퍼티 제거, `private SharedHImage _resultShared` + `AcquireResultImage`/`SetResultImageOwned`/`CloneResultImage` 3개 API 추가, 해제자 2곳 전환, `ActionContext.CopyFrom(SequenceContext)` 를 `CloneResultImage()` 경유로 전환(다른 클래스인 `ActionContext` 자기 필드 처리는 무변경)
- `WPF_Example/Sequence/Sequence/SequenceBase.cs` - `SaveResultImage` 가 `Context.CloneResultImage()` 로 스냅샷 획득(워커 람다 내부는 무변경)
- `WPF_Example/UI/ContentItem/MainView.xaml.cs` - `DisplayContextToViewer` 확정 크래시 지점을 `AcquireResultImage()` 획득 구간(`try`/`finally Release()`)으로 감쌈, 디스크 폴백 경로는 무변경
- `WPF_Example/Custom/Sequence/Inspection/RepeatRunService.cs` - `TriggerNext` 의 `DispatcherPriority.Background` 우회 코드 유지, 안전 역할과 순서보장 역할이 분리됐음을 명시하는 주석 갱신
- `WPF_Example/UI/ControlItem/InspectionListView.xaml.cs` - `ClearShotImageCache` 에 `ActionContext.ResultHalconImage` 잔여 이중 해제 창(조건 a/b/c) 주석 추가(코드 동작 무변경)
- `C:/Users/admin/.../scratchpad/odo-harness/RefRaceHarness.cs`, `build-and-run.sh` - 저장소 밖 독립 레이스 검증 하네스(신규, 저장소 커밋 대상 아님)

## Decisions Made

1. **채택 — (c) `SharedHImage` refcount 재사용.** 독자 4개(UI 2개 + 시퀀스 2개)를 하나의 모델로 전부 덮고, 시퀀스 스레드 블로킹이 0(해제자는 `Interlocked.Exchange` + `Release()` 1회짜리 lock뿐)이며, 복사 횟수가 수정 전과 정확히 동일(액션 사본 1 + `CopyFrom` clone 1 + 뷰어 clone 1)하다는 3가지가 하드 제약을 모두 만족하는 유일한 안이었다. 동시 read 안전성은 260810 round4 실측(127MP, 워커 2개 동시읽기 150회 반복 오염 0건)에 근거해 신뢰했고, Task 2 하네스가 이번 조합(UI clone ↔ 시퀀스 clone)에 대해서도 재확인했다.
2. **탈락 — (a) 소유권 이전(UI 로 단독 이관).** UI 127MB memcpy 1회/사이클을 제거할 수 있는 실제 성능 이득이 있었으나, 트리 클릭 독자(R2)를 훔치기 모델로는 깔끔하게 못 덮고(노드 클릭 후 이미지가 사라지는 동작 변경 또는 레이스 잔존), `MainWindow.xaml.cs` 수정이 강제되며(요구사항 3의 현장 회피책 회귀 표면 확대), 핀 누적형 설계라 이 프로젝트의 과거 메모리 폭증(34~41GB) 이력에 비춰 감수할 수 없는 리스크였다. **후속 최적화 후보로 정직하게 기록.**
3. **탈락 — (b) 락 보호(`ShotConfig._imageLock` 미러링).** `ThreadPriority.Highest` 시퀀스 스레드가 UI 의 127MB 복사(수십 ms)를 락 대기하게 되어 택트 회귀를 직접 유발하는 하드 제약 위반이었고, 이 경로에 이미 `mDrawInterlock`/`_startLock` 두 개의 락이 있어 세 번째 락 추가가 실제 데드락 이력(주석에 기록됨)을 재현할 위험이 있었다.
4. **`ActionContext.ResultHalconImage` 는 범위 밖으로 명시적으로 제외.** writer(`Action_TopInspection.cs`/`Action_FAIMeasurement.cs`/`Action_BottomInspection.cs`)가 이번 작업의 수정 금지 파일에 있어, writer 를 비원자적으로 남긴 채 소비 지점만 고치면 use-after-dispose 가 그대로 성립 — "절반만 고치는 것"이 `RepeatRunService` 가 과거에 저지른 패턴의 반복이 되므로 손대지 않고 잔여 리스크로 문서화했다.

## 표시 신선도 트레이드오프

채택안에서 UI 는 그리기 직전에 획득을 시도한다. 다음 `$TEST` 가 매우 빨리 들어와 이미 `Clear()` 로 소유권이 교체된 뒤라면 `AcquireResultImage()` 가 null 을 반환하고 기존 디스크 폴백/오버레이 갱신 경로로 떨어진다 — **드물게 마지막 프레임이 다시 그려지지 않을 수 있다.**

수용 근거: (1) 현재는 그 경우 크래시하거나 오염된 픽셀을 그린다 — 어느 쪽이든 명백히 열등하다. (2) 사용자는 자동검사 중 실시간 표시를 통째로 끄는 설정(`DisableViewerDuringAutoInspect`, quick-260810-egx)을 이미 도입해 운용 중이다 — 자동검사 중 표시 신선도가 필수 요구가 아님을 스스로 확인해 준 셈이다. (3) 수동/반복검사 경로는 `RepeatRunService` 의 `DispatcherPriority.Background` 순서 보장(이번에도 유지) 덕에 영향이 사실상 없다.

## Task 2 하네스 출력 원문

```
[build-and-run] extracting SharedHImage from /c/code/DataMeasurement/WPF_Example/Utility/CaptureImageSaveService.cs
[build-and-run] compiling with csc.exe
[build-and-run] running harness
--- Phase A (1024x1024, small/fast, max iterations) duration=20s ---
  generations=118637 acquireSuccess=111221 acquireMiss=46116230 integrityFailures=0 exceptions=0 elapsed=20.0s
--- Phase B (4000x4000, large, wider copy window) duration=25s ---
  generations=17178 acquireSuccess=5853 acquireMiss=8909357 integrityFailures=0 exceptions=0 elapsed=25.0s

=== TOTAL ===
generations=135815 acquireSuccess=117074 acquireMiss=55025587 integrityFailures=0 exceptions=0
PASS
```

miss 수가 success 수보다 훨씬 많은 것은 정상이다(리더 스레드가 tight spin-loop 로 매우 자주 폴링하는 반면, 쓰기 스레드는 이미지 생성(`GenImageConst`+`ScaleImage`)에 상대적으로 더 오래 걸림) — miss 는 "이미지가 아직/이미 없음"의 정상 경로이지 실패가 아니다. 검증 대상은 획득 성공(0 이 아님, 실제로 레이스 창을 통과함)과 그 성공 구간에서의 무결성(실패 0)이다.

## Deviations from Plan

None - plan executed exactly as written.

**빌드 툴체인 관련 자잘한 시행착오 2건(스크립트 내부 조정, 플랜 지시사항 자체의 변경 아님):**
1. `HARNESS_DIR` 를 bash `pwd` 로 계산하면 이 실행 환경의 scratchpad 임시경로가 `/tmp/claude/...` 로 가상매핑되어 네이티브 `csc.exe` 가 파일을 못 찾음 — `pwd -W`(git-bash 의 실제 Windows 경로 조회)로 수정.
2. `csc.exe`(레거시 인자 파서)가 forward-slash 경로의 디렉터리 부분을 조용히 버리고 파일명만 인식하는 것을 실측 확인 — 컴파일러 호출 시 `cygpath -w` 로 backslash 경로로 변환해 전달하도록 수정. 실행 시점 `System.IO.FileNotFoundException`(halcondotnet 어셈블리 프로빙 실패, PATH 는 네이티브 DLL 탐색에만 영향)도 실측 확인 — exe 옆에 `halcondotnet.dll` 을 복사해 해결.

이 3가지는 전부 Task 2 의 저장소 밖 하네스 빌드 스크립트 내부 구현 디테일이며, 플랜이 요구한 검증 내용(sentinel 무결성, 두 페이즈, 50초 이내, PASS/FAIL 판정)에는 영향이 없다.

## Issues Encountered

None beyond the harness toolchain items documented above.

## User Setup Required

None - no external service configuration required.

## 잔여 리스크

**`ActionContext.ResultHalconImage` 이중 해제 창 (수정 금지 파일 제약).** `InspectionListView.ClearShotImageCache`(UI 정리 경로)와 `SequenceContext.Clear()` 안의 `act.Context.Clear()` 루프(MainRun 스레드) 둘 다 이 필드의 해제자다. 후자는 `StartCore` 에서 시퀀스 State 가 `Running` 으로 점유되기 **직전**에 도는데, 전자의 호출부(`PendingImageCleanupTimer_Tick`)가 보는 `EContextState.Running` 가드는 그 찰나의 창을 막지 못한다 — 이론상 이중 해제 창이 남아 있다. 근본 수정은 `ActionContext.ResultHalconImage` 에도 동일한 `SharedHImage` 원자적 소유권 모델을 적용하는 것이지만, writer(`Action_TopInspection.cs`, `Action_FAIMeasurement.cs`, `Action_BottomInspection.cs`)가 이번 작업의 수정 금지 파일에 있어 범위에서 제외했다. 다음 세션에서 이 파일들의 수정 금지가 해제되면 착수 가능.

**후속 최적화 후보 (이번엔 포기).** (a)안(소유권 이전)이 제거할 수 있었던 UI 측 127MB memcpy 1회/사이클은 이번 수정에서 포기했다 — 트리 클릭 독자를 못 덮고 `MainWindow.xaml.cs` 수정이 강제되며 핀 누적 리스크가 있었기 때문. 표시 신선도 요구가 낮음(`DisableViewerDuringAutoInspect` 상시 운용)이 재확인된 지금, 별도 작업으로 재검토 가능.

## 사용자 실기 UAT 항목 (이 플랜 범위 밖)

- 실기 자동 연속 반복 검사로 `AccessViolationException` 크래시 미재현 확인 (원래 재현 조건: Bottom "30개 항목 체크 + 일괄검사" 등 장시간 자동검사)
- 표시 동작 육안 확인 — 자동검사/수동검사/반복검사 각각에서 결과 이미지가 정상 표시되는지, 드문 "마지막 프레임 미표시"가 실사용에 문제되지 않는지
- `DisableViewerDuringAutoInspect=true` / `=false` 양쪽에서 동작 확인 (현장 회피책 경로 보존 검증)
- `InspectionListView` 트리 노드를 자동검사 진행 중에 클릭해 크래시가 재현되지 않는지 확인 (R2, 이번 조사에서 새로 발견된 경로)

## Next Phase Readiness

이번 수정은 독립적인 quick task 로, 다음 단계에 대한 블로커는 없다. `ActionContext.ResultHalconImage` 잔여 리스크(위 문서화됨)는 `Action_TopInspection.cs`/`Action_FAIMeasurement.cs`/`Action_BottomInspection.cs` 수정 금지가 해제되는 시점에 별도 작업으로 착수 권장.

---
*Phase: quick-260811-odo*
*Completed: 2026-08-11*

## Self-Check: PASSED

All 6 repository files + SUMMARY.md confirmed present on disk. Commits `8c06ce0` (Task 1) and `27cc255` (Task 3) confirmed present in git log. Scratchpad harness files (`RefRaceHarness.cs`, `build-and-run.sh`) confirmed present.
