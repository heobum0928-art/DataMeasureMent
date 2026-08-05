---
phase: 69-poc-run
plan: 01
subsystem: ui
tags: [sequence-handler, run-gate, camera-sharing, wpf, halcon]

# Dependency graph
requires: []
provides:
  - "SequenceHandler.TryGetBlockingSequence(ESequence, out string) — 시퀀스 단위 RUN 차단 판정 API"
  - "InspectionListView RUN 진입점 4곳(Btn_start_Click/ResolveRunnableAction/Btn_batchRun_Click/batch rebuild)의 시퀀스 단위 게이트 교체"
affects: [69-02, poc-run]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "fail-closed 카메라 공유 판정: DeviceHandler[name] 이 돌려주는 VirtualCamera 객체의 ReferenceEquals 로만 물리 카메라 공유 여부를 판정, 해석 실패(미등록/공백 이름/DeviceHandler null)는 항상 '공유함(차단)'으로 수렴"

key-files:
  created: []
  modified:
    - "WPF_Example/Custom/Sequence/SequenceHandler.cs"
    - "WPF_Example/UI/ControlItem/InspectionListView.xaml.cs"

key-decisions:
  - "SequenceHandler._StateSeqName 재사용 안 함 — 전역 StateAll getter 의 부수효과 필드라 시퀀스 단위 판정에서 엉뚱한 이름을 돌려줄 수 있어, TryGetBlockingSequence 가 원인 이름을 직접 계산/반환"
  - "물리 카메라 공유 판정은 역할 이름 문자열 매칭이 아니라 DeviceHandler 인덱서가 돌려주는 객체 참조 동일성(ReferenceEquals)으로만 수행 — 디바이스 등록 구조 변경에도 안전"
  - "UI grab/조명 경로(button_grab_Click 등 4곳)는 시퀀스 단위로 완화하지 않고 전역 IsIdle 유지 — RUN 경로가 아니라 UI 직접 카메라 점유 경로이므로 완화 시 검사 grab 과 충돌 위험"

requirements-completed: [MAINT-POC-01]

duration: 14min
completed: 2026-08-05
---

# Phase 69 Plan 01: 시퀀스 단위 RUN 차단 판정 API + UI 게이트 교체 Summary

**SequenceHandler.TryGetBlockingSequence(ESequence, out string) 신설로 RUN 게이트를 전역 IsIdle에서 "자기 자신 + 물리 카메라를 실제로 공유하는 시퀀스"로 좁히고, InspectionListView RUN 진입점 4곳을 이 API/GetSequenceState 로 교체.**

## Performance

- **Duration:** 약 14분 (커밋 3982da5 → ca88862 기준, plan 파일 커밋 8b7343e 제외)
- **Started:** 2026-08-05T15:17 (plan 커밋 직후 실행 시작)
- **Completed:** 2026-08-05T15:31:36+09:00
- **Tasks:** 2 (모두 auto)
- **Files modified:** 2

## Accomplishments
- `SequenceHandler`(Custom partial)에 `TryGetBlockingSequence` / `FindBlockingSequenceName` / `SharesCameraDevice` / `TryCollectSequenceCameras` 4개 멤버 추가 — 순수 추가, 기존 코드 0줄 수정
- `InspectionListView.xaml.cs` RUN 진입점 4곳(단일 RUN, rebuild 게이트, 일괄검사 RUN, batch rebuild 게이트)을 시퀀스 단위 판정으로 교체, 차단 메시지 2곳 모두 원인 시퀀스 이름 포함
- 차단 발생 시 `[RUN-GATE] blocked: target=..., busy=...` Trace 로그 1줄 기록

## Task Commits

Each task was committed atomically:

1. **Task 1: SequenceHandler 에 시퀀스 단위 RUN 차단 판정 API 추가 (D-01)** - `3982da5` (feat)
2. **Task 2: RUN 진입점 4곳을 시퀀스 단위 판정으로 교체 + 차단 사유 메시지 (D-01/D-03)** - `ca88862` (feat)

_Note: 이 plan 은 TDD 가 아니며, 두 태스크 모두 msbuild 컴파일 검증으로 done 조건을 확인했다._

## 추가된 4개 멤버 최종 시그니처

```csharp
// WPF_Example/Custom/Sequence/SequenceHandler.cs (public sealed partial class SequenceHandler)
public bool TryGetBlockingSequence(ESequence eTargetSeqId, out string sBlockingSeqName);
private string FindBlockingSequenceName(ESequence eTargetSeqId);
private bool SharesCameraDevice(SequenceBase seqA, SequenceBase seqB);
private bool TryCollectSequenceCameras(SequenceBase seq, List<VirtualCamera> listOut);
```

`TryGetBlockingSequence`: 대상 시퀀스가 지금 RUN 가능한지 판정. 차단이면 `true` + `sBlockingSeqName` 에 원인 시퀀스 이름, 실행 가능이면 `false` + `sBlockingSeqName = null`. 차단 시 `Logging.PrintLog((int)ELogType.Trace, "[RUN-GATE] blocked: target={0}, busy={1}", ...)` 1줄 기록.

판정 순서(`FindBlockingSequenceName`):
1. 대상 시퀀스가 이 PC(CameraRole)에 미등록이면 즉시 차단(자기 이름 반환)
2. 자기 자신이 `Idle` 이 아니면 차단(기존 동작 그대로 유지 — Finish/Error 도 non-Idle)
3. 등록된 다른 시퀀스 중 non-Idle 이면서 `SharesCameraDevice` 로 물리 카메라를 실제로 공유하는 시퀀스가 있으면 그 시퀀스 이름으로 차단
4. 위 셋 다 아니면 `null`(실행 가능)

## Fail-closed 로 처리되는 정확한 조건 목록

`TryCollectSequenceCameras` 가 `false`(해석 실패)를 반환 → 호출부 `SharesCameraDevice` 가 무조건 `true`(공유로 간주, 차단)로 처리하는 조건:
1. `seq == null`
2. `SystemHandler.Handle.Devices == null`
3. 마스터 파라미터(`CameraMasterParam.DeviceName`) + 각 Action 의 `ICameraParam.DeviceName` 을 모두 모은 이름 목록 중, 빈 문자열이 아닌 이름이 `DeviceHandler[sName]` 에서 `null`(미등록 디바이스)로 돌아오는 경우
4. 해석 가능한 카메라 객체를 하나도 모으지 못한 경우(`listOut.Count == 0`) — 판정 근거 자체가 없으므로 해석 실패로 처리

`SharesCameraDevice` 자체에서 `TryCollectSequenceCameras(seqA, ...) == false` 또는 `TryCollectSequenceCameras(seqB, ...) == false` 인 두 지점 모두 `return true;` — 두 곳 모두 존재함을 `rg` 로 확인 완료(자동 검증 통과).

## 범위 밖으로 남긴 IsIdle 4곳

`InspectionListView.xaml.cs` 현재 줄 번호(Task 2 편집 후 기준, +17줄 시프트):

| 줄 (편집 후 실측) | 위치 | 남긴 이유 |
|---|---|---|
| 980 | `button_grab_Click` — 라이브 grab (1) | RUN 경로가 아니라 UI 가 직접 카메라를 점유하는 경로. 시퀀스 단위로 완화하면 검사 grab 과 UI grab 이 같은 카메라를 동시에 건드릴 수 있어 보수적으로 전역 게이트 유지 |
| 985 | `button_grab_Click` — 라이브 grab (2) | 상동 |
| 1000 | `button_grabInsp_Click` — 검사이미지 grab | 상동 |
| 1155 | `button_light_Click` — 조명 제어 | 상동 (조명도 카메라와 마찬가지로 UI 직접 점유 자원) |

`rg -n "button_grab_Click|button_grabInsp_Click|button_light_Click" -A 12 ... | rg -c "Sequences\.IsIdle"` → `4` 로 4곳 원형 보존을 자동 검증했다. 정확한 줄 번호는 plan 작성 시점(원본 파일 963/968/983/1138) 대비 이 plan 이 삽입한 신규 라인만큼 뒤로 밀렸을 뿐, 코드 자체는 한 글자도 변경하지 않았다.

## Files Created/Modified
- `WPF_Example/Custom/Sequence/SequenceHandler.cs` - `IsSequenceActive` 바로 아래 4개 멤버(`TryGetBlockingSequence`/`FindBlockingSequenceName`/`SharesCameraDevice`/`TryCollectSequenceCameras`) 순수 추가
- `WPF_Example/UI/ControlItem/InspectionListView.xaml.cs` - RUN 진입점 4곳을 시퀀스 단위 판정으로 교체 (`Btn_start_Click`, `ResolveRunnableAction` 내부 rebuild 게이트, `Btn_batchRun_Click`, batch rebuild 게이트)

## Decisions Made
- `SequenceHandler._StateSeqName`/`StateSequenceName`/`StateAll`/`IsIdle` 은 전혀 수정하지 않음 — 새 API 는 순수 추가로만 구현해 TCP/MainWindow/MainView 등 기존 전역 게이트 호출부의 회귀를 0으로 유지
- 카메라 공유 판정은 역할 이름("TOP"/"BOTTOM" 등) 매칭이 아니라 `DeviceHandler[name]` 이 돌려주는 `VirtualCamera` 객체의 `ReferenceEquals` 로만 수행 — SIMUL_MODE(독립 인스턴스)와 실HW TopBottom(`sharedMil` 공유 인스턴스) 양쪽 모두 구조적으로 정확히 포착
- 마스터 파라미터뿐 아니라 각 Action 의 `ICameraParam.DeviceName` 도 모두 수집 — 자식 Action 이 마스터와 다른 카메라를 쓰는 경우도 놓치지 않기 위함(SequenceBase.cs 상속은 빈 값일 때만 발생)

## Deviations from Plan

None - plan 을 그대로 실행했다. 4개 신규 멤버, 4곳 UI 게이트 교체 모두 plan 의 `<action>` 코드 블록을 그대로 삽입했으며, read_first 로 지정된 파일들의 실제 내용(줄 번호, 시그니처)이 plan 서술과 완전히 일치함을 편집 전 확인했다.

## Issues Encountered
- msbuild 빌드 시 `bin\x64\Debug\DatumMeasurement.exe` 복사 단계에서 실행 중인 프로세스(Visual Studio Insiders 디버깅 세션, PID 28076)에 의한 파일 잠금으로 `MSB3027`/`MSB3021` 에러 발생. 프로세스를 종료하지 않고(안전 규칙 준수) `-p:OutDir=`/`-p:BaseIntermediateOutputPath=` 로 별도 출력 경로를 지정한 컴파일 전용 검증으로 대체 — 두 태스크 모두 0 CS 에러(사전 존재하던 CS0618/CS0162 경고만 잔존, 이 plan 무관)로 확인 완료. 검증에 사용한 `_verify_out`/`_verify_obj` 임시 디렉터리는 커밋 전 삭제했다.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness
- Task 1/2 모두 완료, 두 파일 모두 msbuild Debug/x64 컴파일 PASS(대체 OutDir 검증)
- **실HW(TopBottom, MIL 공유 카메라) 상호배타 동작은 이 환경(SIMUL_MODE)에서 검증 불가** — SIMUL 은 역할별 독립 VirtualCamera 인스턴스를 생성하므로 `SharesCameraDevice` 가 항상 false 를 반환하는 경로만 실기 확인 가능하다. 실HW 공유 경로(`ReferenceEquals` 가 true 가 되는 경로) 검증은 69-02 에서 기록만 하고, 실제 HW UAT 는 별도 추적 필요
- 69-02 는 이 plan 이 남긴 범위 밖 4곳(UI 직접 grab/조명)과 D-02(POC 패널 유지)를 그대로 전제로 진행 가능

---
*Phase: 69-poc-run*
*Completed: 2026-08-05*

## Self-Check: PASSED

- FOUND: WPF_Example/Custom/Sequence/SequenceHandler.cs
- FOUND: WPF_Example/UI/ControlItem/InspectionListView.xaml.cs
- FOUND: .planning/phases/69-poc-run/69-01-SUMMARY.md
- FOUND commit: 3982da5
- FOUND commit: ca88862
