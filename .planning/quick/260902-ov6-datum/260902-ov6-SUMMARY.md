---
phase: quick-260902-ov6
plan: 01
status: complete
subsystem: device/camera + inspection-tree UI
tags: [mil-camera, live-preview, mirror, inspection-tree, mvvm, wording-fix]
dependency-graph:
  requires:
    - quick-260901-k7a (MIL 라이브 미리보기 미러 연동 최초 구현: _liveRoleInfo, ApplyLiveMirror, ApplyLiveMirrorForNode)
    - quick-260813-jnh (단발 grab 역할 시스템: DeviceHandler.BuildGrabRoleIdentifier, ResolveRoleInfo, BuildMirrorRoleInfos)
  provides:
    - MilCamera._szLiveRoleIdentifier (라이브 정지/재시작을 넘어 보존되는 "원하는 역할" 식별자)
    - InspectionListView.OnLiveMirrorDatumPropertyChanged (선택 중인 Datum 의 MirrorX/Y 변경 즉시 재적용)
  affects:
    - MIL(CXP) 라이브 미리보기 방향(정지/재시작 후 유지, PropertyGrid 변경 즉시 반영)
    - DatumConfig MirrorX/MirrorY 사용자 안내 문구
tech-stack:
  added: []
  patterns:
    - "'원하는 역할' 과 '유효 역할' 을 별도 필드로 분리 — 장치 계층 내부에서 계층 역전 없이 상태 보존"
    - "단일 choke point(ApplyLiveMirrorForNode)에서 PropertyChanged 구독 swap — 해제 누락 구조적 차단"
    - "PropertyName 화이트리스트 + stale 참조 방어로 재진입/오발동 차단"
key-files:
  created: []
  modified:
    - WPF_Example/Device/Camera/Mil/MilCamera.cs
    - WPF_Example/UI/ControlItem/InspectionListView.xaml.cs
    - WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs
decisions:
  - "UI 배선(DeviceSelector.xaml.cs 는 금지 파일) 대신 MilCamera 내부에서 '원하는 역할'과 '유효 역할' 을 분리해 QF-OV6-01 해결 — 계층 역전 없이 정지/재시작 보존"
  - "ShotConfig 는 Mirror 속성 자체가 없음(파생만 함)을 확인하고 구독 대상에서 명시적으로 제외"
  - "미러 안내 문구 '재시작 필요' 주장은 코드로 검증(BuildMirrorRoleInfos 정적 4조합 등록 + Action_FAIMeasurement grab 시점 직독) 후 사실이 아님을 확인하고 정정 — 안전 경고 1·2번은 원문 유지"
  - "라이브 창 비모달 전환은 이번 범위 밖 — 사용자가 (A) 이 플랜만 실행을 선택함 (아래 followup 참조)"
metrics:
  duration: "~35분"
  completed: "2026-09-02"
---

# Phase quick-260902-ov6 Plan 01: MIL 라이브 미러 결함 2건 수정 + 안내 문구 정정 Summary

라이브 미러가 Datum 선택/미러값 변경을 따라오지 않던 결함 2건(정지→재시작 시 풀림, 선택 중 미러값 변경 시 재적용 없음)을 수정하고, 사실과 어긋난 "재시작 필요" 안내 문구를 정정했다. 사용자 결정에 따라 라이브 창의 모달 구조는 그대로 유지했다.

## What Was Built

**Task 1 — MilCamera 라이브 역할 식별자를 정지/재시작 사이에 보존 (QF-OV6-01)**
- `_szLiveRoleIdentifier` 필드 추가(`_liveRoleInfo` 옆, `#if !SIMUL_MODE` 밖) — "트리가 마지막으로 지정한 원하는 역할"이며 라이브 정지/재시작을 넘어 보존된다.
- `SetLiveGrabRole`: 빈 값 분기에서 `_szLiveRoleIdentifier` 와 `_liveRoleInfo` 를 둘 다 되돌림(무미러 복귀 시 불변식 유지). 정상 분기는 필드에 먼저 저장한 뒤 필드를 경유해 `ResolveRoleInfo` 호출.
- `StartStream()`: 이른 return 가드 뒤, `_liveThread.Start()` 앞에서 `_szLiveRoleIdentifier` 가 비어 있지 않으면 `ResolveRoleInfo` 로 `_liveRoleInfo` 를 다시 채운다 — 정지 시 비웠던 유효 역할을 재시작 시 복원.
- `StopStream()`: 유효 역할(`_liveRoleInfo`)만 비우고, 식별자는 그대로 남겨 둔다(주석 갱신). `LiveLoop`/`RegisterRoleInfo`/`ResolveRoleInfo`/단발 grab 경로는 무수정.

**Task 2 — 선택 중인 Datum 의 MirrorX/Y 변경 시 라이브 즉시 재적용 (QF-OV6-02)**
- `_liveMirrorWatchedDatum` 필드 추가.
- `ApplyLiveMirrorForNode` 최상단(기존 try 블록 첫 부분)에 구독 swap 삽입 — 이 메서드가 선택 변경마다 반드시 지나가는 유일한 지점이므로 여기서 해제하면 누수가 구조적으로 불가능. `ReferenceEquals` 로 같은 참조면 swap 을 건너뛴다(핸들러가 이 메서드를 재호출하므로 매번 흔들지 않기 위함).
- `OnLiveMirrorDatumPropertyChanged` 추가: PropertyName 화이트리스트(`MirrorX`/`MirrorY` 만, 빈 문자열 대량 갱신은 통과시키지 않음) + stale 방어(`treeListBox_sequence.SelectedItem` 의 `Param` 과 참조 일치할 때만 적용) 후 `ApplyLiveMirrorForNode` 재호출.
- `ShotConfig` 에는 Mirror 속성 자체가 없음을 코드로 확인(`ShotConfig.cs` grep 0건, `InspectionSequence.ResolveShotGrabMirror` 가 소속 `DatumConfig` 들에서 파생)하고 구독 대상에서 제외.
- `InspectionList_SelectionChanged` 의 기존 `ApplyLiveMirrorForNode(itemParam)` 호출, `ResolveDatumCameraParam` 은 무수정.

**Task 3 — 미러 안내 문구 정정 + 최종 빌드 게이트 (QF-OV6-03)**
- 문구 수정 전 "재시작 불필요" 주장을 코드로 검증: `DeviceHandler.Custom.BuildMirrorRoleInfos`/`BuildGrabRoleIdentifier` 가 앱 시작 시 미러 4조합을 레시피 스캔 없이 정적 등록하고(`RegisterRoleInfo` 로 `_roleInfoMap` 에 반영), `Action_FAIMeasurement.cs:978` 이 grab 시점에 `datum.MirrorX/Y` 를 직접 읽어 역할 식별자를 조립함을 확인 — 세 조건 모두 성립.
- `MirrorX`/`MirrorY` 의 `[Description]` 및 `WarnMirrorChanged` 3번 항목의 "프로그램을 다시 실행해야 반영됩니다" 를 "다음 촬영부터 바로 적용됩니다" 취지로 교체. 안전 경고 1번(촬영 방향 자체가 바뀜)·2번(다른 항목까지 영향)은 원문 그대로 유지.
- `MirrorX`/`MirrorY` setter 로직, `_suppressMirrorWarning` 게이트, `RaisePropertyChanged` 호출, `CustomMessageBox.Show` 인자 구성은 무수정(Task 2 의 재적용 트리거가 이 발화에 의존).

## Deviations from Plan

### Auto-fixed Issues

None — 플랜 그대로 실행됨.

### Gate Script Imprecision (기록만, 코드 무수정)

**1. Task 1 `IDENT_CLEARS` 게이트 수치 불일치(2 vs 기대 1)**
- **발견 시점:** Task 1 verify 실행 중
- **내용:** `grep -c '_szLiveRoleIdentifier = null'` 이 필드 선언 초기화(`private string _szLiveRoleIdentifier = null;`)와 `SetLiveGrabRole` 의 실제 런타임 클리어를 둘 다 매치해 2가 나왔다. 플랜의 "정확히 1" 기대치는 이 두 매치를 구분하지 못한 grep 패턴의 부정확성이며, 시맨틱 불변식(식별자를 런타임에 지우는 곳은 `SetLiveGrabRole` 빈 값 분기 한 곳뿐, `StopStream` 은 `_liveRoleInfo` 만 지움)은 실제로 성립한다.
- **처리:** 숫자를 맞추려고 필드 초기화(`= null`)를 제거하지 않았다(하드룰 "숫자 맞추려 기존 코드 삭제 금지" 정신 적용, 형제 필드 `_liveRoleInfo` 도 동일하게 `= null` 명시). 코드 검토(`grep -n`)로 두 매치가 선언 1 + 런타임 클리어 1 임을 직접 확인함.
- **Files modified:** 없음(검증 방식만 조정)
- **Commit:** 해당 없음(gate 재해석)

## Auth Gates

None.

## Known Stubs

None. 모든 필드/핸들러가 실제 로직으로 연결되어 있으며, 미지정/미구독 시 기존 무미러 동작으로 안전하게 폴백한다.

## Threat Flags

None. 이번 변경은 프로세스 내부 UI ↔ 장치 계층 필드 갱신과 사용자 안내 문자열뿐이며, 신규 네트워크/인증/파일 파싱 경계를 만들지 않는다. 플랜의 STRIDE 등록(T-ov6-01~04)에 따른 완화(참조 대입 원자성, 화이트리스트, stale 방어, 단일 choke point 구독 swap)를 구현에 그대로 반영했다.

## Automated Verification (Tasks 1-3)

- 가독성 하드룰 grep(삼항/`??`/`?.`/switch식/신규 `hbk` 주석) — 3개 수정 파일 모두 **diff 로 추가된 라인만** 대상, 전부 0건.
- Task 1: `IDENT_USES=7`, `IDENT_CLEARS=2`(성분 분석 결과 선언 1 + 런타임 클리어 1, 위 "Gate Script Imprecision" 참조), `INFO_CLEARS=3` — 기대치 충족.
- Task 2: `SUBSCRIBE=1`, `UNSUBSCRIBE=1`, `WHITELIST=2` — 기대치 정확히 일치.
- Task 3: `RESTART_CLAIM=0`, `RAISE_INTACT=2`, `WARN_INTACT=3` — 기대치 정확히 일치.
- 금지 파일(`DeviceSelector.xaml.cs`/`MainWindow.xaml.cs`/`MainView.xaml.cs`/`Action_FAIMeasurement.cs`/`HikCamera.cs`/`BaslerCamera.cs`) 무변경 확인(각 task 후 3회 재확인).
- `DatumMeasurement.csproj`: `git status --short` 에는 사용자의 미커밋 실HW 세팅이 계속 남아 있고(1), `git diff --cached --name-only` 에는 한 번도 나타나지 않음(0) — 세 커밋 모두 확인.
- Debug|x64 MSBuild 빌드(각 task 후 재검증): `error CS` 0건.

## Human Verification Required (실기 UAT — 이 작업 범위 밖)

**현재 UI 구조에서 검증 가능한 항목:**
- U-1 (QF-OV6-01): Datum A 를 선택 → 카메라 창을 열어 라이브 확인(미러 적용됨) → 창을 닫고 **트리를 건드리지 않은 채** 다시 연다 → 미러가 그대로 유지되어야 한다. (수정 전에는 무미러로 풀렸다)
- U-2 (QF-OV6-02): Datum A 선택 상태에서 PropertyGrid 로 MirrorX 를 토글 → 카메라 창을 연다 → **토글한 새 방향**으로 보여야 한다. (수정 전에는 무미러였다)
- U-3: 미러 미설정 Datum 또는 시퀀스 이름 같은 기타 노드를 선택 → 카메라 창을 연다 → 무미러 원본 방향으로 보여야 한다(회귀 없음)
- U-4: 같은 Datum 에서 Grab 버튼으로 한 장 촬영 → 촬영된 사진 방향과 라이브 화면 방향이 일치해야 한다(단발 grab 경로 회귀 없음)

**현재 UI 구조에서 검증 불가능한 항목(모달 제약 — blocking_discovery 이월):**
- 라이브 ON 상태에서 Datum A → B 로 트리 선택을 바꾸며 즉시 전환을 보는 것
- 라이브 ON 상태에서 MirrorX 체크를 토글하며 즉시 반영을 보는 것
- 이유: MIL 라이브 영상의 유일한 표시 경로는 `DeviceSelector` 창(`GuiReadyForDisplay`/`StartStream()` 호출부가 이 파일 단독)이며, `MainWindow.PopupView` 의 `EPageType.Camera` 분기가 이 창을 `ShowDialog()` 모달 + `WindowState="Maximized"` 로 연다. 라이브가 켜져 있는 동안 메인 창의 검사 트리와 PropertyGrid 는 입력이 차단되고 화면도 가려진다. 선행 작업 quick-260901-k7a 의 UAT 체크리스트 2~4번도 같은 이유로 수행 불가능한 항목이었다.

## Followup Decision (사용자 결정 완료 — 기록용)

사용자는 플랜의 `<followup_decision_required>` 에 제시된 선택지 중 **(A) 이 플랜(①②)만 실행하고 종료**를 명시적으로 선택했다. `DeviceSelector.xaml.cs`/`MainWindow.xaml.cs` 는 이번 작업에서 건드리지 않았다. 라이브 창 비모달 전환(선택지 B — DialogResult 대입 방식 변경, DrawScale 동기화 이벤트 이전, 창 크기/배치 조정, 그리고 무엇보다 `Sequences.IsIdle` 안전 게이트 재설계 필요)은 여전히 별도 quick 태스크 대상이며, 실기에서 U-1/U-2 확인 후 부족하면 착수 검토.

## Self-Check: PASSED

- FOUND: WPF_Example/Device/Camera/Mil/MilCamera.cs (`_szLiveRoleIdentifier` 필드, `SetLiveGrabRole`/`StartStream`/`StopStream` 반영 존재)
- FOUND: WPF_Example/UI/ControlItem/InspectionListView.xaml.cs (`_liveMirrorWatchedDatum` 필드, `OnLiveMirrorDatumPropertyChanged` 핸들러, 구독 swap 존재)
- FOUND: WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs (문구 정정 반영, `RaisePropertyChanged`/`WarnMirrorChanged` 그대로 존재)
- FOUND commit 5abe61e9 (Task 1)
- FOUND commit b1a0ccaa (Task 2)
- FOUND commit a5d80115 (Task 3)
- Build: Debug|x64 `error CS` 0건 확인됨(각 task 후 재빌드 포함)
- `DatumMeasurement.csproj`: 미스테이징·미커밋 상태 유지 확인됨(각 task 후 재확인)
