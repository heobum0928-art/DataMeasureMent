---
phase: 04-datum
plan: 02
subsystem: inspection-datum
tags: [datum, halcon, fai, measurement, ui, tree-node, property-grid, overlay]
dependency_graph:
  requires: [DatumConfig, DatumFindingService, ShotConfig.Datum]
  provides: [FAIEdgeMeasurementService.TryMeasure-transform, Action_FAIMeasurement.FindDatum, ENodeType.Datum, HalconDisplayService.RenderDatumOverlay]
  affects: [Action_FAIMeasurement, FAIEdgeMeasurementService, InspectionListViewModel, InspectionListView, HalconDisplayService, Node]
tech_stack:
  added: []
  patterns: [AffineTransPoint2d-ROI-transform, hom_mat2d-pass-through, ParamBase-PropertyGrid-auto-bind, DispRectangle2-overlay]
key_files:
  created: []
  modified:
    - WPF_Example/Halcon/Algorithms/FAIEdgeMeasurementService.cs
    - WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs
    - WPF_Example/UI/ControlItem/Node.cs
    - WPF_Example/UI/ControlItem/InspectionListViewModel.cs
    - WPF_Example/UI/ControlItem/InspectionListView.xaml.cs
    - WPF_Example/Halcon/Display/HalconDisplayService.cs
decisions:
  - "기존 TryMeasure(image, fai)를 TryMeasure(image, fai, null) 위임으로 변환 — 모든 Phase 3 호출자 무수정 (D-08)"
  - "measurePhi 회전 보정 = roiPhi - fai.ROI_Phi 차분 적용 — Datum 회전이 스캔 방향에도 반영"
  - "DatumConfig : ParamBase 이므로 SetParam 경로로 PropertyGrid 자동 바인딩 — 추가 코드 불필요 (D-10)"
  - "RenderDatumOverlay는 독립 메서드로 제공 — MainView 통합은 다음 Phase에서 call-site 결정"
metrics:
  duration_minutes: 30
  completed_date: "2026-04-09"
  tasks_completed: 2
  files_created: 0
  files_modified: 6
---

# Phase 4 Plan 02: Datum Integration Summary

**One-liner:** FAIEdgeMeasurementService에 AffineTransPoint2d ROI 변환 오버로드 추가 + Action_FAIMeasurement에 FindDatum 선행 호출 + ENodeType.Datum UI 트리 노드 + RenderDatumOverlay 캔버스 오버레이로 Datum 좌표 보정 시스템 완전 통합

## What Was Built

### FAIEdgeMeasurementService.cs (수정)

기존 `TryMeasure(HImage, FAIConfig, out result)` → `TryMeasure(image, fai, null, out result)` 위임으로 변환.

새 오버로드 `TryMeasure(HImage, FAIConfig, HTuple transform, out result)` 추가:
- transform != null 이고 Length > 0 이면 `HOperatorSet.AffineTransPoint2d`로 ROI 중심(Row/Col) 변환
- 회전 성분 추출: `rotAngle = Math.Atan2(-transform[1].D, transform[0].D)`, `roiPhi = fai.ROI_Phi + rotAngle`
- measurePhi도 roiPhi 기반으로 차분 보정 적용
- transform 적용 실패 시 try/catch로 원본 ROI 사용 (T-04-05 fallback)
- sinPhi/cosPhi AABB 계산, top/bottom/left/right 모두 roiRow/roiCol/roiPhi 사용

### Action_FAIMeasurement.cs (수정)

`case EStep.Measure:` 내 FAI 루프 전에 Datum 선행 처리 삽입:
- `ShotParam.Datum != null && ShotParam.Datum.IsConfigured` 조건부 실행 (D-08 pass-through)
- `DatumFindingService.TryFindDatum(image, ShotParam.Datum, out datumTransform, out datumError)` 호출
- 실패 시: 전체 FAI `ClearResult()` + `AllPass = false` + `Step = EStep.End` (D-17)
- 성공 시: `LastFindSucceeded = true`, `CurrentTransform = datumTransform` 저장
- FAI 루프 내 `service.TryMeasure(image, fai, datumTransform, out r)` — null이면 identity (D-08)

### Node.cs (수정)

- `ENodeType.Datum` 열거값 추가
- `ImageSource` getter에 `case ENodeType.Datum: return "/Resource/layout.png"` 추가 (D-09)

### InspectionListViewModel.cs (수정)

`CreateSequenceNode()` 내 `if (act.Param is ShotConfig shot)` 블록에서 FAI 루프 전 Datum 노드 삽입:
- `shot.Datum != null` 조건부 `CompositeNode { Name="Datum", NodeType=ENodeType.Datum, ParamData=shot.Datum }` 생성
- FAI 노드보다 먼저 `actNode.Children.Add(datumNode)` — 트리에서 Shot 첫 번째 자식

### InspectionListView.xaml.cs (수정)

`InspectionList_SelectionChanged` 핸들러에 `ENodeType.Datum` 분기 추가:
- `button_addFAI/removeFAI/renameFAI.IsEnabled = false`
- `_inspectionVm?.ClearResults()`
- PropertyGrid 바인딩: `DatumConfig : ParamBase` 이므로 기존 `SetParam` 경로에서 자동 처리 (D-10)

### HalconDisplayService.cs (수정)

`using ReringProject.Sequence` 추가 및 `RenderDatumOverlay(HWindow, DatumConfig, bool isSelected)` 메서드 추가:
- isSelected=true: "cyan" 3px, false: "blue" 2px
- Line1_Length1/2 > 0이면 `HOperatorSet.DispRectangle2` (Line1 ROI)
- Line2_Length1/2 > 0이면 `HOperatorSet.DispRectangle2` (Line2 ROI)
- `datum.IsConfigured`이면 RefOriginRow/Col 기준 magenta 십자 + "Datum Origin" 레이블 (D-12)

## Deviations from Plan

### Auto-fixed Issues

None.

### Plan Adjustments

**measurePhi 회전 보정 추가 (Plan 명세 보완)**
- Plan 명세는 `roiPhi = fai.ROI_Phi + rotAngle` 계산만 언급했으나, measurePhi(스캔 방향)도 동일 회전량으로 보정해야 Datum 회전이 에지 스캔 방향에 반영됨
- `measurePhi = measurePhi + (roiPhi - fai.ROI_Phi)` 차분 추가 (correctness requirement)

## Known Stubs

None. RenderDatumOverlay는 완전 구현됨. MainView에서의 call-site 통합(선택 시 오버레이 표시)은 Phase 5에서 결정 예정이나, 이는 메서드가 없어서가 아닌 UI 통합 설계 결정 사항임.

## Threat Flags

None. 계획된 T-04-05~T-04-07 모두 처리됨:
- T-04-05: AffineTransPoint2d 실패 시 try/catch + 원본 ROI fallback 구현 완료
- T-04-06: PropertyGrid 로컬 전용, 네트워크 노출 없음 (accept)
- T-04-07: Display-only rendering (accept)

## Self-Check: PASSED

- [x] `WPF_Example/Halcon/Algorithms/FAIEdgeMeasurementService.cs` — `AffineTransPoint2d` FOUND, `HTuple transform` overload FOUND
- [x] `WPF_Example/Custom/Sequence/Inspection/Action_FAIMeasurement.cs` — `DatumFindingService` FOUND, `TryFindDatum` FOUND, `datumTransform` FOUND
- [x] `WPF_Example/UI/ControlItem/Node.cs` — `ENodeType.Datum` FOUND
- [x] `WPF_Example/UI/ControlItem/InspectionListViewModel.cs` — `ENodeType.Datum` FOUND, `Name = "Datum"` FOUND
- [x] `WPF_Example/UI/ControlItem/InspectionListView.xaml.cs` — `ENodeType.Datum` FOUND
- [x] `WPF_Example/Halcon/Display/HalconDisplayService.cs` — `RenderDatumOverlay` FOUND, `DispRectangle2` FOUND
- [x] Commit 76157d2 — Task 1 (FAIEdgeMeasurementService + Action_FAIMeasurement)
- [x] Commit 99c900e — Task 2 (Node + InspectionListViewModel + InspectionListView + HalconDisplayService)
- [x] Build: CS error 0개 (MC3074는 worktree DLL 환경 문제로 pre-existing)
