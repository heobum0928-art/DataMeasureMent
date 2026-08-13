---
phase: quick-260813-jnh
plan: 01
subsystem: device/vision-inspection
tags: [halcon, mil-camera, cxp, grab-direction, mirror, datum, shot, wpf]

# Dependency graph
requires:
  - phase: quick-260813-fdt
    provides: "DatumConfig.MirrorX/MirrorY 설정 프로퍼티 (commit b49d14f) — 이번 plan이 실제 하드웨어에 배선"
provides:
  - "MIL 역할 4종(무미러 기준 + #MX/#MY/#MXY) 앱 시작 시 정적 등록 — MilCamera.cs 무수정"
  - "DeviceHandler.GrabHalconImage(ICameraParam, string requestIdentifier) 2-인자 오버로드"
  - "InspectionSequence.ResolveShotGrabMirror — Shot→FAI→Measurement→DatumRef 로 소유 Datum 미러 역추적 + fail-safe"
  - "grab 호출부 5곳(생산 2곳 + 티칭 3곳) 미러 역할 식별자 배선 완료"
affects: [mil-camera, side-inspection, datum-detection, shot-grab, offline-inspect-mode]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "정적 역할(role) 사전등록 패턴 — 레시피 스캔 없이 앱 시작 시 (X,Y) 불리언 2개의 전체 조합(4가지)을 MilCamera._roleInfoMap 에 등록, grab 시점엔 식별자 문자열만 선택"
    - "Shot→DatumRef 역추적 fail-safe 패턴 — 참조 해석 실패/불일치 시 무조건 안전측(무미러) 폴백 + Error 로그, 조용한 오검 방지"

key-files:
  created: []
  modified:
    - WPF_Example/Custom/Device/DeviceHandler.cs
    - WPF_Example/Device/DeviceHandler.cs
    - WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs
    - WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs
    - WPF_Example/UI/ContentItem/MainView.xaml.cs

key-decisions:
  - "MIL 역할 4종을 등록되는 모든 MIL 카메라(SIDE 뿐 아니라 TOP/BOTTOM 공유 인스턴스 포함)에 대해 생성 — 미등록 식별자가 ResolveRoleInfo 의 기본 Info 폴백으로 조용히 다른 카메라 방향을 쓰게 되는 함정을 원천 차단하는 안전 설계"
  - "RESEARCH.md 의 '고아 DatumRef' 주장은 2026-08-13 라이브 레시피 전수 재확인(DatumRef= 122건 전부 실재 Datum 6개와 일치) 결과 사실이 아니었음이 확인되어, 계획됐던 레시피 데이터 교정 태스크를 실행 전에 제거함 — 레시피는 한 바이트도 편집하지 않음"
  - "Task 3(실기 MIL 하드웨어 미러 육안 확인)은 물리 SIDE PC/CXP 카메라가 없어 defer — 코드/빌드 검증은 완료, 실기 UAT 미수행 상태로 기록"

patterns-established:
  - "역할별 미러 등록은 _roleInfoMap 전용(Devices 딕셔너리에는 절대 추가하지 않음) — INI 영속화/UI 드롭다운 오염 방지가 필요한 모든 향후 역할 확장에 재사용 가능한 규약"

requirements-completed: [QUICK-260813-JNH]

# Metrics
duration: ~16min (Task 1+2 코드/빌드 검증만; Task 3 는 실기 보류)
completed: 2026-08-13
---

# Quick Task 260813-jnh: MirrorX/Y → MIL Grab 방향 배선 (Part 2/2) Summary

**Part 1(quick-260813-fdt)에서 추가만 해두고 아무도 읽지 않던 `DatumConfig.MirrorX/MirrorY` 를, MIL 역할 4종 정적 등록 + `GrabHalconImage(param, requestIdentifier)` 2-인자 오버로드 + Shot→DatumRef 역추적으로 실제 CXP 카메라 grab 방향(`M_GRAB_DIRECTION_X/Y`) 반전에 연결. HALCON 소프트웨어 미러(mirror_image) 는 택타임 비용 때문에 전면 배제, MIL 하드웨어 반전(비용 0)만 사용. 실기 육안 검증(Task 3)은 물리 SIDE 하드웨어 부재로 defer.**

## Performance

- **Duration:** 코드 작업(Task 1-2) 약 16분 (베이스라인 측정 포함, 두 커밋 타임스탬프 15:24~15:27 기준)
- **Completed:** 2026-08-13
- **Tasks:** 2/3 완료 (Task 3 는 defer — 아래 "Task 3 처리" 참고)
- **Files modified:** 5

## Accomplishments
- MIL 역할 4종(무미러 기준 + `#MX`/`#MY`/`#MXY`)이 등록되는 모든 MIL 카메라에 대해 앱 시작 시 정적 등록됨. `MilCamera.cs` 변경 0줄(요구사항대로 `RegisterRoleInfo`/`ResolveRoleInfo`/`GrabFromBuffer` 기존 코드만 재사용).
- `DeviceHandler.GrabHalconImage(ICameraParam, string)` 2-인자 오버로드 신설. 기존 1-인자는 시그니처·동작 완전 동일 유지(내부적으로 새 오버로드에 위임만).
- `InspectionSequence.ResolveShotGrabMirror` 신설 — Shot 이 참조하는 measurement 들의 `DatumRef` 를 순회해 소유 Datum 을 찾고, 그 Datum 의 `MirrorX/MirrorY` 를 채택. 해석 실패 또는 서로 다른 Datum 을 혼재 참조하는 경우 둘 다 fail-safe 로 무미러 + `[ShotMirror]` Error 로그.
- 생산 경로 grab 2곳(`Action_FAIMeasurement.cs` — Shot 검사이미지 grab, Datum 검출 grab) + 티칭 경로 grab 3곳(`MainView.xaml.cs` — `GrabAndDisplay` x2, `GrabSaveAndDisplay`) 전부 미러 역할 식별자로 배선 완료.
- 회귀 0 구조적으로 보장: 라이브 레시피에 `Mirror` 키가 아직 0건이라(2026-07-29 저장분, Part 1 커밋보다 이전) 모든 기존 Datum/Shot 이 무미러로 로드되고, `BuildGrabRoleIdentifier` 는 둘 다 false 면 base 식별자를 그대로 반환 — 사용자가 직접 미러를 켜기 전까지 변경 전과 바이트 단위로 동일한 인자로 grab.

## Task Commits

Each task was committed atomically:

1. **Task 1: MIL 미러 역할 4종 정적 등록 + 2-인자 grab 오버로드 (인프라)** - `37c8875` (feat)
2. **Task 2: Shot→Datum 미러 역추적(fail-safe) + grab 호출부 5곳 배선** - `36e8f94` (feat)
3. **Task 3: 실기 MIL 하드웨어 미러 육안 확인** - 코드 변경 없음 (checkpoint, defer — 아래 참고)

## Files Created/Modified
- `WPF_Example/Custom/Device/DeviceHandler.cs` - `BuildGrabRoleIdentifier`/`BuildMirrorRoleInfos`/`CloneRoleInfo` 순수 헬퍼 3개 + `MIRROR_ROLE_SUFFIX_*` 상수 3개 추가 (RegisterCxpCamera 아래)
- `WPF_Example/Device/DeviceHandler.cs` - MIL 등록 분기(`#else`)에서 등록되는 모든 MIL 역할에 미러 3조합(`_roleInfoMap` 전용) 등록 + `GrabHalconImage(ICameraParam, string)` 2-인자 오버로드 신설(기존 1-인자는 위임으로 전환)
- `WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs` - `ResolveShotGrabMirror` + `FindDatumByName` 사설 헬퍼 신설 (Allman 스타일, `IsDatumRefUnresolvable` 바로 아래)
- `WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs` - `EStep.Grab` 라이브 grab 분기 + `GrabOrLoadDatumImage` else 분기 2곳에 미러 역할 식별자 배선
- `WPF_Example/UI/ContentItem/MainView.xaml.cs` - `ResolveGrabRoleIdentifier` private static 헬퍼 신설 + `GrabAndDisplay` x2, `GrabSaveAndDisplay` 3곳 배선

## Decisions Made
- **등록 범위: SIDE 전용이 아니라 등록되는 모든 MIL 역할.** `CameraRole.Side`(PC2)에서는 자연히 `CAM_SIDE` 하나만 등록되므로 결과적으로 SIDE 4종만 생기지만, TOP/BOTTOM 공유 인스턴스(PC1)에도 동일 코드가 적용되도록 특수분기를 넣지 않았다. 이유: 미등록 식별자는 `MilCamera.ResolveRoleInfo` 가 기본 `Info`(공유 인스턴스의 첫 등록 역할, 예: TOP)로 조용히 폴백하는데, SIDE 전용 분기를 넣으면 이 폴백 함정이 TOP/BOTTOM 쪽에 남는다. 전 역할 등록이 이 함정을 원천 제거하며, TOP/BOTTOM 의 기존 동작(상수값 무변경 + 변형 역할은 어떤 Datum 도 미러를 켜지 않는 한 조회되지 않음)에는 영향이 없다.
- **RESEARCH.md 레시피 서술 폐기.** 이 plan 은 2 회 plan-check 검증을 거쳤고, 1차에서 RESEARCH.md 의 라이브 레시피 관련 서술(파일 크기·Shot 이름·"고아 DatumRef" 결함 주장)이 허구임이 드러났다. 2026-08-13 전수 재확인 결과 `DatumRef=` 122건이 전부 실재 Datum 6개(`Top_Datum`/`Side_Datum_3-1`/`Side_Datum_3-2`/`Side_Datum_4-1`/`Side_Datum_4-2`/`Bottom_Datum`) 중 하나와 정확히 일치했다. 원래 계획돼 있던 "stale DatumRef 교정" 태스크는 대상 결함이 존재하지 않아 plan 에서 완전히 제거됐고, 레시피 파일은 이번 작업에서 한 바이트도 편집되지 않았다(작업 전후 261714 bytes / 2026-07-29 17:40 mtime 동일 확인).
- **SIDE Shot↔Datum 실측 매핑** (2026-08-13 라이브 레시피 직접 확인): `SHOT_3-1`→`Side_Datum_3-1`, `SHOT_3-2-1`/`SHOT_3-2-2`→`Side_Datum_3-2`, `SHOT_4-1-1`(6측정)/`SHOT_4-1-2`(3측정)→`Side_Datum_4-1`, `SHOT_4-2-1`/`SHOT_4-2-2`→`Side_Datum_4-2`. 7개 Shot 전부 단일 Datum 만 참조하므로, `ResolveShotGrabMirror` 의 "혼재 참조" 분기(8번)는 현재 데이터에서 발동하지 않는 순수 방어 코드다.
- **Part 1 경고 문구 부정확성 — 무해, 이번 범위 밖.** `DatumConfig.cs` 의 "프로그램을 다시 시작해야 적용된다" 안내는 `MilCamera.cs:322-323` 이 매 grab 마다 `M_GRAB_DIRECTION_X/Y` 를 재적용하는 기존 동작(quick-260805-jtj 이후 확립)과 맞지 않아 실제보다 보수적이다. `DatumConfig.cs` 는 이번 작업의 수정 금지 대상이라 문구는 고치지 않았다. Task 3(defer)에서 재시작 없이 반영되는지 관찰 예정이었으나 실기 미보유로 관찰도 보류됨 — 향후 실기 UAT 시 함께 확인 권장.

## Deviations from Plan

None - plan executed exactly as written (Task 1, Task 2). Task 3 was a pre-planned `checkpoint:human-verify` requiring physical hardware; deferring it per the plan's own `<resume-signal>` protocol ("defer" 응답 시 실기 UAT 미수행 상태로 기록) is not a deviation — it is the plan's designed fallback path when hardware isn't available.

## Issues Encountered

**Task 3 — 실기 하드웨어 부재로 defer.** 이 개발 PC 는 `SIMUL_MODE`(Debug) 이고 물리 SIDE PC/CXP 카메라가 연결돼 있지 않다. Plan 이 명시한 대로 "로컬에서 원리적으로 검증 불가능한" 항목(`MilCamera` 객체 자체가 SIMUL 에선 생성되지 않고 `VirtualCamera` 로 대체되며, `VirtualCamera.GrabHalconImage(string)` 는 식별자를 통째로 무시함)이라 로컬 대체 검증을 시도하지 않았다. 사용자가 물리 하드웨어 미보유를 확인해 "defer" 로 명시 지시했고, 이 SUMMARY 에 실기 UAT 미수행 상태로 정확히 기록한다(가짜 PASS 처리 금지).

## Task 3 처리 (Deferred — 실기 SIDE 하드웨어 확보 후 재개 필요)

**상태: DEFERRED — 물리 SIDE MIL/CXP 카메라 하드웨어 부재로 미수행. 코드/빌드 검증은 완료, 실기 하드웨어 검증만 남음.**

Task 1-2 로컬에서 이미 검증된 것 (재확인 불필요):
- Debug/x64 + Release/x64 컴파일 통과 (Release 는 SIMUL_MODE 가 없어 MIL 등록 분기가 실제로 컴파일됨)
- MIL 역할 4종(`CAM_SIDE`, `CAM_SIDE#MX`, `CAM_SIDE#MY`, `CAM_SIDE#MXY`) 등록 코드 존재 및 grep 검증 통과
- Shot→DatumRef→Datum 미러 역추적 + 해석 실패 시 무미러 fail-safe + Error 로그 코드 존재
- HALCON 소프트웨어 미러 0건, 삼항 0건, 운영 레시피 무편집(작업 전후 파일 동일)

로컬에서 원리적으로 검증 불가능해 남아있는 것 (다음 실기 세션에서 수행할 것 — Plan Task 3 의 Test 1~6 그대로):
1. **Test 1 — Datum 이미지 반전**: `Side_Datum_4-1.MirrorY=True` 설정 후 검사이미지 Grab → 상하 반전 육안 확인
2. **Test 2 — Shot 이미지가 같은 방향으로 따라오는지**: `SHOT_4-1-1`/`SHOT_4-1-2` grab → Datum 이미지와 동일 방향 반전 확인 (설계 A 의 핵심 가정)
3. **Test 3 — 회귀 0 확인**: 나머지 SIDE Datum/Shot 3쌍(`Side_Datum_3-1`↔`SHOT_3-1`, `Side_Datum_3-2`↔`SHOT_3-2-1`/`SHOT_3-2-2`, `Side_Datum_4-2`↔`SHOT_4-2-1`/`SHOT_4-2-2`) 방향 무변화 확인
4. **Test 4 — 전체 사이클**: `MirrorY=True` 유지한 채 SIDE 시퀀스 전체 검사 1회 실행, FAI 측정값 정합성 확인
5. **Test 5 — fail-safe 로그 확인**: `[ShotMirror]` Error 정상 0건, (선택) 의도적 오류주입 시 로그 발생 확인 후 즉시 원복
6. **Test 6 — 재시작 필요 여부 관찰(기록용)**: `MirrorY` 변경 후 재시작 없이 즉시 반영되는지 관찰만

**재개 방법:** 물리 SIDE PC(`CameraRole=Side`)에 Release/x64 빌드(커밋 `36e8f94` 이후 어떤 빌드든 가능) 를 배포하고, 위 Test 1~6 를 `.planning/quick/260813-jnh-mirrorx-y-mil-grab-side-datum-part-2-2/260813-jnh-PLAN.md` Task 3 `<how-to-verify>` 원문 그대로 수행. 완료 후 이 SUMMARY 를 갱신하거나 후속 quick task 로 결과를 기록할 것.

## User Setup Required

None - no external service configuration required. 실기 SIDE MIL 하드웨어 확보 시 위 "Task 3 처리" 섹션의 Test 1~6 수행 필요.

## Next Phase Readiness
- 코드/빌드 관점에서는 완결 — Part 1(DatumConfig 설정) + Part 2(MIL grab 배선)로 MirrorX/Y 기능이 end-to-end 로 코드에 존재한다.
- 남은 블로커: 실기 SIDE MIL 하드웨어 UAT(Task 3) — 물리 카메라 확보 시 최우선 재개 대상.
- 이번 작업으로 TOP/BOTTOM 경로에는 코드상 어떤 동작 변화도 없음(상수 무변경, 변형 역할은 Datum 이 미러를 켜지 않는 한 조회되지 않음) — 별도 TOP/BOTTOM 회귀 검증 불필요.

---
*Phase: quick-260813-jnh*
*Completed: 2026-08-13*

## Self-Check: PASSED

- FOUND: `WPF_Example/Custom/Device/DeviceHandler.cs`
- FOUND: `WPF_Example/Device/DeviceHandler.cs`
- FOUND: `WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs`
- FOUND: `WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs`
- FOUND: `WPF_Example/UI/ContentItem/MainView.xaml.cs`
- FOUND: `.planning/quick/260813-jnh-mirrorx-y-mil-grab-side-datum-part-2-2/260813-jnh-SUMMARY.md`
- FOUND: `.planning/quick/260813-jnh-mirrorx-y-mil-grab-side-datum-part-2-2/260813-jnh-PLAN.md`
- FOUND commit: `37c8875` (Task 1)
- FOUND commit: `36e8f94` (Task 2)
