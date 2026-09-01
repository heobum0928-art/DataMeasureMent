---
phase: quick-260901-k7a
plan: 01
subsystem: device/camera + inspection-tree UI
tags: [mil-camera, live-preview, mirror, inspection-tree, mvvm]
dependency-graph:
  requires:
    - quick-260813-jnh (단발 grab 역할 시스템: DeviceHandler.BuildGrabRoleIdentifier, ResolveRoleInfo)
  provides:
    - VirtualCamera.SetLiveGrabRole (가상 no-op 진입점)
    - MilCamera 라이브 역할(_liveRoleInfo) 반영
    - DeviceHandler.ApplyLiveGrabRole 파사드
    - InspectionListViewModel.ApplyLiveMirror / ResolveShotLiveMirror (미러 계산 + 장치 적용)
    - InspectionListView.ApplyLiveMirorForNode (트리 선택 → VM 호출 wiring)
  affects:
    - MIL(CXP) 라이브 미리보기 방향
tech-stack:
  added: []
  patterns:
    - "가상 메서드 no-op 기본 구현 + 단일 서브클래스(MilCamera)만 override — Hik/Basler 회귀 0"
    - "락 없는 참조 스왑(_liveRoleInfo) — UI 스레드 쓰기, 백그라운드 스레드 읽기, 최악 한 프레임 지연 허용"
    - "MVVM: 미러 계산/장치 적용 로직은 InspectionListViewModel, code-behind는 UI 상태 의존 조회(ResolveDatumCameraParam)와 VM 호출 wiring만"
key-files:
  created: []
  modified:
    - WPF_Example/Device/Camera/VirtualCamera.cs
    - WPF_Example/Device/Camera/Mil/MilCamera.cs
    - WPF_Example/Device/DeviceHandler.cs
    - WPF_Example/UI/ControlItem/InspectionListView.xaml.cs
    - WPF_Example/UI/ControlItem/InspectionListViewModel.cs
decisions:
  - "Datum 노드의 DeviceName 해석에 기존 인스턴스 메서드 ResolveDatumCameraParam(code-behind, 트리 선택 상태 의존)을 재사용 — 계획서가 가정한 'DatumConfig가 ICameraParam 계열'은 사실이 아니었음(Rule 1 버그 수정)"
  - "미러 계산(ResolveShotLiveMirror)과 장치 적용(ApplyLiveMirror)은 InspectionListViewModel로 이동 — CLAUDE.md MVVM 규정 재확인에 따라 code-behind에 새 비즈니스 로직을 두지 않음"
metrics:
  duration: "~60분"
  completed: "2026-09-01"
---

# Phase quick-260901-k7a Plan 01: MIL 라이브 미리보기 미러 연동 Summary

MIL(CXP) 라이브 미리보기가 검사 트리에서 선택된 Datum/Shot 노드의 MirrorX/MirrorY 설정을 자동으로 따라가도록, MilCamera에 락 없는 라이브 역할 참조(`_liveRoleInfo`)를 추가하고 트리 선택 시 InspectionListViewModel이 미러를 계산해 적용하도록 연동했다.

## What Was Built

**Task 1 — MilCamera 라이브 grab 역할 지정 진입점**
- `VirtualCamera.SetLiveGrabRole(string)` 가상 no-op 메서드 추가. Hik/Basler는 override하지 않아 완전히 무영향.
- `MilCamera`에 `_liveRoleInfo` 필드 추가(`#if !SIMUL_MODE` 밖, 공통 필드). `SetLiveGrabRole` override가 기존 `ResolveRoleInfo(requestIdentifier)`를 재사용해 이 필드를 갱신한다. 빈 문자열/null이면 `null`로 되돌려 무미러 기본 복귀.
- `LiveLoop`이 루프 1회당 `_liveRoleInfo`를 한 번만 읽어(`liveInfo`), null이면 기존 `Info`로 폴백 — 미지정 시 회귀 0.
- `StopStream()` 끝에서 `_liveRoleInfo = null`로 초기화해 라이브 재시작 시 이전 선택이 남지 않게 함.
- `DeviceHandler.ApplyLiveGrabRole(szDeviceName, szRoleIdentifier)` 파사드 추가 — 이름으로 카메라 조회 후 위 메서드 호출. 조회 실패 시 조용히 return.

**Task 2 — 트리 선택 → 라이브 미러 역할 연동 (MVVM)**
- `InspectionListViewModel`에 `_szLiveRoleDeviceName`(직전 라이브 역할 지정 장치), `ResolveShotLiveMirror(shotCfg, out szDeviceName, out bMirrorX, out bMirrorY)`(순수 로직, `InspectionSequence.ResolveShotGrabMirror` 재사용), `ApplyLiveMirror(szDeviceName, bMirrorX, bMirrorY)`(역할 문자열 조립 + 직전 장치 무미러 복귀 + `DeviceHandler.ApplyLiveGrabRole` 호출)를 추가.
- `InspectionListView.xaml.cs`의 `ApplyLiveMirrorForNode(itemParam)`는 배선만 담당: DatumConfig면 기존 `ResolveDatumCameraParam`(단발 grab 버튼과 동일 선례, 트리 선택 상태에 의존해 code-behind에 남겨둠)으로 소유 Shot의 DeviceName만 얻어 `ViewModel.ApplyLiveMirror`를 호출하고, ShotConfig면 `ViewModel.ResolveShotLiveMirror` → `ViewModel.ApplyLiveMirror`를 그대로 호출한다. 그 외 노드는 `ViewModel.ApplyLiveMirror(null, false, false)`로 무미러 복귀.
- `InspectionList_SelectionChanged`의 Dispatcher(Background) 블록, `object itemParam = item.Param;` 바로 다음 줄에서 `ApplyLiveMirrorForNode(itemParam)` 1줄 호출. 전체를 try/catch로 감싸 라이브 표시 실패가 트리 선택 처리 자체를 깨뜨리지 않도록 함.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] DatumConfig가 ICameraParam을 구현하지 않아 계획대로 구현하면 Datum 노드 라이브 미러가 항상 무동작**
- **Found during:** Task 2 구현 중 (인터페이스 검증)
- **Issue:** 계획서는 "DatumConfig는 ICameraParam 계열이므로 DeviceName을 얻는다"고 가정했으나, 실제 `DatumConfig : ParamBase, ICustomTypeDescriptor, IOfflineImageParam`이며 `ICameraParam`을 구현하지 않는다. 트리 구조상 Datum 노드는 Sequence의 직접 자식(특정 Shot에 종속되지 않음)이라 DeviceName을 직접 가지지 않는다. 계획서의 폴백(`itemParam as ICameraParam`)도 동일 객체를 다시 캐스팅하는 것이라 항상 null이 되어, Datum 노드를 선택해도 라이브가 절대 미러되지 않는 결과(핵심 must_have 위반)가 나왔을 것이다.
- **Fix:** 이 파일에 이미 존재하는 `ResolveDatumCameraParam(DatumConfig)` (단발 grab/검사Grab/Load 버튼이 쓰는 선례 — `SourceShotName` 우선, 없으면 소유 시퀀스의 첫 Shot으로 폴백)을 재사용해 Datum의 소유 Shot을 찾고 그 Shot의 `DeviceName`을 사용하도록 구현.
- **Files modified:** WPF_Example/UI/ControlItem/InspectionListView.xaml.cs
- **Commit:** 74dd2348

**2. [Rule 2 - MVVM 하드룰 준수] 미러 계산/장치 적용 로직을 InspectionListViewModel로 이동**
- **Found during:** 코디네이터가 CLAUDE.md MVVM 규정(code-behind = 배선만, 새 로직은 ViewModel)을 재확인 요청
- **Issue:** 최초 구현(commit 74dd2348)은 `ResolveLiveRoleIdentifier`/`ApplyLiveMirrorForNode`를 `InspectionListView.xaml.cs`(code-behind)에 전부 두었다. 이 View에는 이미 `InspectionListViewModel`이 존재하므로 새 비즈니스 로직(미러 계산, 역할 문자열 조립, 직전 장치 무미러 복귀, 장치 호출)을 code-behind에 두는 것은 CLAUDE.md 하드룰 위반이다.
- **Fix:** `ResolveShotLiveMirror`, `ApplyLiveMirror`, `_szLiveRoleDeviceName`을 `InspectionListViewModel`로 이동. code-behind의 `ApplyLiveMirrorForNode`는 Datum 노드 전용으로 UI 상태(`treeListBox_sequence.SelectedItem`)에 의존하는 기존 `ResolveDatumCameraParam` 호출과, 노드 타입별로 어떤 VM 메서드를 호출할지 분기하는 wiring만 남겼다. `ResolveDatumCameraParam` 자체는 이번 작업 범위 밖(기존 code-behind 리팩토링 금지 규정)이라 그대로 두었다.
- **Files modified:** WPF_Example/UI/ControlItem/InspectionListView.xaml.cs, WPF_Example/UI/ControlItem/InspectionListViewModel.cs
- **Commit:** 36f8e256

## Auth Gates

None.

## Known Stubs

None. `_liveRoleInfo`/`ApplyLiveGrabRole`/`ApplyLiveMirror`/`ApplyLiveMirrorForNode` 전부 실제 로직으로 연결되어 있으며, 미지정 시 기존 무미러 동작(Info/null)으로 안전하게 폴백한다.

## Threat Flags

None. 새 네트워크/인증 경로 없음. 기존 grab 역할 조회(`ResolveRoleInfo`)를 재사용하는 순수 UI 편의 기능이며, 실패 시 조용히 무미러로 폴백한다(트리 선택을 막지 않음).

## Automated Verification (Tasks 1-2)

- 가독성 하드룰 grep(삼항/`??`/`?.`/switch식/신규 `hbk` 주석) — 수정한 5개 파일 모두 신규 라인 기준 0건. 기존 라인의 매치(MilCamera.cs 169-170 M_REVERSE 삼항, DeviceHandler.cs mojibake 주석 `??`, InspectionListView.xaml.cs 기존 삼항 4건 + `siblingShot?.CopyTo` 1건 + 기존 `hbk` 주석 28건)는 전부 이번 작업 이전부터 있던 것으로 diff 미포함.
- `grep SetLiveGrabRole|_liveRoleInfo|ApplyLiveGrabRole` — VirtualCamera/MilCamera/DeviceHandler 3개 파일에 모두 존재 확인.
- `grep ApplyLiveMirrorForNode|ApplyLiveMirror|ResolveShotLiveMirror|_szLiveRoleDeviceName` — InspectionListView.xaml.cs(wiring)와 InspectionListViewModel.cs(로직)에 각각 존재 확인.
- `git status --short` — 금지 파일(HikCamera.cs/BaslerCamera.cs/MainView.xaml.cs/DeviceSelector.xaml.cs/Action_FAIMeasurement.cs) 변경 없음. `DatumMeasurement.csproj`는 사용자의 미커밋 실험 그대로이며 이번 작업에서 스테이징/커밋하지 않음.
- Debug|x64 MSBuild 빌드(리팩토링 후 재검증 포함): `error CS` 0건 (경고만 존재, 전부 기존 Phase 33 마이그레이션 관련 obsolete 경고로 이번 변경과 무관).

## Manual Verification Required (Checkpoint — not performed by executor)

Task 3(체크포인트)은 실 카메라 하드웨어 라이브 관찰이 필요해 자동화 실행자가 수행할 수 없습니다. 사용자가 아래를 직접 확인해야 합니다:

1. 프로그램 실행 → 장치(카메라) 창 열어 CXP 라이브 영상 켜기.
2. 검사 목록 트리에서 MirrorX가 켜진 Datum 항목 클릭 → 라이브가 좌우로 뒤집혀 보여야 함.
3. 그 Datum을 쓰는 Shot(촬영) 항목 클릭 → Datum과 같은 방향으로 보여야 함.
4. 시퀀스 이름 등 미러와 무관한 상위 항목 클릭 → 라이브가 원래(무미러) 영상으로 복귀해야 함.
5. 같은 항목에서 'Grab' 버튼으로 한 장 촬영 → 찍힌 사진 방향과 라이브 화면 방향이 일치해야 함.
6. 트리를 건드리지 않고 장치 창만 켠 상태의 라이브는 예전과 동일하게 무미러 영상이어야 함.

이 체크리스트가 통과할 때까지 플랜은 "완전 완료"로 보지 않으며, 사용자 확인 결과를 기다립니다.

## Self-Check: PASSED

- FOUND: WPF_Example/Device/Camera/VirtualCamera.cs (SetLiveGrabRole 존재)
- FOUND: WPF_Example/Device/Camera/Mil/MilCamera.cs (_liveRoleInfo/SetLiveGrabRole/LiveLoop 반영 존재)
- FOUND: WPF_Example/Device/DeviceHandler.cs (ApplyLiveGrabRole 존재)
- FOUND: WPF_Example/UI/ControlItem/InspectionListView.xaml.cs (ApplyLiveMirrorForNode wiring 존재)
- FOUND: WPF_Example/UI/ControlItem/InspectionListViewModel.cs (ApplyLiveMirror/ResolveShotLiveMirror 존재)
- FOUND commit 9f00d567 (Task 1)
- FOUND commit 74dd2348 (Task 2 최초 구현)
- FOUND commit 36f8e256 (Task 2 MVVM 리팩토링)
- Build: error CS 0건 확인됨(리팩토링 후 재빌드 포함)
