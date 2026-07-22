---
phase: 260722-p5d
plan: 01
subsystem: vision-measurement
tags: [halcon, projection_pl, fit_line, datum, measurement-math]

# Dependency graph
requires: []
provides:
  - "ArcLineIntersectDistanceMeasurement: IntersectionPointSelection(Far/Close) 실동작 복원 (int1/int2 분기)"
  - "DualImageEdgeDistanceMeasurement/EdgeToLineDistanceMeasurement/ArcEdgeDistanceMeasurement: per-edge-point projection 평균 측정 수학"
affects: [FAI measurement recipes using ArcLineIntersectDistance/DualImageEdgeDistance/EdgeToLineDistance/ArcEdgeDistance types]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "VisionAlgorithmService.TryFitLine 의 opt-in collectedEdges 파라미터로 fit-line 에 실제 쓰인 (row,col) 점들을 caller 에 노출 후 per-point 재투영 평균"
    - "nPts==0 폴백으로 기존 단일-중점 동작 100% 재현 (회귀 방지)"

key-files:
  created: []
  modified:
    - WPF_Example/Custom/Sequence/Inspection/Measurements/ArcLineIntersectDistanceMeasurement.cs
    - WPF_Example/Custom/Sequence/Inspection/Measurements/DualImageEdgeDistanceMeasurement.cs
    - WPF_Example/Custom/Sequence/Inspection/Measurements/EdgeToLineDistanceMeasurement.cs
    - WPF_Example/Custom/Sequence/Inspection/Measurements/ArcEdgeDistanceMeasurement.cs

key-decisions:
  - "ArcLineIntersect: Close→좌 교점(int1), Far/기본/미설정→우 교점(int2, INI 하위호환 회귀 0)"
  - "DualImage/EdgeToLine/ArcEdge 3종은 각 파일의 기존 부호식(unsigned sqrt / signed 3분기 / ComputeProjectionDistance signed)을 per-point 루프에서 그대로 재사용, 새 부호 공식 도입 없음"
  - "EdgeToLine 레거시 else 경로(AffineTransPoint2d)는 무변경 — affine 이 선형 사상이라 per-point 평균과 중점변환이 수학적으로 동일"

patterns-established:
  - "Pattern: TryFitLine(..., collectedEdges) opt-in 리스트 → per-point projection loop → nPts>=1 이면 평균값+평균좌표 재대입, nPts==0 이면 기존 단일-중점 코드 폴백"

requirements-completed: [FIX-1-FarClose, FIX-2-PerPointAvg]

# Metrics
duration: ~20min
completed: 2026-07-22
---

# Phase 260722-p5d: Fix ArcLineIntersect Far/Close + Per-Point Projection Averaging Summary

**ArcLineIntersect 의 Far/Close 교점 선택 회귀 복원 + DualImage/EdgeToLine/ArcEdge 3종의 중점-투영을 per-edge-point 투영 평균으로 교체 (각 파일 기존 부호/축 규약 보존, 빈 리스트 폴백 포함)**

## Performance

- **Duration:** ~20 min
- **Completed:** 2026-07-22
- **Tasks:** 4/4 completed
- **Files modified:** 4

## Accomplishments

- `ArcLineIntersectDistanceMeasurement.cs`: 260716 재설계 때 하드코딩(항상 int2=우 교점)으로 폐기됐던 `IntersectionPointSelection`(Far/Close) 토글을 실제 분기로 복원. Close 레시피(SHOT_I10 등)가 다시 좌 교점(int1) 기준으로 측정된다.
- `DualImageEdgeDistanceMeasurement.cs`: PointROI fit 라인의 단일 중점 1점을 LineROI 기준선에 투영하던 것을, `TryFitLine` 의 `collectedEdges` 로 수집한 개별 에지점 각각을 투영한 UNSIGNED 거리들의 산술평균으로 교체.
- `EdgeToLineDistanceMeasurement.cs`: `datumOriginInjected` 정상 경로에서 에지 중점 1점 투영을, 수집 에지점 각각을 axis 에 투영한 SIGNED per-point 거리 평균으로 교체 (measureX/useAngle2/Y 3분기 부호식은 항별 그대로 재사용). 레거시 else(affine) 경로는 무변경.
- `ArcEdgeDistanceMeasurement.cs`: `ComputeProjectionDistance` 단일 helper 호출을, 수집 에지점별 helper 호출(fOk 게이트) 평균으로 교체. datum 기준선(FAI-DatumLine) 오버레이는 무변경.
- 4개 파일 모두 msbuild Debug/x64 클린 빌드 확인 (신규 에러/경고 0 — 기존 프로젝트 전역 warning 5건은 본 작업과 무관, 사전 존재).

## Task Commits

Each task was committed atomically:

1. **Task 1: Restore IntersectionPointSelection (Far/Close) in ArcLineIntersectDistanceMeasurement** - `9d840ec` (fix)
2. **Task 2: Average per-edge-point projection in DualImageEdgeDistanceMeasurement (unsigned)** - `565aac7` (fix)
3. **Task 3: Average per-edge-point projection in EdgeToLineDistanceMeasurement (signed, main path only)** - `fe1b72f` (fix)
4. **Task 4: Average per-edge-point projection in ArcEdgeDistanceMeasurement (helper loop)** - `c77a165` (fix)

**Plan metadata:** (handled by orchestrator — not committed by executor per constraints)

## Files Created/Modified

- `WPF_Example/Custom/Sequence/Inspection/Measurements/ArcLineIntersectDistanceMeasurement.cs` - Close/Far 교점 선택 분기 복원(target=int1/int2), segDrow/segDcol/foot 참조를 target 으로 교체. int1/int2/measurePointP/L_cross/L_datum 산출은 무변경.
- `WPF_Example/Custom/Sequence/Inspection/Measurements/DualImageEdgeDistanceMeasurement.cs` - `collectedEdgePoints` 수집 + per-point unsigned projection 평균 (`nPts==0` 폴백 포함). LineROI/기준선 무변경.
- `WPF_Example/Custom/Sequence/Inspection/Measurements/EdgeToLineDistanceMeasurement.cs` - `collectedEdgePoints` 수집 + per-point signed projection 평균 (3분기 부호식 보존, 정규화 루프 밖 1회). 레거시 affine 폴백 무변경 + "왜" 주석 추가.
- `WPF_Example/Custom/Sequence/Inspection/Measurements/ArcEdgeDistanceMeasurement.cs` - `collectedEdgePoints` 수집 + `ComputeProjectionDistance` per-point 호출 평균(fOk 게이트). datum 기준선 오버레이 무변경.

## Decisions Made

- ArcLineIntersect: "Close" → 좌 교점(int1), 그 외("Far"/기본/공백/미설정) → 우 교점(int2). 대소문자 무시(`OrdinalIgnoreCase`). 기본값 유지로 기존 INI 레시피 회귀 0.
- 3종 측정 모두 `nPts>=1` 이면 평균값(mm)과 표시용 평균 좌표(pointRow/Col, footRow/Col)를 재대입하고, `nPts==0`(수집 리스트 비었거나 전 투영 실패)이면 기존 단일-중점 코드 경로를 그대로 재실행하는 방식으로 폴백을 구현 — 크래시/NaN/0-나눗셈 없이 완전한 회귀 방지.
- EdgeToLineDistance 레거시 else(affine 변환) 경로는 의도적으로 무변경 — affine 변환이 선형 사상이므로 per-point 평균과 단일 중점 변환이 수학적으로 동일하여 per-point 평균화가 불필요함.

## Deviations from Plan

None - plan executed exactly as written. 파일 스타일(Allman 브레이스, 로컬 명명 규칙) 그대로 유지, 새 헬퍼/추상화 없이 각 파일 인라인으로만 구현.

## Issues Encountered

- `WPF_Example/DatumMeasurement.csproj` 의 `Debug|x64` `DefineConstants` 가 빌드 실행 후 로컬 작업 트리에서 `TRACE;DEBUG` → `TRACE;DEBUG;SIMUL_MODE` 로 변경된 상태로 남아있음(diff 확인됨). 이는 본 세션의 4개 측정 파일 수정과 무관한 이 PC(카메라 없는 SIMUL 전용) 특유의 로컬 오버라이드로, 파일 내 기존 주석(`260610 hbk 이 PC 는 카메라 없는 SIMUL 전용...`)이 이를 언급하고 있다. 플랜의 `files_modified` 목록에 없으므로 스코프 밖으로 판단해 커밋에서 제외했다(unstaged 로 남김). 별도 조치 불필요.

## User Setup Required

None - no external service configuration required.

## Build Verification

- Tool: `MSBuild.exe` (Visual Studio 2022 계열, `C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe`) — `where msbuild` 로는 발견되지 않아 `find` 로 직접 경로 확인 후 사용.
- Command: `MSBuild.exe DatumMeasurement.sln /p:Configuration=Debug /p:Platform=x64 /t:Build /v:m`
- Result (after each of the 4 task commits): 빌드 성공(`DatumMeasurement -> ...\bin\x64\Debug\DatumMeasurement.exe`), 신규 에러/경고 0. 유일하게 나타난 경고 5건(`CS0618`×4, `CS0162`×1)은 `Sequence_Top.cs`/`Sequence_Bottom.cs`/`SequenceHandler.cs`/`VirtualCamera.cs` 의 사전 존재하는 Phase 33 관련 obsolete/unreachable-code 경고로, 본 4개 측정 파일과 무관.
- HALCON 24.11 dotnet35 어셈블리(`halcondotnet.dll`) 존재 확인 후 빌드 진행 — 실 HW 카메라 SDK 는 검증 대상 아님(순수 컴파일 검증).

## Next Phase Readiness

- 4개 측정 파일 모두 빌드 검증 완료, 개별 커밋 완료. 실제 레시피(main.ini) 대상 육안/현장 UAT 는 이 quick task 범위 밖 — 별도 UAT 계획에서 Close/Far 레시피 재실측 및 per-point 평균 노이즈 완화 효과 확인 권장.

---
*Phase: 260722-p5d*
*Completed: 2026-07-22*

## Self-Check: PASSED

- FOUND: WPF_Example/Custom/Sequence/Inspection/Measurements/ArcLineIntersectDistanceMeasurement.cs
- FOUND: WPF_Example/Custom/Sequence/Inspection/Measurements/DualImageEdgeDistanceMeasurement.cs
- FOUND: WPF_Example/Custom/Sequence/Inspection/Measurements/EdgeToLineDistanceMeasurement.cs
- FOUND: WPF_Example/Custom/Sequence/Inspection/Measurements/ArcEdgeDistanceMeasurement.cs
- FOUND: .planning/quick/260722-p5d-fix-arclineintersectdistancemeasurement-/260722-p5d-SUMMARY.md
- FOUND commit: 9d840ec
- FOUND commit: 565aac7
- FOUND commit: fe1b72f
- FOUND commit: c77a165
