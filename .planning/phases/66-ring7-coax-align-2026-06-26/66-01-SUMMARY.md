---
phase: 66-ring7-coax-align-2026-06-26
plan: "01"
subsystem: inspection-lighting
tags: [shotconfig, inspectionsequence, lighthandler, ring7, coax, halcon, wpf]

# Dependency graph
requires:
  - phase: 64-inspection-light
    provides: "ApplyShotLightsInternal 4종 조명 매핑 + LightHandler.LIGHT_RING7 상수 등록 (D-08)"
  - phase: 66-context
    provides: "LIGHT-01 요구사항: Ring7 추가 + Coax 숨김 (D-01/D-02/D-03 잠금 결정)"
provides:
  - "ShotConfig.Ring7Light_Enabled / Ring7Light_Brightness 프로퍼티 (INI 직렬화 포함)"
  - "InspectionSequence.ApplyShotLightsInternal Ring7Light → LIGHT_RING7 점등 매핑"
  - "InspectionSequence.TurnOffShotLights RING7 소등 ($PREP Op=0 사이클 종료 대칭)"
  - "ShotConfig.CoaxLight_* [Browsable(false)] 숨김 (INI 키/매핑 코드 보존)"
affects:
  - 66-02-coax-align-backend
  - 66-03-coax-align-ui

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "ShotConfig 조명 필드 패턴: [Category(Light|XXX)] + bool/int 프로퍼티 쌍 — Ring7 추가로 5종 완성(Coax 숨김 상태)"
    - "LightHandler 매핑 패턴: ApplyShotLightsInternal if(Enabled){SetOnOff+SetLevel}else{SetOnOff(false)} — Ring7 블록 추가"
    - "TurnOffShotLights 소등 대칭 패턴: 점등 경로(ApplyShotLightsInternal)와 소등 경로(TurnOffShotLights)에 동일 그룹 목록 유지"

key-files:
  created: []
  modified:
    - WPF_Example/Custom/Sequence/Inspection/ShotConfig.cs
    - WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs

key-decisions:
  - "D-01: Ring7Light_Enabled/Brightness 프로퍼티 추가 — [Category(Light|Ring7)], PascalCase+언더스코어 패턴, ParamBase INI 직렬화 자동"
  - "D-02: ApplyShotLightsInternal Ring7→LIGHT_RING7 매핑 — 기존 4종 if-else 블록과 동일 패턴, Allman 중괄호"
  - "D-03: CoaxLight_* [Browsable(false)] 숨김 — INI 키(CoaxLight_Enabled/Brightness) + ALIGN_COAX 런타임 매핑 코드 보존, PropertyGrid에서만 숨김"
  - "Task 4 빌드 결과: Build succeeded (경고만, 오류 0) — 기존 CS0618/CS0162 경고는 사전 존재하며 이 Plan과 무관"

patterns-established:
  - "점등/소등 대칭 원칙: LightHandler 그룹 추가 시 ApplyShotLightsInternal(점등)과 TurnOffShotLights(소등) 양쪽에 동시 반영"

requirements-completed: [LIGHT-01]

# Metrics
duration: 계속 에이전트(Tasks 1-3 사전 커밋)
completed: "2026-06-29"
---

# Phase 66 Plan 01: Ring7 조명 추가 + Coax 숨김 Summary

**검사 Shot>Light PropertyGrid를 HW 사양(Ring/Back/Bar/Ring7)과 정합 — ShotConfig Ring7 프로퍼티 2개 추가, ApplyShotLightsInternal LIGHT_RING7 매핑, TurnOffShotLights RING7 소등 대칭, CoaxLight [Browsable(false)] 숨김**

## Performance

- **Duration:** 계속 실행 (Tasks 1-3: 이전 에이전트, Task 4: 계속 에이전트)
- **Started:** 2026-06-26 (이전 에이전트 시작)
- **Completed:** 2026-06-29T23:39Z
- **Tasks:** 4 (3+1)
- **Files modified:** 2

## Accomplishments

- Ring7Light_Enabled/Brightness 프로퍼티 추가 → Shot>Light 탭에 Ring7 조명 ON/OFF + 밝기 노출 (Ring/Back/Bar/Ring7 4종 자유 조합 완성)
- ApplyShotLightsInternal에 Ring7Light → LIGHT_RING7 점등 블록 추가 → $PREP 수신 시 Ring7 조명 제어 동작
- TurnOffShotLights에 RING7 소등 추가 → $PREP Op=0 사이클 종료 시 Ring7 잔존 없이 전 조명 소등 (점등/소등 대칭)
- CoaxLight_* [Browsable(false)] 숨김 → 검사 PropertyGrid에서 동축 비노출 (INI 키/ALIGN_COAX 런타임 매핑 하위호환 유지)

## Task Commits

각 Task는 원자적으로 커밋됨:

1. **Task 1: ShotConfig Ring7 필드 추가 + CoaxLight [Browsable(false)] 숨김 (D-01, D-03)** - `db2ce56` (feat)
2. **Task 2: InspectionSequence ApplyShotLightsInternal Ring7→LIGHT_RING7 매핑 추가 (D-02)** - `0ce3038` (feat)
3. **Task 3: InspectionSequence TurnOffShotLights RING7 소등 추가 — 점등/소등 대칭** - `99e7e5b` (feat)
4. **Task 4: 빌드 검증 (Debug/x64) + 회귀 0 확인** - (별도 코드 커밋 없음, 빌드 PASS 확인)

**Plan 메타데이터:** (이 SUMMARY 커밋 포함)

## Files Created/Modified

- `WPF_Example/Custom/Sequence/Inspection/ShotConfig.cs` — Ring7Light_Enabled/Brightness 프로퍼티 + [Category("Light|Ring7")] 추가; CoaxLight_Enabled/Brightness 위 [Browsable(false)] 추가 (5줄 추가, 삭제 0)
- `WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs` — ApplyShotLightsInternal Ring7 if-else 점등 블록(11줄) + 주석 1줄 갱신; TurnOffShotLights RING7 소등 1줄 추가 (총 13줄 추가, 1줄 교체)

## Decisions Made

- D-01: Ring7 프로퍼티 위치 — SideLight 블록 뒤에 추가. 노출 순서: Ring/Back/Side/Ring7 (Coax 숨김 상태)
- D-02: 기존 4종 블록과 동일한 if(Enabled){SetOnOff(true)+SetLevel}else{SetOnOff(false)} 패턴 사용 — 일관성 유지
- D-03: [Browsable(false)]만 추가, CoaxLight 필드/ALIGN_COAX 매핑 코드는 유지 — Plan 03 Align 창이 런타임에 계속 참조

## Deviations from Plan

없음 — 플랜대로 정확히 실행됨.

## Issues Encountered

**Task 4 빌드 실행 환경 이슈:** Bash에서 MSBuild.exe를 `/p:` 슬래시 인자로 직접 호출 시 경로 파싱 오류 발생. PowerShell 경유(`powershell.exe -Command "& '...' /p:..."`)로 우회하여 빌드 성공. 기능적 영향 없음.

## Build Evidence (Task 4)

```
빌드 결과: DatumMeasurement -> C:\Info\Project\DataMeasurement\WPF_Example\bin\x64\Debug\DatumMeasurement.exe
오류: 0건
경고: CS0618(기존 Obsolete), CS0162(기존 unreachable), MSB3884(ruleset 미존재) — 모두 이 Plan 이전부터 존재하는 사전 경고
```

## Regression-Zero Evidence

```
git diff --stat 5815c0a..HEAD -- WPF_Example/
  InspectionSequence.cs | 13 ++++++++++++-
  ShotConfig.cs         |  5 +++++
  2 files changed, 17 insertions(+), 1 deletion(-)

삭제 라인(git diff ... | grep "^-"):
  InspectionSequence.cs: 1줄 (주석 매핑 목록 갱신 — 기존 설명 교체)
  ShotConfig.cs: 0줄
기존 LIGHT_RING / LIGHT_BACK / LIGHT_ALIGN_COAX / LIGHT_BAR 블록: 무변경
```

## Self-Check

- [x] `db2ce56` 커밋 존재 (Task 1)
- [x] `0ce3038` 커밋 존재 (Task 2)
- [x] `99e7e5b` 커밋 존재 (Task 3)
- [x] `Ring7Light_Enabled` grep 결과 = 2건 (ShotConfig.cs)
- [x] `LIGHT_RING7` grep 결과 = 5건 (InspectionSequence.cs: 점등 3건 + 소등 1건 + 주석 1건)
- [x] TurnOffShotLights 본문 LIGHT_RING7 1건
- [x] Build succeeded (오류 0)
- [x] git diff 변경 파일 = 2건만 (ShotConfig.cs + InspectionSequence.cs)

## Self-Check: PASSED

## Next Phase Readiness

- Plan 02 (Coax 동축 Align 백엔드): 이 Plan과 파일 겹침 없음, 병렬 가능
- Plan 03 (Align UI): Plan 02 완료 후 진행
- 실 HW UAT: 검사 Shot>Light 탭에서 Ring7 ON/OFF + 밝기 + Coax 비표시 확인 필요

---
*Phase: 66-ring7-coax-align-2026-06-26*
*Plan: 01*
*Completed: 2026-06-29*
