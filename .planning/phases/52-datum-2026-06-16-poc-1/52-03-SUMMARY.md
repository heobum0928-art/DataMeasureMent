---
phase: 52-datum-2026-06-16-poc-1
plan: 03
subsystem: inspection-sequence
tags: [datum, leveling, image-rotation, action-statemachine, estep, halcon]

# Dependency graph
requires:
  - "InspectionSequence leveling 멤버 (LevelingEnabled/LevelingAngleRad/LevelingComputed/SetLevelingAngle/ResetLeveling, 52-01)"
  - "DatumConfig.IsLevelingReference (52-01)"
  - "DatumFindingService.TryGetLevelingAngle (52-02)"
  - "VisionAlgorithmService.RotateImageByAngle (52-02)"
provides:
  - "InspectionSequence.TryComputeLevelingAngle — 기준 Datum 1회 각도 산출 + 캐시 (시퀀스당 1회, D-03)"
  - "Action_FAIMeasurement EStep.Level — 레벨링 각도 산출 step (DatumPhase 앞)"
  - "DatumPhase(1-image+DualImage) + Grab 회전 적용 — 회전된 단일 이미지로 Datum 검출 + 전 FAI 측정 (D-02 원안)"
affects:
  - "52-04 (UAT/육안 검증 — 회전 정렬 + 부호 방향 시각 확인)"

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "EStep.Level = MoveZ 와 DatumPhase 사이 신규 step, 각도 산출만 수행 (시퀀스당 1회 캐시 가드)"
    - "회전 게이팅 = LevelingEnabled && LevelingComputed 양 조건 (off/미산출 → pass-through, 회귀 0)"
    - "회전 원본 dispose 후 회전본 교체 (img/imgH/imgV/image 전부 누수 가드)"
    - "Datum 검출·측정 동일 -LevelingAngleRad 동일 회전중심 → 좌표계 정합 보존"

key-files:
  created: []
  modified:
    - WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs
    - WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs

key-decisions:
  - "TryComputeLevelingAngle = LevelingComputed 캐시 가드 우선 → 재산출 0 (시퀀스당 1회, D-03). 기준 미지정/실패 false+0.0 무회전 폴백 (lenient, abort 없음)"
  - "EStep.Level 삽입 위치 = MoveZ 와 DatumPhase 사이 — 회전 이미지가 DatumPhase 검출 + Measure 둘 다의 입력이 되도록 각도를 먼저 확정"
  - "좌표계 정합 = 방식 (a) 이미지 회전 + taught ROI 좌표 유지 (소각도 가정). ROI affine 변환 없음 — 이중보정 금지"
  - "부호 규약 = 회전각 -LevelingAngleRad. DatumPhase·Grab 양쪽 동일 부호 동일 각도 (동일 수평 정렬 프레임)"

patterns-established:
  - "신규 EStep 삽입: enum 멤버 추가 + 직전 step 의 Step= 분기 갱신 + 신규 case 추가 (pass-through 게이트로 회귀 0)"
  - "다운스트림 일관 회전: Datum 검출 입력과 측정 입력에 동일 각도·동일 회전중심 적용으로 좌표계 정합"

requirements-completed: [LEVEL-01]

# Metrics
duration: 9min
completed: 2026-06-17
---

# Phase 52 Plan 03: 레벨링 각도 1회 산출 + DatumPhase/Grab 회전 통합 Summary

**EStep.Level 을 Action_FAIMeasurement 에 삽입해 레벨링 기준 Datum 으로 각도를 시퀀스당 1회 산출(D-03)하고, DatumPhase(1-image+DualImage)·Grab 양쪽에 동일 -각도 회전을 적용해 회전된 단일 이미지로 Datum 검출 + 전 FAI 측정 전체가 동작하게 만듦(D-02 원안). LevelingEnabled off/미산출/실패 시 무회전 pass-through 로 회귀 0.**

## Performance

- **Duration:** ~9 min
- **Started:** 2026-06-17T04:00:53Z
- **Completed:** 2026-06-17T04:10:00Z (approx)
- **Tasks:** 2
- **Files modified:** 2

## Accomplishments

### Task 1 — InspectionSequence.TryComputeLevelingAngle (commit 29a0fc3)
- `public bool TryComputeLevelingAngle(HImage refImage, out double angleRad)` 추가 (TryGetDatumTransform 뒤).
- `LevelingComputed` 캐시 가드 우선 분기 → 이미 산출됐으면 캐시값 반환 (시퀀스당 1회, D-03).
- `DatumConfigs` 에서 `IsLevelingReference==true` 첫 Datum 탐색 → `DatumFindingService.TryGetLevelingAngle` 호출 → `SetLevelingAngle(computed)` 캐시.
- 기준 미지정 → false + Logging (무회전 폴백). 산출 실패 → false + Logging (lenient). 산출 성공 시 deg 환산 Trace 로그.
- refImage null 가드. throw 0.

### Task 2 — Action_FAIMeasurement EStep.Level + 회전 적용 (commit 0fbba79)
- EStep enum 에 `Level` 멤버 추가 (MoveZ 와 DatumPhase 사이). 흐름 = Init → MoveZ → **Level** → DatumPhase → Grab → Measure → End.
- MoveZ step 의 `Step = (int)EStep.DatumPhase;` → `Step = (int)EStep.Level;` 로 갱신.
- 신규 `case EStep.Level`: `LevelingEnabled && !LevelingComputed` 시 기준 Datum raw 이미지(`GrabOrLoadDatumImage`)로 `TryComputeLevelingAngle` 호출(시퀀스당 1회). refImage finally dispose. off 면 즉시 pass-through.
- DatumPhase 진입부: `datumLevelOn = LevelingEnabled && LevelingComputed`, `datumLevelAngle = -LevelingAngleRad` 1회 계산.
- DatumPhase 1-image 경로: `GrabOrLoadDatumImage` 직후 `datumLevelOn` 시 `RotateImageByAngle(img, datumLevelAngle)` → 원본 dispose 후 교체. 기존 finally img.Dispose() 가 회전본 해제.
- DatumPhase DualImage 경로: `TryGrabOrLoadDualDatumImages` 성공 직후 imgH/imgV 둘 다 동일 -각도 회전 → 각자 dispose 후 교체. 기존 finally 가 회전본 해제.
- Grab step: `image != null` 직후 `LevelingEnabled && LevelingComputed` 시 `RotateImageByAngle(image, -LevelingAngleRad)` → 원본 dispose 후 교체 → SetImage/ResultHalconImage 회전본 사용.

## 일관성 보장 (D-02 원안 핵심)

- Datum 검출(DatumPhase)과 측정(Grab→Measure)이 **동일 `-LevelingAngleRad`, 동일 회전중심(이미지 중심, RotateImageByAngle 내부)** 로 회전된 이미지를 입력받는다. 따라서 Datum 좌표계와 측정 좌표계가 동일 수평 정렬 프레임에 있어 정합 유지.
- **taught ROI 좌표 미변환** (방식 a, 소각도 가정). 이미지만 회전하고 ROI 좌표는 그대로 — 이중보정 금지.
- 부호 규약: `TryGetLevelingAngle` = Math.Atan2 라디안. 레벨링 = 기울기 0 으로 → 회전각 `-angle`. Datum/측정 양쪽 동일 적용. **UAT(52-04)에서 부호/방향 시각 확인 필요 — 잘못된 방향이면 양쪽 동시 부호 반전.**

## Files Created/Modified
- `WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs` — TryComputeLevelingAngle 신규 (+41 라인)
- `WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs` — EStep.Level 멤버 + Level case + DatumPhase 1-image/DualImage 회전 + Grab 회전 (+54 라인)

## Task Commits

1. **Task 1: InspectionSequence.TryComputeLevelingAngle** - `29a0fc3` (feat)
2. **Task 2: Action_FAIMeasurement EStep.Level + DatumPhase/Grab 회전** - `0fbba79` (feat)

_Note: 두 task 모두 plan 에서 tdd="true" 였으나 이 프로젝트에는 단위 테스트 프레임워크가 없어(CLAUDE.md "No test framework detected"), 플랜 `<verify>`/`<acceptance_criteria>` 가 명시한 grep 기반 acceptance + msbuild Debug/x64 PASS 로 RED/GREEN 을 대체했다. 모든 grep 기준 + 빌드 PASS._

## Verification

- msbuild Debug/x64 Build: **PASS (0 errors)** — Task 1 후 1회, Task 2 후 1회.
- Task 1 grep: `public bool TryComputeLevelingAngle` (1), `d.IsLevelingReference` (1), `service.TryGetLevelingAngle` (1).
- Task 2 grep: `case EStep.Level` (1), `RotateImageByAngle` (4 = 1-image 1 + DualImage 2 + Grab 1, min 3 충족), `datumLevelOn` (4 = decl 1 + usage 3), `lvlSeq.TryComputeLevelingAngle` (1), `Step = (int)EStep.Level;` (1 = MoveZ 갱신), `Level,` enum 멤버 (1).
- 회전 게이팅: DatumPhase + Grab 모두 `LevelingEnabled && LevelingComputed` 조건 → off/미산출 pass-through (회귀 0).
- taught ROI affine 변환 코드 없음 (방식 a 준수).
- 회전 원본 dispose 후 교체 — img/imgH/imgV/image 전부 누수 가드.
- 기존 datum skip/log/MarkDatumFailed lenient 분기 전부 보존.

## Threat Mitigations Applied
- **T-52-05 (Tampering / malformed 각도로 이미지 회전)**: `RotateImageByAngle` 가 예외/근사0 시 원본 복사 폴백(52-02). 각도 미산출(`LevelingComputed=false`) → 회전 분기 미진입 → 무회전. corrupt 레시피가 측정 파괴 불가. 회전 후 datum 검출 실패는 기존 lenient skip(MarkDatumFailed) 경로로 흡수.
- **T-52-06 (Info Disclosure / 회전 HImage 메모리 수명)**: accept (단일 사용자 오프라인 앱). dispose 누수만 코드로 가드 (원본 dispose 후 교체) — img/imgH/imgV/image/refImage 전부 적용.

## Deviations from Plan

None - plan executed exactly as written. (TDD 형식은 프로젝트에 단위 테스트 인프라가 없어 플랜이 명시한 grep + msbuild 검증으로 진행 — 플랜 `<verify>`/`<acceptance_criteria>` 가 grep 으로 정의되어 deviation 아님.)

## Known Stubs
None. 52-01 에서 interface-first stub 였던 레벨링 각도 캐시(`_levelingAngleRad`/`_levelingComputed`)가 본 plan 에서 `TryComputeLevelingAngle` write + DatumPhase/Grab read 로 완전히 소비됨.

## Next Phase Readiness
- LEVEL-01 코드 경로 완성: 각도 산출(52-02) → 시퀀스 1회 산출/캐시(52-03 Task 1) → EStep.Level → DatumPhase/Grab 회전(52-03 Task 2).
- 52-04 UAT 필요 사항: (1) LevelingEnabled=on 시 회전 후 Datum 수평 라인 오버레이가 수평 정렬되는지 + 검출 성공(taught ROI 가 회전 이미지 에지 덮음) 육안 확인. (2) 부호/방향 시각 확인 — 잘못된 방향이면 -LevelingAngleRad 부호 양쪽 동시 반전. (3) LevelingEnabled=off 회귀 0 확인.
- 큰 각도 케이스(taught ROI 가 회전 잘림/배경 영역 진입)는 POC SCOPE 외 — UAT 관찰 후 carry-over.

## User Setup Required
None - no external service configuration required.

## Self-Check: PASSED

- WPF_Example/Custom/Sequence/Inspection/InspectionSequence.cs — FOUND (TryComputeLevelingAngle present)
- WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs — FOUND (case EStep.Level present)
- commit 29a0fc3 — FOUND
- commit 0fbba79 — FOUND

---
*Phase: 52-datum-2026-06-16-poc-1*
*Completed: 2026-06-17*
