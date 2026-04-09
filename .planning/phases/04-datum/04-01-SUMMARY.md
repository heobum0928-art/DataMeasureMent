---
phase: 04-datum
plan: 01
subsystem: inspection-datum
tags: [datum, halcon, ini, shot-config, edge-measurement]
dependency_graph:
  requires: []
  provides: [DatumConfig, DatumFindingService, ShotConfig.Datum, INI-DATUM-section]
  affects: [InspectionRecipeManager, ShotConfig, Action_FAIMeasurement]
tech_stack:
  added: [DatumFindingService, DatumConfig]
  patterns: [ParamBase-serialization, GenMeasureRectangle2-MeasurePos-FitLineContourXld-IntersectionLl, hom_mat2d]
key_files:
  created:
    - WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs
    - WPF_Example/Halcon/Algorithms/DatumFindingService.cs
  modified:
    - WPF_Example/Custom/Sequence/Inspection/ShotConfig.cs
    - WPF_Example/Custom/Sequence/Inspection/InspectionRecipeManager.cs
    - WPF_Example/DatumMeasurement.csproj
decisions:
  - "DatumConfig : ParamBase 상속으로 INI 직렬화 자동화 (HTuple은 switch-case에 없으므로 런타임 전용)"
  - "IsConfigured=false이면 identity hom_mat2d 반환 (D-08 pass-through)"
  - "AddShot()에서 DatumConfig 자동 생성 — null 방어 코드 최소화"
  - "Load()는 DATUM 섹션 존재 시만 DatumConfig 생성 — 기존 레시피 하위 호환"
metrics:
  duration_minutes: 25
  completed_date: "2026-04-09"
  tasks_completed: 2
  files_created: 2
  files_modified: 3
---

# Phase 4 Plan 01: Datum Infrastructure Summary

**One-liner:** DatumConfig(ParamBase) + DatumFindingService(TryFindDatum/TryTeachDatum) — GenMeasureRectangle2→MeasurePos→FitLineContourXld→IntersectionLl→hom_mat2d 파이프라인으로 Datum 교점 기반 좌표 보정 인프라 구축

## What Was Built

### DatumConfig.cs (신규)
`ParamBase` 상속 데이터 모델. INI 자동 직렬화 대상 필드 17개:
- **Line1 ROI** 5개 double (Row/Col/Phi/Length1/Length2)
- **Line2 ROI** 5개 double (기본 Phi=1.5708, 수직 방향)
- **Reference** 3개 double (RefOriginRow/Col/AngleRad)
- **Edge Detection** 3개 (EdgeThreshold int, Sigma double, EdgePolarity string)
- **Status** 1개 (IsConfigured bool)
- 런타임 전용 2개: `CurrentTransform HTuple`, `LastFindSucceeded bool` (ParamBase 직렬화 skip)

### DatumFindingService.cs (신규)
- `TryFindDatum()`: 런타임 교점 찾기 → hom_mat2d 변환 반환. IsConfigured=false → identity pass-through (D-08).
- `TryTeachDatum()`: 티칭 교점 찾기 → config.RefOriginRow/Col/AngleRad 저장, IsConfigured=true (D-13).
- `TryFindLine()`: GenMeasureRectangle2 → MeasurePos → GenContourPolygonXld → FitLineContourXld. MeasureHandle은 finally에서 항상 해제 (T-04-02).
- `IntersectionLl`: 두 피팅 라인 교점 계산, 평행 검사(isOverlapping==1).
- `HomMat2dIdentity/Translate/Rotate`: 평행이동+회전 hom_mat2d 빌드 (D-07).

### ShotConfig.cs (수정)
- `public DatumConfig Datum { get; set; }` 추가 ([Browsable(false)])
- `ClearAllResults()`: Datum.CurrentTransform=null, Datum.LastFindSucceeded=false 초기화

### InspectionRecipeManager.cs (수정)
- `AddShot()`: `shot.Datum = new DatumConfig(shot)` 자동 생성
- `Save()`: FAI 루프 후 `[SHOT_{s}_DATUM]` 섹션에 Datum 저장
- `Load()`: `ContainsSection(datumSection)` 검사 후 조건부 로드 (하위 호환 — 기존 레시피 null 유지)

## Deviations from Plan

None — plan executed exactly as written.

## Known Stubs

None. DatumFindingService는 완전 구현됨. Phase 4 Plan 02에서 Action_FAIMeasurement 파이프라인 통합 예정.

## Threat Flags

None. 모든 T-04-xx 항목은 계획된 수준에서 처리됨:
- T-04-02: MeasureHandle finally 해제 구현 완료

## Self-Check: PASSED

- [x] `WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs` — FOUND
- [x] `WPF_Example/Halcon/Algorithms/DatumFindingService.cs` — FOUND
- [x] `ShotConfig.cs` — `public DatumConfig Datum` FOUND
- [x] `InspectionRecipeManager.cs` — `SHOT_{s}_DATUM` FOUND
- [x] Commit b4d1420 — Task 1 (DatumConfig + ShotConfig)
- [x] Commit 7a0753c — Task 2 (DatumFindingService + InspectionRecipeManager)
- [x] Build: main project 빌드 성공 (error CS 없음, MC3074는 worktree DLL 환경 문제로 pre-existing)
