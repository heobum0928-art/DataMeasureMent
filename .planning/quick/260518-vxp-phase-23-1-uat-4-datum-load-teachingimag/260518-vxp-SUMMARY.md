---
phase: quick-260518-vxp
plan: 01
subsystem: inspection-ui
tags: [datum, propertygrid, roi-overlay, tolerance, uat-fix]
requires: []
provides:
  - "DatumConfig IOfflineImageParam — TeachingImagePath get/set"
  - "MeasurementBase.EvaluateJudgement 절대값 공차 처리"
  - "FAIConfig 동적 FAI 모드 레거시 Edge 파라미터 PropertyGrid 숨김"
  - "HalconDisplayService Rect/Circle/Polygon ROI 명칭 라벨"
  - "MainView.HighlightSelectedRoi — 선택 노드 ROI 하이라이트"
affects:
  - WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs
  - WPF_Example/Custom/Sequence/Inspection/MeasurementBase.cs
  - WPF_Example/Custom/Sequence/Inspection/FAIConfig.cs
  - WPF_Example/Halcon/Display/HalconDisplayService.cs
  - WPF_Example/UI/ContentItem/MainView.xaml.cs
  - WPF_Example/UI/ControlItem/InspectionListView.xaml.cs
tech-stack:
  added: []
  patterns:
    - "ShotConfig IOfflineImageParam 패턴을 DatumConfig 에 동일 적용 (backing 만 TeachingImagePath 로 교체)"
    - "BuildFilteredProperties hideFunc OR 확장 — 인스턴스 컨텍스트(Measurements.Count) 기반 동적 숨김"
    - "DrawRoiLabelAt 재사용으로 Rect/Circle/Polygon 3-shape 라벨 통일"
key-files:
  created: []
  modified:
    - WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs
    - WPF_Example/Custom/Sequence/Inspection/MeasurementBase.cs
    - WPF_Example/Custom/Sequence/Inspection/FAIConfig.cs
    - WPF_Example/Halcon/Display/HalconDisplayService.cs
    - WPF_Example/UI/ContentItem/MainView.xaml.cs
    - WPF_Example/UI/ControlItem/InspectionListView.xaml.cs
decisions:
  - "메인 작업 트리(HEAD=f4029d0) 에서 직접 실행 — 이전 worktree 실행은 옛 커밋 5727bd8 기반으로 폐기됨"
  - "MeasurementBase / FAIConfig 실제 경로는 WPF_Example/Custom/Sequence/Inspection/ — PLAN frontmatter 의 Sequence/Action/ 경로는 오류"
  - "DatumConfig.TeachingImagePath / FAIConfig.BuildFilteredProperties+IsHiddenForEdgeMeasureType / ICustomTypeDescriptor 인프라는 메인 트리에 모두 실재 — 기존 구조를 확장 (신규 생성 아님)"
  - "LoadAndDisplay 1-인자 오버로드를 2-인자 위임으로 축약 — 코드 중복 제거"
  - "IsLegacyEdgeParam 은 *List(ItemsSource) 이름을 매칭하지 않음 — 콤보 깨짐 회피"
  - "halconViewer 는 MainResultViewerControl 타입 — 4-인자 UpdateDisplayState(rois, selId, ...) 오버로드 사용 (기존 1172/1203 라인 패턴과 일치)"
metrics:
  duration: ~20min
  completed: 2026-05-18
---

# Quick 260518-vxp: Phase 23.1 UAT 4건 (Datum Load / 공차 / FAI Edge 숨김 / ROI 라벨) Summary

Phase 23.1 EdgeToLineDistance ROI 티칭 SIMUL UAT 후 발견된 UI/UX 갭 4건(#3, #Tol, #4, #6)을 메인 작업 트리에서 직접 수정. 알고리즘 로직 무변경 — persistence / PropertyGrid 노출 / 캔버스 시각화 / 입력 UX 갭만 정리.

## Tasks Completed

### Task 1: #3 Datum Load 경로 persistence + #Tol 공차 절대값 처리 (commit 2260353)
- `DatumConfig` 클래스 선언에 `IOfflineImageParam` 추가. 기존 `TeachingImagePath` (Phase 22 IMG-01) 필드를 backing 으로 `GetLatestImagePath`/`SetLatestImagePath` 구현 — ShotConfig 의 IOfflineImageParam 패턴 동일.
- `MainView.LoadAndDisplay` 2-인자 오버로드 `(ICameraParam displayParam, IOfflineImageParam pathSinkParam)` 추가 — 표시용/경로저장용 param 분리, `pathSinkParam == null` 이면 skip. 기존 1-인자 진입점은 `(param, param as IOfflineImageParam)` 위임으로 축약.
- `InspectionListView.button_loadImage_Click` Datum 분기가 `LoadAndDisplay(resolved, datumForLoad)` 호출 — 표시는 Shot 위임, 경로는 `DatumConfig.TeachingImagePath` 저장.
- `MeasurementBase.EvaluateJudgement` 가 `NominalValue - Abs(ToleranceMinus)` / `NominalValue + Abs(TolerancePlus)` 처리 — 공차 입력 부호 무관(음수/양수/비대칭 동일·정상 결과, INI 하위호환). `TolerancePlus`/`ToleranceMinus` 에 `[Description]` attribute 추가, XML 주석 갱신.

### Task 2: #4 FAI 레거시 Edge 탭 동적 숨김 (commit 9ff516b)
- `FAIConfig.BuildFilteredProperties` 의 `hideFunc` 람다 확장 — 기존 `IsHiddenForEdgeMeasureType` 호출에 `hasDynamicMeasurements (Measurements.Count > 0) && IsLegacyEdgeParam(name)` 를 OR 결합.
- `IsLegacyEdgeParam` private static 헬퍼 신규 — EdgeMeasureType/EdgeThreshold/Sigma/EdgeDirection/EdgeSelection/EdgeSampleCount/EdgeTrimCount/EdgePolarity 매칭. `*List` (ItemsSource) 는 매칭 제외.
- INI 직렬화는 `GetType().GetProperties()` reflection 경로 사용 → 영향 0. 비동적 FAI(Measurement 0개)는 그대로 노출 — 회귀 0.

### Task 3: #6 ROI 선택 하이라이트 + 명칭 라벨 (commit 1fcaed6)
- `HalconDisplayService` Render rois 루프에 Rectangle/Circle/Polygon 3종 ROI 명칭 라벨 추가 — `DrawRoiLabelAt` (yellow 텍스트) 재사용. Rect: 좌상단 외곽 위, Circle: 원 상단, Polygon: 첫 점 위 (각 22px).
- `MainView.HighlightSelectedRoi(ParamBase)` public 메서드 신규 — FAIConfig 면 `FAIName`, MeasurementBase 면 `FindFaiNameContainingMeasurement` 로 소유 FAI 도출 후 `FAIName + "_" + (MeasurementName ?? TypeName)` 으로 ROI Id 조립 (GetCurrentFAIRois 의 Id 규칙과 일치). `halconViewer.UpdateDisplayState(rois, selRoiId, null, null)` 호출.
- `InspectionListView.InspectionList_SelectionChanged` Measurement/FAI 분기에서 `HighlightSelectedRoi` 호출.
- 선택 ROI 노란색/width 3 분기는 Render 루프에 이미 존재 — `selectedRoiId == roi.Id` 매칭.

## Deviations from Plan

### Rule 3 — Auto-fix blocking issues

**1. [Rule 3 - Blocking] MeasurementBase / FAIConfig 실제 경로**
- **Found during:** Task 1 / Task 2
- **Issue:** PLAN.md frontmatter 가 `WPF_Example/Sequence/Action/MeasurementBase.cs` 로 지정했으나 실제 경로는 `WPF_Example/Custom/Sequence/Inspection/MeasurementBase.cs`. critical_context 에서 이미 지적된 사항.
- **Fix:** 실제 경로의 파일을 Read 후 수정.

**참고:** `InspectionListView.xaml.cs` 는 PLAN frontmatter 의 files_modified 에 없었으나 PLAN action 본문(#3 Datum 분기, #6 selection 분기)이 명시적으로 수정을 요구했고 critical_context 지침대로 함께 수정함.

### 메모

이전 worktree 실행자가 "DatumConfig.TeachingImagePath / FAIConfig ICustomTypeDescriptor 인프라 부재" 로 보고했던 것은 stale worktree(옛 커밋 5727bd8) 때문이었다. 메인 트리(f4029d0)에는 해당 구조가 모두 실재하며, 본 실행은 기존 구조를 확장했다 (신규 생성 0, CS0102/CS0111 중복 선언 0).

## Verification

- **빌드:** `msbuild WPF_Example/DatumMeasurement.csproj /p:Configuration=Debug /p:Platform=x64 /t:Rebuild` — **0 error**. 경고는 baseline 2건만 (MSB3884 ruleset 누락, VirtualCamera.cs:266 CS0162 unreachable) — 본 수정과 무관, out-of-scope.
- `DatumMeasurement.exe` 생성 확인 (WPF_Example/bin/x64/Debug/, 2026-05-18 23:21).
- **#3:** DatumConfig 가 IOfflineImageParam 구현, Datum 노드 Load 시 TeachingImagePath 기록. Shot 노드 Load 는 SimulImagePath 무변경 (회귀 0).
- **#Tol:** EvaluateJudgement 가 Abs 처리 — 양수/음수/비대칭 공차 입력 모두 정상.
- **#4:** Measurement 보유 FAI → 레거시 Edge 파라미터 숨김. 비동적 FAI → 그대로 노출. INI 직렬화 무변경.
- **#6:** Rect/Circle/Polygon 명칭 라벨 + 선택 ROI 노란색 하이라이트 배선 완료.
- SIMUL_MODE 수동 UAT 는 사용자 수행 대상 (plan verify 가 MISSING — automated 불가).

## Known Stubs

None.

## Self-Check: PASSED

- FOUND: WPF_Example/Custom/Sequence/Inspection/DatumConfig.cs
- FOUND: WPF_Example/Custom/Sequence/Inspection/MeasurementBase.cs
- FOUND: WPF_Example/Custom/Sequence/Inspection/FAIConfig.cs
- FOUND: WPF_Example/Halcon/Display/HalconDisplayService.cs
- FOUND: WPF_Example/UI/ContentItem/MainView.xaml.cs
- FOUND: WPF_Example/UI/ControlItem/InspectionListView.xaml.cs
- FOUND commit: 2260353
- FOUND commit: 9ff516b
- FOUND commit: 1fcaed6
- 빌드 0 error 확인.
